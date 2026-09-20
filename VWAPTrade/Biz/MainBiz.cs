using System;
using System.Collections.Generic;

namespace cAlgo.Robots;

public class MainBiz {
    // 每一档关键位各自判断：K 线接触到这一档、并且收在正确的一侧，就为这一档产生一个信号。
    // 一根 K 线同时命中几档就返回几个信号 —— 每一档各自独立持仓（见 OrderExecutor）。
    public static List<SignalModel> Evaluate(CandleModel current, CandleModel previous, CandleModel earlier,
        IReadOnlyList<TradeLevelModel> levels) {
        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);
        var signals = new List<SignalModel>();

        foreach (TradeLevelModel level in levels) {
            if (!level.IsConfigured)
                continue;

            SignalModel signal = level.Side == SignalSideModel.Sell
                ? MatchShort(level, scanResult, current, previous, earlier)
                : MatchLong(level, scanResult, current, previous, earlier);

            if (signal != null)
                signals.Add(signal);
        }

        return signals;
    }

    /*
        一. 假突破/反转
           空单开仓条件（关键位在上方时）
           K线接触到关键位
           出现看跌信号：看跌pinbar、看跌吞没、顶分型、孕线下破。
           看跌信号的收线价格一定要低于关键位
     */
    private static SignalModel MatchShort(TradeLevelModel level, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.Pinbar == SignalSideModel.Sell && Utils.TouchesAndClosesBelow(level.Price, current.Close, current))
            return CreateSignal(level, "S_Pin_1", current.High, current);

        if (scanResult.Engulf == SignalSideModel.Sell && Utils.TouchesAndClosesBelow(level.Price, current.Close, current, previous))
            return CreateSignal(level, "S_Eng_1", current.High, current);

        if (scanResult.FractalTop == SignalSideModel.Sell && Utils.AnyBarIsShort(current) &&
            Utils.TouchesAndClosesBelow(level.Price, current.Close, current, previous, earlier))
            return CreateSignal(level, "S_Top_1", previous.High, current);

        if (scanResult.HaramiSingle == SignalSideModel.Sell &&
            Utils.TouchesAndClosesBelow(level.Price, current.Close, current, previous, earlier))
            return CreateSignal(level, "S_Harami_1", Math.Max(previous.High, current.High), current);

        return null;
    }

    /**
     一. 假突破/反转
        多单开仓条件（关键位在下方时）
        K线接触到关键位
        出现看涨信号：看涨pinbar、看涨吞没、底分型、孕线上破。
        看涨信号的收线价格一定要高于关键位
     */
    private static SignalModel MatchLong(TradeLevelModel level, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.Pinbar == SignalSideModel.Buy && Utils.TouchesAndClosesAbove(level.Price, current.Close, current))
            return CreateSignal(level, "L_Pin_1", current.Low, current);

        if (scanResult.Engulf == SignalSideModel.Buy && Utils.TouchesAndClosesAbove(level.Price, current.Close, current, previous))
            return CreateSignal(level, "L_Eng_1", current.Low, current);

        if (scanResult.FractalBottom == SignalSideModel.Buy && Utils.AnyBarIsLong(current) &&
            Utils.TouchesAndClosesAbove(level.Price, current.Close, current, previous, earlier))
            return CreateSignal(level, "L_Bot_1", previous.Low, current);

        if (scanResult.HaramiSingle == SignalSideModel.Buy &&
            Utils.TouchesAndClosesAbove(level.Price, current.Close, current, previous, earlier))
            return CreateSignal(level, "L_Harami_1", Math.Min(previous.Low, current.Low), current);

        return null;
    }

    private static SignalModel CreateSignal(TradeLevelModel level, string label, double stopLoss, CandleModel current) {
        return new SignalModel {
            Level = level,
            Label = label,
            StopLoss = stopLoss,
            Close = current.Close,
            High = current.High,
            Low = current.Low
        };
    }
}
