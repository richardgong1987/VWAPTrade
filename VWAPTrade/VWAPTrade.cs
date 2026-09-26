using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
    [Parameter("VWAP斜率速度最小值 (30分钟标准化, ATR倍数, 0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10.0, Step = 0.01, Group = "VWAP过滤")]
    public double VwapSlopeRateMin { get; set; }

    [Parameter("斜率回看K线数 N", DefaultValue = 6, MinValue = 1, MaxValue = 200, Group = "VWAP过滤")]
    public int VwapSlopeLookbackBars { get; set; }

    [Parameter("ATR归一周期", DefaultValue = Atr14SourceModel.ATR14_H1, Group = "VWAP过滤")]
    public Atr14SourceModel Atr14Source { get; set; }

    // 扩口变化用独立开关，不沿用「0 = 关闭」：它允许负值，有效区间还可能跨过 0。
    [Parameter("扩口变化过滤", DefaultValue = false, Group = "VWAP过滤")]
    public bool UseGapChangeFilter { get; set; }

    [Parameter("扩口变化最小值 (30M标准化)", DefaultValue = -0.04, MinValue = -10.0, MaxValue = 10.0, Step = 0.01, Group = "VWAP过滤")]
    public double GapChangeRateMin { get; set; }

    [Parameter("扩口变化最大值 (30M标准化)", DefaultValue = 0.01, MinValue = -10.0, MaxValue = 10.0, Step = 0.01, Group = "VWAP过滤")]
    public double GapChangeRateMax { get; set; }

    // Catches an ageing trend: the gap is already wide but the daily VWAP has slowed (docs/VWAP.docx).
    // Defaults to 0 (off) so existing instances trade as before; 0.01 is the centre still to be validated.
    [Parameter("斜率效率最小值 (0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 1.0, Step = 0.001, Group = "VWAP过滤")]
    public double SlopeEfficiencyMin { get; set; }

    // Catches a gap that still widens, but too slowly for how wide it already is (docs/VWAP2.docx).
    // Defaults to 0 (off) so existing instances trade as before; 0.0045 is the centre still to be validated.
    [Parameter("扩口效率最小值 (0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 1.0, Step = 0.0005, Group = "VWAP过滤")]
    public double ExpansionEfficiencyMin { get; set; }

    // These property names stay so existing cTrader instances retain their saved values from the
    // earlier long-only version. The gate now counts the opposite side for both trade directions.
    [Parameter("信号前回看K线数", DefaultValue = 6, MinValue = 1, MaxValue = 200, Group = "反向黄线过滤")]
    public int LongBelowDailyVwapLookbackBars { get; set; }

    [Parameter("反向侧K线阻断根数 (0=关闭)", DefaultValue = 3, MinValue = 0, MaxValue = 200, Group = "反向黄线过滤")]
    public int LongBelowDailyVwapBlockCount { get; set; }

    // 先离开日 VWAP、再回踩，才允许做这根形态（V2 第 6 节）。0 = 关闭，结果与没有这道闸门时完全一致。
    [Parameter("离开最小距离 (ATR倍数, 0=关闭)", DefaultValue = 0.0, MinValue = 0.0, MaxValue = 10.0, Step = 0.1, Group = "Departure离开确认")]
    public double DepartureMin { get; set; }

    [Parameter("离开连续确认K线数", DefaultValue = 3, MinValue = 1, MaxValue = 100, Group = "Departure离开确认")]
    public int DepartureConfirmBars { get; set; }

    [Parameter("离开后最大等待K线数 (0=不限制)", DefaultValue = 0, MinValue = 0, MaxValue = 500, Group = "Departure离开确认")]
    public int DepartureMaxWaitBars { get; set; }

    // 只做「最终交易许可」，不影响任何指标计算：All 模式的成交结果与没有这个开关时完全一致。
    [Parameter("交易方向", DefaultValue = TradeDirectionPermissionModel.All, Group = "交易方向")]
    public TradeDirectionPermissionModel TradeDirectionMode { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = true, Group = "开发调试")]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "VWAPTrades.csv", Group = "开发调试")]
    public string FileName { get; set; }

    // 只留 OnBar / OnTick 还要用到的那几件；其余零件在组装时用完即走。
    private VwapSeries _vwapSeries;
    private SignalDetector _signalDetector;
    private OrderExecutor _orderExecutor;
    private StrongSignalCsvLogger _strongSignalLog;
    private VwapLines _vwapLines;

    private SignalMarkers _signalMarkers;

    // Optimisation only: GetFitness checks the whole run year by year, and GetFitnessArgs does not
    // carry the window, so the robot has to remember where it started.
    private DateTime _optimisationWindowStart;

    // 组合根：读参数 → 校验 → 分三路组装（找信号、下单、画图）。这里只做接线，不放任何规则。
    protected override void OnStart() {
        _optimisationWindowStart = Server.Time;
        LaunchDebug();
        var vwapFilters = new VwapFilterSettingsModel(VwapGapMin, VwapSlopeRateMin, UseGapChangeFilter, GapChangeRateMin, GapChangeRateMax,
            SlopeEfficiencyMin, ExpansionEfficiencyMin);
        var oppositeDailyVwap = new OppositeDailyVwapSettingsModel(LongBelowDailyVwapLookbackBars, LongBelowDailyVwapBlockCount);
        var departureSettings = new DepartureSettingsModel(DepartureMin, DepartureConfirmBars, DepartureMaxWaitBars);
        string error = StartupCheck.FindError(Bars.TimeFrame.Equals(TimeFrame.Minute5), Bars.TimeFrame.ToString(), OrderLabel,
            VwapSlopeLookbackBars, vwapFilters, departureSettings, oppositeDailyVwap);

        if (error != null) {
            Print("*****参数有误，已停止：{0}", error);
            Stop();
            return;
        }

        var settings = new TradeSettingsModel(RiskPct, TakeProfitR, StopOffsetTicks, BreakevenTriggerR, BreakevenOffsetTicks, vwapFilters,
            VwapSlopeLookbackBars, Atr14Source, TradeDirectionMode, oppositeDailyVwap);
        PrintSettings(settings, departureSettings);

        BuildSignalPipeline(settings, departureSettings);
        BuildOrderPipeline(settings);
        BuildChartDrawing();

        Print("*****VWAP break and reverse started.");
    }

    // Signal pipeline: VWAP series → two ATRs → opposite-side recovery history → Departure state → signal detection.
    private void BuildSignalPipeline(TradeSettingsModel settings, DepartureSettingsModel departureSettings) {
        _vwapSeries = new VwapSeries(Bars);
        _vwapSeries.Update();

        // 两套 ATR14 始终都算、都写进 CSV，「ATR归一周期」只决定过滤器拿哪一套当分母。
        var atr14 = new Atr14Pair(new Atr14Series(Indicators, MarketData.GetBars(TimeFrame.Minute5)),
            new Atr14Series(Indicators, MarketData.GetBars(TimeFrame.Hour)));

        var departureTracker = new DepartureTracker(departureSettings);
        var departureFeed = new DepartureFeed(Bars, _vwapSeries, atr14, settings.Atr14Source, departureTracker);
        var oppositeDailyVwapFeed = new OppositeDailyVwapFeed(Bars, _vwapSeries);
        _signalDetector = new SignalDetector(Bars, _vwapSeries, atr14, settings, departureFeed, departureTracker, oppositeDailyVwapFeed);
    }

    // 下单这一路：CSV 文件 → 定价定量 → 记账 → 保本止损 → 执行。
    private void BuildOrderPipeline(TradeSettingsModel settings) {
        var csvLogger = new TradeCsvLogger(new TradeCsvFile(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName));
        Print("****CSV logger path: {0}", csvLogger.FilePath);

        _strongSignalLog = new StrongSignalCsvLogger(Path.GetDirectoryName(csvLogger.FilePath), ResetTradeLogOnStart, SymbolName, settings);
        Print("****Debug CSV path: {0}", _strongSignalLog.FilePath);

        var symbolModel = new CAlgoSymbolModel(Symbol);
        var planner = new OrderPlanner(symbolModel, settings);
        var journal = new TradeJournal(this, csvLogger, SymbolName, Bars.TimeFrame.ToString());
        var breakeven = new BreakevenProtector(this, symbolModel, settings);
        _orderExecutor = new OrderExecutor(this, SymbolName, OrderLabel.Trim(), planner, settings, journal, breakeven);
    }

    // 画图这一路：三条 VWAP 线 + 入场标记。要在 VWAP 序列建好之后。
    private void BuildChartDrawing() {
        _signalMarkers = new SignalMarkers(Chart, Symbol.TickSize);
        _vwapLines = new VwapLines(Chart, _vwapSeries);
        _vwapLines.Draw();
        // 画不出线时先看这一行：收线 K 线数为 0 就是还没历史数据，图形对象数为 0 就是这个周期不画（日线及以上）。
        Print("*****VWAP lines | ClosedBars: {0}, ChartObjects: {1}, TimeFrame: {2}", _vwapSeries.Count, _vwapLines.DrawnObjectCount,
            Bars.TimeFrame);
    }

    protected override void OnBar() {
        // 先补算新收线那根 K 线的 VWAP，画线和找信号都要用它。
        _vwapSeries?.Update();
        _vwapLines?.Draw();
        HandleClosedBarSignal();
        _orderExecutor?.ManageOpenPositions();
    }

    // 保本止损要盯的是盘中价格，不能只在收线时检查，否则一根 K 线里冲到触发价又回落就错过了。
    protected override void OnTick() {
        _orderExecutor?.ManageOpenPositions();
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
    }

    // Per closed bar: find the signal, try to trade it, then show what happened on the chart and in debug.csv.
    private void HandleClosedBarSignal() {
        SignalModel signalModel = _signalDetector.DetectOnClosedBar();

        if (signalModel == null)
            return;

        EntryOutcomeModel outcome = _orderExecutor.TryEnter(signalModel);

        if (outcome.IsOrdered) {
            _signalMarkers.Draw(signalModel);
            _signalDetector.ResetAfterEntry();
        }

        LogStrongSignal(signalModel, outcome);
    }

    // Runs after the order, and a file error only prints: debug.csv must never cost a trade or
    // stop the bot (for example while the file is open in another program).
    private void LogStrongSignal(SignalModel signalModel, EntryOutcomeModel outcome) {
        try {
            _strongSignalLog.Append(signalModel, outcome, Server.Time);
        } catch (IOException exception) {
            Print("*****Debug CSV not written: {0}", exception.Message);
        }
    }

    // 风险% 留 0 就等于这个 cBot 不会下任何单，启动时说清楚，免得以为是信号没出。
    private void PrintSettings(TradeSettingsModel settings, DepartureSettingsModel departureSettings) {
        Print(
            "*****Trade settings | RiskPct: {0}, TakeProfitR: {1}, StopOffsetTicks: {2}, BreakevenTriggerR: {3}, BreakevenOffsetTicks: {4}",
            settings.RiskPct, settings.TakeProfitR, settings.StopOffsetTicks, settings.BreakevenTriggerR, settings.BreakevenOffsetTicks);
        Print(
            "*****VWAP filters | GapMin: {0}, SlopeRateMin: {1}, SlopeEfficiencyMin: {2}, ExpansionEfficiencyMin: {3}, " +
            "LookbackN: {4} ({5} min), Atr: {6} (0 = filter off)", settings.VwapFilters.GapMin, settings.VwapFilters.SlopeRateMin,
            settings.VwapFilters.SlopeEfficiencyMin, settings.VwapFilters.ExpansionEfficiencyMin, settings.VwapSlopeLookbackBars,
            settings.VwapSlopeLookbackBars * 5, settings.Atr14Source);
        Print("*****GapChange filter | Enabled: {0}, Min: {1}, Max: {2} | TradeDirection: {3}", settings.VwapFilters.UseGapChangeFilter,
            settings.VwapFilters.GapChangeRateMin, settings.VwapFilters.GapChangeRateMax, settings.TradeDirectionMode);
        Print("*****Opposite-side daily VWAP gate | Enabled: {0}, LookbackBars: {1}, BlockCount: {2}", settings.OppositeDailyVwap.IsEnabled,
            settings.OppositeDailyVwap.LookbackBars, settings.OppositeDailyVwap.BlockCount);
        Print("*****Departure gate | Enabled: {0}, Min: {1}, ConfirmBars: {2}, MaxWaitBars: {3} (0 = no limit)",
            departureSettings.IsEnabled, departureSettings.DepartureMin, departureSettings.ConfirmBars, departureSettings.MaxWaitBars);

        if (settings.RiskPct <= 0.0)
            Print("*****Risk % is 0, so this cBot will never trade. Set it above 0 to enable orders.");
    }

    private void LaunchDebug() {
        if (!IsDebug)
            return;

        // Debugger.Launch() is the Windows route; on macOS, wait for Rider to attach instead.
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!Debugger.IsAttached && DateTime.UtcNow < deadline)
            Thread.Sleep(200);
    }

    // 输出目录按运行模式分开、互不覆盖：回测目录由脚本每次清空重建，模拟/实盘目录只追加、从不删除。
    // 回测经 run_conditions 传入绝对路径 FileName，此目录会被忽略（见 TradeCsvFile）。
    private string ResolveReportsDirectory() {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, ResolveReportsFolderName());
    }

    private string ResolveReportsFolderName() {
        if (IsBacktesting)
            return "trading_reports";

        return Account.IsLive ? "release_trading_reports" : "simulate_trading_reports";
    }

    // Called once per pass by the desktop Optimisation tab only — a plain backtest, CLI or GUI,
    // never calls it. Passes with a losing (or idle) calendar year sink below every survivor;
    // survivors keep cTrader's own score. See AnnualFitness.
    protected override double GetFitness(GetFitnessArgs args) {
        List<ClosedTradeModel> closedTrades = args.History
            .Select(trade => new ClosedTradeModel(trade.ClosingTime, trade.NetProfit)).ToList();

        var stats = new FitnessStatsModel {
            NetProfit = args.NetProfit, WinningTrades = args.WinningTrades, MaxEquityDrawdownPercent = args.MaxEquityDrawdownPercentages
        };

        return new AnnualFitness(_optimisationWindowStart, Server.Time).Calculate(closedTrades, stats);
    }
}
