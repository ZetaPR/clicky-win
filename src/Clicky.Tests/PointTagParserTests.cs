using Clicky.Core;
using Clicky.Services;
using Xunit;

namespace Clicky.Tests;

public class PointTagParserTests
{
    [Fact]
    public void Parse_PointNoneTag_ReturnsNoCoordinates()
    {
        // Arrange
        const string input = "Sure, I can help with that. [POINT:none]";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal("Sure, I can help with that.", result.SpokenText);
        Assert.False(result.HasPoint);
        Assert.Null(result.X);
        Assert.Null(result.Y);
        Assert.Null(result.Label);
        Assert.Null(result.ScreenNumber);
    }

    [Fact]
    public void Parse_PointTagWithCoordinatesAndLabel_ReturnsCorrectValues()
    {
        // Arrange
        const string input = "Click the build button. [POINT:150,45:build button]";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal("Click the build button.", result.SpokenText);
        Assert.True(result.HasPoint);
        Assert.Equal(150, result.X);
        Assert.Equal(45, result.Y);
        Assert.Equal("build button", result.Label);
        Assert.Null(result.ScreenNumber);
    }

    [Fact]
    public void Parse_PointTagWithScreenNumber_ReturnsScreenNumber()
    {
        // Arrange
        const string input = "Look here. [POINT:960,540:main area:screen2]";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal("Look here.", result.SpokenText);
        Assert.True(result.HasPoint);
        Assert.Equal(960, result.X);
        Assert.Equal(540, result.Y);
        Assert.Equal("main area", result.Label);
        Assert.Equal(2, result.ScreenNumber);
    }

    [Fact]
    public void Parse_NoPointTag_ReturnsFullResponseTrimmed()
    {
        // Arrange
        const string input = "  There is no pointer in this response.  ";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal("There is no pointer in this response.", result.SpokenText);
        Assert.False(result.HasPoint);
        Assert.Null(result.X);
        Assert.Null(result.Y);
    }

    [Fact]
    public void Parse_TagWithTrailingWhitespace_StillParsesCorrectly()
    {
        // Arrange
        const string input = "Click here. [POINT:100,200:button]   ";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal("Click here.", result.SpokenText);
        Assert.Equal(100, result.X);
        Assert.Equal(200, result.Y);
        Assert.Equal("button", result.Label);
    }

    [Fact]
    public void Parse_CoordinatesWithSpacesAroundComma_ParsesCorrectly()
    {
        // Arrange
        const string input = "See this. [POINT:150 , 45:label]";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal("See this.", result.SpokenText);
        Assert.Equal(150, result.X);
        Assert.Equal(45, result.Y);
        Assert.Equal("label", result.Label);
    }

    [Fact]
    public void Parse_EmptyLabel_ReturnsNullLabel()
    {
        // Arrange
        const string input = "Here. [POINT:150,45:]";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal("Here.", result.SpokenText);
        Assert.Equal(150, result.X);
        Assert.Equal(45, result.Y);
        Assert.Null(result.Label);
    }

    [Fact]
    public void Parse_LabelWithSpaces_PreservesFullLabel()
    {
        // Arrange
        const string input = "Press save. [POINT:960,540:save button]";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal("Press save.", result.SpokenText);
        Assert.Equal(960, result.X);
        Assert.Equal(540, result.Y);
        Assert.Equal("save button", result.Label);
    }

    [Fact]
    public void Parse_ResponseTextBeforeTag_IsTrimmedCorrectly()
    {
        // Arrange
        const string input = "   Lots of leading whitespace.   [POINT:10,20:target]";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal("Lots of leading whitespace.", result.SpokenText);
        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptySpokenText()
    {
        // Act
        var result = PointTagParser.Parse(string.Empty);

        // Assert
        Assert.Equal(string.Empty, result.SpokenText);
        Assert.False(result.HasPoint);
    }

    [Fact]
    public void Parse_PointTagOnly_ReturnsEmptySpokenText()
    {
        // Arrange
        const string input = "[POINT:none]";

        // Act
        var result = PointTagParser.Parse(input);

        // Assert
        Assert.Equal(string.Empty, result.SpokenText);
        Assert.False(result.HasPoint);
    }

    [Fact]
    public void Parse_NullInput_ReturnsEmptySpokenTextAndNoCoordinates()
    {
        // Act
        var result = PointTagParser.Parse(null);

        // Assert
        Assert.Equal(string.Empty, result.SpokenText);
        Assert.False(result.HasPoint);
        Assert.Null(result.X);
        Assert.Null(result.Y);
    }
}
