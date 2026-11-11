namespace Mahjong.Core.Yakus;

// 和了の状況で決まる役(手の形に関係しないもの)

public sealed class Riichi : IYaku
{
    public string Name => "立直";
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsRiichi && !ctx.IsDoubleRiichi ? 1 : 0;
}

public sealed class DoubleRiichi : IYaku
{
    public string Name => "両立直";
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsDoubleRiichi ? 2 : 0;
}

public sealed class Ippatsu : IYaku
{
    public string Name => "一発";
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsRiichi && ctx.IsIppatsu ? 1 : 0;
}

public sealed class MenzenTsumo : IYaku
{
    public string Name => "門前清自摸和";
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsMenzen && ctx.IsTsumo ? 1 : 0;
}

public sealed class Haitei : IYaku
{
    public string Name => "海底摸月";
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsLastTile && ctx.IsTsumo && !ctx.IsRinshan ? 1 : 0;
}

public sealed class Houtei : IYaku
{
    public string Name => "河底撈魚";
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsLastTile && !ctx.IsTsumo ? 1 : 0;
}

public sealed class Rinshan : IYaku
{
    public string Name => "嶺上開花";
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsRinshan && ctx.IsTsumo ? 1 : 0;
}

public sealed class Chankan : IYaku
{
    public string Name => "槍槓";
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsChankan && !ctx.IsTsumo ? 1 : 0;
}

public sealed class Tenhou : IYaku
{
    public string Name => "天和";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsFirstTurn && ctx.IsTsumo && ctx.IsDealer ? 13 : 0;
}

public sealed class Chiihou : IYaku
{
    public string Name => "地和";
    public bool IsYakuman => true;
    public int Evaluate(WinContext ctx, Decomposition d) => ctx.IsFirstTurn && ctx.IsTsumo && !ctx.IsDealer ? 13 : 0;
}
