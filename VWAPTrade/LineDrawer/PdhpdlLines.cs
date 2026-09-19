using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// 把手工配置的六档价位画成贯穿全图的水平线：空档（PDH1~3）绿色，多档（PDL1~3）红色。
// 留 0 没启用的档不画。
public class PdhpdlLines {
    private const string Prefix = "PDH_PDL_LEVEL_";
    private const int Thickness = 2;
    private const int LabelFontSize = 9;

    private readonly Chart _chart;
    private readonly List<TradeLevelModel> _tradeLevels;
    private readonly List<string> _objectNames = new();

    public PdhpdlLines(Chart chart, IEnumerable<TradeLevelModel> tradeLevels) {
        _chart = chart;
        _tradeLevels = tradeLevels.Where(level => level.IsConfigured).ToList();
    }

    // 线本身是无限长的，不用重画；重画只是把名字挪到最新一根 K 线那边，跟着行情走。
    public void Draw() {
        Clear();

        foreach (TradeLevelModel level in _tradeLevels) {
            DrawLevel(level);
        }
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }

    private void DrawLevel(TradeLevelModel level) {
        string label = GetLabel(level);
        Color color = level.Side == SignalSideModel.Sell ? Color.Lime : Color.Red;

        string lineName = $"{Prefix}{label}";
        string textName = $"{Prefix}TEXT_{label}";

        _chart.DrawHorizontalLine(lineName, level.Price, color, Thickness, LineStyle.Solid);

        ChartText text = _chart.DrawText(textName, label, _chart.BarsTotal - 1, level.Price, color);
        text.FontSize = LabelFontSize;
        text.VerticalAlignment = VerticalAlignment.Bottom;
        text.HorizontalAlignment = HorizontalAlignment.Right;

        _objectNames.Add(lineName);
        _objectNames.Add(textName);
    }

    // 配置里的档位名是 Short1/Long1，图上按交易习惯显示成 PDH1/PDL1。
    private static string GetLabel(TradeLevelModel level) {
        string prefix = level.Side == SignalSideModel.Sell ? "PDH" : "PDL";
        return prefix + level.Name[level.Name.Length - 1];
    }
}
