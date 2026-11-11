namespace Mahjong.Core;

/// <summary>牌の色(スート)。</summary>
public enum Suit { Man, Pin, Sou, Wind, Dragon }

/// <summary>牌の種類 34 種。値は 0〜33 で、<see cref="Tile.Kind"/> と一致する。</summary>
public enum TileKind
{
    M1, M2, M3, M4, M5, M6, M7, M8, M9,
    P1, P2, P3, P4, P5, P6, P7, P8, P9,
    S1, S2, S3, S4, S5, S6, S7, S8, S9,
    East, South, West, North,
    White, Green, Red,
}

/// <summary>
/// 牌 1 枚(不変)。136 枚を 0〜135 の ID で区別し、種類は ID / 4 で求める。
/// </summary>
public readonly record struct Tile(int Id, bool IsRed = false)
{
    public int Kind => Id / 4;                       // 0..33
    public TileKind KindEnum => (TileKind)Kind;
    public Suit Suit => TileKinds.SuitOf(Kind);
    public int Number => TileKinds.NumberOf(Kind);
    public bool IsHonor => TileKinds.IsHonor(Kind);
    public bool IsTerminal => TileKinds.IsTerminal(Kind);

    public override string ToString() => IsRed ? $"赤{TileKinds.ToDisplay(Kind)}" : TileKinds.ToDisplay(Kind);
}

/// <summary>牌の種類(0〜33 の整数)に関する補助関数。</summary>
public static class TileKinds
{
    public const int Count = 34;

    private static readonly string[] HonorNames = ["東", "南", "西", "北", "白", "發", "中"];

    public static Suit SuitOf(int kind) => kind switch
    {
        < 9 => Suit.Man,
        < 18 => Suit.Pin,
        < 27 => Suit.Sou,
        < 31 => Suit.Wind,
        _ => Suit.Dragon,
    };

    /// <summary>数牌なら 1〜9、字牌なら 0。</summary>
    public static int NumberOf(int kind) => kind < 27 ? kind % 9 + 1 : 0;

    public static bool IsHonor(int kind) => kind >= 27;
    public static bool IsWind(int kind) => kind is >= 27 and <= 30;
    public static bool IsDragon(int kind) => kind >= 31;

    /// <summary>老頭牌(数牌の 1・9)。</summary>
    public static bool IsTerminal(int kind) => kind < 27 && (kind % 9 is 0 or 8);

    /// <summary>么九牌(老頭牌または字牌)。</summary>
    public static bool IsTerminalOrHonor(int kind) => IsHonor(kind) || IsTerminal(kind);

    /// <summary>中張牌(数牌の 2〜8)。</summary>
    public static bool IsSimple(int kind) => !IsTerminalOrHonor(kind);

    /// <summary>表示用の文字列。数牌は「1m」「5p」、字牌は「東」「中」など。</summary>
    public static string ToDisplay(int kind) => kind < 27
        ? $"{NumberOf(kind)}{"mps"[kind / 9]}"
        : HonorNames[kind - 27];

    /// <summary>ドラ表示牌の種類から、実際のドラの種類を求める。</summary>
    public static int DoraFromIndicator(int indicatorKind)
    {
        if (indicatorKind < 27)
        {
            int suitBase = indicatorKind / 9 * 9;
            return suitBase + (indicatorKind - suitBase + 1) % 9;
        }
        if (indicatorKind <= 30) return 27 + (indicatorKind - 27 + 1) % 4;   // 東→南→西→北→東
        return 31 + (indicatorKind - 31 + 1) % 3;                            // 白→發→中→白
    }
}
