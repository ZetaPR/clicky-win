namespace Clicky.Core;

/// <summary>Carries a SharpHook KeyCode name ("VcLeftControl", "VcSpace", etc.) from the global hook.</summary>
public sealed class HookKeyEventArgs(string keyCodeName) : EventArgs
{
    public string KeyCodeName { get; } = keyCodeName;
}
