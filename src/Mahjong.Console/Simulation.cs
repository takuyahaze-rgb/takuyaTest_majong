using System.Diagnostics;
using Mahjong.AI;
using Mahjong.Core;
using Mahjong.Engine;

namespace Mahjong.ConsoleApp;

/// <summary>
/// AI 4 人の自動対戦を繰り返し、強さの比較と異常の検出をする。
/// 席の有利不利をなくすため、半荘ごとに AI の座る席を 1 つずつずらす。
/// </summary>
internal static class Simulation
{
    private sealed class Stats
    {
        public int RankSum, ScoreSum, Wins, DealIns, Rounds;
        public readonly int[] RankCounts = new int[4];
    }

    /// <summary>局ごとの和了・放銃を数える観測者。</summary>
    private sealed class Counter(Func<int, Stats> statsOfSeat) : IGameObserver
    {
        public void OnEvent(GameEvent e)
        {
            if (e is not RoundEnded end) return;
            for (int seat = 0; seat < 4; seat++) statsOfSeat(seat).Rounds++;
            foreach (var w in end.Result.Wins)
            {
                statsOfSeat(w.Winner).Wins++;
                if (w.Loser is int l) statsOfSeat(l).DealIns++;
            }
        }
    }

    public static async Task<int> RunAsync(int games, int seed, AiLevel[] levels, RuleSet rules,
        AgentFactory factory, ScoreCalculator calculator)
    {
        Console.WriteLine($"AI 対戦シミュレーション: {games} 回, シード {seed}, {(rules.Length == GameLength.Tonpuu ? "東風戦" : "東南戦")}");
        Console.WriteLine("参加: " + string.Join(" / ", levels.Select((l, i) => $"AI{i} {AgentFactory.DisplayName(l)}")));

        var stats = levels.Select(_ => new Stats()).ToArray();
        int errors = 0;
        var sw = Stopwatch.StartNew();

        for (int g = 0; g < games; g++)
        {
            int rotation = g % 4;
            int AgentIndexOf(int seat) => (seat + rotation) % 4;

            var agents = Enumerable.Range(0, 4)
                .Select(seat => factory.Create(levels[AgentIndexOf(seat)], seed: seed * 7919 + g * 4 + seat))
                .ToArray();
            var engine = new GameEngine(agents, seed + g, rules, calculator);
            engine.AddObserver(new Counter(seat => stats[AgentIndexOf(seat)]));

            GameResult result;
            try
            {
                result = await engine.PlayAsync();
            }
            catch (Exception ex)
            {
                errors++;
                Console.WriteLine($"[エラー] {g} 回目(シード {seed + g}): {ex.Message}");
                continue;
            }

            if (result.FinalScores.Sum() != 4 * rules.StartingPoints)
            {
                errors++;
                Console.WriteLine($"[異常] {g} 回目: 点数の合計が {result.FinalScores.Sum()} 点です。");
            }

            for (int rank = 0; rank < 4; rank++)
            {
                int seat = result.Ranking[rank];
                var s = stats[AgentIndexOf(seat)];
                s.RankSum += rank + 1;
                s.RankCounts[rank]++;
                s.ScoreSum += result.FinalScores[seat];
            }

            if ((g + 1) % Math.Max(1, games / 10) == 0)
                Console.WriteLine($"  {g + 1}/{games} 回 完了 ({sw.Elapsed.TotalSeconds:F1} 秒)");
        }

        Console.WriteLine();
        Console.WriteLine("AI   強さ            平均順位  1位率  4位率  平均点   和了率  放銃率");
        for (int i = 0; i < stats.Length; i++)
        {
            var s = stats[i];
            int n = Math.Max(1, s.RankCounts.Sum());
            int rounds = Math.Max(1, s.Rounds);
            Console.WriteLine(
                $"AI{i}  {AgentFactory.DisplayName(levels[i]),-14} {(double)s.RankSum / n,6:F2}  " +
                $"{100.0 * s.RankCounts[0] / n,5:F1}% {100.0 * s.RankCounts[3] / n,5:F1}% {s.ScoreSum / n,7}  " +
                $"{100.0 * s.Wins / rounds,5:F1}% {100.0 * s.DealIns / rounds,5:F1}%");
        }
        Console.WriteLine();
        Console.WriteLine($"所要時間 {sw.Elapsed.TotalSeconds:F1} 秒(1 半荘あたり {sw.Elapsed.TotalMilliseconds / games:F1} ms)、エラー・異常 {errors} 件");
        return errors == 0 ? 0 : 1;
    }
}
