using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(AccessRights = AccessRights.None, AddIndicators = true)]
public class VWAPTrade : Robot {
    [Parameter("订单标签", DefaultValue = "VWAPTrade-label")]
    public string OrderLabel { get; set; }


    [Parameter("空1风险1%", DefaultValue = 1, MinValue = 0, Group = "空1")]
    public double Short1RiskPct { get; set; }

    [Parameter("空1入场价", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double PDH1 { get; set; }

    [Parameter("空1止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "空1")]
    public double Short1TPPrice { get; set; }

    [Parameter("空2风险%", DefaultValue = 0, MinValue = 0, Group = "空2")]
    public double Short2RiskPct { get; set; }

    [Parameter("空2入场价", DefaultValue = 0, MinValue = 0, Group = "空2")]
    public double PDH2 { get; set; }

    [Parameter("空2止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "空2")]
    public double Short2TPPrice { get; set; }

    [Parameter("空3风险%", DefaultValue = 0, MinValue = 0, Group = "空3")]
    public double Short3RiskPct { get; set; }

    [Parameter("空3入场价", DefaultValue = 0, MinValue = 0, Group = "空3")]
    public double PDH3 { get; set; }

    [Parameter("空3止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "空3")]
    public double Short3TPPrice { get; set; }


    [Parameter("多1风险%", DefaultValue = 0, MinValue = 0, Group = "多1")]
    public double Long1RiskPct { get; set; }

    [Parameter("多1入场价", DefaultValue = 0, MinValue = 0, Group = "多1")]
    public double PDL1 { get; set; }

    [Parameter("多1止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "多1")]
    public double Long1TPPrice { get; set; }


    [Parameter("多2风险%", DefaultValue = 0, MinValue = 0, Group = "多2")]
    public double Long2RiskPct { get; set; }

    [Parameter("多2入场价", DefaultValue = 0, MinValue = 0, Group = "多2")]
    public double PDL2 { get; set; }

    [Parameter("多2止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "多2")]
    public double Long2TPPrice { get; set; }


    [Parameter("多3风险%", DefaultValue = 0, MinValue = 0, Group = "多3")]
    public double Long3RiskPct { get; set; }

    [Parameter("多3入场价", DefaultValue = 0, MinValue = 0, Group = "多3")]
    public double PDL3 { get; set; }

    [Parameter("多3止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "多3")]
    public double Long3TPPrice { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = false, Group = "开发调试")]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "VWAPTrades.csv", Group = "开发调试")]
    public string FileName { get; set; }

    private SignalDetector _signalDetector;
    private SignalMarkers _signalMarkers;
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

        List<TradeLevelModel> tradeLevels = BuildTradeLevels();
        PrintConfiguredLevels(tradeLevels);

        _signalDetector = new SignalDetector(Bars, tradeLevels);
        _signalMarkers = new SignalMarkers(Chart, Symbol.TickSize);
        _vwapSlim = new VwapSlim(Chart, Bars);
        _vwapSlim.Draw();

        _csvLogger = new TradeCsvLogger(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName);
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        var riskGuard = new RiskGuard();
        var planner = new OrderPlanner(new CAlgoSymbolModel(Symbol), riskGuard);
        _orderExecutor = new OrderExecutor(this, SymbolName, Bars.TimeFrame.ToString(), OrderLabel.Trim(), planner, riskGuard,
            _csvLogger);
        CancelPendingOrdersOfClearedLevels(tradeLevels);

        Print("*****PDH/PDL Break and Reverse started.");
    }

    // 手工输入的六档价位。价格或风险百分比留 0 的那一档不参与判断（见 TradeLevelModel）。
    private List<TradeLevelModel> BuildTradeLevels() {
        return new List<TradeLevelModel> {
            new("Short1", SignalSideModel.Sell, PDH1, Short1RiskPct, Short1TPPrice),
            new("Short2", SignalSideModel.Sell, PDH2, Short2RiskPct, Short2TPPrice),
            new("Short3", SignalSideModel.Sell, PDH3, Short3RiskPct, Short3TPPrice),
            new("Long1", SignalSideModel.Buy, PDL1, Long1RiskPct, Long1TPPrice),
            new("Long2", SignalSideModel.Buy, PDL2, Long2RiskPct, Long2TPPrice),
            new("Long3", SignalSideModel.Buy, PDL3, Long3RiskPct, Long3TPPrice)
        };
    }

    // 全都没配置就等于这个 cBot 不会下任何单，启动时说清楚，免得以为是信号没出。
    private void PrintConfiguredLevels(List<TradeLevelModel> tradeLevels) {
        foreach (TradeLevelModel level in tradeLevels.Where(level => level.IsConfigured)) {
            Print("*****Level configured | Name: {0}, Side: {1}, Price: {2}, RiskPct: {3}, TakeProfitR: {4}", level.Name, level.Side,
                level.Price, level.RiskPct, level.TakeProfitR);
        }

        if (!tradeLevels.Any(level => level.IsConfigured))
            Print("*****No trade level configured. Set both 入场价 and 风险% on at least one level, or this cBot will never trade.");
    }

    // Setting a level's entry price to 0 withdraws that level: its unfilled orders are cancelled,
    // while positions that already filled keep running with their own stop loss and take profit.
    private void CancelPendingOrdersOfClearedLevels(List<TradeLevelModel> tradeLevels) {
        foreach (TradeLevelModel level in tradeLevels.Where(level => level.Price <= 0.0)) {
            _orderExecutor.CancelPendingOrdersForLevel(level.Name);
        }
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
        _vwapSlim?.Draw();
        HandleClosedBarSignal();
    }

    // 一根 K 线可能同时命中几档价位，每一档各自下单、各自画标记。
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
