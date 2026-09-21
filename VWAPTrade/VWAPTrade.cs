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

    [Parameter("止损偏移点数", DefaultValue = 400, MinValue = 0, MaxValue = 2000, Group = "风控配置")]
    public int StopOffsetTicks { get; set; }

    [Parameter("保护止损触发R (0=关闭)", DefaultValue = 1.0, MinValue = 0.0, MaxValue = 30.0, Step = 0.1, Group = "风控配置")]
    public double BreakevenTriggerR { get; set; }

    [Parameter("保护止损偏移点数", DefaultValue = 50, MinValue = 0, MaxValue = 2000, Group = "风控配置")]
    public int BreakevenOffsetTicks { get; set; }

    [Parameter("VWAP间距最小值 (ATR倍数, 0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10.0, Step = 0.05, Group = "VWAP过滤")]
    public double VwapGapMin { get; set; }

    [Parameter("VWAP斜率最小值 (ATR倍数, 0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10.0, Step = 0.05, Group = "VWAP过滤")]
    public double VwapSlopeMin { get; set; }

    [Parameter("斜率回看K线数", DefaultValue = 12, MinValue = 1, MaxValue = 200, Group = "VWAP过滤")]
    public int VwapSlopeLookbackBars { get; set; }

    [Parameter("ATR归一周期", DefaultValue = Atr14SourceModel.ATR14_H1, Group = "VWAP过滤")]
    public Atr14SourceModel Atr14Source { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = false, Group = "开发调试")]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "VWAPTrades.csv", Group = "开发调试")]
    public string FileName { get; set; }

    private SignalDetector _signalDetector;
    private SignalMarkers _signalMarkers;
    private VwapSeries _vwapSeries;
    private VwapSlim _vwapSlim;
    private OrderExecutor _orderExecutor;
    private TradeCsvLogger _csvLogger;

    protected override void OnStart() {
        // A blank label would make every "_L"/"_S" label on the symbol look like this bot's order.
        if (string.IsNullOrWhiteSpace(OrderLabel)) {
            Print("*****OrderLabel must not be empty. cBot stopped.");
            Stop();
            return;
        }

        LaunchDebug();

        var settings = new TradeSettingsModel(RiskPct, TakeProfitR, StopOffsetTicks, BreakevenTriggerR, BreakevenOffsetTicks,
            VwapGapMin, VwapSlopeMin, VwapSlopeLookbackBars);
        PrintSettings(settings);

        _vwapSeries = new VwapSeries(Bars);
        _vwapSeries.Update();

        // 间距与斜率都按 ATR14 归一，周期由参数选。显式按周期取 K 线，不用图表当前周期，
        // 这样换到别的周期挂载时行为不变。
        TimeFrame atrTimeFrame = Atr14Source == Atr14SourceModel.ATR14_M5 ? TimeFrame.Minute5 : TimeFrame.Hour;
        var atr14 = new Atr14Series(Indicators, MarketData.GetBars(atrTimeFrame));

        _signalDetector = new SignalDetector(Bars, _vwapSeries, atr14, settings);
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
        _orderExecutor = new OrderExecutor(this, SymbolName, Bars.TimeFrame.ToString(), OrderLabel.Trim(), planner, riskGuard, _csvLogger,
            symbolModel, settings);

        Print("*****VWAP break and reverse started.");
    }

    // 风险% 留 0 就等于这个 cBot 不会下任何单，启动时说清楚，免得以为是信号没出。
    private void PrintSettings(TradeSettingsModel settings) {
        Print(
            "*****Trade settings | RiskPct: {0}, TakeProfitR: {1}, StopOffsetTicks: {2}, BreakevenTriggerR: {3}, BreakevenOffsetTicks: {4}",
            settings.RiskPct, settings.TakeProfitR, settings.StopOffsetTicks, settings.BreakevenTriggerR, settings.BreakevenOffsetTicks);
        Print("*****VWAP filters | GapMin: {0}, SlopeMin: {1}, SlopeLookbackBars: {2}, Atr: {3} (0 = filter off)",
            settings.VwapGapMin, settings.VwapSlopeMin, settings.VwapSlopeLookbackBars, Atr14Source);

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
            }
        }
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
    }

    protected override void OnBarClosed() { }
}
