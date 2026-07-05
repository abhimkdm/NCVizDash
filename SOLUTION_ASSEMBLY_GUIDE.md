# NC VizDash — Solution Assembly Guide

**Purpose of this document:** a complete, file-by-file map so every artifact generated
across Phases 1–9 can be placed into a Visual Studio 2022 / Cursor solution with zero
ambiguity, and so the solution builds and opens correctly on the first attempt.

**Status honesty note:** this guide describes **exactly what has been built** (Phases
1–9: Foundation → Global Filters). It does **not** invent files for unbuilt phases.
Where a folder or project is referenced by the original 18-phase plan but doesn't
exist yet (e.g. `NCVizDash.Connectors`, `NCVizDash.Setup`), it is explicitly marked
**`[NOT YET BUILT — Phase N]`** with the phase that will introduce it, rather than
presented as if it already exists. Do not create empty placeholder folders for these —
add them when their phase is actually implemented, so the tree you assemble always
matches what's real.

**Current phase status:** Phases 1–9 complete. Phase 10 (Dashboard Storage) is next.

---

## Table of Contents

1. Complete Solution Structure
2. Project References
3. File Location Reference (every file)
4. MVVM Structure
5. Connector Structure (not yet built)
6. Dashboard Engine Structure
7. DuckDB Structure
8. Chart Engine Structure
9. Resource Structure
10. Configuration Files
11. Documentation
12. Build Order
13. Visual Studio Solution Structure
14. Final Validation Checklist

---

## 1. Complete Solution Structure

```
NCVizDash/
│
├── NCVizDash.sln
├── README.md
├── TASKS.md
├── CHANGELOG.md
│
├── src/
│   │
│   ├── NCVizDash.Models/                          ← Domain models. No dependencies.
│   │   ├── NCVizDash.Models.csproj
│   │   ├── AppSettings.cs
│   │   ├── Dashboard.cs                           (Dashboard, DashboardWidget, WidgetLayout,
│   │   │                                            WidgetFilter, FilterOperator, VisualType)
│   │   └── DataSourceDescriptor.cs                (DataSourceDescriptor, FieldDescriptor, FieldType)
│   │
│   ├── NCVizDash.Core/                            ← Abstractions + engine-agnostic logic.
│   │   ├── NCVizDash.Core.csproj
│   │   ├── Abstractions/
│   │   │   └── IServices.cs                       (every service interface — see §3)
│   │   ├── Analytics/
│   │   │   ├── QuerySpec.cs                       (QuerySpec, MeasureSpec, WindowFunctionSpec,
│   │   │   │                                        PivotSpec, AggregateFunction, WindowFunctionType)
│   │   │   └── SqlFilterTranslator.cs
│   │   ├── Classification/
│   │   │   └── FieldTypeClassifier.cs
│   │   └── DependencyInjection/
│   │       └── CoreServiceCollectionExtensions.cs
│   │
│   ├── NCVizDash.Infrastructure/                  ← Logging + settings.
│   │   ├── NCVizDash.Infrastructure.csproj
│   │   ├── Configuration/
│   │   │   └── JsonAppSettingsProvider.cs
│   │   └── Logging/
│   │       ├── SerilogBootstrapper.cs
│   │       └── InfrastructureServiceCollectionExtensions.cs
│   │
│   ├── NCVizDash.RuleEngine/                      ← Deterministic visualization rules.
│   │   ├── NCVizDash.RuleEngine.csproj
│   │   ├── FieldComposition.cs
│   │   ├── VisualizationRule.cs
│   │   ├── RuleRegistry.cs
│   │   └── DeterministicRuleEngine.cs
│   │
│   ├── NCVizDash.ChartEngine/                     ← ECharts option builders + HTML builders.
│   │   ├── NCVizDash.ChartEngine.csproj
│   │   ├── AnimationPresets.cs
│   │   ├── ChartPalette.cs
│   │   ├── EChartsChartEngine.cs
│   │   └── Builders/
│   │       ├── ChartOptionContext.cs
│   │       ├── CartesianBuilder.cs                (Bar, Line, Area)
│   │       ├── PolarBuilder.cs                    (Pie, Donut, Gauge, Radar)
│   │       ├── XyBuilder.cs                       (Scatter, Bubble, Heatmap, Treemap)
│   │       └── HtmlBuilder.cs                     (KPI, Table)
│   │
│   ├── NCVizDash.DuckDB/                          ← Analytics engine (in-memory DuckDB).
│   │   ├── NCVizDash.DuckDB.csproj
│   │   ├── DuckDbAnalyticsEngine.cs
│   │   └── AnalyticsQueryBuilder.cs
│   │
│   ├── NCVizDash.Connectors/                      [NOT YET BUILT — Phase 14]
│   │   ├── Jira/                                  [NOT YET BUILT — Phase 14]
│   │   ├── AzureDevOps/                           [NOT YET BUILT — Phase 14]
│   │   ├── SQL/                                   [NOT YET BUILT — Phase 14]
│   │   ├── REST/                                  [NOT YET BUILT — Phase 14]
│   │   ├── SharePoint/                            [NOT YET BUILT — Phase 14]
│   │   ├── Csv/                                   [NOT YET BUILT — Phase 14]
│   │   └── Json/                                  [NOT YET BUILT — Phase 14]
│   │
│   ├── NCVizDash.Persistence/                     ← Dashboard storage (STUB — Phase 10 fills this in).
│   │   ├── NCVizDash.Persistence.csproj
│   │   └── WorkbookDashboardRepository.cs         (interface implemented, methods are stubs)
│   │
│   ├── NCVizDash.Ribbon/                          ← Excel ribbon (IRibbonExtensibility).
│   │   ├── NCVizDash.Ribbon.csproj
│   │   ├── NCVizDashRibbon.xml                    (Fluent UI ribbon XML, embedded resource)
│   │   └── NCVizDashRibbon.cs
│   │
│   ├── NCVizDash.TaskPane/                        ← WPF UI: shell, panels, canvas, chart hosting.
│   │   ├── NCVizDash.TaskPane.csproj
│   │   ├── Assets/
│   │   │   ├── chart-host.html                    (WebView2 harness, committed)
│   │   │   └── echarts.min.js                     [NOT COMMITTED — see §9 for how to obtain]
│   │   ├── Controls/
│   │   │   ├── DashboardCanvas.cs                 (custom Panel: move/resize/select/guides)
│   │   │   ├── WidgetCard.cs                      (per-widget chrome + hosts ChartHost)
│   │   │   └── ChartHost.cs                       (WebView2 wrapper)
│   │   ├── Converters/
│   │   │   └── ValueConverters.cs                 (6 converters — see §4)
│   │   ├── Geometry/
│   │   │   └── GridGeometryHelper.cs              (pure snap/clamp/guide math)
│   │   ├── Services/
│   │   │   ├── ThemeService.cs
│   │   │   ├── WidgetRenderCoordinator.cs
│   │   │   ├── CrossFilterManager.cs              (IFilterManager impl — Phase 8)
│   │   │   ├── GlobalFilterManager.cs             (IGlobalFilterManager impl — Phase 9)
│   │   │   └── DistinctValueService.cs            (Phase 9)
│   │   ├── ViewModels/
│   │   │   ├── ShellViewModel.cs
│   │   │   ├── PanelViewModels.cs                 (ExplorerPanelViewModel, CanvasPanelViewModel,
│   │   │   │                                        VisualLibraryViewModel, VisualTypeEntry)
│   │   │   └── GlobalFilterBarViewModel.cs        (+ GlobalFilterFieldOption)
│   │   └── Views/
│   │       ├── ShellWindow.xaml / .xaml.cs
│   │       ├── ExplorerPanelView.xaml / .xaml.cs
│   │       ├── CanvasPanelView.xaml / .xaml.cs
│   │       ├── VisualLibraryView.xaml / .xaml.cs
│   │       └── GlobalFilterBarView.xaml / .xaml.cs
│   │
│   ├── NCVizDash.ExcelAddIn/                      ← VSTO host + DI composition root.
│   │   ├── NCVizDash.ExcelAddIn.csproj
│   │   ├── ThisAddIn.cs
│   │   └── DataAccess/
│   │       └── ExcelDataReader.cs                 (IExcelDataReader impl)
│   │
│   └── NCVizDash.Setup/                           [NOT YET BUILT — installer/ClickOnce, no phase yet assigned]
│
├── tests/
│   └── NCVizDash.Tests/                           ← Single consolidated test project (see note below).
│       ├── NCVizDash.Tests.csproj
│       ├── Core/                                  (31 test files — ViewModel, engine, and pure-logic tests)
│       └── Infrastructure/
│           ├── AppSettingsTests.cs
│           └── DuckDbAnalyticsEngineTests.cs      (integration tests — real in-memory DuckDB)
│
├── docs/                                          [NOT YET BUILT — no docs/ folder exists yet;
│                                                     ARCHITECTURE.md, PRD.md, ROADMAP.md are not
│                                                     written. Only README.md/TASKS.md/CHANGELOG.md
│                                                     exist so far, at solution root — see §11]
│
├── assets/                                        [NOT YET BUILT — top-level design assets folder;
│                                                     the only asset committed today is
│                                                     src/NCVizDash.TaskPane/Assets/chart-host.html]
│
└── scripts/                                       [NOT YET BUILT — no build/CI scripts yet]
```

> **Note on the test project structure:** the original plan's `tests/` layout
> (`NCVizDash.Tests`, `RuleEngine.Tests`, `Connector.Tests` as separate projects) was
> **not** what was actually built. All 24 test files across every phase live in a
> single `NCVizDash.Tests` project, organised into `Core/` (ViewModel, engine, and
> pure-logic tests spanning every project) and `Infrastructure/` (settings +
> integration tests). This was a deliberate simplification — one test project avoids
> the overhead of 15+ tiny test assemblies for a solution this size. If per-project
> test assemblies are wanted later, split `NCVizDash.Tests/Core/*.cs` by the
> production project each file exercises.

---

## 2. Project References

Eleven projects total (ten `src/` + one `tests/`). Every `.csproj` targets
`net8.0-windows` — VSTO and WPF both require Windows, so nothing here is cross-platform.

### 2.1 NCVizDash.Models
- **Purpose:** Pure domain models. The dependency root every other project relies on.
- **Output type:** Library — **Framework:** net8.0-windows
- **Project references:** none
- **NuGet packages:** System.Text.Json 8.0.5
- **Build order:** 1st (no dependencies)

### 2.2 NCVizDash.Core
- **Purpose:** Service interfaces (`IServices.cs`) and engine-agnostic logic: `QuerySpec`, `SqlFilterTranslator`, `FieldTypeClassifier`. Nothing here knows about Excel, DuckDB, or WPF.
- **Output type:** Library — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Models
- **NuGet packages:** Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2, Microsoft.Extensions.Configuration.Abstractions 8.0.0
- **Build order:** 2nd

### 2.3 NCVizDash.Infrastructure
- **Purpose:** Serilog bootstrap, JSON-file settings provider, DI registration extensions.
- **Output type:** Library — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Core, NCVizDash.Models
- **NuGet packages:** Serilog 4.1.0, Serilog.Extensions.Logging 8.0.0, Serilog.Sinks.File 6.0.0, Serilog.Sinks.Debug 3.0.0, Serilog.Enrichers.Thread 4.0.0, Serilog.Enrichers.Environment 3.0.0, Microsoft.Extensions.DependencyInjection 8.0.1, Microsoft.Extensions.Logging 8.0.1, Microsoft.Extensions.Configuration 8.0.0, Microsoft.Extensions.Configuration.Json 8.0.0, Microsoft.Extensions.Configuration.Binder 8.0.2, System.Text.Json 8.0.5
- **Build order:** 3rd

### 2.4 NCVizDash.RuleEngine
- **Purpose:** Deterministic (no-AI) visualization type recommendation. Implements `IVisualizationRuleEngine`.
- **Output type:** Library — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Core, NCVizDash.Models
- **NuGet packages:** none beyond transitive
- **Build order:** 3rd (parallel with Infrastructure)

### 2.5 NCVizDash.ChartEngine
- **Purpose:** Builds ECharts `option` JSON and KPI/Table HTML fragments. Implements `IChartEngine`.
- **Output type:** Library — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Core, NCVizDash.Models
- **NuGet packages:** System.Text.Json 8.0.5, Microsoft.Extensions.Logging.Abstractions 8.0.2
- **Build order:** 3rd (parallel with Infrastructure/RuleEngine)

### 2.6 NCVizDash.DuckDB
- **Purpose:** In-memory analytics engine. Implements `IAnalyticsEngine`. Owns `AnalyticsQueryBuilder` (DuckDB-flavoured SQL generation from `QuerySpec`).
- **Output type:** Library — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Core, NCVizDash.Models
- **NuGet packages:** DuckDB.NET.Data 1.1.0, DuckDB.NET.Bindings.Full 1.1.0, Microsoft.Extensions.Logging.Abstractions 8.0.2
- **Build order:** 4th

### 2.7 NCVizDash.Persistence
- **Purpose:** Implements `IDashboardRepository` — will read/write dashboards to Excel Custom XML Parts. **Currently a stub** (methods return empty/no-op); Phase 10 fills this in.
- **Output type:** Library — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Core, NCVizDash.Models
- **NuGet packages:** System.Text.Json 8.0.5, Microsoft.Office.Interop.Excel 15.0.4795.1000, Microsoft.Extensions.Logging.Abstractions 8.0.2
- **Build order:** 4th (parallel with DuckDB)

### 2.8 NCVizDash.Ribbon
- **Purpose:** Excel Fluent UI ribbon (`IRibbonExtensibility`). Owns the ribbon XML as an embedded resource.
- **Output type:** Library — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Core, NCVizDash.Models
- **NuGet packages:** Microsoft.Office.Interop.Excel 15.0.4795.1000, Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2
- **Build order:** 3rd (parallel with Infrastructure/RuleEngine/ChartEngine)
- **Special build setting:** `NCVizDashRibbon.xml` must be `<EmbeddedResource>`, not `<Content>` — `GetCustomUI` reads it via `Assembly.GetManifestResourceStream("NCVizDash.Ribbon.NCVizDashRibbon.xml")`.

### 2.9 NCVizDash.TaskPane
- **Purpose:** The entire WPF UI: three-panel shell, dashboard canvas, chart hosting (WebView2), all ViewModels, all cross-cutting UI services (theming, filtering, rendering coordination). The largest project.
- **Output type:** Library — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Core, NCVizDash.Models, NCVizDash.ChartEngine
- **NuGet packages:** MaterialDesignThemes 5.1.0, MaterialDesignColors 3.1.0, Microsoft.Web.WebView2 1.0.2739.15, Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2, CommunityToolkit.Mvvm 8.3.2
- **Build order:** 5th (depends on ChartEngine)
- **Special build setting:** `<UseWPF>true</UseWPF>` and `<UseWindowsForms>true</UseWindowsForms>` both required (WinForms needed for `ElementHost`/`CustomTaskPane` interop in the add-in host). `Assets\chart-host.html` and `Assets\echarts.min.js` must be `<Content>` with `CopyToOutputDirectory=PreserveNewest` and `<Link>Assets\ChartHost\...</Link>`.
- **Deliberately NOT referenced:** NCVizDash.DuckDB — kept out on purpose (see §6.3) so query construction stays engine-agnostic.

### 2.10 NCVizDash.ExcelAddIn
- **Purpose:** VSTO host. `ThisAddIn.cs` is the DI composition root — every interface implementation from every other project is registered here. Also implements `IExcelDataReader` (`DataAccess/ExcelDataReader.cs`), the one interface that genuinely needs `Microsoft.Office.Interop.Excel`.
- **Output type:** Library (VSTO add-ins are class libraries loaded by the VSTO runtime, not EXEs) — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Core, NCVizDash.Infrastructure, NCVizDash.Models, NCVizDash.Ribbon, NCVizDash.TaskPane, NCVizDash.DuckDB, NCVizDash.Persistence, NCVizDash.RuleEngine, NCVizDash.ChartEngine
- **NuGet packages:** Microsoft.Office.Interop.Excel 15.0.4795.1000, Microsoft.Vbe.Interop 15.0.4795.1000, Microsoft.Extensions.DependencyInjection 8.0.1, Microsoft.Extensions.Logging 8.0.1, Serilog 4.1.0 + the same 5 Serilog sink/enricher packages as Infrastructure
- **Build order:** 6th / last (references everything)
- **Startup project:** **Yes — set this as Startup Project in Visual Studio.**

### 2.11 NCVizDash.Tests
- **Purpose:** All unit + integration tests, every phase, one project.
- **Output type:** Library (test) — **Framework:** net8.0-windows
- **Project references:** NCVizDash.Core, NCVizDash.Models, NCVizDash.Infrastructure, NCVizDash.RuleEngine, NCVizDash.ChartEngine, NCVizDash.DuckDB, NCVizDash.TaskPane
- **NuGet packages:** Microsoft.NET.Test.Sdk 17.11.1, xunit 2.9.2, xunit.runner.visualstudio 2.8.2, coverlet.collector 6.0.2, Moq 4.20.72, Microsoft.Extensions.Logging.Abstractions 8.0.2, CommunityToolkit.Mvvm 8.3.2
- **Build order:** last (after everything it tests)

### Build order summary (topological)

```
1. NCVizDash.Models
2. NCVizDash.Core
3. NCVizDash.Infrastructure, NCVizDash.RuleEngine, NCVizDash.ChartEngine, NCVizDash.Ribbon   (parallel)
4. NCVizDash.DuckDB, NCVizDash.Persistence                                                    (parallel)
5. NCVizDash.TaskPane
6. NCVizDash.ExcelAddIn   ← Startup Project
7. NCVizDash.Tests
```

MSBuild resolves this automatically from project references — you do not need to
manually order anything in Visual Studio. This table is for verifying the reference
graph is correct, not for manual sequencing.

---

## 3. File Location Reference

Every generated file, grouped by project. **Folder Path** is relative to the project
root (i.e. relative to the `.csproj` file's own directory). Namespace is verified
against the actual `namespace` declaration in each file — not inferred.

### 3.1 NCVizDash.Models

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.Models.csproj` | *(root)* | — | Project file |
| `AppSettings.cs` | *(root)* | `NCVizDash.Models` | App-wide settings POCO (theme, log level, grid snap, etc.) |
| `Dashboard.cs` | *(root)* | `NCVizDash.Models` | `Dashboard`, `DashboardWidget`, `WidgetLayout`, `WidgetFilter`, `FilterOperator`, `VisualType` |
| `DataSourceDescriptor.cs` | *(root)* | `NCVizDash.Models` | `DataSourceDescriptor`, `FieldDescriptor`, `FieldType` |

### 3.2 NCVizDash.Core

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.Core.csproj` | *(root)* | — | Project file |
| `IServices.cs` | `Abstractions/` | `NCVizDash.Core.Abstractions` | Every service interface: `IExcelDataReader`, `IAnalyticsEngine`, `IDashboardRepository`, `IVisualizationRuleEngine`, `IChartEngine`, `IFilterManager`, `IGlobalFilterManager`, `IAppSettingsProvider` |
| `QuerySpec.cs` | `Analytics/` | `NCVizDash.Core.Analytics` | `QuerySpec`, `MeasureSpec`, `WindowFunctionSpec`, `PivotSpec`, `AggregateFunction`, `WindowFunctionType` |
| `SqlFilterTranslator.cs` | `Analytics/` | `NCVizDash.Core.Analytics` | Shared `WidgetFilter` → SQL-clause logic |
| `FieldTypeClassifier.cs` | `Classification/` | `NCVizDash.Core.Classification` | Deterministic column → `FieldType` classification |
| `CoreServiceCollectionExtensions.cs` | `DependencyInjection/` | `NCVizDash.Core.DependencyInjection` | `AddNCVizDashCore()` DI extension |

### 3.3 NCVizDash.Infrastructure

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.Infrastructure.csproj` | *(root)* | — | Project file |
| `JsonAppSettingsProvider.cs` | `Configuration/` | `NCVizDash.Infrastructure.Configuration` | `IAppSettingsProvider` impl |
| `SerilogBootstrapper.cs` | `Logging/` | `NCVizDash.Infrastructure.Logging` | Configures the Serilog pipeline |
| `InfrastructureServiceCollectionExtensions.cs` | `Logging/` | `NCVizDash.Infrastructure.Logging` | `AddNCVizDashInfrastructure()` DI extension |

### 3.4 NCVizDash.RuleEngine

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.RuleEngine.csproj` | *(root)* | — | Project file |
| `FieldComposition.cs` | *(root)* | `NCVizDash.RuleEngine` | Summarises a field selection into counts + name-hint flags |
| `VisualizationRule.cs` | *(root)* | `NCVizDash.RuleEngine` | Named, prioritised rule record |
| `RuleRegistry.cs` | *(root)* | `NCVizDash.RuleEngine` | All 25 ordered rules |
| `DeterministicRuleEngine.cs` | *(root)* | `NCVizDash.RuleEngine` | `IVisualizationRuleEngine` impl |

### 3.5 NCVizDash.ChartEngine

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.ChartEngine.csproj` | *(root)* | — | Project file |
| `AnimationPresets.cs` | *(root)* | `NCVizDash.ChartEngine` | Per-`VisualType` ECharts animation config |
| `ChartPalette.cs` | *(root)* | `NCVizDash.ChartEngine` | Light/Dark 10-colour brand palettes |
| `EChartsChartEngine.cs` | *(root)* | `NCVizDash.ChartEngine` | `IChartEngine` impl — dispatcher |
| `ChartOptionContext.cs` | `Builders/` | `NCVizDash.ChartEngine.Builders` | Shared data-extraction + config-block helpers |
| `CartesianBuilder.cs` | `Builders/` | `NCVizDash.ChartEngine.Builders` | Bar, Line, Area |
| `PolarBuilder.cs` | `Builders/` | `NCVizDash.ChartEngine.Builders` | Pie, Donut, Gauge, Radar |
| `XyBuilder.cs` | `Builders/` | `NCVizDash.ChartEngine.Builders` | Scatter, Bubble, Heatmap, Treemap |
| `HtmlBuilder.cs` | `Builders/` | `NCVizDash.ChartEngine.Builders` | KPI, Table |

### 3.6 NCVizDash.DuckDB

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.DuckDB.csproj` | *(root)* | — | Project file |
| `DuckDbAnalyticsEngine.cs` | *(root)* | `NCVizDash.DuckDB` | `IAnalyticsEngine` impl |
| `AnalyticsQueryBuilder.cs` | *(root)* | `NCVizDash.DuckDB` | Pure `QuerySpec` → DuckDB SQL translator |

### 3.7 NCVizDash.Persistence

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.Persistence.csproj` | *(root)* | — | Project file |
| `WorkbookDashboardRepository.cs` | *(root)* | `NCVizDash.Persistence` | `IDashboardRepository` impl — **stub**, Phase 10 completes it |

### 3.8 NCVizDash.Ribbon

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.Ribbon.csproj` | *(root)* | — | Project file — must mark the .xml below as `EmbeddedResource` |
| `NCVizDashRibbon.xml` | *(root)* | — (XML) | Fluent UI ribbon markup |
| `NCVizDashRibbon.cs` | *(root)* | `NCVizDash.Ribbon` | `IRibbonExtensibility` impl + callbacks |

### 3.9 NCVizDash.TaskPane

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.TaskPane.csproj` | *(root)* | — | Project file |
| `chart-host.html` | `Assets/` | — (static) | WebView2 harness |
| `echarts.min.js` | `Assets/` | — (static, **not committed**) | ECharts distribution — see §9 |
| `DashboardCanvas.cs` | `Controls/` | `NCVizDash.TaskPane.Controls` | Custom `Panel`: positioning, move/resize/select, guides, filter click wiring |
| `WidgetCard.cs` | `Controls/` | `NCVizDash.TaskPane.Controls` | Per-widget chrome; hosts a child `ChartHost` |
| `ChartHost.cs` | `Controls/` | `NCVizDash.TaskPane.Controls` | `WebView2` wrapper |
| `ValueConverters.cs` | `Converters/` | `NCVizDash.TaskPane.Converters` | 6 WPF value converters |
| `GridGeometryHelper.cs` | `Geometry/` | `NCVizDash.TaskPane.Geometry` | Pure snap/clamp/overlap/guide math |
| `ThemeService.cs` | `Services/` | `NCVizDash.TaskPane.Services` | Theme change coordination |
| `WidgetRenderCoordinator.cs` | `Services/` | `NCVizDash.TaskPane.Services` | Builds `QuerySpec` per widget, queries, renders |
| `CrossFilterManager.cs` | `Services/` | `NCVizDash.TaskPane.Services` | `IFilterManager` impl |
| `GlobalFilterManager.cs` | `Services/` | `NCVizDash.TaskPane.Services` | `IGlobalFilterManager` impl |
| `DistinctValueService.cs` | `Services/` | `NCVizDash.TaskPane.Services` | Generic distinct-value lookup |
| `ShellViewModel.cs` | `ViewModels/` | `NCVizDash.TaskPane.ViewModels` | Root shell ViewModel |
| `PanelViewModels.cs` | `ViewModels/` | `NCVizDash.TaskPane.ViewModels` | Explorer/Canvas/VisualLibrary ViewModels |
| `GlobalFilterBarViewModel.cs` | `ViewModels/` | `NCVizDash.TaskPane.ViewModels` | Filter bar ViewModel + field-option model |
| `ShellWindow.xaml(.cs)` | `Views/` | `NCVizDash.TaskPane.Views` | Three-panel shell window |
| `ExplorerPanelView.xaml(.cs)` | `Views/` | `NCVizDash.TaskPane.Views` | Left panel |
| `CanvasPanelView.xaml(.cs)` | `Views/` | `NCVizDash.TaskPane.Views` | Centre panel |
| `VisualLibraryView.xaml(.cs)` | `Views/` | `NCVizDash.TaskPane.Views` | Right panel |
| `GlobalFilterBarView.xaml(.cs)` | `Views/` | `NCVizDash.TaskPane.Views` | Filter bar UI |

### 3.10 NCVizDash.ExcelAddIn

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.ExcelAddIn.csproj` | *(root)* | — | Project file. **Startup project.** |
| `ThisAddIn.cs` | *(root)* | `NCVizDash.ExcelAddIn` | VSTO entry point + full DI composition root |
| `ExcelDataReader.cs` | `DataAccess/` | `NCVizDash.ExcelAddIn.DataAccess` | `IExcelDataReader` impl |

### 3.11 NCVizDash.Tests

All 24 files live under `tests/NCVizDash.Tests/`:

| File | Folder | Namespace | Purpose |
|---|---|---|---|
| `NCVizDash.Tests.csproj` | *(root)* | — | Project file |
| `TestFactories.cs` | `Core/` | `NCVizDash.Tests.Core` | Shared construction helpers |
| `DeterministicRuleEngineTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Phase 1 baseline rule-engine tests |
| `Phase5RuleEngineTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Full 25-rule registry tests |
| `FieldTypeClassifierTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Field classification |
| `ExplorerPanelViewModelTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Data-source load/search/preview |
| `WidgetFilterTests.cs` | `Core/` | `NCVizDash.Tests.Core` | `WidgetFilter` defaults + JSON round-trip |
| `GridGeometryHelperTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Snap/clamp/overlap/guides |
| `CanvasPanelViewModelTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Phase 3 baseline canvas tests |
| `CanvasPanelViewModelPhase4Tests.cs` | `Core/` | `NCVizDash.Tests.Core` | Move/resize/select/duplicate |
| `CanvasPanelRuleEngineIntegrationTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Field-drop → rule engine routing |
| `ThemeServiceTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Theme event coordination |
| `ValueConvertersTests.cs` | `Core/` | `NCVizDash.Tests.Core` | All 6 WPF converters |
| `AnimationAndPaletteTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Animation presets + palette |
| `EChartsChartEngineTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Engine dispatch, all 11 chart types |
| `ChartBuildersTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Per-builder structural assertions |
| `AnalyticsQueryBuilderTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Every aggregate/filter/window/pivot |
| `CrossFilterManagerTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Toggle/overwrite/self-exclusion |
| `SqlFilterTranslatorTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Shared filter-clause translator |
| `WidgetRenderCoordinatorTests.cs` | `Core/` | `NCVizDash.Tests.Core` | QuerySpec + filter merging |
| `GlobalFilterManagerTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Dashboard binding, filter CRUD |
| `DistinctValueServiceTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Generic distinct-value lookup |
| `GlobalFilterBarViewModelTests.cs` | `Core/` | `NCVizDash.Tests.Core` | Dynamic field discovery |
| `AppSettingsTests.cs` | `Infrastructure/` | `NCVizDash.Tests.Infrastructure` | `AppSettings` defaults |
| `DuckDbAnalyticsEngineTests.cs` | `Infrastructure/` | `NCVizDash.Tests.Infrastructure` | Real in-memory DuckDB integration tests |

---

## 4. MVVM Structure

All MVVM code lives in **NCVizDash.TaskPane** (the only project with a UI). The
pattern is standard MVVM Toolkit (`[ObservableProperty]`, `[RelayCommand]`), one
folder per concern:

| Concern | Folder | Files |
|---|---|---|
| **Views** | `Views/` | `ShellWindow`, `ExplorerPanelView`, `CanvasPanelView`, `VisualLibraryView`, `GlobalFilterBarView` — each a `.xaml` + `.xaml.cs` pair |
| **ViewModels** | `ViewModels/` | `ShellViewModel`, `ExplorerPanelViewModel`, `CanvasPanelViewModel`, `VisualLibraryViewModel` (all in `PanelViewModels.cs`), `GlobalFilterBarViewModel` |
| **Models** | *(not in TaskPane)* | Domain models live in `NCVizDash.Models` (`Dashboard.cs`, `DataSourceDescriptor.cs`) — deliberately outside the UI project so they're reusable by non-UI code (persistence, engines) |
| **Services** | `Services/` | `ThemeService`, `WidgetRenderCoordinator`, `CrossFilterManager`, `GlobalFilterManager`, `DistinctValueService` |
| **Interfaces** | *(not in TaskPane)* | All service interfaces live in `NCVizDash.Core/Abstractions/IServices.cs` — TaskPane only contains implementations, never interface declarations, so Core stays the single source of contracts |
| **Commands** | *(no separate folder)* | Commands are `[RelayCommand]`-generated methods directly on the owning ViewModel — there is no separate `Commands/` folder; this is the MVVM Toolkit convention, not a missing piece |
| **Behaviors** | *(none exist)* | No WPF Behaviors (`Microsoft.Xaml.Behaviors`) are used anywhere in the solution. All interactivity (drag-drop, mouse gestures) is handled via direct event handlers in code-behind (`ExplorerPanelView.xaml.cs`, `CanvasPanelView.xaml.cs`, `DashboardCanvas.cs`) |
| **Resources** | `ShellWindow.xaml` `<Window.Resources>` | Converters and the `BundledTheme` are declared as window-level resources on `ShellWindow.xaml` and inherited by every child view — there is no separate `Resources/*.xaml` dictionary file yet |
| **Converters** | `Converters/` | `ValueConverters.cs` — 6 converters, all in one file |
| **Themes** | *(no dedicated folder)* | Material Design theming (Light/Dark, DeepPurple/Teal) is configured inline in `ShellWindow.xaml`'s `<md:BundledTheme>` element, not a separate theme resource dictionary |
| **Controls** (custom, code-only) | `Controls/` | `DashboardCanvas`, `WidgetCard`, `ChartHost` — all pure C# `FrameworkElement`/`Panel`/`UserControl` subclasses with no XAML |
| **UserControls** (XAML-based) | `Views/` | The five `Views/*.xaml` files are technically `UserControl`s (except `ShellWindow`, which is a `Window`) — kept in `Views/` rather than a separate `UserControls/` folder since in this solution "View" and "UserControl" are the same thing |

---

## 5. Connector Structure — NOT YET BUILT

**Nothing in this section exists in the current solution.** Phase 14 ("Data
Connectors") is where SQL Server, Oracle, PostgreSQL, MySQL, SQLite, CSV, JSON, XML,
REST API, and SharePoint connectors get built. The original 18-phase master prompt
also mentions Jira and Azure DevOps connectors, which aren't in the Phase 14 task
list either — they'd need to be added to the plan explicitly before implementation.

**Planned shape (for when Phase 14 starts — not present today):**

```
src/NCVizDash.Connectors/
├── NCVizDash.Connectors.csproj
├── IDataConnector.cs                    (new Core.Abstractions-style interface)
├── Sql/
│   ├── SqlServerConnector.cs
│   ├── PostgresConnector.cs
│   ├── MySqlConnector.cs
│   └── SqliteConnector.cs
├── Rest/
│   └── RestApiConnector.cs
├── SharePoint/
│   └── SharePointListConnector.cs
├── Csv/
│   └── CsvFileConnector.cs
├── Json/
│   └── JsonFileConnector.cs
├── Jira/                                 (not in the approved 18-phase plan yet)
│   └── JiraConnector.cs
└── AzureDevOps/                          (not in the approved 18-phase plan yet)
    └── AzureDevOpsConnector.cs
```

Each connector would implement a shared `IDataConnector` (to be added to
`NCVizDash.Core.Abstractions`, following the same pattern as `IExcelDataReader`) and
produce a `DataSourceDescriptor` + row list — the exact same shape
`IExcelDataReader.GetDataSourcesAsync`/`ReadRowsAsync` already produce, so
`IAnalyticsEngine.LoadDataSourceAsync` and everything downstream (rule engine, chart
engine, filters) needs zero changes to consume connector data once Phase 14 lands.

**Do not create these folders now.** Add them when Phase 14 is actually implemented.

---

## 6. Dashboard Engine Structure

| Component | Location | Status |
|---|---|---|
| **Canvas** | `NCVizDash.TaskPane/Controls/DashboardCanvas.cs` | ✅ Built (Phase 4) — custom `Panel`, absolute pixel positioning via `ArrangeOverride`, `OnRender`-based grid/guide/rubber-band drawing |
| **Widgets** | `NCVizDash.TaskPane/Controls/WidgetCard.cs` (chrome) + `NCVizDash.Models/Dashboard.cs` (`DashboardWidget` data model) | ✅ Built |
| **Layout Manager** | Logic embedded in `DashboardCanvas` (`MoveWidget`, `ResizeWidget` on `CanvasPanelViewModel`) + `NCVizDash.TaskPane/Geometry/GridGeometryHelper.cs` (pure math) | ✅ Built (Phase 4) — no separate `LayoutManager` class; the math is factored out into `GridGeometryHelper` for testability, but orchestration lives directly on `CanvasPanelViewModel` |
| **Grid Manager** | Same as Layout Manager — `GridGeometryHelper.SnapToGrid`/`ClampPosition`/`ClampColumnSpan`/`ClampRowSpan` | ✅ Built — not a separate class; "grid management" and "layout management" turned out to be the same concern in this codebase, so they share one helper rather than being artificially split |
| **Filter Manager** (cross-filter) | `NCVizDash.TaskPane/Services/CrossFilterManager.cs` (implements `IFilterManager` in Core) | ✅ Built (Phase 8) |
| **Filter Manager** (global) | `NCVizDash.TaskPane/Services/GlobalFilterManager.cs` (implements `IGlobalFilterManager` in Core) | ✅ Built (Phase 9) |
| **Selection Manager** | Logic embedded in `CanvasPanelViewModel` (`SelectWidget`, `ClearSelection`, `SelectedWidgets`) + `DashboardCanvas` (rubber-band hit-testing) | ✅ Built (Phase 4) — no separate class; selection state is simple enough (a set of widget IDs) that a dedicated manager wasn't warranted |
| **Theme Manager** | `NCVizDash.TaskPane/Services/ThemeService.cs` | ✅ Built (Phase 3) |
| **Persistence** | `NCVizDash.Persistence/WorkbookDashboardRepository.cs` | ⬜ **Stub only** — Phase 10 |
| **History / Undo-Redo** | *(none)* | ⬜ **Not built** — Phase 12 ("Advanced Features"). The canvas toolbar already has disabled Undo/Redo buttons as UI placeholders (`CanvasPanelView.xaml`, `IsEnabled="False"`) so the layout won't need to change when Phase 12 wires them up |

---

## 7. DuckDB Structure

Everything lives in the single `NCVizDash.DuckDB` project — there is no further
sub-folder split, because the two files together are under 400 lines and a deeper
folder structure would add navigation overhead without benefit at this size.

| Concern | File | Notes |
|---|---|---|
| **Connection** | `DuckDbAnalyticsEngine.cs` | Owns one `DuckDBConnection` per add-in session, opened in the constructor (`DataSource=:memory:`), disposed in `Dispose()` |
| **Query Engine** | `DuckDbAnalyticsEngine.QueryAsync` (both the raw-SQL and `QuerySpec` overloads) | Thread-guarded via a private lock object around all connection access |
| **Schema Manager** | `DuckDbAnalyticsEngine.CreateTable`/`DropTableIfExists` | DDL generation is inline, keyed off `FieldType` → DuckDB type mapping (`MapToDuckDbType`) |
| **Metadata** | `DuckDbAnalyticsEngine.GetTableName` + the private `_loadedTables` dictionary (`Guid` → sanitised table name) | This *is* the metadata layer — no separate class |
| **Import** | `DuckDbAnalyticsEngine.LoadDataSourceAsync` (+ `BulkInsert`) | Transactional, parameterised bulk insert |
| **Export** | *(not built)* | No export-from-DuckDB path exists yet; Phase 13 ("Export") will add PDF/PPTX/PNG export of *rendered charts*, which is a different concern from exporting raw DuckDB query results — if raw data export is wanted later, it would be a new method on `IAnalyticsEngine` |
| **Cache** | *(not built)* | Every widget re-queries DuckDB on every render; there is no query-result cache yet. Phase 16 ("Performance") is the natural home for this |
| **Refresh** | `ThisAddIn.OnSheetChange` (debounced, in `NCVizDash.ExcelAddIn`) → `ExplorerPanelViewModel.LoadDataSourcesAsync` → `DuckDbAnalyticsEngine.LoadDataSourceAsync` (drop + recreate table) | Cross-project flow, not a single file — refresh is triggered by the add-in host, not by DuckDB itself |
| **Query building** | `AnalyticsQueryBuilder.cs` | Pure static translator, `QuerySpec` → SQL string; this is the one part of "Query Engine" substantial enough to warrant its own file |

---

## 8. Chart Engine Structure

All in `NCVizDash.ChartEngine`. Per-chart-type code lives in `Builders/`, grouped by
rendering family (not one file per chart type) because charts within a family share
axis/tooltip/legend plumbing:

| Chart type | File | Builder method |
|---|---|---|
| Bar | `Builders/CartesianBuilder.cs` | `BuildBar` |
| Line | `Builders/CartesianBuilder.cs` | `BuildLine` |
| Area | `Builders/CartesianBuilder.cs` | `BuildArea` |
| Pie | `Builders/PolarBuilder.cs` | `BuildPie` |
| Donut | `Builders/PolarBuilder.cs` | `BuildDonut` |
| Gauge | `Builders/PolarBuilder.cs` | `BuildGauge` |
| Radar | `Builders/PolarBuilder.cs` | `BuildRadar` |
| Scatter | `Builders/XyBuilder.cs` | `BuildScatter` |
| Bubble | `Builders/XyBuilder.cs` | `BuildBubble` |
| Heatmap | `Builders/XyBuilder.cs` | `BuildHeatmap` |
| Treemap | `Builders/XyBuilder.cs` | `BuildTreemap` |
| Table | `Builders/HtmlBuilder.cs` | `BuildTableHtml` (HTML, not ECharts) |
| KPI | `Builders/HtmlBuilder.cs` | `BuildKpiHtml` (HTML, not ECharts) |

| Cross-cutting concern | File | Notes |
|---|---|---|
| **Chart Factory** | `EChartsChartEngine.BuildEChartsOption` (private `switch` expression) | This *is* the factory — dispatches `VisualType` → the correct builder method. No separate `ChartFactory` class; the switch is the whole factory and lives directly in the engine |
| **Chart Renderer** | `NCVizDash.TaskPane/Controls/ChartHost.cs` + `Assets/chart-host.html` | The "renderer" is split across two layers by necessity: `ChartHost.cs` is the WPF/WebView2 host (C#), `chart-host.html` is the actual ECharts `init`/`setOption` JS harness that does the rendering. This is **not** in `NCVizDash.ChartEngine` — the chart engine only *produces JSON*, it never touches WPF or WebView2, keeping it UI-framework-agnostic |
| **Chart Options** (shared building blocks) | `Builders/ChartOptionContext.cs` | Tooltip/legend/axis/grid helpers + data-extraction methods shared by every builder |
| **Animation** | `AnimationPresets.cs` | Per-`VisualType` easing/duration, merged into every option by `EChartsChartEngine.MergeAnimationPreset` |
| **Colour palette** | `ChartPalette.cs` | Light/Dark 10-colour brand palettes + semantic colours (positive/negative/neutral) |

---

## 9. Resource Structure

| Resource type | Location | Status |
|---|---|---|
| **Icons** | *(none as files)* | All icons in the UI come from `MaterialDesignThemes`' `PackIcon` (`md:PackIcon Kind="..."`) — a font-based icon set bundled with the NuGet package. There are no custom `.ico`/`.png` icon files anywhere in the solution yet. If a custom app icon is needed for the installer (Phase "Setup", not yet built), it would go in a new `assets/icons/` folder at the solution root |
| **Images** | *(none)* | No image assets exist. If product screenshots/logos are added for `docs/` or the future installer, they'd go in a new `assets/images/` folder |
| **Themes** | Inline in `ShellWindow.xaml` | `<md:BundledTheme BaseTheme="Light" PrimaryColor="DeepPurple" SecondaryColor="Teal" />` — not a separate resource dictionary file |
| **Fonts** | *(none custom)* | UI uses `Segoe UI` (Windows system font) throughout — see `WidgetCard.cs`'s `Typeface TitleTypeface = new("Segoe UI")` and the CSS in `chart-host.html`/`HtmlBuilder.cs`. No embedded/custom font files |
| **Localization** | *(none)* | No `.resx` files or localization infrastructure exists. All UI strings are hardcoded English literals directly in XAML/C#. This would be a new concern to design from scratch if internationalization is ever required — not part of the current 18-phase plan |
| **Templates** (dashboard templates) | *(none — Phase 11)* | "Executive Dashboard", "Sales Dashboard", etc. from Phase 11 don't exist yet. When built, they'd likely live as JSON files (serialized `Dashboard` objects) in a new `src/NCVizDash.TaskPane/Templates/` or `assets/templates/` folder — TBD when Phase 11 is scoped |
| **WebView2 chart harness** | `src/NCVizDash.TaskPane/Assets/chart-host.html` (committed) | The one genuine "asset" file in the solution today |
| **ECharts library** | `src/NCVizDash.TaskPane/Assets/echarts.min.js` | **Not committed.** Apache-2.0 licensed, ~1MB minified. Obtain via `npm install echarts` and copy `node_modules/echarts/dist/echarts.min.js` to this exact path, or download directly from https://echarts.apache.org/en/download.html. The `.csproj`'s `<Content Include="Assets\echarts.min.js" Condition="Exists(...)">` means the solution still builds without it — but chart widgets will show a "file not found" error at runtime until it's present |

---

## 10. Configuration Files

| File | Location | Status |
|---|---|---|
| `app.config` | *(none — not applicable)* | .NET 8 SDK-style projects don't use `app.config`/`web.config`; configuration is `appsettings`-style or code-based. Not needed here |
| `manifest.xml` (VSTO/Office manifest) | *(none yet)* | VSTO add-ins built with the SDK-style `net8.0-windows` `.csproj` shown here don't hand-author a `manifest.xml` the way legacy `.vsto`/ClickOnce projects do — deployment manifest generation is a Visual-Studio-project-properties concern (Office/SharePoint publish settings) that hasn't been configured yet, since the add-in has only been run via F5 debugging so far, not packaged for distribution |
| `settings.json` | `%LOCALAPPDATA%\NCVizDash\ncvizdash.json` (runtime, **not in source control**) | Generated at first run by `JsonAppSettingsProvider.EnsureDefaultFileExists()` — the *default content* is a string literal inside `JsonAppSettingsProvider.cs`, not a checked-in file, since it's genuinely per-user runtime state |
| `logging.json` | *(none — config is code, not JSON)* | Serilog is configured entirely in code (`SerilogBootstrapper.cs`) driven by `AppSettings.LogLevel`/`LogDirectory`, which themselves come from `ncvizdash.json` above. There's no separate `logging.json` |
| `launchSettings.json` | *(none — not applicable)* | `launchSettings.json` is an ASP.NET Core / console-app debugging concept (`dotnet run` profiles). VSTO add-ins launch via Visual Studio's "Start External Program" (Excel.exe), configured in project Debug properties, not a JSON file |
| `Directory.Build.props` | *(none yet)* | No solution-wide MSBuild props file exists — every `.csproj` currently repeats `<TargetFramework>net8.0-windows</TargetFramework>`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, etc. individually. **Recommended cleanup when reassembling in Visual Studio:** add a `Directory.Build.props` at the solution root with these shared properties to remove the duplication — this is safe to do at any time since it doesn't change behaviour, only reduces repetition |
| `NuGet.Config` | *(none yet — uses default feeds)* | All packages resolve from the default `nuget.org` source; no private feed or custom config has been needed |
| `.editorconfig` | *(none yet)* | No solution-wide code-style enforcement file exists. Style has been kept consistent by convention (XML doc comments on every public member, `TreatWarningsAsErrors`) rather than an `.editorconfig` |

---

## 11. Documentation

| File | Location | Status |
|---|---|---|
| `README.md` | Solution root | ✅ Exists — product overview, quick start, architecture diagram, tech stack table, phase status table, configuration reference |
| `TASKS.md` | Solution root | ✅ Exists — full phase-by-phase task checklist, Phases 1–9 checked off in detail, Phases 10–18 outlined |
| `CHANGELOG.md` | Solution root | ✅ Exists — one dated entry per phase (0.1.0 through 0.9.0), Keep-a-Changelog format, includes an "Architecture decisions" subsection per phase explaining *why*, not just *what* |
| `SOLUTION_ASSEMBLY_GUIDE.md` | Solution root | ✅ This document |
| `PRD.md` | *(none yet)* | Not written. The closest equivalent today is the "PRODUCT VISION" / "TARGET USERS" sections of the original master prompt plus `README.md`'s overview — a formal PRD would be a new document, not a rename of anything existing |
| `ARCHITECTURE.md` | *(none yet)* | Not written as a standalone doc. Architecture rationale is currently distributed across each phase's `CHANGELOG.md` entry ("Architecture decisions" subsections) rather than centralised. **Recommended when reassembling:** if a single architecture doc is wanted, it can be assembled by extracting those subsections chronologically — no new investigation needed, just compilation |
| `ROADMAP.md` | *(none yet)* | `TASKS.md` currently serves this purpose (it lists all 18 phases with status) — a dedicated `ROADMAP.md` would be redundant with `TASKS.md` unless it's meant to carry different content (e.g. target dates, not just task lists) |
| `CONTRIBUTING.md` | *(none yet)* | Not written. `README.md` has a brief 4-point "Contributing" section (one phase per PR, `dotnet test` must pass, zero warnings, update docs) but no dedicated file |

---

## 12. Build Order

MSBuild determines actual compile order automatically from `<ProjectReference>`
entries — this section documents the resulting order for verification purposes, not
as a manual instruction list.

```
┌─────────────────────────────────────────────────────────────────────┐
│  1. NCVizDash.Models              (no dependencies)                  │
└─────────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────────┐
│  2. NCVizDash.Core                (→ Models)                         │
└─────────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┬──────────────────┐
        ▼                     ▼                     ▼                  ▼
┌───────────────┐   ┌──────────────────┐   ┌────────────────┐  ┌──────────────┐
│ 3. Infra-      │   │ 3. RuleEngine     │   │ 3. ChartEngine  │  │ 3. Ribbon    │
│    structure   │   │ (→ Core, Models)  │   │ (→ Core, Models)│  │(→ Core,Models)│
│ (→ Core,Models)│   └──────────────────┘   └────────────────┘  └──────────────┘
└───────────────┘
        │                                            │
        ▼                                            │
┌────────────────────┬──────────────────────┐        │
│ 4. DuckDB           │ 4. Persistence        │        │
│ (→ Core, Models)    │ (→ Core, Models)      │        │
└────────────────────┴──────────────────────┘        │
                                                       ▼
                                          ┌────────────────────────┐
                                          │ 5. TaskPane             │
                                          │ (→ Core, Models,        │
                                          │    ChartEngine)         │
                                          └────────────────────────┘
                                                       │
        ┌──────────────────────────────────────────────┘
        ▼
┌──────────────────────────────────────────────────────────────────┐
│ 6. ExcelAddIn   ★ STARTUP PROJECT ★                                │
│ (→ Core, Infrastructure, Models, Ribbon, TaskPane,                 │
│    DuckDB, Persistence, RuleEngine, ChartEngine)                   │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│ 7. NCVizDash.Tests                                                  │
│ (→ Core, Models, Infrastructure, RuleEngine, ChartEngine,          │
│    DuckDB, TaskPane)                                                │
└──────────────────────────────────────────────────────────────────┘
```

**Startup project:** `NCVizDash.ExcelAddIn`. Right-click it in Solution Explorer →
"Set as Startup Project" if it isn't already. Debug target is Excel itself (VSTO
project properties → Debug → configured to launch `EXCEL.EXE` with the add-in
registered).

**Why `NCVizDash.TaskPane` doesn't reference `NCVizDash.DuckDB`:** this is
intentional, not an oversight — see the Phase 7 "Architecture decisions" entry in
`CHANGELOG.md`. `WidgetRenderCoordinator` (in TaskPane) builds an engine-agnostic
`QuerySpec` (defined in `Core.Analytics`) and calls it through the `IAnalyticsEngine`
interface; only `NCVizDash.ExcelAddIn`'s DI registration ever mentions the concrete
`DuckDbAnalyticsEngine` type. If DuckDB is ever swapped for a different engine, only
`ThisAddIn.cs`'s DI registration line changes.

---

## 13. Visual Studio Solution Structure

How Solution Explorer should look after opening `NCVizDash.sln`. No Solution Folders
(the `<Project>` grouping kind used purely for Explorer organisation, distinct from
actual class-library projects) exist in the current `.sln` — every project sits flat
at the solution root. This is a reasonable simplification at 11 projects; consider
adding Solution Folders (`src`, `tests`) if the project count grows significantly in
later phases.

```
Solution 'NCVizDash' (11 of 11 projects)
│
├── 📁 NCVizDash.Models
│   ├── 📄 AppSettings.cs
│   ├── 📄 Dashboard.cs
│   ├── 📄 DataSourceDescriptor.cs
│   └── 📚 Dependencies
│       └── 📦 NuGet: System.Text.Json
│
├── 📁 NCVizDash.Core
│   ├── 📁 Abstractions
│   │   └── 📄 IServices.cs
│   ├── 📁 Analytics
│   │   ├── 📄 QuerySpec.cs
│   │   └── 📄 SqlFilterTranslator.cs
│   ├── 📁 Classification
│   │   └── 📄 FieldTypeClassifier.cs
│   ├── 📁 DependencyInjection
│   │   └── 📄 CoreServiceCollectionExtensions.cs
│   └── 📚 Dependencies
│       ├── 🔗 Project: NCVizDash.Models
│       └── 📦 NuGet: 3 packages (DI/Logging/Config Abstractions)
│
├── 📁 NCVizDash.Infrastructure
│   ├── 📁 Configuration → JsonAppSettingsProvider.cs
│   ├── 📁 Logging → SerilogBootstrapper.cs, InfrastructureServiceCollectionExtensions.cs
│   └── 📚 Dependencies → Core, Models + 12 NuGet packages
│
├── 📁 NCVizDash.RuleEngine
│   ├── 📄 FieldComposition.cs
│   ├── 📄 VisualizationRule.cs
│   ├── 📄 RuleRegistry.cs
│   ├── 📄 DeterministicRuleEngine.cs
│   └── 📚 Dependencies → Core, Models
│
├── 📁 NCVizDash.ChartEngine
│   ├── 📄 AnimationPresets.cs
│   ├── 📄 ChartPalette.cs
│   ├── 📄 EChartsChartEngine.cs
│   ├── 📁 Builders
│   │   ├── 📄 ChartOptionContext.cs
│   │   ├── 📄 CartesianBuilder.cs
│   │   ├── 📄 PolarBuilder.cs
│   │   ├── 📄 XyBuilder.cs
│   │   └── 📄 HtmlBuilder.cs
│   └── 📚 Dependencies → Core, Models
│
├── 📁 NCVizDash.DuckDB
│   ├── 📄 DuckDbAnalyticsEngine.cs
│   ├── 📄 AnalyticsQueryBuilder.cs
│   └── 📚 Dependencies → Core, Models + DuckDB.NET packages
│
├── 📁 NCVizDash.Persistence
│   ├── 📄 WorkbookDashboardRepository.cs
│   └── 📚 Dependencies → Core, Models, Excel Interop
│
├── 📁 NCVizDash.Ribbon
│   ├── 📄 NCVizDashRibbon.xml   (Build Action: Embedded Resource)
│   ├── 📄 NCVizDashRibbon.cs
│   └── 📚 Dependencies → Core, Models, Excel Interop
│
├── 📁 NCVizDash.TaskPane
│   ├── 📁 Assets
│   │   ├── 📄 chart-host.html
│   │   └── 📄 echarts.min.js   ⚠ not committed, add manually
│   ├── 📁 Controls
│   │   ├── 📄 DashboardCanvas.cs
│   │   ├── 📄 WidgetCard.cs
│   │   └── 📄 ChartHost.cs
│   ├── 📁 Converters
│   │   └── 📄 ValueConverters.cs
│   ├── 📁 Geometry
│   │   └── 📄 GridGeometryHelper.cs
│   ├── 📁 Services
│   │   ├── 📄 ThemeService.cs
│   │   ├── 📄 WidgetRenderCoordinator.cs
│   │   ├── 📄 CrossFilterManager.cs
│   │   ├── 📄 GlobalFilterManager.cs
│   │   └── 📄 DistinctValueService.cs
│   ├── 📁 ViewModels
│   │   ├── 📄 ShellViewModel.cs
│   │   ├── 📄 PanelViewModels.cs
│   │   └── 📄 GlobalFilterBarViewModel.cs
│   ├── 📁 Views
│   │   ├── 📄 ShellWindow.xaml ⚙ ShellWindow.xaml.cs
│   │   ├── 📄 ExplorerPanelView.xaml ⚙ .xaml.cs
│   │   ├── 📄 CanvasPanelView.xaml ⚙ .xaml.cs
│   │   ├── 📄 VisualLibraryView.xaml ⚙ .xaml.cs
│   │   └── 📄 GlobalFilterBarView.xaml ⚙ .xaml.cs
│   └── 📚 Dependencies → Core, Models, ChartEngine + MaterialDesign/WebView2/Mvvm packages
│
├── 📁 NCVizDash.ExcelAddIn   ★ (bold — startup project)
│   ├── 📄 ThisAddIn.cs
│   ├── 📁 DataAccess
│   │   └── 📄 ExcelDataReader.cs
│   └── 📚 Dependencies → all 8 other src/ projects + Excel Interop + Serilog
│
└── 📁 NCVizDash.Tests
    ├── 📁 Core (23 files)
    ├── 📁 Infrastructure (2 files)
    └── 📚 Dependencies → Core, Models, Infrastructure, RuleEngine, ChartEngine, DuckDB, TaskPane
```

**Solution-level files** (visible at the very top of Solution Explorer, above all
projects, via "Show All Files" or already tracked): `NCVizDash.sln`, `README.md`,
`TASKS.md`, `CHANGELOG.md`, `SOLUTION_ASSEMBLY_GUIDE.md`.

---

## 14. Final Validation Checklist

Work through this after placing every file, before attempting a build.

**Structural**
- [ ] Every `.cs`/`.xaml` file listed in §3 exists at the exact folder path specified
- [ ] Every `namespace` declaration matches its folder (verified programmatically
      against the real source for this guide — see the table notes in §3; spot-check
      a handful after copying, e.g. `Builders/CartesianBuilder.cs` should declare
      `namespace NCVizDash.ChartEngine.Builders;`)
- [ ] `NCVizDashRibbon.xml` build action is **Embedded Resource**, not Content —
      check this explicitly, it's the single most common VSTO ribbon mistake
- [ ] `Assets/chart-host.html` build action is **Content**, `Copy to Output
      Directory = Copy if newer`, linked to `Assets\ChartHost\chart-host.html` in
      the output
- [ ] `Assets/echarts.min.js` has been manually downloaded and placed (not committed
      — see §9) before attempting to actually render a chart at runtime; the
      solution *builds* without it but chart widgets show a runtime error until
      it's present

**References**
- [ ] Every project reference in §2 is present in each `.csproj` — no more, no
      fewer (in particular: confirm `NCVizDash.TaskPane` does **not** reference
      `NCVizDash.DuckDB` — this is intentional, not a gap)
- [ ] No circular project references exist (the build-order diagram in §12 is a
      strict DAG — if MSBuild reports a circular dependency, a reference was added
      somewhere it shouldn't be)
- [ ] All NuGet package versions match §2 exactly — version drift (e.g. picking up
      a newer transitive `Microsoft.Extensions.*` package) is the most likely
      source of a "builds here, not there" discrepancy between environments

**Duplicates**
- [ ] No duplicate file exists — every filename in §3 appears exactly once in the
      solution. (Two files share a base name across projects only by suffix:
      `NCVizDash.*.csproj` — that's expected, one per project, not a duplicate.)
- [ ] No duplicate class/interface definitions — in particular, `WidgetFilter`,
      `FilterOperator`, `QuerySpec`, and `AggregateFunction` should each be defined
      in exactly **one** file (`Dashboard.cs` for the first two, `QuerySpec.cs` for
      the latter two) and referenced everywhere else via `using`, never redeclared

**Build**
- [ ] `dotnet restore` succeeds from the solution root before opening in Visual
      Studio (catches NuGet source/version issues early, independent of the IDE)
- [ ] Build order matches §12 exactly when doing a clean rebuild (Build → Clean
      Solution, then Build → Rebuild Solution) — if a project builds out of the
      expected order, its `.csproj` references are wrong
- [ ] `NCVizDash.ExcelAddIn` is set as the Startup Project
- [ ] `dotnet test` (or Test Explorer in Visual Studio) discovers and can run all
      24 test files in `NCVizDash.Tests` — a `0 tests discovered` result almost
      always means a missing `PackageReference` to `Microsoft.NET.Test.Sdk` or
      `xunit.runner.visualstudio`, both listed in §2.11

**Environment**
- [ ] Windows 10/11 64-bit (VSTO + WPF are Windows-only — this solution cannot be
      built on macOS/Linux, including inside Cursor if Cursor itself is running on
      a non-Windows host)
- [ ] Microsoft Excel 2016+ installed (required for `Microsoft.Office.Interop.Excel`
      at both compile time — the PIA — and runtime debugging)
- [ ] Visual Studio 2022 with the **Office/SharePoint development** workload
      installed (provides VSTO project templates and the VSTO runtime)
- [ ] WebView2 Runtime present (ships with Windows 11 and current Edge; verify on
      Windows 10 machines specifically, as it's not always preinstalled there)

**Opens cleanly**
- [ ] `NCVizDash.sln` opens in Visual Studio 2022 with no "project could not be
      loaded" errors
- [ ] `NCVizDash.sln` (or the folder) opens in Cursor with the C# extension able to
      resolve all project references for IntelliSense (Cursor uses the same
      `.sln`/`.csproj`/OmniSharp or C# Dev Kit tooling as VS Code — no separate
      Cursor-specific configuration is needed beyond what's already in the
      `.csproj` files)

---

*End of SOLUTION_ASSEMBLY_GUIDE.md. This document reflects Phases 1–9 exactly as
built and committed to `CHANGELOG.md`. Regenerate or extend this guide after each
subsequent phase (10 onward) rather than letting it drift out of sync with the
actual source tree.*
