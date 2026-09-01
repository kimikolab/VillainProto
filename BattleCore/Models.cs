namespace BattleCore;

/// <summary>列。前ほど狙われやすく、後ろは前が生きている間は狙われにくい。</summary>
public enum Row
{
    Front,
    Mid,
    Back
}

/// <summary>
/// 攻撃の届き方。隊列に意味を持たせる中核。
/// パターンは増やしても4つまでに留めること。1つ増えるたびに、
/// 庇う・標的・巻き込みなど既存の全特性との相互作用を監査する必要がある。
/// </summary>
public enum AttackPattern
{
    /// <summary>単体。庇う・標的の介入を受ける唯一のパターン。</summary>
    Single,
    /// <summary>薙ぎ。狙った敵と、その両隣（同じ列）にも当たる。</summary>
    Sweep,
    /// <summary>
    /// 貫き。レーン（縦一列）を前から後ろへ走り抜け、並んでいる敵すべてに当たる。
    /// 奥へ進むほど威力が落ちる。庇えず、標的にも釣られない。
    /// </summary>
    Pierce,
    /// <summary>全体。敵全員に当たる。</summary>
    All
}

/// <summary>
/// 手番に何をするか。攻撃型（<see cref="AttackPattern"/>）とは別軸で、あちらが
/// 「攻撃がどう届くか」を表すのに対し、こちらは「その手番に攻撃するのかどうか」を表す。
///
/// パッシブ（Trait の反応）とは分離してある。特性は起きたことへの反応で、
/// こちらは手番そのもの。溜めている駒も特性は普通に反応する。
/// </summary>
public enum ActionKind
{
    /// <summary>攻撃する。倍率と攻撃型の上書きが乗る。</summary>
    Attack,
    /// <summary>溜める。攻撃せず、周期だけ進める。次の手番に大技が来る。</summary>
    Charge,

    /// <summary>
    /// 術を使う。攻撃せず、<see cref="Trait.OnAction"/> を持つ特性がその場で効果を出す。
    ///
    /// **手番を消費するのが要点。** 攻撃もして効果も出すなら、いつ撃つかに意味は出ない
    /// ——それは <c>OnTurnStart</c>（毎ターン無条件）をただ別の場所へ書き写しただけになる。
    /// 「回復と攻撃のどちらを取るか」があって初めてタイミングが選択になる。
    /// </summary>
    Skill
}

/// <summary>
/// 手番の1回ぶん。<see cref="UnitDef.Actions"/> に並べた順に繰り返す。
///
/// 倍率を double ではなく int の百分率にしてあるのは、ダメージ計算が
/// <see cref="BattleEngine.SecondaryPercent"/>（60）や
/// <see cref="BattleEngine.PierceDecayPercent"/>（25）と同じ <c>x * pct / 100</c> の
/// 整数演算で一貫して書かれているため。ここに double を1本だけ通すと、
/// 丸めの規則がこの1箇所だけ違うものになる。
/// </summary>
/// <param name="Kind">攻撃するのか、溜めるのか、術を使うのか。</param>
/// <param name="AttackPercent">攻撃力の倍率（百分率）。100 なら素の値をそのまま使う。</param>
/// <param name="PatternOverride">この手番だけ攻撃型を差し替える。null なら CurrentPattern。</param>
/// <param name="Label">ログと台本に出す名前（「魔力集中」など）。</param>
public sealed record UnitAction(
    ActionKind Kind,
    int AttackPercent = 100,
    AttackPattern? PatternOverride = null,
    string? Label = null);

/// <summary>ユニットの定義（不変データ）。カタログから読み込まれる想定。</summary>
public sealed class UnitDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int MaxHp { get; init; }
    public required int Attack { get; init; }
    public required int Speed { get; init; }

    /// <summary>付与されている特性のID。プラス・マイナスの区別は表示上のものでしかない。</summary>
    public required IReadOnlyList<TraitId> Traits { get; init; }

    public AttackPattern Pattern { get; init; } = AttackPattern.Single;

    /// <summary>
    /// 手番の行動を順に繰り返す。**null なら従来どおり毎ターン通常攻撃**で、
    /// ターンループは分岐前とまったく同じ経路を通る（第10期 Phase AA の受け入れ条件）。
    /// 周期の位置は <see cref="UnitState.ActionIndex"/> が持つ。
    /// </summary>
    public IReadOnlyList<UnitAction>? Actions { get; init; }

    /// <summary>編成画面で見せる説明文。</summary>
    public string PlusText { get; init; } = "";
    public string MinusText { get; init; } = "";
    public string Flavor { get; init; } = "";

    public override string ToString() => Name;
}

/// <summary>戦闘中のユニットの状態（可変）。</summary>
public sealed class UnitState
{
    public required UnitDef Def { get; init; }
    public required int TeamId { get; init; }

    /// <summary>
    /// この戦闘の中でだけ一意な連番。BattleContext.Add が振る。
    /// 胞子のように同じ UnitDef の駒が複数立つので、Def.Id では駒を指せない。
    /// 構造化イベント（BattleEvent）が「どの駒か」を指すための唯一の手段。
    /// </summary>
    public int InstanceId { get; internal set; }

    /// <summary>0..5。配置は FormationRules を参照。臆病などで戦闘中に変化する。</summary>
    public int Slot { get; set; }

    public int Hp { get; set; }
    public int MaxHp { get; set; }

    /// <summary>戦闘中に加算される攻撃力補正。バフ・デバフともにここへ入る。</summary>
    public int AtkBonus { get; set; }

    public IReadOnlyList<Trait> Traits { get; init; } = Array.Empty<Trait>();

    /// <summary>特性が自由に使えるカウンタ置き場。特性ごとにキーを分ける。</summary>
    public Dictionary<string, int> Counters { get; } = new();

    /// <summary>
    /// 戦闘中に一度でも後ろの列へ動かされたか。
    /// 「下がってから本領を発揮する」性質を、初期配置ではなく実績で判定するために使う。
    /// これが無いと、最初から後列に置くだけで代償を踏まずに後退後の性能が手に入る。
    /// </summary>
    public bool HasFallenBack { get; set; }

    /// <summary>
    /// 行動周期のどこにいるか（<see cref="UnitDef.Actions"/> の添字。剰余で回す）。
    ///
    /// Counters ではなく専用プロパティに置いてある。Counters のキーは特性の私有物
    /// （Engagement.CarryOver 参照）で、どの特性にも属さない周期の位置をそこへ入れると
    /// 会戦の境界処理が「エンジンはホワイトリストを持たない」原則を破ることになる。
    /// Slot・HasFallenBack と同格に置き、境界の扱いを engine 側の明示的な1行にした。
    /// </summary>
    public int ActionIndex { get; set; }

    public bool IsAlive => Hp > 0;
    public Row Row => FormationRules.RowOf(Slot);

    /// <summary>
    /// いま実際に使う攻撃パターン。定義上のパターンを特性が状況で書き換える。
    /// 参照は必ずこちらを使うこと。Def.Pattern を直接見ると状況変化が乗らない。
    /// </summary>
    public AttackPattern CurrentPattern
    {
        get
        {
            AttackPattern p = Def.Pattern;
            foreach (Trait t in Traits)
                p = t.ModifyPattern(this, p);
            return p;
        }
    }
    /// <summary>
    /// この手番に実行する行動。<c>Actions</c> を持たない駒（既存の全ユニット）では null で、
    /// 呼び出し側は従来どおり通常攻撃へ落ちる。
    /// </summary>
    public UnitAction? CurrentAction
        => Def.Actions is null || Def.Actions.Count == 0
            ? null
            : Def.Actions[ActionIndex % Def.Actions.Count];

    public string Name => Def.Name;

    /// <summary>支援・妨害を受け付けるか。受け付けない場合、バフもデバフも回復も通らない。</summary>
    public bool AcceptsSupport => !Traits.Any(t => t.BlocksSupport);

    public int CurrentAttack
    {
        get
        {
            int atk = Def.Attack + AtkBonus;
            foreach (Trait t in Traits)
                atk = t.ModifyAttack(this, atk);
            return Math.Max(0, atk);
        }
    }

    public bool HasTrait(TraitId id) => Traits.Any(t => t.Id == id);

    public int Counter(string key) => Counters.TryGetValue(key, out int v) ? v : 0;
    public void SetCounter(string key, int v) => Counters[key] = v;
}

/// <summary>
/// 盤面の形。X字に並ぶ編成5枠 + 召喚専用4枠の9枠。
///
///         後   中   前          ■ 編成スロット（0-4・常に5体）
///     1   ■   ○   ■          ○ 召喚専用（5-8・プレイヤーは置けない）
///     2   ○   ■   ○
///     3   ■   ○   ■
///
///     0 前1   1 前3   2 中央   3 後1   4 後3
///     5 ○中1（後1-前1 の間）   6 ○中3（後3-前3 の間）
///     7 ○前2（中央の前）       8 ○後2（中央の後ろ）
///
/// 貫きの経路は2本で、どちらも奥行きが等しい（前X → 中央 →〔○中X〕→ 後X）。
/// 編成スロットだけを見れば、角4つ（前1・前3・後1・後3）は全員が隣接次数2
/// （レーン相手＋中央）で完全に等価。中央のみ次数4。これが対称化の本体。
///
/// 旧盤面（前3・中1・後2 の6枠／レーンの奥行き 2/3/1）では、前3が「隣接次数1」かつ
/// 「後列に1体でも置けば貫きの対象から完全に外れる」逃げ場になっていた。
/// 奥行きを揃えることで、その2つを同時に潰してある。
///
/// 召喚が湧くと角の次数は最大4まで増えるが、これは戦闘中に生じる非対称なので許容する。
/// </summary>
public static class FormationRules
{
    public const int TotalSlots = 9;

    /// <summary>プレイヤーが置ける枠の数。編成は常にこの数ちょうどで埋まる。</summary>
    public const int PlayableSlotCount = 5;

    public const int LaneCount = 2;

    /// <summary>プレイヤーが置ける枠。</summary>
    public static readonly int[] PlayableSlots = { 0, 1, 2, 3, 4 };

    /// <summary>
    /// 召喚専用の枠。<b>この並びが Summon の走査順そのもの</b>で、調整ノブになっている。
    /// 貫き経路に入る 中1・中3 から先に埋めるので、召喚駒が盾として機能しやすい。
    /// </summary>
    public static readonly int[] SummonSlots = { 5, 6, 7, 8 };

    /// <summary>
    /// 席の名前。UI と診断はここを見ること。
    /// <b>各所で配列を手写ししない</b>——召喚枠が増えたとき、写した側だけが添字範囲外で落ちる。
    /// </summary>
    public static readonly string[] SeatNames =
        { "前1", "前3", "中央", "後1", "後3", "○中1", "○中3", "○前2", "○後2" };

    private static readonly Row[] RowTable =
    {
        Row.Front, Row.Front, Row.Mid, Row.Back, Row.Back,   // 編成 0-4
        Row.Mid, Row.Mid, Row.Front, Row.Back                // 召喚 5-8
    };

    /// <summary>
    /// 貫きの走査順そのもの。前から後ろへ。
    ///
    /// <b>中央は両方の経路に属する</b>ので「スロット → レーン」は単数では表せない
    /// （<see cref="LanesOf"/> を使うこと）。○中X はそこに召喚駒が立っているときだけ
    /// 経路に加わる（空席は占有者0で自然に飛ぶ）。召喚駒はもう1体ぶんの減衰として働き、
    /// 後列を守る——「実態があるなら遮る」という判断からの帰結。
    /// </summary>
    private static readonly int[][] LanePaths =
    {
        new[] { 0, 2, 5, 3 },
        new[] { 1, 2, 6, 4 }
    };

    /// <summary>
    /// 召喚枠を除いた経路。<see cref="IsLanePredecessor"/> 専用。
    /// 守備範囲が「そのとき召喚駒が湧いているか」で変わってはいけないので、
    /// 貫きの走査順とは分けてある。
    /// </summary>
    private static readonly int[][] CorePaths =
    {
        new[] { 0, 2, 3 },
        new[] { 1, 2, 4 }
    };

    /// <summary>
    /// 隣接表。<b>幾何計算で導出しない。</b>X字の隣接は不規則で、
    /// 「同じ列の左右」と「同じレーンの前後」の和には分解できない
    /// （前1と後1は隣接するが、貫き経路では間に中央が入る）。
    ///
    /// 味方に及ぶもの（巻き込み・生贄・囃し立て・散開・毒漏れ・火の粉）は必ずこちらを見ること。
    /// 敵に及ぶもの（薙ぎの巻き込み・反撃の返し）は <see cref="SweepTargets"/> を見ること。
    /// この線引きを崩すと、範囲攻撃が縦へ広がって貫きと区別がつかなくなる。
    ///
    /// 中央は編成5枠すべてと接続する。通常攻撃からは守られるが、味方のマイナスは一身に浴びる席。
    /// 「隣接デメリットの捨て場」を作らないための措置（旧盤面の中列と同じ役割）。
    /// </summary>
    private static readonly int[][] AdjacencyTable =
    {
        new[] { 2, 3, 5, 7 },        // 0 前1
        new[] { 2, 4, 6, 7 },        // 1 前3
        new[] { 0, 1, 3, 4, 7, 8 },  // 2 中央
        new[] { 0, 2, 5, 8 },        // 3 後1
        new[] { 1, 2, 6, 8 },        // 4 後3
        new[] { 0, 3 },              // 5 ○中1
        new[] { 1, 4 },              // 6 ○中3
        new[] { 0, 1, 2 },           // 7 ○前2
        new[] { 2, 3, 4 }            // 8 ○後2
    };

    /// <summary>
    /// 薙ぎの巻き込み先。「標的と同じ列の全員 + 中列の駒」。
    ///
    /// <b>対称な述語では書けない。</b>前1を薙げば中央まで巻き込むが、中央を薙いでも前列へは
    /// 広がらない（召喚が無ければ中央は自分だけ）。前列が削れるほど薙ぎが痩せる、という
    /// 非対称が要。旧盤面の AreLateralNeighbors（対称）はこの形を表現できない。
    /// </summary>
    private static readonly int[][] SweepTable =
    {
        new[] { 1, 2, 7 },   // 0 前1 → 前列の相方・中央・○前2
        new[] { 0, 2, 7 },   // 1 前3
        new[] { 5, 6 },      // 2 中央 → 中列の召喚枠のみ（召喚が無ければ巻き込みゼロ）
        new[] { 2, 4, 8 },   // 3 後1 → 後列の相方・中央・○後2
        new[] { 2, 3, 8 },   // 4 後3
        new[] { 2, 6 },      // 5 ○中1
        new[] { 2, 5 },      // 6 ○中3
        new[] { 0, 1, 2 },   // 7 ○前2
        new[] { 2, 3, 4 }    // 8 ○後2
    };

    public static Row RowOf(int slot) => RowTable[slot];

    /// <summary>召喚専用の枠か。プレイヤーはここに置けない。</summary>
    public static bool IsSummonSlot(int slot) => slot >= PlayableSlotCount;

    /// <summary>貫きが走る経路。前から後ろの順。</summary>
    public static IReadOnlyList<int> LanePath(int lane) => LanePaths[lane];

    /// <summary>
    /// そのスロットが属するレーン。<b>中央は2本に属し、○前2・○後2 はどこにも属さない。</b>
    /// 単数を返す旧 LaneOf ではこの盤面を表せない。
    /// </summary>
    public static IReadOnlyList<int> LanesOf(int slot)
    {
        var lanes = new List<int>(LaneCount);
        for (int l = 0; l < LaneCount; l++)
            if (Array.IndexOf(LanePaths[l], slot) >= 0) lanes.Add(l);
        return lanes;
    }

    /// <summary>前ほど小さい。押し出しと後退の向きを比べるために使う。</summary>
    public static int DepthOf(Row row) => row switch
    {
        Row.Front => 0,
        Row.Mid => 1,
        _ => 2
    };

    public static IEnumerable<int> SlotsOfRow(Row row)
    {
        for (int i = 0; i < TotalSlots; i++)
            if (RowTable[i] == row) yield return i;
    }

    /// <summary>
    /// その列のうちプレイヤーが置ける枠だけ。<b>逃亡・後退の行き先はこちらを使うこと。</b>
    /// 召喚枠を含めると、空いている ○中1 へ逃げ込んで誰も押しのけないことになり、
    /// 逃亡が純粋な利益になる（<c>BattleContext.FindBackSlotFor</c> 参照）。
    /// </summary>
    public static IEnumerable<int> PlayableSlotsOfRow(Row row)
    {
        for (int i = 0; i < PlayableSlotCount; i++)
            if (RowTable[i] == row) yield return i;
    }

    /// <summary>隣接。味方に及ぶものは必ずこちらを見ること。</summary>
    public static bool AreAdjacent(int a, int b)
        => a != b && Array.IndexOf(AdjacencyTable[a], b) >= 0;

    /// <summary>薙ぎが巻き込む席。敵に及ぶ範囲はこちらを見ること。</summary>
    public static IReadOnlyList<int> SweepTargets(int slot) => SweepTable[slot];

    /// <summary>「横」＝同じ列に並ぶ相方。編成スロットでは 前1↔前3 と 後1↔後3 の2組だけ。</summary>
    public static bool AreSameRowPair(int a, int b) => a != b && RowTable[a] == RowTable[b];

    /// <summary>
    /// 「前」＝ <paramref name="a"/> が <paramref name="b"/> の同じレーンの1つ手前か。
    /// <see cref="CorePaths"/>（召喚枠抜き）で数えるので、○中1 が空でも
    /// 後1の駒は中央を「1つ手前」と見なせる。
    /// </summary>
    public static bool IsLanePredecessor(int a, int b)
    {
        for (int l = 0; l < LaneCount; l++)
        {
            int ia = Array.IndexOf(CorePaths[l], a);
            int ib = Array.IndexOf(CorePaths[l], b);
            if (ia >= 0 && ib >= 0 && ib - ia == 1) return true;
        }
        return false;
    }
}

/// <summary>編成。スロットに UnitDef を入れる。null は空きスロット。</summary>
public sealed class Formation
{
    // 長さは編成枠のぶんだけ。召喚枠まで確保すると、そこへ黙って書けてしまい
    // Materialize が「プレイヤーが置けないはずの席に立つ駒」を作る。
    private readonly UnitDef?[] _slots = new UnitDef?[FormationRules.PlayableSlotCount];

    public UnitDef? this[int slot]
    {
        get => _slots[slot];
        set => _slots[slot] = value;
    }

    public int Count => _slots.Count(s => s is not null);

    public IEnumerable<(int Slot, UnitDef Def)> Occupied()
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] is { } d)
                yield return (i, d);
    }

    public Formation Clone()
    {
        var f = new Formation();
        for (int i = 0; i < _slots.Length; i++) f[i] = _slots[i];
        return f;
    }

    /// <summary>
    /// スロットを名前で指定して編成を作る。
    /// front1 → 0、front3 → 1、center → 2、back1 → 3、back3 → 4。
    ///
    /// 旧 Of(params) は引数の並びとスロット番号の対応が暗黙で、
    /// 盤面の形が変わったときに黙って別物の編成になった（5枠→6枠で後列1枚目が全部中列に落ちた）。
    /// 編成定義では必ずこちらを使うこと。
    ///
    /// X字化のとき旧引数名（front2 / mid / back2）は残さなかった。残すと呼び出し側の
    /// 移行漏れが見えなくなる——コンパイルエラーが移行のチェックリストになっている。
    /// </summary>
    public static Formation Build(
        UnitDef? front1 = null, UnitDef? front3 = null,
        UnitDef? center = null,
        UnitDef? back1 = null, UnitDef? back3 = null)
    {
        var f = new Formation();
        f[0] = front1;
        f[1] = front3;
        f[2] = center;
        f[3] = back1;
        f[4] = back3;
        return f;
    }
}

/// <summary>ログ行の種類。UI はこれを見て色を決める。文字列を解析させないための型。</summary>
public enum LogKind
{
    System,        // 開始・終了
    Turn,          // ターン区切り
    Action,        // 通常の行動
    Damage,        // 敵への与ダメージ
    FriendlyFire,  // 味方への事故
    Status,        // 毒などの継続効果
    Trigger,       // 特性の発動
    Highlight,     // 見せ場（覚醒・破裂）
    Summon,        // 増援・蘇生
    Death          // 撃破
}

public sealed record LogLine(LogKind Kind, int Indent, string Text)
{
    public override string ToString() => new string(' ', Indent * 2) + Text;
}

/// <summary>
/// 1体の駒が1戦で何をしたかの集計。
///
/// ログや BattleEvent と違い <b>verbose に関係なく数える</b>。
/// 200 seed × 全ステージの一括シミュレーションで平均を取るのが用途なので、
/// ここを verbose で切ると測りたいときに測れない。
/// 盤面には一切影響しない（ただの足し算）。
/// </summary>
public sealed class UnitTally
{
    /// <summary>
    /// 攻撃を振った回数（<c>PerformAttack</c> を通った回数）。
    /// 反撃（棘）はここを通らない。反撃は <c>ApplyDamage</c> を直接呼ぶので
    /// <see cref="Interventions"/> の側に出る。この2つのズレ自体が情報になる
    /// （振らないのに干渉する＝反応型、振るのに干渉しない＝空振り）。
    /// </summary>
    public int Attacks;

    /// <summary>
    /// 実際にダメージを通した回数。攻撃・反撃・破裂・毒のどれでも、
    /// この駒が起点になって盤面が動いた回数を数える。**これが活動量の本体。**
    /// </summary>
    public int Interventions;

    /// <summary>敵に与えたダメージ。</summary>
    public int DamageToEnemy;

    /// <summary>味方に与えたダメージ。破裂・生贄・吸いはここに出る。</summary>
    public int DamageToAlly;

    /// <summary>受けたダメージ（敵味方を問わない）。</summary>
    public int DamageTaken;

    /// <summary>受けたダメージのうち味方由来のぶん。</summary>
    public int TakenFromAlly;

    /// <summary>
    /// 回復で実際に増えた HP（<c>ctx.Heal</c> が動かした分だけ。上限で切られた分は入らない）。
    /// 代金の分解（第9期 bill）が「払った HP」から差し引くために足したもので、
    /// <b>既存の出力には出さない</b>——pulse の表に列を増やすと第8期以前の出力と diff が出る。
    /// </summary>
    public int Healed;

    /// <summary>
    /// 溜めた回数（<see cref="ActionKind.Charge"/> の手番を消費した回数）。
    /// </summary>
    public int Charges;

    /// <summary>
    /// 倍率つきで振った回数（大技の発火数）。<see cref="Attacks"/> の内数。
    ///
    /// <see cref="Healed"/> と同じく<b>既存の出力には出さない</b>——pulse の表に列を増やすと
    /// 第9期以前の出力と diff が出る。第10期の charge 診断だけが読む。
    /// verbose 非依存なのは、発火率を 200 seed × 全編成で測るため。
    /// </summary>
    public int BigAttacks;

    /// <summary>
    /// この駒が最後に生存していたターン。倒れた時点のターン番号。
    /// 生き残った場合は決着ターン。
    ///
    /// <see cref="Healed"/> / <see cref="BigAttacks"/> と同じく<b>既存の出力には出さない</b>。
    /// pulse・compare・docs に列を足すと過去の出力と diff が出る。life 診断だけが読む。
    /// verbose 非依存（200 seed × 全編成で稼働率を測るため）。
    ///
    /// 蘇生された場合は上書きされる（後の値が勝つ）。<see cref="Deaths"/> が
    /// 「倒れた回数」で2以上になりうるのと同じ扱いで、欲しいのは最後に活動していたターン。
    /// </summary>
    public int LastActiveTurn;

    /// <summary>
    /// 巨躯（<see cref="TraitId.Colossus"/>）が肩代わりで飲み込んだ量の累計。
    /// <c>ApplyDamage</c> の巨躯の分岐で <c>blocked</c> を数える——<b>吐き戻しの計上と同じ場所・同じ量</b>
    /// なので、返した先の増分と突き合わせられる。壁が自分の減衰で実際に受けた量とは別物
    /// （壁の被弾は <see cref="DamageTaken"/> の側）。
    ///
    /// <see cref="Healed"/> / <see cref="BigAttacks"/> / <see cref="LastActiveTurn"/> と同じく
    /// <b>既存の出力には出さない</b>（pulse・compare・docs に列を足すと過去の出力と diff が出る）。
    /// 第36期の gullet 診断だけが読む。verbose 非依存。
    /// </summary>
    public int Swallowed;

    /// <summary>
    /// まどろんだ回数（腹が満ちて手番を失った回数）。第36期。<see cref="Swallowed"/> と同じ扱いで
    /// 既存の出力には出さない。
    /// </summary>
    public int Slumbers;

    /// <summary>
    /// 還しが発火した回数（1戦につき高々1回。<see cref="ColossusTrait.RefundSpentKey"/> が担保する）。
    /// <b>届いたかどうかとは別</b>——渇き（第三波）の下では発火しても 0 点しか届かない。
    /// 第36期。<see cref="Swallowed"/> と同じ扱いで既存の出力には出さない。
    /// </summary>
    public int Refunds;

    /// <summary>
    /// 還しで<b>実際に味方の HP が増えた量</b>の合計。額面（腹 × 率）ではない
    /// ——渇き（<c>Drought</c>）・支援拒否（<c>Stoic</c>）・満タンで消えた分は入らない。
    /// <see cref="Refunds"/> が 1 なのに 0 なら、その戦の還しは丸ごと封じられている。
    /// 第36期。<see cref="Swallowed"/> と同じ扱いで既存の出力には出さない。
    /// </summary>
    public int Refunded;

    /// <summary>とどめを刺した敵の数。</summary>
    public int Kills;

    /// <summary>倒れた回数。蘇生されて再度倒れると2になる。</summary>
    public int Deaths;

    public void Add(UnitTally o)
    {
        Attacks += o.Attacks; Interventions += o.Interventions;
        DamageToEnemy += o.DamageToEnemy; DamageToAlly += o.DamageToAlly;
        DamageTaken += o.DamageTaken; TakenFromAlly += o.TakenFromAlly;
        Healed += o.Healed;
        Charges += o.Charges; BigAttacks += o.BigAttacks;
        Swallowed += o.Swallowed; Slumbers += o.Slumbers;
        Refunds += o.Refunds; Refunded += o.Refunded;
        Kills += o.Kills; Deaths += o.Deaths;
        // LastActiveTurn は**加算しない**。ターン番号は足しても意味を持たない。
        // Math.Max を取るのは、合算の順序に依存しない（可換・結合的）ため——
        // 「最後の値を残す」方式は Add を呼ぶ順で答えが変わる。
        LastActiveTurn = Math.Max(LastActiveTurn, o.LastActiveTurn);
    }
}

/// <summary>
/// 構造化された戦闘イベントの種類。
///
/// LogLine（人が読む文字列）と対になる、機械が読む側の記録。
/// 戦闘画面は「誰が誰に何をしたか」を必要とするが、文字列からは復元できないので分けてある。
/// **文字列を解析して画面を作ってはいけない**（LogKind の原則と同じ）。
/// </summary>
public enum BattleEventKind
{
    TurnStart,   // ターンの区切り
    Attack,      // 振った（当たったかどうかとは別）
    Damage,      // 実際に減った
    Heal,
    Death,
    Summon,      // 新しい駒が盤面に出た
    Revive,      // 倒れていた駒が戻った
    Move,        // スロットが変わった
    Status,      // 毒・燃焼・痺れなどの継続効果が「働いた」（そのターン実際に削った量）
    Highlight,   // 見せ場（覚醒・破裂）。演出を差し込む位置の指示

    /// <summary>
    /// 溜めた（<see cref="ActionKind.Charge"/>）。攻撃していないので Attack とは別。
    ///
    /// **次の手番に何が来るかをこのイベントだけで読めるようにしてある**
    /// （<c>Amount</c> = 次の倍率、<c>Pattern</c> = 次の攻撃型、<c>Text</c> = 溜めの名前）。
    /// 再生側が「次のターンに大技が来る」を予告できないと、溜めは画面上ただの空白になる。
    /// </summary>
    Charge,

    /// <summary>
    /// ターン開始時点で駒が負っている継続効果の「残量」。<see cref="Status"/> とは意味が違う
    /// （あちらは働いた記録、こちらは今いくつ乗っているか）ので種類を分けてある。
    ///
    /// 状態異常のカウンタは16箇所から書かれていて、書き込み側すべてに通知を挟むと
    /// Traits.cs を広く触ることになる（バランスが載っている場所なので触りたくない）。
    /// 継続効果はターン開始時にまとめて処理されるので、そこで1回スナップショットを撮れば足りる。
    /// **ターン中に積まれたぶんは次のターンの頭まで出ない。** 効き始めるのもそのときなので、
    /// 表示としてはむしろ揃っている。
    /// </summary>
    StatusSnapshot,

    /// <summary>
    /// ターン開始時点の攻撃力（<see cref="UnitState.CurrentAttack"/>）。
    /// 積み上げ系（墓守の三角数、溜め、被弾強化）は素の値から大きく離れるので、
    /// 素の値だけ見せると盤面で何が起きているか読めない。
    /// <see cref="StatusSnapshot"/> と同じ理由で、ターン頭に1回だけ写す。
    /// </summary>
    StatSnapshot,

    /// <summary>
    /// 術を使った（<see cref="ActionKind.Skill"/>）。攻撃していないので Attack とは別。
    ///
    /// 効果そのもの（回復・毒の濃縮）は各特性が自分のイベントを出すので、こちらは
    /// 「誰がその手番に何を撃ったか」だけを持つ。**空振りした手番（繕う相手がいない・
    /// 毒が積まれていない）はこの1件しか残らない**が、残らないと画面上は手番を飛ばした
    /// のと区別が付かない。溜めと同じ理由で、条件を付けずに必ず打つ。
    /// </summary>
    Skill
}

/// <summary>
/// 戦闘中に起きた一つの出来事。時間順に並んだこの列が、そのまま再生用の台本になる。
///
/// BattleEngine.Run は seed 決定的な純関数で戦闘を丸ごと計算し切るので、
/// 戦闘画面はリアルタイムのシミュレーションではなく**この列の再生**として書ける。
/// </summary>
public sealed class BattleEvent
{
    public required BattleEventKind Kind { get; init; }
    public required int Turn { get; init; }

    /// <summary>行為者の InstanceId。盤面全体に関わる出来事では null。</summary>
    public int? ActorId { get; init; }

    /// <summary>対象の InstanceId。</summary>
    public int? TargetId { get; init; }

    /// <summary>ダメージ量・回復量など。種類によって意味が変わる。</summary>
    public int Amount { get; init; }

    /// <summary>対象のこの出来事の直後のHP。バーの補間に使う。</summary>
    public int HpAfter { get; init; }

    /// <summary>味方への巻き込みか。色を変えるため。</summary>
    public bool FriendlyFire { get; init; }

    /// <summary>Move / Summon の行き先スロット。</summary>
    public int Slot { get; init; }

    /// <summary>
    /// Summon で新しく出た駒の陣営。
    /// 増援は初期盤面に載っていないので、再生側はこのイベントだけで駒を組み立てる必要がある。
    /// </summary>
    public int? Team { get; init; }

    /// <summary>Attack のときに実際に使われたパターン。</summary>
    public AttackPattern? Pattern { get; init; }

    /// <summary>Highlight / Status のフレーバー。演出の中身ではなく添え物として扱う。</summary>
    public string? Text { get; init; }
}

/// <summary>戦闘結果。UIはこれを表示するだけでよい。</summary>
public sealed class BattleResult
{
    public required bool PlayerWon { get; init; }
    public required int Turns { get; init; }
    public required IReadOnlyList<LogLine> Log { get; init; }

    /// <summary>味方の生存数。バランス調整の指標として使う。</summary>
    public required int PlayerSurvivors { get; init; }

    /// <summary>ユニットIDごとの与ダメージ合計。誰が働いたかを機械的に見るため。</summary>
    public required IReadOnlyDictionary<string, int> DamageByUnit { get; init; }

    /// <summary>
    /// ユニットIDごとの働きの内訳。「勝ったかどうか」ではなく「誰が何をしたか」を見る。
    /// 与ダメージだけだと、蘇生・萎縮のような出力を持たない駒が全員ゼロに潰れて
    /// 区別がつかない（`docs/pulse.md`）。
    /// </summary>
    public required IReadOnlyDictionary<string, UnitTally> TallyByUnit { get; init; }

    /// <summary>1ターンのうちに味方が倒した敵の数の最大値。「連鎖の深さ」の代理指標。</summary>
    public required int MaxEnemyKillsInOneTurn { get; init; }

    /// <summary>
    /// 構造化された出来事の列。再生用の台本。
    /// Log と同じく verbose=false のときは空（一括シミュレーションで積むと遅くなるため）。
    /// </summary>
    public required IReadOnlyList<BattleEvent> Events { get; init; }

    /// <summary>
    /// 曝き（第40期）が実際に引きずり出した回数と、後列／前列が 0 体で何もしなかった回数。
    ///
    /// <para><b>ログからは数えられないので結果に載せる。</b> 空振りはログを1行も出さない
    /// （出すと「何も起きていない」ことがログの主役になる）ので、盤面にも文字列にも痕跡が残らない。
    /// <b>verbose に依存しない</b>——診断は verbose=false で数百戦回すため。</para>
    ///
    /// <para>既定（<c>ExposeRule.Default</c> ＝ 無効）では常に 0。</para>
    /// </summary>
    public required int ExposeCount { get; init; }
    public required int ExposeMissed { get; init; }

    /// <summary>
    /// 突き返し（第41期）の計数。<b>ログからは数えられないものが混じるので結果に載せる。</b>
    /// 1ターン1回の上限で弾かれた回（<c>ShoveCapped</c>）と、支援拒否で弾かれた回
    /// （<c>ShoveBlocked</c>）は<b>ログを1行も出さない</b>——出すと「何も起きていない」ことが
    /// ログの主役になる——ので、盤面にも文字列にも痕跡が残らない。
    /// <b>verbose に依存しない</b>（診断は verbose=false で数百戦回すため）。
    ///
    /// <para>保持者（<c>UnitCatalog.Hane</c>）を編成に入れなければ全部 0。</para>
    /// </summary>
    public required int ShoveFired { get; init; }
    public required int ShoveCapped { get; init; }
    public required int ShoveSwapped { get; init; }
    public required int ShoveNoRow { get; init; }
    public required int ShoveStaggered { get; init; }
    public required int ShoveBlocked { get; init; }

    /// <summary>
    /// 弱体（第42期）の計数。<b>窓口 <see cref="BattleContext.Dull"/> を通った量を経路別に数える。</b>
    /// 開戦時1回の3経路（呪詛×2・萎縮）はログを1行にまとめて出すので、
    /// <b>文字列からは延べ体数が復元できない</b>——だから結果に載せる。
    /// <b>verbose に依存しない</b>（診断は verbose=false で数百戦回すため）。
    ///
    /// <para><c>DullTotal</c> 総量（両陣営） ／ <c>DullByRoute</c> 経路別（<see cref="DullRoute"/> の順） ／
    /// <c>BearTaken</c> 集約役が引き受けた量 ／ <c>BearPassed</c> 横取りされずに素通りした量 ／
    /// <c>BearArmor</c> 生成したアーマー ／ <c>BearSoaked</c> そのうち実際に吸った量 ／
    /// <c>BearFrom</c> 引き受けた相手の内訳（駒名 → 量）。</para>
    ///
    /// <para>集約役（<c>UnitCatalog.Uke</c>）を編成に入れなければ <c>Bear*</c> は全部 0。
    /// <c>Dull*</c> は弱体源（ドハ／ネル／クビ／ハネ）がいなければ全部 0。</para>
    /// </summary>
    public required int DullTotal { get; init; }
    public required IReadOnlyList<int> DullByRoute { get; init; }

    /// <summary>
    /// そのうち横取り役（集約・渡し）に横取りされた量を<b>経路別に割ったもの</b>（第44期）。
    /// <c>DullByRoute[r] - DullTakenByRoute[r]</c> がその経路の「素通り」。
    /// 供給源が複数ある行では <c>BearTaken</c> / <c>BearPassed</c>（全経路の合算）から
    /// 経路ごとの割合が引けないので足した。
    /// </summary>
    public required IReadOnlyList<int> DullTakenByRoute { get; init; }

    /// <summary>
    /// 弱体で味方（正確には窓口の受け手）の <c>CurrentAttack</c> が 0 になった回数と駒の内訳。
    /// <b>崖の検算</b>用（第44期）。敵側の同型は <c>RelayZeroed</c>。
    /// </summary>
    public required int DullZeroed { get; init; }
    public required IReadOnlyDictionary<string, int> DullZeroedWho { get; init; }
    public required int BearTaken { get; init; }
    public required int BearPassed { get; init; }
    public required int BearArmor { get; init; }
    public required int BearSoaked { get; init; }
    public required IReadOnlyDictionary<string, int> BearFrom { get; init; }

    /// <summary>
    /// 渡し（第43期）の計数。<b>窓口 <see cref="BattleContext.Dull"/> の中で
    /// 味方から敵へ移った量</b>を数える。<b>ログからは数えられないものが混じる</b>
    /// ——敵が全滅していて転嫁が起きなかった回も、肩代わりで代金が他人へ移った分も、
    /// 文字列には痕跡が残らない。<b>verbose に依存しない</b>。
    ///
    /// <para><c>RelayTaken</c> 横取りした量 ／ <c>RelaySent</c> 敵へ流した量 ／
    /// <c>RelayMaxSent</c> 1回の <c>Dull</c> で流した最大量（崖の検算） ／
    /// <c>RelayZeroed</c> 転嫁で敵の <c>CurrentAttack</c> が 0 になった回数（崖の検算） ／
    /// <c>RelayCost</c> 代金として <c>ApplyDamage</c> へ渡した総量 ／
    /// <c>RelaySelfPaid</c> そのうち渡し役自身の身に実際に落ちた量 ／
    /// <c>RelayFrom</c> 横取りした相手の内訳 ／ <c>RelayTo</c> 流し先の内訳。</para>
    ///
    /// <para>渡し役（<c>UnitCatalog.Wata</c>）を編成に入れなければ全部 0。</para>
    /// </summary>
    public required int RelayTaken { get; init; }
    public required int RelaySent { get; init; }
    public required int RelayMaxSent { get; init; }
    public required int RelayZeroed { get; init; }
    public required int RelayCost { get; init; }
    public required int RelaySelfPaid { get; init; }
    public required IReadOnlyDictionary<string, int> RelayFrom { get; init; }
    public required IReadOnlyDictionary<string, int> RelayTo { get; init; }

    /// <summary>
    /// 誹り（第44期）の計数。<b>敵から味方へ弱体を撒く初めての経路。</b>
    /// <c>SlanderFired</c> 発火回数 ／ <c>SlanderTotal</c> 撒いた総量 ／
    /// <c>SlanderTo</c> 誹られた相手の内訳（駒名 → 量）。
    ///
    /// <para>保持者（<c>EnemyCatalog.Slanderer</c>）が盤上にいないか
    /// <c>SlanderRule.Penalty == 0</c> なら全部 0。<b>verbose に依存しない</b>。</para>
    /// </summary>
    public required int SlanderFired { get; init; }
    public required int SlanderTotal { get; init; }
    public required IReadOnlyDictionary<string, int> SlanderTo { get; init; }
}
