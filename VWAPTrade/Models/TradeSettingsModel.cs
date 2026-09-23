namespace cAlgo.Robots;

// 用户填的风控设置，从组合根一路传给 planner 和 executor。
public class TradeSettingsModel {
    public TradeSettingsModel(double riskPct, double takeProfitR, int stopOffsetTicks, double breakevenTriggerR,
        int breakevenOffsetTicks, VwapFilterSettingsModel vwapFilters, int vwapSlopeLookbackBars,
        Atr14SourceModel atr14Source, TradeDirectionPermissionModel tradeDirectionMode,
        LongBelowDailyVwapSettingsModel longBelowDailyVwap) {
        RiskPct = riskPct;
        TakeProfitR = takeProfitR;
        StopOffsetTicks = stopOffsetTicks;
        BreakevenTriggerR = breakevenTriggerR;
        BreakevenOffsetTicks = breakevenOffsetTicks;
        VwapFilters = vwapFilters;
        VwapSlopeLookbackBars = vwapSlopeLookbackBars;
        Atr14Source = atr14Source;
        TradeDirectionMode = tradeDirectionMode;
        LongBelowDailyVwap = longBelowDailyVwap;
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

    // 距离、速度、扩口三道过滤的阈值。
    public VwapFilterSettingsModel VwapFilters { get; }

    // 算斜率往回看几根 K 线。M5 上 6 根 = 30 分钟。必须 ≥ 1，在 OnStart 里校验。
    public int VwapSlopeLookbackBars { get; }

    // 过滤器用哪一套 ATR14 当分母。两套始终都算、都记录。
    public Atr14SourceModel Atr14Source { get; }

    // 允许往哪个方向下单。只在下单前拦截，不影响任何指标计算。
    public TradeDirectionPermissionModel TradeDirectionMode { get; }

    // The long recovery setting affects longs only; a block count of 0 turns it off completely.
    public LongBelowDailyVwapSettingsModel LongBelowDailyVwap { get; }
}
