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

    // ---------------- 時間（第174期） ----------------

    /// <summary>
    /// 道のマス数（第174期 §2-1）。<b>拠点と1つ目の敵のあいだに空きマスを1つ足した。</b>
    /// マス 0 が空き、マス 1・2 が敵部隊の初期位置（区画の番号 + 1）。拠点は -1。
    /// </summary>
    public const int RoadCells = 3;

    /// <summary>1 マップの作戦ターンの上限（戦闘回数の上限とは別の歯止め）。</summary>
    public const int TurnCap = 60;

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
public readonly record struct PortalRule(bool On, int SpawnEvery, int BattleCap = 120)
{
    /// <summary>敵の拠点なし（第175期の規則そのもの）。</summary>
    public static readonly PortalRule Off = new(false, 0, Map11.BattleCap);

    public static PortalRule Every(int s, int battleCap = 120) => new(true, s, battleCap);

    public override string ToString() => On ? $"S={SpawnEvery}" : "拠点なし";
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
        /// <summary>盤上に出ている駒。<c>null</c> ＝ まだ出ていない（控え）／全滅した。</summary>
        public List<UnitState>? Units { get; set; }
        /// <summary>向かっている道。-1 ＝ 拠点で待機。</summary>
        public int Road { get; set; } = -1;
        /// <summary>
        /// 道の上のマス（第174期）。<b>-1 ＝ 拠点</b>で、0 が空きマス、1・2 が敵部隊の初期位置。
        /// <b><c>Road</c> と必ず同時に動く</b>（<c>Road &gt;= 0</c> ⇔ <c>Cell &gt;= 0</c>）。
        /// <b>時間を切った設定では誰も読まない。</b>
        /// </summary>
        public int Cell { get; set; } = -1;
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
        public required Map11.RoadNode Def { get; init; }
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
        /// <summary>
        /// いまいるマス（第174期）。初期値は <see cref="Map11.StartCellOf"/>。
        /// <b>-1 ＝ 拠点まで来た</b>（迎撃戦）。<b>時間を切った設定では誰も読まない。</b>
        /// </summary>
        public int Cell { get; set; }
        /// <summary>
        /// この到着の迎撃でもう出した隊（同じ隊が1回の到着で何度も出ないようにするだけ）。
        /// <b>区画ごとに持つ</b>——2 本の道が同じ作戦ターンに拠点へ着くことがある。
        /// </summary>
        public HashSet<int> Tried { get; } = new();
    }

    public int Seed { get; }
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

    public Map11State(int seed, TimeRule? time = null, PortalRule? portal = null)
    {
        Seed = seed;
        Time = time ?? TimeRule.Off;
        Portal = portal ?? PortalRule.Off;
        Bench = Map11.Reserves
            .Select(d => BattleEngine.Materialize(Formation.Build(front1: d), BattleContext.PlayerTeam)[0])
            .ToList();
        Squads = Map11.Squads.Select((d, i) => new Squad { Def = d, Index = i }).ToArray();
        Nodes = Map11.Roads.Select(r => r.Select(n =>
        {
            int hp = n.Enemy.Occupied().Sum(o => o.Def.MaxHp);
            return new Node { Def = n, DefMaxHp = hp, HpNow = hp, Cell = Map11.StartCellOf(n.Index) };
        }).ToArray()).ToArray();
    }

    public int ClearedCount => Nodes.Sum(r => r.Count(n => n.Cleared));
    public bool AllCleared => ClearedCount >= Map11.TotalNodes;

    /// <summary>出せる隊が1つも残っていない（盤上に居らず、かつ出していないわけでもない）。</summary>
    public bool NoSquadsLeft => Squads.All(s => s.Units is null && (s.Lost || s.Deployed));

    /// <summary>盤上に敵部隊が1つでも残っているか（<b>湧いたものも数える</b>）。</summary>
    public bool FoesLeft => Nodes.Any(r => r.Any(n => !n.Cleared)) || Spawns.Any(n => !n.Cleared);

    public bool Finished => (Portal.On ? Won : AllCleared) || NoSquadsLeft || Battles >= BattleCapNow
                            || Fallen || (Time.On && Turn >= Map11.TurnCap);

    /// <summary>
    /// 勝ち。<b>拠点ありでは「制圧した ＋ 盤上の敵部隊が 0」</b>（第176期 §1-4）
    /// ——道を抜くだけでは終わらない。拠点なしでは第175期のまま。
    /// </summary>
    public bool Won => Portal.On ? (Captured && !FoesLeft && !Fallen) : (AllCleared && !Fallen);

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

    /// <summary>そのマスにいる敵部隊（手前の順で最初の1つ）。</summary>
    public Node? FoeAt(int road, int cell) => FoesOn(road).FirstOrDefault(n => n.Cell == cell);

    /// <summary>敵の拠点のマスにいる敵部隊（<b>道をまたいで数える</b>——北と南はそこで合流する）。</summary>
    public Node? PortalFoe()
    {
        if (!Portal.On) return null;
        for (int road = 0; road < Map11.RoadCount; road++)
            foreach (Node n in FoesOn(road))
                if (n.Cell >= Map11.PortalCell) return n;
        return null;
    }

    /// <summary>
    /// その隊がいま当たっている敵部隊（<b>同じマスにいる相手</b>）。いなければ null。
    /// <b>拠点なしでは「道の先頭の敵が同じマスにいるか」そのもの</b>（第175期の <c>Advance</c> の式）。
    /// </summary>
    public Node? FoeFacing(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Units is null || s.Road < 0) return null;
        if (Portal.On && s.Cell >= Map11.PortalCell) return PortalFoe();
        Node? f = FrontFoe(s.Road);
        return f is not null && f.Cell == s.Cell ? f : null;
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
            if (s.Units is not null && s.Cell >= Map11.PortalCell && s.Units.Any(u => u.IsAlive))
            {
                Captured = true;
                CapturedTurn = Turn;
                return;
            }
    }

    /// <summary>その隊は制圧した第2拠点にいるか（<b>休む・組み直しができる</b>）。</summary>
    public bool AtSecondBase(int squadIndex)
        => Captured && Squads[squadIndex].Units is not null && Squads[squadIndex].Cell >= Map11.PortalCell;

    /// <summary>出撃させる（拠点 → 道）。控えはここで初めて盤上に出る。</summary>
    public void Send(int squadIndex, int road)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost) return;
        if (!CanSend(squadIndex)) return;
        s.Road = road;
        if (s.Home < 0) s.Home = road;
        // 組み直しで先に実体化していることがあるので `??=`。**第169期と同値**
        // ——あの版で「`Units` が null でなく `Deployed` が偽」になる道は1本も無かった。
        s.Units ??= BattleEngine.Materialize(s.Def.F, BattleContext.PlayerTeam);
        s.Deployed = true;
        // 第174期: 拠点から出たら道の先頭のマス（空きマス）に立つ。
        // **時間を切った設定では誰も読まない**ので、第169期と1ビットも違わない。
        if (s.Cell < 0) s.Cell = 0;
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
        s.Road = -1;
        s.Cell = -1;
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
        return !s.Lost && (s.Road < 0 || AtSecondBase(squadIndex));
    }

    /// <summary>出せるか。<b>0 枚の隊は出せない</b>（指示書 §1-1）。</summary>
    public bool CanSend(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost) return false;
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
        node.Units ??= BattleEngine.Materialize(node.Def.Enemy, BattleContext.EnemyTeam);
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
            s.Road = -1;
            s.Cell = -1;
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
    public bool AtHome(int squadIndex) => Squads[squadIndex].Cell < 0;

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

        if (s.Cell < 0)
        {
            if (!CanHead(road)) return null;
            Send(squadIndex, road);
        }
        else
        {
            road = s.Road;
            // **すでに敵と同じマスにいる**（前の戦闘が決着しなかった）ときは、その場でもう一度当たる。
            if (FoeFacing(squadIndex) is { } contact) return contact;
            Node? front = FrontFoe(road);
            int limit = front?.Cell ?? MaxCell;
            int next = Math.Min(s.Cell + 1, Math.Min(limit, MaxCell));
            if (next == s.Cell) return null;
            s.Cell = next;
            // 敵の拠点のマスへ入って、そこに敵が1つも無ければ制圧（第176期 §1-3）。
            TryCapture();
        }

        return FoeFacing(squadIndex);
    }

    /// <summary>
    /// 拠点どうしのワープ（第176期 §1-3）。<b>制圧した後だけ</b>、味方の拠点 ⇔ 第2拠点を
    /// <b>1 作戦ターンで行き来できる</b>。<b>道の上へは飛べない。</b>
    /// </summary>
    public bool Warp(int squadIndex, Dest dest)
    {
        if (!Portal.On || !Captured) return false;
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is null || !CanSend(squadIndex)) return false;

        if (dest.AtHome)
        {
            if (!AtSecondBase(squadIndex)) return false;
            s.Road = -1;
            s.Cell = -1;
            return true;
        }
        if (s.Cell >= 0 || dest.Cell < Map11.PortalCell) return false;
        if (PortalFoe() is not null) return false;   // 敵がいるあいだは拠点ではない
        s.Road = dest.Road;
        if (s.Home < 0) s.Home = dest.Road;
        s.Cell = Map11.PortalCell;
        s.Deployed = true;
        return true;
    }

    /// <summary>そのワープができるか（<see cref="Map11Orders.Plan"/> が読む）。</summary>
    public bool CanWarp(int squadIndex, Dest dest)
    {
        if (!Portal.On || !Captured) return false;
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is null || !CanSend(squadIndex)) return false;
        if (dest.AtHome) return AtSecondBase(squadIndex);
        return s.Cell < 0 && dest.Cell >= Map11.PortalCell && PortalFoe() is null;
    }

    /// <summary>戻る（拠点へ向かって 1 マス）。<b>一瞬では戻れない。</b></summary>
    public bool StepBack(int squadIndex)
    {
        Squad s = Squads[squadIndex];
        if (s.Lost || s.Units is null || s.Cell < 0) return false;
        s.Cell--;
        if (s.Cell < 0) s.Road = -1;
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
        if (s.Cell >= 0 && !AtSecondBase(squadIndex)) return false;
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
        Turn++;
        if (Time.AdvanceEvery <= 0 || Turn % Time.AdvanceEvery != 0) return SpawnIfDue(list);

        // 1 部隊ぶんの前進。**第175期の式をそのまま関数にしただけ**で、中身は1文字も変えていない。
        // 戻り値が偽 ＝ 拠点が陥落したので、その場で打ち切る。
        bool Step(Node n)
        {
            if (n.Cell >= 0) n.Cell--;
            if (n.Cell < 0)
            {
                BeginIntercept(n);
                if (NextInterceptor(n) < 0) { Fallen = true; FallenRoad = n.Def.Road; return false; }
                list.Add(new Encounter(-1, n, true));
                return true;
            }
            int sq = SquadAt(n.Def.Road, n.Cell);
            if (sq >= 0) list.Add(new Encounter(sq, n, false));
            return true;
        }

        for (int road = 0; road < Map11.RoadCount; road++)
        {
            // **最初からいる4部隊の規則は第175期のまま**（先頭だけが前進する・§1-1）。
            Node? n = NextNode(road);
            if (n is not null && !Step(n)) return list;
            if (!Portal.On) continue;

            // **湧いた部隊は列になって進む**（§1-2）——手前から順に1マスずつ、
            // 前が詰まっていればそのマスの手前で待つ。固定の4部隊も「詰まり」として数える。
            foreach (Node sp in FoesOn(road).Where(x => x.FromPortal).ToList())
            {
                if (sp.Cell < 0) continue;
                int target = sp.Cell - 1;
                if (FoesOn(road).Any(x => !ReferenceEquals(x, sp) && x.Cell == target)) continue;
                if (!Step(sp)) return list;
            }
        }
        return SpawnIfDue(list);
    }

    /// <summary>
    /// 敵の拠点から1部隊湧かせる（第176期 §1-2）。<b>制圧していれば湧かない。</b>
    /// 道は<b>北と南を交互</b>に選び、中身はその道の先遣（<see cref="Map11.SpawnEnemy"/>）。
    /// <b>満タンで湧く</b>——<c>Materialize</c> は戦闘の直前まで遅らせるので、ここでは箱だけ作る。
    /// </summary>
    private List<Encounter> SpawnIfDue(List<Encounter> list)
    {
        if (!Portal.On || Captured || Fallen) return list;
        if (Portal.SpawnEvery <= 0 || Turn <= 0 || Turn % Portal.SpawnEvery != 0) return list;

        int road = SpawnCount % Map11.RoadCount;
        var def = new Map11.RoadNode(road, Map11.RoadCells, Map11.SpawnName(road),
                                     Map11.SpawnEnemy(road), Map11.SpawnStage(road));
        int hp = def.Enemy.Occupied().Sum(o => o.Def.MaxHp);
        Spawns.Add(new Node
        {
            Def = def, FromPortal = true, DefMaxHp = hp, HpNow = hp, Cell = Map11.PortalCell,
        });
        SpawnCount++;
        return list;
    }

    /// <summary>そのマスにいる隊のうち、いちばん番号の若いもの（いなければ -1）。</summary>
    public int SquadAt(int road, int cell)
    {
        for (int i = 0; i < Squads.Length; i++)
        {
            Squad s = Squads[i];
            if (s.Units is not null && s.Road == road && s.Cell == cell && s.Units.Any(u => u.IsAlive))
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
            if (!node.Tried.Contains(i) && !Squads[i].Lost && Squads[i].Cell < 0 && CanSend(i)) return i;
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
        node.Units ??= BattleEngine.Materialize(node.Def.Enemy, BattleContext.EnemyTeam);
        return (pu, node.Units, Map11.DeriveSeed(Seed, Battles), node);
    }

    /// <summary>
    /// 迎撃が終わったあとの判定。<b>抜けず、拠点に出せる隊がもう1つも無ければ陥落。</b>
    /// </summary>
    public void CloseIntercept(Node node)
    {
        // §1-3 (4): 第2拠点も、敵が来て隊が1つもいなければ陥落する。**敵は拠点へ向かってしか
        // 動かない**ので、制圧した後に敵が第2拠点へ来る道は1本も無い——数えて 0 であることを出す。
        if (Portal.On && Captured && PortalFoe() is not null
            && !Squads.Any(x => !x.Lost && AtSecondBase(x.Index) && CanSend(x.Index)))
        {
            Captured = false;
            SecondBaseFalls++;
        }
        if (node.Cleared) return;
        // **判定は「拠点に出せる隊が1つも無いか」の1本だけ**——この到着でもう出した隊が
        // 残っていても（決着しなかったなど）、まだ拠点にいるなら陥落ではない。
        if (!Squads.Any(x => !x.Lost && x.Cell < 0 && CanSend(x.Index)))
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
