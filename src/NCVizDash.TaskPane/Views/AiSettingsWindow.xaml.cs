using System.Windows;
using System.Windows.Controls;
using NCVizDash.Core.Abstractions;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// AI configuration dialog — the piece Phase 18 never actually built. The
/// provider layer (<see cref="IAiProvider"/>, <see cref="Ai.AiFeatureGate"/>)
/// has been fully implemented and tested since Phase 18, but nothing in the
/// shipped UI ever let a user turn AI on or enter a key. This dialog is that
/// missing entry point, reading from and writing to the same
/// <see cref="IAppSettingsProvider"/> the feature gate checks at runtime.
/// </summary>
public sealed partial class AiSettingsWindow : Window
{
    private readonly IAppSettingsProvider _settingsProvider;

    /// <summary>Loads the dialog with the currently-saved AI settings.</summary>
    public AiSettingsWindow(IAppSettingsProvider settingsProvider)
    {
        InitializeComponent();
        _settingsProvider = settingsProvider;

        var settings = settingsProvider.Settings;
        EnabledCheckBox.IsChecked = settings.AiEnabled;
        EndpointTextBox.Text = settings.AiEndpoint;
        ApiKeyPasswordBox.Password = settings.AiApiKey;

        foreach (ComboBoxItem item in ProviderComboBox.Items)
        {
            if ((string)item.Tag == settings.AiProvider)
            {
                ProviderComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsProvider.Settings;

        settings.AiEnabled = EnabledCheckBox.IsChecked == true;
        settings.AiProvider = (ProviderComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
        settings.AiEndpoint = EndpointTextBox.Text.Trim();
        settings.AiApiKey = ApiKeyPasswordBox.Password;

        if (settings.AiEnabled && string.IsNullOrWhiteSpace(settings.AiProvider))
        {
            StatusText.Text = "Choose a provider before enabling AI.";
            return;
        }

        _settingsProvider.Save();
        StatusText.Text = "Saved.";
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
