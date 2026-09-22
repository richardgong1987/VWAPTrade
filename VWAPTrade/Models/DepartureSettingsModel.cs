namespace cAlgo.Robots;

// Thresholds for the Departure gate (V2 section 6.1). Plain data; DepartureTracker applies them.
public class DepartureSettingsModel {
    public DepartureSettingsModel(double departureMin, int confirmBars, int maxWaitBars) {
        DepartureMin = departureMin;
        ConfirmBars = confirmBars;
        MaxWaitBars = maxWaitBars;
    }

    // Minimum distance from the daily VWAP, in ATR multiples. 0 switches the whole gate off,
    // which must leave results identical to a build without it.
    public double DepartureMin { get; }

    // How many consecutive M5 closes must clear DepartureMin before the departure counts.
    public int ConfirmBars { get; }

    // How many bars the confirmed departure stays valid while waiting for the pullback.
    // 0 = no limit: it waits until an entry, a new daily period, or the structure breaking.
    public int MaxWaitBars { get; }

    public bool IsEnabled => DepartureMin > 0.0;
}
