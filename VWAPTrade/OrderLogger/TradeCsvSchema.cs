namespace cAlgo.Robots;

// 交易 CSV 的列定义，只此一份：TradeCsvLogger 按它写表头和每一行，TradeCsvMigrator 按它决定旧文件
// 要补到几列。两边各写一份的话，加列时漏改一边就会写出宽度对不上的文件。
//
// 旧列的含义保持不变（PDF 第 10 节）：ATR14 / GapX 是「所选周期」那一套的读数，
// SlopeX 是原始斜率（未做 30 分钟归一）。新的速度值另开 SlopeRateX30_* 列，不覆盖旧列。
//
// 新列一律加在末尾：旧文件升级时只要在后面补空列，前面的列序不用动。
// 列名尽量不要改 —— 改了之后旧表头就不再是当前表头的前缀，TradeCsvMigrator 必须把那一版
// 表头逐字列出来才认得出（ATR14_H1 改名成 ATR14 时就是这么处理的）。
public static class TradeCsvSchema {
    private static readonly string[] Columns = {
        "编号", "关键位", "信号", "备注", "交易品种", "时间周期", "入场时间", "入场价格", "平仓价格", "止损价格", "止盈价格", "风险价格距离", "下单数量",
        "平仓原因", "开仓账户权益", "平仓账户权益", "平仓盈亏", "平仓时间", "持仓ID", "成交ID",
        "多空", "DailyVWAP", "WeeklyVWAP", "ATR14", "GapX", "SlopeX", "最终结果",
        "DailyVWAP_Lookback", "ResultR", "GapChangeX", "WeeklyVWAP_Lookback",
        "LookbackN", "ATR14_M5", "ATR14_H1", "GapX_M5", "GapX_H1", "SlopeRawX_M5", "SlopeRawX_H1",
        "SlopeRateX30_M5", "SlopeRateX30_H1", "SelectedATRPeriod", "GapX_Selected", "SlopeRateX_Selected",
        "TradeDirectionMode", "UseGapChangeFilter", "GapChangeRateMin", "GapChangeRateMax", "GapChangeRawPrice",
        "GapChangeRawX_M5", "GapChangeRawX_H1", "GapChangeRateX30_M5", "GapChangeRateX30_H1", "GapChangeRateX30_Selected"
    };

    public static int ColumnCount => Columns.Length;

    public static string Header => string.Join(",", Columns);
}
