using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.TradeLog {
    // The indicator snapshot taken at entry is what both the entry row and the close row report.
    // TradeCsvLogger needs a cTrader Position, so this covers the part that can be built without
    // one: the plan carries the snapshot, and the schema has a column for every value in it.
    public class TradeCsvSnapshotTests {
        [Fact]
        public void the_plan_carries_the_reading_and_the_metrics_unchanged() {
            VwapStrongReadingModel reading = new() {
                Close = 106.0, DailyVwap = 104.0, WeeklyVwap = 100.0, DailyVwapBefore = 102.0, WeeklyVwapBefore = 99.0,
                Atr14M5 = 2.0, Atr14H1 = 8.0, LookbackN = 6, SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };
            VwapStrongMetricsModel metrics = VwapStrongMetrics.Compute(reading, SignalSideModel.Buy);

            SignalModel signal = new() { Strong = reading, Metrics = metrics };
            OrderPlanModel plan = new() { VwapReading = signal.Strong, VwapMetrics = signal.Metrics };

            // Same objects, so a close row written minutes later reports the entry's values.
            Assert.Same(reading, plan.VwapReading);
            Assert.Same(metrics, plan.VwapMetrics);
            Assert.Equal(8.0, plan.VwapReading.SelectedAtr, precision: 9);
            Assert.Equal(6, plan.VwapReading.LookbackN);
        }

        [Fact]
        public void the_selected_atr_follows_the_chosen_period() {
            VwapStrongReadingModel onM5 = new() {
                Atr14M5 = 2.0, Atr14H1 = 8.0, SelectedAtrPeriod = Atr14SourceModel.ATR14_M5
            };
            VwapStrongReadingModel onH1 = new() {
                Atr14M5 = 2.0, Atr14H1 = 8.0, SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };

            Assert.Equal(2.0, onM5.SelectedAtr, precision: 9);
            Assert.Equal(8.0, onH1.SelectedAtr, precision: 9);
        }

        [Fact]
        public void the_schema_has_a_column_for_every_value_the_pdf_lists() {
            string[] columns = TradeCsvColumns.Header.Split(',');

            foreach (string name in new[] {
                         "入场时间", "多空", "DailyVWAP", "WeeklyVWAP", "DailyVWAP_Lookback", "WeeklyVWAP_Lookback",
                         "LookbackN", "ATR14_M5", "ATR14_H1", "GapX_M5", "GapX_H1", "SlopeRawX_M5", "SlopeRawX_H1",
                         "SlopeRateX30_M5", "SlopeRateX30_H1", "SelectedATRPeriod", "GapX_Selected",
                         "SlopeRateX_Selected", "GapChangeX", "ResultR",
                         "TradeDirectionMode", "UseGapChangeFilter", "GapChangeRateMin", "GapChangeRateMax",
                         "GapChangeRawPrice", "GapChangeRawX_M5", "GapChangeRawX_H1", "GapChangeRateX30_M5",
                         "GapChangeRateX30_H1", "GapChangeRateX30_Selected"
                     }) {
                Assert.Contains(name, columns);
            }
        }

        [Fact]
        public void the_schema_has_a_column_for_every_departure_value_the_pdf_lists() {
            string[] columns = TradeCsvColumns.Header.Split(',');

            foreach (string name in new[] {
                         "DepartureX_Selected", "DepartureMin", "DepartureConfirmBars", "DepartureConfirmCount",
                         "DepartureConfirmed", "DepartureBarsSinceConfirmed", "DepartureMaxWaitBars"
                     }) {
                Assert.Contains(name, columns);
            }
        }

        [Fact]
        public void a_row_reports_the_departure_state_of_its_entry_bar() {
            string[] cells = Row(Snapshot(min: 1.0, confirmBars: 3, maxWaitBars: 4, departureX: -0.35, confirmed: true,
                confirmCount: 3, barsSinceConfirmed: 2));

            // The entry bar is a pullback bar, so a negative distance here is the normal case.
            Assert.Equal("-0.35", Cell(cells, "DepartureX_Selected"));
            Assert.Equal("1", Cell(cells, "DepartureMin"));
            Assert.Equal("3", Cell(cells, "DepartureConfirmBars"));
            Assert.Equal("3", Cell(cells, "DepartureConfirmCount"));
            Assert.Equal("True", Cell(cells, "DepartureConfirmed"));
            Assert.Equal("2", Cell(cells, "DepartureBarsSinceConfirmed"));
            Assert.Equal("4", Cell(cells, "DepartureMaxWaitBars"));
        }

        [Fact]
        public void a_zero_count_is_written_rather_than_blanked() {
            // 0 is a real reading here — it says the count had just been cleared. Blanking it
            // would be indistinguishable from "this build did not record it".
            string[] cells = Row(Snapshot(min: 1.0, confirmCount: 0, barsSinceConfirmed: 0));

            Assert.Equal("0", Cell(cells, "DepartureConfirmCount"));
            Assert.Equal("0", Cell(cells, "DepartureBarsSinceConfirmed"));
        }

        [Fact]
        public void a_run_with_the_gate_off_reports_only_the_threshold() {
            // Same reason GapChangeRateMin/Max stay blank while that filter is off: those values
            // took no part in the decision, and 0 would read as "it was required and met".
            string[] cells = Row(Snapshot(min: 0.0, confirmBars: 3, maxWaitBars: 4));

            Assert.Equal("0", Cell(cells, "DepartureMin"));
            Assert.Equal("", Cell(cells, "DepartureConfirmBars"));
            Assert.Equal("", Cell(cells, "DepartureConfirmCount"));
            Assert.Equal("", Cell(cells, "DepartureConfirmed"));
            Assert.Equal("", Cell(cells, "DepartureBarsSinceConfirmed"));
            Assert.Equal("", Cell(cells, "DepartureMaxWaitBars"));
            Assert.Equal("", Cell(cells, "DepartureX_Selected"));
        }

        [Fact]
        public void a_row_without_a_departure_snapshot_leaves_every_departure_column_blank() {
            // Positions opened by an older build: no snapshot, so nothing to report — 0 would be
            // a fabricated reading.
            string[] cells = Row(departure: null);

            foreach (string name in new[] {
                         "DepartureX_Selected", "DepartureMin", "DepartureConfirmBars", "DepartureConfirmCount",
                         "DepartureConfirmed", "DepartureBarsSinceConfirmed", "DepartureMaxWaitBars"
                     }) {
                Assert.Equal("", Cell(cells, name));
            }
        }

        private static DepartureSnapshotModel Snapshot(double min, int confirmBars = 3, int maxWaitBars = 0,
            double departureX = double.NaN, bool confirmed = false, int confirmCount = 0, int barsSinceConfirmed = 0) =>
            new() {
                Settings = new DepartureSettingsModel(departureMin: min, confirmBars: confirmBars, maxWaitBars: maxWaitBars),
                DepartureX = departureX,
                IsConfirmed = confirmed,
                ConfirmCount = confirmCount,
                BarsSinceConfirmed = barsSinceConfirmed
            };

        private static string[] Row(DepartureSnapshotModel departure) {
            TradeRecordModel record = new() { EntryPlan = new OrderPlanModel { Departure = departure } };

            return TradeCsvColumns.ToCsvLine(record).Split(',');
        }

        private static string Cell(string[] cells, string columnName) =>
            cells[System.Array.IndexOf(TradeCsvColumns.Header.Split(','), columnName)];

        [Fact]
        public void the_old_columns_keep_their_place_and_meaning() {
            string[] columns = TradeCsvColumns.Header.Split(',');

            // ATR14 / GapX hold the selected period's readings; SlopeX stays the raw slope.
            Assert.Equal(23, System.Array.IndexOf(columns, "ATR14"));
            Assert.Equal(24, System.Array.IndexOf(columns, "GapX"));
            Assert.Equal(25, System.Array.IndexOf(columns, "SlopeX"));
        }
    }
}
