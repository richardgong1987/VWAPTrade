namespace cAlgo.Robots;

// 判断「强趋势」要用到的一根 K 线上的全部原始读数，交给 VwapStrongMetrics 算出各项指标。
// 纯数据，不依赖 cAlgo。
public class VwapStrongReadingModel {
    public double Close { get; set; }

    public double DailyVwap { get; set; }

    public double WeeklyVwap { get; set; }

    // N 根之前的日/周 VWAP，用来算斜率与开口变化。取不到（回看那根跨了日 VWAP 重置边界，
    // 或者还没有那么多根）时是 NaN。
    public double DailyVwapBefore { get; set; } = double.NaN;

    public double WeeklyVwapBefore { get; set; } = double.NaN;

    // 两套 ATR14 始终都算、都记录；SelectedAtrPeriod 只决定过滤器拿哪一套当分母。
    public double Atr14M5 { get; set; } = double.NaN;

    public double Atr14H1 { get; set; } = double.NaN;

    // 这一笔实际用的回看根数。写进 CSV，日后复核公式时不用猜当时的设置。
    public int LookbackN { get; set; }

    public Atr14SourceModel SelectedAtrPeriod { get; set; }

    public double SelectedAtr => SelectedAtrPeriod == Atr14SourceModel.ATR14_M5 ? Atr14M5 : Atr14H1;
}
