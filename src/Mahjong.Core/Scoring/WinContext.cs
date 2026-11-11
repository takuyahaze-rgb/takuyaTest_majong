namespace Mahjong.Core;

/// <summary>
/// 和了の状況。<see cref="Hand"/> の閉じた牌には和了牌を「含めない」(閉じた牌+和了牌で和了形になる)。
/// </summary>
/// <param name="Doras">表ドラの種類(表示牌ではなく、ドラそのもの)。</param>
public sealed record WinContext(
    Hand Hand, Tile WinTile, bool IsTsumo, bool IsRiichi, bool IsIppatsu,
    TileKind RoundWind, TileKind SeatWind, IReadOnlyList<TileKind> Doras)
{
    /// <summary>裏ドラの種類(リーチ時のみ数える)。</summary>
    public IReadOnlyList<TileKind> UraDoras { get; init; } = [];

    /// <summary>ダブルリーチ(<see cref="IsRiichi"/> も true にすること)。</summary>
    public bool IsDoubleRiichi { get; init; }

    /// <summary>嶺上開花(槓のあとの嶺上牌でツモ和了)。</summary>
    public bool IsRinshan { get; init; }

    /// <summary>槍槓(加槓された牌でロン)。</summary>
    public bool IsChankan { get; init; }

    /// <summary>最後の 1 枚での和了(ツモなら海底摸月、ロンなら河底撈魚)。</summary>
    public bool IsLastTile { get; init; }

    /// <summary>鳴きの入っていない第 1 巡のツモ和了(親なら天和、子なら地和)。</summary>
    public bool IsFirstTurn { get; init; }

    public int Honba { get; init; }

    public RuleSet Rules { get; init; } = RuleSet.Default;

    public bool IsDealer => SeatWind == TileKind.East;
    public bool IsMenzen => Hand.IsMenzen;

    /// <summary>手牌の全牌(閉じた牌+和了牌+副露牌)。</summary>
    public IEnumerable<Tile> AllTiles =>
        Hand.Closed.Append(WinTile).Concat(Hand.Melds.SelectMany(m => m.Tiles));

    /// <summary>全牌の 34 要素の枚数配列(槓子は 4 枚として数える)。</summary>
    public int[] AllCounts
    {
        get
        {
            var c = new int[TileKinds.Count];
            foreach (var t in AllTiles) c[t.Kind]++;
            return c;
        }
    }

    /// <summary>雀頭にすると符が付く牌(三元牌・自風・場風)か。</summary>
    public bool IsYakuhaiKind(int kind) =>
        TileKinds.IsDragon(kind) || kind == (int)SeatWind || kind == (int)RoundWind;
}
