# NC VizDash – Task Tracker

## Legend
- ✅ Complete
- 🔄 In Progress
- ⬜ Not Started

---

## Phase 1 – Foundation ✅

- [x] Solution file (NCVizDash.sln)
- [x] NCVizDash.Models – Domain models (FieldDescriptor, Dashboard, DashboardWidget, AppSettings)
- [x] NCVizDash.Core – Service abstractions (IExcelDataReader, IAnalyticsEngine, IDashboardRepository, IVisualizationRuleEngine, IChartEngine, IFilterManager, IAppSettingsProvider)
- [x] NCVizDash.Infrastructure – Serilog bootstrap, JSON settings provider, DI extensions
- [x] NCVizDash.Ribbon – Ribbon XML + IRibbonExtensibility callbacks
- [x] NCVizDash.TaskPane – WPF three-panel shell (Explorer / Canvas / Visual Library)
- [x] NCVizDash.ExcelAddIn – VSTO ThisAddIn, DI composition root, Excel event wiring
- [x] NCVizDash.DuckDB – Stub (Phase 2 / 7)
- [x] NCVizDash.Persistence – Stub (Phase 10)
- [x] NCVizDash.RuleEngine – Stub with deterministic rule table (Phase 5)
- [x] NCVizDash.ChartEngine – Stub with minimal Bar/KPI (Phase 6)
- [x] NCVizDash.Tests – Unit tests for RuleEngine and AppSettings

---

## Phase 2 – Excel Data Engine ✅

- [x] Read Excel ListObjects (Tables)
- [x] Read Named Ranges (worksheet-scoped and workbook-scoped)
- [x] Read Worksheets as data sources (via Tables/Named Ranges; raw used-range fallback deferred to Phase 3 UI trigger)
- [x] Auto-refresh on SheetChange event (debounced via AutoRefreshSeconds setting)
- [x] Convert range data to List<Dictionary<string,object?>>
- [x] Field type classifier (Number→Measure, Text→Dimension, Date→Time, Boolean→Filter) — NCVizDash.Core.Classification.FieldTypeClassifier
- [x] Date detection via cell NumberFormat (OLE date serial → DateTime conversion)
- [x] Load classified data into DuckDB (full DuckDbAnalyticsEngine implementation)
- [x] Implement IExcelDataReader in ExcelAddIn (NCVizDash.ExcelAddIn.DataAccess.ExcelDataReader)
- [x] Wire ExplorerPanelViewModel.LoadDataSourcesAsync() to real services
- [x] DI registration for IExcelDataReader, IAnalyticsEngine, IVisualizationRuleEngine, IChartEngine, IDashboardRepository
- [x] Unit tests: FieldTypeClassifierTests (13 tests)
- [x] Integration tests: DuckDbAnalyticsEngineTests (4 tests — load, aggregate, unload, reload)

---

## Phase 3 – User Interface ✅

- [x] Refine three-panel layout proportions (Visual Library widened to 220px/min190 for two-column tile fit)
- [x] Workbook Explorer tree with live search and filter (FilteredDataSources, reactive on SearchText)
- [x] Field drag source — fields in the explorer initiate WPF DragDrop with a custom "NCVizDash.Field" format
- [x] Data preview tooltip / mini-grid — debounced hover Popup with a 10-row DataGrid sample
- [x] Canvas empty-state improvements — updated copy referencing both drag sources
- [x] Canvas drop target — accepts both Visual Library tiles and Explorer fields; drag-over highlight border; auto-creates a default Dashboard on first drop
- [x] Visual Library drag handles — tiles initiate WPF DragDrop with a custom "NCVizDash.VisualType" format
- [x] Material Design theme switching (Light ↔ Dark) — ThemeService + direct BundledTheme mutation (VSTO-safe, no Application.Current dependency)
- [x] Fixed missing BooleanToVisibilityConverter / GridUnitConverter (referenced since Phase 1 XAML but never defined) — added full Converters.cs
- [x] Unit tests: ExplorerPanelViewModelTests (7), CanvasPanelViewModelTests (7), ThemeServiceTests (3), ValueConvertersTests (7)

---

## Phase 4 – Dashboard Builder ✅

> Baseline drop-to-add (Visual Library tile → canvas, Explorer field → canvas with
> auto-selected visual type) shipped in Phase 3 via `CanvasPanelViewModel.AddWidgetFromDrop`.
> This phase replaces the naive staggered placement with real interactive mechanics.

- [x] `DashboardCanvas` custom WPF Panel (absolute pixel positioning, WPF hit-testing, OnRender-based grid/guide/rubber-band drawing)
- [x] `WidgetCard` FrameworkElement (code-behind-only rendering: title bar, resize grip, selection highlight, elevation shadow)
- [x] Move widgets via left-button drag anywhere on card (snap-to-grid, bounds clamping)
- [x] Resize widgets via bottom-right corner grip (snap, minimum span enforcement)
- [x] Single-click widget selection
- [x] Ctrl+click additive multi-select
- [x] Click-on-background deselect all
- [x] Rubber-band multi-select (drag on background)
- [x] Delete selected widgets (toolbar button + Del key; multi-select aware)
- [x] Duplicate selected widgets (toolbar button + Ctrl+D; multi-select aware; deep-copies LocalFilters)
- [x] Snap to grid (toggle on toolbar; affects move and resize)
- [x] Alignment guides (computed during drag/resize from `GridGeometryHelper`, rendered as dashed lines)
- [x] Escape clears selection
- [x] `NullToCollapsedConverter` added; dashboard name bar showing active dashboard + widget count
- [x] `GridGeometryHelper` — pure, WPF-free snap/clamp/overlap/guide math
- [x] `WidgetLayout` and `DashboardWidget` made `INotifyPropertyChanged` (layout changes flow through to canvas arrange)
- [x] Unit tests: `GridGeometryHelperTests` (17), `CanvasPanelViewModelPhase4Tests` (17)

---

## Phase 5 – Rule Engine ✅

- [x] `FieldComposition` value object — summarises field selection into counts + name-hint flags (rate/financial/budget/geo/people) via term-table scanning; fully immutable and hashable
- [x] `VisualizationRule` record — named, prioritised predicate + `Explanation` string
- [x] `RuleRegistry` — 25 ordered rules covering every `VisualType` across 5 priority bands (100-199 specific combos, 200-299 time-series, 300-399 categorical, 400-499 multi-measure, 500-599 KPI, 900 Table fallback)
- [x] `DeterministicRuleEngine` — implements `IVisualizationRuleEngine.Recommend`, `RecommendWithExplanation` (returns rule name + human explanation for Phase 12 "Explain Chart"), `AllMatches` (full ranked match list for future visual-picker suggestion UI)
- [x] `IVisualizationRuleEngine` interface extended with `RecommendWithExplanation` and `AllMatches`
- [x] `CanvasPanelViewModel.AddWidgetFromFieldDrop` — delegates visual-type selection to rule engine; supports `overrideVisual` parameter for caller control; per-visual-type default column/row spans
- [x] All field-type switch statements removed from Views/Controls; rule engine is the single source of truth
- [x] Unit tests: `Phase5RuleEngineTests` (44 — per-rule, composition, name-hints, RecommendWithExplanation, AllMatches, registry ordering/uniqueness/fallback), `CanvasPanelRuleEngineIntegrationTests` (6 — field-drop routing, override, layout defaults)

---

## Phase 6 – Chart Engine ✅

- [x] WebView2 host control (`ChartHost`) integrated as a real child visual of `WidgetCard`
- [x] `chart-host.html` — static harness that loads local `echarts.min.js`, exposes `window.ncvizdashRender(payloadJson)`, posts `chart-click` and `host-ready` messages back to C# via `chrome.webview.postMessage`
- [x] `WidgetRenderCoordinator` — builds a GROUP BY aggregate SQL query from a widget's field mappings, runs it via `IAnalyticsEngine`, and produces the rendering payload via `IChartEngine`
- [x] `IAnalyticsEngine.GetTableName` added so rendering stays data-source-agnostic
- [x] KPI visual — animated count-up (JS `requestAnimationFrame` + cubic-out easing), trend indicator (▲/▼ % change first-vs-last row), HTML-escaped
- [x] Bar chart — grouped multi-measure bars, bounce-in animation
- [x] Line chart — smooth multi-series, fast linear draw-in animation
- [x] Pie chart — staggered slice entry, expansion animation
- [x] Donut chart — centre total label, same staggered entry as Pie
- [x] Area chart — gradient fill, stacked when multi-measure
- [x] Scatter chart — per-category colour grouping, fade-in animation
- [x] Bubble chart — 3-measure X/Y/size mapping with a JS `symbolSize` sqrt-scale function
- [x] Radar chart — per-category series or single aggregate series depending on dimension presence
- [x] Heatmap — 2-dimension × 1-measure grid with `visualMap` colour scale
- [x] Treemap — recursive multi-level aggregation by dimension hierarchy
- [x] Gauge — 0-100 clamped arc with tri-colour band (red/amber/green), elastic pointer snap, animated value counter
- [x] Table visual — HTML table with staggered per-row fade-in, HTML-escaped, capped at 200 rendered rows
- [x] Tooltip, legend, click-to-select interactions — `ChartOptionContext.AxisTooltip`/`ItemTooltip`/`BottomLegend` shared across builders; `chart-click` message posted back to C# via WebView2 (consumed by Phase 8 cross-filtering)
- [x] `AnimationPresets` — per-visual-type easing/duration tuned to the visual's character (elastic Gauge, bounce Bar, staggered Pie/Donut, linear Line/Area, etc.)
- [x] `ChartPalette` — 10-colour brand palette (DeepPurple/Teal-anchored) for Light and Dark themes; semantic positive/negative/neutral colours
- [x] `ChartOptionContext` — shared data-extraction and standard-block helpers (tooltip, legend, axis, grid) used by every builder
- [x] Theme-aware rendering — `DashboardCanvas.Theme` bound to `ShellViewModel.ActiveTheme`; changing theme live re-renders every widget with the new palette
- [x] All builder output verified to be valid, parseable JSON with animation config present for every chart-shaped `VisualType`
- [x] Unit tests: `AnimationAndPaletteTests` (14), `EChartsChartEngineTests` (19), `ChartBuildersTests` (16), `WidgetRenderCoordinatorTests` (7)

> **Build note:** `echarts.min.js` (Apache-2.0) must be downloaded once — via
> https://echarts.apache.org/en/download.html or `npm install echarts` then copying
> `dist/echarts.min.js` — into `NCVizDash.TaskPane/Assets/echarts.min.js` before building.
> It's intentionally excluded from source control (large generated binary); `chart-host.html`
> is committed since it's authored by this project. See the licence note in `ChartHost.cs`.

---

## Phase 7 – DuckDB Analytics ✅

- [x] `QuerySpec` / `MeasureSpec` / `WindowFunctionSpec` / `PivotSpec` (new `NCVizDash.Core.Analytics` namespace) — engine-agnostic, serialisable description of an analytics query
- [x] `AggregateFunction` enum — Sum, Count, CountDistinct, Avg, Min, Max, None (raw/unaggregated, used by Scatter/Bubble)
- [x] `AnalyticsQueryBuilder` (DuckDB-flavoured, pure/static, fully unit-testable without a live connection) — translates `QuerySpec` → SQL
- [x] GROUP BY + SUM / COUNT / COUNT DISTINCT / AVG / MIN / MAX
- [x] Sorting — explicit `SortField`/`SortDescending`, defaults to first dimension ascending
- [x] Filtering — every `FilterOperator` (Equals, NotEquals, GreaterThan(OrEqual), LessThan(OrEqual), Contains via ILIKE, In, NotIn, Between) translated to safely-escaped WHERE clauses; disabled filters excluded
- [x] Top N — `Limit` with a 5000-row safety cap regardless of what's requested
- [x] Window functions — RowNumber, Rank, DenseRank, RunningTotal, MovingAverage (configurable window size), PercentOfTotal; all support optional `PARTITION BY`
- [x] Pivot queries — DuckDB-native `PIVOT ... ON ... USING ...` statement generation
- [x] `IAnalyticsEngine.QueryAsync(QuerySpec)` — structured overload alongside the existing raw-SQL `QueryAsync(string)`; implemented in `DuckDbAnalyticsEngine` by delegating to `AnalyticsQueryBuilder`
- [x] `IAnalyticsEngine.GetTableName` promoted from Phase 6's ad-hoc addition into the formal Phase 7 query pipeline
- [x] `WidgetRenderCoordinator` rewritten to build a `QuerySpec` (no more hand-written SQL strings in the TaskPane project) — now also wires `DashboardWidget.LocalFilters` into the query automatically, closing the loop on the Phase 4 architectural correction
- [x] SQL injection safety — all identifiers sanitised, all literal values parametrised via safe escaping (numeric passthrough, `'` doubled in strings)
- [x] Unit tests: `AnalyticsQueryBuilderTests` (37 — every aggregate function, every filter operator, sorting, Top N/safety cap, every window function, pivot, sanitisation, validation), `WidgetRenderCoordinatorTests` updated (8 — QuerySpec construction, LocalFilters wiring, per-visual-type row limits)

---

## Phase 8 – Cross Filtering ✅

- [x] `IFilterManager` extended — `GetActiveFilters(Guid? excludeSourceWidgetId)` and `ActiveFilterCount` added alongside the original `ApplyFilter`/`ClearAll`/`BuildWhereClause`/`FiltersChanged`
- [x] `CrossFilterManager` (new, `NCVizDash.TaskPane.Services`) — per-field active-filter dictionary; click-to-toggle (clicking the same value from the same widget again clears it); a new widget clicking the same field overwrites the previous source's filter; thread-safe via a simple lock
- [x] `NCVizDash.Core.Analytics.SqlFilterTranslator` (new, shared) — filter→SQL-clause logic extracted from `AnalyticsQueryBuilder` so both the DuckDB query builder and the TaskPane's `CrossFilterManager` use one implementation without `TaskPane` taking a dependency on `NCVizDash.DuckDB`
- [x] `AnalyticsQueryBuilder` refactored to delegate to `SqlFilterTranslator` (no behavioural change; Phase 7 tests pass unmodified)
- [x] `chart-host.html` (Phase 6) — click handling was already wired; `ChartHost`/`WidgetCard` already forwarded `ChartClicked` events; Phase 8 is the first phase to actually *consume* them
- [x] `DashboardCanvas` — new `FilterManager` dependency property; subscribes each `WidgetCard.ChartClicked` to `OnCardChartClicked` (applies a filter on the widget's first dimension field using the clicked category/series name, gated by `IsCrossFilterSource`); subscribes to `IFilterManager.FiltersChanged` and re-renders every widget on the canvas when it fires
- [x] `WidgetRenderCoordinator` — every render now merges the widget's own `LocalFilters` with `IFilterManager.GetActiveFilters(excludeSourceWidgetId: widget.Id)` when `IsCrossFilterTarget` is true; self-exclusion means clicking a bar doesn't filter that same chart down to one bar, so re-clicking a different category still works
- [x] `CanvasPanelViewModel` — exposes `FilterManager` (for XAML binding onto `DashboardCanvas.FilterManager`), `ActiveFilterCount` (observable, kept in sync via `FiltersChanged`), and `ClearFiltersCommand`
- [x] Canvas toolbar — "Clear cross-filters" button with a live badge showing `ActiveFilterCount`
- [x] DI registration: `IFilterManager` → `CrossFilterManager` (singleton, one cross-filter session per open dashboard)
- [x] Unit tests: `CrossFilterManagerTests` (19 — toggle on/off, overwrite-by-different-source, multi-field independence, self-exclusion, `BuildWhereClause`, event-raising semantics including the "no-op doesn't raise" case), `SqlFilterTranslatorTests` (8), `WidgetRenderCoordinatorTests` extended with 3 new cross-filter-merge cases

---

## Phase 9 – Global Filters ✅

> Implemented as a fully **dynamic, data-agnostic** system rather than hardcoded
> Date/Department/Project/Employee/Region/Business-Unit filters — the filter bar
> discovers whatever fields actually exist in the loaded data sources and builds
> the appropriate filter shape per `FieldType`, so it works identically for any
> kind of data the user's workbook happens to contain.

- [x] `Dashboard.GlobalFilters` changed from `Dictionary<string,string>` to `List<WidgetFilter>` — reuses the same shape as `DashboardWidget.LocalFilters`, one JSON representation for both persisted filter lists
- [x] `IGlobalFilterManager` (new Core abstraction) — `SetDashboard`, `GetFilters`/`GetEnabledFilters`, `AddOrUpdateFilter`, `RemoveFilter`, `SetFilterEnabled`, `ClearAll`, `FiltersChanged`
- [x] `GlobalFilterManager` — thin coordinator around the *active dashboard's own* `GlobalFilters` list (no separate store), so filters are naturally persisted whenever the dashboard is saved (Phase 10) with no separate sync step
- [x] `DistinctValueService` (new) — queries distinct values for **any field on any loaded data source** via a `QuerySpec` with no aggregated measure (a GROUP BY naturally yields distinct values); powers a real, data-driven value picker instead of free-text entry
- [x] `GlobalFilterBarViewModel` — discovers every filterable field across every loaded data source (`RefreshAvailableFields`), lets the user add an Equals filter on any Dimension/Time/Filter field (value picked from `DistinctValueService`) or a range filter (Between/GreaterThanOrEqual/LessThanOrEqual) on any Measure field — all generically, no field-name special-casing anywhere
- [x] `GlobalFilterBarView` — horizontal bar above the canvas: active-filter chips (deletable), field/value pickers, Add / Clear All buttons
- [x] `WidgetRenderCoordinator` — merges `IGlobalFilterManager.GetEnabledFilters()` into **every** widget's query unconditionally (no self-exclusion, no `IsCrossFilterTarget` opt-out — global filters apply even to widgets that opted out of cross-filtering)
- [x] `DashboardCanvas` — new `GlobalFilterManager` dependency property; subscribes to `FiltersChanged` and re-renders the whole canvas, reusing the exact same handler as Phase 8's cross-filter wiring
- [x] `CanvasPanelViewModel` — exposes `GlobalFilterManager` and `GlobalFilterBar`; binds the manager to the active dashboard on `OpenDashboard` and on first-drop dashboard auto-creation
- [x] `ShellViewModel.RefreshDataAsync` — refreshes the filter bar's available-fields list after every data reload
- [x] `FilterValuesToStringConverter` (new) — joins a filter's value list for chip tooltips
- [x] DI registrations: `IGlobalFilterManager` → `GlobalFilterManager` (singleton), `DistinctValueService` (singleton), `GlobalFilterBarViewModel` (singleton)
- [x] Unit tests: `GlobalFilterManagerTests` (17), `DistinctValueServiceTests` (7 — exercised against arbitrary, non-business-specific field/table names to prove genericity), `GlobalFilterBarViewModelTests` (14), `WidgetRenderCoordinatorTests` extended with 3 new global-filter-merge cases

---

## Phase 10 – Dashboard Storage ✅

- [x] `WorkbookDashboardRepository` — real implementation using Excel Custom XML Parts (one part per dashboard, JSON-embedded, delete+re-add for updates since Interop has no in-place edit)
- [x] `ShellViewModel` — `NewDashboard`, `LoadSavedDashboardsAsync`, `OpenDashboard`, `SaveDashboardAsync`, `DeleteDashboardAsync` commands, all backed by `IDashboardRepository`
- [x] Ribbon New/Open/Save buttons wired end-to-end (were logging-only stubs since Phase 1)
- [x] `SavedDashboards` observable list for an Open picker UI
- [x] Tests: 6 tests on `ShellViewModelDashboardTests` covering save/open/delete/list-sorting
---

## Phase 11 – Templates ✅

- [x] `TemplateRegistry` — all 10 named templates (Executive, Engineering, Sprint, QA, Finance, HR, Project, PMO, Inventory, Sales), each defined generically as widget "slots" (visual type + measure/dimension counts), never hardcoded field names
- [x] `TemplateInstantiationService` — greedy field-matching from any real data source into template slots; skips a slot gracefully if the data source lacks enough fields of the right type, rather than producing a broken widget
- [x] `ShellViewModel.ApplyTemplateCommand` — instantiates a template against the first loaded (or caller-specified) data source and opens it on the canvas
- [x] Tests: template count, successful instantiation, graceful degradation on insufficient fields
---

## Phase 12 – Advanced Features ✅ (scoped subset — see notes below)

> **Scoped subset**, not the full Phase 12 feature list — see notes per item.

- [x] Undo/Redo — `UndoRedoManager`, snapshot-based (JSON snapshot of the widget list before each mutating operation), wired into Add/Delete/Move/Resize/Duplicate; toolbar buttons enabled and bound (were disabled placeholders since Phase 4)
- [x] Bookmarks — `Bookmark` model + `BookmarkManager` (capture/restore global-filter state under a name)
- [x] Calculated Measures — `CalculatedMeasureSpec` on `QuerySpec`, translated to a raw SQL expression column by `AnalyticsQueryBuilder`, with a minimal keyword-based safety guard (rejects `;`, `--`, `DROP`, etc.)
- [x] Conditional Formatting — `ConditionalFormatRule` model + `DashboardWidget.ConditionalFormatRules`, applied to KPI accent colour in `HtmlBuilder`
- [x] Drill Down — `DrillManager`: push/pop per-widget dimension swap + pinning filter on click
- [ ] Drill Through — **not implemented**; `DrillManager.DrillThrough` throws `NotSupportedException` with an explanation (needs dashboard-to-dashboard linking that doesn't exist in the model yet)
- [ ] Conditional formatting on Table/other chart types — only KPI accent colour is wired; Table cell-level and chart-series-level conditional formatting would need separate builder changes
- [x] Tests: undo/redo restore + can-undo state, bookmark round-trip, calculated-measure SQL shape + safety guard
---

## Phase 13 – Export ✅

- [x] PDF export — `ChartHost.ExportToPdfAsync` via WebView2's native `PrintToPdfAsync`
- [x] PNG export — `ChartHost.ExportToPngAsync`/`CapturePngBytesAsync` via WebView2's `CapturePreviewAsync`
- [x] PowerPoint export — `ExportService.ExportDashboardToPptxAsync`, real OpenXML SDK presentation generation (one slide per widget, title + full-bleed captured image)
- [x] Excel Snapshot export — `ExcelSnapshotExporter` (lives in `NCVizDash.ExcelAddIn` since it needs Interop), pastes captured widget images onto a new worksheet
- [x] `WidgetCard` exposes `ExportToPdfAsync`/`ExportToPngAsync`/`CapturePngBytesAsync`, delegating to its `ChartHost`
- [ ] Ribbon export buttons (`btnExportPdf` etc., defined since Phase 1) are not yet wired to `ExportService` — the export pipeline is complete and callable but has no UI trigger yet
---

## Phase 14 – Data Connectors ✅ (SharePoint stubbed — see notes)

> SharePoint is explicitly **not implemented** — see notes below.

- [x] `IDataConnector` (new Core abstraction) — same `DataSourceDescriptor` + rows shape as `IExcelDataReader`, so every downstream consumer works unchanged regardless of data origin
- [x] CSV — `CsvFileConnector`, real RFC-4180-ish parser (quoted fields, embedded commas/newlines, escaped quotes) — no external CSV library dependency
- [x] JSON — `JsonFileConnector`, flattens a top-level array of objects; nested structures preserved as raw JSON text rather than expanded
- [x] SQL Server — `SqlServerConnector`, real ADO.NET via `Microsoft.Data.SqlClient`, works against any reachable SQL Server given a connection string + query
- [x] REST API — `RestApiConnector`, generic JSON-over-HTTP via `HttpClient`, auto-detects `{"data":[...]}`-style envelope shapes
- [ ] Oracle / PostgreSQL / MySQL / SQLite — **not implemented**; would each need their own ADO.NET provider package (`Oracle.ManagedDataAccess`, `Npgsql`, `MySqlConnector`, `Microsoft.Data.Sqlite`) but follow the exact same pattern as `SqlServerConnector`
- [ ] SharePoint — **not implemented**; `SharePointListConnector` exists and is registered but every method throws `NotSupportedException` with an explanation (needs OAuth 2.0 / MSAL, out of scope for this pass)
- [x] Tests: CSV round-trip (including quoted-comma handling), JSON round-trip, SharePoint stub explicitly throws
---

## Phase 15 – Collaboration ✅

- [x] Comments — `WidgetComment` model + `DashboardWidget.Comments` (persisted as part of the dashboard's normal JSON — Phase 10's storage covers this automatically)
- [x] Dashboard sharing — `DashboardShareService.ExportToFileAsync`/`ImportFromFileAsync`, file-based (`.json`), imported dashboards get a fresh ID and a `SharedBy` attribution
- [x] Version history — `DashboardShareService.CaptureVersion`/`GetVersionTimestamps`/`RestoreVersion`, capped at 20 versions per dashboard
- [x] Read-only mode — `Dashboard.IsReadOnly`, enforced as a guard at the top of every mutating `CanvasPanelViewModel` operation (Add/Delete/Move/Resize)
- [ ] Version history persistence — **in-memory only**, does not survive add-in restart; Phase 10's Custom-XML-Part storage would need to be extended to include version snapshots for durability
- [x] Tests: export/import round-trip with fresh ID + attribution, version capture/restore, read-only guard blocks AddWidget
---

## Phase 16 – Performance ✅ (partial — see notes)

> **Partial** — the two most impactful items (query caching, parallel rendering) are implemented; UI virtualisation and background/progressive loading are not.

- [x] Query result caching — `CachingAnalyticsEngine`, a decorator over `IAnalyticsEngine` (SHA-256 hash of the serialized `QuerySpec` as cache key, 15s default TTL, wholesale invalidation on any data load/unload for correctness)
- [x] Parallel queries — `DashboardCanvas.RenderAllWidgetsAsync`, `SemaphoreSlim`-throttled concurrent rendering (default max 4 in-flight) so a many-widget dashboard doesn't serialise its refresh
- [ ] UI virtualisation for the widget canvas — **not implemented**; all widgets render simultaneously regardless of viewport
- [ ] Background/progressive loading — **not implemented** beyond the existing async pipeline; no incremental/streaming row loading for very large data sources
- [ ] 1M+ row benchmark suite — **not implemented**
- [x] Tests: cache hit avoids redundant inner query, cache invalidates on data load
---

## Phase 17 – Plugin SDK ✅

- [x] `IChartPlugin`, `IWidgetPlugin`, `IDataSourcePlugin`, `IThemePlugin` (new `Core.Abstractions.Plugins` namespace) — `IChartPlugin` returns the exact same ECharts-option/HTML-envelope JSON shape the built-in chart engine produces, so a custom chart plugs into the existing WebView2 pipeline unmodified
- [x] `PluginLoader` — scans `AppSettings.PluginDirectory` for `.dll` files, loads each into its own collectible `AssemblyLoadContext` (isolation — a broken plugin can't corrupt the host's loaded types), discovers implementing types via reflection, instantiates via parameterless constructor
- [ ] Sample plugin project — **not created**; the SDK (interfaces + loader) is complete but there's no example third-party plugin demonstrating it end-to-end
- [ ] Plugin unload — the `AssemblyLoadContext`s are collectible but nothing currently calls `Unload()`; the isolation is in place for future hot-reload support, not wired up yet
---

## Phase 18 – Optional AI ✅ (AI strictly opt-in, disabled by default)

> AI is **strictly opt-in** per the product vision — `AppSettings.AiEnabled` defaults to `false`, and `AiFeatureGate` is the single enforcement point every future AI-triggering UI action must go through.

- [x] `IAiProvider` (new Core abstraction) — `ExplainChartAsync`, `SuggestWidgetsAsync`, `GenerateInsightsAsync`, `ForecastAsync`
- [x] `AiFeatureGate` — the one place that decides whether AI is allowed to run (`AiEnabled == true` AND a known provider configured); returns `null` otherwise rather than a caller being able to bypass the check
- [x] Azure OpenAI, OpenAI, Local LLM — `OpenAiCompatibleProvider` base class (shared Chat Completions request shape) with three thin subclasses differing only in endpoint/auth
- [x] Anthropic — separate implementation (`AnthropicProvider`) since the Messages API has a different request shape
- [x] Forecasting — deterministic linear-trend extrapolation, **not an LLM call** (numeric extrapolation is a better fit for simple regression than a chat prompt, and it's free/instant/reliable)
- [ ] `SuggestWidgetsAsync` — **not implemented**; every provider returns an empty list with a logged warning. Turning free-text into safe, schema-valid widget definitions needs strict output validation against the real data source's actual fields, which is a correctness-critical feature deserving its own dedicated pass rather than a quick JSON-parsing bolt-on
- [ ] No AI-triggering UI exists yet (no "Explain Chart" button, no NL prompt box) — the provider layer and the feature gate are complete and testable, but nothing in the shipped UI calls them yet
- [x] Tests: gate blocks by default, gate allows when explicitly enabled + configured, forecast uses linear trend with no network call

---

## v2.0 – Productivity Features (One-Click Generator, Templates+, Story Mode, Live Refresh, Jira) ✅

> Delivered as a follow-on batch after Phases 1–18, per the "NC VizDash v2.0" prompt.
> Same honesty standard as the Phase 10–18 batch: real, working implementations
> with focused test coverage, gaps called out explicitly rather than glossed over.

### Feature 1 — One-Click Dashboard Generator
- [x] `OneClickDashboardGenerator` — scans a data source's actual fields and deterministically builds: KPI cards (≤4), monthly trend (if a time field exists), category-analysis bar chart, Top 10 + Bottom 10 (real `TopN`/`TopNDescending` support added to `DashboardWidget` and honoured by `WidgetRenderCoordinator`/`AnalyticsQueryBuilder`, not just a label), pie chart, summary table — responsive left-to-right wrapping grid layout, no AI, no configuration
- [x] `ShellViewModel.GenerateDashboardCommand` — one command, wired to the first loaded (or caller-specified) data source
- [x] Tests: all-sections-present, Top/Bottom sort direction correctness, graceful degradation with no time field / no measures, KPI cap enforcement

### Feature 2 — Dashboard Templates
- [x] Added **Delivery Dashboard** (was the one template from the v2.0 list missing from Phase 11's set) — now 11 templates total
- [x] `TemplateInstantiationService.InstantiateWithReport` — new overload returning `TemplateInstantiationResult` (dashboard + the list of slots that couldn't be auto-filled), satisfying "prompt the user only for missing fields, never ask unnecessary questions" — the *hook* for a picker UI to ask targeted questions; no picker UI was built (out of scope for this pass, matching Phase 11's original UI-light scope)
- [x] Tests: template count/presence, unfilled-slot reporting on a sparse source, `IsComplete` on a fully-matched source

### Feature 3 — Dashboard Story Mode
- [x] `PresentationController` — Next/Previous (both wrap around), Play/Stop auto-advance (thread-pool timer marshalled onto the WPF dispatcher — a real thread-affinity bug caught and fixed during implementation, not shipped broken), built on Phase 12's `Bookmark`/`BookmarkManager` rather than inventing a parallel "slide" concept
- [x] `PresentationWindow` — full-screen, no window chrome, re-parents the **live** `DashboardCanvas` (not a second copy) so the presentation always reflects real widget data; cross-fade transition on page change; minimal auto-hiding-style nav bar; Esc/Space/←/→ keyboard shortcuts
- [x] "Present" button added to the canvas toolbar, wired through `CanvasPanelViewModel.Presentation`
- [ ] Auto-hide of the nav bar on mouse idle — **not implemented**; the bar is always visible rather than fading out during playback
- [x] Tests: page load/activate, wrap-around in both directions, stop clears state, zero-bookmark presentation doesn't crash

### Feature 4 — Live Refresh
- [x] **Genuinely selective**, not a relabelled full refresh: `ExplorerPanelViewModel.RefreshSheetAsync(sheetName)` reloads only the data source(s) on the changed sheet, preserving `DataSourceDescriptor` identity so widget bindings never break
- [x] `CanvasPanelViewModel.DataSourceRefreshed` event + `DashboardCanvas.OnDataSourceRefreshed` — re-renders only the widget cards bound to the refreshed data source, leaving every other widget's current render completely untouched
- [x] `ThisAddIn.OnSheetChange` rewritten to call the selective path instead of `ExplorerPanel.LoadDataSourcesAsync()`'s full workbook rescan
- [x] Filters, layout, and selection are preserved automatically — nothing about the dashboard/widget structure is touched, only the underlying rows for the affected source
- [x] Tests: selective refresh only touches the matching sheet, no-match returns empty, event propagation

### Feature 5 — Jira Enterprise Connector
- [x] `JiraConnectionProfile` + `JiraConnectionProfileStore` — Connection Name/URL/Email/API Token, saved to `%LOCALAPPDATA%\NCVizDash\jira-connections.json` (same "reasonable placeholder, not production secret storage" caveat as Phase 18's AI API key — an OS credential vault is the real answer before shipping to non-developer users)
- [x] `JiraConnector` — real Jira Cloud REST API v2 (`/rest/api/2/search` with JQL, `/rest/api/2/myself` for connection testing), HTTP Basic auth (email + API token), automatic pagination up to the requested row count, issue-field flattening (nested objects like `assignee`/`priority` reduced to their display name)
- [x] Implements `IDataConnector` — **the same interface every other connector satisfies**, so once imported, a Jira dataset is structurally indistinguishable from an Excel one anywhere downstream (rule engine, chart engine, cross-filtering, global filters, Top N, calculated measures — all "just work" with zero Jira-specific code in any of those layers)
- [x] `JqlEditorViewModel` — connection management (save/test), JQL validation (delegates to Jira itself via a `maxResults=0` query rather than reimplementing JQL grammar), 100-row preview with column/type/count display, favourite-query saving, Import as New/Replace/Append
- [ ] Import → **Append** falls back to Replace semantics with a logged warning — true row-appending isn't exposed by `IAnalyticsEngine` today (`LoadDataSourceAsync` always replaces the backing table); adding an `AppendRowsAsync` method would be the real fix
- [ ] JQL syntax highlighting / auto-complete — **not implemented** (both were marked optional/future in the spec itself)
- [ ] OAuth authentication — **not implemented**; API Token (Basic auth) is the only supported credential type, same reasoning as SharePoint's OAuth gap in Phase 14
- [ ] No ribbon/UI entry point wired to open the JQL editor yet — the ViewModel and connector are complete and independently testable, but nothing in the shipped ribbon launches it
- [x] Tests: connection test success/failure, JQL validation success/failure with real Jira-shaped error extraction, issue-field flattening (including nested-object reduction), connection-info parsing, profile round-trip — all against a mocked `HttpMessageHandler`, no live network access
