using System;

namespace cAlgo.Robots;

// docs/vwap-v5-slim.pine 的累积口径：VWAP = Σ(hlc3 × volume) / Σ(volume)，每逢新的交易日/交易周
// 清零重算。调用方按 K 线顺序逐根 Append，交易日/交易周的边界用下面的静态方法判断。
// 这里只有算术和时间比较，没有 cAlgo 依赖，所以是单元测试覆盖的部分。
public class VwapCalculator {
    private double _dailyPriceVolume;
    private double _dailyVolume;
    private double _weeklyPriceVolume;
    private double _weeklyVolume;

    // 上一根 K 线的当日 VWAP。新的一天开始时，它就是刚结束那一天的收盘 VWAP。
    private double _lastDailyVwap = double.NaN;

    // 对应 Pine 的 ta.valuewhen(newSessionD, vD[1], 0)：跨日时取一次，之后整天保持不变。
    private double _closedDailyVwap = double.NaN;

    public VwapSampleModel Append(double typicalPrice, double volume, bool isDailySessionStart, bool isWeeklySessionStart) {
        if (isDailySessionStart && !double.IsNaN(_lastDailyVwap))
            _closedDailyVwap = _lastDailyVwap;

        Accumulate(isDailySessionStart, ref _dailyPriceVolume, ref _dailyVolume, typicalPrice, volume);
        Accumulate(isWeeklySessionStart, ref _weeklyPriceVolume, ref _weeklyVolume, typicalPrice, volume);

        _lastDailyVwap = ToVwap(_dailyPriceVolume, _dailyVolume, typicalPrice);

        return new VwapSampleModel {
            Daily = _lastDailyVwap,
            Weekly = ToVwap(_weeklyPriceVolume, _weeklyVolume, typicalPrice),
            PreviousDaily = _closedDailyVwap,
            IsDailySessionStart = isDailySessionStart
        };
    }

    // 交易日按服务器时间的自然日切分，不是 TradingView 的交易所时段，因此日界附近可能与 Pine 原
    // 脚本差一两根 K 线。
    public static bool IsNewDay(DateTime current, DateTime previous) {
        return current.Date != previous.Date;
    }

    // 交易周以周一为界：周一开盘那根 K 线开新的一周，周末的跳空不算。
    public static bool IsNewWeek(DateTime current, DateTime previous) {
        return GetWeekStart(current) != GetWeekStart(previous);
    }

    public static DateTime GetWeekStart(DateTime time) {
        int daysSinceMonday = ((int)time.DayOfWeek + 6) % 7;
        return time.Date.AddDays(-daysSinceMonday);
    }

    private static void Accumulate(bool isSessionStart, ref double priceVolume, ref double volume, double typicalPrice,
        double barVolume) {
        if (isSessionStart) {
            priceVolume = typicalPrice * barVolume;
            volume = barVolume;
            return;
        }

        priceVolume += typicalPrice * barVolume;
        volume += barVolume;
    }

    // 整段没有成交量时（休市、行情静止）除法失效，退回用这根 K 线的 hlc3，线不会断。
    private static double ToVwap(double priceVolume, double volume, double typicalPrice) {
        return volume > 0.0 ? priceVolume / volume : typicalPrice;
    }
}
