# clicky-worker

Cloudflare Worker that acts as a secure proxy between the Clicky Windows app and the Anthropic Claude API.

## What it does

Accepts a `POST /` with a multipart form body containing:
- `screenshot`: binary JPEG image of the user's screen
- `transcript`: UTF-8 text of the user's spoken request

Calls Claude claude-sonnet-4-6 (with vision) via the Anthropic API and streams the Server-Sent Events (SSE) response directly back to the caller.

CORS headers are included so the worker can be called from any origin.

## Environment variables

| Variable            | Type   | Description                          |
|---------------------|--------|--------------------------------------|
| `ANTHROPIC_API_KEY` | Secret | Anthropic API key (required)         |

## Deploy

```bash
npm install
wrangler secret put ANTHROPIC_API_KEY
wrangler deploy
```

## Test locally

```bash
wrangler dev
```

The worker will be available at `http://localhost:8787`.

Example request with `curl`:

```bash
curl -X POST http://localhost:8787 \
  -F "screenshot=@/path/to/screenshot.jpg" \
  -F "transcript=What is on my screen?"
```

## Run unit tests

```bash
node --test index.test.js
```
