using System;
using System.Globalization;
using System.IO;
using System.Text;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

public class TradeCsvLogger {
    private static readonly Encoding CsvEncoding = new UTF8Encoding(true);
    private readonly string _filePath;

    // resetOnStart: overwrite the file with a fresh header so it holds only this run.
    // The file is fixed and append-only, so without this every backtest run stacks another
    // copy of the same trades. Turn it off to keep history across runs.
    //
    // reportsDirectory: 按运行模式选定的输出目录（回测/模拟/实盘各一个，见组合根）。
    // fileName 传绝对路径时（回测经 run_conditions 指定完整路径）直接采用、忽略 reportsDirectory。
    public TradeCsvLogger(bool resetOnStart, string reportsDirectory, string fileName) {
        _filePath = ResolveFilePath(reportsDirectory, fileName);
        EnsureDirectoryExists(_filePath);

        if (resetOnStart)
            System.IO.File.WriteAllText(_filePath, BuildHeader() + Environment.NewLine, CsvEncoding);
        else
            EnsureFileExists();
    }

    private static string ResolveFilePath(string reportsDirectory, string fileName) {
        if (Path.IsPathRooted(fileName))
            return fileName;

        return Path.Combine(reportsDirectory, fileName);
    }

    private static void EnsureDirectoryExists(string filePath) {
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public string FilePath => _filePath;

    public string AppendEntry(OrderPlanModel planModel, Position position, string symbolName, string timeFrame) {
        if (planModel == null || position == null)
            return "";

        var record = new TradeCsvRecordModel {
            Id = position.Id.ToString(),
            KeyLevel = planModel.KeyLevel,
            Signal = planModel.SignalName,
            Comment = "ENTRY",
            Symbol = symbolName,
            TimeFrame = timeFrame,
            EntryAccountEquity = planModel.AccountEquity,
            CloseAccountEquity = 0.0,
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            StopPrice = position.StopLoss ?? planModel.StopPrice,
            TakeProfitPrice = planModel.TakeProfitPrice,
            RiskPrice = Math.Abs(position.EntryPrice - (position.StopLoss ?? planModel.StopPrice)),
            VolumeInUnits = position.VolumeInUnits,
            PositionId = position.Id.ToString(),
            DealId = GetOpenDealId(position),
            Side = GetSideText(planModel.DirectionModel),
            DailyVwap = planModel.DailyVwap,
            WeeklyVwap = planModel.WeeklyVwap,
            Atr14H1 = planModel.Atr14H1,
            GapX = planModel.GapX,
            SlopeX = planModel.SlopeX
        };

        Append(record);
        return record.Id;
    }

    private static string GetSideText(TradeDirectionModel directionModel) {
        return directionModel == TradeDirectionModel.Long ? "多" : "空";
    }

    // entryPlan 是开仓时那一份下单方案（见 OrderExecutor 的 _positionPlans）。把它的 VWAP 读数
    // 抄到平仓行，一行里就同时有 GapX/SlopeX 和这笔的盈亏，调参时不用再按持仓 ID 去拼两行。
    public string AppendClose(Position position, PositionCloseReason reason, string csvId, string symbolName, string timeFrame,
        DateTime serverTime, double closePrice, double entryAccountEquity, double closeAccountEquity, OrderPlanModel entryPlan) {
        if (position == null)
            return "";

        string closeReason = GetCloseReasonCode(reason);
        double resolvedEntryAccountEquity = entryAccountEquity;

        if (resolvedEntryAccountEquity <= 0.0 && closeAccountEquity > 0.0)
            resolvedEntryAccountEquity = closeAccountEquity - position.NetProfit;

        string finalResult = position.NetProfit >= 0.0 ? "盈利" : "亏损";

        var record = new TradeCsvRecordModel {
            Id = GetCloseRecordId(csvId, reason),
            KeyLevel = "",
            Signal = "close",
            Comment = finalResult,
            FinalResult = finalResult,
            Side = position.TradeType == TradeType.Buy ? "多" : "空",
            DailyVwap = entryPlan?.DailyVwap ?? double.NaN,
            WeeklyVwap = entryPlan?.WeeklyVwap ?? double.NaN,
            Atr14H1 = entryPlan?.Atr14H1 ?? double.NaN,
            GapX = entryPlan?.GapX ?? double.NaN,
            SlopeX = entryPlan?.SlopeX ?? double.NaN,
            Symbol = symbolName,
            TimeFrame = timeFrame,
            EntryAccountEquity = resolvedEntryAccountEquity,
            CloseAccountEquity = closeAccountEquity,
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            ClosePrice = closePrice,
            StopPrice = 0.0,
            TakeProfitPrice = 0.0,
            RiskPrice = 0.0,
            VolumeInUnits = position.VolumeInUnits,
            CloseReason = closeReason,
            ProfitLoss = position.NetProfit,
            CloseTime = serverTime.ToString("yyyy-MM-dd HH:mm:ss"),
            PositionId = position.Id.ToString(),
            DealId = GetCloseDealId(position)
        };

        Append(record);
        return record.Id;
    }

    public void Append(TradeCsvRecordModel recordModel) {
        if (recordModel == null)
            return;

        string line = string.Join(",", Escape(recordModel.Id), Escape(recordModel.KeyLevel), Escape(recordModel.Signal),
            Escape(recordModel.Comment), Escape(recordModel.Symbol), Escape(recordModel.TimeFrame),
            Escape(recordModel.EntryTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            Escape(recordModel.EntryPrice.ToString(CultureInfo.InvariantCulture)), Escape(FormatOptionalNumber(recordModel.ClosePrice)),
            Escape(recordModel.StopPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(recordModel.TakeProfitPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(recordModel.RiskPrice.ToString(CultureInfo.InvariantCulture)),
            Escape(recordModel.VolumeInUnits.ToString(CultureInfo.InvariantCulture)), Escape(recordModel.CloseReason),
            Escape(FormatOptionalNumber(recordModel.EntryAccountEquity)), Escape(FormatOptionalNumber(recordModel.CloseAccountEquity)),
            Escape(recordModel.ProfitLoss.ToString(CultureInfo.InvariantCulture)), Escape(recordModel.CloseTime),
            Escape(recordModel.PositionId), Escape(recordModel.DealId),
            Escape(recordModel.Side), Escape(FormatReading(recordModel.DailyVwap)), Escape(FormatReading(recordModel.WeeklyVwap)),
            Escape(FormatReading(recordModel.Atr14H1)), Escape(FormatReading(recordModel.GapX)),
            Escape(FormatReading(recordModel.SlopeX)), Escape(recordModel.FinalResult));
        System.IO.File.AppendAllText(_filePath, line + Environment.NewLine, CsvEncoding);
    }

    private void EnsureFileExists() {
        string header = BuildHeader();

        if (!System.IO.File.Exists(_filePath)) {
            System.IO.File.WriteAllText(_filePath, header + Environment.NewLine, CsvEncoding);
            return;
        }

        string[] lines = System.IO.File.ReadAllLines(_filePath);

        if (lines.Length == 0) {
            System.IO.File.WriteAllText(_filePath, header + Environment.NewLine, CsvEncoding);
            return;
        }

        string[] upgraded = TradeCsvMigrator.Upgrade(lines, header);

        if (upgraded != null)
            System.IO.File.WriteAllLines(_filePath, upgraded, CsvEncoding);
    }

    private static string BuildHeader() {
        // 新列一律加在末尾：旧文件升级时只要在后面补空列即可，前面的列序不用动（见 TradeCsvMigrator）。
        return string.Join(",", "编号", "关键位", "信号", "备注", "交易品种", "时间周期", "入场时间", "入场价格", "平仓价格", "止损价格", "止盈价格", "风险价格距离", "下单数量",
            "平仓原因", "开仓账户权益", "平仓账户权益", "平仓盈亏", "平仓时间", "持仓ID", "成交ID",
            "多空", "DailyVWAP", "WeeklyVWAP", "ATR14_H1", "GapX", "SlopeX", "最终结果");
    }

    private static string FormatOptionalNumber(double value) {
        if (value <= 0.0)
            return "";

        return value.ToString(CultureInfo.InvariantCulture);
    }

    // VWAP 读数可以是负数（逆着方向走的 SlopeX），所以不能套用 FormatOptionalNumber 的「≤0 留空」。
    // 只有取不到值（NaN/无穷）才留空。保留 6 位小数，GapX/SlopeX 这种小数值才看得出差别。
    private static string FormatReading(double value) {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "";

        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value) {
        if (value == null)
            return string.Empty;

        bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");

        if (!mustQuote)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string GetCloseReasonCode(PositionCloseReason reason) {
        switch (reason) {
            case PositionCloseReason.StopLoss:
                return "SL";
            case PositionCloseReason.StopOut:
                return "SO";
            case PositionCloseReason.TakeProfit:
                return "TP";
            default:
                return "CLOSE";
        }
    }

    private static string GetCloseRecordId(string csvId, PositionCloseReason reason) {
        if (reason == PositionCloseReason.TakeProfit)
            return $"{csvId}-TP";

        return $"{csvId}-{GetCloseReasonCode(reason)}";
    }

    private static string GetOpenDealId(Position position) {
        if (position.Deals == null || position.Deals.Count == 0)
            return "";

        return position.Deals[0].Id.ToString();
    }

    private static string GetCloseDealId(Position position) {
        if (position.Deals == null || position.Deals.Count == 0)
            return "";

        return position.Deals[position.Deals.Count - 1].Id.ToString();
    }

}
