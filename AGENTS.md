# AGENTS.md

Working rules for coding agents in this repository. For deeper reference:

- `CLAUDE.md`: the full gate formulas and the module map.
- `README.md`: every parameter, and what the two CSV files contain.
- `docs/debugging-in-rider.md`: stepping through the running cBot on macOS.

## What this is

`VWAPTrade` is a cTrader cBot (C#, cAlgo API, `net6.0`) that trades the **VWAP Strong** strategy
(`docs/VWAP_Strong_V2.pdf`) on **M5 only**, on Japan time.

Once per closed bar:

1. **Strong?** `VwapStack.ResolveSide(close, daily, weekly)` returns Buy for
   `close > daily > weekly`, Sell for `close < daily < weekly`, otherwise None and nothing happens.
2. **Signal?** A candle pattern for that side touches the daily VWAP (the yellow line):
   `LevelPatternMatcher` with `HanJinSignals26`.
3. **Signal filters**, in order: gap (`GapMin`), speed (`SlopeRateMin`), gap change (optional
   range), opposite-side recovery (configured preceding same-day closes below daily VWAP block a
   long; closes above it block a short), Departure (price left the daily VWAP and came back). The
   first one that fails is recorded as `SignalModel.FailedFilter`.
4. **Order gates**, in order: order window (Tue–Fri 10:30 → 06:00), trade direction, open
   position on this level, sizing (`OrderPlanner`), broker. `OrderExecutor.TryEnter` returns an
   `EntryOutcomeModel`: ordered, or the first gate that stopped it.
5. **Record:** a traded signal gets a chart marker and a trade CSV row. Every Strong signal, traded
   or not, gets one `debug.csv` row.

## Rules that protect the strategy

Breaking one of these changes trading results silently, so treat them as fixed:

- **Direction is signed, never absolute.** Metrics multiply by `direction` (+1 long, −1 short), so
  a VWAP moving against the trade is negative and fails. Never take an absolute value.
- **A threshold of 0 switches a filter off completely.** An off filter must never block a trade,
  even when its ATR or lookback value is missing. An on filter must block when the value is
  missing. The gap-change filter has its own boolean, because its range can straddle 0.
- **A filter that is off must leave results identical** to a build without it. The same goes for
  `交易方向 = All`.
- **The signal bar is the last closed bar, `Bars.Count - 2`.** `OnBar()` fires when a new bar
  opens. Never use `Bars.Count - 1` for signal logic.
- **M5 only.** The `6/N` speed conversion assumes 6 bars = 30 minutes. `StartupCheck` stops the bot
  on any other timeframe.
- **Two clocks.** The VWAP resets at 06:00 daily and Monday 06:00 weekly (`VwapPeriod`), and every
  bar accumulates. Orders follow a different clock (`TradingSession`). Don't merge them.
- **Departure's direction comes from daily vs weekly only**, never from the close: the pullback it
  waits for pushes the close back through the daily VWAP.
- **Opposite-side recovery counts closes, not wicks.** A long counts preceding closes below the
  daily VWAP; a short counts preceding closes above it. The signal bar is excluded, and the
  lookback never crosses the 06:00 daily-VWAP reset.
- **Touching counts only the candles the pattern uses:** pinbar 1, engulfing 2, fractal and
  harami 3.
- **Harami** (`current [0]`, `previous [1]`, `earlier [2]`): `earlier` strictly contains `previous`
  by high and low. Then `current.Close > previous.High` is Buy, `current.Close < previous.Low` is
  Sell, anything else None. Candle body direction plays no part. Don't revert to the old
  "previous contains current, reverse the parent body" rule.
- **`HanJinSignals26` is a faithful port of a Pine library.** Change it only when the original
  changes.

## Architecture

The layers are already right; keep them and don't add more.

| Folder | Holds |
| --- | --- |
| `VWAPTrade.cs` | Robot lifecycle and wiring only: parameters, `OnStart` builds three pipelines, the per-bar flow above. No rules. |
| `Vwap/` | VWAP accumulation and periods, order window, `VwapStack`, `VwapStrongMetrics`, `OppositeDailyVwapGate`, `DepartureTracker`, `StartupCheck`, plus the readers `VwapSeries`, `OppositeDailyVwapFeed` and `DepartureFeed`. |
| `Indicators/` | ATR14 on M5 and H1. |
| `Signals/` | `SignalDetector` (reads the bar, finds the signal), `LevelPatternMatcher`, `HanJinSignals26`. |
| `Orders/` | `OrderPlanner` (pure sizing), `OrderExecutor` (gates + placing), `TradeJournal`, `BreakevenProtector`, `TradeDirectionGate`, `TradeResultR`. |
| `Risk/` | `RiskBudget`. |
| `TradeLog/` | Trade CSV (`TradeCsvColumns`, `TradeCsvLogger`, `TradeCsvFile`, `TradeCsvMigrator`), `debug.csv` (`StrongSignalCsvColumns`, `StrongSignalCsvLogger`), shared `CsvCell`. |
| `Chart/` | `VwapLines`, `SignalMarkers`. Drawing only. |
| `Models/` | Every data type, suffixed `Model`. |

Conventions:

- **Pure by default.** A class without `using cAlgo.API` is pure and unit tested; keep new rules
  that way. `CAlgoSymbolModel` is the only `Models/` file that touches cAlgo.
- **Reader / rule pairs.** When a rule needs market data, one class reads `Bars` and another holds
  the rule: `VwapSeries`/`VwapCalculator`, `OppositeDailyVwapFeed`/`OppositeDailyVwapGate`,
  `DepartureFeed`/`DepartureTracker`.
- **Keep separate types separate:**
  - `SignalModel` says what the bar showed; `EntryOutcomeModel` says what became of it.
  - `SignalSideModel` (None/Buy/Sell) is what a bar suggests; `TradeDirectionModel` (Long/Short)
    is the side an order is sent with.
  - `OrderPlanModel` is sizing only, plus references to its signal and settings. Don't copy signal
    fields into it.
- **Keep display out of logic.** Drawing, CSV text and `Print` messages stay out of the rule classes.

## Working rules

- **Keep the Robot class clean**, and give each responsibility its own class. Drawing, signal
  detection, order execution and CSV writing never share a class.
- **Expose only real strategy parameters.** Visual constants (marker offsets, font size, line
  styles) stay hard-coded.
- **Prefer simple C#** over abstractions. No interface without a second implementation or a
  real testing need. No new NuGet dependencies.
- **Comments:** explain why, in English. Existing Chinese comments stay unless you are already
  editing that line; don't bulk-translate.
- **User-facing text is Chinese on purpose:** parameter labels, CSV headers, CSV values such as
  多/空, 盈利/亏损 and the `拦截闸门` reasons. Match it.
- **Trade CSV columns:** add new ones at the end only, and never rename one. `TradeCsvMigrator`
  upgrades old files by padding on the right. `debug.csv` has no migration: a changed header
  simply starts the file over.

## Coding style

Java-style braces: the opening brace goes on the same line. Single-statement `if`s have no braces.

```csharp
public EntryOutcomeModel TryEnter(SignalModel signalModel) {
    if (signalModel.FailedFilter != EntryGateModel.None)
        return EntryOutcomeModel.Blocked(signalModel.FailedFilter);
}
```

Don't reformat touched code to Allman braces. Name things for what they are (`OrderPlanner`,
`SignalMarkers`), not `Helper` or `Manager`.

## Build, test, run

```bash
dotnet build "VWAPTrade.sln"                                   # Debug
dotnet build "VWAPTrade.sln" -c Release
./scripts/test.sh                                              # Release build + all unit tests
dotnet test "tests/VWAPTrade.Tests/VWAPTrade.Tests.csproj"     # tests only
```

- **The build output** is `VWAPTrade/bin/<Config>/net6.0/VWAPTrade.algo`, which cTrader loads.
- **Paths:** spaces in them are intentional; always quote them.
- **The test project** (`net10.0`, xUnit) isn't in the solution. It links the pure source files
  with `<Compile Include>`, never a project reference. Link every new pure file there.
- **Runtime checks** happen in cTrader's backtester: build → refresh the bot in cTrader →
  backtest → read the Log tab and the CSVs.

## cTrader behaviour worth knowing

- **Parameter defaults:** `[Parameter(DefaultValue = …)]` only affects new instances. Existing
  instances keep their saved values; recreate the instance to see a new default.
- **Instances:** each running instance (symbol/timeframe) has its own state and its own `OnStart`.
- **Access rights:** `AccessRights.FullAccess` is required because the bot writes its CSVs.
  Output goes under `~/Documents`:
  - `trading_reports` for backtests
  - `simulate_trading_reports` for demo
  - `release_trading_reports` for live
  
  The trade CSV is `VWAPTrades.csv` by default, with `debug.csv` next to it. If cTrader reports a
  sync conflict over full access, keep the local source.
- **Chart objects** persist after the bot stops; nothing clears them in `OnStop`.
