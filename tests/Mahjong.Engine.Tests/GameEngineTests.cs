using Mahjong.Core;
using Mahjong.Engine;

namespace Mahjong.Engine.Tests;

public class GameEngineTests
{
    private static GameEngine CreateGreedyGame(int seed, RuleSet? rules = null) =>
        new(Enumerable.Range(0, 4).Select(_ => (IPlayerAgent)new GreedyAgent()).ToArray(), seed, rules);

    /// <summary>シード固定で半荘を繰り返し、例外なく終わり、点数の合計が常に 10 万点であることを確かめる。</summary>
    [Fact]
    public async Task 半荘を何度回しても点数の合計は10万点()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var log = new EventLog();
            var game = CreateGreedyGame(seed);
            game.AddObserver(log);
            var result = await game.PlayAsync();

            Assert.Equal(100000, result.FinalScores.Sum());
            Assert.Equal(4, result.Ranking.Distinct().Count());
            // 局の途中でも、持ち点+供託の合計は常に 10 万点
            foreach (var e in log.Events.OfType<RoundStarted>())
                Assert.Equal(100000, e.Scores.Sum() + e.RiichiSticks * 1000);
        }
    }

    [Fact]
    public async Task 同じシードなら同じ結果になる()
    {
        var a = await CreateGreedyGame(12345).PlayAsync();
        var b = await CreateGreedyGame(12345).PlayAsync();
        Assert.Equal(a.FinalScores, b.FinalScores);
        Assert.Equal(a.RoundsPlayed, b.RoundsPlayed);
    }

    [Fact]
    public async Task 東風戦は東4局までに終わる()
    {
        for (int seed = 1; seed <= 10; seed++)
        {
            var log = new EventLog();
            var game = CreateGreedyGame(seed, new RuleSet { Length = GameLength.Tonpuu });
            game.AddObserver(log);
            await game.PlayAsync();
            Assert.All(log.Events.OfType<RoundStarted>(), e => Assert.Equal(TileKind.East, e.RoundWind));
        }
    }

    [Fact]
    public async Task 子の和了で本場はリセット_流局で本場が増える()
    {
        var log = new EventLog();
        var game = CreateGreedyGame(7);
        game.AddObserver(log);
        await game.PlayAsync();

        var starts = log.Events.OfType<RoundStarted>().ToList();
        var ends = log.Events.OfType<RoundEnded>().ToList();
        for (int i = 0; i + 1 < starts.Count; i++)
        {
            var r = ends[i].Result;
            bool childWon = r.Type is RoundEndType.Tsumo or RoundEndType.Ron && !r.DealerContinues;
            Assert.Equal(childWon ? 0 : starts[i].Honba + 1, starts[i + 1].Honba);
            Assert.Equal(r.DealerContinues, starts[i + 1].Dealer == starts[i].Dealer);
        }
    }
}
