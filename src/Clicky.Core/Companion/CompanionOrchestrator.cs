using System.Net.Http;
using System.Net.Http.Headers;
using Serilog;

namespace Clicky.Core;

/// <summary>
/// Wires push-to-talk release to screen capture and HTTP POST to the worker endpoint.
/// Uses async void for the event handler — intentional for event-based fire-and-forget;
/// the catch block prevents unhandled exceptions from crashing the process.
/// </summary>
public sealed class CompanionOrchestrator : ICompanionOrchestrator
{
    private readonly IPushToTalkHook _ptt;
    private readonly IScreenCaptureService _capture;
    private readonly HttpClient _http;
    private readonly CompanionSettings _settings;
    private bool _disposed;

    public CompanionOrchestrator(
        IPushToTalkHook ptt,
        IScreenCaptureService capture,
        HttpClient http,
        CompanionSettings settings)
    {
        _ptt = ptt;
        _capture = capture;
        _http = http;
        _settings = settings;
    }

    /// <inheritdoc/>
    public void Start()
    {
        _ptt.RecordingStopped += OnRecordingStopped;
    }

    private async void OnRecordingStopped(object? sender, EventArgs e)
    {
        try
        {
            Log.Information("PTT released — capturing screen");
            var jpeg = await _capture.CapturePrimaryMonitorAsync();

            Log.Information("Captured {Bytes} bytes — posting to {Url}", jpeg.Length, _settings.WorkerUrl);

            using var content = new MultipartFormDataContent();
            using var imageContent = new ByteArrayContent(jpeg);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(imageContent, "screenshot", "screenshot.jpg");

            using var response = await _http.PostAsync(_settings.WorkerUrl, content);
            var body = await response.Content.ReadAsStringAsync();
            Log.Information("Worker response [{Status}]: {Body}",
                (int)response.StatusCode, body.Length > 500 ? body[..500] + "..." : body);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Companion loop error");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ptt.RecordingStopped -= OnRecordingStopped;
    }
}
