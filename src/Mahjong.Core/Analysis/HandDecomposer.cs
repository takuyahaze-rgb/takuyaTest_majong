namespace Mahjong.Core;

/// <summary>
/// 和了形を面子・雀頭に分解する。分解と待ちの解釈を「すべて」列挙し、
/// 点数計算側で点数が最大になるものを選ぶ。
/// </summary>
public static class HandDecomposer
{
    private static readonly int[] TerminalAndHonorKinds = [0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33];

    /// <summary>和了の状況から分解を列挙する。</summary>
    public static IReadOnlyList<Decomposition> Enumerate(WinContext ctx) =>
        Enumerate(ctx.Hand, ctx.WinTile, ctx.IsTsumo);

    /// <summary>
    /// 手牌(和了牌を含まない閉じた牌+副露)と和了牌から分解を列挙する。
    /// 和了形でなければ空のリストを返す。
    /// </summary>
    public static IReadOnlyList<Decomposition> Enumerate(Hand hand, Tile winTile, bool isTsumo)
    {
        var counts = hand.ToCounts();
        counts[winTile.Kind]++;
        return Enumerate(counts, hand.Melds, winTile.Kind, isTsumo);
    }

    /// <param name="counts">閉じた牌+和了牌の 34 要素の枚数配列。</param>
    public static IReadOnlyList<Decomposition> Enumerate(int[] counts, IReadOnlyList<Meld> melds, int winKind, bool isTsumo)
    {
        var results = new List<Decomposition>();
        int total = counts.Sum();
        if (total != 14 - melds.Count * 3) return results;

        // 1. 特殊形(七対子・国士無双)を先に判定する
        if (melds.Count == 0)
        {
            if (IsChiitoitsu(counts))
            {
                var pairs = Enumerable.Range(0, TileKinds.Count).Where(k => counts[k] == 2).ToList();
                results.Add(new Decomposition(HandForm.Chiitoitsu, -1, [], WaitType.Tanki, pairs));
            }
            if (IsKokushi(counts))
            {
                int pair = TerminalAndHonorKinds.First(k => counts[k] == 2);
                results.Add(new Decomposition(HandForm.Kokushi, pair, [], WaitType.Tanki, []));
            }
        }

        // 2. 雀頭候補を 1 つ決め、残りを刻子・順子に再帰分解する
        var meldBlocks = melds.Select(ToBlock).ToList();
        var work = (int[])counts.Clone();
        var seen = new HashSet<string>();
        for (int pair = 0; pair < TileKinds.Count; pair++)
        {
            if (work[pair] < 2) continue;
            work[pair] -= 2;
            var closedSets = new List<List<Block>>();
            Split(work, 0, new List<Block>(), closedSets);
            work[pair] += 2;

            // 3. 副露済みの面子を足し、和了牌の位置ごとに待ちを決める
            foreach (var closed in closedSets)
                AddWaitVariants(pair, closed, meldBlocks, winKind, isTsumo, results, seen);
        }
        return results;
    }

    /// <summary>和了形か(副露数を考慮した 14 枚相当の枚数配列で判定)。</summary>
    public static bool IsAgari(int[] counts, int meldCount = 0) => Shanten.Calculate(counts, meldCount) == -1;

    public static bool IsChiitoitsu(int[] counts) =>
        counts.Count(c => c == 2) == 7 && counts.All(c => c is 0 or 2);

    public static bool IsKokushi(int[] counts)
    {
        if (counts.Sum() != 14) return false;
        int pairs = 0;
        foreach (int k in TerminalAndHonorKinds)
        {
            if (counts[k] == 0) return false;
            if (counts[k] == 2) pairs++;
        }
        return pairs == 1;
    }

    private static Block ToBlock(Meld m) => m.Type switch
    {
        MeldType.Chi => new Block(BlockType.Shuntsu, m.Kind, false, true),
        MeldType.Pon => new Block(BlockType.Koutsu, m.Kind, false, true),
        MeldType.Ankan => new Block(BlockType.Kantsu, m.Kind, true, false),
        _ => new Block(BlockType.Kantsu, m.Kind, false, true),   // 大明槓・加槓
    };

    // 閉じた牌を刻子・順子だけに分解する(全通り)。
    private static void Split(int[] c, int i, List<Block> acc, List<List<Block>> results)
    {
        while (i < TileKinds.Count && c[i] == 0) i++;
        if (i == TileKinds.Count) { results.Add(new List<Block>(acc)); return; }

        if (c[i] >= 3)
        {
            c[i] -= 3;
            acc.Add(new Block(BlockType.Koutsu, i, true, false));
            Split(c, i, acc, results);
            acc.RemoveAt(acc.Count - 1);
            c[i] += 3;
        }
        if (i < 27 && i % 9 <= 6 && c[i + 1] > 0 && c[i + 2] > 0)
        {
            c[i]--; c[i + 1]--; c[i + 2]--;
            acc.Add(new Block(BlockType.Shuntsu, i, true, false));
            Split(c, i, acc, results);
            acc.RemoveAt(acc.Count - 1);
            c[i]++; c[i + 1]++; c[i + 2]++;
        }
        // どちらも作れなければ分解失敗(何も追加しない)
    }

    private static void AddWaitVariants(
        int pair, List<Block> closed, List<Block> meldBlocks, int winKind, bool isTsumo,
        List<Decomposition> results, HashSet<string> seen)
    {
        void Add(List<Block> blocks, WaitType wait)
        {
            var all = blocks.Concat(meldBlocks).ToList();
            string key = $"{pair}|{wait}|{string.Join(",", all.Select(b => $"{b.Type}{b.Kind}{b.IsConcealed}"))}";
            if (seen.Add(key)) results.Add(new Decomposition(HandForm.Standard, pair, all, wait, []));
        }

        // 単騎待ち
        if (pair == winKind) Add(closed, WaitType.Tanki);

        for (int j = 0; j < closed.Count; j++)
        {
            var b = closed[j];
            if (!b.Contains(winKind)) continue;

            if (b.Type == BlockType.Koutsu)
            {
                // ロンで完成した刻子は明刻扱い
                var copy = new List<Block>(closed) { [j] = b with { IsConcealed = isTsumo } };
                Add(copy, WaitType.Shanpon);
                continue;
            }

            int pos = winKind - b.Kind;
            int startNumber = TileKinds.NumberOf(b.Kind);
            WaitType wait = pos switch
            {
                1 => WaitType.Kanchan,
                2 when startNumber == 1 => WaitType.Penchan,   // 12 で 3 待ち
                0 when startNumber == 7 => WaitType.Penchan,   // 89 で 7 待ち
                _ => WaitType.Ryanmen,
            };
            Add(closed, wait);
        }
    }
}
