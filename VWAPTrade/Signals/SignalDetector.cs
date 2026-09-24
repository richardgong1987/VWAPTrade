using System;
using cAlgo.API;

namespace cAlgo.Robots;

// Asked once per closed bar. The bar is Strong when the stack alone points one way
// (VwapStack.ResolveSide on close / daily / weekly returns Buy or Sell). A Strong bar with a
// candle pattern for that side touching the daily VWAP (LevelPatternMatcher) is a signal, and it
// comes back with the first signal filter it fails (FailedFilter):
//
//   distance/speed/gap change/slope and expansion efficiency (VwapStack)
//   → recent closes on the opposite daily-VWAP side
//   → leave first, then pull back (DepartureTracker)
//
// Whether it trades is OrderExecutor's call. This class only reads data and orders the filters;
// the rules live in their own classes.
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
    private readonly OppositeDailyVwapFeed _oppositeDailyVwapFeed;

    public SignalDetector(Bars chartBars, VwapSeries vwapSeries, Atr14Pair atr14, TradeSettingsModel settings,
        DepartureFeed departureFeed, DepartureTracker departureTracker, OppositeDailyVwapFeed oppositeDailyVwapFeed) {
        _chartBars = chartBars;
        _vwapSeries = vwapSeries;
        _atr14 = atr14;
        _settings = settings;
        _departureFeed = departureFeed;
        _departureTracker = departureTracker;
        _oppositeDailyVwapFeed = oppositeDailyVwapFeed;
    }

    // Null when the bar is not Strong, or no pattern for the Strong side touches the daily VWAP.
    public SignalModel DetectOnClosedBar() {
        int closedBarIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()

        if (_chartBars.Count < MinimumBarCount || closedBarIndex >= _vwapSeries.Count)
            return null;

        _departureFeed.CatchUpTo(closedBarIndex);

        CandleModel current = ReadCandle(closedBarIndex);
        VwapSampleModel vwap = _vwapSeries[closedBarIndex];
        VwapStrongReadingModel strong = ReadStrong(closedBarIndex, current, vwap);
        SignalSideModel side = VwapStack.ResolveSide(strong.Close, strong.DailyVwap, strong.WeeklyVwap);

        if (side == SignalSideModel.None)
            return null;

        // The key level is the daily VWAP, the yellow line; the weekly VWAP only gates direction.
        var level = new TradeLevelModel(LevelName, side, vwap.Daily);
        SignalModel signal =
            LevelPatternMatcher.Match(current, ReadCandle(closedBarIndex - 1), ReadCandle(closedBarIndex - 2), level);

        if (signal == null)
            return null;

        Stamp(signal, closedBarIndex, strong, side);
        return signal;
    }

    // After a fill the next trade must earn its own departure (V2 section 6.3).
    public void ResetAfterEntry() {
        _departureTracker.ResetAfterEntry();
    }

    // The readings travel on to debug.csv, and to the trade CSV when the signal trades.
    private void Stamp(SignalModel signal, int closedBarIndex, VwapStrongReadingModel strong, SignalSideModel side) {
        signal.BarIndex = closedBarIndex;
        signal.BarTime = _chartBars.OpenTimes[closedBarIndex];
        signal.Strong = strong;
        signal.Metrics = VwapStrongMetrics.Compute(strong, side);
        signal.Departure = _departureTracker.CreateSnapshot();
        OppositeDailyVwapSettingsModel oppositeDailyVwap = _settings.OppositeDailyVwap;
        var precedingBars = oppositeDailyVwap?.IsEnabled == true
            ? _oppositeDailyVwapFeed.ReadBefore(closedBarIndex, oppositeDailyVwap.LookbackBars)
            : null;
        signal.OppositeDailyVwap = OppositeDailyVwapGate.Evaluate(side, oppositeDailyVwap,
            precedingBars);
        signal.FailedFilter = FindFailedFilter(signal.Metrics, side, signal.OppositeDailyVwap);
    }

    private EntryGateModel FindFailedFilter(VwapStrongMetricsModel metrics, SignalSideModel side,
        OppositeDailyVwapSnapshotModel oppositeDailyVwap) {
        EntryGateModel vwapFilter = VwapStack.FindFailedFilter(metrics, _settings.VwapFilters);

        if (vwapFilter != EntryGateModel.None)
            return vwapFilter;

        if (oppositeDailyVwap?.IsBlocked == true)
            return EntryGateModel.OppositeDailyVwap;

        // Leave first, then pull back: a pattern on the daily VWAP that never left it is not a
        // V2 trade. Off when DepartureMin = 0, and then this line changes nothing.
        return _departureTracker.IsAllowed(side) ? EntryGateModel.None : EntryGateModel.Departure;
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
