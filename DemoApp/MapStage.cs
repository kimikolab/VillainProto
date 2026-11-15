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
    /// <summary>
    /// 候補の 1 枚目を「いま盤上・手札にいる駒と同じ <c>Presets.Compare</c> の行に載る駒」から引く（第449期 §0-5・無ければふつうの引き）。
    /// 残りの候補はふつう。リロール・呼んだ後の補充でも 1 枚目は同じ規則。<b>噛むかどうかは画面に出さない。</b>偽なら第448期まで。
    /// </summary>
    public bool MatchOffer { get; init; }
    /// <summary>
    /// 本陣の門（第451期 §0-1）。<b>中間拠点をこの数以上味方のものにするまで、敵の拠点のマスへ入れない</b>（本陣の守りと戦えない・制圧できない）。
    /// 取り返されて数を割れば門は閉じる（中にいる隊はそのまま・新しく入れないだけ）。湧き・予備隊・敵の前進は門と無関係。<b>0 なら門は無い</b>（第450期まで）。
    /// </summary>
    public int PortalGate { get; init; }
    /// <summary>作戦マップのレリック（第452期 §1・<b>既定は Off</b>）。</summary>
    public RelicRule Relic { get; init; } = RelicRule.Off;
    /// <summary>作戦マップの勇者（第454期 §2・<b>既定は Off</b>）。</summary>
    public HeroRule Hero { get; init; } = HeroRule.Off;

    /// <summary>本陣の連戦（第457期・<see cref="SiegeRule"/>）。既定は無し（第456期まで）。</summary>
    public SiegeRule Siege { get; init; } = SiegeRule.Off;

    /// <summary>
    /// 盤の見え方の倍率（第458期・<b>画面だけが読む</b>——カメラの引きと盤の文字の大きさ）。既定 1（1-1 ／ 1-2 は第457期のまま）。
    /// 1-3 は盤が横に 1.5 倍長い（縦も 1.25 倍）ので 1.35 倍引く（文字の縮尺は第177期の作法＝引いた距離の比で大きくする）。
    /// </summary>
    public float ViewScale { get; init; } = 1f;

    /// <summary>
    /// 横の辺の穴の直し（第460期 §B・第459期 報告 §5-1）。真なら<b>隊のいるマスに敵がいれば当たる</b>（先頭の敵かどうかを問わない・`Map11State.FoeFacing`）。
    /// 偽なら第459期まで（経路の「先頭の敵」が同じマスにいるときだけ当たる——横の辺で先頭でない敵のいるマスへ入ると素通りする）。
    /// 遊ぶ 1-2 ／ 1-3 だけ真（1-1 に横の辺は無い）。
    /// </summary>
    public bool CrossFacing { get; init; }

    /// <summary>
    /// 湧いた部隊は、待ち構える敵（<see cref="Map11.RoadNode.Hold"/>）の脇を抜けて拠点へ歩く（第476期・1 章の 1-1 だけ）。
    /// 偽なら第458期からの規則（動かない区画の奥で詰まる・1-3 の大群）。隊は今までどおり、敵のいるマスを素通りできない。
    /// </summary>
    public bool SpawnsPassHolds { get; init; }

    /// <summary>
    /// 湧く部隊は<b>味方の隊のいない経路</b>へ向かう（第476期の測りの口・予備隊と同じ選び方 `Map11State.ReserveTarget`）。偽なら経路を順に回す（今まで・遊ぶ盤はどれも偽）。
    /// </summary>
    public bool SpawnToThinLane { get; init; }

    /// <summary>
    /// 予備隊（と <see cref="SpawnToThinLane"/> の湧き）が「隊のいない経路」を探す順（第477期・1 章の 1-2 は 中 → 北 → 南）。null なら番号の順（今まで）。
    /// </summary>
    public IReadOnlyList<int>? ReserveOrder { get; init; }

    /// <summary>湧く部隊の経路（第477期・1 章の 1-2 は中の街道だけ）。-1 なら経路を順に回す（今まで）。</summary>
    public int SpawnLane { get; init; } = -1;

    /// <summary>
    /// 中間拠点ごとの見返り（第477期・<b>経路の番号の順</b>——その経路の中間拠点を取ったときの金と、初めて取ったときにレリックの候補が出るか）。
    /// null なら今まで（どこも <see cref="GoldRule.Outpost"/>・どの中間拠点も初めて取ればレリック）。
    /// </summary>
    public IReadOnlyList<OutpostReward>? OutpostRewards { get; init; }

    /// <summary>
    /// 本陣の連戦で引く先を<b>その経路の取っている中間拠点</b>にする（第477期・1 章の 1-2 の新しい盤だけ——経路の奥のマスが中間拠点でない盤）。
    /// 取っていなければ今までどおり経路の奥のマス。偽なら今まで（経路の奥のマス）。
    /// </summary>
    public bool SiegeRetreatToOutpost { get; init; }

    /// <summary>拠点を空にして出ようとしたとき、画面が確かめの 1 行を出す盤か（第476期・1 章の 1-1 だけ・<b>表示だけ</b>——止めはしない）。</summary>
    public bool WarnEmptyHome { get; init; }

    /// <summary>ステージの頭の札に足す行（第476期・勝ち方と道の違い・<b>表示だけ</b>）。空なら足さない。</summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 敵の数値の倍率（第462期・1 章だけ・<see cref="ChapterRule.V462"/>）。<b>盤の敵はすでに掛けた後の値</b>（<see cref="Scaled"/> が経路・湧き・予備隊・勇者の定義を写して掛ける）
    /// ——ここは表示（ステージの頭の札・上の帯）と、勇者の固定回復を掛ける口（`Map11State` が勇者の駒を作るとき・<see cref="MapFoeScale.MendHero"/>）だけが読む。
    /// 単発のステージは <see cref="MapFoeScale.None"/>。
    /// </summary>
    public MapFoeScale FoeScale { get; init; } = MapFoeScale.None;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(MapStage, MapFoeScale), MapStage> _scaled = new();

    /// <summary>
    /// 敵の数値に倍率を掛けた写し（第462期）。<b>同じステージ・同じ倍率には同じインスタンスを返す</b>（1 章の通しが `ReferenceEquals(State.Stage, Chapter1.CurrentStage)` で続きを見分けるため）。
    /// 掛けるのは<b>敵の駒の最大HP と攻撃</b>（四捨五入）——経路の区画（道の部隊・中間拠点・大群・本陣の守り）・湧き・予備隊・勇者。速さ・札・金は掛けない。
    /// 倍率が 1.0 ならこのステージそのもの。
    /// </summary>
    public MapStage Scaled(MapFoeScale sc) => !sc.Active ? this : _scaled.GetOrAdd((this, sc), static k => Copy(k.Item1, scale: k.Item2));

    /// <summary>
    /// ☆の規則（第463期・1 章だけ・<see cref="ChapterRule.V463"/>）。<b>偽（<see cref="StarRule.Off"/>）なら☆は無い</b>——単発のステージと第462期までの 1 章。
    /// 読むのは `Map11State`（手札の駒を☆1 で作る・呼ぶ・重ねる・売る・持ち越す）と画面の札だけ。
    /// </summary>
    public StarRule Stars { get; init; } = StarRule.Off;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(MapStage, StarRule), MapStage> _starred = new();

    /// <summary>☆の規則を載せた写し（第463期・<see cref="Scaled"/> と同じく、同じステージ・同じ規則には同じインスタンス）。☆の無い規則ならこのステージそのもの。</summary>
    public MapStage Starred(StarRule rule) => !rule.On ? this : _starred.GetOrAdd((this, rule), static k => Copy(k.Item1, stars: k.Item2));

    /// <summary>
    /// 開始の配りの規則（第464期・1 章の 1-1 だけ・<see cref="ChapterRule.V464"/>）。<b>偽（<see cref="DealRule.Off"/>）なら第441期の引き</b>。読むのは `Map11State` の開始の手札だけ。
    /// </summary>
    public DealRule Deal { get; init; } = DealRule.Off;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(MapStage, DealRule), MapStage> _dealt = new();

    /// <summary>配りの規則を載せた写し（第464期・<see cref="Scaled"/> と同じく、同じステージ・同じ規則には同じインスタンス）。保証の無い規則ならこのステージそのもの。</summary>
    public MapStage Dealt(DealRule rule) => !rule.On ? this : _dealt.GetOrAdd((this, rule), static k => Copy(k.Item1, deal: k.Item2));

    /// <summary>
    /// 軸への投資の規則（第467期・1 章だけ・<see cref="ChapterRule.V467"/>）。<b>偽（<see cref="InvestRule.Off"/>）なら投資は無い</b>——呼ぶ候補は今の引き方のまま（乱数も引かない）。
    /// 読むのは `Map11State`（候補の寄せ・投資の記録）と画面の札だけ。
    /// </summary>
    public InvestRule Invest { get; init; } = InvestRule.Off;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(MapStage, InvestRule), MapStage> _invested = new();

    /// <summary>投資の規則を載せた写し（第467期・<see cref="Scaled"/> と同じく、同じステージ・同じ規則には同じインスタンス）。投資の無い規則ならこのステージそのもの。</summary>
    public MapStage Invested(InvestRule rule) => !rule.On ? this : _invested.GetOrAdd((this, rule), static k => Copy(k.Item1, invest: k.Item2));

    /// <summary>
    /// 体験版の加入の表（第469期・1 章だけ・<see cref="ChapterRule.V469"/>）。<b>偽（<see cref="RosterRule.Off"/>）なら表は無い</b>——始めの手札は配りの規則のまま、呼ぶ候補は 52 枚から。
    /// 読むのは `Map11State`（始めの手札・呼ぶ候補の母数・「呼べる駒 あと n 体」）と `Chapter1`（投資の軸の札）と画面の札だけ。
    /// </summary>
    public RosterRule Roster { get; init; } = RosterRule.Off;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(MapStage, RosterRule), MapStage> _rostered = new();

    /// <summary>加入の表を載せた写し（第469期・<see cref="Scaled"/> と同じく、同じステージ・同じ規則には同じインスタンス）。表の無い規則ならこのステージそのもの。</summary>
    public MapStage Rostered(RosterRule rule) => !rule.On ? this : _rostered.GetOrAdd((this, rule), static k => Copy(k.Item1, roster: k.Item2));

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
    /// 1-2 の第448期の姿。<b>道中を帯 A（雑兵の部隊・`EnemyCatalog.MapSquads`）に置き直した</b>——「第2〜第4の波ばかりで制限のある戦いばかり」（ポン）を、
    /// 抜いて爽快な側へ。<b>封じる敵（波ルール持ち）は北の中間拠点の 1 部隊だけ</b>（第二波・先遣・粛）。第三波・第五波・軛は 1-2 から外した（<see cref="Stage12H447"/> に残る）。
    /// 盤・金・期限・隊の枠は第447期のまま。中と南の中間拠点（騎士の小隊・傭兵崩れ）は<b>仮</b>（第449期に帯 B と入れ替えた）。
    /// 第449期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H448 { get; } = new()
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

    /// <summary>
    /// 敵の拠点に立つ動かない守備隊（第449期 §0-3）。<b>経路 <paramref name="lane"/> の区画の最後</b>に置き、位置は敵の拠点のマス（<see cref="Map11.RoadNode.Stationary"/>）。
    /// 経路は表示と「その道の敵」の数え方にしか効かない（制圧の判定 <c>PortalFoe</c> は道をまたいで数える）。
    /// </summary>
    private static Map11.RoadNode Garrison(int lane, int index, string id, int backdrop)
        => Band(lane, index, id, backdrop) with { Stationary = true };

    /// <summary>
    /// 1-2（第449期・遊ぶ版）。帯 B（燃焼・毒の餌になる敵）と本陣の守備隊（ポン案）:
    /// 中の中間拠点を<b>火の魔術師の部隊</b>、南の中間拠点を<b>毒霧の部隊</b>に替え、敵の拠点に<b>本陣の守り（動かない・抜かないと制圧できない・抜くと ＋10）</b>を置いた。
    /// 候補の 1 枚目は手持ちと噛む駒（<see cref="MatchOffer"/>）。北・湧き・予備隊・盤・金（守備隊の ＋10 のほか）・期限・隊の枠は第448期（<see cref="Stage12H448"/>）のまま。
    /// 第450期からは回帰の基準だけが使う——火の魔術師の部隊は第449期の版（<see cref="EnemyCatalog.BandFireH449"/>・自分の部隊の隣にも燃え移る）。遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H449 { get; } = new()
    {
        Id = Stage12H448.Id,
        Title = Stage12H448.Title,
        Board = Stage12H448.Board,
        Lanes = new IReadOnlyList<Map11.RoadNode>[]
        {
            new[] { Band(0, 0, "band_archers", 2), Node(0, 1, EnemyCatalog.Vanguards[0], 1) },
            new[] { Node(1, 0, EnemyCatalog.Stages[0], 0),
                    new Map11.RoadNode(1, 1, EnemyCatalog.BandFireH449.Name, EnemyCatalog.BandFireH449.Enemy, 3), Garrison(1, 2, "band_garrison", 4) },
            new[] { Band(2, 0, "band_shields", 3), Band(2, 1, "band_miasma", 2) },
        },
        LaneNames = Stage12H448.LaneNames,
        Spawn = Stage12H448.Spawn,
        Portal = Stage12H448.Portal,
        Time = Stage12H448.Time,
        Arrival = Stage12H448.Arrival,
        Reserve = Stage12H448.Reserve,
        SquadCap = Stage12H448.SquadCap,
        TurnCap = Stage12H448.TurnCap,
        TimeLimit = Stage12H448.TimeLimit,
        WinOnCapture = Stage12H448.WinOnCapture,
        Gold = GoldRule.V449(),
        MatchOffer = true,
    };

    /// <summary>
    /// 1-2 の第450期の姿。第449期（<see cref="Stage12H449"/>）から<b>火の魔術師の部隊だけ</b>を替えた——火の魔術師・火矢の射手の火は
    /// 敵の火の粉（`CinderFoe`・当てた相手全員に点く・<b>自分の部隊の隣へは燃え移らない</b>）。盤・部隊の並び・金・候補の規則は第449期のまま。
    /// 第451期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H450 { get; } = new()
    {
        Id = Stage12H449.Id,
        Title = Stage12H449.Title,
        Board = Stage12H449.Board,
        Lanes = new IReadOnlyList<Map11.RoadNode>[]
        {
            Stage12H449.Lanes[0],
            new[] { Stage12H449.Lanes[1][0], Band(1, 1, "band_fire", 3), Stage12H449.Lanes[1][2] },
            Stage12H449.Lanes[2],
        },
        LaneNames = Stage12H449.LaneNames,
        Spawn = Stage12H449.Spawn,
        Portal = Stage12H449.Portal,
        Time = Stage12H449.Time,
        Arrival = Stage12H449.Arrival,
        Reserve = Stage12H449.Reserve,
        SquadCap = Stage12H449.SquadCap,
        TurnCap = Stage12H449.TurnCap,
        TimeLimit = Stage12H449.TimeLimit,
        WinOnCapture = Stage12H449.WinOnCapture,
        Gold = Stage12H449.Gold,
        MatchOffer = Stage12H449.MatchOffer,
    };

    /// <summary>
    /// 1-2 の第451期の姿。第450期（<see cref="Stage12H450"/>）に<b>本陣の門</b>（<see cref="PortalGate"/> ＝ 2）を足しただけ——
    /// 中間拠点を 3 つのうち 2 つ取るまで敵の拠点へ入れない。北（粛）・中（物見と火の魔術師の部隊が同じマス）・南（毒霧）のどの 2 つでも、火か毒には必ず当たる。
    /// 第452期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H451 { get; } = new()
    {
        Id = Stage12H450.Id,
        Title = Stage12H450.Title,
        Board = Stage12H450.Board,
        Lanes = Stage12H450.Lanes,
        LaneNames = Stage12H450.LaneNames,
        Spawn = Stage12H450.Spawn,
        Portal = Stage12H450.Portal,
        Time = Stage12H450.Time,
        Arrival = Stage12H450.Arrival,
        Reserve = Stage12H450.Reserve,
        SquadCap = Stage12H450.SquadCap,
        TurnCap = Stage12H450.TurnCap,
        TimeLimit = Stage12H450.TimeLimit,
        WinOnCapture = Stage12H450.WinOnCapture,
        Gold = Stage12H450.Gold,
        MatchOffer = Stage12H450.MatchOffer,
        PortalGate = 2,
    };

    /// <summary>
    /// 1-2 の第452期の姿。第451期（<see cref="Stage12H451"/>）に<b>作戦マップのレリック</b>（<see cref="RelicRule.V452"/>）を足しただけ——
    /// 中間拠点を初めて取った刻に候補 3 枚から 1 枚（選ばないこともできる）。1 マップ限り（持ち越さない）。
    /// 第453期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H452 { get; } = new()
    {
        Id = Stage12H451.Id,
        Title = Stage12H451.Title,
        Board = Stage12H451.Board,
        Lanes = Stage12H451.Lanes,
        LaneNames = Stage12H451.LaneNames,
        Spawn = Stage12H451.Spawn,
        Portal = Stage12H451.Portal,
        Time = Stage12H451.Time,
        Arrival = Stage12H451.Arrival,
        Reserve = Stage12H451.Reserve,
        SquadCap = Stage12H451.SquadCap,
        TurnCap = Stage12H451.TurnCap,
        TimeLimit = Stage12H451.TimeLimit,
        WinOnCapture = Stage12H451.WinOnCapture,
        Gold = Stage12H451.Gold,
        MatchOffer = Stage12H451.MatchOffer,
        PortalGate = Stage12H451.PortalGate,
        Relic = RelicRule.V452,
    };

    /// <summary>
    /// 1-2 の第453期の姿。第452期（<see cref="Stage12H452"/>）のレリックの候補の 1 枚目を<b>両端が手持ちにある札</b>から引く（<see cref="RelicRule.V453"/>）。ほかは第452期のまま。
    /// 第454期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H453 { get; } = new()
    {
        Id = Stage12H452.Id,
        Title = Stage12H452.Title,
        Board = Stage12H452.Board,
        Lanes = Stage12H452.Lanes,
        LaneNames = Stage12H452.LaneNames,
        Spawn = Stage12H452.Spawn,
        Portal = Stage12H452.Portal,
        Time = Stage12H452.Time,
        Arrival = Stage12H452.Arrival,
        Reserve = Stage12H452.Reserve,
        SquadCap = Stage12H452.SquadCap,
        TurnCap = Stage12H452.TurnCap,
        TimeLimit = Stage12H452.TimeLimit,
        WinOnCapture = Stage12H452.WinOnCapture,
        Gold = Stage12H452.Gold,
        MatchOffer = Stage12H452.MatchOffer,
        PortalGate = Stage12H452.PortalGate,
        Relic = RelicRule.V453,
    };

    /// <summary>
    /// 1-2 の第454期の姿。第453期（<see cref="Stage12H453"/>）に<b>勇者</b>（<see cref="HeroRule.V454"/>・門が開いた刻の 2 刻後に予告なしで敵の拠点に現れ、2 刻に 1 マス味方の拠点へ進む）と、
    /// レリックの両端を軸の語だけで見る規則（<see cref="RelicRule.V454"/>）を足した。勝ちは制圧のまま（勇者が着く前に制圧すれば逃げ切り）。
    /// 第455期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H454 { get; } = new()
    {
        Id = Stage12H453.Id,
        Title = Stage12H453.Title,
        Board = Stage12H453.Board,
        Lanes = Stage12H453.Lanes,
        LaneNames = Stage12H453.LaneNames,
        Spawn = Stage12H453.Spawn,
        Portal = Stage12H453.Portal,
        Time = Stage12H453.Time,
        Arrival = Stage12H453.Arrival,
        Reserve = Stage12H453.Reserve,
        SquadCap = Stage12H453.SquadCap,
        TurnCap = Stage12H453.TurnCap,
        TimeLimit = Stage12H453.TimeLimit,
        WinOnCapture = Stage12H453.WinOnCapture,
        Gold = Stage12H453.Gold,
        MatchOffer = Stage12H453.MatchOffer,
        PortalGate = Stage12H453.PortalGate,
        Relic = RelicRule.V454,
        Hero = HeroRule.V454,
    };

    /// <summary>
    /// 1-2 の第455期の姿。第454期（<see cref="Stage12H454"/>）の勇者を<b>本陣を制圧した刻</b>に出す（<see cref="HeroRule.V455"/>・1-3 の形）——
    /// 制圧した本陣の 1 つ手前（経路の奥のマス）に予告なしで現れ、2 刻に 1 マス味方の拠点へ進む。<b>勝ちは制圧 ＋ 勇者を倒す</b>・期限は制圧まで。
    /// 第456期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H455 { get; } = new()
    {
        Id = Stage12H453.Id,
        Title = Stage12H453.Title,
        Board = Stage12H453.Board,
        Lanes = Stage12H453.Lanes,
        LaneNames = Stage12H453.LaneNames,
        Spawn = Stage12H453.Spawn,
        Portal = Stage12H453.Portal,
        Time = Stage12H453.Time,
        Arrival = Stage12H453.Arrival,
        Reserve = Stage12H453.Reserve,
        SquadCap = Stage12H453.SquadCap,
        TurnCap = Stage12H453.TurnCap,
        TimeLimit = Stage12H453.TimeLimit,
        WinOnCapture = Stage12H453.WinOnCapture,
        Gold = Stage12H453.Gold,
        MatchOffer = Stage12H453.MatchOffer,
        PortalGate = Stage12H453.PortalGate,
        Relic = RelicRule.V454,
        Hero = HeroRule.V455,
    };

    /// <summary>
    /// 1-2 の第456期の姿。第455期（<see cref="Stage12H455"/>）の勇者を<b>門番</b>にした（<see cref="HeroRule.V456"/>）——
    /// 門が開いた刻に敵の拠点のマス（本陣の守りと同じマス）に予告なしで立ち、動かない。本陣に入ると守り → 勇者の順に当たり、勇者が立っている間は制圧できない。
    /// <b>勝ちは制圧のまま</b>（第445期と同じ・制圧で勝ち・残りは退いた）・期限 12 のまま・倒せば ＋20。
    /// 第457期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H456 { get; } = new()
    {
        Id = Stage12H455.Id,
        Title = Stage12H455.Title,
        Board = Stage12H455.Board,
        Lanes = Stage12H455.Lanes,
        LaneNames = Stage12H455.LaneNames,
        Spawn = Stage12H455.Spawn,
        Portal = Stage12H455.Portal,
        Time = Stage12H455.Time,
        Arrival = Stage12H455.Arrival,
        Reserve = Stage12H455.Reserve,
        SquadCap = Stage12H455.SquadCap,
        TurnCap = Stage12H455.TurnCap,
        TimeLimit = Stage12H455.TimeLimit,
        WinOnCapture = Stage12H455.WinOnCapture,
        Gold = Stage12H455.Gold,
        MatchOffer = Stage12H455.MatchOffer,
        PortalGate = Stage12H455.PortalGate,
        Relic = Stage12H455.Relic,
        Hero = HeroRule.V456,
    };

    /// <summary>
    /// 1-2（第457期）。第456期（<see cref="Stage12H456"/>）に<b>本陣の連戦</b>（<see cref="SiegeRule.V457"/>）を足した——
    /// 敵の拠点のマスで勝つと、同じ刻のうちに次の相手（守り → 勇者 → 湧き・予備隊）と続けて戦うか、入ってきた経路の中間拠点へ引くかを選ぶ。本陣の戦闘では道中の回復を乗せない。
    /// 第460期からは回帰の基準だけが使う——遊ぶ 1-2 は <see cref="Stage12"/>。
    /// </summary>
    public static MapStage Stage12H459 { get; } = new()
    {
        Id = Stage12H456.Id,
        Title = Stage12H456.Title,
        Board = Stage12H456.Board,
        Lanes = Stage12H456.Lanes,
        LaneNames = Stage12H456.LaneNames,
        Spawn = Stage12H456.Spawn,
        Portal = Stage12H456.Portal,
        Time = Stage12H456.Time,
        Arrival = Stage12H456.Arrival,
        Reserve = Stage12H456.Reserve,
        SquadCap = Stage12H456.SquadCap,
        TurnCap = Stage12H456.TurnCap,
        TimeLimit = Stage12H456.TimeLimit,
        WinOnCapture = Stage12H456.WinOnCapture,
        Gold = Stage12H456.Gold,
        MatchOffer = Stage12H456.MatchOffer,
        PortalGate = Stage12H456.PortalGate,
        Relic = Stage12H456.Relic,
        Hero = Stage12H456.Hero,
        Siege = SiegeRule.V457,
    };

    /// <summary>1-2（第460期・遊ぶ版）。第459期（<see cref="Stage12H459"/>）に横の辺の穴の直し（<see cref="CrossFacing"/>）を載せた。</summary>
    public static MapStage Stage12 { get; } = Copy(Stage12H459, crossFacing: true);

    /// <summary>ステージの写し（第460期・版を足すための道具）。<paramref name="crossFacing"/> を渡したときだけ横の辺の直しを替える。</summary>
    /// 第462期: <paramref name="scale"/> を渡したときは敵の数値に倍率を掛ける（<see cref="Scaled"/> だけが渡す）。
    private static MapStage Copy(MapStage s, bool? crossFacing = null, MapFoeScale? scale = null, StarRule? stars = null, DealRule? deal = null, InvestRule? invest = null,
                                 RosterRule? roster = null) => new()
    {
        Roster = roster ?? s.Roster,
        Invest = invest ?? s.Invest,
        Stars = stars ?? s.Stars,
        Deal = deal ?? s.Deal,
        CrossFacing = crossFacing ?? s.CrossFacing,
        FoeScale = scale ?? s.FoeScale,
        Id = s.Id,
        Title = s.Title,
        Board = s.Board,
        Lanes = scale is { } sl ? sl.Lanes(s.Lanes) : s.Lanes,
        LaneNames = s.LaneNames,
        Spawn = scale is { } ss ? ss.Spawn(s.Spawn) : s.Spawn,
        Portal = s.Portal,
        Time = s.Time,
        Arrival = s.Arrival,
        Reserve = scale is { } sr && s.Reserve.Enemy is { } re ? s.Reserve with { Enemy = sr.Foe(re) } : s.Reserve,
        SquadCap = s.SquadCap,
        TurnCap = s.TurnCap,
        HeroAtCap = s.HeroAtCap,
        TimeLimit = s.TimeLimit,
        WinOnCapture = s.WinOnCapture,
        Gold = s.Gold,
        MatchOffer = s.MatchOffer,
        PortalGate = s.PortalGate,
        Relic = s.Relic,
        Hero = scale is { } sh && s.Hero.Unit is { } hu ? s.Hero with { Unit = sh.HeroDef(hu) } : s.Hero,
        Siege = s.Siege,
        ViewScale = s.ViewScale,
        SpawnsPassHolds = s.SpawnsPassHolds,
        SpawnToThinLane = s.SpawnToThinLane,
        WarnEmptyHome = s.WarnEmptyHome,
        Notes = s.Notes,
        ReserveOrder = s.ReserveOrder,
        SpawnLane = s.SpawnLane,
        OutpostRewards = s.OutpostRewards,
        SiegeRetreatToOutpost = s.SiegeRetreatToOutpost,
    };

    /// <summary>大群の 1 部隊（第458期・1-3 の C3・動かない・<see cref="Map11.RoadNode.Hold"/>）。</summary>
    private static Map11.RoadNode Horde(int lane, int index, string id, int backdrop)
        => Band(lane, index, id, backdrop) with { Hold = true };

    /// <summary>
    /// 1-3（第458期・遊ぶ版・指示書 design/PHASE458_STAGE13_SPEC.md・構想 claude/STAGE13_PLAN.md）。<b>この期は盤と敵だけ</b>——勇者は 1-2 と同じ門番（<see cref="HeroRule.V456"/>）。
    /// 盤は <see cref="MapGraph.Build13"/>（拠点 → 分かれ道 → 3 経路・中間拠点どうしの横の辺）。
    /// 北: 弓隊 → 北の砦に<b>雷杖の部隊</b>（帯 B・新） ／ 中: 物見 → 野営地に火の魔術師の部隊 → C3 に<b>大群（徴募兵 × 3・重なって動かない）</b> ／
    /// 南: 盾の列 → 南の関所に<b>槍兵の部隊</b>（帯 B・新） ／ 本陣: <b>本陣の守り（重）</b>（抜くと ＋12）。
    /// 湧きは S = 3 で 毒霧の部隊（北）→ 徴募兵（中）→ 傭兵崩れ（南）の順・予備隊は騎士の小隊。期限 <b>18</b>。
    /// 連戦の窓は本陣と<b>敵が重なったマス</b>（<see cref="SiegeRule.V458"/>）。金は 1-2 と同じ（大群は 1 部隊 ＋3・本陣の守り（重）は ＋12）。
    /// 経路・隊の枠・候補・レリック・門（中間拠点 2 つ）は 1-2（<see cref="Stage12"/>）のまま。
    /// 第459期からは回帰の基準だけが使う——遊ぶ 1-3 は <see cref="Stage13"/>（追ってくる勇者）。
    /// </summary>
    public static MapStage Stage13H458 { get; } = new()
    {
        Id = "1-3",
        Title = "検証用マップ 1-3",
        Board = MapGraph.Build13,
        Lanes = new IReadOnlyList<Map11.RoadNode>[]
        {
            new[] { Band(0, 0, "band_archers", 2), Band(0, 1, "band_shock", 3) },
            new[]
            {
                Node(1, 0, EnemyCatalog.Stages[0], 0), Band(1, 1, "band_fire", 3),
                Horde(1, 2, "band_levy", 0), Horde(1, 3, "band_levy", 0), Horde(1, 4, "band_levy", 0),
                Garrison(1, 5, "band_garrison_heavy", 4),
            },
            new[] { Band(2, 0, "band_shields", 3), Band(2, 1, "band_lance", 2) },
        },
        LaneNames = MapGraph.Lanes13,
        Spawn = lane => lane switch
        {
            0 => (EnemyCatalog.MapSquadOf("band_miasma").Enemy, EnemyCatalog.MapSquadOf("band_miasma").Name, 2),
            1 => (EnemyCatalog.MapSquadOf("band_levy").Enemy, EnemyCatalog.MapSquadOf("band_levy").Name, 0),
            _ => (EnemyCatalog.MapSquadOf("band_sellswords").Enemy, EnemyCatalog.MapSquadOf("band_sellswords").Name, 2),
        },
        Portal = Stage12.Portal,
        Time = Stage12.Time,
        Arrival = Stage12.Arrival,
        Reserve = Stage12.Reserve,
        SquadCap = Stage12.SquadCap,
        TurnCap = 18,
        TimeLimit = Stage12.TimeLimit,
        WinOnCapture = Stage12.WinOnCapture,
        Gold = GoldRule.V458(),
        MatchOffer = Stage12.MatchOffer,
        PortalGate = Stage12.PortalGate,
        Relic = Stage12.Relic,
        Hero = Stage12.Hero,
        Siege = SiegeRule.V458,
        ViewScale = 1.35f,
    };

    /// <summary>
    /// 1-3（第459期・遊ぶ版・指示書 design/PHASE459_HERO_CHASE_SPEC.md）。第458期（<see cref="Stage13H458"/>）の勇者を<b>追ってくる勇者</b>（<see cref="HeroRule.V459"/>）に替えた——
    /// 門が開いた刻に拠点から 3 歩のマスに現れ、毎刻 1 歩で拠点へ。拠点が空なら拠点が落ちる（負けではない）・勝ちは本陣の制圧（勇者は倒さなくてよい）。
    /// 盤・敵・湧き・金・連戦は第458期のまま。
    /// 第460期からは回帰の基準だけが使う——遊ぶ 1-3 は <see cref="Stage13"/>。
    /// </summary>
    public static MapStage Stage13H459 { get; } = new()
    {
        Id = Stage13H458.Id,
        Title = Stage13H458.Title,
        Board = Stage13H458.Board,
        Lanes = Stage13H458.Lanes,
        LaneNames = Stage13H458.LaneNames,
        Spawn = Stage13H458.Spawn,
        Portal = Stage13H458.Portal,
        Time = Stage13H458.Time,
        Arrival = Stage13H458.Arrival,
        Reserve = Stage13H458.Reserve,
        SquadCap = Stage13H458.SquadCap,
        TurnCap = Stage13H458.TurnCap,
        TimeLimit = Stage13H458.TimeLimit,
        WinOnCapture = Stage13H458.WinOnCapture,
        Gold = Stage13H458.Gold,
        MatchOffer = Stage13H458.MatchOffer,
        PortalGate = Stage13H458.PortalGate,
        Relic = Stage13H458.Relic,
        Hero = HeroRule.V459,
        Siege = Stage13H458.Siege,
        ViewScale = Stage13H458.ViewScale,
    };

    /// <summary>1-3（第460期・遊ぶ版）。第459期（<see cref="Stage13H459"/>）に横の辺の穴の直し（<see cref="CrossFacing"/>）を載せた。</summary>
    public static MapStage Stage13 { get; } = Copy(Stage13H459, crossFacing: true);

    /// <summary>
    /// 1 章の 1-1（第461期 §1-2・持ち越しのある 1 章 `ChapterRule.V461` だけが使う）。盤・敵・湧きは単発の 1-1（<see cref="Stage11"/>）と同じもの。
    /// <b>小さく始める</b>: 手札は開始の保証つきの引きを <b>6 枚</b>（`ArrivalRule.OpeningOnly(6)`・後から届かない）・隊の枠は 1-2 と同じ数（4）で<b>使えるのは 2</b>・
    /// <b>金の規則は 1-2 と同じ値</b>（<see cref="GoldRule.V449"/>・始め 8・中間拠点と勇者の分は 1-1 に無いので起きない）。単発の 1-1 は変えない（手札 15・隊 3・金なし）。
    /// </summary>
    public static MapStage Stage11Ch { get; } = new()
    {
        Id = Stage11.Id,
        Title = Stage11.Title,
        Board = Stage11.Board,
        Lanes = Stage11.Lanes,
        LaneNames = Stage11.LaneNames,
        Spawn = Stage11.Spawn,
        Portal = Stage11.Portal,
        Time = Stage11.Time,
        Arrival = ArrivalRule.OpeningOnly(start: 6),
        SquadCap = Stage12.SquadCap,
        Gold = GoldRule.V449(),
    };

    /// <summary>
    /// 1 章の 1-1（第470期 §1-3・`ChapterRule.V470` から）。<see cref="Stage11Ch"/> の<b>道の先頭だけ</b>を軽い部隊に替えた:
    /// 北の先頭 第二波・先遣 → <b>第一波 ／ 物見の兵</b>（`EnemyCatalog.Stages[0]`）・南の先頭 第四波・先遣 → <b>徴募兵</b>（`band_levy`）。
    /// 奥（第三波 ／ 討伐隊本隊・第五波 ／ 異端審問団）・湧き・盤・手札・隊の枠・金は <see cref="Stage11Ch"/> のまま。単発の 1-1 と第461〜469期の 1 章の 1-1 は変えない。
    /// </summary>
    public static MapStage Stage11Ch470 { get; } = new()
    {
        Id = Stage11Ch.Id,
        Title = Stage11Ch.Title,
        Board = Stage11Ch.Board,
        Lanes = new IReadOnlyList<Map11.RoadNode>[]
        {
            new[] { Node(0, 0, EnemyCatalog.Stages[0], 0), Stage11Ch.Lanes[0][1] },
            new[] { Band(1, 0, "band_levy", 0), Stage11Ch.Lanes[1][1] },
        },
        LaneNames = Stage11Ch.LaneNames,
        Spawn = Stage11Ch.Spawn,
        Portal = Stage11Ch.Portal,
        Time = Stage11Ch.Time,
        Arrival = Stage11Ch.Arrival,
        SquadCap = Stage11Ch.SquadCap,
        Gold = Stage11Ch.Gold,
    };

    /// <summary>
    /// 1 章の 1-1（第471期 §1-2・`ChapterRule.V471` から）。<see cref="Stage11Ch470"/> の<b>奥と湧きを雑兵の部隊</b>（`EnemyCatalog.MapSquads`・波ルールなし）に替えた:
    /// 北の奥 第三波／討伐隊本隊 → <b>盾の列</b>・南の奥 第五波／異端審問団 → <b>騎士の小隊</b>・湧き 第二波・先遣 ／ 第四波・先遣 → <b>徴募兵 ／ 傭兵崩れ</b>。
    /// 南の奥は指示書では<b>弓隊</b>だったが、`--foe-weight`（始めの 6 枚・☆1・×0.6）で 5 枚の隊の終わりの HP 60% ／ 3 枚の隊 12／20 と線（70% ／ 15／20）に届かず、
    /// 騎士の小隊（5 枚 84〜91%・3 枚 20／20）に替えた（傭兵崩れも線を通るが湧きと同じ部隊になる・報告 design/PHASE471_STAGE11.md §2）。
    /// 先頭（物見の兵・徴募兵）・盤・手札・隊の枠・金は第470期のまま。<b>波ルール（粛・渇き・軛など）を持つ部隊は 1 つも出ない。</b>
    /// </summary>
    public static MapStage Stage11Ch471 { get; } = new()
    {
        Id = Stage11Ch470.Id,
        Title = Stage11Ch470.Title,
        Board = Stage11Ch470.Board,
        Lanes = new IReadOnlyList<Map11.RoadNode>[]
        {
            new[] { Stage11Ch470.Lanes[0][0], Band(0, 1, "band_shields", 3) },
            new[] { Stage11Ch470.Lanes[1][0], Band(1, 1, "band_knights", 2) },
        },
        LaneNames = Stage11Ch470.LaneNames,
        Spawn = lane => lane == 0
            ? (EnemyCatalog.MapSquadOf("band_levy").Enemy, EnemyCatalog.MapSquadOf("band_levy").Name, 0)
            : (EnemyCatalog.MapSquadOf("band_sellswords").Enemy, EnemyCatalog.MapSquadOf("band_sellswords").Name, 2),
        Portal = Stage11Ch470.Portal,
        Time = Stage11Ch470.Time,
        Arrival = Stage11Ch470.Arrival,
        SquadCap = Stage11Ch470.SquadCap,
        Gold = Stage11Ch470.Gold,
    };

    /// <summary>
    /// 区画 1 つだけに倍率を掛ける口（第474期 §1・「北2 だけに掛ける倍率」）。<b>ステージの倍率（<see cref="FoeScale"/>）とは別に、先に掛かる</b>
    /// ——1 章の ×0.6 はこの後に掛かるので、盤の上の値は 素の値 × ここの倍率 × 0.6（どちらも四捨五入）。100 ／ 100 ならそのまま。
    /// <b>最大HP と攻撃を別々に掛けられる</b>（「硬い ＝ 手数が要る」は HP だけを上げる）。同じ編成・同じ倍率には同じ写しを返す（<see cref="Scaled"/> が編成の同一性で写しを引くため）。
    /// </summary>
    private static Map11.RoadNode Hard(Map11.RoadNode node, int hpPercent, int atkPercent)
        => hpPercent == 100 && atkPercent == 100 ? node : node with
        {
            Enemy = _hard.GetOrAdd((node.Enemy, hpPercent, atkPercent), static k =>
            {
                Formation c = k.Item1.Clone();
                foreach ((int slot, UnitDef d) in k.Item1.Occupied())
                    c[slot] = d.WithStats(MapFoeScale.Mul(d.MaxHp, k.Item2), MapFoeScale.Mul(d.Attack, k.Item3));
                return c;
            }),
        };

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Formation, int, int), Formation> _hard = new();

    /// <summary>第474期の 1 章の 1-1 で、北の 2 マス目（峠の関）の部隊とその区画だけの倍率（最大HP ／ 攻撃・%）。<see cref="Stage11Ch474"/> と門・重さの調べが読む。</summary>
    internal const string Stage11Ch474GateBand = "band_shields";
    internal const int Stage11Ch474GateHp = 250, Stage11Ch474GateAtk = 200;
    /// <summary>峠の関を抜いたときの金（ポン 2026-11-14・道の部隊を抜くのと同じ +6）。動かない区画の金の口（<see cref="GoldRule.Horde"/>）に載せる。</summary>
    internal const int Stage11Ch474GateGold = 6;

    /// <summary>
    /// 1 章の 1-1（第474期・`ChapterRule.V474` から・指示書 design/PHASE474_STAGE11_ASYM_SPEC.md §1）。<b>盤を非対称にした</b>（<see cref="MapGraph.Build11F474"/>）:
    /// 北の道は 2 マス（物見の兵 → <b>峠の関の硬い部隊</b>）、南の道は 4 マス（徴募兵 → 空 → 騎士の小隊 → 空）。湧き・倍率 ×0.6・手札 6・隊の枠 2・金 8 は第471期のまま。
    /// ポンの決定（2026-11-14）: <b>峠の関は動かない</b>（<see cref="Map11.RoadNode.Hold"/>・拠点へ歩いてこない・抜くまで北の湧きは関の奥で詰まる）・<b>敵の拠点を制圧したら勝ち</b>（<see cref="WinOnCapture"/>・1-2 ／ 1-3 と同じ）・
    /// 関は盾の列の 最大HP 250% ／ 攻撃 200%・抜くと金 +6。物見の兵・徴募兵・騎士の小隊は今までどおり拠点へ歩く。
    /// 単発の 1-1 と第461〜473期の 1 章の 1-1 は変えない。
    /// </summary>
    public static MapStage Stage11Ch474 { get; } = Stage11Ch474With(Stage11Ch474GateBand, Stage11Ch474GateHp, Stage11Ch474GateAtk);

    /// <summary>北の 2 マス目の部隊と倍率を替えた 1 章の 1-1（第474期の重さの調べが候補を並べるための口・遊ぶ盤は <see cref="Stage11Ch474"/>）。</summary>
    /// <paramref name="gateHolds"/>（関が動かない）と <paramref name="winOnCapture"/>（制圧したら勝ち）を偽にすると、最初に測った「関も歩く ／ 全部抜いて勝ち」の盤（通しが比べる相手）。
    internal static MapStage Stage11Ch474With(string gateBand, int gateHp, int gateAtk, bool gateHolds = true, bool winOnCapture = true) => new()
    {
        Id = Stage11Ch471.Id,
        Title = Stage11Ch471.Title,
        Board = MapGraph.Build11F474,
        Lanes = new IReadOnlyList<Map11.RoadNode>[]
        {
            new[] { Stage11Ch471.Lanes[0][0], Hard(Band(0, 1, gateBand, 3), gateHp, gateAtk) with { Hold = gateHolds } },
            new[] { Stage11Ch471.Lanes[1][0], Stage11Ch471.Lanes[1][1] },
        },
        WinOnCapture = winOnCapture,
        LaneNames = Stage11Ch471.LaneNames,
        Spawn = Stage11Ch471.Spawn,
        Portal = Stage11Ch471.Portal,
        Time = Stage11Ch471.Time,
        Arrival = Stage11Ch471.Arrival,
        SquadCap = Stage11Ch471.SquadCap,
        Gold = gateHolds ? Stage11Ch471.Gold with { Horde = Stage11Ch474GateGold } : Stage11Ch471.Gold,
    };

    // ---------------- 第476期: 1-1 に「道を選ぶ理由」（指示書 design/PHASE476_STAGE11_REASON_SPEC.md §2） ----------------

    /// <summary>第476期の 1 章の 1-1 の賞金: 雑兵（物見の兵・徴募兵・騎士の小隊）＋4・峠の関 ＋14・本陣の守り ＋4。</summary>
    internal const int Stage11Ch476FoeGold = 4, Stage11Ch476GateGold = 14, Stage11Ch476GarrisonGold = 4;
    /// <summary>最初の湧きは 6 刻目（5 作戦ターンが終わったとき）。2 回目からは今までの間隔（S = 8）。</summary>
    internal const int Stage11Ch476FirstSpawn = 5;
    /// <summary>本陣の守り（軽い帯・`--foe-weight` で決めた）。</summary>
    internal const string Stage11Ch476GarrisonBand = "band_levy";
    /// <summary>
    /// 湧く部隊（北も南も傭兵崩れ）。第471期は北が徴募兵だったが、`--chapter1-reason-smoke` の重さで「拠点に 1 枚だけ残しても勝つ」（ムド 1 枚 20／20）と出て、
    /// 「3 枚の隊 15／20 以上・1 枚では負ける」の線を通る傭兵崩れに替えた（3 枚 20／20・1 枚は最大でリリ 7／20）。
    /// </summary>
    internal const string Stage11Ch476SpawnBand = "band_sellswords";
    /// <summary>峠の関の名前（札・遭遇の窓・敵部隊の一覧・記録）。中身は盾の列（<see cref="Stage11Ch474GateBand"/>）。</summary>
    internal const string Stage11Ch476GateName = "峠の関";

    /// <summary>
    /// 1 章の 1-1（第476期・`ChapterRule.V476` から）。盤・関の重さ・倍率・手札・隊の枠・始めの金は第474期のまま（<see cref="Stage11Ch474"/>）に:
    /// <b>関の賞金</b>（雑兵 ＋4・峠の関 ＋14）／ <b>始めの 4 部隊は待ち構える</b>（どれも <see cref="Map11.RoadNode.Hold"/>・歩くのは湧きだけ）／
    /// <b>最初の湧きは 6 刻目</b>（北も傭兵崩れ・湧いた部隊は待ち構える敵の脇を抜けて拠点へ歩く・<see cref="SpawnsPassHolds"/>）／ <b>本陣の守り</b>（動かない・抜くと ＋4・勝てば制圧）。
    /// 単発の 1-1 と第461〜475期の 1 章の 1-1 は変えない。
    /// </summary>
    public static MapStage Stage11Ch476 { get; } = Stage11Ch476With();

    /// <summary>賞金・最初の湧きの刻・本陣の守りを替えた 1 章の 1-1（第476期の測りが候補を並べるための口・遊ぶ盤は <see cref="Stage11Ch476"/>）。</summary>
    internal static MapStage Stage11Ch476With(int gateGold = Stage11Ch476GateGold, int firstSpawn = Stage11Ch476FirstSpawn, string? garrison = Stage11Ch476GarrisonBand,
                                              bool spawnsPass = true, string spawn = Stage11Ch476SpawnBand, bool thinLane = false, bool reserve = false, bool leadsWalk = false)
    {
        MapStage b = Stage11Ch474;
        EnemyCatalog.MapSquad sp = EnemyCatalog.MapSquadOf(spawn);
        string shields = EnemyCatalog.MapSquadOf(Stage11Ch474GateBand).Name;
        Map11.RoadNode Wait(Map11.RoadNode n) => n with { Hold = true, Gold = Stage11Ch476FoeGold };
        // 測りの口 `leadsWalk`: 道の先頭（物見の兵・徴募兵）だけ第474期と同じに 1 刻目から歩かせる（賞金はそのまま）。
        Map11.RoadNode Lead(Map11.RoadNode n) => leadsWalk ? n with { Gold = Stage11Ch476FoeGold } : Wait(n);
        var south = new List<Map11.RoadNode> { Lead(b.Lanes[1][0]), Wait(b.Lanes[1][1]) };
        if (garrison is not null) south.Add(Garrison(1, 2, garrison, 4) with { Name = "本陣の守り", Gold = Stage11Ch476GarrisonGold });
        return new()
        {
            Id = b.Id,
            Title = "1 章 1-1 辺境の砦",   // 題と結果の窓（第475期までは「検証用マップ 1-1」）
            Board = b.Board,
            Lanes = new IReadOnlyList<Map11.RoadNode>[]
            {
                new[]
                {
                    Lead(b.Lanes[0][0]) with { Name = "物見の兵" },
                    b.Lanes[0][1] with
                    {
                        Name = Stage11Ch476GateName, Gold = gateGold, Tag = "硬い",
                        Note = $"最大HP ×{Stage11Ch474GateHp / 100.0:0.#}・攻撃 ×{Stage11Ch474GateAtk / 100.0:0.#}（中身は{shields}）",
                    },
                },
                south.ToArray(),
            },
            LaneNames = b.LaneNames,
            Spawn = lane => lane == 0 ? (sp.Enemy, sp.Name, 2) : b.Spawn(lane),
            Portal = b.Portal with { FirstSpawn = firstSpawn },
            Time = b.Time,
            Arrival = b.Arrival,
            SquadCap = b.SquadCap,
            WinOnCapture = true,
            Gold = b.Gold,
            SpawnsPassHolds = spawnsPass,
            SpawnToThinLane = thinLane,
            // 測りの口: 本陣で出番を待つ部隊（1-2 の予備隊と同じ規則——最初の接敵が決着した刻の終わりに、味方の隊のいない経路へ出る）。
            Reserve = reserve ? new ReserveRule(true, sp.Enemy, sp.Name, 2) : ReserveRule.Off,
            WarnEmptyHome = true,
            Notes = new[]
            {
                "敵の本陣を制圧したら勝ち。",
                "北は近道だが峠の関が硬い（抜けば賞金）。南は回り道だが軽い。",
                $"{firstSpawn + 1} 刻目から、敵の本陣から部隊が歩いてくる。拠点を空にしないこと。",
            },
        };
    }

    // ---------------- 第477期: 1-2 の盤を作り直す（指示書 design/PHASE477_STAGE12_BOARD_SPEC.md §2） ----------------

    /// <summary>第477期の 1 章の 1-2 の値（測りが振る口の既定）。見張り台の部隊・中の 2 番手・中間拠点の金（北 ／ 中 ／ 南）・待ち構える部隊の賞金・期限。</summary>
    internal const string Stage12Ch477TowerBand = "band_fire", Stage12Ch477MidSecond = "band_levy", Stage12Ch477SpawnBand = "band_levy", Stage12Ch477ReserveBand = "band_levy";   // 歩く敵は徴募兵（ポン 2026-11-15・始めは 弓隊 ／ 傭兵崩れ ／ 騎士の小隊）
    internal const int Stage12Ch477TowerGold = 0, Stage12Ch477GateGold = 8, Stage12Ch477FerryGold = 12, Stage12Ch477FoeGold = 6, Stage12Ch477TurnCap = 14;

    /// <summary>
    /// 1 章の 1-2（第477期・`ChapterRule.V477` から）。盤は <see cref="MapGraph.Build12F477"/>（北 4 ／ 中 3 ／ 南 6 マス・横の辺は 関所 ⇔ 見張り台）:
    /// <b>北の山道</b> 見張り台に帯 B の硬い部隊（待ち構える）・取ればレリックの候補 ／ <b>中の街道</b> 先頭（物見の兵）と 2 番手（徴募兵）が拠点へ歩いてくる・関所は金 ＋8 ／
    /// <b>南の湿地の道</b> 軽い部隊が 2 つ待ち構える・渡し場は金 ＋12。<b>歩くのは中の街道の部隊・湧き・予備隊だけ</b>:
    /// 湧きは全部 中の街道へ（<see cref="SpawnLane"/>）・予備隊は隊のいない経路を 中 → 北 → 南 の順に選ぶ（<see cref="ReserveOrder"/>）。
    /// 門（中間拠点 2 つ）・門番の勇者・本陣の守り・連戦・湧きの間隔・金の規則・隊の枠・候補・レリックの引き方は単発の 1-2（<see cref="Stage12"/>）のまま。
    /// 単発の 1-2 と第461〜476期の 1 章の 1-2 は変えない。
    /// </summary>
    public static MapStage Stage12Ch477 { get; } = Stage12Ch477With();

    /// <summary>値を替えた第477期の 1 章の 1-2（測りが候補を並べるための口・遊ぶ盤は <see cref="Stage12Ch477"/>）。</summary>
    internal static MapStage Stage12Ch477With(string tower = Stage12Ch477TowerBand, int towerHp = 100, int towerAtk = 100, string midSecond = Stage12Ch477MidSecond,
                                              int towerGold = Stage12Ch477TowerGold, int gateGold = Stage12Ch477GateGold, int ferryGold = Stage12Ch477FerryGold,
                                              int turnCap = Stage12Ch477TurnCap, bool passHolds = false, int spawnEvery = 0, string? reserve = null,
                                              string southFirst = "band_levy", string southSecond = "band_sellswords", string spawn = Stage12Ch477SpawnBand,
                                              string garrison = "band_garrison", bool hero = true, int heroPercent = 100, bool retreatToOutpost = true)
    {
        MapStage b = Stage12;
        Map11.RoadNode Wait(Map11.RoadNode n) => n with { Hold = true, Gold = Stage12Ch477FoeGold };
        EnemyCatalog.MapSquad sp = EnemyCatalog.MapSquadOf(spawn);
        EnemyCatalog.MapSquad rs = EnemyCatalog.MapSquadOf(reserve ?? Stage12Ch477ReserveBand);
        string towerNote = towerHp == 100 && towerAtk == 100 ? "" : $"最大HP ×{towerHp / 100.0:0.##}・攻撃 ×{towerAtk / 100.0:0.##}";
        return new()
        {
            Id = b.Id,
            Title = "1 章 1-2 討伐隊の砦",
            Board = MapGraph.Build12F477,
            Lanes = new IReadOnlyList<Map11.RoadNode>[]
            {
                new[] { Wait(Hard(Band(0, 0, tower, 3), towerHp, towerAtk)) with { Tag = "硬い", Note = towerNote } },
                new[] { Node(1, 0, EnemyCatalog.Stages[0], 0) with { Name = "物見の兵" }, Band(1, 1, midSecond, 2), Garrison(1, 2, garrison, 4) with { Name = "本陣の守り" } },
                new[] { Wait(Band(2, 0, southFirst, 0)), Wait(Band(2, 1, southSecond, 2)) },
            },
            LaneNames = MapGraph.Lanes12F477,
            Spawn = _ => (sp.Enemy, sp.Name, 2),
            Portal = spawnEvery > 0 ? b.Portal with { SpawnEvery = spawnEvery } : b.Portal,
            Time = b.Time,
            Arrival = b.Arrival,
            Reserve = new ReserveRule(true, rs.Enemy, rs.Name, 1),
            SquadCap = b.SquadCap,
            TurnCap = turnCap,
            TimeLimit = b.TimeLimit,
            WinOnCapture = b.WinOnCapture,
            Gold = b.Gold,
            MatchOffer = b.MatchOffer,
            PortalGate = b.PortalGate,
            Relic = b.Relic,
            // 測りの口 `heroPercent`: 勇者の最大HP と攻撃だけに掛ける倍率（採らない・並べるだけ）。
            Hero = !hero ? HeroRule.Off : heroPercent == 100 || b.Hero.Unit is not { } hu ? b.Hero : b.Hero with { Unit = hu.WithStats(MapFoeScale.Mul(hu.MaxHp, heroPercent), MapFoeScale.Mul(hu.Attack, heroPercent)) },
            Siege = b.Siege,
            CrossFacing = true,
            ViewScale = 1.35f,
            SpawnsPassHolds = passHolds,
            SpawnLane = 1,
            SiegeRetreatToOutpost = retreatToOutpost,
            ReserveOrder = new[] { 1, 0, 2 },
            OutpostRewards = new[] { new OutpostReward(towerGold, true), new OutpostReward(gateGold, false), new OutpostReward(ferryGold, false) },
            WarnEmptyHome = true,
            Notes = new[]
            {
                "中間拠点を 2 つ取ると砦の門が開く。門番を倒して砦を制圧したら勝ち。",
                $"見張り台 ＝ レリック（硬い）／ 関所 ＝ 金 ＋{gateGold}（近い）／ 渡し場 ＝ 金 ＋{ferryGold}（遠い）。",
                "敵は中の街道を歩いてくる。拠点を空にしないこと——関所に隊がいれば、そこで受けられる。",
            },
        };
    }

    /// <summary>選べるステージ（編成画面の選択の並び）。</summary>
    public static IReadOnlyList<MapStage> All { get; } = new[] { Stage11, Stage12, Stage13 };
}

/// <summary>中間拠点 1 つの見返り（第477期）。<c>Gold</c> は取るたびに入る金、<c>Relic</c> は初めて取ったときにレリックの候補が出るか。</summary>
public readonly record struct OutpostReward(int Gold, bool Relic)
{
    /// <summary>画面の名札の文（「金 ＋8」「レリック」「金 ＋8・レリック」）。</summary>
    public string Text => (Gold > 0 ? $"金 ＋{Gold}" : "") + (Gold > 0 && Relic ? "・" : "") + (Relic ? "レリック" : "");
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
/// <param name="Garrison">動かない守備隊（<see cref="Map11.RoadNode.Stationary"/>）を抜く（第449期）。0 の盤には守備隊がいない。</param>
public readonly record struct GoldRule(bool On, int Start, int Kill, int SpawnKill, int Outpost, int Recruit, int Slot, int Sell,
                                       int Offers, int StartSquads, int Reroll = 0, bool SlotFreePick = false,
                                       int SlotStep = 0, int Revive = 0, int RevivePercent = 50, int Garrison = 0, int Horde = 0)
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
    /// <summary>第449期の値: 第447期に「本陣の守り（動かない守備隊）を抜く ＋10」を足した。</summary>
    public static GoldRule V449() => V447() with { Garrison = 10 };
    /// <summary>第458期の値（1-3）: 第449期に「大群の 1 部隊を抜く ＋3」を足し、本陣の守り（重）を ＋12 にした。</summary>
    public static GoldRule V458() => V449() with { Garrison = 12, Horde = 3 };
    /// <summary>第445期の仮の値（ポンが遊んで決める）: 開始 0・抜く +6・湧き +3・中間拠点 +8・呼ぶ 10・枠 12・売る +3・候補 3・隊 2 で始まる。</summary>
    public static GoldRule Of(int start = 0, int kill = 6, int spawnKill = 3, int outpost = 8, int recruit = 10, int slot = 12,
                              int sell = 3, int offers = 3, int startSquads = 2)
        => new(true, start, kill, spawnKill, outpost, recruit, slot, sell, offers, startSquads);
}

/// <summary>
/// 作戦マップのレリック（第452期 §1）。<b><c>On</c> が偽ならレリックは出ない</b>（第451期まで・1-1）。
/// 札は `RelicCatalog.All`（12 枚）から seed で並べた列の先頭から引く——<b>同じ札は 1 マップに 1 度しか出ない</b>（引いた札は列に戻さない）。
/// <b>出るのは中間拠点を初めて取った刻だけ</b>（敵を抜いても・制圧しても出ない——レリックは「場所を取った」見返り、金は「敵を抜いた」見返り）。
/// </summary>
/// <param name="Offers">候補の枚数。</param>
/// <param name="Bridge">
/// 候補の 1 枚目を「両端が手持ちにある札」（無ければ片端・無ければふつう）から引く（第453期 §0-2・両端は `RelicBridges`）。
/// 残りはふつう。<b>噛むかどうかは画面に出さない</b>。偽なら第452期まで。
/// </param>
/// <param name="AxisOnly">
/// 両端の判定を<b>軸の語だけ</b>で見る（第454期 §1・役割の語（タンク・ヒーラーなど）の端は「誰でも」と同じに扱う）——
/// 第453期は 1 枚目が弾性（タンク → 移動）・痺れる足に偏った（初期の手持ちにタンクがほぼ必ずいる）。
/// </param>
public readonly record struct RelicRule(bool On, int Offers, bool Bridge = false, bool AxisOnly = false)
{
    public static readonly RelicRule Off = new(false, 0);
    /// <summary>第452期の値: 候補 3 枚から 1 枚。</summary>
    public static readonly RelicRule V452 = new(true, 3);
    /// <summary>第453期の値: 第452期 ＋ 1 枚目を両端の札から。</summary>
    public static readonly RelicRule V453 = new(true, 3, Bridge: true);
    /// <summary>第454期の値: 第453期 ＋ 両端は軸の語だけで見る。</summary>
    public static readonly RelicRule V454 = new(true, 3, Bridge: true, AxisOnly: true);
}

/// <summary>
/// 作戦マップの勇者（第454期 §2・骨子の段C）。<b><c>On</c> が偽なら勇者は出ない</b>。
/// <b>門が開いた刻の <c>AfterGate</c> 刻後</b>に敵の拠点のマスへ<b>予告なしに</b>現れ（札も「あと n」も出さない）、<b><c>StepEvery</c> 刻に 1 マス</b>味方の拠点へ進む
/// （経路は予備隊と同じ選び方・前が詰まれば待つ・味方のいるマスへ入れば戦闘・拠点に着けば迎撃）。HP は戦闘をまたいで持ち越す（敵部隊と同じ）。
/// 勝ちは制圧のまま——倒しても勝ちではないが、倒せば <c>Gold</c>。
/// </summary>
/// <param name="AfterCapture">
/// 第455期 §0（1-3 の形）: <b>本陣を制圧した刻</b>に、制圧した本陣の 1 つ手前（経路の奥のマス・味方の隊がいない経路を先に）へ現れる（<c>AfterGate</c> は読まない）。
/// <b>勝ちは制圧 ＋ 勇者を倒す</b>・刻の期限は制圧までで、現れた後は外す（勇者が時計）。制圧した刻に残っていたほかの敵部隊は退く。
/// 勇者は中間拠点を取り返さず、味方の隊と同じマスに立てば素通りしない（その刻の終わりに戦闘）。拠点に居座れば 2 刻ごとに迎撃をやり直す。偽なら第454期まで。
/// </param>
/// <param name="Gatekeeper">
/// 第456期 §0（門番）: <b>門が開いた刻</b>（その刻の敵の前進の終わり）に<b>敵の拠点のマス</b>に立ち、<b>動かない</b>（<c>AfterGate</c> ／ <c>StepEvery</c> は読まない）。
/// 本陣に入ると本陣の守り → 勇者 → 湧き・予備隊の順に当たり、勇者が立っている間は制圧できない。勝ちは制圧のまま。門が閉じ直しても立ったまま。
/// </param>
/// <param name="Chaser">
/// 第459期 §1（追ってくる勇者・1-3）: <b>門が開いた刻</b>に<b>味方の拠点から <c>FromHome</c> 歩のマス</b>（経路の上・`Lanes[road][FromHome - 1]`）に現れ、
/// <b><c>StepEvery</c> 刻に 1 歩</b>拠点へ進む（ほかの敵に詰まらない）。隊のいるマスに入ればその刻に戦闘・拠点に隊がいれば迎撃。
/// 拠点に隊がいなければ<b>拠点が落ちる（`Map11State.HomeLost`）が負けではない</b>——現れた刻から、拠点に何が着いても負けにしない。
/// 落ちた後は呼ぶ・休む・組み直しは取っている中間拠点でだけ、勇者はいちばん近い隊を追う。勝ちは制圧のまま（勇者は倒さなくてよい・本陣では守りの後ろに並ぶ）。
/// </param>
/// <param name="FromHome">追う勇者が現れるマスの、味方の拠点からの歩数（第459期・<c>Chaser</c> の盤だけが読む）。</param>
/// <summary>
/// 敵の数値の倍率（第462期・指示書 design/PHASE462_CHAPTER_SCALE_SPEC.md §1-1・1 章だけ）。百分率で「勇者」と「それ以外」の 2 列（この期は同じ値）。
/// <b>掛けるのは敵の駒の最大HP と攻撃（四捨五入）と、勇者の毎手番の固定回復（<see cref="BossMendTrait.Fixed"/>）</b>。速さ・札の係数・状態異常・金は掛けない。
/// <para>engine の倍率（<see cref="EnemyScaleRule"/>・切り捨て）は使わない——指示書が四捨五入を指定していて、小さい攻撃（6 × 1.3 ＝ 7.8）で 1 点ずれるため。
/// engine の倍率が持つ「敵が呼んだ召喚に同じ倍率」の枝は、いまの敵に敵陣へ駒を呼ぶ敵が 0 体なので（`BattleContext.Summon` の注）、この写しでは要らない。</para>
/// </summary>
/// 第465期: <c>HeroMends</c> が偽なら勇者は毎手番の固定回復の札（<see cref="TraitId.BossMend300"/>）を持たない（1 章の `ChapterRule.V465`）。真が第464期まで。
public readonly record struct MapFoeScale(int FoePercent, int HeroPercent, bool HeroMends = true)
{
    public static readonly MapFoeScale None = new(100, 100);
    public bool Active => FoePercent != 100 || HeroPercent != 100 || !HeroMends;

    /// <summary>四捨五入で倍率を掛ける（下限 1）。</summary>
    public static int Mul(int x, int percent) => Math.Max(1, (int)Math.Round(x * (double)percent / 100, MidpointRounding.AwayFromZero));

    private static string Times(int percent) => $"×{percent / 100.0:0.0#}";
    /// <summary>表示の倍率（「×1.3」・勇者だけ違えば「×1.3（勇者 ×1.0）」）。</summary>
    public string Text => FoePercent == HeroPercent ? Times(FoePercent) : $"{Times(FoePercent)}（勇者 {Times(HeroPercent)}）";
    /// <summary>ステージの頭の札の 1 行（第462期 §1-3・1.0 なら空）。</summary>
    public string IntroLine => Active ? $"敵の強さ {Text}" : "";
    /// <summary>作戦の画面の上の帯（第462期 §1-3・1.0 なら空）。</summary>
    public string BarText => Active ? $"敵 {Text}" : "";

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(UnitDef, int, bool, bool), UnitDef> _defs = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Formation, int), Formation> _forms = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(TraitId, int), BossMendTrait> _mends = new();

    private static UnitDef Apply(UnitDef d, int percent, bool hero, bool mends = true)
        => percent == 100 && (mends || !hero || !d.Traits.Contains(TraitId.BossMend300)) ? d : _defs.GetOrAdd((d, percent, hero, mends), static k =>
    {
        (UnitDef src, int p, bool isHero, bool keepMend) = k;
        // 項目は `UnitDef.WithStats` と同じ並び（足したらここにも足す）。勇者だけ説明文の固定回復の数を掛けた後の値に替える。
        int mend = Mul(BossMendTrait.Fixed300, p);
        // 第465期: 回復の無い勇者は札（BossMend300）を外し、説明文の「ターン頭に 300 癒える。」の文を消す（ほかの札・文はそのまま）。
        bool drop = isHero && !keepMend && src.Traits.Contains(TraitId.BossMend300);
        return new UnitDef
        {
            Id = src.Id, Name = src.Name, MaxHp = Mul(src.MaxHp, p), Attack = Mul(src.Attack, p), Speed = src.Speed,
            Traits = drop ? src.Traits.Where(t => t != TraitId.BossMend300).ToArray() : src.Traits,
            Pattern = src.Pattern, Advances = src.Advances, Actions = src.Actions,
            PlusText = drop ? src.PlusText.Replace($"ターン頭に {BossMendTrait.Fixed300} 癒える。", "")
                     : isHero && src.Traits.Contains(TraitId.BossMend300) ? src.PlusText.Replace($"{BossMendTrait.Fixed300} 癒える", $"{mend} 癒える") : src.PlusText,
            MinusText = src.MinusText, Flavor = src.Flavor,
        };
    });

    /// <summary>勇者以外の敵の駒の定義に掛けた写し（同じ定義・同じ倍率には同じインスタンス）。</summary>
    public UnitDef Foe(UnitDef d) => Apply(d, FoePercent, false);
    /// <summary>勇者の定義に掛けた写し（説明文の「300 癒える」も掛けた後の値）。</summary>
    public UnitDef HeroDef(UnitDef d) => Apply(d, HeroPercent, true, HeroMends);

    /// <summary>敵の部隊（編成）の写し（陣形・枠はそのまま・駒だけ掛ける・同じ編成には同じインスタンス）。</summary>
    public Formation Foe(Formation f)
    {
        int p = FoePercent;
        if (p == 100) return f;
        MapFoeScale self = this;
        return _forms.GetOrAdd((f, p), _ =>
        {
            Formation c = f.Clone();
            foreach ((int slot, UnitDef d) in f.Occupied()) c[slot] = self.Foe(d);
            return c;
        });
    }

    /// <summary>経路の区画（道の部隊・中間拠点・大群・本陣の守り）に掛けた写し。</summary>
    public IReadOnlyList<Map11.RoadNode>[] Lanes(IReadOnlyList<Map11.RoadNode>[] lanes)
    {
        MapFoeScale self = this;
        return lanes.Select(l => (IReadOnlyList<Map11.RoadNode>)l.Select(n => n with { Enemy = self.Foe(n.Enemy) }).ToArray()).ToArray();
    }

    /// <summary>湧きに掛けた写し（湧くたびに同じ編成なら同じ写し）。</summary>
    public Func<int, (Formation Enemy, string Name, int StageIndex)> Spawn(Func<int, (Formation Enemy, string Name, int StageIndex)> spawn)
    {
        MapFoeScale self = this;
        return road => { var (e, name, idx) = spawn(road); return (self.Foe(e), name, idx); };
    }

    /// <summary>勇者の毎手番の固定回復に掛けた量（300 × 倍率・四捨五入）。</summary>
    public int HeroMend(int fixedAmount) => Mul(fixedAmount, HeroPercent);

    /// <summary>
    /// 勇者の駒の固定回復を掛けた後の量に替える（`Map11State` が勇者の駒を作った直後に 1 度だけ・§3 の器具も同じ口）。
    /// 札（<see cref="BossMendTrait"/>）は量だけが違う同じ種類の写しに差し替える——札の持つ状態は無い（定数だけ）ので、量ごとに 1 つを共有する。
    /// 倍率 1.0 ・固定回復を持たない駒はそのまま。
    /// </summary>
    public List<UnitState> MendHero(List<UnitState> us)
    {
        if (HeroPercent == 100) return us;
        MapFoeScale self = this;
        for (int i = 0; i < us.Count; i++)
        {
            UnitState u = us[i];
            if (!u.Traits.Any(t => t is BossMendTrait { Fixed: > 0 })) continue;
            us[i] = new UnitState
            {
                Def = u.Def, TeamId = u.TeamId, Shape = u.Shape, Slot = u.Slot, Hp = u.Hp, MaxHp = u.MaxHp, Relic = u.Relic,
                Traits = u.Traits.Select(t => t is BossMendTrait { Fixed: > 0 } m
                    ? _mends.GetOrAdd((m.Id, self.HeroMend(m.Fixed)), static k => new BossMendTrait(k.Item1, 0, k.Item2))
                    : t).ToArray(),
            };
        }
        return us;
    }
}

public readonly record struct HeroRule(bool On, int AfterGate, int StepEvery, UnitDef? Unit, int Gold, bool AfterCapture = false, bool Gatekeeper = false,
                                       bool Chaser = false, int FromHome = 0)
{
    public static readonly HeroRule Off = new(false, 0, 0, null, 0);
    /// <summary>第454期の値: 門の 2 刻後・2 刻に 1 マス・版 B（`EnemyCatalog.HeroMap`・固定 300 回復）・倒して ＋20。</summary>
    public static readonly HeroRule V454 = new(true, 2, 2, EnemyCatalog.HeroMap, 20);
    /// <summary>版 A（回復は最大HPの 40%）。遊ぶのは <see cref="V454"/>。</summary>
    public static readonly HeroRule V454A = new(true, 2, 2, EnemyCatalog.HeroMapA, 20);
    /// <summary>第455期の値: 制圧した刻に現れる・2 刻に 1 マス・版 B・倒せば勝ち（金は無し）。</summary>
    public static readonly HeroRule V455 = new(true, 0, 2, EnemyCatalog.HeroMap, 0, AfterCapture: true);
    /// <summary>版 A（回復は最大HPの 40%・「難しい」の切替）。遊ぶのは <see cref="V455"/>。</summary>
    public static readonly HeroRule V455A = new(true, 0, 2, EnemyCatalog.HeroMapA, 0, AfterCapture: true);
    /// <summary>第456期の値: 門が開いた刻に本陣の前に立つ門番・動かない・版 B・倒して ＋20。</summary>
    public static readonly HeroRule V456 = new(true, 0, 0, EnemyCatalog.HeroMap, 20, Gatekeeper: true);
    /// <summary>版 A（回復は最大HPの 40%）。遊ぶのは <see cref="V456"/>。</summary>
    public static readonly HeroRule V456A = new(true, 0, 0, EnemyCatalog.HeroMapA, 20, Gatekeeper: true);
    /// <summary>第459期の値（1-3）: 門が開いた刻に拠点から 3 歩のマスに現れて追ってくる・毎刻 1 歩（K = 1）・版 B・倒して ＋20。</summary>
    public static readonly HeroRule V459 = new(true, 0, 1, EnemyCatalog.HeroMap, 20, Chaser: true, FromHome: 3);
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

/// <summary>
/// 本陣の連戦（第457期 §0-1）。<b><c>On</c> が偽なら第456期まで</b>（敵の拠点のマスでも 1 刻 1 戦・残った相手とは次の刻に当たる）。
/// 真なら、敵の拠点のマスで勝った隊は<b>同じ刻のうちに</b>次の相手（`Map11State.PortalFoe` の順）と続けて戦うか、
/// 入ってきた経路の中間拠点（経路の奥のマス）へ引くかを選ぶ（引くのは無料・1 刻かからない）。本陣の戦闘では道中の回復を乗せない。
/// 中間拠点・道の上・味方の拠点の迎撃は今までどおり。
/// </summary>
/// <param name="Stacks">
/// 第458期: 本陣のほか、<b>敵が 2 部隊以上重なっているマス</b>（1-3 の大群）でも連戦の窓を出す。引くと入ってきた側の隣のマスへ。
/// 大群（<see cref="Map11.RoadNode.Hold"/>）との戦闘では道中の回復を乗せない（本陣と同じ）。中間拠点は 1 刻 1 戦のまま。
/// </param>
public readonly record struct SiegeRule(bool On, bool Stacks = false)
{
    public static readonly SiegeRule Off = new(false);
    /// <summary>第457期の値: 本陣で連戦・合間に引ける・本陣では回復なし。</summary>
    public static readonly SiegeRule V457 = new(true);
    /// <summary>第458期の値（1-3）: 第457期に、敵が重なったマス（大群）の連戦を足した。</summary>
    public static readonly SiegeRule V458 = new(true, Stacks: true);
}
