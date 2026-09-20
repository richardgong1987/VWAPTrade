using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.OrderLogger {
    // The header and the migrator's target width come from the same place. These tests pin that
    // the schema is what the strategy notes ask for, and that every row the logger writes is as
    // wide as the header — a row one column short silently shifts every value after it.
    public class TradeCsvSchemaTests {
        [Fact]
        public void the_header_has_one_name_per_column() {
            Assert.Equal(TradeCsvSchema.ColumnCount, TradeCsvSchema.Header.Split(',').Length);
        }

        [Fact]
        public void the_schema_carries_the_tuning_columns() {
            string[] columns = TradeCsvSchema.Header.Split(',');

            Assert.Contains("入场时间", columns);
            Assert.Contains("多空", columns);
            Assert.Contains("DailyVWAP", columns);
            Assert.Contains("WeeklyVWAP", columns);
            Assert.Contains("ATR14_H1", columns);
            Assert.Contains("GapX", columns);
            Assert.Contains("SlopeX", columns);
            Assert.Contains("最终结果", columns);
            Assert.Contains("DailyVWAP_Lookback", columns);
            Assert.Contains("ResultR", columns);
            Assert.Contains("GapChangeX", columns);
        }

        [Fact]
        public void no_column_name_is_repeated() {
            string[] columns = TradeCsvSchema.Header.Split(',');

            Assert.Equal(columns.Length, new System.Collections.Generic.HashSet<string>(columns).Count);
        }

        [Fact]
        public void the_newest_columns_come_last() {
            // Migration pads old rows on the right, so anything added must stay at the end.
            Assert.EndsWith("DailyVWAP_Lookback,ResultR,GapChangeX", TradeCsvSchema.Header);
        }
    }
}
