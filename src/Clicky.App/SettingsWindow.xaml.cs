using System.Windows;
using Clicky.Core;
using Clicky.Core.Settings;

namespace Clicky.App;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _settings;
    private readonly IUserSettingsService _svc;
    private readonly IOverlayService _overlay;
    private readonly CompanionSettings _companion;

    private record VoiceItem(string Label, string Id);

    public SettingsWindow(
        UserSettings settings,
        IUserSettingsService svc,
        IOverlayService overlay,
        CompanionSettings companion)
    {
        _settings = settings;
        _svc = svc;
        _overlay = overlay;
        _companion = companion;
        InitializeComponent();
        Populate();
    }

    private void Populate()
    {
        PersistentMode.IsChecked = _settings.CursorMode != "Hidden";
        HiddenMode.IsChecked     = _settings.CursorMode == "Hidden";

        ArrowColorBox.Text  = _settings.ArrowColor;
        LoaderColorBox.Text = _settings.LoaderColor;
        BarsColorBox.Text   = _settings.BarsColor;

        var voiceItems = VoiceCatalog.Voices
            .Select(v => new VoiceItem(v.Label, v.Id))
            .ToList();
        VoiceCombo.ItemsSource       = voiceItems;
        VoiceCombo.DisplayMemberPath = nameof(VoiceItem.Label);
        VoiceCombo.SelectedItem      = voiceItems.FirstOrDefault(v => v.Id == _settings.VoiceId)
                                       ?? voiceItems.FirstOrDefault();

        LogLevelCombo.ItemsSource   = new[] { "Verbose", "Debug", "Information", "Warning", "Error" };
        LogLevelCombo.SelectedItem  = _settings.LogLevel;
        LogFolderBox.Text           = _settings.LogFolderPath;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.CursorMode   = HiddenMode.IsChecked == true ? "Hidden" : "Persistent";
        _settings.ArrowColor   = ArrowColorBox.Text.Trim();
        _settings.LoaderColor  = LoaderColorBox.Text.Trim();
        _settings.BarsColor    = BarsColorBox.Text.Trim();
        _settings.LogLevel     = LogLevelCombo.SelectedItem as string ?? "Information";
        _settings.LogFolderPath = LogFolderBox.Text.Trim();

        if (VoiceCombo.SelectedItem is VoiceItem voice)
        {
            _settings.VoiceId         = voice.Id;
            _companion.CartesiaVoiceId = voice.Id;
        }

        _svc.Save(_settings);

        _overlay.ApplyColors(_settings.ArrowColor, _settings.LoaderColor, _settings.BarsColor);
        _overlay.ApplyCursorMode(_settings.CursorMode);

        StatusBorder.Visibility = Visibility.Visible;
        StatusText.Text = "Saved. Colors and cursor mode took effect. Restart for logging changes.";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void ApiConfig_Click(object sender, RoutedEventArgs e)
        => new ConfigWindow(_settings, _svc).Show();
}
