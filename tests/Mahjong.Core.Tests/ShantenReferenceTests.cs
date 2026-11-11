using FsCheck.Xunit;
using Mahjong.Core;

namespace Mahjong.Core.Tests;

/// <summary>
/// 高速化した向聴数計算を、単純な全探索(以前の実装)と比べて確かめる。
/// </summary>
public class ShantenReferenceTests
{
    [Property(MaxTest = 2000)]
    public bool 高速版と全探索版の結果が一致する(int seed, bool fourteen, byte meldSeed)
    {
        int melds = meldSeed % 4;
        int size = (fourteen ? 14 : 13) - melds * 3;
        var counts = new int[34];
        foreach (var t in new Wall(seed).AllTiles.Take(size)) counts[t.Kind]++;
        return Shanten.Normal(counts, melds) == ReferenceNormal(counts, melds);
    }

    private static int ReferenceNormal(int[] counts, int meldCount)
    {
        var c = (int[])counts.Clone();
        int best = 8;
        Search(c, 0, meldCount, 0, false, ref best);
        for (int k = 0; k < 34; k++)
        {
            if (c[k] < 2) continue;
            c[k] -= 2;
            Search(c, 0, meldCount, 0, true, ref best);
            c[k] += 2;
        }
        return best;
    }

    private static void Search(int[] c, int i, int m, int t, bool head, ref int best)
    {
        while (i < 34 && c[i] == 0) i++;
        if (i == 34)
        {
            int s = 8 - 2 * m - Math.Min(t, 4 - m) - (head ? 1 : 0);
            if (s < best) best = s;
            return;
        }
        bool num = i < 27;
        int pos = i % 9;
        if (c[i] >= 3) { c[i] -= 3; Search(c, i, m + 1, t, head, ref best); c[i] += 3; }
        if (num && pos <= 6 && c[i + 1] > 0 && c[i + 2] > 0)
        {
            c[i]--; c[i + 1]--; c[i + 2]--; Search(c, i, m + 1, t, head, ref best); c[i]++; c[i + 1]++; c[i + 2]++;
        }
        if (m + t < 4)
        {
            if (c[i] >= 2) { c[i] -= 2; Search(c, i, m, t + 1, head, ref best); c[i] += 2; }
            if (num && pos <= 7 && c[i + 1] > 0) { c[i]--; c[i + 1]--; Search(c, i, m, t + 1, head, ref best); c[i]++; c[i + 1]++; }
            if (num && pos <= 6 && c[i + 2] > 0) { c[i]--; c[i + 2]--; Search(c, i, m, t + 1, head, ref best); c[i]++; c[i + 2]++; }
        }
        int saved = c[i]; c[i] = 0; Search(c, i + 1, m, t, head, ref best); c[i] = saved;
    }
}
