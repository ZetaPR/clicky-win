namespace Clicky.Core.Settings;

/// <summary>User-configurable preferences persisted to JSON at %LOCALAPPDATA%\Clicky\user-settings.json.</summary>
public sealed class UserSettings
{
    /// <summary>SharpHook KeyCode enum value names forming the push-to-talk hotkey combination.</summary>
    public List<string> HotkeyKeys { get; set; } = ["VcLeftControl", "VcLeftMeta", "VcSpace"];

    /// <summary>Hex color for the triangle pointer (e.g. "#4A9EFF").</summary>
    public string ArrowColor { get; set; } = "#4A9EFF";

    /// <summary>Hex color for the spinner/loader arc.</summary>
    public string LoaderColor { get; set; } = "#4A9EFF";

    /// <summary>Hex color for the waveform recording bars.</summary>
    public string BarsColor { get; set; } = "#4A9EFF";

    /// <summary>Cartesia voice ID for TTS synthesis.</summary>
    public string VoiceId { get; set; } = "a0e99841-438c-4a64-b679-ae501e7d6091";

    /// <summary>Directory where Serilog rolling log files are written. Takes effect after restart.</summary>
    public string LogFolderPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Clicky", "logs");

    /// <summary>Minimum log level written to the log file. One of: "Information", "Warning", "Error". Takes effect after restart.</summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>Overlay display mode. "Persistent" = triangle always visible. "Hidden" = hidden at idle, appears and animates on PTT.</summary>
    public string CursorMode { get; set; } = "Persistent";

    /// <summary>Cloudflare Worker URL. Overrides BuildSecrets/WORKER_URL env var when non-empty. Takes effect after restart.</summary>
    public string WorkerUrl { get; set; } = string.Empty;

    /// <summary>AssemblyAI API key. Overrides BuildSecrets/ASSEMBLYAI_API_KEY env var when non-empty. Takes effect after restart.</summary>
    public string AssemblyAiApiKey { get; set; } = string.Empty;

    /// <summary>Cartesia API key. Overrides BuildSecrets/CARTESIA_API_KEY env var when non-empty. Takes effect after restart.</summary>
    public string CartesiaApiKey { get; set; } = string.Empty;

    /// <summary>True until the user dismisses the first-run welcome window. Controls whether setup guidance is shown on launch.</summary>
    public bool IsFirstRun { get; set; } = true;
}
