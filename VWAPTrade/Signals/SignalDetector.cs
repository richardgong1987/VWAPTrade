using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

public class SignalDetector {
    // 形态最多要看三根 K 线（current / previous / earlier），而 OnBar() 里最后一根收盘 K 线
    // 的下标是 Count-2，所以至少要有 4 根才读得到 earlier。
    private const int MinimumBarCount = 4;

    // 写进订单标签与交易 CSV 的「关键位」列，两条线各自独立持仓。
    private const string DailyLevelName = "VWAP_D";
    private const string WeeklyLevelName = "VWAP_W";

    private readonly Bars _chartBars;
    private readonly VwapSeries _vwapSeries;
    private readonly TradeSettingsModel _settings;

    public SignalDetector(Bars chartBars, VwapSeries vwapSeries, TradeSettingsModel settings) {
        _chartBars = chartBars;
        _vwapSeries = vwapSeries;
        _settings = settings;
    }

    // 返回这根收盘 K 线上命中的全部信号，当日/当周两条 VWAP 各最多一个。
    public List<SignalModel> DetectOnClosedBar() {
        int closedBarIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()

        if (_chartBars.Count < MinimumBarCount || closedBarIndex >= _vwapSeries.Count)
            return new List<SignalModel>();

        CandleModel current = ReadCandle(closedBarIndex);
        CandleModel previous = ReadCandle(closedBarIndex - 1);
        CandleModel earlier = ReadCandle(closedBarIndex - 2);

        List<SignalModel> signals = MainBiz.Evaluate(current, previous, earlier, BuildLevels(closedBarIndex, current));

        foreach (SignalModel signal in signals) {
            signal.BarIndex = closedBarIndex;
            signal.BarTime = _chartBars.OpenTimes[closedBarIndex];
        }

        return signals;
    }

    // 关键位不再手工输入，而是这根 K 线上的当日/当周 VWAP。
    private IReadOnlyList<TradeLevelModel> BuildLevels(int closedBarIndex, CandleModel current) {
        VwapSampleModel vwap = _vwapSeries[closedBarIndex];

        return new List<TradeLevelModel> {
            CreateLevel(DailyLevelName, vwap.Daily, current),
            CreateLevel(WeeklyLevelName, vwap.Weekly, current)
        };
    }

    // 收盘价在关键位下方就只可能是空信号，上方就只可能是多信号（见 Utils 的两个确认条件），
    // 所以这一根 K 线上这一档的方向由收盘价相对 VWAP 的位置决定。
    private TradeLevelModel CreateLevel(string name, double price, CandleModel current) {
        SignalSideModel side = current.Close < price ? SignalSideModel.Sell : SignalSideModel.Buy;
        return new TradeLevelModel(name, side, price, _settings.RiskPct, _settings.TakeProfitR);
    }

    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }
}
