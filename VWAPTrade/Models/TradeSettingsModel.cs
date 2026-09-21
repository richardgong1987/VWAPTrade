namespace cAlgo.Robots;

// 用户填的风控设置，从组合根一路传给 planner 和 executor。
public class TradeSettingsModel {
    public TradeSettingsModel(double riskPct, double takeProfitR, int stopOffsetTicks, double breakevenTriggerR,
        int breakevenOffsetTicks, double vwapGapMin, double vwapSlopeRateMin, int vwapSlopeLookbackBars,
        Atr14SourceModel atr14Source) {
        RiskPct = riskPct;
        TakeProfitR = takeProfitR;
        StopOffsetTicks = stopOffsetTicks;
        BreakevenTriggerR = breakevenTriggerR;
        BreakevenOffsetTicks = breakevenOffsetTicks;
        VwapGapMin = vwapGapMin;
        VwapSlopeRateMin = vwapSlopeRateMin;
        VwapSlopeLookbackBars = vwapSlopeLookbackBars;
        Atr14Source = atr14Source;
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

    // 日周 VWAP 的间距下限，单位是所选 ATR14 的倍数。0 = 关闭这道闸门。
    public double VwapGapMin { get; }

    // 日 VWAP 的「30 分钟标准化速度」下限，单位同上。0 = 关闭这道闸门。
    // 注意这不是旧的 VwapSlopeMin：阈值比的是 SlopeRawX × 6/N，不是原始斜率（见 VwapStrongMetrics）。
    public double VwapSlopeRateMin { get; }

    // 算斜率往回看几根 K 线。M5 上 6 根 = 30 分钟。必须 ≥ 1，在 OnStart 里校验。
    public int VwapSlopeLookbackBars { get; }

    // 过滤器用哪一套 ATR14 当分母。两套始终都算、都记录。
    public Atr14SourceModel Atr14Source { get; }
}
