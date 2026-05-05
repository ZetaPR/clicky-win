using System.Text;
using Clicky.Core;
using Serilog;

namespace Clicky.Services;

/// <summary>
/// Orchestrates the full voice pipeline: mic recording → STT → LLM → TTS.
/// Subscribed to PTT events. RecordingStarted opens the pipeline; RecordingStopped
/// stops the mic, waits for a final transcript, then streams the LLM response to TTS.
/// </summary>
public sealed class CompanionOrchestrator : ICompanionOrchestrator
{
    private readonly IPushToTalkHook _ptt;
    private readonly IScreenCaptureService _capture;
    private readonly IMicrophoneRecorder _mic;
    private readonly ITranscriptionService _stt;
    private readonly ILlmService _llm;
    private readonly ITtsService _tts;
    private readonly CancellationTokenSource _cts = new();
    private volatile CancellationTokenSource? _pipelineCts;
    private readonly StringBuilder _transcriptBuilder = new();
    private readonly object _transcriptLock = new();
    private int _disposed;

    /// <summary>
    /// Initializes a new <see cref="CompanionOrchestrator"/> with all pipeline dependencies.
    /// </summary>
    public CompanionOrchestrator(
        IPushToTalkHook ptt,
        IScreenCaptureService capture,
        IMicrophoneRecorder mic,
        ITranscriptionService stt,
        ILlmService llm,
        ITtsService tts)
    {
        _ptt = ptt;
        _capture = capture;
        _mic = mic;
        _stt = stt;
        _llm = llm;
        _tts = tts;
    }

    /// <inheritdoc/>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _ptt.RecordingStarted += OnRecordingStarted;
        _ptt.RecordingStopped += OnRecordingStopped;
    }

    /// <summary>
    /// Handles PTT press: cancels any in-flight pipeline, starts mic recording and STT session.
    /// </summary>
    private async void OnRecordingStarted(object? sender, EventArgs e)
    {
        try
        {
            _pipelineCts?.Cancel();
            _pipelineCts?.Dispose();
            _pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);

            lock (_transcriptLock) _transcriptBuilder.Clear();
            _stt.TranscriptReceived += OnTranscriptReceived;

            await _stt.ConnectAsync(_pipelineCts.Token).ConfigureAwait(false);

            _mic.AudioDataAvailable += OnAudioDataAvailable;
            _mic.Start();
        }
        catch (OperationCanceledException)
        {
            // Shutdown — silent
        }
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

    /// <summary>
    /// Handles PTT release: stops mic, waits for final STT transcript,
    /// then streams the LLM response through TTS.
    /// </summary>
    private async void OnRecordingStopped(object? sender, EventArgs e)
    {
        try
        {
            await RunPipelineAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is normal on new PTT press or shutdown — silent
        }
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

        var jpeg = await _capture.CapturePrimaryMonitorAsync(token).ConfigureAwait(false);
        Log.Information("Captured {Bytes} bytes, transcript: {Transcript}", jpeg.Length, transcript);

        await StreamLlmToTtsAsync(jpeg, transcript, token).ConfigureAwait(false);
    }

    private async Task StreamLlmToTtsAsync(byte[] jpeg, string transcript, CancellationToken token)
    {
        await foreach (var delta in _llm.StreamResponseAsync(jpeg, transcript, token).ConfigureAwait(false))
        {
            await _tts.SpeakAsync(delta, token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _cts.Cancel();
        _pipelineCts?.Cancel();

        _ptt.RecordingStarted -= OnRecordingStarted;
        _ptt.RecordingStopped -= OnRecordingStopped;
        _mic.AudioDataAvailable -= OnAudioDataAvailable;
        _stt.TranscriptReceived -= OnTranscriptReceived;

        _cts.Dispose();
        _pipelineCts?.Dispose();
    }
}
