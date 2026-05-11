const CORS_HEADERS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type',
};

const ANTHROPIC_URL = 'https://api.anthropic.com/v1/messages';
const ANTHROPIC_VERSION = '2023-06-01';
const CLAUDE_MODEL = 'claude-sonnet-4-6';
const MAX_TOKENS = 2048;

const PLAN_SYSTEM_PROMPT = `You are Clicky, a helpful AI assistant. The user has shared their screen and spoken a request.

When the user asks how to do something that requires multiple steps (e.g. "how do I save a file", "how do I change my wallpaper"), respond with a step-by-step plan using this exact tag format:

[STEP:1:x,y:label]Spoken instruction for step 1[/STEP]
[STEP:2]Spoken instruction for step 2 (no coords — provided by verify)[/STEP]
[STEP:3]Spoken instruction for step 3[/STEP]

Rules for the [STEP:...] format:
- Step 1 MUST include coordinates x,y pointing to the first UI element to click (in screenshot pixel space). Include an optional label (e.g. "File menu").
- Steps 2+ have NO coordinates — they are resolved from fresh screenshots during verification.
- Keep each step instruction short and spoken naturally, as it will be read aloud.
- Use at most 6 steps.

When the user asks a simple factual question or wants a single-action answer, respond normally and append a [POINT:x,y:label] tag at the end pointing to the relevant screen element, or [POINT:none] if no element applies. Example:
  The save button is in the top toolbar. [POINT:450,32:Save button]

Never mix [STEP:...] and [POINT:...] in the same response.`;

const VERIFY_SYSTEM_PROMPT = `You are Clicky, verifying whether the user completed a step correctly based on a fresh screenshot.

Respond with JSON only (no markdown, no explanation):
{"result":"advance","spokenText":"...","nextX":300,"nextY":400,"nextLabel":"Save As"}

Rules:
- "result" must be one of: "advance", "correct", "complete"
  - "advance": user completed this step correctly; move to the next step. Include nextX/nextY/nextLabel for the next step's target in the screenshot.
  - "correct": user did NOT complete the step; provide spoken correction guidance. No next coords.
  - "complete": the entire task is done (e.g., last step succeeded or user reached the goal early). No next coords.
- "spokenText": what Clicky says aloud — keep it short and natural.
- nextX/nextY are pixel coordinates in the current screenshot pointing to the next step's target.
- nextLabel is a short description of the next target (1–3 words).`;

function arrayBufferToBase64(buffer) {
  const bytes = new Uint8Array(buffer);
  let binary = '';
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
}

function buildPlanPayload(imageBase64, transcript, screenshotWidth, screenshotHeight) {
  return {
    model: CLAUDE_MODEL,
    max_tokens: MAX_TOKENS,
    stream: true,
    system: PLAN_SYSTEM_PROMPT,
    messages: [
      {
        role: 'user',
        content: [
          {
            type: 'image',
            source: { type: 'base64', media_type: 'image/jpeg', data: imageBase64 },
          },
          {
            type: 'text',
            text: `Screenshot dimensions: ${screenshotWidth}x${screenshotHeight} pixels.\n\n${transcript}`,
          },
        ],
      },
    ],
  };
}

function buildVerifyPayload(imageBase64, screenshotWidth, screenshotHeight, stepNumber, stepText, history) {
  const messages = [
    ...history.map(m => ({ role: m.role, content: m.content })),
    {
      role: 'user',
      content: [
        {
          type: 'image',
          source: { type: 'base64', media_type: 'image/jpeg', data: imageBase64 },
        },
        {
          type: 'text',
          text: `Screenshot dimensions: ${screenshotWidth}x${screenshotHeight} pixels.\n\nVerify step ${stepNumber}: "${stepText}". Did the user complete this step? What should happen next?`,
        },
      ],
    },
  ];

  return {
    model: CLAUDE_MODEL,
    max_tokens: 512,
    stream: false,
    system: VERIFY_SYSTEM_PROMPT,
    messages,
  };
}

async function callAnthropic(payload, apiKey) {
  return fetch(ANTHROPIC_URL, {
    method: 'POST',
    headers: {
      'x-api-key': apiKey,
      'anthropic-version': ANTHROPIC_VERSION,
      'content-type': 'application/json',
    },
    body: JSON.stringify(payload),
  });
}

function handleOptions() {
  return new Response(null, { status: 204, headers: CORS_HEADERS });
}

function errorResponse(message, status) {
  return new Response(JSON.stringify({ error: message }), {
    status,
    headers: { ...CORS_HEADERS, 'content-type': 'application/json' },
  });
}

async function handlePost(request, env) {
  let body;
  try {
    body = await request.json();
  } catch {
    return errorResponse('Invalid JSON body', 400);
  }

  const { mode = 'plan', screenshot, transcript, screenshotWidth, screenshotHeight } = body;

  if (!screenshot) return errorResponse('Missing required field: screenshot', 400);

  const imageBuffer = Uint8Array.from(atob(screenshot), c => c.charCodeAt(0));
  const imageBase64 = arrayBufferToBase64(imageBuffer.buffer);

  if (mode === 'verify') {
    const { stepNumber, stepText, history = [] } = body;
    if (!stepNumber || !stepText) return errorResponse('Missing stepNumber or stepText', 400);

    const payload = buildVerifyPayload(imageBase64, screenshotWidth, screenshotHeight, stepNumber, stepText, history);
    const anthropicResponse = await callAnthropic(payload, env.ANTHROPIC_API_KEY);

    if (!anthropicResponse.ok) {
      const errorBody = await anthropicResponse.text();
      return new Response(errorBody, {
        status: anthropicResponse.status,
        headers: { ...CORS_HEADERS, 'content-type': 'application/json' },
      });
    }

    const verifyJson = await anthropicResponse.json();
    const responseText = verifyJson.content?.[0]?.text ?? '{"result":"correct","spokenText":"Could not verify."}';

    return new Response(responseText, {
      status: 200,
      headers: { ...CORS_HEADERS, 'content-type': 'application/json' },
    });
  }

  // mode === 'plan' (default)
  if (!transcript) return errorResponse('Missing required field: transcript', 400);

  const payload = buildPlanPayload(imageBase64, transcript, screenshotWidth ?? 0, screenshotHeight ?? 0);
  const anthropicResponse = await callAnthropic(payload, env.ANTHROPIC_API_KEY);

  if (!anthropicResponse.ok) {
    const errorBody = await anthropicResponse.text();
    return new Response(errorBody, {
      status: anthropicResponse.status,
      headers: { ...CORS_HEADERS, 'content-type': anthropicResponse.headers.get('content-type') ?? 'application/json' },
    });
  }

  return new Response(anthropicResponse.body, {
    status: 200,
    headers: { ...CORS_HEADERS, 'content-type': 'text/event-stream', 'cache-control': 'no-cache' },
  });
}

export default {
  async fetch(request, env, ctx) {
    if (request.method === 'OPTIONS') return handleOptions();
    if (request.method !== 'POST') return errorResponse('Method not allowed', 405);
    return handlePost(request, env);
  },
};
