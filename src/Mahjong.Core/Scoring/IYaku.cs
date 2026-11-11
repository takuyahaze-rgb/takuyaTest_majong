namespace Mahjong.Core;

/// <summary>役 1 つ。DI や <see cref="Yakus.StandardYaku"/> で登録して使う。</summary>
public interface IYaku
{
    string Name { get; }

    /// <summary>役満か。役満が成立した分解では、通常役とドラは数えない。</summary>
    bool IsYakuman => false;

    /// <summary>
    /// 成立していれば翻数を返す(0 なら不成立)。役満は 13(ダブル役満は 26)を返す。
    /// 副露で下がる役は ctx.IsMenzen を見る。
    /// </summary>
    int Evaluate(WinContext ctx, Decomposition d);
}
