using Clicky.Core;
using Xunit;

namespace Clicky.Tests;

public class PushToTalkHookRawEventsTests
{
    [Fact]
    public void HookKeyEventArgs_StoresKeyCodeName()
    {
        // Arrange
        const string keyName = "VcLeftMeta";

        // Act
        var args = new HookKeyEventArgs(keyName);

        // Assert
        Assert.Equal(keyName, args.KeyCodeName);
    }

    [Fact]
    public void HookKeyEventArgs_PreservesArbitraryKeyName()
    {
        // Arrange — any string the hook produces must pass through unchanged
        const string keyName = "VcLeftControl";

        // Act
        var args = new HookKeyEventArgs(keyName);

        // Assert
        Assert.Equal("VcLeftControl", args.KeyCodeName);
    }

    [Fact]
    public void HookMouseEventArgs_StoresButtonCodeWithMousePrefix()
    {
        // Arrange
        const string buttonCode = "MouseButton3";

        // Act
        var args = new HookMouseEventArgs(buttonCode);

        // Assert
        Assert.Equal(buttonCode, args.ButtonCode);
    }

    [Fact]
    public void HookMouseEventArgs_Button1FormatMatchesExclusionList()
    {
        // Arrange — PushToTalkHook produces "Mouse" + e.Data.Button.ToString()
        // MouseButton.Button1.ToString() == "Button1", so the code is "MouseButton1"
        const string expectedCode = "MouseButton1";

        // Act
        var args = new HookMouseEventArgs(expectedCode);

        // Assert
        Assert.Equal(expectedCode, args.ButtonCode);
        Assert.StartsWith("Mouse", args.ButtonCode, StringComparison.Ordinal);
    }
}
