namespace cAlgo.Robots;

// 由 VwapStrongMetrics 从一根 K 线的读数算出的全部指标。两套 ATR 的结果都留着，
// 过滤只用 *Selected 那两个；其余都是研究字段，写进 CSV 供离线对比。
// 取不到的一律是 NaN，不能当成 0。纯数据，不依赖 cAlgo。
public class VwapStrongMetricsModel {
    // 日周 VWAP 的方向性距离 ÷ 各自 ATR。
    public double GapXM5 { get; set; } = double.NaN;

    public double GapXH1 { get; set; } = double.NaN;

    // N 根里日 VWAP 的方向性位移 ÷ 各自 ATR，未做时间归一。
    public double SlopeRawXM5 { get; set; } = double.NaN;

    public double SlopeRawXH1 { get; set; } = double.NaN;

    // 上面两个 × 6/N，换算成「等效 30 分钟变化速度」，不同 N 之间才能用同一个阈值比较。
    public double SlopeRateX30M5 { get; set; } = double.NaN;

    public double SlopeRateX30H1 { get; set; } = double.NaN;

    // 实际参与过滤的那一套。
    public double GapXSelected { get; set; } = double.NaN;

    public double SlopeRateXSelected { get; set; } = double.NaN;

    // 旧 SlopeX 列保留「原始斜率」的含义，取所选 ATR 那一套。
    public double SlopeRawXSelected { get; set; } = double.NaN;

    // 开口这 N 根里的变化，用所选 ATR 归一。只记录，不参与过滤（PDF 第 11 节）。
    public double GapChangeX { get; set; } = double.NaN;
}
