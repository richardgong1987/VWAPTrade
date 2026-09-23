namespace cAlgo.Robots;

// One already-closed bar used by the opposite-side recovery filter. Keeping this independent from
// cAlgo lets OppositeDailyVwapGate be unit tested.
public class RecentDailyVwapBarModel {
    public RecentDailyVwapBarModel(double close, double dailyVwap) {
        Close = close;
        DailyVwap = dailyVwap;
    }

    public double Close { get; }

    public double DailyVwap { get; }
}
