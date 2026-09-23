namespace cAlgo.Robots;

// Settings for the long-only recovery filter. A positive BlockCount turns it on:
// among the preceding LookbackBars closes in the same VWAP day, BlockCount or more
// closes below the daily VWAP reject a new long signal.
public class LongBelowDailyVwapSettingsModel {
    public LongBelowDailyVwapSettingsModel(int lookbackBars, int blockCount) {
        LookbackBars = lookbackBars;
        BlockCount = blockCount;
    }

    public int LookbackBars { get; }

    // 0 switches the whole gate off, preserving the behaviour of a build without it.
    public int BlockCount { get; }

    public bool IsEnabled => BlockCount > 0;
}
