using System.Text;
using Mahjong.AI;
using Mahjong.ConsoleApp;
using Mahjong.Core;
using Mahjong.Core.Yakus;
using Mahjong.Engine;
using Microsoft.Extensions.DependencyInjection;

// 使い方:
//   dotnet run --project src/Mahjong.Console                       … 人間 1 人 + CPU 3 人で対局
//   dotnet run --project src/Mahjong.Console -- sim 100            … AI 4 人で 100 半荘の自動対戦
// オプション: --seed N / --tonpuu(東風戦) / --cpu 1|2(対局の CPU の強さ) / --levels 2,1,1,1(sim の参加 AI)

Console.OutputEncoding = Encoding.UTF8;
try { Console.InputEncoding = Encoding.UTF8; } catch (IOException) { }

// 役と AI は DI で登録する(差し替え・追加はここだけで行える)
var services = new ServiceCollection();
foreach (var yaku in StandardYaku.Create()) services.AddSingleton(yaku);
services.AddSingleton(sp => new ScoreCalculator(sp.GetServices<IYaku>()));
services.AddSingleton<AgentFactory>();
using var provider = services.BuildServiceProvider();

var options = CommandLine.Parse(args);
if (options is null)
{
    CommandLine.PrintUsage();
    return 1;
}

var rules = new RuleSet { Length = options.Tonpuu ? GameLength.Tonpuu : GameLength.Hanchan };
var factory = provider.GetRequiredService<AgentFactory>();
var calculator = provider.GetRequiredService<ScoreCalculator>();

if (options.Mode == "sim")
    return await Simulation.RunAsync(options.Games, options.Seed ?? 1, options.Levels, rules, factory, calculator);

// 人間 1 人 + CPU 3 人
var input = Console.In;
IPlayerAgent[] agents =
[
    new ConsoleHumanAgent(input),
    factory.Create(options.CpuLevel, 1),
    factory.Create(options.CpuLevel, 2),
    factory.Create(options.CpuLevel, 3),
];
int seed = options.Seed ?? Environment.TickCount;
Console.WriteLine($"シード: {seed}(--seed {seed} で同じ牌山を再現できます)  CPU: {AgentFactory.DisplayName(options.CpuLevel)}");

var engine = new GameEngine(agents, seed, rules, calculator);
engine.AddObserver(new ConsoleObserver(input), seat: 0);
try
{
    await engine.PlayAsync();
}
catch (OperationCanceledException ex)
{
    Console.WriteLine();
    Console.WriteLine(ex.Message);
}
return 0;

namespace Mahjong.ConsoleApp
{
    internal sealed record CommandLine(string Mode, int Games, int? Seed, bool Tonpuu, AiLevel CpuLevel, AiLevel[] Levels)
    {
        public static CommandLine? Parse(string[] args)
        {
            string mode = "play";
            int games = 100;
            int? seed = null;
            bool tonpuu = false;
            var cpu = AiLevel.Efficiency;
            AiLevel[] levels = [AiLevel.Efficiency, AiLevel.Efficiency, AiLevel.Efficiency, AiLevel.Efficiency];

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string? Next() => i + 1 < args.Length ? args[++i] : null;
                switch (a)
                {
                    case "play" or "sim": mode = a; break;
                    case "--seed" when int.TryParse(Next(), out int s): seed = s; break;
                    case "--tonpuu": tonpuu = true; break;
                    case "--cpu" when int.TryParse(Next(), out int c) && Enum.IsDefined((AiLevel)c): cpu = (AiLevel)c; break;
                    case "--levels":
                    {
                        var parts = Next()?.Split(',');
                        if (parts is not { Length: 4 } || !parts.All(p => int.TryParse(p, out int l) && Enum.IsDefined((AiLevel)l)))
                            return null;
                        levels = parts.Select(p => (AiLevel)int.Parse(p)).ToArray();
                        break;
                    }
                    case "help" or "--help" or "-h": return null;
                    default:
                        if (mode == "sim" && int.TryParse(a, out int n) && n > 0) { games = n; break; }
                        return null;
                }
            }
            return new CommandLine(mode, games, seed, tonpuu, cpu, levels);
        }

        public static void PrintUsage()
        {
            Console.WriteLine("""
                麻雀(リーチ麻雀・四人打ち)

                使い方:
                  Mahjong.Console [play] [--seed N] [--tonpuu] [--cpu 1|2]
                      人間 1 人 + CPU 3 人で対局します。
                  Mahjong.Console sim [回数] [--seed N] [--tonpuu] [--levels 2,1,1,1]
                      AI 4 人の自動対戦を繰り返し、成績を表示します。

                AI の強さ: 1 = ランダム, 2 = 牌効率
                """);
        }
    }
}
