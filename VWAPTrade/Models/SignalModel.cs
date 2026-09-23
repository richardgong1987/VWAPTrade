using System;

namespace cAlgo.Robots;

// 一个信号 = 关键位在某根收盘 K 线上被形态命中（见 LevelPatternMatcher），
// 一根 K 线最多一个，交给 OrderExecutor 下单。
public class SignalModel {
    // 命中的那一档：方向、风险预算与止盈倍数都从它来。
    public TradeLevelModel Level { get; set; }

    // 命中的形态名，例如 S_Pin_1。写进 CSV 的「信号」列，也是图上标记的文字。
    public string Label { get; set; } = "";

    // 形态自带的止损价位。
    public double StopLoss { get; set; }

    public double Close { get; set; }

    public double High { get; set; }

    public double Low { get; set; }

    // 下单当时的原始读数与算出来的各项指标，一路带到交易 CSV，
    // 方便回头拿 GapX / SlopeRateX 对着盈亏调参。
    public VwapStrongReadingModel Strong { get; set; }

    public VwapStrongMetricsModel Metrics { get; set; }

    // Departure 状态机在这根 K 线上的状态，同样带进 CSV。
    public DepartureSnapshotModel Departure { get; set; }

    public int BarIndex { get; set; }

    public DateTime BarTime { get; set; }

    // The first gate that stopped this signal; None = it passed them all. SignalDetector fills in
    // the signal gates, OrderExecutor the order gates.
    public EntryGateModel BlockedBy { get; set; }

    // Extra facts for the gates that have them: the planner's reject reason, the broker's error.
    public string BlockDetail { get; set; } = "";
}
