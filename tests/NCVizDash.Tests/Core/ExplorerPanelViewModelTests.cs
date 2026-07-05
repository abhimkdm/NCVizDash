using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.ViewModels;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="ExplorerPanelViewModel"/>.</summary>
public sealed class ExplorerPanelViewModelTests
{
    private static DataSourceDescriptor MakeSource(string name, params string[] fieldNames)
    {
        var ds = new DataSourceDescriptor { Name = name, SourceType = "ExcelTable", SheetName = "Sheet1" };
        foreach (var f in fieldNames)
            ds.Fields.Add(new FieldDescriptor { Name = f, DisplayName = f, FieldType = FieldType.Dimension });
        return ds;
    }

    private static (Mock<IExcelDataReader> reader, Mock<IAnalyticsEngine> engine) MakeMocks() =>
        (new Mock<IExcelDataReader>(), new Mock<IAnalyticsEngine>());

    [Fact]
    public async Task LoadDataSourcesAsync_PopulatesDataSourcesAndFilteredDataSources()
    {
        var (reader, engine) = MakeMocks();
        var source = MakeSource("Sales");

        reader.Setup(r => r.GetDataSourcesAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { source });
        reader.Setup(r => r.ReadRowsAsync(source.Id, It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<IReadOnlyDictionary<string, object?>>());

        var sut = new ExplorerPanelViewModel(NullLogger<ExplorerPanelViewModel>.Instance, reader.Object, engine.Object);

        await sut.LoadDataSourcesAsync();

        Assert.Single(sut.DataSources);
        Assert.Single(sut.FilteredDataSources);
        Assert.Equal("Sales", sut.DataSources[0].Name);

        engine.Verify(e => e.LoadDataSourceAsync(source, It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadDataSourcesAsync_OneSourceFailsToIngest_OthersStillLoad()
    {
        var (reader, engine) = MakeMocks();
        var good = MakeSource("Good");
        var bad = MakeSource("Bad");

        reader.Setup(r => r.GetDataSourcesAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new[] { good, bad });
        reader.Setup(r => r.ReadRowsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<IReadOnlyDictionary<string, object?>>());

        engine.Setup(e => e.LoadDataSourceAsync(good, It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(), It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);
        engine.Setup(e => e.LoadDataSourceAsync(bad, It.IsAny<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("simulated DuckDB failure"));

        var sut = new ExplorerPanelViewModel(NullLogger<ExplorerPanelViewModel>.Instance, reader.Object, engine.Object);

        await sut.LoadDataSourcesAsync();

        Assert.Single(sut.DataSources);
        Assert.Equal("Good", sut.DataSources[0].Name);
    }

    [Fact]
    public async Task SearchText_FiltersBySourceName()
    {
        var (reader, engine) = MakeMocks();
        var sales = MakeSource("Sales");
        var hr = MakeSource("HR Roster");

        reader.Setup(r => r.GetDataSourcesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { sales, hr });
        reader.Setup(r => r.ReadRowsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<IReadOnlyDictionary<string, object?>>());

        var sut = new ExplorerPanelViewModel(NullLogger<ExplorerPanelViewModel>.Instance, reader.Object, engine.Object);
        await sut.LoadDataSourcesAsync();

        sut.SearchText = "sales";

        Assert.Single(sut.FilteredDataSources);
        Assert.Equal("Sales", sut.FilteredDataSources[0].Name);
    }

    [Fact]
    public async Task SearchText_FiltersByFieldName()
    {
        var (reader, engine) = MakeMocks();
        var sales = MakeSource("Orders", "Revenue", "Department");

        reader.Setup(r => r.GetDataSourcesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { sales });
        reader.Setup(r => r.ReadRowsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<IReadOnlyDictionary<string, object?>>());

        var sut = new ExplorerPanelViewModel(NullLogger<ExplorerPanelViewModel>.Instance, reader.Object, engine.Object);
        await sut.LoadDataSourcesAsync();

        sut.SearchText = "Revenue";

        Assert.Single(sut.FilteredDataSources);
        Assert.Equal("Orders", sut.FilteredDataSources[0].Name);
    }

    [Fact]
    public async Task SearchText_NoMatches_ReturnsEmptyFilteredList()
    {
        var (reader, engine) = MakeMocks();
        var sales = MakeSource("Orders");

        reader.Setup(r => r.GetDataSourcesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { sales });
        reader.Setup(r => r.ReadRowsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(Array.Empty<IReadOnlyDictionary<string, object?>>());

        var sut = new ExplorerPanelViewModel(NullLogger<ExplorerPanelViewModel>.Instance, reader.Object, engine.Object);
        await sut.LoadDataSourcesAsync();

        sut.SearchText = "nonexistent-term";

        Assert.Empty(sut.FilteredDataSources);
    }

    [Fact]
    public async Task LoadPreviewAsync_PopulatesPreviewRows()
    {
        var (reader, engine) = MakeMocks();
        var source = MakeSource("Orders");
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["A"] = 1 },
            new Dictionary<string, object?> { ["A"] = 2 }
        };

        reader.Setup(r => r.ReadRowsAsync(source.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rows);

        var sut = new ExplorerPanelViewModel(NullLogger<ExplorerPanelViewModel>.Instance, reader.Object, engine.Object);

        await sut.LoadPreviewAsync(source);

        Assert.Equal(2, sut.PreviewRows.Count);
        Assert.Equal(source.Id, sut.PreviewSource?.Id);
    }

    [Fact]
    public void ClearPreview_ResetsPreviewState()
    {
        var (reader, engine) = MakeMocks();
        var sut = new ExplorerPanelViewModel(NullLogger<ExplorerPanelViewModel>.Instance, reader.Object, engine.Object);

        sut.PreviewRows.Add(new Dictionary<string, object?>());
        sut.ClearPreview();

        Assert.Empty(sut.PreviewRows);
        Assert.Null(sut.PreviewSource);
    }
}
