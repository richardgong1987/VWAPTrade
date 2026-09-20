using System;
using cAlgo.Robots;

namespace VWAPTrade.Tests {
    // Builds the signal the planner consumes: a configured level plus the closed bar that hit it.
    // Entry is the bar close and the take profit is derived from the level's R multiple, so tests
    // only ever state the entry, the stop and the level's risk settings.
    internal static class TestSignal {
        public static SignalModel Short(double entry, double stopLoss, double riskPct = 1.0, double takeProfitR = 2.0) =>
            ForSide(SignalSideModel.Sell, "Short1", entry, stopLoss, riskPct, takeProfitR);

        public static SignalModel Long(double entry, double stopLoss, double riskPct = 1.0, double takeProfitR = 2.0) =>
            ForSide(SignalSideModel.Buy, "Long1", entry, stopLoss, riskPct, takeProfitR);

        private static SignalModel ForSide(SignalSideModel side, string levelName, double entry, double stopLoss,
            double riskPct, double takeProfitR) {
            return new SignalModel {
                Level = new TradeLevelModel(levelName, side, price: entry, riskPct: riskPct, takeProfitR: takeProfitR),
                Label = side == SignalSideModel.Sell ? "S_Pin_1" : "B_Pin_1",
                Close = entry,
                StopLoss = stopLoss,
                High = Math.Max(entry, stopLoss),
                Low = Math.Min(entry, stopLoss),
                BarIndex = 42,
                BarTime = new DateTime(2026, 9, 18, 13, 0, 0, DateTimeKind.Utc)
            };
        }
    }
}
