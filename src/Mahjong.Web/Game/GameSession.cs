using Mahjong.AI;
using Mahjong.Core;
using Mahjong.Engine;

namespace Mahjong.Web.Game;

/// <summary>
/// ブラウザでの 1 回の対局。エンジンを動かし、画面が必要とする情報(結果・ログ・点数)をまとめる。
/// 人間は席 0(起家)に座る。
/// </summary>
public sealed class GameSession(AgentFactory factory, ScoreCalculator calculator)
{
    public static readonly string[] SeatNames = ["あなた", "CPU1", "CPU2", "CPU3"];

    private readonly Gate _gate = new();
    private int[] _scores = new int[4];

    // 対局ごとの番号。やめた対局から遅れて届くイベントを無視するために使う
    private int _runId;
    private CancellationTokenSource? _cts;

    /// <summary>状態が変わったときに呼ばれる(画面の再描画用)。</summary>
    public event Action? Changed;

    public GameEngine? Engine { get; private set; }
    public WebHumanAgent? Human { get; private set; }
    public bool IsRunning { get; private set; }
    public int Seed { get; private set; }
    public string? Error { get; private set; }

    /// <summary>CPU が打牌する前の待ち時間(ミリ秒)。</summary>
    public int CpuDelayMs { get; set; } = 400;

    /// <summary>表示中の局の結果(「次へ」を押すまで残る)。</summary>
    public RoundResult? RoundResult { get; private set; }
    public IReadOnlyList<int> ScoresBeforeRound { get; private set; } = [];

    public GameResult? FinalResult { get; private set; }

    /// <summary>直近の出来事(リーチ・鳴きなど)。</summary>
    public List<string> Log { get; } = new();

    /// <summary>人間(席 0)から見た現在の場の状況。</summary>
    public PlayerView? View => Engine is null ? null : PlayerView.Create(Engine.State, 0);

    public async Task StartAsync(GameLength length, AiLevel cpuLevel, int? seed)
    {
        Seed = seed ?? Random.Shared.Next(1, 1_000_000);
        Error = null;
        RoundResult = null;
        FinalResult = null;
        Log.Clear();
        _gate.Open();
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        int runId = ++_runId;

        Human = new WebHumanAgent(_gate);
        Human.Changed += Notify;
        IPlayerAgent[] agents =
        [
            Human,
            .. Enumerable.Range(1, 3).Select(i => (IPlayerAgent)new CpuAgent(
                factory.Create(cpuLevel, Seed + i), _gate, () => CpuDelayMs, _cts.Token)),
        ];

        Engine = new GameEngine(agents, Seed, new RuleSet { Length = length }, calculator);
        Engine.AddObserver(new DelegateObserver(ev => { if (runId == _runId) OnEvent(ev); }), seat: 0);
        IsRunning = true;
        Notify();

        try
        {
            await Engine.PlayAsync();
        }
        catch (TaskCanceledException)
        {
            // 「やめる」で中断した
        }
        catch (Exception ex)
        {
            if (runId == _runId) Error = ex.ToString();
        }
        if (runId == _runId) IsRunning = false;
        Notify();
    }

    /// <summary>局の結果を閉じて、次の局へ進める。</summary>
    public void NextRound()
    {
        RoundResult = null;
        _gate.Open();
        Notify();
    }

    /// <summary>対局をやめてタイトルに戻る。</summary>
    public void Quit()
    {
        _runId++;
        _cts?.Cancel();
        Human?.Cancel();
        IsRunning = false;
        Engine = null;
        Human = null;
        RoundResult = null;
        FinalResult = null;
        _gate.Open();
        Notify();
    }

    private void OnEvent(GameEvent e)
    {
        switch (e)
        {
            case GameStarted g:
                _scores = g.Scores.ToArray();
                break;
            case RoundStarted r:
                _scores = r.Scores.ToArray();
                Log.Clear();
                AddLog($"{TileKinds.ToDisplay((int)r.RoundWind)}{r.RoundNumber}局 {r.Honba}本場 開始");
                break;
            case TileDiscarded { Riichi: true } d:
                AddLog($"{SeatNames[d.Seat]} がリーチ");
                break;
            case RiichiAccepted a:
                _scores[a.Seat] -= 1000;
                break;
            case MeldDeclared m:
                AddLog($"{SeatNames[m.Seat]} が{MeldName(m.Meld.Type)}");
                break;
            case DoraRevealed:
                AddLog("新しいドラ");
                break;
            case RoundEnded end:
                ScoresBeforeRound = _scores.ToArray();
                for (int i = 0; i < 4; i++) _scores[i] += end.Result.ScoreDeltas[i];
                RoundResult = end.Result;
                _gate.Close();   // 「次へ」を押すまで止める
                break;
            case GameEnded g:
                FinalResult = g.Result;
                break;
        }
        Notify();
    }

    public static string MeldName(MeldType t) => t switch
    {
        MeldType.Chi => "チー", MeldType.Pon => "ポン", MeldType.Ankan => "暗槓", _ => "カン",
    };

    private void AddLog(string message)
    {
        Log.Add(message);
        if (Log.Count > 4) Log.RemoveAt(0);
    }

    private void Notify() => Changed?.Invoke();
}
