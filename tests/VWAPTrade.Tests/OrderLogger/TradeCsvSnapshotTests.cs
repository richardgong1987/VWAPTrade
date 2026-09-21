using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.OrderLogger {
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
            string[] columns = TradeCsvSchema.Header.Split(',');

            foreach (string name in new[] {
                         "入场时间", "多空", "DailyVWAP", "WeeklyVWAP", "DailyVWAP_Lookback", "WeeklyVWAP_Lookback",
                         "LookbackN", "ATR14_M5", "ATR14_H1", "GapX_M5", "GapX_H1", "SlopeRawX_M5", "SlopeRawX_H1",
                         "SlopeRateX30_M5", "SlopeRateX30_H1", "SelectedATRPeriod", "GapX_Selected",
                         "SlopeRateX_Selected", "GapChangeX", "ResultR"
                     }) {
                Assert.Contains(name, columns);
            }
        }

        [Fact]
        public void the_old_columns_keep_their_place_and_meaning() {
            string[] columns = TradeCsvSchema.Header.Split(',');

            // ATR14 / GapX hold the selected period's readings; SlopeX stays the raw slope.
            Assert.Equal(23, System.Array.IndexOf(columns, "ATR14"));
            Assert.Equal(24, System.Array.IndexOf(columns, "GapX"));
            Assert.Equal(25, System.Array.IndexOf(columns, "SlopeX"));
        }
    }
}
