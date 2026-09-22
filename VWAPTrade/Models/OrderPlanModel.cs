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

    // 下单当时的 VWAP 指标快照：开仓行与平仓行写的是同一份，平仓时不再重算
    // （那时的 VWAP 已经跑远了）。取不到的一律留 NaN，写进 CSV 时是空白，不能伪造成 0。
    public VwapStrongReadingModel VwapReading { get; set; }

    public VwapStrongMetricsModel VwapMetrics { get; set; }

    // 下单当时生效的过滤设置与方向许可，一并写进 CSV：日后看一行就知道这笔是在什么口径下成交的。
    public VwapFilterSettingsModel VwapFilters { get; set; }

    public TradeDirectionPermissionModel TradeDirectionMode { get; set; }

    // 下单当时的 Departure 状态（V2 第 6 节）。闸门关着时只有阈值 0 有意义，状态列写空白。
    public DepartureSnapshotModel Departure { get; set; }
}
