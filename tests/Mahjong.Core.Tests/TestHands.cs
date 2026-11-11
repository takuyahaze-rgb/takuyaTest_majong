using Mahjong.Core;

namespace Mahjong.Core.Tests;

/// <summary>テスト用に手牌・和了状況を文字列から作る補助クラス。</summary>
public static class TestHands
{
    public static readonly ScoreCalculator Calculator = ScoreCalculator.CreateDefault();

    public static int[] Counts(string notation)
    {
        var c = new int[TileKinds.Count];
        foreach (var t in TileParser.Parse(notation)) c[t.Kind]++;
        return c;
    }

    /// <summary>
    /// 副露の表記を解析する。空白区切りで、先頭の文字が種類を表す。
    /// c=チー, p=ポン, k=大明槓, a=暗槓, x=加槓。例: "c123m p777z a1111s"
    /// </summary>
    public static List<Meld> ParseMelds(string melds, ISet<int> used)
    {
        var list = new List<Meld>();
        foreach (var token in melds.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var type = token[0] switch
            {
                'c' => MeldType.Chi, 'p' => MeldType.Pon, 'k' => MeldType.Daiminkan,
                'a' => MeldType.Ankan, 'x' => MeldType.Kakan,
                _ => throw new FormatException(token),
            };
            list.Add(new Meld(type, TileParser.Parse(token[1..], used), type == MeldType.Ankan ? 0 : 3));
        }
        return list;
    }

    /// <param name="closed">和了牌を除く閉じた牌。</param>
    /// <param name="win">和了牌。</param>
    /// <param name="doras">ドラの種類(表示牌ではない)。例: "2m"</param>
    public static WinContext Ctx(
        string closed, string win, bool tsumo = false, bool riichi = false, string melds = "",
        TileKind seat = TileKind.South, TileKind round = TileKind.East, string doras = "")
    {
        var used = new HashSet<int>();
        var meldList = ParseMelds(melds, used);
        var hand = new Hand(TileParser.Parse(closed, used), meldList);
        var winTile = TileParser.ParseOne(win, used);
        var doraKinds = TileParser.Parse(doras).Select(t => t.KindEnum).ToList();
        return new WinContext(hand, winTile, tsumo, riichi, false, round, seat, doraKinds);
    }

    public static ScoreResult Score(WinContext ctx) => Calculator.Calculate(ctx);
}
