using System.Diagnostics;
using Mahjong.Core;

namespace Mahjong.Core.Tests;

public class ScoreTableTests
{
    [Theory]
    // 翻, 符, ツモ, 親, 合計
    [InlineData(1, 30, false, false, 1000)]
    [InlineData(1, 30, false, true, 1500)]
    [InlineData(2, 30, false, false, 2000)]
    [InlineData(3, 30, false, false, 3900)]
    [InlineData(4, 30, false, false, 7700)]
    [InlineData(4, 30, false, true, 11600)]
    [InlineData(4, 40, false, false, 8000)]     // 基本点 2560 → 満貫
    [InlineData(3, 70, false, false, 8000)]
    [InlineData(3, 25, false, false, 3200)]
    [InlineData(5, 30, false, false, 8000)]
    [InlineData(6, 30, false, false, 12000)]
    [InlineData(8, 30, false, false, 16000)]
    [InlineData(11, 30, false, false, 24000)]
    [InlineData(13, 30, false, false, 32000)]   // 数え役満
    [InlineData(1, 30, true, false, 1100)]      // 300-500
    [InlineData(1, 30, true, true, 1500)]       // 500 オール
    [InlineData(2, 20, true, false, 1500)]      // 400-700
    public void 翻と符から支払いを求める(int han, int fu, bool tsumo, bool dealer, int expected)
    {
        Assert.Equal(expected, ScoreTable.From(han, fu, tsumo, dealer).Payment);
    }

    [Fact]
    public void 子のツモは親と子で支払いが違う()
    {
        var r = ScoreTable.From(3, 20, isTsumo: true, isDealer: false);
        Assert.Equal(1300, r.TsumoPaymentFromDealer);
        Assert.Equal(700, r.TsumoPaymentFromNonDealer);
    }
}

public class ScoreCalculatorTests
{
    [Fact]
    public void 立直平和ロン_子_30符2翻2000点()
    {
        var r = TestHands.Score(TestHands.Ctx("23m456m789p234s55p", "1m", riichi: true));
        Assert.Equal((2, 30, 2000), (r.Han, r.Fu, r.Payment));
    }

    [Fact]
    public void 立直平和ロン_親_2900点()
    {
        var r = TestHands.Score(TestHands.Ctx("23m456m789p234s55p", "1m", riichi: true, seat: TileKind.East));
        Assert.Equal(2900, r.Payment);
    }

    [Fact]
    public void 本場は1本につきロンで300点()
    {
        var r = TestHands.Score(TestHands.Ctx("23m456m789p234s55p", "1m", riichi: true) with { Honba = 2 });
        Assert.Equal(2600, r.Payment);
    }

    [Fact]
    public void ドラは役があれば加算される()
    {
        var r = TestHands.Score(TestHands.Ctx("23m456m789p234s55p", "1m", riichi: true, doras: "2m"));
        Assert.Equal((3, 3900), (r.Han, r.Payment));
    }

    [Fact]
    public void 赤ドラと裏ドラ()
    {
        var ctx = TestHands.Ctx("23m406m789p234s55p", "1m", riichi: true) with { UraDoras = [TileKind.P5] };
        var r = TestHands.Score(ctx);
        Assert.Equal(1, r.Yaku.Single(y => y.Name == "赤ドラ").Han);
        Assert.Equal(2, r.Yaku.Single(y => y.Name == "裏ドラ").Han);
    }

    [Fact]
    public void 門前ツモ平和断么九_子_20符3翻_1300_700()
    {
        var r = TestHands.Score(TestHands.Ctx("23m456m678p234s55p", "4m", tsumo: true));
        Assert.Equal((3, 20), (r.Han, r.Fu));
        Assert.Equal((1300, 700, 2700), (r.TsumoPaymentFromDealer, r.TsumoPaymentFromNonDealer, r.Payment));
    }

    [Fact]
    public void 親のツモはオール()
    {
        var r = TestHands.Score(TestHands.Ctx("23m456m678p234s55p", "4m", tsumo: true, seat: TileKind.East));
        Assert.Equal((1300, 3900), (r.TsumoPaymentFromNonDealer, r.Payment));
    }

    [Fact]
    public void 七対子は25符()
    {
        var r = TestHands.Score(TestHands.Ctx("1122m3344p5566s7z", "7z"));
        Assert.Equal((2, 25, 1600), (r.Han, r.Fu, r.Payment));
    }

    [Fact]
    public void 暗刻の么九牌は8符_40符1翻1300点()
    {
        var r = TestHands.Score(TestHands.Ctx("111m456p789s23s55p", "1s", riichi: true));
        Assert.Equal((1, 40, 1300), (r.Han, r.Fu, r.Payment));
    }

    [Fact]
    public void 副露した平和形のロンは30符()
    {
        var r = TestHands.Score(TestHands.Ctx("456p678s2345s", "5s", melds: "c234m"));
        Assert.Equal((1, 30, 1000), (r.Han, r.Fu, r.Payment));
    }

    [Fact]
    public void 二盃口は七対子より高い分解を採用する()
    {
        var r = TestHands.Score(TestHands.Ctx("112233m445566p7z", "7z"));
        Assert.Equal((3, 40, 5200), (r.Han, r.Fu, r.Payment));
    }

    [Fact]
    public void 清一色平和は跳満()
    {
        var r = TestHands.Score(TestHands.Ctx("123m345m567m789m5m", "5m"));
        Assert.Equal(("跳満", 12000), (r.LimitName, r.Payment));
    }

    [Fact]
    public void 役満は子32000点_親48000点()
    {
        Assert.Equal(32000, TestHands.Score(TestHands.Ctx("19m19p19s1234567z", "1m")).Payment);
        Assert.Equal(48000, TestHands.Score(TestHands.Ctx("19m19p19s1234567z", "1m", seat: TileKind.East)).Payment);
    }

    [Fact]
    public void 役満の複合はダブル役満()
    {
        // 字一色+四暗刻(単騎)
        var r = TestHands.Score(TestHands.Ctx("111z222z333z555z6z", "6z", tsumo: true));
        Assert.Equal("ダブル役満", r.LimitName);
        Assert.DoesNotContain("混老頭", r.Yaku.Select(y => y.Name));
    }

    [Fact]
    public void 和了形でなければ無効()
    {
        Assert.False(TestHands.Score(TestHands.Ctx("123m456p789s1234z", "5z", riichi: true)).IsValid);
    }

    [Fact]
    public void 点数計算は1回あたり1ミリ秒未満()
    {
        var ctx = TestHands.Ctx("1112345678999m", "5m");
        TestHands.Score(ctx);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++) TestHands.Score(ctx);
        Assert.True(sw.Elapsed.TotalMilliseconds / 1000 < 1.0, $"平均 {sw.Elapsed.TotalMilliseconds / 1000:F3} ms");
    }
}

public class HandDecomposerTests
{
    [Fact]
    public void 分解をすべて列挙する()
    {
        // 111222333m は 3 刻子とも 3 順子とも取れる
        var hand = new Hand(TileParser.Parse("111222333m456p7z"));
        var list = HandDecomposer.Enumerate(hand, TileParser.ParseOne("7z"), isTsumo: false);
        Assert.Contains(list, d => d.Blocks.Count(b => b.Type == BlockType.Koutsu) == 3);
        Assert.Contains(list, d => d.Blocks.Count(b => b.Type == BlockType.Shuntsu) == 4);
    }

    [Fact]
    public void 七対子と国士無双を判定する()
    {
        Assert.True(HandDecomposer.IsChiitoitsu(TestHands.Counts("1122m3344p5566s77z")));
        Assert.False(HandDecomposer.IsChiitoitsu(TestHands.Counts("1111m3344p5566s77z")));   // 同じ牌 4 枚は 2 対子にならない
        Assert.True(HandDecomposer.IsKokushi(TestHands.Counts("19m19p19s12345677z")));
    }

    [Theory]
    [InlineData("23m456m789p234s55p", "1m", WaitType.Ryanmen)]
    [InlineData("13m456m789p234s55p", "2m", WaitType.Kanchan)]
    [InlineData("12m456m789p234s55p", "3m", WaitType.Penchan)]
    [InlineData("89m456m789p234s55p", "7m", WaitType.Penchan)]
    [InlineData("123m456m789p234s5p", "5p", WaitType.Tanki)]
    [InlineData("11m456m789p234s55p", "1m", WaitType.Shanpon)]
    public void 待ちの形を判定する(string closed, string win, WaitType expected)
    {
        var hand = new Hand(TileParser.Parse(closed));
        var list = HandDecomposer.Enumerate(hand, TileParser.ParseOne(win), isTsumo: false);
        Assert.Contains(list, d => d.Wait == expected);
    }
}
