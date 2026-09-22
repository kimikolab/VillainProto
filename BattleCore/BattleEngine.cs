namespace BattleCore;

/// <summary>
/// 戦闘中の盤面。特性はこれを通してのみ盤面に触る。
/// </summary>
/// <summary>
/// 第94期 (T2) の観測子。<b>盤面には一切影響しない。</b>
///
/// <para><paramref name="trait"/> がいま実行中の特性、<paramref name="owner"/> がその持ち主、
/// <paramref name="target"/> がカウンタを読み書きされた駒、<paramref name="key"/> がキー
/// （<see cref="StatusKeys"/> か <see cref="UnitTally.CarryKeys"/> の名前）、
/// <paramref name="delta"/> は <b>0 なら読み・正なら供給・負なら消費</b>。</para>
/// </summary>
public delegate void CounterProbe(TraitId trait, UnitState owner, UnitState target, string key, int delta);

/// <summary>
/// ボスの土台の計数（第117期）。<b>診断（<c>boss</c>）が時系列を取るためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。static のノブにしない理由は同型の doc を参照。
///
/// <para><b>盤面を1ビットも動かさない。</b> <c>Census</c> は「ターンごとの攻撃力と与ダメを
/// 配列に写すか」だけを切り替える。既定は偽で、そのとき配列は1本も割り当たらない
/// ——<c>compare</c> / <c>layout</c> は数百万戦を回すので、確保だけで効く。</para>
/// </summary>
public readonly record struct BossRule(bool Census)
{
    /// <summary>既定は<b>数えない</b>。診断だけが <c>new BossRule(true)</c> を渡す。</summary>
    public static BossRule Default => new(false);
}

/// <summary>
/// 第94期 (T2) の印。<b>いま実行中の特性とその持ち主</b>だけを持つ観測専用の値。
/// <b>どの規則もこれを読まない</b>（`derive check` の自己検査 (d)）。
/// </summary>
public readonly struct TraitMark
{
    public TraitMark(TraitId id, UnitState? owner) { Id = id; Owner = owner; }
    public TraitId Id { get; }
    public UnitState? Owner { get; }
}

/// <summary>状態異常のカウンタ名。特性と engine の間の唯一の接点。</summary>
public static class StatusKeys
{
    public const string Poison = "poison";
    public const string Marked = "marked";
    public const string Stun = "stun";

    /// <summary>燃焼の残りターン。毒と違い「量」ではなく「時間」を持つ。</summary>
    public const string Burn = "burn";

    /// <summary>そのターン行動できなかった駒に記録されるターン番号。</summary>
    public const string IdleTurn = "idleTurn";

    /// <summary>
    /// 破片（アーマー）。HP の前に削られるプール。
    ///
    /// **回復とは別資源**にしてあるのが要点。`ctx.Heal` は `AcceptsSupport` を見るので
    /// 廃棄聖騎士ガルド（`Stoic`＝回復も強化も受け付けない）には一切届かないが、
    /// これは damage 側で消費されるだけなので届く。
    /// 「誰の助けも届かない」駒に唯一届く支援、という位置づけ。
    ///
    /// 減衰も上限も持たせていない。供給源が「砕け盾のヒビが範囲攻撃を浴びること」だけに
    /// 限られていて、ヒビのHPという有限プールがそのまま天井になるため。
    /// </summary>
    public const string Armor = "armor";

    /// <summary>
    /// 傷。攻撃1発につき1つ刻まれる。**ダメージ量に依存しない**（38の一撃も1の刺しも傷1）。
    ///
    /// 毒と違い自分では何もしない——読み手がいて初めて意味を持つ、純粋な盤面の記録。
    /// <see cref="BattleContext.TickStatuses"/> には**何も足していない**。時間で進行しないことが
    /// 毒との分岐点で、手数が無ければ完全に不活性なまま終わる。
    ///
    /// 減衰なし・上限なし。供給が「裂きの保持者が主目標を殴る」＝1ターン1つに限られるので、
    /// 伸びは戦闘ターン数に対して線形。毒（層が二次関数で伸びる）と同じ穴には落ちない。
    /// 量に比例させないのも同じ理由で、比例させた瞬間に「強い駒がもっと強くなる」乗算になる
    /// （<see cref="PyreTrait"/> がロスター唯一の例外として記録されている形）。
    /// </summary>
    public const string Wound = "wound";

    /// <summary>
    /// 深手（第93期・<see cref="DeepRule"/>）。<b>傷が <see cref="DeepRule.Bundle"/> に達すると
    /// 傷を 0 に戻して立つ、0 か 1 の二値。</b>
    ///
    /// <para><b>「消えた」のではなく「器が変わった」。</b> だから<b>傷の読み手から見ると
    /// 傷を1つ持っている扱い</b>にする（<see cref="BattleContext.WoundDepthOf"/>）——
    /// これが無いと、深手化した瞬間に 抉り・断ち・縫い・継ぎ当て・ミオの着火・滲み則が
    /// <b>全部止まる</b>（現象としても説明がつかない）。
    /// <b>深さは 3 と数えない</b>——深手化した瞬間に読み手の出力が3倍になるのを避ける。</para>
    ///
    /// <para>払い出しは2つ。<b>自傷</b>（深手を持つ駒が実際に行動したら
    /// <see cref="DeepRule.DeepBite"/> の自傷・<c>lethal: true</c>）と、
    /// <b>上乗せ</b>（深手の上に新しく書かれた傷は溜まらず、その場で同じ量のダメージになる）。
    /// <b>この期では解けない</b>（解除を同じ期に足すと変数が2つになる）。</para>
    /// </summary>
    public const string Deep = "deep";

    /// <summary>
    /// 呪い（第96期・<see cref="CurseRule"/>）。<b>0 か 1 の二値。重ねない。</b>
    ///
    /// <para><b>単体では何も起こさない。</b> ダメージもデバフも無い純粋な盤面の記録で、
    /// <see cref="BattleContext.TickStatuses"/> には<b>何も足していない</b>——時間で進行しない
    /// （<see cref="Wound"/> と同じ分岐点）。<b>読み手は engine の共有の段1箇所だけ。</b></para>
    ///
    /// <para>意味は<b>「繋がっている」</b>であって「汚れている」ではない。だから
    /// <see cref="SoakRule.Kinds"/>（なまりが読む汚れ 5 本）には<b>足していない</b>
    /// ——足すと第96期 (R1) の「改名であって機構の変更ではない」が破れる。
    /// 一方 <see cref="ScapegoatTrait.Kinds"/> は<b>除外を並べる作法</b>なので自動で 6 → 7 本になる
    /// （業は <c>UnitCatalog.All</c> にも <c>EnemyCatalog.Stages</c> にも居ないので盤面は動かない）。</para>
    ///
    /// <para><b>剥がれない。</b> 解除を同じ期に足すと変数が2つになる。ただし
    /// <b>呪い持ちが何体いるかで効果が変わる</b>ので、第93期の深手（一度なると効果が固定）とは違う。</para>
    /// </summary>
    public const string Curse = "curse";

    /// <summary>
    /// 全キーの一覧。会戦（Engagement）が部隊戦の境界で状態異常を一律に消すために使う
    /// （状態異常は Battle スコープ、という寿命規則。Armor も含めて消す——破片は
    /// Battle 内の供給に依存するプール）。**新しいキーを足したら必ずここにも足すこと。**
    /// </summary>
    /// <summary>
    /// 転倒（第143期・<see cref="BraceTrait"/>）。<b>次の手番を1回だけ失う。0 か 1 の二値。</b>
    ///
    /// <para><b>痺れ（<see cref="Stun"/>）を流用しない。</b> 痺れには読み手がいる
    /// （責め苦のシガ「縛られた敵しか殴れない」ほか）ので、<b>味方の転倒が痺れの帳簿に混ざる</b>
    /// ——別の出来事を同じキーに積むと、帳簿が閉じなくなる。</para>
    ///
    /// <para><b>まどろみ（第36期）とまったく同じ形で engine が立てる。</b>
    /// <see cref="IdleTurn"/> を立てて手番を潰すだけで <c>CanAct</c> は1つも false にしないので、
    /// <c>Trait.SurrenderedTurn</c> が真のまま通り、<b>号令（ガン）・据え（バン）が買い取れる</b>
    /// ——それがこの代金の狙いである。<c>CanAct</c> のオーバーライドで書くと
    /// 不動（カド）・追い打ち（ハギ）と同じ扱いになって買い手が消える。</para>
    ///
    /// <para><b>ターン外の行動は失わない。</b> <see cref="IdleTurn"/> は <c>CanReact</c> を
    /// 1ビットも見ないので、軋み（ヨミ）の割り込みはその場で走る——落ちるのは次の通常の手番だけ。</para>
    /// </summary>
    public const string Stagger = "stagger";

    /// <summary>
    /// 混乱（第146期・<see cref="ConfusionRule"/>）。<b>次の1回の攻撃を自軍に向ける。0 か 1 の二値。</b>
    ///
    /// <para>立つのは <c>SwapSlots</c> の通知1箇所——<b>誰が動かしたかを問わない</b>
    /// （自分で逃げても引きずり出されても「動かされた」は同じ）。落ちるのは
    /// <c>PerformAttack</c> が標的を解決した直後で、<b>1回で落ちる</b>。</para>
    ///
    /// <para><b>痺れ・転倒を流用しない。</b> どちらにも読み手がいる（痺れ＝深追い・背かれ・尾灯／
    /// 転倒＝据え・号令が買う手番）ので、混乱がその帳簿に混ざる。</para>
    ///
    /// <para><b>ターン外の行動は混乱しない。</b> 棘・仇討ち・軋み・追い打ち・譲渡は
    /// <c>PerformAttack</c> を通らない（反撃は <c>ctx.Reaction</c>、割り込みは <c>ctx.Interrupt</c>）ので、
    /// 混乱が乗るのは<b>通常の手番の一振りだけ</b>。</para>
    /// </summary>
    public const string Confused = "confused";

    /// <summary>
    /// 預かり（第153期・<see cref="WardTrait"/>）。<b>後から本人へ返すために積んである HP。</b>
    ///
    /// <para><b>積むときは <see cref="BattleContext.Heal"/> を通らず、返すときだけ通る。</b>
    /// だから<b>渇き（第三波）の下では積まれ続けて1点も返らない</b>——
    /// 「渇きの祭司を先に割れば預かりが一気に戻る」という回避判断がここから出る。
    /// <b>engine には何も足していない。既存の構造がそのまま出るだけである。</b></para>
    ///
    /// <para><b>破片（<see cref="Armor"/>）を流用しない。</b> あちらは <c>ApplyDamage</c> の側で
    /// 消費される damage のプールで、預かりは <c>Heal</c> を通る別資源
    /// ——混ぜると帳簿が閉じない（第143期に転倒が痺れを流用しなかったのと同じ理由）。</para>
    ///
    /// <para><b>減衰も上限も無い。</b> 供給が被弾に縛られているので自然に止まる。
    /// <b>返るのは実際に HP が増えた分だけ</b>で、満タン・渇き・支援拒否（<c>Stoic</c>）で
    /// 入らなかった分はプールに残る（＝捨てない）。</para>
    /// </summary>
    public const string Ward = "ward";

    /// <summary>
    /// 負債（第155期・<see cref="IndulgenceTrait"/>）。<b>前借りで受け取った HP の残高。</b>
    ///
    /// <para><b>積むのは「実際に増えた HP」だけ</b>——満タン・渇き・支援拒否（<c>Stoic</c>）で
    /// 入らなかった分は負債にもならない。だから<b>渇き（第三波）では前借りも取り立ても起きず、
    /// 保持者が無害化されるだけ</b>で、第153期の預かり（積むだけ積んで返らない丸損）にはならない。</para>
    ///
    /// <para><b>預かり（<see cref="Ward"/>）と流用しない。</b> あちらは <c>Heal</c> で返す側のプールで、
    /// こちらは <c>ApplyDamage</c> で取り立てる側の残高——<b>符号が逆で、混ぜると帳簿が閉じない。</b></para>
    ///
    /// <para><b>増える経路は前借りの1本きり。</b> だから積んだ直後に閾値を見れば取りこぼさない。</para>
    /// </summary>
    public const string Debt = "debt";

    /// <summary>
    /// 灰（第179期・<see cref="AshTrait"/>）。<b>味方が味方から受けたダメージの総量。</b>
    ///
    /// <para><b>破片（<see cref="Armor"/>）を流用しない。</b> あちらは <c>ApplyDamage</c> の側で
    /// 消費される防御のプールで、こちらは<b>撃つための燃料</b>——混ぜると帳簿が閉じない
    /// （第143期に転倒が痺れを流用しなかったのと同じ理由）。</para>
    ///
    /// <para><b>減衰も上限も無い。</b> 供給が「味方が味方を傷つけること」に縛られていて、
    /// その供給源はこちらの編成が選んだ駒だけなので、<b>天井は編成が自分で決める</b>。</para>
    ///
    /// <para><b>溜まるのは保持者が生きている間だけ。</b> 倒れた瞬間に
    /// <see cref="AshTrait.OnDeath"/> が隣接する味方へ等分で落とし、0 に戻す。</para>
    /// </summary>
    public const string Ash = "ash";

    public static readonly string[] All = { Poison, Marked, Stun, Burn, IdleTurn, Armor, Wound, Deep, Curse, Stagger, Confused, Ward, Debt, Ash };

    /// <summary>
    /// キーの表示名。<b>ログと診断が同じ名前を使うためだけ</b>にある（規則は1つも読まない）。
    /// <see cref="BattleContext"/> のスナップショット用ラベルと重複するが、あちらは
    /// 「台本に載せる継続効果」の一覧で、こちらは全キーの索引——目的が違うので分けてある。
    /// </summary>
    public static string LabelOf(string key) => key switch
    {
        Poison => "毒",
        Marked => "標",
        Stun => "痺",
        Burn => "燃",
        IdleTurn => "手番",
        Armor => "破片",
        Wound => "傷",
        Deep => "深手",
        Curse => "呪",
        Stagger => "転",
        Confused => "乱",
        Ward => "預",
        Debt => "負",
        Ash => "灰",
        _ => key
    };
}

/// <summary>
/// 燃焼の規則。毒と対になる「積み上がらない持続ダメージ」。
///
/// 毒が層を積んで二次関数で伸びるのに対し、燃焼は固定量・非スタックで残りターンだけを持つ。
/// 再付与は量ではなく持続を更新する。
///
/// **非スタックにしたのは、これを「低火力の駒でも払い続けられる上限つきのコスト」に
/// するため。** 味方に毒を積む案は二度とも壊滅している（ミオの検証で 毒+耐久 が
/// 94/98/99/78 → 23/12/0/0、グザ×ムド は第4波以降 0%）。どちらも層が減衰せず、
/// 味方側の累積が二次関数で伸びたのが原因。燃焼はその形を構造的に避ける。
///
/// **持続を必ず持たせること。** 永続にすると撒いた時点で盤面が飽和し、出力が
/// 「撒き役がいるかどうか」だけで決まる。ミオの澱みが没になったのと同じ穴になる。
/// </summary>
public static class BurnRules
{
    /// <summary>1ターンあたりの固定ダメージ。層に依存しない。</summary>
    public const int Damage = 6;

    /// <summary>着火時に設定される残りターン。再付与でここまで戻る（加算しない）。</summary>
    public const int Turns = 3;
}

/// <summary>
/// ターン外の行動の呼び出し口（第134期 段2）。<b>計数専用で、どの規則も読まない</b>
/// ——<see cref="BattleContext.CanActOutOfTurn"/> の答えを1ビットも変えない。
///
/// <para><b>呼び出し口は5本</b>。<c>CLAUDE.md</c> は第27期以来「棘・仇討ち・軋み・追い打ちの
/// 4本だけ」と書いているが、<b>第110期の譲渡（尾灯・<c>TaillightTrait</c>）が5本目として
/// 増えている</b>（第134期 Q0-7 の走査で判明）。</para>
/// </summary>
public enum OutOfTurnRoute
{
    /// <summary>棘（<c>ThornsTrait.OnDamaged</c>）。</summary>
    Thorns,
    /// <summary>仇討ち（<c>AvengeTrait.OnAllyDamaged</c>）。</summary>
    Avenge,
    /// <summary>軋み（<c>DisplacedTrait.OnMoved</c>）。</summary>
    Creak,
    /// <summary>追い打ち（<c>PursuerTrait.OnAnyDeath</c>）。</summary>
    Pursue,
    /// <summary>譲渡（<c>TaillightTrait</c>・第110期）。</summary>
    Taillight,
    /// <summary>呼び出し口を名乗らなかった問い合わせ（既定値。<b>現状 0 件</b>）。</summary>
    Other
}

/// <summary><see cref="OutOfTurnRoute"/> の一覧（帳簿の添字用）。</summary>
public static class OutOfTurnRoutes
{
    /// <summary>経路の名前（<see cref="OutOfTurnRoute"/> の順）。</summary>
    public static readonly string[] Names =
        { "棘", "仇討ち", "軋み", "追い打ち", "譲渡", "その他" };

    /// <summary>経路の数。</summary>
    public static int Count => Names.Length;
}

/// <summary>
/// 1体ぶんの手番で何が起きたか（第104期）。<see cref="BattleContext.TakeTurn"/> の戻り値で、
/// <b>盤面には一切影響しない</b>——再行動（<c>EncoreRule</c>）の内訳（§3-3 の Q4）を
/// 前後の差分ではなく直接数えるためだけにある。
/// </summary>
public enum TurnOutcome
{
    /// <summary>痺れ・まどろみ・<c>CanAct</c> 偽で潰れた（手番を1つも使っていない）。</summary>
    Stalled,
    /// <summary>通常攻撃を振った（<c>Actions</c> 無しの従来経路と <c>ActionKind.Attack</c>）。</summary>
    Attack,
    /// <summary>術を撃った（<c>ActionKind.Skill</c>）。</summary>
    Skill,
    /// <summary>力を溜めた（<c>ActionKind.Charge</c>）。</summary>
    Charge
}

public sealed class BattleContext
{
    /// <summary>反撃処理の最中か。反撃が反撃を呼ぶ無限連鎖を止めるために見る。</summary>
    public bool InReaction { get; private set; }

    public void Reaction(Action body)
    {
        if (InReaction) return;
        InReaction = true;
        try { body(); }
        finally { InReaction = false; }
    }

    /// <summary>
    /// 割り込み攻撃（ターン外の攻撃）の最中か。割り込みの中で起きた移動が
    /// さらなる割り込みを生む再入を止めるために見る。反撃（Reaction）とは別の連鎖なので別フラグ。
    ///
    /// 戦闘ごとの状態として BattleContext に置く。Trait は全戦闘で共有されるシングルトンで、
    /// static に持つと layout モード（Parallel.For で戦闘を並列実行）で別の戦闘同士が
    /// 互いの割り込みを止め合い、結果が非決定的になる。
    /// </summary>
    public bool InInterrupt { get; private set; }

    /// <summary>
    /// ターン外の攻撃（割り込み・追い打ち）が通るか。無力化されている駒はターン外でも振れない。
    ///
    /// 痺れカウンタはここでは消費しない。割り込みは相手のターン中に起きるので、
    /// ここで消すと本人のターンが回ってくる前に縛めが解けてしまう。
    ///
    /// <para>粛（<see cref="HushTrait"/>）: 保持者が盤上に生きている間、ここが全員に対して閉じる。
    /// <b>両陣営にかかる。</b> 非対称なのは「こちらはそのルールを知って編成を組めるが、
    /// 敵は組めない」点だけ（逆位・渇き・軛と同じ）。</para>
    ///
    /// <para><b>保持者の探索を最後に置く</b>のは、既存の条件で落ちる場合に走らせないため
    /// （<c>&amp;&amp;</c> は短絡する。layout は数百万戦を並列で回す）。軛が
    /// <c>amount &gt; Cap</c> を先に見るのと同じ理由。</para>
    ///
    /// <para><b>止まるのはここを通る4本だけ</b>（棘・仇討ち・軋み・追い打ち）。肩代わり
    /// （庇う・分かち・巨躯・後備え・棘守り）はダメージの再分配であって行動ではないので、
    /// この窓口を通らない＝粛の下でも働く。責め苦（シガ）の追撃も自分の手番の中なので無風。</para>
    /// </summary>
    /// <param name="route">
    /// どの経路からの問い合わせか（第134期 段2・<b>計数専用。答えは1ビットも変えない</b>）。
    /// <b>呼び出し口は5本</b>——棘・仇討ち・軋み・追い打ちの4本に、第110期の譲渡（尾灯）が加わっている。
    /// </param>
    public bool CanActOutOfTurn(UnitState u, OutOfTurnRoute route = OutOfTurnRoute.Other)
    {
        // **式のままだと「粛が単独の原因だったか」が数えられない**ので、第134期に
        // 節へほどいた。**評価の順序も結果も第27期から1ビットも変えていない**——
        // 保持者の走査は `AllUnits.Any(...)` から `_hushHolders`（`Add` が積む）へ寄せてあり、
        // 短絡の意味（数百万戦を並列で回すので全駒走査を後ろに置く）はそのまま残る。
        bool basic = u.IsAlive
                     && u.RawCounter(StatusKeys.Stun) == 0
                     && u.Traits.All(t => CanReactProbed(t, u));
        bool hushed = Hush.Active && HushHolderAlive;

        HushAskedSide[SideOf(u)]++;
        if (hushed) NoteHushBlocked(u, route, sole: basic);

        return basic && !hushed;
    }

    /// <summary>第94期 (T2)。<see cref="Trait.CanReact"/> を印つきで問う。<b>答えは1ビットも変えない。</b></summary>
    bool CanReactProbed(Trait t, UnitState u)
    {
        TraitMark m = BeginTrait(t.Id, u);
        bool ok = t.CanReact(this, u);
        EndTrait(m);
        return ok;
    }

    /// <summary>第94期 (T2)。<see cref="Trait.CanAct"/> を印つきで問う。<b>答えは1ビットも変えない。</b></summary>
    public bool CanActProbed(Trait t, UnitState u, ActionKind kind)
    {
        TraitMark m = BeginTrait(t.Id, u);
        bool ok = t.CanAct(this, u, kind);
        EndTrait(m);
        return ok;
    }

    /// <summary>
    /// 第113期。<b>そのターン、その駒が自分の手番で行動できるか</b>を<b>静かに</b>問う（観測専用）。
    /// 灯の動的な濾し（<see cref="LitFilter.ActingNow"/>）だけが呼ぶ。
    ///
    /// <para><b>答えは行動順ループと1ビットも違わない</b>——同じ種別（<c>CurrentAction</c> の
    /// 種別。持たない駒は <see cref="ActionKind.Attack"/>）で同じ <c>CanAct</c> を問う。
    /// <b>印とログを落とす</b>のは、この問い合わせが観測を汚さないため
    /// （<c>Trait.SurrenderedTurn</c> の呼び出し口と同じ作法・第103期）
    /// ——のろまと断ちが <c>CanAct</c> の中でログを出す。<b>乱数は1つも引かない。</b></para>
    ///
    /// <para><b>痺れ・まどろみはここでは見ない。</b> あの2つは <c>TakeTurnCore</c> が
    /// <c>CanAct</c> より前に engine 側で弾いており、<b>痺れは弾いた瞬間に 0 へ戻す</b>ので、
    /// 「そのターン潰れるか」を完全に写すことはできない。<b>写せるのは <c>CanAct</c> の層だけ</b>
    /// ——濾しの定義（指示書 §0-3 の (C)）もその層に置いてある。</para>
    /// </summary>
    public bool CanActNow(UnitState u)
    {
        ActionKind kind = u.CurrentAction?.Kind ?? ActionKind.Attack;
        TraitMark saveMark = Mark; Mark = default;
        bool wasQuiet = _quiet; _quiet = true;
        bool ok = true;
        foreach (Trait t in u.Traits) if (!t.CanAct(this, u, kind)) { ok = false; break; }
        _quiet = wasQuiet; Mark = saveMark;
        return ok;
    }

    public void Interrupt(Action body)
    {
        if (InInterrupt) return;
        InInterrupt = true;
        try { body(); }
        finally { InInterrupt = false; }   // 例外で立ちっぱなしになると以後の割り込みが永久に止まる
    }

    /// <summary>
    /// 突き返し（<see cref="ShoveTrait"/>）の最中か。効果Aは<b>敵陣</b>を動かすので
    /// 現状は再帰しないが、<b>敵側に突き返しを持たせた瞬間に無限再帰する</b>
    /// （こちらが敵を動かす → 敵の突き返しがこちらを動かす → …）。
    /// 1ターン1回の上限だけに頼らず、反撃・割り込みと同じ形のガードを1つ置く。
    ///
    /// <para>反撃（<see cref="InReaction"/>）とも割り込み（<see cref="InInterrupt"/>）とも
    /// 別の連鎖なので別フラグ。<b>static に持たないこと</b>——Trait は全戦闘で共有される
    /// シングルトンで、layout モードは戦闘を並列実行する。</para>
    /// </summary>
    public bool InShove { get; private set; }

    public void Shoving(Action body)
    {
        if (InShove) return;
        InShove = true;
        try { body(); }
        finally { InShove = false; }   // 例外で立ちっぱなしになると以後の突き返しが永久に止まる
    }

    /// <summary>毒などの継続ダメージ。ターン開始時に engine から呼ばれる。</summary>
    public void TickStatuses()
    {
        NoteRuleHolders();   // 第134期 段2 —— 保持者が落ちたターンの記録。**盤面には触らない。**

        foreach (UnitState u in _units.Where(x => x.IsAlive).ToList())
        {
            int poison = u.RawCounter(StatusKeys.Poison);
            if (poison <= 0) continue;

            // 毒喰らい（ベニ）は澱みを啜って癒す代わりに、味方が負った毒をより深く効かせる。
            // 回復量は「毒に侵された敵の数」に比例するので敵が減るほど落ちるが、
            // 味方の毒は瘴気で積み上がり続ける。**時間が経つほど収支が反転する。**
            // 減衰を外から与えなくても、二つの伸びる量の競争として自然に出る形。
            // 浄化（増分と引き算する）と違って閾値で全ゼロにならず、倍率が傾斜として効く。
            if (_units.Any(x => x.IsAlive && x.TeamId == u.TeamId && x.HasTrait(TraitId.Devour)))
                poison *= DevourTrait.AllyPoisonMultiplier;
            Log($"    {u.Name} は毒に蝕まれている（{poison}）", LogKind.Status);
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.Status,
                Turn = _turn,
                TargetId = u.InstanceId,
                Amount = poison,
                Text = "毒"
            });
            // 業（第49期）の帰属。**保持者が盤上にいなければ1回も走らない**（短絡）。
            if (ScapegoatActive) NoteScapegoatDot(u, poison, StatusKeys.Poison);
            NotePoisonBite(u, poison);   // 第61期の計数。盤面には触らない

            // 傷口の着火の帰属（第87期・持続係数の分子）。**盤面には触らない。**
            // 着火の時点でこの駒の毒は 0 だったので、以後の刻みはすべて着火の下流にある。
            if (u.RawCounter(AmplifierTrait.IgnitedKey) > 0)
            {
                UnitTally it = TallyOf(u);
                it.IgnitePoisonDamage += poison;
                it.IgnitePoisonTicks++;
            }

            ApplyDamage(u, poison, null);
        }

        // 燃焼は毒とは別のループで回す。固定量なので増幅も変換もされず、
        // 残りターンを減らすだけ。毒の後に置いてあるのは、同じターンに両方を負った駒が
        // 「積み上がる方」で先に落ちるようにするため（燃焼のほうが後から効く）。
        foreach (UnitState u in _units.Where(x => x.IsAlive).ToList())
        {
            int left = u.RawCounter(StatusKeys.Burn);
            if (left <= 0) continue;

            // 燃焼の計数（第57期）。**盤面には触らない。**
            UnitTally bt = TallyOf(u);
            bt.BurnTicks++;

            u.SetCounter(StatusKeys.Burn, left - 1);
            // 第134期 段1 —— 燃え尽きた時点で区間を閉じる。**盤面には触らない。**
            if (left - 1 <= 0) CloseBurnEpisode(u, expired: true);

            // 火には焼かれない（第178期・熾のホタ）。**燃焼の状態は1ビットも消さない**
            // ——残りターンは上で普通に減り、攻 ×4・貫き（`PyreTrait`）も今までどおり立つ。
            // 変わるのは「この刻みが HP を削るか」の1点だけである。
            //
            // **`ApplyDamage` の中ではなく、ここで切る。** あちらで切ると
            // 「浴びた量」を読む札（被弾強化・砕け・分かち・逆しま…）が
            // 「0 を浴びた」で発火し、帳簿（`BurnTaken` / 破片の吸い）にも段が残る。
            // **刻みそのものを起こさない**のが「焼かれない」の正しい形。
            //
            // `Ember.Fireproof` は **既定 true ＝ 採用した版**。偽にすると第177期までの盤面に戻る
            // （`EmberRule.Charred`。自己検査 (a) がそれで 305 セルを突き合わせる）。
            // 保持者はロスターに熾のホタ1枚だけなので、他の 51 枚は比較1つで抜ける。
            if (Ember.Fireproof && u.HasTrait(TraitId.Pyre))
            {
                Log($"    {u.Name} は燃えているが焼かれない（残り {left - 1}）", LogKind.Status);
                // ノブ（既定 0 ＝ 1ビットも動かない）。**`ctx.Heal` を通す**ので、
                // 渇き（盤面ルール）にも支援拒否（`Stoic`）にも素直に課税される。
                if (Ember.TickHeal > 0) Heal(u, Ember.TickHeal);
                continue;
            }

            Log($"    {u.Name} が燃えている（残り {left - 1}）", LogKind.Status);
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.Status,
                Turn = _turn,
                TargetId = u.InstanceId,
                Amount = BurnRules.Damage,
                Text = "燃焼"
            });
            if (ScapegoatActive) NoteScapegoatDot(u, BurnRules.Damage, StatusKeys.Burn);
            // burnTick: この刻みが破片に吸われた量・HP を削った量を、
            // 毒の刻み（同じく source が null）と混ぜずに数えるための札。**盤面には影響しない。**
            ApplyDamage(u, BurnRules.Damage, null, burnTick: true);
            if (!u.IsAlive) bt.BurnDeaths++;
        }
    }

    /// <summary>
    /// 傷を積む唯一の窓口（第93期）。<b>束ね（<see cref="DeepRule"/>）の入口だけを担う。</b>
    ///
    /// <para><b>通すのは加算だけ。</b> 減算（断ちの 0 戻し・縫いと継ぎ当ての塞ぎ）と
    /// 引き取り（<c>GatherRule</c>）の <b>donor 側</b>は通さない
    /// ——<b>受け取る側は通す</b>（3 に届けば深手になる）。第90期の毒の窓口と同じ作法。</para>
    ///
    /// <para><b>計数は規則の分岐より手前。</b> 紙の分子（§1-1 の門）を W0 の実測から取るため、
    /// 「傷が <see cref="DeepRule.Bundle"/> に達した回数」「達した駒にさらに書かれた回数」は
    /// <b>版に依らず数える</b>（第86期の X1P・第90期の作法）。</para>
    ///
    /// <para><b>ログの文言は各特性の側に残す</b>——深手になったときだけここで1行足す。</para>
    /// </summary>
    /// <param name="writer">書いた駒（計数の帰属先。<b>盤面には一切影響しない</b>）。</param>
    /// <returns>
    /// 書き込み後の傷の数。<b>深手になった／上乗せに化けた場合は −1</b>
    /// （呼び出し側は「傷 N」のログを出さない）。書けなかったときも −1。
    /// </returns>
    public int Wound(UnitState target, int amount, UnitState writer, WoundRoute route)
    {
        if (!target.IsAlive || amount <= 0) return -1;
        // 第120期の対照（`WoundRule.Enabled = false`）。**計数より手前で返す**
        // ——「傷を丸ごと外したら何が壊れるか」を測るので、傷は 1 つも書かれてはいけない
        // （自己検査 (b)）。既定は真なので通常の実行では素通りする。
        if (!Wounds.Enabled) return -1;

        // **計数は規則の分岐より手前**（版に依らない）。
        UnitTally wt = TallyOf(writer);
        wt.WoundWrites += amount;
        (wt.WoundWritesByRoute ??= new int[WoundRouteCount])[(int)route] += amount;

        UnitTally tt = TallyOf(target);
        int cur = target.RawCounter(StatusKeys.Wound);
        // §1-1 の門の 3: **すでに Bundle に達したことのある駒**にさらに書かれた回数（＝上乗せの機会）。
        if (target.RawCounter(DeepRule.ReachedKey) > 0) tt.DeepOnTop++;

        if (Deep.Enabled && target.RawCounter(StatusKeys.Deep) > 0)
        {
            // 上乗せ。傷は溜まらず、その場でダメージになる。**巻き込み則を通さない**（閉じたループを作らない）。
            tt.DeepOverFires += amount;
            tt.DeepOverOut += amount * DeepRule.DeepBite;
            Log($"    {target.Name} の深手に {writer.Name} の刃がそのまま通る（{amount * DeepRule.DeepBite}）", LogKind.Status);
            for (int i = 0; i < amount; i++)
                ApplyDamage(target, DeepRule.DeepBite, writer, spillWound: false, deepBite: true);
            return -1;
        }

        int w = cur + amount;
        // §1-1 の門の 1: **Bundle に達した回数**（版に依らない。W0 でも数える）。
        if (cur < DeepRule.Bundle && w >= DeepRule.Bundle)
        {
            tt.DeepReach++;
            if (tt.DeepReachFirstTurn == 0) tt.DeepReachFirstTurn = Math.Max(1, Turn);
            tt.DeepReachTurnSum += Math.Max(1, Turn);
            target.SetCounter(DeepRule.ReachedKey, 1);
            DeepWatch = true;
        }

        if (Deep.Enabled && w >= DeepRule.Bundle)
        {
            // **余りを繰り越さない**（深手は二値なので繰り越す先が無い）。
            NoteWoundLoss(target, cur, WoundLoss.Bundle);   // 第120期の帳簿（既定では走らない）
            target.SetCounter(StatusKeys.Wound, 0);
            target.SetCounter(StatusKeys.Deep, 1);
            tt.DeepBundles++;
            if (tt.DeepBundleFirstTurn == 0) tt.DeepBundleFirstTurn = Math.Max(1, Turn);
            Log($"    {target.Name} の傷が束ねられて深手になった（傷 {w} → 深手）", LogKind.Highlight, writer);
            EmitStatusGain(target, StatusKeys.Deep, 1, writer);   // 第97期・表示専用
            // 第104期: **束ねは「書けた」側**（深手は WoundDepthOf / IsWounded が傷として読む）。
            NoteWoundWriter(target, writer);
            FireSutureOnWound(target);   // 第107期 (S3)。既定（Swing）では素通りする
            return -1;
        }

        target.SetCounter(StatusKeys.Wound, w);
        // 第120期。**書かれた側の陣営**で割る（書き手の帰属は `WoundWritesByRoute` の側にある）。
        (target.TeamId == PlayerTeam ? WoundWriteAlly : WoundWriteFoe)[(int)route] += amount;
        // 在庫の齢（計数専用）。**`Census` のときだけ書く**——既定では counter を1つも足さない
        // （`SetCounter` は `NoteStatusGain` を呼ぶので、既定の経路に1本も枝を増やさない）。
        if (WoundCensus && cur == 0) target.SetCounter(WoundSinceKey, Math.Max(1, Turn));
        EmitStatusGain(target, StatusKeys.Wound, amount, writer);   // 第97期・表示専用
        NoteWoundWriter(target, writer);   // 第104期。**版に依らない記録。盤面には影響しない**
        FireSutureOnWound(target);   // 第107期 (S3)。既定（Swing）では素通りする
        return w;
    }

    /// <summary>
    /// 縫いを<b>手番の外</b>から走らせる（第107期 (S3)・<see cref="SutureFire.OnWound"/>）。
    /// <b><see cref="Wound"/> が実際に傷を書いたときだけ</b>呼ぶ——engine が傷を読む窓口
    /// （第90期の滲み則）と同じ層で、<b>新しい窓口は1つも作っていない</b>。
    ///
    /// <para><b>順序はスロット昇順</b>（<c>ctx.PickOne</c> を使わない——候補2個以上で <c>Roll</c> を
    /// 消費して乱数列が動く。第89期 (h)）。<b>陣営を問わない</b>——engine の窓口は両側に開いている。</para>
    ///
    /// <para><b>再入ガード</b>（<c>_suturingOnWound</c>）。縫いは <c>ctx.Heal</c> と塞ぎしか呼ばないので
    /// 現状は再帰しないが、<b>将来「縫いが傷を書く」経路ができた瞬間に無限再帰する</b>ので、
    /// 1ターン1回の上限だけに頼らない（突き返しの `Shoving` と同じ判断）。</para>
    ///
    /// <para><b>ログの但し書き</b>: 塞ぎが走ると呼び出し側が持っている <c>w</c> が1つ古くなる
    /// （「傷 N」の行が実際より1多く出る）。<b>盤面には影響しない</b>——値は書いた瞬間の真値で、
    /// 塞いだことは縫いの行に別に出る。<c>verbose</c> のときだけ見える表示上の順序の話。</para>
    /// </summary>
    private bool _suturingOnWound;

    private void FireSutureOnWound(UnitState wounded)
    {
        // プロパティ名が列挙型名を隠すので `global::` で当てる（型を改名しないための1文字）。
        if (SutureFire.Fire != BattleCore.SutureFire.OnWound) return;
        if (_suturingOnWound) return;
        _suturingOnWound = true;
        try
        {
            foreach (UnitState u in AllUnits.Where(x => x.IsAlive && x.HasTrait(TraitId.Suture))
                                            .OrderBy(x => x.TeamId).ThenBy(x => x.Slot).ToList())
                foreach (Trait t in u.Traits.ToList())
                    if (t is SutureTrait st)
                    {
                        // **第94期 (T2) の印を立ててから呼ぶ**——立てないと `ctx.Heal` の帰属が
                        // 「誰のものでもない出力」に落ちて、第105期の器具（`tempo`）でハリの回復が
                        // まるごと消える（Q6 が測れなくなる）。engine が特性を直に呼ぶ箇所は
                        // すべてこの形（`CanReactProbed` / `CanActProbed` と同じ）。
                        TraitMark m = BeginTrait(t.Id, u);
                        try { st.Fire(this, u, wounded); }
                        finally { EndTrait(m); }
                    }
        }
        finally { _suturingOnWound = false; }
    }

    /// <summary><see cref="UnitTally.WoundWritesByRoute"/> の長さ（<see cref="WoundRoute"/> の要素数）。</summary>
    public const int WoundRouteCount = 6;

    /// <summary>
    /// 傷の読み手から見た深さ（第93期）。<b>深手は「傷1つぶん」として読む</b>
    /// ——深さを 3 と数えると深手化した瞬間に読み手の出力が3倍になる。
    /// <para><b>規則が無効なら <see cref="StatusKeys.Deep"/> を1度も引かない</b>ので、
    /// 既定では辞書の参照回数も変わらない。</para>
    /// </summary>
    public int WoundDepthOf(UnitState u)
        // **`Counter` を使う**（第94期 (T2)）——ここは engine の内部ではなく
        // **傷の読み手が傷を読む窓口**なので、どの特性が読んだかを観測する。
        => u.Counter(StatusKeys.Wound) + (Deep.Enabled && u.Counter(StatusKeys.Deep) > 0 ? 1 : 0);

    /// <summary>
    /// 傷の読み手から見て「傷を持っているか」（第93期）。<see cref="WoundDepthOf"/> の二値版。
    /// </summary>
    public bool IsWounded(UnitState u)
        => u.Counter(StatusKeys.Wound) > 0 || (Deep.Enabled && u.Counter(StatusKeys.Deep) > 0);

    /// <summary>
    /// 傷が <see cref="DeepRule.Bundle"/> に達した駒が1体でも出たか（<b>計数専用</b>・版に依らない）。
    /// 行動順ループの計数（§1-1 の門の 2）を、何も起きていない戦闘では1度も走らせないための短絡。
    /// </summary>
    public bool DeepWatch;

    /// <summary>
    /// 深手の自傷（第93期・§2-3）。<b>その駒が実際に行動した直後</b>に呼ぶ
    /// （<c>Attack</c> / <c>Skill</c> / <c>Charge</c> のいずれかを通ったときだけ）。
    /// <para>痺れ・まどろみ・<c>CanAct</c> 偽で <c>IdleTurn</c> が立った駒は<b>自傷しない</b>
    /// ——止められた駒が延命するのは意図した帰結で、回数を <see cref="UnitTally.DeepStalled"/> に出す。</para>
    /// <para><c>lethal: true</c>（既定）。<c>isFriendlyFire</c> は<b>偽</b>——自分で自分を裂いているので
    /// 味方の刃ではない。<c>spillWound: false</c> で<b>深手 → 自傷 → 傷 → 深手</b>の閉じたループを作らない。</para>
    /// </summary>
    internal void NoteDeepAction(UnitState actor)
    {
        // §1-1 の門の 2（版に依らない）: **達した駒がその後に行動した回数** ＝ 自傷が払い出される機会。
        if (actor.RawCounter(DeepRule.ReachedKey) > 0) TallyOf(actor).DeepActs++;

        if (!Deep.Enabled || !actor.IsAlive || actor.RawCounter(StatusKeys.Deep) <= 0) return;

        UnitTally t = TallyOf(actor);
        t.DeepBiteFires++;
        t.DeepBiteOut += DeepRule.DeepBite;
        Log($"    {actor.Name} は動くたびに深手が開く（{DeepRule.DeepBite}）", LogKind.Status);
        ApplyDamage(actor, DeepRule.DeepBite, actor, spillWound: false, deepBite: true);
    }

    /// <summary>
    /// 深手を持つ駒が手番を止められた回数（痺れ・まどろみ・<c>CanAct</c> 偽）。<b>盤面には一切影響しない。</b>
    /// </summary>
    internal void NoteDeepStalled(UnitState actor)
    {
        if (Deep.Enabled && actor.RawCounter(StatusKeys.Deep) > 0) TallyOf(actor).DeepStalled++;
    }

    /// <summary>
    /// 毒を積む唯一の窓口（第90期）。<b>滲み則（<see cref="SoakRule"/>）の入口だけを担う。</b>
    ///
    /// <para><b>通すのは「加算の入口」だけ。</b> 減算（毒喰らいの啜り・澱み喰いの吸い上げ）や
    /// 上書き（澱みの着火・増幅の <c>SetCounter</c>）は通さない
    /// ——<b>ミオは第87期で既に傷を読んでいる</b>ので、ここを通すと二重取りになる（§0-4）。</para>
    ///
    /// <para><b>通る書き手は3枚だけ</b>——瘴気（グザ・敵と味方漏れの両方）／毒撃（スィド・被弾した相手と隣への漏れ）／
    /// 疫み（ラウ）。<b>味方漏れも同じ窓口を通す</b>のが、両陣営に等しくかかるというこの規則の要点。</para>
    ///
    /// <para><b>足すのは定数 1。傷の数に比例させない</b>（自己検査 (c)）。
    /// <b>ログの文言は各特性の側に残してある</b>——差分を読めるようにするため、
    /// 滲みで深くなったときだけ1行足す。</para>
    /// </summary>
    /// <param name="writer">書いた駒（計数の帰属先。<b>盤面には一切影響しない</b>）。</param>
    public void Poison(UnitState target, int amount, UnitState writer, PoisonRoute route)
    {
        if (!target.IsAlive || amount <= 0) return;

        // **計数は規則の分岐より手前**（第86期の X1P と同じ作法）。紙の分子を W0 の実測から取るため、
        // 「傷を持つ相手に書いた回数」は版に依らず数える。
        UnitTally wt = TallyOf(writer);
        wt.SoakPoisonWrites++;
        // 第93期: **深手も「傷を持っている」**（`WoundDepthOf` の二値版）。深手なら +1 ではなく +2。
        bool deepW = Deep.Enabled && target.RawCounter(StatusKeys.Deep) > 0;
        bool wounded = deepW || target.RawCounter(StatusKeys.Wound) > 0;
        if (wounded)
        {
            wt.SoakPoisonSeen++;
            if (target.TeamId == writer.TeamId) wt.SoakPoisonSeenAlly++;
            (wt.SoakSeenByRoute ??= new int[SoakRouteCount])[(int)route]++;
        }

        int add = amount;
        // **深手の数に比例させない**（二値なので比例のしようが無いが、明記しておく）。
        if (Soak.Poison && wounded)
        {
            int bump = deepW ? 2 : 1; add += bump; wt.SoakPoisonAdded++; if (deepW) wt.DeepSoakDeeper++;
            // 第120期。**滲み則だけは単位が違う**（HP ではなく毒の残ターン）ので、
            // 実効の列には載せない（名目だけを数え、単価の分子からは外す）。
            NoteWoundRead(target, WoundReader.Soak, 1, bump, 0);
        }

        target.SetCounter(StatusKeys.Poison, target.RawCounter(StatusKeys.Poison) + add);
        EmitStatusGain(target, StatusKeys.Poison, add, writer);   // 第97期・表示専用（滲みで増えたぶんも込み）
        if (add != amount)
            Log($"    {target.Name} の{(deepW ? "深手" : "傷口")}から毒が滲みた（+{add - amount}）", LogKind.Status);
    }

    /// <summary><see cref="UnitTally.SoakSeenByRoute"/> の長さ（毒 5 経路 ＋ 燃焼 1）。</summary>
    public const int SoakRouteCount = 6;

    /// <summary>燃焼の経路の添字（<see cref="UnitTally.SoakSeenByRoute"/> の末尾）。</summary>
    public const int SoakBurnRouteIx = 5;

    /// <summary>
    /// 巻き込み則（第85期）で最後にこの駒へ傷を書いた駒の <c>InstanceId + 1</c>（第90期の計数専用の札）。
    /// <b>誰も読んで分岐しない。</b> 自己給餌（ボルグの余波 → 傷 → 深い火）の成立を数えるためだけにある。
    /// </summary>
    public const string SpillWoundFromKey = "soakSpillFrom";

    /// <summary>
    /// 着火。非スタックなので、量ではなく残りターンを更新する。
    /// 既に燃えている相手への再付与は持続のリセットにしかならない（ダメージは増えない）。
    /// <para><b>滲み則（第90期・<see cref="SoakRule"/>）はここ1箇所。</b> 相手が傷を持っていれば
    /// 残ターンが +1 される（3 → 4）。<b>点け直しでも同じ</b>——戻す先が 4 になる。
    /// <c>BurnRules.Turns</c> は書き換えない（局所的に +1 するだけ）。</para>
    /// </summary>
    /// <param name="source">火を点けた駒（計数の帰属先。<b>盤面には一切影響しない</b>）。</param>
    public void Ignite(UnitState target, bool friendly = false, UnitState? source = null)
    {
        if (!target.IsAlive) return;

        bool relit = target.RawCounter(StatusKeys.Burn) > 0;

        // 滲み則の計数（第90期）。**規則の分岐より手前**なので版に依らない。
        // 第93期: **深手も「傷を持っている」**（`Soak.Burn` は既定 false なので盤面は動かない）。
        bool wounded = target.RawCounter(StatusKeys.Wound) > 0
                       || (Deep.Enabled && target.RawCounter(StatusKeys.Deep) > 0);
        if (source is not null)
        {
            UnitTally st = TallyOf(source);
            st.SoakBurnWrites++;
            if (wounded)
            {
                st.SoakBurnSeen++;
                if (target.TeamId == source.TeamId) st.SoakBurnSeenAlly++;
                (st.SoakSeenByRoute ??= new int[SoakRouteCount])[SoakBurnRouteIx]++;
                if (Soak.Burn) st.SoakBurnAdded++;
                // 自己給餌（§1-2 の 4）。**同じ駒が味方に傷を書き、その味方に深い火を点けた。**
                if (target.TeamId == source.TeamId && target.RawCounter(SpillWoundFromKey) == source.InstanceId + 1)
                    st.SoakSelfFeed++;
            }
        }

        int turns = BurnRules.Turns;
        if (Soak.Burn && wounded) turns += 1;

        // 燃焼の計数（第57期）。**盤面には触らない。**
        // 「点いた」と「煽られた」を分けるのが要点——非スタックなので後者は
        // 残ターンを 3 に戻すだけで、供給としては捨てられている。
        UnitTally it = TallyOf(target);
        NoteIgnite(target, source, relit);   // 第134期 段1 —— 重ね掛けの帳簿。盤面には触らない
        if (relit)
        {
            it.BurnRelit++;
        }
        else
        {
            it.BurnLit++;
            if (friendly) it.BurnLitAlly++;
            if (it.FirstBurnTurn == 0) it.FirstBurnTurn = _turn;
        }

        target.SetCounter(StatusKeys.Burn, turns);
        EmitStatusGain(target, StatusKeys.Burn, turns, source);   // 第97期・表示専用（量ではなく残ターン）
        Log(relit
                ? $"    {target.Name} の火が煽られた（残り {turns}）"
                : $"    {target.Name} に火が点いた（残り {turns}）",
            friendly ? LogKind.FriendlyFire : LogKind.Status);
        if (turns != BurnRules.Turns)
            Log($"    {target.Name} の傷口に火が回った（残り +1）", LogKind.Status);
    }

    /// <summary>
    /// なまり（<c>AtkBonus</c> の負の側）を <see cref="BattleEventKind.StatusGain"/> に載せるときのキー
    /// （第97期・<b>表示専用</b>）。
    ///
    /// <para><b><see cref="StatusKeys"/> には足していない</b>——なまりは <c>Counters</c> ではなく
    /// <c>AtkBonus</c> に載る量で、キーを足すと会戦の境界の一括消去（<see cref="StatusKeys.All"/>）や
    /// <c>ScapegoatTrait.Kinds</c> の分母が黙って動く。<b>ここは表示の札の名前でしかない。</b></para>
    /// </summary>
    public const string DullKey = "dull";

    public const int MarkPullPercent = 75;

    public const int PlayerTeam = 0;
    public const int EnemyTeam = 1;

    private readonly List<UnitState> _units = new();
    private readonly List<LogLine> _log = new();
    private readonly List<BattleEvent> _events = new();
    private readonly Random _rng;
    private readonly bool _verbose;
    private int _nextInstanceId;

    internal Dictionary<string, int> DamageByUnit { get; } = new();

    /// <summary>
    /// 駒ごとの働きの内訳。**verbose に関係なく数える**（一括シミュレーションで平均を取るため）。
    /// 盤面には触らないので、数えることで戦闘が変わることはない。
    /// </summary>
    internal Dictionary<string, UnitTally> TallyByUnit { get; } = new();

    /// <summary>internal なのは、ターンループ（BattleEngine 側）が溜めを数えるため。</summary>
    internal UnitTally TallyOf(UnitState u)
    {
        if (!TallyByUnit.TryGetValue(u.Def.Id, out UnitTally? t))
            TallyByUnit[u.Def.Id] = t = new UnitTally();
        return t;
    }

    private int _turn;
    private int _enemyKillsThisTurn;

    /// <summary>
    /// 1ターンのうちに味方が倒した敵の数の最大値。「連鎖の深さ」の代理指標として使う。
    /// 撃破のたびに次の反応（追い打ち・墓守の層など）が起きるかどうかは特性ごとに違うが、
    /// 「1ターンで何体畳みかけたか」は特性を問わず一様に測れるので、まずここから見る。
    /// </summary>
    public int MaxEnemyKillsInOneTurn { get; private set; }

    public int Turn
    {
        get => _turn;
        internal set { _turn = value; _enemyKillsThisTurn = 0; }
    }

    /// <summary>
    /// 巨躯の規則。<b>診断（gullet）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="ColossusRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public ColossusRule Colossus { get; }

    /// <summary>
    /// 軛の規則。<b>診断（yoke）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="YokeRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public YokeRule Yoke { get; }

    /// <summary>
    /// 粛の規則。<b>診断（hush）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="HushRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public HushRule Hush { get; }

    /// <summary>
    /// 殉教の規則。<b>診断（guard）が割合を振るためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="MartyrRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public MartyrRule Martyr { get; }

    /// <summary>
    /// 曝きの規則。<b>診断（expose）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="ExposeRule.Default"/> ＝ 無効）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public ExposeRule Expose { get; }

    /// <summary>
    /// この戦闘で実際に引きずり出した回数。<b>保持者ではなく戦闘単位で数える</b>ので、
    /// 保持者が複数いても合算される（<see cref="ExposeRule.MaxPerBattle"/> の残数はここから引く）。
    /// 駒ごとの <c>Counters</c> に置くと合算にならないため、盤面側で持つ。
    /// </summary>
    public int ExposeCount { get; internal set; }

    /// <summary>
    /// 後列または前列が 0 体で何もしなかった回数（空振り）。上限は消費しない。
    /// **発火しなかったことは盤面の値に痕跡を残さない**ので、診断が読むためだけに数える。
    /// </summary>
    public int ExposeMissed { get; internal set; }

    /// <summary>残りの引きずり出し回数。既定（MaxPerBattle = 0）では常に 0 で、走査に入らない。</summary>
    public int ExposesLeft => Expose.MaxPerBattle - ExposeCount;

    /// <summary>
    /// 突き返しの強度。<b>診断（shove）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="ShoveRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public ShoveRule Shove { get; }

    /// <summary>
    /// 突き返しの計数。<b>発火しなかったことは盤面の値に痕跡を1つも残さない</b>ので、
    /// 診断が読むためだけに数える（<c>verbose</c> には依存しない）。
    /// 保持者ではなく<b>戦闘単位</b>で数えるので、保持者が複数いても合算される。
    ///
    /// <para><c>ShoveFired</c> 実際に突き返した回数 ／ <c>ShoveCapped</c> 1ターン1回の上限で
    /// 弾かれた回数 ／ <c>ShoveSwapped</c> 効果A（敵陣の突き崩し）が成立した回数 ／
    /// <c>ShoveNoRow</c> 敵の後列か前列が 0 体で効果Aだけが空振りした回数 ／
    /// <c>ShoveStaggered</c> 効果Bが当たった延べ体数 ／
    /// <c>ShoveBlocked</c> 効果Bが <c>Stoic</c> で弾かれた延べ体数。</para>
    /// </summary>
    public int ShoveFired { get; internal set; }
    public int ShoveCapped { get; internal set; }
    public int ShoveSwapped { get; internal set; }
    public int ShoveNoRow { get; internal set; }
    public int ShoveStaggered { get; internal set; }
    public int ShoveBlocked { get; internal set; }

    /// <summary>
    /// 集約（引き受け）の強度。<b>診断（dull）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="BearRule.Default"/>）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public BearRule Bear { get; }

    /// <summary>
    /// 弱体（<see cref="Dull"/>）の計数。<b>発火しなかったことは盤面の値に痕跡を残さない</b>ので、
    /// 診断が読むためだけに数える（<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><c>DullTotal</c> 窓口を通った総量（両陣営） ／ <c>DullByRoute</c> 経路別の内訳 ／
    /// <c>BearTaken</c> 集約役が引き受けた量 ／ <c>BearPassed</c> 横取りされずに素通りした量 ／
    /// <c>BearArmor</c> 生成したアーマー量 ／ <c>BearSoaked</c> そのうち実際にダメージを吸った量 ／
    /// <c>BearFrom</c> 引き受けた相手の内訳。</para>
    /// </summary>
    public int DullTotal { get; internal set; }
    public int[] DullByRoute { get; } = new int[DullRoutes.Count];

    /// <summary>
    /// 呪い則（第95期）の計数。<b>盤面には一切影響しない</b>（誰も読んで分岐しない・verbose 非依存）。
    /// <c>SoakDullFired</c> 汚れが1種以上あって重くなった回数 ／ <c>SoakDullDry</c> 空振り（汚れ 0 種） ／
    /// <c>SoakDullKinds</c> 種類数の総和（÷ <c>SoakDullFired</c> が倍率） ／ <c>SoakDullAdded</c> 上乗せした量。
    /// </summary>
    public int SoakDullFired, SoakDullDry, SoakDullKinds, SoakDullAdded;

    // =====================================================================================
    // 呪い（第96期・CurseRule）の計数。**盤面には一切影響しない**
    // （誰も読んで分岐しない・verbose 非依存。`SoakDull*` / `ShoveFired` と同じ扱い）。
    //
    // **門（§1-1）は W0 では数えられない**——呪いが1つも書かれないので、
    // 「同陣営に2体以上」が定義上 0 になる。だから門を数えるときは
    // **`CurseRule(true, 0)`**（印は書くが共有量 0）を使う。
    // `ApplyDamage` は `amount <= 0` で即座に返るので、**盤面は W0 と完全に同一のまま**。
    // =====================================================================================

    /// <summary>祟りの保持者（ムド）がダメージを受けた回数＝<b>呪いを配れる機会</b>。
    /// 出どころで割る（敵 / 味方の刃 / 出どころ無し＝毒・燃焼の刻み）。<b>版に依らない。</b></summary>
    public int HexHits, HexHitsFromFoe, HexHitsFromAlly, HexHitsNoSource;

    /// <summary>祟りの保持者を叩いた駒の内訳（<b>味方の刃の全数</b>・敵の全数）。<b>版に依らない。</b></summary>
    public readonly Dictionary<string, int> HexHitByAlly = new();
    public readonly Dictionary<string, int> HexHitByFoe = new();

    /// <summary>実際に呪いが<b>新しく</b>付いた回数（既に付いている相手は数えない）。陣営別。</summary>
    public int HexMarks, HexMarksOnPlayer, HexMarksOnEnemy;

    /// <summary>既に呪われている相手にもう一度書こうとして弾いた回数（自己検査 (b)。<b>二値の証拠</b>）。</summary>
    public int HexReMarkBlocked;

    /// <summary>陣営ごとの<b>2体目の呪いが立ったターン</b>（0 ＝ 一度も立たなかった）。</summary>
    public int HexPairTurnPlayer, HexPairTurnEnemy;

    /// <summary>ターン頭に呪い持ちの生存が2体以上だったターンの数（陣営別）と、その最大値。</summary>
    public int HexPairTurnsPlayer, HexPairTurnsEnemy, HexMaxCursedPlayer, HexMaxCursedEnemy;

    /// <summary>ターン頭の census を取った回数（＝ターン数。上の比の分母）。</summary>
    public int HexCensusTurns;

    /// <summary><b>単体攻撃</b>が呪い持ちに入った回数（＝共有の発火）と、
    /// そのうち<b>同陣営に他の呪い持ちが1体もいなかった</b>回数（空振り）。</summary>
    public int HexShareHits, HexShareDry;

    /// <summary>実際に配った本数と量。<c>HexShareToPlayer</c> は味方側が受けた量（Q4）。</summary>
    public int HexShares, HexShareDamage, HexSharesToPlayer, HexShareDamageToPlayer;

    /// <summary>共有先の延べ体数（<b>版に依らない</b>——共有量が 0 でも数える）と、
    /// 紙のスループットの材料（Σ 元の打点 ／ Σ 元の打点 × 共有先の体数）。</summary>
    public int HexShareTargets;
    public long HexShareBase, HexShareBaseTimesTargets;

    /// <summary>支援拒否（<see cref="TraitId.Stoic"/>）持ちに呪いが付いた回数（§1-3 の 4）。</summary>
    public int HexMarksOnStoic;

    /// <summary>1ホップのガードが止めた回数（自己検査 (c)。<b>0 でないことが「ガードが働いている」</b>）。</summary>
    public int HexHopBlocked;

    /// <summary>薙ぎ・貫き・全体が呪い持ちに入った回数（自己検査 (d)。
    /// <b>これが 0 より大きいのに共有が起きていない</b>ことが「範囲は反応しない」の証拠）。</summary>
    public int HexNonSingleOnCursed;

    /// <summary>陣営をまたいで配った回数（自己検査 (e)。<b>構成上つねに 0</b>）。</summary>
    public int HexCrossTeam;

    /// <summary>巻き込み則の傷を抑えた回数（自己検査 (f)。<c>spillWound: false</c> が無ければ書かれていた本数）。</summary>
    public int HexSpillSuppressed;

    /// <summary>共有を起こした単体攻撃の出どころ（Q3）。<b>駒の名前で数える。</b></summary>
    public readonly Dictionary<string, int> HexShareBySource = new();
    public readonly Dictionary<string, int> HexShareDamageBySource = new();

    /// <summary>祟りの被弾（門の 1）。<b>規則の有無に依らず数える。</b></summary>
    internal void NoteHexHit(UnitState self, UnitState? source)
    {
        HexHits++;
        if (source is null) { HexHitsNoSource++; return; }
        Dictionary<string, int> d;
        if (source.TeamId == self.TeamId) { HexHitsFromAlly++; d = HexHitByAlly; }
        else { HexHitsFromFoe++; d = HexHitByFoe; }
        d.TryGetValue(source.Def.Name, out int n0);
        d[source.Def.Name] = n0 + 1;
    }

    /// <summary>呪いが新しく付いた（門の 2 の分子）。</summary>
    internal void NoteHexMark(UnitState marked)
    {
        HexMarks++;
        bool player = marked.TeamId == PlayerTeam;
        if (player) HexMarksOnPlayer++; else HexMarksOnEnemy++;
        if (!marked.AcceptsSupport) HexMarksOnStoic++;   // §1-3 の 4。**呪いは支援ではないので弾かない**
        int n = LivingMembers(marked.TeamId).Count(u => u.RawCounter(StatusKeys.Curse) > 0);
        if (n >= 2)
        {
            if (player) { if (HexPairTurnPlayer == 0) HexPairTurnPlayer = Math.Max(1, _turn); }
            else { if (HexPairTurnEnemy == 0) HexPairTurnEnemy = Math.Max(1, _turn); }
        }
    }

    /// <summary>ターン頭の census（門の 2）。<b>盤面は読むだけ。</b></summary>
    internal void NoteHexCensus()
    {
        HexCensusTurns++;
        int p = LivingMembers(PlayerTeam).Count(u => u.RawCounter(StatusKeys.Curse) > 0);
        int e = LivingMembers(EnemyTeam).Count(u => u.RawCounter(StatusKeys.Curse) > 0);
        if (p >= 2) HexPairTurnsPlayer++;
        if (e >= 2) HexPairTurnsEnemy++;
        if (p > HexMaxCursedPlayer) HexMaxCursedPlayer = p;
        if (e > HexMaxCursedEnemy) HexMaxCursedEnemy = e;
    }

    /// <summary>
    /// そのうち<b>横取り役（集約・渡し）に横取りされた量</b>を経路別に割ったもの（第44期）。
    /// <c>DullByRoute[r] - DullTakenByRoute[r]</c> がその経路の「素通り」になる。
    ///
    /// <para><b>経路別に割る必要がここで初めて出た。</b> 既存の <c>BearTaken</c> /
    /// <c>BearPassed</c> は全経路の合算なので、供給源が複数ある行（誹り＋なまり＋萎縮）では
    /// 「敵が撒いたぶんの何割が資産に変わったか」が引けない。<b>盤面には一切影響しない</b>。</para>
    /// </summary>
    public int[] DullTakenByRoute { get; } = new int[DullRoutes.Count];

    /// <summary>
    /// 弱体で <c>CurrentAttack</c> が 0 になった回数と、その駒の内訳（<b>崖の検算</b>・第44期）。
    /// <c>CurrentAttack</c> の下限は 0 だが <c>AtkBonus</c> に下限は無いので、
    /// 0 を割ったぶんは<b>負の在庫として溜まる</b>（沈めた駒は同量の強化では戻らない）。
    /// 敵側の同型は <c>RelayZeroed</c>（転嫁の分だけ）で、こちらは<b>窓口を通る全経路</b>を数える。
    /// </summary>
    public int DullZeroed { get; internal set; }
    public Dictionary<string, int> DullZeroedWho { get; } = new();
    public int BearTaken { get; internal set; }
    public int BearPassed { get; internal set; }
    public int BearArmor { get; internal set; }
    public int BearSoaked { get; internal set; }
    public Dictionary<string, int> BearFrom { get; } = new();

    /// <summary>
    /// 強化（<see cref="Whet"/>）の計数。<see cref="DullTotal"/> と対になる（第56期）。
    /// <b>発火しなかったことは盤面の値に痕跡を残さない</b>ので診断が読むためだけに数える
    /// （<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><c>WhetTotal</c> 窓口を通った総量（両陣営） ／ <c>WhetByRoute</c> 経路別の内訳。
    /// <b>駒ごとの受取量は <see cref="UnitTally.Whetted"/> の側にある</b>——
    /// 収支（<c>Whetted - Dulled</c>）と死蔵（受け取ったのに <c>Attacks</c> が 0）を
    /// 同じ台帳の上で引くために、名前引きの辞書ではなく tally に置いた。</para>
    /// </summary>
    public int WhetTotal { get; internal set; }
    public int[] WhetByRoute { get; } = new int[WhetRoutes.Count];

    /// <summary>
    /// 強化の経路を1本ずつ落とすノブ（第65期・<b>診断専用</b>）。既定は
    /// <see cref="WhetMask.None"/>＝現行で、通常の実行では誰も渡さない。
    /// </summary>
    public WhetMask WhetBlock { get; }

    /// <summary>
    /// <b>強化の「到着の時刻」と「その後使われたか」</b>（第65期）。
    /// <b>誰も読んで分岐しない</b>・<c>verbose</c> に依存しない・盤面には一切影響しない。
    ///
    /// <para><c>WhetTurnSumByRoute</c> 経路別の Σ(量 × 到着ターン)（÷ 量 で<b>到着の平均ターン</b>） ／
    /// <c>WhetFirstTurnSumByRoute</c> と <c>WhetFirstTurnCountByRoute</c> は
    /// <b>1戦につき1回</b>その経路が最初に届いたターン（÷ で<b>初到着の平均</b>） ／
    /// <c>WhetUsedByRoute</c> <b>受け手がその後 <see cref="NoteAttackRead"/> を1度でも通した量</b>
    /// （÷ 供給量で<b>使用率</b>）。</para>
    ///
    /// <para>使用率は「受け取った<b>後</b>に攻撃力を出力へ変換したか」なので、
    /// 死蔵（<c>AttackReads == 0</c>・第64期）より<b>厳しい</b>——最後の一撃の後に届いた強化は
    /// 死蔵に数えられなくても使用率には乗らない。<b>遅さ（H3）を測るのはこちら。</b></para>
    /// </summary>
    public int[] WhetTurnSumByRoute { get; } = new int[WhetRoutes.Count];
    public int[] WhetFirstTurnSumByRoute { get; } = new int[WhetRoutes.Count];
    public int[] WhetFirstTurnCountByRoute { get; } = new int[WhetRoutes.Count];
    public int[] WhetUsedByRoute { get; } = new int[WhetRoutes.Count];

    /// <summary>
    /// <b>経路ごとの受け手</b>（第65期。キーは <c>Def.Id</c>）。
    /// <see cref="NoteFavorReceiver"/> が火選り1本だけについてやっていたことを、
    /// <b>7経路すべてについて常時数える</b>——「行き先を決めているものは何か」（判断の地図）は
    /// 経路別の受け手が無いと実測できない。<b>盤面には一切影響しない。</b>
    /// </summary>
    public Dictionary<string, int>[] WhetToByRoute { get; } =
        Enumerable.Range(0, WhetRoutes.Count).Select(_ => new Dictionary<string, int>()).ToArray();

    /// <summary>
    /// 逆しま（<see cref="PerverseTrait"/>・ウツ）が受けた強化の量と、
    /// <b>それで符号が正へ渡った回数</b>（<c>WhetPerverseFlips</c>）。
    ///
    /// <para><see cref="DullZeroed"/> の裏返しにあたる<b>崖の検算</b>。逆しまは
    /// <c>AtkBonus</c> の<b>符号だけ</b>を読み、負なら3倍・正なら半減なので、
    /// 「半減側へ落ちた瞬間」はここでしか観測できない（後から差分を取ると
    /// 弱体で押し戻された往復に埋もれる）。第52期に駆り立てだけで観測された
    /// 「カリはウツの呪いを『治して』殺す」を、<b>6経路すべてについて常時数える</b>。</para>
    /// </summary>
    public int WhetToPerverse { get; internal set; }
    public int WhetPerverseFlips { get; internal set; }

    /// <summary>
    /// 横流し（<see cref="TraitId.Funnel"/>）の強度＝<b>選択子</b>。
    /// <b>診断（funnel）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="FunnelRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public FunnelRule Funnel { get; }

    /// <summary>
    /// 盤上に横流し役がいるか。<b>短絡のためだけのフラグ</b>で、盤面には一切影響しない
    /// （<c>DivertActive</c> / <c>FinisherActive</c> と同じ扱い）。
    /// <see cref="Add"/> が立てるので、蘇生・召喚で後から現れた保持者も拾う。
    /// </summary>
    public bool FunnelActive { get; internal set; }

    /// <summary>
    /// 横流し（<see cref="Whet"/> の横取り）の計数。<b>発火しなかったことは盤面の値に痕跡を残さない</b>ので、
    /// 診断が読むためだけに数える（<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><c>FunnelTaken</c> 横流しした総量 ／ <c>FunnelByRoute</c> <b>どの経路を横取りしたか</b> ／
    /// <c>FunnelFrom</c> 取り上げた相手の内訳 ／ <c>FunnelTo</c> 回した先の内訳。</para>
    ///
    /// <para><b><c>WhetRoute</c> は足していない。</b> 横流しは供給ではなく横取りなので、
    /// 経路の一覧（<see cref="WhetRoutes"/>）に列を増やすと「合計 = 供給の総和」が壊れる
    /// ——<see cref="Dull"/> 側の <c>DullTakenByRoute</c> とまったく同じ扱いにしてある。</para>
    ///
    /// <para><b>2つの辞書のキーは <c>Def.Id</c>。</b> <c>BearFrom</c> / <c>RelayTo</c> は
    /// <c>Name</c> を使っているが、こちらは<b>死蔵（<c>FunnelDead</c>）を
    /// <see cref="TallyByUnit"/> と突き合わせて引く</b>ので、同じキーで持たないと結合できない。</para>
    /// </summary>
    public int FunnelTaken { get; internal set; }
    public int[] FunnelByRoute { get; } = new int[WhetRoutes.Count];
    public Dictionary<string, int> FunnelFrom { get; } = new();
    public Dictionary<string, int> FunnelTo { get; } = new();

    /// <summary>
    /// 横流しの<b>弱体側</b>（V3・第63期）の計数。強化側と対になる。
    /// <c>FunnelDullByRoute</c> は <see cref="DullRoutes"/> で割る（強化側は <see cref="WhetRoutes"/>）
    /// ——<b>配列の長さが違うので取り違えないこと。</b> 盤面には一切影響しない。
    /// </summary>
    public int FunnelDullTaken { get; internal set; }
    public int[] FunnelDullByRoute { get; } = new int[DullRoutes.Count];
    public Dictionary<string, int> FunnelDullFrom { get; } = new();
    public Dictionary<string, int> FunnelDullTo { get; } = new();

    /// <summary>
    /// 渡し（転嫁）の強度。<b>診断（relay）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="RelayRule.Default"/>）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public RelayRule Relay { get; }

    /// <summary>
    /// 渡し（<see cref="TraitId.Relay"/>）の計数。<b>発火しなかったことは盤面の値に痕跡を残さない</b>ので、
    /// 診断が読むためだけに数える（<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><c>RelayTaken</c> 横取りした量 ／ <c>RelaySent</c> 敵へ流した量 ／
    /// <c>RelayMaxSent</c> <b>1回の <see cref="Dull"/> で流した最大量</b>（崖の検算） ／
    /// <c>RelayZeroed</c> 転嫁で敵の <c>CurrentAttack</c> が 0 になった回数（崖の検算） ／
    /// <c>RelayCost</c> 代金として <see cref="ApplyDamage"/> へ渡した総量 ／
    /// <c>RelaySelfPaid</c> そのうち<b>渡し役自身の身に実際に落ちた量</b>（肩代わりされなかった分） ／
    /// <c>RelayFrom</c> 横取りした相手の内訳 ／ <c>RelayTo</c> 流し先の内訳。</para>
    ///
    /// <para><b><c>RelaySelfPaid</c> は tally の差分で取る。</b> <c>ApplyDamage</c> は
    /// 最終的な受け手の <c>UnitTally.DamageTaken</c> に加算するので、代金の前後で
    /// 渡し役の tally を引き算すれば「庇う・分かち・巨躯・後備え・棘守りが割り込んだ後に
    /// 本人へ落ちた量」がそのまま出る。<b>戻り値を足さないこと</b>——
    /// <c>ApplyDamage</c> は割り込みの後の値を返さない。</para>
    /// </summary>
    public int RelayTaken { get; internal set; }
    public int RelaySent { get; internal set; }
    public int RelayMaxSent { get; internal set; }
    public int RelayZeroed { get; internal set; }
    public int RelayCost { get; internal set; }
    public int RelaySelfPaid { get; internal set; }
    public Dictionary<string, int> RelayFrom { get; } = new();
    public Dictionary<string, int> RelayTo { get; } = new();

    /// <summary>
    /// 渡し（<see cref="RelayTrait"/>）の転嫁の最中か。転嫁は <see cref="Dull"/> の中から
    /// <see cref="Dull"/> を呼ぶ<b>唯一の経路</b>で、<b>敵側に渡しを持たせた瞬間に無限往復する</b>
    /// （こちらが敵を弱体化 → 敵の渡しがこちらへ返す → …）。現状は敵に誰もいないが、
    /// 反撃・割り込み・突き返しと同じ形のガードを先に置く。
    ///
    /// <para><b>止めるのは渡しの横取りだけ</b>——転嫁の途中でも集約は働いてよい
    /// （集約は流し先を作らないので往復しない）。<b>static に持たないこと</b>。</para>
    /// </summary>
    public bool InRelay { get; private set; }

    public void Relaying(Action body)
    {
        if (InRelay) return;
        InRelay = true;
        try { body(); }
        finally { InRelay = false; }   // 例外で立ちっぱなしになると以後の転嫁が永久に止まる
    }

    /// <summary>
    /// 誹りの強度。<b>診断（slander）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="SlanderRule.Default"/> ＝ 無効）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public SlanderRule Slander { get; }

    /// <summary>
    /// 誹り（<see cref="TraitId.Slander"/>）の計数。<b>発火しなかったことは盤面の値に痕跡を残さない</b>ので、
    /// 診断が読むためだけに数える（<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><c>SlanderFired</c> 発火回数 ／ <c>SlanderTotal</c> 撒いた総量 ／
    /// <c>SlanderTo</c> 誹られた相手の内訳（駒名 → 量）。</para>
    ///
    /// <para><b>「撒いた量」は成果ではない。</b> 読み手に届いたかは
    /// <see cref="DullTakenByRoute"/>（横取り）と、その差＝素通りで読む。</para>
    /// </summary>
    public int SlanderFired { get; internal set; }
    public int SlanderTotal { get; internal set; }
    public Dictionary<string, int> SlanderTo { get; } = new();

    /// <summary>
    /// 驕りの強度。<b>診断（overbear）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="OverbearRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public OverbearRule Overbear { get; }

    /// <summary>
    /// 驕り（<see cref="TraitId.Overbear"/>）の計数。<b>発火しなかったことは盤面の値に痕跡を残さない</b>
    /// ので、診断が読むためだけに数える（<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><b>「成立時刻」がこの期の核心</b>なので、成立率（<c>MetTurns</c> / <c>Turns</c>）と
    /// 初成立ターン（<c>FirstTurn</c>・一度も成立しなければ 0）を分けて持つ。
    /// 平均だけでは「遅く成立した」と「半分の試行で成立しなかった」が区別できない。</para>
    /// </summary>
    public int OverbearFired { get; internal set; }
    public int OverbearTotal { get; internal set; }
    public Dictionary<string, int> OverbearTo { get; } = new();
    public int OverbearMetTurns { get; internal set; }
    public int OverbearTurns { get; internal set; }
    public int OverbearFirstTurn { get; internal set; }
    public int OverbearSwings { get; internal set; }
    public int OverbearDoubled { get; internal set; }
    public int OverbearBackfire { get; internal set; }
    public int OverbearBackfireHits { get; internal set; }

    /// <summary>
    /// 鱗の強度。<b>診断（scale）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="ScaleRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public ScaleRule Scale { get; }

    /// <summary>
    /// 鱗（<see cref="TraitId.Scale"/>）の計数。<b>発火しなかったことは盤面の値に痕跡を残さない</b>
    /// ので、診断が読むためだけに数える（<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><b>「獲得」と「支出」を経路ごとに割るのがこの期の要。</b>
    /// アーマーは被弾でも攻撃でも減るので<b>二重支出</b>で、どちらが律速かで
    /// この駒が「攻撃型」なのか「防御型」なのかが決まる。</para>
    ///
    /// <para><b>「貫き」と「後列到達」を分ける。</b> 貫いた回数は成果ではない
    /// ——後列に敵がいなければ単体攻撃と同じである。</para>
    /// </summary>
    public int ScaleGainDeath { get; internal set; }
    public int ScaleGainShatter { get; internal set; }
    public int ScaleGainBear { get; internal set; }
    /// <summary>獲得のうち儚い駒（胞子・亡骸）の死から来たぶん。<c>ScaleGainDeath</c> の内数。</summary>
    public int ScaleGainEphemeral { get; internal set; }
    /// <summary>初めて破片を得たターン。一度も得なければ 0。</summary>
    public int ScaleFirstTurn { get; internal set; }
    /// <summary>保持者が生きてターン頭を迎えた回数（纏い率の分母）。</summary>
    public int ScaleAliveTurns { get; internal set; }
    /// <summary>そのうち <c>Armor &gt; 0</c> だったターン数。</summary>
    public int ScaleWornTurns { get; internal set; }
    /// <summary>保持者が振った回数。</summary>
    public int ScaleSwings { get; internal set; }
    /// <summary>そのうち貫きだった回数。</summary>
    public int ScalePierceSwings { get; internal set; }
    /// <summary>貫きが<b>後列の敵</b>に当たった回数（レーンの段ごとに1つ数える）。</summary>
    public int ScaleBackHits { get; internal set; }
    /// <summary>そのとき後列に振り下ろした量（減衰後）。</summary>
    public int ScaleBackDamage { get; internal set; }
    /// <summary>攻撃で消費した破片の量。</summary>
    public int ScaleSpentAttack { get; internal set; }
    /// <summary>被弾で吸われた破片の量。</summary>
    public int ScaleSpentHit { get; internal set; }
    /// <summary>破片が 0 に戻った回数（攻撃・被弾のどちらでも）。</summary>
    public int ScaleDepleted { get; internal set; }
    /// <summary>
    /// 保持者の破片が被弾を<b>受け切った</b>回数。§7-1 の干渉
    /// （受け切ると <c>OnDamaged</c> が呼ばれない＝被弾を条件にする特性が発火しない）の実測用。
    /// </summary>
    public int ScaleFullSoaks { get; internal set; }

    // --- 砕け（第137期）---------------------------------------------------------------------
    // **計数だけ。盤面には1ビットも触らない**（`Dull` の経路別と同じ扱い）。
    // 既存の `ScaleGainShatter` は**受け手が鱗（ウロ）のときしか呼ばれない**ので
    // 「砕けが陣営全体へ何点配ったか」は引けない（Phase 0 Q0-2）。ここで陣営合計を持つ。

    /// <summary>砕けが発火した回数（配る相手が 0 人でも、配る量が 0 でも数えない）。</summary>
    /// <summary>
    /// 砕けの保持者が盤上にいるか（<see cref="Add"/> が立てる）。
    /// <b>計数の短絡専用で、どの規則も読まない。</b>
    /// </summary>
    public bool ShatterActive { get; internal set; }

    public int ShatterTicks { get; internal set; }
    /// <summary>配った破片の総量（受け手の人数ぶん合算する）。</summary>
    public int ShatterGiven { get; internal set; }
    /// <summary>
    /// 配った破片のうち<b>実際にダメージを吸った量</b>。
    /// <para><b>出どころ別には割らない。</b> 集約（ウケ）・鱗（味方の死）と同じプールに落ちるので、
    /// 砕けを含む行に集約や鱗が同席していると混ざる。Phase 0 Q0-3 で同席する行を数えてあり
    /// （`鱗改` / `破片×被弾` の 2 行にウロがいる）、そこは <c>ScaleSpentHit</c> と
    /// <c>ScaleGainDeath</c> を並べて読む。<b>陣営合計で足りる</b>のは、
    /// この期の主判定がローカル台（砕け以外の破片の供給を置かない）だから。</para>
    /// </summary>
    public int ShatterSoaked { get; internal set; }
    /// <summary>代金の総額（自前の鍵のときだけ立つ。<c>Passive</c> では 0）。</summary>
    public int ShatterPaid { get; internal set; }
    /// <summary>
    /// そのうち保持者が<b>自弁した</b>額（自弁率の分子。分母は <see cref="ShatterPaid"/>）。
    /// <b>tally の差分で取る</b>——巨躯・分かちが割り込むと代金は他人へ移るので、
    /// 「払ったつもりの額」を払ったことにはならない（ワタの <c>RelaySelfPaid</c> と同じ作法・第43期）。
    /// </summary>
    public int ShatterPaidSelf { get; internal set; }

    /// <summary>砕けの供給を記録する。<b>盤面には触らない。</b></summary>
    internal void NoteShatter(int given)
    {
        ShatterTicks++;
        ShatterGiven += given;
    }

    /// <summary>鱗の獲得を記録する。<b>盤面には触らない。</b></summary>
    internal void NoteScaleGain(int amount, ScaleSource src, bool ephemeral = false)
    {
        if (amount <= 0) return;
        switch (src)
        {
            case ScaleSource.Death: ScaleGainDeath += amount; break;
            case ScaleSource.Shatter: ScaleGainShatter += amount; break;
            default: ScaleGainBear += amount; break;
        }
        if (ephemeral) ScaleGainEphemeral += amount;
        if (ScaleFirstTurn == 0) ScaleFirstTurn = Turn;
    }

    /// <summary>鱗の支出（攻撃側）を記録する。<b>盤面には触らない。</b></summary>
    internal void NoteScaleSpend(int amount, bool depleted)
    {
        ScaleSpentAttack += amount;
        if (depleted) ScaleDepleted++;
    }

    /// <summary>
    /// 業の強度。<b>診断（scapegoat）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="ScapegoatRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public ScapegoatRule Scapegoat { get; }

    /// <summary>
    /// 業（<see cref="TraitId.Scapegoat"/>）の計数フックを走らせるか。
    /// <b>短絡のためだけ</b>のフラグで、盤面には一切影響しない（layout は数百万戦を並列で回すので、
    /// 保持者がいない実行では毒・燃焼のたびに走らせたくない）。
    ///
    /// <para><see cref="Add"/> が保持者を見つけたときに立つ（蘇生・増援で湧いた保持者も拾う）。
    /// <b><see cref="ScapegoatRule.Audit"/> でも立つ</b>——診断が<b>素体の対照</b>
    /// （業と同数値で特性だけを持たない駒）でも自傷・味方の継続ダメージを数えるため。
    /// <b>監査は計数だけで盤面を1つも動かさない</b>ことは、診断 §0 が
    /// 「監査あり」と「監査なし」を突き合わせて毎回確かめる。</para>
    /// </summary>
    public bool ScapegoatActive { get; private set; }

    /// <summary>
    /// 業（<see cref="TraitId.Scapegoat"/>）の計数。<b>発火しなかったことは盤面の値に痕跡を残さない</b>
    /// ので、診断が読むためだけに数える（<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><b>「転写」と「転写の効き」を分けてある。</b> 付けた回数は成果ではない
    /// ——敵が次のターンに死ぬなら毒を付けても意味がない。効きは
    /// <c>ScapegoatFoeDot</c>（毒・燃焼が実際に削った量）／<c>ScapegoatFoeSkips</c>（痺れで飛ばした敵の手番）／
    /// <c>ScapegoatMarkPulls</c>（標に味方の単体攻撃が引かれた回数）の3本で測る。</para>
    ///
    /// <para><b>「自傷」と「味方の救済」も分けてある。</b> この駒は引き取りが防御でもあり
    /// 自壊でもあるので、収支がどちらに振れるかが性格を決める。
    /// <b>どちらも素体との対照で帰属を取る</b>——瘴気の毒はゴウが引き取らなくても載るので、
    /// 絶対値だけでは機構のぶんが割れない。</para>
    /// </summary>
    public int ScapegoatTakes { get; internal set; }
    public Dictionary<string, int> ScapegoatTakeByKind { get; } = new();
    public Dictionary<string, int> ScapegoatTakeFrom { get; } = new();
    /// <summary>引き取れる種類が盤面に無くて何もしなかった回数（空振り）。</summary>
    public int ScapegoatMissed { get; internal set; }
    /// <summary>全種類を既に背負っていて引き取る余地が無かった回数。空振りとは原因が違う。</summary>
    public int ScapegoatFull { get; internal set; }
    /// <summary>保持者が生きてターン頭を迎えた回数（成立率の分母）。</summary>
    public int ScapegoatAliveTurns { get; internal set; }
    /// <summary>そのうち閾値を満たしていたターン数。</summary>
    public int ScapegoatMetTurns { get; internal set; }
    /// <summary>背負っている種類数の合計（÷ <c>ScapegoatAliveTurns</c> が平均）と最大。</summary>
    public int ScapegoatKindSum { get; internal set; }
    public int ScapegoatKindMax { get; internal set; }
    /// <summary>閾値に初めて達したターン。一度も達しなければ 0。</summary>
    public int ScapegoatFirstTurn { get; internal set; }
    /// <summary>保持者が振った回数と、そのうち転写した回数。</summary>
    public int ScapegoatSwings { get; internal set; }
    public int ScapegoatFired { get; internal set; }
    /// <summary>転写で敵に書いた延べ数（種類別）。</summary>
    public Dictionary<string, int> ScapegoatWriteByKind { get; } = new();
    /// <summary>転写の効き: 業が書いたぶんに帰属する毒・燃焼のダメージ。</summary>
    public int ScapegoatFoeDot { get; internal set; }
    /// <summary>転写の効き: 業が書いた痺れで敵が飛ばした手番の数。</summary>
    public int ScapegoatFoeSkips { get; internal set; }
    /// <summary>転写の効き: 業が書いた標に味方の単体攻撃が引かれた回数。</summary>
    public int ScapegoatMarkPulls { get; internal set; }
    /// <summary>
    /// 味方側が毒・燃焼で受けたダメージと、痺れで失った手番を<b>駒ごとに</b>割ったもの
    /// （キーは <c>Def.Id</c>）。
    ///
    /// <para><b>「保持者かどうか」で箱を分けていないのが要点。</b> 分けると
    /// <b>素体の対照が成立しなくなる</b>——素体（特性なし・同数値）は保持者ではないので、
    /// 同じ駒の同じ被害が別の箱に落ちて業版と引き算できない。
    /// 駒ごとに割っておけば、診断が「5枚目の席の駒」と「残り4枚」を
    /// <b>両方の版で同じ切り方</b>で数えられる。</para>
    /// </summary>
    public Dictionary<string, int> ScapegoatDotByUnit { get; } = new();
    public Dictionary<string, int> ScapegoatSkipByUnit { get; } = new();

    /// <summary>引き取りを記録する。<b>盤面には触らない。</b></summary>
    internal void NoteScapegoatTake(string kind, string from, int amount)
    {
        ScapegoatTakes += amount;
        ScapegoatTakeByKind[kind] = ScapegoatTakeByKind.TryGetValue(kind, out int a) ? a + amount : amount;
        ScapegoatTakeFrom[from] = ScapegoatTakeFrom.TryGetValue(from, out int b) ? b + amount : amount;
    }

    /// <summary>そのターンの種類数を記録する。<b>盤面には触らない。</b></summary>
    internal void NoteScapegoatStand(int kinds)
    {
        ScapegoatAliveTurns++;
        ScapegoatKindSum += kinds;
        if (kinds > ScapegoatKindMax) ScapegoatKindMax = kinds;
        if (kinds >= Scapegoat.Threshold)
        {
            ScapegoatMetTurns++;
            if (ScapegoatFirstTurn == 0) ScapegoatFirstTurn = Turn;
        }
    }

    /// <summary>転写で書いた量を記録する。<b>盤面には触らない。</b></summary>
    internal void NoteScapegoatWrite(string kind, int amount)
        => ScapegoatWriteByKind[kind] =
               ScapegoatWriteByKind.TryGetValue(kind, out int a) ? a + amount : amount;

    /// <summary>
    /// 毒・燃焼が削った量を、業から見た3つの箱（自分 / 味方 / 敵に書いたぶん）へ割る。
    ///
    /// <para><b>敵側だけは控え（<see cref="ScapegoatTrait.OwedKey"/>）で帰属を取る。</b>
    /// 毒は層が混ざるので、そのターンの削りのうち控えの割合ぶんだけを数える。
    /// 燃焼は残ターンなので、1ターン分を数えて控えを1つ減らす。
    /// <b>控えは誰も読んで分岐しない私有カウンタ</b>なので、ここで書き換えても盤面は動かない
    /// （受け入れ基準1で 250 セル 0 件を確認する）。</para>
    /// </summary>
    internal void NoteScapegoatDot(UnitState u, int dmg, string kind)
    {
        if (dmg <= 0) return;
        if (u.TeamId == PlayerTeam)
        {
            string id = u.Def.Id;
            ScapegoatDotByUnit[id] = ScapegoatDotByUnit.TryGetValue(id, out int d) ? d + dmg : dmg;
            return;
        }

        string owed = ScapegoatTrait.OwedKey(kind);
        int o = u.RawCounter(owed);
        if (o <= 0) return;
        if (kind == StatusKeys.Poison)
        {
            int stacks = u.RawCounter(StatusKeys.Poison);
            if (stacks <= 0) return;
            ScapegoatFoeDot += dmg * Math.Min(o, stacks) / stacks;
        }
        else
        {
            // 燃焼は層ではなく残ターンなので、業が足した 1 は**末尾の1ターンを延ばした**
            // ことにあたる。だから帰属させるのは**最後の o ターンだけ**——
            // 先頭の刻みを数えると、既に燃えていた相手（火の粉が3ターンで点けた相手）に
            // 重ねただけで満額を取ってしまうし、延びた末尾に届く前に敵が落ちた場合に
            // 「効かなかった」が記録されない。
            // 呼ばれる時点で残ターンは既に 1 引かれている（`TickStatuses` の順序）。
            if (u.RawCounter(StatusKeys.Burn) >= o) return;
            ScapegoatFoeDot += dmg;
            u.SetCounter(owed, o - 1);
        }
    }

    /// <summary>痺れで飛んだ手番を同じ3つの箱へ割る。<b>控え以外の盤面には触らない。</b></summary>
    internal void NoteScapegoatSkip(UnitState u)
    {
        if (u.TeamId == PlayerTeam)
        {
            string id = u.Def.Id;
            ScapegoatSkipByUnit[id] = ScapegoatSkipByUnit.TryGetValue(id, out int d) ? d + 1 : 1;
            return;
        }

        string owed = ScapegoatTrait.OwedKey(StatusKeys.Stun);
        int o = u.RawCounter(owed);
        if (o <= 0) return;
        ScapegoatFoeSkips++;
        u.SetCounter(owed, o - 1);
    }

    /// <summary>
    /// 逸らしの強度。<b>診断（divert）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="DivertRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public DivertRule Divert { get; }

    /// <summary>
    /// 盤上に逸らし（<see cref="TraitId.Divert"/>）の保持者が一度でも立ったか。
    /// <b>計数のフックを短絡させるためだけ</b>のフラグ（layout は数百万戦を並列で回す）。
    /// <see cref="DivertRule.Audit"/> でも立つ——診断が<b>素体の対照</b>でも
    /// 撃破ターンと単体振りを同じ切り方で数えるため。<b>監査は盤面を1つも動かさない。</b>
    /// </summary>
    public bool DivertActive { get; private set; }

    /// <summary>
    /// 逸らし（<see cref="TraitId.Divert"/>）の計数。<b>発火しなかったことは盤面の値に痕跡を残さない</b>
    /// ので、診断が読むためだけに数える（<c>verbose</c> には依存しない）。盤面には一切影響しない。
    ///
    /// <para><b>「焦点」と「焦点の効き」を分けてある。</b> 標を付けた回数は成果ではない
    /// ——味方がそちらを殴らなければ意味がない。効きは <c>DivertAllyOnMarked</c> ÷
    /// <c>DivertAllySingles</c>（味方の単体振りのうち標持ちに当たった割合）で測る。</para>
    ///
    /// <para><b>「焦点数」が指示書の仕様から出る落とし穴。</b> 外すのは味方の標だけなので、
    /// <b>敵に付けた標は戦闘が終わるまで消えない</b>——焦点を浴びた敵のHPが下がると
    /// 次のターンには別の敵が最高HPになり、そちらにも標が付く。<b>焦点は自分で溶ける。</b></para>
    /// </summary>
    public int DivertFires { get; internal set; }
    public int DivertStrips { get; internal set; }
    public int DivertFocus { get; internal set; }
    /// <summary>焦点のうち<b>新しく標が付いた</b>回数（既に標持ちなら数えない）。</summary>
    public int DivertFocusFresh { get; internal set; }
    public Dictionary<string, int> DivertStripFrom { get; } = new();
    public Dictionary<string, int> DivertFocusTo { get; } = new();
    /// <summary>発火のたびの「標を持つ敵の数」の合計と最大（÷ <c>DivertFires</c> が平均）。</summary>
    public int DivertMarkedFoeSum { get; internal set; }
    public int DivertMarkedFoeMax { get; internal set; }

    /// <summary>味方の単体振りの回数と、そのうち<b>標持ちの敵に当たった</b>回数（＝焦点の効き）。</summary>
    public int DivertAllySingles { get; internal set; }
    public int DivertAllyOnMarked { get; internal set; }
    /// <summary>敵の単体振りの回数と、そのうち<b>標持ちの味方に当たった</b>回数（＝代金）。</summary>
    public int DivertFoeSingles { get; internal set; }
    public int DivertFoeOnMarked { get; internal set; }
    /// <summary>engine の鎖が<b>実際に主目標を差し替えた</b>回数（陣営別）。</summary>
    public int DivertAllyPulls { get; internal set; }
    public int DivertFoePulls { get; internal set; }

    /// <summary>
    /// 敵の駒ごとの撃破ターン（<c>Def.Id</c> → 合計 / 件数）。<b>撃破順がこの期の本命の指標</b>で、
    /// 素体の対照と直接引き算できるように<b>標に依存しない切り方</b>で数える。
    /// </summary>
    public Dictionary<string, int> DivertKillTurnByFoe { get; } = new();
    public Dictionary<string, int> DivertKillCountByFoe { get; } = new();

    internal void NoteDivertStrip(string from)
    {
        DivertStrips++;
        DivertStripFrom[from] = DivertStripFrom.TryGetValue(from, out int a) ? a + 1 : 1;
    }

    internal void NoteDivertFocus(string to, bool fresh)
    {
        DivertFocus++;
        if (fresh) DivertFocusFresh++;
        DivertFocusTo[to] = DivertFocusTo.TryGetValue(to, out int a) ? a + 1 : 1;
    }

    internal void NoteDivertFire(int stripped, int focused, int markedFoes)
    {
        DivertFires++;
        DivertMarkedFoeSum += markedFoes;
        if (markedFoes > DivertMarkedFoeMax) DivertMarkedFoeMax = markedFoes;
    }

    /// <summary>単体振りの着地点を陣営別に数える。<b>盤面には触らない。</b></summary>
    internal void NoteDivertSwing(UnitState actor, UnitState target)
    {
        bool marked = target.RawCounter(StatusKeys.Marked) > 0;
        if (actor.TeamId == PlayerTeam)
        {
            DivertAllySingles++;
            if (marked) DivertAllyOnMarked++;
        }
        else
        {
            DivertFoeSingles++;
            if (marked) DivertFoeOnMarked++;
        }
    }

    /// <summary>敵が倒れたターンを駒ごとに記録する。<b>盤面には触らない。</b></summary>
    internal void NoteDivertKill(UnitState dead)
    {
        if (dead.TeamId == PlayerTeam) return;
        string id = dead.Def.Id;
        DivertKillTurnByFoe[id] = DivertKillTurnByFoe.TryGetValue(id, out int a) ? a + Turn : Turn;
        DivertKillCountByFoe[id] = DivertKillCountByFoe.TryGetValue(id, out int b) ? b + 1 : 1;
    }

    /// <summary>
    /// 駆り立ての強度。<b>診断（goad）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="GoadRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public GoadRule Goad { get; }

    /// <summary>
    /// 駆り立て（<see cref="TraitId.Goad"/>）の計数。<b>発火しなかったこと（空振り）は盤面の値に
    /// 痕跡を残さない</b>ので、診断が読むためだけに数える（<c>verbose</c> には依存しない）。
    ///
    /// <para><b>「渡した量」と「効き」は別の列。</b> <c>GoadGiven</c> は
    /// <c>AtkBonus</c> の累積付与量で、<b>成果ではない</b>——対象が渡した直後に死ぬなら
    /// ダメージに変わっていない。効きは診断が<b>素体との差</b>（対象の <c>DamageToEnemy</c>）で取る。</para>
    ///
    /// <para><c>GoadMarkLost</c> は<b>付けた標が次の発火までに剥がされていた回数</b>
    /// ——逸らし（ソラ）が唯一の経路で、<b>席番号の順序に依存する</b>（<see cref="GoadTrait"/> の doc）。</para>
    /// </summary>
    public int GoadFires { get; internal set; }
    public int GoadIdle { get; internal set; }
    public int GoadGiven { get; internal set; }
    public int GoadSwitches { get; internal set; }
    public int GoadMarkLost { get; internal set; }
    /// <summary>渡した先が逆しま（<see cref="TraitId.Perverse"/>）だった回数。<b>強化が害になる</b>。</summary>
    public int GoadToPerverse { get; internal set; }
    public Dictionary<string, int> GoadTargetTo { get; } = new();

    internal void NoteGoadIdle() => GoadIdle++;

    internal void NoteGoadFire(UnitState pick, int boost, bool switched, bool lost)
    {
        GoadFires++;
        GoadGiven += boost;
        if (switched) GoadSwitches++;
        if (lost) GoadMarkLost++;
        if (pick.HasTrait(TraitId.Perverse)) GoadToPerverse++;
        string k = pick.Def.Name;
        GoadTargetTo[k] = GoadTargetTo.TryGetValue(k, out int a) ? a + 1 : 1;
    }

    /// <summary>
    /// 止めの強度。<b>診断（finisher）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="FinisherRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public FinisherRule Finisher { get; }

    /// <summary>
    /// 盤上に止め（<see cref="TraitId.Finisher"/>）の保持者が一度でも立ったか。
    /// <b>計数のフックを短絡させるためだけ</b>のフラグ（layout は数百万戦を並列で回す）。
    /// </summary>
    public bool FinisherActive { get; private set; }

    /// <summary>
    /// 止め（<see cref="TraitId.Finisher"/>）の計数。<b>発火しなかったこと（空振り）は盤面の値に
    /// 痕跡を残さない</b>ので、診断が読むためだけに数える（<c>verbose</c> には依存しない）。
    ///
    /// <para><b>「発火」と「列越え」を分けてある。</b> 標を持つ敵を殴った回数は成果ではない
    /// ——<b>標が無ければ狙えなかった敵</b>（<c>pool</c> の外＝中列・後列）を殴れたかどうかが、
    /// 第50期に判明した「標だけが列の壁を破る」性質を実際に使えているかの指標。</para>
    ///
    /// <para><b>「止めた砲火」が代金の実体。</b> 標を消すと engine の
    /// <see cref="MarkPullPercent"/> も切れるので、<b>味方全体の集中砲火を自分で終わらせる</b>
    /// ——消費したターンのうちに振った味方の単体攻撃で、盤上に標持ちが1体も残っていなかった回数。</para>
    /// </summary>
    public int FinisherFires { get; internal set; }
    /// <summary>標を持つ敵が1体もいなくて通常の対象選択に戻った回数（＝空振り）。</summary>
    public int FinisherIdle { get; internal set; }
    /// <summary><b>標が無ければ狙えなかった敵</b>（<c>PoolOf</c> の外）を殴った回数。</summary>
    public int FinisherCross { get; internal set; }
    public int FinisherConsumed { get; internal set; }
    /// <summary>標を持つ敵を殴って<b>実際に倒した</b>回数。</summary>
    public int FinisherKills { get; internal set; }
    public Dictionary<string, int> FinisherTargetTo { get; } = new();
    /// <summary>標が付いてから止めが殴るまでのターン数の合計と件数（÷ が <b>遊休</b>）。</summary>
    public int FinisherWaitSum { get; internal set; }
    public int FinisherWaitCount { get; internal set; }
    /// <summary>味方の単体振りの回数と、そのうち<b>止めが標を消した後で標が尽きていた</b>回数。</summary>
    public int FinisherAllySingles { get; internal set; }
    public int FinisherStarved { get; internal set; }

    /// <summary>
    /// 標が敵に付いたターンを記録するための私有キー。<b>盤面には一切影響しない</b>
    /// （読むのは <see cref="NoteFinisherFire"/> の遊休の計算だけ）。
    /// </summary>
    internal const string FinisherSinceKey = "finisherSince";
    private int _finisherConsumedTurn = -1;

    /// <summary>
    /// 標が付いた敵に「いつ付いたか」の印を立てる。<b>ターン頭の <c>OnTurnStart</c> の直後に
    /// 1回だけ呼ぶ</b>——標の書き手（逸らし・駆り立て・囃し立て）はすべてそこまでに書き終わる。
    /// <b>盤面は1つも動かさない</b>（私有カウンタを書くだけ）。
    /// </summary>
    internal void NoteFinisherMarkAges()
    {
        foreach (UnitState u in _units)
        {
            if (u.TeamId == PlayerTeam) continue;
            if (!u.IsAlive || u.RawCounter(StatusKeys.Marked) <= 0) { u.SetCounter(FinisherSinceKey, 0); continue; }
            if (u.RawCounter(FinisherSinceKey) == 0) u.SetCounter(FinisherSinceKey, Turn);
        }
    }

    internal void NoteFinisherIdle() => FinisherIdle++;

    internal void NoteFinisherFire(UnitState target, bool crossed)
    {
        FinisherFires++;
        if (crossed) FinisherCross++;
        int since = target.RawCounter(FinisherSinceKey);
        if (since > 0) { FinisherWaitSum += Turn - since; FinisherWaitCount++; }
        string k = target.Def.Name;
        FinisherTargetTo[k] = FinisherTargetTo.TryGetValue(k, out int a) ? a + 1 : 1;
    }

    /// <summary>殴った結果（撃破したか）。<b>消費の有無に関わらず数える</b>（対照2 と揃えるため）。</summary>
    internal void NoteFinisherOutcome(UnitState target, bool killed)
    {
        if (killed) FinisherKills++;
    }

    internal void NoteFinisherConsume()
    {
        FinisherConsumed++;
        _finisherConsumedTurn = Turn;
    }

    /// <summary>
    /// 味方の単体振りを数え、<b>止めが標を消したせいで焦点が無くなっていた振り</b>を拾う。
    /// <b>盤面には触らない。</b> これが「止めた砲火」の推定値で、
    /// 厳密な代金は診断が<b>対照2（消費なし版）との差</b>で取る。
    /// </summary>
    internal void NoteFinisherSwing(UnitState actor)
    {
        if (actor.TeamId != PlayerTeam) return;
        FinisherAllySingles++;
        if (_finisherConsumedTurn != Turn) return;
        foreach (UnitState f in _units)
            if (f.TeamId != PlayerTeam && f.IsAlive && f.RawCounter(StatusKeys.Marked) > 0) return;
        FinisherStarved++;
    }

    /// <summary>
    /// 火選りの強度。<b>診断（favor）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="FavorRule.Default"/>）。static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public FavorRule Favor { get; }

    /// <summary>
    /// 火選り（<see cref="TraitId.Favor"/>）の計数。<b>空振り（盤上に燃えている味方が1体もいない
    /// 手番）は盤面の値に痕跡を残さない</b>ので、診断が読むためだけに数える
    /// （<c>verbose</c> には依存しない）。
    ///
    /// <para><b>「配った量」と「効き」は別の列。</b> <c>FavorGiven</c> は付与量の累積で、
    /// <b>成果ではない</b>——受け手が熾火なら 4 倍、不動のカドなら 0 になる。
    /// 効きは診断が<b>素体との差</b>で取る。</para>
    ///
    /// <para><c>FavorIdle</c> は<b>第1ターンに構造的に 1 立つ</b>——ターンの順序が
    /// <c>TickStatuses</c> → <c>OnTurnStart</c> → 行動順ループで、火の粉は <c>OnAfterAttack</c>
    /// なので、火選りの発火時点ではまだ誰も燃えていない。</para>
    /// </summary>
    public int FavorFires { get; internal set; }
    /// <summary>盤上に燃えている味方が1体もいなくて何も強化しなかった手番の数（＝空振り）。</summary>
    public int FavorIdle { get; internal set; }
    /// <summary>強化した延べ体数（＝燃えている味方の延べ数）。</summary>
    public int FavorWhetted { get; internal set; }
    /// <summary>鈍らせた延べ体数（＝隣接する非燃焼の味方の延べ数）。</summary>
    public int FavorDulled { get; internal set; }
    /// <summary>配った強化の総量と、撒いた弱体の総量。</summary>
    public int FavorGiven { get; internal set; }
    public int FavorTaken { get; internal set; }
    /// <summary>強化の受け手の内訳（駒名）。<b>熾火に落ちた割合</b>を読むための列。</summary>
    public Dictionary<string, int> FavorWhetTo { get; } = new();
    /// <summary>弱体の受け手の内訳（駒名）。</summary>
    public Dictionary<string, int> FavorDullTo { get; } = new();
    /// <summary>強化が<b>熾火（乗算持ち）</b>へ落ちた量。Q4（乗算の許容）の分子。</summary>
    public int FavorToPyre { get; internal set; }

    /// <summary>
    /// 瘴気（<see cref="TraitId.Miasma"/>）と毒の刻みの計数（第61期）。
    /// <b>どれも誰も読んで分岐しない</b>ので盤面には一切影響しない
    /// （<c>FavorFires</c> と同じ扱いで <c>verbose</c> 非依存）。診断 <c>miasma</c> だけが読む。
    ///
    /// <para><b>撒いた量は「誰が撒いたか」ではなく総量で持つ。</b> 保持者は現状グザ1枚で、
    /// 版によって <c>Def.Id</c> が変わる（診断のローカルの <c>UnitDef</c>）ので、
    /// 駒側の <c>UnitTally</c> に置くと版をまたいだ突き合わせに id の一覧が要る。</para>
    ///
    /// <para><c>PoisonBite*</c> は毒の刻みの<b>額面</b>（毒喰らいの倍率を掛けた後・
    /// 肩代わり／破片／軛を通す前）。<b>実際に減った HP ではない</b>
    /// ——P3（味方の毒の刻みが増えるか）は額面で読むのが素直で、
    /// 実害は勝率と <c>DamageTaken</c> の側に出る。</para>
    /// </summary>
    public int MiasmaFires { get; internal set; }
    /// <summary>瘴気が敵へ撒いた毒の総量（層）。</summary>
    public int MiasmaToFoe { get; internal set; }
    /// <summary>瘴気が味方へ漏らした毒の総量（層）。<b>撒いた本人も含む。</b></summary>
    public int MiasmaToAlly { get; internal set; }
    /// <summary>毒の刻みの額面と回数を陣営で割ったもの。</summary>
    public int PoisonBitePlayer { get; internal set; }
    public int PoisonBiteEnemy { get; internal set; }
    public int PoisonTicksPlayer { get; internal set; }
    public int PoisonTicksEnemy { get; internal set; }

    internal void NoteMiasma(int toFoe, int toAlly)
    {
        MiasmaFires++;
        MiasmaToFoe += toFoe;
        MiasmaToAlly += toAlly;
    }

    internal void NotePoisonBite(UnitState u, int amount)
    {
        if (u.TeamId == PlayerTeam) { PoisonBitePlayer += amount; PoisonTicksPlayer++; }
        else { PoisonBiteEnemy += amount; PoisonTicksEnemy++; }
    }

    internal void NoteFavor(int whetted, int dulled, int idle, int given, int taken)
    {
        if (whetted > 0 || dulled > 0) FavorFires++;
        FavorIdle += idle;
        FavorWhetted += whetted;
        FavorDulled += dulled;
        FavorGiven += given;
        FavorTaken += taken;
    }

    /// <summary>
    /// 受け手の内訳。<b><see cref="Whet"/> / <see cref="Dull"/> の中から札で引く</b>
    /// ——横取り（集約・転嫁）が宛先を書き換えた後の<b>実際の受け手</b>を数えるため。
    /// <b>盤面には一切影響しない。</b>
    /// </summary>
    /// <summary>
    /// <b>この駒の <c>CurrentAttack</c> を出力量に変換した</b>ことを1回数える（第64期）。
    /// <see cref="UnitTally.AttackReads"/> の唯一の書き手で、<b>盤面には一切影響しない</b>
    /// （誰も読んで分岐しない・<c>verbose</c> に依存しない）。
    ///
    /// <para>呼ぶのは<b>4箇所だけ</b>——<c>PerformAttack</c>（攻撃の解決）と、
    /// <c>PerformAttack</c> を通らずに自分の攻撃力を打点に変える3本
    /// （棘・仇討ち・責め苦の追撃）。<b>固定量の干渉（破裂・生贄・吸い・分裂・巻き込み）は
    /// 攻撃力を読まないので呼ばない。</b></para>
    /// </summary>
    /// <summary>
    /// <b>外から届いた量</b>を1件数える（第68期）。<see cref="UnitTally.CarryAmount"/> /
    /// <c>CarryCount</c> / <c>CarryProbeTurn</c> の唯一の書き手で、
    /// <b>盤面には一切影響しない</b>（誰も読んで分岐しない・<c>verbose</c> にも依存しない）。
    ///
    /// <para>呼ぶのは<b>窓口だけ</b>——<see cref="Whet"/> ／ <see cref="Dull"/> ／
    /// <see cref="ApplyDamage"/> ／ <see cref="SwapSlots"/> と、
    /// <see cref="UnitState.SetCounter"/> から来る <see cref="NoteStatusGain"/>。
    /// <b>特性の側には1行も足していない</b>ので、経路を追加しても数え漏らさない。</para>
    /// </summary>
    internal void NoteCarry(UnitState u, int key, int amount)
    {
        if (amount <= 0) return;
        // 第94期 (T2)。**供給の観測はここ1箇所**——`Whet` / `Dull` / `ApplyDamage` /
        // `SwapSlots` / `NoteStatusGain` が全部ここへ来るので、11 キーを同じ器具で観測できる。
        // **既定 null なので通常の実行では1行も走らない。**
        if (Probe is not null) NoteProbeWrite(u, UnitTally.CarryKeys[key], amount);
        // 第105期。**状態異常の書き込みを「回数」で書き手に帰属する**（計数のみ）。
        // 数えるのは <see cref="StatusKeys"/> 由来の7キー（毒・燃・痺・標・破片・傷・手番）だけ
        // ——強化／弱体には専用の窓口があり、被弾／移動は「書き込み」ではない。
        // **量ではなく回数**なのは、キーごとに単位が違って足せないため（第68期 `CarryUnits`）。
        if (key >= UnitTally.CarryPoison && key <= UnitTally.CarryIdle)
        {
            TurnStatusAll++;
            UnitState? writer = Mark.Owner;
            if (writer is null) TurnStatusNone++;
            else if (InOwnTurn(writer)) { TallyOf(writer).StatusOutInTurn++; TurnStatusIn++; }
            else { TallyOf(writer).StatusOutOffTurn++; TurnStatusOff++; }
        }
        UnitTally t = TallyOf(u);
        int[] amt = t.CarryAmount ??= new int[UnitTally.CarryKeys.Length];
        int[] cnt = t.CarryCount ??= new int[UnitTally.CarryKeys.Length];
        amt[key] += amount;
        cnt[key]++;

        int[][] pt = t.CarryProbeTurn ??= new int[UnitTally.CarryKeys.Length][];
        int[] probe = pt[key] ??= new int[UnitTally.CarryProbes.Length];
        for (int i = 0; i < UnitTally.CarryProbes.Length; i++)
        {
            if (probe[i] != 0 || amt[key] < UnitTally.CarryProbes[i]) continue;
            probe[i] = Math.Max(1, Turn);   // 開戦時の到達は 1 に丸める（0 を「未到達」に使う）
        }
    }

    /// <summary>
    /// <see cref="UnitState.AtkBonus"/> が上がった分を数える（第68期）。
    /// <b>窓口経由（<see cref="Whet"/>）も自己強化の9本もどちらもここへ来る</b>
    /// ——差し引きで「自前」が引けるのが狙いで、<b>盤面には一切影響しない</b>。
    /// </summary>
    /// <summary>
    /// <see cref="UnitState.AtkBonus"/> が動いた分を1件だけ帳簿へ入れる（<b>setter の1箇所からのみ来る</b>）。
    /// <b>盤面には一切影響しない</b>（誰も読んで分岐しない・<c>verbose</c> 非依存）。
    ///
    /// <para>第68期の <c>CarryAtkGain</c>（上がった分だけ）はそのまま。第106期 (T1) で
    /// <b>4つ目の通貨（強化・弱体）</b>の3分割をここに足した——観測点が setter なので、
    /// 窓口（<see cref="Whet"/> / <see cref="Dull"/>）を通らない自己強化の9本も数え漏らさない。
    /// 帰属は第94期 (T2) の印（<see cref="Mark"/>）で、印が立っていない箇所からの増減は
    /// <b>誰のものでもない出力</b>になる。</para>
    /// </summary>
    internal void NoteAtkMove(UnitState u, int delta)
    {
        UnitTally ut = TallyOf(u);
        if (delta > 0) ut.CarryAtkGain += delta;

        // 到達点と到達ターン（第106期 Q1）。setter の中なので `u.AtkBonus` は**更新後の値**。
        if (u.AtkBonus > ut.AtkPeak) { ut.AtkPeak = u.AtkBonus; ut.AtkPeakTurn = Math.Max(1, Turn); }
        int[] pr = ut.AtkProbeTurn ??= new int[UnitTally.AtkProbes.Length];
        for (int i = 0; i < pr.Length; i++)
            if (pr[i] == 0 && u.AtkBonus >= UnitTally.AtkProbes[i]) pr[i] = Math.Max(1, Turn);

        int mag = Math.Abs(delta);
        TurnBuffAll += mag;
        UnitState? writer = Mark.Owner;
        if (writer is null) TurnBuffNone += mag;
        else if (InOwnTurn(writer)) { TallyOf(writer).BuffOutInTurn += mag; TurnBuffIn += mag; }
        else { TallyOf(writer).BuffOutOffTurn += mag; TurnBuffOff += mag; }

        // 経路の全数（Phase 0 §1）。**印が無ければ NoMark の桶へ。**
        if (writer is null) { if (delta > 0) BuffGainNoMark += mag; else BuffLossNoMark += mag; }
        else if (delta > 0) BuffGainByTrait[(int)Mark.Id] += mag;
        else BuffLossByTrait[(int)Mark.Id] += mag;
    }

    /// <summary>
    /// <see cref="StatusKeys"/> のカウンタが増えた分を数える（第68期）。
    /// <see cref="UnitState.SetCounter"/> からのみ来る。<b>7キー以外は捨てる</b>
    /// （特性の私有キーは帳簿に載せない）。<b>盤面には一切影響しない。</b>
    ///
    /// <para><b>単位はキーで違う</b>（<see cref="UnitTally.CarryUnits"/>）。
    /// 毒・破片は<b>増分</b>（層・量）、燃は<b>残ターンの増分</b>、
    /// 痺・標・傷・手番は<b>回数</b>（0/1 のキーと、ターン番号を書く <c>IdleTurn</c> は
    /// 増分に意味が無いので 1 で数える）。</para>
    /// </summary>
    internal void NoteStatusGain(UnitState u, string key, int delta)
    {
        switch (key)
        {
            case StatusKeys.Poison: NoteCarry(u, UnitTally.CarryPoison, delta); break;
            case StatusKeys.Armor: NoteCarry(u, UnitTally.CarryArmor, delta); break;
            case StatusKeys.Burn: NoteCarry(u, UnitTally.CarryBurn, delta); break;
            // 第146期 段0（表示専用）: 付いた瞬間。**計数の隣に置くだけで盤面は1ビットも動かない。**
            case StatusKeys.Stun: NoteCarry(u, UnitTally.CarryStun, 1); EmitStun(u, StunLabels.Struck, Mark.Owner); break;
            // 第147期（表示専用）: 混乱が付いた瞬間。**計数（NoteCarry）は足していない**
            // ——`UnitTally.CarryKeys` を増やすと過去の期の帳簿が動く。書き手は Mark.Owner。
            case StatusKeys.Confused: EmitConfused(u, ConfusedLabels.Lost, Mark.Owner); break;
            case StatusKeys.Marked: NoteCarry(u, UnitTally.CarryMark, 1); break;
            case StatusKeys.Wound: NoteCarry(u, UnitTally.CarryWound, 1); break;
            case StatusKeys.IdleTurn: NoteCarry(u, UnitTally.CarryIdle, 1); break;
        }
    }

    public void NoteAttackRead(UnitState u)
    {
        UnitTally t = TallyOf(u);
        t.AttackReads++;

        // 到着と使用（第65期）。**受け取った後に1度でも攻撃力を出力へ変換したか**を、
        // 経路ごとに「保留 → 使用済み」へ移して数える。**盤面には一切影響しない。**
        if (t.WhetFirstTurn > 0) t.AttackReadsAfterWhet++;
        if (t.WhetPendingByRoute is int[] pend)
            for (int i = 0; i < pend.Length; i++)
                if (pend[i] > 0) { WhetUsedByRoute[i] += pend[i]; pend[i] = 0; }
    }

    internal void NoteFavorReceiver(UnitState receiver, int amount, bool whet)
    {
        Dictionary<string, int> d = whet ? FavorWhetTo : FavorDullTo;
        string k = receiver.Def.Name;
        d[k] = d.TryGetValue(k, out int a) ? a + amount : amount;
        if (whet && receiver.HasTrait(TraitId.Pyre)) FavorToPyre += amount;
    }

    /// <summary>
    /// 破裂の着火の強度（第59期）。<b>診断（blaze）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="BlazeRule.Default"/> ＝ 着火なし）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public BlazeRule Blaze { get; }

    /// <summary>
    /// 熾火の配布（第130期）。<b>診断（relay）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="EmberRule.Default"/> ＝ 配る）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public EmberRule Ember { get; }

    /// <summary>
    /// 火勢の強度（第133期）。<b>診断（wildfire）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="WildfireRule.Default"/> ＝ 不活性）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public WildfireRule Wildfire { get; }

    /// <summary>
    /// 軋みが響く閾値（第66期）。<b>診断（creak）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="CreakRule.Default"/> ＝ 無効）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public CreakRule Creak { get; }

    /// <summary>
    /// 断ちの待ち方と閾値（第74期）。<b>診断（wcost）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="SeverRule.Default"/> ＝ 第38期の現行）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public SeverRule Sever { get; }

    /// <summary>
    /// 薄刃の払い方（第75期）。<b>診断（blade）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="ThinBladeRule.Default"/> ＝ 常に 1）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public ThinBladeRule ThinBlade { get; }

    /// <summary>
    /// 棘の傷（第84期）。<b>診断（thorn）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="ThornRule.Default"/>）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public ThornRule Thorn { get; }

    /// <summary>
    /// 縫いの糸口（第85期）。<b>診断（suture2）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="SutureRule.Default"/> ＝ 敵側だけ）。
    /// </summary>
    public SutureRule Suture { get; }

    /// <summary>
    /// 縫いの発火口（第107期 (S3)・<see cref="SutureFireRule"/>）。
    /// <b>診断（hold2）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
    /// （既定は <see cref="SutureFireRule.Default"/> ＝ 現行の <see cref="SutureFire.Swing"/>）。
    /// </summary>
    public SutureFireRule SutureFire { get; }

    /// <summary>
    /// 巻き込み則（第85期・W2）。<b>診断（suture2）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="SpillWoundRule.Default"/> ＝ 無効）。
    /// </summary>
    public SpillWoundRule SpillWound { get; }

    /// <summary>
    /// 繕いの読み（第86期）。<b>診断（mender）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="MendRule.Default"/> ＝ 読まない）。
    /// </summary>
    public MendRule Mend { get; }

    /// <summary>
    /// 傷口の着火（第87期）。<b>診断（blaze2）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="IgniteRule.Default"/> ＝ 着火しない）。
    /// <para><b>名前が <c>Ignite</c> でないのは、<see cref="Ignite(UnitState, bool)"/>（燃焼の着火）と
    /// 衝突するため。</b> 指示書 §2-2 の <c>ctx.Ignite.Enabled</c> はこの <c>ctx.WoundIgnite.Enabled</c>。</para>
    /// </summary>
    public IgniteRule WoundIgnite { get; }

    /// <summary>
    /// 傷の引き取り（第89期）。<b>診断（gather）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="GatherRule.Default"/> ＝ 引き取らない）。
    /// </summary>
    public GatherRule Gather { get; }

    /// <summary>
    /// 滲み則（第90期）。<b>診断（soak）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="SoakRule.Default"/>）。
    /// <b>第91期に通貨ごとに分かれた</b>——<c>Soak.Poison</c> は <see cref="Poison"/>、<c>Soak.Burn</c> は <see cref="Ignite"/> が見る。
    /// </summary>
    public SoakRule Soak { get; }

    /// <summary>
    /// 深手（第93期）。<b>診断（deep）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="DeepRule.Default"/> ＝ 束ねない）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public DeepRule Deep { get; }

    /// <summary>
    /// 傷という通貨のノブ（第120期・<see cref="WoundRule"/>）。<b>診断（wound2）だけが渡す。</b>
    /// 既定は現行（書かれる・走査しない）なので、通常の実行では 1 ビットも動かない。
    /// </summary>
    public WoundRule Wounds { get; }

    /// <summary>
    /// 呪い（第96期）。<b>診断（hex）が版を差し替えるためだけの窓口</b>で、
    /// 通常の実行では誰も渡さない（既定は <see cref="CurseRule.Default"/> ＝ 共有しない）。
    /// 見るのは <see cref="HexTrait"/>（付与）と <see cref="ApplyDamage"/> の共有の段（1箇所）だけ。
    /// <para><b>第95期の「なまりが汚れの種類だけ重くなる」は同じ名前を取っていたが、
    /// 実体は滲み則そのものだった</b>ので第96期 (R1) で <c>SoakRule.DullPerKind</c> へ畳んだ
    /// ——名前の衝突を先に解いてから呪いを作っている。</para>
    /// </summary>
    public CurseRule Curse { get; }

    /// <summary>
    /// 背かれの規則（第103期・<see cref="BetrayRule"/>）。
    /// <b>既定は <see cref="BetrayRule.Default"/> ＝喚ばない</b>ので、
    /// ロスターに 52 枚目を足しても盤面は1ビットも動かない。
    /// </summary>
    public BetrayRule Betray { get; }

    /// <summary>
    /// 再行動の規則（第104期・<see cref="EncoreRule"/>）。
    /// <b>既定は <see cref="EncoreRule.Default"/> ＝再行動しない</b>ので、
    /// <see cref="NoteEncore"/> は計数だけを取って必ず即座に返る
    /// （<c>compare</c> 305 セルが 0 件であることが検算）。
    /// </summary>
    public EncoreRule Encore { get; }

    // =====================================================================================
    // 第106期 —— 保留の3枚のノブ。**既定は3つとも現行**（`compare` 305 セル 0 件が検算）。
    // =====================================================================================

    /// <summary>憤怒の育ち方（第106期 (T2)・<see cref="RageRule"/>）。</summary>
    public RageRule Rage { get; }

    /// <summary>繕いの代金（第106期 (T2)・<see cref="MenderCostRule"/>）。</summary>
    public MenderCostRule MenderCost { get; }

    /// <summary>散開の弾き（第106期 (T2)・<see cref="LooseRule"/>）。</summary>
    public LooseRule Loose { get; }

    /// <summary>身構え（第143期・<see cref="BraceRule"/>）。<b>既定（<c>Cap = 0</c>）では1バイトも動かない。</b></summary>
    public BraceRule Brace { get; }

    /// <summary>預かり（第153期・<see cref="WardRule"/>）。<b>保持者が盤上に居なければ1バイトも動かない。</b></summary>
    public WardRule Ward { get; }

    /// <summary>贖い（第155期・<see cref="IndulgenceTrait"/>）。<b>保持者が盤上に居なければ1バイトも動かない。</b></summary>
    public IndulgenceRule Indulgence { get; }

    /// <summary>混乱（第146期・<see cref="ConfusionRule"/>）。<b>既定（<c>Active = false</c>）では1バイトも動かない。</b></summary>
    public ConfusionRule Confusion { get; }

    /// <summary>喧噪（第144期・<see cref="ShufflerRule"/>）。<b><c>Foes = false</c> では第143期と1バイトも違わない。</b></summary>
    public ShufflerRule Shuffler { get; }

    /// <summary>
    /// 前倒し（第149期・<see cref="HasteRule"/>）。<b>既定（<c>Pick = None</c>）では
    /// <c>order</c> に一切触らない</b>ので、盤面も乱数列も1ビット動かない。
    /// </summary>
    public HasteRule Haste { get; }

    /// <summary>
    /// <b><see cref="StatusKeys.Confused"/> を立てうる経路が1本でもあるか</b>（第147期）。
    /// ctor で1回だけ計算する。
    ///
    /// <para><b>読む側（<see cref="FoesOf"/> / <c>ConsumeConfusion</c>）は「誰が立てたか」を見ない</b>
    /// ——ノブが決めるのは<b>誰が立てるか</b>だけで、立っていれば従う。
    /// それでも <c>RawCounter</c> を既定の経路で毎回引かないのは、<c>FoesOf</c> が1回の攻撃で
    /// 複数回通り、<c>compare</c> / <c>layout</c> が数百万戦を回すため（軛・受け流しと同じ短絡の作法）。</para>
    ///
    /// <para><b>唯一の抜け穴は業（<c>ScapegoatTrait</c>）がカウンタを移す経路</b>だが、
    /// あの駒は <c>UnitCatalog.All</c> に保持者 0 枚（第49期・残置）なので盤面には出ない。</para>
    /// </summary>
    internal bool ConfusionLive { get; }

    /// <summary>憤怒の発火の内訳（<b>版に依らない</b>。Phase 0 で「1発と数える集合」を出すため）。</summary>
    public int RageFiresFromFoe, RageFiresFromAlly, RageFiresNoSource;

    /// <summary>
    /// 再行動の中か（★ 1ホップ）。<b>再行動から生まれた撃破では、さらに再行動しない。</b>
    /// <see cref="Relaying"/> / <see cref="Shoving"/> と同じ形の再入ガードで、
    /// <c>Trait</c> の static に置いてはいけない（Trait は共有シングルトンで
    /// <c>layout</c> は戦闘を並列実行する）。
    /// </summary>
    public bool Encoring;

    /// <summary>
    /// 尾灯（第108期）が手番を譲っている最中か（1ホップ）。
    /// <b>譲った手番の中で敵が倒れても、そこから再度譲らない。</b>
    /// <see cref="Encoring"/> / <see cref="Relaying"/> / <see cref="Shoving"/> と同じ形の再入ガードで、
    /// <c>Trait</c> の static に置いてはいけない（Trait は共有シングルトンで
    /// <c>layout</c> は戦闘を並列実行する）。
    ///
    /// <para><b>トモが1枚でも要る</b>——譲った相手が敵を倒すと <c>OnAnyDeath</c> が
    /// そのターンの印を立て直すが、トモの手番はもう終わっているので単独では往復しない。
    /// <b>2枚同席すると互いに譲り合って無限に往復する</b>ので、そこを構造で止める
    /// （第41期の突き返しと同じ判断——「敵側に持たせた瞬間に無限往復する」）。</para>
    /// </summary>
    public bool Yielding;

    // =====================================================================================
    // 第110期 —— 尾灯の譲渡条件（TaillightRule）。**既定は V0 ＝現行**で、
    // 渡さない限り `OnAnyDeath` の分岐も `_tlChainAtk` の走査も1回も走らない
    // （`compare` 305 セルが 0 件であることが検算）。
    // =====================================================================================

    /// <summary>尾灯の譲渡条件（第110期・<see cref="TaillightRule"/>）。</summary>
    public TaillightRule Taillight { get; }

    /// <summary>V2（即時）のときだけ真。<b>短絡の作法</b>（軛の Cap・粛の保持者走査と同じ）。</summary>
    public bool TaillightImmediate => Taillight.Mode == YieldMode.Immediate;

    // =====================================================================================
    // 第115期 —— 積み過ぎ（ReaderRule）。**強化の2枚目の読み手。**
    // engine に足したのは<b>規則の受け渡しと計数だけ</b>で、判定は `OverloadTrait.ModifyPattern`
    // の中にある（軋み＝`CreakRule` と同じ形）。**既定は不活性**で、渡さない限り
    // `ModifyPattern` が最初の比較1つで抜けるので乱数も盤面も1ビットも動かない。
    // =====================================================================================

    /// <summary>積み過ぎの強度（第115期・<see cref="ReaderRule"/>）。</summary>
    public ReaderRule Reader { get; }

    /// <summary>規則が生きているときだけ真。<b>短絡の作法</b>（軛の Cap・粛の保持者走査と同じ）。</summary>
    public bool ReaderActive => Reader.Threshold > 0;

    /// <summary>
    /// 積み過ぎ（第115期）の門の計数。<b>ターン頭に1回だけ、盤面を読むだけ。</b>
    /// 呼び出しは `Run` のターンループ（`TickStatuses` の直後・`NoteHexCensus` の隣）1箇所。
    ///
    /// <para><b>規則を無効にしていても数える</b>——「閾値に届く供給があったか」は
    /// 版に依らず読めたほうがよい（第65期の到着の帳簿と同じ判断）。門1（到達）の分子と、
    /// 空振り（閾値は超えたが振れなかったターン）の分母がここで積まれる。</para>
    /// </summary>
    public void NoteReaderCensus()
    {
        int line = Reader.Threshold > 0 ? Reader.Threshold : ReaderProbeLine;
        foreach (UnitState u in _units)
        {
            if (!u.IsAlive || !PatternReader(u)) continue;
            UnitTally t = TallyOf(u);
            t.ReaderTurns++;
            t.ReaderBonusSum += u.AtkBonus;
            if (u.AtkBonus > t.ReaderBonusMax) t.ReaderBonusMax = u.AtkBonus;
            int[] probe = t.ReaderProbeTurns ??= new int[UnitTally.ReaderProbes.Length];
            for (int i = 0; i < UnitTally.ReaderProbes.Length; i++)
                if (u.AtkBonus >= UnitTally.ReaderProbes[i]) probe[i]++;
            if (u.AtkBonus < line) continue;
            t.ReaderOverTurns++;
            t.ReaderOverTurnMark = Turn;   // 空振りの分母は**ターン頭の印**（振りの側と同じ瞬間で数える）
            if (t.ReaderFirstOverTurn == 0) t.ReaderFirstOverTurn = Math.Max(1, Turn);
        }
    }

    /// <summary>
    /// 規則を無効にした版でも「閾値に届いていたか」を数えるための既定の線（第115期）。
    /// <b>盤面には一切影響しない</b>——採用値と同じ 5 を置いてあるだけで、
    /// V0 と V1 の門1 を同じ物差しで並べるために要る。
    /// </summary>
    public const int ReaderProbeLine = 5;

    /// <summary>
    /// 積み過ぎの門を数える対象（第128期に対象を広げた）。<b>計数の条件であって、規則ではない。</b>
    ///
    /// <para>第115期は保持者が積み過ぎ（<c>Overload</c>）1枚だけだったが、第127期に段違い
    /// （<see cref="TraitId.GradeStep"/> ほか）が増えた。**同じ値（<c>AtkBonus</c>）を同じ閾値で読む札**
    /// なので門の分母・分子は同じ形で数えられる。<b>盤面は1ビットも動かない</b>
    /// ——増えるのは <see cref="UnitTally"/> の列だけで、誰も読んで分岐しない。</para>
    /// </summary>
    private static bool PatternReader(UnitState u)
        => u.HasTrait(TraitId.Overload) || u.HasTrait(TraitId.GradeStep)
           || u.HasTrait(TraitId.GradePierce) || u.HasTrait(TraitId.GradeAll);

    // =====================================================================================
    // 第117期 —— ボスの土台（BossRule）。**傾きを測るためだけの計数。engine に規則は1本も無い。**
    //
    // 既定（`BossRule.Default` ＝ 数えない）では `BossCensus` が偽なので、走査も確保も
    // 1回も走らない（`compare` 305 セルが 0 件であることが検算）。**盤面は読むだけ。**
    // =====================================================================================

    /// <summary>ボスの土台の計数（第117期・<see cref="BossRule"/>）。</summary>
    public BossRule Boss { get; }

    /// <summary>計数が生きているときだけ真。<b>短絡の作法</b>（軛の Cap・粛の保持者走査と同じ）。</summary>
    public bool BossCensus => Boss.Census;

    /// <summary>
    /// ボスの土台（第117期）の時系列。<b>ターン頭に1回だけ、盤面を読むだけ。</b>
    /// 呼び出しは `Run` のターンループ（`NoteReaderCensus` の隣）1箇所。
    ///
    /// <para>写すのは <see cref="UnitState.CurrentAttack"/>（<c>AtkBonus</c> の生値ではない）。
    /// ホタの燃焼倍率・ウツの逆しまは <c>ModifyAttack</c> の側にあるので、
    /// 生値で取ると「育ち」を取り落とす（指示書の自己検査 (d)）。</para>
    ///
    /// <para><b>味方だけを写す。</b> 傾きの分子も分母も味方の側にしかない。</para>
    /// </summary>
    public void NoteBossCensus()
    {
        if (!BossCensus) return;
        foreach (UnitState u in _units)
        {
            if (!u.IsAlive || u.TeamId != PlayerTeam) continue;
            UnitTally t = TallyOf(u);
            t.BossAliveTurns++;
            t.BossLastAliveTurn = Turn;
            int[] atk = t.BossAtkByTurn ??= new int[BattleEngine.MaxTurns + 2];
            if (Turn >= 0 && Turn < atk.Length) atk[Turn] = u.CurrentAttack;
            // 第118期。**同じ場所・同じ guard で HP も写す**（交差点＝門2 の材料）。
            int[] hp = t.BossHpByTurn ??= new int[BattleEngine.MaxTurns + 2];
            if (Turn >= 0 && Turn < hp.Length) hp[Turn] = u.Hp;
        }
    }

    // =====================================================================================
    // 第120期 —— 傷という通貨の棚卸し。**engine に規則は1本も足していない。計数だけ。**
    //
    // 足したのは (1) 規則の受け渡し（<see cref="WoundRule"/>）と (2) 計数の3種類:
    //   在庫（ターン頭の走査。`Census` のときだけ）／消滅の帳簿（減算の全数）／
    //   実効（盤面から実際に減った HP）。
    //
    // **帳簿が閉じることが自己検査 (a)**——書かれた傷 ＝ 消えた傷 ＋ 決着時に残っていた傷。
    // 加算は <see cref="Wound"/> の1箇所だけなので、減算の側を全数当たれば必ず閉じる。
    // =====================================================================================

    /// <summary><see cref="WoundLoss"/> の要素数。</summary>
    public const int WoundLossCount = 8;

    /// <summary>傷の在庫の走査を回すか。<b>規則が真のときだけ。</b></summary>
    public bool WoundCensus => Wounds.Census;

    /// <summary>消滅の帳簿（添字は <see cref="WoundLoss"/>）。全体と、味方側の駒から消えたぶん。</summary>
    public readonly int[] WoundLossAll = new int[WoundLossCount];
    /// <inheritdoc cref="WoundLossAll"/>
    public readonly int[] WoundLossAlly = new int[WoundLossCount];

    /// <summary>書かれた傷（<b>実際に counter が増えたぶん</b>）。陣営は<b>書かれた側</b>で割る。</summary>
    public readonly int[] WoundWriteAlly = new int[WoundRouteCount];
    /// <inheritdoc cref="WoundWriteAlly"/>
    public readonly int[] WoundWriteFoe = new int[WoundRouteCount];

    /// <summary>在庫の走査（ターン頭）。延べ・最大・「1体でもいたターン」の数。</summary>
    public long WoundStockAllySum, WoundStockFoeSum;
    /// <inheritdoc cref="WoundStockAllySum"/>
    public int WoundStockTurns, WoundStockAllyMax, WoundStockFoeMax, WoundTurnsAllyAny, WoundTurnsFoeAny;

    /// <summary>深さの分布（走査の延べ。添字 0 = 深さ1 / 1 = 深さ2 / 2 = 深さ3以上）。</summary>
    public readonly long[] WoundDepthAlly = new long[3];
    /// <inheritdoc cref="WoundDepthAlly"/>
    public readonly long[] WoundDepthFoe = new long[3];

    /// <summary>書かれてから読まれるまでのターン数（読まれた傷だけが分母）。</summary>
    public long WoundLagSum;
    /// <inheritdoc cref="WoundLagSum"/>
    public int WoundLagCount;

    /// <summary>
    /// <b>盤面から実際に減った HP の総量</b>（第120期）。<see cref="ApplyDamage"/> が
    /// HP を引いた直後に、<b>過剰分（オーバーキル）を除いた実額</b>を足す。
    /// <para><b>誰も読んで分岐しない。</b> 読み手の呼び出しを挟んで差を取ると、
    /// 肩代わりで分割された段も貫きの各段も含めた<b>正味の効き</b>が1つの数で取れる
    /// ——名目（定数 3 × 傷の数）との差が「空振り」そのものになる。</para>
    /// </summary>
    public long HpRemoved;

    // ===== 第132期 段1: 上限（軛）の帳簿 =========================================================
    //
    // **誰も読んで分岐しない。** 盤面にも乱数列にも1ビットも触らない。
    // 第25期に軛を採ってから第131期まで「何が何回・何点切られたか」を数える窓口が1つも無く、
    // 「型ごとに上限との相性が逆を向く」が**3期にわたって未測定のまま指示書に書き継がれていた**。
    //
    // 添字は攻撃型（`AttackPattern`）で、**4 は「型なし」**——継続ダメージ（毒・燃焼）・反撃・
    // 肩代わりの中継・徴収はどれも `pattern` を渡さないのでここに落ちる。
    // **肩代わりの中継が元の型に戻らないのは意図した形**（分割された段は別の一撃なので、
    // 「どの型の一撃が切られたか」に足すと二重に数えることになる）。

    /// <summary>
    /// 攻撃型ごとの「軛に切られた一撃」の回数。
    /// <b>添字は <c>型 + (受けたのが味方なら 5)</c></b>——0..4 が敵に入った一撃（＝味方の刃）、
    /// 5..9 が味方に入った一撃（＝敵の刃）。<b>型の 4 は「型なし」。</b>
    /// </summary>
    public readonly long[] YokeCutHits = new long[10];
    /// <summary>同・切り落とされた量（<c>amount - Cap</c> の合計）。</summary>
    public readonly long[] YokeCutLost = new long[10];
    /// <summary>同・切られたうえで通った量（<c>Cap</c> の合計）。</summary>
    public readonly long[] YokeCutPassed = new long[10];
    /// <summary>切られなかったが上限に近い一撃（<c>Cap * 4 / 5</c> 超〜<c>Cap</c>）の回数。</summary>
    public readonly long[] YokeNearHits = new long[10];
    /// <summary>上限が効いている間に HP へ届いた回数（切られた一撃も含む）。</summary>
    public readonly long[] YokeInHits = new long[10];
    /// <summary>同・量（上限を通した後の実額）。</summary>
    public readonly long[] YokeInAmount = new long[10];
    /// <summary>同・その一撃で相手が倒れた回数。</summary>
    public readonly long[] YokeKills = new long[10];
    /// <summary>同・過剰分（<c>amount - 直前のHP</c>。倒した一撃だけ）。</summary>
    public readonly long[] YokeOverkill = new long[10];
    /// <summary>切られた側が味方（プレイヤー）だった回数と量。</summary>
    public long YokeCutOnPlayerHits, YokeCutOnPlayerLost;
    /// <summary>切られた側が敵だった回数と量。</summary>
    public long YokeCutOnEnemyHits, YokeCutOnEnemyLost;
    /// <summary>上限が効いている間に、破片（<c>Armor</c>）が上限の<b>手前</b>で食った量。</summary>
    public long YokeArmorSoak;
    /// <summary>上限が効いている間に HP へ届いた量のうち、肩代わりの中継だったぶん。</summary>
    public long YokeInRelayedHits, YokeInRelayedAmount;
    /// <summary>同・継続ダメージ（毒・燃焼の刻み）だったぶん。</summary>
    public long YokeInBurnHits, YokeInBurnAmount;
    /// <summary>同・徴収（生贄・吸い・置き去りの削り）だったぶん。</summary>
    public long YokeInLevyHits, YokeInLevyAmount;
    /// <summary>
    /// <see cref="ApplyDamage"/> を<b>1度も通さずに</b>書かれた HP の減り（繕いの代金）。
    /// <b>上限も破片も肩代わりも通らない</b>ので、回避経路の3分類でいちばん外側にいる。
    /// </summary>
    public long DirectHpLoss;
    /// <summary>誰の一撃が切られたか（<c>Def.Id</c> → 回数・切られた量）。</summary>
    public readonly Dictionary<string, (long Hits, long Lost)> YokeCutBy = new();

    /// <summary>軛の保持者（<see cref="Add"/> が積む）。<b>全駒の走査を避けるためのキャッシュ。</b></summary>
    readonly List<UnitState> _yokeHolders = new();

    // =====================================================================================
    // 第134期 段1・段2 —— 重ね掛けと盤面ルールの対称性の帳簿。**計数専用。**
    //
    // **どの規則も読まない。** 足したのは (a) 下の配列と辞書、(b) `Ignite` / `TickStatuses` /
    // `Heal` / `CanActOutOfTurn` に置いた `Note*` の呼び出し、(c) `Run` の組み立てだけで、
    // **盤面の分岐も乱数も1ビットも動かない**（受け入れ条件 A1・A2）。
    //
    // 陣営の添字は**課税された側**（0 = 敵 / 1 = 味方）。第132期の `YokeLedger` に揃えてある。
    // =====================================================================================

    /// <summary>陣営の添字（0 = 敵 / 1 = 味方）。<b>第134期の帳簿はすべてこの向き。</b></summary>
    static int SideOf(UnitState u) => u.TeamId == PlayerTeam ? 1 : 0;

    /// <summary>点け直し回数の分布の段数。<b>添字 5 は「5回以上」</b>。</summary>
    public const int BurnHistBuckets = 6;

    public readonly long[] BurnLitSide = new long[2];
    public readonly long[] BurnRelitSide = new long[2];
    public readonly long[] BurnEpisodes = new long[2];
    public readonly long[] BurnRelitSum = new long[2];
    public readonly long[] BurnRelitMax = new long[2];
    public readonly long[][] BurnHist = { new long[BurnHistBuckets], new long[BurnHistBuckets] };
    public readonly long[] BurnEndExpired = new long[2];
    public readonly long[] BurnEndDeath = new long[2];
    public readonly long[] BurnEndAlive = new long[2];

    /// <summary>点けた側の帳簿（<c>Def.Id</c> → 点けた回数・煽った回数）。</summary>
    public readonly Dictionary<string, (long Lit, long Relit)> BurnBy = new();

    /// <summary>点けられた側の帳簿（<c>Def.Id</c> → 点いた回数・煽られた回数）。</summary>
    public readonly Dictionary<string, (long Lit, long Relit)> BurnOn = new();

    /// <summary>
    /// いま開いている区間の点け直し回数（<c>InstanceId</c> → 回数）。
    /// <b>区間は「燃えていない駒に火が点いた瞬間」に開く</b>ので、キーがあること自体が
    /// 「その駒がいま燃えている」と同値になる（<see cref="CloseBurnEpisode"/> が閉じるまで）。
    /// </summary>
    readonly Dictionary<int, int> _burnOpen = new();

    /// <summary>着火の帳簿（<see cref="Ignite"/> の中から1回だけ呼ぶ）。<b>盤面には触らない。</b></summary>
    void NoteIgnite(UnitState target, UnitState? source, bool relit)
    {
        int side = SideOf(target);
        if (relit)
        {
            BurnRelitSide[side]++;
            // 区間が開いていない既燃は原理的に無い（区間はここでしか開かず、
            // <see cref="CloseBurnEpisode"/> でしか閉じない）が、キーが無ければ 0 から開き直す
            // ——数え落とすより、開き直したことが分布に出る形にしておく。
            _burnOpen[target.InstanceId] = _burnOpen.TryGetValue(target.InstanceId, out int n) ? n + 1 : 0;
        }
        else
        {
            BurnLitSide[side]++;
            _burnOpen[target.InstanceId] = 0;
        }

        BurnOn.TryGetValue(target.Def.Id, out var on);
        BurnOn[target.Def.Id] = relit ? (on.Lit, on.Relit + 1) : (on.Lit + 1, on.Relit);

        if (source is not null)
        {
            BurnBy.TryGetValue(source.Def.Id, out var by);
            BurnBy[source.Def.Id] = relit ? (by.Lit, by.Relit + 1) : (by.Lit + 1, by.Relit);
        }
    }

    /// <summary>
    /// 区間を閉じる（第134期 段1）。<b>閉じ方は3通り</b>——燃え尽きた（<paramref name="expired"/>）／
    /// 燃えたまま倒れた／燃えたまま決着した。<b>盤面には触らない。</b>
    /// </summary>
    void CloseBurnEpisode(UnitState u, bool expired)
    {
        if (!_burnOpen.Remove(u.InstanceId, out int relit)) return;
        int side = SideOf(u);
        BurnEpisodes[side]++;
        BurnRelitSum[side] += relit;
        if (relit > BurnRelitMax[side]) BurnRelitMax[side] = relit;
        BurnHist[side][Math.Min(relit, BurnHistBuckets - 1)]++;
        if (expired) BurnEndExpired[side]++;
        else if (u.IsAlive) BurnEndAlive[side]++;
        else BurnEndDeath[side]++;
    }

    /// <summary>
    /// 決着時に開いたままの区間を閉じる（第134期 段1）。<b>死者も数える</b>——
    /// 燃えたまま倒れた駒の区間は <see cref="TickStatuses"/> が二度と触らないので、
    /// ここで閉じないと帳簿から丸ごと落ちる。<b>1戦につき最後に1度だけ呼ぶ。</b>
    /// </summary>
    public void CloseBurnLedger()
    {
        foreach (UnitState u in _units.ToList()) CloseBurnEpisode(u, expired: false);
    }

    // =====================================================================================
    // 第150期 段A —— 標（<c>StatusKeys.Marked</c>）の一生の帳簿。**計数専用。**
    //
    // **どの規則も読まない。** 足したのは (a) 下の配列と辞書、(b) `Add` の短絡フラグ、
    // (c) ターン頭の走査（`ScanMarkLedger`）、(d) 消す側3箇所に置いた `Note*` の呼び出し、
    // (e) `Run` の組み立てだけで、**盤面の分岐も乱数も1ビットも動かない**。
    //
    // **区間の定義**: 「標が付いていない駒に標が付いた」瞬間に開き、次のどれかで閉じる:
    //
    //     消費   止め（トメ）が殴って消した                 ＝ 設計どおり働いた
    //     死亡   標を持ったまま倒れた                       ＝ 対抗仮説の直接の証拠
    //     剥がし 逸らし（ソラ）が味方から引き剥がした
    //     替え   駆り立て（カリ）が別の相手へ付け替えた
    //     他     上のどれでもない（**0 でなければ経路を数え落としている**）
    //     残存   決着時にまだ立っていた
    //
    // **区間を開くのはターン頭の走査だけ**——標の書き手4枚（囃し立て・逸らし・駆り立て・業）は
    // すべて `OnBattleStart` か `OnTurnStart` なので、行動順ループが回る前に書き終わっている。
    // 消す側は手番の中でも走る（消費・死亡）ので、そちらは即時に閉じる。
    // **駆り立ては毎ターン `prev` を 0 にしてから付け直す**ので、同じ相手に付け直した場合は
    // 走査の時点で標が立っており区間は閉じない（＝「替え」は宛先が変わったときだけ数える）。
    //
    // 陣営の添字は**標が付いた側**（0 = 敵に付いた標 / 1 = 味方に付いた標）。
    // `駆り立て改` と `止め` は書き手の向きが逆なので、混ぜると読めない（指示書 Q0-3）。
    // =====================================================================================

    /// <summary>標を書ける駒が盤上にいるか（<see cref="Add"/> が立てる）。<b>走査の短絡専用。</b></summary>
    public bool MarkActive { get; private set; }

    public readonly long[] MarkOpened = new long[2];
    public readonly long[] MarkEndConsumed = new long[2];
    public readonly long[] MarkEndDied = new long[2];
    public readonly long[] MarkEndDiedByFinisher = new long[2];
    public readonly long[] MarkEndStrip = new long[2];
    public readonly long[] MarkEndGoad = new long[2];
    public readonly long[] MarkEndOther = new long[2];
    public readonly long[] MarkEndStanding = new long[2];
    public readonly long[] MarkLifeSum = new long[2];
    public readonly long[] MarkLifeMax = new long[2];

    /// <summary>標が立っているあいだに標持ちが受けた攻撃の回数（<c>source</c> つきだけ＝継続ダメージは外れる）。</summary>
    public readonly long[] MarkHits = new long[2];

    /// <summary>そのうち止め（<see cref="TraitId.Finisher"/>）が入れたもの。</summary>
    public readonly long[] MarkHitsByFinisher = new long[2];

    /// <summary>標が付いた側の帳簿（<c>Def.Id</c> → 開いた区間・消費・死亡）。</summary>
    public readonly Dictionary<string, (long Opened, long Consumed, long Died)> MarkOn = new();

    /// <summary>いま開いている区間（<c>InstanceId</c> → 開いたターン）。</summary>
    readonly Dictionary<int, int> _markOpen = new();

    /// <summary>閉じた理由の下書き（<c>InstanceId</c> → (ターン, 1 = 剥がし / 2 = 替え)）。同一ターンに両方来たら後勝ち。</summary>
    readonly Dictionary<int, (int Turn, int Why)> _markHint = new();

    /// <summary>逸らしが味方から引き剥がした（<see cref="DivertTrait"/> から。<b>盤面には触らない</b>）。</summary>
    public void NoteMarkStrip(UnitState u)
    {
        if (MarkActive) _markHint[u.InstanceId] = (_turn, 1);
    }

    /// <summary>駆り立てが前の相手の標を消した（<see cref="GoadTrait"/> から。<b>同上</b>）。</summary>
    public void NoteMarkGoadClear(UnitState u)
    {
        if (MarkActive) _markHint[u.InstanceId] = (_turn, 2);
    }

    /// <summary>止めが標を消費した（<see cref="FinisherTrait"/> から。<b>同上</b>）。手番の中で即時に閉じる。</summary>
    public void NoteMarkConsumed(UnitState u)
    {
        if (!MarkActive || !_markOpen.ContainsKey(u.InstanceId)) return;
        CloseMarkEpisode(u, 0);
        MarkOn.TryGetValue(u.Def.Id, out var on);
        MarkOn[u.Def.Id] = (on.Opened, on.Consumed + 1, on.Died);
    }

    /// <summary>標を持ったまま倒れた（<see cref="HandleDeath"/> から。<b>同上</b>）。</summary>
    void NoteMarkDeath(UnitState u, UnitState? killer)
    {
        if (!MarkActive || u.RawCounter(StatusKeys.Marked) <= 0) return;
        if (!_markOpen.ContainsKey(u.InstanceId)) return;
        int side = SideOf(u);
        if (killer is not null && killer.HasTrait(TraitId.Finisher)) MarkEndDiedByFinisher[side]++;
        CloseMarkEpisode(u, 1);
        MarkOn.TryGetValue(u.Def.Id, out var on);
        MarkOn[u.Def.Id] = (on.Opened, on.Consumed, on.Died + 1);
    }

    /// <summary>標持ちが殴られた（<see cref="ApplyDamage"/> から。<b>同上</b>）。</summary>
    void NoteMarkHit(UnitState target, UnitState? source)
    {
        if (!MarkActive || source is null || target.RawCounter(StatusKeys.Marked) <= 0) return;
        int side = SideOf(target);
        MarkHits[side]++;
        if (source.HasTrait(TraitId.Finisher)) MarkHitsByFinisher[side]++;
    }

    /// <summary>
    /// 区間を閉じる。<paramref name="why"/> は 0 = 消費 / 1 = 死亡 / 2 = 走査（理由は下書きから）/ 3 = 残存。
    /// </summary>
    void CloseMarkEpisode(UnitState u, int why)
    {
        if (!_markOpen.Remove(u.InstanceId, out int since)) return;
        int side = SideOf(u);
        long life = _turn - since;
        MarkLifeSum[side] += life;
        if (life > MarkLifeMax[side]) MarkLifeMax[side] = life;
        switch (why)
        {
            case 0: MarkEndConsumed[side]++; break;
            case 1: MarkEndDied[side]++; break;
            case 3: MarkEndStanding[side]++; break;
            default:
                if (_markHint.TryGetValue(u.InstanceId, out var h) && h.Turn == _turn)
                {
                    if (h.Why == 1) MarkEndStrip[side]++;
                    else MarkEndGoad[side]++;
                }
                else MarkEndOther[side]++;
                break;
        }
    }

    /// <summary>
    /// ターン頭の走査（第150期 段A）。<b><c>OnTurnStart</c> が全部走り終わった直後に1回だけ呼ぶ</b>
    /// ——標の書き手はすべてそこまでに書き終わる（<see cref="NoteFinisherMarkAges"/> と同じ位置・同じ理由）。
    /// <b>盤面は1つも動かさない</b>（私有の辞書を書くだけ）。
    /// </summary>
    internal void ScanMarkLedger()
    {
        if (!MarkActive) return;
        foreach (UnitState u in _units)
        {
            if (!u.IsAlive) continue;
            bool marked = u.RawCounter(StatusKeys.Marked) > 0;
            bool open = _markOpen.ContainsKey(u.InstanceId);
            if (marked && !open)
            {
                _markOpen[u.InstanceId] = _turn;
                MarkOpened[SideOf(u)]++;
                MarkOn.TryGetValue(u.Def.Id, out var on);
                MarkOn[u.Def.Id] = (on.Opened + 1, on.Consumed, on.Died);
            }
            else if (!marked && open)
            {
                CloseMarkEpisode(u, 2);
            }
        }
    }

    /// <summary>決着時に開いたままの区間を閉じる。<b>1戦につき最後に1度だけ呼ぶ。</b></summary>
    public void CloseMarkLedger()
    {
        if (!MarkActive) return;
        foreach (UnitState u in _units.ToList()) CloseMarkEpisode(u, 3);
    }

    // --- 預かりの帳簿（第153期・**計数専用。どの規則も読まない**） --------------------------
    // 収支が閉じること（積んだ ＝ 返した ＋ 没収 ＋ 残額）が自己検査 (c)。

    /// <summary>預かりに積んだ総量。</summary>
    public long WardStacked;
    /// <summary>返そうとした総量（プールから出そうとした量）。</summary>
    public long WardReleaseAsked;
    /// <summary><b>実際に HP が増えた量</b>＝プールから実際に減った量。</summary>
    public long WardReleased;
    /// <summary>閾値に達して全額を返そうとした回数（<c>Burst</c>）。</summary>
    public long WardBursts;
    /// <summary>毎ターン頭に返そうとした回数（<c>Drip</c>）。</summary>
    public long WardDrips;
    /// <summary>返そうとしたが1点も入らなかった回数（満タン・渇き・支援拒否）。</summary>
    public long WardDry;
    /// <summary>そのうち渇きで止まった回数。</summary>
    public long WardDryDrought;
    /// <summary>そのうち支援拒否（<c>Stoic</c>）で止まった回数。</summary>
    public long WardDryStoic;
    /// <summary>没収の発火回数。</summary>
    public long WardForfeits;
    /// <summary>没収された預かりの総量（＝敵へ渡した名目量）。</summary>
    public long WardForfeited;
    /// <summary>没収で敵の HP が実際に増えた量（体数ぶん重なるので名目とは別物）。</summary>
    public long WardForfeitHealed;
    /// <summary>決着時にプールに残っていた量（<see cref="CloseWardLedger"/> が閉じる）。</summary>
    public long WardResidual;

    // --- 第154期の代金の帳簿（**計数専用。どの規則も読まない**） ---

    /// <summary>荷が実際に被ダメージを増やした回数。</summary>
    public long WardBurdenHits;
    /// <summary>荷が増やした被ダメージの総量（<b>増えた分だけ</b>。素のダメージは含まない）。</summary>
    public long WardBurdenAdded;
    /// <summary>重りが乗った振りの回数（<c>PerformAttack</c> が <c>atk</c> を作った瞬間に数える）。</summary>
    public long WardLadenSwings;
    /// <summary>そのうち下限 1 で切られた振り（<b>名目より実効が小さい</b>）。</summary>
    public long WardLadenFloored;
    /// <summary><b>実際に振られなかった打点</b>（名目。下限で切られた分を含む）。</summary>
    public long WardLadenSwingLost;
    /// <summary>ターン頭に数えた「下がっている攻撃力の総量」（<b>在庫の側</b>・延べターン）。</summary>
    public long WardLadenNominal;
    /// <summary>ターン頭に預かりを抱えていた駒の延べ数（<see cref="WardLadenNominal"/> の分母）。</summary>
    public long WardLadenCarriers;

    /// <summary>
    /// 重りの在庫（第154期・<b>計数専用</b>）。ターン頭に1度だけ、生きている全駒について
    /// 「いま何点ぶん攻撃力が下がっているか」を数える。<b>盤面は読むだけ。</b>
    /// </summary>
    public void NoteWardCensus()
    {
        if (!LadenActive) return;
        foreach (UnitState u in _units)
        {
            if (!u.IsAlive) continue;
            int pen = LadenPenalty(u);
            if (pen <= 0) continue;
            WardLadenCarriers++;
            WardLadenNominal += pen;
        }
    }

    /// <summary>重りが乗った一振りを数える（<c>PerformAttack</c> から・<b>計数専用</b>）。</summary>
    /// <param name="atk">下限まで含めて解決したあとの打点。</param>
    internal void NoteLadenSwing(UnitState actor, int atk)
    {
        int pen = LadenPenalty(actor);
        if (pen <= 0) return;
        WardLadenSwings++;
        WardLadenSwingLost += pen;
        TallyOf(actor).WardLadenLost += pen;
        // 下限 1 に当たった振りだけは名目 > 実効になる（`atk > 1` なら名目＝実効）。
        if (atk <= 1) WardLadenFloored++;
    }
    /// <summary>預けた駒ごとの内訳（<c>Def.Id</c> → 積んだ量・返った量）。</summary>
    public readonly Dictionary<string, (long Stacked, long Released)> WardOn = new();

    internal void NoteWardStacked(UnitState by, UnitState on, int amount)
    {
        WardStacked += amount;
        WardOn.TryGetValue(on.Def.Id, out var a);
        WardOn[on.Def.Id] = (a.Stacked + amount, a.Released);
        TallyOf(by).WardStacked += amount;
    }

    internal void NoteWardRelease(UnitState by, UnitState on, int asked, int healed, bool burst)
    {
        WardReleaseAsked += asked;
        WardReleased += healed;
        if (burst) WardBursts++; else WardDrips++;
        if (healed <= 0)
        {
            WardDry++;
            if (DroughtBinding) WardDryDrought++;
            else if (!on.AcceptsSupport) WardDryStoic++;
        }
        WardOn.TryGetValue(on.Def.Id, out var a);
        WardOn[on.Def.Id] = (a.Stacked, a.Released + healed);
        TallyOf(by).WardReleased += healed;
    }

    internal void NoteWardForfeit(UnitState by, UnitState dead, int pool, int healed)
    {
        WardForfeits++;
        WardForfeited += pool;
        WardForfeitHealed += healed;
        TallyOf(by).WardForfeited += pool;
    }

    /// <summary>決着時に残っていた預かりを数える（<b>死者も含めた全駒を1度だけ</b>）。</summary>
    public void CloseWardLedger()
    {
        foreach (UnitState u in _units) WardResidual += u.RawCounter(StatusKeys.Ward);
    }

    // =====================================================================================
    // 第155期 —— 贖い（免罪・取り立て・焼き）の帳簿。**計数専用で、どの規則も読まない。**
    //
    // 収支は **積んだ負債 ＝ 取り立てた負債 ＋ 肩代わりした負債 ＋ 決着時の残額** で閉じる
    // （第153・154期と同じ水準を要求する）。**HP ではなく負債の帳簿**であることに注意——
    // 取り立てが実際に削った HP（`TollTaken`）は上限・破片・HP1 のクランプで名目を下回る。
    // =====================================================================================

    /// <summary>前借りの発火回数。</summary>
    public long IndulgenceFires;
    /// <summary>前借りが <c>ctx.Heal</c> に要求した名目量。</summary>
    public long IndulgenceAsked;
    /// <summary><b>実際に増えた HP</b>＝積まれた負債の総量。</summary>
    public long DebtStacked;
    /// <summary>前借りを撃ったが1点も入らなかった回数。</summary>
    public long IndulgenceDry;
    /// <summary>そのうち渇きで止まった回数。</summary>
    public long IndulgenceDryDrought;
    /// <summary>貸す相手がいなかった回数（傷ついた味方が1体もいない・契約の上限）。</summary>
    public long IndulgenceNoPatient;
    /// <summary>
    /// <b>そのうち「契約枠が埋まっていて貸せなかった」回数</b>（第155期 追補・計数専用）。
    /// <see cref="IndulgenceRule.Contracts"/> が 0（無制限）なら構造的に 0 になる。
    /// </summary>
    public long IndulgenceBlocked;
    /// <summary>同・そのとき engine の <c>IdleTurn</c>（号令・据えが買い取る札）が立っていた回数。</summary>
    public long IndulgenceBlockedIdle;

    /// <summary>取り立ての発火回数。</summary>
    public long TollFires;
    /// <summary>取り立てた負債の名目量（<b>収支の分子</b>）。</summary>
    public long TollNominal;
    /// <summary><b>実際に削った HP</b>（＝焼きの燃料）。</summary>
    public long TollTaken;
    /// <summary>HP1 のクランプで止まった回数。</summary>
    public long TollFloored;
    /// <summary><b>取り立てが直接殺した回数</b>（<c>lethal: false</c> が効いていれば常に 0）。</summary>
    public long TollKills;
    /// <summary>取り立てが軛に切られた量。</summary>
    public long TollYokeCut;

    /// <summary>踏み倒し（借り手が負債を抱えたまま倒れた）の回数。</summary>
    public long TollForgives;
    /// <summary>同・保持者が引き受けた負債の名目量。</summary>
    public long TollForgiven;
    /// <summary>同・<b>保持者の HP が実際に減った量</b>（肩代わりされた分はここに出ない）。</summary>
    public long TollForgivenSelfHp;

    /// <summary>焼きの発火回数。</summary>
    public long BrandFires;
    /// <summary>焼きが叩き込んだ名目量（全体の巻き込みを含む）。</summary>
    public long BrandSpent;
    /// <summary>同・<b>実際に削った HP</b>。</summary>
    public long BrandRemoved;
    /// <summary>同・軛に切られた量。</summary>
    public long BrandYokeCut;
    /// <summary>焼きが当たった体数（延べ）。</summary>
    public long BrandHits;
    /// <summary>焼きで倒した敵の数。</summary>
    public long BrandKills;
    /// <summary>燃料はあるのに狙える敵が1体もいなかった回数。</summary>
    public long BrandDry;
    /// <summary>決着時に燃え残った燃料。</summary>
    public long BrandResidual;

    /// <summary>決着時に残っていた負債（死者も含む）。</summary>
    public long DebtResidual;

    /// <summary>借り手ごとの内訳（<c>Def.Id</c> → 積んだ量・取り立てられた量）。</summary>
    public readonly Dictionary<string, (long Stacked, long Collected)> DebtOn = new();

    internal void NoteIndulgence(UnitState by, UnitState on, int asked, int gained)
    {
        IndulgenceFires++;
        IndulgenceAsked += asked;
        DebtStacked += gained;
        if (gained <= 0)
        {
            IndulgenceDry++;
            if (DroughtBinding) IndulgenceDryDrought++;
        }
        DebtOn.TryGetValue(on.Def.Id, out var a);
        DebtOn[on.Def.Id] = (a.Stacked + gained, a.Collected);
        TallyOf(by).IndulgenceStacked += gained;
    }

    internal void NoteIndulgenceDry(UnitState by, bool noPatient, bool blocked = false, bool idle = false)
    {
        IndulgenceFires++;
        IndulgenceDry++;
        if (noPatient) IndulgenceNoPatient++;
        if (blocked) { IndulgenceBlocked++; if (idle) IndulgenceBlockedIdle++; }
    }

    internal void NoteToll(UnitState by, UnitState on, int nominal, int taken, bool floored, long yokeCut, bool killed)
    {
        TollFires++;
        if (killed) TollKills++;
        TollNominal += nominal;
        TollTaken += taken;
        TollYokeCut += yokeCut;
        if (floored) TollFloored++;
        DebtOn.TryGetValue(on.Def.Id, out var a);
        DebtOn[on.Def.Id] = (a.Stacked, a.Collected + nominal);
        TallyOf(by).TollTaken += taken;
    }

    internal void NoteTollForgive(UnitState by, UnitState dead, int nominal, int selfHp)
    {
        TollForgives++;
        TollForgiven += nominal;
        TollForgivenSelfHp += selfHp;
    }

    internal void NoteBrandFire(UnitState by, int fuel)
    {
        BrandFires++;
        TallyOf(by).BrandFires++;
    }

    internal void NoteBrandDry(UnitState by) => BrandDry++;

    internal void NoteBrandHit(UnitState by, int amount, int removed, long yokeCut, bool killed)
    {
        BrandHits++;
        BrandSpent += amount;
        BrandRemoved += removed;
        BrandYokeCut += yokeCut;
        if (killed) BrandKills++;
        TallyOf(by).BrandDealt += removed;
    }

    /// <summary>決着時に残っていた負債と燃料を数える（<b>死者も含めた全駒を1度だけ</b>）。</summary>
    public void CloseIndulgenceLedger()
    {
        foreach (UnitState u in _units)
        {
            DebtResidual += u.RawCounter(StatusKeys.Debt);
            BrandResidual += u.RawCounter(BrandTrait.FuelKey);
        }
    }

    /// <summary>
    /// 決着時に撒かれずに残っていた灰を数える（第179期・<b>計数のみ</b>）。
    /// <b>収支（溜めた ＝ 撒いた ＋ 抱えて倒れた ＋ 残り）が閉じるか</b>を見るためだけにある。
    /// </summary>
    public void CloseAshLedger()
    {
        for (int i = 0; i < _ashHolders.Count; i++)
        {
            int left = _ashHolders[i].RawCounter(StatusKeys.Ash);
            if (left <= 0) continue;
            AshResidual += left;
            TallyOf(_ashHolders[i]).AshResidual += left;
        }
    }

    public readonly long[] DroughtHits = new long[2];
    public readonly long[] DroughtRequested = new long[2];
    public readonly long[] DroughtEffective = new long[2];

    /// <summary>渇きに止められた駒の帳簿（<c>Def.Id</c> → 回数・実効量）。</summary>
    public readonly Dictionary<string, (long Hits, long Amount)> DroughtOn = new();

    public readonly long[] HushBlockedSide = new long[2];
    public readonly long[] HushBlockedAnySide = new long[2];
    public readonly long[] HushAskedSide = new long[2];
    public readonly long[][] HushByRoute = MakeHushRoutes();

    static long[][] MakeHushRoutes()
    {
        var a = new long[OutOfTurnRoutes.Count][];
        for (int i = 0; i < a.Length; i++) a[i] = new long[2];
        return a;
    }

    /// <summary>渇きの保持者（<see cref="Add"/> が積む）。</summary>
    readonly List<UnitState> _droughtHolders = new();

    /// <summary>粛の保持者（<see cref="Add"/> が積む）。</summary>
    readonly List<UnitState> _hushHolders = new();

    /// <summary>逆位の保持者（<see cref="Add"/> が積む）。<b>第134期 P5 の実証用</b>（規則は第22期から）。</summary>
    readonly List<UnitState> _inversionHolders = new();

    /// <summary>荷（第154期・<see cref="TraitId.Burden"/>）の保持者。</summary>
    readonly List<UnitState> _burdenHolders = new();

    /// <summary>重り（第154期・<see cref="TraitId.Laden"/>）の保持者。</summary>
    readonly List<UnitState> _ladenHolders = new();

    /// <summary>
    /// 重りの規則がいま効きうるか（<b>規則の値だけで決まる定数</b>。保持者の生存は
    /// <see cref="LadenPenalty"/> が見る）。<c>CurrentAttack</c> は最も呼ばれる読みなので、
    /// <b>既定では bool 1つの読みで抜ける</b>ようにここに畳んである。
    /// </summary>
    internal bool LadenActive { get; private set; }

    /// <summary>
    /// その駒がいま抱えている預かりのぶんの攻撃力の下げ幅（<b>下限の適用前</b>）。
    /// <b>効くのは抱えている本人</b>で、保持者（ノチ）自身ではない。
    /// </summary>
    internal int LadenPenalty(UnitState u)
    {
        if (!LadenActive) return 0;
        int pool = u.RawCounter(StatusKeys.Ward);
        if (pool <= 0) return 0;
        for (int i = 0; i < _ladenHolders.Count; i++)
            if (_ladenHolders[i].IsAlive && _ladenHolders[i].TeamId == u.TeamId)
                return pool / Ward.LadenPer;
        return 0;
    }

    /// <summary>荷がいま効いているか（<paramref name="target"/> の陣営に生きた保持者がいるか）。</summary>
    bool BurdenBinding(UnitState target)
    {
        for (int i = 0; i < _burdenHolders.Count; i++)
            if (_burdenHolders[i].IsAlive && _burdenHolders[i].TeamId == target.TeamId) return true;
        return false;
    }

    /// <summary>保持者が全員倒れたターン（0 ＝ 最後まで生きていた／保持者がいない）。</summary>
    public readonly int[] RuleFallTurn = new int[BoardRuleLedger.RuleCount];

    static bool AnyAlive(List<UnitState> holders)
    {
        for (int i = 0; i < holders.Count; i++) if (holders[i].IsAlive) return true;
        return false;
    }

    /// <summary>渇きがいま効いているか。<b>判定は第22期から1ビットも変えていない</b>（走査を配列に寄せただけ）。</summary>
    public bool DroughtBinding => AnyAlive(_droughtHolders);

    /// <summary>粛の保持者が生きているか（<c>Hush.Active</c> は呼び出し側で見る）。</summary>
    public bool HushHolderAlive => AnyAlive(_hushHolders);

    /// <summary>
    /// 保持者が落ちたターンを控える（第134期 段2）。<b>ターン頭に1度だけ</b>呼ぶ。
    /// <b>盤面には触らない。</b> 「保持者を割れば解除できる」設計（第110期の粛・第118期の渇き）が
    /// 実際に何ターン目に解除されているかを出すためだけにある。
    /// </summary>
    void NoteRuleHolders()
    {
        Fall((int)BoardRuleLedger.RuleIndex.Yoke, _yokeHolders);
        Fall((int)BoardRuleLedger.RuleIndex.Drought, _droughtHolders);
        Fall((int)BoardRuleLedger.RuleIndex.Hush, _hushHolders);
        Fall((int)BoardRuleLedger.RuleIndex.Inversion, _inversionHolders);

        void Fall(int ix, List<UnitState> holders)
        {
            if (holders.Count == 0 || RuleFallTurn[ix] != 0 || AnyAlive(holders)) return;
            RuleFallTurn[ix] = _turn;
        }
    }

    /// <summary>
    /// 決着後に1度だけ呼ぶ（第134期 段2）。<b>決着したターンに保持者が落ちた場合を拾う</b>
    /// ——そのターンの頭はもう過ぎていて、次のターンの頭は来ない。
    /// </summary>
    public void CloseRuleHolders() => NoteRuleHolders();

    /// <summary>保持者の数（<see cref="BoardRuleLedger.RuleIndex"/> の順）。</summary>
    public int[] RuleHolderCount => new[]
    {
        _yokeHolders.Count, _droughtHolders.Count, _hushHolders.Count, _inversionHolders.Count
    };

    /// <summary>渇きが止めた回復の帳簿（<see cref="Heal"/> の入口から呼ぶ）。<b>盤面には触らない。</b></summary>
    void NoteDroughtBlocked(UnitState target, int amount)
    {
        int side = SideOf(target);
        // **実効量は上限で切ってから数える**——満タンの駒への回復は、渇きが無くても
        // 1点も入らない（`Heal` は `Hp == before` で抜ける）。要求量だけを数えると
        // 「封じられた量」を上振れさせる。両方を出して報告書で並べる。
        int effective = Math.Max(0, Math.Min(amount, target.MaxHp - target.Hp));
        DroughtHits[side]++;
        DroughtRequested[side] += amount;
        DroughtEffective[side] += effective;
        DroughtOn.TryGetValue(target.Def.Id, out var acc);
        DroughtOn[target.Def.Id] = (acc.Hits + 1, acc.Amount + effective);
        // 第171期・**表示専用**。量は `effective`（上限で切ったあと）を出す
        // ——満タンの駒への回復は渇きが無くても入らないので、要求量だと画面が上振れする。
        EmitSealed(target, SealedLabels.Drought, effective);
    }

    /// <summary>
    /// 粛が止めたターン外の行動の帳簿（<see cref="CanActOutOfTurn"/> から呼ぶ）。<b>盤面には触らない。</b>
    /// <paramref name="sole"/> が真なら<b>粛が単独の原因</b>（痺れ・<c>CanReact</c> では落ちていない）。
    /// </summary>
    void NoteHushBlocked(UnitState u, OutOfTurnRoute route, bool sole)
    {
        int side = SideOf(u);
        HushBlockedAnySide[side]++;
        if (!sole) return;
        HushBlockedSide[side]++;
        HushByRoute[(int)route][side]++;
        // 第171期・**表示専用**。ここが「粛が単独の原因で止めた」の唯一の合流点なので、
        // 台本へ出すのもここ1行で足りる（計数には1ビットも触らない）。
        EmitSealed(u, SealedLabels.Hush, 0);
    }

    /// <summary>
    /// 上限がいま効いているか。<b>規則の有効・保持者の生存を1箇所に集めただけ</b>で、
    /// 判定は第25期から1ビットも変わっていない（<c>AllUnits.Any(...)</c> と同値）。
    /// </summary>
    public bool YokeBinding
    {
        get
        {
            if (!Yoke.Active) return false;
            for (int i = 0; i < _yokeHolders.Count; i++) if (_yokeHolders[i].IsAlive) return true;
            return false;
        }
    }

    // =====================================================================================
    // 第179期 —— 灰（TraitId.Ash）。**溜める側だけが engine にある。**
    // 撒く側・降らす側は `AshTrait` の中で、engine は規則を1本も持たない。
    // =====================================================================================

    /// <summary>灰の保持者（<see cref="Add"/> が積む）。<b>陣営ごとに引く</b>ので陣営で分けない。</summary>
    readonly List<UnitState> _ashHolders = new();

    /// <summary>
    /// いま灰を溜められる駒が盤上にいるか。<b>いなければ <see cref="ApplyDamage"/> は
    /// 比較1つで抜ける</b>（軛の <c>Cap</c> 判定・粛の保持者走査と同じ短絡の作法）。
    /// </summary>
    bool AshBinding => _ashHolders.Count > 0;

    /// <summary>撒いた回数 ／ 撒いた灰の総量 ／ 着弾した体数 ／ 灰が無くて素振りした回数 ／ 降った総量。</summary>
    public long AshFires, AshSpent, AshHits, AshDry, AshFallout;
    /// <summary>溜まった灰の総量（<b>実際に削られた量</b>）と、倒れた時点で抱えていた灰の総量。</summary>
    public long AshGained, AshAtDeath;
    /// <summary>決着時に撒かれずに残っていた灰。</summary>
    public long AshResidual;

    /// <summary>
    /// 味方が味方から受けたダメージを灰として溜める（第179期）。
    /// <b><see cref="ApplyDamage"/> が HP を引いた直後の1箇所からだけ呼ぶ。</b>
    ///
    /// <para><b>入力は「実際に削られた量」。</b> 惨禍・据え・散開・萎縮・肩代わり・破片・
    /// 受け流し・上限をすべて通った後の値なので、<b>帳簿の <c>DamageTaken</c> と定義上ずれない</b>
    /// （第115期「同じ比を作る2つの計数は、同じ瞬間に取ること」）。</para>
    ///
    /// <para><b>肩代わりの中継（<c>relayed</c>）も数える。</b> 中継は元の削りの一部であって
    /// 別の出来事ではない——ただし<b>元の段も中継の段も別々に HP を削っている</b>ので、
    /// 二重計上ではなく実額の合計になる。</para>
    ///
    /// <para><b>倒れた駒の分も数える</b>（この呼び出しは死亡判定より手前）。
    /// 「最後の一撃だけ灰にならない」という説明のつかない穴を作らないため。</para>
    /// </summary>
    /// <param name="target">削られた駒。<b>灰が溜まるのはこの駒と同じ陣営の保持者だけ。</b></param>
    /// <param name="amount">実際に削られた量。</param>
    /// <param name="source">出どころ。<c>null</c> は継続ダメージ（燃焼・毒の刻み）。</param>
    /// <param name="havocExtra">惨禍が上乗せした量（<see cref="AshRule.CountHavoc"/> が偽なら引く）。</param>
    void NoteAsh(UnitState target, int amount, UnitState? source, int havocExtra)
    {
        if (amount <= 0 || !AshBinding) return;

        // 敵から受けたダメージは灰にならない。**これがマイナスの半分**（指示書 §1）。
        if (source is null) { if (!Ash.CountDot) return; }
        else if (source.TeamId != target.TeamId) return;

        int gained = amount;
        if (!Ash.CountHavoc && havocExtra > 0) gained -= Math.Min(havocExtra, gained);
        if (gained <= 0) return;

        for (int i = 0; i < _ashHolders.Count; i++)
        {
            UnitState h = _ashHolders[i];
            if (!h.IsAlive || h.TeamId != target.TeamId) continue;
            h.SetCounter(StatusKeys.Ash, h.RawCounter(StatusKeys.Ash) + gained);
            AshGained += gained;
            TallyOf(h).AshGained += gained;
        }
    }

    /// <summary>灰を撒いた（<b>計数のみ</b>）。</summary>
    public void NoteAshThrow(UnitState self, int ash, int dealt)
    {
        AshFires++; AshSpent += ash;
        UnitTally t = TallyOf(self);
        t.AshFires++; t.AshSpent += ash;
        if (ash > t.AshPeak) t.AshPeak = ash;
    }

    /// <summary>灰が無くて素振りした（<b>計数のみ</b>）。</summary>
    public void NoteAshDry(UnitState self) { AshDry++; TallyOf(self).AshDry++; }

    /// <summary>灰が1体に着弾した（<b>計数のみ</b>。名目量）。</summary>
    public void NoteAshHit(int dmg) { AshHits++; }

    /// <summary>灰が隣へ降った（<b>計数のみ</b>。名目量）。</summary>
    public void NoteAshFallout(int amount) { AshFallout += amount; }

    /// <summary>倒れた時点で抱えていた灰（<b>計数のみ</b>）。</summary>
    public void NoteAshAtDeath(UnitState self, int ash)
    {
        if (ash <= 0) return;
        AshAtDeath += ash; TallyOf(self).AshAtDeath += ash;
    }

    /// <summary><see cref="ApplyDamage"/> を通らずに HP を減らした量を記録する（計数のみ）。</summary>
    public void NoteDirectHpLoss(int amount) { if (amount > 0) DirectHpLoss += amount; }

    /// <summary>帳簿の添字（型 ＋ 受け手の陣営）。</summary>
    int YokeSlot(AttackPattern? p, UnitState target)
        => (p is null ? 4 : (int)p) + (target.TeamId == PlayerTeam ? 5 : 0);

    /// <summary>読み手ごと（添字は <see cref="WoundReader"/>）の 発火／読んだ傷／名目／実効。</summary>
    public readonly int[] ReadFires = new int[6];
    /// <inheritdoc cref="ReadFires"/>
    public readonly long[] ReadWounds = new long[6];
    /// <inheritdoc cref="ReadFires"/>
    public readonly long[] ReadNominal = new long[6];
    /// <inheritdoc cref="ReadFires"/>
    public readonly long[] ReadEffective = new long[6];

    /// <summary>
    /// 介入の材料（指示書 §2-5・添字は <see cref="GuardKind"/>）。
    /// 発火／守った相手が傷を持っていた回数／その瞬間の傷持ちの味方の数の延べ／傷持ちの味方が1体でもいた回数。
    /// <b>味方陣の介入だけを数える</b>（案 A は味方側の配置の話）。
    /// </summary>
    public readonly int[] GuardFires = new int[5];
    /// <inheritdoc cref="GuardFires"/>
    public readonly int[] GuardTargetWounded = new int[5];
    /// <inheritdoc cref="GuardFires"/>
    public readonly long[] GuardWoundedAllySum = new long[5];
    /// <inheritdoc cref="GuardFires"/>
    public readonly int[] GuardAnyWoundedAlly = new int[5];

    /// <summary>傷が最初に書かれたターンを覚える私有キー（<b>計数専用</b>。<see cref="StatusKeys"/> ではない）。</summary>
    public const string WoundSinceKey = "woundSince";

    /// <summary>
    /// 在庫の走査（ターン頭）。<b>盤面は読むだけ。</b>
    /// <see cref="NoteBossCensus"/> と同じ場所・同じ guard に置いてある。
    /// </summary>
    public void NoteWoundCensus()
    {
        if (!WoundCensus) return;
        WoundStockTurns++;
        int a = 0, f = 0;
        foreach (UnitState u in _units)
        {
            if (!u.IsAlive) continue;
            int w = WoundDepthOf(u);
            if (w <= 0) continue;
            bool ally = u.TeamId == PlayerTeam;
            if (ally) a++; else f++;
            long[] hist = ally ? WoundDepthAlly : WoundDepthFoe;
            hist[w >= 3 ? 2 : w - 1]++;
        }
        WoundStockAllySum += a; WoundStockFoeSum += f;
        if (a > WoundStockAllyMax) WoundStockAllyMax = a;
        if (f > WoundStockFoeMax) WoundStockFoeMax = f;
        if (a > 0) WoundTurnsAllyAny++;
        if (f > 0) WoundTurnsFoeAny++;
    }

    // =====================================================================================
    // 第138期 段2 —— 破片（StatusKeys.Armor）の在庫（ArmorLedger）。
    //
    // **盤面には一切影響しない。** 既定（`ShrapnelRule.Default.ArmorCensus` ＝ 偽）では
    // `ArmorCensus` が偽なので走査も加算も1回も走らない（`compare` 305 セル 0 件が検算）。
    //
    // **`RawCounter` で読む**——`Counter` は `Probe` が刺さっているとき読みを記録するので、
    // 診断（第94期 (T2)）と混ざる。数えたいのは在庫であって「誰が読んだか」ではない。
    // =====================================================================================

    /// <summary>
    /// 破片の在庫を走査するか。<b>規則が有効なときだけ。</b>
    /// 札が <see cref="ShrapnelRule"/> に同居しているのは <c>Run</c> の引数を増やせないから
    /// （理由は <see cref="ShrapnelRule.ArmorCensus"/> の doc）。
    /// </summary>
    public bool ArmorCensus => Shrapnel.ArmorCensus;

    /// <summary>在庫の帳簿（添字は陣営。<c>0 = 敵</c> / <c>1 = 味方</c>）。</summary>
    public long ArmorTurns;
    /// <inheritdoc cref="ArmorTurns"/>
    public readonly long[] ArmorStockSum = new long[2], ArmorStockMax = new long[2];
    /// <inheritdoc cref="ArmorTurns"/>
    public readonly long[] ArmorTopSum = new long[2], ArmorTopMax = new long[2];
    /// <inheritdoc cref="ArmorTurns"/>
    public readonly long[] ArmorTurnsAny = new long[2], ArmorHolders = new long[2];
    /// <summary>味方側の最大保持者の <c>Def.Id</c> → (回数, 在庫の総和)。</summary>
    public readonly Dictionary<string, (long Times, long Sum)> ArmorTopBy = new();

    /// <summary>
    /// 在庫の走査（ターン頭）。<b>盤面は読むだけ。</b>
    /// <see cref="NoteWoundCensus"/> と同じ場所・同じ guard に置いてある。
    ///
    /// <para><b>同値のときは走査順（スロット昇順）で先に来た1体を「最大保持者」にする。</b>
    /// 礫の選択（<c>PickOne</c> で割る）とは一致しないが、<b>ここで測りたいのは
    /// 「1回に砕ける量」＝最大値</b>であって誰が砕かれるかではない。
    /// 名前の表（<c>ArmorTopBy</c>）は同値のぶんだけ先着に偏る——<b>それを承知で読むこと。</b></para>
    /// </summary>
    public void NoteArmorCensus()
    {
        if (!ArmorCensus) return;
        ArmorTurns++;

        Span<long> stock = stackalloc long[2];
        Span<long> top = stackalloc long[2];
        UnitState? topAlly = null;

        foreach (UnitState u in _units)
        {
            if (!u.IsAlive) continue;
            int v = u.RawCounter(StatusKeys.Armor);
            if (v <= 0) continue;
            int t = u.TeamId == PlayerTeam ? 1 : 0;
            stock[t] += v;
            ArmorHolders[t]++;
            if (v > top[t])
            {
                top[t] = v;
                if (t == 1) topAlly = u;
            }
        }

        for (int t = 0; t < 2; t++)
        {
            ArmorStockSum[t] += stock[t];
            if (stock[t] > ArmorStockMax[t]) ArmorStockMax[t] = stock[t];
            ArmorTopSum[t] += top[t];
            if (top[t] > ArmorTopMax[t]) ArmorTopMax[t] = top[t];
            if (stock[t] > 0) ArmorTurnsAny[t]++;
        }

        if (topAlly is not null)
        {
            (long times, long sum) = ArmorTopBy.TryGetValue(topAlly.Def.Id, out var prev) ? prev : (0, 0);
            ArmorTopBy[topAlly.Def.Id] = (times + 1, sum + top[1]);
        }
    }

    /// <summary>
    /// 消滅の帳簿に1件足す（第120期）。<b>盤面には一切影響しない。</b>
    /// <b>減算の窓口は無い</b>ので、呼び出し側（減算する側）に置いてある。
    /// </summary>
    public void NoteWoundLoss(UnitState from, int amount, WoundLoss why)
    {
        if (amount <= 0) return;
        WoundLossAll[(int)why] += amount;
        if (from.TeamId == PlayerTeam) WoundLossAlly[(int)why] += amount;
        if (WoundCensus && why != WoundLoss.Death && why != WoundLoss.End && why != WoundLoss.Carry)
            from.SetCounter(WoundSinceKey, 0);
    }

    /// <summary>
    /// 読み手が傷を読んだことの計数（第120期）。<b>盤面には一切影響しない。</b>
    /// <paramref name="effective"/> は <see cref="HpRemoved"/> の差（回復側は癒した実額）。
    /// </summary>
    public void NoteWoundRead(UnitState target, WoundReader who, int wounds, long nominal, long effective)
    {
        ReadFires[(int)who]++;
        ReadWounds[(int)who] += wounds;
        ReadNominal[(int)who] += nominal;
        ReadEffective[(int)who] += effective;
        if (!WoundCensus) return;
        int since = target.RawCounter(WoundSinceKey);
        if (since > 0) { WoundLagSum += Math.Max(0, Turn - since); WoundLagCount++; }
    }

    /// <summary>
    /// 介入が主目標を差し替えた瞬間の材料（第120期・指示書 §2-5）。<b>盤面には一切影響しない。</b>
    /// </summary>
    public void NoteGuardPick(GuardKind kind, UnitState guard, UnitState target)
    {
        if (!WoundCensus || guard.TeamId != PlayerTeam) return;
        int i = (int)kind;
        GuardFires[i]++;
        if (IsWounded(target)) GuardTargetWounded[i]++;
        int n = 0;
        foreach (UnitState u in _units)
            if (u.IsAlive && u.TeamId == guard.TeamId && u != guard && IsWounded(u)) n++;
        GuardWoundedAllySum[i] += n;
        if (n > 0) GuardAnyWoundedAlly[i]++;
    }

    // =====================================================================================
    // 第135期 —— 害の帳簿（HarmRule）。**engine に規則は1本も無い。計数だけ。**
    //
    // 既定（`HarmRule.Default` ＝ 数えない）では `HarmCensus` が偽なので、
    // 配列は1本も確保されず、分類の分岐も1回も走らない（`compare` 305 セル 0 件が検算）。
    // =====================================================================================

    /// <summary>害の帳簿（第135期）。</summary>
    public HarmRule Harm { get; }

    /// <summary>帳簿を回すか。<b>規則が有効なときだけ。</b></summary>
    public bool HarmCensus => Harm.Census;

    /// <summary>受け流しの強度（第135期）。<b>既定は不活性</b>（<c>Uses = 0</c>）。</summary>
    public ParryRule Parry { get; }

    /// <summary>
    /// 砕けの供給の出どころ（第137期。既定は <see cref="ShatterRule.Default"/> ＝ 現行）。
    /// static のノブにしない理由は同型の doc を参照。
    /// </summary>
    public ShatterRule Shatter { get; }


    /// <summary>
    /// 礫の強度（第138期。既定は <see cref="ShrapnelRule.Default"/>）。
    /// <b>保持者が <see cref="UnitCatalog.All"/> に 0 枚なので、既定では盤面に1度も現れない。</b>
    /// </summary>
    public ShrapnelRule Shrapnel { get; }

    /// <summary>
    /// 灰の規則（第179期。既定は <see cref="AshRule.Default"/>）。
    /// <b>保持者（スス）が盤上にいなければ1ビットも動かない</b>——
    /// 溜める側は <see cref="AshBinding"/> で短絡し、撃つ側は特性の中にある。
    /// </summary>
    public AshRule Ash { get; }

    // =====================================================================================
    // 第138期 —— 礫（TraitId.Shrapnel）の計数。**盤面には一切影響しない。**
    // 撃った回数は `ShrapnelFires`、捨てた回数は `UnitTally.StallCanAct`（engine が既に数えている）。
    // **`ShrapnelDealt` は名目**（`shards * Multiplier + 攻撃力` × 体数）で、
    // 実際に通った量は `UnitTally.DamageToEnemy` にある——**差が軽減と軛の切り取り**である（予測 P6）。
    // =====================================================================================

    /// <summary>撃った回数 ／ 砕いた破片の総量 ／ 敵を砕いた回数（<b>この期は 0 が正</b>）。</summary>
    public int ShrapnelFires, ShrapnelShards, ShrapnelFoeTargets;
    /// <summary>敵全体へ撃った名目の総量 ／ 砕かれた駒へ返した総量 ／ 着弾した体数。</summary>
    public int ShrapnelDealt, ShrapnelSelfHarm, ShrapnelHits;

    /// <summary>1回の発火を数える（<b>計数のみ</b>）。</summary>
    public void NoteShrapnel(int shards, bool foeSide)
    {
        ShrapnelFires++;
        ShrapnelShards += shards;
        if (foeSide) ShrapnelFoeTargets++;
    }

    /// <summary>敵1体への着弾を数える（<b>計数のみ・名目</b>）。</summary>
    public void NoteShrapnelHit(int nominal) { ShrapnelHits++; ShrapnelDealt += nominal; }

    /// <summary>砕かれた駒への返りを数える（<b>計数のみ・名目</b>）。</summary>
    public void NoteShrapnelSelfHarm(int amount) => ShrapnelSelfHarm += amount;


    /// <summary>
    /// <b>直前の標的選択で介入が主目標を差し替えた相手</b>（第135期・<b>計数専用</b>）。
    /// <see cref="SelectTargetChain"/> の冒頭で毎回 null に戻し、
    /// <c>EmitIntercept</c> が立て、最初にその駒へ入ったダメージ1件で消費する。
    ///
    /// <para><b>盤面は1ビットも読まない・書かない。</b> 「庇って引き受けたぶん」と
    /// 「素で狙われたぶん」を分けるためだけにあり、<see cref="RedirectGainTrait.PendingKey"/>
    /// では足りない——あの印は庇う・殉教の2段にしか立たず、後備え・棘守り・標では立たない。</para>
    /// </summary>
    private UnitState? _interceptedInto;

    /// <summary>
    /// 被弾1件を経路別の帳簿へ入れる（第135期）。<b>盤面には一切影響しない。</b>
    /// <see cref="ApplyDamage"/> が HP を引いた直後の1箇所からだけ呼ぶ。
    /// </summary>
    void NoteHarm(UnitState target, int amount, bool fatal,
                  bool burnTick, bool levy, bool relayed, bool isFriendlyFire,
                  AttackPattern? pattern, UnitState? source)
    {
        if (!HarmCensus || amount <= 0) return;

        DamageRoute r;
        if (burnTick) r = DamageRoute.Burn;
        else if (levy) r = DamageRoute.Levy;
        else if (relayed) r = DamageRoute.Relay;
        else if (source is null) r = isFriendlyFire ? DamageRoute.Self : DamageRoute.Poison;
        else if (source == target) r = DamageRoute.Self;
        else if (pattern is AttackPattern p)
            r = p switch
            {
                AttackPattern.Sweep => DamageRoute.Sweep,
                AttackPattern.Pierce => DamageRoute.Pierce,
                AttackPattern.All => DamageRoute.All,
                _ => DamageRoute.Single,
            };
        else if (isFriendlyFire || source.TeamId == target.TeamId) r = DamageRoute.Friendly;
        else r = DamageRoute.Other;

        int i = (int)r;
        UnitTally t = TallyOf(target);
        int[] amt = t.HarmAmount ??= new int[DamageRoutes.Count];
        int[] cnt = t.HarmHits ??= new int[DamageRoutes.Count];
        amt[i] += amount;
        cnt[i]++;

        // 介入で引き受けた一撃か。**印は1件で消費する**（同じ差し替えを2度数えない）。
        if (_interceptedInto == target)
        {
            _interceptedInto = null;
            int[] gamt = t.HarmGuardAmount ??= new int[DamageRoutes.Count];
            int[] gcnt = t.HarmGuardHits ??= new int[DamageRoutes.Count];
            gamt[i] += amount;
            gcnt[i]++;
        }

        // 致命打。**総量の内訳とは別に持つ**——「たくさん殴られている」と
        // 「何で死んだか」は別の量である（第134期）。
        if (fatal)
        {
            int[] f = t.HarmFatal ??= new int[DamageRoutes.Count];
            f[i]++;
        }
    }

    /// <summary>
    /// 受け流しを1件数える（第135期）。<b>盤面には一切影響しない。</b>
    /// <b>経路は <see cref="NoteHarm"/> と同じ分類を使う</b>——ここへ来る時点で
    /// 刻み・徴収・中継・味方の刃はすべて除外済みなので、攻撃型だけを見れば足りる。
    /// </summary>
    void NoteParry(UnitState target, int amount, AttackPattern? pattern, UnitState source)
    {
        UnitTally t = TallyOf(target);
        t.ParryFires++;
        t.ParryBlocked += amount;
        if (amount > t.ParryBlockedMax) t.ParryBlockedMax = amount;
        int[] cnt = t.ParryByRoute ??= new int[DamageRoutes.Count];
        cnt[(int)(pattern switch
        {
            AttackPattern.Sweep => DamageRoute.Sweep,
            AttackPattern.Pierce => DamageRoute.Pierce,
            AttackPattern.All => DamageRoute.All,
            AttackPattern.Single => DamageRoute.Single,
            _ => DamageRoute.Other,
        })]++;
    }

    /// <summary>
    /// 決着時に残っていた傷を帳簿へ落とす（第120期）。<b>死者も数える</b>——傷は死んでも消えない。
    /// <b>途中で数えると蘇生で二重計上になる</b>ので、1戦につき最後に1度だけ呼ぶ。
    /// </summary>
    public void CloseWoundLedger()
    {
        foreach (UnitState u in _units)
        {
            int w = u.RawCounter(StatusKeys.Wound);
            // **深手は足さない**——束ねられた傷は `WoundLoss.Bundle` で既に落ちていて、
            // 束ねに使われた分は「書かれた」側にも載っていない（`SetCounter` を通らない）。
            if (w > 0) NoteWoundLoss(u, w, u.IsAlive ? WoundLoss.End : WoundLoss.Death);
        }
    }

    // =====================================================================================
    // 第118期 —— 糧タンク（NourishRule）。
    //
    // **engine に判定は1本も無い。** ここにあるのは (1) 規則の受け渡し、(2) 計数、
    // (3) ダメージ1回ぶんの「札」（<see cref="Hit"/>）だけで、機構の本体は
    // <see cref="RegenTrait"/> / <see cref="NourishTrait"/> の中にある（軋み・積み過ぎと同じ形）。
    //
    // **`Hit.Levy` だけが規則に読まれる。** 残りの3つ（`FriendlyFire` / `Relayed` / `Pattern`）は
    // 経路表（指示書 §3）を実測で1行ずつ検証するための計数専用で、**誰も読んで分岐しない。**
    // =====================================================================================

    /// <summary>糧の強度（第118期・<see cref="NourishRule"/>）。</summary>
    public NourishRule Nourish { get; }

    /// <summary>
    /// いま解決中のダメージ1回ぶんの札（第118期）。<see cref="ApplyDamage"/> が入口で立て、
    /// 出口で元に戻す（肩代わりの中継で入れ子になるので退避・復帰する。<c>Mark</c> と同じ作法）。
    ///
    /// <para><b><c>Levy</c> だけが盤面の規則に読まれる</b>——徴収（生贄・吸い・置き去りの削り）は
    /// 「攻撃によるダメージ」ではないので糧を渡さない。残りは計数専用。</para>
    /// </summary>
    public readonly record struct HitFrame(bool Levy, bool FriendlyFire, bool Relayed, AttackPattern? Pattern);

    /// <summary>いま解決中のダメージの札。<see cref="ApplyDamage"/> の外では既定値。</summary>
    public HitFrame Hit { get; private set; }

    /// <summary>いま解決中のダメージが徴収（コスト）か。<b>糧が読む唯一の札。</b></summary>
    public bool InLevy => Hit.Levy;

    /// <summary>糧の計数（第118期）。<b>誰も読んで分岐しない。</b></summary>
    public int NourishFires, NourishGiven, NourishToFoe, NourishToAlly;

    /// <summary>糧が発火しなかった内訳（順に 出どころなし・自傷・徴収・相打ち・破片で受け切り）。</summary>
    public int NourishNoSource, NourishSelf, NourishLevy, NourishDead, NourishSoaked;

    /// <summary>経路別の発火回数（<see cref="NourishPaths"/>）。<b>保持者がいなければ1本も確保しない。</b></summary>
    public int[]? NourishByPath;

    /// <summary>
    /// 発火した経路を1つ数える（第118期・<b>盤面には一切影響しない</b>）。
    /// 分類は <see cref="Hit"/> と陣営だけから引く——手で書いた分類は1件も無い。
    /// </summary>
    public void NoteNourishPath(UnitState source, UnitState target)
    {
        int[] by = NourishByPath ??= new int[NourishPaths.Count];
        int i;
        if (Hit.Relayed) i = 4;                                   // 中継の段（肩代わりの内側）
        else if (source.TeamId == target.TeamId || Hit.FriendlyFire) i = 3;   // 味方の刃
        else if (Hit.Pattern == AttackPattern.Single) i = 0;      // 敵の刃・単体
        else if (Hit.Pattern is not null) i = 1;                  // 敵の刃・範囲
        else i = 2;                                               // 型なし（反撃・破裂）
        by[i]++;
    }

    /// <summary>
    /// いま処理中の死亡通知の連鎖に入った時点の「味方の振りの総数」（指示書 Q3 の材料）。
    /// <b>観測専用で、誰も読んで分岐しない。</b> 入れ子（追い打ちが更に誰かを倒す）に備えて
    /// <c>HandleDeath</c> が退避・復帰する。
    /// </summary>
    private int _tlChainAtk;

    /// <summary>
    /// 譲渡の時点で「この死亡通知の連鎖の中で、既に味方の振りが走っていたか」。
    /// <b>追い打ち（ハギ）と譲渡が1つの撃破で両方立ったか</b>を数えるためだけにある。
    /// </summary>
    public bool TaillightChainSwung => _units.Sum(u => TallyOf(u).Attacks) > _tlChainAtk;

    /// <summary>死亡通知の連鎖に入る（<c>HandleDeath</c> が呼ぶ）。戻り値を <see cref="EndTlChain"/> へ返す。</summary>
    internal int BeginTlChain()
    {
        int prev = _tlChainAtk;
        _tlChainAtk = _units.Sum(u => TallyOf(u).Attacks);
        return prev;
    }

    /// <summary>死亡通知の連鎖から出る。</summary>
    internal void EndTlChain(int prev) => _tlChainAtk = prev;

    // =====================================================================================
    // 第104期 —— 再行動（EncoreRule）の計数。**盤面には一切影響しない。**
    //
    // 門（§2-2）の 1・2 は**版に依らず数える**（第86期の X1P・第90期の作法）——
    // 紙の分子を V0 の実測から取るため。3 以降は規則が有効なときだけ立つ。
    // =====================================================================================

    /// <summary>門 1 —— 傷を刻まれた駒が倒れた回数（味方側も含む全体）と、そのうち<b>敵</b>の駒。</summary>
    public int EncoreWoundedDeaths, EncoreWoundedFoeDeaths;

    /// <summary>
    /// 門 2 —— そのとき<b>生存していて敵陣にいた</b>刻み手の延べ数と、
    /// 1体以上いた死の件数。<b>0 なら再行動は起きない。</b>
    /// </summary>
    public int EncoreLiveWriters, EncoreDeathsWithLiveWriter;

    /// <summary>門 3 —— 再行動が走った回数（<see cref="TakeTurn"/> を呼んだ回数）。</summary>
    public int EncoreFired;

    /// <summary>Q4 —— 再行動が何をしたかの内訳（通常攻撃／術／溜め／潰れた）。</summary>
    public int EncoreAttack, EncoreSkill, EncoreCharge, EncoreStalled;

    /// <summary>自己検査 (d) —— ★ 1ホップで抑えた回数。</summary>
    public int EncoreBlockedHop;

    /// <summary>自己検査 (g) —— <b>敵側</b>で再行動が起きた回数。<b>0 でなければならない。</b></summary>
    public int EncoreOnEnemySide;

    /// <summary>
    /// 自己検査 (h) —— 再行動した駒のうち <c>Actions</c> を持っていた回数。
    /// <b>持っていれば <c>ActionIndex</c> が1つ進む</b>（現行のキリ・ノミは持たないので 0）。
    /// </summary>
    public int EncoreWithActions;

    /// <summary>死の連鎖の中で蘇ったので再行動させなかった回数。</summary>
    public int EncoreRevivedSkip;

    /// <summary>Q5 —— 餌（第103期）が刻まれて倒れた回数と、そこから走った再行動の回数。</summary>
    public int EncoreFodderDeaths, EncoreFromFodder;

    /// <summary>
    /// 刻み手を記録する（第104期）。<b>版に依らない</b>——規則が無効でも記録は取る。
    /// <para>挿入順・重複なし。<see cref="Wound"/> が<b>実際に傷を書いた</b>ときだけ呼ぶ。</para>
    /// </summary>
    private static void NoteWoundWriter(UnitState target, UnitState writer)
    {
        var list = target.WoundWriters ??= new List<UnitState>(2);
        if (!list.Contains(writer)) list.Add(writer);
    }

    /// <summary>
    /// 傷が減った箇所から呼ぶ（第104期）。<b>傷が 0 になったら刻んだ事実も消える。</b>
    ///
    /// <para>呼び出し口は<b>4つだけ</b>——断ち（<c>SeverTrait</c>・0 に戻す）／
    /// 縫い（<c>SutureTrait</c> の塞ぎ・1 引く）／継ぎ当て（<c>MenderTrait</c> の塞ぎ・1 引く）／
    /// 引き取り（<c>GatherTrait</c>）の <b>donor 側</b>（1 引く）。
    /// 加算はすべて <see cref="Wound"/> を通るのでここには来ない。</para>
    ///
    /// <para><b>束ねられた深手は消さない</b>——<c>WoundDepthOf</c> / <c>IsWounded</c> が
    /// 「傷を持っている」と読む側なので、記録もそちらに揃える。</para>
    /// </summary>
    public void NoteWoundDrop(UnitState u)
    {
        if (u.WoundWriters is null) return;
        if (u.RawCounter(StatusKeys.Wound) > 0) return;
        if (u.RawCounter(StatusKeys.Deep) > 0) return;
        u.WoundWriters = null;
    }

    /// <summary>
    /// 再行動（第104期）。<b>傷を刻まれた駒が倒れたら、刻み手が手番をもう一度得る。</b>
    ///
    /// <para><b>死亡通知の固定順（<c>OnKill</c> → <c>OnDeath</c> → <c>OnAnyDeath</c> →
    /// <c>OnAllyDeath</c>）が全部終わってから走らせる</b>——連鎖の途中に手番を差し込むと、
    /// 墓守・分裂・蘇生のあいだに不定な行動が挟まって固定順が壊れる。
    /// 全部終わった後なら、再行動が見るのは「死の連鎖が解決し切った盤面」になる。</para>
    ///
    /// <para><b>順序はスロット昇順</b>（記録の挿入順ではない。決定的にするため）。
    /// <b><c>ctx.PickOne</c> を使わない</b>——候補2個以上で <c>Roll</c> を消費して
    /// 乱数列が動く（第89期 (h)）。</para>
    ///
    /// <para><b>傷は消費しない。</b> 倒れた駒の傷はどのみち消える。</para>
    /// </summary>
    private void NoteEncore(UnitState dead)
    {
        List<UnitState>? writers = dead.WoundWriters;
        if (writers is null || writers.Count == 0) return;

        // 門 1・2 は**版に依らない**（規則の分岐より手前）。
        EncoreWoundedDeaths++;
        if (dead.TeamId == EnemyTeam) EncoreWoundedFoeDeaths++;
        bool fodder = BetrayedTrait.IsFodder(dead);
        if (fodder) EncoreFodderDeaths++;

        List<UnitState> live = writers
            .Where(w => w.IsAlive && w.TeamId != dead.TeamId)
            .OrderBy(w => w.Slot)
            .ToList();
        EncoreLiveWriters += live.Count;
        if (live.Count > 0) EncoreDeathsWithLiveWriter++;

        if (!Encore.Enabled || live.Count == 0) return;
        if (dead.IsAlive) { EncoreRevivedSkip++; return; }   // 連鎖の中で蘇っていたら動かさない
        if (Encoring) { EncoreBlockedHop += live.Count; return; }   // ★ 1ホップ

        Encoring = true;
        try
        {
            foreach (UnitState w in live)
            {
                if (!w.IsAlive) continue;                          // 連鎖の途中で落ちうる
                if (!TeamAlive(Opponent(w.TeamId))) break;         // 行動順ループと同じ番人
                EncoreFired++;
                if (w.TeamId != PlayerTeam) EncoreOnEnemySide++;   // 自己検査 (g)
                if (w.Def.Actions is { Count: > 0 }) EncoreWithActions++;   // 自己検査 (h)
                if (fodder) EncoreFromFodder++;                    // Q5
                TallyOf(w).EncoreFires++;
                Log($"    {w.Name} は刻んだ獲物が倒れるのを見て、もう一度踏み込む", LogKind.Highlight, w);
                switch (TakeTurn(w))
                {
                    case TurnOutcome.Attack: EncoreAttack++; break;
                    case TurnOutcome.Skill:  EncoreSkill++;  break;
                    case TurnOutcome.Charge: EncoreCharge++; break;
                    default:
                        EncoreStalled++;
                        TallyOf(w).EncoreStalls++;
                        break;
                }
            }
        }
        finally { Encoring = false; }
    }

    /// <summary>
    /// 軋み（第66期）の在庫の記録。<b>盤面には一切影響しない。</b>
    /// <see cref="TraitId.Displaced"/> 保持者の <see cref="UnitState.AtkBonus"/> が動いた直後に呼ぶ
    /// ——上げる経路は<b>軋み自身と <see cref="Whet"/> の2本だけ</b>（ヨミは自己強化を1つも持たない）。
    /// <paramref name="selfGain"/> が真なら軋み由来、偽なら窓口経由。
    /// </summary>
    public void NoteCreakBonus(UnitState self, int amount, bool selfGain, bool regurgitate = false)
    {
        if (!self.HasTrait(TraitId.Displaced)) return;
        UnitTally t = TallyOf(self);
        if (selfGain) t.CreakSelfGain += amount;
        else
        {
            t.CreakWhetGain += amount;
            if (regurgitate) t.CreakRegurgGain += amount;
        }
        if (self.AtkBonus > t.CreakMaxBonus) t.CreakMaxBonus = self.AtkBonus;
        if (self.WhetReceived > t.CreakWhetMax) t.CreakWhetMax = self.WhetReceived;

        // 第67期の条件の側（WhetReceived）の到達ターン。**規則を無効にしていても数える。**
        int[] wp = t.CreakWhetProbeTurn ??= new int[UnitTally.CreakWhetProbes.Length];
        for (int i = 0; i < UnitTally.CreakWhetProbes.Length; i++)
        {
            if (wp[i] != 0 || self.WhetReceived < UnitTally.CreakWhetProbes[i]) continue;
            wp[i] = Math.Max(1, Turn);
        }

        // 第77期。供給元の選択子の `Both`（AtkBonus + WhetReceived）側の初到達ターン。
        // **格子は CreakProbes と同じ 9 / 18 / 30**（`Both` の版はその3点で振る）。
        // **既存の2本には触っていない。誰も読んで分岐しない。**
        int[] bp = t.CreakBothProbeTurn ??= new int[UnitTally.CreakProbes.Length];
        int both = self.AtkBonus + self.WhetReceived;
        for (int i = 0; i < UnitTally.CreakProbes.Length; i++)
        {
            if (bp[i] != 0 || both < UnitTally.CreakProbes[i]) continue;
            bp[i] = Math.Max(1, Turn);
        }

        int[] probe = t.CreakProbeTurn ??= new int[UnitTally.CreakProbes.Length];
        int[] ps = t.CreakSelfAtProbe ??= new int[UnitTally.CreakProbes.Length];
        int[] pw = t.CreakWhetAtProbe ??= new int[UnitTally.CreakProbes.Length];
        int[] pr = t.CreakRegurgAtProbe ??= new int[UnitTally.CreakProbes.Length];
        for (int i = 0; i < UnitTally.CreakProbes.Length; i++)
        {
            if (probe[i] != 0 || self.AtkBonus < UnitTally.CreakProbes[i]) continue;
            probe[i] = Math.Max(1, Turn);   // 開戦時の到達は 1 に丸める（0 を「未到達」に使うため）
            ps[i] = t.CreakSelfGain;
            pw[i] = t.CreakWhetGain;
            pr[i] = t.CreakRegurgGain;
        }
    }

    public BattleContext(int seed, bool verbose, ColossusRule? colossus = null, YokeRule? yoke = null,
                         HushRule? hush = null, MartyrRule? martyr = null, ExposeRule? expose = null,
                         ShoveRule? shove = null, BearRule? bear = null,
                         RelayRule? relay = null, SlanderRule? slander = null,
                         OverbearRule? overbear = null, ScaleRule? scale = null,
                         ScapegoatRule? scapegoat = null, DivertRule? divert = null,
                         GoadRule? goad = null, FinisherRule? finisher = null,
                         FavorRule? favor = null, BlazeRule? blaze = null,
                         FunnelRule? funnel = null, WhetMask? whetMask = null,
                         CreakRule? creak = null, SeverRule? sever = null,
                         ThinBladeRule? thinBlade = null, ThornRule? thorn = null,
                         SutureRule? suture = null, SutureFireRule? sutureFire = null,
                         SpillWoundRule? spillWound = null,
                         MendRule? mend = null, IgniteRule? woundIgnite = null,
                         GatherRule? gather = null, SoakRule? soak = null,
                         DeepRule? deep = null, CurseRule? curse = null,
                         BetrayRule? betray = null, EncoreRule? encore = null,
                         RageRule? rage = null, MenderCostRule? menderCost = null,
                         LooseRule? loose = null, TaillightRule? taillight = null,
                         ReaderRule? reader = null, BossRule? boss = null,
                         NourishRule? nourish = null, WoundRule? wound = null,
                         EmberRule? ember = null, WildfireRule? wildfire = null,
                         HarmRule? harm = null, ParryRule? parry = null,
                         ShatterRule? shatter = null, ShrapnelRule? shrapnel = null,
                         BraceRule? brace = null, ShufflerRule? shuffler = null,
                         ConfusionRule? confusion = null, HasteRule? haste = null,
                         WardRule? ward = null, IndulgenceRule? indulgence = null,
                                   AshRule? ash = null,
                         CounterProbe? probe = null)
    {
        _rng = new Random(seed);
        Probe = probe;          // 第94期 (T2)。**既定 null。診断だけが渡す。**
        _verbose = verbose;
        Colossus = colossus ?? ColossusRule.Default;
        Yoke = yoke ?? YokeRule.Default;
        Hush = hush ?? HushRule.Default;
        Martyr = martyr ?? MartyrRule.Default;
        Expose = expose ?? ExposeRule.Default;
        Shove = shove ?? ShoveRule.Default;
        Bear = bear ?? BearRule.Default;
        Relay = relay ?? RelayRule.Default;
        Slander = slander ?? SlanderRule.Default;
        Overbear = overbear ?? OverbearRule.Default;
        Scale = scale ?? ScaleRule.Default;
        Scapegoat = scapegoat ?? ScapegoatRule.Default;
        if (Scapegoat.Audit) ScapegoatActive = true;
        Divert = divert ?? DivertRule.Default;
        if (Divert.Audit) DivertActive = true;
        Goad = goad ?? GoadRule.Default;
        Finisher = finisher ?? FinisherRule.Default;
        Favor = favor ?? FavorRule.Default;
        Blaze = blaze ?? BlazeRule.Default;
        Ember = ember ?? EmberRule.Default;
        Wildfire = wildfire ?? WildfireRule.Default;
        Funnel = funnel ?? FunnelRule.Default;
        WhetBlock = whetMask ?? WhetMask.None;
        Creak = creak ?? CreakRule.Default;
        Sever = sever ?? SeverRule.Default;
        ThinBlade = thinBlade ?? ThinBladeRule.Default;
        Thorn = thorn ?? ThornRule.Default;
        Suture = suture ?? SutureRule.Default;
        SutureFire = sutureFire ?? SutureFireRule.Default;
        SpillWound = spillWound ?? SpillWoundRule.Default;
        Mend = mend ?? MendRule.Default;
        WoundIgnite = woundIgnite ?? IgniteRule.Default;
        Gather = gather ?? GatherRule.Default;
        Soak = soak ?? SoakRule.Default;
        Deep = deep ?? DeepRule.Default;
        Curse = curse ?? CurseRule.Default;
        Betray = betray ?? BetrayRule.Default;
        Encore = encore ?? EncoreRule.Default;
        Rage = rage ?? RageRule.Default;
        MenderCost = menderCost ?? MenderCostRule.Default;
        Loose = loose ?? LooseRule.Default;
        Brace = brace ?? BraceRule.Default;
        Ward = ward ?? WardRule.Default;
        Indulgence = indulgence ?? IndulgenceRule.Default;
        Taillight = taillight ?? TaillightRule.Default;
        Reader = reader ?? ReaderRule.Default;
        Boss = boss ?? BossRule.Default;
        Nourish = nourish ?? NourishRule.Default;
        Wounds = wound ?? WoundRule.Default;
        Harm = harm ?? HarmRule.Default;
        Parry = parry ?? ParryRule.Default;
        Shatter = shatter ?? ShatterRule.Default;
        Shrapnel = shrapnel ?? ShrapnelRule.Default;
        Ash = ash ?? AshRule.Default;
        Shuffler = shuffler ?? ShufflerRule.Default;
        Confusion = confusion ?? ConfusionRule.Default;
        Haste = haste ?? HasteRule.Default;
        // **枝を足したらここも足す。** 読む側（`FoesOf` / `ConsumeConfusion`）はこの短絡の内側に
        // あるので、**新しい供給の口を `ShuffleStagger` に足してこの行を忘れると、
        // 混乱は立つのに誰も読まない**（第148期に実際に踏んだ。実測は「敵に立った 2.3〜3.4 /
        // 敵が振った 0.00」——立った数だけが帳簿に残り、盤面では何も起きない）。
        ConfusionLive = Confusion.Active || Shuffler.Confuses();
        // 第154期。**既定（`Forfeit`）では `CurrentAttack` が bool 1つを読んで抜ける。**
        LadenActive = Ward.Cost == WardCost.Laden && Ward.LadenPer > 0;
    }

    // =====================================================================================
    // 第103期 —— 背かれ（BetrayRule）の計数。**盤面には一切影響しない。**
    //
    // 既定（BetrayRule.Default ＝ 喚ばない）では `BetrayWatch` が偽なので、
    // 走査も加算も1回も走らない（`compare` 305 セルが 0 件であることが検算）。
    // =====================================================================================

    /// <summary>背かれの計数を回すか。<b>規則が有効なときだけ。</b></summary>
    public bool BetrayWatch => Betray.Enabled;

    /// <summary>門の 1 —— 喚んだ回数 ／ 実際に湧いた回数 ／ 席が埋まっていて湧かなかった回数。</summary>
    public int BetrayTries, BetraySummoned, BetrayBlocked;

    /// <summary>自己検査 (c)(d)(e) —— 味方陣に湧いた回数 ／ ○前2 以外に湧いた回数 ／ 同時に生きていた最大数。</summary>
    public int BetrayAllySide, BetrayWrongSlot, BetrayMaxAlive;

    /// <summary>自己検査 (f) —— 餌の空き手番が「差し出された本物の空き」と判定された回数（0 のはず）。</summary>
    public int BetrayIdleSellable;

    /// <summary>自己検査 (g) —— 餌が蘇生された回数（0 のはず）。</summary>
    public int BetrayRevived;

    /// <summary>門の 2 —— 餌が倒された回数。</summary>
    public int BetrayKilled;

    /// <summary>
    /// 門の 3 —— <b>餌の撃破で読み手が発火した量</b>。
    /// <para><c>Attack</c> は餌の死の連鎖の中で走った <c>PerformAttack</c> の回数（＝ハギの追い打ち）、
    /// <c>Poison</c> はその連鎖の中で盤面に増えた毒の層（＝ラウの拡散）、
    /// <c>Overreach</c> は撃破者が深追いで痺れた回数（＝エグの手番喪失）。</para>
    /// <para><b>連鎖の入れ子はそのまま外側に積む</b>——ハギの追い打ちが更に誰かを倒せば、
    /// その分も「餌の死が引き起こしたもの」として数える。</para>
    /// </summary>
    public int BetrayFireAttack, BetrayFirePoison, BetrayFireOverreach;

    /// <summary>
    /// 代金（§2-3）—— <b>主目標が餌だった振りの回数</b>と、そのときの打点の総和。
    /// <c>本物の敵に当たらなかった手番の数 × その手番の平均打点</c> の材料。
    /// </summary>
    public int BetrayHits, BetrayHitAtkSum;

    /// <summary>喚び出しの1件を記録する（<paramref name="f"/> が null なら席が埋まっていた）。</summary>
    public void NoteBetraySummon(UnitState self, UnitState? f)
    {
        BetrayTries++;
        if (f is null) { BetrayBlocked++; return; }
        BetraySummoned++;
        if (f.TeamId == self.TeamId) BetrayAllySide++;                       // (c)
        if (f.Slot != BetrayedTrait.FodderSlot) BetrayWrongSlot++;           // (d)
        int alive = _units.Count(u => u.IsAlive && BetrayedTrait.IsFodder(u));
        if (alive > BetrayMaxAlive) BetrayMaxAlive = alive;                  // (e)
        // (f) 餌の空き手番が号令・据えに売れないこと。**Immobile の SurrendersTurn が偽**なので
        // `Trait.SurrenderedTurn` は必ず偽になる——engine が立てる `IdleTurn` そのものは立つので、
        // 見るのは生の counter ではなく<b>買い手が通す判定のほう</b>である。
        if (Trait.SurrenderedTurn(this, f)) BetrayIdleSellable++;
    }

    /// <summary>餌に振られた1件を記録する（代金の材料）。</summary>
    public void NoteBetrayHit(UnitState actor, UnitState target)
    {
        BetrayHits++;
        BetrayHitAtkSum += actor.CurrentAttack;
        TallyOf(actor).BetrayFodderHits++;
    }

    // =====================================================================================
    // 第94期 (T2) —— 観測の印。**分岐に一切使わない。**
    //
    // `TraitEntryMap` / `TraitKeyMap`（BattleSim 側の手で作った表）は「特性 X がキー K を
    // 読む／供給する」と書いてあるが、**ソースの静的解析では取れない**
    // （`target.Counter(StatusKeys.Wound)` がどの特性の中で呼ばれたかは実行しないと分からない）。
    // だから走らせて観測する。engine が特性のフックを呼ぶ直前に印を立て、直後に戻す。
    //
    // **盤面には一切影響しない**——`Probe` は既定 null（通常の実行では一度も呼ばれない）で、
    // 印そのものはどの規則からも読まれない。乱数も1つも消費しない
    // （`compare` 305 セルが `docs/balance.md` と 0 件であることが検算・第94期の Q3）。
    // =====================================================================================

    /// <summary>味方の刃（<c>isFriendlyFire</c>）を観測するための擬似キー。第94期 (T2)。</summary>
    public const string FriendlyBladeKey = "味方の刃";

    /// <summary>印が立っていない（＝engine の中継が出した）味方の刃。第94期 (T2)。</summary>
    public const string FriendlyBladeEngineKey = "味方の刃(engine)";

    /// <summary>いま実行中の特性（<see cref="Probe"/> 用の印）。<b>観測専用。</b></summary>
    public TraitMark Mark { get; private set; }

    /// <summary>観測子。<b>既定 null。</b>診断（`derive scan`）だけが立てる。</summary>
    public CounterProbe? Probe { get; set; }

    /// <summary>印を立て、直前の印を返す（呼び出し側が <see cref="EndTrait"/> へ戻す）。</summary>
    public TraitMark BeginTrait(TraitId id, UnitState owner)
    {
        TraitMark prev = Mark;
        Mark = new TraitMark(id, owner);
        return prev;
    }

    /// <summary>印を戻す。</summary>
    public void EndTrait(TraitMark prev) => Mark = prev;

    // =====================================================================================
    // 第105期（手番の値段）。**どれも観測専用で、どの規則もこれを読まない。**
    // =====================================================================================

    /// <summary>
    /// いま <see cref="TakeTurn"/> の枠の中にいる駒（入れ子なら内側）。<b>観測専用。</b>
    /// 再行動（第104期）は <c>HandleDeath</c> の中から <c>TakeTurn</c> を呼ぶので入れ子になる
    /// ——だから1本の変数ではなく<b>退避して戻す</b>形で持つ。
    /// </summary>
    public UnitState? TurnActor { get; private set; }

    /// <summary>
    /// <b>その出力が「手番の中」で生まれたか。</b> 条件は3つの積で、
    /// <b>この定義はここ1箇所にしか無い</b>（第105期 §2 の境界）:
    /// <list type="number">
    /// <item><see cref="TakeTurn"/> の枠の中であること</item>
    /// <item><b>出どころがその枠の主であること</b>——棘の反撃は殴った側の枠の中で走るが
    ///   出どころは棘の側なので外に落ちる</item>
    /// <item>反撃（<see cref="InReaction"/>）・割り込み（<see cref="InInterrupt"/>）の中でないこと</item>
    /// </list>
    /// <c>OnTurnStart</c> は行動順ループの<b>外側</b>なので (1) で外れる。
    /// </summary>
    public bool InOwnTurn(UnitState? u)
        => u is not null && ReferenceEquals(TurnActor, u) && !InReaction && !InInterrupt;

    /// <summary>ログを一時的に黙らせる（売れた手番の判定が <c>CanAct</c> のログを二重に出さないため）。</summary>
    private bool _quiet;

    /// <summary>行動順ループが <see cref="TakeTurn"/> を呼んだ回数（自己検査 (c) の右辺）。</summary>
    public int TurnLoopCalls;

    /// <summary>
    /// 前倒し（第149期）が実際に <c>order</c> を組み替えた回数（<b>計数のみ。どの規則も読まない</b>）。
    /// 既に先頭にいる駒が選ばれたターンは数えない。
    /// </summary>
    public int HasteMoves;

    /// <summary>
    /// 前倒しが <c>order</c> の<b>要素数</b>を変えてしまった回数（第149期）。<b>常に 0 のはず。</b>
    /// <c>Remove</c> に失敗して <c>Insert</c> だけが通ると 1 増える。
    /// </summary>
    public int HasteCountMismatch;

    /// <summary>
    /// 出力の3分割の総計（自己検査 (d)）。<c>*All</c> は分割前の総量で、
    /// <c>In + Off + None == All</c> が成り立つことを診断が検算する。
    /// <c>None</c> は<b>誰のものでもない出力</b>——毒・燃焼の刻み（<c>source</c> が null）と、
    /// 特性の印が立っていない箇所からの回復・状態異常の書き込み。
    /// </summary>
    public long TurnDmgAll, TurnDmgIn, TurnDmgOff, TurnDmgNone;
    public long TurnHealAll, TurnHealIn, TurnHealOff, TurnHealNone;
    public long TurnStatusAll, TurnStatusIn, TurnStatusOff, TurnStatusNone;
    /// <summary>4つ目の通貨（第106期 (T1)）。<c>AtkBonus</c> を動かした<b>絶対量</b>の3分割。</summary>
    public long TurnBuffAll, TurnBuffIn, TurnBuffOff, TurnBuffNone;

    /// <summary>
    /// 4つ目の通貨を<b>特性ごとに</b>割った観測（第106期 Phase 0 §1）。添字は <c>(int)TraitId</c>。
    /// <b>盤面には一切影響しない。</b>「<c>AtkBonus</c> を動かす経路の全数」を手で書かずに出す器具。
    /// </summary>
    public readonly long[] BuffGainByTrait = new long[TraitIdCount];
    public readonly long[] BuffLossByTrait = new long[TraitIdCount];
    public long BuffGainNoMark, BuffLossNoMark;

    internal static readonly int TraitIdCount = Enum.GetValues(typeof(TraitId)).Length;

    /// <summary>ダメージ1件を3分割の帳簿へ入れる（<see cref="ApplyDamage"/> から。<b>盤面には影響しない</b>）。</summary>
    private void NoteTurnDamage(UnitState? source, int amount)
    {
        TurnDmgAll += amount;
        if (source is null) { TurnDmgNone += amount; return; }
        if (InOwnTurn(source)) TurnDmgIn += amount; else TurnDmgOff += amount;
    }

    /// <summary>カウンタの読みを1件観測する（<see cref="UnitState.Counter"/> から来る）。</summary>
    internal void NoteProbeRead(UnitState u, string key)
    {
        if (Probe is null || Mark.Owner is null) return;
        Probe(Mark.Id, Mark.Owner, u, key, 0);
    }

    /// <summary>書きを1件観測する。<paramref name="delta"/> は増減の符号つき（0 は読み）。</summary>
    internal void NoteProbeWrite(UnitState u, string key, int delta)
    {
        if (Probe is null || Mark.Owner is null || delta == 0) return;
        Probe(Mark.Id, Mark.Owner, u, key, delta);
    }

    public IReadOnlyList<UnitState> AllUnits => _units;

    /// <summary>
    /// 盤面に駒を加える。増援・蘇生もここを通す（InstanceId を必ず振るため）。
    /// ID は verbose に関係なく振る。verbose のときだけ振ると、
    /// 一括シミュレーションと再生とで盤面の同一性が変わってしまう。
    /// </summary>
    internal void Add(UnitState u)
    {
        // 業の計数フックを短絡させるためのフラグ（盤面には影響しない）。
        if (u.HasTrait(TraitId.Scapegoat)) ScapegoatActive = true;
        if (u.HasTrait(TraitId.Divert)) DivertActive = true;
        if (u.HasTrait(TraitId.Finisher)) FinisherActive = true;
        // 第150期 段A: 標の一生の帳簿を短絡させるためのフラグ（**計数専用**。盤面には影響しない）。
        // 標を書ける駒（囃し立て・逸らし・駆り立て・業）が1枚も盤上にいなければ走査ごと飛ばす。
        if (u.HasTrait(TraitId.Marker) || u.HasTrait(TraitId.Divert)
            || u.HasTrait(TraitId.Goad) || u.HasTrait(TraitId.Scapegoat)) MarkActive = true;
        if (u.HasTrait(TraitId.Funnel)) FunnelActive = true;
        // 第137期: 砕けの保持者が盤上にいるか（`ShatterSoaked` を短絡させるためだけ。盤面には影響しない）。
        if (u.HasTrait(TraitId.Shatter)) ShatterActive = true;
        // 第132期 段1: 上限の保持者をここで拾う（`YokeBinding` が全駒を走査しないため）。
        // 第179期: 灰の保持者（`NoteAsh` が全駒を走査しないため）。
        if (u.HasTrait(TraitId.Ash)) _ashHolders.Add(u);
        if (u.HasTrait(TraitId.Yoke)) _yokeHolders.Add(u);
        // 第134期 段2: 残り3つの盤面ルールの保持者も同じ形で拾う（**計数専用**。
        // 渇きだけは `Heal` の入口の判定もここに寄せた——`AllUnits.Any(...)` と同値）。
        if (u.HasTrait(TraitId.Drought)) _droughtHolders.Add(u);
        // 第154期: 預かりの代金の保持者（惨禍と同じく「本人ではなく味方」に効くので engine 側に判定がある）。
        // **既定（`WardCost.Forfeit`）ではこの2本のリストを1度も引かない。**
        if (u.HasTrait(TraitId.Burden)) _burdenHolders.Add(u);
        if (u.HasTrait(TraitId.Laden)) _ladenHolders.Add(u);
        if (u.HasTrait(TraitId.Hush)) _hushHolders.Add(u);
        if (u.HasTrait(TraitId.Inversion)) _inversionHolders.Add(u);
        u.InstanceId = _nextInstanceId++;
        u.Board = this;          // 「隣に誰がいるか」を読む特性のため（UnitState.Board の doc 参照）
        _units.Add(u);
    }

    /// <summary>
    /// 支援・弱体の宛先を解く。**ばら撒き型（味方全体を回す種類）専用。**
    ///
    /// <para>拡散持ち（誓約が壊れたガルド）は自分では受け取らず、<b>隣接する味方へそのまま渡す</b>。
    /// 無効化のままだとマイナスがその駒の中で閉じてしまい、噛み合う余地が生まれない
    /// （README の分かち＝ドハで記録済みの穴と同じ形）。渡すことで
    /// 「ガルドの隣に誰を置くか」が初めて編成の判断になる。</para>
    ///
    /// <para><b>割り算はしない。</b>隣接それぞれが満額を受け取る。率で割ると、毎ターン走る
    /// ばら撒き（号令・萎縮）では端数の扱いが比例関係を壊す（分かちの腕なまりで
    /// 切り捨てを選んだのと同じ理由）。代わりに隣接の数＝配置が量を決める。
    /// 隣接は前3なら1枠・前2なら3枠なので、<b>置き場所が拡散の形そのものになる</b>。</para>
    ///
    /// <para>渡した先が更に拡散することはない（渡す相手を <c>AcceptsSupport</c> で絞っている）。
    /// ばら撒きが元から全員を回るので、隣接した味方は<b>直接ぶんと拡散ぶんで二重に受ける</b>。
    /// これが狙いで、逆しま（ウツ）を隣に置くと弱体が二重に乗って大きく伸びる。</para>
    ///
    /// <para>対象を1体選ぶ型（継ぎ当て・縛め・移り木）はここを通さない。あちらは
    /// ガルドを選択候補から外したままにしてある。候補に戻すと「最も傷ついた味方」を
    /// 壁役が独占して、回復が常に隣へ流れ続ける形になりかねないため。</para>
    /// </summary>
    public IReadOnlyList<UnitState> SupportTargets(UnitState u)
    {
        if (u.AcceptsSupport) return new[] { u };
        if (!u.HasTrait(TraitId.Stoic)) return Array.Empty<UnitState>();

        var heads = LivingMembers(u.TeamId)
            .Where(a => a != u && a.AcceptsSupport && FormationRules.AreAdjacent(u.Slot, a.Slot))
            .ToList();
        // 第135期の計数。**隣へ流した回数と宛先の延べ数**（指示書 Q0-5）。
        // **量は持たない**——この窓口は「誰に配るか」しか知らない。量は素体対照で取る。
        if (HarmCensus)
        {
            UnitTally st = TallyOf(u);
            st.StoicSupportHops++;
            st.StoicSupportHeads += heads.Count;
        }
        return heads;
    }

    public int Opponent(int teamId) => teamId == PlayerTeam ? EnemyTeam : PlayerTeam;

    /// <summary>
    /// 生存中の味方。必ずスナップショットを返す。
    /// 召喚や蘇生が特性の中から呼ばれるので、遅延評価のままだと列挙中に盤面が変わって落ちる。
    /// </summary>
    public IReadOnlyList<UnitState> LivingMembers(int teamId)
        => _units.Where(u => u.TeamId == teamId && u.IsAlive).ToList();

    /// <summary>
    /// 生存中の味方を<b>並びを混ぜて</b>返す。**味方全員に順に効果を適用する処理はこちらを使う。**
    ///
    /// <para><see cref="LivingMembers"/> は <c>_units</c> の並び＝実質スロット昇順なので、
    /// 「途中で誰かが落ちるとその後の適用が変わる」種類の処理（吸い・巻き込み・破裂）は
    /// 席番号順に解決していた。X字盤面では前1と前3（後1と後3）が等価なはずなので、
    /// これが残っていると鏡像の配置が同値にならない
    /// （ゴルムの吸い × セロの逃亡で、鏡像差が独立 seed でも 6.6pt 残っていた）。</para>
    ///
    /// <para>数える・探す用途では使わないこと。<c>Roll</c> を <c>Count - 1</c> 回消費する。</para>
    /// </summary>
    public IReadOnlyList<UnitState> LivingMembersShuffled(int teamId)
    {
        var list = _units.Where(u => u.TeamId == teamId && u.IsAlive).ToList();
        Shuffle(list);
        return list;
    }

    public bool TeamAlive(int teamId) => LivingMembers(teamId).Any();

    /// <summary>
    /// <b>「最も傷ついた味方」</b>（自分を除く・<see cref="UnitState.AcceptsSupport"/> を通る・
    /// HP が満タンでない生存者のうち、HP割合が最小の駒）。該当が無ければ null。
    ///
    /// <para>継ぎ当て（<see cref="MenderTrait"/>）・施し（<see cref="AlmsTrait"/>）・
    /// 縫い（<see cref="SutureTrait"/>）の3者が同じ選択を持つので、
    /// <b>定義をここ1箇所に集めてある</b>（第39期に抽出。挙動は3者とも従来と1バイトも変えていない）。</para>
    ///
    /// <para>同値のタイブレークは <see cref="PickOne"/>。HP割合が同値なら席番号順に落ちるのを
    /// 避けるための唯一の窓口で、候補 0 個・1 個では <c>Roll</c> を消費しない。</para>
    ///
    /// <para><b>回復量の上限（継ぎ当ての自消費）や封じ（渇き）はここでは見ない。</b>
    /// 患者を選ぶことと、実際に何が届くかは別の層（<see cref="Heal"/>）の仕事。</para>
    /// </summary>
    public UnitState? MostHurtAlly(UnitState self) => MostHurtAlly(self, null);

    /// <summary>
    /// 同じ選択に候補の絞りを掛けた版（第155期）。<b><paramref name="filter"/> が
    /// <c>null</c> なら上の版と1ビットも違わない</b>——選択の定義を2箇所に増やさないための
    /// オーバーロードで、規則は1つも足していない。
    /// </summary>
    public UnitState? MostHurtAlly(UnitState self, Func<UnitState, bool>? filter)
    {
        var hurt = LivingMembers(self.TeamId)
            .Where(a => a != self && a.AcceptsSupport && a.Hp < a.MaxHp)
            .Where(a => filter is null || filter(a)).ToList();
        int worst = hurt.Count == 0 ? 0 : hurt.Min(a => a.Hp * 100 / Math.Max(1, a.MaxHp));
        return PickOne(hurt.Where(a => a.Hp * 100 / Math.Max(1, a.MaxHp) == worst).ToList());
    }

    /// <summary>
    /// <b>「引きずり出す駒」と「引きずり出す先の枠」</b>の組。<paramref name="teamId"/> の陣の
    /// 生存駒から、<b>後列で現在HPが最も高い1体</b>と<b>前列で現在HPが最も低い1体</b>を選ぶ。
    /// どちらかが 0 体なら null。
    ///
    /// <para>曝き（<see cref="ExposeTrait"/>・第40期）と突き返し（<see cref="ShoveTrait"/>・第41期）が
    /// 同じ選択を持つので、<b>定義をここ1箇所に集めてある</b>（第39期の
    /// <see cref="MostHurtAlly"/> と同じ扱い。挙動は曝き側と1バイトも変えていない）。
    /// <b>選び方を揃えるのは意図的</b>——プレイヤーが規則を1つ覚えれば両方読める。
    /// 片方だけ振りたくなった時点で分ければよい。</para>
    ///
    /// <para><b>決定的にする。乱数で選ばない。</b> 同値が並んだときだけ <see cref="PickOne"/>
    /// （席バイアスを作らないための既存の窓口。候補 0 個・1 個では <c>Roll</c> を消費しない）。</para>
    ///
    /// <para><b>召喚枠を含める。</b> <c>Row.Back</c> には ○後2（スロット8）も入る。実態があるなら
    /// 盤面の一部として扱う、という既存の判断（貫きのレーン経路・巨躯の被覆）に揃えた。</para>
    ///
    /// <para><b>止まる条件は呼び出し側に残す。</b> 上限（<see cref="ExposeRule.MaxPerBattle"/>）も
    /// 空振りの計数も、選択そのものの一部ではない。</para>
    /// </summary>
    public (UnitState Victim, UnitState Seat)? HaulOutPair(int teamId)
    {
        var foes = LivingMembers(teamId);

        // 引き出す駒＝いちばん隠れている駒＝後列で現在HPが最も高い1体。
        var hidden = foes.Where(f => f.Row == Row.Back).ToList();
        if (hidden.Count == 0) return null;

        // 引き出す先＝いちばん先に落ちる枠＝前列で現在HPが最も低い1体。
        // 矢面の意味が最大になる席へ出す。
        var exposedTo = foes.Where(f => f.Row == Row.Front).ToList();
        if (exposedTo.Count == 0) return null;

        int most = hidden.Max(f => f.Hp);
        UnitState? victim = PickOne(hidden.Where(f => f.Hp == most).ToList());

        int least = exposedTo.Min(f => f.Hp);
        UnitState? seat = PickOne(exposedTo.Where(f => f.Hp == least).ToList());

        // 同じ駒が両方に選ばれることはない（Row.Back と Row.Front は排他）。
        if (victim is null || seat is null) return null;
        return (victim, seat);
    }


    public IReadOnlyList<LogLine> Log_ => _log;
    public IReadOnlyList<BattleEvent> Events => _events;

    /// <summary>
    /// 行頭の空白をインデント段数として取り込む。
    /// 呼び出し側は今まで通り空白付きの文字列を渡せばよい。
    ///
    /// 見せ場（Highlight）だけは構造化イベントにも流す。特性側は今まで通り
    /// ctx.Log を呼ぶだけでよく、演出の差し込み位置が自動的に台本へ乗る。
    /// </summary>
    /// <param name="by">
    /// 見せ場の主（第124期 段2・<b>表示専用</b>）。<see cref="LogKind.Highlight"/> のときだけ
    /// <see cref="BattleEvent.ActorId"/> へ載る。<b>省略可能なので既存の呼び出しは1つも壊れない。</b>
    /// <b>書き手が居ないところへ無理に「動いた本人」を入れないこと</b>——それは書き手ではないので、
    /// 線を引くと嘘になる（第124期 §5-3）。
    /// </param>
    public void Log(string line, LogKind kind = LogKind.Action, UnitState? by = null)
    {
        if (!_verbose || _quiet) return;
        int spaces = line.Length - line.TrimStart().Length;
        string text = line.Trim();
        _log.Add(new LogLine(kind, spaces / 2, text));

        if (kind == LogKind.Highlight)
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.Highlight,
                Turn = _turn,
                ActorId = by?.InstanceId,
                Text = text,
            });
    }

    /// <summary>
    /// 構造化イベントを積む。ログと同じく verbose のときだけ。
    /// 一括シミュレーション（compare / layout は数百万戦を回す）で積むと確保だけで効いてくる。
    /// **ここは盤面を一切変えない。** 変えた瞬間、verbose の有無で戦闘結果が変わる。
    /// </summary>
    private void Emit(BattleEvent e)
    {
        if (!_verbose) return;
        _events.Add(e);
    }

    /// <summary>ターンの区切りを台本に打つ。再生側はここで間を置く。</summary>
    internal void EmitTurnStart()
        => Emit(new BattleEvent { Kind = BattleEventKind.TurnStart, Turn = _turn });

    /// <summary>
    /// 介入が主目標を差し替えたことを台本に打つ（第125期・<b>表示専用</b>）。
    ///
    /// <para><b>盤面は1ビットも触らない。</b> <c>verbose</c> のときだけ積むのは他の
    /// <c>Emit*</c> と同じで、<b>計数（<see cref="UnitTally.Intercepts"/>）は
    /// <c>verbose</c> に依らず積む</b>——`UnitTally` は 200 seed の一括シミュレーションで
    /// 平均を取るためのもので、ここを verbose で切ると測りたいときに測れない。</para>
    ///
    /// <para><b>呼ぶのは差し替えが実際に起きた段だけ。</b> 標の段の
    /// <c>marked == target</c>（もともと主目標だった）は鎖の計数を動かさない既存の作法に揃える。</para>
    /// </summary>
    /// <param name="guard">割り込んだ駒。</param>
    /// <param name="target">本来の標的。</param>
    /// <param name="label">どの段か（<see cref="InterceptLabels"/> の5つのどれか）。</param>
    /// <summary>
    /// <b>混乱した攻撃を庇った回数</b>（第147期・<b>計数のみ。どの規則も読まない</b>）。
    /// 介入の鎖の6段すべてに1行ずつ置いてある（標・後備え×2・庇う・殉教・棘守り）。
    ///
    /// <para><b>鎖は陣営を見ない</b>ので、混乱した敵の攻撃は<b>敵の殉教者が庇う</b>
    /// ——第147期の予測4 はこの計数で読む。<c>NoteGuardPick</c> は流用できない
    /// （あちらは <c>WoundCensus</c> とプレイヤー陣営で絞ってある・第120期）。</para>
    /// </summary>
    private void NoteConfusedGuard(UnitState attacker, UnitState guard)
    {
        if (ConfusionLive && attacker.RawCounter(StatusKeys.Confused) > 0)
            TallyOf(guard).ConfusedGuards++;
    }

    private void EmitIntercept(UnitState guard, UnitState target, string label)
    {
        TallyOf(guard).Intercepts++;
        // 第135期。**段別の内訳**と、「次にこの駒へ入るダメージは引き受けたぶん」の印。
        // どちらも計数専用で、盤面は1ビットも動かない（既定では配列も確保しない）。
        if (HarmCensus)
        {
            int li = Array.IndexOf(InterceptLabels.All, label);
            if (li >= 0)
            {
                int[] by = TallyOf(guard).InterceptsByLabel ??= new int[InterceptLabels.All.Length];
                by[li]++;
            }
            _interceptedInto = guard;
        }
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Intercept,
            Turn = _turn,
            ActorId = guard.InstanceId,
            TargetId = target.InstanceId,
            Slot = guard.Slot,
            HpAfter = guard.Hp,
            Team = guard.TeamId,
            Text = label,
        });
    }

    /// <summary>
    /// 溜めを台本に打つ。**次の手番に何が来るかをこの1件で読めること**が要件
    /// （溜めは画面上「何も起きないターン」なので、予告が無いとただの空白になる）。
    /// 次の行動が攻撃型を上書きしないなら、いま実際に使う型（CurrentPattern）を載せる。
    /// </summary>
    internal void EmitCharge(UnitState actor, UnitAction charging, UnitAction? next)
        => Emit(new BattleEvent
        {
            Kind = BattleEventKind.Charge,
            Turn = _turn,
            ActorId = actor.InstanceId,
            Text = charging.Label,
            Amount = next?.AttackPercent ?? 100,
            Pattern = next?.PatternOverride ?? actor.CurrentPattern,
        });

    /// <summary>
    /// 術の手番を台本に打つ。効果そのもの（回復・毒の濃縮）は各特性が自分のイベントを
    /// 出すので、ここは「誰がその手番に何を撃ったか」だけを置く。
    /// **空振りでも必ず打つ**（理由は <see cref="BattleEventKind.Skill"/>）。
    /// </summary>
    internal void EmitSkill(UnitState actor, UnitAction skill)
        => Emit(new BattleEvent
        {
            Kind = BattleEventKind.Skill,
            Turn = _turn,
            ActorId = actor.InstanceId,
            Text = skill.Label,
        });

    /// <summary>
    /// 「条件が成立している駒がいま初めて出た」を1度だけ見せ場に打つ（第97期・<b>表示専用</b>）。
    ///
    /// <para><b>条件が成立していることを画面から読めるようにするためだけ</b>にある
    /// ——散開・熾火・後衛特化はどれも「隣が空いた」「燃えている」「下がった」を engine が
    /// <b>毎回その場で評価する</b>ので、成立の瞬間が出来事として1つも残らない
    /// （第82期の実測で散開は +2.61pt 効いているのに、画面では装備と区別が付かない）。</para>
    ///
    /// <para><b>毎回打つと被弾のたびに出て読めない</b>ので、駒 × 条件 で1戦に1度だけ打つ。
    /// 抑止の記憶（<see cref="_shown"/>）は <b>verbose のときしか触らない</b> フィールドで、
    /// <c>Counters</c> には1文字も書かない——盤面にも計数にも一切影響しない。</para>
    /// </summary>
    private readonly HashSet<(int Id, string Key)> _shown = new();

    internal void HighlightOnce(UnitState u, string key, string line)
    {
        if (!_verbose) return;
        if (!_shown.Add((u.InstanceId, key))) return;
        Log(line, LogKind.Highlight, u);   // 第124期 段2: 見せ場の主を台本へ載せる
    }

    /// <summary>
    /// 状態異常が付いた瞬間を台本に打つ（第97期・<b>表示専用</b>）。
    ///
    /// <para><b>engine の窓口を持つ4通貨だけが呼ぶ</b>——<see cref="Wound"/> / <see cref="Poison"/> /
    /// <see cref="Ignite"/> / <see cref="Dull"/>。窓口の無い痺れ・標・破片は出さない
    /// （出すには先に窓口が要る。第90・93期と同じ形）。</para>
    ///
    /// <para><see cref="Emit"/> は verbose のときしか積まないので、<c>compare</c>（verbose 偽）では
    /// 1件も作られない。<b>盤面には一切影響しない</b>——<c>NoteStatusGain</c> の計数にも触っていない。</para>
    /// </summary>
    /// <param name="writer">書いた駒。engine の規則が足したぶんは null。</param>
    internal void EmitStatusGain(UnitState target, string key, int amount, UnitState? writer)
    {
        if (!_verbose || amount <= 0) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.StatusGain,
            Turn = _turn,
            ActorId = writer?.InstanceId,
            TargetId = target.InstanceId,
            Amount = amount,
            Text = key,
        });
    }

    /// <summary>
    /// 転倒を台本に打つ（第145期・<b>表示専用</b>）。呼び口は2つだけ——
    /// <c>ShufflerTrait</c>（付いた瞬間）と <see cref="TakeTurnCore"/>（手番を失った瞬間）。
    ///
    /// <para><see cref="Emit"/> は verbose のときしか積まないので、<c>compare</c>（verbose 偽）では
    /// 1件も作られない。<b>盤面には一切影響しない</b>——計数（<c>ShuffleStaggers</c> /
    /// <c>StallStagger</c>）にも触っていない。</para>
    /// </summary>
    /// <param name="phase"><see cref="StaggerLabels"/> のどちらか。</param>
    /// <param name="by">転ばせた駒。消費側は null。</param>
    internal void EmitStagger(UnitState target, string phase, UnitState? by)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Stagger,
            Turn = _turn,
            ActorId = by?.InstanceId,
            TargetId = target.InstanceId,
            HpAfter = target.Hp,
            Text = phase,
        });
    }

    /// <summary>
    /// 痺れを台本に打つ（第146期 段0・<b>表示専用</b>）。呼び口は2つだけ——
    /// <see cref="NoteStatusGain"/>（付いた瞬間）と <c>TakeTurnCore</c>（手番を失った瞬間）。
    ///
    /// <para><b>付与側を <see cref="NoteStatusGain"/> に置いたのは、そこが
    /// <see cref="UnitState.SetCounter"/> から来る唯一の合流点だから。</b>
    /// <c>StatusKeys.Stun</c> を書く箇所は <c>Traits.cs</c> に7つ（責め苦・断罪・縛め・
    /// 深追い・かき回し ほか）＋ 業の引き取りがあるが、全部ここを通る
    /// ——7箇所に呼び出しを撒くと、次に痺れを書く札が増えたとき静かに1本落ちる。
    /// 書き手は <c>Mark.Owner</c>（第94期 (T2) の印）から取れるので引数も要らない。</para>
    ///
    /// <para><see cref="Emit"/> は verbose のときしか積まないので、<c>compare</c>（verbose 偽）では
    /// 1件も作られない。<b>盤面には一切影響しない</b>——計数（<c>CarryStun</c> /
    /// <c>StallStun</c>）にも触っていない。</para>
    /// </summary>
    /// <param name="phase"><see cref="StunLabels"/> のどちらか。</param>
    /// <param name="by">痺れさせた駒。engine 由来と消費側は null。</param>
    internal void EmitStun(UnitState target, string phase, UnitState? by)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Stun,
            Turn = _turn,
            ActorId = by?.InstanceId,
            TargetId = target.InstanceId,
            HpAfter = target.Hp,
            Text = phase,
        });
    }

    /// <summary>
    /// 混乱を台本に打つ（第147期・<b>表示専用</b>）。呼び口は2つだけ——
    /// <see cref="NoteStatusGain"/>（付いた瞬間）と <c>ConsumeConfusion</c>（自軍へ振った瞬間）。
    ///
    /// <para><b>付与側を <see cref="NoteStatusGain"/> に置いたのは、そこが <c>SetCounter</c> から来る
    /// 唯一の合流点だから</b>（痺れと同じ判断）。混乱の書き手は<b>2つある</b>
    /// ——喧噪（<c>ShufflerTrait</c>・<see cref="ShuffleStagger.Confuse"/>）と
    /// 波ルール版（<c>SwapSlots</c> の通知・<see cref="ConfusionRule"/>・既定オフ）で、
    /// 呼び口を分けると片方だけ画面から落ちる。</para>
    ///
    /// <para><see cref="Emit"/> は verbose のときしか積まないので、<c>compare</c>（verbose 偽）では
    /// 1件も作られない。<b>盤面には一切影響しない。</b></para>
    /// </summary>
    /// <param name="phase"><see cref="ConfusedLabels"/> のどちらか。</param>
    /// <param name="by">混乱させた駒。発動側は null。</param>
    internal void EmitConfused(UnitState target, string phase, UnitState? by)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Confused,
            Turn = _turn,
            ActorId = by?.InstanceId,
            TargetId = target.InstanceId,
            HpAfter = target.Hp,
            Text = phase,
        });
    }

    /// <summary>
    /// 盤面ルールが何かを封じたことを台本に打つ（第171期・<b>表示専用</b>）。呼び口は3つだけ——
    /// <see cref="NoteHushBlocked"/>（粛が<b>単独の原因</b>のときだけ）・
    /// <see cref="NoteDroughtBlocked"/>（回復の入口）・<c>ApplyDamage</c> の <c>yokeBinding</c> の中。
    ///
    /// <para><b>3箇所とも既にある計数の合流点</b>——粛・渇きは <c>Note*</c> の中、軛は
    /// 切る前にしか取れない量を記録している同じブロックの中。<b>新しい判定は1つも足していない</b>ので、
    /// 盤面ルールの答えも乱数の消費も1ビットも変わらない（第171期 Q0-3）。</para>
    ///
    /// <para><see cref="Emit"/> は verbose のときしか積まないので、<c>compare</c>（verbose 偽）では
    /// 1件も作られない。<b>計数（<c>HushBlockedSide</c> / <c>DroughtHits</c> / <c>YokeCutHits</c>）には
    /// 触っていない</b>——自己検査 (c) は「帳簿の数 ＝ 台本の封じイベントの数」で取るので、
    /// ここで数え直すと検算にならない。</para>
    /// </summary>
    /// <param name="target">封じられた駒。</param>
    /// <param name="rule"><see cref="SealedLabels"/> の3つのどれか。</param>
    /// <param name="amount">通らなかった量（渇き＝入るはずだった回復／軛＝切り落とされた量／粛＝0）。</param>
    /// <param name="by">相手側の駒（軛なら殴った駒）。粛・渇きは null。</param>
    internal void EmitSealed(UnitState target, string rule, int amount, UnitState? by = null)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Sealed,
            Turn = _turn,
            ActorId = by?.InstanceId,
            TargetId = target.InstanceId,
            Amount = amount,
            HpAfter = target.Hp,
            Team = target.TeamId,
            Text = rule,
        });
    }

    /// <summary>
    /// そのターン頭に各駒が負っている継続効果を、値ごと台本へ写す。
    /// 再生側は TurnStart で持っている状態を捨て、これで組み直す（0 のものは出さない）。
    /// </summary>
    internal void EmitStatusSnapshot()
    {
        if (!_verbose) return;

        foreach (UnitState u in _units)
        {
            if (!u.IsAlive) continue;

            foreach ((string key, string label) in StatusLabels)
            {
                int v = u.RawCounter(key);
                if (v <= 0) continue;
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.StatusSnapshot,
                    Turn = _turn,
                    TargetId = u.InstanceId,
                    Amount = v,
                    Text = label,
                });
            }

            // 積み上げ系は素の値から大きく離れる（墓守は層の三角数で伸びる）。
            // 素の攻撃力だけ見せると、盤面で何が起きているか読めない。
            //
            // **攻撃型も一緒に写す（第98期 V1・表示専用）。** 戦闘中に型が変わる駒が5枚あり
            // （逃亡兵セロ／墓守リィカ／熾のホタ／鱗のウロ／軋みのヨミ）、
            // 開始時の型だけを見せると**画面と盤面が食い違う**——第97期の画面は
            // `BattleOpening.Pattern` を1回読むだけだったので、下がったセロが単体のまま表示されていた。
            // **新しいフィールドは足していない**（`BattleEvent.Pattern` は `Attack` / `Damage` が既に使っている）。
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.StatSnapshot,
                Turn = _turn,
                TargetId = u.InstanceId,
                Amount = u.CurrentAttack,
                Pattern = u.CurrentPattern,
            });
        }
    }

    /// <summary>
    /// スナップショットに出す継続効果と、その表示名。
    ///
    /// <para><b>手で並べない</b>（第97期 D4）——<see cref="StatusKeys.All"/> を列挙して
    /// <see cref="StatusKeys.LabelOf"/> を当てる。手で並べていた頃は 7 本で、
    /// <c>IdleTurn</c> と <c>Curse</c> が<b>黙って落ちていた</b>
    /// （第95期の「`StatusKeys` は 7 本ではなく 8 本」と同じ形の数え落とし）。
    /// <b>これでキーを足せば札も自動で増える。</b></para>
    ///
    /// <para>札の文字列は <c>LabelOf</c> が正になったので <c>Armor</c> だけ「盾」→「破片」に変わる。
    /// <b>診断が突き合わせている札は 毒・燃・痺・傷・深手 の5つで、どれも <c>LabelOf</c> と一致する</b>
    /// （破片を名前で引いている診断は1つも無い）。</para>
    ///
    /// <para><c>IdleTurn</c> は<b>量ではなくターン番号</b>（そのターン動けなかった記録）で、
    /// 0 に戻す箇所が1つも無い。<b>再生側がその意味で描くこと</b>——
    /// 台本には落とさずに載せる、というのがここの責務。</para>
    /// </summary>
    private static readonly (string Key, string Label)[] StatusLabels =
        StatusKeys.All.Select(k => (Key: k, Label: StatusKeys.LabelOf(k))).ToArray();

    public int Roll(int maxExclusive) => _rng.Next(maxExclusive);

    /// <summary>
    /// 同値の候補から1体選ぶ。<b>席番号の若い順で決めないための唯一の窓口。</b>
    ///
    /// <para>X字化で盤面のグラフは自己同型になった（前1↔前3 / 後1↔後3 / ○中1↔○中3）が、
    /// 候補を <c>FirstOrDefault</c> で拾うとリストの並び＝実質スロット昇順に落ちるので、
    /// 鏡像の配置が同値にならない。実測で最大 24.1pt ずれていた。</para>
    ///
    /// <para><b>決定的なまま対称にする案は採らない。</b>「レーン内の位置で決める」なども
    /// 結局どこかで席番号に落ちる。乱数で割る。</para>
    ///
    /// <para><c>Roll</c> の消費は候補数に対して決定的（0個・1個なら消費しない）。</para>
    /// </summary>
    public UnitState? PickOne(IReadOnlyList<UnitState> candidates) => candidates.Count switch
    {
        0 => null,
        1 => candidates[0],
        _ => candidates[Roll(candidates.Count)]
    };

    /// <summary>
    /// Fisher-Yates。<b>消費する <c>Roll</c> は必ず <c>Count - 1</c> 回</b>で、
    /// 中身に依存しない。<c>OrderBy(_ => Roll(...))</c> は消費回数が読めないので使わない。
    /// </summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Roll(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>攻撃対象を選ぶ。前列が生きている限り後列は狙われない。庇うはここで割り込む。</summary>
    /// <summary>
    /// 主目標を選ぶ。
    /// 庇う・標的の介入は「一人を狙う攻撃」＝ Single にしか効かない。
    /// 薙ぎや全体を体で止めることはできず、貫きは前列そのものを素通りする。
    /// この非対称が、単体高火力とそれ以外の使い分けを生む。
    /// </summary>
    /// <param name="patternOverride">
    /// 行動（<see cref="UnitAction"/>）がこの手番だけ攻撃型を差し替える場合の型。
    /// null なら <see cref="UnitState.CurrentPattern"/>（＝従来と完全に同じ挙動）。
    /// </param>
    public UnitState? SelectTarget(UnitState attacker, AttackPattern? patternOverride = null)
        => SelectTargetCore(attacker, patternOverride, out _);

    /// <summary>
    /// 標的選択の本体。貫きのときは<b>選んだレーンも返す</b>（それ以外は -1）。
    ///
    /// 新盤面では中央が2本のレーンに属するので、entry のスロットからレーンを逆算できない
    /// （前1・前3 が両方落ちた局面で entry が中央になる）。かといって選び直すと
    /// <see cref="Roll"/> を二重に消費し、ログに出した entry と実際に貫く列が食い違う。
    /// <b>1回の攻撃につきここを呼ぶのは1度だけ。</b>
    /// </summary>
    private UnitState? SelectTargetCore(UnitState attacker, AttackPattern? patternOverride, out int lane)
    {
        UnitState? chosen = SelectTargetChain(attacker, patternOverride, out lane);

        // 執着（ノミ）は**介入の鎖を通ったあとの相手**を覚える。庇われたら次の手番からは
        // 庇った駒に執着が移る＝「庇うで執着を引き剥がす」（FixateTrait 参照）。
        // 鎖の前で覚えると、毎ターン庇われ続けて執着が永久に動かない駒になる。
        // 保持者の走査は既存条件の後ろ（&& の短絡）。効くのは単体攻撃だけ。
        if (chosen is not null && attacker.HasTrait(TraitId.Fixate)
            && (patternOverride ?? attacker.CurrentPattern) == AttackPattern.Single)
            FixateTrait.Remember(attacker, chosen);

        return chosen;
    }

    /// <summary>
    /// 標的選択の鎖の本体（<see cref="SelectTargetCore"/> から1回だけ呼ばれる）。
    /// 執着の記憶の書き込みは呼び出し側に置いてある——ここは戻り口が5つあり、
    /// **鎖を通ったあとの相手**を覚えるには出口を1つに絞る必要があるため。
    /// </summary>
    private UnitState? SelectTargetChain(UnitState attacker, AttackPattern? patternOverride, out int lane)
    {
        lane = -1;
        // 第135期。**標的選択1回ごとに印を落とす**（計数専用）。立ったまま次の一撃へ持ち越すと、
        // 破片が全額吸って `NoteHarm` に届かなかった介入が、無関係な被弾を「引き受けたぶん」に化けさせる。
        _interceptedInto = null;

        List<UnitState> foes = FoesOf(attacker);   // 第146期: 混乱はここで陣営を反転する
        if (foes.Count == 0) return null;

        AttackPattern pattern = patternOverride ?? attacker.CurrentPattern;

        // 第135期の計数。**庇いは Single にしか効かない**ので、単体以外の一撃では
        // 資格のある庇い手（前列に立つ Guardian / Martyr）は判定を振られることすらない。
        // これが「庇えなかった範囲攻撃の数」＝受け流しの機会数の上限になる（指示書 Q0-3）。
        //
        // **貫きの早期リターンより前に置く。** 後ろに置くと貫きが丸ごと落ちて、
        // 「庇えなかった範囲」から最大の経路が消える（実測でガルドの範囲の被弾の半分が貫き）。
        // **主目標の除外（f != target）は掛けない**——貫きの時点では主目標がまだ決まっていない。
        if (HarmCensus && pattern != AttackPattern.Single)
            foreach (UnitState f in foes)
                if (f.Row == Row.Front
                    && (f.HasTrait(TraitId.Guardian) || f.HasTrait(TraitId.Martyr)))
                    TallyOf(f).GuardRangeMissed++;

        if (pattern == AttackPattern.Pierce)
            return SelectPierceEntry(foes, out lane);

        // 前から順に、生き残っている最も前の列を狙う。
        List<UnitState> pool = PoolOf(foes);

        // 執着（ノミ）。**pool から無作為に選ぶ直前**が唯一の窓口で、攻撃者側の標的選択には
        // Trait のフックが無いので engine 側に置く（庇う・後備え・標的・棘守りと同じ層）。
        //
        // 「pool に含まれるなら」が安全弁——**前列が生きている限り後列は狙われない**という
        // 盤面の中核規則を執着に破らせない。記憶した敵が後列に取り残されたら執着は自然に解ける。
        // 生存判定も兼ねている（pool は生存者からしか作られない）。
        //
        // **薙ぎ・全体もここを通る**ので pattern を明示的に見る（貫きだけが手前で分岐する）。
        // 巻き込みの中心が固定されると、行動パターンで型が変わる駒と組んだときに意味が変わる。
        UnitState? fixated = pattern == AttackPattern.Single && attacker.HasTrait(TraitId.Fixate)
            ? FixateTrait.Remembered(attacker, pool)
            : null;

        // 傷の選好（断ち＝ナタ / 縫い＝ハリ）。**執着と同じ窓口**（pool から無作為に選ぶ直前）に置く攻撃者側の選好で、
        // pool そのものは1体も足さない・引かない——「前列が生きている限り後列は狙われない」は
        // 執着と同じく pool 経由で守られる。候補の中で傷がいちばん深い駒を選ぶだけ。
        //
        // **候補集合は SeverTrait.CanAct と共有する**（PoolOf / TargetPool の1箇所）。
        // 「傷持ちが1体もいなければ振らない」と「傷がいちばん深い相手を狙う」が別々の集合を
        // 数えると、振ると決めた手番で狙う相手がいない（あるいはその逆）が起こりうる。
        //
        // 保持者の走査は既存条件の後ろ（&& ではなく三項の条件側だが同じ短絡）。
        // 貫きは手前で分岐して pool を作らないので、この段は通らない（執着と同じ）。
        //
        // **第39期に利用者が2枚になった（ナタ＝断ち / ハリ＝縫い）が、段は増やさない。**
        // 「傷がいちばん深い敵を狙う」という選好の定義は SeverTrait.Prefers / Preferred の
        // 1箇所きりで、共有していないのは閾値（振るか捨てるか）だけ——あちらは CanAct の側。
        UnitState? severed = SeverTrait.Prefers(attacker)
            ? SeverTrait.Preferred(this, pool)
            : null;

        // 執着・断ちが効いている手番は **Roll を消費しない**。ここで引くと、効いている間と
        // いない間で以降の乱数列がずれる。同数のタイブレークは Preferred の中の PickOne
        // （候補 0 個・1 個では Roll を消費しない）。
        UnitState target = fixated ?? severed ?? pool[Roll(pool.Count)];

        if (fixated is not null)
            Log($"    {attacker.Name} は {fixated.Name} から目を離せない", LogKind.Trigger);
        else if (severed is not null)
            Log($"    {attacker.Name} は {severed.Name} の傷口を見定めた", LogKind.Trigger);

        // 以下、割り込む側は **PickOne**（同じ資格の駒が複数いたら乱数で選ぶ）。
        // FirstOrDefault のままだと常に席番号の若い駒が割り込むので、鏡像の配置が同値にならない。
        // **優先順位の鎖（標的 → 後備え → 庇う → 棘守り）は変えていない。**
        // 乱数化するのは「同じ段の中で誰が割り込むか」だけ。

        if (pattern != AttackPattern.Single)
        {
            // 後備えは範囲攻撃にも割り込む。貫きはレーン単位で解決するのでここを通らない。
            UnitState? rearAny = PickOne(foes.Where(
                f => f.HasTrait(TraitId.RearGuard) && f.Row == Row.Back && f != target).ToList());

            if (target.Row != Row.Front && rearAny is not null
                && Roll(100) < RearGuardTrait.RedirectPercent)
            {
                Log($"    {rearAny.Name} が後列の {target.Name} の前に入った", LogKind.Trigger);
                NoteGuardPick(GuardKind.RearGuard, rearAny, target);   // 第120期・§2-5 の材料
                NoteConfusedGuard(attacker, rearAny);   // 第147期（計数のみ）
                EmitIntercept(rearAny, target, InterceptLabels.RearGuard);   // 第125期 段1（表示専用）
                return rearAny;
            }
            return target;
        }

        // 止め（第53期）。**標の段を 100%・決定的にするだけで、窓口は増やしていない。**
        // 執着（pool から選ぶ直前）でも断ちの選好でもなく**この段**に置くのは、
        // 標の候補集合が `foes`（＝列を無視する）だからで、`pool` に移すと
        // **この駒の主眼である列越えが構造的に消える**（第50期 Phase 0-2）。
        //
        // **`Roll` を引かない**（執着・断ちと同じ）。候補が 2 体以上のときだけ Preferred の中の
        // PickOne が引くが、それは「同じ資格の駒が複数いたら乱数で選ぶ」既存の作法そのもの。
        bool finisher = attacker.HasTrait(TraitId.Finisher);
        UnitState? marked = finisher
            ? FinisherTrait.Preferred(this, foes)
            : PickOne(foes.Where(f => f.RawCounter(StatusKeys.Marked) > 0).ToList());

        // 止めのときは **`marked == target` でもここで返す**——標の段は鎖の1段目なので、
        // 庇い・後備え・殉教・棘守りを飛び越すのは標が元から持つ性質。
        // 「たまたま無作為の主目標が標持ちだった」ときだけ介入を許すのは非対称になる。
        if (marked is not null && (finisher || (marked != target && Roll(100) < MarkPullPercent)))
        {
            if (marked == target) return marked;   // 差し替えていないので鎖の計数は動かさない

            // 業（第49期）が敵へ書いた標が実際に引いた回数。**engine が標の読み手**なので、
            // 「敵側に読み手がいない」＝「効かない」ではない（第48期の棚卸しが数えたのは駒）。
            if (ScapegoatActive && marked.RawCounter(ScapegoatTrait.OwedKey(StatusKeys.Marked)) > 0)
                ScapegoatMarkPulls++;
            // 逸らし（第50期）。**engine の鎖が実際に主目標を差し替えた回数**を陣営別に数える。
            // ログの「気を取られた」と1対1で対応する（あちらは verbose のときしか出ない）。
            if (DivertActive)
            {
                if (attacker.TeamId == PlayerTeam) DivertAllyPulls++;
                else DivertFoePulls++;
            }
            Log($"    敵は {marked.Name} に気を取られた", LogKind.Trigger);
            NoteConfusedGuard(attacker, marked);   // 第147期（計数のみ）
            EmitIntercept(marked, target, InterceptLabels.Mark);   // 第125期 段1（表示専用）
            return marked;
        }

        UnitState? rear = PickOne(foes.Where(
            f => f.HasTrait(TraitId.RearGuard) && f.Row == Row.Back && f != target).ToList());

        if (target.Row != Row.Front && rear is not null && Roll(100) < RearGuardTrait.RedirectPercent)
        {
            Log($"    {rear.Name} が後列の {target.Name} の前に入った", LogKind.Trigger);
            NoteGuardPick(GuardKind.RearGuard, rear, target);   // 第120期・§2-5 の材料
            NoteConfusedGuard(attacker, rear);   // 第147期（計数のみ）
            EmitIntercept(rear, target, InterceptLabels.RearGuard);   // 第125期 段1（表示専用）
            return rear;
        }

        UnitState? guardian = PickOne(foes.Where(
            f => f.HasTrait(TraitId.Guardian) && f.Row == Row.Front && f != target).ToList());

        // 第135期の計数。**鎖が庇いの段まで来て、この駒が判定を振られた回数。**
        // 成立したぶんは InterceptsByLabel の側にあるので、差が「振って外した回数」になる
        // （第136期 段1 で RedirectPercent を 100 にしたので、以後は差が 0 になる。Roll は残す）。
        if (HarmCensus && guardian is not null) TallyOf(guardian).GuardChances++;

        if (guardian is not null && Roll(100) < GuardianTrait.RedirectPercent)
        {
            Log($"    {guardian.Name} が {target.Name} を庇った", LogKind.Trigger);
            // 肩代わりで受けた分だけ伸びる（GuardianTrait 参照）。素の被弾と区別するための印。
            guardian.SetCounter(GuardianTrait.PendingKey, 1);
            NoteGuardPick(GuardKind.Guardian, guardian, target);   // 第120期・§2-5 の材料
            NoteConfusedGuard(attacker, guardian);   // 第147期（計数のみ）
            EmitIntercept(guardian, target, InterceptLabels.Guardian);   // 第125期 段1（表示専用）
            return guardian;
        }

        // 殉教（敵の殉教者）。**庇うと挙動は1行も違わない**——別の段にしてあるのは
        // 割合（Martyr.RedirectPercent）を味方ガルドと分けるためだけ。共有したままだと
        // 殉教者の割合を振ったときにガルドを含む行が全部動いて交絡が戻る（第35期）。
        //
        // **ガルドの段の直後に置く。** 前に置くと、味方が庇うを持つ盤面で殉教が
        // ガルドを差し置くことになる（ガルドが 50% の専任である以上、先の権利は残す
        // ——棘守りをガルドの後ろに置いたのと同じ理由）。実際には両陣営に同時に
        // 立つ局面が無い（foes は攻撃者の相手陣営1つだけ）ので順序は観測不能だが、
        // 規則としては既存の鎖の作法に合わせておく。
        //
        // **PickOne は候補 0 個・1 個では Roll を消費しない**ので、段を1つ足しても
        // 乱数列は動かない（p=50 の同値検証がその証明）。
        UnitState? martyr = PickOne(foes.Where(
            f => f.HasTrait(TraitId.Martyr) && f.Row == Row.Front && f != target).ToList());

        if (martyr is not null && Roll(100) < Martyr.RedirectPercent)
        {
            Log($"    {martyr.Name} が {target.Name} を庇った", LogKind.Trigger);
            martyr.SetCounter(RedirectGainTrait.PendingKey, 1);
            NoteGuardPick(GuardKind.Martyr, martyr, target);   // 第120期・§2-5 の材料
            NoteConfusedGuard(attacker, martyr);   // 第147期（計数のみ）
            EmitIntercept(martyr, target, InterceptLabels.Martyr);   // 第125期 段1（表示専用）
            return martyr;
        }

        // 棘守り（カド）。**鎖の最後に置く。** 庇う（ガルド）は 50% の確率判定を持つ
        // 専任の防御役で、100% のカドを先に置くと常にガルドを差し置いてしまう。
        // ガルドに先の権利を与え、カドが残りを拾う。
        //
        // 守れるのは「前」か「横」だけ（ThornGuardTrait.Covers）。構えている印は
        // スキルの手番に立ち、ここで1回消費する。入れ替え相手のスロットを記録するだけで、
        // **SwapSlots はここで呼ばない**——移動は OnMoved を通じて割り込み攻撃を起こすので、
        // 標的選択の途中でやると攻撃が着弾する前に攻撃者や標的が死にうる
        // （実行は ThornGuardTrait.OnDamaged。「入れ替え → 反撃」の順）。
        UnitState? thornGuard = PickOne(foes.Where(
            f => f.HasTrait(TraitId.ThornGuard) && f != target
                 && f.RawCounter(ThornGuardTrait.PendingKey) > 0
                 && ThornGuardTrait.Covers(f, target)).ToList());

        if (thornGuard is not null)
        {
            Log($"    {thornGuard.Name} が {target.Name} の前に棘を差し出した", LogKind.Trigger);
            thornGuard.SetCounter(ThornGuardTrait.PendingKey, 0);
            // スロット + 1 を格納し、0 を「なし」とする（スロット0 と未設定の区別）
            thornGuard.SetCounter(ThornGuardTrait.PartnerKey, target.Slot + 1);
            NoteGuardPick(GuardKind.ThornGuard, thornGuard, target);   // 第120期・§2-5 の材料
            NoteConfusedGuard(attacker, thornGuard);   // 第147期（計数のみ）
            EmitIntercept(thornGuard, target, InterceptLabels.ThornGuard);   // 第125期 段1（表示専用）
            return thornGuard;
        }

        return target;
    }

    /// <summary>
    /// 狙える敵（前列 → 中列 → 全員）。<b>標的選択と断ち（<see cref="SeverTrait"/>）の
    /// 候補判定はこの1箇所を共有する。</b> 2箇所で別の集合を数えると、
    /// 「振ると決めた手番に狙う相手がいない」が起こりうる。
    ///
    /// <para>貫きは <see cref="SelectPierceEntry"/> がレーンを直接走るのでこの pool を使わない
    /// ——貫き型の駒が断ちを持っても選好は働かない（執着とまったく同じ非対称）。</para>
    /// </summary>
    public List<UnitState> TargetPool(UnitState attacker)
        => PoolOf(FoesOf(attacker));

    /// <summary>
    /// <b>攻撃の標的になりうる駒の集合（第146期に1箇所へ寄せた）。</b>
    /// 混乱（<see cref="ConfusionRule"/>）が効く<b>唯一の窓口</b>で、
    /// ここを通るのは <see cref="SelectTargetChain"/> の <c>foes</c>／<see cref="TargetPool"/>／
    /// <see cref="SecondaryTargets"/> の3つだけ。
    ///
    /// <para><see cref="ResolvePierce"/> は <c>entry.TeamId</c>（＝標的側）を見ているので
    /// <b>自動で追従する</b>——混乱した貫きは自軍のレーンで正しく解決される。
    /// 攻撃者側の選好（執着・断ち・止め）は <c>foes</c> / <c>pool</c> を受け取る側なので触らない。</para>
    ///
    /// <para><b>混乱のときだけ自分を除く。</b> 素の経路では攻撃者は相手陣営にいないので
    /// この <c>Where</c> は恒等になる——だから<b>分岐の中に入れて、既定の経路に1本も枝を増やさない</b>
    /// （軛・受け流しと同じ短絡の作法）。</para>
    /// </summary>
    internal List<UnitState> FoesOf(UnitState attacker)
    {
        if (ConfusionLive && attacker.RawCounter(StatusKeys.Confused) > 0)
            return LivingMembers(attacker.TeamId).Where(u => u != attacker).ToList();

        return LivingMembers(Opponent(attacker.TeamId)).ToList();
    }

    /// <summary>
    /// 混乱を1つ消費する（第146期）。<b>1回の攻撃で必ず落ちる。</b>
    /// 呼び口は <see cref="PerformAttack"/> の2つの出口だけ——貫きの早期リターンと、
    /// <see cref="SecondaryTargets"/> を引いた後。<b>落とすのは最後の読み手より後ろ</b>で、
    /// 手前で落とすと薙ぎ・全体の巻き込みだけが敵陣へ戻る。
    /// </summary>
    private void ConsumeConfusion(UnitState actor)
    {
        if (!ConfusionLive || actor.RawCounter(StatusKeys.Confused) == 0) return;
        actor.SetCounter(StatusKeys.Confused, 0);
        TallyOf(actor).ConfusedSwings++;
        // 第147期（表示専用）: 自軍へ振った瞬間。付与は何ターンも前でありうるので別の出来事として打つ。
        EmitConfused(actor, ConfusedLabels.Struck, null);
    }

    private static List<UnitState> PoolOf(List<UnitState> foes)
    {
        List<UnitState> pool = foes.Where(f => f.Row == Row.Front).ToList();
        if (pool.Count == 0) pool = foes.Where(f => f.Row == Row.Mid).ToList();
        if (pool.Count == 0) pool = foes;
        return pool;
    }

    /// <summary>
    /// 貫きが撃ち込むレーンを選び、その先頭（最も前の生存者）を返す。
    /// 後ろに誰かがいるレーンを優先する。「後ろに隠れる」への回答という役割を残すため。
    /// 隠れる者がいなければ、どのレーンでも構わない。
    ///
    /// <b>X字化でこの優先は編成が満席なら実質無効になった。</b>2本のレーンはどちらも
    /// 後X を終点に持つので、5体が埋まっていれば deep が常に両方を拾う。前3が
    /// 「後列に1体でも置けば貫きの対象から外れる」逃げ場だった穴を潰した結果であって、
    /// 狙いどおり。ただし「後ろに隠れるへの回答」という元の役割はここでは失われている。
    /// </summary>
    private UnitState SelectPierceEntry(List<UnitState> foes, out int lane)
    {
        lane = -1;

        var lanes = Enumerable.Range(0, FormationRules.LaneCount)
            .Where(l => foes.Any(f => FormationRules.LanesOf(f.Slot).Contains(l)))
            .ToList();

        // ○前2・○後2 はどのレーンにも属さないので、生き残りがそこだけになると
        // 走る列が無くなる。落とさずに単体として1体だけ刺す（lane = -1）。
        if (lanes.Count == 0) return foes[Roll(foes.Count)];

        var deep = lanes
            .Where(l => foes.Any(f => FormationRules.LanesOf(f.Slot).Contains(l)
                                      && f.Row != Row.Front))
            .ToList();

        List<int> pick = deep.Count > 0 ? deep : lanes;
        lane = pick[Roll(pick.Count)];

        return LaneOccupants(foes, lane)[0];
    }

    /// <summary>
    /// レーン上の生存者を前から後ろの順に並べる。
    /// 増援は死者の枠に入らなくなった（Summon 参照）ので通常は1枠1体だが、
    /// スロットの一意性は今後も前提にしないこと。ここが落ちると全戦闘が落ちる。
    /// </summary>
    private static List<UnitState> LaneOccupants(IEnumerable<UnitState> members, int lane)
    {
        var alive = members.Where(m => m.IsAlive).ToList();
        var line = new List<UnitState>();
        foreach (int slot in FormationRules.LanePath(lane))
            line.AddRange(alive.Where(u => u.Slot == slot));
        return line;
    }

    /// <summary>
    /// 一回の攻撃を最後まで解決する。通常のターン進行からも、追撃のようなターン外の割り込みからも呼ぶ。
    /// ターン順のループに攻撃処理を直書きすると、割り込み系の特性が一切書けなくなる。
    /// </summary>
    /// <param name="attackPercent">
    /// 攻撃力の倍率（百分率）。行動（<see cref="UnitAction"/>）に属する値なので、
    /// **反撃・追い打ちのような手番外の攻撃には掛からない**（呼び出し側が渡さない＝100）。
    /// </param>
    /// <param name="patternOverride">この攻撃だけ攻撃型を差し替える。null なら CurrentPattern。</param>
    /// <summary>
    /// 火勢の帳簿（第133期・<b>計数専用</b>）。<b>盤面を1ビットも動かさない</b>
    /// ——読むのは <see cref="WildfireTrait.BurningFoes"/> と攻撃力の2つだけ。
    ///
    /// <para><b>上乗せは「全部通した値 − 火勢を除いた値」で取る</b>
    /// （<c>OverbearTrait.PlainAttack</c> と同型）。<c>attackPercent</c> の割引は掛けない
    /// ——大技（<c>BigAttacks</c>）を持つ保持者はいまの所いないので、素の打点で数える。</para>
    /// </summary>
    void NoteWildfireSwing(UnitState actor)
    {
        int n = WildfireTrait.BurningFoes(this, actor);
        int gain = actor.CurrentAttack - WildfireTrait.PlainAttack(actor);
        UnitTally t = TallyOf(actor);
        t.WildfireSwings++;
        if (n > 0) t.WildfireLit++;
        t.WildfireFoes += n;
        t.WildfireFoesSq += (long)n * n;
        if (n > t.WildfireFoesMax) t.WildfireFoesMax = n;
        t.WildfireGain += gain;
        t.WildfireGainSq += (long)gain * gain;
    }

    public void PerformAttack(UnitState actor, string prefix = "  ",
                              int attackPercent = 100, AttackPattern? patternOverride = null)
    {
        if (!actor.IsAlive) return;

        AttackPattern pattern = patternOverride ?? actor.CurrentPattern;

        UnitState? target = SelectTargetCore(actor, patternOverride, out int pierceLane);
        if (target is null) return;

        // 積み過ぎ（第115期）の門の 2。**盤面には一切影響しない。**
        // ここで数えるのは「実際に振った型」なので、`patternOverride` を渡す経路
        // （貫きのレーン解決など）もそのまま正しく落ちる。
        // **規則を無効にしていても振りの総数は数える**——門2 の分母が版に依らないため。
        if (PatternReader(actor))
        {
            UnitTally rt = TallyOf(actor);
            rt.ReaderSwings++;
            if (pattern == AttackPattern.Sweep && ReaderActive) rt.ReaderSweeps++;
            if (pattern == AttackPattern.All && ReaderActive) rt.ReaderAlls++;   // 第128期（上の段）
            // **ターン頭の印が付いたターンだけを数える**——振った瞬間の `AtkBonus` で数えると、
            // ターンの途中で届いた強化のぶんだけ分子が分母を超え、空振りが負になる（第115期に踏んだ）。
            if (rt.ReaderOverTurnMark == Turn && rt.ReaderLastOverSwingTurn != Turn)
            {
                rt.ReaderLastOverSwingTurn = Turn;
                rt.ReaderOverTurnsSwung++;
            }
        }

        // 第117期。**振ったターン数**（空振り＝生きていたターン − 振ったターン）。
        // `Overload` の門と同じく**ターン単位**で数える（1ターンに2度振っても 1）。
        if (BossCensus)
        {
            UnitTally bt = TallyOf(actor);
            if (bt.BossLastSwingTurn != Turn) { bt.BossLastSwingTurn = Turn; bt.BossSwingTurns++; }
        }

        // 逸らし（第50期）。**焦点の効きは「付けた回数」ではなく「実際にそこへ振られた割合」。**
        // 標は単体攻撃にしか効かないので、分母も単体振りだけで数える。
        if (DivertActive && pattern == AttackPattern.Single) NoteDivertSwing(actor, target);

        // 第103期。**餌に吸われた手番**（＝本物の敵に当たらなかった手番）。
        // 打点は `CurrentAttack` で取る——薄刃・止めの払い直しより手前なので、
        // 「その手番が本物の敵に向いていたら出せたはずの量」の素直な代理になる。
        // **盤面には一切影響しない。**
        if (BetrayWatch && BetrayedTrait.IsFodder(target)) NoteBetrayHit(actor, target);

        // CurrentAttack 自体は変えない。AtkBonus と混ぜると会戦の境界処理（第1期 D2/D3）や
        // 墓守の層の再適用と衝突する。
        // 100 のときに分岐を残してあるのは、攻撃力 0 の駒を 0 のまま通すため
        // （素の値をそのまま返す経路が、倍率導入前と1命令も違わないことの保証にもなる）。
        int atk = attackPercent == 100
            ? actor.CurrentAttack
            : actor.CurrentAttack * attackPercent / 100;

        // 第133期・**計数専用**。火勢（`TraitId.Wildfire`）が実際に乗った振りを数える。
        // **`ModifyAttack` の中では数えない**——`CurrentAttack` は駆り立ての選択・転嫁の流し先・
        // `StatSnapshot`・棘/仇討ち/責め苦の反撃量からも読まれるので、数えると
        // 「振った回数」ではなく**「読まれた回数」**になる（驕り＝第46期の明文）。
        // **規則は1本も足していない**（判定は `WildfireTrait.ModifyAttack` の中にある）。
        // **誰も読んで分岐しない。** 保持者がいなければ比較1つで抜ける。
        if (actor.HasTrait(TraitId.Wildfire)) NoteWildfireSwing(actor);

        // 第154期・**計数専用**。重り（`TraitId.Laden`）が乗った振りを数える。
        // **`CurrentAttack` の中では数えない**——あちらは駆り立ての選択・転嫁の流し先・
        // `StatSnapshot`・棘/仇討ち/責め苦の反撃量からも読まれるので、数えると
        // 「振った回数」ではなく**「読まれた回数」**になる（第75期・第133期の明文）。
        // **既定では bool 1つで抜ける。**
        if (LadenActive) NoteLadenSwing(actor, atk);

        // 薄刃の払い方（第75期）。**規則が V0（既定）なら最初の比較1つで抜ける**ので、
        // 通常の実行では乱数も盤面も1ビットも動かない（`compare` 305 セル 0 件が検算）。
        //
        // **`Trait.ModifyAttack` には書けない**——「相手に傷があるか」は対象を見る条件で、
        // あちらは `self` しか受け取らない。止め（第53期）の倍率とまったく同じ理由・同じ場所で、
        // **`atk` を作った直後に払い直す**。`ThinBladeTrait.ModifyAttack` は常に 1 を返し続けるので、
        // `CurrentAttack` を読む他の全員（駆り立ての選択・転嫁の流し先・StatSnapshot・
        // 棘/仇討ち/責め苦の反撃量）から見たキリは版によらず攻撃力 1 のまま
        // ——**版が動かすのは「この一振りの打点」だけ**である。
        //
        // **代金は消えていない。**条件が真の側は `atk` を読まずに 1 のまま通す（第29期の設計）。
        // 主目標で判定するので薙ぎの巻き込み・貫きの後続も同じ値で解決される（キリは単体だが、
        // 型を書き換える機構＝軋みが載った場合の挙動をここで決めてある）。
        if (ThinBlade.Cost != ThinBladeCost.Always && actor.HasTrait(TraitId.ThinBlade))
        {
            bool pay = ThinBlade.Cost switch
            {
                // V1: 傷の無い相手には 1。**自分で開けた傷に自分で入る。**
                ThinBladeCost.Unwounded => !IsWounded(target),   // 第93期: 深手も「傷あり」
                // V2: 刻める攻撃だけ 1。刻めない＝この一撃で倒れる（死体には刻まない）。
                // **判定は代金を払った価格で行う予測**——実際の生死は肩代わり・上限まで行かないと
                // 決まらないので、破片が無く HP がこの打点以下のときだけ「刻めない」と読む。
                ThinBladeCost.Carving => !(target.RawCounter(StatusKeys.Armor) <= 0 && target.Hp <= atk),
                // V3: 自分より遅い相手には 1。**同速は「遅い」ではない。**
                _ => target.Def.Speed < actor.Def.Speed,
            };
            if (!pay)
            {
                int raw = ThinBladeTrait.RawAttack(actor);
                atk = attackPercent == 100 ? raw : raw * attackPercent / 100;
                if (atk < 1) atk = 1;   // 払わない側が払う側より小さくならないこと（0 は返さない）
            }
        }

        // 攻撃力を出力に変換した回数（第64期）。**死蔵の判定に使う唯一の計数**で、
        // 盤面には一切影響しない。`Attacks` との差は「振ったか」対「攻撃力を読んだか」。
        NoteAttackRead(actor);

        // 止め（第53期）。**倍率は攻撃の解決時に掛ける**——`Trait.ModifyAttack` は対象を
        // 受け取らないので「相手が標を持つか」で分岐できない（`ModifyAttack` に書くと
        // 標の無い相手にも倍率が乗り、供給とのサイクルが消えて単なる高打点の駒になる）。
        // **単体攻撃だけ**（薙ぎ・全体・貫きは engine の鎖が標を1ビットも見ない）。
        //
        // **「発火」と「列越え」を分けて数える。** 列越え＝`PoolOf` の外（中列・後列）を殴った回数で、
        // 「標が無ければ狙えなかった敵」。標が持つ特権を実際に使えたかはこちらでしか読めない。
        if (FinisherActive && pattern == AttackPattern.Single && actor.HasTrait(TraitId.Finisher))
        {
            if (target.RawCounter(StatusKeys.Marked) > 0)
            {
                atk *= Finisher.Multiplier;
                NoteFinisherFire(target, !TargetPool(actor).Contains(target));
            }
            else
            {
                NoteFinisherIdle();
            }
        }

        // 止めの代金（止めた砲火）の分母と拾い上げ。**盤面には触らない。**
        if (FinisherActive && pattern == AttackPattern.Single) NoteFinisherSwing(actor);

        string label = pattern switch
        {
            AttackPattern.Sweep => " 薙ぎ",
            AttackPattern.Pierce => " 貫き",
            AttackPattern.All => " 全体",
            _ => ""
        };
        // 手番の1回だけでなく、反撃・追い打ちのような手番外の攻撃もここを通る。
        // 「1ターンあたり何回振ったか」が、手番でしか動かない駒と反応する駒を分ける。
        TallyOf(actor).Attacks++;
        if (attackPercent > 100) TallyOf(actor).BigAttacks++;   // 大技の発火数（Attacks の内数）
        // 軋みが響く（第66期）。**分母は保持者が振った回数の全部**（手番も割り込みも）で、
        // 分子は「素の型が単体なのに薙ぎで出た」回数。規則が無効なら分子は恒等的に 0。
        if (actor.HasTrait(TraitId.Displaced) && patternOverride is null)
        {
            TallyOf(actor).CreakSwings++;
            if (actor.Def.Pattern == AttackPattern.Single && pattern == AttackPattern.Sweep)
                TallyOf(actor).CreakSweeps++;
        }
        // 燃えている状態で振った回数（第57期・Attacks の内数）。熾火の稼働率の分子。
        if (actor.RawCounter(StatusKeys.Burn) > 0) TallyOf(actor).BurnAttacks++;

        // 鱗（第47期）。**振った回数と、そのうち貫きだった回数を分けて数える。**
        // 「貫き」は成果ではないので、後列に当たった回数は ResolvePierce の側で別に数える。
        if (actor.HasTrait(TraitId.Scale))
        {
            ScaleSwings++;
            if (pattern == AttackPattern.Pierce) ScalePierceSwings++;
        }

        // 第97期・表示専用。**攻撃型と打点が化けた瞬間**を1度だけ見せ場に打つ。
        // どちらも `ModifyAttack` / `ModifyPattern` が毎回その場で評価するので、
        // 出来事としては1つも残らない（`ModifyAttack` は ctx を受け取らないので特性側では打てない）。
        if (actor.HasTrait(TraitId.Pyre) && actor.RawCounter(StatusKeys.Burn) > 0)
            HighlightOnce(actor, "pyre", $"  {actor.Name} は燃えたまま振り抜いた（攻 ×{PyreTrait.Multiplier}・貫き）");
        if (actor.HasTrait(TraitId.Sniper) && actor.HasFallenBack && actor.Row == Row.Back)
        {
            // 第129期・**計数のみ**。狙撃の成立は `PerformAttack` がその場で評価して
            // 打点と攻撃型を書き換えるだけなので、**盤面にも計数にも痕跡が残らない**
            // （見せ場は verbose が偽だと出ず、1戦に1度しか打たない）。誰も読んで分岐しない。
            TallyOf(actor).SniperSwings++;
            HighlightOnce(actor, "sniper", $"  {actor.Name} は下がりきって狙いを定めた（攻 ×2・貫き）");
        }

        Log($"{prefix}{actor.Name} → {target.Name} (攻撃 {atk}{label})");
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Attack,
            Turn = _turn,
            ActorId = actor.InstanceId,
            TargetId = target.InstanceId,
            Amount = atk,
            Pattern = pattern,
            // 第151期・表示専用。着弾側だけでなく「振る直前」にも札を載せる。
            // 再生側が追加攻撃の予告を出すには Damage まで待っていては遅い。
            Reaction = InReaction || InInterrupt
        });

        if (pattern == AttackPattern.Pierce)
        {
            ConsumeConfusion(actor);   // 第146期: 貫きの出口（ResolvePierce は entry.TeamId を見る）
            ResolvePierce(actor, pierceLane, target, atk);
            return;
        }

        int dealt = atk;
        // 呪いの共有（第96期）は**単体攻撃の一撃そのもの**にだけ札を付ける。
        // 副次目標（薙ぎ・全体）と貫きの段には付けない——範囲が二乗で伸びるのを止める構造。
        ApplyDamage(target, dealt, actor, singleHit: pattern == AttackPattern.Single, pattern: pattern);

        // 適用順を混ぜる。同じ一振りで2体以上落ちるとき、死亡順（墓守の層・破裂の連鎖）が
        // 席番号で決まっていた。巻き込む相手の顔ぶれは変わらない——順番だけ。
        var extras = SecondaryTargets(actor, target, patternOverride).ToList();
        ConsumeConfusion(actor);   // 第146期: 最後の読み手（巻き込み）を引いた後に落とす
        Shuffle(extras);
        foreach (UnitState extra in extras)
        {
            if (!extra.IsAlive) continue;
            // 積み過ぎ（第116期）の門の 3。**盤面には一切影響しない。**
            // 薙ぎに化けた一撃が実際に何体へ届いたか（＝巻き込み）。安い bool を先に見る
            // （`ReaderActive` → 型 → 保持者。既存条件の後ろに置く作法）。
            if (ReaderActive && pattern == AttackPattern.Sweep && actor.HasTrait(TraitId.Overload))
                TallyOf(actor).ReaderSplash++;
            Log($"    刃が {extra.Name} まで届く", LogKind.Damage);
            ApplyDamage(extra, Math.Max(1, dealt * SecondaryPercent / 100), actor, pattern: pattern);
        }

        // 特性の発動は攻撃1回につき1度、主目標に対してのみ。
        // 範囲攻撃のたびに巻き込みや毒が複数回発動すると、範囲持ちが即座に壊れる。
        foreach (Trait t in actor.Traits.ToList())
        {
            TraitMark m = this.BeginTrait(t.Id, actor);   // 第94期 (T2) の印
            t.OnAfterAttack(this, actor, target, dealt);
            this.EndTrait(m);
        }
    }

    /// <summary>副次目標のダメージ倍率（%）。範囲は「敵の数 × 値」で効くので、必ず割り引く。</summary>
    public const int SecondaryPercent = 60;

    /// <summary>貫きが1体貫くごとに失う威力（%）。</summary>
    public const int PierceDecayPercent = 25;

    /// <summary>
    /// 貫きを解決する。レーンを前から後ろへ走り、並んでいる敵すべてに当たる。
    /// 奥へ行くほど威力が落ちるので、レーンを厚くすることが後ろを守る手段になる。
    /// 逆に、誰も並んでいないレーンは減衰ゼロの直撃を受ける。
    /// </summary>
    /// <param name="lane">
    /// <see cref="SelectPierceEntry"/> が選んだ列。<b>entry のスロットから逆算してはいけない</b>
    /// ——中央は2本のレーンに属するので、そこが先頭になった局面で列が決まらない。
    /// -1 は「どのレーンにも属さない席（○前2・○後2）だけが残った」で、entry 1体で終わる。
    /// </param>
    private void ResolvePierce(UnitState actor, int lane, UnitState entry, int atk)
    {
        List<UnitState> line = lane < 0
            ? new List<UnitState> { entry }
            : LaneOccupants(LivingMembers(entry.TeamId), lane);

        int passed = 0;
        int primaryDealt = 0;

        foreach (UnitState u in line)
        {
            // 途中で倒れた駒はもう立ちはだかっていないので、減衰の数に入れない。
            if (!u.IsAlive) continue;

            int dmg = Math.Max(1, atk * Math.Max(0, 100 - PierceDecayPercent * passed) / 100);
            if (passed > 0)
                Log($"    刃は {u.Name} まで貫いた（威力 {dmg}）", LogKind.Damage);

            // 鱗（第47期）の**後列到達**。当たった回数と、減衰後に振り下ろした量の両方を数える
            // ——「貫いた回数」は成果ではない（後列に敵がいなければ単体攻撃と同じ）。
            // 敵側に ApplyDamage 内の肩代わり（巨躯・分かち）は1枚も無いので、
            // ここで数えた段はそのまま後列の駒に落ちる。
            if (actor.HasTrait(TraitId.Scale) && FormationRules.RowOf(u.Slot) == Row.Back)
            {
                ScaleBackHits++;
                ScaleBackDamage += dmg;
            }

            ApplyDamage(u, dmg, actor, pattern: AttackPattern.Pierce);
            if (u == entry) primaryDealt = dmg;
            passed++;
        }

        // 特性の発動は攻撃1回につき1度、レーンの先頭に対してのみ。
        // 貫いた全員に毒や巻き込みが乗ると、貫き持ちが即座に壊れる。
        foreach (Trait t in actor.Traits.ToList())
        {
            TraitMark m = this.BeginTrait(t.Id, actor);   // 第94期 (T2) の印
            t.OnAfterAttack(this, actor, entry, primaryDealt);
            this.EndTrait(m);
        }
    }

    /// <summary>主目標以外に巻き添えになる敵。</summary>
    /// <param name="patternOverride">SelectTarget と同じ。null なら CurrentPattern。</param>
    public IReadOnlyList<UnitState> SecondaryTargets(UnitState attacker, UnitState primary,
                                                    AttackPattern? patternOverride = null)
    {
        // 第146期: **ここも混乱を通す。** 主目標だけ味方・巻き込みは敵、という破れ方をする。
        List<UnitState> foes = FoesOf(attacker).Where(f => f != primary).ToList();

        return (patternOverride ?? attacker.CurrentPattern) switch
        {
            // 薙ぎは標的と同じ列 + 中列へ広がる。レーンに沿って前後へ広げると貫きと区別がつかなくなる。
            // 表は非対称（前1を薙げば中央まで届くが、中央を薙いでも前列へは戻らない）なので、
            // 標的の側から引く。前列が削れるほど薙ぎが痩せる、というのがこの形の要。
            AttackPattern.Sweep => foes
                .Where(f => FormationRules.SweepTargets(primary.Slot).Contains(f.Slot)).ToList(),
            AttackPattern.All => foes,
            _ => Array.Empty<UnitState>()
        };
    }

    /// <summary>
    /// ダメージ処理の単一窓口。味方からの巻き込みも生贄もここを通る。
    /// だから「被弾で強くなる」駒が、敵の攻撃でも味方の事故でも等しく反応する。
    /// </summary>
    /// <param name="burnTick">
    /// 燃焼の刻み（第57期）。<b>計数専用の札で、盤面の判断には一切使わない。</b>
    /// 毒の刻みと同じく <paramref name="source"/> が <c>null</c> なので、
    /// これが無いと「破片が吸ったのは毒か燃焼か」が割れない。
    /// 肩代わり（巨躯・分かち・棘守り）が分割した各段にも引き継ぐ
    /// ——分割された先で吸われた量も同じ刻みのぶんだから。
    /// </param>
    /// <param name="relayed">
    /// 肩代わり（巨躯・分かち）が<b>元のダメージを分割して中継した段</b>であることの札（第85期）。
    /// <b>計数と巻き込み則（<see cref="SpillWound"/>）の除外にだけ使い、盤面の判断には一切使わない。</b>
    /// 元の攻撃者が味方（棘の巻き込み・吸い）だと <paramref name="source"/> が同陣営になるので、
    /// 「中継は刃ではない」を <paramref name="source"/> だけでは書けない。
    /// </param>
    /// <param name="spillWound">
    /// 巻き込み則（<see cref="SpillWound"/>）をこの呼び出しで走らせるか（第93期）。
    /// <b>既定は現行の挙動（走らせる）。</b> 深手の自傷と上乗せだけが偽を渡す
    /// ——<b>深手 → 自傷 → 傷 → 深手</b>の閉じたループを構造的に作らないため（自己検査 (d)）。
    /// </param>
    /// <param name="deepBite">
    /// 深手の払い出し（第93期）であることの札。<b>計数専用で、盤面の判断には一切使わない</b>
    /// （<c>burnTick</c> と同じ扱い）。中継（巨躯・分かち）に拾われた回数を数えるためだけにある。
    /// </param>
    /// <param name="singleHit">
    /// この damage が<b>単体攻撃（<see cref="AttackPattern.Single"/>）の一撃そのもの</b>であることの札（第96期）。
    /// <b>呪いの共有（<see cref="CurseRule"/>）だけが読む。</b> 既定は偽なので、
    /// <see cref="PerformAttack"/> の単体の一振り以外の呼び出しは1つも変わらない。
    /// <para><b>肩代わり（巨躯・分かち・棘守り）で分割された段には引き継がない</b>
    /// ——<c>burnTick</c> とはここが違う。引き継ぐと同じ一撃が壁と後ろの味方の両方から共有を起こし、
    /// <b>「1ホップ」が肩代わり役の枚数だけ増える</b>（測る前に固定した判断・§2-3）。</para>
    /// </param>
    /// <param name="hexShare">
    /// 呪いの共有そのものであることの札（第96期）。<b>1ホップのガード</b>で、
    /// これが真の段は共有を起こさない（<see cref="ThornsTrait"/> の <c>InReaction</c> と同じ形）。
    /// 計数（<c>HexHopBlocked</c>）にも使う。
    /// </param>
    /// <param name="pattern">
    /// この damage を生んだ攻撃型（第97期・<b>表示専用</b>）。<see cref="BattleEvent.Pattern"/> に
    /// そのまま載るだけで、<b>どの規則も読まない</b>（<c>burnTick</c> と同じ計数専用の札の系列だが、
    /// こちらは計数にも使わない）。
    /// <para><b>攻撃型が分かる経路だけが渡す</b>——<see cref="PerformAttack"/> の主目標と副次目標、
    /// <see cref="ResolvePierce"/> の各段。反撃・状態異常の刻み・肩代わりの中継・呪いの共有は
    /// 既定の <c>null</c> のままで、再生側は「型なし」として描く。</para>
    /// </param>
    /// <param name="levy">
    /// 徴収であることの札（第118期）。<b>攻撃ではない削り</b>——生贄（開戦時に味方を削る）・
    /// 吸い（毎ターン味方から取る）・置き去りの削り——に立てる。糧（<see cref="NourishTrait"/>）が
    /// <see cref="InLevy"/> で読む<b>唯一の分岐</b>で、それ以外の規則は1つも見ない。
    /// <para><b>肩代わりの中継には引き継ぐ</b>（<c>burnTick</c> と同じ扱い）——中継は
    /// 元の削りの一部であって、途中で攻撃に変わるわけではない。</para>
    /// </param>
    public void ApplyDamage(UnitState target, int amount, UnitState? source,
                            bool isFriendlyFire = false, bool lethal = true,
                            bool burnTick = false, bool relayed = false,
                            bool spillWound = true, bool deepBite = false,
                            bool singleHit = false, bool hexShare = false,
                            AttackPattern? pattern = null, bool levy = false)
    {
        if (Probe is null)
        {
            ApplyDamageCore(target, amount, source, isFriendlyFire, lethal, burnTick, relayed, spillWound, deepBite, singleHit, hexShare, pattern, levy);
            return;
        }

        // 第94期 (T2)。**味方の刃の呼び出し口を、走らせて数える**（第85期に 6 → 10 とずれた表）。
        // 擬似キーなので `StatusKeys` にも `UnitTally.CarryKeys` にも足していない
        // ——観測子の側で名前で拾う。**既定 null なので通常の実行では走らない。**
        if (isFriendlyFire && target.IsAlive && amount > 0)
        {
            if (Mark.Owner is not null) NoteProbeWrite(target, FriendlyBladeKey, 1);
            else Probe(default, target, target, FriendlyBladeEngineKey, 1);   // 巨躯・分かちの中継
        }

        // **ここから先は engine の機構**（破片・据え・惨禍・肩代わり・巻き込み則）で、
        // 殴った特性の挙動ではない。**印を降ろす**——降ろさないと、殴っただけの駒が
        // 「傷を供給し、破片と手番を読んだ」ことになる（この期に実際に踏んだ）。
        // 中で走る特性のフックは自分で印を立て直すので、そこは正しく付く。
        TraitMark prev = Mark;
        Mark = default;
        try
        {
            ApplyDamageCore(target, amount, source, isFriendlyFire, lethal, burnTick, relayed, spillWound, deepBite, singleHit, hexShare, pattern, levy);
        }
        finally { Mark = prev; }
    }

    /// <summary>
    /// ダメージ1回ぶんの札（<see cref="Hit"/>）を立てて本体を呼ぶ（第118期）。<b>盤面は1ビットも触らない。</b>
    /// 肩代わりの中継で入れ子になるので<b>退避・復帰する</b>（<c>Mark</c> と同じ作法）。
    /// </summary>
    void ApplyDamageCore(UnitState target, int amount, UnitState? source,
                         bool isFriendlyFire, bool lethal,
                         bool burnTick, bool relayed,
                         bool spillWound, bool deepBite,
                         bool singleHit, bool hexShare,
                         AttackPattern? pattern, bool levy)
    {
        HitFrame prevHit = Hit;
        Hit = new HitFrame(levy, isFriendlyFire, relayed, pattern);
        try
        {
            ApplyDamageBody(target, amount, source, isFriendlyFire, lethal, burnTick, relayed,
                            spillWound, deepBite, singleHit, hexShare, pattern, levy);
        }
        finally { Hit = prevHit; }
    }

    /// <summary>ダメージ処理の本体。<see cref="ApplyDamageCore"/> だけが呼ぶ。</summary>
    void ApplyDamageBody(UnitState target, int amount, UnitState? source,
                         bool isFriendlyFire, bool lethal,
                         bool burnTick, bool relayed,
                         bool spillWound, bool deepBite,
                         bool singleHit, bool hexShare,
                         AttackPattern? pattern, bool levy)
    {
        if (!target.IsAlive || amount <= 0) return;

        // 棘守り（カド）の肩代わり上限。**素の入力ダメージを ThornGuardTrait.AbsorbCap で切り、
        // 超過分を守った相手へ素のまま中継する。** 惨禍・据え・散開・萎縮より前に置いてあるのは、
        // 上限が「鎧の厚み」というカド固有の性質で、味方全体にかかる増減とは独立であるべきだから
        // （UnitCatalog に書かれた敵の攻撃力の数字とそのまま突き合わせて読める。後に切ると
        // 分割の前後で増幅が二重にかかりうる）。増幅はカド側・相手側の ApplyDamage で1回ずつ乗る。
        //
        // 中継先はカドではないので肩代わりの判定に再入しない。再入の抑止は既存の
        // ctx.InInterrupt に任せる（新しい static フラグを作らない。Trait は共有シングルトン）。
        //
        // 守った相手が既に倒れている（巻き込み・毒で先に落ちた）なら中継先が無いので、
        // カドが全額を受ける＝上限なしの従来挙動に落ちる。
        if (amount > ThornGuardTrait.AbsorbCap
            && target.HasTrait(TraitId.ThornGuard)
            && target.RawCounter(ThornGuardTrait.PartnerKey) > 0)
        {
            int covered = target.RawCounter(ThornGuardTrait.PartnerKey) - 1;
            UnitState? behind = PickOne(LivingMembers(target.TeamId)
                .Where(u => u != target && u.Slot == covered).ToList());

            if (behind is null)
            {
                Log($"    {target.Name} の棘は独りで受け止めた（庇った相手はもういない）", LogKind.Trigger);
            }
            else
            {
                int overflow = amount - ThornGuardTrait.AbsorbCap;
                amount = ThornGuardTrait.AbsorbCap;
                Log($"    {target.Name} の鎧は貫かれ、{behind.Name} にも {overflow} 届いた", LogKind.Trigger);
                // 出どころは元の攻撃者のまま。中継で相手が倒れた場合、入れ替え（SwapSlots）は
                // ThornGuardTrait.OnDamaged 側の「相手が既に死んでいるならそのまま」で自然に落ちる。
                ApplyDamage(behind, overflow, source, burnTick: burnTick, levy: levy);
            }
        }

        foreach (Trait t in target.Traits)
        {
            TraitMark m = this.BeginTrait(t.Id, target);   // 第94期 (T2) の印
            amount = t.ModifyIncomingDamage(target, amount);
            this.EndTrait(m);
        }

        // 惨禍は「本人ではなく味方全体」に効くので、駒の特性ではなく盤面側で解決する。
        var teammates = LivingMembers(target.TeamId);

        // u != target ＝ 惨禍は本人には乗らない（HavocTrait のコメント参照）。
        // 「カドを名指しで除外」ではなく関係で書いてあるので、惨禍持ちが2体並べば互いに増幅し合う。
        // 第179期・**計数のみ**（灰が惨禍の増分を数えるかの対照に使う。既定では読まれない）。
        int havocExtra = 0;
        if (teammates.Any(u => u != target && u.HasTrait(TraitId.Havoc)))
        {
            havocExtra = amount * HavocTrait.Percent / 100;
            amount += havocExtra;
        }

        // 荷（BurdenTrait・第154期）: 預かりを抱えている味方は、抱えている間だけ被ダメージが増える。
        // **惨禍の直後・同じ入口の族**（据え・散開・萎縮・肩代わり・破片・身構え・軛より前）。
        //
        // **出口ではなく入口に置く。** 身構え（ササ）の上限は出口の手前にあるので、
        // ここで増やした分はそのまま**切り落とし**になって隣の味方の破片に変わる
        // ——出口に置くと切り落としが1点も増えず、この期で測りたい変換がまるごと消える（第154期 §1-2）。
        // 「殺さない／量を切る」制約（受け流し・猶予・軛）はこの後ろで効くので、
        // **この札は上限を押し戻さない**（第25期の禁止に触れない）。
        //
        // **増幅は加算**（惨禍と同じ式）。掛け算にしない（README「増幅は必ず加算にする」）。
        //
        // **既定（`WardCost.Forfeit`）では列挙の比較1つで抜ける**ので、保持者の走査も
        // カウンタの読みも1回も走らない（軛の `Cap` 判定・粛の保持者走査と同じ短絡の作法）。
        if (Ward.Cost == WardCost.Burden && Ward.BurdenPercent > 0
            && target.RawCounter(StatusKeys.Ward) > 0 && BurdenBinding(target))
        {
            int extra = amount * Ward.BurdenPercent / 100;
            if (extra > 0)
            {
                amount += extra;
                WardBurdenHits++;
                WardBurdenAdded += extra;
                TallyOf(target).WardBurdenTaken += extra;
                Log($"    {target.Name} は預かりの重さで深く傷ついた（+{extra}）", LogKind.FriendlyFire);
            }
        }

        // 据え: このターン差し出された駒は硬くなる。
        // 「動けなかった」ではなく「差し出した」を見る（Trait.SurrenderedTurn。号令と同じ判定）。
        // ハギ（追い打ち）のように最初から自分の手番を持たない型は差し出すものが無いので、
        // ここを見ないと静的なマイナスが毎ターンの −50% に化ける。
        //
        // **ログを1行出す。** 据えはロスターで唯一「無言で効く」買い手で、盤面の値にも痕跡を残さない
        // （減った後の数字しか残らない）。まどろみ（第36期）が実際に売れたかを数える窓口がここしか
        // 無いので、他の割り込みと同じように出来事として記録する。
        // 引き算は `amount -= amount * p / 100` と1点も違わない（同じ式を変数に置いただけ）。
        if (target.RawCounter(StatusKeys.IdleTurn) >= Turn
            && Trait.SurrenderedTurn(this, target)
            && teammates.Any(u => u.HasTrait(TraitId.Bulwark)))
        {
            int eased = amount * BulwarkTrait.ReductionPercent / 100;
            amount -= eased;
            Log($"    据えが差し出した {target.Name} の被弾を {eased} 抑えた", LogKind.Trigger);
        }

        // 散開: 同じ列に隣り合う味方がいない駒は硬くなる。薙ぎへの対策。
        if (teammates.Any(u => u.HasTrait(TraitId.Loose))
            && !teammates.Any(u => u != target && FormationRules.AreAdjacent(target.Slot, u.Slot)))
        {
            amount -= amount * LooseTrait.ReductionPercent / 100;
            // 第97期・表示専用。**隣が空くのは戦闘の途中**（味方が倒れる）なので、
            // 成立の瞬間が見えないと「最初から付いている装備」と区別が付かない。
            HighlightOnce(target, "loose", $"  {target.Name} の周りが空いた（散開 -{LooseTrait.ReductionPercent}%）");
        }

        // 萎縮: 火力と引き換えの被ダメージ減
        if (teammates.Any(u => u.HasTrait(TraitId.Cower)))
            amount -= amount * CowerTrait.ReductionPercent / 100;

        if (amount <= 0) return;

        // 巨躯: 自分より前の列に立つ壁が、後ろの味方への攻撃を引き受ける。
        // 標的選択（庇う・後備え）と違って damage の層なので、薙ぎ・全体・貫きの一発ずつを拾える。
        //
        // 前後の判定は DepthOf で厳密に「より前」だけを見る。同じ列は守らない
        // （横に並んでいるだけの駒を守れると、前列に3枚並べるだけで壁が3重になる）。
        // 壁自身への攻撃は自分より前に自分がいないので自然に外れ、
        // 壁が複数いても HasTrait(Colossus) で受け側を除外しているため多段の肩代わりは起きない。
        if (!target.HasTrait(TraitId.Colossus))
        {
            int targetDepth = FormationRules.DepthOf(target.Row);
            // **壁自身が出どころのダメージは肩代わりしない。** 自分で殴っておいて
            // 自分で庇うと打ち消しになる。大喰らいの吸いがここを通っていて、
            // 味方が受ける味方由来ダメージが 23〜29 → 7 まで落ちていた（＝代金が消えていた）。
            // 資格のある壁が複数いたら乱数で選ぶ（席番号の若い方に偏らせない）。
            UnitState? wall = PickOne(teammates.Where(
                u => u.HasTrait(TraitId.Colossus) && u != target && u != source
                     && FormationRules.DepthOf(u.Row) < targetDepth).ToList());

            if (wall is not null)
            {
                int blocked = amount * Colossus.Percent / 100;
                if (blocked > 0)
                {
                    amount -= blocked;
                    Log($"    {wall.Name} が {target.Name} の前に立ちはだかる", LogKind.Trigger);
                    NoteGuardPick(GuardKind.Colossus, wall, target);   // 第120期・§2-5 の材料

                    // 腹（第36期）。**吐き戻しと同じ場所・同じ量を積む**ので、
                    // 「返した先の増分」と「腹に溜まった量」が定義上ずれない。
                    // 大喰らいの吸いはここを通らない（あちらは ApplyDamage の呼び出し元）ので、
                    // 腹は「殴られた誰かを庇ったとき」にだけ増える。
                    wall.SetCounter(ColossusTrait.BellyKey,
                                    wall.RawCounter(ColossusTrait.BellyKey) + blocked);
                    TallyOf(wall).Swallowed += blocked;

                    // 吐き戻し: 飲み込んだ分を、庇った相手の力に変える。
                    // **肩代わりは価値を消さず、経路を変えるだけ**にするのが狙い。
                    // 見返りを壁自身ではなく守った相手に返すので、第19期 route の
                    // 「ナラの削り7のうち6をゴルムが食い、ムドの Rage が +3 のはずが +1 に潰れる」
                    // に出口が付く（燃料がムドへ戻る）。第21期 swap の回復の吸い込みも同じ形。
                    //
                    // **ゴルム自身は育たない。** 分かち方式（全被弾に反応）にしないこと。
                    // 前列でHP150、素の被弾が膨大なので「壁だから育つ」になって巨躯との結び付きが切れる
                    // （GuardianTrait のコメントが同じ失敗を記録している）。
                    //
                    // source が null の継続ダメージ（毒・燃焼の刻み）では返さない。
                    // 庇う（GuardianTrait.OnDamaged）が同じ除外を持っているのと同じ理由で、
                    // 刻みまで拾うと「立っているだけで育つ」になる。
                    //
                    // **強化なので SupportTargets を通す。** 支援拒否（ガルド）へは届かず隣へ漏れ、
                    // 逆しま（ウツ）に対しては強化がそのまま弱体として働く——どちらも意図した帰結。
                    //
                    // 返すのは redirect の**前**。ApplyDamage(wall, ...) で壁が倒れると
                    // 死亡トリガーが走って盤面が動くので、その前に確定させる。
                    if (Colossus.Regurgitate && source is not null)
                    {
                        // 宛先が空（隣接する生存者がいない拡散持ち）なら何も起きていない。
                        // ログも出さない——「返した」と書いてあるのに数字が動かない行になる。
                        IReadOnlyList<UnitState> back = SupportTargets(target);
                        if (back.Count > 0)
                        {
                            int gain = Math.Max(1, blocked / Colossus.DamagePerGain);
                            // 第94期 (T2) の印。**engine に本体がある機構は、その特性の名前で観測する**
                            // ——印が無いと吐き戻しが「壁を殴った側の特性」に付いてしまう。
                            TraitMark cm = BeginTrait(TraitId.Colossus, wall);
                            foreach (UnitState t in back) Whet(t, gain, WhetRoute.Regurgitate);
                            EndTrait(cm);
                            Log($"    {wall.Name} が飲み込んだ力を {target.Name} へ返した（攻撃 +{gain}）",
                                LogKind.Trigger);
                        }
                    }

                    // 深手の自傷が中継に拾われた回数（第93期 §1-2 の 3）。**仕様として残す**
                    // ——代金を誰かが肩代わりできるのは編成の選択肢。計数だけ出す。
                    if (deepBite) TallyOf(target).DeepBiteRelayed++;

                    ApplyDamage(wall, blocked, source, isFriendlyFire: true, burnTick: burnTick, relayed: true, levy: levy);
                }
            }
        }

        // 分かち: 型を問わず肩代わりする。庇うと違い、薙ぎや全体でも働く。
        // 味方同士の巻き込み（isFriendlyFire）も引き受ける。ここを除外していたとき、
        // ドハはカドの代金（敵からの被弾）だけを4割肩代わりして守り、収入源（味方への巻き込み）は
        // 満額通していた。都合のいい側だけを助ける形になっていたので条件を外した。
        // 肩代わり先が自分自身になる再帰は下の HasTrait(Sharer) で止まる。
        if (!target.HasTrait(TraitId.Sharer))
        {
            UnitState? sharer = PickOne(
                teammates.Where(u => u.HasTrait(TraitId.Sharer) && u != target).ToList());
            if (sharer is not null)
            {
                int taken = amount * SharerTrait.Percent / 100;
                if (taken > 0)
                {
                    amount -= taken;
                    Log($"    {sharer.Name} が {target.Name} の痛みを引き受けた", LogKind.Trigger);
                    if (deepBite) TallyOf(target).DeepBiteRelayed++;   // 第93期 §1-2 の 3（計数のみ）
                    ApplyDamage(sharer, taken, source, isFriendlyFire: true, burnTick: burnTick, relayed: true, levy: levy);

                    // 痛みを取り上げられた者は腕がなまる。肩代わり量に比例させているので、
                    // 代金はドハのHPという有限プールから払われる（SharerTrait.DullDivisor 参照）。
                    // 切り捨てのままにしてあるのは、Math.Max(1, ...) にすると小さいダメージの
                    // 連打で比例関係が崩れ、下げ幅が肩代わり量から切り離されるため。
                    int dull = taken / SharerTrait.DullDivisor;
                    if (dull > 0)
                    {
                        Dull(target, dull, DullRoute.Sharer, sharer);
                        Log($"    痛みを取り上げられた {target.Name} の腕がなまる（攻撃 -{dull}）",
                            LogKind.FriendlyFire);
                    }
                }
            }
        }

        // 破片（アーマー）は HP の前に削られる。
        //
        // **プールにしてあるのが要点。** 「1発を完全に吸う」形にすると、敵の一撃を
        // 超えるか超えないかで効果が二値になる（README の浄化と同じ「引き算は崖」の穴で、
        // あちらは -4 という最小の刻みで毒軸の第2波が 98% → 0% に落ちた）。
        // 超過分は素通りさせることで、崖ではなく傾斜にしてある。
        int armor = target.RawCounter(StatusKeys.Armor);
        if (armor > 0)
        {
            int soak = Math.Min(armor, amount);
            target.SetCounter(StatusKeys.Armor, armor - soak);
            amount -= soak;

            // 集約が作った鎧が実際に吸った量。**砕け（ShatterTrait）の破片と混ざる**ので、
            // Bear 持ちの駒に限って数える（診断の台に砕けを入れないことが前提）。
            // 盤面には影響しない——生成量だけを見ると第23期の吐き戻し（経路は通ったが
            // 出力に変換される前に戦闘が終わる）と同じ穴に落ちるので、吸った量を別に持つ。
            if (target.HasTrait(TraitId.Bear)) BearSoaked += soak;

            // 砕け（第137期）が配った破片が実際に吸った量。**盤面には触らない。**
            // 砕けの保持者は自分には配らないので、**保持者自身の破片は数に入らない**
            // （ヒビの `Armor` は他の供給が無ければ常に 0）。
            if (ShatterActive) ShatterSoaked += soak;

            // 第132期 段1・**計数のみ**。上限が効いている間に、破片が上限の**手前**で食った量。
            // 破片は上限の外側で効く（この行より下で切る）ので、回避経路の実測はここでしか取れない。
            if (YokeBinding) YokeArmorSoak += soak;

            // 燃焼の刻みが破片に吸われた量（第57期）。**盤面には触らない。**
            if (burnTick) TallyOf(target).BurnSoaked += soak;

            // 鱗（第47期）の**支出・被**。攻撃側の支出（ScaleSpentAttack）と分けて持つ
            // ——アーマーは被弾でも攻撃でも減る二重支出で、どちらが律速かで
            // この駒が「攻撃型」なのか「防御型」なのかが決まる。
            if (target.HasTrait(TraitId.Scale))
            {
                ScaleSpentHit += soak;
                if (armor - soak == 0) ScaleDepleted++;
                if (amount - soak <= 0) ScaleFullSoaks++;
            }

            Log($"    {target.Name} の破片が {soak} 防いだ（残り {armor - soak}）", LogKind.Trigger);

            // 破片で受け切ったなら「何も起きなかった」と扱う。被弾強化も反撃も走らせない。
            // ここを通すと、削られていない駒が削られた駒と同じ収入を得る。
            if (amount <= 0)
            {
                // 第118期・**計数のみ**。糧が「破片で受け切った被弾では発火しない」ことを
                // 実測で示すための1行で、保持者がいなければ1度も加算しない（誰も読んで分岐しない）。
                if (target.HasTrait(TraitId.Nourish)) NourishSoaked++;
                // 第143期 Q0-1 の穴（**計数のみ**）。破片が一撃を全部吸うとここで返るので、
                // `OnDamaged` が鳴らず**身構えの弾きが立たない**。**この期では塞がない。**
                // 現行の散開にも同じ穴があるので、機構の新しい欠陥ではない。
                if (Brace.Cap > 0 && target.HasTrait(TraitId.Brace)) TallyOf(target).BraceArmorMuted++;
                return;
            }
        }

        if (!lethal) amount = Math.Min(amount, Math.Max(0, target.Hp - 1));
        if (amount <= 0) return;

        // 受け流し（ParryTrait・第135期に置き、第136期 段2 に本採用）: 在庫（StockKey）が残っているあいだ、
        // 敵の一撃を丸ごと無効化する。在庫は開戦時と毎ターン頭の構えで N に戻り、庇って身に受けるたび 1 戻る
        // （どちらも特性側。engine は在庫を 1 減らすだけ——**規則は1本も足していない**）。
        //
        // **出口に置く。** 猶予・不死・軛と同じ族で、入口（ModifyIncomingDamage）だと
        // 惨禍（+50%）や脆弱（×1.5）が 0 にしたつもりの量を押し戻す（第25期・第126期）。
        // **破片・肩代わり・据え・散開・萎縮より後ろ**なので、弾くのは
        // 「それら全部を通り抜けて自分に残ったぶん」だけ。
        //
        // **敵陣から来た攻撃だけ。** 刻み（毒・燃焼）も徴収（生贄・吸い・置き去り）も
        // 味方の巻き込みも「敵の一撃」ではないので弾かない。
        //
        // **Uses <= 0 なら1ビットも動かない**（保持者の走査もしない——軛と同じ短絡の作法）。
        if (Parry.Uses > 0 && source is not null && source.TeamId != target.TeamId
            && !burnTick && !levy && !isFriendlyFire
            && target.HasTrait(TraitId.Parry)
            && target.RawCounter(ParryTrait.StockKey) > 0
            && (Parry.Scope == ParryScope.Any
                || target.RawCounter(RedirectGainTrait.PendingKey) > 0))
        {
            target.SetCounter(ParryTrait.StockKey, target.RawCounter(ParryTrait.StockKey) - 1);
            // **肩代わりの印をここで落とす。** 弾いた時点で OnDamaged が呼ばれなくなるので、
            // 落とさないと印が次の被弾まで残って毒の刻みを肩代わりと取り違える
            // （RedirectGainTrait が元から持っている懸念そのもの）。
            // 「受け流すと育たない」は仕様——受けなかった傷では強くなれない。
            target.SetCounter(RedirectGainTrait.PendingKey, 0);
            NoteParry(target, amount, pattern, source);
            if (_verbose) Emit(new BattleEvent
            {
                Kind = BattleEventKind.Parry, Turn = Turn,
                ActorId = source.InstanceId, TargetId = target.InstanceId,
                Amount = amount, HpAfter = target.Hp, Pattern = pattern,
                Reaction = InReaction || InInterrupt, Relayed = relayed
            });
            Log($"    {target.Name} が {source.Name} の一撃を受け流した（{amount} を無効化）", LogKind.Trigger);
            return;
        }

        // 猶予（ReprieveTrait・第126期）: 致死の一撃を1戦に1度だけ HP1 で止める。
        //
        // **出口に置く。** 入口（ModifyIncomingDamage）だと惨禍（+50%）や脆弱が
        // 上限を押し戻して「死なない」が守られない——軛（第25期）とまったく同じ理由で、
        // **1つ上の `lethal: false` のクランプと同じ族**（どちらも「殺さない」制約）。
        //
        // 軛より**前**に置いてあるのは、軛が更に切るだけで結果が変わらないのと、
        // 「殺さない」制約どうしを隣に並べたほうが読めるため（軛のコメントと同じ判断）。
        //
        // **保持者がいなければ1ビットも動かない。** `amount >= target.Hp` を先に見るのは
        // 特性の走査を毎回の被弾で走らせないため（軛と同じ作法。layout は数百万戦を並列で回す）。
        if (amount >= target.Hp && target.Hp > 1
            && target.HasTrait(TraitId.Reprieve)
            && target.RawCounter(ReprieveTrait.UsedKey) == 0)
        {
            target.SetCounter(ReprieveTrait.UsedKey, 1);
            amount = target.Hp - 1;
            Log($"    {target.Name} は倒れるはずの一撃を堪えた（残り 1）", LogKind.Trigger);
        }

        // 不死（UndyingTrait・第129期）: **器具であって機構ではない。**
        // 「守れたら起動するのか」を測る延命台（段2）のための札で、`UnitCatalog.All` には
        // 保持者が1枚もいない。**猶予の直後・同じ出口**に置く——`lethal: false` のクランプの
        // 一般化にすぎず、「殺さない」制約は出口にしか置けない（軛＝第25期・猶予＝第126期）。
        //
        // **ログを出さない**（毎回の被弾で出るうえ、延命台の1戦ログを読む用途が無い）。
        // `amount >= target.Hp` を先に見るのは特性の走査を毎回の被弾で走らせないため（軛と同じ作法）。
        if (amount >= target.Hp && target.HasTrait(TraitId.Undying))
            amount = Math.Max(0, target.Hp - 1);
        if (amount <= 0) return;

        // 軛（YokeTrait）: 保持者が盤上に生きている間、1回のダメージは上限で切られる。
        // **両陣営にかかる。** 非対称なのは「こちらはそのルールを知って編成を組めるが、
        // 敵は組めない」点だけ（逆位・渇きと同じ）。
        //
        // **HP を引く直前で切る。** 入口で切ると惨禍（HavocTrait +50%）や脆弱（Frail）が
        // 上限を押し戻して「1発は Cap を超えない」が守られない。増幅が無効化されているように
        // 見えるのは正しい——それがこの規則の意味。lethal: false の Hp-1 クランプより後に
        // 置いてあるのは、あちらが「殺さない」という別の制約で、順序を入れ替えると
        // 上限で切った後にもう一度切ることになるため（結果は同じだが意図が読めなくなる）。
        //
        // 破片（Armor）は**この上より前**で引かれる別資源なので、上限の外側で効く。
        // 肩代わり（巨躯・分かち・後備え・棘守り）で分割された各段はそれぞれ別の ApplyDamage
        // 呼び出しなので**段ごとに独立して切られる**——分割は上限を回避する経路になる。
        // これは意図した帰結で、「重い一撃は分けて受けろ」が肩代わり役の存在理由になる。
        //
        // amount > Cap を先に見るのは、保持者の探索（AllUnits の走査）を毎回の被弾で
        // 走らせないため。Math.Min の結果は変わらない（layout は数百万戦を並列で回す）。
        //
        // 身構え（BraceTrait・第143期）: 保持者が手番で身を固めたターンのあいだ、
        // 1回のダメージが BraceRule.Cap で切られる。**切り落とした分は捨てずに保留へ積む**
        // ——そのターンに突き飛ばした隣の味方へ破片として渡る（Trait 側の Deliver）。
        //
        // **軛の直前に置く。** 「駒の上限が先、波の上限はその後」——軛の後ろに置くと
        // 第四波では軛が 25 で先に切るので、ササの切り落としがその波だけ細る。
        // （**実測では第四波の一撃は最大 16 で軛は 1 発も切っていない**ので、この期の盤面では
        // どちらに置いても同じ値になる。順序は「切られる波が後から来たときに壊れない側」で決めてある。）
        //
        // **出口に置く理由は軛・猶予・受け流しと同じ。** 入口（Trait.ModifyIncomingDamage）だと
        // 惨禍（+50%）や脆弱が切ったつもりの量を押し戻して「1発は Cap を超えない」が守られない。
        //
        // Cap > 0 を先に見るのは、既定（0 ＝ 上限なし）で HasTrait の走査を1回も走らせないため
        // （軛の Cap 判定・粛の保持者走査と同じ短絡の作法）。
        if (Brace.Cap > 0 && amount > Brace.Cap && target.HasTrait(TraitId.Brace)
            && BraceTrait.IsBraced(this, target))
        {
            int refused = amount - Brace.Cap;
            amount = Brace.Cap;
            BraceTrait.Refuse(this, target, refused);
            Log($"    {target.Name} が身構えて一撃を {refused + Brace.Cap} から {Brace.Cap} に抑えた", LogKind.Trigger);
        }

        // **保持者の探索は `YokeBinding` に寄せた**（第132期 段1）。判定は同値
        // （`Yoke.Active && AllUnits.Any(u => u.IsAlive && u.HasTrait(TraitId.Yoke))`）で、
        // 走査の対象が全駒から保持者のキャッシュに変わっただけ。
        bool yokeBinding = amount > Yoke.Cap ? YokeBinding : false;
        if (yokeBinding)
        {
            // 第132期 段1・**計数のみ**。切る前にしか取れない量（切り落とされた量）をここで記録する。
            int pi = YokeSlot(pattern, target);
            YokeCutHits[pi]++;
            YokeCutLost[pi] += amount - Yoke.Cap;
            YokeCutPassed[pi] += Yoke.Cap;
            if (target.TeamId == PlayerTeam) { YokeCutOnPlayerHits++; YokeCutOnPlayerLost += amount - Yoke.Cap; }
            else { YokeCutOnEnemyHits++; YokeCutOnEnemyLost += amount - Yoke.Cap; }
            string who = source?.Def.Id ?? "（出どころなし）";
            YokeCutBy.TryGetValue(who, out var acc);
            YokeCutBy[who] = (acc.Hits + 1, acc.Lost + amount - Yoke.Cap);

            Log($"    軛が {target.Name} への一撃を {amount} から {Yoke.Cap} に切った", LogKind.Trigger);
            // 第171期・**表示専用**。**`amount` を書き換える前**に打つ（切り落とされた量は
            // ここでしか取れない）。`ActorId` は殴った駒——粛・渇きと違って相手が居る唯一の封じ。
            EmitSealed(target, SealedLabels.Yoke, amount - Yoke.Cap, source);
            amount = Yoke.Cap;
        }
        else if (Yoke.Cap > 0 && amount > Yoke.Cap * 4 / 5 && YokeBinding)
        {
            // 切られなかったが上限に近い一撃（上限が効いている境界を見るため）。**計数のみ。**
            YokeNearHits[YokeSlot(pattern, target)]++;
        }

        int hpBefore120 = target.Hp;   // 第120期の計数（オーバーキルを除いた実額を取るため）

        // 第132期 段1・**計数のみ**。上限が効いている間に HP へ届いた一撃を、攻撃型と入口で割る。
        // 「通った量」と「切られた量」を同じ場所で取らないと、分母が版で動いて比較できない
        // （第115期「同じ比を作る2つの計数は同じ瞬間に取ること」）。
        if (yokeBinding || YokeBinding)
        {
            int pi = YokeSlot(pattern, target);
            YokeInHits[pi]++;
            YokeInAmount[pi] += amount;
            if (amount >= hpBefore120)
            {
                YokeKills[pi]++;
                YokeOverkill[pi] += amount - hpBefore120;
            }
            if (burnTick) { YokeInBurnHits++; YokeInBurnAmount += amount; }
            else if (relayed) { YokeInRelayedHits++; YokeInRelayedAmount += amount; }
            else if (levy) { YokeInLevyHits++; YokeInLevyAmount += amount; }
        }

        target.Hp -= amount;
        // 第120期。**盤面から実際に減った HP**（過剰分を除く）。誰も読んで分岐しない。
        HpRemoved += hpBefore120 - Math.Max(0, target.Hp);
        // 燃焼の刻みが実際に削った量（第57期）。**すべての増減を通した後の値**。
        if (burnTick) TallyOf(target).BurnTaken += amount;
        // 第125期 段1。**中継の段が実際に削った量**（巨躯・分かち）。`Swallowed`（名目量）とは別物。
        // **誰も読んで分岐しない。**
        if (relayed) TallyOf(target).Shouldered += amount;
        // 第179期。**味方が味方から受けたダメージを灰として溜める**（拾い屋のスス）。
        // **HP を引いた直後・死亡判定より手前**——実額で溜め、最後の一撃も落とさない。
        // 保持者が盤上にいなければ `AshBinding` の比較1つで抜ける。
        NoteAsh(target, amount, source, havocExtra);
        Log($"    {target.Name} に {amount} ダメージ (残り {Math.Max(0, target.Hp)})",
            isFriendlyFire ? LogKind.FriendlyFire : LogKind.Damage);
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Damage,
            Turn = _turn,
            ActorId = source?.InstanceId,
            TargetId = target.InstanceId,
            Amount = amount,
            HpAfter = Math.Max(0, target.Hp),
            FriendlyFire = isFriendlyFire,
            Relayed = relayed,
            // 第97期・表示専用。線の形（薙ぎの扇・貫きの矢印）と向き（反撃は逆）を描き分けるため。
            Pattern = pattern,
            Reaction = InReaction || InInterrupt
        });

        if (source is not null && !isFriendlyFire)
        {
            DamageByUnit.TryGetValue(source.Def.Id, out int prev);
            DamageByUnit[source.Def.Id] = prev + amount;
        }

        // 与ダメージは敵と味方を分けて数える。混ぜると破裂・生贄・吸いのような
        // 「味方を削ることで仕事をする駒」が出力の大きい優等生に見えてしまう。
        NoteTurnDamage(source, amount);   // 第105期（3分割の総計。計数のみ）
        if (source is not null)
        {
            UnitTally st = TallyOf(source);
            st.Interventions++;
            if (isFriendlyFire || source.TeamId == target.TeamId) st.DamageToAlly += amount;
            else
            {
                st.DamageToEnemy += amount;
                // 第117期。**ターンごとの与ダメ**（前半3T / 後半3T は戦ごとに切り出すので、
                // 集計の側では復元できない）。既定では `BossCensus` が偽で1本も確保しない。
                if (BossCensus)
                {
                    int[] by = st.BossDmgByTurn ??= new int[BattleEngine.MaxTurns + 2];
                    if (Turn >= 0 && Turn < by.Length) by[Turn] += amount;
                }
                // 第105期。**敵への与ダメだけを手番の中／外に割る**（味方への刃は出力ではない）。
                if (InOwnTurn(source)) st.DmgOutInTurn += amount; else st.DmgOutOffTurn += amount;
                // 第109期。**尾灯が譲った手番のぶんだけを切り出す**（指示書 Q2 の分子）。
                // `Yielding` を先に見るのは、保持者が盤上にいなければ `InOwnTurn` を
                // 1回も走らせないため（軛の Cap 判定・粛の保持者走査と同じ短絡の作法）。
                // **誰も読んで分岐しない計数で、盤面には一切影響しない。**
                if (Yielding && InOwnTurn(source)) st.TaillightYieldDamage += amount;
            }
        }

        UnitTally tt = TallyOf(target);
        tt.DamageTaken += amount;
        // 第135期。**経路別の帳簿**（既定では1行も走らない）。`target.Hp <= 0` がそのまま
        // 「この一撃で倒れた」——死亡判定（`HandleDeath`）はこの数行下にあり、
        // 死ぬ経路は `ApplyDamage` のここ1箇所しかない。
        NoteHarm(target, amount, target.Hp <= 0, burnTick, levy, relayed, isFriendlyFire, pattern, source);
        // 第68期。被弾は**回数**で数える（量は DamageTaken の側。格子は回数に当てる）。
        NoteCarry(target, UnitTally.CarryHit, 1);
        // 第150期 段A。標が立っている駒への一撃（**計数専用**。継続ダメージは source が null なので外れる）。
        NoteMarkHit(target, source);
        if (source is not null && (isFriendlyFire || source.TeamId == target.TeamId))
            tt.TakenFromAlly += amount;

        foreach (Trait t in target.Traits.ToList())
        {
            TraitMark m = this.BeginTrait(t.Id, target);   // 第94期 (T2) の印
            t.OnDamaged(this, target, amount, source);
            this.EndTrait(m);
        }

        // 味方への通知。OnAllyDeath の走査と同じ形で、本人以外の生存チームメイトへ流す。
        // 破片で受け切った被弾はここより上の early return で自然に外れる。
        foreach (UnitState ally in LivingMembers(target.TeamId))
        {
            if (ally == target) continue;
            foreach (Trait t in ally.Traits.ToList())
            {
                TraitMark m = this.BeginTrait(t.Id, ally);   // 第94期 (T2) の印
                t.OnAllyDamaged(this, ally, target, amount, source);
                this.EndTrait(m);
            }
        }

        if (target.Hp <= 0)
            HandleDeath(target, source);

        // 巻き込み則（第85期・W2・SpillWoundRule）。**味方の刃**が通って対象が生きていれば傷 1。
        // 「味方の刃」＝ isFriendlyFire かつ source が同陣営。転嫁の代金・深追いの反動（source は null）はこれで外れるが、
        // **巨躯・分かちの中継は外れない**——元の刃が味方（棘の巻き込み・吸い）なら source も同陣営になるので、
        // 中継の段には札（relayed）を付けて外す（中継は肩代わりであって刃ではない。W1 の棘の書き込みと同数になることが自己検査 (c)）。
        // 燃焼の刻み（burnTick）にも書かない。数は定数 1（打点に比例させない）。
        // **HP を引いた後・死亡判定の後**に置いてあるので、削りで倒れた味方には書かれない（死体には刻まない作法）。
        // 既定（無効）では素通りする。書くのは SetCounter だけなので、乱数列も他の窓口も動かさない。
        if (SpillWound.Enabled && spillWound && isFriendlyFire && !burnTick && !relayed && source is not null
            && source.TeamId == target.TeamId && target.IsAlive && SpillWound.Writes(source))
        {
            // 第93期: 加算は**傷の窓口**を通す（束ねの入口）。既定（DeepRule 無効）では
            // `SetCounter(Wound, cur + 1)` と1ビットも違わない。
            int w = Wound(target, 1, source, WoundRoute.Spill);
            // 自己給餌（第90期 §1-2 の 4）の札。**計数専用**——`StatusKeys` ではないので帳簿
            // （`NoteCarry`）にも会戦の掃除にも載らず、誰も読んで分岐しない（`burnTick` と同じ扱い）。
            target.SetCounter(SpillWoundFromKey, source.InstanceId + 1);
            TallyOf(source).SpillWoundsWritten++;
            if (w >= 0)
                Log($"    巻き込みの傷: {source.Name} の刃が {target.Name} に残る（傷 {w}）", LogKind.Status);
        }

        // 呪いの共有（第96期・CurseRule）。**点で刺すと、繋がった駒にも届く。**
        //
        // **HP を引いた後・死亡判定の後**に置いてある。共有は「その一撃が呪い持ちに入った」
        // という出来事に対する反応なので、**その一撃で相手が倒れても届く**
        // （傷の「死体には刻まない」作法とは目的が違う——あちらは盤面に状態を書く話）。
        //
        // **share は入った量から1度だけ計算する。** 共有先のHPや防御で再計算しない
        // ——受け手ごとに割り引くと「誰に繋がっているか」ではなく「誰が硬いか」の機構になる。
        // 元の一撃を割合で分けるだけなので、**呪い3体でも総量は 2.0 倍で打ち止め（線形）。**
        //
        // 条件は4つ。**どれも構造で、係数ではない**（§0-4）:
        //   `singleHit`  薙ぎ・貫き・全体は反応しない（範囲が二乗で伸びるのを止める）
        //   `!hexShare`  1ホップ（共有が共有を呼ぶと往復する）
        //   同じ陣営だけ  陣営をまたぐと、ムドが敵を呪う目的と味方が呪われる副作用が混ざる
        //   `spillWound: false`  共有 → 傷 → 滲み の経路をこの期では作らない
        if (Curse.Enabled && singleHit && target.RawCounter(StatusKeys.Curse) > 0)
        {
            if (hexShare)
            {
                HexHopBlocked++;                     // 自己検査 (c)。**ここで止まっている**
            }
            else
            {
                HexShareHits++;
                int share = amount * Curse.SharePercent / 100;
                var others = LivingMembers(target.TeamId)
                    .Where(u => u != target && u.RawCounter(StatusKeys.Curse) > 0).ToList();
                if (others.Count == 0) HexShareDry++;
                // **紙の材料は共有量が 0 の版でも数える**（§1-2。門を数える `CurseRule(true, 0)` で
                // 「もし配っていたらいくらになったか」を出すため）。
                HexShareTargets += others.Count;
                HexShareBase += amount;
                HexShareBaseTimesTargets += (long)amount * others.Count;
                foreach (UnitState other in others)
                {
                    if (other.TeamId != target.TeamId) HexCrossTeam++;   // 自己検査 (e)。構成上 0
                    if (share <= 0) continue;
                    // 出どころは元の攻撃者のまま。味方の刃が起点なら共有も味方の刃として数える
                    // （`DamageToAlly` / `DamageToEnemy` の割り振りは陣営で決まるが、
                    //  `isFriendlyFire` はログの色と巻き込み則の判定に効く）。
                    bool ff = source is not null && source.TeamId == other.TeamId;
                    if (ff && SpillWound.Enabled && !relayed && other.IsAlive
                        && SpillWound.Writes(source!)) HexSpillSuppressed++;   // 自己検査 (f)
                    HexShares++;
                    HexShareDamage += share;
                    if (other.TeamId == PlayerTeam) { HexSharesToPlayer++; HexShareDamageToPlayer += share; }
                    if (source is not null)
                    {
                        HexShareBySource.TryGetValue(source.Def.Name, out int n0);
                        HexShareBySource[source.Def.Name] = n0 + 1;
                        HexShareDamageBySource.TryGetValue(source.Def.Name, out int a0);
                        HexShareDamageBySource[source.Def.Name] = a0 + share;
                    }
                    Log($"    呪いが {target.Name} の痛みを {other.Name} へ渡す（{share}）", LogKind.Status);
                    ApplyDamage(other, share, source, isFriendlyFire: ff,
                                burnTick: burnTick, spillWound: false,
                                singleHit: true, hexShare: true);
                }
            }
        }
        else if (Curse.Enabled && !singleHit && target.RawCounter(StatusKeys.Curse) > 0)
        {
            HexNonSingleOnCursed++;                  // 自己検査 (d)。**範囲は反応しない**
        }
    }

    /// <summary>
    /// 1体ぶんの手番（第104期に <see cref="BattleEngine.Run"/> の行動順ループから切り出した）。
    /// <b>痺れ／まどろみ／<c>CurrentAction</c>／<c>CanAct</c>／<c>PerformAttack</c>／
    /// <c>Charge</c>／<c>OnAction</c> の分岐がここに全部ある。</b>
    ///
    /// <para><b>切り出しは挙動の変更を1つも含まない</b>——元の <c>continue</c> が <c>return</c> に、
    /// ループ変数 <c>turn</c> が <see cref="Turn"/> に変わっただけ
    /// （受け入れ条件: <c>compare</c> 305 セル 0 件）。</para>
    ///
    /// <para><b>ループ側に残したのは2つの番人だけ</b>——<c>!actor.IsAlive</c> の <c>continue</c> と、
    /// 「相手チームが全滅したら <c>break</c>」。後者は<b>ループを抜ける</b>判断なので、
    /// 手番の中身ではない。</para>
    ///
    /// <para><b>ここを外から呼ぶのは再行動（<c>EncoreRule</c>・第104期）だけ。</b>
    /// 「通常攻撃をもう1回」ではなく<b>手番まるごと</b>を渡すのは、
    /// 「素の通常攻撃しか振らない駒を減らす」方針があるため——
    /// 現時点の刻み手（キリ・ノミ）は <c>Actions</c> を持たないので実質は通常攻撃1回だが、
    /// あとで <c>Actions</c> を与えたときに<b>術も溜めもそのまま乗る</b>。</para>
    /// </summary>
    /// <returns>
    /// その手番で何が起きたか（第104期に足した。<b>盤面には一切影響しない</b>——
    /// 再行動（<c>EncoreRule</c>）の Q4 の内訳を、差分ではなく直接数えるため）。
    /// </returns>
    public TurnOutcome TakeTurn(UnitState actor)
    {
        // 第105期。**中身は1文字も触っていない**——枠だけを被せて
        // 「いま誰の手番か」を立て、帰ってきた種別を数える（観測専用）。
        UnitState? prevActor = TurnActor;
        TurnActor = actor;
        UnitTally tt = TallyOf(actor);
        tt.TurnsTaken++;
        try
        {
            TurnOutcome outcome = TakeTurnCore(actor);
            switch (outcome)
            {
                case TurnOutcome.Attack: tt.TurnAttacks++; break;
                case TurnOutcome.Skill:  tt.TurnSkills++;  break;
                case TurnOutcome.Charge: tt.TurnCharges++; break;
                default:
                    tt.TurnStalls++;
                    // 「差し出した」かどうかは<b>買い手が通す判定</b>で見る（第103期の訂正）。
                    // 印を落としてログを黙らせるのは、この問い合わせが観測を汚さないため
                    // ——`CanAct` は Sluggish / Sever がログを出し、Sever は counter を読む。
                    // **乱数は1つも引かない**（CanAct の4つの実装のどれも Roll を呼ばない）。
                    TraitMark saveMark = Mark; Mark = default;
                    bool wasQuiet = _quiet; _quiet = true;
                    bool sold = Trait.SurrenderedTurn(this, actor);
                    _quiet = wasQuiet; Mark = saveMark;
                    if (sold) tt.TurnsSurrendered++;
                    break;
            }
            return outcome;
        }
        finally { TurnActor = prevActor; }
    }

    /// <summary>手番の中身（第104期に切り出した本体。第105期に枠を被せた）。</summary>
    private TurnOutcome TakeTurnCore(UnitState actor)
    {

        if (actor.RawCounter(StatusKeys.Stun) > 0)
        {
            if (ScapegoatActive) NoteScapegoatSkip(actor);
            // 第93期: 深手は**実際に行動したとき**だけ開く。止められた駒は延命する（§1 の予測）。
            if (DeepWatch) NoteDeepStalled(actor);
            actor.SetCounter(StatusKeys.Stun, 0);
            actor.SetCounter(StatusKeys.IdleTurn, Turn);
            TallyOf(actor).StallStun++;   // 第105期（計数のみ）
            // 第146期 段0（表示専用）: 手番を失った瞬間。付与は別のターンなので別の出来事として打つ。
            EmitStun(actor, StunLabels.Lost, null);
            Log($"  {actor.Name} は痺れて動けない", LogKind.Status);
            return TurnOutcome.Stalled;
        }

        // 転倒（第143期・StatusKeys.Stagger）: 身構えに突き飛ばされた味方は、次の手番を失う。
        //
        // **まどろみとまったく同じ形で立てる。** engine 側で IdleTurn を立てて Stalled を返すので
        // CanAct を1つも false にしない ＝ Trait.SurrenderedTurn が真のまま通り、
        // 号令（ガン）・据え（バン）がそのまま買い取る——**それがこの代金の狙いである。**
        // CanAct のオーバーライドで書くと不動（カド）・追い打ち（ハギ）と同じ扱いになって買い手が消える。
        //
        // **痺れを流用しない**（StatusKeys.Stagger の doc を参照）。痺れには読み手がいるので、
        // 味方の転倒がその帳簿に混ざる。
        //
        // **ターン外の行動は失わない。** IdleTurn は CanReact を1ビットも見ないので、
        // 軋み（ヨミ）の割り込みはその場で走る——落ちるのは次の通常の手番だけ。
        if (actor.RawCounter(StatusKeys.Stagger) > 0)
        {
            actor.SetCounter(StatusKeys.Stagger, 0);
            actor.SetCounter(StatusKeys.IdleTurn, Turn);
            TallyOf(actor).StallStagger++;            // 第143期（計数のみ）
            if (ScapegoatActive) NoteScapegoatSkip(actor);
            if (DeepWatch) NoteDeepStalled(actor);
            // 第145期（表示専用）: 手番を失った瞬間。付与はターン頭なので別の出来事として打つ。
            EmitStagger(actor, StaggerLabels.Lost, null);
            Log($"  {actor.Name} は転んで動けない", LogKind.Status);
            return TurnOutcome.Stalled;
        }

        // まどろみ（第36期）: 腹が満ちた壁は、その手番を失う。
        //
        // **痺れとまったく同じ形で立てる。** engine 側で IdleTurn を立てて continue するので
        // CanAct を1つも false にしない ＝ Trait.SurrenderedTurn が true のまま通り、
        // 号令（ガン・次のターンに攻撃+8）と据え（バン・そのターンの被ダメ-50%）が
        // そのまま買い取る。**CanAct のオーバーライドで書いてはいけない**——
        // 不動（カド）・追い打ち（ハギ）と同じ扱いになって買い手が消える（Trait.SurrendersTurn 参照）。
        //
        // **手番だけを失う。** 巨躯の肩代わり・吐き戻しは ApplyDamage の中、
        // 大喰らいの吸いは OnTurnStart（この行動順ループの外側）なので、どれも止まらない。
        // 眠りが壁機能を止めると、壁が眠るほど味方が削られて更に眠る自滅ループになる。
        //
        // 腹は閾値ぶんだけ引く（0 に戻さない）。溜まり過ぎた分を次の眠りへ繰り越すので、
        // 飲み込みの総量と眠りの回数が線形に結びつく（floor(飲み込み / N) 回眠る）。
        //
        // Colossus.Slumber を先に見るのは、既定（V0）で HasTrait の走査を
        // 1回も走らせないため（layout は数百万戦を並列で回す。軛の Cap 判定と同じ作法）。
        if (Colossus.Slumber && actor.HasTrait(TraitId.Colossus)
            && actor.RawCounter(ColossusTrait.BellyKey) >= Colossus.SlumberThreshold)
        {
            actor.SetCounter(ColossusTrait.BellyKey,
                actor.RawCounter(ColossusTrait.BellyKey) - Colossus.SlumberThreshold);
            actor.SetCounter(StatusKeys.IdleTurn, Turn);
            TallyOf(actor).Slumbers++;
            TallyOf(actor).StallSlumber++;           // 第105期（計数のみ）
            if (DeepWatch) NoteDeepStalled(actor);   // 第93期（計数のみ）
            Log($"  {actor.Name} は腹が満ちてまどろんだ", LogKind.Status);
            return TurnOutcome.Stalled;
        }

        // **行動種別を先に決めてから CanAct を問う。** 「動けない」には二種類あって、
        // 無力化（痺れ・のろま）は何をするのも止めるが、不動（カド）が止めているのは
        // 攻撃だけ。何をしようとしているかが分からないと、この二つを区別できない。
        // Actions を持たない駒は Attack で問われるので従来とまったく同じ答えになる。
        UnitAction? act = actor.CurrentAction;
        ActionKind kind = act?.Kind ?? ActionKind.Attack;

        // 第105期。`All` の短絡とまったく同じ回数だけ `CanActProbed` を呼びつつ、
        // **最初に否決した特性**を控える（潰れた内訳の「不動」を分けるため。計数のみ）。
        Trait? vetoed = null;
        foreach (Trait t in actor.Traits)
            if (!CanActProbed(t, actor, kind)) { vetoed = t; break; }
        bool canAct = vetoed is null;
        if (!canAct)
        {
            // 動けなかったことを記録する。ただし「差し出したターン」だけを数える。
            // 不動（カド）・追い打ち（ハギ）は最初から自分のターンに振らない型なので、
            // ここで数えると号令・据えが無償の毎ターン収入になる（Trait.SurrendersTurn 参照）。
            //
            // **種別依存で弾かれたターンは周期を進めない**（下の ActionIndex++ は
            // CanAct 通過後）。したがって `Actions` に「その駒が永久に実行できない種別」を
            // 混ぜると無限に停止する——不動の駒に `Attack` を含む `Actions` を
            // 与えてはいけない。周期がその要素で止まり、二度と先へ進まない。
            actor.SetCounter(StatusKeys.IdleTurn, Turn);
            if (vetoed!.Id == TraitId.Immobile) TallyOf(actor).StallImmobile++;   // 第105期
            else TallyOf(actor).StallCanAct++;
            if (DeepWatch) NoteDeepStalled(actor);   // 第93期（計数のみ）
            return TurnOutcome.Stalled;
        }

        // 第152期 段B。**計数のみ**（どの規則も読んで分岐しない）。構えを解いて振った手番と、
        // そのとき在庫が満タンだったかを**同じ瞬間に**数える（第115期）。
        // **既定（`ParrySwing.Off`）では `CanAct` が偽なので、ここへは1度も到達しない。**
        if (Parry.Swing != ParrySwing.Off && kind == ActionKind.Attack
            && actor.HasTrait(TraitId.Parry))
        {
            UnitTally pst = TallyOf(actor);
            pst.ParrySwings++;
            if (actor.RawCounter(ParryTrait.StockKey) >= Parry.Uses) pst.ParrySwingsFull++;
        }

        if (act is null)
        {
            SwingTurn(actor, null);   // 従来経路。Actions を持たない駒はここしか通らない
            if (DeepWatch) NoteDeepAction(actor);    // 第93期 §2-3: 実際に行動した直後
            return TurnOutcome.Attack;
        }

        // 周期を進めるのは「手番が回ってきたとき」だけ。痺れ・CanAct 偽で飛ばされた
        // ターンでは進めない。飛ばされたのは行動ではなく手番そのものなので、
        // 溜めの途中で痺れても溜めは解けず、続きから再開する。
        actor.ActionIndex++;

        if (act.Kind == ActionKind.Charge)
        {
            // **IdleTurn を立てない。** 溜めは「行動できない」ではなく
            // 「構造的に行動しない」——痺れ・鈍足と同じ扱いにすると、据え・号令が
            // 溜めを無償の毎ターン収入として拾う（上の :1015 と同じ問題が敵側で再現する）。
            EmitCharge(actor, act, actor.CurrentAction);
            TallyOf(actor).Charges++;
            Log($"  {actor.Name} は{act.Label ?? "力を溜めている"}", LogKind.Status);
            if (DeepWatch) NoteDeepAction(actor);    // 第93期 §2-3
            return TurnOutcome.Charge;
        }

        if (act.Kind == ActionKind.Skill)
        {
            // **攻撃を消費する。** 攻撃もして効果も出すなら、いつ撃つかに意味は出ない
            // （OnTurnStart を別の場所へ書き写しただけになる。第11期 Phase BB）。
            //
            // IdleTurn は立てない。振ってはいないが手番は使っているので、
            // 号令・据えが買い取る「差し出したターン」ではない（溜めと同じ扱い）。
            // 先にイベントとログを置いてから効果を流す。特性側のログが下に入って、
            // 台本でも「撃った → 何が起きた」の順に読める。
            EmitSkill(actor, act);
            Log($"  {actor.Name} は{act.Label ?? "術を使った"}", LogKind.Action);
            foreach (Trait t in actor.Traits.ToList())
            {
                TraitMark m = BeginTrait(t.Id, actor);   // 第94期 (T2) の印
                t.OnAction(this, actor, act);
                EndTrait(m);
            }
            if (DeepWatch) NoteDeepAction(actor);    // 第93期 §2-3
            return TurnOutcome.Skill;
        }

        SwingTurn(actor, act);
        if (DeepWatch) NoteDeepAction(actor);        // 第93期 §2-3
        return TurnOutcome.Attack;
    }

    /// <summary>
    /// <b>手番の攻撃を、問うた回数だけ振る（第178期）。</b>
    ///
    /// <para><b>ここが `Trait.ModifyHitCount` を問う唯一の場所である。</b>
    /// 反撃（<c>Reaction</c>）・割り込み（<c>Interrupt</c>）・追い打ち・再行動は
    /// <see cref="PerformAttack"/> を直接呼ぶので<b>1発のまま</b>
    /// ——手番の外まで増やすと、連鎖の中で二乗に伸びる。</para>
    ///
    /// <para><b>1発ずつ独立した <see cref="PerformAttack"/> を呼ぶ。</b>
    /// 標的は毎発取り直され（前の1発で倒れていれば次の相手へ）、
    /// 傷・毒・燃焼・上限（軛）・庇い・反撃も1発ごとに通常どおり走る。
    /// <b>途中で倒れたら残りは振らない</b>（カドの棘は1発ごとに返ってくる）。</para>
    ///
    /// <para><b>周期・痺れ・転倒・まどろみ・<c>IdleTurn</c> は1手番に1回のまま</b>
    /// ——どれも <see cref="TakeTurnCore"/> の頭で、この呼び出しより手前にある。</para>
    /// </summary>
    private void SwingTurn(UnitState actor, UnitAction? act)
    {
        int hits = 1;
        foreach (Trait t in actor.Traits) hits = t.ModifyHitCount(actor, hits);
        if (hits < 1) hits = 1;   // 上限は特性の側。engine が保証するのは「1発は振る」だけ

        for (int i = 0; i < hits; i++)
        {
            if (!actor.IsAlive) break;
            if (i > 0) TallyOf(actor).ExtraSwings++;   // 第178期・**計数専用**
            if (act is null) PerformAttack(actor);
            else PerformAttack(actor, attackPercent: act.AttackPercent,
                               patternOverride: act.PatternOverride);
        }
    }

    private void HandleDeath(UnitState dead, UnitState? killer)
    {
        dead.Hp = 0;
        TallyOf(dead).Deaths++;
        // 第136期・計数のみ。倒れた瞬間に受け流しの在庫が残っていたか（＝在庫切れで死んだのか、上限を素通りしたのか）。
        if (Parry.Uses > 0 && dead.HasTrait(TraitId.Parry))
            TallyOf(dead).ParryStockAtDeath += dead.RawCounter(ParryTrait.StockKey);
        // 読まれないまま落ちた傷（第85期・自己検査 (j)）。**盤面には一切影響しない。**
        TallyOf(dead).WoundsAtDeath += dead.RawCounter(StatusKeys.Wound);
        // 第150期 段A。標を持ったまま倒れた（**計数専用**）。
        NoteMarkDeath(dead, killer);
        TallyOf(dead).LastActiveTurn = _turn;   // 蘇生されて再度倒れると上書きされる（後の値が勝つ）
        // 第102期。**純粋な記録で、誰も読んで分岐しない**（盤面は1ビットも動かない）。
        // 会戦の境界の蘇生が「最後に倒れた駒」を選ぶためだけにある。
        dead.LastDeathTurn = _turn;
        if (killer is not null && killer.TeamId != dead.TeamId) TallyOf(killer).Kills++;

        Log($"    {dead.Name} は倒れた", LogKind.Death);
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Death,
            Turn = _turn,
            ActorId = killer?.InstanceId,
            TargetId = dead.InstanceId,
            Slot = dead.Slot
        });

        // 逸らし（第50期）。**撃破順が本命の指標**なので、敵の駒ごとに倒れたターンを記録する。
        // 標に依存しない切り方なので、素体の対照とそのまま引き算できる。
        if (DivertActive) NoteDivertKill(dead);

        if (dead.TeamId == EnemyTeam)
        {
            _enemyKillsThisTurn++;
            if (_enemyKillsThisTurn > MaxEnemyKillsInOneTurn) MaxEnemyKillsInOneTurn = _enemyKillsThisTurn;
        }

        // 第103期・門の 3。**餌の死で読み手が発火したか**を、連鎖の前後の差で測る。
        // 特性側には1行も足していない（読み手を1枚も作らない設計なので、engine の1箇所で数える）。
        // 既定では `BetrayWatch` が偽なので走らない。
        bool betrayDeath = BetrayWatch && BetrayedTrait.IsFodder(dead);
        int bfA = 0, bfP = 0, bfS = 0;
        if (betrayDeath)
        {
            BetrayKilled++;
            if (killer is not null) TallyOf(killer).BetrayFodderKills++;
            bfA = _units.Sum(u => TallyOf(u).Attacks);
            bfP = _units.Sum(u => u.RawCounter(StatusKeys.Poison));
            bfS = killer is not null && killer.HasTrait(TraitId.Overreach)
                ? killer.RawCounter(StatusKeys.Stun) : 0;
        }

        // 混乱の下流（第147期・**計数のみ**）。**`ConsumeConfusion` は `PerformAttack` の出口なので、
        // ここではまだ札が立っている**——立っているうちに数えないと、同士討ちが1件も残らない。
        if (killer is not null && ConfusionLive && killer.RawCounter(StatusKeys.Confused) > 0
            && killer.TeamId == dead.TeamId)
        {
            TallyOf(killer).ConfusedKills++;
            // 処刑は陣営を見ない（`ExecutionerTrait.OnKill`）ので、混乱した処刑持ちは
            // **味方を倒して育つ**。この1行がその実測（第147期 Q0-3・予測4）。
            if (killer.IsAlive && killer.HasTrait(TraitId.Executioner)) TallyOf(killer).ConfusedExecGain++;
        }

        if (killer is not null && killer.IsAlive)
            foreach (Trait t in killer.Traits.ToList())
            {
                TraitMark m = this.BeginTrait(t.Id, killer);   // 第94期 (T2) の印
                t.OnKill(this, killer, dead);
                this.EndTrait(m);
            }

        // 本人の死亡時効果（分裂など）
        foreach (Trait t in dead.Traits.ToList())
        {
            TraitMark m = this.BeginTrait(t.Id, dead);   // 第94期 (T2) の印
            t.OnDeath(this, dead);
            this.EndTrait(m);
        }

        // 敵味方を問わない死亡通知。墓守はこちらを見る。
        //
        // 第110期。V2（即時）の譲渡はこの通知の中で走るので、**入る前に「味方の振りの総数」を
        // 控えておく**——「1つの撃破で追い打ちと譲渡が両方立ったか」（指示書 Q3）は、
        // 譲渡の時点でこの値が増えているかどうかで数える。**盤面には一切影響しない**うえ、
        // 既定（V0）では `TaillightImmediate` が偽なので走査そのものが走らない。
        int tlPrevChain = TaillightImmediate ? BeginTlChain() : 0;
        foreach (UnitState u in _units.Where(u => u.IsAlive).ToList())
            foreach (Trait t in u.Traits.ToList())
            {
                TraitMark m = this.BeginTrait(t.Id, u);   // 第94期 (T2) の印
                t.OnAnyDeath(this, u, dead);
                this.EndTrait(m);
            }
        if (TaillightImmediate) EndTlChain(tlPrevChain);

        // 味方限定の通知。蘇生はこちらで、墓守が強化を得た後に走る。
        foreach (UnitState ally in LivingMembers(dead.TeamId).ToList())
        {
            if (ally == dead) continue;
            foreach (Trait t in ally.Traits.ToList())
            {
                TraitMark m = this.BeginTrait(t.Id, ally);   // 第94期 (T2) の印
                t.OnAllyDeath(this, ally, dead);
                this.EndTrait(m);
            }
        }

        if (betrayDeath)
        {
            BetrayFireAttack += Math.Max(0, _units.Sum(u => TallyOf(u).Attacks) - bfA);
            BetrayFirePoison += Math.Max(0, _units.Sum(u => u.RawCounter(StatusKeys.Poison)) - bfP);
            if (killer is not null && killer.HasTrait(TraitId.Overreach)
                && killer.RawCounter(StatusKeys.Stun) > bfS) BetrayFireOverreach++;
        }

        // 再行動（第104期・EncoreRule）。**死亡通知の固定順が全部終わってから**走らせる。
        // 既定では計数だけを取って即座に返る（`compare` 305 セル 0 件が検算）。
        NoteEncore(dead);
    }

    /// <summary>
    /// 空きスロットに増援を出す。空きが無ければ何も起きない。
    /// 空きの判定は生死を問わない。死者の枠を「空き」と見なすと、
    /// 増援がそこへ入った後に蘇生が走って1枠に2体が立つため。
    ///
    /// <para><paramref name="at"/> を渡すと<b>その席だけ</b>を見る（埋まっていれば null）。
    /// 第103期に足した経路で、<b>既存の呼び出し（<paramref name="at"/> 省略）は
    /// 1ビットも挙動が変わらない</b>——<c>FormationRules.SummonSlots</c> の走査順は
    /// 「湧いた駒が減衰1段ぶんの盾として働く」という別の調整ノブなので、
    /// 席を指定したい機構のためにそちらを書き換えることはしない
    /// （背かれ＝<see cref="BetrayedTrait"/> は ○前2 に湧かないと、
    /// 敵の前列が全滅するまで餌が食べられない）。</para>
    /// </summary>
    public UnitState? Summon(UnitDef def, int teamId, int? at = null, bool overCorpse = false,
                             UnitState? by = null)
    {
        var taken = _units.Where(u => u.TeamId == teamId).Select(u => u.Slot).ToHashSet();
        int slot = -1;
        if (at is int want)
        {
            // 席を指定する経路。**召喚枠の外は受け付けない**（編成枠を上書きしない）。
            //
            // <paramref name="overCorpse"/> が真なら<b>死者は席を塞がない</b>。
            // 既定の走査（at 省略）は今までどおり生死を問わない——あちらは
            // 「増援がそこへ入った後に蘇生が走って1枠に2体が立つ」のを避けるための規則で、
            // 蘇生されない駒（Ephemeral）を湧かせる経路には当たらない。
            // **死者と生者が同じ席に並びうるが、席を読む箇所は全部 LivingMembers で濾している**
            // （SelectTargetChain の pool ／ LaneOccupants ／ SwapSlots ／ HaulOutPair ／
            // 棘守りの被覆）ので、死体は盤面から見えない。
            bool free = overCorpse
                ? !_units.Any(u => u.TeamId == teamId && u.Slot == want && u.IsAlive)
                : !taken.Contains(want);
            if (FormationRules.IsSummonSlot(want) && free) slot = want;
        }
        else
        {
            // 召喚専用の枠だけを走る。編成枠へ入れると、5体で満席の盤面では一度も湧かない。
            // **走査順（FormationRules.SummonSlots）は調整ノブ。** 貫き経路に入る 中1・中3 から
            // 埋めるので、湧いた駒が減衰1段ぶんの盾として働く。
            foreach (int i in FormationRules.SummonSlots)
                if (!taken.Contains(i)) { slot = i; break; }
        }
        if (slot < 0) return null;

        var unit = new UnitState
        {
            Def = def,
            TeamId = teamId,
            Slot = slot,
            Hp = def.MaxHp,
            MaxHp = def.MaxHp,
            Traits = TraitCatalog.Resolve(def.Traits)
        };
        Add(unit);
        Log($"    {def.Name} が現れた", LogKind.Summon);
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Summon,
            Turn = _turn,
            ActorId = by?.InstanceId,   // 第124期 段2: 呼んだ駒
            TargetId = unit.InstanceId,
            Slot = slot,
            HpAfter = unit.Hp,
            Team = teamId,
            Text = def.Name
        });
        return unit;
    }

    /// <summary>倒れた駒を戦線に戻す。無制限にすると壊れるので回数制限は特性側で持つこと。</summary>
    public void Revive(UnitState target, int hp, UnitState? by = null)
    {
        if (target.IsAlive) return;
        if (BetrayWatch && BetrayedTrait.IsFodder(target)) BetrayRevived++;   // 第103期・自己検査 (g)
        target.Hp = Math.Max(1, hp);
        target.ResetAtkBonus();   // 第68期: 帳簿に載せずに戻す（負→0 を上昇として数えないため）
        // 第67期。配られた力が消える場所で「押された累計」も一緒に消す（寿命を AtkBonus に揃える）。
        target.WhetReceived = 0;
        Log($"    {target.Name} が繋ぎ直された（HP {target.Hp}）", LogKind.Summon);
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Revive,
            Turn = _turn,
            ActorId = by?.InstanceId,   // 第124期 段2: 蘇生させた駒
            TargetId = target.InstanceId,
            Slot = target.Slot,
            HpAfter = target.Hp
        });
    }

    /// <summary>
    /// 攻撃力を下げる唯一の窓口。<b><c>AtkBonus</c> を直接引かないこと。</b>
    /// 集約（引き受け・<see cref="TraitId.Bear"/>）の横取りはここに立っている。
    /// 回復に対する <see cref="Heal"/> と同じ位置づけで、渇きが <c>Heal</c> の入口に
    /// 1つ立っているのと同じ形。
    ///
    /// <para><b>やることは2つだけ。</b> (1) 対象に隣接する生存味方に集約役がいれば
    /// <paramref name="amount"/> をそちらへ移す。(2) 最終的な受け手の <c>AtkBonus</c> から引く。</para>
    ///
    /// <para><b>やらないこと（意図的）:</b></para>
    /// <list type="bullet">
    ///   <item><b><c>AcceptsSupport</c> の判定を入れない。</b> 5経路で扱いが揃っていない
    ///   （なまりは無検査／呪詛の敵側と突き返しは自前で弾く／呪詛の漏れと萎縮は
    ///   <see cref="SupportTargets"/> を通す）。ここで統一すると既存45行が動く。
    ///   判定は呼び出し側に残したまま、<c>AtkBonus -=</c> の行だけを差し替えてある</item>
    ///   <item><b><c>OnDebuffed</c> のようなフックを作らない。</b> 読み手が2枚
    ///   （逆しま・引き受け）になっただけでイベントを作るのは早い</item>
    ///   <item><b>ログの文言を変えない。</b> 各経路のログは呼び出し側に残す
    ///   （「腕がなまる」「勢い余って体勢まで崩れた」は駒の個性であって窓口の責務ではない）</item>
    /// </list>
    ///
    /// <para><b>5経路すべてを通す</b>——敵への呪詛も含む。集約は両陣営に等しく書けるべきで、
    /// 将来敵側に集約役を置くときに窓口が分かれていると監査が二重になる。
    /// 墓守の層の減衰（<c>desired - applied</c> が負になる）は<b>通していない</b>——
    /// あれは自分で積んだ自分のボーナスの引き直しで、弱体化ではない。</para>
    ///
    /// <para><paramref name="route"/> は診断（<c>dull</c>）が経路別に数えるためだけの札で、
    /// <b>盤面には一切影響しない</b>（<c>ShoveFired</c> 等と同じ扱いで verbose に依存しない）。
    /// 経路をログの文字列から数え直すこともできるが、開戦時1回の3経路（呪詛×2・萎縮）は
    /// 1行にまとめて出るので延べ体数が復元できない。</para>
    /// </summary>
    public void Dull(UnitState target, int amount, DullRoute route = DullRoute.Other,
                     UnitState? by = null)
    {
        if (amount <= 0) return;

        // 滲み則のなまり（第95期に `CurseRule` として作り、第96期 (R1) で `SoakRule` へ畳んだ）。
        // **なまりは、相手が背負っている汚れの種類だけ重くなる。**
        //
        // **横取りより前**に置く——ここで増やさないと、なまりの読み手3枚
        // （逆しま・引き受け・渡し）が増えた量を読めず、
        // 「既存駒が1文も増やさずに読み手になる」という設計そのものが成立しない。
        // **`DullTotal` / `DullByRoute` / `NoteCarry` も増えた量を数える**
        // ——呪いは弱体の別勘定ではなく、弱体そのものが重くなる形だから。
        //
        // **`target` の種類を数える**（`receiver` ではない）。横取りは「誰が払うか」を変える機構で、
        // 呪いが読むのは「誰が汚れているか」のほう。分けないと、集約役の隣にいるだけで
        // 呪いが宛先の汚れを読むことになって、第63期の「移す機構は読み手にとって奪う機構」を
        // 呪いの側でもう一度作ることになる。
        if (Soak.DullPerKind > 0)
        {
            int kinds = 0;
            foreach (string k in SoakRule.Kinds) if (target.RawCounter(k) > 0) kinds++;
            if (kinds > 0)
            {
                int add = kinds * Soak.DullPerKind;
                amount += add;
                SoakDullFired++;
                SoakDullKinds += kinds;
                SoakDullAdded += add;
            }
            else SoakDullDry++;
        }

        DullTotal += amount;
        DullByRoute[(int)route] += amount;

        UnitState receiver = target;

        // 横取り。**対象が横取り役（集約・渡し）自身なら横取りしない**（再帰を作らない）。
        // **対象が横取り役でも横取りしない**（分かちが !HasTrait(Sharer) で止めているのと同じ形。
        // 横取り役どうしが押し付け合う経路を消す）。
        //
        // **候補プールは集約と渡しで共有する**（第43期）。優先順位を固定すると
        // 片方が構造的に飢える（第41期「先に来る供給源だけが使われる」と同じ形）ので、
        // 両方を1つのプールに入れて PickOne に任せる。**同席する編成が現状ゼロなので
        // 既存47行の乱数列は動かない**（候補が 0/1 個では Roll を消費しない）。
        //
        // **横流し（第63期・V3）も同じプールに入る**（`FunnelRule.Both` が true のときだけ）。
        // ただし1点だけ非対称で、**横流し役自身に来た弱体は横流し役自身が起点になる**
        // ——集約・渡しは「自分の分は横取りしない」（自分に溜める駒だから）のに対し、
        // 横流しは「**自分の手元には残らない**」が規則の本体なので、`Whet` 側と同じ形にする。
        if (FunnelActive && Funnel.Both && target.HasTrait(TraitId.Funnel))
        {
            if (FunnelThrough(target, target, amount, (int)route, whet: false, ref receiver))
            {
                DullTakenByRoute[(int)route] += amount;
            }
            else
            {
                BearPassed += amount;
            }
        }
        else if (!target.HasTrait(TraitId.Bear) && !target.HasTrait(TraitId.Relay))
        {
            UnitState? taker = PickOne(
                LivingMembers(target.TeamId)
                    .Where(u => u != target
                                && (u.HasTrait(TraitId.Bear) || (u.HasTrait(TraitId.Relay) && !InRelay)
                                    || (u.HasTrait(TraitId.Funnel) && Funnel.Both))
                                && FormationRules.AreAdjacent(u.Slot, target.Slot))
                    .ToList());
            if (taker is not null && taker.HasTrait(TraitId.Funnel)
                && !taker.HasTrait(TraitId.Bear) && !taker.HasTrait(TraitId.Relay))
            {
                // 横流し（第63期）。**量は移さない——宛先だけを差し替える**（集約のように
                // 資産へ変換もしないし、転嫁のように陣営も変えない）。
                if (FunnelThrough(taker, target, amount, (int)route, whet: false, ref receiver))
                {
                    DullTakenByRoute[(int)route] += amount;
                }
                else
                {
                    BearPassed += amount;
                }
            }
            else if (taker is not null && taker.HasTrait(TraitId.Bear))
            {
                receiver = taker;
                BearTaken += amount;
                DullTakenByRoute[(int)route] += amount;
                BearFrom[target.Name] = BearFrom.TryGetValue(target.Name, out int prev) ? prev + amount : amount;

                int armor = amount * Bear.ArmorPerDull;
                if (armor > 0)
                {
                    TraitMark bm = BeginTrait(TraitId.Bear, taker);   // 第94期 (T2) の印
                    taker.SetCounter(StatusKeys.Armor, taker.RawCounter(StatusKeys.Armor) + armor);
                    EndTrait(bm);
                    BearArmor += armor;
                    // 鱗（第47期）の獲得の3本目の経路。現行の台では集約と同席しないので 0。
                    if (taker.HasTrait(TraitId.Scale)) NoteScaleGain(armor, ScaleSource.Bear);
                }
                Log($"    {taker.Name} が {target.Name} の重荷を引き受けた（攻撃 -{amount} / 鎧 +{armor}）",
                    LogKind.Trigger);
            }
            else if (taker is not null)
            {
                // 渡し（第43期）。**味方側の AtkBonus は誰も引かない**——量はそのまま
                // 敵陣へ移る。転嫁を先に済ませてから代金を払うのは、代金で渡し役が
                // 倒れても転嫁そのものは成立させるため（巨躯の吐き戻しが redirect の前に
                // 確定させているのと同じ順序）。
                DullTakenByRoute[(int)route] += amount;
                TraitMark rm = BeginTrait(TraitId.Relay, taker);      // 第94期 (T2) の印
                RelayThrough(taker, target, amount);
                EndTrait(rm);
                return;
            }
            else
            {
                BearPassed += amount;
            }
        }
        else
        {
            BearPassed += amount;
        }

        // 崖の検算（第44期）。CurrentAttack は 0 で底を打つが AtkBonus は打たないので、
        // 「0 になった瞬間」はここでしか観測できない（後から差分を取ると沈んだ量に埋もれる）。
        int atkBefore = receiver.CurrentAttack;
        // 駒ごとの受取量（第56期）。Whet の側と対にして収支（Whetted - Dulled）を引くために足した。
        // **実際に AtkBonus が減った駒にだけ載る**——転嫁（RelayThrough）は上で return しており、
        // 敵側で減る分は再帰した Dull がその受け手に載せる。**盤面には一切影響しない。**
        TallyOf(receiver).Dulled += amount;
        // 第68期。**実際に AtkBonus が減った駒にだけ載る**（Dulled と同じ行・同じ条件）。
        NoteCarry(receiver, UnitTally.CarryDull, amount);
        // 火選り（第58期）の受け手の内訳。**横取りが宛先を書き換えた後の実際の受け手**に載せる。
        // 盤面には一切影響しない（札で引くだけ・verbose 非依存）。
        if (route == DullRoute.Favor) NoteFavorReceiver(receiver, amount, whet: false);
        // 第97期・表示専用。**実際に AtkBonus が減った駒**に出す（横取りが宛先を書き換えた後）。
        // **第124期 段2: 書き手を受け取るようにした**（`Whet` / `Wound` / `Poison` と同じ形）。
        // 渡していない呼び出し元からは今までどおり null が載る。
        EmitStatusGain(receiver, DullKey, amount, by);
        receiver.AtkBonus -= amount;
        if (atkBefore > 0 && receiver.CurrentAttack == 0)
        {
            DullZeroed++;
            DullZeroedWho[receiver.Name] =
                DullZeroedWho.TryGetValue(receiver.Name, out int z) ? z + 1 : 1;
        }
    }

    /// <summary>
    /// 渡し（<see cref="RelayTrait"/>）の転嫁と代金。<see cref="Dull"/> からのみ呼ばれる。
    ///
    /// <para>流し先は<b>敵陣で <c>CurrentAttack</c> が最も高い生存駒</b>で、決定的に選ぶ
    /// （同値が並んだときだけ <c>PickOne</c>）。<b>敵が全滅していたら転嫁は起きないが、
    /// 横取りは成立させて弱体をそのまま捨てる</b>——横取りを取り消すと
    /// 「最後の1体を倒した瞬間だけ味方の腕が落ちる」という読めない挙動になる。
    /// 代金も同じ理由で払う（通り道になった事実は敵の生死で変わらない）。</para>
    ///
    /// <para>転嫁は <see cref="Dull"/> を再び呼ぶので <see cref="Relaying"/> で包む。
    /// 現状の敵ロスターに渡し持ちはいないが、置いた瞬間に無限往復する。</para>
    /// </summary>
    private void RelayThrough(UnitState relayer, UnitState target, int amount)
    {
        RelayTaken += amount;
        RelayFrom[target.Name] = RelayFrom.TryGetValue(target.Name, out int prev) ? prev + amount : amount;

        int sent = amount * Relay.TransferPercent / 100;
        if (sent > 0)
        {
            IReadOnlyList<UnitState> foes = LivingMembers(Opponent(relayer.TeamId));
            if (foes.Count > 0)
            {
                int top = foes.Max(u => u.CurrentAttack);
                UnitState? victim = PickOne(foes.Where(u => u.CurrentAttack == top).ToList());
                if (victim is not null)
                {
                    RelaySent += sent;
                    RelayTo[victim.Name] = RelayTo.TryGetValue(victim.Name, out int t) ? t + sent : sent;
                    if (sent > RelayMaxSent) RelayMaxSent = sent;

                    int before = victim.CurrentAttack;
                    Relaying(() => Dull(victim, sent, DullRoute.Relay, relayer));
                    if (before > 0 && victim.CurrentAttack == 0) RelayZeroed++;

                    Log($"    {relayer.Name} が {target.Name} の重荷を {victim.Name} へ渡した（攻撃 -{sent}）",
                        LogKind.Trigger);
                }
            }
        }

        int cost = amount * RelayTrait.HpCostPerDull;
        if (cost > 0)
        {
            // **自弁率は tally の差分で取る。** 肩代わり5種が割り込むと代金は他人へ移るので、
            // 「渡した量 × 2」を払ったことにはならない。
            int paidBefore = TallyOf(relayer).DamageTaken;
            RelayCost += cost;
            ApplyDamage(relayer, cost, null, isFriendlyFire: true);
            RelaySelfPaid += TallyOf(relayer).DamageTaken - paidBefore;
        }
    }

    /// <summary>
    /// 攻撃力を上げる唯一の窓口（<b>他者強化のみ</b>）。<b><c>AtkBonus</c> を直接足さないこと。</b>
    /// <see cref="Dull"/> の対義で、コード上で一対に読めるように名前を <c>Whet</c>（研ぐ）にしてある。
    ///
    /// <para><b>やることは2つだけ</b>（<see cref="Dull"/> と対称）。(1) 集計を更新する。
    /// (2) 受け手の <c>AtkBonus</c> に <paramref name="amount"/> を加算する。</para>
    ///
    /// <para><b>やらないこと（意図的）:</b></para>
    /// <list type="bullet">
    ///   <item><b><c>AcceptsSupport</c> の判定を入れない。</b> 6経路で扱いが揃っていない
    ///   （駆り立て・縛め・移り木は自前で弾く／号令2本と吐き戻しは <see cref="SupportTargets"/> を
    ///   通して隣へ漏らす）。ここで統一すると既存56行が動く。判定は呼び出し側に残したまま、
    ///   <c>AtkBonus +=</c> の行だけを差し替えてある——<b><see cref="Dull"/> と同じ判断</b></item>
    ///   <item><b>横取りの仕組みを作らない。</b> 強化の読み手は逆しま（ウツ）1枚しかいないので、
    ///   ウケ・ワタに相当する駒はこの期では作らない。<b>ただし将来そこに立てられる位置は空けてある</b>
    ///   （下の <c>receiver</c> のところが <see cref="Dull"/> で横取りが立っている位置）</item>
    ///   <item><b><c>OnEmpowered</c> のようなフックを作らない。</b> 読み手が1枚の段階で
    ///   イベントを作るのは早い（<see cref="Dull"/> が2枚でもまだ作っていない）</item>
    ///   <item><b>ログの文言を変えない。</b> 各経路のログは呼び出し側に残す
    ///   （「鬨を上げた」「縛りつけた」「飲み込んだ力を返した」は駒の個性であって窓口の責務ではない）</item>
    /// </list>
    ///
    /// <para><b>自己強化の9本は通していない</b>（怒り・庇う／殉教・墓守2本・処刑・棘・澱み喰い・
    /// 軋み・分かち）。窓口は将来の横取りの立ち位置になるので、<b>「自分の被弾で自分が強くなる」を
    /// 他人が横取りできる形にしてはいけない</b>——第42期の <see cref="Dull"/> が通した5経路も
    /// すべて他者への弱体で、自傷型は1本も無かった。<b>意図的な非対称</b>である。
    /// 墓守の層の引き直し（<c>desired - applied</c>）を通していないのも
    /// <see cref="Dull"/> 側と揃えた扱い（自分で積んだ自分のボーナスの再計算で、強化ではない）。</para>
    ///
    /// <para><b><see cref="Dull"/> と統合しない</b>（負の値を渡せる1本の関数にしない）。
    /// 1つの関数が符号で挙動を変えると、<b>横取りの立ち位置が2つの意味を持つ</b>
    /// ——同じ行にウケ（弱体を資産に変える）とその強化版が同居することになり、
    /// どちらが走ったかを呼び出し側の符号から逆算する羽目になる。</para>
    ///
    /// <para><paramref name="route"/> は診断（<c>whet</c>）が経路別に数えるためだけの札で、
    /// <b>盤面には一切影響しない</b>（<see cref="DullRoute"/> と同じ扱いで <c>verbose</c> に依存しない）。</para>
    /// </summary>
    public void Whet(UnitState target, int amount, WhetRoute route = WhetRoute.Other)
    {
        if (amount <= 0) return;

        WhetTotal += amount;
        WhetByRoute[(int)route] += amount;

        // 横流し（第62期）。**Dull が集約（ウケ）と転嫁（ワタ）を置いているのと同じ位置・同じ形。**
        // 第56期がここに空けておいた席にそのまま立っている——**engine に新しい窓口は足していない。**
        //
        // 規則は1文:「**自分と隣の味方に来た強化を、すべて自分の隣で一番遅い味方へ回す**」。
        //   1. 対象自身が横流し役なら、その本人が起点（**自分に来た分も回す＝自分は育たない**）
        //   2. そうでなければ、対象に隣接する横流し役を探す（複数なら PickOne）
        //   3. 宛先は「横流し役に隣接する生存味方のうち、AcceptsSupport を通り、
        //      **横流し役でない**（＝1ホップで止める）者の中で Def.Speed が最小（同速のみ PickOne）」
        //   4. 宛先が元の対象と同じなら何もしない（消えない）
        //
        // **量は加算のまま**——減衰も倍率も付けない。「行き先が本体」という主題を係数で薄めない。
        // **候補 0 / 1 個では Roll を消費しない**（PickOne）ので、**横流し役が盤上に1枚もいない行では
        // 乱数列が1ビットも動かない**（第43期の渡しと同じ受け入れ基準）。
        UnitState receiver = target;

        if (FunnelActive)
        {
            UnitState? funnel = target.HasTrait(TraitId.Funnel)
                ? target
                : PickOne(LivingMembers(target.TeamId)
                          .Where(u => u != target
                                      && u.HasTrait(TraitId.Funnel)
                                      && FormationRules.AreAdjacent(u.Slot, target.Slot))
                          .ToList());

            if (funnel is not null)
                FunnelThrough(funnel, target, amount, (int)route, whet: true, ref receiver);
        }

        // 崖の検算（Dull の DullZeroed の裏返し）。逆しまは AtkBonus の**符号だけ**を読み、
        // 負なら3倍・正なら半減なので、「半減側へ落ちた瞬間」はここでしか観測できない。
        bool perverse = receiver.HasTrait(TraitId.Perverse);
        int bonusBefore = receiver.AtkBonus;

        UnitTally rt = TallyOf(receiver);
        rt.Whetted += amount;
        // 火選り（第58期）の受け手の内訳。強化側にはまだ横取りが無いので receiver == target だが、
        // 立ち位置は Dull と揃えてある（横取りができたらそのまま正しく数える）。
        if (route == WhetRoute.Favor) NoteFavorReceiver(receiver, amount, whet: true);

        // 到着の時刻と、その後使われたか（第65期）。**誰も読んで分岐しない。**
        // 落とした経路（WhetBlock）でも数える——「その経路がいつどこへ届くはずだったか」は
        // 落とした版でも変わらず読めたほうがよい（合否は盤面の側で取る）。
        WhetTurnSumByRoute[(int)route] += amount * Turn;
        if (WhetFirstTurnCountByRoute[(int)route] == 0)
        {
            WhetFirstTurnCountByRoute[(int)route] = 1;
            WhetFirstTurnSumByRoute[(int)route] = Turn;
        }
        Dictionary<string, int> to = WhetToByRoute[(int)route];
        to[receiver.Def.Id] = to.TryGetValue(receiver.Def.Id, out int had) ? had + amount : amount;
        rt.WhetTurnSum += amount * Turn;
        if (rt.WhetFirstTurn == 0) rt.WhetFirstTurn = Math.Max(1, Turn);
        (rt.WhetPendingByRoute ??= new int[WhetRoutes.Count])[(int)route] += amount;

        // **落とすのはこの1行だけ**（第65期）。計数も横流しも乱数の消費も一切変えない。
        // 第67期: 「外から押された累計」（UnitState.WhetReceived）も**同じ行・同じ条件**で積む。
        // 落とした経路は届いていないので数えない——帳簿ではなく実際に届いた力の累計である。
        if (!WhetBlock.Blocks(route))
        {
            receiver.AtkBonus += amount;
            receiver.WhetReceived += amount;
            // 第68期。**`WhetReceived` と同じ行・同じ条件**で外から届いた量の帳簿にも積む。
            NoteCarry(receiver, UnitTally.CarryWhet, amount);
        }

        // 軋み（第66期）。**外の供給が同じ AtkBonus に積まれる**ことの記録で、盤面には触らない。
        NoteCreakBonus(receiver, amount, selfGain: false, regurgitate: route == WhetRoute.Regurgitate);

        if (perverse)
        {
            WhetToPerverse += amount;
            if (bonusBefore <= 0 && receiver.AtkBonus > 0) WhetPerverseFlips++;
        }
    }

    /// <summary>
    /// 横流し（<see cref="TraitId.Funnel"/>）の宛先の差し替え。<see cref="Whet"/> と
    /// <see cref="Dull"/> の両方から呼ばれる<b>唯一の実装</b>で、V1（強化だけ）と V3（両方）で
    /// <b>選び方が1ビットも違わない</b>ことをコードの形で担保する。
    ///
    /// <para>宛先は「<paramref name="funnel"/> に隣接する生存味方のうち、
    /// <see cref="UnitState.AcceptsSupport"/> を通り、<b>横流し役でない</b>者の中で
    /// <c>Def.Speed</c> が最小（<see cref="FunnelRule.Slowest"/> が偽なら最大）」。
    /// 同速が並んだときだけ <see cref="PickOne"/> ——<b>候補 0 / 1 個では <c>Roll</c> を消費しない</b>ので、
    /// 横流し役が盤上にいない行の乱数列は1ビットも動かない。</para>
    ///
    /// <para><b>横流し役を候補から除くことで1ホップに止める</b>（回した先がさらに回さない）。
    /// <b><c>AcceptsSupport</c> は自前で濾す（隣へ漏らさない）</b>——
    /// <see cref="SupportTargets"/> を通すと流した先が「一番遅い隣」とは限らず、規則そのものが破れる
    /// （第58期の火選りと同じ判断）。<b>強化側と弱体側で濾し方を分けない。</b></para>
    ///
    /// <para>戻り値は<b>宛先を差し替えたか</b>。<c>false</c> は「隣に流せる相手がいなかった」
    /// （候補が空、または一番遅い隣が元の対象自身）で、そのとき量はそのまま元の対象に入る
    /// ——<b>強化も弱体も消さない</b>（消える挙動のほうが読めない・第62期 §11-4）。</para>
    /// </summary>
    private bool FunnelThrough(UnitState funnel, UnitState target, int amount, int route,
                               bool whet, ref UnitState receiver)
    {
        if (!funnel.IsAlive) return false;

        var cands = LivingMembers(funnel.TeamId)
            .Where(u => u != funnel
                        && u.AcceptsSupport
                        && !u.HasTrait(TraitId.Funnel)
                        && FormationRules.AreAdjacent(funnel.Slot, u.Slot))
            .ToList();
        if (cands.Count == 0) return false;

        int want = Funnel.Slowest ? cands.Min(u => u.Def.Speed) : cands.Max(u => u.Def.Speed);
        UnitState? dest = PickOne(cands.Where(u => u.Def.Speed == want).ToList());
        if (dest is null || dest == target) return false;

        receiver = dest;
        if (whet)
        {
            FunnelTaken += amount;
            FunnelByRoute[route] += amount;
            FunnelFrom[target.Def.Id] =
                FunnelFrom.TryGetValue(target.Def.Id, out int f) ? f + amount : amount;
            FunnelTo[dest.Def.Id] =
                FunnelTo.TryGetValue(dest.Def.Id, out int t) ? t + amount : amount;
            Log($"    {funnel.Name} が {target.Name} の取り分を {dest.Name} へ回した（攻撃 +{amount}）",
                LogKind.Trigger);
        }
        else
        {
            FunnelDullTaken += amount;
            FunnelDullByRoute[route] += amount;
            FunnelDullFrom[target.Def.Id] =
                FunnelDullFrom.TryGetValue(target.Def.Id, out int f) ? f + amount : amount;
            FunnelDullTo[dest.Def.Id] =
                FunnelDullTo.TryGetValue(dest.Def.Id, out int t) ? t + amount : amount;
            Log($"    {funnel.Name} が {target.Name} の負担を {dest.Name} へ押し付けた（攻撃 -{amount}）",
                LogKind.Trigger);
        }
        return true;
    }

    public void Heal(UnitState target, int amount, UnitState? by = null)
    {
        if (!target.IsAlive || amount <= 0) return;
        if (!target.AcceptsSupport)
        {
            // 第135期の計数。**支援拒否（Stoic）が弾いた回復**（指示書 Q0-5）。
            // 盤面は1ビットも動かない——弾く判断そのものは第22期から変わっていない。
            if (HarmCensus)
            {
                UnitTally bt = TallyOf(target);
                bt.StoicHealBlocked += amount;
                bt.StoicHealBlockedFires++;
            }
            return;
        }

        // 渇き（DroughtTrait）: 保持者が盤上に生きている間、回復は一切通らない。
        // **両陣営にかかる。** ここ1箇所で止めれば足りるのは、ここが回復の単一窓口だから
        // ——継ぎ当て・施し・毒喰らい・移り木・置き去りのすべてがこの入口を通る。
        //
        // **止めないもの（意図的）:**
        //   蘇生（Revive）    Hp を直接書くのでこの窓口を通らない。**死軸には無風のまま**
        //                     ——狙いは持続回復軸への課税で、死軸まで巻き込むと分離が粗くなる
        //   破片（Armor）     ApplyDamage の側で消費されるプールで、回復とは別資源。
        //                     「誰の助けも届かない駒に唯一届く支援」がこの波でもう一段強くなる
        //   攻撃力の強化      回復ではない。号令・鬨・縛めは通常どおり
        //
        // ノノ（MenderTrait）は ctx.Heal の後に self.Hp -= amount を無条件で走らせるので、
        // 渇き下では**一方的に減る**。これは意図した挙動（回復役を連れてきた代金だけが残る）。
        //
        // **第134期 段2**: 判定は `_droughtHolders`（`Add` が積む）に寄せた。
        // `AllUnits.Any(u => u.IsAlive && u.HasTrait(TraitId.Drought))` と**同値**で、
        // 足したのは `NoteDroughtBlocked`（計数専用）だけ。
        if (DroughtBinding)
        {
            NoteDroughtBlocked(target, amount);
            return;
        }

        int before = target.Hp;
        target.Hp = Math.Min(target.MaxHp, target.Hp + amount);
        if (target.Hp == before) return;

        // 実際に増えた分だけを数える（上限で切られた分は「払い戻し」になっていない）。
        // 代金の分解（第9期 bill）が差し引く側の資源。回復役の側ではなく
        // **回復された駒**に付けるのは、代金が駒ごとの HP の増減だからで、
        // 「誰が回復したか」を見たいときは Interventions / DamageToAlly の側を見る。
        TallyOf(target).Healed += target.Hp - before;

        // 第105期。**配り手の側**にも同じ量を載せる（受け手側の <c>Healed</c> はそのまま）。
        // 出どころは第94期 (T2) の印——`Heal` は源を引数で受け取らないので、
        // 「いま実行中の特性の持ち主」が唯一の手掛かりになる。
        // 印が立っていない箇所からの回復は<b>誰のものでもない出力</b>として数える。
        {
            int gained = target.Hp - before;
            TurnHealAll += gained;
            UnitState? healer = Mark.Owner;
            if (healer is null) TurnHealNone += gained;
            else if (InOwnTurn(healer)) { TallyOf(healer).HealOutInTurn += gained; TurnHealIn += gained; }
            else { TallyOf(healer).HealOutOffTurn += gained; TurnHealOff += gained; }
        }

        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Heal,
            Turn = _turn,
            ActorId = by?.InstanceId,   // 第124期 段2: 回復させた駒
            TargetId = target.InstanceId,
            Amount = target.Hp - before,
            HpAfter = target.Hp
        });
    }

    /// <summary>後列に空きか入れ替え先があれば返す。</summary>
    /// <summary>
    /// 逃げ込む先。後列に味方がいれば必ずそいつと入れ替える（＝前へ押し出す）。
    /// 空きスロットへ逃げるだけだと誰も損をせず、逃亡が純粋な利益になってしまう。
    /// </summary>
    public int? FindBackSlotFor(UnitState self)
    {
        // 一度に下がれるのは一列だけ。前 → 中 → 後 と段階を踏む。
        Row? next = self.Row switch
        {
            Row.Front => Row.Mid,
            Row.Mid => Row.Back,
            _ => null
        };
        if (next is null) return null;

        var team = LivingMembers(self.TeamId).ToList();
        // **編成枠だけ。** 召喚枠を含めると、空いている ○中1 へ逃げ込んで誰も押しのけないので、
        // 下の「味方がいるなら必ず入れ替える」が空振りして逃亡が純粋な利益になる。
        var slots = FormationRules.PlayableSlotsOfRow(next.Value).ToList();

        // 味方がいるなら必ず入れ替える（＝前へ押し出す）。
        // 空きへ逃げるだけだと誰も損をせず、逃亡が純粋な利益になってしまう。
        // **この優先順位は維持する。**乱数化するのは「同じ段の中で誰を押しのけるか」だけ。
        //
        // 昇順に走って最初の1つを返していたので、後退先が常に 後1 へ偏っていた。
        // 後1 と 後3 は等価なはずなので、鏡像の配置が同値にならない原因になっていた
        // （逃亡兵セロを含む編成で鏡像差が最大 24.1pt）。
        var occupied = slots.Where(s => team.Any(u => u.Slot == s && u != self)).ToList();
        if (occupied.Count > 0) return occupied[Roll(occupied.Count)];

        var empty = slots.Where(s => team.All(u => u.Slot != s)).ToList();
        if (empty.Count > 0) return empty[Roll(empty.Count)];

        return null;
    }

    /// <summary>
    /// 隊列を入れ替える。移動した駒すべてに OnMoved を通知するので、
    /// 逃亡・喧噪・庇いのどれが原因でも「動かされた」駒は等しく反応できる。
    /// </summary>
    public void SwapSlots(UnitState self, int destSlot, UnitState? by = null)
    {
        UnitState? occupant = PickOne(
            LivingMembers(self.TeamId).Where(u => u.Slot == destSlot).ToList());
        int origin = self.Slot;
        Row selfFrom = self.Row;

        self.Slot = destSlot;
        Notify(self, selfFrom);

        if (occupant is null) return;
        Row otherFrom = occupant.Row;
        occupant.Slot = origin;
        Notify(occupant, otherFrom);

        void Notify(UnitState u, Row from)
        {
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.Move,
                Turn = _turn,
                ActorId = by?.InstanceId,   // 第124期 段2: 移動させた駒（押しのけられた側にも同じ駒が載る）
                TargetId = u.InstanceId,
                Slot = u.Slot,
                HpAfter = u.Hp
            });

            // 第68期。動かされた回数。**動かした側ではなく動いた側**に載せる
            // （読み手＝軋み・移り木・突き返しが読むのはこちら）。
            NoteCarry(u, UnitTally.CarryMove, 1);

            // 混乱（第146期）。**動かされた駒に立てる。動かした側の陣営は問わない**
            // ——自分で逃げても引きずり出されても「動かされた」は同じ。
            // 押しのけられた側にも同じだけ立つ（Notify は両方に走る）。
            // **`Active = false` なら 1 バイトも動かない**（軛と同じ短絡の作法）。
            // **`Percent >= 100` なら `Roll` を引かない**（段B の乱数列を段C のノブで動かさない）。
            if (Confusion.Active && u.RawCounter(StatusKeys.Confused) == 0
                && (Confusion.Percent >= 100 || Roll(100) < Confusion.Percent))
            {
                u.SetCounter(StatusKeys.Confused, 1);
                TallyOf(u).ConfusedMarks++;
                Log($"    {u.Name} は足を取られて向きを見失った", LogKind.Status);
            }

            // 後ろへ動いた事実を記録する。自分から逃げたか突き飛ばされたかは問わない。
            // どちらの場合も「味方が矢面に立つ」という代償は発生している。
            if (FormationRules.DepthOf(u.Row) > FormationRules.DepthOf(from))
                u.HasFallenBack = true;

            // 味方の反応を先に流す。OnMoved は割り込み攻撃まで含むので、逆順だと
            // シオの強化が「振った後」に乗る（軋みが +5 を載せずに振ってしまう）。
            // 支援が先・本人の反応が後、という順序をここで固定する。
            foreach (UnitState ally in LivingMembers(u.TeamId))
            {
                if (ally == u) continue;
                foreach (Trait t in ally.Traits.ToList())
                {
                    TraitMark m = this.BeginTrait(t.Id, ally);   // 第94期 (T2) の印
                    t.OnAllyMoved(this, ally, u);
                    this.EndTrait(m);
                }
            }

            foreach (Trait t in u.Traits.ToList())
            {
                TraitMark m = this.BeginTrait(t.Id, u);   // 第94期 (T2) の印
                t.OnMoved(this, u, from, u.Row);
                this.EndTrait(m);
            }
        }
    }
}

public static class BattleEngine
{
    public const int MaxTurns = 30;

    /// <summary>
    /// 編成を2つ渡すと戦闘結果が返る。それだけ。副作用も外部依存もない。
    /// verbose=false にするとログを作らないので、一括シミュレーションが速い。
    /// </summary>
    public static BattleResult Run(Formation player, Formation enemy, int seed, bool verbose = true,
                                   ColossusRule? colossus = null, YokeRule? yoke = null,
                                   HushRule? hush = null, MartyrRule? martyr = null,
                                   ExposeRule? expose = null, ShoveRule? shove = null,
                                   BearRule? bear = null, RelayRule? relay = null,
                                   SlanderRule? slander = null, OverbearRule? overbear = null,
                                   ScaleRule? scale = null, ScapegoatRule? scapegoat = null,
                                   DivertRule? divert = null, GoadRule? goad = null,
                                   FinisherRule? finisher = null, FavorRule? favor = null,
                                   BlazeRule? blaze = null, FunnelRule? funnel = null,
                                   WhetMask? whetMask = null, CreakRule? creak = null,
                                   SeverRule? sever = null, ThinBladeRule? thinBlade = null,
                                   ThornRule? thorn = null, SutureRule? suture = null,
                                   SutureFireRule? sutureFire = null,
                                   SpillWoundRule? spillWound = null, MendRule? mend = null,
                                   IgniteRule? woundIgnite = null, GatherRule? gather = null,
                                   SoakRule? soak = null, DeepRule? deep = null,
                                   CurseRule? curse = null, BetrayRule? betray = null,
                                   EncoreRule? encore = null, RageRule? rage = null,
                                   MenderCostRule? menderCost = null, LooseRule? loose = null,
                                   TaillightRule? taillight = null,
                         ReaderRule? reader = null, BossRule? boss = null,
                                   NourishRule? nourish = null, WoundRule? wound = null,
                                   EmberRule? ember = null, WildfireRule? wildfire = null,
                                   HarmRule? harm = null, ParryRule? parry = null,
                                   ShatterRule? shatter = null, ShrapnelRule? shrapnel = null,
                                   BraceRule? brace = null, ShufflerRule? shuffler = null,
                                   ConfusionRule? confusion = null, HasteRule? haste = null,
                                   WardRule? ward = null, IndulgenceRule? indulgence = null,
                                   AshRule? ash = null,
                                   CounterProbe? probe = null)
        => Run(Materialize(player, BattleContext.PlayerTeam),
               Materialize(enemy, BattleContext.EnemyTeam),
               seed, verbose, colossus, yoke, hush, martyr, expose, shove, bear, relay, slander,
               overbear, scale, scapegoat, divert, goad, finisher, favor, blaze, funnel, whetMask,
               creak, sever, thinBlade, thorn, suture, sutureFire, spillWound, mend, woundIgnite,
               gather, soak, deep, curse, betray, encore, rage, menderCost, loose, taillight, reader, boss,
               nourish, wound, ember, wildfire, harm, parry, shatter, shrapnel, brace, shuffler,
               confusion, haste, ward, indulgence, ash, probe);

    /// <summary>
    /// 駒の状態を直接渡して1戦を回す。会戦（Engagement）が持ち越した UnitState を
    /// そのまま次の戦闘へ投入するための入り口。
    ///
    /// InstanceId は ctx.Add がここで渡された順（味方リスト → 敵リスト）に振り直す。
    /// 再生側（GodotApp / replay）は「Deploy の順で数えれば一致する」前提を持っているので、
    /// 渡す側はリストの並びを決定的に保つこと（Materialize はスロット昇順で返す）。
    /// </summary>
    public static BattleResult Run(IReadOnlyList<UnitState> player, IReadOnlyList<UnitState> enemy,
                                   int seed, bool verbose = true, ColossusRule? colossus = null,
                                   YokeRule? yoke = null, HushRule? hush = null,
                                   MartyrRule? martyr = null, ExposeRule? expose = null,
                                   ShoveRule? shove = null, BearRule? bear = null,
                                   RelayRule? relay = null, SlanderRule? slander = null,
                                   OverbearRule? overbear = null, ScaleRule? scale = null,
                                   ScapegoatRule? scapegoat = null, DivertRule? divert = null,
                                   GoadRule? goad = null, FinisherRule? finisher = null,
                                   FavorRule? favor = null, BlazeRule? blaze = null,
                                   FunnelRule? funnel = null, WhetMask? whetMask = null,
                                   CreakRule? creak = null, SeverRule? sever = null,
                                   ThinBladeRule? thinBlade = null, ThornRule? thorn = null,
                                   SutureRule? suture = null, SutureFireRule? sutureFire = null,
                                   SpillWoundRule? spillWound = null,
                                   MendRule? mend = null, IgniteRule? woundIgnite = null,
                                   GatherRule? gather = null, SoakRule? soak = null,
                                   DeepRule? deep = null, CurseRule? curse = null,
                                   BetrayRule? betray = null, EncoreRule? encore = null,
                                   RageRule? rage = null, MenderCostRule? menderCost = null,
                                   LooseRule? loose = null, TaillightRule? taillight = null,
                         ReaderRule? reader = null, BossRule? boss = null,
                                   NourishRule? nourish = null, WoundRule? wound = null,
                                   EmberRule? ember = null, WildfireRule? wildfire = null,
                                   HarmRule? harm = null, ParryRule? parry = null,
                                   ShatterRule? shatter = null, ShrapnelRule? shrapnel = null,
                                   BraceRule? brace = null, ShufflerRule? shuffler = null,
                                   ConfusionRule? confusion = null, HasteRule? haste = null,
                                   WardRule? ward = null, IndulgenceRule? indulgence = null,
                                   AshRule? ash = null,
                                   CounterProbe? probe = null)
    {
        var ctx = new BattleContext(seed, verbose, colossus, yoke, hush, martyr, expose, shove, bear,
                                    relay, slander, overbear, scale, scapegoat, divert, goad, finisher,
                                    favor, blaze, funnel, whetMask, creak, sever, thinBlade, thorn,
                                    suture, sutureFire, spillWound, mend, woundIgnite, gather, soak, deep, curse,
                                    betray, encore, rage, menderCost, loose, taillight, reader, boss,
                                    nourish, wound, ember, wildfire, harm, parry, shatter, shrapnel, brace,
                                    shuffler, confusion, haste, ward, indulgence, ash, probe);

        foreach (UnitState u in player) ctx.Add(u);
        foreach (UnitState u in enemy) ctx.Add(u);

        ctx.Log("=== 戦闘開始 ===", LogKind.System);

        // 開戦時の通知順。ThenBy が無いので同速は _units の並び＝実質スロット昇順に落ちる。
        // ターン順と同じ扱いにして、同速の群だけを混ぜる（生贄・呪詛の適用順が席番号で決まらない）。
        var opening = ctx.AllUnits
            .GroupBy(u => u.Def.Speed)
            .OrderByDescending(g => g.Key)
            .SelectMany(g =>
            {
                var tie = g.ToList();
                ctx.Shuffle(tie);
                return tie;
            })
            .ToList();

        foreach (UnitState u in opening)
        {
            if (!u.IsAlive) continue;
            foreach (Trait t in u.Traits.ToList())
            {
                TraitMark m = ctx.BeginTrait(t.Id, u);   // 第94期 (T2) の印
                t.OnBattleStart(ctx, u);
                ctx.EndTrait(m);
            }
        }

        int turn = 1;
        for (; turn <= MaxTurns; turn++)
        {
            ctx.Turn = turn;
            if (!ctx.TeamAlive(BattleContext.PlayerTeam) || !ctx.TeamAlive(BattleContext.EnemyTeam))
                break;

            ctx.Log($"--- ターン {turn} ---", LogKind.Turn);
            ctx.EmitTurnStart();
            ctx.TickStatuses();
            ctx.EmitStatusSnapshot();   // 削った後の残量を写す。表示用で、盤面には触らない
            ctx.NoteHexCensus();        // 呪い（第96期）の門の 2。**盤面は読むだけ**
            ctx.NoteReaderCensus();     // 積み過ぎ（第115期）の門の 1。**盤面は読むだけ**
            ctx.NoteBossCensus();       // ボスの土台（第117期）の時系列。**盤面は読むだけ**
            ctx.NoteWoundCensus();      // 傷の在庫（第120期）。**盤面は読むだけ**
            ctx.NoteArmorCensus();      // 破片の在庫（第138期 段2）。**盤面は読むだけ**
            ctx.NoteWardCensus();       // 重りの在庫（第154期）。**盤面は読むだけ**

            foreach (UnitState u in ctx.AllUnits.Where(x => x.IsAlive).ToList())
                foreach (Trait t in u.Traits.ToList())
                {
                    TraitMark m = ctx.BeginTrait(t.Id, u);   // 第94期 (T2) の印
                    t.OnTurnStart(ctx, u);
                    ctx.EndTrait(m);
                }

            // 止め（第53期）の遊休（標が付いてから殴られるまで）を測るための印。
            // 標の書き手（逸らし・駆り立て・囃し立て）はここまでに全部書き終わっている。
            // **盤面は1つも動かさない**（私有カウンタを書くだけ）ので、保持者がいなければ走らない。
            if (ctx.FinisherActive) ctx.NoteFinisherMarkAges();

            // 第150期 段A。標の一生の帳簿（**計数専用**）。位置と理由は上と同じ。
            ctx.ScanMarkLedger();

            // 素早さ順。同値はチームで割り、**その中は毎ターン乱数で混ぜる**。
            //
            // 以前は .ThenBy(u => u.Slot) で安定させていたが、これが席番号の偏りの本体だった。
            // X字盤面では前1と前3（後1と後3）は等価なはずなのに、同速なら常に若い席が先に動く。
            // 陣営間のタイブレーク（TeamId）は席番号とは無関係な設計上の順序なので残す。
            //
            // **毎ターン振り直す。**開戦時に1回だけ決めると、その後に生死や速さの前提が
            // 変わっても初回の順序を引きずる。
            //
            // 逆位（InversionTrait）: 保持者が盤上に生きている間だけ、速さの**向き**が逆になる。
            // **両陣営にかかる。** 非対称なのは「こちらはそのルールを知って編成を組めるが、
            // 敵は組めない」点だけ。毎ターン評価するので、保持者を倒せば次のターンから戻る。
            //
            // 反転するのは速さの向き 1本だけ。以下は**一緒に反転させない**——
            //   陣営タイブレーク（ThenBy(TeamId)）: 席番号とは無関係な設計上の順序で、
            //     速さの向きとは別の話。一緒に反転すると変数が2つ動く
            //   同速の中のシャッフル: 群の中の乱数化は席バイアス対策であって順序の話ではない
            //   開戦時の通知順（opening）: あちらは生贄・呪詛の適用順で、目的が違う
            bool inverted = ctx.AllUnits.Any(u => u.IsAlive && u.HasTrait(TraitId.Inversion));

            var speedGroups = ctx.AllUnits
                .Where(u => u.IsAlive)
                .GroupBy(u => (u.Def.Speed, u.TeamId));

            var order = (inverted
                    ? speedGroups.OrderBy(g => g.Key.Speed)
                    : speedGroups.OrderByDescending(g => g.Key.Speed))
                .ThenBy(g => g.Key.TeamId)
                .SelectMany(g =>
                {
                    var tie = g.ToList();
                    ctx.Shuffle(tie);      // 同速・同陣営の中だけ。群をまたいで混ぜない
                    return tie;
                })
                .ToList();

            // 前倒し（第149期・HasteRule）。**order が確定した後**に1体を抜いて先頭へ差し込む。
            //
            // **speedGroups にも Shuffle にも触らない。** 群から先に抜くと群の要素数が変わって
            // シャッフルの乱数消費がずれ、盤面全体が動く（この期の最大の実装上の罠）。
            // ここは Shuffle を全部走らせ切った後なので、**乱数列は規則の有無に依らず同一**
            // ——既定（Pick = None）で `compare` 305 セルが 0 件であることが検算。
            //
            // **Remove してから Insert する**ので延べ手番数は変わらない（TurnLoopCalls が版間で一致）。
            // 選択は決定的（PickOne を使わない）。同値は order の並びで解決する
            // ＝新しい乱数を1つも引かない。
            if (ctx.Haste.Pick != HastePick.None)
            {
                UnitState? pick = null;
                foreach (UnitState u in order)
                {
                    if (u.TeamId != BattleContext.PlayerTeam || !u.IsAlive) continue;
                    if (pick is null) { pick = u; continue; }
                    // 同値では入れ替えない（order の中で先に出てくるほうを残す）。
                    bool better = ctx.Haste.Pick == HastePick.Slowest
                        ? u.Def.Speed < pick.Def.Speed
                        : u.CurrentAttack > pick.CurrentAttack;
                    if (better) pick = u;
                }
                if (pick is not null && order[0] != pick)
                {
                    int before = order.Count;
                    order.Remove(pick);
                    order.Insert(0, pick);
                    ctx.HasteMoves++;   // 計数のみ。どの規則も読まない
                    // **その<u>ターンの</u>延べ手番数が変わっていないこと**の検算（第149期）。
                    // 戦をまたいだ `TurnLoopCalls` の総和は一致しない——順序が盤面を動かすと
                    // 決着ターン数が動き、分母がターン数である量はそれに引きずられる（第113期）。
                    if (order.Count != before) ctx.HasteCountMismatch++;
                }
            }

            foreach (UnitState actor in order)
            {
                if (!actor.IsAlive) continue;
                if (!ctx.TeamAlive(ctx.Opponent(actor.TeamId))) break;

                ctx.TurnLoopCalls++;   // 第105期・自己検査 (c)（計数のみ）
                ctx.TakeTurn(actor);
            }
        }

        bool playerWon = ctx.TeamAlive(BattleContext.PlayerTeam)
                         && !ctx.TeamAlive(BattleContext.EnemyTeam);

        ctx.Log(playerWon ? "=== 勝利 ===" : "=== 敗北 ===", LogKind.System);

        // 生き残った駒の「最後に活動していたターン」は決着ターン。
        // 集計（life 診断）専用で、誰もこの値を読んで分岐しない。
        int settled = Math.Min(turn, MaxTurns);
        foreach (UnitState u in ctx.AllUnits)
            if (u.IsAlive)
            {
                ctx.TallyOf(u).LastActiveTurn = settled;
                // 読まれないまま戦闘が終わった傷（第85期・自己検査 (j)）。集計専用。
                ctx.TallyOf(u).WoundsAtEnd += u.RawCounter(StatusKeys.Wound);
            }

        // 第120期の帳簿を閉じる（**死者を含む全駒を1度だけ**。上のループは生存駒しか見ていない）。
        ctx.CloseWoundLedger();

        // 第134期 段1 の帳簿を閉じる（**死者を含む全駒を1度だけ**。燃えたまま倒れた駒の区間は
        // `TickStatuses` が二度と触らないので、ここで閉じないと丸ごと落ちる）。
        ctx.CloseBurnLedger();
        // 第134期 段2。保持者が最後のターンに落ちた場合を拾う（`TickStatuses` はもう回らない）。
        ctx.CloseRuleHolders();
        // 第150期 段A。標の帳簿を閉じる（決着時にまだ立っていた標）。
        ctx.CloseMarkLedger();
        // 第153期。決着時にプールに残っていた預かりを数える（収支を閉じるため）。
        ctx.CloseWardLedger();
        // 第155期。決着時に残っていた負債と燃料を数える（収支を閉じるため）。
        ctx.CloseIndulgenceLedger();
        ctx.CloseAshLedger();   // 第179期・**計数のみ**

        return new BattleResult
        {
            PlayerWon = playerWon,
            Turns = Math.Min(turn, MaxTurns),
            Log = ctx.Log_.ToList(),
            PlayerSurvivors = ctx.LivingMembers(BattleContext.PlayerTeam).Count(),
            PlayerStarterFallen = FallenStarters(player),
            DamageByUnit = new Dictionary<string, int>(ctx.DamageByUnit),
            TallyByUnit = new Dictionary<string, UnitTally>(ctx.TallyByUnit),
            MaxEnemyKillsInOneTurn = ctx.MaxEnemyKillsInOneTurn,
            Events = ctx.Events.ToList(),
            Wounds = new WoundLedger(
                (int[])ctx.WoundWriteAlly.Clone(), (int[])ctx.WoundWriteFoe.Clone(),
                (int[])ctx.WoundLossAll.Clone(), (int[])ctx.WoundLossAlly.Clone(),
                ctx.WoundStockTurns, ctx.WoundStockAllySum, ctx.WoundStockFoeSum,
                ctx.WoundStockAllyMax, ctx.WoundStockFoeMax,
                ctx.WoundTurnsAllyAny, ctx.WoundTurnsFoeAny,
                (long[])ctx.WoundDepthAlly.Clone(), (long[])ctx.WoundDepthFoe.Clone(),
                ctx.WoundLagSum, ctx.WoundLagCount, ctx.HpRemoved,
                (int[])ctx.ReadFires.Clone(), (long[])ctx.ReadWounds.Clone(),
                (long[])ctx.ReadNominal.Clone(), (long[])ctx.ReadEffective.Clone(),
                (int[])ctx.GuardFires.Clone(), (int[])ctx.GuardTargetWounded.Clone(),
                (long[])ctx.GuardWoundedAllySum.Clone(), (int[])ctx.GuardAnyWoundedAlly.Clone()),
            // 第132期 段1。**計数専用**（どの規則も読まない）。
            Yoke = new YokeLedger(
                (long[])ctx.YokeCutHits.Clone(), (long[])ctx.YokeCutLost.Clone(),
                (long[])ctx.YokeCutPassed.Clone(), (long[])ctx.YokeNearHits.Clone(),
                (long[])ctx.YokeInHits.Clone(), (long[])ctx.YokeInAmount.Clone(),
                (long[])ctx.YokeKills.Clone(), (long[])ctx.YokeOverkill.Clone(),
                ctx.YokeCutOnPlayerHits, ctx.YokeCutOnPlayerLost,
                ctx.YokeCutOnEnemyHits, ctx.YokeCutOnEnemyLost,
                ctx.YokeArmorSoak, ctx.YokeInRelayedHits, ctx.YokeInRelayedAmount,
                ctx.YokeInBurnHits, ctx.YokeInBurnAmount,
                ctx.YokeInLevyHits, ctx.YokeInLevyAmount,
                ctx.DirectHpLoss, new Dictionary<string, (long, long)>(ctx.YokeCutBy)),
            // 第138期 段2。**計数専用**（どの規則も読まない）。
            Armor = new ArmorLedger(
                ctx.ArmorTurns,
                (long[])ctx.ArmorStockSum.Clone(), (long[])ctx.ArmorStockMax.Clone(),
                (long[])ctx.ArmorTopSum.Clone(), (long[])ctx.ArmorTopMax.Clone(),
                (long[])ctx.ArmorTurnsAny.Clone(), (long[])ctx.ArmorHolders.Clone(),
                new Dictionary<string, (long, long)>(ctx.ArmorTopBy)),
            // 第134期 段1・段2。**計数専用**（どの規則も読まない）。
            Burns = new BurnLedger(
                (long[])ctx.BurnLitSide.Clone(), (long[])ctx.BurnRelitSide.Clone(),
                (long[])ctx.BurnEpisodes.Clone(), (long[])ctx.BurnRelitSum.Clone(),
                (long[])ctx.BurnRelitMax.Clone(),
                new[] { (long[])ctx.BurnHist[0].Clone(), (long[])ctx.BurnHist[1].Clone() },
                (long[])ctx.BurnEndExpired.Clone(), (long[])ctx.BurnEndDeath.Clone(),
                (long[])ctx.BurnEndAlive.Clone(),
                new Dictionary<string, (long, long)>(ctx.BurnBy),
                new Dictionary<string, (long, long)>(ctx.BurnOn)),
            // 第150期 段A。標の一生（**計数専用**。どの規則も読まない）。
            Marks = new MarkLedger(
                (long[])ctx.MarkOpened.Clone(), (long[])ctx.MarkEndConsumed.Clone(),
                (long[])ctx.MarkEndDied.Clone(), (long[])ctx.MarkEndDiedByFinisher.Clone(),
                (long[])ctx.MarkEndStrip.Clone(), (long[])ctx.MarkEndGoad.Clone(),
                (long[])ctx.MarkEndOther.Clone(), (long[])ctx.MarkEndStanding.Clone(),
                (long[])ctx.MarkLifeSum.Clone(), (long[])ctx.MarkLifeMax.Clone(),
                (long[])ctx.MarkHits.Clone(), (long[])ctx.MarkHitsByFinisher.Clone(),
                new Dictionary<string, (long, long, long)>(ctx.MarkOn)),
            // 第153期 段A。預かりの帳簿（**計数専用**。どの規則も読まない）。
            Ward = new WardLedger(
                ctx.WardStacked, ctx.WardReleaseAsked, ctx.WardReleased, ctx.WardResidual,
                ctx.WardBursts, ctx.WardDrips, ctx.WardDry, ctx.WardDryDrought, ctx.WardDryStoic,
                ctx.WardForfeits, ctx.WardForfeited, ctx.WardForfeitHealed,
                new Dictionary<string, (long, long)>(ctx.WardOn),
                ctx.WardBurdenHits, ctx.WardBurdenAdded,
                ctx.WardLadenSwings, ctx.WardLadenFloored, ctx.WardLadenSwingLost,
                ctx.WardLadenNominal, ctx.WardLadenCarriers),
            // 第155期 段A。贖いの帳簿（**計数専用**。どの規則も読まない）。
            Indulgence = new IndulgenceLedger(
                ctx.IndulgenceFires, ctx.IndulgenceAsked, ctx.DebtStacked,
                ctx.IndulgenceDry, ctx.IndulgenceDryDrought, ctx.IndulgenceNoPatient,
                ctx.IndulgenceBlocked, ctx.IndulgenceBlockedIdle,
                ctx.TollFires, ctx.TollNominal, ctx.TollTaken, ctx.TollFloored, ctx.TollYokeCut, ctx.TollKills,
                ctx.TollForgives, ctx.TollForgiven, ctx.TollForgivenSelfHp,
                ctx.BrandFires, ctx.BrandSpent, ctx.BrandRemoved, ctx.BrandYokeCut,
                ctx.BrandHits, ctx.BrandKills, ctx.BrandDry, ctx.BrandResidual,
                ctx.DebtResidual, new Dictionary<string, (long, long)>(ctx.DebtOn)),
            BoardRules = new BoardRuleLedger(
                (long[])ctx.DroughtHits.Clone(), (long[])ctx.DroughtRequested.Clone(),
                (long[])ctx.DroughtEffective.Clone(),
                new Dictionary<string, (long, long)>(ctx.DroughtOn),
                (long[])ctx.HushBlockedSide.Clone(), (long[])ctx.HushBlockedAnySide.Clone(),
                ctx.HushByRoute.Select(r => (long[])r.Clone()).ToArray(),
                (long[])ctx.HushAskedSide.Clone(),
                ctx.RuleHolderCount, (int[])ctx.RuleFallTurn.Clone()),
            ExposeCount = ctx.ExposeCount,
            ExposeMissed = ctx.ExposeMissed,
            DullTotal = ctx.DullTotal,
            SoakDullFired = ctx.SoakDullFired,
            SoakDullDry = ctx.SoakDullDry,
            SoakDullKinds = ctx.SoakDullKinds,
            SoakDullAdded = ctx.SoakDullAdded,
            HexHits = ctx.HexHits,
            HexHitsFromFoe = ctx.HexHitsFromFoe,
            HexHitsFromAlly = ctx.HexHitsFromAlly,
            HexHitsNoSource = ctx.HexHitsNoSource,
            HexMarks = ctx.HexMarks,
            HexMarksOnPlayer = ctx.HexMarksOnPlayer,
            HexMarksOnEnemy = ctx.HexMarksOnEnemy,
            HexReMarkBlocked = ctx.HexReMarkBlocked,
            HexPairTurnPlayer = ctx.HexPairTurnPlayer,
            HexPairTurnEnemy = ctx.HexPairTurnEnemy,
            HexPairTurnsPlayer = ctx.HexPairTurnsPlayer,
            HexPairTurnsEnemy = ctx.HexPairTurnsEnemy,
            HexMaxCursedPlayer = ctx.HexMaxCursedPlayer,
            HexMaxCursedEnemy = ctx.HexMaxCursedEnemy,
            HexCensusTurns = ctx.HexCensusTurns,
            HexShareHits = ctx.HexShareHits,
            HexShareDry = ctx.HexShareDry,
            HexShares = ctx.HexShares,
            HexShareDamage = ctx.HexShareDamage,
            HexSharesToPlayer = ctx.HexSharesToPlayer,
            HexShareDamageToPlayer = ctx.HexShareDamageToPlayer,
            HexShareTargets = ctx.HexShareTargets,
            HexShareBase = ctx.HexShareBase,
            HexShareBaseTimesTargets = ctx.HexShareBaseTimesTargets,
            HexMarksOnStoic = ctx.HexMarksOnStoic,
            HexHopBlocked = ctx.HexHopBlocked,
            HexNonSingleOnCursed = ctx.HexNonSingleOnCursed,
            NourishFires = ctx.NourishFires,
            NourishGiven = ctx.NourishGiven,
            NourishToFoe = ctx.NourishToFoe,
            NourishToAlly = ctx.NourishToAlly,
            NourishNoSource = ctx.NourishNoSource,
            NourishSelf = ctx.NourishSelf,
            NourishLevy = ctx.NourishLevy,
            NourishDead = ctx.NourishDead,
            NourishSoaked = ctx.NourishSoaked,
            NourishByPath = ctx.NourishByPath ?? Array.Empty<int>(),
            BetrayTries = ctx.BetrayTries,
            BetraySummoned = ctx.BetraySummoned,
            BetrayBlocked = ctx.BetrayBlocked,
            BetrayAllySide = ctx.BetrayAllySide,
            BetrayWrongSlot = ctx.BetrayWrongSlot,
            BetrayMaxAlive = ctx.BetrayMaxAlive,
            BetrayIdleSellable = ctx.BetrayIdleSellable,
            BetrayRevived = ctx.BetrayRevived,
            BetrayKilled = ctx.BetrayKilled,
            EncoreWoundedDeaths = ctx.EncoreWoundedDeaths,
            EncoreWoundedFoeDeaths = ctx.EncoreWoundedFoeDeaths,
            EncoreLiveWriters = ctx.EncoreLiveWriters,
            EncoreDeathsWithLiveWriter = ctx.EncoreDeathsWithLiveWriter,
            EncoreFired = ctx.EncoreFired,
            TurnLoopCalls = ctx.TurnLoopCalls,
            HasteMoves = ctx.HasteMoves,
            HasteCountMismatch = ctx.HasteCountMismatch,
            TurnDmgAll = ctx.TurnDmgAll, TurnDmgIn = ctx.TurnDmgIn,
            TurnDmgOff = ctx.TurnDmgOff, TurnDmgNone = ctx.TurnDmgNone,
            TurnHealAll = ctx.TurnHealAll, TurnHealIn = ctx.TurnHealIn,
            TurnHealOff = ctx.TurnHealOff, TurnHealNone = ctx.TurnHealNone,
            TurnStatusAll = ctx.TurnStatusAll, TurnStatusIn = ctx.TurnStatusIn,
            TurnStatusOff = ctx.TurnStatusOff, TurnStatusNone = ctx.TurnStatusNone,
            TurnBuffAll = ctx.TurnBuffAll, TurnBuffIn = ctx.TurnBuffIn,
            TurnBuffOff = ctx.TurnBuffOff, TurnBuffNone = ctx.TurnBuffNone,
            BuffGainByTrait = ctx.BuffGainByTrait, BuffLossByTrait = ctx.BuffLossByTrait,
            BuffGainNoMark = ctx.BuffGainNoMark, BuffLossNoMark = ctx.BuffLossNoMark,
            RageFiresFromFoe = ctx.RageFiresFromFoe, RageFiresFromAlly = ctx.RageFiresFromAlly,
            RageFiresNoSource = ctx.RageFiresNoSource,
            EncoreAttack = ctx.EncoreAttack,
            EncoreSkill = ctx.EncoreSkill,
            EncoreCharge = ctx.EncoreCharge,
            EncoreStalled = ctx.EncoreStalled,
            EncoreBlockedHop = ctx.EncoreBlockedHop,
            EncoreOnEnemySide = ctx.EncoreOnEnemySide,
            EncoreWithActions = ctx.EncoreWithActions,
            EncoreRevivedSkip = ctx.EncoreRevivedSkip,
            EncoreFodderDeaths = ctx.EncoreFodderDeaths,
            EncoreFromFodder = ctx.EncoreFromFodder,
            BetrayFireAttack = ctx.BetrayFireAttack,
            BetrayFirePoison = ctx.BetrayFirePoison,
            BetrayFireOverreach = ctx.BetrayFireOverreach,
            BetrayHits = ctx.BetrayHits,
            BetrayHitAtkSum = ctx.BetrayHitAtkSum,
            HexCrossTeam = ctx.HexCrossTeam,
            HexSpillSuppressed = ctx.HexSpillSuppressed,
            HexShareBySource = ctx.HexShareBySource,
            HexShareDamageBySource = ctx.HexShareDamageBySource,
            HexHitByAlly = ctx.HexHitByAlly,
            HexHitByFoe = ctx.HexHitByFoe,
            DullByRoute = ctx.DullByRoute,
            DullTakenByRoute = ctx.DullTakenByRoute,
            WhetTotal = ctx.WhetTotal,
            WhetByRoute = ctx.WhetByRoute,
            WhetTurnSumByRoute = ctx.WhetTurnSumByRoute,
            WhetFirstTurnSumByRoute = ctx.WhetFirstTurnSumByRoute,
            WhetFirstTurnCountByRoute = ctx.WhetFirstTurnCountByRoute,
            WhetUsedByRoute = ctx.WhetUsedByRoute,
            WhetToByRoute = ctx.WhetToByRoute,
            WhetToPerverse = ctx.WhetToPerverse,
            WhetPerverseFlips = ctx.WhetPerverseFlips,
            DullZeroed = ctx.DullZeroed,
            DullZeroedWho = ctx.DullZeroedWho,
            BearTaken = ctx.BearTaken,
            BearPassed = ctx.BearPassed,
            BearArmor = ctx.BearArmor,
            BearSoaked = ctx.BearSoaked,
            BearFrom = ctx.BearFrom,
            RelayTaken = ctx.RelayTaken,
            RelaySent = ctx.RelaySent,
            RelayMaxSent = ctx.RelayMaxSent,
            RelayZeroed = ctx.RelayZeroed,
            RelayCost = ctx.RelayCost,
            RelaySelfPaid = ctx.RelaySelfPaid,
            RelayFrom = ctx.RelayFrom,
            RelayTo = ctx.RelayTo,
            ShoveFired = ctx.ShoveFired,
            ShoveCapped = ctx.ShoveCapped,
            ShoveSwapped = ctx.ShoveSwapped,
            ShoveNoRow = ctx.ShoveNoRow,
            ShoveStaggered = ctx.ShoveStaggered,
            ShoveBlocked = ctx.ShoveBlocked,
            SlanderFired = ctx.SlanderFired,
            SlanderTotal = ctx.SlanderTotal,
            SlanderTo = ctx.SlanderTo,
            OverbearFired = ctx.OverbearFired,
            OverbearTotal = ctx.OverbearTotal,
            OverbearTo = ctx.OverbearTo,
            OverbearMetTurns = ctx.OverbearMetTurns,
            OverbearTurns = ctx.OverbearTurns,
            OverbearFirstTurn = ctx.OverbearFirstTurn,
            OverbearSwings = ctx.OverbearSwings,
            OverbearDoubled = ctx.OverbearDoubled,
            OverbearBackfire = ctx.OverbearBackfire,
            OverbearBackfireHits = ctx.OverbearBackfireHits,
            ScaleGainDeath = ctx.ScaleGainDeath,
            ScaleGainShatter = ctx.ScaleGainShatter,
            ScaleGainBear = ctx.ScaleGainBear,
            ScaleGainEphemeral = ctx.ScaleGainEphemeral,
            ScaleFirstTurn = ctx.ScaleFirstTurn,
            ScaleAliveTurns = ctx.ScaleAliveTurns,
            ScaleWornTurns = ctx.ScaleWornTurns,
            ScaleSwings = ctx.ScaleSwings,
            ScalePierceSwings = ctx.ScalePierceSwings,
            ScaleBackHits = ctx.ScaleBackHits,
            ScaleBackDamage = ctx.ScaleBackDamage,
            ScaleSpentAttack = ctx.ScaleSpentAttack,
            ScaleSpentHit = ctx.ScaleSpentHit,
            ScaleDepleted = ctx.ScaleDepleted,
            ScaleFullSoaks = ctx.ScaleFullSoaks,
            // 死蔵: 決着時に保持者がまだ纏っていた量（使われずに終わった破片）。
            // **倒れた保持者も数える**——纏ったまま倒れた破片も「使われずに終わった」側。
            ScaleLeftover = ctx.AllUnits
                .Where(u => u.HasTrait(TraitId.Scale))
                .Sum(u => u.RawCounter(StatusKeys.Armor)),
            ShrapnelFires = ctx.ShrapnelFires,
            ShrapnelShards = ctx.ShrapnelShards,
            ShrapnelFoeTargets = ctx.ShrapnelFoeTargets,
            ShrapnelDealt = ctx.ShrapnelDealt,
            ShrapnelSelfHarm = ctx.ShrapnelSelfHarm,
            ShrapnelHits = ctx.ShrapnelHits,
            ShatterTicks = ctx.ShatterTicks,
            ShatterGiven = ctx.ShatterGiven,
            ShatterSoaked = ctx.ShatterSoaked,
            ShatterPaid = ctx.ShatterPaid,
            ShatterPaidSelf = ctx.ShatterPaidSelf,
            ScapegoatTakes = ctx.ScapegoatTakes,
            ScapegoatTakeByKind = ctx.ScapegoatTakeByKind,
            ScapegoatTakeFrom = ctx.ScapegoatTakeFrom,
            ScapegoatMissed = ctx.ScapegoatMissed,
            ScapegoatFull = ctx.ScapegoatFull,
            ScapegoatAliveTurns = ctx.ScapegoatAliveTurns,
            ScapegoatMetTurns = ctx.ScapegoatMetTurns,
            ScapegoatKindSum = ctx.ScapegoatKindSum,
            ScapegoatKindMax = ctx.ScapegoatKindMax,
            ScapegoatFirstTurn = ctx.ScapegoatFirstTurn,
            ScapegoatSwings = ctx.ScapegoatSwings,
            ScapegoatFired = ctx.ScapegoatFired,
            ScapegoatWriteByKind = ctx.ScapegoatWriteByKind,
            ScapegoatFoeDot = ctx.ScapegoatFoeDot,
            ScapegoatFoeSkips = ctx.ScapegoatFoeSkips,
            ScapegoatMarkPulls = ctx.ScapegoatMarkPulls,
            ScapegoatDotByUnit = ctx.ScapegoatDotByUnit,
            ScapegoatSkipByUnit = ctx.ScapegoatSkipByUnit,
            DivertFires = ctx.DivertFires,
            DivertStrips = ctx.DivertStrips,
            DivertFocus = ctx.DivertFocus,
            DivertFocusFresh = ctx.DivertFocusFresh,
            DivertStripFrom = ctx.DivertStripFrom,
            DivertFocusTo = ctx.DivertFocusTo,
            DivertMarkedFoeSum = ctx.DivertMarkedFoeSum,
            DivertMarkedFoeMax = ctx.DivertMarkedFoeMax,
            DivertAllySingles = ctx.DivertAllySingles,
            DivertAllyOnMarked = ctx.DivertAllyOnMarked,
            DivertFoeSingles = ctx.DivertFoeSingles,
            DivertFoeOnMarked = ctx.DivertFoeOnMarked,
            DivertAllyPulls = ctx.DivertAllyPulls,
            DivertFoePulls = ctx.DivertFoePulls,
            DivertKillTurnByFoe = ctx.DivertKillTurnByFoe,
            DivertKillCountByFoe = ctx.DivertKillCountByFoe,
            GoadFires = ctx.GoadFires,
            GoadIdle = ctx.GoadIdle,
            GoadGiven = ctx.GoadGiven,
            GoadSwitches = ctx.GoadSwitches,
            GoadMarkLost = ctx.GoadMarkLost,
            GoadToPerverse = ctx.GoadToPerverse,
            GoadTargetTo = ctx.GoadTargetTo,
            FinisherFires = ctx.FinisherFires,
            FinisherIdle = ctx.FinisherIdle,
            FinisherCross = ctx.FinisherCross,
            FinisherConsumed = ctx.FinisherConsumed,
            FinisherKills = ctx.FinisherKills,
            FinisherWaitSum = ctx.FinisherWaitSum,
            FinisherWaitCount = ctx.FinisherWaitCount,
            FinisherAllySingles = ctx.FinisherAllySingles,
            FinisherStarved = ctx.FinisherStarved,
            FinisherTargetTo = ctx.FinisherTargetTo,
            FavorFires = ctx.FavorFires,
            FavorIdle = ctx.FavorIdle,
            MiasmaFires = ctx.MiasmaFires,
            MiasmaToFoe = ctx.MiasmaToFoe,
            MiasmaToAlly = ctx.MiasmaToAlly,
            PoisonBitePlayer = ctx.PoisonBitePlayer,
            PoisonBiteEnemy = ctx.PoisonBiteEnemy,
            PoisonTicksPlayer = ctx.PoisonTicksPlayer,
            PoisonTicksEnemy = ctx.PoisonTicksEnemy,
            FavorWhetted = ctx.FavorWhetted,
            FavorDulled = ctx.FavorDulled,
            FavorGiven = ctx.FavorGiven,
            FavorTaken = ctx.FavorTaken,
            FavorToPyre = ctx.FavorToPyre,
            FavorWhetTo = ctx.FavorWhetTo,
            FavorDullTo = ctx.FavorDullTo,
            FunnelTaken = ctx.FunnelTaken,
            FunnelByRoute = ctx.FunnelByRoute,
            FunnelFrom = ctx.FunnelFrom,
            FunnelTo = ctx.FunnelTo,
            // 死蔵: **回した先が一度も振らなかった**ぶんの量（第62期）。
            // `Attacks` は `PerformAttack` を通った回数なので、不動のカド（反撃しかしない）は
            // 必ずここへ落ちる——**マイナスの本体がこの列**。反応型と本当の置物の切り分けは
            // 診断が `FunnelTo` の内訳で読む（第56期の死蔵の但し書きと同じ）。
            FunnelDead = ctx.FunnelTo
                .Where(kv => ctx.TallyByUnit.TryGetValue(kv.Key, out UnitTally? t) && t.Attacks == 0)
                .Sum(kv => kv.Value),
            // 死蔵の新定義（第64期）。**回した先が攻撃力を出力に1度も変換しなかった**量。
            // `Attacks == 0` は棘のような反応型を数え過ぎる（第63期 §11-2 で符号を逆に読んだ）。
            FunnelDeadNew = ctx.FunnelTo
                .Where(kv => ctx.TallyByUnit.TryGetValue(kv.Key, out UnitTally? t) && t.AttackReads == 0)
                .Sum(kv => kv.Value),
            FunnelDullTaken = ctx.FunnelDullTaken,
            FunnelDullByRoute = ctx.FunnelDullByRoute,
            FunnelDullFrom = ctx.FunnelDullFrom,
            FunnelDullTo = ctx.FunnelDullTo,
            // 弱体側の死蔵（第63期）。**「捨て場として成功した量」ではない**（第63期に実測で否定）。
            // `Attacks` は `PerformAttack` を通った回数なので、**棘の反撃は通らない**
            // ——カドの反撃量は自分の `CurrentAttack` で決まるので、振らなくても弱体は効く。
            // **この列は「振らなかった量」でしかない。**
            FunnelDullDead = ctx.FunnelDullTo
                .Where(kv => ctx.TallyByUnit.TryGetValue(kv.Key, out UnitTally? t) && t.Attacks == 0)
                .Sum(kv => kv.Value)
        };
    }

    /// <summary>
    /// 編成から新品の UnitState 群を作る（スロット昇順）。旧 Deploy の切り出し。
    /// 盤面には触らない（Add は Run 側でやる）ので、会戦が「部隊列から次の部隊を起こす」
    /// 用途にそのまま使える。
    /// </summary>
    /// <summary>
    /// 出撃した味方のうち決着時に生存していない駒の <c>Def.Id</c>（第129期・<b>計数専用</b>）。
    /// <b>渡された <c>player</c> のリストそのもの</b>を見るので、戦闘中に湧いた駒は入らない
    /// （<c>BattleResult.PlayerStarterFallen</c> の doc を参照）。
    /// 欠けが無ければ割り当てを1つも作らない（`layout` は数百万戦を並列で回す）。
    /// </summary>
    static IReadOnlyList<string> FallenStarters(IReadOnlyList<UnitState> player)
    {
        List<string>? fallen = null;
        foreach (UnitState u in player)
            if (!u.IsAlive) (fallen ??= new List<string>()).Add(u.Def.Id);
        return fallen ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    public static List<UnitState> Materialize(Formation formation, int teamId)
    {
        var units = new List<UnitState>();
        foreach ((int slot, UnitDef def) in formation.Occupied())
        {
            units.Add(new UnitState
            {
                Def = def,
                TeamId = teamId,
                Slot = slot,
                Hp = def.MaxHp,
                MaxHp = def.MaxHp,
                Traits = TraitCatalog.Resolve(def.Traits)
            });
        }
        return units;
    }
}
