using Clicky.Services;
using Xunit;

namespace Clicky.Tests;

public class TranscriptionServiceTests
{
    [Fact]
    public void ParseMessage_PartialTranscript_ReturnsTextNotFinalNotTerminated()
    {
        // Arrange
        const string json = """{"type":"PartialTranscript","text":"hello"}""";

        // Act
        var (text, isFinal, isTerminated) = AssemblyAITranscriptionService.ParseMessage(json);

        // Assert
        Assert.Equal("hello", text);
        Assert.False(isFinal);
        Assert.False(isTerminated);
    }

    [Fact]
    public void ParseMessage_FinalTranscript_ReturnsTextIsFinalNotTerminated()
    {
        // Arrange
        const string json = """{"type":"FinalTranscript","text":"Hello world."}""";

        // Act
        var (text, isFinal, isTerminated) = AssemblyAITranscriptionService.ParseMessage(json);

        // Assert
        Assert.Equal("Hello world.", text);
        Assert.True(isFinal);
        Assert.False(isTerminated);
    }

    [Fact]
    public void ParseMessage_SessionTerminated_ReturnsEmptyTextAndIsTerminated()
    {
        // Arrange
        const string json = """{"type":"SessionTerminated"}""";

        // Act
        var (text, isFinal, isTerminated) = AssemblyAITranscriptionService.ParseMessage(json);

        // Assert
        Assert.Equal(string.Empty, text);
        Assert.False(isFinal);
        Assert.True(isTerminated);
    }

    [Fact]
    public void ParseMessage_SessionBegins_ReturnsEmptyAndAllFalse()
    {
        // Arrange
        const string json = """{"type":"SessionBegins","session_id":"abc-123"}""";

        // Act
        var (text, isFinal, isTerminated) = AssemblyAITranscriptionService.ParseMessage(json);

        // Assert
        Assert.Equal(string.Empty, text);
        Assert.False(isFinal);
        Assert.False(isTerminated);
    }

    [Fact]
    public void ParseMessage_UnknownType_ReturnsEmptyAndAllFalse()
    {
        // Arrange
        const string json = """{"type":"SomeNewUnknownMessageType","data":"irrelevant"}""";

        // Act
        var (text, isFinal, isTerminated) = AssemblyAITranscriptionService.ParseMessage(json);

        // Assert
        Assert.Equal(string.Empty, text);
        Assert.False(isFinal);
        Assert.False(isTerminated);
    }

    [Fact]
    public void ParseMessage_EmptyTextField_ReturnsEmptyText()
    {
        // Arrange — AssemblyAI sometimes sends partial transcripts with an empty text field
        const string json = """{"type":"PartialTranscript","text":""}""";

        // Act
        var (text, isFinal, isTerminated) = AssemblyAITranscriptionService.ParseMessage(json);

        // Assert
        Assert.Equal(string.Empty, text);
        Assert.False(isFinal);
        Assert.False(isTerminated);
    }
}
