using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

// Reads the closed bars before a signal for OppositeDailyVwapGate. It never crosses the 06:00
// VWAP-day boundary: yesterday's price action must not suppress today's first valid signal.
public class OppositeDailyVwapFeed {
    private readonly Bars _chartBars;
    private readonly VwapSeries _vwapSeries;

    public OppositeDailyVwapFeed(Bars chartBars, VwapSeries vwapSeries) {
        _chartBars = chartBars;
        _vwapSeries = vwapSeries;
    }

    // Returns newest first and excludes the signal bar itself. The Strong rule already puts that
    // bar on the trade side of the daily VWAP, so including it would dilute this opposite-side rule.
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
