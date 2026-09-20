using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

// docs/vwap-v5-slim.pine 的 cTrader 版本，画三条线：当日 VWAP、当周 VWAP，以及前一交易日收盘时
// 的当日 VWAP（在新的一天里保持不变）。累积口径在 VwapCalculator 里（纯算术，有单元测试）；这里
// 只做 cAlgo 那一侧的事：读 K 线、切分交易日/交易周、画图形对象。
// cAlgo 的 TypicalPrices 就是 Pine 的 hlc3，成交量用 TickVolumes（外汇没有真实成交量）。
//
// cBot 里没有 Pine 的 plot()，只能自己画图形对象，所以每两根 K 线之间画一段 trend line，且只画图表
// 可见区间。VWAP 本身始终从最早的一根 K 线累积，画多少根不影响数值。
//
// 交易日/交易周的边界按服务器时间切分，而不是 TradingView 的交易所时段，因此在日界附近可能与
// Pine 原脚本差一两根 K 线。
public class VwapSlim {
    private const string Prefix = "VWAP_SLIM_";

    private const int DailyThickness = 1;
    private const int WeeklyThickness = 2;

    // 与 Pine 脚本同色：当日与前日 #FFEB3B，当周 #9C27B0。
    private static readonly Color DailyColor = Color.FromHex("#FFEB3B");
    private static readonly Color WeeklyColor = Color.FromHex("#9C27B0");

    // 对应 Pine 的 timeframe.isdwm：日线及以上不画，那种周期上的日内 VWAP 没有意义。
    private static readonly TimeFrame[] HiddenTimeFrames = {
        TimeFrame.Daily, TimeFrame.Day2, TimeFrame.Day3, TimeFrame.Weekly, TimeFrame.Monthly,
        TimeFrame.HeikinDaily, TimeFrame.HeikinDay2, TimeFrame.HeikinDay3, TimeFrame.HeikinWeekly, TimeFrame.HeikinMonthly
    };

    private readonly Chart _chart;
    private readonly Bars _bars;
    private readonly List<string> _objectNames = new();
    private readonly VwapCalculator _calculator = new();

    // 每根已收线 K 线一个值，下标就是 bar index。
    private readonly List<double> _dailyVwap = new();
    private readonly List<double> _weeklyVwap = new();
    private readonly List<double> _previousDailyVwap = new();
    private readonly List<bool> _dailySessionStarts = new();

    public VwapSlim(Chart chart, Bars bars) {
        _chart = chart;
        _bars = bars;
    }

    public void Draw() {
        AppendClosedBars();
        Clear();

        if (IsHiddenTimeFrame(_chart.TimeFrame))
            return;

        // 每段连接 barIndex-1 与 barIndex，所以从第 1 根开始；右端不超过已算完的最后一根。
        int firstSegment = Math.Max(_chart.FirstVisibleBarIndex, 1);
        int lastSegment = Math.Min(_chart.LastVisibleBarIndex, _dailyVwap.Count - 1);

        for (int barIndex = firstSegment; barIndex <= lastSegment; barIndex++) {
            DrawSegment("D", barIndex, _dailyVwap, DailyColor, DailyThickness, LineStyle.Solid);
            DrawSegment("W", barIndex, _weeklyVwap, WeeklyColor, WeeklyThickness, LineStyle.Solid);

            // 前一日 VWAP 在一天之内是常数，只在开新的一天时跳变。那一段竖线没有意义，跳过。
            if (!_dailySessionStarts[barIndex])
                DrawSegment("PD", barIndex, _previousDailyVwap, DailyColor, DailyThickness, LineStyle.Dots);
        }
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }

    // OnBar 触发时最后一根 K 线刚开盘、值还会变，所以只累积已经收线的部分；每次调用补上新收线的那几根。
    private void AppendClosedBars() {
        int closedBarCount = _bars.Count - 1;

        for (int barIndex = _dailyVwap.Count; barIndex < closedBarCount; barIndex++) {
            AppendBar(barIndex);
        }
    }

    private void AppendBar(int barIndex) {
        VwapSampleModel sample = _calculator.Append(_bars.TypicalPrices[barIndex], _bars.TickVolumes[barIndex],
            IsSessionStart(barIndex, isWeekly: false), IsSessionStart(barIndex, isWeekly: true));

        _dailyVwap.Add(sample.Daily);
        _weeklyVwap.Add(sample.Weekly);
        _previousDailyVwap.Add(sample.PreviousDaily);
        _dailySessionStarts.Add(sample.IsDailySessionStart);
    }

    private bool IsSessionStart(int barIndex, bool isWeekly) {
        if (barIndex == 0)
            return true;

        DateTime current = _bars.OpenTimes[barIndex];
        DateTime previous = _bars.OpenTimes[barIndex - 1];

        if (isWeekly)
            return GetWeekStart(current) != GetWeekStart(previous);

        return current.Date != previous.Date;
    }

    private static DateTime GetWeekStart(DateTime time) {
        int daysSinceMonday = ((int)time.DayOfWeek + 6) % 7;
        return time.Date.AddDays(-daysSinceMonday);
    }

    private void DrawSegment(string seriesKey, int barIndex, List<double> series, Color color, int thickness, LineStyle style) {
        double from = series[barIndex - 1];
        double to = series[barIndex];

        if (double.IsNaN(from) || double.IsNaN(to))
            return;

        string name = $"{Prefix}{seriesKey}_{barIndex}";
        _chart.DrawTrendLine(name, barIndex - 1, from, barIndex, to, color, thickness, style);
        _objectNames.Add(name);
    }

    private static bool IsHiddenTimeFrame(TimeFrame timeFrame) {
        foreach (TimeFrame hidden in HiddenTimeFrames) {
            if (timeFrame.Equals(hidden))
                return true;
        }

        return false;
    }
}
