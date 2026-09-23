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
    // The current width follows TradeCsvColumns, so adding a column never needs an edit here.
    private static int CurrentColumnCount => TradeCsvColumns.Count;

    // 再往前：只有 20 列业务字段。「回撤开仓模式」与「挂单ID」两列在更早的时候已经废弃。
    // 所有可识别的历史行都先收敛到这 20 列，再在末尾补空列凑到当前的列数。
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

    // 发布过、但不是当前表头前缀的旧表头。第 24 列 ATR14_H1 后来改名成 ATR14（周期可以选了），
    // 所以带 VWAP 读数的那三版表头都断了前缀关系，只能逐字列出来认。列序没变、数值含义也没变，
    // 认出来之后照样在末尾补空列即可。
    private const string HeaderBeforeAtrRename20 =
        "编号,关键位,信号,备注,交易品种,时间周期,入场时间,入场价格,平仓价格,止损价格,止盈价格,风险价格距离,下单数量,平仓原因,开仓账户权益,平仓账户权益,平仓盈亏,平仓时间,持仓ID,成交ID";

    private const string HeaderBeforeAtrRename27 =
        HeaderBeforeAtrRename20 + ",多空,DailyVWAP,WeeklyVWAP,ATR14_H1,GapX,SlopeX,最终结果";

    private const string HeaderBeforeAtrRename30 = HeaderBeforeAtrRename27 + ",DailyVWAP_Lookback,ResultR,GapChangeX";

    private const string HeaderBeforeAtrRename31 = HeaderBeforeAtrRename30 + ",WeeklyVWAP_Lookback";

    private static readonly string[] RenamedEarlierHeaders = {
        HeaderBeforeAtrRename27, HeaderBeforeAtrRename30, HeaderBeforeAtrRename31
    };

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

        MigrateRows(lines, hasCurrentHeader, lines[0]);

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

    private static void MigrateRows(string[] lines, bool isCurrentHeader, string header) {
        for (int i = 1; i < lines.Length; i++) {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] columns = lines[i].Split(',');

            if (isCurrentHeader && columns.Length == CurrentColumnCount)
                continue;

            string[] previousLayout = ToPreviousLayout(columns, header);

            // 认不出布局的行原样留着，Upgrade 会因此整份文件都不动。
            if (previousLayout != null)
                lines[i] = string.Join(",", PadToCurrentLayout(previousLayout));
        }
    }

    // 把任意可识别的历史布局收敛成一行「前缀正确」的列，交给 PadToCurrentLayout 补齐末尾。
    // 认不出来就返回 null。
    private static string[] ToPreviousLayout(string[] columns, string header) {
        // 历次加列都是往末尾加，所以每一版旧表头都是当前表头的前缀。凭这一点认出「只是少了末尾
        // 几列」的旧文件：原样留着，后面补空列即可。必须排在状态列那一支前面 —— 有几版的列数与
        // 那些旧布局撞上（27、30 都撞过），认错了就会把 VWAP 读数当成状态列截掉。
        if (IsEarlierSchemaHeader(header, columns.Length))
            return columns;

        if (columns.Length == PreviousColumnCount)
            return columns;

        if (columns.Length == BusinessColumnCount)
            return RemoveDroppedColumns(columns);

        if (HasTrailingStateColumns(columns.Length, header == PreviousHeaderBeforeDmsState))
            return RemoveDroppedColumns(TrimToBusinessColumns(columns));

        return null;
    }

    // 表头是当前表头的前缀，且这一行的宽度正好等于那份表头的列数 —— 也就是「同一套 schema 的
    // 早期版本」，中间的列序没有变过，只是末尾少了几列。
    private static bool IsEarlierSchemaHeader(string header, int columnCount) {
        if (header.Split(',').Length != columnCount)
            return false;

        // 只加过列的那些版本：旧表头是当前表头的前缀。
        if (TradeCsvColumns.Header.StartsWith(header + ",", StringComparison.Ordinal))
            return true;

        // 改过列名、因而断了前缀关系的那几版。
        return Array.IndexOf(RenamedEarlierHeaders, header) >= 0;
    }

    // 旧行缺的列一律在末尾，补空即可 —— 新列一直是往末尾加的，前面的列序不用动。
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
