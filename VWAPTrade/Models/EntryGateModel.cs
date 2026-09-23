namespace cAlgo.Robots;

// Every gate a Strong signal must pass to become a trade, in the order they are checked.
// None = it passed them all. The bar being Strong at all is not a gate here: a signal only
// exists on a Strong bar (see SignalDetector).
public enum EntryGateModel {
    None,

    // Signal filters, checked by SignalDetector.
    GapMin, // GapX_Selected below GapMin
    SlopeRateMin, // SlopeRateX_Selected below SlopeRateMin
    GapChange, // GapChangeRateX30_Selected outside [Min, Max]
    OppositeDailyVwap, // too many preceding closes on the opposite side of the daily VWAP
    Departure, // price has not yet left the daily VWAP and come back

    // Order gates, checked by OrderExecutor.
    Session, // outside the order window
    Direction, // TradeDirection forbids this side
    OpenPosition, // this level already has an open position
    OrderPlan, // OrderPlanner rejected the sizing, e.g. volume below the broker minimum
    Broker // the broker refused the market order
}
