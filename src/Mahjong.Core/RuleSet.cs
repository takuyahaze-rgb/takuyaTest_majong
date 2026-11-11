namespace Mahjong.Core;

/// <summary>対局の長さ。</summary>
public enum GameLength { Tonpuu, Hanchan }

/// <summary>複数人が同時にロンしたときの扱い。</summary>
public enum MultipleRonRule
{
    /// <summary>ダブロンまで認める(三家和は流局)。</summary>
    DoubleRon,
    /// <summary>頭ハネ(放銃者から見て下家に近い 1 人だけが和了)。</summary>
    HeadBump,
}

/// <summary>ルール設定。既定値は設計書 2 章の表に従う。</summary>
public sealed record RuleSet
{
    public static RuleSet Default { get; } = new();

    public GameLength Length { get; init; } = GameLength.Hanchan;

    /// <summary>持ち点。</summary>
    public int StartingPoints { get; init; } = 25000;

    /// <summary>返し点。</summary>
    public int ReturnPoints { get; init; } = 30000;

    /// <summary>赤ドラの枚数(0 または 3。3 なら萬子・筒子・索子の 5 が各 1 枚)。</summary>
    public int RedFiveCount { get; init; } = 3;

    /// <summary>喰いタン(副露した断么九)を認めるか。</summary>
    public bool Kuitan { get; init; } = true;

    /// <summary>後付けを認めるか(判定はエンジン側で行う)。</summary>
    public bool Atozuke { get; init; } = true;

    /// <summary>西入・サドンデスを行うか。</summary>
    public bool WestRound { get; init; } = false;

    /// <summary>流し満貫を認めるか。</summary>
    public bool NagashiMangan { get; init; } = true;

    public MultipleRonRule MultipleRon { get; init; } = MultipleRonRule.DoubleRon;
}
