namespace Mahjong.Core;

/// <summary>符計算。</summary>
public static class FuCalculator
{
    /// <summary>平和の形か(門前・4 順子・役牌以外の雀頭・両面待ち)。</summary>
    public static bool IsPinfuShape(WinContext ctx, Decomposition d) =>
        d.Form == HandForm.Standard
        && ctx.IsMenzen
        && d.Blocks.All(b => b.Type == BlockType.Shuntsu)
        && !ctx.IsYakuhaiKind(d.Pair)
        && d.Wait == WaitType.Ryanmen;

    public static int Calculate(WinContext ctx, Decomposition d)
    {
        if (d.Form == HandForm.Chiitoitsu) return 25;
        if (d.Form == HandForm.Kokushi) return 30;   // 役満なので点数には影響しない

        // 平和: ツモ 20 符、ロン 30 符
        if (IsPinfuShape(ctx, d)) return ctx.IsTsumo ? 20 : 30;

        int fu = 20;                                   // 副底
        if (ctx.IsMenzen && !ctx.IsTsumo) fu += 10;    // 門前加符
        if (ctx.IsTsumo) fu += 2;                      // ツモ符

        foreach (var b in d.Blocks)
        {
            if (!b.IsTriplet) continue;
            int f = TileKinds.IsTerminalOrHonor(b.Kind) ? 4 : 2;   // 明刻: 中張 2 / 么九 4
            if (b.IsConcealed) f *= 2;                              // 暗刻は 2 倍
            if (b.Type == BlockType.Kantsu) f *= 4;                 // 槓子は 4 倍
            fu += f;
        }

        // 雀頭: 三元牌・自風・場風(連風牌は 4 符)
        if (TileKinds.IsDragon(d.Pair)) fu += 2;
        if (d.Pair == (int)ctx.SeatWind) fu += 2;
        if (d.Pair == (int)ctx.RoundWind) fu += 2;

        // 待ち: 嵌張・辺張・単騎は 2 符
        if (d.Wait is WaitType.Kanchan or WaitType.Penchan or WaitType.Tanki) fu += 2;

        fu = (fu + 9) / 10 * 10;      // 10 符単位に切り上げ
        if (fu == 20) fu = 30;        // 副露した平和形のロンなど(喰い平和は 30 符)
        return fu;
    }
}
