using System;
using System.Collections.Generic;
using System.Globalization;

namespace cAlgo.Robots;

// 交易 CSV 的全部列，一列一行：列名 + 怎么从一条记录取出它的值。
//
// 表头和数据行都由这同一张表生成，所以「表头 31 列、数据行 30 列」这种错位在结构上就不可能
// 发生；加一列也只需要在这里加一行。TradeCsvMigrator 也从这里读列数，决定旧文件要补到几列。
//
// 加列一律往末尾加、不要改已有列名：旧表头是新表头的前缀，迁移只需在右边补空列。
// 改名会打断这个前缀关系，那就得去 TradeCsvMigrator 手工登记一版旧表头（ATR14_H1 → ATR14 就是这么处理的）。
public static class TradeCsvColumns {
    private static readonly IReadOnlyList<TradeCsvColumn> Columns = new[] {
        // ── 交易本身 ──────────────────────────────────────────────────────────
        Text("编号", r => r.Id),
        Text("关键位", r => r.KeyLevel),
        Text("信号", r => r.Signal),
        Text("备注", r => r.Comment),
        Text("交易品种", r => r.Symbol),
        Text("时间周期", r => r.TimeFrame),
        Text("入场时间", r => r.EntryTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
        Raw("入场价格", r => r.EntryPrice),
        Positive("平仓价格", r => r.ClosePrice),
        Raw("止损价格", r => r.StopPrice),
        Raw("止盈价格", r => r.TakeProfitPrice),
        Raw("风险价格距离", r => r.RiskPrice),
        Raw("下单数量", r => r.VolumeInUnits),
        Text("平仓原因", r => r.CloseReason),
        Positive("开仓账户权益", r => r.EntryAccountEquity),
        Positive("平仓账户权益", r => r.CloseAccountEquity),
        Raw("平仓盈亏", r => r.ProfitLoss),
        Text("平仓时间", r => r.CloseTime),
        Text("持仓ID", r => r.PositionId),
        Text("成交ID", r => r.DealId),

        // ── 开仓当时的 VWAP 读数 ─────────────────────────────────────────────
        Text("多空", r => r.Side),
        Reading("DailyVWAP", r => Reading(r)?.DailyVwap),
        Reading("WeeklyVWAP", r => Reading(r)?.WeeklyVwap),
        Reading("ATR14", r => Reading(r)?.SelectedAtr), // 所选周期那一套
        Reading("GapX", r => Metrics(r)?.GapXSelected),
        Reading("SlopeX", r => Metrics(r)?.SlopeRawXSelected), // 原始斜率，未做 30 分钟标准化
        Text("最终结果", r => r.FinalResult),
        Reading("DailyVWAP_Lookback", r => Reading(r)?.DailyVwapBefore),
        Reading("ResultR", r => r.ResultR),
        Reading("GapChangeX", r => Metrics(r)?.GapChangeX), // 旧口径，保留原义
        Reading("WeeklyVWAP_Lookback", r => Reading(r)?.WeeklyVwapBefore),

        // ── 两套 ATR 与归一后的指标 ──────────────────────────────────────────
        BarCount("LookbackN", r => Reading(r)?.LookbackN),
        Reading("ATR14_M5", r => Reading(r)?.Atr14M5),
        Reading("ATR14_H1", r => Reading(r)?.Atr14H1),
        Reading("GapX_M5", r => Metrics(r)?.GapXM5),
        Reading("GapX_H1", r => Metrics(r)?.GapXH1),
        Reading("SlopeRawX_M5", r => Metrics(r)?.SlopeRawXM5),
        Reading("SlopeRawX_H1", r => Metrics(r)?.SlopeRawXH1),
        Reading("SlopeRateX30_M5", r => Metrics(r)?.SlopeRateX30M5),
        Reading("SlopeRateX30_H1", r => Metrics(r)?.SlopeRateX30H1),
        Text("SelectedATRPeriod", r => Reading(r)?.SelectedAtrPeriod.ToString() ?? ""),
        Reading("GapX_Selected", r => Metrics(r)?.GapXSelected),
        Reading("SlopeRateX_Selected", r => Metrics(r)?.SlopeRateXSelected),

        // ── 当时生效的设置与扩口变化 ─────────────────────────────────────────
        Text("TradeDirectionMode", r => r.EntryPlan?.TradeDirectionMode.ToString() ?? ""),
        Text("UseGapChangeFilter", r => Filters(r)?.UseGapChangeFilter.ToString() ?? ""),
        Reading("GapChangeRateMin", r => EnabledRange(r)?.GapChangeRateMin),
        Reading("GapChangeRateMax", r => EnabledRange(r)?.GapChangeRateMax),
        Reading("GapChangeRawPrice", r => Metrics(r)?.GapChangeRawPrice),
        Reading("GapChangeRawX_M5", r => Metrics(r)?.GapChangeRawXM5),
        Reading("GapChangeRawX_H1", r => Metrics(r)?.GapChangeRawXH1),
        Reading("GapChangeRateX30_M5", r => Metrics(r)?.GapChangeRateX30M5),
        Reading("GapChangeRateX30_H1", r => Metrics(r)?.GapChangeRateX30H1),
        Reading("GapChangeRateX30_Selected", r => Metrics(r)?.GapChangeRateX30Selected),

        // ── Departure：先离开日 VWAP，再回踩（V2 第 6 节、第 10.1 节）────────────
        Reading("DepartureX_Selected", r => EnabledDeparture(r)?.DepartureX),
        Reading("DepartureMin", r => Departure(r)?.Settings?.DepartureMin),
        Whole("DepartureConfirmBars", r => EnabledDeparture(r)?.Settings?.ConfirmBars),
        Whole("DepartureConfirmCount", r => EnabledDeparture(r)?.ConfirmCount),
        Text("DepartureConfirmed", r => EnabledDeparture(r)?.IsConfirmed.ToString() ?? ""),
        Whole("DepartureBarsSinceConfirmed", r => EnabledDeparture(r)?.BarsSinceConfirmed),
        Whole("DepartureMaxWaitBars", r => EnabledDeparture(r)?.Settings?.MaxWaitBars)
    };

    public static int Count => Columns.Count;

    public static string Header => Join(column => column.Name);

    public static string ToCsvLine(TradeRecordModel record) => Join(column => column.Read(record));

    private static string Join(Func<TradeCsvColumn, string> select) {
        string[] cells = new string[Columns.Count];

        for (int i = 0; i < Columns.Count; i++) {
            cells[i] = Escape(select(Columns[i]));
        }

        return string.Join(",", cells);
    }

    private static VwapStrongReadingModel Reading(TradeRecordModel record) => record.EntryPlan?.VwapReading;

    private static VwapStrongMetricsModel Metrics(TradeRecordModel record) => record.EntryPlan?.VwapMetrics;

    private static VwapFilterSettingsModel Filters(TradeRecordModel record) => record.EntryPlan?.VwapFilters;

    // 过滤没开时不写 Min/Max —— 那两个数当时根本没参与判断，写出来会被误读成生效过。
    private static VwapFilterSettingsModel EnabledRange(TradeRecordModel record) {
        VwapFilterSettingsModel filters = Filters(record);
        return filters != null && filters.UseGapChangeFilter ? filters : null;
    }

    private static DepartureSnapshotModel Departure(TradeRecordModel record) => record.EntryPlan?.Departure;

    // 同 EnabledRange 的理由：闸门关着时那些状态和根数都没参与判断，只留 DepartureMin —— 它本身
    // 就是开关（0 = 关闭），写出来正好说明这一趟是在关着的口径下跑的。
    private static DepartureSnapshotModel EnabledDeparture(TradeRecordModel record) {
        DepartureSnapshotModel departure = Departure(record);
        return departure != null && departure.IsEnabled ? departure : null;
    }

    // ── 列的三种取值方式 ─────────────────────────────────────────────────────
    private static TradeCsvColumn Text(string name, Func<TradeRecordModel, string> read) =>
        new(name, record => read(record) ?? "");

    // 价格、盈亏这类：原样输出，0 和负数都是有意义的值。
    private static TradeCsvColumn Raw(string name, Func<TradeRecordModel, double> read) =>
        new(name, record => read(record).ToString(CultureInfo.InvariantCulture));

    // 只在开仓行或只在平仓行才有的正数（平仓价、账户权益）：没有就留空。
    private static TradeCsvColumn Positive(string name, Func<TradeRecordModel, double> read) =>
        new(name, record => read(record) is var value && value > 0.0 ? value.ToString(CultureInfo.InvariantCulture) : "");

    // 指标读数：可以是负数（逆向斜率、缩口），所以不能套用「≤0 留空」。
    // 只有取不到值（NaN/无穷/没有快照）才留空 —— 绝不写成 0，0 是一个合法读数。
    private static TradeCsvColumn Reading(string name, Func<TradeRecordModel, double?> read) =>
        new(name, record => FormatReading(read(record)));

    // 回看根数：没有快照的行（例如更早版本留下的持仓）留空，不要写成 0 —— 0 不是合法的 N。
    private static TradeCsvColumn BarCount(string name, Func<TradeRecordModel, int?> read) =>
        new(name, record => read(record) is int value && value >= 1 ? value.ToString(CultureInfo.InvariantCulture) : "");

    // Departure 的计数与根数：0 是合法读数（刚清零、刚确认），所以不能套用 BarCount 的「≥1 才写」。
    // 取不到（闸门关着、或旧版本留下的行）才留空。
    private static TradeCsvColumn Whole(string name, Func<TradeRecordModel, int?> read) =>
        new(name, record => read(record) is int value ? value.ToString(CultureInfo.InvariantCulture) : "");

    private static string FormatReading(double? value) {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return "";

        return value.Value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value) {
        if (string.IsNullOrEmpty(value))
            return "";

        bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");

        return mustQuote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    private sealed class TradeCsvColumn {
        public TradeCsvColumn(string name, Func<TradeRecordModel, string> read) {
            Name = name;
            Read = read;
        }

        public string Name { get; }

        public Func<TradeRecordModel, string> Read { get; }
    }
}
