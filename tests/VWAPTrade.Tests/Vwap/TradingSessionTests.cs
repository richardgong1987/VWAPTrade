using System;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // When orders may be opened, in Japan time (the robot sets TimeZones.TokyoStandardTime).
    //   A session runs 10:30 -> 06:00 the next morning, on Tuesday through Friday.
    //   A week runs Tuesday 10:30 -> Saturday 06:00.
    // This is only the order gate. The VWAP's own periods start earlier — see VwapPeriodTests.
    // 2026-09-21 is a Monday, so the week under test is 21st Mon ... 27th Sun.
    public class TradingSessionTests {
        private static readonly DateTime Monday = new(2026, 9, 21);
        private static readonly DateTime Tuesday = new(2026, 9, 22);
        private static readonly DateTime Wednesday = new(2026, 9, 23);
        private static readonly DateTime Friday = new(2026, 9, 25);
        private static readonly DateTime Saturday = new(2026, 9, 26);
        private static readonly DateTime Sunday = new(2026, 9, 27);

        [Fact]
        public void the_session_opens_at_half_past_ten() {
            Assert.False(TradingSession.IsInSession(At(Tuesday, 10, 29)));
            Assert.True(TradingSession.IsInSession(At(Tuesday, 10, 30)));
        }

        [Fact]
        public void the_session_runs_past_midnight_and_ends_at_six() {
            Assert.True(TradingSession.IsInSession(At(Wednesday, 5, 59)));
            Assert.False(TradingSession.IsInSession(At(Wednesday, 6, 0)));
        }

        [Fact]
        public void the_morning_gap_between_six_and_half_past_ten_is_outside_any_session() {
            Assert.False(TradingSession.IsInSession(At(Wednesday, 7, 30)));
            Assert.False(TradingSession.IsInSession(At(Wednesday, 10, 29)));
        }

        [Fact]
        public void monday_never_trades() {
            Assert.False(TradingSession.IsInSession(At(Monday, 10, 30)));
            Assert.False(TradingSession.IsInSession(At(Monday, 15, 0)));
            Assert.False(TradingSession.IsInSession(At(Monday, 23, 59)));
        }

        [Fact]
        public void tuesday_before_six_belongs_to_mondays_session_so_it_does_not_trade() {
            // Those hours are the tail of the Monday 10:30 session, which is excluded, and they
            // also fall before the week opens at Tuesday 10:30.
            Assert.False(TradingSession.IsInSession(At(Tuesday, 3, 0)));
        }

        [Fact]
        public void the_week_ends_on_saturday_morning() {
            // Saturday before 06:00 is still Friday's session.
            Assert.True(TradingSession.IsInSession(At(Saturday, 5, 59)));
            Assert.False(TradingSession.IsInSession(At(Saturday, 6, 0)));
            Assert.False(TradingSession.IsInSession(At(Saturday, 12, 0)));
            Assert.False(TradingSession.IsInSession(At(Sunday, 12, 0)));
        }

        [Fact]
        public void every_bar_of_one_session_reports_the_same_session_start() {
            DateTime expected = At(Tuesday, 10, 30);

            Assert.Equal(expected, TradingSession.GetDaySessionStart(At(Tuesday, 10, 30)));
            Assert.Equal(expected, TradingSession.GetDaySessionStart(At(Tuesday, 23, 55)));
            Assert.Equal(expected, TradingSession.GetDaySessionStart(At(Wednesday, 0, 0)));
            Assert.Equal(expected, TradingSession.GetDaySessionStart(At(Wednesday, 5, 55)));
        }

        [Fact]
        public void the_gap_between_two_days_still_belongs_to_the_week() {
            // A week is one continuous span Tuesday 10:30 -> Saturday 06:00; the daily 06:00-10:30
            // gaps sit inside it.
            DateTime expected = At(Tuesday, 10, 30);

            Assert.Equal(expected, TradingSession.GetWeekSessionStart(At(Wednesday, 7, 30)));
            Assert.Equal(expected, TradingSession.GetWeekSessionStart(At(Friday, 8, 0)));
        }

        [Fact]
        public void times_before_the_week_opens_belong_to_no_week() {
            Assert.Null(TradingSession.GetWeekSessionStart(At(Tuesday, 10, 29)));
            Assert.Null(TradingSession.GetWeekSessionStart(At(Monday, 12, 0)));
            Assert.Null(TradingSession.GetWeekSessionStart(At(Saturday, 6, 0)));
            Assert.Null(TradingSession.GetWeekSessionStart(At(Sunday, 12, 0)));
        }

        [Fact]
        public void every_session_of_the_week_maps_back_to_tuesday_half_past_ten() {
            DateTime expected = At(Tuesday, 10, 30);

            Assert.Equal(expected, TradingSession.GetWeekSessionStart(At(Tuesday, 10, 30)));
            Assert.Equal(expected, TradingSession.GetWeekSessionStart(At(Wednesday, 14, 0)));
            Assert.Equal(expected, TradingSession.GetWeekSessionStart(At(Friday, 23, 0)));
            Assert.Equal(expected, TradingSession.GetWeekSessionStart(At(Saturday, 5, 59)));
        }

        [Fact]
        public void times_outside_a_session_have_no_session_start() {
            Assert.Null(TradingSession.GetDaySessionStart(At(Wednesday, 8, 0)));
            Assert.Null(TradingSession.GetWeekSessionStart(At(Monday, 12, 0)));
        }

        private static DateTime At(DateTime day, int hour, int minute) => day.Date.AddHours(hour).AddMinutes(minute);
    }
}
