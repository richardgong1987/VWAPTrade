using System;

namespace cAlgo.Robots;

public class TradeCsvRecordModel {
    public string Id { get; set; }

    public string KeyLevel { get; set; }

    public string Signal { get; set; }

    public string Comment { get; set; }

    public string Symbol { get; set; }

    public string TimeFrame { get; set; }

    public double EntryAccountEquity { get; set; }

    public double CloseAccountEquity { get; set; }

    public DateTime EntryTime { get; set; }

    public double EntryPrice { get; set; }

    public double ClosePrice { get; set; }

    public double StopPrice { get; set; }

    public double TakeProfitPrice { get; set; }

    public double RiskPrice { get; set; }

    public double VolumeInUnits { get; set; }

    public string CloseReason { get; set; }

    public double ProfitLoss { get; set; }

    public string CloseTime { get; set; }

    public string PositionId { get; set; }

    public string DealId { get; set; }

    // 下面几列是开仓当时的三道闸门读数（见 VwapStack），开仓行和平仓行都写一份，
    // 这样一行就能把 GapX/SlopeX 和这笔的盈亏对上，不用再按持仓 ID 去拼。
    public string Side { get; set; }

    public double DailyVwap { get; set; } = double.NaN;

    public double WeeklyVwap { get; set; } = double.NaN;

    public double Atr14H1 { get; set; } = double.NaN;

    public double GapX { get; set; } = double.NaN;

    public double SlopeX { get; set; } = double.NaN;

    // 这一笔最后是赚是赔。只有平仓行有值。
    public string FinalResult { get; set; }
}
