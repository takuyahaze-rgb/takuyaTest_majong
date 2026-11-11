using Mahjong.Core;

namespace Mahjong.Engine;

/// <summary>
/// 1 局の進行(ステートマシン)。配牌 → ツモ → 打牌選択 → 応答判定 → … → 局終了。
/// 開始前に <see cref="GameState"/> の牌山・親・本場などを設定しておくこと。
/// </summary>
/// <remarks>
/// 簡略化している点:
/// ・槓ドラは暗槓・明槓とも槓の直後にめくる。
/// ・後付けなしルール(<see cref="RuleSet.Atozuke"/> = false)の判定はまだ行わない。
/// </remarks>
public sealed class RoundEngine
{
    private enum DrawKind { None, Normal, Rinshan }

    private readonly GameState _s;
    private readonly IReadOnlyList<IPlayerAgent> _agents;
    private readonly ScoreCalculator _calc;
    private readonly EventHub _hub;

    private bool _interrupted;                          // 鳴き・槓が入ったか(第 1 巡の判定用)
    private readonly int[] _discardCounts = new int[GameState.PlayerCount];
    private readonly List<int> _kanSeats = new();       // 槓をした席(槓の数だけ)
    private HashSet<int> _kuikaeKinds = new();          // 鳴いた直後に捨てられない牌種

    public RoundEngine(GameState state, IReadOnlyList<IPlayerAgent> agents, ScoreCalculator? calculator = null, EventHub? hub = null)
    {
        if (agents.Count != GameState.PlayerCount) throw new ArgumentException("エージェントは 4 人分必要です。");
        _s = state;
        _agents = agents;
        _calc = calculator ?? ScoreCalculator.CreateDefault();
        _hub = hub ?? new EventHub();
    }

    public async Task<RoundResult> PlayAsync()
    {
        Deal();

        int seat = _s.Dealer;
        var draw = DrawKind.Normal;
        while (true)
        {
            var p = _s.Players[seat];
            _s.CurrentSeat = seat;

            // ── ツモ ──
            if (draw == DrawKind.None)
            {
                p.DrawnTile = null;
            }
            else
            {
                if (draw == DrawKind.Normal && _s.Wall.Remaining == 0) return ExhaustiveDraw();
                var drawn = draw == DrawKind.Normal ? _s.Wall.Draw() : _s.Wall.DrawFromDeadWall();
                p.Hand.Closed.Add(drawn);
                p.DrawnTile = drawn;
                _hub.Publish(new TileDrawn(seat, drawn, draw == DrawKind.Rinshan));
            }
            bool afterCall = draw == DrawKind.None;
            bool isRinshan = draw == DrawKind.Rinshan;

            // ── 打牌選択 ──
            var options = BuildTurnOptions(seat, afterCall, isRinshan);
            var action = await _agents[seat].ChooseTurnActionAsync(PlayerView.Create(_s, seat), options);
            ValidateTurnAction(seat, action, options);
            _kuikaeKinds = new HashSet<int>();

            switch (action.Type)
            {
                case TurnActionType.Tsumo:
                    return FinishWins([(seat, (Tile)p.DrawnTile!, options.TsumoScore!)], loser: null);

                case TurnActionType.KyuushuKyuuhai:
                    return Abortive("九種九牌");

                case TurnActionType.Ankan:
                    DoAnkan(seat, action.Kind);
                    draw = DrawKind.Rinshan;
                    continue;

                case TurnActionType.Kakan:
                {
                    var added = DoKakan(seat, action.Kind);
                    // 槍槓: 加槓した牌でのロンだけを問い合わせる
                    var chankan = await CollectCallsAsync(seat, added, isChankan: true);
                    if (chankan.Rons.Count > 0) return ResolveRons(chankan.Rons, seat, added, isChankan: true);
                    RevealKanDora();
                    draw = DrawKind.Rinshan;
                    continue;
                }
            }

            // ── 打牌 ──
            var tile = action.Tile!.Value;
            bool isTsumogiri = p.DrawnTile?.Id == tile.Id;
            bool isFirstDiscard = _discardCounts[seat] == 0;
            p.Hand.Closed.RemoveAll(t => t.Id == tile.Id);
            p.DrawnTile = null;
            p.IsIppatsu = false;              // リーチ後、自分の次の打牌で一発は消える
            p.IsTemporaryFuriten = false;     // 同巡内フリテンは自分の打牌で解除
            p.River.Add(tile, isTsumogiri, action.Riichi);
            _discardCounts[seat]++;
            _hub.Publish(new TileDiscarded(seat, tile, action.Riichi, isTsumogiri));

            // ── 応答判定 ──
            var calls = await CollectCallsAsync(seat, tile, isChankan: false);
            if (calls.Rons.Count > 0) return ResolveRons(calls.Rons, seat, tile, isChankan: false);

            // リーチ成立(宣言牌でロンされなかった)
            if (action.Riichi)
            {
                p.IsRiichi = true;
                p.IsDoubleRiichi = isFirstDiscard && !_interrupted;
                p.IsIppatsu = true;
                p.Score -= 1000;
                _s.RiichiSticks++;
                _hub.Publish(new RiichiAccepted(seat));
                if (_s.Players.All(x => x.IsRiichi)) return Abortive("四家立直");
            }

            if (IsSuufonRenda()) return Abortive("四風連打");
            if (_kanSeats.Count == 4 && _kanSeats.Distinct().Count() > 1 && calls.Call is null) return Abortive("四開槓");

            if (calls.Call is { } call)
            {
                ExecuteCall(call.Seat, call.Decision, seat, tile);
                seat = call.Seat;
                draw = call.Decision.Type == CallType.Daiminkan ? DrawKind.Rinshan : DrawKind.None;
                continue;
            }

            seat = (seat + 1) % GameState.PlayerCount;
            draw = DrawKind.Normal;
        }
    }

    // ───────────────────────── 配牌 ─────────────────────────

    /// <summary>親から順に 4 枚ずつ 3 回、最後に 1 枚ずつ配る。</summary>
    private void Deal()
    {
        foreach (var p in _s.Players) p.ResetForRound();

        for (int round = 0; round < 4; round++)
        {
            int count = round < 3 ? 4 : 1;
            for (int i = 0; i < GameState.PlayerCount; i++)
            {
                var p = _s.Players[(_s.Dealer + i) % GameState.PlayerCount];
                for (int j = 0; j < count; j++) p.Hand.Closed.Add(_s.Wall.Draw());
            }
        }

        _hub.Publish(new RoundStarted(_s.RoundWind, _s.RoundNumber, _s.Dealer, _s.Honba, _s.RiichiSticks,
            _s.Wall.DoraIndicators[0], _s.Players.Select(p => p.Score).ToList()));
        foreach (var p in _s.Players) _hub.Publish(new HandDealt(p.Seat, p.Hand.Closed.ToList()));
    }

    // ───────────────────────── 合法手の生成 ─────────────────────────

    private bool CanKan => _kanSeats.Count < 4 && _s.Wall.Remaining > 0 && _s.Wall.RinshanRemaining > 0;

    private TurnOptions BuildTurnOptions(int seat, bool afterCall, bool isRinshan)
    {
        var p = _s.Players[seat];
        var closed = p.Hand.Closed;
        var counts = p.Hand.ToCounts();

        ScoreResult? tsumoScore = null;
        IReadOnlyList<Tile> riichiDiscards = [];
        var ankan = new List<int>();
        var kakan = new List<int>();
        bool canKyuushu = false;

        if (!afterCall && p.DrawnTile is Tile drawn)
        {
            // ツモ和了
            var handWithoutDrawn = new Hand(closed.Where(t => t.Id != drawn.Id), p.Hand.Melds);
            var ctx = CreateWinContext(seat, handWithoutDrawn, drawn, isTsumo: true) with
            {
                IsRinshan = isRinshan,
                IsLastTile = !isRinshan && _s.Wall.Remaining == 0,
                IsFirstTurn = !_interrupted && _discardCounts[seat] == 0,
            };
            var score = _calc.Calculate(ctx);
            if (score.IsValid) tsumoScore = score;

            // リーチ: 門前・1000 点以上・残り 4 枚以上・打牌後に聴牌
            if (!p.IsRiichi && p.Hand.IsMenzen && p.Score >= 1000 && _s.Wall.Remaining >= 4
                && Shanten.Calculate(counts, p.Hand.Melds.Count) <= 0)
            {
                riichiDiscards = DistinctByKind(closed)
                    .Where(t => { counts[t.Kind]--; bool ok = Waits.Calculate(counts, p.Hand.Melds.Count).Count > 0; counts[t.Kind]++; return ok; })
                    .ToList();
            }

            if (CanKan)
            {
                // 暗槓(リーチ中はツモ牌で、待ちが変わらない場合だけ)
                for (int k = 0; k < TileKinds.Count; k++)
                {
                    if (counts[k] != 4) continue;
                    if (p.IsRiichi && !RiichiAnkanAllowed(p, k, drawn)) continue;
                    ankan.Add(k);
                }
                // 加槓
                if (!p.IsRiichi)
                    kakan.AddRange(p.Hand.Melds.Where(m => m.Type == MeldType.Pon && counts[m.Kind] > 0).Select(m => m.Kind));
            }

            // 九種九牌: 鳴きのない第 1 巡で么九牌が 9 種以上
            canKyuushu = !_interrupted && _discardCounts[seat] == 0
                && Enumerable.Range(0, TileKinds.Count).Count(k => counts[k] > 0 && TileKinds.IsTerminalOrHonor(k)) >= 9;
        }

        IReadOnlyList<Tile> discards;
        if (p.IsRiichi && p.DrawnTile is Tile d)
        {
            discards = [d];   // リーチ中はツモ切りのみ
        }
        else
        {
            var allowed = closed.Where(t => !_kuikaeKinds.Contains(t.Kind)).ToList();
            discards = allowed.Count > 0 ? allowed : closed.ToList();
        }

        return new TurnOptions(p.DrawnTile, tsumoScore, discards, riichiDiscards, ankan, kakan, canKyuushu);
    }

    private static bool RiichiAnkanAllowed(Player p, int kind, Tile drawn)
    {
        if (drawn.Kind != kind) return false;
        var before = p.Hand.ToCounts();
        before[kind]--;   // ツモ前の 13 枚
        var waitsBefore = Waits.Calculate(before, p.Hand.Melds.Count);
        var after = (int[])before.Clone();
        after[kind] -= 3;
        var waitsAfter = Waits.Calculate(after, p.Hand.Melds.Count + 1);
        return waitsBefore.SequenceEqual(waitsAfter);
    }

    private static IEnumerable<Tile> DistinctByKind(IEnumerable<Tile> tiles) =>
        tiles.GroupBy(t => (t.Kind, t.IsRed)).Select(g => g.First());

    private CallOptions BuildCallOptions(int seat, int fromSeat, Tile tile, bool isChankan, IReadOnlyList<int> waits)
    {
        var p = _s.Players[seat];

        ScoreResult? ronScore = null;
        if (waits.Contains(tile.Kind) && !p.IsFuriten(waits))
        {
            var ctx = CreateWinContext(seat, p.Hand, tile, isTsumo: false) with
            {
                IsChankan = isChankan,
                IsLastTile = !isChankan && _s.Wall.Remaining == 0,
            };
            var score = _calc.Calculate(ctx);
            if (score.IsValid) ronScore = score;
        }

        bool canPon = false, canKan = false;
        var chi = new List<IReadOnlyList<Tile>>();
        // 槍槓はロンのみ。リーチ中・海底牌(河底)は鳴けない
        if (!isChankan && !p.IsRiichi && _s.Wall.Remaining > 0)
        {
            var counts = p.Hand.ToCounts();
            canPon = counts[tile.Kind] >= 2;
            canKan = counts[tile.Kind] >= 3 && CanKan;

            if (seat == (fromSeat + 1) % GameState.PlayerCount && tile.Kind < 27)
            {
                int n = tile.Number;
                foreach (var (a, b) in new[] { (-2, -1), (-1, 1), (1, 2) })
                {
                    if (n + a < 1 || n + b > 9) continue;
                    var ta = p.Hand.Closed.Where(t => t.Kind == tile.Kind + a).OrderBy(t => t.IsRed).FirstOrDefault();
                    var tb = p.Hand.Closed.Where(t => t.Kind == tile.Kind + b).OrderBy(t => t.IsRed).FirstOrDefault();
                    if (counts[tile.Kind + a] > 0 && counts[tile.Kind + b] > 0) chi.Add([ta, tb]);
                }
            }
        }

        return new CallOptions(tile, fromSeat, ronScore, canPon, canKan, chi, isChankan);
    }

    private WinContext CreateWinContext(int seat, Hand hand, Tile winTile, bool isTsumo)
    {
        var p = _s.Players[seat];
        return new WinContext(hand, winTile, isTsumo, p.IsRiichi, p.IsIppatsu, _s.RoundWind, _s.SeatWindOf(seat), _s.Wall.Doras)
        {
            UraDoras = _s.Wall.UraDoras,
            IsDoubleRiichi = p.IsDoubleRiichi,
            Honba = _s.Honba,
            Rules = _s.Rules,
        };
    }

    // ───────────────────────── 検証 ─────────────────────────

    private static void ValidateTurnAction(int seat, TurnAction a, TurnOptions o)
    {
        bool ok = a.Type switch
        {
            TurnActionType.Tsumo => o.CanTsumo,
            TurnActionType.KyuushuKyuuhai => o.CanKyuushu,
            TurnActionType.Ankan => o.AnkanKinds.Contains(a.Kind),
            TurnActionType.Kakan => o.KakanKinds.Contains(a.Kind),
            TurnActionType.Discard => a.Tile is Tile t
                && o.Discards.Any(x => x.Id == t.Id)
                && (!a.Riichi || o.RiichiDiscards.Any(x => x.Kind == t.Kind)),
            _ => false,
        };
        if (!ok) throw new InvalidOperationException($"席{seat}の行動 {a} は合法手ではありません。");
    }

    private static void ValidateCall(int seat, CallDecision d, CallOptions o)
    {
        bool ok = d.Type switch
        {
            CallType.Pass => true,
            CallType.Ron => o.CanRon,
            CallType.Pon => o.CanPon,
            CallType.Daiminkan => o.CanDaiminkan,
            CallType.Chi => d.Tiles is { Count: 2 } tiles
                && o.ChiCandidates.Any(c => c.Select(t => t.Kind).Order().SequenceEqual(tiles.Select(t => t.Kind).Order())),
            _ => false,
        };
        if (!ok) throw new InvalidOperationException($"席{seat}の応答 {d.Type} は合法手ではありません。");
    }

    // ───────────────────────── 応答判定 ─────────────────────────

    private sealed record CallResult(List<int> Rons, (int Seat, CallDecision Decision)? Call);

    /// <summary>
    /// 他の 3 家全員に問い合わせ、ロン > ポン・大明槓 > チー の順で採用する。
    /// 和了牌を見逃した席は同巡内フリテン(リーチ中なら永続フリテン)にする。
    /// </summary>
    private async Task<CallResult> CollectCallsAsync(int fromSeat, Tile tile, bool isChankan)
    {
        var rons = new List<int>();
        (int, CallDecision)? ponKan = null, chi = null;

        for (int i = 1; i < GameState.PlayerCount; i++)
        {
            int seat = (fromSeat + i) % GameState.PlayerCount;
            var p = _s.Players[seat];
            var waits = Waits.Calculate(p.Hand);
            var options = BuildCallOptions(seat, fromSeat, tile, isChankan, waits);

            var decision = CallDecision.Pass;
            if (options.HasAny)
            {
                decision = await _agents[seat].ChooseCallAsync(PlayerView.Create(_s, seat), options);
                ValidateCall(seat, decision, options);
            }

            switch (decision.Type)
            {
                case CallType.Ron: rons.Add(seat); break;
                case CallType.Pon or CallType.Daiminkan: ponKan = (seat, decision); break;
                case CallType.Chi: chi = (seat, decision); break;
            }

            // 見逃し → フリテン
            if (decision.Type != CallType.Ron && waits.Contains(tile.Kind))
            {
                p.IsTemporaryFuriten = true;
                if (p.IsRiichi) p.IsRiichiFuriten = true;
            }
        }

        return new CallResult(rons, ponKan ?? chi);
    }

    // ───────────────────────── 鳴き・槓 ─────────────────────────

    private void ExecuteCall(int seat, CallDecision decision, int fromSeat, Tile tile)
    {
        var p = _s.Players[seat];
        BreakIppatsuAndFirstTurn();
        _s.Players[fromSeat].River.MarkLastCalled();

        List<Tile> fromHand;
        MeldType type;
        switch (decision.Type)
        {
            case CallType.Pon:
                type = MeldType.Pon;
                fromHand = PickTiles(p, decision.Tiles, tile.Kind, 2);
                _kuikaeKinds = [tile.Kind];
                break;
            case CallType.Daiminkan:
                type = MeldType.Daiminkan;
                fromHand = PickTiles(p, null, tile.Kind, 3);
                _kanSeats.Add(seat);
                break;
            default:
                type = MeldType.Chi;
                fromHand = decision.Tiles!.Select(t => p.Hand.Closed.First(x => x.Kind == t.Kind)).ToList();
                _kuikaeKinds = ChiKuikaeKinds(tile, fromHand);
                break;
        }

        foreach (var t in fromHand) p.Hand.Closed.Remove(t);
        var meld = new Meld(type, fromHand.Append(tile).OrderBy(t => t.Kind).ToList(), fromSeat);
        p.Hand.Melds.Add(meld);
        _hub.Publish(new MeldDeclared(seat, meld));

        if (type == MeldType.Daiminkan) RevealKanDora();
    }

    private static List<Tile> PickTiles(Player p, IReadOnlyList<Tile>? requested, int kind, int count)
    {
        if (requested is { } r && r.Count == count && r.All(t => t.Kind == kind && p.Hand.Closed.Contains(t)))
            return r.ToList();
        return p.Hand.Closed.Where(t => t.Kind == kind).Take(count).ToList();
    }

    /// <summary>喰い替え禁止: 鳴いた牌と、両面チーの場合はその反対側の牌(スジ)。</summary>
    private static HashSet<int> ChiKuikaeKinds(Tile called, List<Tile> fromHand)
    {
        var set = new HashSet<int> { called.Kind };
        int low = fromHand.Min(t => t.Kind), high = fromHand.Max(t => t.Kind);
        if (called.Kind < low && called.Number <= 6) set.Add(called.Kind + 3);   // 3 を 45 でチー → 6 も禁止
        if (called.Kind > high && called.Number >= 4) set.Add(called.Kind - 3);  // 6 を 45 でチー → 3 も禁止
        return set;
    }

    private void DoAnkan(int seat, int kind)
    {
        var p = _s.Players[seat];
        var tiles = p.Hand.Closed.Where(t => t.Kind == kind).ToList();
        p.Hand.Closed.RemoveAll(t => t.Kind == kind);
        var meld = new Meld(MeldType.Ankan, tiles, seat);
        p.Hand.Melds.Add(meld);
        _kanSeats.Add(seat);
        BreakIppatsuAndFirstTurn();
        _hub.Publish(new MeldDeclared(seat, meld));
        RevealKanDora();
    }

    private Tile DoKakan(int seat, int kind)
    {
        var p = _s.Players[seat];
        var tile = p.Hand.Closed.First(t => t.Kind == kind);
        p.Hand.Closed.Remove(tile);
        int index = p.Hand.Melds.FindIndex(m => m.Type == MeldType.Pon && m.Kind == kind);
        var pon = p.Hand.Melds[index];
        var meld = new Meld(MeldType.Kakan, pon.Tiles.Append(tile).ToList(), pon.FromSeat);
        p.Hand.Melds[index] = meld;
        _kanSeats.Add(seat);
        BreakIppatsuAndFirstTurn();
        _hub.Publish(new MeldDeclared(seat, meld));
        return tile;
    }

    private void RevealKanDora()
    {
        _s.Wall.RevealDora();
        _hub.Publish(new DoraRevealed(_s.Wall.DoraIndicators[^1]));
    }

    private void BreakIppatsuAndFirstTurn()
    {
        _interrupted = true;
        foreach (var x in _s.Players) x.IsIppatsu = false;
    }

    /// <summary>四風連打: 鳴きなしで、4 人の第 1 打がすべて同じ風牌。</summary>
    private bool IsSuufonRenda()
    {
        if (_interrupted || _discardCounts.Any(c => c != 1)) return false;
        var first = _s.Players.Select(p => p.River.Discards[0].Tile.Kind).ToList();
        return TileKinds.IsWind(first[0]) && first.All(k => k == first[0]);
    }

    // ───────────────────────── 局の終了 ─────────────────────────

    private RoundResult ResolveRons(List<int> rons, int loser, Tile tile, bool isChankan)
    {
        // rons は放銃者の下家から順に並んでいる
        if (rons.Count >= 2 && _s.Rules.MultipleRon == MultipleRonRule.HeadBump) rons = [rons[0]];
        if (rons.Count == 3) return Abortive("三家和");

        var winners = new List<(int, Tile, ScoreResult)>();
        for (int i = 0; i < rons.Count; i++)
        {
            int seat = rons[i];
            // 本場は放銃者から見て一番近い和了者だけが受け取る
            var ctx = CreateWinContext(seat, _s.Players[seat].Hand, tile, isTsumo: false) with
            {
                Honba = i == 0 ? _s.Honba : 0,
                IsChankan = isChankan,
                IsLastTile = !isChankan && _s.Wall.Remaining == 0,
            };
            var score = _calc.Calculate(ctx);
            if (!score.IsValid) throw new InvalidOperationException("ロンの点数計算に失敗しました。");
            winners.Add((seat, tile, score));
        }
        return FinishWins(winners, loser);
    }

    private RoundResult FinishWins(List<(int Seat, Tile WinTile, ScoreResult Score)> winners, int? loser)
    {
        var deltas = new int[GameState.PlayerCount];
        var wins = new List<WinInfo>();
        bool isTsumo = loser is null;

        for (int i = 0; i < winners.Count; i++)
        {
            var (seat, winTile, score) = winners[i];
            var p = _s.Players[seat];
            var hand = isTsumo ? new Hand(p.Hand.Closed.Where(t => t.Id != winTile.Id), p.Hand.Melds) : p.Hand;

            if (isTsumo)
            {
                for (int other = 0; other < GameState.PlayerCount; other++)
                {
                    if (other == seat) continue;
                    int pay = other == _s.Dealer ? score.TsumoPaymentFromDealer : score.TsumoPaymentFromNonDealer;
                    if (seat == _s.Dealer) pay = score.TsumoPaymentFromNonDealer;
                    deltas[other] -= pay;
                    deltas[seat] += pay;
                }
            }
            else
            {
                deltas[loser!.Value] -= score.RonPayment;
                deltas[seat] += score.RonPayment;
            }

            if (i == 0)
            {
                // 供託のリーチ棒も一番近い和了者が受け取る
                deltas[seat] += _s.RiichiSticks * 1000;
                _s.RiichiSticks = 0;
            }

            var allTiles = hand.Closed.Append(winTile).Concat(hand.Melds.SelectMany(m => m.Tiles)).ToList();
            wins.Add(new WinInfo(seat, loser, score, allTiles, winTile));
        }

        return Finish(new RoundResult
        {
            Type = isTsumo ? RoundEndType.Tsumo : RoundEndType.Ron,
            Wins = wins,
            ScoreDeltas = deltas,
            DealerContinues = winners.Any(w => w.Seat == _s.Dealer),
        });
    }

    private RoundResult ExhaustiveDraw()
    {
        var deltas = new int[GameState.PlayerCount];
        var tenpai = _s.Players.Where(p => Waits.IsTenpai(p.Hand)).Select(p => p.Seat).ToList();

        var nagashi = !_s.Rules.NagashiMangan ? [] : _s.Players
            .Where(p => p.River.Discards.Count > 0
                && p.River.Discards.All(d => TileKinds.IsTerminalOrHonor(d.Tile.Kind) && !d.IsCalled))
            .Select(p => p.Seat).ToList();

        if (nagashi.Count > 0)
        {
            // 流し満貫は満貫のツモ和了と同じ支払い(本場・供託は動かさない)
            foreach (int seat in nagashi)
            {
                for (int other = 0; other < GameState.PlayerCount; other++)
                {
                    if (other == seat) continue;
                    int pay = seat == _s.Dealer || other == _s.Dealer ? 4000 : 2000;
                    deltas[other] -= pay;
                    deltas[seat] += pay;
                }
            }
        }
        else if (tenpai.Count is > 0 and < GameState.PlayerCount)
        {
            // ノーテン罰符 3000 点
            int receive = 3000 / tenpai.Count;
            int pay = 3000 / (GameState.PlayerCount - tenpai.Count);
            for (int seat = 0; seat < GameState.PlayerCount; seat++)
                deltas[seat] = tenpai.Contains(seat) ? receive : -pay;
        }

        return Finish(new RoundResult
        {
            Type = RoundEndType.ExhaustiveDraw,
            ScoreDeltas = deltas,
            TenpaiSeats = tenpai,
            NagashiManganSeats = nagashi,
            DealerContinues = tenpai.Contains(_s.Dealer),
        });
    }

    private RoundResult Abortive(string reason) => Finish(new RoundResult
    {
        Type = RoundEndType.AbortiveDraw,
        ScoreDeltas = new int[GameState.PlayerCount],
        AbortiveReason = reason,
        DealerContinues = true,
    });

    private RoundResult Finish(RoundResult result)
    {
        for (int i = 0; i < GameState.PlayerCount; i++) _s.Players[i].Score += result.ScoreDeltas[i];
        result = result with { FinalHands = _s.Players.Select(p => (IReadOnlyList<Tile>)p.Hand.Closed.ToList()).ToList() };
        _hub.Publish(new RoundEnded(result));
        return result;
    }
}
