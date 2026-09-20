using System;

namespace cAlgo.Robots;

public class RiskGuard {
    // 只在交易时段里开新单：周二到周五每天 10:30~次日 06:00（日本时间），周一和周末不开。
    // 时段定义见 TradingSession —— VWAP 的清零点用的是同一份定义。
    // 已经持有的仓位不管：收盘时间到了也不平，照常由止损/止盈了结。
    public bool ShouldBlockNewOrder(DateTime time) {
        return !TradingSession.IsInSession(time);
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
