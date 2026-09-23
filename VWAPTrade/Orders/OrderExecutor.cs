using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// 把一个信号变成一张真实订单。这里只做「放不放行、下不下单」：
//
//   交易时段 → 方向许可 → 该关键位是否已有持仓 → 定价定量 → 市价单 → 交给 TradeJournal 记账
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

    public void Stop() {
        _robot.Positions.Closed -= OnPositionClosed;
    }

    public void ManageOpenPositions() {
        _breakeven.Protect(_robot.Positions.Where(IsStrategyPosition), _journal.Plans);
    }

    public bool ExecuteIfSignal(SignalModel signalModel) {
        if (signalModel?.Level == null)
            return false;

        // 只在交易时段里开新单（周二~周五 10:30~次日 06:00，见 TradingSession）。
        // 已经持有的仓位不受影响，照常由止损/止盈了结。
        if (!TradingSession.IsInSession(_robot.Server.Time)) {
            _robot.Print("*****Order skipped | Outside the trading session. Time: {0}", _robot.Server.Time);
            return Block(signalModel, EntryGateModel.Session);
        }

        // 方向许可：下单前的最后一道闸门。放在算仓位之前，不给一个注定要拒的信号做定价。
        if (!TradeDirectionGate.IsAllowed(_settings.TradeDirectionMode, signalModel.Level.Side)) {
            _robot.Print("*****Order skipped | TradeDirection {0} blocks a {1} signal.", _settings.TradeDirectionMode,
                signalModel.Level.Side);
            return Block(signalModel, EntryGateModel.Direction);
        }

        string label = _strategyLabelPrefix + signalModel.Level.Name;

        if (HasPositionForLevel(label)) {
            _robot.Print("*****Order skipped | Level {0} already has an open position on symbol: {1}", signalModel.Level.Name,
                _symbolName);
            return Block(signalModel, EntryGateModel.OpenPosition);
        }

        OrderPlanModel planModel = _planner.CreatePlan(signalModel, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Level: {0}, Reason: {1}", signalModel.Level.Name, planModel.RejectReason);
            return Block(signalModel, EntryGateModel.OrderPlan, planModel.RejectReason);
        }

        FillOrderContext(planModel, signalModel, label);
        return ExecutePlan(planModel, signalModel);
    }

    // Records the gate on the signal so debug.csv can say why it never became a trade.
    private static bool Block(SignalModel signalModel, EntryGateModel gate, string detail = "") {
        signalModel.BlockedBy = gate;
        signalModel.BlockDetail = detail ?? "";
        return false;
    }

    // 方案本身只有价格和数量；这里补上它属于哪个信号、以及当时生效的设置，好一路带进 CSV。
    private void FillOrderContext(OrderPlanModel planModel, SignalModel signalModel, string label) {
        planModel.Label = label;
        planModel.SignalName = signalModel.Label;
        planModel.KeyLevel = signalModel.Level.Name;
        planModel.VwapReading = signalModel.Strong;
        planModel.VwapMetrics = signalModel.Metrics;
        planModel.VwapFilters = _settings.VwapFilters;
        planModel.TradeDirectionMode = _settings.TradeDirectionMode;
        planModel.Departure = signalModel.Departure;
    }

    // 按这一档自己的标签判断，其他档照样可以开自己的仓。查的是券商的实时持仓而不是内存里的表，
    // 这样重启之后也不会重复开一单。
    private bool HasPositionForLevel(string label) {
        return _robot.Positions.Any(position => position.SymbolName == _symbolName && position.Label == label);
    }

    private bool ExecutePlan(OrderPlanModel planModel, SignalModel signalModel) {
        _robot.Print(
            "*****Order plan | Level: {0}, Side: {1}, Entry: {2}, Stop: {3}, TakeProfit: {4}, RiskPrice: {5}, StopLossPips: {6}, RiskMoney: {7}, EstimatedRiskMoney: {8}, Lots: {9}, VolumeUnits: {10}",
            planModel.KeyLevel, planModel.DirectionModel, planModel.EntryPrice, planModel.StopPrice, planModel.TakeProfitPrice,
            planModel.RiskPrice, planModel.StopLossPips, planModel.RiskMoney, planModel.EstimatedRiskMoney, planModel.Lots,
            planModel.VolumeInUnits);

        TradeResult result = _robot.ExecuteMarketOrder(ToTradeType(planModel.DirectionModel), _symbolName, planModel.VolumeInUnits,
            planModel.Label, planModel.StopLossPips, planModel.TakeProfitPips, EntryComment);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return Block(signalModel, EntryGateModel.Broker, result.Error.ToString());
        }

        _robot.Print("*****Order submitted | Label: {0}", planModel.Label);
        signalModel.PositionId = result.Position.Id;
        return _journal.RecordEntry(planModel, result.Position);
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
