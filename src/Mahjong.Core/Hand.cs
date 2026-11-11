namespace Mahjong.Core;

/// <summary>副露・槓の種類。</summary>
public enum MeldType { Chi, Pon, Daiminkan, Ankan, Kakan }

/// <summary>
/// 副露・槓 1 組。<paramref name="FromSeat"/> は牌を出した席(暗槓は自分の席)。
/// </summary>
public sealed record Meld(MeldType Type, IReadOnlyList<Tile> Tiles, int FromSeat)
{
    /// <summary>面子の代表となる牌種(順子は最小の牌、刻子・槓子はその牌)。</summary>
    public int Kind => Tiles.Min(t => t.Kind);

    public bool IsKan => Type is MeldType.Daiminkan or MeldType.Ankan or MeldType.Kakan;

    /// <summary>他家の牌を使った副露か(暗槓だけが false)。</summary>
    public bool IsOpen => Type != MeldType.Ankan;
}

/// <summary>手牌(閉じた牌+副露)。</summary>
public sealed class Hand
{
    public List<Tile> Closed { get; } = new();
    public List<Meld> Melds { get; } = new();

    /// <summary>門前か(暗槓のみなら門前扱い)。</summary>
    public bool IsMenzen => Melds.All(m => m.Type == MeldType.Ankan);

    public Hand() { }

    public Hand(IEnumerable<Tile> closed, IEnumerable<Meld>? melds = null)
    {
        Closed.AddRange(closed);
        if (melds is not null) Melds.AddRange(melds);
    }

    /// <summary>閉じた牌の 34 要素の枚数配列。</summary>
    public int[] ToCounts()
    {
        var c = new int[TileKinds.Count];
        foreach (var t in Closed) c[t.Kind]++;
        return c;
    }

    public override string ToString()
    {
        var closed = TileParser.ToNotation(Closed);
        if (Melds.Count == 0) return closed;
        return closed + " " + string.Join(" ", Melds.Select(m => $"[{TileParser.ToNotation(m.Tiles)}]"));
    }
}
