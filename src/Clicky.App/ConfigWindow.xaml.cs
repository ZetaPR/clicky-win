using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Clicky.Core.Settings;

namespace Clicky.App;

public partial class ConfigWindow : Window
{
    private readonly UserSettings _settings;
    private readonly IUserSettingsService _settingsService;

    public ConfigWindow(UserSettings settings, IUserSettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
        InitializeComponent();
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        WorkerUrlBox.Text     = _settings.WorkerUrl;
        AssemblyAiKeyBox.Text = _settings.AssemblyAiApiKey;
        CartesiaKeyBox.Text   = _settings.CartesiaApiKey;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.WorkerUrl       = WorkerUrlBox.Text.Trim();
        _settings.AssemblyAiApiKey = AssemblyAiKeyBox.Text.Trim();
        _settings.CartesiaApiKey   = CartesiaKeyBox.Text.Trim();
        _settingsService.Save(_settings);

        StatusBorder.Visibility = Visibility.Visible;
        StatusText.Text = "Saved. Restart Clicky for the new keys to take effect.";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => Close();

    private void Link_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string url)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
