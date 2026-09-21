using System;

namespace cAlgo.Robots;

// 交易 CSV 的一行。开仓写一行、平仓写一行，两行共用同一份开仓时的 VWAP 快照。
//
// 这里只放「这一行自己的事实」，指标读数不再逐个字段抄一遍 —— 直接持有开仓时的
// OrderPlanModel，由 TradeCsvColumns 按列取值。以前那份 50 个扁平字段的拷贝，
// 是列名、字段、赋值、写行四处分散的根源。
public class TradeRecordModel {
    public string Id { get; set; } = "";

    public string KeyLevel { get; set; } = "";

    public string Signal { get; set; } = "";

    public string Comment { get; set; } = "";

    public string Symbol { get; set; } = "";

    public string TimeFrame { get; set; } = "";

    public string Side { get; set; } = "";

    public DateTime EntryTime { get; set; }

    public double EntryPrice { get; set; }

    public double ClosePrice { get; set; }

    public double StopPrice { get; set; }

    public double TakeProfitPrice { get; set; }

    public double RiskPrice { get; set; }

    public double VolumeInUnits { get; set; }

    public string CloseReason { get; set; } = "";

    public double EntryAccountEquity { get; set; }

    public double CloseAccountEquity { get; set; }

    public double ProfitLoss { get; set; }

    public string CloseTime { get; set; } = "";

    public string PositionId { get; set; } = "";

    public string DealId { get; set; } = "";

    // 只有平仓行有值。
    public string FinalResult { get; set; } = "";

    public double ResultR { get; set; } = double.NaN;

    // 开仓时的下单方案：VWAP 读数、算出的指标、当时生效的过滤设置与方向许可都在里面。
    // 平仓行复用同一个对象，所以两行的指标列逐字相同。
    public OrderPlanModel EntryPlan { get; set; }
}
