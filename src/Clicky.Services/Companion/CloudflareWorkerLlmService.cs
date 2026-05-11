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
