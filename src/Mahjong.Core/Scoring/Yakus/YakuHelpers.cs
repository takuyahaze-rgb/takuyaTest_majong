namespace Mahjong.Core.Yakus;

/// <summary>役判定で共通に使う補助関数。</summary>
internal static class YakuHelpers
{
    public static bool IsStandard(this Decomposition d) => d.Form == HandForm.Standard;

    public static IEnumerable<Block> Triplets(this Decomposition d) => d.Blocks.Where(b => b.IsTriplet);

    public static IEnumerable<Block> Sequences(this Decomposition d) => d.Blocks.Where(b => b.Type == BlockType.Shuntsu);

    public static bool HasTripletOf(this Decomposition d, int kind) => d.Triplets().Any(b => b.Kind == kind);

    public static bool HasSequenceAt(this Decomposition d, int kind) => d.Sequences().Any(b => b.Kind == kind);

    /// <summary>手牌に含まれる牌種がすべて条件を満たすか。</summary>
    public static bool AllKinds(this WinContext ctx, Func<int, bool> predicate)
    {
        var c = ctx.AllCounts;
        for (int k = 0; k < TileKinds.Count; k++)
            if (c[k] > 0 && !predicate(k)) return false;
        return true;
    }

    /// <summary>使われている数牌の色の数(0〜3)。</summary>
    public static int NumberSuitCount(this WinContext ctx)
    {
        var c = ctx.AllCounts;
        int n = 0;
        for (int s = 0; s < 3; s++)
            if (Enumerable.Range(s * 9, 9).Any(k => c[k] > 0)) n++;
        return n;
    }

    public static bool HasHonor(this WinContext ctx)
    {
        var c = ctx.AllCounts;
        return Enumerable.Range(27, 7).Any(k => c[k] > 0);
    }

    /// <summary>同じ順子のペア数(一盃口 1、二盃口 2)。</summary>
    public static int IdenticalSequencePairs(this Decomposition d) =>
        d.Sequences().GroupBy(b => b.Kind).Sum(g => g.Count() / 2);
}
