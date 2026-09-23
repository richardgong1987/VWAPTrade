using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// 把一个信号变成一张真实订单，或者说清楚为什么没下：
//
//   信号过滤 → 交易时段 → 方向许可 → 该关键位是否已有持仓 → 定价定量 → 市价单 → 交给 TradeJournal 记账
//
// 定价定量在 OrderPlanner，记账在 TradeJournal，持仓期间的保本止损在 BreakevenProtector。
public class OrderExecutor {
    private const string EntryComment = "ENTRY";

    private readonly Robot _robot;
    private readonly string _symbolName;

    // 订单标签是 "{OrderLabel}_{关键位名}"，例如 "VWAPTrade-label_VWAP"。关键位后缀让每一档
    // 各自独立持仓；前缀用来认出本实例的单子，手工单和同品种上的别的 cBot 都会被忽略。
    private readonly string _strategyLabelPrefix;

    private readonly OrderPlanner _planner;
    private readonly TradeSettingsModel _settings;
    private readonly TradeJournal _journal;
    private readonly BreakevenProtector _breakeven;

    public OrderExecutor(Robot robot, string symbolName, string orderLabel, OrderPlanner planner,
        TradeSettingsModel settings, TradeJournal journal, BreakevenProtector breakeven) {
        _robot = robot;
        _symbolName = symbolName;
        _strategyLabelPrefix = orderLabel + "_";
        _planner = planner;
        _settings = settings;
        _journal = journal;
        _breakeven = breakeven;

        _robot.Positions.Closed += OnPositionClosed;
    }

    public void ManageOpenPositions() {
        _breakeven.Protect(_robot.Positions.Where(IsStrategyPosition), _journal.Plans);
    }

    public EntryOutcomeModel TryEnter(SignalModel signalModel) {
        if (signalModel.FailedFilter != EntryGateModel.None)
            return EntryOutcomeModel.Blocked(signalModel.FailedFilter);

        // 只在交易时段里开新单（周二~周五 10:30~次日 06:00，见 TradingSession）。
        // 已经持有的仓位不受影响，照常由止损/止盈了结。
        if (!TradingSession.IsInSession(_robot.Server.Time)) {
            _robot.Print("*****Order skipped | Outside the trading session. Time: {0}", _robot.Server.Time);
            return EntryOutcomeModel.Blocked(EntryGateModel.Session);
        }

        // 方向许可：下单前的最后一道闸门。放在算仓位之前，不给一个注定要拒的信号做定价。
        if (!TradeDirectionGate.IsAllowed(_settings.TradeDirectionMode, signalModel.Level.Side)) {
            _robot.Print("*****Order skipped | TradeDirection {0} blocks a {1} signal.", _settings.TradeDirectionMode,
                signalModel.Level.Side);
            return EntryOutcomeModel.Blocked(EntryGateModel.Direction);
        }

        string label = _strategyLabelPrefix + signalModel.Level.Name;

        if (HasPositionForLevel(label)) {
            _robot.Print("*****Order skipped | Level {0} already has an open position on symbol: {1}", signalModel.Level.Name,
                _symbolName);
            return EntryOutcomeModel.Blocked(EntryGateModel.OpenPosition);
        }

        OrderPlanModel planModel = _planner.CreatePlan(signalModel, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Level: {0}, Reason: {1}", signalModel.Level.Name, planModel.RejectReason);
            return EntryOutcomeModel.Blocked(EntryGateModel.OrderPlan, planModel.RejectReason);
        }

        planModel.Label = label;
        return PlaceOrder(planModel);
    }

    // 按这一档自己的标签判断，其他档照样可以开自己的仓。查的是券商的实时持仓而不是内存里的表，
    // 这样重启之后也不会重复开一单。
    private bool HasPositionForLevel(string label) {
        return _robot.Positions.Any(position => position.SymbolName == _symbolName && position.Label == label);
    }

    private EntryOutcomeModel PlaceOrder(OrderPlanModel planModel) {
        _robot.Print(
            "*****Order plan | Level: {0}, Side: {1}, Entry: {2}, Stop: {3}, TakeProfit: {4}, RiskPrice: {5}, StopLossPips: {6}, RiskMoney: {7}, EstimatedRiskMoney: {8}, Lots: {9}, VolumeUnits: {10}",
            planModel.Signal.Level.Name, planModel.DirectionModel, planModel.EntryPrice, planModel.StopPrice,
            planModel.TakeProfitPrice, planModel.RiskPrice, planModel.StopLossPips, planModel.RiskMoney,
            planModel.EstimatedRiskMoney, planModel.Lots, planModel.VolumeInUnits);

        TradeResult result = _robot.ExecuteMarketOrder(ToTradeType(planModel.DirectionModel), _symbolName, planModel.VolumeInUnits,
            planModel.Label, planModel.StopLossPips, planModel.TakeProfitPips, EntryComment);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return EntryOutcomeModel.Blocked(EntryGateModel.Broker, result.Error.ToString());
        }

        _robot.Print("*****Order submitted | Label: {0}", planModel.Label);
        _journal.RecordEntry(planModel, result.Position);
        return EntryOutcomeModel.Ordered(result.Position.Id);
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args?.Position == null || !IsStrategyPosition(args.Position))
            return;

        _breakeven.Forget(args.Position.Id);
        _journal.RecordClose(args.Position, args.Reason);
    }

    private bool IsStrategyPosition(Position position) {
        return position.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(position.Label) &&
               position.Label.StartsWith(_strategyLabelPrefix);
    }

    private static TradeType ToTradeType(TradeDirectionModel directionModel) {
        return directionModel == TradeDirectionModel.Long ? TradeType.Buy : TradeType.Sell;
    }
}
