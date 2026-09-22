using System;
using cAlgo.API;

namespace cAlgo.Robots;

// Feeds closed bars to the Departure state machine. Same split as VwapSeries/VwapCalculator:
// this side reads the platform (Bars, VwapSeries, ATR), DepartureTracker holds the rules.
//
// The tracker has to see every closed bar in order, so CatchUpTo walks from the last bar it fed
// to the one asked for — the first call covers the whole loaded history. Without that, the first
// trades of a run would depend on how much history the platform happened to load.
public class DepartureFeed {
    private readonly Bars _chartBars;
    private readonly VwapSeries _vwapSeries;
    private readonly Atr14Pair _atr14;
    private readonly Atr14SourceModel _atr14Source;
    private readonly DepartureTracker _tracker;

    private int _lastFedBarIndex = -1;

    public DepartureFeed(Bars chartBars, VwapSeries vwapSeries, Atr14Pair atr14, Atr14SourceModel atr14Source,
        DepartureTracker tracker) {
        _chartBars = chartBars;
        _vwapSeries = vwapSeries;
        _atr14 = atr14;
        _atr14Source = atr14Source;
        _tracker = tracker;
    }

    // Skipped entirely when the gate is off, so a run with DepartureMin = 0 does not even pay
    // for the ATR lookups.
    public void CatchUpTo(int closedBarIndex) {
        if (!_tracker.IsEnabled)
            return;

        for (int barIndex = _lastFedBarIndex + 1; barIndex <= closedBarIndex; barIndex++) {
            _tracker.Observe(ReadBar(barIndex));
        }

        _lastFedBarIndex = closedBarIndex;
    }

    private DepartureBarModel ReadBar(int barIndex) {
        VwapSampleModel vwap = _vwapSeries[barIndex];

        // The ATR is read at the bar's close, which is the next bar's open time. barIndex never
        // exceeds Count-2, so that next bar exists.
        DateTime closeTime = _chartBars.OpenTimes[barIndex + 1];

        return new DepartureBarModel {
            Close = _chartBars.ClosePrices[barIndex],
            DailyVwap = vwap.Daily,
            WeeklyVwap = vwap.Weekly,
            SelectedAtr = _atr14.GetSelected(closeTime, _atr14Source),
            IsDayPeriodStart = vwap.IsDayPeriodStart
        };
    }
}
