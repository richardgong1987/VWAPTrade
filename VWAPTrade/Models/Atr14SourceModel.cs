namespace cAlgo.Robots;

// GapX / SlopeX / GapChangeX 都要除以 ATR14 做归一，这里选用哪个周期的 ATR14。
// 枚举名直接显示在 cTrader 的参数下拉框里，所以按用户习惯的写法命名。
public enum Atr14SourceModel {
    ATR14_M5,
    ATR14_H1
}
