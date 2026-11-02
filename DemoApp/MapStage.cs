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
    /// 1-2（第440期 §2）。北と南は 1-1 のまま、<b>中は軽い列</b>（第一波 ／ 物見の兵 → 第二波・先遣）。
    /// 湧きは <b>S = 4・R = 3</b>（第177期の S = 8 は制圧前に効いていなかった）で、北 → 中 → 南の順に回す。
    /// 時間は 1-1 と同じ（K = 1・休む 25%・道中の回復 50%）。
    /// </summary>
    public static MapStage Stage12 { get; } = new()
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

    /// <summary>選べるステージ（編成画面の選択の並び）。</summary>
    public static IReadOnlyList<MapStage> All { get; } = new[] { Stage11, Stage12 };
}
