using System;

namespace cAlgo.Robots;

// 一个信号 = 一档价位在某根收盘 K 线上命中。同一根 K 线命中几档就有几个信号，
// 每个各自下单（见 MainBiz 与 OrderExecutor）。
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

    public int BarIndex { get; set; }

    public DateTime BarTime { get; set; }
}
