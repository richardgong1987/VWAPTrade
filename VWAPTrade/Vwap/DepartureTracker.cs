namespace cAlgo.Robots;

// The Departure gate (V2 section 6): price must first genuinely leave the daily VWAP before a
// pullback pattern on that line may be traded. Without it the bot also takes the trades that
// just chop along the yellow line.
//
//   DepartureX = direction × (Close − 日VWAP) / SelectedATR      (+1 long, −1 short)
//
// The structure direction comes from 日VWAP vs 周VWAP alone, never from the close: the pullback
// this gate is waiting for pushes the close back through the daily VWAP, so a direction that
// included the close would cancel itself exactly when the setup is forming.
//
// Once ConfirmBars consecutive closes clear DepartureMin the departure is LOCKED — a pullback
// shrinking DepartureX must not undo it (section 6.2 #6). It is cleared only by an entry, a new
// daily VWAP period, the structure flipping or failing, or MaxWaitBars running out (section 6.3).
//
// DepartureMin = 0 switches the gate off completely, including the "unusable value blocks the
// trade" rule — a gate that is off must never become a new reason to skip a trade.
//
// One direction is tracked at a time, because the structure only points one way at a time.
// Pure state machine, no cAlgo dependency, unit tested.
public class DepartureTracker {
    private readonly DepartureSettingsModel _settings;

    private SignalSideModel _structureSide = SignalSideModel.None;
    private int _confirmCount;
    private bool _departed;
    private int _barsSinceConfirmed;
    private double _lastDepartureX = double.NaN;

    public DepartureTracker(DepartureSettingsModel settings) {
        _settings = settings;
    }

    public bool IsEnabled => _settings != null && _settings.IsEnabled;

    // Feed every closed bar, oldest first, exactly once.
    public void Observe(DepartureBarModel bar) {
        if (!IsEnabled || bar == null)
            return;

        SignalSideModel side = ResolveStructureSide(bar.DailyVwap, bar.WeeklyVwap);

        // Structure flipped, broke, or the daily VWAP restarted: the old departure is void.
        if (side != _structureSide || bar.IsDayPeriodStart)
            ResetProgress();

        _structureSide = side;

        if (side == SignalSideModel.None) {
            _lastDepartureX = double.NaN;
            return;
        }

        // Measured on every bar, including the locked ones: the distance the CSV reports for a
        // trade is the one on its entry bar, which is a pullback bar.
        _lastDepartureX = ComputeDepartureX(bar, side);

        if (_departed) {
            _barsSinceConfirmed++;

            if (!HasWaitedTooLong())
                return; // Locked: the pullback is what we are waiting for, not a reason to clear.

            ResetProgress(); // Waited too long; this bar starts counting a fresh departure.
        }

        CountDeparture(_lastDepartureX);
    }

    // The state on the bar just observed, copied out for the trade CSV.
    public DepartureSnapshotModel CreateSnapshot() {
        return new DepartureSnapshotModel {
            Settings = _settings,
            DepartureX = _lastDepartureX,
            IsConfirmed = _departed,
            ConfirmCount = _confirmCount,
            BarsSinceConfirmed = _barsSinceConfirmed
        };
    }

    // The last gate before the pattern check: the signal must run with the departed structure.
    public bool IsAllowed(SignalSideModel side) {
        if (!IsEnabled)
            return true;

        return _departed && side != SignalSideModel.None && side == _structureSide;
    }

    // After a fill the next trade must earn its own departure (section 6.3).
    public void ResetAfterEntry() {
        ResetProgress();
    }

    private void CountDeparture(double departureX) {
        // The gate is on, so a value we cannot compute (ATR missing) counts as "not away" —
        // the same rule the Gap and Slope gates follow.
        bool isAway = VwapStrongMetrics.IsUsable(departureX) && departureX >= _settings.DepartureMin;
        _confirmCount = isAway ? _confirmCount + 1 : 0;

        if (_confirmCount < _settings.ConfirmBars)
            return;

        _departed = true;
        _barsSinceConfirmed = 0;
    }

    // MaxWaitBars counts bars after the confirming bar, so MaxWaitBars = 3 leaves the next three
    // bars to pull back. 0 = no limit.
    private bool HasWaitedTooLong() {
        return _settings.MaxWaitBars > 0 && _barsSinceConfirmed > _settings.MaxWaitBars;
    }

    private static double ComputeDepartureX(DepartureBarModel bar, SignalSideModel side) {
        if (!VwapStrongMetrics.IsUsable(bar.Close) || !VwapStrongMetrics.IsUsable(bar.DailyVwap) ||
            !VwapStrongMetrics.IsUsable(bar.SelectedAtr) || bar.SelectedAtr <= 0.0)
            return double.NaN;

        double direction = side == SignalSideModel.Buy ? 1.0 : -1.0;

        return direction * (bar.Close - bar.DailyVwap) / bar.SelectedAtr;
    }

    // Daily against weekly only — see the note at the top about leaving the close out.
    private static SignalSideModel ResolveStructureSide(double dailyVwap, double weeklyVwap) {
        if (!VwapStrongMetrics.IsUsable(dailyVwap) || !VwapStrongMetrics.IsUsable(weeklyVwap))
            return SignalSideModel.None;

        if (dailyVwap > weeklyVwap)
            return SignalSideModel.Buy;

        if (dailyVwap < weeklyVwap)
            return SignalSideModel.Sell;

        return SignalSideModel.None;
    }

    private void ResetProgress() {
        _confirmCount = 0;
        _departed = false;
        _barsSinceConfirmed = 0;
    }
}
