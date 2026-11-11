using Mahjong.Core;
using Mahjong.Engine;

namespace Mahjong.Engine.Tests;

/// <summary>
/// 配牌とツモ順を指定して牌山を組み立てる(牌譜の再現用)。
/// 指定しなかった場所には、使われていない牌を ID 順に詰める。
/// </summary>
public static class WallBuilder
{
    /// <param name="hands">席ごとの配牌 13 枚(null なら自動)。</param>
    /// <param name="draws">配牌後のツモ順(親から順に 1 枚ずつ)。</param>
    /// <param name="doraIndicator">ドラ表示牌(null なら自動)。</param>
    public static Wall Build(string?[] hands, string draws = "", string? doraIndicator = null, int dealer = 0)
    {
        var used = new HashSet<int>();
        var handTiles = hands.Select(h => h is null ? null : TileParser.Parse(h, used)).ToArray();
        var drawTiles = TileParser.Parse(draws, used);
        Tile? dora = doraIndicator is null ? null : TileParser.ParseOne(doraIndicator, used);

        var order = new Tile?[Wall.TotalTiles];
        int pos = 0;
        for (int round = 0; round < 4; round++)
        {
            int count = round < 3 ? 4 : 1;
            for (int i = 0; i < 4; i++)
            {
                var hand = handTiles[(dealer + i) % 4];
                for (int j = 0; j < count; j++, pos++)
                    if (hand is not null) order[pos] = hand[round * 4 + j];
            }
        }
        foreach (var t in drawTiles) order[pos++] = t;
        if (dora is Tile d) order[Wall.TotalTiles - Wall.DeadWallSize + 4] = d;

        var unused = new Queue<Tile>(Wall.CreateAllTiles().Where(t => !used.Contains(t.Id)));
        for (int i = 0; i < order.Length; i++) order[i] ??= unused.Dequeue();
        return new Wall(order.Select(t => t!.Value).ToList());
    }
}

/// <summary>ツモ切りだけするエージェント。canWin が true なら和了できるときは和了する。</summary>
public sealed class TsumogiriAgent(bool canWin = true) : IPlayerAgent
{
    public Task<TurnAction> ChooseTurnActionAsync(PlayerView view, TurnOptions o)
    {
        if (canWin && o.CanTsumo) return Task.FromResult(TurnAction.Tsumo());
        var tile = o.DrawnTile is Tile d && o.Discards.Any(t => t.Id == d.Id) ? d : o.Discards[0];
        return Task.FromResult(TurnAction.Discard(tile));
    }

    public Task<CallDecision> ChooseCallAsync(PlayerView view, CallOptions o) =>
        Task.FromResult(canWin && o.CanRon ? CallDecision.Ron : CallDecision.Pass);
}

/// <summary>決められた手順で動くエージェント。関数が null を返したらツモ切り・パスする。</summary>
public sealed class ScriptedAgent(
    Func<PlayerView, TurnOptions, TurnAction?>? turn = null,
    Func<PlayerView, CallOptions, CallDecision?>? call = null) : IPlayerAgent
{
    private readonly TsumogiriAgent _fallback = new();

    public Task<TurnAction> ChooseTurnActionAsync(PlayerView view, TurnOptions o) =>
        turn?.Invoke(view, o) is { } a ? Task.FromResult(a) : _fallback.ChooseTurnActionAsync(view, o);

    public Task<CallDecision> ChooseCallAsync(PlayerView view, CallOptions o) =>
        call?.Invoke(view, o) is { } c ? Task.FromResult(c) : _fallback.ChooseCallAsync(view, o);
}

/// <summary>
/// シミュレーション用の簡単なエージェント。向聴数が最小になる牌を切り、聴牌したらリーチ、
/// 三元牌はポンし、槓できるときは槓する(エンジンの分岐をなるべく多く通すため)。
/// </summary>
public sealed class GreedyAgent : IPlayerAgent
{
    public Task<TurnAction> ChooseTurnActionAsync(PlayerView view, TurnOptions o)
    {
        if (o.CanTsumo) return Task.FromResult(TurnAction.Tsumo());
        if (o.AnkanKinds.Count > 0) return Task.FromResult(TurnAction.Ankan(o.AnkanKinds[0]));
        if (o.KakanKinds.Count > 0) return Task.FromResult(TurnAction.Kakan(o.KakanKinds[0]));
        if (o.CanRiichi) return Task.FromResult(TurnAction.Discard(o.Discards.First(t => o.RiichiDiscards.Any(r => r.Kind == t.Kind)), riichi: true));

        var counts = view.ClosedCounts();
        Tile best = o.Discards[0];
        int bestShanten = int.MaxValue;
        foreach (var t in o.Discards)
        {
            counts[t.Kind]--;
            int s = Shanten.Calculate(counts, view.Melds.Count);
            counts[t.Kind]++;
            if (s < bestShanten || (s == bestShanten && t.IsHonor && !best.IsHonor))
            {
                bestShanten = s;
                best = t;
            }
        }
        return Task.FromResult(TurnAction.Discard(best));
    }

    public Task<CallDecision> ChooseCallAsync(PlayerView view, CallOptions o)
    {
        if (o.CanRon) return Task.FromResult(CallDecision.Ron);
        if (o.CanPon && TileKinds.IsDragon(o.Tile.Kind)) return Task.FromResult(CallDecision.Pon());
        return Task.FromResult(CallDecision.Pass);
    }
}

/// <summary>受け取ったイベントを記録する観測者。</summary>
public sealed class EventLog : IGameObserver
{
    public List<GameEvent> Events { get; } = new();
    public void OnEvent(GameEvent e) => Events.Add(e);
}
