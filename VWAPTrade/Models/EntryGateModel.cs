namespace cAlgo.Robots;

// Every gate a pattern on the daily VWAP must pass to become a trade, in the order they are
// checked. A signal records the first one that stopped it, so debug.csv can say why it never
// traded (see StrongSignalCsvLogger). None = it passed them all.
public enum EntryGateModel {
    None,

    // Signal gates, checked by SignalDetector.
    Stack, // close / daily / weekly are not lined up for this side (the bar is not Strong)
    GapMin, // GapX_Selected below GapMin
    SlopeRateMin, // SlopeRateX_Selected below SlopeRateMin
    GapChange, // GapChangeRateX30_Selected outside [Min, Max]
    Departure, // price has not yet left the daily VWAP and come back

    // Order gates, checked by OrderExecutor.
    Session, // outside the order window
    Direction, // TradeDirection forbids this side
    OpenPosition, // this level already has an open position
    OrderPlan, // OrderPlanner rejected the sizing, e.g. volume below the broker minimum
    Broker // the broker refused the market order
}
