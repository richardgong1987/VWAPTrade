using System;
using System.IO;
using System.Text;
using cAlgo.API;
using cAlgo.API.Internals;

// cAlgo.API 也有个 File 类型，会跟 System.IO.File 撞名。
using IoFile = System.IO.File;

namespace cAlgo.Robots;

// 把每一笔的开仓与平仓各写一行进交易 CSV。列定义在 TradeCsvColumns，这里只管
// 「文件在哪、什么时候写、一行里放哪些事实」。
public class TradeCsvLogger {
    private static readonly Encoding CsvEncoding = new UTF8Encoding(true);

    private readonly string _filePath;

    // resetOnStart: 用一份新表头覆盖整个文件，只保留本次运行。文件是固定名、只追加的，
    // 不清空的话每跑一次回测就叠一份同样的交易。关掉它则跨运行累积。
    //
    // reportsDirectory: 按运行模式选定的输出目录（回测/模拟/实盘各一个，见组合根）。
    // fileName 传绝对路径时（回测经 run_conditions 指定完整路径）直接采用、忽略 reportsDirectory。
    public TradeCsvLogger(bool resetOnStart, string reportsDirectory, string fileName) {
        _filePath = ResolveFilePath(reportsDirectory, fileName);
        EnsureDirectoryExists(_filePath);

        if (resetOnStart)
            IoFile.WriteAllText(_filePath, TradeCsvColumns.Header + Environment.NewLine, CsvEncoding);
        else
            EnsureFileUpToDate();
    }

    public string FilePath => _filePath;

    public string AppendEntry(OrderPlanModel planModel, Position position, string symbolName, string timeFrame) {
        if (planModel == null || position == null)
            return "";

        var record = new TradeRecordModel {
            Id = position.Id.ToString(),
            KeyLevel = planModel.KeyLevel,
            Signal = planModel.SignalName,
            Comment = "ENTRY",
            Symbol = symbolName,
            TimeFrame = timeFrame,
            Side = GetSideText(planModel.DirectionModel),
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            StopPrice = position.StopLoss ?? planModel.StopPrice,
            TakeProfitPrice = planModel.TakeProfitPrice,
            RiskPrice = Math.Abs(position.EntryPrice - (position.StopLoss ?? planModel.StopPrice)),
            VolumeInUnits = position.VolumeInUnits,
            EntryAccountEquity = planModel.AccountEquity,
            PositionId = position.Id.ToString(),
            DealId = GetDealId(position, first: true),
            EntryPlan = planModel
        };

        Append(record);
        return record.Id;
    }

    // entryPlan 是开仓时那一份下单方案（见 TradeJournal）。平仓行复用它，
    // 一行里就同时有 GapX/SlopeRateX 和这笔的盈亏，调参时不用再按持仓 ID 去拼两行。
    public string AppendClose(Position position, PositionCloseReason reason, string csvId, string symbolName, string timeFrame,
        DateTime serverTime, double closePrice, double entryAccountEquity, double closeAccountEquity, OrderPlanModel entryPlan) {
        if (position == null)
            return "";

        string finalResult = position.NetProfit >= 0.0 ? "盈利" : "亏损";

        var record = new TradeRecordModel {
            Id = GetCloseRecordId(csvId, reason),
            Signal = "close",
            Comment = finalResult,
            FinalResult = finalResult,
            Symbol = symbolName,
            TimeFrame = timeFrame,
            Side = position.TradeType == TradeType.Buy ? "多" : "空",
            EntryTime = position.EntryTime,
            EntryPrice = position.EntryPrice,
            ClosePrice = closePrice,
            VolumeInUnits = position.VolumeInUnits,
            CloseReason = GetCloseReasonCode(reason),
            EntryAccountEquity = ResolveEntryEquity(position, entryAccountEquity, closeAccountEquity),
            CloseAccountEquity = closeAccountEquity,
            ProfitLoss = position.NetProfit,
            ResultR = GetResultR(position, closePrice, entryPlan),
            CloseTime = serverTime.ToString("yyyy-MM-dd HH:mm:ss"),
            PositionId = position.Id.ToString(),
            DealId = GetDealId(position, first: false),
            EntryPlan = entryPlan
        };

        Append(record);
        return record.Id;
    }

    public void Append(TradeRecordModel record) {
        if (record == null)
            return;

        IoFile.AppendAllText(_filePath, TradeCsvColumns.ToCsvLine(record) + Environment.NewLine, CsvEncoding);
    }

    // ── 文件与表头 ───────────────────────────────────────────────────────────
    private static string ResolveFilePath(string reportsDirectory, string fileName) {
        return Path.IsPathRooted(fileName) ? fileName : Path.Combine(reportsDirectory, fileName);
    }

    private static void EnsureDirectoryExists(string filePath) {
        string directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    private void EnsureFileUpToDate() {
        string[] lines = IoFile.Exists(_filePath) ? IoFile.ReadAllLines(_filePath) : Array.Empty<string>();

        if (lines.Length == 0) {
            IoFile.WriteAllText(_filePath, TradeCsvColumns.Header + Environment.NewLine, CsvEncoding);
            return;
        }

        string[] upgraded = TradeCsvMigrator.Upgrade(lines, TradeCsvColumns.Header);

        if (upgraded != null)
            IoFile.WriteAllLines(_filePath, upgraded, CsvEncoding);
    }

    // ── 单行里的几个小判断 ───────────────────────────────────────────────────
    private static string GetSideText(TradeDirectionModel directionModel) {
        return directionModel == TradeDirectionModel.Long ? "多" : "空";
    }

    // 实际打出来的 R，按开仓时的初始风险价格距离算（公式见 TradeResultR）。
    private static double GetResultR(Position position, double closePrice, OrderPlanModel entryPlan) {
        return TradeResultR.Calculate(position.TradeType == TradeType.Buy ? TradeDirectionModel.Long : TradeDirectionModel.Short,
            position.EntryPrice, closePrice, entryPlan?.RiskPrice ?? double.NaN);
    }

    // 开仓权益没记到（例如重启后才平的仓）时，用平仓权益倒推。
    private static double ResolveEntryEquity(Position position, double entryAccountEquity, double closeAccountEquity) {
        if (entryAccountEquity > 0.0 || closeAccountEquity <= 0.0)
            return entryAccountEquity;

        return closeAccountEquity - position.NetProfit;
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
        return $"{csvId}-{GetCloseReasonCode(reason)}";
    }

    private static string GetDealId(Position position, bool first) {
        if (position.Deals == null || position.Deals.Count == 0)
            return "";

        return (first ? position.Deals[0] : position.Deals[position.Deals.Count - 1]).Id.ToString();
    }
}
