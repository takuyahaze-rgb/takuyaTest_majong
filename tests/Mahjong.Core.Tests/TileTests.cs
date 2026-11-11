using Mahjong.Core;

namespace Mahjong.Core.Tests;

public class TileTests
{
    [Theory]
    [InlineData(0, 0, Suit.Man, 1)]
    [InlineData(35, 8, Suit.Man, 9)]
    [InlineData(36, 9, Suit.Pin, 1)]
    [InlineData(107, 26, Suit.Sou, 9)]
    [InlineData(108, 27, Suit.Wind, 0)]
    [InlineData(135, 33, Suit.Dragon, 0)]
    public void IDから種類と色と数字が求まる(int id, int kind, Suit suit, int number)
    {
        var t = new Tile(id);
        Assert.Equal(kind, t.Kind);
        Assert.Equal(suit, t.Suit);
        Assert.Equal(number, t.Number);
    }

    [Fact]
    public void 老頭牌と字牌の判定()
    {
        Assert.True(new Tile(0).IsTerminal);        // 1m
        Assert.True(new Tile(35).IsTerminal);       // 9m
        Assert.False(new Tile(4).IsTerminal);       // 2m
        Assert.False(new Tile(108).IsTerminal);     // 東は老頭牌ではない
        Assert.True(new Tile(108).IsHonor);
    }

    [Fact]
    public void 表記を解析できる()
    {
        var tiles = TileParser.Parse("123m0p東中7z");
        Assert.Equal(["1m", "2m", "3m", "赤5p", "東", "中", "中"], tiles.Select(t => t.ToString()));
        Assert.True(tiles[3].IsRed);
        Assert.Equal(tiles.Count, tiles.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void 同じ牌が5枚以上なら例外()
    {
        Assert.Throws<FormatException>(() => TileParser.Parse("11111m"));
    }

    [Theory]
    [InlineData("1m", "2m")]
    [InlineData("9m", "1m")]
    [InlineData("9s", "1s")]
    [InlineData("北", "東")]
    [InlineData("中", "白")]
    [InlineData("白", "發")]
    public void ドラ表示牌からドラを求める(string indicator, string dora)
    {
        int ind = TileParser.ParseOne(indicator).Kind;
        Assert.Equal(TileParser.ParseOne(dora).Kind, TileKinds.DoraFromIndicator(ind));
    }
}

public class WallTests
{
    [Fact]
    public void 牌山は136枚で重複がない()
    {
        var wall = new Wall(seed: 1);
        Assert.Equal(136, wall.AllTiles.Count);
        Assert.Equal(136, wall.AllTiles.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void 同じシードなら同じ牌山になる()
    {
        var a = new Wall(seed: 42).AllTiles.Select(t => t.Id);
        var b = new Wall(seed: 42).AllTiles.Select(t => t.Id);
        var c = new Wall(seed: 43).AllTiles.Select(t => t.Id);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public void 赤ドラの枚数はルールに従う(int red)
    {
        var wall = new Wall(1, new RuleSet { RedFiveCount = red });
        Assert.Equal(red, wall.AllTiles.Count(t => t.IsRed));
    }

    [Fact]
    public void ツモは122枚までで嶺上牌を取ると海底が減る()
    {
        var wall = new Wall(seed: 7);
        Assert.Equal(122, wall.Remaining);
        wall.DrawFromDeadWall();
        Assert.Equal(121, wall.Remaining);
        for (int i = 0; i < 121; i++) wall.Draw();
        Assert.Throws<InvalidOperationException>(() => wall.Draw());
    }

    [Fact]
    public void 槓ドラをめくるとドラ表示牌が増える()
    {
        var wall = new Wall(seed: 7);
        Assert.Single(wall.DoraIndicators);
        wall.RevealDora();
        Assert.Equal(2, wall.DoraIndicators.Count);
        Assert.Equal(2, wall.UraDoraIndicators.Count);
    }
}
