using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// H1 上的 ATR14，用来把 VWAP 的间距和斜率归一（见 VwapStack）。
// 策略跑在 5 分钟图上，所以要按时间去 H1 序列里找对应的那一根。
public class Atr14H1Series {
    private const int Period = 14;

    private readonly Bars _h1Bars;
    private readonly AverageTrueRange _atr;

    public Atr14H1Series(IIndicatorsAccessor indicators, Bars h1Bars) {
        _h1Bars = h1Bars;
        _atr = indicators.AverageTrueRange(h1Bars, Period, MovingAverageType.WilderSmoothing);
    }

    // 取这个时刻之前最后一根「已经收线」的 H1 K 线的 ATR。当前这一小时还没走完，它的 ATR 还会变，
    // 用它会让回测与实盘对不上，所以往前退一根。
    public bool TryGetValue(DateTime time, out double atr) {
        atr = double.NaN;

        int closedBarIndex = _h1Bars.OpenTimes.GetIndexByTime(time) - 1;

        if (closedBarIndex < Period - 1 || closedBarIndex >= _h1Bars.Count)
            return false;

        atr = _atr.Result[closedBarIndex];
        return !double.IsNaN(atr) && !double.IsInfinity(atr) && atr > 0.0;
    }
}
