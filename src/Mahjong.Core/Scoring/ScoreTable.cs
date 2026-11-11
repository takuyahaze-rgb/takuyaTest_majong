namespace Mahjong.Core;

/// <summary>
/// 翻・符から支払い額を求める。
/// 基本点 = 符 × 2^(翻+2)。2000 を超える手は満貫以上の固定点。支払いは 100 点単位に切り上げ。
/// </summary>
public static class ScoreTable
{
    /// <param name="yakumanCount">役満の倍数(0 なら通常役)。</param>
    public static ScoreResult From(int han, int fu, bool isTsumo, bool isDealer, int honba = 0, int yakumanCount = 0)
    {
        var (basePoints, limitName) = BasePoints(han, fu, yakumanCount);

        int ron = 0, fromDealer = 0, fromNonDealer = 0, total;
        if (!isTsumo)
        {
            ron = RoundUp100(basePoints * (isDealer ? 6 : 4)) + 300 * honba;
            total = ron;
        }
        else if (isDealer)
        {
            fromNonDealer = RoundUp100(basePoints * 2) + 100 * honba;
            total = fromNonDealer * 3;
        }
        else
        {
            fromDealer = RoundUp100(basePoints * 2) + 100 * honba;
            fromNonDealer = RoundUp100(basePoints) + 100 * honba;
            total = fromDealer + fromNonDealer * 2;
        }

        return new ScoreResult
        {
            Han = han,
            Fu = fu,
            IsYakuman = yakumanCount > 0,
            LimitName = limitName,
            BasePoints = basePoints,
            RonPayment = ron,
            TsumoPaymentFromDealer = fromDealer,
            TsumoPaymentFromNonDealer = fromNonDealer,
            Payment = total,
            IsTsumo = isTsumo,
        };
    }

    public static (int BasePoints, string? LimitName) BasePoints(int han, int fu, int yakumanCount = 0)
    {
        if (yakumanCount > 0)
            return (8000 * yakumanCount, yakumanCount switch { 1 => "役満", 2 => "ダブル役満", 3 => "トリプル役満", _ => $"{yakumanCount}倍役満" });
        if (han >= 13) return (8000, "数え役満");
        if (han >= 11) return (6000, "三倍満");
        if (han >= 8) return (4000, "倍満");
        if (han >= 6) return (3000, "跳満");
        if (han >= 5) return (2000, "満貫");

        int b = fu * (1 << (han + 2));
        return b >= 2000 ? (2000, "満貫") : (b, null);
    }

    private static int RoundUp100(int points) => (points + 99) / 100 * 100;
}
