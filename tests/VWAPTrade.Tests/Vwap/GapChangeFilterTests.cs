using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // 扩口变化（VWAP_Strong_V1.1 第 3、4 节）：
    //   GapChangeRawPrice = Gap₀ − Gapₙ，两端都已按方向统一成「顺势为正」
    //   GapChangeRateX30  = GapChangeRawPrice / ATR × 6/N
    // 过滤用独立开关 + 闭区间，不沿用「0 = 关闭」—— 它允许负值，区间还可能跨过 0。
    public class GapChangeFilterTests {
        // 多头：Gap₀ = 104−100 = 4.0，Gapₙ = 102−99 = 3.0，所以 RawPrice = +1.0（开口在扩大）。
        private static VwapStrongReadingModel Long(double dailyBefore = 102.0, double weeklyBefore = 99.0, int lookbackN = 6,
            Atr14SourceModel selected = Atr14SourceModel.ATR14_H1) =>
            new() {
                Close = 106.0, DailyVwap = 104.0, WeeklyVwap = 100.0,
                DailyVwapBefore = dailyBefore, WeeklyVwapBefore = weeklyBefore,
                Atr14M5 = 2.0, Atr14H1 = 8.0, LookbackN = lookbackN, SelectedAtrPeriod = selected
            };

        // 空头镜像：Gap₀ = 100−96 = 4.0，Gapₙ = 101−98 = 3.0，RawPrice 同样是 +1.0。
        private static VwapStrongReadingModel Short(double dailyBefore = 98.0, double weeklyBefore = 101.0) =>
            new() {
                Close = 94.0, DailyVwap = 96.0, WeeklyVwap = 100.0,
                DailyVwapBefore = dailyBefore, WeeklyVwapBefore = weeklyBefore,
                Atr14M5 = 2.0, Atr14H1 = 8.0, LookbackN = 6, SelectedAtrPeriod = Atr14SourceModel.ATR14_H1
            };

        private static VwapFilterSettingsModel Filters(bool use = false, double min = 0.0, double max = 0.0) =>
            new(gapMin: 0.0, slopeRateMin: 0.0, useGapChangeFilter: use, gapChangeRateMin: min, gapChangeRateMax: max);

        // Only the gap change is ever switched on here, so a failure can only be GapChange.
        private static EntryGateModel FailedFilter(VwapStrongReadingModel reading, VwapFilterSettingsModel filters) =>
            VwapStack.FindFailedFilter(VwapStrongMetrics.Compute(reading, SignalSideModel.Buy), filters);

        [Theory]
        [InlineData(3, 2.0)] // 6/3
        [InlineData(6, 1.0)] // 6/6
        [InlineData(9, 2.0 / 3.0)] // 6/9
        public void the_rate_is_the_raw_change_times_six_over_n(int lookbackN, double expectedFactor) {
            VwapStrongMetricsModel m = VwapStrongMetrics.Compute(Long(lookbackN: lookbackN), SignalSideModel.Buy);

            Assert.Equal(1.0, m.GapChangeRawPrice, precision: 9);
            Assert.Equal(1.0 / 8.0, m.GapChangeRawXH1, precision: 9); // 价格变化 ÷ H1 的 ATR
            Assert.Equal(m.GapChangeRawXH1 * expectedFactor, m.GapChangeRateX30H1, precision: 9);
        }

        [Fact]
        public void a_widening_gap_is_positive_and_a_narrowing_one_negative_for_a_long() {
            // 缩口：Gapₙ = 104−96 = 8.0 > Gap₀ = 4.0
            VwapStrongMetricsModel widening = VwapStrongMetrics.Compute(Long(), SignalSideModel.Buy);
            VwapStrongMetricsModel narrowing =
                VwapStrongMetrics.Compute(Long(dailyBefore: 104.0, weeklyBefore: 96.0), SignalSideModel.Buy);

            Assert.True(widening.GapChangeRateX30Selected > 0.0);
            Assert.True(narrowing.GapChangeRateX30Selected < 0.0);
        }

        [Fact]
        public void a_short_reports_the_same_signs_as_a_long() {
            // 空头扩口扩大同样是正数：方向已由 direction 统一，多空共用一个区间。
            VwapStrongMetricsModel widening = VwapStrongMetrics.Compute(Short(), SignalSideModel.Sell);
            VwapStrongMetricsModel narrowing =
                VwapStrongMetrics.Compute(Short(dailyBefore: 96.0, weeklyBefore: 104.0), SignalSideModel.Sell);

            Assert.True(widening.GapChangeRateX30Selected > 0.0);
            Assert.True(narrowing.GapChangeRateX30Selected < 0.0);

            // 多空对称：同样的扩口幅度得同样的数值。
            VwapStrongMetricsModel longSide = VwapStrongMetrics.Compute(Long(), SignalSideModel.Buy);
            Assert.Equal(longSide.GapChangeRateX30Selected, widening.GapChangeRateX30Selected, precision: 9);
        }

        [Fact]
        public void switching_atr_changes_only_the_denominator() {
            VwapStrongMetricsModel onH1 = VwapStrongMetrics.Compute(Long(), SignalSideModel.Buy);
            VwapStrongMetricsModel onM5 =
                VwapStrongMetrics.Compute(Long(selected: Atr14SourceModel.ATR14_M5), SignalSideModel.Buy);

            Assert.Equal(onH1.GapChangeRawPrice, onM5.GapChangeRawPrice, precision: 9); // 价格变化本身不变
            Assert.Equal(onH1.GapChangeRateX30H1, onH1.GapChangeRateX30Selected, precision: 9);
            Assert.Equal(onM5.GapChangeRateX30M5, onM5.GapChangeRateX30Selected, precision: 9);
        }

        [Fact]
        public void the_old_column_keeps_its_original_meaning() {
            // 旧 GapChangeX 仍是「未做 30 分钟标准化」的那一版，不能被悄悄改成新口径。
            VwapStrongMetricsModel m = VwapStrongMetrics.Compute(Long(lookbackN: 3), SignalSideModel.Buy);

            Assert.Equal(m.GapChangeRawXH1, m.GapChangeX, precision: 9);
            Assert.Equal(m.GapChangeX * 2.0, m.GapChangeRateX30Selected, precision: 9);
        }

        [Fact]
        public void the_filter_is_off_by_default_and_lets_everything_through() {
            // 关闭时连数值可不可用都不看 —— 关掉的过滤器不能变成新的过滤条件。
            VwapStrongReadingModel noLookback = Long(dailyBefore: double.NaN, weeklyBefore: double.NaN);

            Assert.Equal(EntryGateModel.None, FailedFilter(Long(), Filters()));
            Assert.Equal(EntryGateModel.None, FailedFilter(noLookback, Filters()));
        }

        [Fact]
        public void an_enabled_filter_accepts_only_the_closed_interval() {
            // 这一笔的 GapChangeRateX30_Selected = 1.0/8 × 1 = 0.125
            Assert.Equal(EntryGateModel.None, FailedFilter(Long(), Filters(use: true, min: 0.10, max: 0.15)));
            Assert.Equal(EntryGateModel.GapChange, FailedFilter(Long(), Filters(use: true, min: 0.13, max: 0.20)));
            Assert.Equal(EntryGateModel.GapChange, FailedFilter(Long(), Filters(use: true, min: 0.00, max: 0.12)));
        }

        [Theory]
        [InlineData(0.125, 0.20)] // 正好等于下界
        [InlineData(0.05, 0.125)] // 正好等于上界
        public void the_interval_endpoints_pass(double min, double max) {
            Assert.Equal(EntryGateModel.None, FailedFilter(Long(), Filters(use: true, min, max)));
        }

        [Fact]
        public void a_negative_interval_is_usable_which_is_why_zero_cannot_mean_off() {
            // 缩口的一笔：−0.5。区间跨过 0，用 0 当关闭值就会有歧义。
            VwapStrongReadingModel narrowing = Long(dailyBefore: 104.0, weeklyBefore: 96.0);

            Assert.Equal(EntryGateModel.None, FailedFilter(narrowing, Filters(use: true, min: -0.60, max: 0.10)));
            Assert.Equal(EntryGateModel.GapChange, FailedFilter(narrowing, Filters(use: true, min: -0.40, max: 0.10)));
        }

        [Fact]
        public void an_enabled_filter_rejects_an_unusable_value() {
            VwapStrongReadingModel noLookback = Long(dailyBefore: double.NaN, weeklyBefore: double.NaN);
            VwapStrongReadingModel noAtr = Long();
            noAtr.Atr14H1 = double.NaN;

            Assert.Equal(EntryGateModel.GapChange, FailedFilter(noLookback, Filters(use: true, min: -1.0, max: 1.0)));
            Assert.Equal(EntryGateModel.GapChange, FailedFilter(noAtr, Filters(use: true, min: -1.0, max: 1.0)));
        }

        [Fact]
        public void an_enabled_filter_with_a_zero_zero_range_still_filters() {
            // 「开关关着」与「区间正好是 [0,0]」是两回事。若用 Min/Max 是否为 0 来判断关闭，
            // 这一笔（0.125）会被误放行 —— 这正是 GapChange 必须用独立开关的原因。
            Assert.Equal(EntryGateModel.GapChange, FailedFilter(Long(), Filters(use: true, min: 0.0, max: 0.0)));
            Assert.Equal(EntryGateModel.None, FailedFilter(Long(), Filters(use: false, min: 0.0, max: 0.0)));
        }

        [Fact]
        public void a_zero_change_sits_inside_a_zero_zero_range() {
            // 开口纹丝不动的一笔：RawPrice = 0，落在 [0,0] 里，应当放行。
            VwapStrongReadingModel stable = Long(dailyBefore: 103.0, weeklyBefore: 99.0);

            Assert.Equal(0.0, VwapStrongMetrics.Compute(stable, SignalSideModel.Buy).GapChangeRateX30Selected, precision: 9);
            Assert.Equal(EntryGateModel.None, FailedFilter(stable, Filters(use: true, min: 0.0, max: 0.0)));
        }

        [Fact]
        public void an_inverted_range_is_reported_rather_than_silently_swapped() {
            VwapFilterSettingsModel inverted = Filters(use: true, min: 0.05, max: -0.05);
            VwapFilterSettingsModel disabled = Filters(use: false, min: 0.05, max: -0.05);

            Assert.True(inverted.IsGapChangeRangeInverted);
            Assert.False(disabled.IsGapChangeRangeInverted); // 关着的时候不看这两个数
        }
    }
}
