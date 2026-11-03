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
    /// <summary>
    /// 作戦ターンの上限（第444期 §0-3）。<b>既定は <see cref="Map11.TurnCap"/>（60）</b>——第443期までと同じ。
    /// </summary>
    public int TurnCap { get; init; } = Map11.TurnCap;
    /// <summary>
    /// 上限を「勇者パーティーの到着」として盤に予告し、上限で<b>敗北</b>にするか（第444期 §0-3・骨子 §5 の予告）。
    /// 偽なら上限は第443期までと同じ「時間切れ」（結果は撤退）。勇者そのものは段C で入れる。
    /// </summary>
    public bool HeroAtCap { get; init; }
    /// <summary>
    /// 敵の拠点を制圧したら勝ち（第445期 §0-2・残った敵部隊は退く）。<b>偽なら第176期からの「制圧 ＋ 盤上の敵部隊が 0」</b>。
    /// </summary>
    public bool WinOnCapture { get; init; }
    /// <summary>ゴールドの規則（第445期 §0-1・<b>既定は Off</b>）。</summary>
    public GoldRule Gold { get; init; } = GoldRule.Off;
    /// <summary>
    /// 上限（<see cref="TurnCap"/>）を「n 作戦ターン以内に敵の拠点を制圧」という<b>勝ち条件の文</b>として出し、上限で「時間切れ」の負け（第447期 §0-3）。
    /// 勇者の予告（<see cref="HeroAtCap"/>）の言い換えで、規則は同じ。
    /// </summary>
    public bool TimeLimit { get; init; }

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
    /// 1-2 の第442期の姿。第441期の 3 つ（<see cref="Stage12H441"/>）を<b>試遊で必ず働く形</b>にした:
    /// <b>隊は 2 つ</b>（経路 3 本より少ない・`SquadCap`）／ <b>中の道の先頭（物見）は 1 マス奥</b>から始まり拠点まで 3 刻
    /// （<see cref="MapGraph.Build12F442"/>・最初の駒が届く 2 刻目より前に負けが決まらないように）。
    /// 到着・予備隊・湧きの値は第441期のまま。
    /// 第443期からは回帰の基準（`--map12-change-smoke` の (g)〜(i) と第442期の器具の表）だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H442 { get; } = new()
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

    /// <summary>
    /// 1-2 の第443期の姿。第442期（<see cref="Stage12H442"/>）の判断の仕組み（途中到着・予備隊・中の先頭は 1 マス奥）は残し、
    /// <b>量の釣り合い</b>を直した（第442期の試遊で 2 巡とも陥落）:
    /// <b>隊は 3 つ</b>（開始の 10 枚で組めるのは 2 隊・<b>3 隊目は届いた駒で組む</b>）／ <b>到着は 2 刻ごとに 2 枚</b>（5 枚が刻 6 までに揃う）／
    /// <b>湧き S = 3</b>（R = 3 のまま）／ <b>予備隊は第二波・先遣</b>（4 体・粛）。最初からいる 6 部隊はそのまま。
    /// 第444期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H443 { get; } = new()
    {
        Id = Stage12H442.Id,
        Title = Stage12H442.Title,
        Board = Stage12H442.Board,
        Lanes = Stage12H442.Lanes,
        LaneNames = Stage12H442.LaneNames,
        Spawn = Stage12H442.Spawn,
        Portal = PortalRule.Every(3, warpEvery: 3),
        Time = Stage12H442.Time,
        Arrival = ArrivalRule.Of(start: 10, every: 2, minPer: 2, maxPer: 2),
        Reserve = new ReserveRule(true, EnemyCatalog.Vanguards[0].Enemy, EnemyCatalog.Vanguards[0].Name, 1),
        SquadCap = 3,
    };

    /// <summary>
    /// 1-2 の第444期の姿。<b>攻めに出る理由を 3 つ同時に</b>（第443期の勝ち筋が籠城だった）:
    /// <b>中間拠点</b>（各経路の 3 マス目・取ると組み直す・届いた駒を受け取る・休む・<see cref="MapGraph.Build12F444"/>）／
    /// <b>増援は見返り</b>（時間では届かない・敵部隊を 1 つ抜くと 1 枚・中間拠点を取ると 2 枚）／
    /// <b>刻の上限 14 ＝ 勇者の予告</b>（上限で敗北）。隊 3・S=3・予備隊は第443期のまま。
    /// 第445期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H444 { get; } = new()
    {
        Id = Stage12H443.Id,
        Title = Stage12H443.Title,
        Board = MapGraph.Build12F444,
        Lanes = Stage12H443.Lanes,
        LaneNames = Stage12H443.LaneNames,
        Spawn = Stage12H443.Spawn,
        Portal = Stage12H443.Portal,
        Time = Stage12H443.Time,
        Arrival = ArrivalRule.Rewards(start: 10, onKill: 1, onOutpost: 2),
        Reserve = Stage12H443.Reserve,
        SquadCap = 3,
        TurnCap = 14,
        HeroAtCap = true,
    };

    /// <summary>
    /// 1-2 の第445期の姿。盤（中間拠点つき）・敵・湧き・予備隊は第444期のまま（<see cref="Stage12H444"/>）に:
    /// <b>駒の供給はゴールド</b>（抜くと +6・湧いた部隊は +3・中間拠点を取ると +8・呼ぶ 10・3 隊目の枠 12・売る +3・開始 0・<see cref="GoldRule"/>）／
    /// 見返りで駒が届くのは切った（開始の 10 枚の保証つきの引きだけ残す・<see cref="ArrivalRule.OpeningOnly"/>）／
    /// <b>隊は 2 つで始まり、枠を買うと 3</b> ／ <b>敵の拠点を制圧したら勝ち</b>（<see cref="WinOnCapture"/>）／ 勇者の予告は <b>12</b>。
    /// 第446期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H445 { get; } = new()
    {
        Id = Stage12H444.Id,
        Title = Stage12H444.Title,
        Board = Stage12H444.Board,
        Lanes = Stage12H444.Lanes,
        LaneNames = Stage12H444.LaneNames,
        Spawn = Stage12H444.Spawn,
        Portal = Stage12H444.Portal,
        Time = Stage12H444.Time,
        Arrival = ArrivalRule.OpeningOnly(start: 10),
        Reserve = Stage12H444.Reserve,
        SquadCap = 3,
        TurnCap = 12,
        HeroAtCap = true,
        WinOnCapture = true,
        Gold = GoldRule.Of(),
    };

    /// <summary>
    /// 1-2 の第446期の姿（軽い期）。第445期（<see cref="Stage12H445"/>）の<b>金の値だけ</b>を変えた（<see cref="GoldRule.V446"/>）:
    /// 開始 0 → <b>8</b>・呼ぶ 10 → <b>4</b>・3 隊目の枠 12 → <b>8</b>（買うと<b>その場で候補から 1 枚を無料で選べる</b>）・
    /// <b>リロール 1</b>（新・候補 3 枚を引き直す・回数の上限なし）・売る ＋3 → <b>＋2</b>。稼ぎ（＋6 ／ ＋3 ／ ＋8）は変えない。
    /// 第447期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H446 { get; } = new()
    {
        Id = Stage12H445.Id,
        Title = Stage12H445.Title,
        Board = Stage12H445.Board,
        Lanes = Stage12H445.Lanes,
        LaneNames = Stage12H445.LaneNames,
        Spawn = Stage12H445.Spawn,
        Portal = Stage12H445.Portal,
        Time = Stage12H445.Time,
        Arrival = Stage12H445.Arrival,
        Reserve = Stage12H445.Reserve,
        SquadCap = Stage12H445.SquadCap,
        TurnCap = Stage12H445.TurnCap,
        HeroAtCap = Stage12H445.HeroAtCap,
        WinOnCapture = Stage12H445.WinOnCapture,
        Gold = GoldRule.V446(),
    };

    /// <summary>
    /// 1-2 の第447期の姿。第446期（<see cref="Stage12H446"/>）の金の遊びの仕上げ（<see cref="GoldRule.V447"/>）:
    /// <b>倒れた駒を呼び戻す（6・HP 50%）</b> ／ リロール 1 → <b>2</b> ／ <b>隊の枠は 4 つ</b>（3 隊目 8 → 4 隊目 12・どちらも無料で 1 枚つき）／
    /// 勇者の予告をやめ、<b>「12 作戦ターン以内に敵の拠点を制圧」</b>の勝ち条件の文に（上限で「時間切れ」・<see cref="TimeLimit"/>）。
    /// 第448期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H447 { get; } = new()
    {
        Id = Stage12H446.Id,
        Title = Stage12H446.Title,
        Board = Stage12H446.Board,
        Lanes = Stage12H446.Lanes,
        LaneNames = Stage12H446.LaneNames,
        Spawn = Stage12H446.Spawn,
        Portal = Stage12H446.Portal,
        Time = Stage12H446.Time,
        Arrival = Stage12H446.Arrival,
        Reserve = Stage12H446.Reserve,
        SquadCap = 4,
        TurnCap = Stage12H446.TurnCap,
        TimeLimit = true,
        WinOnCapture = Stage12H446.WinOnCapture,
        Gold = GoldRule.V447(),
    };

    /// <summary>作戦マップの敵部隊（第448期・`EnemyCatalog.MapSquads`）を区画にする。<paramref name="backdrop"/> は戦闘の背景の波（0〜4・表示だけ）。</summary>
    private static Map11.RoadNode Band(int lane, int index, string id, int backdrop)
    {
        EnemyCatalog.MapSquad m = EnemyCatalog.MapSquadOf(id);
        return new(lane, index, m.Name, m.Enemy, backdrop);
    }

    /// <summary>
    /// 1-2（第448期・遊ぶ版）。<b>道中を帯 A（雑兵の部隊・`EnemyCatalog.MapSquads`）に置き直した</b>——「第2〜第4の波ばかりで制限のある戦いばかり」（ポン）を、
    /// 抜いて爽快な側へ。<b>封じる敵（波ルール持ち）は北の中間拠点の 1 部隊だけ</b>（第二波・先遣・粛）。第三波・第五波・軛は 1-2 から外した（<see cref="Stage12H447"/> に残る）。
    /// 盤・金・期限・隊の枠は第447期のまま。中と南の中間拠点（騎士の小隊・傭兵崩れ）は<b>仮</b>（第449期に帯 B と入れ替える）。
    /// </summary>
    public static MapStage Stage12 { get; } = new()
    {
        Id = Stage12H447.Id,
        Title = Stage12H447.Title,
        Board = Stage12H447.Board,
        Lanes = new IReadOnlyList<Map11.RoadNode>[]
        {
            new[] { Band(0, 0, "band_archers", 2), Node(0, 1, EnemyCatalog.Vanguards[0], 1) },
            new[] { Node(1, 0, EnemyCatalog.Stages[0], 0), Band(1, 1, "band_knights", 1) },
            new[] { Band(2, 0, "band_shields", 3), Band(2, 1, "band_sellswords", 2) },
        },
        LaneNames = Stage12H447.LaneNames,
        Spawn = lane => lane switch
        {
            0 => (EnemyCatalog.MapSquadOf("band_levy").Enemy, EnemyCatalog.MapSquadOf("band_levy").Name, 0),
            1 => (EnemyCatalog.Stages[0].Enemy, EnemyCatalog.Stages[0].Name, 0),
            _ => (EnemyCatalog.MapSquadOf("band_sellswords").Enemy, EnemyCatalog.MapSquadOf("band_sellswords").Name, 2),
        },
        Portal = Stage12H447.Portal,
        Time = Stage12H447.Time,
        Arrival = Stage12H447.Arrival,
        Reserve = new ReserveRule(true, EnemyCatalog.MapSquadOf("band_knights").Enemy, EnemyCatalog.MapSquadOf("band_knights").Name, 1),
        SquadCap = Stage12H447.SquadCap,
        TurnCap = Stage12H447.TurnCap,
        TimeLimit = Stage12H447.TimeLimit,
        WinOnCapture = Stage12H447.WinOnCapture,
        Gold = Stage12H447.Gold,
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
/// <param name="OnKill">第444期 §0-2: 敵部隊を 1 つ抜くと届く枚数（道の上の勝ちも迎撃の勝ちも）。0 なら見返りで届かない。</param>
/// <param name="OnOutpost">第444期 §0-2: 中間拠点を取ると届く枚数。</param>
public readonly record struct ArrivalRule(bool On, int Start, int Every, int MinPer, int MaxPer, int OnKill = 0, int OnOutpost = 0)
{
    public static readonly ArrivalRule Off = new(false, 0, 0, 0, 0);
    public static ArrivalRule Of(int start, int every, int minPer, int maxPer) => new(true, start, every, minPer, maxPer);
    /// <summary>第444期: <b>時間では届かない</b>（<c>Every = 0</c>）。見返りでだけ届く。届く順は seed で決めた列のまま。</summary>
    public static ArrivalRule Rewards(int start, int onKill, int onOutpost) => new(true, start, 0, 1, 1, onKill, onOutpost);
    /// <summary>
    /// 第445期: <b>開始の手札（保証つきの引き）だけ</b>。残りは届かない（列は捨てる——ゴールドで呼ぶ候補に戻る）。
    /// 指示書は `ArrivalRule.Off` だが、Off は開始の 10 枚の引きも切る（手札が 15 枚になる）ので、引きだけ残す値にした。
    /// </summary>
    public static ArrivalRule OpeningOnly(int start) => new(true, start, 0, 1, 1, 0, 0);
    /// <summary>この規則で駒が後から届くか（時間・見返りのどちらか）。偽なら開始の引きの残りは捨てる。</summary>
    public bool Delivers => Every > 0 || OnKill > 0 || OnOutpost > 0;
}

/// <summary>
/// ゴールドの規則（第445期 §0-1・<b>小さく作る</b>——店・値段差・リロール・持ち越しは無い）。<b><c>On</c> が偽なら金は無い</b>（第444期まで）。
/// </summary>
/// <param name="Start">開始の金。</param>
/// <param name="Kill">敵部隊を 1 つ抜く（最初からいる部隊・予備隊）。</param>
/// <param name="SpawnKill">湧いた部隊を抜く。</param>
/// <param name="Outpost">中間拠点を取る。</param>
/// <param name="Recruit">呼ぶ（候補から 1 枚）。値段は全部同じ。</param>
/// <param name="Slot">3 隊目の枠を開ける。</param>
/// <param name="Sell">控えの駒を手放す（盤上の隊からは売れない）。</param>
/// <param name="Offers">候補の枚数。</param>
/// <param name="StartSquads">最初に使える隊の数（枠を買うと 1 つ増える・上限は <see cref="MapStage.SquadCap"/>）。</param>
/// <param name="Reroll">候補を引き直す値段（第446期）。<b>0 ならリロールは無い</b>（第445期）。</param>
/// <param name="SlotFreePick">枠を買うと、その場で候補から 1 枚を無料で選べる（第446期）。</param>
/// <param name="SlotStep">枠を 1 つ買うごとに次の枠が上がる額（第447期: 3 隊目 8 → 4 隊目 12 なら 4）。0 なら値は同じ。</param>
/// <param name="Revive">倒れた駒を呼び戻す値段（第447期）。<b>0 なら呼び戻しは無く、倒れた駒の扱いは第446期まで</b>。</param>
/// <param name="RevivePercent">呼び戻した駒の HP（最大 HP に対する割合）。</param>
public readonly record struct GoldRule(bool On, int Start, int Kill, int SpawnKill, int Outpost, int Recruit, int Slot, int Sell,
                                       int Offers, int StartSquads, int Reroll = 0, bool SlotFreePick = false,
                                       int SlotStep = 0, int Revive = 0, int RevivePercent = 50)
{
    public static readonly GoldRule Off = new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    /// <summary>
    /// 第446期の値（仮・ポンが遊んで決める）: 開始 8・抜く ＋6・湧き ＋3・中間拠点 ＋8・呼ぶ 4・枠 8（無料で 1 枚つき）・売る ＋2・リロール 1・候補 3・隊 2 で始まる。
    /// </summary>
    public static GoldRule V446(int recruit = 4, int slot = 8)
        => new(true, 8, 6, 3, 8, recruit, slot, 2, 3, 2, Reroll: 1, SlotFreePick: true);
    /// <summary>
    /// 第447期の値（仮）: 第446期に リロール 2・枠の逓増 ＋4（3 隊目 8 → 4 隊目 12）・呼び戻す 6（HP 50%）を足した。
    /// </summary>
    public static GoldRule V447()
        => new(true, 8, 6, 3, 8, 4, 8, 2, 3, 2, Reroll: 2, SlotFreePick: true, SlotStep: 4, Revive: 6, RevivePercent: 50);
    /// <summary>第445期の仮の値（ポンが遊んで決める）: 開始 0・抜く +6・湧き +3・中間拠点 +8・呼ぶ 10・枠 12・売る +3・候補 3・隊 2 で始まる。</summary>
    public static GoldRule Of(int start = 0, int kill = 6, int spawnKill = 3, int outpost = 8, int recruit = 10, int slot = 12,
                              int sell = 3, int offers = 3, int startSquads = 2)
        => new(true, start, kill, spawnKill, outpost, recruit, slot, sell, offers, startSquads);
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
