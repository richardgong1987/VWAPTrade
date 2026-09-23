using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // 启动校验。这些规则一旦失效，cBot 会带着一套算错的参数安静跑完整个回测 —— 所以宁可停下来。
    public class StartupCheckTests {
        private static VwapFilterSettingsModel Filters(bool use = false, double min = 0.0, double max = 0.0) =>
            new(gapMin: 0.0, slopeRateMin: 0.0, useGapChangeFilter: use, gapChangeRateMin: min, gapChangeRateMax: max);

        private static DepartureSettingsModel Departure(double min = 0.0, int confirmBars = 3, int maxWaitBars = 0) =>
            new(departureMin: min, confirmBars: confirmBars, maxWaitBars: maxWaitBars);

        private static OppositeDailyVwapSettingsModel OppositeDailyVwap(int lookbackBars = 6, int blockCount = 0) =>
            new(lookbackBars, blockCount);

        private static string Check(bool isM5 = true, string label = "VWAPTrade-label", int lookbackN = 6,
            VwapFilterSettingsModel filters = null, DepartureSettingsModel departure = null,
            OppositeDailyVwapSettingsModel oppositeDailyVwap = null) =>
            StartupCheck.FindError(isM5, "m5", label, lookbackN, filters ?? Filters(), departure ?? Departure(),
                oppositeDailyVwap ?? OppositeDailyVwap());

        [Fact]
        public void a_correct_setup_reports_nothing() {
            Assert.Null(Check());
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void an_empty_order_label_is_refused(string label) {
            // 标签为空时，这个品种上每一个单子都会被当成本 cBot 的。
            Assert.Contains("订单标签", Check(label: label));
        }

        [Fact]
        public void a_chart_other_than_m5_is_refused() {
            // 6/N 换算写死了「M5 上 6 根 = 30 分钟」，别的周期这个系数不成立。
            Assert.Contains("只支持 M5", Check(isM5: false));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void a_lookback_below_one_is_refused(int lookbackN) {
            Assert.Contains("必须 ≥ 1", Check(lookbackN: lookbackN));
        }

        [Fact]
        public void an_inverted_gap_change_range_is_refused_rather_than_swapped() {
            Assert.Contains("区间为空", Check(filters: Filters(use: true, min: 0.05, max: -0.05)));
        }

        [Fact]
        public void an_inverted_range_is_ignored_while_the_filter_is_off() {
            // 关着的时候根本不读 Min/Max，不该因为它们拦住启动。
            Assert.Null(Check(filters: Filters(use: false, min: 0.05, max: -0.05)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void a_departure_confirm_count_below_one_is_refused(int confirmBars) {
            Assert.Contains("离开连续确认K线数", Check(departure: Departure(min: 1.0, confirmBars: confirmBars)));
        }

        [Fact]
        public void a_negative_departure_wait_is_refused() {
            Assert.Contains("最大等待K线数", Check(departure: Departure(min: 1.0, maxWaitBars: -1)));
        }

        [Fact]
        public void departure_values_are_ignored_while_the_gate_is_off() {
            // 关着的时候这两个值不参与判断，不该因为它们拦住启动。
            Assert.Null(Check(departure: Departure(min: 0.0, confirmBars: 0, maxWaitBars: -1)));
        }

        [Fact]
        public void an_opposite_side_count_above_the_lookback_is_refused() {
            Assert.Contains("不能大于回看K线数", Check(oppositeDailyVwap: OppositeDailyVwap(lookbackBars: 2, blockCount: 3)));
        }

        [Fact]
        public void disabled_opposite_side_values_are_ignored() {
            Assert.Null(Check(oppositeDailyVwap: OppositeDailyVwap(lookbackBars: 0, blockCount: 0)));
        }

        [Fact]
        public void the_label_is_checked_before_the_timeframe() {
            // 两个都错时先报标签：那是更基础的一个。
            Assert.Contains("订单标签", Check(isM5: false, label: ""));
        }
    }
}
