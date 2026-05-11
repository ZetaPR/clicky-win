using Clicky.Core;
using Clicky.Services;
using NSubstitute;
using Xunit;

namespace Clicky.Tests;

public class CompanionOrchestratorTests
{
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

    private static async IAsyncEnumerable<string> AsyncEnumerableReturn(params string[] items)
    {
        foreach (var item in items)
            yield return item;
        await Task.CompletedTask;
    }

    [Fact]
    public async Task OnRecordingStarted_CallsConnectAndStartsMic()
    {
        // Arrange
        var (ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier) = CreateFakes();
        using var watcher2 = watcher;
        using var store2 = store;
        using var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier);
        orchestrator.Start();

        // Act
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(100);

        // Assert
        await stt.Received().ConnectAsync(Arg.Any<CancellationToken>());
        mic.Received().Start();
    }

    [Fact]
    public async Task OnRecordingStopped_WithTranscript_CallsLlmAndTts()
    {
        // Arrange
        var (ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier) = CreateFakes();
        using var watcher2 = watcher;
        using var store2 = store;

        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };
        var screenCapture = new ScreenCapture(jpeg, 1920, 1080, new MonitorBounds(0, 0, 1920, 1080));
        capture.CaptureAsync(Arg.Any<CancellationToken>()).Returns(screenCapture);

        llm.StreamResponseAsync(Arg.Any<byte[]>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
           .Returns(AsyncEnumerableReturn("Hello", " world"));

        overlay.FlyToAndShowBubbleAsync(
            Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<MonitorBounds>(), Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        using var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier);
        orchestrator.Start();

        // Start recording
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(100);

        // Fire a final transcript
        stt.TranscriptReceived += Raise.EventWith(new TranscriptReceivedEventArgs("test query", isFinal: true));

        // Act — stop recording
        ptt.RecordingStopped += Raise.Event();
        await Task.Delay(200);

        // Assert — TTS was called at least once with any string
        await tts.Received().SpeakAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await stt.Received().DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnRecordingStopped_WithEmptyTranscript_DoesNotCallLlm()
    {
        // Arrange
        var (ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier) = CreateFakes();
        using var watcher2 = watcher;
        using var store2 = store;

        using var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier);
        orchestrator.Start();

        // Start then stop without any transcript
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(100);

        ptt.RecordingStopped += Raise.Event();
        await Task.Delay(200);

        // Assert — LLM should not be called
        llm.DidNotReceive().StreamResponseAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnRecordingStarted_WhilePipelineRunning_CancelsPreviousPipeline()
    {
        // Arrange
        var (ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier) = CreateFakes();
        using var watcher2 = watcher;
        using var store2 = store;

        // First ConnectAsync blocks until its token is cancelled
        stt.ConnectAsync(Arg.Any<CancellationToken>()).Returns(async ci =>
        {
            await Task.Delay(Timeout.Infinite, (CancellationToken)ci[0]);
        });

        using var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier);
        orchestrator.Start();

        // Act — first press starts the blocking ConnectAsync
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(50);

        // Second press should cancel the first and start a new session
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(100);

        // Assert — ConnectAsync was called twice (second press started a new session)
        await stt.Received(2).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var (ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier) = CreateFakes();
        using var watcher2 = watcher;
        using var store2 = store;
        var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts, overlay, store, watcher, verifier);

        // Act + Assert — no exception on double dispose
        orchestrator.Dispose();
        orchestrator.Dispose();
    }
}
