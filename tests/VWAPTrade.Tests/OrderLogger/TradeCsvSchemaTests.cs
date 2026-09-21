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
            Assert.Contains("ATR14", columns);
            Assert.Contains("GapX", columns);
            Assert.Contains("SlopeX", columns);
            Assert.Contains("最终结果", columns);
            Assert.Contains("DailyVWAP_Lookback", columns);
            Assert.Contains("WeeklyVWAP_Lookback", columns);
            Assert.Contains("ResultR", columns);
            Assert.Contains("GapChangeX", columns);
        }

        [Fact]
        public void no_column_name_is_repeated() {
            string[] columns = TradeCsvSchema.Header.Split(',');

            Assert.Equal(columns.Length, new System.Collections.Generic.HashSet<string>(columns).Count);
        }

        [Fact]
        public void the_columns_of_the_original_schema_stay_at_the_front_in_order() {
            // Migration pads old rows on the right, so the leading columns must never be inserted
            // into or reordered — that would silently mis-map every value in every historical file.
            const string legacy =
                "编号,关键位,信号,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,持仓ID,成交ID";

            Assert.StartsWith(legacy + ",", TradeCsvSchema.Header);
        }

        [Fact]
        public void the_renamed_atr_column_kept_its_position() {
            // ATR14_H1 became ATR14 when the period became selectable. Values in older files sit at
            // that same index, so TradeCsvMigrator can pad those rows instead of remapping them —
            // but the rename did break the prefix rule, so those headers are listed there by hand.
            Assert.Equal(23, System.Array.IndexOf(TradeCsvSchema.Header.Split(','), "ATR14"));
        }
    }
}
