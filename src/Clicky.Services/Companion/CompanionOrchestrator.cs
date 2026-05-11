using System.Runtime.InteropServices;
using System.Text;
using Clicky.Core;
using Serilog;

namespace Clicky.Services;

/// <summary>
/// Orchestrates the full voice pipeline: mic recording → STT → LLM → TTS.
/// Handles both single-turn (POINT) and multi-step (STEP) response modes.
/// </summary>
public sealed class CompanionOrchestrator : ICompanionOrchestrator
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    private readonly IPushToTalkHook _ptt;
    private readonly IScreenCaptureService _capture;
    private readonly IMicrophoneRecorder _mic;
    private readonly ITranscriptionService _stt;
    private readonly ILlmService _llm;
    private readonly ITtsService _tts;
    private readonly IOverlayService _overlay;
    private readonly StepPlanStore _stepPlanStore;
    private readonly StepClickWatcher _stepClickWatcher;
    private readonly IStepVerifier _verifier;
    private readonly CancellationTokenSource _cts = new();
    private volatile CancellationTokenSource? _pipelineCts;
    private readonly StringBuilder _transcriptBuilder = new();
    private readonly object _transcriptLock = new();
    private int _disposed;

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

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _ptt.RecordingStarted += OnRecordingStarted;
        _ptt.RecordingStopped += OnRecordingStopped;
        _overlay.ForegroundLost += OnForegroundLost;
        _stepPlanStore.TimedOut += OnStepTimedOut;
        _stepClickWatcher.ClickConfirmed += OnStepClicked;
    }

    private async void OnRecordingStarted(object? sender, EventArgs e)
    {
        try
        {
            if (_stepPlanStore.IsActive)
            {
                _stepClickWatcher.Disarm();
                _stepPlanStore.Clear();
                _overlay.ReturnToIdle();
            }

            _pipelineCts?.Cancel();
            _pipelineCts?.Dispose();
            _pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

            lock (_transcriptLock) _transcriptBuilder.Clear();
            _stt.TranscriptReceived += OnTranscriptReceived;

            await _stt.ConnectAsync(_pipelineCts.Token).ConfigureAwait(false);

            _mic.AudioDataAvailable += OnAudioDataAvailable;
            _mic.Start();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting recording pipeline");
        }
    }

    private void OnAudioDataAvailable(object? sender, byte[] audioData)
    {
        if (_pipelineCts is null) return;
        _ = _stt.SendAudioAsync(audioData, _pipelineCts.Token)
            .ContinueWith(
                t => Log.Error(t.Exception, "STT send error"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
    }

    private void OnTranscriptReceived(object? sender, TranscriptReceivedEventArgs e)
    {
        if (e.IsFinal && !string.IsNullOrWhiteSpace(e.Text))
            lock (_transcriptLock) _transcriptBuilder.Append(e.Text);
    }

    private async void OnRecordingStopped(object? sender, EventArgs e)
    {
        try
        {
            await RunPipelineAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Companion pipeline error");
        }
    }

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

        var targetHwnd = (nint)GetForegroundWindow();
        var capture = await _capture.CaptureAsync(token).ConfigureAwait(false);
        Log.Information("Captured {Bytes} bytes, transcript: {Transcript}", capture.Jpeg.Length, transcript);

        await StreamAndRouteAsync(capture, transcript, targetHwnd, token).ConfigureAwait(false);
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
                // Step numbers are 1-based; the 0-based index of the next step = currentStep.Number
                _stepPlanStore.AdvanceTo(currentStep.Number, result.NextX, result.NextY, result.NextLabel);

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
}
