using System;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Indicators {
    // Which ATR bar a signal may use. The signal bar opens 11:30 and closes 11:35, so 11:35 is both
    // the moment OnBar fires and the moment the value is read.
    //
    // A bar has closed exactly when its next bar has opened, so the rule needs no timeframe of its
    // own: on M5 it lands on the signal bar itself, on H1 it lands on the previous hourly bar.
    public class ClosedBarSelectorTests {
        private static readonly DateTime EvaluationTime = new(2026, 9, 22, 11, 35, 0);

        [Fact]
        public void on_the_signal_timeframe_it_steps_back_to_the_bar_that_just_closed() {
            // GetIndexByTime(11:35) on M5 lands on the 11:35 bar, which has only just opened;
            // its successor opens 11:40, after the evaluation, so step back to 11:30.
            int index = ClosedBarSelector.ResolveClosedBarIndex(containingIndex: 100, nextBarOpenTime: At(11, 40), EvaluationTime);

            Assert.Equal(99, index);
        }

        [Fact]
        public void the_signal_bar_itself_counts_as_closed_and_is_not_lookahead() {
            // Index 99 is the 11:30 bar; its successor opened at exactly 11:35, so it has finished.
            // Using its ATR at 11:35 reads only settled data — the old code skipped it needlessly.
            int index = ClosedBarSelector.ResolveClosedBarIndex(containingIndex: 99, nextBarOpenTime: At(11, 35), EvaluationTime);

            Assert.Equal(99, index);
        }

        [Fact]
        public void on_a_higher_timeframe_it_steps_back_past_the_bar_still_forming() {
            // GetIndexByTime(11:35) on H1 lands on the 11:00 bar, which runs to 12:00 and is still
            // forming; its ATR would keep changing, so step back to the 10:00 bar.
            int index = ClosedBarSelector.ResolveClosedBarIndex(containingIndex: 50, nextBarOpenTime: At(12, 0), EvaluationTime);

            Assert.Equal(49, index);
        }

        [Fact]
        public void a_bar_with_no_successor_is_never_treated_as_closed() {
            // The last bar of the series: nothing proves it has finished.
            int index = ClosedBarSelector.ResolveClosedBarIndex(containingIndex: 100, nextBarOpenTime: null, EvaluationTime);

            Assert.Equal(99, index);
        }

        [Fact]
        public void a_successor_opening_after_the_evaluation_does_not_count() {
            int index = ClosedBarSelector.ResolveClosedBarIndex(containingIndex: 100,
                nextBarOpenTime: EvaluationTime.AddTicks(1), EvaluationTime);

            Assert.Equal(99, index);
        }

        private static DateTime At(int hour, int minute) => new(2026, 9, 22, hour, minute, 0);
    }
}
