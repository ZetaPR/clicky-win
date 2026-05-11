# Multi-Step Guidance — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add guided step-by-step workflows to Clicky — Claude returns `[STEP:...]` plans, the client delivers each step one at a time, watches for user clicks, verifies with a fresh screenshot, then advances or corrects.

**Architecture:** Five new components (`StepPlanParser`, `StepPlanStore`, `StepClickWatcher`, `CloudflareWorkerVerifyService`, overlay `WaitingForStep` state) wired into the existing `CompanionOrchestrator`. Worker gets two new JSON modes (`plan`, `verify`). Conversation history travels client-side. Coordinates are lazy — step 1 only; verify responses supply step N+1 coords.

**Tech Stack:** C# 13, .NET 9, WPF, SharpHook, xUnit, NSubstitute, Cloudflare Workers, Anthropic Messages API

---

## File Map

| Action | File |
|---|---|
| Commit | staged files from orphaned commit (overlay, physics, settings, tests) |
| Create | `src/Clicky.Core/Capture/ScreenCapture.cs` |
| Modify | `src/Clicky.Core/Capture/IScreenCaptureService.cs` |
| Modify | `src/Clicky.Capture/ScreenCapture/WgcCaptureService.cs` |
| Create | `src/Clicky.Tests/ScreenCaptureTests.cs` |
| Modify | `src/Clicky.Core/Companion/ILlmService.cs` |
| Modify | `src/Clicky.Services/Companion/CloudflareWorkerLlmService.cs` |
| Modify | `src/Clicky.Tests/LlmServiceTests.cs` |
| Modify | `src/Clicky.Tests/CompanionOrchestratorTests.cs` |
| Create | `src/Clicky.Core/MultiStep/Step.cs` |
| Create | `src/Clicky.Core/MultiStep/LlmMessage.cs` |
| Create | `src/Clicky.Core/MultiStep/VerifyResult.cs` |
| Create | `src/Clicky.Core/MultiStep/IStepVerifier.cs` |
| Create | `src/Clicky.Core/Pointing/MousePressedEventArgs.cs` |
| Modify | `src/Clicky.Core/Pointing/IPushToTalkHook.cs` |
| Create | `src/Clicky.Services/MultiStep/StepPlanParser.cs` |
| Create | `src/Clicky.Tests/StepPlanParserTests.cs` |
| Create | `src/Clicky.Services/MultiStep/StepPlanStore.cs` |
| Create | `src/Clicky.Tests/StepPlanStoreTests.cs` |
| Create | `src/Clicky.Services/MultiStep/StepClickWatcher.cs` |
| Create | `src/Clicky.Tests/StepClickWatcherTests.cs` |
| Create | `src/Clicky.Services/Companion/CloudflareWorkerVerifyService.cs` |
| Create | `src/Clicky.Tests/StepVerifyServiceTests.cs` |
| Modify | `src/Clicky.Core/Overlay/IOverlayService.cs` |
| Modify | `src/Clicky.Overlay/CursorOverlayWindow.xaml` |
| Modify | `src/Clicky.Overlay/CursorOverlayWindow.xaml.cs` |
| Modify | `src/Clicky.Services/Companion/CompanionOrchestrator.cs` |
| Modify | `worker/index.js` |
| Modify | `src/Clicky.App/ServiceRegistration.cs` |

---

## Task 1: Commit recovered orphaned files

**Files:**
- Stage + commit: all files restored from orphaned commit `0811323` (overlay, physics, settings, test files)

- [ ] **Step 1: Check what is staged**

```bash
git -C projects/clicky-win status
```

Expected: several files in `src/Clicky.Core/`, `src/Clicky.Overlay/`, `src/Clicky.Services/Settings/`, `src/Clicky.Tests/` shown as "new file" under "Changes to be committed".

- [ ] **Step 2: Also stage any recovered files not yet staged**

```bash
git -C projects/clicky-win add \
  src/Clicky.Core/Overlay/IOverlayService.cs \
  src/Clicky.Core/Overlay/PointTagParseResult.cs \
  src/Clicky.Core/Capture/MonitorBounds.cs \
  src/Clicky.Core/Physics/SpringSimulator.cs \
  src/Clicky.Core/Physics/BezierFlight.cs \
  src/Clicky.Core/Pointing/HookKeyEventArgs.cs \
  src/Clicky.Core/Pointing/HookMouseEventArgs.cs \
  src/Clicky.Core/Settings/UserSettings.cs \
  src/Clicky.Core/Settings/VoiceCatalog.cs \
  src/Clicky.Core/Settings/VoiceOption.cs \
  src/Clicky.Core/Settings/IUserSettingsService.cs \
  src/Clicky.Services/Overlay/PointTagParser.cs \
  src/Clicky.Services/Settings/UserSettingsService.cs \
  src/Clicky.App/ConfigWindow.xaml \
  src/Clicky.App/ConfigWindow.xaml.cs \
  src/Clicky.App/WelcomeWindow.xaml \
  src/Clicky.App/WelcomeWindow.xaml.cs \
  src/Clicky.Overlay/CursorOverlayWindow.xaml.cs \
  src/Clicky.Tests/BezierFlightTests.cs \
  src/Clicky.Tests/PointTagParserTests.cs \
  src/Clicky.Tests/PushToTalkHookRawEventsTests.cs \
  src/Clicky.Tests/SpringSimulatorTests.cs
```

- [ ] **Step 3: Run tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --no-build 2>&1 | tail -20
```

Expected: all tests pass (or build first: `dotnet build projects/clicky-win`).

- [ ] **Step 4: Commit**

```bash
git -C projects/clicky-win add -p  # review staged hunks
git -C projects/clicky-win commit -m "feat: restore overlay, physics, settings, and test files from orphaned commit"
```

---

## Task 2: Add `ScreenCapture` record + upgrade `IScreenCaptureService`

**Files:**
- Create: `src/Clicky.Core/Capture/ScreenCapture.cs`
- Modify: `src/Clicky.Core/Capture/IScreenCaptureService.cs`
- Modify: `src/Clicky.Capture/ScreenCapture/WgcCaptureService.cs`
- Create: `src/Clicky.Tests/ScreenCaptureTests.cs`
- Modify: `src/Clicky.Tests/CompanionOrchestratorTests.cs` (mock return type update)

- [ ] **Step 1: Write failing test**

Create `src/Clicky.Tests/ScreenCaptureTests.cs`:

```csharp
using Clicky.Core;
using Xunit;

namespace Clicky.Tests;

public class ScreenCaptureTests
{
    [Fact]
    public void ScreenCapture_StoresAllProperties()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF };
        var bounds = new MonitorBounds(10, 20, 1920, 1080);
        var capture = new ScreenCapture(jpeg, 1920, 1080, bounds);

        Assert.Same(jpeg, capture.Jpeg);
        Assert.Equal(1920, capture.Width);
        Assert.Equal(1080, capture.Height);
        Assert.Equal(bounds, capture.MonitorPhysBounds);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter ScreenCaptureTests -v normal 2>&1 | tail -10
```

Expected: FAIL — `ScreenCapture` type does not exist.

- [ ] **Step 3: Create `ScreenCapture` record**

Create `src/Clicky.Core/Capture/ScreenCapture.cs`:

```csharp
namespace Clicky.Core;

public sealed record ScreenCapture(
    byte[] Jpeg,
    int Width,
    int Height,
    MonitorBounds MonitorPhysBounds);
```

- [ ] **Step 4: Upgrade `IScreenCaptureService`**

Replace `src/Clicky.Core/Capture/IScreenCaptureService.cs` entirely:

```csharp
namespace Clicky.Core;

/// <summary>Captures screenshots of the user's displays.</summary>
public interface IScreenCaptureService : IDisposable
{
    /// <summary>
    /// Captures the primary monitor and returns JPEG bytes with dimensions and monitor bounds.
    /// Called on a background thread; implementation must be thread-safe.
    /// </summary>
    Task<ScreenCapture> CaptureAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Update `WgcCaptureService`**

Replace `src/Clicky.Capture/ScreenCapture/WgcCaptureService.cs`:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using Clicky.Core;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicky.Capture.ScreenCapture;

public sealed class WgcCaptureService : IScreenCaptureService
{
    /// <inheritdoc/>
    public Task<ScreenCapture> CaptureAsync(CancellationToken cancellationToken = default)
        => Task.Run(CaptureJpeg, cancellationToken);

    private static ScreenCapture CaptureJpeg()
    {
        var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(width, height));

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Jpeg);

        var bounds = new MonitorBounds(0, 0, width, height);
        return new ScreenCapture(ms.ToArray(), width, height, bounds);
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
```

- [ ] **Step 6: Update `CompanionOrchestratorTests` for new capture type**

In `src/Clicky.Tests/CompanionOrchestratorTests.cs`, change the `OnRecordingStopped_WithTranscript_CallsLlmAndTts` test setup:

Old:
```csharp
var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };
capture.CapturePrimaryMonitorAsync(Arg.Any<CancellationToken>()).Returns(jpeg);

llm.StreamResponseAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
   .Returns(AsyncEnumerableReturn("Hello", " world"));
```

New:
```csharp
var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };
var screenCapture = new ScreenCapture(jpeg, 1920, 1080, new MonitorBounds(0, 0, 1920, 1080));
capture.CaptureAsync(Arg.Any<CancellationToken>()).Returns(screenCapture);

llm.StreamResponseAsync(Arg.Any<byte[]>(), Arg.Any<string>(),
    Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
   .Returns(AsyncEnumerableReturn("Hello", " world"));
```

Also update the `OnRecordingStopped_WithEmptyTranscript_DoesNotCallLlm` DidNotReceive call to match the new 5-param signature.

Also update `CompanionOrchestrator.cs` to call `_capture.CaptureAsync()` (rename from `CapturePrimaryMonitorAsync`).

- [ ] **Step 7: Run tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests -v normal 2>&1 | tail -20
```

Expected: all tests pass.

- [ ] **Step 8: Commit**

```bash
git -C projects/clicky-win add src/Clicky.Core/Capture/ScreenCapture.cs \
  src/Clicky.Core/Capture/IScreenCaptureService.cs \
  src/Clicky.Capture/ScreenCapture/WgcCaptureService.cs \
  src/Clicky.Tests/ScreenCaptureTests.cs \
  src/Clicky.Tests/CompanionOrchestratorTests.cs \
  src/Clicky.Services/Companion/CompanionOrchestrator.cs
git -C projects/clicky-win commit -m "feat: upgrade IScreenCaptureService to return ScreenCapture with dimensions and bounds"
```

---

## Task 3: Upgrade `ILlmService` + switch `CloudflareWorkerLlmService` to JSON

**Files:**
- Modify: `src/Clicky.Core/Companion/ILlmService.cs`
- Modify: `src/Clicky.Services/Companion/CloudflareWorkerLlmService.cs`
- Modify: `src/Clicky.Tests/LlmServiceTests.cs`

- [ ] **Step 1: Upgrade `ILlmService` with width/height params**

Replace `src/Clicky.Core/Companion/ILlmService.cs`:

```csharp
namespace Clicky.Core;

/// <summary>Sends a screen capture and spoken transcript to an LLM and streams back text deltas.</summary>
public interface ILlmService
{
    /// <summary>
    /// Streams text deltas from the LLM. Each yielded string is a small piece of the response.
    /// The stream ends when the LLM finishes or the cancellation token fires.
    /// <paramref name="screenshotWidth"/> and <paramref name="screenshotHeight"/> are the pixel
    /// dimensions of the JPEG — used by the worker to resolve coordinate percentages accurately.
    /// </summary>
    IAsyncEnumerable<string> StreamResponseAsync(
        byte[] screenshot,
        string transcript,
        int screenshotWidth,
        int screenshotHeight,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Switch `CloudflareWorkerLlmService` to JSON body**

Replace `src/Clicky.Services/Companion/CloudflareWorkerLlmService.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Clicky.Core;

namespace Clicky.Services;

/// <summary>
/// Sends a screenshot and transcript to the Cloudflare Worker as JSON and reads back
/// the Anthropic SSE stream, yielding text delta strings.
/// </summary>
public sealed class CloudflareWorkerLlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly CompanionSettings _settings;

    /// <summary>Initializes the service with a pre-configured HTTP client and companion settings.</summary>
    public CloudflareWorkerLlmService(HttpClient http, CompanionSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamResponseAsync(
        byte[] screenshot,
        string transcript,
        int screenshotWidth,
        int screenshotHeight,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var response = await SendRequestAsync(screenshot, transcript, screenshotWidth, screenshotHeight, cancellationToken)
            .ConfigureAwait(false);

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using var reader = new StreamReader(stream);

        await foreach (var delta in ParseSseStreamAsync(reader, cancellationToken).ConfigureAwait(false))
            yield return delta;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        byte[] screenshot,
        string transcript,
        int screenshotWidth,
        int screenshotHeight,
        CancellationToken cancellationToken)
    {
        var imageBase64 = Convert.ToBase64String(screenshot);
        var payload = new
        {
            mode = "plan",
            screenshot = imageBase64,
            transcript,
            screenshotWidth,
            screenshotHeight,
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(_settings.WorkerUrl, content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return response;
    }

    /// <summary>
    /// Parses an Anthropic SSE stream from the given reader and yields text delta strings.
    /// Exposed as internal for unit testing without a real HTTP server.
    /// </summary>
    internal static async IAsyncEnumerable<string> ParseSseStreamAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var currentEvent = string.Empty;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
                yield break;

            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                currentEvent = line["event: ".Length..];
                if (currentEvent == "message_stop")
                    yield break;
                continue;
            }

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;

            if (currentEvent != "content_block_delta")
                continue;

            var json = line["data: ".Length..];
            var delta = ExtractTextDelta(json);
            if (delta is not null)
                yield return delta;
        }
    }

    private static string? ExtractTextDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("delta", out var delta))
                return null;

            if (!delta.TryGetProperty("type", out var typeEl))
                return null;

            if (typeEl.GetString() != "text_delta")
                return null;

            return delta.TryGetProperty("text", out var textEl) ? textEl.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 3: Update `LlmServiceTests` — `ParseSseStreamAsync` signature unchanged, no action needed**

The `ParseSseStreamAsync` internal method signature is unchanged — the tests call it directly and still pass. Verify:

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter LlmServiceTests -v normal 2>&1 | tail -10
```

Expected: all LlmServiceTests pass.

- [ ] **Step 4: Update `CompanionOrchestrator` to pass width/height to `_llm`**

In `CompanionOrchestrator.cs`, `RunPipelineAsync`:

Old:
```csharp
var jpeg = await _capture.CapturePrimaryMonitorAsync(token).ConfigureAwait(false);
...
await StreamLlmToTtsAsync(jpeg, transcript, token).ConfigureAwait(false);
```

New:
```csharp
var capture = await _capture.CaptureAsync(token).ConfigureAwait(false);
...
await StreamLlmToTtsAsync(capture, transcript, token).ConfigureAwait(false);
```

Old `StreamLlmToTtsAsync`:
```csharp
private async Task StreamLlmToTtsAsync(byte[] jpeg, string transcript, CancellationToken token)
{
    await foreach (var delta in _llm.StreamResponseAsync(jpeg, transcript, token).ConfigureAwait(false))
        await _tts.SpeakAsync(delta, token).ConfigureAwait(false);
}
```

New `StreamLlmToTtsAsync`:
```csharp
private async Task StreamLlmToTtsAsync(ScreenCapture capture, string transcript, CancellationToken token)
{
    await foreach (var delta in _llm.StreamResponseAsync(
        capture.Jpeg, transcript, capture.Width, capture.Height, token).ConfigureAwait(false))
    {
        await _tts.SpeakAsync(delta, token).ConfigureAwait(false);
    }
}
```

- [ ] **Step 5: Run all tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests -v normal 2>&1 | tail -20
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git -C projects/clicky-win add src/Clicky.Core/Companion/ILlmService.cs \
  src/Clicky.Services/Companion/CloudflareWorkerLlmService.cs \
  src/Clicky.Services/Companion/CompanionOrchestrator.cs
git -C projects/clicky-win commit -m "feat: upgrade ILlmService with screenshot dimensions, switch worker client to JSON"
```

---

## Task 4: Add multi-step core models

**Files:**
- Create: `src/Clicky.Core/MultiStep/Step.cs`
- Create: `src/Clicky.Core/MultiStep/LlmMessage.cs`
- Create: `src/Clicky.Core/MultiStep/VerifyResult.cs`
- Create: `src/Clicky.Core/MultiStep/IStepVerifier.cs`
- Create: `src/Clicky.Core/Pointing/MousePressedEventArgs.cs`
- Modify: `src/Clicky.Core/Pointing/IPushToTalkHook.cs`

- [ ] **Step 1: Create `Step`**

Create `src/Clicky.Core/MultiStep/Step.cs`:

```csharp
namespace Clicky.Core;

public sealed class Step
{
    public required int Number { get; init; }
    public required string Text { get; init; }
    public int? X { get; init; }
    public int? Y { get; init; }
    public string? Label { get; init; }

    public bool HasCoords => X.HasValue && Y.HasValue;
}
```

- [ ] **Step 2: Create `LlmMessage`**

Create `src/Clicky.Core/MultiStep/LlmMessage.cs`:

```csharp
namespace Clicky.Core;

/// <summary>A single turn in the conversation history sent to the worker on verify calls.</summary>
public sealed record LlmMessage(string Role, string Content);
```

- [ ] **Step 3: Create `VerifyResult`**

Create `src/Clicky.Core/MultiStep/VerifyResult.cs`:

```csharp
namespace Clicky.Core;

public enum VerifyOutcome { Advance, Correct, Complete }

public sealed record VerifyResult(
    VerifyOutcome Outcome,
    string SpokenText,
    int? NextX,
    int? NextY,
    string? NextLabel);
```

- [ ] **Step 4: Create `IStepVerifier`**

Create `src/Clicky.Core/MultiStep/IStepVerifier.cs`:

```csharp
namespace Clicky.Core;

/// <summary>
/// Sends a fresh screenshot plus conversation history to the worker and returns
/// a verification result telling the orchestrator whether to advance, correct, or complete.
/// </summary>
public interface IStepVerifier
{
    Task<VerifyResult> VerifyAsync(
        byte[] screenshot,
        int screenshotWidth,
        int screenshotHeight,
        int stepNumber,
        string stepText,
        IReadOnlyList<LlmMessage> history,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Create `MousePressedEventArgs`**

Create `src/Clicky.Core/Pointing/MousePressedEventArgs.cs`:

```csharp
namespace Clicky.Core;

/// <summary>Carries screen coordinates and button code from the global mouse hook.</summary>
public sealed class MousePressedEventArgs(int x, int y, string buttonCode) : EventArgs
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public string ButtonCode { get; } = buttonCode;
}
```

- [ ] **Step 6: Add `MousePressed` to `IPushToTalkHook`**

Replace `src/Clicky.Core/Pointing/IPushToTalkHook.cs`:

```csharp
namespace Clicky.Core;

/// <summary>Detects the global Ctrl+Win+Space push-to-talk hotkey and raises recording lifecycle events.</summary>
public interface IPushToTalkHook : IDisposable
{
    /// <summary>Fired on a background thread when recording starts.</summary>
    event EventHandler RecordingStarted;

    /// <summary>Fired on a background thread when recording stops.</summary>
    event EventHandler RecordingStopped;

    /// <summary>Fired on a background thread for every global mouse button press.</summary>
    event EventHandler<MousePressedEventArgs>? MousePressed;

    /// <summary>Starts the global keyboard and mouse hook on a background thread.</summary>
    void Start();
}
```

- [ ] **Step 7: Build (no new tests yet — models are tested through StepPlanParser/Store)**

```bash
dotnet build projects/clicky-win 2>&1 | grep -E "error|warning" | head -20
```

Expected: build succeeds. `PushToTalkHookRawEventsTests.cs` may need updating if it mocks `IPushToTalkHook` — check and add the new event stub to any NSubstitute mock setup (NSubstitute auto-stubs interface events so no change needed).

- [ ] **Step 8: Commit**

```bash
git -C projects/clicky-win add \
  src/Clicky.Core/MultiStep/Step.cs \
  src/Clicky.Core/MultiStep/LlmMessage.cs \
  src/Clicky.Core/MultiStep/VerifyResult.cs \
  src/Clicky.Core/MultiStep/IStepVerifier.cs \
  src/Clicky.Core/Pointing/MousePressedEventArgs.cs \
  src/Clicky.Core/Pointing/IPushToTalkHook.cs
git -C projects/clicky-win commit -m "feat: add multi-step core models (Step, LlmMessage, VerifyResult, IStepVerifier)"
```

---

## Task 5: Add `StepPlanParser`

**Files:**
- Create: `src/Clicky.Services/MultiStep/StepPlanParser.cs`
- Create: `src/Clicky.Tests/StepPlanParserTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/Clicky.Tests/StepPlanParserTests.cs`:

```csharp
using Clicky.Core;
using Clicky.Services;
using Xunit;

namespace Clicky.Tests;

public class StepPlanParserTests
{
    [Fact]
    public void Feed_StepWithCoords_ParsesAllFields()
    {
        var parser = new StepPlanParser();
        var steps = parser.Feed("[STEP:1:450,200:File menu]click File in the top menu bar[/STEP]").ToList();

        Assert.Single(steps);
        Assert.Equal(1, steps[0].Number);
        Assert.Equal("click File in the top menu bar", steps[0].Text);
        Assert.Equal(450, steps[0].X);
        Assert.Equal(200, steps[0].Y);
        Assert.Equal("File menu", steps[0].Label);
    }

    [Fact]
    public void Feed_StepWithoutCoords_ParsesTextOnly()
    {
        var parser = new StepPlanParser();
        var steps = parser.Feed("[STEP:2]then click Save As[/STEP]").ToList();

        Assert.Single(steps);
        Assert.Equal(2, steps[0].Number);
        Assert.Equal("then click Save As", steps[0].Text);
        Assert.Null(steps[0].X);
        Assert.Null(steps[0].Y);
        Assert.Null(steps[0].Label);
    }

    [Fact]
    public void Feed_TagSplitAcrossDeltas_ParsesStep()
    {
        var parser = new StepPlanParser();
        var s1 = parser.Feed("[STEP:1:100,200:label]some ").ToList();
        var s2 = parser.Feed("text[/STEP]").ToList();

        Assert.Empty(s1);
        Assert.Single(s2);
        Assert.Equal("some text", s2[0].Text);
    }

    [Fact]
    public void Feed_MultipleStepsInOneChunk_ParsesAll()
    {
        var parser = new StepPlanParser();
        var text = "[STEP:1:100,200:A]step one[/STEP][STEP:2]step two[/STEP]";
        var steps = parser.Feed(text).ToList();

        Assert.Equal(2, steps.Count);
        Assert.Equal("step one", steps[0].Text);
        Assert.Equal("step two", steps[1].Text);
    }

    [Fact]
    public void Feed_IgnoresTextOutsideTags()
    {
        var parser = new StepPlanParser();
        var text = "preamble text [STEP:1:10,20:X]do this[/STEP] trailing";
        var steps = parser.Feed(text).ToList();

        Assert.Single(steps);
        Assert.Equal("do this", steps[0].Text);
    }

    [Fact]
    public void Feed_StepWithCoordsButNoLabel_ParsesCoordsWithNullLabel()
    {
        var parser = new StepPlanParser();
        var steps = parser.Feed("[STEP:1:300,400]no label step[/STEP]").ToList();

        Assert.Single(steps);
        Assert.Equal(300, steps[0].X);
        Assert.Equal(400, steps[0].Y);
        Assert.Null(steps[0].Label);
    }

    [Fact]
    public void Feed_EmptyDelta_ReturnsEmpty()
    {
        var parser = new StepPlanParser();
        Assert.Empty(parser.Feed(""));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter StepPlanParserTests -v normal 2>&1 | tail -10
```

Expected: FAIL — `StepPlanParser` type not found.

- [ ] **Step 3: Implement `StepPlanParser`**

Create `src/Clicky.Services/MultiStep/StepPlanParser.cs`:

```csharp
using System.Text;
using System.Text.RegularExpressions;
using Clicky.Core;

namespace Clicky.Services;

/// <summary>
/// Incrementally parses [STEP:n:x,y:label]text[/STEP] tags from an SSE delta stream.
/// Call Feed() with each arriving delta; it returns any steps that completed in that chunk.
/// Tag boundaries may safely split across deltas — the parser buffers internally.
/// </summary>
public sealed partial class StepPlanParser
{
    [GeneratedRegex(
        @"\[STEP:(\d+)(?::(\d+),(\d+)(?::([^\]]*))?)?\]",
        RegexOptions.Compiled)]
    private static partial Regex StepOpenRegex();

    private const string CloseTag = "[/STEP]";

    private readonly StringBuilder _buffer = new();
    private int _scanStart;
    private PendingStep? _pending;

    private sealed record PendingStep(int Number, int? X, int? Y, string? Label, int TextStart);

    /// <summary>
    /// Feeds a delta string into the parser. Returns any steps whose [/STEP] closing tag
    /// was found within this or a prior delta.
    /// </summary>
    public IEnumerable<Step> Feed(string delta)
    {
        if (string.IsNullOrEmpty(delta))
            yield break;

        _buffer.Append(delta);
        var buf = _buffer.ToString();

        while (true)
        {
            if (_pending is null)
            {
                var m = StepOpenRegex().Match(buf, _scanStart);
                if (!m.Success) yield break;

                int? x = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : null;
                int? y = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : null;
                string? label = m.Groups[4].Success && m.Groups[4].Length > 0 ? m.Groups[4].Value : null;

                _pending = new PendingStep(
                    Number: int.Parse(m.Groups[1].Value),
                    X: x, Y: y, Label: label,
                    TextStart: m.Index + m.Length);
                _scanStart = _pending.TextStart;
            }
            else
            {
                var closeIdx = buf.IndexOf(CloseTag, _scanStart, StringComparison.Ordinal);
                if (closeIdx == -1) yield break;

                var text = buf[_pending.TextStart..closeIdx].Trim();
                yield return new Step
                {
                    Number = _pending.Number,
                    Text = text,
                    X = _pending.X,
                    Y = _pending.Y,
                    Label = _pending.Label,
                };

                _scanStart = closeIdx + CloseTag.Length;
                _pending = null;
            }
        }
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter StepPlanParserTests -v normal 2>&1 | tail -15
```

Expected: all 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git -C projects/clicky-win add \
  src/Clicky.Services/MultiStep/StepPlanParser.cs \
  src/Clicky.Tests/StepPlanParserTests.cs
git -C projects/clicky-win commit -m "feat: add StepPlanParser — incremental [STEP:...][/STEP] tag parser"
```

---

## Task 6: Add `StepPlanStore`

**Files:**
- Create: `src/Clicky.Services/MultiStep/StepPlanStore.cs`
- Create: `src/Clicky.Tests/StepPlanStoreTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/Clicky.Tests/StepPlanStoreTests.cs`:

```csharp
using Clicky.Core;
using Clicky.Services;
using Xunit;

namespace Clicky.Tests;

public class StepPlanStoreTests
{
    private static ScreenCapture MakeCapture() =>
        new(Array.Empty<byte>(), 100, 100, new MonitorBounds(0, 0, 100, 100));

    [Fact]
    public void StepPlanStore_InitiallyNotActive()
    {
        var store = new StepPlanStore();
        Assert.False(store.IsActive);
        Assert.Null(store.CurrentStep);
    }

    [Fact]
    public void Load_SetsActiveAndStoresCapture()
    {
        var store = new StepPlanStore();
        var capture = MakeCapture();
        store.Load(capture, nint.Zero);
        Assert.True(store.IsActive);
        Assert.Same(capture, store.OriginalCapture);
    }

    [Fact]
    public void AddStep_BecomesCurrentStep_WhenFirstAdded()
    {
        var store = new StepPlanStore();
        store.Load(MakeCapture(), nint.Zero);
        store.AddStep(new Step { Number = 1, Text = "do this", X = 10, Y = 20 });
        Assert.NotNull(store.CurrentStep);
        Assert.Equal(1, store.CurrentStep!.Number);
    }

    [Fact]
    public void AdvanceTo_UpdatesCurrentStep()
    {
        var store = new StepPlanStore();
        store.Load(MakeCapture(), nint.Zero);
        store.AddStep(new Step { Number = 1, Text = "step one", X = 10, Y = 20 });
        store.AddStep(new Step { Number = 2, Text = "step two" });
        store.AdvanceTo(1, nextX: 50, nextY: 60, nextLabel: null);
        Assert.Equal(2, store.CurrentStep!.Number);
        Assert.Equal(50, store.CurrentStep.X);
    }

    [Fact]
    public void Clear_ResetsStore()
    {
        var store = new StepPlanStore();
        store.Load(MakeCapture(), nint.Zero);
        store.AddStep(new Step { Number = 1, Text = "do this" });
        store.Clear();
        Assert.False(store.IsActive);
        Assert.Null(store.CurrentStep);
    }

    [Fact]
    public void AppendHistory_GrowsHistoryList()
    {
        var store = new StepPlanStore();
        store.Load(MakeCapture(), nint.Zero);
        store.AppendHistory(new LlmMessage("assistant", "step one text"));
        store.AppendHistory(new LlmMessage("user", "clicked"));
        Assert.Equal(2, store.History.Count);
    }

    [Fact]
    public async Task TimedOut_FiresAfterTimeout()
    {
        var store = new StepPlanStore(timeoutSeconds: 0.15);
        bool fired = false;
        store.TimedOut += (_, _) => fired = true;
        store.Load(MakeCapture(), nint.Zero);
        await Task.Delay(400);
        Assert.True(fired);
        Assert.False(store.IsActive);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter StepPlanStoreTests -v normal 2>&1 | tail -10
```

Expected: FAIL.

- [ ] **Step 3: Implement `StepPlanStore`**

Create `src/Clicky.Services/MultiStep/StepPlanStore.cs`:

```csharp
using Clicky.Core;

namespace Clicky.Services;

/// <summary>
/// Holds the active multi-step plan in memory. Tracks step index, original screenshot,
/// conversation history, and target HWND. Clears itself on timeout (default 30s) or when
/// Clear() is called. Thread-safe for reads; writes should come from the orchestrator thread.
/// </summary>
public sealed class StepPlanStore : IDisposable
{
    private readonly double _timeoutSeconds;
    private readonly List<Step> _steps = [];
    private readonly List<LlmMessage> _history = [];
    private int _currentIndex;
    private ScreenCapture? _originalCapture;
    private nint _targetHwnd;
    private Timer? _timeoutTimer;
    private volatile bool _isActive;

    public event EventHandler? TimedOut;

    public StepPlanStore(double timeoutSeconds = 30.0)
        => _timeoutSeconds = timeoutSeconds;

    public bool IsActive => _isActive;
    public ScreenCapture? OriginalCapture => _originalCapture;
    public nint TargetHwnd => _targetHwnd;
    public IReadOnlyList<LlmMessage> History => _history;

    public Step? CurrentStep
    {
        get
        {
            if (!_isActive || _currentIndex >= _steps.Count) return null;
            return _steps[_currentIndex];
        }
    }

    public int TotalSteps => _steps.Count;

    public void Load(ScreenCapture capture, nint targetHwnd)
    {
        Clear();
        _originalCapture = capture;
        _targetHwnd = targetHwnd;
        _isActive = true;
        ResetTimeout();
    }

    public void AddStep(Step step)
    {
        _steps.Add(step);
    }

    public void AppendHistory(LlmMessage message)
    {
        _history.Add(message);
    }

    /// <summary>
    /// Advances to step at <paramref name="newIndex"/>, updating its coords if provided
    /// (lazy coordinate resolution from the verify response).
    /// </summary>
    public void AdvanceTo(int newIndex, int? nextX, int? nextY, string? nextLabel)
    {
        if (newIndex >= _steps.Count) return;

        if (nextX.HasValue || nextY.HasValue || nextLabel is not null)
        {
            var s = _steps[newIndex];
            _steps[newIndex] = new Step
            {
                Number = s.Number,
                Text = s.Text,
                X = nextX ?? s.X,
                Y = nextY ?? s.Y,
                Label = nextLabel ?? s.Label,
            };
        }

        _currentIndex = newIndex;
        ResetTimeout();
    }

    public void Clear()
    {
        _isActive = false;
        _steps.Clear();
        _history.Clear();
        _currentIndex = 0;
        _originalCapture = null;
        _targetHwnd = nint.Zero;
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
    }

    private void ResetTimeout()
    {
        _timeoutTimer?.Dispose();
        _timeoutTimer = new Timer(_ =>
        {
            Clear();
            TimedOut?.Invoke(this, EventArgs.Empty);
        }, null, TimeSpan.FromSeconds(_timeoutSeconds), Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _timeoutTimer?.Dispose();
        _timeoutTimer = null;
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter StepPlanStoreTests -v normal 2>&1 | tail -15
```

Expected: all 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git -C projects/clicky-win add \
  src/Clicky.Services/MultiStep/StepPlanStore.cs \
  src/Clicky.Tests/StepPlanStoreTests.cs
git -C projects/clicky-win commit -m "feat: add StepPlanStore — active plan holder with 30s timeout and lazy coord resolution"
```

---

## Task 7: Add `CloudflareWorkerVerifyService`

**Files:**
- Create: `src/Clicky.Services/Companion/CloudflareWorkerVerifyService.cs`
- Create: `src/Clicky.Tests/StepVerifyServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/Clicky.Tests/StepVerifyServiceTests.cs`:

```csharp
using Clicky.Core;
using Clicky.Services;
using Xunit;

namespace Clicky.Tests;

public class StepVerifyServiceTests
{
    [Fact]
    public void ParseVerifyResponse_Advance_ReturnsAdvanceOutcome()
    {
        var json = """{"result":"advance","spokenText":"Good job!","nextX":300,"nextY":400,"nextLabel":"Save As"}""";
        var result = CloudflareWorkerVerifyService.ParseVerifyResponse(json);

        Assert.Equal(VerifyOutcome.Advance, result.Outcome);
        Assert.Equal("Good job!", result.SpokenText);
        Assert.Equal(300, result.NextX);
        Assert.Equal(400, result.NextY);
        Assert.Equal("Save As", result.NextLabel);
    }

    [Fact]
    public void ParseVerifyResponse_Complete_ReturnsCompleteOutcome()
    {
        var json = """{"result":"complete","spokenText":"All done!"}""";
        var result = CloudflareWorkerVerifyService.ParseVerifyResponse(json);

        Assert.Equal(VerifyOutcome.Complete, result.Outcome);
        Assert.Equal("All done!", result.SpokenText);
        Assert.Null(result.NextX);
    }

    [Fact]
    public void ParseVerifyResponse_Correct_ReturnsCorrectOutcome()
    {
        var json = """{"result":"correct","spokenText":"Not quite, try again."}""";
        var result = CloudflareWorkerVerifyService.ParseVerifyResponse(json);

        Assert.Equal(VerifyOutcome.Correct, result.Outcome);
    }

    [Fact]
    public void ParseVerifyResponse_UnknownResult_ThrowsInvalidOperationException()
    {
        var json = """{"result":"unknown","spokenText":"?"}""";
        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkerVerifyService.ParseVerifyResponse(json));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter StepVerifyServiceTests -v normal 2>&1 | tail -10
```

Expected: FAIL.

- [ ] **Step 3: Implement `CloudflareWorkerVerifyService`**

Create `src/Clicky.Services/Companion/CloudflareWorkerVerifyService.cs`:

```csharp
using System.Text;
using System.Text.Json;
using Clicky.Core;

namespace Clicky.Services;

/// <summary>
/// Sends a verify request (fresh screenshot + history + current step) to the Cloudflare Worker
/// and parses the JSON response into a <see cref="VerifyResult"/>.
/// The worker call is non-streaming — verify responses are short.
/// </summary>
public sealed class CloudflareWorkerVerifyService : IStepVerifier
{
    private readonly HttpClient _http;
    private readonly CompanionSettings _settings;

    /// <summary>Initializes with a pre-configured HTTP client and companion settings.</summary>
    public CloudflareWorkerVerifyService(HttpClient http, CompanionSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    /// <inheritdoc/>
    public async Task<VerifyResult> VerifyAsync(
        byte[] screenshot,
        int screenshotWidth,
        int screenshotHeight,
        int stepNumber,
        string stepText,
        IReadOnlyList<LlmMessage> history,
        CancellationToken cancellationToken = default)
    {
        var imageBase64 = Convert.ToBase64String(screenshot);

        var historyPayload = history.Select(m => new { role = m.Role, content = m.Content }).ToArray();

        var payload = new
        {
            mode = "verify",
            screenshot = imageBase64,
            screenshotWidth,
            screenshotHeight,
            stepNumber,
            stepText,
            history = historyPayload,
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(_settings.WorkerUrl, content, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        return ParseVerifyResponse(responseJson);
    }

    /// <summary>
    /// Parses the worker's JSON verify response.
    /// Exposed as internal for unit testing without a real HTTP call.
    /// </summary>
    internal static VerifyResult ParseVerifyResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var resultStr = root.GetProperty("result").GetString()!;
        var spokenText = root.GetProperty("spokenText").GetString() ?? string.Empty;

        int? nextX = root.TryGetProperty("nextX", out var xEl) ? xEl.GetInt32() : null;
        int? nextY = root.TryGetProperty("nextY", out var yEl) ? yEl.GetInt32() : null;
        string? nextLabel = root.TryGetProperty("nextLabel", out var lEl) ? lEl.GetString() : null;

        var outcome = resultStr switch
        {
            "advance" => VerifyOutcome.Advance,
            "correct" => VerifyOutcome.Correct,
            "complete" => VerifyOutcome.Complete,
            _ => throw new InvalidOperationException($"Unknown verify result: {resultStr}"),
        };

        return new VerifyResult(outcome, spokenText, nextX, nextY, nextLabel);
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter StepVerifyServiceTests -v normal 2>&1 | tail -10
```

Expected: all 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git -C projects/clicky-win add \
  src/Clicky.Services/Companion/CloudflareWorkerVerifyService.cs \
  src/Clicky.Tests/StepVerifyServiceTests.cs
git -C projects/clicky-win commit -m "feat: add CloudflareWorkerVerifyService implementing IStepVerifier"
```

---

## Task 8: Add `StepClickWatcher`

**Files:**
- Create: `src/Clicky.Services/MultiStep/StepClickWatcher.cs`
- Create: `src/Clicky.Tests/StepClickWatcherTests.cs`

Note: `PushToTalkHook` also needs updating to fire `MousePressed` — that's a `Clicky.Capture` change covered in Step 5 below. For tests, use a mock `IPushToTalkHook`.

- [ ] **Step 1: Write failing tests**

Create `src/Clicky.Tests/StepClickWatcherTests.cs`:

```csharp
using Clicky.Core;
using Clicky.Services;
using NSubstitute;
using Xunit;

namespace Clicky.Tests;

public class StepClickWatcherTests
{
    private static IPushToTalkHook MakePtt() => Substitute.For<IPushToTalkHook>();

    [Fact]
    public void ClickWithinRadius_WhenArmed_FiresClickConfirmed()
    {
        var ptt = MakePtt();
        using var watcher = new StepClickWatcher(ptt);
        bool fired = false;
        watcher.ClickConfirmed += (_, _) => fired = true;

        watcher.Arm(targetX: 500, targetY: 300);

        ptt.MousePressed += Raise.EventWith(new MousePressedEventArgs(520, 310, "MouseButton1"));

        Assert.True(fired);
    }

    [Fact]
    public void ClickOutsideRadius_WhenArmed_DoesNotFire()
    {
        var ptt = MakePtt();
        using var watcher = new StepClickWatcher(ptt);
        bool fired = false;
        watcher.ClickConfirmed += (_, _) => fired = true;

        watcher.Arm(targetX: 500, targetY: 300);

        ptt.MousePressed += Raise.EventWith(new MousePressedEventArgs(700, 500, "MouseButton1"));

        Assert.False(fired);
    }

    [Fact]
    public void Click_WhenDisarmed_DoesNotFire()
    {
        var ptt = MakePtt();
        using var watcher = new StepClickWatcher(ptt);
        bool fired = false;
        watcher.ClickConfirmed += (_, _) => fired = true;

        watcher.Arm(targetX: 500, targetY: 300);
        watcher.Disarm();

        ptt.MousePressed += Raise.EventWith(new MousePressedEventArgs(510, 305, "MouseButton1"));

        Assert.False(fired);
    }

    [Fact]
    public void ClickExactlyAtRadius_Fires()
    {
        var ptt = MakePtt();
        using var watcher = new StepClickWatcher(ptt);
        bool fired = false;
        watcher.ClickConfirmed += (_, _) => fired = true;

        watcher.Arm(targetX: 100, targetY: 100);

        // exactly 60px away diagonally: sqrt(42^2 + 42^2) ≈ 59.4 — within radius
        ptt.MousePressed += Raise.EventWith(new MousePressedEventArgs(142, 142, "MouseButton1"));

        Assert.True(fired);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter StepClickWatcherTests -v normal 2>&1 | tail -10
```

Expected: FAIL.

- [ ] **Step 3: Implement `StepClickWatcher`**

Create `src/Clicky.Services/MultiStep/StepClickWatcher.cs`:

```csharp
using Clicky.Core;

namespace Clicky.Services;

/// <summary>
/// Arms after a step is delivered. Listens to global mouse presses via IPushToTalkHook.MousePressed.
/// When a left-click within <see cref="HitRadiusPx"/> of the target fires, raises ClickConfirmed.
/// Disarms on PTT press, sequence end, or timeout.
/// </summary>
public sealed class StepClickWatcher : IDisposable
{
    public const int HitRadiusPx = 60;

    private readonly IPushToTalkHook _ptt;
    private volatile bool _armed;
    private int _targetX;
    private int _targetY;

    public event EventHandler? ClickConfirmed;

    public StepClickWatcher(IPushToTalkHook ptt)
    {
        _ptt = ptt;
        _ptt.MousePressed += OnMousePressed;
    }

    /// <summary>Arms the watcher at the given screen coordinates.</summary>
    public void Arm(int targetX, int targetY)
    {
        _targetX = targetX;
        _targetY = targetY;
        _armed = true;
    }

    /// <summary>Disarms without firing ClickConfirmed.</summary>
    public void Disarm() => _armed = false;

    private void OnMousePressed(object? sender, MousePressedEventArgs e)
    {
        if (!_armed) return;
        if (e.ButtonCode != "MouseButton1") return;

        var dx = e.X - _targetX;
        var dy = e.Y - _targetY;
        if (dx * dx + dy * dy > HitRadiusPx * HitRadiusPx) return;

        _armed = false;
        ClickConfirmed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _armed = false;
        _ptt.MousePressed -= OnMousePressed;
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests --filter StepClickWatcherTests -v normal 2>&1 | tail -10
```

Expected: all 4 tests pass.

- [ ] **Step 5: Wire `MousePressed` into `PushToTalkHook` implementation**

Open `src/Clicky.Capture/Hotkeys/PushToTalkHook.cs` (or equivalent). Locate where SharpHook mouse events fire and add:

```csharp
// In the SharpHook mouse button pressed callback:
private void OnMouseButtonPressed(object? sender, SharpHook.Native.MouseHookEventArgs e)
{
    var args = new MousePressedEventArgs(e.Data.X, e.Data.Y, e.Data.Button.ToString());
    MousePressed?.Invoke(this, args);
}
```

Add the event field:
```csharp
public event EventHandler<MousePressedEventArgs>? MousePressed;
```

Subscribe in `Start()`:
```csharp
_hook.MouseButtonPressed += OnMouseButtonPressed;
```

(Exact SharpHook API: `SimpleGlobalHook` fires `MouseButtonPressed` with `MouseHookEventArgs`. The `Data` property has `X`, `Y`, and `Button` of type `MouseButton`.)

- [ ] **Step 6: Run all tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests -v normal 2>&1 | tail -20
```

Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
git -C projects/clicky-win add \
  src/Clicky.Services/MultiStep/StepClickWatcher.cs \
  src/Clicky.Tests/StepClickWatcherTests.cs \
  src/Clicky.Capture/Hotkeys/PushToTalkHook.cs
git -C projects/clicky-win commit -m "feat: add StepClickWatcher — arms on step delivery, fires on click within 60px radius"
```

---

## Task 9: Upgrade `IOverlayService` + `CursorOverlayWindow` for multi-step

**Files:**
- Modify: `src/Clicky.Core/Overlay/IOverlayService.cs`
- Modify: `src/Clicky.Overlay/CursorOverlayWindow.xaml` (add stepper badge `TextBlock`)
- Modify: `src/Clicky.Overlay/CursorOverlayWindow.xaml.cs` (new state + ForegroundGuard)

- [ ] **Step 1: Add multi-step methods to `IOverlayService`**

Append to `src/Clicky.Core/Overlay/IOverlayService.cs` (inside the interface):

```csharp
/// <summary>
/// Pins the triangle at the target coordinate and shows the "Step N of M" badge.
/// The triangle pulses slowly in this state instead of tracking the mouse cursor.
/// </summary>
void StartWaitingForStep(
    int claudeX, int claudeY,
    int screenshotWidth, int screenshotHeight,
    MonitorBounds monitorPhysBounds,
    string? label,
    int stepNumber,
    int totalSteps);

/// <summary>
/// Registers the target window HWND for foreground monitoring.
/// ForegroundLost fires when the user switches away from this window.
/// Pass <see cref="nint.Zero"/> to disable monitoring.
/// </summary>
void SetTargetHwnd(nint hwnd);

/// <summary>Fired on the UI thread when the foreground window no longer matches TargetHwnd.</summary>
event EventHandler? ForegroundLost;
```

- [ ] **Step 2: Add stepper badge to `CursorOverlayWindow.xaml`**

Inside the root `Canvas` of `CursorOverlayWindow.xaml`, add a `TextBlock` for the badge (exact position TBD at render time):

```xml
<!-- Step badge — top-right corner, visible only in WaitingForStep state -->
<Border x:Name="StepBadge"
        Background="#CC1A1A2E"
        CornerRadius="6"
        Padding="8,4"
        HorizontalAlignment="Right"
        VerticalAlignment="Top"
        Margin="0,12,12,0"
        Visibility="Collapsed">
    <TextBlock x:Name="StepBadgeText"
               Foreground="White"
               FontSize="13"
               FontWeight="SemiBold"
               FontFamily="Segoe UI" />
</Border>
```

Place this at the end of the root `Canvas`, after existing elements.

- [ ] **Step 3: Add `WaitingForStep` state and ForegroundGuard to `CursorOverlayWindow.xaml.cs`**

In the `OverlayState` enum, add:

```csharp
private enum OverlayState { Idle, Listening, Processing, WaitingForStep }
```

Add fields:

```csharp
private double _pinnedX, _pinnedY;
private int _stepNumber, _totalSteps;
private nint _targetHwnd;
private double _pulsePhase;
private const double PulsePeriodSeconds = 2.0;
```

Add `ForegroundLost` event:

```csharp
public event EventHandler? ForegroundLost;
```

Implement `SetTargetHwnd`:

```csharp
public void SetTargetHwnd(nint hwnd) => _targetHwnd = hwnd;
```

Implement `StartWaitingForStep` (call `Dispatcher.Invoke` since called from background):

```csharp
public void StartWaitingForStep(
    int claudeX, int claudeY,
    int screenshotWidth, int screenshotHeight,
    MonitorBounds monitorPhysBounds,
    string? label,
    int stepNumber,
    int totalSteps)
{
    Dispatcher.Invoke(() =>
    {
        var dip = ClaudeCoordToWpfDip(claudeX, claudeY, screenshotWidth, screenshotHeight, monitorPhysBounds);
        _pinnedX = dip.x;
        _pinnedY = dip.y;
        _stepNumber = stepNumber;
        _totalSteps = totalSteps;
        _pulsePhase = 0;
        _state = OverlayState.WaitingForStep;
        StepBadgeText.Text = $"Step {stepNumber} of {totalSteps}";
        StepBadge.Visibility = Visibility.Visible;
    });
}
```

In `OnMainTick` (the 60fps timer), add the foreground guard check at the start:

```csharp
// ForegroundGuard — fires when user leaves target app during a step sequence
if (_targetHwnd != nint.Zero && _state == OverlayState.WaitingForStep)
{
    var fg = PInvoke.GetForegroundWindow();
    if (fg != (Windows.Win32.Foundation.HWND)_targetHwnd)
    {
        _targetHwnd = nint.Zero;
        ForegroundLost?.Invoke(this, EventArgs.Empty);
    }
}
```

In `OnMainTick`, in the state-specific rendering section, add handling for `WaitingForStep`:

```csharp
case OverlayState.WaitingForStep:
    // Pulse the triangle scale slowly
    _pulsePhase += Dt / PulsePeriodSeconds * 2 * Math.PI;
    var pulse = 1.0 + Math.Sin(_pulsePhase) * 0.15;
    TriangleScale.ScaleX = pulse;
    TriangleScale.ScaleY = pulse;
    // Keep triangle pinned at target position
    Canvas.SetLeft(TriangleRoot, _pinnedX - TriangleRoot.ActualWidth / 2);
    Canvas.SetTop(TriangleRoot, _pinnedY - TriangleRoot.ActualHeight / 2);
    TriangleRoot.Visibility = Visibility.Visible;
    break;
```

When returning to `Idle` from `WaitingForStep`, hide the badge:

```csharp
public void ReturnToIdle()
{
    Dispatcher.Invoke(() =>
    {
        _state = OverlayState.Idle;
        StepBadge.Visibility = Visibility.Collapsed;
        _targetHwnd = nint.Zero;
    });
}
```

- [ ] **Step 4: Build**

```bash
dotnet build projects/clicky-win/src/Clicky.Overlay 2>&1 | grep -E "error" | head -10
```

Expected: no errors. (`CursorOverlayWindow.xaml.cs` is ~700 lines — navigate to the relevant sections by searching for `OverlayState`, `OnMainTick`, `ReturnToIdle`.)

- [ ] **Step 5: Run all tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests -v normal 2>&1 | tail -15
```

Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git -C projects/clicky-win add \
  src/Clicky.Core/Overlay/IOverlayService.cs \
  src/Clicky.Overlay/CursorOverlayWindow.xaml \
  src/Clicky.Overlay/CursorOverlayWindow.xaml.cs
git -C projects/clicky-win commit -m "feat: add WaitingForStep overlay state — pulsing pinned triangle, stepper badge, ForegroundGuard"
```

---

## Task 10: Wire multi-step orchestration into `CompanionOrchestrator`

**Files:**
- Modify: `src/Clicky.Services/Companion/CompanionOrchestrator.cs`
- Modify: `src/Clicky.Tests/CompanionOrchestratorTests.cs`

This is the integration task that wires all previous components together. The orchestrator gains four new dependencies: `IOverlayService`, `StepPlanStore`, `StepClickWatcher`, `IStepVerifier`.

- [ ] **Step 1: Add dependencies to `CompanionOrchestrator`**

Add to the constructor and field declarations:

```csharp
private readonly IOverlayService _overlay;
private readonly StepPlanStore _stepPlanStore;
private readonly StepClickWatcher _stepClickWatcher;
private readonly IStepVerifier _verifier;

public CompanionOrchestrator(
    IPushToTalkHook ptt,
    IScreenCaptureService capture,
    IMicrophoneRecorder mic,
    ITranscriptionService stt,
    ILlmService llm,
    ITtsService tts,
    IOverlayService overlay,
    StepPlanStore stepPlanStore,
    StepClickWatcher stepClickWatcher,
    IStepVerifier verifier)
{
    _ptt = ptt;
    _capture = capture;
    _mic = mic;
    _stt = stt;
    _llm = llm;
    _tts = tts;
    _overlay = overlay;
    _stepPlanStore = stepPlanStore;
    _stepClickWatcher = stepClickWatcher;
    _verifier = verifier;
}
```

- [ ] **Step 2: Wire overlay state changes + PTT cancellation**

In `Start()`, also wire overlay and PTT cancel of step sequence:

```csharp
public void Start()
{
    ObjectDisposedException.ThrowIf(_disposed != 0, this);
    _ptt.RecordingStarted += OnRecordingStarted;
    _ptt.RecordingStopped += OnRecordingStopped;
    _overlay.ForegroundLost += OnForegroundLost;
    _stepPlanStore.TimedOut += OnStepTimedOut;
    _stepClickWatcher.ClickConfirmed += OnStepClicked;
}
```

In `OnRecordingStarted` — cancel any active step sequence on new PTT press:

```csharp
private async void OnRecordingStarted(object? sender, EventArgs e)
{
    try
    {
        // Cancel active step sequence if any
        if (_stepPlanStore.IsActive)
        {
            _stepClickWatcher.Disarm();
            _stepPlanStore.Clear();
            _overlay.ReturnToIdle();
        }

        _pipelineCts?.Cancel();
        // ... rest of existing code ...
    }
    // ...
}
```

- [ ] **Step 3: Replace `StreamLlmToTtsAsync` with multi-step-aware streaming**

Replace the existing `StreamLlmToTtsAsync` and `RunPipelineAsync` with:

```csharp
private async Task RunPipelineAsync()
{
    var token = _pipelineCts?.Token ?? _cts.Token;

    _mic.AudioDataAvailable -= OnAudioDataAvailable;
    _mic.Stop();

    await _stt.DisconnectAsync(token).ConfigureAwait(false);
    _stt.TranscriptReceived -= OnTranscriptReceived;

    string transcript;
    lock (_transcriptLock) { transcript = _transcriptBuilder.ToString().Trim(); }
    if (string.IsNullOrEmpty(transcript))
    {
        Log.Information("PTT released with empty transcript — skipping LLM");
        return;
    }

    _overlay.StartProcessing();

    var targetHwnd = PInvoke.GetForegroundWindow();
    var capture = await _capture.CaptureAsync(token).ConfigureAwait(false);
    Log.Information("Captured {Bytes} bytes, transcript: {Transcript}", capture.Jpeg.Length, transcript);

    await StreamAndRouteAsync(capture, transcript, (nint)targetHwnd, token).ConfigureAwait(false);
}

private async Task StreamAndRouteAsync(ScreenCapture capture, string transcript, nint targetHwnd, CancellationToken token)
{
    var responseBuilder = new StringBuilder();
    var stepParser = new StepPlanParser();
    var isMultiStep = false;
    var stepsDelivered = 0;

    _stepPlanStore.Load(capture, targetHwnd);
    _overlay.SetTargetHwnd(targetHwnd);

    await foreach (var delta in _llm.StreamResponseAsync(
        capture.Jpeg, transcript, capture.Width, capture.Height, token).ConfigureAwait(false))
    {
        responseBuilder.Append(delta);

        foreach (var step in stepParser.Feed(delta))
        {
            if (!isMultiStep)
            {
                isMultiStep = true;
                _stepPlanStore.AppendHistory(new LlmMessage("user", transcript));
            }

            _stepPlanStore.AddStep(step);

            if (stepsDelivered == 0)
            {
                stepsDelivered++;
                await DeliverStepAsync(step, capture, token).ConfigureAwait(false);
            }
        }
    }

    if (!isMultiStep)
    {
        // Single-turn response
        _stepPlanStore.Clear();
        _overlay.SetTargetHwnd(nint.Zero);
        var result = PointTagParser.Parse(responseBuilder.ToString());
        _overlay.StartResponding();

        var ttsTask = _tts.SpeakAsync(result.SpokenText, token);
        if (result.HasPoint)
        {
            var flyTask = _overlay.FlyToAndShowBubbleAsync(
                result.X!.Value, result.Y!.Value,
                capture.Width, capture.Height,
                capture.MonitorPhysBounds, result.Label, token);
            await Task.WhenAll(ttsTask, flyTask).ConfigureAwait(false);
        }
        else
        {
            await ttsTask.ConfigureAwait(false);
        }

        _overlay.ReturnToIdle();
    }
    else
    {
        // Multi-step: record assistant response in history
        _stepPlanStore.AppendHistory(new LlmMessage("assistant", responseBuilder.ToString()));
    }
}

private async Task DeliverStepAsync(Step step, ScreenCapture capture, CancellationToken token)
{
    _overlay.StartResponding();
    var ttsTask = _tts.SpeakAsync(step.Text, token);

    if (step.HasCoords)
    {
        var flyTask = _overlay.FlyToAndShowBubbleAsync(
            step.X!.Value, step.Y!.Value,
            capture.Width, capture.Height,
            capture.MonitorPhysBounds, step.Label, token);
        await Task.WhenAll(ttsTask, flyTask).ConfigureAwait(false);

        _overlay.StartWaitingForStep(
            step.X.Value, step.Y.Value,
            capture.Width, capture.Height,
            capture.MonitorPhysBounds, step.Label,
            step.Number, _stepPlanStore.TotalSteps);

        _stepClickWatcher.Arm(step.X.Value, step.Y.Value);
    }
    else
    {
        await ttsTask.ConfigureAwait(false);
        _overlay.ReturnToIdle();
    }
}
```

- [ ] **Step 4: Handle click, timeout, and foreground lost events**

```csharp
private async void OnStepClicked(object? sender, EventArgs e)
{
    try
    {
        var token = _pipelineCts?.Token ?? _cts.Token;
        await RunVerifyAsync(token).ConfigureAwait(false);
    }
    catch (OperationCanceledException) { }
    catch (Exception ex) { Log.Error(ex, "Step verify error"); }
}

private async Task RunVerifyAsync(CancellationToken token)
{
    await Task.Delay(300, token).ConfigureAwait(false);

    var capture = await _capture.CaptureAsync(token).ConfigureAwait(false);
    var currentStep = _stepPlanStore.CurrentStep;
    if (currentStep is null) return;

    var result = await _verifier.VerifyAsync(
        capture.Jpeg, capture.Width, capture.Height,
        currentStep.Number, currentStep.Text,
        _stepPlanStore.History, token).ConfigureAwait(false);

    _stepPlanStore.AppendHistory(new LlmMessage("assistant", result.SpokenText));

    switch (result.Outcome)
    {
        case VerifyOutcome.Advance:
        {
            var nextIndex = _stepPlanStore.History.Count / 2; // rough step index
            _stepPlanStore.AdvanceTo(
                _stepPlanStore.AllSteps.IndexOf(currentStep) + 1,
                result.NextX, result.NextY, result.NextLabel);

            var nextStep = _stepPlanStore.CurrentStep;
            if (nextStep is not null)
                await DeliverStepAsync(nextStep, capture, token).ConfigureAwait(false);
            else
                await FinishSequenceAsync(result.SpokenText, capture, token).ConfigureAwait(false);
            break;
        }
        case VerifyOutcome.Correct:
            await _tts.SpeakAsync(result.SpokenText, token).ConfigureAwait(false);
            _stepClickWatcher.Arm(currentStep.X ?? 0, currentStep.Y ?? 0);
            break;

        case VerifyOutcome.Complete:
            await FinishSequenceAsync(result.SpokenText, capture, token).ConfigureAwait(false);
            break;
    }
}

private async Task FinishSequenceAsync(string spokenText, ScreenCapture capture, CancellationToken token)
{
    _stepPlanStore.Clear();
    _overlay.SetTargetHwnd(nint.Zero);
    await _tts.SpeakAsync(spokenText, token).ConfigureAwait(false);
    _overlay.ReturnToIdle();
}

private void OnStepTimedOut(object? sender, EventArgs e)
{
    _stepClickWatcher.Disarm();
    _overlay.ReturnToIdle();
    Log.Information("Step sequence timed out");
}

private void OnForegroundLost(object? sender, EventArgs e)
{
    _stepClickWatcher.Disarm();
    _stepPlanStore.Clear();
    _overlay.ReturnToIdle();
    Log.Information("Step sequence cancelled — user left target app");
}
```

Add `AllSteps` read access to `StepPlanStore`:
```csharp
public IReadOnlyList<Step> AllSteps => _steps;
```

- [ ] **Step 5: Update `Dispose` to unsubscribe all events**

```csharp
public void Dispose()
{
    if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
    _cts.Cancel();
    _pipelineCts?.Cancel();

    _ptt.RecordingStarted -= OnRecordingStarted;
    _ptt.RecordingStopped -= OnRecordingStopped;
    _overlay.ForegroundLost -= OnForegroundLost;
    _stepPlanStore.TimedOut -= OnStepTimedOut;
    _stepClickWatcher.ClickConfirmed -= OnStepClicked;
    _mic.AudioDataAvailable -= OnAudioDataAvailable;
    _stt.TranscriptReceived -= OnTranscriptReceived;

    _cts.Dispose();
    _pipelineCts?.Dispose();
}
```

- [ ] **Step 6: Update `CompanionOrchestratorTests` for new constructor**

Add mock stubs for the four new deps to `CreateFakes()`:

```csharp
private static (IPushToTalkHook ptt, IScreenCaptureService capture, IMicrophoneRecorder mic,
    ITranscriptionService stt, ILlmService llm, ITtsService tts,
    IOverlayService overlay, StepPlanStore store, StepClickWatcher watcher, IStepVerifier verifier)
    CreateFakes()
{
    var ptt = Substitute.For<IPushToTalkHook>();
    var capture = Substitute.For<IScreenCaptureService>();
    var mic = Substitute.For<IMicrophoneRecorder>();
    var stt = Substitute.For<ITranscriptionService>();
    var llm = Substitute.For<ILlmService>();
    var tts = Substitute.For<ITtsService>();
    var overlay = Substitute.For<IOverlayService>();
    var store = new StepPlanStore();
    var watcher = new StepClickWatcher(ptt);
    var verifier = Substitute.For<IStepVerifier>();
    return (ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier);
}

private static CompanionOrchestrator CreateOrchestrator(
    IPushToTalkHook ptt, IScreenCaptureService capture, IMicrophoneRecorder mic,
    ITranscriptionService stt, ILlmService llm, ITtsService tts,
    IOverlayService overlay, StepPlanStore store, StepClickWatcher watcher, IStepVerifier verifier)
    => new(ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier);
```

Update all existing test methods to use the expanded `CreateFakes()` and `CreateOrchestrator()` signatures. The `CapturePrimaryMonitorAsync` → `CaptureAsync` update from Task 2 also needs to be reflected here.

- [ ] **Step 7: Run all tests**

```bash
dotnet test projects/clicky-win/src/Clicky.Tests -v normal 2>&1 | tail -20
```

Expected: all tests pass. Fix any compilation errors from the orchestrator refactor.

- [ ] **Step 8: Commit**

```bash
git -C projects/clicky-win add \
  src/Clicky.Services/Companion/CompanionOrchestrator.cs \
  src/Clicky.Tests/CompanionOrchestratorTests.cs \
  src/Clicky.Services/MultiStep/StepPlanStore.cs
git -C projects/clicky-win commit -m "feat: wire multi-step orchestration — streaming step parser, verify loop, overlay integration"
```

---

## Task 11: Update `worker/index.js` for plan/verify modes

**Files:**
- Modify: `worker/index.js`

- [ ] **Step 1: Update worker to handle JSON body + plan/verify modes**

Replace `worker/index.js` entirely:

```javascript
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
```

- [ ] **Step 2: Test worker locally (optional)**

```bash
cd projects/clicky-win/worker && npx wrangler dev --local
```

Test plan mode:
```bash
curl -X POST http://localhost:8787 \
  -H "Content-Type: application/json" \
  -d '{"mode":"plan","screenshot":"<base64>","transcript":"how do I save this file","screenshotWidth":1920,"screenshotHeight":1080}'
```

Expected: SSE stream with `[STEP:...]` tags (if Claude decides multi-step) or `[POINT:...]` for single-turn.

- [ ] **Step 3: Commit**

```bash
git -C projects/clicky-win add worker/index.js
git -C projects/clicky-win commit -m "feat: update worker for plan/verify JSON modes and multi-step system prompt"
```

---

## Task 12: Register services + end-to-end smoke

**Files:**
- Modify: `src/Clicky.App/ServiceRegistration.cs`

- [ ] **Step 1: Register new services**

Replace `src/Clicky.App/ServiceRegistration.cs`:

```csharp
using Clicky.Capture.Audio;
using Clicky.Capture.Hotkeys;
using Clicky.Capture.ScreenCapture;
using Clicky.Core;
using Clicky.Overlay;
using Clicky.Services;
using Clicky.Services.Audio;
using Microsoft.Extensions.DependencyInjection;

namespace Clicky.App;

public static class ServiceRegistration
{
    /// <summary>Registers all Clicky application services.</summary>
    public static IServiceCollection AddClickyServices(this IServiceCollection services)
    {
        services.AddSingleton<IPushToTalkHook, PushToTalkHook>();
        services.AddSingleton<IScreenCaptureService, WgcCaptureService>();
        services.AddSingleton<IMicrophoneRecorder, WasapiMicrophoneRecorder>();
        services.AddSingleton<ITranscriptionService, AssemblyAITranscriptionService>();
        services.AddSingleton<IOverlayService, CursorOverlayWindow>();
        services.AddSingleton<StepPlanStore>();
        services.AddSingleton<StepClickWatcher>();
        services.AddSingleton<ICompanionOrchestrator, CompanionOrchestrator>();
        services.AddHttpClient<ILlmService, CloudflareWorkerLlmService>();
        services.AddHttpClient<ITtsService, CartesiaTtsService>();
        services.AddHttpClient<IStepVerifier, CloudflareWorkerVerifyService>();

        var workerUrl = Environment.GetEnvironmentVariable("WORKER_URL") ?? "https://httpbin.org/post";
        var assemblyAiApiKey = Environment.GetEnvironmentVariable("ASSEMBLYAI_API_KEY") ?? string.Empty;
        var cartesiaApiKey = Environment.GetEnvironmentVariable("CARTESIA_API_KEY") ?? string.Empty;
        var cartesiaVoiceId = Environment.GetEnvironmentVariable("CARTESIA_VOICE_ID") ?? string.Empty;
        services.AddSingleton(new CompanionSettings
        {
            WorkerUrl = workerUrl,
            AssemblyAiApiKey = assemblyAiApiKey,
            CartesiaApiKey = cartesiaApiKey,
            CartesiaVoiceId = string.IsNullOrEmpty(cartesiaVoiceId)
                ? "a0e99841-438c-4a64-b679-ae501e7d6091"
                : cartesiaVoiceId,
        });

        return services;
    }
}
```

- [ ] **Step 2: Run full build + test suite**

```bash
dotnet build projects/clicky-win 2>&1 | grep -E "^Build|error" | head -20
dotnet test projects/clicky-win/src/Clicky.Tests -v normal 2>&1 | tail -20
```

Expected: build succeeds, all tests pass.

- [ ] **Step 3: Smoke test — run the app**

```bash
dotnet run --project projects/clicky-win/src/Clicky.App
```

1. Press Ctrl+Win+Space and ask: **"How do I save a file in Notepad?"**
2. Verify: Clicky processes, speaks step 1, triangle flies to File menu, stepper badge appears ("Step 1 of N").
3. Click the File menu — verify: Clicky waits 300ms, speaks verification, advances to step 2.
4. Press Ctrl+Win+Space mid-sequence — verify: sequence cancels, overlay returns to idle.

- [ ] **Step 4: Commit**

```bash
git -C projects/clicky-win add src/Clicky.App/ServiceRegistration.cs
git -C projects/clicky-win commit -m "feat: register multi-step services in DI container"
```

---

## Self-Review Checklist

**Spec coverage:**
- [x] `StepPlanStore` — Task 6
- [x] `StepClickWatcher` ±60px radius — Task 8
- [x] `StepVerifier` 300ms wait + verify call — Task 10 (`RunVerifyAsync`)
- [x] `ForegroundGuard` in `OnMainTick` — Task 9
- [x] Overlay `WaitingForStep` + stepper badge — Task 9
- [x] Lazy coordinates (step 1 only; verify supplies rest) — Task 6 (`AdvanceTo`), Task 7 (`VerifyResult.NextX/Y`)
- [x] Incremental streaming parse — Task 5 (`StepPlanParser.Feed`)
- [x] Step 1 TTS starts without waiting for full plan — Task 10 (`stepsDelivered == 0` branch)
- [x] 30s timeout — Task 6 (`StepPlanStore(timeoutSeconds: 30)`)
- [x] PTT cancels sequence — Task 10 (`OnRecordingStarted` clears store)
- [x] Claude decides single-turn vs multi-step — Task 11 (system prompt + no client analysis)
- [x] Worker modes `plan` / `verify` — Task 11
- [x] Conversation history client-side — Task 10 (`StepPlanStore.History`, `AppendHistory`)

**Placeholder scan:** None found.

**Type consistency:**
- `ScreenCapture` used in Tasks 2, 3, 6, 9, 10 — consistent `record` with `Jpeg`, `Width`, `Height`, `MonitorPhysBounds`
- `Step` used in Tasks 4, 5, 6, 8, 10 — consistent `class` with `Number`, `Text`, `X?`, `Y?`, `Label?`, `HasCoords`
- `LlmMessage` used in Tasks 4, 6, 7, 10 — consistent `record(Role, Content)`
- `VerifyResult` used in Tasks 4, 7, 10 — consistent `record(Outcome, SpokenText, NextX?, NextY?, NextLabel?)`
- `StepPlanStore.AllSteps` added in Task 10 but defined in Task 6 — needs `public IReadOnlyList<Step> AllSteps => _steps;` in Task 6 implementation
- `IStepVerifier.VerifyAsync` defined in Task 4, implemented in Task 7, called in Task 10 — all use same 7-param signature
