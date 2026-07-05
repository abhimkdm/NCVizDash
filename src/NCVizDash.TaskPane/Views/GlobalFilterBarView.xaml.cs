using System.Windows.Controls;
using NCVizDash.TaskPane.ViewModels;

namespace NCVizDash.TaskPane.Views;

/// <summary>Code-behind for the dashboard-wide global filter bar.</summary>
public sealed partial class GlobalFilterBarView : System.Windows.Controls.UserControl
{
    /// <summary>Initialises the view.</summary>
    public GlobalFilterBarView() => InitializeComponent();

    private GlobalFilterBarViewModel? ViewModel => DataContext as GlobalFilterBarViewModel;

    /// <summary>
    /// When the user picks a field to filter on, immediately load its distinct
    /// value list (for non-Measure fields) so the value ComboBox populates.
    /// </summary>
    private async void FieldCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null) return;
        await ViewModel.LoadValueOptionsCommand.ExecuteAsync(null);
    }
}
