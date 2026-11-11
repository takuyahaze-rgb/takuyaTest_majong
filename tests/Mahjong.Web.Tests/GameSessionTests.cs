using Mahjong.AI;
using Mahjong.Core;
using Mahjong.Engine;
using Mahjong.Web.Game;

namespace Mahjong.Web.Tests;

/// <summary>
/// ブラウザ版の進行役を、画面の代わりに自動で入力して動かす。
/// 入力待ち → タップ相当の Submit → 局の結果で一時停止 → 「次へ」 の流れが最後まで回ることを確かめる。
/// </summary>
public class GameSessionTests
{
    private static GameSession CreateSession() =>
        new(new AgentFactory(), ScoreCalculator.CreateDefault()) { CpuDelayMs = 0 };

    /// <summary>画面の代わりに、入力待ちになったら自動で操作する。</summary>
    private static void AutoPlay(GameSession session, Func<TurnOptions, TurnAction> turn, List<string> trace)
    {
        session.Changed += () =>
        {
            var human = session.Human;
            if (human?.Turn is { } o) { trace.Add("turn"); human.Submit(turn(o)); }
            else if (human?.Call is { } c) { trace.Add("call"); human.Submit(c.CanRon ? CallDecision.Ron : CallDecision.Pass); }
            else if (session.RoundResult is not null) { trace.Add("result"); session.NextRound(); }
        };
    }

    [Fact]
    public async Task ツモ切りし続けて東風戦が最後まで進む()
    {
        var session = CreateSession();
        var trace = new List<string>();
        AutoPlay(session, o => o.CanTsumo ? TurnAction.Tsumo()
            : TurnAction.Discard(o.DrawnTile is Tile d && o.Discards.Any(t => t.Id == d.Id) ? d : o.Discards[0]), trace);

        var run = session.StartAsync(GameLength.Tonpuu, AiLevel.Efficiency, seed: 42);
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.Same(run, finished);
        Assert.Null(session.Error);
        Assert.False(session.IsRunning);
        Assert.NotNull(session.FinalResult);
        Assert.Equal(100000, session.FinalResult!.FinalScores.Sum());
        Assert.Contains("turn", trace);
        Assert.Equal(session.FinalResult.RoundsPlayed, trace.Count(t => t == "result"));
    }

    [Fact]
    public async Task 局の結果を表示している間は次の局に進まない()
    {
        var session = CreateSession();
        int resultsShown = 0;
        session.Changed += () =>
        {
            var human = session.Human;
            if (human?.Turn is { } o) human.Submit(o.CanTsumo ? TurnAction.Tsumo() : TurnAction.Discard(o.Discards[^1]));
            else if (human?.Call is { } c) human.Submit(CallDecision.Pass);
            else if (session.RoundResult is not null) resultsShown++;   // 「次へ」を押さない
        };

        _ = session.StartAsync(GameLength.Tonpuu, AiLevel.Efficiency, seed: 7);
        for (int i = 0; i < 200 && session.RoundResult is null; i++) await Task.Delay(50);
        Assert.NotNull(session.RoundResult);

        // しばらく待っても、局の結果が表示されたまま次の局が始まらない
        var view = session.View!;
        await Task.Delay(500);
        Assert.NotNull(session.RoundResult);
        Assert.Equal(view.RoundNumber, session.View!.RoundNumber);
        Assert.Equal(view.Honba, session.View!.Honba);

        session.Quit();
        Assert.Null(session.Engine);
    }
}
