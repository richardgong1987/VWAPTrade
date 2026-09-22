namespace cAlgo.Robots;

// 允许往哪个方向下单（VWAP Strong V1.1 第 5 节）。只做「最终交易许可」，
// 不参与 VWAP / Gap / Slope / 裸 K 信号本身的计算 —— All 模式下的成交结果必须与没有这个开关时完全一致。
public enum TradeDirectionPermissionModel {
    All,
    LongOnly,
    ShortOnly
}
