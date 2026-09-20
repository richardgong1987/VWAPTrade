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

    // Orders are labelled "{OrderLabel}_{level name}", e.g. "VWAPTrade-label_Short1". The level
    // suffix is what keeps the six levels independent — each one gates on its own label, so several
    // can hold a position at the same time. The prefix marks this instance's orders, so manual
    // trades and other bots on the same symbol are ignored.
    private readonly string _strategyLabelPrefix;

    private readonly OrderPlanner _planner;
    private readonly RiskGuard _riskGuard;
    private readonly TradeCsvLogger _csvLogger;

    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, double> _positionEntryEquities = new();

    public OrderExecutor(Robot robot, string symbolName, string timeFrame, string orderLabel, OrderPlanner planner,
        RiskGuard riskGuard, TradeCsvLogger csvLogger) {
        _robot = robot;
        _symbolName = symbolName;
        _timeFrame = timeFrame;
        _strategyLabelPrefix = orderLabel + "_";
        _planner = planner;
        _riskGuard = riskGuard;
        _csvLogger = csvLogger;

        _robot.Positions.Closed += OnPositionClosed;
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

        return ExecutePlan(planModel);
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
        _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
        return true;
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args?.Position == null || !IsStrategyPosition(args.Position))
            return;

        string csvId = GetPositionCsvId(args.Position);
        double entryEquity = GetPositionEntryEquity(args.Position);
        double closePrice = GetClosePrice(args.Position);
        string closeRecordId = _csvLogger.AppendClose(args.Position, args.Reason, csvId, _symbolName, _timeFrame, _robot.Server.Time,
            closePrice, entryEquity, _robot.Account.Equity);

        _positionCsvIds.Remove(args.Position.Id);
        _positionEntryEquities.Remove(args.Position.Id);

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
