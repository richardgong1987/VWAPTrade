namespace cAlgo.Robots;

// The sized order the planner produces from a signal. Pure data, no cAlgo dependency.
public class OrderPlanModel {
    public bool IsValid { get; set; }
    public string RejectReason { get; set; } = "";

    public TradeDirectionModel DirectionModel { get; set; }

    public double EntryPrice { get; set; }
    public double StopPrice { get; set; }
    public double TakeProfitPrice { get; set; }
    public double RiskPrice { get; set; }
    public double StopLossPips { get; set; }
    public double TakeProfitPips { get; set; }

    public double Lots { get; set; }
    public double VolumeInUnits { get; set; }

    public double AccountEquity { get; set; }
    public double RiskMoney { get; set; }
    public double EstimatedRiskMoney { get; set; }
    public string Label { get; set; } = "";

    public string KeyLevel { get; set; } = "";
    public string SignalName { get; set; } = "";

    // 下单当时的三道闸门读数，写进交易 CSV 用来调参。
    public double DailyVwap { get; set; } = double.NaN;
    public double WeeklyVwap { get; set; } = double.NaN;
    public double Atr14H1 { get; set; } = double.NaN;
    public double GapX { get; set; } = double.NaN;
    public double SlopeX { get; set; } = double.NaN;
    public double DailyVwapBefore { get; set; } = double.NaN;
    public double WeeklyVwapBefore { get; set; } = double.NaN;
    public double GapChangeX { get; set; } = double.NaN;
}
