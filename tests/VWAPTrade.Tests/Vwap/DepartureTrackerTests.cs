using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // The Departure gate: price must first leave the daily VWAP before a pullback pattern on
    // that line may be traded (V2 section 6).
    //
    // Every bar here uses ATR = 1 and daily VWAP = 100, so a close of 102 reads as
    // DepartureX = 2 on the long side. Weekly VWAP picks the structure direction: 99 puts the
    // daily above it (long), 101 below it (short).
    public class DepartureTrackerTests {
        private const double DailyVwap = 100.0;
        private const double WeeklyForLong = 99.0;
        private const double WeeklyForShort = 101.0;

        private static DepartureTracker Tracker(double min = 1.0, int confirmBars = 3, int maxWaitBars = 0) =>
            new(new DepartureSettingsModel(departureMin: min, confirmBars: confirmBars, maxWaitBars: maxWaitBars));

        private static DepartureBarModel Bar(double close, double weekly = WeeklyForLong, double atr = 1.0,
            bool isDayStart = false) =>
            new() {
                Close = close,
                DailyVwap = DailyVwap,
                WeeklyVwap = weekly,
                SelectedAtr = atr,
                IsDayPeriodStart = isDayStart
            };

        private static void Observe(DepartureTracker tracker, int barCount, double close, double weekly = WeeklyForLong) {
            for (int i = 0; i < barCount; i++) {
                tracker.Observe(Bar(close, weekly));
            }
        }

        [Fact]
        public void a_threshold_of_zero_lets_every_signal_through() {
            // 0 = off, and an off gate must never become a new reason to skip a trade — not even
            // before it has seen a single bar.
            DepartureTracker tracker = Tracker(min: 0.0);

            Assert.False(tracker.IsEnabled);
            Assert.True(tracker.IsAllowed(SignalSideModel.Buy));
            Assert.True(tracker.IsAllowed(SignalSideModel.Sell));
        }

        [Fact]
        public void nothing_is_allowed_before_the_price_has_left() {
            DepartureTracker tracker = Tracker();

            Observe(tracker, barCount: 5, close: 100.5); // 0.5 ATR away — below the 1.0 threshold

            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void the_departure_needs_the_full_run_of_confirming_bars() {
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 2, close: 102.0);
            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));

            Observe(tracker, barCount: 1, close: 102.0);
            Assert.True(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void one_bar_back_at_the_line_restarts_the_count() {
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 2, close: 102.0);
            Observe(tracker, barCount: 1, close: 100.2); // breaks the run
            Observe(tracker, barCount: 2, close: 102.0);

            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void a_confirmed_departure_survives_the_pullback_it_is_waiting_for() {
            // The whole point of the gate is to trade the return to the daily VWAP, so a close
            // coming back — even through the line — must not cancel it (section 6.2 #6).
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 3, close: 102.0);
            Observe(tracker, barCount: 4, close: 99.8);

            Assert.True(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void a_long_departure_does_not_license_a_short_signal() {
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 3, close: 102.0);

            Assert.False(tracker.IsAllowed(SignalSideModel.Sell));
            Assert.False(tracker.IsAllowed(SignalSideModel.None));
        }

        [Fact]
        public void the_short_side_measures_the_distance_below_the_daily_vwap() {
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 3, close: 98.0, weekly: WeeklyForShort);

            Assert.True(tracker.IsAllowed(SignalSideModel.Sell));
            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void a_close_on_the_wrong_side_of_the_line_never_counts_as_leaving() {
            // Long structure, price below the daily VWAP: DepartureX is negative. Taking an
            // absolute value here would read a collapse as a departure.
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 5, close: 97.0);

            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void a_new_daily_period_clears_a_confirmed_departure() {
            // The daily VWAP restarts at 06:00, so the line the price left no longer exists.
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 3, close: 102.0);
            tracker.Observe(Bar(close: 102.0, isDayStart: true));

            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void the_structure_flipping_clears_a_confirmed_departure() {
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 3, close: 102.0);
            Observe(tracker, barCount: 1, close: 98.0, weekly: WeeklyForShort);

            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
            Assert.False(tracker.IsAllowed(SignalSideModel.Sell));
        }

        [Fact]
        public void a_wait_limit_of_zero_never_expires() {
            DepartureTracker tracker = Tracker(confirmBars: 3, maxWaitBars: 0);

            Observe(tracker, barCount: 3, close: 102.0);
            Observe(tracker, barCount: 500, close: 100.1);

            Assert.True(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void the_departure_expires_after_the_wait_limit() {
            // MaxWaitBars = 2 leaves the two bars after the confirming one to pull back.
            DepartureTracker tracker = Tracker(confirmBars: 3, maxWaitBars: 2);

            Observe(tracker, barCount: 3, close: 102.0);

            Observe(tracker, barCount: 2, close: 100.1);
            Assert.True(tracker.IsAllowed(SignalSideModel.Buy));

            Observe(tracker, barCount: 1, close: 100.1);
            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void an_expired_departure_can_be_earned_again() {
            DepartureTracker tracker = Tracker(confirmBars: 3, maxWaitBars: 1);

            Observe(tracker, barCount: 3, close: 102.0);
            Observe(tracker, barCount: 2, close: 100.1); // expires on the second one
            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));

            Observe(tracker, barCount: 3, close: 102.0);
            Assert.True(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void an_entry_spends_the_departure() {
            // The next trade has to leave the line on its own account (section 6.3).
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 3, close: 102.0);
            tracker.ResetAfterEntry();

            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void a_bar_without_an_atr_does_not_count_as_leaving() {
            // The gate is on, so a distance we cannot measure blocks rather than passes —
            // the same rule the Gap and Slope gates follow.
            DepartureTracker tracker = Tracker(confirmBars: 3);

            for (int i = 0; i < 5; i++) {
                tracker.Observe(Bar(close: 102.0, atr: double.NaN));
            }

            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void a_missing_atr_in_the_middle_restarts_the_count() {
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 2, close: 102.0);
            tracker.Observe(Bar(close: 102.0, atr: double.NaN));
            Observe(tracker, barCount: 2, close: 102.0);

            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
        }

        [Fact]
        public void a_flat_structure_holds_everything_back() {
            // 日VWAP = 周VWAP gives no direction to depart in.
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 5, close: 102.0, weekly: DailyVwap);

            Assert.False(tracker.IsAllowed(SignalSideModel.Buy));
            Assert.False(tracker.IsAllowed(SignalSideModel.Sell));
        }

        [Fact]
        public void the_snapshot_reports_the_state_of_the_bar_just_observed() {
            DepartureTracker tracker = Tracker(min: 1.0, confirmBars: 3, maxWaitBars: 4);

            Observe(tracker, barCount: 3, close: 102.0);
            Observe(tracker, barCount: 2, close: 99.5); // pullback, through the line

            DepartureSnapshotModel snapshot = tracker.CreateSnapshot();

            Assert.True(snapshot.IsEnabled);
            Assert.True(snapshot.IsConfirmed);
            Assert.Equal(3, snapshot.ConfirmCount);
            Assert.Equal(2, snapshot.BarsSinceConfirmed);
            Assert.Equal(-0.5, snapshot.DepartureX, precision: 9); // measured on the pullback bar
            Assert.Equal(1.0, snapshot.Settings.DepartureMin, precision: 9);
            Assert.Equal(4, snapshot.Settings.MaxWaitBars);
        }

        [Fact]
        public void the_snapshot_keeps_the_entry_state_after_the_departure_is_spent() {
            // The close row is written minutes after the fill, and the fill clears the departure.
            DepartureTracker tracker = Tracker(confirmBars: 3);

            Observe(tracker, barCount: 3, close: 102.0);
            DepartureSnapshotModel snapshot = tracker.CreateSnapshot();
            tracker.ResetAfterEntry();

            Assert.True(snapshot.IsConfirmed);
            Assert.Equal(3, snapshot.ConfirmCount);
        }

        [Fact]
        public void a_snapshot_from_a_run_with_the_gate_off_is_marked_as_such() {
            DepartureSnapshotModel snapshot = Tracker(min: 0.0).CreateSnapshot();

            Assert.False(snapshot.IsEnabled);
            Assert.False(snapshot.IsConfirmed);
            Assert.Equal(double.NaN, snapshot.DepartureX);
        }

        [Fact]
        public void the_threshold_is_inclusive() {
            DepartureTracker tracker = Tracker(min: 1.0, confirmBars: 2);

            Observe(tracker, barCount: 2, close: 101.0); // exactly 1.0 ATR away

            Assert.True(tracker.IsAllowed(SignalSideModel.Buy));
        }
    }
}
