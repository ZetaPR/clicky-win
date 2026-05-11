using Clicky.Core;
using Clicky.Services;
using Xunit;

namespace Clicky.Tests;

public class StepPlanParserTests
{
    [Fact]
    public void Feed_StepWithCoords_ParsesAllFields()
    {
        var parser = new StepPlanParser();
        var steps = parser.Feed("[STEP:1:450,200:File menu]click File in the top menu bar[/STEP]").ToList();

        Assert.Single(steps);
        Assert.Equal(1, steps[0].Number);
        Assert.Equal("click File in the top menu bar", steps[0].Text);
        Assert.Equal(450, steps[0].X);
        Assert.Equal(200, steps[0].Y);
        Assert.Equal("File menu", steps[0].Label);
    }

    [Fact]
    public void Feed_StepWithoutCoords_ParsesTextOnly()
    {
        var parser = new StepPlanParser();
        var steps = parser.Feed("[STEP:2]then click Save As[/STEP]").ToList();

        Assert.Single(steps);
        Assert.Equal(2, steps[0].Number);
        Assert.Equal("then click Save As", steps[0].Text);
        Assert.Null(steps[0].X);
        Assert.Null(steps[0].Y);
        Assert.Null(steps[0].Label);
    }

    [Fact]
    public void Feed_TagSplitAcrossDeltas_ParsesStep()
    {
        var parser = new StepPlanParser();
        var s1 = parser.Feed("[STEP:1:100,200:label]some ").ToList();
        var s2 = parser.Feed("text[/STEP]").ToList();

        Assert.Empty(s1);
        Assert.Single(s2);
        Assert.Equal("some text", s2[0].Text);
    }

    [Fact]
    public void Feed_MultipleStepsInOneChunk_ParsesAll()
    {
        var parser = new StepPlanParser();
        var text = "[STEP:1:100,200:A]step one[/STEP][STEP:2]step two[/STEP]";
        var steps = parser.Feed(text).ToList();

        Assert.Equal(2, steps.Count);
        Assert.Equal("step one", steps[0].Text);
        Assert.Equal("step two", steps[1].Text);
    }

    [Fact]
    public void Feed_IgnoresTextOutsideTags()
    {
        var parser = new StepPlanParser();
        var text = "preamble text [STEP:1:10,20:X]do this[/STEP] trailing";
        var steps = parser.Feed(text).ToList();

        Assert.Single(steps);
        Assert.Equal("do this", steps[0].Text);
    }

    [Fact]
    public void Feed_StepWithCoordsButNoLabel_ParsesCoordsWithNullLabel()
    {
        var parser = new StepPlanParser();
        var steps = parser.Feed("[STEP:1:300,400]no label step[/STEP]").ToList();

        Assert.Single(steps);
        Assert.Equal(300, steps[0].X);
        Assert.Equal(400, steps[0].Y);
        Assert.Null(steps[0].Label);
    }

    [Fact]
    public void Feed_EmptyDelta_ReturnsEmpty()
    {
        var parser = new StepPlanParser();
        Assert.Empty(parser.Feed(""));
    }
}
