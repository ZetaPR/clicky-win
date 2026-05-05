import { describe, it, before, after } from 'node:test';
import assert from 'node:assert/strict';

// We need to mock globalThis.fetch before importing the worker so that
// the callAnthropic function uses our mock. We store and restore it around tests.

let worker;

// Helper: build a multipart/form-data Request with screenshot + transcript
function makePostRequest({ screenshot, transcript } = {}) {
  const formData = new FormData();

  if (screenshot !== undefined) {
    const blob = new Blob([screenshot], { type: 'image/jpeg' });
    formData.append('screenshot', blob, 'screenshot.jpg');
  }

  if (transcript !== undefined) {
    formData.append('transcript', transcript);
  }

  return new Request('http://localhost/', {
    method: 'POST',
    body: formData,
  });
}

// Fake SSE body streamed back from Anthropic
const FAKE_SSE_BODY = 'data: {"type":"content_block_delta","delta":{"type":"text_delta","text":"Hello"}}\n\n';

before(async () => {
  // Dynamic import so we can set up mocks before other tests run.
  // The worker module is pure — it only calls globalThis.fetch at handler time.
  const mod = await import('./index.js');
  worker = mod.default;
});

describe('OPTIONS preflight', () => {
  it('should return 204 with CORS headers', async () => {
    const request = new Request('http://localhost/', { method: 'OPTIONS' });
    const env = { ANTHROPIC_API_KEY: 'test-key' };

    const response = await worker.fetch(request, env, {});

    assert.equal(response.status, 204);
    assert.equal(response.headers.get('Access-Control-Allow-Origin'), '*');
    assert.ok(response.headers.get('Access-Control-Allow-Methods').includes('POST'));
  });
});

describe('POST validation', () => {
  it('should return 400 when screenshot field is missing', async () => {
    const request = makePostRequest({ transcript: 'What is on my screen?' });
    const env = { ANTHROPIC_API_KEY: 'test-key' };

    const response = await worker.fetch(request, env, {});

    assert.equal(response.status, 400);
    const body = await response.json();
    assert.ok(body.error.includes('screenshot'));
  });

  it('should return 400 when transcript field is missing', async () => {
    const request = makePostRequest({ screenshot: new Uint8Array([0xff, 0xd8, 0xff]) });
    const env = { ANTHROPIC_API_KEY: 'test-key' };

    const response = await worker.fetch(request, env, {});

    assert.equal(response.status, 400);
    const body = await response.json();
    assert.ok(body.error.includes('transcript'));
  });
});

describe('POST success — streams Anthropic SSE', () => {
  let originalFetch;

  before(() => {
    originalFetch = globalThis.fetch;

    // Mock fetch to intercept the Anthropic API call
    globalThis.fetch = async (url, options) => {
      if (url === 'https://api.anthropic.com/v1/messages') {
        // Verify the request is constructed correctly
        const body = JSON.parse(options.body);
        assert.equal(body.model, 'claude-sonnet-4-6');
        assert.equal(body.stream, true);
        assert.equal(options.headers['x-api-key'], 'test-key');
        assert.equal(options.headers['anthropic-version'], '2023-06-01');

        const content = body.messages[0].content;
        const imageBlock = content.find((b) => b.type === 'image');
        const textBlock = content.find((b) => b.type === 'text');
        assert.ok(imageBlock, 'image block must be present');
        assert.equal(imageBlock.source.type, 'base64');
        assert.equal(imageBlock.source.media_type, 'image/jpeg');
        assert.ok(textBlock, 'text block must be present');
        assert.equal(textBlock.text, 'What is on my screen?');

        const stream = new ReadableStream({
          start(controller) {
            controller.enqueue(new TextEncoder().encode(FAKE_SSE_BODY));
            controller.close();
          },
        });

        return new Response(stream, {
          status: 200,
          headers: { 'content-type': 'text/event-stream' },
        });
      }

      // Fall through to real fetch for anything else (shouldn't happen in tests)
      return originalFetch(url, options);
    };
  });

  after(() => {
    globalThis.fetch = originalFetch;
  });

  it('should call Anthropic and stream the SSE response back', async () => {
    const jpegBytes = new Uint8Array([0xff, 0xd8, 0xff, 0xe0]);
    const request = makePostRequest({
      screenshot: jpegBytes,
      transcript: 'What is on my screen?',
    });
    const env = { ANTHROPIC_API_KEY: 'test-key' };

    const response = await worker.fetch(request, env, {});

    assert.equal(response.status, 200);
    assert.equal(response.headers.get('content-type'), 'text/event-stream');
    assert.equal(response.headers.get('Access-Control-Allow-Origin'), '*');

    const text = await response.text();
    assert.ok(text.includes('content_block_delta'), 'response body should contain SSE data');
  });
});
