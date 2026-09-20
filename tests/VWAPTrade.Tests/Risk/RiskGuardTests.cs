using System;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Risk {
    // New orders are only opened inside a trading session. 2026-09-21 is a Monday.
    public class RiskGuardTests {
        private static readonly DateTime Monday = new(2026, 9, 21);
        private static readonly DateTime Tuesday = new(2026, 9, 22);
        private static readonly DateTime Wednesday = new(2026, 9, 23);
        private static readonly DateTime Saturday = new(2026, 9, 26);

        [Fact]
        public void orders_are_allowed_inside_a_session() {
            RiskGuard guard = new();

            Assert.False(guard.ShouldBlockNewOrder(At(Tuesday, 10, 0)));
            Assert.False(guard.ShouldBlockNewOrder(At(Wednesday, 3, 0)));
        }

        [Fact]
        public void orders_are_blocked_in_the_morning_gap() {
            RiskGuard guard = new();

            Assert.True(guard.ShouldBlockNewOrder(At(Wednesday, 6, 0)));
            Assert.True(guard.ShouldBlockNewOrder(At(Wednesday, 9, 59)));
        }

        [Fact]
        public void orders_are_blocked_on_monday_and_over_the_weekend() {
            RiskGuard guard = new();

            Assert.True(guard.ShouldBlockNewOrder(At(Monday, 15, 0)));
            Assert.True(guard.ShouldBlockNewOrder(At(Saturday, 10, 0)));
        }

        [Fact]
        public void risk_money_is_a_percentage_of_equity() {
            RiskGuard guard = new();

            Assert.Equal(100.0, guard.CalculateRiskMoney(equity: 10000.0, riskPct: 1.0), precision: 6);
        }

        [Theory]
        [InlineData(0.0, 1.0)]
        [InlineData(10000.0, 0.0)]
        [InlineData(-1.0, 1.0)]
        public void a_missing_equity_or_risk_percentage_risks_nothing(double equity, double riskPct) {
            RiskGuard guard = new();

            Assert.Equal(0.0, guard.CalculateRiskMoney(equity, riskPct), precision: 6);
        }

        [Fact]
        public void a_stop_distance_of_zero_is_rejected() {
            RiskGuard guard = new();

            Assert.True(guard.TryGetStopLossPipsRejectReason(0.0, out string reason));
            Assert.Contains("not positive", reason);
        }

        private static DateTime At(DateTime day, int hour, int minute) => day.Date.AddHours(hour).AddMinutes(minute);
    }
}
