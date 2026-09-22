namespace cAlgo.Robots;

// The Departure state as it stood on the signal bar, carried into the trade CSV so a row says
// how far price had left the daily VWAP and how long it had been waiting to come back.
//
// A copy of the values, not a view of the tracker: the departure is cleared right after the fill
// (V2 section 6.3), and the close row written minutes later must still report the entry's state.
public class DepartureSnapshotModel {
    // The thresholds this run used. Null only on rows written by an older build.
    public DepartureSettingsModel Settings { get; set; }

    public bool IsEnabled => Settings != null && Settings.IsEnabled;

    // direction × (close − 日VWAP) / SelectedATR on the signal bar. On an entry bar this is the
    // pullback distance, so a small or negative value is expected here — the departure that
    // qualified the trade happened earlier and is recorded by IsConfirmed.
    public double DepartureX { get; set; } = double.NaN;

    public bool IsConfirmed { get; set; }

    public int ConfirmCount { get; set; }

    public int BarsSinceConfirmed { get; set; }
}
