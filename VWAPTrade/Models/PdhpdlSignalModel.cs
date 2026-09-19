using System;

namespace cAlgo.Robots;

// 一个信号 = 一档价位在某根收盘 K 线上命中。同一根 K 线命中几档就有几个信号，
// 每个各自下单（见 MainBiz 与 PdhpdlOrderExecutor）。
public class PdhpdlSignalModel {
    // 命中的那一档：方向、风险预算与止盈倍数都从它来。
    public TradeLevelModel Level { get; set; }

    // 命中的形态名，例如 S_Pin_1。写进 CSV 的「信号」列，也是图上标记的文字。
    public string Label { get; set; } = "";

    // 形态自带的止损价位。
    public double StopLoss { get; set; }

    public double Close { get; set; }

    public double High { get; set; }

    public double Low { get; set; }

    public int BarIndex { get; set; }

    public DateTime BarTime { get; set; }
}
