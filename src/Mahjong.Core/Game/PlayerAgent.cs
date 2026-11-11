namespace Mahjong.Core;

/// <summary>
/// プレイヤー(人間・AI)。エンジンは中身を知らず、「この状況で何をするか」だけを問い合わせる。
/// 人間の入力待ちを表せるよう、どちらのメソッドも非同期にしている。
/// </summary>
public interface IPlayerAgent
{
    /// <summary>自分の手番(ツモ後、または鳴いた後)の行動を選ぶ。打牌・リーチ・ツモ和了・暗槓・加槓・九種九牌。</summary>
    Task<TurnAction> ChooseTurnActionAsync(PlayerView view, TurnOptions options);

    /// <summary>他家の打牌(または加槓)に対して、ロン・ポン・大明槓・チー・パスを選ぶ。</summary>
    Task<CallDecision> ChooseCallAsync(PlayerView view, CallOptions options);
}

public enum TurnActionType { Discard, Tsumo, Ankan, Kakan, KyuushuKyuuhai }

/// <summary>手番での行動。</summary>
/// <param name="Kind">暗槓・加槓する牌種。</param>
public sealed record TurnAction(TurnActionType Type, Tile? Tile = null, bool Riichi = false, int Kind = -1)
{
    public static TurnAction Discard(Tile tile, bool riichi = false) => new(TurnActionType.Discard, tile, riichi);
    public static TurnAction Tsumo() => new(TurnActionType.Tsumo);
    public static TurnAction Ankan(int kind) => new(TurnActionType.Ankan, Kind: kind);
    public static TurnAction Kakan(int kind) => new(TurnActionType.Kakan, Kind: kind);
    public static TurnAction Kyuushu() => new(TurnActionType.KyuushuKyuuhai);
}

/// <summary>手番で選べる合法手。</summary>
/// <param name="TsumoScore">ツモ和了した場合の点数(和了できなければ null)。</param>
/// <param name="Discards">捨てられる牌(喰い替えで禁止された牌は含まない)。</param>
/// <param name="RiichiDiscards">リーチ宣言と同時に捨てられる牌(捨てると聴牌になる牌)。</param>
public sealed record TurnOptions(
    Tile? DrawnTile,
    ScoreResult? TsumoScore,
    IReadOnlyList<Tile> Discards,
    IReadOnlyList<Tile> RiichiDiscards,
    IReadOnlyList<int> AnkanKinds,
    IReadOnlyList<int> KakanKinds,
    bool CanKyuushu)
{
    public bool CanTsumo => TsumoScore is { IsValid: true };
    public bool CanRiichi => RiichiDiscards.Count > 0;
}

public enum CallType { Pass, Ron, Pon, Daiminkan, Chi }

/// <summary>鳴き・ロンの選択。</summary>
/// <param name="Tiles">鳴きに使う自分の手牌(チーは必須。ポン・大明槓は null なら自動で選ぶ)。</param>
public sealed record CallDecision(CallType Type, IReadOnlyList<Tile>? Tiles = null)
{
    public static CallDecision Pass { get; } = new(CallType.Pass);
    public static CallDecision Ron { get; } = new(CallType.Ron);
    public static CallDecision Pon(IReadOnlyList<Tile>? tiles = null) => new(CallType.Pon, tiles);
    public static CallDecision Daiminkan() => new(CallType.Daiminkan);
    public static CallDecision Chi(IReadOnlyList<Tile> tiles) => new(CallType.Chi, tiles);
}

/// <summary>他家の打牌に対して選べる合法手。</summary>
/// <param name="ChiCandidates">チーに使える自分の手牌 2 枚の組み合わせ。</param>
/// <param name="IsChankan">加槓に対する問い合わせ(ロンしか選べない)。</param>
public sealed record CallOptions(
    Tile Tile,
    int FromSeat,
    ScoreResult? RonScore,
    bool CanPon,
    bool CanDaiminkan,
    IReadOnlyList<IReadOnlyList<Tile>> ChiCandidates,
    bool IsChankan)
{
    public bool CanRon => RonScore is { IsValid: true };
    public bool CanChi => ChiCandidates.Count > 0;
    public bool HasAny => CanRon || CanPon || CanDaiminkan || CanChi;
}
