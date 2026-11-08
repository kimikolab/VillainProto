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

    /// <summary>制圧の後の勇者（第455期）が現れてから決着しないまま打ち切る刻（器具の止め・ふつうは起きない）。</summary>
    public const int HeroStallTurns = 40;

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
    /// <para><c>Stationary</c>（第449期 §0-3）は<b>動かない区画</b>——敵の拠点のマスに立ち、前進しない（「先頭だけが前進する」は飛ばして次を先頭と見る）。
    /// 制圧するには抜く必要がある（<see cref="Map11State.PortalFoe"/> に数える）。湧きの列は塞がない。</para>
    /// <para><c>Hold</c>（第458期 §0-2）は<b>自分の初期位置から動かない区画</b>（1-3 の大群・徴募兵 ×3 が C3 に重なって待つ）。
    /// 先頭の前進（<see cref="Map11State.NextNode"/>）からは飛ばし、湧いた部隊はその手前で詰まる。敵の拠点の守備隊（<c>Stationary</c>）とは別の札。</para>
    public sealed record RoadNode(int Road, int Index, string Name, Formation Enemy, int StageIndex, bool Stationary = false, bool Hold = false);

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
    /// <summary>
    /// 作戦マップの敵部隊の 1 行（第448期・`EnemyCatalog.MapSquads` の <c>Line</c>）。<b>部隊の名前で引く</b>——湧いた部隊・予備隊も同じ名前なので同じ行になる。
    /// 帯 A でない部隊（波の写し）は空。
    /// </summary>
    public static string SquadLineOf(string name)
        => EnemyCatalog.MapSquads.FirstOrDefault(m => m.Name == name)?.Line ?? "";

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
        /// <summary>作戦マップの勇者か（第454期 §2）。湧いた数・湧きを抜いた数には入れない。</summary>
        public bool IsHero { get; init; }
        /// <summary>
        /// 敵の拠点から湧いた部隊か（第176期）。<b>最初からいる4部隊は偽。</b>
        /// 2 本抜き（<see cref="Squad.Cleared"/>）の分子に入れないためだけの印で、
        /// 戦闘・回復・勝敗の規則は1つも読まない。
        /// </summary>
        public bool FromPortal { get; init; }
        public List<UnitState>? Units { get; set; }
        public bool Cleared { get; set; }
        /// <summary>
        /// 制圧した刻に退いた（第455期・<see cref="HeroRule.AfterCapture"/> の盤だけ）。<b>盤からは <see cref="Cleared"/> と同じに消える</b>が、
        /// 「抜いた」には数えない（<see cref="ClearedCount"/>・湧きを抜いた数）。
        /// </summary>
        public bool Retreated { get; set; }
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

    /// <summary>
    /// 拠点が落ちた（第459期 §1-3・追ってくる勇者の盤 <see cref="HeroRule.Chaser"/> だけ）。<b>負けではない</b>——
    /// 勇者が現れた刻から、空の拠点に何が着いてもこちらになる（<see cref="Fallen"/> は立たない）。
    /// 落ちた後は呼ぶ・休む・組み直しは取っている中間拠点でだけ（<see cref="CanReform"/> ／ <see cref="Rest"/>）・拠点へは戻れない・
    /// 落ちた拠点に着いた敵はそこで止まる・勇者はいちばん近い隊を追う（<see cref="HeroChase"/>）。
    /// </summary>
    public bool HomeLost { get; private set; }
    /// <summary>追ってくる勇者が、味方の隊のいる本陣のマスへ入った回数（第459期・本陣の連戦に加わった・測るためだけ）。</summary>
    public int HeroJoinedSiege { get; private set; }
    /// <summary>拠点が落ちた刻（第459期・-1 ＝ 落ちていない・測るためだけ）。</summary>
    public int HomeLostTurn { get; private set; } = -1;
    /// <summary>拠点を落とした敵部隊の名前（第459期・結果画面と器具が読む・測るためだけ）。</summary>
    public string? HomeLostBy { get; private set; }

    /// <summary>
    /// 空の拠点に敵が着いた（第459期 §1-5）。追ってくる勇者が現れた後なら<b>拠点が落ちるだけ</b>で続く（true）、
    /// それ以外は今までどおり<b>陥落</b>（<see cref="Fallen"/>・false）。<b>`Fallen = true` を書くのはここだけ</b>。
    /// </summary>
    private bool LoseHome(Node n)
    {
        if (Stage.Hero.Chaser && HeroTurn >= 0)
        {
            if (!HomeLost)
            {
                HomeLost = true;
                HomeLostTurn = Turn;
                HomeLostBy = n.IsHero ? "勇者" : n.Def.Name;
                RelocateHomeless();
            }
            return true;
        }
        Fallen = true;
        FallenRoad = n.Def.Road;
        return false;
    }

    /// <summary>
    /// 拠点が落ちた後、拠点に残っている隊（0 枚の隊・まだ枠を開けていない隊）を<b>取っている中間拠点</b>へ移す（第459期 §1-4「新しい隊は中間拠点から出す」）。
    /// 取っている中間拠点が無ければ拠点に残す（出せない）——後で中間拠点を取ったとき（<see cref="UpdateOutposts"/>）にもう一度呼ぶ。
    /// </summary>
    private void RelocateHomeless()
    {
        if (!HomeLost || _relocating || HeldOutposts.Count == 0) return;
        _relocating = true;
        int cell = _outposts.First(c => HeldOutposts.Contains(c));
        foreach (Squad s in Squads)
            if (!s.Lost && s.Pos == Graph.Home) Place(s, cell, Graph[cell].Lane);
        _relocating = false;
    }
    private bool _relocating;

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

    /// <summary>
    /// 経路 <paramref name="road"/> で隊が立てるいちばん奥（第458期・経路の長さが盤で違う 1-3 のため）。
    /// 1-1 ／ 1-2 は経路の長さが <see cref="Map11.RoadCells"/> なので <see cref="MaxCell"/> と同じ値。
    /// </summary>
    public int MaxCellOf(int road) => road < 0 || road >= Graph.Lanes.Count ? MaxCell
        : Portal.On ? Graph.Lanes[road].Length : Graph.Lanes[road].Length - 1;

    /// <summary>経路の長さが 1-1 ／ 1-2 と違う盤か（第458期・1-3）。<b>画面の文（マスの呼び名）だけが読む。</b></summary>
    public bool LongLanes => Graph.Lanes.Any(l => l.Length != Map11.RoadCells);

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
    /// <summary>倒れた駒がどの隊で倒れたか（第461期・1 章の持ち越しだけが読む）。</summary>
    private readonly Dictionary<UnitState, int> _fallenSquad = new(ReferenceEqualityComparer.Instance);

    // ---------------- 1 章の持ち越し（第461期 §1-1・`Chapter1` だけが使う） ----------------

    /// <summary>持ち越す駒 1 枚（定義・席（手札なら -1）・持たせたレリック）。HP と状態は持ち越さない（次のステージの始めに全快）。</summary>
    public sealed record CarryUnit(UnitDef Def, int Slot, TraitId? Relic);

    /// <summary>
    /// 次のステージへ持ち越すもの（第461期）: 手札・隊ごとの駒と席・隊の枠・金の余り・手持ちのレリック。
    /// 倒れた駒は<b>倒れた隊の同じ席</b>へ戻す（席が埋まっていれば手札へ）——「全快・死者も戻る」。
    /// </summary>
    /// <param name="Stars">第463期: キャラごとの☆と途中まで重ねた枚数（☆の無いステージは null）。駒の定義はすでに☆の写し。</param>
    /// <param name="Invest">第467期: 投資の記録（どのステージで何の軸を選んだか・選んだ順・投資の無いステージは null）。</param>
    public sealed record CarryPack(IReadOnlyList<CarryUnit> Bench, IReadOnlyList<IReadOnlyList<CarryUnit>> Squads,
                                   int SquadCap, int Gold, IReadOnlyList<TraitId> RelicHand,
                                   IReadOnlyDictionary<string, StarState>? Stars = null,
                                   IReadOnlyList<(string Stage, string Axis)>? Invest = null)
    {
        /// <summary>駒の枚数（手札 ＋ 隊）。</summary>
        public int Units => Bench.Count + Squads.Sum(s => s.Count);
        /// <summary>レリックの数（手持ち ＋ 駒に持たせた札）。</summary>
        public int Relics => RelicHand.Count + Bench.Concat(Squads.SelectMany(s => s)).Count(u => u.Relic is not null);
    }

    /// <summary>このステージの終わりの持ち越し（第461期・<b>盤面は1ビットも動かさない</b>）。</summary>
    public CarryPack TakeCarry()
    {
        var squads = new List<List<CarryUnit>>();
        foreach (Squad q in Squads)
        {
            IEnumerable<CarryUnit> alive = q.Units is { } us
                ? us.Where(u => u.IsAlive).Select(u => new CarryUnit(DoctrineDefs.BaseOf(u.Def), u.Slot, u.Relic))   // 第468期: 盤の札の写しは持ち越さない（戦闘の外では元の定義のはず・念のため）
                : q.Lost ? Enumerable.Empty<CarryUnit>() : q.Def.F.Occupied().Select(o => new CarryUnit(o.Def, o.Slot, null));
            squads.Add(alive.ToList());
        }
        var bench = Bench.Select(u => new CarryUnit(u.Def, -1, u.Relic)).ToList();
        var seen = squads.SelectMany(s => s).Concat(bench).Select(c => c.Def.Id).ToHashSet();
        foreach (UnitState u in FallenUnits)
        {
            if (!seen.Add(u.Def.Id)) continue;
            int sq = _fallenSquad.TryGetValue(u, out int k) ? k : -1;
            if (sq >= 0 && sq < squads.Count && squads[sq].All(c => c.Slot != u.Slot)) squads[sq].Add(new CarryUnit(u.Def, u.Slot, u.Relic));
            else bench.Add(new CarryUnit(u.Def, -1, u.Relic));
        }
        foreach (var s in squads) s.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        return new CarryPack(bench, squads, SquadCap, Gold, RelicHand.ToList(),
                             Stage.Stars.On ? new Dictionary<string, StarState>(Stars) : null,   // 第463期: ☆と途中まで重ねた枚数も
                             Stage.Invest.On ? InvestLog.ToList() : null);                       // 第467期: 投資の記録も
    }

    /// <summary>前のステージから持ち越した（第461期・画面が組み直しの窓を開けるために読む）。</summary>
    public bool CarriedIn { get; private set; }

    /// <summary>
    /// 持ち越しを、始めたばかりのこのステージに載せる（第461期 §1-1・作戦ターンを 1 度も進めていない状態で呼ぶ）。
    /// 手札・隊の駒と席・隊の枠・レリックを写し、金は<b>余り ＋ このステージの始めの金</b>。駒は作り直す（全快・状態は 0・札は付けたまま）。
    /// 呼ぶ候補とレリックの列は、持ち越した駒・札を除いて引き直す（列の並びは同じ seed のまま）。
    /// </summary>
    public void ApplyCarry(CarryPack p)
    {
        UnitState Make(CarryUnit c) => MakeCarried(c);
        // 第463期: ☆と途中まで重ねた枚数を写す（駒の定義はすでに☆の写し）。このステージの始めの引きの☆は捨てる。
        Stars.Clear();
        if (p.Stars is { } ps) foreach (var (id, st) in ps) Stars[id] = st;
        Bench.Clear();
        Bench.AddRange(p.Bench.Select(Make));
        for (int i = 0; i < Squads.Length; i++)
        {
            Squads[i].Units = i < p.Squads.Count ? p.Squads[i].Select(Make).ToList() : new List<UnitState>();
            SortBySlot(Squads[i].Units!);
        }
        SquadCap = Math.Clamp(p.SquadCap, 1, Squads.Length);
        Gold = p.Gold + (Stage.Gold.On ? Stage.Gold.Start : 0);
        RelicHand.Clear();
        RelicHand.AddRange(p.RelicHand);
        // レリックの列: 持ち越した札（手持ち ＋ 駒に付いた札）は出さない。
        var owned = p.RelicHand.Concat(p.Bench.Concat(p.Squads.SelectMany(s => s)).Where(c => c.Relic is not null).Select(c => c.Relic!.Value)).ToHashSet();
        var relics = _relicQueue.Where(r => !owned.Contains(r)).ToList();
        _relicQueue.Clear();
        foreach (TraitId r in relics) _relicQueue.Enqueue(r);
        // 第467期: 投資の記録（投資の無いステージでは捨てる）。
        InvestLog.Clear();
        if (Stage.Invest.On && p.Invest is { } inv) InvestLog.AddRange(inv);
        // 呼ぶ候補: 列を同じ seed で並べ直し、持ち越した駒を飛ばして引き直す（コンストラクタと同じ式）。
        RebuildOffers();
        CarriedIn = true;
    }

    /// <summary>
    /// 呼ぶ候補を始めから引き直す（第461期の <see cref="ApplyCarry"/> の中の式を外へ出しただけ・第467期）。列を同じ seed で並べ直し、盤上・手札の駒を飛ばして候補の枚数を引く。
    /// 投資の乱数も始めに戻す（同じ投資・同じ盤なら同じ候補）。金の無い盤では何もしない。<b>ステージの始め（作戦ターンを 1 度も進めていない）でだけ呼ぶ。</b>
    /// </summary>
    private void RebuildOffers()
    {
        if (!Stage.Gold.On) return;
        Offers.Clear();
        _offerQueue.Clear();
        _investIds.Clear();
        _investRng = new Random(unchecked(Seed * 7919 + 467));
        var rng = new Random(unchecked(Seed * 7919 + 445));
        UnitDef[] pool = UnitCatalog.All.ToArray();
        for (int i = pool.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
        foreach (UnitDef d in pool) _offerQueue.Enqueue(d);
        for (int k = 0; k < Stage.Gold.Offers; k++) if ((k == 0 ? FirstOffer() : NextOffer()) is { } d) AddOffer(d);
    }

    // ---------------- 軸への投資（第467期 §1・投資のあるステージ（1 章）だけ） ----------------

    /// <summary>投資の記録（どのステージで何の軸を選んだか・選んだ順・持ち越す）。<b>投資の無いステージでは空</b>。</summary>
    public List<(string Stage, string Axis)> InvestLog { get; } = new();
    /// <summary>このステージの投資をまだ選んでいない（投資のあるステージだけ）。</summary>
    public bool InvestPending => Stage.Invest.On && !Finished && InvestLog.All(x => x.Stage != Stage.Id);
    /// <summary>その軸に投資した回数。</summary>
    public int InvestCount(string axis) => InvestLog.Count(x => x.Axis == axis);
    /// <summary>投資の 1 行（「投資: 燃焼 ×1・感電 ×1」）。</summary>
    public string InvestText => InvestRule.Text(InvestLog);

    /// <summary>
    /// このステージの投資を選ぶ（第467期 §1-1・ステージの始めに 1 度だけ）。呼ぶ候補はこの投資で引き直す（ステージの始めに投資していたのと同じ）。
    /// 選べたら true（投資の無いステージ・選び終えたステージ・軸でない語は false）。
    /// 第468期: 盤・金の札のあるステージでは盤・金の札も選べる（もう持っている札は選べない）。
    /// </summary>
    public bool Invest(string axis)
    {
        if (!InvestPending) return false;
        bool doctrine = Stage.Invest.Doctrines && Doctrine.Find(axis) is not null && InvestLog.All(x => x.Axis != axis);
        if (!UnitTags.Axes.Contains(axis) && !doctrine) return false;
        InvestLog.Add((Stage.Id, axis));
        RebuildOffers();
        return true;
    }

    // ---------------- 盤の札・金の札（第468期 §1-1・盤 ／ 金の札のあるステージ（1 章）だけ） ----------------

    /// <summary>持っている盤・金の札（選んだ順）。<b>盤・金の札の無いステージでは空</b>——下の値段と数値はすべて今のまま。</summary>
    public IReadOnlyList<Doctrine> Doctrines
        => Stage.Invest.Doctrines ? InvestLog.Select(x => Doctrine.Find(x.Axis)).OfType<Doctrine>().ToList() : Array.Empty<Doctrine>();

    /// <summary>その席（開戦時）の最大HP ／ 攻撃の増減（%・持っている盤の札を足し合わせる）。席の列は今の陣形の表（<see cref="FormationRules.RowOf(int)"/>）で読む。</summary>
    public (int Hp, int Atk) SeatPercent(int slot)
    {
        int hp = 0, atk = 0;
        Row row = FormationRules.RowOf(Math.Clamp(slot, 0, FormationRules.TotalSlots - 1));
        foreach (Doctrine d in Doctrines.Where(d => d.Kind == DoctrineKind.Board)) { var (h, a) = d.Of(row); hp += h; atk += a; }
        return (hp, atk);
    }

    /// <summary>その席で開戦したときの定義（盤の札の写し・☆の写しの後に掛ける）。盤の札が効かない席なら元の定義。</summary>
    public UnitDef DoctrineDef(UnitDef d, int slot)
    {
        var (hp, atk) = SeatPercent(slot);
        return DoctrineDefs.Of(d, hp, atk);
    }

    /// <summary>
    /// 盤の札が効く駒の札の文（第468期 §1-3・「開戦時 HP 60 → 75・攻 9 → 10（前のめり）」・効かなければ空）。
    /// <paramref name="slot"/> は今の席（開戦時の席として読む）。
    /// </summary>
    public string DoctrineText(UnitDef d, int slot)
    {
        UnitDef b = DoctrineDefs.BaseOf(d), x = DoctrineDef(b, slot);
        if (ReferenceEquals(b, x)) return "";
        Row row = FormationRules.RowOf(Math.Clamp(slot, 0, FormationRules.TotalSlots - 1));
        var parts = new List<string>();
        if (x.MaxHp != b.MaxHp) parts.Add($"HP {b.MaxHp} → {x.MaxHp}");
        if (x.Attack != b.Attack) parts.Add($"攻 {b.Attack} → {x.Attack}");
        string by = string.Join("・", Doctrines.Where(dc => dc.Kind == DoctrineKind.Board && dc.Of(row) != (0, 0)).Select(dc => dc.Name));
        return parts.Count == 0 ? "" : $"開戦時 {string.Join("・", parts)}（{by}）";
    }

    /// <summary>席の図の札に出す短い文（第468期・「開戦 HP75 攻13」・効かなければ空）。</summary>
    public string DoctrineShort(UnitDef d, int slot)
    {
        UnitDef b = DoctrineDefs.BaseOf(d), x = DoctrineDef(b, slot);
        if (ReferenceEquals(b, x)) return "";
        var parts = new List<string>();
        if (x.MaxHp != b.MaxHp) parts.Add($"HP{x.MaxHp}");
        if (x.Attack != b.Attack) parts.Add($"攻{x.Attack}");
        return parts.Count == 0 ? "" : "開戦 " + string.Join(" ", parts);
    }

    /// <summary>盤の札で数値を差した駒（差す前の HP ／ 最大HP と、差した直後の HP ／ 最大HP・戦闘で動かなければ元へそのまま戻すため）。</summary>
    private readonly Dictionary<UnitState, (int Hp, int Max, int ArmedHp, int ArmedMax)> _armed = new(ReferenceEqualityComparer.Instance);
    /// <summary>盤の札で数値を差した回数（駒の枚数・測るためだけ）。</summary>
    public int DoctrineArmed { get; private set; }

    /// <summary>
    /// 開戦の直前に、隊の駒へ盤の札の数値を差す（第468期 §1-1・<see cref="Prepare"/> ／ <see cref="PrepareIntercept"/> の最後）。<b>開戦時の席</b>で決め、戦闘中に動かされても変えない
    /// （定義の写しなので）。リストの中で置き換える（同じリストを戦闘へ渡す）。HP は割合を保つ。<b>盤の札が無ければ何もしない</b>（駒も作り直さない）。
    /// 戦闘の後始末（<see cref="Resolve"/>）で元の定義へ戻す（<see cref="DisarmDoctrine"/>）。
    /// </summary>
    private void ArmDoctrine(List<UnitState> units)
    {
        if (!Doctrines.Any(d => d.Kind == DoctrineKind.Board)) return;
        for (int i = 0; i < units.Count; i++)
        {
            UnitState u = units[i];
            if (!u.IsAlive || u.Def.Traits.Contains(TraitId.Ephemeral)) continue;
            UnitDef to = DoctrineDef(u.Def, u.Slot);
            if (ReferenceEquals(to, u.Def)) continue;
            UnitState n = WithDef(u, to);
            _armed[n] = (u.Hp, u.MaxHp, n.Hp, n.MaxHp);
            units[i] = n;
            DoctrineArmed++;
        }
    }

    /// <summary>
    /// 盤の札の写しを元の定義へ戻す（第468期・<see cref="Resolve"/> の最後）。隊・手札・倒れた駒のどこにいても。
    /// 戦闘で HP ／ 最大HP が動かなかった駒は差す前の値へそのまま、動いた駒は割合を保って戻す。
    /// </summary>
    private void DisarmDoctrine()
    {
        if (_armed.Count == 0 && !Squads.Any(q => q.Units?.Any(u => DoctrineDefs.IsCopy(u.Def)) == true)) return;
        var all = Squads.Where(q => q.Units is not null).SelectMany(q => q.Units!).Concat(Bench).Concat(FallenUnits).ToList();
        foreach (UnitState u in all)
        {
            if (!DoctrineDefs.IsCopy(u.Def)) continue;
            UnitState n = WithDef(u, DoctrineDefs.BaseOf(u.Def));
            if (_armed.TryGetValue(u, out var a) && u.Hp == a.ArmedHp && u.MaxHp == a.ArmedMax) { n.Hp = a.Hp; n.MaxHp = a.Max; }
            ReplaceUnit(u, n);
        }
        _armed.Clear();
    }

    /// <summary>
    /// 定義を替えた写し（第468期・盤の札）。最大HP は定義の比で掛け直し（戦闘で削れた最大HP の割合は保つ）、HP は割合を保つ（倒れた駒は倒れたまま）。
    /// 席・レリック・私有のカウンタは写す（<see cref="WithRelic"/> と同じ項目）。
    /// </summary>
    private static UnitState WithDef(UnitState u, UnitDef def)
    {
        static int R(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);
        int max = u.MaxHp == u.Def.MaxHp ? def.MaxHp : Math.Max(1, R((double)u.MaxHp * def.MaxHp / Math.Max(1, u.Def.MaxHp)));
        int hp = u.Hp <= 0 ? 0 : Math.Clamp(R((double)u.Hp * max / Math.Max(1, u.MaxHp)), 1, max);
        var n = new UnitState
        {
            Def = def, TeamId = u.TeamId, Shape = u.Shape, Slot = u.Slot, Hp = hp, MaxHp = max,
            Traits = u.Relic is TraitId r ? RelicCatalog.Attach(def.Traits, r) : TraitCatalog.Resolve(def.Traits),
            Relic = u.Relic,
            HasFallenBack = u.HasFallenBack, ActionIndex = u.ActionIndex, LastDeathTurn = u.LastDeathTurn, TakenTurn = u.TakenTurn,
        };
        foreach (var (k, v) in u.Counters) n.Counters[k] = v;
        return n;
    }

    /// <summary>手持ち（隊・手札・倒れた駒）のうち、その軸のキャラの数（器具の選び方 <see cref="InvestRule.BotPick"/> が読む）。</summary>
    public int OwnedAxisCount(string axis)
        => Squads.SelectMany(q => q.Units is { } us ? us.Select(u => u.Def) : q.Lost ? Enumerable.Empty<UnitDef>() : q.Def.F.Occupied().Select(o => o.Def))
            .Concat(Bench.Select(u => u.Def)).Concat(FallenUnits.Select(u => u.Def))
            .Where(d => MapDraw.AxesOf(d).Contains(axis)).Select(d => d.Id).Distinct().Count();

    /// <summary>いまの候補のうち、投資の枠から来た札か（画面の小さな印）。</summary>
    public bool IsInvestOffer(UnitDef d) => _investIds.Contains(d.Id);
    /// <summary>投資の枠から候補に出た枚数（測るためだけ）。</summary>
    public int InvestOffersShown { get; private set; }

    /// <summary>投資の乱数（戦闘の seed とも候補の列とも別・<b>投資が 1 回も無ければ引かない</b>）。</summary>
    private Random _investRng = new(0);
    /// <summary>いまの候補のうち投資の枠から来た札の Id。</summary>
    private readonly HashSet<string> _investIds = new();
    private bool _lastInvest;

    /// <summary>
    /// 候補 1 枚を投資の枠から引く（第467期 §1-2）。投資 n 回なら <see cref="InvestRule.Percent"/>(n)% で投資の枠に入り、軸は<b>投資の回数で按分</b>して選ぶ
    /// （投資の記録から 1 つを等しく引く——1 回ずつなら等分）。その軸の駒のうち列で最初の「呼べる駒」を抜き出す（ほかの駒の列の並びは変えない）。
    /// 外れたとき・その軸の駒を全部持っているときは null（今の引き方へ）。
    /// </summary>
    private UnitDef? InvestOffer()
    {
        // 第468期: 盤・金の札は数えない（軸の記録だけ・軸の記録が無ければ乱数も引かない）。第467期の記録はすべて軸なので今のまま。
        var axes = InvestLog.Where(x => InvestRule.IsAxis(x.Axis)).ToList();
        int picks = axes.Count;
        if (picks == 0 || !Stage.Invest.On) return null;
        if (_investRng.Next(100) >= Stage.Invest.Percent(picks)) return null;
        string axis = axes[_investRng.Next(picks)].Axis;
        int n = _offerQueue.Count;
        UnitDef? hit = null;
        for (int i = 0; i < n; i++)
        {
            UnitDef d = _offerQueue.Dequeue();
            if (hit is null && MapDraw.AxesOf(d).Contains(axis) && OfferEligible(d)) { hit = d; continue; }
            _offerQueue.Enqueue(d);
        }
        if (hit is not null) { _investIds.Add(hit.Id); InvestOffersShown++; _lastInvest = true; }
        return hit;
    }

    /// <summary>
    /// 門（`--chapter1-invest-smoke` (c)(d)）だけ: 候補 1 枚を引いて列の最後へ戻す（いまの候補は変えない）。投資の枠から来たかを返す。
    /// </summary>
    internal (UnitDef? Def, bool Invest) ProbeOffer()
    {
        _lastInvest = false;
        UnitDef? d = NextOffer();
        bool inv = _lastInvest;
        if (d is not null) { _offerQueue.Enqueue(d); _investIds.Remove(d.Id); if (inv) InvestOffersShown--; }
        return (d, inv);
    }
    /// <summary>
    /// 持ち越した駒 1 枚を作る（第461期の <see cref="ApplyCarry"/> の中の式を外へ出しただけ・第466期）。全快・状態は 0・札は付けたまま・席は持ち越しの席。
    /// 敵の重さの表（`FoeWeight`）も同じ式で駒を立てる（作戦マップの戦闘と同じ駒で回すため）。
    /// </summary>
    internal static UnitState MakeCarried(CarryUnit c)
    {
        UnitState u = One(c.Def);
        if (c.Slot >= 0) u.Slot = c.Slot;
        return c.Relic is TraitId r ? WithRelic(u, r) : u;
    }
    private static readonly HashSet<string> RosterIds = UnitCatalog.All.Select(d => d.Id).ToHashSet();
    /// <summary>開始の手札の引きで保証 3 条件を満たせたか（満たせなければ結果画面に印）。到着を切った通しでは真。</summary>
    public bool OpeningOk { get; } = true;
    /// <summary>開始の手札の引き直しの回数。</summary>
    public int OpeningRerolls { get; }
    /// <summary>開始の配りの軸の保証で選んだ軸（第464期・1 章の 1-1 だけ・保証の無い引きは null）。</summary>
    public string? OpeningAxis { get; }

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
        _holds = Stage.Lanes.Any(l => l.Any(n => n.Hold));
        // 最初の1回まで S ターン（第176期の `Turn % S == 0` と同じ立ち上がり）。
        _portalReady = Portal.On ? Math.Max(0, Portal.SpawnEvery) : 0;
        // 第177期 §3: 自分で組む通しでは、**3 隊の 15 枚をばらして手札にする**。
        if (draft && Stage.Arrival.On)
        {
            // 第441期 §2: **開始は 10 枚**。残りは到着の列に並ぶ（どれも seed で引く・戦闘の seed とは別の乱数）。
            // 第464期 §1-3: 配りの規則のある盤（1 章の 1-1）は 52 枚から・同じ軸 3 枚以上の保証つき。
            MapDraw.Opening o = Stage.Deal.On
                ? MapDraw.DrawAxis(seed, Stage.Arrival.Start, Stage.Deal)
                : MapDraw.Draw(seed, Stage.Arrival.Start, Stage.Arrival.MinPer, Stage.Arrival.MaxPer);
            OpeningAxis = o.Axis;
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
        // 第463期 §1-1: ☆のあるステージでは、始めの手札（と届く駒）はすべて☆1。
        if (Stage.Stars.On)
        {
            for (int i = 0; i < Bench.Count; i++) Bench[i] = Star1(Bench[i].Def);
            for (int i = 0; i < Arrivals.Count; i++) Arrivals[i] = Star1(Arrivals[i].Def);
        }
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
            // 第449期: 動かない区画（守備隊）は敵の拠点のマスに立つ。
            int pos = n.Stationary && Graph.HasPortal ? Graph.Portal : Graph.FoeStarts[n.Road][n.Index];
            return new Node { Def = n, Graph = Graph, DefMaxHp = hp, HpNow = hp, Pos = pos };
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
            // 第452期 §1-1: レリックの列（12 枚を別の乱数で並べる・戦闘の seed とも候補の列とも別）。
            if (Stage.Relic.On)
            {
                var relicRng = new Random(unchecked(seed * 7919 + 452));
                TraitId[] relics = RelicCatalog.All.Select(r => r.Id).ToArray();
                for (int i = relics.Length - 1; i > 0; i--) { int j = relicRng.Next(i + 1); (relics[i], relics[j]) = (relics[j], relics[i]); }
                foreach (TraitId r in relics) _relicQueue.Enqueue(r);
            }
            for (int k = 0; k < Stage.Gold.Offers; k++) if ((k == 0 ? FirstOffer() : NextOffer()) is { } d) AddOffer(d);
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

    // ---------------- 作戦マップのレリック（第452期 §1） ----------------

    /// <summary>手持ちのレリック（まだ誰にも持たせていない札・得た順）。</summary>
    public List<TraitId> RelicHand { get; } = new();
    /// <summary>いま選べる候補（中間拠点を取った刻に <see cref="RelicRule.Offers"/> 枚・選ぶか見送るまで残る）。空なら候補は出ていない。</summary>
    public List<TraitId> RelicOffer { get; } = new();
    /// <summary>レリックの列（12 枚を seed で並べたもの・引いた札は戻さない）。</summary>
    private readonly Queue<TraitId> _relicQueue = new();
    /// <summary>レリックを出した中間拠点（初めて取った刻だけ出す）。</summary>
    private readonly HashSet<int> _relicCells = new();
    /// <summary>候補が出ている間に次の中間拠点を取ったとき、後で出す候補の数。</summary>
    private int _relicPending;
    /// <summary>得た枚数 ／ 見送った回数 ／ 候補に出た札の回数（測るためだけの計数）。</summary>
    public int RelicsGained { get; private set; }
    public int RelicsDeclined { get; private set; }
    public Dictionary<TraitId, int> RelicShown { get; } = new();

    /// <summary>その駒がいまレリックを持たせ直せる場所にいるか（手札・組み直しのできる隊）。</summary>
    public bool CanEquip(UnitState u)
        => Stage.Relic.On && !Finished && u.IsAlive
           && (Bench.Contains(u) || Squads.Any(q => q.Units is { } us && us.Contains(u) && CanReform(q.Index)));

    /// <summary>候補を出す（中間拠点を初めて取った刻・<see cref="UpdateOutposts"/> だけが呼ぶ）。</summary>
    private void OfferRelics()
    {
        if (RelicOffer.Count > 0) { _relicPending++; return; }
        for (int k = 0; k < Stage.Relic.Offers && _relicQueue.Count > 0; k++)
        {
            TraitId r = k == 0 && Stage.Relic.Bridge ? FirstRelic() : _relicQueue.Dequeue();
            RelicOffer.Add(r);
            RelicShown[r] = RelicShown.GetValueOrDefault(r) + 1;
        }
    }

    /// <summary>
    /// 第453期 §0-2: 候補の 1 枚目。列のうち<b>両端が手持ちにある札</b>（<see cref="RelicFit"/> ＝ 2）の最初の 1 枚、無ければ<b>片端</b>（1）の最初の 1 枚、
    /// 無ければ列の先頭。抜き出した札のほかは列の並びを変えない。乱数は引かない。どれだったかは <see cref="RelicFirstFit"/> に数える（測るためだけ）。
    /// </summary>
    private TraitId FirstRelic()
    {
        var own = OwnedTags();
        bool axis = Stage.Relic.AxisOnly;
        TraitId[] q = _relicQueue.ToArray();
        int pick = -1;
        for (int want = 3; want >= 1 && pick < 0; want--) pick = Array.FindIndex(q, r => RelicFit(r, own, axis) == want);
        int fit = pick < 0 ? 0 : RelicFit(q[pick], own, axis);
        if (pick < 0) pick = 0;
        _relicQueue.Clear();
        for (int i = 0; i < q.Length; i++) if (i != pick) _relicQueue.Enqueue(q[i]);
        RelicFirstFit[fit]++;
        RelicFirstShown[q[pick]] = RelicFirstShown.GetValueOrDefault(q[pick]) + 1;   // 第454期（測るためだけ）
        return q[pick];
    }

    /// <summary>候補の 1 枚目が 無関係（0）／ 片端（1）／ 両端・誰でも ＋ 1 語（2）／ 両端・2 語（3）だった回数（第453期・測るためだけ）。</summary>
    public int[] RelicFirstFit { get; } = new int[4];
    /// <summary>候補の 1 枚目に出た札の回数（第454期・測るためだけ）。</summary>
    public Dictionary<TraitId, int> RelicFirstShown { get; } = new();

    /// <summary>いま盤上（生きている駒・組んでいない既定の隊は定義のまま）と手札にいる駒のタグの語（軸と役割）。</summary>
    public HashSet<string> OwnedTags()
    {
        var tags = new HashSet<string>(Bench.Where(u => u.IsAlive).SelectMany(u => u.Def.Tags));
        foreach (Squad q in Squads)
        {
            if (q.Units is { } u) tags.UnionWith(u.Where(x => x.IsAlive).SelectMany(x => x.Def.Tags));
            else if (!q.Lost) tags.UnionWith(q.Def.F.Occupied().SelectMany(o => o.Def.Tags));
        }
        return tags;
    }

    /// <summary>
    /// 札が手持ちと噛む度合い（第453期）: <b>3 ＝ 両端・2 語</b>（引き金も効果も語のある端で、両方が手持ちのタグにある＝本当の繋ぎ）／
    /// <b>2 ＝ 両端・誰でも ＋ 1 語</b>（片方の端が「誰でも」で、語のある端が手持ちにある）／
    /// <b>1 ＝ 片端</b>（語のある端の 1 つだけが手持ちにある）／ <b>0 ＝ 無関係</b>。
    /// 指示書 §0-2 の「両端」は 3 と 2 の両方——<b>同じ「両端」なら 2 語の札を先に引く</b>（誰でもの端は誰でも満たすので、2 だけだと「繋ぐ」にならない札が先に来る）。
    /// </summary>
    /// <param name="axisOnly">第454期: 役割の語（タンク・ヒーラーなど）の端を「誰でも」と同じに扱う（軸の語だけで両端を見る）。</param>
    public static int RelicFit(TraitId relic, IReadOnlySet<string> own, bool axisOnly = false)
    {
        if (RelicBridges.Of(relic) is not RelicEnds e) return 0;
        var ends = new[] { e.Trigger, e.Effect }.Where(x => x.Length > 0 && (!axisOnly || UnitTags.Axes.Contains(x))).ToArray();
        int hit = ends.Count(own.Contains);
        if (ends.Length == 0 || hit == 0) return 0;
        if (hit < ends.Length) return 1;
        return ends.Length == 2 ? 3 : 2;
    }

    /// <summary>その札をいま持っている駒（隊・手札・倒れた駒のどこか・無ければ null）。</summary>
    public UnitState? RelicHolder(TraitId relic)
        => Squads.Where(q => q.Units is not null).SelectMany(q => q.Units!).Concat(Bench).FirstOrDefault(u => u.IsAlive && u.Relic == relic);

    /// <summary>候補の <paramref name="index"/> 番目を得る（-1 で見送る）。見送った札と選ばなかった札は列に戻さない。</summary>
    public bool ChooseRelic(int index)
    {
        if (RelicOffer.Count == 0 || index >= RelicOffer.Count) return false;
        if (index >= 0) { RelicHand.Add(RelicOffer[index]); RelicsGained++; }
        else RelicsDeclined++;
        RelicOffer.Clear();
        if (_relicPending > 0) { _relicPending--; OfferRelics(); }
        return true;
    }

    /// <summary>手持ちのレリック <paramref name="relic"/> を駒に持たせる（1 駒 1 枚・持っている札は手持ちに戻して置き換える）。</summary>
    public bool EquipRelic(UnitState u, TraitId relic)
    {
        if (!CanEquip(u) || !RelicHand.Contains(relic)) return false;
        RelicHand.Remove(relic);
        if (u.Relic is TraitId old) RelicHand.Add(old);
        ReplaceUnit(u, WithRelic(u, relic));
        return true;
    }

    /// <summary>駒からレリックを外して手持ちに戻す。</summary>
    public bool UnequipRelic(UnitState u)
    {
        if (!CanEquip(u) || u.Relic is not TraitId old) return false;
        RelicHand.Add(old);
        ReplaceUnit(u, WithRelic(u, null));
        return true;
    }

    /// <summary>
    /// レリックを付け替えた写し（第452期）。<b>`UnitState` の札の列は作るときにしか決まらない</b>ので、駒を作り直して置き換える
    /// （札の付け方は `BattleEngine.Materialize` と同じ `RelicCatalog.Attach`）。HP・席・私有のカウンタは写す（戦闘の外なので状態異常と強化は 0）。
    /// </summary>
    private static UnitState WithRelic(UnitState u, TraitId? relic)
    {
        var n = new UnitState
        {
            Def = u.Def, TeamId = u.TeamId, Shape = u.Shape, Slot = u.Slot, Hp = u.Hp, MaxHp = u.MaxHp,
            Traits = relic is TraitId r ? RelicCatalog.Attach(u.Def.Traits, r) : TraitCatalog.Resolve(u.Def.Traits),
            Relic = relic,
            HasFallenBack = u.HasFallenBack, ActionIndex = u.ActionIndex, LastDeathTurn = u.LastDeathTurn, TakenTurn = u.TakenTurn,
        };
        foreach (var (k, v) in u.Counters) n.Counters[k] = v;
        return n;
    }

    /// <summary>駒を写しに置き換える（隊・手札・倒れた駒の列のどこにいても）。</summary>
    private void ReplaceUnit(UnitState from, UnitState to)
    {
        foreach (Squad q in Squads)
            if (q.Units is { } us && us.IndexOf(from) is int i && i >= 0) us[i] = to;
        if (Bench.IndexOf(from) is int b && b >= 0) Bench[b] = to;
        if (FallenUnits.IndexOf(from) is int f && f >= 0) FallenUnits[f] = to;
        if (_fallenSquad.Remove(from, out int sq)) _fallenSquad[to] = sq;   // 第463期: 倒れた駒の☆が上がったとき
    }

    /// <summary><c>Presets.Compare</c> の行ごとの駒の Id（第449期 §0-5・噛む駒の判定）。</summary>
    private static readonly HashSet<string>[] CompareRows
        = Presets.Compare.Select(r => r.F.Occupied().Select(o => o.Def.Id).ToHashSet()).ToArray();

    /// <summary>
    /// いま盤上・手札にいる（生きている・<b>組んでいない既定の隊は定義のまま</b>）駒と、<c>Presets.Compare</c> の同じ行に載る駒の Id（手持ち自身は除く）。
    /// </summary>
    public HashSet<string> MatchIds()
    {
        var own = new HashSet<string>(Bench.Where(u => u.IsAlive).Select(u => u.Def.Id));
        foreach (Squad q in Squads)
        {
            if (q.Units is { } u) own.UnionWith(u.Where(x => x.IsAlive).Select(x => x.Def.Id));
            else if (!q.Lost) own.UnionWith(q.Def.F.Occupied().Select(o => o.Def.Id));
        }
        var mates = new HashSet<string>();
        foreach (HashSet<string> row in CompareRows)
            if (row.Overlaps(own)) mates.UnionWith(row);
        mates.ExceptWith(own);
        return mates;
    }

    /// <summary>
    /// 候補の 1 枚目（第449期 §0-5）。<see cref="MapStage.MatchOffer"/> の盤では、列のうち<b>手持ちと噛む駒（<see cref="MatchIds"/>）の最初の 1 枚</b>を抜き出す
    /// （ほかの駒の列の並びは変えない）。無ければ・偽の盤ではふつうの引き（<see cref="NextOffer"/>）。乱数は引かない。
    /// </summary>
    private UnitDef? FirstOffer()
    {
        if (!Stage.MatchOffer) return NextOffer();
        HashSet<string> mates = MatchIds();
        int n = _offerQueue.Count;
        UnitDef? hit = null;
        for (int i = 0; i < n; i++)
        {
            UnitDef d = _offerQueue.Dequeue();
            if (hit is null && mates.Contains(d.Id) && !OnBoardOrHand(d) && Offers.All(o => o.Id != d.Id)) { hit = d; continue; }
            _offerQueue.Enqueue(d);
        }
        return hit ?? NextOffer();
    }

    /// <summary>候補の列から、盤上・手札・候補にいない次の 1 枚（尽きたら null）。飛ばした駒は列に戻さない。</summary>
    /// 第467期: 投資のあるステージでは、先に投資の枠を引く（<see cref="InvestOffer"/>・外れたら今の引き方）。
    private UnitDef? NextOffer()
    {
        if (InvestLog.Count > 0 && InvestOffer() is { } inv) return inv;
        while (_offerQueue.Count > 0)
        {
            UnitDef d = _offerQueue.Dequeue();
            if (OfferEligible(d)) return d;
        }
        return null;
    }

    /// <summary>候補に出せる駒か（第467期に <see cref="NextOffer"/> の条件を外へ出しただけ）。</summary>
    private bool OfferEligible(UnitDef d)
        => (!OnBoardOrHand(d) || (Stage.Stars.StackOffers && Stackable(d))) && Offers.All(o => o.Id != d.Id);   // 第463期: ☆のあるステージでは持っている駒も（☆3 は出さない）・第464期の強化の盤では持っていない駒だけ

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
        // 第463期 §1-3: ☆のあるステージでは、持っている駒と同じキャラは<b>すぐに持っている駒へ重なる</b>（手札に 2 体目は来ない・倒れた駒にも重なる）。
        if (Stage.Stars.StackOffers && Stackable(d) && OwnedUnit(d.Id) is { } owned) StackOnto(owned);
        else Bench.Add(Stage.Stars.On ? Star1(d) : One(d));
        Recruits++;
        Offers.RemoveAt(offer);
        _investIds.Remove(d.Id);   // 第467期
        if ((offer == 0 ? FirstOffer() : NextOffer()) is { } next) { Offers.Insert(offer, next); NoteOffer(next); }
        // 第463期: 呼んだキャラは☆3 になるまで列の最後へ戻す（列は全キャラを同じ重みで 1 周する）。
        // 第464期の強化の盤では戻さない——呼んだキャラは列から外れる（第462期まで・重ねは「強化」で）。
        if (Stage.Stars.StackOffers && StarOf(d.Id).Star < Stage.Stars.Max) _offerQueue.Enqueue(Stage.Stars.BaseOf(d));
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
        _investIds.Clear();   // 第467期
        for (int k = 0; k < Stage.Gold.Offers; k++) if ((k == 0 ? FirstOffer() : NextOffer()) is { } d) AddOffer(d);
        foreach (UnitDef d in old) _offerQueue.Enqueue(d);
        // 列の残りが候補の枚数に満たなかったときは、戻した駒から足りないぶんを補う。
        while (Offers.Count < Stage.Gold.Offers && NextOffer() is { } more) AddOffer(more);
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
        Earn(SellPrice(u), $"{u.Def.Name}を手放した");
        Sold++;
        // 第463期 §1-3: ☆を捨てる。☆3 で列から外れていたキャラは列の最後へ戻す。
        if (Stage.Stars.On && Stars.Remove(u.Def.Id) && _offerQueue.All(q => q.Id != u.Def.Id) && Offers.All(o => o.Id != u.Def.Id))
            _offerQueue.Enqueue(Stage.Stars.BaseOf(u.Def));
        return true;
    }

    /// <summary>絵の口 `enhance-press`（第467期 §2・画面の押し方の確かめ）だけ: 金を足す。<b>遊ぶ通し・器具・門の通しは呼ばない。</b></summary>
    internal void GrantGoldForCheck(int amount) => Earn(amount, "押し方の確かめ（絵の口）");

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

    // ---------------- ☆の重ね（第463期 §1・☆のあるステージ（1 章）だけ） ----------------

    /// <summary>キャラ（<c>Id</c>）ごとの☆と、次の☆へ重ねた枚数。<b>☆の無いステージでは空</b>。</summary>
    public Dictionary<string, StarState> Stars { get; } = new();
    /// <summary>候補に出た札の数 ／ そのうち重ねの札（持っている駒と同じキャラ）だった数（測るためだけ・候補に入った時点で数える）。</summary>
    public int OffersShown { get; private set; }
    public int StackOffersShown { get; private set; }
    /// <summary>重ねた枚数 ／ ☆が上がった回数（測るためだけ）。</summary>
    public int Stacks { get; private set; }
    public int StarUps { get; private set; }
    /// <summary>☆が上がった駒（上がった順・画面の短い印が読む）。</summary>
    public List<(string Name, int Star)> StarUpLog { get; } = new();

    /// <summary>そのキャラの☆（☆の無いステージ・持っていないキャラは☆の上限＝今の強さ）。</summary>
    public StarState StarOf(string id) => Stars.TryGetValue(id, out StarState s) ? s : new StarState(Stage.Stars.Max, 0);
    /// <summary>札の上の☆（「☆1（1／2）」・☆の無いステージは空）。</summary>
    public string StarText(UnitDef d) => Stage.Stars.On ? Stage.Stars.Text(StarOf(d.Id)) : "";
    /// <summary>呼ぶ候補の☆（重ねの札は「重ねる ☆1→（1／2）」・新しいキャラは「☆1」・☆の無いステージは空）。</summary>
    public string OfferStarText(UnitDef d) => !Stage.Stars.On ? "" : Stage.Stars.StackOffers && Stackable(d) ? Stage.Stars.StackText(StarOf(d.Id)) : "☆1";
    /// <summary>呼んだ後のその駒の定義（☆の写し・画面の候補の札の数値）。☆の無いステージは候補の定義そのもの。</summary>
    public UnitDef OfferDef(UnitDef d) => !Stage.Stars.On ? d : Stage.Stars.Def(d, Stage.Stars.StackOffers && Stackable(d) ? Stage.Stars.Add(StarOf(d.Id)).Star : 1);
    /// <summary>手放したときの金（☆のあるステージは ＋2 × 重ねた枚数）。</summary>
    public int SellPrice(UnitState u) => Stage.Gold.Sell * (Stage.Stars.On ? Stage.Stars.Copies(StarOf(u.Def.Id)) : 1);

    /// <summary>重ねられるキャラか（☆のあるステージで、持っていて（倒れていても）☆3 でない）。</summary>
    public bool Stackable(UnitDef d) => Stage.Stars.On && OnBoardOrHand(d) && Stars.ContainsKey(d.Id) && StarOf(d.Id).Star < Stage.Stars.Max;

    /// <summary>そのキャラの駒（隊・手札・倒れた駒のどこか・生きている駒を先に）。</summary>
    private UnitState? OwnedUnit(string id)
        => Squads.Where(q => q.Units is not null).SelectMany(q => q.Units!).Concat(Bench).Concat(FallenUnits)
            .Where(u => u.Def.Id == id).OrderByDescending(u => u.IsAlive).FirstOrDefault();

    /// <summary>☆1 の駒を作る（☆を 1 から数え直す）。</summary>
    private UnitState Star1(UnitDef d)
    {
        Stars[d.Id] = new StarState(1, 0);
        return One(Stage.Stars.Def(d, 1));
    }

    /// <summary>候補に 1 枚足す（重ねの札だったかを数える）。</summary>
    private void AddOffer(UnitDef d) { Offers.Add(d); NoteOffer(d); }
    private void NoteOffer(UnitDef d) { OffersShown++; if (Stackable(d)) StackOffersShown++; }

    // ---------------- 強化（第464期 §1-1・金で直に 1 枚重ねる） ----------------

    /// <summary>強化した回数（測るためだけ）。</summary>
    public int Enhances { get; private set; }

    /// <summary>
    /// その駒を強化できない理由（1 行・強化できれば null）。<b>強化できる場所は呼べる場所と同じ</b>——隊の駒はその隊を組み直せるとき（拠点・取っている中間拠点）、
    /// 手札と倒れた駒は金を動かせるとき（<see cref="CanTrade"/>）。倒れた駒も強化できる（倒れたまま☆が上がる）。☆3 と、強化の無い盤では出さない。
    /// </summary>
    public string? EnhanceBlock(UnitState? u)
    {
        if (u is null || !Stage.Stars.On || Stage.Stars.EnhancePrice <= 0) return "強化の無い盤";
        if (!Stars.TryGetValue(u.Def.Id, out StarState s) || !ReferenceEquals(OwnedUnit(u.Def.Id), u)) return "持っている駒ではない";
        if (s.Star >= Stage.Stars.Max) return $"☆{Stage.Stars.Max} は強化できない";
        if (Squads.FirstOrDefault(q => q.Units is { } us && us.Contains(u)) is { } sq && !CanReform(sq.Index))
            return $"{sq.Label} は道の上（強化は拠点・取っている中間拠点で）";
        if (!CanTrade) return "いまは金を使えない（拠点・取っている中間拠点に隊がいない）";
        if (Gold < Stage.Stars.EnhancePrice) return "金が足りない";
        return null;
    }

    public bool CanEnhance(UnitState? u) => EnhanceBlock(u) is null;

    /// <summary>強化の値段と、強化した後の☆の文（「強化 4 金（☆1 → 1／2）」・☆が上がる 1 枚は「強化 4 金（☆1（1／2）→ ☆2）」）。</summary>
    public string EnhanceText(UnitDef d)
    {
        StarState s = StarOf(d.Id), a = Stage.Stars.Add(s);
        string to = a.Star > s.Star ? $"☆{s.Star}{(s.Pips > 0 ? $"（{s.Pips}／{Stage.Stars.NeedAt(s.Star)}）" : "")} → ☆{a.Star}" : $"☆{s.Star} → {a.Pips}／{Stage.Stars.NeedAt(s.Star)}";
        return $"強化 {Stage.Stars.EnhancePrice} 金（{to}）";
    }

    /// <summary>
    /// 強化する（第464期 §1-1）。金を <see cref="StarRule.EnhancePrice"/> 払い、持っている駒に同じキャラを 1 枚重ねる（第463期の重ね方のまま・☆が上がれば HP は割合を保つ）。
    /// 返すのは強化した後の駒（☆が上がれば写しに置き換わる・強化できなければ null）。
    /// </summary>
    public UnitState? Enhance(UnitState u)
    {
        if (!CanEnhance(u)) return null;
        Spend(Stage.Stars.EnhancePrice, $"{u.Def.Name}を強化した");
        Enhances++;
        StackOnto(u);
        return OwnedUnit(u.Def.Id);
    }

    /// <summary>
    /// 持っている駒に 1 枚重ねる（第463期 §1-3）。☆が上がれば定義を☆の写しに替えた駒に置き換える——<b>今の HP は割合を保つ</b>（満タンなら満タン・倒れた駒は倒れたまま）。
    /// 席・レリック・私有のカウンタは写す（`WithRelic` と同じ）。
    /// </summary>
    private void StackOnto(UnitState u)
    {
        StarState before = StarOf(u.Def.Id), after = Stage.Stars.Add(before);
        Stars[u.Def.Id] = after;
        Stacks++;
        if (after.Star <= before.Star) return;
        StarUps++;
        StarUpLog.Add((u.Def.Name, after.Star));
        UnitDef def = Stage.Stars.Def(u.Def, after.Star);
        int hp = u.Hp <= 0 ? 0 : Math.Max(1, (int)Math.Round((double)u.Hp * def.MaxHp / u.MaxHp, MidpointRounding.AwayFromZero));
        var n = new UnitState
        {
            Def = def, TeamId = u.TeamId, Shape = u.Shape, Slot = u.Slot, Hp = Math.Min(hp, def.MaxHp), MaxHp = def.MaxHp,
            Traits = u.Relic is TraitId r ? RelicCatalog.Attach(def.Traits, r) : TraitCatalog.Resolve(def.Traits),
            Relic = u.Relic,
            HasFallenBack = u.HasFallenBack, ActionIndex = u.ActionIndex, LastDeathTurn = u.LastDeathTurn, TakenTurn = u.TakenTurn,
        };
        foreach (var (k, v) in u.Counters) n.Counters[k] = v;
        ReplaceUnit(u, n);
    }

    public int ClearedCount => Nodes.Sum(r => r.Count(n => n.Cleared && !n.Retreated));
    public bool AllCleared => ClearedCount >= Stage.TotalNodes;

    /// <summary>出せる隊が1つも残っていない（盤上に居らず、かつ出していないわけでもない）。</summary>
    public bool NoSquadsLeft => Squads.All(s => s.Units is null && (s.Lost || s.Deployed));

    /// <summary>盤上に敵部隊が1つでも残っているか（<b>湧いたものも数える</b>）。</summary>
    public bool FoesLeft => Nodes.Any(r => r.Any(n => !n.Cleared)) || Spawns.Any(n => !n.Cleared) || ReserveWaiting;

    /// <summary>経路の名前（<b>-1 は敵の拠点</b>——待機中の予備隊は経路を持たない・第441期）。</summary>
    public string LaneName(int road) => road < 0 ? Map11.PortalName : Stage.LaneNames[road];

    public bool Finished => (Portal.On ? Won : AllCleared) || NoSquadsLeft || Battles >= BattleCapNow
                            || Fallen || (Time.On && Turn >= Stage.TurnCap && !HeroClock) || HeroStalled;

    /// <summary>
    /// 制圧の後の勇者が時計になっているか（第455期 §0・<see cref="HeroRule.AfterCapture"/> の盤で制圧した後）。
    /// 真の間は刻の期限（<see cref="MapStage.TurnCap"/>）で終わらない——終わるのは勇者を倒すか、拠点が陥落するか、隊が尽きるか。
    /// </summary>
    public bool HeroClock => Stage.Hero.On && Stage.Hero.AfterCapture && Captured;

    /// <summary>勇者が現れてから <see cref="Map11.HeroStallTurns"/> 刻たっても決着しない（器具の打ち切り・ふつうは起きない）。</summary>
    public bool HeroStalled => HeroClock && HeroTurn >= 0 && Turn >= HeroTurn + Map11.HeroStallTurns && !Won && !Fallen;

    /// <summary>勝つのに勇者を倒す必要があり、まだ倒していない（第455期）。それ以外の盤では常に偽。</summary>
    public bool HeroStanding => Stage.Hero.On && Stage.Hero.AfterCapture && Hero is not { Cleared: true };

    /// <summary>
    /// 勇者パーティーが着いて負けた（第444期 §0-3）。<b>上限を勇者の予告にした盤（<see cref="MapStage.HeroAtCap"/>）で、
    /// 刻の上限に達したときに勝っていなければ真。</b>勇者そのものはまだ盤に出ない（段C）。
    /// </summary>
    public bool HeroArrived => Stage.HeroAtCap && Time.On && Turn >= Stage.TurnCap && !Won && !Fallen;

    /// <summary>勇者パーティーが現れるまでの刻（予告の札「あと n 刻」）。予告の無い盤では null。</summary>
    public int? HeroTurnsLeft => Stage.HeroAtCap && Time.On ? Math.Max(0, Stage.TurnCap - Turn) : null;

    /// <summary>「n 作戦ターン以内に制圧」の残り（第447期・勝ち条件の文を出す盤だけ）。</summary>
    public int? TimeLimitLeft => Stage.TimeLimit && Time.On && !HeroClock ? Math.Max(0, Stage.TurnCap - Turn) : null;

    /// <summary>時間切れで負けた（第447期・勝ち条件の文を出す盤で、上限の刻に勝っていない）。第455期: 制圧の後の勇者の盤では期限を外す。</summary>
    public bool TimeUp => Stage.TimeLimit && Time.On && Turn >= Stage.TurnCap && !Won && !Fallen && !HeroClock;

    /// <summary>
    /// 勝ち。<b>拠点ありでは「制圧した ＋ 盤上の敵部隊が 0」</b>（第176期 §1-4）
    /// ——道を抜くだけでは終わらない。拠点なしでは第175期のまま。
    /// </summary>
    /// 第455期: 制圧の後に勇者が現れる盤（<see cref="HeroRule.AfterCapture"/>）では、<b>勇者を倒すまで勝ちにならない</b>。
    public bool Won => Portal.On ? (Captured && (Stage.WinOnCapture || !FoesLeft) && !Fallen && !HeroStanding) : (AllCleared && !Fallen);

    /// <summary>盤に残っている敵部隊の数（第445期: 制圧で勝ったとき「退いた敵部隊」として結果に出す）。</summary>
    public int FoesRemaining => Nodes.Sum(r => r.Count(n => !n.Cleared)) + Spawns.Count(n => !n.Cleared) + (ReserveWaiting ? 1 : 0);

    /// <summary>道 <paramref name="road"/> で次に当たる区画。道が抜け切っていれば null。</summary>
    /// 第449期: <b>動かない区画（<see cref="Map11.RoadNode.Stationary"/>）は飛ばす</b>——前進するのは次の動く区画。
    /// 第458期: <b>動かない大群（<see cref="Map11.RoadNode.Hold"/>）も飛ばす</b>。
    public Node? NextNode(int road)
    {
        foreach (Node n in Nodes[road]) if (!n.Cleared && !n.Def.Stationary && !n.Def.Hold) return n;
        return null;
    }

    /// <summary>動かない大群（<see cref="Map11.RoadNode.Hold"/>）のいる盤か（第458期・1-3）。偽の盤では <see cref="FoesOn"/> の並びは第457期のまま。</summary>
    private readonly bool _holds;

    // ---------------- 道の上の敵（第176期。**拠点なしでは湧きが 0 なので第175期と同義**） ----------------

    /// <summary>
    /// その道に残っている敵部隊を<b>手前（拠点に近い）順</b>に返す。
    /// <b>固定の4部隊は必ず湧きより手前にいる</b>——湧きは敵の拠点から出て、
    /// 前が詰まっていれば進めない（<see cref="AdvanceFoes"/>）ので、この並びが位置の順になる。
    /// </summary>
    /// 第449期: 動かない区画（敵の拠点の守備隊）は<b>いちばん奥</b>なので最後に返す。
    /// 第458期: 大群のいる盤では<b>経路の上の位置の順</b>に並べ直す（湧いた部隊は大群の奥で詰まるので、元の並びでは「手前」が崩れる）。
    /// 同じ位置は元の並び（区画 → 湧き → 守備隊）のまま。大群のいない盤は 1 行も変えていない。
    public IEnumerable<Node> FoesOn(int road)
        => _holds ? FoesOnRaw(road).OrderBy(n => Graph.IndexOn(road, n.Pos)) : FoesOnRaw(road);

    private IEnumerable<Node> FoesOnRaw(int road)
    {
        foreach (Node n in Nodes[road]) if (!n.Cleared && !n.Def.Stationary) yield return n;
        foreach (Node n in Spawns) if (!n.Cleared && n.Def.Road == road) yield return n;
        foreach (Node n in Nodes[road]) if (!n.Cleared && n.Def.Stationary) yield return n;
    }

    /// <summary>その道でいちばん拠点に近い敵部隊。<b>拠点なしでは <see cref="NextNode"/> と同じもの。</b></summary>
    /// 第459期: <b>追ってくる勇者は数えない</b>——勇者は隊の後ろ（拠点の側）から来ることがあり、「先頭の敵は隊より奥にいる」を崩す。
    /// 勇者とは<b>同じマスにいるときだけ</b>当たる（<see cref="ChaserAt"/>・<see cref="FoeFacing"/>）。追ってくる勇者のいない盤では 1 行も変わらない。
    public Node? FrontFoe(int road) => Stage.Hero.Chaser ? FoesOn(road).FirstOrDefault(n => !n.IsHero) : FoesOn(road).FirstOrDefault();

    /// <summary>そのマスに立っている追ってくる勇者（第459期・拠点と本陣のマスは除く——拠点は迎撃、本陣は守りの後ろに並ぶ）。いなければ null。</summary>
    public Node? ChaserAt(int cell)
        => Stage.Hero.Chaser && Hero is { Cleared: false } h && h.Pos == cell && cell != Graph.Home && cell != Graph.Portal ? h : null;

    /// <summary>そのマスにいる敵部隊（道 <paramref name="road"/> の手前の順で最初の1つ）。</summary>
    public Node? FoeAt(int road, int cellId) => FoesOn(road).FirstOrDefault(n => n.Pos == cellId);

    /// <summary>敵の拠点のマスにいる敵部隊（<b>道をまたいで数える</b>——北と南はそこで合流する）。</summary>
    public Node? PortalFoe()
    {
        if (!Portal.On) return null;
        // 第456期: 門番の勇者が立っていれば、本陣の守り → 勇者 → 湧き・予備隊の順（門番の盤だけ・ほかの盤は下の順のまま）。
        if (Stage.Hero.Gatekeeper && Hero is { Cleared: false } keeper && keeper.Pos == Graph.Portal)
            return Nodes.SelectMany(r => r).FirstOrDefault(n => !n.Cleared && n.Def.Stationary && n.Pos == Graph.Portal) ?? keeper;
        // 第459期 §1-6: 追ってくる勇者が本陣のマスに入ってきたら、ほかの相手の後ろに並ぶ（守りを抜けば制圧が先に成る・`PortalBlocker`）。
        if (Stage.Hero.Chaser && Hero is { Cleared: false } chaser && chaser.Pos == Graph.Portal)
            return PortalBlocker() ?? chaser;
        for (int road = 0; road < Stage.LaneCount; road++)
            foreach (Node n in FoesOn(road))
                if (n.Pos == Graph.Portal) return n;
        // 第441期 §3: 待機中の予備隊も敵の拠点にいる（制圧するには先に倒すか、出発させてから入る）。
        return ReserveWaiting ? Reserve : null;
    }

    /// <summary>
    /// 制圧を止める本陣の敵（第459期）。<see cref="PortalFoe"/> から<b>追ってくる勇者だけを除いた</b>もの——勇者が本陣にいても、守りを抜けば制圧になる（逃げ切り）。
    /// 追ってくる勇者の盤でなければ <see cref="PortalFoe"/> と同じ。
    /// </summary>
    public Node? PortalBlocker()
    {
        if (!Stage.Hero.Chaser || Hero is not { Cleared: false } h || h.Pos != Graph.Portal) return PortalFoe();
        for (int road = 0; road < Stage.LaneCount; road++)
            foreach (Node n in FoesOn(road))
                if (n.Pos == Graph.Portal && !n.IsHero) return n;
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
        // 第459期: 追ってくる勇者と同じマスにいれば、その勇者に当たる（勇者は「先頭の敵」に数えない）。
        if (ChaserAt(s.Pos) is { } chaser) return chaser;
        // 第458期: 経路に属さない通り道（1-3 の分かれ道）では、どの経路の敵でも同じマスにいれば当たる（素通りしない）。
        if (s.Pos != Graph.Home && Graph[s.Pos].Lane < 0)
            for (int road = 0; road < Stage.LaneCount; road++)
            {
                if (FrontFoe(road) is { } here && here.Pos == s.Pos) return here;
                if (Stage.CrossFacing && FoeAt(road, s.Pos) is { } any) return any;   // 第460期 §B
            }
        Node? f = FrontFoe(s.Road);
        if (f is not null && f.Pos == s.Pos) return f;
        // 第460期 §B（横の辺の穴の直し・`MapStage.CrossFacing`）: 先頭でなくても、隊のいるマスに敵がいれば当たる（並びは `FoesOn` のまま）。
        return Stage.CrossFacing ? FoeAt(s.Road, s.Pos) : null;
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
        if (!Portal.On || Captured || PortalBlocker() is not null) return;
        foreach (Squad s in Squads)
            if (s.Units is not null && s.Pos == Graph.Portal && s.Units.Any(u => u.IsAlive))
            {
                Captured = true;
                CapturedTurn = Turn;
                if (Stage.Hero.On && Stage.Hero.AfterCapture) RetreatAll();   // 第455期: 残りの敵部隊は退く（勇者だけが残る）
                // 第177期 §1: **制圧した時点でポータルは味方のものになる**ので、
                // リキャストは味方の <see cref="PortalRule.WarpEvery"/> から数え直す
                // ——敵の湧きの残り（最大 S ターン）を引き継がない。
                // <b>制圧は「使う」ことではない</b>ので、その場で1回は使える。
                _portalReady = Turn;
                return;
            }
    }

    // ---------------- 本陣の連戦（第457期 §0-1・`SiegeRule`） ----------------

    /// <summary>
    /// 本陣で続けて戦える次の相手（第457期）。連戦の盤で、その隊が敵の拠点のマスにいて生きた駒があり、
    /// まだそこに敵がいれば <see cref="PortalFoe"/>（守り → 勇者 → 湧き・予備隊の順）を返す。それ以外は null。<b>盤面は1ビットも動かさない。</b>
    /// </summary>
    /// 第458期: <see cref="SiegeRule.Stacks"/> の盤では、<b>敵が重なっているマス</b>（1-3 の大群）で勝ってまだそこに敵がいるときも連戦の合間になる（相手は <see cref="FoeFacing"/>）。
    public Node? SiegeNext(int squadIndex)
    {
        if (!Stage.Siege.On || !Portal.On || Finished) return null;
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is not { } u || !u.Any(x => x.IsAlive)) return null;
        if (s.Pos != Graph.Portal)
            return Stage.Siege.Stacks && s.Pos != Graph.Home && FoeFacing(squadIndex) is { Cleared: false } h ? h : null;
        return PortalFoe() is { Cleared: false } f ? f : null;
    }

    /// <summary>勇者を倒した刻（第457期・-1 ＝ まだ・測るためだけ）。</summary>
    public int HeroDownTurn { get; private set; } = -1;

    /// <summary>本陣の連戦の合間に引いた回数（第457期・測るためだけ）。第458期から重なったマス（大群）で引いた回数も含む。</summary>
    public int SiegeRetreats { get; private set; }

    /// <summary>そのうち重なったマス（大群）で引いた回数（第458期・測るためだけ）。</summary>
    public int StackRetreats { get; private set; }

    /// <summary>横の辺（経路をまたぐ辺・<see cref="MapGraph.IsCross"/>）を通った回数（第458期・測るためだけ）。</summary>
    public int CrossMoves { get; private set; }
    /// <summary>横の辺で入ったマスで、経路の先頭でない敵に当たった回数（第460期・直した後だけ起きる・測るためだけ）。</summary>
    public int CrossContacts { get; private set; }
    /// <summary>横の辺で入ったマスに敵がいたのに当たらなかった回数（第460期・直す前の穴・測るためだけ）。</summary>
    public int CrossSkips { get; private set; }
    /// <summary>追ってくる勇者が、味方の取っている中間拠点のマスを通った回数（第460期 §4・規則は変えない・測るためだけ）。</summary>
    public int HeroOverHeld { get; private set; }
    /// <summary>そのとき取り返されていたら門が閉じていた（取っている中間拠点が門の数を割った）か（第460期 §4・測るためだけ）。</summary>
    public bool HeroWouldCloseGate { get; private set; }

    /// <summary>
    /// 本陣から引く（第457期）。隊を<b>入ってきた経路の中間拠点</b>（経路の奥のマス）へ戻す——1 刻かからない・無料。
    /// 中間拠点が味方のものなら組み直し・呼ぶ・休むができる（<see cref="AtOutpost"/> の規則のまま）。本陣の敵はそのまま（HP は持ち越し）。
    /// </summary>
    public bool SiegeRetreat(int squadIndex)
    {
        if (!Stage.Siege.On) return false;
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is null || s.Pos == Graph.Home) return false;
        int road = s.Road >= 0 && s.Road < Stage.LaneCount ? s.Road : 0;
        // 第458期: 重なったマス（大群）からは、入ってきた側の隣（経路の上で 1 つ拠点寄り・1-3 の C3 なら野営地）へ。
        if (s.Pos != Graph.Portal)
        {
            if (!Stage.Siege.Stacks) return false;
            Place(s, Graph.TowardHome(road, s.Pos), road);
            SiegeRetreats++;
            StackRetreats++;
            return true;
        }
        Place(s, Graph.Lanes[road][^1], road);
        SiegeRetreats++;
        return true;
    }

    /// <summary>その隊は制圧した第2拠点にいるか（<b>休む・組み直しができる</b>）。</summary>
    public bool AtSecondBase(int squadIndex)
        => Captured && Squads[squadIndex].Units is not null && Squads[squadIndex].Pos == Graph.Portal;

    /// <summary>本陣の門が開いているか（第451期 §0-1・門の無い盤では常に真）。<b>入れるかの判定はここ1箇所</b>。</summary>
    public bool GateOpen => Stage.PortalGate <= 0 || HeldOutposts.Count >= Stage.PortalGate;

    /// <summary>門が開くまでに取る中間拠点の残り（門の無い盤・開いていれば 0）。</summary>
    public int GateNeed => Math.Max(0, Stage.PortalGate - HeldOutposts.Count);

    /// <summary>その隊はいまそのマスへ入れるか（第451期: 門が閉じている間は敵の拠点のマスへ新しく入れない）。</summary>
    public bool CanEnter(int squadIndex, int cell) => cell != Graph.Portal || GateOpen || Squads[squadIndex].Pos == Graph.Portal;

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
            // 第455期: 制圧の後の勇者は中間拠点を取り返さない（味方の拠点だけを目指す）。第459期: 追ってくる勇者も取り返さない（隊だけを追う）。
            bool foe = FoesOn(lane).Any(n => n.Pos == cell && !(n.IsHero && (Stage.Hero.AfterCapture || Stage.Hero.Chaser)));
            int mine = -1;
            for (int i = 0; i < Squads.Length; i++)
                if (!Squads[i].Lost && Squads[i].Units is { } u && Squads[i].Pos == cell && u.Any(x => x.IsAlive)) { mine = i; break; }
            if (!HeldOutposts.Contains(cell))
            {
                if (mine < 0 || foe) continue;
                HeldOutposts.Add(cell);
                OutpostCaptures++;
                if (Stage.Relic.On && _relicCells.Add(cell)) OfferRelics();   // 第452期: 初めて取った中間拠点だけ
                Deliver(Stage.Arrival.OnOutpost, Graph[cell].Name, $"{Graph[cell].Name}を取った");
                Earn(Stage.Gold.Outpost, $"{Graph[cell].Name}を取った", cell);
                RelocateHomeless();   // 第459期: 拠点が落ちた後、拠点に取り残された隊（0 枚・枠を開けていない隊）をここへ
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
        if (s.Lost || s.Units is null || s.Road < 0 || HomeLost) return false;   // 第459期: 落ちた拠点へは戻せない
        Place(s, Graph.Home, -1);
        return true;
    }

    /// <summary>その隊を拠点へ戻せるか（道の上に出ている隊だけ）。</summary>
    public bool CanWithdraw(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        return !s.Lost && s.Units is not null && s.Road >= 0 && !HomeLost;
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
        // 第459期 §1-4: 拠点が落ちた後は拠点では組み直せない（取っている中間拠点でだけ）。
        return !s.Lost && !IsLocked(squadIndex) && ((s.Road < 0 && !(HomeLost && s.Pos == Graph.Home)) || AtForwardBase(squadIndex));
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
        node.Units ??= FoeUnits(node);
        ArmDoctrine(pu);   // 第468期: 盤の札（開戦時の席で数値を差す・盤の札が無ければ何もしない）
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
        if (node.IsHero) { HeroBattles++; HeroSquads.Add(squadIndex); _heroFoughtTurn = Turn; }   // 第454期（測るためだけ）・第455期: 当たった刻

        var aliveP = pu.Where(u => u.IsAlive).ToList();
        var aliveE = eu.Where(u => u.IsAlive).ToList();
        // 第452期 §1-2: 倒れた駒のレリックは手持ちに戻る（倒れた駒の列には札の無い写しを置く——呼び戻した駒には付いていない）。
        foreach (UnitState u in pu.Where(x => !x.IsAlive && x.Relic is not null).ToList())
        {
            RelicHand.Add(u.Relic!.Value);
            UnitState bare = WithRelic(u, null);
            int at = pu.IndexOf(u);
            pu[at] = bare;
        }
        // 第447期: 呼び戻しのある盤では、この戦闘で倒れた駒を記録する（境界で隊から外れるので、ここで拾わないと消える）。
        if (Stage.Gold.Revive > 0)
            foreach (UnitState u in pu)
                if (!u.IsAlive && RosterIds.Contains(u.Def.Id) && !FallenUnits.Contains(u))
                {
                    FallenUnits.Add(u);
                    _fallenSquad[u] = squadIndex;   // 第461期: 1 章の持ち越しで、倒れた駒を倒れた隊の席へ戻すため（規則は読まない）
                }
        node.HpNow = aliveE.Sum(u => u.Hp);

        if (aliveE.Count == 0)
        {
            node.Cleared = true;
            node.Units = null;
            node.HpNow = 0;
            if (node.Def.Road == s.Home && !node.FromPortal) s.Cleared++;
        }
        else node.Units = EngagementEngine.CrossBoundary(aliveE);
        if (node.IsHero) HeroHpTrail.Add(node.HpNow);   // 第455期: 勇者の HP の推移（結果画面・測るためだけ）
        if (node.IsHero && node.Cleared && HeroDownTurn < 0) HeroDownTurn = Turn;   // 第457期: 勇者を倒した刻（測るためだけ）

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
                           && (!intercept || Time.InterceptRecover)
                           && !(Stage.Siege.On && node.Pos == Graph.Portal)    // 第457期: 本陣の戦闘では道中の回復を乗せない
                           && !(Stage.Siege.Stacks && node.Def.Hold);         // 第458期: 大群の戦闘でも乗せない（連戦の窓は本陣と同じ規則）
            s.Units = EngagementEngine.CrossBoundary(aliveP, pu,
                recover ? new RecoverRule(Map11.RecoverPercent, false) : null);
            RepairSeats(s.Units);
        }

        // 敵の拠点のマスで最後の1つを抜いたら、その場で制圧になる（第176期 §1-3）。
        TryCapture();
        // 第444期: 中間拠点の取り合い → 抜いた見返り（先に中間拠点を取ってから、その隊がいる拠点へ届ける）。
        UpdateOutposts();
        if (node.Cleared && Stage.Arrival.OnKill > 0)
            Deliver(Stage.Arrival.OnKill, intercept ? "拠点" : DeliveryPlace(squadIndex), $"{node.Def.Name}を抜いた");
        // 第445期: 抜いた見返りの金（湧いた部隊は安い）。
        if (node.Cleared)
            Earn(KillGold(node), $"{node.Def.Name}を抜いた", node.Pos);
        DisarmDoctrine();   // 第468期: 盤の札の写しを元の定義へ（盤の札が無ければ何もしない）
    }

    /// <summary>同じ席の直し（第451期 §0-4）を何回したか（測るためだけの計数）。</summary>
    public int SeatRepairs { get; private set; }

    /// <summary>
    /// 同じ席の直し（第451期 §0-4・<b>不具合の修正・規則の変更ではない</b>）。戦闘の後、生きている駒が 2 体以上同じ席にいたら、
    /// 後ろの駒（隊の並びで後ろ）を同じ列の空いた席へ、無ければ空いた席の若い順へ移す。
    /// 戦闘の中の入れ替え（ヨミの「勢い余って」と他の移動の重なり・第450期の seed 4）で生きている駒が同じ席に残ることがある——
    /// 画面（<c>SeatsOf</c>）が席を辞書の鍵にしていて落ちるので、隊に戻す時点で 1 席 1 体に揃える。戦闘の中の振る舞いは触らない。
    /// </summary>
    private void RepairSeats(List<UnitState> units)
    {
        var taken = new HashSet<int>();
        foreach (UnitState u in units.Where(x => x.IsAlive && !x.Def.Traits.Contains(TraitId.Ephemeral)))
        {
            if (taken.Add(u.Slot)) continue;
            int to = FormationRules.PlayableSlotsOfRow(u.Row).Concat(FormationRules.PlayableSlots).FirstOrDefault(sl => !taken.Contains(sl), -1);
            if (to < 0) continue;
            u.Slot = to;
            taken.Add(to);
            SeatRepairs++;
        }
    }

    /// <summary>その敵部隊を抜いたときの金（第449期: 本陣の守りは <see cref="GoldRule.Garrison"/>・湧いた部隊は安い）。画面の札も同じ値を読む。</summary>
    public int KillGold(Node node)
        => node.IsHero ? Stage.Hero.Gold   // 第454期: 勇者
           : node.Def.Hold ? Stage.Gold.Horde   // 第458期: 大群は 1 部隊ずつ
           : node.Def.Stationary ? Stage.Gold.Garrison : node.FromPortal && !node.IsReserve ? Stage.Gold.SpawnKill : Stage.Gold.Kill;

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
            int next = Graph.NextStep(s.Pos, road, Graph.At(road, MaxCellOf(road)), road);   // 第458期: 経路ごとの奥（1-1 ／ 1-2 は MaxCell と同じ）
            if (FrontFoe(road) is { } front && front.Cell < Graph.IndexOn(road, next)) next = front.Pos;
            // 第451期: 本陣の門が閉じていれば、敵の拠点のマスの手前で止まる。
            if (!CanEnter(squadIndex, next)) return null;
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
        bool cross = Graph.IsCross(s.Pos, cell);
        if (cross) CrossMoves++;   // 第458期（測るためだけ）
        Place(s, cell, s.Road);
        Node? met = FoeFacing(squadIndex);
        // 第460期（測るためだけ）: 横の辺で入ったマスで、先頭でない敵に当たった ／ 敵がいるのに当たらなかった（直す前の穴）。
        if (cross)
        {
            if (met is not null && !ReferenceEquals(met, FrontFoe(s.Road))) CrossContacts++;
            else if (met is null && Enumerable.Range(0, Stage.LaneCount).Any(r => FoesOn(r).Any(n => n.Pos == cell))) CrossSkips++;
        }
        return met;
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
        if (dest.AtHome) return AtSecondBase(squadIndex) && !HomeLost;   // 第459期: 落ちた拠点へはワープできない
        return s.Pos == Graph.Home && dest.IsPortal && PortalFoe() is null;
    }

    /// <summary>戻る（拠点へ向かって 1 マス）。<b>一瞬では戻れない。</b></summary>
    public bool StepBack(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is null || s.Pos == Graph.Home) return false;
        // 第438期: 拠点への最短路を 1 歩（敵の拠点からは入ってきた経路へ出る）。
        int next = Graph.NextStep(s.Pos, s.Road, Graph.Home, -1);
        if (next == Graph.Home && HomeLost) return false;   // 第459期: 落ちた拠点へは入らない
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
        if (s.Pos == Graph.Home && HomeLost) return false;   // 第459期 §1-4: 落ちた拠点では休めない
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
        { LoseSecondBaseIfEmpty(); UpdateOutposts(); ArriveIfDue(); ReserveIfDue(); HeroIfDue(); return SpawnIfDue(list); }

        // 1 部隊ぶんの前進。**第175期の式をそのまま関数にしただけ**で、中身は1文字も変えていない。
        // 戻り値が偽 ＝ 拠点が陥落したので、その場で打ち切る。
        bool Step(Node n)
        {
            // 第459期 §1-4: 落ちた拠点に着いた敵はそこで止まる（迎撃も陥落も起きない）。
            if (HomeLost && n.Pos == Graph.Home) return true;
            // 第438期: 経路の上で 1 つ拠点寄りのマスへ（敵の拠点からは経路の奥のマスへ）。
            if (n.Pos != Graph.Home) n.Pos = Graph.TowardHome(n.Def.Road, n.Pos);
            if (n.Pos == Graph.Home)
            {
                BeginIntercept(n);
                // 第459期 §1-5: 追ってくる勇者が現れた後は、空の拠点に着いても負けにならない（拠点が落ちるだけ・`LoseHome`）。
                if (NextInterceptor(n) < 0) return LoseHome(n);
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
                if (sp.IsHero && Stage.Hero.Gatekeeper) continue;   // 第456期: 門番の勇者は動かない
                if (sp.IsHero && Stage.Hero.Chaser) continue;   // 第459期: 追ってくる勇者は経路の列の外で動く（下の `MoveChaser`）
                // 第455期: 制圧の後の勇者は、味方の隊と同じマスにいれば素通りせずその場で戦闘・拠点に居座れば 2 刻ごとに迎撃をやり直す。
                if (sp.IsHero && Stage.Hero.AfterCapture)
                {
                    if (sp.Pos == Graph.Home)
                    {
                        if ((Turn - HeroTurn) % Math.Max(1, Stage.Hero.StepEvery) == 0 && !Step(sp)) return list;
                        continue;
                    }
                    int with = SquadAt(sp.Pos);
                    if (with >= 0) { if (_heroFoughtTurn != Turn - 1) list.Add(new Encounter(with, sp, false)); continue; }   // 同じ刻にもう当たっていれば二度は当てない
                }
                if (sp.Pos == Graph.Home) continue;
                // 第454期: 勇者は StepEvery 刻に 1 マス（現れた刻からかぞえて）。
                if (sp.IsHero && Stage.Hero.StepEvery > 1 && (Turn - HeroTurn) % Stage.Hero.StepEvery != 0) continue;
                int target = Graph.TowardHome(road, sp.Pos);
                if (FoesOn(road).Any(x => !ReferenceEquals(x, sp) && x.Pos == target)) continue;
                if (!Step(sp)) return list;
            }
        }
        // 第459期 §1-2 ／ §1-4: 追ってくる勇者（経路の列の後・ほかの敵に詰まらない）。
        if (!MoveChaser()) return list;
        LoseSecondBaseIfEmpty();
        UpdateOutposts();
        // 第441期: 到着と予備隊の出発は**湧きの隣**（1 刻の終わり・敵が動いた後）。並びは 到着 → 予備隊 → 湧き。
        ArriveIfDue();
        ReserveIfDue();
        HeroIfDue();
        return SpawnIfDue(list);

        // 追ってくる勇者の 1 歩（第459期）。拠点が落ちる前は経路を拠点へ（`Step`・前が詰まっても待たない）、落ちた後はいちばん近い隊へ。
        // 隊と同じマスにいれば動かずに戦う（同じ刻にもう当たっていれば二度は当てない）。本陣のマスでは戦わない（守りの後ろに並ぶ）。
        // 戻り値が偽 ＝ 陥落（勇者が現れた後なので起きない・`Step` と同じ形にしておく）。
        bool MoveChaser()
        {
            if (!Stage.Hero.Chaser || Hero is not { Cleared: false } h || Turn <= HeroTurn) return true;
            if ((Turn - HeroTurn) % Math.Max(1, Stage.Hero.StepEvery) != 0) return true;
            int with = h.Pos == Graph.Home || h.Pos == Graph.Portal ? -1 : SquadAt(h.Pos);
            if (with >= 0) { if (_heroFoughtTurn != Turn - 1) list.Add(new Encounter(with, h, false)); return true; }
            if (!HomeLost) return Step(h);
            if (HeroChase() is not { } chase) return true;
            int next = chase.Next;
            if (next == h.Pos) return true;
            h.Pos = next;
            if (Graph[next].Lane >= 0) h.Def = h.Def with { Road = Graph[next].Lane };
            if (HeldOutposts.Contains(next))
            {
                HeroOverHeld++;   // 第460期 §4（測るためだけ・取り返しはしない）
                if (Stage.PortalGate > 0 && GateOpen && HeldOutposts.Count - 1 < Stage.PortalGate) HeroWouldCloseGate = true;
            }
            if (next == Graph.Portal && Squads.Any(q => !q.Lost && q.Pos == Graph.Portal && q.Units is { } qu && qu.Any(x => x.IsAlive)))
                HeroJoinedSiege++;   // 測るためだけ
            if (next != Graph.Portal && SquadAt(next) is var sq and >= 0) list.Add(new Encounter(sq, h, false));
            return true;
        }
    }

    /// <summary>
    /// 拠点が落ちた後に追ってくる勇者の行き先（第459期 §1-4・<b>盤面は1ビットも動かさない</b>）: <b>いちばん近い味方の隊</b>
    /// （横の辺も含めた盤の最短路の歩数・同じなら若い番号）と、そこへの次のマス・歩数。追う相手がいなければ null。
    /// </summary>
    public (int Squad, int Next, int Steps)? HeroChase()
    {
        if (!Stage.Hero.Chaser || !HomeLost || Hero is not { Cleared: false } h) return null;
        (int Squad, int Next, int Steps)? best = null;
        for (int i = 0; i < Squads.Length; i++)
        {
            Squad s = Squads[i];
            if (s.Lost || s.Units is not { } u || !u.Any(x => x.IsAlive)) continue;
            List<int> path = Graph.Path(h.Pos, h.Def.Road, s.Pos, s.Pos == Graph.Portal ? s.Via : -1);
            if (path.Count == 0) continue;
            int steps = path.Count - 1;
            if (best is null || steps < best.Value.Steps) best = (i, path.Count >= 2 ? path[1] : h.Pos, steps);
        }
        return best;
    }

    // ---------------- 作戦マップの勇者（第454期 §2） ----------------

    /// <summary>勇者（現れていなければ null）。</summary>
    public Node? Hero { get; private set; }
    /// <summary>門が初めて開いた刻（敵の前進の終わりで見る・開いていなければ -1）。</summary>
    public int GateOpenedTurn { get; private set; } = -1;
    /// <summary>勇者が現れた刻（-1 ＝ まだ）。</summary>
    public int HeroTurn { get; private set; } = -1;
    /// <summary>勇者と戦った回数（迎撃も含む・測るためだけ）。</summary>
    public int HeroBattles { get; internal set; }
    /// <summary>勇者に当てた隊（隊の番号・重ねない・測るためだけ）。</summary>
    public HashSet<int> HeroSquads { get; } = new();

    /// <summary>
    /// 勇者を出す（敵の前進の終わり・到着と予備隊の後・湧きの前）。門が初めて開いた刻を記録し、その <see cref="HeroRule.AfterGate"/> 刻後に
    /// 敵の拠点のマスへ立てる（行き先は予備隊と同じ選び方 <see cref="ReserveTarget"/>）。制圧の後・陥落の後は出さない。
    /// </summary>
    private void HeroIfDue()
    {
        if (!Stage.Hero.On || Stage.Hero.Unit is not UnitDef hero || !Portal.On || !Graph.HasPortal) return;
        if (GateOpenedTurn < 0 && Stage.PortalGate > 0 && GateOpen) GateOpenedTurn = Turn;
        // 第459期 §1-1: 門が開いた刻に、味方の拠点から FromHome 歩のマス（経路の上）へ。経路は `ChaserLane`。拠点へ向かってくる（`MoveChaser`）。
        if (Stage.Hero.Chaser)
        {
            if (Hero is not null || GateOpenedTurn < 0 || Captured || Fallen) return;
            int road = ChaserLane();
            var fc = Formation.Build(center: hero);
            Hero = new Node
            {
                Def = new Map11.RoadNode(road, Map11.RoadCells, hero.Name, fc, EnemyCatalog.Stages.Count - 1),
                Graph = Graph, FromPortal = true, IsHero = true, DefMaxHp = hero.MaxHp, HpNow = hero.MaxHp,
                Pos = Graph.At(road, Math.Clamp(Stage.Hero.FromHome - 1, 0, Graph.Lanes[road].Length - 1)),
            };
            Spawns.Add(Hero);
            HeroTurn = Turn;
            HeroHpTrail.Add(hero.MaxHp);
            return;
        }
        // 第456期 §0: 門が開いた刻（その刻の敵の前進の終わり）に、敵の拠点のマス（本陣の守りと同じマス）へ。動かない（門番）。
        // 経路の番号は本陣の守りの経路（戦闘の記録の「中の道 n.」）——敵の拠点のマスの相手は経路によらず `PortalFoe` が選ぶ。
        if (Stage.Hero.Gatekeeper)
        {
            if (Hero is not null || GateOpenedTurn < 0 || Captured || Fallen) return;
            int road = Nodes.SelectMany(r => r).FirstOrDefault(n => n.Def.Stationary)?.Def.Road ?? 0;
            var fg = Formation.Build(center: hero);
            Hero = new Node
            {
                Def = new Map11.RoadNode(road, Map11.RoadCells, hero.Name, fg, EnemyCatalog.Stages.Count - 1),
                Graph = Graph, FromPortal = true, IsHero = true, DefMaxHp = hero.MaxHp, HpNow = hero.MaxHp, Pos = Graph.Portal,
            };
            Spawns.Add(Hero);
            HeroTurn = Turn;
            HeroHpTrail.Add(hero.MaxHp);
            return;
        }
        // 第455期 §0: 本陣を制圧した刻（その刻の敵の前進の終わり）に、制圧した本陣の 1 つ手前（経路の奥のマス）へ。
        // 経路は予備隊と同じ選び方（味方の隊のいない経路 → 生存の少ない経路）。そのマスに隊がいれば、奥のマスが空いている経路へ替える。
        if (Stage.Hero.AfterCapture)
        {
            if (Hero is not null || !Captured || Fallen) return;
            int road = ReserveTarget();
            if (SquadAt(Graph.Lanes[road][^1]) >= 0)
                road = Enumerable.Range(0, Stage.LaneCount).Where(l => SquadAt(Graph.Lanes[l][^1]) < 0).DefaultIfEmpty(road).First();
            var fa = Formation.Build(center: hero);
            Hero = new Node
            {
                Def = new Map11.RoadNode(road, Map11.RoadCells, hero.Name, fa, EnemyCatalog.Stages.Count - 1),
                Graph = Graph, FromPortal = true, IsHero = true, DefMaxHp = hero.MaxHp, HpNow = hero.MaxHp, Pos = Graph.Lanes[road][^1],
            };
            Spawns.Add(Hero);
            HeroTurn = Turn;
            HeroHpTrail.Add(hero.MaxHp);
            return;
        }
        if (Hero is not null || GateOpenedTurn < 0 || Turn < GateOpenedTurn + Stage.Hero.AfterGate || Captured || Fallen) return;
        var f = Formation.Build(center: hero);
        var def = new Map11.RoadNode(ReserveTarget(), Map11.RoadCells, hero.Name, f, EnemyCatalog.Stages.Count - 1);
        Hero = new Node
        {
            Def = def, Graph = Graph, FromPortal = true, IsHero = true, DefMaxHp = hero.MaxHp, HpNow = hero.MaxHp, Pos = Graph.Portal,
        };
        Spawns.Add(Hero);
        HeroTurn = Turn;
    }

    /// <summary>
    /// 追ってくる勇者が現れる経路（第459期 §1-1・<b>乱数は引かない・盤面は1ビットも動かさない</b>）:
    /// <b>隊のいない経路</b>（無ければ全部）のうち、<b>敵の生存（枚数）の少ない経路</b>・同じなら<b>中</b>（経路の真ん中）→ 番号の若い経路。
    /// </summary>
    public int ChaserLane()
    {
        int lanes = Stage.LaneCount;
        var manned = new bool[lanes];
        foreach (Squad s in Squads)
            if (!s.Lost && s.Units is { } u && u.Any(x => x.IsAlive) && s.Road >= 0 && s.Road < lanes) manned[s.Road] = true;
        var pool = Enumerable.Range(0, lanes).Where(l => !manned[l]).ToList();
        if (pool.Count == 0) pool = Enumerable.Range(0, lanes).ToList();
        int Alive(int lane) => FoesOn(lane).Where(n => n.Pos != Graph.Portal)
            .Sum(n => n.Units?.Count(u => u.IsAlive) ?? n.Def.Enemy.Occupied().Count());
        int mid = lanes / 2;
        return pool.OrderBy(Alive).ThenBy(l => l == mid ? 0 : 1).ThenBy(l => l).First();
    }

    /// <summary>勇者の HP の推移（第455期・現れた時の最大 HP、以後は戦闘のたびの残り・結果画面と器具が読む）。</summary>
    public List<int> HeroHpTrail { get; } = new();
    /// <summary>勇者に最後に当たった刻（第455期・同じ刻に二度当てないため）。</summary>
    private int _heroFoughtTurn = -1;
    /// <summary>制圧した刻に退いた敵部隊の数（第455期・<see cref="HeroRule.AfterCapture"/> の盤だけ）。</summary>
    public int RetreatedCount { get; private set; }

    /// <summary>制圧した刻に、勇者のほかの敵部隊を退かせる（第455期・盤から消える・「抜いた」には数えない）。</summary>
    private void RetreatAll()
    {
        foreach (Node n in Nodes.SelectMany(r => r).Concat(Spawns).Concat(Reserve is { } rv && !Spawns.Contains(rv) ? new[] { rv } : Array.Empty<Node>()))
        {
            if (n.Cleared || n.IsHero) continue;
            n.Cleared = true;
            n.Retreated = true;
            RetreatedCount++;
        }
    }

    /// <summary>勇者のその後（結果画面の 1 行・表示だけ）。</summary>
    public string HeroOutcome
        => Stage.Hero.Chaser
           ? (Hero is null ? "現れなかった（門が開かなかった）"
              : Hero.Cleared ? "倒した"
              : Won ? (HomeLost ? "拠点を落とされたが、逃げ切った（制圧）" : "逃げ切った（制圧）")
              : HomeLost ? "拠点を落とされた（追われたまま）"
              : "まだ盤上にいる")
           : Stage.Hero.Gatekeeper
           ? (Hero is null ? "現れなかった（門が開かなかった）"
              : Hero.Cleared ? "倒した"
              : "本陣の前に立ったまま")
           : Stage.Hero.AfterCapture
           ? (Hero is null ? "現れなかった（制圧していない）"
              : Hero.Cleared ? "倒した"
              : Fallen ? "着かれた（拠点が陥落）"
              : NoSquadsLeft ? "倒せなかった（隊が尽きた）"
              : HeroStalled ? "倒せなかった（決着しないまま打ち切り）"
              : "まだ盤上にいる")
           : Hero is null ? "現れなかった"
           : Hero.Cleared ? "倒した"
           : Fallen ? "着かれた（拠点が陥落）"
           : Captured ? "逃げ切った（着く前に制圧）"
           : "まだ盤上にいる";

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

    /// <summary>
    /// その敵部隊が<b>いまの速さで</b>拠点に着くまでの作戦ターン（第459期 §2・<b>表示だけ</b>——敵の札の「拠点まで あと n」）。
    /// 動かない敵（本陣の守り・大群・門番の勇者・先頭でない最初からの部隊・待機中の予備隊）・抜いた敵・拠点に着いた敵は null。
    /// 前が詰まって待つ刻は数えない（位置からの歩数 × 歩く間隔）。
    /// </summary>
    public int? TurnsToHome(Node n)
    {
        if (!Time.On || Time.AdvanceEvery <= 0 || n.Cleared || n.Def.Stationary || n.Def.Hold || n.Pos == Graph.Home || n.Def.Road < 0) return null;
        if (n.IsHero && Stage.Hero.Gatekeeper) return null;
        if (n.IsHero && Stage.Hero.Chaser && HomeLost) return null;   // 第459期: 拠点を落とした後の勇者は隊を追う（`HeroChase`）
        if (!n.FromPortal && !ReferenceEquals(NextNode(n.Def.Road), n)) return null;
        int at = Graph.IndexOn(n.Def.Road, n.Pos);
        int steps = at != int.MinValue ? at + 1 : Math.Max(0, Graph.Path(n.Pos, n.Def.Road, Graph.Home, -1).Count - 1);
        return steps * Time.AdvanceEvery * (n.IsHero ? Math.Max(1, Stage.Hero.StepEvery) : 1);
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
                if (sp.IsHero && Stage.Hero.AfterCapture && SquadAt(sp.Pos) >= 0) continue;   // 第455期: 隊と同じマスの勇者は動かずに戦う
                if (sp.IsHero && Stage.Hero.Gatekeeper) continue;   // 第456期: 門番の勇者は動かない
                if (sp.IsHero && Stage.Hero.Chaser) continue;   // 第459期: 追ってくる勇者は下で（`MoveChaser` と同じ式）
                if (sp.IsHero && Stage.Hero.StepEvery > 1 && (Turn + 1 - HeroTurn) % Stage.Hero.StepEvery != 0) continue;   // 第454期（同じ式・次の刻）
                int target = Graph.TowardHome(road, sp.Pos);
                if (FoesOn(road).Any(x => !ReferenceEquals(x, sp) && PosOf(x) == target)) continue;
                list.Add((sp, target));
                moved[sp] = target;
            }
        }
        // 第459期: 追ってくる勇者（`AdvanceFoes` の `MoveChaser` と同じ式・隊と同じマスなら動かずに戦う）。
        if (Stage.Hero.Chaser && Hero is { Cleared: false } h && Turn + 1 > HeroTurn
            && (Turn + 1 - HeroTurn) % Math.Max(1, Stage.Hero.StepEvery) == 0
            && (h.Pos == Graph.Home || h.Pos == Graph.Portal || SquadAt(h.Pos) < 0))
        {
            if (!HomeLost) { if (h.Pos != Graph.Home) list.Add((h, Graph.TowardHome(h.Def.Road, h.Pos))); }
            else if (HeroChase() is { } c && c.Next != h.Pos) list.Add((h, c.Next));
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
        if (!Portal.On || !Captured || PortalBlocker() is null) return;
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
    /// <summary>
    /// 敵の部隊の駒を作る（初めて当たるときに 1 度だけ）。第462期: 1 章の倍率のかかった盤（<see cref="MapStage.FoeScale"/>）では、
    /// 勇者の毎手番の固定回復も同じ倍率にする（最大HP と攻撃は盤の定義がすでに掛けた後の値）。倍率の無い盤では第461期と同じ 1 行。
    /// </summary>
    private List<UnitState> FoeUnits(Node node)
    {
        List<UnitState> us = BattleEngine.Materialize(node.Def.Enemy, BattleContext.EnemyTeam, Map11.EnemyScale);
        return node.IsHero ? Stage.FoeScale.MendHero(us) : us;
    }

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
        node.Units ??= FoeUnits(node);
        ArmDoctrine(pu);   // 第468期: 盤の札
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
            LoseHome(node);   // 第459期: 追ってくる勇者が現れた後は拠点が落ちるだけ（負けではない）
    }

    /// <summary>部分点（第168期と同じ定義）＝ 抜いた部隊数 ＋ 残った部隊の削り。</summary>
    public double Partial()
    {
        double p = ClearedCount;
        foreach (Node[] road in Nodes)
            foreach (Node n in road)
                if ((!n.Cleared || n.Retreated) && n.DefMaxHp > 0)
                    p += (double)(n.DefMaxHp - n.HpNow) / n.DefMaxHp;
        return p;
    }
}
