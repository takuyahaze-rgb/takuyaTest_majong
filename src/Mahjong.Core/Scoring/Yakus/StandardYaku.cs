namespace Mahjong.Core.Yakus;

/// <summary>
/// 標準のリーチ麻雀の役一式。DI を使う層では、この一覧を IYaku として登録する。
/// ローカル役を足す場合は、IYaku を実装したクラスを一覧に追加するだけでよい。
/// </summary>
public static class StandardYaku
{
    public static IReadOnlyList<IYaku> Create() =>
    [
        // 1 翻
        new Riichi(), new Ippatsu(), new MenzenTsumo(), new Pinfu(), new Tanyao(), new Iipeikou(),
        new DragonYakuhai(TileKind.White), new DragonYakuhai(TileKind.Green), new DragonYakuhai(TileKind.Red),
        new SeatWindYakuhai(), new RoundWindYakuhai(),
        new Haitei(), new Houtei(), new Rinshan(), new Chankan(),
        // 2 翻
        new DoubleRiichi(), new Chiitoitsu(), new SanshokuDoujun(), new Ittsu(), new Chanta(),
        new Toitoi(), new Sanankou(), new SanshokuDoukou(), new Sankantsu(), new Shousangen(), new Honroutou(),
        // 3 翻以上
        new Ryanpeikou(), new Honitsu(), new Junchan(), new Chinitsu(),
        // 役満
        new KokushiMusou(), new Suuankou(), new Daisangen(), new Shousuushii(), new Daisuushii(),
        new Tsuuiisou(), new Chinroutou(), new Ryuuiisou(), new Chuuren(), new Suukantsu(),
        new Tenhou(), new Chiihou(),
    ];
}
