namespace Mahjong.Core;

/// <summary>対局の状態(局をまたいで持ち越す情報+現在の局の牌山)。</summary>
public sealed class GameState
{
    public const int PlayerCount = 4;

    public GameState(RuleSet rules)
    {
        Rules = rules;
        Players = Enumerable.Range(0, PlayerCount).Select(i => new Player(i, rules.StartingPoints)).ToArray();
    }

    public RuleSet Rules { get; }
    public Player[] Players { get; }

    /// <summary>場風(東場・南場)。</summary>
    public TileKind RoundWind { get; set; } = TileKind.East;

    /// <summary>局数(1〜4)。</summary>
    public int RoundNumber { get; set; } = 1;

    /// <summary>親の席。</summary>
    public int Dealer { get; set; }

    public int Honba { get; set; }

    /// <summary>場に出ているリーチ棒の本数(供託)。</summary>
    public int RiichiSticks { get; set; }

    public Wall Wall { get; set; } = new(0);

    /// <summary>現在手番の席。</summary>
    public int CurrentSeat { get; set; }

    /// <summary>その席の自風。</summary>
    public TileKind SeatWindOf(int seat) => (TileKind)(27 + (seat - Dealer + PlayerCount) % PlayerCount);

    /// <summary>「東1局 0本場」のような表示。</summary>
    public string RoundName => $"{TileKinds.ToDisplay((int)RoundWind)}{RoundNumber}局 {Honba}本場";
}
