using System;
using System.Diagnostics;
using System.IO;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.FullAccess, AddIndicators = false)]
public class VWAPTrade : Robot {
    [Parameter("订单标签", DefaultValue = "VWAPTrade-label")]
    public string OrderLabel { get; set; }


    [Parameter("风险%", DefaultValue = 1, MinValue = 0)]
    public double RiskPct { get; set; }

    [Parameter("止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1)]
    public double TakeProfitR { get; set; }

    [Parameter("止损偏移点数", DefaultValue = 50, MinValue = 0, MaxValue = 2000, Group = "风控配置")]
    public int StopOffsetTicks { get; set; }

    [Parameter("保护止损触发R (0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 30.0, Step = 0.1, Group = "风控配置")]
    public double BreakevenTriggerR { get; set; }

    [Parameter("保护止损偏移点数", DefaultValue = 50, MinValue = 0, MaxValue = 2000, Group = "风控配置")]
    public int BreakevenOffsetTicks { get; set; }

    [Parameter("VWAP间距最小值 (ATR倍数, 0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10.0, Step = 0.05, Group = "VWAP过滤")]
    public double VwapGapMin { get; set; }

    // 旧参数叫 VwapSlopeMin，比的是原始斜率；这个比的是 30 分钟标准化速度，不是同一个量纲，
    // 所以换了名字，避免旧值被静默当成新阈值。N 与 ATR 口径不变时，旧阈值 × 6/N 才是对应的新阈值；
    // 不知道当时的 N 就不能换算，只能重新标定。
    [Parameter("VWAP斜率速度最小值 (30分钟标准化, ATR倍数, 0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10.0,
        Step = 0.01, Group = "VWAP过滤")]
    public double VwapSlopeRateMin { get; set; }

    [Parameter("斜率回看K线数 N", DefaultValue = 6, MinValue = 1, MaxValue = 200, Group = "VWAP过滤")]
    public int VwapSlopeLookbackBars { get; set; }

    [Parameter("ATR归一周期", DefaultValue = Atr14SourceModel.ATR14_H1, Group = "VWAP过滤")]
    public Atr14SourceModel Atr14Source { get; set; }

    // 扩口变化用独立开关，不沿用「0 = 关闭」：它允许负值，有效区间还可能跨过 0。
    [Parameter("扩口变化过滤", DefaultValue = false, Group = "VWAP过滤")]
    public bool UseGapChangeFilter { get; set; }

    [Parameter("扩口变化最小值 (30M标准化)", DefaultValue = -0.04, MinValue = -10.0, MaxValue = 10.0, Step = 0.01,
        Group = "VWAP过滤")]
    public double GapChangeRateMin { get; set; }

    [Parameter("扩口变化最大值 (30M标准化)", DefaultValue = 0.01, MinValue = -10.0, MaxValue = 10.0, Step = 0.01,
        Group = "VWAP过滤")]
    public double GapChangeRateMax { get; set; }

    // 先离开日 VWAP、再回踩，才允许做这根形态（V2 第 6 节）。0 = 关闭，结果与没有这道闸门时完全一致。
    [Parameter("离开最小距离 (ATR倍数, 0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10.0, Step = 0.1,
        Group = "Departure离开确认")]
    public double DepartureMin { get; set; }

    [Parameter("离开连续确认K线数", DefaultValue = 3, MinValue = 1, MaxValue = 100, Group = "Departure离开确认")]
    public int DepartureConfirmBars { get; set; }

    [Parameter("离开后最大等待K线数 (0=不限制)", DefaultValue = 0, MinValue = 0, MaxValue = 500, Group = "Departure离开确认")]
    public int DepartureMaxWaitBars { get; set; }

    // 只做「最终交易许可」，不影响任何指标计算：All 模式的成交结果与没有这个开关时完全一致。
    [Parameter("交易方向", DefaultValue = TradeDirectionModeModel.All, Group = "交易方向")]
    public TradeDirectionModeModel TradeDirectionMode { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = true, Group = "开发调试")]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "VWAPTrades.csv", Group = "开发调试")]
    public string FileName { get; set; }

    private SignalDetector _signalDetector;
    private DepartureTracker _departureTracker;
    private SignalMarkers _signalMarkers;
    private VwapSeries _vwapSeries;
    private VwapSlim _vwapSlim;
    private OrderExecutor _orderExecutor;
    private TradeCsvLogger _csvLogger;

    protected override void OnStart() {
        LaunchDebug();

        var vwapFilters = new VwapFilterSettingsModel(VwapGapMin, VwapSlopeRateMin, UseGapChangeFilter, GapChangeRateMin,
            GapChangeRateMax);
        var departureSettings = new DepartureSettingsModel(DepartureMin, DepartureConfirmBars, DepartureMaxWaitBars);
        string error = StartupCheck.FindError(Bars.TimeFrame.Equals(TimeFrame.Minute5), Bars.TimeFrame.ToString(), OrderLabel,
            VwapSlopeLookbackBars, vwapFilters, departureSettings);

        if (error != null) {
            Print("*****参数有误，已停止：{0}", error);
            Stop();
            return;
        }

        var settings = new TradeSettingsModel(RiskPct, TakeProfitR, StopOffsetTicks, BreakevenTriggerR, BreakevenOffsetTicks,
            vwapFilters, VwapSlopeLookbackBars, Atr14Source, TradeDirectionMode);
        PrintSettings(settings, departureSettings);

        _vwapSeries = new VwapSeries(Bars);
        _vwapSeries.Update();

        // 两套 ATR14 始终都算、都写进 CSV，「ATR归一周期」只决定过滤器拿哪一套当分母。
        var atr14 = new Atr14Pair(new Atr14Series(Indicators, MarketData.GetBars(TimeFrame.Minute5)),
            new Atr14Series(Indicators, MarketData.GetBars(TimeFrame.Hour)));

        _departureTracker = new DepartureTracker(departureSettings);
        _signalDetector = new SignalDetector(Bars, _vwapSeries, atr14, settings, _departureTracker);
        _signalMarkers = new SignalMarkers(Chart, Symbol.TickSize);
        _vwapSlim = new VwapSlim(Chart, _vwapSeries);
        _vwapSlim.Draw();
        // 画不出线时先看这一行：收线 K 线数为 0 就是还没历史数据，图形对象数为 0 就是这个周期不画（日线及以上）。
        Print("*****VWAP lines | ClosedBars: {0}, ChartObjects: {1}, TimeFrame: {2}", _vwapSeries.Count, _vwapSlim.DrawnObjectCount,
            Bars.TimeFrame);

        _csvLogger = new TradeCsvLogger(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName);
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        var riskGuard = new RiskGuard();
        var symbolModel = new CAlgoSymbolModel(Symbol);
        var planner = new OrderPlanner(symbolModel, riskGuard, settings);
        var journal = new TradeJournal(this, _csvLogger, SymbolName, Bars.TimeFrame.ToString());
        var breakeven = new BreakevenProtector(this, symbolModel, settings);
        _orderExecutor = new OrderExecutor(this, SymbolName, OrderLabel.Trim(), planner, riskGuard, settings, journal, breakeven);

        Print("*****VWAP break and reverse started.");
    }

    // 风险% 留 0 就等于这个 cBot 不会下任何单，启动时说清楚，免得以为是信号没出。
    private void PrintSettings(TradeSettingsModel settings, DepartureSettingsModel departureSettings) {
        Print(
            "*****Trade settings | RiskPct: {0}, TakeProfitR: {1}, StopOffsetTicks: {2}, BreakevenTriggerR: {3}, BreakevenOffsetTicks: {4}",
            settings.RiskPct, settings.TakeProfitR, settings.StopOffsetTicks, settings.BreakevenTriggerR, settings.BreakevenOffsetTicks);
        Print("*****VWAP filters | GapMin: {0}, SlopeRateMin: {1}, LookbackN: {2} ({3} min), Atr: {4} (0 = filter off)",
            settings.VwapFilters.GapMin, settings.VwapFilters.SlopeRateMin, settings.VwapSlopeLookbackBars,
            settings.VwapSlopeLookbackBars * 5, settings.Atr14Source);
        Print("*****GapChange filter | Enabled: {0}, Min: {1}, Max: {2} | TradeDirection: {3}",
            settings.VwapFilters.UseGapChangeFilter, settings.VwapFilters.GapChangeRateMin,
            settings.VwapFilters.GapChangeRateMax, settings.TradeDirectionMode);
        Print("*****Departure gate | Enabled: {0}, Min: {1}, ConfirmBars: {2}, MaxWaitBars: {3} (0 = no limit)",
            departureSettings.IsEnabled, departureSettings.DepartureMin, departureSettings.ConfirmBars,
            departureSettings.MaxWaitBars);

        if (settings.RiskPct <= 0.0)
            Print("*****Risk % is 0, so this cBot will never trade. Set it above 0 to enable orders.");
    }

    private void LaunchDebug() {
        if (IsDebug) {
            bool result = Debugger.Launch();
            if (!result) {
                Print("Debugger launch failed");
            }
        }
    }

    // 输出目录按运行模式分开、互不覆盖：回测目录由脚本每次清空重建，模拟/实盘目录只追加、从不删除。
    // 回测经 run_conditions 传入绝对路径 FileName，此目录会被忽略（见 TradeCsvLogger）。
    private string ResolveReportsDirectory() {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, ResolveReportsFolderName());
    }

    private string ResolveReportsFolderName() {
        if (IsBacktesting)
            return "trading_reports";

        return Account.IsLive ? "release_trading_reports" : "simulate_trading_reports";
    }

    protected override void OnBar() {
        // 先补算新收线那根 K 线的 VWAP，画线和找信号都要用它。
        _vwapSeries?.Update();
        _vwapSlim?.Draw();
        HandleClosedBarSignal();
        _orderExecutor?.ManageOpenPositions();
    }

    // 保本止损要盯的是盘中价格，不能只在收线时检查，否则一根 K 线里冲到触发价又回落就错过了。
    protected override void OnTick() {
        _orderExecutor?.ManageOpenPositions();
    }

    // 一根 K 线可能同时命中当日与当周两条 VWAP，每一档各自下单、各自画标记。
    private void HandleClosedBarSignal() {
        foreach (SignalModel signalModel in _signalDetector.DetectOnClosedBar()) {
            if (_orderExecutor.ExecuteIfSignal(signalModel)) {
                _signalMarkers.Draw(signalModel);
                // 开完仓这一段「先离开」就用掉了：下一笔必须重新走一遍离开确认（V2 第 6.3 节）。
                _departureTracker.ResetAfterEntry();
            }
        }
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
    }

    protected override void OnBarClosed() { }
}
