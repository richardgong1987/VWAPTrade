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

    // 图上最多保留这么多根 K 线的线段，太老的自动删掉，免得长时间回测堆出几万个图形对象。
    private const int MaxDrawnBars = 1000;

    private readonly Chart _chart;
    private readonly VwapSeries _series;

    // 按画出的先后排队，超出上限就从最老的开始删。
    private readonly Queue<string> _objectNames = new();

    // 下一根要补画的 K 线下标。已经画过的线段不会再动，所以每根 K 线只新增两三个图形对象。
    private int _nextBarIndex;

    public VwapSlim(Chart chart, VwapSeries series) {
        _chart = chart;
        _series = series;
    }

    // 已经画在图上的图形对象数量，启动时打进日志，好确认到底有没有画出来。
    public int DrawnObjectCount => _objectNames.Count;

    public void Draw() {
        if (IsHiddenTimeFrame(_chart.TimeFrame)) {
            Clear();
            return;
        }

        // 每段连接 barIndex-1 与 barIndex，所以从第 1 根开始。下标用的是 VwapSeries 的下标，
        // 也就是这个 cBot 自己的 Bars 下标 —— 不能用 Chart 的可见区间下标，回测时图表里还有
        // 回测开始之前的历史 K 线，两套下标对不上，会一段都画不出来。
        int firstBarIndex = Math.Max(_series.Count - MaxDrawnBars, 1);

        for (int barIndex = Math.Max(_nextBarIndex, firstBarIndex); barIndex < _series.Count; barIndex++) {
            DrawBar(barIndex);
        }

        _nextBarIndex = Math.Max(_series.Count, 1);
        TrimOldestObjects();
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
        _nextBarIndex = 0;
    }

    private void DrawBar(int barIndex) {
        VwapSampleModel from = _series[barIndex - 1];
        VwapSampleModel to = _series[barIndex];

        DrawSegment(DailyStyle, barIndex, from.Daily, to.Daily);
        DrawSegment(WeeklyStyle, barIndex, from.Weekly, to.Weekly);

        // 前一日 VWAP 在一天之内是常数，只在开新的一天时跳变。那一段竖线没有意义，跳过。
        if (!to.IsDailySessionStart)
            DrawSegment(PreviousDailyStyle, barIndex, from.PreviousDaily, to.PreviousDaily);
    }

    private void TrimOldestObjects() {
        while (_objectNames.Count > MaxDrawnBars * 3) {
            _chart.RemoveObject(_objectNames.Dequeue());
        }
    }

    private void DrawSegment(SeriesStyle style, int barIndex, double from, double to) {
        if (double.IsNaN(from) || double.IsNaN(to))
            return;

        string name = $"{Prefix}{style.Key}_{barIndex}";
        _chart.DrawTrendLine(name, barIndex - 1, from, barIndex, to, style.Color, style.Thickness, style.LineStyle);
        _objectNames.Enqueue(name);
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
