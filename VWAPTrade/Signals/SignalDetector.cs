using System;
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
    private readonly Atr14H1Series _atr14H1;
    private readonly TradeSettingsModel _settings;

    public SignalDetector(Bars chartBars, VwapSeries vwapSeries, Atr14H1Series atr14H1, TradeSettingsModel settings) {
        _chartBars = chartBars;
        _vwapSeries = vwapSeries;
        _atr14H1 = atr14H1;
        _settings = settings;
    }

    // 这根收盘 K 线上最多一个信号：先过方向闸门，再看形态有没有长在日 VWAP 上。
    public List<SignalModel> DetectOnClosedBar() {
        int closedBarIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()

        if (_chartBars.Count < MinimumBarCount || closedBarIndex >= _vwapSeries.Count)
            return new List<SignalModel>();

        CandleModel current = ReadCandle(closedBarIndex);
        VwapSampleModel vwap = _vwapSeries[closedBarIndex];

        // 排列、间距、斜率三道闸门，任何一道过不了这根 K 线就不做。
        SignalSideModel side = VwapStack.ResolveSide(ReadStrong(closedBarIndex, current, vwap), _settings.VwapGapMin,
            _settings.VwapSlopeMin);

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

    private VwapStrongReadingModel ReadStrong(int closedBarIndex, CandleModel current, VwapSampleModel vwap) {
        return new VwapStrongReadingModel {
            Close = current.Close,
            DailyVwap = vwap.Daily,
            WeeklyVwap = vwap.Weekly,
            DailyVwapBefore = ReadDailyVwapBefore(closedBarIndex),
            Atr14H1 = _atr14H1.TryGetValue(_chartBars.OpenTimes[closedBarIndex], out double atr) ? atr : double.NaN
        };
    }

    // 回看那一根必须和当前在同一场：日 VWAP 每场开盘都清零，跨场相减得到的是清零那一下的跳变，
    // 不是斜率。取不到就返回 NaN，斜率闸门开着时会把这根 K 线拦下（见 VwapStack）。
    private double ReadDailyVwapBefore(int closedBarIndex) {
        int lookbackIndex = closedBarIndex - _settings.VwapSlopeLookbackBars;

        if (lookbackIndex < 0)
            return double.NaN;

        DateTime? currentSession = TradingSession.GetDaySessionStart(_vwapSeries[closedBarIndex].OpenTime);
        DateTime? lookbackSession = TradingSession.GetDaySessionStart(_vwapSeries[lookbackIndex].OpenTime);

        return currentSession == lookbackSession ? _vwapSeries[lookbackIndex].Daily : double.NaN;
    }

    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }
}
