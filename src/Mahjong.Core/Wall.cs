namespace Mahjong.Core;

/// <summary>
/// 牌山(王牌・ドラ表示牌を含む)。乱数シードが同じなら同じ牌山を再現する。
/// </summary>
/// <remarks>
/// 136 枚のうち末尾 14 枚を王牌とする。王牌の並びは
/// 0〜3 番目が嶺上牌、4・6・8・10・12 番目がドラ表示牌、5・7・9・11・13 番目が裏ドラ表示牌。
/// </remarks>
public sealed class Wall
{
    public const int TotalTiles = 136;
    public const int DeadWallSize = 14;
    private const int MaxRinshan = 4;
    private const int MaxDoraIndicators = 5;

    private readonly Tile[] _tiles;
    private int _drawn;
    private int _rinshanDrawn;
    private int _revealedDora = 1;

    /// <summary>シードからシャッフルした牌山を作る。</summary>
    public Wall(int seed, RuleSet? rules = null)
    {
        rules ??= RuleSet.Default;
        _tiles = CreateAllTiles(rules.RedFiveCount);
        var random = new Random(seed);
        // Fisher–Yates シャッフル
        for (int i = _tiles.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (_tiles[i], _tiles[j]) = (_tiles[j], _tiles[i]);
        }
    }

    /// <summary>並び順を指定して牌山を作る(テスト・牌譜再現用)。</summary>
    public Wall(IReadOnlyList<Tile> orderedTiles)
    {
        if (orderedTiles.Count != TotalTiles)
            throw new ArgumentException($"牌山は {TotalTiles} 枚必要です(指定 {orderedTiles.Count} 枚)。");
        if (orderedTiles.Select(t => t.Id).Distinct().Count() != TotalTiles)
            throw new ArgumentException("牌 ID が重複しています。");
        _tiles = orderedTiles.ToArray();
    }

    /// <summary>赤ドラ設定に従って 136 枚を ID 順に作る。</summary>
    public static Tile[] CreateAllTiles(int redFiveCount = 3)
    {
        var redIds = new HashSet<int>();
        int[] fiveKinds = [4, 13, 22];   // 5m, 5p, 5s
        for (int i = 0; i < Math.Min(redFiveCount, 3); i++) redIds.Add(fiveKinds[i] * 4);
        return Enumerable.Range(0, TotalTiles).Select(id => new Tile(id, redIds.Contains(id))).ToArray();
    }

    /// <summary>ツモれる残り枚数(嶺上牌を取るごとに海底が 1 枚ずつ手前になる)。</summary>
    public int Remaining => TotalTiles - DeadWallSize - _drawn - _rinshanDrawn;

    public int RinshanRemaining => MaxRinshan - _rinshanDrawn;

    /// <summary>全 136 枚(並び順どおり)。テスト・牌譜用。</summary>
    public IReadOnlyList<Tile> AllTiles => _tiles;

    public Tile Draw()
    {
        if (Remaining <= 0) throw new InvalidOperationException("牌山にツモれる牌が残っていません。");
        return _tiles[_drawn++];
    }

    /// <summary>嶺上牌をツモる。</summary>
    public Tile DrawFromDeadWall()
    {
        if (_rinshanDrawn >= MaxRinshan) throw new InvalidOperationException("嶺上牌が残っていません。");
        return DeadWall(_rinshanDrawn++);
    }

    /// <summary>槓ドラを 1 枚めくる。</summary>
    public void RevealDora()
    {
        if (_revealedDora >= MaxDoraIndicators) throw new InvalidOperationException("ドラ表示牌はこれ以上めくれません。");
        _revealedDora++;
    }

    public IReadOnlyList<Tile> DoraIndicators =>
        Enumerable.Range(0, _revealedDora).Select(i => DeadWall(4 + i * 2)).ToList();

    public IReadOnlyList<Tile> UraDoraIndicators =>
        Enumerable.Range(0, _revealedDora).Select(i => DeadWall(5 + i * 2)).ToList();

    /// <summary>表ドラの種類(表示牌の次の牌)。</summary>
    public IReadOnlyList<TileKind> Doras =>
        DoraIndicators.Select(t => (TileKind)TileKinds.DoraFromIndicator(t.Kind)).ToList();

    /// <summary>裏ドラの種類。</summary>
    public IReadOnlyList<TileKind> UraDoras =>
        UraDoraIndicators.Select(t => (TileKind)TileKinds.DoraFromIndicator(t.Kind)).ToList();

    private Tile DeadWall(int index) => _tiles[TotalTiles - DeadWallSize + index];
}
