using Mahjong.Core;
using Mahjong.Engine;

namespace Mahjong.ConsoleApp;

/// <summary>
/// 人間の席から見た対局の出来事を表示する。局の終わりでは Enter を待つ。
/// </summary>
internal sealed class ConsoleObserver(TextReader input) : IGameObserver
{
    private int[] _scores = new int[4];

    public void OnEvent(GameEvent e)
    {
        switch (e)
        {
            case GameStarted g:
                _scores = g.Scores.ToArray();
                Ui.Line("対局開始! あなたは席0(起家)です。? を入力するといつでも操作方法を表示します。", ConsoleColor.Green);
                break;

            case RoundStarted r:
                _scores = r.Scores.ToArray();
                Console.WriteLine();
                Ui.Line($"==== {Ui.WindName(r.RoundWind)}{r.RoundNumber}局 {r.Honba}本場  親: {Ui.SeatNames[r.Dealer]} ====", ConsoleColor.Green);
                break;

            case TileDiscarded d when d.Riichi:
                Console.Write($"{Ui.SeatNames[d.Seat]} がリーチを宣言 (宣言牌 ");
                Ui.WriteTile(d.Tile);
                Console.WriteLine(")");
                break;

            case RiichiAccepted a:
                _scores[a.Seat] -= 1000;
                break;

            case MeldDeclared m:
            {
                string name = m.Meld.Type switch
                {
                    MeldType.Chi => "チー", MeldType.Pon => "ポン", MeldType.Ankan => "暗槓", _ => "カン",
                };
                Console.Write($"{Ui.SeatNames[m.Seat]} {name} ");
                Ui.WriteMeld(m.Meld);
                Console.WriteLine();
                break;
            }

            case DoraRevealed dr:
                Console.Write("新しいドラ表示牌: ");
                Ui.WriteTile(dr.Indicator);
                Console.WriteLine();
                break;

            case RoundEnded end:
                ShowRoundResult(end.Result);
                break;

            case GameEnded g:
                ShowGameResult(g.Result);
                break;
        }
    }

    private void ShowRoundResult(RoundResult r)
    {
        Console.WriteLine();
        Ui.Line("──── 局終了 ────", ConsoleColor.Green);
        switch (r.Type)
        {
            case RoundEndType.Tsumo or RoundEndType.Ron:
                foreach (var w in r.Wins)
                {
                    string how = w.Loser is int l ? $"ロン({Ui.SeatNames[l]}から)" : "ツモ";
                    Ui.Line($"{Ui.SeatNames[w.Winner]} の{how}", ConsoleColor.Yellow);
                    Console.Write("  ");
                    Ui.WriteTiles(w.Hand.Where(t => t.Id != w.WinTile.Id).OrderBy(t => t.Kind));
                    Console.Write("  和了牌 ");
                    Ui.WriteTile(w.WinTile);
                    Console.WriteLine();
                    Ui.WriteScore(w.Score);
                }
                break;
            case RoundEndType.ExhaustiveDraw when r.NagashiManganSeats.Count > 0:
                Ui.Line("流し満貫: " + string.Join(", ", r.NagashiManganSeats.Select(s => Ui.SeatNames[s])));
                break;
            case RoundEndType.ExhaustiveDraw:
                Ui.Line("荒牌平局  聴牌: " + (r.TenpaiSeats.Count == 0 ? "なし" : string.Join(", ", r.TenpaiSeats.Select(s => Ui.SeatNames[s]))));
                break;
            default:
                Ui.Line($"途中流局({r.AbortiveReason})");
                break;
        }

        for (int i = 0; i < 4; i++)
        {
            int before = _scores[i];
            int after = before + r.ScoreDeltas[i];
            string delta = r.ScoreDeltas[i] == 0 ? "" : $" ({r.ScoreDeltas[i]:+#;-#})";
            Console.WriteLine($"  {Ui.SeatNames[i],-4} {before,6} → {after,6}{delta}");
            _scores[i] = after;
        }

        Console.Write("Enter で次へ > ");
        input.ReadLine();
    }

    private static void ShowGameResult(GameResult g)
    {
        Console.WriteLine();
        Ui.Line("════ 終局 ════", ConsoleColor.Green);
        for (int rank = 0; rank < 4; rank++)
        {
            int seat = g.Ranking[rank];
            Ui.Line($"  {rank + 1}位 {Ui.SeatNames[seat],-4} {g.FinalScores[seat],6}点",
                seat == 0 ? ConsoleColor.Yellow : null);
        }
        Ui.Line($"  ({g.RoundsPlayed}局)");
    }
}
