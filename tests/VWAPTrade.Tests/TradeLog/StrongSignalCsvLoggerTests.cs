using System;
using System.IO;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.TradeLog {
    // How debug.csv behaves as a file across runs. What a row says is StrongSignalCsvColumnsTests.
    public class StrongSignalCsvLoggerTests : IDisposable {
        private static readonly DateTime TuesdayNoon = new(2026, 9, 22, 12, 0, 0);

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "StrongSignalCsvLoggerTests-" + Guid.NewGuid());

        public void Dispose() {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private StrongSignalCsvLogger CreateLogger(bool resetOnStart) =>
            new(_directory, resetOnStart, "XAUUSD", TestSettings.NoStopOffset());

        private static void AppendOneRow(StrongSignalCsvLogger logger) =>
            logger.Append(TestSignal.Short(entry: 99.0, stopLoss: 101.0), EntryOutcomeModel.Blocked(EntryGateModel.Session),
                TuesdayNoon);

        [Fact]
        public void the_file_is_named_debug_csv_and_starts_with_the_header() {
            StrongSignalCsvLogger logger = CreateLogger(resetOnStart: true);

            Assert.Equal(Path.Combine(_directory, "debug.csv"), logger.FilePath);
            Assert.Equal(new[] { StrongSignalCsvColumns.Header }, File.ReadAllLines(logger.FilePath));
        }

        [Fact]
        public void without_reset_a_file_with_the_current_header_keeps_its_rows() {
            AppendOneRow(CreateLogger(resetOnStart: true));

            StrongSignalCsvLogger second = CreateLogger(resetOnStart: false);
            AppendOneRow(second);

            Assert.Equal(3, File.ReadAllLines(second.FilePath).Length);
        }

        [Fact]
        public void with_reset_the_previous_rows_are_dropped() {
            AppendOneRow(CreateLogger(resetOnStart: true));

            StrongSignalCsvLogger second = CreateLogger(resetOnStart: true);

            Assert.Single(File.ReadAllLines(second.FilePath));
        }

        [Fact]
        public void without_reset_a_file_with_an_old_header_starts_over() {
            Directory.CreateDirectory(_directory);
            File.WriteAllLines(Path.Combine(_directory, "debug.csv"), new[] { "old,header", "1,2" });

            StrongSignalCsvLogger logger = CreateLogger(resetOnStart: false);

            Assert.Equal(new[] { StrongSignalCsvColumns.Header }, File.ReadAllLines(logger.FilePath));
        }
    }
}
