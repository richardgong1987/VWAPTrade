using System;
using System.Collections.Generic;
using cAlgo.API;

namespace cAlgo.Robots;

public class SignalMarkers {
    private const string Prefix = "VWAP_SIGNAL_";

    private const int IconOffsetTicks = 120;
    private const int TextOffsetTicks = 320;
    private const int TextFontSize = 8;

    private readonly Chart _chart;
    private readonly double _iconOffset;
    private readonly double _textOffset;
    private readonly HashSet<string> _objectNames = new();

    public SignalMarkers(Chart chart, double tickSize) {
        _chart = chart;
        _iconOffset = tickSize * IconOffsetTicks;
        _textOffset = tickSize * TextOffsetTicks;
    }

    public void Draw(SignalModel signalModel) {
        if (signalModel?.Level == null)
            return;

        if (signalModel.Level.Side == SignalSideModel.Buy)
            DrawLong(signalModel);
        else
            DrawShort(signalModel);
    }

    public void Clear() {
        foreach (string name in _objectNames) {
            _chart.RemoveObject(name);
        }

        _objectNames.Clear();
    }

    private void DrawLong(SignalModel signalModel) {
        string key = GetKey(signalModel);

        double iconPrice = signalModel.Low - _iconOffset;
        double textPrice = signalModel.Low - _textOffset;

        string iconName = $"{Prefix}LONG_ICON_{key}";
        string textName = $"{Prefix}LONG_TEXT_{key}";

        RemoveExisting(iconName);
        RemoveExisting(textName);

        _chart.DrawIcon(iconName, ChartIconType.UpTriangle, signalModel.BarIndex, iconPrice, Color.Lime);

        ChartText text = _chart.DrawText(textName, signalModel.Label, signalModel.BarIndex, textPrice, Color.Lime);

        ApplyTextStyle(text);

        _objectNames.Add(iconName);
        _objectNames.Add(textName);
    }

    private void DrawShort(SignalModel signalModel) {
        string key = GetKey(signalModel);

        double iconPrice = signalModel.High + _iconOffset;
        double textPrice = signalModel.High + _textOffset;

        string iconName = $"{Prefix}SHORT_ICON_{key}";
        string textName = $"{Prefix}SHORT_TEXT_{key}";

        RemoveExisting(iconName);
        RemoveExisting(textName);

        _chart.DrawIcon(iconName, ChartIconType.DownTriangle, signalModel.BarIndex, iconPrice, Color.Red);

        ChartText text = _chart.DrawText(textName, signalModel.Label, signalModel.BarIndex, textPrice, Color.Red);

        ApplyTextStyle(text);

        _objectNames.Add(iconName);
        _objectNames.Add(textName);
    }

    private static void ApplyTextStyle(ChartText text) {
        text.FontSize = TextFontSize;
        text.IsBold = false;
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
    }

    private void RemoveExisting(string name) {
        _chart.RemoveObject(name);
        _objectNames.Remove(name);
    }

    // 一根 K 线可能同时命中几档，所以 key 要带上档位名，否则几个标记会互相覆盖。
    private static string GetKey(SignalModel signalModel) {
        return $"{signalModel.BarTime:yyyyMMdd_HHmmss}_{signalModel.Level.Name}";
    }
}
