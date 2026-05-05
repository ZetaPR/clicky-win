const CORS_HEADERS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type',
};

const ANTHROPIC_URL = 'https://api.anthropic.com/v1/messages';
const ANTHROPIC_VERSION = '2023-06-01';
const CLAUDE_MODEL = 'claude-sonnet-4-6';
const MAX_TOKENS = 1024;

const SYSTEM_PROMPT =
  'You are Clicky, a helpful AI assistant. The user has shared their screen and spoken a request. Answer concisely and helpfully based on what you see and hear.';

/**
 * Convert an ArrayBuffer to a base64 string.
 * @param {ArrayBuffer} buffer
 * @returns {string}
 */
function arrayBufferToBase64(buffer) {
  const bytes = new Uint8Array(buffer);
  let binary = '';
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary);
}

/**
 * Parse a multipart/form-data request and extract screenshot and transcript fields.
 * @param {Request} request
 * @returns {Promise<{ screenshot: ArrayBuffer | null, transcript: string | null }>}
 */
async function parseMultipart(request) {
  const formData = await request.formData();
  const screenshotFile = formData.get('screenshot');
  const transcriptValue = formData.get('transcript');

  const screenshot = screenshotFile ? await screenshotFile.arrayBuffer() : null;
  const transcript = transcriptValue ?? null;

  return { screenshot, transcript };
}

/**
 * Build the Anthropic messages payload.
 * @param {string} imageBase64
 * @param {string} transcript
 * @returns {object}
 */
function buildAnthropicPayload(imageBase64, transcript) {
  return {
    model: CLAUDE_MODEL,
    max_tokens: MAX_TOKENS,
    stream: true,
    system: SYSTEM_PROMPT,
    messages: [
      {
        role: 'user',
        content: [
          {
            type: 'image',
            source: {
              type: 'base64',
              media_type: 'image/jpeg',
              data: imageBase64,
            },
          },
          {
            type: 'text',
            text: transcript,
          },
        ],
      },
    ],
  };
}

/**
 * Call the Anthropic API with the given payload and API key.
 * @param {object} payload
 * @param {string} apiKey
 * @returns {Promise<Response>}
 */
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

/**
 * Handle CORS preflight OPTIONS requests.
 * @returns {Response}
 */
function handleOptions() {
  return new Response(null, {
    status: 204,
    headers: CORS_HEADERS,
  });
}

/**
 * Return a JSON error response with CORS headers.
 * @param {string} message
 * @param {number} status
 * @returns {Response}
 */
function errorResponse(message, status) {
  return new Response(JSON.stringify({ error: message }), {
    status,
    headers: {
      ...CORS_HEADERS,
      'content-type': 'application/json',
    },
  });
}

/**
 * Handle POST requests: parse multipart, call Anthropic, stream SSE back.
 * @param {Request} request
 * @param {{ ANTHROPIC_API_KEY: string }} env
 * @returns {Promise<Response>}
 */
async function handlePost(request, env) {
  let screenshot, transcript;

  try {
    ({ screenshot, transcript } = await parseMultipart(request));
  } catch (err) {
    console.error('Failed to parse multipart form data:', err.message);
    return errorResponse('Failed to parse request body', 400);
  }

  if (!screenshot) {
    return errorResponse('Missing required field: screenshot', 400);
  }

  if (!transcript) {
    return errorResponse('Missing required field: transcript', 400);
  }

  const imageBase64 = arrayBufferToBase64(screenshot);
  const payload = buildAnthropicPayload(imageBase64, transcript);

  const anthropicResponse = await callAnthropic(payload, env.ANTHROPIC_API_KEY);

  if (!anthropicResponse.ok) {
    const errorBody = await anthropicResponse.text();
    return new Response(errorBody, {
      status: anthropicResponse.status,
      headers: {
        ...CORS_HEADERS,
        'content-type': anthropicResponse.headers.get('content-type') ?? 'application/json',
      },
    });
  }

  return new Response(anthropicResponse.body, {
    status: 200,
    headers: {
      ...CORS_HEADERS,
      'content-type': 'text/event-stream',
      'cache-control': 'no-cache',
    },
  });
}

export default {
  /**
   * Main Cloudflare Worker fetch handler.
   * @param {Request} request
   * @param {{ ANTHROPIC_API_KEY: string }} env
   * @param {ExecutionContext} ctx
   * @returns {Promise<Response>}
   */
  async fetch(request, env, ctx) {
    if (request.method === 'OPTIONS') {
      return handleOptions();
    }

    if (request.method !== 'POST') {
      return errorResponse('Method not allowed', 405);
    }

    return handlePost(request, env);
  },
};
