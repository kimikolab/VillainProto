using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 作戦マップのステージ（第440期）
//
// **盤・経路ごとの敵・湧き・名前・遊ぶときの規則を 1 つにまとめた入れ物。** 規則は1つも持たない
// ——進行は `Map11State` に残り、ここは「何を並べるか」だけを引き受ける。
// **ここには Godot の型を1つも使わない**（`Map11.cs` と同じ扱い・頭なしの門が同じクラスを回す）。
//
// 1-1（`Stage11`）は第439期までの `Map11.Roads` ／ `SpawnEnemy` ／ `AdoptedPortal` ／ `AdoptedTime` を
// **そのまま写した**もの——中身の定義は `Map11` に残し、ここは指すだけ（器具はいまも 1-1 の static を直に読む）。
//
// 1-2（`Stage12`・第440期 §2）は経路 3 本 ＋ 横の辺 2 本:
//
//         N0 ── N1 ── N2 ──┐
//        /      |          │
//   Home ─── C0 ── C1 ── C2 ─┼── Portal
//        \      |          │
//         S0 ── S1 ── S2 ──┘
//
// 横の辺（N1–C1・C1–S1）は**隊だけが使う**——敵は経路に沿って拠点へ向かうだけ（`TowardHome`）。
// =====================================================================================

/// <summary>作戦マップのステージ（盤 ＋ 経路ごとの敵 ＋ 湧き ＋ 遊ぶときの規則）。</summary>
public sealed class MapStage
{
    /// <summary>短い呼び名（「1-1」）。</summary>
    public required string Id { get; init; }
    /// <summary>画面の見出しと結果画面に出す名前（「検証用マップ 1-1」）。</summary>
    public required string Title { get; init; }
    /// <summary>盤を作る（<c>portalOn</c> が偽なら敵の拠点のマスは作らない）。</summary>
    public required Func<bool, MapGraph> Board { get; init; }
    /// <summary>経路ごとの敵部隊（手前から）。<b>経路の番号は盤の <see cref="MapGraph.Lanes"/> と同じ。</b></summary>
    public required IReadOnlyList<Map11.RoadNode>[] Lanes { get; init; }
    /// <summary>経路の名前（「北の道」など・盤のマスの名前もこれで始まる）。</summary>
    public required string[] LaneNames { get; init; }
    /// <summary>その経路に湧く部隊（中身・名前・背景の波）。<b>新しい敵は作らない</b>（どれも既存の波の写し）。</summary>
    public required Func<int, (Formation Enemy, string Name, int StageIndex)> Spawn { get; init; }
    /// <summary>遊ぶときの敵の拠点の規則。</summary>
    public required PortalRule Portal { get; init; }
    /// <summary>遊ぶときの時間の規則。</summary>
    public required TimeRule Time { get; init; }
    /// <summary>途中到着の規則（第441期 §2）。<b>既定は Off</b>（15 枚を最初に全部配る・第440期まで）。</summary>
    public ArrivalRule Arrival { get; init; } = ArrivalRule.Off;
    /// <summary>敵の予備隊の規則（第441期 §3）。<b>既定は Off</b>（第440期まで）。</summary>
    public ReserveRule Reserve { get; init; } = ReserveRule.Off;
    /// <summary>
    /// 隊の数（第442期 §0-3）。<b>既定は 3</b>（`Map11.Squads` の 3 隊・第441期まで）。
    /// 少なくすると `Map11.Squads` の先頭から数えて、それより後ろの隊は<b>作らない</b>（画面にも出ない・組めない）。
    /// </summary>
    public int SquadCap { get; init; } = Map11.Squads.Length;

    public int LaneCount => Lanes.Length;
    public int TotalNodes => Lanes.Sum(l => l.Count);

    /// <summary>経路の名前の頭の 1 文字（敵の拠点の札「敵の拠点（北）」に使う）。</summary>
    public string LaneMark(int lane) => LaneNames[lane][..1];

    /// <summary>
    /// 1-1（第439期まで）。<b>中身は `Map11` の static を指すだけ</b>——
    /// 湧く経路は北と南を交互（<c>SpawnCount % 2</c>）で、第176期の規則そのもの。
    /// </summary>
    public static MapStage Stage11 { get; } = new()
    {
        Id = "1-1",
        Title = "検証用マップ 1-1",
        Board = MapGraph.Build11,
        Lanes = Map11.Roads,
        LaneNames = Map11.RoadNames,
        Spawn = road => (Map11.SpawnEnemy(road), Map11.SpawnName(road), Map11.SpawnStage(road)),
        Portal = Map11.AdoptedPortal,
        Time = Map11.AdoptedTime,
    };

    private static Map11.RoadNode Node(int lane, int index, EnemyCatalog.Stage s, int stageIndex)
        => new(lane, index, s.Name, s.Enemy, stageIndex);

    /// <summary>
    /// 1-2 の第440期の姿（§2）。北と南は 1-1 のまま、<b>中は軽い列</b>（第一波 ／ 物見の兵 → 第二波・先遣）。
    /// 湧きは <b>S = 4・R = 3</b>、北 → 中 → 南の順に回す。時間は 1-1 と同じ（K = 1・休む 25%・道中の回復 50%）。
    /// <b>到着も予備隊も無い。</b>第441期からは回帰の基準（`--map12-smoke`）だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H440 { get; } = new()
    {
        Id = "1-2",
        Title = "検証用マップ 1-2",
        Board = MapGraph.Build12,
        Lanes = new IReadOnlyList<Map11.RoadNode>[]
        {
            new[] { Node(0, 0, EnemyCatalog.Vanguards[0], 1), Node(0, 1, EnemyCatalog.Stages[2], 2) },
            new[] { Node(1, 0, EnemyCatalog.Stages[0], 0),    Node(1, 1, EnemyCatalog.Vanguards[0], 1) },
            new[] { Node(2, 0, EnemyCatalog.Vanguards[1], 3), Node(2, 1, EnemyCatalog.Stages[4], 4) },
        },
        LaneNames = MapGraph.Lanes12,
        Spawn = lane => lane switch
        {
            0 => (EnemyCatalog.Vanguards[0].Enemy, EnemyCatalog.Vanguards[0].Name, 1),
            1 => (EnemyCatalog.Stages[0].Enemy, EnemyCatalog.Stages[0].Name, 0),
            _ => (EnemyCatalog.Vanguards[1].Enemy, EnemyCatalog.Vanguards[1].Name, 3),
        },
        Portal = PortalRule.Every(4, warpEvery: 3),
        Time = Map11.AdoptedTime,
    };

    /// <summary>
    /// 1-2 の第441期の姿。盤と敵の 6 部隊は第440期のまま（<see cref="Stage12H440"/>）に、「待つと変わるもの」を 3 つ足した:
    /// <b>途中到着</b>（開始 10 枚・2 刻ごとに 1〜2 枚）／ <b>敵の予備隊</b>（第三波 ／ 討伐隊本隊・最初の接敵の後に手薄な経路へ）／
    /// <b>湧き S = 2</b>（R = 3 のまま・制圧の前に湧く）。隊は 3 つ。
    /// 第442期からは回帰の基準（`--map12-change-smoke` の (a)〜(f)）だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H441 { get; } = new()
    {
        Id = Stage12H440.Id,
        Title = Stage12H440.Title,
        Board = Stage12H440.Board,
        Lanes = Stage12H440.Lanes,
        LaneNames = Stage12H440.LaneNames,
        Spawn = Stage12H440.Spawn,
        Portal = PortalRule.Every(2, warpEvery: 3),
        Time = Stage12H440.Time,
        Arrival = ArrivalRule.Of(start: 10, every: 2, minPer: 1, maxPer: 2),
        Reserve = new ReserveRule(true, EnemyCatalog.Stages[2].Enemy, EnemyCatalog.Stages[2].Name, 2),
    };

    /// <summary>
    /// 1-2（第442期・遊ぶ版）。第441期の 3 つ（<see cref="Stage12H441"/>）を<b>試遊で必ず働く形</b>にした:
    /// <b>隊は 2 つ</b>（経路 3 本より少ない・`SquadCap`）／ <b>中の道の先頭（物見）は 1 マス奥</b>から始まり拠点まで 3 刻
    /// （<see cref="MapGraph.Build12F442"/>・最初の駒が届く 2 刻目より前に負けが決まらないように）。
    /// 到着・予備隊・湧きの値は第441期のまま。
    /// </summary>
    public static MapStage Stage12 { get; } = new()
    {
        Id = Stage12H441.Id,
        Title = Stage12H441.Title,
        Board = MapGraph.Build12F442,
        Lanes = Stage12H441.Lanes,
        LaneNames = Stage12H441.LaneNames,
        Spawn = Stage12H441.Spawn,
        Portal = Stage12H441.Portal,
        Time = Stage12H441.Time,
        Arrival = Stage12H441.Arrival,
        Reserve = Stage12H441.Reserve,
        SquadCap = 2,
    };

    /// <summary>選べるステージ（編成画面の選択の並び）。</summary>
    public static IReadOnlyList<MapStage> All { get; } = new[] { Stage11, Stage12 };
}

/// <summary>
/// 途中到着の規則（第441期 §2）。<b><c>On</c> が偽なら 15 枚を最初に全部手札に載せる</b>（第440期まで）。
/// 自分で組む通し（<c>draft</c>）でだけ効く。<b>「既定の3隊で始める」も開始の枚数だけを配る</b>（第442期 §0-2・残りは到着の列のまま。
/// 第441期は残りを全部その場で配り切っていた）。
/// </summary>
/// <param name="Start">開始時に手札へ載る枚数（10）。どの 10 枚かは seed で引く（<see cref="MapDraw.Draw"/>）。</param>
/// <param name="Every">届く間隔（刻）。<c>A</c>。</param>
/// <param name="MinPer">1 回に届く枚数の下限。</param>
/// <param name="MaxPer">同・上限（1〜2 を seed で引く）。</param>
public readonly record struct ArrivalRule(bool On, int Start, int Every, int MinPer, int MaxPer)
{
    public static readonly ArrivalRule Off = new(false, 0, 0, 0, 0);
    public static ArrivalRule Of(int start, int every, int minPer, int maxPer) => new(true, start, every, minPer, maxPer);
}

/// <summary>
/// 敵の予備隊の規則（第441期 §3）。<b><c>On</c> が偽なら予備隊はいない</b>（第440期まで）。
/// 中身は既存の波の写し。<b>出発の引き金</b>（プレイヤーの最初の接敵が決着した刻の終わり）と
/// <b>行き先の選び方</b>（味方のいない経路 → 無ければ生存枚数の合計が最少の経路）は 1 つずつしか無いので、値として持たない
/// （`Map11State.ReserveIfDue`）。敵の拠点が無い設定では置かない。
/// </summary>
public readonly record struct ReserveRule(bool On, Formation? Enemy, string Name, int StageIndex)
{
    public static readonly ReserveRule Off = new(false, null, "", 0);
}
