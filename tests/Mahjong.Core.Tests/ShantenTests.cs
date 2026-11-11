using System.Diagnostics;
using FsCheck.Xunit;
using Mahjong.Core;

namespace Mahjong.Core.Tests;

public class ShantenTests
{
    [Theory]
    [InlineData("123m456p789s11222z", -1)]     // 和了形
    [InlineData("123m456p789s1122z", 0)]       // シャンポン聴牌
    [InlineData("123456789m1234p", 0)]         // 聴牌
    [InlineData("1122m3344p5566s7z", 0)]       // 七対子聴牌
    [InlineData("19m19p19s1234567z", 0)]       // 国士無双 13 面待ち
    [InlineData("147m258p369s1234z", 6)]       // バラバラ(七対子・国士・面子手の最小が 6)
    [InlineData("123m456p789s1234z", 2)]       // 3 面子+孤立牌 4 枚
    [InlineData("1122334455667z", 0)]          // 字牌の 6 対子+1 枚(七対子聴牌)
    public void 向聴数(string hand, int expected)
    {
        Assert.Equal(expected, Shanten.Calculate(TestHands.Counts(hand)));
    }

    [Fact]
    public void 副露があっても計算できる()
    {
        // 1 副露 + 10 枚: 123m 456p 78s 99s → 聴牌
        Assert.Equal(0, Shanten.Calculate(TestHands.Counts("123m456p78s99s"), meldCount: 1));
        // 4 副露 + 単騎 1 枚 → 聴牌
        Assert.Equal(0, Shanten.Calculate(TestHands.Counts("5m"), meldCount: 4));
    }

    [Fact]
    public void 向聴数の計算は1回あたり1ミリ秒未満()
    {
        var hands = Enumerable.Range(0, 1000).Select(i =>
        {
            var c = new int[34];
            foreach (var t in new Wall(seed: i).AllTiles.Take(14)) c[t.Kind]++;
            return c;
        }).ToList();

        Shanten.Calculate(hands[0]);   // JIT のウォームアップ
        var sw = Stopwatch.StartNew();
        foreach (var c in hands) Shanten.Calculate(c);
        sw.Stop();
        Assert.True(sw.Elapsed.TotalMilliseconds / hands.Count < 1.0, $"平均 {sw.Elapsed.TotalMilliseconds / hands.Count:F3} ms");
    }

    /// <summary>ランダムな 14 枚で「和了形 ⇔ 向聴数 -1 ⇔ 分解が 1 つ以上ある」が成り立つ。</summary>
    [Property(MaxTest = 300)]
    public bool 和了形なら向聴数はマイナス1(int seed)
    {
        var tiles = new Wall(seed).AllTiles.Take(14).ToList();
        var counts = new int[34];
        foreach (var t in tiles) counts[t.Kind]++;

        int shanten = Shanten.Calculate(counts);
        var hand = new Hand(tiles.Take(13));
        bool hasDecomposition = HandDecomposer.Enumerate(hand, tiles[13], isTsumo: true).Count > 0;
        return shanten >= -1 && (shanten == -1) == hasDecomposition;
    }

    /// <summary>実際の和了形を組み立てて、必ず向聴数 -1 になることを確かめる。</summary>
    [Property(MaxTest = 300)]
    public bool 組み立てた和了形は必ず和了(int seed)
    {
        var rnd = new Random(seed);
        var counts = new int[34];
        int pair = rnd.Next(34);
        counts[pair] += 2;
        for (int i = 0; i < 4; i++)
        {
            for (int retry = 0; retry < 100; retry++)
            {
                int k = rnd.Next(34);
                bool seq = k < 27 && k % 9 <= 6 && rnd.Next(2) == 0;
                if (seq && counts[k] < 4 && counts[k + 1] < 4 && counts[k + 2] < 4)
                {
                    counts[k]++; counts[k + 1]++; counts[k + 2]++;
                    break;
                }
                if (!seq && counts[k] <= 1)
                {
                    counts[k] += 3;
                    break;
                }
            }
        }
        if (counts.Sum() != 14) return true;   // 組み立てに失敗した場合は対象外
        return Shanten.Calculate(counts) == -1;
    }

    [Property(MaxTest = 100)]
    public bool 牌の総数は常に136(int seed) =>
        new Wall(seed).AllTiles.Select(t => t.Id).Distinct().Count() == 136;
}
