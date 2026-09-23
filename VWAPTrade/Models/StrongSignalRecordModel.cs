using System;

namespace cAlgo.Robots;

// One row of debug.csv: a Strong signal, what became of it, and the settings it was judged by.
// StrongSignalCsvColumns turns it into text.
public class StrongSignalRecordModel {
    public SignalModel Signal { get; set; }

    public EntryOutcomeModel Outcome { get; set; }

    // The server time the gates ran at: the signal bar's close, when the order window is checked.
    public DateTime DecisionTime { get; set; }

    public string Symbol { get; set; } = "";

    public TradeSettingsModel Settings { get; set; }
}
