namespace cAlgo.Robots;

public class Utils {
    public static bool AnyBarTouchesLevel(double level, params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (TouchesLevel(candle, level))
                return true;
        }

        return false;
    }

    private static bool TouchesLevel(CandleModel candle, double level) {
        return candle.Low <= level && candle.High >= level;
    }

    // 看跌确认：touchCandles 里有 K 线接触到该价位，且收盘价低于该价位。
    public static bool TouchesAndClosesBelow(double level, double closePrice, params CandleModel[] touchCandles) {
        return AnyBarTouchesLevel(level, touchCandles) && closePrice < level;
    }

    // 看涨确认：touchCandles 里有 K 线接触到该价位，且收盘价高于该价位。
    public static bool TouchesAndClosesAbove(double level, double closePrice, params CandleModel[] touchCandles) {
        return AnyBarTouchesLevel(level, touchCandles) && closePrice > level;
    }

    public static bool AnyBarIsLong(params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (!candle.IsBullish) {
                return false;
            }
        }

        return true;
    }

    public static bool AnyBarIsShort(params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (!candle.IsBearish) {
                return false;
            }
        }

        return true;
    }

}
