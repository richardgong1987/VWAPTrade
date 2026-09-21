using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// ATR14，用来把 VWAP 的间距和斜率归一（见 VwapStack）。用哪个周期由参数决定（见 Atr14SourceModel），
// 所以这里不关心是 M5 还是 H1，只按传进来的那一套 K 线算。
//
// 策略跑在 5 分钟图上，而 ATR 可能取自别的周期，所以要按时间去对应序列里找那一根 ——
// 具体取哪一根由 ClosedBarSelector 决定：到评估时刻为止最后一根已经收线的。
public class Atr14Series {
    private const int Period = 14;

    private readonly Bars _bars;
    private readonly AverageTrueRange _atr;

    public Atr14Series(IIndicatorsAccessor indicators, Bars bars) {
        _bars = bars;
        _atr = indicators.AverageTrueRange(bars, Period, MovingAverageType.WilderSmoothing);
    }

    // evaluationTime 是信号 K 线的收盘时刻（也就是 OnBar 触发的那一刻）。取在它之前最后一根
    // 已经收线的 ATR：同周期（M5）落在刚收线的信号 K 线本身，跨周期（H1）落在上一根整点线，
    // 都不会用到还在走的那一根。
    public bool TryGetValue(DateTime evaluationTime, out double atr) {
        atr = double.NaN;

        int containingIndex = _bars.OpenTimes.GetIndexByTime(evaluationTime);
        int closedBarIndex = ClosedBarSelector.ResolveClosedBarIndex(containingIndex, GetOpenTimeOrNull(containingIndex + 1),
            evaluationTime);

        if (closedBarIndex < Period - 1 || closedBarIndex >= _bars.Count)
            return false;

        atr = _atr.Result[closedBarIndex];
        return !double.IsNaN(atr) && !double.IsInfinity(atr) && atr > 0.0;
    }

    private DateTime? GetOpenTimeOrNull(int barIndex) {
        return barIndex >= 0 && barIndex < _bars.Count ? _bars.OpenTimes[barIndex] : (DateTime?)null;
    }
}
