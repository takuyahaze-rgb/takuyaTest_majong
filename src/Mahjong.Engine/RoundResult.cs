using Mahjong.Core;

namespace Mahjong.Engine;

public enum RoundEndType { Tsumo, Ron, ExhaustiveDraw, AbortiveDraw }

/// <summary>和了 1 件。<see cref="Loser"/> はロンされた席(ツモなら null)。</summary>
public sealed record WinInfo(int Winner, int? Loser, ScoreResult Score, IReadOnlyList<Tile> Hand, Tile WinTile);

/// <summary>1 局の結果。</summary>
public sealed record RoundResult
{
    public required RoundEndType Type { get; init; }

    public IReadOnlyList<WinInfo> Wins { get; init; } = [];

    /// <summary>各席の点数の増減(リーチ棒の供託分は含まず、供託の受け取りは含む)。</summary>
    public required IReadOnlyList<int> ScoreDeltas { get; init; }

    /// <summary>流局時に聴牌だった席。</summary>
    public IReadOnlyList<int> TenpaiSeats { get; init; } = [];

    /// <summary>流し満貫を達成した席。</summary>
    public IReadOnlyList<int> NagashiManganSeats { get; init; } = [];

    /// <summary>途中流局の理由(九種九牌・四風連打・四家立直・四開槓・三家和)。</summary>
    public string? AbortiveReason { get; init; }

    /// <summary>親が連荘するか。</summary>
    public required bool DealerContinues { get; init; }

    /// <summary>局終了時の全員の手牌(結果表示用)。</summary>
    public IReadOnlyList<IReadOnlyList<Tile>> FinalHands { get; init; } = [];

    public override string ToString() => Type switch
    {
        RoundEndType.Tsumo or RoundEndType.Ron =>
            string.Join(" / ", Wins.Select(w => $"席{w.Winner} {(w.Loser is int l ? $"ロン(席{l}から)" : "ツモ")} {w.Score}")),
        RoundEndType.ExhaustiveDraw => NagashiManganSeats.Count > 0
            ? $"流し満貫 席{string.Join(",", NagashiManganSeats)}"
            : $"荒牌平局 聴牌: {(TenpaiSeats.Count == 0 ? "なし" : string.Join(",", TenpaiSeats.Select(s => $"席{s}")))}",
        _ => $"途中流局({AbortiveReason})",
    };
}

/// <summary>半荘(または東風戦)の最終結果。</summary>
/// <param name="FinalScores">席順の最終持ち点(供託の残りは 1 位に加算済み)。</param>
/// <param name="Ranking">順位順の席(同点は起家に近い方が上)。</param>
public sealed record GameResult(IReadOnlyList<int> FinalScores, IReadOnlyList<int> Ranking, int RoundsPlayed);
