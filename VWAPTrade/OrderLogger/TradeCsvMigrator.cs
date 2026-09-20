using System;

namespace cAlgo.Robots;

// Upgrades a previously written trades CSV to the current column schema. This is a separate
// concern from TradeCsvLogger: the logger writes today's format, this migrator knows the older
// layouts and rewrites their rows in place. Pure string work, no cAlgo dependency, so it is
// unit tested.
//
// Two shapes of old file are recognised, because those are the ones this cBot ever wrote:
//   - the 22-column layout, which is today's schema plus the dropped 「回撤开仓模式」and「挂单ID」columns;
//   - that same layout followed by indicator-state columns (ATR / DMI / ADX / GapX), which are
//     simply truncated — the leading columns never changed order.
// Anything else is left untouched: an unrecognised row is safer kept than guessed at.
public static class TradeCsvMigrator {
    // 当前 schema 是 27 列：20 列的业务字段，加上末尾 7 列 VWAP 读数（多空 / DailyVWAP /
    // WeeklyVWAP / ATR14_H1 / GapX / SlopeX / 最终结果）。
    private const int CurrentColumnCount = 27;

    // 加 VWAP 读数之前的 20 列。「回撤开仓模式」与「挂单ID」两列在更早的时候已经废弃。
    // 所有可识别的历史行都先收敛到这 20 列，再在末尾补空列凑到当前的 27 列。
    private const int PreviousColumnCount = 20;

    // 去掉末尾那串波动/趋势状态列之后、尚未去掉上面两列的 22 列布局。所有可识别的历史行都先收敛到它，
    // 再由 RemoveDroppedColumns 落到当前的 20 列。
    private const int BusinessColumnCount = 22;
    private const int EntryModeColumnIndex = 3;
    private const int PendingOrderIdColumnIndex = 19;

    // 曾经在末尾带过状态列的历史 schema。列序从未变过，截掉末尾多出来的部分即可回到 22 列布局。
    // 24 列还对应过一种更早、列序不同的布局，所以只在表头对得上时才按状态列处理（见 MigrateRows）。
    //
    // 注意 27 列与当前 schema 撞上了：只有表头也是当前表头时，27 列才算「已经是新格式」，
    // 否则就是下面这个带 DMI 状态列的旧布局。
    private const int ColumnCountWithAtrState = 24;
    private const int ColumnCountWithDmsState = 27;
    private const int ColumnCountWithAdxPreviousState = 28;
    private const int ColumnCountWithGapX = 29;
    private const int ColumnCountWithShortGapX = 30;

    // 加入 DMI 三列之前的表头（24 列），用来把它与同样 24 列的更早布局区分开。
    private const string PreviousHeaderBeforeDmsState =
        "编号,关键位,信号,回撤开仓模式,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,挂单ID,持仓ID,成交ID,ATR_Ratio_H1,PD_Range_ATR";

    // Returns the upgraded lines (header replaced, old rows rewritten), or null when nothing is
    // rewritten — either the file is already current, or it holds rows in a layout this migrator
    // does not recognise. Returning null in that second case leaves the file exactly as it was,
    // rather than stamping the current header onto rows that do not match it.
    public static string[] Upgrade(string[] lines, string currentHeader) {
        bool hasCurrentHeader = lines[0] == currentHeader;

        if (hasCurrentHeader && !NeedsRowMigration(lines))
            return null;

        MigrateRows(lines, hasCurrentHeader, lines[0] == PreviousHeaderBeforeDmsState);

        if (NeedsRowMigration(lines))
            return null;

        lines[0] = currentHeader;
        return lines;
    }

    private static bool NeedsRowMigration(string[] lines) {
        for (int i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            if (lines[i].Split(',').Length != CurrentColumnCount)
                return true;
        }

        return false;
    }

    private static void MigrateRows(string[] lines, bool isCurrentHeader, bool isHeaderBeforeDmsState) {
        for (int i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] columns = lines[i].Split(',');

            if (isCurrentHeader && columns.Length == CurrentColumnCount)
                continue;

            string[] previousLayout = ToPreviousLayout(columns, isHeaderBeforeDmsState);

            // 认不出布局的行原样留着，Upgrade 会因此整份文件都不动。
            if (previousLayout != null)
                lines[i] = string.Join(",", PadToCurrentLayout(previousLayout));
        }
    }

    // 把任意可识别的历史布局收敛到 20 列；认不出来就返回 null。
    private static string[] ToPreviousLayout(string[] columns, bool isHeaderBeforeDmsState) {
        if (columns.Length == PreviousColumnCount)
            return columns;

        if (columns.Length == BusinessColumnCount)
            return RemoveDroppedColumns(columns);

        if (HasTrailingStateColumns(columns.Length, isHeaderBeforeDmsState))
            return RemoveDroppedColumns(TrimToBusinessColumns(columns));

        return null;
    }

    // 旧行没有 VWAP 读数，末尾补空列即可 —— 新列一律加在末尾，前面的列序不用动。
    private static string[] PadToCurrentLayout(string[] previousLayout) {
        string[] padded = new string[CurrentColumnCount];
        Array.Copy(previousLayout, padded, previousLayout.Length);

        for (int i = previousLayout.Length; i < padded.Length; i++) {
            padded[i] = "";
        }

        return padded;
    }

    private static bool HasTrailingStateColumns(int columnCount, bool isHeaderBeforeDmsState) {
        if (columnCount == ColumnCountWithAtrState)
            return isHeaderBeforeDmsState;

        return columnCount == ColumnCountWithDmsState || columnCount == ColumnCountWithAdxPreviousState ||
               columnCount == ColumnCountWithGapX || columnCount == ColumnCountWithShortGapX;
    }

    private static string[] TrimToBusinessColumns(string[] columns) {
        string[] trimmed = new string[BusinessColumnCount];
        Array.Copy(columns, trimmed, BusinessColumnCount);
        return trimmed;
    }

    // 从 22 列布局里删掉两列已废弃的列。先删靠后的那一列，前面那一列的下标才不会被挪动。
    private static string[] RemoveDroppedColumns(string[] businessColumns) {
        return RemoveColumnAt(RemoveColumnAt(businessColumns, PendingOrderIdColumnIndex), EntryModeColumnIndex);
    }

    private static string[] RemoveColumnAt(string[] columns, int index) {
        string[] result = new string[columns.Length - 1];
        Array.Copy(columns, result, index);
        Array.Copy(columns, index + 1, result, index, columns.Length - index - 1);
        return result;
    }
}
