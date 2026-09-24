# VWAPTrade

A cTrader cBot (C#, cAlgo API, `net6.0`) that trades the **VWAP Strong** strategy on **M5**
charts.

The daily VWAP is the key level. When a closed bar forms a candle pattern (pinbar, engulfing,
fractal or harami) that touches it, and the VWAP filters agree, the bot enters at market. It
sizes each trade so that hitting the stop loses a fixed share of account equity.

Strategy spec: `docs/VWAP_Strong_V2.pdf`. TradingView reference indicator for the VWAP lines:
`docs/vwap-v5-slim.pine`.

## How it trades

**M5 only.** The bot stops on start-up if the chart is any other timeframe.

**Japan time.** Two separate clocks:

- **VWAP periods.** The daily VWAP runs 06:00 → 06:00 the next morning. The weekly VWAP starts
  Monday 06:00. Every bar counts, including Mondays and pre-open hours.
- **Order window.** New orders only between 10:30 and 06:00 the next morning, Tuesday to Friday.
  Positions that are already open run to their stop or target.

**Entry gates**, checked in this order on each closed bar:

1. **Stack.** Long only if `close > daily > weekly`; short only if `close < daily < weekly`.
2. **Distance.** The daily–weekly VWAP gap, in ATR, must reach a minimum.
3. **Speed.** The daily VWAP's slope, scaled to 30 minutes and measured in ATR, must reach a
   minimum.
4. **Gap change.** The change in that gap must sit inside a set range. Optional.
5. **Slope efficiency.** The speed from gate 3 divided by the gap from gate 2 must reach a
   minimum. It catches a trend that has already run far but whose daily VWAP has gone flat.
   Spec: `docs/VWAP.docx`.
6. **Expansion efficiency.** The gap change from gate 4 divided by the gap from gate 2 must reach
   a minimum. It catches a gap that is still widening, but too slowly for how wide it already is.
   It works whether or not gate 4 is switched on. Spec: `docs/VWAP2.docx`.
7. **Opposite-side recovery.** Count the preceding closed bars in the same daily-VWAP period. For
   a long, closes below the daily VWAP count; for a short, closes above it count. If at least the
   configured number are on that opposite side, do not enter. The signal bar itself is excluded.
8. **Departure.** The price must first move clearly away from the daily VWAP before a pattern
   touching it may be traded.
9. **Direction.** All, long only, or short only.

Gates 2, 3, 5, 6 and 8 are off when their threshold is 0. Gate 7 is off when its block count is 0.
Gate 4 has its own on/off switch.

**Order.** The stop sits a few ticks beyond the pattern's own stop level. The target is a fixed
multiple of that risk (R). An optional breakeven stop moves in once the trade is far enough in
profit.

## Parameters

The labels are the ones shown in cTrader.

| cTrader label | What it does |
| --- | --- |
| 订单标签 | Label put on every order the bot places. |
| 风险% | Share of equity one trade may lose at its stop. 0 = never trades. |
| 止盈目标 | Take-profit, in R (multiples of the stop distance). |
| **风控配置** | |
| 止损偏移点数 | Ticks the stop sits beyond the pattern's stop level. |
| 保护止损触发R | Profit, in R, at which the breakeven stop kicks in. 0 = off. |
| 保护止损偏移点数 | Tick offset for the breakeven stop. |
| **VWAP过滤** | |
| VWAP间距最小值 | Gate 2: minimum daily–weekly gap, in ATR. 0 = off. |
| VWAP斜率速度最小值 | Gate 3: minimum 30-minute-scaled slope of the daily VWAP, in ATR. 0 = off. |
| 斜率回看K线数 N | Lookback, in bars, for the slope and the gap change. |
| ATR归一周期 | Which ATR14 the gates divide by: M5 or H1. Both are always logged. |
| 扩口变化过滤 | Turns gate 4 on. |
| 扩口变化最小值 / 最大值 | Gate 4: allowed range for the 30-minute-scaled gap change. |
| 斜率效率最小值 | Gate 5: minimum `SlopeRateX_Selected / GapX_Selected`. 0 = off. 0.01 is the candidate still being validated. |
| 扩口效率最小值 | Gate 6: minimum `GapChangeRateX30_Selected / GapX_Selected`. 0 = off. 0.0045 is the candidate still being validated. |
| **反向黄线过滤** | |
| 信号前回看K线数 | How many closed bars before the signal to inspect, without crossing the 06:00 daily-VWAP reset. |
| 反向侧K线阻断根数 | Gate 7: block a long when this many closes are below the yellow daily VWAP, or a short when this many are above it. 0 = off. |
| **Departure离开确认** | |
| 离开最小距离 | Gate 8: how far, in ATR, the close must move away from the daily VWAP. 0 = off. |
| 离开连续确认K线数 | Consecutive closes needed to confirm that move. |
| 离开后最大等待K线数 | Bars to wait for the pullback after that. 0 = no limit. |
| **交易方向** | |
| 交易方向 | All, LongOnly or ShortOnly. |
| **开发调试** | |
| 启动时清空交易记录CSV | Empty the trade CSV when the bot starts. |
| debug调试 | Call `Debugger.Launch()` in `OnStart`. See [Debugging](#debugging). |
| 输出文件名 | Trade CSV file name. An absolute path is used as is. |

## Trade log

Every trade is written to a CSV in `~/Documents`, in a folder picked by how the bot is running:

| Running as | Folder |
| --- | --- |
| Backtest | `~/Documents/trading_reports` |
| Demo account | `~/Documents/simulate_trading_reports` |
| Live account | `~/Documents/release_trading_reports` |

The full path is printed in the cTrader log at start-up, on the `CSV logger path` line.

### debug.csv: every Strong signal, traded or not

`debug.csv` sits next to the trade CSV. It gets one row for every **Strong signal**, whether it
traded or not:

- **Strong:** the stack alone points one way: `close > daily > weekly` (long) or
  `close < daily < weekly` (short). The distance, speed, gap-change, slope-efficiency,
  expansion-efficiency, opposite-side recovery and Departure filters do not decide Strong; they
  are gates the row reports on.
- **Signal:** a candle pattern for that side (e.g. a bearish engulfing on a short bar) touches
  the daily VWAP, the yellow line.

Each row has:

- **The signal:** bar time, decision time, whether that time is inside the order window
  (`在开仓时段`), symbol, signal name (e.g. `S_Eng_1`) and side.
- **What became of it:** `下单结果` is 已下单 or 未下单. A traded row has its `持仓ID`, the same
  ID as in the trade CSV, so the two files can be joined. An untraded row has `拦截闸门`, the
  first gate that blocked it, and `拦截详情`, the planner's reject reason or the broker's error
  when there is one.
- **What the gates compared:** close, high, low, pattern stop, VWAPs, both ATRs, and every
  filter reading next to its threshold (GapX, SlopeRateX, gap change, SlopeEfficiency,
  ExpansionEfficiency, previous closes on the daily VWAP's opposite side, Departure, trade
  direction).

| `拦截闸门` | Gate |
| --- | --- |
| 间距不足 | GapX below `VWAP间距最小值`. |
| 速度不足 | SlopeRateX below `VWAP斜率速度最小值`. |
| 扩口变化超出区间 | Gap change outside its range. |
| 斜率效率不足 | SlopeEfficiency below `斜率效率最小值`, or no usable value (e.g. GapX ≤ 0). |
| 扩口效率不足 | ExpansionEfficiency below `扩口效率最小值`, or no usable value (e.g. GapX ≤ 0). |
| 黄线反向侧K线过多 | A long had at least the configured number of preceding closes below the daily VWAP, or a short had that many above it. |
| 未完成离开确认 | Price hasn't left the daily VWAP and come back yet. |
| 不在开仓时段 | Outside the order window. |
| 交易方向不允许 | `交易方向` forbids this side. |
| 已有持仓 | A position is already open. |
| 下单方案被拒 | Sizing failed, e.g. volume below the broker minimum. |
| 券商拒单 | The broker refused the order. |

`启动时清空交易记录CSV` resets `debug.csv` together with the trade CSV. A file written by a build
with different columns starts over instead of being upgraded.

## Build and test

Needs the .NET 10 SDK and cTrader desktop.

```bash
dotnet build "VWAPTrade.sln"              # Debug
dotnet build "VWAPTrade.sln" -c Release   # Release
./scripts/test.sh                         # Release build + all unit tests
```

The build writes `VWAPTrade.algo` to `VWAPTrade/bin/<Config>/net6.0/`. Load or refresh it in
cTrader, then backtest.

The unit tests (`tests/VWAPTrade.Tests`, xUnit, `net10.0`) aren't part of the solution. They
compile the pure source files directly, so they never need cTrader.

## Debugging

Attach Rider to the process cTrader runs the bot in. Step-by-step guide:
[docs/debugging-in-rider.md](docs/debugging-in-rider.md).

## Project layout

| Folder | What's in it |
| --- | --- |
| `VWAPTrade/VWAPTrade.cs` | The cBot itself: reads parameters and wires the pieces together. |
| `VWAPTrade/Vwap/` | VWAP periods, order window, entry filters, start-up checks. |
| `VWAPTrade/Indicators/` | ATR14 on M5 and H1. |
| `VWAPTrade/Signals/` | Runs the gates and finds the candle pattern on the key level. |
| `VWAPTrade/Orders/` | Sizing, stops, direction gate, placing orders, breakeven. |
| `VWAPTrade/Risk/` | How much one trade may lose. |
| `VWAPTrade/TradeLog/` | The trade CSV: columns, writing, upgrading old files. |
| `VWAPTrade/Chart/` | VWAP lines and entry markers on the chart. |
| `VWAPTrade/Models/` | Data types. |
| `tests/VWAPTrade.Tests/` | Unit tests for the pure classes. |

The full module map and design rules are in [CLAUDE.md](CLAUDE.md).
