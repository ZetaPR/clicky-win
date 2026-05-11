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
