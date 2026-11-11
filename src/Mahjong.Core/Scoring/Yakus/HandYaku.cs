namespace Mahjong.Core.Yakus;

// 手の形で決まる通常役

public sealed class Pinfu : IYaku
{
    public string Name => "平和";
    public int Evaluate(WinContext ctx, Decomposition d) => FuCalculator.IsPinfuShape(ctx, d) ? 1 : 0;
}

public sealed class Tanyao : IYaku
{
    public string Name => "断么九";
    public int Evaluate(WinContext ctx, Decomposition d)
    {
        if (!ctx.IsMenzen && !ctx.Rules.Kuitan) return 0;
        return ctx.AllKinds(TileKinds.IsSimple) ? 1 : 0;
    }
}

public sealed class Iipeikou : IYaku
{
    public string Name => "一盃口";
    public int Evaluate(WinContext ctx, Decomposition d) =>
        ctx.IsMenzen && d.IsStandard() && d.IdenticalSequencePairs() == 1 ? 1 : 0;
}

public sealed class Ryanpeikou : IYaku
{
    public string Name => "二盃口";
    public int Evaluate(WinContext ctx, Decomposition d) =>
        ctx.IsMenzen && d.IsStandard() && d.IdenticalSequencePairs() == 2 ? 3 : 0;
}

/// <summary>役牌(三元牌の刻子)。</summary>
public sealed class DragonYakuhai(TileKind dragon) : IYaku
{
    public string Name => $"役牌 {TileKinds.ToDisplay((int)dragon)}";
    public int Evaluate(WinContext ctx, Decomposition d) => d.IsStandard() && d.HasTripletOf((int)dragon) ? 1 : 0;
}

public sealed class SeatWindYakuhai : IYaku
{
    public string Name => "自風牌";
    public int Evaluate(WinContext ctx, Decomposition d) => d.IsStandard() && d.HasTripletOf((int)ctx.SeatWind) ? 1 : 0;
}

public sealed class RoundWindYakuhai : IYaku
{
    public string Name => "場風牌";
    public int Evaluate(WinContext ctx, Decomposition d) => d.IsStandard() && d.HasTripletOf((int)ctx.RoundWind) ? 1 : 0;
}

public sealed class Chiitoitsu : IYaku
{
    public string Name => "七対子";
    public int Evaluate(WinContext ctx, Decomposition d) => d.Form == HandForm.Chiitoitsu ? 2 : 0;
}

public sealed class SanshokuDoujun : IYaku
{
    public string Name => "三色同順";
    public int Evaluate(WinContext ctx, Decomposition d)
    {
        if (!d.IsStandard()) return 0;
        for (int n = 0; n <= 6; n++)
            if (d.HasSequenceAt(n) && d.HasSequenceAt(9 + n) && d.HasSequenceAt(18 + n))
                return ctx.IsMenzen ? 2 : 1;
        return 0;
    }
}

public sealed class Ittsu : IYaku
{
    public string Name => "一気通貫";
    public int Evaluate(WinContext ctx, Decomposition d)
    {
        if (!d.IsStandard()) return 0;
        for (int s = 0; s < 27; s += 9)
            if (d.HasSequenceAt(s) && d.HasSequenceAt(s + 3) && d.HasSequenceAt(s + 6))
                return ctx.IsMenzen ? 2 : 1;
        return 0;
    }
}

public sealed class Chanta : IYaku
{
    public string Name => "混全帯么九";
    public int Evaluate(WinContext ctx, Decomposition d)
    {
        // 順子を含み、全面子と雀頭に么九牌があり、字牌を含む(字牌なしは純全帯么九)
        if (!d.IsStandard() || !d.Sequences().Any()) return 0;
        if (!d.Blocks.All(b => b.HasTerminalOrHonor) || !TileKinds.IsTerminalOrHonor(d.Pair)) return 0;
        if (!ctx.HasHonor()) return 0;
        return ctx.IsMenzen ? 2 : 1;
    }
}

public sealed class Junchan : IYaku
{
    public string Name => "純全帯么九";
    public int Evaluate(WinContext ctx, Decomposition d)
    {
        if (!d.IsStandard() || !d.Sequences().Any()) return 0;
        if (ctx.HasHonor()) return 0;
        if (!d.Blocks.All(b => b.HasTerminalOrHonor) || !TileKinds.IsTerminal(d.Pair)) return 0;
        return ctx.IsMenzen ? 3 : 2;
    }
}

public sealed class Toitoi : IYaku
{
    public string Name => "対々和";
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.IsStandard() && d.Blocks.All(b => b.IsTriplet) ? 2 : 0;
}

public sealed class Sanankou : IYaku
{
    public string Name => "三暗刻";
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.IsStandard() && d.Triplets().Count(b => b.IsConcealed) == 3 ? 2 : 0;
}

public sealed class SanshokuDoukou : IYaku
{
    public string Name => "三色同刻";
    public int Evaluate(WinContext ctx, Decomposition d)
    {
        if (!d.IsStandard()) return 0;
        for (int n = 0; n < 9; n++)
            if (d.HasTripletOf(n) && d.HasTripletOf(9 + n) && d.HasTripletOf(18 + n)) return 2;
        return 0;
    }
}

public sealed class Sankantsu : IYaku
{
    public string Name => "三槓子";
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.IsStandard() && d.Blocks.Count(b => b.Type == BlockType.Kantsu) == 3 ? 2 : 0;
}

public sealed class Shousangen : IYaku
{
    public string Name => "小三元";
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.IsStandard() && TileKinds.IsDragon(d.Pair) && d.Triplets().Count(b => TileKinds.IsDragon(b.Kind)) == 2 ? 2 : 0;
}

public sealed class Honroutou : IYaku
{
    public string Name => "混老頭";
    public int Evaluate(WinContext ctx, Decomposition d) =>
        d.Form != HandForm.Kokushi && ctx.AllKinds(TileKinds.IsTerminalOrHonor) ? 2 : 0;
}

public sealed class Honitsu : IYaku
{
    public string Name => "混一色";
    public int Evaluate(WinContext ctx, Decomposition d) =>
        ctx.NumberSuitCount() == 1 && ctx.HasHonor() ? (ctx.IsMenzen ? 3 : 2) : 0;
}

public sealed class Chinitsu : IYaku
{
    public string Name => "清一色";
    public int Evaluate(WinContext ctx, Decomposition d) =>
        ctx.NumberSuitCount() == 1 && !ctx.HasHonor() ? (ctx.IsMenzen ? 6 : 5) : 0;
}
