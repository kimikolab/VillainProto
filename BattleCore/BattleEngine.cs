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
///
/// <para><b>第187期に敵の難易度のつまみ（<see cref="Scale"/>）を相乗りさせた</b>——指示書が
/// 「<c>Run</c> の引数は増やさない（既存の規則の束に1本足す）」と決めたため。<c>Census</c> は計数だけだが、
/// <b><c>Scale</c> は盤面を動かす</b>（敵の最大HPと素の攻撃力）。</para>
/// </summary>
public readonly record struct BossRule(bool Census)
{
    /// <summary>既定は<b>数えない</b>。診断だけが <c>new BossRule(true)</c> を渡す。敵の倍率は採用値。</summary>
    public static BossRule Default => new(false);

    /// <summary>
    /// 敵の数値の倍率（第187期）。<b>渡さなければ <see cref="EnemyScaleRule.Default"/>（採用値）</b>。
    /// 欄は null を持ち、読むときに既定へ落とす——<c>default(BossRule)</c> や引数なしの <c>new BossRule()</c> で
    /// 倍率が (0, 0)（＝ HP 1）にならないため。<c>default(BossRule) == BossRule.Default</c> も保たれる。
    /// </summary>
    public EnemyScaleRule Scale { get => _scale ?? EnemyScaleRule.Default; init => _scale = value; }
    readonly EnemyScaleRule? _scale;
}

/// <summary>
/// 敵の難易度のつまみ（第187期）。<b>敵陣営の駒が盤面に出るとき、最大HPと素の攻撃力
/// （<see cref="UnitDef.Attack"/>）を百分率で一律に掛ける</b>（切り捨て・最低1）。
///
/// <para><b>味方には一切かけない。</b> 敵の特性・波ルールの数値（軛の上限・施しの量・処刑の伸び）・
/// 配置・速さ・攻撃型は1つも触らない。<b>掛ける口は2つだけ</b>——
/// <see cref="BattleEngine.Materialize(Formation, int)"/>（敵の編成から作るとき。会戦・作戦マップ・DemoApp も全部ここ）と、
/// <see cref="BattleContext.Summon"/>（<b>敵の駒が</b>敵陣に呼んだとき。ソムの餌は味方の仕組みなので掛けない）。
/// 会戦の持ち越しは同じ <see cref="UnitState"/> を使い回すので二重には掛からない。</para>
///
/// <para><b>掛け方は <see cref="UnitDef"/> の写しを <c>Def</c> に差す</b>——<c>Def.Attack</c> を直に読む箇所
/// （ムドの床・突きの素の倍率・味方巻き込みの基礎）が全部1本で揃う。<c>AtkBonus</c> には掛けない。
/// <b>(100, 100) は写しを作らない</b>ので現行と1ビットも違わない。</para>
/// </summary>
public readonly record struct EnemyScaleRule(int HpPercent, int AtkPercent)
{
    /// <summary>現行（倍率なし）。回帰の検算に使う。</summary>
    public static EnemyScaleRule None => new(100, 100);

    /// <summary>
    /// <b>採用値 ＝ S1（115 / 115）</b>（第187期 §3）。7 版のうち足切り（主判定19行の第五波 40%）の内側で
    /// 情報セル（`compare` 61 行 × 第2〜5波）が最大——112 → 149。参考の 5% 刻みでも 115 が頂上だった。
    /// </summary>
    public static EnemyScaleRule Adopted => new(115, 115);

    /// <summary>既定 ＝ <see cref="Adopted"/>。<see cref="None"/> に戻すと第186期と1ビットも違わない。</summary>
    public static EnemyScaleRule Default => Adopted;

    /// <summary>この規則が盤面を動かしうるか。偽なら写しを作らない。</summary>
    public bool Active => HpPercent != 100 || AtkPercent != 100;

    static readonly System.Collections.Concurrent.ConcurrentDictionary<(UnitDef, int, int), UnitDef> _cache = new();
    static readonly System.Collections.Concurrent.ConcurrentDictionary<UnitDef, EnemyScaleRule> _origin = new();

    /// <summary>
    /// その定義に掛かっている倍率（写しでなければ <see cref="None"/>）。<b>召喚は呼んだ敵と同じ倍率を引き継ぐ</b>
    /// ——作戦マップは倍率を掛けずに敵を作る（<c>Map11.EnemyScale</c>）ので、召喚だけ既定の倍率が掛かることが無い。
    /// </summary>
    public static EnemyScaleRule Of(UnitDef d) => _origin.TryGetValue(d, out EnemyScaleRule r) ? r : None;

    /// <summary>
    /// 倍率を掛けた定義。<b>同じ定義・同じ倍率には同じインスタンスを返す</b>（写しの同一性を揃えるため）。
    /// </summary>
    public UnitDef Apply(UnitDef d)
    {
        if (!Active) return d;
        return _cache.GetOrAdd((d, HpPercent, AtkPercent), static k =>
        {
            (UnitDef src, int hp, int atk) = k;
            UnitDef w = src.WithStats(Math.Max(1, src.MaxHp * hp / 100), Math.Max(1, src.Attack * atk / 100));
            _origin[w] = new EnemyScaleRule(hp, atk);
            return w;
        });
    }
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
    ///
    /// <para><b>第204期の注: 上の「ヒビだけ」は古い。</b> 供給源は 砕け（ヒビ）・引き受け（ウケ・<c>Dull</c> の中）・
    /// 身構え（ササ）・鱗（ウロ）に続いて、<b>施しのリリの溢れ（<see cref="KissTrait"/>）が5本目</b>。
    /// リリの供給は敵の最大HPが燃料なので「有限プールが天井」は成り立たない（上限は付けていない。帳簿は `lili ledger`）。</para>
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

    /// <summary>
    /// 組み付き（第185期・クグの <see cref="TraitId.Grapple"/>）。<b>0/1 のキー</b>で、立っている間は
    /// <b>自分の手番を失い続ける</b>（痺れと違って手番で消費されない。ほどくのは組み付いた側だけ）。
    /// ターン外の行動も止まる（<c>CanActOutOfTurn</c>）。責め苦は「動けない」と読む。
    /// </summary>
    public const string Grappled = "grappled";

    /// <summary>
    /// 竦み（第185期・シガの <see cref="TraitId.Shame"/>）。<b>0/1 のキー</b>で、次の手番を1回失う（手番で消費）。
    /// <b>痺れを流用しない</b>——痺れにはターン外の行動を止める意味と、責め苦・号令の読み手がいる。
    /// 竦みは「次の手番を1回」だけを表す（転倒と同じ形）。
    /// </summary>
    public const string Cowed = "cowed";

    /// <summary>
    /// 据えの層（第185期・バンの <see cref="TraitId.Footing"/>）。<b>量のキー</b>（0〜<see cref="FootingTrait.MaxLayers"/>）で、
    /// 1層ごとに被ダメ −10%。<see cref="All"/> に入れてあるので、会戦の境界で消え、状態の札として画面に出る。
    /// </summary>
    public const string Footing = "footing";

    /// <summary>
    /// 萎縮（第189期・クビの <see cref="TraitId.Daunt"/>）。<b>二値</b>で、立っている駒の<b>次の1回の攻撃</b>
    /// （<c>PerformAttack</c> 1回）のダメージが半分になり、その場で消える。竦み（<see cref="Cowed"/>）・痺れ・転倒は
    /// 手番そのものを消すが、萎縮は手番を残して一撃だけを軽くする——だから別のキー。
    /// <see cref="All"/> に入れてあるので会戦の境界で消える。
    /// </summary>
    public const string Daunted = "daunted";

    /// <summary>
    /// 濃縮の印（第194期・澱みのミオ・<see cref="TraitId.Concentrate"/>）。値は<b>刻みの追加回数 n</b>（重ねがけ可・上限なし）。
    /// 印が n の駒は、毒と燃焼の刻み（ターン頭の刻みと起爆）を<b>同じ量で 1+n 回</b>受ける
    /// ——層と残りターンの減算は1回分だけ。<b>戦闘中は消えない</b>（ミオが倒れても残る）。
    /// <see cref="All"/> に入れてあるので会戦の境界で消える。
    /// </summary>
    public const string Concentrated = "concentrated";

    /// <summary>
    /// 痺れ毒の印（第195期・毒吐きのスィド・<see cref="TraitId.Numb"/>）。<b>二値</b>（値は付いた経路＝
    /// <see cref="SpewTrait.OriginSpew"/> 吐いた ／ <see cref="SpewTrait.OriginVenom"/> 殴ってきた。<b>計数の帰属だけに使い、規則は読まない</b>）。
    /// 印のある駒は、与えるダメージ（<c>PerformAttack</c> の一撃）が<b>毒の層 × 3%</b>（上限 60%）下がる。
    /// <b>戦闘中は消えない</b>（スィドが倒れても残る）。毒の層が 0 なら減らない。
    /// <see cref="All"/> に入れてあるので会戦の境界で消える。
    /// </summary>
    public const string Numbed = "numbed";

    /// <summary>
    /// 紅蓮（第197期・<see cref="TraitId.Guren"/>・ベニの側）。啜りでもベニに入りきらなかった溢れの量（上限なし）。
    /// 溜める口は <c>BattleContext.InverseSip</c>、放つ口は <c>GurenTrait.Release</c>（放つと 0）。
    /// <see cref="All"/> に入れてあるので会戦の境界で消え、状態の札として画面に出る。
    /// </summary>
    public const string Guren = "guren";

    /// <summary>
    /// 聖痕（第204期・施しのリリ・<see cref="TraitId.Kiss"/>）。<b>二値</b>。リリに精気を吸われた敵に付く<b>数え札</b>で、
    /// <b>それ自体は何もしない</b>——読むのはリリの札だけ（まだ聖痕の無い敵を選ぶ／全員に付いたら祝福の儀／聖痕の敵が倒れたら祝福が還る）。
    /// 儀式が終わると全部消える。<b>移さない</b>（<see cref="KissTrait.Excluded"/>）。
    /// <see cref="All"/> に入れてあるので会戦の境界で消える。
    /// </summary>
    public const string Stigma = "stigma";

    /// <summary>
    /// 板の印（第207期・継ぎ当てのツギ・<see cref="TraitId.Plank"/>）。ツギが板（破片）を貼った味方に付く。
    /// <b>値は 1 ＝ 燃えにくい板（T1 の対照）／ 2 ＝ 燃えやすい板（<see cref="TraitId.PlankTinder"/> を持つツギが貼った）</b>。
    /// 読むのは燃焼の付与の2口（<c>Ignite</c> とリリの移し）だけで、値が 2 なら燃焼の残りターンを倍にする。
    /// <b>その駒の破片が 0 になった瞬間に消える</b>（<c>BattleContext.NoteArmorLost</c> の1点）。
    /// <b>移さない</b>（<see cref="KissTrait.Excluded"/>）。<see cref="All"/> に入れてあるので会戦の境界で消える。
    /// </summary>
    public const string Plank = "plank";

    /// <summary>
    /// 感電（第214期・禍導のカタ）。<b>0 か 1 の二値。層を持たない</b>（層を持つと毒と同じ軸になる）。
    /// <b>時間では消えない</b>——感電している駒が HP に届く被弾を受けると<b>起爆</b>し、感電が消えて
    /// 同じ陣営の隣接する駒すべてへ放電する（<see cref="BattleContext.ShockTrigger"/>）。
    /// <b>カタの雷と毒・燃焼の刻みは起爆しない</b>（刻みで起爆する版は札 <c>ShockTick</c>）。
    /// 書き手は <see cref="BattleContext.MarkShock"/> の1箇所。<see cref="All"/> に入れてあるので会戦の境界で消える。
    /// </summary>
    public const string Shock = "shock";

    /// <summary>
    /// 糸（第293期・クグの網 KW-a ／ KW-b）。値は張ったクグの <c>InstanceId + 1</c>（0 ＝ 糸なし）。書き手は <see cref="BattleContext.SpinWeb"/> の1箇所。
    /// 戦の終わりまで残る（組み付きがほどけても残る）・倒れたら消える。KW-a はターンの頭に帯電し直し、KW-b は速さ −3（行動順だけ・重ならない）。
    /// <b>カタの雷の「帯びた種類」には数えない</b>（<c>ThunderTrait.CountedKeys</c> に入れない）。<see cref="All"/> に入れてあるので会戦の境界で消える。
    /// </summary>
    public const string Web = "web";

    public static readonly string[] All = { Poison, Marked, Stun, Burn, IdleTurn, Armor, Wound, Deep, Curse, Stagger, Confused, Ward, Debt, Ash, Grappled, Cowed, Footing, Daunted, Concentrated, Numbed, Guren, Stigma, Plank, Shock, Web };

    /// <summary>
    /// 手番を奪う状態（第261期）: 痺れ・転倒・組み付き・竦み・混乱。<see cref="Trait.BlocksControl"/> の保持者には付かない（<c>UnitState.SetCounter</c> の入口）。
    /// まどろみ（巨躯の腹）と <c>CanAct</c> の否決は本人の札なので入れない。萎縮・毒の鈍りは一撃を軽くするだけなので入れない。
    /// </summary>
    public static readonly string[] Control = { Stun, Stagger, Grappled, Cowed, Confused };
    public static bool IsControl(string key) => key is Stun or Stagger or Grappled or Cowed or Confused;

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
        Grappled => "組",
        Cowed => "竦",
        Footing => "据",
        Daunted => "萎",
        Concentrated => "濃",
        Numbed => "鈍",
        Guren => "紅",
        Stigma => "聖",
        Plank => "板",
        Shock => "雷",
        Web => "糸",
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
/// 感電の規則（第214期）。<b>放電の量は仮置き</b>（指示書 §9・ポンが遊んで決める）。
/// </summary>
public static class ShockRule
{
    /// <summary>起爆した駒が、同じ陣営の隣接する駒1体ずつへ流す量。</summary>
    public const int Discharge = 8;

    /// <summary>感電で痺れる S3（第216期・<see cref="TraitId.ShockStunHalf"/>）の確率（%）。</summary>
    public const int StunHalfPercent = 50;

    /// <summary>
    /// 感電で付いた痺れの印（第216期・<b>計数専用</b>・私有キー）。痺れを消費した手番で読んで消す（失った手番を感電の分と、ほかの分に分ける）。
    /// </summary>
    public const string StunKey = "shockStun";

    /// <summary>
    /// 感電の痺れのハメ防止の印（第217期・G3H・<see cref="TraitId.LiveWireGuard"/>）。<b>痺れで手番を失った手番</b>に 1 を立て、
    /// <b>その駒の次の手番の頭</b>で 0 に戻す（竦みの <c>ShameTrait.GuardKey</c> と同じ形）。立っている間は感電で痺れない。私有キー（<see cref="StatusKeys.All"/> に入れない）。
    /// </summary>
    public const string GuardKey = "shockStunGuard";

    /// <summary>手番を続けて失った数（第217期・<b>計数専用</b>・私有キー）。手番を失うたびに +1、動いたら 0。</summary>
    public const string StallRunKey = "stallRun";
}

/// <summary>
/// ターン外の行動の呼び出し口（第134期 段2）。<b>計数専用で、どの規則も読まない</b>
/// ——<see cref="BattleContext.CanActOutOfTurn"/> の答えを1ビットも変えない。
///
/// <para><b>呼び出し口は9本</b>（第198期に斬り返しが8本目・第210期に応急処置が9本目）。<c>design/ENGINE_HOOKS.md</c>（第259期までは <c>CLAUDE.md</c>）は第27期以来「棘・仇討ち・軋み・追い打ちの
/// 4本だけ」と書いていたが、<b>第110期の譲渡（尾灯・<c>TaillightTrait</c>）が5本目として
/// 増えていた</b>（第134期 Q0-7 の走査で判明）。<b>第180期に暴発（<c>EruptTrait</c>）と
/// 叩き起こし（<c>ReveilleTrait</c>）が 6・7 本目になった</b>
/// ——<b>叩き起こしだけは問う相手が自分ではなく「起こされる味方」</b>で、
/// 他の6本（自分がターン外に動けるか）とはここが違う。</para>
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
    /// <summary>暴発（<c>EruptTrait.OnDamaged</c>・第180期）。</summary>
    Erupt,
    /// <summary>叩き起こし（<c>ReveilleTrait.OnAfterAttack</c>・第180期。<b>問う相手は起こされる味方</b>）。</summary>
    Reveille,
    /// <summary>斬り返し（<c>LastStandTrait.OnDamaged</c>・第198期。<b>倒れる一撃でも問う</b>——「生きている」だけを外す）。</summary>
    LastStand,
    /// <summary>応急処置（<c>FirstAidTrait</c>・第210期。<b>問う相手はツギ</b>——貼られる味方ではない）。</summary>
    FirstAid,
    /// <summary>追い撃ち（<c>EvadeTrait</c>・第223期。避けた後の撃ち返し。<b>避けること自体と入れ替えは問わない</b>——行動ではない）。</summary>
    Evade,
    /// <summary>緊急退避（<c>RetreatTrait</c>・第225期。<b>問う相手はシオ</b>——下げられる味方ではない）。</summary>
    Retreat,
    /// <summary>動かされて吹く突風（<c>SquallTrait</c>・第226期。<b>問う相手はバサ</b>）。</summary>
    Squall,
    /// <summary>弾き返し（<c>SpringTrait</c>・第228期。<b>問う相手はハネ</b>）。</summary>
    Spring,
    /// <summary>移動の追撃（<c>EvadeMoveShotTrait</c>・第231期。<b>問う相手はセロ</b>）。</summary>
    MoveShot,
    /// <summary>感電の割り込み（<c>ShockWhipTrait</c>・第289期。<b>問う相手はシガ</b>）。</summary>
    ShockWhip,
    /// <summary>橋（<c>BeckonBridge</c>・第294期 HS-c。<b>問う相手は癒し手</b>——踏みとどまった味方ではない）。</summary>
    Bridge,
    /// <summary>ミサの羽が標の付いた駒へ飛ぶ（第298期・MF-a ／ MF-b・`DrainFeatherMarks`）。</summary>
    FeatherMark,
    /// <summary>呼び出し口を名乗らなかった問い合わせ（既定値。<b>現状 0 件</b>）。</summary>
    Other
}

/// <summary><see cref="OutOfTurnRoute"/> の一覧（帳簿の添字用）。</summary>
public static class OutOfTurnRoutes
{
    /// <summary>経路の名前（<see cref="OutOfTurnRoute"/> の順）。</summary>
    public static readonly string[] Names =
        { "棘", "仇討ち", "軋み", "追い打ち", "譲渡", "暴発", "叩き起こし", "斬り返し", "応急処置", "追い撃ち", "緊急退避", "突風", "弾き返し", "移動の追撃", "感電の割り込み", "橋", "羽の標撃ち", "その他" };

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
        if (_rallyLive) BundlePush(null, outOfTurn: true);   // 第295期（攻撃のひとまとまり・HK の保持者がいなければ比較1つで抜ける）
        try { body(); }
        finally { InReaction = false; if (_rallyLive) BundlePop(); }
        if (_mfLive) DrainFeatherMarks();   // 第298期（MF・まとまりが閉じた後に、控えた羽を撃つ）
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
    /// <param name="dying">
    /// 第198期。<b>倒れる一撃の中で問う</b>（剣の段の相打ち）。真なら「生きている」だけを外し、残りの門（痺れ・組み付き・札・粛）は同じ。
    /// <b>既定（偽）の呼び出しは答えが1ビットも変わらない。</b>
    /// </param>
    public bool CanActOutOfTurn(UnitState u, OutOfTurnRoute route = OutOfTurnRoute.Other, bool dying = false)
    {
        // **式のままだと「粛が単独の原因だったか」が数えられない**ので、第134期に
        // 節へほどいた。**評価の順序も結果も第27期から1ビットも変えていない**——
        // 保持者の走査は `AllUnits.Any(...)` から `_hushHolders`（`Add` が積む）へ寄せてあり、
        // 短絡の意味（数百万戦を並列で回すので全駒走査を後ろに置く）はそのまま残る。
        bool basic = (u.IsAlive || dying)
                     && u.RawCounter(StatusKeys.Stun) == 0
                     && (!_restrainLive || u.RawCounter(StatusKeys.Grappled) == 0)   // 第185期: 組み付かれた駒
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
        if (_rallyLive) BundlePush(null, outOfTurn: true);   // 第295期（攻撃のひとまとまり）
        try { body(); }
        finally { InInterrupt = false; if (_rallyLive) BundlePop(); }   // 例外で立ちっぱなしになると以後の割り込みが永久に止まる
        if (_mfLive) DrainFeatherMarks();   // 第298期（MF）
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
    /// <summary>ターン頭（刻みの前）の燃焼の在り方を数える（第219期・<b>計数のみ</b>）。</summary>
    void NoteBurnPresence()
    {
        BrittleLedger b = BrittleBook;
        b.Turns++;
        int t = Math.Clamp(_turn, 0, 30);
        bool any0 = false, any1 = false;
        foreach (UnitState u in _units)
        {
            if (!u.IsAlive) continue;
            int side = SideOf(u);
            b.UnitTurns[side]++;
            b.AliveByTurn[side][t]++;
            if (u.RawCounter(StatusKeys.Burn) <= 0) continue;
            b.BurnUnitTurns[side]++;
            b.BurnByTurn[side][t]++;
            if (side == 0) any0 = true; else any1 = true;
        }
        if (any0) b.TurnsAnyBurn[0]++;
        if (any1) b.TurnsAnyBurn[1]++;
    }

    public void TickStatuses()
    {
        NoteRuleHolders();   // 第134期 段2 —— 保持者が落ちたターンの記録。**盤面には触らない。**
        NoteBurnPresence();  // 第219期 —— 刻みの前に燃えている駒を数える。**盤面には触らない。**
        if (_fireArmorLive || _fireWardHolders.Count > 0) NoteFireWardCensus();   // 第238期 —— **計数のみ**

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

            int total = TickTotal(u);   // 表示専用（何回目か／全部で何回か）
            PoisonTickOnce(u, poison, second: false, TickOrd(1, total));
            // 濃縮の印（第194期・ミオ）。**印の数だけ同じ量でもう1回ずつ刻む**（毒の層は刻みで減らないので、1回だけにするものは無い）。
            // 倒れた駒には次の回を当てない。印が1つも無い戦闘は旗1本で抜ける。
            if (_markLive) RepeatTick(u, k => PoisonTickOnce(u, poison, second: true, TickOrd(k, total)));
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

            // 第245期: 刻みの回数と脆さは、残りターンを減らす前の火勢で読む（最後の刻みは減らした後に刻むので `Of` は 0 を返す）。
            int preLv = _foeFireLive ? FireLevelRule.Of(u) : 0;
            int extra = _foeFireLive && _lvTickTeams[u.TeamId] && preLv > 1 ? preLv - 1 : 0;
            if (_burnHitLive && _splitTickTeams[u.TeamId]) extra = 0;   // 第255期（分担）: ターン頭の刻みは火勢に関わらず 6 × 1
            // 第249期（刻み・一撃）: 火勢の回数を「6 × 火勢 を1回」にまとめる（札 `TickOnce` の持ち主がいるときだけ）。
            if (_tickOnceLive && extra > 0) { _burnTickAmt = BurnRules.Damage * (extra + 1); FireBook.TickOnceN[Math.Clamp(preLv, 0, 4)]++; extra = 0; }
            u.SetCounter(StatusKeys.Burn, left - 1);
            // 第134期 段1 —— 燃え尽きた時点で区間を閉じる。**盤面には触らない。**
            if (left - 1 <= 0) CloseBurnEpisode(u, expired: true);
            if (left - 1 <= 0 && _fireLvLive) FireOut(u);   // 第242期（火勢: 燃焼が切れたら 0）

            int total = TickTotal(u) + extra;   // 表示専用
            _inBurnTickNow = true;   // 第233期・**計数のみ**
            if (_foeFireLive) { _tickLvUnit = u; _tickLv = preLv; }
            int hb0 = u.Hp;
            BurnTickOnce(u, left, bt, second: false, TickOrd(1, total));
            if (_foeFireLive) NoteLvTick(u, preLv, hb0, extraTick: false);
            // 第245期: 火勢の回数（燃焼の刻み 6 を火勢の回数だけ別々に・倒れたら止める）。**残りターンの減算と区間の帳簿は上の1回だけ。**
            for (int k = 0; k < extra && u.IsAlive; k++)
            {
                int hb = u.Hp;
                BurnTickOnce(u, left, bt, second: true, TickOrd(k + 2, total));
                NoteLvTick(u, preLv, hb, extraTick: true);
            }
            // 濃縮の印（第194期）。**残りターンの減算と区間の帳簿は上の1回だけ**で、刻みの本体を印の数だけもう1回ずつ（第245期の火勢の回数の後ろに足し算）。
            if (_markLive) RepeatTick(u, k => BurnTickOnce(u, left, bt, second: true, TickOrd(k + extra, total)));
            _tickLvUnit = null;
            _burnTickAmt = 0;
            _inBurnTickNow = false;
            if (_burnHitLive)   // 第255期・**計数のみ**（ターン頭の刻みの量）
            {
                int sd = BhSide(u), dd = Math.Max(0, u.Hp) - hb0;
                BurnHitBook.TickFires[sd]++;
                if (dd < 0) BurnHitBook.TickDmg[sd] -= dd; else BurnHitBook.TickHeal[sd] += dd;
            }
            if (left - 1 <= 0 && u.RawCounter(GurenTrait.BurnKey) > 0) u.SetCounter(GurenTrait.BurnKey, 0);   // 第197期・**計数のみ**
        }
    }

    /// <summary>第245期: 燃焼の刻み1回ぶんの帳簿（<b>計数のみ</b>）。刻みの前後の HP の差で、削った量と回復した量（火の変換・ベニ・火の癒し）を分ける。</summary>
    void NoteLvTick(UnitState u, int lv, int hpBefore, bool extraTick)
    {
        int l = Math.Clamp(lv, 0, 4);
        int d = Math.Max(0, u.Hp) - hpBefore;
        bool foe = _foeFireTeams[u.TeamId];
        if (foe)
        {
            if (extraTick) { FireBook.FoeTickExtraN[l]++; FireBook.FoeTickExtraHp[l] += Math.Max(0, -d); }
            else { FireBook.FoeTickN[l]++; FireBook.FoeTickHp[l] += Math.Max(0, -d); }
            return;
        }
        if (!_fireLvTeams[u.TeamId]) return;
        if (extraTick) { FireBook.AllyTickExtraN[l]++; if (d < 0) FireBook.AllyTickExtraDmg[l] -= d; else FireBook.AllyTickExtraHeal[l] += d; if (!u.IsAlive) FireBook.AllyTickExtraDeaths++; }
        else { FireBook.AllyTickN[l]++; if (d < 0) FireBook.AllyTickDmg[l] -= d; else FireBook.AllyTickHeal[l] += d; if (!u.IsAlive) FireBook.AllyTickDeaths++; }
    }

    /// <summary>
    /// 毒の刻み1回ぶんの本体（第194期に <see cref="TickStatuses"/> から切り出した。<b>中身は1文字も変えていない</b>）。
    /// <paramref name="second"/> は濃縮の印の2回目（計数の帰属だけに使う）。
    /// </summary>
    void PoisonTickOnce(UnitState u, int poison, bool second, (int? Index, int? Count) ord)
    {
        {
            NoteTickLayer(u, poison, burn: false, second);   // 第194期・**計数のみ**

            // 反転（第190期・ベニ）。隣の味方の刻みは `ApplyDamage` を通らず回復になる。
            // **刻みの計数（業・毒の刻み・着火の持続係数）には写さない**（自前の帳簿に数える）。
            UnitState? inverter = InvertsTick(u);
            if (inverter is not null)
            {
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.Status, Turn = _turn, TargetId = u.InstanceId, Amount = poison, Text = "毒",
                    SourceTrait = TraitId.Inverse, InverterId = inverter.InstanceId,
                    TickIndex = ord.Index, TickCount = ord.Count,
                });
                int hb = u.Hp;
                InverseHeal(inverter, u, poison, 0, "毒");
                if (_openingLive) NoteOpeningTick(u, poison, true, u.Hp - hb);   // 第216期・**計数のみ**
                return;
            }
            if (_openingLive) NoteOpeningTick(u, poison, false, 0);   // 第216期・**計数のみ**
            NoteTaintPostBite(u, poison);   // 第190期・**計数のみ**
            NoteGurenPoisonTick(u, poison, second);   // 第197期・**計数のみ**
            Log($"    {u.Name} は毒に蝕まれている（{poison}）", LogKind.Status);
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.Status,
                Turn = _turn,
                TargetId = u.InstanceId,
                Amount = poison,
                Text = "毒",
                TickIndex = ord.Index, TickCount = ord.Count,
            });
            // 業（第49期）の帰属。**保持者が盤上にいなければ1回も走らない**（短絡）。
            if (ScapegoatActive) NoteScapegoatDot(u, poison, StatusKeys.Poison);
            NotePoisonBite(u, poison);   // 第61期の計数。盤面には触らない
            if (_arrowPoison.Count > 0 && _arrowPoison.TryGetValue(u.InstanceId, out var arr))   // 第224期・**計数のみ**
                TallyOf(arr.Sero).ArrowTickDealt += Math.Min(arr.Layers, poison);

            // 傷口の着火の帰属（第87期・持続係数の分子）。**盤面には触らない。**
            // 着火の時点でこの駒の毒は 0 だったので、以後の刻みはすべて着火の下流にある。
            if (u.RawCounter(AmplifierTrait.IgnitedKey) > 0)
            {
                UnitTally it = TallyOf(u);
                it.IgnitePoisonDamage += poison;
                it.IgnitePoisonTicks++;
            }

            MarkTickHit();   // 第214期（刻みは K2 のときだけ感電を起爆する）
            ApplyDamage(u, poison, null);
        }
    }

    /// <summary>
    /// 開戦の撒きの毒の刻み（第216期・<b>計数のみ</b>）。刻んだ層のうち開戦の撒きの分（<c>min(撒いた層, 今の層)</c>）を、
    /// 受けた駒の側に「反転で回復になった（名目・実際に癒えた分は按分）」「削られた（名目）」に分けて数える。
    /// </summary>
    void NoteOpeningTick(UnitState u, int poison, bool inverted, int healed)
    {
        int h = u.RawCounter(OpeningSprayTrait.HeldKey);
        if (h <= 0 || poison <= 0) return;
        int share = Math.Min(h, poison);
        UnitTally t = TallyOf(u);
        if (inverted) { t.OpeningTickInverted += share; t.OpeningHealed += (long)healed * share / poison; }
        else t.OpeningTickBitten += share;
    }

    /// <summary>
    /// 燃焼の刻み1回ぶんの本体（第194期に <see cref="TickStatuses"/> から切り出した。<b>中身は1文字も変えていない</b>
    /// ——残りターンの減算と区間の帳簿は呼ぶ側に残した）。<paramref name="left"/> は減らす前の残りターン（ログ用）。
    /// </summary>
    void BurnTickOnce(UnitState u, int left, UnitTally bt, bool second, (int? Index, int? Count) ord)
    {
        {
            if (second && _inBurnHit == 0) bt.BurnTicks++;   // 刻みの回数（計数）。印の2回目も1回と数える（第255期: 被弾の燃焼は数えない）
            // 第249期（刻み・一撃）: 1回の量（既定は `BurnRules.Damage`・まとめた刻みでは 6 × 火勢）。
            int baseD = _burnTickAmt > 0 ? _burnTickAmt : BurnRules.Damage;
            NoteTickLayer(u, baseD, burn: true, second);   // 第194期・**計数のみ**

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
            if (Ember.Fireproof && (u.HasTrait(TraitId.Pyre) || (_fireArmorLive && FireproofArmor(u))))   // 第234期: 火の鎧も焼かれない
            {
                // 第235期: 火の癒し（H1）。「焼かれない」を置き換え、刻みの量だけ回復する（火の回復・ベニの反転の裏は通らない）。
                if (_fireConvertHolders.Count > 0) NoteConvertPrec(u, ScorchTick(u, baseD), MendsFire(u) ? 1 : 0);   // 第238期・**計数のみ**
                if (MendsFire(u)) { FireHeal(u, ScorchTick(u, baseD), FireArmorLabels.Mend, tick: true); return; }   // 第249期: ホタの火の癒しも
                Log($"    {u.Name} は燃えているが焼かれない（残り {left - 1}）", LogKind.Status);
                // ノブ（既定 0 ＝ 1ビットも動かない）。**`ctx.Heal` を通す**ので、
                // 渇き（盤面ルール）にも支援拒否（`Stoic`）にも素直に課税される。
                if (Ember.TickHeal > 0) TickHealAttr(u);
                return;
            }

            // 第208期: 燃えやすい板（ダメージ倍）。**反転の枝より前**で倍にする——化けた回復も倍（指示書 §2.2・Q0-3）。
            int burnDmg = ScorchTick(u, baseD);

            // 反転（第190期・ベニ）。燃焼の残りターンは上で普通に減っている。
            UnitState? inverterB = InvertsTick(u);
            if (inverterB is not null)
            {
                if (_fireConvertHolders.Count > 0) NoteConvertPrec(u, burnDmg, 2);   // 第238期・**計数のみ**
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.Status, Turn = _turn, TargetId = u.InstanceId, Amount = burnDmg, Text = "燃焼",
                    SourceTrait = TraitId.Inverse, InverterId = inverterB.InstanceId,
                    ActorId = _inBurnHit > 0 ? _burnHitBy?.InstanceId : null,   // 第255期: 被弾の燃焼なら一撃の主
                    TickIndex = ord.Index, TickCount = ord.Count,
                });
                InverseHeal(inverterB, u, burnDmg, 1, "火");
                ClearKindleHeld(u);
                return;
            }
            // 第238期: 火の変換（ヒヨ・V）。熾火・火の癒し・ベニの結界の後。刻みを受ける代わりに回復（ベニの反転と同じく刻みの計数には写さない）。
            if (_fireConvertHolders.Count > 0 && FireConvertHolder(u) is UnitState hiyoB)
            {
                FireConvert(hiyoB, u, burnDmg, tick: true);
                ClearKindleHeld(u);
                return;
            }
            NoteKindlePostBurn(u);   // 第191期・**計数のみ**
            NoteGurenBurnTick(u, second);   // 第197期・**計数のみ**
            ClearKindleHeld(u);

            if (_inBurnHit > 0) Log($"    {u.Name} は叩かれて炎が燃え上がった（{burnDmg}）", LogKind.Status);   // 第255期（被弾の燃焼）
            else Log($"    {u.Name} が燃えている（残り {left - 1}）", LogKind.Status);
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.Status,
                Turn = _turn,
                ActorId = _inBurnHit > 0 ? _burnHitBy?.InstanceId : null,   // 第255期: 被弾の燃焼なら一撃の主（刻みは null のまま）
                TargetId = u.InstanceId,
                Amount = burnDmg,
                Text = "燃焼",
                TickIndex = ord.Index, TickCount = ord.Count,
            });
            if (ScapegoatActive) NoteScapegoatDot(u, burnDmg, StatusKeys.Burn);
            // burnTick: この刻みが破片に吸われた量・HP を削った量を、
            // 毒の刻み（同じく source が null）と混ぜずに数えるための札。**盤面には影響しない。**
            MarkTickHit();   // 第214期
            // 第219期: 燃焼の刻みそのもの（最後の刻みは残りターンを減らした後に刻むので、札で「燃えている」と渡す）。
            if (Ember.Brittle > 0)
            {
                _burnTickSelf = true;
                if (burnDmg > baseD && BrittleApplies(u)) BrittleBook.PlankTimesBrittle++;   // 計数のみ
            }
            ApplyDamage(u, burnDmg, null, burnTick: true);
            _burnTickSelf = false;
            if (!u.IsAlive) bt.BurnDeaths++;
        }
    }

    /// <summary>
    /// 起爆（第188期・触媒のカタ・<see cref="CatalystTrait"/>）。<b>敵全体の毒と燃焼を、その場でもう1回働かせる。</b>
    ///
    /// <para><b><see cref="TickStatuses"/> は1文字も触らない。</b> 1体ぶんの刻みの本体（毒は層の数 ×
    /// 同じ陣営のベニ、燃焼は <see cref="BurnRules.Damage"/>、熾のホタは焼かれない）だけを写す
    /// ——同じ量・同じ計算。違うのは3点: <b>層も残りターンも減らさない</b>／刻みの計数（持続係数・業など）を
    /// 足さない（ターン頭の刻みを数える量なので）／<b>毒と燃焼を両方帯びた敵には
    /// <see cref="CatalystTrait.DualMultiplier"/> 倍</b>（味方には掛けない）。</para>
    ///
    /// <para><b>出どころは null</b>（刻みと同じ）。撃破者はいないので <c>OnKill</c> は走らないが、
    /// 死亡処理（ラウの飛散・ゾトの破裂・リィカの層）は <c>ApplyDamage</c> の中でその場で走る。
    /// <b>回すのは手番の開始時点の生存者の写し</b>で、順は 敵全体 → 味方全体（どちらも席番号順・
    /// <b>乱数を引かない</b>）。途中で書かれた状態（破裂の着火・飛散の毒）は、写しの後ろの駒には同じ起爆で効く。</para>
    ///
    /// <para><paramref name="backfire"/> が偽（代金の札を外した <c>yP</c>）なら味方は起爆しない。</para>
    /// </summary>
    /// <returns>起爆したか（対象に毒・燃焼が1つも無ければ偽＝何も起きない）。</returns>
    public bool Detonate(UnitState self, bool backfire)
    {
        IReadOnlyList<UnitState> foes = LivingMembers(Opponent(self.TeamId));
        IReadOnlyList<UnitState> allies = backfire ? LivingMembers(self.TeamId) : Array.Empty<UnitState>();
        UnitTally kt = TallyOf(self);

        static bool Charged(UnitState u)
            => u.RawCounter(StatusKeys.Poison) > 0 || u.RawCounter(StatusKeys.Burn) > 0;
        if (!foes.Any(Charged) && !allies.Any(Charged))
        {
            kt.DetonateDry++;
            Log($"    {self.Name} は弾けさせる毒も火も見当たらない", LogKind.Status);
            return false;
        }

        kt.DetonateFires++;
        EmitSkill(self, new UnitAction(ActionKind.Skill, Label: "起爆"));
        Log($"    ★ {self.Name} が触媒を撒いた——毒と火が一斉に弾ける", LogKind.Highlight, self);

        foreach (UnitState u in foes) DetonateTwiceIfMarked(self, kt, u, dual: true);
        foreach (UnitState u in allies) DetonateTwiceIfMarked(self, kt, u, dual: false);
        return true;
    }

    /// <summary>
    /// 濃縮の印（第194期）。印が n の駒は起爆も<b>同じ本体を 1+n 回</b>（起爆は層も残りターンも減らさないので、
    /// 1回だけにすべきものが無い・Q0-10 / Q0-11）。倒れた駒には次の回を当てない。
    /// </summary>
    void DetonateTwiceIfMarked(UnitState kata, UnitTally kt, UnitState u, bool dual)
    {
        int total = TickTotal(u);   // 表示専用
        DetonateOne(kata, kt, u, dual, TickOrd(1, total));
        if (_markLive) RepeatTick(u, k => { kt.DetonateMarkedAgain++; DetonateOne(kata, kt, u, dual, TickOrd(k, total)); });
    }

    /// <summary>
    /// 刻み1回ぶんを 1+n 回に広げたときの全体の回数（第194期の台本・<b>表示専用</b>）。印が無ければ 1。
    /// 印は刻みの途中では増えないので、最初の1回の前に読んでよい。
    /// </summary>
    int TickTotal(UnitState u) => _markLive ? 1 + Math.Max(0, u.RawCounter(StatusKeys.Concentrated)) : 1;

    /// <summary>台本の <c>TickIndex</c> / <c>TickCount</c>。<b>印で繰り返す刻み（全部で2回以上）のときだけ</b>値を入れる。</summary>
    static (int? Index, int? Count) TickOrd(int index, int total)
        => total > 1 ? (index, total) : (null, null);

    /// <summary>
    /// 濃縮の印（第194期）。印の数 n だけ <paramref name="again"/> を呼ぶ（倒れたらそこで止める）。
    /// 1回の刻みで発火した回数（1 + 実際に呼んだ回数）を駒の帳簿に写す。<b>乱数を引かない。</b>
    /// </summary>
    void RepeatTick(UnitState u, Action<int> again)
    {
        int n = u.RawCounter(StatusKeys.Concentrated);
        int done = 0;
        for (int k = 0; k < n && u.IsAlive; k++) { again(k + 2); done++; }   // 引数は何回目か（2 始まり・表示専用）
        if (n <= 0) return;
        UnitTally t = TallyOf(u);
        if (1 + done > t.TickFiresMax) t.TickFiresMax = 1 + done;
    }

    void DetonateOne(UnitState kata, UnitTally kt, UnitState u, bool dual, (int? Index, int? Count) ord)
    {
        if (!u.IsAlive) return;
        int poison = u.RawCounter(StatusKeys.Poison);
        int burn = u.RawCounter(StatusKeys.Burn);
        if (poison <= 0 && burn <= 0) return;

        bool foe = u.TeamId != kata.TeamId;
        int mult = dual && poison > 0 && burn > 0 ? CatalystTrait.DualMultiplier : 1;
        if (mult > 1) kt.DetonateDualTargets++;

        if (poison > 0)
        {
            // 刻みと同じ計算（ベニの味方の毒 ×2 は、その駒の陣営にベニが生きているときだけ）。
            if (_units.Any(x => x.IsAlive && x.TeamId == u.TeamId && x.HasTrait(TraitId.Devour)))
                poison *= DevourTrait.AllyPoisonMultiplier;
            int dmg = poison * mult;
            // 反転（第190期）。**味方側だけ**——隣にベニがいれば弾けた毒は薬になる（起爆の帳簿には載せない）。
            UnitState? inverter = foe ? null : InvertsTick(u);
            if (inverter is not null)
            {
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.Status, Turn = _turn, ActorId = kata.InstanceId,
                    TargetId = u.InstanceId, Amount = dmg, Text = "毒",
                    SourceTrait = TraitId.Inverse, InverterId = inverter.InstanceId,
                    TickIndex = ord.Index, TickCount = ord.Count,
                });
                InverseHeal(inverter, u, dmg, 2, "弾けた毒");
            }
            else
            {
                if (foe) { kt.DetonatePoisonNominal += dmg; kt.DetonateDualExtra += dmg - poison; }
                else kt.DetonateAllyNominal += dmg;
                if (dmg > Yoke.Cap && YokeBinding) kt.DetonateYokeCut++;
                Log($"    {u.Name} の毒が弾けた（{dmg}）", LogKind.Status);
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.Status, Turn = _turn, ActorId = kata.InstanceId,
                    TargetId = u.InstanceId, Amount = dmg, Text = "毒",
                    TickIndex = ord.Index, TickCount = ord.Count,
                });
                DetonateHit(kt, u, foe, () => { MarkTickHit(); ApplyDamage(u, dmg, null); });   // 第214期: 起爆は刻みの写し
            }
        }

        if (burn > 0 && u.IsAlive)
        {
            if (Ember.Fireproof && (u.HasTrait(TraitId.Pyre) || (_fireArmorLive && FireproofArmor(u))))   // 第234期: 火の鎧も焼かれない
            {
                if (!foe && _fireConvertHolders.Count > 0) NoteConvertPrec(u, ScorchTick(u, BurnRules.Damage), MendsFire(u) ? 1 : 0);   // 第238期・**計数のみ**
                // 火には焼かれない（刻みと同じ枝）。第235期: 火の癒しなら刻みの量だけ回復（倍は掛けない＝刻みと同じ量）。第249期: ホタの火の癒しも。
                if (MendsFire(u)) FireHeal(u, ScorchTick(u, BurnRules.Damage), FireArmorLabels.Mend, tick: true);
                else if (Ember.TickHeal > 0) TickHealAttr(u);
            }
            else if ((foe ? null : InvertsTick(u)) is UnitState inverterB)
            {
                if (_fireConvertHolders.Count > 0) NoteConvertPrec(u, BurnRules.Damage * mult * ((u.RawCounter(StatusKeys.Plank) & PlankTrait.Scorch) != 0 ? 2 : 1), 2);   // 第238期・**計数のみ**
                mult *= (u.RawCounter(StatusKeys.Plank) & PlankTrait.Scorch) != 0 ? 2 : 1;   // 第208期: 燃えやすい板
                // 反転（第190期）。弾けた火も、隣のベニの前では薬になる。
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.Status, Turn = _turn, ActorId = kata.InstanceId,
                    TargetId = u.InstanceId, Amount = BurnRules.Damage * mult, Text = "燃焼",
                    SourceTrait = TraitId.Inverse, InverterId = inverterB.InstanceId,
                    TickIndex = ord.Index, TickCount = ord.Count,
                });
                InverseHeal(inverterB, u, BurnRules.Damage * mult, 2, "弾けた火");
            }
            else if (!foe && _fireConvertHolders.Count > 0 && FireConvertHolder(u) is UnitState hiyoD)
            {
                FireConvert(hiyoD, u, ScorchTick(u, BurnRules.Damage * mult), tick: true);   // 第238期: 火の変換（弾けた火も刻みと同じ）
            }
            else
            {
                int dmg = ScorchTick(u, BurnRules.Damage * mult);   // 第208期: 燃えやすい板
                if (foe) { kt.DetonateBurnNominal += dmg; kt.DetonateDualExtra += dmg - BurnRules.Damage; }
                else kt.DetonateAllyNominal += dmg;
                Log($"    {u.Name} の火が弾けた（{dmg}）", LogKind.Status);
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.Status, Turn = _turn, ActorId = kata.InstanceId,
                    TargetId = u.InstanceId, Amount = dmg, Text = "燃焼",
                    TickIndex = ord.Index, TickCount = ord.Count,
                });
                DetonateHit(kt, u, foe, () => { MarkTickHit(); ApplyDamage(u, dmg, null, burnTick: true); });
            }
        }
    }

    /// <summary>起爆の1段が実際に削った HP と、倒した数（<b>計数のみ</b>）。</summary>
    static void DetonateHit(UnitTally kt, UnitState u, bool foe, Action hit)
    {
        int before = u.Hp;
        hit();
        int removed = before - Math.Max(0, u.Hp);
        if (foe) kt.DetonateFoeDealt += removed; else kt.DetonateAllyDealt += removed;
        if (!u.IsAlive)
        {
            if (foe) kt.DetonateFoeKills++; else kt.DetonateAllyKills++;
        }
    }

    // =================================================================================
    // 第214期 —— 感電（StatusKeys.Shock）と雷（ThunderTrait）
    //
    // **起爆の判定は `ApplyDamageBody` の1箇所**（HP を引いて死亡処理を通した直後）で、ここにあるのは
    // その先（幅優先の放電）と、雷の1発の窓口と、帳簿だけ。**感電が1度も書かれていない戦闘は `_shockLive` の比較1つで抜ける。**
    // =================================================================================

    /// <summary>感電が1度でも書かれたか（書かれるまで起爆の判定を1度も走らせない）。</summary>
    bool _shockLive;

    /// <summary>刻みでも起爆する札（<see cref="TraitId.ShockTick"/>・K2）の保持者が戦闘に出たか。</summary>
    bool _shockTickLive;

    /// <summary>雷で弾ける餌の札（<see cref="TraitId.BetrayedShockThunderPop"/>・第276期 S1p）の保持者が戦闘に出たか。立っていなければ雷は従来どおり起爆しない。</summary>
    bool _thunderPopLive;

    /// <summary>
    /// 感電で痺れる（第216期）: 0 なし ／ 1 起点だけ（S1）／ 2 弾けた駒すべて（S2）／ 3 それぞれ 50%（S3）。
    /// 保持者（<see cref="TraitId.ShockStun"/> ほか）が戦闘に出たときに立つ。複数の版が同席したら番号の大きいほう（診断の外では起きない）。
    /// </summary>
    byte _shockStun;

    /// <summary>開戦の撒き（第216期）が1度でも走ったか（<b>計数専用</b>・毒の刻みの帳簿を短絡させる）。</summary>
    bool _openingLive;

    /// <summary>開戦の撒きの札（<see cref="OpeningSprayTrait"/>）が呼ぶ（<b>計数専用</b>）。</summary>
    public void NoteOpeningLive() => _openingLive = true;

    // =================================================================================
    // 第217期 —— 鞭（責め鞭・鞭・電気鞭のシガ）
    //
    // **2倍の判定はここの `WhipAmount` の1本**（`PerformAttackBody` が主目標と巻き込みの `ApplyDamage` の直前に呼ぶ）。
    // 1回の攻撃の間だけ枠（`WhipSwing`）を立て、当たった駒と「振り始めに感電していたか」を控える。
    // 怖気づき・悲鳴・電気鞭は枠を読む札の側（`OnAfterAttack`）。**保持者（`Scourge`）がいなければ `_whipLive` の比較1つで抜ける。**
    // =================================================================================

    bool _whipLive;

    /// <summary>感電の痺れのハメ防止（G3H・<see cref="TraitId.LiveWireGuard"/>）の保持者が戦闘に出たか。</summary>
    bool _shockStunGuard;

    /// <summary>1回の攻撃（鞭の一振り）の枠。<b>乱数を引かない。</b></summary>
    public sealed class WhipSwing
    {
        public required UnitState Actor { get; init; }
        /// <summary>振り始めに感電していた（電気鞭の札を持つときだけ真）。</summary>
        public bool Wired { get; init; }
        /// <summary>感電している敵も2倍に数える（参考 G3K）。</summary>
        public bool CountShock { get; init; }
        /// <summary>当たった駒（主目標が先頭・巻き込みは当てた順）。</summary>
        public readonly List<UnitState> Hits = new();
        public long PopsBefore;
        /// <summary>第288期: 雷霆の手番（蓄電が上限で振り始めた・<see cref="TraitId.Thunderclap"/> の保持者だけ真）。</summary>
        public bool Bolt { get; init; }
        /// <summary>第288期（参考 SG-c′）: 雷霆の的を当たった敵すべてにする。</summary>
        public bool BoltAny { get; init; }
        /// <summary>第289期: 割り込みの鞭（怖気の判定から外す）。</summary>
        public bool NoCower { get; init; }
        /// <summary>第288期: 当たったとき感電していた敵（雷霆の的・当てた順）。</summary>
        public readonly List<UnitState> BoltTargets = new();
    }

    /// <summary>蓄電（第288期・<see cref="TraitId.StoredCharge"/>）の保持者が戦闘に出たか。<b>いなければ <c>MarkShock</c> は比較1つで抜ける。</b></summary>
    bool _chargeLive;

    /// <summary>第289期: 連鎖の後の口（<see cref="AfterChain"/>）を読む駒——蓄電・雷の保持者。空なら連鎖の後は比較1つで抜ける。</summary>
    readonly List<UnitState> _chainReaders = new();
    /// <summary>第289期: いま割り込みの鞭を振っている駒（怖気の判定から外す・<see cref="ShockWhip"/> の中だけ）。</summary>
    UnitState? _shockWhipActor;
    /// <summary>第293期（SW-a）: いま振っている割り込みの鞭の倍率（1 ＝ 掛けない・<see cref="ShockWhip"/> の中だけ）。</summary>
    int _shockWhipMult = 1;

    /// <summary>第290期: 組み付きの保持者（クグ）が戦闘に出たか（<b>計数</b>の口を短絡させる）。</summary>
    bool _grappleLive;
    /// <summary>第290期: 糸（<see cref="TraitId.Thread"/>・KG-a〜）の保持者が戦闘に出たか。<b>いなければ放電は比較1つで従来どおり。</b></summary>
    bool _threadLive;

    /// <summary>
    /// 糸の先（第290期）。<paramref name="k"/> が糸の保持者で、組み付いている（<see cref="GrappleTrait.TargetKey"/>）か、
    /// この一撃でほどけた（<see cref="ThreadTrait.MemoKey"/>）相手が<b>生きている敵</b>ならその駒、ほかは null。<b>乱数を引かない。</b>
    /// </summary>
    UnitState? ThreadTarget(UnitState k)
    {
        if (!k.HasTrait(TraitId.Thread)) return null;
        int id = k.RawCounter(GrappleTrait.TargetKey) - 1;
        if (id < 0) id = k.RawCounter(ThreadTrait.MemoKey) - 1;
        if (id < 0) return null;
        foreach (UnitState u in _units)
            if (u.InstanceId == id) return u.IsAlive && u.TeamId != k.TeamId ? u : null;
        return null;
    }

    /// <summary>糸が帯電させた敵の印（第290期・KG-b・<b>計数専用</b>の私有キー・どの規則も読まない）。その敵が起点で弾けたら 0 に戻す。</summary>
    public const string ThreadMarkKey = "threadMarked";

    /// <summary>組み付いている相手が生きているか（第290期・<b>計数専用</b>）。</summary>
    bool KuguHeld(UnitState k)
    {
        int id = k.RawCounter(GrappleTrait.TargetKey) - 1;
        if (id < 0) return false;
        foreach (UnitState u in _units) if (u.InstanceId == id) return u.IsAlive;
        return false;
    }

    /// <summary>直近に糸を伝わせたクグ（第290期・<b>計数の帰属先だけ</b>）。</summary>
    UnitState? _lastThreadKugu;

    // =================================================================================
    // 第292期 —— 糸玉（クグの KB-a ／ KB-b・design/PHASE292_KUGU_SILKBALL_SPEC.md §2-1）
    //
    // **糸玉は盤面の駒の列 `_units` に入れない**（別の列 `_silkBalls`）。だから標的・巻き込み・貫き・全体・庇い・前列の判定・勝敗・手番・
    // 生存数・撃破の数・会戦の持ち越しのどれにも現れない——「外す口」を1つも書かずに済む形にした（`AllUnits` ／ `LivingMembers` を読む箇所は全部素通り）。
    // 糸玉を読むのは感電の連鎖だけ: ① 弾けた駒の隣の放電の走査（`ShockTrigger`）② 糸玉に届いた放電（`Discharge` → `SilkBallDischarge`）
    // ③ ターンの頭の帯電し直し（`RechargeSilkBalls`）。連鎖の後の口（`AfterChain`）には弾けた糸玉が入る（雷雲・割り込みの燃料）が、
    // 割り込みの鞭と雷霆の的からは外す。**糸玉が1つも無い戦は `_silkBalls.Count == 0` の比較1つで従来どおり。** 乱数を引かない。
    // =================================================================================

    readonly List<UnitState> _silkBalls = new();
    readonly Dictionary<UnitState, UnitState> _silkOwner = new();

    /// <summary>いま盤上にある糸玉（第292期・置物・<see cref="AllUnits"/> には入らない）。</summary>
    public IReadOnlyList<UnitState> SilkBalls => _silkBalls;

    /// <summary>糸玉か（第292期）。</summary>
    public static bool IsSilkBall(UnitState u) => ReferenceEquals(u.Def, UnitCatalog.SilkBall);

    /// <summary>
    /// 糸玉を張る（第292期・<see cref="GrappleTrait"/> だけが呼ぶ）。<paramref name="near"/> の陣営の、<paramref name="near"/> の隣の空き席（生きている駒も糸玉もいない席）を
    /// 席番号の若い順に探し、無ければその陣営のほかの空き席（席番号の若い順）、それも無ければ張らない。<b>置いた瞬間から帯電している。乱数を引かない。</b>
    /// </summary>
    public UnitState? PlaceSilkBall(UnitState kugu, UnitState near)
    {
        UnitTally kt = TallyOf(kugu);
        int team = near.TeamId;
        FormationShape shape = ShapeOfTeam(team);
        bool Free(int s)
        {
            foreach (UnitState u in _units) if (u.TeamId == team && u.Slot == s && u.IsAlive) return false;
            foreach (UnitState b in _silkBalls) if (b.TeamId == team && b.Slot == s) return false;
            return true;
        }
        int slot = -1;
        for (int s = 0; s < FormationRules.TotalSlots && slot < 0; s++) if (shape.AreAdjacent(near.Slot, s) && Free(s)) slot = s;
        if (slot < 0)
        {
            for (int s = 0; s < FormationRules.TotalSlots && slot < 0; s++) if (Free(s)) slot = s;
            if (slot >= 0) kt.SilkFar++;
        }
        if (slot < 0) { kt.SilkNoRoom++; return null; }
        var ball = new UnitState
        {
            Def = UnitCatalog.SilkBall, TeamId = team, Shape = shape, Slot = slot,
            Hp = UnitCatalog.SilkBall.MaxHp, MaxHp = UnitCatalog.SilkBall.MaxHp,
            Traits = TraitCatalog.Resolve(UnitCatalog.SilkBall.Traits),
        };
        ball.InstanceId = _nextInstanceId++;   // 台本の番号だけ（`_units` には入れない）
        ball.Board = this;
        ball.SetCounter(StatusKeys.Shock, 1);
        _shockLive = true;
        _silkBalls.Add(ball);
        _silkOwner[ball] = kugu;
        kt.SilkPlaced++;
        Log($"    {kugu.Name} が {near.Name} のそばに帯電した糸玉を張った（{FormationRules.SeatNames[slot]}）", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.SilkBall, Turn = _turn, ActorId = kugu.InstanceId, TargetId = ball.InstanceId, PartnerId = near.InstanceId,
            Slot = slot, Team = team, Amount = _silkBalls.Count, Text = SilkBallLabels.Place,
        });
        return ball;
    }

    /// <summary>ターンの頭に糸玉を帯電し直す（第292期・<c>Run</c> の <c>TickStatuses</c> の直後）。糸玉が無ければ比較1つで抜ける。</summary>
    public void RechargeSilkBalls()
    {
        if (_webLive) RechargeWebs();   // 第293期（KW-a の糸の敵・網が無ければ比較1つ）
        if (_silkBalls.Count == 0) return;
        int n = 0;
        foreach (UnitState b in _silkBalls)
            if (b.RawCounter(StatusKeys.Shock) <= 0) { b.SetCounter(StatusKeys.Shock, 1); n++; }
        if (n == 0) return;
        Log($"    糸玉が帯電し直した（{n} 個）", LogKind.Status);
        if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.SilkBall, Turn = _turn, Amount = n, Text = SilkBallLabels.Recharge });
    }

    // =================================================================================
    // 第293期 —— クグの網（KW-a 帯電の網 ／ KW-b 絡まる網・design/PHASE293_SHOCK_WEB_SPEC.md §3）
    //
    // 組み付いている間、クグの手番ごとに糸を1本張る（`GrappleTrait` が呼ぶ）。糸は `StatusKeys.Web`（値 ＝ 張ったクグの番号 + 1）。
    // KW-a はターンの頭に帯電し直す（`RechargeSilkBalls` の中・`MarkShock` を通す）、KW-b は行動順の速さ −3（`TurnSpeed`）。
    // **網の保持者がいなければ `_webLive` の比較1つで従来どおり。** 乱数を引かない。
    // =================================================================================

    bool _webLive;

    /// <summary>糸の持ち主（張ったクグ・倒れていても引く）。無ければ null。</summary>
    UnitState? WebOwner(UnitState u)
    {
        int id = u.RawCounter(StatusKeys.Web) - 1;
        if (id < 0) return null;
        foreach (UnitState k in _units) if (k.InstanceId == id) return k;
        return null;
    }

    /// <summary>
    /// 糸を1本張る（第293期・<see cref="GrappleTrait"/> だけが呼ぶ）。張る先: ① 組み付いた敵（<paramref name="held"/>）の隣の敵で糸の掛かっていない駒（席番号の若い順）
    /// ② いなければほかの敵で糸の掛かっていない駒（組み付いた敵そのものは除く）③ すべて糸の中なら組み付いた敵の隣の空き席に糸玉（<paramref name="noBall"/> なら張らない）。
    /// KW-a の糸は張った瞬間にも帯電させる（糸玉と同じ）。<b>乱数を引かない。</b>
    /// </summary>
    public void SpinWeb(UnitState kugu, UnitState held, bool noBall)
    {
        _webLive = true;
        UnitTally kt = TallyOf(kugu);
        UnitState? pick = null;
        foreach (UnitState u in LivingMembers(held.TeamId).OrderBy(x => x.Slot))
            if (u != held && u.RawCounter(StatusKeys.Web) <= 0 && FormationRules.AreAdjacent(held, u)) { pick = u; break; }
        if (pick is null)
            foreach (UnitState u in LivingMembers(held.TeamId).OrderBy(x => x.Slot))
                if (u != held && u.RawCounter(StatusKeys.Web) <= 0) { pick = u; break; }
        if (pick is null)
        {
            if (noBall) { kt.WebNone++; return; }
            UnitState? ball = PlaceSilkBall(kugu, held);
            if (ball is null) { kt.WebNone++; return; }
            kt.WebBalls++;
            if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.Web, Turn = _turn, ActorId = kugu.InstanceId, TargetId = ball.InstanceId, PartnerId = held.InstanceId, Slot = ball.Slot, Team = ball.TeamId, Text = WebLabels.Ball });
            return;
        }
        pick.SetCounter(StatusKeys.Web, kugu.InstanceId + 1);
        kt.WebSpun++;
        if (FormationRules.AreAdjacent(held, pick)) kt.WebAdjacent++;
        Log($"    {kugu.Name} が {pick.Name} に糸を張った", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.Web, Turn = _turn, ActorId = kugu.InstanceId, TargetId = pick.InstanceId, PartnerId = held.InstanceId, Slot = pick.Slot, Team = pick.TeamId, Text = WebLabels.Spin });
        if (kugu.HasTrait(TraitId.WebCharge) && MarkShock(pick, kugu)) kt.WebCharged++;
    }

    /// <summary>ターンの頭に KW-a の糸の敵を帯電し直す（第293期・<see cref="RechargeSilkBalls"/> の中）。</summary>
    void RechargeWebs()
    {
        foreach (UnitState u in _units.ToList())
        {
            if (!u.IsAlive || u.RawCounter(StatusKeys.Web) <= 0) continue;
            UnitState? k = WebOwner(u);
            if (k is null || !k.HasTrait(TraitId.WebCharge)) continue;
            if (!MarkShock(u, k)) continue;
            TallyOf(k).WebRecharged++;
            if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.Web, Turn = _turn, ActorId = k.InstanceId, TargetId = u.InstanceId, Slot = u.Slot, Team = u.TeamId, Text = WebLabels.Recharge });
        }
    }

    /// <summary>
    /// 行動順の速さ（第293期）。KW-b の糸の掛かった駒は −3（重ならない）。<b>網が張られていない戦は <c>Def.Speed</c> のまま</b>（`Run` の並べ替えだけが読む）。
    /// </summary>
    public int TurnSpeed(UnitState u)
    {
        if (!_webLive || u.RawCounter(StatusKeys.Web) <= 0) return u.Def.Speed;
        UnitState? k = WebOwner(u);
        return k is not null && k.HasTrait(TraitId.WebSnare) ? u.Def.Speed - WebSnareSlow : u.Def.Speed;
    }

    /// <summary>KW-b の速さの減り。</summary>
    public const int WebSnareSlow = 3;

    /// <summary>
    /// 糸玉に届いた放電（第292期・<see cref="Discharge"/> だけが呼ぶ）。<b>HP は減らない</b>（<c>ApplyDamage</c> を通さない・<c>Damage</c> も出さない）。
    /// 糸玉が帯電していれば、この連鎖の列に積む（通常の起爆と同じ深さ・1つの連鎖で1回）。
    /// </summary>
    void SilkBallDischarge(UnitState from, UnitState ball, int depth, UnitState? ini)
    {
        UnitTally ot = TallyOf(_silkOwner[ball]);
        ot.SilkDisIn++;
        if (IsSilkBall(from)) ot.SilkDisOut++;
        TallyOf(from).DischargeHits++;
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Discharge, Turn = _turn, ActorId = from.InstanceId, TargetId = ball.InstanceId,
            Amount = ShockRule.Discharge, Slot = depth + 1, Team = ball.TeamId, SourceTrait = TraitId.SilkBallEvery,
        });
        Log($"    {from.Name} から糸玉へ放電", LogKind.Status);
        if (ball.RawCounter(StatusKeys.Shock) > 0) _shockQueue.Enqueue((ball, depth + 1, ini));
    }

    /// <summary>手番の頭のクグの帳簿（第290期・<b>計数のみ</b>・盤面は読むだけ）: 生きている手番・帯電していた・組み付いていた・両方・組んだ相手に組み付きが立っていた。</summary>
    public void NoteKuguCensus()
    {
        if (!_grappleLive) return;
        foreach (UnitState k in _units)
        {
            if (!k.IsAlive || !k.HasTrait(TraitId.Grapple)) continue;
            UnitTally t = TallyOf(k);
            t.KuguTurns++;
            bool sh = k.RawCounter(StatusKeys.Shock) > 0, held = KuguHeld(k);
            if (sh) t.KuguShockTurns++;
            if (held) { t.KuguHeldTurns++; if (GrappleTrait.Held(this, k) is { } h && h.RawCounter(StatusKeys.Grappled) > 0) t.KuguHeldGrappled++; }
            if (sh && held) t.KuguBothTurns++;
        }
    }

    WhipSwing? _whip;

    /// <summary>いま振っている鞭の枠（無ければ null）。札が <c>OnAfterAttack</c> で読む。</summary>
    public WhipSwing? Whip => _whip;

    /// <summary>当てる前の2倍（当たる駒ごと・加算で「量＋量」）。当たった駒を枠に控える。</summary>
    int WhipAmount(WhipSwing w, UnitState t, int amount)
    {
        w.Hits.Add(t);
        UnitTally wt = TallyOf(w.Actor);
        wt.WhipHits++;
        if (_chargeLive)
        {
            wt.WhipBase += amount;   // 第288期・計数のみ
            if (w.Bolt && (w.BoltAny || t.RawCounter(StatusKeys.Shock) > 0)) w.BoltTargets.Add(t);   // 当たる前の感電（この一撃で弾ける）
        }
        bool bound = TormentTrait.IsBound(this, t);
        bool shocked = !bound && w.CountShock && t.RawCounter(StatusKeys.Shock) > 0;
        int result = amount;
        if (bound || shocked)
        {
            wt.WhipDoubled++;
            if (shocked) wt.WhipDoubledShock++;
            wt.WhipBonus += amount;
            Log($"    {w.Actor.Name} の鞭が動けない {t.Name} に二重に入る（{amount} → {amount + amount}）", LogKind.Highlight, w.Actor);
            result = amount + amount;
        }
        // 第293期（SW-a）: 割り込みの鞭の倍率（2倍の後）。割り込みの外・保持者のいない戦では 1 のまま。
        if (_shockWhipMult > 1 && _shockWhipActor == w.Actor)
        {
            int m = result * _shockWhipMult;
            wt.SwMultBonus += m - result;
            Log($"    弾けた電気をまとった鞭が {t.Name} を打つ（{result} → {m}）", LogKind.Damage);
            result = m;
        }
        return result;
    }

    /// <summary>
    /// 電気鞭（第217期・<see cref="LiveWireTrait"/> が呼ぶ）。当たって生きている敵すべてに感電を付け（<see cref="MarkShock"/>）、
    /// 自分の感電を<b>弾けさせずに消す</b>（放電しない・痺れない）。台本は <c>BattleEventKind.LiveWire</c>（表示専用）の後に
    /// 敵ごとの <c>StatusGain</c>（<c>shock</c>・書き手はシガ）が並ぶ。<b>乱数を引かない。</b>
    /// </summary>
    public void LiveWire(UnitState self, WhipSwing w)
    {
        var targets = w.Hits.Distinct().Where(h => h.IsAlive && h.TeamId != self.TeamId).ToList();
        UnitTally t = TallyOf(self);
        t.WiredSwings++;
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.LiveWire, Turn = _turn, ActorId = self.InstanceId, TargetId = self.InstanceId,
            Amount = targets.Count, Team = self.TeamId, HpAfter = Math.Max(0, self.Hp),
        });
        Log($"    {self.Name} の鞭が身の雷を移す（{targets.Count} 体）", LogKind.Trigger);   // 見せ場の出来事は LiveWire（Highlight にすると間に1件挟まる）
        foreach (UnitState u in targets)
            if (MarkShock(u, self)) t.WiredMarked++;
        // 第290期（SI-c）: 割り込みの鞭では自分の感電を残す（手番の電気鞭はいまどおり使い切る）。
        if (_shockWhipActor == self && self.HasTrait(TraitId.ShockWhipKeep)) { t.SwKeptShock++; return; }
        self.SetCounter(StatusKeys.Shock, 0);   // 弾けさせずに消す（ShockTrigger を通さない）
    }

    public void NoteWhipCowered(UnitState u) => TallyOf(u).WhipCowered++;
    public void NoteWhipSpared(UnitState u) => TallyOf(u).WhipWiredSpared++;
    public void NoteWhipScream(UnitState u, bool fromSplash) { if (fromSplash) TallyOf(u).WhipScreamSplash++; }

    /// <summary>
    /// 次の <c>ApplyDamage</c> 1回にだけ効く札（逸らしの <c>_deflectFrom</c> と同じ作法・<c>ApplyDamageBody</c> の最初の行で読んで消す）。
    /// 0 通常 ／ 1 雷（起爆しない）／ 2 刻み（K2 のときだけ起爆する）／ 3 放電 ／ 4 雷霆（第288期・起爆しない）。
    /// </summary>
    byte _shockNext;

    /// <summary>次の <c>ApplyDamage</c> 1回にだけ効く撃破者の差し替え（放電で倒れた駒の撃破者 ＝ 連鎖を起こした一撃の主）。</summary>
    bool _shockKillerSet;
    UnitState? _shockKiller;

    bool _shockChaining;
    int _shockDepth;
    readonly Queue<(UnitState U, int Depth, UnitState? Ini)> _shockQueue = new();

    /// <summary>刻みの <c>ApplyDamage</c> の直前に呼ぶ（感電が書かれた戦闘でだけ札を立てる）。</summary>
    void MarkTickHit() { if (_shockLive) _shockNext = 2; }

    /// <summary>
    /// 感電を付ける唯一の窓口（第214期）。<b>二値</b>——既に感電していれば何もしない。倒れた駒には付かない。
    /// <paramref name="writer"/> は計数と台本の書き手だけに使う。<b>乱数を引かない。</b>
    /// </summary>
    /// <returns>新しく付いたか。</returns>
    public bool MarkShock(UnitState target, UnitState writer, PowderRoute? powder = null, UnitState? spreadFrom = null)
    {
        if (!target.IsAlive) return false;
        _shockLive = true;
        if (target.RawCounter(StatusKeys.Shock) > 0)
        {
            // 第293期（SW-b）: 感電を浴びるたび蓄電 +1（すでに帯電していても）。保持者がいなければ比較1つで抜ける。
            if (_chargeLive && target.HasTrait(TraitId.StoredChargeEvery)) { TallyOf(target).ChargeOnShocked++; GainCharge(target, writer); }
            return false;
        }
        target.SetCounter(StatusKeys.Shock, 1);
        // 第294期（計数のみ）: 膜の保持者がいる戦だけ、いまの帯電の書き手の種類を控える（0 トウ ／ 1 カタ ／ 2 ソム ／ 3 ほかの味方 ／ 4 敵）。
        if (_membraneHolders.Count > 0)
            _shockWriterCat[target.InstanceId] = writer.TeamId != target.TeamId ? 4 : writer.Def.Id == "tou" ? 0 : writer.Def.Id == "kata" ? 1 : writer.Def.Id == "som" ? 2 : 3;
        UnitTally wt = TallyOf(writer);
        if (writer.TeamId == target.TeamId) wt.ShockOnAlly++; else wt.ShockOnFoe++;
        TallyOf(target).ShockReceived++;   // 第217期（計数のみ）
        EmitStatusGain(target, StatusKeys.Shock, 1, writer, spreadFrom: spreadFrom, powder: powder);   // 表示専用（第291期: トウの粉は経路の印つき）
        if (_chargeLive && target.HasTrait(TraitId.StoredCharge)) GainCharge(target, writer);   // 第288期（蓄電の口・ここ1箇所）
        if (_grappleLive && target.HasTrait(TraitId.Grapple))   // 第290期・計数のみ（クグが帯電した書き手）
            (TallyOf(target).KuguShockBySrc ??= new long[5])[writer.TeamId != target.TeamId ? 4 : writer.Def.Id == "tou" ? 0 : writer.Def.Id == "kata" ? 1 : writer.Def.Id == "som" ? 2 : 3]++;
        return true;
    }

    /// <summary>
    /// 蓄電が1つ増える（第288期・<see cref="MarkShock"/> だけが呼ぶ）。上限 <see cref="StoredChargeTrait.Cap"/>。<b>乱数を引かない。</b>
    /// </summary>
    void GainCharge(UnitState u, UnitState writer)
    {
        UnitTally t = TallyOf(u);
        int c = StoredChargeTrait.Of(u);
        if (c >= StoredChargeTrait.Cap) { t.ChargeCapped++; return; }
        u.SetCounter(StoredChargeTrait.Key, c + 1);
        EmitShockGauge(ShockGaugeLabels.ChargeGain, writer, u, c + 1, c);   // 第291期・表示専用
        t.ChargeGains++;
        int src = writer == u || writer.TeamId != u.TeamId ? 4
                : writer.Def.Id == "tou" ? 0 : writer.Def.Id == "kata" ? 1 : writer.Def.Id == "som" ? 2 : 3;
        (t.ChargeBySrc ??= new long[5])[src]++;
        if (c + 1 == StoredChargeTrait.Cap)
        {
            t.ChargeFulls++;
            if (t.ChargeFullTurn == 0) t.ChargeFullTurn = _turn;
            Log($"    {u.Name} の身に雷が溜まりきった（蓄電 {c + 1}）", LogKind.Trigger);
        }
        else Log($"    {u.Name} に電気が溜まる（蓄電 {c + 1}）", LogKind.Status);
    }

    /// <summary>
    /// 雷霆（第288期・<see cref="ThunderclapTrait"/> が呼ぶ）。枠に控えた「当たったとき感電していた敵」のうち生きている駒それぞれに、
    /// 攻撃力 × <see cref="ThunderclapTrait.Multiplier"/>（孤立への雷霆の保持者なら、隣の味方が1体もいない敵には ×<see cref="ThunderclapLoneTrait.Factor"/>）を
    /// <c>ApplyDamage</c> の直呼びで足す（<b>起爆しない</b>・札 4）。撃ち終えたら蓄電 0。<b>乱数を引かない。</b>
    /// </summary>
    public void Thunderclap(UnitState self, WhipSwing w) => Thunderclap(self, w.BoltTargets);

    /// <summary>雷霆の本体（第289期に的の列を引数にした——SI-a の割り込みは「その連鎖で弾けた敵」を渡す）。</summary>
    void Thunderclap(UnitState self, IEnumerable<UnitState> boltTargets)
    {
        UnitTally t = TallyOf(self);
        t.BoltCasts++;
        int atk = self.CurrentAttack;   // 蓄電 4 の攻撃力（0 に戻す前）
        bool loneRule = self.HasTrait(TraitId.ThunderclapLone);
        var targets = boltTargets.Distinct().Where(h => h.IsAlive && h.TeamId != self.TeamId && !IsSilkBall(h)).ToList();   // 第292期: 糸玉は的にしない
        if (targets.Count == 0) t.BoltDry++;
        else Log($"    {self.Name} の溜めた雷が鞭から迸る——雷霆（{targets.Count} 体）", LogKind.Highlight, self);
        foreach (UnitState u in targets)
        {
            if (!u.IsAlive) continue;
            int amt = atk * ThunderclapTrait.Multiplier;
            if (loneRule && !LivingMembers(u.TeamId).Any(n => n != u && FormationRules.AreAdjacent(u, n)))
            {
                t.BoltLoneHits++;
                t.BoltLoneNominal += amt * (ThunderclapLoneTrait.Factor - 1);
                amt *= ThunderclapLoneTrait.Factor;
                Log($"    孤立した {u.Name} を雷霆が深く焼く（{amt}）", LogKind.Damage);
            }
            else Log($"    雷霆が {u.Name} を焼く（{amt}）", LogKind.Damage);
            t.BoltHits++;
            t.BoltNominal += amt;
            int before = u.Hp;
            bool shocked = u.RawCounter(StatusKeys.Shock) > 0;   // 計数のみ（自己検査: 雷霆は起爆しない）
            _shockNext = 4;
            ApplyDamage(u, amt, self);
            _shockNext = 0;
            if (shocked) { t.BoltOnShocked++; if (u.RawCounter(StatusKeys.Shock) > 0) t.BoltMuted++; }
            t.BoltDealt += before - Math.Max(0, u.Hp);
            if (before > 0 && !u.IsAlive) t.BoltKills++;
        }
        int drained = StoredChargeTrait.Of(self);
        self.SetCounter(StoredChargeTrait.Key, 0);
        if (drained > 0) EmitShockGauge(ShockGaugeLabels.ChargeDrained, self, self, 0, drained);   // 第291期・表示専用
    }

    /// <summary>帯電の粉が新しく感電を付けた（第286期・<see cref="ChargedPowderTrait"/> だけが呼ぶ・<b>計数のみ</b>）。</summary>
    public void NotePowder(UnitState tou, bool spread)
    {
        UnitTally t = TallyOf(tou);
        if (spread) t.PowderSpread++; else t.PowderMain++;
    }

    /// <summary>
    /// 雷の1発（第214期・<see cref="ThunderTrait"/> だけが呼ぶ）。<b>術</b>——<c>PerformAttack</c> を通らず
    /// <c>ApplyDamage(敵, 量, カタ)</c> を直に呼ぶ（リリの吸い取りと同じ入口。§1 の +50%・破片・軛は効き、反撃は起きない）。
    /// <b>この1発は感電を起爆しない。</b> 当てた後に生きていれば感電を付ける。
    /// </summary>
    /// <param name="hop">何発目か（1 始まり・表示と計数）。</param>
    /// <param name="kinds">命中の前に数えた状態異常の種類（表示と計数）。</param>
    public void StrikeThunder(UnitState kata, UnitState target, int amount, int hop, int kinds)
    {
        if (_mireDull != 0) amount = MireCut(kata, amount, 1);   // 第218期（澱みのデバフ・雷）
        UnitTally kt = TallyOf(kata);
        kt.ThunderHits++;
        (kt.ThunderKindsHist ??= new long[ThunderTrait.CountedKeys.Count + 1])[Math.Min(kinds, ThunderTrait.CountedKeys.Count)]++;
        if (amount > kt.ThunderMax) kt.ThunderMax = amount;
        kt.ThunderNominal += amount;   // 第289期・計数のみ
        if (_turn == 1) { kt.ThunderHitsT1++; kt.ThunderKindsT1 += kinds; }   // 第216期・**計数のみ**
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Thunder, Turn = _turn, ActorId = kata.InstanceId, TargetId = target.InstanceId,
            Amount = amount, Slot = hop, StatusRemaining = kinds, Team = target.TeamId,
            // 表示専用: 数えた状態の表示名（命中の前）。
            Text = string.Join(",", ThunderTrait.CountedKeys.Where(k => target.RawCounter(k) > 0).Select(StatusKeys.LabelOf)),
        });
        Log($"    雷が {target.Name} に落ちた（{hop} 発目・状態 {kinds} 種 → {amount}）", LogKind.Trigger);
        int before = target.Hp;
        _shockNext = 1;
        _thunderDepth++;   // 第220期・計数のみ
        ApplyDamage(target, amount, kata);
        _thunderDepth--;
        _shockNext = 0;
        kt.ThunderDealt += before - Math.Max(0, target.Hp);
        if (before > 0 && !target.IsAlive) kt.ThunderKills++;
        MarkShock(target, kata);
    }

    /// <summary>雷を落とし終えた（<b>計数のみ</b>）。</summary>
    public void NoteThunderCast(UnitState kata, int hits, bool fallback)
    {
        UnitTally kt = TallyOf(kata);
        kt.ThunderCasts++;
        if (_turn == 1) kt.ThunderCastsT1++;   // 第216期・**計数のみ**
        if (fallback) kt.ThunderFallback++;
        (kt.ThunderPerCastHist ??= new long[10])[Math.Min(hits, 9)]++;
        // 第289期・**計数のみ**: 前の雷からいまの雷までに弾けた敵の数（KR-a の雷雲と同じ量）と、いまの雷雲
        (kt.ThunderPopsHist ??= new long[16])[(int)Math.Min(kt.ThunderPopsPending, 15)]++;
        kt.ThunderPopsPending = 0;
        int cloud = ThundercloudTrait.Of(kata);
        kt.CloudAtCastSum += cloud;
        if (cloud > kt.CloudAtCastMax) kt.CloudAtCastMax = cloud;
        if (cloud > 0) (kt.CloudByCast ??= new long[8])[(int)Math.Min(kt.ThunderCasts, 8) - 1] += cloud;   // 第290期・計数のみ（雷雲の推移・何回目の雷か）
    }

    /// <summary>
    /// 起爆（第214期）。<c>ApplyDamageBody</c> が「感電している駒の HP に届いた一撃」で呼ぶ。
    /// <b>連鎖の中で呼ばれたら起爆せずに列へ積む</b>——外側の1回だけが列を幅優先で回す
    /// （入れ子の呼び出しのまま起爆すると深さ優先になる）。<b>乱数を引かない。</b>
    /// </summary>
    /// <param name="initiator">連鎖を起こした一撃の主（撃破の帰属先）。刻みが起こした連鎖は null。</param>
    /// <param name="rootKind">根の種類（計数のみ）: 0 攻撃などの一撃 ／ 1 刻み ／ 2 出どころの無い削り。</param>
    internal void ShockTrigger(UnitState u, UnitState? initiator, int rootKind)
    {
        if (_shockChaining) { _shockQueue.Enqueue((u, _shockDepth + 1, initiator)); return; }

        _shockChaining = true;
        _shockQueue.Clear();
        _shockQueue.Enqueue((u, 0, initiator));
        int size = 0, deepest = 0, cross = 0;
        bool threadRoot = false;
        List<UnitState>? popped = _chainReaders.Count > 0 ? new List<UnitState>() : null;   // 第289期（連鎖の後の口）
        try
        {
            while (_shockQueue.Count > 0)
            {
                var (x, d, ini) = _shockQueue.Dequeue();
                if (x.RawCounter(StatusKeys.Shock) <= 0) continue;   // 既に放電した（1つの駒は1回しか放電しない）
                x.SetCounter(StatusKeys.Shock, 0);
                size++;
                popped?.Add(x);
                if (d > deepest) deepest = d;
                _shockDepth = d;
                TallyOf(x).ShockSpent++;
                if (_verbose) Emit(new BattleEvent
                {
                    Kind = BattleEventKind.ShockSpent, Turn = _turn, ActorId = ini?.InstanceId, TargetId = x.InstanceId,
                    Slot = d, HpAfter = Math.Max(0, x.Hp), Team = x.TeamId,
                });
                Log($"    {x.Name} の感電が弾けた（{(d == 0 ? "起点" : d + " 段目")}）", LogKind.Trigger);
                bool ball = _silkBalls.Count > 0 && IsSilkBall(x);   // 第292期（糸玉が無ければ比較1つ）
                if (_webLive && x.RawCounter(StatusKeys.Web) > 0 && WebOwner(x) is { } wo) TallyOf(wo).WebPops++;   // 第293期・計数のみ
                if (ball)
                {
                    UnitTally ot = TallyOf(_silkOwner[x]);
                    ot.SilkPops++;
                    if (u.TeamId == x.TeamId) ot.SilkPopsHeroRoot++;
                    if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.SilkBall, Turn = _turn, ActorId = ini?.InstanceId, TargetId = x.InstanceId, Slot = x.Slot, Team = x.TeamId, Text = SilkBallLabels.Pop });
                }
                if (_shockStun != 0 && !ball) StunByShock(x, d, ini);   // 第216期（S1〜S3・保持者がいなければ比較1つで抜ける）。第292期: 糸玉は痺れない（乱数も引かない）
                if (x.TeamId != u.TeamId) cross++;                // 第290期・計数のみ（糸を伝って敵の陣で弾けた）
                if (_grappleLive && d > 0 && x.HasTrait(TraitId.Grapple)) { UnitTally kt = TallyOf(x); kt.KuguPopChain++; if (KuguHeld(x)) kt.KuguPopChainHeld++; }   // 第290期・計数のみ
                if (_threadLive && d == 0 && x.RawCounter(ThreadMarkKey) > 0) { x.SetCounter(ThreadMarkKey, 0); threadRoot = true; }   // 第290期・計数のみ（KG-b の印の駒が起点）
                // 第290期（糸 ①）: 糸の保持者の放電は、隣の味方ではなく糸の先の敵へ1本流れる。保持者がいなければ比較1つで従来どおり。
                UnitState? th = _threadLive ? ThreadTarget(x) : null;
                if (th is not null) Discharge(x, th, d, ini);
                else
                {
                    foreach (UnitState n in LivingMembers(x.TeamId))
                        if (n != x && FormationRules.AreAdjacent(x, n)) Discharge(x, n, d, ini);
                    // 第292期: 隣の糸玉へも放電する（駒の後・張った順）。糸玉が無ければ比較1つで抜ける。
                    if (_silkBalls.Count > 0)
                        foreach (UnitState b in _silkBalls.ToList())
                            if (b != x && b.TeamId == x.TeamId && FormationRules.AreAdjacent(x, b)) Discharge(x, b, d, ini);
                }
            }
        }
        finally { _shockChaining = false; _shockDepth = 0; }

        UnitTally rt = TallyOf(u);
        rt.ChainRoots++;
        rt.ChainUnits += size;
        (rt.ChainSizeHist ??= new long[12])[Math.Min(size, 11)]++;
        if (deepest > rt.ChainDepthMax) rt.ChainDepthMax = deepest;
        if (rootKind == 1) rt.ShockTriggeredTick++;
        else if (initiator is not null) { UnitTally it = TallyOf(initiator); it.ShockTriggered++; it.ShockTriggeredUnits += size; }   // 第217期: 大きさも
        else rt.ShockTriggeredOther++;

        if (cross > 0 && _lastThreadKugu is not null) TallyOf(_lastThreadKugu).ThreadCrossPops += cross;   // 第290期・計数のみ
        if (threadRoot && _lastThreadKugu is not null) { UnitTally kt = TallyOf(_lastThreadKugu); kt.ThreadMarkRoots++; kt.ThreadMarkChainUnits += size; }   // 第290期・計数のみ

        if (popped is not null)
        {
            // 第290期: 糸を伝うと1つの連鎖に両陣営の駒が入る——陣営ごとに分けて渡す（糸が無ければ1陣営だけで、従来と同じ1回）。
            UnitState? ini2 = rootKind == 1 ? null : initiator;
            if (cross == 0) AfterChain(u.TeamId, popped, ini2);
            else
                foreach (int team in popped.Select(q => q.TeamId).Distinct().ToList())
                    AfterChain(team, popped.Where(q => q.TeamId == team).ToList(), ini2);
        }
    }

    /// <summary>
    /// 連鎖の後の口（第289期）。<see cref="ShockTrigger"/> の外側の1回が終わった直後に1度だけ呼ぶ。<paramref name="popped"/> はその連鎖で弾けた駒（すべて <paramref name="team"/> の側）。
    /// 雷雲（カタ・KR）と、蓄電の保持者の計数と、割り込み（シガ・SI）。<b>乱数を引かない。</b>
    /// </summary>
    void AfterChain(int team, List<UnitState> popped, UnitState? ini)
    {
        foreach (UnitState h in _chainReaders.ToList())
        {
            if (h.TeamId == team) continue;
            UnitTally ht = TallyOf(h);
            if (h.HasTrait(TraitId.Thunder))
            {
                ht.ThunderPopsPending += popped.Count;   // 計数のみ（雷を落とすまでに弾けた敵の数）
                if (h.HasTrait(TraitId.Thundercloud))
                {
                    int c0 = ThundercloudTrait.Of(h);
                    int c = c0 + popped.Count;
                    if (h.HasTrait(TraitId.ThundercloudKeep) && !h.HasTrait(TraitId.ThundercloudUncapped)) c = Math.Min(ThundercloudTrait.KeepCap, c);   // 第292期: KR-∞ は上限なし
                    h.SetCounter(ThundercloudTrait.Key, c);
                    if (c != c0) EmitShockGauge(ShockGaugeLabels.Cloud, popped.Count > 0 ? popped[0] : null, h, c, c0, remaining: popped.Count);   // 第291期・表示専用
                }
                continue;
            }
            if (ini == h) { ht.ChainOwn++; continue; }   // シガ自身の一撃で起きた連鎖では割り込まない
            // 計数のみ（Phase 0）: 連鎖の起点の書き手・その時点の蓄電・連鎖の後に生きている敵
            int cat = ini is null ? 4 : ini.TeamId == h.TeamId ? (ini.Def.Id == "tou" ? 0 : ini.Def.Id == "kata" ? 1 : 3) : 5;
            (ht.ChainIniHist ??= new long[6])[cat]++;
            (ht.ChainChargeHist ??= new long[StoredChargeTrait.Cap + 1])[Math.Min(StoredChargeTrait.Cap, StoredChargeTrait.Of(h))]++;
            int alive = 0;
            foreach (UnitState x in popped) if (x.IsAlive && !IsSilkBall(x)) alive++;
            (ht.ChainAliveHist ??= new long[10])[Math.Min(alive, 9)]++;
            if (h.HasTrait(TraitId.ShockWhipBolt) || h.HasTrait(TraitId.ShockWhipFlurry)) ShockWhip(h, popped);
        }
    }

    /// <summary>
    /// シガの割り込み（第289期・SI-a ／ SI-b）。連鎖で弾けた生きている敵から主目標を選び（動けない敵を優先・弾けた順）、
    /// <see cref="Interrupt"/> の中で的を固定した鞭を1振り。SI-a はその後、弾けた生きている敵それぞれに雷霆（蓄電 0）、
    /// SI-b は蓄電を1減らす（振り終えた後）。<b>乱数を引かない。</b>
    /// </summary>
    void ShockWhip(UnitState h, List<UnitState> popped)
    {
        UnitTally t = TallyOf(h);
        t.SwAsked++;
        bool bolt = h.HasTrait(TraitId.ShockWhipBolt);
        int c = StoredChargeTrait.Of(h);
        if (bolt ? c < StoredChargeTrait.Cap : c < 1) { t.SwNoCharge++; return; }
        UnitState? target = null;
        // 第292期: 糸玉は的にしない（狙われない置物）。
        foreach (UnitState x in popped) if (x.IsAlive && !IsSilkBall(x) && TormentTrait.IsBound(this, x)) { target = x; break; }
        if (target is null) foreach (UnitState x in popped) if (x.IsAlive && !IsSilkBall(x)) { target = x; break; }
        if (target is null) { t.SwNoTarget++; return; }
        if (InInterrupt) { t.SwNested++; return; }
        bool hush = Hush.Active && HushHolderAlive;
        if (!CanActOutOfTurn(h, OutOfTurnRoute.ShockWhip)) { if (hush) t.SwHushed++; else t.SwBlocked++; return; }
        Interrupt(() =>
        {
            t.SwFires++;
            EmitShockGauge(ShockGaugeLabels.Interrupt, h, target, c, popped.Count, partner: popped.Count > 0 ? popped[0] : null);   // 第291期・表示専用（見出し）
            Log($"    そばで弾けた電気に、{h.Name} の鞭が割り込む", LogKind.Highlight, h);
            long before = t.DamageToEnemy;
            UnitState? prevActor = _shockWhipActor;
            int prevMult = _shockWhipMult;
            _shockWhipActor = h;
            // 第293期（SW-a ／ SW-b）: 割り込みの鞭は合図の連鎖で弾けた数（糸玉を含む）だけ重くなる（× (1 ＋ 数)・2倍の後）。
            _shockWhipMult = h.HasTrait(TraitId.ShockWhipChain) ? 1 + popped.Count : 1;
            if (_shockWhipMult > 1)
            {
                t.SwMultSum += _shockWhipMult; t.SwMultN++;
                (t.SwChainHist ??= new long[12])[Math.Min(popped.Count, 11)]++;
                EmitShockGauge(ShockGaugeLabels.WhipChain, h, target, _shockWhipMult, popped.Count);   // 表示専用（倍率）
            }
            _forcedTarget = target;
            try { PerformAttack(h); }
            finally { _forcedTarget = null; _shockWhipActor = prevActor; _shockWhipMult = prevMult; }
            t.SwWhipDealt += t.DamageToEnemy - before;
            if (bolt)
            {
                long b2 = t.DamageToEnemy;
                Thunderclap(h, popped);
                t.SwBoltDealt += t.DamageToEnemy - b2;
            }
            else
            {
                int c1 = StoredChargeTrait.Of(h);
                h.SetCounter(StoredChargeTrait.Key, Math.Max(0, c1 - 1));
                if (c1 > 0) EmitShockGauge(ShockGaugeLabels.ChargeSpent, h, h, c1 - 1, c1);   // 第291期・表示専用
            }
        });
    }

    /// <summary>
    /// 感電で痺れる（第216期・S1〜S3）。<b>弾けた直後、放電より前</b>に痺れを付ける（台本は <c>ShockSpent</c> の直後に <c>StatusGain</c>（<c>stun</c>））。
    /// 倒れた駒には付けない（S3 の乱数も引かない）。S1 は起点（深さ 0）だけ。既に痺れていれば何も増えない（二値）。
    /// <b>乱数を引くのは S3 だけ</b>（弾けた生きている駒1体につき1回）。
    /// </summary>
    void StunByShock(UnitState x, int depth, UnitState? ini)
    {
        UnitTally t = TallyOf(x);
        if (!x.IsAlive) { t.ShockStunDead++; return; }
        if (_shockStun == 1 && depth != 0) return;
        // 第294期（SM-b・痺れない膜）: ソムが生きている間、その陣営の駒は弾けても痺れない（乱数も引かない）。保持者がいなければ件数の比較1つで抜ける。
        if (_membraneHolders.Count > 0 && MembraneOf(x.TeamId, noStun: true) is not null) { t.MembraneStunSkipped++; return; }
        // 第217期（G3H）: 痺れが明けた駒は、次の自分の手番まで感電で痺れない。保持者がいなければ比較1つで抜ける。
        if (_shockStunGuard && x.RawCounter(ShockRule.GuardKey) > 0) { t.ShockStunGuarded++; return; }
        if (_shockStun == 3 && Roll(100) >= ShockRule.StunHalfPercent) { t.ShockStunMissed++; return; }
        if (x.RawCounter(StatusKeys.Stun) > 0) { t.ShockStunAlready++; return; }
        t.ShockStunned++;
        // 第218期・**計数のみ**: そのターンの手番をまだ終えていなかったか（＝動く前に止めた）。ミオの一撃が起こした連鎖はミオの帳簿にも。
        bool early = x.TakenTurn < _turn;
        if (early) t.ShockStunnedEarly++;
        if (ini is not null && ini.HasTrait(TraitId.MireSlam))
        {
            UnitTally mt = TallyOf(ini);
            if (x.TeamId == ini.TeamId) mt.MireStunnedAlly++;
            else { mt.MireStunned++; if (early) mt.MireStunnedEarly++; }
        }
        EmitStatusGain(x, StatusKeys.Stun, 1, ini);   // 表示専用（ShockSpent の直後）
        x.SetCounter(StatusKeys.Stun, 1);
        x.SetCounter(ShockRule.StunKey, 1);            // 計数専用（失った手番の帰属）
        Log($"    {x.Name} は感電で痺れた", LogKind.Status);
    }

    /// <summary>
    /// 放電1本（第214期）。<b>状態異常のダメージ</b>——行動でも攻撃でもない（粛では止まらない・棘と板の反射は鳴らない）。
    /// 出どころは<b>放電した駒</b>（同じ陣営・<c>isFriendlyFire</c>）、撃破者は<b>連鎖を起こした一撃の主</b>。
    /// 隣がベニの結界の内側なら、毒・燃焼の刻みと同じ口（<c>InvertsTick</c> → <c>InverseHeal</c>）で回復に反転する。
    /// </summary>
    void Discharge(UnitState from, UnitState to, int depth, UnitState? ini)
    {
        // 第292期: 糸玉に届いた放電は HP を減らさず、帯電していれば連鎖に積むだけ。糸玉が無ければ比較1つで抜ける。
        bool fromBall = false;
        if (_silkBalls.Count > 0)
        {
            if (IsSilkBall(to)) { SilkBallDischarge(from, to, depth, ini); return; }
            if (IsSilkBall(from)) { fromBall = true; UnitTally ot = TallyOf(_silkOwner[from]); ot.SilkDisOut++; ot.SilkDisToUnit++; }
        }
        int amt = ShockRule.Discharge;
        // 第290期・計数のみ: 隣の味方の放電がクグに来た（糸の ② の発火見込み）。
        if (_grappleLive && to.TeamId == from.TeamId && to.HasTrait(TraitId.Grapple)) { UnitTally kt = TallyOf(to); kt.KuguDisIn++; if (KuguHeld(to)) kt.KuguDisInHeld++; }
        // 第290期（糸）: ① 糸の保持者の放電は糸の先の敵へ（呼び出し側が to に敵を渡す）／ ② 隣の味方の放電が糸の保持者に来たら、受けずに糸の先の敵へ移す。
        // 出どころはクグ（味方の刃ではない）・量は放電のまま。**保持者がいなければ比較1つで抜ける。**
        if (_threadLive)
        {
            UnitState? via = null;
            if (to.TeamId != from.TeamId) via = from;
            else if (ThreadTarget(to) is { } th) { via = to; to = th; }
            if (via is not null) { ThreadDischarge(from, via, to, amt, depth, ini); return; }
        }
        UnitTally ft = TallyOf(from), tt = TallyOf(to);
        ft.DischargeHits++;
        UnitState? inv = InvertsTick(to);
        // 第218期: 印を運ぶ（放電した駒が印を持っていれば、放電を受けた駒に +1・陣営を問わない）。量の口は澱みのデバフ（反転で回復になる放電には掛けない）。
        bool carry = _mireCarry && from.RawCounter(StatusKeys.Concentrated) > 0;
        if (inv is null && _mireDull != 0) amt = MireCut(from, amt, 2);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Discharge, Turn = _turn, ActorId = from.InstanceId, TargetId = to.InstanceId,
            Amount = amt, Slot = depth + 1, Team = to.TeamId,
            SourceTrait = inv is null ? null : TraitId.Inverse, InverterId = inv?.InstanceId,
        });
        if (inv is not null)
        {
            int hb = to.Hp;
            InverseHeal(inv, to, amt, 3, "放電");
            tt.DischargeInvertedIn += to.Hp - hb;
            if (carry) MireCarryTo(to, from);
            return;
        }
        Log($"    {from.Name} から {to.Name} へ放電（{amt}）", LogKind.Status);
        int before = to.Hp;
        _shockNext = 3;
        _shockKillerSet = true;
        _shockKiller = ini;
        _dischargeDepth++;   // 第220期・計数のみ（爆発の連鎖の最初の死が放電か）
        ApplyDamage(to, amt, from, isFriendlyFire: true);
        _dischargeDepth--;
        _shockNext = 0;
        _shockKillerSet = false;
        _shockKiller = null;
        int removed = before - Math.Max(0, to.Hp);
        ft.DischargeDealt += removed;
        tt.DischargeTaken += removed;
        if (fromBall) TallyOf(_silkOwner[from]).SilkDealt += removed;   // 第292期・計数のみ
        if (before > 0 && !to.IsAlive) tt.DischargeDeaths++;
        if (carry) MireCarryTo(to, from);
    }

    /// <summary>
    /// 糸を伝った放電（第290期・<see cref="Discharge"/> だけが呼ぶ）。<paramref name="via"/> ＝ 糸の保持者（クグ）、<paramref name="to"/> ＝ 糸の先の敵。
    /// <c>ApplyDamage(敵, 放電, クグ)</c>（味方の刃ではない・撃破者は連鎖を起こした一撃の主）。敵が感電していればこの一撃で弾けて連鎖に入る（敵の陣へ放電が広がる）。
    /// KG-b（<see cref="TraitId.ThreadCharge"/>）は、浴びる前に感電していなかった敵が生きていれば感電させる。<b>乱数を引かない。</b>
    /// </summary>
    void ThreadDischarge(UnitState from, UnitState via, UnitState to, int amt, int depth, UnitState? ini)
    {
        UnitTally vt = TallyOf(via), tt = TallyOf(to);
        _lastThreadKugu = via;
        if (from == via) vt.ThreadSelf++; else vt.ThreadRelay++;
        TallyOf(from).DischargeHits++;
        if (_mireDull != 0) amt = MireCut(from, amt, 2);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Discharge, Turn = _turn, ActorId = via.InstanceId, TargetId = to.InstanceId,
            Amount = amt, Slot = depth + 1, Team = to.TeamId, SourceTrait = TraitId.Thread,
            PartnerId = from == via ? null : from.InstanceId,   // 第291期・表示専用（② 隣の味方の放電を移したときの元の駒）
            Text = via.RawCounter(GrappleTrait.TargetKey) == 0 && via.RawCounter(ThreadTrait.MemoKey) > 0 ? ThreadLabels.Release : null,   // 第291期・表示専用（ほどける一撃の中）
        });
        Log($"    {(from == via ? "" : from.Name + " の電気が ")}{via.Name} の糸を伝って {to.Name} へ放電（{amt}）", LogKind.Trigger);
        bool wasShocked = to.RawCounter(StatusKeys.Shock) > 0;
        int before = to.Hp;
        _shockNext = 3;
        _shockKillerSet = true;
        _shockKiller = ini;
        _dischargeDepth++;
        ApplyDamage(to, amt, via);
        _dischargeDepth--;
        _shockNext = 0;
        _shockKillerSet = false;
        _shockKiller = null;
        int removed = before - Math.Max(0, to.Hp);
        vt.ThreadDealt += removed;
        TallyOf(from).DischargeDealt += removed;
        tt.DischargeTaken += removed;
        if (before > 0 && !to.IsAlive) { tt.DischargeDeaths++; vt.ThreadKills++; }
        if (wasShocked && to.RawCounter(StatusKeys.Shock) <= 0) vt.ThreadPopped++;
        if (!wasShocked && to.IsAlive && via.HasTrait(TraitId.ThreadCharge) && MarkShock(to, via))
        {
            vt.ThreadCharged++;
            to.SetCounter(ThreadMarkKey, 1);   // 計数専用（そこから起きた連鎖）
            Log($"    {to.Name} は糸の電気を浴びて帯電した", LogKind.Status);
        }
    }

    /// <summary>決着時に残っていた感電を数える（第214期・<b>計数のみ</b>）。</summary>
    public void CloseShockLedger()
    {
        if (!_shockLive) return;
        foreach (UnitState u in _units)
        {
            if (u.RawCounter(StatusKeys.Shock) <= 0) continue;
            if (u.IsAlive) TallyOf(u).ShockLeftAlive++; else TallyOf(u).ShockLeftDead++;
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
    /// <param name="spreadFrom">
    /// <b>表示専用</b>（第183期 追補2）。伝染（<see cref="PoisonRoute.Touch"/>）のときだけ、
    /// <b>うつした元の敵（殴られた標的）</b>を渡す。台本の <see cref="BattleEvent.SpreadFromId"/> に載るだけで、
    /// <b>どの規則も読まない</b>。
    /// </param>
    public void Poison(UnitState target, int amount, UnitState writer, PoisonRoute route,
                       UnitState? spreadFrom = null)
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

        // 第273期（レリック・毒を招く）: 受ける層を2倍（滲みの後・書く直前）。保持者がいない戦は比較1つで抜ける。
        if (_poisonMagnetLive && target.HasTrait(TraitId.RelicPoisonMagnet)) add *= RelicPoisonMagnetTrait.Factor;
        target.SetCounter(StatusKeys.Poison, target.RawCounter(StatusKeys.Poison) + add);
        BurstBook.PoisonWrites[(int)route]++; BurstBook.PoisonAmount[(int)route] += add;   // 第220期・**計数のみ**
        EmitStatusGain(target, StatusKeys.Poison, add, writer, route, spreadFrom);   // 第97期・表示専用（滲みで増えたぶんも込み）。第183期 追補2: 経路と伝染元
        if (add != amount)
            Log($"    {target.Name} の{(deepW ? "深手" : "傷口")}から毒が滲みた（+{add - amount}）", LogKind.Status);
    }

    /// <summary><see cref="UnitTally.SoakSeenByRoute"/> の長さ（毒 9 経路 ＋ 燃焼 1。第180期に吐き戻しで1本、
    /// 第183期に触れてうつす・その漏れで2本、第190期に澱み分けで1本、第195期にスィドの吐きで1本、第197期に紅蓮の奔流で1本、第216期に開戦の撒きで1本、第223期に状態の矢で1本増えた）。**毒の経路を足したら燃焼の添字も後ろへずらすこと**
    /// ——ずらさないと新しい経路の添字が燃焼と重なる。</summary>
    public const int SoakRouteCount = 14;

    /// <summary>燃焼の経路の添字（<see cref="UnitTally.SoakSeenByRoute"/> の末尾）。</summary>
    public const int SoakBurnRouteIx = 13;

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
        // 第207期: 燃えやすい板（ツギ）の印があれば倍（点く・点け直しの口・Q0-4）。印が無ければ比較1つで抜ける。
        turns = PlankFlare(target, turns, fromKiss: false);

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
        if (_fireLvLive) FireKeepLit(target, source, relit);   // 第242期（保つ火: 0 なら 1・1 以上は上げない）
        // 第250期（くべられる火・札 `PyreFed`）: 燃えていたホタに、ホタ以外の味方が火を点けた——攻撃力 +2（火勢は上げない）。札が無ければ比較1つで抜ける。
        if (relit && source is not null && source != target && source.TeamId == target.TeamId && target.HasTrait(TraitId.PyreStage))
        {
            FireBook.FedChanceBy[source.Def.Id] = FireBook.FedChanceBy.GetValueOrDefault(source.Def.Id) + 1;   // 第250期 Phase 0（計数のみ）
            if (target.HasTrait(TraitId.PyreFed)) FeedAtk(target, source, FireFeedRule.FedAtk, overflow: false);
        }
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

    /// <summary>
    /// 呪いの受け渡しの出どころ（第182期・<b>表示専用</b>）。共有の段が <c>ApplyDamage</c> を呼ぶ間だけ立ち、
    /// <c>hexShare</c> が真の段の <c>Damage</c> イベントが読む。<b>どの規則も読まない。</b>
    /// </summary>
    int? _hexShareFrom;

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
        BurnLinkBook.FavorCalls++;   // 第233期・**計数のみ**
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
            case StatusKeys.Armor:
                NoteCarry(u, UnitTally.CarryArmor, delta);
                // 第211期（計数のみ・戦績表の「破片(与)」）: 味方（自分を含む）に書いた破片の量を、書き手（第94期の印）に付ける。
                if (Mark.Owner is UnitState aw && aw.TeamId == u.TeamId) TallyOf(aw).ArmorOut += delta;
                // 第208期（計数のみ）: ツギ以外の書き手の破片（反射の「混ざった板」）。
                if (_reboundLive && Mark.Id != TraitId.Plank) u.SetCounter(PlankTrait.MixedKey, 1);
                break;
            case StatusKeys.Burn: NoteCarry(u, UnitTally.CarryBurn, delta); break;
            // 第146期 段0（表示専用）: 付いた瞬間。**計数の隣に置くだけで盤面は1ビットも動かない。**
            case StatusKeys.Stun: NoteCarry(u, UnitTally.CarryStun, 1); EmitStun(u, StunLabels.Struck, Mark.Owner); break;
            // 第147期（表示専用）: 混乱が付いた瞬間。**計数（NoteCarry）は足していない**
            // ——`UnitTally.CarryKeys` を増やすと過去の期の帳簿が動く。書き手は Mark.Owner。
            case StatusKeys.Confused: EmitConfused(u, ConfusedLabels.Lost, Mark.Owner); break;
            case StatusKeys.Marked: NoteCarry(u, UnitTally.CarryMark, 1); NoteMarkWrite(u, delta); if (_mfLive) QueueFeatherMark(u, delta); break;   // 第298期（MF・保持者がいなければ比較1つで抜ける）
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
    /// 通常の実行では誰も渡さない（既定は <see cref="CurseRule.Default"/> ＝ <b>第182期から共有する</b>。
    /// 第181期までは共有しなかった ＝ <see cref="CurseRule.Off"/>）。
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

    /// <summary>
    /// <b>陣営ごとに実際に減った HP の累計</b>（第205期・施しのリリの「痛み」）。<see cref="ApplyDamage"/> が HP を引いた直後に、
    /// <see cref="HpRemoved"/> と同じ実額（過剰分を除く）を<b>当たった駒の陣営</b>に足す。出どころは問わない（敵の攻撃・巻き込み・刻み・代価すべて）。
    /// <b>破片で受けた分は数えない</b>（破片は HP を引く前に削られるので、ここへ届かない）。<b>肩代わりは二重にならない</b>
    /// ——中継の段は別の <c>ApplyDamage</c> 呼び出しで、元の被弾者に残った分と肩代わりした駒の分がそれぞれ1回ずつ入る。
    /// 読むのは <see cref="KissTrait"/> だけ（痛みの版の札を持つとき）。<c>ApplyDamage</c> を通らない HP の減り（旧ノノの繕いの代価）は入らない。
    /// </summary>
    long _painLostPlayer, _painLostEnemy;

    /// <summary>その陣営が戦の開始から実際に失った HP の累計（第205期）。</summary>
    public long PainLostOf(int team) => team == PlayerTeam ? _painLostPlayer : _painLostEnemy;

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

    /// <summary>燃焼の在り方と脆さの帳簿（第219期・<b>計数専用</b>）。</summary>
    public readonly BrittleLedger BrittleBook = new();

    /// <summary>倒れた瞬間の在庫と澱みの爆発の帳簿（第220期・<b>計数専用</b>）。</summary>
    public readonly BurstLedger BurstBook = new();

    /// <summary>燃焼の繋ぎの発火見込み（第233期・<b>計数専用</b>）。</summary>
    public readonly BurnLinkLedger BurnLinkBook = new();
    /// <summary>燃焼の刻みの中か（第233期・<b>計数専用</b>。<see cref="NoteBurnLink"/> が「燃えていた」を数えるためだけに読む）。</summary>
    bool _inBurnTickNow;

    bool BurningForLink(UnitState u) => u.RawCounter(StatusKeys.Burn) > 0;

    /// <summary>① 延焼 ／ ③ 火の受け渡しの見込み（第233期・<b>計数のみ</b>・乱数を引かない）。倒れた直後（`Death` を打つ前）に呼ぶ。</summary>
    void NoteBurnLink(UnitState dead, UnitState? killer)
    {
        BurnLinkLedger b = BurnLinkBook;
        int ph = BurnLinkLedger.PhaseOf(_turn);
        if (dead.TeamId == EnemyTeam)
        {
            b.FoeDeaths[ph]++;
            if (BurningForLink(dead) || _inBurnTickNow)
            {
                b.FoeBurnDeaths[ph]++;
                int any = 0, unburnt = 0;
                foreach (UnitState x in _units)
                {
                    if (x == dead || !x.IsAlive || x.TeamId != dead.TeamId || !FormationRules.AreAdjacent(dead, x)) continue;
                    any++;
                    if (!BurningForLink(x)) unburnt++;
                }
                if (any > 0) b.FoeBurnDeathNeighbor[ph]++;
                if (unburnt > 0) b.FoeBurnDeathUnburntNeighbor[ph]++;
                b.FoeBurnDeathUnburntSum[ph] += unburnt;
            }
            if (killer is not null && killer.TeamId == PlayerTeam)
            {
                b.AllyKills[ph]++;
                if (BurningForLink(killer))
                {
                    b.AllyBurnKills[ph]++;
                    int unburnt = 0;
                    foreach (UnitState x in _units)
                        if (x != killer && x.IsAlive && x.TeamId == killer.TeamId && FormationRules.AreAdjacent(killer, x) && !BurningForLink(x)) unburnt++;
                    if (unburnt > 0) b.AllyBurnKillUnburntNeighbor[ph]++;
                    b.AllyBurnKillUnburntSum[ph] += unburnt;
                }
            }
        }
    }

    /// <summary>燃焼の刻みそのものの札（第219期・<see cref="ApplyDamageBody"/> の頭で読んで消す）。</summary>
    bool _burnTickSelf;
    /// <summary>叩きつけ・通電の札（第219期・<b>計数の経路だけ</b>）。</summary>
    bool _brittleSlamNext;

    // =================================================================================
    // 第220期 —— 澱みが爆ぜる（`MireBurst` / `MireBurstStack` / `MireBurstAll`・ミオ）
    //
    // 印を持つ駒が倒れると、同じ陣営の隣の生きている駒すべてに爆発（状態異常のダメージ）。**幅優先**——連鎖の最初の死で
    // `EnqueueBurst` が列を回し、爆発で倒れた駒は列の後ろに積まれる（席番号の順に当てるので積む順も席番号の順）。
    // **1つの駒は1回しか爆ぜない**（蘇生されても）。倒れた駒ごとに ①爆ぜる → ②印が隣へ移る（M5・爆発の後に生き残った隣へ）。
    // 出どころは倒れた駒（同じ陣営・`isFriendlyFire`）なので棘・板の反射は鳴らず、粛の窓口も通らない。燃焼の脆さは乗る（経路 8）。
    // 撃破は連鎖の最初の死を倒した一撃の主（放電と同じ `_shockKiller` の札）。**乱数を引かない。札が無ければ比較1つで抜ける。**
    // =================================================================================

    /// <summary>0 なし ／ 1 B1（毒 ÷ 2）／ 2 B2（毒 ×（1＋印）÷ 4）。</summary>
    byte _burstMode;
    /// <summary>B2x（敵味方の両方）。</summary>
    bool _burstAll;
    bool _burstRunning, _burstHitNext;
    int _burstStage;
    UnitState? _burstRoot;
    int _dischargeDepth, _thunderDepth;   // 計数のみ
    int _burstChainIdx;                   // 計数のみ（戦の何本目の連鎖か）
    readonly Queue<(UnitState Dead, int Poison, int Marks, int Stage)> _burstQueue = new();
    readonly HashSet<int> _burstDone = new();

    /// <summary>爆発の量（B1 ＝ 毒 ÷ 2 ／ B2 ＝ 毒 ×（1＋印）÷ 4・切り捨て1回）。</summary>
    int BurstAmount(int poison, int marks) => _burstMode == 1 ? poison / 2 : poison * (1 + marks) / 4;

    void EnqueueBurst(UnitState dead, int marks, UnitState? killer)
    {
        if (!_burstDone.Add(dead.InstanceId))
        {
            // 既に爆ぜた駒（蘇生されて再び倒れた）は爆ぜない。印の移りだけは今までどおり。
            if (_mireHandoff && dead.TeamId != _mireHolder!.TeamId) MireHandoff(dead, marks);
            return;
        }
        int stage = _burstRunning ? _burstStage + 1 : 0;
        _burstQueue.Enqueue((dead, dead.RawCounter(StatusKeys.Poison), marks, stage));
        if (_burstRunning) return;

        BurstLedger b = BurstBook;
        if (_dischargeDepth > 0) b.RootByDischarge++; else if (_thunderDepth > 0) b.RootByThunder++;
        _burstRunning = true;
        _burstRoot = killer;
        int n = 0, burst = 0; long nominal = 0;
        try
        {
            while (_burstQueue.Count > 0)
            {
                var q = _burstQueue.Dequeue();
                _burstStage = q.Stage;
                int amt = BurstOne(q.Dead, q.Poison, q.Marks, q.Stage);
                if (amt > 0) { burst++; nominal += amt; }
                n++;
                if (_mireHandoff && q.Dead.TeamId != _mireHolder!.TeamId)
                {
                    int m = q.Dead.RawCounter(StatusKeys.Concentrated);
                    if (m > 0) MireHandoff(q.Dead, m);
                }
            }
        }
        finally { _burstRunning = false; _burstRoot = null; _burstStage = 0; }
        b.ChainLenHist[Math.Min(n, 10)]++;
        int ci = Math.Min(_burstChainIdx++, 9);
        b.ChainIdxChains[ci]++; b.ChainIdxBursts[ci] += burst; b.ChainIdxNominal[ci] += nominal;
    }

    int BurstOne(UnitState dead, int poison, int marks, int stage)
    {
        BurstLedger b = BurstBook;
        int side = SideOf(dead);
        int amt = BurstAmount(poison, marks);
        if (amt <= 0) { b.BurstsEmpty[side]++; return 0; }
        var targets = LivingMembers(dead.TeamId).Where(u => u != dead && FormationRules.AreAdjacent(dead, u)).OrderBy(u => u.Slot).ToList();
        int st = Math.Min(stage, 9);
        b.Bursts[side]++;
        b.StageBursts[st]++;
        Log($"    ★ {dead.Name} の澱みが爆ぜた（{amt}・{targets.Count} 体・{stage + 1} 段目）", LogKind.Highlight, _mireHolder);
        foreach (UnitState u in targets)
        {
            if (!u.IsAlive) continue;
            b.Hits[side]++; b.Nominal[side] += amt;
            b.StageHits[st]++; b.StageNominal[st] += amt;
            UnitState? inv = InvertsTick(u);
            if (_verbose)
            {
                int? be = Ember.Brittle > 0 && BrittleApplies(u) && u.RawCounter(StatusKeys.Burn) > 0 ? (amt * BrittlePct(u, out _) + 99) / 100 : null;
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.MireBurst, Turn = _turn, ActorId = _mireHolder?.InstanceId, SpreadFromId = dead.InstanceId,
                    TargetId = u.InstanceId, Amount = amt, Slot = stage + 1, StatusRemaining = marks, BrittleExtra = be,
                    InverterId = inv?.InstanceId, Team = u.TeamId,
                    SourceTrait = _burstAll ? TraitId.MireBurstAll : _burstMode == 1 ? TraitId.MireBurst : TraitId.MireBurstStack,
                });
            }
            if (inv is not null)
            {
                int hb = u.Hp;
                InverseHeal(inv, u, amt, 4, "澱みの爆発");
                b.InverseHits++; b.InverseNominal += amt; b.InverseHealed += u.Hp - hb;
                continue;
            }
            bool shocked = u.RawCounter(StatusKeys.Shock) > 0;
            int before = u.Hp;
            _shockKillerSet = true;
            _shockKiller = _burstRoot;
            _burstHitNext = true;
            ApplyDamage(u, amt, dead, isFriendlyFire: true);
            _burstHitNext = false;
            _shockKillerSet = false;
            _shockKiller = null;
            b.Removed[side] += before - Math.Max(0, u.Hp);
            if (before > 0 && !u.IsAlive) b.Kills[side]++;
            if (shocked && u.RawCounter(StatusKeys.Shock) <= 0) b.ShockPops[side]++;
        }
        return amt;
    }

    /// <summary>倒れた瞬間の在庫（第220期・<b>計数のみ</b>）。</summary>
    void NoteDeathStock(UnitState dead)
    {
        BurstLedger b = BurstBook;
        int side = SideOf(dead);
        b.Deaths[side]++;
        int nb = 0;
        foreach (UnitState u in _units)
            if (u != dead && u.IsAlive && u.TeamId == dead.TeamId && FormationRules.AreAdjacent(dead, u)) nb++;
        b.NeighborHist[side][Math.Min(nb, 5)]++;
        b.NeighborSum[side] += nb;
        int cm = dead.RawCounter(StatusKeys.Concentrated);
        if (cm <= 0) return;
        int p = dead.RawCounter(StatusKeys.Poison);
        b.DeathsMarked[side]++;
        b.PoisonHist[side][BurstLedger.PoisonBin(p)]++;
        b.MarkHist[side][Math.Min(cm, 5)]++;
        b.PoisonSum[side] += p;
        b.MarkSum[side] += cm;
    }

    /// <summary>燃焼の脆さがこの駒の陣営に掛かるか（第219期）。敵だけの版は敵陣営にだけ。</summary>
    bool BrittleApplies(UnitState u) => Ember.BrittleAllies || u.TeamId != PlayerTeam;

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
        // 第219期・**計数のみ**（書き手ごとに、相手陣営／同じ陣営に付けた回数）
        var book = source is not null && source.TeamId == target.TeamId ? BrittleBook.IgniteAlly : BrittleBook.IgniteFoe;
        string key = source?.Def.Id ?? (target.TeamId == PlayerTeam ? "-p" : "-e");   // 書き手なしは付いた側で分ける
        book[key] = (book.TryGetValue(key, out long c) ? c : 0) + 1;
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

        // 第184期 §1 の帳簿: ターン頭に敵の標が何体に立っていたか（計数のみ）。
        long foes = _units.Count(u => u.IsAlive && u.TeamId != PlayerTeam && u.RawCounter(StatusKeys.Marked) > 0);
        MarkFoeUnitTurns += foes;
        if (foes > 0) MarkFoeTurns++;
        if (foes > MarkFoeMax) MarkFoeMax = foes;
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
    public long AshFires, AshSpent, AshHits, AshDry, AshFallout, AshHolds;
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
    void NoteAsh(UnitState target, int amount, UnitState? source, int havocExtra, bool discharge = false)
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
            if (discharge) TallyOf(h).AshFromDischarge += gained;   // 第214期・計数のみ
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

    /// <summary>溜めに専念した手番（第179期 追補・<b>計数のみ</b>）。</summary>
    public void NoteAshHold(UnitState self, int carried)
    {
        AshHolds++;
        UnitTally t = TallyOf(self);
        t.AshHolds++;
        if (carried > t.AshPeak) t.AshPeak = carried;   // 在庫の山は投げる直前とは限らない
    }

    /// <summary>灰が1体に着弾した（<b>計数のみ</b>。名目量）。</summary>
    public void NoteAshHit(int dmg) { AshHits++; }

    /// <summary>灰が隣へ降った（<b>計数のみ</b>。名目量）。</summary>
    public void NoteAshFallout(UnitState from, int amount)
    {
        AshFallout += amount;
        TallyOf(from).AshFalloutOut += amount;
    }

    /// <summary>倒れた時点で抱えていた灰（<b>計数のみ</b>）。</summary>
    public void NoteAshAtDeath(UnitState self, int ash)
    {
        if (ash <= 0) return;
        AshAtDeath += ash; TallyOf(self).AshAtDeath += ash;
    }

    // =====================================================================================
    // 第180期 —— 暴発（ムド）／泥散り（ムド）／吐き戻し（ヴィオ）／叩き起こし（ガン）
    // **どれも計数専用。engine には規則も窓口も1本も足していない**（判定はすべて特性の中）。
    // =====================================================================================

    /// <summary>暴発の燃料（数えた被弾）／味方の刃から数えた分 ／ 暴発した回数 ／ 放った発数 ／
    /// 入れ子で見送った回数（<c>InInterrupt</c>・<c>InReaction</c> の中で閾値に届いた回数）。</summary>
    public long EruptFuel, EruptFuelFromAlly, EruptFires, EruptSwings, EruptHeld;

    /// <summary>床が効いた発（第181期・<b>計数のみ</b>）。</summary>
    public long EruptFloored;

    /// <summary>泥散りが撒いた総量 ／ 支援拒否（<c>Stoic</c>）で弾かれた回数。</summary>
    public long SmearDealt, SmearBlocked;

    /// <summary>吐き戻し: 腹に記帳した層 ／ 吐いた層 ／ 吐いた回数。</summary>
    public long SpitStored, SpitMoved, SpitFires;

    /// <summary>叩き起こし: 起こした回数 ／ 起こす相手がいなかった回数。</summary>
    public long ReveilleFires, ReveilleMisses;

    /// <summary>暴発の燃料を1つ数えた（<b>計数のみ</b>）。</summary>
    public void NoteEruptFuel(UnitState self, UnitState source)
    {
        EruptFuel++;
        UnitTally t = TallyOf(self);
        t.EruptFuel++;
        if (source.TeamId == self.TeamId) { EruptFuelFromAlly++; t.EruptFuelFromAlly++; }
    }

    /// <summary>閾値に届いたが入れ子だったので見送った（<b>計数のみ</b>）。</summary>
    public void NoteEruptHeld(UnitState self) { EruptHeld++; TallyOf(self).EruptHeld++; }

    /// <summary>暴発した（<b>計数のみ</b>）。<paramref name="n"/> は溜まっていた怒り。</summary>
    public void NoteEruptFire(UnitState self, int n)
    {
        EruptFires++;
        UnitTally t = TallyOf(self);
        t.EruptFires++;
        if (n > t.EruptPeak) t.EruptPeak = n;
    }

    /// <summary>
    /// 暴発の1発（<b>計数のみ</b>・第181期に値を足した）。
    /// <paramref name="atk"/> はその発の攻撃力、<paramref name="floored"/> は
    /// <b>床が無ければ素攻より下だった発</b>（＝その時点で <c>AtkBonus &lt; 0</c>）。
    /// </summary>
    public void NoteEruptSwing(UnitState self, int atk = 0, bool floored = false)
    {
        EruptSwings++;
        UnitTally t = TallyOf(self);
        t.EruptSwings++;
        t.EruptSwingAtk += atk;
        if (floored) { EruptFloored++; t.EruptFloored++; }
    }

    /// <summary>泥が散った（<b>計数のみ</b>。名目量）。</summary>
    public void NoteSmear(UnitState self, int amount)
    {
        SmearDealt += amount; TallyOf(self).SmearDealt += amount;
    }

    /// <summary>支援拒否が泥散りを弾いた（<b>計数のみ</b>）。</summary>
    public void NoteSmearBlocked(UnitState self) { SmearBlocked++; TallyOf(self).SmearBlocked++; }

    /// <summary>腹に層を記帳した（<b>計数のみ</b>。読み手がいなくても数える＝版に依らない分母）。</summary>
    public void NoteSpitStore(UnitState self, int stacks)
    {
        SpitStored += stacks; TallyOf(self).SpitStored += stacks;
    }

    /// <summary>腹から吐いた（<b>計数のみ</b>）。</summary>
    public void NoteSpit(UnitState self, int stacks)
    {
        SpitMoved += stacks; SpitFires++;
        UnitTally t = TallyOf(self);
        t.SpitMoved += stacks; t.SpitFires++;
    }

    /// <summary>叩き起こした（<b>計数のみ</b>）。<b>起こされた側にも記録する</b>——「誰が起きたか」の内訳。</summary>
    public void NoteReveille(UnitState self, UnitState woken)
    {
        ReveilleFires++;
        TallyOf(self).ReveilleFires++;
        TallyOf(woken).ReveilleWoken++;
    }

    /// <summary>起こす相手がいなかった（<b>計数のみ</b>）。</summary>
    public void NoteReveilleMiss(UnitState self) { ReveilleMisses++; TallyOf(self).ReveilleMisses++; }

    // =====================================================================================
    // 第183期 —— 縫い合わせ（ヴェル）／触れてうつす・漏れ（ラウ）
    // **どれも計数専用。engine には規則も窓口も1本も足していない**（判定はすべて特性の中）。
    // =====================================================================================

    /// <summary>
    /// 漏れた毒の印（<b>私有キー・計数専用</b>）。漏れを受けた味方に積み、澱み喰い（ヴィオ）が
    /// 吸い上げたときに「そのうち漏れ由来は何層か（上限）」を数えて 0 に戻す。<b>盤面の誰も読まない。</b>
    /// </summary>
    public const string TouchLeakMarkKey = "touchLeakMark";

    /// <summary>縫った（<b>計数のみ</b>）。<b>縫われた側にも記録する</b>——「誰が縫われたか」の内訳。</summary>
    public void NoteStitch(UnitState self, UnitState patient, int healed, int scar)
    {
        UnitTally t = TallyOf(self);
        t.StitchFires++; t.StitchHealed += healed; t.StitchScarDealt += scar;
        UnitTally p = TallyOf(patient);
        p.StitchedTimes++; p.StitchScarTaken += scar;
    }

    /// <summary>縫おうとしたが渇きに封じられた（<b>計数のみ</b>）。</summary>
    public void NoteStitchSealed(UnitState self) => TallyOf(self).StitchSealed++;

    /// <summary>傷ついた隣人がいなかったので殴った（<b>計数のみ</b>）。</summary>
    public void NoteStitchSwing(UnitState self) => TallyOf(self).StitchSwings++;

    /// <summary>うつした（<b>計数のみ</b>）。</summary>
    public void NoteTouch(UnitState self, int targets, int layers)
    {
        UnitTally t = TallyOf(self);
        t.TouchFires++; t.TouchTargets += targets; t.TouchLayers += layers;
    }

    /// <summary>標的に毒はあったが、隣に敵が1体もいなかった（<b>計数のみ</b>）。</summary>
    public void NoteTouchMiss(UnitState self) => TallyOf(self).TouchMisses++;

    /// <summary>漏れた（<b>計数のみ</b>）。受け手の側にも載せ、澱み喰いが読むための印を積む。</summary>
    public void NoteTouchLeak(UnitState self, UnitState ally, int layers)
    {
        TallyOf(self).TouchLeakOut += layers;
        TallyOf(ally).TouchLeakIn += layers;
        ally.SetCounter(TouchLeakMarkKey, ally.RawCounter(TouchLeakMarkKey) + layers);
    }

    /// <summary>
    /// 澱み喰いが味方の毒を吸った瞬間に、そのうち漏れ由来の上限を数える（<b>計数のみ</b>）。
    /// <paramref name="drawn"/> はその味方から吸った層。印は 0 に戻す。
    /// </summary>
    public void NoteLeakDrawn(UnitState reader, UnitState ally, int drawn)
    {
        // 第190期・**計数のみ**。吸い上げた層のうちベニの澱み分けの分。
        int taint = ally.RawCounter(TaintTrait.HeldKey);
        if (taint > 0)
        {
            ally.SetCounter(TaintTrait.HeldKey, 0);
            TallyOf(reader).VioAteTaint += Math.Min(taint, drawn);
        }
        int mark = ally.RawCounter(TouchLeakMarkKey);
        if (mark <= 0) return;
        ally.SetCounter(TouchLeakMarkKey, 0);
        int fromLeak = Math.Min(mark, drawn);
        UnitTally t = TallyOf(reader);
        t.TouchLeakDrawn += fromLeak; t.TouchLeakDrawFires++;
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

    /// <summary>
    /// 敵の標の被ダメージ増（第184期 §1。既定は <see cref="MarkRule.Default"/>）。
    /// <b>敵に標を付ける駒（ソラ・ザン）がいなければ1ビットも動かない</b>——敵の <c>Marked</c> が 0 のまま。
    /// 判定は <c>ApplyDamage</c> の入口の族の1箇所。
    /// </summary>
    public MarkRule MarkRules { get; }

    // =====================================================================================
    // 第184期 —— 標の軸の帳簿（**計数専用。どの規則も読まない**）
    // =====================================================================================

    /// <summary>矢面（ヒサ）の保持者。<c>ApplyDamage</c> の半減の判定を、保持者がいない盤面で1回も走らせないため。</summary>
    readonly List<UnitState> _beckonHolders = new();

    /// <summary>逸らし（第186期・ソラ）の保持者。<c>ApplyDamage</c> の入口の判定を、保持者がいない盤面で1回も走らせないため。</summary>
    readonly List<UnitState> _deflectHolders = new();

    /// <summary>
    /// いま入れようとしている <c>ApplyDamage</c> が逸らしの受け渡しであるときの、逸らした駒（第186期）。
    /// <b>本体の最初の行で読んで消す</b>（引数を足さずに、その1回の呼び出しにだけ札を渡すため）。
    /// </summary>
    UnitState? _deflectFrom;
    int? _deflectCharge;   // 第186期 追補・表示専用。逸らしの受け渡しに載せる溜めの段（_deflectFrom と対で読んで消す）

    /// <summary>突き（第186期 追補）の保持者が盤上に1体でもいるか。いなければ列の指定と倍率の判定を比較1つで抜ける。</summary>
    bool _thrustLive;
    bool _heroShieldLive;   // 第267期（勇者の庇いの短絡・保持者は bosswave の勇者だけ）

    // ---- 第223期: 回避（逃げ上手のセロ・`EvadeTrait`）。**保持者がいなければ `_evadeLive` の比較1つで全部抜ける。** ----
    bool _evadeLive;

    // ---- 第226期: 回避盾（`DecoyTrait`）と敵の乱れ（`DisarrayTrait`）。**保持者がいなければ比較1つで全部抜ける。** ----
    bool _decoyLive, _disarrayLive;
    /// <summary>第228期: 弾き返し（<c>SpringTrait</c>）の保持者が戦にいるか。いなければ被弾の後の判定を比較1つで抜ける。</summary>
    bool _springLive;
    bool _overflowLive, _poisonMagnetLive;   // 第273期（レリック・溢れの刃 ／ 毒を招く）
    bool _tailwindLive;   // 第229期（追い風の保持者がいる戦）
    bool _impactLive;     // 第230期（撃破の衝撃の保持者がいる戦）

    /// <summary>
    /// 転倒の穴（第229期・<see cref="ShufflerRule.StaggerHole"/>）: この駒は転倒していて壁にならない・引き受ける介入をしないか。
    /// 規則が偽なら常に偽（第228期と1ビットも違わない）。
    /// </summary>
    public bool IsFallen(UnitState u) => Shuffler.StaggerHole && u.RawCounter(StatusKeys.Stagger) > 0;

    /// <summary>介入の候補から転倒した駒を外す（外したら計数して真）。<b>候補の絞り込みの最後の条件に置く</b>（計数が「ほかの条件は満たしていた」回になるように）。</summary>
    public bool HoleSkip(UnitState u)
    {
        if (!IsFallen(u)) return false;
        TallyOf(u).HoleSkips++;
        return true;
    }
    /// <summary>第228期・<b>計数専用</b>: いまハネが動かしている動作（0 なし ／ 1 吹っ飛ばし ／ 2 弾き返し）。敵の乱れの混乱の帰属だけが読む。</summary>
    int _haneAct;
    /// <summary>挑発が主目標にした駒（標的選択1回ぶん・計数と表示のためだけ）。</summary>
    UnitState? _decoyPicked;
    /// <summary>陣営ごとの「隊列を動かされた」累計（敵の乱れの段）。<c>SwapSlots</c> の通知が数える（保持者がいる戦だけ）。</summary>
    readonly int[] _disorder = new int[2];
    /// <summary>敵の乱れの札の保持者が戦にいるか（転倒を「動けない敵」に数えるかを <see cref="TormentTrait.IsBound"/> が読む）。</summary>
    public bool DisarrayLive => _disarrayLive;
    /// <summary>その陣営が隊列を動かされた累計（第226期・保持者がいない戦では 0 のまま）。</summary>
    public int DisorderOf(int teamId) => _disorder[teamId];
    /// <summary>次の標的選択1回にだけ効く「的の固定」（追い撃ち・乱れ撃ち）。`SelectTargetChain` の頭で読んで消す。</summary>
    UnitState? _forcedTarget;
    /// <summary>第231期（移動の追撃）: 的の固定と一緒に経路も固定する（−1 ＝ 固定しない・的のレーンから引く）。的の固定と同じく読んで消す。</summary>
    int _forcedLane = -1;
    /// <summary>第231期（挑発の表示）: いま「効いている」と台本に出している保持者（<b>表示専用・盤面の規則は読まない</b>）。</summary>
    readonly HashSet<int> _decoyShown = new();
    /// <summary>いまの `PerformAttack` の枠で回避した駒（主目標なら `OnAfterAttack` を走らせない）。入れ子は `PerformAttack` が退避する。</summary>
    UnitState? _evadedNow;
    /// <summary>セロの一撃の出どころ（0 手番 ／ 1 追い撃ち ／ 2 乱れ撃ち・<b>計数専用</b>）。</summary>
    int _evadeShotKind;
    /// <summary>回避の保持者が最後に受けた呼び出しの種類（倒れた原因の帳簿・<b>計数専用</b>）。</summary>
    int _evadeHitClass;

    /// <summary>
    /// いま `SwapSlots` で駒を動かしている駒（第223期・<b>計数専用・どの規則も読まない</b>）。回避の段の「動かされた出どころ」の帳簿だけが読む。
    /// `SwapSlots` の外では null。入れ子（入れ替えの中の入れ替え）は退避する。
    /// </summary>
    public UnitState? CurrentMover { get; private set; }

    /// <summary>
    /// 追い撃ち・乱れ撃ちの1本（第223期・<see cref="EvadeTrait"/> だけが呼ぶ）。**的を固定した `PerformAttack`**——
    /// 標的の鎖（庇い・後備え・標・執着）を通らず、それ以外（痺れ毒・萎縮・澱み・§1・破片・軛・反撃・`OnAfterAttack`）は今までどおり。
    /// 貫きなら的を通るレーンを前から後ろへ（`SelectTargetChain` の頭・<see cref="ForcedLane"/>）。
    /// </summary>
    /// <param name="kind">1 追い撃ち ／ 2 乱れ撃ち（帳簿の割り当てだけ）。</param>
    public void EvadeShot(UnitState sero, UnitState foe, bool pierce, int kind)
    {
        if (!sero.IsAlive || !foe.IsAlive) return;
        UnitTally t = TallyOf(sero);
        long before = t.DamageToEnemy;
        int prevKind = _evadeShotKind;
        _evadeShotKind = kind;
        _forcedTarget = foe;
        try { PerformAttack(sero, patternOverride: pierce ? AttackPattern.Pierce : AttackPattern.Single); }
        finally { _forcedTarget = null; _forcedLane = -1; _evadeShotKind = prevKind; }
        long d = t.DamageToEnemy - before;
        if (kind == 1) t.EvRiposteDealt += d; else if (kind == 3) t.EvMoveShotDealt += d; else t.EvBarrageDealt += d;
    }

    /// <summary>
    /// 移動の追撃の1本（第231期・<see cref="EvadeMoveShotTrait"/> だけが呼ぶ）。セロのいる経路と同じ番号の敵の経路を前から後ろへ貫く
    /// （中央のセロは生きている敵が多い経路・同数なら経路0）。その経路に敵がいなければ敵が多い方の経路へ落とす。的と経路を固定した
    /// <see cref="EvadeShot"/>（kind 3）なので、介入の鎖は通らず、状態の矢・痺れ毒・§1・破片・軛・反撃は今までどおり。<b>乱数を引かない</b>（貫きの中の既存の乱数を除く）。
    /// </summary>
    public void MoveShot(UnitState sero, int ordinal)
    {
        if (!sero.IsAlive) return;
        var foes = LivingMembers(Opponent(sero.TeamId));
        if (foes.Count == 0) return;
        FormationShape es = foes[0].Shape;
        var mine = sero.Shape.LanesOf(sero.Slot);
        int lane = mine.Count == 1 ? mine[0] : MostLane(mine.Count > 0 ? mine : new[] { 0, 1 });
        var occ = LaneOccupants(foes, lane, es);
        UnitTally t = TallyOf(sero);
        if (occ.Count == 0)
        {
            t.MoveShotFallback++;
            lane = MostLane(new[] { 0, 1 });
            occ = LaneOccupants(foes, lane, es);
            if (occ.Count == 0) return;
        }
        t.MoveShots++;
        (t.MoveShotByTurn ??= new long[7])[Math.Clamp(_turn, 0, 6)]++;
        UnitState ft = occ[0];
        Log($"    {sero.Name} は動かされた勢いで {ft.Name} の列へ矢を放った（移動の追撃・{ordinal} 本目）", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.MoveShot, Turn = _turn, ActorId = sero.InstanceId, TargetId = ft.InstanceId,
            Slot = lane, Amount = ordinal, Team = sero.TeamId,
        });
        _forcedLane = lane;
        EvadeShot(sero, ft, true, 3);

        int MostLane(IEnumerable<int> lanes)
        {
            int best = -1, bestN = -1;
            foreach (int l in lanes)
            {
                int n = LaneOccupants(foes, l, es).Count;
                if (n > bestN || (n == bestN && l < best)) { best = l; bestN = n; }
            }
            return best;
        }
    }

    /// <summary>
    /// 挑発が効いているか（第231期・<b>表示専用</b>）。盤面の挑発と同じ判定（<see cref="DecoyTrait.Eligible"/>）を、
    /// 自陣を敵の単体攻撃の的の側から見た pool に当てる。転倒の穴の計数（<see cref="HoleSkip"/>）は触らない。<b>盤面の規則はこれを読まない。</b>
    /// </summary>
    public bool DecoyShowNow(UnitState u) => DecoyTrait.Eligible(this, u, PoolOf(LivingMembers(u.TeamId).ToList()), count: false);

    /// <summary>挑発の表示を今の盤面に合わせ、切り替わった保持者だけ <see cref="BattleEventKind.DecoyShow"/> を出す（第231期・verbose のときだけ）。</summary>
    void RefreshDecoyShow()
    {
        if (!_verbose || !_decoyLive) return;
        foreach (UnitState u in _units)
        {
            if (!u.HasTrait(TraitId.Decoy)) continue;
            bool on = u.IsAlive && DecoyShowNow(u);
            if (on == _decoyShown.Contains(u.InstanceId)) continue;
            if (on) _decoyShown.Add(u.InstanceId); else _decoyShown.Remove(u.InstanceId);
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.DecoyShow, Turn = _turn, ActorId = u.InstanceId, Slot = on ? 1 : 0,
                Amount = EvadeTrait.PercentOf(u), Team = u.TeamId,
            });
        }
    }

    /// <summary>的を通るレーン（第223期）。0 本なら −1（単体）、2 本なら生きている駒が多い方・同数は添字の若い方（<b>乱数を引かない</b>）。</summary>
    int ForcedLane(UnitState ft)
    {
        var lanes = ft.Shape.LanesOf(ft.Slot);
        if (lanes.Count == 0) return -1;
        if (lanes.Count == 1) return lanes[0];
        var team = LivingMembers(ft.TeamId);
        int best = lanes[0], bestN = -1;
        foreach (int l in lanes)
        {
            int n = LaneOccupants(team, l, ft.Shape).Count;
            if (n > bestN) { best = l; bestN = n; }
        }
        return best;
    }

    /// <summary>乱れ撃ち（第223期・段2 以上のセロの手番）。5本の矢を生きている敵へ1本ずつ乱数で（前列の規則は無視）。</summary>
    void Barrage(UnitState sero)
    {
        TallyOf(sero).EvBarrages++;
        int arrows = EvadeTrait.ArrowsOf(sero);   // 第224期: 段3 ＋ 増し矢（F3）なら 7 本
        for (int i = 1; i <= arrows; i++)
        {
            if (!sero.IsAlive) break;
            var foes = LivingMembers(Opponent(sero.TeamId));
            if (foes.Count == 0) break;
            UnitState t = foes.Count == 1 ? foes[0] : foes[Roll(foes.Count)];
            TallyOf(sero).EvArrows++;
            Log($"    {sero.Name} の乱れ撃ち（{i} 本目）が {t.Name} へ", LogKind.Action);
            if (_verbose) Emit(new BattleEvent
            {
                Kind = BattleEventKind.Barrage, Turn = _turn, ActorId = sero.InstanceId, TargetId = t.InstanceId,
                Slot = i, Amount = arrows,
            });
            EvadeShot(sero, t, false, 2);
        }
    }

    /// <summary>避けた（第223期・表示専用の出来事 ＋ 計数）。<b>盤面は1ビットも触らない。</b></summary>
    public void NoteEvaded(UnitState sero, UnitState foe, int amount, UnitState? partner, AttackPattern? pattern)
    {
        UnitTally t = TallyOf(sero);
        t.Evades++;
        t.EvadedAmount += amount;
        Log($"    {sero.Name} は {foe.Name} の一撃をかわした（{amount}）", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Evade, Turn = _turn, ActorId = sero.InstanceId, TargetId = foe.InstanceId,
            Amount = amount, PartnerId = partner?.InstanceId, Pattern = pattern,
            StatusRemaining = sero.CurrentAttack, Slot = EvadeTrait.StageOf(sero), Team = sero.TeamId,
        });
    }

    /// <summary>必死の逃げ足（第227期・表示専用の出来事 ＋ 計数）。<b>盤面は1ビットも触らない。</b></summary>
    public void NoteLastDodge(UnitState sero, UnitState foe, int amount, int ordinal, int limit, AttackPattern? pattern)
    {
        TallyOf(sero).LastDodges++;
        Log($"    {sero.Name} は倒れる一撃を死ぬ気でかわした（{amount}・この戦 {ordinal}/{limit} 回目）", LogKind.Highlight);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.LastDodge, Turn = _turn, ActorId = sero.InstanceId, TargetId = foe.InstanceId,
            Slot = ordinal, StatusRemaining = limit, Amount = EvadeTrait.StageOf(sero), Pattern = pattern, Team = sero.TeamId,
        });
    }

    /// <summary>倒れる一撃が来たが必死の逃げ足の回数を使い切っていた（第227期・計数のみ）。</summary>
    public void NoteLastDodgeSpent(UnitState sero) => TallyOf(sero).LastDodgeSpent++;

    /// <summary>回避の判定を振った（第223期・計数のみ）。</summary>
    public void NoteEvadeRoll(UnitState sero) => TallyOf(sero).EvRolls++;

    /// <summary>入れ替えの結果（第223期・計数のみ）。0 入れ替わった ／ 1 隣がいない ／ 2 据えた足で空振り。</summary>
    public void NoteEvadeSwap(UnitState sero, int result)
    {
        UnitTally t = TallyOf(sero);
        if (result == 0) t.EvSwaps++; else if (result == 1) t.EvSwapNone++; else t.EvSwapRefused++;
    }

    /// <summary>追い撃ちを撃てなかった（第223期・計数のみ）。<paramref name="inReaction"/> なら反撃の中の回避。</summary>
    public void NoteRiposteBlocked(UnitState sero, bool inReaction)
    {
        UnitTally t = TallyOf(sero);
        if (inReaction) t.EvRiposteInReaction++; else t.EvRiposteHushed++;
    }

    /// <summary>追い撃ちの1本の直前（第223期・表示専用 ＋ 計数）。</summary>
    public void NoteRiposte(UnitState sero, UnitState foe, bool pierce, int index)
    {
        UnitTally t = TallyOf(sero);
        t.EvRipostes++;
        if (pierce) t.EvRipostePierce++;
        Log($"    {sero.Name} が {foe.Name} へ撃ち返す（{(pierce ? "貫き" : "単体")}・{index} 本目）", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.EvadeRiposte, Turn = _turn, ActorId = sero.InstanceId, TargetId = foe.InstanceId,
            Pattern = pierce ? AttackPattern.Pierce : AttackPattern.Single, Slot = index,
        });
    }

    /// <summary>動かされた（第223期・計数 ＋ 段が上がれば表示専用の出来事）。<b>盤面は1ビットも触らない。</b></summary>
    public void NoteEvadeMove(UnitState sero, int stageBefore, int stageAfter)
    {
        UnitTally t = TallyOf(sero);
        UnitState? by = CurrentMover;
        int src = by is null ? 6
                : by == sero ? 0
                : by.TeamId != sero.TeamId ? 5
                : by.Def.Id == "basa" ? 1 : by.Def.Id == "shio" ? 2 : by.Def.Id == "hane" ? 3 : 4;
        (t.EvMoveSrc ??= new int[7])[src]++;
        if (stageAfter <= stageBefore) return;
        var st = t.EvStageTurn ??= new int[4];
        for (int k = stageBefore + 1; k <= stageAfter; k++) if (st[k] == 0) st[k] = Math.Max(1, _turn);
        Log($"    {sero.Name} は動かされるほど身軽になる（段 {stageAfter}）", LogKind.Highlight);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.EvadeStage, Turn = _turn, ActorId = sero.InstanceId, TargetId = sero.InstanceId,
            Slot = stageAfter, Amount = EvadeTrait.MovesOf(sero),
        });
    }

    /// <summary>状態の矢（第223期・E2・表示専用の出来事 ＋ 計数）。</summary>
    public void NoteStatusArrow(UnitState sero, UnitState foe, bool poison, bool burn, bool shock)
    {
        UnitTally t = TallyOf(sero);
        if (poison)
        {
            t.ArrowPoison++;
            // 第224期・**計数のみ**: 矢で積んだ毒の層を覚えておき、刻みの名目のうち矢の層の分をセロに付ける。
            _arrowPoison[foe.InstanceId] = (sero, (_arrowPoison.TryGetValue(foe.InstanceId, out var ap) ? ap.Layers : 0) + StatusArrowTrait.PoisonStack);
        }
        if (burn) t.ArrowBurn++;
        if (shock) t.ArrowShock++;
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.StatusArrow, Turn = _turn, ActorId = sero.InstanceId, TargetId = foe.InstanceId,
            Text = string.Join(",", new[] { poison ? StatusKeys.LabelOf(StatusKeys.Poison) : null, burn ? StatusKeys.LabelOf(StatusKeys.Burn) : null,
                                             shock ? StatusKeys.LabelOf(StatusKeys.Shock) : null }.Where(x => x is not null)),
        });
    }

    /// <summary>状態の矢で積んだ毒の層（第224期・<c>InstanceId</c> → 書いたセロと層・<b>計数専用</b>）。</summary>
    readonly Dictionary<int, (UnitState Sero, int Layers)> _arrowPoison = new();

    /// <summary>敵の標の出どころ（<c>InstanceId</c> → 最後に付けた書き手）。<b>計数専用。</b></summary>
    readonly Dictionary<int, MarkOrigin> _markOrigin = new();

    /// <summary>§1: 被ダメージ増が乗った回数と、上乗せした量（出どころ別。添字は <see cref="MarkOrigin"/>）。</summary>
    public readonly long[] MarkVulnHits = new long[3];
    public readonly long[] MarkVulnAdded = new long[3];

    /// <summary>§1: ターン頭に敵の標が立っていた延べ体数 ／ 1体以上立っていたターン数 ／ 同時に立っていた最大数。</summary>
    public long MarkFoeUnitTurns, MarkFoeTurns, MarkFoeMax;

    /// <summary>§2: 矢面の半減が効いた回数と、防いだ量。</summary>
    public long BeckonGuardHits, BeckonGuardSaved;

    /// <summary>敵の標を付けた書き手を記録する（<b>計数のみ</b>）。</summary>
    public void NoteMarkOrigin(UnitState u, MarkOrigin origin) => _markOrigin[u.InstanceId] = origin;

    /// <summary>矢面が標を付けた（<b>計数のみ</b>）。付け替えなら <paramref name="switched"/>。</summary>
    public void NoteBeckon(UnitState self, UnitState pick, bool switched)
    {
        UnitTally t = TallyOf(self);
        t.BeckonFires++;
        if (switched) t.BeckonSwitches++;
        TallyOf(pick).BeckonPicked++;
    }

    // =================================================================================
    // 第185期（A群の転生 3〜5枚目）: 組み付き・見せしめ・踏みしめ・据えた足
    // =================================================================================

    /// <summary>組み付き・見せしめの保持者が盤上にいるか（手番の頭の2つのキーと標的の選好の短絡）。</summary>
    bool _restrainLive;
    /// <summary>踏みしめ（範囲の盾・層の軽減）の保持者。</summary>
    readonly List<UnitState> _shieldHolders = new();
    /// <summary>据えた足の保持者が盤上にいるか（入れ替えの空振りの短絡）。</summary>
    bool _plantedLive;

    /// <summary>萎縮させる駒（第189期・<see cref="TraitId.Daunt"/>）が盤上に来たか。<b>偽なら萎縮の判定を比較1つで抜ける。</b></summary>
    bool _dauntLive;
    bool _confuseHalf;   // 第243期（④）

    /// <summary>痺れ毒（第195期・<see cref="TraitId.Numb"/>）の保持者が盤上に来たか。<b>偽なら痺れ毒の判定を比較1つで抜ける。</b>
    /// 一度立てば戦闘の終わりまで立ったまま（保持者が倒れても印は残る）。</summary>
    bool _numbLive;

    /// <summary>反転（第190期・<see cref="TraitId.Inverse"/>）の保持者。</summary>
    readonly List<UnitState> _inverseHolders = new();
    /// <summary>反転の裏（第190期・<see cref="TraitId.InverseLeak"/>）の保持者。</summary>
    readonly List<UnitState> _inverseLeakHolders = new();
    /// <summary>澱み分け（第190期・<see cref="TraitId.Taint"/>）の保持者（<b>計数専用</b>・離れた後の刻みの帳簿の帰属先）。</summary>
    readonly List<UnitState> _taintHolders = new();
    /// <summary>紅蓮（第197期）の保持者（<b>計数専用</b>・奔流の刻みの帰属先）。</summary>
    readonly List<UnitState> _gurenHolders = new();

    UnitState? GurenHolderAgainst(UnitState u)
    {
        foreach (UnitState h in _gurenHolders) if (h.TeamId != u.TeamId) return h;
        return null;
    }

    /// <summary>
    /// 奔流の毒の刻み（第197期・<b>計数専用</b>）。刻みの額面のうち、奔流が積んだ層（<see cref="GurenTrait.HeldKey"/>）と、
    /// 奔流の毒しか持っていなかった敵にミオが足した層（<see cref="GurenTrait.MioKey"/>）の分を、1回目と印の2回目以降に分けて数える。
    /// </summary>
    void NoteGurenPoisonTick(UnitState u, int poison, bool second)
    {
        if (_gurenHolders.Count == 0) return;
        int held = Math.Min(u.RawCounter(GurenTrait.HeldKey), poison);
        int mio = Math.Min(u.RawCounter(GurenTrait.MioKey), poison - held);
        if (held <= 0 && mio <= 0) return;
        UnitState? h = GurenHolderAgainst(u);
        if (h is null) return;
        UnitTally t = TallyOf(h);
        if (second) { t.GurenPoisonTickMark += held; t.GurenMioTickMark += mio; }
        else { t.GurenPoisonTick += held; t.GurenMioTick += mio; }
    }

    /// <summary>奔流が点けた火の刻み（第197期・<b>計数専用</b>）。</summary>
    void NoteGurenBurnTick(UnitState u, bool second)
    {
        if (_gurenHolders.Count == 0) return;
        int k = u.RawCounter(GurenTrait.BurnKey);
        if (k <= 0) return;
        UnitState? h = GurenHolderAgainst(u);
        if (h is null) return;
        UnitTally t = TallyOf(h);
        if (second) t.GurenBurnTickMark += BurnRules.Damage;
        else if (k == 1) t.GurenBurnTickNew += BurnRules.Damage;
        else t.GurenBurnTickRelit += BurnRules.Damage;
    }

    /// <summary>
    /// ミオの +4（第197期・<b>計数専用</b>）。奔流の毒しか持っていなかった敵（毒の層 ≤ 奔流の層 ＋ 奔流の上のミオの層）への +4 を、
    /// 「奔流が無ければ乗らなかった +4」として数える。
    /// </summary>
    public void NoteGurenThicken(UnitState foe, int poisonBefore, int step)
    {
        if (_gurenHolders.Count == 0) return;
        int g = foe.RawCounter(GurenTrait.HeldKey), m = foe.RawCounter(GurenTrait.MioKey);
        if (g <= 0 || poisonBefore > g + m) return;
        foe.SetCounter(GurenTrait.MioKey, m + step);
        UnitState? h = GurenHolderAgainst(foe);
        if (h is not null) TallyOf(h).GurenMioLayers += step;
    }

    /// <summary>
    /// 施しのリリの出来事（第204期・<see cref="BattleEventKind.Kiss"/>・<b>表示専用</b>）。verbose のときだけ積む。
    /// </summary>
    public void EmitKiss(UnitState lili, string label, UnitState? target, int amount,
                         UnitState? from = null, UnitState? intended = null, int slot = 0, int? remaining = null)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Kiss, Turn = _turn, ActorId = lili.InstanceId, TargetId = target?.InstanceId,
            Amount = amount, Text = label, SpreadFromId = from?.InstanceId, IntendedId = intended?.InstanceId,
            Slot = slot, StatusRemaining = remaining, SourceTrait = TraitId.Kiss, HpAfter = target?.Hp ?? 0,
        });
    }

    // =================================================================================
    // 第207期 —— 継ぎ当てのツギ（板・燃えやすい板・瓦礫拾い）
    // =================================================================================

    /// <summary>瓦礫拾い（<see cref="TraitId.Scrap"/>）の保持者。<b>空なら破片の減りは比較1つで抜ける。</b></summary>
    readonly List<UnitState> _scrapHolders = new();
    /// <summary>第211期: 棘（<see cref="TraitId.Thorns"/>）の保持者が盤上にいるか。偽なら破片で受け切った一撃の口は比較1つで抜ける。</summary>
    bool _thornsLive;
    int _hitSerial;
    /// <summary>第212期（<b>計数のみ</b>）: いま処理している1回の `ApplyDamage` の枠の通し番号（入れ子は退避・復帰）。誰も読んで分岐しない。</summary>
    public int CurrentHitSerial { get; private set; }
    /// <summary>第212期: 破片で受けても身構えが働く駒（<see cref="TraitId.BraceArmored"/>）が盤上にいるか。</summary>
    bool _braceArmoredLive;
    /// <summary>第213期（<b>計数のみ</b>）: 身構え（<see cref="TraitId.Brace"/>）の保持者が盤上にいるか。偽なら身構えの計数は比較1つで抜ける。</summary>
    bool _braceLive;
    /// <summary>第213期（<b>計数のみ</b>）: そのターンの頭に板の印を持っていた身構えの保持者。誰も読んで分岐しない。</summary>
    readonly HashSet<UnitState> _bracePlankNow = new();
    /// <summary>第213期（<b>計数のみ</b>）: 「上限が先なら破片が受け切った」一撃の枠の通し番号（Q0-1）。0 は無し。誰も読んで分岐しない。</summary>
    public int BraceWouldMuteSerial { get; private set; }
    /// <summary>第213期（<b>計数のみ</b>）: いま `ArmorOnlyHit`（破片が受け切った一撃）の中か。誰も読んで分岐しない。</summary>
    public bool ArmorOnlyNow { get; private set; }
    /// <summary>第213期（<b>計数のみ</b>）: 身構えの保持者がそのターンの頭に板の印を持っていたか。</summary>
    public bool BracePlankTurn(UnitState u) => _bracePlankNow.Contains(u);

    /// <summary>
    /// 第213期（<b>計数のみ</b>）: ターンの頭に、身構えの保持者が板の印を持っているかを写す（表D）。<b>盤面は読むだけ。</b>
    /// </summary>
    public void NoteBraceCensus()
    {
        if (!_braceLive) return;
        _bracePlankNow.Clear();
        foreach (UnitState u in _units)
        {
            if (!u.IsAlive || !u.HasTrait(TraitId.Brace)) continue;
            UnitTally t = TallyOf(u);
            if (u.RawCounter(StatusKeys.Plank) != 0) { _bracePlankNow.Add(u); t.BraceTurnsPlank++; }
            else t.BraceTurnsBare++;
        }
    }

    /// <summary>
    /// 燃焼が付く2口（<see cref="Ignite"/> とリリの移し）から呼ぶ。<paramref name="target"/> に燃えやすい板の印
    /// （<see cref="StatusKeys.Plank"/> == <see cref="PlankTrait.Flammable"/>）があれば残りターンを倍にして返す。
    /// 印が無ければ <paramref name="turns"/> をそのまま返す（<b>乱数を引かない</b>）。
    /// </summary>
    public int PlankFlare(UnitState target, int turns, bool fromKiss)
    {
        if ((target.RawCounter(StatusKeys.Plank) & PlankTrait.Flammable) == 0 || turns <= 0) return turns;
        int doubled = turns * 2;
        UnitTally t = TallyOf(target);
        t.PlankFlares++;
        t.PlankFlareTurns += doubled - turns;
        Log($"    {target.Name} の板に火が回った（燃焼 {turns} → {doubled} ターン）", LogKind.FriendlyFire);
        if (_verbose)
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.Plank, Turn = _turn, TargetId = target.InstanceId, Amount = doubled,
                Text = PlankLabels.Flare, Slot = fromKiss ? 1 : 0, SourceTrait = TraitId.PlankTinder,
            });
        return doubled;
    }

    /// <summary>
    /// 破片が減った（<see cref="UnitState.SetCounter"/> の1点からだけ来る・Q0-5）。減る口は3つ——<see cref="ApplyDamage"/> の破片の段・
    /// 礫（ガレ）の砕き・鱗（ウロ）の支払い——と、リリの移しで敵の破片が 0 になる口。
    /// <list type="number">
    /// <item><b>板の印を消す</b>: 破片が 0 になった瞬間。印があった間に吸った量は <c>PlankSoaked</c>（計数）。</item>
    /// <item><b>瓦礫拾い</b>: 同じ陣営に生きている <see cref="TraitId.Scrap"/> の保持者が、減った量を拾う（<see cref="ScrapTrait"/>）。</item>
    /// </list>
    /// 会戦の境界は <c>Counters.Remove</c> なのでここを通らない（境界で消えた破片は拾わない）。
    /// </summary>
    internal void NoteArmorLost(UnitState u, int lost, int after)
    {
        if (u.RawCounter(StatusKeys.Plank) > 0)
        {
            TallyOf(u).PlankSoaked += lost;
            if (after <= 0) { TallyOf(u).PlankBreaks++; u.SetCounter(StatusKeys.Plank, 0); }   // 第213期（計数のみ）: 板が割れた回数
        }
        if (_scrapHolders.Count == 0) return;
        foreach (UnitState h in _scrapHolders)
            if (h.IsAlive && h.TeamId == u.TeamId) ScrapTrait.Pick(this, h, u, lost, fall: false);
    }

    /// <summary>第208期: 撃ち返す板（<see cref="TraitId.PlankRebound"/>）の保持者が盤上にいるか。偽なら破片の段は比較1つで抜ける。</summary>
    bool _reboundLive;
    UnitState? _reboundTsugi;
    int _reflectAmt;
    UnitState? _reflectFrom;
    /// <summary>第209期: 反射を控えた破片の段で、その一撃を受けた後に残った破片（<b>板の印がある間は出どころを問わない</b>）と、印の倍率（百分率）。</summary>
    int _reflectRest, _reflectRatio;
    int _attackSerial;

    /// <summary>第208期: 1回の攻撃の枠（計数・表示専用）。第209期に、その攻撃が当たったツギの陣営の駒と、そのうち板の印を持っていた数を足した。</summary>
    sealed class ReflectFrame
    {
        public required UnitState Actor;
        public int Serial, Count;
        public List<int>? Hit;
        public int Planked;
    }
    readonly Stack<ReflectFrame> _reflectFrames = new();

    /// <summary>
    /// 第208期: 板が敵の一撃で砕けた量だけ、その敵へ撃ち返す。<see cref="ApplyDamageCore"/> の出口から1回だけ来る。
    /// <b>攻撃ではない</b>（`PerformAttack` を通らない・庇い・受け流し・反撃・割り込みの対象にならない）が、ダメージは
    /// 通常の入口（敵の破片・軛・§1 の標）を通す。棘と同じく <see cref="Reaction"/> で包む（中では返さない）。
    /// <para>第209期: 返す量 ＝ <paramref name="lost"/>（失った破片）＋ floor(<paramref name="rest"/>（その一撃の後に残った破片）× 倍率)。
    /// 倍率は板の印（<see cref="PlankTrait.RatioOf"/>）から読む。倍率 0 なら第208期と1ビットも違わない。</para>
    /// </summary>
    void ReflectPlank(UnitState holder, UnitState foe, int lost, int rest, int ratioPct)
    {
        UnitTally ht = TallyOf(holder);
        int thick = rest * ratioPct / 100;
        int amount = lost + thick;
        if (!foe.IsAlive) { ht.ReflectWasted += amount; ht.ReflectWastedLost += lost; return; }
        ht.ReflectCount++;
        ht.ReflectNominal += amount;
        ht.ReflectLost += lost;
        ht.ReflectThick += thick;
        ht.ReflectRestSum += rest;
        if (rest == 0) ht.ReflectRestZero++;
        (ht.ReflectRestHist ??= new long[UnitTally.RestHistSize])[Math.Min(rest, UnitTally.RestHistSize - 1)]++;
        if (YokeBinding)
        {
            ht.ReflectYoke++;
            if (amount > Yoke.Cap) ht.ReflectYokeOver++;
            var hyp = ht.ReflectYokeHyp ??= new long[4];
            for (int i = 0; i < 4; i++)
                if (lost + rest * UnitTally.HypRatios[i] / 100 > Yoke.Cap) hyp[i]++;
        }
        if (holder.RawCounter(PlankTrait.MixedKey) > 0) ht.ReflectMixed++;
        int serial = 0;
        if (_reflectFrames.Count > 0 && _reflectFrames.Peek().Actor == foe)
        {
            var top = _reflectFrames.Peek();
            serial = top.Serial;
            top.Count++;
        }
        if (_verbose)
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.Plank, Turn = _turn, ActorId = holder.InstanceId, TargetId = foe.InstanceId,
                Amount = amount, StatusRemaining = thick, HpAfter = rest,
                Text = PlankLabels.Reflect, Slot = serial, SourceTrait = TraitId.PlankRebound,
            });
        int before = foe.Hp;
        Reaction(() =>
        {
            Log($"    {holder.Name} の板の破片が {foe.Name} へ飛んだ（{amount}" + (thick > 0 ? $"・うち板の厚さ {thick}" : "") + "）", LogKind.Trigger);
            ApplyDamage(foe, amount, holder);
        });
        ht.ReflectDealt += before - Math.Max(0, foe.Hp);
        if (_turn == 1) ht.FirstTurnReflect += before - Math.Max(0, foe.Hp);   // 第212期（計数のみ）
        if (_reboundTsugi is not null) TallyOf(_reboundTsugi).ReflectByPlank += before - Math.Max(0, foe.Hp);   // 第211期（戦績表の「反射」）
        if (!foe.IsAlive) ht.ReflectKills++;
        if (serial == 0) NoteReflectGroup(1, !foe.IsAlive);
    }

    void CloseReflectFrame()
    {
        var f = _reflectFrames.Pop();
        if (f.Count > 0) NoteReflectGroup(f.Count, !f.Actor.IsAlive);
        // 第209期（計数のみ）: 敵の1回の攻撃がツギの陣営の駒に2体以上当たったとき、当たった数と、そのうち板の印を持っていた数。
        if (f.Hit is { Count: >= 2 } && _reboundTsugi is not null)
        {
            UnitTally t = TallyOf(_reboundTsugi);
            t.MultiHitAttacks++;
            t.MultiHitTargets += f.Hit.Count;
            t.MultiHitPlanked += f.Planked;
            (t.MultiHitPlankedHist ??= new long[4])[Math.Min(f.Planked, 3)]++;
        }
    }

    /// <summary>第209期（計数のみ）: 攻撃の枠の中で、その攻撃の主がツギの陣営の駒に当てた（同じ駒は1回だけ数える）。</summary>
    void NoteFrameHit(UnitState target, UnitState? source, bool burnTick)
    {
        if (_reflectFrames.Count == 0 || source is null || burnTick || _reboundTsugi is null) return;
        var f = _reflectFrames.Peek();
        if (f.Actor != source || target.TeamId != _reboundTsugi.TeamId || source.TeamId == target.TeamId) return;
        f.Hit ??= new List<int>();
        if (f.Hit.Contains(target.InstanceId)) return;
        f.Hit.Add(target.InstanceId);
        if ((target.RawCounter(StatusKeys.Plank) & PlankTrait.Rebound) != 0) f.Planked++;
    }

    /// <summary>1回の敵の攻撃で返った本数の分布（1・2・3・4以上）と、その攻撃の主が倒れたか（計数のみ・ツギの帳簿に付ける）。</summary>
    void NoteReflectGroup(int count, bool killed)
    {
        if (_reboundTsugi is null) return;
        UnitTally t = TallyOf(_reboundTsugi);
        int i = Math.Min(count, 4) - 1;
        (t.ReflectGroupHist ??= new long[4])[i]++;
        if (killed) (t.ReflectGroupKilled ??= new long[4])[i]++;
    }

    /// <summary>第208期: 燃えやすい板（<see cref="TraitId.PlankScorch"/>・印の <see cref="PlankTrait.Scorch"/>）なら燃焼の刻みを倍にする。</summary>
    int ScorchTick(UnitState u, int dmg)
    {
        if ((u.RawCounter(StatusKeys.Plank) & PlankTrait.Scorch) == 0) return dmg;
        UnitTally t = TallyOf(u);
        t.PlankScorched++;
        t.PlankScorchExtra += dmg;
        return dmg * 2;
    }

    /// <summary>ツギの出来事（第207期・<see cref="BattleEventKind.Plank"/>・<b>表示専用</b>）。verbose のときだけ積む。</summary>
    public void EmitPlank(UnitState tsugi, string label, UnitState? target, int amount, int slot, int? remaining,
                          int? partBase = null, int? partSkill = null, int? aidOrdinal = null)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Plank, Turn = _turn, ActorId = tsugi.InstanceId, TargetId = target?.InstanceId,
            SpreadFromId = label == PlankLabels.Scrap ? target?.InstanceId : null,
            Amount = amount, Text = label, Slot = slot, StatusRemaining = remaining,
            SourceTrait = label == PlankLabels.Scrap ? TraitId.Scrap : label == PlankLabels.FirstAid ? TraitId.FirstAid : label == PlankLabels.Skill ? TraitId.PlankSkill : TraitId.Plank,
            HpAfter = target?.Hp ?? 0, PlankBase = partBase, PlankSkill = partSkill, AidOrdinal = aidOrdinal,
        });
    }

    /// <summary>
    /// 第210期: 腕の累計（<see cref="TraitId.PlankSkill"/>）。破片の段が反射の「失った破片」を控えるのと<b>同じ場所・同じ条件</b>で呼ぶ
    /// （Q0-4: 腕の累計 ＝ 反射の失った破片の累計・空振りの分も含む）。段が上がった瞬間は台本に出す（<see cref="PlankLabels.Skill"/>）。<b>乱数を引かない。</b>
    /// </summary>
    public void NotePlankSkill(int lost)
    {
        UnitState? h = _reboundTsugi;
        if (h is null || lost <= 0 || !(h.HasTrait(TraitId.PlankSkill) || h.HasTrait(TraitId.AidSkill))) return;   // 第212期: 腕の中身が応急処置の回数でも段は同じ累計で上がる
        int sum = h.RawCounter(PlankTrait.SkillLostKey) + lost;
        h.SetCounter(PlankTrait.SkillLostKey, sum);
        UnitTally t = TallyOf(h);
        t.PlankSkillLost += lost;
        int was = h.RawCounter(PlankTrait.SkillTierKey), now = PlankTrait.SkillTierOf(sum);
        if (now <= was) return;
        h.SetCounter(PlankTrait.SkillTierKey, now);
        for (int k = was + 1; k <= now; k++)
        {
            int i = Math.Min(k, 3);
            if (k > 3) continue;
            (t.PlankSkillReach ??= new long[4])[i]++;
            (t.PlankSkillReachTurn ??= new long[4])[i] += _turn;
        }
        Log($"    {h.Name} の腕が上がった（段 {now}・砕かれた累計 {sum}）", LogKind.Highlight, h);
        EmitPlank(h, PlankLabels.Skill, h, sum, now, null);
    }

    /// <summary>
    /// 第210期（<b>計数のみ</b>）: 応急処置の条件（<see cref="FirstAidTrait.Needs"/>）を満たす被弾が起きたターンを、ツギの帳簿に数える（1ターン1回）。
    /// 札の有無に依らず、瓦礫拾い（ツギ）が盤上にいれば数える——W0 でも「応急処置があれば何回出番があったか」が読める。<b>盤面に触らない。</b>
    /// </summary>
    void NoteFirstAidChance(UnitState target, int amount)
    {
        UnitState? h = null;
        foreach (UnitState x in _scrapHolders) if (x.IsAlive && x.TeamId == target.TeamId) { h = x; break; }
        if (h is null || !FirstAidTrait.NeedsFor(h, target)) return;
        TallyOf(target).FirstAidNeed++;
        if (_firstAidChanceTurn == _turn) return;
        _firstAidChanceTurn = _turn;
        UnitTally t = TallyOf(h);
        t.FirstAidChance++;
        if (HushBindingNow) t.FirstAidChanceHushed++;
        if ((target.Hp + amount) * 100 >= target.MaxHp * FirstAidTrait.Percent) t.FirstAidChanceCross++;
    }

    /// <summary>
    /// 第211期: 敵の一撃を破片が受け切った（HP は減らず、<c>OnDamaged</c> も <c>OnAllyDamaged</c> も鳴らない）。
    /// (1) 棘の保持者: 計数（<c>ThornArmorMuted</c>）と、<see cref="TraitId.ThornsArmored"/> を持てば棘を返す（門は棘と同じ）。
    /// (2) 応急処置: 同じ陣営のツギが <see cref="TraitId.FirstAidArmored"/> を持てば、受け切った後の実質の残り体力で条件を見る。
    /// <b>乱数を引かない。</b>
    /// </summary>
    void ArmorOnlyHit(UnitState target, UnitState source, int soaked)
    {
        if (_thornsLive && target.HasTrait(TraitId.Thorns))
        {
            UnitTally kt = TallyOf(target);
            kt.ThornArmorMuted++;
            if (target.HasTrait(TraitId.ThornsArmored) && target.IsAlive && !InReaction
                && CanActOutOfTurn(target, OutOfTurnRoute.Thorns))
            {
                kt.ThornArmorRiposte++;
                TraitMark m = this.BeginTrait(TraitId.Thorns, target);
                ThornsTrait.Riposte(this, target, source);
                this.EndTrait(m);
            }
        }
        // 第212期: 破片で受け切った一撃でも、身構えの弾き（錯乱）と配りを起こす（`BraceArmored`）。
        if (_braceArmoredLive && target.IsAlive && target.HasTrait(TraitId.BraceArmored) && target.HasTrait(TraitId.Brace))
        {
            TallyOf(target).BraceArmorStruck++;
            TraitMark bm = this.BeginTrait(TraitId.Brace, target);
            BraceTrait.Struck(this, target);
            this.EndTrait(bm);
        }
        // 第213期（`BraceHeldDeliver`・W2）: 受け切った一撃では弾かない。そのターン既に宛先がいれば保留を配るだけ（新しい宛先は作らない）。
        else if (_braceArmoredLive && target.IsAlive && target.HasTrait(TraitId.BraceHeldDeliver) && target.HasTrait(TraitId.Brace))
        {
            TraitMark bm = this.BeginTrait(TraitId.Brace, target);
            BraceTrait.Deliver(this, target);
            this.EndTrait(bm);
        }
        if (_scrapHolders.Count == 0) return;
        foreach (UnitState h in _scrapHolders.ToList())
        {
            if (!h.IsAlive || h.TeamId != target.TeamId) continue;
            TraitMark m = this.BeginTrait(TraitId.FirstAid, h);
            FirstAidTrait.TryArmoredHit(this, h, target, soaked);
            this.EndTrait(m);
        }
    }
    int _firstAidChanceTurn = -1;

    /// <summary>状態が移った瞬間（第204期・<see cref="BattleEventKind.StatusTransfer"/>・<b>表示専用</b>）。</summary>
    public void EmitStatusTransfer(UnitState lili, UnitState from, UnitState to, string key, int amount, int after)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.StatusTransfer, Turn = _turn, ActorId = lili.InstanceId, TargetId = to.InstanceId,
            SpreadFromId = from.InstanceId, Text = key, Amount = amount, StatusRemaining = after, SourceTrait = TraitId.KissSpill,
        });
    }

    /// <summary>
    /// 紅蓮を放った瞬間（第197期・<b>表示専用</b>）。<paramref name="target"/> が null の1件が見出し（<c>Amount</c> = 紅蓮の量・
    /// <c>Slot</c> = 敵の数）、続く敵ごとの1件が <c>TargetId</c> = 敵・<c>Amount</c> = 積んだ層（直撃の版は直撃の名目）。
    /// </summary>
    public void EmitGurenRelease(UnitState beni, UnitState? target, int amount, int foes)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.GurenRelease, Turn = _turn, ActorId = beni.InstanceId, TargetId = target?.InstanceId,
            Amount = amount, Slot = foes, SourceTrait = TraitId.Guren,
        });
    }

    /// <summary>剣の段に入った瞬間（第198期・<b>表示専用</b>）。<see cref="BattleEventKind.LastStand"/>。第199期に上乗せの量（<c>StatusRemaining</c>）を足した。</summary>
    public void EmitLastStand(UnitState self, TraitId variant, int bonus)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.LastStand, Turn = _turn, ActorId = self.InstanceId, TargetId = self.InstanceId,
            Amount = self.CurrentAttack, HpAfter = self.Hp, Slot = self.Slot, SourceTrait = variant,
            StatusRemaining = bonus,
        });
    }

    /// <summary>
    /// 第199期: 相打ちで最後の敵を倒した陣営（-1 ＝ 無し）。<b>立てる口は <see cref="MarkLastStandVictory"/> の1箇所</b>
    /// （剣の段の相打ちの斬り返しの直後）で、読むのは <c>BattleEngine.Run</c> の勝敗の1行だけ。
    /// </summary>
    public int LastStandVictoryTeam { get; private set; } = -1;

    /// <summary>
    /// 第199期: 相打ちの斬り返しの直後に呼ぶ。<b>相手陣営に生きている駒が1体もいなければ</b>印を立て、表示専用の
    /// <see cref="BattleEventKind.LastStandVictory"/> を出して真を返す（敵の死亡通知で何かが湧いていれば立たない）。乱数を引かない。
    /// </summary>
    public bool MarkLastStandVictory(UnitState self, UnitState killed)
    {
        foreach (UnitState u in _units) if (u.TeamId != self.TeamId && u.IsAlive) return false;
        if (LastStandVictoryTeam >= 0) return false;
        LastStandVictoryTeam = self.TeamId;
        Log($"  最期の一太刀が {killed.Name} を斬り伏せた——相打ちで勝つ", LogKind.Highlight);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.LastStandVictory, Turn = _turn, ActorId = self.InstanceId, TargetId = killed.InstanceId,
            SourceTrait = TraitId.LastStandHold, Team = self.TeamId,
        });
        return true;
    }

    /// <summary>斬り返し・相打ちの直前（第198期・<b>表示専用</b>）。<see cref="BattleEventKind.LastStandRiposte"/>。</summary>
    public void EmitLastStandRiposte(UnitState self, UnitState foe, int amount, bool dying)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.LastStandRiposte, Turn = _turn, ActorId = self.InstanceId, TargetId = foe.InstanceId,
            Amount = amount, HpAfter = Math.Max(0, self.Hp), Slot = dying ? 1 : 0, SourceTrait = TraitId.LastStand,
            Reaction = true,
        });
    }

    /// <summary>反転で癒えた味方の、このターンの <c>InstanceId</c>（<b>計数専用</b>・最大同時人数）。</summary>
    readonly List<int> _inverseTurnSet = new();
    int _inverseTurn = -1;

    /// <summary>
    /// <paramref name="u"/> に隣接する、同じ陣営の生きている保持者（第190期）。
    /// <b>第192期から、<see cref="InverseTrait.IncludesSelf"/> が真なら保持者自身も返す</b>（呼び出し口は反転と反転の裏の2本）。
    /// 並びは盤に来た順（乱数を引かない）。
    /// </summary>
    static UnitState? AdjacentHolder(List<UnitState> holders, UnitState u)
    {
        foreach (UnitState h in holders)
        {
            if (!h.IsAlive || h.TeamId != u.TeamId) continue;
            if (ReferenceEquals(h, u) ? InverseTrait.IncludesSelf : FormationRules.AreAdjacent(h, u)) return h;
        }
        return null;
    }

    /// <summary>
    /// 反転（第190期）。<paramref name="u"/> の毒・燃焼の削りを回復に変えるベニ（いなければ null）。
    /// <b>保持者がいなければ比較1つで抜ける。</b>
    /// </summary>
    public UnitState? InvertsTick(UnitState u) => _inverseHolders.Count == 0 ? null : AdjacentHolder(_inverseHolders, u);

    /// <summary>
    /// 啜り（第193期）。反転で隣の味方に入った回復のうち<b>満タンで溢れた分</b>を、その反転を起こしたベニに流す。
    ///
    /// <para><b>溢れ ＝ 回復の量 − 実際に増えた HP。</b> 渇き（<see cref="HealOutcome.Drought"/>）・支援拒否（<see cref="HealOutcome.Blocked"/>）で
    /// 止められた回復は溢れではない（回復が通らなかっただけ）。<b>対象がベニ自身なら流さない。</b>
    /// ベニへの流し込みは <c>inverted: true</c>（反転の裏でダメージに戻らない）で、ベニが満タンならその分は捨てる（連鎖させない）。</para>
    ///
    /// <para><see cref="InverseTrait.OverflowToHolder"/> が偽なら<b>計数だけ</b>取って流さない（第192期の盤面）。乱数は引かない。</para>
    /// </summary>
    void InverseSip(UnitState beni, UnitState u, int amount, int gained, HealOutcome res)
    {
        if (ReferenceEquals(u, beni) || !beni.IsAlive) return;
        if (res != HealOutcome.Healed && res != HealOutcome.Full) return;
        int over = amount - gained;
        if (over <= 0) return;
        UnitTally t = TallyOf(beni);
        t.SipEligible += over;
        t.SipRoom += Math.Min(over, Math.Max(0, beni.MaxHp - beni.Hp));
        if (!InverseTrait.OverflowToHolder) return;

        // 表示専用。直後の `Heal`（ベニ → ベニ）が「隣の溢れを啜った」ものだと再生側が分けられるように。
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.InverseSip, Turn = _turn, ActorId = beni.InstanceId,
            TargetId = u.InstanceId, Amount = over, SourceTrait = TraitId.Inverse,
        });
        int b0 = beni.Hp;
        HealOutcome br = Heal(beni, over, beni, inverted: true);
        int g = beni.Hp - b0;
        t.SipGained += g;
        if (br == HealOutcome.Healed || br == HealOutcome.Full)   // 第197期・**計数のみ**（満タンで入りきらなかった分）
        {
            int waste = over - g;
            if (waste > 0)
            {
                t.SipWaste += waste;
                (t.SipWasteByTurn ??= new long[31])[Math.Clamp(_turn, 0, 30)] += waste;
            }
            // 紅蓮（第197期）。**紅蓮の札を持つベニだけ**が溢れの余りを溜める（持たなければ1ビットも動かない）。
            if (waste > 0 && GurenTrait.Holds(beni))
            {
                int total = beni.RawCounter(StatusKeys.Guren) + waste;
                beni.SetCounter(StatusKeys.Guren, total);
                t.GurenGained += waste;
                if (_verbose)   // 表示専用（アイコン用の溜まった量）
                    Emit(new BattleEvent
                    {
                        Kind = BattleEventKind.GurenGain, Turn = _turn, ActorId = beni.InstanceId, TargetId = u.InstanceId,
                        Amount = waste, StatusRemaining = total, SourceTrait = TraitId.Guren,
                    });
            }
        }
        if (_turn <= 3) t.SipEarly += g;
        if (g > 0) Log($"    {beni.Name} が {u.Name} の溢れを啜った（+{g}）", LogKind.Status);
    }

    /// <summary>反転の回復を1段行う（<c>kind</c>: 0 刻みの毒 ／ 1 刻みの燃焼 ／ 2 起爆）。渇き・支援拒否は <see cref="Heal"/> がそのまま掛ける。</summary>
    void InverseHeal(UnitState beni, UnitState u, int amount, int kind, string what)
    {
        // 第298期 段0-2（群1）: 反転の回復（と中の啜り）はベニの出力。印をベニに立てる（観測専用）。
        int h0 = HealOutOf(beni);
        TraitMark am = BeginTrait(TraitId.Inverse, beni);
        try { InverseHealCore(beni, u, amount, kind, what); }
        finally { AttrEnd(am, 1, beni, HealOutOf(beni) - h0); }
    }

    void InverseHealCore(UnitState beni, UnitState u, int amount, int kind, string what)
    {
        // 第219期: 燃焼の脆さ（F3・F4）。**伸びた量をそのまま回復に反転する**。燃焼の刻み（kind 1）は刻みそのものなので燃えていると数える。
        if (Ember.Brittle > 0 && amount > 0 && BrittleApplies(u) && (kind == 1 || u.RawCounter(StatusKeys.Burn) > 0))
        {
            int extra = (amount * BrittlePct(u, out _) + 99) / 100;
            amount += extra;
            BrittleBook.InverseBase += amount - extra;
            BrittleBook.InverseExtra += extra;
            BrittleBook.InverseHits++;
        }
        Log($"    {u.Name} の{what}は {beni.Name} の隣で薬になる（+{amount}）", LogKind.Status);
        int before = u.Hp;
        HealOutcome res = Heal(u, amount, beni, inverted: true);
        int gained = u.Hp - before;
        UnitTally t = TallyOf(beni);
        t.InverseNominal += amount;
        InverseSip(beni, u, amount, gained, res);   // 第193期
        if (ReferenceEquals(u, beni))   // 第192期・**計数のみ**（ベニ自身が受けた分）
        {
            if (kind == 0) t.InverseSelfPoison += gained; else if (kind == 1) t.InverseSelfBurn += gained; else t.InverseSelfDetonate += gained;
        }
        if (kind == 0) t.InversePoisonHealed += gained;
        else if (kind == 1) { t.InverseBurnHealed += gained; t.InverseBurnNominal += amount; }
        else if (kind == 3) t.InverseDischargeHealed += gained;   // 第214期: 放電
        else if (kind == 4) { }                                   // 第220期: 澱みの爆発（帳簿は BurstLedger）
        else if (kind == 5) { }                                   // 第235期: 燃える巻き込み（帳簿はボルグの側）
        else t.InverseDetonateHealed += gained;
        if (_turn <= 3 && kind != 5)   // 第191期・**計数のみ**（1〜3 ターン目の分）
        {
            if (kind == 0) t.InversePoisonEarly += gained;
            else if (kind == 1) { t.InverseBurnEarly += gained; t.InverseBurnNominalEarly += amount; }
            else t.InverseDetonateEarly += gained;
        }
        if (gained <= 0) return;
        if (_inverseTurn != _turn) { _inverseTurn = _turn; _inverseTurnSet.Clear(); }
        if (!_inverseTurnSet.Contains(u.InstanceId))
        {
            _inverseTurnSet.Add(u.InstanceId);
            t.InverseRecipientTurns++;
            if (_inverseTurnSet.Count > t.InverseMaxSimul) t.InverseMaxSimul = _inverseTurnSet.Count;
        }
    }

    /// <summary>
    /// 澱み分けの層を持ったまま、反転の外で毒に刻まれた量（第190期・<b>計数専用</b>）。
    /// 入れ替え・ベニの死亡の後に「溜めた毒が本物のダメージに戻る」分。
    /// </summary>
    void NoteTaintPostBite(UnitState u, int poison)
    {
        if (_taintHolders.Count == 0) return;
        int held = Math.Min(u.RawCounter(TaintTrait.HeldKey), u.RawCounter(StatusKeys.Poison));
        if (held <= 0) return;
        UnitState? holder = _taintHolders.FirstOrDefault(h => h.TeamId == u.TeamId);
        if (holder is not null) TallyOf(holder).TaintPostBite += poison;
    }

    /// <summary>
    /// ベニが点けた火を持ったまま、反転の外で燃焼に刻まれた量（第191期・<b>計数専用</b>）。
    /// </summary>
    void NoteKindlePostBurn(UnitState u)
    {
        if (_inverseHolders.Count == 0 || u.RawCounter(KindleTrait.HeldKey) <= 0) return;
        UnitState? holder = _inverseHolders.FirstOrDefault(h => h.TeamId == u.TeamId);
        if (holder is not null) TallyOf(holder).KindlePostBurn += BurnRules.Damage;
    }

    /// <summary>燃え尽きたら「ベニが点けた火」の印を消す（第191期・<b>計数専用</b>）。</summary>
    static void ClearKindleHeld(UnitState u)
    {
        if (u.RawCounter(StatusKeys.Burn) <= 0 && u.RawCounter(KindleTrait.HeldKey) > 0) u.SetCounter(KindleTrait.HeldKey, 0);
    }

    /// <summary>反転の裏の1段（第190期）。出どころは回復させた駒（味方由来のダメージ）。</summary>
    void InverseLeakHit(UnitState leak, UnitState target, int amount, UnitState? by)
    {
        Log($"    {target.Name} への回復は {leak.Name} の隣で毒に変わった（{amount}）", LogKind.FriendlyFire);
        // 第191期・**表示専用**。直後の `Damage` が「回復役が殴った」ではなく「反転の裏」だと再生側が分けられるように。
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.HealInverted, Turn = _turn, ActorId = by?.InstanceId,
            TargetId = target.InstanceId, Amount = amount, Text = leak.Name, SourceTrait = TraitId.InverseLeak,
        });
        int before = target.Hp;
        ApplyDamage(target, amount, by, isFriendlyFire: by is not null && by.TeamId == target.TeamId);
        int dealt = before - Math.Max(0, target.Hp);
        UnitTally t = TallyOf(leak);
        t.InverseLeakFires++; t.InverseLeakNominal += amount; t.InverseLeakDealt += dealt;
        if (!target.IsAlive) t.InverseLeakKills++;
        if (by is not null) TallyOf(by).InverseLeakBy += dealt;
        if (ReferenceEquals(target, leak))   // 第192期・**計数のみ**（ベニ自身が受けた分・出どころの側にも）
        {
            t.InverseLeakSelf += dealt;
            if (by is not null) TallyOf(by).InverseLeakOnHolder += dealt;
        }
    }

    /// <summary>
    /// ハネの「勢い余って」（第189期・<see cref="OverrunTrait"/>）で入れ替えている最中の保持者（<b>計数専用</b>）。
    /// <see cref="SwapSlots"/> の通知が移動の読み手に届いた回数をこの駒の帳簿に数えるためだけにある。どの規則も読まない。
    /// </summary>
    public UnitState? OverrunBy { get; set; }

    /// <summary>1ターンに手番を失った敵の数の分布（0/1/2/3+。<b>計数のみ</b>・毎ターン末に1回）。</summary>
    public readonly long[] FoeStalledHist = new long[4];

    // 第202期・**計数専用**（どの規則も読まない）。規則で選ぶ陣形の貫き（`PickPierceLane`）の帳簿。
    // [撃つ敵の格子のレーン 0..2 × 選んだ経路 0..1]・同数の交互・反対側へ回った・選ぶ瞬間の経路ごとの生存数の検算用。
    public readonly long[] PierceChose = new long[6];
    public long PierceTies, PierceFallbacks;
    /// <summary>第221期・<b>計数専用</b>。貫きの経路に生きている駒が1体もいなくて単体1発に落ちた回数（○前2・○後2 だけが残った局面）。</summary>
    public long PierceDeadEnds;

    private void NotePierceChoice(UnitState attacker, int gl, int pick, bool tie, bool fallback, int[] occ)
    {
        if (pick < 2) PierceChose[(gl - 1) * 2 + pick]++;
        if (tie) PierceTies++;
        if (fallback) PierceFallbacks++;
    }

    /// <summary>ターン末に呼ぶ。このターンに <c>IdleTurn</c> が立った敵を数える（倒れた敵も数える）。</summary>
    internal void NoteFoeStalled()
    {
        int n = 0;
        foreach (UnitState u in _units)
            if (u.TeamId != PlayerTeam && u.RawCounter(StatusKeys.IdleTurn) == Turn) n++;
        FoeStalledHist[Math.Min(3, n)]++;
        // 据えの層の時間平均（踏みしめの保持者が生きていたターンだけ）。
        foreach (UnitState h in _shieldHolders)
            if (h.IsAlive)
            {
                UnitTally t = TallyOf(h);
                t.FootingLayerSum += h.RawCounter(StatusKeys.Footing);
                t.FootingLayerTurns++;
            }
    }

    /// <summary>竦みを消費する（手番を失ったとき）。ハメ防止の印を立てる。</summary>
    /// <summary>殴られて積もる層の控え（攻撃の枠ごと・第185期 追補4）。枠の外の攻撃（反撃など）はその場で積む。</summary>
    readonly Stack<List<UnitState>> _footingFrames = new();

    void QueueFootingHit(UnitState u)
    {
        if (_footingFrames.Count == 0) { StepFootingOnHit(u); return; }
        List<UnitState> top = _footingFrames.Peek();
        if (!top.Contains(u)) top.Add(u);   // 1回の攻撃につき1層
    }

    void StepFootingOnHit(UnitState u)
    {
        if (!u.IsAlive) return;
        int now = u.RawCounter(StatusKeys.Footing);
        if (now >= FootingTrait.MaxLayers) return;
        u.SetCounter(StatusKeys.Footing, now + 1);
        TallyOf(u).FootingHitSteps++;
        if (now + 1 >= FootingTrait.MaxLayers) NoteFootingFull(u);
        EmitStatusGain(u, StatusKeys.Footing, 1, u);   // 表示専用（層が増えた瞬間）
        Log($"    {u.Name} は殴られて踏みとどまった（据え {now + 1} 層）", LogKind.Trigger);
    }

    /// <summary>層が最大に届いた最初のターン（戦闘ごと・<b>計数のみ</b>）。</summary>
    public void NoteFootingFull(UnitState u)
    {
        UnitTally t = TallyOf(u);
        if (t.FootingFullAt == 0) t.FootingFullAt = Turn;
    }

    /// <param name="phase"><see cref="CowedLabels"/>——竦み自身で手番を失ったか、別の理由で失う手番に吸われたか（表示専用）。</param>
    void ConsumeCowed(UnitState u, string phase = CowedLabels.Absorbed)
    {
        if (u.RawCounter(StatusKeys.Cowed) <= 0) return;
        u.SetCounter(StatusKeys.Cowed, 0);
        u.SetCounter(ShameTrait.GuardKey, 1);
        TallyOf(u).CowedLost++;
        EmitCowed(u, phase, u.RawCounter(ShameTrait.ByKey) - 1, u.RawCounter(ShameTrait.FromKey) - 1);   // 第185期 追補3・表示専用
        u.SetCounter(ShameTrait.ByKey, 0);
        u.SetCounter(ShameTrait.FromKey, 0);
    }

    /// <summary>竦みを消費した瞬間を台本に打つ（第185期 追補3・<b>表示専用</b>。<c>verbose</c> のときだけ）。</summary>
    /// <summary>第237期（表示専用）: 着地の反動の見出し。<see cref="LandingTrait.Run"/> が入れ替えの直前に呼ぶ。</summary>
    internal void EmitLanding(UnitState hane, UnitState ally, UnitState with, int ordinal)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Landing, Turn = _turn, ActorId = hane.InstanceId, TargetId = ally.InstanceId,
            PartnerId = with.InstanceId, Slot = ordinal, Team = hane.TeamId,
        });
    }

    internal void EmitCowed(UnitState target, string phase, int byId, int fromId)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Cowed,
            Turn = _turn,
            ActorId = byId >= 0 ? byId : null,          // 竦ませたシガ
            SpreadFromId = fromId >= 0 ? fromId : null, // 悲鳴の出どころ
            TargetId = target.InstanceId,
            HpAfter = target.Hp,
            Text = phase,
        });
    }

    /// <summary>組み付かれて手番を失った。止めた側（組み付いている駒）にも数える。</summary>
    void NoteGrappleStall(UnitState u)
    {
        TallyOf(u).StallGrappled++;
        foreach (UnitState h in _units)
            if (h.IsAlive && h.TeamId != u.TeamId && h.RawCounter(GrappleTrait.TargetKey) == u.InstanceId + 1)
                TallyOf(h).GrappleStalled++;
    }

    public void NoteGrapple(UnitState self, UnitState target)
    {
        TallyOf(self).GrappleFires++;
        TallyOf(target).GrappledTimes++;
    }
    public void NoteGrappleHold(UnitState self) => TallyOf(self).GrappleHolds++;
    public void NoteGrappleBreak(UnitState self) => TallyOf(self).GrappleBreaks++;

    public void NoteShame(UnitState self, int cowed, int blocked)
    {
        UnitTally t = TallyOf(self);
        t.ShameFires++;
        t.ShameCowed += cowed;
        t.ShameBlocked += blocked;
    }

    public void NoteFooting(UnitState self, bool added)
    {
        UnitTally t = TallyOf(self);
        if (added) t.FootingSteps++; else t.FootingFull++;
    }

    /// <summary>
    /// 範囲の盾の持ち主のうち、この一撃（薙ぎ・全体）に当たるもの。<b>乱数を引かない。</b>
    /// 「当たる」＝主目標そのものか、振る前の盤面での巻き込みの顔ぶれに入っていること。
    /// </summary>
    UnitState? ShieldHit(UnitState actor, UnitState target, AttackPattern? patternOverride)
    {
        IReadOnlyList<UnitState>? extras = null;
        foreach (UnitState h in _shieldHolders)
        {
            if (!h.IsAlive || h.TeamId != target.TeamId) continue;
            if (HoleSkip(h)) continue;   // 第229期: 転倒の穴（倒れた盾は受け止めない）
            if (h == target) return h;
            extras ??= SecondaryTargets(actor, target, patternOverride);
            if (extras.Contains(h)) return h;
        }
        return null;
    }

    /// <summary>
    /// 範囲の盾: 同じ一撃が盾の持ち主と<b>その隣の味方</b>に当たるとき、隣の味方の分を盾が受ける。
    /// 盾が倒れていれば（この一撃の途中で倒れた場合も）本人が受ける。
    ///
    /// <para><b>第185期 追補: 受け止めた分は半分にしてから盾が受ける</b>（<see cref="FootingTrait.ShieldPercent"/>・
    /// 1 点を下回らない）。盾自身の層の軽減はその後（盾への <c>ApplyDamage</c> の中）で乗る。
    /// 盾自身に当たった分は今までどおり（ここを通っても <c>struck == shield</c> で素通りする）。</para>
    /// </summary>
    UnitState ShieldRecv(UnitState shield, UnitState struck, ref int amount)
    {
        if (struck == shield || !shield.IsAlive || !struck.IsAlive) return struck;
        if (struck.TeamId != shield.TeamId || !FormationRules.AreAdjacent(shield, struck)) return struck;
        int raw = amount;
        amount = Math.Max(1, raw * FootingTrait.ShieldPercent / 100);
        UnitTally t = TallyOf(shield);
        t.ShieldTakes++;
        t.ShieldTaken += amount;
        t.ShieldHalved += raw - amount;
        TallyOf(struck).ShieldCovered += raw;
        Log($"    {shield.Name} が {struck.Name} に及ぶ刃を代わりに受け止めた（{raw} → {amount}）", LogKind.Trigger);
        EmitShieldIntercept(shield, struck, amount);   // 第185期 追補2・表示専用
        return shield;
    }

    /// <summary>矢面が指差す相手がいなかった（<b>計数のみ</b>）。</summary>
    public void NoteBeckonIdle(UnitState self) => TallyOf(self).BeckonIdle++;

    /// <summary>
    /// 逃げ回る（第184期）。<b>入れ替えは <see cref="SwapSlots"/> そのもの</b>——ここで足すのは、
    /// その入れ替えの最中に<b>移動の読み手が何をしたか</b>の計数だけ（振った回数・受けた強化・動いた敵）。
    /// <b>盤面の分岐は1つも足していない。</b>
    /// </summary>
    public void FleeSwap(UnitState self, UnitState? partner)
    {
        if (partner is null) { TallyOf(self).FleeStuck++; return; }
        long atk0 = 0, whet0 = 0;
        foreach (UnitState u in _units) { atk0 += AttacksOf(u); if (u.TeamId == self.TeamId) whet0 += u.WhetReceived; }
        var foeSlots = _units.Where(u => u.TeamId != self.TeamId).Select(u => (u, u.Slot)).ToList();

        SwapSlots(self, partner.Slot, self);

        long atk1 = 0, whet1 = 0;
        foreach (UnitState u in _units) { atk1 += AttacksOf(u); if (u.TeamId == self.TeamId) whet1 += u.WhetReceived; }
        UnitTally t = TallyOf(self);
        t.FleeSwaps++;
        t.FleeReaderSwings += atk1 - atk0;
        t.FleeReaderWhet += whet1 - whet0;
        t.FleeFoeMoves += foeSlots.Count(x => x.u.Slot != x.Slot);
        TallyOf(partner).FleePushed++;
    }

    /// <summary>
    /// 隊を組み替える（第222期・シオ）。<b>入れ替えは <see cref="SwapSlots"/> そのもの</b>——ここで足すのは
    /// 表示専用の出来事（<see cref="BattleEventKind.Regroup"/>・誰を下げて誰を出したか）と計数だけ。<b>盤面の分岐は1つも足していない。</b>
    /// </summary>
    public void RegroupSwap(UnitState self, UnitState low, UnitState with)
    {
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Regroup,
            Turn = _turn,
            ActorId = self.InstanceId,
            TargetId = low.InstanceId,      // 下げた駒（最も傷ついた味方）
            PartnerId = with.InstanceId,    // 前へ出した駒
            HpAfter = low.Hp,
            Amount = low.MaxHp,
        });
        int hp0 = low.Hp + with.Hp;
        long whet0 = low.WhetReceived + with.WhetReceived;
        long atk0 = 0;
        foreach (UnitState u in _units) atk0 += AttacksOf(u);

        SwapSlots(low, with.Slot, self);

        long atk1 = 0;
        foreach (UnitState u in _units) atk1 += AttacksOf(u);
        UnitTally t = TallyOf(self);
        t.RegroupSwaps++;
        if (low == self) t.RegroupSelf++;
        t.RegroupHeal += Math.Max(0, low.Hp + with.Hp - hp0);
        t.RegroupWhet += low.WhetReceived + with.WhetReceived - whet0;
        t.RegroupReaderSwings += atk1 - atk0;
        TallyOf(low).RegroupLowered++;
        TallyOf(with).RegroupPushed++;
    }

    /// <summary>
    /// 移り木の回復（第224期・<b>計数のみ</b>）。<paramref name="gained"/> は実際に増えた HP（負なら 0）。
    /// 出どころは <see cref="CurrentMover"/>（シオの手番の入れ替えならシオ自身）。第225期: 緊急退避の入れ替えは添字 7。
    /// </summary>
    public void NoteDrifterHeal(UnitState shio, int nominal, int gained)
    {
        UnitTally t = TallyOf(shio);
        UnitState? by = CurrentMover;
        int src = by is null ? 6
                : by == shio ? (_inRetreatSwap ? 7 : 0)
                : by.TeamId != shio.TeamId ? 5
                : by.Def.Id == "basa" ? 1 : by.Def.Id == "sero" ? 2 : by.Def.Id == "hane" ? 3 : 4;
        int g = Math.Max(0, gained);
        t.DrifterFires++; t.DrifterNominal += nominal; t.DrifterGained += g;
        (t.DrifterBySrc ??= new long[8])[src] += g;
        (t.DrifterNomBySrc ??= new long[8])[src] += nominal;
    }

    /// <summary>第225期・計数専用: 緊急退避の入れ替えの最中か（移り木の出どころの帳簿だけが読む）。</summary>
    bool _inRetreatSwap;

    /// <summary>
    /// 緊急退避の入れ替え（第225期・<see cref="RetreatTrait"/> だけが呼ぶ）。表示専用の出来事 ＋ <see cref="SwapSlots"/> ＋ 計数。
    /// <b>盤面を変えるのは <c>SwapSlots</c> だけ</b>（組み替えの <see cref="RegroupSwap"/> と同じ形）。
    /// </summary>
    public void RetreatSwap(UnitState shio, UnitState low, UnitState with, int stage, int ordinal)
    {
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Retreat,
            Turn = _turn,
            ActorId = shio.InstanceId,
            TargetId = low.InstanceId,
            PartnerId = with.InstanceId,
            Slot = stage,
            StatusRemaining = ordinal,
            HpAfter = low.Hp,
            Amount = low.MaxHp,
        });
        UnitTally t = TallyOf(shio);
        t.RetreatSwaps++;
        if (low == shio) t.RetreatSelf++;
        if (InReaction) t.RetreatInReaction++;
        TallyOf(low).RetreatLowered++;
        TallyOf(with).RetreatPushed++;
        bool prev = _inRetreatSwap;
        _inRetreatSwap = true;
        try { SwapSlots(low, with.Slot, shio); }
        finally { _inRetreatSwap = prev; }
    }

    /// <summary>
    /// 挑発（第226期）。主目標の段で挑発が選んだ駒と、鎖を通った後の相手。<b>計数と表示のためだけ</b>（盤面は動かさない）。
    /// 最後まで挑発の主が的なら <c>DecoyDrew</c> と表示専用の <see cref="BattleEventKind.Decoy"/>、介入に引き剥がされたら <c>DecoyStolen</c>。
    /// </summary>
    void NoteDecoy(UnitState decoy, UnitState attacker, UnitState? chosen)
    {
        UnitTally t = TallyOf(decoy);
        if (chosen != decoy) { t.DecoyStolen++; return; }
        t.DecoyDrew++;
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Decoy, Turn = _turn, ActorId = decoy.InstanceId, TargetId = attacker.InstanceId,
            Slot = EvadeTrait.StageOf(decoy), Amount = EvadeTrait.PercentOf(decoy),
        });
    }

    /// <summary>
    /// 敵の乱れ（第226期）。<c>SwapSlots</c> の通知が1体ぶん呼ぶ。陣営の累計を1つ進め、相手陣営の保持者の段が上がったら表示と計数、
    /// 行が前に変わった駒には、相手陣営の生きているバサ（`Shuffler` ＋ `Disarray`・席番号の若い方）が混乱を立てる。<b>乱数を引かない。</b>
    /// </summary>
    void NoteDisorder(UnitState u, Row from, UnitState? by)
    {
        int team = u.TeamId;
        int before = DisarrayTrait.StageOfCount(_disorder[team]);
        _disorder[team]++;
        int after = DisarrayTrait.StageOfCount(_disorder[team]);
        if (after > before)
            foreach (UnitState h in LivingMembers(Opponent(team)))
            {
                if (!h.HasTrait(TraitId.Disarray)) continue;
                var st = TallyOf(h).DisarrayStageTurn ??= new int[4];
                if (st[after] == 0) st[after] = Math.Max(1, _turn);
                Log($"    {h.Name} の周りで敵の乱れが深まる（段 {after}）", LogKind.Trigger);
                if (_verbose) Emit(new BattleEvent
                {
                    Kind = BattleEventKind.DisarrayStage, Turn = _turn, ActorId = h.InstanceId, TargetId = h.InstanceId,
                    Slot = after, Amount = _disorder[team],
                });
            }
        if (!u.IsAlive || FormationRules.DepthOf(u.Row) >= FormationRules.DepthOf(from)) return;
        UnitState? basa = LivingMembers(Opponent(team)).FirstOrDefault(h => h.HasTrait(TraitId.Shuffler) && h.HasTrait(TraitId.Disarray));
        if (basa is not null) ShufflerTrait.DisarrayConfuse(this, basa, u, by);
    }

    /// <summary>敵の乱れの混乱（第226期・<b>表示専用</b>）。<c>ActorId</c> ＝ 動かした駒（前へ出した張本人）／ <c>TargetId</c> ＝ 混乱した駒 ／ <c>PartnerId</c> ＝ バサ。</summary>
    internal void NoteDisarrayConfuse(UnitState basa, UnitState u, UnitState? by)
    {
        if (_haneAct != 0 && by is not null)   // 第228期・計数のみ
        {
            if (_haneAct == 1) TallyOf(by).BlastConfused++; else TallyOf(by).SpringConfused++;
        }
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Disarray, Turn = _turn, ActorId = by?.InstanceId, TargetId = u.InstanceId, PartnerId = basa.InstanceId,
            Slot = u.Slot, Amount = DisarrayTrait.StageOf(this, basa),
        });
    }

    /// <summary>動かされて吹く突風（第226期・<b>表示専用</b>）。<c>ActorId</c> ＝ <c>TargetId</c> ＝ バサ ／ <c>StatusRemaining</c> ＝ そのターンの何回目か。直後にバサの <c>Attack</c>。</summary>
    internal void NoteSquall(UnitState basa, int nth)
    {
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Squall, Turn = _turn, ActorId = basa.InstanceId, TargetId = basa.InstanceId, StatusRemaining = nth,
        });
    }

    /// <summary>隊の乱れの段が上がった（第225期・表示専用の出来事 ＋ 計数）。</summary>
    public void NoteShioStage(UnitState shio, int stage, int moves)
    {
        var st = TallyOf(shio).ShioStageTurn ??= new int[4];
        if (st[stage] == 0) st[stage] = Math.Max(1, _turn);
        Log($"    {shio.Name} の拾う手が速くなる（隊の乱れ 段 {stage}）", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.ShioStage, Turn = _turn, ActorId = shio.InstanceId, TargetId = shio.InstanceId,
            Slot = stage, Amount = moves,
        });
    }

    /// <summary>
    /// シオの回復の溢れ（第230期）。移り木・手当ての回復が相手の減っている HP を超えた分（<paramref name="outcome"/> が増やした／満タンのときだけ）。
    /// 受け手の側に数える。
    /// </summary>
    public void ShioOverflow(UnitState shio, UnitState target, int nominal, int gained, HealOutcome outcome)
    {
        if (outcome is not (HealOutcome.Healed or HealOutcome.Full)) return;
        int over = nominal - Math.Max(0, gained);
        if (over <= 0) return;
        UnitTally rt = TallyOf(target);
        rt.ShioOverflowEvents++; rt.ShioOverflowRecv += over; rt.ShioOverflowHalf += over / 2;
        // 第230期（`DriftSurge`・W3/W4）: 溢れの半分（切り捨て）を受け手の攻撃力に。1体1戦 +15 まで。**札が無ければここで抜ける。**
        if (!shio.HasTrait(TraitId.DriftSurge) || !target.IsAlive) return;
        int had = _surgeGiven.GetValueOrDefault(target.InstanceId);
        int gain = Math.Min(over / 2, DriftSurgeTrait.CapPerUnit - had);
        if (over / 2 > Math.Max(0, gain)) rt.ShioOverflowCapped += over / 2 - Math.Max(0, gain);
        if (gain <= 0) return;
        _surgeGiven[target.InstanceId] = had + gain;
        Whet(target, gain, WhetRoute.Drifter);
        rt.ShioOverflowGain += gain;
        Log($"    {shio.Name} の溢れた手当てが {target.Name} の腕に宿った（攻撃 +{gain}）", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Overflow, Turn = _turn, ActorId = shio.InstanceId, TargetId = target.InstanceId,
            Amount = gain, StatusRemaining = had + gain, Team = shio.TeamId,
        });
    }

    /// <summary>第230期: 溢れを攻撃力にした累計（1戦・受け手の InstanceId ごと）。</summary>
    readonly Dictionary<int, int> _surgeGiven = new();

    // ---- 第230期: 撃破の衝撃（`KillImpactTrait`） ----
    readonly Stack<(UnitState Actor, List<(UnitState Dead, int Slot)> Kills)> _impactFrames = new();

    /// <summary>撃破の衝撃の保持者が敵を倒した（<see cref="KillImpactTrait.OnKill"/> だけが呼ぶ）。その保持者の攻撃の枠の中なら控える。</summary>
    public void NoteImpactKill(UnitState self, UnitState victim)
    {
        if (victim.TeamId == self.TeamId) return;
        if (_impactFrames.Count == 0 || _impactFrames.Peek().Actor != self) { TallyOf(self).ImpactOutside++; return; }
        _impactFrames.Peek().Kills.Add((victim, victim.Slot));
        TallyOf(self).ImpactKills++;
    }

    /// <summary>
    /// 撃破の衝撃の解決（攻撃が終わってから・倒した順）。後ろに敵がいれば吹き飛ばし、いなければ勢い余って隣の味方と入れ替わる（1ターン2回）。
    /// </summary>
    void ResolveImpact(UnitState yomi, List<(UnitState Dead, int Slot)> kills)
    {
        if (kills.Count == 0) return;
        UnitTally t = TallyOf(yomi);
        int tb = Math.Clamp(_turn, 0, 6);
        foreach (var (dead, slot) in kills)
        {
            if (!yomi.IsAlive) { t.ImpactDead++; continue; }
            FormationShape shape = dead.Shape;
            UnitState? behind = null; int dest = -1;
            foreach (int lane in shape.LanesOf(slot))
            {
                var path = shape.LanePath(lane);
                int idx = -1;
                for (int k = 0; k < path.Count; k++) if (path[k] == slot) { idx = k; break; }
                if (idx < 0) continue;
                for (int k = idx + 1; k < path.Count && behind is null; k++)
                {
                    int seat = path[k];
                    behind = LivingMembers(dead.TeamId).FirstOrDefault(u => u.Slot == seat);
                    if (behind is not null) dest = k + 1 < path.Count ? path[k + 1] : -1;
                }
                if (behind is not null) break;
            }
            if (behind is not null)
            {
                int to = dest;
                UnitState? partner = to >= 0 ? LivingMembers(dead.TeamId).FirstOrDefault(u => u.Slot == to) : null;
                Log(to >= 0 ? $"    {yomi.Name} の一撃の勢いが {dead.Name} を越えて {behind.Name} を吹き飛ばした"
                            : $"    {yomi.Name} の一撃の勢いで {behind.Name} がよろめいた", LogKind.Trigger);
                if (_verbose) Emit(new BattleEvent
                {
                    Kind = BattleEventKind.KillImpact, Turn = _turn, ActorId = yomi.InstanceId, TargetId = behind.InstanceId,
                    PartnerId = partner?.InstanceId, SpreadFromId = dead.InstanceId, Slot = to >= 0 ? to : behind.Slot,
                    Text = to >= 0 ? ImpactLabels.Blow : ImpactLabels.Stumble, Team = yomi.TeamId,
                });
                if (to >= 0)
                {
                    if (SwapSlots(behind, to, yomi)) { t.ImpactBlow++; (t.ImpactBlowByTurn ??= new long[7])[tb]++; }
                    else t.ImpactRefused++;
                }
                else t.ImpactStumble++;
                if (behind.IsAlive)
                {
                    behind.SetCounter(StatusKeys.Stagger, 1);
                    EmitStagger(behind, StaggerLabels.Fell, yomi);
                    Log($"    {behind.Name} は転んだ（次の手番を失う）", LogKind.Status);
                }
                continue;
            }
            // 勢い余って（味方側・1ターン2回）
            int used = yomi.RawCounter(KillImpactTrait.TurnKey) == _turn + 1 ? yomi.RawCounter(KillImpactTrait.CountKey) : 0;
            if (used >= KillImpactTrait.TumblesPerTurn) { t.ImpactCapped++; continue; }
            var cands = LivingMembers(yomi.TeamId).Where(a => a != yomi && !FormationRules.IsSummonSlot(a) && !a.HasTrait(TraitId.Planted)
                                                          && FormationRules.AreAdjacent(yomi, a)).ToList();
            UnitState? with = PickOne(cands);
            if (with is null) { t.ImpactNoAlly++; continue; }
            yomi.SetCounter(KillImpactTrait.TurnKey, _turn + 1);
            yomi.SetCounter(KillImpactTrait.CountKey, used + 1);
            Log($"    勢い余って {yomi.Name} は {with.Name} と場所を入れ替えた", LogKind.FriendlyFire);
            if (_verbose) Emit(new BattleEvent
            {
                Kind = BattleEventKind.KillImpact, Turn = _turn, ActorId = yomi.InstanceId, TargetId = with.InstanceId,
                SpreadFromId = dead.InstanceId, Slot = used + 1, Text = ImpactLabels.Tumble, Team = yomi.TeamId,
            });
            if (SwapSlots(yomi, with.Slot, yomi)) { t.ImpactTumble++; (t.ImpactTumbleByTurn ??= new long[7])[tb]++; }
            else t.ImpactRefused++;
        }
    }


    /// <summary>手当て（第224期・H2・<b>計数のみ</b>）。</summary>
    public void NoteRegroupTend(UnitState shio, int nominal, int gained)
    {
        UnitTally t = TallyOf(shio);
        t.TendFires++; t.TendNominal += nominal; t.TendGained += Math.Max(0, gained);
    }

    /// <summary>隊を組み替える相手がいなかった（<b>計数のみ</b>）。<paramref name="anyHurt"/> が偽なら全員満タン。</summary>
    public void NoteRegroupIdle(UnitState self, bool anyHurt)
    {
        UnitTally t = TallyOf(self);
        if (anyHurt) t.RegroupStuck++; else t.RegroupAllFull++;
    }

    /// <summary>振った回数を読むだけ（<b>帳簿の行を作らない</b>——<c>TallyOf</c> は無ければ作るので使わない）。</summary>
    long AttacksOf(UnitState u) => TallyByUnit.TryGetValue(u.Def.Id, out UnitTally? t) ? t.Attacks : 0;

    /// <summary>仇指しが刃を返した（<b>計数のみ</b>）。</summary>
    public void NoteVendetta(UnitState self, int dealt, bool marked)
    {
        UnitTally t = TallyOf(self);
        t.VendettaFires++;
        t.VendettaDealt += dealt;
        if (marked) t.VendettaMarks++;
    }

    /// <summary>返り血（<b>計数のみ</b>）。</summary>
    public void NoteRecoil(UnitState self, int taken) => TallyOf(self).RecoilTaken += taken;

    /// <summary>§2: 矢面の記憶が指しているのに標が剥がされていて、半減が掛からなかった被弾（回数・量）。</summary>
    public long BeckonStrippedHits, BeckonStrippedDamage;

    /// <summary>
    /// 矢面の記憶が指している相手なのに標が無い（ソラの剥がし・カリの付け替えで消えた）まま殴られた（<b>計数のみ</b>）。
    /// </summary>
    void NoteBeckonStripped(UnitState target, int amount)
    {
        if (target.RawCounter(StatusKeys.Marked) > 0) return;
        foreach (UnitState h in _beckonHolders)
            if (h.TeamId == target.TeamId && h.RawCounter(BeckonTrait.TargetKey) == target.InstanceId + 1)
            {
                BeckonStrippedHits++;
                BeckonStrippedDamage += amount;
                TallyOf(h).BeckonStrippedHits++;
                return;
            }
    }

    /// <summary>
    /// 矢面の半減が掛かる相手か。<b>標がある かつ 同じ陣営の矢面の保持者の記憶がこの駒を指している</b>
    /// （保持者の生死は問わない——標が残る限り守りも残る）。
    /// </summary>
    UnitState? BeckonGuardOf(UnitState target)
    {
        if (target.RawCounter(StatusKeys.Marked) <= 0) return null;
        foreach (UnitState h in _beckonHolders)
            if (h.TeamId == target.TeamId && h.RawCounter(BeckonTrait.TargetKey) == target.InstanceId + 1) return h;
        return null;
    }

    /// <summary>
    /// 泥人形ムドの規則（第181期。既定は <see cref="EruptRule.Default"/> ＝ 採用候補）。
    /// <b>保持者がいなければ1ビットも動かない</b>——読むのは <see cref="EruptTrait"/> と
    /// <see cref="SmearTrait"/> の中だけで、engine には判定が1つも無い。
    /// </summary>
    public EruptRule Erupt { get; }

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
        if (burnTick && relayed) { t.HarmBurnRelayed += amount; if (fatal) t.HarmBurnRelayedFatal++; }   // 第256期・**計数のみ**

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
                // 第277期: 豆鉄砲の一振りの中の撃破では、再行動は1振り1回（2体目以降は止める）。一振りの外では `_volley` は null。
                if (_volley is { } pv && pv.Actor == w)
                {
                    if (pv.Encored) { TallyOf(w).PelletEncoreCapped++; continue; }
                    pv.Encored = true;
                }
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
                                   AshRule? ash = null, EruptRule? erupt = null, MarkRule? markRule = null,
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
        if (Ember.BurnHit)   // 第256期: 被弾の燃焼 H-分担 を規定に（札 `BurnHitSplit` と同じ門を両陣営に立てる）
        {
            _burnHitLive = true;
            _burnHitTeams[0] = _burnHitTeams[1] = true;
            _splitTickTeams[0] = _splitTickTeams[1] = true;
        }
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
        Erupt = erupt ?? EruptRule.Default;
        MarkRules = markRule ?? MarkRule.Default;
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
            || u.HasTrait(TraitId.Goad) || u.HasTrait(TraitId.Scapegoat)
            || u.HasTrait(TraitId.Beckon) || u.HasTrait(TraitId.Vendetta)) MarkActive = true;   // 第184期に2本
        if (u.HasTrait(TraitId.Beckon)) _beckonHolders.Add(u);   // 第184期（半減の判定の短絡）
        if (u.HasTrait(TraitId.BeckonFeather)) _beckonFeatherLive = true;   // 第300期（矢面は羽も半分）
        if (u.HasTrait(TraitId.Deflect)) _deflectHolders.Add(u); // 第186期（逸らしの判定の短絡）
        if (u.HasTrait(TraitId.BeckonHold)) _holdLive = true;              // 第294期（踏みとどまり・猶予・橋）
        if (u.HasTrait(TraitId.DeflectWide)) _wideHolders.Add(u);          // 第294期（SR-a・肩代わりと範囲の逸らし）
        if (u.HasTrait(TraitId.DivertPressure)) _pressureHolders.Add(u);   // 第294期（SR-b・重圧）
        if (u.HasTrait(TraitId.StaticMembrane)) _membraneHolders.Add(u);   // 第294期（SM・静電気の膜）
        if (u.HasTrait(TraitId.MarkRally) || u.HasTrait(TraitId.MarkRallyWide)) { _rallyLive = true; _rallyHolders.Add(u); }   // 第295期（HK・攻撃のひとまとまり）
        if (u.HasTrait(TraitId.FeatherMark) || u.HasTrait(TraitId.FeatherMarkLayer)) { _mfLive = true; _mfHolders.Add(u); }   // 第298期（MF・標が付いた瞬間の羽）
        if (u.HasTrait(TraitId.Vendetta)) _vendettaTurnLive = true;   // 第299期（ザンの手番の計数・仇巡り）
        if (u.HasTrait(TraitId.Thrust) || u.HasTrait(TraitId.ThrustPlain)) _thrustLive = true;   // 第186期 追補
        if (HeroShieldTrait.Holds(u)) _heroShieldLive = true;   // 第267期（勇者の庇い）
        if (u.HasTrait(TraitId.Evade)) _evadeLive = true;   // 第223期（回避の判定・的の固定・乱れ撃ちの短絡）
        if (u.HasTrait(TraitId.Pellet)) _pelletLive = true;   // 第277期（豆鉄砲の一振り・1発の打点・再行動の上限）
        if (u.HasTrait(TraitId.Rupture)) _ruptureLive = true; // 第281期（炸裂の標の段・打点・乱射・敵の標の層）
        if (u.HasTrait(TraitId.Feathers)) _featherLive = true; // 第285期（羽の一振り・羽の書き込み・1発の打点）
        if (u.HasTrait(TraitId.FireArmor) || u.HasTrait(TraitId.Smolder)
            || u.HasTrait(TraitId.FireMend) || u.HasTrait(TraitId.FireFeed)) _fireArmorLive = true;   // 第234期（火の鎧・焼け残り）・第235期（火の癒し・焼き返し）
        if (u.HasTrait(TraitId.FireWard) || u.HasTrait(TraitId.FireWardAll)) _fireWardHolders.Add(u);             // 第238期（盾の配り）
        if (u.HasTrait(TraitId.FireConvert) || u.HasTrait(TraitId.FireConvertHalf)) _fireConvertHolders.Add(u);   // 第238期（火の変換）
        if (FireLevelRule.Holds(u)) { _fireLvLive = true; _fireLvTeams[u.TeamId] = true; }   // 第242期（火勢）
        if (u.HasTrait(TraitId.FireSpreadCap)) _spreadCapTeams[u.TeamId] = true;              // 第244期（燃え広がりの上限）
        if (FireKindleRule.Holds(u)) _kindleLive = true;                                      // 第252期（ボルグが育つ口・あぶれた火）
        if (u.HasTrait(TraitId.FoeFireLevel)) { _foeFireLive = true; _foeFireTeams[Opponent(u.TeamId)] = true; }   // 第245期（敵の火勢）
        if (u.HasTrait(TraitId.FoeFireTick)) { _foeFireLive = true; _lvTickTeams[Opponent(u.TeamId)] = true; }
        if (u.HasTrait(TraitId.AllyFireTick)) { _foeFireLive = true; _lvTickTeams[u.TeamId] = true; }
        if (u.HasTrait(TraitId.FoeFireBrittle)) { _foeFireLive = true; _foeBrittleTeams[Opponent(u.TeamId)] = true; }
        if (u.HasTrait(TraitId.FoeFireSpread)) { _foeFireLive = true; _foeSpreadTeams[Opponent(u.TeamId)] = true; }
        if (u.HasTrait(TraitId.TickOnce)) _tickOnceLive = true;   // 第249期（刻み・一撃）
        NoteBurnHitHolder(u);                                      // 第255期（被弾の燃焼）
        if (u.HasTrait(TraitId.Decoy)) _decoyLive = true;         // 第226期（挑発）
        if (u.HasTrait(TraitId.Spring)) _springLive = true;       // 第228期（弾き返し）
        if (u.HasTrait(TraitId.RelicOverflowEdge)) _overflowLive = true;      // 第273期（レリック・溢れの刃）
        if (u.HasTrait(TraitId.RelicPoisonMagnet)) _poisonMagnetLive = true;  // 第273期（レリック・毒を招く）
        if (u.HasTrait(TraitId.Tailwind)) _tailwindLive = true;   // 第229期（追い風）
        if (u.HasTrait(TraitId.Landing)) _landingLive = true;     // 第237期（着地の反動）
        if (u.HasTrait(TraitId.SpringDaunt)) _dauntLive = true;   // 第243期（⑤ 動かした敵の萎縮を消費させる）
        if (u.HasTrait(TraitId.ConfuseHalf)) _confuseHalf = true; // 第243期（④ 半分の混乱）
        if (u.HasTrait(TraitId.KillImpact)) _impactLive = true;   // 第230期（撃破の衝撃）
        if (u.HasTrait(TraitId.Disarray)) _disarrayLive = true;   // 第226期（敵の乱れ）
        // 第185期: 組み付き・見せしめ（手番の頭の2つのキーと標的の選好を短絡させる）／踏みしめ（範囲の盾・層の軽減）／
        // 据えた足（入れ替えの空振り）。**保持者がいなければ比較1つで抜ける**——既存の行が 0 件差分であることの根拠。
        if (u.HasTrait(TraitId.Grapple) || u.HasTrait(TraitId.Shame)) _restrainLive = true;
        if (u.HasTrait(TraitId.Scourge)) _whipLive = true;                 // 第217期（鞭の枠と2倍）
        if (u.HasTrait(TraitId.StoredCharge)) _chargeLive = true;          // 第288期（蓄電の口・雷霆の枠）
        if (u.HasTrait(TraitId.StoredCharge) || u.HasTrait(TraitId.Thunder)) _chainReaders.Add(u);   // 第289期（連鎖の後の口）
        if (u.HasTrait(TraitId.Grapple)) _grappleLive = true;              // 第290期（クグの計数・糸の口の手前）
        if (u.HasTrait(TraitId.Thread)) _threadLive = true;                // 第290期（糸・KG-a〜）
        if (u.HasTrait(TraitId.LiveWireGuard)) _shockStunGuard = true;     // 第217期（G3H）
        // 第218期（澱みのミオの版・M3〜M5）。**保持者がいなければ比較1つで抜ける。**
        if (u.HasTrait(TraitId.MireDull) || u.HasTrait(TraitId.MireDullAll))
        {
            _mireDull = Math.Max(_mireDull, u.HasTrait(TraitId.MireDullAll) ? (byte)2 : (byte)1);
            _mireDullTeam = u.TeamId;
        }
        if (u.HasTrait(TraitId.MireCarry)) { _mireCarry = true; _mireHolder ??= u; }
        if (u.HasTrait(TraitId.MireHandoff)) { _mireHandoff = true; _mireHolder ??= u; }
        if (u.HasTrait(TraitId.MireBurst)) { if (_burstMode == 0) _burstMode = 1; _mireHolder ??= u; }                 // 第220期
        if (u.HasTrait(TraitId.MireBurstStack)) { _burstMode = 2; _mireHolder ??= u; }
        if (u.HasTrait(TraitId.MireBurstAll)) { _burstMode = 2; _burstAll = true; _mireHolder ??= u; }
        u.TakenTurn = 0;   // 第218期・**計数のみ**
        if (u.HasTrait(TraitId.Footing)) _shieldHolders.Add(u);
        if (u.HasTrait(TraitId.Planted)) _plantedLive = true;
        if (u.HasTrait(TraitId.Daunt)) _dauntLive = true;   // 第189期（萎縮の消費を短絡させる）
        if (u.HasTrait(TraitId.Numb)) _numbLive = true;     // 第195期（痺れ毒の減少を短絡させる）
        if (u.HasTrait(TraitId.Scrap)) _scrapHolders.Add(u); // 第207期（破片の減りを拾う口を短絡させる）
        if (u.HasTrait(TraitId.Thorns)) _thornsLive = true;
        if (u.HasTrait(TraitId.Brace)) _braceLive = true;   // 第213期（計数のみ）
        if (u.HasTrait(TraitId.BraceArmored) || u.HasTrait(TraitId.BraceCapFirst) || u.HasTrait(TraitId.BraceHeldDeliver)) _braceArmoredLive = true;   // 第213期に W1・W2 の札を足した   // 第212期（破片の前の身構え・受け切った一撃の弾き）  // 第211期（破片で受け切った一撃の棘・計数と Y3 の口を短絡させる）
        if (u.HasTrait(TraitId.PlankRebound)) { _reboundLive = true; _reboundTsugi ??= u; }   // 第208期（撃ち返す板）
        // 第190期: 反転の結界（ベニ）。**保持者がいなければ `Count == 0` の比較1つで抜ける**。
        if (u.HasTrait(TraitId.Inverse)) _inverseHolders.Add(u);
        if (u.HasTrait(TraitId.InverseLeak)) _inverseLeakHolders.Add(u);
        if (u.HasTrait(TraitId.Taint)) _taintHolders.Add(u);
        if (GurenTrait.Holds(u)) _gurenHolders.Add(u);   // 第197期・**計数専用**（奔流の刻みの帰属先）
        if (u.HasTrait(TraitId.Funnel)) FunnelActive = true;
        // 第137期: 砕けの保持者が盤上にいるか（`ShatterSoaked` を短絡させるためだけ。盤面には影響しない）。
        if (u.HasTrait(TraitId.Shatter)) ShatterActive = true;
        // 第132期 段1: 上限の保持者をここで拾う（`YokeBinding` が全駒を走査しないため）。
        // 第179期: 灰の保持者（`NoteAsh` が全駒を走査しないため）。
        if (u.HasTrait(TraitId.Ash)) _ashHolders.Add(u);
        if (u.HasTrait(TraitId.Yoke)) _yokeHolders.Add(u);
        if (u.HasTrait(TraitId.ShockTick)) _shockTickLive = true;   // 第214期（K2 の札）
        if (u.HasTrait(TraitId.BetrayedShockThunderPop)) _thunderPopLive = true;   // 第276期（S1p・雷で弾ける餌）
        // 第216期（S1〜S3 の札）。**保持者がいなければ 0 のまま**で、起爆の中の比較1つで抜ける。
        if (u.HasTrait(TraitId.ShockStunHalf)) _shockStun = Math.Max(_shockStun, (byte)3);
        else if (u.HasTrait(TraitId.ShockStunAll)) _shockStun = Math.Max(_shockStun, (byte)2);
        else if (u.HasTrait(TraitId.ShockStun)) _shockStun = Math.Max(_shockStun, (byte)1);
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
            .Where(a => a != u && a.AcceptsSupport && FormationRules.AreAdjacent(u, a))
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
        // 第294期（HS-c・橋）: 橋の割り込みの中だけ、受け手を踏みとどまった味方に固定する（`BeckonBridgeFire` が立てて消す）。橋が無ければ比較1つで抜ける。
        if (_bridgePatient is { } bp && bp.IsAlive && bp.TeamId == self.TeamId && bp != self) return bp;
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

    /// <summary>
    /// 範囲の盾が受け止めた瞬間（第185期 追補2・<b>表示専用</b>）。<see cref="EmitIntercept"/> と同じ種類
    /// （<see cref="BattleEventKind.Intercept"/>）を出すが、<b>計数（<c>Intercepts</c>・害の帳簿の印）には1つも触らない</b>
    /// ——あちらを呼ぶと `pulse` / `harm` の数字が動く。<c>verbose</c> のときしか積まない。
    /// 直後にバンへの <c>Damage</c> が続く（受けた量は層・軛の後の値でそちらに出る）。
    /// </summary>
    private void EmitShieldIntercept(UnitState shield, UnitState covered, int amount)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Intercept,
            Turn = _turn,
            ActorId = shield.InstanceId,
            TargetId = covered.InstanceId,
            Slot = shield.Slot,
            HpAfter = shield.Hp,
            Team = shield.TeamId,
            Amount = amount,
            Text = InterceptLabels.RangeShield,
        });
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

    // 組み付きの解除を台本に残す。0 は解除であり、盤面は呼び元で変更済み。
    internal void EmitGrappleRelease(UnitState self, UnitState target)
    {
        if (!_verbose) return;
        Emit(new BattleEvent { Kind = BattleEventKind.StatusGain, Turn = _turn,
            ActorId = self.InstanceId, TargetId = target.InstanceId,
            Text = StatusKeys.Grappled, Amount = 0 });
    }

    /// <summary>
    /// 状態異常が付いた瞬間を台本に打つ（第97期・<b>表示専用</b>）。
    ///
    /// <para><b>engine の窓口を持つ4通貨だけが呼ぶ</b>——<see cref="Wound"/> / <see cref="Poison"/> /
    /// <see cref="Ignite"/> / <see cref="Dull"/>。窓口の無い痺れ・標・破片は出さない
    /// （出すには先に窓口が要る。第90・93期と同じ形）。</para>
    /// <para><b>第182期に5本目の呼び口</b>——<see cref="HexTrait"/> の呪いの付与（書き手はムド）。
    /// 窓口は無いが付与口が1箇所きりなので、そこから直接呼ぶ。</para>
    ///
    /// <para><see cref="Emit"/> は verbose のときしか積まないので、<c>compare</c>（verbose 偽）では
    /// 1件も作られない。<b>盤面には一切影響しない</b>——<c>NoteStatusGain</c> の計数にも触っていない。</para>
    /// </summary>
    /// <param name="writer">書いた駒。engine の規則が足したぶんは null。</param>
    internal void EmitStatusGain(UnitState target, string key, int amount, UnitState? writer,
                                 PoisonRoute? route = null, UnitState? spreadFrom = null, PowderRoute? powder = null)
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
            PoisonRoute = route,                     // 第183期 追補2・表示専用（毒の窓口を通ったときだけ）
            SpreadFromId = spreadFrom?.InstanceId,   // 第183期 追補2・表示専用（伝染のときだけ）
            PowderRoute = powder,                    // 第291期・表示専用（トウの粉のときだけ）
            FriendlyFire = powder == BattleCore.PowderRoute.Leak,   // 第291期・表示専用（粉の漏れ）
        });
    }

    // 第291期 —— 表示専用の口（`ShockGauge` ／ `Feather` ／ `Scar` ／ `MarkLayer`）。**盤面を読むだけで書かない・乱数を引かない。**
    // verbose でなければ最初の比較で抜ける。呼び出し側は保持者の札（`_chargeLive` ／ `_featherLive` ／ `_ruptureLive` ほか）の後ろに置く。
    // 自己検査（`playtest291 check` (k)）はこの区間に乱数の口とカウンタ ／ HP の書き込みが無いことを見る。

    /// <summary>感電軸のゲージ（<see cref="ShockGaugeLabels"/>）を台本に打つ。</summary>
    internal void EmitShockGauge(string label, UnitState? actor, UnitState target, int amount, int slot = 0, UnitState? partner = null, int? remaining = null)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.ShockGauge, Turn = _turn, Text = label, ActorId = actor?.InstanceId, TargetId = target.InstanceId,
            Amount = amount, Slot = slot, PartnerId = partner?.InstanceId, StatusRemaining = remaining, Team = target.TeamId, HpAfter = target.Hp,
        });
    }

    /// <summary>ミサの羽（<see cref="FeatherLabels"/>）を台本に打つ。</summary>
    void EmitFeather(string label, UnitState? actor, UnitState? target, int amount, int slot = 0, UnitState? partner = null, int? remaining = null)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Feather, Turn = _turn, Text = label, ActorId = actor?.InstanceId, TargetId = target?.InstanceId,
            Amount = amount, Slot = slot, PartnerId = partner?.InstanceId, StatusRemaining = remaining, Team = actor?.TeamId,
        });
    }

    /// <summary>爪痕（最大HPが縮んだ）を台本に打つ。</summary>
    void EmitScar(UnitState misa, UnitState target, int lost)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Scar, Turn = _turn, ActorId = misa.InstanceId, TargetId = target.InstanceId,
            Amount = lost, Slot = target.MaxHp, HpAfter = target.Hp, Team = target.TeamId,
        });
    }

    /// <summary>標の層（前の層 → 新しい層）を台本に打つ。</summary>
    void EmitMarkLayer(UnitState writer, UnitState target, int before, int after)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.MarkLayer, Turn = _turn, ActorId = writer.InstanceId, TargetId = target.InstanceId,
            Amount = after, Slot = before, Team = target.TeamId,
        });
    }
    // 第291期 —— 表示専用の口（ここまで）

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
    /// <summary>吸い上げの通し番号（第183期 追補3・表示専用）。1戦の中で 1 から数える。</summary>
    private int _drainSeq;

    /// <summary>台本を積むか（表示専用の束ねを特性の側で組むときの番人。<b>盤面の判断に使わないこと</b>）。</summary>
    public bool Verbose => _verbose;

    /// <summary>
    /// 状態を取り上げた一連を台本に打つ（第183期 追補3・<b>表示専用</b>）。
    /// 吸われた駒1体につき1件、同じ <c>DrainSeq</c> を振り、最後の1件にだけ吸った側の攻撃力を載せる。
    /// <b>盤面には一切影響しない</b>（<c>verbose</c> 偽では何もしない）。
    /// </summary>
    /// <summary>盤面に濃縮の印が1つでも付いたか（第194期）。偽なら刻みの2回目の判定を比較1つで抜ける。</summary>
    bool _markLive;

    /// <summary>
    /// 濃縮の印を +1 する（第194期・<see cref="ConcentrateTrait"/> だけが呼ぶ）。<b>重ねがけ可・上限なし</b>。
    /// 倒れている駒には何もせず偽を返す。乱数を引かない。印の最大値はミオの帳簿（<c>ConcMarkPeak</c>）に写す。
    /// </summary>
    /// <summary>
    /// 痺れ毒の印を付ける（第195期・スィド）。<b>二値</b>——既に付いていれば何もしない（付いた経路は最初の1回のもの）。
    /// 付けたら真。<b>乱数を引かない。</b>印が付いた瞬間は <c>StatusGain</c>（<see cref="StatusKeys.Numbed"/>）で台本に出る。
    /// </summary>
    public bool MarkNumbed(UnitState writer, UnitState u, int origin)
    {
        if (!u.IsAlive || u.RawCounter(StatusKeys.Numbed) > 0) return false;
        u.SetCounter(StatusKeys.Numbed, origin);
        EmitStatusGain(u, StatusKeys.Numbed, 1, writer);   // 表示専用（印が付いた瞬間）
        Log($"    {u.Name} に痺れ毒が回った（毒の層 × {NumbTrait.PercentPerLayer}% だけ手が鈍る）", LogKind.Status);
        return true;
    }

    /// <summary>痺れ毒の計数（第195期）。<b>計数専用・どの規則も読まない。</b>振った側（印のある駒）に付ける。</summary>
    void NoteNumbed(UnitState actor, int layers, int pct, int cut)
    {
        UnitTally t = TallyOf(actor);
        t.NumbedSwings++;
        t.NumbedCut += cut;
        if (actor.RawCounter(StatusKeys.Numbed) == SpewTrait.OriginVenom) t.NumbedCutVenom += cut; else t.NumbedCutSpew += cut;
        t.NumbedLayerSum += layers;
        t.NumbedLayerMax = Math.Max(t.NumbedLayerMax, layers);
        if (pct >= NumbTrait.MaxPercent)
        {
            t.NumbedCapSwings++;
            t.NumbedCapTurn = UnitTally.MinReach(t.NumbedCapTurn, _turn);
        }
    }

    public bool MarkConcentrated(UnitState mio, UnitState u, string label) => MarkConcentrated(mio, u, label, 1, null);

    /// <summary>
    /// 濃縮の印を <paramref name="add"/> だけ足す（第218期に量を引数にした・1 なら第194期と1ビットも違わない）。
    /// <paramref name="from"/> は倒れた敵から移った印の出どころ（台本の <c>SpreadFromId</c>・<b>表示専用</b>）。
    /// 移った印の台本は <c>StatusRemaining</c> ＝ 移した数。
    /// </summary>
    public bool MarkConcentrated(UnitState mio, UnitState u, string label, int add, UnitState? from)
    {
        if (!u.IsAlive) return false;
        int n = u.RawCounter(StatusKeys.Concentrated) + add;
        u.SetCounter(StatusKeys.Concentrated, n);
        _markLive = true;
        UnitTally mt = TallyOf(mio);
        if (n > mt.ConcMarkPeak) mt.ConcMarkPeak = n;
        if (_verbose)
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.ConcentrateMark, Turn = _turn, ActorId = mio.InstanceId,
                TargetId = u.InstanceId, Amount = n, Text = label, SourceTrait = TraitId.Concentrate,
                SpreadFromId = from?.InstanceId, StatusRemaining = from is null ? null : add,
            });
        return true;
    }

    // =================================================================================
    // 第218期 —— 澱みのミオ（叩きつけ・通電・澱みのデバフ・印を運ぶ／移す）
    //
    // 叩きつけと通電は `ConcentrateTrait` の最後から `MireSlam` を呼ぶ。デバフは与ダメの量を作る4口で `MireCut`、
    // 運ぶは `Discharge` の最後、移すは `HandleDeath` の中。**保持者がいなければどれも比較1つで抜ける。乱数を引かない。**
    // =================================================================================

    /// <summary>澱みのデバフ: 0 なし ／ 1 敵だけ（保持者と違う陣営）／ 2 両方。</summary>
    byte _mireDull;
    int _mireDullTeam;
    bool _mireCarry, _mireHandoff;
    /// <summary>運ぶ・移るの書き手（最初に加わった保持者）。倒れていても書き手として使う。</summary>
    UnitState? _mireHolder;

    /// <summary>
    /// 澱みのデバフ（第218期）。出どころの印 × <see cref="MireDullTrait.PercentPerMark"/>%（上限 <see cref="MireDullTrait.MaxPercent"/>%）を切り捨てで引く。
    /// 敵だけの版は保持者と同じ陣営の出どころに掛けない。<paramref name="route"/>: 0 攻撃 ／ 1 雷 ／ 2 放電 ／ 3 叩きつけ（計数のみ）。
    /// </summary>
    int MireCut(UnitState src, int amount, int route)
    {
        if (amount <= 0) return amount;
        int n = src.RawCounter(StatusKeys.Concentrated);
        if (n <= 0) return amount;
        if (_mireDull == 1 && src.TeamId == _mireDullTeam) return amount;
        int pct = Math.Min(n * MireDullTrait.PercentPerMark, MireDullTrait.MaxPercent);
        int cut = amount * pct / 100;
        UnitTally t = TallyOf(src);
        t.MireDulledHits++;
        t.MireDulledCut += cut;
        (t.MireDulledByRoute ??= new long[4])[route] += cut;
        if (cut > 0) Log($"    {src.Name} は澱みに手を取られた（-{pct}%・この一撃 -{cut}）", LogKind.Status);
        return amount - cut;
    }

    /// <summary>印を運ぶ（第218期・M4〜）。放電を受けて生きている駒に印 +1（書き手は保持者）。</summary>
    void MireCarryTo(UnitState to, UnitState? from = null)
    {
        if (_mireHolder is null || !to.IsAlive) return;
        if (!MarkConcentrated(_mireHolder, to, ConcentrateTrait.CarryLabel)) return;
        if (_verbose && from is not null)   // 第219期・表示専用（どこから・どこへ・何個）
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.MireCarried, Turn = _turn, ActorId = _mireHolder.InstanceId, TargetId = to.InstanceId,
                SpreadFromId = from.InstanceId, Amount = 1, StatusRemaining = to.RawCounter(StatusKeys.Concentrated),
                SourceTrait = TraitId.MireCarry,
            });
        UnitTally ht = TallyOf(_mireHolder);
        ht.MireCarried++;
        if (to.TeamId == _mireHolder.TeamId) ht.MireCarriedAlly++;
    }

    /// <summary>
    /// 倒れたら印が移る（第218期・M5・敵だけ）。倒れた駒の印を全部、隣の生きている駒（同じ陣営）のうち次の刻みが最も大きい1体へ
    /// （同値は席番号）。隣が1体もいなければ消える（数える）。
    /// </summary>
    void MireHandoff(UnitState dead, int marks)
    {
        UnitTally ht = TallyOf(_mireHolder!);
        UnitState? pick = null;
        int best = 0;
        foreach (UnitState u in LivingMembers(dead.TeamId))
        {
            if (u == dead || !FormationRules.AreAdjacent(dead, u)) continue;
            int n = ConcentrateTrait.NextTick(u);
            if (pick is null || n > best || (n == best && u.Slot < pick.Slot)) { pick = u; best = n; }
        }
        if (pick is null) { ht.MireHandoffLost += marks; return; }
        dead.SetCounter(StatusKeys.Concentrated, 0);
        MarkConcentrated(_mireHolder!, pick, ConcentrateTrait.HandoffLabel, marks, dead);
        if (_verbose)   // 第219期・表示専用（どこから・どこへ・何個）
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.MireHandedOff, Turn = _turn, ActorId = _mireHolder!.InstanceId, TargetId = pick.InstanceId,
                SpreadFromId = dead.InstanceId, Amount = marks, StatusRemaining = pick.RawCounter(StatusKeys.Concentrated),
                SourceTrait = TraitId.MireHandoff,
            });
        ht.MireHandedOff += marks;
        ht.MireHandoffs++;
        Log($"    {dead.Name} の澱みが {pick.Name} へ流れ込む（印 {marks}）", LogKind.Status);
    }

    /// <summary>
    /// 叩きつけ（第218期・<see cref="MireSlamTrait"/>・ミオの手番の最後）。寄せ先（中心）へ <b>ミオの現在攻撃力</b>（澱みのデバフの後）の一撃を
    /// <c>ApplyDamage</c> で直に入れる（<c>PerformAttack</c> は通らない＝標的の鎖・庇いを通らない。撃破はミオ）。HP に届けば既存の規則で感電が弾ける。
    /// <para><b>通電</b>（<see cref="MireConductTrait"/>）: 叩きつける相手が<b>当てる前に</b>感電していれば、中心へ当てた後、
    /// <b>印を持つ生きている敵</b>（席番号順・中心を除く）それぞれに同じ量の一撃。1手番に1回。</para>
    /// </summary>
    public void MireSlam(UnitState mio, UnitState center)
    {
        UnitTally t = TallyOf(mio);
        bool shocked = center.RawCounter(StatusKeys.Shock) > 0;
        bool conduct = shocked && mio.HasTrait(TraitId.MireConduct);
        int amt = Math.Max(0, mio.CurrentAttack);
        if (_mireDull != 0) amt = MireCut(mio, amt, 3);
        List<UnitState>? others = conduct
            ? LivingMembers(center.TeamId).Where(u => u != center && u.RawCounter(StatusKeys.Concentrated) > 0).ToList()
            : null;
        int reach = 1 + (others?.Count ?? 0);
        t.MireSlams++;
        if (shocked) t.MireSlamOnShocked++;
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.MireSlam, Turn = _turn, ActorId = mio.InstanceId, TargetId = center.InstanceId,
            Amount = amt, Slot = conduct ? 1 : 0, StatusRemaining = conduct ? reach : null, Team = center.TeamId,
        });
        Log($"    {mio.Name} が澱みを {center.Name} に叩きつけた（{amt}）" + (conduct && reach > 1 ? $"——濁った水が雷を通す（{reach} 体）" : ""), LogKind.Trigger);
        MireHit(mio, center, amt, t, first: true);
        if (others is null || others.Count == 0) return;

        t.MireConducts++;
        if (center.Shape == FormationShape.X && !LivingMembers(center.TeamId).Any(u => u.Slot == 2)) t.MireConductAfterCenter++;
        int k = 1;
        foreach (UnitState u in others)
        {
            if (!u.IsAlive) continue;
            k++;
            t.MireConductHits++;
            if (_verbose) Emit(new BattleEvent
            {
                Kind = BattleEventKind.MireConduct, Turn = _turn, ActorId = mio.InstanceId, TargetId = u.InstanceId,
                SpreadFromId = center.InstanceId, Amount = amt, Slot = k, StatusRemaining = reach, Team = u.TeamId,
            });
            MireHit(mio, u, amt, t, first: false);
        }
    }

    void MireHit(UnitState mio, UnitState u, int amt, UnitTally t, bool first)
    {
        if (!u.IsAlive || amt <= 0) return;
        int before = u.Hp;
        bool wasShocked = u.RawCounter(StatusKeys.Shock) > 0;
        _brittleSlamNext = true;   // 第219期・計数の経路だけ（1回の呼び出しにだけ効く）
        if (first) ApplyDamage(u, amt, mio, singleHit: true, pattern: AttackPattern.Single);
        else ApplyDamage(u, amt, mio);
        _brittleSlamNext = false;
        t.MireSlamDealt += before - Math.Max(0, u.Hp);
        if (wasShocked && u.RawCounter(StatusKeys.Shock) <= 0) { if (first) t.MireSlamPops++; else t.MireConductPops++; }
        if (before > 0 && !u.IsAlive) t.MireSlamKills++;
    }

    /// <summary>
    /// 濃縮（旧 <see cref="AmplifierTrait"/> の本体・ミオ）で敵の毒の層が増えた瞬間を台本に打つ（第194期・<b>表示専用</b>）。
    /// <paramref name="label"/> は <see cref="ThickenLabels"/>（+4 層の濃縮／傷口への着火）。<paramref name="after"/> は足した後の層。
    /// <b>盤面には一切影響しない</b>（<c>verbose</c> 偽では何もしない）。
    /// </summary>
    public void EmitThicken(UnitState mio, UnitState foe, int after, string label)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.PoisonThicken, Turn = _turn, ActorId = mio.InstanceId,
            TargetId = foe.InstanceId, Amount = after, Text = label, SourceTrait = TraitId.Amplifier,
        });
    }

    /// <summary>
    /// 第194期。刻み1回ぶんを駒の帳簿に写す（毒と燃焼を分ける・<b>盤面には触らない</b>）。
    /// 量は実際に刻む量（毒は層 × 旧 `Devour`、燃焼は定数）。反転で回復になった刻みも数える。
    /// </summary>
    void NoteTickLayer(UnitState u, int amount, bool burn, bool second)
    {
        UnitTally t = TallyOf(u);
        bool cut = amount > Yoke.Cap && YokeBinding;
        if (second) (t.ConcExtraByTurn ??= new long[8])[Math.Clamp(_turn, 0, 7)] += amount;   // 第218期・**計数のみ**
        if (burn)
        {
            if (amount > t.BurnTickMax) t.BurnTickMax = amount;
            if (_turn <= 3) t.BurnTickEarly += amount; else t.BurnTickLate += amount;
            if (second) t.BurnTickSecond += amount;
            if (cut) t.BurnYokeCut++;
            return;
        }
        if (!second)
        {
            if (amount >= 25 && (t.PoisonReach25 == 0 || _turn < t.PoisonReach25)) t.PoisonReach25 = _turn;
            if (amount >= 50 && (t.PoisonReach50 == 0 || _turn < t.PoisonReach50)) t.PoisonReach50 = _turn;
        }
        if (amount > t.PoisonTickMax) t.PoisonTickMax = amount;
        if (_turn <= 3) t.PoisonTickEarly += amount; else t.PoisonTickLate += amount;
        if (second) t.PoisonTickSecond += amount;
        if (cut) { t.PoisonYokeCut++; t.PoisonYokeLost += amount - Yoke.Cap; }
    }

    public void EmitStatusDrain(UnitState drainer, string key,
                                IReadOnlyList<(UnitState From, int Amount)> drained)
    {
        if (!_verbose || drained.Count == 0) return;
        int seq = ++_drainSeq;
        for (int i = 0; i < drained.Count; i++)
        {
            bool last = i == drained.Count - 1;
            (UnitState from, int amount) = drained[i];
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.StatusDrain,
                Turn = _turn,
                ActorId = drainer.InstanceId,
                TargetId = from.InstanceId,
                Amount = amount,
                Text = key,
                Team = drainer.TeamId,
                StatusRemaining = from.RawCounter(key),
                DrainSeq = seq,
                DrainLast = last,
                AttackAfter = last ? drainer.CurrentAttack : null,
            });
        }
    }

    /// <summary>
    /// 叩き起こし（<see cref="BattleEventKind.Reveille"/>）を台本に打つ（<b>表示専用</b>）。
    /// <paramref name="label"/> は <see cref="ReveilleLabels"/>。
    /// </summary>
    public void EmitReveille(UnitState caller, UnitState ally, string label)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Reveille,
            Turn = _turn,
            ActorId = caller.InstanceId,
            TargetId = ally.InstanceId,
            HpAfter = ally.Hp,
            Team = caller.TeamId,
            Text = label,
        });
    }

    /// <summary>粛がいま効いているか（<b>表示専用の読み口</b>。盤面の判断に使わないこと）。</summary>
    public bool HushBindingNow => Hush.Active && HushHolderAlive;

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
        RefreshDecoyShow();   // 第231期（表示専用）

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
        if (_decoyPicked is not null) NoteDecoy(_decoyPicked, attacker, chosen);

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
        RefreshDecoyShow();   // 第231期（表示専用・verbose のときだけ）
        // 第135期。**標的選択1回ごとに印を落とす**（計数専用）。立ったまま次の一撃へ持ち越すと、
        // 破片が全額吸って `NoteHarm` に届かなかった介入が、無関係な被弾を「引き受けたぶん」に化けさせる。
        _interceptedInto = null;
        _decoyPicked = null;

        // 第223期: 的の固定（追い撃ち・乱れ撃ち）。**介入の鎖を通さない**——撃ち返す相手・矢の的は決まっている。読んで消す。
        if (_forcedTarget is not null)
        {
            UnitState ft = _forcedTarget;
            _forcedTarget = null;
            int fl = _forcedLane;
            _forcedLane = -1;
            if (!ft.IsAlive) return null;
            if ((patternOverride ?? attacker.CurrentPattern) == AttackPattern.Pierce) lane = fl >= 0 ? fl : ForcedLane(ft);
            return ft;
        }

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
        {
            // 突き（第186期 追補）: **指差した敵のいる列を突き抜く**。宛先が生きていて foes（混乱なら反転後の陣営）に
            // いるときだけ。中央は2本の列に属するので、生きている敵が多い列・同数なら番号の若い列（`Roll` を引かない）。
            // 宛先が列に属さない席（○前2・○後2）にいれば通常の選び方に落とす。
            if (_thrustLive && (attacker.HasTrait(TraitId.Thrust) || attacker.HasTrait(TraitId.ThrustPlain)))
            {
                UnitState? aim = DeflectTrait.Target(this, attacker);
                if (aim is not null && foes.Contains(aim))
                {
                    int best = -1, bestN = -1;
                    foreach (int l in aim.Shape.LanesOf(aim.Slot))
                    {
                        int n = LaneOccupants(foes, l, aim.Shape).Count;
                        if (n > bestN || (n == bestN && l < best)) { best = l; bestN = n; }
                    }
                    if (best >= 0)
                    {
                        lane = best;
                        TallyOf(attacker).ThrustForced++;
                        return LaneOccupants(foes, lane, aim.Shape)[0];
                    }
                }
            }
            return SelectPierceEntry(attacker, foes, out lane);
        }

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
        // 見せしめ（第185期・シガ）: 動けない敵を優先する。**執着・断ちと同じ段**（pool から無作為に選ぶ直前）で、
        // pool は1体も足さない・引かない（前列の制約の内側）。標の段・庇いの鎖はこの後ろ。
        // 効いた手番は pool の Roll を引かない（執着・断ちと同じ）。**保持者がいなければ比較1つで抜ける。**
        UnitState? shamed = _restrainLive && fixated is null && severed is null
                            && (pattern == AttackPattern.Single || (_whipLive && pattern == AttackPattern.Sweep && attacker.HasTrait(TraitId.Lash)))   // 第217期: 鞭は薙ぎにも
                            && attacker.HasTrait(TraitId.Shame)
            ? ShameTrait.Preferred(this, pool)
            : null;
        if (shamed is not null) TallyOf(attacker).ShamePicks++;

        // 挑発（第226期・回避盾のセロ）: **主目標の段の最後**（攻撃者側の選好の後・`pool[Roll]` の代わり）。pool は1体も足さない・引かない
        // （前列の規則の内側）。**以下の介入（標・後備え・庇う・殉教・棘守り）はすべてこの後ろなので挑発より優先。**
        // 単体の一撃だけ。効いた一撃は `pool[Roll]` を引かない。**保持者がいなければ比較1つで抜ける。**
        UnitState? decoy = _decoyLive && fixated is null && severed is null && shamed is null && pattern == AttackPattern.Single
            ? DecoyTrait.Pick(this, attacker, pool)
            : null;
        _decoyPicked = decoy;

        UnitState target = fixated ?? severed ?? shamed ?? decoy ?? pool[Roll(pool.Count)];

        // 第229期（転倒の穴）: 転倒した列が立っていれば狙えなかった駒を選んだ（計数 ＋ 表示専用の出来事）。**規則が偽なら比較1つで抜ける。**
        if (Shuffler.StaggerHole && !PoolOfPlain(foes).Contains(target))
        {
            TallyOf(attacker).HoleBreaches++;
            Log($"    {attacker.Name} は倒れた前列を越えて {target.Name} を狙った", LogKind.Trigger);
            if (_verbose) Emit(new BattleEvent
            {
                Kind = BattleEventKind.StaggerBreach, Turn = _turn, ActorId = attacker.InstanceId, TargetId = target.InstanceId,
                Slot = target.Slot, Team = attacker.TeamId,
            });
        }

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
                f => f.HasTrait(TraitId.RearGuard) && f.Row == Row.Back && f != target && !HoleSkip(f)).ToList());   // 第229期: 転倒の穴

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
        // 第281期: 炸裂（新トメ）も同じ段で 100%・決定的に狙う。選好だけが違う（層が最も深い1体）。**保持者がいなければ比較1つで抜ける。**
        bool rupture = _ruptureLive && attacker.HasTrait(TraitId.Rupture);
        bool finisher = rupture || attacker.HasTrait(TraitId.Finisher);
        UnitState? marked = rupture
            ? RuptureTrait.Preferred(this, foes)
            : finisher
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
            f => f.HasTrait(TraitId.RearGuard) && f.Row == Row.Back && f != target && !HoleSkip(f)).ToList());   // 第229期: 転倒の穴

        if (target.Row != Row.Front && rear is not null && Roll(100) < RearGuardTrait.RedirectPercent)
        {
            Log($"    {rear.Name} が後列の {target.Name} の前に入った", LogKind.Trigger);
            NoteGuardPick(GuardKind.RearGuard, rear, target);   // 第120期・§2-5 の材料
            NoteConfusedGuard(attacker, rear);   // 第147期（計数のみ）
            EmitIntercept(rear, target, InterceptLabels.RearGuard);   // 第125期 段1（表示専用）
            return rear;
        }

        UnitState? guardian = PickOne(foes.Where(
            f => f.HasTrait(TraitId.Guardian) && f.Row == Row.Front && f != target && !HoleSkip(f)).ToList());   // 第229期: 転倒の穴

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
            f => f.HasTrait(TraitId.Martyr) && f.Row == Row.Front && f != target && !HoleSkip(f)).ToList());   // 第229期: 転倒の穴

        if (martyr is not null && Roll(100) < Martyr.RedirectPercent)
        {
            Log($"    {martyr.Name} が {target.Name} を庇った", LogKind.Trigger);
            martyr.SetCounter(RedirectGainTrait.PendingKey, 1);
            NoteGuardPick(GuardKind.Martyr, martyr, target);   // 第120期・§2-5 の材料
            NoteConfusedGuard(attacker, martyr);   // 第147期（計数のみ）
            EmitIntercept(martyr, target, InterceptLabels.Martyr);   // 第125期 段1（表示専用）
            return martyr;
        }

        // 勇者の庇い（第267期・`bosswave` の勇者だけ）。**殉教の向き替え**——守る相手を癒し手（`HeroMend` の持ち主）に限り、列は問わない。
        // 保持者がいなければ `_heroShieldLive` の比較1つで抜ける。100% の版は `Roll` を引かない（候補が1体なら `PickOne` も引かない）。
        if (_heroShieldLive && target.HasTrait(TraitId.HeroMend))
        {
            UnitState? shield = PickOne(foes.Where(f => f != target && HeroShieldTrait.Holds(f) && !HoleSkip(f)).ToList());
            if (shield is not null)
            {
                int pct = HeroShieldTrait.PercentOf(shield);
                if (pct >= 100 || Roll(100) < pct)
                {
                    Log($"    {shield.Name} が {target.Name} を庇った", LogKind.Trigger);
                    EmitIntercept(shield, target, InterceptLabels.HeroShield);
                    return shield;
                }
            }
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
                 && ThornGuardTrait.Covers(f, target) && !HoleSkip(f)).ToList());   // 第229期: 転倒の穴

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

    private List<UnitState> PoolOf(List<UnitState> foes)
    {
        // 第229期（転倒の穴）: 転倒している駒は壁に数えない。**立っている駒がいる一番前の列**までを狙える（その手前の列の転倒した駒も狙える）。
        // 転倒した駒が1体もいなければ下の道と1ビットも違わない（並びも同じ・乱数も同じ）。
        if (Shuffler.StaggerHole && foes.Any(f => f.RawCounter(StatusKeys.Stagger) > 0))
        {
            foreach (Row r in new[] { Row.Front, Row.Mid, Row.Back })
            {
                if (!foes.Any(f => f.Row == r && f.RawCounter(StatusKeys.Stagger) == 0)) continue;
                int d = FormationRules.DepthOf(r);
                return foes.Where(f => FormationRules.DepthOf(f.Row) <= d).ToList();
            }
            return foes;
        }
        return PoolOfPlain(foes);
    }

    /// <summary>前列の規則（第228期まで）: 前列 → 中列 → 全員。</summary>
    private static List<UnitState> PoolOfPlain(List<UnitState> foes)
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
    private UnitState SelectPierceEntry(UnitState attacker, List<UnitState> foes, out int lane)
    {
        lane = -1;
        // 第200期: 経路は受ける隊の陣形から引く（`foes` は1つの隊。混乱で反転していても同じ隊）。
        FormationShape shape = foes[0].Shape;

        // パターン2（ひし形）・今後の新しい陣形は**乱数を引かない**。選び方は陣形の `PierceRule`（`PickPierceLane`）。
        // X 字はこの枝を通らないので、下の `Roll` の引き方は第199期と1ビットも違わない。
        if (shape.DeterministicPierce)
        {
            int best = PickPierceLane(attacker, foes, shape);
            if (best < 0) { PierceDeadEnds++; return foes[Roll(foes.Count)]; }
            lane = best;
            return LaneOccupants(foes, lane, shape)[0];
        }

        var lanes = Enumerable.Range(0, shape.LaneCount)
            .Where(l => foes.Any(f => shape.LanesOf(f.Slot).Contains(l)))
            .ToList();

        // ○前2・○後2 はどのレーンにも属さないので、生き残りがそこだけになると
        // 走る列が無くなる。落とさずに単体として1体だけ刺す（lane = -1）。
        if (lanes.Count == 0) { PierceDeadEnds++; return foes[Roll(foes.Count)]; }

        var deep = lanes
            .Where(l => foes.Any(f => shape.LanesOf(f.Slot).Contains(l)
                                      && f.Row != Row.Front))
            .ToList();

        List<int> pick = deep.Count > 0 ? deep : lanes;
        lane = pick[Roll(pick.Count)];

        return LaneOccupants(foes, lane, shape)[0];
    }

    /// <summary>
    /// 規則で選ぶ陣形の貫きの経路（<b>乱数を引かない</b>）。生きている駒のいる経路が無ければ −1。
    /// <list type="bullet">
    ///   <item><see cref="PierceRule.MostOccupied"/>（第200〜201期）: 生きている駒が多い方・同数なら添字の若い方</item>
    ///   <item><see cref="PierceRule.Facing"/>（第202期）: 撃つ敵のいる格子のレーン（<see cref="FormationShape.GridLane"/>）を通る経路。
    ///         両方の経路に入る（2レーン）なら多い方・同数ならその敵が貫くたびに交互（最初は添字の若い方）。
    ///         選んだ経路が空なら反対側（生きている駒の多い方）</item>
    /// </list>
    /// 交互の記録は <see cref="_pierceAlt"/>（この戦闘だけ・駒の <c>InstanceId</c> ごと）——会戦の次の戦では最初に戻る。
    /// </summary>
    private int PickPierceLane(UnitState attacker, List<UnitState> foes, FormationShape shape)
    {
        int nl = shape.LaneCount;
        var occ = new int[nl];
        for (int l = 0; l < nl; l++) occ[l] = LaneOccupants(foes, l, shape).Count;

        int MostOf(IEnumerable<int> ls)
        {
            int best = -1, bestN = 0;
            foreach (int l in ls) if (occ[l] > bestN) { best = l; bestN = occ[l]; }
            return best;
        }

        if (shape.Pierce == PierceRule.MostOccupied) return MostOf(Enumerable.Range(0, nl));

        int gl = FormationShape.GridLane(attacker.Slot);
        var facing = Enumerable.Range(0, nl).Where(l => shape.LaneCovers(l, gl)).ToList();
        int pick;
        bool tie = false;
        if (facing.Count == 1) pick = facing[0];
        else
        {
            int top = facing.Max(l => occ[l]);
            var tops = facing.Where(l => occ[l] == top).ToList();
            if (tops.Count == 1) pick = tops[0];
            else
            {
                tie = true;
                int k = _pierceAlt.GetValueOrDefault(attacker.InstanceId);
                pick = tops[k % tops.Count];
                _pierceAlt[attacker.InstanceId] = k + 1;
            }
        }

        bool fallback = false;
        if (occ[pick] == 0)
        {
            fallback = true;
            pick = MostOf(Enumerable.Range(0, nl).Where(l => l != pick));
        }
        if (pick >= 0) NotePierceChoice(attacker, gl, pick, tie, fallback, occ);
        return pick;
    }

    /// <summary>貫きの交互の記録（第202期・この戦闘だけ）。</summary>
    private readonly Dictionary<int, int> _pierceAlt = new();

    /// <summary>
    /// レーン上の生存者を前から後ろの順に並べる。
    /// 増援は死者の枠に入らなくなった（Summon 参照）ので通常は1枠1体だが、
    /// スロットの一意性は今後も前提にしないこと。ここが落ちると全戦闘が落ちる。
    /// </summary>
    private static List<UnitState> LaneOccupants(IEnumerable<UnitState> members, int lane, FormationShape shape)
    {
        var alive = members.Where(m => m.IsAlive).ToList();
        var line = new List<UnitState>();
        foreach (int slot in shape.LanePath(lane))
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
        // 第255期（被弾の燃焼）: 1回の攻撃の枠。札が無ければ比較1つで素通り。
        if (OpenBurnHitScope(actor))
        {
            try { PerformAttackImpact(actor, prefix, attackPercent, patternOverride); }
            finally { CloseBurnHitScope(); }
            return;
        }
        PerformAttackImpact(actor, prefix, attackPercent, patternOverride);
    }

    void PerformAttackImpact(UnitState actor, string prefix, int attackPercent, AttackPattern? patternOverride)
    {
        // 第230期: 撃破の衝撃の枠（ヨミの攻撃の中で倒した敵を控え、攻撃が終わってから解決する）。**保持者がいなければ比較1つで素通り。**
        if (_impactLive && actor.HasTrait(TraitId.KillImpact))
        {
            var frame = new List<(UnitState Dead, int Slot)>();
            _impactFrames.Push((actor, frame));
            try { PerformAttackEv(actor, prefix, attackPercent, patternOverride); }
            finally { _impactFrames.Pop(); }
            ResolveImpact(actor, frame);
            return;
        }
        PerformAttackEv(actor, prefix, attackPercent, patternOverride);
    }

    void PerformAttackEv(UnitState actor, string prefix, int attackPercent, AttackPattern? patternOverride)
    {
        // 第223期: 回避の枠（主目標が避けたら `OnAfterAttack` を走らせない）。**保持者がいなければ比較1つで本体へ直行する。**
        if (_evadeLive)
        {
            UnitState? prevEv = _evadedNow;
            _evadedNow = null;
            try { PerformAttackOuter(actor, prefix, attackPercent, patternOverride); }
            finally { _evadedNow = prevEv; }
            return;
        }
        PerformAttackOuter(actor, prefix, attackPercent, patternOverride);
    }

    void PerformAttackOuter(UnitState actor, string prefix, int attackPercent, AttackPattern? patternOverride)
    {
        // 第185期 追補4: 殴られて積もる据えの層を「1回の攻撃につき1層・攻撃が終わってから」にする枠。
        // **保持者がいなければ比較1つで本体へ直行する**（既存の行が 0 件差分であることの根拠）。
        // 第208期: 撃ち返す板の「1回の攻撃に何本返ったか」を数える枠（計数・表示専用）。保持者がいなければ比較1つで抜ける。
        if (_reboundLive)
        {
            _reflectFrames.Push(new ReflectFrame { Actor = actor, Serial = ++_attackSerial });
            try { PerformAttackFramed(actor, prefix, attackPercent, patternOverride); }
            finally { CloseReflectFrame(); }
            return;
        }
        PerformAttackFramed(actor, prefix, attackPercent, patternOverride);
    }

    // =====================================================================================
    // 第234期 —— 火の鎧（`FireArmorTrait`）・焼け残り（`SmolderTrait`）。**保持者がいなければ `_fireArmorLive` の比較1つで全部抜ける。**
    // =====================================================================================
    bool _fireArmorLive;
    /// <summary>第252期: ボルグが育つ口・ボルグとヒヨのあぶれた火の札（7枚のどれか）の持ち主がいる。**いなければ比較1つで全部抜ける。**</summary>
    bool _kindleLive;

    /// <summary>火の鎧の枠（1回の攻撃ごと）。殴られた保持者を控え、攻撃が終わってから火を点ける。</summary>
    sealed class FireArmorFrame
    {
        public required UnitState Actor { get; init; }
        public readonly List<UnitState> Hit = new();
        /// <summary>この攻撃で焼け残りが働いた保持者（自分には点けない）。</summary>
        public readonly List<UnitState> NoSelf = new();
    }
    readonly Stack<FireArmorFrame> _fireArmorFrames = new();

    /// <summary>いまの攻撃の枠（その主が <paramref name="source"/> のときだけ）。枠の外・他人の枠なら null。</summary>
    FireArmorFrame? FireArmorFrameOf(UnitState source)
        => _fireArmorFrames.Count > 0 && _fireArmorFrames.Peek().Actor == source ? _fireArmorFrames.Peek() : null;

    /// <summary>攻撃の枠が閉じた: 殴られた保持者ごとに、殴った敵と自分に火を点ける（乱数を引かない）。</summary>
    void ResolveFireArmor(FireArmorFrame fr)
    {
        foreach (UnitState b in fr.Hit)
        {
            UnitTally bt = TallyOf(b);
            if (fr.Actor.IsAlive)
            {
                bt.FireArmorFoeLit++;
                EmitFireArmor(b, fr.Actor, FireArmorLabels.Foe, 0);
                Log($"    {b.Name} の火の鎧が {fr.Actor.Name} に燃え移った", LogKind.Trigger);
                Ignite(fr.Actor, source: b);
            }
            if (b.IsAlive && !fr.NoSelf.Contains(b))
            {
                bt.FireArmorSelfLit++;
                EmitFireArmor(b, b, FireArmorLabels.Self, 0);
                Ignite(b, friendly: true, source: b);
            }
        }
    }

    /// <summary>火の鎧・焼け残りの台本（第234期・<b>表示専用</b>）。<c>verbose</c> のときだけ。</summary>
    void EmitFireArmor(UnitState holder, UnitState target, string label, int amount)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.FireArmor, Turn = _turn,
            ActorId = holder.InstanceId, TargetId = target.InstanceId,
            Amount = amount, HpAfter = Math.Max(0, target.Hp), Text = label,
        });
    }

    // =====================================================================================
    // 第242期 —— 火勢（燃え広がり・ヒヨのターンギフト・ホタの段）。**保持者（`FireLevelRule.Holds`）がいない陣営では何もしない**——
    // `_fireLvLive` と `_fireLvTeams[陣営]` の比較で抜ける（R0 が第241期と台本ごと一致する根拠）。**乱数を引かない。**
    // 火勢は私有カウンタ `FireLevelRule.LvKey`。読みは `FireLevelRule.Of` の1本。
    // =====================================================================================
    bool _fireLvLive;
    readonly bool[] _fireLvTeams = new bool[2];
    /// <summary>第244期 ②: 燃え広がりの上限（1回の攻撃で +1 まで）を持つ陣営。</summary>
    readonly bool[] _spreadCapTeams = new bool[2];
    /// <summary>第245期: 敵の火勢の門（保持者の相手の陣営）・刻みを火勢の回数にする陣営・脆さを火勢で上げる陣営・延焼の陣営。どれも札が無ければ偽のまま。</summary>
    bool _foeFireLive;
    /// <summary>第249期（刻み・一撃）: 札の持ち主がいる ／ いま刻む1回の量（0 なら <see cref="BurnRules.Damage"/>）。</summary>
    bool _tickOnceLive;
    int _burnTickAmt;

    // =====================================================================================
    // 第255期 —— 被弾の燃焼（札 `BurnHitAdd` / `BurnHitSplit` / `BurnHitSplitOnce` / `BurnHitFoeOnly`・保持者 0 枚）。
    // 燃えている駒が出どころのある一撃を受けるたび（その一撃より前から燃えていたときだけ）、燃焼の刻み 6 を火勢の回数だけ入れる。
    // 刻みの本体は `BurnTickOnce` をそのまま使う（熾火・火の癒し・ベニの反転・火の変換・燃えやすい板・脆さ・濃縮の印が刻みと同じに通る）。
    // **札の持ち主がいなければ `_burnHitLive` の比較1つで全部抜ける。乱数を引かない。**
    // =====================================================================================
    bool _burnHitLive;
    /// <summary>被弾の燃焼が起きる陣営 ／ ターン頭の燃焼の刻みを 6 × 1 にする陣営（分担）。</summary>
    readonly bool[] _burnHitTeams = new bool[2], _splitTickTeams = new bool[2];
    /// <summary>分担1: 1回の攻撃で同じ駒は1回まで。</summary>
    bool _burnHitOnce;
    /// <summary>いま被弾の燃焼を刻んでいる（入れ子の深さ）／ その一撃の主（台本の `ActorId`・ログ）。</summary>
    int _inBurnHit;
    UnitState? _burnHitBy;
    /// <summary>被弾の燃焼の帳簿（計数のみ）。</summary>
    public readonly BurnHitLedger BurnHitBook = new();
    public bool BurnHitLive => _burnHitLive;

    /// <summary>札の持ち主を盤に足すとき（`Add`）に門を立てる。足す・分担・分担1 は両陣営、敵だけは持ち主の相手の陣営だけ。</summary>
    void NoteBurnHitHolder(UnitState u)
    {
        if (u.HasTrait(TraitId.BurnHitAdd)) { _burnHitLive = true; _burnHitTeams[0] = _burnHitTeams[1] = true; }
        if (u.HasTrait(TraitId.BurnHitSplit) || u.HasTrait(TraitId.BurnHitSplitOnce))
        {
            _burnHitLive = true; _burnHitTeams[0] = _burnHitTeams[1] = true; _splitTickTeams[0] = _splitTickTeams[1] = true;
            if (u.HasTrait(TraitId.BurnHitSplitOnce)) _burnHitOnce = true;
        }
        if (u.HasTrait(TraitId.BurnHitCount)) _burnHitLive = true;   // 計数専用（門は立てない・機会だけ数える）
        if (u.HasTrait(TraitId.BurnHitFoeOnly))
        {
            int op = Opponent(u.TeamId);
            _burnHitLive = true; _burnHitTeams[op] = true; _splitTickTeams[op] = true;
        }
    }

    /// <summary>帳簿の添字（0 味方 ／ 1 敵）。</summary>
    static int BhSide(UnitState u) => u.TeamId == PlayerTeam ? 0 : 1;
    static string BhKey(UnitState u) => $"{BhSide(u)}:{u.Def.Id}";

    /// <summary>1回の攻撃の枠（手番の一振り全体、または手番の外の1回の攻撃）。分担1 の「同じ駒は1回まで」と跳ねの計数に使う。</summary>
    sealed class BurnHitScope
    {
        public required UnitState Actor { get; init; }
        public readonly HashSet<int> Hit = new();
        public int Fires;
    }
    readonly List<BurnHitScope> _burnHitScopes = new();

    /// <summary>枠を開く（いまの枠の主が <paramref name="actor"/> なら開かない・その枠を使う）。開いたら true。</summary>
    bool OpenBurnHitScope(UnitState actor)
    {
        if (!_burnHitLive) return false;
        if (_burnHitScopes.Count > 0 && _burnHitScopes[^1].Actor == actor) return false;
        _burnHitScopes.Add(new BurnHitScope { Actor = actor });
        return true;
    }
    void CloseBurnHitScope()
    {
        var sc = _burnHitScopes[^1];
        _burnHitScopes.RemoveAt(_burnHitScopes.Count - 1);
        if (sc.Fires > 0) BurnHitBook.PerScope[Math.Min(sc.Fires, 10)]++;
    }

    /// <summary>
    /// この一撃が被弾の燃焼の対象か（<c>ApplyDamageCore</c> が本体の前に読む）。出どころのある一撃で、
    /// 刻み・徴収・中継・呪いの共有・逸らしの受け渡し・放電・澱みの爆発・自分の一撃でなく、その一撃より前から燃えていること。
    /// </summary>
    bool BurnHitEligible(UnitState target, UnitState? source, bool burnTick, bool relayed, bool hexShare, bool levy)
    {
        if (source is null || source == target || burnTick || levy || relayed || hexShare) return false;
        if (_deflectFrom is not null || _shockNext == 3 || _burstHitNext) return false;
        if (!target.IsAlive || target.RawCounter(StatusKeys.Burn) <= 0) return false;
        return true;
    }

    /// <summary>
    /// 被弾の燃焼（第255期）。<paramref name="u"/> が <paramref name="by"/> の一撃を受けた直後に、燃焼の刻み（6）を火勢の回数だけ入れる
    /// （倒れたら止める・残りターンは減らさない・濃縮の印の回数も刻みと同じに足す）。<b>乱数を引かない。</b>
    /// </summary>
    void NoteBurnHitChance(UnitState u, UnitState by)
    {
        BurnHitLedger b = BurnHitBook;
        int side = BhSide(u), lv = Math.Max(1, FireLevelRule.Of(u));
        b.Chances[side]++; b.ChanceLv[side] += lv;
        string k = BhKey(by);
        if (!b.ChanceBy.TryGetValue(k, out var a)) b.ChanceBy[k] = a = new long[2];
        a[0]++; a[1] += lv;
    }

    void BurnOnHit(UnitState u, UnitState by)
    {
        BurnHitLedger b = BurnHitBook;
        int side = BhSide(u);
        BurnHitScope? sc = _burnHitScopes.Count > 0 && _burnHitScopes[^1].Actor == by ? _burnHitScopes[^1] : null;
        if (sc is not null)
        {
            if (!sc.Hit.Add(u.InstanceId))
            {
                if (_burnHitOnce) { b.Skipped[side]++; return; }
                b.Repeats[side]++;
            }
            sc.Fires++;
        }
        int lv = Math.Max(1, FireLevelRule.Of(u));
        int left = u.RawCounter(StatusKeys.Burn);
        UnitTally bt = TallyOf(u);
        int hb = u.Hp;
        int prevAmt = _burnTickAmt; _burnTickAmt = 0;
        UnitState? prevBy = _burnHitBy; _burnHitBy = by;
        _inBurnHit++;
        int marks = _markLive ? Math.Max(0, u.RawCounter(StatusKeys.Concentrated)) : 0;
        int total = lv + marks;
        int done = 0;
        try
        {
            for (int k = 0; k < lv && u.IsAlive; k++) { BurnTickOnce(u, left + 1, bt, second: k > 0, TickOrd(k + 1, total)); done++; }
            if (_markLive && u.IsAlive) RepeatTick(u, k => { BurnTickOnce(u, left + 1, bt, second: true, TickOrd(lv + k - 1, total)); done++; });
        }
        finally
        {
            _inBurnHit--;
            _burnHitBy = prevBy;
            _burnTickAmt = prevAmt;
        }
        int d = Math.Max(0, u.Hp) - hb;
        b.Fires[side]++; b.Units[side] += done;
        if (d < 0) b.HitDmg[side] -= d; else b.HitHeal[side] += d;
        if (!u.IsAlive) b.Kills[side]++;
        string ak = BhKey(by), tk = BhKey(u);
        if (!b.ByActor.TryGetValue(ak, out var av)) b.ByActor[ak] = av = new long[4];
        av[0]++; av[1] += done; if (d < 0) av[2] -= d; else av[3] += d;
        if (!b.ByTarget.TryGetValue(tk, out var tv)) b.ByTarget[tk] = tv = new long[3];
        tv[0]++; if (d < 0) tv[1] -= d; else tv[2] += d;
    }
    readonly bool[] _foeFireTeams = new bool[2], _lvTickTeams = new bool[2], _foeBrittleTeams = new bool[2], _foeSpreadTeams = new bool[2];
    /// <summary>第245期: いま燃焼の刻みを受けている駒と、残りターンを減らす前の火勢（最後の刻みは減らした後に刻むので `Of` は 0 を返す）。</summary>
    UnitState? _tickLvUnit;
    int _tickLv;
    /// <summary>火勢が動く陣営か（味方の火勢の門 ／ 第245期の敵の火勢の門）。</summary>
    bool LvTracked(int team) => _fireLvTeams[team] || _foeFireTeams[team];
    /// <summary>第245期: 刻みの間はその刻みの前の火勢、ほかは今の火勢。</summary>
    int LvNow(UnitState u) => _tickLvUnit == u ? _tickLv : FireLevelRule.Of(u);
    /// <summary>
    /// 燃焼の脆さの割合（第219期の `Ember.Brittle`・第245期に火勢で上げる口を足した）。脆さを上げる陣営の駒なら火勢 2〜4 で 40 / 55 / 70%
    /// （<see cref="FoeFireRule.BrittlePercent"/>）、ほかは `Ember.Brittle`。<paramref name="lv"/> は脆さを上げる陣営のときだけ火勢（ほかは 0）。
    /// </summary>
    int BrittlePct(UnitState u, out int lv)
    {
        lv = 0;
        if (!_foeFireLive || !_foeBrittleTeams[u.TeamId]) return Ember.Brittle;
        lv = LvNow(u);
        return lv >= 2 ? FoeFireRule.BrittlePercent[lv] : Ember.Brittle;
    }
    /// <summary>火勢の帳簿（計数のみ）。</summary>
    public readonly FireLevelLedger FireBook = new();
    public bool FireLvLive => _fireLvLive;

    /// <summary>燃え広がりの枠（手番の一振り全体、または手番の外の1回の攻撃）。敵ごとに「最初に当たった瞬間に、本人も敵も燃えていたか」を控える。</summary>
    sealed class SpreadScope
    {
        public required UnitState Actor { get; init; }
        public readonly List<(UnitState Foe, bool Counts)> First = new();
    }
    readonly List<SpreadScope> _spreadScopes = new();
    SpreadScope? SpreadScopeOf(UnitState actor)
    {
        for (int i = _spreadScopes.Count - 1; i >= 0; i--) if (_spreadScopes[i].Actor == actor) return _spreadScopes[i];
        return null;
    }

    /// <summary>火勢の攻撃の枠（1回の `PerformAttack`）。当てた敵（重複なし・当てた順）と、枠を開いた時点のホタの段。</summary>
    sealed class FireAtkFrame
    {
        public required UnitState Actor { get; init; }
        public int Stage { get; init; }
        /// <summary>第244期: 大技の一撃（当てた敵に保つ火を点ける・段に依らない）。</summary>
        public bool MoveIgnite { get; init; }
        /// <summary>第246期: 臨界の一撃（当てた敵の火勢 +1・着火の後）。</summary>
        public bool Critical { get; init; }
        public readonly List<UnitState> Hit = new();
    }
    readonly List<FireAtkFrame> _fireAtkFrames = new();
    /// <summary>ホタの 5連撃の的（直前の1発の主目標）。</summary>
    UnitState? _burstLock;
    FireAtkFrame? FireAtkFrameOf(UnitState actor)
    {
        for (int i = _fireAtkFrames.Count - 1; i >= 0; i--) if (_fireAtkFrames[i].Actor == actor) return _fireAtkFrames[i];
        return null;
    }

    /// <summary>この攻撃で当てた敵（火の粉・広が読む）。枠の外なら空。</summary>
    public IReadOnlyList<UnitState> FoesHitThisAttack(UnitState actor)
        => FireAtkFrameOf(actor)?.Hit ?? (IReadOnlyList<UnitState>)Array.Empty<UnitState>();

    /// <summary>攻撃が敵に当たる直前（`PerformAttackBody` の主目標・巻き込み、`ResolvePierce` の段）。</summary>
    void NoteFireContact(UnitState actor, UnitState hit)
    {
        if (!_fireLvTeams[actor.TeamId] || hit.TeamId == actor.TeamId) return;
        if (FireAtkFrameOf(actor) is FireAtkFrame fa && !fa.Hit.Contains(hit)) fa.Hit.Add(hit);
        if (_unleashHits is not null && !_unleashHits.Contains(hit)) _unleashHits.Add(hit);   // 第245期 追記 A
        if (SpreadScopeOf(actor) is not SpreadScope sc) return;
        foreach (var x in sc.First) if (x.Foe == hit) return;   // 同じ敵は1回まで（最初に当たった瞬間で決める）
        bool actorBurning = actor.RawCounter(StatusKeys.Burn) > 0, foeBurning = hit.RawCounter(StatusKeys.Burn) > 0;
        if (foeBurning && !actorBurning) FireBook.SpreadNotBurning++;
        sc.First.Add((hit, actorBurning && foeBurning));
    }

    /// <summary>燃え広がりの枠が閉じた: 当たる前から燃えていた敵の数だけ本人 +1。ヒヨ（煽りの札）はその数だけ自分 +1。</summary>
    void ResolveSpread(SpreadScope sc)
    {
        var foes = sc.First.Where(x => x.Counts).Select(x => x.Foe).ToList();
        if (foes.Count == 0) return;
        UnitState a = sc.Actor;
        FireBook.SpreadHits += foes.Count;
        foreach (UnitState f in foes) EmitFireLevel(a, f, FireLevelLabels.Spread, 0, 0);
        // 第245期: 敵の火勢——当たる前から燃えていた敵は、その攻撃で1回ずつ +1（味方の上限は掛けない）。札が無ければ比較1つで抜ける。
        if (_foeFireLive)
            foreach (UnitState f in foes)
            {
                if (!_foeFireTeams[f.TeamId] || !f.IsAlive) continue;
                FireBook.FoeGrowHits++;
                int b = FireLevelRule.Of(f);
                if (GrowFire(f, 1, a, FireLevelLabels.GrowFoe) && FireLevelRule.Of(f) > b) FireBook.FoeGrowth++;
            }
        // 第244期 ②: 上限の陣営では、育つのは1回の攻撃で +1 まで（ヒヨ自身の育ちも）。上限が無ければ相手の数だけ（第242期）。
        int n = _spreadCapTeams[a.TeamId] ? 1 : foes.Count;
        if (!GrowFire(a, n, a, FireLevelLabels.GrowSpread)) { FireBook.SpreadWasted += foes.Count; return; }
        FireBook.SpreadGrowth += n;
        FireBook.SpreadCapped += foes.Count - n;
        foreach (UnitState h in LivingMembers(a.TeamId))
            if (h != a && h.HasTrait(TraitId.FireStoke) && FireLevelRule.Of(h) > 0)
            {
                // 第245期 追記 B: ギフトで得た手番の攻撃の燃え広がりでは、札（`GiftQuiet`）を持つヒヨは育たない（撃った本人は育つ）。
                if (_giftTurnActor == a && h.HasTrait(TraitId.GiftQuiet)) { FireBook.GiftQuietSkipped += n; continue; }
                // 第246期（`HiyoSpark`）: 味方の燃え広がりでは育たない（煽りと火の粉で育つ）。
                if (h.HasTrait(TraitId.HiyoSpark)) { FireBook.SparkSpreadSkipped += n; continue; }
                FireBook.SelfGrowth += n;
                FireBook.SelfCapped += foes.Count - n;
                GrowFire(h, n, a, FireLevelLabels.GrowSelf);
            }
    }

    /// <summary>
    /// 育てる: 燃えていなければ捨てる（偽を返す）。燃えていれば「育った」の印を付け、上限 4 まで上げる（4 の上は捨てる・印は付ける）。
    /// </summary>
    bool GrowFire(UnitState u, int n, UnitState? cause, string label, bool emit = true)
    {
        if (!u.IsAlive || n <= 0 || !LvTracked(u.TeamId)) return false;
        int lv = FireLevelRule.Of(u);
        if (lv == 0) return false;
        u.SetCounter(FireLevelRule.GrewKey, _turn);
        // 第250期（あぶれた火・札 `PyreOverflow`）: 火勢4 のときに来た育ちは攻撃力 +4 に変わる（1回につき）。札が無ければ比較1つで抜ける。
        if (lv == FireLevelRule.Max && u.HasTrait(TraitId.PyreStage))   // 第250期 Phase 0（計数のみ）: 札が無くても機会を数える
        {
            string cid = cause?.Def.Id ?? "—";
            FireBook.OverflowChanceBy[cid] = FireBook.OverflowChanceBy.GetValueOrDefault(cid) + 1;
            if (u.HasTrait(TraitId.PyreOverflow)) FeedAtk(u, cause, FireFeedRule.OverflowAtk, overflow: true);
        }
        // 第252期: ボルグ・ヒヨのあぶれた火（溜め火・鎧の火・渡す火・癒しの灯）。機会は札が無くても数える（計数のみ）。札が無ければ比較1つで抜ける。
        if (lv == FireLevelRule.Max)
        {
            FireBook.MaxGrowChanceBy[u.Def.Id] = FireBook.MaxGrowChanceBy.GetValueOrDefault(u.Def.Id) + 1;
            if (_kindleLive) KindleOverflow(u, cause);
        }
        int to = Math.Min(FireLevelRule.Max, lv + n);
        if (to == lv) return true;
        u.SetCounter(FireLevelRule.LvKey, to);
        if (emit) EmitFireLevel(cause, u, label, to, lv);
        Log($"    {u.Name} の火勢が {to} に育った", LogKind.Status);
        return true;
    }

    /// <summary>保つ火（`Ignite` の出口）: 燃えていなかった駒なら 1。点け直しでは上げない。</summary>
    void FireKeepLit(UnitState target, UnitState? source, bool relit)
    {
        if (!LvTracked(target.TeamId)) return;
        if (relit && target.RawCounter(FireLevelRule.LvKey) > 0) { FireBook.Relit++; return; }
        FireBook.Lit++;
        target.SetCounter(FireLevelRule.LvKey, 1);
        target.SetCounter(FireLevelRule.GrewKey, 0);
        EmitFireLevel(source, target, FireLevelLabels.Lit, 1, 0);
    }

    /// <summary>燃焼が切れた（刻みの減算で 0・焼け残り）: 火勢 0。</summary>
    void FireOut(UnitState u)
    {
        if (!LvTracked(u.TeamId)) return;
        // 第247期（放熱・指名）: 燃えていなくなったら印は消える。**印が無ければ比較1つで抜ける。**
        if (u.RawCounter(FireCycleRule.CallKey) > 0)
        {
            u.SetCounter(FireCycleRule.CallKey, 0);
            FireBook.CallLost++;
            EmitFireLevel(null, u, FireLevelLabels.CallLost, 0, 0);
        }
        int lv = u.RawCounter(FireLevelRule.LvKey);
        if (lv <= 0) return;
        u.SetCounter(FireLevelRule.LvKey, 0);
        FireBook.Outs++;
        EmitFireLevel(null, u, FireLevelLabels.Out, 0, lv);
    }

    /// <summary>ターンの終わり: そのターン一度も育たなかった駒は −1（燃えている間は 1 未満にしない）。</summary>
    public void WiltFire()
    {
        if (!_fireLvLive) return;
        foreach (UnitState u in _units)
        {
            if (!u.IsAlive || !LvTracked(u.TeamId)) continue;
            int lv = FireLevelRule.Of(u);
            if (lv <= 1 || u.RawCounter(FireLevelRule.GrewKey) == _turn) continue;
            u.SetCounter(FireLevelRule.LvKey, lv - 1);
            FireBook.Wilts++;
            EmitFireLevel(null, u, FireLevelLabels.Wilt, lv - 1, lv);
        }
    }

    /// <summary>ターンの頭（刻みの後）の写し（計数のみ）。</summary>
    public void NoteFireLevelCensus()
    {
        if (!_fireLvLive) return;
        foreach (UnitState u in _units)
            if (u.IsAlive && LvTracked(u.TeamId))
                FireBook.Snaps.Add(new FireLevelLedger.Snap(_turn, u.InstanceId, u.TeamId, FireLevelRule.Of(u), u.RawCounter(StatusKeys.Burn) > 0));
    }

    /// <summary>攻撃の枠が閉じた: ホタの段 2 以上なら、この攻撃で当てた敵（生きている）に着火（保つ火）。</summary>
    void ResolveStageIgnite(FireAtkFrame fa)
    {
        if ((fa.Stage < 2 && !fa.MoveIgnite) || !fa.Actor.IsAlive) return;
        foreach (UnitState f in fa.Hit) if (f.IsAlive) Ignite(f, source: fa.Actor);
    }

    /// <summary>第246期: 臨界の出口——当てた敵（生きていて燃えている・敵の火勢の門の中）の火勢 +1（燃え広がりの育ちとは別）。</summary>
    void ResolveCritical(FireAtkFrame fa)
    {
        foreach (UnitState f in fa.Hit)
        {
            if (!f.IsAlive || !LvTracked(f.TeamId)) continue;
            FireBook.CriticalFoeHits++;
            int b = FireLevelRule.Of(f);
            if (GrowFire(f, 1, fa.Actor, FireLevelLabels.GrowCritical) && FireLevelRule.Of(f) > b) FireBook.CriticalFoeGrowth++;
        }
    }

    /// <summary>ホタの段（手番の頭・計数と台本だけ）。</summary>
    void NoteHotaStage(UnitState actor)
    {
        if (!actor.HasTrait(TraitId.PyreStage)) return;
        int st = FireLevelRule.Of(actor);
        FireBook.StageHands[st]++;
        EmitFireLevel(actor, actor, FireLevelLabels.Stage, st, st);
        // 第246期: 大火槍・臨界の手番（計数と見出し）。札が無ければ比較1つで抜ける。
        if (PyreStageTrait.IsCritical(actor)) { FireBook.Criticals++; EmitFireLevel(actor, actor, FireLevelLabels.Critical, st, st); }
        else if (PyreStageTrait.IsLance(actor)) { FireBook.Lances++; EmitFireLevel(actor, actor, FireLevelLabels.Lance, st, st); }   // 第247期 前段: 大火槍の見出し（段2 の火槍と区別）
    }

    /// <summary>火を保つ（計数のみ）。</summary>
    public void NoteFireKeep(UnitState u) => FireBook.FireKeeps++;
    public void NoteStokeNoTarget(UnitState u) => FireBook.StokeNoTarget++;
    public void NoteGiftNoTarget(UnitState u) => FireBook.GiftNoTarget++;
    /// <summary>第247期 (b): 火勢3 で準備のできた2体に渡す（計数と見出し）。</summary>
    public void NoteGiftPairChance(UnitState hiyo) => FireBook.GiftPairChance++;
    /// <summary>第248期: ギフトの手番に、印を持つが火勢4 未満で指名しなかったボルグがいた（計数のみ）。</summary>
    public void NoteCallHeld(UnitState hiyo) => FireBook.CallHeld++;
    public void NoteGiftPair(UnitState hiyo) { FireBook.GiftPairs++; EmitFireLevel(hiyo, hiyo, FireLevelLabels.GiftPair, FireLevelRule.Of(hiyo), 2); }

    /// <summary>煽り: 相手 +1、ヒヨが燃えていれば自分も +1。</summary>
    public void Stoke(UnitState hiyo, UnitState target)
    {
        FireBook.Stokes++;
        // 第246期（`StokePick`・表示と計数だけ）: 選んだ理由——「+1 で型が変わる」なら見出しを1件。
        if (hiyo.HasTrait(TraitId.StokePick))
        {
            if (FireStokeTrait.FormChanges(target)) { FireBook.StokeForm++; EmitFireLevel(hiyo, target, FireLevelLabels.StokeForm, FireLevelRule.Of(target), FireLevelRule.Of(target) + 1); }
            else FireBook.StokeFallback++;
        }
        EmitFireLevel(hiyo, target, FireLevelLabels.Stoke, FireLevelRule.Of(target), FireLevelRule.Of(target));
        Log($"    {hiyo.Name} が {target.Name} の火を煽った", LogKind.Trigger);
        GrowFire(target, 1, hiyo, FireLevelLabels.GrowStoke);
        if (FireLevelRule.Of(hiyo) > 0) { FireBook.SelfGrowth++; GrowFire(hiyo, 1, hiyo, FireLevelLabels.GrowSelf); }
    }

    readonly Queue<(UnitState Giver, UnitState To, int Ord)> _giftQueue = new();
    bool _inGift;

    /// <summary>ターンギフトを撃つ: 相手を控え、ヒヨの火勢を 1 に戻す。手番はヒヨの `TakeTurn` が返った後に渡す。</summary>
    public void QueueGift(UnitState hiyo, IReadOnlyList<UnitState> to, int level)
    {
        // 第252期（H1 渡す火）: 撃つときの溜め（札が無ければ 0）。相手1体ごとに相手の火勢を溜めの数だけ上げる（上限 4・あぶれた火にはならない）。
        int lift = _kindleLive && hiyo.HasTrait(TraitId.GiftHoard) ? hiyo.RawCounter(FireKindleRule.GiftHoardKey) : 0;
        // 第253期（渡す順・札 `GiftOrder`）: 2体に渡すとき、放つ（`FireUnleash`）の持ち主を先に（安定な並べ替え・乱数なし）。
        // 2体のギフトに放つの持ち主がいた回は、札が無くても数える（計数のみ）。
        if (to.Count == 2 && (to[0].HasTrait(TraitId.FireUnleash) || to[1].HasTrait(TraitId.FireUnleash)))
        {
            FireBook.OrderPairs++;
            if (to[0].HasTrait(TraitId.FireUnleash)) FireBook.OrderAlready++;
            else if (hiyo.HasTrait(TraitId.GiftOrder))
            {
                to = new[] { to[1], to[0] };
                FireBook.OrderSwapped++;
                EmitFireLevel(hiyo, to[0], FireLevelLabels.GiftOrder, FireLevelRule.Of(to[0]), 1);
                Log($"    {hiyo.Name} は先に {to[0].Name} へ火を渡す", LogKind.Trigger);
            }
        }
        FireBook.Gifts++;
        FireBook.GiftRecipients += to.Count;
        FireBook.GiftAtLevel[Math.Clamp(level, 0, 4)]++;
        if (FireBook.FirstGiftTurn == 0) FireBook.FirstGiftTurn = _turn;
        for (int i = 0; i < to.Count; i++)
        {
            // 第247期（放熱・指名）: 放熱の印を持つ相手なら、印を消して見出しを1件（`Amount` ＝ そのときの火勢）。印が無ければ比較1つで抜ける。
            // 第248期（`CallFull`）: 火勢4 未満の印のボルグは指名ではない——ギフトを受けても印は残す（計数だけ）。
            if (to[i].RawCounter(FireCycleRule.CallKey) > 0 && !FireStokeTrait.Nominated(to[i])) FireBook.CallHeldGift++;
            else if (to[i].RawCounter(FireCycleRule.CallKey) > 0)
            {
                to[i].SetCounter(FireCycleRule.CallKey, 0);
                int cl = FireLevelRule.Of(to[i]);
                FireBook.Called[Math.Clamp(cl, 0, 4)]++;
                EmitFireLevel(hiyo, to[i], FireLevelLabels.Called, cl, i + 1);
                Log($"    {hiyo.Name} は放熱の灯った {to[i].Name} に真っ先に火を渡す", LogKind.Trigger);
            }
            // 第246期（`StokePick`・表示と計数だけ）: 大技の準備ができた相手なら見出しを1件（`Slot` ＝ 何体目）。
            if (hiyo.HasTrait(TraitId.StokePick))
            {
                if (FireStokeTrait.BigMoveReady(to[i])) { FireBook.GiftReady++; EmitFireLevel(hiyo, to[i], FireLevelLabels.GiftReady, FireLevelRule.Of(to[i]), i + 1); }
                else FireBook.GiftNotReady++;
            }
            _giftQueue.Enqueue((hiyo, to[i], i + 1));
            if (_verbose) Emit(new BattleEvent
            {
                Kind = BattleEventKind.FireLevel, Turn = _turn, ActorId = hiyo.InstanceId, TargetId = to[i].InstanceId,
                Amount = level, Slot = i + 1, HpAfter = Math.Max(0, to[i].Hp), Text = FireLevelLabels.Gift,
            });
            if (lift > 0) LiftGift(hiyo, to[i], lift);
        }
        if (lift > 0)
        {
            hiyo.SetCounter(FireKindleRule.GiftHoardKey, 0);
            FireBook.GiftHoardGifts++;
            FireBook.GiftHoardSpent += lift;
        }
        Log($"    {hiyo.Name} が火を渡した（{string.Join("・", to.Select(u => u.Name))}）", LogKind.Highlight, hiyo);
        int lv = FireLevelRule.Of(hiyo);
        if (lv > 1)
        {
            hiyo.SetCounter(FireLevelRule.LvKey, 1);
            EmitFireLevel(hiyo, hiyo, FireLevelLabels.Spent, 1, lv);
        }
        CallFire(hiyo);   // 第244期（呼び火）: 札の持ち主がいなければ何もしない
    }

    /// <summary>控えた相手へ通常の手番を1回ずつ（`TakeTurn`・粛の窓口は通らない）。倒れていれば飛ばす。</summary>
    void DrainGifts()
    {
        _inGift = true;
        try
        {
            while (_giftQueue.Count > 0)
            {
                var (giver, to, ord) = _giftQueue.Dequeue();
                // 第297期（DH-t）: ドハが控えた手番。口（キュー・再入の止め）だけを共有し、火の帳簿・火のギフトの手番（大技の条件）には数えない。
                if (giver.HasTrait(TraitId.ShareGift))
                {
                    if (!to.IsAlive) { TallyOf(giver).ShareGiftSkipped++; continue; }
                    if (!TeamAlive(Opponent(to.TeamId))) { TallyOf(giver).ShareGiftSkipped += 1 + _giftQueue.Count; _giftQueue.Clear(); break; }
                    if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.ShareGive, Turn = _turn, ActorId = giver.InstanceId, TargetId = to.InstanceId, Amount = 0, Slot = 1, Text = ShareGiveLabels.GiftTurn, Team = to.TeamId });
                    Log($"  {to.Name} は {giver.Name} に背を押されて動く", LogKind.Highlight, to);
                    TurnOutcome so = TakeTurn(to);
                    UnitTally gt = TallyOf(to);
                    gt.ShareGiftTurns++;
                    if (so == TurnOutcome.Attack) gt.ShareGiftAttacks++;
                    continue;
                }
                if (!to.IsAlive) { FireBook.GiftTurnsSkipped++; continue; }
                if (!TeamAlive(Opponent(to.TeamId))) { FireBook.GiftTurnsSkipped += 1 + _giftQueue.Count; _giftQueue.Clear(); break; }
                FireBook.GiftTurns++;
                if (HushHolderAlive) FireBook.GiftHushTurns++;
                if (_verbose) Emit(new BattleEvent
                {
                    Kind = BattleEventKind.FireLevel, Turn = _turn, ActorId = giver.InstanceId, TargetId = to.InstanceId,
                    Amount = FireLevelRule.Of(to), Slot = ord, HpAfter = Math.Max(0, to.Hp), Text = FireLevelLabels.GiftTurn,
                });
                Log($"  {to.Name} は {giver.Name} から火を受け取って動く", LogKind.Highlight, to);
                UnitState? prevGift = _giftTurnActor;
                _giftTurnActor = to;   // 第244期（大技はギフトの手番だけ）
                TurnOutcome o;
                int mv0 = FireBook.Moves.Count;
                try { o = TakeTurn(to); }
                finally { _giftTurnActor = prevGift; }
                // 第252期（H1）: 渡す火で 4 に届いた相手が、このギフトの手番で大技（放つ・焼き尽くす）を撃った（**計数のみ**）。
                if (_giftLifted.Remove(to))
                    for (int k = mv0; k < FireBook.Moves.Count; k++)
                        if (FireBook.Moves[k].Id == to.InstanceId && FireBook.Moves[k].Kind is 1 or 2) { FireBook.GiftHoardBig++; break; }
                FireBook.GiftOutcome[(int)o]++;
                if (o == TurnOutcome.Stalled) FireBook.GiftStalled++;
                if (o == TurnOutcome.Attack && HushHolderAlive) FireBook.GiftHushAttacks++;
            }
        }
        finally { _inGift = false; _giftLifted.Clear(); }
    }

    // =====================================================================================
    // 第252期 —— ボルグが育つ口（守るほど燃え上がる）と、ボルグ・ヒヨのあぶれた火（溜め火・鎧の火 ／ 渡す火・癒しの灯）。
    // **札の持ち主がいなければ `_kindleLive` の比較1つで呼ばれない。乱数を引かない。**
    // =====================================================================================
    /// <summary>第252期（H1）: 渡す火で 4 に届いた相手（そのギフトの手番の大技を数えるだけ・**計数のみ**）。</summary>
    readonly HashSet<UnitState> _giftLifted = new();

    /// <summary>
    /// 守るほど燃え上がる（B1）: ボルグ（札の持ち主）が火の鎧（<paramref name="kind"/> 0）か盾の配り（1）で切った被ダメ <paramref name="saved"/> を累計し、
    /// 30 に達するごとに火勢 +1（`GrowFire`・火勢4 ならあぶれた火）。燃えていない間は数えない。
    /// </summary>
    void KindleGuard(UnitState b, int saved, int kind)
    {
        if (saved <= 0 || !b.HasTrait(TraitId.KindleGuard)) return;
        if (!b.IsAlive || b.RawCounter(StatusKeys.Burn) <= 0) { FireBook.GuardOff += saved; return; }
        FireBook.GuardSaved[kind] += saved;
        int acc = b.RawCounter(FireKindleRule.GuardKey) + saved;
        while (acc >= FireKindleRule.GuardStep && b.IsAlive)
        {
            acc -= FireKindleRule.GuardStep;
            FireBook.GuardSteps++;
            int lv = FireLevelRule.Of(b);
            EmitFireLevel(b, b, FireLevelLabels.KindleGuard, lv, acc);
            Log($"    {b.Name} は守るほどに燃え上がる", LogKind.Trigger);
            GrowFire(b, 1, b, FireLevelLabels.GrowGuard);
            if (FireLevelRule.Of(b) > lv) FireBook.GuardRaised++;
        }
        b.SetCounter(FireKindleRule.GuardKey, acc);
    }

    /// <summary>
    /// 火勢4 の駒に育ちが来た（`GrowFire`・1回の呼び出しにつき1回）: 溜め火（O1）・鎧の火（O2）・渡す火（H1）・癒しの灯（H2）。
    /// </summary>
    void KindleOverflow(UnitState u, UnitState? cause)
    {
        if (u.HasTrait(TraitId.BlazeHoard))
        {
            int h = u.RawCounter(FireKindleRule.HoardKey) + 1;
            u.SetCounter(FireKindleRule.HoardKey, h);
            FireBook.HoardAdds++;
            EmitFireLevel(cause, u, FireLevelLabels.Hoard, 1, h);
            Log($"    {u.Name} の大剣に火が溜まる（溜め {h}）", LogKind.Status);
        }
        if (u.HasTrait(TraitId.ArmorFlame))
        {
            TraitMark m = BeginTrait(TraitId.ArmorFlame, u);
            u.SetCounter(StatusKeys.Armor, u.RawCounter(StatusKeys.Armor) + FireKindleRule.ArmorFlame);
            EndTrait(m);
            FireBook.ArmorFlameN++;
            FireBook.ArmorFlameAmt += FireKindleRule.ArmorFlame;
            EmitFireLevel(cause, u, FireLevelLabels.ArmorFlame, FireKindleRule.ArmorFlame, u.RawCounter(StatusKeys.Armor));
            Log($"    {u.Name} の鎧に炎の破片がまとわりつく（破片 +{FireKindleRule.ArmorFlame}）", LogKind.Status);
        }
        if (u.HasTrait(TraitId.GiftHoard))
        {
            int h = u.RawCounter(FireKindleRule.GiftHoardKey);
            if (h >= FireKindleRule.GiftHoardMax) FireBook.GiftHoardCapped++;
            else
            {
                u.SetCounter(FireKindleRule.GiftHoardKey, h + 1);
                FireBook.GiftHoardAdds++;
                EmitFireLevel(cause, u, FireLevelLabels.GiftHoardAdd, 1, h + 1);
                Log($"    {u.Name} が渡す火を溜めた（{h + 1}）", LogKind.Status);
            }
        }
        if (u.HasTrait(TraitId.MendGlow)) MendGlow(u);
    }

    /// <summary>癒しの灯（H2）: 燃えている味方全員（自分を含む・席番号の順）を 4 回復（火の回復——ベニの反転の裏は通らず、渇きは素通り）。</summary>
    void MendGlow(UnitState hiyo)
    {
        // 第298期 段0-2（群3）: 癒しの灯はヒヨの出力（同じ関数の鎧の火は第252期から印を立てている）。
        int h0 = HealOutOf(hiyo);
        TraitMark am = BeginTrait(TraitId.MendGlow, hiyo);
        try { MendGlowCore(hiyo); }
        finally { AttrEnd(am, 3, hiyo, HealOutOf(hiyo) - h0); }
    }

    void MendGlowCore(UnitState hiyo)
    {
        var allies = LivingMembers(hiyo.TeamId).Where(a => a.RawCounter(StatusKeys.Burn) > 0).ToList();
        FireBook.MendGlowN++;
        EmitFireLevel(hiyo, hiyo, FireLevelLabels.MendGlow, FireKindleRule.MendGlow, allies.Count);
        Log($"    {hiyo.Name} から燃える味方へ小さな灯が飛ぶ", LogKind.Trigger);
        foreach (UnitState a in allies)
        {
            if (!a.IsAlive) continue;
            int before = a.Hp;
            Heal(a, FireKindleRule.MendGlow, hiyo, inverted: true, fireHeal: true);
            int g = Math.Max(0, a.Hp - before);
            FireBook.MendGlowNom += FireKindleRule.MendGlow;
            FireBook.MendGlowHp += g;
            EmitFireLevel(hiyo, a, FireLevelLabels.MendGlowHeal, g, FireKindleRule.MendGlow);
        }
    }

    /// <summary>渡す火（H1）: ギフトの相手の火勢を <paramref name="lift"/> だけ上げる（上限 4・燃えていなければ何もしない・既に 4 なら何もしない）。`GrowFire` を通さない（あぶれた火にならない）。</summary>
    void LiftGift(UnitState hiyo, UnitState to, int lift)
    {
        int b = FireLevelRule.Of(to);
        if (b <= 0) return;
        if (b >= FireLevelRule.Max) { FireBook.GiftHoardAt4++; return; }
        int a = Math.Min(FireLevelRule.Max, b + lift);
        to.SetCounter(FireLevelRule.LvKey, a);
        to.SetCounter(FireLevelRule.GrewKey, _turn);
        FireBook.GiftHoardRaised++;
        if (a == FireLevelRule.Max) { FireBook.GiftHoardTo4++; _giftLifted.Add(to); }
        EmitFireLevel(hiyo, to, FireLevelLabels.GiftHoard, a, b);
        Log($"    {hiyo.Name} が溜めた火を {to.Name} に渡す（火勢 {b} → {a}）", LogKind.Trigger);
    }

    // =====================================================================================
    // 第244期 —— 火勢の大技（放つ・焼き尽くす・残り火・呼び火）。**札の持ち主がいなければどれも呼ばれない**
    // （`BigMoveOf` は火勢を持つ陣営の `SwingTurn` の中でだけ・印と札の比較で 0 を返す）。乱数を引くのは火の雨の R だけ。
    // =====================================================================================
    /// <summary>いまギフトの手番の駒（`DrainGifts` の中だけ）。</summary>
    UnitState? _giftTurnActor;
    /// <summary>いま残り火を撃つ手番の駒（`TakeTurn` の入口で印を消したとき）。</summary>
    UnitState? _embersNow;
    /// <summary>大技の一撃の枠（当てた敵に保つ火）。</summary>
    bool _fireMoveIgnite;
    /// <summary>第245期 追記 A: 放つで当てた敵（`NoteFireContact` が控える・放つの間だけ非 null）。</summary>
    List<UnitState>? _unleashHits;

    /// <summary>この手番の大技: 0 なし ／ 1 放つ ／ 2 焼き尽くす ／ 3 残り火。残り火は段より先、放つ・焼き尽くすはギフトの手番で火勢4 のときだけ。</summary>
    int BigMoveOf(UnitState actor)
    {
        if (_embersNow == actor && actor.HasTrait(TraitId.PyreEmbers)) return 3;
        if (FireLevelRule.Of(actor) < FireLevelRule.Max) return 0;
        // 第250期（爆炎・独り・札 `BlazeSolo`）: 盤面に火を渡す者（ヒヨ）がいなければ、自分の手番の火勢4 で爆炎を撃つ。札が無ければ比較1つで抜ける。
        if (_giftTurnActor != actor)
            return actor.HasTrait(TraitId.BlazeSolo) && actor.HasTrait(TraitId.FireUnleash) && actor.HasTrait(TraitId.UnleashBlaze) && !GiverAlive(actor.TeamId) ? 1 : 0;
        if (actor.HasTrait(TraitId.FireUnleash)) return 1;
        if (actor.HasTrait(TraitId.PyreBurnout)) return 2;
        return 0;
    }

    void BigMove(UnitState actor, int move)
    {
        int lv = FireLevelRule.Of(actor);
        FireBook.Moves.Add((_turn, move, actor.InstanceId));
        if (move == 1 && _giftTurnActor != actor)   // 第250期（爆炎・独り）: ギフトでない手番の放つは独りの爆炎だけ
        {
            FireBook.BlazeSolos++;
            EmitFireLevel(actor, actor, FireLevelLabels.BlazeSolo, lv, 0);
            Log($"  {actor.Name} の火を渡す者はいない——独りで火を放つ", LogKind.Highlight, actor);
        }
        if (move is 1 or 2)
        {
            // 撃つ直前に火勢を 1 に戻す（燃え広がりはこの手番の枠の出口で数える＝撃った直後に 2 になりうる）。
            EmitFireLevel(actor, actor, move == 1 ? FireLevelLabels.Unleash : FireLevelLabels.Burnout, lv, lv);
            actor.SetCounter(FireLevelRule.LvKey, 1);
            EmitFireLevel(actor, actor, FireLevelLabels.Spent, 1, lv);
        }
        if (move == 1)
        {
            FireBook.Unleashes++;
            Log($"  {actor.Name} が溜めた火を放った（薙ぎ・攻 ×{FireBurstRule.UnleashPercent / 100}・当たった敵全員に火）", LogKind.Highlight, actor);
            // 第245期 追記 A: 放つで敵の火を育てる（札 `UnleashStoke`・敵の火勢の門が開いているときだけ）。当たる前の燃えていたかを先に控える。
            bool stoke = _foeFireLive && actor.HasTrait(TraitId.UnleashStoke);
            HashSet<UnitState>? wasBurning = null;
            if (stoke) { wasBurning = LivingMembers(Opponent(actor.TeamId)).Where(f => f.RawCounter(StatusKeys.Burn) > 0).ToHashSet(); _unleashHits = new List<UnitState>(); }
            // 第249期（爆炎・札 `UnleashBlaze`）: 薙ぎの代わりに敵全体 ×3。巻き込みは起こさず（`SplashTrait` が `BlazeActor` を見る）、続いて味方全体に燃焼ダメージ。
            bool blaze = actor.HasTrait(TraitId.UnleashBlaze);
            // 第254期（爆炎・上げ）: 当てた敵を控える（`NoteFireContact` が `_unleashHits` に足す）。札が無ければ控えない。
            int surge = blaze ? SurgeOf(actor) : 0;
            // 第257期（爆炎・敵上げ）: 敵の側に渡す量は別の1本（第254期の上げがあればそれ、無ければ敵上げの札）。味方には `surge` のまま。
            int foeSurge = blaze ? FoeSurgeOf(actor) : 0;
            if (foeSurge > 0 && _unleashHits is null) _unleashHits = new List<UnitState>();
            // 第252期（O1 溜め火）: 次の爆炎で、敵への倍率と味方への燃焼ダメージに溜め × 0.5 を足す。撃ったら 0。札が無ければ溜めは 0。
            int hoard = blaze && _kindleLive && actor.HasTrait(TraitId.BlazeHoard) ? actor.RawCounter(FireKindleRule.HoardKey) : 0;
            int unleashPct = FireBurstRule.UnleashPercent + hoard * FireKindleRule.HoardPercent;
            int blazeAmt = blaze ? Math.Max(0, actor.CurrentAttack) * (FireFinishRule.BlazeAllyPercent + hoard * FireKindleRule.HoardPercent) / 100 : 0;
            if (blaze && _kindleLive && actor.HasTrait(TraitId.BlazeHoard))
            {
                FireBook.HoardLog.Add((_turn, hoard, unleashPct));
                if (hoard > 0)
                {
                    actor.SetCounter(FireKindleRule.HoardKey, 0);
                    FireBook.HoardBlazes++;
                    FireBook.HoardSpent += hoard;
                    EmitFireLevel(actor, actor, FireLevelLabels.HoardRelease, hoard, unleashPct);
                    Log($"  {actor.Name} が溜めた火 {hoard} を解き放つ（敵全体 ×{unleashPct / 100.0:0.#}）", LogKind.Highlight, actor);
                }
            }
            if (blaze)
            {
                FireBook.Blazes++;
                EmitFireLevel(actor, actor, FireLevelLabels.Blaze, blazeAmt, 0);
                Log($"  {actor.Name} の火が爆ぜた——敵陣も味方も炎に包まれる（敵全体 ×{unleashPct / 100.0:0.#}・味方全体に燃焼 {blazeAmt}）", LogKind.Highlight, actor);
                BlazeActor = actor;
            }
            _fireMoveIgnite = true;
            try { PerformAttack(actor, attackPercent: unleashPct, patternOverride: blaze ? AttackPattern.All : AttackPattern.Sweep); }
            finally { _fireMoveIgnite = false; BlazeActor = null; }
            if (blaze)
            {
                // 第254期（爆炎・上げ）: 当てた敵のうち生き残った敵（席番号の順）の火勢を上げる。爆炎の瞬間の敵の火勢の分布は札が無くても数える（計数のみ）。
                List<UnitState>? hitFoes = foeSurge > 0 ? _unleashHits!.ToList() : null;
                if (foeSurge > 0 && !stoke) _unleashHits = null;
                var living = LivingMembers(Opponent(actor.TeamId));
                foreach (UnitState f in living) FireBook.BlazeFoeLvPre[FireLevelRule.Of(f)]++;
                if (hitFoes is not null)
                    foreach (UnitState f in hitFoes.OrderBy(x => x.Slot)) BlazeSurge(actor, f, foeSurge);
                foreach (UnitState f in living) FireBook.BlazeFoeLvPost[FireLevelRule.Of(f)]++;
            }
            if (blaze && actor.IsAlive)
            {
                _blazeSoloNow = _giftTurnActor != actor;   // 第250期（爆炎・独り）: 帳簿を分けるだけ
                try { BlazeAllies(actor, blazeAmt, surge); }
                finally { _blazeSoloNow = false; }
            }
            if (stoke)
            {
                var hits = _unleashHits!; _unleashHits = null;
                foreach (UnitState f in hits)
                {
                    if (!f.IsAlive || !_foeFireTeams[f.TeamId] || f.RawCounter(StatusKeys.Burn) <= 0) continue;
                    FireBook.UnleashFoeStoked++;
                    int b = FireLevelRule.Of(f);
                    if (wasBurning!.Contains(f))
                    {
                        GrowFire(f, 1, actor, FireLevelLabels.GrowFoe);   // 燃え広がりの +1 は手番の枠の出口で入る（合わせて +2）
                        if (FireLevelRule.Of(f) > b) FireBook.UnleashFoeRaised++; else FireBook.UnleashFoeAt4++;   // **計数のみ**
                        continue;
                    }
                    if (b >= FoeFireRule.SpreadLevel) continue;
                    FireBook.UnleashFoeRaised++;   // **計数のみ**
                    f.SetCounter(FireLevelRule.LvKey, FoeFireRule.SpreadLevel);
                    f.SetCounter(FireLevelRule.GrewKey, _turn);
                    EmitFireLevel(actor, f, FireLevelLabels.GrowFoe, FoeFireRule.SpreadLevel, b);
                }
            }
            UnleashSparks(actor);   // 第247期（火の粉・放つ）: 札の持ち主がいなければ何もしない
            CallFire(actor);
            return;
        }
        if (move == 2)
        {
            FireBook.Burnouts++;
            Log($"  {actor.Name} が焼き尽くす（全体 ×{(actor.HasTrait(TraitId.BurnoutHeavy) ? FireCycleRule.HeavyMultiplier : 4)} ＋ 火の雨 {FireBurstRule.RainDrops} 発）", LogKind.Highlight, actor);
            _fireMoveIgnite = true;
            try
            {
                actor.SetCounter(FireBurstRule.MoveKey, FireBurstRule.MoveBlast);
                PerformAttack(actor, patternOverride: AttackPattern.All);
                actor.SetCounter(FireBurstRule.MoveKey, FireBurstRule.MoveRain);
                bool ordered = actor.HasTrait(TraitId.FireRainOrdered);
                for (int i = 0; i < FireBurstRule.RainDrops; i++)
                {
                    if (!actor.IsAlive) break;
                    var foes = LivingMembers(Opponent(actor.TeamId));
                    if (foes.Count == 0) break;
                    // R: 生きている敵から一様（1体なら引かない＝`PickOne` の作法）／ D: その瞬間の HP が最も多い敵（同値は席の番号順）。
                    UnitState t = ordered
                        ? foes.OrderByDescending(f => f.Hp).ThenBy(f => f.Slot).First()
                        : foes.Count == 1 ? foes[0] : foes[Roll(foes.Count)];
                    FireBook.RainDrops++;
                    if (_verbose) Emit(new BattleEvent
                    {
                        Kind = BattleEventKind.FireLevel, Turn = _turn, ActorId = actor.InstanceId, TargetId = t.InstanceId,
                        Amount = foes.Count, Slot = i + 1, HpAfter = Math.Max(0, t.Hp), Text = FireLevelLabels.Rain,
                    });
                    _forcedTarget = t;
                    try { PerformAttack(actor, patternOverride: AttackPattern.Single); }
                    finally { _forcedTarget = null; }
                }
            }
            finally { actor.SetCounter(FireBurstRule.MoveKey, 0); _fireMoveIgnite = false; }
            if (actor.IsAlive && actor.HasTrait(TraitId.PyreEmbers)) actor.SetCounter(FireBurstRule.EmbersKey, 1);
            BurnoutEchoes(actor);   // 第246期（火の粉・放熱）: 札の持ち主がいなければ何もしない
            return;
        }
        // 残り火: 段の代わりに全体 ×2。敵が2体以下なら追加で全体 ×2。着火はしない（燃え広がりは通常どおり）。
        FireBook.Embers++;
        if (actor.HasTrait(TraitId.EmbersChain)) { EmbersChain(actor, lv); return; }   // 第249期（残り火・連撃）
        Log($"  {actor.Name} の残り火が燃え広がる（全体 ×2）", LogKind.Highlight, actor);
        try
        {
            actor.SetCounter(FireBurstRule.MoveKey, FireBurstRule.MoveEmbers);
            EmitFireLevel(actor, actor, FireLevelLabels.Embers, lv, 1);
            PerformAttack(actor, patternOverride: AttackPattern.All);
            int left = LivingMembers(Opponent(actor.TeamId)).Count;
            if (actor.IsAlive && left is > 0 and <= FireBurstRule.EmbersExtraFoes)
            {
                FireBook.EmbersExtra++;
                EmitFireLevel(actor, actor, FireLevelLabels.Embers, lv, 2);
                PerformAttack(actor, patternOverride: AttackPattern.All);
            }
        }
        finally { actor.SetCounter(FireBurstRule.MoveKey, 0); }
    }

    // =====================================================================================
    // 第249期 —— 爆炎（ボルグ）・残り火の連撃（ホタ）。**札の持ち主がいなければ呼ばれない。乱数を引かない。**
    // =====================================================================================
    /// <summary>第250期: 火を渡す者（ターンギフトの札の持ち主）が陣営に生きているか（爆炎・独りの条件）。</summary>
    bool GiverAlive(int team)
    {
        foreach (UnitState u in _units)
            if (u.TeamId == team && u.IsAlive && (u.HasTrait(TraitId.TurnGift) || u.HasTrait(TraitId.TurnGiftWait))) return true;
        return false;
    }

    /// <summary>
    /// 第250期: ホタの攻撃力の育ち（あぶれた火 ／ くべられる火）。`AtkBonus` に直に足す（自分の火で自分が強くなる札・強化の窓口を通さない）。
    /// 台本は `FireLevel` の見出し1件（<c>Slot</c> ＝ その戦の累計）。<b>乱数を引かない。</b>
    /// </summary>
    void FeedAtk(UnitState hota, UnitState? cause, int amount, bool overflow)
    {
        if (!hota.IsAlive) return;
        // 第298期 段0-2（群8）: 自己強化の直叩き（`NoteAtkMove` が印の主に数える）。印をホタの札に立てる。
        TraitMark am = BeginTrait(overflow ? TraitId.PyreOverflow : TraitId.PyreFed, hota);
        hota.AtkBonus += amount;
        AttrEnd(am, 8, hota, amount);
        string id = cause?.Def.Id ?? "—";
        long sum;
        if (overflow) { FireBook.OverflowN++; sum = FireBook.OverflowAtk += amount; FireBook.OverflowBy[id] = FireBook.OverflowBy.GetValueOrDefault(id) + 1; }
        else { FireBook.FedN++; sum = FireBook.FedAtk += amount; FireBook.FedBy[id] = FireBook.FedBy.GetValueOrDefault(id) + 1; }
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.FireLevel, Turn = _turn, ActorId = cause?.InstanceId, TargetId = hota.InstanceId,
            Amount = amount, Slot = (int)Math.Min(int.MaxValue, sum), HpAfter = Math.Max(0, hota.Hp), Text = overflow ? FireLevelLabels.Overflow : FireLevelLabels.Fed,
        });
        Log(overflow ? $"    {hota.Name} の火があぶれて刃に凝る（攻撃力 +{amount}）" : $"    {hota.Name} に火がくべられた（攻撃力 +{amount}）", LogKind.Status);
    }

    /// <summary>爆炎の敵への一撃の最中だけ非 null（<see cref="SplashTrait"/> が巻き込みを起こさない）。</summary>
    public UnitState? BlazeActor { get; private set; }
    /// <summary>爆炎の味方への燃焼ダメージの最中（ホタの火の癒しの帳簿の出どころ・<b>計数のみ</b>）。</summary>
    bool _blazeNow;
    /// <summary>第250期: いま配っている爆炎が独りの爆炎か（帳簿を分けるだけ・<b>計数のみ</b>）。</summary>
    bool _blazeSoloNow;

    /// <summary>
    /// 爆炎の味方の側（第249期）: 味方全体（ボルグ以外・席番号の順）に着火してから、燃焼ダメージ <paramref name="amount"/> を燃焼の規則どおりに配る
    /// ——火に焼かれない駒（熾のホタ・火の鎧）は受けず、火の癒し（ホタ・ボルグ）なら回復 ／ ベニの結界の内側は反転で回復 ／ 火の変換（ヒヨ）で回復 ／
    /// それ以外は味方の刃（<c>ApplyDamage(ally, amount, borg, isFriendlyFire: true)</c>・燃える巻き込みと同じ口）。
    /// </summary>
    void BlazeAllies(UnitState borg, int amount, int surge = 0)
    {
        foreach (UnitState ally in LivingMembers(borg.TeamId))
        {
            if (ally == borg || !ally.IsAlive) continue;
            EmitFireArmor(borg, ally, FireArmorLabels.BlazeAlly, amount);
            Ignite(ally, friendly: true, source: borg);
            if (!FireBook.BlazeById.TryGetValue(ally.Def.Id, out var row)) FireBook.BlazeById[ally.Def.Id] = row = new long[4];
            row[0] += amount;
            int h0 = ally.Hp;
            int kind;
            _blazeNow = true;
            try
            {
                if (Ember.Fireproof && (ally.HasTrait(TraitId.Pyre) || (_fireArmorLive && FireproofArmor(ally))))
                {
                    if (MendsFire(ally)) { kind = 0; FireHeal(ally, amount, FireArmorLabels.Mend, tick: false); }
                    else { kind = 4; Log($"    {ally.Name} は爆炎に焼かれない（{amount}）", LogKind.Status); }
                }
                else if (InvertsTick(ally) is UnitState beni) { kind = 2; InverseHeal(beni, ally, amount, 5, "爆炎"); }
                else if (_fireConvertHolders.Count > 0 && ally.RawCounter(StatusKeys.Burn) > 0 && FireConvertHolder(ally) is UnitState hiyo) { kind = 1; FireConvert(hiyo, ally, amount, tick: false); }
                else { kind = 3; ApplyDamage(ally, amount, borg, isFriendlyFire: true); }
            }
            finally { _blazeNow = false; }
            int d = Math.Max(0, ally.Hp) - h0;
            FireBook.BlazeNom[kind] += amount;
            FireBook.BlazeHp[kind] += Math.Abs(d);
            if (d > 0) row[1] += d; else row[2] -= d;
            if (!ally.IsAlive) { FireBook.BlazeAllyKills++; row[3]++; }
            if (_blazeSoloNow)
            {
                if (!FireBook.BlazeSoloById.TryGetValue(ally.Def.Id, out var sr)) FireBook.BlazeSoloById[ally.Def.Id] = sr = new long[4];
                sr[0] += amount; if (d > 0) sr[1] += d; else sr[2] -= d;
                FireBook.BlazeSoloNom[kind] += amount; FireBook.BlazeSoloHp[kind] += Math.Abs(d);
                if (!ally.IsAlive) { FireBook.BlazeSoloAllyKills++; sr[3]++; }
            }
            // 第254期（爆炎・上げ）: 燃焼ダメージ（か回復）の後に、生きていれば火勢を上げる。分布は札が無くても数える（計数のみ）。
            if (!ally.IsAlive) continue;
            int pre = FireLevelRule.Of(ally);
            FireBook.BlazeAllyLvPre[pre]++;
            if (surge > 0) BlazeSurge(borg, ally, surge);
            FireBook.BlazeAllyLvPost[FireLevelRule.Of(ally)]++;
            FireBook.BlazeAllyLog.Add((_turn, ally.InstanceId, pre, FireLevelRule.Of(ally)));
        }
    }

    /// <summary>第254期（爆炎・上げ）: 0 なし ／ 2 上げ2（`BlazeSurge2`）／ 4 上げ満（`BlazeSurgeMax`・両方持てば上げ満）。</summary>
    static int SurgeOf(UnitState borg) => borg.HasTrait(TraitId.BlazeSurgeMax) ? 4 : borg.HasTrait(TraitId.BlazeSurge2) ? 2 : 0;
    /// <summary>第257期（爆炎・敵上げ）: 敵の側の量。第254期の上げ（敵と味方）があればそれ、無ければ 4 敵上げ満（`BlazeFoeSurgeMax`）／ 2 敵上げ2（`BlazeFoeSurge2`）／ 0。
    /// 味方の側は今の <see cref="SurgeOf"/> のまま（敵上げの札だけなら 0＝味方は上げない）。</summary>
    static int FoeSurgeOf(UnitState borg) => SurgeOf(borg) is int s && s > 0 ? s
        : borg.HasTrait(TraitId.BlazeFoeSurgeMax) ? 4 : borg.HasTrait(TraitId.BlazeFoeSurge2) ? 2 : 0;

    /// <summary>
    /// 第254期（爆炎・上げ）: 爆炎で当たった駒（ボルグ以外）の火勢を上げる。上げは「育ち」（`GrowFire`）として通す——
    /// 上げ2 は +1 の育ちを2回（3 → 4 → あぶれた火1回 ／ 4 → あぶれた火2回）、上げ満は 4 に届くまでの育ち1回（既に 4 なら +1 の育ち1回＝あぶれた火1回）。
    /// 燃えていない駒・火勢が動かない陣営の駒には何もしない（`GrowFire` が捨てる）。台本は駒ごとに「爆炎・上げ」を1件（前後の火勢）。<b>乱数を引かない。</b>
    /// </summary>
    void BlazeSurge(UnitState borg, UnitState u, int surge)
    {
        if (u == borg || !u.IsAlive || !LvTracked(u.TeamId)) return;
        int b = FireLevelRule.Of(u);
        if (b == 0) return;
        bool foe = u.TeamId != borg.TeamId;
        long over0 = FireBook.OverflowN + FireBook.GiftHoardAdds + FireBook.GiftHoardCapped;
        if (surge >= FireLevelRule.Max) GrowFire(u, b < FireLevelRule.Max ? FireLevelRule.Max - b : 1, borg, FireLevelLabels.BlazeSurge, emit: false);
        else for (int i = 0; i < surge; i++) GrowFire(u, 1, borg, FireLevelLabels.BlazeSurge, emit: false);
        int a = FireLevelRule.Of(u);
        if (foe) { FireBook.SurgeFoe++; FireBook.SurgeFoeSteps += a - b; if (a == FireLevelRule.Max && b < a) FireBook.SurgeFoeTo4++; }
        else
        {
            FireBook.SurgeAlly++; FireBook.SurgeAllySteps += a - b; if (a == FireLevelRule.Max && b < a) FireBook.SurgeAllyTo4++;
            FireBook.SurgeAllyOver += FireBook.OverflowN + FireBook.GiftHoardAdds + FireBook.GiftHoardCapped - over0;
        }
        // 第257期: 敵上げの札だけ（第254期の上げが無い）なら見出しは「爆炎・敵上げ」——敵の火が一斉に跳ね上がる瞬間（表示専用）。
        EmitFireLevel(borg, u, foe && SurgeOf(borg) == 0 ? FireLevelLabels.BlazeFoeSurge : FireLevelLabels.BlazeSurge, a, b);
        Log(a > b ? $"    爆炎で {u.Name} の火勢が {b} → {a} に跳ね上がる" : $"    爆炎の火が {u.Name} にあふれる（火勢 {a} のまま）", LogKind.Status);
    }

    /// <summary>
    /// 残り火の連撃（第249期）: 全体 ×2 の代わりに <see cref="FireFinishRule.EmbersHits"/> 発の単体 ×2（大技の一撃なので当てた敵に保つ火）。
    /// 主目標は前列（攻撃の標的になりうる列）の席番号の最初の敵、そこから生きている敵を席番号の順に1発ずつ巡回する（敵が1体なら全部その敵）。<b>乱数を引かない。</b>
    /// </summary>
    void EmbersChain(UnitState actor, int lv)
    {
        FireBook.EmbersChains++;
        Log($"  {actor.Name} の残り火が{FireFinishRule.EmbersHits}発の燃えさしになって降る（1発 ×2・着火）", LogKind.Highlight, actor);
        var hit = new HashSet<UnitState>();
        try
        {
            actor.SetCounter(FireBurstRule.MoveKey, FireBurstRule.MoveEmbers);
            EmitFireLevel(actor, actor, FireLevelLabels.Embers, lv, 1);
            var foes0 = LivingMembers(Opponent(actor.TeamId)).ToList();
            var pool = PoolOf(foes0);
            int cursor = (pool.Count > 0 ? pool : foes0).Select(f => f.Slot).DefaultIfEmpty(0).Min();
            for (int k = 1; k <= FireFinishRule.EmbersHits; k++)
            {
                if (!actor.IsAlive) break;
                var foes = LivingMembers(Opponent(actor.TeamId)).OrderBy(f => f.Slot).ToList();
                if (foes.Count == 0) break;
                UnitState t = foes.FirstOrDefault(f => f.Slot >= cursor) ?? foes[0];
                cursor = t.Slot + 1;
                hit.Add(t);
                FireBook.EmbersChainHits++;
                if (_verbose) Emit(new BattleEvent
                {
                    Kind = BattleEventKind.FireLevel, Turn = _turn, ActorId = actor.InstanceId, TargetId = t.InstanceId,
                    Amount = foes.Count, Slot = k, HpAfter = Math.Max(0, t.Hp), Text = FireLevelLabels.EmbersHit,
                });
                _fireMoveIgnite = true;
                _forcedTarget = t;
                try { PerformAttack(actor, patternOverride: AttackPattern.Single); }
                finally { _forcedTarget = null; _fireMoveIgnite = false; }
            }
        }
        finally { actor.SetCounter(FireBurstRule.MoveKey, 0); }
        FireBook.EmbersChainDistinct += hit.Count;
    }

    /// <summary>
    /// 延焼（第245期）: 倒れた瞬間の火勢が 4 なら、同じ陣営の隣の生きている駒（席番号の順）に火を移す——燃えていれば +1（上限 4）、
    /// 燃えていなければ点けて火勢 2（その周回は萎まない）。延焼では誰も倒れないので連鎖しない。<b>乱数を引かない。</b>
    /// </summary>
    void FoeFireSpread(UnitState dead)
    {
        if (LvNow(dead) < FireLevelRule.Max) return;
        FireBook.FoeLv4Deaths++;
        Log($"    {dead.Name} の燃え盛る火が隣へ移る", LogKind.Highlight, dead);
        foreach (UnitState n in LivingMembers(dead.TeamId).Where(x => x != dead && FormationRules.AreAdjacent(dead, x)).OrderBy(x => x.Slot))
        {
            bool burning = n.RawCounter(StatusKeys.Burn) > 0;
            FireBook.FoeSpreads++;
            EmitFireLevel(dead, n, FireLevelLabels.FoeSpread, FireLevelRule.Of(n), burning ? 2 : 1);
            if (burning) { FireBook.FoeSpreadGrow++; GrowFire(n, 1, dead, FireLevelLabels.GrowFoe); continue; }
            FireBook.FoeSpreadLit++;
            Ignite(n, source: dead);
            if (!n.IsAlive || n.RawCounter(StatusKeys.Burn) <= 0) continue;
            int b = FireLevelRule.Of(n);
            n.SetCounter(FireLevelRule.LvKey, FoeFireRule.SpreadLevel);
            n.SetCounter(FireLevelRule.GrewKey, _turn);
            EmitFireLevel(dead, n, FireLevelLabels.GrowFoe, FoeFireRule.SpreadLevel, b);
        }
    }

    /// <summary>
    /// 第246期: 焼き尽くすの返し——味方の火の粉の持ち主（`HiyoSpark`・燃えている）は火勢 +1、放熱の持ち主（`BorgRadiate`・燃えている）は放熱を1つ蓄える（重ねない）。
    /// <b>乱数を引かない。</b>席の番号の順。
    /// </summary>
    void BurnoutEchoes(UnitState hota)
    {
        foreach (UnitState h in LivingMembers(hota.TeamId))
        {
            if (h == hota || FireLevelRule.Of(h) == 0) continue;
            if (h.HasTrait(TraitId.HiyoSpark) || h.HasTrait(TraitId.SparkCatch))   // 第247期: 旧育ちの火の粉（`SparkCatch`）も同じ口
            {
                FireBook.Sparks++;
                int b = FireLevelRule.Of(h);
                EmitFireLevel(hota, h, FireLevelLabels.Spark, b, b);
                GrowFire(h, 1, hota, FireLevelLabels.GrowSpark);
                FireBook.SparkGrowth += FireLevelRule.Of(h) - b;
                FireBook.SparkBy[0]++; FireBook.SparkGrowBy[0] += FireLevelRule.Of(h) - b;
            }
            if (h.HasTrait(TraitId.RadiateCall))   // 第247期（放熱・指名）: 燃えている間だけ印（重ねない）
            {
                if (h.RawCounter(FireCycleRule.CallKey) > 0) FireBook.CallStacked++;
                else
                {
                    FireBook.CallMarks++;
                    h.SetCounter(FireCycleRule.CallKey, 1);
                    EmitFireLevel(hota, h, FireLevelLabels.CallMark, FireLevelRule.Of(h), 1);
                    Log($"    {h.Name} の鎧に {hota.Name} の放熱が灯った（次の火は自分に）", LogKind.Trigger);
                }
            }
            if (h.HasTrait(TraitId.RadiateGrow))   // 第252期（B3 放熱で育つ）: その場で +1（放熱の印・指名はそのまま）
            {
                FireBook.RadiateGrowN++;
                int b = FireLevelRule.Of(h);
                EmitFireLevel(hota, h, FireLevelLabels.RadiateGrow, b, b);
                GrowFire(h, 1, hota, FireLevelLabels.GrowRadiateNow);
                if (FireLevelRule.Of(h) > b) FireBook.RadiateGrowRaised++;
            }
            if (h.HasTrait(TraitId.BorgRadiate))
            {
                if (h.RawCounter(FireCycleRule.RadiateKey) > 0) { FireBook.RadiateStacked++; continue; }
                FireBook.Radiates++;
                h.SetCounter(FireCycleRule.RadiateKey, 1);
                EmitFireLevel(hota, h, FireLevelLabels.Radiate, FireLevelRule.Of(h), 1);
                Log($"    {h.Name} が {hota.Name} の放熱を受け止めた", LogKind.Trigger);
            }
        }
    }

    /// <summary>第247期: 放つの火の粉——味方の `SparkUnleash` の持ち主（燃えている・放った本人以外）は火勢 +1。<b>乱数を引かない。</b>席の番号の順。</summary>
    void UnleashSparks(UnitState borg)
    {
        foreach (UnitState h in LivingMembers(borg.TeamId))
        {
            if (h == borg || FireLevelRule.Of(h) == 0 || !h.HasTrait(TraitId.SparkUnleash)) continue;
            FireBook.Sparks++;
            int b = FireLevelRule.Of(h);
            EmitFireLevel(borg, h, FireLevelLabels.Spark, b, b);
            GrowFire(h, 1, borg, FireLevelLabels.GrowSpark);
            FireBook.SparkGrowth += FireLevelRule.Of(h) - b;
            FireBook.SparkBy[1]++; FireBook.SparkGrowBy[1] += FireLevelRule.Of(h) - b;
        }
    }

    /// <summary>第246期: 放熱を使う（通常の手番の頭）。燃えていれば火勢 +1、燃えていなければ捨てる。</summary>
    void UseRadiate(UnitState u)
    {
        u.SetCounter(FireCycleRule.RadiateKey, 0);
        int b = FireLevelRule.Of(u);
        EmitFireLevel(u, u, FireLevelLabels.RadiateUse, b, b);
        if (b > 0 && GrowFire(u, 1, u, FireLevelLabels.GrowRadiate)) { FireBook.RadiateUsed++; FireBook.RadiateGrowth += FireLevelRule.Of(u) - b; }
        else FireBook.RadiateWasted++;
    }

    /// <summary>呼び火: 味方の呼び火の持ち主（燃えている・生きている・本人以外）の火勢 +1（上限 4）。</summary>
    void CallFire(UnitState caller)
    {
        foreach (UnitState h in LivingMembers(caller.TeamId))
        {
            if (h == caller || !h.HasTrait(TraitId.CallFire) || FireLevelRule.Of(h) == 0) continue;
            FireBook.CallFires++;
            int before = FireLevelRule.Of(h);
            EmitFireLevel(caller, h, FireLevelLabels.CallFire, before, before);
            GrowFire(h, 1, caller, FireLevelLabels.GrowCall);
            FireBook.CallGrowth += FireLevelRule.Of(h) - before;
        }
    }

    /// <summary>火勢の台本（表示専用・verbose のときだけ）。</summary>
    void EmitFireLevel(UnitState? actor, UnitState target, string label, int amount, int slot)
    {
        if (!_verbose) return;
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.FireLevel, Turn = _turn, ActorId = actor?.InstanceId, TargetId = target.InstanceId,
            Amount = amount, Slot = slot, HpAfter = Math.Max(0, target.Hp), Text = label,
        });
    }

    // =====================================================================================
    // 第235期 —— 燃える巻き込み（S）・くすぶり（O）・火の癒し（H1）・焼き返し（H2）。どれも乱数を引かない。
    // 台本は `FireArmor` の種類に札（`FireArmorLabels`）を足しただけ（表示専用・verbose のときだけ）。
    // =====================================================================================

    /// <summary>火の鎧か火の癒しを持つか（「火に焼かれない」の判定・呼び出し側で <c>_fireArmorLive</c> を先に見る）。</summary>
    static bool FireproofArmor(UnitState u) => u.HasTrait(TraitId.FireArmor) || u.HasTrait(TraitId.FireMend);
    /// <summary>「焼かれない」の枝で、燃焼ダメージを同じ量の火の回復に変えるか（第235期 ボルグの火の癒し ／ 第249期 ホタの火の癒し）。呼ぶのは「焼かれない」の枝の中だけ。</summary>
    bool MendsFire(UnitState u) => (_fireArmorLive && u.HasTrait(TraitId.FireMend)) || u.HasTrait(TraitId.PyreMend);

    /// <summary>
    /// 燃える巻き込み（第235期・S）の1体ぶん。<b>量は巻き込みと同じ</b>で、燃焼の刻みと同じ順に分ける——
    /// 火に焼かれない駒（熾のホタ・火の鎧）は受けない（火の癒しなら回復）／ベニの結界の内側では回復に反転（<see cref="InverseHeal"/> の kind 5）／
    /// それ以外は<b>今までと同じ味方の刃</b>（<c>ApplyDamage(ally, spill, borg, isFriendlyFire: true)</c>）なので、脆さ（規定は敵だけ）・巨躯・分かち・破片・身構え・軛の順と、
    /// 被弾で動く札の反応は巻き込みのまま。乱数を引かない。
    /// </summary>
    public void FireSplashHit(UnitState ally, int spill, UnitState borg)
    {
        UnitTally t = TallyOf(borg);
        t.FireSplashHits++; t.FireSplashNominal += spill;
        EmitFireArmor(borg, ally, FireArmorLabels.Splash, spill);
        bool conv = _fireConvertHolders.Count > 0 && ally.RawCounter(StatusKeys.Burn) > 0;   // 第238期: 火の変換は燃えている味方だけ
        if (Ember.Fireproof && (ally.HasTrait(TraitId.Pyre) || (_fireArmorLive && FireproofArmor(ally))))
        {
            if (conv) NoteConvertPrec(ally, spill, MendsFire(ally) ? 1 : 0);   // **計数のみ**
            t.FireSplashImmune += spill;
            Log($"    {ally.Name} は燃える巻き込みに焼かれない（{spill}）", LogKind.Status);
            if (MendsFire(ally)) FireHeal(ally, spill, FireArmorLabels.Mend, tick: false);   // 第249期: ホタの火の癒しも
            return;
        }
        if (InvertsTick(ally) is UnitState beni)
        {
            if (conv) NoteConvertPrec(ally, spill, 2);   // 第238期・**計数のみ**
            int h0 = ally.Hp;
            InverseHeal(beni, ally, spill, 5, "燃える巻き込み");
            t.FireSplashInverted += spill; t.FireSplashInvHealed += Math.Max(0, ally.Hp - h0);
            return;
        }
        if (conv && FireConvertHolder(ally) is UnitState hiyo)
        {
            FireConvert(hiyo, ally, spill, tick: false);   // 第238期: 火の変換（燃えている味方には薬）
            return;
        }
        int b0 = ally.Hp;
        ApplyDamage(ally, spill, borg, isFriendlyFire: true);
        t.FireSplashTaken += b0 - Math.Max(0, ally.Hp);
    }

    // =====================================================================================
    // 第238期 —— 盾の配り（ボルグ・D1 隣 ／ D2 全員）・火の変換（ヒヨ・V1 全量 ／ V2 半分）。どれも乱数を引かない。
    // 保持者がいなければ `_fireWardHolders` / `_fireConvertHolders` の件数の比較1つで抜ける。台本は `FireArmor` の札（`Ward` / `Convert`）。
    // =====================================================================================
    readonly List<UnitState> _fireWardHolders = new();
    readonly List<UnitState> _fireConvertHolders = new();

    /// <summary>燃えている味方 <paramref name="u"/> に働く火の変換の保持者（同じ陣営で生きている・最初の1枚）。</summary>
    UnitState? FireConvertHolder(UnitState u)
    {
        foreach (UnitState h in _fireConvertHolders)
            if (h.IsAlive && h.TeamId == u.TeamId) return h;
        return null;
    }

    /// <summary>盾の配りの保持者（同じ陣営で生きていて、D1 なら <paramref name="u"/> の隣）。保持者自身は対象外。</summary>
    UnitState? FireWardHolder(UnitState u)
    {
        foreach (UnitState h in _fireWardHolders)
            if (h.IsAlive && h != u && h.TeamId == u.TeamId && (h.HasTrait(TraitId.FireWardAll) || FormationRules.AreAdjacent(h, u))) return h;
        return null;
    }

    /// <summary>
    /// 火の変換（第238期・V）。燃焼ダメージ <paramref name="amount"/> を受ける代わりに、同じ量（V2 は半分・切り捨て）の火の回復
    /// （<c>Heal(…, inverted: true, fireHeal: …)</c>＝ベニの反転の裏を通らず、渇きは <see cref="TraitId.FireConvertDry"/> を持たなければ素通り）。
    /// 溢れた分は捨てる。支援拒否と上限は <c>Heal</c> がそのまま守る。
    /// </summary>
    void FireConvert(UnitState hiyo, UnitState u, int amount, bool tick)
    {
        // 第298期 段0-2（群2）: 火の変換の回復はヒヨの出力。
        int h0 = HealOutOf(hiyo);
        TraitMark am = BeginTrait(TraitId.FireConvert, hiyo);
        try { FireConvertCore(hiyo, u, amount, tick); }
        finally { AttrEnd(am, 2, hiyo, HealOutOf(hiyo) - h0); }
    }

    void FireConvertCore(UnitState hiyo, UnitState u, int amount, bool tick)
    {
        UnitTally t = TallyOf(hiyo);
        t.FireConvPrecV += amount;
        EmitFireArmor(hiyo, u, FireArmorLabels.Convert, amount);
        int heal = hiyo.HasTrait(TraitId.FireConvert) ? amount : amount / 2;
        int before = u.Hp;
        HealOutcome res = heal > 0 ? Heal(u, heal, hiyo, inverted: true, fireHeal: !hiyo.HasTrait(TraitId.FireConvertDry)) : HealOutcome.None;
        int g = Math.Max(0, u.Hp - before);
        if (res == HealOutcome.Drought) t.FireConvDry++;
        if (tick) { t.FireConvTickNominal += amount; t.FireConvTickHealed += g; }
        else { t.FireConvSplashNominal += amount; t.FireConvSplashHealed += g; }
        Log($"    {hiyo.Name} の火が {u.Name} の燃焼を癒しに変えた（{amount} → +{g}）", LogKind.Status);
    }

    /// <summary>優先順位の内訳（<b>計数のみ</b>）: 火の変換の保持者がいるとき、燃えている味方の燃焼ダメージがどの規則に回ったか。0 焼かれない ／ 1 火の癒し ／ 2 ベニの結界。</summary>
    void NoteConvertPrec(UnitState u, int amount, int kind)
    {
        if (FireConvertHolder(u) is not UnitState h) return;
        UnitTally t = TallyOf(h);
        if (kind == 0) t.FireConvPrecPyre += amount; else if (kind == 1) t.FireConvPrecMend += amount; else t.FireConvPrecBeni += amount;
    }

    /// <summary>ターン頭の燃える味方の数（<b>計数のみ</b>・ボルグ＝火の鎧か盾の配りの保持者ごと）。</summary>
    void NoteFireWardCensus()
    {
        foreach (UnitState b in _units)
        {
            if (!b.IsAlive || !(b.HasTrait(TraitId.FireArmor) || b.HasTrait(TraitId.FireWard) || b.HasTrait(TraitId.FireWardAll))) continue;
            UnitTally t = TallyOf(b);
            t.WardCensusTurns++;
            foreach (UnitState a in _units)
            {
                if (a == b || !a.IsAlive || a.TeamId != b.TeamId || a.RawCounter(StatusKeys.Burn) <= 0) continue;
                t.WardBurnAllies++;
                if (FormationRules.AreAdjacent(b, a)) t.WardBurnAdj++;
            }
        }
    }

    /// <summary>くすぶり（第235期・O）。開戦時に自分に火（通常の着火・残り 3）。</summary>
    public void SelfKindle(UnitState u)
    {
        if (!u.IsAlive) return;
        TallyOf(u).SelfKindleLit++;
        EmitFireArmor(u, u, FireArmorLabels.Kindle, 0);
        Log($"    {u.Name} の身体がくすぶり始めた", LogKind.Trigger);
        Ignite(u, friendly: true, source: u);
        // 第252期（B2 開幕の火勢）: くすぶりの火を火勢2 から（その周回は萎まない＝延焼と同じ扱い）。札が無ければ比較1つで抜ける。
        if (_kindleLive && u.HasTrait(TraitId.KindleOpen) && LvTracked(u.TeamId))
        {
            int b = FireLevelRule.Of(u);
            if (b > 0 && b < FireKindleRule.OpenLevel)
            {
                u.SetCounter(FireLevelRule.LvKey, FireKindleRule.OpenLevel);
                u.SetCounter(FireLevelRule.GrewKey, Math.Max(1, _turn));
                FireBook.OpenLv++;
                EmitFireLevel(u, u, FireLevelLabels.KindleOpen, FireKindleRule.OpenLevel, b);
                Log($"    {u.Name} のくすぶりは初めから強い（火勢 {FireKindleRule.OpenLevel}）", LogKind.Status);
            }
        }
    }

    /// <summary>焼き返し（第235期・H2）。殴る前から燃えていた主目標を殴った: 与えた量の半分を回復し、自分に火。</summary>
    public void FireFeed(UnitState self, UnitState target, int amount)
    {
        if (!self.IsAlive) return;
        UnitTally t = TallyOf(self);
        t.FireFeedFires++;
        Log($"    {self.Name} が {target.Name} の火を焼き返した", LogKind.Trigger);
        FireHeal(self, amount, FireArmorLabels.Feed, tick: false);
        Ignite(self, friendly: true, source: self);
    }

    /// <summary>
    /// 火の回復（第235期・H1 / H2）。<see cref="Heal"/> を <c>inverted: true</c>（ベニの反転の裏でダメージに化けない）で通し、
    /// <b>渇きは <see cref="TraitId.FireMendDry"/> を持たなければ素通り</b>（H1a）。上限は <c>Heal</c> がそのまま守る。
    /// </summary>
    public void FireHeal(UnitState u, int amount, string label, bool tick)
    {
        // 第298期 段0-2（群4）: 火の癒し（焼き返し・熾の癒し）は本人の札の出力。
        if (!u.IsAlive || amount <= 0) return;
        int h0 = HealOutOf(u);
        TraitMark am = BeginTrait(label == FireArmorLabels.Feed ? TraitId.FireFeed : u.HasTrait(TraitId.PyreMend) ? TraitId.PyreMend : TraitId.FireMend, u);
        try { FireHealCore(u, amount, label, tick); }
        finally { AttrEnd(am, 4, u, HealOutOf(u) - h0); }
    }

    void FireHealCore(UnitState u, int amount, string label, bool tick)
    {
        if (!u.IsAlive || amount <= 0) return;
        UnitTally t = TallyOf(u);
        EmitFireArmor(u, u, label, amount);
        int before = u.Hp;
        HealOutcome res = Heal(u, amount, u, inverted: true, fireHeal: !u.HasTrait(TraitId.FireMendDry));
        int g = Math.Max(0, u.Hp - before);
        if (label == FireArmorLabels.Feed) { t.FireFeedNominal += amount; t.FireFeedHealed += g; if (res == HealOutcome.Drought) t.FireFeedDry++; }
        else
        {
            t.FireMendNominal += amount; t.FireMendHealed += g; if (res == HealOutcome.Drought) t.FireMendDry++;
            if (!tick) t.FireMendSplash += g;
            if (u.HasTrait(TraitId.PyreMend)) { int k = tick ? 0 : _blazeNow ? 2 : 1; FireBook.PyreMendNom[k] += amount; FireBook.PyreMendHp[k] += g; }   // 第249期・**計数のみ**
        }
        if (g > 0) Log($"    {u.Name} は火で癒える（+{g}）", LogKind.Status);
    }

    void PerformAttackFramed(UnitState actor, string prefix, int attackPercent, AttackPattern? patternOverride)
    {
        // 第242期: 火勢の攻撃の枠（当てた敵の控え・燃え広がりの控え・ホタの段の着火）。**保持者がいなければ比較1つで素通り。**
        if (_fireLvLive && _fireLvTeams[actor.TeamId])
        {
            var fa = new FireAtkFrame { Actor = actor, Stage = actor.HasTrait(TraitId.PyreStage) ? FireLevelRule.Of(actor) : 0, MoveIgnite = _fireMoveIgnite,
                                        Critical = !_fireMoveIgnite && actor.HasTrait(TraitId.PyreCritical) && PyreStageTrait.IsCritical(actor) };
            SpreadScope? own = SpreadScopeOf(actor) is null ? new SpreadScope { Actor = actor } : null;
            if (own is not null) _spreadScopes.Add(own);
            _fireAtkFrames.Add(fa);
            try { PerformAttackArmored(actor, prefix, attackPercent, patternOverride); }
            finally
            {
                _fireAtkFrames.RemoveAt(_fireAtkFrames.Count - 1);
                if (own is not null) _spreadScopes.Remove(own);
            }
            if (own is not null) ResolveSpread(own);
            if (fa.Stage >= PyreStageTrait.BurstLevel) _burstLock = fa.Hit.Count > 0 ? fa.Hit[0] : null;
            ResolveStageIgnite(fa);
            if (fa.Critical) ResolveCritical(fa);   // 第246期（臨界）
            return;
        }
        PerformAttackArmored(actor, prefix, attackPercent, patternOverride);
    }

    void PerformAttackArmored(UnitState actor, string prefix, int attackPercent, AttackPattern? patternOverride)
    {
        if (_fireArmorLive)
        {
            var fr = new FireArmorFrame { Actor = actor };
            _fireArmorFrames.Push(fr);
            try { PerformAttackFooting(actor, prefix, attackPercent, patternOverride); }
            finally { _fireArmorFrames.Pop(); }
            ResolveFireArmor(fr);
            return;
        }
        PerformAttackFooting(actor, prefix, attackPercent, patternOverride);
    }

    void PerformAttackFooting(UnitState actor, string prefix, int attackPercent, AttackPattern? patternOverride)
    {
        if (!FootingTrait.StackOnHit || _shieldHolders.Count == 0)
        {
            PerformAttackBody(actor, prefix, attackPercent, patternOverride);
            return;
        }
        _footingFrames.Push(new List<UnitState>());
        try { PerformAttackBody(actor, prefix, attackPercent, patternOverride); }
        finally
        {
            foreach (UnitState u in _footingFrames.Pop()) StepFootingOnHit(u);
        }
    }

    private void PerformAttackBody(UnitState actor, string prefix, int attackPercent, AttackPattern? patternOverride)
    {
        if (!actor.IsAlive) return;

        // 第243期（④・バサの版 `ConfuseHalf`）: 保持者がいる戦では、混乱した駒の攻撃は標的を選ぶ前に `Roll(100)` を1回——
        // 50% で今までどおり自軍へ、残りは混乱をここで消して普段どおり振る。**保持者がいなければ比較1つで抜ける。**
        if (_confuseHalf && ConfusionLive && actor.RawCounter(StatusKeys.Confused) > 0)
        {
            UnitTally ct = TallyOf(actor);
            if (Roll(100) < ConfuseHalfTrait.SelfPercent) ct.ConfuseHalfSelf++;
            else
            {
                actor.SetCounter(StatusKeys.Confused, 0);
                ct.ConfuseHalfNormal++;
                Log($"    {actor.Name} は我に返った（混乱が解けた）", LogKind.Status);
            }
        }

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

        // 第277期（豆鉄砲）: 1発の打点は攻撃力に依らず `ShotDamage`（攻撃力は弾数の側・`PelletTrait.ModifyHitCount`）。
        // 萎縮・痺れ毒・澱み・止めの倍率はこの後ろなので、今までどおりこの 1 点に掛かる（切り捨てなので 1 は 1 のまま）。**保持者がいなければ比較1つで抜ける。**
        if (_pelletLive && actor.HasTrait(TraitId.Pellet)) atk = PelletTrait.ShotDamage;

        // 突き（第186期 追補）: 威力 ＝ 現在攻撃力 ×（1 ＋ 前の突きから逸らした回数）。突いたら 0 に戻す。
        // **増幅を意図して乗算にしてある**（ポンの判断）。対照（`ThrustPlain`）は 現在攻撃力 ＋ 素の攻撃力 × 回数。
        int? thrustCharge = null;   // 表示専用（台本の Attack の ThrustCharge）
        if (_thrustLive && pattern == AttackPattern.Pierce
            && (actor.HasTrait(TraitId.Thrust) || actor.HasTrait(TraitId.ThrustPlain)))
        {
            int charge = actor.RawCounter(ThrustTrait.ChargeKey);
            thrustCharge = charge;
            atk += (actor.HasTrait(TraitId.ThrustPlain) ? actor.Def.Attack : atk) * charge;
            actor.SetCounter(ThrustTrait.ChargeKey, 0);
            UnitTally th = TallyOf(actor);
            th.ThrustSwings++;
            th.ThrustChargeSum += charge;
            th.ThrustChargeMax = Math.Max(th.ThrustChargeMax, charge);
            th.ThrustAtkSum += atk;
            th.ThrustAtkMax = Math.Max(th.ThrustAtkMax, atk);
            (th.ThrustAtks ??= new List<int>()).Add(atk);
            if (charge > 0) Log($"    {actor.Name} は逸らした {charge} 回ぶんを乗せて突いた（威力 {atk}）", LogKind.Trigger);
        }

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

        // 第281期（炸裂）: 攻 × 層 × 倍率。止めと同じく**攻撃の解決時**に掛ける（敵の標の +50% はこの後の `ApplyDamage`）。
        // 爪痕と消費は `OnAfterAttack`（`RuptureAfter`）——ここでは「この一撃が炸裂か」と相手の HP を控えるだけ。
        if (_ruptureLive && pattern == AttackPattern.Single && actor.HasTrait(TraitId.Rupture))
        {
            int layers = target.TeamId != actor.TeamId ? target.RawCounter(StatusKeys.Marked) : 0;
            if (layers > 0)
            {
                // 第285期: 羽の1発は層を掛けない（攻 × 倍率だけ）。層は的の順番と §1 の +50% にだけ効く。
                bool feather = _featherLive && actor.HasTrait(TraitId.Feathers);
                atk *= feather ? Finisher.Multiplier : layers * Finisher.Multiplier;
                _ruptureTarget = target;
                _ruptureHpBefore = target.Hp;
                if (feather) _featherLast = target;
                NoteRupture(actor, layers, !TargetPool(actor).Contains(target), feather);
            }
            else _ruptureTarget = null;
        }

        // 萎縮（第189期・クビの `Daunt`）。**攻撃1回＝`PerformAttack` 1回**で、`atk` を作り終えた直後に半分にして消す。
        // 突き・止め・薄刃の払い直しの**後**なので、この一撃の打点そのものが半分になる。§1 の +50%・萎縮 −30%・
        // ヒサの半減・層・軛・呪いの共有は `ApplyDamage` の中なので、**半分になった量に今までどおり掛かる**。
        // 式は萎縮の段と同じ切り捨て（攻1 は 1 のまま）。**保持者がいなければ比較1つで抜ける。**
        if (_dauntLive && actor.RawCounter(StatusKeys.Daunted) > 0)
        {
            actor.SetCounter(StatusKeys.Daunted, 0);
            int cut = atk * DauntTrait.CowerPercent / 100;
            atk -= cut;
            UnitTally dt = TallyOf(actor);
            dt.DauntedSwings++;
            dt.DauntedCut += cut;
            Log($"    {actor.Name} は怯えて腕が縮んだ（この一撃 -{cut}）", LogKind.Status);
        }

        // 痺れ毒（第195期・スィドの `Numb`）。**萎縮の直後**——萎縮と重なれば 半分 → さらに層の割合（切り捨て2回）。
        // 印は消えず、減る割合は**その時点の毒の層**で決まる（層 × 3%・上限 60%・層 0 なら減らない）。
        // 反撃・割り込み・追い打ち・再行動・混乱した一撃もここを通る（敵の与ダメージは全部 `PerformAttack`・Phase 0 Q0-1）。
        // `CurrentAttack` は下げない（選び方は動かない）。**保持者がいなければ比較1つで抜ける。**
        int numbCut = 0, numbPct = 0;
        if (_numbLive && actor.RawCounter(StatusKeys.Numbed) > 0)
        {
            int layers = actor.RawCounter(StatusKeys.Poison);
            if (layers > 0)
            {
                numbPct = Math.Min(layers * NumbTrait.PercentPerLayer, NumbTrait.MaxPercent);
                numbCut = atk * numbPct / 100;
                atk -= numbCut;
                NoteNumbed(actor, layers, numbPct, numbCut);
                if (numbCut > 0) Log($"    {actor.Name} は毒で手が鈍った（-{numbPct}%・この一撃 -{numbCut}）", LogKind.Status);
            }
        }

        // 澱みのデバフ（第218期・ミオの `MireDull` / `MireDullAll`）。**痺れ毒の直後**（萎縮 → 痺れ毒 → 澱み・どれも切り捨て）。
        // 反撃・割り込み・追い打ち・再行動もここを通る。**保持者がいなければ比較1つで抜ける。**
        if (_mireDull != 0) atk = MireCut(actor, atk, 0);

        // 重圧（第294期・SR-b・`DivertPressureTrait`）。**澱みの直後・同じ段**（出どころの側の修正・切り捨て）。標を持つ敵（書き手を問わない）の一撃が
        // 層 × 15%（上限 45%）軽くなる——相手陣営に生きている保持者がいる間だけ。**保持者がいなければ件数の比較1つで抜ける。乱数を引かない。**
        if (_pressureHolders.Count > 0 && atk > 0 && actor.RawCounter(StatusKeys.Marked) > 0)
        {
            UnitState? pr = null;
            foreach (UnitState h in _pressureHolders) if (h.IsAlive && h.TeamId != actor.TeamId) { pr = h; break; }
            if (pr is not null)
            {
                int layers = actor.RawCounter(StatusKeys.Marked);
                int pct = Math.Min(layers * DivertPressureTrait.PercentPerLayer, DivertPressureTrait.MaxPercent);
                int cut = atk * pct / 100;
                atk -= cut;
                UnitTally pt = TallyOf(pr);
                pt.PressureHits++;
                pt.PressureCut += cut;
                (pt.PressureByLayer ??= new long[4])[Math.Min(layers, 3)] += cut;
                (pt.PressureHitsByLayer ??= new long[4])[Math.Min(layers, 3)]++;
                if (cut > 0) Log($"    {pr.Name} が {actor.Name} の一撃を見切った（-{pct}%・この一撃 -{cut}）", LogKind.Status);
                if (_verbose && cut > 0) Emit(new BattleEvent { Kind = BattleEventKind.Insight, Turn = _turn, ActorId = pr.InstanceId, TargetId = actor.InstanceId, Amount = cut, Slot = layers, StatusRemaining = pct, Team = actor.TeamId });   // 第295期・表示専用
            }
        }

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
        if (actor.HasTrait(TraitId.Pyre) && actor.RawCounter(StatusKeys.Burn) > 0 && actor.RawCounter(FireBurstRule.MoveKey) == 0)   // 第244期: 大技の一撃では段の見せ場を打たない
        {
            // 第242期（ホタの段）: 段ごとに1度ずつ（1 単体 ×4 ／ 2 貫き ×4 ＋着火 ／ 3 以上 5連撃）。札が無ければ今の1行。
            if (actor.HasTrait(TraitId.PyreStage))
            {
                int st = Math.Min(FireLevelRule.Of(actor), PyreStageTrait.BurstLevel);
                if (PyreStageTrait.IsCritical(actor))   // 第246期（表示だけ）
                    HighlightOnce(actor, "pyreCrit", $"  {actor.Name} の炎が臨界に達した（段4・貫き ×{FireCycleRule.HeavyMultiplier}・当たった敵に着火・敵の火を煽る）");
                else if (PyreStageTrait.IsLance(actor))
                    HighlightOnce(actor, "pyreLance", $"  {actor.Name} の炎が大火槍になった（段3・貫き ×{FireCycleRule.HeavyMultiplier}・当たった敵に着火）");
                else
                HighlightOnce(actor, "pyre" + st, st switch
                {
                    1 => $"  {actor.Name} は燃えたまま振り抜いた（段1・攻 ×{PyreTrait.Multiplier}・単体）",
                    2 => $"  {actor.Name} の炎が列を貫いた（段2・攻 ×{PyreTrait.Multiplier}・貫き・当たった敵に着火）",
                    _ => $"  {actor.Name} が燃え盛って連撃に入った（段3・1回 ×1.6 を {PyreStageTrait.BurstHits} 回・1回ごとに着火）",
                });
            }
            else HighlightOnce(actor, "pyre", $"  {actor.Name} は燃えたまま振り抜いた（攻 ×{PyreTrait.Multiplier}・貫き）");
        }
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
            Reaction = InReaction || InInterrupt,
            // 第186期 追補・表示専用。この突きに乗った逸らしの回数（突きの保持者だけ）
            ThrustCharge = thrustCharge,
            // 第195期・表示専用。痺れ毒で減った割合と量（印があって層 > 0 のときだけ）
            NumbPercent = numbPct > 0 ? numbPct : null,
            NumbCut = numbPct > 0 ? numbCut : null,
            // 第202期・表示専用。規則で選ぶ陣形（パターン2）の貫きが選んだ経路（自己検査が台本で突き合わせる）
            PierceLane = pattern == AttackPattern.Pierce && pierceLane >= 0 && target.Shape.DeterministicPierce ? pierceLane : null
        });

        if (pattern == AttackPattern.Pierce)
        {
            ConsumeConfusion(actor);   // 第146期: 貫きの出口（ResolvePierce は entry.TeamId を見る）
            ResolvePierce(actor, pierceLane, target, atk);
            return;
        }

        int dealt = atk;

        // 第217期: 鞭の一振りの枠（責め鞭の保持者だけ）。**振り始めに感電していたか**をここで控える（電気鞭）。
        // 入れ子（一振りの途中で別の駒が攻撃する）に備えて前の枠を退避する。**保持者がいなければ比較1つで抜ける。**
        WhipSwing? prevWhip = _whip, whip = null;
        if (_whipLive && actor.HasTrait(TraitId.Scourge))
        {
            whip = new WhipSwing
            {
                Actor = actor,
                Wired = actor.HasTrait(TraitId.LiveWire) && actor.RawCounter(StatusKeys.Shock) > 0,
                CountShock = actor.HasTrait(TraitId.ScourgeShock),
                PopsBefore = TallyOf(actor).ShockTriggeredUnits,
                // 第288期: 雷霆は手番の鞭だけ（反撃・割り込みの一振りでは撃たない）。保持者がいなければ比較1つで抜ける。
                Bolt = _chargeLive && actor.HasTrait(TraitId.Thunderclap) && StoredChargeTrait.Of(actor) >= StoredChargeTrait.Cap && !InReaction && !InInterrupt,
                BoltAny = _chargeLive && actor.HasTrait(TraitId.ThunderclapAny),
                NoCower = _shockWhipActor == actor,
            };
            _whip = whip;
            TallyOf(actor).WhipSwings++;
            if (_chargeLive) TallyOf(actor).ChargeAtSwing += actor.RawCounter(StoredChargeTrait.Key);   // 第288期・計数のみ
        }

        // 範囲の盾（第185期・バン）。**この一撃が盾の持ち主にも当たるか**を、振る前の盤面で決める
        // （巻き込みの顔ぶれは主目標の着弾の後に引き直されるが、盾が「同時に当たる」かはこの時点で読む）。
        // **保持者がいなければ比較1つで抜ける**——SecondaryTargets は乱数を引かないので、引いても盤面は動かない。
        UnitState? shield = _shieldHolders.Count > 0 && pattern != AttackPattern.Single
            ? ShieldHit(actor, target, patternOverride)
            : null;

        // 呪いの共有（第96期）は**単体攻撃の一撃そのもの**にだけ札を付ける。
        // 副次目標（薙ぎ・全体）と貫きの段には付けない——範囲が二乗で伸びるのを止める構造。
        int first = dealt;
        if (whip is not null) first = WhipAmount(whip, target, first);   // 第217期（当てる前の2倍）
        UnitState firstRecv = shield is null ? target : ShieldRecv(shield, target, ref first);
        if (_fireLvLive) NoteFireContact(actor, firstRecv);   // 第242期（燃え広がりの控え・当てる前）
        ApplyDamage(firstRecv, first, actor, singleHit: pattern == AttackPattern.Single, pattern: pattern);

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
            int share = Math.Max(1, dealt * SecondaryPercent / 100);
            if (whip is not null) share = WhipAmount(whip, extra, share);   // 第217期（当てる前の2倍・当たる駒ごと）
            UnitState recv = shield is null ? extra : ShieldRecv(shield, extra, ref share);
            if (_fireLvLive) NoteFireContact(actor, recv);   // 第242期
            ApplyDamage(recv, share, actor, pattern: pattern);
        }

        // 第217期・自己検査用（計数のみ）: 札が「動けるか」を読む時点の値を控える。
        bool whipPrimBound = whip is not null && TormentTrait.IsBound(this, target);
        bool whipSplashMovable = whip is not null && whip.Hits.Skip(1).Any(h => !TormentTrait.IsBound(this, h));

        // 特性の発動は攻撃1回につき1度、主目標に対してのみ。
        // 範囲攻撃のたびに巻き込みや毒が複数回発動すると、範囲持ちが即座に壊れる。
        // 第223期: 主目標が避けた（回避）なら走らせない——「当たらなかった」。
        if (!_evadeLive || _evadedNow != target)
            foreach (Trait t in actor.Traits.ToList())
            {
                TraitMark m = this.BeginTrait(t.Id, actor);   // 第94期 (T2) の印
                t.OnAfterAttack(this, actor, target, dealt);
                this.EndTrait(m);
            }

        if (whip is not null)
        {
            UnitTally wt = TallyOf(actor);
            int hits = whip.Hits.Count, pops = (int)(wt.ShockTriggeredUnits - whip.PopsBefore);
            // 自己検査用（**計数のみ**）: 主目標が動けず巻き込みに動ける敵がいた振り ／ 電気鞭の後に感電が残った振り ／ 怖気づくべき振り（主目標が動ける・感電していない）。
            // 「動けるか」は札が読むのと同じ時点（`OnAfterAttack` の前）で控えてある（悲鳴の竦みで後から変わるため）。
            if (whipPrimBound && whipSplashMovable) wt.WhipCheckSplashMovable++;
            if (whip.Wired && actor.RawCounter(StatusKeys.Shock) > 0) wt.WhipCheckWiredLeft++;
            if (!whipPrimBound && !whip.Wired) wt.WhipCheckShouldCower++;
            (wt.WhipHitsHist ??= new long[8])[Math.Min(hits, 7)]++;
            (wt.WhipPopsHist ??= new long[8])[Math.Min(pops, 7)]++;
            _whip = prevWhip;
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
            : LaneOccupants(LivingMembers(entry.TeamId), lane, entry.Shape);

        int passed = 0;
        int primaryDealt = 0;
        // 第233期・**計数のみ**（② 火を運ぶ貫きの見込み）: 燃えているホタの貫き。
        bool pyreLink = actor.TeamId == PlayerTeam && actor.HasTrait(TraitId.Pyre) && BurningForLink(actor);
        int linkPh = BurnLinkLedger.PhaseOf(_turn);
        if (pyreLink) BurnLinkBook.PyrePierces[linkPh]++;

        // 範囲の盾（第185期）。貫きは同じレーンに並んだ全員に当たるので、盾がこの列にいれば「同時に当たる」。
        UnitState? shield = null;
        if (_shieldHolders.Count > 0)
            foreach (UnitState h in _shieldHolders)
                if (h.IsAlive && line.Contains(h) && !HoleSkip(h)) { shield = h; break; }   // 第229期: 転倒の穴

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

            if (pyreLink && u != entry)   // 第233期・**計数のみ**
            {
                BurnLinkBook.PyreExtraHits[linkPh]++;
                if (!BurningForLink(u)) BurnLinkBook.PyreExtraUnburnt[linkPh]++;
            }
            int got = dmg;
            UnitState recv = shield is null ? u : ShieldRecv(shield, u, ref got);
            if (_fireLvLive) NoteFireContact(actor, recv);   // 第242期
            ApplyDamage(recv, got, actor, pattern: AttackPattern.Pierce);
            if (u == entry) primaryDealt = dmg;
            passed++;
        }

        // 特性の発動は攻撃1回につき1度、レーンの先頭に対してのみ。
        // 貫いた全員に毒や巻き込みが乗ると、貫き持ちが即座に壊れる。
        // 第223期: 先頭が避けた（回避）なら走らせない。
        if (!_evadeLive || _evadedNow != entry)
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
                .Where(f => primary.Shape.SweepTargets(primary.Slot).Contains(f.Slot)).ToList(),
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
        // 第208期: 撃ち返す板の控え（1回の `ApplyDamage` の枠ごと。入れ子の呼び出しは退避・復帰する）。
        int prevHitSerial212 = CurrentHitSerial; CurrentHitSerial = ++_hitSerial;   // 第212期（計数のみ・1回の ApplyDamage の枠の通し番号）
        int prevAmt = _reflectAmt; UnitState? prevFrom = _reflectFrom;
        int prevRest = _reflectRest, prevRatio = _reflectRatio;   // 第209期
        _reflectAmt = 0; _reflectFrom = null; _reflectRest = 0; _reflectRatio = 0;
        if (_reboundLive && amount > 0) NoteFrameHit(target, source, burnTick);   // 第209期（計数のみ）
        // 第255期（被弾の燃焼）: 本体の前に「前から燃えていたか」と HP＋破片を控え、本体の後で減っていれば燃焼を刻む。札が無ければ比較1つで抜ける。
        bool burnHit = _burnHitLive && amount > 0 && BurnHitEligible(target, source, burnTick, relayed, hexShare, levy);
        int burnHitBefore = burnHit ? target.Hp + target.RawCounter(StatusKeys.Armor) : 0;
        // 第256期（計数のみ）: 中継の一撃で燃えている駒が削られた機会（被弾の燃焼の対象外）。
        bool relayProbe = _burnHitLive && relayed && amount > 0 && source is not null && target.IsAlive && target.RawCounter(StatusKeys.Burn) > 0;
        int relayBefore = relayProbe ? target.Hp + target.RawCounter(StatusKeys.Armor) : 0;
        int myAmt, myRest, myRatio; UnitState? myFrom;
        try
        {
            ApplyDamageBody(target, amount, source, isFriendlyFire, lethal, burnTick, relayed,
                            spillWound, deepBite, singleHit, hexShare, pattern, levy);
        }
        finally
        {
            Hit = prevHit;
            CurrentHitSerial = prevHitSerial212;
            myAmt = _reflectAmt; myFrom = _reflectFrom; myRest = _reflectRest; myRatio = _reflectRatio;
            _reflectAmt = prevAmt; _reflectFrom = prevFrom; _reflectRest = prevRest; _reflectRatio = prevRatio;
        }
        if (burnHit && target.IsAlive && target.RawCounter(StatusKeys.Burn) > 0
            && target.Hp + target.RawCounter(StatusKeys.Armor) < burnHitBefore)
        {
            NoteBurnHitChance(target, source!);   // 計数のみ（版に依らず）
            if (_burnHitTeams[target.TeamId]) BurnOnHit(target, source!);
            else BurnHitBook.GateOff[BhSide(target)]++;
        }
        if (relayProbe && target.IsAlive && target.RawCounter(StatusKeys.Burn) > 0
            && target.Hp + target.RawCounter(StatusKeys.Armor) < relayBefore)
        {
            string rk = BhKey(target);
            if (!BurnHitBook.RelayChanceBy.TryGetValue(rk, out var rc)) BurnHitBook.RelayChanceBy[rk] = rc = new long[2];
            rc[0]++; rc[1] += Math.Max(1, FireLevelRule.Of(target));
        }
        if (myAmt > 0 && myFrom is not null) ReflectPlank(target, myFrom, myAmt, myRest, myRatio);
    }

    /// <summary>ダメージ処理の本体。<see cref="ApplyDamageCore"/> だけが呼ぶ。</summary>
    void ApplyDamageBody(UnitState target, int amount, UnitState? source,
                         bool isFriendlyFire, bool lethal,
                         bool burnTick, bool relayed,
                         bool spillWound, bool deepBite,
                         bool singleHit, bool hexShare,
                         AttackPattern? pattern, bool levy)
    {
        // 逸らしの受け渡しの札（第186期）。**ここで読んで消す**——この呼び出しの中で起きる
        // 別の ApplyDamage（中継・死亡トリガー）に札を漏らさないため。
        UnitState? deflectFrom = _deflectFrom;
        _deflectFrom = null;
        // 第297期: 分かちの中継の札（「誰の痛みを引き受けた一撃か」）。**ここで読んで消す**（逸らしの札と同じ作法）。
        UnitState? shareFrom = _shareFrom;
        _shareFrom = null;
        int? deflectCharge = _deflectCharge;
        _deflectCharge = null;
        // 第214期: 感電の札（1回の呼び出しにだけ効く）。**ここで読んで消す**（逸らしの札と同じ作法）。
        byte shockNote = _shockNext;
        _shockNext = 0;
        bool kuguHeldAtEntry = _grappleLive && target.HasTrait(TraitId.Grapple) && KuguHeld(target);   // 第290期・計数のみ
        bool shockKillerSet = _shockKillerSet;
        UnitState? shockKiller = _shockKiller;
        _shockKillerSet = false;
        _shockKiller = null;
        // 第219期: 燃焼の刻みそのものの札・叩きつけの札（1回の呼び出しにだけ効く・ここで読んで消す）。
        bool burnTickSelf = _burnTickSelf;
        _burnTickSelf = false;
        bool slamHit = _brittleSlamNext;
        _brittleSlamNext = false;
        bool burstHit = _burstHitNext;   // 第220期（計数の経路だけ）
        _burstHitNext = false;
        bool wideRelay = _wideNext;      // 第294期（SR-a の肩代わりの段・1回の呼び出しにだけ効く・ここで読んで消す）
        _wideNext = false;

        if (!target.IsAlive || amount <= 0) return;

        // 第295期（HK）: いま開いている攻撃のひとまとまりの中で、標を持つ敵に当たった（最も深い層を控える）。主が決まっていない枠（反撃・割り込み）は最初の出どころを主にする。
        // HK の保持者がいなければ比較1つで抜ける。盤面は読むだけ。
        if (_rallyLive && _bundles.Count > 0) BundleHit(target, source);

        // 第294期（計数のみ）: 踏みとどまった駒が次に受けた敵の攻撃（癒やされていたか・その一撃で倒れたか）。踏みとどまりの保持者がいなければ比較1つで抜ける。
        UnitState? holdWatchBy = null;
        if (_holdLive && _holdWatch.Count > 0 && source is not null && source.TeamId != target.TeamId && !burnTick && !levy && !relayed && !hexShare && !isFriendlyFire
            && _holdWatch.Remove(target.InstanceId, out holdWatchBy))
        {
            UnitTally wt = TallyOf(holdWatchBy);
            wt.HoldNextHit++;
            if (target.Hp > 1 || target.RawCounter(StatusKeys.Armor) > 0) wt.HoldNextHealed++;
        }

        // 回避（第223期・逃げ上手のセロ・`EvadeTrait`）。**札を読んで消した直後・逸らしより前**——避けて返っても次の呼び出しに札が漏れず、
        // 破片・受け流し・軛・`OnDamaged` はすべて後ろなので、避けた一撃は破片も減らさない。
        // 避けるのは「出どころが相手陣営 かつ 刻み・徴収・中継・呪いの共有ではない」一撃だけ——状態異常の刻み（出どころ null）・
        // 放電と爆発（出どころが同じ陣営）・味方の刃は外れる。**保持者がいなければ比較1つで抜ける。**
        if (_evadeLive && target.HasTrait(TraitId.Evade))
        {
            bool attack = source is not null && source.TeamId != target.TeamId && !burnTick && !levy && !relayed && !hexShare;
            _evadeHitClass = attack ? 0
                           : source is null || burnTick ? 1
                           : burstHit || shockNote == 3 ? 2
                           : levy || relayed || hexShare ? 4 : 3;
            if (attack && EvadeTrait.TryEvade(this, target, source!, amount, pattern))
            {
                _evadedNow = target;
                return;
            }
        }

        // 逸らし（第186期・DeflectTrait・ソラ）: 単体攻撃のダメージを、半分だけ本人が受け、残り半分を
        // 「逸らし（Divert）で標を付けた敵」へ逸らす。**入口に置く**（棘守りの上限・駒の被ダメ修正・惨禍より前）
        // ——逸らすのは素の攻撃の量で、ソラ側の増減（惨禍・ヒサの半減・巨躯・破片・軛）はソラの取り分にだけ掛かる。
        // 逸らした分は敵への別の呼び出しで、そちらでは敵の側の段（§1 の +50%・破片・軛）が掛かる。
        // **逸らす方を先に入れる**（棘守りの中継と同じ順。ソラが倒れても逸らした分は既に届いている）。
        // 対象は「敵陣営の出どころを持つ主目標への一撃」だけ（`pattern == Single` は PerformAttack の主目標にしか立たない）。
        // 刻み・徴収・中継・共有・同士討ちは逸らさない。**保持者がいなければリストが空で比較1つで抜ける。**
        bool deflectedHere = false;
        // 第294期（SR-a）: 肩代わりの保持者は、単体以外の敵の一撃（主目標・巻き込み・貫き・全体）と、自分が肩代わりで受けた段も逸らす。
        // 保持者がいなければ `wideOk` は偽で、条件は第186期のまま（評価の順も変えない）。
        bool wideOk = _wideHolders.Count > 0 && source is not null && source.TeamId != target.TeamId && !burnTick && !levy && !hexShare
            && (wideRelay || (pattern is not null && pattern != AttackPattern.Single && !relayed && !isFriendlyFire))
            && target.HasTrait(TraitId.DeflectWide);
        if (_deflectHolders.Count > 0 && (wideOk || (pattern == AttackPattern.Single && source is not null
            && source.TeamId != target.TeamId && !burnTick && !levy && !relayed && !hexShare && !isFriendlyFire))
            && target.HasTrait(TraitId.Deflect) && !HoleSkip(target))   // 第229期: 転倒の穴
        {
            UnitTally dt = TallyOf(target);
            UnitState? to = DeflectTrait.Target(this, target);
            // 第186期 追補（ポンの指示）: **殴ってきた本人が指差した敵なら、本人には返さない**
            // ——ほかの標持ち、いなければ本人以外の現在HP最大へ（`DeflectTrait.Redirect`・乱数を引かない）。
            int route = 0;   // 0 指差した敵 ／ 1 ほかの標持ち ／ 2 補助
            if (to is not null && to == source)
            {
                dt.DeflectSelf++;   // 計数のみ（差し替えが起きた回数）
                to = DeflectTrait.Redirect(this, target, source, out bool otherMarked);
                route = otherMarked ? 1 : 2;
            }
            int moved = to is null ? 0 : amount * DeflectTrait.DeflectPercent / 100;
            if (to is null) dt.DeflectNoTarget++;
            else if (moved > 0)
            {
                amount -= moved;
                deflectedHere = true;
                dt.DeflectHits++;
                if (wideOk) dt.WideDeflects++;   // 第294期（計数のみ）
                dt.DeflectMoved += moved;
                if (route == 0) dt.DeflectToPointed++; else if (route == 1) dt.DeflectToOtherMarked++; else dt.DeflectToFallback++;
                // 突き（第186期 追補）の回数。**逸らしが実際に起きたときだけ**積む（Q0-8）。
                int? chargeNow = null;   // 表示専用（台本の ThrustCharge）
                if (_thrustLive && (target.HasTrait(TraitId.Thrust) || target.HasTrait(TraitId.ThrustPlain)))
                {
                    chargeNow = target.RawCounter(ThrustTrait.ChargeKey) + 1;
                    target.SetCounter(ThrustTrait.ChargeKey, chargeNow.Value);
                }
                Log($"    {target.Name} が {source.Name} の一撃を {to.Name} へ逸らした（{moved}）", LogKind.Trigger);
                UnitTally tt0 = TallyOf(to);
                long before = tt0.DamageTaken;
                _deflectFrom = target;
                _deflectCharge = chargeNow;
                ApplyDamage(to, moved, source, isFriendlyFire: true);
                _deflectFrom = null;
                _deflectCharge = null;
                dt.DeflectLanded += tt0.DamageTaken - before;
                if (!to.IsAlive)
                {
                    dt.DeflectKills++;
                    if (source.IsAlive && source.HasTrait(TraitId.Executioner)) dt.DeflectExecFeed++;   // 計数のみ
                }
                if (!target.IsAlive || amount <= 0) return;
            }
        }

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
            if (havocExtra > 0) TallyOf(target).HavocTaken += havocExtra;   // 第225期・計数のみ（名目・破片と上限の前）
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

        // 敵の標の被ダメージ増（第184期 §1・MarkRule）。**入口の族**（惨禍・荷の直後、軽減・肩代わり・
        // 破片・身構え・軛より前）——上限の前に置くので「1発は Cap を超えない」は守られる。
        // **敵陣営の標持ちだけ・攻撃によるダメージだけ**（相手陣営の出どころがあり、刻み・徴収・中継・共有ではない）。
        // **増幅は加算**（惨禍と同じ式）。既定の判定は `VulnerablePercent > 0` と陣営の比較で短絡する。
        // 第186期: 逸らしの受け渡し（`deflectFrom`）は出どころが同じ陣営でも §1 に乗せる（指示書 §1-1）。
        // 上乗せの帳簿は殴った敵ではなく**逸らした駒**に付ける。
        if (MarkRules.VulnerablePercent > 0 && target.TeamId != PlayerTeam
            && source is not null && (source.TeamId != target.TeamId || deflectFrom is not null)
            && !burnTick && !levy && !relayed && !hexShare
            && target.RawCounter(StatusKeys.Marked) > 0)
        {
            int extra = amount * MarkRules.VulnerablePercent / 100;
            if (extra > 0)
            {
                amount += extra;
                int oi = _markOrigin.TryGetValue(target.InstanceId, out MarkOrigin o) ? (int)o : 0;
                MarkVulnHits[oi]++;
                MarkVulnAdded[oi] += extra;
                if (deflectFrom is not null) TallyOf(deflectFrom).DeflectVulnAdded += extra;
                else TallyOf(source).MarkVulnDealt += extra;
                Log($"    指差された {target.Name} は深く傷ついた（+{extra}）", LogKind.Trigger);
            }
        }

        // 燃焼の脆さ（第219期・`EmberRule.Brittle`）。**入口の族の最後**（惨禍・荷・§1 の直後）で、
        // 据え・散開・萎縮・矢面・層・巨躯・分かち・破片・身構え・軛より前＝**一撃の重さそのものが上がる**。
        // 攻め手側の増減（萎縮・痺れ毒・澱み・呪い則のなまり）は `PerformAttack` と各口で既に掛かっている。
        // **中継（巨躯・分かち）と呪いの共有には掛けない**——最初の受け手で重さが決まった一撃の分け前なので、
        // 掛けると同じ一撃に2度乗る。逸らし（ソラ）と棘守りの上限は入口で**素の量**を割るので、それぞれの受け手で1度ずつ乗る。
        // **増幅は加算・切り上げ**（`amount + ⌈amount × Brittle / 100⌉`）。既定 0 なら比較1つで抜ける。
        int brittleExtra = 0;
        if (Ember.Brittle > 0 && !relayed && !hexShare && BrittleApplies(target)
            && (burnTickSelf || target.RawCounter(StatusKeys.Burn) > 0))
        {
            brittleExtra = (amount * BrittlePct(target, out int lvB) + 99) / 100;   // 第245期: 敵の火勢で割合が上がる（札が無ければ `Ember.Brittle`）
            if (lvB > 0) { FireBook.FoeBrittle[lvB] += brittleExtra; FireBook.FoeBrittleUp[lvB] += brittleExtra - (amount * Ember.Brittle + 99) / 100; }   // **計数のみ**
            amount += brittleExtra;
            int route = burstHit ? 8 : burnTickSelf ? 6 : slamHit ? 2 : shockNote == 1 ? 1 : shockNote == 3 ? 3
                      : source is null ? 5 : InReaction ? 4 : levy ? 7
                      : source.TeamId != target.TeamId ? 0 : 7;
            int side = SideOf(target);
            BrittleBook.Base[side, route] += amount - brittleExtra;
            BrittleBook.Extra[side, route] += brittleExtra;
            BrittleBook.Hits[side, route]++;
            if (target.HasTrait(TraitId.Pyre)) BrittleBook.PyreExtra += brittleExtra;
            Log($"    燃える {target.Name} は脆くなっている（+{brittleExtra}）", LogKind.Status);
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
            && !teammates.Any(u => u != target && FormationRules.AreAdjacent(target, u)))
        {
            amount -= amount * LooseTrait.ReductionPercent / 100;
            // 第97期・表示専用。**隣が空くのは戦闘の途中**（味方が倒れる）なので、
            // 成立の瞬間が見えないと「最初から付いている装備」と区別が付かない。
            HighlightOnce(target, "loose", $"  {target.Name} の周りが空いた（散開 -{LooseTrait.ReductionPercent}%）");
        }

        // 萎縮: 火力と引き換えの被ダメージ減
        // 第189期: 新しいクビの「身を寄せる」（`Huddle`）も同じ段で同じ量（旧 `Cower` は保持者 0 枚で残置）。
        if (teammates.Any(u => u.HasTrait(TraitId.Cower) || u.HasTrait(TraitId.Huddle)))
            amount -= amount * CowerTrait.ReductionPercent / 100;

        // 矢面（第184期 §2・BeckonTrait）: ヒサの標を持つ味方は、攻撃によるダメージが半分になる。
        // **軽減の族**（据え・散開・萎縮の直後、肩代わり・破片・軛より前）で、§1 の被ダメージ増と
        // **同じ条件で符号だけ違う形**（相手陣営の出どころがあり、刻み・徴収・中継・共有ではない）。
        // **保持者がいなければリストが空で1回も走らない。**
        if (_beckonHolders.Count > 0 && source is not null && source.TeamId != target.TeamId
            && !burnTick && !levy && !relayed && !hexShare)
        {
            UnitState? holder = BeckonGuardOf(target);
            if (holder is null) NoteBeckonStripped(target, amount);   // 計数のみ（剥がされて守りが無かった被弾）
            if (holder is not null)
            {
                int saved = amount * BeckonTrait.GuardPercent / 100;
                if (saved > 0)
                {
                    amount -= saved;
                    if (deflectedHere) TallyOf(target).DeflectBeckonStack++;   // 第186期（計数のみ）
                    BeckonGuardHits++;
                    BeckonGuardSaved += saved;
                    TallyOf(holder).BeckonGuardSaved += saved;
                    TallyOf(target).BeckonGuardTaken += saved;
                    Log($"    矢面の {target.Name} は痛みを半分に抑えた（-{saved}）", LogKind.Trigger);
                }
            }
        }
        // 第300期（規定のヒサ・`BeckonFeather`）: **ミサの羽**（羽の保持者が出どころの一撃——手番の羽 ／ 乱射 ／ 標撃ち）が矢面の味方に当たったときも、同じ量を半分にする。
        // 上の矢面と同じ段・同じ「刻み・徴収・中継・共有ではない」に、出どころが**同じ陣営の羽の保持者**であることを足しただけ（ボルグの巻き込み・カドの反撃の巻き込みなど、ほかの同士討ちには掛けない）。
        // 半分にするかどうかは標を付けたヒサの札で決める（旧の規定のヒサ `HisaHKs` は掛けない）。**札の保持者がいなければ比較1つで抜ける。乱数を引かない。**
        else if (_beckonFeatherLive && source is not null && source.TeamId == target.TeamId && source != target
            && !burnTick && !levy && !relayed && !hexShare && source.HasTrait(TraitId.Feathers)
            && BeckonGuardOf(target) is UnitState fholder && fholder.HasTrait(TraitId.BeckonFeather))
        {
            int saved = amount * BeckonTrait.GuardPercent / 100;
            if (saved > 0)
            {
                amount -= saved;
                UnitTally ht = TallyOf(fholder);
                ht.BeckonFeatherHits++;
                ht.BeckonFeatherSaved += saved;
                TallyOf(target).BeckonFeatherTaken += saved;
                Log($"    矢面の {target.Name} は羽の痛みも半分に抑えた（-{saved}）", LogKind.Trigger);
                // 表示専用（直後にこの一撃の `Damage`）。
                if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.BeckonFeather, Turn = _turn, ActorId = fholder.InstanceId, TargetId = target.InstanceId, PartnerId = source.InstanceId, Amount = saved, Team = target.TeamId });
            }
        }

        // 静電気の膜（第294期・SM・`StaticMembraneTrait`）: ソムが生きている間、帯電している味方への敵の攻撃は半分。**軽減の族**（矢面の直後・層の手前）。
        // 矢面と同じ条件（相手陣営の出どころ・刻み／徴収／中継／共有ではない）に同士討ちの除外を足す——放電・トウの漏れ・カタの雷の漏れは半分にしない。
        // HP に届けば感電は普段どおり弾ける（起爆の段は下のまま）。**保持者がいなければ件数の比較1つで抜ける。乱数を引かない。**
        if (_membraneHolders.Count > 0 && source is not null && source.TeamId != target.TeamId
            && !burnTick && !levy && !relayed && !hexShare && !isFriendlyFire
            && target.RawCounter(StatusKeys.Shock) > 0 && MembraneOf(target.TeamId) is UnitState mem)
        {
            int saved = amount * StaticMembraneTrait.GuardPercent / 100;
            if (saved > 0)
            {
                amount -= saved;
                UnitTally mt = TallyOf(mem);
                mt.MembraneHits++;
                mt.MembraneSaved += saved;
                (mt.MembraneBySrc ??= new long[5])[_shockWriterCat.TryGetValue(target.InstanceId, out int wc) ? wc : 4]++;
                Log($"    帯電した {target.Name} の膜が一撃を半分に抑えた（-{saved}）", LogKind.Trigger);
            }
        }

        // 据えの層（第185期・FootingTrait）: 1層ごとに被ダメ −10%。**軽減の族**（矢面の直後、肩代わり・破片・軛より前）。
        // 範囲の盾で代わりに受けた分にもここで乗る（盾はこの駒への ApplyDamage として入ってくる）。
        // **保持者がいなければ比較1つで抜ける。**
        if (_shieldHolders.Count > 0)
        {
            int layers = target.RawCounter(StatusKeys.Footing);
            if (layers > 0)
            {
                int saved = amount * layers * FootingTrait.PercentPerLayer / 100;
                if (saved > 0)
                {
                    amount -= saved;
                    TallyOf(target).FootingSaved += saved;
                }
            }
        }

        // 火の鎧（第234期・`FireArmorTrait`）。**軽減の族の最後**（層の直後、巨躯・分かち・破片・身構え・軛より前）。
        // ① 敵の攻撃（いまの攻撃の枠の主が出どころ・刻み／徴収／中継／共有ではない）を受けたら控える——火は攻撃が終わってから点く。
        // ② 燃えている間は半分（切り上げ）。種類を問わない（燃焼の刻みは手前で焼かれないので来ない）。
        // **保持者がいなければ比較1つで抜ける。乱数を引かない。**
        if (_fireArmorLive && target.HasTrait(TraitId.FireArmor))
        {
            if (source is not null && source.TeamId != target.TeamId && !burnTick && !levy && !relayed && !hexShare
                && FireArmorFrameOf(source) is FireArmorFrame fr && !fr.Hit.Contains(target))
                fr.Hit.Add(target);
            if (amount > 0 && target.RawCounter(StatusKeys.Burn) > 0)
            {
                int saved = amount * FireArmorTrait.GuardPercent / 100;
                if (saved > 0)
                {
                    amount -= saved;
                    UnitTally ft = TallyOf(target);
                    ft.FireArmorGuardHits++;
                    ft.FireArmorSaved += saved;
                    EmitFireArmor(target, target, FireArmorLabels.Guard, saved);
                    Log($"    燃える {target.Name} の鎧が痛みを半分に抑えた（-{saved}）", LogKind.Trigger);
                    if (_kindleLive) KindleGuard(target, saved, 0);   // 第252期（B1・札が無ければ何もしない）
                }
            }
        }

        // 盾の配り（第238期・ボルグの版 D1 隣 ／ D2 全員）。火の鎧の半減の直後（巨躯・分かち・破片・身構え・軛より前）。
        // 敵の攻撃（相手陣営の出どころ・刻み／徴収／中継／共有ではない）を燃えている味方が受けたら半分（切り上げ）。保持者自身は対象外。
        // **保持者がいなければ件数の比較1つで抜ける。乱数を引かない。**
        if (_fireWardHolders.Count > 0 && amount > 0 && target.RawCounter(StatusKeys.Burn) > 0
            && source is not null && source.TeamId != target.TeamId && !burnTick && !levy && !relayed && !hexShare
            && FireWardHolder(target) is UnitState ward)
        {
            int saved = amount * FireWardTrait.GuardPercent / 100;
            if (saved > 0)
            {
                amount -= saved;
                UnitTally wt = TallyOf(ward);
                wt.FireWardHits++;
                wt.FireWardSaved += saved;
                TallyOf(target).FireWardTaken += saved;
                EmitFireArmor(ward, target, FireArmorLabels.Ward, saved);
                Log($"    {ward.Name} の火が燃える {target.Name} を守った（-{saved}）", LogKind.Trigger);
                if (_kindleLive) KindleGuard(ward, saved, 1);   // 第252期（B1・札が無ければ何もしない）
            }
        }

        if (amount <= 0) return;

        // 肩代わり（第294期・SR-a・`DeflectWideTrait`）。**肩代わりの族の先頭**（巨躯・分かちの手前・軽減の族の後）——ソラが受けるのは
        // 味方の側の軽減（矢面・膜・層など）を通った後の量の半分。単体以外の敵の攻撃（相手陣営の出どころ・刻み／徴収／中継／共有／同士討ちではない）だけ。
        // **`u != source`**（敵の出どころなので自明だが作法として入れる）・ソラ自身・倒れたソラは除く。受けた段はソラの側で逸らし（入口）が掛かる。
        // **保持者がいなければ件数の比較1つで抜ける。乱数を引かない**（保持者が複数なら席番号の若い方）。
        if (_wideHolders.Count > 0 && source is not null && source.TeamId != target.TeamId
            && pattern is not null && pattern != AttackPattern.Single
            && !burnTick && !levy && !relayed && !hexShare && !isFriendlyFire && !target.HasTrait(TraitId.DeflectWide))
        {
            UnitState? sora = null;
            foreach (UnitState w in _wideHolders)
                if (w.IsAlive && w.TeamId == target.TeamId && w != target && w != source && (sora is null || w.Slot < sora.Slot)) sora = w;
            int taken = sora is null ? 0 : amount * DeflectWideTrait.Percent / 100;
            if (sora is not null && taken > 0)
            {
                amount -= taken;
                UnitTally wt = TallyOf(sora);
                wt.WideShoulders++;
                wt.WideShoulderAmt += taken;
                Log($"    {sora.Name} が {target.Name} への一撃を半分引き受けた（{taken}）", LogKind.Trigger);
                _wideNext = true;
                ApplyDamage(sora, taken, source, isFriendlyFire: true, burnTick: burnTick, relayed: true, levy: levy, pattern: pattern);
                _wideNext = false;
                if (!target.IsAlive || amount <= 0) return;
            }
        }

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
                            WhetEach(back, target, gain, WhetRoute.Regurgitate);
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

        UnitState? lateSharer = null;   // 第208期（U3）: 破片の段の後ろで肩代わりするドハ

        // 分かち: 型を問わず肩代わりする。庇うと違い、薙ぎや全体でも働く。
        // 味方同士の巻き込み（isFriendlyFire）も引き受ける。ここを除外していたとき、
        // ドハはカドの代金（敵からの被弾）だけを4割肩代わりして守り、収入源（味方への巻き込み）は
        // 満額通していた。都合のいい側だけを助ける形になっていたので条件を外した。
        // 肩代わり先が自分自身になる再帰は下の HasTrait(Sharer) で止まる。
        if (!target.HasTrait(TraitId.Sharer))
        {
            UnitState? sharer = PickOne(
                teammates.Where(u => u.HasTrait(TraitId.Sharer) && u != target).ToList());
            // 第208期（U3・`SharerArmored`）: 殴られた味方の破片の段の**後ろ**で肩代わりする（板が吸った残りだけを4割）。
            // 選ぶのはここ（乱数の位置は変えない）で、取るのは破片の段の直後（下の `lateSharer`）。
            if (sharer is not null && sharer.HasTrait(TraitId.SharerArmored)) { lateSharer = sharer; sharer = null; }
            if (sharer is not null)
            {
                int taken = amount * SharerTrait.Percent / 100;
                if (taken > 0)
                {
                    amount -= taken;
                    Log($"    {sharer.Name} が {target.Name} の痛みを引き受けた", LogKind.Trigger);
                    if (deepBite) TallyOf(target).DeepBiteRelayed++;   // 第93期 §1-2 の 3（計数のみ）
                    _shareFrom = target;   // 第297期（中継の札・呼び出しの頭で読んで消す）
                    ApplyDamage(sharer, taken, source, isFriendlyFire: true, burnTick: burnTick, relayed: true, levy: levy);
                    _shareFrom = null;

                    // 痛みを取り上げられた者は腕がなまる。肩代わり量に比例させているので、
                    // 代金はドハのHPという有限プールから払われる（SharerTrait.DullDivisor 参照）。
                    // 切り捨てのままにしてあるのは、Math.Max(1, ...) にすると小さいダメージの
                    // 連打で比例関係が崩れ、下げ幅が肩代わり量から切り離されるため。
                    int dull = taken / SharerTrait.DullDivisor;
                    if (dull > 0 && !sharer.HasTrait(TraitId.SharerNoDull))
                    {
                        TraitMark dm = BeginTrait(TraitId.Sharer, sharer);   // 第298期 段0-2（群6）
                        Dull(target, dull, DullRoute.Sharer, sharer);
                        AttrEnd(dm, 6, sharer, dull);
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
        int armorAtEntry211 = armor;   // 第211期（計数のみ）
        // 第212期（`BraceArmored`・ササ）: 身を固めている間の一撃は、**破片より先に**上限で切る（切り落とした分は今までどおり保留へ）。
        // 破片が無いときは下の上限の段が同じことをするので、ここは破片があるときだけ——破片の無い駒・札の無い駒は1ビットも動かない。
        // 第213期（`BraceCapFirst`・W1 / W2）: 同じ門を通す（破片が受け切った一撃の扱いだけが `ArmorOnlyHit` で分かれる）。
        if (_braceArmoredLive && armor > 0 && Brace.Cap > 0 && amount > Brace.Cap
            && (target.HasTrait(TraitId.BraceArmored) || target.HasTrait(TraitId.BraceCapFirst))
            && BraceTrait.IsBraced(this, target))
        {
            int refused = amount - Brace.Cap;
            amount = Brace.Cap;
            BraceTrait.Refuse(this, target, refused);
            TallyOf(target).BraceArmorEarly++;
            TallyOf(target).BraceArmorEarlyAmt += refused;   // 第213期（計数のみ）
            Log($"    {target.Name} が身構えて一撃を {refused + Brace.Cap} から {Brace.Cap} に抑えた（破片より先に）", LogKind.Trigger);
        }
        if (armor > 0)
        {
            int soak = Math.Min(armor, amount);
            // 第212期（計数のみ・Q0-3）: 身を固めている間に、上限を超える一撃を破片が先に受けた。
            if (Brace.Cap > 0 && amount > Brace.Cap && source is not null && source.TeamId != target.TeamId
                && target.HasTrait(TraitId.Brace) && BraceTrait.IsBraced(this, target))
            {
                UnitTally bt = TallyOf(target);
                bt.BraceArmorHits++;
                bt.BraceArmorHypRefused += amount - Brace.Cap;
                bt.BraceArmorExtraBurned += soak - Math.Min(armor, Brace.Cap);
                if (amount - soak <= 0) bt.BraceArmorFullMuted++;
                // 第213期 Q0-1（計数のみ）: 上限が先なら破片が受け切った（破片 ≧ 上限）のに、いまは残りが HP に届く一撃。
                else if (armor >= Brace.Cap) { bt.BraceWouldMute++; bt.BraceWouldMuteHp += amount - soak; BraceWouldMuteSerial = CurrentHitSerial; }
            }
            // 第213期（計数のみ）: 板の印を持つ駒が敵の一撃を破片で受けた回数（表C の「板が割れるまでの一撃の数」）。
            if (source is not null && source.TeamId != target.TeamId && !burnTick && target.RawCounter(StatusKeys.Plank) != 0)
                TallyOf(target).PlankHitsTaken++;
            if (_turn == 1 && _scrapHolders.Count > 0) TallyOf(target).FirstTurnArmorSoak += soak;   // 第212期（計数のみ）
            // 第208期: 撃ち返す板（`PlankRebound`）。印は破片が 0 になると消えるので、減らす前に読む。
            if (_reboundLive && source is not null && source.TeamId != target.TeamId && !burnTick && !InReaction
                && (target.RawCounter(StatusKeys.Plank) & PlankTrait.Rebound) != 0)
            {
                _reflectAmt += soak;
                _reflectFrom = source;
                // 第209期: その一撃を受けた後に残った破片と、印の倍率（印は破片が 0 になると消えるので、ここで読む）。
                _reflectRest = armor - soak;
                _reflectRatio = PlankTrait.RatioOf(target.RawCounter(StatusKeys.Plank));
                NotePlankSkill(soak);   // 第210期: 腕の累計（同じ条件・同じ量）
            }
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
                // 第214期（計数のみ）: 感電している駒への一撃を破片が受け切った（起爆しない）。
                if (_shockLive && target.RawCounter(StatusKeys.Shock) > 0) TallyOf(target).ShockArmorMuted++;
                // 第211期: 破片で受け切った一撃。**保持者がいなければ比較1つで抜ける。**
                if ((_thornsLive || _scrapHolders.Count > 0 || _braceArmoredLive) && source is not null && source.TeamId != target.TeamId && !burnTick)
                {
                    ArmorOnlyNow = true;   // 第213期（計数のみ）
                    try { ArmorOnlyHit(target, source, soak); }
                    finally { ArmorOnlyNow = false; }
                }
                return;
            }
        }

        // 第208期（U3）: 破片の段の後ろの分かち。板（破片）が吸いきれなかった残りから4割を取る——中身は上の段と同じ。
        if (lateSharer is not null && lateSharer.IsAlive)
        {
            int taken = amount * SharerTrait.Percent / 100;
            if (taken > 0)
            {
                amount -= taken;
                TallyOf(lateSharer).SharerLate++;
                Log($"    {lateSharer.Name} が {target.Name} の痛みを引き受けた", LogKind.Trigger);
                if (deepBite) TallyOf(target).DeepBiteRelayed++;
                _shareFrom = target;   // 第297期（中継の札・呼び出しの頭で読んで消す）
                ApplyDamage(lateSharer, taken, source, isFriendlyFire: true, burnTick: burnTick, relayed: true, levy: levy);
                _shareFrom = null;
                int dull = taken / SharerTrait.DullDivisor;
                if (dull > 0 && !lateSharer.HasTrait(TraitId.SharerNoDull))
                {
                    TraitMark dm = BeginTrait(TraitId.Sharer, lateSharer);   // 第298期 段0-2（群6）
                    Dull(target, dull, DullRoute.Sharer, lateSharer);
                    AttrEnd(dm, 6, lateSharer, dull);
                    Log($"    痛みを取り上げられた {target.Name} の腕がなまる（攻撃 -{dull}）", LogKind.FriendlyFire);
                }
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
                || target.RawCounter(RedirectGainTrait.PendingKey) > 0)
            && !HoleSkip(target))   // 第229期: 転倒の穴
        {
            target.SetCounter(ParryTrait.StockKey, target.RawCounter(ParryTrait.StockKey) - 1);
            if (LastStandTrait.Drawn(target)) TallyOf(target).LastStandParried++;   // 第198期（計数のみ。第199期の版は残った在庫のぶんだけ立つ）
            // 第199期: 受け流した刃の累計（剣の段の上乗せの元）。**判定の時点の打点**（軛より前）。抜いた後は足さない。
            else target.SetCounter(LastStandTrait.ParriedKey, target.RawCounter(LastStandTrait.ParriedKey) + amount);
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

        // 踏みとどまり（第294期・HS・`BeckonHoldTrait`）。**猶予の直後・同じ出口**（「殺さない」制約の族・軛より前）。
        // 敵の攻撃（相手陣営の出どころ・刻み／徴収／中継／共有／同士討ちではない）の倒れる一撃だけ。HS-d の猶予の間は標が無くても止まる。
        // 標で止めたら標を剥がす。**保持者がいなければ比較1つで抜ける。乱数を引かない。**
        UnitState? holdBy = null;
        if (_holdLive && amount >= target.Hp && source is not null && source.TeamId != target.TeamId
            && !burnTick && !levy && !relayed && !hexShare && !isFriendlyFire)
            holdBy = Hold(target, source);
        if (holdBy is not null) amount = Math.Max(0, target.Hp - 1);
        if (holdBy is not null && amount <= 0)
        {
            if (holdBy.HasTrait(TraitId.BeckonBridge)) BeckonBridgeFire(holdBy, target);
            return;
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
            // 第219期・**計数のみ**。脆さの分のうち上限に切られた量（切り落とした量と脆さの分の小さい方＝上限の見積もり）。
            if (brittleExtra > 0) BrittleBook.YokeCutExtra += Math.Min(brittleExtra, amount - Yoke.Cap);
            amount = Yoke.Cap;
        }
        else if (Yoke.Cap > 0 && amount > Yoke.Cap * 4 / 5 && YokeBinding)
        {
            // 切られなかったが上限に近い一撃（上限が効いている境界を見るため）。**計数のみ。**
            YokeNearHits[YokeSlot(pattern, target)]++;
        }

        // 焼け残り（第234期・`SmolderTrait`）。**逃げ足と同じ HP を引く直前**——破片・受け流し・身構え・軛の後なので本当に倒れる一撃だけが来る。
        // 燃えている間・1戦1回・出どころは問わない。HP1 で止め、火を消す（区間は燃え尽きとして閉じる）。この攻撃では火の鎧の自分への着火をしない。
        if (_fireArmorLive && amount >= target.Hp && target.HasTrait(TraitId.Smolder)
            && target.RawCounter(StatusKeys.Burn) > 0 && target.RawCounter(SmolderTrait.UsedKey) == 0)
        {
            target.SetCounter(SmolderTrait.UsedKey, 1);
            int cut = amount - Math.Max(0, target.Hp - 1);
            amount = Math.Max(0, target.Hp - 1);
            target.SetCounter(StatusKeys.Burn, 0);
            CloseBurnEpisode(target, expired: true);
            if (_fireLvLive) FireOut(target);   // 第242期（火勢: 火が消えたら 0）
            UnitTally st = TallyOf(target);
            st.SmolderUsed++;
            st.SmolderTurn = Turn;
            if (source is not null && FireArmorFrameOf(source) is FireArmorFrame sf && !sf.NoSelf.Contains(target)) sf.NoSelf.Add(target);
            EmitFireArmor(target, target, FireArmorLabels.Smolder, cut);
            Log($"    {target.Name} は焼け残った（残り 1・火が消えた）", LogKind.Highlight);
            if (amount <= 0) return;
        }

        // 必死の逃げ足（第227期・セロの版 L1/L2・`LastDodgeTrait`）。**HP を引く直前**——破片・受け流し・身構え・軛・猶予はすべて上で済んでいるので、
        // 破片で受け切れる一撃はここへ来ない。倒れる一撃（`amount >= Hp`）のうち、回避と同じ「相手陣営の攻撃」だけ。
        // かわしたら入口の回避と同じく `_evadedNow` を立てて返す（その一撃で減った破片などは戻さない）。**回避の保持者がいなければ比較1つで抜ける。**
        if (_evadeLive && amount >= target.Hp && target.HasTrait(TraitId.LastDodge)
            && source is not null && source.TeamId != target.TeamId && !burnTick && !levy && !relayed && !hexShare
            && LastDodgeTrait.TryUse(this, target, source, amount, pattern))
        {
            _evadedNow = target;
            return;
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
        // 第205期。陣営ごとの痛み（同じ実額）。読むのはリリの痛みの版だけ。
        if (hpBefore120 > target.Hp)
        {
            long lost = hpBefore120 - Math.Max(0, target.Hp);
            if (target.TeamId == PlayerTeam) _painLostPlayer += lost; else _painLostEnemy += lost;
        }
        // 燃焼の刻みが実際に削った量（第57期）。**すべての増減を通した後の値**。
        if (burnTick) TallyOf(target).BurnTaken += amount;
        // 第125期 段1。**中継の段が実際に削った量**（巨躯・分かち）。`Swallowed`（名目量）とは別物。
        // **誰も読んで分岐しない。**
        if (relayed) TallyOf(target).Shouldered += amount;
        // 第297期（**計数のみ**）: 分かちが引き受けた実額を、痛みをくれた相手の側に（相手ごとの内訳）。
        if (shareFrom is not null) { TallyOf(shareFrom).SharedAway += amount; TallyOf(target).ShareTakenHits++; }
        // 第179期。**味方が味方から受けたダメージを灰として溜める**（拾い屋のスス）。
        // **HP を引いた直後・死亡判定より手前**——実額で溜め、最後の一撃も落とさない。
        // 保持者が盤上にいなければ `AshBinding` の比較1つで抜ける。
        NoteAsh(target, amount, source, havocExtra, shockNote == 3);
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
            Reaction = InReaction || InInterrupt,
            // 第182期・表示専用。受け渡しの段だけ出どころ（元の被弾者）を載せる
            ShareFromId = hexShare ? _hexShareFrom : null,
            // 第186期・表示専用。逸らしの受け渡しの段だけ、逸らした駒（ソラ）を載せる
            DeflectFromId = deflectFrom?.InstanceId,
            // 第186期 追補・表示専用。この逸らしで積んだ後の溜めの段（突きの保持者だけ）
            ThrustCharge = deflectCharge,
            // 第219期・表示専用。燃焼の脆さで足した分（破片・軛で削られる前の名目）
            BrittleExtra = brittleExtra > 0 ? brittleExtra : null,
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
        if (deflectedHere) tt.DeflectKeptTaken += amount;   // 第186期（計数のみ）
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
        // 第300期（**計数のみ**）: ミサの羽（羽の保持者が出どころ・同じ陣営・徴収 ／ 中継は除く）で受けた実額。矢面の味方（ヒサの標を持つ）かどうかで分ける。羽の保持者がいなければ比較1つで抜ける。
        if (_featherLive && source is not null && source.TeamId == target.TeamId && source != target && !levy && !relayed && source.HasTrait(TraitId.Feathers))
        {
            if (_beckonHolders.Count > 0 && BeckonGuardOf(target) is not null) { tt.FeatherFfBeckonTaken += amount; tt.FeatherFfBeckonHits++; }
            else { tt.FeatherFfOtherTaken += amount; tt.FeatherFfOtherHits++; }
        }
        // 第298期（**計数のみ**・ZN-b の対象）: 味方による同士討ち（徴収・中継・自分は除く）が標の付いた駒に当たった回数を、撃った側に。
        if (source is not null && source.TeamId == target.TeamId && source != target && !levy && !relayed && target.RawCounter(StatusKeys.Marked) > 0)
            TallyOf(source).FfOnMarked++;

        // 殴られて据えの層が積もる（第185期 追補4・FootingTrait.StackOnHit）。**攻撃によるダメージが HP に届いたときだけ**。
        // ここでは積まずに控える（攻撃の枠が閉じたときに1層だけ積む）。**保持者がいなければ比較1つで抜ける。**
        if (FootingTrait.StackOnHit && _shieldHolders.Count > 0 && source is not null && source.TeamId != target.TeamId
            && !burnTick && !levy && !relayed && !hexShare && target.HasTrait(TraitId.Footing))
            QueueFootingHit(target);

        // 第210期（計数のみ）: 応急処置の出番（札が貼る前に数える）。**瓦礫拾いの保持者がいなければ比較1つで抜ける。**
        if (_scrapHolders.Count > 0 && amount > 0) NoteFirstAidChance(target, amount);

        // 第297期: 分かちの版（`ShareBack` ／ `ShareTop` ／ `ShareGift`）が「誰の痛みを引き受けた一撃か」を読む口（`ShareFrom`）。入れ子に備えて退避して戻す。
        UnitState? prevSharerFrom = ShareFrom;
        ShareFrom = shareFrom;
        foreach (Trait t in target.Traits.ToList())
        {
            TraitMark m = this.BeginTrait(t.Id, target);   // 第94期 (T2) の印
            t.OnDamaged(this, target, amount, source);
            this.EndTrait(m);
        }
        ShareFrom = prevSharerFrom;

        // 弾き返し（第228期・突き返しのハネの版 H2/H3・`SpringTrait`）。**敵の攻撃が HP に届いたとき**（入口の回避と同じ条件の攻撃・
        // 状態異常の刻み・徴収・中継・呪いの共有・味方からのダメージは外れる）。ハネが倒れる一撃では弾かない。**保持者がいなければ比較1つで抜ける。**
        if (_springLive && target.Hp > 0 && source is not null && source.IsAlive && source.TeamId != target.TeamId
            && !burnTick && !levy && !relayed && !hexShare && target.HasTrait(TraitId.Spring))
            SpringTrait.Try(this, target, source);
        // 第231期（B・`SpringGuard`）: ハネの隣の味方が同じ条件で殴られたら、ハネが弾く（ハネ自身の分と回数を共有）。
        // 相手は隣（`SpringRow` を持てば同じ列も）の生きている札の保持者（席番号の若い順の最初の1体）。召喚枠の駒が殴られたときは弾かない。**乱数を引かない。**
        else if (_springLive && target.Hp > 0 && source is not null && source.IsAlive && source.TeamId != target.TeamId
            && !burnTick && !levy && !relayed && !hexShare && !FormationRules.IsSummonSlot(target))
        {
            UnitState? guard = LivingMembers(target.TeamId).FirstOrDefault(h => h != target && h.HasTrait(TraitId.Spring)
                && h.HasTrait(TraitId.SpringGuard)
                && (FormationRules.AreAdjacent(h, target) || (h.HasTrait(TraitId.SpringRow) && h.Row == target.Row)));   // 第236期（S3）: 同じ列も
            if (guard is not null) SpringTrait.TryGuard(this, guard, target, source);
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

        // 第294期（HS-c・橋）: 踏みとどまった一撃の通知が済んだ後に、癒し手を手番の外で1度動かす（応急処置など既存の反応の後）。
        if (holdBy is not null)
        {
            if (TallyOf(target).FirstAidReceived > _holdAidBase) TallyOf(holdBy).HoldAided++;   // 計数のみ（同じ一撃でツギの応急処置が先に届いた）
            if (holdBy.HasTrait(TraitId.BeckonBridge)) BeckonBridgeFire(holdBy, target);
        }
        if (holdWatchBy is not null && target.Hp <= 0) TallyOf(holdWatchBy).HoldNextKilled++;   // 計数のみ

        if (target.Hp <= 0)
        {
            // 第211期（計数のみ）: 倒れた一撃を受ける直前の破片（ツギの盤面だけ）。
            if (_scrapHolders.Count > 0) { UnitTally dt = TallyOf(target); dt.DiedCount++; dt.DiedArmorBefore += armorAtEntry211; }
            HandleDeath(target, shockKillerSet ? shockKiller : source);   // 第214期: 放電で倒れた駒の撃破者は連鎖を起こした一撃の主
        }

        // 第214期: 感電の起爆。**HP に届いた一撃**（この一撃で倒れた駒も）だけがここまで来る——破片が受け切った一撃・受け流し・
        // `lethal: false` で 0 になった一撃は上で返っている。**雷（1）は起爆しない。刻み（2 と `burnTick`）は K2 のときだけ。**
        // 感電が1度も書かれていない戦闘は `_shockLive` の比較1つで抜ける。
        if (_shockLive && target.RawCounter(StatusKeys.Shock) > 0)
        {
            bool tick = shockNote == 2 || burnTick;
            bool thunderPop = shockNote == 1 && _thunderPopLive && target.RawCounter(BetrayedShockTrait.ThunderPopKey) > 0;   // 第276期（S1p）
            if (shockNote == 4) { }                                                            // 第288期: 雷霆は起爆しない（計数は `Thunderclap` の側）
            else if (shockNote == 1 && !thunderPop) TallyOf(target).ShockThunderMuted++;          // 計数のみ（自己検査: 雷は起爆しない）
            else if (tick && !_shockTickLive) TallyOf(target).ShockTickMuted++; // 計数のみ（自己検査: K1 の刻みは起爆しない）
            else
            {
                // 第290期・計数のみ: クグの感電が一撃で弾けた（敵に殴られた ／ そのとき組み付いていた）。
                if (_grappleLive && target.HasTrait(TraitId.Grapple))
                {
                    UnitTally kt = TallyOf(target);
                    kt.KuguPopHit++;
                    if (source is not null && source.TeamId != target.TeamId) kt.KuguPopHitFoe++;
                    if (kuguHeldAtEntry) kt.KuguPopHitHeld++;
                }
                ShockTrigger(target, shockKillerSet ? shockKiller : tick ? null : source, tick ? 1 : source is null ? 2 : 0);
            }
        }
        // 第290期（糸）: ほどけた一撃の間だけ控えた糸の先を消す（起爆の段の後）。保持者がいなければ比較1つで抜ける。
        if (_threadLive && target.RawCounter(ThreadTrait.MemoKey) > 0) target.SetCounter(ThreadTrait.MemoKey, 0);

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
                    // 第182期・表示専用。台本の `Damage.ShareFromId` に載せるためだけの退避
                    // （1ホップなので入れ子の受け渡しは起きないが、作法として退避・復帰する）。
                    int? prevShareFrom = _hexShareFrom;
                    _hexShareFrom = target.InstanceId;
                    try
                    {
                        ApplyDamage(other, share, source, isFriendlyFire: ff,
                                    burnTick: burnTick, spillWound: false,
                                    singleHit: true, hexShare: true);
                    }
                    finally { _hexShareFrom = prevShareFrom; }
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
    /// <summary>
    /// 手番の枠（第239期・<b>計数のみ</b>）。<see cref="TakeTurn"/> が verbose のときだけ1手番1件を積む
    /// ——その手番の間に台本（<see cref="Events"/>）へ積まれた出来事の範囲 [<c>EventStart</c>, <c>EventEnd</c>) を持つ。
    /// <b>台本そのものには1件も足していない</b>（手番の絵の単調さを数えるためだけ・誰も読んで分岐しない）。
    /// 再行動は手番の中から <see cref="TakeTurn"/> を呼ぶので、内側の枠が先に積まれ、外側の枠がそれを含む。
    /// </summary>
    public List<HandRecord> Hands { get; } = new();

    public TurnOutcome TakeTurn(UnitState actor)
    {
        // 第244期（残り火）: 印は「ギフトでない次の手番」の入口で消える——その手番の `SwingTurn` だけが残り火を撃つ（動けない手番なら撃たずに消える）。
        // **火勢を持つ陣営がいなければ比較1つで抜ける。**
        UnitState? prevEmbers = _embersNow;
        if (_fireLvLive && !_inGift && actor.RawCounter(FireBurstRule.EmbersKey) > 0)
        {
            actor.SetCounter(FireBurstRule.EmbersKey, 0);
            _embersNow = actor;
        }
        // 第246期（放熱）: 印は「ギフトでない次の手番」の頭で使う（火勢 +1 してから動く）。**印が無ければ比較1つで抜ける。**
        if (_fireLvLive && !_inGift && actor.RawCounter(FireCycleRule.RadiateKey) > 0) UseRadiate(actor);
        TurnOutcome o;
        try { o = TakeTurnFramed(actor); }
        finally { _embersNow = prevEmbers; }
        // 第242期: ヒヨのターンギフト。手番（と手番の枠）が閉じた後に、控えた相手へ通常の手番を1回ずつ渡す。**控えが無ければ比較1つで返る。**
        if (_giftQueue.Count > 0 && !_inGift) DrainGifts();
        if (_mfLive) DrainFeatherMarks();   // 第298期（MF・手番の枠が閉じた後に、控えた羽を撃つ）
        return o;
    }

    TurnOutcome TakeTurnFramed(UnitState actor)
    {
        // 第105期。**中身は1文字も触っていない**——枠だけを被せて
        // 「いま誰の手番か」を立て、帰ってきた種別を数える（観測専用）。
        UnitState? prevActor = TurnActor;
        TurnActor = actor;
        actor.TakenTurn = _turn;   // 第218期・**計数のみ**（感電の痺れが「動く前」だったか）
        if (_rallyLive) BundlePush(actor, outOfTurn: false);   // 第295期（手番の攻撃 ＝ 1つのまとまり・羽が何枚でも1回）
        UnitTally tt = TallyOf(actor);
        tt.TurnsTaken++;
        int handEv0 = _events.Count;   // 第239期・**計数のみ**（手番の枠。verbose のときだけ `Hands` に積む）
        try
        {
            TurnOutcome outcome = TakeTurnCore(actor);
            if (_verbose) Hands.Add(new HandRecord(_turn, actor.InstanceId, actor.TeamId, outcome, handEv0, _events.Count));
            // 第217期（**計数のみ**）: 手番を続けて失った数。見せしめか感電がある戦闘でだけ数える（私有キー・誰も読んで分岐しない）。
            if (_restrainLive || _shockLive)
            {
                int run = outcome == TurnOutcome.Stalled ? actor.RawCounter(ShockRule.StallRunKey) + 1 : 0;
                actor.SetCounter(ShockRule.StallRunKey, run);
                if (run > tt.StallRunMax) tt.StallRunMax = run;
            }
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
        finally { TurnActor = prevActor; if (_rallyLive) BundlePop(); }
    }

    /// <summary>手番の中身（第104期に切り出した本体。第105期に枠を被せた）。</summary>
    private TurnOutcome TakeTurnCore(UnitState actor)
    {
        // 第185期: 竦みのハメ防止の印は「次の自分の手番の頭」で落とす（ShameTrait.GuardKey）。
        if (_restrainLive && actor.RawCounter(ShameTrait.GuardKey) > 0) actor.SetCounter(ShameTrait.GuardKey, 0);
        // 第217期（G3H）: 感電の痺れのハメ防止の印も同じく「次の自分の手番の頭」で落とす。
        if (_shockStunGuard && actor.RawCounter(ShockRule.GuardKey) > 0) actor.SetCounter(ShockRule.GuardKey, 0);

        if (actor.RawCounter(StatusKeys.Stun) > 0)
        {
            if (_restrainLive) ConsumeCowed(actor);   // 第185期: 失う手番に竦みも吸わせる（二重に取らない）
            if (ScapegoatActive) NoteScapegoatSkip(actor);
            // 第93期: 深手は**実際に行動したとき**だけ開く。止められた駒は延命する（§1 の予測）。
            if (DeepWatch) NoteDeepStalled(actor);
            actor.SetCounter(StatusKeys.Stun, 0);
            actor.SetCounter(StatusKeys.IdleTurn, Turn);
            TallyOf(actor).StallStun++;   // 第105期（計数のみ）
            if (actor.RawCounter(ShockRule.StunKey) > 0) { TallyOf(actor).StallShockStun++; actor.SetCounter(ShockRule.StunKey, 0); }   // 第216期（計数のみ）
            if (_shockStunGuard) actor.SetCounter(ShockRule.GuardKey, 1);   // 第217期（G3H）: 痺れが明けた駒は次の自分の手番まで感電で痺れない
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
            if (_restrainLive) ConsumeCowed(actor);   // 第185期
            if (ScapegoatActive) NoteScapegoatSkip(actor);
            if (DeepWatch) NoteDeepStalled(actor);
            // 第145期（表示専用）: 手番を失った瞬間。付与はターン頭なので別の出来事として打つ。
            EmitStagger(actor, StaggerLabels.Lost, null);
            Log($"  {actor.Name} は転んで動けない", LogKind.Status);
            return TurnOutcome.Stalled;
        }

        // 組み付き・竦み（第185期）。**転倒とまったく同じ形で立てる**（engine 側で IdleTurn を立てて Stalled を返す。
        // CanAct は1つも偽にしない）。組み付きは**消費しない**（ほどくのは組み付いた側だけ）、竦みは1回で消える。
        // **保持者がいなければ `_restrainLive` の比較1つで抜ける**——既存の行が 0 件差分であることの根拠。
        if (_restrainLive)
        {
            if (actor.RawCounter(StatusKeys.Grappled) > 0)
            {
                actor.SetCounter(StatusKeys.IdleTurn, Turn);
                ConsumeCowed(actor);
                NoteGrappleStall(actor);
                if (ScapegoatActive) NoteScapegoatSkip(actor);
                if (DeepWatch) NoteDeepStalled(actor);
                Log($"  {actor.Name} は組み付かれて動けない", LogKind.Status);
                return TurnOutcome.Stalled;
            }
            if (actor.RawCounter(StatusKeys.Cowed) > 0)
            {
                actor.SetCounter(StatusKeys.IdleTurn, Turn);
                ConsumeCowed(actor, CowedLabels.Lost);
                TallyOf(actor).StallCowed++;
                if (ScapegoatActive) NoteScapegoatSkip(actor);
                if (DeepWatch) NoteDeepStalled(actor);
                Log($"  {actor.Name} は竦んで動けない", LogKind.Status);
                return TurnOutcome.Stalled;
            }
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
        // 第255期（被弾の燃焼）: 手番の一振り全体を1回の攻撃の枠にする（5連撃・火の雨も1つ）。札が無ければ比較1つで素通り。
        if (OpenBurnHitScope(actor))
        {
            try { SwingTurnFire(actor, act); }
            finally { CloseBurnHitScope(); }
            return;
        }
        SwingTurnFire(actor, act);
    }

    private void SwingTurnFire(UnitState actor, UnitAction? act)
    {
        // 第242期: 燃え広がりの枠（手番の一振り全体で「同じ敵は1回まで」・5連撃も1つ）。**保持者がいなければ比較1つで本体へ直行する。**
        if (_fireLvLive && _fireLvTeams[actor.TeamId])
        {
            int move = BigMoveOf(actor);   // 第244期（大技）: 0 なら第242期の手番のまま
            if (move == 0) NoteHotaStage(actor);
            var sc = new SpreadScope { Actor = actor };
            _spreadScopes.Add(sc);
            try { if (move == 0) SwingTurnBody(actor, act); else BigMove(actor, move); }
            finally { _spreadScopes.RemoveAt(_spreadScopes.Count - 1); }
            ResolveSpread(sc);
            return;
        }
        SwingTurnBody(actor, act);
    }

    private void SwingTurnBody(UnitState actor, UnitAction? act)
    {
        // 第223期: 段2 以上のセロの手番は乱れ撃ち（5本・的は乱数）。**保持者がいなければ比較1つで抜ける。**
        if (_evadeLive && actor.HasTrait(TraitId.Evade) && EvadeTrait.StageOf(actor) >= 2) { Barrage(actor); return; }

        // 第285期: 羽の保持者の手番は羽の一振り（標を追う発と乱射の発を1つの枠で）。**保持者がいなければ比較1つで抜ける。**
        if (_featherLive && actor.HasTrait(TraitId.Feathers)) { FeatherVolley(actor); return; }

        // 第281期: 炸裂の保持者の手番で、敵に標持ちが 0 なら乱射（札 `Spray` の保持者だけ）。**保持者がいなければ比較1つで抜ける。**
        if (_ruptureLive && actor.HasTrait(TraitId.Spray) && !AnyMarkedFoe(actor)) { Spray(actor); return; }

        // 第299期: 仇指し（ザン）の手番。規定では数えるだけで、下の `SwingTurnHits` をそのまま振る。仇巡り（ZM）の札があり標の敵がいれば仇巡り。**保持者がいなければ比較1つで抜ける。**
        if (_vendettaTurnLive && actor.HasTrait(TraitId.Vendetta)) { VendettaTurn(actor, act); return; }

        SwingTurnHits(actor, act);
    }

    private void SwingTurnHits(UnitState actor, UnitAction? act)
    {
        int hits = 1;
        foreach (Trait t in actor.Traits) hits = t.ModifyHitCount(actor, hits);
        if (hits < 1) hits = 1;   // 上限は特性の側。engine が保証するのは「1発は振る」だけ

        // 第277期: 豆鉄砲の一振り（1 点 × 弾数）。**保持者がいなければ比較1つで抜ける。**
        if (_pelletLive && actor.HasTrait(TraitId.Pellet)) { PelletVolley(actor, act, hits); return; }

        // 第242期（ホタの段3 の 5連撃）: 同じ敵に続けて振る——2発目以降は直前の1発の主目標に的を固定する（倒れていれば通常どおり選び直す）。
        bool burst = _fireLvLive && hits > 1 && actor.HasTrait(TraitId.PyreStage);
        _burstLock = null;
        for (int i = 0; i < hits; i++)
        {
            if (!actor.IsAlive) break;
            if (i > 0) TallyOf(actor).ExtraSwings++;   // 第178期・**計数専用**
            if (burst && i > 0 && _burstLock is { IsAlive: true } lk) _forcedTarget = lk;
            if (act is null) PerformAttack(actor);
            else PerformAttack(actor, attackPercent: act.AttackPercent,
                               patternOverride: act.PatternOverride);
        }
    }

    // =====================================================================================
    // 第294期 —— 守りの版（指示書 design/PHASE294_GUARD_SPEC.md §3）。ヒサの踏みとどまり（HS 土台）・橋（HS-c）・猶予（HS-d）／
    // ソラの肩代わり（SR-a）・重圧（SR-b）／ ソムの静電気の膜（SM 土台）・痺れない膜（SM-b）。
    // **保持者がいなければどの口も比較1つで抜ける。乱数を引かない**（`PickOne` ／ `Roll` を新たに呼ばない）。
    // 1戦の中だけの状態（猶予の期限・橋のターン・帯電の書き手）は `BattleContext` の辞書に置く——戦ごとに作り直されるので会戦の境界で消す手間が要らない。
    // =====================================================================================
    bool _holdLive;
    /// <summary>第300期: 矢面は羽も半分（<see cref="TraitId.BeckonFeather"/>）の保持者が盤上にいるか（判定の短絡）。</summary>
    bool _beckonFeatherLive;
    readonly List<UnitState> _wideHolders = new(), _pressureHolders = new(), _membraneHolders = new();
    /// <summary>次の <c>ApplyDamage</c> 1回にだけ効く札: SR-a の肩代わりでソラが受ける段（`ApplyDamageBody` の最初で読んで消す）。</summary>
    bool _wideNext;
    /// <summary>計数のみ: いまの帯電の書き手の種類（膜の保持者がいる戦だけ書く）。</summary>
    readonly Dictionary<int, int> _shockWriterCat = new();
    /// <summary>橋の割り込みの中だけ立つ受け手（<see cref="MostHurtAlly(UnitState, Func{UnitState, bool}?)"/> が読む）。</summary>
    UnitState? _bridgePatient;
    /// <summary>HS-d の猶予: 駒の <c>InstanceId</c> → 猶予が続く「自分の手番」のターン（その手番を終えるまで）。与えた駒は <see cref="_graceUsed"/>（1戦1度）。</summary>
    readonly Dictionary<int, int> _graceFrom = new();
    readonly HashSet<int> _graceUsed = new();
    /// <summary>HS-a′（参考）: 一度踏みとどまった駒の <c>InstanceId</c>。</summary>
    readonly HashSet<int> _holdOnce = new();
    /// <summary>HS-c の橋: ヒサの <c>InstanceId</c> → 最後に架けたターン（1ターンに1度）。</summary>
    readonly Dictionary<int, int> _bridgeTurn = new();
    /// <summary>計数のみ: 踏みとどまった駒 → 与えたヒサ（次の敵の一撃まで見張る）／ 踏みとどまった瞬間の応急処置の受け取り数。</summary>
    readonly Dictionary<int, UnitState> _holdWatch = new();
    long _holdAidBase;

    /// <summary>
    /// 踏みとどまりの判定（<c>ApplyDamageBody</c> の出口・敵の攻撃の倒れる一撃でだけ呼ばれる）。止めるなら与えたヒサを返す（呼び出し側が HP 1 に切る）。
    /// ① HS-d の猶予の間なら標を問わず止める ／ ② ヒサの標（矢面）を持ち、そのヒサが <see cref="TraitId.BeckonHold"/> を持てば止めて標を剥がす
    /// （HS-d のヒサなら、まだ与えていない駒に猶予を与える）。
    /// </summary>
    UnitState? Hold(UnitState target, UnitState source)
    {
        if (_graceFrom.TryGetValue(target.InstanceId, out int from) && GraceActive(target, from))
        {
            UnitState? gby = null;
            foreach (UnitState h in _beckonHolders) if (h.TeamId == target.TeamId && h.HasTrait(TraitId.BeckonGrace)) { gby = h; break; }
            if (gby is not null)
            {
                TallyOf(gby).GraceStops++;
                Log($"    {target.Name} は猶予の中で踏みとどまる（残り 1）", LogKind.Trigger);
                return gby;
            }
        }
        UnitState? holder = BeckonGuardOf(target);
        if (holder is null)
        {
            // 計数のみ: ヒサの記憶はこの駒を指しているが、標が剥がされていて止められなかった（ソラの逸らしなど）。
            foreach (UnitState h in _beckonHolders)
                if (h.TeamId == target.TeamId && h.HasTrait(TraitId.BeckonHold) && h.RawCounter(BeckonTrait.TargetKey) == target.InstanceId + 1) { TallyOf(h).HoldLostStripped++; break; }
            return null;
        }
        if (!holder.HasTrait(TraitId.BeckonHold)) return null;
        // HS-a′（参考）: 同じ味方は1戦に1度だけ（指差し直されても2度目は止めない）。
        if (holder.HasTrait(TraitId.BeckonHoldOnce) && !_holdOnce.Add(target.InstanceId)) { TallyOf(holder).HoldOnceSpent++; return null; }
        target.SetCounter(StatusKeys.Marked, 0);
        // ヒサの記憶も消す（決めたこと）: 記憶が残ると、標を自分で書き直す駒（ソラの逸らしの「自分に付ける」）がヒサの次の手番を待たずに踏みとどまりを
        // 張り直し、1ターン1発の敵（ボス）に対して倒れなくなる（30 ターンの上限まで続く）。標を張り直せるのはヒサの次の指差しだけ。
        holder.SetCounter(BeckonTrait.TargetKey, 0);
        NoteMarkStrip(target);
        UnitTally ht = TallyOf(holder);
        ht.HoldFires++;
        TallyOf(target).HoldReceived++;
        // 計数のみ: このターンに手番の残っている癒し手がいたか（HS-a のまま橋が架かる見込み）・同じ一撃の応急処置の控え・次の一撃の見張り。
        bool left = false, any = false;
        foreach (UnitState u in LivingMembers(target.TeamId))
            if (u.HasTrait(TraitId.Plank) || KissTrait.Holds(u)) { any = true; if (u.TakenTurn < _turn) left = true; }
        if (left) ht.HoldHealerLeft++; else if (!any) ht.HoldNoHealer++;
        _holdAidBase = TallyOf(target).FirstAidReceived;
        _holdWatch[target.InstanceId] = holder;
        Log($"    矢面の {target.Name} は倒れる一撃に踏みとどまった（残り 1・標が剥がれた）", LogKind.Highlight);
        if (holder.HasTrait(TraitId.BeckonGrace) && _graceUsed.Add(target.InstanceId))
        {
            // 次の自分の手番: このターンにまだ動いていなければこのターンの手番、動いた後（か動いている最中）なら次のターンの手番。
            _graceFrom[target.InstanceId] = target.TakenTurn < _turn ? _turn : _turn + 1;
            ht.GraceGranted++;
            Log($"    {target.Name} は次に動き終えるまで倒れない", LogKind.Trigger);
        }
        return holder;
    }

    /// <summary>HS-d の猶予が続いているか: 期限の手番をまだ迎えていない、または期限の手番の最中。</summary>
    bool GraceActive(UnitState u, int from) => u.TakenTurn < from || (u.TakenTurn == from && ReferenceEquals(TurnActor, u));

    /// <summary>
    /// HS-c の橋（<see cref="TraitId.BeckonBridge"/>）。踏みとどまった直後に、味方の癒し手（ツギ ＝ 板を貼る ／ リリ ＝ 施す・席番号の若い方）が手番の外で1度動き、
    /// その味方へ向ける。<b>1ターンに1度</b>（架かったときだけ数える）。割り込みの作法は応急処置と同じ: 割り込み・反撃の中では出ない ／
    /// <c>CanActOutOfTurn(癒し手, Bridge)</c> を通す（痺れ・組み付き・粛で止まる）／ 本体は <c>Interrupt</c> で包む。<b>乱数を引かない</b>（リリの吸う相手の選び方は手番と同じ）。
    /// </summary>
    void BeckonBridgeFire(UnitState hisa, UnitState held)
    {
        UnitTally t = TallyOf(hisa);
        if (!held.IsAlive) return;
        if (_bridgeTurn.TryGetValue(hisa.InstanceId, out int bt) && bt == _turn) { t.BridgeSpent++; return; }
        UnitState? healer = null;
        foreach (UnitState u in LivingMembers(held.TeamId))
            if ((u.HasTrait(TraitId.Plank) || KissTrait.Holds(u)) && (healer is null || u.Slot < healer.Slot)) healer = u;
        if (healer is null) { t.BridgeNoHealer++; return; }
        if (InInterrupt || InReaction) { t.BridgeNested++; return; }
        var foes = LivingMembers(Opponent(held.TeamId));
        if (foes.Count == 0) return;
        if (!CanActOutOfTurn(healer, OutOfTurnRoute.Bridge)) { if (HushBindingNow) t.BridgeHushed++; else t.BridgeBlocked++; return; }
        _bridgeTurn[hisa.InstanceId] = _turn;
        t.BridgeFired++;
        int before = held.Hp + held.RawCounter(StatusKeys.Armor);
        bool plank = healer.HasTrait(TraitId.Plank);
        if (plank) t.BridgeByPlank++; else t.BridgeByKiss++;
        Log($"    踏みとどまった {held.Name} へ {healer.Name} が駆けつける", LogKind.Highlight, healer);
        // 第298期 段0-2（群7）: 橋の中の板 ／ 口づけはツギ ／ リリの出力。印を立てる（跳ね返りの「混ぜ板」の計数 `Mark.Id != Plank` も正しくなる）。
        TraitMark am = BeginTrait(plank ? TraitId.Plank : TraitId.Kiss, healer);
        try
        {
            Interrupt(() =>
            {
                if (plank) PlankTrait.Paste(this, healer, held, foes, firstAid: false);
                else
                {
                    UnitState? prev = _bridgePatient;
                    _bridgePatient = held;
                    try { KissTrait.Act(this, healer, KissTrait.DrainPercent, rite: true); }
                    finally { _bridgePatient = prev; }
                }
            });
        }
        finally { AttrEnd(am, 7, healer, Math.Max(0, held.Hp + held.RawCounter(StatusKeys.Armor) - before)); }
        t.BridgeGain += Math.Max(0, held.Hp + held.RawCounter(StatusKeys.Armor) - before);
    }

    /// <summary>その陣営の生きている膜の保持者（<paramref name="noStun"/> なら痺れない膜を持つ者だけ）。いなければ null。</summary>
    UnitState? MembraneOf(int team, bool noStun = false)
    {
        foreach (UnitState h in _membraneHolders)
            if (h.IsAlive && h.TeamId == team && (!noStun || h.HasTrait(TraitId.MembraneNoStun))) return h;
        return null;
    }

    // ---- 第295期 —— ヒサの「あいつを狙え！」（HK-a `MarkRally` ／ HK-b `MarkRallyWide`・指示書 design/PHASE295_MARK_HEAL_SPEC.md §3）。
    // **攻撃のひとまとまり**は枠で数える: 手番（`TakeTurnFramed`）・反撃（`Reaction`）・割り込み（`Interrupt`）がそれぞれ1つの枠を開いて閉じる。
    // 枠の中で起きたダメージ（巻き込み・貫きの2体目・放電・刻みを含む）は新しい枠を作らず、「標を持つ敵に当たったか」にだけ数える。
    // 枠が閉じたとき、標の敵に当たっていて、その枠の主と同じ陣営に生きている HK のヒサがいれば、最も深い層 × 6 を癒す。**乱数を引かない。**
    bool _rallyLive;
    readonly List<UnitState> _rallyHolders = new();
    sealed class Bundle { public UnitState? Owner; public bool OutOfTurn; public int Layer; }
    readonly List<Bundle> _bundles = new();

    /// <summary>回復の量（層 1 あたり・指示書が<b>測る前に固定</b>した値）。</summary>
    public const int RallyPerLayer = 6;

    void BundlePush(UnitState? owner, bool outOfTurn) => _bundles.Add(new Bundle { Owner = owner, OutOfTurn = outOfTurn });

    void BundleHit(UnitState target, UnitState? source)
    {
        Bundle b = _bundles[^1];
        if (b.Owner is null && source is not null && source.TeamId != target.TeamId) b.Owner = source;
        if (b.Owner is null || b.Owner.TeamId == target.TeamId) return;
        int layer = target.RawCounter(StatusKeys.Marked);
        if (layer > b.Layer) b.Layer = layer;
    }

    void BundlePop()
    {
        Bundle b = _bundles[^1];
        _bundles.RemoveAt(_bundles.Count - 1);
        if (b.Layer <= 0 || b.Owner is null) return;
        UnitTally ot = TallyOf(b.Owner);
        if (b.OutOfTurn) ot.BundleOut++; else ot.BundleTurn++;
        UnitState? hisa = null;
        foreach (UnitState h in _rallyHolders) if (h.IsAlive && h.TeamId == b.Owner.TeamId) { hisa = h; break; }
        if (hisa is null) return;
        UnitTally ht = TallyOf(hisa);
        ht.RallyFires++;
        int amt = b.Layer * RallyPerLayer;
        bool wide = hisa.HasTrait(TraitId.MarkRallyWide);
        UnitState? first = wide ? (b.Owner.IsAlive ? b.Owner : null) : RallyNeediest(hisa, marked: true);
        UnitState? second = wide ? RallyNeediest(hisa, marked: false) : null;
        if (first is null && second is null) { ht.RallyNone++; return; }
        Log($"    {hisa.Name} が叫ぶ——「あいつを狙え！ まだ倒れるな！」", LogKind.Trigger);
        // 第297期 段0: 回復(与) の帰属。`Heal` は配り手を「いま実行中の特性の持ち主」（第94期の印）で数えるので、
        // 印を立てないとザンの仇討ち（反撃の枠）の中で閉じたまとまりの回復がザンに、手番の枠なら誰のものでもない回復に入っていた。
        // **印は観測専用**（盤面・乱数・台本の順は変わらない）。
        TraitMark rm = BeginTrait(wide ? TraitId.MarkRallyWide : TraitId.MarkRally, hisa);
        if (first is not null) RallyHeal(hisa, first, amt, b.Owner, b.Layer, ht, attacker: wide);
        if (second is not null && !ReferenceEquals(second, first)) RallyHeal(hisa, second, amt, b.Owner, b.Layer, ht, attacker: false);
        EndTrait(rm);
    }

    /// <summary>最も傷ついた味方（割合・同値は席番号の若い方・回復を受け付ける・満タンでない）。<paramref name="marked"/> なら標を持つ駒だけ。ヒサ自身は除く
    /// ——<b>第299期: <see cref="TraitId.MarkRallySelf"/>（規定のヒサ）の「最も傷ついた味方」（<paramref name="marked"/> が偽の側）ではヒサ自身も含める</b>。<b>乱数を引かない</b>（`MostHurtAlly` は同値で `PickOne` を引くので使わない）。</summary>
    UnitState? RallyNeediest(UnitState hisa, bool marked)
    {
        UnitState? best = null;
        bool self = !marked && hisa.HasTrait(TraitId.MarkRallySelf);
        foreach (UnitState u in LivingMembers(hisa.TeamId))
        {
            if ((u == hisa && !self) || !u.AcceptsSupport || u.Hp >= u.MaxHp) continue;
            if (marked && u.RawCounter(StatusKeys.Marked) <= 0) continue;
            if (best is null || (long)u.Hp * best.MaxHp < (long)best.Hp * u.MaxHp || ((long)u.Hp * best.MaxHp == (long)best.Hp * u.MaxHp && u.Slot < best.Slot)) best = u;
        }
        return best;
    }

    void RallyHeal(UnitState hisa, UnitState to, int amt, UnitState owner, int layer, UnitTally ht, bool attacker)
    {
        int before = to.Hp;
        Heal(to, amt, hisa);
        int got = Math.Max(0, to.Hp - before);
        ht.RallyHeals++;
        ht.RallyHealed += got;
        ht.RallyOver += amt - got;
        if (ReferenceEquals(to, hisa)) { ht.RallySelfHeals++; ht.RallySelfHealed += got; }   // 第299期（計数のみ・`MarkRallySelf`）
        int cat = to.Def.Id == "sora" ? 0 : hisa.RawCounter(BeckonTrait.TargetKey) == to.InstanceId + 1 ? 1 : to.Def.Id == "zan" ? 2 : attacker ? 3 : 4;
        (ht.RallyTo ??= new long[5])[cat] += got;
        if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.MarkRally, Turn = _turn, ActorId = hisa.InstanceId, TargetId = to.InstanceId, Amount = got, PartnerId = owner.InstanceId, Slot = layer, HpAfter = to.Hp, Team = to.TeamId });
    }

    // ---- 第298期 段1 —— ミサの「指差されたものは、全部撃つ」（MF-a `FeatherMark` ／ MF-b `FeatherMarkLayer`）と
    // ザンの「濡れ衣の仇討ち」（ZN-a `VendettaFrame` ／ ZN-b `VendettaFrameAll`・本体は `VendettaTrait.Frame`）。指示書 design/PHASE298_MARK_FEATHER_SPEC.md §4。
    // 標が増えた瞬間（`UnitState.SetCounter` → `NoteStatusGain` の1点）に控え、手番 ／ 反撃 ／ 割り込みの枠が閉じた後（とターンの頭・開戦の後）に、
    // **割り込み（`Interrupt`・経路 `FeatherMark`・粛で止まる）**として1発ずつ撃つ。羽の発射の中で書かれた標は控えない（同じ連鎖で羽が羽を呼ばない）。
    // **保持者がいなければ `_mfLive` の比較1つで全部抜ける。** 相手選びは乱数を引かない（敵への発は的を固定した `PerformAttack`）。
    bool _mfLive;
    readonly List<UnitState> _mfHolders = new();
    readonly Queue<(UnitState Misa, UnitState Target, bool Fresh)> _mfQueue = new();
    bool _mfFiring;

    /// <summary>標の書き込み（第298期・<b>計数のみ</b>）。書き手 ＝ 第94期の印（`Mark.Owner`）。新しい標か層の追加か、相手が書き手の味方か敵かで分ける。印が無ければ書かれた駒の側の `MarkWriteNoOwner`。</summary>
    void NoteMarkWrite(UnitState u, int delta)
    {
        bool fresh = u.RawCounter(StatusKeys.Marked) - delta <= 0;
        UnitState? w = Mark.Owner;
        if (w is null) { TallyOf(u).MarkWriteNoOwner++; return; }
        UnitTally t = TallyOf(w);
        if (w.TeamId == u.TeamId) { if (fresh) t.MarkWriteAllyFresh++; else t.MarkWriteAllyLayer++; }
        else { if (fresh) t.MarkWriteFoeFresh++; else t.MarkWriteFoeLayer++; }
    }

    void QueueFeatherMark(UnitState u, int delta)
    {
        bool fresh = u.RawCounter(StatusKeys.Marked) - delta <= 0;
        foreach (UnitState misa in _mfHolders)
        {
            if (!misa.IsAlive || ReferenceEquals(misa, u)) continue;
            if (!fresh && !misa.HasTrait(TraitId.FeatherMarkLayer)) continue;
            UnitTally t = TallyOf(misa);
            if (_mfFiring) { t.MfChainSkipped++; continue; }
            if (fresh) t.MfQueuedFresh++; else t.MfQueuedLayer++;
            _mfQueue.Enqueue((misa, u, fresh));
        }
    }

    internal void DrainFeatherMarksPublic() { if (_mfLive) DrainFeatherMarks(); }

    void DrainFeatherMarks()
    {
        if (_mfQueue.Count == 0 || _mfFiring || InInterrupt) return;
        while (_mfQueue.Count > 0)
        {
            var (misa, tgt, fresh) = _mfQueue.Dequeue();
            UnitTally t = TallyOf(misa);
            if (!misa.IsAlive || !tgt.IsAlive) { t.MfDropped++; continue; }
            if (!TeamAlive(PlayerTeam) || !TeamAlive(EnemyTeam)) { t.MfDropped += 1 + _mfQueue.Count; _mfQueue.Clear(); break; }
            if (!CanActOutOfTurn(misa, OutOfTurnRoute.FeatherMark)) { if (HushBindingNow) t.MfHushed++; else t.MfBlocked++; continue; }
            _mfFiring = true;
            try { Interrupt(() => FeatherMarkShot(misa, tgt, fresh, t)); }
            finally { _mfFiring = false; }
        }
    }

    /// <summary>標が付いた駒への羽の1発。敵 ＝ 的を固定した単体の `PerformAttack`（手番の羽と同じ打点・爪痕）／ 味方 ＝ 同士討ちの `ApplyDamage`（攻 × 倍率・爪痕なし・矢面の半減は掛からない）。在庫は減らない。</summary>
    void FeatherMarkShot(UnitState misa, UnitState tgt, bool fresh, UnitTally t)
    {
        if (!misa.IsAlive || !tgt.IsAlive) { t.MfDropped++; return; }
        bool ally = tgt.TeamId == misa.TeamId;
        int before = tgt.Hp;
        if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.FeatherMark, Turn = _turn, ActorId = misa.InstanceId, TargetId = tgt.InstanceId, Amount = tgt.RawCounter(StatusKeys.Marked), Slot = fresh ? 1 : 0, Text = ally ? FeatherMarkLabels.Ally : FeatherMarkLabels.Foe, Team = tgt.TeamId });
        if (t.FeatherVolleys == 0) t.MfBeforeFirstTurn++;
        if (fresh) t.MfFresh++; else t.MfLayer++;
        if (!ally)
        {
            Log($"  {misa.Name} の羽が、指差された {tgt.Name} へ飛ぶ", LogKind.Trigger);
            _forcedTarget = tgt;
            try { PerformAttack(misa, patternOverride: AttackPattern.Single); }
            finally { _forcedTarget = null; _forcedLane = -1; }
            t.MfShotsFoe++;
            t.MfDealtFoe += Math.Max(0, before - Math.Max(0, tgt.Hp));
        }
        else
        {
            int dmg = Math.Max(1, misa.CurrentAttack * Finisher.Multiplier);
            NoteAttackRead(misa);
            Log($"  {misa.Name} の羽が、指差された味方の {tgt.Name} へ飛ぶ（{dmg}）", LogKind.FriendlyFire);
            ApplyDamage(tgt, dmg, misa, isFriendlyFire: true, pattern: AttackPattern.Single);
            int lost = Math.Max(0, before - Math.Max(0, tgt.Hp));
            t.MfShotsAlly++;
            t.MfDealtAlly += lost;
            UnitTally vt = TallyOf(tgt);
            vt.MfTaken += lost; vt.MfTakenHits++;
            if (!tgt.IsAlive) t.MfAllyKills++;
        }
    }

    /// <summary>濡れ衣の仇討ちで指差すヒサ（ザンと同じ陣営に生きている矢面の保持者・席番号の若い方）。いなければ null。</summary>
    public UnitState? FrameAccuser(UnitState zan)
    {
        UnitState? best = null;
        foreach (UnitState u in LivingMembers(zan.TeamId))
            if (u.HasTrait(TraitId.Beckon) && (best is null || u.Slot < best.Slot)) best = u;
        return best;
    }

    /// <summary>ヒサが指差す敵: 標の層が最も深い → 現在の攻撃力が最も高い → 席番号の若い方。<b>乱数を引かない。</b></summary>
    public UnitState? FramePick(UnitState zan)
    {
        UnitState? best = null;
        foreach (UnitState f in LivingMembers(Opponent(zan.TeamId)))
        {
            if (best is null) { best = f; continue; }
            int a = f.RawCounter(StatusKeys.Marked), b = best.RawCounter(StatusKeys.Marked);
            if (a > b || (a == b && (f.CurrentAttack > best.CurrentAttack || (f.CurrentAttack == best.CurrentAttack && f.Slot < best.Slot)))) best = f;
        }
        return best;
    }

    public void NoteFrameNoAccuser(UnitState zan) => TallyOf(zan).FrameNoAccuser++;

    public void NoteFramed(UnitState hisa, UnitState zan, UnitState foe, UnitState ally, UnitState shooter)
    {
        TallyOf(hisa).FrameAccuses++;
        UnitTally zt = TallyOf(zan);
        zt.FrameVendettas++;
        if (shooter.HasTrait(TraitId.Feathers)) zt.FrameByFeather++;
        Log($"    {hisa.Name} が叫ぶ——「あいつがやった！」（{foe.Name} を指差す）", LogKind.Trigger);
        if (_verbose)
        {
            Emit(new BattleEvent { Kind = BattleEventKind.Framed, Turn = _turn, ActorId = hisa.InstanceId, TargetId = foe.InstanceId, PartnerId = ally.InstanceId, Text = FramedLabels.Accuse, Team = foe.TeamId });
            Emit(new BattleEvent { Kind = BattleEventKind.Framed, Turn = _turn, ActorId = zan.InstanceId, TargetId = foe.InstanceId, PartnerId = shooter.InstanceId, Text = FramedLabels.Vendetta, Team = foe.TeamId });
        }
    }

    public void NoteFrameDealt(UnitState zan, int dealt) => TallyOf(zan).FrameDealt += dealt;

    // ---- 第299期 段1 —— ザンの手番「仇巡り」（ZM-a `VendettaRound` ／ ZM-1 `VendettaRoundOne`・指示書 design/PHASE299_ZAN_ROUND_SPEC.md §4）。
    // 手番で、標を持つ敵が1体でも生きていれば、普通の攻撃の代わりに、標を持つ敵を**層の深い順（同じなら席番号）**に巡って、
    // ZM-a はその敵の層の数だけ・ZM-1 は1太刀ずつ斬る。合計は `VendettaTrait.RoundCap`（8）まで・2周目はしない・的が倒れたら次の敵へ。
    // 1太刀 ＝ 的を固定した単体の `PerformAttack`（**介入の鎖を通さない**＝庇う・後備え・挑発が掛からない・前列の制限も受けない）。
    // 太刀の数は `ModifyHitCount` を通さない（あの窓口は「回数」しか返さず、的を巡れない）——手番の外の連鎖を増やさないのは同じ（手番の中だけ）。
    // 標を消費しない・新しい標を書かない（太刀は普通の攻撃）・返り血は付かない（仇討ちの代金のまま）。手番の枠（`TakeTurnFramed`）の中なので、叫びは1手番に1回。
    // 巡る順と的は手番の頭で決める（層は手番の頭の値）。**乱数を引かない。** 規定（札なし）は数えるだけ。
    bool _vendettaTurnLive;

    long FoeHpLeft(UnitState actor)
    {
        long s = 0;
        int opp = Opponent(actor.TeamId);
        foreach (UnitState u in _units) if (u.TeamId == opp && u.Hp > 0) s += u.Hp;
        return s;
    }

    void VendettaTurn(UnitState actor, UnitAction? act)
    {
        UnitTally t = TallyOf(actor);
        var marked = new List<(UnitState Foe, int Layer)>();
        foreach (UnitState f in LivingMembers(Opponent(actor.TeamId)))
        {
            int l = f.RawCounter(StatusKeys.Marked);
            if (l > 0) marked.Add((f, l));
        }
        marked.Sort((a, b) => a.Layer != b.Layer ? b.Layer.CompareTo(a.Layer) : a.Foe.Slot.CompareTo(b.Foe.Slot));
        int cap = VendettaTrait.RoundCap, layers = 0;
        foreach (var m in marked) layers += m.Layer;
        t.ZanTurns++;
        if (marked.Count == 0) t.ZanTurnNoMarked++;
        t.ZanTurnMarkedFoes += marked.Count; t.ZanTurnLayers += layers;
        t.ZanPlanA += Math.Min(cap, layers); t.ZanPlan1 += Math.Min(cap, marked.Count);
        if (layers >= cap) t.ZanPlanACapped++;
        if (marked.Count >= cap) t.ZanPlan1Capped++;

        long hp0 = FoeHpLeft(actor);
        bool roundA = actor.HasTrait(TraitId.VendettaRound), round1 = !roundA && actor.HasTrait(TraitId.VendettaRoundOne);
        if (marked.Count == 0 || (!roundA && !round1))
        {
            SwingTurnHits(actor, act);
            t.ZanTurnDealt += hp0 - FoeHpLeft(actor);
            return;
        }

        int planned = 0, foes = 0;
        foreach (var m in marked)
        {
            if (planned >= cap) break;
            planned += Math.Min(cap - planned, roundA ? m.Layer : 1);
            foes++;
        }
        t.RoundTurns++;
        Log($"  {actor.Name} が仇を巡る（{foes} 体・{planned} 太刀）", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.VendettaRound, Turn = _turn, ActorId = actor.InstanceId, Amount = planned, Slot = foes, Text = VendettaRoundLabels.Start, Team = actor.TeamId });

        int n = 0, visited = 0;
        int opp = Opponent(actor.TeamId);
        foreach (var (foe, layer) in marked)
        {
            if (n >= cap || !actor.IsAlive || !TeamAlive(opp)) break;
            if (!foe.IsAlive) continue;   // 的が倒れていたら次の敵へ
            int k = roundA ? layer : 1;
            bool any = false;
            for (int j = 0; j < k && n < cap; j++)
            {
                if (!actor.IsAlive || !foe.IsAlive || !TeamAlive(opp)) break;
                n++; any = true;
                t.RoundSlashes++;
                if (!TargetPool(actor).Contains(foe)) t.RoundCrossed++;
                if (foe.Row == Row.Back) t.RoundBack++;
                if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.VendettaRound, Turn = _turn, ActorId = actor.InstanceId, TargetId = foe.InstanceId, Amount = planned, Slot = n, Text = VendettaRoundLabels.Slash, Team = foe.TeamId });
                _forcedTarget = foe;
                try
                {
                    if (act is null) PerformAttack(actor, patternOverride: AttackPattern.Single);
                    else PerformAttack(actor, attackPercent: act.AttackPercent, patternOverride: AttackPattern.Single);
                }
                finally { _forcedTarget = null; _forcedLane = -1; }
            }
            if (any) visited++;
        }
        if (n >= cap) t.RoundCapped++;
        t.RoundFoes += visited;
        (t.RoundSlashHist ??= new long[cap + 1])[Math.Min(cap, n)]++;
        t.RoundDealt += hp0 - FoeHpLeft(actor);
    }

    // ---- 第298期 段0-2 —— 戦績の帰属のずれ（第297期 §2-3 の 8 群）。engine の中から別の駒のために回復・強化・弱体・破片を出す所で、
    // 本当の出どころの印を立てる（`BeginTrait` ／ `EndTrait`・**観測専用**）。`AttrEnd` は印を戻し、包む前の印が別の駒（か誰でもない）を
    // 指していた量を**計数だけ**する（`UnitTally.AttrFixed` ／ `AttrFromNone` ／ `AttrStolen`・群の番号 1〜8）。
    int HealOutOf(UnitState u) { UnitTally t = TallyOf(u); return t.HealOutInTurn + t.HealOutOffTurn; }

    void AttrEnd(TraitMark prev, int g, UnitState src, long amount)
    {
        EndTrait(prev);
        if (amount <= 0) return;
        UnitTally t = TallyOf(src);
        (t.AttrTotal ??= new long[9])[g] += amount;
        if (ReferenceEquals(prev.Owner, src)) return;
        (t.AttrFixed ??= new long[9])[g] += amount;
        if (prev.Owner is null) (t.AttrFromNone ??= new long[9])[g] += amount;
        else (TallyOf(prev.Owner).AttrStolen ??= new long[9])[g] += amount;
    }

    void TickHealAttr(UnitState u)
    {
        // 第298期 段0-2（群5）: 耐火の枝の回復（ノブ `Ember.TickHeal`・既定 0 で不活性）は本人の札の出力。
        int h0 = HealOutOf(u);
        TraitMark am = BeginTrait(u.HasTrait(TraitId.Pyre) ? TraitId.Pyre : TraitId.FireArmor, u);
        Heal(u, Ember.TickHeal);
        AttrEnd(am, 5, u, HealOutOf(u) - h0);
    }

    // ---- 第297期 —— 分かちのドハの版（DH-a `ShareBack` ／ DH-b `ShareTop` ／ DH-t `ShareGift`・指示書 design/PHASE297_DOHA_SHARE_SPEC.md §3）。
    // 肩代わり（4割）は規定と1ビットも違わない。変わるのは「溜まる力（痛み ÷ 2）の行き先」だけで、本体は `SharerTrait.OnDamaged`。
    // **乱数を引かない**（相手選びは攻撃力 → 席番号）。版の札を持つ駒がいない戦では、ここは1行も走らない。
    UnitState? _shareFrom;

    /// <summary>いま <c>OnDamaged</c> を受けている一撃が分かちの中継なら、その痛みをくれた相手（それ以外は null）。<c>ApplyDamage</c> の <c>OnDamaged</c> の走査の間だけ立つ。</summary>
    public UnitState? ShareFrom { get; private set; }

    /// <summary>味方で現在の攻撃力が最も高い1体（<paramref name="doha"/> を除く・支援を拒む駒を除く・同値は席番号の若い方）。<b>乱数を引かない。</b></summary>
    public UnitState? ShareTopAlly(UnitState doha)
    {
        UnitState? best = null;
        foreach (UnitState u in LivingMembers(doha.TeamId))
        {
            if (u == doha || !u.AcceptsSupport) continue;
            if (best is null || u.CurrentAttack > best.CurrentAttack || (u.CurrentAttack == best.CurrentAttack && u.Slot < best.Slot)) best = u;
        }
        return best;
    }

    /// <summary>力を配る（DH-a ／ DH-b）。<b>他者強化の窓口 <see cref="Whet"/> を通す。</b> 支援を拒む駒かどうかは呼び出し側で見る。</summary>
    public void ShareGive(UnitState doha, UnitState to, int gain)
    {
        int before = to.AtkBonus;
        TraitMark m = BeginTrait(TraitId.Sharer, doha);
        Whet(to, gain, WhetRoute.Share);
        EndTrait(m);
        UnitTally dt = TallyOf(doha);
        dt.ShareGives++; dt.ShareGiven += gain;
        TallyOf(to).ShareGot += gain;
        Log($"    {doha.Name} が引き受けた痛みを {to.Name} の力に変えた（攻撃 +{gain} → {to.CurrentAttack}）", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.ShareGive, Turn = _turn, ActorId = doha.InstanceId, TargetId = to.InstanceId, Amount = gain, Slot = to.AtkBonus - before, Text = ShareGiveLabels.Power, Team = to.TeamId });
    }

    /// <summary>
    /// DH-t: 肩代わりした実額 <paramref name="dmg"/> を累計し、<see cref="SharerTrait.GiftEvery"/>（最大HPの半分）に達するたび、
    /// 攻撃力の最も高い味方（ドハを除く）へ手番を1回控える（ヒヨの火を渡すと同じ口 `_giftQueue`）。1ターンに1回まで・端数は持ち越す
    /// （上限で止まった回は累計を減らさず、次のターンの最初の肩代わりで撃つ）。
    /// </summary>
    public void ShareGiftAccrue(UnitState doha, int dmg)
    {
        int every = SharerTrait.GiftEvery(doha);
        int pool = doha.RawCounter(SharerTrait.GiftPoolKey) + dmg;
        doha.SetCounter(SharerTrait.GiftPoolKey, pool);
        UnitTally dt = TallyOf(doha);
        dt.ShareGiftAccrued += dmg;
        if (pool < every) return;
        if (doha.RawCounter(SharerTrait.GiftTurnKey) == _turn + 1) { dt.ShareGiftCapped++; return; }
        UnitState? to = ShareTopAlly(doha);
        if (to is null) { dt.ShareGiftNoTarget++; return; }
        doha.SetCounter(SharerTrait.GiftPoolKey, pool - every);
        doha.SetCounter(SharerTrait.GiftTurnKey, _turn + 1);
        dt.ShareGifts++;
        TallyOf(to).ShareGiftGot++;
        _giftQueue.Enqueue((doha, to, 1));
        Log($"    {doha.Name} の痛みが積もった——{to.Name} を先に行かせる", LogKind.Highlight, doha);
        if (_verbose) Emit(new BattleEvent { Kind = BattleEventKind.ShareGive, Turn = _turn, ActorId = doha.InstanceId, TargetId = to.InstanceId, Amount = every, Slot = 0, Text = ShareGiveLabels.Gift, Team = to.TeamId });
    }

    /// <summary>膜で新しく帯電させた数（<see cref="StaticMembraneTrait"/> だけが呼ぶ・<b>計数のみ</b>）。</summary>
    public void NoteMembraneSpread(UnitState som, int n) => TallyOf(som).MembraneSpread += n;

    // =====================================================================================
    // 第277期 —— 豆鉄砲（`PelletTrait`・ノミの転生の版 N1 ／ N2）。**保持者がいなければ `_pelletLive` の比較1つで全部抜ける。乱数を引かない。**
    // 一振りの枠（`Volley`）を立てて、1発ずつ独立した `PerformAttack` を弾数だけ呼ぶ。枠が持つのは「いま何発目か」（刻みの N1 が読む）と
    // 「この一振りで再行動したか」（1振り1回の上限・`NoteEncore` が読む）の2つだけ。再行動の一振りは入れ子の別の枠（終われば外の枠に戻る）。
    // =====================================================================================
    bool _pelletLive;

    // =====================================================================================
    // 第281期 —— 炸裂・爪痕・乱射（`RuptureTrait` ／ `RuptureScarTrait` ／ `SprayTrait`・トメの転生の版 T1 ／ T2）と、敵側の標の層。
    // **保持者がいなければ `_ruptureLive` の比較1つで全部抜ける**（層の書き込みも従来どおり 1 にする）。乱射だけが乱数を引く（保持者の戦だけ）。
    // =====================================================================================
    bool _ruptureLive;
    UnitState? _ruptureTarget;
    int _ruptureHpBefore;

    /// <summary>敵側の標を層にする戦か（炸裂の保持者が盤上にいる）。書き手（逸らしの焦点・仇指し）は <see cref="LayerMark"/> を通す。</summary>
    public bool MarkLayers => _ruptureLive;

    /// <summary>
    /// 標を書く（第281期）。<see cref="MarkLayers"/> が真で、相手が<b>敵陣営</b>（プレイヤーの相手）で既に標があるなら層を1つ足す。
    /// それ以外は従来どおり 1 にする（＝ <c>SetCounter(Marked, 1)</c> と1ビットも違わない）。
    /// </summary>
    public void LayerMark(UnitState u, UnitState writer)
    {
        if (_featherLive) GainFeathers(u, writer);   // 第285期（保持者がいなければ比較1つで抜ける）
        int cur = u.RawCounter(StatusKeys.Marked);
        if (_ruptureLive && u.TeamId != PlayerTeam && cur > 0)
        {
            u.SetCounter(StatusKeys.Marked, cur + 1);
            TallyOf(writer).MarkLayerAdds++;   // 計数のみ
            EmitMarkLayer(writer, u, cur, cur + 1);   // 第291期・表示専用
            return;
        }
        u.SetCounter(StatusKeys.Marked, 1);
        if (_ruptureLive && u.TeamId != PlayerTeam && cur == 0) EmitMarkLayer(writer, u, 0, 1);   // 第291期・表示専用（層が意味を持つ戦だけ）
    }

    bool AnyMarkedFoe(UnitState actor)
    {
        foreach (UnitState f in LivingMembers(Opponent(actor.TeamId)))
            if (f.RawCounter(StatusKeys.Marked) > 0) return true;
        return false;
    }

    void NoteRupture(UnitState actor, int layers, bool crossed, bool feather = false)
    {
        UnitTally t = TallyOf(actor);
        t.RuptureFires++;
        if (crossed) t.RuptureCross++;
        t.RuptureLayerSum += layers;
        if (layers > t.RuptureLayerMax) t.RuptureLayerMax = layers;
        (t.RuptureLayerHist ??= new long[10])[Math.Min(layers, 9)]++;
        if (feather) Log($"    {actor.Name} の羽が標を捉えた（× {Finisher.Multiplier}・層 {layers}）", LogKind.Trigger);
        else Log($"    {actor.Name} の炸裂（層 {layers} × {Finisher.Multiplier}）", LogKind.Trigger);
    }

    /// <summary>
    /// 炸裂の後始末（<see cref="RuptureTrait.OnAfterAttack"/> だけが呼ぶ）。この一撃が炸裂なら、爪痕（札 <see cref="TraitId.RuptureScar"/>）と消費。
    /// 爪痕は<b>実際に減らした HP</b>（肩代わり・上限・破片の後）と同量。下限 1・現在HPは新しい最大HPで切る（縫いの2行と同じ）。
    /// </summary>
    public void RuptureAfter(UnitState self, UnitState target)
    {
        if (!ReferenceEquals(_ruptureTarget, target)) return;
        _ruptureTarget = null;
        UnitTally t = TallyOf(self);
        int lost = Math.Max(0, _ruptureHpBefore - Math.Max(0, target.Hp));
        t.RuptureDealt += lost;
        if (!target.IsAlive) t.RuptureKills++;
        if (self.HasTrait(TraitId.RuptureScar) && target.IsAlive && lost > 0)
        {
            int maxBefore = target.MaxHp;
            target.MaxHp = Math.Max(1, target.MaxHp - lost);
            target.Hp = Math.Min(target.Hp, target.MaxHp);
            t.RuptureScar += maxBefore - target.MaxHp;
            (t.RuptureScarByTurn ??= new long[21])[Math.Clamp(_turn, 0, 20)] += maxBefore - target.MaxHp;
            Log($"    {target.Name} に塞がらない爪痕が残った（最大HP {maxBefore} → {target.MaxHp}）", LogKind.Trigger);
            if (maxBefore > target.MaxHp) EmitScar(self, target, maxBefore - target.MaxHp);   // 第291期・表示専用
        }
        if (!Finisher.Consume || self.HasTrait(TraitId.RuptureKeep)) return;   // 第282期: 層を残す札（T1n）
        target.SetCounter(StatusKeys.Marked, 0);
        t.RuptureConsumed++;
        NoteMarkConsumed(target);   // 第150期の帳簿（計数のみ）
    }

    /// <summary>周期の版（T2）の術の手番。周期を持たない版と同じ一振り（乱射の分岐を含む）。</summary>
    public void RuptureSkill(UnitState actor)
    {
        if (!actor.IsAlive) return;
        SwingTurn(actor, null);
    }

    /// <summary>
    /// 乱射（第281期・<see cref="SprayTrait"/>）。<see cref="SprayTrait.Shots"/> 発・1発 ＝ 攻 × <see cref="SprayTrait.Percent"/>%（床 1）。
    /// 的は自分以外の生存全駒（<c>AllUnits</c> の並び）から等確率・毎発独立（<c>Roll</c>）。各発は標の段だけを通す
    /// ——的の陣営に標持ちがいて、的がそれでなければ <see cref="MarkPullPercent"/>% で引かれる（引く先は層が深い → 席番号の順・乱数を引かない）。
    /// <c>ApplyDamage</c> を直に呼ぶ（味方なら <c>isFriendlyFire</c>）。<c>PerformAttack</c> を通らないので庇い・介入・<c>OnAfterAttack</c> は走らない。標は消さない。
    /// </summary>
    void Spray(UnitState actor)
    {
        UnitTally t = TallyOf(actor);
        t.SprayTurns++;
        int dmg = Math.Max(1, actor.CurrentAttack * SprayTrait.Percent / 100);
        NoteAttackRead(actor);
        Log($"  {actor.Name} は指差す者がおらず、敵味方構わず乱射した", LogKind.FriendlyFire);
        for (int i = 1; i <= SprayTrait.Shots; i++)
        {
            if (!actor.IsAlive) break;
            if (!SprayShot(actor, i, dmg, t)) break;
        }
    }

    /// <summary>乱射の1発（第285期に <see cref="Spray"/> から切り出した・中身は1行も変えていない）。的が1体もいなければ偽を返す（撃たない）。</summary>
    bool SprayShot(UnitState actor, int i, int dmg, UnitTally t)
    {
        {
            var cands = AllUnits.Where(u => u.IsAlive && u != actor).ToList();
            if (cands.Count == 0) return false;
            UnitState pick = cands.Count == 1 ? cands[0] : cands[Roll(cands.Count)];
            UnitState? pull = null;
            foreach (UnitState u in LivingMembers(pick.TeamId))
            {
                if (u == actor || u.RawCounter(StatusKeys.Marked) <= 0) continue;
                if (pull is null || u.RawCounter(StatusKeys.Marked) > pull.RawCounter(StatusKeys.Marked)
                    || (u.RawCounter(StatusKeys.Marked) == pull.RawCounter(StatusKeys.Marked) && u.Slot < pull.Slot)) pull = u;
            }
            if (pull is not null && pull != pick && Roll(100) < MarkPullPercent)
            {
                t.SprayPulled++;
                Log($"    乱射の {i} 発目は指差された {pull.Name} へ逸れた", LogKind.Trigger);
                pick = pull;
            }
            bool ally = pick.TeamId == actor.TeamId;
            int before = pick.Hp;
            Log($"    乱射（{i} 発目）が {pick.Name} へ（{dmg}）", ally ? LogKind.FriendlyFire : LogKind.Damage);
            ApplyDamage(pick, dmg, actor, isFriendlyFire: ally, pattern: AttackPattern.Single);
            int lost = Math.Max(0, before - Math.Max(0, pick.Hp));
            if (ally) { t.SprayAlly++; t.SprayAllyDealt += lost; if (!pick.IsAlive) t.SprayAllyKills++; }
            else { t.SprayFoe++; t.SprayFoeDealt += lost; }
        }
        return true;
    }

    // =====================================================================================
    // 第285期 —— 羽（`FeathersTrait` ／ `FeatherLossTrait`・ミサの連射化の版 M-a ／ M-b）。
    // **保持者がいなければ `_featherLive` の比較1つで全部抜ける。** 標を追う発は乱数を引かない（標の段の `Preferred` の同値割りだけ）。乱射の発だけが引く。
    // =====================================================================================
    bool _featherLive;
    /// <summary>直前の羽の1発の的（流れた発の計数だけが読む・<b>計数専用</b>）。</summary>
    UnitState? _featherLast;

    /// <summary>敵に標が書かれた（<see cref="LayerMark"/> の頭）。書き手の陣営の羽の保持者の羽を1枚ずつ増やす。味方への標では増えない。</summary>
    void GainFeathers(UnitState marked, UnitState writer)
    {
        if (marked.TeamId == writer.TeamId) return;
        foreach (UnitState h in LivingMembers(writer.TeamId))
        {
            if (!h.HasTrait(TraitId.Feathers)) continue;
            h.SetCounter(FeathersTrait.ExtraKey, h.RawCounter(FeathersTrait.ExtraKey) + 1);
            UnitTally t = TallyOf(h);
            t.FeatherGained++;
            int f = FeathersTrait.Count(h);
            if (f > t.FeatherMax) t.FeatherMax = f;
            Log($"    {h.Name} の羽が1枚増えた（{f} 枚）", LogKind.Trigger);
            EmitFeather(FeatherLabels.Gain, writer, h, f, partner: marked);   // 第291期・表示専用
        }
    }

    /// <summary>
    /// 羽の一振り（第285期）。羽の枚数だけ1発ずつ: 敵に標持ちがいれば単体の <c>PerformAttack</c>（的は標の段・列越え・1発 ＝ 攻 × 倍率）、
    /// いなければ乱射の1発（<see cref="SprayShot"/>）。敵が全滅したら残りは撃たない。M-b は乱射した数だけ羽を失う（下限 1）。
    /// 豆鉄砲と同じ一振りの枠（<see cref="Volley"/>）を立てる——再行動は1振り1回、<see cref="VolleyShotOf"/> は何発目か。
    /// </summary>
    void FeatherVolley(UnitState actor)
    {
        UnitTally t = TallyOf(actor);
        int f = FeathersTrait.Count(actor);
        t.FeatherVolleys++;
        t.FeatherShots += f;
        if (f > t.FeatherMax) t.FeatherMax = f;
        Volley? prev = _volley;
        var v = new Volley { Actor = actor };
        _volley = v;
        int chased = 0, sprayed = 0, dmg = 0;
        _featherLast = null;
        EmitFeather(FeatherLabels.Volley, actor, actor, f);   // 第291期・表示専用（見出し）
        try
        {
            for (int i = 0; i < f; i++)
            {
                if (!actor.IsAlive) break;
                if (!TeamAlive(Opponent(actor.TeamId))) break;
                v.Shot = i;
                if (AnyMarkedFoe(actor))
                {
                    if (_featherLast is { IsAlive: false }) t.FeatherFlow++;
                    // 第291期・表示専用: 1発ごとの札（前の的が倒れていれば「流れた」）。直後にこの1発の Attack ／ Damage。
                    if (_featherLast is { IsAlive: false } fell) EmitFeather(FeatherLabels.Flow, actor, null, f, i + 1, partner: fell);
                    else EmitFeather(FeatherLabels.Chase, actor, null, f, i + 1);
                    PerformAttack(actor);
                    chased++;
                    continue;
                }
                if (sprayed == 0)
                {
                    if (chased > 0)
                    {
                        t.FeatherTurned++;
                        Log($"  {actor.Name} の追う標が尽き、残りの羽が敵味方構わず飛んだ", LogKind.FriendlyFire);
                    }
                    else Log($"  {actor.Name} は指差す者がおらず、羽が敵味方構わず飛んだ", LogKind.FriendlyFire);
                    t.SprayTurns++;
                    dmg = Math.Max(1, actor.CurrentAttack * SprayTrait.Percent / 100);
                    NoteAttackRead(actor);
                }
                EmitFeather(FeatherLabels.Spray, actor, null, f, i + 1, remaining: sprayed + 1);   // 第291期・表示専用（直後に Damage）
                if (!SprayShot(actor, i + 1, dmg, t)) break;
                sprayed++;
            }
        }
        finally { _volley = prev; _featherLast = null; }
        t.FeatherChased += chased;
        t.FeatherSprayed += sprayed;
        if (sprayed > 0 && actor.HasTrait(TraitId.FeatherLoss))
        {
            int extra = actor.RawCounter(FeathersTrait.ExtraKey);
            int lost = Math.Min(extra, sprayed);
            if (lost > 0)
            {
                actor.SetCounter(FeathersTrait.ExtraKey, extra - lost);
                t.FeatherLost += lost;
                EmitFeather(FeatherLabels.Lost, actor, actor, FeathersTrait.Count(actor), lost);   // 第291期・表示専用
                Log($"    {actor.Name} の羽が {lost} 枚戻らなかった（{FeathersTrait.Count(actor)} 枚）", LogKind.Trigger);
            }
        }
    }

    sealed class Volley
    {
        public required UnitState Actor { get; init; }
        public int Shot;
        public bool Encored;
    }
    Volley? _volley;

    /// <summary>いまの一振りが <paramref name="u"/> の豆鉄砲の一振りなら何発目か（0 始まり）。一振りの外・他人の一振りなら 0。</summary>
    public int VolleyShotOf(UnitState u) => _volley is { } v && v.Actor == u ? v.Shot : 0;

    void PelletVolley(UnitState actor, UnitAction? act, int hits)
    {
        Volley? prev = _volley;
        var v = new Volley { Actor = actor };
        _volley = v;
        UnitTally pt = TallyOf(actor);
        pt.PelletVolleys++;   // 計数のみ
        try
        {
            for (int i = 0; i < hits; i++)
            {
                if (!actor.IsAlive) break;
                v.Shot = i;
                if (i > 0) pt.ExtraSwings++;   // 第178期・**計数専用**
                pt.PelletShots++;
                if (act is null) PerformAttack(actor);
                else PerformAttack(actor, attackPercent: act.AttackPercent, patternOverride: act.PatternOverride);
            }
        }
        finally { _volley = prev; }
    }

    private void HandleDeath(UnitState dead, UnitState? killer)
    {
        dead.Hp = 0;
        if (_webLive && dead.RawCounter(StatusKeys.Web) > 0) dead.SetCounter(StatusKeys.Web, 0);   // 第293期: 糸の掛かった敵が倒れたら糸は消える
        NoteBurnLink(dead, killer);   // 第233期・**計数のみ**（燃焼の繋ぎの見込み）
        if (_evadeLive && dead.HasTrait(TraitId.Evade))   // 第223期・**計数のみ**（倒れた一撃の種類）
        {
            UnitTally et = TallyOf(dead);
            (et.EvDeathBy ??= new int[5])[_evadeHitClass]++;
            et.EvDeathTurn += _turn;
        }
        TallyOf(dead).Deaths++;
        (TallyOf(dead).DeathTurnHist ??= new long[7])[Math.Clamp(_turn, 0, 6)]++;   // 第216期・**計数のみ**
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

        NoteDeathStock(dead);   // 第220期・**計数のみ**（倒れた瞬間の毒・印・隣）
        bool handedOff = false;   // 第220期・計数のみ（疫みと重なったか）
        // 第218期: 倒れた瞬間に持っていた濃縮の印（**計数のみ**）と、倒れたら移る（M5・敵だけ）。印が1つも無い戦闘は旗1本で抜ける。
        if (_markLive)
        {
            int cm = dead.RawCounter(StatusKeys.Concentrated);
            if (cm > 0)
            {
                TallyOf(dead).ConcMarksAtDeath += cm;
                // 第220期: 澱みが爆ぜる（①爆ぜる → ②印が移る を `EnqueueBurst` の中で行う）。札が無ければ比較1つで抜ける。
                if (_burstMode != 0 && _mireHolder is not null && (_burstAll || dead.TeamId != _mireHolder.TeamId))
                {
                    handedOff = _mireHandoff && dead.TeamId != _mireHolder.TeamId;
                    EnqueueBurst(dead, cm, killer);
                }
                else if (_mireHandoff && _mireHolder is not null && dead.TeamId != _mireHolder.TeamId) { MireHandoff(dead, cm); handedOff = true; }
            }
        }

        if (_foeFireLive && _foeSpreadTeams[dead.TeamId]) FoeFireSpread(dead);   // 第245期（延焼）: 札が無ければ比較1つで抜ける

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
        // 第220期・**計数のみ**: 同じ死で印の移りと疫み（ラウ）の毒の飛びが両方起きた。
        if (handedOff && dead.RawCounter(StatusKeys.Poison) > 0 && _units.Any(u => u.IsAlive && u.HasTrait(TraitId.Contagion)))
            BurstBook.HandoffAndContagion++;

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
        if (dead.TeamId == PlayerTeam) NoteAlone();   // 第198期 Phase 0（計数のみ）
    }

    /// <summary>
    /// 第198期 Phase 0（<b>計数専用・盤面には一切影響しない</b>）。味方陣営の死が<b>固定順の通知を全部通った後</b>
    /// （蘇生・分裂の召喚が済んだ後）に、生き残りが1体だけならその駒に「最後の1体になったターン」を記録する。
    /// <b>乱数を引かない</b>（走査だけ）。
    /// </summary>
    void NoteAlone()
    {
        UnitState? only = null;
        foreach (UnitState u in _units)
        {
            if (u.TeamId != PlayerTeam || !u.IsAlive) continue;
            if (only is not null) return;
            only = u;
        }
        if (only is null) return;
        UnitTally t = TallyOf(only);
        if (t.AloneTurn != 0) return;
        t.AloneTurn = _turn;
        t.AloneHp = only.Hp;
        t.AloneMaxHp = only.MaxHp;
        int foes = 0;
        foreach (UnitState u in _units) if (u.TeamId == EnemyTeam && u.IsAlive) foes++;
        t.AloneFoes = foes;
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
    /// <summary>その隊の陣形（第200期）。隊の最初の駒の陣形で、駒がいなければ X 字。</summary>
    public FormationShape ShapeOfTeam(int teamId)
    {
        foreach (UnitState u in _units) if (u.TeamId == teamId) return u.Shape;
        return FormationShape.X;
    }

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
            if (ShapeOfTeam(teamId).IsSummonSlot(want) && free) slot = want;
        }
        else
        {
            // 召喚専用の枠だけを走る。編成枠へ入れると、5体で満席の盤面では一度も湧かない。
            // **走査順（FormationRules.SummonSlots）は調整ノブ。** 貫き経路に入る 中1・中3 から
            // 埋めるので、湧いた駒が減衰1段ぶんの盾として働く。
            // 第200期: 走査順は隊の陣形の召喚枠（X 字は ○中1 → ○中3 → ○前2 → ○後2 のまま）。
            foreach (int i in ShapeOfTeam(teamId).SummonSlots)
                if (!taken.Contains(i)) { slot = i; break; }
        }
        if (slot < 0) return null;

        // 第187期: 敵の駒が敵陣に呼んだときだけ、**呼んだ敵と同じ倍率**を掛ける（ソムの餌＝味方が敵陣に呼ぶ駒は掛けない）。
        // 規則の束ではなく呼び手の定義から引くのは、作戦マップ（倍率なしで敵を作る）でも召喚だけ既定が掛からないため。
        // **現行の敵の編成に、敵陣へ駒を呼ぶ敵は 0 体**なので、いまの盤面ではこの枝は1度も走らない。
        if (teamId == EnemyTeam && by is not null && by.TeamId == EnemyTeam) def = EnemyScaleRule.Of(by.Def).Apply(def);

        var unit = new UnitState
        {
            Def = def,
            TeamId = teamId,
            Shape = ShapeOfTeam(teamId),
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
        if (by is not null) TallyOf(by).RevivesGiven++;   // 第183期・**計数のみ**（蘇生の本体は触らない）
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
                                && FormationRules.AreAdjacent(u, target))
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
        => WhetCore(target, amount, route, target, 0, true);

    /// <summary>
    /// 支援の宛先（<see cref="SupportTargets"/> の戻り値）それぞれに <see cref="Whet"/> する。
    /// <b><c>foreach (t in heads) Whet(t, …)</c> と1ビットも違わない</b>——違うのは台本
    /// （<see cref="BattleEventKind.Whet"/>）に<b>本来の対象</b>と<b>一連の通し番号</b>を載せることだけ（表示専用）。
    /// </summary>
    public void WhetEach(IReadOnlyList<UnitState> heads, UnitState intended, int amount, WhetRoute route)
    {
        int seq = _verbose && heads.Count > 0 ? ++_whetSeq : 0;
        for (int i = 0; i < heads.Count; i++)
            WhetCore(heads[i], amount, route, intended, seq, i == heads.Count - 1);
    }

    /// <summary>強化の一連の通し番号（表示専用）。1戦の中で 1 から数える。</summary>
    private int _whetSeq;

    private void WhetCore(UnitState target, int amount, WhetRoute route,
                          UnitState intended, int seq, bool last)
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
                                      && FormationRules.AreAdjacent(u, target))
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
            // 号令の台本（表示専用）。**実際に乗ったときだけ**出す。盤面は読むだけ。
            if (_verbose)
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.Whet,
                    Turn = _turn,
                    ActorId = Mark.Owner?.InstanceId,
                    TargetId = receiver.InstanceId,
                    IntendedId = intended.InstanceId,
                    Amount = amount,
                    Team = receiver.TeamId,
                    WhetRoute = route,
                    SourceTrait = Mark.Owner is null ? null : Mark.Id,
                    SupportSeq = seq > 0 ? seq : ++_whetSeq,
                    SupportLast = last,
                    AttackAfter = receiver.CurrentAttack,
                });
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
                        && FormationRules.AreAdjacent(funnel, u))
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

    /// <param name="inverted">反転（第190期）から来た回復か。<b>真なら反転の裏で再反転しない</b>（無限反転の再入ガード）。</param>
    /// <returns>何が起きたか（第191期）。<b>呼び出し口のほとんどは読まない</b>——読むのは縫い合わせ（ヴェル）だけ。</returns>
    /// <param name="fireHeal">火の回復（第235期・ボルグの火の癒し／焼き返し）。<b>真なら渇きを素通りする</b>（支援拒否は通常どおり）。</param>
    public HealOutcome Heal(UnitState target, int amount, UnitState? by = null, bool inverted = false, bool fireHeal = false)
    {
        if (!target.IsAlive || amount <= 0) return HealOutcome.None;
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
            // 表示専用。支援拒否が回復を弾いた瞬間（隣へは流さない）。盤面は読むだけ。
            if (_verbose && target.HasTrait(TraitId.Stoic))
            {
                UnitState? src = by ?? Mark.Owner;
                Emit(new BattleEvent
                {
                    Kind = BattleEventKind.HealBlocked,
                    Turn = _turn,
                    ActorId = src?.InstanceId,
                    TargetId = target.InstanceId,
                    Amount = amount,
                    HpAfter = target.Hp,
                    Team = target.TeamId,
                    SourceTrait = Mark.Owner is null ? null : Mark.Id,
                });
            }
            return HealOutcome.Blocked;
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
        if (DroughtBinding && !fireHeal)
        {
            NoteDroughtBlocked(target, amount);
            return HealOutcome.Drought;
        }

        // 反転の裏（第190期・ベニ）。**渇きと支援拒否を通った後**（＝本来なら通っていた回復）で、
        // 隣にベニがいれば HP を増やす代わりに、回復させた駒を出どころとするダメージにする。
        // **反転由来の回復（`inverted`）は対象外**。保持者がいなければ比較1つで抜ける。
        if (!inverted && _inverseLeakHolders.Count > 0 && AdjacentHolder(_inverseLeakHolders, target) is UnitState leak)
        {
            InverseLeakHit(leak, target, amount, by);
            return HealOutcome.Inverted;
        }

        int before = target.Hp;
        target.Hp = Math.Min(target.MaxHp, target.Hp + amount);
        // 第273期（レリック・溢れの刃）: 溢れた分（最大HPで切られた分）を札へ渡す。全快で 0 しか増えなかった回復も溢れに数える。
        // 支援拒否・渇き・反転の裏で止まった回復はここまで来ない。保持者がいない戦は比較1つで抜ける。
        if (_overflowLive && amount - (target.Hp - before) > 0 && target.HasTrait(TraitId.RelicOverflowEdge))
            RelicOverflowEdgeTrait.Gain(this, target, amount - (target.Hp - before));
        if (target.Hp == before) return HealOutcome.Full;

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
        return HealOutcome.Healed;
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
        var slots = self.Shape.PlayableSlotsOfRow(next.Value).ToList();

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
    /// <returns>
    /// 入れ替えたか（第185期 追補6）。<b>据えた足で空振りしたときだけ偽</b>。呼び出し側が計数を分けるためだけにあり、
    /// <b>盤面の分岐には使わない</b>（今の呼び出し口は喧噪だけが読む）。
    /// </returns>
    public bool SwapSlots(UnitState self, int destSlot, UnitState? by = null)
    {
        // 第229期（追い風）: 保持者がいなければ比較1つで素通り。いれば入れ替えの外側で控えた追い風を順に流す。
        if (!_tailwindLive && !_landingLive) { bool m0 = SwapSlotsCore(self, destSlot, by); RefreshDecoyShow(); return m0; }   // 第231期（表示専用）
        _moveDepth++;
        bool moved;
        try { moved = SwapSlotsCore(self, destSlot, by); }
        finally { _moveDepth--; }
        if (_moveDepth == 0) { FlushTailwind(); FlushLanding(); }
        RefreshDecoyShow();   // 第231期（表示専用）
        return moved;
    }

    bool SwapSlotsCore(UnitState self, int destSlot, UnitState? by)
    {
        UnitState? occupant = PickOne(
            LivingMembers(self.TeamId).Where(u => u.Slot == destSlot).ToList());

        // 据えた足（第185期・バン）: **この駒を動かす入れ替えは空振りする**（どちら側でも）。
        // occupant は先に引いてある（PickOne は候補 0/1 個では乱数を引かないので、空振りでも乱数列は変わらない）。
        // **保持者がいなければ比較1つで抜ける。**
        if (_plantedLive)
        {
            UnitState? planted = self.HasTrait(TraitId.Planted) ? self
                               : occupant is not null && occupant.HasTrait(TraitId.Planted) ? occupant : null;
            if (planted is not null)
            {
                TallyOf(planted).PlantedRefused++;
                if (by is not null && by.TeamId != planted.TeamId) TallyOf(planted).PlantedRefusedFoe++;   // 敵が起こした入れ替え（曝き）
                Log($"    {planted.Name} の据えた足は動かない（入れ替えは空振りした）", LogKind.Trigger);
                return false;
            }
        }

        int origin = self.Slot;
        Row selfFrom = self.Row;

        // 第223期・**計数専用**: 動かしている駒を控える（回避の段の出どころの帳簿だけが読む）。
        UnitState? prevMover = CurrentMover;
        CurrentMover = by;
        try
        {
            self.Slot = destSlot;
            NotifyMoved(self, selfFrom, origin, by);

            if (occupant is null) return true;
            Row otherFrom = occupant.Row;
            occupant.Slot = origin;
            NotifyMoved(occupant, otherFrom, destSlot, by);
            return true;
        }
        finally { CurrentMover = prevMover; }
    }

    /// <summary>動かされた駒1体ぶんの通知（第228期に <see cref="SwapSlots"/> の中から切り出した・中身は1文字も変えていない）。</summary>
    void NotifyMoved(UnitState u, Row from, int fromSlot, UnitState? by)
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

        // 敵の乱れ（第226期）: 陣営ごとの累計と、バサがいる間は前へ出た駒の混乱。**保持者がいなければ比較1つで抜ける。**
        if (_disarrayLive) NoteDisorder(u, from, by);

        // 着地の反動（第237期）: 保持者が動かされた。**ここでは控えるだけ**（追い風と同じ理由）。**保持者がいなければ比較1つで抜ける。**
        if (_landingLive && u.HasTrait(TraitId.Landing)) _landingQ.Enqueue(u);

        // 追い風（第229期）: 保持者が敵を後ろの行へ動かした。**ここでは控えるだけ**——入れ替えの途中（同じ席に2体いる瞬間）に味方を動かさない。
        // 追い風の入れ替えの中で起きた敵の移動は追い風を呼ばない（連鎖しない）。**保持者がいなければ比較1つで抜ける。**
        // 第230期の追記: 撃破の衝撃の吹き飛ばし（`ImpactTailwind` を持つヨミが動かした敵）でも、同じ陣営の生きている追い風の保持者（席番号の若い方）の追い風が起きる。
        if (_tailwindLive && by is not null && by.TeamId != u.TeamId
            && FormationRules.DepthOf(u.Row) > FormationRules.DepthOf(from))
        {
            UnitState? tw = by.HasTrait(TraitId.Tailwind) ? by : by.HasTrait(TraitId.ImpactTailwind) ? TailwindHolderOf(by.TeamId) : null;
            if (tw is not null)
            {
                if (_inTailwind) TallyOf(tw).TailwindNested++;
                else _tailwindQ.Enqueue((tw, u, fromSlot, tw != by ? 4 : by.HasTrait(TraitId.Shuffler) ? 0 : _haneAct == 1 ? 1 : _haneAct == 2 ? 2 : 3));
            }
        }

        // 味方の反応を先に流す。OnMoved は割り込み攻撃まで含むので、逆順だと
        // シオの強化が「振った後」に乗る（軋みが +5 を載せずに振ってしまう）。
        // 支援が先・本人の反応が後、という順序をここで固定する。
        // 第189期・計数のみ: ハネの「勢い余って」で後ろへ下がった狙撃（セロ）は構えが整う。
        if (OverrunBy is not null && u.HasTrait(TraitId.Sniper)
            && FormationRules.DepthOf(u.Row) > FormationRules.DepthOf(from)) TallyOf(OverrunBy).OverrunReaderSniper++;

        foreach (UnitState ally in LivingMembers(u.TeamId))
        {
            if (ally == u) continue;
            foreach (Trait t in ally.Traits.ToList())
            {
                if (OverrunBy is not null && t.Id == TraitId.Drifter) TallyOf(OverrunBy).OverrunReaderDrifter++;   // 第189期（計数のみ）
                TraitMark m = this.BeginTrait(t.Id, ally);   // 第94期 (T2) の印
                t.OnAllyMoved(this, ally, u);
                this.EndTrait(m);
            }
        }

        foreach (Trait t in u.Traits.ToList())
        {
            if (OverrunBy is not null && t.Id == TraitId.Displaced) TallyOf(OverrunBy).OverrunReaderDisplaced++;   // 第189期（計数のみ）
            TraitMark m = this.BeginTrait(t.Id, u);   // 第94期 (T2) の印
            t.OnMoved(this, u, from, u.Row);
            this.EndTrait(m);
        }
    }

    /// <summary>
    /// 経路の並べ替え（第228期・吹っ飛ばし）。<paramref name="moves"/> の駒を一度に指定の席へ置き、<b>席が変わった駒1体につき1回</b>
    /// 「動かされた」を通知する（<see cref="SwapSlots"/> と同じ通知・順は渡した順）。<b>乱数を引かない。</b>
    /// 据えた足（バン）の駒が1体でも含まれていれば何もせず偽を返す。
    /// </summary>
    public bool RelocateLane(IReadOnlyList<(UnitState U, int Dest)> moves, UnitState by)
    {
        if (!_tailwindLive && !_landingLive) return RelocateLaneCore(moves, by);
        _moveDepth++;
        bool done;
        try { done = RelocateLaneCore(moves, by); }
        finally { _moveDepth--; }
        if (_moveDepth == 0) { FlushTailwind(); FlushLanding(); }
        return done;
    }

    // ---- 第229期: 追い風 ----
    readonly Queue<(UnitState By, UnitState Foe, int FromSlot, int Src)> _tailwindQ = new();
    int _moveDepth;
    bool _inTailwind;

    /// <summary>第230期の追記: その陣営の生きている追い風の保持者（席番号の若い方）。いなければ null。<b>乱数を引かない。</b></summary>
    UnitState? TailwindHolderOf(int team)
    {
        UnitState? h = null;
        foreach (UnitState a in LivingMembers(team))
            if (a.HasTrait(TraitId.Tailwind) && (h is null || a.Slot < h.Slot)) h = a;
        return h;
    }

    // ---- 第237期: 着地の反動 ----
    // ハネ（札の保持者）が動かされたら控え、入れ替えの一番外側の出口で順に流す（入れ替えの途中は同じ席に2体いる瞬間がある）。
    bool _landingLive, _inLanding;
    readonly Queue<UnitState> _landingQ = new();

    void FlushLanding()
    {
        if (_inLanding) return;
        _inLanding = true;
        try { while (_landingQ.Count > 0) LandingTrait.Run(this, _landingQ.Dequeue()); }
        finally { _inLanding = false; }
    }

    void FlushTailwind()
    {
        if (_inTailwind) return;
        while (_tailwindQ.Count > 0)
        {
            var (by, foe, fromSlot, src) = _tailwindQ.Dequeue();
            _inTailwind = true;
            try { Tailwind(by, foe, fromSlot, src); }
            finally { _inTailwind = false; }
        }
    }

    /// <summary>
    /// 追い風の本体（<see cref="TailwindTrait"/>）。敵がいた席の経路（中央は2本・番号の若い方から）で、味方の最後尾（HP 4割以上）を1つ前の味方と入れ替える。
    /// <b>乱数を引かない</b>（入れ替えの窓口は占有者が 0/1 体なら引かない）。
    /// </summary>
    void Tailwind(UnitState by, UnitState foe, int fromSlot, int src)
    {
        UnitTally t = TallyOf(by);
        t.TailwindTriggers++;
        if (src == 0) t.TailwindFromShuffle++; else if (src == 1) t.TailwindFromBlast++; else if (src == 2) t.TailwindFromSpring++; else if (src == 4) t.TailwindFromImpact++; else t.TailwindFromOther++;
        var allies = LivingMembers(by.TeamId);
        if (allies.Count < 2) { t.TailwindNoPair++; return; }
        FormationShape shape = allies[0].Shape;
        bool pair = false;
        foreach (int lane in foe.Shape.LanesOf(fromSlot))
        {
            if (lane >= shape.LaneCount) continue;
            var line = LaneOccupants(allies, lane, shape);
            if (line.Count < 2) continue;
            pair = true;
            // 第230期（`TailwindFighter`）: 踏み込むのは「最も前にいない味方」のうち攻撃力（現在値）が最も高い1体（同値は後ろの行・次に席番号）。4割未満は除外。
            // 札が無ければ第229期の道（最後尾から見て4割以上の最初の駒）。**どちらも乱数を引かない。**
            List<int> order = Enumerable.Range(1, line.Count - 1).Reverse().ToList();
            if (by.HasTrait(TraitId.TailwindFighter))
                order = order.OrderByDescending(i => line[i].CurrentAttack).ThenByDescending(i => FormationRules.DepthOf(line[i].Row))
                             .ThenBy(i => line[i].Slot).ToList();
            foreach (int i in order)
            {
                UnitState step = line[i], back = line[i - 1];
                if (step.Hp * 100 < step.MaxHp * TailwindTrait.HpGatePercent) { t.TailwindLowHp++; continue; }
                Log($"    追い風: {by.Name} が {foe.Name} を押し下げた隙に、{step.Name} が {back.Name} の前へ踏み込んだ", LogKind.Trigger);
                if (_verbose) Emit(new BattleEvent
                {
                    Kind = BattleEventKind.Tailwind, Turn = _turn, ActorId = by.InstanceId, TargetId = step.InstanceId,
                    PartnerId = back.InstanceId, SpreadFromId = foe.InstanceId, Slot = lane, Team = by.TeamId,
                });
                if (SwapSlots(step, back.Slot, by)) { t.TailwindSteps++; TallyOf(step).TailwindStepped++; }
                else t.TailwindRefused++;
                return;
            }
        }
        if (pair) t.TailwindAllLow++; else t.TailwindNoPair++;
    }

    bool RelocateLaneCore(IReadOnlyList<(UnitState U, int Dest)> moves, UnitState by)
    {
        if (_plantedLive && moves.Any(m => m.U.HasTrait(TraitId.Planted))) return false;
        var from = moves.Select(m => (m.U, Row: m.U.Row, Slot: m.U.Slot)).ToList();
        foreach (var (u, dest) in moves) u.Slot = dest;
        UnitState? prevMover = CurrentMover;
        CurrentMover = by;
        _haneAct = 1;
        try
        {
            foreach (var (u, row, slot) in from)
                if (u.Slot != slot) NotifyMoved(u, row, slot, by);
        }
        finally { CurrentMover = prevMover; _haneAct = 0; }
        return true;
    }

    /// <summary>その陣営の経路の上の生きている駒（前から後ろの順・第228期）。</summary>
    public List<UnitState> LaneMembers(int teamId, int lane, FormationShape shape) => LaneOccupants(LivingMembers(teamId), lane, shape);

    /// <summary>
    /// 吹っ飛ばしの貫き（第228期・<see cref="BlastTrait"/> だけが呼ぶ）。<b>的を A に固定した貫きの1発</b>（第223期の追い撃ちと同じ作法）——
    /// 経路の先頭の A から後ろの全員へ、既存の貫きの減衰（1体ごとに −25%）で当たる。敵に与えた量を返す（計数のため）。
    /// </summary>
    public long BlastShot(UnitState hane, UnitState a)
    {
        if (!hane.IsAlive || !a.IsAlive) return 0;
        UnitTally t = TallyOf(hane);
        long before = t.DamageToEnemy;
        _forcedTarget = a;
        try { PerformAttack(hane, patternOverride: AttackPattern.Pierce); }
        finally { _forcedTarget = null; }
        return t.DamageToEnemy - before;
    }

    /// <summary>吹っ飛ばしの見出し（第228期・表示専用 ＋ 計数）。<c>Slot</c> ＝ 経路 ／ <c>Amount</c> ＝ 経路の上の敵の数。<b>盤面は1ビットも触らない。</b></summary>
    public void NoteBlast(UnitState hane, UnitState a, int lane, int count)
    {
        TallyOf(hane).BlastCount++;
        Log($"    {hane.Name} が {a.Name} を経路の奥まで吹っ飛ばす", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Blast, Turn = _turn, ActorId = hane.InstanceId, TargetId = a.InstanceId,
            Slot = lane, Amount = count, Team = hane.TeamId,
        });
    }

    /// <summary>
    /// 弾き返し（第228期・<see cref="SpringTrait"/> だけが呼ぶ・<c>Interrupt</c> の中）。殴ってきた敵を経路で1つ後ろの席へ入れ替え、転ばせ、
    /// ハネ自身は勢い余って隣の味方と入れ替わる（<see cref="OverrunTrait"/>）。表示専用の <see cref="BattleEventKind.Spring"/> を先に出す。
    /// </summary>
    public void SpringSwap(UnitState hane, UnitState foe, int dest, UnitState? guarded = null)
    {
        UnitTally t = TallyOf(hane);
        UnitState? partner = LivingMembers(foe.TeamId).FirstOrDefault(u => u.Slot == dest);
        Row partnerFrom = partner?.Row ?? Row.Back;
        if (guarded is not null)
        {
            // 第231期（B）: 隣の味方が殴られた弾き返し。表示専用の見出しを `Spring` の直前に1件（`TargetId` ＝ 殴られた味方 ／ `PartnerId` ＝ 殴った敵）。
            t.SpringGuardFires++;
            Log($"    {hane.Name} が {guarded.Name} を殴った {foe.Name} へ飛び込む", LogKind.Trigger);
            if (_verbose) Emit(new BattleEvent
            {
                Kind = BattleEventKind.SpringGuard, Turn = _turn, ActorId = hane.InstanceId, TargetId = guarded.InstanceId,
                PartnerId = foe.InstanceId, Slot = dest, Team = hane.TeamId,
            });
        }
        Log($"    {hane.Name} が殴ってきた {foe.Name} を弾き返す", LogKind.Trigger);
        if (_verbose) Emit(new BattleEvent
        {
            Kind = BattleEventKind.Spring, Turn = _turn, ActorId = hane.InstanceId, TargetId = foe.InstanceId,
            Slot = dest, PartnerId = partner?.InstanceId, Team = hane.TeamId,
        });
        bool moved;
        _haneAct = 2;
        try { moved = SwapSlots(foe, dest, hane); }
        finally { _haneAct = 0; }
        if (!moved) { t.SpringRefused++; return; }
        t.SpringCount++;
        if (guarded is not null) t.SpringGuardCount++;
        if (partner is not null && FormationRules.DepthOf(partner.Row) < FormationRules.DepthOf(partnerFrom)) t.SpringForward++;
        if (foe.IsAlive)
        {
            foe.SetCounter(StatusKeys.Stagger, 1);
            EmitStagger(foe, StaggerLabels.Fell, hane);
            Log($"    {foe.Name} は弾き返されて転んだ（次の手番を失う）", LogKind.Status);
        }
        // 第243期（⑤）: 弾き返しで動かした敵（殴ってきた敵と、入れ替わって前へ出た敵）を萎縮させる。
        if (hane.HasTrait(TraitId.SpringDaunt))
        {
            DauntTrait.MarkPushed(this, hane, foe);
            if (partner is not null) DauntTrait.MarkPushed(this, hane, partner);
        }
        // 第243期（③）: 弾き返しの打撃——殴ってきた敵へハネの現在の攻撃力ぶん（攻撃ではない・`ApplyDamage` を直に・反撃は起きない）。
        if (hane.HasTrait(TraitId.SpringStrike) && foe.IsAlive && hane.IsAlive)
        {
            int amount = hane.CurrentAttack;
            int before = foe.Hp;
            t.SpringStrikeHits++;
            Log($"    {hane.Name} の弾き返しが {foe.Name} を打つ（{amount}）", LogKind.Trigger);
            Reaction(() => ApplyDamage(foe, amount, hane));
            t.SpringStrikeDealt += Math.Max(0, before - Math.Max(0, foe.Hp));
            if (!foe.IsAlive) t.SpringStrikeKills++;
        }
        if (guarded is not null)
        {
            // 第232期（S2・`SpringStay`）: 弾くだけで入れ替わらない（ハネは動かない・勢い余ってもない）。
            if (hane.HasTrait(TraitId.SpringStay)) { t.SpringGuardStays++; return; }
            // 第231期（B）: 殴られた味方と入れ替わる（弾いた反動で前へ出て、味方を後ろへかばう）。突き崩しは起こさない（勢い余ってと同じ `Shoving` の中）。
            if (!hane.IsAlive || !guarded.IsAlive || guarded.HasTrait(TraitId.Planted)) { t.SpringGuardRefused++; return; }
            bool sw = false;
            Shoving(() => sw = SwapSlots(hane, guarded.Slot, hane));
            if (sw) t.SpringGuardSwaps++; else t.SpringGuardRefused++;
            return;
        }
        if (hane.HasTrait(TraitId.Overrun))
        {
            long before = t.OverrunSwaps;
            OverrunTrait.Swap(this, hane);
            t.SpringSwaps += t.OverrunSwaps - before;
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
                                   AshRule? ash = null, EruptRule? erupt = null, MarkRule? markRule = null,
                                   CounterProbe? probe = null)
        => Run(Materialize(player, BattleContext.PlayerTeam),
               Materialize(enemy, BattleContext.EnemyTeam, (boss ?? BossRule.Default).Scale),
               seed, verbose, colossus, yoke, hush, martyr, expose, shove, bear, relay, slander,
               overbear, scale, scapegoat, divert, goad, finisher, favor, blaze, funnel, whetMask,
               creak, sever, thinBlade, thorn, suture, sutureFire, spillWound, mend, woundIgnite,
               gather, soak, deep, curse, betray, encore, rage, menderCost, loose, taillight, reader, boss,
               nourish, wound, ember, wildfire, harm, parry, shatter, shrapnel, brace, shuffler,
               confusion, haste, ward, indulgence, ash, erupt, markRule, probe);

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
                                   AshRule? ash = null, EruptRule? erupt = null, MarkRule? markRule = null,
                                   CounterProbe? probe = null)
    {
        var ctx = new BattleContext(seed, verbose, colossus, yoke, hush, martyr, expose, shove, bear,
                                    relay, slander, overbear, scale, scapegoat, divert, goad, finisher,
                                    favor, blaze, funnel, whetMask, creak, sever, thinBlade, thorn,
                                    suture, sutureFire, spillWound, mend, woundIgnite, gather, soak, deep, curse,
                                    betray, encore, rage, menderCost, loose, taillight, reader, boss,
                                    nourish, wound, ember, wildfire, harm, parry, shatter, shrapnel, brace,
                                    shuffler, confusion, haste, ward, indulgence, ash, erupt, markRule, probe);

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
        ctx.DrainFeatherMarksPublic();   // 第298期（MF・開戦時に書かれた標の羽。保持者がいなければ比較1つで抜ける）

        int turn = 1;
        for (; turn <= MaxTurns; turn++)
        {
            ctx.Turn = turn;
            if (!ctx.TeamAlive(BattleContext.PlayerTeam) || !ctx.TeamAlive(BattleContext.EnemyTeam))
                break;

            ctx.Log($"--- ターン {turn} ---", LogKind.Turn);
            ctx.EmitTurnStart();
            ctx.TickStatuses();
            ctx.RechargeSilkBalls();    // 第292期: 糸玉の帯電し直し（糸玉が無ければ比較1つで抜ける）
            ctx.EmitStatusSnapshot();   // 削った後の残量を写す。表示用で、盤面には触らない
            ctx.NoteHexCensus();        // 呪い（第96期）の門の 2。**盤面は読むだけ**
            ctx.NoteReaderCensus();     // 積み過ぎ（第115期）の門の 1。**盤面は読むだけ**
            ctx.NoteBossCensus();       // ボスの土台（第117期）の時系列。**盤面は読むだけ**
            ctx.NoteWoundCensus();      // 傷の在庫（第120期）。**盤面は読むだけ**
            ctx.NoteArmorCensus();      // 破片の在庫（第138期 段2）。**盤面は読むだけ**
            ctx.NoteWardCensus();       // 重りの在庫（第154期）。**盤面は読むだけ**
            ctx.NoteBraceCensus();      // 身構えの保持者の板（第213期）。**盤面は読むだけ**
            ctx.NoteFireLevelCensus();  // 火勢（第242期）。**盤面は読むだけ**
            ctx.NoteKuguCensus();       // クグの帯電と組み付き（第290期）。**盤面は読むだけ**

            foreach (UnitState u in ctx.AllUnits.Where(x => x.IsAlive).ToList())
                foreach (Trait t in u.Traits.ToList())
                {
                    TraitMark m = ctx.BeginTrait(t.Id, u);   // 第94期 (T2) の印
                    t.OnTurnStart(ctx, u);
                    ctx.EndTrait(m);
                }
            ctx.DrainFeatherMarksPublic();   // 第298期（MF・ターンの頭に書かれた標の羽——ソラの自分への標など）

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
                .GroupBy(u => (Speed: ctx.TurnSpeed(u), u.TeamId));   // 第293期: KW-b の糸で −3（網の無い戦は Def.Speed のまま）

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

            ctx.WiltFire();         // 第242期: 育たなかった駒の火勢が萎む（ターンの終わり）。保持者がいなければ比較1つで抜ける
            ctx.NoteFoeStalled();   // 第185期（計数のみ）: このターンに手番を失った敵の数の分布
        }

        // 第199期: 剣の段の相打ちで最後の敵を倒した戦だけは、味方が全滅していても勝ち（印を立てる口は相打ちの1箇所）。
        bool playerWon = (ctx.TeamAlive(BattleContext.PlayerTeam) || ctx.LastStandVictoryTeam == BattleContext.PlayerTeam)
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
        ctx.CloseShockLedger();   // 第214期・**計数のみ**

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
            Hands = ctx.Hands.ToList(),   // 第239期（計数のみ）
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
            Brittle = ctx.BrittleBook,   // 第219期（計数のみ）
            Burst = ctx.BurstBook,       // 第220期（計数のみ）
            BurnLink = ctx.BurnLinkBook, // 第233期（計数のみ）
            FireLevels = ctx.FireLvLive ? ctx.FireBook : null,   // 第242期（計数のみ）
            BurnHit = ctx.BurnHitLive ? ctx.BurnHitBook : null,  // 第255期（計数のみ）
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
            FoeStalledHist = (long[])ctx.FoeStalledHist.Clone(),   // 第185期（計数のみ）
            PierceChose = (long[])ctx.PierceChose.Clone(),         // 第202期（計数のみ）
            PierceTies = ctx.PierceTies,
            PierceFallbacks = ctx.PierceFallbacks,
            PierceDeadEnds = ctx.PierceDeadEnds,
            // 第184期。標の軸（**計数専用**。どの規則も読まない）。
            MarkAxis = new MarkAxisLedger(
                (long[])ctx.MarkVulnHits.Clone(), (long[])ctx.MarkVulnAdded.Clone(),
                ctx.MarkFoeUnitTurns, ctx.MarkFoeTurns, ctx.MarkFoeMax,
                ctx.BeckonGuardHits, ctx.BeckonGuardSaved, ctx.BeckonStrippedHits, ctx.BeckonStrippedDamage),
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

    /// <summary>
    /// 編成から駒を作る。<b>敵陣営（<see cref="BattleContext.EnemyTeam"/>）には
    /// <see cref="EnemyScaleRule.Default"/>（採用値）を掛ける</b>（第187期）——会戦・作戦マップ・DemoApp は
    /// 自分でここを呼んでから <c>Run</c> に渡すので、この既定が唯一の口になる。
    /// </summary>
    public static List<UnitState> Materialize(Formation formation, int teamId)
        => Materialize(formation, teamId, EnemyScaleRule.Default);

    /// <summary>倍率を明示する版。<b>味方陣営には <paramref name="scale"/> を読まない</b>。</summary>
    public static List<UnitState> Materialize(Formation formation, int teamId, EnemyScaleRule scale)
    {
        var units = new List<UnitState>();
        foreach ((int slot, UnitDef raw) in formation.Occupied())
        {
            UnitDef def = teamId == BattleContext.EnemyTeam ? scale.Apply(raw) : raw;
            TraitId? relic = formation.RelicAt(slot);   // 第270期: 付けない枠は従来と同じ1本道（Attach を通らない）
            units.Add(new UnitState
            {
                Def = def,
                TeamId = teamId,
                Shape = formation.Shape,
                Slot = formation.Shape.PlayableSlots[slot],   // 第200期: X 字は恒等（枠 i ＝ 席 i）
                Hp = def.MaxHp,
                MaxHp = def.MaxHp,
                Traits = relic is TraitId r ? RelicCatalog.Attach(def.Traits, r) : TraitCatalog.Resolve(def.Traits),
                Relic = relic,
            });
        }
        return units;
    }

    /// <summary>
    /// <b>敵専用の9枠の入口</b>（第221期）。<see cref="EnemyWave"/> の席 0〜8 に<b>そのまま</b>立たせる。
    /// <b>陣営を引数に取らない</b>——作る駒は必ず <see cref="BattleContext.EnemyTeam"/>・陣形は X 字
    /// （プレイヤー側からこの経路に来る道は無い）。倍率は <see cref="Materialize(Formation, int, EnemyScaleRule)"/> と同じ掛け方。
    /// </summary>
    public static List<UnitState> MaterializeEnemy(EnemyWave wave, EnemyScaleRule scale)
    {
        var units = new List<UnitState>();
        foreach ((int seat, UnitDef raw) in wave.Occupied())
        {
            UnitDef def = scale.Apply(raw);
            units.Add(new UnitState
            {
                Def = def,
                TeamId = BattleContext.EnemyTeam,
                Shape = FormationShape.X,
                Slot = seat,
                Hp = def.MaxHp,
                MaxHp = def.MaxHp,
                Traits = TraitCatalog.Resolve(def.Traits)
            });
        }
        return units;
    }

    /// <summary><see cref="MaterializeEnemy(EnemyWave, EnemyScaleRule)"/> の既定の倍率（採用値）版。</summary>
    public static List<UnitState> MaterializeEnemy(EnemyWave wave)
        => MaterializeEnemy(wave, EnemyScaleRule.Default);
}
