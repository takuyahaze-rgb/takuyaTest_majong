namespace Mahjong.Core.Yakus;

// 役満(天和・地和は SituationYaku.cs)。いずれも 13 翻を返す。

public sealed class KokushiMusou : IYaku
{
    public string Name => "国士無双";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) => d.Form == HandForm.Kokushi ? 13 : 0;
}

public sealed class Suuankou : IYaku
{
    public string Name => "四暗刻";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.IsStandard() && d.Triplets().Count(b => b.IsConcealed) == 4 ? 13 : 0;
}

public sealed class Daisangen : IYaku
{
    public string Name => "大三元";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.IsStandard() && d.Triplets().Count(b => TileKinds.IsDragon(b.Kind)) == 3 ? 13 : 0;
}

public sealed class Shousuushii : IYaku
{
    public string Name => "小四喜";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.IsStandard() && TileKinds.IsWind(d.Pair) && d.Triplets().Count(b => TileKinds.IsWind(b.Kind)) == 3 ? 13 : 0;
}

public sealed class Daisuushii : IYaku
{
    public string Name => "大四喜";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.IsStandard() && d.Triplets().Count(b => TileKinds.IsWind(b.Kind)) == 4 ? 13 : 0;
}

public sealed class Tsuuiisou : IYaku
{
    public string Name => "字一色";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.AllKinds(TileKinds.IsHonor) ? 13 : 0;
}

public sealed class Chinroutou : IYaku
{
    public string Name => "清老頭";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.AllKinds(TileKinds.IsTerminal) ? 13 : 0;
}

public sealed class Ryuuiisou : IYaku
{
    // 2s 3s 4s 6s 8s 發
    private static readonly HashSet<int> GreenKinds = [19, 20, 21, 23, 25, 32];

    public string Name => "緑一色";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.AllKinds(GreenKinds.Contains) ? 13 : 0;
}

public sealed class Chuuren : IYaku
{
    public string Name => "九蓮宝燈";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d)
    {
        if (ctx.Hand.Melds.Count > 0 || d.Form != HandForm.Standard) return 0;
        var c = ctx.AllCounts;
        for (int s = 0; s < 27; s += 9)
        {
            // 他の色や字牌が混ざっていないこと
            if (Enumerable.Range(0, TileKinds.Count).Any(k => c[k] > 0 && (k < s || k >= s + 9))) continue;
            if (c[s] < 3 || c[s + 8] < 3) continue;
            if (Enumerable.Range(s + 1, 7).All(k => c[k] >= 1)) return 13;
        }
        return 0;
    }
}

public sealed class Suukantsu : IYaku
{
    public string Name => "四槓子";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.IsStandard() && d.Blocks.Count(b => b.Type == BlockType.Kantsu) == 4 ? 13 : 0;
}
