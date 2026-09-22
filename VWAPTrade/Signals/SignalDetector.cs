using System;
using cAlgo.API;

namespace cAlgo.Robots;

// 每根收盘 K 线问一次：这一根能不能做？能做就返回一个信号，不能就返回 null。
//
// 闸门按顺序排，任何一道过不了就没有信号：
//
//   排列/距离/速度/扩口（VwapStack） → 先离开再回踩（DepartureTracker） → 形态长在日 VWAP 上（LevelPatternMatcher）
//
// 方向许可（LongOnly / ShortOnly）不在这里 —— 那是下单前的最后一道，见 OrderExecutor。
// 这里只读数据、只排闸门，规则本身都在各自的类里。
public class SignalDetector {
    // 形态最多要看三根 K 线（current / previous / earlier），而 OnBar() 里最后一根收盘 K 线
    // 的下标是 Count-2，所以至少要有 4 根才读得到 earlier。
    private const int MinimumBarCount = 4;

    // 写进订单标签与交易 CSV 的「关键位」列。关键位只有一个：图上那条黄线，也就是日 VWAP。
    private const string LevelName = "VWAP";

    private readonly Bars _chartBars;
    private readonly VwapSeries _vwapSeries;
    private readonly Atr14Pair _atr14;
    private readonly TradeSettingsModel _settings;
    private readonly DepartureFeed _departureFeed;
    private readonly DepartureTracker _departureTracker;

    public SignalDetector(Bars chartBars, VwapSeries vwapSeries, Atr14Pair atr14, TradeSettingsModel settings,
        DepartureFeed departureFeed, DepartureTracker departureTracker) {
        _chartBars = chartBars;
        _vwapSeries = vwapSeries;
        _atr14 = atr14;
        _settings = settings;
        _departureFeed = departureFeed;
        _departureTracker = departureTracker;
    }

    // 没有信号就返回 null。
    public SignalModel DetectOnClosedBar() {
        int closedBarIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()

        if (_chartBars.Count < MinimumBarCount || closedBarIndex >= _vwapSeries.Count)
            return null;

        _departureFeed.CatchUpTo(closedBarIndex);

        CandleModel current = ReadCandle(closedBarIndex);
        VwapSampleModel vwap = _vwapSeries[closedBarIndex];
        VwapStrongReadingModel strong = ReadStrong(closedBarIndex, current, vwap);

        SignalSideModel side = VwapStack.ResolveSide(strong, _settings.VwapFilters);

        if (side == SignalSideModel.None)
            return null;

        // Leave first, then pull back: a pattern on the daily VWAP that never left it is not a
        // V2 trade. Off when DepartureMin = 0, and then this line changes nothing.
        if (!_departureTracker.IsAllowed(side))
            return null;

        // 关键位是日 VWAP —— 图上那条黄线；周 VWAP 只当方向闸门，不作为关键位。
        var level = new TradeLevelModel(LevelName, side, vwap.Daily, _settings.RiskPct, _settings.TakeProfitR);
        SignalModel signal =
            LevelPatternMatcher.Match(current, ReadCandle(closedBarIndex - 1), ReadCandle(closedBarIndex - 2), level);

        if (signal == null)
            return null;

        Stamp(signal, closedBarIndex, strong, side);
        return signal;
    }

    // 开仓当时的读数一路带进交易 CSV，日后拿 GapX / SlopeRateX / Departure 对着盈亏复盘。
    private void Stamp(SignalModel signal, int closedBarIndex, VwapStrongReadingModel strong, SignalSideModel side) {
        signal.BarIndex = closedBarIndex;
        signal.BarTime = _chartBars.OpenTimes[closedBarIndex];
        signal.Strong = strong;
        signal.Metrics = VwapStrongMetrics.Compute(strong, side);
        signal.Departure = _departureTracker.CreateSnapshot();
    }

    private VwapStrongReadingModel ReadStrong(int closedBarIndex, CandleModel current, VwapSampleModel vwap) {
        VwapSampleModel lookback = ReadLookbackSample(closedBarIndex);

        // ATR 按「信号 K 线的收盘时刻」取值。这根收线的同时下一根就开出来了，所以它的开盘时间
        // 就是本根的收盘时间 —— closedBarIndex 是 Count-2，下一根必定存在。
        DateTime closeTime = _chartBars.OpenTimes[closedBarIndex + 1];

        return new VwapStrongReadingModel {
            Close = current.Close,
            DailyVwap = vwap.Daily,
            WeeklyVwap = vwap.Weekly,
            DailyVwapBefore = lookback?.Daily ?? double.NaN,
            WeeklyVwapBefore = lookback?.Weekly ?? double.NaN,
            Atr14M5 = _atr14.GetM5(closeTime),
            Atr14H1 = _atr14.GetH1(closeTime),
            LookbackN = _settings.VwapSlopeLookbackBars,
            SelectedAtrPeriod = _settings.Atr14Source
        };
    }

    // 回看那一根必须和当前在同一个 VWAP 日（06:00 日切）：日 VWAP 每天 06:00 清零，跨过去相减
    // 得到的是清零那一下的跳变，不是斜率。取不到就返回 null，斜率闸门开着时会把这根 K 线拦下。
    // 下单从 10:30 才开始，而日切在 06:00，所以正常情况下回看那一根总在同一天里。
    private VwapSampleModel ReadLookbackSample(int closedBarIndex) {
        int lookbackIndex = closedBarIndex - _settings.VwapSlopeLookbackBars;

        if (lookbackIndex < 0)
            return null;

        DateTime currentDay = VwapPeriod.GetDayStart(_vwapSeries[closedBarIndex].OpenTime);
        DateTime lookbackDay = VwapPeriod.GetDayStart(_vwapSeries[lookbackIndex].OpenTime);

        return currentDay == lookbackDay ? _vwapSeries[lookbackIndex] : null;
    }

    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }
}
