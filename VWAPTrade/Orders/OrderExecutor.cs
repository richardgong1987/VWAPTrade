using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// Places and tracks the strategy's cTrader orders. It gates on the risk guard and open
// exposure, asks OrderPlanner to size the order, submits it as a market order, and
// keeps the CSV row ids so opens and closes can be reconciled. All sizing math lives in
// the planner.
public class OrderExecutor {
    private const string EntryComment = "ENTRY";

    private readonly Robot _robot;
    private readonly string _symbolName;
    private readonly string _timeFrame;

    // Orders are labelled "{OrderLabel}_{level name}", e.g. "VWAPTrade-label_VWAP". The level
    // suffix gates each key level on its own label. The prefix marks this instance's orders, so
    // manual trades and other bots on the same symbol are ignored.
    private readonly string _strategyLabelPrefix;

    private readonly OrderPlanner _planner;
    private readonly RiskGuard _riskGuard;
    private readonly TradeCsvLogger _csvLogger;
    private readonly ISymbolModel _symbolModel;
    private readonly TradeSettingsModel _settings;

    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, double> _positionEntryEquities = new();

    // 开仓时的下单方案：保本止损要用它的 R（止损距离）算触发价，平仓那一行也要用它的 VWAP 读数。
    // 开仓时记下来，平仓时清掉。
    private readonly Dictionary<int, OrderPlanModel> _positionPlans = new();
    private readonly HashSet<int> _positionsProtected = new();

    public OrderExecutor(Robot robot, string symbolName, string timeFrame, string orderLabel, OrderPlanner planner,
        RiskGuard riskGuard, TradeCsvLogger csvLogger, ISymbolModel symbolModel, TradeSettingsModel settings) {
        _robot = robot;
        _symbolName = symbolName;
        _timeFrame = timeFrame;
        _strategyLabelPrefix = orderLabel + "_";
        _planner = planner;
        _riskGuard = riskGuard;
        _csvLogger = csvLogger;
        _symbolModel = symbolModel;
        _settings = settings;

        _robot.Positions.Closed += OnPositionClosed;
    }

    public void ManageOpenPositions() {
        ApplyBreakevenProtection();
    }

    // 止盈是开仓时定死的 TakeProfitR×R，已经挂在订单上由券商执行，这里不需要盯。
    // 持仓期间唯一要做的是浮盈达到 BreakevenTriggerR 时把止损推到保本位。
    private void ApplyBreakevenProtection() {
        // 0 = 关闭。必须在这里挡掉：触发距离为 0 会让保护在开仓瞬间就「触发」，然后因为保本价
        // 落在市价另一侧而被跳过，机会白白消耗掉 —— 看着像没保护，实则是行情决定的哑火。
        if (_settings.BreakevenTriggerR <= 0.0)
            return;

        foreach (Position position in _robot.Positions.Where(IsStrategyPosition).ToArray()) {
            ApplyBreakevenProtection(position);
        }
    }

    private void ApplyBreakevenProtection(Position position) {
        if (_positionsProtected.Contains(position.Id))
            return;

        if (!_positionPlans.TryGetValue(position.Id, out OrderPlanModel plan) || plan.RiskPrice <= 0.0)
            return;

        bool isLong = position.TradeType == TradeType.Buy;
        double profitDistance = _settings.BreakevenTriggerR * plan.RiskPrice;
        double trigger = isLong ? position.EntryPrice + profitDistance : position.EntryPrice - profitDistance;

        if (!HasReached(isLong, position.CurrentPrice, trigger))
            return;

        MoveStopToProtection(position, isLong);
    }

    private static bool HasReached(bool isLong, double price, double targetPrice) {
        return isLong ? price >= targetPrice : price <= targetPrice;
    }

    // The position must not turn back into a loss, so the stop moves to the entry price plus a
    // small offset in the profitable direction.
    private void MoveStopToProtection(Position position, bool isLong) {
        // 先记账再动手：券商拒单时也不要每个 tick 重试一次，日志里会留下失败原因。
        if (!_positionsProtected.Add(position.Id))
            return;

        double offset = _symbolModel.TickSize * _settings.BreakevenOffsetTicks;
        double protectiveStop = isLong ? position.EntryPrice + offset : position.EntryPrice - offset;

        // 止损不能落在市价的另一侧：券商会拒单，或者直接把仓位按市价平掉。
        bool stopIsPastMarket = isLong ? protectiveStop >= position.CurrentPrice : protectiveStop <= position.CurrentPrice;

        if (stopIsPastMarket) {
            _robot.Print("*****Protective stop skipped | Position: {0}, Stop: {1}, Price: {2}", position.Id, protectiveStop,
                position.CurrentPrice);
            return;
        }

        TradeResult result = _robot.ModifyPosition(position, protectiveStop, position.TakeProfit, ProtectionType.Absolute);

        if (!result.IsSuccessful) {
            _robot.Print("*****Protective stop failed | Position: {0}, Stop: {1}, Error: {2}", position.Id, protectiveStop, result.Error);
            return;
        }

        _robot.Print("*****Protective stop set | Position: {0}, Entry: {1}, Stop: {2}", position.Id, position.EntryPrice, protectiveStop);
    }

    public void Stop() {
        _robot.Positions.Closed -= OnPositionClosed;
    }

    public bool ExecuteIfSignal(SignalModel signalModel) {
        if (signalModel?.Level == null)
            return false;

        if (_riskGuard.ShouldBlockNewOrder(_robot.Server.Time)) {
            _robot.Print("*****Order skipped | Risk guard blocked new order. Time: {0}", _robot.Server.Time);
            return false;
        }

        // 方向许可：下单前的最后一道闸门（V1.1 第 6 节第 7 步）。放在算仓位之前，
        // 不给一个注定要拒的信号做定价；也不回头去改任何指标 —— All 模式与没有这个开关时一致。
        if (!TradeDirectionGate.IsAllowed(_settings.TradeDirectionMode, signalModel.Level.Side)) {
            _robot.Print("*****Order skipped | TradeDirection {0} blocks a {1} signal.", _settings.TradeDirectionMode,
                signalModel.Level.Side);
            return false;
        }

        string label = _strategyLabelPrefix + signalModel.Level.Name;

        if (HasPositionForLevel(label)) {
            _robot.Print("*****Order skipped | Level {0} already has an open position on symbol: {1}",
                signalModel.Level.Name, _symbolName);
            return false;
        }

        OrderPlanModel planModel = _planner.CreatePlan(signalModel, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Level: {0}, Reason: {1}", signalModel.Level.Name, planModel.RejectReason);
            return false;
        }

        planModel.Label = label;
        planModel.SignalName = signalModel.Label;
        planModel.KeyLevel = signalModel.Level.Name;
        CopyVwapReading(planModel, signalModel);

        return ExecutePlan(planModel);
    }

    private void CopyVwapReading(OrderPlanModel planModel, SignalModel signalModel) {
        planModel.VwapReading = signalModel.Strong;
        planModel.VwapMetrics = signalModel.Metrics;
        planModel.VwapFilters = _settings.VwapFilters;
        planModel.TradeDirectionMode = _settings.TradeDirectionMode;
    }

    // Gates on the level's own label, so the other levels stay free to open their own position.
    // Checks live broker state rather than in-memory maps, so a restart does not stack a second order.
    private bool HasPositionForLevel(string label) {
        return _robot.Positions.Any(position => position.SymbolName == _symbolName && position.Label == label);
    }

    private bool ExecutePlan(OrderPlanModel planModel) {
        _robot.Print(
            "*****Order plan | Level: {0}, Side: {1}, Entry: {2}, Stop: {3}, TakeProfit: {4}, RiskPrice: {5}, StopLossPips: {6}, RiskMoney: {7}, EstimatedRiskMoney: {8}, Lots: {9}, VolumeUnits: {10}",
            planModel.KeyLevel, planModel.DirectionModel, planModel.EntryPrice, planModel.StopPrice, planModel.TakeProfitPrice,
            planModel.RiskPrice, planModel.StopLossPips, planModel.RiskMoney, planModel.EstimatedRiskMoney, planModel.Lots,
            planModel.VolumeInUnits);

        TradeResult result = SubmitOrder(planModel);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return false;
        }

        _robot.Print("*****Order submitted | Label: {0}", planModel.Label);
        return RecordMarketEntry(planModel, result.Position);
    }

    private TradeResult SubmitOrder(OrderPlanModel planModel) {
        return _robot.ExecuteMarketOrder(ToTradeType(planModel.DirectionModel), _symbolName, planModel.VolumeInUnits, planModel.Label,
            planModel.StopLossPips, planModel.TakeProfitPips, EntryComment);
    }

    private bool RecordMarketEntry(OrderPlanModel planModel, Position position) {
        string csvId = _csvLogger.AppendEntry(planModel, position, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return false;

        _positionCsvIds[position.Id] = csvId;
        _positionEntryEquities[position.Id] = planModel.AccountEquity;
        _positionPlans[position.Id] = planModel;
        _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
        return true;
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args?.Position == null || !IsStrategyPosition(args.Position))
            return;

        string csvId = GetPositionCsvId(args.Position);
        double entryEquity = GetPositionEntryEquity(args.Position);
        double closePrice = GetClosePrice(args.Position);
        _positionPlans.TryGetValue(args.Position.Id, out OrderPlanModel entryPlan);
        string closeRecordId = _csvLogger.AppendClose(args.Position, args.Reason, csvId, _symbolName, _timeFrame, _robot.Server.Time,
            closePrice, entryEquity, _robot.Account.Equity, entryPlan);

        _positionCsvIds.Remove(args.Position.Id);
        _positionEntryEquities.Remove(args.Position.Id);
        _positionPlans.Remove(args.Position.Id);
        _positionsProtected.Remove(args.Position.Id);

        if (!string.IsNullOrWhiteSpace(closeRecordId))
            _robot.Print("*****CSV close record added. Id: {0}, ProfitLoss: {1}", closeRecordId, args.Position.NetProfit);
    }

    private bool IsStrategyPosition(Position position) {
        return position.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(position.Label) &&
               position.Label.StartsWith(_strategyLabelPrefix);
    }

    private string GetPositionCsvId(Position position) {
        return _positionCsvIds.TryGetValue(position.Id, out string csvId) ? csvId : position.Id.ToString();
    }

    private double GetPositionEntryEquity(Position position) {
        return _positionEntryEquities.TryGetValue(position.Id, out double entryEquity) ? entryEquity : 0.0;
    }

    private double GetClosePrice(Position position) {
        HistoricalTrade[] closedTrades = _robot.History.FindByPositionId(position.Id);

        if (closedTrades != null && closedTrades.Length > 0)
            return closedTrades.OrderByDescending(trade => trade.ClosingTime).First().ClosingPrice;

        for (int i = position.Deals.Count - 1; i >= 0; i--) {
            Deal deal = position.Deals[i];

            if (deal.PositionImpact == DealPositionImpact.Closing && deal.ExecutionPrice.HasValue)
                return deal.ExecutionPrice.Value;
        }

        return 0.0;
    }

    private static TradeType ToTradeType(TradeDirectionModel directionModel) {
        return directionModel == TradeDirectionModel.Long ? TradeType.Buy : TradeType.Sell;
    }
}
