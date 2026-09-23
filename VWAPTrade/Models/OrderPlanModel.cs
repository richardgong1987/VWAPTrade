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

    // The broker order label, set by OrderExecutor.
    public string Label { get; set; } = "";

    // The signal and the settings this plan was made from. The trade CSV reads the entry's VWAP
    // readings and filter settings through them, for the entry row and again for the close row
    // (by then the live VWAP has moved on).
    public SignalModel Signal { get; set; }

    public TradeSettingsModel Settings { get; set; }
}
