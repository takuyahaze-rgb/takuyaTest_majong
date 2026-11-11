using Mahjong.Core;

namespace Mahjong.Web.Game;

/// <summary>
/// 対局を一時停止するための関所。閉じている間、エージェントは開くまで待つ
/// (局の結果を表示している間、次の局が進まないようにする)。
/// </summary>
public sealed class Gate
{
    private TaskCompletionSource? _tcs;

    public Task WaitAsync() => _tcs?.Task ?? Task.CompletedTask;

    public void Close() => _tcs ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Open()
    {
        var tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult();
    }
}

/// <summary>
/// ブラウザの人間プレイヤー。エンジンからの問い合わせを TaskCompletionSource で保留し、
/// 牌やボタンのタップで完了させる。
/// </summary>
public sealed class WebHumanAgent(Gate gate) : IPlayerAgent
{
    private TaskCompletionSource<TurnAction>? _turn;
    private TaskCompletionSource<CallDecision>? _call;

    /// <summary>入力待ちの状態が変わったときに呼ばれる(画面の再描画用)。</summary>
    public event Action? Changed;

    /// <summary>手番の入力待ち中なら、その合法手。</summary>
    public TurnOptions? Turn { get; private set; }

    /// <summary>鳴き・ロンの入力待ち中なら、その合法手。</summary>
    public CallOptions? Call { get; private set; }

    public async Task<TurnAction> ChooseTurnActionAsync(PlayerView view, TurnOptions options)
    {
        await gate.WaitAsync();
        _turn = new TaskCompletionSource<TurnAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        Turn = options;
        Changed?.Invoke();
        try { return await _turn.Task; }
        finally { Turn = null; Changed?.Invoke(); }
    }

    public async Task<CallDecision> ChooseCallAsync(PlayerView view, CallOptions options)
    {
        await gate.WaitAsync();
        _call = new TaskCompletionSource<CallDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        Call = options;
        Changed?.Invoke();
        try { return await _call.Task; }
        finally { Call = null; Changed?.Invoke(); }
    }

    public void Submit(TurnAction action)
    {
        if (Turn is not null) _turn?.TrySetResult(action);
    }

    public void Submit(CallDecision decision)
    {
        if (Call is not null) _call?.TrySetResult(decision);
    }

    /// <summary>対局をやめるときに、待っている問い合わせを打ち切る。</summary>
    public void Cancel()
    {
        _turn?.TrySetCanceled();
        _call?.TrySetCanceled();
    }
}

/// <summary>
/// CPU。打牌の前に少し間をあけ、画面が固まらず、打牌の流れが目で追えるようにする。
/// </summary>
public sealed class CpuAgent(IPlayerAgent inner, Gate gate, Func<int> delayMs, CancellationToken token) : IPlayerAgent
{
    public async Task<TurnAction> ChooseTurnActionAsync(PlayerView view, TurnOptions options)
    {
        await gate.WaitAsync();
        await Task.Delay(delayMs(), token);   // 対局をやめたら TaskCanceledException で止まる
        return await inner.ChooseTurnActionAsync(view, options);
    }

    public async Task<CallDecision> ChooseCallAsync(PlayerView view, CallOptions options)
    {
        await gate.WaitAsync();
        token.ThrowIfCancellationRequested();
        await Task.Yield();
        return await inner.ChooseCallAsync(view, options);
    }
}

/// <summary>関数でイベントを受け取る観測者。</summary>
public sealed class DelegateObserver(Action<Mahjong.Engine.GameEvent> handler) : Mahjong.Engine.IGameObserver
{
    public void OnEvent(Mahjong.Engine.GameEvent e) => handler(e);
}
