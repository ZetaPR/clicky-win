using Clicky.Core;
using Clicky.Services;
using NSubstitute;
using Xunit;

namespace Clicky.Tests;

public class CompanionOrchestratorTests
{
    private static (IPushToTalkHook ptt, IScreenCaptureService capture, IMicrophoneRecorder mic,
        ITranscriptionService stt, ILlmService llm, ITtsService tts)
        CreateFakes()
    {
        var ptt = Substitute.For<IPushToTalkHook>();
        var capture = Substitute.For<IScreenCaptureService>();
        var mic = Substitute.For<IMicrophoneRecorder>();
        var stt = Substitute.For<ITranscriptionService>();
        var llm = Substitute.For<ILlmService>();
        var tts = Substitute.For<ITtsService>();
        return (ptt, capture, mic, stt, llm, tts);
    }

    private static CompanionOrchestrator CreateOrchestrator(
        IPushToTalkHook ptt, IScreenCaptureService capture, IMicrophoneRecorder mic,
        ITranscriptionService stt, ILlmService llm, ITtsService tts)
        => new(ptt, capture, mic, stt, llm, tts);

    /// <summary>
    /// Yields an async stream from an array of strings for use in LLM mock setup.
    /// </summary>
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
        var (ptt, capture, mic, stt, llm, tts) = CreateFakes();
        using var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts);
        orchestrator.Start();

        // Act
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(100); // allow async void handler to complete

        // Assert
        await stt.Received().ConnectAsync(Arg.Any<CancellationToken>());
        mic.Received().Start();
    }

    [Fact]
    public async Task OnRecordingStopped_WithTranscript_CallsLlmAndTts()
    {
        // Arrange
        var (ptt, capture, mic, stt, llm, tts) = CreateFakes();

        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };
        capture.CapturePrimaryMonitorAsync(Arg.Any<CancellationToken>()).Returns(jpeg);

        llm.StreamResponseAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
           .Returns(AsyncEnumerableReturn("Hello", " world"));

        using var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts);
        orchestrator.Start();

        // Start recording
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(100);

        // Fire a final transcript
        stt.TranscriptReceived += Raise.EventWith(new TranscriptReceivedEventArgs("test query", isFinal: true));

        // Act — stop recording
        ptt.RecordingStopped += Raise.Event();
        await Task.Delay(200); // allow async void handler and pipeline to complete

        // Assert — TTS was called at least once with any string
        await tts.Received().SpeakAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await stt.Received().DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnRecordingStopped_WithEmptyTranscript_DoesNotCallLlm()
    {
        // Arrange
        var (ptt, capture, mic, stt, llm, tts) = CreateFakes();

        using var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts);
        orchestrator.Start();

        // Start then stop without any transcript
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(100);

        ptt.RecordingStopped += Raise.Event();
        await Task.Delay(200);

        // Assert — LLM should not be called
        // StreamResponseAsync returns IAsyncEnumerable — not awaitable; verify call count directly
        llm.DidNotReceive().StreamResponseAsync(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnRecordingStarted_WhilePipelineRunning_CancelsPreviousPipeline()
    {
        // Arrange
        var (ptt, capture, mic, stt, llm, tts) = CreateFakes();

        // First ConnectAsync blocks until its token is cancelled
        stt.ConnectAsync(Arg.Any<CancellationToken>()).Returns(async ci =>
        {
            await Task.Delay(Timeout.Infinite, (CancellationToken)ci[0]);
        });

        using var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts);
        orchestrator.Start();

        // Act — first press starts the blocking ConnectAsync
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(50); // let first pipeline get into ConnectAsync

        // Second press should cancel the first and start a new session
        ptt.RecordingStarted += Raise.Event();
        await Task.Delay(100); // let second pipeline begin

        // Assert — ConnectAsync was called twice (second press started a new session)
        await stt.Received(2).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var (ptt, capture, mic, stt, llm, tts) = CreateFakes();
        var orchestrator = CreateOrchestrator(ptt, capture, mic, stt, llm, tts);

        // Act + Assert — no exception on double dispose
        orchestrator.Dispose();
        orchestrator.Dispose();
    }

}
