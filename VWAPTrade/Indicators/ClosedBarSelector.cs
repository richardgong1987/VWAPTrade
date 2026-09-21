using System;

namespace cAlgo.Robots;

// 在另一个周期的序列里，挑出「到评估时刻为止，最后一根已经收线的 K 线」。
//
// 判据只有一条：一根 K 线收线，等价于它的下一根已经开出来。所以下一根的开盘时间 ≤ 评估时刻，
// 这一根就可以用；否则它还在走，得往前退一根。
//
// 这样不用写死周期就能同时处理两种情况（评估时刻 = 信号 K 线的收盘时间 11:35）：
//   M5：含 11:35 的是刚开的 11:35 那根 → 退到 11:30，正是刚收线的信号 K 线本身。
//   H1：含 11:35 的是 11:00~12:00 那根，12:00 > 11:35 还没收 → 退到 10:00。
//
// 从前是一律退一根，对 H1 正确，对 M5 却白白滞后一根。纯时间比较，有单元测试。
public static class ClosedBarSelector {
    // containingIndex：含评估时刻的那一根的下标（cAlgo 的 GetIndexByTime 给出）。
    // nextBarOpenTime：它下一根的开盘时间；没有下一根（它是序列最后一根）时传 null。
    public static int ResolveClosedBarIndex(int containingIndex, DateTime? nextBarOpenTime, DateTime evaluationTime) {
        if (nextBarOpenTime.HasValue && nextBarOpenTime.Value <= evaluationTime)
            return containingIndex;

        return containingIndex - 1;
    }
}
