using System.Collections.Generic;

namespace cAlgo.Robots;

// Opposite-side recovery gate:
//
//   Among the preceding N closed bars in this VWAP day, at least K closes on the opposite side
//   of the daily VWAP block a new signal: below for a Buy, above for a Sell. Only closes count.
//   A pattern must touch the daily VWAP, so using lows or highs here would reject valid touch
//   patterns for the wrong reason.
//
// The feed that reads cTrader Bars is separate; this class remains pure and unit tested.
public static class OppositeDailyVwapGate {
    public static OppositeDailyVwapSnapshotModel Evaluate(SignalSideModel side, OppositeDailyVwapSettingsModel settings,
        IReadOnlyList<RecentDailyVwapBarModel> precedingBars) {
        var snapshot = new OppositeDailyVwapSnapshotModel { Settings = settings };

        if (settings == null || !settings.IsEnabled ||
            (side != SignalSideModel.Buy && side != SignalSideModel.Sell) || precedingBars == null)
            return snapshot;

        for (int index = 0; index < precedingBars.Count && index < settings.LookbackBars; index++) {
            RecentDailyVwapBarModel bar = precedingBars[index];

            if (bar == null)
                continue;

            snapshot.CheckedBars++;

            if (VwapStrongMetrics.IsUsable(bar.Close) && VwapStrongMetrics.IsUsable(bar.DailyVwap) &&
                IsOnOppositeSide(side, bar.Close, bar.DailyVwap))
                snapshot.OppositeSideCount++;
        }

        snapshot.IsBlocked = snapshot.OppositeSideCount >= settings.BlockCount;
        return snapshot;
    }

    private static bool IsOnOppositeSide(SignalSideModel side, double close, double dailyVwap) {
        return side == SignalSideModel.Buy ? close < dailyVwap : close > dailyVwap;
    }
}
