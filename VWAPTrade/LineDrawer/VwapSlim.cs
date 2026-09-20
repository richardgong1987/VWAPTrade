using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

// docs/vwap-v5-slim.pine 的 cTrader 版本，画三条线：当日 VWAP、当周 VWAP，以及前一交易日收盘时
// 的当日 VWAP（在新的一天里保持不变）。取值全部来自 VwapSeries，这里只负责画。
//
// cBot 里没有 Pine 的 plot()，只能自己画图形对象，所以每两根 K 线之间画一段 trend line，且只画图表
// 可见区间。VWAP 本身始终从最早的一根 K 线累积，画多少根不影响数值。
public class VwapSlim {
    private const string Prefix = "VWAP_SLIM_";

    // 与 Pine 脚本同色：当日与前日 #FFEB3B，当周 #9C27B0。
    private static readonly Color DailyColor = Color.FromHex("#FFEB3B");
    private static readonly Color WeeklyColor = Color.FromHex("#9C27B0");

    private static readonly SeriesStyle DailyStyle = new("D", DailyColor, thickness: 1, LineStyle.Solid);
    private static readonly SeriesStyle WeeklyStyle = new("W", WeeklyColor, thickness: 2, LineStyle.Solid);
    private static readonly SeriesStyle PreviousDailyStyle = new("PD", DailyColor, thickness: 1, LineStyle.Dots);

    // 对应 Pine 的 timeframe.isdwm：日线及以上不画，那种周期上的日内 VWAP 没有意义。
    private static readonly TimeFrame[] HiddenTimeFrames = {
        TimeFrame.Daily, TimeFrame.Day2, TimeFrame.Day3, TimeFrame.Weekly, TimeFrame.Monthly,
        TimeFrame.HeikinDaily, TimeFrame.HeikinDay2, TimeFrame.HeikinDay3, TimeFrame.HeikinWeekly, TimeFrame.HeikinMonthly
    };

    private readonly Chart _chart;
    private readonly VwapSeries _series;
    private readonly List<string> _objectNames = new();

    public VwapSlim(Chart chart, VwapSeries series) {
        _chart = chart;
        _series = series;
    }

    public void Draw() {
        Clear();

        if (IsHiddenTimeFrame(_chart.TimeFrame))
            return;

        // 每段连接 barIndex-1 与 barIndex，所以从第 1 根开始；右端不超过已算完的最后一根。
        int firstSegment = Math.Max(_chart.FirstVisibleBarIndex, 1);
        int lastSegment = Math.Min(_chart.LastVisibleBarIndex, _series.Count - 1);

        for (int barIndex = firstSegment; barIndex <= lastSegment; barIndex++) {
            VwapSampleModel from = _series[barIndex - 1];
            VwapSampleModel to = _series[barIndex];

            DrawSegment(DailyStyle, barIndex, from.Daily, to.Daily);
            DrawSegment(WeeklyStyle, barIndex, from.Weekly, to.Weekly);

            // 前一日 VWAP 在一天之内是常数，只在开新的一天时跳变。那一段竖线没有意义，跳过。
            if (!to.IsDailySessionStart)
                DrawSegment(PreviousDailyStyle, barIndex, from.PreviousDaily, to.PreviousDaily);
        }
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }

    private void DrawSegment(SeriesStyle style, int barIndex, double from, double to) {
        if (double.IsNaN(from) || double.IsNaN(to))
            return;

        string name = $"{Prefix}{style.Key}_{barIndex}";
        _chart.DrawTrendLine(name, barIndex - 1, from, barIndex, to, style.Color, style.Thickness, style.LineStyle);
        _objectNames.Add(name);
    }

    private static bool IsHiddenTimeFrame(TimeFrame timeFrame) {
        foreach (TimeFrame hidden in HiddenTimeFrames) {
            if (timeFrame.Equals(hidden))
                return true;
        }

        return false;
    }

    // 一条线的外观：图形对象名前缀用的 Key，加上颜色/粗细/线型。
    private sealed class SeriesStyle {
        public SeriesStyle(string key, Color color, int thickness, LineStyle lineStyle) {
            Key = key;
            Color = color;
            Thickness = thickness;
            LineStyle = lineStyle;
        }

        public string Key { get; }
        public Color Color { get; }
        public int Thickness { get; }
        public LineStyle LineStyle { get; }
    }
}
