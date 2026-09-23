namespace cAlgo.Robots;

// Settings for the side-aware recovery filter. A positive BlockCount turns it on: among the
// preceding LookbackBars closes in the same VWAP day, BlockCount or more on the opposite side
// of the daily VWAP reject the signal (below for Buy, above for Sell).
public class OppositeDailyVwapSettingsModel {
    public OppositeDailyVwapSettingsModel(int lookbackBars, int blockCount) {
        LookbackBars = lookbackBars;
        BlockCount = blockCount;
    }

    public int LookbackBars { get; }

    // 0 switches the whole gate off, preserving the behaviour of a build without it.
    public int BlockCount { get; }

    public bool IsEnabled => BlockCount > 0;
}
