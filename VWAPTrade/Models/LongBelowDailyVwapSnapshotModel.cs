namespace cAlgo.Robots;

// What the long recovery gate saw for one signal. It travels with SignalModel to debug.csv.
public class LongBelowDailyVwapSnapshotModel {
    public LongBelowDailyVwapSettingsModel Settings { get; set; }

    public int CheckedBars { get; set; }

    public int BelowCount { get; set; }

    public bool IsBlocked { get; set; }
}
