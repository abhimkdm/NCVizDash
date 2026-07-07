using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.Services;
using NCVizDash.TaskPane.ViewModels;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="ShellViewModel"/>'s dashboard lifecycle commands (Phase 10).</summary>
public sealed class ShellViewModelDashboardTests
{
    private static (Mock<IDashboardRepository> repo, ShellViewModel sut) MakeSut()
    {
        var repo = new Mock<IDashboardRepository>();
        var settingsProvider = new Mock<IAppSettingsProvider>();
        settingsProvider.Setup(s => s.Settings).Returns(new AppSettings());

        var themeService = new ThemeService(NullLogger<ThemeService>.Instance);
        var explorer = new ExplorerPanelViewModel(
            NullLogger<ExplorerPanelViewModel>.Instance,
            new Mock<IExcelDataReader>().Object,
            TestFactories.MakeAnalyticsEngineMock().Object);

        var canvas = TestFactories.MakeCanvasPanelViewModel();
        var visualLibrary = new VisualLibraryViewModel(NullLogger<VisualLibraryViewModel>.Instance);

        var sut = new ShellViewModel(
            NullLogger<ShellViewModel>.Instance, settingsProvider.Object, themeService,
            repo.Object,
            new NCVizDash.TaskPane.Templates.TemplateInstantiationService(NullLogger<NCVizDash.TaskPane.Templates.TemplateInstantiationService>.Instance),
            new NCVizDash.TaskPane.Generation.OneClickDashboardGenerator(NullLogger<NCVizDash.TaskPane.Generation.OneClickDashboardGenerator>.Instance),
            explorer, canvas, visualLibrary, null);

        return (repo, sut);
    }

    [Fact]
    public void NewDashboard_OpensEmptyDashboardOnCanvas()
    {
        var (_, sut) = MakeSut();
        sut.NewDashboard();

        Assert.NotNull(sut.CanvasPanel.ActiveDashboard);
        Assert.Empty(sut.CanvasPanel.ActiveDashboard!.Widgets);
    }

    [Fact]
    public async Task SaveDashboardAsync_NoActiveDashboard_DoesNotCallRepository()
    {
        var (repo, sut) = MakeSut();
        await sut.SaveDashboardCommand.ExecuteAsync(null);

        repo.Verify(r => r.SaveAsync(It.IsAny<Dashboard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveDashboardAsync_ActiveDashboard_CallsRepositorySaveAsync()
    {
        var (repo, sut) = MakeSut();
        sut.NewDashboard();

        await sut.SaveDashboardCommand.ExecuteAsync(null);

        repo.Verify(r => r.SaveAsync(sut.CanvasPanel.ActiveDashboard!, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadSavedDashboardsAsync_PopulatesSavedDashboards_SortedByModifiedDescending()
    {
        var (repo, sut) = MakeSut();
        var older = new Dashboard { Name = "Older" };
        var newer = new Dashboard { Name = "Newer", ModifiedAt = DateTimeOffset.UtcNow.AddMinutes(5) };

        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dashboard> { older, newer });

        await sut.LoadSavedDashboardsCommand.ExecuteAsync(null);

        Assert.Equal(2, sut.SavedDashboards.Count);
        Assert.Equal("Newer", sut.SavedDashboards[0].Name);
    }

    [Fact]
    public void OpenDashboard_LoadsGivenDashboardOntoCanvas()
    {
        var (_, sut) = MakeSut();
        var dashboard = new Dashboard { Name = "Existing" };

        sut.OpenDashboard(dashboard);

        Assert.Equal(dashboard, sut.CanvasPanel.ActiveDashboard);
    }

    [Fact]
    public async Task DeleteDashboardAsync_CallsRepositoryAndRemovesFromList()
    {
        var (repo, sut) = MakeSut();
        var dashboard = new Dashboard { Name = "ToDelete" };
        sut.SavedDashboards.Add(dashboard);

        await sut.DeleteDashboardCommand.ExecuteAsync(dashboard);

        repo.Verify(r => r.DeleteAsync(dashboard.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.DoesNotContain(dashboard, sut.SavedDashboards);
    }
}
