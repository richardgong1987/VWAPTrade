using System;
using System.IO;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.TradeLog {
    // debug.csv: one row per pattern on the yellow line that did not trade, saying which gate
    // stopped it. These tests pin what a row says and how the file behaves across runs.
    public class BlockedSignalCsvLoggerTests : IDisposable {
        // 2026-09-22 is a Tuesday, 2026-09-21 a Monday (no trading on Mondays).
        private static readonly DateTime TuesdayNoon = new(2026, 9, 22, 12, 0, 0);
        private static readonly DateTime MondayNoon = new(2026, 9, 21, 12, 0, 0);

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "BlockedSignalCsvLoggerTests-" + Guid.NewGuid());

        public void Dispose() {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private static SignalModel ShortEngulfing(EntryGateModel blockedBy, DepartureSettingsModel departure = null) {
            SignalModel signal = TestSignal.Short(entry: 99.0, stopLoss: 101.0);
            signal.Label = "S_Eng_1";
            signal.BarTime = TuesdayNoon.AddMinutes(-5);
            signal.BlockedBy = blockedBy;
            signal.Strong = new VwapStrongReadingModel {
                Close = 99.0, DailyVwap = 100.0, WeeklyVwap = 102.0, Atr14H1 = double.NaN,
                LookbackN = 6, SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };
            signal.Metrics = new VwapStrongMetricsModel { GapXSelected = 0.25 };
            signal.Departure = new DepartureSnapshotModel {
                Settings = departure ?? new DepartureSettingsModel(0.0, 3, 0)
            };
            return signal;
        }

        private static string Cell(SignalModel signal, DateTime decisionTime, string column) {
            string[] names = BlockedSignalCsvLogger.Header.Split(',');
            string[] cells = BlockedSignalCsvLogger.ToCsvLine(signal, decisionTime, "XAUUSD", TestSettings.NoStopOffset()).Split(',');

            return cells[Array.IndexOf(names, column)];
        }

        [Fact]
        public void a_row_has_one_cell_per_header_column() {
            string line = BlockedSignalCsvLogger.ToCsvLine(ShortEngulfing(EntryGateModel.GapMin), TuesdayNoon, "XAUUSD",
                TestSettings.NoStopOffset());

            Assert.Equal(BlockedSignalCsvLogger.Header.Split(',').Length, line.Split(',').Length);
        }

        [Fact]
        public void a_row_names_the_signal_its_side_and_the_gate_that_stopped_it() {
            SignalModel signal = ShortEngulfing(EntryGateModel.GapMin);

            Assert.Equal("S_Eng_1", Cell(signal, TuesdayNoon, "信号"));
            Assert.Equal("空", Cell(signal, TuesdayNoon, "多空"));
            Assert.Equal("间距不足", Cell(signal, TuesdayNoon, "拦截闸门"));
            Assert.Equal("0.25", Cell(signal, TuesdayNoon, "GapX_Selected"));
        }

        [Fact]
        public void the_session_flag_is_judged_at_the_decision_time() {
            SignalModel signal = ShortEngulfing(EntryGateModel.Session);

            Assert.Equal("True", Cell(signal, TuesdayNoon, "在开仓时段"));
            Assert.Equal("False", Cell(signal, MondayNoon, "在开仓时段"));
        }

        [Fact]
        public void a_reading_that_could_not_be_computed_is_blank_not_zero() {
            Assert.Equal("", Cell(ShortEngulfing(EntryGateModel.Stack), TuesdayNoon, "ATR14_H1"));
        }

        [Fact]
        public void a_departure_gate_that_is_off_leaves_its_counters_blank() {
            SignalModel off = ShortEngulfing(EntryGateModel.Stack);
            SignalModel on = ShortEngulfing(EntryGateModel.Departure, new DepartureSettingsModel(0.5, 3, 0));

            Assert.Equal("0", Cell(off, TuesdayNoon, "DepartureMin"));
            Assert.Equal("", Cell(off, TuesdayNoon, "DepartureConfirmCount"));
            Assert.Equal("0", Cell(on, TuesdayNoon, "DepartureConfirmCount"));
        }

        [Fact]
        public void a_reject_reason_with_commas_stays_in_one_cell() {
            SignalModel signal = ShortEngulfing(EntryGateModel.OrderPlan);
            signal.BlockDetail = "Volume=100, Min=1000";

            string line = BlockedSignalCsvLogger.ToCsvLine(signal, TuesdayNoon, "XAUUSD", TestSettings.NoStopOffset());

            Assert.Contains("下单方案被拒,\"Volume=100, Min=1000\"", line);
        }

        [Fact]
        public void the_file_is_named_debug_csv_and_starts_with_the_header() {
            var logger = new BlockedSignalCsvLogger(_directory, resetOnStart: true, "XAUUSD", TestSettings.NoStopOffset());

            Assert.Equal(Path.Combine(_directory, "debug.csv"), logger.FilePath);
            Assert.Equal(new[] { BlockedSignalCsvLogger.Header }, File.ReadAllLines(logger.FilePath));
        }

        [Fact]
        public void without_reset_a_file_with_the_current_header_keeps_its_rows() {
            var first = new BlockedSignalCsvLogger(_directory, resetOnStart: true, "XAUUSD", TestSettings.NoStopOffset());
            first.Append(ShortEngulfing(EntryGateModel.Stack), TuesdayNoon);

            var second = new BlockedSignalCsvLogger(_directory, resetOnStart: false, "XAUUSD", TestSettings.NoStopOffset());
            second.Append(ShortEngulfing(EntryGateModel.Session), MondayNoon);

            Assert.Equal(3, File.ReadAllLines(second.FilePath).Length);
        }

        [Fact]
        public void without_reset_a_file_with_an_old_header_starts_over() {
            Directory.CreateDirectory(_directory);
            File.WriteAllLines(Path.Combine(_directory, "debug.csv"), new[] { "old,header", "1,2" });

            var logger = new BlockedSignalCsvLogger(_directory, resetOnStart: false, "XAUUSD", TestSettings.NoStopOffset());

            Assert.Equal(new[] { BlockedSignalCsvLogger.Header }, File.ReadAllLines(logger.FilePath));
        }
    }
}
