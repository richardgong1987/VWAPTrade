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

    public double Atr14 { get; set; } = double.NaN;

    public double GapX { get; set; } = double.NaN;

    public double SlopeX { get; set; } = double.NaN;

    // 这一笔最后是赚是赔。只有平仓行有值。
    public string FinalResult { get; set; }

    // 算斜率时回看那一根上的日 VWAP，以及同一根上的周 VWAP。两个都只是记录，不参与任何判断。
    public double DailyVwapLookback { get; set; } = double.NaN;

    public double WeeklyVwapLookback { get; set; } = double.NaN;

    // 这一笔实际打出来的 R，按开仓时的初始风险价格距离算（见 TradeResultR）。只有平仓行有值。
    public double ResultR { get; set; } = double.NaN;

    // 开口这 N 根里的变化：正数 = 扩口扩大，负数 = 扩口缩小。
    public double GapChangeX { get; set; } = double.NaN;

    // 以下是 VWAP Strong V1 追加的研究字段（PDF 第 10 节）。两套 ATR 的结果都留着，
    // *_Selected 两列是实际参与过滤的那一套。
    public int LookbackN { get; set; }

    public double Atr14M5 { get; set; } = double.NaN;

    public double Atr14H1 { get; set; } = double.NaN;

    public double GapXM5 { get; set; } = double.NaN;

    public double GapXH1 { get; set; } = double.NaN;

    public double SlopeRawXM5 { get; set; } = double.NaN;

    public double SlopeRawXH1 { get; set; } = double.NaN;

    public double SlopeRateX30M5 { get; set; } = double.NaN;

    public double SlopeRateX30H1 { get; set; } = double.NaN;

    public string SelectedAtrPeriod { get; set; }

    public double GapXSelected { get; set; } = double.NaN;

    public double SlopeRateXSelected { get; set; } = double.NaN;

    // 以下是 V1.1 追加的字段。前四个是当时生效的设置，后六个是扩口变化的读数。
    public string TradeDirectionMode { get; set; }

    public string UseGapChangeFilter { get; set; }

    public double GapChangeRateMin { get; set; } = double.NaN;

    public double GapChangeRateMax { get; set; } = double.NaN;

    public double GapChangeRawPrice { get; set; } = double.NaN;

    public double GapChangeRawXM5 { get; set; } = double.NaN;

    public double GapChangeRawXH1 { get; set; } = double.NaN;

    public double GapChangeRateX30M5 { get; set; } = double.NaN;

    public double GapChangeRateX30H1 { get; set; } = double.NaN;

    public double GapChangeRateX30Selected { get; set; } = double.NaN;
}
