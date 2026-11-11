using System.Collections.Concurrent;

namespace Mahjong.Core;

/// <summary>
/// 向聴数の計算。0 が聴牌、-1 が和了形。
/// 面子手・七対子・国士無双をそれぞれ計算して最小値を取る。
/// </summary>
/// <remarks>
/// 面子手は「萬子・筒子・索子・字牌」の 4 グループに分け、グループごとに
/// 「雀頭の有無 × 面子数 → 最大の搭子数」の表を作ってから組み合わせる。
/// グループの表は枚数の並びごとにキャッシュするので、AI が何度呼んでも速い。
/// </remarks>
public static class Shanten
{
    private static readonly int[] TerminalAndHonorKinds = [0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33];

    // キー: グループ内の枚数を 5 進数で表した値。値: [雀頭有無 * 5 + 面子数] → 最大搭子数(-1 は不可能)
    private static readonly ConcurrentDictionary<int, int[]> SuitCache = new();
    private static readonly ConcurrentDictionary<int, int[]> HonorCache = new();

    /// <param name="counts">閉じた牌の 34 要素の枚数配列(13 枚または 14 枚から副露分を引いた枚数)。</param>
    /// <param name="meldCount">副露・槓の数。</param>
    public static int Calculate(int[] counts, int meldCount = 0)
    {
        int best = Normal(counts, meldCount);
        if (meldCount == 0)
        {
            best = Math.Min(best, Chiitoitsu(counts));
            best = Math.Min(best, Kokushi(counts));
        }
        return best;
    }

    public static int Calculate(Hand hand) => Calculate(hand.ToCounts(), hand.Melds.Count);

    /// <summary>七対子の向聴数。</summary>
    public static int Chiitoitsu(int[] counts)
    {
        int pairs = 0, kinds = 0;
        foreach (int c in counts)
        {
            if (c >= 1) kinds++;
            if (c >= 2) pairs++;
        }
        return 6 - pairs + Math.Max(0, 7 - kinds);
    }

    /// <summary>国士無双の向聴数。</summary>
    public static int Kokushi(int[] counts)
    {
        int unique = 0;
        bool hasPair = false;
        foreach (int k in TerminalAndHonorKinds)
        {
            if (counts[k] >= 1) unique++;
            if (counts[k] >= 2) hasPair = true;
        }
        return 13 - unique - (hasPair ? 1 : 0);
    }

    /// <summary>面子手(4 面子 1 雀頭)の向聴数。</summary>
    public static int Normal(int[] counts, int meldCount = 0)
    {
        // dp[雀頭有無 * 5 + 面子数] = 最大搭子数
        Span<int> dp = stackalloc int[10];
        Span<int> next = stackalloc int[10];
        dp.Fill(-1);
        dp[Math.Min(meldCount, 4)] = 0;

        for (int g = 0; g < 4; g++)
        {
            var table = GroupTable(counts, g);
            next.Fill(-1);
            for (int h1 = 0; h1 < 2; h1++)
            for (int m1 = 0; m1 < 5; m1++)
            {
                int t1 = dp[h1 * 5 + m1];
                if (t1 < 0) continue;
                for (int h2 = 0; h1 + h2 < 2; h2++)
                for (int m2 = 0; m1 + m2 < 5; m2++)
                {
                    int t2 = table[h2 * 5 + m2];
                    if (t2 < 0) continue;
                    int idx = (h1 + h2) * 5 + m1 + m2;
                    if (t1 + t2 > next[idx]) next[idx] = t1 + t2;
                }
            }
            next.CopyTo(dp);
        }

        int best = 8;
        for (int h = 0; h < 2; h++)
        for (int m = 0; m < 5; m++)
        {
            int t = dp[h * 5 + m];
            if (t < 0) continue;
            int s = 8 - 2 * m - Math.Min(t, 4 - m) - h;
            if (s < best) best = s;
        }
        return best;
    }

    private static int[] GroupTable(int[] counts, int group)
    {
        bool honor = group == 3;
        int start = group * 9, length = honor ? 7 : 9;
        int key = 0;
        for (int i = 0; i < length; i++) key = key * 5 + counts[start + i];

        var cache = honor ? HonorCache : SuitCache;
        if (cache.TryGetValue(key, out var table)) return table;

        var c = new int[length];
        Array.Copy(counts, start, c, 0, length);
        table = new int[10];
        Array.Fill(table, -1);
        Search(c, 0, !honor, 0, 0, 0, table);
        cache[key] = table;
        return table;
    }

    // グループ内で、雀頭・面子・搭子の取り方をすべて試す。
    private static void Search(int[] c, int i, bool isSuit, int h, int m, int t, int[] table)
    {
        while (i < c.Length && c[i] == 0) i++;
        if (i == c.Length)
        {
            if (m <= 4 && t > table[h * 5 + m]) table[h * 5 + m] = t;
            return;
        }

        // 雀頭
        if (h == 0 && c[i] >= 2)
        {
            c[i] -= 2; Search(c, i, isSuit, 1, m, t, table); c[i] += 2;
        }
        // 面子
        if (c[i] >= 3)
        {
            c[i] -= 3; Search(c, i, isSuit, h, m + 1, t, table); c[i] += 3;
        }
        if (isSuit && i <= 6 && c[i + 1] > 0 && c[i + 2] > 0)
        {
            c[i]--; c[i + 1]--; c[i + 2]--;
            Search(c, i, isSuit, h, m + 1, t, table);
            c[i]++; c[i + 1]++; c[i + 2]++;
        }
        // 搭子(4 つを超えても意味がない)
        if (t < 4)
        {
            if (c[i] >= 2)
            {
                c[i] -= 2; Search(c, i, isSuit, h, m, t + 1, table); c[i] += 2;
            }
            if (isSuit && i <= 7 && c[i + 1] > 0)
            {
                c[i]--; c[i + 1]--; Search(c, i, isSuit, h, m, t + 1, table); c[i]++; c[i + 1]++;
            }
            if (isSuit && i <= 6 && c[i + 2] > 0)
            {
                c[i]--; c[i + 2]--; Search(c, i, isSuit, h, m, t + 1, table); c[i]++; c[i + 2]++;
            }
        }
        // 残りは孤立牌として次の牌種へ
        int saved = c[i];
        c[i] = 0;
        Search(c, i + 1, isSuit, h, m, t, table);
        c[i] = saved;
    }
}
