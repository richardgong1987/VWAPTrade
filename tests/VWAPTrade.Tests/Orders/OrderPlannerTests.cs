using System;
using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Orders {
    public class OrderPlannerTests {
        // Default symbol is quoted in the account currency: pipSize 0.1 and pipValue 0.1, so one unit
        // loses 0.1 per pip and volume = riskMoney / price distance. Equity 10000 at 1% risks 100.

        [Fact]
        public void sizes_short_to_lose_exactly_the_risk_budget_at_the_stop() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(ShortRequest(entry: 100.0, stopLoss: 102.0, takeProfit: 96.0), accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            Assert.Equal(TradeDirectionModel.Short, plan.DirectionModel);
            Assert.Equal("VWAPTrade_Short1", plan.Label);
            Assert.Equal(100.0, plan.EntryPrice, precision: 6);
            Assert.Equal(102.0, plan.StopPrice, precision: 6);
            Assert.Equal(96.0, plan.TakeProfitPrice, precision: 6);
            Assert.Equal(20.0, plan.StopLossPips, precision: 6);
            Assert.Equal(100.0, plan.RiskMoney, precision: 6);
            // idealVolume = 100 / (20 pips * 0.1) = 50 units = 0.5 lot.
            Assert.Equal(50.0, plan.VolumeInUnits, precision: 6);
            Assert.Equal(0.5, plan.Lots, precision: 6);
            Assert.Equal(100.0, plan.EstimatedRiskMoney, precision: 6);
        }

        [Fact]
        public void rounds_volume_down_so_a_stop_out_never_exceeds_the_budget() {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(ShortRequest(entry: 100.0, stopLoss: 102.15, takeProfit: 96.0), accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            // idealVolume = 100 / (21.5 pips * 0.1) = 46.51; rounding to nearest would give 47 and lose 101.05.
            Assert.Equal(46.0, plan.VolumeInUnits, precision: 6);
            Assert.True(plan.EstimatedRiskMoney <= plan.RiskMoney);
        }

        [Fact]
        public void sizes_in_account_currency_when_pip_value_differs_from_pip_size() {
            // pipValue 0.0855 < pipSize 0.1 models a EUR account trading a USD-quoted instrument: each
            // pip costs less in the account currency, so more volume is needed to risk the same 100.
            OrderPlanner planner = CreatePlanner(pipValue: 0.0855);

            OrderPlanModel plan = planner.CreatePlan(ShortRequest(entry: 100.0, stopLoss: 102.15, takeProfit: 96.0), accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            // lossPerUnit = 21.5 pips * 0.0855 = 1.83825; idealVolume = 54.40 -> 54.
            Assert.Equal(54.0, plan.VolumeInUnits, precision: 6);
            Assert.True(plan.EstimatedRiskMoney <= plan.RiskMoney);
        }

        [Fact]
        public void sizes_long_with_stop_below_entry() {
            OrderPlanner planner = CreatePlanner();
            PendingOrderRequestModel request = ShortRequest(entry: 100.0, stopLoss: 98.0, takeProfit: 104.0);
            request.Direction = TradeDirectionModel.Long;

            OrderPlanModel plan = planner.CreatePlan(request, accountEquity: 10000.0);

            Assert.True(plan.IsValid);
            Assert.Equal(TradeDirectionModel.Long, plan.DirectionModel);
            Assert.Equal(20.0, plan.StopLossPips, precision: 6);
            Assert.Equal(50.0, plan.VolumeInUnits, precision: 6);
        }

        [Theory]
        [InlineData(0.0, 102.0, 96.0)]
        [InlineData(100.0, 0.0, 96.0)]
        [InlineData(100.0, 102.0, 0.0)]
        public void rejects_when_any_price_is_missing(double entry, double stopLoss, double takeProfit) {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(ShortRequest(entry, stopLoss, takeProfit), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("must all be set", plan.RejectReason);
        }

        [Theory]
        [InlineData(99.0, 96.0)]
        [InlineData(100.0, 96.0)]
        [InlineData(102.0, 101.0)]
        public void rejects_short_unless_stop_is_above_entry_and_take_profit_below(double stopLoss, double takeProfit) {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(ShortRequest(entry: 100.0, stopLoss, takeProfit), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("StopLoss > Entry > TakeProfit", plan.RejectReason);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void rejects_when_risk_percentage_is_not_positive(double riskPct) {
            OrderPlanner planner = CreatePlanner();

            OrderPlanModel plan = planner.CreatePlan(ShortRequest(entry: 100.0, stopLoss: 102.0, takeProfit: 96.0, riskPct), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("Risk percentage must be positive", plan.RejectReason);
        }

        [Fact]
        public void rejects_when_sized_volume_is_below_broker_minimum() {
            OrderPlanner planner = CreatePlanner(volumeInUnitsMin: 100.0);

            OrderPlanModel plan = planner.CreatePlan(ShortRequest(entry: 100.0, stopLoss: 102.0, takeProfit: 96.0), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("too small for the broker minimum", plan.RejectReason);
        }

        [Fact]
        public void rejects_when_sized_volume_is_above_broker_maximum() {
            OrderPlanner planner = CreatePlanner(volumeInUnitsMax: 10.0);

            OrderPlanModel plan = planner.CreatePlan(ShortRequest(entry: 100.0, stopLoss: 102.0, takeProfit: 96.0), accountEquity: 10000.0);

            Assert.False(plan.IsValid);
            Assert.Contains("above broker maximum", plan.RejectReason);
        }

        private static OrderPlanner CreatePlanner(double pipValue = 0.1, double volumeInUnitsMin = 1.0,
            double volumeInUnitsMax = 1_000_000.0) {
            return new OrderPlanner(new FakeSymbolModel {
                PipSize = 0.1,
                LotSize = 100.0,
                VolumeInUnitsMin = volumeInUnitsMin,
                VolumeInUnitsMax = volumeInUnitsMax,
                PipValue = pipValue
            });
        }

        private static PendingOrderRequestModel ShortRequest(double entry, double stopLoss, double takeProfit, double riskPct = 1.0) {
            return new PendingOrderRequestModel {
                Label = "VWAPTrade_Short1",
                Direction = TradeDirectionModel.Short,
                EntryPrice = entry,
                StopLossPrice = stopLoss,
                TakeProfitPrice = takeProfit,
                RiskPct = riskPct
            };
        }

        // Deterministic stand-in for a cTrader Symbol: volume step is one whole unit, rounded down
        // (matching RoundingMode.Down in the real adapter). The epsilon absorbs floating-point noise
        // such as 49.999999999 so an exact budget is not rounded one step short.
        private sealed class FakeSymbolModel : ISymbolModel {
            public double PipSize { get; set; }
            public double LotSize { get; set; }
            public double VolumeInUnitsMin { get; set; }
            public double VolumeInUnitsMax { get; set; }
            public double PipValue { get; set; }

            public double NormalizeVolumeInUnits(double volumeInUnits) => Math.Floor(volumeInUnits + 1e-9);
            public double AmountRisked(double volumeInUnits, double stopLossPips) => volumeInUnits * stopLossPips * PipValue;
        }
    }
}
