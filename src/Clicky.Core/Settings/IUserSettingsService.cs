namespace Clicky.Core.Settings;

/// <summary>Loads and persists <see cref="UserSettings"/> to/from disk.</summary>
public interface IUserSettingsService
{
    /// <summary>Loads settings from disk; returns defaults if the file is missing or corrupt.</summary>
    UserSettings Load();

    /// <summary>Persists the given settings to disk.</summary>
    void Save(UserSettings settings);
}
