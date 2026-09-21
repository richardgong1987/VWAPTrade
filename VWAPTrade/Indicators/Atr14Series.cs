using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// ATR14，用来把 VWAP 的间距和斜率归一（见 VwapStack）。用哪个周期由参数决定（见 Atr14SourceModel），
// 所以这里不关心是 M5 还是 H1，只按传进来的那一套 K 线算。
//
// 策略跑在 5 分钟图上，而 ATR 可能取自别的周期，所以要按时间去对应序列里找那一根。
public class Atr14Series {
    private const int Period = 14;

    private readonly Bars _bars;
    private readonly AverageTrueRange _atr;

    public Atr14Series(IIndicatorsAccessor indicators, Bars bars) {
        _bars = bars;
        _atr = indicators.AverageTrueRange(bars, Period, MovingAverageType.WilderSmoothing);
    }

    // 取这个时刻所在那一根的前一根 ATR，也就是最后一根「肯定已经收线」的。
    //
    // 取 H1 时，5 分钟图上的 10:35 落在 10:00 那根小时线中间，它还没走完、ATR 还会变，用它会让回测
    // 与实盘对不上，所以往前退一根。取 M5 时这一退会让数值晚一根 —— ATR14 是 14 根的均值，差一根
    // 对归一没有实质影响，换来的是无论选哪个周期都绝不会用到未来数据。
    public bool TryGetValue(DateTime time, out double atr) {
        atr = double.NaN;

        int closedBarIndex = _bars.OpenTimes.GetIndexByTime(time) - 1;

        if (closedBarIndex < Period - 1 || closedBarIndex >= _bars.Count)
            return false;

        atr = _atr.Result[closedBarIndex];
        return !double.IsNaN(atr) && !double.IsInfinity(atr) && atr > 0.0;
    }
}
