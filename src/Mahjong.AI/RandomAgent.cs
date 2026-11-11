using Mahjong.Core;

namespace Mahjong.AI;

/// <summary>
/// Lv1: ランダム。和了できれば和了し、それ以外は合法手からランダムに選ぶ。
/// </summary>
public sealed class RandomAgent(int seed) : IPlayerAgent
{
    private readonly Random _random = new(seed);

    public Task<TurnAction> ChooseTurnActionAsync(PlayerView view, TurnOptions o)
    {
        if (o.CanTsumo) return Task.FromResult(TurnAction.Tsumo());

        if (o.CanRiichi && _random.Next(2) == 0)
        {
            var riichiTile = o.Discards.Where(t => o.RiichiDiscards.Any(r => r.Kind == t.Kind)).ToList();
            return Task.FromResult(TurnAction.Discard(riichiTile[_random.Next(riichiTile.Count)], riichi: true));
        }

        return Task.FromResult(TurnAction.Discard(o.Discards[_random.Next(o.Discards.Count)]));
    }

    public Task<CallDecision> ChooseCallAsync(PlayerView view, CallOptions o)
    {
        if (o.CanRon) return Task.FromResult(CallDecision.Ron);

        // 鳴けるときは 1 割程度の確率で鳴く
        if (_random.Next(10) == 0)
        {
            if (o.CanPon) return Task.FromResult(CallDecision.Pon());
            if (o.CanChi) return Task.FromResult(CallDecision.Chi(o.ChiCandidates[_random.Next(o.ChiCandidates.Count)]));
        }
        return Task.FromResult(CallDecision.Pass);
    }
}
