using System;
using System.Collections.Generic;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Vwap {
    // Walks a week of 5-minute bars through the same steps VwapSeries takes, so the session rules
    // and the accumulator are exercised together. The unit tests check each rule in isolation; this
    // catches the case where both are individually right and still produce a wrong chart — which is
    // how the weekly VWAP once ended up resetting every morning and matching the daily one exactly.
    public class VwapSeriesWalkTests {
        [Fact]
        public void the_weekly_vwap_stops_matching_the_daily_one_after_the_first_session() {
            List<VwapSampleModel> week = WalkOneWeek();

            // Tuesday is the one session where both legitimately coincide: the week and the day open
            // at the same moment, so they accumulate the same bars.
            Assert.All(BarsOfSessionStartingOn(week, dayOfMonth: 22), sample => Assert.Equal(sample.Daily, sample.Weekly, precision: 9));

            // From Wednesday on, the day restarts while the week keeps running, so they must differ.
            foreach (int dayOfMonth in new[] { 23, 24, 25 }) {
                List<VwapSampleModel> session = BarsOfSessionStartingOn(week, dayOfMonth);
                VwapSampleModel last = session[session.Count - 1];

                Assert.NotEqual(last.Daily, last.Weekly, precision: 6);
            }
        }

        [Fact]
        public void exactly_four_day_sessions_and_one_week_open_in_a_week() {
            List<VwapSampleModel> week = WalkOneWeek();

            Assert.Equal(4, week.FindAll(sample => sample.IsDailySessionStart).Count);
            Assert.Equal(1, week.FindAll(sample => sample.IsWeeklySessionStart).Count);
        }

        [Fact]
        public void monday_and_the_morning_gaps_produce_no_values() {
            List<VwapSampleModel> week = WalkOneWeek();

            foreach (VwapSampleModel sample in week) {
                bool hasValue = !double.IsNaN(sample.Daily);

                Assert.Equal(TradingSession.IsInSession(sample.OpenTime), hasValue);
            }
        }

        // Monday 00:00 through Saturday 12:00 in 5-minute bars, priced with a small sawtooth so the
        // daily and weekly averages genuinely drift apart.
        private static List<VwapSampleModel> WalkOneWeek() {
            List<VwapSampleModel> samples = new();
            VwapCalculator calculator = new();
            DateTime cursor = new(2026, 9, 21, 0, 0, 0); // Monday
            DateTime end = new(2026, 9, 26, 12, 0, 0); // Saturday noon
            DateTime? previous = null;
            double price = 4000.0;

            for (int i = 0; cursor < end; i++) {
                price += i % 7 - 3;

                VwapSampleModel sample = TradingSession.IsInSession(cursor)
                    ? calculator.Append(price, volume: 100.0, TradingSession.IsNewDaySession(cursor, previous),
                        TradingSession.IsNewWeekSession(cursor, previous))
                    : calculator.AppendOutsideSession();

                sample.OpenTime = cursor;
                samples.Add(sample);
                previous = cursor;
                cursor = cursor.AddMinutes(5);
            }

            return samples;
        }

        private static List<VwapSampleModel> BarsOfSessionStartingOn(List<VwapSampleModel> week, int dayOfMonth) {
            DateTime sessionStart = new(2026, 9, dayOfMonth, 10, 0, 0);

            return week.FindAll(sample => TradingSession.GetDaySessionStart(sample.OpenTime) == sessionStart);
        }
    }
}
