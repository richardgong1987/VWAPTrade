using System;
using System.IO;
using System.Text;

// cAlgo.API 也有个 File 类型，会跟 System.IO.File 撞名。
using IoFile = System.IO.File;

namespace cAlgo.Robots;

// 交易 CSV 这个文件本身：它在哪、表头对不对、怎么往里追加一行。
// 一行里放哪些事实由 TradeCsvLogger 决定，有哪些列由 TradeCsvColumns 决定。
public class TradeCsvFile {
    private static readonly Encoding CsvEncoding = new UTF8Encoding(true);

    // resetOnStart: 用一份新表头覆盖整个文件，只保留本次运行。文件是固定名、只追加的，
    // 不清空的话每跑一次回测就叠一份同样的交易。关掉它则跨运行累积，旧文件先迁移到当前列数。
    //
    // reportsDirectory: 按运行模式选定的输出目录（回测/模拟/实盘各一个，见组合根）。
    // fileName 传绝对路径时（回测经 run_conditions 指定完整路径）直接采用、忽略 reportsDirectory。
    public TradeCsvFile(bool resetOnStart, string reportsDirectory, string fileName) {
        FilePath = Path.IsPathRooted(fileName) ? fileName : Path.Combine(reportsDirectory, fileName);
        EnsureDirectoryExists(FilePath);

        if (resetOnStart)
            WriteHeader();
        else
            MigrateExisting();
    }

    public string FilePath { get; }

    public void AppendLine(string line) {
        IoFile.AppendAllText(FilePath, line + Environment.NewLine, CsvEncoding);
    }

    private void WriteHeader() {
        IoFile.WriteAllText(FilePath, TradeCsvColumns.Header + Environment.NewLine, CsvEncoding);
    }

    // 旧文件的列数比现在少时，先补齐再往下追加；补不了（认不出布局）就原样留着，
    // 让人看得见出了什么问题，而不是把当前表头盖到对不上的行上面（见 TradeCsvMigrator）。
    private void MigrateExisting() {
        string[] lines = IoFile.Exists(FilePath) ? IoFile.ReadAllLines(FilePath) : Array.Empty<string>();

        if (lines.Length == 0) {
            WriteHeader();
            return;
        }

        string[] upgraded = TradeCsvMigrator.Upgrade(lines, TradeCsvColumns.Header);

        if (upgraded != null)
            IoFile.WriteAllLines(FilePath, upgraded, CsvEncoding);
    }

    private static void EnsureDirectoryExists(string filePath) {
        string directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}
