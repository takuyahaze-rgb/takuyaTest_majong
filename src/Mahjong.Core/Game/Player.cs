namespace Mahjong.Core;

/// <summary>河に置かれた牌 1 枚。</summary>
/// <param name="IsCalled">他家に鳴かれたか(鳴かれた牌も河の履歴としては残す)。</param>
public sealed record RiverTile(Tile Tile, bool IsTsumogiri, bool IsRiichiTile, bool IsCalled = false);

/// <summary>河(捨て牌の履歴)。</summary>
public sealed class River
{
    private readonly List<RiverTile> _discards = new();

    public IReadOnlyList<RiverTile> Discards => _discards;

    public void Add(Tile tile, bool isTsumogiri, bool isRiichiTile) =>
        _discards.Add(new RiverTile(tile, isTsumogiri, isRiichiTile));

    /// <summary>最後に捨てた牌を「鳴かれた」にする。</summary>
    public void MarkLastCalled() => _discards[^1] = _discards[^1] with { IsCalled = true };

    /// <summary>その牌種を捨てたことがあるか(フリテン判定用)。</summary>
    public bool ContainsKind(int kind) => _discards.Any(d => d.Tile.Kind == kind);
}

/// <summary>プレイヤー(席・持ち点・手牌・河・リーチ状態)。</summary>
public sealed class Player
{
    public Player(int seat, int score)
    {
        Seat = seat;
        Score = score;
    }

    public int Seat { get; }
    public int Score { get; set; }
    public Hand Hand { get; private set; } = new();
    public River River { get; private set; } = new();

    /// <summary>直前にツモった牌(鳴いた直後は null)。</summary>
    public Tile? DrawnTile { get; set; }

    public bool IsRiichi { get; set; }
    public bool IsDoubleRiichi { get; set; }
    public bool IsIppatsu { get; set; }

    /// <summary>同巡内フリテン(和了牌を見逃した。自分が打牌すると解除)。</summary>
    public bool IsTemporaryFuriten { get; set; }

    /// <summary>リーチ後の見逃しによるフリテン(局の終わりまで続く)。</summary>
    public bool IsRiichiFuriten { get; set; }

    /// <summary>新しい局のために手牌・河・状態を初期化する(持ち点は残す)。</summary>
    public void ResetForRound()
    {
        Hand = new Hand();
        River = new River();
        DrawnTile = null;
        IsRiichi = IsDoubleRiichi = IsIppatsu = false;
        IsTemporaryFuriten = IsRiichiFuriten = false;
    }

    /// <summary>フリテンか(自分の河に待ち牌がある、または見逃し中)。</summary>
    public bool IsFuriten(IReadOnlyList<int> waits) =>
        IsTemporaryFuriten || IsRiichiFuriten || waits.Any(River.ContainsKind);
}
