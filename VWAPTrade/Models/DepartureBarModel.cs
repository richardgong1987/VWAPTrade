namespace cAlgo.Robots;

// One closed bar as the Departure state machine sees it: the close against the daily VWAP,
// the weekly VWAP that fixes the structure direction, and the ATR the distance is measured in.
// Plain data, no cAlgo dependency.
public class DepartureBarModel {
    public double Close { get; set; }

    public double DailyVwap { get; set; }

    public double WeeklyVwap { get; set; }

    // The ATR14 picked by the "ATR归一周期" parameter — the same denominator the Gap and Slope
    // gates divide by. NaN when it is not available yet.
    public double SelectedAtr { get; set; }

    // First bar of a new daily VWAP period (06:00). The daily VWAP restarts here, so whatever
    // departure was confirmed against the previous day's line no longer applies.
    public bool IsDayPeriodStart { get; set; }
}
