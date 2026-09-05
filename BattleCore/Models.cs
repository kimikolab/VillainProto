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

    /// <summary>
    /// いま立っている盤面。<see cref="BattleContext.Add"/> が自分を指すように差す
    /// （<see cref="InstanceId"/> と同じ1箇所）。
    ///
    /// <para><b>足した理由は1つだけ</b>——<see cref="Trait.ModifyAttack"/> が <c>self</c> しか
    /// 受け取らないので、<b>「隣に誰がいるか」を攻撃力の条件にする特性が書けない</b>
    /// （驕り・<see cref="TraitId.Overbear"/>・第46期）。隣接は盤面の量なので、
    /// 隣接を読む条件を <c>Counters</c> のキャッシュに固定すると
    /// <b>戦闘中の揺れ（味方が倒れて隣接が減る／隣が育って条件から外れる）が消える</b>。
    /// 揺れそのものが第46期の主題なので、固定ではなく毎回読む形にした。</para>
    ///
    /// <para><b>窓口は増えていない。</b> 参照するのは <see cref="BattleContext"/> であって
    /// 盤面の生データではない（CLAUDE.md「BattleContext = 盤面への唯一の窓口」）。
    /// <b>盤面の外で作られた <see cref="UnitState"/> では <c>null</c></b> になるので、
    /// 読む側は必ず null を「隣が1人もいない」と同じ扱いにすること。</para>
    /// </summary>
    internal BattleContext? Board { get; set; }

    /// <summary>0..5。配置は FormationRules を参照。臆病などで戦闘中に変化する。</summary>
    public int Slot { get; set; }

    public int Hp { get; set; }
    public int MaxHp { get; set; }

    /// <summary>戦闘中に加算される攻撃力補正。バフ・デバフともにここへ入る。</summary>
    ///
    /// <remarks>
    /// 第68期に<b>自動プロパティから書き換えた</b>。理由は1つだけ——
    /// <b>「外から届いた強化」と「自分で作った強化」を分けて数える</b>ため
    /// （<see cref="WhetReceived"/> は前者しか持たず、自己強化の9本は窓口を通らないので
    /// どこにも記録が無かった）。<b>上がった分だけ</b>を
    /// <see cref="BattleContext.NoteAtkGain"/> へ流す（下がった分は流さない）。
    ///
    /// <para><b>盤面は1ビットも動かない。</b> 通知先は計数だけで、誰も読んで分岐しない。
    /// 盤面の外で作られた <see cref="UnitState"/>（<c>Board</c> が null）では何も起きない。</para>
    ///
    /// <para><b>0 に戻す2箇所（蘇生・会戦の境界）は <see cref="ResetAtkBonus"/> を使う。</b>
    /// あそこを通常の代入で書くと、負の補正を背負った駒を蘇生したときに
    /// 「0 へ戻った」が<b>正の上昇として帳簿に載る</b>。</para>
    /// </remarks>
    public int AtkBonus
    {
        get => _atkBonus;
        set
        {
            int delta = value - _atkBonus;
            _atkBonus = value;
            if (delta > 0) Board?.NoteAtkGain(this, delta);
        }
    }
    private int _atkBonus;

    /// <summary>
    /// <see cref="AtkBonus"/> を<b>帳簿に載せずに</b> 0 へ戻す（第68期）。
    /// 呼ぶのは寿命の2箇所だけ——<c>BattleEngine.Revive</c> と <c>Engagement.CarryOver</c>。
    /// </summary>
    internal void ResetAtkBonus() => _atkBonus = 0;

    /// <summary>
    /// <b><see cref="BattleContext.Whet"/> 窓口を通って届いた強化の累計</b>（第67期）。
    ///
    /// <para><b>書くのは窓口の1箇所だけ</b>——<c>AtkBonus</c> に加算するのと同じ行で、
    /// 同じ条件（<c>WhetBlock</c> で落とされた経路は届いていないので数えない）。
    /// 自己強化の9本も、墓守の層の引き直しも通らない（<c>Whet</c> の非対称と同じ）。</para>
    ///
    /// <para><b><see cref="BattleContext.Dull"/> では減らさない。</b>
    /// 閾値は<b>累積の床</b>であって在庫ではない——「外から何点押されたか」を条件にしたいので、
    /// 後から弱体が来て正味が下がっても「押された」事実は消えない。
    /// <b>却下した代案</b>: 正味（<c>Whet − Dull</c>）を読む形。これだと弱体を撒く敵の前で
    /// 条件が引っ込み、<b>読んでいる量が「外の供給」ではなく「弱体との差」になる</b>
    /// ——第63期「通貨を移す機構は読み手にとって奪う機構」を条件式の中に持ち込むことになる。</para>
    ///
    /// <para>寿命は <c>AtkBonus</c> と<b>完全に同じ</b>（蘇生 ＝ <c>Revive</c> と
    /// 会戦の境界 ＝ <c>Engagement.CarryOver</c> の2箇所で 0 に戻す）。
    /// 「配られた力」の帳簿なので、力そのものが消える場所で一緒に消えるのが筋。</para>
    /// </summary>
    public int WhetReceived { get; set; }

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
            BattleContext? b = Board;
            foreach (Trait t in Traits)
            {
                TraitMark m = b?.BeginTrait(t.Id, this) ?? default;   // 第94期 (T2) の印
                p = t.ModifyPattern(this, p);
                b?.EndTrait(m);
            }
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
            BattleContext? b = Board;
            foreach (Trait t in Traits)
            {
                TraitMark m = b?.BeginTrait(t.Id, this) ?? default;   // 第94期 (T2) の印
                atk = t.ModifyAttack(this, atk);
                b?.EndTrait(m);
            }
            return Math.Max(0, atk);
        }
    }

    public bool HasTrait(TraitId id) => Traits.Any(t => t.Id == id);

    /// <summary>
    /// 観測を通らない読み（第94期 (T2)）。<b>engine の内部はこちらを使う。</b>
    ///
    /// <para><see cref="Counter"/> は「いま実行中の特性がこのカウンタを読んだ」を観測するが、
    /// <c>ApplyDamage</c> や <c>ctx.Poison</c> の中の読みは<b>engine の読み</b>であって
    /// 特性の読みではない——そこを分けないと「殴った駒が破片と手番と傷を読んだ」ことになる。</para>
    /// </summary>
    public int RawCounter(string key) => Counters.TryGetValue(key, out int v) ? v : 0;

    public int Counter(string key)
    {
        // 第94期 (T2) の観測。**既定 null なので通常の実行では null 検査1つで抜ける**
        // （`Board?.Probe` はどの規則からも読まれない・乱数も1つも消費しない）。
        if (Board?.Probe is not null) Board.NoteProbeRead(this, key);
        return Counters.TryGetValue(key, out int v) ? v : 0;
    }

    /// <summary>
    /// カウンタを書く。第68期に<b>増えた分だけ</b>を
    /// <see cref="BattleContext.NoteStatusGain"/> へ流すようにした
    /// （<see cref="AtkBonus"/> の setter と同じ形・同じ理由）。
    ///
    /// <para><b>盤面は1ビットも動かない。</b> 受け取る側は
    /// <see cref="StatusKeys.All"/> の7キー以外を捨てるので、
    /// 特性の私有キー（<c>goadTarget</c> ・ <c>refundSpent</c> など）は帳簿に載らない。
    /// 減った分（毒の吸い上げ・破片の消費・痺れの消費・境界の一括消去）は流さない
    /// ——数えたいのは<b>外から届いた累計</b>であって在庫ではない。</para>
    /// </summary>
    public void SetCounter(string key, int v)
    {
        int delta = v - (Counters.TryGetValue(key, out int had) ? had : 0);
        Counters[key] = v;
        if (delta > 0) Board?.NoteStatusGain(this, key, delta);
        // 第94期 (T2)。**減った分もここで観測する**——供給と消費を両方数えないと
        // 「中継」（移すだけで盤面の総量を増やさない特性・ガルドの傷）が供給と区別できない。
        // 増えた分は `NoteStatusGain` → `NoteCarry` の側で観測される（二重に数えない）。
        if (delta < 0 && Board?.Probe is not null) Board.NoteProbeWrite(this, key, delta);
    }
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
    /// <b>この駒の <c>CurrentAttack</c> が出力（ダメージ量）に変換された回数</b>（第64期）。
    ///
    /// <para><see cref="Attacks"/>（<c>PerformAttack</c> を通った回数）では
    /// <b>「配った強化が無駄になったか」を判定できない</b>——棘（<see cref="ThornsTrait"/>・カド）は
    /// <c>PerformAttack</c> を1度も通らないのに反撃量を自分の <c>CurrentAttack</c> で決めるので、
    /// <c>Attacks == 0</c> を「死蔵」と読むと<b>符号を逆に読む</b>（第63期 §11-2 の実測）。</para>
    ///
    /// <para>逆に <see cref="Interventions"/>（ダメージの出どころになった回数）では
    /// <b>広すぎる</b>——破裂・生贄・大喰らいの吸いは<b>固定量</b>で攻撃力を1ビットも読まない。</para>
    ///
    /// <para><b>加算する場所はロスター全体で4つだけ</b>（<c>CurrentAttack</c> を自分の出力量に
    /// 変換している箇所の全部）: <c>PerformAttack</c> ／ 棘（<see cref="ThornsTrait"/>）／
    /// 仇討ち（<see cref="AvengeTrait"/>）／ 責め苦の追撃（<see cref="TormentTrait"/>）。
    /// <b>誰も読んで分岐しない。</b></para>
    /// </summary>
    public int AttackReads;

    /// <summary>
    /// <b>強化を受け取った「後」に <see cref="AttackReads"/> を通した回数</b>（第65期）。
    /// 死蔵（<c>AttackReads == 0</c>・第64期）は<b>受け取る前に振った</b>ぶんを数えてしまうので、
    /// 「配った強化が実際に使われたか」を測るにはこちらが要る。
    /// <b>誰も読んで分岐しない。</b>
    /// </summary>
    public int AttackReadsAfterWhet;

    /// <summary>
    /// 強化の到着（第65期）。<c>WhetTurnSum</c> は Σ(量 × 到着ターン)、
    /// <c>WhetFirstTurn</c> は<b>初めて受け取ったターン</b>（0 = 一度も受けていない。
    /// 開戦時＝ターン 0 の到着は <b>1 に丸める</b>——「受けたか」の判定を 0 で兼ねるため）。
    /// <c>WhetPendingByRoute</c> は<b>まだ使われていない受取量</b>の経路別の保留で、
    /// <see cref="BattleContext.NoteAttackRead"/> が使用済みへ移す（遅延評価にしているのは、
    /// 「受け取った時点より後に振ったか」を1回の走査で判定するため）。
    /// <b>どれも盤面には一切影響しない。</b>
    /// </summary>
    public int WhetTurnSum;
    public int WhetFirstTurn;
    public int[]? WhetPendingByRoute;

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

    /// <summary>
    /// この駒が<b>他者から受け取った</b>強化・弱体の量（第56期）。
    /// <c>Whetted</c> は <see cref="BattleContext.Whet"/> を、
    /// <c>Dulled</c> は <see cref="BattleContext.Dull"/> を通った分だけ。
    ///
    /// <para><b>自己強化の9本は入らない</b>（怒り・庇う／殉教・墓守2本・処刑・棘・澱み喰い・
    /// 軋み・分かち）。窓口を通っていないので、これは <c>AtkBonus</c> の総収支ではなく
    /// <b>「他者から受け取った正味」</b>である。</para>
    ///
    /// <para><see cref="Healed"/> / <see cref="BigAttacks"/> と同じく
    /// <b>既存の出力には出さない</b>（pulse の表に列を足すと過去の出力と diff が出る）。
    /// 診断 <c>whet</c> だけが読む。<c>verbose</c> 非依存。
    /// <see cref="Attacks"/> と組にすると<b>死蔵</b>（受け取ったのに一度も振らなかった駒）が引ける。</para>
    /// </summary>
    public int Whetted;
    public int Dulled;

    /// <summary>
    /// 燃焼の計数（第57期）。<b>どれも誰も読んで分岐しない私有カウンタ</b>で、
    /// 盤面には一切影響しない（<see cref="Whetted"/> と同じ扱いで <c>verbose</c> 非依存）。
    /// 診断 <c>burn</c> だけが読む。
    ///
    /// <para><b>着火は「誰が付けたか」ではなく「誰に付いたか」で持つ。</b>
    /// <see cref="BattleContext.Ignite"/> は付け手を受け取らないので、
    /// 引数に足すと呼び出し規約が変わり「盤面を動かさない」という保証が弱くなる。</para>
    ///
    /// <list type="bullet">
    /// <item><c>BurnLit</c> 火が<b>点いた</b>回数（<c>relit == false</c>）</item>
    /// <item><c>BurnRelit</c> 火が<b>煽られた</b>回数（既燃への再付与＝<b>捨てられた供給</b>）</item>
    /// <item><c>BurnLitAlly</c> <c>BurnLit</c> のうち <c>friendly: true</c> で点いた回数。
    ///   陣営で分ける計数（受け手の Def.Id で引く）との<b>突き合わせ用</b></item>
    /// <item><c>BurnTicks</c> 燃えたターンの延べ数（<c>TickStatuses</c> の燃焼ループを通った回数）</item>
    /// <item><c>BurnTaken</c> 燃焼の刻みで<b>実際に HP を失った量</b>。
    ///   <c>ApplyDamage</c> が <c>Hp -= amount</c> を実行した地点で数えるので、
    ///   惨禍・据え・散開・萎縮・肩代わり・破片・軛をすべて通した後の量</item>
    /// <item><c>BurnSoaked</c> 燃焼の刻みのうち<b>破片が吸った</b>量</item>
    /// <item><c>BurnDeaths</c> 燃焼の刻みで倒れた回数（表A の「燃焼で落ちた」）</item>
    /// <item><c>BurnAttacks</c> <b>燃えている状態で振った</b>回数（<see cref="Attacks"/> の内数）。
    ///   表C の稼働率の分子</item>
    /// <item><c>FirstBurnTurn</c> 最初に火が点いたターン。0 は「一度も点かなかった」</item>
    /// </list>
    /// </summary>
    public int BurnLit;
    public int BurnRelit;
    public int BurnLitAlly;
    public int BurnTicks;
    public int BurnTaken;
    public int BurnSoaked;
    public int BurnDeaths;
    public int BurnAttacks;
    public int FirstBurnTurn;

    /// <summary>とどめを刺した敵の数。</summary>
    public int Kills;

    /// <summary>倒れた回数。蘇生されて再度倒れると2になる。</summary>
    public int Deaths;

    /// <summary>
    /// 軋みが響く（第66期）の計数。<b>どれも誰も読んで分岐しない</b>
    /// ——<c>verbose</c> にも依存しない（3行 × 4版 × 5波 × 200 seed を回すため）。
    ///
    /// <para><c>CreakSwings</c> は <see cref="TraitId.Displaced"/> 保持者が
    /// <c>PerformAttack</c> を通った回数（分母。手番も割り込みも含む）、
    /// <c>CreakSweeps</c> はそのうち<b>軋みの規則で薙ぎになった</b>回数。</para>
    ///
    /// <para><c>CreakMaxBonus</c> は戦闘中の <see cref="UnitState.AtkBonus"/> の最大値。
    /// <c>CreakProbeTurn</c> は<b>閾値候補ごと</b>（<see cref="CreakProbes"/> ＝ 9 / 18 / 30）に
    /// 初めてそこへ到達したターン（0 ＝ 未到達）で、<c>CreakSelfAtProbe</c> /
    /// <c>CreakWhetAtProbe</c> / <c>CreakRegurgAtProbe</c> がその時点での
    /// <b>出どころの内訳</b>（軋み自身 ／ 窓口経由の全経路 ／ そのうち吐き戻し）。
    /// <b>規則を無効にしていても数える</b>ので、Phase 0 の分布は V0 の走査から読める。</para>
    /// </summary>
    public int CreakSwings, CreakSweeps, CreakMaxBonus;
    public int CreakSelfGain, CreakWhetGain, CreakRegurgGain;
    public int[]? CreakProbeTurn, CreakSelfAtProbe, CreakWhetAtProbe, CreakRegurgAtProbe;

    /// <summary>
    /// 第67期。条件の出どころを <c>AtkBonus</c> から <see cref="UnitState.WhetReceived"/>
    /// （<b><c>Whet</c> 窓口を通って届いた累計</b>）へ差し替えたので、
    /// <b>到達の計数もそちら向きに1本足してある</b>。
    ///
    /// <para><c>CreakWhetMax</c> は戦闘中の <c>WhetReceived</c> の最大値（＝最終値）。
    /// <c>CreakWhetProbeTurn</c> は <see cref="CreakWhetProbes"/> の各点へ初めて到達したターン
    /// （0 ＝ 未到達）。<b>規則を無効にしていても数える</b>ので、閾値の候補は V0 の走査から引ける。</para>
    /// </summary>
    public int CreakWhetMax;
    public int[]? CreakWhetProbeTurn;

    /// <summary>閾値の候補（第66期 §2-2 の V9 / V18 / V30）。<b>計数の添字はこの並び。</b></summary>
    public static readonly int[] CreakProbes = { 9, 18, 30 };

    /// <summary>
    /// 第67期の閾値の候補格子（<see cref="UnitState.WhetReceived"/> の側）。
    /// <b>版の閾値はこの格子から採る</b>——外れた値を採ると主表の「初到達T」が引けなくなる。
    /// </summary>
    public static readonly int[] CreakWhetProbes = { 1, 2, 4, 6, 8, 12, 16, 24, 32 };

    /// <summary>
    /// 第77期。条件の供給元を選択子にしたので（<see cref="CreakSource"/>）、
    /// <c>Both</c>（<c>AtkBonus + WhetReceived</c>）の側にも到達ターンの計数を1本足した。
    /// <b>格子は <see cref="CreakProbes"/> と同じ 9 / 18 / 30</b>
    /// ——<c>Both</c> の版は第66期の V9 と同じ3点で振るので、初到達Tを同じ添字で引ける。
    ///
    /// <para><b>既存の2本（<see cref="CreakProbeTurn"/> / <see cref="CreakWhetProbeTurn"/>）には
    /// 1ビットも触っていない</b>——第66・67期の表がそのまま再現することが検算になる。
    /// <b>誰も読んで分岐しない</b>ので盤面には一切影響しない。</para>
    /// </summary>
    public int[]? CreakBothProbeTurn;

    // ------------------------------------------------------------------------------------
    // 第68期（棚卸し: 条件付き変質を載せられる駒はどれか）。
    // **どれも誰も読んで分岐しない私有カウンタで、盤面には一切影響しない**
    // （Whetted / Burn* と同じ扱いで verbose 非依存）。診断 `carry` だけが読む。
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// <b>外から届いた量</b>のキー。<b>並びがそのまま計数の添字</b>。
    ///
    /// <para>第67期は「押された累計」を強化1本でしか持っていなかったので、
    /// <b>同じ格子を全キーへ広げた</b>のがこの配列。<see cref="StatusKeys.All"/> の7キーに、
    /// 窓口を持つ2つ（強化・弱体）と、engine が起こす2つ（被弾・移動）を足した形。</para>
    ///
    /// <para><b>単位は「量」と「回数」が混ざる</b>（下の <see cref="CarryUnits"/>）。
    /// 混ぜたのは格子（<see cref="CarryProbes"/>）を全キーで共有するためで、
    /// <b>被弾だけは「量」を格子に当てると全格子点が即座に飽和して情報を持たない</b>
    /// （1発が 10〜38 で、格子の上限 32 を1〜2発で越える）ので<b>回数</b>を当てる。
    /// 被弾の量そのものは <see cref="DamageTaken"/> に元からある。</para>
    /// </summary>
    public static readonly string[] CarryKeys =
        { "強化", "弱体", "毒", "燃", "痺", "標", "破片", "傷", "手番", "被弾", "移動" };

    /// <summary>各キーの単位。<b>格子はこの単位の累計に当てる。</b></summary>
    public static readonly string[] CarryUnits =
        { "量", "量", "層", "残T", "回", "回", "量", "回", "回", "回", "回" };

    /// <summary>キーの添字（<see cref="CarryKeys"/> の並び）。</summary>
    public const int CarryWhet = 0, CarryDull = 1, CarryPoison = 2, CarryBurn = 3,
                     CarryStun = 4, CarryMark = 5, CarryArmor = 6, CarryWound = 7,
                     CarryIdle = 8, CarryHit = 9, CarryMove = 10;

    /// <summary>
    /// 到達の格子。<b>第67期の <see cref="CreakWhetProbes"/> と同じ9点</b>
    /// ——器具の検算（Q3: ヨミの再現）が成り立つように、値を1つも変えていない。
    /// </summary>
    public static readonly int[] CarryProbes = { 1, 2, 4, 6, 8, 12, 16, 24, 32 };

    /// <summary>キーごとの届いた累計（単位は <see cref="CarryUnits"/>）。</summary>
    public int[]? CarryAmount;

    /// <summary>キーごとの届いた回数（＝窓口を通った回数）。</summary>
    public int[]? CarryCount;

    /// <summary>
    /// キーごと・格子ごとの<b>初到達ターン</b>（0 ＝ 未到達）。添字は <c>[キー][格子]</c>。
    /// 開戦時（ターン 0）の到達は 1 に丸める——0 を「未到達」に使うため
    /// （<see cref="CreakWhetProbeTurn"/> と同じ作法）。
    /// </summary>
    public int[][]? CarryProbeTurn;

    /// <summary>
    /// 戦闘中に <see cref="UnitState.AtkBonus"/> が<b>上がった量の総和</b>。
    /// <c>CarryAmount[CarryWhet]</c>（窓口経由＝外から）を引いた残りが<b>自前</b>
    /// ——軋み・墓守の層・溜め・怒り・棘などの自己強化9本がここに出る。
    /// </summary>
    public int CarryAtkGain;

    /// <summary>
    /// 縫いの糸口（第85期）。敵から引いた回数／味方から引いた回数／繕いが 1 点も届かなかった回数
    /// （渇きの下で塞ぎだけが走った）／繕いで実際に増えた HP。<b>盤面には一切影響しない。</b>
    /// </summary>
    public int SutureFoe, SutureAlly, SutureDry, SutureHealed;

    /// <summary>縫いが味方側から糸を引いたときの傷の深さの総和と最大（第89期）。<b>盤面には一切影響しない。</b></summary>
    public int SutureAllyDepth, SutureAllyDepthMax;

    /// <summary>巻き込み則（第85期・<c>SpillWoundRule</c>）で<b>この駒が味方に書いた</b>傷の回数。</summary>
    public int SpillWoundsWritten;

    // ------------------------------------------------------------------------------------
    // 第93期（深手）。**どれも誰も読んで分岐しない。盤面には一切影響しない。**
    // 門（§1-1）の3本（DeepReach / DeepActs / DeepOnTop）と WoundWrites* は
    // **規則の分岐より手前**にあるので版に依らない（第86期の X1P・第90期の作法）。
    // ------------------------------------------------------------------------------------

    /// <summary>傷の窓口（<c>BattleContext.Wound</c>）を通って<b>この駒が書いた</b>傷の量。<b>書き手の側に載る。</b></summary>
    public int WoundWrites;

    /// <summary>
    /// 同・経路別（添字は <c>WoundRoute</c>）。<b>加算だけが窓口を通る</b>ので、これが傷の供給の全数になる。
    /// <c>WoundRoute.Gather</c> だけは新しい供給ではなく中継（盤面の総量を増やさない）。
    /// </summary>
    public int[]? WoundWritesByRoute;

    /// <summary>
    /// 門（§1-1 の 1）。<b>この駒の傷が <c>DeepRule.Bundle</c> に達した回数</b>（＜Bundle → ≧Bundle の越え）。
    /// <b>版に依らない</b>ので W0 で数えられる。<c>DeepReachFirstTurn</c> は初到達ターン、
    /// <c>DeepReachTurnSum</c> は到達ターンの総和。
    /// </summary>
    public int DeepReach, DeepReachFirstTurn, DeepReachTurnSum;

    /// <summary>門（§1-1 の 2）。<b>達した駒がその後に行動した回数</b>＝自傷が払い出される機会。<b>版に依らない。</b></summary>
    public int DeepActs;

    /// <summary>門（§1-1 の 3）。<b>達した駒にその後さらに傷が書かれた回数</b>＝上乗せの機会。<b>版に依らない。</b></summary>
    public int DeepOnTop;

    /// <summary>実際に<b>深手になった</b>回数と初回のターン（W1 のみ。二値なので 1 戦に何度も立つのは蘇生と会戦の境界だけ）。</summary>
    public int DeepBundles, DeepBundleFirstTurn;

    /// <summary>自傷（§2-3）の発火回数と額面（回数 × <c>DeepRule.DeepBite</c>）。</summary>
    public int DeepBiteFires, DeepBiteOut;

    /// <summary>自傷が中継（巨躯・分かち）に拾われた回数（§1-2 の 3）。<b>仕様として残してある。</b></summary>
    public int DeepBiteRelayed;

    /// <summary>上乗せ（§0-4 の 2）の発火回数（＝溜まらずに化けた傷の数）と額面。</summary>
    public int DeepOverFires, DeepOverOut;

    /// <summary>深手を持つ駒が手番を止められた回数（痺れ・まどろみ・<c>CanAct</c> 偽）。</summary>
    public int DeepStalled;

    /// <summary>滲み則が<b>深手</b>を読んで毒を +2 した回数（傷だけの +1 とは別に数える）。</summary>
    public int DeepSoakDeeper;

    /// <summary>引き取り（<c>GatherRule</c>）が走った時点で<b>受け手が既に深手だった</b>回数（Q5）。</summary>
    public int DeepGatherAfter;

    /// <summary>
    /// 傷の引き取り（第89期・<c>GatherRule</c>）。庇いが成立した回数／そのうち隣に傷のある味方がいた回数／
    /// 実際に引き取った回数／引き取った直後の自分の傷の深さの総和／その最大。
    /// <b>盤面には一切影響しない。</b>
    /// </summary>
    public int GatherGuards, GatherHadDonor, GatherTaken, GatherDepthSum, GatherDepthMax;

    /// <summary>
    /// 滲み則（第90期・<c>SoakRule</c>）。<b>書き手の側に載る。盤面には一切影響しない。</b>
    /// <para><c>SoakPoisonWrites</c> 毒の窓口を通った回数（分母）／
    /// <c>SoakPoisonSeen</c> そのうち<b>相手が傷を持っていた</b>回数（<b>版に依らず数える</b>＝紙の分子）／
    /// <c>SoakPoisonSeenAlly</c> 同・相手が同陣営（味方漏れ）だった回数／
    /// <c>SoakPoisonAdded</c> 実際に層を +1 した回数（W1 のみ）／
    /// <c>SoakBurn*</c> 燃焼の同じ4本。</para>
    /// </summary>
    public int SoakPoisonWrites, SoakPoisonSeen, SoakPoisonSeenAlly, SoakPoisonAdded;

    /// <summary>滲み則（第90期）の燃焼側。<see cref="SoakPoisonWrites"/> の doc を参照。</summary>
    public int SoakBurnWrites, SoakBurnSeen, SoakBurnSeenAlly, SoakBurnAdded;

    /// <summary>
    /// 滲み則の経路別（第90期・添字は <see cref="PoisonRoute"/>）。
    /// 毒 5 経路 ＋ 燃焼 1 経路を1本の配列で持つ（末尾が燃焼）。<b>盤面には一切影響しない。</b>
    /// </summary>
    public int[]? SoakSeenByRoute;

    /// <summary>
    /// 自己給餌（第90期 §1-2 の 4）。<b>ボルグの巻き込み（余波）が味方に傷を書き、
    /// その味方に同じ一振りの火の粉が滲み則で深く入った</b>回数。<b>盤面には一切影響しない。</b>
    /// </summary>
    public int SoakSelfFeed;

    /// <summary>
    /// 継ぎ当ての繕い（第86期・<c>MendRule</c>）。発火回数／そのうち患者に傷があった回数（<b>版に依らず観測する</b>）／観測した傷の総深さ／
    /// 繕いが 1 点も届かなかった回数（渇き）／実際に増えた HP／自分が払った HP／患者が敵だった回数（0 のはず）。
    /// <b>盤面には一切影響しない。</b>
    /// </summary>
    public int MendFires, MendWoundSeen, MendWoundDepth, MendDry, MendHealed, MendPaid, MendFoePatient;

    /// <summary>
    /// 読まれないまま落ちた傷（第85期・自己検査 (j)）——倒れた時点で負っていた傷の数と、
    /// 戦闘終了時に生き残った駒が負っていた傷の数。<b>盤面には一切影響しない。</b>
    /// </summary>
    public int WoundsAtDeath, WoundsAtEnd;

    /// <summary>
    /// 傷口の着火（第87期・<c>IgniteRule</c>）。澱み（<see cref="TraitId.Amplifier"/>）の側に載る。
    /// <b>盤面には一切影響しない。</b>
    /// <para><c>AmpFires</c> 濃縮の発火回数（＝ミオが手番を持てたターン数）／
    /// <c>AmpThickened</c> 濃くした延べ体数／
    /// <c>AmpIgnitable</c>「傷を持ち毒を持たない敵」を見た延べ回数（<b>版に依らず数える</b>）／
    /// <c>AmpIgnitableBodies</c> 同・実体数（駒ごとに1度だけ）／
    /// <c>AmpIgnited</c> 実際に着火した回数（Y1 のみ）／<c>AmpIgniteAmount</c> 置いた層の総和（自己検査 (f)）／
    /// <c>AmpIgniteWoundBefore</c> / <c>AmpIgniteWoundAfter</c> 着火の前後の傷の深さの総和（自己検査 (g)）／
    /// <c>AmpIgnitePoisonAfter</c> 着火直後の毒の総和（自己検査 (e)）／
    /// <c>AmpFirstIgnitableTurn</c> / <c>AmpFirstIgniteTurn</c> 初出ターン。</para>
    /// </summary>
    public int AmpFires, AmpThickened, AmpIgnitable, AmpIgnitableBodies, AmpIgnited,
               AmpIgniteAmount, AmpIgniteWoundBefore, AmpIgniteWoundAfter, AmpIgnitePoisonAfter,
               AmpFirstIgnitableTurn, AmpFirstIgniteTurn;

    /// <summary>
    /// 着火された駒が<b>以後に受けた毒の刻み</b>（第87期・持続係数の分子）。着火された駒の側に載る。
    /// 着火の時点で毒は 0 だったので、その駒がその後に受ける毒はすべて着火の下流にある。
    /// <b>盤面には一切影響しない。</b>
    /// </summary>
    public int IgnitePoisonDamage, IgnitePoisonTicks;

    /// <summary>
    /// 抉り（<see cref="TraitId.Gouge"/>）の発火回数と上乗せの総量（第87期・持続係数の検算用）。
    /// <b>盤面には一切影響しない。</b>
    /// </summary>
    public int GougeFires, GougeOut;

    public void Add(UnitTally o)
    {
        SutureFoe += o.SutureFoe; SutureAlly += o.SutureAlly; SutureDry += o.SutureDry; SutureHealed += o.SutureHealed;
        SutureAllyDepth += o.SutureAllyDepth; SutureAllyDepthMax = Math.Max(SutureAllyDepthMax, o.SutureAllyDepthMax);
        GatherGuards += o.GatherGuards; GatherHadDonor += o.GatherHadDonor; GatherTaken += o.GatherTaken;
        GatherDepthSum += o.GatherDepthSum; GatherDepthMax = Math.Max(GatherDepthMax, o.GatherDepthMax);
        SpillWoundsWritten += o.SpillWoundsWritten;
        // 深手（第93期）。**盤面には一切影響しない。**
        WoundWrites += o.WoundWrites;
        if (o.WoundWritesByRoute is not null)
        {
            WoundWritesByRoute ??= new int[o.WoundWritesByRoute.Length];
            for (int i = 0; i < o.WoundWritesByRoute.Length; i++) WoundWritesByRoute[i] += o.WoundWritesByRoute[i];
        }
        DeepReach += o.DeepReach; DeepReachTurnSum += o.DeepReachTurnSum;
        if (o.DeepReachFirstTurn > 0 && (DeepReachFirstTurn == 0 || o.DeepReachFirstTurn < DeepReachFirstTurn)) DeepReachFirstTurn = o.DeepReachFirstTurn;
        DeepActs += o.DeepActs; DeepOnTop += o.DeepOnTop; DeepBundles += o.DeepBundles;
        if (o.DeepBundleFirstTurn > 0 && (DeepBundleFirstTurn == 0 || o.DeepBundleFirstTurn < DeepBundleFirstTurn)) DeepBundleFirstTurn = o.DeepBundleFirstTurn;
        DeepBiteFires += o.DeepBiteFires; DeepBiteOut += o.DeepBiteOut; DeepBiteRelayed += o.DeepBiteRelayed;
        DeepOverFires += o.DeepOverFires; DeepOverOut += o.DeepOverOut;
        DeepStalled += o.DeepStalled; DeepSoakDeeper += o.DeepSoakDeeper; DeepGatherAfter += o.DeepGatherAfter;
        // 滲み則（第90期）。**盤面には一切影響しない。**
        SoakPoisonWrites += o.SoakPoisonWrites; SoakPoisonSeen += o.SoakPoisonSeen;
        SoakPoisonSeenAlly += o.SoakPoisonSeenAlly; SoakPoisonAdded += o.SoakPoisonAdded;
        SoakBurnWrites += o.SoakBurnWrites; SoakBurnSeen += o.SoakBurnSeen;
        SoakBurnSeenAlly += o.SoakBurnSeenAlly; SoakBurnAdded += o.SoakBurnAdded;
        SoakSelfFeed += o.SoakSelfFeed;
        if (o.SoakSeenByRoute is not null)
        {
            SoakSeenByRoute ??= new int[o.SoakSeenByRoute.Length];
            for (int i = 0; i < o.SoakSeenByRoute.Length; i++) SoakSeenByRoute[i] += o.SoakSeenByRoute[i];
        }
        MendFires += o.MendFires; MendWoundSeen += o.MendWoundSeen; MendWoundDepth += o.MendWoundDepth;
        MendDry += o.MendDry; MendHealed += o.MendHealed; MendPaid += o.MendPaid; MendFoePatient += o.MendFoePatient;
        WoundsAtDeath += o.WoundsAtDeath; WoundsAtEnd += o.WoundsAtEnd;
        AmpFires += o.AmpFires; AmpThickened += o.AmpThickened; AmpIgnitable += o.AmpIgnitable;
        AmpIgnitableBodies += o.AmpIgnitableBodies; AmpIgnited += o.AmpIgnited;
        AmpIgniteAmount += o.AmpIgniteAmount; AmpIgniteWoundBefore += o.AmpIgniteWoundBefore;
        AmpIgniteWoundAfter += o.AmpIgniteWoundAfter; AmpIgnitePoisonAfter += o.AmpIgnitePoisonAfter;
        if (o.AmpFirstIgnitableTurn > 0 && (AmpFirstIgnitableTurn == 0 || o.AmpFirstIgnitableTurn < AmpFirstIgnitableTurn)) AmpFirstIgnitableTurn = o.AmpFirstIgnitableTurn;
        if (o.AmpFirstIgniteTurn > 0 && (AmpFirstIgniteTurn == 0 || o.AmpFirstIgniteTurn < AmpFirstIgniteTurn)) AmpFirstIgniteTurn = o.AmpFirstIgniteTurn;
        IgnitePoisonDamage += o.IgnitePoisonDamage; IgnitePoisonTicks += o.IgnitePoisonTicks;
        GougeFires += o.GougeFires; GougeOut += o.GougeOut;
        Attacks += o.Attacks; Interventions += o.Interventions;
        DamageToEnemy += o.DamageToEnemy; DamageToAlly += o.DamageToAlly;
        DamageTaken += o.DamageTaken; TakenFromAlly += o.TakenFromAlly;
        Healed += o.Healed;
        Charges += o.Charges; BigAttacks += o.BigAttacks;
        Swallowed += o.Swallowed; Slumbers += o.Slumbers;
        Refunds += o.Refunds; Refunded += o.Refunded;
        Kills += o.Kills; Deaths += o.Deaths;
        Whetted += o.Whetted; Dulled += o.Dulled;
        BurnLit += o.BurnLit; BurnRelit += o.BurnRelit; BurnLitAlly += o.BurnLitAlly;
        BurnTicks += o.BurnTicks; BurnTaken += o.BurnTaken; BurnSoaked += o.BurnSoaked;
        BurnDeaths += o.BurnDeaths; BurnAttacks += o.BurnAttacks;
        // FirstBurnTurn は**加算しない**。0（一度も点かなかった）を除いた最小値を取る
        // ——LastActiveTurn の Math.Max と同じく、合算の順序に依存しない形にする。
        FirstBurnTurn = FirstBurnTurn == 0 ? o.FirstBurnTurn
                      : o.FirstBurnTurn == 0 ? FirstBurnTurn
                      : Math.Min(FirstBurnTurn, o.FirstBurnTurn);
        // LastActiveTurn は**加算しない**。ターン番号は足しても意味を持たない。
        // Math.Max を取るのは、合算の順序に依存しない（可換・結合的）ため——
        // 「最後の値を残す」方式は Add を呼ぶ順で答えが変わる。
        LastActiveTurn = Math.Max(LastActiveTurn, o.LastActiveTurn);
        // 軋み（第66期）。回数は加算、最大値は Math.Max、到達ターンは FirstBurnTurn と同じ扱い。
        CreakSwings += o.CreakSwings; CreakSweeps += o.CreakSweeps;
        CreakSelfGain += o.CreakSelfGain; CreakWhetGain += o.CreakWhetGain;
        CreakRegurgGain += o.CreakRegurgGain;
        CreakMaxBonus = Math.Max(CreakMaxBonus, o.CreakMaxBonus);
        CreakWhetMax = Math.Max(CreakWhetMax, o.CreakWhetMax);
        // 第68期。量と回数は加算、初到達ターンは FirstBurnTurn と同じ扱い（0 を除いた最小値）。
        CarryAtkGain += o.CarryAtkGain;
        if (o.CarryAmount is not null)
        {
            CarryAmount ??= new int[CarryKeys.Length];
            CarryCount ??= new int[CarryKeys.Length];
            for (int i = 0; i < CarryKeys.Length; i++)
            {
                CarryAmount[i] += o.CarryAmount[i];
                CarryCount[i] += o.CarryCount![i];
            }
        }
        if (o.CarryProbeTurn is not null)
        {
            CarryProbeTurn ??= new int[CarryKeys.Length][];
            for (int i = 0; i < CarryKeys.Length; i++)
            {
                if (o.CarryProbeTurn[i] is not int[] src) continue;
                int[] dst = CarryProbeTurn[i] ??= new int[CarryProbes.Length];
                for (int j = 0; j < CarryProbes.Length; j++)
                    dst[j] = dst[j] == 0 ? src[j] : src[j] == 0 ? dst[j] : Math.Min(dst[j], src[j]);
            }
        }
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

    /// <summary>
    /// 肩代わり（巨躯・分かち）が元のダメージを分割して<b>中継した段</b>か（第85期）。
    /// <see cref="FriendlyFire"/> が真でも、元の刃が味方ならこの段の <c>ActorId</c> は味方になる
    /// ——「味方の刃が着弾した回数」を数えるときにこの段を外すための札。
    /// </summary>
    public bool Relayed { get; init; }

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
    /// 強化（第56期）の計数。<b>窓口 <see cref="BattleContext.Whet"/> を通った量を経路別に数える。</b>
    /// <see cref="DullTotal"/> と対で、<b>他者強化の6経路だけ</b>が通る（自己強化の9本は直叩きのまま）。
    /// <b>verbose に依存しない</b>。
    ///
    /// <para><c>WhetTotal</c> 総量（両陣営） ／ <c>WhetByRoute</c> 経路別（<see cref="WhetRoute"/> の順） ／
    /// <c>WhetToPerverse</c> 逆しま（ウツ）が受けた量 ／
    /// <c>WhetPerverseFlips</c> それで符号が正へ渡った回数（<b>半減側へ落ちた回数</b>）。</para>
    ///
    /// <para>強化源（カリ／ガン／クグ／シオ／ゴルム）がいなければ全部 0。
    /// 駒ごとの受取量は <see cref="TallyByUnit"/> の <see cref="UnitTally.Whetted"/> 側。</para>
    /// </summary>
    public required int WhetTotal { get; init; }
    public required IReadOnlyList<int> WhetByRoute { get; init; }
    public required int WhetToPerverse { get; init; }
    public required int WhetPerverseFlips { get; init; }

    /// <summary>
    /// 強化の<b>到着の時刻と使用</b>（第65期）。<see cref="WhetByRoute"/> と同じ並び。
    ///
    /// <para><c>WhetTurnSumByRoute</c> ÷ <see cref="WhetByRoute"/> = <b>到着の平均ターン</b> ／
    /// <c>WhetFirstTurnSumByRoute</c> ÷ <c>WhetFirstTurnCountByRoute</c> = <b>初到着の平均ターン</b>
    /// （<b>1戦につき1回</b>しか数えないので、量の多い経路に引っ張られない） ／
    /// <c>WhetUsedByRoute</c> ÷ <see cref="WhetByRoute"/> = <b>使用率</b>
    /// （受け取った<b>後</b>に受け手が攻撃力を出力へ変換した量の割合）。</para>
    ///
    /// <para><b>経路を落とした版でも数える</b>（落ちるのは <c>AtkBonus</c> への加算だけ）。
    /// <b>誰も読んで分岐しない</b>・<c>verbose</c> に依存しない。</para>
    /// </summary>
    public required IReadOnlyList<int> WhetTurnSumByRoute { get; init; }
    public required IReadOnlyList<int> WhetFirstTurnSumByRoute { get; init; }
    public required IReadOnlyList<int> WhetFirstTurnCountByRoute { get; init; }
    public required IReadOnlyList<int> WhetUsedByRoute { get; init; }

    /// <summary>
    /// <b>経路ごとの受け手</b>（第65期。キーは <c>Def.Id</c>・<see cref="WhetRoute"/> の順）。
    /// 「行き先を決めているものは何か」を実測するための唯一の窓で、
    /// <b>誰も読んで分岐しない</b>・<c>verbose</c> に依存しない。
    /// </summary>
    public required IReadOnlyList<IReadOnlyDictionary<string, int>> WhetToByRoute { get; init; }

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

    /// <summary>
    /// 驕り（第46期）の計数。<b>隣接を「量」ではなく「誰がいるか」で読む初めての出力条件</b>で、
    /// <b>発火しなかったことは盤面の値に痕跡を残さない</b>ので診断が読むためだけに数える
    /// （<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><c>OverbearFired</c> 削った延べ体数 ／ <c>OverbearTotal</c> 撒いた総量 ／
    /// <c>OverbearTo</c> 削った相手の内訳（駒名 → 量） ／
    /// <c>OverbearMetTurns</c> ターン頭に条件が成立していたターン数 ／
    /// <c>OverbearTurns</c> 保持者が生きてターン頭を迎えた回数（成立率の分母） ／
    /// <c>OverbearFirstTurn</c> 条件が初めて成立したターン（一度も成立しなければ 0） ／
    /// <c>OverbearSwings</c> 保持者が振った回数 ／ <c>OverbearDoubled</c> そのうち2倍が乗った回数 ／
    /// <c>OverbearBackfire</c> 削ったのに相手の <c>CurrentAttack</c> が<b>上がった</b>量
    /// （逆しまの自己矛盾。<c>OverbearBackfireHits</c> がその回数）。</para>
    ///
    /// <para>保持者（<c>UnitCatalog.Ogo</c>）を編成に入れなければ全部 0。</para>
    /// </summary>
    public required int OverbearFired { get; init; }
    public required int OverbearTotal { get; init; }
    public required IReadOnlyDictionary<string, int> OverbearTo { get; init; }
    public required int OverbearMetTurns { get; init; }
    public required int OverbearTurns { get; init; }
    public required int OverbearFirstTurn { get; init; }
    public required int OverbearSwings { get; init; }
    public required int OverbearDoubled { get; init; }
    public required int OverbearBackfire { get; init; }
    public required int OverbearBackfireHits { get; init; }

    /// <summary>
    /// 鱗（第47期）の計数。<b>アーマー（<see cref="StatusKeys.Armor"/>）に初めて読み手が付いた</b>ので、
    /// 供給・発揮・消費の3段をそれぞれ別に数える（<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><b>獲得</b>: <c>ScaleGainDeath</c> 味方の死から ／ <c>ScaleGainShatter</c> 砕けの破片から ／
    /// <c>ScaleGainBear</c> 集約の鎧から ／ <c>ScaleGainEphemeral</c> はそのうち儚い駒（胞子）の死から来たぶん
    /// （<c>ScaleGainDeath</c> の内数） ／ <c>ScaleFirstTurn</c> 初めて纏ったターン（一度も纏わなければ 0）。</para>
    ///
    /// <para><b>纏い率</b>: <c>ScaleWornTurns</c> ÷ <c>ScaleAliveTurns</c>。分母は保持者が生きて
    /// ターン頭を迎えた回数で、<b>戦闘の全ターン数ではない</b>（早く落ちる駒で率が下がらない）。</para>
    ///
    /// <para><b>発揮</b>: <c>ScaleSwings</c> 振った回数 ／ <c>ScalePierceSwings</c> そのうち貫きだった回数 ／
    /// <c>ScaleBackHits</c> <b>貫きが後列の敵に当たった回数</b> ／ <c>ScaleBackDamage</c> その量（減衰後）。
    /// <b>貫いた回数は成果ではない</b>——後列に敵がいなければ単体攻撃と同じである。</para>
    ///
    /// <para><b>消費</b>: <c>ScaleSpentAttack</c> 攻撃で消費した量 ／ <c>ScaleSpentHit</c> 被弾で吸われた量
    /// （<b>二重支出</b>のどちらが律速かを読む） ／ <c>ScaleDepleted</c> 0 に戻った回数 ／
    /// <c>ScaleLeftover</c> 決着時に残っていた量（＝死蔵） ／
    /// <c>ScaleFullSoaks</c> 破片が被弾を受け切った回数（受け切ると <c>OnDamaged</c> が呼ばれない）。</para>
    ///
    /// <para>保持者（<c>UnitCatalog.Uro</c>）を編成に入れなければ全部 0。</para>
    /// </summary>
    public required int ScaleGainDeath { get; init; }
    public required int ScaleGainShatter { get; init; }
    public required int ScaleGainBear { get; init; }
    public required int ScaleGainEphemeral { get; init; }
    public required int ScaleFirstTurn { get; init; }
    public required int ScaleAliveTurns { get; init; }
    public required int ScaleWornTurns { get; init; }
    public required int ScaleSwings { get; init; }
    public required int ScalePierceSwings { get; init; }
    public required int ScaleBackHits { get; init; }
    public required int ScaleBackDamage { get; init; }
    public required int ScaleSpentAttack { get; init; }
    public required int ScaleSpentHit { get; init; }
    public required int ScaleDepleted { get; init; }
    public required int ScaleFullSoaks { get; init; }
    public required int ScaleLeftover { get; init; }

    /// <summary>
    /// 業（第49期）の計数。<b>ロスターで初めて「状態異常の種類数」を読む駒</b>なので、
    /// 引き取り・発揮・代金の3段をそれぞれ別に数える（<c>verbose</c> には依存しない）。
    ///
    /// <para><b>引き取り</b>: <c>ScapegoatTakes</c> 移した延べ量 ／ <c>ScapegoatTakeByKind</c> 種類別 ／
    /// <c>ScapegoatTakeFrom</c> 取った相手の内訳 ／ <c>ScapegoatMissed</c> 引き取れる種類が
    /// 盤面に無かった回数（空振り） ／ <c>ScapegoatFull</c> 全種類を既に背負っていた回数
    /// （<b>空振りとは原因が違うので分ける</b>）。</para>
    ///
    /// <para><b>種類数と到達</b>: <c>ScapegoatKindMax</c> 最大 ／ <c>ScapegoatKindSum</c> ÷
    /// <c>ScapegoatAliveTurns</c> が平均 ／ <c>ScapegoatMetTurns</c> ÷ <c>ScapegoatAliveTurns</c> が成立率 ／
    /// <c>ScapegoatFirstTurn</c> 閾値に初めて達したターン（一度も達しなければ 0）。</para>
    ///
    /// <para><b>転写</b>: <c>ScapegoatSwings</c> 振った回数 ／ <c>ScapegoatFired</c> 転写した回数 ／
    /// <c>ScapegoatWriteByKind</c> 書いた延べ数（種類別）。</para>
    ///
    /// <para><b>転写の効き</b>（<b>付けた回数は成果ではない</b>）: <c>ScapegoatFoeDot</c> 業が書いた
    /// 毒・燃焼が実際に削った量 ／ <c>ScapegoatFoeSkips</c> 業が書いた痺れで敵が飛ばした手番 ／
    /// <c>ScapegoatMarkPulls</c> 業が書いた標に味方の単体攻撃が引かれた回数。</para>
    ///
    /// <para><b>自傷と味方の救済</b>: <c>ScapegoatDotByUnit</c> / <c>ScapegoatSkipByUnit</c> は
    /// <b>味方側の被害を駒ごと（<c>Def.Id</c>）に割ったもの</b>。<b>「保持者かどうか」で箱を
    /// 分けていない</b>——分けると素体の対照（特性なし・同数値）が別の箱に落ちて引き算できない。
    /// <b>帰属は素体との差で取る</b>——瘴気の毒は引き取らなくても味方全員に載るので、
    /// 絶対値だけでは機構のぶんが割れない。</para>
    ///
    /// <para>保持者（<c>UnitCatalog.Gou</c>）を編成に入れなければ全部 0。</para>
    /// </summary>
    public required int ScapegoatTakes { get; init; }
    public required IReadOnlyDictionary<string, int> ScapegoatTakeByKind { get; init; }
    public required IReadOnlyDictionary<string, int> ScapegoatTakeFrom { get; init; }
    public required int ScapegoatMissed { get; init; }
    public required int ScapegoatFull { get; init; }
    public required int ScapegoatAliveTurns { get; init; }
    public required int ScapegoatMetTurns { get; init; }
    public required int ScapegoatKindSum { get; init; }
    public required int ScapegoatKindMax { get; init; }
    public required int ScapegoatFirstTurn { get; init; }
    public required int ScapegoatSwings { get; init; }
    public required int ScapegoatFired { get; init; }
    public required IReadOnlyDictionary<string, int> ScapegoatWriteByKind { get; init; }
    public required int ScapegoatFoeDot { get; init; }
    public required int ScapegoatFoeSkips { get; init; }
    public required int ScapegoatMarkPulls { get; init; }
    public required IReadOnlyDictionary<string, int> ScapegoatDotByUnit { get; init; }
    public required IReadOnlyDictionary<string, int> ScapegoatSkipByUnit { get; init; }

    /// <summary>
    /// 逸らし（第50期）の計数。<b>ロスターで初めて標（<c>StatusKeys.Marked</c>）を操作する駒</b>なので、
    /// 外し・焦点・効き・代金をそれぞれ別に数える（<c>verbose</c> には依存しない）。
    ///
    /// <para><b>発火</b>: <c>DivertFires</c>（<b>0 になっていないことが受け入れ基準4</b>
    /// ——配置探索が機構を無効化する席を選んでいないか）。</para>
    ///
    /// <para><b>外し</b>: <c>DivertStrips</c> 味方から外した回数 ／ <c>DivertStripFrom</c> 相手の内訳。</para>
    ///
    /// <para><b>焦点</b>: <c>DivertFocus</c> 敵に付けた回数 ／ <c>DivertFocusFresh</c> そのうち
    /// 新しく標が付いた回数 ／ <c>DivertFocusTo</c> 相手の内訳 ／
    /// <c>DivertMarkedFoeSum</c> ÷ <c>DivertFires</c> が<b>「標を持つ敵の数」の平均</b>
    /// （敵の標は消えないので<b>焦点は自分で溶ける</b>——この列がその実測）。</para>
    ///
    /// <para><b>焦点の効き</b>（<b>付けた回数は成果ではない</b>）: <c>DivertAllyOnMarked</c> ÷
    /// <c>DivertAllySingles</c> ＝ 味方の単体振りのうち標持ちに当たった割合。
    /// <c>DivertAllyPulls</c> は engine の鎖が<b>実際に主目標を差し替えた</b>回数。</para>
    ///
    /// <para><b>代金</b>: <c>DivertFoeOnMarked</c> ÷ <c>DivertFoeSingles</c> ＝
    /// 敵の単体振りのうち標持ちの味方に当たった割合。<c>DivertFoePulls</c> は差し替えた回数。</para>
    ///
    /// <para><b>撃破順</b>（<b>本命の指標</b>）: <c>DivertKillTurnByFoe</c> / <c>DivertKillCountByFoe</c>
    /// は敵の駒ごとの撃破ターン。<b>標に依存しない切り方</b>なので素体の対照とそのまま引き算できる。</para>
    ///
    /// <para>保持者（<c>UnitCatalog.Sora</c>）を編成に入れず監査も切っていれば全部 0。</para>
    /// </summary>
    public required int DivertFires { get; init; }
    public required int DivertStrips { get; init; }
    public required int DivertFocus { get; init; }
    public required int DivertFocusFresh { get; init; }
    public required IReadOnlyDictionary<string, int> DivertStripFrom { get; init; }
    public required IReadOnlyDictionary<string, int> DivertFocusTo { get; init; }
    public required int DivertMarkedFoeSum { get; init; }
    public required int DivertMarkedFoeMax { get; init; }
    public required int DivertAllySingles { get; init; }
    public required int DivertAllyOnMarked { get; init; }
    public required int DivertFoeSingles { get; init; }
    public required int DivertFoeOnMarked { get; init; }
    public required int DivertAllyPulls { get; init; }
    public required int DivertFoePulls { get; init; }
    public required IReadOnlyDictionary<string, int> DivertKillTurnByFoe { get; init; }
    public required IReadOnlyDictionary<string, int> DivertKillCountByFoe { get; init; }

    /// <summary>
    /// 駆り立て（第52期）の計数。<b>ロスターで2枚目の標の書き手</b>（1枚目は囃し立て＝開戦時1回）で、
    /// <b>毎ターン・最高攻撃力の隣接味方</b>に標と強化を同時に渡す。
    ///
    /// <para><b>発火</b>: <c>GoadFires</c>（<b>0 になっていないことが受け入れ基準4</b>
    /// ——配置探索が機構を無効化する席を選んでいないか。第49期の業改の失敗）。
    /// <c>GoadIdle</c> は<b>空振り</b>（隣接に候補がいなくて何もしなかった回数）。</para>
    ///
    /// <para><b>渡した量</b>: <c>GoadGiven</c>（<c>AtkBonus</c> の累積付与量）。
    /// <b>これは成果ではない</b>——対象が渡した直後に死ぬならダメージに変わっていない。
    /// <b>効きは診断が素体との差（対象の <c>DamageToEnemy</c>）で取る。</b></para>
    ///
    /// <para><b>対象</b>: <c>GoadTargetTo</c> が渡した相手の内訳、<c>GoadSwitches</c> が
    /// 対象が入れ替わった回数。<b>強化するほどその駒が選ばれ続ける</b>設計なので、
    /// <c>GoadSwitches</c> が小さいほど狙いどおり（強化と危険が1体に集中している）。</para>
    ///
    /// <para><b>干渉</b>: <c>GoadMarkLost</c> は付けた標が次の発火までに剥がされていた回数
    /// （逸らし＝ソラが唯一の経路・<b>席番号の順序に依存</b>）、
    /// <c>GoadToPerverse</c> は渡した先が逆しま（ウツ）だった回数（<b>強化が害になる</b>）。</para>
    /// </summary>
    public required int GoadFires { get; init; }
    public required int GoadIdle { get; init; }
    public required int GoadGiven { get; init; }
    public required int GoadSwitches { get; init; }
    public required int GoadMarkLost { get; init; }
    public required int GoadToPerverse { get; init; }
    public required IReadOnlyDictionary<string, int> GoadTargetTo { get; init; }

    /// <summary>
    /// 止め（第53期）の計数。<b>ロスターで初めて「敵に付いた標」を読む駒。</b>
    ///
    /// <para><b>発火</b>: <c>FinisherFires</c>（標を持つ敵を殴った回数。
    /// <b>0 になっていないことが受け入れ基準3・4</b>）。<c>FinisherIdle</c> は<b>空振り</b>
    /// （標を持つ敵が1体もいなくて通常の対象選択に戻った回数）。</para>
    ///
    /// <para><b>列越え</b>: <c>FinisherCross</c>（<b>標が無ければ狙えなかった敵</b>＝
    /// <c>PoolOf</c> の外を殴った回数）。<b>発火は成果ではない</b>——標が持つ
    /// 「前列の壁を破る」特権を実際に使えたかはこちらでしか読めない（受け入れ基準6）。</para>
    ///
    /// <para><b>止めた砲火</b>: <c>FinisherStarved</c> ÷ <c>FinisherAllySingles</c>。
    /// 標を消すと engine の <c>MarkPullPercent</c> も切れるので、
    /// <b>味方全体の集中砲火を自分で終わらせる</b>——これが代金の実体（受け入れ基準7）。
    /// <b>推定値</b>なので、厳密な代金は診断が<b>対照2（消費なし版）との差</b>で取る。</para>
    ///
    /// <para><b>遊休</b>: <c>FinisherWaitSum</c> ÷ <c>FinisherWaitCount</c>
    /// （標が付いてから止めが殴るまでの平均ターン数）。</para>
    /// </summary>
    public required int FinisherFires { get; init; }
    public required int FinisherIdle { get; init; }
    public required int FinisherCross { get; init; }
    public required int FinisherConsumed { get; init; }
    public required int FinisherKills { get; init; }
    public required int FinisherWaitSum { get; init; }
    public required int FinisherWaitCount { get; init; }
    public required int FinisherAllySingles { get; init; }
    public required int FinisherStarved { get; init; }
    public required IReadOnlyDictionary<string, int> FinisherTargetTo { get; init; }

    /// <summary>
    /// 火選り（第58期）の計数。<b>ロスターで初めて「味方に付いた燃焼」を読む駒。</b>
    ///
    /// <para><b>発火</b>: <c>FavorFires</c>（強化か弱体を1体でも配った手番の数。
    /// <b>0 になっていないことが受け入れ基準</b>）。<c>FavorIdle</c> は<b>空振り</b>
    /// （盤上に燃えている味方が1体もいなかった手番）で、<b>第1ターンは構造的にここへ落ちる</b>
    /// ——<c>OnTurnStart</c> は行動順ループの外側なので、火の粉（<c>OnAfterAttack</c>）より先に走る。</para>
    ///
    /// <para><b>体数と量を分けてある</b>: <c>FavorWhetted</c> / <c>FavorDulled</c> が延べ体数、
    /// <c>FavorGiven</c> / <c>FavorTaken</c> が量。掃引で <c>Gain</c> / <c>Loss</c> を振ると
    /// 量だけが動いて体数は動かない——<b>ノブが機構の計数を動かしたかの切り分け</b>
    /// （第49期・全幅が小さいときの読み方）にこの2本が要る。</para>
    ///
    /// <para><b><c>FavorToPyre</c> が Q4 の分子。</b> 配った強化のうち熾火（乗算持ち）へ
    /// 落ちた量で、<b>そこだけ実効 4 倍で入る</b>（<c>UnitState.CurrentAttack</c> は
    /// <c>Def.Attack + AtkBonus</c> を作ってから <c>ModifyAttack</c> を通す）。</para>
    /// </summary>
    public required int FavorFires { get; init; }
    public required int FavorIdle { get; init; }
    public required int FavorWhetted { get; init; }
    public required int FavorDulled { get; init; }
    public required int FavorGiven { get; init; }
    public required int FavorTaken { get; init; }
    public required int FavorToPyre { get; init; }
    public required IReadOnlyDictionary<string, int> FavorWhetTo { get; init; }
    public required IReadOnlyDictionary<string, int> FavorDullTo { get; init; }

    /// <summary>
    /// 瘴気と毒の刻みの計数（第61期）。<b>誰も読んで分岐しない</b>ので盤面には影響しない。
    /// <c>MiasmaFires</c> は瘴気が撒いた回数、<c>MiasmaToFoe</c> / <c>MiasmaToAlly</c> は
    /// 撒いた層の総量（味方側は<b>撒いた本人を含む</b>）。
    /// <c>PoisonBite*</c> は毒の刻みの<b>額面</b>を陣営で割ったもので、実際に減った HP ではない。
    /// 診断 <c>miasma</c> だけが読む。
    /// </summary>
    public required int MiasmaFires { get; init; }
    public required int MiasmaToFoe { get; init; }
    public required int MiasmaToAlly { get; init; }
    public required int PoisonBitePlayer { get; init; }
    public required int PoisonBiteEnemy { get; init; }
    public required int PoisonTicksPlayer { get; init; }
    public required int PoisonTicksEnemy { get; init; }

    /// <summary>
    /// 横流し（第62期）の計数。<b>ロスターで初めて「強化の行き先」を書き換える駒。</b>
    ///
    /// <para><b>横流し量</b>: <c>FunnelTaken</c>（<see cref="BattleContext.Whet"/> の窓口で
    /// 宛先を差し替えた総量）。<c>FunnelByRoute</c> は<b>どの供給経路を横取りしたか</b>で、
    /// <c>WhetByRoute</c> から引けば「素通りした量」になる（<c>DullTakenByRoute</c> と同じ形）。</para>
    ///
    /// <para><b>死蔵</b>: <c>FunnelDead</c>（回した先が<b>一度も <c>PerformAttack</c> を
    /// 通らなかった</b>ぶんの量）。<b>マイナスの本体はこの列</b>——一番遅い隣が不動のカドなら
    /// 回した全部がここへ落ちる。<b>反撃・ターン外の振りは <c>Attacks</c> を通らない</b>ので、
    /// 反応型の駒が出たら「死蔵」ではなく「振らずに干渉している」（第56期の但し書きと同じ）。</para>
    ///
    /// <para><c>FunnelFrom</c> / <c>FunnelTo</c> のキーは <b><c>Def.Id</c></b>
    /// （<c>BearFrom</c> / <c>RelayTo</c> は <c>Name</c> だが、こちらは
    /// <see cref="TallyByUnit"/> と突き合わせて死蔵を引くので同じキーで持つ）。</para>
    /// </summary>
    public required int FunnelTaken { get; init; }
    public required IReadOnlyList<int> FunnelByRoute { get; init; }
    public required int FunnelDead { get; init; }

    /// <summary>
    /// <b>死蔵の新定義</b>（第64期）。回した先が <see cref="UnitTally.AttackReads"/> <c>== 0</c>
    /// ＝ <b>その戦闘で攻撃力を出力に1度も変換しなかった</b>ぶんの量。
    ///
    /// <para><see cref="FunnelDead"/>（<c>Attacks == 0</c>）は<b>広すぎる</b>
    /// ——棘（カド）は <c>PerformAttack</c> を1度も通らないのに反撃量を自分の
    /// <c>CurrentAttack</c> で決めるので、強化は満額効く。第63期はこれで符号を逆に読んだ。</para>
    /// </summary>
    public required int FunnelDeadNew { get; init; }
    public required IReadOnlyDictionary<string, int> FunnelFrom { get; init; }
    public required IReadOnlyDictionary<string, int> FunnelTo { get; init; }

    /// <summary>
    /// 横流しの<b>弱体側</b>（V3・第63期）の計数。規則を対称にした版
    /// （「隣で起きる攻撃力の上げ下げを、全部いちばん遅い隣に押し付ける」）でだけ 0 でなくなる。
    ///
    /// <para><b><c>FunnelDullDead</c> は「捨て場として成功した量」ではない</b>（第63期に実測で否定）。
    /// 「回した先が一度も <c>PerformAttack</c> を通らなければ押し付けた弱体は盤面に出ない」は
    /// <b>反撃型の駒に対して成り立たない</b>——棘（<see cref="ThornsTrait"/>・カド）の反撃量は
    /// <b>自分の <c>CurrentAttack</c></b> で決まるので、<c>Attacks == 0</c> でも弱体は効く。
    /// 実測でも宛先がカドの席は V3 − V1 が <b>−1.5 / −2.2pt</b>、宛先が据えのバン（普通に振る駒）の席は
    /// <b>+1.3 / +1.9pt</b> と符号が逆になった。<b>この列は「振らなかった量」でしかない。</b></para>
    ///
    /// <para><c>FunnelDullByRoute</c> は <see cref="DullRoutes"/> の長さ
    /// （強化側の <c>FunnelByRoute</c> は <see cref="WhetRoutes"/> の長さ）。<b>取り違えないこと。</b></para>
    /// </summary>
    public required int FunnelDullTaken { get; init; }
    public required IReadOnlyList<int> FunnelDullByRoute { get; init; }
    public required int FunnelDullDead { get; init; }
    public required IReadOnlyDictionary<string, int> FunnelDullFrom { get; init; }
    public required IReadOnlyDictionary<string, int> FunnelDullTo { get; init; }
}

