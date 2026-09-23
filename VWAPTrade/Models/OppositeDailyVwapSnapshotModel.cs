namespace cAlgo.Robots;

// What the opposite-side recovery gate saw for one signal. It travels with SignalModel to debug.csv.
public class OppositeDailyVwapSnapshotModel {
    public OppositeDailyVwapSettingsModel Settings { get; set; }

    public int CheckedBars { get; set; }

    public int OppositeSideCount { get; set; }

    public bool IsBlocked { get; set; }
}
