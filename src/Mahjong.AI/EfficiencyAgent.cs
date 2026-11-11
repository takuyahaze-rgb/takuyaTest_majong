using Mahjong.Core;

namespace Mahjong.AI;

/// <summary>
/// Lv2: 牌効率。打牌候補ごとに打牌後の向聴数と受け入れ枚数を求め、
/// 「向聴数が小さい → 受け入れ枚数が多い → 孤立した字牌・端牌を先に切る」の順で選ぶ。
/// 門前で聴牌したらリーチ。鳴きは役牌のポンと、役牌を鳴いた後の前進する鳴きだけ行う。
/// </summary>
public class EfficiencyAgent : IPlayerAgent
{
    public virtual Task<TurnAction> ChooseTurnActionAsync(PlayerView view, TurnOptions o)
    {
        if (o.CanTsumo) return Task.FromResult(TurnAction.Tsumo());

        var counts = view.ClosedCounts();
        int melds = view.Melds.Count;

        // 九種九牌: 国士無双が遠ければ流す
        if (o.CanKyuushu && Shanten.Kokushi(counts) > 3) return Task.FromResult(TurnAction.Kyuushu());

        var best = ChooseDiscard(view, o.Discards, counts, melds);

        // 槓: 槓しても向聴数が悪くならないなら槓する(リーチ中はエンジンが待ちが変わらない槓だけを許す)
        foreach (int k in o.AnkanKinds)
        {
            counts[k] -= 4;
            int s = Shanten.Calculate(counts, melds + 1);
            counts[k] += 4;
            if (view.IsRiichi || s <= best.Shanten) return Task.FromResult(TurnAction.Ankan(k));
        }
        foreach (int k in o.KakanKinds)
        {
            counts[k] -= 1;
            int s = Shanten.Calculate(counts, melds);
            counts[k] += 1;
            if (s <= best.Shanten) return Task.FromResult(TurnAction.Kakan(k));
        }

        // 門前で聴牌できるならリーチ(リーチ可能な牌の中で最も良い打牌)
        if (o.CanRiichi)
        {
            var riichiCandidates = o.Discards.Where(t => o.RiichiDiscards.Any(r => r.Kind == t.Kind)).ToList();
            var r = ChooseDiscard(view, riichiCandidates, counts, melds);
            return Task.FromResult(TurnAction.Discard(r.Tile, riichi: true));
        }

        return Task.FromResult(TurnAction.Discard(best.Tile));
    }

    public virtual Task<CallDecision> ChooseCallAsync(PlayerView view, CallOptions o)
    {
        if (o.CanRon) return Task.FromResult(CallDecision.Ron);

        var counts = view.ClosedCounts();
        int melds = view.Melds.Count;
        int current = Shanten.Calculate(counts, melds);
        int kind = o.Tile.Kind;

        // 役牌は必ずポン(役が確定する)
        if (o.CanPon && AiUtil.IsYakuhai(view, kind)) return Task.FromResult(CallDecision.Pon());

        // 役牌を鳴いて役が確定しているなら、向聴数が進む鳴きはする
        bool hasYaku = view.Melds.Any(m => m.Type != MeldType.Chi && AiUtil.IsYakuhai(view, m.Kind));
        if (!hasYaku) return Task.FromResult(CallDecision.Pass);

        if (o.CanPon && ShantenAfterCall(counts, melds, [kind, kind]) < current)
            return Task.FromResult(CallDecision.Pon());

        foreach (var pair in o.ChiCandidates)
            if (ShantenAfterCall(counts, melds, pair.Select(t => t.Kind).ToArray()) < current)
                return Task.FromResult(CallDecision.Chi(pair));

        return Task.FromResult(CallDecision.Pass);
    }

    protected readonly record struct DiscardChoice(Tile Tile, int Shanten, int Ukeire);

    /// <summary>打牌候補から最も良い牌を選ぶ。</summary>
    protected static DiscardChoice ChooseDiscard(PlayerView view, IReadOnlyList<Tile> candidates, int[] counts, int melds)
    {
        var unseen = AiUtil.UnseenCounts(view);
        DiscardChoice? best = null;
        int bestIsolation = -1;

        // 同じ種類の牌は 1 回だけ評価する(赤ドラは残したいので、通常の牌を優先して候補にする)
        foreach (var tile in candidates.OrderBy(t => t.IsRed).GroupBy(t => t.Kind).Select(g => g.First()))
        {
            counts[tile.Kind]--;
            int s = Shanten.Calculate(counts, melds);
            int uke = AiUtil.Ukeire(counts, melds, unseen, s);
            counts[tile.Kind]++;
            int iso = AiUtil.Isolation(view, counts, tile.Kind);

            bool better = best is not { } b
                || s < b.Shanten
                || (s == b.Shanten && uke > b.Ukeire)
                || (s == b.Shanten && uke == b.Ukeire && iso > bestIsolation);
            if (better)
            {
                best = new DiscardChoice(tile, s, uke);
                bestIsolation = iso;
            }
        }
        return best!.Value;
    }

    /// <summary>手牌から used の牌を抜いて鳴いた後、最良の打牌をしたときの向聴数。</summary>
    private static int ShantenAfterCall(int[] counts, int melds, int[] used)
    {
        var c = (int[])counts.Clone();
        foreach (int k in used) c[k]--;
        int best = int.MaxValue;
        for (int k = 0; k < TileKinds.Count; k++)
        {
            if (c[k] == 0) continue;
            c[k]--;
            best = Math.Min(best, Shanten.Calculate(c, melds + 1));
            c[k]++;
        }
        return best;
    }
}
