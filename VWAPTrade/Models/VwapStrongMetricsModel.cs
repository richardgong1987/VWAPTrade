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

    // 旧字段：开口这 N 根里的变化，用所选 ATR 归一、未做 30 分钟标准化。
    // V1.1 保留它的原定义不动，新口径另开下面的 GapChangeRate* 字段，免得新旧 CSV 混淆。
    public double GapChangeX { get; set; } = double.NaN;

    // 扩口变化（V1.1 第 3 节）。先算价格上的原始变化，再分别用两套 ATR 归一，
    // 最后 × 6/N 换算成「等效 30 分钟扩口速度」—— 与 Slope 同一套标准化，N 不同才能比。
    //   > 0 顺势开口扩大   ≈ 0 基本稳定   < 0 顺势开口缩小
    public double GapChangeRawPrice { get; set; } = double.NaN;

    public double GapChangeRawXM5 { get; set; } = double.NaN;

    public double GapChangeRawXH1 { get; set; } = double.NaN;

    public double GapChangeRateX30M5 { get; set; } = double.NaN;

    public double GapChangeRateX30H1 { get; set; } = double.NaN;

    // 实际参与过滤的那一套。
    public double GapChangeRateX30Selected { get; set; } = double.NaN;

    // SlopeRateX_Selected / GapX_Selected: how fast the daily VWAP still moves for the gap it has
    // already opened. A low value is an ageing trend, wide but slowing (docs/VWAP.docx).
    public double SlopeEfficiency { get; set; } = double.NaN;

    // GapChangeRateX30_Selected / GapX_Selected: how fast the gap still widens for how wide it
    // already is. Low means still widening, but too slowly for a gap that size (docs/VWAP2.docx).
    public double ExpansionEfficiency { get; set; } = double.NaN;
}
