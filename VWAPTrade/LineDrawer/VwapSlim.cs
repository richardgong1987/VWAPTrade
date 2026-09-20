using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

// Draw the daily, weekly and previous-day VWAP from the shared closed-bar series.
// Use source timestamps: chart and cBot bar indexes can differ during backtesting.
public class VwapSlim {
    private const string Prefix = "VWAP_SLIM_";
    private static readonly Color DailyColor = Color.FromArgb(255, 255, 235, 59);
    private static readonly Color WeeklyColor = Color.FromArgb(255, 156, 39, 176);

    private static readonly SeriesStyle DailyStyle = new("D", DailyColor, thickness: 10, LineStyle.Solid);
    private static readonly SeriesStyle WeeklyStyle = new("W", WeeklyColor, thickness: 14, LineStyle.Solid);
    private static readonly SeriesStyle PreviousDailyStyle = new("PD", DailyColor, thickness: 10, LineStyle.Dots);

    private static readonly TimeFrame[] HiddenTimeFrames = {
        TimeFrame.Daily, TimeFrame.Day2, TimeFrame.Day3, TimeFrame.Weekly, TimeFrame.Monthly,
        TimeFrame.HeikinDaily, TimeFrame.HeikinDay2, TimeFrame.HeikinDay3, TimeFrame.HeikinWeekly, TimeFrame.HeikinMonthly
    };

    private const int MaxDrawnBars = 1000;

    private readonly Chart _chart;
    private readonly VwapSeries _series;

    private readonly Queue<string> _objectNames = new();

    private int _nextBarIndex;

    public VwapSlim(Chart chart, VwapSeries series) {
        _chart = chart;
        _series = series;
    }

    public int DrawnObjectCount => _objectNames.Count;

    public void Draw() {
        if (IsHiddenTimeFrame(_chart.TimeFrame)) {
            Clear();
            return;
        }

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

        // Pine circles do not connect the reset between two sessions.
        if (!to.IsDailySessionStart)
            DrawSegment(DailyStyle, barIndex, from.Daily, to.Daily);

        if (!TradingSession.IsNewWeekSession(to.OpenTime, from.OpenTime))
            DrawSegment(WeeklyStyle, barIndex, from.Weekly, to.Weekly);

        if (!to.IsDailySessionStart)
            DrawSegment(PreviousDailyStyle, barIndex, from.PreviousDaily, to.PreviousDaily);
    }

    private void TrimOldestObjects() {
        while (_objectNames.Count > MaxDrawnBars * 3) {
            _chart.RemoveObject(_objectNames.Dequeue());
        }
    }

    private void DrawSegment(SeriesStyle style, int barIndex, double from, double to) {
        if (double.IsNaN(from) || double.IsNaN(to) || double.IsInfinity(from) || double.IsInfinity(to))
            return;

        string name = $"{Prefix}{style.Key}_{barIndex}";
        DateTime fromTime = _series[barIndex - 1].OpenTime;
        DateTime toTime = _series[barIndex].OpenTime;
        _chart.DrawTrendLine(name, fromTime, from, toTime, to, style.Color, style.Thickness, style.LineStyle);
        _objectNames.Enqueue(name);
    }

    private static bool IsHiddenTimeFrame(TimeFrame timeFrame) {
        foreach (TimeFrame hidden in HiddenTimeFrames) {
            if (timeFrame.Equals(hidden))
                return true;
        }

        return false;
    }

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
