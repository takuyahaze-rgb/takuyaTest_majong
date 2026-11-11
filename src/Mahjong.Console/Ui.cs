using Mahjong.Core;

namespace Mahjong.ConsoleApp;

/// <summary>コンソールへの描画(牌は「1m 2p 東 中」のような文字で表示)。</summary>
internal static class Ui
{
    private static readonly bool UseColor = !Console.IsOutputRedirected;

    public static readonly string[] SeatNames = ["あなた", "CPU1", "CPU2", "CPU3"];

    public static string WindName(TileKind wind) => TileKinds.ToDisplay((int)wind);

    /// <summary>牌の文字列。出力をリダイレクトしているときは赤 5 を「0m」と書く。</summary>
    public static string TileText(Tile t) => t.IsRed && !UseColor ? $"0{"mps"[(int)t.Suit]}" : TileKinds.ToDisplay(t.Kind);

    public static void WriteTile(Tile t, ConsoleColor? color = null)
    {
        var c = color ?? (t.IsRed ? ConsoleColor.Red : t.IsHonor ? ConsoleColor.Yellow : (ConsoleColor?)null);
        WriteColored(TileText(t), c);
    }

    public static void WriteTiles(IEnumerable<Tile> tiles, string separator = " ")
    {
        bool first = true;
        foreach (var t in tiles)
        {
            if (!first) Console.Write(separator);
            WriteTile(t);
            first = false;
        }
    }

    public static void WriteMeld(Meld m)
    {
        Console.Write("[");
        WriteTiles(m.Tiles, "");
        Console.Write(m.Type == MeldType.Ankan ? "暗]" : "]");
    }

    public static void WriteColored(string text, ConsoleColor? color)
    {
        if (!UseColor || color is null) { Console.Write(text); return; }
        var old = Console.ForegroundColor;
        Console.ForegroundColor = color.Value;
        Console.Write(text);
        Console.ForegroundColor = old;
    }

    public static void Line(string text = "", ConsoleColor? color = null)
    {
        WriteColored(text, color);
        Console.WriteLine();
    }

    /// <summary>場の状況(全員の河・副露・点数)を表示する。</summary>
    public static void RenderBoard(PlayerView v)
    {
        Line(new string('─', 60), ConsoleColor.DarkGray);
        Console.Write($"{WindName(v.RoundWind)}{v.RoundNumber}局 {v.Honba}本場  供託{v.RiichiSticks}本  残り{v.WallRemaining}枚  ドラ表示: ");
        WriteTiles(v.DoraIndicators);
        Console.WriteLine();

        // 下家・対面・上家・自分の順に表示
        for (int i = 1; i <= 4; i++)
        {
            var p = v.Players[(v.Seat + i) % 4];
            Console.Write($"{SeatNames[p.Seat],-4} {WindName(p.SeatWind)}家 {p.Score,6}点");
            if (p.IsRiichi) WriteColored(" リーチ", ConsoleColor.Magenta);
            Console.WriteLine();

            Console.Write("  河: ");
            foreach (var d in p.River)
            {
                if (d.IsRiichiTile) Console.Write("[");
                WriteTile(d.Tile, d.IsCalled ? ConsoleColor.DarkGray : null);
                if (d.IsRiichiTile) Console.Write("]");
                Console.Write(" ");
            }
            if (p.Melds.Count > 0)
            {
                Console.Write("  副露: ");
                foreach (var m in p.Melds) { WriteMeld(m); Console.Write(" "); }
            }
            Console.WriteLine();
        }
    }

    /// <summary>手牌を番号付きで表示する。ツモ牌は少し離して右端に置く。</summary>
    public static void RenderHand(IReadOnlyList<Tile> display, bool lastIsDrawn, IReadOnlyList<Meld> melds)
    {
        Console.Write("手牌: ");
        for (int i = 0; i < display.Count; i++)
        {
            if (lastIsDrawn && i == display.Count - 1) Console.Write("  ");
            WriteTile(display[i]);
            Console.Write(" ");
        }
        foreach (var m in melds) { Console.Write(" "); WriteMeld(m); }
        Console.WriteLine();

        Console.Write("番号: ");
        for (int i = 0; i < display.Count; i++)
        {
            if (lastIsDrawn && i == display.Count - 1) Console.Write("  ");
            Console.Write((i + 1).ToString().PadRight(3));
        }
        Console.WriteLine();
    }

    public static void WriteScore(ScoreResult s)
    {
        foreach (var y in s.Yaku)
            Line(s.IsYakuman ? $"  {y.Name}" : $"  {y.Name} {y.Han}翻");
        string head = s.IsYakuman ? s.LimitName! : $"{s.Fu}符 {s.Han}翻" + (s.LimitName is null ? "" : $" {s.LimitName}");
        Line($"  {head}  {s.Payment}点", ConsoleColor.Cyan);
    }
}
