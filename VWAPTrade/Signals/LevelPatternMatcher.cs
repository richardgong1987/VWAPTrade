using System;

namespace cAlgo.Robots;

// 关键位就是图上那条黄线（日 VWAP）。方向已经由 VwapStack 的闸门定好（见 SignalDetector），
// 这里只判断形态，以及形态有没有长在关键位上。碰到了就出信号，没碰到就不做。
//
// 「碰到」只看这个形态自己用到的那几根 K 线：pinbar 一根、吞没两根、分型和孕线三根。
// 笼统地拿三根去判断是错的 —— 单根形态会被隔壁那根的触碰放行。
//
// 纯判断，没有 cAlgo 依赖，有单元测试。
public static class LevelPatternMatcher {
    public static SignalModel Match(CandleModel current, CandleModel previous, CandleModel earlier, TradeLevelModel level) {
        HanJinSignalScanModel patterns = HanJinSignals26.Scan(current, previous, earlier);

        return level.Side == SignalSideModel.Sell
            ? MatchShort(level, patterns, current, previous, earlier)
            : MatchLong(level, patterns, current, previous, earlier);
    }

    /*
           K线接触到关键位（日 VWAP）
           出现看跌信号：看跌pinbar、看跌吞没、顶分型、孕线下破。
     */
    private static SignalModel MatchShort(TradeLevelModel level, HanJinSignalScanModel patterns, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (patterns.Pinbar == SignalSideModel.Sell && TouchesLevel(level.Price, current))
            return CreateSignal(level, "S_Pin_1", current.High, current);

        if (patterns.Engulf == SignalSideModel.Sell && TouchesLevel(level.Price, current, previous))
            return CreateSignal(level, "S_Eng_1", current.High, current);

        if (patterns.FractalTop == SignalSideModel.Sell && current.IsBearish &&
            TouchesLevel(level.Price, current, previous, earlier))
            return CreateSignal(level, "S_Top_1", previous.High, current);

        if (patterns.HaramiSingle == SignalSideModel.Sell && TouchesLevel(level.Price, current, previous, earlier))
            return CreateSignal(level, "S_Harami_1", Math.Max(previous.High, current.High), current);

        return null;
    }

    /**
        K线接触到关键位（日 VWAP）
        出现看涨信号：看涨pinbar、看涨吞没、底分型、孕线上破。
     */
    private static SignalModel MatchLong(TradeLevelModel level, HanJinSignalScanModel patterns, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (patterns.Pinbar == SignalSideModel.Buy && TouchesLevel(level.Price, current))
            return CreateSignal(level, "L_Pin_1", current.Low, current);

        if (patterns.Engulf == SignalSideModel.Buy && TouchesLevel(level.Price, current, previous))
            return CreateSignal(level, "L_Eng_1", current.Low, current);

        if (patterns.FractalBottom == SignalSideModel.Buy && current.IsBullish &&
            TouchesLevel(level.Price, current, previous, earlier))
            return CreateSignal(level, "L_Bot_1", previous.Low, current);

        if (patterns.HaramiSingle == SignalSideModel.Buy && TouchesLevel(level.Price, current, previous, earlier))
            return CreateSignal(level, "L_Harami_1", Math.Min(previous.Low, current.Low), current);

        return null;
    }

    // 价位落在某一根 K 线的高低之间就算碰到（含端点）。
    private static bool TouchesLevel(double price, params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (candle.Low <= price && candle.High >= price)
                return true;
        }

        return false;
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
