namespace Mahjong.Core;

/// <summary>他家も含め、誰からでも見える 1 人分の情報。</summary>
public sealed record PublicPlayerInfo(
    int Seat,
    int Score,
    TileKind SeatWind,
    IReadOnlyList<Meld> Melds,
    IReadOnlyList<RiverTile> River,
    bool IsRiichi,
    int ClosedTileCount);

/// <summary>
/// そのプレイヤーから見える情報だけをまとめたもの。他家の手牌は含まない。
/// エンジンが問い合わせのたびに作り直すスナップショットで、書き換えても対局には影響しない。
/// </summary>
public sealed record PlayerView(
    int Seat,
    IReadOnlyList<Tile> Closed,
    IReadOnlyList<Meld> Melds,
    Tile? DrawnTile,
    bool IsRiichi,
    IReadOnlyList<PublicPlayerInfo> Players,
    IReadOnlyList<Tile> DoraIndicators,
    TileKind RoundWind,
    TileKind SeatWind,
    int RoundNumber,
    int Dealer,
    int Honba,
    int RiichiSticks,
    int WallRemaining,
    RuleSet Rules)
{
    /// <summary>自分の手牌(閉じた牌)の枚数配列。</summary>
    public int[] ClosedCounts()
    {
        var c = new int[TileKinds.Count];
        foreach (var t in Closed) c[t.Kind]++;
        return c;
    }

    public static PlayerView Create(GameState s, int seat)
    {
        var me = s.Players[seat];
        var players = s.Players.Select(p => new PublicPlayerInfo(
            p.Seat, p.Score, s.SeatWindOf(p.Seat), p.Hand.Melds.ToList(), p.River.Discards.ToList(),
            p.IsRiichi, p.Hand.Closed.Count)).ToList();

        return new PlayerView(
            seat, me.Hand.Closed.ToList(), me.Hand.Melds.ToList(), me.DrawnTile, me.IsRiichi, players,
            s.Wall.DoraIndicators, s.RoundWind, s.SeatWindOf(seat), s.RoundNumber, s.Dealer, s.Honba,
            s.RiichiSticks, s.Wall.Remaining, s.Rules);
    }
}
