using Mahjong.Core;

namespace Mahjong.Core.Tests;

/// <summary>
/// 役の成立/不成立。特に指定がなければ、子(南家)・東場・ロン。
/// 表記: closed=和了牌を除く閉じた牌, win=和了牌, melds=副露(c=チー p=ポン k=大明槓 a=暗槓)。
/// </summary>
public class YakuTests
{
    [Theory]
    [InlineData("23m456m678p234s55p", "4m", true, "", "門前清自摸和")]
    [InlineData("23m456m678p234s55p", "4m", true, "", "平和")]
    [InlineData("23m456m678p234s55p", "4m", true, "", "断么九")]
    [InlineData("23m456m789p234s55p", "1m", false, "", "平和")]
    [InlineData("456p678s2345s", "5s", false, "c234m", "断么九")]          // 喰いタン
    [InlineData("112233m456p789s5p", "5p", false, "", "一盃口")]
    [InlineData("123m456p789s1s", "1s", false, "p555z", "役牌 白")]
    [InlineData("123m456p789s1s", "1s", false, "p666z", "役牌 發")]
    [InlineData("123m456p789s1s", "1s", false, "p777z", "役牌 中")]
    [InlineData("123m456p789s1s", "1s", false, "p111z", "場風牌")]
    [InlineData("123m456p789s1s", "1s", false, "p222z", "自風牌")]
    [InlineData("1122m3344p5566s7z", "7z", false, "", "七対子")]
    [InlineData("123m123p123s456m9p", "9p", false, "", "三色同順")]
    [InlineData("123456789m456p1s", "1s", false, "", "一気通貫")]
    [InlineData("123m789p111z99s12s", "3s", false, "", "混全帯么九")]
    [InlineData("123m789p123s99s78s", "9s", false, "", "純全帯么九")]
    [InlineData("555p777s99m11s", "1s", false, "p222m", "対々和")]
    [InlineData("111m333p555s78s99m", "9s", false, "", "三暗刻")]
    [InlineData("111m333p555s77s99m", "9m", false, "", "三暗刻")]          // ロンの刻子は明刻 → 四暗刻にならない
    [InlineData("222p222s45m99p", "3m", false, "p222m", "三色同刻")]
    [InlineData("456m7z", "7z", false, "a1111m k2222p a3333s", "三槓子")]
    [InlineData("555z666z77z123m45p", "6p", false, "", "小三元")]
    [InlineData("999p111s99s11z", "9s", false, "p111m", "混老頭")]
    [InlineData("112233m445566p7z", "7z", false, "", "二盃口")]
    [InlineData("123m456m789m111z5m", "5m", false, "", "混一色")]
    [InlineData("123m345m567m789m5m", "5m", false, "", "清一色")]
    // 役満
    [InlineData("19m19p19s1234567z", "1m", false, "", "国士無双")]
    [InlineData("111m333p555s77s99m", "9m", true, "", "四暗刻")]
    [InlineData("555z666z777z123m4p", "4p", false, "", "大三元")]
    [InlineData("111z222z333z44z55m", "5m", false, "", "小四喜")]
    [InlineData("111z222z333z44z55m", "4z", false, "", "大四喜")]
    [InlineData("111z222z333z555z6z", "6z", false, "", "字一色")]
    [InlineData("111m999m111p999p1s", "1s", false, "", "清老頭")]
    [InlineData("22334466s888s66z", "6s", false, "", "緑一色")]
    [InlineData("1112345678999m", "5m", false, "", "九蓮宝燈")]
    [InlineData("5m", "5m", false, "a1111m a2222p k3333s a4444s", "四槓子")]
    public void 役が成立する(string closed, string win, bool tsumo, string melds, string yaku)
    {
        var r = TestHands.Score(TestHands.Ctx(closed, win, tsumo, melds: melds));
        Assert.True(r.IsValid, "役なしと判定された");
        Assert.Contains(yaku, r.Yaku.Select(y => y.Name));
    }

    /// <summary>不成立のケース。役なしにならないよう立直を付けて計算する。</summary>
    [Theory]
    [InlineData("13m456m789p234s55p", "2m", false, "平和")]                // 嵌張待ち
    [InlineData("23m456m789p234s11z", "1m", false, "平和")]                // 場風の雀頭
    [InlineData("23m456m789p234s55p", "1m", false, "断么九")]              // 1m を含む
    [InlineData("23m456m789p234s55p", "1m", false, "門前清自摸和")]        // ロンなので不成立
    [InlineData("112233m445566p7z", "7z", false, "一盃口")]                // 二盃口になる
    [InlineData("112233m445566p7z", "7z", false, "七対子")]                // 二盃口の方が高い
    [InlineData("123m789p123s99s78s", "9s", false, "混全帯么九")]          // 純全帯么九になる
    [InlineData("123m345m567m789m5m", "5m", false, "混一色")]              // 清一色になる
    [InlineData("111m333p555s77s99m", "9m", false, "四暗刻")]              // ロンなので三暗刻
    [InlineData("123m123p234s456m9p", "9p", false, "三色同順")]            // 索子だけ 234
    [InlineData("123m456m67m456p11s", "8m", false, "一気通貫")]            // 789m ではなく 678m
    public void 役が成立しない(string closed, string win, bool tsumo, string yaku)
    {
        var r = TestHands.Score(TestHands.Ctx(closed, win, tsumo, riichi: true));
        Assert.True(r.IsValid, "和了形として計算されていない");
        Assert.DoesNotContain(yaku, r.Yaku.Select(y => y.Name));
    }

    [Fact]
    public void 喰いタンなしのルールでは副露した断么九は役なし()
    {
        var ctx = TestHands.Ctx("456p678s2345s", "5s", melds: "c234m") with { Rules = new RuleSet { Kuitan = false } };
        Assert.False(TestHands.Score(ctx).IsValid);
    }

    [Fact]
    public void ドラだけでは和了できない()
    {
        var r = TestHands.Score(TestHands.Ctx("13m456m789p234s55p", "2m", doras: "2m"));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void 副露すると食い下がる()
    {
        // 一気通貫: 門前 2 翻 / 副露 1 翻
        var r = TestHands.Score(TestHands.Ctx("456789m456p1s", "1s", melds: "c123m"));
        Assert.Equal(1, r.Yaku.Single(y => y.Name == "一気通貫").Han);
    }

    [Fact]
    public void 状況役()
    {
        var baseCtx = TestHands.Ctx("23m456m678p234s55p", "4m", tsumo: true, riichi: true);
        Assert.Contains("一発", Names(baseCtx with { IsIppatsu = true }));
        Assert.Contains("海底摸月", Names(baseCtx with { IsLastTile = true }));
        Assert.Contains("嶺上開花", Names(baseCtx with { IsRinshan = true }));
        Assert.Contains("両立直", Names(baseCtx with { IsDoubleRiichi = true }));
        Assert.DoesNotContain("立直", Names(baseCtx with { IsDoubleRiichi = true }));
        Assert.Contains("地和", Names(baseCtx with { IsFirstTurn = true }));
        Assert.Contains("天和", Names(baseCtx with { IsFirstTurn = true, SeatWind = TileKind.East }));

        var ron = baseCtx with { IsTsumo = false };
        Assert.Contains("河底撈魚", Names(ron with { IsLastTile = true }));
        Assert.Contains("槍槓", Names(ron with { IsChankan = true }));
    }

    private static IEnumerable<string> Names(WinContext ctx) => TestHands.Score(ctx).Yaku.Select(y => y.Name);
}
