using System.IO;
using System.Text.Json;
using Clicky.Core.Settings;
using Serilog;

namespace Clicky.Services.Settings;

/// <summary>Persists <see cref="UserSettings"/> as JSON at %LOCALAPPDATA%\Clicky\user-settings.json.</summary>
public sealed class UserSettingsService : IUserSettingsService
{
    internal static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Clicky", "user-settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <inheritdoc/>
    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new UserSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load user settings; falling back to defaults");
            return new UserSettings();
        }
    }

    /// <inheritdoc/>
    public void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save user settings");
        }
    }
}
