namespace Mahjong.Core;

/// <summary>成立した役 1 つ(ドラも含む)と、その翻数。役満は 13 翻(ダブル役満は 26 翻)として表す。</summary>
public sealed record YakuHan(string Name, int Han);

/// <summary>点数計算の結果。</summary>
public sealed record ScoreResult
{
    public static ScoreResult None { get; } = new();

    public bool IsValid => Han > 0;

    public int Han { get; init; }
    public int Fu { get; init; }
    public bool IsYakuman { get; init; }

    /// <summary>満貫・跳満などの名前(該当しなければ null)。</summary>
    public string? LimitName { get; init; }

    /// <summary>基本点。</summary>
    public int BasePoints { get; init; }

    public IReadOnlyList<YakuHan> Yaku { get; init; } = [];

    /// <summary>ロンのとき放銃者が払う点(本場を含む)。</summary>
    public int RonPayment { get; init; }

    /// <summary>ツモのとき親が払う点(親の和了では 0)。本場を含む。</summary>
    public int TsumoPaymentFromDealer { get; init; }

    /// <summary>ツモのとき子 1 人が払う点。本場を含む。</summary>
    public int TsumoPaymentFromNonDealer { get; init; }

    /// <summary>和了者が受け取る合計点(供託リーチ棒は含まない)。</summary>
    public int Payment { get; init; }

    public bool IsTsumo { get; init; }

    public Decomposition? Decomposition { get; init; }

    public override string ToString()
    {
        if (!IsValid) return "役なし";
        string yaku = string.Join(" ", Yaku.Select(y => IsYakuman ? y.Name : $"{y.Name}{y.Han}"));
        string head = IsYakuman ? LimitName! : $"{Fu}符{Han}翻" + (LimitName is null ? "" : $" {LimitName}");
        string pay = !IsTsumo ? $"{RonPayment}点"
            : TsumoPaymentFromDealer == 0 ? $"{TsumoPaymentFromNonDealer}点オール"
            : $"{TsumoPaymentFromNonDealer}-{TsumoPaymentFromDealer}点";
        return $"{yaku} / {head} / {pay}(合計{Payment}点)";
    }
}
