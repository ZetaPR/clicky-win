using Clicky.Core;
using Clicky.Services;
using Xunit;

namespace Clicky.Tests;

public class StepVerifyServiceTests
{
    [Fact]
    public void ParseVerifyResponse_Advance_ReturnsAdvanceOutcome()
    {
        var json = """{"result":"advance","spokenText":"Good job!","nextX":300,"nextY":400,"nextLabel":"Save As"}""";
        var result = CloudflareWorkerVerifyService.ParseVerifyResponse(json);

        Assert.Equal(VerifyOutcome.Advance, result.Outcome);
        Assert.Equal("Good job!", result.SpokenText);
        Assert.Equal(300, result.NextX);
        Assert.Equal(400, result.NextY);
        Assert.Equal("Save As", result.NextLabel);
    }

    [Fact]
    public void ParseVerifyResponse_Complete_ReturnsCompleteOutcome()
    {
        var json = """{"result":"complete","spokenText":"All done!"}""";
        var result = CloudflareWorkerVerifyService.ParseVerifyResponse(json);

        Assert.Equal(VerifyOutcome.Complete, result.Outcome);
        Assert.Equal("All done!", result.SpokenText);
        Assert.Null(result.NextX);
    }

    [Fact]
    public void ParseVerifyResponse_Correct_ReturnsCorrectOutcome()
    {
        var json = """{"result":"correct","spokenText":"Not quite, try again."}""";
        var result = CloudflareWorkerVerifyService.ParseVerifyResponse(json);

        Assert.Equal(VerifyOutcome.Correct, result.Outcome);
    }

    [Fact]
    public void ParseVerifyResponse_UnknownResult_ThrowsInvalidOperationException()
    {
        var json = """{"result":"unknown","spokenText":"?"}""";
        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkerVerifyService.ParseVerifyResponse(json));
    }
}
