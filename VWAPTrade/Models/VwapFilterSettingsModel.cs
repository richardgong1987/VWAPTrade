namespace cAlgo.Robots;

// Thresholds for the four VWAP filters. Pure data, judged by VwapStack.
public class VwapFilterSettingsModel {
    public VwapFilterSettingsModel(double gapMin, double slopeRateMin, bool useGapChangeFilter, double gapChangeRateMin,
        double gapChangeRateMax, double slopeEfficiencyMin) {
        GapMin = gapMin;
        SlopeRateMin = slopeRateMin;
        UseGapChangeFilter = useGapChangeFilter;
        GapChangeRateMin = gapChangeRateMin;
        GapChangeRateMax = gapChangeRateMax;
        SlopeEfficiencyMin = slopeEfficiencyMin;
    }

    // 0 = 关闭这道闸门。
    public double GapMin { get; }

    public double SlopeRateMin { get; }

    // 扩口变化过滤用单独的开关，不沿用「0 = 关闭」：GapChange 允许负值，有效区间还可能跨过 0，
    // 拿 0 当关闭值会有歧义（V1.1 第 4 节）。
    public bool UseGapChangeFilter { get; }

    // 开启时要求 GapChangeRateMin ≤ GapChangeRateX30_Selected ≤ GapChangeRateMax（闭区间）。
    public double GapChangeRateMin { get; }

    public double GapChangeRateMax { get; }

    // Minimum SlopeRateX_Selected / GapX_Selected (docs/VWAP.docx). 0 = off. 0.01 is only the
    // centre still to be validated, so it stays a parameter.
    public double SlopeEfficiencyMin { get; }

    // Min > Max 是参数填错，启动时报错停止，绝不静默对调（V1.1 第 4 节）。
    public bool IsGapChangeRangeInverted => UseGapChangeFilter && GapChangeRateMin > GapChangeRateMax;
}
