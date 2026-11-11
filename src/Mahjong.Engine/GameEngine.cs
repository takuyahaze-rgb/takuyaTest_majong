using Mahjong.Core;

namespace Mahjong.Engine;

/// <summary>
/// 半荘(または東風戦)の進行。局を繰り返し、親・本場・供託を管理する。
/// 席 0 を起家とする。
/// </summary>
public sealed class GameEngine
{
    private readonly IReadOnlyList<IPlayerAgent> _agents;
    private readonly ScoreCalculator _calc;
    private readonly int _seed;

    public GameEngine(IReadOnlyList<IPlayerAgent> agents, int seed, RuleSet? rules = null, ScoreCalculator? calculator = null)
    {
        if (agents.Count != GameState.PlayerCount) throw new ArgumentException("エージェントは 4 人分必要です。");
        _agents = agents;
        _seed = seed;
        _calc = calculator ?? ScoreCalculator.CreateDefault();
        State = new GameState(rules ?? RuleSet.Default);
    }

    public GameState State { get; }

    public EventHub Events { get; } = new();

    /// <param name="seat">その席の視点で見る(null なら全員の牌が見える)。</param>
    public void AddObserver(IGameObserver observer, int? seat = null) => Events.Add(observer, seat);

    public async Task<GameResult> PlayAsync()
    {
        var s = State;
        var random = new Random(_seed);   // 局ごとの牌山のシードを作る(同じシードなら半荘全体を再現できる)
        int lastRoundIndex = s.Rules.Length == GameLength.Tonpuu ? 3 : 7;
        int roundIndex = 0;   // 0 = 東1局, 4 = 南1局
        int roundsPlayed = 0;

        Events.Publish(new GameStarted(s.Players.Select(p => p.Score).ToList()));

        while (true)
        {
            s.RoundWind = (TileKind)(27 + roundIndex / 4);
            s.RoundNumber = roundIndex % 4 + 1;
            s.Dealer = roundIndex % 4;
            s.Wall = new Wall(random.Next(), s.Rules);

            var result = await new RoundEngine(s, _agents, _calc, Events).PlayAsync();
            roundsPlayed++;

            // 誰かの持ち点がマイナスになったら終了(トビ)
            if (s.Players.Any(p => p.Score < 0)) break;

            bool childWon = result.Type is RoundEndType.Tsumo or RoundEndType.Ron && !result.DealerContinues;
            s.Honba = childWon ? 0 : s.Honba + 1;
            if (!result.DealerContinues) roundIndex++;
            if (roundIndex > lastRoundIndex) break;
        }

        // 残った供託は 1 位が受け取る
        var ranking = Enumerable.Range(0, GameState.PlayerCount)
            .OrderByDescending(i => s.Players[i].Score).ThenBy(i => i).ToList();
        s.Players[ranking[0]].Score += s.RiichiSticks * 1000;
        s.RiichiSticks = 0;

        var gameResult = new GameResult(s.Players.Select(p => p.Score).ToList(), ranking, roundsPlayed);
        Events.Publish(new GameEnded(gameResult));
        return gameResult;
    }
}
