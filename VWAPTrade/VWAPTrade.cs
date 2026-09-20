using System;
using System.Diagnostics;
using System.IO;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(AccessRights = AccessRights.FullAccess, AddIndicators = true)]
public class VWAPTrade : Robot {
    [Parameter("订单标签", DefaultValue = "VWAPTrade-label")]
    public string OrderLabel { get; set; }


    [Parameter("风险%", DefaultValue = 1, MinValue = 0)]
    public double RiskPct { get; set; }

    [Parameter("止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1)]
    public double TakeProfitR { get; set; }

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

        var settings = new TradeSettingsModel(RiskPct, TakeProfitR);
        PrintSettings(settings);

        _vwapSeries = new VwapSeries(Bars);
        _vwapSeries.Update();

        _signalDetector = new SignalDetector(Bars, _vwapSeries, settings);
        _signalMarkers = new SignalMarkers(Chart, Symbol.TickSize);
        _vwapSlim = new VwapSlim(Chart, _vwapSeries);
        _vwapSlim.Draw();

        _csvLogger = new TradeCsvLogger(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName);
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        var riskGuard = new RiskGuard();
        var planner = new OrderPlanner(new CAlgoSymbolModel(Symbol), riskGuard);
        _orderExecutor = new OrderExecutor(this, SymbolName, Bars.TimeFrame.ToString(), OrderLabel.Trim(), planner, riskGuard,
            _csvLogger);

        Print("*****VWAP break and reverse started.");
    }

    // 风险% 留 0 就等于这个 cBot 不会下任何单，启动时说清楚，免得以为是信号没出。
    private void PrintSettings(TradeSettingsModel settings) {
        Print("*****Trade settings | RiskPct: {0}, TakeProfitR: {1}", settings.RiskPct, settings.TakeProfitR);

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
