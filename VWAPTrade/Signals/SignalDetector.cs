using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

public class SignalDetector {
    // 形态最多要看三根 K 线（current / previous / earlier），而 OnBar() 里最后一根收盘 K 线
    // 的下标是 Count-2，所以至少要有 4 根才读得到 earlier。
    private const int MinimumBarCount = 4;

    // 写进订单标签与交易 CSV 的「关键位」列。关键位只有一个：图上那条黄线，也就是日 VWAP。
    private const string LevelName = "VWAP";

    private readonly Bars _chartBars;
    private readonly VwapSeries _vwapSeries;
    private readonly TradeSettingsModel _settings;

    public SignalDetector(Bars chartBars, VwapSeries vwapSeries, TradeSettingsModel settings) {
        _chartBars = chartBars;
        _vwapSeries = vwapSeries;
        _settings = settings;
    }

    // 这根收盘 K 线上最多一个信号：先过方向闸门，再看形态有没有长在日 VWAP 上。
    public List<SignalModel> DetectOnClosedBar() {
        int closedBarIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()

        if (_chartBars.Count < MinimumBarCount || closedBarIndex >= _vwapSeries.Count)
            return new List<SignalModel>();

        CandleModel current = ReadCandle(closedBarIndex);
        VwapSampleModel vwap = _vwapSeries[closedBarIndex];

        // 收盘价与两条 VWAP 没排成一条线，就是趋势没站在任何一边，这根 K 线不做。
        SignalSideModel side = VwapStack.ResolveSide(current.Close, vwap.Daily, vwap.Weekly);

        if (side == SignalSideModel.None)
            return new List<SignalModel>();

        // 关键位是日 VWAP —— 图上那条黄线；周 VWAP 只当方向闸门，不作为关键位。
        var level = new TradeLevelModel(LevelName, side, vwap.Daily, _settings.RiskPct, _settings.TakeProfitR);
        SignalModel signal = MainBiz.Evaluate(current, ReadCandle(closedBarIndex - 1), ReadCandle(closedBarIndex - 2), level);

        if (signal == null)
            return new List<SignalModel>();

        signal.BarIndex = closedBarIndex;
        signal.BarTime = _chartBars.OpenTimes[closedBarIndex];
        return new List<SignalModel> { signal };
    }

    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }
}
