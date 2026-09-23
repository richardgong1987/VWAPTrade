using System.Collections.Generic;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    public class LongBelowDailyVwapGateTests {
        private static LongBelowDailyVwapSettingsModel Settings(int lookbackBars = 6, int blockCount = 3) =>
            new(lookbackBars, blockCount);

        // Values are distances from a daily VWAP of 100.0, newest preceding bar first.
        private static LongBelowDailyVwapSnapshotModel Evaluate(SignalSideModel side, int lookbackBars, int blockCount,
            params double[] closeDistances) {
            var bars = new List<RecentDailyVwapBarModel>();

            foreach (double distance in closeDistances) {
                bars.Add(new RecentDailyVwapBarModel(close: 100.0 + distance, dailyVwap: 100.0));
            }

            return LongBelowDailyVwapGate.Evaluate(side, Settings(lookbackBars, blockCount), bars);
        }

        [Fact]
        public void three_of_the_previous_six_closes_below_daily_vwap_block_a_long() {
            LongBelowDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Buy, lookbackBars: 6, blockCount: 3,
                -1.0, 0.5, -0.5, 0.2, -0.2, 0.1);

            Assert.True(snapshot.IsBlocked);
            Assert.Equal(6, snapshot.CheckedBars);
            Assert.Equal(3, snapshot.BelowCount);
        }

        [Fact]
        public void fewer_than_the_block_count_do_not_block_a_long() {
            LongBelowDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Buy, lookbackBars: 6, blockCount: 3,
                -1.0, 0.5, -0.5, 0.2, 0.1, 0.3);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(2, snapshot.BelowCount);
        }

        [Fact]
        public void only_the_configured_lookback_is_counted() {
            LongBelowDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Buy, lookbackBars: 6, blockCount: 3,
                -1.0, 0.5, -0.5, 0.2, 0.1, 0.3, -0.2, -0.4);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(6, snapshot.CheckedBars);
            Assert.Equal(2, snapshot.BelowCount);
        }

        [Fact]
        public void an_equal_close_is_not_below_the_daily_vwap() {
            LongBelowDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Buy, lookbackBars: 6, blockCount: 3,
                0.0, -0.5, 0.1, -0.2, 0.2, 0.3);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(2, snapshot.BelowCount);
        }

        [Fact]
        public void the_gate_never_blocks_a_short() {
            LongBelowDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Sell, lookbackBars: 6, blockCount: 3,
                -1.0, -0.5, -0.2, -0.1, -0.3, -0.4);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(0, snapshot.CheckedBars);
        }

        [Fact]
        public void a_zero_block_count_switches_the_gate_off_completely() {
            LongBelowDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Buy, lookbackBars: 0, blockCount: 0,
                -1.0, -0.5, -0.2, -0.1, -0.3, -0.4);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(0, snapshot.CheckedBars);
        }
    }
}
