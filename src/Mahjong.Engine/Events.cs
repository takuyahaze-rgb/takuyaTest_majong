using Mahjong.Core;

namespace Mahjong.Engine;

/// <summary>エンジンが発行する状態変化。UI はこれを購読して描画する。</summary>
public abstract record GameEvent;

public record GameStarted(IReadOnlyList<int> Scores) : GameEvent;

public record RoundStarted(TileKind RoundWind, int RoundNumber, int Dealer, int Honba, int RiichiSticks,
    Tile DoraIndicator, IReadOnlyList<int> Scores) : GameEvent;

/// <summary>配牌。他家の配牌は Tiles が null。</summary>
public record HandDealt(int Seat, IReadOnlyList<Tile>? Tiles) : GameEvent;

/// <summary>ツモ。他家のツモ牌は Tile が null。</summary>
public record TileDrawn(int Seat, Tile? Tile, bool IsRinshan = false) : GameEvent;

public record TileDiscarded(int Seat, Tile Tile, bool Riichi, bool IsTsumogiri = false) : GameEvent;

/// <summary>リーチが成立し、リーチ棒が出された(宣言牌がロンされなかった)。</summary>
public record RiichiAccepted(int Seat) : GameEvent;

public record MeldDeclared(int Seat, Meld Meld) : GameEvent;

public record DoraRevealed(Tile Indicator) : GameEvent;

public record RoundEnded(RoundResult Result) : GameEvent;

public record GameEnded(GameResult Result) : GameEvent;

public interface IGameObserver
{
    void OnEvent(GameEvent e);
}

/// <summary>
/// 観測者にイベントを配る。席を指定した観測者には、他家のツモ牌・配牌を伏せて渡す。
/// </summary>
public sealed class EventHub
{
    private readonly List<(IGameObserver Observer, int? Seat)> _observers = new();

    /// <param name="seat">その席の視点で見る(null なら全員の牌が見える)。</param>
    public void Add(IGameObserver observer, int? seat = null) => _observers.Add((observer, seat));

    public void Publish(GameEvent e)
    {
        foreach (var (observer, seat) in _observers) observer.OnEvent(Mask(e, seat));
    }

    private static GameEvent Mask(GameEvent e, int? seat) => (seat, e) switch
    {
        (int s, TileDrawn d) when d.Seat != s => d with { Tile = null },
        (int s, HandDealt h) when h.Seat != s => h with { Tiles = null },
        _ => e,
    };
}
