using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

// Reads the closed bars before a signal for LongBelowDailyVwapGate. It never crosses the 06:00
// VWAP-day boundary: yesterday's price action must not suppress today's first valid long.
public class LongBelowDailyVwapFeed {
    private readonly Bars _chartBars;
    private readonly VwapSeries _vwapSeries;

    public LongBelowDailyVwapFeed(Bars chartBars, VwapSeries vwapSeries) {
        _chartBars = chartBars;
        _vwapSeries = vwapSeries;
    }

    // Returns newest first and excludes the signal bar itself. The Strong rule already requires
    // that bar to close above the daily VWAP for a long, so including it would dilute the rule.
    public IReadOnlyList<RecentDailyVwapBarModel> ReadBefore(int signalBarIndex, int lookbackBars) {
        var bars = new List<RecentDailyVwapBarModel>();

        if (lookbackBars < 1 || signalBarIndex < 1 || signalBarIndex >= _vwapSeries.Count)
            return bars;

        DateTime signalDayStart = VwapPeriod.GetDayStart(_vwapSeries[signalBarIndex].OpenTime);

        for (int barIndex = signalBarIndex - 1; barIndex >= 0 && bars.Count < lookbackBars; barIndex--) {
            if (VwapPeriod.GetDayStart(_vwapSeries[barIndex].OpenTime) != signalDayStart)
                break;

            bars.Add(new RecentDailyVwapBarModel(_chartBars.ClosePrices[barIndex], _vwapSeries[barIndex].Daily));
        }

        return bars;
    }
}
