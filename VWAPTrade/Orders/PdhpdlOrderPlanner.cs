using System;

namespace cAlgo.Robots;

// Turns a signal into a sized, validated order plan. Pure: it depends only on the
// IPdhpdlSymbolModel port and PdhpdlRiskGuard, never on cAlgo, so it is unit tested.
//
// Sizing: volume = riskMoney / riskPrice, rounded to the nearest tradable step, so a
// stop-out loses as close to the risk budget as the step allows. 风险预算与止盈倍数都来自
// 信号命中的那一档价位（见 TradeLevelModel），每一档各用各的。
public class PdhpdlOrderPlanner {
    private readonly IPdhpdlSymbolModel _symbolModel;
    private readonly PdhpdlRiskGuard _riskGuard;

    public PdhpdlOrderPlanner(IPdhpdlSymbolModel symbolModel, PdhpdlRiskGuard riskGuard) {
        _symbolModel = symbolModel;
        _riskGuard = riskGuard;
    }

    public PdhpdlOrderPlanModel CreatePlan(PdhpdlSignalModel signalModel, double accountEquity) {
        PdhpdlOrderPlanModel planModel = new();
        TradeLevelModel level = signalModel.Level;

        PdhpdlTradeDirectionModel directionModel =
            level.Side == SignalSideModel.Buy ? PdhpdlTradeDirectionModel.Long : PdhpdlTradeDirectionModel.Short;
        FillGeometry(signalModel, directionModel, level.TakeProfitR, out double entry, out double stop, out double riskPrice,
            out double takeProfit);
        double stopLossPips = riskPrice / _symbolModel.PipSize;

        if (_riskGuard.TryGetStopLossPipsRejectReason(stopLossPips, out string rejectReason)) {
            planModel.RejectReason = rejectReason;
            return planModel;
        }

        double takeProfitPips = Math.Abs(takeProfit - entry) / _symbolModel.PipSize;
        double riskMoney = _riskGuard.CalculateRiskMoney(accountEquity, level.RiskPct);

        // Volume whose loss at the stop equals the risk budget, snapped to the nearest tradable
        // step. lossPerUnit uses PipValue (account-currency value of a pip), so the budget stays
        // in the account currency. Dividing riskMoney by the raw price distance (the old formula)
        // ignored the quote->deposit currency conversion — e.g. a EUR account trading USD-quoted
        // XAUUSD was sized ~15% too small, so realized losses fell short of the budget.
        double lossPerUnit = stopLossPips * _symbolModel.PipValue;
        double idealVolume = lossPerUnit > 0.0 ? riskMoney / lossPerUnit : 0.0;
        double volume = _symbolModel.NormalizeVolumeInUnits(idealVolume);

        if (TryGetVolumeRejectReason(volume, out rejectReason)) {
            planModel.RejectReason = rejectReason;
            return planModel;
        }

        FillPlan(planModel, directionModel, entry, stop, takeProfit, riskPrice, stopLossPips, takeProfitPips, volume,
            accountEquity, riskMoney);
        return planModel;
    }

    private static void FillGeometry(PdhpdlSignalModel signalModel, PdhpdlTradeDirectionModel directionModel, double takeProfitR,
        out double entry, out double stop, out double riskPrice, out double takeProfit) {
        // The stop sits exactly on the signal's stop-loss price — the pattern-specific level the
        // detector chose — and the entry is the signal bar's close, since every order is a market order.
        stop = signalModel.StopLoss;
        entry = signalModel.Close;

        if (directionModel == PdhpdlTradeDirectionModel.Long) {
            riskPrice = entry - stop;
            takeProfit = entry + takeProfitR * riskPrice;
        } else {
            riskPrice = stop - entry;
            takeProfit = entry - takeProfitR * riskPrice;
        }
    }

    private bool TryGetVolumeRejectReason(double volume, out string rejectReason) {
        rejectReason = "";

        if (volume < _symbolModel.VolumeInUnitsMin) {
            rejectReason = $"Calculated volume is below broker minimum. Volume={volume}, Min={_symbolModel.VolumeInUnitsMin}";
            return true;
        }

        if (volume > _symbolModel.VolumeInUnitsMax) {
            rejectReason = $"Calculated volume is above broker maximum. Volume={volume}, Max={_symbolModel.VolumeInUnitsMax}";
            return true;
        }

        return false;
    }

    private void FillPlan(PdhpdlOrderPlanModel planModel, PdhpdlTradeDirectionModel directionModel,
        double entry, double stop, double takeProfit, double riskPrice, double stopLossPips, double takeProfitPips, double volume,
        double accountEquity, double riskMoney) {
        planModel.IsValid = true;
        planModel.DirectionModel = directionModel;
        planModel.EntryPrice = entry;
        planModel.StopPrice = stop;
        planModel.TakeProfitPrice = takeProfit;
        planModel.RiskPrice = riskPrice;
        planModel.StopLossPips = stopLossPips;
        planModel.TakeProfitPips = takeProfitPips;
        planModel.Lots = volume / _symbolModel.LotSize;
        planModel.VolumeInUnits = volume;
        planModel.AccountEquity = accountEquity;
        planModel.RiskMoney = riskMoney;
        planModel.EstimatedRiskMoney = _symbolModel.AmountRisked(volume, stopLossPips);
    }

}
