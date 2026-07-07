using System.Windows;
using System.Windows.Input;
using NCVizDash.Models;
using NCVizDash.TaskPane.Templates;
using NCVizDash.TaskPane.ViewModels;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// Modal template gallery — the real implementation behind the ribbon's
/// "Templates" button, which previously did nothing at all (logged and
/// returned). Lists every <see cref="TemplateRegistry"/> entry as a clickable
/// card; clicking one applies it against the selected data source via
/// <see cref="ShellViewModel.GenerateDashboardCommand"/>'s sibling,
/// <see cref="ShellViewModel.ApplyTemplateCommand"/>, and closes the dialog.
/// </summary>
public sealed partial class TemplatePickerWindow : Window
{
    private readonly ShellViewModel _shellViewModel;

    /// <summary>All 11 built-in templates, bound to the gallery.</summary>
    public IReadOnlyList<DashboardTemplate> Templates => TemplateRegistry.All;

    /// <summary>Every data source currently loaded in the Explorer panel.</summary>
    public IReadOnlyList<DataSourceDescriptor> DataSources => _shellViewModel.ExplorerPanel.DataSources;

    /// <summary>The data source the chosen template will be applied against.</summary>
    public DataSourceDescriptor? SelectedDataSource { get; set; }

    /// <summary>Initialises the picker, defaulting the data source to the first one loaded (if any).</summary>
    public TemplatePickerWindow(ShellViewModel shellViewModel)
    {
        InitializeComponent();
        _shellViewModel = shellViewModel;
        SelectedDataSource = DataSources.FirstOrDefault();
        DataContext = this;
    }

    private void TemplateCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DashboardTemplate template }) return;

        if (SelectedDataSource is null)
        {
            StatusText.Text = "Load a data source first (Refresh, then reopen Templates).";
            return;
        }

        _shellViewModel.ApplyTemplateCommand.Execute((template, SelectedDataSource));
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}
