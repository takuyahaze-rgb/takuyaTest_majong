using Mahjong.AI;
using Mahjong.Core;
using Mahjong.Engine;

namespace Mahjong.AI.Tests;

public class EfficiencyAgentTests
{
    /// <summary>席 0 の手牌を指定して、その視点の PlayerView を作る。</summary>
    private static PlayerView ViewOf(string closed)
    {
        var state = new GameState(RuleSet.Default) { Wall = new Wall(1) };
        state.Players[0].Hand.Closed.AddRange(TileParser.Parse(closed));
        return PlayerView.Create(state, 0);
    }

    private static TurnOptions OptionsFor(PlayerView view, IReadOnlyList<Tile>? riichi = null) =>
        new(view.Closed[^1], null, view.Closed, riichi ?? [], [], [], false);

    [Fact]
    public async Task 聴牌に取れる打牌を選ぶ()
    {
        // 4z を切れば 8m 嵌張待ちの聴牌。7m・9m を切ると一向聴に戻る
        var view = ViewOf("123m456p789s55m79m4z");
        var action = await new EfficiencyAgent().ChooseTurnActionAsync(view, OptionsFor(view));
        Assert.Equal((int)TileKind.North, action.Tile!.Value.Kind);
        Assert.False(action.Riichi);
    }

    [Fact]
    public async Task 向聴数が同じなら受け入れの広い方を残す()
    {
        // 北を切っても 5s を切っても向聴数は同じ。5s を残す方が受け入れ(3s〜7s)が広いので北を切る
        var view = ViewOf("123m456p789s11s4z5s");
        var action = await new EfficiencyAgent().ChooseTurnActionAsync(view, OptionsFor(view));
        Assert.Equal((int)TileKind.North, action.Tile!.Value.Kind);
    }

    [Fact]
    public async Task 門前で聴牌できるならリーチする()
    {
        var view = ViewOf("123m456p789s55m79m4z");
        var riichi = view.Closed.Where(t => t.Kind == (int)TileKind.North).ToList();
        var action = await new EfficiencyAgent().ChooseTurnActionAsync(view, OptionsFor(view, riichi));
        Assert.True(action.Riichi);
        Assert.Equal((int)TileKind.North, action.Tile!.Value.Kind);
    }

    [Fact]
    public async Task 役牌はポンし_それ以外は鳴かない()
    {
        var view = ViewOf("55z123m456p789s1s4z");
        var agent = new EfficiencyAgent();
        var white = new Tile(TileParser.ParseOne("白").Id + 3);
        var pon = await agent.ChooseCallAsync(view, new CallOptions(white, 3, null, true, false, [], false));
        Assert.Equal(CallType.Pon, pon.Type);

        var view2 = ViewOf("22m123m456p789s1s4z");
        var twoMan = new Tile(TileParser.ParseOne("2m").Id + 3);
        var pass = await agent.ChooseCallAsync(view2, new CallOptions(twoMan, 3, null, true, false, [], false));
        Assert.Equal(CallType.Pass, pass.Type);
    }

    [Fact]
    public void 受け入れ枚数_シャンポン待ちは4枚()
    {
        var counts = new int[34];
        foreach (var t in TileParser.Parse("123m456p789s1122z")) counts[t.Kind]++;
        var unseen = Enumerable.Repeat(4, 34).ToArray();
        for (int k = 0; k < 34; k++) unseen[k] -= counts[k];
        Assert.Equal(4, AiUtil.Ukeire(counts, 0, unseen, Shanten.Calculate(counts)));
    }
}

public class AiMatchTests
{
    /// <summary>Lv2 1 人と Lv1 3 人で対戦し、席を回しながら Lv2 の平均順位が良いことを確かめる。</summary>
    [Fact]
    public async Task Lv2はLv1より強い()
    {
        var factory = new AgentFactory();
        double rankSum = 0;
        const int games = 20;
        for (int g = 0; g < games; g++)
        {
            int lv2Seat = g % 4;
            var agents = Enumerable.Range(0, 4)
                .Select(i => factory.Create(i == lv2Seat ? AiLevel.Efficiency : AiLevel.Random, seed: g * 10 + i))
                .ToArray();
            var result = await new GameEngine(agents, seed: g).PlayAsync();
            Assert.Equal(100000, result.FinalScores.Sum());
            rankSum += result.Ranking.ToList().IndexOf(lv2Seat) + 1;
        }
        Assert.True(rankSum / games < 2.0, $"Lv2 の平均順位 {rankSum / games:F2}");
    }

    [Fact]
    public async Task Lv2同士で半荘を回しても例外なく終わる()
    {
        var factory = new AgentFactory();
        for (int g = 0; g < 10; g++)
        {
            var agents = Enumerable.Range(0, 4).Select(_ => factory.Create(AiLevel.Efficiency)).ToArray();
            var result = await new GameEngine(agents, seed: 100 + g).PlayAsync();
            Assert.Equal(100000, result.FinalScores.Sum());
        }
    }
}
