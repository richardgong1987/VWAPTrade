namespace cAlgo.Robots;

// VWAP Strong 的距离、斜率与扩口变化公式（docs/VWAP_Strong_V1.1.docx 第 2 节沿用定义、第 3 节扩口变化）。
//
// 先把方向统一成 direction：多头 +1、空头 −1，这样多空共用同一个正数阈值，
// 顺着交易方向是正数、逆着是负数 —— 绝不能取绝对值，那会把逆向斜率伪装成合格。
//
//   GapRaw       = direction × (日VWAP − 周VWAP)
//   SlopeRaw     = direction × (日VWAP[0] − 日VWAP[N])
//   GapX         = GapRaw   / ATR
//   SlopeRawX    = SlopeRaw / ATR
//   SlopeRateX30 = SlopeRawX × 6/N
//   SlopeEfficiency = SlopeRateX30_Selected / GapX_Selected   (docs/VWAP.docx)
//   ExpansionEfficiency = GapChangeRateX30_Selected / GapX_Selected   (docs/VWAP2.docx)
//
// 6/N 不是新的交易条件，只是单位换算：M5 上 6 根 = 30 分钟，把任意 Lookback 的累计变化量
// 折算成「等效 30 分钟变化速度」，N 不同的参数组才能共用同一个 SlopeRateMin。
// 因此本版只允许跑在 M5 上（周期在 OnStart 里校验）。
//
// 两套 ATR 的结果都算出来，过滤只用所选那一套。纯算术，没有 cAlgo 依赖，有单元测试。
public static class VwapStrongMetrics {
    // M5 上 6 根 = 30 分钟。
    private const double BarsPer30Minutes = 6.0;

    public static VwapStrongMetricsModel Compute(VwapStrongReadingModel reading, SignalSideModel side) {
        VwapStrongMetricsModel metrics = new();

        if (reading == null || side == SignalSideModel.None)
            return metrics;

        double direction = side == SignalSideModel.Buy ? 1.0 : -1.0;
        double gapRaw = direction * (reading.DailyVwap - reading.WeeklyVwap);
        double slopeRaw = direction * (reading.DailyVwap - reading.DailyVwapBefore);
        double rateFactor = GetRateFactor(reading.LookbackN);

        metrics.GapXM5 = Divide(gapRaw, reading.Atr14M5);
        metrics.GapXH1 = Divide(gapRaw, reading.Atr14H1);
        metrics.SlopeRawXM5 = Divide(slopeRaw, reading.Atr14M5);
        metrics.SlopeRawXH1 = Divide(slopeRaw, reading.Atr14H1);
        metrics.SlopeRateX30M5 = Multiply(metrics.SlopeRawXM5, rateFactor);
        metrics.SlopeRateX30H1 = Multiply(metrics.SlopeRawXH1, rateFactor);

        bool isM5 = reading.SelectedAtrPeriod == Atr14SourceModel.ATR14_M5;
        metrics.GapXSelected = isM5 ? metrics.GapXM5 : metrics.GapXH1;
        metrics.SlopeRawXSelected = isM5 ? metrics.SlopeRawXM5 : metrics.SlopeRawXH1;
        metrics.SlopeRateXSelected = isM5 ? metrics.SlopeRateX30M5 : metrics.SlopeRateX30H1;

        // The ATR cancels out, so M5 and H1 give the same efficiency; it still needs the selected ATR
        // to be usable. A gap of 0 or less has no efficiency: NaN, which an enabled filter blocks.
        metrics.SlopeEfficiency = Divide(metrics.SlopeRateXSelected, metrics.GapXSelected);

        // 扩口变化：现在的方向性开口减去 N 根之前的。多空都已经由 direction 统一成「顺势为正」，
        // 所以扩大恒为正、缩小恒为负，多空共用同一个区间（V1.1 第 3.4 节）。
        double gapBefore = direction * (reading.DailyVwapBefore - reading.WeeklyVwapBefore);
        metrics.GapChangeRawPrice = Subtract(gapRaw, gapBefore);
        metrics.GapChangeRawXM5 = Divide(metrics.GapChangeRawPrice, reading.Atr14M5);
        metrics.GapChangeRawXH1 = Divide(metrics.GapChangeRawPrice, reading.Atr14H1);
        metrics.GapChangeRateX30M5 = Multiply(metrics.GapChangeRawXM5, rateFactor);
        metrics.GapChangeRateX30H1 = Multiply(metrics.GapChangeRawXH1, rateFactor);
        metrics.GapChangeRateX30Selected = isM5 ? metrics.GapChangeRateX30M5 : metrics.GapChangeRateX30H1;

        // Same shape as SlopeEfficiency: the ATR cancels out, and a gap of 0 or less gives NaN.
        metrics.ExpansionEfficiency = Divide(metrics.GapChangeRateX30Selected, metrics.GapXSelected);

        // 旧字段保持原定义：未做 30 分钟标准化的那一版。
        metrics.GapChangeX = isM5 ? metrics.GapChangeRawXM5 : metrics.GapChangeRawXH1;

        return metrics;
    }

    // N<1 会让 6/N 失去意义（N 在 OnStart 里已经校验过，这里是第二道保险）。
    private static double GetRateFactor(int lookbackN) {
        return lookbackN >= 1 ? BarsPer30Minutes / lookbackN : double.NaN;
    }

    // Every divisor here (an ATR, the gap) is meaningful only when positive.
    private static double Divide(double value, double divisor) {
        if (!IsUsable(value) || !IsUsable(divisor) || divisor <= 0.0)
            return double.NaN;

        return value / divisor;
    }

    private static double Subtract(double left, double right) {
        if (!IsUsable(left) || !IsUsable(right))
            return double.NaN;

        return left - right;
    }

    private static double Multiply(double value, double factor) {
        if (!IsUsable(value) || !IsUsable(factor))
            return double.NaN;

        return value * factor;
    }

    public static bool IsUsable(double value) {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
