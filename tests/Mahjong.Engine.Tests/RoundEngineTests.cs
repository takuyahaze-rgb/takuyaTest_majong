using Mahjong.Core;
using Mahjong.Engine;

namespace Mahjong.Engine.Tests;

/// <summary>牌山と手順を固定して、1 局の進行を再現するテスト。席 0 が親。</summary>
public class RoundEngineTests
{
    private static async Task<(RoundResult Result, GameState State, EventLog Log)> Play(
        Wall wall, IPlayerAgent[] agents, RuleSet? rules = null, int? observerSeat = null)
    {
        var state = new GameState(rules ?? RuleSet.Default) { Wall = wall, Dealer = 0 };
        var hub = new EventHub();
        var log = new EventLog();
        hub.Add(log, observerSeat);
        var result = await new RoundEngine(state, agents, hub: hub).PlayAsync();
        return (result, state, log);
    }

    private static IPlayerAgent[] Tsumogiri(bool canWin = true) =>
        Enumerable.Range(0, 4).Select(_ => (IPlayerAgent)new TsumogiriAgent(canWin)).ToArray();

    // 4 人とも聴牌しない配牌(147m258p369s 東南西北)
    private const string Noten = "147m258p369s1234z";

    // 1m-4m 待ちの平和・断么九の聴牌(Noten と組み合わせても同じ牌が 5 枚にならない)
    private const string Tenpai14m = "23m678m678p456s77s";

    [Fact]
    public async Task 天和_親の配牌とツモで和了()
    {
        var wall = WallBuilder.Build(["123m456p789s1122z", null, null, null], draws: "1z");
        var (r, state, _) = await Play(wall, Tsumogiri());

        Assert.Equal(RoundEndType.Tsumo, r.Type);
        Assert.Equal(0, r.Wins[0].Winner);
        Assert.Contains("天和", r.Wins[0].Score.Yaku.Select(y => y.Name));
        Assert.Equal([48000, -16000, -16000, -16000], r.ScoreDeltas);
        Assert.Equal(25000 + 48000, state.Players[0].Score);
        Assert.True(r.DealerContinues);
    }

    [Fact]
    public async Task ロン_親の打牌を子が和了()
    {
        // 席1 は 1m-4m 待ちの平和聴牌。親が 4m をツモ切りする → 平和・断么九 30符2翻 2000点
        var wall = WallBuilder.Build([Noten, Tenpai14m, Noten, Noten], draws: "4m");
        var (r, _, _) = await Play(wall, Tsumogiri());

        Assert.Equal(RoundEndType.Ron, r.Type);
        Assert.Equal((1, (int?)0), (r.Wins[0].Winner, r.Wins[0].Loser));
        Assert.Equal([-2000, 2000, 0, 0], r.ScoreDeltas);
        Assert.False(r.DealerContinues);
    }

    [Fact]
    public async Task ダブロン_2人とも和了する()
    {
        // 席1 は 2000 点、席2 は平和のみ 1000 点
        var wall = WallBuilder.Build([Noten, Tenpai14m, "23m567m789p345s66p", Noten], draws: "4m");
        var (r, _, _) = await Play(wall, Tsumogiri());

        Assert.Equal(2, r.Wins.Count);
        Assert.Equal([-3000, 2000, 1000, 0], r.ScoreDeltas);
    }

    [Fact]
    public async Task 頭ハネルールでは下家に近い1人だけ和了する()
    {
        var wall = WallBuilder.Build([Noten, Tenpai14m, "23m567m789p345s66p", Noten], draws: "4m");
        var (r, _, _) = await Play(wall, Tsumogiri(), new RuleSet { MultipleRon = MultipleRonRule.HeadBump });

        Assert.Single(r.Wins);
        Assert.Equal(1, r.Wins[0].Winner);
    }

    [Fact]
    public async Task ポンすると手番が飛び_鳴いた人が打牌して次は下家がツモる()
    {
        // 親が白をツモ切り → 席2 がポン
        var wall = WallBuilder.Build([Noten, null, "55z123m456p789s11s", null], draws: "5z");
        var agents = Tsumogiri(canWin: false);
        agents[2] = new ScriptedAgent(call: (_, o) => o.CanPon && o.Tile.Kind == (int)TileKind.White ? CallDecision.Pon() : null);
        var (_, state, log) = await Play(wall, agents);

        int meldIndex = log.Events.FindIndex(e => e is MeldDeclared { Seat: 2 });
        Assert.True(meldIndex > 0);
        Assert.IsType<TileDiscarded>(log.Events[meldIndex + 1]);
        Assert.Equal(2, ((TileDiscarded)log.Events[meldIndex + 1]).Seat);
        Assert.Equal(3, Assert.IsType<TileDrawn>(log.Events[meldIndex + 2]).Seat);
        Assert.True(state.Players[0].River.Discards[0].IsCalled);
        Assert.Equal(MeldType.Pon, state.Players[2].Hand.Melds[0].Type);
    }

    [Fact]
    public async Task チーは下家だけができ_喰い替えの牌は捨てられない()
    {
        // 親が 3m をツモ切り → 下家(席1)が 45m でチー。3m と 6m(スジ)は直後に捨てられない
        var wall = WallBuilder.Build([Noten, "456m258p369s1234z", null, null], draws: "3m");
        var agents = Tsumogiri(canWin: false);
        IReadOnlyList<Tile>? discardsAfterChi = null;
        bool called = false;
        agents[1] = new ScriptedAgent(
            turn: (_, o) => { if (called && discardsAfterChi is null) discardsAfterChi = o.Discards; return null; },
            call: (_, o) =>
            {
                if (called || !o.CanChi) return null;
                called = true;
                return CallDecision.Chi(o.ChiCandidates[0]);
            });
        var (_, state, _) = await Play(wall, agents);

        Assert.Equal(MeldType.Chi, state.Players[1].Hand.Melds[0].Type);
        Assert.NotNull(discardsAfterChi);
        Assert.DoesNotContain(discardsAfterChi!, t => t.Kind is (int)TileKind.M3 or (int)TileKind.M6);
    }

    [Fact]
    public async Task 全員ノーテンで荒牌平局_親は流れる()
    {
        var wall = WallBuilder.Build([Noten, Noten, Noten, Noten]);
        var (r, state, _) = await Play(wall, Tsumogiri());

        Assert.Equal(RoundEndType.ExhaustiveDraw, r.Type);
        Assert.Empty(r.TenpaiSeats);
        Assert.Equal([0, 0, 0, 0], r.ScoreDeltas);
        Assert.False(r.DealerContinues);
        Assert.Equal(0, state.Wall.Remaining);
    }

    [Fact]
    public async Task 流局時のノーテン罰符()
    {
        // 席1 だけ聴牌(4s-7s 待ち)。和了はしない設定にする
        var wall = WallBuilder.Build([Noten, "234m567m999p4477s", Noten, Noten]);
        var (r, _, _) = await Play(wall, Tsumogiri(canWin: false));

        Assert.Equal(RoundEndType.ExhaustiveDraw, r.Type);
        Assert.Equal([1], r.TenpaiSeats);
        Assert.Equal([-1000, 3000, -1000, -1000], r.ScoreDeltas);
    }

    [Fact]
    public async Task リーチするとリーチ棒が出て_和了者が受け取る()
    {
        // 親が 1 巡目にリーチ(1m-4m 待ち)、席1 が 4m をツモ切り → 親がロン
        var wall = WallBuilder.Build([Tenpai14m, Noten, Noten, Noten], draws: "9m4m", doraIndicator: "7z");
        var agents = Tsumogiri();
        agents[0] = new ScriptedAgent(turn: (_, o) => o.CanRiichi ? TurnAction.Discard(o.DrawnTile!.Value, riichi: true) : null);
        var (r, state, log) = await Play(wall, agents);

        Assert.Contains(log.Events, e => e is RiichiAccepted { Seat: 0 });
        Assert.Equal(RoundEndType.Ron, r.Type);
        var yaku = r.Wins[0].Score.Yaku.Select(y => y.Name).ToList();
        Assert.Contains("両立直", yaku);
        Assert.Contains("一発", yaku);
        Assert.Equal(0, state.RiichiSticks);
        // 両立直2+一発1+平和1+断么九1 = 5翻 親満貫 12000、供託 1000 を受け取り、リーチ棒 1000 を出している
        Assert.Equal(12000, r.Wins[0].Score.Payment);
        Assert.Equal(25000 - 1000 + 12000 + 1000, state.Players[0].Score);
    }

    [Fact]
    public async Task 九種九牌で途中流局()
    {
        var wall = WallBuilder.Build(["19m19p19s12345z55m", null, null, null], draws: "6z");
        var agents = Tsumogiri();
        agents[0] = new ScriptedAgent(turn: (_, o) => o.CanKyuushu ? TurnAction.Kyuushu() : null);
        var (r, _, _) = await Play(wall, agents);

        Assert.Equal(RoundEndType.AbortiveDraw, r.Type);
        Assert.Equal("九種九牌", r.AbortiveReason);
        Assert.True(r.DealerContinues);
    }

    [Fact]
    public async Task 暗槓すると嶺上牌をツモり槓ドラが増える()
    {
        var wall = WallBuilder.Build(["1111m258p369s123z", null, null, null], draws: "4z");
        var agents = Tsumogiri(canWin: false);
        bool done = false;
        agents[0] = new ScriptedAgent(turn: (_, o) =>
        {
            if (done || o.AnkanKinds.Count == 0) return null;
            done = true;
            return TurnAction.Ankan(o.AnkanKinds[0]);
        });
        var (_, state, log) = await Play(wall, agents);

        Assert.Contains(log.Events, e => e is MeldDeclared { Seat: 0, Meld.Type: MeldType.Ankan });
        Assert.Contains(log.Events, e => e is TileDrawn { Seat: 0, IsRinshan: true });
        Assert.Contains(log.Events, e => e is DoraRevealed);
        Assert.Equal(2, state.Wall.DoraIndicators.Count);
    }

    [Fact]
    public async Task 他家のツモ牌と配牌は伏せて通知される()
    {
        var wall = WallBuilder.Build([Noten, Noten, Noten, Noten]);
        var (_, _, log) = await Play(wall, Tsumogiri(), observerSeat: 0);

        Assert.All(log.Events.OfType<TileDrawn>(), e => Assert.Equal(e.Seat == 0, e.Tile is not null));
        Assert.All(log.Events.OfType<HandDealt>(), e => Assert.Equal(e.Seat == 0, e.Tiles is not null));
    }

    [Fact]
    public async Task 不正な打牌は例外()
    {
        var wall = WallBuilder.Build([Noten, Noten, Noten, Noten]);
        var agents = Tsumogiri();
        agents[0] = new ScriptedAgent(turn: (_, _) => TurnAction.Discard(new Tile(135)));   // 持っていない中
        await Assert.ThrowsAsync<InvalidOperationException>(() => Play(wall, agents));
    }
}
