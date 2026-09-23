using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // FindBlockingGate names the first of gates 1–4 that stops a side. debug.csv reports it, so it
    // must name the gate trading actually stopped at: the same order ResolveSide applies.
    public class VwapStackBlockingGateTests {
        // Long: GapX = 4/8 = 0.5, SlopeRateX30 = 2/8 = 0.25, GapChangeRateX30 = 1/8 = 0.125 (H1 ATR 8).
        private static VwapStrongReadingModel Long() =>
            new() {
                Close = 106.0, DailyVwap = 104.0, WeeklyVwap = 100.0,
                DailyVwapBefore = 102.0, WeeklyVwapBefore = 99.0,
                Atr14M5 = 2.0, Atr14H1 = 8.0, LookbackN = 6, SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };

        private static VwapFilterSettingsModel Filters(double gapMin = 0.0, double slopeRateMin = 0.0,
            bool useGapChange = false, double gapChangeMin = 0.0, double gapChangeMax = 0.0) =>
            new(gapMin, slopeRateMin, useGapChange, gapChangeMin, gapChangeMax);

        [Fact]
        public void a_pattern_against_the_stack_is_stopped_by_the_stack() {
            Assert.Equal(EntryGateModel.Stack, VwapStack.FindBlockingGate(Long(), Filters(), SignalSideModel.Sell));
        }

        [Fact]
        public void a_side_the_stack_points_to_passes_when_every_filter_is_off() {
            Assert.Equal(EntryGateModel.None, VwapStack.FindBlockingGate(Long(), Filters(), SignalSideModel.Buy));
        }

        [Fact]
        public void each_filter_is_named_when_it_is_the_one_that_fails() {
            Assert.Equal(EntryGateModel.GapMin,
                VwapStack.FindBlockingGate(Long(), Filters(gapMin: 0.6), SignalSideModel.Buy));
            Assert.Equal(EntryGateModel.SlopeRateMin,
                VwapStack.FindBlockingGate(Long(), Filters(slopeRateMin: 0.3), SignalSideModel.Buy));
            Assert.Equal(EntryGateModel.GapChange,
                VwapStack.FindBlockingGate(Long(), Filters(useGapChange: true, gapChangeMin: 0.2, gapChangeMax: 0.3),
                    SignalSideModel.Buy));
        }

        [Fact]
        public void when_several_filters_fail_the_first_in_trading_order_is_named() {
            VwapFilterSettingsModel allFail = Filters(gapMin: 0.6, slopeRateMin: 0.3, useGapChange: true, gapChangeMin: 0.2,
                gapChangeMax: 0.3);

            Assert.Equal(EntryGateModel.GapMin, VwapStack.FindBlockingGate(Long(), allFail, SignalSideModel.Buy));
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(0.5, 0.0)]
        [InlineData(0.6, 0.0)]
        [InlineData(0.0, 0.25)]
        [InlineData(0.0, 0.3)]
        public void resolve_side_trades_exactly_when_no_gate_blocks(double gapMin, double slopeRateMin) {
            VwapFilterSettingsModel filters = Filters(gapMin, slopeRateMin);
            bool isBlocked = VwapStack.FindBlockingGate(Long(), filters, SignalSideModel.Buy) != EntryGateModel.None;

            Assert.Equal(isBlocked ? SignalSideModel.None : SignalSideModel.Buy, VwapStack.ResolveSide(Long(), filters));
        }
    }
}
