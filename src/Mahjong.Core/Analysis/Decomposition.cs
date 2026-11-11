namespace Mahjong.Core;

/// <summary>和了形の種類。</summary>
public enum HandForm { Standard, Chiitoitsu, Kokushi }

/// <summary>面子の種類。</summary>
public enum BlockType { Shuntsu, Koutsu, Kantsu }

/// <summary>待ちの形(符計算・平和判定で使う)。</summary>
public enum WaitType { Ryanmen, Kanchan, Penchan, Shanpon, Tanki }

/// <summary>
/// 面子 1 つ。<see cref="Kind"/> は順子なら最小の牌種、刻子・槓子ならその牌種。
/// </summary>
/// <param name="IsConcealed">暗刻・暗槓扱いか(ロンで完成した刻子や副露は false)。</param>
/// <param name="IsCalled">副露(チー・ポン・明槓)由来か。</param>
public readonly record struct Block(BlockType Type, int Kind, bool IsConcealed, bool IsCalled)
{
    public bool IsTriplet => Type is BlockType.Koutsu or BlockType.Kantsu;

    public bool Contains(int kind) => Type == BlockType.Shuntsu
        ? kind >= Kind && kind <= Kind + 2
        : kind == Kind;

    /// <summary>么九牌を含むか(混全帯么九・純全帯么九の判定用)。</summary>
    public bool HasTerminalOrHonor => Type == BlockType.Shuntsu
        ? TileKinds.IsTerminal(Kind) || TileKinds.IsTerminal(Kind + 2)
        : TileKinds.IsTerminalOrHonor(Kind);

    public override string ToString()
    {
        string tiles = Type switch
        {
            BlockType.Shuntsu => string.Join("", Enumerable.Range(Kind, 3).Select(TileKinds.ToDisplay)),
            BlockType.Koutsu => string.Concat(Enumerable.Repeat(TileKinds.ToDisplay(Kind), 3)),
            _ => string.Concat(Enumerable.Repeat(TileKinds.ToDisplay(Kind), 4)),
        };
        return IsCalled ? $"[{tiles}]" : tiles;
    }
}

/// <summary>
/// 和了形の分解結果 1 通り。同じ手牌でも複数の分解・待ちの解釈がありうる。
/// </summary>
/// <param name="Pair">雀頭の牌種(国士無双では重なっている牌種、七対子では -1)。</param>
/// <param name="Blocks">4 面子(副露・暗槓を含む)。七対子・国士無双では空。</param>
/// <param name="Pairs">七対子の 7 対子の牌種。それ以外の形では空。</param>
public sealed record Decomposition(
    HandForm Form,
    int Pair,
    IReadOnlyList<Block> Blocks,
    WaitType Wait,
    IReadOnlyList<int> Pairs)
{
    public override string ToString() => Form switch
    {
        HandForm.Chiitoitsu => "七対子 " + string.Join(" ", Pairs.Select(k => TileKinds.ToDisplay(k) + TileKinds.ToDisplay(k))),
        HandForm.Kokushi => "国士無双",
        _ => string.Join(" ", Blocks) + $" 雀頭{TileKinds.ToDisplay(Pair)} ({Wait})",
    };
}
