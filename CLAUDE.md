# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A **cTrader cBot** (automated trading robot) written in C# against the cAlgo API, targeting
`net6.0`. The strategy is **VWAP Strong** (`docs/VWAP_Strong_V1.1.docx`), a VWAP break-and-reverse
on **M5 only**. The daily VWAP is the key level (the yellow line): a closed bar whose candle
pattern (pinbar / engulfing / fractal / harami) touches it becomes an entry, sized against a
per-trade risk budget. Three gates, in order (`Vwap/VwapStack.cs`):

1. **Stack** — only `close > daily > weekly` may go long, only `close < daily < weekly` may short.
2. **Distance** — `GapX = direction × (daily − weekly) / ATR ≥ GapMin`.
3. **Speed** — `SlopeRateX30 = direction × (daily − daily[N]) / ATR × 6/N ≥ SlopeRateMin`.
4. **Gap change** — `GapChangeRateX30 = (gap − gap[N]) / ATR × 6/N` must sit inside
   `[GapChangeRateMin, GapChangeRateMax]`, but only when `UseGapChangeFilter` is on.
5. **Direction** — `TradeDirectionGate` (All / LongOnly / ShortOnly), the last gate before sizing.

`direction` is +1 long, −1 short, so a VWAP moving against the trade is negative and can never
pass — never take an absolute value here. Gates 2 and 3 switch off with a threshold of 0; gate 4
needs its own boolean, because a gap change is legitimately negative and its useful interval can
straddle 0, so 0 cannot double as "off". Gate 5 defaults to `All` and must leave results identical
to a build without it. The `6/N` term is unit conversion, not a new condition:
6 M5 bars = 30 minutes, so any lookback is expressed as an equivalent 30-minute speed and different
`N` share one threshold. That is why the bot refuses to run on anything but M5. A threshold of 0
switches that gate off entirely — a missing ATR must not then block the trade.

Both `ATR14_M5` and `ATR14_H1` are always computed and always logged; the `ATR归一周期` parameter
only picks which one the two gates divide by. `GapChangeX` is recorded but never filters on.

Everything runs on Japan time (`[Robot(TimeZone = TimeZones.TokyoStandardTime)]`). Two separate
clocks, deliberately decoupled:

- **Indicator periods** (`Vwap/VwapPeriod.cs`) — the daily VWAP accumulates 06:00 → 06:00 the next
  morning, the weekly from Monday 06:00. Every bar accumulates, including Monday and the pre-open
  hours, so a VWAP value is never blank.
- **Order gate** (`Vwap/TradingSession.cs`) — new orders only between 10:30 and 06:00 the next
  morning, Tuesday through Friday (no Monday). Open positions are left to their stop or target.

So at 10:30, when trading opens, the daily VWAP has already been accumulating for four and a half
hours — it does not start from zero.

`VWAPTrade.cs` is the Robot lifecycle shell that wires the pieces together (the composition root).

## Module map

Behavior classes live beside the feature they serve; all data types live in `Models/`
(suffixed `Model`):

- `Vwap/` — `VwapPeriod` (when the VWAP resets), `TradingSession` (when orders may open — a
  different clock, see above), `VwapStrongMetrics` (the GapX / SlopeRawX / SlopeRateX30 formulas),
  `VwapStack` (the gates), `StartupCheck` (parameter validation), `VwapCalculator` (pure
  accumulation) — all unit tested — and
  `VwapSeries`, which reads `Bars` and caches one `VwapSampleModel` per closed bar. It is the
  single source of VWAP values for drawing and signals.
- `Indicators/` — `Atr14Series` (one timeframe's ATR14, Wilder) and `Atr14Pair` (the M5 + H1 pair).
- `Signals/` — `SignalDetector` applies the direction gate, builds the daily-VWAP key level for
  the closed bar, and asks `Biz/MainBiz` which candle pattern touches it.
- `LineDrawer/` — `VwapSlim` draws the three VWAP lines, `SignalMarkers` the entry markers.
- `Orders/` — `OrderPlanner` (pure sizing/geometry), `TradeDirectionGate` and `TradeResultR` (pure,
  unit tested); `OrderExecutor` only decides whether to place an order and places it, delegating the
  position bookkeeping to `TradeJournal` and the breakeven stop to `BreakevenProtector`.
- `Risk/` — `RiskGuard` (trading-session window + stop-distance and risk-money rules, pure).
- `OrderLogger/` — `TradeCsvColumns` is the single declarative table of CSV columns (name + how to
  read it), so the header and every row are generated from one list and cannot drift apart;
  `TradeCsvLogger` decides what facts go in a row and when to write it; `TradeCsvMigrator` (pure,
  unit tested) upgrades files written by older builds.
- `Models/` — data types: `OrderPlanModel`, `SignalModel`, `TradeLevelModel`,
  `TradeSettingsModel`, `VwapSampleModel`, `TradeDirectionModel`, the `ISymbolModel` port, and
  its `CAlgoSymbolModel` adapter (the one Models/ file that references `cAlgo.API`).

Rule of thumb: classes with no `using cAlgo.API` are pure and testable; keep them that way.
`CAlgoSymbolModel` is the sole broker adapter — it is the only Models/ file that touches
cAlgo, and it is never linked into the test project.

## Build & run

```bash
# Build (from repo root)
dotnet build "VWAPTrade.sln"          # Debug
dotnet build "VWAPTrade.sln" -c Release
```

A successful build produces a `.algo` package under
`VWAPTrade/bin/<Config>/net6.0/`. The `.algo` file is the deployable
cBot artifact loaded by the cTrader desktop platform.

The cBot itself is validated by running it in cTrader's backtester/optimizer, not via a CLI
runner. Iteration loop: edit `.cs` → `dotnet build` → load/refresh the `.algo` in cTrader →
backtest.

Pure (framework-independent) helpers are unit-tested with xUnit under `tests/`:

```bash
./scripts/test.sh                                       # build cBot + run all tests
dotnet test "tests/VWAPTrade.Tests/VWAPTrade.Tests.csproj"      # tests only
```

The test project is intentionally **not** part of the `.sln` (which cTrader builds) and
targets `net10.0` rather than the cBot's `net6.0` — it links pure source files directly (via
`<Compile Include>`) instead of referencing the cBot project, so tests never pull in the
`cTrader.Automate` / cAlgo.API dependency. Keep new domain/risk logic pure so it can be
tested this way.

The `cTrader.Automate` NuGet package (versioned `*`) supplies the `cAlgo.API.*` assemblies;
restore happens automatically on build.

## Code structure

A cBot is a single class deriving from `cAlgo.API.Robot` in namespace `cAlgo.Robots`,
annotated with `[Robot(...)]`. The framework drives it through lifecycle overrides — there is
no `Main`:

- `OnStart()` — one-time setup (read parameters, attach indicators).
- `OnTick()` — runs on every price update; intraday/entry logic lives here.
- `OnBar()` — runs on each completed bar (override when the strategy is bar-based, e.g.
  computing the prior session's high/low).
- `OnStop()` — teardown.

User-tunable inputs are `public` properties decorated with `[Parameter(...)]`; these surface
in the cTrader UI and the optimizer. Trading actions and market data come from inherited
members (`ExecuteMarketOrder`, `Positions`, `Symbol`, `Bars`, `MarketSeries`, `Print`, etc.).

`[Robot(AccessRights = AccessRights.None)]` means the bot cannot touch the file system or
network — keep it that way unless a feature genuinely requires elevated access.

## Conventions

- Spaces in the project/solution/file names are intentional (cTrader convention) — always
  quote paths in shell commands.
- `bin/`, `obj/`, `.idea/`, `*.user`, and generated `*.algo` files are git-ignored; commit
  only the `.cs`, `.csproj`, and `.sln`.
