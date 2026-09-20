namespace cAlgo.Robots;

// 交易 CSV 的列定义，只此一份：TradeCsvLogger 按它写表头和每一行，TradeCsvMigrator 按它决定旧文件
// 要补到几列。两边各写一份的话，加列时漏改一边就会写出宽度对不上的文件。
//
// 新列一律加在末尾：旧文件升级时只要在后面补空列，前面的列序不用动。
public static class TradeCsvSchema {
    private static readonly string[] Columns = {
        "编号", "关键位", "信号", "备注", "交易品种", "时间周期", "入场时间", "入场价格", "平仓价格", "止损价格", "止盈价格", "风险价格距离", "下单数量",
        "平仓原因", "开仓账户权益", "平仓账户权益", "平仓盈亏", "平仓时间", "持仓ID", "成交ID",
        "多空", "DailyVWAP", "WeeklyVWAP", "ATR14_H1", "GapX", "SlopeX", "最终结果",
        "DailyVWAP_Lookback", "ResultR", "GapChangeX", "WeeklyVWAP_Lookback"
    };

    public static int ColumnCount => Columns.Length;

    public static string Header => string.Join(",", Columns);
}
