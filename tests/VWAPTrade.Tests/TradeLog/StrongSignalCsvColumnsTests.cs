using System;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.TradeLog {
    // What a debug.csv row says: one row per Strong signal on the yellow line, traded or not; an
    // untraded row names the gate that stopped it.
    public class StrongSignalCsvColumnsTests {
        // 2026-09-22 is a Tuesday, 2026-09-21 a Monday (no trading on Mondays).
        private static readonly DateTime TuesdayNoon = new(2026, 9, 22, 12, 0, 0);
        private static readonly DateTime MondayNoon = new(2026, 9, 21, 12, 0, 0);

        private static readonly EntryOutcomeModel NotOrdered = EntryOutcomeModel.Blocked(EntryGateModel.GapMin);

        private static SignalModel ShortEngulfing(DepartureSettingsModel departure = null) {
            SignalModel signal = TestSignal.Short(entry: 99.0, stopLoss: 101.0);
            signal.Label = "S_Eng_1";
            signal.BarTime = TuesdayNoon.AddMinutes(-5);
            signal.Strong = new VwapStrongReadingModel {
                Close = 99.0, DailyVwap = 100.0, WeeklyVwap = 102.0, Atr14H1 = double.NaN,
                LookbackN = 6, SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };
            signal.Metrics = new VwapStrongMetricsModel { GapXSelected = 0.25 };
            signal.Departure = new DepartureSnapshotModel { Settings = departure ?? new DepartureSettingsModel(0.0, 3, 0) };
            return signal;
        }

        private static string Line(SignalModel signal, EntryOutcomeModel outcome, DateTime? decisionTime = null,
            TradeSettingsModel settings = null) =>
            StrongSignalCsvColumns.ToCsvLine(new StrongSignalRecordModel {
                Signal = signal, Outcome = outcome, DecisionTime = decisionTime ?? TuesdayNoon, Symbol = "XAUUSD",
                Settings = settings ?? TestSettings.NoStopOffset()
            });

        private static string Cell(string line, string column) =>
            line.Split(',')[Array.IndexOf(StrongSignalCsvColumns.Header.Split(','), column)];

        [Fact]
        public void a_row_has_one_cell_per_header_column() {
            Assert.Equal(StrongSignalCsvColumns.Header.Split(',').Length, Line(ShortEngulfing(), NotOrdered).Split(',').Length);
        }

        [Fact]
        public void a_row_names_the_signal_its_side_and_the_gate_that_stopped_it() {
            string line = Line(ShortEngulfing(), NotOrdered);

            Assert.Equal("S_Eng_1", Cell(line, "信号"));
            Assert.Equal("空", Cell(line, "多空"));
            Assert.Equal("间距不足", Cell(line, "拦截闸门"));
            Assert.Equal("0.25", Cell(line, "GapX_Selected"));
        }

        [Fact]
        public void a_blocked_signal_is_marked_not_ordered_and_has_no_position() {
            string line = Line(ShortEngulfing(), EntryOutcomeModel.Blocked(EntryGateModel.Departure));

            Assert.Equal("未下单", Cell(line, "下单结果"));
            Assert.Equal("", Cell(line, "持仓ID"));
            Assert.Equal("未完成离开确认", Cell(line, "拦截闸门"));
        }

        [Fact]
        public void the_opposite_side_daily_vwap_gate_has_a_clear_debug_name() {
            Assert.Equal("黄线反向侧K线过多", StrongSignalCsvColumns.DescribeGate(EntryGateModel.OppositeDailyVwap));
        }

        [Fact]
        public void a_slope_efficiency_block_is_named_and_its_reading_sits_beside_the_threshold() {
            SignalModel signal = ShortEngulfing();
            signal.Metrics.SlopeEfficiency = 0.008675;
            var filters = new VwapFilterSettingsModel(gapMin: 0.0, slopeRateMin: 0.0, useGapChangeFilter: false,
                gapChangeRateMin: 0.0, gapChangeRateMax: 0.0, slopeEfficiencyMin: 0.01, expansionEfficiencyMin: 0.0);

            string line = Line(signal, EntryOutcomeModel.Blocked(EntryGateModel.SlopeEfficiency),
                settings: TestSettings.Create(vwapFilters: filters));

            Assert.Equal("斜率效率不足", Cell(line, "拦截闸门"));
            Assert.Equal("0.008675", Cell(line, "SlopeEfficiency"));
            Assert.Equal("0.01", Cell(line, "SlopeEfficiencyMin"));
        }

        [Fact]
        public void an_expansion_efficiency_block_is_named_and_its_reading_sits_beside_the_threshold() {
            SignalModel signal = ShortEngulfing();
            signal.Metrics.ExpansionEfficiency = 0.004;
            var filters = new VwapFilterSettingsModel(gapMin: 0.0, slopeRateMin: 0.0, useGapChangeFilter: false,
                gapChangeRateMin: 0.0, gapChangeRateMax: 0.0, slopeEfficiencyMin: 0.0, expansionEfficiencyMin: 0.0045);

            string line = Line(signal, EntryOutcomeModel.Blocked(EntryGateModel.ExpansionEfficiency),
                settings: TestSettings.Create(vwapFilters: filters));

            Assert.Equal("扩口效率不足", Cell(line, "拦截闸门"));
            Assert.Equal("0.004", Cell(line, "ExpansionEfficiency"));
            Assert.Equal("0.0045", Cell(line, "ExpansionEfficiencyMin"));
        }

        [Fact]
        public void an_efficiency_filter_that_is_off_still_writes_its_zero_threshold() {
            // 0 in the threshold is how the row says the filter did not take part.
            string line = Line(ShortEngulfing(), NotOrdered);

            Assert.Equal("0", Cell(line, "SlopeEfficiencyMin"));
            Assert.Equal("0", Cell(line, "ExpansionEfficiencyMin"));
        }

        [Fact]
        public void a_traded_signal_is_recorded_with_its_position_and_no_gate() {
            string line = Line(ShortEngulfing(), EntryOutcomeModel.Ordered(12345));

            Assert.Equal("已下单", Cell(line, "下单结果"));
            Assert.Equal("12345", Cell(line, "持仓ID"));
            Assert.Equal("", Cell(line, "拦截闸门"));
        }

        [Fact]
        public void the_session_flag_is_judged_at_the_decision_time() {
            EntryOutcomeModel outsideSession = EntryOutcomeModel.Blocked(EntryGateModel.Session);

            Assert.Equal("True", Cell(Line(ShortEngulfing(), outsideSession, TuesdayNoon), "在开仓时段"));
            Assert.Equal("False", Cell(Line(ShortEngulfing(), outsideSession, MondayNoon), "在开仓时段"));
        }

        [Fact]
        public void a_reading_that_could_not_be_computed_is_blank_not_zero() {
            Assert.Equal("", Cell(Line(ShortEngulfing(), NotOrdered), "ATR14_H1"));
        }

        [Fact]
        public void a_departure_gate_that_is_off_leaves_its_counters_blank() {
            string off = Line(ShortEngulfing(), NotOrdered);
            string on = Line(ShortEngulfing(new DepartureSettingsModel(0.5, 3, 0)), NotOrdered);

            Assert.Equal("0", Cell(off, "DepartureMin"));
            Assert.Equal("", Cell(off, "DepartureConfirmCount"));
            Assert.Equal("0", Cell(on, "DepartureConfirmCount"));
        }

        [Fact]
        public void a_reject_reason_with_commas_stays_in_one_cell() {
            string line = Line(ShortEngulfing(), EntryOutcomeModel.Blocked(EntryGateModel.OrderPlan, "Volume=100, Min=1000"));

            Assert.Contains("下单方案被拒,\"Volume=100, Min=1000\"", line);
        }
    }
}
