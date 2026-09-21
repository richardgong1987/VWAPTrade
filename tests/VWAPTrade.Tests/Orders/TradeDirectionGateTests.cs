using cAlgo.Robots;
using Xunit;

namespace VWAPTrade.Tests.Orders {
    // 交易方向开关（VWAP_Strong_V1.1 第 5 节）。只管准不准下单，不改任何指标。
    public class TradeDirectionGateTests {
        [Theory]
        [InlineData(SignalSideModel.Buy)]
        [InlineData(SignalSideModel.Sell)]
        public void all_lets_both_sides_through(SignalSideModel side) {
            // All 必须放行全部信号，否则就与没有这个开关的旧版本结果对不上。
            Assert.True(TradeDirectionGate.IsAllowed(TradeDirectionModeModel.All, side));
        }

        [Fact]
        public void long_only_refuses_every_short() {
            Assert.True(TradeDirectionGate.IsAllowed(TradeDirectionModeModel.LongOnly, SignalSideModel.Buy));
            Assert.False(TradeDirectionGate.IsAllowed(TradeDirectionModeModel.LongOnly, SignalSideModel.Sell));
        }

        [Fact]
        public void short_only_refuses_every_long() {
            Assert.True(TradeDirectionGate.IsAllowed(TradeDirectionModeModel.ShortOnly, SignalSideModel.Sell));
            Assert.False(TradeDirectionGate.IsAllowed(TradeDirectionModeModel.ShortOnly, SignalSideModel.Buy));
        }

        [Fact]
        public void the_default_mode_is_all() {
            // 默认值必须是 All：升级本身不该改变任何人的回测结果。
            Assert.Equal(TradeDirectionModeModel.All, default(TradeDirectionModeModel));
        }
    }
}
