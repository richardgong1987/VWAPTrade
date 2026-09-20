namespace cAlgo.Robots;

// 方向闸门：收盘价和两条 VWAP 要排成一条线，才允许往那个方向开仓。
//   收盘价 > 日 VWAP > 周 VWAP  只开多
//   收盘价 < 日 VWAP < 周 VWAP  只开空
// 排不出来（两条 VWAP 交叉、或者收盘价夹在中间）就是趋势没站在任何一边，这根 K 线不做。
// 纯比较，没有 cAlgo 依赖，有单元测试。
public static class VwapStack {
    public static SignalSideModel ResolveSide(double close, double dailyVwap, double weeklyVwap) {
        if (!IsUsable(close) || !IsUsable(dailyVwap) || !IsUsable(weeklyVwap))
            return SignalSideModel.None;

        if (close > dailyVwap && dailyVwap > weeklyVwap)
            return SignalSideModel.Buy;

        if (close < dailyVwap && dailyVwap < weeklyVwap)
            return SignalSideModel.Sell;

        return SignalSideModel.None;
    }

    private static bool IsUsable(double value) {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
