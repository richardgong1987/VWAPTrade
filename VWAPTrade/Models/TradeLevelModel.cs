namespace cAlgo.Robots;

// 手工配置的一档价位：接触到它才考虑这一档的信号，成交后按这一档自己的风险预算和止盈倍数下单。
// Name 会写进订单标签和交易 CSV 的「关键位」列，所以每一档要各不相同。
//
// 价格或风险百分比留 0 就是没启用这一档：两者都必须填，否则算不出仓位。
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
