using System;

namespace cAlgo.Robots;

// M5 与 H1 两套 ATR14。两套始终都算、都写进 CSV，「ATR归一周期」参数只决定过滤器拿哪一套
// 当分母（docs/VWAP_Strong_V1.1.docx 第 2 节 SelectedATR、第 3.4 节）—— 留着另一套是为了日后离线 A/B，别删。
//
// 取不到就返回 NaN：所选那一套缺失时，开着的过滤器会拦下这根 K 线；未选中那一套缺失只是
// CSV 里对应研究字段留空，不影响交易。
public class Atr14Pair {
    private readonly Atr14Series _m5;
    private readonly Atr14Series _h1;

    public Atr14Pair(Atr14Series m5, Atr14Series h1) {
        _m5 = m5;
        _h1 = h1;
    }

    public double GetM5(DateTime time) => Read(_m5, time);

    public double GetH1(DateTime time) => Read(_h1, time);

    private static double Read(Atr14Series series, DateTime time) {
        return series != null && series.TryGetValue(time, out double atr) ? atr : double.NaN;
    }
}
