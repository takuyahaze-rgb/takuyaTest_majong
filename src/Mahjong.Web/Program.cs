using Mahjong.AI;
using Mahjong.Core;
using Mahjong.Core.Yakus;
using Mahjong.Web;
using Mahjong.Web.Game;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 役と AI は DI で登録する(差し替え・追加はここだけで行える)
foreach (var yaku in StandardYaku.Create()) builder.Services.AddSingleton(yaku);
builder.Services.AddSingleton(sp => new ScoreCalculator(sp.GetServices<IYaku>()));
builder.Services.AddSingleton<AgentFactory>();
builder.Services.AddSingleton<GameSession>();

await builder.Build().RunAsync();
