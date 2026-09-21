using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// 浮盈达到 BreakevenTriggerR 个 R 之后，把止损推到保本位。
//
// 止盈是开仓时定死的 TakeProfitR×R，已经挂在订单上由券商执行，这里不需要盯；持仓期间
// 要做的只有这一件事，所以单独一个类。触发价按开仓时那一份方案里的 R（止损距离）算。
public class BreakevenProtector {
    private readonly Robot _robot;
    private readonly ISymbolModel _symbolModel;
    private readonly TradeSettingsModel _settings;

    // 每个仓位只推一次，推过就记下来。
    private readonly HashSet<int> _protected = new();

    public BreakevenProtector(Robot robot, ISymbolModel symbolModel, TradeSettingsModel settings) {
        _robot = robot;
        _symbolModel = symbolModel;
        _settings = settings;
    }

    public void Protect(IEnumerable<Position> positions, IReadOnlyDictionary<int, OrderPlanModel> plans) {
        // 0 = 关闭。必须在这里挡掉：触发距离为 0 会让保护在开仓瞬间就「触发」，然后因为保本价
        // 落在市价另一侧而被跳过，机会白白消耗掉 —— 看着像没保护，实则是行情决定的哑火。
        if (_settings.BreakevenTriggerR <= 0.0)
            return;

        foreach (Position position in positions.ToArray()) {
            Protect(position, plans);
        }
    }

    public void Forget(int positionId) {
        _protected.Remove(positionId);
    }

    private void Protect(Position position, IReadOnlyDictionary<int, OrderPlanModel> plans) {
        if (_protected.Contains(position.Id))
            return;

        if (!plans.TryGetValue(position.Id, out OrderPlanModel plan) || plan.RiskPrice <= 0.0)
            return;

        bool isLong = position.TradeType == TradeType.Buy;
        double trigger = isLong
            ? position.EntryPrice + _settings.BreakevenTriggerR * plan.RiskPrice
            : position.EntryPrice - _settings.BreakevenTriggerR * plan.RiskPrice;

        if (isLong ? position.CurrentPrice < trigger : position.CurrentPrice > trigger)
            return;

        MoveStop(position, isLong);
    }

    // 仓位不能再跌回亏损，所以止损挪到开仓价顺盈利方向偏移一点点的位置。
    private void MoveStop(Position position, bool isLong) {
        // 先记账再动手：券商拒单时也不要每个 tick 重试一次，日志里会留下失败原因。
        if (!_protected.Add(position.Id))
            return;

        double offset = _symbolModel.TickSize * _settings.BreakevenOffsetTicks;
        double protectiveStop = isLong ? position.EntryPrice + offset : position.EntryPrice - offset;

        // 止损不能落在市价的另一侧：券商会拒单，或者直接把仓位按市价平掉。
        if (isLong ? protectiveStop >= position.CurrentPrice : protectiveStop <= position.CurrentPrice) {
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
}
