using Clicky.Core;
using Clicky.Services;
using NSubstitute;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Clicky.Tests;

public class CompanionOrchestratorTests
{
    [Fact]
    public async Task OnRecordingStopped_PostsScreenshotToWorker()
    {
        // Arrange
        var ptt = Substitute.For<IPushToTalkHook>();
        var capture = Substitute.For<IScreenCaptureService>();
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0x00 };
        capture.CapturePrimaryMonitorAsync(Arg.Any<CancellationToken>()).Returns(jpeg);

        var handler = new StubHttpHandler(HttpStatusCode.OK, "Hello from worker");
        var http = new HttpClient(handler);
        var settings = new CompanionSettings { WorkerUrl = "https://test.example/post" };

        using var orchestrator = new CompanionOrchestrator(ptt, capture, http, settings);
        orchestrator.Start();

        // Act
        ptt.RecordingStopped += Raise.Event();

        // Assert — deterministic wait with 5s timeout
        var completed = await Task.WhenAny(handler.RequestReceivedTask, Task.Delay(5_000));
        Assert.Equal(handler.RequestReceivedTask, completed);
        Assert.True(handler.RequestCount > 0);
        Assert.Equal("https://test.example/post", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public void Start_WiresRecordingStoppedEvent()
    {
        // Arrange
        var ptt = Substitute.For<IPushToTalkHook>();
        var capture = Substitute.For<IScreenCaptureService>();
        var http = new HttpClient(new StubHttpHandler(HttpStatusCode.OK, "ok"));
        var settings = new CompanionSettings();

        using var orchestrator = new CompanionOrchestrator(ptt, capture, http, settings);

        // Act
        orchestrator.Start();

        // Assert — the event handler was subscribed
        ptt.Received().RecordingStopped += Arg.Any<EventHandler>();
    }

    [Fact]
    public void Dispose_UnwiresRecordingStoppedEvent()
    {
        // Arrange
        var ptt = Substitute.For<IPushToTalkHook>();
        var capture = Substitute.For<IScreenCaptureService>();
        var http = new HttpClient(new StubHttpHandler(HttpStatusCode.OK, "ok"));
        var settings = new CompanionSettings();

        var orchestrator = new CompanionOrchestrator(ptt, capture, http, settings);
        orchestrator.Start();

        // Act
        orchestrator.Dispose();

        // Assert — the event handler was unsubscribed on dispose
        ptt.Received().RecordingStopped -= Arg.Any<EventHandler>();
    }
}

/// <summary>Stub HTTP handler that records requests without making real network calls.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    public int RequestCount { get; private set; }
    public Uri? LastRequestUri { get; private set; }

    private readonly TaskCompletionSource<bool> _requestReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task RequestReceivedTask => _requestReceived.Task;

    public StubHttpHandler(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri;
        _requestReceived.TrySetResult(true);
        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body)
        });
    }
}
