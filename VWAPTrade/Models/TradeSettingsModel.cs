namespace cAlgo.Robots;

// 用户填的风控设置，从组合根一路传给 planner 和 executor。
public class TradeSettingsModel {
    public TradeSettingsModel(double riskPct, double takeProfitR, int stopOffsetTicks, double breakevenTriggerR,
        int breakevenOffsetTicks, double vwapGapMin, double vwapSlopeMin, int vwapSlopeLookbackBars) {
        RiskPct = riskPct;
        TakeProfitR = takeProfitR;
        StopOffsetTicks = stopOffsetTicks;
        BreakevenTriggerR = breakevenTriggerR;
        BreakevenOffsetTicks = breakevenOffsetTicks;
        VwapGapMin = vwapGapMin;
        VwapSlopeMin = vwapSlopeMin;
        VwapSlopeLookbackBars = vwapSlopeLookbackBars;
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

    // 日周 VWAP 的间距下限，单位是 H1 的 ATR14 倍数。0 = 关闭这道闸门。
    public double VwapGapMin { get; }

    // 日 VWAP 的斜率下限，单位同上。0 = 关闭这道闸门。
    public double VwapSlopeMin { get; }

    // 算斜率往回看几根 K 线。5 分钟图上 12 根 = 1 小时。
    public int VwapSlopeLookbackBars { get; }
}
