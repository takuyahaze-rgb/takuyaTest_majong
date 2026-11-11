namespace Mahjong.Core;

/// <summary>ドラ・赤ドラ・裏ドラの枚数を数える。</summary>
public static class DoraCounter
{
    public sealed record Result(int Dora, int Red, int Ura)
    {
        public int Total => Dora + Red + Ura;
    }

    public static Result CountDetail(WinContext ctx)
    {
        var tiles = ctx.AllTiles.ToList();
        int dora = ctx.Doras.Sum(k => tiles.Count(t => t.Kind == (int)k));
        int red = tiles.Count(t => t.IsRed);
        int ura = ctx.IsRiichi ? ctx.UraDoras.Sum(k => tiles.Count(t => t.Kind == (int)k)) : 0;
        return new Result(dora, red, ura);
    }

    public static int Count(WinContext ctx) => CountDetail(ctx).Total;
}
