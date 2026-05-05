using Clicky.Services;
using Xunit;

namespace Clicky.Tests;

public class LlmServiceTests
{
    private static async Task<List<string>> CollectAsync(IAsyncEnumerable<string> source)
    {
        var results = new List<string>();
        await foreach (var item in source)
            results.Add(item);
        return results;
    }

    [Fact]
    public async Task ParseSseStreamAsync_YieldsTextDeltas_FromContentBlockDeltaEvents()
    {
        // Arrange
        const string sse = """
            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":" world"}}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        using var reader = new StringReader(sse);

        // Act
        var deltas = await CollectAsync(
            CloudflareWorkerLlmService.ParseSseStreamAsync(reader, CancellationToken.None));

        // Assert
        Assert.Equal(2, deltas.Count);
        Assert.Equal("Hello", deltas[0]);
        Assert.Equal(" world", deltas[1]);
    }

    [Fact]
    public async Task ParseSseStreamAsync_StopsYielding_AfterMessageStopEvent()
    {
        // Arrange
        const string sse = """
            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"First"}}

            event: message_stop
            data: {"type":"message_stop"}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"ShouldNotAppear"}}

            """;

        using var reader = new StringReader(sse);

        // Act
        var deltas = await CollectAsync(
            CloudflareWorkerLlmService.ParseSseStreamAsync(reader, CancellationToken.None));

        // Assert
        Assert.Single(deltas);
        Assert.Equal("First", deltas[0]);
    }

    [Fact]
    public async Task ParseSseStreamAsync_IgnoresEvents_OtherThanContentBlockDelta()
    {
        // Arrange
        const string sse = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_01","type":"message"}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hi"}}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        using var reader = new StringReader(sse);

        // Act
        var deltas = await CollectAsync(
            CloudflareWorkerLlmService.ParseSseStreamAsync(reader, CancellationToken.None));

        // Assert
        Assert.Single(deltas);
        Assert.Equal("Hi", deltas[0]);
    }

    [Fact]
    public async Task ParseSseStreamAsync_HandlesEmptyDelimiterLines_WithoutCrashing()
    {
        // Arrange — multiple blank lines between events
        const string sse = """
            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"A"}}


            event: message_stop
            data: {"type":"message_stop"}

            """;

        using var reader = new StringReader(sse);

        // Act
        var deltas = await CollectAsync(
            CloudflareWorkerLlmService.ParseSseStreamAsync(reader, CancellationToken.None));

        // Assert — delta before the stop must still be yielded despite extra blank line
        Assert.Single(deltas);
        Assert.Equal("A", deltas[0]);
    }

    [Fact]
    public async Task ParseSseStreamAsync_SkipsNonTextDeltaTypes_SuchAsInputJsonDelta()
    {
        // Arrange
        const string sse = """
            event: content_block_delta
            data: {"type":"content_block_delta","index":1,"delta":{"type":"input_json_delta","partial_json":"{\"key\":"}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Real"}}

            event: message_stop
            data: {"type":"message_stop"}

            """;

        using var reader = new StringReader(sse);

        // Act
        var deltas = await CollectAsync(
            CloudflareWorkerLlmService.ParseSseStreamAsync(reader, CancellationToken.None));

        // Assert
        Assert.Single(deltas);
        Assert.Equal("Real", deltas[0]);
    }
}
