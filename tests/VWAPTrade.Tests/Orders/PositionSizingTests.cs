using System;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Orders {
    // Checks the lot size OrderPlanner computes for realistic broker symbol settings.
    // Sizing rule: riskMoney = equity × RiskPct%, units = riskMoney / (stop pips × pip value per unit),
    // rounded down to the volume step, lots = units / lot size.
    public class PositionSizingTests {
        [Fact]
        public void xauusd_one_percent_with_a_10_dollar_stop_is_a_tenth_of_a_lot() {
            OrderPlanner planner = new(Xauusd(pipValuePerUnit: 0.01));

            // riskMoney = 10000 × 1% = 100 USD. Stop 2410 − 2400 = 10 USD = 1000 pips.
            // One unit (1 oz) loses 1000 × 0.01 = 10 USD, so 100 / 10 = 10 units = 0.10 lot.
            OrderPlanModel plan = planner.CreatePlan(Short(entry: 2400.0, stopLoss: 2410.0, takeProfit: 2380.0, riskPct: 1.0),
                accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(1000.0, plan.StopLossPips, precision: 6);
            Assert.Equal(10.0, plan.VolumeInUnits, precision: 6);
            Assert.Equal(0.10, plan.Lots, precision: 6);
            Assert.Equal(100.0, plan.EstimatedRiskMoney, precision: 6);
        }

        [Fact]
        public void xauusd_rounds_down_to_the_unit_step_so_the_loss_stays_under_budget() {
            OrderPlanner planner = new(Xauusd(pipValuePerUnit: 0.01));

            // riskMoney = 25000 × 2% = 500 USD. Stop 2358.00 − 2350.50 = 7.5 USD = 750 pips.
            // One unit loses 7.5 USD, so 500 / 7.5 = 66.67 units -> rounded down to 66 = 0.66 lot.
            // Loss at the stop = 66 × 7.5 = 495 USD; 67 units would lose 502.5 and break the budget.
            OrderPlanModel plan = planner.CreatePlan(Short(entry: 2350.50, stopLoss: 2358.00, takeProfit: 2330.0, riskPct: 2.0),
                accountEquity: 25000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(66.0, plan.VolumeInUnits, precision: 6);
            Assert.Equal(0.66, plan.Lots, precision: 6);
            Assert.Equal(495.0, plan.EstimatedRiskMoney, precision: 6);
            Assert.True(plan.EstimatedRiskMoney <= plan.RiskMoney);
        }

        [Fact]
        public void eurusd_one_percent_with_a_50_pip_stop_is_two_tenths_of_a_lot() {
            OrderPlanner planner = new(Eurusd());

            // riskMoney = 10000 × 1% = 100 USD. Stop 1.0900 − 1.0850 = 50 pips.
            // One unit loses 50 × 0.0001 = 0.005 USD, so 100 / 0.005 = 20000 units = 0.20 lot.
            OrderPlanModel plan = planner.CreatePlan(Short(entry: 1.0850, stopLoss: 1.0900, takeProfit: 1.0750, riskPct: 1.0),
                accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(50.0, plan.StopLossPips, precision: 6);
            Assert.Equal(20000.0, plan.VolumeInUnits, precision: 6);
            Assert.Equal(0.20, plan.Lots, precision: 6);
            Assert.Equal(100.0, plan.EstimatedRiskMoney, precision: 6);
        }

        [Fact]
        public void eurusd_rounds_down_to_the_one_thousand_unit_step() {
            OrderPlanner planner = new(Eurusd());

            // riskMoney = 100 USD. Stop 1.0880 − 1.0850 = 30 pips; one unit loses 0.003 USD.
            // 100 / 0.003 = 33333 units -> rounded down to the 1000-unit step = 33000 = 0.33 lot.
            // Loss at the stop = 33000 × 0.003 = 99 USD.
            OrderPlanModel plan = planner.CreatePlan(Short(entry: 1.0850, stopLoss: 1.0880, takeProfit: 1.0790, riskPct: 1.0),
                accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(33000.0, plan.VolumeInUnits, precision: 6);
            Assert.Equal(0.33, plan.Lots, precision: 6);
            Assert.Equal(99.0, plan.EstimatedRiskMoney, precision: 6);
        }

        [Fact]
        public void eur_account_on_usd_quoted_gold_sizes_in_account_currency() {
            // 1 USD = 0.92 EUR, so one pip on one ounce is worth 0.01 × 0.92 = 0.0092 EUR.
            OrderPlanner planner = new(Xauusd(pipValuePerUnit: 0.0092));

            // riskMoney = 10000 EUR × 1% = 100 EUR. Stop 2405 − 2400 = 5 USD = 500 pips.
            // One unit loses 500 × 0.0092 = 4.6 EUR, so 100 / 4.6 = 21.74 -> 21 units = 0.21 lot.
            // Ignoring the conversion (100 / 5 USD) would give 20 units and under-risk the account.
            OrderPlanModel plan = planner.CreatePlan(Short(entry: 2400.0, stopLoss: 2405.0, takeProfit: 2390.0, riskPct: 1.0),
                accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(21.0, plan.VolumeInUnits, precision: 6);
            Assert.Equal(0.21, plan.Lots, precision: 6);
            Assert.Equal(96.6, plan.EstimatedRiskMoney, precision: 6);
        }

        private static FakeSymbolModel Xauusd(double pipValuePerUnit) {
            return new FakeSymbolModel {
                PipSize = 0.01, PipValue = pipValuePerUnit, LotSize = 100.0, VolumeStep = 1.0,
                VolumeInUnitsMin = 1.0, VolumeInUnitsMax = 1_000_000.0
            };
        }

        private static FakeSymbolModel Eurusd() {
            return new FakeSymbolModel {
                PipSize = 0.0001, PipValue = 0.0001, LotSize = 100_000.0, VolumeStep = 1000.0,
                VolumeInUnitsMin = 1000.0, VolumeInUnitsMax = 100_000_000.0
            };
        }

        private static PendingOrderRequestModel Short(double entry, double stopLoss, double takeProfit, double riskPct) {
            return new PendingOrderRequestModel {
                Label = "VWAPTrade_Short1",
                Direction = TradeDirectionModel.Short,
                EntryPrice = entry,
                StopLossPrice = stopLoss,
                TakeProfitPrice = takeProfit,
                RiskPct = riskPct
            };
        }

        // Stand-in for a cTrader Symbol with a broker volume step, rounded down like
        // RoundingMode.Down in CAlgoSymbolModel. The epsilon absorbs floating-point noise such as
        // 19999.9999999 so an exact budget is not rounded one step short.
        private sealed class FakeSymbolModel : ISymbolModel {
            public double PipSize { get; init; }
            public double PipValue { get; init; }
            public double LotSize { get; init; }
            public double VolumeStep { get; init; }
            public double VolumeInUnitsMin { get; init; }
            public double VolumeInUnitsMax { get; init; }

            public double NormalizeVolumeInUnits(double volumeInUnits) =>
                Math.Floor(volumeInUnits / VolumeStep + 1e-9) * VolumeStep;

            public double AmountRisked(double volumeInUnits, double stopLossPips) => volumeInUnits * stopLossPips * PipValue;
        }
    }
}
