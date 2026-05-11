# Multi-Step Guidance — Architecture Design

**Date:** 2026-05-11
**Status:** Sections 1–3 approved — design complete, ready for implementation plan

---

## What this feature does

Clicky currently answers single questions. This feature adds guided multi-step workflows: when a user asks "how do I do X?", Clicky returns a step-by-step plan, guides them through each step one at a time, watches for their click, verifies they did it correctly, then advances — or corrects them — automatically.

---

## Section 1: Architecture Overview (APPROVED)

### Existing pipeline — unchanged for single-turn questions

```
PTT press/release → Mic + STT → Screenshot → Worker → Claude → TTS + Overlay → ReturnToIdle
```

### New components

#### `StepPlanStore` (new — `Clicky.Services`)
Holds the current multi-step plan in memory. Tracks:
- Step index (current step)
- Original screenshot
- Conversation history for adaptive calls
- Target window HWND

Clears itself when the sequence ends or times out.

#### `StepClickWatcher` (new — `Clicky.Services`)
Arms itself after a step is delivered. Subscribes to SharpHook's existing `RawMousePressed` event. On a click within ±60px of the target in the right window: triggers the verify flow. Disarms on PTT press, timeout, or sequence end.

#### `StepVerifier` (new — `Clicky.Services`)
Waits 300ms after a click, grabs a fresh screenshot, sends it to the worker with step context and conversation history. Worker returns one of:
- `advance` — move to next step
- `correct` — spoken correction, stay on same step
- `complete` — sequence done early

#### `ForegroundGuard` (new — wired into existing `OnMainTick`)
~5 lines in the existing 60fps timer. Checks `GetForegroundWindow()`. Fires the drift/timeout state machine when the user leaves the target app. Uses existing P/Invoke infrastructure in `CursorOverlayWindow`.

#### Overlay changes — `StepperBadge` + stay-pointed triangle (`Clicky.Overlay`)
Two additions to `CursorOverlayWindow`:

1. **Triangle stays pinned** at the target coordinate and pulses between steps — new `OverlayState.WaitingForStep` state that holds `_animEnd` and renders a slow pulse animation.
2. **"Step 2 of 4" badge** in the top-right corner that repositions itself away from the target coordinate if they would overlap (simple bounding-box check at render time).

### `CompanionOrchestrator` — what changes

After `StreamLlmToTtsAsync` completes, instead of always calling `ReturnToIdle()`, orchestrator checks the parsed result:

```
if result is single-turn       → ReturnToIdle()                          // unchanged
if result is multi-step plan   → StepPlanStore.Load(plan) → DeliverStep(0)
if StepVerifier returns advance  → DeliverStep(n+1)
if StepVerifier returns correct  → TTS(correction) → re-arm StepClickWatcher
if StepVerifier returns complete → TTS(done message) → ReturnToIdle()
if PTT pressed                 → cancel pipeline → cancel sequence
```

### Cloudflare Worker — what changes

Two new request modes alongside the existing single-turn mode:

**`mode: "plan"`**
First call. Returns full step plan in new tag format. Sets up conversation history.

**`mode: "verify"`**
Step verification call. Sends new screenshot + history + current step. Returns `advance` / `correct` / `complete` + spoken text + next step coordinates.

Conversation history is maintained **client-side** in `StepPlanStore` (array of messages). Sent with each verify call. Worker stays stateless — history travels with the request, same pattern as Claude's API natively.

---

## Section 2: Step Plan Format (APPROVED)

### Key insight: coordinates are lazy

Claude only sees the initial screenshot. Step 2 might point at a dropdown that doesn't exist yet. Step 3 might be in a dialog that only appears after step 2. **Coordinates are only set for step 1 in the initial plan. The `verify` call resolves each subsequent step's coordinates from the fresh post-action screenshot.**

`StepVerifier` already takes a fresh screenshot showing the current UI state after the user's action. The verify response carries double duty — it both confirms the action and pins the next step's target:

```json
{ "result": "advance", "nextX": 450, "nextY": 280, "nextLabel": "Save As" }
```

`StepPlanStore` holds coordinates as nullable on all steps beyond step 1, populated one step ahead as verification completes.

### Tag format

Step 1 carries full coordinates. Steps 2+ carry text only:

```
[STEP:1:450,200:File menu]click File in the top menu bar[/STEP]
[STEP:2]then click Save As from the dropdown[/STEP]
[STEP:3]type your filename and press Enter[/STEP]
```

### Streaming parse — the incremental object

The SSE stream is parsed incrementally. The parser maintains a `PendingStep` object accumulating text between `[STEP:...]` and `[/STEP]`. The moment `[/STEP]` is detected, `PendingStep` materialises into a real `Step` and is added to `StepPlanStore`.

**Benefit:** Step 1's spoken text starts TTS the instant its `[/STEP]` arrives — the user hears the first instruction while Claude is still generating steps 2, 3, 4. Zero perceived latency on the first step.

---

## Section 3: Behaviour Decisions (APPROVED)

### Timeout
`StepClickWatcher` disarms after **30 seconds** of no click. Overlay returns to idle, `StepPlanStore` clears. (Range: 20–30s — 30s chosen as safe upper bound.)

### PTT during a sequence
Pressing PTT **cancels** the sequence entirely. No resume. `StepPlanStore` clears, overlay returns to idle, pipeline starts fresh from the new PTT press.

### What triggers multi-step vs single-turn
**Claude decides** — no client-side transcript analysis. The worker sends every request in the same format. Claude returns either a `[POINT:...]` single-turn response or a `[STEP:...]` plan based on its own judgement about whether the user's question warrants guidance. The system prompt instructs Claude when to use each format.

---

## Next step

Design is complete (Sections 1–3 approved). Next: invoke `writing-plans` to produce the implementation plan.
