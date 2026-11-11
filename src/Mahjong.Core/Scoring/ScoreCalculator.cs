using Mahjong.Core.Yakus;

namespace Mahjong.Core;

/// <summary>
/// 役・符・翻から点数を計算する。分解ごとに計算して、受け取る点が最大のものを採用する。
/// </summary>
public sealed class ScoreCalculator
{
    private readonly IReadOnlyList<IYaku> _yakus;

    public ScoreCalculator(IEnumerable<IYaku> yakus) => _yakus = yakus.ToList();

    /// <summary>標準の役一式で計算機を作る。</summary>
    public static ScoreCalculator CreateDefault() => new(StandardYaku.Create());

    public ScoreResult Calculate(WinContext ctx)
    {
        ScoreResult? best = null;
        foreach (var d in HandDecomposer.Enumerate(ctx))
        {
            var r = CalculateFor(ctx, d);
            if (r is null) continue;
            if (best is null || IsBetter(r, best)) best = r;
        }
        return best ?? ScoreResult.None;
    }

    private ScoreResult? CalculateFor(WinContext ctx, Decomposition d)
    {
        // 役満が 1 つでもあれば役満だけを数える(通常役・ドラは数えない)
        var yakuman = Evaluate(ctx, d, yakumanOnly: true);
        if (yakuman.Count > 0)
        {
            int count = yakuman.Sum(y => y.Han) / 13;
            return ScoreTable.From(yakuman.Sum(y => y.Han), 0, ctx.IsTsumo, ctx.IsDealer, ctx.Honba, count)
                with { Yaku = yakuman, Decomposition = d };
        }

        var list = Evaluate(ctx, d, yakumanOnly: false);
        if (list.Count == 0) return null;   // 役なし(ドラだけでは和了できない)

        var dora = DoraCounter.CountDetail(ctx);
        if (dora.Dora > 0) list.Add(new YakuHan("ドラ", dora.Dora));
        if (dora.Red > 0) list.Add(new YakuHan("赤ドラ", dora.Red));
        if (dora.Ura > 0) list.Add(new YakuHan("裏ドラ", dora.Ura));

        int han = list.Sum(y => y.Han);
        int fu = FuCalculator.Calculate(ctx, d);
        return ScoreTable.From(han, fu, ctx.IsTsumo, ctx.IsDealer, ctx.Honba)
            with { Yaku = list, Decomposition = d };
    }

    private List<YakuHan> Evaluate(WinContext ctx, Decomposition d, bool yakumanOnly)
    {
        var list = new List<YakuHan>();
        foreach (var y in _yakus)
        {
            if (y.IsYakuman != yakumanOnly) continue;
            int han = y.Evaluate(ctx, d);
            if (han > 0) list.Add(new YakuHan(y.Name, han));
        }
        return list;
    }

    // 支払いが多い方、同じなら翻が多い方、さらに同じなら符が高い方を採用する
    private static bool IsBetter(ScoreResult a, ScoreResult b) =>
        a.Payment != b.Payment ? a.Payment > b.Payment
        : a.Han != b.Han ? a.Han > b.Han
        : a.Fu > b.Fu;
}
