namespace cAlgo.Robots;

// 方向许可：下单前的最后一道闸门（VWAP_Strong_V1.1 第 5、6 节）。
// 只决定「这个方向的信号准不准下单」，不碰任何指标计算 —— All 模式下必须放行全部信号，
// 这样它与没有这个开关的旧版本成交结果完全一致。纯判断，有单元测试。
public static class TradeDirectionGate {
    public static bool IsAllowed(TradeDirectionModeModel mode, SignalSideModel side) {
        switch (mode) {
            case TradeDirectionModeModel.LongOnly:
                return side == SignalSideModel.Buy;
            case TradeDirectionModeModel.ShortOnly:
                return side == SignalSideModel.Sell;
            default:
                return true;
        }
    }
}
