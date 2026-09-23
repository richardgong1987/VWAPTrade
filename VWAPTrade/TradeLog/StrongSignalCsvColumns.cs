using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace cAlgo.Robots;

// Every column of debug.csv, one per line: its name and how to read it from a record. The header
// and every row come from this one list, so they cannot drift apart. The file itself is
// StrongSignalCsvLogger's job.
//
// Pure, no cAlgo dependency, unit tested.
public static class StrongSignalCsvColumns {
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly IReadOnlyList<Column> Columns = new Column[] {
        // ── The signal and what became of it ────────────────────────────────
        new("K线时间", r => r.Signal.BarTime.ToString(TimeFormat, CultureInfo.InvariantCulture)),
        // The session check runs at this time (the bar's close), not at the bar's open time.
        new("判定时间", r => r.DecisionTime.ToString(TimeFormat, CultureInfo.InvariantCulture)),
        new("在开仓时段", r => TradingSession.IsInSession(r.DecisionTime).ToString()),
        new("交易品种", r => r.Symbol),
        new("信号", r => r.Signal.Label),
        new("多空", r => r.Signal.Level.Side == SignalSideModel.Sell ? "空" : "多"),
        new("下单结果", r => r.Outcome.IsOrdered ? "已下单" : "未下单"),
        new("持仓ID", r => r.Outcome.PositionId?.ToString(CultureInfo.InvariantCulture) ?? ""),
        new("拦截闸门", r => DescribeGate(r.Outcome.BlockedBy)),
        new("拦截详情", r => r.Outcome.Detail),

        // ── The bar ──────────────────────────────────────────────────────────
        new("收盘价", r => CsvCell.Number(r.Signal.Close)),
        new("最高价", r => CsvCell.Number(r.Signal.High)),
        new("最低价", r => CsvCell.Number(r.Signal.Low)),
        new("形态止损价", r => CsvCell.Number(r.Signal.StopLoss)),
        new("DailyVWAP", r => CsvCell.Number(r.Signal.Strong?.DailyVwap)),
        new("WeeklyVWAP", r => CsvCell.Number(r.Signal.Strong?.WeeklyVwap)),
        new("DailyVWAP_Lookback", r => CsvCell.Number(r.Signal.Strong?.DailyVwapBefore)),
        new("WeeklyVWAP_Lookback", r => CsvCell.Number(r.Signal.Strong?.WeeklyVwapBefore)),

        // ── Filters 2–4, each reading beside its threshold ──────────────────
        // Thresholds are always written, even when the filter is off: 0 in GapMin is how the row
        // says that filter did not take part.
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

        // ── Filter 5: Departure ──────────────────────────────────────────────
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

    public static string Header => string.Join(",", Columns.Select(column => CsvCell.Escape(column.Name)));

    public static string ToCsvLine(StrongSignalRecordModel record) =>
        string.Join(",", Columns.Select(column => CsvCell.Escape(column.Read(record))));

    // The 拦截闸门 text, in the words the strategy notes use.
    public static string DescribeGate(EntryGateModel gate) {
        return gate switch {
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

    private static DepartureSnapshotModel EnabledDeparture(StrongSignalRecordModel record) {
        DepartureSnapshotModel departure = record.Signal.Departure;
        return departure != null && departure.IsEnabled ? departure : null;
    }

    private static string Whole(int? value) {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "";
    }

    private sealed record Column(string Name, Func<StrongSignalRecordModel, string> Read);
}
