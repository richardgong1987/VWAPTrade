namespace cAlgo.Robots;

// 用户填的风控设置，从组合根一路传给 planner 和 executor。
public class TradeSettingsModel {
    public TradeSettingsModel(double riskPct, double takeProfitR, int stopOffsetTicks, double breakevenTriggerR,
        int breakevenOffsetTicks) {
        RiskPct = riskPct;
        TakeProfitR = takeProfitR;
        StopOffsetTicks = stopOffsetTicks;
        BreakevenTriggerR = breakevenTriggerR;
        BreakevenOffsetTicks = breakevenOffsetTicks;
    }

    // 单笔可亏的账户权益百分比。
    public double RiskPct { get; }

    // 止盈距离 = TakeProfitR × 止损距离，开仓时就定死、挂在订单上交给券商执行。
    public double TakeProfitR { get; }

    // 止损在形态价位之外再让开这么多个 tick，免得贴着影线被扫。
    public int StopOffsetTicks { get; }

    // 浮盈达到这么多个 R 就把止损推到保本位。0 = 关闭。
    public double BreakevenTriggerR { get; }

    // 保本止损落在开仓价顺盈利方向偏移这么多个 tick 的位置，别正好压在开仓价上。
    public int BreakevenOffsetTicks { get; }
}
