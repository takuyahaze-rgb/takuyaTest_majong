namespace Mahjong.Core;

/// <summary>待ち牌(聴牌のとき和了になる牌種)の計算。</summary>
public static class Waits
{
    /// <param name="counts">閉じた牌の枚数配列(13 枚から副露分を引いた枚数)。</param>
    public static IReadOnlyList<int> Calculate(int[] counts, int meldCount)
    {
        // 聴牌でなければ待ちはない(ここで打ち切ると 34 回の計算を省ける)
        if (Shanten.Calculate(counts, meldCount) != 0) return [];

        var waits = new List<int>();
        var c = (int[])counts.Clone();
        for (int k = 0; k < TileKinds.Count; k++)
        {
            if (c[k] >= 4) continue;   // 自分で 4 枚持っている牌は待てない
            c[k]++;
            if (Shanten.Calculate(c, meldCount) == -1) waits.Add(k);
            c[k]--;
        }
        return waits;
    }

    public static IReadOnlyList<int> Calculate(Hand hand) => Calculate(hand.ToCounts(), hand.Melds.Count);

    public static bool IsTenpai(Hand hand) => Calculate(hand).Count > 0;
}
