using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Clicky.Core;
using Serilog;

namespace Clicky.Services;

/// <summary>
/// Streams PCM audio to AssemblyAI's streaming v3 WebSocket API and raises
/// <see cref="ITranscriptionService.TranscriptReceived"/> for each partial and final segment.
/// </summary>
public sealed class AssemblyAITranscriptionService : ITranscriptionService
{
    // AssemblyAI streaming v3: 16 kHz signed 16-bit PCM LE
    private const string WS_URL = "wss://streaming.assemblyai.com/v3/ws?sample_rate=16000&encoding=pcm_s16le";
    private const string TERMINATE_MSG = """{"terminate_session":true}""";

    private readonly CompanionSettings _settings;
    private readonly ClientWebSocket _ws = new();
    private readonly CancellationTokenSource _loopCts = new();
    private readonly TaskCompletionSource _sessionTerminated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private Task? _receiveLoop;
    private volatile int _disposed;

    /// <inheritdoc/>
    public event EventHandler<TranscriptReceivedEventArgs>? TranscriptReceived;

    /// <summary>Initializes the service with the given companion settings.</summary>
    public AssemblyAITranscriptionService(CompanionSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        _ws.Options.SetRequestHeader("Authorization", _settings.AssemblyAiApiKey);
        await _ws.ConnectAsync(new Uri(WS_URL), cancellationToken).ConfigureAwait(false);
        Log.Information("AssemblyAI WebSocket connected");
        _receiveLoop = Task.Run(() => RunReceiveLoopAsync(_loopCts.Token), _loopCts.Token);
    }

    /// <inheritdoc/>
    public async Task SendAudioAsync(byte[] pcmData, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _ws.SendAsync(
                new ArraySegment<byte>(pcmData),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        await SendTerminateSignalAsync(cancellationToken).ConfigureAwait(false);
        await WaitForSessionTerminatedAsync(cancellationToken).ConfigureAwait(false);
        await CloseWebSocketAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SendTerminateSignalAsync(CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(TERMINATE_MSG);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }

        Log.Information("AssemblyAI terminate_session sent");
    }

    private async Task WaitForSessionTerminatedAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            await _sessionTerminated.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Timed out waiting for AssemblyAI SessionTerminated");
        }
    }

    private async Task CloseWebSocketAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (WebSocketException ex)
        {
            Log.Warning(ex, "WebSocket already closed during CloseAsync");
        }
    }

    private async Task RunReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[32 * 1024];

        try
        {
            while (_ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var message = await ReceiveFullMessageAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (message is null) break;
                ProcessMessage(message);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (WebSocketException ex)
        {
            Log.Warning(ex, "AssemblyAI WebSocket receive error");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error in AssemblyAI receive loop");
        }
    }

    private async Task<string?> ReceiveFullMessageAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        using var ms = new System.IO.MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken)
                .ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close) return null;
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private void ProcessMessage(string json)
    {
        var (text, isFinal, isTerminated) = ParseMessage(json);

        if (isTerminated)
        {
            Log.Information("AssemblyAI SessionTerminated received");
            _sessionTerminated.TrySetResult();
            return;
        }

        if (text.Length > 0)
        {
            TranscriptReceived?.Invoke(this, new TranscriptReceivedEventArgs(text, isFinal));
        }
    }

    /// <summary>
    /// Parses a raw JSON message from AssemblyAI's streaming API.
    /// Returns the transcript text, whether it is final, and whether it signals session termination.
    /// </summary>
    internal static (string text, bool isFinal, bool isTerminated) ParseMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                return (string.Empty, false, false);

            var type = typeProp.GetString() ?? string.Empty;

            return type switch
            {
                "PartialTranscript" => (GetText(root), false, false),
                "FinalTranscript" => (GetText(root), true, false),
                "SessionTerminated" => (string.Empty, false, true),
                _ => (string.Empty, false, false),
            };
        }
        catch (JsonException ex)
        {
            Log.Warning(ex, "Failed to parse AssemblyAI message: {Json}", json);
            return (string.Empty, false, false);
        }
    }

    private static string GetText(JsonElement root)
    {
        if (root.TryGetProperty("text", out var textProp))
            return textProp.GetString() ?? string.Empty;
        return string.Empty;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _loopCts.Cancel();
        _ = _receiveLoop?.ContinueWith(
            t => Log.Error(t.Exception, "Receive loop faulted after dispose"),
            TaskContinuationOptions.OnlyOnFaulted);
        _loopCts.Dispose();

        _sendLock.Dispose();

        if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.Connecting)
        {
            try
            {
                _ws.Abort();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error aborting AssemblyAI WebSocket during dispose");
            }
        }

        _ws.Dispose();
    }
}
