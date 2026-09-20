namespace cAlgo.Robots;

// 一档关键位：接触到它才考虑这一档的信号，成交后按这一档的风险预算和止盈倍数下单。
// 现在的关键位是每根 K 线上的当日/当周 VWAP（见 SignalDetector），所以价格逐根变，
// 方向也随收盘价落在 VWAP 的哪一侧而定。
// Name 会写进订单标签和交易 CSV 的「关键位」列，所以每一档要各不相同。
//
// 价格或风险百分比留 0 就是这一档不可用：两者都必须有值，否则算不出仓位。
public class TradeLevelModel {
    public TradeLevelModel(string name, SignalSideModel side, double price, double riskPct, double takeProfitR) {
        Name = name;
        Side = side;
        Price = price;
        RiskPct = riskPct;
        TakeProfitR = takeProfitR;
    }

    public string Name { get; }

    // Sell 档是上方的关键位（作空），Buy 档是下方的关键位（作多）。
    public SignalSideModel Side { get; }

    public double Price { get; }

    // 这一档单笔可亏的账户权益百分比。
    public double RiskPct { get; }

    // 止盈距离 = TakeProfitR × 止损距离。
    public double TakeProfitR { get; }

    public bool IsConfigured => Price > 0.0 && RiskPct > 0.0;
}
