using System.Windows.Controls;
using NCVizDash.TaskPane.ViewModels;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// AI Report Generator pane — hosted in its own <c>CustomTaskPane</c> via
/// <c>ElementHost</c>, fully independent of the dashboard <see cref="ShellView"/>.
/// Mirrors ShellView's pattern: UserControl root + late ViewModel binding so
/// DataTemplates expand only after ElementHost has parented the visual tree.
/// </summary>
public partial class AiReportPaneView : UserControl
{
    /// <summary>Initializes the view and loads its XAML-defined visual tree.</summary>
    public AiReportPaneView()
    {
        InitializeComponent();
    }

    /// <summary>Binds the ViewModel after the control is hosted (see ShellView).</summary>
    public void BindViewModel(AiReportPaneViewModel viewModel) =>
        DataContext = viewModel;
}
