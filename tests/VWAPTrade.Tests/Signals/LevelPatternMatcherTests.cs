using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Signals {
    // The key level is the daily VWAP — the yellow line on the chart. A candle pattern only becomes
    // a signal when it actually touches that line, and only the candles that form the pattern count:
    // pinbar one, engulfing two, fractal and harami three.
    public class LevelPatternMatcherTests {
        // Bearish pinbar spanning 99.5 to 110: upper wick 9.8 of a 10.5 range, lower wick 0.5.
        private static CandleModel BearishPinbar() => new(open: 100.0, high: 110.0, low: 99.5, close: 100.2);

        // A quiet candle well below the pinbar, used as filler where the pattern ignores it.
        private static CandleModel Filler() => new(open: 60.0, high: 61.0, low: 59.0, close: 60.5);

        [Fact]
        public void a_pattern_sitting_on_the_daily_vwap_becomes_a_signal() {
            SignalModel signal = LevelPatternMatcher.Match(BearishPinbar(), Filler(), Filler(), ShortLevel(dailyVwap: 105.0));

            Assert.NotNull(signal);
            Assert.Equal("S_Pin_1", signal.Label);
            Assert.Equal(110.0, signal.StopLoss, precision: 6);
        }

        [Fact]
        public void the_same_pattern_away_from_the_daily_vwap_is_ignored() {
            // Identical candle, but the line is above its high, so price never reached the level.
            SignalModel signal = LevelPatternMatcher.Match(BearishPinbar(), Filler(), Filler(), ShortLevel(dailyVwap: 120.0));

            Assert.Null(signal);
        }

        [Theory]
        [InlineData(99.5)] // the low exactly touches the line
        [InlineData(110.0)] // the high exactly touches the line
        public void touching_the_line_with_a_wick_is_enough(double dailyVwap) {
            Assert.NotNull(LevelPatternMatcher.Match(BearishPinbar(), Filler(), Filler(), ShortLevel(dailyVwap)));
        }

        [Fact]
        public void a_one_candle_pattern_is_not_let_through_by_a_neighbours_touch() {
            // The pinbar is a single-candle pattern, so only the signal candle may satisfy the touch.
            // Here the previous candle straddles the line and the pinbar does not.
            CandleModel previousOnTheLine = new(open: 69.0, high: 75.0, low: 65.0, close: 70.0);

            SignalModel signal = LevelPatternMatcher.Match(BearishPinbar(), previousOnTheLine, Filler(), ShortLevel(dailyVwap: 70.0));

            Assert.Null(signal);
        }

        [Fact]
        public void a_two_candle_pattern_accepts_a_touch_on_either_candle() {
            // Bearish engulfing: current engulfs previous and closes down. Only `previous` reaches
            // the line, which is enough because the pattern is built from both candles.
            CandleModel previous = new(open: 101.0, high: 105.0, low: 100.0, close: 100.5);
            CandleModel current = new(open: 106.0, high: 106.0, low: 99.0, close: 99.5);

            SignalModel signal = LevelPatternMatcher.Match(current, previous, Filler(), ShortLevel(dailyVwap: 104.0));

            Assert.NotNull(signal);
            Assert.Equal("S_Eng_1", signal.Label);
        }

        [Fact]
        public void a_bearish_pattern_is_not_taken_when_the_gate_only_allows_longs() {
            // Side comes from the VWAP stack gate; a long-only bar never matches a bearish pattern.
            TradeLevelModel longOnly = new("VWAP", SignalSideModel.Buy, price: 105.0);

            Assert.Null(LevelPatternMatcher.Match(BearishPinbar(), Filler(), Filler(), longOnly));
        }

        private static TradeLevelModel ShortLevel(double dailyVwap) => new("VWAP", SignalSideModel.Sell, dailyVwap);
    }
}
