namespace Mahjong.Core;

/// <summary>
/// 「123m456p789s11z」のような表記から牌を作る。テストやコンソール入力で使う。
/// 字牌は 1z〜7z(東南西北白發中)または漢字(東南西北白發中、発も可)で書ける。
/// 0m / 0p / 0s は赤 5 を表す。
/// </summary>
public static class TileParser
{
    /// <summary>
    /// 表記を牌の列に変換する。<paramref name="usedIds"/> を渡すと、
    /// 複数回の呼び出しで同じ ID が重複しないように割り当てる。
    /// </summary>
    public static List<Tile> Parse(string notation, ISet<int>? usedIds = null)
    {
        usedIds ??= new HashSet<int>();
        var result = new List<Tile>();
        var pendingDigits = new List<char>();

        foreach (char ch in notation)
        {
            if (char.IsWhiteSpace(ch)) continue;
            if (char.IsDigit(ch)) { pendingDigits.Add(ch); continue; }

            int honorIndex = "東南西北白發中".IndexOf(ch);
            if (ch == '発') honorIndex = 5;
            if (honorIndex >= 0)
            {
                result.Add(Allocate(27 + honorIndex, false, usedIds));
                continue;
            }

            int suitBase = ch switch
            {
                'm' => 0, 'p' => 9, 's' => 18, 'z' => 27,
                _ => throw new FormatException($"不明な文字です: '{ch}'"),
            };
            if (pendingDigits.Count == 0) throw new FormatException($"'{ch}' の前に数字がありません。");

            foreach (char d in pendingDigits)
            {
                int n = d - '0';
                bool red = n == 0;
                if (red) { if (suitBase == 27) throw new FormatException("字牌に赤はありません。"); n = 5; }
                if (suitBase == 27 && n is < 1 or > 7) throw new FormatException($"字牌は 1z〜7z です: {n}z");
                result.Add(Allocate(suitBase + n - 1, red, usedIds));
            }
            pendingDigits.Clear();
        }

        if (pendingDigits.Count > 0) throw new FormatException("末尾の数字に m/p/s/z が付いていません。");
        return result;
    }

    /// <summary>牌 1 枚を表記から作る(例: "5m", "0p", "中")。</summary>
    public static Tile ParseOne(string notation, ISet<int>? usedIds = null)
    {
        var tiles = Parse(notation, usedIds);
        if (tiles.Count != 1) throw new FormatException($"牌 1 枚を指定してください: {notation}");
        return tiles[0];
    }

    /// <summary>牌の列を「123m456p東」のような短い表記に戻す。</summary>
    public static string ToNotation(IEnumerable<Tile> tiles)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var group in tiles.OrderBy(t => t.Kind).GroupBy(t => t.Suit))
        {
            if (group.Key is Suit.Wind or Suit.Dragon)
            {
                foreach (var t in group) sb.Append(TileKinds.ToDisplay(t.Kind));
                continue;
            }
            foreach (var t in group) sb.Append(t.IsRed ? 0 : t.Number);
            sb.Append("mps"[(int)group.Key]);
        }
        return sb.ToString();
    }

    // 赤 5 は 4 枚のうち 0 番目の ID を使う。通常の 5 は 1〜3 番目を優先して使う。
    private static Tile Allocate(int kind, bool red, ISet<int> used)
    {
        int[] order = red ? [0] : TileKinds.NumberOf(kind) == 5 ? [1, 2, 3, 0] : [0, 1, 2, 3];
        foreach (int copy in order)
        {
            int id = kind * 4 + copy;
            if (used.Add(id)) return new Tile(id, red);
        }
        throw new FormatException($"{TileKinds.ToDisplay(kind)} が 5 枚以上あります。");
    }
}
