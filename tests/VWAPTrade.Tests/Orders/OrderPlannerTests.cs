using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Orders {
    public class OrderPlannerTests {
        // Default symbol is quoted in the account currency: pipSize 0.1 and pipValue 0.1, so one unit
        // loses 0.1 per pip and volume = riskMoney / price distance. Equity 10000 at 1% risks 100.

        [Fact]
        public void sizes_short_to_lose_the_risk_budget_at_the_stop() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.0), accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(TradeDirectionModel.Short, plan.DirectionModel);
            Assert.Equal(100.0, plan.EntryPrice, precision: 6);
            Assert.Equal(102.0, plan.StopPrice, precision: 6);
            Assert.Equal(20.0, plan.StopLossPips, precision: 6);
            Assert.Equal(100.0, plan.RiskMoney, precision: 6);
            // idealVolume = 100 / (20 pips * 0.1) = 50 units = 0.5 lot.
            Assert.Equal(50.0, plan.VolumeInUnits, precision: 6);
            Assert.Equal(0.5, plan.Lots, precision: 6);
            Assert.Equal(100.0, plan.EstimatedRiskMoney, precision: 6);
        }

        [Fact]
        public void sizes_long_with_stop_below_entry() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Long(entry: 100.0, stopLoss: 98.0), accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(TradeDirectionModel.Long, plan.DirectionModel);
            Assert.Equal(20.0, plan.StopLossPips, precision: 6);
            Assert.Equal(50.0, plan.VolumeInUnits, precision: 6);
        }

        [Fact]
        public void places_the_short_take_profit_at_the_levels_r_multiple_below_entry() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.0, takeProfitR: 3.0),
                accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            // Risk distance is 2.0, so 3R of take profit sits 6.0 below the entry.
            Assert.Equal(2.0, plan.RiskPrice, precision: 6);
            Assert.Equal(94.0, plan.TakeProfitPrice, precision: 6);
            Assert.Equal(60.0, plan.TakeProfitPips, precision: 6);
        }

        [Fact]
        public void places_the_long_take_profit_at_the_levels_r_multiple_above_entry() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Long(entry: 100.0, stopLoss: 98.0, takeProfitR: 3.0),
                accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(106.0, plan.TakeProfitPrice, precision: 6);
            Assert.Equal(60.0, plan.TakeProfitPips, precision: 6);
        }

        [Fact]
        public void budgets_risk_from_the_hit_levels_own_risk_percentage() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.0, riskPct: 2.0),
                accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(200.0, plan.RiskMoney, precision: 6);
            Assert.Equal(100.0, plan.VolumeInUnits, precision: 6);
        }

        [Fact]
        public void rounds_volume_up_to_the_step_when_the_ideal_size_is_past_the_halfway_point() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.15), accountEquity: 10000.0);

            // idealVolume = 100 / (21.5 pips * 0.1) = 46.51, so the nearest step is 47.
            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(47.0, plan.VolumeInUnits, precision: 6);
        }

        [Fact]
        public void rounds_volume_down_to_the_step_when_the_ideal_size_is_short_of_the_halfway_point() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.2), accountEquity: 10000.0);

            // idealVolume = 100 / (22 pips * 0.1) = 45.45, so the nearest step is 45.
            Assert.True(plan.IsValid, plan.RejectReason);
            Assert.Equal(45.0, plan.VolumeInUnits, precision: 6);
            Assert.True(plan.EstimatedRiskMoney <= plan.RiskMoney);
        }

        [Fact]
        public void sizes_in_account_currency_when_pip_value_differs_from_pip_size() {
            // pipValue 0.0855 < pipSize 0.1 models a EUR account trading a USD-quoted instrument: each
            // pip costs less in the account currency, so more volume is needed to risk the same 100.
            OrderPlanner planner = CreatePlanner(pipValue: 0.0855);

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.15), accountEquity: 10000.0);

            Assert.True(plan.IsValid, plan.RejectReason);
            // lossPerUnit = 21.5 pips * 0.0855 = 1.83825; idealVolume = 54.40 -> 54.
            Assert.Equal(54.0, plan.VolumeInUnits, precision: 6);
            Assert.True(plan.EstimatedRiskMoney <= plan.RiskMoney);
        }

        [Fact]
        public void rejects_a_short_whose_stop_is_not_above_the_entry() {
            OrderPlanner planner = CreatePlanner();

            // A stop on the wrong side yields a negative risk distance, which cannot be sized.
            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 98.0), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("Stop loss pips is not positive", plan.RejectReason);
        }

        [Fact]
        public void rejects_when_the_stop_sits_on_the_entry() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 100.0), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("Stop loss pips is not positive", plan.RejectReason);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void rejects_when_the_level_has_no_risk_budget(double riskPct) {
            OrderPlanner planner = CreatePlanner();

            // No risk budget sizes to zero volume, which the broker minimum then rejects.
            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.0, riskPct), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("below broker minimum", plan.RejectReason);
        }

        [Fact]
        public void rejects_when_the_account_has_no_equity() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.0), accountEquity: 0.0);

            Assert.False(plan.IsValid);
            Assert.Contains("below broker minimum", plan.RejectReason);
        }

        [Fact]
        public void rejects_when_sized_volume_is_below_broker_minimum() {
            OrderPlanner planner = CreatePlanner(volumeInUnitsMin: 100.0);

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.0), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("below broker minimum", plan.RejectReason);
        }

        [Fact]
        public void rejects_when_sized_volume_is_above_broker_maximum() {
            OrderPlanner planner = CreatePlanner(volumeInUnitsMax: 10.0);

            OrderPlanModel plan = planner.CreatePlan(TestSignal.Short(entry: 100.0, stopLoss: 102.0), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("above broker maximum", plan.RejectReason);
        }

        private static OrderPlanner CreatePlanner(double pipValue = 0.1, double volumeInUnitsMin = 1.0,
            double volumeInUnitsMax = 1_000_000.0) {
            FakeSymbolModel symbol = new() {
                PipSize = 0.1,
                LotSize = 100.0,
                VolumeInUnitsMin = volumeInUnitsMin,
                VolumeInUnitsMax = volumeInUnitsMax,
                PipValue = pipValue
            };

            return new OrderPlanner(symbol, new RiskGuard());
        }
    }
}
