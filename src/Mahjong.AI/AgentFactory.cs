using Mahjong.Core;

namespace Mahjong.AI;

/// <summary>AI の強さ。</summary>
public enum AiLevel
{
    /// <summary>Lv1: ランダム。</summary>
    Random = 1,
    /// <summary>Lv2: 牌効率。</summary>
    Efficiency = 2,
}

/// <summary>強さを指定して AI を作る。</summary>
public sealed class AgentFactory
{
    public IPlayerAgent Create(AiLevel level, int seed = 0) => level switch
    {
        AiLevel.Random => new RandomAgent(seed),
        AiLevel.Efficiency => new EfficiencyAgent(),
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "未対応の AI レベルです。"),
    };

    public static string DisplayName(AiLevel level) => level switch
    {
        AiLevel.Random => "Lv1(ランダム)",
        AiLevel.Efficiency => "Lv2(牌効率)",
        _ => level.ToString(),
    };
}
