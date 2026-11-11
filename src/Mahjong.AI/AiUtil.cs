using Mahjong.Core;

namespace Mahjong.AI;

/// <summary>AI で共通に使う計算。</summary>
public static class AiUtil
{
    /// <summary>自分から見えていない牌の残り枚数(34 種)。自分の手牌・全員の河と副露・ドラ表示牌を除く。</summary>
    public static int[] UnseenCounts(PlayerView view)
    {
        var seen = new int[TileKinds.Count];
        foreach (var t in view.Closed) seen[t.Kind]++;
        foreach (var p in view.Players)
        {
            // 鳴かれた牌は副露の側で数えるので、河では数えない
            foreach (var d in p.River) if (!d.IsCalled) seen[d.Tile.Kind]++;
            foreach (var m in p.Melds) foreach (var t in m.Tiles) seen[t.Kind]++;
        }
        foreach (var t in view.DoraIndicators) seen[t.Kind]++;
        return seen.Select(s => Math.Max(0, 4 - s)).ToArray();
    }

    /// <summary>
    /// 受け入れ枚数: 引くと向聴数が下がる牌の、見えていない残り枚数の合計。
    /// </summary>
    /// <param name="counts">打牌後の手牌(13 枚相当)の枚数配列。</param>
    public static int Ukeire(int[] counts, int meldCount, int[] unseen, int shanten)
    {
        int total = 0;
        for (int k = 0; k < TileKinds.Count; k++)
        {
            if (unseen[k] == 0 || counts[k] >= 4) continue;
            counts[k]++;
            if (Shanten.Calculate(counts, meldCount) < shanten) total += unseen[k];
            counts[k]--;
        }
        return total;
    }

    /// <summary>その牌種が自分にとって役牌か(三元牌・自風・場風)。</summary>
    public static bool IsYakuhai(PlayerView view, int kind) =>
        TileKinds.IsDragon(kind) || kind == (int)view.SeatWind || kind == (int)view.RoundWind;

    /// <summary>
    /// 孤立度: 大きいほど先に切りたい牌。孤立した字牌(役牌以外)> 孤立した役牌 > 孤立した老頭牌 > 孤立した中張牌 > それ以外。
    /// </summary>
    public static int Isolation(PlayerView view, int[] counts, int kind)
    {
        if (TileKinds.IsHonor(kind))
            return counts[kind] == 1 ? (IsYakuhai(view, kind) ? 3 : 4) : 0;

        int n = TileKinds.NumberOf(kind);
        bool hasNeighbor = false;
        for (int d = -2; d <= 2; d++)
        {
            if (d == 0 || n + d < 1 || n + d > 9) continue;
            if (counts[kind + d] > 0) { hasNeighbor = true; break; }
        }
        if (hasNeighbor || counts[kind] > 1) return 0;
        return TileKinds.IsTerminal(kind) ? 2 : 1;
    }
}
