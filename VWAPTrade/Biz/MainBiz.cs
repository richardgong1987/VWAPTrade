using System;

namespace cAlgo.Robots;

public class MainBiz {
    // 关键位就是图上那条黄线（日 VWAP）。方向已经由 VwapStack 的闸门定好（见 SignalDetector），
    // 这里只判断形态，以及形态有没有长在关键位上。碰到了就出信号，没碰到就不做。
    //
    // 「碰到」只看这个形态自己用到的那几根 K 线：pinbar 一根、吞没两根、分型和孕线三根。
    // 笼统地拿三根去判断是错的 —— 单根形态会被隔壁那根的触碰放行。
    public static SignalModel Evaluate(CandleModel current, CandleModel previous, CandleModel earlier, TradeLevelModel level) {
        if (level == null || !level.IsConfigured)
            return null;

        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);

        return level.Side == SignalSideModel.Sell
            ? MatchShort(level, scanResult, current, previous, earlier)
            : MatchLong(level, scanResult, current, previous, earlier);
    }

    /*
        一. 假突破/反转
           空单开仓条件
           K线接触到关键位（日 VWAP）
           出现看跌信号：看跌pinbar、看跌吞没、顶分型、孕线下破。
     */
    private static SignalModel MatchShort(TradeLevelModel level, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.Pinbar == SignalSideModel.Sell && Utils.AnyBarTouchesLevel(level.Price, current))
            return CreateSignal(level, "S_Pin_1", current.High, current);

        if (scanResult.Engulf == SignalSideModel.Sell && Utils.AnyBarTouchesLevel(level.Price, current, previous))
            return CreateSignal(level, "S_Eng_1", current.High, current);

        if (scanResult.FractalTop == SignalSideModel.Sell && Utils.AnyBarIsShort(current) &&
            Utils.AnyBarTouchesLevel(level.Price, current, previous, earlier))
            return CreateSignal(level, "S_Top_1", previous.High, current);

        if (scanResult.HaramiSingle == SignalSideModel.Sell && Utils.AnyBarTouchesLevel(level.Price, current, previous, earlier))
            return CreateSignal(level, "S_Harami_1", Math.Max(previous.High, current.High), current);

        return null;
    }

    /**
     一. 假突破/反转
        多单开仓条件
        K线接触到关键位（日 VWAP）
        出现看涨信号：看涨pinbar、看涨吞没、底分型、孕线上破。
     */
    private static SignalModel MatchLong(TradeLevelModel level, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous, CandleModel earlier) {
        if (scanResult.Pinbar == SignalSideModel.Buy && Utils.AnyBarTouchesLevel(level.Price, current))
            return CreateSignal(level, "L_Pin_1", current.Low, current);

        if (scanResult.Engulf == SignalSideModel.Buy && Utils.AnyBarTouchesLevel(level.Price, current, previous))
            return CreateSignal(level, "L_Eng_1", current.Low, current);

        if (scanResult.FractalBottom == SignalSideModel.Buy && Utils.AnyBarIsLong(current) &&
            Utils.AnyBarTouchesLevel(level.Price, current, previous, earlier))
            return CreateSignal(level, "L_Bot_1", previous.Low, current);

        if (scanResult.HaramiSingle == SignalSideModel.Buy && Utils.AnyBarTouchesLevel(level.Price, current, previous, earlier))
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
