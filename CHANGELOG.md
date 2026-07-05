# NC VizDash – Changelog

All notable changes are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

## [0.11.0] – v2.0 Productivity Features – 2026-07-03

> Follow-on batch after Phases 1–18: One-Click Dashboard Generator, expanded
> Templates, Story Mode, Live Refresh, and a Jira Enterprise Connector with
> Dynamic JQL Editor. Same scope-honesty standard as the Phase 10–18 batch.

### Added

**Feature 1 — One-Click Dashboard Generator**
- `OneClickDashboardGenerator` — deterministic, field-composition-driven dashboard builder: KPI cards, monthly trend, category analysis, Top 10, Bottom 10, pie, summary table, all in a responsive wrapping grid
- `DashboardWidget.TopN`/`TopNDescending` — new, real fields (not a UI label) honoured end-to-end by `WidgetRenderCoordinator` → `QuerySpec.SortField`/`SortDescending`/`Limit` → `AnalyticsQueryBuilder`
- `ShellViewModel.GenerateDashboardCommand`

**Feature 2 — Dashboard Templates**
- Delivery Dashboard template added (11 templates total, matching the v2.0 list exactly)
- `TemplateInstantiationService.InstantiateWithReport` / `TemplateInstantiationResult` — reports exactly which slots couldn't be auto-filled, the hook for a future "ask only what's missing" picker UI

**Feature 3 — Dashboard Story Mode**
- `PresentationController` — bookmark-sequence-driven presentation state (Next/Previous with wrap-around, Play/Stop auto-advance). The auto-play timer callback is explicitly marshalled onto the WPF dispatcher (`Application.Current.Dispatcher.Invoke`) since `System.Threading.Timer` fires on a thread-pool thread and `ObservableProperty` changes bound to UI must run on the UI thread — this was caught and fixed during implementation rather than left as a latent crash
- `PresentationWindow` — full-screen (`WindowState=Maximized`, `WindowStyle=None`), re-parents the **live** `DashboardCanvas` rather than creating a duplicate, cross-fade transition between bookmark pages, minimal nav bar, Esc/Space/←/→ shortcuts
- "Present" button on the canvas toolbar

**Feature 4 — Live Refresh**
- `ExplorerPanelViewModel.RefreshSheetAsync(sheetName)` — reloads only the data source(s) on the changed sheet, preserving `DataSourceDescriptor` object identity/ID so existing widget bindings never break
- `CanvasPanelViewModel.DataSourceRefreshed` event + `DashboardCanvas.OnDataSourceRefreshed` — re-renders only the affected widgets, leaving everything else's current render untouched (filters/layout/selection preserved automatically since nothing about dashboard structure changes)
- `ThisAddIn.OnSheetChange` rewritten to call the selective path — this replaces what was previously a full `ExplorerPanel.LoadDataSourcesAsync()` workbook rescan on every debounced sheet change

**Feature 5 — Jira Enterprise Connector**
- `JiraConnectionProfile` + `JiraConnectionProfileStore` — Connection Name/URL/Email/API Token, JSON file storage
- `JiraConnector` (implements `IDataConnector`) — real Jira Cloud REST API v2 (`/rest/api/2/search` JQL execution with pagination, `/rest/api/2/myself` connection testing), HTTP Basic auth, issue-field flattening
- `JqlEditorViewModel` — connection management, JQL validation (delegates to Jira's own query engine via `maxResults=0` rather than reimplementing JQL grammar), 100-row preview, favourite queries, Import as New/Replace/Append

### Architecture decisions
- **`JiraConnector` implements the exact same `IDataConnector` contract as every Phase 14 connector** — this is what makes "Excel and Jira should not be distinguished" true structurally rather than by convention: the rule engine, chart engine, cross-filtering, global filters, Top N, and calculated measures all already work with any `IDataConnector`-sourced dataset with zero Jira-specific branches anywhere in those layers
- **JQL validation calls Jira itself with `maxResults=0`** rather than a client-side JQL parser — Jira is definitionally the correct JQL validator; reimplementing its grammar would be both more code and less accurate
- **Story Mode re-parents the live canvas instead of creating a presentation-specific copy** — guarantees the presentation can never show stale data relative to the editing view, at the cost of the canvas being briefly absent from the editing view while presenting (acceptable — you can't edit and present simultaneously anyway)
- **Live Refresh's selectivity is enforced at two levels** — data reload is scoped to the changed sheet (`RefreshSheetAsync`), and widget re-render is further scoped to only the widgets bound to that specific data source (`DataSourceRefreshed` event) — both levels matter: without the second, a one-row change to a small lookup table would still force every widget on a large dashboard to re-render

### Tests
`V2ProductivityFeaturesTests.cs` (20 — generator section coverage, template unfilled-slot reporting, presentation wrap-around/lifecycle, selective refresh scoping) and `JiraConnectorTests.cs` (9 — connection test success/failure, JQL validation with real Jira-shaped error extraction, issue flattening including nested-object reduction, connection-info parsing, profile round-trip), all against mocked HTTP — no live network access exercised.

---

## [0.10.0] – Phases 10–18 – Dashboard Storage through Optional AI – 2026-07-03

> **Scope note:** Phases 1–9 were built incrementally with exhaustive test suites
> (20–50 tests per phase) and full architectural review at each step. Phases 10–18
> were delivered together in a single consolidated pass at the user's request,
> which necessarily means lighter test coverage (a handful of representative smoke
> tests per feature rather than exhaustive per-case coverage) and several explicitly
> scoped-down or stubbed items. Every gap is called out below and in `TASKS.md`
> rather than glossed over — **honesty about what's real vs. stubbed matters more
> here than in any other phase**, since this is the batch most likely to contain
> something that looks done but isn't.

### Phase 10 — Dashboard Storage
- `WorkbookDashboardRepository` — real implementation (was a stub since Phase 1): stores each dashboard as an Excel Custom XML Part, JSON-serialized `Dashboard` embedded as the part's text content, keyed by a `dashboard-id` attribute on the root element for lookup/update/delete
- `ShellViewModel` gained the full dashboard lifecycle: `NewDashboard`, `LoadSavedDashboardsAsync`, `OpenDashboard`, `SaveDashboardAsync`, `DeleteDashboardAsync`
- Ribbon New/Open/Save buttons — logging-only stubs since Phase 1 — now fire real events wired through to the above commands

### Phase 11 — Templates
- `TemplateRegistry` — all 10 named templates from the spec, each defined as generic widget "slots" (visual type + measure/dimension *counts*, never real field names)
- `TemplateInstantiationService` — greedy matching of template slots against whatever fields a real data source actually has; a slot with insufficient matching fields is skipped rather than producing a broken widget

### Phase 12 — Advanced Features (scoped subset)
- Undo/Redo (`UndoRedoManager`, snapshot-based, wired into every mutating canvas operation; toolbar buttons — disabled placeholders since Phase 4 — now live)
- Bookmarks (`Bookmark` + `BookmarkManager` — captures/restores global filter state)
- Calculated Measures (`CalculatedMeasureSpec` on `QuerySpec`, with a keyword-based SQL injection guard)
- Conditional Formatting (KPI accent colour only — `ConditionalFormatRule`)
- Drill Down (`DrillManager` — per-widget dimension swap + pinning filter)
- **Not implemented:** Drill Through (throws `NotSupportedException` with explanation — needs dashboard-linking that doesn't exist in the model), Table/chart-level conditional formatting

### Phase 13 — Export
- PDF and PNG export via WebView2's native `PrintToPdfAsync`/`CapturePreviewAsync` (`ChartHost`, `WidgetCard`)
- PowerPoint export — genuine OpenXML SDK presentation generation (`ExportService`), one slide per widget
- Excel Snapshot export (`ExcelSnapshotExporter`, in `ExcelAddIn` since it needs Interop)
- **Not implemented:** ribbon export buttons aren't wired to `ExportService` yet — the pipeline works, but there's no UI trigger

### Phase 14 — Data Connectors
- `IDataConnector` — new Core abstraction, same `DataSourceDescriptor`+rows shape as `IExcelDataReader`
- CSV (`CsvFileConnector`) — real hand-written parser (quoted fields, embedded commas/newlines, escaped quotes), no external CSV library
- JSON (`JsonFileConnector`) — flattens a top-level array of objects
- SQL Server (`SqlServerConnector`) — real ADO.NET via `Microsoft.Data.SqlClient`
- REST API (`RestApiConnector`) — generic JSON-over-HTTP, auto-detects common envelope shapes
- **Not implemented:** Oracle/PostgreSQL/MySQL/SQLite (would follow the exact `SqlServerConnector` pattern with a different ADO.NET provider); SharePoint (`SharePointListConnector` exists but every method throws `NotSupportedException` — needs OAuth 2.0/MSAL, a meaningfully larger scope than the other connectors)

### Phase 15 — Collaboration
- Comments (`WidgetComment` + `DashboardWidget.Comments`, persisted automatically via Phase 10's storage)
- Sharing (`DashboardShareService` export/import to `.json`, fresh ID + `SharedBy` attribution on import)
- Version history (`DashboardShareService.CaptureVersion`/`RestoreVersion`, capped at 20 — **in-memory only, not yet durable across add-in restarts**)
- Read-only mode (`Dashboard.IsReadOnly`, enforced as a guard in every mutating `CanvasPanelViewModel` command)

### Phase 16 — Performance (partial)
- Query result caching (`CachingAnalyticsEngine`, a decorator over `IAnalyticsEngine`; SHA-256-hashed `QuerySpec` as cache key, 15s TTL, invalidated wholesale on any data load/unload)
- Parallel widget rendering (`DashboardCanvas.RenderAllWidgetsAsync`, `SemaphoreSlim`-throttled)
- **Not implemented:** UI virtualisation, background/progressive loading, 1M+ row benchmarks

### Phase 17 — Plugin SDK
- `IChartPlugin`/`IWidgetPlugin`/`IDataSourcePlugin`/`IThemePlugin` (new `Core.Abstractions.Plugins`) — `IChartPlugin` returns the same JSON envelope shape the built-in engine produces, so custom charts need zero host changes to render
- `PluginLoader` — scans `AppSettings.PluginDirectory`, loads each DLL into its own collectible `AssemblyLoadContext` for isolation, discovers plugin types via reflection
- **Not implemented:** sample plugin project, plugin unloading (contexts are collectible but nothing calls `Unload()` yet)

### Phase 18 — Optional AI
- `IAiProvider` + `AiFeatureGate` — the single enforcement point for "AI must always remain optional"; `AppSettings.AiEnabled` defaults to `false`, and the gate returns `null` unless explicitly enabled AND a configured provider exists
- Azure OpenAI, OpenAI, Local LLM (shared `OpenAiCompatibleProvider` base — same Chat Completions shape); Anthropic (separate implementation — different Messages API shape)
- Forecasting uses deterministic linear-trend regression, **not an LLM call** — better fit for numeric extrapolation, free, instant, reliable
- **Not implemented:** `SuggestWidgetsAsync` (every provider returns empty + logs a warning — turning free text into safe widget definitions needs output-schema validation deserving its own pass); no AI-triggering UI exists yet (no "Explain Chart" button)

### New projects
- `NCVizDash.Connectors` (Phase 14) — added to the solution and `.sln`

### Cross-cutting changes
- `Dashboard.GlobalFilters` — no change from Phase 9 (already `List<WidgetFilter>`)
- `DashboardWidget` gained `ConditionalFormatRules` (Phase 12) and `Comments` (Phase 15)
- `Dashboard` gained `IsReadOnly` and `SharedBy` (Phase 15)
- `QuerySpec` gained `CalculatedMeasures` (Phase 12)
- `AppSettings` gained `AiEnabled`/`AiProvider`/`AiEndpoint`/`AiApiKey` (Phase 18), all defaulting to off/empty
- `IAnalyticsEngine`'s DI registration now wraps the concrete `DuckDbAnalyticsEngine` in `CachingAnalyticsEngine` (Phase 16) — transparent to every existing caller since both implement the same interface

### Tests
One consolidated `Phase10Through18Tests.cs` (24 tests) covering the representative path through every phase above, plus 6 tests in `ShellViewModelDashboardTests.cs` for Phase 10's dashboard lifecycle. This is deliberately lighter than the 20–50-tests-per-phase depth of Phases 1–9 — see the scope note at the top of this entry.

### Architecture decisions
- **Every "not implemented" item throws or logs a clear explanation rather than silently no-op'ing** (`SharePointListConnector`, `DrillManager.DrillThrough`, AI's `SuggestWidgetsAsync`) — a caller should never be able to mistake "not built yet" for "ran successfully and did nothing"
- **`IAiProvider` has zero callers in the shipped UI** — the interface, gate, and four provider implementations are complete and independently testable, but wiring an actual "Explain this chart" button was left undone rather than rushed, since a half-wired AI entry point is worse than a well-built one with no button yet
- **Query caching lives as a decorator, not inside `DuckDbAnalyticsEngine` itself** — keeps the caching concern fully separable (and skippable, by just not registering it) from the query-execution concern

---

## [0.9.0] – Phase 9 – Global Filters – 2026-07-03

### Added

> Built as a **fully dynamic, data-agnostic** filter system rather than the six
> hardcoded field names (Date/Department/Project/Employee/Region/Business Unit)
> from the original spec — the filter bar discovers whatever fields actually exist
> in the loaded data and adapts its filter UI to each field's `FieldType`, so it
> works unmodified for any workbook, in any business domain.

**Model**
- `Dashboard.GlobalFilters` — type changed from `Dictionary<string, string>` to `List<WidgetFilter>`, matching `DashboardWidget.LocalFilters`'s shape exactly. No production code referenced the old shape yet, so this was a clean, non-breaking change made ahead of Phase 10 persistence

**Manager**
- `IGlobalFilterManager` (new `Core.Abstractions`) + `GlobalFilterManager` (new `TaskPane.Services`) — deliberately does **not** maintain its own filter store; it mutates `ActiveDashboard.GlobalFilters` directly, so global filters are automatically included whenever a dashboard is saved without any separate persistence wiring. `SetDashboard` re-binds on dashboard open/switch and raises `FiltersChanged` so the UI picks up the new dashboard's existing filters immediately

**Dynamic field & value discovery**
- `DistinctValueService` (new) — the piece that makes this "for any kind of data": given *any* `dataSourceId` + `fieldName`, it builds a `QuerySpec` with that field as the sole dimension and no aggregated measure, which naturally yields distinct values via GROUP BY — no separate "SELECT DISTINCT" code path, no field-name allowlist. Deduplicates case-insensitively, strips null/empty, sorts, caps at 200 values, never throws (empty list on any failure)
- `GlobalFilterBarViewModel` — `RefreshAvailableFields(IEnumerable<DataSourceDescriptor>)` flattens every visible field from every loaded source into a flat, filterable list; `AddSelectedFilter` builds an `Equals` filter for Dimension/Time/Filter fields using a value picked from `DistinctValueService`; `AddRangeFilter` builds `Between`/`GreaterThanOrEqual`/`LessThanOrEqual` for Measure fields depending on which bound(s) are supplied — both paths are entirely generic, keyed only off `FieldType`, never a specific field name

**UI**
- `GlobalFilterBarView` (new) — horizontal bar docked above the canvas: deletable chips for each active filter (tooltip shows the full value list via the new `FilterValuesToStringConverter`), a field ComboBox, a value ComboBox (populated live via `DistinctValueService` on selection), Add and Clear All buttons
- `CanvasPanelView.xaml` — new `Auto`-height row hosts the filter bar between the dashboard name bar and the canvas scroll host

**Wiring**
- `WidgetRenderCoordinator.BuildQuerySpec` — now merges `_globalFilterManager.GetEnabledFilters()` into every widget's filter list **unconditionally**, before the cross-filter merge and regardless of `IsCrossFilterTarget`. This is the key behavioural difference from Phase 8: a widget can opt out of *cross*-filtering (so clicking other charts doesn't affect it) while still respecting the dashboard's *global* filters
- `DashboardCanvas` — new `GlobalFilterManager` dependency property, wired to the exact same `OnFiltersChanged` full-canvas-re-render handler already used for cross-filters — one re-render code path serves both filter systems
- `CanvasPanelViewModel.OpenDashboard` and the first-drop dashboard auto-creation path both call `GlobalFilterManager.SetDashboard(...)`, so the filter bar is always bound to whichever dashboard is actually on screen
- `ShellViewModel.RefreshDataAsync` calls `CanvasPanel.GlobalFilterBar.RefreshAvailableFields(ExplorerPanel.DataSources)` after every reload, so newly-discovered fields (a second table added to the workbook, for instance) show up in the picker without any manual refresh step

### Tests
`GlobalFilterManagerTests` (17 — dashboard binding/switching, add/update/remove/enable-disable, `GetEnabledFilters` vs `GetFilters`, event-raising no-op cases, arbitrary field-name genericity), `DistinctValueServiceTests` (7 — run against deliberately made-up field/table names like "warehouse_zone" and "widgets_inventory" rather than the six spec field names, to prove no hardcoded assumptions), `GlobalFilterBarViewModelTests` (14 — multi-source field aggregation, hidden-field exclusion, generic Equals-filter construction, all three range-filter shapes), `WidgetRenderCoordinatorTests` (+3 — unconditional merge, merge-even-when-not-a-cross-filter-target, no-filters no-op)

### Architecture decisions
- **No hardcoded field list anywhere in the global filter system** — this was the core design choice for Phase 9. The literal spec called out Date/Department/Project/Employee/Region/Business Unit, but baking those names into the code would make the filter bar useless for a workbook about, say, manufacturing defects or student grades. `GlobalFilterFieldOption` + `RefreshAvailableFields` + `DistinctValueService` together mean the exact same code path serves any domain
- **`GlobalFilterManager` has no independent state** — it's a coordinator over `Dashboard.GlobalFilters`, not a parallel store that would need syncing. This was the natural consequence of making `GlobalFilters` a `List<WidgetFilter>` rather than inventing a separate global-filter type
- **Global filters skip both the self-exclusion and the `IsCrossFilterTarget` opt-out that cross-filters respect** — by design, per the "dashboard-wide filtering" framing: global filters are an explicit, deliberate user action (not an incidental side-effect of clicking a chart), so there's no scenario where a widget should silently ignore one

---

## [0.8.0] – Phase 8 – Cross Filtering – 2026-07-02

### Added

**Shared filter-to-SQL logic**
- `NCVizDash.Core.Analytics.SqlFilterTranslator` — extracted from Phase 7's `AnalyticsQueryBuilder` so the DuckDB engine and the TaskPane's new `CrossFilterManager` share one WHERE-clause implementation without `TaskPane` depending on `NCVizDash.DuckDB`. `AnalyticsQueryBuilder.BuildFilterClauses` now simply delegates; identical SQL output, verified by the unchanged Phase 7 test suite passing without modification

**Filter manager**
- `IFilterManager` extended with `GetActiveFilters(Guid? excludeSourceWidgetId = null)` and `ActiveFilterCount`
- `CrossFilterManager` (new) — one instance per open dashboard session (DI singleton). Active filters are keyed by field name:
  - Clicking a data point applies `{widgetId, field, [value]}`
  - Clicking the *same* value from the *same* widget again removes it (click-to-deselect)
  - A different widget clicking the same field simply overwrites the previous filter for that field (last click wins per field)
  - `ClearAll()` and every mutating `ApplyFilter` call only raise `FiltersChanged` when something actually changed, so a no-op clear-of-nothing doesn't trigger an unnecessary dashboard-wide re-render

**Wiring: click → filter → re-render**
- `DashboardCanvas` gained a `FilterManager` dependency property. Every `WidgetCard` created in `RebuildChildren` has its `ChartClicked` event wired to `OnCardChartClicked`, which calls `FilterManager.ApplyFilter(widget.Id, firstDimensionField, [clickedValue])` — gated by `DashboardWidget.IsCrossFilterSource`. Subscribing to `IFilterManager.FiltersChanged` re-renders every widget on the canvas (not just cross-filter targets, so a widget that just toggled itself out of filtering shows its unfiltered state immediately)
- `WidgetRenderCoordinator.BuildQuerySpec` now merges `widget.LocalFilters` with `_filterManager.GetActiveFilters(excludeSourceWidgetId: widget.Id)` whenever `widget.IsCrossFilterTarget` is true. The self-exclusion is the detail that makes cross-filtering usable: without it, clicking a bar in a Bar chart would immediately filter that same chart down to a single bar, making it impossible to click a different category afterwards

**UI**
- `CanvasPanelViewModel` — `FilterManager` (bound onto `DashboardCanvas.FilterManager`), `ActiveFilterCount` (observable, updated via a `FiltersChanged` subscription), `ClearFiltersCommand`
- Canvas toolbar — new "Clear cross-filters" icon button with a live `md:Badge` showing the active-filter count, positioned between the Delete/Duplicate group and the selection-info label

### Tests
`CrossFilterManagerTests` (19 — every toggle/overwrite/multi-field/self-exclusion combination, `BuildWhereClause` shape, and the two "should NOT raise `FiltersChanged`" no-op cases), `SqlFilterTranslatorTests` (8), `WidgetRenderCoordinatorTests` (+3 new — cross-filter target merges active filters, non-target ignores them entirely and never even calls `GetActiveFilters`, self-exclusion verified via `Times.Once` on the correct widget ID)

### Architecture decisions
- **Self-exclusion is enforced at the query layer (`WidgetRenderCoordinator`), not the filter-manager layer** — `CrossFilterManager` always stores the true click source; each *consumer* decides whether to see its own filter. This keeps `GetActiveFilters` reusable for a future "active filters" debug/summary panel that would want to see everything, unfiltered
- **`FiltersChanged` triggers a full-canvas re-render, not a per-target one** — simpler and correct: Phase 8 has no dependency graph to walk (every widget potentially reads every field), and DuckDB queries are fast enough in-memory that re-rendering the whole dashboard on every click is not a performance concern at this scale (revisited if Phase 16 profiling says otherwise)
- **One filter per field, last-click-wins, not an accumulating multi-select** — matches the literal "click visual → update all visuals" framing; a richer multi-select (Ctrl+click to add multiple values to one field's filter) is a natural but explicitly deferred enhancement, since `ApplyFilter`'s `IReadOnlyList<object?> selectedValues` parameter already supports it without an interface change

---

### Added

**Query specification (engine-agnostic)**
- `NCVizDash.Core.Analytics.QuerySpec` — `TableName`, `Dimensions`, `Measures` (each with its own `AggregateFunction`), `Filters` (reuses Phase 4's `WidgetFilter`/`FilterOperator`), `SortField`/`SortDescending`, `Limit`, optional `WindowFunction`, optional `Pivot`
- `AggregateFunction` — Sum, Count, CountDistinct, Avg, Min, Max, and `None` (raw/unaggregated rows, used by Scatter/Bubble which plot individual points rather than per-category sums)
- `WindowFunctionSpec` — `Type` (RowNumber, Rank, DenseRank, RunningTotal, MovingAverage, PercentOfTotal), `OrderByField`, optional `PartitionByFields`, `WindowSize` (for moving average), output `Alias`
- `PivotSpec` — `PivotField` (becomes new columns), `ValueField`, `Aggregate`

**Query builder**
- `NCVizDash.DuckDB.AnalyticsQueryBuilder` — pure `static` translator from `QuerySpec` to DuckDB SQL, zero side effects, fully testable without a live connection:
  - Aggregate queries: `SELECT dims, AGG(measure) AS alias ... GROUP BY dims` (GROUP BY omitted when there are no aggregated measures, so raw-row Scatter/Bubble queries aren't accidentally collapsed)
  - Filters: every `FilterOperator` translated to a WHERE fragment — `Contains` uses `ILIKE '%...%'`, `In`/`NotIn` expand to `IN (...)`/`NOT IN (...)`, `Between` requires exactly 2 values or is silently skipped; disabled filters are excluded; multiple filters join with `AND`
  - Sorting: explicit `SortField` or falls back to the first dimension, always ascending unless `SortDescending`
  - Top N: `Limit` is honoured up to a hard 5000-row safety cap (`DefaultSafetyLimit`) regardless of what's requested — protects the WebView2 host from being handed an unbounded result set
  - Window functions: each `WindowFunctionType` maps to its DuckDB `OVER (...)` expression; `PARTITION BY` is prepended when `PartitionByFields` is non-empty; `RunningTotal` uses `ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW`, `MovingAverage` uses a configurable trailing window
  - Pivot: leans on DuckDB's native `PIVOT table ON col USING agg(val) GROUP BY dims` syntax rather than hand-rolled conditional aggregation; documented limitation that inline `WHERE` isn't supported before a `PIVOT`'s `GROUP BY` in DuckDB's grammar (filtering pivot-backed widgets is deferred to Phase 9's global filter manager)
  - All identifiers re-sanitised with the exact same algorithm as `DuckDbAnalyticsEngine`'s load-time column sanitisation, so widget field names (captured from raw Excel headers) always resolve correctly
  - All literal values safely escaped: numeric strings pass through unquoted, everything else is single-quoted with embedded `'` doubled

**Engine integration**
- `IAnalyticsEngine.QueryAsync(QuerySpec, CancellationToken)` — new structured overload alongside the existing raw-SQL `QueryAsync(string, CancellationToken)`; `DuckDbAnalyticsEngine` implements it by delegating to `AnalyticsQueryBuilder.Build` then the existing string-SQL path
- `IAnalyticsEngine.GetTableName` — was added ad-hoc in Phase 6 for the coordinator; now formally part of the Phase 7 structured-query pipeline

**Coordinator rewrite**
- `WidgetRenderCoordinator` — no longer hand-builds SQL strings (removing the duplicate, less-capable query logic introduced as a Phase 6 stopgap); now constructs a `QuerySpec` from the widget's field mappings and **`LocalFilters`** (only enabled ones), closing the loop on the architectural correction made ahead of Phase 4 — widget-level filters set by the user now actually reach the database. Table-visual widgets get a smaller 200-row limit; everything else gets 500

### Tests
`AnalyticsQueryBuilderTests` (37 — one per aggregate function, one per filter operator including edge cases like a malformed `Between` and quote-escaping, sort defaulting, Top N and its safety-cap clamp, every window function type including partition-by and custom alias, both pivot aggregate shapes, identifier sanitisation, null/empty-table validation), `WidgetRenderCoordinatorTests` (8, updated — QuerySpec shape per visual type, enabled-only local filters, per-visual-type row limits, error containment)

### Architecture decisions
- **`QuerySpec` lives in `Core.Analytics`, not `DuckDB`** — keeps the query *description* engine-agnostic; `WidgetRenderCoordinator` (in `TaskPane`) never references `NCVizDash.DuckDB` directly, only `IAnalyticsEngine` and `QuerySpec`. A future non-DuckDB backend (Phase 14 remote connectors, for instance) would only need its own `QuerySpec`-to-SQL translator, not changes to any calling code
- **A 5000-row safety cap is enforced inside the builder, not the caller** — no caller can accidentally request an unbounded result set that would choke the WebView2 JSON bridge; Phase 16 (Performance) will revisit this cap once virtualisation lands
- **Pivot's WHERE-clause gap is documented rather than worked around** — DuckDB's `PIVOT` statement doesn't support inline filtering before its `GROUP BY`; wrapping in a subquery was considered but deferred, since Phase 9's global filter manager is the more natural place to solve filtering-plus-pivot generally rather than special-casing it here

---

### Added

**Rendering pipeline**
- `WidgetRenderCoordinator` (new, `NCVizDash.TaskPane.Services`) — the missing link between a widget's field mappings and pixels: builds a `SELECT dim, SUM(measure) ... GROUP BY dim` query (raw, non-aggregated rows for Scatter/Bubble), runs it via `IAnalyticsEngine.QueryAsync`, and calls `IChartEngine.BuildChartOption`. Column names are re-sanitised identically to `DuckDbAnalyticsEngine`'s load-time sanitisation so widget field names (captured from raw Excel headers) resolve to the correct DuckDB columns. Returns a JSON error envelope (never throws) for unloaded data sources or unconfigured widgets
- `IAnalyticsEngine.GetTableName(Guid)` — added to the interface (was previously a `DuckDbAnalyticsEngine`-only method) so the coordinator stays engine-agnostic
- `DashboardCanvas` — gained `RenderCoordinator` and `Theme` dependency properties; `RebuildChildren` now creates a live-rendering `WidgetCard` per widget and disposes the old ones' `ChartHost` on rebuild; `RefreshWidget(widget)` re-renders a single card without a full rebuild; theme changes trigger a re-render of every card with the new palette

**WebView2 chart host**
- `ChartHost` (new WPF `UserControl`) — owns one `WebView2` instance per widget; navigates to a local `chart-host.html` (no CDN — fully offline per product vision); queues a render payload until the harness signals `host-ready`; forwards `chart-click` messages as a typed `ChartClicked` event for Phase 8 cross-filtering
- `chart-host.html` (new static asset) — vanilla JS harness: `window.ncvizdashRender(payloadJson)` dispatches to either `echarts.init/setOption` (chart-shaped visuals) or direct `innerHTML` injection (KPI/Table), with `notMerge:true` so switching a widget's visual type at runtime doesn't leave stale series behind; includes a `requestAnimationFrame`-based KPI count-up matching ECharts' `cubicOut` easing so HTML and chart animations feel consistent
- `WidgetCard` — restructured from pure `OnRender` placeholder to a real single-child `FrameworkElement` (`AddVisualChild`/`GetVisualChild` pattern) hosting a `ChartHost` in its body, with chrome (title bar, border, resize grip, selection highlight, shadow) still drawn in `OnRender`

**Chart builders** (`NCVizDash.ChartEngine.Builders`, new namespace)
- `ChartOptionContext` — shared data-extraction (`CategoryLabels`, `NumericValues`, `NameValuePairs`, `ScalarSum`, `ScalarFirst`) and standard ECharts config blocks (`AxisTooltip`, `ItemTooltip`, `BottomLegend`, `CategoryXAxis`, `ValueYAxis`, `DefaultGrid`) shared by every builder
- `CartesianBuilder` — Bar (grouped multi-measure), Line (smooth multi-series), Area (gradient fill, auto-stacked when multi-measure)
- `PolarBuilder` — Pie, Donut (centre-total `graphic` label), Gauge (tri-colour 0–100 arc, elastic pointer), Radar (per-category series when a dimension is present, else one aggregate series)
- `XyBuilder` — Scatter (per-category colour grouping), Bubble (3-measure X/Y/size via a JS `symbolSize` sqrt-scale function embedded in the option), Heatmap (`visualMap` colour scale), Treemap (recursive multi-level dimension aggregation)
- `HtmlBuilder` — KPI (animated count-up value + trend indicator) and Table (staggered row fade-in, 200-row cap ahead of Phase 16 virtualisation); both HTML-escape all user data (field names, cell values) to prevent injection into the WebView2 host

**Animation & theming**
- `AnimationPresets` — per-visual-type easing tuned to the visual's character: `elasticOut` for Gauge (satisfying snap), `bounceOut` for Bar, staggered 80ms-delay entry for Pie/Donut, fast `linear` draw for Line/Area, `cubicOut` elsewhere; every preset also defines a separate (shorter) update-transition easing so live data refreshes animate too, not just first paint
- `ChartPalette` — 10-colour brand palette anchored to the app's DeepPurple/Teal Material theme, separate Light/Dark variants, plus semantic positive/negative/neutral colours used by the KPI trend indicator and Gauge colour bands

**Engine**
- `EChartsChartEngine` — rewritten as a pure dispatcher: routes to the correct builder by `VisualType`, merges the `AnimationPreset` into the resulting option, and wraps everything in a `{ "kind": "echarts"|"html"|"error", ... }` envelope so the WebView2 harness can tell payload kinds apart with one check. Catches all builder exceptions and returns an `"error"` envelope rather than throwing, so a single misconfigured widget can't take down the render loop for the rest of the dashboard

### Tests
`AnimationAndPaletteTests` (14), `EChartsChartEngineTests` (19 — envelope shape, all 11 chart types produce valid JSON with animation config, theme sensitivity, KPI sum/trend, Table row content, null/empty-data handling), `ChartBuildersTests` (16 — structural assertions per builder: series count, smoothing, area gradient, pie data count, donut inner radius, gauge clamping, radar indicator count, scatter/bubble axis shape, heatmap visualMap, treemap aggregation, HTML escaping), `WidgetRenderCoordinatorTests` (7 — error envelopes for unloaded/unconfigured widgets, GROUP BY query shape for aggregate visuals, raw-row query for Scatter, exception containment, column-name sanitisation)

### Architecture decisions
- **KPI and Table are never routed through ECharts** — they're HTML fragments with CSS/JS animation, wrapped in the same envelope format as chart payloads so the WebView2 harness and `WidgetCard` don't need to know or care which kind a given widget is
- **The rendering payload is a tagged JSON envelope (`kind: echarts|html|error`)**, not a bare ECharts option — this was necessary once KPI/Table needed a fundamentally different rendering path, and it also gives the engine a clean, structured way to report per-widget render failures instead of throwing across the WebView2 boundary
- **`echarts.min.js` is not committed to source control** — it's a large (~1MB) generated third-party artifact; the project documents exactly where to obtain it (Apache-2.0 licensed, official ECharts distribution) and the `.csproj` only copies it to the output directory `Condition="Exists(...)"`, so the solution still builds (with a clear runtime error) if a contributor hasn't fetched it yet
- **Query building lives in `WidgetRenderCoordinator`, not in `IAnalyticsEngine` or `IChartEngine`** — neither engine should know about `DashboardWidget`'s field-mapping shape; the coordinator is the one place that translates "what the user configured" into "what SQL to run," keeping both engines reusable in isolation (e.g. `IChartEngine` could someday render pre-fetched data with no DuckDB involved at all)

---

### Added

- `FieldComposition` — immutable value object built from a raw field list; exposes `Measures`/`Dimensions`/`Times`/`Filters` counts and five name-hint flags (`HasRateHint`, `HasFinancialHint`, `HasBudgetHint`, `HasGeoHint`, `HasPeopleHint`) derived by scanning field names against curated term tables (e.g. "revenue", "rate", "region")
- `VisualizationRule` — named, priority-ordered rule: a `Func<FieldComposition, bool>` predicate, a `VisualType` result, and a human-readable `Explanation` string consumed by the tooltip and Phase 12 "Explain Chart"
- `RuleRegistry` — 25 deterministic rules in 5 priority bands, covering every `VisualType`:

  | Band | Range | Examples |
  |---|---|---|
  | Specific combos | 100–199 | Bubble (3M), Scatter (2M,0D), Radar (4+M,0D), Heatmap (2D,1M), Gauge (rate hint) |
  | Time-series | 200–299 | Line (1T,1M), Line multi-series, Area (financial+time) |
  | Categorical | 300–399 | Pie (1M,2D), Donut (financial,1D), Treemap (budget,1D), Bar (1M,1D) |
  | Multi-measure | 400–499 | Scatter (2M,1D), grouped Bar, Radar (3+M,1D) |
  | KPI | 500–599 | KPI financial, KPI generic |
  | Fallback | 900 | Table (always matches) |

- `DeterministicRuleEngine` — fully implements `IVisualizationRuleEngine`: walks registry in priority order, logs each match at Debug level; `RecommendWithExplanation` returns `(VisualType, RuleName, Explanation)` for UI and Phase 12; `AllMatches` returns the full ranked list for a suggestion picker
- `IVisualizationRuleEngine` — extended with `RecommendWithExplanation` and `AllMatches`
- `CanvasPanelViewModel.AddWidgetFromFieldDrop` — replaces every hardcoded `field.FieldType switch` statement across Views and Controls; calls `RecommendWithExplanation` so the winning explanation is logged; `overrideVisual` parameter for caller-forced type (used by the visual library tile drop path); per-visual-type default `ColumnSpan`/`RowSpan` (KPI = 4×3, Table = 10×6, etc.)
- Tests: `Phase5RuleEngineTests` (44 — every rule fired in isolation, composition building, name-hint detection, `RecommendWithExplanation`, `AllMatches`, registry invariants), `CanvasPanelRuleEngineIntegrationTests` (6 — field-drop routing per FieldType, override, layout defaults)

### Architecture decisions
- **No fallback to hardcoded `switch` anywhere** — the rule engine is the single source of truth for all visual-type selection; the `IVisualizationRuleEngine` interface is the contract, so swapping in a different recommender (e.g. Phase 18's AI advisor) requires no changes outside the DI registration
- **`FieldComposition` is pre-computed once per call** and passed to every rule predicate — avoids repeated LINQ scans when `AllMatches` walks all 25 rules
- **`Explanation` strings are author-written prose** rather than generated from the rule predicate — they read naturally in a tooltip ("3 measures selected — X position, Y position and bubble size map naturally to a Bubble chart") and cost nothing at runtime
- **Name-hint flags use `Contains` not exact match** — "total_revenue", "revenue_ytd", "arr_revenue" all trigger `HasFinancialHint`; this is intentional and covers real-world column naming conventions without requiring users to follow a strict naming scheme

---

### Added

- `DashboardCanvas` — custom `Panel` subclass, the centrepiece of the designer:
  - `ArrangeOverride` positions every `WidgetCard` child at pixel coordinates derived from `WidgetLayout` × `GridGeometryHelper.UnitSize` (40px/unit)
  - `OnRender` draws the dot-grid (on/off via `ShowGrid`), dashed alignment-guide lines, and the rubber-band rectangle — no XAML/ResourceDictionary required so the class is safe to instantiate in unit tests
  - Full mouse pipeline: `OnMouseLeftButtonDown` → hit-test (widget vs. background) → set `DragMode` (Move / Resize / RubberBand) → `CaptureMouse`; `OnMouseMove` → compute snapped delta → call `MoveWidget`/`ResizeWidget`/update rubber-band; `OnMouseLeftButtonUp` → commit rubber-band selection → `ClearGuides`
  - Bottom-right 10×10px resize grip detected via hit-test in `OnMouseLeftButtonDown`; cursor updates live on `OnMouseMove`
  - `RebuildChildren` synchronises `Panel.Children` with `CanvasPanelViewModel.Widgets` whenever the collection changes
- `WidgetCard` — `FrameworkElement` drawn entirely in `OnRender` (no XAML): rounded card + elevation shadow, title bar, resize grip with diagonal stripe decoration, selection highlight (2px purple border when `IsSelected`), chart-type placeholder centred in the body (replaced by WebView2 in Phase 6); subscribes to `DashboardWidget.PropertyChanged` and `WidgetLayout.PropertyChanged` to self-invalidate on any data change
- `GridGeometryHelper` (pure, WPF-free) — `SnapToGrid`, `ToPixels`, `ClampPosition`, `ClampColumnSpan`, `ClampRowSpan`, `Overlaps`, `ComputeAlignmentGuides` (at most one vertical + one horizontal guide, best-match within 6px tolerance)
- `CanvasPanelViewModel` — fully implemented: `SelectWidget` (single/additive), `ClearSelection`, `MoveWidget` (snap + clamp), `ResizeWidget` (clamp), `DuplicateWidget` (new Id, independent deep copy of `LocalFilters`, offset layout), `DuplicateSelectedWidgets`, `DeleteSelectedWidget` (multi-select aware), `UpdateGuides`/`ClearGuides`, `OpenDashboard`
- `WidgetLayout` and `DashboardWidget` — made `INotifyPropertyChanged`; `DashboardWidget.IsSelected` is `[JsonIgnore]` (transient UI flag)
- `CanvasPanelView.xaml` — rebuilt: `DashboardCanvas` embedded in a `ScrollViewer` over a min-960×640 `Grid`; toolbar with duplicate/delete/snap/grid toggles; dashboard name bar showing active dashboard name and widget count; empty-state overlay (`IsHitTestVisible=False` so it doesn't block drops)
- `CanvasPanelView.xaml.cs` — keyboard shortcuts (Del → delete, Ctrl+D → duplicate, Escape → clear selection); DragDrop routing decoupled from `DashboardCanvas` so the panel stays purely a layout/gesture concern
- `NullToCollapsedConverter` added to `ValueConverters.cs`; registered in `ShellWindow.xaml` resources
- Tests: `GridGeometryHelperTests` (17 cases — snap rounding, pixel conversion, clamp bounds, overlap detection, alignment-guide detection/tolerance/self-skip), `CanvasPanelViewModelPhase4Tests` (17 cases — move/resize with clamping, selection toggling, multi-select, duplicate deep-copy independence, guide lifecycle)

### Architecture decisions
- **`DashboardCanvas` renders grid/guides/rubber-band directly in `OnRender`** rather than using WPF `adorner` layer or overlay `Canvas` — avoids a separate hit-test surface and keeps the visual hierarchy flat (one element owns its own chrome)
- **`WidgetCard` is pure code-behind** — no dependency on `ResourceDictionary` or `Application.Current`, making it safe to create in both VSTO and future web/desktop host processes without a WPF application object
- **Alignment guides capped at one per axis** — returning the single closest match per orientation avoids screen clutter during complex arrangements; Phase 12 (advanced features) can relax this if users want all matches shown simultaneously

---

- `NCVizDash.Models.WidgetFilter` + `FilterOperator` enum — persisted, widget-scoped filter overrides (`DashboardWidget.LocalFilters`). Distinct from the transient runtime cross-filter state that `IFilterManager` will manage in Phase 8: local filters are part of the widget's saved JSON and survive a dashboard reload (e.g. "this chart always excludes Q1 regardless of the active global date filter")
- `DashboardWidget` and `WidgetLayout` confirmed/clarified as already fully decoupled from Excel: widgets reference `DataSourceId` (a `Guid`) rather than any cell/range/worksheet, so multiple dashboards and cross-worksheet data sources were already supported by the existing shape — this change closes the one real gap (no persisted per-widget filter override existed before)
- Tests: `WidgetFilterTests` (5 — defaults, JSON round-trip including `LocalFilters`, confirms `IsSelected` stays excluded from serialization, enum string-serialization for all `FilterOperator` values)

---

## [0.3.0] – Phase 3 – User Interface – 2026-06-30

### Added

- `NCVizDash.TaskPane.Services.ThemeService` — event-based theme coordinator. Deliberately avoids MaterialDesignThemes' `PaletteHelper` (which targets `Application.Current.Resources`, unreliable in a VSTO host with no guaranteed WPF `Application` instance); instead raises `ThemeChanged`, consumed by `ShellWindow` to mutate its own named `BundledTheme` resource directly
- `NCVizDash.TaskPane.Converters.ValueConverters` — `BooleanToVisibilityConverter`, `GridUnitConverter` (grid units ↔ device pixels, 40px/unit), `InverseBooleanConverter`, `CountToVisibilityConverter`. **Fixes a Phase 1 defect**: these were referenced via `{StaticResource}` in `CanvasPanelView.xaml` and `ExplorerPanelView.xaml` since Phase 1 but never actually defined
- `ShellWindow.xaml` — converters registered as window resources; `BundledTheme` given `x:Name` for direct mutation; Visual Library column widened (200→220, min 160→190) for comfortable two-tile-wide layout
- `ExplorerPanelViewModel` — `FilteredDataSources` observable collection, live-updated via `OnSearchTextChanged` partial hook and `DataSources.CollectionChanged`; `PreviewRows` / `PreviewSource` / `IsPreviewLoading` plus `LoadPreviewAsync` (capped at 10 rows) and `ClearPreview` for the hover data-preview popup
- `ExplorerPanelView` — field rows now initiate WPF `DragDrop` (custom `"NCVizDash.Field"` format) on drag-distance threshold; data source headers trigger a debounced (450ms) hover preview `Popup` containing a read-only `DataGrid` sample
- `VisualLibraryView` — chart tiles now initiate WPF `DragDrop` (custom `"NCVizDash.VisualType"` format)
- `CanvasPanelView` — registered as a drop target (`AllowDrop`); accepts both Visual Library tiles and Explorer fields; shows a highlighted border during an acceptable drag-over; routes drops to `CanvasPanelViewModel.AddWidgetFromDrop`
- `CanvasPanelViewModel.AddWidgetFromDrop` — creates a `DashboardWidget` from a dropped visual type and optional seed field (field's `FieldType` picks measure vs. dimension slot, and a sensible default visual type when dropped directly from the explorer); auto-creates a default `Dashboard` if none is open yet; naive staggered placement (full grid/snap/collision logic deferred to Phase 4)
- Tests: `ExplorerPanelViewModelTests` (7 — load/populate, partial-failure isolation, name/field search filtering, preview load/clear), `CanvasPanelViewModelTests` (7 — drop creates dashboard/widget, field-type routing, delete), `ThemeServiceTests` (3), `ValueConvertersTests` (7)

### Architecture decisions
- **Theme switching avoids `Application.Current` entirely** — VSTO add-ins run inside Excel's process without a guaranteed `System.Windows.Application` instance; mutating a named, window-owned `BundledTheme` resource is robust regardless of host process shape
- **Drag-and-drop uses raw WPF `DragDrop` with custom format strings**, not a third-party behaviors library — keeps Phase 3 dependency-light; Phase 4 can swap in a richer mechanism without touching the data contracts (`FieldDescriptor`, `VisualTypeEntry`)
- **Canvas drop-to-add is intentionally minimal** — full move/resize/snap/multi-select/alignment-guide mechanics are scoped to Phase 4's dedicated `DashboardCanvas` panel; Phase 3 only needed a working, testable drop target so the UI isn't a dead end
- **Search filtering is read off `ExplorerPanelViewModel.GetFilteredDataSources()` (plain LINQ)** rather than a WPF `ICollectionView`, so the filtering logic itself stays unit-testable without an STA/Dispatcher context

---

## [0.2.0] – Phase 2 – Excel Data Engine – 2026-06-30

### Added

- `NCVizDash.Core.Classification.FieldTypeClassifier` — deterministic, no-AI field classification:
  - `Classify(columnName, clrType)` — type-based classification with identifier/code name-hint overrides
  - `ClassifyFromSample(columnName, values)` — dominant-type sampling for loosely-typed Excel ranges
  - Boolean → Filter, DateTime/DateTimeOffset → Time, numeric → Measure (IDs/codes excluded), text → Dimension with date/boolean name-hint fallback
- `NCVizDash.ExcelAddIn.DataAccess.ExcelDataReader` — full `IExcelDataReader` implementation:
  - Discovers Excel Tables (`ListObject`), worksheet-scoped and workbook-scoped Named Ranges
  - Per-column date detection via cell `NumberFormat` inspection (OLE date serials → `DateTime` only when the source cell is actually date-formatted, avoiding false positives on plain numbers)
  - Column sampling (up to 25 rows) feeds `FieldTypeClassifier` for automatic field typing
  - `ReadRowsAsync` materialises a previously-discovered source's full range into row dictionaries
- `NCVizDash.DuckDB.DuckDbAnalyticsEngine` — full `IAnalyticsEngine` implementation:
  - In-memory DuckDB connection (no server/file dependency, per offline-first product vision)
  - `LoadDataSourceAsync` — sanitised table/column identifiers, `FieldType`-aware DDL (`DOUBLE`/`TIMESTAMP`/`BOOLEAN`/`VARCHAR`), transactional parameterised bulk insert
  - `QueryAsync` — generic SQL passthrough returning row dictionaries for the chart/rule engines
  - `UnloadDataSourceAsync` — drops the backing table and clears the source mapping
  - Reloading an already-loaded source safely drops and recreates the table (handles Phase 2 auto-refresh)
- `NCVizDash.TaskPane.ViewModels.ExplorerPanelViewModel` — now takes `IExcelDataReader` and `IAnalyticsEngine` via constructor injection; `LoadDataSourcesAsync` performs full discover → read → classify → ingest pipeline with per-source error isolation (one bad source no longer blocks the rest); added `GetFilteredDataSources()` for search-box filtering
- `ThisAddIn.cs` — DI registrations for `IExcelDataReader`, `IAnalyticsEngine`, `IVisualizationRuleEngine`, `IChartEngine`, `IDashboardRepository`; `SheetChange` now drives a debounced auto-refresh (interval from `AppSettings.AutoRefreshSeconds`, default disabled)
- Tests: `FieldTypeClassifierTests` (13 cases covering all four field types plus identifier/name-hint edge cases), `DuckDbAnalyticsEngineTests` (4 integration tests: load+query, GROUP BY aggregation, unload, reload-replaces-data)

### Architecture decisions
- **Date detection uses cell `NumberFormat`, not heuristic serial-number ranges** — a plain numeric column with a value like `45000` should never be silently reinterpreted as a date; only cells Excel itself displays as dates are converted
- **Per-source error isolation in `ExplorerPanelViewModel`** — a malformed table or unreadable named range is logged and skipped rather than aborting the entire refresh
- **DuckDB table/column identifiers are sanitised and GUID-suffixed** — prevents SQL injection from user-controlled sheet/table/column names and guarantees uniqueness across sheets with identical table names
- **Auto-refresh is opt-in and debounced** — `AutoRefreshSeconds = 0` (the default) disables it entirely; when enabled, rapid successive `SheetChange` events collapse into a single refresh after the configured quiet period

---

## [0.1.0] – Phase 1 – Foundation – 2026-06-30

### Added

**Solution & Projects**
- `NCVizDash.sln` – 11-project solution with Debug/Release configurations
- `NCVizDash.Models` – Core domain models: `FieldDescriptor`, `DataSourceDescriptor`, `Dashboard`, `DashboardWidget`, `WidgetLayout`, `VisualType`, `FieldType`, `AppSettings`
- `NCVizDash.Core` – Service contract interfaces: `IExcelDataReader`, `IAnalyticsEngine`, `IDashboardRepository`, `IVisualizationRuleEngine`, `IChartEngine`, `IFilterManager`, `IAppSettingsProvider`
- `NCVizDash.Core` – DI extension: `CoreServiceCollectionExtensions.AddNCVizDashCore()`
- `NCVizDash.Infrastructure` – `SerilogBootstrapper` (rolling file + Debug sink, enriched with thread ID and environment)
- `NCVizDash.Infrastructure` – `JsonAppSettingsProvider` (reads/writes `%LOCALAPPDATA%\NCVizDash\ncvizdash.json`)
- `NCVizDash.Infrastructure` – `InfrastructureServiceCollectionExtensions.AddNCVizDashInfrastructure()`
- `NCVizDash.Ribbon` – `NCVizDashRibbon.xml` (Office 2009 Fluent UI schema) defining the **NC VizDash** tab with Dashboard, Data, View, Export, and Help groups
- `NCVizDash.Ribbon` – `NCVizDashRibbon.cs` implementing `IRibbonExtensibility` with full callback stubs and events for DI consumers
- `NCVizDash.TaskPane` – WPF three-panel shell (`ShellWindow.xaml`) with Material Design theming
- `NCVizDash.TaskPane` – `ShellViewModel` (MVVM toolkit, `ObservableObject`) with `ApplyThemeCommand` and `RefreshDataCommand`
- `NCVizDash.TaskPane` – `ExplorerPanelViewModel` + `ExplorerPanelView.xaml` (data source tree, field type icons, search box)
- `NCVizDash.TaskPane` – `CanvasPanelViewModel` + `CanvasPanelView.xaml` (grid overlay, widget canvas, empty-state UI, toolbar)
- `NCVizDash.TaskPane` – `VisualLibraryViewModel` + `VisualLibraryView.xaml` (chart tile grid with Material icons)
- `NCVizDash.ExcelAddIn` – `ThisAddIn.cs` VSTO entry point; DI composition root; Serilog bootstrap; Excel application event wiring; task pane lifecycle (ElementHost wrapping WPF)
- `NCVizDash.DuckDB` – Stub `DuckDbAnalyticsEngine` (Phase 2/7 target)
- `NCVizDash.Persistence` – Stub `WorkbookDashboardRepository` (Phase 10 target)
- `NCVizDash.RuleEngine` – `DeterministicRuleEngine` with initial 6-rule table
- `NCVizDash.ChartEngine` – `EChartsChartEngine` with stub Bar and KPI builders
- `NCVizDash.Tests` – xUnit test project; `DeterministicRuleEngineTests` (6 tests); `AppSettingsTests` (5 tests)

### Architecture decisions
- **VSTO (.NET 8)** chosen per spec; noted as legacy – no Office.js migration planned for this project
- **Clean Architecture** enforced: Models → Core (abstractions only) → Infrastructure / domain implementations → Add-in host
- **DI container** built in `ThisAddIn.cs`; all services resolved via `IServiceProvider`; no `new` of service classes outside DI
- **Serilog** bootstrapped before DI so fatal startup errors are captured; reconfigured once settings are DI-available
- **MVVM Toolkit** (`CommunityToolkit.Mvvm`) used for source-generated `[ObservableProperty]` and `[RelayCommand]` to eliminate boilerplate

## [0.11.1] – Framework targeting correction – 2026-07-05

### Fixed

**Corrected a wrong architectural call from the previous entry.** The prior guidance
("only `Ribbon`/`TaskPane`/`ExcelAddIn` need `net48`, everything else can stay on
`net8.0-windows`") was incorrect and caused real build failures once actually tried
in Visual Studio. The mistake: VSTO loads an add-in into Excel's process using the
classic .NET Framework CLR, and **a .NET Framework process cannot load .NET 8–targeted
assemblies at all** — they are different runtimes, not just different API surfaces.
Since `ExcelAddIn` transitively references every other project, *all* of them end up
loaded into the same Excel process at runtime, so all of them must target `net48`.

- Retargeted `Models`, `Core`, `Infrastructure`, `RuleEngine`, `ChartEngine`, `DuckDB`,
  `Persistence`, `Connectors`, and `Tests` to `net48` (joining `Ribbon`, `TaskPane`,
  `ExcelAddIn`, which were already correctly retargeted) — the whole solution is now
  consistently `net48` with `LangVersion 12.0` explicitly set on every project, so
  the modern C# syntax throughout the codebase (records, `init`, collection
  expressions, file-scoped namespaces) keeps compiling despite the older runtime target
- Added `IsExternalInit.cs` (a small, `internal`, per-assembly polyfill for
  `System.Runtime.CompilerServices.IsExternalInit`) to every project that declares
  `init`-only properties or `record` types: `Models`, `Core`, `ChartEngine`,
  `Connectors`, `TaskPane`, `ExcelAddIn`. This type exists in the .NET 5+ BCL but not
  in .NET Framework 4.8; Roslyn can auto-synthesize it, but that synthesis proved
  unreliable in this SDK-style net48 configuration, so it's declared explicitly instead
- Added the missing Office core COM reference (`CustomXMLPart`/`CustomXMLParts`, used
  by `WorkbookDashboardRepository`) to `NCVizDash.Persistence.csproj` — the same gap
  already fixed in `Ribbon`/`ExcelAddIn` during earlier build-error passes, just missed
  in this one project since it wasn't touched at the time

### Known risk areas not yet verified against net48
- `DuckDB.NET.Data`/`DuckDB.NET.Bindings.Full` 1.1.0 — should support net48 via their
  native binary loader, but this hasn't been build-verified in this environment (no
  Windows/Office available here); watch for native-library-loading errors specific to
  DuckDB if they surface
- Any .NET 8–only BCL API used anywhere in the codebase (newer LINQ overloads, certain
  `string`/`Enumerable` methods) will now surface as `CS0117`/`CS1061` ("member does
  not exist") rather than a language-version error — these weren't systematically
  audited line-by-line; flag any that appear and they can be swapped for the
  net48-compatible equivalent
