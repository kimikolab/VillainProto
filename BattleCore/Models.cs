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
    /// 一撃を入れるとき、相手の前まで<b>踏み込む</b>か（既定 <c>true</c>）、その場から出すか。
    ///
    /// <para><b>表示専用。どの規則もこれを読まない</b>（第131期）。engine に射程という軸は
    /// 存在しない——攻撃の到達は<b>席</b>と <see cref="AttackPattern"/>（前列規則・薙ぎの列・
    /// 貫きの経路）だけで完全に決まる。だから「近接／遠距離」ではなく
    /// <b>「その場から動くか動かないか」</b>という演出の言葉そのものを札にしてある。
    /// 名前に距離・射程・間合いを入れないのは、読んだ人が判定上の意味を読み取らないため。</para>
    ///
    /// <para><b>割り当ての基準は1文</b>——「その駒は<b>行く</b>のか、<b>来られる</b>のか」。
    /// 追撃（ハギ「同じターンに続けて<b>踏み込む</b>ほど自分が傷つく」）・割り込み（ザン
    /// 「殴った者へ<b>割り込んで</b>刃を返す」）は自分が行くので <c>true</c>、
    /// 反撃（カド「<b>自分からは決して攻撃しない</b>」）・軋み（ヨミ「<b>その場で</b>割り込んで
    /// 攻撃する」）は相手が来るので <c>false</c>。<b>攻撃を一度もしない駒も <c>false</c> 側</b>で
    /// 筋が通る（ヒヨ「贔屓が手番そのもの」）ので、2値で足りる。</para>
    ///
    /// <para><b>敵にも付く</b>（<c>EnemyCatalog</c> も同じ <see cref="UnitDef"/> を使う）。
    /// これは都合ではなく必要で、<b>棘鎧のカドが1歩も動かないまま接触した絵になる</b>のは
    /// 殴りに来た敵が踏み込むときだけである。</para>
    ///
    /// <para><b>演出側の制約</b>: 踏み込みは<b>主目標への一撃だけ</b>の表現にすること。
    /// 巻き込み・隣接・伝播は<b>席</b>で決まるので、<b>席の位置で描く</b>——踏み込みは
    /// 勢いの表現であって当たり判定の表現ではない。</para>
    /// </summary>
    public bool Advances { get; init; } = true;

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

    /// <summary>
    /// 最大HPと攻撃力だけを差し替えた写し（第187期・敵の難易度のつまみ）。
    /// <b>他の全部（特性・速さ・攻撃型・行動・説明文）はそのまま写す</b>——項目を足したらここにも足すこと。
    /// </summary>
    public UnitDef WithStats(int maxHp, int attack) => new()
    {
        Id = Id, Name = Name, MaxHp = maxHp, Attack = attack, Speed = Speed, Traits = Traits,
        Pattern = Pattern, Advances = Advances, Actions = Actions,
        PlusText = PlusText, MinusText = MinusText, Flavor = Flavor,
    };

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

    /// <summary>
    /// この駒の隊の陣形（第200期）。<b>隣接・薙ぎ・貫きの経路・召喚枠は陣形ごとに違う</b>ので、
    /// engine はこの駒を通して引く（<see cref="FormationRules.AreAdjacent(UnitState, UnitState)"/> など）。
    /// 既定は X 字（<see cref="FormationShape.X"/>）で、X 字の表は第199期までと1ビットも違わない。
    /// <c>BattleEngine.Materialize</c> が編成から写し、召喚は隊の陣形を引く。
    /// </summary>
    public FormationShape Shape { get; set; } = FormationShape.X;

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
            // 第106期 (T1)。**符号を問わず流す**——4つ目の通貨（強化・弱体）の帰属を
            // ここ1箇所で取るため。上がった分だけを CarryAtkGain に載せる第68期の扱いは
            // <see cref="BattleContext.NoteAtkMove"/> の中でそのまま続いている。
            if (delta != 0) Board?.NoteAtkMove(this, delta);
        }
    }
    private int _atkBonus;

    /// <summary>
    /// <see cref="AtkBonus"/> を<b>帳簿に載せずに</b> 0 へ戻す（第68期）。
    /// 呼ぶのは寿命の2箇所だけ——<c>BattleEngine.Revive</c> と <c>Engagement.CarryOver</c>。
    /// </summary>
    /// <param name="value">
    /// 戻す先。既定の 0 が寿命の2箇所（蘇生・会戦の境界）の現行の動作。
    /// 第102期の <c>BoundaryChoice.Carry</c> だけが 0 以外を渡す——境界で
    /// 「消さなかったことにする」ために、既存の段をそのまま通した後で控えた値へ戻す
    /// （帳簿を持つ特性〈墓守〉の <c>OnCarryOver</c> と二重計上しないための順序。理由は Engagement.cs）。
    /// </param>
    internal void ResetAtkBonus(int value = 0) => _atkBonus = value;

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
    /// 最後に倒れたターン番号（第102期）。<b>純粋な記録で、誰も読んで分岐しない</b>
    /// ——盤面は1ビットも動かない（受け入れ条件: <c>compare</c> 305 セル 0 件）。
    ///
    /// <para>会戦の境界の蘇生（<see cref="BoundaryChoice.Revive"/>）が
    /// 「最後に倒れた駒」を選ぶためだけにある。<c>BattleResult.Events</c> からは取れない
    /// ——診断は verbose=false で数万戦回すのでイベントを積んでいない。</para>
    ///
    /// <para>蘇生されて再度倒れると上書きされる（後の値が勝つ）。
    /// 生きている駒の値は読まれないので、境界でも蘇生でも 0 に戻していない。</para>
    /// </summary>
    public int LastDeathTurn { get; set; }

    /// <summary>
    /// この駒に傷を書いた駒（第104期）。<b>挿入順の列で、重複は入れない。</b>
    ///
    /// <para><see cref="BattleContext.Wound"/> が<b>実際に傷を書いたときだけ</b>足す
    /// （深手への上乗せ・書けなかった呼び出しでは足さない）。
    /// <b>傷が 0 になったら記録も消える</b>——断ち（0 に戻す）／縫い（1 引く）／
    /// 継ぎ当て（1 引く）／引き取りの donor 側（1 引く）の4箇所が
    /// <see cref="BattleContext.NoteWoundDrop"/> を通す。</para>
    ///
    /// <para><b>戦闘をまたがない。</b> 傷は会戦の境界で消えるので記録も消える
    /// （<c>Engagement</c> が <see cref="StatusKeys.All"/> を消すのと同じ行）。</para>
    ///
    /// <para><b>版に依らず記録する</b>（<c>EncoreRule.Enabled</c> を見ない）——
    /// 門と紙の分子を V0 の実測から取るため（第86期の X1P・第90期の作法）。
    /// <b>純粋な記録で、誰も読んで分岐しない</b>のは規則が無効なあいだだけで、
    /// 有効なら再行動がここを読む。</para>
    /// </summary>
    public List<UnitState>? WoundWriters;

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
            // 重り（第154期・<see cref="TraitId.Laden"/>）。**抱えている本人にかかる**ので
            // `Trait.ModifyAttack`（self しか受け取らない）では書けず、盤面越しに引く
            // ——窓口は驕り（第46期）が足した `Board` で、engine に新しい窓口は1つも無い。
            // **既定では bool 1つ（`LadenActive`）を読んで抜ける。**
            // **下限は 1。`atk > 1` を見るので、攻撃力 0 の駒を 1 に上げることはない。**
            if (b is not null && b.LadenActive && atk > 1)
            {
                int pen = b.LadenPenalty(this);
                if (pen > 0) atk = Math.Max(1, atk - pen);
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
        // 第207期: 破片が減った1点（板の印を消す・瓦礫拾い）。**減った分を1回だけ**流す（Q0-5・二重に数えない）。
        else if (delta < 0 && key == StatusKeys.Armor) Board?.NoteArmorLost(this, -delta, v);
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
    internal static readonly int[][] LanePaths =
    {
        new[] { 0, 2, 5, 3 },
        new[] { 1, 2, 6, 4 }
    };

    /// <summary>
    /// 召喚枠を除いた経路。<see cref="IsLanePredecessor"/> 専用。
    /// 守備範囲が「そのとき召喚駒が湧いているか」で変わってはいけないので、
    /// 貫きの走査順とは分けてある。
    /// </summary>
    internal static readonly int[][] CorePaths =
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
    internal static readonly int[][] AdjacencyTable =
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
    internal static readonly int[][] SweepTable =
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

    /// <summary>その駒の陣形で召喚専用の枠か（第200期。engine はこちらを使う）。</summary>
    public static bool IsSummonSlot(UnitState u) => u.Shape.IsSummonSlot(u.Slot);

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

    /// <summary>
    /// 隣接（第200期・<b>駒を受け取る版</b>）。<b>engine はこちらを使う</b>——陣形は隊ごとに違うので、
    /// 席の番号だけでは隣接が決まらない。<paramref name="a"/> の陣形の表を引く（隣接を問うのは同じ隊の2体）。
    /// </summary>
    public static bool AreAdjacent(UnitState a, UnitState b) => a.Shape.AreAdjacent(a.Slot, b.Slot);

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

/// <summary>
/// 貫きの2レーンの選び方（第202期）。<b>乱数を引くのは <see cref="Random"/>（X 字）だけ</b>。
/// </summary>
public enum PierceRule
{
    /// <summary>X 字（第199期まで）: 後ろに誰かいるレーンから <c>Roll</c> で選ぶ。</summary>
    Random,
    /// <summary>第200〜201期のパターン2: 生きている駒が多い方・同数なら添字の若い方（1-2）。</summary>
    MostOccupied,
    /// <summary>
    /// 第202期: <b>撃つ敵のいるレーン側</b>の経路（格子のレーン 1 なら 1-2、3 なら 2-3）。
    /// 撃つ敵が2レーン（両方の経路に入る）なら生きている駒の多い方、同数ならその敵が貫くたびに交互（最初は添字の若い方）。
    /// 選んだ経路に生きている駒がいなければ反対側。
    /// </summary>
    Facing,
}

/// <summary>
/// 味方の陣形（第200期）。<b>9マス（3レーン × 3列）は陣形に依らず同じ</b>で、変わるのは
/// 「どの5マスに編成が立つか」と、隣接・薙ぎ・貫きの経路・召喚枠の表だけ。列（前・中・後）は
/// <see cref="FormationRules.RowOf"/> の幾何なので陣形は持たない。
///
/// <para><b>X 字（<see cref="X"/>）は <see cref="FormationRules"/> の表をそのまま引く</b>——第199期までと1ビットも違わない。
/// パターン2（<see cref="Diamond"/>・ひし形・前衛1枚）は指示書 §2.2 の一般規則から作る:</para>
/// <code>
///           後   中   前          A＝○中1(5)  B＝○後2(8)  C＝中央(2)  D＝○前2(7)  E＝○中3(6)
///     1         A              編成の枠 0〜4 が A〜E（枠0＝A … 枠4＝E）
///     2    B    C    D         召喚は四隅 後1 → 後3 → 前1 → 前3
///     3         E
/// </code>
/// <list type="number">
///   <item>隣接: 縦横斜めの8方向。同じ列でレーン1と3は、間（レーン2）の席が編成の席でなければ隣接</item>
///   <item>薙ぎ: 標的の列の全員 ＋ その1つ後ろの列の全員</item>
///   <item>貫き: 2レーン（1-2 / 2-3）を前の列から後ろへ、同じ列はレーン番号の小さい方から。
///         どちらを貫くかは<b>撃つ敵のいるレーン側</b>（第202期・<see cref="PierceRule.Facing"/>）で<b>乱数を引かない</b>。
///         第200〜201期の「生きている駒が多い方・同数なら 1-2」は <see cref="Diamond201"/>（診断の対照だけ）に残す</item>
/// </list>
/// <para><b>列（前・中・後）は陣形に依らない</b>ので「生きている駒がいる一番前の列を狙う」（<c>PoolOf</c>）はそのまま効く。</para>
/// </summary>
public sealed class FormationShape
{
    public string Name { get; }
    /// <summary>編成の枠 i（0〜4）が立つ盤の席。</summary>
    public IReadOnlyList<int> PlayableSlots { get; }
    /// <summary>召喚の走査順。</summary>
    public IReadOnlyList<int> SummonSlots { get; }
    /// <summary>貫きのレーンを乱数ではなく規則で選ぶか（パターン2・今後の新しい陣形）。X 字だけが偽。</summary>
    public bool DeterministicPierce => Pierce != PierceRule.Random;

    /// <summary>貫きの2レーンの選び方（第202期）。</summary>
    public PierceRule Pierce { get; }

    private readonly bool[] _playable;
    private readonly int[][] _adj, _sweep, _lanes, _core;
    // 第202期: 経路 l が通る格子のレーン（1〜3）。「撃つ敵のいるレーン側」を引くのに使う。
    private readonly int[][] _laneGrid;

    private FormationShape(string name, int[] playable, int[] summon, int[][] adj, int[][] sweep,
                           int[][] lanes, int[][] core, PierceRule pierce)
    {
        Name = name; PlayableSlots = playable; SummonSlots = summon;
        _adj = adj; _sweep = sweep; _lanes = lanes; _core = core;
        Pierce = pierce;
        _playable = new bool[FormationRules.TotalSlots];
        foreach (int p in playable) _playable[p] = true;
        _laneGrid = lanes.Select(path => path.Select(GridLane).Distinct().OrderBy(g => g).ToArray()).ToArray();
    }

    /// <summary>X 字（第199期までの盤面そのもの）。</summary>
    public static readonly FormationShape X = new(
        "X字", FormationRules.PlayableSlots, FormationRules.SummonSlots,
        FormationRules.AdjacencyTable, FormationRules.SweepTable,
        FormationRules.LanePaths, FormationRules.CorePaths, PierceRule.Random);

    private static readonly string[] DiamondFrames = { "中衛・上", "後衛", "中衛・中央", "前衛", "中衛・下" };

    /// <summary>パターン2（ひし形・前衛1枚）。</summary>
    public static readonly FormationShape Diamond = BuildGrid("パターン2", new[] { 5, 8, 2, 7, 6 }, new[] { 3, 4, 0, 1 }, PierceRule.Facing)
        .Named(DiamondFrames);

    /// <summary>
    /// パターン2の<b>第200〜201期の貫き</b>（生きている駒が多い方・同数なら 1-2）。<b>診断の対照だけ</b>で、
    /// 編成画面からは選べない。表は <see cref="Diamond"/> と同じで、違うのは <see cref="Pierce"/> だけ。
    /// </summary>
    public static readonly FormationShape Diamond201 = BuildGrid("パターン2（第201期の貫き）", new[] { 5, 8, 2, 7, 6 }, new[] { 3, 4, 0, 1 }, PierceRule.MostOccupied)
        .Named(DiamondFrames);

    private static readonly string[] SpearFrames = { "後衛・上", "中衛・上", "前衛", "後衛・下", "中衛・下" };

    /// <summary>
    /// パターン3（第203期・前衛1枚・中衛2・後衛2）。<b>今は敵の写しの波（<c>EnemyCatalog.Pattern3Copies</c>）だけが使う。</b>
    /// <code>
    ///           後   中   前          A＝後1(3)  B＝○中1(5)  C＝○前2(7)  D＝後3(4)  E＝○中3(6)
    ///     1    A    B              編成の枠 0〜4 が A〜E（枠0＝A … 枠4＝E）
    ///     2              C         召喚は編成に使っていない4マスを前から 前1 → 前3 → 中央 → ○後2
    ///     3    D    E
    /// </code>
    /// 表は <see cref="Diamond"/> と同じ一般規則（<c>BuildGrid</c>）で作る。貫きは第202期の <see cref="PierceRule.Facing"/>
    /// （撃つ側の席の格子のレーン）で、<b>乱数を引かない</b>。
    /// </summary>
    public static readonly FormationShape Spear = BuildGrid("パターン3", new[] { 3, 5, 7, 4, 6 }, new[] { 0, 1, 2, 8 }, PierceRule.Facing)
        .Named(SpearFrames);

    /// <summary>経路 <paramref name="lane"/> が格子のレーン <paramref name="gridLane"/>（1〜3）を通るか（第202期）。</summary>
    public bool LaneCovers(int lane, int gridLane) => Array.IndexOf(_laneGrid[lane], gridLane) >= 0;

    /// <summary>
    /// 席の表示名（第202期・<b>表示だけ</b>）。編成の席なら <see cref="FrameNames"/>、召喚枠は <see cref="FormationRules.SeatNames"/>。
    /// X 字は <see cref="FormationRules.SeatNames"/> と同じ。
    /// </summary>
    public string SeatName(int slot)
    {
        for (int i = 0; i < PlayableSlots.Count; i++)
            if (PlayableSlots[i] == slot) return FrameNames[i];
        return FormationRules.SeatNames[slot];
    }

    /// <summary>
    /// 編成の枠 i（0〜4）の表示名（第201期・編成画面と診断の表示だけ。<b>読んで分岐する規則は 0 件</b>）。
    /// X 字は <see cref="FormationRules.SeatNames"/> の先頭5つ。パターン2は位置が分かる名前（右が前・1レーンを上）。
    /// </summary>
    public IReadOnlyList<string> FrameNames { get; private set; } = FormationRules.SeatNames.Take(FormationRules.PlayableSlotCount).ToArray();

    private FormationShape Named(string[] names) { FrameNames = names; return this; }

    public int LaneCount => _lanes.Length;
    public bool IsPlayable(int slot) => _playable[slot];
    public bool IsSummonSlot(int slot) => !_playable[slot];
    public bool AreAdjacent(int a, int b) => a != b && Array.IndexOf(_adj[a], b) >= 0;
    public IReadOnlyList<int> SweepTargets(int slot) => _sweep[slot];
    public IReadOnlyList<int> LanePath(int lane) => _lanes[lane];

    public IReadOnlyList<int> LanesOf(int slot)
    {
        var lanes = new List<int>(_lanes.Length);
        for (int l = 0; l < _lanes.Length; l++)
            if (Array.IndexOf(_lanes[l], slot) >= 0) lanes.Add(l);
        return lanes;
    }

    /// <summary>「前」＝ a が b の同じレーンの1つ手前か（召喚枠を除いた経路で数える）。</summary>
    public bool IsLanePredecessor(int a, int b)
    {
        foreach (int[] path in _core)
        {
            int ia = Array.IndexOf(path, a), ib = Array.IndexOf(path, b);
            if (ia >= 0 && ib >= 0 && ib - ia == 1) return true;
        }
        return false;
    }

    /// <summary>その列のうち編成の席だけ（席番号の昇順。X 字では <see cref="FormationRules.PlayableSlotsOfRow"/> と同じ並び）。</summary>
    public IEnumerable<int> PlayableSlotsOfRow(Row row)
    {
        for (int i = 0; i < FormationRules.TotalSlots; i++)
            if (_playable[i] && FormationRules.RowOf(i) == row) yield return i;
    }

    /// <summary>9マスの格子のレーン（1〜3・上から）。</summary>
    public static int GridLane(int slot) => slot switch
    {
        0 or 3 or 5 => 1,
        1 or 4 or 6 => 3,
        _ => 2
    };

    /// <summary>§2.2 の一般規則で表を作る（パターン2・3 で共有する）。</summary>
    private static FormationShape BuildGrid(string name, int[] playable, int[] summon, PierceRule pierce)
    {
        const int n = FormationRules.TotalSlots;
        bool[] seat = new bool[n];
        foreach (int p in playable) seat[p] = true;
        static int Depth(int s) => FormationRules.DepthOf(FormationRules.RowOf(s));
        static int MiddleOf(int depth) => Enumerable.Range(0, n).First(s => GridLane(s) == 2 && Depth(s) == depth);

        var adj = new int[n][];
        var sweep = new int[n][];
        for (int a = 0; a < n; a++)
        {
            var la = new List<int>();
            var sa = new List<int>();
            for (int b = 0; b < n; b++)
            {
                if (a == b) continue;
                int dl = Math.Abs(GridLane(a) - GridLane(b)), dc = Math.Abs(Depth(a) - Depth(b));
                bool near = dl <= 1 && dc <= 1;
                bool across = dc == 0 && dl == 2 && !seat[MiddleOf(Depth(a))];
                if (near || across) la.Add(b);
                if (Depth(b) == Depth(a) || Depth(b) == Depth(a) + 1) sa.Add(b);
            }
            adj[a] = la.ToArray();
            sweep[a] = sa.ToArray();
        }

        static int[] Path(int l1, int l2) => Enumerable.Range(0, n)
            .Where(s => GridLane(s) == l1 || GridLane(s) == l2)
            .OrderBy(Depth).ThenBy(GridLane).ToArray();
        int[][] lanes = { Path(1, 2), Path(2, 3) };

        int[][] core = new[] { 1, 2, 3 }
            .Select(l => playable.Where(s => GridLane(s) == l).OrderBy(Depth).ToArray())
            .Where(p => p.Length >= 2).ToArray();

        return new FormationShape(name, playable, summon, adj, sweep, lanes, core, pierce);
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

    /// <summary>陣形（第200期）。既定は X 字。枠 i は <see cref="FormationShape.PlayableSlots"/>[i] の席に立つ。</summary>
    public FormationShape Shape { get; set; } = FormationShape.X;

    public int Count => _slots.Count(s => s is not null);

    public IEnumerable<(int Slot, UnitDef Def)> Occupied()
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] is { } d)
                yield return (i, d);
    }

    public Formation Clone()
    {
        var f = new Formation { Shape = Shape };
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

    /// <summary>
    /// パターン2（ひし形・前衛1枚）の編成（第200期）。枠0〜4 が A〜E。
    /// A＝1レーン中衛・B＝後衛・C＝中央・D＝前衛（1枚）・E＝3レーン中衛。
    /// </summary>
    public static Formation BuildDiamond(
        UnitDef? a = null, UnitDef? b = null, UnitDef? c = null, UnitDef? d = null, UnitDef? e = null)
    {
        var f = new Formation { Shape = FormationShape.Diamond };
        f[0] = a; f[1] = b; f[2] = c; f[3] = d; f[4] = e;
        return f;
    }

    /// <summary>
    /// パターン3（前衛1枚・中衛2・後衛2）の編成（第203期）。枠0〜4 が A〜E。
    /// A＝1レーン後衛・B＝1レーン中衛・C＝前衛（1枚）・D＝3レーン後衛・E＝3レーン中衛。
    /// </summary>
    public static Formation BuildSpear(
        UnitDef? a = null, UnitDef? b = null, UnitDef? c = null, UnitDef? d = null, UnitDef? e = null)
    {
        var f = new Formation { Shape = FormationShape.Spear };
        f[0] = a; f[1] = b; f[2] = c; f[3] = d; f[4] = e;
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
    /// <summary>
    /// 第103期 —— <b>この駒が餌を倒した回数</b>（撃破者の内訳）と、
    /// <b>この駒の振りの主目標が餌だった回数</b>（代金の内訳）。
    /// <b>どちらも誰も読んで分岐しない。</b>
    /// </summary>
    public int BetrayFodderKills, BetrayFodderHits;

    /// <summary>
    /// 再行動（第104期・<c>EncoreRule</c>）—— <b>この駒が刻んだ獲物が倒れて、もう一度動いた回数</b>。
    /// <c>EncoreStalls</c> はそのうち痺れ・まどろみ・<c>CanAct</c> 偽で潰れた回数。
    /// <b>どちらも誰も読んで分岐しない。</b>
    /// </summary>
    public int EncoreFires, EncoreStalls;

    /// <summary>
    /// 尾灯（第108期・<see cref="TraitId.Taillight"/>）。<b>誰も読んで分岐しない計数。</b>
    ///
    /// <para><c>TaillightFires</c> 灯した回数 ／ <c>TaillightLumen</c> 灯した総量 ／
    /// <c>TaillightSwitches</c> 対象が替わった回数 ／ <c>TaillightDoused</c> 消した総量 ／
    /// <c>TaillightIdle</c> 照らす相手がいなかった回数（空振り）。</para>
    ///
    /// <para><c>TaillightYields</c> 手番を譲った回数 ／ <c>TaillightYieldStalls</c> 譲った手番が潰れた回数 ／
    /// <c>TaillightNoDeath</c> 敵が倒れていなくて譲れなかった手番 ／
    /// <c>TaillightNoTarget</c> 灯した相手がいない（または倒れていた）手番 ／
    /// <c>TaillightBlockedHop</c> 1ホップで止めた回数（<see cref="BattleContext.Yielding"/>）。</para>
    /// </summary>
    public int TaillightFires, TaillightLumen, TaillightSwitches, TaillightDoused, TaillightIdle;
    public int TaillightYields, TaillightYieldStalls, TaillightNoDeath, TaillightNoTarget, TaillightBlockedHop;

    /// <summary>
    /// 積み過ぎ（第115期・<see cref="TraitId.Overload"/>）の門。<b>誰も読んで分岐しない計数。</b>
    ///
    /// <para><c>ReaderTurns</c> 生きていたターン数（門1 の分母）／
    /// <c>ReaderOverTurns</c> そのターン頭に閾値以上だったターン数（門1 の分子）／
    /// <c>ReaderFirstOverTurn</c> 初到達ターン（0 は未到達）／
    /// <c>ReaderSwings</c> <c>PerformAttack</c> を通った総回数 ／
    /// <c>ReaderSweeps</c> そのうち薙ぎで振った回数（門2）／
    /// <c>ReaderOverTurnsSwung</c> 閾値以上のターンのうち1度でも振ったターン数
    /// （<b>空振り = <c>ReaderOverTurns</c> − <c>ReaderOverTurnsSwung</c></b>）。</para>
    ///
    /// <para><c>ReaderBonusSum</c> / <c>ReaderBonusMax</c> は表C（<c>AtkBonus</c> の分布）の材料、
    /// <c>ReaderProbeTurns</c> は格子（<see cref="ReaderProbes"/>）ごとの到達ターン数。
    /// <b>どれも規則を無効にした版でも積む</b>——「閾値に届く供給があったか」は版に依らず読む。</para>
    /// </summary>
    public int ReaderTurns, ReaderOverTurns, ReaderFirstOverTurn;
    public int ReaderSwings, ReaderSweeps, ReaderOverTurnsSwung, ReaderLastOverSwingTurn, ReaderOverTurnMark;

    /// <summary>
    /// 振った一撃が<b>全体</b>だった回数（第128期・<see cref="ReaderSweeps"/> の上の段の版）。
    /// <b>誰も読んで分岐しない計数</b>で、盤面には一切影響しない。
    /// 段違い（<see cref="TraitId.GradeStep"/>）をドルガに載せた期に、
    /// <b>「普段は薙ぎ、強化されると全体」が実戦で成立しているか</b>を数えるために足した。
    /// </summary>
    public int ReaderAlls;
    public int ReaderBonusSum, ReaderBonusMax;

    /// <summary>
    /// 薙ぎに化けた一撃が副次目標へ届いた体数（第116期・<b>巻き込み</b>）。
    /// <b>規則が生きているときだけ積む</b>——素の型が薙ぎの駒に載せたときの分母を
    /// V0 と揃えないため（現に載せているバンは単体なので、V0 では恒等的に 0）。
    /// </summary>
    public int ReaderSplash;
    /// <summary>格子ごとの到達ターン数。<b>保持者がいる戦闘でだけ確保する</b>（`CreakProbeTurn` と同じ作法）。</summary>
    public int[]? ReaderProbeTurns;

    /// <summary>表C の格子（第115期）。<b>閾値の掃引ではない</b>——分布のどこに線があるかを見るだけ。</summary>
    public static readonly int[] ReaderProbes = { 1, 5, 10, 20, 40 };

    // =====================================================================================
    // 第117期 —— ボスの土台（<see cref="BossRule"/>）。**傾きを測るためだけの計数。**
    //
    // 勝率は「決着が早いほど高い」ので、時間に比例する項を探すのには向かない（指示書 §0-2）。
    // ここで積むのは**ターンごとの時系列**で、前半3T と後半3T を戦ごとに切り出すために要る
    // （後半は戦闘の長さに対して相対なので、集計の側では復元できない）。
    //
    // **規則が生きているときだけ確保する。** 既定（<c>BossRule.Default</c>）では
    // <c>BattleContext.BossCensus</c> が偽で、配列は1本も割り当たらない
    // ——`compare` / `layout` は数百万戦を回すので、確保だけで効く（`ReaderProbeTurns` と同じ作法）。
    // **誰も読んで分岐しない。**
    // =====================================================================================

    /// <summary>
    /// ターン頭の <see cref="UnitState.CurrentAttack"/>（添字＝ターン番号。0 は未使用）。
    /// <b><c>AtkBonus</c> の生値ではない</b>——ホタの燃焼倍率やウツの逆しまは
    /// <c>ModifyAttack</c> の側にあるので、生値で取ると育ちを取り落とす（自己検査 (d)）。
    /// </summary>
    public int[]? BossAtkByTurn;

    /// <summary>そのターンに敵へ与えたダメージ（添字＝ターン番号）。<c>DamageToEnemy</c> の内訳。</summary>
    public int[]? BossDmgByTurn;

    /// <summary>
    /// ターン頭の <see cref="UnitState.Hp"/>（添字＝ターン番号。第118期に足した）。
    /// <b>回復の前・行動順ループの前</b>に写すので、<c>Hp[T] &lt; Hp[T-1]</c> は
    /// 「T-1 ターンのあいだに受けた削りが、その間に戻した回復を上回った」を意味する
    /// ——これが交差点（指示書 §2-2 の門2）の定義になる。<b>同じ計数（<see cref="BossRule"/>）の中で写す。</b>
    /// </summary>
    public int[]? BossHpByTurn;

    /// <summary>ターン頭に生きていたターン数（空振りの分母）と、そのうち1度でも振ったターン数。</summary>
    public int BossAliveTurns, BossSwingTurns, BossLastSwingTurn;

    /// <summary>最後にターン頭で生きていたターン（＝倒れた／決着したターンの代理。生存T）。</summary>
    public int BossLastAliveTurn;

    /// <summary>
    /// 尾灯の続き（第109期に足した観測。<b>誰も読んで分岐しない計数。</b>）。
    ///
    /// <para><c>TaillightPeak</c> 1体に同時に載った灯の最大（＝到達点。指示書 Q3）。
    /// <b>灯の総量ではない</b>——対象が変わると消えるので、総量では到達点が測れない。</para>
    ///
    /// <para><c>TaillightYieldAttack</c> / <c>TaillightYieldSkill</c> / <c>TaillightYieldCharge</c>
    /// 譲った手番で相手が何をしたか（<see cref="TurnOutcome"/> の内訳。
    /// 潰れたぶんは既存の <c>TaillightYieldStalls</c>）。</para>
    ///
    /// <para><c>TaillightYieldDamage</c> <b>譲られた駒がその手番で敵へ通した量</b>（指示書 Q2 の分子）。
    /// トモ自身の1手番あたりの出力は 0（攻撃力 0・<c>Actions = [Skill]</c>）なので、
    /// この値が正であることが Q2 の成立そのものになる。</para>
    ///
    /// <para><c>TaillightLitReceived</c> / <c>TaillightLitLumen</c>
    /// <b>受け手の側の帳簿</b>——灯を受けた回数と量。上の5本はすべて<b>灯した本人（トモ）</b>が持つので、
    /// 「誰が照らされたか」の内訳はこちらでしか引けない。</para>
    /// </summary>
    public int TaillightPeak, TaillightYieldAttack, TaillightYieldSkill, TaillightYieldCharge;
    public int TaillightYieldDamage, TaillightLitReceived, TaillightLitLumen;

    /// <summary>
    /// 尾灯の譲渡条件の版（第110期・<see cref="TaillightRule"/>）。<b>誰も読んで分岐しない計数。</b>
    ///
    /// <para><b>V2（即時）の段の表</b>（指示書 §1-3。どこで鎖が切れたかを段で数える）——
    /// <c>TaillightSawDeath</c> 敵の死の通知が生きている自分に届いた回数 ／
    /// <c>TaillightDeadTarget</c> そのうち灯した相手がいない・倒れていた回数 ／
    /// <c>TaillightNoOutOfTurn</c> <see cref="BattleContext.CanActOutOfTurn"/> で止まった回数
    /// （内訳 <c>TaillightOutHush</c> 粛 ／ <c>TaillightOutStun</c> 痺れ ／ <c>TaillightOutReact</c> <c>CanReact</c> 偽）／
    /// <c>TaillightRepeat</c> このターン既に譲っていた回数（1ターン1回の上限）／
    /// <c>TaillightNoFoe</c> 敵が全滅していて譲らなかった回数。</para>
    ///
    /// <para><c>TaillightPair</c> <b>1つの撃破で追い打ちと譲渡が両方立った回数</b>（指示書 Q3）。
    /// 「その死亡通知の連鎖の中で、譲渡より前に味方の振りが走っていた」を数える
    /// ——V0 / V1 は譲渡が <c>OnAction</c>（自分の手番）にあるので<b>構造的に 0</b>。</para>
    ///
    /// <para><c>TaillightStallStun</c> / <c>TaillightStallSlumber</c> / <c>TaillightStallCanAct</c>
    /// 譲った手番が潰れた理由の内訳（譲られた駒の第105期の計数の差分で取る。
    /// 合計は <c>TaillightYieldStalls</c>）。</para>
    ///
    /// <para><c>TaillightSaw2</c> V1（窓）で「前のターンの撃破」を読んで成立した回数
    /// ——<c>TaillightYields</c> のうち V0 では立たなかったぶん。</para>
    /// </summary>
    public int TaillightSawDeath, TaillightDeadTarget, TaillightNoOutOfTurn;
    public int TaillightOutHush, TaillightOutStun, TaillightOutReact;
    public int TaillightRepeat, TaillightNoFoe, TaillightPair;
    public int TaillightStallStun, TaillightStallSlumber, TaillightStallCanAct, TaillightSaw2;

    /// <summary>
    /// 灯の濾し（第113期・<see cref="LitFilter"/>）に掛かった回数。<b>誰も読んで分岐しない計数で、
    /// 積むのは「飛ばされた駒」の側の帳簿</b>——自己検査 (b)（濾した集合が Phase 0 の一覧と一致するか）は
    /// ここを数えて突き合わせる。
    ///
    /// <para><c>TaillightSkipStatic</c> W1 の静的な濾し（(A) 恒久的に振らない型 ／ (B) 周期に攻撃が無い）で
    /// 飛ばされた回数 ／ <c>TaillightSkipNow</c> W2 の動的な濾し（そのターン <c>CanAct</c> が偽）で
    /// 飛ばされた回数。<b>第114期に既定が <see cref="LitFilter.ActingNow"/> になったので両方とも走る</b>
    /// （第108〜113期の既定 <see cref="LitFilter.SupportOnly"/> では構造的に 0 だった）。</para>
    /// </summary>
    public int TaillightSkipStatic, TaillightSkipNow;

    public int Attacks;

    /// <summary>
    /// <b>手番の中で 2 発目以降に振った回数（第178期・計数専用）。</b>
    /// <see cref="Attacks"/> の内数で、<b>誰も読んで分岐しない。</b>
    ///
    /// <para>数えるのは <c>SwingTurn</c> の1箇所だけ ＝ <c>Trait.ModifyHitCount</c> が
    /// 1 より大きい値を返した手番のぶん。<b>反撃・割り込み・追い打ち・再行動は入らない</b>
    /// ——あれらは <c>PerformAttack</c> を直接呼ぶので <see cref="Attacks"/> だけが増える。</para>
    /// </summary>
    public int ExtraSwings;

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
    /// <b>割り込んで主目標を引き受けた回数</b>（第125期・<c>SelectTargetChain</c> の全段）。
    /// 庇う・後備え・棘守り・殉教、および標（<c>StatusKeys.Marked</c>）が引いたぶん。
    ///
    /// <para>第124期に戦績パネルの「庇い」列を <c>—</c> で出したのはこの計数が1本も無かったため
    /// （Q0-3）。<b><see cref="DamageTaken"/> が増えるだけでは「庇ったから増えた」と
    /// 「殴られたから増えた」が数字から分けられない。</b></para>
    ///
    /// <para><b>差し替えが実際に起きた段だけ数える</b>——標の段の
    /// <c>marked == target</c>（もともと主目標だった）は鎖の計数を動かさない既存の作法に揃える。
    /// <b>誰も読んで分岐しない。</b></para>
    /// </summary>
    public int Intercepts;

    /// <summary>
    /// <b>段別の介入回数</b>（第135期・添字は <see cref="InterceptLabels.All"/> の並び）。
    /// <see cref="Intercepts"/> の内訳で、<b>合計は必ず一致する</b>（自己検査）。
    ///
    /// <para>第125期に足した <see cref="Intercepts"/> は5段の合計しか持たないので、
    /// <b>「ガルドが庇った回数」だけを引けなかった</b>——指示書 Q0-3 の分子はここ。
    /// <b>既定では確保しない</b>（<c>HarmRule.Census</c> が偽なら1本も割り当たらない）。</para>
    /// </summary>
    public int[]? InterceptsByLabel;

    /// <summary>
    /// <b>庇いの機会</b>（第135期・<c>HarmRule.Census</c> のときだけ）。
    ///
    /// <para><c>GuardChances</c> 鎖が庇いの段まで来て、資格のある庇い手として数えられた回数
    /// （＝<b>50% の判定を振られた回数</b>。成立したぶんは <see cref="InterceptsByLabel"/> の側）／
    /// <c>GuardRangeMissed</c> <b>範囲攻撃だったので庇いの段に到達すらしなかった回数</b>
    /// ——<b>これが「庇えなかった範囲攻撃の数」</b>で、受け流しの機会数の上限を決める。</para>
    ///
    /// <para><b>誰も読んで分岐しない。</b></para>
    /// </summary>
    public int GuardChances, GuardRangeMissed;

    /// <summary>
    /// <b>肩代わりで受けた傷が攻撃力に化けた量と回数</b>（第135期・<b>版に依らず数える</b>）。
    /// <see cref="TraitId.Guardian"/> / <see cref="TraitId.Martyr"/> が共有する
    /// <c>RedirectGainTrait.OnDamaged</c> の <c>self.AtkBonus += gain</c> の1箇所きり。
    ///
    /// <para><b>ガルドの一文「その傷のぶん強くなる」が死文かどうかを決める唯一の計数。</b>
    /// 傷の引き取り（<c>GatherRule</c>・<see cref="GatherTaken"/>）とは<b>別の機構</b>で、
    /// あちらは <see cref="StatusKeys.Wound"/> を移すだけで <c>AtkBonus</c> を1も動かさない。</para>
    /// </summary>
    public int RedirectGainFires, RedirectGain;

    /// <summary>
    /// <b>支援拒否（<see cref="TraitId.Stoic"/>）が弾いた回復</b>（第135期・<c>HarmRule.Census</c> のときだけ）。
    /// <c>BattleContext.Heal</c> の <c>!target.AcceptsSupport</c> の早期リターンで数える。
    /// <b>1体を選ぶ回復がガルドに届かなかった量と回数</b>そのもの。
    /// </summary>
    public int StoicHealBlocked, StoicHealBlockedFires;

    /// <summary>
    /// <b>支援拒否が隣へ流した回数と宛先の延べ数</b>（第135期・<c>HarmRule.Census</c> のときだけ）。
    /// <c>BattleContext.SupportTargets</c> の <c>Stoic</c> の枝で数える。
    /// <b>量は持たない</b>——あの窓口は「誰に配るか」しか知らない。
    /// 量は素体対照（<c>Stoic</c> を外した版との差）で取る。
    /// </summary>
    public int StoicSupportHops, StoicSupportHeads;

    /// <summary>
    /// <b>経路別に受けたダメージの量と回数</b>（第135期・添字は <see cref="DamageRoute"/>）。
    /// <c>HarmRule.Census</c> のときだけ確保する。<b>誰も読んで分岐しない。</b>
    ///
    /// <para><c>HarmGuardAmount</c> / <c>HarmGuardHits</c> は<b>そのうち介入で引き受けたぶん</b>
    /// （庇う・後備え・殉教・棘守り・標のどれかが主目標を差し替えた一撃）。
    /// 差が「素で狙われたぶん」になる。</para>
    ///
    /// <para><c>HarmFatal</c> は<b>倒れた一撃だけ</b>を経路別に数える（1回の死につき1件）。
    /// <b>総量の内訳と一致しない</b>のがこの帳簿の要点で、
    /// 「たくさん殴られている」と「何で死んだか」は別の量である（第134期）。</para>
    /// </summary>
    public int[]? HarmAmount, HarmHits, HarmGuardAmount, HarmGuardHits, HarmFatal;

    /// <summary>
    /// <b>受け流し</b>（第135期・<see cref="TraitId.Parry"/>）。<b>誰も読んで分岐しない計数。</b>
    /// <c>ParryFires</c> 弾いた回数 ／ <c>ParryBlocked</c> 弾いた総量 ／
    /// <c>ParryBlockedMax</c> 1回で弾いた最大 ／ <c>ParryByRoute</c> 経路別の回数。
    /// <b>既定（<c>ParryRule.Uses</c> = 0）では1度も動かない。</b>
    /// </summary>
    public int ParryFires, ParryBlocked, ParryBlockedMax;
    /// <inheritdoc cref="ParryFires"/>
    public int[]? ParryByRoute;
    /// <summary>
    /// 受け流しの在庫の帳簿（第136期 段2・<b>計数専用</b>）。
    /// <c>ParryStances</c> 構えで補充したターン数 ／ <c>ParryRefillTurn</c> 構えで戻した総数 ／
    /// <c>ParryRefillGuard</c> 庇って身に受けて戻した回数 ／ <c>ParryRefillGuardWasted</c> 満タンで戻せなかった回数 ／
    /// <c>ParryStockAtDeath</c> 倒れた瞬間の残り在庫の総和。
    /// </summary>
    public int ParryStances, ParryRefillTurn, ParryRefillGuard, ParryRefillGuardWasted, ParryStockAtDeath;

    /// <summary>
    /// <b>構えを解いて振った手番</b>（第152期 段B・<see cref="ParrySwing"/>）。<b>誰も読んで分岐しない計数。</b>
    /// <c>ParrySwings</c> 振った手番の数 ／ <c>ParrySwingsFull</c> うち在庫が満タンだった数。
    /// <b>既定（<see cref="ParrySwing.Off"/>）では1度も動かない。</b>
    /// <b>2つは同じ瞬間に数える</b>（行動順ループの中・第115期「同じ比を作る2つの計数は同じ瞬間に取る」）。
    /// </summary>
    public int ParrySwings, ParrySwingsFull;

    /// <summary>
    /// <b>狙撃（<see cref="TraitId.Sniper"/>）が成立したまま振った回数</b>（第129期・<b>計数専用</b>）。
    /// 成立の条件（<c>HasFallenBack</c> かつ <c>Row.Back</c>）は <c>PerformAttack</c> が
    /// その場で評価して打点と攻撃型を書き換えるだけなので、<b>盤面にも計数にも痕跡が残らない</b>
    /// ——見せ場（<c>HighlightOnce</c>）は <c>verbose</c> が偽だと1件も出ないうえ1戦に1度しか打たない。
    /// <b>逃亡兵セロの「起動したか」を数えられる唯一の計数</b>で、誰も読んで分岐しない。
    /// </summary>
    public int SniperSwings;

    /// <summary>
    /// <b>火勢（<see cref="TraitId.Wildfire"/>）の帳簿</b>（第133期・<b>計数専用</b>）。
    ///
    /// <para><b><c>ModifyAttack</c> の中では数えられない</b>——<c>CurrentAttack</c> は
    /// 駆り立ての選択・転嫁の流し先・<c>StatSnapshot</c>・棘/仇討ち/責め苦の反撃量からも
    /// 読まれるので、あそこで数えると「振った回数」ではなく<b>「読まれた回数」</b>になる
    /// （<c>OverbearTrait</c> の明文）。<c>PerformAttack</c> が打点を作った直後に
    /// <b>計数専用の1行</b>で書く。<b>誰も読んで分岐しない。</b></para>
    ///
    /// <para><c>WildfireSwings</c> ＝ 保持者が振った回数（手番も割り込みも） ／
    /// <c>WildfireLit</c> ＝ そのうち燃えている敵が1体以上いた回数（＝発火率の分子） ／
    /// <c>WildfireFoes</c>・<c>WildfireFoesSq</c>・<c>WildfireFoesMax</c> ＝
    /// 振った時点の燃えている敵の数の 和・二乗和・最大（<b>分散が P6</b>） ／
    /// <c>WildfireGain</c>・<c>WildfireGainSq</c> ＝ 実際に上乗せされた打点の 和・二乗和。</para>
    /// </summary>
    public int WildfireSwings, WildfireLit, WildfireFoesMax;
    public long WildfireFoes, WildfireFoesSq, WildfireGain, WildfireGainSq;

    /// <summary>
    /// <b>肩代わりの中継で引き受けたダメージの合計</b>（第125期・<c>relayed</c> の札が立った段）。
    /// 巨躯（ゴルム）と分かち（ドハ）の2経路。
    ///
    /// <para><see cref="Swallowed"/> は巨躯が<b>飲み込んだ名目量</b>（吐き戻しと同じ場所・同じ量）で、
    /// こちらは<b>中継された段が実際に HP を削った量</b>——破片・据え・軛を通した後の値なので
    /// 別物である。<b>誰も読んで分岐しない。</b></para>
    /// </summary>
    public int Shouldered;

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

    /// <summary>
    /// 縫いの発火口（第107期 (S3)）。<c>SutureCalls</c> は<b>フックに入った回数</b>
    /// （発火 ＝ <c>SutureFoe + SutureAlly</c>。差が空振り）、
    /// <c>SutureCapped</c> は 1ターン1回の上限で弾かれた回数（<see cref="SutureFire.OnWound"/> のときだけ立つ）。
    /// <b>盤面には一切影響しない。</b>
    /// </summary>
    public int SutureCalls, SutureCapped;

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

    // ------------------------------------------------------------------------------------
    // 第105期（手番の値段）。**どれも誰も読んで分岐しない。盤面には一切影響しない**
    // （Whet* / Burn* / Carry* と同じ扱いで verbose 非依存）。診断 `tempo` だけが読む。
    //
    // **「手番の中」の定義は <see cref="BattleContext.InOwnTurn"/> の1箇所だけ**
    // ——`TakeTurn` の枠の中で、かつ<b>その駒自身が出どころ</b>で、かつ反撃・割り込みの
    // 中でないこと。棘の反撃は殴った側の `TakeTurn` の中で走るが出どころが違うので外、
    // `OnTurnStart` は行動順ループの外側なので外になる。
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// 回ってきた手番の数（<see cref="BattleContext.TakeTurn"/> を通った回数）。
    /// <b>再行動（第104期・<c>EncoreRule</c>）を含む</b>——渡されているのは手番まるごとなので。
    /// </summary>
    public int TurnsTaken;

    /// <summary>手番の内訳（<see cref="TurnOutcome"/> の4つ）。合計は <see cref="TurnsTaken"/>。</summary>
    public int TurnAttacks, TurnSkills, TurnCharges, TurnStalls;

    /// <summary>
    /// 潰れた内訳。<c>StallStun</c> 痺れ／<c>StallSlumber</c> まどろみ／
    /// <c>StallImmobile</c> 不動（<see cref="TraitId.Immobile"/> が拒んだ）／
    /// <c>StallCanAct</c> それ以外の <c>CanAct</c> 偽。合計は <see cref="TurnStalls"/>。
    /// </summary>
    public int StallStun, StallSlumber, StallImmobile, StallCanAct;

    /// <summary>転倒（第143期・<see cref="StatusKeys.Stagger"/>）で潰れた手番。<b>計数のみ。</b></summary>
    public int StallStagger;

    /// <summary>
    /// <b>売れた手番</b>——潰れた手番のうち <see cref="Trait.SurrenderedTurn"/> が真だった回数。
    /// 第103期の訂正どおり、生の <c>IdleTurn</c> ではなく<b>買い手が通す判定のほう</b>を数える
    /// （engine は <c>CanAct</c> が偽の駒にも <c>IdleTurn</c> を無条件に立てる）。
    /// </summary>
    public int TurnsSurrendered;

    // ------------------------------------------------------------------------------------
    // 第106期 —— 保留の3枚のノブの計数。**どれも誰も読んで分岐しない。盤面には一切影響しない。**
    // ------------------------------------------------------------------------------------

    /// <summary>憤怒（<see cref="TraitId.Rage"/>）が発火した回数。<b>版に依らない</b>
    /// ——数える場所は式の分岐より手前で、発火する集合は <c>Amount</c> / <c>Count</c> で同一。</summary>
    public int RageCountFires;

    /// <summary>憤怒が積んだ攻撃力の総量（自己検査 (b)。<c>÷ RageCountFires</c> が1発あたりの上昇）。</summary>
    public int RageGain;

    /// <summary>
    /// 戦闘中の <see cref="UnitState.AtkBonus"/> の<b>到達点</b>と、そこへ最初に届いたターン。
    /// <c>AtkProbeTurn[i]</c> は <see cref="AtkProbes"/>[i] に最初に届いたターン（0 ＝ 未到達）。
    /// <b>観測専用</b>（<see cref="CreakMaxBonus"/> は軋みの窓口だけを見るので別物）。
    /// </summary>
    public int AtkPeak, AtkPeakTurn;
    public int[]? AtkProbeTurn;

    /// <summary>到達ターンを測る格子（第106期 Q1。<b>ムドの到達点 21 前後を挟む</b>）。</summary>
    public static readonly int[] AtkProbes = { 6, 12, 18, 24 };

    /// <summary>繕い（<see cref="TraitId.Mender"/>）が<b>実際に自分から引いた</b> HP の総量
    /// （<see cref="MendPaid"/> は<b>癒した量</b>で、等価交換をやめると別の数になる）。</summary>
    public int MendPaidRaw;

    /// <summary>散開（<see cref="TraitId.Loose"/>）の弾き。<c>LooseShoves</c> 実際に弾いた回数 ／
    /// <c>LooseCapped</c> 1ターン1回の上限で弾かれた回数 ／ <c>LooseNoTarget</c> 隣に味方がいなかった回数。</summary>
    public int LooseShoves, LooseCapped, LooseNoTarget;

    /// <summary>
    /// 身構え（<see cref="TraitId.Brace"/>・第143期）の帳簿。<b>計数専用で、どの規則も読まない。</b>
    ///
    /// <para><c>BraceGuards</c> 身を固めた手番数 ／ <c>BraceCuts</c> 上限で切った回数 ／
    /// <c>BraceRefused</c> 切り落とした総量 ／ <c>BraceGiven</c> 味方の破片にした量 ／
    /// <c>BraceLost</c> 宛先が無いまま消えた量 ／ <c>BraceShoves</c>・<c>BraceShoveCapped</c>・
    /// <c>BraceNoTarget</c> 弾きの3分類 ／ <c>BraceStaggers</c> 転ばせた回数 ／
    /// <c>BraceArmorMuted</c> <b>破片が一撃を全部吸って <c>OnDamaged</c> が鳴らなかった回数</b>
    /// （第143期 Q0-1 の穴。<b>この期では塞がず、発生だけ数える</b>）。</para>
    ///
    /// <para><b><c>BraceGiven ≦ BraceRefused</c> が受け入れ条件</b>（配った量が切り落とした量を超えない）。
    /// 差は <c>BraceLost</c> と、戦闘終了時にまだ配られていない保留。</para>
    /// </summary>
    /// <summary>
    /// 預かり（<see cref="TraitId.Ward"/>・第153期）の帳簿。<b>計数専用で、どの規則も読まない。</b>
    /// <b>保持者（ノチ）の側</b>に載せる——<c>WardStacked</c> 積んだ量 ／
    /// <c>WardReleased</c> 実際に返った量 ／ <c>WardForfeited</c> 没収された量。
    /// </summary>
    public long WardStacked, WardReleased, WardForfeited;

    /// <summary>
    /// 第154期の代金（<b>こちらは<u>受け手</u>の側に載せる</b>——効くのは預かりを抱えている味方本人で、
    /// 保持者ではない）。<c>WardBurdenTaken</c> 荷で増えた被ダメージ ／
    /// <c>WardLadenLost</c> 重りで振られなかった打点（名目）。<b>計数専用。</b>
    /// </summary>
    public long WardBurdenTaken, WardLadenLost;

    /// <summary>
    /// 贖い（第155期・<see cref="TraitId.Indulgence"/>）。<b>保持者（アガ）の側</b>に載せる——
    /// <c>IndulgenceStacked</c> 前借りで積んだ負債 ／ <c>TollTaken</c> 取り立てで実際に削った HP ／
    /// <c>BrandFires</c> 焼いた回数 ／ <c>BrandDealt</c> 焼きが実際に削った HP。<b>計数専用。</b>
    /// </summary>
    public long IndulgenceStacked, TollTaken, BrandFires, BrandDealt;

    /// <summary>
    /// 灰（第179期・<see cref="TraitId.Ash"/>）。<b>保持者（スス）の側</b>に載せる——
    /// <c>AshGained</c> 溜めた総量（実額）／ <c>AshFires</c> 撒いた回数 ／
    /// <c>AshSpent</c> 撒いた灰の総量 ／ <c>AshPeak</c> 1回の最大 ／
    /// <c>AshDry</c> 灰が無くて素振りした回数 ／ <c>AshAtDeath</c> 倒れた時点で抱えていた灰。
    /// <b>計数専用。</b>
    /// </summary>
    public long AshGained, AshFires, AshSpent, AshPeak, AshDry, AshAtDeath, AshResidual,
                AshHolds, AshFalloutOut;

    /// <summary>
    /// 起爆（第188期・<see cref="TraitId.Catalyst"/>）。<b>保持者（カタ）の側</b>に載せる——
    /// <c>DetonateFires</c> 起爆した回数 ／ <c>DetonateDry</c> 毒も火も無くて空振りした回数 ／
    /// <c>DetonateDualTargets</c> 両方持ちで倍になった敵の数（延べ）／
    /// <c>DetonatePoisonNominal</c>・<c>DetonateBurnNominal</c> 敵への名目量（倍を含む）／
    /// <c>DetonateDualExtra</c> そのうち倍で上乗せした分 ／ <c>DetonateFoeDealt</c> 敵から実際に削った HP ／
    /// <c>DetonateAllyNominal</c>・<c>DetonateAllyDealt</c> 味方への名目量と実額 ／
    /// <c>DetonateFoeKills</c>・<c>DetonateAllyKills</c> 起爆の段で倒れた数 ／
    /// <c>DetonateYokeCut</c> 軛が効いている間に上限を超えた毒の段の数。<b>計数専用。</b>
    /// </summary>
    public long DetonateFires, DetonateDry, DetonateDualTargets, DetonatePoisonNominal, DetonateBurnNominal,
                DetonateDualExtra, DetonateFoeDealt, DetonateAllyNominal, DetonateAllyDealt,
                DetonateFoeKills, DetonateAllyKills, DetonateYokeCut;

    /// <summary>
    /// デバッファー3枚（第189期）の帳簿。<b>計数専用</b>（どの規則も読まない）。
    /// <para>ネル（<see cref="TraitId.Hexer"/>）の側: <c>HexerActs</c> 手番の回数 ／ <c>HexerMarks</c> 呪った回数 ／
    /// <c>HexerCursedSum</c>・<c>HexerCursedMax</c> 手番ごとの呪い持ちの数の合計と最大 ／ <c>HexerDullTotal</c> 呪い持ちへの攻撃ダウンの名目の総量 ／
    /// <c>HexLeakTotal</c> 隣の味方への漏れの名目の総量。</para>
    /// <para>クビ（<see cref="TraitId.Daunt"/>）の側: <c>DauntActs</c> 手番の回数 ／ <c>DauntFoes</c> 萎縮させた敵の延べ数 ／
    /// <c>DauntFoesAlready</c> 既に萎縮していた敵の延べ数 ／ <c>DauntAllies</c> 萎縮させた隣の味方の延べ数。
    /// <b>振った側</b>（萎縮を消費した駒）: <c>DauntedSwings</c> 半分になった一撃の数 ／ <c>DauntedCut</c> 削った打点の合計。</para>
    /// <para>ハネ（<see cref="TraitId.Rebound"/>）の側: <c>ReboundThrusts</c> 突き返した回数 ／ <c>ReboundStaggers</c> 転ばせた回数 ／
    /// <c>ReboundNoFront</c> 前列に敵がいなかった手番 ／ <c>ReboundRefused</c> 入れ替えが空振りした回数 ／
    /// <c>OverrunSwaps</c>・<c>OverrunRefused</c>・<c>OverrunNoAlly</c> 勢い余っての入れ替え・空振り（据えた足）・隣に味方なし ／
    /// <c>OverrunReaderDisplaced</c>・<c>OverrunReaderDrifter</c>・<c>OverrunReaderSniper</c> その入れ替えの通知が届いた読み手
    /// （ヨミが動かされた・シオに味方の移動が届いた・セロが後ろへ下がった）の延べ数。</para>
    /// </summary>
    public long HexerActs, HexerMarks, HexerCursedSum, HexerCursedMax, HexerDullTotal, HexLeakTotal,
                DauntActs, DauntFoes, DauntFoesAlready, DauntAllies, DauntedSwings, DauntedCut,
                ReboundThrusts, ReboundStaggers, ReboundNoFront, ReboundRefused,
                OverrunSwaps, OverrunRefused, OverrunNoAlly, OverrunReaderDisplaced, OverrunReaderDrifter, OverrunReaderSniper;

    /// <summary>
    /// 反転の結界（第190期・毒喰らいのベニ）の帳簿。<b>計数専用</b>（どの規則も読まない）。
    /// <para><b>ベニの側</b>: <c>InversePoisonHealed</c>・<c>InverseBurnHealed</c>・<c>InverseDetonateHealed</c> 反転で実際に癒えた量
    /// （刻みの毒・刻みの燃焼・起爆の味方側）／ <c>InverseNominal</c> 反転した削りの名目の総量 ／
    /// <c>InverseRecipientTurns</c> 反転で癒えた味方の延べ人数（1ターンに同じ駒は1回）／ <c>InverseMaxSimul</c> 1ターンに癒えた味方の最大人数 ／
    /// <c>TaintActs</c>・<c>TaintDry</c>・<c>TaintLayers</c> 澱み分けの手番・空振り・積んだ層 ／
    /// <c>InverseLeakFires</c>・<c>InverseLeakNominal</c>・<c>InverseLeakDealt</c>・<c>InverseLeakKills</c> 反転の裏の回数・名目・実際に削った HP・倒した数 ／
    /// <c>TaintPostBite</c> 澱み分けの層を持ったまま隣から離れた味方が、刻みで削られた量。</para>
    /// <para><b>回復させた駒の側</b>: <c>InverseLeakBy</c> 自分の回復が裏でダメージになった実額。
    /// <b>ヴィオの側</b>: <c>VioAteTaint</c> 吸い上げた層のうちベニの澱み分けの分。</para>
    /// </summary>
    public long InversePoisonHealed, InverseBurnHealed, InverseDetonateHealed, InverseNominal,
                InverseRecipientTurns, InverseMaxSimul, TaintActs, TaintDry, TaintLayers,
                InverseLeakFires, InverseLeakNominal, InverseLeakDealt, InverseLeakKills, TaintPostBite,
                InverseLeakBy, VioAteTaint;

    /// <summary>
    /// 第191期（ベニの手番の周期）の帳簿。<b>計数専用。</b>
    /// <c>KindleActs</c>・<c>KindleDry</c>・<c>KindleTargets</c> 火を分けた手番・隣がいなかった手番・火を点けた延べ人数 ／
    /// <c>InversePoisonEarly</c>・<c>InverseBurnEarly</c>・<c>InverseDetonateEarly</c> 反転で癒えた量のうち 1〜3 ターン目の分 ／
    /// <c>InverseBurnNominal</c>・<c>InverseBurnNominalEarly</c> 反転した燃焼の刻みの名目（全体・1〜3 ターン目）／
    /// <c>KindlePostBurn</c> ベニが点けた火を持ったまま隣から外れた味方が、燃焼の刻みで受けた名目（保持者の側）。
    /// 第192期: <c>InverseSelfPoison</c>・<c>InverseSelfBurn</c>・<c>InverseSelfDetonate</c> 保持者自身が反転で癒えた量 ／
    /// <c>InverseLeakSelf</c> 保持者自身が反転の裏で受けたダメージ（保持者の側）／ <c>InverseLeakOnHolder</c> 同じ量の出どころの側。
    /// 第193期（啜り）: <c>SipEligible</c> 隣で満タンに溢れた反転の回復（渇き・支援拒否を除く）／ <c>SipRoom</c> そのときベニが受け取れた上限（溢れとベニの失った HP の小さいほう）／
    /// <c>SipGained</c>・<c>SipEarly</c> 実際にベニに流れ込んだ量（全体・1〜3 ターン目）。
    /// </summary>
    public long KindleActs, KindleDry, KindleTargets, InversePoisonEarly, InverseBurnEarly, InverseDetonateEarly, InverseBurnNominal,
                KindlePostBurn, InverseBurnNominalEarly,
                InverseSelfPoison, InverseSelfBurn, InverseSelfDetonate, InverseLeakSelf, InverseLeakOnHolder,
                SipEligible, SipRoom, SipGained, SipEarly;

    /// <summary>
    /// 第197期 Phase 0（<b>計数専用・どの規則も読まない</b>）: <c>SipWaste</c> 啜りでベニへ流したのに、ベニが満タンで入りきらなかった量
    /// （ベニへの流し込みが渇き・支援拒否で止まった回は数えない）／ <c>SipWasteByTurn</c> 同じ量のターン別（添字 ＝ ターン番号・0..30）。
    /// </summary>
    public long SipWaste;
    public long[]? SipWasteByTurn;

    /// <summary>
    /// 第197期（紅蓮・<b>ベニの側・計数専用</b>）: <c>GurenGained</c> 溜めた量 ／ <c>GurenFires</c> 放った回数 ／ <c>GurenSpent</c> 放った紅蓮の総量 ／
    /// <c>GurenFirstTurn</c> 初めて放ったターン（0 ＝ 放っていない）／ <c>GurenLitNew</c>・<c>GurenRelit</c> 奔流で新しく火が点いた敵・既に燃えていた敵（延べ）／
    /// <c>GurenLayers</c> 奔流で積んだ毒の層（滲み則込み）／ <c>GurenPoisonTick</c>・<c>GurenPoisonTickMark</c> その層の刻みの額面（1回目・印の2回目以降）／
    /// <c>GurenMioLayers</c> 奔流の毒しか無い敵にミオが足した +4 ／ <c>GurenMioTick</c>・<c>GurenMioTickMark</c> その層の刻み ／
    /// <c>GurenBurnTickNew</c>・<c>GurenBurnTickRelit</c>・<c>GurenBurnTickMark</c> 奔流が点けた・煽った火の刻み（1回目）と印の2回目以降 ／
    /// <c>GurenStrikeNominal</c>・<c>GurenStrikeDealt</c> 直撃の版の名目と実額。
    /// </summary>
    public long GurenGained, GurenFires, GurenSpent, GurenLitNew, GurenRelit, GurenLayers, GurenPoisonTick, GurenPoisonTickMark,
                GurenMioLayers, GurenMioTick, GurenMioTickMark, GurenBurnTickNew, GurenBurnTickRelit, GurenBurnTickMark,
                GurenStrikeNominal, GurenStrikeDealt;
    public int GurenFirstTurn;

    /// <summary>
    /// 第204期（施しのリリ・<b>リリの側・計数専用・どの規則も読まない</b>）:
    /// <c>KissFires</c> 1体ずつ吸った回数 ／ <c>KissNominal</c> 吸う名目（最大HPの割合）／ <c>KissDrained</c> 実際に敵の HP から減った量 ／
    /// <c>KissKills</c> 吸い取りで倒した数 ／ <c>KissHealed</c> 与えて実際に癒えた量 ／ <c>KissArmor</c> 溢れて破片になった量 ／
    /// <c>KissBlocked</c> 渇き・支援拒否・反転で止められた量（溢れにしない）／ <c>KissToSelf</c> 受け取り手がリリ自身だった回数 ／
    /// <c>KissMoves</c>・<c>KissMovedLayers</c> 移した状態の件数と値の合計 ／ <c>KissReset</c> 吸うだけの版で聖痕を消して一巡目に戻った回数 ／
    /// <c>RiteFires</c>・<c>RiteDrained</c>・<c>RiteHealed</c>・<c>RiteArmor</c>・<c>RiteBlocked</c>・<c>RiteTurnSum</c> 祝福の儀 ／ <c>RiteFirstTurn</c> 初回のターン（0 ＝ 無し）／
    /// <c>ReturnFires</c>・<c>ReturnNominal</c>・<c>ReturnHealed</c>・<c>ReturnArmor</c> 祝福が還る ／
    /// <c>KissMovedBy</c> 移した状態（キー ＋ "|" ＋ 受け取った駒の <c>Def.Id</c>）→ (件数, 値の合計)。
    /// </summary>
    public long KissFires, KissNominal, KissDrained, KissKills, KissHealed, KissArmor, KissBlocked, KissToSelf, KissMoves, KissMovedLayers, KissReset,
                RiteFires, RiteDrained, RiteHealed, RiteArmor, RiteBlocked, RiteTurnSum, ReturnFires, ReturnNominal, ReturnHealed, ReturnArmor;
    public int RiteFirstTurn;
    public Dictionary<string, (long N, long Sum)>? KissMovedBy;

    /// <summary>
    /// 第204期 追補（<b>計数専用</b>）: リリが自分で受け取った回の理由。添字 ＝ 場面 × 3 ＋ 理由
    /// （場面 0 吸い取り ／ 1 儀式の余り ／ 2 祝福が還る、理由 0 ほかが全員満タン ／ 1 傷ついているのが支援拒否の駒だけ ／ 2 ほかの味方が全滅）。
    /// <c>KissSelfFull</c> はそのうちリリ自身も満タンだった回（場面ごと・与えた全額が破片になる）。
    /// </summary>
    public long[]? KissSelfWhy, KissSelfFull;

    /// <summary>
    /// 第205期（<b>計数専用</b>）: <c>KissActs</c> リリが手番で吸った（儀式を含む）回数 ／ <c>KissPainSum</c> その手番の頭に測った痛みの合計
    /// （前の手番の終わりから味方が失った HP・<b>版に依らず測る</b>）／ <c>KissAmountSum</c> 痛みから決まった1体あたりの吸う量の合計（痛みの版だけ）／
    /// <c>KissVoided</c> 溢れて捨てた量（捨てる版だけ・場面を問わない）／ <c>KissTierMax</c> その戦で届いた段の最大 ／
    /// <c>KissTierTurn</c> 段 1・2・3 に初めて届いたターン（0 ＝ 未到達）／
    /// <c>KissFoeBonusPos</c>・<c>KissFoeBonusNeg</c> 1体ずつ吸った敵の <c>AtkBonus</c> が正・負だった回数（Q0-5・版に依らず）と
    /// <c>…Sum</c> その絶対値の合計 ／ <c>KissStealN</c>・<c>KissStealPos</c>・<c>KissStealNeg</c> 移した攻撃力の上げ下げの件数と正・負の量（強弱を移す版だけ）／
    /// <c>KissStolenBy</c> 受け取った駒の <c>Def.Id</c> → (件数, 符号つきの合計) ／ <c>RiteFinish</c> 儀式の吸い取りで敵が全員倒れた回数（版に依らず）。
    /// </summary>
    public long KissActs, KissPainSum, KissAmountSum, KissVoided, KissFoeBonusPos, KissFoeBonusNeg, KissFoeBonusPosSum, KissFoeBonusNegSum,
                KissStealN, KissStealPos, KissStealNeg, RiteFinish;
    public int KissTierMax;
    public int[]? KissTierTurn;
    public Dictionary<string, (long N, long Sum)>? KissStolenBy;

    /// <summary>
    /// 第207期（<b>計数専用</b>・継ぎ当てのツギ）。ツギの側: <c>PlankPastes</c> 板を貼った回数 ／ <c>PlankGiven</c> 貼った破片の合計 ／
    /// <c>PlankStockUsed</c> そのうち背中の在庫の分 ／ <c>ScrapArmor</c> 拾った「砕けた破片」の量（生・率を掛ける前）／ <c>ScrapFalls</c> 拾った「倒れた駒」の数 ／
    /// <c>PlankSelf</c> 自分に貼った回数 ／ <c>PlankOnStoic</c> 支援を受け付けない駒に貼った回数 ／ <c>PlankInDrought</c> 渇きの保持者が生きている間に貼った回数。
    /// 受け取った側: <c>PlankSoaked</c> 板の印を持っている間に破片が吸った量 ／ <c>PlankFlares</c> 板の印で燃焼が倍になった回数 ／
    /// <c>PlankFlareTurns</c> 倍で増えた残りターンの合計。
    /// </summary>
    public long PlankPastes, PlankGiven, PlankStockUsed, ScrapArmor, ScrapFalls, PlankSelf, PlankOnStoic, PlankInDrought,
                PlankSoaked, PlankFlares, PlankFlareTurns;

    /// <summary>
    /// 第208期（<b>計数専用</b>）。板を持つ側: <c>ReflectCount</c> 返した回数 ／ <c>ReflectNominal</c> 返した量（砕けた破片）／ <c>ReflectDealt</c> 実際に削った HP ／
    /// <c>ReflectKills</c> 返した一撃で敵が倒れた回数 ／ <c>ReflectWasted</c> 殴った敵が既に倒れていて返らなかった量 ／ <c>ReflectMixed</c> ほかの書き手の破片も受けた板からの反射の回数。
    /// ツギの側: <c>ReflectGroupHist</c> 1回の敵の攻撃で返った本数（1・2・3・4以上）と <c>ReflectGroupKilled</c> その攻撃の主が倒れた数。
    /// 燃えた側: <c>PlankScorched</c> 燃えやすい板で燃焼の刻みが倍になった回数 ／ <c>PlankScorchExtra</c> 倍で増えた量。
    /// ドハの側: <c>SharerLate</c> 破片の段の後ろで肩代わりした回数（U3）。
    /// </summary>
    public long ReflectCount, ReflectNominal, ReflectDealt, ReflectKills, ReflectWasted, ReflectMixed, PlankScorched, PlankScorchExtra, SharerLate;
    public long[]? ReflectGroupHist, ReflectGroupKilled;

    /// <summary>
    /// 第209期（<b>計数専用</b>）。板を持つ側: <c>ReflectLost</c> 返した量のうち失った破片の分 ／ <c>ReflectThick</c> 残りの厚さの分（<c>ReflectNominal</c> ＝ 両者の和）／
    /// <c>ReflectWastedLost</c> 空振りのうち失った破片の分 ／ <c>ReflectRestSum</c>・<c>ReflectRestZero</c>・<c>ReflectRestHist</c> 反射の瞬間に残っていた破片（合計・0 の回数・値ごとの回数）／
    /// <c>ReflectYoke</c> 軛が効いている間の反射の回数 ／ <c>ReflectYokeOver</c> そのうち返す量が上限を超えた回数 ／
    /// <c>ReflectYokeHyp</c> 同じ反射に倍率 0・25・50・100% を当てたと仮定して上限を超える回数（見当）。
    /// ツギの側: <c>MultiHitAttacks</c> 敵の1回の攻撃がツギの陣営の駒に2体以上当たった回数 ／ <c>MultiHitTargets</c> 当たった延べ数 ／
    /// <c>MultiHitPlanked</c> そのうち撃ち返す板の印を持っていた延べ数 ／ <c>MultiHitPlankedHist</c> 1回の攻撃で板を持っていた数（0・1・2・3以上）。
    /// </summary>
    public long ReflectLost, ReflectThick, ReflectWastedLost, ReflectRestSum, ReflectRestZero, ReflectYoke, ReflectYokeOver,
                MultiHitAttacks, MultiHitTargets, MultiHitPlanked;
    public long[]? ReflectRestHist, ReflectYokeHyp, MultiHitPlankedHist;
    /// <summary><see cref="ReflectRestHist"/> の大きさ（最後の欄は「それ以上」）。</summary>
    public const int RestHistSize = 121;
    /// <summary>
    /// 第210期（<b>計数専用</b>）。ツギの側: <c>PlankBaseHist</c> 手番の板の「在庫を除いた分」（基本 ＋ 腕）の値ごとの回数（最後の欄は <see cref="RestHistSize"/> − 1 以上）／
    /// <c>PlankBaseGiven</c>・<c>PlankSkillGiven</c> 貼った破片のうち基本の分・腕の分（在庫の分は <c>PlankStockUsed</c>）／
    /// <c>FirstAidChance</c> 応急処置の条件（破片 0・生きている・HP が <see cref="FirstAidTrait.Percent"/>% 未満）を満たす被弾が起きたターンの数（1ターン1回に数える）／
    /// <c>FirstAidChanceHushed</c> そのうち粛の保持者が生きていた数 ／ <c>FirstAidChanceCross</c> そのうちその一撃で閾値を跨いだ数 ／
    /// <c>FirstAidFired</c> 応急処置を貼った回数 ／ <c>FirstAidPaste</c> その量の合計 ／ <c>FirstAidTurnSum</c> 貼ったターンの合計 ／
    /// <c>FirstAidCross</c> そのうち跨いだ一撃で貼った数 ／ <c>FirstAidSelf</c> 自分に貼った数 ／
    /// <c>FirstAidHushed</c> 粛で止まった数 ／ <c>FirstAidHeld</c> 痺れ・組み付き・割り込みや反撃の中で止まった数 ／ <c>FirstAidSpent</c> そのターンの1回を使い切っていて貼れなかった数 ／
    /// <c>PlankSkillLost</c> 腕の累計（板の印を持つ味方が敵の一撃で失った破片）／ <c>PlankSkillReach</c>・<c>PlankSkillReachTurn</c> 腕が段 1・2・3（以上）に届いた回数とターンの合計。
    /// 受け取った側: <c>FirstAidReceived</c> 応急処置を受けた回数 ／ <c>FirstAidMissed</c> 条件を満たしたのに貼られなかった回数 ／
    /// <c>FirstAidNeed</c> 条件を満たす被弾を受けた回数（札の有無に依らない・ツギが生きている間）。
    /// </summary>
    public long PlankBaseGiven, PlankSkillGiven, FirstAidChance, FirstAidChanceHushed, FirstAidChanceCross,
                FirstAidFired, FirstAidPaste, FirstAidTurnSum, FirstAidCross, FirstAidSelf, FirstAidHushed, FirstAidHeld, FirstAidSpent,
                PlankSkillLost, FirstAidReceived, FirstAidMissed, FirstAidNeed;
    public long[]? PlankBaseHist, PlankSkillReach, PlankSkillReachTurn;
    /// <summary><see cref="ReflectYokeHyp"/> の倍率（百分率）。</summary>
    public static readonly int[] HypRatios = { 0, 25, 50, 100 };

    /// <summary>
    /// 第206期（<b>計数専用</b>）: <c>RiteFoeHist</c> 儀式の頭に生きていた聖痕の敵の数（添字 0 ＝ 1体・1 ＝ 2体・2 ＝ 3体以上）／
    /// <c>RiteMaxPerFoe</c> その戦の儀式で1体から吸えた量の最大 ／ <c>KissHealCross</c> 施した累計が
    /// <see cref="KissTrait.HealCrossProbes"/>（40・120・240・400）に初めて届いたターン（0 ＝ 未到達・<b>段の刻みに依らず</b>・段の札を持つときだけ積む）。
    /// </summary>
    public long[]? RiteFoeHist;
    public int RiteMaxPerFoe;
    public int[]? KissHealCross;

    /// <summary>第204期（<b>計数専用</b>）: この駒に載っていた破片の最大値（リリの手番の頭の走査と、リリが破片を足した直後に更新）。</summary>
    public int ArmorPeakSeen;

    /// <summary>
    /// 第198期 Phase 0（<b>計数専用・どの規則も読まない</b>）: 自陣営で<b>最後の1体</b>になった瞬間（味方陣営だけ・召喚された駒も数える）。
    /// <c>AloneTurn</c> そのターン（0 ＝ 一度もならなかった。蘇生で仲間が戻っても最初の値のまま）／
    /// <c>AloneHp</c>・<c>AloneMaxHp</c> そのときの HP と最大HP ／ <c>AloneFoes</c> そのとき生きていた敵の数。
    /// </summary>
    public int AloneTurn, AloneHp, AloneMaxHp, AloneFoes;

    /// <summary>
    /// 第198期（剣の段・<b>ガルドの側・計数専用</b>）: <c>LastStandTurn</c> 入ったターン（0 ＝ 入らなかった）／ <c>LastStandHp</c>・<c>LastStandMaxHp</c> そのときの HP ／
    /// <c>LastStandFoes</c> そのときの敵の数 ／ <c>LastStandBaseDealt</c> そのときまでの <c>DamageToEnemy</c>（剣の段で与えた量 ＝ 終わりの値 − これ）／
    /// <c>LastStandStockDropped</c> 捨てた受け流しの在庫 ／ <c>LastStandRipostes</c>・<c>LastStandRiposteDealt</c> 斬り返し（生きているうち）／
    /// <c>LastStandDyingRipostes</c>・<c>LastStandDyingDealt</c> 相打ち ／ <c>LastStandRiposteSkipped</c> 徴収・巻き込み・中継で返さなかった被弾 ／
    /// <c>LastStandRiposteInReaction</c> 反撃の中で返さなかった被弾 ／ <c>LastStandRiposteBlocked</c> 門（粛・痺れ・組み付き）で返せなかった被弾 ／
    /// <c>LastStandParried</c> 剣の段に入った後に受け流した回数（盾を捨てた版では構造的に 0）／ <c>LastStandRefillBlocked</c> 庇っても在庫が戻らなかった回数。
    /// </summary>
    public int LastStandTurn, LastStandHp, LastStandMaxHp, LastStandFoes, LastStandBaseDealt;
    /// <summary>第198期（剣＋傷・計数専用）: 剣を抜いたときの「庇って身に受けた傷の累計」（版に依らず）と、加えた攻撃力（剣＋傷の版だけ）。</summary>
    public int LastStandScarTaken, LastStandScarAtk;
    /// <summary>
    /// 第199期（計数専用）: <c>LastStandParriedTaken</c> 抜いたときの受け流した刃の累計 ／ <c>LastStandStockAtDraw</c> 抜いたときの受け流しの在庫 ／
    /// <c>LastStandStancesAtDraw</c>・<c>LastStandRefillGuardAtDraw</c> 抜いたときの <c>ParryStances</c>・<c>ParryRefillGuard</c>（抜いた後に増えていないかの自己検査）／
    /// <c>LastStandMutualWins</c> 相打ちで最後の敵を倒して勝った回数。
    /// </summary>
    public int LastStandParriedTaken, LastStandStockAtDraw, LastStandStancesAtDraw, LastStandRefillGuardAtDraw;
    public long LastStandMutualWins;
    public long LastStandStockDropped, LastStandRipostes, LastStandRiposteDealt, LastStandDyingRipostes, LastStandDyingDealt,
                LastStandRiposteSkipped, LastStandRiposteInReaction, LastStandRiposteBlocked, LastStandParried, LastStandRefillBlocked;

    /// <summary>
    /// 第194期（濃縮の印・ミオ）。<b>計数専用で、どの規則も読まない。</b>
    /// <para><b>ミオの側</b>: <c>ConcFires</c> 印を付けようとした手番 ／ <c>ConcDry</c> 次の刻みが 0 の敵しかいなかった手番 ／
    /// <c>ConcMarkCenter</c>・<c>ConcMarkAround</c>・<c>ConcMarkAlly</c> 足した印（中心・周りの敵・隣の味方。1回 +1）／
    /// <c>ConcCenterBurning</c> 中心が燃えていた手番 ／ <c>ConcNoNew</c>（第194期の途中の版の名残・常に 0）／
    /// <c>ConcMarkPeak</c> 1戦で付けた印の最大値。</para>
    /// <para><b>カタの側</b>: <c>DetonateMarkedAgain</c> 印で起爆をもう1回働かせた延べ（回数ぶん）。</para>
    /// <para><b>全駒の側</b>: <c>TickFiresMax</c> 1回の刻み（毒・燃焼・起爆のどれか）で発火した回数の最大（印が無ければ 0）。</para>
    /// <para><b>全駒の側</b>（刻みの時点・<b>旧のミオでも同じ計数が取れる</b>）: <c>PoisonTickMax</c>・<c>BurnTickMax</c> 1回の刻みの最大 ／
    /// <c>PoisonTickEarly</c>・<c>PoisonTickLate</c>・<c>BurnTickEarly</c>・<c>BurnTickLate</c> 刻みの名目（1〜3 T・4 T〜・2回目も含む）／
    /// <c>PoisonTickSecond</c>・<c>BurnTickSecond</c> そのうち印の2回目 ／ <c>PoisonReach25</c>・<c>PoisonReach50</c> 毒の刻みが初めて 25・50 に届いたターン
    /// （0 なら届かず・同じ Id の駒どうしは早いほう）／ <c>PoisonYokeCut</c>・<c>PoisonYokeLost</c>・<c>BurnYokeCut</c> 軛が効いている間に上限を越えた刻み。</para>
    /// </summary>
    public long ConcFires, ConcDry, ConcMarkCenter, ConcMarkAround, ConcMarkAlly, ConcCenterBurning, ConcNoNew, DetonateMarkedAgain,
                PoisonTickMax, BurnTickMax, PoisonTickEarly, PoisonTickLate, BurnTickEarly, BurnTickLate,
                PoisonTickSecond, BurnTickSecond, PoisonReach25, PoisonReach50, PoisonYokeCut, PoisonYokeLost, BurnYokeCut,
                ConcMarkPeak, TickFiresMax;

    /// <summary>
    /// 第195期（毒吐きのスィド・痺れ毒）。<b>計数専用で、どの規則も読まない。</b>
    /// <para><b>スィドの側</b>: <c>SpewActs</c> 吐いた手番 ／ <c>SpewDry</c> 敵がいなかった手番 ／
    /// <c>SpewMarks</c>・<c>VenomMarks</c> 新しく印を付けた敵の数（吐いた ／ 殴ってきた）／
    /// <c>SpewOnMarked</c> 既に印のある敵に吐いた回数（第196期）。</para>
    /// <para><b>振った側</b>（印のある駒）: <c>NumbedSwings</c> 減った一撃の数（層 > 0 のときだけ）／ <c>NumbedCut</c> 減らした打点の合計
    /// （<c>NumbedCutSpew</c>・<c>NumbedCutVenom</c> 印が付いた経路で分けたもの）／ <c>NumbedLayerSum</c>・<c>NumbedLayerMax</c> そのときの毒の層 ／
    /// <c>NumbedCapSwings</c> 上限 60% に届いた一撃 ／ <c>NumbedCapTurn</c> 初めて上限に届いたターン（0 は届かず）。</para>
    /// </summary>
    public long SpewActs, SpewDry, SpewMarks, VenomMarks, SpewOnMarked,
                NumbedSwings, NumbedCut, NumbedCutSpew, NumbedCutVenom, NumbedLayerSum, NumbedLayerMax, NumbedCapSwings, NumbedCapTurn;

    /// <summary>「初めて届いたターン」の合成（0 は届かず）。</summary>
    public static long MinReach(long a, long b) => a == 0 ? b : b == 0 ? a : Math.Min(a, b);

    /// <summary>墓守の層の最大値（第188期・<b>計数専用</b>。<c>NecroTrait.SetStack</c> が書く）。</summary>
    public long NecroPeak;

    /// <summary>
    /// 第180期の4枚。<b>計数専用で、どの規則も読まない。</b>
    ///
    /// <para>暴発（ムド）: <c>EruptFuel</c> 数えた被弾 ／ <c>EruptFuelFromAlly</c> うち味方の刃 ／
    /// <c>EruptFires</c> 暴発した回数 ／ <c>EruptSwings</c> 放った発数 ／
    /// <c>EruptPeak</c> 1回の最大連撃 ／ <c>EruptHeld</c> 入れ子で見送った回数。</para>
    ///
    /// <para>泥散り（ムド）: <c>SmearDealt</c> 撒いた総量 ／ <c>SmearBlocked</c> 支援拒否に弾かれた回数。</para>
    ///
    /// <para>吐き戻し（ヴィオ）: <c>SpitStored</c> 腹に記帳した層（<b>版に依らない分母</b>）／
    /// <c>SpitMoved</c> 吐いた層 ／ <c>SpitFires</c> 吐いた回数。</para>
    ///
    /// <para>叩き起こし（ガン）: <c>ReveilleFires</c> 起こした回数（保持者の側）／
    /// <c>ReveilleMisses</c> 相手がいなかった回数 ／ <c>ReveilleWoken</c> <b>起こされた回数</b>（受け手の側）。</para>
    /// </summary>
    public long EruptFuel, EruptFuelFromAlly, EruptFires, EruptSwings, EruptPeak, EruptHeld,
                EruptSwingAtk, EruptFloored,
                SmearDealt, SmearBlocked,
                SpitStored, SpitMoved, SpitFires,
                ReveilleFires, ReveilleMisses, ReveilleWoken;

    /// <summary>
    /// 第183期の3枚。<b>計数専用で、どの規則も読まない。</b>
    ///
    /// <para>縫い合わせ（ヴェル・保持者の側）: <c>StitchFires</c> 縫った回数 ／ <c>StitchHealed</c> 実際に増えた HP ／
    /// <c>StitchScarDealt</c> 削った最大HP ／ <c>StitchSealed</c> 渇きに封じられた回数 ／
    /// <c>StitchSwings</c> 傷ついた隣人がいなくて殴った回数 ／ <c>RevivesGiven</c> 蘇生した回数（<c>Revive</c> の <c>by</c>）。
    /// 受け手の側: <c>StitchedTimes</c> 縫われた回数 ／ <c>StitchScarTaken</c> 失った最大HP。</para>
    ///
    /// <para>触れてうつす（ラウ）: <c>TouchFires</c> うつした回数 ／ <c>TouchTargets</c> うつした延べ体数 ／
    /// <c>TouchLayers</c> うつした層 ／ <c>TouchMisses</c> 毒はあったが隣に敵がいなかった回数 ／
    /// <c>TouchLeakOut</c> 漏らした層。受け手の側: <c>TouchLeakIn</c> 漏れを受けた層。
    /// 読み手（ヴィオ）の側: <c>TouchLeakDrawn</c> 吸った層のうち漏れ由来の上限 ／ <c>TouchLeakDrawFires</c> その回数。</para>
    /// </summary>
    public long StitchFires, StitchHealed, StitchScarDealt, StitchSealed, StitchSwings, RevivesGiven,
                StitchedTimes, StitchScarTaken,
                TouchFires, TouchTargets, TouchLayers, TouchMisses, TouchLeakOut, TouchLeakIn,
                TouchLeakDrawn, TouchLeakDrawFires;

    /// <summary>
    /// 第184期の2枚（標の軸）。<b>計数専用で、どの規則も読まない。</b>
    ///
    /// <para>矢面（ヒサ・保持者の側）: <c>BeckonFires</c> 標を付けた回数 ／ <c>BeckonSwitches</c> そのうち付け替え ／
    /// <c>BeckonIdle</c> 隣がいなかった ／ <c>BeckonGuardSaved</c> 半減で防いだ量。
    /// 受け手の側: <c>BeckonPicked</c> 指差された回数 ／ <c>BeckonGuardTaken</c> 半減で防いでもらった量。</para>
    ///
    /// <para>逃げ回る: <c>FleeSwaps</c> 入れ替えた回数 ／ <c>FleeStuck</c> 相手がいなかった ／
    /// <c>FleeReaderSwings</c> 入れ替えの最中に振られた回数（軋みの割り込みなど）／
    /// <c>FleeReaderWhet</c> 入れ替えの最中に味方が受けた強化（移り木など）／
    /// <c>FleeFoeMoves</c> 入れ替えの最中に動いた敵（突き返しなど）。受け手の側: <c>FleePushed</c>。</para>
    ///
    /// <para>仇指し（ザン）: <c>VendettaFires</c> 刃を返した回数 ／ <c>VendettaDealt</c> 実際に削った HP ／
    /// <c>VendettaMarks</c> 標を付けた回数 ／ <c>RecoilTaken</c> 返り血の総量。
    /// 殴った側: <c>MarkVulnDealt</c> §1 の被ダメージ増で上乗せした量。</para>
    /// </summary>
    public long BeckonFires, BeckonSwitches, BeckonIdle, BeckonGuardSaved, BeckonPicked, BeckonGuardTaken,
                FleeSwaps, FleeStuck, FleeReaderSwings, FleeReaderWhet, FleeFoeMoves, FleePushed,
                VendettaFires, VendettaDealt, VendettaMarks, RecoilTaken, MarkVulnDealt,
                BeckonStrippedHits;

    /// <summary>
    /// 第185期の3枚（A群の転生 3〜5枚目）。<b>計数専用で、どの規則も読まない。</b>
    ///
    /// <para>組み付き（クグ・保持者の側）: <c>GrappleFires</c> 組み付いた回数 ／ <c>GrappleHolds</c> 組んだまま維持した手番 ／
    /// <c>GrappleStalled</c> 止めた敵の手番数 ／ <c>GrappleBreaks</c> 殴られてほどけた回数。
    /// 敵の側: <c>GrappledTimes</c> 組み付かれた回数（＝相手の内訳）／ <c>StallGrappled</c> 組み付かれて失った手番。</para>
    ///
    /// <para>見せしめ（シガ）: <c>ShamePicks</c> 動けない敵を優先して狙った回数 ／ <c>ShameFires</c> 見せしめが出た回数 ／
    /// <c>ShameCowed</c> 竦ませた延べ体数 ／ <c>ShameBlocked</c> ハメ防止で弾いた延べ体数。
    /// 敵の側: <c>CowedLost</c> 竦みを消費した回数（失う手番に吸われた分を含む）／ <c>StallCowed</c> 竦みで失った手番。</para>
    ///
    /// <para>踏みしめ（バン）: <c>FootingSteps</c> 層を積んだ手番 ／ <c>FootingFull</c> 満ちていた手番 ／ <c>FootingSaved</c> 層で防いだ量 ／
    /// <c>ShieldTakes</c> 範囲の盾で代わりに受けた回数 ／ <c>ShieldTaken</c> その量（半分にした後・層の軽減の前）／
    /// <c>ShieldHalved</c> 半分にして消えた量（第185期 追補）／
    /// <c>PlantedRefused</c> 入れ替えを空振りさせた回数（うち敵が起こした入れ替え＝曝き <c>PlantedRefusedFoe</c>）／ <c>FootingLayerSum</c> ÷ <c>FootingLayerTurns</c> ターン末の層の平均。受け手の側: <c>ShieldCovered</c> 盾に受けてもらった量。</para>
    /// </summary>
    public long GrappleFires, GrappleHolds, GrappleStalled, GrappleBreaks, GrappledTimes, StallGrappled,
                ShamePicks, ShameFires, ShameCowed, ShameBlocked, CowedLost, StallCowed,
                FootingSteps, FootingFull, FootingSaved, ShieldTakes, ShieldTaken, ShieldCovered, PlantedRefused,
                FootingLayerSum, FootingLayerTurns, PlantedRefusedFoe, ShieldHalved, FootingHitSteps;

    /// <summary>
    /// 第186期の逸らし（ソラ・保持者の側）。<b>計数専用で、どの規則も読まない。</b>
    ///
    /// <para><c>DeflectHits</c> 逸らした回数 ／ <c>DeflectMoved</c> 逸らした名目量（素の量の半分）／
    /// <c>DeflectLanded</c> 宛先が実際に受けた量（敵の段・§1 込み・過剰を含む）／ <c>DeflectVulnAdded</c> そのうち §1 の上乗せ ／
    /// <c>DeflectKills</c> 逸らした分で宛先が倒れた回数 ／ <c>DeflectNoTarget</c> 宛先がいなくて全部受けた単体の一撃 ／
    /// <c>DeflectBeckonStack</c> 逸らした一撃にヒサの半減も重なった回数 ／ <c>DeflectKeptTaken</c> 逸らした一撃でソラが実際に受けた量 ／
    /// <c>DeflectSelf</c> 宛先が殴った本人だった回数（＝その一撃に限って反射と同じ形になる）／
    /// <c>DeflectExecFeed</c> 逸らした分で倒した撃破者（元の攻撃者）が処刑持ちだった回数（勇者候補が味方を倒して育つ）。</para>
    /// </summary>
    public long DeflectHits, DeflectMoved, DeflectLanded, DeflectVulnAdded, DeflectKills, DeflectNoTarget,
                DeflectBeckonStack, DeflectKeptTaken, DeflectSelf, DeflectExecFeed;

    /// <summary>
    /// 第186期 追補（逸らし先の内訳と突き）。<b>計数専用で、どの規則も読まない。</b>
    ///
    /// <para>逸らした宛先: <c>DeflectToPointed</c> 指差した敵 ／ <c>DeflectToOtherMarked</c> 殴った本人が指差した敵だったので、
    /// ほかの標持ちへ ／ <c>DeflectToFallback</c> ほかに標持ちがいないので、殴った本人以外の現在HP最大へ。
    /// （<c>DeflectSelf</c> は差し替えが起きた回数＝後ろ2つの和）</para>
    ///
    /// <para>突き: <c>ThrustSwings</c> 突いた回数 ／ <c>ThrustChargeSum</c> 乗った回数の和 ／ <c>ThrustChargeMax</c> 1回の最大 ／
    /// <c>ThrustAtkSum</c> 威力の和 ／ <c>ThrustAtkMax</c> 最大 ／ <c>ThrustForced</c> 指差した敵の列を突いた回数 ／
    /// <c>ThrustAtks</c> 威力の一覧（分布用・突きが1度でもあれば確保）。</para>
    /// </summary>
    public long DeflectToPointed, DeflectToOtherMarked, DeflectToFallback,
                ThrustSwings, ThrustChargeSum, ThrustChargeMax, ThrustAtkSum, ThrustAtkMax, ThrustForced;
    public List<int>? ThrustAtks;

    /// <summary>据えの層が最大（3）に届いた最初のターン（0 は届かなかった・第185期 追補4・<b>計数のみ</b>）。戦闘ごとの値で、Merge は最小を取る。</summary>
    public int FootingFullAt;

    public int BraceGuards, BraceCuts, BraceRefused, BraceGiven, BraceLost;
    public int BraceShoves, BraceShoveCapped, BraceNoTarget, BraceStaggers, BraceArmorMuted;

    /// <summary>
    /// 喧噪（<see cref="TraitId.Shuffler"/>・第144期）の帳簿。<b>計数専用で、どの規則も読まない。</b>
    ///
    /// <para><c>ShuffleAllySwaps</c> 味方を入れ替えた回数（<b>第144期より前と同じ動作</b>。
    /// 敵側を足しても味方側が1ビットも動いていないことの検算）／
    /// <c>ShuffleFoeSwaps</c> 敵を入れ替えた回数 ／
    /// <c>ShuffleAdvanced</c> そのうち<b>行が前に変わった敵の延べ体数</b>
    /// （<see cref="FormationRules.DepthOf"/> が浅くなった側。1回の入れ替えで 0 か 1）／
    /// <c>ShuffleAdvancedTraited</c> そのうち<b>特性を1つ以上持っていた体数</b>
    /// （＝後列・中央の祭司・狙撃手・盤面ルール持ちを引きずり出した回数）／
    /// <c>ShuffleAdvancedFromBack</c> そのうち <see cref="Row.Back"/> から出てきた体数 ／
    /// <c>ShuffleStaggers</c> 実際に <see cref="StatusKeys.Stagger"/> を立てた回数 ／
    /// <c>ShuffleNoFoePair</c> 敵側の候補が2体に満たず入れ替えなかったターン数。</para>
    ///
    /// <para><b><c>ShuffleStaggers ≦ ShuffleAdvanced</c> が受け入れ条件</b>
    /// （<see cref="ShuffleStagger.Advanced"/> のとき。<see cref="ShuffleStagger.Both"/> では
    /// 1回の入れ替えで2体に立つので <c>≦ 2 × ShuffleFoeSwaps</c> のほうが上限になる）。</para>
    /// </summary>
    public int ShuffleAllySwaps, ShuffleFoeSwaps, ShuffleAdvanced;

    /// <summary>
    /// 喧噪の味方側の入れ替えが<b>据えた足（バン）で空振りした</b>回数（第185期 追補6・<b>計数のみ</b>）。
    /// 空振りは <see cref="ShuffleAllySwaps"/> には数えない（あちらは実際に入れ替えた回数だけ）。
    /// </summary>
    public int ShuffleAllyRefused;
    public int ShuffleAdvancedTraited, ShuffleAdvancedFromBack, ShuffleStaggers, ShuffleNoFoePair;

    /// <summary>
    /// 喧噪が<b>混乱</b>を立てた回数（第147期・<see cref="ShuffleStagger.Confuse"/>。<b>計数のみ</b>）。
    /// <b><c>ShuffleConfuses ≦ ShuffleAdvanced</c> が受け入れ条件</b>——前へ出た敵にしか立てない。
    /// <b>保持者に載せる</b>（<c>ShuffleStaggers</c> と同じ。立った側は <c>ConfusedMarks</c>）ので、
    /// 在庫（<see cref="ShufflerRule.ConfuseUses"/>）の消費回数と一致する。
    /// </summary>
    public int ShuffleConfuses;

    /// <summary>
    /// 突風（第151期・<see cref="ShufflerRule.GustPercent"/>。<b>計数のみ。どの規則も読まない</b>）。
    /// <b>保持者に載せる</b>: <c>GustSwings</c> 薙ぎを振った回数 ／
    /// <c>GustPrimary</c> 主目標に当たった体数（＝振った回数。<b>倒れた相手も数える</b>）／
    /// <c>GustSplash</c> 巻き込みで当たった体数（<c>OnAfterAttack</c> の時点で生きている分）／
    /// <c>GustFell</c> 実際に <see cref="StatusKeys.Stagger"/> を立てた回数。
    ///
    /// <para><b>立てられた側には <c>GustFellHere</c> を載せる</b>——
    /// 「誰を転ばせたか」は保持者の帳簿からは引けない（第150期の「門を読み手に掛けない」の逆側で、
    /// <b>供給と受領を別々に数える</b>・第124期 Q0-4）。</para>
    ///
    /// <para><b><c>GustFell ≦ GustPrimary + GustSplash</c> が受け入れ条件。</b>
    /// 等号にならないのは (i) 既に転んでいる相手には立て直さない（二値）／
    /// (ii) その振りで倒れた相手は <c>IsAlive</c> で落ちる／(iii) <c>GustPercent &lt; 100</c> の分。</para>
    /// </summary>
    public int GustSwings, GustPrimary, GustSplash, GustFell, GustFellHere;

    /// <summary>
    /// 混乱（第146期・<see cref="ConfusionRule"/>）。<b>計数のみ。</b>
    /// <c>ConfusedMarks</c> この駒に混乱が立った回数（＝動かされた回数のうち、まだ混乱していなかった分）／
    /// <c>ConfusedSwings</c> 混乱したまま振った回数。
    ///
    /// <para><b><c>ConfusedSwings ≦ ConfusedMarks</c> が受け入れ条件</b>
    /// （指示書 §4 の 6。差は「立ったまま振らずに終わった」分——倒れた・波が終わった・
    /// 振らない駒だった）。<b>1回の攻撃で必ず1つ落ちる</b>ので、
    /// 「発火回数 ＝ 混乱した駒が攻撃した回数」はこの2列で読む。</para>
    /// </summary>
    public int ConfusedMarks, ConfusedSwings;

    /// <summary>
    /// 混乱の下流（第147期・<b>計数のみ。どの規則も読まない</b>）。
    /// <c>ConfusedGuards</c> この駒が<b>混乱した攻撃を庇った</b>回数（介入の鎖の全段）／
    /// <c>ConfusedKills</c> この駒が<b>混乱したまま自軍の駒を倒した</b>回数／
    /// <c>ConfusedExecGain</c> そのうち<b>処刑（<see cref="TraitId.Executioner"/>）が積まれた</b>回数。
    ///
    /// <para><b>3つとも「敵が敵に対してやったこと」を測るためにある</b>——処刑も殉教も
    /// <b>陣営を見ない</b>ので、混乱は敵側の札に直撃する（第147期 Q0-3）。
    /// <c>ConfusedExecGain ≦ ConfusedKills</c> が受け入れ条件。</para>
    /// </summary>
    public int ConfusedGuards, ConfusedKills, ConfusedExecGain;

    /// <summary>敵に与えたダメージのうち、手番の中／外で生んだ分（<see cref="DamageToEnemy"/> の内訳）。</summary>
    public int DmgOutInTurn, DmgOutOffTurn;

    /// <summary>この駒が回復させた HP（実際に増えた分）のうち、手番の中／外の分。<b>受け手ではなく配り手に載る。</b></summary>
    public int HealOutInTurn, HealOutOffTurn;

    /// <summary>
    /// この駒が書いた状態異常の<b>回数</b>（毒・燃・痺・標・破片・傷・手番の7キー）のうち、手番の中／外の分。
    /// <b>量ではなく回数</b>——キーごとに単位が違う（層／残T／回／量）ので足せない。
    /// </summary>
    public int StatusOutInTurn, StatusOutOffTurn;

    /// <summary>
    /// <b>4つ目の通貨（第106期 (T1)）</b>——この駒が動かした <see cref="UnitState.AtkBonus"/> の
    /// <b>絶対量</b>のうち、手番の中／外の分。強化と弱体を1本にまとめてあるのは、
    /// どちらも単位が「攻撃力の点」で足せるから（状態異常が回数なのと逆）。
    ///
    /// <para><b>窓口（<c>Whet</c> / <c>Dull</c>）だけでなく、自己強化の9本も入る</b>
    /// ——観測点が <c>AtkBonus</c> の setter なので、経路を追加しても数え漏らさない。
    /// <b>境界・蘇生の一括消去（<see cref="ResetAtkBonus"/>）は setter を通らないので入らない</b>
    /// （第68期の判断をそのまま引き継ぐ）。逆しま（<c>ModifyAttack</c>）は
    /// <c>AtkBonus</c> を1点も動かさないので、この通貨には現れない。</para>
    /// </summary>
    public int BuffOutInTurn, BuffOutOffTurn;

    public void Add(UnitTally o)
    {
        // 第103期。**盤面には一切影響しない。**
        BetrayFodderKills += o.BetrayFodderKills; BetrayFodderHits += o.BetrayFodderHits;
        SutureFoe += o.SutureFoe; SutureAlly += o.SutureAlly; SutureDry += o.SutureDry; SutureHealed += o.SutureHealed;
        SutureAllyDepth += o.SutureAllyDepth; SutureAllyDepthMax = Math.Max(SutureAllyDepthMax, o.SutureAllyDepthMax);
        SutureCalls += o.SutureCalls; SutureCapped += o.SutureCapped;
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
        // 第105期。すべて単純加算（ターン番号を持つ列は1つも無い）。
        TurnsTaken += o.TurnsTaken;
        TurnAttacks += o.TurnAttacks; TurnSkills += o.TurnSkills;
        TurnCharges += o.TurnCharges; TurnStalls += o.TurnStalls;
        StallStun += o.StallStun; StallSlumber += o.StallSlumber;
        StallImmobile += o.StallImmobile; StallCanAct += o.StallCanAct;
        TurnsSurrendered += o.TurnsSurrendered;
        DmgOutInTurn += o.DmgOutInTurn; DmgOutOffTurn += o.DmgOutOffTurn;
        HealOutInTurn += o.HealOutInTurn; HealOutOffTurn += o.HealOutOffTurn;
        StatusOutInTurn += o.StatusOutInTurn; StatusOutOffTurn += o.StatusOutOffTurn;
        BuffOutInTurn += o.BuffOutInTurn; BuffOutOffTurn += o.BuffOutOffTurn;
        RageCountFires += o.RageCountFires; RageGain += o.RageGain; MendPaidRaw += o.MendPaidRaw;
        if (o.AtkPeak > AtkPeak) { AtkPeak = o.AtkPeak; AtkPeakTurn = o.AtkPeakTurn; }
        if (o.AtkProbeTurn is not null)
        {
            int[] mine = AtkProbeTurn ??= new int[AtkProbes.Length];
            for (int i = 0; i < mine.Length; i++)
                if (mine[i] == 0 || (o.AtkProbeTurn[i] != 0 && o.AtkProbeTurn[i] < mine[i])) mine[i] = o.AtkProbeTurn[i];
        }
        LooseShoves += o.LooseShoves; LooseCapped += o.LooseCapped; LooseNoTarget += o.LooseNoTarget;
        BraceGuards += o.BraceGuards; BraceCuts += o.BraceCuts; BraceRefused += o.BraceRefused;
        BraceGiven += o.BraceGiven; BraceLost += o.BraceLost; BraceShoves += o.BraceShoves;
        BraceShoveCapped += o.BraceShoveCapped; BraceNoTarget += o.BraceNoTarget;
        BraceStaggers += o.BraceStaggers; BraceArmorMuted += o.BraceArmorMuted;
        WardStacked += o.WardStacked; WardReleased += o.WardReleased; WardForfeited += o.WardForfeited;
        WardBurdenTaken += o.WardBurdenTaken; WardLadenLost += o.WardLadenLost;
        IndulgenceStacked += o.IndulgenceStacked; TollTaken += o.TollTaken;
        AshGained += o.AshGained; AshFires += o.AshFires; AshSpent += o.AshSpent;
        AshDry += o.AshDry; AshAtDeath += o.AshAtDeath; AshResidual += o.AshResidual;
        AshHolds += o.AshHolds; AshFalloutOut += o.AshFalloutOut;
        if (o.AshPeak > AshPeak) AshPeak = o.AshPeak;
        DetonateFires += o.DetonateFires; DetonateDry += o.DetonateDry; DetonateDualTargets += o.DetonateDualTargets;
        DetonatePoisonNominal += o.DetonatePoisonNominal; DetonateBurnNominal += o.DetonateBurnNominal;
        DetonateDualExtra += o.DetonateDualExtra; DetonateFoeDealt += o.DetonateFoeDealt;
        DetonateAllyNominal += o.DetonateAllyNominal; DetonateAllyDealt += o.DetonateAllyDealt;
        DetonateFoeKills += o.DetonateFoeKills; DetonateAllyKills += o.DetonateAllyKills; DetonateYokeCut += o.DetonateYokeCut;
        HexerActs += o.HexerActs; HexerMarks += o.HexerMarks; HexerCursedSum += o.HexerCursedSum;
        HexerCursedMax = Math.Max(HexerCursedMax, o.HexerCursedMax); HexerDullTotal += o.HexerDullTotal; HexLeakTotal += o.HexLeakTotal;
        DauntActs += o.DauntActs; DauntFoes += o.DauntFoes; DauntFoesAlready += o.DauntFoesAlready; DauntAllies += o.DauntAllies;
        DauntedSwings += o.DauntedSwings; DauntedCut += o.DauntedCut;
        ReboundThrusts += o.ReboundThrusts; ReboundStaggers += o.ReboundStaggers; ReboundNoFront += o.ReboundNoFront; ReboundRefused += o.ReboundRefused;
        OverrunSwaps += o.OverrunSwaps; OverrunRefused += o.OverrunRefused; OverrunNoAlly += o.OverrunNoAlly;
        OverrunReaderDisplaced += o.OverrunReaderDisplaced; OverrunReaderDrifter += o.OverrunReaderDrifter; OverrunReaderSniper += o.OverrunReaderSniper;
        InversePoisonHealed += o.InversePoisonHealed; InverseBurnHealed += o.InverseBurnHealed; InverseDetonateHealed += o.InverseDetonateHealed;
        InverseNominal += o.InverseNominal; InverseRecipientTurns += o.InverseRecipientTurns;
        InverseMaxSimul = Math.Max(InverseMaxSimul, o.InverseMaxSimul);
        TaintActs += o.TaintActs; TaintDry += o.TaintDry; TaintLayers += o.TaintLayers;
        InverseLeakFires += o.InverseLeakFires; InverseLeakNominal += o.InverseLeakNominal; InverseLeakDealt += o.InverseLeakDealt;
        InverseLeakKills += o.InverseLeakKills; TaintPostBite += o.TaintPostBite; InverseLeakBy += o.InverseLeakBy; VioAteTaint += o.VioAteTaint;
        KindleActs += o.KindleActs; KindleDry += o.KindleDry; KindleTargets += o.KindleTargets;
        InversePoisonEarly += o.InversePoisonEarly; InverseBurnEarly += o.InverseBurnEarly;
        InverseDetonateEarly += o.InverseDetonateEarly; InverseBurnNominal += o.InverseBurnNominal; KindlePostBurn += o.KindlePostBurn; InverseBurnNominalEarly += o.InverseBurnNominalEarly;
        InverseSelfPoison += o.InverseSelfPoison; InverseSelfBurn += o.InverseSelfBurn; InverseSelfDetonate += o.InverseSelfDetonate;
        InverseLeakSelf += o.InverseLeakSelf; InverseLeakOnHolder += o.InverseLeakOnHolder;
        SipEligible += o.SipEligible; SipRoom += o.SipRoom; SipGained += o.SipGained; SipEarly += o.SipEarly;
        SipWaste += o.SipWaste;
        GurenGained += o.GurenGained; GurenFires += o.GurenFires; GurenSpent += o.GurenSpent; GurenLitNew += o.GurenLitNew; GurenRelit += o.GurenRelit;
        GurenLayers += o.GurenLayers; GurenPoisonTick += o.GurenPoisonTick; GurenPoisonTickMark += o.GurenPoisonTickMark;
        GurenMioLayers += o.GurenMioLayers; GurenMioTick += o.GurenMioTick; GurenMioTickMark += o.GurenMioTickMark;
        GurenBurnTickNew += o.GurenBurnTickNew; GurenBurnTickRelit += o.GurenBurnTickRelit; GurenBurnTickMark += o.GurenBurnTickMark;
        GurenStrikeNominal += o.GurenStrikeNominal; GurenStrikeDealt += o.GurenStrikeDealt;
        if (o.GurenFirstTurn > 0 && (GurenFirstTurn == 0 || o.GurenFirstTurn < GurenFirstTurn)) GurenFirstTurn = o.GurenFirstTurn;
        // 第204期（リリ）
        KissFires += o.KissFires; KissNominal += o.KissNominal; KissDrained += o.KissDrained; KissKills += o.KissKills; KissHealed += o.KissHealed;
        KissArmor += o.KissArmor; KissBlocked += o.KissBlocked; KissToSelf += o.KissToSelf; KissMoves += o.KissMoves; KissMovedLayers += o.KissMovedLayers; KissReset += o.KissReset;
        RiteFires += o.RiteFires; RiteDrained += o.RiteDrained; RiteHealed += o.RiteHealed; RiteArmor += o.RiteArmor; RiteBlocked += o.RiteBlocked; RiteTurnSum += o.RiteTurnSum;
        ReturnFires += o.ReturnFires; ReturnNominal += o.ReturnNominal; ReturnHealed += o.ReturnHealed; ReturnArmor += o.ReturnArmor;
        if (o.RiteFirstTurn > 0 && (RiteFirstTurn == 0 || o.RiteFirstTurn < RiteFirstTurn)) RiteFirstTurn = o.RiteFirstTurn;
        ArmorPeakSeen = Math.Max(ArmorPeakSeen, o.ArmorPeakSeen);
        if (o.KissSelfWhy is not null) { KissSelfWhy ??= new long[9]; for (int i = 0; i < 9; i++) KissSelfWhy[i] += o.KissSelfWhy[i]; }
        if (o.KissSelfFull is not null) { KissSelfFull ??= new long[3]; for (int i = 0; i < 3; i++) KissSelfFull[i] += o.KissSelfFull[i]; }
        KissActs += o.KissActs; KissPainSum += o.KissPainSum; KissAmountSum += o.KissAmountSum; KissVoided += o.KissVoided;
        KissFoeBonusPos += o.KissFoeBonusPos; KissFoeBonusNeg += o.KissFoeBonusNeg; KissFoeBonusPosSum += o.KissFoeBonusPosSum; KissFoeBonusNegSum += o.KissFoeBonusNegSum;
        KissStealN += o.KissStealN; RiteFinish += o.RiteFinish; KissStealPos += o.KissStealPos; KissStealNeg += o.KissStealNeg;
        PlankPastes += o.PlankPastes; PlankGiven += o.PlankGiven; PlankStockUsed += o.PlankStockUsed; ScrapArmor += o.ScrapArmor; ScrapFalls += o.ScrapFalls;
        PlankSelf += o.PlankSelf; PlankOnStoic += o.PlankOnStoic; PlankInDrought += o.PlankInDrought;
        PlankSoaked += o.PlankSoaked; PlankFlares += o.PlankFlares; PlankFlareTurns += o.PlankFlareTurns;
        ReflectCount += o.ReflectCount; ReflectNominal += o.ReflectNominal; ReflectDealt += o.ReflectDealt; ReflectKills += o.ReflectKills;
        ReflectWasted += o.ReflectWasted; ReflectMixed += o.ReflectMixed; PlankScorched += o.PlankScorched; PlankScorchExtra += o.PlankScorchExtra; SharerLate += o.SharerLate;
        if (o.ReflectGroupHist is not null) { ReflectGroupHist ??= new long[4]; for (int i = 0; i < 4; i++) ReflectGroupHist[i] += o.ReflectGroupHist[i]; }
        if (o.ReflectGroupKilled is not null) { ReflectGroupKilled ??= new long[4]; for (int i = 0; i < 4; i++) ReflectGroupKilled[i] += o.ReflectGroupKilled[i]; }
        ReflectLost += o.ReflectLost; ReflectThick += o.ReflectThick; ReflectWastedLost += o.ReflectWastedLost; ReflectRestSum += o.ReflectRestSum;
        ReflectRestZero += o.ReflectRestZero; ReflectYoke += o.ReflectYoke; ReflectYokeOver += o.ReflectYokeOver;
        MultiHitAttacks += o.MultiHitAttacks; MultiHitTargets += o.MultiHitTargets; MultiHitPlanked += o.MultiHitPlanked;
        if (o.ReflectRestHist is not null) { ReflectRestHist ??= new long[RestHistSize]; for (int i = 0; i < RestHistSize; i++) ReflectRestHist[i] += o.ReflectRestHist[i]; }
        if (o.ReflectYokeHyp is not null) { ReflectYokeHyp ??= new long[4]; for (int i = 0; i < 4; i++) ReflectYokeHyp[i] += o.ReflectYokeHyp[i]; }
        if (o.MultiHitPlankedHist is not null) { MultiHitPlankedHist ??= new long[4]; for (int i = 0; i < 4; i++) MultiHitPlankedHist[i] += o.MultiHitPlankedHist[i]; }
        PlankBaseGiven += o.PlankBaseGiven; PlankSkillGiven += o.PlankSkillGiven; FirstAidChance += o.FirstAidChance; FirstAidChanceHushed += o.FirstAidChanceHushed;
        FirstAidChanceCross += o.FirstAidChanceCross; FirstAidFired += o.FirstAidFired; FirstAidPaste += o.FirstAidPaste; FirstAidTurnSum += o.FirstAidTurnSum;
        FirstAidCross += o.FirstAidCross; FirstAidSelf += o.FirstAidSelf; FirstAidHushed += o.FirstAidHushed; FirstAidHeld += o.FirstAidHeld; FirstAidSpent += o.FirstAidSpent;
        PlankSkillLost += o.PlankSkillLost; FirstAidReceived += o.FirstAidReceived; FirstAidMissed += o.FirstAidMissed; FirstAidNeed += o.FirstAidNeed;
        if (o.PlankBaseHist is not null) { PlankBaseHist ??= new long[RestHistSize]; for (int i = 0; i < RestHistSize; i++) PlankBaseHist[i] += o.PlankBaseHist[i]; }
        if (o.PlankSkillReach is not null) { PlankSkillReach ??= new long[4]; for (int i = 0; i < 4; i++) PlankSkillReach[i] += o.PlankSkillReach[i]; }
        if (o.PlankSkillReachTurn is not null) { PlankSkillReachTurn ??= new long[4]; for (int i = 0; i < 4; i++) PlankSkillReachTurn[i] += o.PlankSkillReachTurn[i]; }
        KissTierMax = Math.Max(KissTierMax, o.KissTierMax);
        if (o.RiteFoeHist is not null) { RiteFoeHist ??= new long[3]; for (int i = 0; i < 3; i++) RiteFoeHist[i] += o.RiteFoeHist[i]; }
        RiteMaxPerFoe = Math.Max(RiteMaxPerFoe, o.RiteMaxPerFoe);
        if (o.KissStolenBy is not null)
        {
            KissStolenBy ??= new();
            foreach (var (k, v) in o.KissStolenBy)
                KissStolenBy[k] = KissStolenBy.TryGetValue(k, out var a2) ? (a2.N + v.N, a2.Sum + v.Sum) : v;
        }
        if (o.KissMovedBy is not null)
        {
            KissMovedBy ??= new();
            foreach (var (k, v) in o.KissMovedBy)
                KissMovedBy[k] = KissMovedBy.TryGetValue(k, out var a) ? (a.N + v.N, a.Sum + v.Sum) : v;
        }
        // 第198期: ターン番号とその瞬間の値は足さない（`LastActiveTurn` と同じ扱い・後の値が勝つ）。
        if (o.AloneTurn > 0) { AloneTurn = o.AloneTurn; AloneHp = o.AloneHp; AloneMaxHp = o.AloneMaxHp; AloneFoes = o.AloneFoes; }
        if (o.LastStandTurn > 0) { LastStandTurn = o.LastStandTurn; LastStandHp = o.LastStandHp; LastStandMaxHp = o.LastStandMaxHp; LastStandFoes = o.LastStandFoes; LastStandBaseDealt = o.LastStandBaseDealt; LastStandScarTaken = o.LastStandScarTaken; LastStandScarAtk = o.LastStandScarAtk; LastStandParriedTaken = o.LastStandParriedTaken; LastStandStockAtDraw = o.LastStandStockAtDraw; LastStandStancesAtDraw = o.LastStandStancesAtDraw; LastStandRefillGuardAtDraw = o.LastStandRefillGuardAtDraw; }
        LastStandMutualWins += o.LastStandMutualWins;
        LastStandStockDropped += o.LastStandStockDropped; LastStandRipostes += o.LastStandRipostes; LastStandRiposteDealt += o.LastStandRiposteDealt;
        LastStandDyingRipostes += o.LastStandDyingRipostes; LastStandDyingDealt += o.LastStandDyingDealt; LastStandRiposteSkipped += o.LastStandRiposteSkipped;
        LastStandRiposteInReaction += o.LastStandRiposteInReaction; LastStandRiposteBlocked += o.LastStandRiposteBlocked;
        LastStandParried += o.LastStandParried; LastStandRefillBlocked += o.LastStandRefillBlocked;
        if (o.SipWasteByTurn is not null) { SipWasteByTurn ??= new long[o.SipWasteByTurn.Length]; for (int i = 0; i < o.SipWasteByTurn.Length && i < SipWasteByTurn.Length; i++) SipWasteByTurn[i] += o.SipWasteByTurn[i]; }
        ConcFires += o.ConcFires; ConcDry += o.ConcDry; ConcMarkCenter += o.ConcMarkCenter; ConcMarkAround += o.ConcMarkAround;
        ConcMarkAlly += o.ConcMarkAlly; ConcCenterBurning += o.ConcCenterBurning; ConcNoNew += o.ConcNoNew; DetonateMarkedAgain += o.DetonateMarkedAgain;
        PoisonTickMax = Math.Max(PoisonTickMax, o.PoisonTickMax); BurnTickMax = Math.Max(BurnTickMax, o.BurnTickMax);
        PoisonTickEarly += o.PoisonTickEarly; PoisonTickLate += o.PoisonTickLate; BurnTickEarly += o.BurnTickEarly; BurnTickLate += o.BurnTickLate;
        PoisonTickSecond += o.PoisonTickSecond; BurnTickSecond += o.BurnTickSecond;
        PoisonReach25 = MinReach(PoisonReach25, o.PoisonReach25); PoisonReach50 = MinReach(PoisonReach50, o.PoisonReach50);
        PoisonYokeCut += o.PoisonYokeCut; PoisonYokeLost += o.PoisonYokeLost; BurnYokeCut += o.BurnYokeCut;
        ConcMarkPeak = Math.Max(ConcMarkPeak, o.ConcMarkPeak); TickFiresMax = Math.Max(TickFiresMax, o.TickFiresMax);
        SpewActs += o.SpewActs; SpewDry += o.SpewDry; SpewMarks += o.SpewMarks; VenomMarks += o.VenomMarks;   // 第195期
        SpewOnMarked += o.SpewOnMarked;   // 第196期
        NumbedSwings += o.NumbedSwings; NumbedCut += o.NumbedCut; NumbedCutSpew += o.NumbedCutSpew; NumbedCutVenom += o.NumbedCutVenom;
        NumbedLayerSum += o.NumbedLayerSum; NumbedLayerMax = Math.Max(NumbedLayerMax, o.NumbedLayerMax);
        NumbedCapSwings += o.NumbedCapSwings; NumbedCapTurn = MinReach(NumbedCapTurn, o.NumbedCapTurn);
        if (o.NecroPeak > NecroPeak) NecroPeak = o.NecroPeak;
        BrandFires += o.BrandFires; BrandDealt += o.BrandDealt;
        StallStagger += o.StallStagger;
        GrappleFires += o.GrappleFires; GrappleHolds += o.GrappleHolds; GrappleStalled += o.GrappleStalled;
        GrappleBreaks += o.GrappleBreaks; GrappledTimes += o.GrappledTimes; StallGrappled += o.StallGrappled;
        ShamePicks += o.ShamePicks; ShameFires += o.ShameFires; ShameCowed += o.ShameCowed;
        ShameBlocked += o.ShameBlocked; CowedLost += o.CowedLost; StallCowed += o.StallCowed;
        FootingSteps += o.FootingSteps; FootingFull += o.FootingFull; FootingSaved += o.FootingSaved;
        ShieldTakes += o.ShieldTakes; ShieldTaken += o.ShieldTaken; ShieldCovered += o.ShieldCovered;
        PlantedRefused += o.PlantedRefused; FootingLayerSum += o.FootingLayerSum; FootingLayerTurns += o.FootingLayerTurns;
        PlantedRefusedFoe += o.PlantedRefusedFoe; ShieldHalved += o.ShieldHalved; FootingHitSteps += o.FootingHitSteps;
        DeflectHits += o.DeflectHits; DeflectMoved += o.DeflectMoved; DeflectLanded += o.DeflectLanded;
        DeflectVulnAdded += o.DeflectVulnAdded; DeflectKills += o.DeflectKills; DeflectNoTarget += o.DeflectNoTarget;
        DeflectBeckonStack += o.DeflectBeckonStack; DeflectKeptTaken += o.DeflectKeptTaken;   // 第186期
        DeflectSelf += o.DeflectSelf; DeflectExecFeed += o.DeflectExecFeed;
        DeflectToPointed += o.DeflectToPointed; DeflectToOtherMarked += o.DeflectToOtherMarked; DeflectToFallback += o.DeflectToFallback;
        ThrustSwings += o.ThrustSwings; ThrustChargeSum += o.ThrustChargeSum; ThrustChargeMax = Math.Max(ThrustChargeMax, o.ThrustChargeMax);
        ThrustAtkSum += o.ThrustAtkSum; ThrustAtkMax = Math.Max(ThrustAtkMax, o.ThrustAtkMax); ThrustForced += o.ThrustForced;
        if (o.ThrustAtks is not null) (ThrustAtks ??= new List<int>()).AddRange(o.ThrustAtks);
        if (o.FootingFullAt > 0 && (FootingFullAt == 0 || o.FootingFullAt < FootingFullAt)) FootingFullAt = o.FootingFullAt;
        ShuffleAllySwaps += o.ShuffleAllySwaps; ShuffleFoeSwaps += o.ShuffleFoeSwaps;
        ShuffleAllyRefused += o.ShuffleAllyRefused;
        ShuffleAdvanced += o.ShuffleAdvanced; ShuffleAdvancedTraited += o.ShuffleAdvancedTraited;
        ShuffleAdvancedFromBack += o.ShuffleAdvancedFromBack; ShuffleStaggers += o.ShuffleStaggers;
        ShuffleConfuses += o.ShuffleConfuses;
        GustSwings += o.GustSwings; GustPrimary += o.GustPrimary; GustSplash += o.GustSplash;
        GustFell += o.GustFell; GustFellHere += o.GustFellHere;
        ShuffleNoFoePair += o.ShuffleNoFoePair;
        ConfusedMarks += o.ConfusedMarks; ConfusedSwings += o.ConfusedSwings;
        ConfusedGuards += o.ConfusedGuards; ConfusedKills += o.ConfusedKills;
        ConfusedExecGain += o.ConfusedExecGain;
        Attacks += o.Attacks; Interventions += o.Interventions;
        DamageToEnemy += o.DamageToEnemy; DamageToAlly += o.DamageToAlly;
        DamageTaken += o.DamageTaken; TakenFromAlly += o.TakenFromAlly;
        Healed += o.Healed;
        Charges += o.Charges; BigAttacks += o.BigAttacks;
        Swallowed += o.Swallowed; Slumbers += o.Slumbers;
        Intercepts += o.Intercepts; Shouldered += o.Shouldered;
        // 第135期。回数・量は単純加算。配列は遅延確保（`CarryAmount` と同じ作法）。
        GuardChances += o.GuardChances; GuardRangeMissed += o.GuardRangeMissed;
        RedirectGainFires += o.RedirectGainFires; RedirectGain += o.RedirectGain;
        StoicHealBlocked += o.StoicHealBlocked; StoicHealBlockedFires += o.StoicHealBlockedFires;
        StoicSupportHops += o.StoicSupportHops; StoicSupportHeads += o.StoicSupportHeads;
        if (o.InterceptsByLabel is not null)
        {
            int[] mine = InterceptsByLabel ??= new int[o.InterceptsByLabel.Length];
            for (int i = 0; i < mine.Length; i++) mine[i] += o.InterceptsByLabel[i];
        }
        MergeHarm(ref HarmAmount, o.HarmAmount);
        MergeHarm(ref HarmHits, o.HarmHits);
        MergeHarm(ref HarmGuardAmount, o.HarmGuardAmount);
        MergeHarm(ref HarmGuardHits, o.HarmGuardHits);
        MergeHarm(ref HarmFatal, o.HarmFatal);
        ParryFires += o.ParryFires; ParryBlocked += o.ParryBlocked;
        ParryBlockedMax = Math.Max(ParryBlockedMax, o.ParryBlockedMax);
        MergeHarm(ref ParryByRoute, o.ParryByRoute);
        ParryStances += o.ParryStances; ParryRefillTurn += o.ParryRefillTurn;
        ParryRefillGuard += o.ParryRefillGuard; ParryRefillGuardWasted += o.ParryRefillGuardWasted;
        ParryStockAtDeath += o.ParryStockAtDeath;
        ParrySwings += o.ParrySwings; ParrySwingsFull += o.ParrySwingsFull;
        SniperSwings += o.SniperSwings;
        WildfireSwings += o.WildfireSwings; WildfireLit += o.WildfireLit;
        WildfireFoes += o.WildfireFoes; WildfireFoesSq += o.WildfireFoesSq;
        WildfireGain += o.WildfireGain; WildfireGainSq += o.WildfireGainSq;
        if (o.WildfireFoesMax > WildfireFoesMax) WildfireFoesMax = o.WildfireFoesMax;
        Refunds += o.Refunds; Refunded += o.Refunded;
        Kills += o.Kills; Deaths += o.Deaths;
        Whetted += o.Whetted; Dulled += o.Dulled;
        BurnLit += o.BurnLit; BurnRelit += o.BurnRelit; BurnLitAlly += o.BurnLitAlly;
        ExtraSwings += o.ExtraSwings;   // 第178期（計数専用）
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

    /// <summary>害の帳簿の配列を足し込む（第135期）。<b>相手が確保していなければ何もしない。</b></summary>
    static void MergeHarm(ref int[]? mine, int[]? other)
    {
        if (other is null) return;
        mine ??= new int[other.Length];
        for (int i = 0; i < mine.Length; i++) mine[i] += other[i];
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
    /// 状態異常が<b>付いた瞬間</b>（第97期・<b>表示専用</b>）。<see cref="StatusSnapshot"/> が
    /// 「ターン頭にいくつ乗っているか」を写すのに対し、こちらは「いま書かれた」という出来事。
    ///
    /// <para><b>engine の窓口を持つ4通貨だけが出す</b>——傷（<c>BattleContext.Wound</c>）・
    /// 毒（<c>BattleContext.Poison</c>）・燃焼（<c>BattleContext.Ignite</c>）・
    /// なまり（<c>BattleContext.Dull</c>）。痺れ・標・破片は窓口が無いので出さない
    /// （<see cref="StatusKeys"/> のカウンタは16箇所から直に書かれていて、
    /// そこへ通知を挟むと Traits.cs を広く触ることになる）。</para>
    ///
    /// <para><c>ActorId</c> = 書き手（engine の規則が足したぶんは null）、
    /// <c>Amount</c> = 付いた量、<c>Text</c> = <see cref="StatusKeys"/> のキー名。
    /// 組み付き（<see cref="StatusKeys.Grappled"/>）は開始が1、解除が0。
    /// 解除も ActorId = 拘束していた駒、TargetId = 拘束されていた駒を保つ。
    /// <b>盤面には一切影響しない。</b></para>
    /// </summary>
    StatusGain,

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
    Skill,

    /// <summary>
    /// 介入が主目標を差し替えた（第125期・<b>表示専用</b>）。
    /// <c>SelectTargetChain</c> の全段（標的・後備え・庇う・殉教・棘守り）が1件ずつ出す。
    ///
    /// <para><b>台本に無かった唯一の「手番の外」だった</b>——割り込み（棘・仇討ち・軋み）は
    /// <see cref="BattleEvent.Reaction"/>、肩代わり（巨躯・分かち）は
    /// <see cref="BattleEvent.Relayed"/> で既に立っていたが、<b>庇いは
    /// <c>Log</c> の文字列にしか書かれていなかった</b>（第123期 (iii) ／ 第124期 Q0-3）。
    /// 駒は1ミリも動かず被弾者が入れ替わるだけなので、画面では「ガルドが殴られた」としか見えない。</para>
    ///
    /// <para><c>ActorId</c> = <b>割り込んだ駒</b>、<c>TargetId</c> = <b>本来の標的</b>、
    /// <c>Text</c> = どの段か（<see cref="InterceptLabels"/>）。
    /// <b>どの規則も読まない。</b> <see cref="Reaction"/> / <see cref="BattleEvent.Relayed"/> と
    /// 同じ表示専用の札で、盤面には一切影響しない。</para>
    /// </summary>
    Intercept,

    /// <summary>
    /// 一撃を受け流した（表示専用）。ActorId = 攻撃者、TargetId = 受け流した駒、
    /// Amount = 無効化量、HpAfter = 変化していないHP。Damage は発生しない。
    /// </summary>
    Parry,

    /// <summary>
    /// 転倒（第145期・<b>表示専用</b>）。<see cref="StatusKeys.Stagger"/> が
    /// <b>付いた瞬間</b>と<b>手番を失った瞬間</b>の2本を、<c>Text</c>
    /// （<see cref="StaggerLabels"/>）で区別して出す。
    ///
    /// <para><b>種類を1つにまとめたのは、2つが別のターンになりうるから</b>——
    /// 付与はターン頭（<c>ShufflerTrait.OnTurnStart</c>）、消費は行動順ループの中
    /// （<c>TakeTurnCore</c>）で、片方だけでは「何が起きたか」か「なぜ動かないか」の
    /// どちらかが欠ける。</para>
    ///
    /// <para><b><see cref="StatusSnapshot"/> では代用できない。</b> スナップショットは
    /// ターン頭の <c>OnTurnStart</c> より<b>前</b>に撮るので、同じターンのうちに
    /// 立って消える転倒は<b>どの写しにも一度も載らない</b>——
    /// 「<c>StatusKeys.All</c> に入っていて記号も定義済みなのに画面に出ない」の正体がこれ。</para>
    ///
    /// <para><b><see cref="StatusGain"/> には載せない</b>——あちらは
    /// 「engine の窓口を持つ4通貨だけが出す」を明文で持っており、転倒に窓口は無い。</para>
    ///
    /// <para><c>ActorId</c> = 転ばせた駒（消費側は null）、<c>TargetId</c> = 転んだ駒、
    /// <c>Text</c> = <see cref="StaggerLabels"/>。
    /// <b>どの規則も読まない。</b> <see cref="Intercept"/> / <see cref="Parry"/> と同じ
    /// 表示専用の札で、盤面には一切影響しない。</para>
    /// </summary>
    Stagger,

    /// <summary>
    /// 痺れ（第146期 段0・<b>表示専用</b>）。<see cref="StatusKeys.Stun"/> が
    /// <b>付いた瞬間</b>と<b>手番を失った瞬間</b>の2本を、<c>Text</c>
    /// （<see cref="StunLabels"/>）で区別して出す。<see cref="Stagger"/> と同じ形。
    ///
    /// <para><b>種類を1つにまとめたのは、2つが別のターンになりうるから</b>——
    /// 付与は攻撃の巻き添え・断罪・縛めなどその場で、消費は<b>次の</b>行動順ループの
    /// <c>TakeTurnCore</c>。片方だけでは「何が起きたか」か「なぜ動かないか」の
    /// どちらかが欠ける（転倒とまったく同じ理由）。</para>
    ///
    /// <para><b>転倒と違い <see cref="StatusSnapshot"/> には載る</b>——痺れはターンを
    /// またいで残るので写しに出る。欠けているのは<b>瞬間</b>のほうで、
    /// 「いつ誰に付けられたか」と「その手番が実際に潰れたか」が画面から引けなかった
    /// （第125期「手番が潰れたターンは台本に1件も残らない」）。</para>
    ///
    /// <para><b><see cref="StatusGain"/> には載せない</b>——あちらは
    /// 「engine の窓口を持つ4通貨だけが出す」を明文で持っており、痺れに窓口は無い。
    /// <b>打ち口が <c>NoteStatusGain</c> なのは、そこが <c>SetCounter</c> から来る
    /// 唯一の合流点だから</b>であって、窓口を1本足したわけではない
    /// （盤面を書く関数ではなく、書かれたのを数える関数である）。</para>
    ///
    /// <para><c>ActorId</c> = 痺れさせた駒（<c>Mark.Owner</c>。engine 由来と消費側は null）、
    /// <c>TargetId</c> = 痺れた駒、<c>Text</c> = <see cref="StunLabels"/>。
    /// <b>どの規則も読まない。</b></para>
    /// </summary>
    Stun,

    /// <summary>
    /// 混乱（第147期・<b>表示専用</b>）。<see cref="StatusKeys.Confused"/> が
    /// <b>付いた瞬間</b>と<b>自軍へ振った瞬間</b>の2本を、<c>Text</c>
    /// （<see cref="ConfusedLabels"/>）で区別して出す。<see cref="Stagger"/> / <see cref="Stun"/> と同じ形。
    ///
    /// <para><b>2本とも要る理由は転倒・痺れとは違う。</b> あちらは「何が起きたか」と
    /// 「なぜ動かないか」だったが、混乱は<b>振る</b>ので <see cref="Attack"/> は出る
    /// ——出ないのは<b>「なぜ味方を殴ったのか」</b>のほうである。付与だけだと
    /// 何ターンも後の同士討ちと結び付かず、発動だけだと理由が画面に無い。</para>
    ///
    /// <para><b>名前をキーの定数名（<c>Confused</c>）に揃えてある。</b> 盲点表
    /// （<c>watch phase0</c>）の「専用の種類」の列が <see cref="StatusKeys"/> の定数名と
    /// この列挙の名前を<b>機械で照合する</b>ので、揃えないと走査が静かに × を出す（第117・123期）。</para>
    ///
    /// <para><b><see cref="StatusGain"/> には載せない</b>——あちらは
    /// 「engine の窓口を持つ4通貨だけが出す」を明文で持っており、混乱に窓口は無い。
    /// 打ち口が <c>NoteStatusGain</c> なのは、そこが <c>SetCounter</c> から来る
    /// 唯一の合流点だからであって、窓口を1本足したわけではない（痺れと同じ判断）。
    /// <b>書き手が2つある</b>（喧噪の <c>ShufflerTrait</c> と、波ルール版の <c>SwapSlots</c>）ので、
    /// 呼び口を1箇所に寄せる利得は痺れのときより大きい。</para>
    ///
    /// <para><c>ActorId</c> = 混乱させた駒（<c>Mark.Owner</c>。発動側は null）、
    /// <c>TargetId</c> = 混乱した駒、<c>Text</c> = <see cref="ConfusedLabels"/>。
    /// <b>どの規則も読まない。</b></para>
    /// </summary>
    Confused,

    /// <summary>
    /// 盤面ルールが何かを<b>封じた瞬間</b>（第171期・<b>表示専用</b>）。粛・渇き・軛の3本を、
    /// <c>Text</c>（<see cref="SealedLabels"/>）で区別して1つの種類にまとめてある。
    ///
    /// <para><b><see cref="Highlight"/> に乗せなかった</b>——あちらは見せ場（破裂・覚醒）の
    /// 差し込み位置で、再生側は文字列をそのままバナーに出す。封じを混ぜると
    /// <b>再生側が封じを他の強調と区別できない</b>（第171期 Q0-1 の判断基準がこれ1つ）。</para>
    ///
    /// <para><b>3本を1つの種類にまとめたのは、3つとも「盤面ルールが働いた」という同じ出来事だから。</b>
    /// 転倒・痺れ・混乱が「付与」と「発動」の2本を1種類に持つのと同じで、
    /// 種類を分けると再生側が同じ形の分岐を3つ持つことになる。</para>
    ///
    /// <para><b>engine で足したのは3箇所だけ</b>——<c>NoteHushBlocked</c>（粛が<b>単独の原因</b>で
    /// 止めたときだけ）・<c>NoteDroughtBlocked</c>（回復の入口）・<c>ApplyDamage</c> の
    /// <c>yokeBinding</c> の中。どれも既にある計数の合流点で、<b>判定・数値・乱数の消費には触らない。</b></para>
    ///
    /// <para><c>ActorId</c> = 封じられた行動の<b>相手側</b>（軛なら殴った駒。粛・渇きは null）、
    /// <c>TargetId</c> = 封じられた駒、<c>Amount</c> = 通らなかった量
    /// （渇き＝入るはずだった回復／軛＝切り落とされた量／粛＝0）、
    /// <c>Text</c> = <see cref="SealedLabels"/>。<b>どの規則も読まない。</b></para>
    /// </summary>
    Sealed,

    /// <summary>
    /// 状態を<b>取り上げた瞬間</b>（第183期 追補3・<b>表示専用</b>）。まず澱み喰いのヴィオの吸い上げで出す。
    ///
    /// <para><b>吸われた味方1体につき1件</b>。同じ吸い上げの一連は <c>DrainSeq</c> が同じ値で、
    /// 一連の最後の1件だけ <c>DrainLast</c> が真になり、<c>AttackAfter</c> に吸い上げ後の攻撃力が載る。</para>
    ///
    /// <para><c>ActorId</c> = 吸った駒、<c>TargetId</c> = 吸われた駒、<c>Text</c> = 状態のキー（<see cref="StatusKeys"/>）、
    /// <c>Amount</c> = 吸った量、<c>StatusRemaining</c> = 吸った後にその駒に残った量。
    /// <b>どの規則も読まない。</b> <c>verbose</c> のときしか積まない。</para>
    /// </summary>
    StatusDrain,

    /// <summary>
    /// 竦み（<see cref="StatusKeys.Cowed"/>）を<b>消費した瞬間</b>（第185期 追補3・<b>表示専用</b>）。
    /// 付いた瞬間は <see cref="StatusGain"/>（<c>Text = "cowed"</c>・<c>SpreadFromId</c> = 悲鳴の出どころ）の側。
    ///
    /// <para><c>TargetId</c> = 竦みを消費した駒、<c>ActorId</c> = 竦ませたシガ、<c>SpreadFromId</c> = 悲鳴の出どころ
    /// （付いたときの <c>StatusGain</c> と同じ値）、<c>Text</c> = <see cref="CowedLabels"/>
    /// ——<see cref="CowedLabels.Lost"/>（竦みで手番を失った）／<see cref="CowedLabels.Absorbed"/>
    /// （痺れ・転倒・組み付きで失う手番に竦みが吸われた。二重には取らない）。
    /// <b>どの規則も読まない。</b> <c>verbose</c> のときしか積まない。末尾に足したので既存の種類の番号は動かない。</para>
    /// </summary>
    Cowed,

    /// <summary>
    /// 攻撃力の強化が<b>乗った瞬間</b>（号令の台本・<b>表示専用</b>）。<c>BattleContext.Whet</c> の出口で、
    /// 実際に <c>AtkBonus</c> へ積んだときだけ1件出す。
    ///
    /// <para><c>ActorId</c> = 書き手（第94期 (T2) の印 <c>Mark.Owner</c>。号令ならガン）、
    /// <c>TargetId</c> = <b>実際に受け取った駒</b>、<c>IntendedId</c> = <b>本来の対象</b>
    /// （支援拒否のガルドや横流しで差し替わったときだけ <c>TargetId</c> と違う）、
    /// <c>Amount</c> = 量、<c>WhetRoute</c> = 経路、<c>SourceTrait</c> = 書き手の札、
    /// <c>AttackAfter</c> = 乗った後の受け手の攻撃力。</para>
    ///
    /// <para><b>支援拒否で隣へ配ったときは受け取った駒ごとに1件</b>で、同じ一連は <c>SupportSeq</c> が同じ値、
    /// 一連の最後の1件だけ <c>SupportLast</c> が真。<b>どの規則も読まない。</b></para>
    /// </summary>
    Whet,

    /// <summary>
    /// 支援拒否（<see cref="TraitId.Stoic"/>）が回復を<b>弾いた瞬間</b>（<b>表示専用</b>）。
    /// <c>BattleContext.Heal</c> の入口。弾いた回復は隣へ流れない。
    ///
    /// <para><c>ActorId</c> = 回復の出どころの駒（<c>by</c> か <c>Mark.Owner</c>。無ければ null）、
    /// <c>TargetId</c> = 弾いた駒、<c>Amount</c> = 弾いた量、<c>SourceTrait</c> = 回復の出どころの札。
    /// <b>どの規則も読まない。</b></para>
    /// </summary>
    HealBlocked,

    /// <summary>
    /// 叩き起こし（<see cref="TraitId.Reveille"/>・鬨の号令ガン）の瞬間（<b>表示専用</b>）。
    ///
    /// <para><c>ActorId</c> = 起こした駒（ガン）、<c>TargetId</c> = 起こされた味方、
    /// <c>Text</c> = <see cref="ReveilleLabels"/>——<see cref="ReveilleLabels.Woken"/>（起きて1回殴る。
    /// この直後に起こされた駒の <c>Attack</c> が <c>Reaction</c> 付きで並ぶ）／
    /// <see cref="ReveilleLabels.Hushed"/>（粛がいて誰も起こせなかった。<c>TargetId</c> は
    /// 粛が無ければ起こしていたはずの1体）／<see cref="ReveilleLabels.Held"/>（粛以外の理由で起こせなかった）。
    /// <b>どの規則も読まない。</b></para>
    /// </summary>
    Reveille,

    /// <summary>
    /// 反転の裏（第191期・<see cref="TraitId.InverseLeak"/>・毒喰らいのベニ）で回復がダメージに変わった瞬間（<b>表示専用</b>）。
    ///
    /// <para><b>直後に同じ駒への <c>Damage</c> が1件並ぶ</b>（出どころは回復させた駒）。この印が無いと、
    /// 再生側では「回復役が味方を殴った」と見分けが付かない。<c>ActorId</c> = 回復させた駒（無ければ null）、
    /// <c>TargetId</c> = 受けた駒、<c>Amount</c> = 回復の量（＝ダメージの名目）、<c>Text</c> = 裏の保持者の名前。
    /// <b>どの規則も読まない。</b></para>
    /// </summary>
    HealInverted,

    /// <summary>
    /// 啜り（第193期・<see cref="TraitId.Inverse"/>）で、隣の溢れた反転の回復がベニに流れ込む瞬間（<b>表示専用</b>）。
    /// <para><b>直後にベニへの <c>Heal</c>（ベニ → ベニ）が1件並ぶ</b>（ベニが満タンなら増えないので出ない）。
    /// <c>ActorId</c> = ベニ、<c>TargetId</c> = 溢れた隣の味方、<c>Amount</c> = 溢れの量（流し込む名目）。<b>どの規則も読まない。</b></para>
    /// </summary>
    InverseSip,

    /// <summary>
    /// 濃縮の印（第194期・<see cref="TraitId.Concentrate"/>・澱みのミオ）が +1 された瞬間（<b>表示専用</b>）。
    /// <para><c>ActorId</c> = ミオ、<c>TargetId</c> = 印が付いた駒、<c>Amount</c> = 足した後の印の数 n、
    /// <c>Text</c> = 「中心」「周り」（敵）／「漏れ」（ミオの隣の味方）。
    /// 印が n の駒は以後の刻みごとに <c>Status</c> → <c>Damage</c> の組が 1+n 組並ぶ。<b>どの規則も読まない。</b></para>
    /// </summary>
    ConcentrateMark,

    /// <summary>
    /// 濃縮（第194期・<see cref="TraitId.Amplifier"/>・澱みのミオ）で敵の毒の層が増えた瞬間（<b>表示専用</b>）。
    /// <para><c>ActorId</c> = ミオ、<c>TargetId</c> = 敵、<c>Amount</c> = 足した<b>後</b>の毒の層、
    /// <c>Text</c> = <see cref="ThickenLabels"/>（+4 層の濃縮／傷口への着火）、<c>SourceTrait</c> = <see cref="TraitId.Amplifier"/>。
    /// ミオの手番の <c>Skill</c> の後、印（<see cref="ConcentrateMark"/>）より前に並ぶ。<b>どの規則も読まない。</b></para>
    /// </summary>
    PoisonThicken,

    /// <summary>
    /// 紅蓮（第197期・<see cref="TraitId.Guren"/>・毒喰らいのベニ）が溜まった瞬間（<b>表示専用</b>）。
    /// <para><b>啜り（<see cref="InverseSip"/>）の直後</b>に並ぶ。<c>ActorId</c> = ベニ、<c>TargetId</c> = 溢れた隣の味方、
    /// <c>Amount</c> = 足した紅蓮、<c>StatusRemaining</c> = 足した<b>後</b>の紅蓮の量（アイコン用）。<b>どの規則も読まない。</b></para>
    /// </summary>
    GurenGain,

    /// <summary>
    /// 紅蓮を放った瞬間（第197期・<b>表示専用</b>）。ベニの手番の <c>Skill</c> の直後に並ぶ。
    /// <para><b>見出しの1件</b>: <c>TargetId</c> = null、<c>ActorId</c> = ベニ、<c>Amount</c> = 放った紅蓮の量、<c>Slot</c> = 敵の数。
    /// 続いて敵全員への着火（<c>StatusGain</c>・燃焼・書き手 ＝ ベニ）、そのあと<b>敵ごとの1件</b>（<c>TargetId</c> = 敵、
    /// <c>Amount</c> = 積んだ層・直撃の版は直撃の名目）と、その敵への毒の <c>StatusGain</c>（経路 <see cref="BattleCore.PoisonRoute.Guren"/>）
    /// ／直撃の版は <c>Damage</c> が並ぶ。等分が 0 なら敵ごとの件は出ない。<b>どの規則も読まない。</b></para>
    /// </summary>
    GurenRelease,

    /// <summary>
    /// 剣の段に入った瞬間（第198期・<see cref="TraitId.LastStand"/>・廃棄聖騎士ガルド・<b>表示専用</b>）。
    /// <para>最後の味方の <c>Death</c> の後に並ぶ。<c>ActorId</c> = <c>TargetId</c> = ガルド、<c>Amount</c> = 入った後の攻撃力、
    /// <c>HpAfter</c> = そのときの HP、<c>Slot</c> = ガルドの席、<c>SourceTrait</c> = 版（剣／返しなし／盾剣）。
    /// 以後のガルドの手番の <c>Attack</c> は剣の段の一撃（剣の版は薙ぎ）。<b>どの規則も読まない。</b></para>
    /// </summary>
    LastStand,

    /// <summary>
    /// 斬り返し（第198期・<b>表示専用</b>）。ガルドへの <c>Damage</c> の直後、斬り返しの <c>Damage</c> の直前に並ぶ。
    /// <para><c>ActorId</c> = ガルド、<c>TargetId</c> = 殴ってきた敵、<c>Amount</c> = 斬り返しの名目、<c>HpAfter</c> = ガルドの HP、
    /// <b><c>Slot</c> = 1 なら相打ち</b>（倒れる一撃への返し。このあとガルドの <c>Death</c> が並ぶ）、0 なら生きているうちの返し。<b>どの規則も読まない。</b></para>
    /// </summary>
    LastStandRiposte,

    /// <summary>
    /// 相打ちで勝った瞬間（第199期・<b>表示専用</b>）。相打ちの斬り返しで最後の敵が倒れた直後（その敵の <c>Death</c> の後、ガルドの <c>Death</c> の前）に並ぶ。
    /// <para><c>ActorId</c> = ガルド、<c>TargetId</c> = 最後に倒れた敵、<c>Team</c> = 勝った陣営。この戦は味方が全滅していても勝ち。<b>どの規則も読まない。</b></para>
    /// </summary>
    LastStandVictory,

    /// <summary>
    /// 施しのリリ（第204期・<see cref="TraitId.Kiss"/>・<b>表示専用</b>）。<c>Text</c> が <see cref="KissLabels"/> のどれかで、場面が分かれる。
    /// <para><b>吸い取り</b>（<see cref="KissLabels.Drain"/>）: <c>ActorId</c> = リリ、<c>TargetId</c> = 吸った敵、<c>Amount</c> = 吸う名目、<c>HpAfter</c> = 吸う<b>前</b>の HP（受け取る味方は吸った後に決まるので載らない）。
    /// 直前に聖痕の <c>StatusGain</c>（キー <c>stigma</c>・書き手 ＝ リリ）、直後に敵への <c>Damage</c>（出どころ ＝ リリ・型なし）が並ぶ。
    /// 続いて状態が移るなら <see cref="StatusTransfer"/> がキーごとに並び、最後に <b>口移し</b>（<see cref="KissLabels.Give"/>）:
    /// <c>TargetId</c> = 受け取った味方、<c>Amount</c> = 吸えた量、<c>SpreadFromId</c> = 吸った敵。その直後に <c>Heal</c>、溢れがあれば <b>破片</b>（<see cref="KissLabels.Armor"/>・<c>Amount</c> = 足した破片、<c>StatusRemaining</c> = 足した後の破片）。</para>
    /// <para><b>祝福の儀</b>: 見出し（<see cref="KissLabels.Rite"/>・<c>TargetId</c> = null・<c>Slot</c> = 聖痕の敵の数・<b>第206期から <c>Amount</c> = 吸う総量（名目）・<c>StatusRemaining</c> = 1体あたり（余りを足す前）</b>）→ 敵ごとの <see cref="KissLabels.RiteDrain"/>（<c>TargetId</c> = 敵）と <c>Damage</c>
    /// → 味方ごとの <see cref="KissLabels.RiteGive"/>（<c>TargetId</c> = 味方・<c>Amount</c> = 施した量）と <c>Heal</c>・破片。<b>儀式の最後の1件は <see cref="KissLabels.RiteEnd"/></b>（聖痕が消えた瞬間・<c>Amount</c> = 吸えた合計）。</para>
    /// <para><b>祝福が還る</b>（<see cref="KissLabels.Return"/>）: 聖痕の敵の <c>Death</c> の後。<c>TargetId</c> = 受け取った味方、<c>SpreadFromId</c> = 倒れた敵、<c>Amount</c> = 名目。直後に <c>Heal</c>・破片。</para>
    /// <c>HpAfter</c> は<b>その出来事を積んだ時点</b>の対象の HP（吸う前・与える前）で、バーの補間は直後の <c>Damage</c> / <c>Heal</c> で読む。<b>どの規則も読まない。</b>
    /// </summary>
    Kiss,

    /// <summary>
    /// 状態が移った瞬間（第204期・リリの口移しの代金・<b>表示専用</b>）。<c>ActorId</c> = リリ、<c>SpreadFromId</c> = 元の敵、<c>TargetId</c> = 受け取った味方、
    /// <c>Text</c> = 状態キー（<see cref="StatusKeys"/>）、<c>Amount</c> = 移した値（層・残りターン・0/1）、<c>StatusRemaining</c> = 受け取った後の値。<b>どの規則も読まない。</b>
    /// </summary>
    StatusTransfer,

    /// <summary>
    /// 継ぎ当てのツギの出来事（第207期・<b>表示専用</b>）。<c>Text</c> は <see cref="PlankLabels"/> で場面を分ける。<b>どの規則も読まない。</b>
    /// </summary>
    Plank
}

/// <summary><see cref="BattleEventKind.Plank"/> の <c>Text</c>（第207期・<b>表示専用</b>）。</summary>
public static class PlankLabels
{
    /// <summary>板を貼った。<c>ActorId</c> = ツギ、<c>TargetId</c> = 味方、<c>Amount</c> = 貼った破片の量、
    /// <c>Slot</c> = そのうち背中の在庫の分、<c>StatusRemaining</c> = 貼った後の味方の破片。</summary>
    public const string Paste = "板を貼った";
    /// <summary>瓦礫を拾った。<c>ActorId</c> = ツギ、<c>TargetId</c> = 出どころの駒（砕けた破片の持ち主／倒れた駒）、
    /// <c>SpreadFromId</c> = 同じ、<c>Slot</c> = 0 砕けた破片 ／ 1 倒れた駒、<c>Amount</c> = 拾った量（切り捨て）、<c>StatusRemaining</c> = 在庫の合計（切り捨て）。</summary>
    public const string Scrap = "瓦礫を拾った";
    /// <summary>板の印で燃焼が倍になった瞬間。<c>TargetId</c> = 燃えた味方、<c>Amount</c> = 倍にした後の残りターン、
    /// <c>Slot</c> = 0 <c>Ignite</c>（点く・点け直し）／ 1 リリの移し。<c>ActorId</c> は付けない（書き手は燃焼の書き手）。</summary>
    public const string Flare = "板が燃えた";
    /// <summary>第208期: 板の破片が殴った敵へ飛んだ。<c>ActorId</c> = 板を持つ味方、<c>TargetId</c> = 殴った敵、<c>Amount</c> = 返した量、
    /// <c>Slot</c> = 攻撃の通し番号（同じ敵の1回の攻撃で返った反射は同じ番号・攻撃の外なら 0）。直後に敵への <c>Damage</c> が並ぶ。
    /// <para>第209期: <c>Amount</c> ＝ 失った破片 ＋ 残りの厚さの分。<c>StatusRemaining</c> ＝ <b>残りの厚さの分</b>（倍率 0 なら 0）、
    /// <c>Amount − StatusRemaining</c> ＝ <b>失った破片の分</b>、<c>HpAfter</c> ＝ 反射の瞬間に板を持つ味方に残っていた破片。</para></summary>
    public const string Reflect = "板の破片が飛んだ";
    /// <summary>第210期: 応急処置（手番の外で駆け込んで貼った）。欄は <see cref="Paste"/> と同じ
    /// ——<c>ActorId</c> = ツギ、<c>TargetId</c> = 味方、<c>Amount</c> = 貼った量、<c>Slot</c> = 在庫の分、<c>PlankBase</c> = 基本の分、<c>PlankSkill</c> = 腕の分、
    /// <c>StatusRemaining</c> = 貼った後の味方の破片、<c>HpAfter</c> = 味方の HP（倒れかけの深さ）。</summary>
    public const string FirstAid = "応急処置";
    /// <summary>第210期: 腕が上がった瞬間。<c>ActorId</c> = <c>TargetId</c> = ツギ、<c>Slot</c> = 新しい段、<c>Amount</c> = 砕かれた累計。</summary>
    public const string Skill = "腕が上がった";
}

/// <summary><see cref="BattleEventKind.Kiss"/> の <c>Text</c>（第204期・<b>表示専用</b>）。</summary>
public static class KissLabels
{
    public const string Drain = "吸い取り";
    public const string Give = "口移し";
    public const string Armor = "破片";
    public const string Rite = "祝福の儀";
    public const string RiteDrain = "儀式・吸う";
    public const string RiteGive = "儀式・施す";
    public const string RiteEnd = "儀式・終わり";
    public const string Return = "祝福が還る";
    /// <summary>第205期（痛みの版）: 手番の頭の痛み。<c>Amount</c> = 測った痛み、<c>StatusRemaining</c> = そこから決まった1体あたりの吸う量、<c>Slot</c> = この手番に吸う体数（段 ＋ 1。儀式の手番は聖痕の敵の数）。</summary>
    public const string Pain = "痛み";
    /// <summary>第205期（段の版）: 段が上がった瞬間。<c>Slot</c> = 新しい段、<c>Amount</c> = 1手番に吸う体数、<c>StatusRemaining</c> = 施した累計。</summary>
    public const string Tier = "段";
    /// <summary>第205期（捨てる版）: 溢れて捨てた量。<c>TargetId</c> = 受け取った味方、<c>Amount</c> = 捨てた量（<see cref="Armor"/> の代わりに並ぶ）。</summary>
    public const string Waste = "溢れ";
    /// <summary>第205期（強弱を移す版）: 攻撃力の上げ下げが移った瞬間。<c>SpreadFromId</c> = 元の敵、<c>TargetId</c> = 受け取った味方、
    /// <c>Amount</c> = 移した値（<b>符号つき</b>・負が弱体）、<c>StatusRemaining</c> = 受け取った後の味方の値。<see cref="StatusTransfer"/> の後、口移しの前に並ぶ。</summary>
    public const string Steal = "強弱";
}

/// <summary><see cref="BattleEventKind.PoisonThicken"/> の <c>Text</c>（<b>表示専用</b>）。</summary>
public static class ThickenLabels
{
    /// <summary>毒のある敵の層を +4 した。</summary>
    public const string Thicken = "濃縮";
    /// <summary>毒の無い敵の傷口に毒を1層流し込んだ（<see cref="IgniteRule"/>・既定では走らない）。</summary>
    public const string Ignite = "着火";
}

/// <summary><see cref="BattleEventKind.Reveille"/> の <c>Text</c>（<b>表示専用</b>）。</summary>
public static class ReveilleLabels
{
    /// <summary>叩き起こした（起こされた駒がこの直後に1回殴る）。</summary>
    public const string Woken = "起床";

    /// <summary>粛がいて、起こそうとした味方がターン外に動けなかった。</summary>
    public const string Hushed = "粛";

    /// <summary>粛以外（痺れ・組み付きなど）で、起こそうとした味方が動けなかった。</summary>
    public const string Held = "封じ";

    public static readonly string[] All = { Woken, Hushed, Held };
}

/// <summary>
/// 封じの3本の名前（第171期・<b>表示専用</b>）。<see cref="BattleEventKind.Sealed"/> の
/// <c>Text</c> に入る文字列はこの3つで全部。
///
/// <para><see cref="StaggerLabels"/> / <see cref="StunLabels"/> / <see cref="ConfusedLabels"/> と
/// 同じく定数で持つ——文字列リテラルを直に書くと、走査が「該当なし」と「引けなかった」を
/// 区別できない（第117期）。</para>
/// </summary>
public static class SealedLabels
{
    /// <summary>粛がターン外の行動を止めた（<b>粛が単独の原因のときだけ</b>）。</summary>
    public const string Hush = "粛";

    /// <summary>渇きが回復を止めた。</summary>
    public const string Drought = "渇き";

    /// <summary>軛が1発を上限で切った。</summary>
    public const string Yoke = "軛";

    /// <summary>3つとも。<b>粛 → 渇き → 軛</b>の順（<c>BoardRuleLedger.RuleIndex</c> とは別の並び）。</summary>
    public static readonly string[] All = { Hush, Drought, Yoke };
}

/// <summary>
/// 混乱の2本の名前（第147期・<b>表示専用</b>）。<see cref="BattleEventKind.Confused"/> の
/// <c>Text</c> に入る文字列はこの2つで全部。
///
/// <para><see cref="StaggerLabels"/> / <see cref="StunLabels"/> と同じく定数で持つ——文字列リテラルを
/// 直に書くと、走査が「該当なし」と「引けなかった」を区別できない（第117期）。</para>
/// </summary>
public static class ConfusedLabels
{
    /// <summary>正気を失った（<see cref="StatusKeys.Confused"/> が立った）。</summary>
    public const string Lost = "錯乱";

    /// <summary>混乱したまま自軍へ振った。<c>PerformAttack</c> の出口。</summary>
    public const string Struck = "同士討ち";

    /// <summary>両方。<b>付与 → 発動</b>の順で持つ。</summary>
    public static readonly string[] All = { Lost, Struck };
}

/// <summary>
/// 痺れの2本の名前（第146期 段0・<b>表示専用</b>）。<see cref="BattleEventKind.Stun"/> の
/// <c>Text</c> に入る文字列はこの2つで全部。
///
/// <para><see cref="StaggerLabels"/> と同じく定数で持つ——文字列リテラルを直に書くと、
/// 走査が「該当なし」と「引けなかった」を区別できない（第117期）。</para>
/// </summary>
public static class StunLabels
{
    /// <summary>痺れた（<see cref="StatusKeys.Stun"/> が立った）。</summary>
    public const string Struck = "痺れ";

    /// <summary>痺れたまま手番を失った。行動順ループの中。</summary>
    public const string Lost = "手番喪失";

    /// <summary>両方。<b>付与 → 消費</b>の順で持つ。</summary>
    public static readonly string[] All = { Struck, Lost };
}

/// <summary>
/// 転倒の2本の名前（第145期・<b>表示専用</b>）。<see cref="BattleEventKind.Stagger"/> の
/// <c>Text</c> に入る文字列はこの2つで全部。
///
/// <para><see cref="InterceptLabels"/> と同じく定数で持つ——文字列リテラルを直に書くと、
/// 走査が「該当なし」と「引けなかった」を区別できない（第117期）。</para>
/// </summary>
/// <summary><see cref="BattleEventKind.Cowed"/> の <c>Text</c>（第185期 追補3・表示専用）。</summary>
public static class CowedLabels
{
    /// <summary>竦みで手番を失った（行動順ループの中・竦み自身が理由）。</summary>
    public const string Lost = "手番喪失";

    /// <summary>痺れ・転倒・組み付きで失う手番に、竦みが一緒に吸われた（竦みの分はもう1手番を取らない）。</summary>
    public const string Absorbed = "吸収";

    /// <summary>両方。</summary>
    public static readonly string[] All = { Lost, Absorbed };
}

public static class StaggerLabels
{
    /// <summary>転んだ（<see cref="StatusKeys.Stagger"/> が立った）。ターン頭。</summary>
    public const string Fell = "転倒";

    /// <summary>転んだまま手番を失った。行動順ループの中。</summary>
    public const string Lost = "手番喪失";

    /// <summary>両方。<b>付与 → 消費</b>の順で持つ。</summary>
    public static readonly string[] All = { Fell, Lost };
}

/// <summary>
/// 介入の段の名前（第125期・<b>表示専用</b>）。<see cref="BattleEventKind.Intercept"/> の
/// <c>Text</c> に入る文字列はこの5つで全部。
///
/// <para><b>定数で持つのは、段の数を機械で数えられるようにするため</b>——受け入れ条件 A3 は
/// 「<c>SelectTargetChain</c> が列挙した段の数」と「実際に出したイベントの種類数」の一致で、
/// 文字列リテラルを直に書くと走査が「該当なし」と「引けなかった」を区別できない（第117期）。</para>
/// </summary>
public static class InterceptLabels
{
    /// <summary>標（<c>StatusKeys.Marked</c>）が主目標を引いた。鎖の1段目。</summary>
    public const string Mark = "標的";
    /// <summary>後備え（セッキ）。<b>範囲攻撃にも割り込む</b>ので呼び出し口が2つある。</summary>
    public const string RearGuard = "後備え";
    /// <summary>庇う（ガルド）。</summary>
    public const string Guardian = "庇う";
    /// <summary>殉教（敵の殉教者）。庇うと挙動は1行も違わない（割合だけ別）。</summary>
    public const string Martyr = "殉教";
    /// <summary>棘守り（カド）。鎖の最後。</summary>
    public const string ThornGuard = "棘守り";

    /// <summary>
    /// 範囲の盾（バン・第185期 追補2・<b>表示専用</b>）。<b>鎖の段ではない</b>——標的選択を差し替えるのではなく、
    /// 範囲の一撃を配る直前に受け手を差し替える（<c>BattleContext.ShieldRecv</c>）。だから <see cref="All"/> には入れない
    /// （<c>All</c> は鎖の段の一覧で、害の帳簿の段別の添字と `offturn check` の段の数に使われている）。
    /// <c>ActorId</c> = バン ／ <c>TargetId</c> = 守られた味方 ／ <c>Amount</c> = 受け止めた量（半分にした後・層の軽減の前）。
    /// </summary>
    public const string RangeShield = "範囲の盾";

    /// <summary>全段。<b>鎖の並び順</b>（標的 → 後備え → 庇う → 殉教 → 棘守り）で持つ。</summary>
    public static readonly string[] All = { Mark, RearGuard, Guardian, Martyr, ThornGuard };
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

    /// <summary>
    /// Attack のときに実際に使われたパターン。
    ///
    /// <para><b>Damage にも載せてある</b>（第97期・表示専用）——線を描くのは Damage の側なので、
    /// Attack にしか無いと薙ぎと単体が同じ1本線になる。<b>攻撃型が分かる経路だけ</b>が入り、
    /// 反撃・状態異常の刻み・肩代わりの中継・呪いの共有は <c>null</c> のまま
    /// （「型なし」として描く）。</para>
    /// </summary>
    public AttackPattern? Pattern { get; init; }

    /// <summary>
    /// この出来事が<b>ターン外の反応の中</b>で起きたか
    /// （第97期・<b>表示専用</b>。<c>ctx.InReaction || ctx.InInterrupt</c> の写し）。
    ///
    /// <para>反撃は被弾側から刃側へ向かうので、<b>線の向きが手番の攻撃と逆になる</b>。
    /// 再生側はこの札で攻撃前に割り込みを予告し、線と拍も通常攻撃から描き分ける。
    /// <b>どの規則も読まない。</b></para>
    ///
    /// <para><b>立つのは <c>ctx.Reaction</c> / <c>ctx.Interrupt</c> に包まれた段だけ</b>
    /// ——棘・仇討ち（<c>Reaction</c>）と軋み（<c>Interrupt</c>）。
    /// <b>追い打ち（ハギ）は立たない</b>：<c>OnAnyDeath</c> から <c>ctx.PerformAttack</c> を
    /// 直に呼ぶので、台本の上では普通の一振りと区別が付かない
    /// （<c>CanActOutOfTurn</c> は通るのでターン外ではある）。</para>
    /// </summary>
    public bool Reaction { get; init; }

    /// <summary>
    /// 呪いの受け渡し（第182期・<see cref="CurseRule"/>）で出た <c>Damage</c> のときだけ、
    /// <b>痛みの出どころ ＝ 元の一撃を受けた呪い持ち</b>の InstanceId が入る（<b>表示専用</b>）。
    /// それ以外の出来事では <c>null</c>——<b>非 null であること自体が「受け渡し」の目印</b>。
    /// <para><c>ActorId</c> は元の攻撃者のまま（与害の帳簿と同じ）。線は
    /// <c>ShareFromId</c> → <c>TargetId</c> に引く。<b>どの規則も読まない。</b></para>
    /// </summary>
    public int? ShareFromId { get; init; }

    /// <summary>
    /// 逸らし（第186期・<see cref="DeflectTrait"/>）で出た <c>Damage</c> のときだけ、
    /// <b>逸らした駒（ソラ）</b>の InstanceId が入る（<b>表示専用</b>）。それ以外は <c>null</c>。
    /// <para><c>ActorId</c> は元の攻撃者のまま（同士討ちとして数える）。線は
    /// <c>DeflectFromId</c> → <c>TargetId</c> に引く。<b>どの規則も読まない。</b>
    /// <c>ShareFromId</c>（呪い）とは別の欄にしてある——再生側が呪いの演出に使っているため。</para>
    /// </summary>
    public int? DeflectFromId { get; init; }

    /// <summary>
    /// 突き（第186期 追補・<see cref="ThrustTrait"/>）の溜めの段（<b>表示専用</b>）。<b>どの規則も読まない。</b>
    /// <list type="bullet">
    /// <item>逸らしの受け渡しの <c>Damage</c>（<see cref="DeflectFromId"/> が非 null）: この逸らしで積んだ<b>後</b>の累計回数
    /// ＝何段目か（1 始まり）。逸らした駒が突きを持たないときは <c>null</c>。</item>
    /// <item>突きの <c>Attack</c>（型は貫き）: <b>この突きに乗った回数</b>（0 もありうる。突いた直後に 0 へ戻る）。
    /// 突きの保持者以外の <c>Attack</c> では <c>null</c>。</item>
    /// </list>
    /// <para>逸らしの元の一撃の攻撃者は、逸らしの <c>Damage</c> の <c>ActorId</c> にそのまま入っている（元の攻撃者のまま）。</para>
    /// </summary>
    public int? ThrustCharge { get; init; }

    /// <summary>
    /// 第202期・<b>表示専用</b>。規則で選ぶ陣形（パターン2）の貫きが抜けた経路（0 ＝ 1-2 ／ 1 ＝ 2-3）。
    /// X 字の貫き・突き以外の攻撃では null。<b>読んで分岐する規則は 0 件</b>（自己検査が台本で突き合わせる）。
    /// </summary>
    public int? PierceLane { get; init; }

    /// <summary>
    /// 痺れ毒（第195期・<see cref="NumbTrait"/>）で減った <c>Attack</c> のときだけ入る（<b>表示専用</b>・どの規則も読まない）。
    /// <c>NumbPercent</c> 減った割合（毒の層 × 3・上限 60）／ <c>NumbCut</c> 減った打点（<c>Amount</c> は減った後の値）。
    /// 印があっても毒の層が 0 のとき・印の無い駒では <c>null</c>。
    /// </summary>
    public int? NumbPercent { get; init; }

    /// <inheritdoc cref="NumbPercent"/>
    public int? NumbCut { get; init; }

    /// <summary>
    /// 毒の <c>StatusGain</c> のときだけ、<b>付与経路</b>（<see cref="BattleCore.PoisonRoute"/>）が入る
    /// （第183期 追補2・<b>表示専用</b>）。毒の窓口（<c>BattleContext.Poison</c>）を通った付与はすべて載る。
    /// 毒以外の <c>StatusGain</c> と他の種類では <c>null</c>。<b>どの規則も読まない。</b>
    /// </summary>
    public PoisonRoute? PoisonRoute { get; init; }

    /// <summary>
    /// 伝染（<see cref="PoisonRoute.Touch"/>・疫みのラウ）の <c>StatusGain</c> のときだけ、
    /// <b>うつした元の敵 ＝ ラウに殴られた標的</b>の InstanceId が入る（第183期 追補2・<b>表示専用</b>）。
    /// <c>ActorId</c> はラウ、<c>TargetId</c> はうつされた隣の敵。線は <c>SpreadFromId</c> → <c>TargetId</c> に引く。
    /// <para><b>第185期 追補2</b>: 竦み（<see cref="StatusKeys.Cowed"/>）の <c>StatusGain</c> にも入る——
    /// <b>悲鳴の出どころ ＝ シガに責められた敵</b>（波紋の始点）。<c>ActorId</c> はシガ、<c>TargetId</c> は竦んだ隣の敵。</para>
    /// それ以外では <c>null</c>。<b>どの規則も読まない。</b>
    /// </summary>
    public int? SpreadFromId { get; init; }

    /// <summary>
    /// 反転（第190期・毒喰らいのベニ）で回復に変わった刻みの <c>Status</c> のときだけ、<b>反転させたベニ</b>の InstanceId
    /// （第194期・<b>表示専用</b>）。同じ件の <c>SourceTrait</c> は <see cref="TraitId.Inverse"/>。
    /// 毒・燃焼の刻みと、起爆（カタ）の味方側の両方に載る——起爆の <c>ActorId</c> はカタのままなので、ベニは別の欄にしてある。
    /// 反転しない刻みでは <c>null</c>。<b>どの規則も読まない。</b>
    /// </summary>
    public int? InverterId { get; init; }

    /// <summary>
    /// 濃縮の印（第194期・ミオ）で 1+n 回に広がった刻みの <c>Status</c> のときだけ: <b>何回目か</b>（1 始まり・<b>表示専用</b>）。
    /// 毒・燃焼の刻みと起爆に載る。印の無い駒（全部で1回）では <c>null</c>。
    /// </summary>
    public int? TickIndex { get; init; }

    /// <summary>
    /// <see cref="TickIndex"/> と対: <b>全部で何回の予定か</b>（1+n・<b>表示専用</b>）。途中で倒れた駒はここまで届かない
    /// （最後に並んだ件の <c>TickIndex</c> が <c>TickCount</c> より小さい）。
    /// </summary>
    public int? TickCount { get; init; }

    /// <summary><see cref="BattleEventKind.StatusDrain"/> のときだけ: 取り上げた後に吸われた駒に残った量（第183期 追補3・<b>表示専用</b>）。</summary>
    public int? StatusRemaining { get; init; }

    /// <summary>
    /// <see cref="BattleEventKind.StatusDrain"/> のときだけ: 同じ吸い上げの一連を束ねる通し番号（1戦の中で 1 から数える）。
    /// 同じ値の件が1回の吸い上げ（第183期 追補3・<b>表示専用</b>）。
    /// </summary>
    public int? DrainSeq { get; init; }

    /// <summary><see cref="BattleEventKind.StatusDrain"/> のときだけ: 一連の最後の1件か（第183期 追補3・<b>表示専用</b>）。</summary>
    public bool DrainLast { get; init; }

    /// <summary>
    /// <see cref="BattleEventKind.StatusDrain"/> の一連の最後の1件にだけ: 吸い上げが終わった時点の吸った側の攻撃力
    /// （<c>CurrentAttack</c>。第183期 追補3・<b>表示専用</b>）。
    /// </summary>
    public int? AttackAfter { get; init; }

    /// <summary>
    /// <see cref="BattleEventKind.Whet"/> のときだけ: <b>本来の対象</b>の InstanceId（<b>表示専用</b>）。
    /// 支援拒否（ガルド）が隣へ配った・横流しが差し替えたときは <c>TargetId</c>（実際に受け取った駒）と違う。
    /// </summary>
    public int? IntendedId { get; init; }

    /// <summary><see cref="BattleEventKind.Whet"/> のときだけ: 強化の経路（<b>表示専用</b>）。</summary>
    public WhetRoute? WhetRoute { get; init; }

    /// <summary>
    /// <see cref="BattleEventKind.Whet"/> / <see cref="BattleEventKind.HealBlocked"/> のときだけ:
    /// 書き手（回復の出どころ）の札（<c>Mark.Id</c>。印が無ければ null。<b>表示専用</b>）。
    /// </summary>
    public TraitId? SourceTrait { get; init; }

    /// <summary>
    /// <see cref="BattleEventKind.Whet"/> のときだけ: 同じ1回の配りの一連を束ねる通し番号（1戦の中で 1 から）。
    /// 支援拒否で隣の複数へ配ったときは同じ値が並ぶ（<b>表示専用</b>）。
    /// </summary>
    public int? SupportSeq { get; init; }

    /// <summary><see cref="BattleEventKind.Whet"/> のときだけ: 一連の最後の1件か（<b>表示専用</b>）。</summary>
    public bool SupportLast { get; init; }

    /// <summary>Highlight / Status のフレーバー。演出の中身ではなく添え物として扱う。</summary>
    public string? Text { get; init; }

    /// <summary>第210期（<b>表示専用</b>）: ツギの板（<see cref="PlankLabels.Paste"/> / <see cref="PlankLabels.FirstAid"/>）の量のうち基本の分と腕の分（在庫の分は <c>Slot</c>）。</summary>
    public int? PlankBase { get; init; }
    public int? PlankSkill { get; init; }
}

/// <summary>戦闘結果。UIはこれを表示するだけでよい。</summary>
/// <summary>
/// 傷という通貨の帳簿（第120期）。<b>1戦ぶんの計数で、盤面には一切影響しない。</b>
///
/// <para><b>帳簿が閉じることが自己検査 (a)</b>——<c>WriteAlly + WriteFoe</c> の総和 ＝
/// <c>LossAll</c> の総和（<see cref="WoundLoss.Death"/> / <see cref="WoundLoss.End"/> は
/// 「消えた」ではなく「読まれずに残った」）。加算は <see cref="BattleContext.Wound"/> の
/// 1 箇所だけなので、減算の側を全数当たれば必ず閉じる。</para>
///
/// <para><b>在庫・齢・介入の材料は <c>WoundRule.Census</c> のときだけ埋まる</b>
/// （既定は偽——`layout` は数百万戦を並列で回す）。書き込みと消滅の帳簿、
/// <see cref="HpRemoved"/>、読み手の計数は<b>版に依らず常に取る</b>。</para>
/// </summary>
/// <param name="WriteAlly">書かれた傷（味方側の駒に）。添字は <c>WoundRoute</c>。</param>
/// <param name="WriteFoe">書かれた傷（敵側の駒に）。添字は <c>WoundRoute</c>。</param>
/// <param name="LossAll">消滅の帳簿（添字は <see cref="WoundLoss"/>）。</param>
/// <param name="LossAlly">同・味方側の駒から消えたぶん。</param>
public readonly record struct WoundLedger(
    int[] WriteAlly, int[] WriteFoe, int[] LossAll, int[] LossAlly,
    int StockTurns, long StockAllySum, long StockFoeSum, int StockAllyMax, int StockFoeMax,
    int TurnsAllyAny, int TurnsFoeAny, long[] DepthAlly, long[] DepthFoe,
    long LagSum, int LagCount, long HpRemoved,
    int[] ReadFires, long[] ReadWounds, long[] ReadNominal, long[] ReadEffective,
    int[] GuardFires, int[] GuardTargetWounded, long[] GuardWoundedAllySum, int[] GuardAnyWoundedAlly)
{
    /// <summary>書かれた傷の総数（味方 ＋ 敵）。</summary>
    public int Written => WriteAlly.Sum() + WriteFoe.Sum();

    /// <summary>帳簿の右辺（消えた ＋ 残った）。<see cref="Written"/> と一致しなければならない。</summary>
    public int Accounted => LossAll.Sum();
}

/// <summary>
/// 上限（軛・第25期）の帳簿（第132期 段1）。<b>計数専用で、どの規則も読まない。</b>
///
/// <para>配列の添字は <c>攻撃型 + (受けたのが味方なら 5)</c> の 10 個。
/// <b>0..4 が敵に入った一撃（＝味方の刃）、5..9 が味方に入った一撃（＝敵の刃）</b>で、
/// <b>型の 4 は「型なし」</b>——継続ダメージ・反撃・肩代わりの中継・徴収は
/// <c>pattern</c> を渡さないのでここに落ちる。</para>
///
/// <para><b>保持者が盤上にいなければ全部 0。</b> 第四波（軛の重装兵）以外では1つも増えない。</para>
/// </summary>
/// <param name="CutHits">切られた一撃の回数。</param>
/// <param name="CutLost">切り落とされた量（<c>amount - Cap</c>）。</param>
/// <param name="CutPassed">切られたうえで通った量（<c>Cap</c>）。</param>
/// <param name="NearHits">切られなかったが上限に近い一撃（<c>Cap * 4 / 5</c> 超）。</param>
/// <param name="InHits">上限が効いている間に HP へ届いた回数。</param>
/// <param name="InAmount">同・量。</param>
/// <param name="Kills">同・その一撃で倒れた回数。</param>
/// <param name="Overkill">同・過剰分。</param>
public readonly record struct YokeLedger(
    long[] CutHits, long[] CutLost, long[] CutPassed, long[] NearHits,
    long[] InHits, long[] InAmount, long[] Kills, long[] Overkill,
    long CutOnPlayerHits, long CutOnPlayerLost, long CutOnEnemyHits, long CutOnEnemyLost,
    long ArmorSoak, long InRelayedHits, long InRelayedAmount,
    long InBurnHits, long InBurnAmount, long InLevyHits, long InLevyAmount,
    long DirectHpLoss, Dictionary<string, (long Hits, long Lost)> CutBy)
{
    /// <summary>切られた一撃の総数。</summary>
    public long Cuts => CutHits.Sum();
    /// <summary>切り落とされた量の総和。</summary>
    public long Lost => CutLost.Sum();
    /// <summary>上限が効いている間に HP へ届いた量の総和。</summary>
    public long Passed => InAmount.Sum();
}

/// <summary>
/// 標（<see cref="StatusKeys.Marked"/>）の一生の帳簿（第150期 段A）。
/// <b>計数専用で、どの規則も読まない。</b>
///
/// <para><b>「区間」の定義。</b> 標が付いていない駒に標が付いた瞬間に開き、次のどれかで閉じる:
/// <list type="bullet">
/// <item><c>Consumed</c> 止め（トメ）が殴って消した ＝ <b>設計どおり働いた</b></item>
/// <item><c>Died</c> 標を持ったまま倒れた（<c>DiedByFinisher</c> は止めが倒した内数）</item>
/// <item><c>Strip</c> 逸らし（ソラ）が味方から引き剥がした</item>
/// <item><c>Goad</c> 駆り立て（カリ）が別の相手へ付け替えた</item>
/// <item><c>Other</c> 上のどれでもない（<b>0 でなければ経路を数え落としている</b>）</item>
/// <item><c>Standing</c> 決着時にまだ立っていた</item>
/// </list>
/// <b>付いた標の総数 ＝ 分類の合計</b>（受け入れ条件4）。</para>
///
/// <para><b>陣営の添字は標が付いた側</b>——<c>0 = 敵に付いた標</c> / <c>1 = 味方に付いた標</c>。
/// 駆り立て（味方に付ける）と逸らし（敵と自分に付ける）は向きが逆なので、
/// 混ぜると読めない（指示書 Q0-3）。</para>
/// </summary>
/// <param name="Opened">開いた区間の数。添字は標が付いた側の陣営。</param>
/// <param name="Consumed">止めが消費して閉じた区間。同上。</param>
/// <param name="Died">標を持ったまま倒れて閉じた区間。同上。</param>
/// <param name="DiedByFinisher"><c>Died</c> のうち<b>止めが倒した</b>もの（内数）。同上。</param>
/// <param name="Strip">逸らしが剥がして閉じた区間。同上。</param>
/// <param name="Goad">駆り立てが付け替えて閉じた区間。同上。</param>
/// <param name="Other">分類できずに閉じた区間（<b>0 が期待値</b>）。同上。</param>
/// <param name="Standing">決着時に立ったまま閉じた区間。同上。</param>
/// <param name="LifeSum">区間の長さ（閉じたターン − 開いたターン）の総和。同上。</param>
/// <param name="LifeMax">区間の長さの最大。同上。</param>
/// <param name="Hits">標が立っているあいだに標持ちが受けた攻撃の回数（継続ダメージは除く）。同上。</param>
/// <param name="HitsByFinisher"><c>Hits</c> のうち止めが入れたもの（内数）。同上。</param>
/// <param name="On">標が付いた側の <c>Def.Id</c> → (開いた区間, 消費, 死亡)。</param>
public readonly record struct MarkLedger(
    long[] Opened, long[] Consumed, long[] Died, long[] DiedByFinisher,
    long[] Strip, long[] Goad, long[] Other, long[] Standing,
    long[] LifeSum, long[] LifeMax, long[] Hits, long[] HitsByFinisher,
    Dictionary<string, (long Opened, long Consumed, long Died)> On)
{
    /// <summary>閉じた区間の合計（<c>Opened</c> と一致するはず＝受け入れ条件4）。</summary>
    public long Closed(int side)
        => Consumed[side] + Died[side] + Strip[side] + Goad[side] + Other[side] + Standing[side];

    /// <summary>1区間あたりの平均の長さ（ターン）。区間が 0 なら 0。</summary>
    public double MeanLife(int side)
        => Opened[side] > 0 ? (double)LifeSum[side] / Opened[side] : 0.0;
}

/// <summary>
/// 標の軸の帳簿（第184期）。<b>計数専用で、どの規則も読まない。</b>
/// </summary>
/// <param name="VulnHits">§1 の被ダメージ増が乗った回数（添字は <see cref="MarkOrigin"/>＝その他／逸らし／仇指し）。</param>
/// <param name="VulnAdded">§1 で上乗せした量（同上）。</param>
/// <param name="FoeUnitTurns">ターン頭に敵の標が立っていた延べ体数。</param>
/// <param name="FoeTurns">ターン頭に敵の標が1体以上立っていたターン数。</param>
/// <param name="FoeMax">同時に立っていた最大数。</param>
/// <param name="GuardHits">§2 矢面の半減が効いた回数。</param>
/// <param name="GuardSaved">§2 矢面の半減で防いだ量。</param>
/// <param name="StrippedHits">§2 矢面の記憶が指しているのに標が剥がされていて、半減が掛からなかった被弾の回数。</param>
/// <param name="StrippedDamage">同じく、その被弾の量（軽減の族を通った後・肩代わりの前）。</param>
public readonly record struct MarkAxisLedger(
    long[] VulnHits, long[] VulnAdded, long FoeUnitTurns, long FoeTurns, long FoeMax,
    long GuardHits, long GuardSaved, long StrippedHits, long StrippedDamage);

/// <summary>
/// 燃焼の重ね掛けの帳簿（第134期 段1）。<b>計数専用で、どの規則も読まない。</b>
///
/// <para><b>陣営の添字は受け手の側</b>——<c>0 = 敵に点いた火</c> / <c>1 = 味方に点いた火</c>。
/// <see cref="BattleContext.Ignite"/> は<b>残ターンを上書きする（加算しない）</b>ので、
/// 既に燃えている駒への再付与は「濃さ」を1ビットも変えない。<b>その捨てられている供給が
/// 何回起きているか</b>だけを数える。</para>
///
/// <para><b>「区間」（＝消えるまでの点け直し回数）の定義。</b>
/// <c>火が点いていない駒に点いた瞬間</c>に区間が開き、次のどれかで閉じる:
/// <list type="bullet">
/// <item><c>Expired</c> 残ターンが 0 まで落ちた（<c>TickStatuses</c> で燃え尽きた）</item>
/// <item><c>Death</c> 燃えたまま倒れた（決着時に <c>IsAlive</c> が偽）</item>
/// <item><c>Alive</c> 燃えたまま決着した（<c>IsAlive</c> が真）</item>
/// </list>
/// <b>1区間の「点け直し回数」は、その区間が開いてから閉じるまでに走った再付与の回数</b>
/// （0 なら一度も煽られずに燃え尽きた）。<b>戦闘単位ではなく区間単位で数える</b>ので、
/// 同じ駒が2度燃えれば2区間になる。</para>
/// </summary>
/// <param name="Lit">火が点いた回数（<c>relit == false</c>）。添字は受け手の陣営。</param>
/// <param name="Relit">既に燃えている駒への再付与の回数。同上。</param>
/// <param name="Episodes">閉じた区間の数。同上。</param>
/// <param name="RelitSum">閉じた区間の点け直し回数の総和。同上。</param>
/// <param name="RelitMax">1区間の点け直し回数の最大。同上。</param>
/// <param name="Hist">点け直し回数の分布。<c>[陣営][0..5]</c> で <b>5 は「5回以上」</b>。</param>
/// <param name="EndExpired">燃え尽きて閉じた区間の数。同上。</param>
/// <param name="EndDeath">燃えたまま倒れて閉じた区間の数。同上。</param>
/// <param name="EndAlive">燃えたまま決着して閉じた区間の数。同上。</param>
/// <param name="By">点けた側の <c>Def.Id</c> → (点けた回数, 煽った回数)。<b>付け手が渡された着火だけ</b>。</param>
/// <param name="On">点けられた側の <c>Def.Id</c> → (点いた回数, 煽られた回数)。</param>
public readonly record struct BurnLedger(
    long[] Lit, long[] Relit, long[] Episodes, long[] RelitSum, long[] RelitMax,
    long[][] Hist, long[] EndExpired, long[] EndDeath, long[] EndAlive,
    Dictionary<string, (long Lit, long Relit)> By,
    Dictionary<string, (long Lit, long Relit)> On)
{
    /// <summary>着火の総回数（点いた ＋ 煽った）。</summary>
    public long Fires => Lit.Sum() + Relit.Sum();

    /// <summary>1区間あたりの平均の点け直し回数（<b>P1 の主判定</b>）。区間が 0 なら 0。</summary>
    public double MeanRelit(int team)
        => Episodes[team] > 0 ? (double)RelitSum[team] / Episodes[team] : 0.0;
}

/// <summary>
/// 破片（<c>StatusKeys.Armor</c>）の在庫の帳簿（第138期 段2）。<b>計数専用で、どの規則も読まない。</b>
///
/// <para><b>ターン頭に1回だけ写す</b>（<c>TickStatuses</c> の後・<c>OnTurnStart</c> の<b>前</b>）。
/// 前に置くのは、砕けの自前の鍵（<c>ShatterTrait.OnTurnStart</c>）と鱗の纏い率の分母が
/// <c>OnTurnStart</c> にあるため——<b>そのターンの供給が乗る前の在庫</b>を見ないと、
/// 「礫が手番で見る在庫」と別のものを数えることになる（礫も <c>OnAction</c> ＝ 行動順ループの中）。</para>
///
/// <para>添字は陣営（<c>0 = 敵</c> / <c>1 = 味方</c>）。<see cref="BattleContext.PlayerTeam"/> の値に合わせてある。</para>
/// </summary>
/// <param name="Turns">走査したターン頭の数（全部の分母）。</param>
/// <param name="StockSum">盤面全体の在庫の総和。陣営別。</param>
/// <param name="StockMax">盤面全体の在庫の最大（1ターン頭あたり）。陣営別。</param>
/// <param name="TopSum">最も多く纏っている1体の在庫の総和。陣営別。</param>
/// <param name="TopMax">同・最大。陣営別。</param>
/// <param name="TurnsAny">在庫が1点でもあったターン頭の数。陣営別。</param>
/// <param name="Holders">在庫を持っていた駒の延べ数。陣営別。</param>
/// <param name="TopBy">最大保持者の <c>Def.Id</c> → (回数, 在庫の総和)。<b>味方側だけ</b>数える。</param>
public readonly record struct ArmorLedger(
    long Turns, long[] StockSum, long[] StockMax, long[] TopSum, long[] TopMax,
    long[] TurnsAny, long[] Holders,
    Dictionary<string, (long Times, long Sum)> TopBy)
{
    /// <summary>味方側の、1ターン頭あたりの平均在庫（盤面全体）。</summary>
    public double MeanStock(int team) => Turns > 0 ? (double)StockSum[team] / Turns : 0.0;

    /// <summary>味方側の、1ターン頭あたりの平均「最大保持者の在庫」＝<b>礫が1回に砕ける量の見積もり</b>。</summary>
    public double MeanTop(int team) => Turns > 0 ? (double)TopSum[team] / Turns : 0.0;
}

/// <summary>
/// 預かりの帳簿（第153期 段A）。<b>計数専用で、どの規則も読まない。</b>
///
/// <para><b>収支が閉じること</b>——<c>Stacked == Released + Forfeited + Residual</c>——が
/// 自己検査 (c)（指示書 §6 条件5）。<b>1点もずれてはいけない。</b></para>
/// </summary>
/// <param name="Stacked">預かりに積んだ総量。</param>
/// <param name="ReleaseAsked">返そうとした総量（プールから出そうとした量）。</param>
/// <param name="Released"><b>実際に HP が増えた量</b>＝プールから実際に減った量。</param>
/// <param name="Residual">決着時にプールに残っていた量（死者も含む）。</param>
/// <param name="Bursts">閾値に達して全額を返そうとした回数。</param>
/// <param name="Drips">毎ターン頭に返そうとした回数。</param>
/// <param name="Dry">返そうとしたが1点も入らなかった回数。</param>
/// <param name="DryDrought">そのうち渇きで止まった回数。</param>
/// <param name="DryStoic">そのうち支援拒否（<c>Stoic</c>）で止まった回数。</param>
/// <param name="Forfeits">没収の発火回数。</param>
/// <param name="Forfeited">没収された預かりの総量（敵へ渡した名目量）。</param>
/// <param name="ForfeitHealed">没収で敵の HP が実際に増えた量（体数ぶん重なる）。</param>
/// <param name="On">預けた駒ごとの内訳（<c>Def.Id</c> → 積んだ量・返った量）。</param>
/// <param name="BurdenHits">荷（第154期）が被ダメージを増やした回数。</param>
/// <param name="BurdenAdded">同・<b>増えた分だけ</b>の総量（素のダメージは含まない）。</param>
/// <param name="LadenSwings">重り（第154期）が乗った振りの回数。</param>
/// <param name="LadenFloored">そのうち下限 1 で切られた振り（<b>名目 &gt; 実効</b>）。</param>
/// <param name="LadenSwingLost"><b>実際に振られなかった打点</b>（名目）。</param>
/// <param name="LadenNominal">ターン頭に数えた「下がっている攻撃力」の延べ総量（<b>在庫の側</b>）。</param>
/// <param name="LadenCarriers">同・ターン頭に預かりを抱えていた駒の延べ数。</param>
public readonly record struct WardLedger(
    long Stacked, long ReleaseAsked, long Released, long Residual,
    long Bursts, long Drips, long Dry, long DryDrought, long DryStoic,
    long Forfeits, long Forfeited, long ForfeitHealed,
    Dictionary<string, (long Stacked, long Released)> On,
    long BurdenHits = 0, long BurdenAdded = 0,
    long LadenSwings = 0, long LadenFloored = 0, long LadenSwingLost = 0,
    long LadenNominal = 0, long LadenCarriers = 0)
{
    /// <summary>収支が閉じているか（1点もずれていないか）。</summary>
    public bool Balanced => Stacked == Released + Forfeited + Residual;
}

/// <summary>
/// 贖いの帳簿（第155期 段A）。<b>計数専用で、どの規則も読まない。</b>
///
/// <para><b>これは「負債」の帳簿であって HP の帳簿ではない。</b>
/// 収支は <c>Stacked == Collected + Forgiven + Residual</c> で 1 点もずれずに閉じる。
/// 取り立てが<b>実際に削った HP</b>（<see cref="TollTaken"/>）は、上限（軛）・破片・
/// HP1 のクランプ・肩代わりで名目（<see cref="Collected"/>）を下回る——
/// <b>この差が「取り立てが取りこぼした分」で、そのまま焼きの燃料の目減りになる。</b></para>
/// </summary>
/// <param name="Fires">前借りの発火回数。</param>
/// <param name="Asked">前借りが <c>ctx.Heal</c> に要求した名目量。</param>
/// <param name="Stacked"><b>実際に増えた HP</b>＝積まれた負債の総量。</param>
/// <param name="Dry">前借りを撃ったが1点も入らなかった回数。</param>
/// <param name="DryDrought">そのうち渇きで止まった回数。</param>
/// <param name="NoPatient">貸す相手がいなかった回数。</param>
/// <param name="Blocked"><b>そのうち契約枠が埋まっていて貸せなかった回数</b>（第155期 追補・計数専用）。</param>
/// <param name="BlockedIdle">同・そのとき <c>IdleTurn</c> が立っていた回数（号令・据えが買い取れるか）。</param>
/// <param name="TollFires">取り立ての発火回数。</param>
/// <param name="Collected">取り立てた負債の<b>名目</b>量。</param>
/// <param name="TollTaken">取り立てが<b>実際に削った HP</b>（＝焼きの燃料）。</param>
/// <param name="TollFloored">HP1 のクランプで止まった回数。</param>
/// <param name="TollYokeCut">取り立てが軛に切られた量。</param>
/// <param name="TollKills"><b>取り立てが直接殺した回数</b>（<c>lethal: false</c> が効いていれば常に 0）。</param>
/// <param name="Forgives">踏み倒し（借り手が負債を抱えたまま倒れた）の回数。</param>
/// <param name="Forgiven">同・保持者が引き受けた負債の名目量。</param>
/// <param name="ForgivenSelfHp">同・<b>保持者の HP が実際に減った量</b>（肩代わりされた分は出ない）。</param>
/// <param name="BrandFires">焼きの発火回数。</param>
/// <param name="BrandSpent">焼きが叩き込んだ名目量（全体の巻き込みを含む）。</param>
/// <param name="BrandRemoved">同・<b>実際に削った HP</b>。</param>
/// <param name="BrandYokeCut">同・軛に切られた量。</param>
/// <param name="BrandHits">焼きが当たった体数（延べ）。</param>
/// <param name="BrandKills">焼きで倒した敵の数。</param>
/// <param name="BrandDry">燃料はあるのに狙える敵が1体もいなかった回数。</param>
/// <param name="BrandResidual">決着時に燃え残った燃料。</param>
/// <param name="Residual">決着時に残っていた負債（死者も含む）。</param>
/// <param name="On">借り手ごとの内訳（<c>Def.Id</c> → 積んだ量・取り立てられた量）。</param>
public readonly record struct IndulgenceLedger(
    long Fires, long Asked, long Stacked, long Dry, long DryDrought, long NoPatient,
    long Blocked, long BlockedIdle,
    long TollFires, long Collected, long TollTaken, long TollFloored, long TollYokeCut, long TollKills,
    long Forgives, long Forgiven, long ForgivenSelfHp,
    long BrandFires, long BrandSpent, long BrandRemoved, long BrandYokeCut,
    long BrandHits, long BrandKills, long BrandDry, long BrandResidual,
    long Residual, Dictionary<string, (long Stacked, long Collected)> On)
{
    /// <summary>収支が閉じているか（1点もずれていないか）。</summary>
    public bool Balanced => Stacked == Collected + Forgiven + Residual;
}

/// <summary>
/// 盤面ルールの対称性の帳簿（第134期 段2）。<b>計数専用で、どの規則も読まない。</b>
///
/// <para><b>第132期の <see cref="YokeLedger"/> と同じ形</b>を、渇き（<c>Drought</c>）と
/// 粛（<c>Hush</c>）に当てたもの。あちらが「切られた側の陣営」を数えたのと同じく、
/// <b>ここも数えるのは「課税された側の陣営」</b>——<c>0 = 敵</c> / <c>1 = 味方</c>。</para>
///
/// <para><b>規則は「両陣営に等しくかかる」と宣言されている</b>が、第132期に軛が
/// <b>実質プレイヤー専用の税</b>だったことが実測で出ている。<b>対称性は規則ではなく
/// 数値と在庫で決まる</b>ので、その2つを陣営別に数える。</para>
/// </summary>
/// <param name="DroughtHits">渇きが止めた回復の回数。添字は回復されるはずだった駒の陣営。</param>
/// <param name="DroughtRequested">同・要求された量（<c>Heal</c> の引数）。</param>
/// <param name="DroughtEffective">同・<b>実際に入るはずだった量</b>（<c>MaxHp - Hp</c> で切った後）。</param>
/// <param name="DroughtOn">止められた駒の <c>Def.Id</c> → (回数, 実効量)。</param>
/// <param name="HushBlocked">粛が<b>単独の原因で</b>止めたターン外の行動の回数。添字は行動しようとした駒の陣営。</param>
/// <param name="HushBlockedAny">同・粛が閉じていた問い合わせの回数（痺れ等で既に落ちていた分を含む）。</param>
/// <param name="HushByRoute">経路別の <c>HushBlocked</c>。<c>[経路][陣営]</c>（経路は <see cref="OutOfTurnRoute"/>）。</param>
/// <param name="HushAsked">粛の有無に関わらず <c>CanActOutOfTurn</c> が問われた回数。添字は陣営。</param>
/// <param name="HolderCount">保持者の数。添字は <see cref="BoardRuleLedger.RuleIndex"/>。</param>
/// <param name="HolderFallTurn">保持者が全員倒れたターン（<c>0</c> ＝ 最後まで生きていた、または保持者がいない）。</param>
public readonly record struct BoardRuleLedger(
    long[] DroughtHits, long[] DroughtRequested, long[] DroughtEffective,
    Dictionary<string, (long Hits, long Amount)> DroughtOn,
    long[] HushBlocked, long[] HushBlockedAny, long[][] HushByRoute, long[] HushAsked,
    int[] HolderCount, int[] HolderFallTurn)
{
    /// <summary>ルールの添字（<see cref="HolderCount"/> / <see cref="HolderFallTurn"/> 用）。</summary>
    public enum RuleIndex { Yoke = 0, Drought = 1, Hush = 2, Inversion = 3 }

    /// <summary>ルールの数（<see cref="RuleIndex"/> の要素数）。</summary>
    public const int RuleCount = 4;
}

public sealed class BattleResult
{
    public required bool PlayerWon { get; init; }
    public required int Turns { get; init; }
    public required IReadOnlyList<LogLine> Log { get; init; }

    /// <summary>味方の生存数。バランス調整の指標として使う。</summary>
    public required int PlayerSurvivors { get; init; }

    /// <summary>
    /// <b>出撃した味方のうち、決着時に生存していない駒の <c>Def.Id</c></b>（第129期・<b>計数専用</b>）。
    ///
    /// <para><b><see cref="PlayerSurvivors"/> では「1体も失わずに勝った」が書けない</b>のが理由。
    /// あちらは <c>ctx.LivingMembers(PlayerTeam).Count()</c> なので<b>戦闘中に湧いた駒
    /// （胞子・餌）も数える</b>——出撃した5枚のうち3枚が落ちて胞子が3体湧いた戦が、
    /// 出撃数との比較では「完全勝利」として通る。ここは<b>出撃した駒だけ</b>を、
    /// <b>個体（<c>UnitState</c>）の同一性で</b>見る。</para>
    ///
    /// <para>空なら「出撃した駒が1枚も欠けていない」。蘇生・継ぎ接ぎで戻った駒は<b>入らない</b>
    /// （見ているのは決着時に生きているかどうかで、倒れた回数ではない
    /// ——<c>UnitTally.Deaths</c> とはそこが違う）。</para>
    ///
    /// <para><b>誰も読んで分岐しない。</b> <c>verbose</c> にも依存しない（`compare` は
    /// <c>verbose: false</c> で 61 行 × 5 波 × 200 seed を回す）。
    /// 会戦（<c>Engagement</c>）では「その部隊戦に入場した駒」を指す。</para>
    /// </summary>
    public required IReadOnlyList<string> PlayerStarterFallen { get; init; }

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
    /// <summary>傷という通貨の帳簿（第120期・<see cref="WoundLedger"/>）。<b>計数専用。</b></summary>
    public required WoundLedger Wounds { get; init; }

    /// <summary>上限（軛）の帳簿（第132期 段1・<see cref="YokeLedger"/>）。<b>計数専用。</b></summary>
    public required YokeLedger Yoke { get; init; }

    /// <summary>燃焼の重ね掛けの帳簿（第134期 段1・<see cref="BurnLedger"/>）。<b>計数専用。</b></summary>
    public required BurnLedger Burns { get; init; }

    /// <summary>標の一生の帳簿（第150期 段A）。<b>計数専用で、どの規則も読まない。</b></summary>
    public required MarkLedger Marks { get; init; }

    /// <summary>
    /// 第184期。標の軸（§1 被ダメージ増・§2 矢面の半減）の帳簿（<b>計数専用</b>。どの規則も読まない）。
    /// 駒ごとの計数（付け替え・逃げ・倍返し・返り血）は <see cref="TallyByUnit"/> にある。
    /// </summary>
    public MarkAxisLedger MarkAxis { get; init; }

    /// <summary>第185期。1ターンに手番を失った敵の数の分布（0/1/2/3+）。<b>計数専用</b>（ターン末に1回数える）。</summary>
    public long[] FoeStalledHist { get; init; } = new long[4];

    /// <summary>第202期・<b>計数専用</b>。規則で選ぶ陣形の貫き: [撃つ敵の格子のレーン 0..2 × 経路 0..1] の回数。</summary>
    public long[] PierceChose { get; init; } = new long[6];
    /// <summary>第202期・計数専用。同数で交互に割った回数 ／ 選んだ経路が空で反対側へ回った回数。</summary>
    public long PierceTies { get; init; }
    public long PierceFallbacks { get; init; }

    /// <summary>盤面ルール（渇き・粛）の帳簿（第134期 段2・<see cref="BoardRuleLedger"/>）。<b>計数専用。</b></summary>
    public required BoardRuleLedger BoardRules { get; init; }

    /// <summary>破片の在庫の帳簿（第138期 段2・<see cref="ArmorLedger"/>）。<b>計数専用。</b></summary>
    public required ArmorLedger Armor { get; init; }

    /// <summary>預かりの帳簿（第153期 段A・<see cref="WardLedger"/>）。<b>計数専用。</b></summary>
    public required WardLedger Ward { get; init; }

    /// <summary>贖いの帳簿（第155期 段A・<see cref="IndulgenceLedger"/>）。<b>計数専用。</b></summary>
    public required IndulgenceLedger Indulgence { get; init; }

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

    /// <summary>
    /// 滲み則のなまり（第95期。第96期 (R1) に <see cref="SoakRule"/> へ畳んだ）の計数。
    /// <b>盤面には一切影響しない。</b>
    /// <c>SoakDullFired</c> 汚れが1種以上あって重くなった回数 ／ <c>SoakDullDry</c> 空振り（汚れ 0 種） ／
    /// <c>SoakDullKinds</c> 種類数の総和 ／ <c>SoakDullAdded</c> 上乗せした量。
    /// <b>既定（<see cref="SoakRule.Default"/> ＝ <c>DullPerKind 1</c>）では走る</b>——
    /// 切るのは <c>DullPerKind 0</c>。
    /// </summary>
    public required int SoakDullFired { get; init; }
    public required int SoakDullDry { get; init; }
    public required int SoakDullKinds { get; init; }
    public required int SoakDullAdded { get; init; }

    /// <summary>
    /// 呪い（第96期・<see cref="CurseRule"/>）の計数。<b>盤面には一切影響しない。</b>
    ///
    /// <para><b>門（§1-1）の3つ</b>——<c>HexHits*</c>（祟りの保持者が殴られた回数＝配れる機会）／
    /// <c>HexPairTurn*</c>（同陣営に2体目の呪いが立ったターン。<b>0 ＝ 一度も立たなかった</b>）／
    /// <c>HexShareHits</c>（単体攻撃が呪い持ちに入った回数）。
    /// <b>3つとも 0 より大きくないと共有は1回も起きない。</b></para>
    ///
    /// <para><b>第182期から既定（<see cref="CurseRule.Default"/>）は共有する。</b>
    /// <see cref="CurseRule.Off"/>（第181期までの既定）では <c>HexHits*</c> 以外は全部 0。</para>
    /// <para>（第96期の記述）<b>既定（共有しない）では全部 0。</b>
    /// 門を数えるときは <c>new CurseRule(true, 0)</c>（印は書くが共有量 0）を渡す。</para>
    /// </summary>
    public required int HexHits { get; init; }
    public required int HexHitsFromFoe { get; init; }
    public required int HexHitsFromAlly { get; init; }
    public required int HexHitsNoSource { get; init; }
    public required int HexMarks { get; init; }
    public required int HexMarksOnPlayer { get; init; }
    public required int HexMarksOnEnemy { get; init; }
    public required int HexReMarkBlocked { get; init; }
    public required int HexPairTurnPlayer { get; init; }
    public required int HexPairTurnEnemy { get; init; }
    public required int HexPairTurnsPlayer { get; init; }
    public required int HexPairTurnsEnemy { get; init; }
    public required int HexMaxCursedPlayer { get; init; }
    public required int HexMaxCursedEnemy { get; init; }
    public required int HexCensusTurns { get; init; }
    public required int HexShareHits { get; init; }
    public required int HexShareDry { get; init; }
    public required int HexShares { get; init; }
    public required int HexShareDamage { get; init; }
    public required int HexSharesToPlayer { get; init; }
    public required int HexShareDamageToPlayer { get; init; }
    public required int HexShareTargets { get; init; }
    public required long HexShareBase { get; init; }
    public required long HexShareBaseTimesTargets { get; init; }
    public required int HexMarksOnStoic { get; init; }
    public required int HexHopBlocked { get; init; }
    public required int HexNonSingleOnCursed { get; init; }

    /// <summary>
    /// 第118期 —— 糧（<c>NourishRule</c>）の計数。<b>盤面には一切影響しない。</b>
    ///
    /// <para><c>NourishFires</c> 発火回数（<c>Gain = 0</c> の対照でも同じだけ立つ） ／
    /// <c>NourishGiven</c> 実際に渡した量（<c>WhetByRoute[Nourish]</c> と一致するのが自己検査） ／
    /// <c>NourishToFoe</c> / <c>NourishToAlly</c> その内訳（門3）。</para>
    ///
    /// <para><b>発火しなかった内訳</b>——<c>NourishNoSource</c>（毒・燃焼・転嫁の代金）／
    /// <c>NourishSelf</c>（自傷）／<c>NourishLevy</c>（徴収）／<c>NourishDead</c>（相打ち）／
    /// <c>NourishSoaked</c>（<b>破片で受け切って <c>OnDamaged</c> まで届かなかった</b>被弾。
    /// これだけは engine の側で数える）。</para>
    ///
    /// <para><c>NourishByPath</c> は経路別の発火回数（<see cref="NourishPaths"/> の並び）。
    /// <b>保持者が盤上にいなければ空</b>。</para>
    /// </summary>
    public required int NourishFires { get; init; }
    public required int NourishGiven { get; init; }
    public required int NourishToFoe { get; init; }
    public required int NourishToAlly { get; init; }
    public required int NourishNoSource { get; init; }
    public required int NourishSelf { get; init; }
    public required int NourishLevy { get; init; }
    public required int NourishDead { get; init; }
    public required int NourishSoaked { get; init; }
    public required IReadOnlyList<int> NourishByPath { get; init; }

    /// <summary>
    /// 第103期 —— 背かれ（<c>BetrayRule</c>）の計数。<b>盤面には一切影響しない。</b>
    /// 既定（喚ばない）では全部 0。意味は <c>BattleContext</c> 側の doc を参照。
    /// </summary>
    public required int BetrayTries { get; init; }
    public required int BetraySummoned { get; init; }
    public required int BetrayBlocked { get; init; }
    public required int BetrayAllySide { get; init; }
    public required int BetrayWrongSlot { get; init; }
    public required int BetrayMaxAlive { get; init; }
    public required int BetrayIdleSellable { get; init; }
    public required int BetrayRevived { get; init; }
    public required int BetrayKilled { get; init; }

    /// <summary>
    /// 再行動（第104期・<c>EncoreRule</c>）の計数。<b>誰も読んで分岐しない。</b>
    /// <para>門 1 <c>EncoreWoundedDeaths</c> / <c>EncoreWoundedFoeDeaths</c>、
    /// 門 2 <c>EncoreLiveWriters</c> / <c>EncoreDeathsWithLiveWriter</c>、
    /// 門 3 <c>EncoreFired</c>。Q4 の内訳が <c>EncoreAttack</c> / <c>EncoreSkill</c> /
    /// <c>EncoreCharge</c> / <c>EncoreStalled</c>、Q5 が <c>EncoreFodderDeaths</c> /
    /// <c>EncoreFromFodder</c>、自己検査が <c>EncoreBlockedHop</c>（d）/
    /// <c>EncoreOnEnemySide</c>（g）/ <c>EncoreWithActions</c>（h）/ <c>EncoreRevivedSkip</c>。</para>
    /// </summary>
    public required int EncoreWoundedDeaths { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreWoundedFoeDeaths { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreLiveWriters { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreDeathsWithLiveWriter { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreFired { get; init; }

    // ------------------------------------------------------------------------------------
    // 第105期（手番の値段）。**required にしない**——既存の生成箇所を1つも触らないため。
    // どれも観測専用で、どの規則もこれを読まない。
    // ------------------------------------------------------------------------------------

    /// <summary>行動順ループが <c>TakeTurn</c> を呼んだ回数（自己検査 (c) の右辺）。</summary>
    public int TurnLoopCalls { get; init; }

    /// <summary>
    /// 前倒し（第149期・<c>HasteRule</c>）が <c>order</c> を組み替えた回数。<b>観測専用。</b>
    /// </summary>
    public int HasteMoves { get; init; }

    /// <summary>
    /// 前倒しが <c>order</c> の要素数を変えた回数（第149期）。<b>常に 0。観測専用。</b>
    /// </summary>
    public int HasteCountMismatch { get; init; }

    /// <summary>
    /// 出力の3分割の総計（自己検査 (d)）。<c>In + Off + None == All</c> が成り立つ。
    /// ダメージ・回復は<b>量</b>、状態異常は<b>書き込みの回数</b>。
    /// </summary>
    public long TurnDmgAll { get; init; }
    public long TurnDmgIn { get; init; }
    public long TurnDmgOff { get; init; }
    public long TurnDmgNone { get; init; }
    public long TurnHealAll { get; init; }
    public long TurnHealIn { get; init; }
    public long TurnHealOff { get; init; }
    public long TurnHealNone { get; init; }
    public long TurnStatusAll { get; init; }
    public long TurnStatusIn { get; init; }
    public long TurnStatusOff { get; init; }
    public long TurnStatusNone { get; init; }

    /// <summary>
    /// 4つ目の通貨（第106期 (T1)）——<c>AtkBonus</c> を動かした<b>絶対量</b>の3分割。
    /// </summary>
    public long TurnBuffAll { get; init; }
    public long TurnBuffIn { get; init; }
    public long TurnBuffOff { get; init; }
    public long TurnBuffNone { get; init; }

    /// <summary>
    /// 4つ目の通貨を<b>特性ごとに</b>割った観測（第106期 Phase 0 §1）。
    /// 添字は <c>(int)TraitId</c>。<c>*NoMark</c> は印が立っていない箇所からの増減。
    /// <b>「AtkBonus を動かす経路の全数」を手で書かずに出すための器具。</b>
    /// </summary>
    public long[]? BuffGainByTrait { get; init; }
    public long[]? BuffLossByTrait { get; init; }
    public long BuffGainNoMark { get; init; }
    public long BuffLossNoMark { get; init; }

    /// <summary>憤怒の発火を出どころで割った観測（第106期 Phase 0 §2-1）。<b>版に依らない。</b></summary>
    public int RageFiresFromFoe { get; init; }
    public int RageFiresFromAlly { get; init; }
    public int RageFiresNoSource { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreAttack { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreSkill { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreCharge { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreStalled { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreBlockedHop { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreOnEnemySide { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreWithActions { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreRevivedSkip { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreFodderDeaths { get; init; }
    /// <inheritdoc cref="EncoreWoundedDeaths"/>
    public required int EncoreFromFodder { get; init; }
    public required int BetrayFireAttack { get; init; }
    public required int BetrayFirePoison { get; init; }
    public required int BetrayFireOverreach { get; init; }
    public required int BetrayHits { get; init; }
    public required int BetrayHitAtkSum { get; init; }
    public required int HexCrossTeam { get; init; }
    public required int HexSpillSuppressed { get; init; }
    public required IReadOnlyDictionary<string, int> HexShareBySource { get; init; }
    public required IReadOnlyDictionary<string, int> HexShareDamageBySource { get; init; }
    public required IReadOnlyDictionary<string, int> HexHitByAlly { get; init; }
    public required IReadOnlyDictionary<string, int> HexHitByFoe { get; init; }
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
    /// 砕け（第137期）の帳簿。<c>ShatterTicks</c> 発火回数 ／ <c>ShatterGiven</c> 配った総量 ／
    /// <c>ShatterSoaked</c> そのうち実際に吸った量 ／ <c>ShatterPaid</c> 代金の総額 ／
    /// <c>ShatterPaidSelf</c> そのうち保持者が自弁した額。
    /// <b>どの規則も読まない計数</b>で、<c>ShatterSoaked</c> は
    /// 破片のプール全体（集約・鱗と混ざる）を数えていることに注意する。
    /// </summary>
    /// <summary>
    /// 礫（第138期・<see cref="TraitId.Shrapnel"/>）の帳簿。<b>どの規則も読まない計数。</b>
    /// <c>ShrapnelFires</c> 撃った回数 ／ <c>ShrapnelShards</c> 砕いた破片の総量 ／
    /// <c>ShrapnelFoeTargets</c> <b>敵</b>を砕いた回数（<b>この期は 0 が正</b>）／
    /// <c>ShrapnelDealt</c> 敵全体へ撃った<b>名目</b>の総量 ／ <c>ShrapnelHits</c> 着弾した体数 ／
    /// <c>ShrapnelSelfHarm</c> 砕かれた駒へ返した総量。
    ///
    /// <para><b>捨てた手番（破片が無くて撃てなかった回数）はここには無い</b>——
    /// engine が既に <c>UnitTally.StallCanAct</c> で数えている。
    /// <b>実際に通ったダメージ</b>も <c>UnitTally.DamageToEnemy</c> にあり、
    /// <c>ShrapnelDealt</c> との差が<b>軽減と軛の切り取り</b>になる（予測 P6）。</para>
    /// </summary>
    public required int ShrapnelFires { get; init; }
    /// <inheritdoc cref="ShrapnelFires"/>
    public required int ShrapnelShards { get; init; }
    /// <inheritdoc cref="ShrapnelFires"/>
    public required int ShrapnelFoeTargets { get; init; }
    /// <inheritdoc cref="ShrapnelFires"/>
    public required int ShrapnelDealt { get; init; }
    /// <inheritdoc cref="ShrapnelFires"/>
    public required int ShrapnelSelfHarm { get; init; }
    /// <inheritdoc cref="ShrapnelFires"/>
    public required int ShrapnelHits { get; init; }

    public required int ShatterTicks { get; init; }
    public required int ShatterGiven { get; init; }
    public required int ShatterSoaked { get; init; }
    public required int ShatterPaid { get; init; }
    public required int ShatterPaidSelf { get; init; }

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
