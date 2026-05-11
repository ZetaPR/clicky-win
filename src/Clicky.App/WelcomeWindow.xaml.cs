using System.Diagnostics;
using System.Windows;
using Clicky.Core.Settings;

namespace Clicky.App;

public partial class WelcomeWindow : Window
{
    private readonly UserSettings _settings;
    private readonly IUserSettingsService _settingsService;

    public WelcomeWindow(UserSettings settings, IUserSettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
        InitializeComponent();
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        MarkSeen();
        Close();
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        MarkSeen();
        Close();
        new ConfigWindow(_settings, _settingsService).Show();
    }

    private void Link_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string url)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void MarkSeen()
    {
        _settings.IsFirstRun = false;
        _settingsService.Save(_settings);
    }
}
