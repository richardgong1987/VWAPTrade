using System.Collections.Generic;

namespace cAlgo.Robots;

// Long-only recovery gate:
//
//   Among the preceding N closed bars in this VWAP day, at least K closes below the daily VWAP
//   block a new long. Only closes count. A long pattern must touch the daily VWAP, so using lows
//   or wicks here would reject valid touch patterns for the wrong reason.
//
// The feed that reads cTrader Bars is separate; this class remains pure and unit tested.
public static class LongBelowDailyVwapGate {
    public static LongBelowDailyVwapSnapshotModel Evaluate(SignalSideModel side, LongBelowDailyVwapSettingsModel settings,
        IReadOnlyList<RecentDailyVwapBarModel> precedingBars) {
        var snapshot = new LongBelowDailyVwapSnapshotModel { Settings = settings };

        if (settings == null || !settings.IsEnabled || side != SignalSideModel.Buy || precedingBars == null)
            return snapshot;

        for (int index = 0; index < precedingBars.Count && index < settings.LookbackBars; index++) {
            RecentDailyVwapBarModel bar = precedingBars[index];

            if (bar == null)
                continue;

            snapshot.CheckedBars++;

            if (VwapStrongMetrics.IsUsable(bar.Close) && VwapStrongMetrics.IsUsable(bar.DailyVwap) &&
                bar.Close < bar.DailyVwap)
                snapshot.BelowCount++;
        }

        snapshot.IsBlocked = snapshot.BelowCount >= settings.BlockCount;
        return snapshot;
    }
}
