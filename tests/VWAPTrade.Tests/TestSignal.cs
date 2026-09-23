using System;
using cAlgo.Robots;

namespace VWAPTrade.Tests {
    // Builds the signal the planner consumes: the level it touched plus the closed bar that hit it.
    // Entry is the bar close, so tests only ever state the entry and the stop; risk and take-profit
    // come from the planner's settings (see TestSettings).
    internal static class TestSignal {
        public static SignalModel Short(double entry, double stopLoss) => ForSide(SignalSideModel.Sell, entry, stopLoss);

        public static SignalModel Long(double entry, double stopLoss) => ForSide(SignalSideModel.Buy, entry, stopLoss);

        private static SignalModel ForSide(SignalSideModel side, double entry, double stopLoss) {
            return new SignalModel {
                Level = new TradeLevelModel("VWAP", side, price: entry),
                Label = side == SignalSideModel.Sell ? "S_Pin_1" : "L_Pin_1",
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
