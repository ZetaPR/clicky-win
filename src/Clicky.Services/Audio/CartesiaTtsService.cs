using System.Net.Http.Json;
using System.Text.Json;
using Clicky.Core;
using NAudio.Wave;

namespace Clicky.Services.Audio;

/// <summary>
/// Synthesizes text to speech via the Cartesia Sonic HTTP streaming API and
/// plays the resulting PCM audio in real-time through the default WASAPI output device.
/// </summary>
public sealed class CartesiaTtsService : ITtsService
{
    private static readonly WaveFormat AudioFormat = new(rate: 22050, bits: 16, channels: 1);
    private const int BufferBytes = 22050 * 2 * 4; // 4-second buffer
    private const int ChunkBytes = 4096;

    private readonly HttpClient _http;
    private readonly CompanionSettings _settings;
    private int _disposed;

    /// <summary>Initializes a new instance of <see cref="CartesiaTtsService"/>.</summary>
    /// <param name="http">The <see cref="HttpClient"/> used to call the Cartesia API.</param>
    /// <param name="settings">Application settings containing API key and voice ID.</param>
    public CartesiaTtsService(HttpClient http, CompanionSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    /// <inheritdoc/>
    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        using var audioStream = await FetchAudioStreamAsync(
            _http, _settings.CartesiaApiKey, _settings.CartesiaVoiceId, text, cancellationToken)
            .ConfigureAwait(false);

        var buffer = new BufferedWaveProvider(AudioFormat)
        {
            BufferLength = BufferBytes,
            DiscardOnBufferOverflow = false,
        };

        using var player = new WasapiOut();
        player.Init(buffer);
        player.Play();

        var chunk = new byte[ChunkBytes];
        try
        {
            int bytesRead;
            while ((bytesRead = await audioStream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                buffer.AddSamples(chunk, 0, bytesRead);
            }

            // Wait for playback to drain
            while (buffer.BufferedDuration > TimeSpan.Zero)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            player.Stop();
            throw;
        }

        player.Stop();
    }

    /// <summary>
    /// Sends a TTS synthesis request to the Cartesia API and returns the raw PCM response stream.
    /// Extracted for testability — callers can verify request shape without real audio hardware.
    /// </summary>
    /// <param name="http">The HTTP client to use.</param>
    /// <param name="apiKey">The Cartesia API key.</param>
    /// <param name="voiceId">The Cartesia voice ID.</param>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw PCM audio stream from the Cartesia API.</returns>
    internal static async Task<Stream> FetchAudioStreamAsync(
        HttpClient http,
        string apiKey,
        string voiceId,
        string text,
        CancellationToken ct)
    {
        var requestBody = new
        {
            model_id = "sonic-2",
            transcript = text,
            voice = new { mode = "id", id = voiceId },
            output_format = new
            {
                container = "raw",
                encoding = "pcm_s16le",
                sample_rate = 22050,
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.cartesia.ai/tts/bytes");
        request.Headers.Add("Cartesia-Version", "2024-06-10");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(requestBody);

        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _http.Dispose();
        }
    }
}
