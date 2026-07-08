using System.Net.Http;
using System.Text;
using System.Text.Json;
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
        ModelTextBox.Text = settings.AiModel;
        ApiKeyPasswordBox.Password = settings.AiApiKey;
        ProviderComboBox.SelectionChanged += ProviderComboBox_SelectionChanged;

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
        settings.AiModel = ModelTextBox.Text.Trim();
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

    /// <summary>Selected provider tag from the combo box ("openai", "local", …).</summary>
    private string SelectedProviderTag =>
        (ProviderComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;

    /// <summary>Auto-fills Ollama defaults and shows local-model help when "Local" is chosen.</summary>
    private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = SelectedProviderTag;
        OllamaHintText.Visibility = tag == "local"  ? Visibility.Visible : Visibility.Collapsed;
        KimiHintText.Visibility   = tag == "kimi"   ? Visibility.Visible : Visibility.Collapsed;
        CustomHintText.Visibility = tag == "custom" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "local")
        {
            if (string.IsNullOrWhiteSpace(EndpointTextBox.Text))
                EndpointTextBox.Text = "http://localhost:11434/v1/chat/completions"; // Ollama default
            if (string.IsNullOrWhiteSpace(ModelTextBox.Text))
                ModelTextBox.Text = "llama3.1";
        }
        else if (tag == "kimi" && string.IsNullOrWhiteSpace(ModelTextBox.Text))
        {
            ModelTextBox.Text = "kimi-k2.6";
        }
    }

    /// <summary>
    /// Fires a one-token chat completion at the endpoint currently typed into the
    /// dialog (unsaved values) so users can verify key/endpoint/model — including a
    /// running Ollama server — before saving. Kept self-contained on purpose: it
    /// tests exactly what is on screen, not what was last saved.
    /// </summary>
    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var provider = SelectedProviderTag;
        if (string.IsNullOrWhiteSpace(provider))
        {
            StatusText.Text = "Choose a provider first.";
            return;
        }

        var model = ModelTextBox.Text.Trim();
        var apiKey = ApiKeyPasswordBox.Password;
        var isAnthropic = provider == "anthropic";

        var endpoint = EndpointTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = provider switch
            {
                "openai"    => "https://api.openai.com/v1/chat/completions",
                "anthropic" => "https://api.anthropic.com/v1/messages",
                "kimi"      => "https://api.k2.6.ai/v1/chat/completions",
                "local"     => "http://localhost:11434/v1/chat/completions",
                _           => string.Empty, // azure-openai / custom need an explicit endpoint
            };

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            StatusText.Text = "Enter the endpoint for this provider.";
            return;
        }

        TestButton.IsEnabled = false;
        StatusText.Text = "Testing…";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

            var body = isAnthropic
                ? JsonSerializer.Serialize(new
                  {
                      model = string.IsNullOrWhiteSpace(model) ? "claude-sonnet-4-6" : model,
                      max_tokens = 1,
                      messages = new[] { new { role = "user", content = "ping" } },
                  })
                : JsonSerializer.Serialize(new
                  {
                      model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model,
                      max_tokens = 1,
                      messages = new[] { new { role = "user", content = "ping" } },
                  });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            if (isAnthropic)
            {
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
            }
            else if (provider == "azure-openai")
            {
                request.Headers.Add("api-key", apiKey);
            }
            else if (!string.IsNullOrWhiteSpace(apiKey)) // openai / kimi / custom Bearer; local servers usually need none
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var response = await http.SendAsync(request);
            StatusText.Text = response.IsSuccessStatusCode
                ? $"Connected ✓ ({(int)response.StatusCode})"
                : $"Failed — HTTP {(int)response.StatusCode}. Check key, model, and endpoint.";
        }
        catch (TaskCanceledException)
        {
            StatusText.Text = provider == "local"
                ? "Timed out — is Ollama running? Try 'ollama serve'."
                : "Timed out — check the endpoint.";
        }
        catch (HttpRequestException ex)
        {
            StatusText.Text = provider == "local"
                ? "Can't reach the local server — start Ollama ('ollama serve') and retry."
                : $"Connection error: {ex.Message}";
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }
}
