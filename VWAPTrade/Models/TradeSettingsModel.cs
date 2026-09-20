namespace cAlgo.Robots;

// 用户唯一要填的两个值：单笔风险占账户权益的百分比，以及止盈距离相对止损距离的倍数。
// 两条 VWAP 关键位共用这一份设置。
public class TradeSettingsModel {
    public TradeSettingsModel(double riskPct, double takeProfitR) {
        RiskPct = riskPct;
        TakeProfitR = takeProfitR;
    }

    public double RiskPct { get; }

    public double TakeProfitR { get; }
}
