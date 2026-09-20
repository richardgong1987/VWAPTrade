using System;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // When the VWAP restarts. Days are server-time calendar days; weeks start on Monday, so the
    // weekend gap between Friday and Sunday does not open a new week but Monday's first bar does.
    public class VwapSessionTests {
        [Fact]
        public void bars_in_the_same_day_do_not_start_a_new_day() {
            DateTime previous = new(2026, 9, 17, 9, 0, 0);
            DateTime current = new(2026, 9, 17, 23, 55, 0);

            Assert.False(VwapCalculator.IsNewDay(current, previous));
        }

        [Fact]
        public void the_first_bar_after_midnight_starts_a_new_day() {
            DateTime previous = new(2026, 9, 17, 23, 55, 0);
            DateTime current = new(2026, 9, 18, 0, 0, 0);

            Assert.True(VwapCalculator.IsNewDay(current, previous));
        }

        [Fact]
        public void a_new_day_inside_the_week_does_not_start_a_new_week() {
            // Thursday into Friday.
            DateTime previous = new(2026, 9, 17, 23, 55, 0);
            DateTime current = new(2026, 9, 18, 0, 0, 0);

            Assert.True(VwapCalculator.IsNewDay(current, previous));
            Assert.False(VwapCalculator.IsNewWeek(current, previous));
        }

        [Fact]
        public void the_weekend_gap_stays_in_the_same_week() {
            // Friday's last bar into Sunday's first bar — the broker week rolls over on Monday.
            DateTime friday = new(2026, 9, 18, 23, 55, 0);
            DateTime sunday = new(2026, 9, 20, 22, 0, 0);

            Assert.Equal(DayOfWeek.Friday, friday.DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);
            Assert.False(VwapCalculator.IsNewWeek(sunday, friday));
        }

        [Fact]
        public void mondays_first_bar_starts_a_new_week() {
            DateTime sunday = new(2026, 9, 20, 22, 0, 0);
            DateTime monday = new(2026, 9, 21, 0, 0, 0);

            Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
            Assert.True(VwapCalculator.IsNewWeek(monday, sunday));
        }

        [Theory]
        [InlineData(2026, 9, 21)] // Monday
        [InlineData(2026, 9, 23)] // Wednesday
        [InlineData(2026, 9, 27)] // Sunday — still the week that began on the 21st
        public void every_day_from_monday_to_sunday_maps_to_the_same_week_start(int year, int month, int day) {
            DateTime weekStart = VwapCalculator.GetWeekStart(new DateTime(year, month, day, 13, 30, 0));

            Assert.Equal(new DateTime(2026, 9, 21), weekStart);
        }

        [Fact]
        public void the_week_start_drops_the_time_of_day() {
            DateTime weekStart = VwapCalculator.GetWeekStart(new DateTime(2026, 9, 23, 17, 45, 30));

            Assert.Equal(new DateTime(2026, 9, 21, 0, 0, 0), weekStart);
        }
    }
}
