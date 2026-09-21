using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// 记账：开仓时把这一笔的下单方案、CSV 行号和账户权益留下来，平仓时取回来写平仓行。
//
// 平仓行要复用开仓那一刻的 VWAP 快照（那时的 VWAP 早跑远了），所以这些东西必须留到平仓，
// 而不是用完就丢。仓位一平就清掉，不让字典无限长大。
public class TradeJournal {
    private readonly Robot _robot;
    private readonly TradeCsvLogger _csvLogger;
    private readonly string _symbolName;
    private readonly string _timeFrame;

    private readonly Dictionary<int, OrderPlanModel> _plans = new();
    private readonly Dictionary<int, string> _csvIds = new();
    private readonly Dictionary<int, double> _entryEquities = new();

    public TradeJournal(Robot robot, TradeCsvLogger csvLogger, string symbolName, string timeFrame) {
        _robot = robot;
        _csvLogger = csvLogger;
        _symbolName = symbolName;
        _timeFrame = timeFrame;
    }

    // 保本止损要按开仓时的 R 算触发价，所以把方案摊给它看。
    public IReadOnlyDictionary<int, OrderPlanModel> Plans => _plans;

    public bool RecordEntry(OrderPlanModel planModel, Position position) {
        string csvId = _csvLogger.AppendEntry(planModel, position, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return false;

        _plans[position.Id] = planModel;
        _csvIds[position.Id] = csvId;
        _entryEquities[position.Id] = planModel.AccountEquity;
        _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
        return true;
    }

    public void RecordClose(Position position, PositionCloseReason reason) {
        _plans.TryGetValue(position.Id, out OrderPlanModel entryPlan);

        string closeRecordId = _csvLogger.AppendClose(position, reason, GetCsvId(position), _symbolName, _timeFrame,
            _robot.Server.Time, GetClosePrice(position), GetEntryEquity(position), _robot.Account.Equity, entryPlan);

        Forget(position.Id);

        if (!string.IsNullOrWhiteSpace(closeRecordId))
            _robot.Print("*****CSV close record added. Id: {0}, ProfitLoss: {1}", closeRecordId, position.NetProfit);
    }

    public void Forget(int positionId) {
        _plans.Remove(positionId);
        _csvIds.Remove(positionId);
        _entryEquities.Remove(positionId);
    }

    private string GetCsvId(Position position) {
        return _csvIds.TryGetValue(position.Id, out string csvId) ? csvId : position.Id.ToString();
    }

    private double GetEntryEquity(Position position) {
        return _entryEquities.TryGetValue(position.Id, out double equity) ? equity : 0.0;
    }

    // 成交价优先取历史成交记录；拿不到就退回仓位自己的平仓 deal。
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
}
