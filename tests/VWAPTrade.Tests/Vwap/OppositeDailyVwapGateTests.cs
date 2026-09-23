using System.Collections.Generic;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    public class OppositeDailyVwapGateTests {
        private static OppositeDailyVwapSettingsModel Settings(int lookbackBars = 6, int blockCount = 3) =>
            new(lookbackBars, blockCount);

        // Values are distances from a daily VWAP of 100.0, newest preceding bar first.
        private static OppositeDailyVwapSnapshotModel Evaluate(SignalSideModel side, int lookbackBars, int blockCount,
            params double[] closeDistances) {
            var bars = new List<RecentDailyVwapBarModel>();

            foreach (double distance in closeDistances) {
                bars.Add(new RecentDailyVwapBarModel(close: 100.0 + distance, dailyVwap: 100.0));
            }

            return OppositeDailyVwapGate.Evaluate(side, Settings(lookbackBars, blockCount), bars);
        }

        [Fact]
        public void three_of_the_previous_six_closes_below_daily_vwap_block_a_long() {
            OppositeDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Buy, lookbackBars: 6, blockCount: 3,
                -1.0, 0.5, -0.5, 0.2, -0.2, 0.1);

            Assert.True(snapshot.IsBlocked);
            Assert.Equal(6, snapshot.CheckedBars);
            Assert.Equal(3, snapshot.OppositeSideCount);
        }

        [Fact]
        public void fewer_than_the_block_count_do_not_block_a_long() {
            OppositeDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Buy, lookbackBars: 6, blockCount: 3,
                -1.0, 0.5, -0.5, 0.2, 0.1, 0.3);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(2, snapshot.OppositeSideCount);
        }

        [Fact]
        public void only_the_configured_lookback_is_counted() {
            OppositeDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Buy, lookbackBars: 6, blockCount: 3,
                -1.0, 0.5, -0.5, 0.2, 0.1, 0.3, -0.2, -0.4);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(6, snapshot.CheckedBars);
            Assert.Equal(2, snapshot.OppositeSideCount);
        }

        [Fact]
        public void an_equal_close_is_not_on_the_opposite_side() {
            OppositeDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Buy, lookbackBars: 6, blockCount: 3,
                0.0, -0.5, 0.1, -0.2, 0.2, 0.3);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(2, snapshot.OppositeSideCount);
        }

        [Fact]
        public void three_of_the_previous_six_closes_above_daily_vwap_block_a_short() {
            OppositeDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Sell, lookbackBars: 6, blockCount: 3,
                1.0, -0.5, 0.5, -0.2, 0.2, -0.1);

            Assert.True(snapshot.IsBlocked);
            Assert.Equal(6, snapshot.CheckedBars);
            Assert.Equal(3, snapshot.OppositeSideCount);
        }

        [Fact]
        public void closes_below_daily_vwap_do_not_block_a_short() {
            OppositeDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Sell, lookbackBars: 6, blockCount: 3,
                -1.0, -0.5, -0.2, -0.1, -0.3, -0.4);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(6, snapshot.CheckedBars);
            Assert.Equal(0, snapshot.OppositeSideCount);
        }

        [Fact]
        public void a_zero_block_count_switches_the_gate_off_completely() {
            OppositeDailyVwapSnapshotModel snapshot = Evaluate(SignalSideModel.Sell, lookbackBars: 0, blockCount: 0,
                1.0, 0.5, 0.2, 0.1, 0.3, 0.4);

            Assert.False(snapshot.IsBlocked);
            Assert.Equal(0, snapshot.CheckedBars);
        }
    }
}
