using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

// cAlgo.API also has a File type; the alias keeps this file safe if it ever imports cAlgo.API.
using IoFile = System.IO.File;

namespace cAlgo.Robots;

// debug.csv: one row for every Strong signal — a closed bar whose stack points one way
// (VwapStack.ResolveSide on close / daily / weekly) with a pattern for that side touching the
// daily VWAP, the yellow line — whether it became a trade or not. A row that did not trade names
// the first gate that stopped it; every row carries the readings and thresholds the gates
// compared, so traded and untraded setups can be set side by side.
//
// A separate file from the trade CSV, written next to it. No migration: if the columns have
// changed since the file was written, it starts over rather than append rows under a header
// they no longer match.
//
// Pure, no cAlgo dependency, unit tested.
public class StrongSignalCsvLogger {
    public const string FileName = "debug.csv";

    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
    private static readonly Encoding CsvEncoding = new UTF8Encoding(true);

    private static readonly IReadOnlyList<Column> Columns = new Column[] {
        // ── The signal and what became of it ────────────────────────────────
        new("K线时间", r => r.Signal.BarTime.ToString(TimeFormat, CultureInfo.InvariantCulture)),
        // The session check runs at this time (the bar's close), not at the bar's open time.
        new("判定时间", r => r.DecisionTime.ToString(TimeFormat, CultureInfo.InvariantCulture)),
        new("在开仓时段", r => TradingSession.IsInSession(r.DecisionTime).ToString()),
        new("交易品种", r => r.SymbolName),
        new("信号", r => r.Signal.Label),
        new("多空", r => r.Signal.Level?.Side == SignalSideModel.Sell ? "空" : "多"),
        new("下单结果", r => r.Signal.PositionId.HasValue ? "已下单" : "未下单"),
        new("持仓ID", r => r.Signal.PositionId?.ToString(CultureInfo.InvariantCulture) ?? ""),
        new("拦截闸门", r => DescribeGate(r.Signal.BlockedBy)),
        new("拦截详情", r => r.Signal.BlockDetail),

        // ── The bar ──────────────────────────────────────────────────────────
        new("收盘价", r => CsvCell.Number(r.Signal.Close)),
        new("最高价", r => CsvCell.Number(r.Signal.High)),
        new("最低价", r => CsvCell.Number(r.Signal.Low)),
        new("形态止损价", r => CsvCell.Number(r.Signal.StopLoss)),
        new("DailyVWAP", r => CsvCell.Number(r.Signal.Strong?.DailyVwap)),
        new("WeeklyVWAP", r => CsvCell.Number(r.Signal.Strong?.WeeklyVwap)),
        new("DailyVWAP_Lookback", r => CsvCell.Number(r.Signal.Strong?.DailyVwapBefore)),
        new("WeeklyVWAP_Lookback", r => CsvCell.Number(r.Signal.Strong?.WeeklyVwapBefore)),

        // ── Gates 2–4, each reading beside its threshold ────────────────────
        // Thresholds are always written, even when the gate is off: 0 in GapMin is how the row
        // says that gate did not take part.
        new("SelectedATRPeriod", r => r.Signal.Strong?.SelectedAtrPeriod.ToString() ?? ""),
        new("ATR14_M5", r => CsvCell.Number(r.Signal.Strong?.Atr14M5)),
        new("ATR14_H1", r => CsvCell.Number(r.Signal.Strong?.Atr14H1)),
        new("LookbackN", r => r.Signal.Strong?.LookbackN.ToString(CultureInfo.InvariantCulture) ?? ""),
        new("GapX_Selected", r => CsvCell.Number(r.Signal.Metrics?.GapXSelected)),
        new("GapMin", r => CsvCell.Number(r.Settings.VwapFilters.GapMin)),
        new("SlopeRateX_Selected", r => CsvCell.Number(r.Signal.Metrics?.SlopeRateXSelected)),
        new("SlopeRateMin", r => CsvCell.Number(r.Settings.VwapFilters.SlopeRateMin)),
        new("GapChangeRateX30_Selected", r => CsvCell.Number(r.Signal.Metrics?.GapChangeRateX30Selected)),
        new("UseGapChangeFilter", r => r.Settings.VwapFilters.UseGapChangeFilter.ToString()),
        new("GapChangeRateMin", r => CsvCell.Number(r.Settings.VwapFilters.GapChangeRateMin)),
        new("GapChangeRateMax", r => CsvCell.Number(r.Settings.VwapFilters.GapChangeRateMax)),

        // ── Gate 5: Departure ────────────────────────────────────────────────
        // With the gate off the tracker never runs, so its counters would read 0 and look like a
        // real state. They are left blank then; DepartureMin = 0 says the gate was off.
        new("DepartureMin", r => CsvCell.Number(r.Signal.Departure?.Settings?.DepartureMin)),
        new("DepartureX_Selected", r => CsvCell.Number(EnabledDeparture(r)?.DepartureX)),
        new("DepartureConfirmed", r => EnabledDeparture(r)?.IsConfirmed.ToString() ?? ""),
        new("DepartureConfirmCount", r => Whole(EnabledDeparture(r)?.ConfirmCount)),
        new("DepartureConfirmBars", r => Whole(EnabledDeparture(r)?.Settings.ConfirmBars)),
        new("DepartureBarsSinceConfirmed", r => Whole(EnabledDeparture(r)?.BarsSinceConfirmed)),
        new("DepartureMaxWaitBars", r => Whole(EnabledDeparture(r)?.Settings.MaxWaitBars)),

        // ── Order gates ──────────────────────────────────────────────────────
        new("TradeDirectionMode", r => r.Settings.TradeDirectionMode.ToString())
    };

    private readonly string _symbolName;
    private readonly TradeSettingsModel _settings;

    // resetOnStart follows the trade CSV's own switch, so both files cover the same runs.
    public StrongSignalCsvLogger(string directory, bool resetOnStart, string symbolName, TradeSettingsModel settings) {
        FilePath = Path.Combine(directory, FileName);
        _symbolName = symbolName;
        _settings = settings;

        Directory.CreateDirectory(directory);

        if (resetOnStart || !HasCurrentHeader())
            IoFile.WriteAllText(FilePath, Header + Environment.NewLine, CsvEncoding);
    }

    public string FilePath { get; }

    public static string Header => string.Join(",", Columns.Select(column => CsvCell.Escape(column.Name)));

    // decisionTime is the server time the gates ran at, the one the order window is checked against.
    public void Append(SignalModel signal, DateTime decisionTime) {
        IoFile.AppendAllText(FilePath, ToCsvLine(signal, decisionTime, _symbolName, _settings) + Environment.NewLine,
            CsvEncoding);
    }

    public static string ToCsvLine(SignalModel signal, DateTime decisionTime, string symbolName, TradeSettingsModel settings) {
        var row = new Row(signal, decisionTime, symbolName, settings);
        return string.Join(",", Columns.Select(column => CsvCell.Escape(column.Read(row))));
    }

    public static string DescribeGate(EntryGateModel gate) {
        return gate switch {
            EntryGateModel.Stack => "排列不符",
            EntryGateModel.GapMin => "间距不足",
            EntryGateModel.SlopeRateMin => "速度不足",
            EntryGateModel.GapChange => "扩口变化超出区间",
            EntryGateModel.Departure => "未完成离开确认",
            EntryGateModel.Session => "不在开仓时段",
            EntryGateModel.Direction => "交易方向不允许",
            EntryGateModel.OpenPosition => "已有持仓",
            EntryGateModel.OrderPlan => "下单方案被拒",
            EntryGateModel.Broker => "券商拒单",
            _ => ""
        };
    }

    private bool HasCurrentHeader() {
        return IoFile.Exists(FilePath) && IoFile.ReadLines(FilePath, CsvEncoding).FirstOrDefault() == Header;
    }

    private static DepartureSnapshotModel EnabledDeparture(Row row) {
        DepartureSnapshotModel departure = row.Signal.Departure;
        return departure != null && departure.IsEnabled ? departure : null;
    }

    private static string Whole(int? value) {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    private sealed record Row(SignalModel Signal, DateTime DecisionTime, string SymbolName, TradeSettingsModel Settings);

    private sealed record Column(string Name, Func<Row, string> Read);
}
