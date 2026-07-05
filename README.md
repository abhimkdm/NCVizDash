# NC VizDash

> **Power BI / Tableau experience — directly inside Microsoft Excel.**  
> Open Excel → Load Data → Drag & Drop → Instant Dashboard.

---

## What is NC VizDash?

NC VizDash is a production-ready enterprise Business Intelligence platform built as a Microsoft Excel VSTO add-in. It gives every Excel user a modern dashboard designer with zero training required, no cloud dependency, and full offline capability.

---

## Quick Start (Prerequisites)

| Requirement | Version |
|---|---|
| Windows | 10 / 11 (64-bit) |
| Microsoft Excel | 2016 or later (Microsoft 365 recommended) |
| .NET | 8.0 (Desktop Runtime) |
| Visual Studio | 2022 with *Office/SharePoint development* workload |
| WebView2 Runtime | Latest (ships with Edge / Windows 11) |

### Build

```bash
git clone https://github.com/your-org/NCVizDash.git
cd NCVizDash
dotnet restore
dotnet build --configuration Release
```

> **One-time asset step (required from Phase 6 onward):** download Apache ECharts'
> minified distribution (Apache-2.0 licensed) — either from
> https://echarts.apache.org/en/download.html or via `npm install echarts` and copying
> `node_modules/echarts/dist/echarts.min.js` — and place it at
> `NCVizDash.TaskPane/Assets/echarts.min.js`. It's intentionally excluded from source
> control as a large generated binary. The solution builds without it, but chart
> widgets will show a "file not found" error at runtime until it's present.

### Run (during development)

1. Open `NCVizDash.sln` in Visual Studio 2022.
2. Set **NCVizDash.ExcelAddIn** as the startup project.
3. Press **F5** – Visual Studio will launch Excel with the add-in registered.

---

## Architecture

```
NCVizDash.Models          ← Pure domain models (no dependencies)
NCVizDash.Core            ← Service abstractions / interfaces
NCVizDash.Infrastructure  ← Serilog, configuration, DI wiring
NCVizDash.RuleEngine      ← Deterministic visualization rule engine
NCVizDash.ChartEngine     ← Apache ECharts option builders
NCVizDash.DuckDB          ← In-process analytics engine
NCVizDash.Persistence     ← Dashboard storage (Excel Custom XML Parts)
NCVizDash.Ribbon          ← Excel ribbon (IRibbonExtensibility)
NCVizDash.TaskPane        ← WPF three-panel UI (MVVM + Material Design)
NCVizDash.ExcelAddIn      ← VSTO host, DI composition root
NCVizDash.Tests           ← xUnit unit + integration tests
```

**Dependency flow:**  
`ExcelAddIn` → `TaskPane` / `Ribbon` → `Core` → `Models`  
`Infrastructure` → `Core` → `Models`  
Domain projects → `Core` → `Models`

---

## Technology Stack

| Layer | Technology |
|---|---|
| Platform | C# / .NET 8, VSTO |
| UI | WPF, MVVM (CommunityToolkit.Mvvm), Material Design in XAML |
| Visualization | WebView2 + Apache ECharts |
| Analytics | DuckDB.NET |
| Logging | Serilog (File + Debug sinks) |
| Testing | xUnit, Moq |
| Serialization | System.Text.Json |
| DI | Microsoft.Extensions.DependencyInjection |

---

## Phases

| # | Phase | Status |
|---|---|---|
| 1 | Foundation | ✅ Complete |
| 2 | Excel Data Engine | ✅ Complete |
| 3 | User Interface | ✅ Complete |
| 4 | Dashboard Builder | ✅ Complete |
| 5 | Rule Engine | ✅ Complete |
| 6 | Chart Engine | ✅ Complete |
| 7 | DuckDB Analytics | ✅ Complete |
| 8 | Cross Filtering | ✅ Complete |
| 9 | Global Filters | ✅ Complete |
| 10 | Dashboard Storage | ✅ Complete |
| 11 | Templates | ✅ Complete |
| 12 | Advanced Features | ✅ Scoped subset (see TASKS.md) |
| 13 | Export | ✅ Complete (ribbon wiring pending) |
| 14 | Data Connectors | ✅ CSV/JSON/SQL Server/REST (SharePoint stubbed) |
| 15 | Collaboration | ✅ Complete |
| 16 | Performance | ✅ Partial (caching + parallel render) |
| 17 | Plugin SDK | ✅ Complete |
| 18 | Optional AI | ✅ Complete, strictly opt-in (disabled by default) |

---

## Configuration

Settings are stored at `%LOCALAPPDATA%\NCVizDash\ncvizdash.json` and created automatically on first run.

```json
{
  "DefaultTheme": "Light",
  "LogLevel": "Information",
  "LogDirectory": "%LOCALAPPDATA%\\NCVizDash\\Logs",
  "RecentDashboardsMax": 10,
  "AutoRefreshSeconds": 0,
  "GridSnapColumns": 1,
  "ShowAlignmentGuides": true,
  "MaxIngestRows": 1000000,
  "TelemetryEnabled": false,
  "PluginDirectory": "%LOCALAPPDATA%\\NCVizDash\\Plugins"
}
```

Logs roll daily under the `LogDirectory` and are retained for 14 days.

---

## v2.0 — Productivity Features

Delivered as a follow-on batch after the 18-phase MVP: One-Click Dashboard
Generator, an 11th dashboard template (Delivery Dashboard), full-screen Story
Mode presentations, selective Live Refresh (only affected widgets re-render on
a sheet change), and a Jira Enterprise Connector with a Dynamic JQL Editor —
Jira datasets flow through the exact same `IDataConnector` → `IAnalyticsEngine`
→ dashboard pipeline as Excel data, so the rest of the app never distinguishes
between them. See `CHANGELOG.md` (`[0.11.0]`) and `TASKS.md` for full detail,
including the handful of explicitly scoped-down items (JQL syntax highlighting,
OAuth for Jira, true dataset-append semantics).

---

## Contributing

1. One phase at a time – do not open PRs that mix phases.
2. Every PR must pass `dotnet test` with zero failures.
3. Zero warnings, zero errors (`TreatWarningsAsErrors = true`).
4. Update `TASKS.md`, `CHANGELOG.md`, and `README.md` with every merged phase.

---

## Licence

MIT © NC VizDash Contributors
