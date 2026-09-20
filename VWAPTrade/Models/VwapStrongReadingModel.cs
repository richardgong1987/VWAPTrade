namespace cAlgo.Robots;

// 判断「强趋势」要用到的一根 K 线上的全部读数，交给 VwapStack 做闸门判断。纯数据，不依赖 cAlgo。
public class VwapStrongReadingModel {
    public double Close { get; set; }

    public double DailyVwap { get; set; }

    public double WeeklyVwap { get; set; }

    // N 根之前的日 VWAP，用来算斜率。取不到（回看那根跨了场或者还没有那么多根）时是 NaN。
    public double DailyVwapBefore { get; set; }

    // 同一根回看 K 线上的周 VWAP，用来算开口这 N 根里是扩大还是缩小（GapChangeX）。
    public double WeeklyVwapBefore { get; set; }

    // H1 的 ATR14。间距和斜率都除以它做归一，不同波动环境才能用同一个阈值比较。
    public double Atr14H1 { get; set; }
}
