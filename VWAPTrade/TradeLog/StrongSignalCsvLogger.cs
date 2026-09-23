using System;
using System.IO;
using System.Linq;
using System.Text;

// cAlgo.API also has a File type; the alias keeps this file safe if it ever imports cAlgo.API.
using IoFile = System.IO.File;

namespace cAlgo.Robots;

// debug.csv: one row for every Strong signal — a closed bar whose stack points one way
// (VwapStack.ResolveSide) with a pattern for that side touching the daily VWAP, the yellow line —
// whether it became a trade or not, so traded and untraded setups can be set side by side.
//
// This class owns the file: where it is, its header, appending a row. What a row says is
// StrongSignalCsvColumns. A separate file from the trade CSV, written next to it. No migration:
// if the columns have changed since the file was written, it starts over rather than append rows
// under a header they no longer match.
//
// Pure, no cAlgo dependency, unit tested.
public class StrongSignalCsvLogger {
    public const string FileName = "debug.csv";

    private static readonly Encoding CsvEncoding = new UTF8Encoding(true);

    private readonly string _symbol;
    private readonly TradeSettingsModel _settings;

    // resetOnStart follows the trade CSV's own switch, so both files cover the same runs.
    public StrongSignalCsvLogger(string directory, bool resetOnStart, string symbol, TradeSettingsModel settings) {
        FilePath = Path.Combine(directory, FileName);
        _symbol = symbol;
        _settings = settings;

        Directory.CreateDirectory(directory);

        if (resetOnStart || !HasCurrentHeader())
            IoFile.WriteAllText(FilePath, StrongSignalCsvColumns.Header + Environment.NewLine, CsvEncoding);
    }

    public string FilePath { get; }

    // decisionTime is the server time the gates ran at, the one the order window is checked against.
    public void Append(SignalModel signal, EntryOutcomeModel outcome, DateTime decisionTime) {
        var record = new StrongSignalRecordModel {
            Signal = signal, Outcome = outcome, DecisionTime = decisionTime, Symbol = _symbol, Settings = _settings
        };

        IoFile.AppendAllText(FilePath, StrongSignalCsvColumns.ToCsvLine(record) + Environment.NewLine, CsvEncoding);
    }

    private bool HasCurrentHeader() {
        return IoFile.Exists(FilePath) && IoFile.ReadLines(FilePath, CsvEncoding).FirstOrDefault() == StrongSignalCsvColumns.Header;
    }
}
