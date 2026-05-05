using Clicky.Core;
using Clicky.Services.Audio;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Clicky.Tests;

public class TtsServiceTests
{
    // ---------------------------------------------------------------------------
    // Test 1: correct Content-Type and Cartesia-Version headers on outgoing request
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task FetchAudioStreamAsync_SetsCartesiaVersionAndContentTypeHeaders()
    {
        // Arrange
        HttpRequestMessage? captured = null;
        var handler = new CapturingStubHandler(HttpStatusCode.OK, Array.Empty<byte>(), req => captured = req);
        var http = new HttpClient(handler);

        // Act
        using var stream = await CartesiaTtsService.FetchAudioStreamAsync(
            http, apiKey: "test-key", voiceId: "voice-123", text: "Hello", ct: CancellationToken.None);

        // Assert
        Assert.NotNull(captured);
        Assert.True(captured!.Headers.Contains("Cartesia-Version"));
        Assert.Equal("2024-06-10", captured.Headers.GetValues("Cartesia-Version").First());
        Assert.Equal("application/json", captured.Content!.Headers.ContentType?.MediaType);
    }

    // ---------------------------------------------------------------------------
    // Test 2: correct JSON body (model_id, transcript, voice.id, output_format.encoding)
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task FetchAudioStreamAsync_SendsCorrectJsonBody()
    {
        // Arrange — capture body eagerly inside the handler before HttpRequestMessage is disposed
        string? capturedBody = null;
        var handler = new CapturingStubHandler(HttpStatusCode.OK, Array.Empty<byte>(), async req =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync().ConfigureAwait(false);
        });
        var http = new HttpClient(handler);

        // Act
        using var stream = await CartesiaTtsService.FetchAudioStreamAsync(
            http, apiKey: "key", voiceId: "voice-abc", text: "Test text", ct: CancellationToken.None);

        // Assert
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;

        Assert.Equal("sonic-2", root.GetProperty("model_id").GetString());
        Assert.Equal("Test text", root.GetProperty("transcript").GetString());
        Assert.Equal("voice-abc", root.GetProperty("voice").GetProperty("id").GetString());
        Assert.Equal("pcm_s16le", root.GetProperty("output_format").GetProperty("encoding").GetString());
    }

    // ---------------------------------------------------------------------------
    // Test 3: response body bytes are returned as-is
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task FetchAudioStreamAsync_ReturnsBytesFromResponseBody()
    {
        // Arrange
        var expectedBytes = new byte[] { 0x01, 0x02, 0x03, 0x04, 0xFF, 0xFE };
        var handler = new CapturingStubHandler(HttpStatusCode.OK, expectedBytes);
        var http = new HttpClient(handler);

        // Act
        using var stream = await CartesiaTtsService.FetchAudioStreamAsync(
            http, apiKey: "key", voiceId: "voice-id", text: "Hi", ct: CancellationToken.None);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);

        // Assert
        Assert.Equal(expectedBytes, ms.ToArray());
    }

    // ---------------------------------------------------------------------------
    // Test 4: throws OperationCanceledException when CancellationToken is cancelled before request
    // ---------------------------------------------------------------------------
    [Fact]
    public async Task FetchAudioStreamAsync_ThrowsOperationCanceledException_WhenTokenCancelledBeforeRequest()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var handler = new CapturingStubHandler(HttpStatusCode.OK, Array.Empty<byte>());
        var http = new HttpClient(handler);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CartesiaTtsService.FetchAudioStreamAsync(
                http, apiKey: "key", voiceId: "voice-id", text: "Text", ct: cts.Token));
    }
}

/// <summary>
/// Stub HTTP handler that captures the outgoing request and returns a configurable response body.
/// Supports both synchronous and asynchronous inspection callbacks.
/// </summary>
internal sealed class CapturingStubHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly byte[] _responseBody;
    private readonly Func<HttpRequestMessage, Task>? _onRequest;

    public CapturingStubHandler(
        HttpStatusCode status,
        byte[] responseBody,
        Func<HttpRequestMessage, Task>? onRequest = null)
    {
        _status = status;
        _responseBody = responseBody;
        _onRequest = onRequest;
    }

    /// <summary>Convenience constructor accepting a synchronous callback.</summary>
    public CapturingStubHandler(
        HttpStatusCode status,
        byte[] responseBody,
        Action<HttpRequestMessage> onRequest)
        : this(status, responseBody, req => { onRequest(req); return Task.CompletedTask; })
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_onRequest != null)
            await _onRequest(request).ConfigureAwait(false);
        return new HttpResponseMessage(_status)
        {
            Content = new ByteArrayContent(_responseBody),
        };
    }
}
