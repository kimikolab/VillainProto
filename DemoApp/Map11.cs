using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 検証用マップ 1-1（第169期）
//
// **第168期 部B で3つの線を同時に通した1点（`S4/S4` × 道中の回復 50%）を、そのまま写しただけ。**
// 数値・敵・隊は第168期から1つも変えていない。**ここには Godot の型を1つも使わない**
// ——同じ規則を頭なしの自己検査（`--map11-verify`）からも回すため。
//
//   北の道: 第二波・先遣（後1 施しの司祭長を抜く） → 第三波（5体）
//   南の道: 第四波・先遣（後1 詠唱兵を抜く）       → 第五波（5体）
//
// 先遣隊の定義は `EnemyCatalog.Vanguards`（`Stages` / `Columns` には載っていない）。
// =====================================================================================

/// <summary>マップ 1-1 の定義。<b>静的な内容だけ</b>で、進行の状態は <see cref="Map11State"/> が持つ。</summary>
public static class Map11
{
    /// <summary>道中の回復（%）。<b>勝った隊だけが、次の戦闘へ入る前に戻す。</b> 死者は戻らない。</summary>
    public const int RecoverPercent = 50;

    /// <summary>1 マップの戦闘回数の上限（第168期 <c>BandCap</c> と同じ）。</summary>
    public const int BattleCap = 24;

    /// <summary>
    /// 敵の数値の倍率（第187期）。<b>作戦マップには掛けない</b>（ポンの判断）——
    /// 単発戦（`compare` の波）は <see cref="EnemyScaleRule.Default"/>（115 / 115）のままだが、
    /// このマップは第168期に斥候級と道中の回復 50% で帯に入れたもので、115 / 115 を掛けると
    /// 「逆」の踏破率が 35.5% → 8.1% まで落ちる。<b>マップの敵を作る口はすべてこれを読む</b>
    /// （<see cref="Map11State.Prepare"/> / <c>PrepareIntercept</c> と、マップの敵で1戦を回す検査器具）。
    /// </summary>
    public static readonly EnemyScaleRule EnemyScale = EnemyScaleRule.None;

    // ---------------- 時間（第174期） ----------------

    /// <summary>
    /// 道のマス数（第174期 §2-1）。<b>拠点と1つ目の敵のあいだに空きマスを1つ足した。</b>
    /// マス 0 が空き、マス 1・2 が敵部隊の初期位置（区画の番号 + 1）。拠点は -1。
    /// </summary>
    public const int RoadCells = 3;

    /// <summary>1 マップの作戦ターンの上限（戦闘回数の上限とは別の歯止め）。</summary>
    public const int TurnCap = 60;

    /// <summary>
    /// 時計の 1 刻（＝ 1 作戦ターン）の長さ（秒・第439期）。2× はこの半分。
    /// <b>0 以下にすると時計を切る</b>——▶ は第438期の「▶ 進める」と同じく出来事まで一気に回す。
    /// 規則（<see cref="Map11State"/>）は読まない。読むのは画面の時計（<see cref="Map11Clock"/>）だけ。
    /// </summary>
    public const double TickSeconds = 1.2;

    /// <summary>敵部隊の初期のマス。<b>空きマスのぶんだけ奥にいる。</b></summary>
    public static int StartCellOf(int nodeIndex) => nodeIndex + 1;

    // ---------------- 敵の拠点（ワープポータル・第176期） ----------------

    /// <summary>
    /// 敵の拠点のマス（道のいちばん奥・第176期 §1-1）。<b>北と南はここで合流する</b>
    /// ——道の上のマスは 0..<see cref="RoadCells"/>-1 なので、その1つ先が拠点である。
    /// <b><see cref="PortalRule.On"/> が偽なら誰もこのマスに立てない</b>（`Map11State.MaxCell`）。
    /// </summary>
    public const int PortalCell = RoadCells;

    /// <summary>敵の拠点の呼び名（画面と器具で1つにする）。</summary>
    public const string PortalName = "敵の拠点";

    /// <summary>
    /// 湧く部隊の中身（第176期 §1-2）。<b>新しい敵は1体も作らない</b>
    /// ——北なら第二波・先遣、南なら第四波・先遣で、どちらも <see cref="EnemyCatalog.Vanguards"/> の写し。
    /// </summary>
    public static Formation SpawnEnemy(int road) => EnemyCatalog.Vanguards[road].Enemy;

    /// <summary>湧く部隊の名前（道で変わる）。</summary>
    public static string SpawnName(int road) => EnemyCatalog.Vanguards[road].Name;

    /// <summary>湧く部隊の背景（<b>元の波と同じ</b>——先遣は第二波 / 第四波の背景で描く）。</summary>
    public static int SpawnStage(int road) => road == 0 ? 1 : 3;

    /// <summary>
    /// 遊ぶときの時間の規則（<b>第174期 段B1 で測って決めた</b>）。
    /// <b>K = 1</b>（敵は毎作戦ターン1マス近づく）——線1〜3 が同時に通る K のうち最も小さい値で、
    /// <b>K = 2/3/4 は線1（全快方針の迎撃が起きた率 ≥ 50%）を通らない</b>（帯A/帯B とも）。
    ///
    /// <para><b>第175期に迎撃の回復を落とした</b>（<see cref="TimeRule.InterceptRecover"/> ＝ <c>false</c>）
    /// ——待ちが得だった3つの理由のうち、規則の1行で外せるのはこれだけ（§2）。</para>
    /// </summary>
    public static readonly TimeRule AdoptedTime = TimeRule.Every(1);

    /// <summary>
    /// 遊ぶときの敵の拠点の規則（<b>第177期 §1</b>）。<b>測って決めた値ではない</b>
    /// ——この期は「器具で線を引いてから画面に出す」形をやめ、
    /// <b>ポンの構想どおりの値をそのまま入れて遊んで直す</b>（指示書 §0）。
    ///
    /// <para><c>S = 8</c>（湧きのリキャスト）／<c>R = 3</c>（味方のワープのリキャスト）。
    /// <b>どちらも同じ1本のタイマー</b>で、使った側の値だけ次に使えなくなる
    /// （<see cref="Map11State.PortalCooldown"/>）。第176期の測定は <c>S = 2/3/4/6</c> だったので、
    /// <b>8 はそのどれよりも緩い</b>——湧きで押し切る盤面ではなくなっている。</para>
    /// </summary>
    public static readonly PortalRule AdoptedPortal = PortalRule.Every(8, warpEvery: 3);

    public const int RoadCount = 2;
    public static readonly string[] RoadNames = { "北の道", "南の道" };

    /// <summary>
    /// 道の 1 区画。<c>Index</c> は 0 が手前（先に当たる側）。
    /// <c>StageIndex</c> は<b>背景の選択にしか使わない</b>（先遣も元の波と同じ背景で描く）。
    /// </summary>
    public sealed record RoadNode(int Road, int Index, string Name, Formation Enemy, int StageIndex);

    /// <summary>道ごとの敵部隊。<b>手前を抜かないと奥に当たれない。</b></summary>
    public static IReadOnlyList<RoadNode>[] Roads { get; } =
    {
        new[]
        {
            new RoadNode(0, 0, EnemyCatalog.Vanguards[0].Name, EnemyCatalog.Vanguards[0].Enemy, 1),
            new RoadNode(0, 1, EnemyCatalog.Stages[2].Name,    EnemyCatalog.Stages[2].Enemy,    2),
        },
        new[]
        {
            new RoadNode(1, 0, EnemyCatalog.Vanguards[1].Name, EnemyCatalog.Vanguards[1].Enemy, 3),
            new RoadNode(1, 1, EnemyCatalog.Stages[4].Name,    EnemyCatalog.Stages[4].Enemy,    4),
        },
    };

    public static int TotalNodes => Roads.Sum(r => r.Count);

    // ---------------- 隊 ----------------

    public sealed record SquadDef(string Id, string Name, string Role, Formation F);

    private const string RowKado = "反撃 (ヒサ×カド)";
    private const string RowHane = "突き返し (ハネ×ウツ)";
    private const string RowHold = "死軸×ヒヨ (ゾト×火選り)";

    private static Formation RowOf(string name) =>
        Presets.Compare.FirstOrDefault(r => r.Name == name).F
        ?? throw new InvalidOperationException("`Presets.Compare` にその行が無い: " + name);

    /// <summary>
    /// ハネ隊。<b>席はその行のまま</b>で、カド隊と重なる 前3 ガルドだけを<b>軋みのヨミ</b>に替える
    /// ——第168期 §2-2 が「どこでも同じ」群の上位5枚を測って選んだ1枚（ハネ隊の第2〜5波の
    /// 単発勝率の平均が最も高い）。<b>ここでは選び直さず、その結果を固定で写す。</b>
    /// </summary>
    private static Formation HaneSquad()
    {
        Formation kado = RowOf(RowKado), src = RowOf(RowHane);
        var inKado = new HashSet<string>(kado.Occupied().Select(o => o.Def.Id), StringComparer.Ordinal);
        var f = new Formation();
        foreach ((int slot, UnitDef def) in src.Occupied())
            f[slot] = inKado.Contains(def.Id) ? UnitCatalog.Yomi : def;
        return f;
    }

    /// <summary>
    /// 3 隊。先頭 2 隊が最初から拠点にいて、3 番目が控え。
    /// <b>第172期に「ハネ隊」を「かき回し隊」へ改名した</b>（指示書 §3）——隊を動かしているのは
    /// バサ・ヨミ・ウツ・ドルガで、<b>看板に偽りがあった</b>（`Id` は `hane` のまま。
    /// 器具（第168期）の行名と突き合わせるときに追えなくなるため）。
    /// </summary>
    /// <summary>
    /// 4 隊目の定義（第447期・<b>隊の枠を 4 つにした盤だけが使う</b>）。中身は空——呼んだ駒で組む。
    /// <see cref="Squads"/>（器具が 3 隊として読む）には入れない。
    /// </summary>
    public static readonly SquadDef ExtraSquad = new("extra", "第4隊", "呼んだ駒で組む隊", Formation.Build());

    public static SquadDef[] Squads { get; } =
    {
        new("kado", "カド隊", "反撃・棘", RowOf(RowKado)),
        new("hane", "かき回し隊", "突き返し", HaneSquad()),
        new("hold", "控え隊", "死軸・火選り", RowOf(RowHold)),
    };

    // ---------------- 控えの駒（第172期 §1-2） ----------------

    /// <summary>控えの駒の枚数（第172期 §1-2 は 6 枚。<b>第173期 §1-3 #4 で 7 枚にした</b>）。</summary>
    public const int ReserveCount = 7;

    /// <summary>
    /// 名指しの3枚（第172期 §1-2 は 2 枚）。<b>ポンが「ヒヨの代わり」を試せるようにするための指定。</b>
    ///
    /// <para><b>第173期に空焚きのホタを足した</b>（指示書 §1-3 #4）——控えの 6 枚には
    /// <b>火を撒く駒（ボルグ）も火を読む駒（ヒヨ）も既に盤上にいるのに、火の受け手だけが
    /// 欠けていた</b>。ホタは自分では着火できず、<b>ボルグの隣に置くという配置判断が
    /// 発動条件そのもの</b>（`PyreTrait` の宣言）なので、<b>組み直しで初めて意味を持つ1枚</b>である。
    /// 規則で選ぶ側（<see cref="ReserveRanking"/>）は「3 隊それぞれと同席した実績」を見るので、
    /// この形の駒は構造的に上がってこない。</para>
    /// </summary>
    private static readonly UnitDef[] NamedReserves =
        { UnitCatalog.Kubi, UnitCatalog.Sekki, UnitCatalog.Hota };

    /// <summary>候補1枚ぶんの素性（<c>--map11-phase172</c> がそのまま表にする）。</summary>
    public sealed record ReserveRow(UnitDef Def, int Seats, int[] WithSquad, bool AllThree, int Total);

    /// <summary>
    /// 残り4枚の候補を、<b>規則で</b>並べる（指示書 §1-2）。手で選ばない。
    ///
    /// <para>線は3つ——(1) 3 隊 15 枚と重ならない ／ (2) `Presets.Compare` の在席枠が 3 以上 ／
    /// (3) <b>カド隊・かき回し隊・控え隊それぞれの駒と同じ行に入った実績がある</b>。
    /// 通った駒を「同席した (行, 相手) の組の数」の多い順に並べ、同数なら
    /// <see cref="UnitCatalog.All"/> の並び順で割る。</para>
    /// </summary>
    public static ReserveRow[] ReserveRanking()
    {
        var rows = Presets.Compare;
        var squadIds = Squads.Select(s => s.F.Occupied().Select(o => o.Def.Id).ToHashSet(StringComparer.Ordinal))
                             .ToArray();
        var taken = new HashSet<string>(squadIds.SelectMany(x => x), StringComparer.Ordinal);
        foreach (UnitDef d in NamedReserves) taken.Add(d.Id);

        var list = new List<ReserveRow>();
        foreach (UnitDef def in UnitCatalog.All)
        {
            if (taken.Contains(def.Id)) continue;
            int seats = rows.Sum(r => r.F.Occupied().Count(o => o.Def.Id == def.Id));
            var with = new int[Squads.Length];
            foreach (var r in rows)
            {
                var ids = r.F.Occupied().Select(o => o.Def.Id).ToHashSet(StringComparer.Ordinal);
                if (!ids.Contains(def.Id)) continue;
                for (int s = 0; s < Squads.Length; s++)
                    with[s] += ids.Count(x => squadIds[s].Contains(x));
            }
            bool all3 = with.All(x => x > 0);
            list.Add(new ReserveRow(def, seats, with, all3 && seats >= 3, with.Sum()));
        }
        return list
            .OrderByDescending(x => x.AllThree)
            .ThenByDescending(x => x.Total)
            .ThenBy(x => UnitCatalog.All.ToList().FindIndex(u => u.Id == x.Def.Id))
            .ToArray();
    }

    /// <summary>
    /// 控えの駒 7 枚。<b>名指しの3枚 ＋ 規則で選んだ4枚</b>（<see cref="ReserveRanking"/>）。
    /// <b>ポンが差し替える前提</b>——差し替えるなら <see cref="NamedReserves"/> に足すだけでよい。
    /// </summary>
    public static UnitDef[] Reserves { get; } = NamedReserves
        .Concat(ReserveRanking().Where(x => x.AllThree).Select(x => x.Def))
        .Take(ReserveCount)
        .ToArray();

    // ---------------- 駒カードの一行（指示書 §3） ----------------

    /// <summary>
    /// 第164〜168期で<b>一行が書けた駒だけ</b>（4 枚）。書けなかった駒には何も出さない
    /// ——第164期は 25 体中 11 体しか書けず、残りは「不明」と書いた。画面では出さない。
    /// </summary>
    private static readonly Dictionary<string, string> Lines = new(StringComparer.Ordinal)
    {
        [UnitCatalog.Kado.Id] = "粛の敵が揃っていると何もできない。粛のいない道へ",
        [UnitCatalog.Hane.Id] = "重い敵が後に来る道で働く",
        [UnitCatalog.Golm.Id] = "重い敵が先に来る道で働く",
        [UnitCatalog.Basa.Id] = "戦う回数が多い道で働く",
    };

    public static string? LineOf(string unitId) => Lines.GetValueOrDefault(unitId);

    /// <summary>この部隊が持つ盤面ルール／敵側の札のうち、名前を出す価値のあるもの。</summary>
    public static string RuleLineOf(Formation f)
    {
        var names = new List<string>();
        foreach ((int _, UnitDef def) in f.Occupied())
            foreach (TraitId t in def.Traits)
            {
                string? label = t switch
                {
                    TraitId.Hush => "粛（ターン外の行動が止まる）",
                    TraitId.Drought => "渇き（回復が通らない）",
                    TraitId.Yoke => "軛（1発が25で切られる）",
                    TraitId.Inversion => "逆位（速さの向きが反転する）",
                    TraitId.Condemn => "断罪（反撃すると痺れる）",
                    _ => null,
                };
                if (label is not null && !names.Contains(label)) names.Add(label);
            }
        return names.Count == 0 ? "盤面ルールなし" : string.Join(" / ", names);
    }

    /// <summary>
    /// 各戦闘の seed を親 seed から決定的に導く。
    /// <b><c>EngagementEngine</c> の private な <c>DeriveSeed</c> と同じ式</b>——第168期の器具が
    /// この式で回しているので、自己検査 (c) を成立させるにはここも同じでなければならない。
    /// </summary>
    public static int DeriveSeed(int seed, int battleIndex) => unchecked(seed * 1000003 + battleIndex);
}

/// <summary>
/// 時間の規則（第174期）。<b><c>On</c> が偽なら第169期と1ビットも違わない</b>
/// ——このレコードを読む箇所はすべて <c>On</c> の裏側にある（自己検査 (a)）。
/// </summary>
/// <param name="On">時間を使うか。偽なら <see cref="Map11State"/> の時間の枝は1行も走らない。</param>
/// <param name="AdvanceEvery">敵が1マス前進する間隔（作戦ターン）。<c>K</c>。</param>
/// <param name="RestPercent">拠点で「休む」1回で戻る割合（<c>MaxHp</c> に対する %）。</param>
/// <param name="InterceptRecover">
/// <b>迎撃戦に勝った隊に道中の回復を乗せるか（第175期 §2。<u>この期で変えた規則はこれ1つ</u>）。</b>
/// 第174期は乗せていた（＝<c>true</c> が第174期の姿）。<b>既定は <c>false</c>。</b>
///
/// <para>理由は第174期の観察ログ——拠点で待つのが得な3つの理由
/// （移動ですり減らない／迎撃にも回復が乗る／1部隊ずつ来る）のうち、
/// <b>規則の1行で外せるのはこれだけ</b>である。休む（拠点で <see cref="RestPercent"/>）は残すので、
/// <b>待って受けるなら休みに作戦ターンを払う</b>形になる。</para>
/// </param>
public readonly record struct TimeRule(bool On, int AdvanceEvery, int RestPercent,
                                       bool InterceptRecover = false)
{
    /// <summary>時間なし（第169期の規則そのもの）。<b>迎撃が原理的に起きない</b>ので最後の1つは効かない。</summary>
    public static readonly TimeRule Off = new(false, 0, 25);

    public static TimeRule Every(int k, int restPercent = 25, bool interceptRecover = false)
        => new(true, k, restPercent, interceptRecover);

    public override string ToString() => On
        ? $"K={AdvanceEvery} 休={RestPercent}% 迎撃回復={(InterceptRecover ? "あり" : "なし")}"
        : "時間なし";
}

/// <summary>
/// 敵の拠点（ワープポータル）の規則（第176期）。
/// <b><c>On</c> が偽なら第175期と1ビットも違わない</b>——このレコードを読む箇所はすべて
/// <c>On</c> の裏側にあり、湧きも合流のマスも1行も走らない（自己検査 (a)）。
/// </summary>
/// <param name="On">敵の拠点を置くか。偽なら道の奥は <see cref="Map11.RoadCells"/>-1 で行き止まり。</param>
/// <param name="SpawnEvery">
/// 湧きの間隔（作戦ターン）。<c>S</c>。<b>制圧するまで、この間隔で敵の拠点に1部隊ずつ湧く</b>
/// ——北と南を交互に選ぶ（第176期 §1-2）。
/// </param>
/// <param name="BattleCap">
/// 1 マップの戦闘回数の上限。<b>第175期までの 24 では足りない</b>
/// ——籠城は 60 作戦ターンのあいだ迎撃を繰り返すので、24 で切ると
/// 「湧きに押し切られた」と「戦闘の上限に当たった」が区別できなくなる（§1-4 の膠着）。
/// </param>
/// <param name="WarpEvery">
/// <b>味方のワープのリキャスト（<c>R</c>・第177期 §1）。</b> 制圧した後、拠点どうしのワープは
/// <b>同じポータルを使う</b>ので、1 回使うとこのターン数だけ誰も使えない。
/// <b>湧きと同じ1本のタイマーを共有する</b>——<c>0</c> なら連続で使える。
/// </param>
public readonly record struct PortalRule(bool On, int SpawnEvery, int BattleCap = 120,
                                         int WarpEvery = 3)
{
    /// <summary>敵の拠点なし（第175期の規則そのもの）。</summary>
    public static readonly PortalRule Off = new(false, 0, Map11.BattleCap);

    public static PortalRule Every(int s, int battleCap = 120, int warpEvery = 3)
        => new(true, s, battleCap, warpEvery);

    public override string ToString() => On ? $"S={SpawnEvery} R={WarpEvery}" : "拠点なし";
}

/// <summary>
/// マップ 1-1 の進行。<b>盤面の規則は <see cref="BattleEngine"/> と
/// <see cref="EngagementEngine.CrossBoundary"/> しか呼ばない</b>（判定はこのクラスに1つも無い）。
///
/// <para>遊ぶ側（<c>Map11Main</c>）と頭なしの自己検査（<c>Map11Verify</c>）が
/// <b>同じこのクラスを回す</b>。違うのは「どの隊をどの道へ出すか」を誰が決めるかだけ。</para>
/// </summary>
public sealed class Map11State
{
    public sealed class Squad
    {
        public required Map11.SquadDef Def { get; init; }
        public required int Index { get; init; }
        /// <summary>
        /// 画面に出す隊の名前（第177期 §3）。<b>自分で組んだ通しでは役割の名前が嘘になる</b>
        /// ——「カド隊」にカドがいない編成を組めるので、<b>番号で呼ぶ</b>。
        /// <c>Def.Name</c> は器具（第168期の行名）との突き合わせに残す。
        /// </summary>
        public required string Label { get; set; }
        /// <summary>同・役割の1語（自分で組んだ通しでは出さない）。</summary>
        public required string Role { get; set; }
        /// <summary>盤上に出ている駒。<c>null</c> ＝ まだ出ていない（控え）／全滅した。</summary>
        public List<UnitState>? Units { get; set; }
        public required MapGraph Graph { get; init; }
        /// <summary>
        /// いるマス（第438期・盤 <see cref="MapGraph"/> の Id）。<b>位置の書き手はこれと <see cref="Via"/> だけ</b>
        /// （<see cref="Map11State"/> の <c>Place</c> の1箇所が書く）。拠点にいる ＝ <c>Pos == Graph.Home</c>。
        /// </summary>
        public int Pos { get; internal set; } = MapGraph.HomeId;
        /// <summary>
        /// 経路の外のマス（拠点・敵の拠点）で<b>どの経路にいることになっているか</b>（第438期）。
        /// 敵の拠点では入ってきた経路——1-1 では北と南がそこで合流するので、出る経路と行き先の「（北）／（南）」がこれで決まる
        /// （第437期の <c>Road</c> がそこで担っていた役目）。経路の上のマスでは読まない（そのマスの経路が決める）。
        /// </summary>
        public int Via { get; internal set; } = -1;
        /// <summary>向かっている道（第437期の <c>Road</c>・<b>第438期から派生値</b>）。-1 ＝ 拠点で待機。</summary>
        public int Road => Graph[Pos].Lane >= 0 ? Graph[Pos].Lane : Via;
        /// <summary>
        /// 道の上のマス（第174期・<b>第438期から派生値</b>＝経路の上の位置）。<b>-1 ＝ 拠点</b>で、0 が空きマス、1・2 が敵部隊の初期位置、
        /// 敵の拠点は 3。<b>時間を切った設定では誰も読まない。</b>
        /// </summary>
        public int Cell => Pos == MapGraph.HomeId ? -1 : Graph.IndexOn(Road, Pos);
        /// <summary>
        /// <b>最初に送り出された道</b>（-1 ＝ まだ出していない）。
        /// <c>Cleared</c> はこの道で抜いた数だけを数える——第168期 <c>BandOnce</c> の
        /// <c>clearedBy</c> が「担当の道」で数えているのと同じ（2 本抜き率の分子）。
        /// </summary>
        public int Home { get; set; } = -1;
        public bool Deployed { get; set; }
        public bool Lost { get; set; }
        /// <summary>この隊が<b>自分の担当の道で</b>抜いた敵部隊の数。</summary>
        public int Cleared { get; set; }
        public bool OnMap => Units is not null;
    }

    public sealed class Node
    {
        /// <summary>
        /// 区画の定義。<b>書き換えるのは敵の予備隊の出発（<see cref="ReserveIfDue"/>）の 1 箇所だけ</b>（第441期）——
        /// 待機中の予備隊は経路を持たず（<c>Road = -1</c>）、出発した刻に行き先の経路が入る。
        /// </summary>
        public required Map11.RoadNode Def { get; set; }
        /// <summary>敵の予備隊か（第441期 §3）。湧いた数（<see cref="SpawnCount"/>）にも、湧きを抜いた数にも入れない。</summary>
        public bool IsReserve { get; init; }
        /// <summary>
        /// 敵の拠点から湧いた部隊か（第176期）。<b>最初からいる4部隊は偽。</b>
        /// 2 本抜き（<see cref="Squad.Cleared"/>）の分子に入れないためだけの印で、
        /// 戦闘・回復・勝敗の規則は1つも読まない。
        /// </summary>
        public bool FromPortal { get; init; }
        public List<UnitState>? Units { get; set; }
        public bool Cleared { get; set; }
        public int DefMaxHp { get; init; }
        /// <summary>いまの残 HP（まだ当たっていない部隊は満タン）。</summary>
        public int HpNow { get; set; }
        public required MapGraph Graph { get; init; }
        /// <summary>
        /// いまいるマス（第438期・盤の Id）。初期値は盤の <see cref="MapGraph.FoeStarts"/>（第437期の <see cref="Map11.StartCellOf"/>）。
        /// <b>拠点 ＝ 拠点まで来た</b>（迎撃戦）。<b>時間を切った設定では誰も読まない。</b>
        /// </summary>
        public int Pos { get; set; }
        /// <summary>経路 <c>Def.Road</c> の上の位置（第174期の <c>Cell</c>・<b>第438期から派生値</b>）。<b>-1 ＝ 拠点まで来た</b>。</summary>
        public int Cell => Graph.IndexOn(Def.Road, Pos);
        /// <summary>
        /// この到着の迎撃でもう出した隊（同じ隊が1回の到着で何度も出ないようにするだけ）。
        /// <b>区画ごとに持つ</b>——2 本の道が同じ作戦ターンに拠点へ着くことがある。
        /// </summary>
        public HashSet<int> Tried { get; } = new();
    }

    public int Seed { get; }
    /// <summary>
    /// ステージ（第440期）。経路の数・名前・区画の敵・湧く部隊はここから引く。<b>省けば 1-1（<see cref="MapStage.Stage11"/>）</b>
    /// ——器具と門はすべて 1-1 のまま。
    /// </summary>
    public MapStage Stage { get; }
    /// <summary>盤（第438期）。<b>1-1 は <see cref="MapGraph.Build11"/></b>——敵の拠点のマスは <see cref="PortalRule.On"/> のときだけある。</summary>
    public MapGraph Graph { get; }
    public int Battles { get; private set; }
    public Squad[] Squads { get; }
    public Node[][] Nodes { get; }

    // ---------------- 時間（第174期） ----------------

    /// <summary>時間の規則。<b><c>On</c> が偽なら、このクラスの時間の枝は1行も走らない。</b></summary>
    public TimeRule Time { get; }

    /// <summary>いま何作戦ターン目か（敵が動いた回数）。</summary>
    public int Turn { get; private set; }

    /// <summary>拠点が陥落した（敵が拠点に着いたのに、拠点に隊が1つもいなかった）。</summary>
    public bool Fallen { get; private set; }

    /// <summary>陥落させた敵部隊の道（-1 ＝ 陥落していない）。<b>測るためだけの計数。</b></summary>
    public int FallenRoad { get; private set; } = -1;

    /// <summary>拠点の迎撃戦が起きた回数（測るためだけの計数）。</summary>
    public int Intercepts { get; private set; }

    /// <summary>休んだ回数（測るためだけの計数）。</summary>
    public int Rests { get; private set; }

    // ---------------- 敵の拠点（第176期） ----------------

    /// <summary>敵の拠点の規則。<b><c>On</c> が偽なら、このクラスの拠点の枝は1行も走らない。</b></summary>
    public PortalRule Portal { get; }

    /// <summary>敵の拠点を制圧したか。<b>制圧した時点で湧きが止まる。</b></summary>
    public bool Captured { get; private set; }

    /// <summary>制圧した作戦ターン（していなければ -1）。</summary>
    public int CapturedTurn { get; private set; } = -1;

    /// <summary>湧いた敵部隊（抜いたものも残す——数えるときに <c>Cleared</c> で除く）。
    /// <b>並びは湧いた順＝道の上では後ろの順</b>（列の順序の不変条件）。</summary>
    public List<Node> Spawns { get; } = new();

    /// <summary>湧いた総数（測るためだけの計数）。</summary>
    public int SpawnCount { get; private set; }

    /// <summary>第2拠点が陥落した回数（§1-3 (4)。<b>敵は拠点へ向かってしか動かないので原理的に 0</b>）。</summary>
    public int SecondBaseFalls { get; private set; }

    /// <summary>拠点どうしをワープした回数（第177期。<b>測るためだけの計数</b>）。</summary>
    public int Warps { get; private set; }

    /// <summary>
    /// ポータルが次に使えるようになる作戦ターン（第177期 §1）。
    /// <b>湧きと味方のワープが1本のタイマーを共有する</b>——使った側の値
    /// （<see cref="PortalRule.SpawnEvery"/> ／ <see cref="PortalRule.WarpEvery"/>）だけ塞がる。
    /// <b>初期値は <c>S</c></b>（最初の湧きまで S ターンかかる ＝ 第176期の <c>Turn % S == 0</c> と同じ）。
    /// </summary>
    private int _portalReady;

    /// <summary>ポータルが使えるまでの残り作戦ターン（<b>0 ＝ いま使える</b>）。画面の札が読む。</summary>
    public int PortalCooldown => Portal.On ? Math.Max(0, _portalReady - Turn) : 0;

    /// <summary>ポータルをいま使えるか（湧きも味方のワープもこの1本を見る）。</summary>
    public bool PortalReady => Portal.On && Turn >= _portalReady;

    /// <summary>隊が立てるいちばん奥のマス。<b>拠点なしなら道の行き止まり</b>（第175期のまま）。</summary>
    public int MaxCell => Portal.On ? Map11.PortalCell : Map11.RoadCells - 1;

    /// <summary>1 マップの戦闘回数の上限（拠点ありでは <see cref="PortalRule.BattleCap"/>）。</summary>
    public int BattleCapNow => Portal.On ? Portal.BattleCap : Map11.BattleCap;

    /// <summary>
    /// 控えの駒（第172期 §1-2）。<b>席を持たない駒の置き場</b>で、拠点の隊とだけ行き来できる。
    /// <see cref="Map11.Reserves"/> をそのまま実体化したもの——<b>乱数を1つも引かない</b>ので、
    /// 組み直しを1度もしない通しは第169期と1ビットも違わない（自己検査 (a)）。
    /// </summary>
    public List<UnitState> Bench { get; }

    // ---------------- 途中到着（第441期 §2） ----------------

    /// <summary>まだ届いていない駒（届く順）。<b>到着を切った通しでは空。</b></summary>
    public List<UnitState> Arrivals { get; } = new();
    /// <summary>1 回に届く枚数の並び（<see cref="Arrivals"/> の先頭から順に使う）。</summary>
    private readonly Queue<int> _arrivalBatches = new();
    /// <summary>直前の刻に届いた駒（画面のトーストが読む・届かなかった刻は空）。</summary>
    public List<UnitState> LastArrived { get; } = new();
    /// <summary>届いた総数（測るためだけの計数）。</summary>
    public int ArrivedCount { get; private set; }

    // ---------------- 中間拠点・見返りの増援（第444期 §0-1・§0-2） ----------------

    /// <summary>見返りで届いた駒の記録（届いた順・画面のトーストと結果画面が読む）。時間で届いた駒は入れない。</summary>
    public sealed record Delivery(UnitState Unit, string Place, string Reason);
    /// <summary>見返りで届いた駒（届いた順）。<b>見返りの無い盤では空。</b></summary>
    public List<Delivery> Deliveries { get; } = new();
    /// <summary>盤の中間拠点のマス（<see cref="MapGraph.Kind.Outpost"/>）。<b>無い盤では空</b>——中間拠点の判定は全部ここで抜ける。</summary>
    private readonly int[] _outposts;
    /// <summary>いま味方のものになっている中間拠点のマス。</summary>
    public HashSet<int> HeldOutposts { get; } = new();
    /// <summary>中間拠点を取った回数 ／ 取り返された回数（測るためだけの計数）。</summary>
    public int OutpostCaptures { get; private set; }
    public int OutpostLosses { get; private set; }

    // ---------------- ゴールド（第445期 §0-1） ----------------

    /// <summary>いまの金。<b>金の無い盤では 0 のまま</b>。</summary>
    public int Gold { get; private set; }
    /// <summary>金の出入り（増えた・使った順・画面のトーストと結果画面が読む）。</summary>
    public sealed record GoldEvent(int Amount, string Reason, int Cell = -1);
    public List<GoldEvent> GoldLog { get; } = new();
    /// <summary>呼べる候補（<see cref="GoldRule.Offers"/> 枚・<b>窓を閉じても変わらない</b>・買うとその位置に列の次の 1 枚）。</summary>
    public List<UnitDef> Offers { get; } = new();
    /// <summary>候補の列（現役の駒を seed で並べたもの・戦闘の seed とは別の乱数）。盤上・手札にいる駒は飛ばす。</summary>
    private readonly Queue<UnitDef> _offerQueue = new();
    /// <summary>使える隊の数（第445期: 2 で始まり、枠を買うと 3）。<b>金の無い盤では隊の数そのもの</b>。</summary>
    public int SquadCap { get; private set; }
    /// <summary>まだ枠を買っていない隊か（組み直せず・出せない）。</summary>
    public bool IsLocked(int squadIndex) => squadIndex >= SquadCap;
    /// <summary>呼んだ枚数 ／ 売った枚数（測るためだけの計数）。</summary>
    public int Recruits { get; private set; }
    public int Sold { get; private set; }
    /// <summary>リロールした回数（第446期）。</summary>
    public int Rerolls { get; private set; }
    /// <summary>無料で呼べる残り（第446期: 枠を買うと 1）。<b>残っているあいだは呼ぶ値段が 0</b>。</summary>
    public int FreePicks { get; private set; }
    /// <summary>
    /// 倒れた駒（第447期・倒れた順）。<b>呼び戻しのある盤（<see cref="GoldRule.Revive"/> ＞ 0）だけ</b>記録する。
    /// 戦闘の境界で倒れた駒は隊から外れる（`EngagementEngine.CrossBoundary` は生き残りだけを返す）ので、ここが唯一の記録。召喚された駒は入れない。
    /// </summary>
    public List<UnitState> FallenUnits { get; } = new();
    /// <summary>呼び戻した枚数（測るためだけの計数）。</summary>
    public int Revives { get; private set; }
    private static readonly HashSet<string> RosterIds = UnitCatalog.All.Select(d => d.Id).ToHashSet();
    /// <summary>開始の手札の引きで保証 3 条件を満たせたか（満たせなければ結果画面に印）。到着を切った通しでは真。</summary>
    public bool OpeningOk { get; } = true;
    /// <summary>開始の手札の引き直しの回数。</summary>
    public int OpeningRerolls { get; }

    // ---------------- 敵の予備隊（第441期 §3） ----------------

    /// <summary>敵の予備隊（いなければ null）。<b>出発するまでは敵の拠点のマスで待ち、どの経路にも属さない</b>（<c>Def.Road = -1</c>）。</summary>
    public Node? Reserve { get; }
    /// <summary>予備隊が出発した作戦ターン（-1 ＝ まだ）。</summary>
    public int ReserveDepartTurn { get; private set; } = -1;
    /// <summary>予備隊は敵の拠点で待機中か。</summary>
    public bool ReserveWaiting => Reserve is { Cleared: false } r && r.Def.Road < 0;

    /// <summary>
    /// <b>自分で組む通しか</b>（第177期 §3）。真なら<b>3 隊は空席で始まり、15 枚が手札に載る</b>
    /// ——控えの 7 枚（<see cref="Map11.Reserves"/>）はこの期では配らない。
    /// <b>偽なら第176期と1ビットも違わない。</b>
    /// </summary>
    public bool Draft { get; }

    /// <param name="graph">
    /// 盤（第438期）。<b>省けば 1-1（<see cref="MapGraph.Build11"/>）</b>。渡す盤は 1-1 と同じ経路の数・長さ・敵の初期位置を持つこと
    /// （区画の定義 <see cref="Map11.Roads"/> は 1-1 のまま）——いまは玩具の盤（`--map11-graph-smoke`）だけが渡す。
    /// </param>
    /// <param name="stage">ステージ（第440期）。<b>省けば 1-1</b>。盤を渡さなければ盤もこのステージのもの。</param>
    public Map11State(int seed, TimeRule? time = null, PortalRule? portal = null, bool draft = false, MapGraph? graph = null,
                      MapStage? stage = null)
    {
        Seed = seed;
        Time = time ?? TimeRule.Off;
        Portal = portal ?? PortalRule.Off;
        Draft = draft;
        Stage = stage ?? MapStage.Stage11;
        Graph = graph ?? Stage.Board(Portal.On);
        _outposts = Graph.Cells.Where(c => c.Kind == MapGraph.Kind.Outpost).Select(c => c.Id).ToArray();
        // 最初の1回まで S ターン（第176期の `Turn % S == 0` と同じ立ち上がり）。
        _portalReady = Portal.On ? Math.Max(0, Portal.SpawnEvery) : 0;
        // 第177期 §3: 自分で組む通しでは、**3 隊の 15 枚をばらして手札にする**。
        if (draft && Stage.Arrival.On)
        {
            // 第441期 §2: **開始は 10 枚**。残りは到着の列に並ぶ（どれも seed で引く・戦闘の seed とは別の乱数）。
            MapDraw.Opening o = MapDraw.Draw(seed, Stage.Arrival.Start, Stage.Arrival.MinPer, Stage.Arrival.MaxPer);
            Bench = o.Start.Select(One).ToList();
            Arrivals.AddRange(o.Queue.Select(One));
            foreach (int k in o.Batches) _arrivalBatches.Enqueue(k);
            OpeningOk = o.Ok;
            OpeningRerolls = o.Rerolls;
            // 第445期: 後から届かない規則（開始の引きだけ）なら、残りは捨てる——ゴールドで呼ぶ候補に戻る。
            if (!Stage.Arrival.Delivers) { Arrivals.Clear(); _arrivalBatches.Clear(); }
        }
        else
            Bench = (draft
                    ? Map11.Squads.SelectMany(s => s.F.Occupied().Select(o => o.Def))
                    : Map11.Reserves.AsEnumerable())
                .Select(One)
                .ToList();
        // 第442期 §0-3: 隊の数はステージが持つ（`SquadCap`・1-1 は 3 のまま）。後ろの隊は作らない。
        // 第447期: 隊の枠が 4 つの盤は 4 隊目の定義（空）を足す。
        Squads = Map11.Squads.Append(Map11.ExtraSquad).Take(Stage.SquadCap).Select((d, i) => new Squad
        {
            Def = d,
            Graph = Graph,
            Index = i,
            Label = draft ? $"第{i + 1}隊" : d.Name,
            Role = draft ? "自分で組んだ隊" : d.Role,
            // **空席で始める**（`null` ＝ 「定義のまま」なので、空のリストを置く）。
            Units = draft ? new List<UnitState>() : null,
        }).ToArray();
        SquadCap = Stage.Gold.On ? Math.Min(Squads.Length, Stage.Gold.StartSquads) : Squads.Length;
        Gold = Stage.Gold.On ? Stage.Gold.Start : 0;
        Nodes = Stage.Lanes.Select(r => r.Select(n =>
        {
            int hp = n.Enemy.Occupied().Sum(o => o.Def.MaxHp);
            return new Node { Def = n, Graph = Graph, DefMaxHp = hp, HpNow = hp, Pos = Graph.FoeStarts[n.Road][n.Index] };
        }).ToArray()).ToArray();
        // 第441期 §3: 敵の予備隊は**敵の拠点のマス**で始まる（敵の拠点が無い設定では置かない）。
        if (Stage.Reserve is { On: true, Enemy: { } re } rr && Portal.On && Graph.HasPortal)
        {
            int hp = re.Occupied().Sum(o => o.Def.MaxHp);
            Reserve = new Node
            {
                Def = new Map11.RoadNode(-1, Map11.RoadCells, rr.Name, re, rr.StageIndex),
                Graph = Graph, FromPortal = true, IsReserve = true, DefMaxHp = hp, HpNow = hp, Pos = Graph.Portal,
            };
        }
        // 第445期 §0-1: 呼ぶ候補の列（現役 52 枚を seed で並べる）と最初の候補。
        if (Stage.Gold.On)
        {
            var rng = new Random(unchecked(seed * 7919 + 445));
            UnitDef[] pool = UnitCatalog.All.ToArray();
            for (int i = pool.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            foreach (UnitDef d in pool) _offerQueue.Enqueue(d);
            for (int k = 0; k < Stage.Gold.Offers; k++) if (NextOffer() is { } d) Offers.Add(d);
        }
    }

    /// <summary>その駒（の定義）がいま盤上（どの隊にでも・倒れていても）か手札にいるか。<b>同じキャラは盤上に 1 体まで</b>（第445期）。</summary>
    public bool OnBoardOrHand(UnitDef d)
        => Stage.Gold.Revive > 0
            // 第447期: 呼び戻しのある盤では、盤上と手札は<b>生きている駒だけ</b>を数え、倒れた駒は呼び戻しの口（<see cref="FallenUnits"/>）にいる——候補には出ない。
            ? AliveAnywhere(d) || FallenUnits.Any(u => u.Def.Id == d.Id)
            : Bench.Any(u => u.Def.Id == d.Id)
              || Squads.Any(q => q.Units is { } u ? u.Any(x => x.Def.Id == d.Id) : q.Def.F.Occupied().Any(o => o.Def.Id == d.Id));

    /// <summary>その駒が生きて盤上（どの隊でも）か手札にいるか（第447期）。</summary>
    public bool AliveAnywhere(UnitDef d)
        => Bench.Any(u => u.IsAlive && u.Def.Id == d.Id)
           || Squads.Any(q => q.Units is { } u ? u.Any(x => x.IsAlive && x.Def.Id == d.Id) : !q.Lost && q.Def.F.Occupied().Any(o => o.Def.Id == d.Id));

    /// <summary>呼び戻せるか（倒れた駒の <paramref name="index"/> 番目）。同じキャラが生きていれば戻せない。</summary>
    public bool CanRevive(int index)
        => CanTrade && Stage.Gold.Revive > 0 && index >= 0 && index < FallenUnits.Count && Gold >= Stage.Gold.Revive
           && !AliveAnywhere(FallenUnits[index].Def);

    /// <summary>倒れた駒を呼び戻す（第447期 §0-1）。HP は最大の <see cref="GoldRule.RevivePercent"/>% で手札に入る。候補の列には混ぜない。</summary>
    public bool Revive(int index)
    {
        if (!CanRevive(index)) return false;
        UnitState u = FallenUnits[index];
        Spend(Stage.Gold.Revive, $"{u.Def.Name}を呼び戻した");
        FallenUnits.RemoveAt(index);
        foreach (Squad q in Squads) q.Units?.Remove(u);
        Bench.Remove(u);
        u.Hp = Math.Max(1, u.MaxHp * Stage.Gold.RevivePercent / 100);
        Bench.Add(u);
        Revives++;
        return true;
    }

    /// <summary>候補の列から、盤上・手札・候補にいない次の 1 枚（尽きたら null）。飛ばした駒は列に戻さない。</summary>
    private UnitDef? NextOffer()
    {
        while (_offerQueue.Count > 0)
        {
            UnitDef d = _offerQueue.Dequeue();
            if (!OnBoardOrHand(d) && Offers.All(o => o.Id != d.Id)) return d;
        }
        return null;
    }

    /// <summary>金を動かせるか（呼ぶ・枠・売る）。<b>拠点・取った中間拠点・第2拠点にいる隊がいて、組み直しの窓が開けるとき</b>（迎撃の合間も含む）。</summary>
    public bool CanTrade => Stage.Gold.On && !Finished && Squads.Any(q => CanReform(q.Index));

    /// <summary>呼べるか（候補 <paramref name="offer"/> 番目）。</summary>
    public bool CanRecruit(int offer) => CanTrade && offer >= 0 && offer < Offers.Count && (FreePicks > 0 || Gold >= Stage.Gold.Recruit);

    /// <summary>呼ぶ（第445期 §0-1）。候補の駒を手札に入れ、その位置に列の次の 1 枚を補充する。</summary>
    public bool Recruit(int offer)
    {
        if (!CanRecruit(offer)) return false;
        UnitDef d = Offers[offer];
        // 第446期: 枠の特典の 1 枚は無料（金の出入りには載せない）。
        if (FreePicks > 0) FreePicks--;
        else Spend(Stage.Gold.Recruit, $"{d.Name}を呼んだ");
        Bench.Add(One(d));
        Recruits++;
        Offers.RemoveAt(offer);
        if (NextOffer() is { } next) Offers.Insert(offer, next);
        return true;
    }

    /// <summary>次の枠の値段（第447期: 枠を 1 つ買うごとに <see cref="GoldRule.SlotStep"/> 上がる）。</summary>
    public int SlotPrice => SlotPriceFor(SquadCap);

    /// <summary>隊 <paramref name="squadIndex"/>（0 から）の枠の値段（画面の閉じた隊の札が読む）。</summary>
    public int SlotPriceFor(int squadIndex) => Stage.Gold.Slot + Math.Max(0, squadIndex - Stage.Gold.StartSquads) * Stage.Gold.SlotStep;

    /// <summary>次の隊の枠を買えるか。</summary>
    public bool CanBuySlot => CanTrade && SquadCap < Squads.Length && Gold >= SlotPrice;

    /// <summary>隊の枠を 1 つ開ける（第445期 §0-1・2 → 3）。</summary>
    public bool BuySlot()
    {
        if (!CanBuySlot) return false;
        Spend(SlotPrice, $"{SquadCap + 1}隊目の枠を開けた");
        SquadCap++;
        // 第446期: 買うとその場で候補から 1 枚を無料で選べる（「枠を先に買うと空のまま」を規則で潰す）。
        if (Stage.Gold.SlotFreePick) FreePicks++;
        return true;
    }

    /// <summary>リロールできるか（第446期・値段 0 の盤では無い）。</summary>
    public bool CanReroll => CanTrade && Stage.Gold.Reroll > 0 && Gold >= Stage.Gold.Reroll && Offers.Count > 0;

    /// <summary>
    /// 候補を引き直す（第446期）。列の次から、盤上・手札にいない駒を候補の枚数だけ引き、<b>捨てた候補は列の最後へ戻す</b>
    /// （回数の上限が無いので、戻さないと列が尽きる）。乱数は引かない（列は最初に並べたまま）。
    /// </summary>
    public bool Reroll()
    {
        if (!CanReroll) return false;
        Spend(Stage.Gold.Reroll, "候補を引き直した");
        var old = Offers.ToList();
        Offers.Clear();
        for (int k = 0; k < Stage.Gold.Offers; k++) if (NextOffer() is { } d) Offers.Add(d);
        foreach (UnitDef d in old) _offerQueue.Enqueue(d);
        // 列の残りが候補の枚数に満たなかったときは、戻した駒から足りないぶんを補う。
        while (Offers.Count < Stage.Gold.Offers && NextOffer() is { } more) Offers.Add(more);
        Rerolls++;
        return true;
    }

    /// <summary>売れるか（手札の <paramref name="benchIndex"/> 番目・生きている駒だけ）。</summary>
    public bool CanSell(int benchIndex) => CanTrade && benchIndex >= 0 && benchIndex < Bench.Count && Bench[benchIndex].IsAlive;

    /// <summary>控えの駒を手放す（第445期 §0-1・盤上の隊からは売れない）。手放した駒は列には戻らない。</summary>
    public bool Sell(int benchIndex)
    {
        if (!CanSell(benchIndex)) return false;
        UnitState u = Bench[benchIndex];
        Bench.RemoveAt(benchIndex);
        Earn(Stage.Gold.Sell, $"{u.Def.Name}を手放した");
        Sold++;
        return true;
    }

    private void Earn(int amount, string reason, int cell = -1)
    {
        if (!Stage.Gold.On || amount <= 0) return;
        Gold += amount;
        GoldLog.Add(new GoldEvent(amount, reason, cell));
    }

    private void Spend(int amount, string reason)
    {
        Gold -= amount;
        GoldLog.Add(new GoldEvent(-amount, reason));
    }

    /// <summary>1 枚を盤に立てられる形にする（開始の手札・呼んだ駒）。</summary>
    private static UnitState One(UnitDef d) => BattleEngine.Materialize(Formation.Build(front1: d), BattleContext.PlayerTeam)[0];

    public int ClearedCount => Nodes.Sum(r => r.Count(n => n.Cleared));
    public bool AllCleared => ClearedCount >= Stage.TotalNodes;

    /// <summary>出せる隊が1つも残っていない（盤上に居らず、かつ出していないわけでもない）。</summary>
    public bool NoSquadsLeft => Squads.All(s => s.Units is null && (s.Lost || s.Deployed));

    /// <summary>盤上に敵部隊が1つでも残っているか（<b>湧いたものも数える</b>）。</summary>
    public bool FoesLeft => Nodes.Any(r => r.Any(n => !n.Cleared)) || Spawns.Any(n => !n.Cleared) || ReserveWaiting;

    /// <summary>経路の名前（<b>-1 は敵の拠点</b>——待機中の予備隊は経路を持たない・第441期）。</summary>
    public string LaneName(int road) => road < 0 ? Map11.PortalName : Stage.LaneNames[road];

    public bool Finished => (Portal.On ? Won : AllCleared) || NoSquadsLeft || Battles >= BattleCapNow
                            || Fallen || (Time.On && Turn >= Stage.TurnCap);

    /// <summary>
    /// 勇者パーティーが着いて負けた（第444期 §0-3）。<b>上限を勇者の予告にした盤（<see cref="MapStage.HeroAtCap"/>）で、
    /// 刻の上限に達したときに勝っていなければ真。</b>勇者そのものはまだ盤に出ない（段C）。
    /// </summary>
    public bool HeroArrived => Stage.HeroAtCap && Time.On && Turn >= Stage.TurnCap && !Won && !Fallen;

    /// <summary>勇者パーティーが現れるまでの刻（予告の札「あと n 刻」）。予告の無い盤では null。</summary>
    public int? HeroTurnsLeft => Stage.HeroAtCap && Time.On ? Math.Max(0, Stage.TurnCap - Turn) : null;

    /// <summary>「n 作戦ターン以内に制圧」の残り（第447期・勝ち条件の文を出す盤だけ）。</summary>
    public int? TimeLimitLeft => Stage.TimeLimit && Time.On ? Math.Max(0, Stage.TurnCap - Turn) : null;

    /// <summary>時間切れで負けた（第447期・勝ち条件の文を出す盤で、上限の刻に勝っていない）。</summary>
    public bool TimeUp => Stage.TimeLimit && Time.On && Turn >= Stage.TurnCap && !Won && !Fallen;

    /// <summary>
    /// 勝ち。<b>拠点ありでは「制圧した ＋ 盤上の敵部隊が 0」</b>（第176期 §1-4）
    /// ——道を抜くだけでは終わらない。拠点なしでは第175期のまま。
    /// </summary>
    public bool Won => Portal.On ? (Captured && (Stage.WinOnCapture || !FoesLeft) && !Fallen) : (AllCleared && !Fallen);

    /// <summary>盤に残っている敵部隊の数（第445期: 制圧で勝ったとき「退いた敵部隊」として結果に出す）。</summary>
    public int FoesRemaining => Nodes.Sum(r => r.Count(n => !n.Cleared)) + Spawns.Count(n => !n.Cleared) + (ReserveWaiting ? 1 : 0);

    /// <summary>道 <paramref name="road"/> で次に当たる区画。道が抜け切っていれば null。</summary>
    public Node? NextNode(int road)
    {
        foreach (Node n in Nodes[road]) if (!n.Cleared) return n;
        return null;
    }

    // ---------------- 道の上の敵（第176期。**拠点なしでは湧きが 0 なので第175期と同義**） ----------------

    /// <summary>
    /// その道に残っている敵部隊を<b>手前（拠点に近い）順</b>に返す。
    /// <b>固定の4部隊は必ず湧きより手前にいる</b>——湧きは敵の拠点から出て、
    /// 前が詰まっていれば進めない（<see cref="AdvanceFoes"/>）ので、この並びが位置の順になる。
    /// </summary>
    public IEnumerable<Node> FoesOn(int road)
    {
        foreach (Node n in Nodes[road]) if (!n.Cleared) yield return n;
        foreach (Node n in Spawns) if (!n.Cleared && n.Def.Road == road) yield return n;
    }

    /// <summary>その道でいちばん拠点に近い敵部隊。<b>拠点なしでは <see cref="NextNode"/> と同じもの。</b></summary>
    public Node? FrontFoe(int road) => FoesOn(road).FirstOrDefault();

    /// <summary>そのマスにいる敵部隊（道 <paramref name="road"/> の手前の順で最初の1つ）。</summary>
    public Node? FoeAt(int road, int cellId) => FoesOn(road).FirstOrDefault(n => n.Pos == cellId);

    /// <summary>敵の拠点のマスにいる敵部隊（<b>道をまたいで数える</b>——北と南はそこで合流する）。</summary>
    public Node? PortalFoe()
    {
        if (!Portal.On) return null;
        for (int road = 0; road < Stage.LaneCount; road++)
            foreach (Node n in FoesOn(road))
                if (n.Pos == Graph.Portal) return n;
        // 第441期 §3: 待機中の予備隊も敵の拠点にいる（制圧するには先に倒すか、出発させてから入る）。
        return ReserveWaiting ? Reserve : null;
    }

    /// <summary>
    /// その隊がいま当たっている敵部隊（<b>同じマスにいる相手</b>）。いなければ null。
    /// <b>拠点なしでは「道の先頭の敵が同じマスにいるか」そのもの</b>（第175期の <c>Advance</c> の式）。
    /// </summary>
    public Node? FoeFacing(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Units is null || s.Road < 0) return null;
        if (Portal.On && s.Pos == Graph.Portal) return PortalFoe();
        Node? f = FrontFoe(s.Road);
        return f is not null && f.Pos == s.Pos ? f : null;
    }

    /// <summary>
    /// その道へ向かう意味があるか。<b>拠点なしでは「抜け切っていない道だけ」</b>（第175期）で、
    /// 拠点ありでは<b>敵が残っていなくても奥へ行ける</b>（その先に敵の拠点がある）。
    /// </summary>
    public bool CanHead(int road) => Portal.On || NextNode(road) is not null;

    /// <summary>
    /// 制圧の判定（<b>ここ1箇所だけ</b>）。敵の拠点のマスに味方がいて、そこに敵部隊が1つも無ければ制圧。
    /// <b>制圧した時点で湧きが止まる</b>（<see cref="SpawnIfDue"/> が <c>Captured</c> を見る）。
    /// </summary>
    private void TryCapture()
    {
        if (!Portal.On || Captured || PortalFoe() is not null) return;
        foreach (Squad s in Squads)
            if (s.Units is not null && s.Pos == Graph.Portal && s.Units.Any(u => u.IsAlive))
            {
                Captured = true;
                CapturedTurn = Turn;
                // 第177期 §1: **制圧した時点でポータルは味方のものになる**ので、
                // リキャストは味方の <see cref="PortalRule.WarpEvery"/> から数え直す
                // ——敵の湧きの残り（最大 S ターン）を引き継がない。
                // <b>制圧は「使う」ことではない</b>ので、その場で1回は使える。
                _portalReady = Turn;
                return;
            }
    }

    /// <summary>その隊は制圧した第2拠点にいるか（<b>休む・組み直しができる</b>）。</summary>
    public bool AtSecondBase(int squadIndex)
        => Captured && Squads[squadIndex].Units is not null && Squads[squadIndex].Pos == Graph.Portal;

    /// <summary>その隊は味方のものになった中間拠点にいるか（第444期 §0-1・<b>休む・組み直し・届いた駒を受け取るができる</b>）。</summary>
    public bool AtOutpost(int squadIndex)
        => _outposts.Length > 0 && Squads[squadIndex].Units is not null && HeldOutposts.Contains(Squads[squadIndex].Pos);

    /// <summary>
    /// 拠点の外で、拠点と同じことができる場所にいるか（第2拠点か、取った中間拠点）。
    /// <b>ワープ（<see cref="CanWarp"/>）と第2拠点の明け渡しは第2拠点だけ</b>——中間拠点は経路の途中のマスなので飛べない。
    /// </summary>
    public bool AtForwardBase(int squadIndex) => AtSecondBase(squadIndex) || AtOutpost(squadIndex);

    /// <summary>
    /// 中間拠点の取り合い（第444期 §0-1・<b>ここ1箇所だけ</b>）。<see cref="TryCapture"/> と <see cref="LoseSecondBaseIfEmpty"/> を中間拠点に広げたもの:
    /// <b>味方の隊（生きている駒あり）がいて敵部隊がいなければ取る</b>（見返りに <see cref="ArrivalRule.OnOutpost"/> 枚）／
    /// <b>敵部隊がいて出せる隊がいなければ取り返される</b>。隊の位置が動くたび（<see cref="Place"/>）・戦闘の精算・敵の前進の後に呼ぶ。
    /// </summary>
    private void UpdateOutposts()
    {
        if (_outposts.Length == 0 || Nodes is null) return;
        foreach (int cell in _outposts)
        {
            int lane = Graph[cell].Lane;
            bool foe = FoesOn(lane).Any(n => n.Pos == cell);
            int mine = -1;
            for (int i = 0; i < Squads.Length; i++)
                if (!Squads[i].Lost && Squads[i].Units is { } u && Squads[i].Pos == cell && u.Any(x => x.IsAlive)) { mine = i; break; }
            if (!HeldOutposts.Contains(cell))
            {
                if (mine < 0 || foe) continue;
                HeldOutposts.Add(cell);
                OutpostCaptures++;
                Deliver(Stage.Arrival.OnOutpost, Graph[cell].Name, $"{Graph[cell].Name}を取った");
                Earn(Stage.Gold.Outpost, $"{Graph[cell].Name}を取った", cell);
            }
            else if (foe && mine < 0)
            {
                HeldOutposts.Remove(cell);
                OutpostLosses++;
            }
        }
    }

    /// <summary>
    /// 見返りの増援（第444期 §0-2）。到着の列の先頭から <paramref name="count"/> 枚を手札（<see cref="Bench"/>）へ。
    /// <b>手札は拠点・第2拠点・中間拠点で共有</b>（第2拠点と同じ扱い）——<paramref name="place"/> は記録と画面の文のためだけ。
    /// </summary>
    private void Deliver(int count, string place, string reason)
    {
        for (int k = 0; k < count && Arrivals.Count > 0 && !Fallen; k++)
        {
            UnitState u = Arrivals[0];
            Arrivals.RemoveAt(0);
            Bench.Add(u);
            ArrivedCount++;
            Deliveries.Add(new Delivery(u, place, reason));
        }
    }

    /// <summary>見返りの届く先の名前（<b>その隊がいる拠点</b>・道の上や迎撃なら味方の拠点）。</summary>
    private string DeliveryPlace(int squadIndex)
        => AtOutpost(squadIndex) ? Graph[Squads[squadIndex].Pos].Name
         : AtSecondBase(squadIndex) ? Map11.PortalName
         : "拠点";

    /// <summary>出撃させる（拠点 → 道）。控えはここで初めて盤上に出る。</summary>
    public void Send(int squadIndex, int road)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost) return;
        if (!CanSend(squadIndex)) return;
        if (s.Home < 0) s.Home = road;
        // 組み直しで先に実体化していることがあるので `??=`。**第169期と同値**
        // ——あの版で「`Units` が null でなく `Deployed` が偽」になる道は1本も無かった。
        s.Units ??= BattleEngine.Materialize(s.Def.F, BattleContext.PlayerTeam);
        s.Deployed = true;
        // 第174期: 拠点から出たら道の先頭のマス（空きマス）に立つ。
        // **時間を切った設定では誰も読まない**ので、第169期と1ビットも違わない。
        // 道の上から呼ばれたら（時間なしの器具が隊を別の道へ回す）、同じ番号のまま道だけ替える（第437期の `Road = road`）。
        Place(s, Graph.At(road, Math.Max(s.Cell, 0)), road);
    }

    /// <summary>
    /// 隊の位置を書く<b>ただ1つの口</b>（第438期）。<paramref name="via"/> は経路の外のマスで
    /// どの経路にいることになるか（拠点へ戻るなら -1・敵の拠点なら入ってきた経路）。
    /// </summary>
    private void Place(Squad s, int pos, int via)
    {
        s.Pos = pos;
        s.Via = Graph[pos].Lane >= 0 ? -1 : via;
        UpdateOutposts();
    }

    /// <summary>その隊は行き先に着いているか（敵の拠点なら、行き先の経路から入ったか）。<b>盤面は1ビットも動かさない。</b></summary>
    public bool AtDest(int squadIndex, Dest dest)
    {
        Squad s = Squads[squadIndex];
        return dest.AtHome ? s.Pos == Graph.Home : s.Pos == dest.Id && s.Road == dest.Road;
    }

    /// <summary>
    /// 道の上の隊を拠点へ戻す（第173期 §1-2）。<b>戻すこと自体に代金は無い</b>
    /// ——HP も死者もそのままで、戻った隊は拠点で組み直せる（時間の代金は次の期の話）。
    ///
    /// <para><b>ここに置くのは「不具合の直し」である。</b> 第172期まで引き返す口は
    /// 接敵の窓（`Map11Main` の「拠点へ引き返す」）1つしか無く、その窓は
    /// <see cref="NextNode"/> が null——<b>つまり道が抜け切った瞬間</b>——に開かなくなる。
    /// 担当の道を抜き切った隊は <c>Road</c> が立ったまま拠点へ戻れず、
    /// <see cref="CanReform"/> が偽のままなので<b>二度と組み直せなくなっていた</b>。</para>
    ///
    /// <para><b>盤面の規則は1つも触らない</b>——書き換えるのは <c>Road</c> だけで、
    /// <c>Units</c> / <c>Home</c> / <c>Cleared</c> / <c>Deployed</c> は1ビットも動かさない
    /// （<c>Home</c> を動かすと 2 本抜きの分子が変わる）。</para>
    /// </summary>
    public bool Withdraw(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is null || s.Road < 0) return false;
        Place(s, Graph.Home, -1);
        return true;
    }

    /// <summary>その隊を拠点へ戻せるか（道の上に出ている隊だけ）。</summary>
    public bool CanWithdraw(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        return !s.Lost && s.Units is not null && s.Road >= 0;
    }

    // =============================================================================
    // 拠点での組み直し（第172期 部A）
    //
    // **戦闘・回復・勝敗の規則は1行も触っていない。** ここでするのは
    // 「拠点にいる隊の `UnitState` を、どの席に置くか」の書き換えだけ。
    // 組み直しを1度もしなければ第169期と1ビットも違わない（自己検査 (a)）。
    // =============================================================================

    /// <summary>その隊を組み直せるか（<b>拠点にいる隊だけ</b>。道の上と全滅した隊は不可）。</summary>
    public bool CanReform(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        return !s.Lost && !IsLocked(squadIndex) && (s.Road < 0 || AtForwardBase(squadIndex));
    }

    /// <summary>出せるか。<b>0 枚の隊は出せない</b>（指示書 §1-1）。</summary>
    public bool CanSend(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost || IsLocked(squadIndex)) return false;
        return s.Units is not { } u || u.Any(x => x.IsAlive);
    }

    /// <summary>
    /// 拠点の隊の駒を、動かせる形（<see cref="UnitState"/> のリスト）にする。
    /// <b>触ったときだけ実体化する</b>——触らない隊は <c>Units</c> が null のままで、
    /// <see cref="Send"/> がこれまでどおり定義から作る。
    /// </summary>
    private List<UnitState> Roster(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        return s.Units ??= BattleEngine.Materialize(s.Def.F, BattleContext.PlayerTeam);
    }

    /// <summary>席の昇順に整える。<b>engine は渡した並びで <c>InstanceId</c> を振る</b>ので、
    /// <c>Materialize</c> / <c>CrossBoundary</c> と同じ並びに保つ。</summary>
    private static void SortBySlot(List<UnitState> units) => units.Sort((a, b) => a.Slot.CompareTo(b.Slot));

    /// <summary>
    /// 組み直しのために席の駒をつまむ。<b>拠点の隊なら、まだ実体化していなくてもここで実体化する</b>
    /// ——定義のままの隊（`Units` が null）でも 1 枚目からつまめるようにするため。
    /// </summary>
    public UnitState? PickUp(int squadIndex, int slot)
        => CanReform(squadIndex)
            ? Roster(squadIndex).FirstOrDefault(u => u.Slot == slot)
            : UnitAt(squadIndex, slot);

    /// <summary>その席に立っている駒（空席なら null）。<b>読むだけ</b>——実体化はしない。</summary>
    public UnitState? UnitAt(int squadIndex, int slot)
    {
        Squad s = Squads[squadIndex];
        if (s.Units is { } u) return u.FirstOrDefault(x => x.Slot == slot);
        return null;
    }

    /// <summary>
    /// 席どうしを入れ替える（<b>同じ隊の中も、拠点にいる隊どうしも同じ操作</b>）。
    /// 片方が空席なら移動になる。どちらも拠点にいないと何もしない。
    /// </summary>
    public bool SwapSeats(int squadA, int slotA, int squadB, int slotB)
    {
        if (!CanReform(squadA) || !CanReform(squadB)) return false;
        if (squadA == squadB && slotA == slotB) return false;
        List<UnitState> a = Roster(squadA), b = Roster(squadB);
        UnitState? ua = a.FirstOrDefault(u => u.Slot == slotA);
        UnitState? ub = b.FirstOrDefault(u => u.Slot == slotB);
        if (ua is null && ub is null) return false;
        if (ua is not null) a.Remove(ua);
        if (ub is not null) b.Remove(ub);
        if (ua is not null) { ua.Slot = slotB; b.Add(ua); }
        if (ub is not null) { ub.Slot = slotA; a.Add(ub); }
        SortBySlot(a);
        if (!ReferenceEquals(a, b)) SortBySlot(b);
        return true;
    }

    /// <summary>
    /// 席と控えの駒を入れ替える。<paramref name="benchIndex"/> が範囲外なら
    /// 「その駒を控えへ下げる」（席が空く）。席が空なら「控えの駒をそこへ入れる」。
    /// </summary>
    public bool SwapWithBench(int squadIndex, int slot, int benchIndex)
    {
        if (!CanReform(squadIndex)) return false;
        List<UnitState> a = Roster(squadIndex);
        UnitState? seat = a.FirstOrDefault(u => u.Slot == slot);
        UnitState? bench = benchIndex >= 0 && benchIndex < Bench.Count ? Bench[benchIndex] : null;
        if (seat is null && bench is null) return false;
        int at = bench is null ? Bench.Count : benchIndex;
        if (seat is not null) a.Remove(seat);
        if (bench is not null) { Bench.RemoveAt(benchIndex); bench.Slot = slot; a.Add(bench); }
        if (seat is not null) Bench.Insert(Math.Min(at, Bench.Count), seat);
        SortBySlot(a);
        return true;
    }

    /// <summary>
    /// その隊を既定の編成へ戻す（指示書 §1-3 の「元に戻す」）。
    /// <b>拠点にある駒からしか集められない</b>——道の上・全滅した隊にいる駒は戻せないので、
    /// 1 枚でも欠けていれば何もしない（<c>false</c> を返す）。
    /// 押し出された駒は控えへ下がる。
    /// </summary>
    public bool ResetSquad(int squadIndex)
    {
        if (!CanReform(squadIndex)) return false;
        var want = Squads[squadIndex].Def.F.Occupied().ToArray();

        // 拠点にある駒を全部集める（どの隊のどの席にいるか／控えの何番目か）。
        UnitState? Find(string id)
        {
            for (int s = 0; s < Squads.Length; s++)
                if (CanReform(s) && Squads[s].Units is { } u
                    && u.FirstOrDefault(x => x.Def.Id == id) is { } hit) return hit;
            return Bench.FirstOrDefault(x => x.Def.Id == id);
        }

        var found = want.Select(w => Find(w.Def.Id)).ToArray();
        if (found.Any(f => f is null)) return false;

        // いったん全部を控えへ引き上げてから、定義どおりに置き直す。
        for (int s = 0; s < Squads.Length; s++)
            if (CanReform(s) && Squads[s].Units is { } u)
                foreach (UnitState x in u.ToList())
                    if (found.Contains(x) || s == squadIndex) { u.Remove(x); Bench.Add(x); }
        foreach (UnitState f in found!) Bench.Remove(f!);

        var target = Roster(squadIndex);
        for (int i = 0; i < want.Length; i++) { found[i]!.Slot = want[i].Slot; target.Add(found[i]!); }
        SortBySlot(target);
        for (int s = 0; s < Squads.Length; s++)
            if (Squads[s].Units is { } u) SortBySlot(u);
        return true;
    }

    /// <summary>
    /// 「既定の3隊で始める」（第177期 §3）——<b>手札の 15 枚を定義どおりに配り直す。</b>
    /// 面倒なときの逃げ道であり、<b>回帰確認の口</b>でもある（既定の3隊で始めた通しは
    /// 第176期までと同じ編成になる）。1 枚でも拠点に無ければ何もしない。
    /// </summary>
    public bool UseDefaultSquads()
    {
        // 第445期: まだ枠を買っていない隊は数えない（配る先にもならない）。
        if (Squads.Any(s => !IsLocked(s.Index) && (s.Lost || !CanReform(s.Index)))) return false;
        // 第442期 §0-2: **到着のある盤では、拠点にある駒（開始の 10 枚）だけを配る**——まだ届いていない駒は列に残す
        // （第441期は列を配り切っていたので、既定で始めると途中到着が 1 度も起きなかった）。
        if (Draft && Stage.Arrival.On) return DealDefaultFromHome();
        for (int i = 0; i < Squads.Length; i++)
            if (!ResetSquad(i)) return false;
        // 名前も既定へ戻す（「第1隊」のままだと、中身と看板がまた食い違う）。
        foreach (Squad s in Squads) { s.Label = s.Def.Name; s.Role = s.Def.Role; }
        return true;
    }

    /// <summary>
    /// 既定の組み方で、<b>拠点にある駒だけ</b>を隊へ配る（第442期 §0-2・到着のある盤の「既定」）。
    /// 1. 各隊に、その隊の定義（`Map11.Squads`）の駒が拠点にあれば、定義の席へ
    /// 2. 残った駒は、定義の席が空いている隊の番号の若い方へ。どの隊もその席が埋まっていれば、空いている隊の空いている席の若い方へ
    /// 乱数を引かない。どの隊も 5 席が埋まったら残りは控えに置く。<b>定義どおりの 5 枚がそろった隊だけ</b>名前を既定に戻す。
    /// <b>配るのは拠点の駒で埋まる数の隊だけ</b>（第443期・10 枚なら 2 隊）——隊が 3 つの盤でも 3 隊目は空けておき、届いた駒で組む
    /// （第442期は隊が 2 つだったので、この行が無くても同じ結果）。
    /// </summary>
    /// <summary>
    /// 「既定」で駒を配る隊の数（第443期）。到着のある自分で組む通しでは<b>拠点の駒で埋まる数</b>（10 枚なら 2）、ほかは隊の数。
    /// 画面の札の文字もこれを読む（「既定の組み方で2隊を組む」——隊が 3 つでも 3 隊目は届いた駒で組む）。
    /// </summary>
    public int DefaultSquadsFilled
    {
        get
        {
            if (!(Draft && Stage.Arrival.On)) return Squads.Length;
            int home = Bench.Count + Squads.Where(q => CanReform(q.Index)).Sum(q => q.Units?.Count ?? 0);
            int seats = FormationRules.PlayableSlots.Length;
            return Math.Min(SquadCap, (home + seats - 1) / seats);
        }
    }

    private bool DealDefaultFromHome()
    {
        int fill = DefaultSquadsFilled;
        // いったん全部を控えへ引き上げる（並びは配った順を決めるので、定義の順に揃える）。
        for (int s = 0; s < Squads.Length; s++)
            if (Squads[s].Units is { } u) { Bench.AddRange(u); u.Clear(); }
        var order = Map11.Squads.SelectMany(d => d.F.Occupied().Select(o => o.Def.Id)).ToList();
        Bench.Sort((a, b) => order.IndexOf(a.Def.Id).CompareTo(order.IndexOf(b.Def.Id)));
        int DefSlot(UnitState u)
            => Map11.Squads.SelectMany(d => d.F.Occupied()).First(o => o.Def.Id == u.Def.Id).Slot;
        bool Free(int squad, int slot) => Roster(squad).All(x => x.Slot != slot);
        void Put(UnitState u, int squad, int slot) { Bench.Remove(u); u.Slot = slot; Roster(squad).Add(u); }

        for (int i = 0; i < fill; i++)
            foreach (var want in Squads[i].Def.F.Occupied())
                if (Bench.FirstOrDefault(x => x.Def.Id == want.Def.Id) is { } hit && Free(i, want.Slot))
                    Put(hit, i, want.Slot);
        foreach (UnitState u in Bench.ToList())
        {
            int slot = DefSlot(u);
            int squad = Enumerable.Range(0, fill).FirstOrDefault(k => Roster(k).Count < 5 && Free(k, slot), -1);
            if (squad < 0)
            {
                squad = Enumerable.Range(0, fill).FirstOrDefault(k => Roster(k).Count < 5, -1);
                if (squad < 0) break;
                slot = FormationRules.PlayableSlots.First(sl => Free(squad, sl));
            }
            Put(u, squad, slot);
        }
        foreach (Squad sq in Squads)
        {
            SortBySlot(Roster(sq.Index));
            bool asDefined = sq.Def.F.Occupied().All(o => sq.Units!.Any(x => x.Def.Id == o.Def.Id && x.Slot == o.Slot));
            sq.Label = asDefined ? sq.Def.Name : $"第{sq.Index + 1}隊";
            sq.Role = asDefined ? sq.Def.Role
                    : IsLocked(sq.Index) ? "枠を開けると使える隊"
                    : sq.Units!.Count == 0 ? (Stage.Gold.On ? "呼んだ駒で組む隊" : "届いた駒で組む隊") : "既定の組み方";
        }
        return true;
    }

    /// <summary>
    /// 1 戦ぶんの駒を用意する。<b>返すリストはそのまま <see cref="BattleEngine.Run"/> へ渡す</b>
    /// ——戦闘が書き換えた同じ参照を <see cref="Resolve"/> が読む。
    /// </summary>
    public (List<UnitState> Players, List<UnitState> Enemies, int Seed, Node Node)? Prepare(
        int squadIndex, Node? against = null)
    {
        Squad s = Squads[squadIndex];
        if (s.Units is not { } pu || s.Road < 0) return null;
        // **拠点なしでは第175期のまま**（道の先頭の区画）。拠点ありでは「いま同じマスにいる相手」
        // ——敵の拠点のマスでは、そこに立っている湧いた部隊が相手になる（§1-2）。
        Node? node = against ?? (Portal.On ? FoeFacing(squadIndex) : NextNode(s.Road));
        if (node is null || node.Cleared) return null;
        node.Units ??= BattleEngine.Materialize(node.Def.Enemy, BattleContext.EnemyTeam, Map11.EnemyScale);
        return (pu, node.Units, Map11.DeriveSeed(Seed, Battles), node);
    }

    /// <summary>
    /// 1 戦ぶんの後始末。<b>第168期 <c>BandOnce</c> と同じ順序・同じ規則</b>——
    /// 敵の生存者は傷ついたまま残り、味方は生き残れば境界を越えて
    /// <b>勝ったときだけ</b> <see cref="Map11.RecoverPercent"/> ぶん回復する。
    ///
    /// <para><b>第175期 §2 —— 迎撃戦に勝った隊にはその回復を乗せない</b>
    /// （<see cref="TimeRule.InterceptRecover"/>・既定 <c>false</c>）。
    /// <b>この期で変えた規則はこれ1つだけ</b>で、他は1行も触っていない。
    /// <c>InterceptRecover = true</c> にすれば第174期と1ビットも違わない。</para>
    /// </summary>
    /// <param name="intercept">拠点の迎撃戦か（<see cref="PrepareIntercept"/> を通った戦闘）。</param>
    public void Resolve(int squadIndex, Node node, bool playerWon, bool intercept = false)
    {
        Squad s = Squads[squadIndex];
        List<UnitState> pu = s.Units!;
        List<UnitState> eu = node.Units!;
        Battles++;

        var aliveP = pu.Where(u => u.IsAlive).ToList();
        var aliveE = eu.Where(u => u.IsAlive).ToList();
        // 第447期: 呼び戻しのある盤では、この戦闘で倒れた駒を記録する（境界で隊から外れるので、ここで拾わないと消える）。
        if (Stage.Gold.Revive > 0)
            foreach (UnitState u in pu)
                if (!u.IsAlive && RosterIds.Contains(u.Def.Id) && !FallenUnits.Contains(u)) FallenUnits.Add(u);
        node.HpNow = aliveE.Sum(u => u.Hp);

        if (aliveE.Count == 0)
        {
            node.Cleared = true;
            node.Units = null;
            node.HpNow = 0;
            if (node.Def.Road == s.Home && !node.FromPortal) s.Cleared++;
        }
        else node.Units = EngagementEngine.CrossBoundary(aliveE);

        if (aliveP.Count == 0)
        {
            s.Units = null;
            s.Lost = true;
            Place(s, Graph.Home, -1);
        }
        else
        {
            // 第175期 §2: **迎撃で勝っても、道中の回復は乗らない**（待ちの抜け道を塞ぐ）。
            bool recover = playerWon && Map11.RecoverPercent > 0
                           && (!intercept || Time.InterceptRecover);
            s.Units = EngagementEngine.CrossBoundary(aliveP, pu,
                recover ? new RecoverRule(Map11.RecoverPercent, false) : null);
        }

        // 敵の拠点のマスで最後の1つを抜いたら、その場で制圧になる（第176期 §1-3）。
        TryCapture();
        // 第444期: 中間拠点の取り合い → 抜いた見返り（先に中間拠点を取ってから、その隊がいる拠点へ届ける）。
        UpdateOutposts();
        if (node.Cleared && Stage.Arrival.OnKill > 0)
            Deliver(Stage.Arrival.OnKill, intercept ? "拠点" : DeliveryPlace(squadIndex), $"{node.Def.Name}を抜いた");
        // 第445期: 抜いた見返りの金（湧いた部隊は安い）。
        if (node.Cleared)
            Earn(node.FromPortal && !node.IsReserve ? Stage.Gold.SpawnKill : Stage.Gold.Kill, $"{node.Def.Name}を抜いた", node.Pos);
    }

    // =============================================================================
    // 時間（第174期 部B）
    //
    // **`Time.On` が偽なら、ここから下は1行も走らない。** 呼ぶ側（`Map11Verify` の方針と
    // `Map11Main` の作戦ターン）が `Time.On` を見て分岐する。
    //
    // 規則は指示書 §2-1 のまま——1 作戦ターンに全部の隊が1つずつ行動し、終わったら敵が動く。
    // **戦闘・回復・勝敗の規則は1つも触っていない**（`Prepare` / `Resolve` をそのまま使う）。
    // =============================================================================

    /// <summary>敵の前進で起きる戦闘。<c>Squad</c> が -1 なら<b>拠点の迎撃</b>（相手は拠点の隊から選ぶ）。</summary>
    public sealed record Encounter(int Squad, Node Node, bool Intercept);

    /// <summary>その隊は拠点にいるか。</summary>
    public bool AtHome(int squadIndex) => Squads[squadIndex].Pos == Graph.Home;

    /// <summary>道 <paramref name="road"/> で次に当たる敵のマス（敵が残っていなければ null）。</summary>
    public int? FoeCell(int road) => NextNode(road)?.Cell;

    /// <summary>
    /// 進む（1 マス）。<b>敵のいるマスへ入れば、その場で戦闘</b>——戻り値が相手の区画。
    /// 拠点にいる隊は <paramref name="road"/> の道へ出る（<see cref="Send"/> を通る）。
    /// 敵を追い越すことはできない。
    /// </summary>
    public Node? Advance(int squadIndex, int road)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost || !CanSend(squadIndex)) return null;

        if (s.Pos == Graph.Home)
        {
            if (!CanHead(road)) return null;
            Send(squadIndex, road);
        }
        else
        {
            road = s.Road;
            // **すでに敵と同じマスにいる**（前の戦闘が決着しなかった）ときは、その場でもう一度当たる。
            if (FoeFacing(squadIndex) is { } contact) return contact;
            // 第438期: 道の奥（敵の拠点・無ければ経路の奥）への最短路の次のマスへ 1 歩。
            // **道の先頭の敵より先へは行かない**——次のマスがその敵のマスなら入って戦闘、
            // その敵が自分より拠点寄りにいる（決着しなかった戦闘の後に脇を抜けられた）ならそのマスへ引き返して当たる
            // （第437期の `Math.Min(Cell + 1, 先頭の敵のマス)` をそのまま写した）。
            int next = Graph.NextStep(s.Pos, road, Graph.At(road, MaxCell), road);
            if (FrontFoe(road) is { } front && front.Cell < Graph.IndexOn(road, next)) next = front.Pos;
            if (next == s.Pos) return null;
            Place(s, next, road);
            // 敵の拠点のマスへ入って、そこに敵が1つも無ければ制圧（第176期 §1-3）。
            TryCapture();
        }

        return FoeFacing(squadIndex);
    }

    /// <summary>
    /// 経路をまたぐ辺で隣のマスへ移る（第438期・<b>1-1 には無い辺</b>——玩具の盤のために置いた）。
    /// <b>移った先に敵がいれば、その場で戦闘</b>（<see cref="Advance"/> と同じ）。拠点へは入らない（<see cref="StepBack"/> の役目）。
    /// </summary>
    public Node? MoveTo(int squadIndex, int cell)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is null || !CanSend(squadIndex) || s.Pos == Graph.Home) return null;
        if (FoeFacing(squadIndex) is { } contact) return contact;
        if (!Graph[s.Pos].Neighbors.Contains(cell) || Graph.IsBase(cell)) return null;
        Place(s, cell, s.Road);
        return FoeFacing(squadIndex);
    }

    /// <summary>
    /// 拠点どうしのワープ（第176期 §1-3）。<b>制圧した後だけ</b>、味方の拠点 ⇔ 第2拠点を
    /// <b>1 作戦ターンで行き来できる</b>。<b>道の上へは飛べない。</b>
    /// </summary>
    public bool Warp(int squadIndex, Dest dest)
    {
        // 第177期 §1: 可否の判定は `CanWarp` の1箇所に寄せた（リキャストもそこで見る）。
        if (!CanWarp(squadIndex, dest)) return false;
        Squad s = Squads[squadIndex];
        _portalReady = Turn + Math.Max(0, Portal.WarpEvery);
        Warps++;

        if (dest.AtHome)
        {
            Place(s, Graph.Home, -1);
            return true;
        }
        if (s.Home < 0) s.Home = dest.Road;
        Place(s, Graph.Portal, dest.Road);
        s.Deployed = true;
        return true;
    }

    /// <summary>そのワープができるか（<see cref="Map11Orders.Plan"/> が読む）。</summary>
    public bool CanWarp(int squadIndex, Dest dest)
    {
        if (!Portal.On || !Captured || !PortalReady) return false;
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is null || !CanSend(squadIndex)) return false;
        if (dest.AtHome) return AtSecondBase(squadIndex);
        return s.Pos == Graph.Home && dest.IsPortal && PortalFoe() is null;
    }

    /// <summary>戻る（拠点へ向かって 1 マス）。<b>一瞬では戻れない。</b></summary>
    public bool StepBack(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is null || s.Pos == Graph.Home) return false;
        // 第438期: 拠点への最短路を 1 歩（敵の拠点からは入ってきた経路へ出る）。
        int next = Graph.NextStep(s.Pos, s.Road, Graph.Home, -1);
        Place(s, next, next == Graph.Home ? -1 : s.Road);
        return true;
    }

    /// <summary>
    /// 休む（<b>拠点でだけ</b>）。生存者の HP を <see cref="TimeRule.RestPercent"/> ぶん戻す。
    /// <b>死者は戻らない。</b> 足し方は境界の回復（<c>CarryOver</c>）と同じ式で、
    /// <c>ctx.Heal</c> は通さない（渇きも支援拒否も戦闘中の規則なので、拠点の手当てには掛からない）。
    /// </summary>
    public bool Rest(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost || Time.RestPercent <= 0) return false;
        if (s.Pos != Graph.Home && !AtForwardBase(squadIndex)) return false;
        if (s.Units is not { } u) return false;   // 未出撃の隊は満タンなので休む意味がない
        bool moved = false;
        foreach (UnitState x in u)
        {
            if (!x.IsAlive) continue;
            int before = x.Hp;
            x.Hp = Math.Min(x.MaxHp, x.Hp + x.MaxHp * Time.RestPercent / 100);
            if (x.Hp != before) moved = true;
        }
        if (moved) Rests++;
        return moved;
    }

    /// <summary>生存者に傷が残っているか（「全快」方針が読む）。</summary>
    public bool Hurt(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        return s.Units is { } u && u.Any(x => x.IsAlive && x.Hp < x.MaxHp);
    }

    /// <summary>
    /// 敵が動く（1 作戦ターンの終わり）。<b>まだ抜かれていない先頭の敵部隊だけ</b>が
    /// <see cref="TimeRule.AdvanceEvery"/> 作戦ターンごとに1マス拠点へ近づく。
    /// 前進先に味方がいれば戦闘、拠点まで着いたら迎撃（拠点に隊がいなければ陥落）。
    /// </summary>
    public List<Encounter> AdvanceFoes()
    {
        var list = new List<Encounter>();
        if (!Time.On) return list;
        // 第445期: 制圧で勝った盤では、勝った後に敵は動かない（残った敵部隊は退く）。
        if (Stage.WinOnCapture && Won) return list;
        Turn++;
        LastArrived.Clear();
        if (Time.AdvanceEvery <= 0 || Turn % Time.AdvanceEvery != 0)
        { LoseSecondBaseIfEmpty(); UpdateOutposts(); ArriveIfDue(); ReserveIfDue(); return SpawnIfDue(list); }

        // 1 部隊ぶんの前進。**第175期の式をそのまま関数にしただけ**で、中身は1文字も変えていない。
        // 戻り値が偽 ＝ 拠点が陥落したので、その場で打ち切る。
        bool Step(Node n)
        {
            // 第438期: 経路の上で 1 つ拠点寄りのマスへ（敵の拠点からは経路の奥のマスへ）。
            if (n.Pos != Graph.Home) n.Pos = Graph.TowardHome(n.Def.Road, n.Pos);
            if (n.Pos == Graph.Home)
            {
                BeginIntercept(n);
                if (NextInterceptor(n) < 0) { Fallen = true; FallenRoad = n.Def.Road; return false; }
                list.Add(new Encounter(-1, n, true));
                return true;
            }
            int sq = SquadAt(n.Pos);
            if (sq >= 0) list.Add(new Encounter(sq, n, false));
            return true;
        }

        for (int road = 0; road < Stage.LaneCount; road++)
        {
            // **最初からいる4部隊の規則は第175期のまま**（先頭だけが前進する・§1-1）。
            Node? n = NextNode(road);
            if (n is not null && !Step(n)) return list;
            if (!Portal.On) continue;

            // **湧いた部隊は列になって進む**（§1-2）——手前から順に1マスずつ、
            // 前が詰まっていればそのマスの手前で待つ。固定の4部隊も「詰まり」として数える。
            foreach (Node sp in FoesOn(road).Where(x => x.FromPortal).ToList())
            {
                if (sp.Pos == Graph.Home) continue;
                int target = Graph.TowardHome(road, sp.Pos);
                if (FoesOn(road).Any(x => !ReferenceEquals(x, sp) && x.Pos == target)) continue;
                if (!Step(sp)) return list;
            }
        }
        LoseSecondBaseIfEmpty();
        UpdateOutposts();
        // 第441期: 到着と予備隊の出発は**湧きの隣**（1 刻の終わり・敵が動いた後）。並びは 到着 → 予備隊 → 湧き。
        ArriveIfDue();
        ReserveIfDue();
        return SpawnIfDue(list);
    }

    /// <summary>
    /// 途中到着（第441期 §2）。<b>A 刻ごと</b>に到着の列の先頭から 1〜2 枚を手札（<see cref="Bench"/>）へ足す。
    /// 届いた駒は<b>拠点にいる隊にだけ組める</b>（第172期の組み直しの規則のまま）。陥落の後は届かない。
    /// </summary>
    private void ArriveIfDue()
    {
        if (Arrivals.Count == 0 || Fallen || Stage.Arrival.Every <= 0 || Turn % Stage.Arrival.Every != 0) return;
        int k = Math.Min(Arrivals.Count, _arrivalBatches.Count > 0 ? _arrivalBatches.Dequeue() : 1);
        for (int i = 0; i < k; i++)
        {
            UnitState u = Arrivals[0];
            Arrivals.RemoveAt(0);
            Bench.Add(u);
            LastArrived.Add(u);
        }
        ArrivedCount += k;
    }

    /// <summary>
    /// 敵の予備隊の出発（第441期 §3）。<b>引き金はプレイヤーの最初の接敵が決着したこと</b>（戦闘が 1 回でも済んだ刻の終わり）。
    /// 行き先は<b>味方の隊が 1 つもいない経路</b>（複数あれば番号の小さい方）、無ければ<b>生存枚数の合計が最少の経路</b>（同じなら番号の小さい方）。
    /// <b>決まったら変えない</b>。出発した予備隊は湧いた部隊の列の最後尾に入り、以後は同じ規則で進む（1 刻 1 マス・前が詰まれば待つ・拠点に着けば迎撃）。
    /// </summary>
    private void ReserveIfDue()
    {
        if (!ReserveWaiting || Battles <= 0 || Fallen) return;
        Node r = Reserve!;
        r.Def = r.Def with { Road = ReserveTarget() };
        Spawns.Add(r);
        ReserveDepartTurn = Turn;
    }

    /// <summary>予備隊がいま出発するなら向かう経路（<b>盤面は1ビットも動かさない</b>・画面と門も読む）。</summary>
    public int ReserveTarget()
    {
        var alive = new int[Stage.LaneCount];
        var any = new bool[Stage.LaneCount];
        foreach (Squad s in Squads)
            if (!s.Lost && s.Units is { } u && s.Road >= 0 && s.Road < Stage.LaneCount && u.Any(x => x.IsAlive))
            {
                any[s.Road] = true;
                alive[s.Road] += u.Count(x => x.IsAlive);
            }
        for (int lane = 0; lane < Stage.LaneCount; lane++) if (!any[lane]) return lane;
        int best = 0;
        for (int lane = 1; lane < Stage.LaneCount; lane++) if (alive[lane] < alive[best]) best = lane;
        return best;
    }

    /// <summary>予備隊が拠点に着くまでの見込みの刻（出発していなければ null・前が詰まれば延びる）。</summary>
    public int? ReserveEta => Reserve is { Cleared: false } r && r.Def.Road >= 0 && r.Pos != Graph.Home
        ? Graph.IndexOn(r.Def.Road, r.Pos) + 1 : null;

    /// <summary>
    /// 次の敵の前進で、<b>どの敵部隊がどのマスへ入るか</b>（第440期・画面の敵の矢印）。<b>盤面は1ビットも動かさない。</b>
    /// <see cref="AdvanceFoes"/> と<b>同じ順・同じ式</b>で、位置を写しの上で動かすだけ（列の詰まりも同じに見る）。
    /// 拠点まで来て止まっている敵・陥落の後・前進しない作戦ターンは出さない。一致は `--map12-smoke` が確かめる。
    /// </summary>
    public List<(Node Node, int To)> PlannedFoeSteps()
    {
        var list = new List<(Node, int)>();
        if (!Time.On || Fallen || Time.AdvanceEvery <= 0 || (Turn + 1) % Time.AdvanceEvery != 0) return list;
        var moved = new Dictionary<Node, int>(ReferenceEqualityComparer.Instance);
        int PosOf(Node x) => moved.TryGetValue(x, out int p) ? p : x.Pos;
        for (int road = 0; road < Stage.LaneCount; road++)
        {
            if (NextNode(road) is { } n && n.Pos != Graph.Home)
            {
                int to = Graph.TowardHome(road, n.Pos);
                list.Add((n, to));
                moved[n] = to;
            }
            if (!Portal.On) continue;
            foreach (Node sp in FoesOn(road).Where(x => x.FromPortal).ToList())
            {
                if (sp.Pos == Graph.Home) continue;
                int target = Graph.TowardHome(road, sp.Pos);
                if (FoesOn(road).Any(x => !ReferenceEquals(x, sp) && PosOf(x) == target)) continue;
                list.Add((sp, target));
                moved[sp] = target;
            }
        }
        return list;
    }

    /// <summary>
    /// 第2拠点の明け渡し（第177期 §1「空の拠点は即占領」の味方側）。
    /// <b>敵が制圧済みの拠点のマスに立っていて、そこに出せる隊が1つも無ければ取り返される</b>
    /// ——防壁は置かない。取り返されれば湧きも再開する。
    ///
    /// <para><b>敵は味方の拠点へ向かってしか動かない</b>ので、実測ではここは1度も発火しない
    /// （第176期 §1-3 (4) で 0 件）。<b>規則として対称にするためだけに残す。</b></para>
    /// </summary>
    private void LoseSecondBaseIfEmpty()
    {
        if (!Portal.On || !Captured || PortalFoe() is null) return;
        if (Squads.Any(x => !x.Lost && AtSecondBase(x.Index) && CanSend(x.Index))) return;
        Captured = false;
        SecondBaseFalls++;
    }

    /// <summary>
    /// 敵の拠点から1部隊湧かせる（第176期 §1-2）。<b>制圧していれば湧かない。</b>
    /// 道は<b>経路を順に回す</b>（1-1 は北と南を交互・1-2 は北 → 中 → 南）。中身はステージが決める（<see cref="MapStage.Spawn"/>・1-1 はその道の先遣）。
    /// <b>満タンで湧く</b>——<c>Materialize</c> は戦闘の直前まで遅らせるので、ここでは箱だけ作る。
    /// </summary>
    private List<Encounter> SpawnIfDue(List<Encounter> list)
    {
        if (!Portal.On || Captured || Fallen) return list;
        // 第177期 §1: **ポータルは使うたびに塞がる**（湧きと味方のワープで1本を共有する）。
        if (Portal.SpawnEvery <= 0 || Turn <= 0 || !PortalReady) return list;
        _portalReady = Turn + Portal.SpawnEvery;

        int road = SpawnCount % Stage.LaneCount;
        var (enemy, name, stageIndex) = Stage.Spawn(road);
        var def = new Map11.RoadNode(road, Map11.RoadCells, name, enemy, stageIndex);
        int hp = def.Enemy.Occupied().Sum(o => o.Def.MaxHp);
        Spawns.Add(new Node
        {
            Def = def, Graph = Graph, FromPortal = true, DefMaxHp = hp, HpNow = hp, Pos = Graph.Portal,
        });
        SpawnCount++;
        return list;
    }

    /// <summary>そのマスにいる隊のうち、いちばん番号の若いもの（いなければ -1）。</summary>
    public int SquadAt(int cellId)
    {
        for (int i = 0; i < Squads.Length; i++)
        {
            Squad s = Squads[i];
            if (s.Units is not null && s.Pos == cellId && s.Units.Any(u => u.IsAlive))
                return i;
        }
        return -1;
    }

    /// <summary>迎撃が始まった（同じ到着で同じ隊を二度出さないための印）。</summary>
    public void BeginIntercept(Node node) { node.Tried.Clear(); Intercepts++; }

    /// <summary>次に迎撃へ出せる隊（拠点にいて、まだこの到着で出していない隊）。いなければ -1。</summary>
    public int NextInterceptor(Node node)
    {
        if (node.Cleared) return -1;
        for (int i = 0; i < Squads.Length; i++)
            if (!node.Tried.Contains(i) && !Squads[i].Lost && Squads[i].Pos == Graph.Home && CanSend(i)) return i;
        return -1;
    }

    /// <summary>迎撃に出す（<b>拠点から動かない</b>——道の上に出るわけではない）。</summary>
    public (List<UnitState> Players, List<UnitState> Enemies, int Seed, Node Node)? PrepareIntercept(
        int squadIndex, Node node)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost || node.Cleared || !CanSend(squadIndex)) return null;
        node.Tried.Add(squadIndex);
        List<UnitState> pu = Roster(squadIndex);
        if (!pu.Any(u => u.IsAlive)) return null;
        s.Deployed = true;
        if (s.Home < 0) s.Home = node.Def.Road;
        node.Units ??= BattleEngine.Materialize(node.Def.Enemy, BattleContext.EnemyTeam, Map11.EnemyScale);
        return (pu, node.Units, Map11.DeriveSeed(Seed, Battles), node);
    }

    /// <summary>
    /// 迎撃が終わったあとの判定。<b>抜けず、拠点に出せる隊がもう1つも無ければ陥落。</b>
    /// </summary>
    public void CloseIntercept(Node node)
    {
        LoseSecondBaseIfEmpty();
        if (node.Cleared) return;
        // **判定は「拠点に出せる隊が1つも無いか」の1本だけ**——この到着でもう出した隊が
        // 残っていても（決着しなかったなど）、まだ拠点にいるなら陥落ではない。
        if (!Squads.Any(x => !x.Lost && x.Pos == Graph.Home && CanSend(x.Index)))
        {
            Fallen = true;
            FallenRoad = node.Def.Road;
        }
    }

    /// <summary>部分点（第168期と同じ定義）＝ 抜いた部隊数 ＋ 残った部隊の削り。</summary>
    public double Partial()
    {
        double p = ClearedCount;
        foreach (Node[] road in Nodes)
            foreach (Node n in road)
                if (!n.Cleared && n.DefMaxHp > 0)
                    p += (double)(n.DefMaxHp - n.HpNow) / n.DefMaxHp;
        return p;
    }
}
