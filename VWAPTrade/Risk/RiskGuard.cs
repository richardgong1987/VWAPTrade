using System;

namespace cAlgo.Robots;

public class RiskGuard {
    public bool ShouldBlockNewOrder(DateTime time) {
        return time.DayOfWeek == DayOfWeek.Sunday;
    }

    public bool TryGetStopLossPipsRejectReason(double stopLossPips, out string rejectReason) {
        rejectReason = "";

        if (stopLossPips <= 0.0) {
            rejectReason = "Stop loss pips is not positive.";
            return true;
        }

        return false;
    }

    // Risk money is the account currency you accept losing on one trade: a percentage of
    // equity (e.g. 1% of 10000 = 100). Non-positive inputs risk 0.
    public double CalculateRiskMoney(double equity, double riskPct) {
        if (equity <= 0.0 || riskPct <= 0.0)
            return 0.0;

        return equity * riskPct / 100.0;
    }
}
