using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlSignalDetector {
    // 形态最多要看三根 K 线（current / previous / earlier），而 OnBar() 里最后一根收盘 K 线
    // 的下标是 Count-2，所以至少要有 4 根才读得到 earlier。
    private const int MinimumBarCount = 4;

    private readonly Bars _chartBars;
    private readonly IReadOnlyList<TradeLevelModel> _levels;

    public PdhpdlSignalDetector(Bars chartBars, IReadOnlyList<TradeLevelModel> levels) {
        _chartBars = chartBars;
        _levels = levels;
    }

    // 返回这根收盘 K 线上命中的全部信号，每一档价位最多一个。
    public List<PdhpdlSignalModel> DetectOnClosedBar() {
        if (_chartBars.Count < MinimumBarCount)
            return new List<PdhpdlSignalModel>();

        int closedBarIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()
        CandleModel current = ReadCandle(closedBarIndex);
        CandleModel previous = ReadCandle(closedBarIndex - 1);
        CandleModel earlier = ReadCandle(closedBarIndex - 2);

        List<PdhpdlSignalModel> signals = MainBiz.Evaluate(current, previous, earlier, _levels);

        foreach (PdhpdlSignalModel signal in signals) {
            signal.BarIndex = closedBarIndex;
            signal.BarTime = _chartBars.OpenTimes[closedBarIndex];
        }

        return signals;
    }

    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }
}
