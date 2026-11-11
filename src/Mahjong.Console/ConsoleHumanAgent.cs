using Mahjong.Core;

namespace Mahjong.ConsoleApp;

/// <summary>
/// 人間プレイヤー。コンソールから入力を読み、合法手だけを受け付ける。
/// 入力が終わった(EOF)ときは OperationCanceledException を投げて対局を中断する。
/// </summary>
internal sealed class ConsoleHumanAgent(TextReader input) : IPlayerAgent
{
    private const string TurnHelp =
        "操作: 番号 または 牌名(例 5m, 東)で打牌 / Enter: ツモ切り / t: ツモ和了 / r: リーチ / a: 暗槓 / k: 加槓 / q: 九種九牌";

    public Task<TurnAction> ChooseTurnActionAsync(PlayerView view, TurnOptions o)
    {
        // 表示用の並び: 理牌した手牌+ツモ牌(右端)
        var drawn = view.DrawnTile;
        var display = view.Closed.Where(t => drawn is not Tile d || t.Id != d.Id)
            .OrderBy(t => t.Kind).ThenBy(t => t.IsRed).ToList();
        if (drawn is Tile dt) display.Add(dt);

        Ui.RenderBoard(view);
        Ui.RenderHand(display, drawn is not null, view.Melds);

        if (o.CanTsumo)
        {
            Ui.Line("ツモ和了できます(t):", ConsoleColor.Green);
            Ui.WriteScore(o.TsumoScore!);
        }

        var specials = new List<string>();
        if (o.CanTsumo) specials.Add("t:ツモ");
        if (o.CanRiichi) specials.Add("r:リーチ");
        if (o.AnkanKinds.Count > 0) specials.Add("a:暗槓");
        if (o.KakanKinds.Count > 0) specials.Add("k:加槓");
        if (o.CanKyuushu) specials.Add("q:九種九牌");
        string prompt = view.IsRiichi
            ? "リーチ中: Enter でツモ切り"
            : "打牌する牌の番号を入力" + (drawn is null ? "" : "(Enter でツモ切り)");
        if (specials.Count > 0) prompt += " / " + string.Join(" ", specials);

        while (true)
        {
            Ui.Line(prompt, ConsoleColor.White);
            Console.Write("> ");
            string line = ReadLine().Trim();

            if (line is "?" or "h") { Ui.Line(TurnHelp); continue; }

            if (line == "")
            {
                if (drawn is Tile d && o.Discards.Any(t => t.Id == d.Id)) return Done(TurnAction.Discard(d));
                Ui.Line("ツモ牌がないので、捨てる牌の番号を入力してください。");
                continue;
            }

            if (line == "t")
            {
                if (o.CanTsumo) return Done(TurnAction.Tsumo());
                Ui.Line("ツモ和了はできません。");
                continue;
            }
            if (line == "q")
            {
                if (o.CanKyuushu) return Done(TurnAction.Kyuushu());
                Ui.Line("九種九牌は宣言できません。");
                continue;
            }
            if (line == "a" || line == "k")
            {
                var kinds = line == "a" ? o.AnkanKinds : o.KakanKinds;
                if (kinds.Count == 0) { Ui.Line("槓できる牌がありません。"); continue; }
                int kind = kinds.Count == 1 ? kinds[0] : AskKind(kinds);
                if (kind < 0) continue;
                return Done(line == "a" ? TurnAction.Ankan(kind) : TurnAction.Kakan(kind));
            }

            bool riichi = line.StartsWith('r');
            if (riichi)
            {
                if (!o.CanRiichi) { Ui.Line("リーチはできません。"); continue; }
                string rest = line[1..].Trim();
                if (rest == "")
                {
                    var nums = display.Select((t, i) => (t, i))
                        .Where(x => o.RiichiDiscards.Any(r => r.Kind == x.t.Kind)).Select(x => x.i + 1);
                    Ui.Line($"リーチ宣言牌の番号を入力してください(候補: {string.Join(", ", nums)})");
                    Console.Write("> ");
                    rest = ReadLine().Trim();
                }
                line = rest;
            }

            var tile = ParseTile(line, display);
            if (tile is null) { Ui.Line("入力が正しくありません(? でヘルプ)。"); continue; }
            if (!o.Discards.Any(t => t.Id == tile.Value.Id)) { Ui.Line("その牌は今は捨てられません(喰い替え禁止など)。"); continue; }
            if (riichi && !o.RiichiDiscards.Any(r => r.Kind == tile.Value.Kind)) { Ui.Line("その牌を切ると聴牌になりません。"); continue; }
            return Done(TurnAction.Discard(tile.Value, riichi));
        }
    }

    public Task<CallDecision> ChooseCallAsync(PlayerView view, CallOptions o)
    {
        Console.Write($"{Ui.SeatNames[o.FromSeat]} の{(o.IsChankan ? "加槓" : "打牌")} ");
        Ui.WriteTile(o.Tile);
        Console.Write(" に対して: ");
        var choices = new List<string>();
        if (o.CanRon) choices.Add("r:ロン");
        if (o.CanPon) choices.Add("p:ポン");
        if (o.CanDaiminkan) choices.Add("k:カン");
        if (o.CanChi) choices.Add("c:チー");
        choices.Add("Enter:パス");
        Console.WriteLine(string.Join(" ", choices));
        if (o.CanRon) Ui.WriteScore(o.RonScore!);

        while (true)
        {
            Console.Write("> ");
            switch (ReadLine().Trim())
            {
                case "": return Task.FromResult(CallDecision.Pass);
                case "r" when o.CanRon: return Task.FromResult(CallDecision.Ron);
                case "p" when o.CanPon: return Task.FromResult(CallDecision.Pon());
                case "k" when o.CanDaiminkan: return Task.FromResult(CallDecision.Daiminkan());
                case "c" when o.CanChi:
                {
                    if (o.ChiCandidates.Count == 1) return Task.FromResult(CallDecision.Chi(o.ChiCandidates[0]));
                    for (int i = 0; i < o.ChiCandidates.Count; i++)
                    {
                        Console.Write($"  {i + 1}: ");
                        Ui.WriteTiles(o.ChiCandidates[i], "");
                        Console.WriteLine();
                    }
                    Console.Write("どの組み合わせでチーしますか > ");
                    if (int.TryParse(ReadLine().Trim(), out int n) && n >= 1 && n <= o.ChiCandidates.Count)
                        return Task.FromResult(CallDecision.Chi(o.ChiCandidates[n - 1]));
                    Ui.Line("番号が正しくありません。");
                    break;
                }
                default:
                    Ui.Line("その操作はできません。" + string.Join(" ", choices));
                    break;
            }
        }
    }

    private int AskKind(IReadOnlyList<int> kinds)
    {
        Ui.Line("どの牌で槓しますか: " + string.Join(" ", kinds.Select((k, i) => $"{i + 1}:{TileKinds.ToDisplay(k)}")));
        Console.Write("> ");
        return int.TryParse(ReadLine().Trim(), out int n) && n >= 1 && n <= kinds.Count ? kinds[n - 1] : -1;
    }

    /// <summary>番号(1 始まり)または牌名から、表示中の手牌の牌を探す。</summary>
    private static Tile? ParseTile(string text, IReadOnlyList<Tile> display)
    {
        if (int.TryParse(text, out int n))
            return n >= 1 && n <= display.Count ? display[n - 1] : null;
        try
        {
            int kind = TileParser.ParseOne(text).Kind;
            // 同じ種類が複数あるときは、赤ドラではない方・ツモ牌ではない方を優先
            return display.Where(t => t.Kind == kind).OrderBy(t => t.IsRed).Cast<Tile?>().FirstOrDefault();
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // パイプ入力の先頭に付くことがある BOM(﻿)は取り除く
    private string ReadLine() =>
        (input.ReadLine() ?? throw new OperationCanceledException("入力が終了したため対局を中断しました。")).Replace("﻿", "");

    private static Task<TurnAction> Done(TurnAction a) => Task.FromResult(a);
}
