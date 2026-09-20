using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.OrderLogger {
    // The migrator upgrades a trades CSV written by an older build. Its two recognised shapes are
    // the 22-column layout (today's columns plus 「回撤开仓模式」at index 3 and 「挂单ID」at index 19)
    // and that layout trailed by indicator-state columns.
    public class TradeCsvMigratorTests {
        private const string CurrentHeader =
            "编号,关键位,信号,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,持仓ID,成交ID";

        private const string PreviousHeader =
            "编号,关键位,信号,回撤开仓模式,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,挂单ID,持仓ID,成交ID";

        private const string HeaderBeforeDmsState = PreviousHeader + ",ATR_Ratio_H1,PD_Range_ATR";

        private const string PreviousRow =
            "1,Short1,S_Pin_1,收线入场,ENTRY,XAUUSD,h1,2026-09-18 13:00:00,2400,,2410,2380,10,10,,10000,,0,,777,123,456";

        private const string ExpectedRow =
            "1,Short1,S_Pin_1,ENTRY,XAUUSD,h1,2026-09-18 13:00:00,2400,,2410,2380,10,10,,10000,,0,,123,456";

        [Fact]
        public void leaves_a_file_that_is_already_on_the_current_schema_alone() {
            string[] lines = { CurrentHeader, ExpectedRow };

            Assert.Null(TradeCsvMigrator.Upgrade(lines, CurrentHeader));
        }

        [Fact]
        public void drops_the_entry_mode_and_pending_order_columns_from_the_previous_layout() {
            string[] lines = { PreviousHeader, PreviousRow };

            string[] upgraded = TradeCsvMigrator.Upgrade(lines, CurrentHeader);

            Assert.NotNull(upgraded);
            Assert.Equal(CurrentHeader, upgraded[0]);
            Assert.Equal(ExpectedRow, upgraded[1]);
        }

        [Fact]
        public void truncates_the_trailing_indicator_state_columns() {
            // 30 columns: the 22-column layout followed by eight GapX-era state values.
            string[] lines = { PreviousHeader, PreviousRow + ",0.8,1.2,25,24,1,0,3,4" };

            string[] upgraded = TradeCsvMigrator.Upgrade(lines, CurrentHeader);

            Assert.NotNull(upgraded);
            Assert.Equal(ExpectedRow, upgraded[1]);
        }

        [Fact]
        public void truncates_the_two_atr_state_columns_when_the_header_identifies_that_layout() {
            string[] lines = { HeaderBeforeDmsState, PreviousRow + ",0.8,1.2" };

            string[] upgraded = TradeCsvMigrator.Upgrade(lines, CurrentHeader);

            Assert.NotNull(upgraded);
            Assert.Equal(ExpectedRow, upgraded[1]);
        }

        [Fact]
        public void leaves_a_24_column_file_alone_when_its_header_does_not_identify_the_layout() {
            // Same column count as the ATR-state layout but an unknown header, so the row order
            // cannot be trusted. Rewriting the header here would mislabel every column.
            string[] lines = { "编号,未知的旧表头", PreviousRow + ",0.8,1.2" };

            Assert.Null(TradeCsvMigrator.Upgrade(lines, CurrentHeader));
            Assert.Equal("编号,未知的旧表头", lines[0]);
        }

        [Fact]
        public void migrates_old_rows_left_under_a_current_header() {
            string[] lines = { CurrentHeader, ExpectedRow, PreviousRow };

            string[] upgraded = TradeCsvMigrator.Upgrade(lines, CurrentHeader);

            Assert.NotNull(upgraded);
            Assert.Equal(ExpectedRow, upgraded[1]);
            Assert.Equal(ExpectedRow, upgraded[2]);
        }

        [Fact]
        public void keeps_blank_lines_and_still_upgrades_the_rows_around_them() {
            string[] lines = { PreviousHeader, PreviousRow, "", PreviousRow };

            string[] upgraded = TradeCsvMigrator.Upgrade(lines, CurrentHeader);

            Assert.NotNull(upgraded);
            Assert.Equal(ExpectedRow, upgraded[1]);
            Assert.Equal("", upgraded[2]);
            Assert.Equal(ExpectedRow, upgraded[3]);
        }

        [Fact]
        public void replaces_a_stale_header_when_the_rows_are_already_current() {
            string[] lines = { PreviousHeader, ExpectedRow };

            string[] upgraded = TradeCsvMigrator.Upgrade(lines, CurrentHeader);

            Assert.NotNull(upgraded);
            Assert.Equal(CurrentHeader, upgraded[0]);
        }
    }
}
