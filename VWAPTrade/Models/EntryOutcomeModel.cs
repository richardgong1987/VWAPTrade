namespace cAlgo.Robots;

// What became of one signal: either an order went out, or the first gate that stopped it.
// Kept apart from SignalModel, which only says what the bar showed.
public class EntryOutcomeModel {
    private EntryOutcomeModel(EntryGateModel blockedBy, string detail, int? positionId) {
        BlockedBy = blockedBy;
        Detail = detail;
        PositionId = positionId;
    }

    public static EntryOutcomeModel Ordered(int positionId) => new(EntryGateModel.None, "", positionId);

    public static EntryOutcomeModel Blocked(EntryGateModel gate, string detail = "") => new(gate, detail ?? "", null);

    // None when the order went out.
    public EntryGateModel BlockedBy { get; }

    // Extra facts for the gates that have them: the planner's reject reason, the broker's error.
    public string Detail { get; }

    // The position the order opened; the same ID as the trade CSV's 持仓ID.
    public int? PositionId { get; }

    public bool IsOrdered => PositionId.HasValue;
}
