namespace Clicky.Core.Settings;

/// <summary>A Cartesia voice available for TTS selection in Clicky.</summary>
public sealed record VoiceOption(string Name, string Id, string Gender, string Country)
{
    /// <summary>Human-readable label shown in the voice picker: "Name (Gender, Country)".</summary>
    public string Label => $"{Name} ({Gender}, {Country})";
}
