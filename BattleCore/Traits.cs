namespace BattleCore;

public enum TraitId
{
    // --- マイナス側 ---
    Splash,      // 巻き込み: 攻撃が隣接する味方にも当たる
    Coward,      // 臆病: 3分の1削られると後列へ逃げる
    Stoic,       // 支援拒否: 回復も強化も呪いも一切受け付けない
    Sacrifice,   // 生贄: 戦闘開始時に味方全体を削る
    Drain,       // 大食い: 毎ターン味方からHPを吸う
    Sluggish,    // のろま: 2ターンに1回しか動かない
    Splitter,    // 分裂: 倒れると子が湧く
    Bomber,      // 自爆: 一定ターン後に自壊して敵全体を巻き込む
    Frail,       // 脆弱: 受けるダメージが増える
    Fixate,      // 執着: 一度狙った敵が狙える位置に生きている限り、他の敵を狙えない

    // --- プラス側 ---
    Rage,        // 被弾強化: ダメージを受けるたび攻撃力が上がる
    Sniper,      // 後衛特化: 一度後退してから後列にいると攻撃力2倍＋貫き化
    Curse,       // 呪詛: 開始時に敵全体の攻撃力を下げる（隣接味方にも漏れる）
    Guardian,    // 庇う: 味方への攻撃を肩代わりする
    Martyr,      // 殉教: 庇うと挙動同一。**割合をガルドと分けるためだけ**に別 Id にしてある（敵側の語彙）
    Necro,       // 墓守: 味方が倒れるたび強化される
    Colossus,    // 巨躯: 自分より後ろの列にいる味方への攻撃を、型を問わず肩代わりする
    Executioner, // 処刑: とどめを刺すと攻撃力が上がる
    Reviver,     // 継ぎ接ぎ: 倒れた味方を回数制限つきで戦線に戻す
    Ephemeral,   // 儚い: 湧いて出た駒。分裂しないし蘇生対象にもならない
    Venom,       // 毒撃: 攻撃した相手に毒を積む
    Thorns,      // 棘: 殴られると殴り返す
    Marker,      // 囃し立て: 隣の味方に敵の攻撃を集中させる
    Mender,      // 継ぎ当て: 自分のHPを削って味方を回復する
    Amplifier,   // 澱み: 敵に積まれた毒を増幅する
    Contagion,   // 疫: 毒を持つ敵が倒れると周囲へ毒が飛ぶ
    Miasma,      // 瘴気: 毎ターン敵全体へ薄く毒を撒く
    Immobile,    // 不動: 自分からは決して攻撃しない
    Havoc,       // 惨禍: 味方全体の被ダメージが増える（他系統の燃料になるマイナス）
    Paralyze,    // 痺れ: 敵の行動を封じる
    Devour,      // 毒喰らい: 敵に積まれた毒の量だけ味方を癒す
    Rally,       // 号令: 前のターンに動かなかった味方を強化する
    Blightfed,   // 澱み喰い: 味方が負った毒を吸い取り、その分だけ強くなる
    Displaced,   // 軋み: 隊列を動かされるほど強くなり、動かされた直後に割り込んで攻撃する
    Shuffler,    // 喧噪: 毎ターン味方2体の位置を入れ替える
    Bind,        // 縛め: 味方1体を動けなくする代わりに大きく強化する
    Bulwark,     // 据え: 動かなかった味方の被ダメージを半減する
    Drifter,     // 移り木: 動かされた味方を癒し強化する
    Perverse,    // 逆しま: 強化されると弱くなり、弱体化されると強くなる
    Sharer,      // 分かち: 味方の被ダメージを肩代わりする（型を問わない）
    Loose,       // 散開: 隣に味方がいない駒を硬くする
    Cower,       // 萎縮: 味方全体の攻撃を下げ、代わりに被ダメージを下げる
    Pursuer,     // 追い打ち: 味方が敵を倒すと、ターン外に割り込んで攻撃する
    RearGuard,   // 後備え: 後列の味方への攻撃を肩代わりする。貫きにも割り込む
    Cinder,      // 火の粉: 攻撃した相手に燃焼を付け、隣接する味方にも燃え移る
    Pyre,        // 熾火: 自分が燃えている間だけ本領を発揮する
    Condemn,     // 断罪: 反撃してきた相手を痺れさせる（敵側の語彙）
    Shatter,     // 砕け: 範囲攻撃を浴びると、その分を破片（アーマー）にして味方へ配る
    ThornGuard,  // 棘守り: 前か横の味方への単体攻撃を身代わりし、その味方と位置を入れ替える
    Carve,       // 刻み: 攻撃した相手に傷を刻み、相手が既に負っている傷1つにつき加算
    Forsake,     // 置き去り: 自分より速い味方を癒し、自分より遅い味方を削る（同速には何も起きない）
                 // プラスとマイナスが1つのルールの表と裏なので、どちらのブロックに入れても嘘になる
    Torment,     // 責め苦: 動きを封じられた敵を殴ると追い打ち、動ける敵を殴ると自分が痺れる
                 // 置き去りと同じく1つのルールの表と裏なので、どちらのブロックに入れても嘘になる
    Avenge,      // 仇討ち: 標的にされた味方が殴られると割り込んで刺し返す。自分が殴られると怯む
                 //（同上）
    Rend,        // 裂き: 攻撃した相手に傷を刻む。刃が薄く、与えるダメージは常に1
                 //（同上。刻めるのは断てないからで、プラスとマイナスが同じ一文から出る）
    Gouge,       // 抉り: 傷を持つ敵を攻撃すると傷1つにつき加算。敵を倒すと次の手番を失う
                 //（同上）
    Sever,       // 断ち: 最も傷の深い敵を狙い、その傷をすべて消費して1つにつき加算。
                 // 傷が閾値に届かない間は断てない（同上。開いた傷しか断てない、の表と裏）
                 // **第74期に「手番を捨てる」から「振るが断てない」へ変えた**（SeverWait.Swing）
    Suture,      // 縫い: 最も傷の深い敵を狙い、その傷1つにつき最も傷ついた味方を回復する。
                 // 繕うたび、糸を通した敵の傷がひとつ塞がる
                 //（同上。糸は開いた傷にしか通らない／通した糸を引けばその傷は塞がる）
    Alms,        // 施し: 自分は減らずに味方を回復する（敵側の語彙）
    Expose,      // 曝き: 攻撃したあと、敵陣の後列でいちばん無傷な駒を、前列でいちばん傷ついた枠へ引き出す（敵側の語彙）
    Slander,     // 誹り: 攻撃した相手の攻撃力を下げる。口先で腕を鈍らせる（敵側の語彙）

    // --- プラスとマイナスが1つの動作の表と裏（どちらのブロックにも入らない） ---
    // 置き去り・責め苦・仇討ち・裂き・抉り・断ち・縫いも本来はこちら側だが、
    // 追加順の都合でプラス側のブロックに並んでいる（各列挙子のコメントに但し書きがある）。
    Shove,       // 突き返し: 味方が動かされるたび、敵陣の隊列を突き崩す。
                 // ただし勢い余って隣接する味方の体勢まで崩す（攻撃力が下がる）
    Bear,        // 引き受け: 隣接する味方が受ける攻撃力低下を代わりに背負い、その分だけ鎧になる。
                 // ただし自分の腕は落ち続ける（同上。1つの動作の表と裏）
    Relay,       // 渡し: 隣の味方が受ける攻撃力低下を引き受け、最も強い敵へそのまま渡す。
                 // ただし通り道になった自分の身が削れる（同上。1つの動作の表と裏）
    Overbear,    // 驕り: 隣の味方を見下して腕を鈍らせ、隣が全員自分より弱くなったとき本気を出す
                 // （同上。削るのは常時、報われるのは条件を満たしたときだけ）
    Scale,       // 鱗: 倒れた味方の破片を拾って纏う。纏っているあいだ攻撃が貫きになるが、
                 // 振るたびに剥がれる（供給・発揮・消費の1サイクルが1枚に入る。同上）
    Scapegoat,   // 業: 味方の呪いを引き取って歩く。種類が揃うと、溜め込んだものを殴った相手に返す
                 // （引き取るほど自分が壊れる。1つの動作の表と裏）
    Divert,      // 逸らし: 味方に向いた視線を引き剥がし、敵1体へ向け直す。
                 // ただし引き剥がした視線は自分にも刺さる（1つの動作の表と裏）
    Goad,        // 駆り立て: 隣のいちばん殴れる味方を前に押し出し、自分の力を全部渡す。
                 // 押し出された側は狙われる（1つの動作の表と裏。クグの縛め＋攻撃+16 と同じ構造）
    Finisher,    // 止め: 誰かが指を差した敵に必ず食らいつき、倍の力で仕留める。
                 // 仕留めると指差しは消える（供給と消費のサイクル。差されなければただの雑魚）
    Favor,      // 火選り: 燃えている味方の腕を上げ、自分の隣で燃えていない味方の腕を鈍らせる
                 // （1つの動作の表と裏。プラスは位置を問わず・マイナスは位置で決まる）
    Funnel,     // 横流し: 自分と隣の味方に来た強化を、自分の隣で一番遅い味方へすべて回す。
                 // 自分も横取りされた側も育たない（1つの動作の表と裏。行き先が本体で量は増えない）

    // --- 傷の5枚から切り出したマイナス（第74期・**器具**） ---
    // どれも既存の駒に既定で付いたままで、**盤面は1ビットも変わらない**（受け入れ基準は
    // `compare` 305 セル 0 件）。切り出した理由は1つだけ——**計量できるようにするため**。
    // 第73期は「5枚のマイナスのうち独立に外せたのは執着（Fixate）1枚だけ」で詰まった。
    // **前例はノミ**（刻み＝Carve と執着＝Fixate が最初から別の TraitId だったので、
    // 第73期は執着だけを外して測れた）。**マイナスをプラスと同じ Trait クラスに書くと、
    // 後から代金が測れない**——次にマイナスを書くときは、別の TraitId に切り出せるかを先に見ること。
    ThinBlade,   // 薄刃: 刃が薄く、与えるダメージは常に1（裂き＝Rend の代金）
    Overreach,   // 深追い: 敵を倒すと次の手番を失う（抉り＝Gouge の代金）
    Await,       // 刃待ち: 狙える敵の傷が閾値に届くまで断ちの機会を捨てる（断ち＝Sever の代金）
                 // **第74期に採用した V1 では手番は捨てない**（SeverWait / SeverRule を参照）
    Seal,        // 塞ぎ: 繕うたび、糸を通した敵の傷がひとつ塞がる（縫い＝Suture の代金）
                 // **これだけは札**（本体は SutureTrait の中）。引き受け＝BearTrait と同型で、
                 // 理由は SealTrait の doc を参照（患者の選び直しが乱数と盤面を動かす）

    // --- 盤面ルール（プラスでもマイナスでもない。敵側の語彙） ---
    // 保持者の損得ではなく、盤面の読み方そのものを書き換える。だからどちらのブロックにも入らない。
    Inversion,   // 逆位: 保持者が生きている間、行動順が速さ昇順になる。**両陣営に等しくかかる**
    Drought,     // 渇き: 保持者が生きている間、回復が一切通らない。**両陣営に等しくかかる**
    Yoke,        // 軛: 保持者が生きている間、1回のダメージが上限で切られる。**両陣営に等しくかかる**
    Hush         // 粛: 保持者が生きている間、ターン外の行動が一切通らない。**両陣営に等しくかかる**
}

/// <summary>
/// 特性はすべて「戦闘イベントに反応するハンドラ」として書く。
/// こうしておくと、意図していない組み合わせでも勝手に噛み合う。
/// 新しい特性を足すときは、このクラスを継承して TraitCatalog に登録するだけ。
/// </summary>
public abstract class Trait
{
    public abstract TraitId Id { get; }

    /// <summary>true を返すと、この駒は回復・強化・弱体をすべて受け付けなくなる。</summary>
    public virtual bool BlocksSupport => false;

    public virtual void OnBattleStart(BattleContext ctx, UnitState self) { }
    public virtual void OnTurnStart(BattleContext ctx, UnitState self) { }

    /// <summary>
    /// 手番の <see cref="ActionKind.Skill"/> で呼ばれる。
    ///
    /// <see cref="OnTurnStart"/>（ターン頭に全員ぶん・毎ターン無条件）と違い、
    /// **行動パターンに載った駒だけが、載せたタイミングで**実行する。しかもその手番は
    /// 攻撃に使えない。「いつ撃つか」を選べるのはこちらだけで、あちらは選択肢を持たない。
    ///
    /// 発火が遅くなることに注意。ターン頭ではなく素早さ順の自分の番なので、
    /// 自分より速い味方が殴られた後になる（第11期 Phase BB。意図した仕様変更）。
    /// </summary>
    public virtual void OnAction(BattleContext ctx, UnitState self, UnitAction action) { }

    /// <summary>
    /// この駒が特性を<b>手番の行動として</b>撃つか（行動パターンに
    /// <see cref="ActionKind.Skill"/> を載せているか）。
    ///
    /// 能動的な特性を <see cref="OnTurnStart"/> から <see cref="OnAction"/> へ移すときの
    /// 分岐に使う。**同じ特性を持つ駒を全部まとめて移すことはできない**——継ぎ当て（Mender）は
    /// 味方のノノと敵の従軍司祭長が共有していて、司祭長は行動パターンを持たない。
    /// 無条件に移すと司祭長の回復だけが静かに消える（gradient / aim の候補波 3b がそれを踏んだ）。
    ///
    /// 移行が終わっていない駒はターン頭の無条件発火のまま、載せた駒だけが手番で撃つ。
    /// アクティブ/パッシブの全面分離が済めばこの分岐は要らなくなる（第11期の残件）。
    /// </summary>
    protected static bool ActsOnPattern(UnitState self)
        => self.Def.Actions is { } acts && acts.Any(a => a.Kind == ActionKind.Skill);

    /// <summary>
    /// 自分のターンに動くか。false は「自分からは動かない」という設計であって、無力化とは限らない。
    ///
    /// <paramref name="kind"/> は<b>その手番に何をしようとしているか</b>。
    /// 「動けない」には二種類あって、無力化（痺れ・のろま）は何をするのも止めるが、
    /// 不動（カド）が止めているのは<b>攻撃だけ</b>——「自分からは決して攻撃しない」であって
    /// 「手番を持たない」ではない。種別を渡さないと、この二つを同じ false でしか表現できず、
    /// 不動の駒に能動スキルを持たせられない（棘守り＝<see cref="ThornGuardTrait"/> がそれを要る）。
    ///
    /// <see cref="UnitDef.Actions"/> を持たない駒は <see cref="ActionKind.Attack"/> で問われるので、
    /// 種別を無視する実装は従来とまったく同じ答えを返す。
    /// </summary>
    public virtual bool CanAct(BattleContext ctx, UnitState self, ActionKind kind) => true;

    /// <summary>
    /// <see cref="CanAct"/> が false のとき、それが「差し出したターン」なのか
    /// 「もともと持っていないターン」なのか。
    ///
    /// 号令（ガン）や据え（バン）は「動かなかった味方」を資源に変えるが、
    /// 不動（カド）や追い打ち（ハギ）はそもそも自分のターンに振らない型なので、
    /// 差し出すものが無い。ここを区別しないと、**編成時に一度払っただけの静的なマイナスが
    /// 毎ターンの収入に化ける**（カドは第五波で号令から無償で +8/ターンを受け取り続けていた）。
    /// のろま（ドルガ）は毎ターン実際にターンを失うので true のままでよい。
    /// </summary>
    public virtual bool SurrendersTurn => true;

    /// <summary>
    /// その駒の <see cref="StatusKeys.IdleTurn"/> が「差し出された本物の空き」か。
    /// 号令（<see cref="RallyTrait"/>）も据え（<see cref="BulwarkTrait"/>）もここを通す。
    ///
    /// **2箇所にコピーしない。** 以前は号令だけがこの判定を持ち、据えは持っていなかったので、
    /// ハギ（<see cref="PursuerTrait"/>）が据えの −50% を無償で受け取っていた。
    /// 片方だけが直されて食い違うのを防ぐために、条件はここ1箇所に置く。
    ///
    /// 種別は <see cref="ActionKind.Attack"/> 固定で問う。訊いているのは
    /// 「その駒が通常の手番で殴りに行く型か」であって周期の現在位置ではない
    /// （号令はターン開始に走るので <c>ActionIndex</c> はすでに次の行動へ進んでいる）。
    /// </summary>
    public static bool SurrenderedTurn(BattleContext ctx, UnitState u)
        => !u.Traits.Where(t => !t.CanAct(ctx, u, ActionKind.Attack))
                    .Any(t => !t.SurrendersTurn);

    /// <summary>
    /// ターン外の攻撃（割り込み・追い打ち）ができるか。
    ///
    /// <see cref="CanAct"/> と分けているのは、あれが二つの別物を兼ねているため。
    /// 不動（カド）や追い打ち（ハギ）の CanAct=false は「自分のターンには振らない」という
    /// 設計上の型であって、割り込みこそが役割そのもの。ここで CanAct を流用すると両方が無価値になる。
    /// 一方で痺れ・のろまは無力化なので、ターン外の攻撃も止まらないと
    /// 「動かされれば縛められていても振れる」ことになり、マイナス特性が配置で消える。
    /// </summary>
    public virtual bool CanReact(BattleContext ctx, UnitState self) => true;

    public virtual int ModifyAttack(UnitState self, int atk) => atk;

    /// <summary>攻撃パターンを状況で書き換える。後列でだけ貫きになる、など。</summary>
    public virtual AttackPattern ModifyPattern(UnitState self, AttackPattern p) => p;
    public virtual int ModifyIncomingDamage(UnitState self, int dmg) => dmg;
    public virtual void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt) { }
    public virtual void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source) { }
    public virtual void OnKill(BattleContext ctx, UnitState self, UnitState victim) { }
    public virtual void OnAllyDeath(BattleContext ctx, UnitState self, UnitState dead) { }

    /// <summary>
    /// 味方の誰かがダメージを受けたとき。<see cref="OnAllyDeath"/> の鏡で、
    /// 発火点は <see cref="OnDamaged"/> の直後（本人以外の生存チームメイトへ通知）。
    ///
    /// <b>破片（Armor）で受け切った被弾では呼ばれない。</b> ApplyDamage が
    /// 「何も起きなかった」として早期 return する位置より後ろにあるため——
    /// 被弾強化も反撃もそこで止まる、という既存の規則にそのまま乗っている。
    ///
    /// 出どころ（敵か味方の事故か）で絞らずに全部流す。<see cref="OnDamaged"/> と同じ方針で、
    /// 「敵からの被弾だけを見る」かどうかは受け手の特性が <paramref name="source"/> で決める。
    /// ここで絞ると、味方の事故に反応する駒を後から足せなくなる。
    ///
    /// 死亡処理（HandleDeath）より<b>前</b>に走る。致命の一撃なら、味方が倒れる前に
    /// 割り込みが刺さる（棘が自分の死の前に刺し返すのと同じ順序）。
    /// </summary>
    public virtual void OnAllyDamaged(BattleContext ctx, UnitState self, UnitState ally,
                                      int dmg, UnitState? source) { }

    /// <summary>敵味方を問わず誰かが倒れたとき。自分自身の死でも呼ばれる。</summary>
    public virtual void OnAnyDeath(BattleContext ctx, UnitState self, UnitState dead) { }

    /// <summary>自分が倒れたとき。分裂や置き土産に使う。</summary>
    public virtual void OnDeath(BattleContext ctx, UnitState self) { }

    /// <summary>隊列が動かされたとき。押し出された側・下がった側の両方で呼ばれる。</summary>
    public virtual void OnMoved(BattleContext ctx, UnitState self, Row from, Row to) { }

    /// <summary>味方の誰かが動かされたとき。移動を支援に変える駒が見る。</summary>
    public virtual void OnAllyMoved(BattleContext ctx, UnitState self, UnitState moved) { }

    /// <summary>
    /// 会戦（Engagement）の部隊戦境界で呼ばれる。エンジンが StatusKeys の全カウンタと
    /// AtkBonus を一律に消した後に走るので、**持ち越したい状態だけ**をここで再構成する。
    /// 既定は何もしない（Counters に残した特性私有のカウンタはそのまま持ち越される。
    /// 継ぎ接ぎの charges / sewn が会戦スコープになるのはこの既定の帰結）。
    /// BattleContext は渡さない——ログもイベントも無く、次の盤面はまだ存在しない場所。
    /// </summary>
    public virtual void OnCarryOver(UnitState self) { }
}

// ---------------------------------------------------------------
// マイナス特性
// ---------------------------------------------------------------

/// <summary>巻き込み。単体では味方を削るだけの事故。Rage 持ちと組むと燃料になる。</summary>
public sealed class SplashTrait : Trait
{
    public override TraitId Id => TraitId.Splash;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        if (dealt <= 0) return;
        int spill = Math.Max(1, dealt / 2);

        foreach (UnitState ally in ctx.LivingMembersShuffled(self.TeamId))
        {
            if (ally == self) continue;
            if (!FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;

            ctx.Log($"    余波: {self.Name} の攻撃が {ally.Name} を巻き込む", LogKind.FriendlyFire);
            ctx.ApplyDamage(ally, spill, self, isFriendlyFire: true);
        }
    }
}

/// <summary>臆病。HPが3分の2を切ると後列へ下がる。Sniper と組むと「逃げること」が正解になる。</summary>
public sealed class CowardTrait : Trait
{
    public override TraitId Id => TraitId.Coward;

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (self.Row == Row.Back) return;
        // 半分まで待つと、後列へ着いた時点で命が残らない。
        // 早く逃げたほうが臆病らしくもある。
        if (self.Hp * 3 > self.MaxHp * 2) return;

        int? dest = ctx.FindBackSlotFor(self);
        if (dest is null) return;

        UnitState? pushed = ctx.PickOne(
            ctx.LivingMembers(self.TeamId).Where(u => u.Slot == dest.Value).ToList());
        ctx.SwapSlots(self, dest.Value);

        if (pushed is null)
            ctx.Log($"    {self.Name} は耐えきれず一列後ろへ下がった", LogKind.Trigger);
        else
            ctx.Log($"    {self.Name} が {pushed.Name} を突き飛ばして後ろへ逃げた", LogKind.FriendlyFire);
    }
}

/// <summary>支援拒否。回復も強化も通らない代わりに、呪いや弱体も通らない。</summary>
/// <summary>
/// 誓約が壊れている。味方全体に配られる強化・弱体を自分では受け取らず、
/// <b>隣接する味方へそのまま渡す</b>（<see cref="BattleContext.SupportTargets"/>）。
///
/// もとは単なる無効化だった。マイナスがその駒の中で閉じていて、隣に誰を置いても何も起きず、
/// 「マイナスを利益に変える駒と組む余地が無い」という分かち（ドハ）と同じ穴だった。
/// 渡す形にすると隣接の選択が編成の判断になる。特に逆しま（ウツ）を隣に置くと、
/// 弱体が直接ぶんと拡散ぶんで二重に乗る。
///
/// 対象を1体選ぶ支援（継ぎ当て・縛め・移り木）は今まで通り受け取らない。
/// </summary>
public sealed class StoicTrait : Trait
{
    public override TraitId Id => TraitId.Stoic;
    public override bool BlocksSupport => true;
}

/// <summary>生贄。開始時に味方を削る。削りは「ダメージ」として処理されるので Rage を起動できる。</summary>
public sealed class SacrificeTrait : Trait
{
    public const int Amount = 14;
    public override TraitId Id => TraitId.Sacrifice;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
    {
        // 全体ではなく隣接のみ。誰を削るかがプレイヤーの選択になる。
        // 被弾強化の駒を隣に置けば、コストがそのまま起動スイッチに変わる。
        foreach (UnitState ally in ctx.LivingMembersShuffled(self.TeamId))
        {
            if (ally == self) continue;
            if (!FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;
            ctx.Log($"  {self.Name} が隣の {ally.Name} から生気を抜いた（-{Amount}）", LogKind.FriendlyFire);
            ctx.ApplyDamage(ally, Amount, self, isFriendlyFire: true, lethal: false);
        }
    }
}

/// <summary>
/// 大食い。毎ターン味方からHPを吸う。<b>傷ついているほど吸う量が増える</b>（自己修復）。
///
/// <para>巨躯（壁）の代金として働かせるための形。壁の見返りは肩代わりした量に比例して
/// 増えるのに、代金が固定だと**守るほど実質無料**になる。README が繰り返し記録している
/// 「積み上げは積んだ量に比例するコストを持つべき」（溜めのコストはテンポで積んだ量と
/// 独立だったため、最も長い波で無料になった）と同じ穴。</para>
///
/// <para>肩代わり量に直接比例させるのではなく<b>欠けたHPに比例</b>させているのは、
/// そのほうが「守った履歴の累積」になるため。壁として立ち続けた結果としてHPが減り、
/// 減った分だけ維持費が上がる。フレーバー（維持費が高すぎる。連れて行くと部隊が保たない）
/// にもそのまま乗る。</para>
///
/// <para>副産物として<b>回復役に仕事が生まれる</b>。壁を繕えば維持費が下がるので、
/// 継ぎ当て（ノノ）がゴルムを癒す意味ができる。固定コストのときは
/// 耐久(ガルド×ノノ) だけが一貫して悪化していた。</para>
/// </summary>
public sealed class DrainTrait : Trait
{
    /// <summary>無傷のときに味方1体から吸う量。</summary>
    public const int Amount = 4;

    /// <summary>HPが尽きかけたときに上乗せされる最大量。欠けたHPの割合で線形に効く。</summary>
    public const int WoundedExtra = 6;

    public override TraitId Id => TraitId.Drain;

    /// <summary>いま味方1体から吸う量。無傷で <see cref="Amount"/>、瀕死で +<see cref="WoundedExtra"/>。</summary>
    public static int DrawOf(UnitState self)
        => Amount + WoundedExtra * Math.Max(0, self.MaxHp - self.Hp) / Math.Max(1, self.MaxHp);

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        int draw = DrawOf(self);
        int gained = 0;
        // 吸う順を混ぜる。席番号順だと、途中で誰かが落ちたときに
        // 「誰が吸われる前に落ちたか」が席番号で決まる（LivingMembersShuffled 参照）。
        foreach (UnitState ally in ctx.LivingMembersShuffled(self.TeamId))
        {
            if (ally == self) continue;
            ctx.ApplyDamage(ally, draw, self, isFriendlyFire: true);
            gained += draw;
        }
        if (gained > 0)
        {
            ctx.Heal(self, gained);
            ctx.Log($"    {self.Name} が味方から精気を吸った（1体あたり {draw} / 計 +{gained}）",
                    LogKind.FriendlyFire);
        }
    }
}

/// <summary>
/// のろま。奇数ターンしか動かない。
///
/// **却下: <c>Actions = [Attack, Charge]</c> への移行（第11期 Phase BA）。**
/// 見た目は溜めそのものだが、仕組みでは表現できないと分かったので特性のまま残す。
/// 位相自体は合っている（<c>[Attack, Charge]</c> で T1 に振る。逆順ではない）が、
/// 溜めと のろま は**別の意味**で、次の2つが同時にずれる。
///
/// 1. <b>ターンを差し出すかどうか。</b> のろまは <see cref="Trait.SurrendersTurn"/> が真
///    ——毎ターン実際にターンを失うので、号令（ガン）と据え（バン）が買い取る。
///    <see cref="ActionKind.Charge"/> は逆に <c>IdleTurn</c> を立てない（第10期 AA。
///    溜めは「行動できない」ではなく「構造的に行動しない」）。移すとこの収入が消え、
///    溜め (ガン×ドルガ×カド) が 第3波 99.0→98.0 / 第4波 99.5→99.0 / 第5波 40.5→37.5。
/// 2. <b>周期の進み方。</b> のろまは <c>ctx.Turn</c> の偶奇（絶対時刻）で決まるが、
///    <see cref="UnitState.ActionIndex"/> は手番が回ってきたときにしか進まない
///    （第10期 AA。溜めの途中で痺れても続きから再開する）。縛め（クグ）に縛られると
///    のろまは振る番を失うのに、Actions は振る番を取っておく。1 を潰しても
///    溜め改 (クグ×バン×ガン) だけは残り、むしろ広がった（第3波 85.5→88.5 / 第5波 27.0→29.5）。
///
/// 差分ゼロにするには <see cref="UnitAction"/> にフラグが2つ要る。1つの特性のために
/// 第10期 AA の設計判断をユニット単位で覆すことになるので、**移さないほうを採った。**
/// のろま＝無力化、溜め＝構造的不行動。似ているのは形だけで、中身は反対のもの。
/// </summary>
public sealed class SluggishTrait : Trait
{
    public override TraitId Id => TraitId.Sluggish;

    // 種別は見ない。のろまは無力化なので、溜めでも術でも偶数ターンには動けない
    // （不動＝「攻撃だけ断る」との違いがここ。Trait.CanAct の説明を参照）。
    public override bool CanAct(BattleContext ctx, UnitState self, ActionKind kind)
    {
        bool act = ctx.Turn % 2 == 1;
        if (!act) ctx.Log($"    {self.Name} はまだ動き出せない", LogKind.Action);
        return act;
    }

    // 割り込みでも動けない。のろまは「自分からは振らない」型ではなく無力化なので、
    // 動かされたからといって偶数ターンに振れてはいけない。
    // 現ロスターにのろま＋割り込みの同居はないため、この行は将来の穴を塞ぐためのもの。
    public override bool CanReact(BattleContext ctx, UnitState self) => ctx.Turn % 2 == 1;
}

/// <summary>脆弱。受けるダメージが5割増し。</summary>
public sealed class FrailTrait : Trait
{
    public override TraitId Id => TraitId.Frail;
    public override int ModifyIncomingDamage(UnitState self, int dmg) => dmg + dmg / 2;
}

// ---------------------------------------------------------------
// プラス特性
// ---------------------------------------------------------------

/// <summary>
/// 被弾強化。誰に殴られたかは問わない。味方の事故もすべて燃料。
///
/// 増加量は被弾の重さに比例する。回数ベースにすると、毒の1ダメージと
/// 一撃30ダメージが等価になり、後衛に隠れて細かい被弾を稼ぐのが
/// 最適解になってしまう。ここではリスクを負った側が伸びる。
///
/// 上限は意図的に設けない。天井はこの駒自身のHPが担う。
/// 大きく殴られれば大きく育つが、何度も殴られる前に倒れる。
/// </summary>
public sealed class RageTrait : Trait
{
    /// <summary>被ダメージ何点につき攻撃力+1か</summary>
    public const int DamagePerGain = 2;

    public override TraitId Id => TraitId.Rage;

    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        if (dmg <= 0 || !self.IsAlive) return;
        int gain = Math.Max(1, dmg / DamagePerGain);
        self.AtkBonus += gain;
        ctx.Log($"    {self.Name} の怒りが増した（攻撃 +{gain} → {self.CurrentAttack}）", LogKind.Trigger);
    }
}

/// <summary>
/// 後衛特化。一度後退してから後列にいるときだけ攻撃力2倍になり、攻撃が貫きに変わる。
/// 「逃げてから本領を発揮する」の実装。
/// </summary>
public sealed class SniperTrait : Trait
{
    public override TraitId Id => TraitId.Sniper;

    /// <summary>
    /// 後列にいるだけでは足りない。戦闘中に実際に後退したことが条件。
    /// 初期配置で後列に置くと、逃げる先が無いので代償が一度も掛からずに済んでしまう。
    /// 位置ではなく履歴で判定することで、「下がってから本領を発揮する」が実際に起きる。
    /// </summary>
    private static bool Ready(UnitState self) => self.Row == Row.Back && self.HasFallenBack;

    public override int ModifyAttack(UnitState self, int atk) => Ready(self) ? atk * 2 : atk;

    public override AttackPattern ModifyPattern(UnitState self, AttackPattern p)
        => Ready(self) ? AttackPattern.Pierce : p;
}

/// <summary>呪詛。敵全体を弱体化するが、隣接する味方にも漏れる。Stoic の隣なら漏れが無効。</summary>
public sealed class CurseTrait : Trait
{
    public const int EnemyDebuff = 6;
    public const int AllyLeak = 5;

    public override TraitId Id => TraitId.Curse;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
    {
        foreach (UnitState foe in ctx.LivingMembers(ctx.Opponent(self.TeamId)))
        {
            if (!foe.AcceptsSupport) continue;
            ctx.Dull(foe, EnemyDebuff, DullRoute.CurseEnemy);
        }
        ctx.Log($"  {self.Name} の呪詛が敵全体を蝕む（攻撃 -{EnemyDebuff}）", LogKind.Trigger);

        // 漏れは味方全体へ。角に置いて隣を空ければ無償、という抜け道を塞ぐ。
        // これで呪詛は「支援を受け付けない駒」か「火力に頼らない構成」を要求するようになる。
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self) continue;
            foreach (UnitState t in ctx.SupportTargets(ally))
                ctx.Dull(t, AllyLeak, DullRoute.CurseLeak);
        }
        ctx.Log($"    呪詛は味方にも漏れた（攻撃 -{AllyLeak}）", LogKind.FriendlyFire);
    }
}

/// <summary>
/// 庇う。前列にいるとき、味方への攻撃を一定確率で肩代わりする。
/// 肩代わりして受けた分だけ攻撃力が伸びる（ドハの分かちと同じ見返りの経路）。
///
/// 見返りが無かった頃、庇うは**プラスの顔をしたコスト**だった。
/// 2026-08-21 の監査では、ガルドを前列に置く制約を外すと試した8編成すべてで勝率が上がり、
/// 例外がひとつも無かった（逆しま+後備え +16.7、逆しま改 +11.5、死の連鎖+後備え +9.1 ほか）。
/// 肩代わりした分だけ早く落ちるだけで、払った代金の行き先が無かったため。
///
/// 伸びるのは**肩代わりした被弾だけ**で、素で殴られたぶんは対象外。
/// ここを区別しないと「前列のHP100が殴られ続ける」だけで育ち、庇うとの結び付きが切れる。
/// engine 側は肩代わりの成立時に <see cref="PendingKey"/> を立て、ここで消費する。
///
/// 上限は設けない。天井はガルド自身のHPが担う（Rage と同じ考え方）。
/// なお第五波は4種の攻撃パターンが同時に出るので庇う自体がほとんど発動しない。
/// この見返りは第二〜四波にしか入らない。第五波の反転は波の作り直しで見ること。
/// </summary>
public sealed class GuardianTrait : RedirectGainTrait
{
    public const int RedirectPercent = 50;

    public override TraitId Id => TraitId.Guardian;

    /// <summary>
    /// 傷の引き取り（第89期・<see cref="GatherRule"/>）。<b>庇いが成立した被弾のたび、
    /// 隣接する味方のうち傷がいちばん深い者から傷をひとつ自分へ移す。</b>
    ///
    /// <para><b>ここに置いて <see cref="RedirectGainTrait"/> には置かない。</b>
    /// 庇う（味方ガルド）と殉教（敵の殉教者）は基底で1行も違わない実装を共有しているので、
    /// 基底に置くと<b>敵側にも生える</b>。<c>gather check</c> の自己検査 (d) が敵側 0 回を確認する。</para>
    ///
    /// <para><b>印（<see cref="RedirectGainTrait.PendingKey"/>）を <c>base</c> より先に読む。</b>
    /// 基底の <c>OnDamaged</c> は<b>冒頭で印を読んで即座に 0 に落とす</b>ので、
    /// 後から読むと庇いの成立が取れない（指示書 §2-2 の ★）。</para>
    ///
    /// <para><b>移すのであって増やさない。</b> 1回の庇いにつき 1 つ・傷の数にも <c>dmg</c> にも比例させない
    /// （第84期の「傷1つの単価」の壁に自分から入らない）。盤面の傷の総量は保存する（自己検査 (b)）。
    /// <c>ApplyDamage</c> を通さないので <see cref="UnitState.AtkBonus"/> は 1 も動かない（自己検査 (c)）。</para>
    ///
    /// <para><b>隣接の窓口は <c>FormationRules.AreAdjacent</c>（<c>Stoic</c> が使っているものと同じ）。</b>
    /// 新しい隣接の読み方は作らない。<b><c>AcceptsSupport</c> は見ない</b>——取り上げるのは支援ではない
    /// （引き受け＝<see cref="BearTrait"/>・業と同じ扱い）。</para>
    ///
    /// <para><b>ガルドは傷を1つも読まない。</b> 集めた傷を使うのは必ず他人（終端は縫いのハリ）。
    /// しかも <c>Stoic</c> により <c>MostHurtAlly</c> の患者になれないので、
    /// <b>傷を集めるが自分は治らない</b>（自己検査 (e)）。</para>
    /// </summary>
    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        bool guarded = self.Counter(PendingKey) > 0;      // ★ base より先に読む（base は冒頭で 0 に落とす）
        base.OnDamaged(ctx, self, dmg, source);
        if (!guarded || !self.IsAlive) return;

        // **ここから下の2つの計数は版に依らない**（第86期の X1P と同じ作法）。
        // 紙のスループットの分子は Z0 の実測から取るので、規則の分岐より手前で数える。
        // **`ctx.PickOne` だけは規則が有効なときにしか呼ばない**——候補が2個以上あると `Roll` を消費するので、
        // ここで呼ぶと Z0 の乱数列が動いてしまう。
        UnitTally t = ctx.TallyOf(self);
        t.GatherGuards++;

        var pool = ctx.LivingMembers(self.TeamId)
            .Where(a => a != self && a.Counter(StatusKeys.Wound) > 0
                        && FormationRules.AreAdjacent(self.Slot, a.Slot)).ToList();
        if (pool.Count == 0) return;
        t.GatherHadDonor++;
        if (!ctx.Gather.Enabled) return;

        int best = pool.Max(a => a.Counter(StatusKeys.Wound));
        // **同数のタイブレークは席番号の昇順**（第90期 §1-1・第89期の自己検査 (h) の訂正）。
        // `ctx.PickOne` は候補が2個以上あると `Roll` を1つ消費するので、
        // **傷を移すだけで誰も読まない行でも乱数列がずれた**（第89期の実測: `compare` 17セル/10行）。
        // 機構の変更ではない——選び方を決定的にしただけ。
        UnitState donor = pool.Where(a => a.Counter(StatusKeys.Wound) == best)
                              .OrderBy(a => a.Slot).First();
        donor.SetCounter(StatusKeys.Wound, best - 1);
        int after = self.Counter(StatusKeys.Wound) + 1;
        self.SetCounter(StatusKeys.Wound, after);
        t.GatherTaken++;
        t.GatherDepthSum += after;
        if (after > t.GatherDepthMax) t.GatherDepthMax = after;
        ctx.Log($"    {self.Name} が {donor.Name} の傷を引き取った（傷 {best} → {best - 1} ／ {self.Name} の傷 {after}）",
            LogKind.Trigger);
    }
}

/// <summary>
/// 肩代わりで育つ介入役の共通部分。<b>庇う（ガルド）と殉教（敵の殉教者）で
/// 1行も違わない</b>ので、ここに寄せてある——**定義は1箇所**（CLAUDE.md）。
///
/// <para>分けてあるのは <see cref="Trait.Id"/> と、engine が読む割合だけ。
/// 割合を共有したままだと、殉教者の割合を振ったときに<b>味方ガルドを含む行が全部動いて
/// 交絡が戻る</b>（第34期で HP が波の総HPを一緒に動かしたのと同じ形）。</para>
///
/// <para><c>PendingKey</c> は**両者で共有する**。engine の介入の鎖はどちらか一方しか
/// 立てず（鎖は最初に成立した段で <c>return</c> する）、印は次の被弾1回で消費される。
/// 別キーにすると「どちらの段で逸れたか」を engine が覚える必要が出て、窓口が増える。</para>
/// </summary>
public abstract class RedirectGainTrait : Trait
{
    /// <summary>被ダメージ何点につき攻撃力+1か（Rage・分かちと同じ比率）。</summary>
    public const int DamagePerGain = 2;

    /// <summary>engine が肩代わりの成立を伝えるための印。次の被弾1回で消費する。</summary>
    public const string PendingKey = "guardPending";

    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        bool guarded = self.Counter(PendingKey) > 0;
        self.SetCounter(PendingKey, 0);

        // source が null の継続ダメージ（毒・燃焼）では育たない。
        // 印が立ったまま肩代わりが0ダメージで流れた場合に、次の毒の刻みを
        // 肩代わりと取り違えるのを防ぐ。
        if (!guarded || dmg <= 0 || source is null || !self.IsAlive) return;

        int gain = Math.Max(1, dmg / DamagePerGain);
        self.AtkBonus += gain;
        ctx.Log($"    {self.Name} が受けた傷が誓いを思い出させる（攻撃 +{gain} → {self.CurrentAttack}）",
            LogKind.Trigger);
    }

    /// <summary>
    /// 部隊戦の境界で肩代わりの印を消す。印は StatusKeys に無いので境界の一律掃除では
    /// 消えない。立ったまま持ち越すと（破片が全額吸って OnDamaged まで届かなかった場合
    /// など）、次の Battle の最初の被弾を肩代わりと取り違えて育ってしまう。
    /// </summary>
    public override void OnCarryOver(UnitState self) => self.SetCounter(PendingKey, 0);
}

/// <summary>
/// 殉教。<b>庇う（<see cref="GuardianTrait"/>）と挙動は1行も違わない。</b>
/// 別の <see cref="TraitId"/> にしてあるのは<b>割合を味方ガルドと分けるためだけ</b>
/// ——`RedirectPercent` を共有したままだと、殉教者の割合を振ったときに
/// ガルドを含む行（42編成中28行）が全部動いて、何を測ったのか決まらなくなる。
///
/// <para>割合は <see cref="MartyrRule"/> で外から差す（既定 <see cref="DefaultPercent"/> = 50）。
/// <b>書き換え可能な static のノブにしないこと</b>——Trait は共有シングルトンで
/// layout は戦闘を並列実行する（ColossusRule / YokeRule / HushRule と同じ判断）。</para>
///
/// <para>判定は engine 側（<c>SelectTargetChain</c> の庇うの段の直後）。同じ
/// <c>Row.Front</c> 条件・同じ <c>f != target</c> 条件で、<b>ガルドの段は1文字も触っていない</b>
/// ——<c>PickOne</c> は候補 0 個・1 個では <c>Roll</c> を消費しないので、
/// 段を1つ足しても乱数列は動かない（p=50 の同値検証がその証明）。</para>
/// </summary>
public sealed class MartyrTrait : RedirectGainTrait
{
    /// <summary>
    /// 逸れる確率。**ガルドの 50% とは別勘定**（分けた理由は上の doc）。
    ///
    /// <para><b>75 は測って決めた</b>（第35期・`guard percent` で 50 / 75 / 100 を掃引）。
    /// 50 では**庇うが作った固有の敗者が 0 行**で、同HP・庇うなしの対照と区別が付かなかった。
    /// 75 で 裂き (キリ×エグ) が 11.5 → 9.5 と閾値 10 を割り、**この盤面で初めて
    /// 「介入がその行を敗者にした」と帰属できる行が出た**（対照では 14.5 で敗者ではない）。
    /// 100 でも敗者は同じ 1 行きりなので、<b>最小介入で 75 を採る</b>。</para>
    ///
    /// <para><b>上げても頭打ちになる。</b> 庇いの窓は「殉教者が落ちる」と「勇者候補が落ちる」の
    /// 両方で閉じる。p を上げると逸れた被弾が殉教者に集中して**殉教者の生存Tはむしろ縮む**
    /// （1.82 → 1.68 → 1.57）ので、発火は p に比例せず 1.12 → 1.56 → 1.95 と逓減する。
    /// 律速の勇者候補の生存Tは 3.02 → 3.09 とほぼ動かない（第34期の結論の再確認）。</para>
    /// </summary>
    public const int DefaultPercent = 75;

    public override TraitId Id => TraitId.Martyr;
}

/// <summary>
/// 殉教の規則。<b>診断（guard）が割合を振るためだけに外から差せる。</b>
/// 既定は <see cref="Default"/> ＝ <see cref="MartyrTrait.DefaultPercent"/>、
/// <b>これが本採用の規則</b>。渡さない限り盤面は常にこの規則で動く。
///
/// <para><b>書き換え可能な static の調整ノブにしないこと。</b> Trait は共有シングルトンで、
/// layout は戦闘を並列実行する——static に置くと版の切り替えが他のスレッドの戦闘へ漏れるし、
/// <c>BattleEngine.Run</c> の「副作用も外部依存もない」もそこで壊れる
/// （<see cref="ColossusRule"/> / <see cref="YokeRule"/> / <see cref="HushRule"/> と同じ判断）。</para>
/// </summary>
public readonly record struct MartyrRule(int RedirectPercent)
{
    public static MartyrRule Default => new(MartyrTrait.DefaultPercent);
}

/// <summary>墓守。味方が倒れるたびに強くなり、回復する。</summary>
public sealed class NecroTrait : Trait
{
    public const int AllyStep = 5;
    public const int EnemyGain = 3;
    public const int HealOnDeath = 12;
    public const int AwakenAt = 3;

    public override TraitId Id => TraitId.Necro;

    // 層は毎ターン1つ落ちる。
    // これが無いと「放っておいても味方はいずれ死ぬ」ので、墓守が無条件に伸びてしまう。
    // 減衰があると、死を絶やさない編成でしか層が積み上がらない。
    // 減衰は「前のターンに味方が一人も倒れなかったとき」だけ。
    // 毎ターン必ず落ちる形にすると、連鎖を繋いでいる間まで目減りして
    // 積み上げる意味が消える。連鎖が途切れたときだけ罰する。
    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        int stack = self.Counter("necro");
        if (stack <= 0) return;

        bool chained = self.Counter("lastDeathTurn") >= ctx.Turn - 1;
        if (chained) return;

        SetStack(ctx, self, stack - 1, decayed: true);
    }

    public override void OnAnyDeath(BattleContext ctx, UnitState self, UnitState dead)
    {
        if (dead == self) return;

        if (dead.TeamId != self.TeamId)
        {
            self.AtkBonus += EnemyGain;
            return;
        }

        int stack = self.Counter("necro") + 1;
        self.SetCounter("lastDeathTurn", ctx.Turn);
        SetStack(ctx, self, stack, decayed: false);
        ctx.Heal(self, HealOnDeath);
        ctx.Log($"    {self.Name} が {dead.Name} を取り込んだ（{stack}層 / 攻撃 {self.CurrentAttack}）", LogKind.Trigger);

        if (stack == AwakenAt) ctx.Log($"    ★ {self.Name} の目の色が変わった", LogKind.Highlight);
    }

    // 覚醒後（AwakenAt層以上）は攻撃が薙ぎに変わる。
    // 追い打ち（ハギ）と組んだとき「1体倒す→層が増えて薙ぎになる→薙ぎで複数体を巻き込んで倒す→さらに増える」
    // という連鎖が回るようになる。単体攻撃のままだと層は伸びても手数が増えないので連鎖が続かなかった。
    public override AttackPattern ModifyPattern(UnitState self, AttackPattern p)
        => self.Counter("necro") >= AwakenAt ? AttackPattern.Sweep : p;

    /// <summary>層に応じた累積ボーナスを再計算して差分だけ反映する。</summary>
    private static void SetStack(BattleContext ctx, UnitState self, int stack, bool decayed)
    {
        stack = ApplyStack(self, stack);
        if (decayed) ctx.Log($"    {self.Name} の層が薄れた（{stack}層）", LogKind.Status);
    }

    /// <summary>SetStack の純計算部。OnCarryOver（ログの無い場所）と共用する。</summary>
    private static int ApplyStack(UnitState self, int stack)
    {
        stack = Math.Max(0, stack);
        int applied = self.Counter("necroBonus");
        int desired = AllyStep * stack * (stack + 1) / 2;
        self.AtkBonus += desired - applied;
        self.SetCounter("necro", stack);
        self.SetCounter("necroBonus", desired);
        return stack;
    }

    /// <summary>
    /// 部隊戦の境界。層を1つ落として持ち越す（ターン減衰と同じ「連鎖が途切れたら罰する」
    /// 思想の境界版）。AtkBonus はエンジンが一律 0 にした後なので、帳簿（necroBonus）も
    /// 0 に戻してから層ぶんを再適用する。敵撃破の EnemyGain は帳簿に載っていないので
    /// ここで自然に消える（会戦中に単調増加する量を作らないため）。
    ///
    /// lastDeathTurn も 0 に戻す。前の Battle のターン番号 T が残ると、次の Battle の
    /// ターン 2..T+1 で「前ターンに死があった」と誤判定されて減衰が止まる
    /// （味方が誰も死んでいないのに層がタダで保つ）。ターン1は counter >= 0 が常に
    /// 成立してもともと減衰しないので、これは二重減衰の防止ではなく偽の連鎖判定の防止。
    /// </summary>
    public override void OnCarryOver(UnitState self)
    {
        self.SetCounter("necroBonus", 0);
        ApplyStack(self, self.Counter("necro") - 1);
        self.SetCounter("lastDeathTurn", 0);
    }
}

/// <summary>巨躯。フラグ用。実効性能は MaxHp の数値側で表現する。</summary>
/// <summary>
/// 巨躯。<b>自分より後ろの列にいる味方</b>への攻撃を、型を問わず肩代わりする。
///
/// <para>もとは <c>Id</c> を返すだけの空のフラグで、「圧倒的な耐久」の実体は HP150 という
/// 数字だけだった。壁役のつもりで置かれているのに防御機構を1つも持たず、
/// それでいて大喰らいで毎ターン味方を削るので、測ると3方向から同じ結論が出ていた
/// （ablate で逆しま改から抜くと +4.4pt / pulse で全11編成とも味方への与ダメが敵向きを上回る /
/// そもそも実装が空）。文字通り壁として働かせる。</para>
///
/// <para><b>肩代わりは damage の層で解決する。</b> 庇う（Guardian）や後備え（RearGuard）は
/// <c>SelectTarget</c> で主目標を差し替えるだけなので、範囲攻撃の巻き込みには触れない。
/// ここは <c>ApplyDamage</c> の中なので薙ぎ・全体・貫きの一発ずつを拾える。</para>
///
/// <para><b>飲み込んだ分は「守った相手」に返す（吐き戻し）。</b> 肩代わり4種のうち見返りを
/// 持たないのは巨躯だけで、90% を後方全員から引き受けてそこで価値が消えていた
/// （第19期 route: ナラの削り7のうち6をゴルムが食い、ムドの Rage が +1 に潰れる /
/// 第21期 swap: ノノの回復の最大の受け手がゴルム / 第22期: 大喰らいが隠れた回復経路）。
/// <b>見返りをゴルム自身ではなく守った相手に返す</b>ので、肩代わりは価値を消さず経路を変えるだけになり、
/// 「後ろに誰を置くか」が初めて判断になる。実装は <c>BattleContext.ApplyDamage</c> の巨躯の分岐。</para>
///
/// <para><b>ゴルム自身は育たない。</b> 分かち方式（全被弾に反応）にすると、前列でHP150・素の被弾が
/// 膨大なので「壁だから育つ」になって巨躯との結び付きが切れる（GuardianTrait のコメントが同じ失敗を記録している）。</para>
///
/// <para><b>列の前後で効き目が変わる。</b> 前列に置けば中衛と後列を守り、中衛なら後列だけ、
/// 後列に置けば誰も守らない。5体を6枠に入れる限り必ず1枠空くので
/// 「デメリットの隣を空ける」が常に最適解になる、という既存の穴（README の未解決の課題）に対して、
/// **置き場所そのものが効き目を決める**駒になっている。</para>
/// </summary>
public sealed class ColossusTrait : Trait
{
    /// <summary>後ろの味方への攻撃を何割引き受けるか。</summary>
    public const int Percent = 90;

    /// <summary>
    /// 飲み込んだダメージ何点につき、<b>庇った相手</b>の攻撃力+1 か（吐き戻し）。
    ///
    /// <para>怒り（Rage）・庇う（Guardian）・分かち（Sharer）は 2 だが、巨躯は
    /// 90% × 後方全員で、庇う50%単体・分かち40%の<b>およそ2倍を吸う</b>ので
    /// 半分の効率にしてある。<b>ここが最初に振る調整ノブ</b>で、
    /// 上がりすぎたら 4 → 6, 8 と振る（ゴルムの数値 150/10/3 は触らない）。</para>
    /// </summary>
    public const int DamagePerGain = 4;

    /// <summary>
    /// 腹。<b>肩代わりで飲み込んだ量の残高</b>（第36期）。<c>ApplyDamage</c> の巨躯の分岐で、
    /// 吐き戻しとまったく同じ場所・同じ量（<c>blocked</c>）を積む。
    ///
    /// <para><b>大喰らい（<see cref="DrainTrait"/>）で吸った分は数えない。</b> あれは毎ターン
    /// 無条件に走るので、混ぜるとまどろみが盤面の出来事から切れてただの周期になる
    /// （「毎ターン」→「〜したとき」への変換原則）。腹は<b>殴られた誰かを庇ったとき</b>にだけ増える。</para>
    ///
    /// <para><b>置き場は <see cref="StatusKeys"/> ではなく特性の私有カウンタ。</b>
    /// engine が書いて特性が読むカウンタは既に <see cref="RedirectGainTrait.PendingKey"/> /
    /// <see cref="ThornGuardTrait.PendingKey"/> が同じ形で、腹・まどろみ・還しは
    /// <b>すべて巨躯の持ち物</b>なので定義を1箇所（この型）に集められる。
    /// 会戦の境界は <see cref="OnCarryOver"/> で捨てる（<see cref="StatusKeys.All"/> の
    /// 一律掃除には載らないので、こちらに書かないと腹が部隊戦をまたぐ）。</para>
    /// </summary>
    public const string BellyKey = "belly";

    /// <summary>
    /// 還しを1戦で使い切ったか。<b>蘇生（墓守・継ぎ接ぎ）で戻って再び倒れても二度は還さない。</b>
    /// 腹を空にするだけでは担保にならない——戻った後に飲み込み直せばもう一度発火してしまう。
    /// </summary>
    public const string RefundSpentKey = "bellySpent";

    /// <summary>
    /// まどろみの閾値。腹がこの量に達した手番を失う（達した分だけ腹から引く）。
    ///
    /// <para><b>掃引ではなく実測から決めた</b>（第36期 Phase 0-1・<c>gullet belly</c>）。
    /// 飲み込みの多い上位5行で 1戦あたり 1.09〜1.49 回眠る値。40 だと惨禍×死の連鎖が
    /// 生存Tの 94%＝実質毎ターン眠り、80 だと上位5行のうち4行が 1.0 を割って「1〜2回」に届かない。
    /// 全18行の総平均では 1.28 回 / 生存T 5.2 ＝ ゴルムが手番の約25%を失う。
    /// 根拠は design/PHASE36_GOLM_BELLY.md。</para>
    /// </summary>
    public const int SlumberThreshold = 60;

    /// <summary>
    /// 還しで返す腹の割合（%）。<b>総量</b>で、生存味方の頭数で割って配る（1体あたりではない）。
    ///
    /// <para><b>実測から決めた</b>（第36期 Phase 0-1）。ゴルムが落ちた戦の腹は平均 81 なので
    /// 81 × 25% ≒ 20 点 ＝ ノノの繕い（<see cref="MenderTrait.Amount"/> = 14）1.4 回ぶん。
    /// 指示書の狙い「繕い1〜2回ぶん」＝ 14〜28 点の帯のちょうど中央。</para>
    /// </summary>
    public const int RefundPercent = 25;

    public override TraitId Id => TraitId.Colossus;

    /// <summary>
    /// 還し。<b>倒れたとき、腹の残りの一部を生存味方へ回復として配る（1戦1回）。</b>
    ///
    /// <para><b>経路は <c>ctx.Heal</c>。</b> 渇き（第三波）で封じられるのは<b>意図</b>で、
    /// ロスターに回復供給を1つ増やす＝渇きが課税できる対象を増やす、というのが本期の狙いの半分。
    /// ガルドの <c>Stoic</c> も弾く（<c>AcceptsSupport</c>）ので、ガルドを含む行では
    /// <b>頭数では割られるのに受け取れない</b>＝取り分が虚空へ消える。
    /// <b>吐き戻しとはここが違う</b>——あちらは <c>SupportTargets</c> を通すので隣へ漏れる。
    /// ウツ（逆しま）には無風（<see cref="PerverseTrait"/> は <c>AtkBonus</c> しか読まない）。</para>
    ///
    /// <para><b>1戦1回は腹を空にするだけでは担保できない。</b> 蘇生（墓守・継ぎ接ぎ）で戻った後に
    /// 飲み込み直せばもう一度発火してしまうので、<see cref="RefundSpentKey"/> を別に立てる。
    /// 印は<b>配れたかどうかに関わらず</b>死んだ時点で立てる——「還す機会は1戦に1度」であって
    /// 「還せるまで持ち越す」ではない。</para>
    ///
    /// <para><b>蘇生より先に走る。</b> <c>HandleDeath</c> の通知順は
    /// <c>OnKill → OnDeath（ここ）→ OnAnyDeath（墓守）→ OnAllyDeath（蘇生）</c> なので、
    /// この還しで縫い直された味方は受け取れない。既存の順序依存に乗っているだけで、動かさない。</para>
    /// </summary>
    public override void OnDeath(BattleContext ctx, UnitState self)
    {
        if (!ctx.Colossus.Refund) return;
        if (self.Counter(RefundSpentKey) > 0) return;
        self.SetCounter(RefundSpentKey, 1);

        int belly = self.Counter(BellyKey);
        self.SetCounter(BellyKey, 0);

        int total = belly * ctx.Colossus.RefundPercent / 100;
        var back = ctx.LivingMembers(self.TeamId).Where(u => u != self).ToList();
        if (total <= 0 || back.Count == 0) return;

        // 頭数で割る。**1体あたりに配るのではない**——体数で回復総量が変わると、
        // 還しが「編成の枚数」を測る量になってしまう（号令の SupportTargets が
        // 割り算をしないのとは逆の判断で、あちらは毎ターン走るばら撒き、こちらは1戦1回の分配）。
        int each = total / back.Count;
        if (each <= 0) return;   // 端数で全員 0 なら何も起きていない。ログも出さない（吐き戻しと同じ）

        // **届いた量を実測する。** 額面（腹 × 率）をそのまま書くと、渇き（第三波）で
        // 1点も通っていない戦が「還した」に数えられる。渇きの判定を**ここに書き写さない**のが要点
        // ——回復を止める場所は ctx.Heal の入口1箇所、という規則をここでも守る（CLAUDE.md）。
        int before = back.Sum(u => u.Hp);
        foreach (UnitState u in back) ctx.Heal(u, each);
        int gained = back.Sum(u => u.Hp) - before;

        ctx.TallyOf(self).Refunds++;
        ctx.TallyOf(self).Refunded += gained;
        ctx.Log($"    {self.Name} が飲み込んだものが還った（各 +{each} / 届いた {gained}）",
            LogKind.Trigger);
    }

    /// <summary>
    /// 部隊戦の境界で腹を空にし、還しの使用済み印も落とす。腹は Battle スコープの資源
    /// （<see cref="StatusKeys.All"/> の一律掃除には載らないので、ここで明示的に捨てる）。
    /// </summary>
    public override void OnCarryOver(UnitState self)
    {
        self.SetCounter(BellyKey, 0);
        self.SetCounter(RefundSpentKey, 0);
    }
}

/// <summary>
/// 巨躯の規則。<b>診断（gullet）が版を並べて 1 回の実行の中で比べるためだけに外から差せる。</b>
/// 既定は <see cref="Default"/> ＝ <see cref="ColossusTrait"/> の const ＋ 吐き戻し有効で、
/// <b>これが本採用の規則</b>。渡さない限り盤面は常にこの規則で動く。
///
/// <para><b>書き換え可能な static の調整ノブにしないこと。</b> Trait は共有シングルトンで、
/// layout は戦闘を並列実行する——static に置くと版の切り替えが他のスレッドの戦闘へ漏れるし、
/// <c>BattleEngine.Run</c> の「副作用も外部依存もない」もそこで壊れる。引数で渡せば決定性がそのまま残る。</para>
/// </summary>
/// <para><b>第36期の4つは既定値つきで足してある。</b> 第23期の <c>gullet</c> が組む4版
/// （<c>new ColossusRule(90, 4, Regurgitate: false)</c> など）を1文字も書き換えずに済ませるため——
/// あの4版は「吐き戻しの前後」を測る対照で、腹の有無とは無関係。
/// <b>パラメータの既定値はどちらも無効</b>なので、明示しない限り第35期の盤面が出る。
/// <c>Default</c>（＝本採用の規則）だけが <c>Refund</c> を立てる。</para>
public readonly record struct ColossusRule(int Percent, int DamagePerGain, bool Regurgitate,
                                           bool Slumber = false,
                                           int SlumberThreshold = ColossusTrait.SlumberThreshold,
                                           bool Refund = false,
                                           int RefundPercent = ColossusTrait.RefundPercent)
{
    /// <summary>
    /// 本採用の規則。<b>還しは有効・まどろみは無効</b>（第36期の測定で決めた）。
    ///
    /// <para><b>まどろみを規則として残してあるのは対照のため</b>で、逆位
    /// （<see cref="InversionTrait"/>・測って採らなかった盤面ルール）と同じ扱い。
    /// <c>Slumber: true</c> を渡した版は <c>gullet belly4</c> がいつでも組み直せる。</para>
    /// </summary>
    public static ColossusRule Default =>
        new(ColossusTrait.Percent, ColossusTrait.DamagePerGain, Regurgitate: true, Refund: true);
}

/// <summary>処刑。とどめを刺すたびに攻撃力が上がる。</summary>
public sealed class ExecutionerTrait : Trait
{
    public const int Gain = 7;
    public override TraitId Id => TraitId.Executioner;

    public override void OnKill(BattleContext ctx, UnitState self, UnitState victim)
    {
        self.AtkBonus += Gain;
        ctx.Log($"    {self.Name} は仕留めるたびに冴える（攻撃 +{Gain}）");
    }
}

/// <summary>分裂。倒れると子が湧く。墓守にとっては「損失にならない死」の供給源。</summary>
public sealed class SplitterTrait : Trait
{
    /// <summary>この体が既に一度崩れたか。<b>自分側に置く</b>（理由は <see cref="OnDeath"/>）。</summary>
    public const string SplitKey = "split";

    public override TraitId Id => TraitId.Splitter;

    /// <summary>
    /// 倒れると胞子が2体湧く。<b>1体につき1回だけ。</b>
    ///
    /// <para>X字化で召喚専用4枠を作るまで、この制限は「6枠5体＝空き1」という枠数由来の
    /// 暗黙の上限に隠されていた。枠が空いた結果、<b>ムグが倒れて2体 → ヴェル
    /// （<see cref="ReviverTrait"/>）が縫い直した体が再び倒れて更に2体</b>で同時4体まで湧き、
    /// 「死の連鎖 (リィカ軸)」から失敗の可能性が消えて計測器として死んでいた。
    /// 問題は枠数ではなく<b>同じ骸から二度湧くこと</b>なので、ここで塞ぐ。</para>
    ///
    /// <para><b>判定は自分側のカウンタで行う。</b><see cref="ReviverTrait"/> が立てる
    /// <c>sewn</c> を見る手もあるが、あれは <c>OnAllyDeath</c> の中で立つので
    /// <c>OnDeath</c> との発火順に依存する（<c>OnAllyDeath</c> が先なら1回目の分裂まで止まる）。
    /// 自分側なら順序に依存しない。</para>
    ///
    /// <para>却下した案:</para>
    /// <list type="bullet">
    /// <item>召喚スロットを2枠（○中1・○中3）に絞る——上限で殴るだけで二度湧きの経路は残り、
    /// ○前2・○後2 が死に地形になる</item>
    /// <item><c>ctx.Summon</c> が null を返したら諦める——既にそうなっている。枠が4つある以上効かない</item>
    /// <item>胞子を 2 → 1 体にする——二度湧きが残るので上限は結局2のまま。しかも1回目の分裂まで
    /// 弱くなり、「ムグ本体の死という有限で高い買い物の対価」という設計意図が痩せる</item>
    /// </list>
    /// </summary>
    public override void OnDeath(BattleContext ctx, UnitState self)
    {
        if (self.Counter(SplitKey) > 0) return;   // 縫い直された体からは、もう胞子は出ない
        self.SetCounter(SplitKey, 1);

        for (int i = 0; i < 2; i++)
            ctx.Summon(UnitCatalog.Spore, self.TeamId);
    }

    /// <summary>
    /// 部隊戦の境界で崩れた記録を捨てる。<b>持ち越さない。</b>
    /// <see cref="ReviverTrait"/> の <c>charges</c> は持ち越す実装だが、あちらは
    /// 「ヴェルというユニットの資源」で、こちらは「ムグ1体の体が既に一度崩れたか」という状態。
    /// 戦闘が変われば体は元に戻っている、と読む。
    /// </summary>
    public override void OnCarryOver(UnitState self) => self.SetCounter(SplitKey, 0);
}

/// <summary>
/// 破裂の着火の対象（第59期）。
///
/// <para>第57〜58期で燃焼は通貨になったが、<b>書き手はボルグ1枚しかない</b>。
/// 燃焼を読む駒はすべてボルグとの AND ゲートになり、燃焼が出る行はボルグを含む 11 行に閉じていた。
/// ゾトを含む 7 行との<b>重複は 0</b> なので、破裂に火を足すと
/// <b>燃焼が一度も発生していない 7 行にいきなり火が入る</b>（かつボルグ系の捨て札を増やさない）。</para>
/// </summary>
public enum BlazeTargets
{
    /// <summary>着火なし（現行の挙動）。<b>既定。</b></summary>
    None,
    /// <summary>味方の巻き込みを受けた者だけ。火選り・熾火が読む在庫を作る。敵側の打点は増えない。</summary>
    AllyOnly,
    /// <summary>破裂が当たった全員（敵も味方も）。</summary>
    Both,

    /// <summary>
    /// 敵だけ。<b>ノブではなく対照</b>（<c>DivertRule.SelfMark</c> / <c>FinisherRule.Consume</c> と同じ扱い）。
    ///
    /// <para><see cref="Both"/> は「味方に在庫を作る」と「敵に打点を足す」という
    /// <b>符号の違う2つの効果を1つの動作に持つ</b>ので、そのままでは
    /// <c>ablate</c> でも <c>swap</c> でも2つが合算されて符号が消える（第41期の突き返しと同型）。
    /// <b>味方側だけを 0 にできる版</b>を対にして初めて、
    /// 「勝率が上がったのは層に乗ったからか、敵が燃えたからか」が割れる。</para>
    /// </summary>
    FoeOnly
}

/// <summary>
/// 破裂の着火の強度（第59期）。<b>診断（blaze）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない（既定は <see cref="Default"/> ＝ <see cref="BlazeTargets.None"/>）。
///
/// <para><b>書き換え可能な static のノブは置かない</b>（Trait は共有シングルトンで
/// <c>layout</c> は戦闘を並列に回すため）。<see cref="BattleEngine.Run"/> に引数で渡す。</para>
///
/// <para><b><see cref="BlazeTargets.None"/> は現行と1セルも違わない</b>
/// ——<see cref="BattleContext.Ignite"/> は乱数を1つも引かないので、
/// 着火を既存のループの中に足しても乱数列は動かない。これが診断の検算になる
/// （ゾトを含まない 52 行が <c>None</c> と <c>Both</c> で 0 件）。</para>
/// </summary>
public readonly record struct BlazeRule(BlazeTargets Targets)
{
    /// <summary>
    /// 採用値（第59期）。<b>指示書 §2-1 の既定は <see cref="BlazeTargets.None"/> だったが、
    /// 測って <see cref="BlazeTargets.Both"/>（B 案）を採った。</b>
    ///
    /// <para>A 案（味方だけ）は<b>7行すべてで負</b>（−2.9〜−24.1pt・別 seed 帯で再現）。
    /// 味方側の着火は<b>燃焼で味方を 0.27〜0.69 体/戦 余分に倒し</b>、
    /// 墓守の層（リィカの <c>AtkBonus</c>）を +4〜+14 押し上げるが、
    /// <b>その層は勝率にならない</b>。B 案は既存7行の平均で +0.7pt・情報セルの合計が 17 → 17 で不変、
    /// 試験行2本が +26.1 / +13.7pt。</para>
    /// </summary>
    public static BlazeRule Default => new(BlazeTargets.Both);

    /// <summary>味方の巻き込みに火を乗せるか。</summary>
    public bool Allies => Targets is BlazeTargets.AllyOnly or BlazeTargets.Both;

    /// <summary>敵側の巻き込みに火を乗せるか。</summary>
    public bool Foes => Targets is BlazeTargets.Both or BlazeTargets.FoeOnly;
}

/// <summary>自爆。決まったターンに自壊し、敵全体を巻き込む。死ぬタイミングが読める。</summary>
public sealed class BomberTrait : Trait
{
    public const int EnemyBlast = 14;
    public const int AllyBlast = 12;

    public override TraitId Id => TraitId.Bomber;

    // 固定ターンではなく「倒れたとき」爆発する。
    // どれだけ早く死なせるかがプレイヤーの操作対象になり、
    // 巻き込みや生贄で自陣から起爆できるようになる。
    public override void OnDeath(BattleContext ctx, UnitState self)
    {
        ctx.Log($"    {self.Name} が破裂した", LogKind.Highlight);

        // 火種（第59期）。**engine に新しい規則は足していない**——既存のループの中で
        // `ctx.Ignite` を呼ぶだけ。`Ignite` は乱数を引かないので乱数列は動かない。
        // 巻き込みで倒れた相手には点かない（`Ignite` が `IsAlive` で弾く）——意図どおりで、
        // 「破裂で死ななかった者が燃える」が火種の形になる。
        BlazeRule blaze = ctx.Blaze;

        foreach (UnitState foe in ctx.LivingMembersShuffled(ctx.Opponent(self.TeamId)))
        {
            ctx.ApplyDamage(foe, EnemyBlast, self);
            if (blaze.Foes) ctx.Ignite(foe, source: self);
        }

        // 味方も巻き込む。これが他の駒の起点になる。
        foreach (UnitState ally in ctx.LivingMembersShuffled(self.TeamId))
        {
            if (ally == self) continue;
            ctx.ApplyDamage(ally, AllyBlast, self, isFriendlyFire: true);
            if (blaze.Allies) ctx.Ignite(ally, friendly: true, source: self);
        }
    }
}

/// <summary>継ぎ接ぎ。倒れた味方を戻す。回数制限が無いと確実に壊れるので必ず持たせる。</summary>
public sealed class ReviverTrait : Trait
{
    public const int MaxCharges = 2;
    public const int ReviveHpPercent = 40;

    public override TraitId Id => TraitId.Reviver;

    // 代償は「蘇る側」ではなく「蘇生する側」が払う。
    // 蘇生される駒が弱体化するだけでは、編成にとってのコストにならない。
    public override void OnAllyDeath(BattleContext ctx, UnitState self, UnitState dead)
    {
        if (!self.IsAlive) return;
        if (dead.HasTrait(TraitId.Ephemeral)) return;
        if (self.Counter("charges") >= MaxCharges) return;

        // 同じ駒は二度は縫えない。
        // これが無いと、蘇生は「一度きりの効果を持つ駒」の価値を無制限に掛け算してしまい、
        // 死亡時効果を持つ駒すべてにとって唯一の正解になる。
        if (dead.Counter("sewn") > 0) return;
        dead.SetCounter("sewn", 1);

        self.SetCounter("charges", self.Counter("charges") + 1);
        ctx.Revive(dead, dead.MaxHp * ReviveHpPercent / 100);

        self.MaxHp = Math.Max(1, self.MaxHp / 2);
        self.Hp = Math.Min(self.Hp, self.MaxHp);
        ctx.Log($"    {self.Name} は自分を削って縫った（最大HP {self.MaxHp}）", LogKind.FriendlyFire);
    }
}

/// <summary>儚い。召喚された駒であることを示すだけのフラグ。</summary>
public sealed class EphemeralTrait : Trait
{
    public override TraitId Id => TraitId.Ephemeral;
}

/// <summary>
/// 毒撃。殴られると、殴ってきた相手に毒を積む。毒袋が破れる形。
///
/// もとは「攻撃した相手に毒を積む」だったが、それだと瘴気（グザ）の下位互換にしかならなかった。
/// グザは毎ターン敵全体に +2 を撒き、澱み（ミオ）は「毒が積まれた敵の数」に比例して増やすので、
/// 広さはグザが押さえている。スィドが1体に +3 を足しても、第三波の総層数33のうち3しか出ていなかった。
/// 敵の両隣へ拡散させる案も測ったが、グザがいる限り広さは飽和していて動かない（第3波 2% → 4%）。
///
/// 重複しない役割は「濃さ」と「体」しか残っていない。被弾を層に変えると両方が同時に成立する。
/// 殴られ続けるほど殴ってきた敵1体に層が集まり、疫み（ラウ）がその死体から撒き直す。
/// 硬くて一撃が軽い波（第四波）では、耐えている時間がそのまま収入になるので、
/// 長期戦が毒軸にとって不利ではなく有利に反転する。実際 第四波は 0% → 84% で、
/// 決着は5ターン全滅から7ターン勝利に変わった。
///
/// 隣接味方への漏れは残してある。ここが澱み喰い（ヴィオ）の燃料になる。
/// 配置探索でも、澱み喰いはスィドを中衛に置いて漏れをわざとミオに当てにいく形が最良だった。
/// </summary>
public sealed class VenomTrait : Trait
{
    public const int StackPerHit = 4;

    public override TraitId Id => TraitId.Venom;

    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        if (source is null || source.TeamId == self.TeamId) return;
        if (!source.IsAlive) return;

        // 毒の窓口（第90期）。滲み則の入口だけを担い、加算量もログも現行のまま。
        ctx.Poison(source, StackPerHit, self, PoisonRoute.Venom);
        ctx.Log($"    {source.Name} の毒が {source.Counter(StatusKeys.Poison)} 層になった", LogKind.Status);

        // 扱いが雑なので隣の味方にもかかる。漏れは前後を含む隣接（味方に及ぶものの定義）。
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self || !FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;
            ctx.Poison(ally, 1, self, PoisonRoute.VenomLeak);
            ctx.Log($"    {ally.Name} にも毒がかかった", LogKind.FriendlyFire);
        }
    }
}

/// <summary>
/// 自分からは攻撃しない。反撃役に持たせて「殴られなければ無価値」を成立させる。
///
/// <b>止めているのは攻撃だけ。</b> 溜めも術も通す。名前のとおり「不動」ではなく
/// 「自分からは決して攻撃しない」がこの特性の意味で、手番そのものを奪ってはいない。
/// 分解する前は <c>CanAct</c> が種別を持たず、攻撃を断ることと手番を失うことが
/// 同じ false に潰れていたので、不動の駒は能動的な関与経路を1つも持てなかった。
/// </summary>
public sealed class ImmobileTrait : Trait
{
    public override TraitId Id => TraitId.Immobile;

    public override bool CanAct(BattleContext ctx, UnitState self, ActionKind kind)
        => kind != ActionKind.Attack;

    // 最初から振らない型なので、差し出したターンとして数えない
    public override bool SurrendersTurn => false;
}

/// <summary>棘。受けたダメージの一部を殴り返す。反撃で反撃が起きない制御は engine 側。</summary>
/// <summary>
/// 棘が刺し返した相手に傷を残すか（第84期）。<b>診断（thorn）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。static のノブにしない理由は <see cref="ColossusRule"/> と同じ
/// （Trait は共有シングルトンで <c>layout</c> は並列実行する）。
///
/// <para><b>なぜ棘か。</b> 傷を書く経路は裂き（キリ）と刻み（ノミ）の2枚で、どちらも
/// <c>OnAfterAttack</c> ＝ 自分の手番の主目標にしか刻まない。棘の反撃は<b>敵の手番に走る</b>
/// （棘守りで隣の味方が殴られたときにも走る）ので、傷の供給が初めて被弾のクロックに乗る。
/// カドは不動で自分からは決して攻撃しないので、<b>手番を1つも使わない傷の供給</b>になる。</para>
///
/// <para><b>数は定数 1</b>（<see cref="RendTrait.Wounds"/> と同じ作法）。打点に比例させない
/// ——軛（第四波）が反撃の打点を切っても供給は 1 のまま残る。<b>主目標のみ</b>で、
/// 薙ぎで巻き込んだ隣の敵には書かない（範囲持ちが供給を独占して非線形に伸びるのを構造的に避ける）。</para>
/// </summary>
public enum ThornWound
{
    /// <summary>V0（対照）。現行。傷を書かない。</summary>
    None,
    /// <summary>V1（本命）。刺し返した相手（<c>source</c>）に傷 1。</summary>
    Foe,
    /// <summary>
    /// V2（自己検査）。V1 ＋ <b>巻き込んだ味方</b>にも傷 1。味方の傷を読む窓口はロスターに1本も無いので
    /// 盤面は V1 と1セルも違わないはず（<c>thorn check</c> (c)）。**採用しない。**
    /// </summary>
    Both
}

/// <summary>棘の傷（第84期）。<see cref="ThornWound"/> の doc を参照。</summary>
public readonly record struct ThornRule(ThornWound Wound)
{
    /// <summary>既定は V0（書かない）＝現行。</summary>
    public static ThornRule Default => new(ThornWound.None);
}

/// <summary>
/// 縫い（<see cref="SutureTrait"/>）が糸を引く先（第85期）。
/// <para><b>1文は2文にならない。</b> 読む対象が2つに増えるのではなく、1つの選び方の候補が広がるだけ
/// ——「殴った相手か、傷がいちばん深い味方か、深いほうの傷口から糸を引く」。
/// 選べない（深いほうが自動）のが代金で、カドの隣では味方側が深くなり、敵の傷を塞げなくなる。</para>
/// <para><b>標的選択（<see cref="SeverTrait.Prefers"/>）は触らない。</b> ハリは今までどおり傷が最も深い敵を狙う。
/// 狙う先と糸を引く先がずれるのは意図した非対称。</para>
/// </summary>
public enum SutureSide
{
    /// <summary>W0（対照）。第87期までの現行。殴った相手の傷だけを読む。</summary>
    Foe,
    /// <summary>W1・W2。<b>第88期からの既定。</b> 殴った相手と、傷がいちばん深い味方（自分を除く）のうち深いほう。同数なら敵側。</summary>
    Both
}

/// <summary>縫いの糸口（第85期）。<see cref="SutureSide"/> の doc を参照。</summary>
public readonly record struct SutureRule(SutureSide Side)
{
    /// <summary>既定は W2（両側）＝**第88期に採用**（第85期は当時の線 +3.0pt で落ちていた）。</summary>
    public static SutureRule Default => new(SutureSide.Both);
}

/// <summary>
/// 巻き込み則の書き手の絞り（第86期・X2）。<see cref="SpillWoundRule"/> の doc を参照。
/// <para><b>「絞ると上がるか」を見る段</b>であって X1 の劣化版ではない——密度の低い書き手
/// （生贄＝開戦1回／破裂＝死亡時／置き去り＝自分より遅い味方だけ）が<b>代金だけ払っている</b>なら
/// こちらが上に出る（第85期の申し送り。第67期「長い周期の機構は短い窓の波で無価値」の供給側）。</para>
/// </summary>
public enum SpillScope
{
    /// <summary>X1。<c>isFriendlyFire</c> かつ <c>source</c> が同陣営の刃をすべて拾う（第85期の W2 と同一）。</summary>
    All,
    /// <summary>X2。<b>吸い（ゴルム）と余波（ボルグ）だけ</b>——毎ターン全味方／攻撃ごと隣接の、密度の高い2枚。</summary>
    Dense
}

/// <summary>
/// 巻き込み則（第85期・W2。<b>第88期に採用して既定になった</b>）。<c>ApplyDamage</c> で <b>味方の刃</b>（<c>isFriendlyFire</c> かつ
/// <c>source</c> が同陣営）のダメージが通り、対象が生きていれば傷を 1 つ書く。
/// <para><b>書き手は6枚</b>——余波（ボルグ）／生贄（リィカ）／吸い（ゴルム）／破裂の味方巻き込み（ゾト）／
/// 棘の巻き込み（カド）／置き去りの削り（ナラ）。<b>中継（巨躯・分かち・転嫁の代金・深追いの反動）は書かない</b>
/// ——転嫁・深追いの <c>source</c> は <c>null</c> なので「<c>source</c> が同陣営」で外れる。
/// 巨躯・分かちの中継は<b>元の刃が味方なら <c>source</c> も同陣営になる</b>ので、中継の段に札（<c>relayed</c>）を付けて外す
/// （中継は肩代わりであって刃ではない。実測でこの札が無いと、カドの巻き込みが巨躯に中継された段が二重に数えられた）。</para>
/// <para>数は定数 1（打点に比例させない）。燃焼の刻み（<c>burnTick</c>）には書かない。</para>
/// </summary>
public readonly record struct SpillWoundRule(bool Enabled, SpillScope Scope = SpillScope.All)
{
    /// <summary>
    /// 既定は<b>有効</b>＝**第88期に採用**（第85期は当時の線 +3.0pt で落ちていた）。
    /// <b>味方に傷が載る経路はこれが初めて</b>——第49期の「傷は味方に載る経路が1つも無い」はここで終わる。
    /// </summary>
    public static SpillWoundRule Default => new(true);

    /// <summary><paramref name="source"/> の刃が書き手として採られるか（<see cref="Scope"/> の判定）。</summary>
    public bool Writes(UnitState source) =>
        Scope == SpillScope.All || source.HasTrait(TraitId.Drain) || source.HasTrait(TraitId.Splash);
}

/// <summary>
/// 傷の引き取り（第89期）。廃棄聖騎士ガルド（<see cref="GuardianTrait"/>）が、
/// <b>庇いが成立した被弾のたび、隣接する味方のうち傷がいちばん深い者から傷をひとつ自分へ移す。</b>
///
/// <para><b>浅く広い供給に深さを作る中継。</b> 第85期の採用で味方の傷は常設になったが、
/// 供給（巻き込み則の6枚）は<b>広く浅い</b>——第86期の実測「一回きりの供給は広く浅く、毎ターンの供給は狭く深い」。
/// 味方の傷を読む唯一の駒（縫いのハリ）は<b>深さを読む</b>（<c>PerWound</c> × 傷の数）ので、
/// 浅い在庫は使えない。源（6枚）→ <b>中継（ガルド）</b> → 終端（ハリ）の3段の真ん中を埋める。</para>
///
/// <para><b>ガルドがその置き場に向いている理由は3つとも構造的。</b>
/// (1) <c>Stoic</c> により1体を選ぶ回復を受け取れないので<b>ハリの繕い先には絶対にならない</b>
/// ——集めた傷を使うのは必ず他人。(2) ガルド自身は傷を1つも読まない（自己完結しない）。
/// (3) HP100 でロスター最高クラス——傷の貯蔵庫として落ちにくい。</para>
/// </summary>
public readonly record struct GatherRule(bool Enabled)
{
    /// <summary>
    /// 既定は<b>引き取る</b>＝**第90期 (P1) に採用**（第89期は「紙のスループット ≥ 5%」という
    /// <b>大きさの線を門に置いていた</b>ので 2×2 を1戦も回さずに落ちていた。第90期 §0-1 で門を
    /// 「鎖が繋がっているか」に置き換え、第88期の特異性の規約で測り直して通った）。
    /// </summary>
    public static GatherRule Default => new(true);
}

/// <summary>
/// 滲み則（第90期）。<b>傷を持つ相手には、状態異常が深く入る。</b>
/// 毒は層が +1、燃焼は残ターンが +1（3 → 4）される。
///
/// <para><b>両陣営に等しくかかる。</b> 第85期の巻き込み則が既定になっているので味方も傷を負う
/// ——傷を負った味方は瘴気の毒漏れ・火の粉を深く受ける。非対称なのは
/// 「こちらはその規則を知って編成を組める」という一点だけで、<b>マイナスを別に足さない。</b></para>
///
/// <para><b>単価の壁を構造的に回避するのが狙い。</b> 第84・86・89期は3回とも
/// 「傷1つ ＝ 3点」という<b>一撃で払い切る形</b>で落ちた。滲み則の払い出しは<b>層と残ターン</b>
/// ——毎ターン刻む通貨なので、1回の書き込みが残りターン数ぶん払い出す
/// （第87期＝ミオの着火が唯一その壁を越えて採用された形の一般化）。</para>
///
/// <para><b>足すのは定数 1。傷の数に比例させない</b>——比例させると「傷 N 個 × 係数」型に戻り、
/// 第84期の単価の壁に自分から入る。<b>傷は消費しない。</b>
/// <b>深さを持つ状態異常は毒と燃焼だけ</b>なので範囲はこの2つに閉じる
/// （痺れは二値・破片は味方に書くもの・標は深さを持たない）。
/// <b>ミオ（<see cref="AmplifierTrait"/>）は通さない</b>——増幅も着火も既に傷を読んでいる（第87期）ので二重取りになる。</para>
/// </summary>
/// <para><b>第91期に通貨ごとに分けた。</b> 燃焼は<b>非スタック</b>なので「量」が存在せず
/// （第57期）、深さを足しても<b>点け直しで消える</b>——第90期の実測で燃焼側の滲みは
/// 1.65 回/戦 発火して<b>紙の 2% しか払い出さない</b>のに、`compare` の 9 行を下げていた。
/// <b>深さを足す設計は毒・傷のような「積む通貨」にしか効かない。</b></para>
public readonly record struct SoakRule(bool Poison, bool Burn)
{
    /// <summary>
    /// 既定は<b>滲む</b>＝**第90期に採用**（主判定は A ＝ キリ・ノミ の両方で通り、
    /// どちらも 50 体中1位が意図した相手の瘴気袋のグザ。拒否権1〜3 もすべて ○）。
    /// <b>ただし理想61行では滲みの 100% が味方側に落ちる</b>——`compare` は 18 セル / 11 行が動き、
    /// うち `追撃×毒 (ハギ×グザ)` の第二波は 87.5 → 29.0（−58.5pt）。
    /// **主判定19行は1セルも動いていない**ので拒否権3 は構造的に立たなかった（第90期の報告書 §7）。
    /// <para><b>第91期に燃焼側を切った。</b> 燃焼の滲みは 1.65 回/戦 発火するのに<b>紙の 2% しか払い出さず</b>、
    /// それでも `compare` の 9 行を下げていた——**切ると 9 行とも第90期より前に完全に戻る**。
    /// **毒側だけを残しても主判定は A ＝ キリ・ノミ の両方で通り**（どちらも 50 体中1位が瘴気袋のグザ）、
    /// **拒否権は 61 行の分母（第91期の (G1)(G2)）でも立たない**——
    /// −10.0pt 以上落ちたのは `追撃×毒 (ハギ×グザ)` の 1 行だけで、
    /// **その5枚のどの駒も「他の行」の平均が −0.13 〜 +0.00pt** ＝ 組み合わせ固有の制約であって壊れではない。</para>
    /// </summary>
    public static SoakRule Default => new(Poison: true, Burn: false);
}

/// <summary>毒を書いた経路（第90期の計数。<b>盤面には一切影響しない</b>）。</summary>
public enum PoisonRoute
{
    /// <summary>瘴気（グザ・毎ターン敵全体）。</summary>
    Miasma,
    /// <summary>瘴気の味方漏れ（グザ・毎ターン味方全体）。</summary>
    MiasmaLeak,
    /// <summary>毒撃（スィド・被弾した相手へ）。</summary>
    Venom,
    /// <summary>毒撃の隣への漏れ（スィド・隣接味方）。</summary>
    VenomLeak,
    /// <summary>疫み（ラウ・死骸から敵全体へ）。</summary>
    Contagion
}

/// <summary>
/// 継ぎ当て（<see cref="MenderTrait"/>）が繕う相手の傷を読むか（第86期）。
/// <para><b>読み手を手番の要らない駒に替える。</b> 縫い（ハリ）の発火は「手番がある <b>かつ</b> 殴った相手に傷がある」の
/// 積だが、ノノの繕いは<b>手番そのもの</b>（<c>Actions = [Skill]</c>）なので条件が1つしかない
/// ——第85期の律速（ハリの振り 2.15 回/戦）がここで外れるかを測る。</para>
/// <para><b>代金は係数を足さずに済む。</b> ノノは繕った分だけ自分が減るので、
/// 傷が深いほど繕いが増え、<b>同じだけ自分が早く尽きる</b>。上限は現行の <c>Math.Min(amount, self.Hp - 1)</c> のまま。</para>
/// </summary>
public enum MendSide
{
    /// <summary>X0（対照）。現行。傷を読まない（繕いは常に <see cref="MenderTrait.Amount"/>）。</summary>
    Plain,
    /// <summary>X1・X2。患者の傷 1 つにつき <see cref="MenderTrait.PerWound"/> だけ繕いが増え、傷はひとつ塞がる。</summary>
    Wound
}

/// <summary>繕いの読み（第86期）。<see cref="MendSide"/> の doc を参照。</summary>
public readonly record struct MendRule(MendSide Side)
{
    /// <summary>既定は X0（読まない）＝現行。</summary>
    public static MendRule Default => new(MendSide.Plain);
}

/// <summary>
/// 傷口の着火（第87期）。<see cref="AmplifierTrait"/>（澱みのミオ）が、
/// <b>毒を持たないが傷を持つ敵</b>に毒を <see cref="AmplifierTrait.IgniteAmount"/> 層だけ置く。
///
/// <para><b>ミオは相変わらず自分では何も供給しない純粋な読み手のまま。</b>
/// 「毒が積まれていなければ完全に無意味」という条件が「毒か傷が無ければ無意味」に緩むだけで、
/// <b>1文も増えない</b>——傷という別軸の在庫を、毒という自分の在庫の入口として読む。</para>
///
/// <para><b>払い出しが発火に比例しない初めての機構。</b> 第84〜86期に落ちた3案
/// （棘 → 傷／縫いの両側読み／繕いの傷読み）は<b>持続係数が 1.0</b>——1回の発火で払い切って終わるので、
/// 稼働率（発火 ÷ 決着T）35〜40% の天井がそのまま出力の天井になっていた。
/// 毒は層が残って毎ターン刻むので、1回の着火の払い出しがその発火の回数に比例しない。</para>
///
/// <para><b>着火量は定数 1。傷の数に比例させない</b>——比例させると「傷 N 個 × 係数」型に戻り、
/// 第84期の単価の壁に自分から入る。<b>傷は消費しない</b>（着火は敵1体につき事実上1回しか走らない）ので、
/// 抉り・断ち・縫いと同じ傷を邪魔しない——<b>傷軸と毒軸を同居させるのがこの機構の目的</b>で、
/// ここで取り合いを作らない。<b>着火したターンは濃くしない</b>（刻みの「足す前に読む」と同じ理由。
/// 同じターンに +4 すると1発目から段が付く）。</para>
/// </summary>
public readonly record struct IgniteRule(bool Enabled)
{
    /// <summary>
    /// 既定は<b>着火する</b>＝**第89期 (P1) に採用**（第87期は当時の線 +3.0pt で、第88期は
    /// 「相乗（水準）の揺れ」から引いたノイズ床で落ちていた。**増分尺度のノイズ床**——同じ実験の中の
    /// 意図しない相手の \|Δ相乗\| の 95%tile ——を規約に固定し、**別標本**で測り直して通った）。
    /// </summary>
    public static IgniteRule Default => new(true);
}

public sealed class ThornsTrait : Trait
{
    public const int Multiplier = 2;
    public const int SplashPercent = 60;

    /// 味方への巻き込み量。基礎攻撃力（Def.Attack）に対する割合で、CurrentAttack を参照しない。
    /// ここを現在攻撃力にすると「上昇量が現在値に比例する」形になり、増分が増分を生む。
    /// 惨禍を自分で持っているぶん締まりが速く、隣接味方のHPという小さな有限プールに
    /// 指数を当てることになるので、何も起きないか2ターンで自壊するかの二極化になる。
    /// 味方への巻き込み量。基礎攻撃力（Def.Attack）に対する割合で、CurrentAttack を参照しない。
    /// ここを現在攻撃力にすると「上昇量が現在値に比例する」形になり、増分が増分を生む。
    /// 惨禍を自分で持っているぶん締まりが速く、隣接味方のHPという小さな有限プールに
    /// 指数を当てることになるので、何も起きないか2ターンで自壊するかの二極化になる。
    ///
    /// 対象の残りHP比に変える案は測って却下した。代金プールは確かに枯れるが、
    /// 3% で全滅・10% で第五波100%と崖が残り、傾斜にならない（詳細は README）。
    public const int FriendlySplashPercent = 50;

    /// 巻き込んだ実ダメージのうち、攻撃力に変わる割合の逆数。ムドの被弾強化（dmg/2）と揃えてある。
    public const int GainDivisor = 2;

    public override TraitId Id => TraitId.Thorns;

    // 反撃量を「受けたダメージ」ではなく「自分の攻撃力」で決める。
    // 被弾量参照だと、敵の火力が低いステージでは何も起きず、
    // 高いステージでは先に自分が死ぬ、という挟み撃ちから抜けられない。
    // 攻撃力参照にすると強化・弱体の対象になり、支援の効く駒になる。
    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        if (source is null || source.TeamId == self.TeamId) return;
        if (ctx.InReaction) return;   // 反撃の連鎖を止める

        // 痺れ・縛めの間は刺し返せない。棘もターン外行動なので、割り込み・追い打ちと同じ門をくぐる。
        //
        // ここを開けたままにすると、縛め（クグ）が不動のカドを縛って +16 を毎ターン払う形になる。
        // カドは自分のターンを持たないので痺れで失うものが何も無く、反撃もそのまま出るため、
        // 号令の無償収入とまったく同じ穴が、より大きい係数で開く。
        // 号令の SurrendersTurn をそのまま流用しないのは、追い打ち（ハギ）が縛められると
        // 実際に割り込めなくなる＝正当に支払っているため。除外側ではなく門の側で揃えるのが正しい。
        //
        // 副作用として敵の痺れがカドに効くようになるが、これは意図した効果。
        // 強力な反撃役に対する攻略の語彙になる。
        if (!ctx.CanActOutOfTurn(self)) return;

        int back = Math.Max(1, self.CurrentAttack * Multiplier);
        ctx.NoteAttackRead(self);   // 攻撃力を出力に変換した（第64期・死蔵の判定）

        // 反撃は範囲。自分から攻撃できず打点が自分しかない駒なので、
        // 見返りをここまで大きくして初めて軸として成立する。
        ctx.Reaction(() =>
        {
            ctx.Log($"    {self.Name} の棘が {source.Name} を刺し返す", LogKind.Trigger);
            ctx.ApplyDamage(source, back, self);

            // 棘の傷（第84期）。**裂きの作法をそのまま踏む**——定数 1・主目標のみ・死体には書かない
            // （ApplyDamage の後に生存を取り直す）。既定（ThornWound.None）ではこの行は素通りする。
            if (ctx.Thorn.Wound != ThornWound.None && source.IsAlive)
            {
                int w = source.Counter(StatusKeys.Wound) + 1;
                source.SetCounter(StatusKeys.Wound, w);
                ctx.Log($"    {self.Name} の棘が {source.Name} に残る（傷 {w}）", LogKind.Status);
            }

            foreach (UnitState other in ctx.LivingMembersShuffled(source.TeamId))
            {
                if (other == source) continue;
                // 敵に及ぶ範囲なので薙ぎと同じ表を引く。味方に及ぶものと定義を分けている。
                if (!FormationRules.SweepTargets(source.Slot).Contains(other.Slot)) continue;
                ctx.ApplyDamage(other, Math.Max(1, back * SplashPercent / 100), self);
            }

            // 棘は味方も巻き込み、巻き込んだぶんだけ据わる。
            // 上昇源を「味方に与えた実ダメージ」だけに限っているのは、隣の味方が全員倒れたあとも
            // 敵ダメージで無償に伸び続ける穴を作らないため（号令がカドに毎ターン +8 を
            // 無償で払っていたのと同じ形になる）。代金は常に隣接味方のHPで支払われる。
            // 味方に及ぶ範囲は隣接表（ボルグと同じ AreAdjacent）。敵に及ぶ範囲の薙ぎ表とは定義を分けている。
            int spill = Math.Max(1, self.Def.Attack * FriendlySplashPercent / 100);
            int gained = 0;

            foreach (UnitState ally in ctx.LivingMembersShuffled(self.TeamId))
            {
                if (ally == self) continue;
                if (!FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;

                int before = ally.Hp;
                ctx.Log($"    余波: {self.Name} の棘が {ally.Name} を巻き込む", LogKind.FriendlyFire);
                ctx.ApplyDamage(ally, spill, self, isFriendlyFire: true);
                gained += (before - ally.Hp) / GainDivisor;

                // V2（自己検査・第84期）。巻き込んだ味方にも傷 1。`gained` の計算には触らない。
                if (ctx.Thorn.Wound == ThornWound.Both && ally.IsAlive)
                {
                    int w = ally.Counter(StatusKeys.Wound) + 1;
                    ally.SetCounter(StatusKeys.Wound, w);
                    ctx.Log($"    {self.Name} の棘が {ally.Name} に残る（傷 {w}）", LogKind.Status);
                }
            }

            if (gained > 0)
            {
                self.AtkBonus += gained;
                ctx.Log($"    {self.Name} は巻き込むほど据わる（攻撃 +{gained} → {self.CurrentAttack}）", LogKind.Trigger);
            }
        });
    }
}

/// <summary>
/// 棘守り。<b>自分の「前」か「横」にいる味方への単体攻撃を身代わりし、その味方と位置を入れ替える。</b>
///
/// <para><b>なぜ足したか。</b> 棘（<see cref="ThornsTrait"/>）だけのカドは、盤面への関与が
/// 反撃という<b>単一の閾値挙動</b>しか無かった。敵の攻撃力が閾値を超えれば反撃で圧勝、
/// 超えなければ何も起きない——中間の勝率が構造的に存在しない（<see cref="CondemnTrait"/> の
/// コメントに記録がある）。係数をいじっても崖の位置が動くだけなので、<b>第2の関与経路</b>を
/// 与える。加えて、これは手番を消費する（<see cref="ActionKind.Skill"/>）ので、カドが
/// 「手番を1つも使わないまま号令・据えの収入だけ受け取る」という穴も構造的に閉じる。</para>
///
/// <para><b>却下した案 1: 棘を隣の味方に移植する。</b> 「隣接する味方1体に棘を分け与え、
/// その味方が殴られても刺し返す」という形。<b>トリガーが移るだけで盤面の状態が1つも変わらない</b>
/// ——誰がどこに立っているかも、誰のHPが何点かも動かず、反撃の出どころが変わるだけ。
/// 「捨てられた駒を噛み合わせる」ための情報がプレイヤーに1つも増えないので採らなかった。</para>
///
/// <para><b>却下した案 2: 構える（自己回復・被ダメ減）。</b> 手番を使って自分を硬くする形。
/// <b>自己完結していて隣に何が立っているかを見ない</b>のが第一の難点で、第二に
/// <b>被ダメ減はカドの経済そのものを壊す</b>——カドの攻撃力は「巻き込んだ味方のダメージ」と
/// 「自分が受けた傷」から生えるので、硬くすることは収入を絞ることと同じ。
/// 崖を潰すつもりで、崖の低い側だけを更に低くすることになる。</para>
///
/// <para><b>肩代わりは 100%（確率判定なし）。</b> 庇う（<see cref="GuardianTrait"/> 50%）と
/// 揃えなかったのは、<b>身代わりになった相手が結局ダメージを受けるのは形として悪い</b>から。
/// 「前に出た」のに半分の確率で後ろの味方が斬られるなら、それは身代わりではなく確率の抽選になる。
/// 代金は確率ではなく<b>巻き込み（下記）という別勘定</b>で受け取る。</para>
///
/// <para><b>意図した挙動なので直さないこと。</b> 入れ替えたあと両者は必ず隣接しているので、
/// 庇われた味方は<b>カドの反撃の巻き込みを必ず受ける</b>
/// （<see cref="ThornsTrait.FriendlySplashPercent"/>。実数で <c>11 × 50% = 5</c>、惨禍で 7）。
/// 敵の攻が低い波では、庇うことで盤面全体の被害がむしろ増える。<b>これは欠陥ではなく設計の中心。</b>
/// 失った味方HPの半分がカドの <c>AtkBonus</c> になり、反撃は攻撃力×2 で返るので、
/// 損ではなく「味方HP → カドの攻撃力 → 撃破」への変換になっている。敵の攻が高い波では本物の
/// 肩代わり、低い波では自傷変換装置——<b>同じスキルが波によって災厄と資産に化ける</b>（可変コスト型）。</para>
///
/// <para><b>配置が意味を持つ。</b> スロットごとに隣接数が違うので、カドをどこで止めたいかが
/// 編成の判断になる。前2（lane2）は隣接1で巻き込み最小だが、lane2 は奥行き1で貫きの直撃レーン。
/// 前0 / 中3 / 後4 / 後5 は隣接2、前1 は隣接3で代金が最大。前進の経路は
/// 後4 → 前0 ／ 後5 → 中3 → 前1 で、前列に着いたあとは 0⇔1⇔2 の横滑りになる。
/// 前2 へは横滑りでしか到達できない。<b>後退する経路は構造上存在しない</b>ので
/// 「後ろへ移動しない」を別途書く必要はない。</para>
/// </summary>
public sealed class ThornGuardTrait : Trait
{
    /// <summary>
    /// 肩代わりの上限。<b>カドが受け止めるのはここまでで、超過分は守った相手がそのまま受ける</b>
    /// （<see cref="BattleEngine.ApplyDamage"/> が中継する）。チーム全体の被ダメージ総量は
    /// 変わらず、<b>分配だけが変わる</b>。
    ///
    /// <para><b>なぜ割合ではなく定数か。</b> 肩代わり 100%・上限なしだと、<b>カドが払う額が
    /// 敵の攻撃力にそのまま比例する</b>。割合で切っても比例は残る（10 の 8 割も 24 の 8 割も
    /// 敵の攻に比例している）。<see cref="ThornsTrait"/> のコメントは、反撃を被弾量参照ではなく
    /// 自分の攻撃力参照にした理由を「敵の火力が低いステージでは何も起きず、高いステージでは先に
    /// 自分が死ぬ、という挟み撃ちから抜けられない」と書いているが、<b>出力側で避けたはずの
    /// 挟み撃ちを、コスト側から持ち込んでいた</b>——実測で、荷駄6（1体攻 7）では出力が上がり、
    /// 重装6（1体攻 12）では味方全滅% が 49.0% → 70.2% に増えている。<b>比例を定数に置き換える</b>
    /// のが狙いなので、ここは割合にしてはいけない。</para>
    ///
    /// <para><b>値は 8 で確定。</b> 敵の攻撃力分布は 荷駄7 / 重装12 / 槍騎17 / 狙撃18 / 勇者20 / 巡礼24 で、
    /// 8 は「荷駄だけをほぼ受け止め、重装以上には貫かれる」位置。
    ///
    /// <b>採った理由は当初の想定（符号の反転を消す）ではない。</b> 反転は 8 でも 16 でも消えなかった
    /// （README「肩代わりに上限を入れても、符号の反転は消えなかった」）。<c>life</c> の実測で、
    /// これが<b>カドの寿命レバーとして機能していた</b>ことが分かったので採る——第三・四波でカドの
    /// 寿命が伸び、<c>反撃改2</c> の第四波で 初落% 75.5 → 59.5、稼働率 0.43 → 0.51。
    /// 16 は参照台で中継が1回も起きず（＝実質存在しない）、残す理由が無い。</para>
    /// </summary>
    public const int AbsorbCap = 8;

    /// <summary>構えている印。スキルの手番に立て、1回の肩代わりで消費する。</summary>
    public const string PendingKey = "thornGuardPending";

    /// <summary>
    /// 肩代わりした相手のスロット。<b>+1 して格納し、0 を「なし」とする</b>
    /// （<see cref="UnitState.Counters"/> は int で、未設定と スロット0 が区別できないため）。
    /// </summary>
    public const string PartnerKey = "thornGuardPartner";

    public override TraitId Id => TraitId.ThornGuard;

    /// <summary>
    /// 守れる位置か。<b>横</b>＝同じ列の相方、<b>前</b>＝同じレーンの1つ手前。
    ///
    /// <see cref="FormationRules"/> の既存の関数だけで書く。<b>新しい幾何関数を足さない</b>
    /// ——盤面の形は1箇所（FormationRules）にしか無い、という前提を崩すと
    /// 「隣接とは何か」の定義が特性ごとに散る。
    ///
    /// <para>X字盤面での実際の守備範囲（旧盤面の定義をそのまま移したもの）:</para>
    /// <code>
    /// 前1のカド → 前3          中央のカド → 前1・前3
    /// 前3のカド → 前1          後1のカド → 後3・中央    後3のカド → 後1・中央
    /// </code>
    /// <para><see cref="FormationRules.IsLanePredecessor"/> は召喚枠を除いた経路で数えるので、
    /// ○中1 に胞子が湧いていても後1のカドの守備範囲は変わらない。</para>
    /// </summary>
    public static bool Covers(UnitState self, UnitState ally)
    {
        if (FormationRules.AreSameRowPair(self.Slot, ally.Slot)) return true;

        // 前だけ。同じレーンの後ろにいる味方は守らない（守れると後列から前列を
        // 素通しで守れることになり、隊列の意味が消える）。
        return FormationRules.IsLanePredecessor(ally.Slot, self.Slot);
    }

    /// <summary>
    /// 手番の <see cref="ActionKind.Skill"/> で構え直す。周期は1要素なので毎ターン。
    ///
    /// 消費されずに残った肩代わりの記録もここで捨てる。破片（アーマー）が
    /// 全額吸って <see cref="OnDamaged"/> まで届かなかった場合に記録が残り、
    /// 次の無関係な被弾で入れ替えが走るのを防ぐ（庇うの <c>guardPending</c> と同じ穴）。
    /// </summary>
    public override void OnAction(BattleContext ctx, UnitState self, UnitAction action)
    {
        self.SetCounter(PendingKey, 1);
        self.SetCounter(PartnerKey, 0);
    }

    /// <summary>
    /// 入れ替えはここで実行する。<b><see cref="BattleContext.SelectTarget"/> の中で
    /// <see cref="BattleContext.SwapSlots"/> を呼んではいけない。</b>
    /// SwapSlots は <see cref="Trait.OnMoved"/> を通知し、ヨミの
    /// <see cref="DisplacedTrait"/> が割り込み攻撃を起こす。標的選択の途中でこれが走ると、
    /// <b>攻撃が着弾する前に攻撃者や標的が死にうる</b>。
    ///
    /// <para>順序は「入れ替え → 反撃」。カドの <c>Traits</c> 配列で棘守りを棘より前に
    /// 置いてあるので（<see cref="UnitCatalog.Kado"/>）、ここが
    /// <see cref="ThornsTrait.OnDamaged"/> より先に走る。<c>ApplyDamage</c> の通知は
    /// <c>target.Traits</c> の順、<c>TraitCatalog.Resolve</c> は <c>Def.Traits</c> の順を
    /// そのまま保つ（Select().ToList()）ので、配列の順序が実行順になる。</para>
    ///
    /// <para>「前に出て身代わりになり、その位置から刺し返す」という絵が一貫し、
    /// 反撃の巻き込み対象が<b>移動後の隣接</b>になるので位置の意味が強くなる。</para>
    ///
    /// <para>再入は既存の <see cref="BattleContext.InInterrupt"/> に任せる。
    /// <b>新しい static フラグを作らないこと</b>——Trait は全戦闘で共有されるシングルトンで、
    /// layout モードの並列実行で別の戦闘同士が干渉する。</para>
    /// </summary>
    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        int stored = self.Counter(PartnerKey);
        if (stored == 0) return;
        self.SetCounter(PartnerKey, 0);

        // 庇ったその一撃で倒れたなら前に出る者がいない
        if (!self.IsAlive) return;

        int dest = stored - 1;
        UnitState? partner = ctx.PickOne(ctx.LivingMembers(self.TeamId)
            .Where(u => u != self && u.Slot == dest).ToList());

        // 入れ替え相手が既に死んでいる（巻き込み・毒で先に落ちた）ならそのまま。
        // 空席へ滑り込ませないのは、それが「誰も押しのけずに前へ出る」＝代金の無い前進になるため。
        if (partner is null) return;

        ctx.Log($"    {self.Name} は {partner.Name} を押しのけて前に出た", LogKind.Trigger);
        ctx.SwapSlots(self, dest);
    }

    /// <summary>
    /// 部隊戦の境界で印を消す。印は <see cref="StatusKeys"/> に無いので境界の一律掃除では
    /// 消えない（庇うの <c>guardPending</c> と同じ理由）。
    /// </summary>
    public override void OnCarryOver(UnitState self)
    {
        self.SetCounter(PendingKey, 0);
        self.SetCounter(PartnerKey, 0);
    }
}

/// <summary>
/// 囃し立て。隣接する味方1体に標的を付ける。以後、敵の攻撃はそいつに集中する。
/// 「被弾で強くなる」「殴られると殴り返す」駒は、これが無いと自分から被弾できない。
/// </summary>
public sealed class MarkerTrait : Trait
{
    public override TraitId Id => TraitId.Marker;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
    {
        // OrderByDescending は安定ソートなので、最大HP が同値だと元の並び＝席番号順に落ちる。
        // 同値の中は乱数で割る（鏡像の配置を同値にするため。PickOne 参照）。
        var adj = ctx.LivingMembers(self.TeamId)
            .Where(a => a != self && FormationRules.AreAdjacent(self.Slot, a.Slot)).ToList();
        int topHp = adj.Count == 0 ? 0 : adj.Max(a => a.MaxHp);
        UnitState? mark = ctx.PickOne(adj.Where(a => a.MaxHp == topHp).ToList());

        if (mark is null)
        {
            ctx.Log($"  {self.Name} は囃し立てる相手がいなかった", LogKind.Action);
            return;
        }

        mark.SetCounter(StatusKeys.Marked, 1);
        ctx.Log($"  {self.Name} が {mark.Name} を敵の前に押し出した", LogKind.Trigger);
    }
}

/// <summary>継ぎ当て。回復量と同じだけ自分が減る。等価交換なので無限には支えられない。
///
/// <para><b>傷を読む版（第86期・<see cref="MendRule"/>）。</b> 患者に傷があれば
/// 傷 1 つにつき <see cref="PerWound"/> だけ繕いが増え、<b>その傷はひとつ塞がる</b>
/// （塞ぎは <see cref="TraitId.Seal"/> の札で計量する。第74期の作法で、どこを塞ぐかは本体が決める
/// ——ハリは敵、ノノは患者）。<b>患者の選び方（<see cref="BattleContext.MostHurtAlly"/>）には傷を混ぜない</b>
/// ——「最も傷ついた味方を繕う」の1文を保つ。</para>
/// <para><b>代金は既存の式のまま。</b> 増えた繕いはそのまま自分の HP から出るので、
/// 深い傷を読むほど早く尽きる（可変コスト型）。上限も現行の <c>Math.Min(..., self.Hp - 1)</c> を使い回す。
/// <b>渇き（第三波）では <c>ctx.Heal</c> が 1 点も返さないのに <c>self.Hp -= amount</c> は無条件で走る</b>ので、
/// 傷を読むぶんだけ一方的に速く尽きる（<c>BattleEngine.Heal</c> のコメントに明記のある意図した挙動）。</para>
/// </summary>
public sealed class MenderTrait : Trait
{
    public const int Amount = 14;

    /// <summary>
    /// 傷1つあたりの上乗せ（第86期）。<b>加算</b>で、抉り（<see cref="GougeTrait.PerWound"/>）・
    /// 縫い（<see cref="SutureTrait.PerWound"/>）と同値。<b>新しい数字を作らない。</b>
    /// </summary>
    public const int PerWound = 3;

    public override TraitId Id => TraitId.Mender;

    // 手番の行動として撃つ（第11期 Phase BB）。ターン頭の無条件発火から移したので、
    // ノノ（速さ6）より速い味方が殴られた**後**に繕いが入る。痺れ・縛めで手番を
    // 失えばその周期の繕いも出ない。どちらも意図した仕様変更。
    public override void OnAction(BattleContext ctx, UnitState self, UnitAction action) => Mend(ctx, self);

    // 行動パターンを持たない保持者（敵の従軍司祭長）は従来どおりターン頭に繕う。
    // 理由は Trait.ActsOnPattern を参照。
    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (!ActsOnPattern(self)) Mend(ctx, self);
    }

    private static void Mend(BattleContext ctx, UnitState self)
    {
        if (!self.IsAlive || self.Hp <= 1) return;

        // 患者の選び方は施し・縫いと共有（BattleContext.MostHurtAlly。第39期に抽出）。
        // HP割合が同値なら席番号順に落ちていたので、同値の中は乱数で割る——その窓口もあちら。
        UnitState? patient = ctx.MostHurtAlly(self);
        if (patient is null) return;

        // 傷を読む（第86期・MendRule.Wound）。既定（Plain）では w が常に 0 に落ちるので、
        // 盤面も乱数列も文字列も1ビットも動かない（自己検査 (b) の根拠）。
        // **観測（wSeen）は版に依らず取る**——紙のスループット（§1-1）を X1 を1戦も回さずに出すため。
        // 計数専用で、盤面の判断には一切使わない。
        int wSeen = patient.Counter(StatusKeys.Wound);
        int w = ctx.Mend.Side == MendSide.Wound ? wSeen : 0;
        bool seal = w > 0 && self.HasTrait(TraitId.Seal);

        int amount = Math.Min(Amount + PerWound * w, self.Hp - 1);
        int before = patient.Hp;
        ctx.Heal(patient, amount);
        self.Hp -= amount;

        // 計数（第86期）。**盤面には一切影響しない。**
        UnitTally mt = ctx.TallyOf(self);
        mt.MendFires++;
        mt.MendWoundDepth += wSeen;
        if (wSeen > 0) mt.MendWoundSeen++;
        if (patient.Hp == before) mt.MendDry++;
        mt.MendHealed += patient.Hp - before;
        mt.MendPaid += amount;
        if (patient.TeamId != self.TeamId) mt.MendFoePatient++;   // 起きないはず（MostHurtAlly は同陣営のみ）

        ctx.Log($"    {self.Name} が自分を裂いて {patient.Name} を繕った（+{amount}）"
            + (w > 0 ? seal ? $"【傷 {w} → 傷 {w - 1}】" : $"【傷 {w}】" : ""), LogKind.Trigger);

        // 塞ぎ。**繕った相手**の傷を1つだけ引く（全部消すのは断ちの役）。
        // 渇き下でも走る（第39期・ハリの塞ぎと同じ作法。原因ではなく結果で解決しない）。
        if (seal) patient.SetCounter(StatusKeys.Wound, w - 1);
    }
}

/// <summary>
/// 施し。毎ターン、最も傷ついた味方を回復する。**自分は減らない。**
///
/// 継ぎ当て（<see cref="MenderTrait"/>）とは別物として足してある。継ぎ当ては等価交換
/// （14 回復して同量だけ自分が減る）なので、**保持者に与えた1ダメージ ＝ 敵が受け取れる回復が
/// 1減る ＝ 前列を1殴ったのと完全に等価**になる。得も損も無い。貫きは 25% 減衰するので、
/// 等価な取引を割引価格で行うぶんだけ正味の損になり、「支援役はレーンを選べば潰せる」という
/// 第二波の主題が測定で**逆向き**に出ていた（司祭長レーン固定で残存 2.96、狙撃手レーン固定で 3.31）。
///
/// 自消費を外すと、保持者の HP は「否定できる回復量」と切り離される。倒すまでに払う量が
/// HP で頭打ちになり、倒したあとに否定できる量は残りターン数に比例するので、初めて
/// 「早く潰すほど得」という閾値が立つ。**意図的な崖だが、鍵のある崖**——後列に届く手段
/// （貫き・全体・毒）を持つ編成にだけ開く。
///
/// 却下した案:
/// - **継ぎ当てのまま HP を下げる**: 効かない。否定できる回復量が一緒に減るだけで、
///   1ダメージ ＝ 回復1減、の線形性は変わらない
/// - **比率を 2:1 にする（14 回復 / 自消費 7）**: 足りない。HP プール全体を先に払わされるので
///   閾値が立たない（HP62 なら減衰後の貫き 18 で3ターン、否定できるのは 28。同じ3ターンで
///   狙撃手 HP38 を消せば「止められない攻撃」36 が消える）
/// - **チャージ持ちに差し替える**: 第二波は練習用の波なので、教える内容の手前に
///   溜めの読み合いを挟まない（<c>UnitCatalog.ArcherG</c> のコメントが明示的に禁じている）
/// </summary>
public sealed class AlmsTrait : Trait
{
    public const int Amount = 14;

    public override TraitId Id => TraitId.Alms;

    // 保持者（敵の施しの司祭長）は Actions を持たないのでターン頭に配るだけでよく、
    // MenderTrait のような ActsOnPattern の分岐は要らない。
    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (!self.IsAlive) return;

        // 患者の選び方は継ぎ当て・縫いと共有（BattleContext.MostHurtAlly。第39期に抽出）。
        // **止まる条件だけが違う**——繕いは自消費のぶん Hp <= 1 で止まるが、施しには要らない。
        // 止まる条件は呼び出し側に残し、選択そのものだけを1箇所に集めてある。
        UnitState? patient = ctx.MostHurtAlly(self);
        if (patient is null) return;

        ctx.Heal(patient, Amount);
        ctx.Log($"    {self.Name} が {patient.Name} に施しを与えた（+{Amount}）", LogKind.Trigger);
    }
}

/// <summary>
/// 曝き（第40期）。保持者が攻撃を1回終えるたびに、<b>敵陣の後列でいちばん無傷な駒を、
/// 前列でいちばん傷ついた枠へ引きずり出す</b>（<see cref="BattleContext.SwapSlots"/> 1回）。
///
/// <para><b>ロスターで初めて「敵から味方へ状態を書く」経路になる。</b> それまで敵側から
/// 味方へ届くのは断罪（<see cref="CondemnTrait"/>・反撃してきた相手を痺れさせる）1本だけで、
/// 味方側には入力を資産に変える読み手（被弾強化・逆しま・澱み喰い・軋み・責め苦・移り木）が
/// 揃っているのに供給源が無かった。撒くものに移動を選んだのは、直接の読み手が最も多く
/// （後衛特化・軋み・移り木の3枚）、かつ<b>同じ1つの規則で駒の符号が反転する</b>ため。</para>
///
/// <para><b>盤面ルール（逆位・渇き・軛・粛）ではない。</b> あちらは両陣営に等しくかかるが、
/// 曝きは敵陣の駒だけを動かす一方向の効果なので、殉教・断罪・施しと同じ
/// 「敵側の語彙のプラス特性」として扱う。判定も engine ではなくここに置く。</para>
///
/// <para><b>発火点は <see cref="OnAfterAttack"/>。</b> ターン頭の無条件発火にしないのは、
/// 保持者の手番に紐づけると「保持者を早く割れば止まる」という勾配が自己言及的に立つから
/// （渇き・軛・粛と同じ狙い）。攻撃はそのまま行うので、<b>同数値の対照に対する差分が
/// 特性1つだけに閉じる</b>——巡礼騎士（Knight2）に戻せば盤面は完全に元へ戻る。</para>
///
/// <para><b>選び方は決定的にする。乱数で選ばない。</b> プレイヤーが「後列でいちばん無傷な駒が
/// 出される」と読めることが、この機構の価値の半分を占める。同値が並んだときだけ
/// <see cref="BattleContext.PickOne"/>（席バイアスを作らないための既存の窓口）に従う。</para>
///
/// <para><b><see cref="BattleContext.SwapSlots"/> には手を入れていない。</b> 移動の通知
/// （<see cref="Trait.OnMoved"/> / <see cref="Trait.OnAllyMoved"/>）・<c>HasFallenBack</c> の記録・
/// <c>Move</c> イベントの発行は既にあちらで正しく行われている。SwapSlots はチーム非依存
/// （占有者を <c>self.TeamId</c> で引く）なので、敵の特性から味方側の駒に対して呼んでよい。</para>
///
/// <para><b>入れ替えであって一方向の移動ではない。</b> 引き出された駒が前へ来る代わりに、
/// 前列の駒が後ろへ下がる。<b>後列は空かない。</b>下がった駒に <c>HasFallenBack</c> が立つのが
/// 符号反転の片側で、後衛特化（<see cref="SniperTrait"/>）はここで無償に起動する。</para>
///
/// <para><b>召喚枠を含める。</b> <c>Row.Back</c> には ○後2（スロット8）も入る。実態があるなら
/// 盤面の一部として扱う、という既存の判断（貫きのレーン経路・巨躯の被覆）に揃えた。</para>
///
/// <para>強度は <see cref="ExposeRule"/> で外から差す（既定は無効）。書き換え可能な static の
/// ノブは置かない——Trait は共有シングルトンで layout は戦闘を並列実行する
/// （<see cref="ColossusRule"/> / <see cref="YokeRule"/> / <see cref="HushRule"/> /
/// <see cref="MartyrRule"/> と同じ判断）。回数は保持者ではなく<b>戦闘単位</b>で数えるので、
/// 残数は <see cref="BattleContext"/> 側が持つ（保持者が複数いても合算される）。</para>
/// </summary>
public sealed class ExposeTrait : Trait
{
    public override TraitId Id => TraitId.Expose;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        // 既定（MaxPerBattle = 0）では走査を1回も走らせない。
        // layout は数百万戦を並列で回すので、軛の Cap 判定と同じ作法で先に落とす。
        if (ctx.ExposesLeft <= 0) return;

        // 選び方は突き返し（第41期・ShoveTrait）と共有（BattleContext.HaulOutPair の1箇所）。
        // **止まる条件（上限）と空振りの計数だけが呼び出し側に残る**——第39期の
        // MostHurtAlly と同じ扱いで、選択そのものの挙動は1バイトも変えていない。
        if (ctx.HaulOutPair(ctx.Opponent(self.TeamId)) is not { } pair) { ctx.ExposeMissed++; return; }

        ctx.ExposeCount++;
        ctx.Log($"    {self.Name} が {pair.Victim.Name} を {pair.Seat.Name} の前へ引きずり出した",
                LogKind.Trigger);
        ctx.SwapSlots(pair.Victim, pair.Seat.Slot);
    }
}

/// <summary>
/// 曝きの強度。<b>診断（expose）が版を差し替えるためだけの窓口</b>で、
/// 既定（<see cref="Default"/>）は<b>無効</b>。
///
/// <para><paramref name="MaxPerBattle"/> は1戦あたりの引きずり出しの上限回数で、
/// <c>0</c> なら完全に無効（<see cref="ExposeTrait"/> の走査が1回も走らない）。
/// <b>回数は保持者ではなく戦闘単位で数える</b>ので、保持者が複数いても合算される。</para>
///
/// <para><b>このノブは盤面の総HPを1も動かさない</b>（駒の席を入れ替えるだけで
/// HP も攻撃力も1点も変わらない）ので、掃引しても対照は1本で足りる——第34期の
/// 殉教者のHP掃引が「介入が効いた」と「ただ硬くなった」を分けるのに各点の対照を
/// 要したのとは、そこが違う。</para>
///
/// <para><b>採用値は 3</b>（第40期。0 / 1 / 2 / 3 / 無制限 の5点で掃引した）。
/// 3 と無制限は第五波の平均で 0.2pt しか違わない（40.5 対 40.3）——**曝きの実測は
/// 2.05 回/戦**なので、上限 3 が縛るのは長引いた戦闘だけ。それでも 3 を採ったのは、
/// <b>第五波が固有の勝者を持つのが 3 以上でだけ</b>だから（上限 1・2 では 0 行、
/// 3 以上で `突き出し (セロ×ヨミ)` の1行。CLAUDE.md「ここが空の波は独立した波として
/// 存在していない」）。上限 1 の方が平均は高い（42.3）が、そこは歯止め（40.0）を
/// 割らない限り調整目標ではない。</para>
/// </summary>
public readonly record struct ExposeRule(int MaxPerBattle)
{
    /// <summary>採用値。1戦あたり最大3回まで引きずり出す（第40期）。</summary>
    public static ExposeRule Default => new(3);
}

/// <summary>
/// 誹り（第44期）。<b>攻撃した相手の攻撃力を下げる。</b>口先で腕を鈍らせる。
///
/// <para><b>敵側の語彙のプラス特性。</b> 殉教・断罪・施し・曝きと同じ場所に置く
/// ——盤面ルール（逆位・渇き・軛・粛）のように両陣営へ等しくかかる規則ではなく、
/// <b>保持者から相手陣営への一方向の効果</b>だから。engine 側の判定はゼロ。</para>
///
/// <para><b>狙いは弱体軸への「敵側からの供給」。</b> 第40期に同じことを検討して見送っている
/// （見送り理由＝読み手がウツ1枚しかなく、新しく生まれる行が1〜2行で天井になる）。
/// その条件は満たされた——読み手は<b>3枚</b>（逆しま＝攻撃力／引き受け＝アーマー／
/// 渡し＝敵へ転嫁）になり、弱体を持つ行は 3行 → <b>7行</b>、そして第42期に共通窓口
/// <see cref="BattleContext.Dull"/> が立った。</para>
///
/// <para><b>発火点は <see cref="Trait.OnAfterAttack"/>。</b> ターン頭の無条件発火にしない理由は
/// 第40期の曝きと同じ2つ——(1) 保持者の手番に紐づくので<b>保持者を早く割れば止まる</b>という
/// 勾配が自己言及的に立つ。(2) 攻撃はそのまま行うので、<b>同数値の対照に対する差分が
/// 特性1つに閉じる</b>。</para>
///
/// <para><b><see cref="BattleContext.Dull"/> を必ず通す。</b> <c>AtkBonus</c> を直に引くと
/// <b>この期の設計目的が丸ごと消える</b>——窓口を通ることで、ウケの横取り（アーマー化）と
/// ワタの横取り（敵への転嫁）が自動的に走る。<b>敵が撒いた弱体を味方が資産に変換する経路</b>が
/// この期の核心で、それは窓口の中にしか無い。</para>
///
/// <para><b><c>AcceptsSupport</c>（ガルドの <c>Stoic</c>）は見ない。</b> 第42期の窓口は
/// 判定を呼び出し側に残す設計なので、<b>誹りは判定せずに <c>Dull</c> を呼ぶ</b>（＝ガルドにも通る）。
/// 第43期までの5経路と扱いが揃わないが、揃えると既存48行が動くので、この期では揃えない
/// （第42期からの持ち越し）。</para>
/// </summary>
public sealed class SlanderTrait : Trait
{
    public override TraitId Id => TraitId.Slander;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        // 既定（Penalty = 0）では窓口を1回も叩かない。Dull の中は横取りの走査があるので、
        // 曝きの ExposesLeft・軛の Cap 判定と同じ作法で先に落とす（layout は数百万戦を並列で回す）。
        int penalty = ctx.Slander.Penalty;
        if (penalty <= 0) return;
        if (!target.IsAlive) return;

        ctx.SlanderFired++;
        ctx.SlanderTotal += penalty;
        ctx.SlanderTo[target.Name] =
            ctx.SlanderTo.TryGetValue(target.Name, out int prev) ? prev + penalty : penalty;

        ctx.Log($"    {self.Name} の誹りが {target.Name} の腕を鈍らせた（攻撃 -{penalty}）", LogKind.Trigger);
        ctx.Dull(target, penalty, DullRoute.Slander);
    }
}

/// <summary>
/// 誹りの強度。<b>診断（slander）が版を差し替えるためだけの窓口</b>で、
/// 既定（<see cref="Default"/>）は<b>無効</b>。static のノブにしない理由は同型の doc を参照。
///
/// <para><c>Penalty</c> は攻撃するたびに対象から引く攻撃力で、<c>0</c> なら完全に無効
/// （<see cref="SlanderTrait"/> が窓口を1回も叩かない）。<b>既定を無効にしておくことで、
/// 保持者を波に置いた状態で <c>compare</c> が現行の <c>docs/balance.md</c> と完全一致する</b>
/// ——それが「差分が規則だけに閉じている」証明になる。</para>
/// </summary>
public readonly record struct SlanderRule(int Penalty)
{
    /// <summary>既定は<b>無効</b>（第44期の探索段階）。</summary>
    public static SlanderRule Default => new(0);
}

/// <summary>
/// 突き返し（第41期）。<b>味方が動かされるたび、敵陣の隊列を突き崩す。ただし勢い余って
/// 隣接する味方の体勢まで崩す（攻撃力が下がる）。</b>
///
/// <para><b>プラスとマイナスが1つの動作の表と裏</b>——置き去り・責め苦・仇討ち・裂き・
/// 抉り・断ち・縫いと同じ形なので、<see cref="TraitId"/> のどちらのブロックにも入らない。</para>
///
/// <para><b>狙いは第40期が作った余剰の回収。</b> 曝き（<see cref="ExposeTrait"/>）は移動の供給を
/// 大きく増やしたが（`HasFallenBack` 0.26 → 2.15 回/戦）、恩恵を受けたのは 44 行中 3 行だけで、
/// しかもその3行はヨミとガルドを共有する実質1クラスタだった。<b>ヨミに依存しない移動の
/// 読み手</b>を1枚足して、移動を<b>弱体化</b>という別の通貨に変換する。</para>
///
/// <para><b>弱体化を選んだのは供給が枯れているから。</b> <c>AtkBonus</c> を負にする経路は
/// ロスターに3つしかない——呪詛の味方漏れ（<see cref="CurseTrait.AllyLeak"/>・開戦時1回）／
/// 萎縮（<see cref="CowerTrait.AttackPenalty"/>・開戦時1回）／分かちの「腕がなまる」
/// （<see cref="SharerTrait.DullDivisor"/>・被弾のたび）。読み手は逆しま
/// （<see cref="PerverseTrait"/>）1枚きりで変換係数は3倍と大きいのに、
/// <b>現行の 44 行にドハとウツが同席する行が1つも無い</b>ので、
/// ウツの攻撃力は表の上では戦闘を通じて定数になっている。ここを開ける。</para>
///
/// <para><b>自分から移動を起こす手段は持たせない。</b> 供給が無ければ1回も発火しないこと
/// 自体がこの駒のマイナス側の一部で、負の特性を自己完結させないための措置。供給元は4つ——
/// 喧噪（<see cref="ShufflerTrait"/>）／臆病（<see cref="CowardTrait"/>）／
/// 棘守り（<see cref="ThornGuardTrait"/>）／<b>敵の曝き</b>（第40期・第五波の告発人）。</para>
///
/// <para><b>1ターン1回まで。</b> <see cref="BattleContext.SwapSlots"/> は2体を動かすので
/// 喧噪だけで <see cref="Trait.OnAllyMoved"/> が毎ターン2回走る。切らないと供給過多になる。
/// 上限はターン境界でリセットせず、<b>「最後に突き返したターン」を持つ</b>ことで表す
/// （据えの <see cref="StatusKeys.IdleTurn"/> と同じ作法。0 を「まだ一度も」に使うため +1 して入れる）。</para>
///
/// <para><b>効果Aを先に、効果Bを後に。</b> 効果Aは敵陣の駒を動かすので <see cref="Trait.OnAllyMoved"/> は
/// 敵側にしか通知されず、味方の突き返しには戻ってこない（この時点では再帰しない）。ただし
/// <b>将来敵側に突き返しを持たせると無限再帰する</b>ので、1ターン1回の上限だけに頼らず
/// <see cref="BattleContext.Shoving"/> の再入ガードを通す。</para>
///
/// <para><b>効果Bが隣接<i>全員</i>なのは、隣接次数をそのまま値段にするため。</b>
/// 対象を1体に絞ると「誰を隣に置くか」の判断が1回で終わる。全員にすると角（次数2）と
/// 中央（次数4）で代金が倍違い、X字の隣接表が不規則なのでそのまま配置パズルになる。</para>
///
/// <para><b>ガルド（<see cref="StoicTrait"/>）は構造的に唯一の非被害者になる。</b>
/// <c>AcceptsSupport</c> が偽なので効果Bを1点も払わない。<b>隣へ流さない</b>
/// （呪詛・萎縮が <see cref="BattleContext.SupportTargets"/> を通すのとはここが違う）——
/// 「ハネの隣をガルドで固める」が正当な配置解になる、という既存駒への payoff は潰さない。</para>
///
/// <para><b>弱体化のイベントは作らない。</b> <c>AtkBonus</c> は共通窓口を持たず、15箇所が
/// 直接フィールドを叩いている。窓口を作るなら既存15箇所の監査を伴う独立した作業になる
/// （第41期 §8）。効果Bもここで直に引く。</para>
///
/// <para>強度は <see cref="ShoveRule"/> で外から差す。書き換え可能な static のノブは置かない
/// ——Trait は共有シングルトンで <c>layout</c> は戦闘を並列実行する
/// （<see cref="ColossusRule"/> / <see cref="YokeRule"/> / <see cref="HushRule"/> /
/// <see cref="MartyrRule"/> / <see cref="ExposeRule"/> と同じ判断）。</para>
/// </summary>
public sealed class ShoveTrait : Trait
{
    /// <summary>最後に突き返したターン + 1。<c>0</c> は「まだ一度も」。</summary>
    public const string LastTurnKey = "shoveTurn";

    public override TraitId Id => TraitId.Shove;

    public override void OnMoved(BattleContext ctx, UnitState self, Row from, Row to) => Push(ctx, self);

    public override void OnAllyMoved(BattleContext ctx, UnitState self, UnitState moved) => Push(ctx, self);

    private static void Push(BattleContext ctx, UnitState self)
    {
        if (!self.IsAlive) return;

        // 再入ガードを先に見る。同じ発火の中から戻ってきた分を「上限で弾かれた」に
        // 数えると、空振りの列が再帰の回数で膨らむ。
        if (ctx.InShove) return;

        if (self.Counter(LastTurnKey) == ctx.Turn + 1) { ctx.ShoveCapped++; return; }

        ctx.Shoving(() =>
        {
            self.SetCounter(LastTurnKey, ctx.Turn + 1);
            ctx.ShoveFired++;
            ShoveOut(ctx, self);
            Stagger(ctx, self);
        });
    }

    /// <summary>
    /// 効果A —— 敵陣を突き崩す。<b>選び方は曝きとまったく同じ</b>
    /// （<see cref="BattleContext.HaulOutPair"/> の1箇所を共有する）。
    /// プレイヤーが規則を1つ覚えれば両方読めることが狙い。
    /// </summary>
    private static void ShoveOut(BattleContext ctx, UnitState self)
    {
        if (ctx.HaulOutPair(ctx.Opponent(self.TeamId)) is not { } pair)
        {
            // 後列か前列が 0 体。効果Aだけが空振りで、効果Bは実行する。
            ctx.ShoveNoRow++;
            return;
        }

        ctx.ShoveSwapped++;
        ctx.Log($"    {self.Name} の突き返しが {pair.Victim.Name} を {pair.Seat.Name} の前へ突き崩した",
                LogKind.Trigger);
        ctx.SwapSlots(pair.Victim, pair.Seat.Slot);
    }

    /// <summary>
    /// 効果B —— 隣接する生存味方全員の攻撃力を下げる。<b>席は動かさない</b>
    /// （移動を起こすのは効果Aだけ）。隣接は効果Aの<b>後</b>に取り直す
    /// ——効果Aが起こした敵側の割り込み（軋み）で味方が落ちていることがある。
    /// </summary>
    private static void Stagger(BattleContext ctx, UnitState self)
    {
        int penalty = ctx.Shove.Penalty;
        if (penalty <= 0) return;

        var hit = new List<string>();
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self) continue;
            if (!FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;

            // 支援拒否（ガルド）は弾く。**隣へ流さない**——呪詛・萎縮が SupportTargets を
            // 通すのとはここが違う。ガルドを隣に置けば代金を1点も払わない。
            if (!ally.AcceptsSupport) { ctx.ShoveBlocked++; continue; }

            ctx.Dull(ally, penalty, DullRoute.Shove);
            ctx.ShoveStaggered++;
            hit.Add(ally.Name);
        }

        if (hit.Count == 0) return;
        ctx.Log($"    勢い余って {string.Join("・", hit)} の体勢まで崩れた（攻撃 -{penalty}）",
                LogKind.FriendlyFire);
    }

    /// <summary>
    /// 部隊戦の境界で印を消す。印は <see cref="StatusKeys"/> に無いので境界の一律掃除では
    /// 消えない（棘守りの <c>PendingKey</c>・庇うの <c>guardPending</c> と同じ理由）。
    /// 残すと、次の部隊戦の同じターン番号で1回だけ突き返しが不発になる。
    /// </summary>
    public override void OnCarryOver(UnitState self) => self.SetCounter(LastTurnKey, 0);
}

/// <summary>
/// 突き返しの強度。<b>診断（shove）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。
///
/// <para><paramref name="Penalty"/> は効果Bで隣接味方から引く攻撃力。
/// <c>0</c> なら効果Bが完全に無効になる（効果Aは走る）——掃引の基準に置く陽性対照。</para>
///
/// <para><b>既定を無効にしなくてよい。</b> 曝きが<b>敵側</b>の駒だったのに対し、突き返しは
/// <b>味方側</b>の駒なので、<see cref="UnitCatalog.Hane"/> を編成に入れない限り
/// 既存 44 行は1バイトも動かない。それ自体が回帰チェックになる。</para>
///
/// <para><b>天井は戦闘長そのもの</b>（1ターン1回 × 戦闘長）。<c>AtkBonus</c> に下限は無く
/// 逆しまは <c>-b × 3</c> なので原理的には無限に伸びるが、<b>回数制限や上限値のような
/// 別のノブは足さない</b>——上限を数値で切ると「戦闘が長引くほど伸びる」という読める規則が
/// 但し書きで壊れる。会戦を跨がないことは <c>AtkBonus</c> をエンジンが境界で 0 にすることで
/// 既に保証されている（<c>Engagement.cs</c> の <c>CarryOver</c>）。</para>
/// </summary>
public readonly record struct ShoveRule(int Penalty)
{
    /// <summary>探索段階の初期値（第41期）。</summary>
    public static ShoveRule Default => new(2);
}

/// <summary>
/// 弱体の経路。<b>診断（<c>dull</c>）が経路別に数えるためだけの札</b>で、盤面には一切影響しない。
/// <see cref="BattleContext.Dull"/> を通る5経路に1対1で対応する。
/// </summary>
public enum DullRoute
{
    Other,        // 札を付け忘れた呼び出し（現状ゼロ）
    Sharer,       // 分かちの「腕がなまる」: ドハ → 守られた味方・肩代わりのたび
    CurseEnemy,   // 呪詛: ネル → 敵全体・開戦時1回
    CurseLeak,    // 呪詛の味方漏れ: ネル → 味方全体（SupportTargets 経由）・開戦時1回
    Shove,        // 突き返しの Stagger: ハネ → 隣接味方・1ターン1回
    Cower,        // 萎縮: クビ → 味方全体（SupportTargets 経由）・開戦時1回
    Relay,        // 渡しの転嫁: ワタ → **敵陣**の最高攻撃力の駒・横取りのたび。
                  // **窓口の中から窓口を呼ぶ唯一の経路**で、宛先が敵側なのもここだけ
    Slander,      // 誹り: 敵の保持者 → 殴った味方・攻撃のたび。
                  // **敵から味方へ弱体を撒く初めての経路**（第44期）。他の6本は味方が起点
    Overbear,     // 驕り: オゴ → 隣接する生存味方全員・毎ターン。
                  // **撒いた本人が撒いた結果を出力条件として読む唯一の経路**（第46期）
    Favor        // 火選りの鈍り: ヒヨ → 隣接する生存味方のうち**燃えていない**者・毎ターン。
                  // **状態異常を条件に宛先を選ぶ初めての弱体経路**（第58期）。
                  // 同じ1回の発火が <see cref="WhetRoute.Favor"/> の側も走らせる（表と裏）
}

/// <summary>経路の名前と本数。診断の表の見出しと配列長をここ1箇所から引く。</summary>
public static class DullRoutes
{
    public static readonly string[] Names = { "その他", "なまり", "呪詛敵", "呪詛漏れ", "突き返し", "萎縮", "渡し", "誹り", "驕り", "火選り" };
    public static int Count => Names.Length;
}

/// <summary>
/// 強化の経路。<b>診断（<c>whet</c>）が経路別に数えるためだけの札</b>で、盤面には一切影響しない。
/// <see cref="BattleContext.Whet"/> を通る6経路に1対1で対応する。
///
/// <para><b><see cref="DullRoute"/> と対になる。</b> 弱体側が第42期に窓口を持ってから
/// 第44期（誹り）・第46期（驕り）・第52期（駆り立て）がすべてそこへ接続できたのに対し、
/// 強化側は15箇所が <c>AtkBonus</c> を直に叩いていた（第52期 Phase 0-1 の持ち越し）。</para>
///
/// <para><b>通すのは「他者を強化する」6本だけ。</b> 自己強化の9本
/// （怒り・庇う／殉教・墓守2本・処刑・棘・澱み喰い・軋み・分かち）は直叩きのまま残してある
/// ——窓口は将来の横取りの立ち位置なので（<see cref="BattleContext.Dull"/> の中にウケとワタが
/// 立っている）、<b>「自分の被弾で自分が強くなる」を他人が横取りできる形にしてはいけない。</b></para>
/// </summary>
public enum WhetRoute
{
    Other,          // 札を付け忘れた呼び出し（現状ゼロ）
    Goad,           // 駆り立て: カリ → 隣接する CurrentAttack 最大の味方1体・毎ターン。
                    // 候補を自前で AcceptsSupport 濾しする（隣へ漏らさない）
    RallyOpening,   // 号令の鬨: ガン → 味方全体（SupportTargets 経由）・開戦時1回
    RallyTurn,      // 号令の溜め: ガン → 手番を差し出した味方（SupportTargets 経由）・毎ターン
    Bind,           // 縛め: クグ → 縛った味方1体・第2ターン以降の毎ターン。
                    // **プラスとマイナスが1つの動作の表と裏**（痺れ+16）なので量が最大
    Drifter,        // 移り木: シオ → 動かされた味方・移動のたび
    Regurgitate,    // 吐き戻し: ゴルム → 庇った相手（SupportTargets 経由）・肩代わりのたび。
                    // **engine 側にある唯一の経路**で、Dull の「なまり」（同じく engine 側）と対称
    Favor          // 火選り: ヒヨ → **燃えている味方全員**（自分を除く）・毎ターン。**位置を問わない**。
                    // 候補を自前で AcceptsSupport 濾しする（隣へ漏らさない＝駆り立てと同じ側）。
                    // **状態異常を条件に宛先を選ぶ初めての強化経路**（第58期）
}

/// <summary>経路の名前と本数。診断の表の見出しと配列長をここ1箇所から引く。</summary>
public static class WhetRoutes
{
    public static readonly string[] Names = { "その他", "駆り立て", "号令開戦", "号令毎T", "縛め", "移り木", "吐き戻し", "火選り" };
    public static int Count => Names.Length;
}

/// <summary>
/// 強化の経路を1本ずつ窓口の入口で落とすノブ（第65期・<b>診断専用</b>）。
///
/// <para><b>既定は空＝現行。</b> 誰も渡さなければ <see cref="BattleContext.Whet"/> は
/// 1命令も変わらない（受け入れ基準1: 引数なしの <c>compare</c> が 305 セル一致）。</para>
///
/// <para><b>落とすのは <c>AtkBonus</c> への加算だけで、計数は行う。</b>
/// 経路別の総量・受け手・横流しの経路はそのまま数え、<b>盤面に入る量だけを 0 にする</b>
/// ——「その経路を落とした版」の対照が「供給者の他の特性は残す」（指示書 §0-4）を
/// 満たすため。縛めの痺れ・移り木の回復・吐き戻しのログのように、
/// <b>同じ動作の他の効果には触れない。</b></para>
///
/// <para><b><see cref="BattleContext.Roll"/> の前後に置かない。</b> 判定は
/// <c>Whet</c> の中の加算の直前1箇所だけで、横流しの <see cref="PickOne"/> より後ろにある
/// ——乱数の消費を1ビットも変えないことを、位置で担保する。</para>
///
/// <para>自己強化の9本は窓口の外なので落とせない（第56期の意図的な非対称。この期の対象外）。</para>
/// </summary>
public sealed record WhetMask(int Bits)
{
    /// <summary>何も落とさない＝現行。</summary>
    public static readonly WhetMask None = new(0);

    /// <summary>経路1本だけを落とす。</summary>
    public static WhetMask Of(WhetRoute route) => new(1 << (int)route);

    public bool Blocks(WhetRoute route) => (Bits & (1 << (int)route)) != 0;
}

/// <summary>
/// 引き受け（集約）。隣接する味方が受ける攻撃力低下を代わりに背負い、その分だけ鎧になる。
/// ただし自分の腕は落ち続ける——<b>プラスとマイナスが1つの動作の表と裏</b>なので、
/// <see cref="TraitId"/> のどちらのブロックにも入らない（置き去り・突き返しと同じ扱い）。
///
/// <para><b>肩代わりで初めて「状態の肩代わり」になった。</b> 既存の肩代わりは5種
/// （庇う・分かち・巨躯・後備え・棘守り）あって<b>全部ダメージ</b>で、
/// 状態を肩代わりするものが1つも無かった。</para>
///
/// <para><b>同じ通貨をウツと逆向きに使う。</b> 逆しま（ウツ）は弱体を<b>攻撃力</b>
/// （下げ幅の3倍）に変え、引き受け（ウケ）は<b>アーマー</b>に変える。傷軸で
/// 維持攻（エグ）と維持防（ハリ）が波ごとに別の順位を作ったのと同じ形。</para>
///
/// <para><b>横取りの実装は <see cref="BattleContext.Dull"/> の中にある。</b>
/// 「弱体が入る直前に横取りする」機構なので、駒ごとのフックでは表現できない
/// （盤面ルールが engine 側に判定を持つのと同じ理由。ただし集約は盤面ルールではなく
/// 片陣営の駒の効果で、両陣営に等しくはかからない）。この Trait 本体は札にすぎない。</para>
///
/// <para><b>隣接に限定するのが設計の中核。</b> 「味方全体」にすると配置の判断が消え、
/// ウツと同居した瞬間に必ずウケが全部持っていく。隣接に限れば
/// 「ウツをウケの隣に置かない」という配置解が残る——同じ供給を2枚の読み手が
/// 配置で分け合う形になる。<b>召喚枠（スロット5〜8）も隣接表に含まれるので対象に入る</b>
/// （貫きのレーン経路・巨躯の被覆と同じ扱い）。</para>
///
/// <para><b>攻撃力ではなくアーマーにするのは意図的。</b> 被ダメージを減算で直接下げると、
/// 敵の一撃を下回った時点で無敵になって二値化する（アーマーがプールにしてある理由と同じ穴）。
/// アーマーは既にプールとして実装されていて HP の前に削られるので、上限を数値で切らずに
/// 崖を避けられる。<c>ModifyIncomingDamage</c> は<b>使わない</b>——実装が1つしかない
/// フックなので増やしたくはあるが、そこに書くと減算になって崖が戻る。</para>
///
/// <para><b>逆しまと1枚に持たせないこと。</b> 供給→横取り→3倍変換が1体で完結して
/// 自己完結したマイナスになり、<c>AtkBonus</c> に下限が無いので実質乗算になる
/// （ホタの熾火に次ぐ2つ目の乗算フラグ）。</para>
/// </summary>
public sealed class BearTrait : Trait
{
    public override TraitId Id => TraitId.Bear;
}

/// <summary>
/// 引き受けの強度。<b>診断（dull）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。
///
/// <para><paramref name="ArmorPerDull"/> は引き受けた攻撃力1点あたりに生成する
/// アーマー。<c>0</c> なら<b>横取りだけを走らせて変換を止める</b>——
/// 第41期が確立した形（「符号を測りたい効果は、その効果だけを 0 にできるノブと対にする」）。
/// 横取り自体を止めるノブは置いていない（それはウケを外した対照と同じなので二重になる）。</para>
///
/// <para><b>既定を無効にしなくてよい。</b> 味方側の駒なので、
/// <see cref="UnitCatalog.Uke"/> を編成に入れない限り既存45行は1バイトも動かない
/// （それ自体が回帰チェックになる）。static のノブを置かない理由は
/// <see cref="ColossusRule"/> / <see cref="YokeRule"/> / <see cref="ShoveRule"/> と同じ。</para>
/// </summary>
public readonly record struct BearRule(int ArmorPerDull)
{
    public static BearRule Default => new(2);
}


/// <summary>
/// 渡し（転嫁）。隣接する味方が受ける攻撃力低下を引き受け、<b>そのまま敵へ渡す</b>。
/// ただし通り道になった自分の身が削れる——<b>プラスとマイナスが1つの動作の表と裏</b>なので、
/// <see cref="TraitId"/> のどちらのブロックにも入らない（置き去り・突き返し・引き受けと同じ扱い）。
///
/// <para><b>弱体軸の三役目。</b> 第42期で窓口 <see cref="BattleContext.Dull"/> と読み手2枚が揃った。
/// 逆しま（ウツ）は弱体を<b>攻撃力</b>（下げ幅の3倍）に、引き受け（ウケ）は<b>アーマー</b>に変える
/// ——どちらも増幅がある代わりに<b>受け手1体で閉じる</b>。渡しは<b>増幅が無い代わりに
/// 味方全体に効く</b>（敵の攻撃力が下がるので、殴られる全員が得をする）。
/// 傷軸で抉り（+3/傷・維持）と断ち（+5/傷・全消費）が別の順位を作ったのと同じ形。</para>
///
/// <para><b>味方から敵へ状態を移す経路はロスターでこれが初めて。</b> 第40期の曝きが
/// 「敵から味方へ」を作ったのの逆向きで、<c>Dull</c> が最初から両陣営を通るように
/// 作ってあるので engine 側に足した規則はゼロ——<b>横取りして流し先を敵にするだけ</b>。</para>
///
/// <para><b>横取りの実装は <see cref="BattleContext.Dull"/> の中にある。</b>
/// 集約（<see cref="BearTrait"/>）とまったく同じ条件で、<b>候補プールも共有する</b>
/// ——隣接する生存味方／対象自身は除く／対象が横取り役（集約・渡し）なら横取りしない／
/// 候補が複数なら <c>PickOne</c>。<b>優先順位を固定しない</b>のは、固定すると
/// 片方が構造的に飢えるから（第41期「先に来る供給源だけが使われる」と同じ形）。
/// この Trait 本体は札にすぎない。</para>
///
/// <para><b>流し先は決定的に選ぶ</b>——敵陣で <c>CurrentAttack</c> が最も高い生存駒。
/// 乱数で選ぶと何に課金したのかが分離できない。<b>最高攻撃力を選ぶこと自体が
/// 自己分散になる</b>: 削れば次は別の敵が最高になるので、1体を 0 まで削り切る前に
/// 対象が移る（<c>CurrentAttack</c> の下限は 0 なので、崖を避けるための選び方でもある）。
/// 同値が並んだ場合だけ <c>PickOne</c>。</para>
///
/// <para><b>代金はHPで払う。</b> <see cref="HpCostPerDull"/> はノブにしない（定数）。
/// ウケの代金が <c>AtkBonus</c>（素の攻撃力 6 で底を打って止まる）なのに対し、
/// <b>HP には底が無い</b>ので、供給が太いほど代金が線形に伸びて自壊する
/// ——<b>可変コスト型</b>であることがこの駒のマイナスの本体。
/// 代金は <see cref="BattleContext.ApplyDamage"/> を通す（直接HPを引かない）ので、
/// 庇う・分かち・巨躯・後備え・棘守りが割り込む＝<b>「代金を誰かに肩代わりさせる」が
/// 編成の選択肢になる</b>。そのぶん意図した代金が効かなくなるので、
/// 診断（<c>relay</c>）は<b>自弁率</b>を必ず数える。</para>
///
/// <para><b>ワタ自身の攻撃力は落ちない。</b> 横取りした分の <c>AtkBonus</c> は
/// 誰にも乗らない（味方側から消えて敵側へ移る）。ウケが背負って 6.0 → 0.0 になったのとは
/// 天井の種類が違う。</para>
/// </summary>
public sealed class RelayTrait : Trait
{
    /// <summary>横取りした攻撃力1点あたりに自分が負うダメージ。<b>ノブにしない</b>（指示書 §2-1）。</summary>
    public const int HpCostPerDull = 2;

    public override TraitId Id => TraitId.Relay;
}

/// <summary>
/// 渡しの強度。<b>診断（relay）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。
///
/// <para><paramref name="TransferPercent"/> は横取りした量のうち敵へ流す割合。
/// <c>0</c> は<b>「横取りするが流さない」＝弱体がそこで消滅する</b>
/// ——これは<b>除去役</b>そのもので、対照であると同時に
/// <b>転嫁と除去を1つのノブで比較できる</b>（指示書 §2-2）。
/// 横取り自体を止めるノブは置かない（それはワタを外した対照と同じで二重になる）。</para>
///
/// <para><b>既定を無効にしなくてよい。</b> 味方側の駒なので、
/// <see cref="UnitCatalog.Wata"/> を編成に入れない限り既存47行は1バイトも動かない
/// （それ自体が回帰チェックになる）。static のノブを置かない理由は
/// <see cref="ColossusRule"/> / <see cref="YokeRule"/> / <see cref="ShoveRule"/> /
/// <see cref="BearRule"/> と同じ。</para>
/// </summary>
public readonly record struct RelayRule(int TransferPercent)
{
    /// <summary>探索段階の初期値（第43期）。</summary>
    public static RelayRule Default => new(100);
}

/// <summary>
/// 驕り（第46期）。<b>隣の味方を見下して腕を鈍らせ、隣が全員自分より弱くなったとき本気を出す。</b>
///
/// <para><b>プラスとマイナスが1つの動作の表と裏</b>——削るのは常時、報われるのは条件を
/// 満たしたときだけ。置き去り・突き返し・引き受け・渡しと同じ形なので、
/// <see cref="TraitId"/> のどちらのブロックにも入らない。</para>
///
/// <para><b>狙いは「隣接を非単調に読む」の n=2 化。</b> 第45期の調査は、隣接を
/// <b>単調な量</b>（隣に何人いるか）で読む駒は席が定数になると結論した——編成5枠の次数は
/// <c>{2, 4}</c> の2値しかないので、単調関数は2値の選好しか返せず、符号が決まった時点で
/// 席が決まる（実測の最頻率 78〜100%）。唯一の例外がロスターで1枚だけの非単調な読み手
/// （囃し立て・<see cref="MarkerTrait"/>＝隣で最大HPの1体を選ぶ）で、その1枚だけが
/// 対照と同水準に分散した（62% 対 63.5%）。<b>n=1 を n=2 にするのがこの特性の役目。</b></para>
///
/// <para><b>なぜ非単調か。</b> 条件は隣接する味方<b>全員</b>に対する AND なので、
/// <b>隣接数が増えるほど成立が遠のく</b>（中央は削る量が多い代わりに条件が厳しい）。
/// さらに<b>「隣が誰か」で成立時刻が変わる</b>——ドルガ（攻38）の隣なら 13ターン、
/// クビ（攻3）の隣なら即座。次数だけでは席が決まらない。</para>
///
/// <para><b>隣接する生存味方が 0 人のときは成立しない。</b> ここを成立にすると
/// 「角に置くだけで無条件2倍」になり、第45期が指摘した二択に戻る。
/// なお編成5枠は 0-4 を必ず埋めるので開幕は必ず 2 人以上いる——0 人は
/// <b>隣が全滅した後</b>にしか起きない（そこで2倍が消えるのは意図した揺れ）。</para>
///
/// <para><b><see cref="BattleContext.Dull"/> を必ず通す。</b> <c>AtkBonus</c> を直に引くと
/// ウケの横取り（アーマー化）もワタの転嫁（敵へ流す）も走らない。
/// <b>弱体軸への戦闘中の供給になることがこの駒の価値の半分</b>で、それは窓口の中にしか無い。
/// <c>AcceptsSupport</c>（ガルドの <c>Stoic</c>）は<b>見ない</b>——第44期の誹りと揃えた
/// （第42期からの持ち越しで、5経路の扱いは元から揃っていない）。</para>
///
/// <para><b>召喚枠（スロット5〜8）も隣接に含む。</b> 隣接表がそう作られているので、
/// 胞子が ○中1 に立てば削りの対象にも条件の判定にも入る（貫きのレーン経路・巨躯の被覆と同じ扱い）。</para>
///
/// <para><b>条件はフラグで固定しない。</b> 味方が倒れて隣接が減る／隣が育って条件から外れる、が
/// この駒の非単調性そのもの。<see cref="ModifyAttack"/> は毎回 <see cref="UnitState.Board"/> から
/// 隣接を読み直す（<c>Board</c> を足した理由はそこの doc を参照）。</para>
///
/// <para><b>隣の攻撃力は驕りを除いて評価する</b>（<see cref="PlainAttack"/>）。
/// 驕り持ちが2枚隣り合うと <c>CurrentAttack</c> が互いを呼び合って無限再帰するため。
/// 現行の編成に驕りは1枚しか入らないので<b>盤面の値は 1 も変わらない</b>が、
/// 1ターン1回の上限だけに頼らない突き返しの再入ガードと同じ判断で先に置く。</para>
///
/// <para>強度は <see cref="OverbearRule"/> で外から差す。書き換え可能な static のノブは置かない
/// （<see cref="ShoveRule"/> / <see cref="BearRule"/> / <see cref="RelayRule"/> と同じ判断）。</para>
/// </summary>
public sealed class OverbearTrait : Trait
{
    /// <summary>本気を出したときの倍率。<b>ノブにしない</b>（掃引の対象は <c>Drain</c> だけ）。</summary>
    public const int Multiplier = 2;

    public override TraitId Id => TraitId.Overbear;

    /// <summary>
    /// マイナス側 —— 常時。隣接する生存味方全員の腕を鈍らせる。
    ///
    /// <para><b>自分から移動を起こす手段は持たせない</b>（突き返しと同じ）。供給は毎ターンの
    /// 手番頭で、<c>Drain = 0</c> なら窓口を1回も叩かない＝<b>プラス側だけを切る対照</b>になる
    /// （削らなければ隣は弱くならないので条件が永遠に成立しない）。</para>
    /// </summary>
    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        int drain = ctx.Overbear.Drain;
        var hit = new List<string>();

        // Dull は横取り（集約・渡し）を走らせ、渡しは代金で駒を落とすので、
        // 列挙中に盤面が変わりうる。LivingMembers はスナップショット（ToList）。
        if (drain > 0)
        {
            foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
            {
                if (ally == self || !ally.IsAlive) continue;
                if (!FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;

                // 逆行の実測。**削ったのに相手が強くなった量**（逆しまの自己矛盾）を、
                // 予測ではなく窓口の前後の差で取る。読むだけで盤面は動かさない。
                int before = ally.CurrentAttack;
                ctx.Dull(ally, drain, DullRoute.Overbear);
                int after = ally.CurrentAttack;

                ctx.OverbearFired++;
                ctx.OverbearTotal += drain;
                ctx.OverbearTo[ally.Name] =
                    ctx.OverbearTo.TryGetValue(ally.Name, out int prev) ? prev + drain : drain;
                if (after > before)
                {
                    ctx.OverbearBackfire += after - before;
                    ctx.OverbearBackfireHits++;
                }
                hit.Add(ally.Name);
            }

            if (hit.Count > 0)
                ctx.Log($"    {self.Name} が {string.Join("・", hit)} を見下した（攻撃 -{drain}）",
                        LogKind.FriendlyFire);
        }

        // 成立率と成立時刻。**削った後**の盤面で測る（その手番の振りが受ける条件と揃える）。
        ctx.OverbearTurns++;
        if (Ready(self))
        {
            ctx.OverbearMetTurns++;
            if (ctx.OverbearFirstTurn == 0) ctx.OverbearFirstTurn = ctx.Turn;
        }
    }

    /// <summary>
    /// プラス側 —— 条件を満たしたときだけ攻撃力2倍。<b>形は後衛特化
    /// （<see cref="SniperTrait"/>）と同じ</b>で、位置ではなく状況で判定する。
    /// </summary>
    public override int ModifyAttack(UnitState self, int atk) => Ready(self, atk) ? atk * Multiplier : atk;

    /// <summary>
    /// 2倍が実際に乗った振りを数える。<b><see cref="ModifyAttack"/> の中では数えない</b>
    /// ——あれは <c>CurrentAttack</c> を読むたびに走るので（<c>StatSnapshot</c> でも走る）、
    /// 数えると「振った回数」ではなく「読まれた回数」になる。
    /// </summary>
    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        ctx.OverbearSwings++;
        if (Ready(self)) ctx.OverbearDoubled++;
    }

    /// <summary>
    /// 隣接する生存味方が1人以上いて、その全員が自分より弱いか。
    /// <paramref name="atk"/> を渡さない呼び出し（計数側）は素の攻撃力で判定する。
    /// </summary>
    private static bool Ready(UnitState self, int? atk = null)
    {
        BattleContext? board = self.Board;
        if (board is null) return false;   // 盤面の外で作られた駒。隣が1人もいないのと同じ扱い

        int mine = atk ?? PlainAttack(self);
        int seen = 0;

        // LivingMembers ではなく AllUnits を走るのは、ModifyAttack が攻撃のたびに呼ばれるから
        // （LivingMembers は ToList するので毎回の確保になる）。
        foreach (UnitState a in board.AllUnits)
        {
            if (a == self || !a.IsAlive || a.TeamId != self.TeamId) continue;
            if (!FormationRules.AreAdjacent(self.Slot, a.Slot)) continue;
            if (PlainAttack(a) >= mine) return false;
            seen++;
        }
        return seen > 0;
    }

    /// <summary>驕りを除いた <c>CurrentAttack</c>。驕り持ちどうしの相互再帰を切るためだけにある。</summary>
    private static int PlainAttack(UnitState u)
    {
        int atk = u.Def.Attack + u.AtkBonus;
        foreach (Trait t in u.Traits)
            if (t.Id != TraitId.Overbear) atk = t.ModifyAttack(u, atk);
        return Math.Max(0, atk);
    }
}

/// <summary>
/// 驕りの強度。<b>診断（overbear）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。
///
/// <para><paramref name="Drain"/> は毎ターン隣接する生存味方から引く攻撃力。
/// <c>0</c> は<b>「削らない」＝条件が永遠に成立しない</b>ので、
/// <b>プラス側だけを切る陽性対照</b>になる（マイナス側だけを切るノブは置かない——
/// それは2倍を無条件にすることで、第45期が棄却した二択そのものになる）。</para>
///
/// <para><b>既定を無効にしなくてよい。</b> 味方側の駒なので、<see cref="UnitCatalog.Ogo"/> を
/// 編成に入れない限り既存48行は1バイトも動かない（それ自体が回帰チェックになる）。
/// static のノブを置かない理由は <see cref="ShoveRule"/> / <see cref="BearRule"/> /
/// <see cref="RelayRule"/> と同じ。</para>
/// </summary>
public readonly record struct OverbearRule(int Drain)
{
    /// <summary>探索段階の初期値（第46期）。</summary>
    public static OverbearRule Default => new(2);
}

/// <summary>
/// 鱗。<b>アーマー（<see cref="StatusKeys.Armor"/>）に初めての読み手を作る</b>（第47期）。
///
/// <para>アーマーは7つの盤面状態キーの中で<b>読み手が0枚だった唯一の資源</b>
/// （書き手は砕け・集約の2つ、消費するのは <see cref="BattleContext.ApplyDamage"/> だけ）。
/// しかも性質が他と違う——<b>回復とは別資源で、<c>AcceptsSupport</c> を貫通する</b>ので
/// 「誰の助けも届かない」駒（ガルドの <see cref="TraitId.Stoic"/>）に唯一届く支援になっている。</para>
///
/// <para><b>隣接を1つも読まない。</b> 第45〜46期で隣接は2期かけて否定的な結論が出ている
/// （隣接を読む駒の席の最頻率 85%、驕りに至っては 100%）。鱗が読むのは
/// <b>自分が纏っている量</b>なので、席の問題から完全に外れる。</para>
///
/// <para><b>供給・発揮・消費の1サイクルが1枚に入っている。</b></para>
/// <list type="bullet">
///   <item><b>供給</b>（<see cref="OnAllyDeath"/>）: 味方が倒れるたび <see cref="GainPerDeath"/> を纏う。
///     <b>自己完結させないために、供給の条件を「他人の身に起きる出来事」にしてある</b>
///     ——自分では破片を作れない。</item>
///   <item><b>発揮</b>（<see cref="ModifyPattern"/>）: 纏っているあいだ攻撃が<b>貫き</b>になる。
///     ロスターに<b>常時の貫きは1枚も無い</b>（<c>Def.Pattern</c> は 単体43 / 薙ぎ3）ので、
///     これは「アーマーを纏っているあいだだけ後列に手が届く」駒になる。</item>
///   <item><b>消費</b>（<see cref="OnAfterAttack"/>）: 振るたび <c>ScaleRule.CostPerAttack</c> だけ剥がれる。
///     <b>アーマーは被弾でも削られるので二重支出</b>で、供給が細ければすぐ枯れる。
///     これがマイナス側で、「盾を削って刃にする」という1つの動作の表と裏になっている。</item>
/// </list>
///
/// <para><b><c>AcceptsSupport</c> を見ない。</b> 砕け（<see cref="ShatterTrait"/>）と揃える
/// ——アーマーは回復でも強化でもなく damage 側で消費されるプールなので、
/// 弱体の窓口（<see cref="BattleContext.Dull"/>）の作法ではなく砕けの作法に従うのが正しい。
/// 既存の <c>AcceptsSupport</c> の扱いが5経路で3通りに割れている件（第42期の持ち越し）には触らない。</para>
///
/// <para><b>召喚枠（胞子・亡骸）の死も通す。</b> <c>HandleDeath</c> は <c>OnAllyDeath</c> を
/// 生存味方全員に流すので、<see cref="TraitId.Ephemeral"/> の駒が倒れても供給が湧く。
/// <see cref="ReviverTrait"/> は同じ場所で儚い駒を除外しているが、あちらは
/// 「一度きりの効果を持つ駒の価値を蘇生が無制限に掛け算する」ことを止めるための除外で、
/// こちらは掛け算にならない——<b>胞子は胞子を産まないので供給は有限</b>（ムグ1体につき最大3件）。
/// <b>除外せず、儚い駒の寄与を診断で別に数える</b>（<c>ScaleGainEphemeral</c>）。</para>
///
/// <para><b>フラグで固定しない。</b> <see cref="ModifyPattern"/> は攻撃のたびに評価されるので、
/// 貫きは戦闘中に立ったり消えたりする（<see cref="SniperTrait"/> / <see cref="PyreTrait"/> と同じ形）。</para>
/// </summary>
public sealed class ScaleTrait : Trait
{
    /// <summary>
    /// 味方1体が倒れるたびに纏う破片の量。<b>定数。振らない。</b>
    /// 掃引の対象は <see cref="ScaleRule.CostPerAttack"/> だけで、
    /// 1変数を振るときに一緒に動かすものを増やさない（第46期の作法）。
    /// </summary>
    public const int GainPerDeath = 4;

    public override TraitId Id => TraitId.Scale;

    /// <summary>
    /// 纏い率の分母（保持者が生きてターン頭を迎えた回数）と分子（そのうち纏っていた回数）。
    /// <b>盤面には一切触らない。計数だけ。</b>
    /// </summary>
    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        ctx.ScaleAliveTurns++;
        if (self.Counter(StatusKeys.Armor) > 0) ctx.ScaleWornTurns++;
    }

    // --- 供給 ---------------------------------------------------------------------------------
    // 自分の死では発火しない（HandleDeath は dead を除いて流すので構造的に来ないが、明示しておく）。
    public override void OnAllyDeath(BattleContext ctx, UnitState self, UnitState dead)
    {
        if (!self.IsAlive || dead == self) return;

        self.SetCounter(StatusKeys.Armor, self.Counter(StatusKeys.Armor) + GainPerDeath);
        ctx.NoteScaleGain(GainPerDeath, ScaleSource.Death, dead.HasTrait(TraitId.Ephemeral));
        ctx.Log($"    {self.Name} が {dead.Name} の欠片を拾った（破片 {self.Counter(StatusKeys.Armor)}）",
                LogKind.Trigger);
    }

    // --- 発揮 ---------------------------------------------------------------------------------
    public override AttackPattern ModifyPattern(UnitState self, AttackPattern p)
        => self.Counter(StatusKeys.Armor) > 0 ? AttackPattern.Pierce : p;

    // --- 消費 ---------------------------------------------------------------------------------
    // 攻撃1回につき1度・主目標に対してのみ呼ばれる（貫きならレーンの先頭）。
    // **振った回数を数えている**ので、範囲で複数体に当たっても支出は1回ぶん。
    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dmg)
    {
        int cost = ctx.Scale.CostPerAttack;
        if (cost <= 0) return;

        int armor = self.Counter(StatusKeys.Armor);
        if (armor <= 0) return;

        int spent = Math.Min(armor, cost);
        self.SetCounter(StatusKeys.Armor, armor - spent);
        ctx.NoteScaleSpend(spent, armor - spent == 0);
        ctx.Log($"    {self.Name} の鱗が剥がれた（破片 {armor - spent}）", LogKind.Status);
    }
}

/// <summary>破片の出どころ。診断が獲得の内訳を割るためだけにある。</summary>
public enum ScaleSource
{
    /// <summary>味方の死（鱗そのものの供給）。</summary>
    Death,
    /// <summary>砕け（ヒビ）が配った破片。</summary>
    Shatter,
    /// <summary>集約（ウケ）が弱体を変換した鎧。現行の台では発生しない。</summary>
    Bear
}

/// <summary>
/// 鱗の強度のノブ。<b>診断（scale）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。
///
/// <para><paramref name="CostPerAttack"/> は攻撃1回あたり剥がれる破片の量。
/// <b><c>0</c> は「消費しない」＝維持型</b>で、纏った破片が被弾で削られるまで貫きが続く。
/// つまりこのノブは強度ではなく<b>性質</b>（維持型か消費型か）を切り替える
/// ——第41期の掃引が「比が全点で 2.30 で動かない＝ノブは強度しか変えない」に終わったので、
/// 性質を動かすノブを選んである。</para>
///
/// <para><b>既定を無効にしなくてよい。</b> 味方側の駒なので、<see cref="UnitCatalog.Uro"/> を
/// 編成に入れない限り既存の行は1バイトも動かない（それ自体が回帰チェックになる）。
/// static のノブを置かない理由は <see cref="ShoveRule"/> / <see cref="BearRule"/> /
/// <see cref="RelayRule"/> / <see cref="OverbearRule"/> と同じ。</para>
/// </summary>
public readonly record struct ScaleRule(int CostPerAttack)
{
    /// <summary>探索段階の初期値（第47期）。</summary>
    public static ScaleRule Default => new(1);
}

/// <summary>
/// 業。<b>ロスターで初めて「状態異常の種類数」を読む駒</b>（第49期）。
///
/// <para>第48期の棚卸しで10通貨すべての厚みを数えたところ、<b>すべての軸が「同じ通貨を厚くする」
/// 方向を向いていた</b>——毒5枚・死9枚・弱体5枚に対して、<b>幅を要求する駒が1枚もない</b>。
/// 業はそこを埋める。</para>
///
/// <para><b>量ではなく種類を読む。</b> 量を読むと供給がいちばん厚い通貨（毒・死）に吸われて
/// 実質そちらの軸の駒になる（第41期に突き返しが喧噪だけに食われたのと同じ形）。
/// 種類なら、<b>マイナス特性の多様性そのものが力になる</b>。</para>
///
/// <para><b>数える種類は <see cref="StatusKeys.All"/> から <see cref="StatusKeys.Armor"/> と
/// <see cref="StatusKeys.IdleTurn"/> を除いたもの</b>（<see cref="Kinds"/>）。除外は2つとも
/// engine の実装から来ている:</para>
/// <list type="bullet">
///   <item><b>アーマー</b>は <c>ApplyDamage</c> が HP の前に削る<b>プラスの資源</b>。
///     数えるとヒビ（砕け）1枚で種類数を稼げる抜け道になる。</item>
///   <item><b><c>IdleTurn</c></b> は行動順ループが痺れを振り替えて書く
///     （<c>Stun &gt; 0</c> → <c>SetCounter(Stun, 0)</c> / <c>SetCounter(IdleTurn, turn)</c>）。
///     数えると1経路で2カウントになる。しかも<b><c>0</c> に戻す箇所が engine に1つも無い</b>ので、
///     一度でも手番を落とした駒は以後ずっと1種類を持ち続けることになる。</item>
/// </list>
/// <para>残る5つのうち<b>傷（<see cref="StatusKeys.Wound"/>）は味方に載る経路が1つも無い</b>
/// （裂き・刻み・断ち・縫いはすべて <c>target</c> ＝敵に書く。第49期 Phase 0-1 の実測でも
/// 50行 × 5波 × 200試行で 0 件）。<b>それでも <see cref="Kinds"/> からは外していない</b>
/// ——外すのは「今のロスターにその経路が無い」という一時的な事実で、
/// 規則として書くと将来その経路が生えたときに静かに数え落とす。
/// <b>実効の分母は 4</b>（毒・標・痺・燃）。</para>
///
/// <para><b>可動部は3つで、1枚の中で1サイクルが閉じる</b>（第47期の鱗＝供給・発揮・消費と同じ構成）。</para>
/// <list type="bullet">
///   <item><b>引き取り</b>（<see cref="OnTurnStart"/>）: 生存する味方（自分を除く）が持つ種類のうち、
///     <b>自分がまだ持っていない種類を1つ</b>選んで<b>移す</b>。複製ではない
///     ——味方からは減り、自分に増える。移す量は <see cref="TransferAmount"/>（= 1）。</item>
///   <item><b>発揮</b>（<see cref="OnAfterAttack"/>）: 背負っている種類数が
///     <c>ScapegoatRule.Threshold</c> 以上のとき、殴った相手に<b>背負っている全種類を1ずつ付ける</b>。
///     種類を選ばない（選択を入れると可動部が増える）。閾値未満のときは何もしない（段を2つ作らない）。</item>
///   <item><b>マイナス</b>: 特別な実装は無い。<b>引き取った呪いはそのまま自分に効く</b>
///     ——毒は自分を削り、燃焼は自分を焼き、標は敵の攻撃を自分に集め、痺れは自分の手番を奪う。
///     <b>代金を軽くする細工を一切していない</b>のがこの駒の設計。</item>
/// </list>
///
/// <para><b>痺れは構造的に「使えない種類」。</b> <c>OnTurnStart</c> は行動順ループの<b>外側</b>で
/// 全員ぶん先に流れるので、痺れを引き取ったターンのゴウは、その後の行動順ループで
/// <c>Stun &gt; 0</c> に当たって手番を飛ばす——<b>痺れを持っているターンのゴウは必ず攻撃しない
/// ＝転写できない</b>。引き取り（<c>OnTurnStart</c>）と発揮（<c>OnAfterAttack</c>）が
/// 同じ資源を奪い合う形で、これは設計として正しい（潰さずに診断で測る）。</para>
///
/// <para><b><c>AcceptsSupport</c> を見ない。</b> 呪いを引き取るのは支援ではない
/// ——回復でも強化でもなく、<b>相手のマイナスを自分に移す</b>操作なので、
/// 支援を拒む駒（ガルドの <see cref="TraitId.Stoic"/>）から取り上げるのは筋が通る。
/// 弱体の窓口（<see cref="BattleContext.Dull"/>）の作法にも従わない
/// ——あちらは <c>AtkBonus</c> の話で、状態異常のカウンタとは資源が違う。
/// 第42期からの「<c>AcceptsSupport</c> の扱いが5経路で3通りに割れている」件には触らない。</para>
///
/// <para><b>燃焼を <see cref="BattleContext.Ignite"/> に通していない。</b>
/// <c>Ignite</c> は残ターンを <see cref="BurnRules.Turns"/>（= 3）に<b>設定</b>するので、
/// 1 を移すつもりで呼ぶと味方は 1 減って自分は 3 増える＝<b>2 ターンぶんの燃焼が生まれる</b>。
/// それは「移す」ではなく複製で、この駒の一文（引き取るほど自分が壊れる）を壊す。
/// <b>種類を問わず一律にカウンタを 1 だけ動かす</b>のが正しい——燃焼だけ窓口を変えると、
/// 「どの種類なら移すのが複製になるか」を呼び出し側ごとに覚えることになる。</para>
///
/// <para><b>隣接を1つも読まない。</b> 引き取りの候補は生存味方**全員**。第45〜47期で
/// 隣接は3期かけて否定的な結論が出ている。ただし<b>供給の側は隣接で決まる</b>
/// （火の粉はボルグの隣・囃し立てはヒサの隣）ので、席は第47期の鱗と同じく
/// 「読まないのに分散しない」形になりうる——診断で測る。</para>
/// </summary>
public sealed class ScapegoatTrait : Trait
{
    /// <summary>1回に移すカウンタの量。<b>定数。振らない。</b>（掃引の対象は
    /// <see cref="ScapegoatRule.Threshold"/> だけ。1変数を振るときに一緒に動かすものを増やさない。）</summary>
    public const int TransferAmount = 1;

    /// <summary>
    /// 数える種類。<see cref="StatusKeys.All"/> から アーマー と <c>IdleTurn</c> を除いたもの。
    /// <b>除外を並べる形で書いてある</b>ので、<c>StatusKeys</c> にキーが増えたら自動で数に入る
    /// （「今のロスターに経路が無い」を規則として焼き付けない）。
    /// </summary>
    public static readonly string[] Kinds = StatusKeys.All
        .Where(k => k != StatusKeys.Armor && k != StatusKeys.IdleTurn).ToArray();

    /// <summary>
    /// 業が<b>書いた</b>ぶんの控え（種類ごと）。<b>診断が「転写の効き」を帰属させるためだけにある。</b>
    /// これを読んで分岐する規則は1つも無い（＝盤面は動かない）。
    /// <c>Counters</c> のキーは特性の私有物、という規約に従って接頭辞で名前空間を切ってある。
    /// </summary>
    public static string OwedKey(string kind) => "sgOwed_" + kind;

    public override TraitId Id => TraitId.Scapegoat;

    // --- 引き取り -------------------------------------------------------------------------------
    //
    // **行動順ループの外側**（engine が全員ぶん先に流す）。だから同じターンのうちに
    // 発揮（OnAfterAttack）まで到達できるし、痺れを引き取れば同じターンの手番が飛ぶ。
    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (!self.IsAlive) return;

        // 引き取る前に一度も数えない。**このターンの種類集合は引き取りの後に確定する**ので、
        // 成立率・到達・平均種類数はすべて引き取りの後で数える。
        var missing = Kinds.Where(k => self.Counter(k) <= 0).ToList();

        if (missing.Count == 0)
        {
            // 全種類を既に背負っている。**空振りとは別に数える**——
            // 「引き取れる種類が盤面に無い」と「もう引き取る余地が無い」は原因が違う。
            ctx.ScapegoatFull++;
        }
        else
        {
            var pool = new List<(UnitState Ally, string Kind)>();
            foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
            {
                if (ally == self) continue;
                foreach (string k in missing)
                    if (ally.Counter(k) > 0) pool.Add((ally, k));
            }

            if (pool.Count == 0)
            {
                ctx.ScapegoatMissed++;
            }
            else
            {
                // PickOne と同じ消費規則（候補1個では Roll を消費しない）。
                // PickOne 本体は UnitState 専用なので、ここは組の上で同じ形を書く。
                var pick = pool.Count == 1 ? pool[0] : pool[ctx.Roll(pool.Count)];

                int take = Math.Min(TransferAmount, pick.Ally.Counter(pick.Kind));
                pick.Ally.SetCounter(pick.Kind, pick.Ally.Counter(pick.Kind) - take);
                self.SetCounter(pick.Kind, self.Counter(pick.Kind) + take);

                ctx.NoteScapegoatTake(pick.Kind, pick.Ally.Def.Name, take);
                ctx.Log($"    {self.Name} が {pick.Ally.Name} の{StatusKeys.LabelOf(pick.Kind)}を引き取った",
                        LogKind.Trigger);
            }
        }

        // 成立率・到達・種類数（引き取りの後の状態で数える）。**盤面には触らない。**
        ctx.NoteScapegoatStand(Kinds.Count(k => self.Counter(k) > 0));
    }

    // --- 発揮 -----------------------------------------------------------------------------------
    // 攻撃1回につき1度・主目標に対してのみ（engine の規則）。
    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        ctx.ScapegoatSwings++;

        int held = Kinds.Count(k => self.Counter(k) > 0);
        if (held < ctx.Scapegoat.Threshold) return;
        if (!target.IsAlive) return;

        // **種類を選ばない。** 選択を入れると可動部が増える（第46期の教訓）。
        // 自分からは減らさない——維持型。溜め込んだものは減らずに写る。
        foreach (string k in Kinds)
        {
            if (self.Counter(k) <= 0) continue;
            target.SetCounter(k, target.Counter(k) + TransferAmount);
            target.SetCounter(OwedKey(k), target.Counter(OwedKey(k)) + TransferAmount);
            ctx.NoteScapegoatWrite(k, TransferAmount);
        }
        ctx.ScapegoatFired++;
        ctx.Log($"    {self.Name} が {target.Name} に溜め込んだものを返した（{held} 種）",
                LogKind.Highlight);
    }

    /// <summary>
    /// 部隊戦の境界で控えを捨てる。<see cref="OwedKey"/> は <see cref="StatusKeys"/> に無いので
    /// 境界の一律掃除では消えない（庇うの <c>guardPending</c> と同じ理由）。
    /// <b>持ち越すのは勝った側だけなので敵側では走らないが、味方に業が2枚並ぶ将来のために書いておく。</b>
    /// </summary>
    public override void OnCarryOver(UnitState self)
    {
        foreach (string k in Kinds) self.SetCounter(OwedKey(k), 0);
    }
}

/// <summary>
/// 業の強度のノブ。<b>診断（scapegoat）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。
///
/// <para><paramref name="Threshold"/> は発揮に必要な種類数。
/// <b>これが律速項であることは実装の前に確かめてある</b>（第49期 Phase 0-7）——
/// 引き取りの<b>量</b>を振っても種類数は動かない（1 カウント移せば種類は成立する）ので、
/// 量のノブは到達時刻を1ターンも変えられない。閾値のほうは
/// 「累積3種に届く台（未達 19.2%）」と「構造的に届かない台（未達 100%）」を
/// そのまま分ける。第41期（<see cref="ShoveRule"/>）と第47期（<see cref="ScaleRule"/>）が
/// <b>律速でない項にノブを付けて掃引の全幅 1pt 未満</b>に終わった轍を踏まないための選択。</para>
///
/// <para><b>既定を無効にしなくてよい。</b> 味方側の駒なので、<see cref="UnitCatalog.Gou"/> を
/// 編成に入れない限り既存の行は1バイトも動かない（それ自体が回帰チェックになる）。
/// static のノブを置かない理由は <see cref="ShoveRule"/> / <see cref="BearRule"/> /
/// <see cref="RelayRule"/> / <see cref="OverbearRule"/> / <see cref="ScaleRule"/> と同じ。</para>
/// </summary>
public readonly record struct ScapegoatRule(int Threshold, bool Audit)
{
    /// <summary>閾値だけを指定する。監査は既定で切る（＝通常の実行）。</summary>
    public ScapegoatRule(int threshold) : this(threshold, false) { }

    /// <summary>探索段階の初期値（第49期）。</summary>
    public static ScapegoatRule Default => new(3, false);
}

/// <summary>
/// 逸らし。<b>ロスターで初めて標（<see cref="StatusKeys.Marked"/>）を操作する駒</b>（第50期）。
///
/// <para><b>標は engine が常時読んでいる強い通貨なのに、盤面での操作手段が無かった。</b>
/// 書き手は囃し立て（ヒサ）1枚で「隣接する最大HPの味方1体に<b>開戦時1回</b>」——選択の余地がゼロ。
/// <b>消す経路は1つも無い</b>（第50期 Phase 0-3。<c>SetCounter(Marked, 0)</c> は grep で 0 件）。
/// 駒の読み手は仇討ち（ザン）1枚だが、<b>engine の窓口</b>
/// （<see cref="BattleEngine.MarkPullPercent"/> = 75・<c>SelectTargetChain</c>）は
/// <b>すべての単体攻撃</b>で評価される。</para>
///
/// <para><b>標が engine の鎖の中で持つ性質</b>（Phase 0-2。この駒の設計の前提）:</para>
/// <list type="number">
///   <item><b><c>75</c> は確率であって重みではない。</b> ただし「既に主目標が標持ちなら引かない」
///     （<c>marked != target</c>）ので実効の被狙撃率は 75% より高い
///     ——<c>1/n + (1 − 1/n) × 0.75</c>（標持ちが pool にいるとき）。</item>
///   <item><b><c>foes</c> から選んでいる（<c>pool</c> ではない）。</b> つまり標は
///     <b>「前列が生きている限り後列は狙われない」という盤面の中核規則を破る</b>
///     ——ロスターで標だけが持つ性質（執着・断ちの選好は <c>pool</c> から選ぶので破らない）。</item>
///   <item><b>標持ちが複数いると <c>PickOne</c> で1体に絞ってから 75% を引く。</b>
///     引きは1回しか起きないが、標持ちが増えると <c>p_t</c>（無作為の主目標が既に標持ちである確率）が
///     上がるので、標の集合が集める総量は <c>p_t + (1 − p_t) × 0.75</c> で<b>増える</b>
///     （実測 81.9% → 95.4%）。1体あたりの取り分は逆に薄まる
///     ——<see cref="DivertRule.TargetCount"/> は<b>「集中」と「被覆」を取り替えるノブ</b>。</item>
///   <item><b>鎖の順序は 標 → 後備え → 庇う → 殉教 → 棘守り で、標がいちばん先。</b>
///     標が引いた瞬間に <c>return</c> するので、<b>標は庇い・後備え・殉教をすべて飛び越す。</b></item>
/// </list>
///
/// <para><b>1つの動作の表と裏</b>（置き去り・責め苦・仇討ち・突き返し・鱗と同型）。
/// <see cref="OnTurnStart"/> の1回の発火で3つを順に行う:</para>
/// <list type="bullet">
///   <item><b>外す</b>（プラス）: 生存する味方（自分を除く）の標を全部 0 にする。
///     <b>ロスターで初めて標を消す。</b> 囃し立てを打ち消す唯一の手段になる。</item>
///   <item><b>自分に付ける</b>（マイナス）: 自分の標を 1 にする。
///     <b>代金に特別な実装は無い</b>——標を負うこと自体が代金で、
///     回避率も被ダメ軽減も持たせていない。</item>
///   <item><b>敵に付ける</b>（プラス）: 敵陣の生存駒のうち<b>現在HPが最も高い順に
///     <see cref="DivertRule.TargetCount"/> 体</b>へ標を 1 付ける。
///     <b>選び方は決定的</b>（同値のみ <see cref="BattleContext.PickOne"/>）で、
///     プレイヤーが「どの敵が焦点になるか」を読める。</item>
/// </list>
///
/// <para><b>敵に付けた標は消さない（仕様）。</b> 外す対象は味方だけ。標には消す経路が無いので、
/// <b>焦点を浴びた敵のHPが下がると次のターンには別の敵が最高HPになり、そちらにも標が付く</b>
/// ——<b>焦点は放っておくと自分で溶ける。</b> これは設計の帰結であって取りこぼしではないが、
/// <see cref="DivertRule.TargetCount"/> の掃引を鈍らせるので診断で数える（<c>焦点数</c>）。</para>
///
/// <para><b>味方に標が1つも無くても、自分と敵への付与は行う。</b>
/// 外す対象が無いだけで、発火そのものは止めない
/// ——止めると「味方が綺麗なら何も起きない」駒になり、代金だけが残る局面が作れなくなる。</para>
///
/// <para><b>単体攻撃にしか効かない</b>（engine の鎖が <c>pattern != Single</c> を手前で返す）。
/// 薙ぎ・全体・貫きは標を1ビットも見ないので、<b>敵の攻撃型の構成がそのまま効き目の上限になる。</b></para>
/// </summary>
public sealed class DivertTrait : Trait
{
    public override TraitId Id => TraitId.Divert;

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (!self.IsAlive) return;

        // --- 外す（味方から。自分は除く）--------------------------------------------------
        int stripped = 0;
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self || ally.Counter(StatusKeys.Marked) <= 0) continue;
            ally.SetCounter(StatusKeys.Marked, 0);
            ctx.NoteDivertStrip(ally.Def.Name);
            stripped++;
            ctx.Log($"    {self.Name} が {ally.Name} から視線を引き剥がした", LogKind.Trigger);
        }

        // --- 自分に付ける（代金）----------------------------------------------------------
        // **`DivertRule.SelfMark` が偽なら付けない。** これは強度のノブではなく
        // 「代金を分離するための対照」で、診断だけが偽を渡す（§4 の対照2）。
        if (ctx.Divert.SelfMark && self.Counter(StatusKeys.Marked) <= 0)
        {
            self.SetCounter(StatusKeys.Marked, 1);
            ctx.Log($"    {self.Name} が矢面に立った", LogKind.FriendlyFire);
        }

        // --- 敵に付ける（焦点）------------------------------------------------------------
        // **現在HPが最も高い生存駒から順に TargetCount 体。** 同値のみ PickOne で割る
        // （席番号の若い順で決めないための唯一の窓口。鏡像の配置を同値にする）。
        var foes = ctx.LivingMembers(ctx.Opponent(self.TeamId)).ToList();
        int focused = 0;
        for (int i = 0; i < ctx.Divert.TargetCount && foes.Count > 0; i++)
        {
            int top = foes.Max(f => f.Hp);
            UnitState? pick = ctx.PickOne(foes.Where(f => f.Hp == top).ToList());
            if (pick is null) break;
            foes.Remove(pick);   // 同じ相手に2回付けない（TargetCount は「体数」）

            bool fresh = pick.Counter(StatusKeys.Marked) <= 0;
            pick.SetCounter(StatusKeys.Marked, 1);
            ctx.NoteDivertFocus(pick.Def.Name, fresh);
            focused++;
            if (fresh)
                ctx.Log($"    {self.Name} が {pick.Name} へ視線を向け直した", LogKind.Trigger);
        }

        ctx.NoteDivertFire(stripped, focused,
            ctx.LivingMembers(ctx.Opponent(self.TeamId)).Count(f => f.Counter(StatusKeys.Marked) > 0));
    }
}

/// <summary>
/// 逸らしの強度のノブ。<b>診断（divert）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。
///
/// <para><paramref name="TargetCount"/> は敵に標を付ける体数。
/// <b>これは強度ではなく「集中」と「被覆」を取り替えるノブ</b>——engine の鎖は標持ちを
/// <c>PickOne</c> で1体に絞ってから 75% を引くが、標持ちが増えると
/// <c>p_t</c>（無作為の主目標が既に標持ちである確率）が上がるので<b>総量は増え</b>
/// （実測で味方の単体振りの 81.9% → 95.4% が標持ちに当たる）、<b>1体あたりの取り分は薄まる</b>。
/// <b>勝率の掃引が平らなのは、この2つが打ち消し合うから</b>であって、
/// ノブが機構を動かしていないからではない（第41期・第47期の空振りとはここが違う）。</para>
///
/// <para><paramref name="SelfMark"/> は<b>ノブではない</b>。
/// 「味方の標を外す」効果と「自分が矢面に立つ」代金を分離するための<b>対照</b>で、
/// 既定は常に <c>true</c>。診断の対照2だけが <c>false</c> を渡す。</para>
///
/// <para><paramref name="Audit"/> も<b>ノブではない</b>。計数のフックを走らせるだけのスイッチで、
/// <b>素体の対照（特性なし・同数値）でも撃破ターンと単体振りを同じ切り方で数える</b>ために要る
/// （第49期の <c>ScapegoatRule.Audit</c> と同型）。<b>盤面を1つも動かさない</b>ことは
/// 診断 §0 が「監査あり」と「監査なし」を突き合わせて毎回検算する。</para>
///
/// <para><b>既定を無効にしなくてよい。</b> 味方側の駒なので、<see cref="UnitCatalog.Sora"/> を
/// 編成に入れない限り既存の行は1バイトも動かない（それ自体が回帰チェックになる）。
/// static のノブを置かない理由は <see cref="ShoveRule"/> / <see cref="BearRule"/> /
/// <see cref="RelayRule"/> / <see cref="ScaleRule"/> / <see cref="ScapegoatRule"/> と同じ。</para>
/// </summary>
public readonly record struct DivertRule(int TargetCount, bool SelfMark, bool Audit)
{
    /// <summary>焦点の数だけを指定する。代金（自分への標）は払い、監査は切る＝通常の実行。</summary>
    public DivertRule(int targetCount) : this(targetCount, true, false) { }

    /// <summary>焦点の数と代金の有無を指定する（診断の対照2）。監査は切る。</summary>
    public DivertRule(int targetCount, bool selfMark) : this(targetCount, selfMark, false) { }

    /// <summary>探索段階の初期値（第50期）。</summary>
    public static DivertRule Default => new(1, true, false);
}

/// <summary>
/// 駆り立て。<b>隣のいちばん殴れる味方を前に押し出し、自分の力を渡す</b>（第52期）。
///
/// <para><b>設計の出発点は囃し立て（ヒサ）だった。</b> ヒサは <c>PlusText</c> が
/// 「隣接する味方1体に敵の攻撃を集中させる」——<b>プラス欄に書いてあるのが味方への害</b>で、
/// 発火は開戦時1回・対象は「隣接する最大HPの味方」に固定。盤上で何も起きず、拾う理由が無い。
/// 対して縛め（クグ）は「毎ターン味方1体を縛る」という害の中に<b>「その味方の攻撃+16」</b>が
/// 埋まっている。<b>この駒はクグ側の構造で作ってある</b>——矛先を集めると同時に、
/// <b>集めた相手に力を渡す。</b></para>
///
/// <para><b>設計原則</b>: <b>マイナスは編成のフックであって、その駒を入れる動機ではない。</b>
/// 味方を犠牲にするだけの駒は盤面を弱くするので打点で釣り合わせても「入れるほど損」になる。
/// <b>害の中に見返りを埋めるのが、この盤面で機能している唯一の形</b>（クグが前例）。</para>
///
/// <para><b>1つの動作の表と裏</b>（置き去り・責め苦・仇討ち・突き返し・鱗・逸らしと同型）。
/// <see cref="OnTurnStart"/> の1回の発火で以下を順に行う:</para>
/// <list type="number">
///   <item><b>前ターンの対象から標を外す</b>（<b>強化は残す</b>）。
///     「一度渡した力は返らないが、矛先は移る」。</item>
///   <item><b>選ぶ</b>: 隣接する生存味方のうち <c>CurrentAttack</c> が最も高い1体。
///     同値のみ <see cref="BattleContext.PickOne"/>。<b>隣接に候補がいなければ何もしない</b>
///     （＝空振り。自己完結しない）。</item>
///   <item><b>標を付ける</b>（マイナス）: 選んだ相手の <see cref="StatusKeys.Marked"/> を 1 に。
///     <b>代金に特別な実装は無い</b>——engine の鎖（<see cref="BattleEngine.MarkPullPercent"/> = 75）が
///     敵の単体攻撃をそこへ引く。</item>
///   <item><b>力を渡す</b>（プラス）: 選んだ相手の <see cref="UnitState.AtkBonus"/> に
///     <see cref="GoadRule.Boost"/> を<b>加算</b>する。</item>
/// </list>
///
/// <para><b>選び方を「最高攻撃力」にするのは意図的。</b> (1) 一番殴れる駒を前に出す、という
/// 判断が1行で説明できる (2) ヒサ（最大HP）と対象条件が違う (3) <b>強化するほどその駒が
/// 選ばれ続ける</b>ので、<b>強化と危険が同じ1体に集中する</b>——前線が1枚できる代わりに、
/// その1枚が死ぬ。<b>素の <c>Def.Attack</c> ではなく <c>CurrentAttack</c> を読む</b>のがこの
/// 固定を作る要で、<b>逆しま（ウツ）だけは自己修正する</b>——強化されると
/// <see cref="PerverseTrait"/> が攻撃力を半減するので、渡した次のターンには選ばれにくくなる。</para>
///
/// <para><b>強化は累積し、上限を数値で切らない。</b> 天井は戦闘長
/// （第41期の突き返し・第47期の鱗と同じ）。<b>対象が変わっても前の強化は消さない。</b></para>
///
/// <para><b>候補は <see cref="UnitState.AcceptsSupport"/> で絞る</b>——縛め
/// （<see cref="BindTrait"/>）と揃えた。1つの動作なので<b>標と強化で候補集合を分けない</b>:
/// 力を渡せない相手（誓約が壊れたガルド）は押し出しもしない。</para>
///
/// <para><b>代金を軽くする細工はしていない。</b> 押し出した相手への被害を肩代わりしたり、
/// 標の効果を弱めたりすると、押し出しが無償の強化になって符号が反転しなくなる。</para>
///
/// <para><b>粛（<see cref="HushTrait"/>）に封じられない</b>（第52期 Phase 0-5）。
/// <c>OnTurnStart</c> は行動順ループの<b>外側</b>で、<c>CanActOutOfTurn</c> を通らない。
/// <b>介入ではないので肩代わりの網にも吸われない</b>——標は攻撃が発生する前の選択段で働く。</para>
///
/// <para><b>逸らし（<see cref="DivertTrait"/>）と打ち消し合う。</b> 両者とも
/// <c>OnTurnStart</c> で標を操作し、<b>発火順は席番号の昇順</b>（engine は
/// <c>ctx.AllUnits</c> ＝ 味方をスロット昇順に並べた順で回す）。ソラの席がカリより
/// <b>後ろ</b>なら、カリが付けた標をその手番のうちにソラが剥がす。前なら残る。
/// <b>順序に依存する挙動</b>で、診断が <c>標消え</c> の列で数える。</para>
/// </summary>
public sealed class GoadTrait : Trait
{
    /// <summary>前ターンに押し出した味方の <c>InstanceId + 1</c>。0 は未設定。</summary>
    public const string TargetKey = "goadTarget";

    public override TraitId Id => TraitId.Goad;

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (!self.IsAlive) return;

        // --- 前ターンの対象を引く（標を外すためだけ。強化は残す）--------------------------
        UnitState? prev = null;
        int id = self.Counter(TargetKey) - 1;
        if (id >= 0)
            foreach (UnitState u in ctx.AllUnits)
                if (u.InstanceId == id) { prev = u; break; }

        // **標が誰かに剥がされていたか**（ソラの逸らしが唯一の経路。第52期 Phase 0-3）。
        // 代金なし版（Mark = false）では自分が付けていないので数えない。
        bool lost = ctx.Goad.Mark && prev is not null && prev.IsAlive
                    && prev.Counter(StatusKeys.Marked) <= 0;
        prev?.SetCounter(StatusKeys.Marked, 0);

        // --- 選ぶ（隣接する生存味方のうち CurrentAttack が最大の1体）----------------------
        var adj = ctx.LivingMembers(self.TeamId)
            .Where(a => a != self && a.AcceptsSupport
                        && FormationRules.AreAdjacent(self.Slot, a.Slot)).ToList();
        int top = adj.Count == 0 ? 0 : adj.Max(a => a.CurrentAttack);
        UnitState? pick = ctx.PickOne(adj.Where(a => a.CurrentAttack == top).ToList());

        if (pick is null)
        {
            self.SetCounter(TargetKey, 0);
            ctx.NoteGoadIdle();
            ctx.Log($"    {self.Name} は前に出せる味方がいなかった", LogKind.Action);
            return;
        }

        bool switched = prev is not null && !ReferenceEquals(prev, pick);
        if (ctx.Goad.Mark) pick.SetCounter(StatusKeys.Marked, 1);
        ctx.Whet(pick, ctx.Goad.Boost, WhetRoute.Goad);
        self.SetCounter(TargetKey, pick.InstanceId + 1);

        ctx.NoteGoadFire(pick, ctx.Goad.Boost, switched, lost);
        ctx.Log($"    {self.Name} が {pick.Name} を前へ押し出した"
            + $"（攻撃 +{ctx.Goad.Boost} → {pick.CurrentAttack}{(ctx.Goad.Mark ? " / 狙われる" : "")}）",
            LogKind.FriendlyFire);
    }

    /// <summary>
    /// <c>InstanceId</c> は戦闘ごとに振り直されるので、部隊戦の境界で必ず捨てる
    /// （執着の <see cref="FixateTrait.MemoryKey"/> と同じ理由）。
    /// </summary>
    public override void OnCarryOver(UnitState self) => self.SetCounter(TargetKey, 0);
}

/// <summary>
/// 駆り立ての強度のノブ。<b>診断（goad）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない（既定は <see cref="Default"/>）。
/// static のノブにしない理由は同型の doc（<see cref="ColossusRule"/>）を参照。
///
/// <para><paramref name="Boost"/> は1回に渡す攻撃力。<b>見返りの大きさを切るノブ</b>で、
/// <b>標の危険は <paramref name="Boost"/> に依存しない</b>——第50期の
/// <see cref="DivertRule.TargetCount"/> のような打ち消し（総量と取り分が逆を向く）は
/// 構造上起きない。掃引が平らなら、それは「渡した力がダメージに変わっていない」の意。</para>
///
/// <para><paramref name="Mark"/> は<b>ノブではない</b>。「力を渡す」効果と
/// 「矛先を集める」代金を分離するための<b>対照</b>で、既定は常に <c>true</c>。
/// 診断の対照2だけが <c>false</c> を渡す——差が小さければ、
/// <b>この駒は「害の中に見返りを埋めた」のではなく単なるバッファー</b>である。</para>
/// </summary>
public readonly record struct GoadRule(int Boost, bool Mark)
{
    /// <summary>見返りの大きさだけを指定する。代金（標）は払う＝通常の実行。</summary>
    public GoadRule(int boost) : this(boost, true) { }

    /// <summary>探索段階の初期値（第52期）。</summary>
    public static GoadRule Default => new(4, true);
}

/// <summary>
/// 止め。<b>ロスターで初めて「敵に付いた標」を読む駒</b>（第53期）。
///
/// <para><b>空白は敵側にあった。</b> 標（<see cref="StatusKeys.Marked"/>）の書き手は
/// 第52期に3枚になった（囃し立て＝味方／逸らし＝自分と敵／駆り立て＝味方）のに、
/// <b>駒の読み手は仇討ち（<see cref="AvengeTrait"/>）1枚だけ</b>で、しかもあちらが読むのは
/// <b>味方</b>の標。<b>第50期にソラが敵へ標を付けられるようになったのに、それを読む駒がいなかった。</b></para>
///
/// <para><b>標には engine の鎖の中で特権がある</b>（第50期 Phase 0-2）。
/// <c>SelectTargetChain</c> の標の段は <c>pool</c> ではなく <c>foes</c> から選ぶので、
/// <b>標だけが「前列が生きている限り後列は狙われない」という盤面の中核規則を破る。</b>
/// しかも鎖の1段目なので<b>庇い・後備え・殉教・棘守りをすべて飛び越す。</b>
/// <b>この駒の価値は倍率ではなく、その経路を確実に使えることかもしれない</b>
/// ——診断は「発火」と「列越え」を必ず分けて数える。</para>
///
/// <para><b>ザンとは「同じ通貨を、逆の陣営で、逆の手番の持ち方で」読む。</b>
/// 仇討ちは <c>CanActOutOfTurn</c> を通る<b>ターン外</b>の駒なので粛（第二波）に封じられるが、
/// <b>止めは自分の手番の <see cref="BattleContext.PerformAttack"/> の中でしか働かないので
/// 粛の非対象</b>（第53期 Phase 0-4）。第51期の「効いているのは窓口ではなく手番の持ち方」に従う。</para>
///
/// <para><b>1つの動作の表と裏</b>（置き去り・責め苦・仇討ち・突き返し・鱗・逸らし・駆り立てと同型）。
/// 3つが1つの動作から出る:</para>
/// <list type="number">
///   <item><b>対象の強制</b>（プラスとマイナスの両方）: 標を持つ生存中の敵がいれば<b>必ずそれを狙う</b>。
///     複数いれば<b>現在HPが最も高い1体</b>（同値のみ <see cref="BattleContext.PickOne"/>）。
///     <b>engine の 75% を 100% にし、選び方を決定的にする</b>だけで、窓口は増やしていない
///     ——実装は <c>SelectTargetChain</c> の標の段（<see cref="Preferred"/> を呼ぶ1行）。
///     <b>倒しきれない相手に食らいつくことがある</b>のがマイナス側。</item>
///   <item><b>倍率</b>（プラス）: 標を持つ敵を殴るとき攻撃力が <see cref="FinisherRule.Multiplier"/> 倍。
///     <b><see cref="Trait.ModifyAttack"/> では書けない</b>——あちらは対象を受け取らないので
///     「相手が標を持つか」で分岐できない。<b>攻撃の解決時</b>（<c>PerformAttack</c> が
///     <c>atk</c> を作った直後）に掛ける。<b>標を持たない敵を殴るときは素の攻12。</b></item>
///   <item><b>消費</b>（マイナス）: 殴った後、その敵の標を 0 にする。
///     <b>敵の標を消す初めての経路</b>（ソラ・カリは味方からしか外さない）。
///     消すと engine の <c>MarkPullPercent</c> も切れるので、
///     <b>味方全体の集中砲火を自分が終わらせてしまう。</b></item>
/// </list>
///
/// <para><b>代金を軽くする細工はしていない。</b> 消費を止めたり、標が無いときも倍率を乗せたりすると、
/// 供給とのサイクルが消えて単なる高打点の駒になる。<b>供給はソラ1枚しかない</b>
/// （第47期のウロ＝砕け1枚と同じ形）ので、<b>ソラ抜きでは素の攻12として振る舞う。</b></para>
///
/// <para><b>消費は <see cref="OnAfterAttack"/>（駒側）に置き、倍率と対象の強制は engine に置いた。</b>
/// 前者は駒ごとのフックで書けるが、後者2つは書けない（<c>ModifyAttack</c> は対象を知らず、
/// 標的選択には Trait のフックが無い）——<b>engine に窓口があるのは「駒ごとのフックでは
/// 書けない機構」だけ</b>という既存の作法に従う。</para>
/// </summary>
public sealed class FinisherTrait : Trait
{
    public override TraitId Id => TraitId.Finisher;

    /// <summary>
    /// 狙う相手。<b>標を持つ生存中の敵のうち現在HPが最も高い1体</b>（同値のみ <c>PickOne</c>）。
    /// <b>候補は <c>pool</c> ではなく <c>foes</c></b>——標の段そのものの候補集合をそのまま使う
    /// （ここを <c>pool</c> にすると列越えが消えて、この駒の主眼が測れなくなる）。
    ///
    /// <para><b>候補 0 個・1 個では <c>Roll</c> を消費しない</b>（<c>PickOne</c> の性質）ので、
    /// 「標持ちが1体だけ」という通常の局面では乱数列が動かない。</para>
    /// </summary>
    public static UnitState? Preferred(BattleContext ctx, List<UnitState> foes)
    {
        List<UnitState> marked = foes.Where(f => f.Counter(StatusKeys.Marked) > 0).ToList();
        if (marked.Count == 0) return null;
        int top = marked.Max(f => f.Hp);
        return ctx.PickOne(marked.Where(f => f.Hp == top).ToList());
    }

    /// <summary>
    /// 殴った相手の標を消す。<b>敵側だけ</b>（味方の標はソラ・カリの領分）。
    ///
    /// <para><b>死んでいても消す。</b> 「仕留めると指差しは消える」は結果で解決する
    /// ——生死で例外を作ると、倒し切れなかったときだけ標が残る非対称ができる。</para>
    ///
    /// <para><c>FinisherRule.Consume</c> が偽なら消さない。<b>これはノブではなく対照</b>
    /// （§4 の対照2）で、既定は常に <c>true</c>。</para>
    /// </summary>
    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        if (target.TeamId == self.TeamId) return;
        if (target.Counter(StatusKeys.Marked) <= 0) return;

        ctx.NoteFinisherOutcome(target, !target.IsAlive);
        if (!ctx.Finisher.Consume) return;

        target.SetCounter(StatusKeys.Marked, 0);
        ctx.NoteFinisherConsume();
        ctx.Log($"    {self.Name} が {target.Name} を仕留めにかかり、指差しが消えた", LogKind.Trigger);
    }
}

/// <summary>
/// 止めの強度のノブ。<b>診断（finisher）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない（既定は <see cref="Default"/>）。
/// static のノブにしない理由は同型の doc（<see cref="ColossusRule"/>）を参照。
///
/// <para><paramref name="Multiplier"/> は標を持つ敵への倍率。<b>動かす量は打点1本だけ</b>
/// ——標の消費量も列越えの有無も <paramref name="Multiplier"/> に依存しないので、
/// 第50期の <see cref="DivertRule.TargetCount"/> のような打ち消し（総量と取り分が逆を向く）は
/// 構造上起きない。第52期の基準（<b>ノブが動かす量を1本にする</b>）に従って選んだ。</para>
///
/// <para><paramref name="Consume"/> は<b>ノブではない</b>。「標を倍で殴る」効果と
/// 「味方の集中砲火を自分で止める」代金を分離するための<b>対照</b>で、既定は常に <c>true</c>。
/// 診断の対照2だけが <c>false</c> を渡す——<b>差が小さければサイクルは代金として働いていない</b>
/// （この駒は「標を倍で殴るだけ」の駒である）。</para>
///
/// <para><b>既定を無効にしなくてよい。</b> 味方側の駒なので、<see cref="UnitCatalog.Tome"/> を
/// 編成に入れない限り既存の行は1バイトも動かない（それ自体が回帰チェックになる）。</para>
/// </summary>
public readonly record struct FinisherRule(int Multiplier, bool Consume)
{
    /// <summary>倍率だけを指定する。代金（標の消費）は払う＝通常の実行。</summary>
    public FinisherRule(int multiplier) : this(multiplier, true) { }

    /// <summary>探索段階の初期値（第53期）。</summary>
    public static FinisherRule Default => new(2, true);
}

/// <summary>
/// 火選り。<b>燃えている味方の腕を上げ、自分の隣で燃えていない味方の腕を鈍らせる</b>（第58期）。
///
/// <para><b>出発点は第57期の実測</b>——燃焼は<b>盤面のどこにも繋がっていない閉じた2枚組</b>だった
/// （表E の 18 セル＝9通貨 × 双方向がすべて 0。「燃えている」という事実を読んで分岐するのは
/// engine の刻み1本と熾火＝<see cref="PyreTrait"/> 1枚の計2本で、engine の1本は燃焼そのものの実装）。
/// しかも<b>着火の 52% は味方に付いていて</b>（味 2.40 対 敵 2.21 /戦）、
/// <b>その味方側の燃焼を読む駒が1枚も無い</b>。<b>この駒はその在庫を読む2枚目の読み手である。</b></para>
///
/// <para><b>1つの動作の表と裏</b>（置き去り・責め苦・仇討ち・突き返し・鱗・逸らし・駆り立てと同型）。
/// <see cref="OnTurnStart"/> の1回の発火で以下を順に行う:</para>
/// <list type="number">
///   <item><b>プラス</b>: <b>燃えている味方全員</b>（自分を除く）に
///     <see cref="BattleContext.Whet"/>(<see cref="FavorRule.Gain"/>, <see cref="WhetRoute.Favor"/>)。
///     <b>位置を問わない。</b></item>
///   <item><b>マイナス</b>: <b>隣接する味方のうち燃えていない者</b>に
///     <see cref="BattleContext.Dull"/>(<see cref="FavorRule.Loss"/>, <see cref="DullRoute.Favor"/>)。
///     <b>位置で決まる。</b></item>
/// </list>
///
/// <para><b>プラスを全体・マイナスを隣接にするのが要点。</b> 逆にすると
/// 「隣に火があるかどうか」だけの二値になり、配置の判断が消える（第45期の
/// 「隣に何人いるか」を読む機構が2値の選好しか返さなかったのと同じ穴）。この形なら
/// <b>「火を全体に回すか、自分の隣を空けるか」</b>という2つの解き方が同時に立つ。</para>
///
/// <para><b>候補は両側とも自前で <see cref="UnitState.AcceptsSupport"/> 濾しする（隣へ漏らさない）。</b>
/// <see cref="BattleContext.SupportTargets"/> は支援拒否の駒（ガルドの <c>Stoic</c>）の取り分を
/// 隣へ流すが、<b>流した先が燃えているとは限らない</b>——「燃えている味方を強化する」という
/// 規則そのものが破れるので採らない。<b>プラスとマイナスで濾し方を分けない</b>のは
/// 駆り立て（<see cref="GoadTrait"/>）と同じ作法で、分けると
/// <b>「強化されないのに鈍りもしない」駒</b>ができて「隣をガルドで固める」が
/// 代金だけを消す配置解になる。</para>
///
/// <para><b>この駒は構造的に供給の1ターン後ろを歩く。</b> ターンの順序は
/// <c>TickStatuses</c> → <c>OnTurnStart</c> → 行動順ループで、火の粉は <c>OnAfterAttack</c>
/// ——つまり<b>第1ターンの発火時点では盤上の誰も燃えていない</b>（実測でホタの
/// <c>CurrentAttack</c> は T1 が 6・T2 以降が 24）。<b>係数では詰められない構造の遅れ</b>で、
/// 短い波ほど取り分が減る。</para>
///
/// <para><b>第60期に手番へ降ろして、この遅れを畳んだ。</b> ヒヨ（速6）はボルグ（速8）より
/// 遅いので、自分の番が回る時点で火は既に点いている。実測で<b>弱体の受け手から
/// 熾のホタ 2.00 量/戦がちょうど消え</b>、<c>撒いた</c>（弱体の総量）は4行とも
/// <b>−1.9〜−4.2 量/戦</b>下がった。<b>ただし遅れは消えたのではなく形が変わっている</b>
/// ——ホタ（速7）はヒヨより<b>速い</b>ので、配った強化がホタの振りに乗るのは次のターンから。
/// <b>代金の相手が「第1ターンの全員」から「自分より速い受け手」へ移った</b>（第60期 Q1）。</para>
///
/// <para><b>熾火（<see cref="PyreTrait"/>）に配ると 4 倍で入る。</b>
/// <see cref="UnitState.CurrentAttack"/> は <c>Def.Attack + AtkBonus</c> を作ってから
/// <c>ModifyAttack</c> を通すので、<b>強化は素の攻撃力と一緒に掛けられる</b>
/// ——ロスター唯一の乗算フラグで、しかも同時に貫きへ変わるので与ダメの実効は 7.4 倍
/// （第58期 Phase 0-1 の実測）。<b>これは仕様として許した</b>——理由は
/// design/PHASE58_KINDLE.md の Q4。</para>
///
/// <para><b>粛（<see cref="HushTrait"/>）に封じられない。</b> 手番へ降ろした後も同じで、
/// <b>行動順ループは <c>CanActOutOfTurn</c> を1度も呼ばない</b>——呼び出し元は特性側の4本
/// （棘・仇討ち・軋み・追い打ち）だけ。<c>OnTurnStart</c> はループの<b>外側</b>なので、
/// こちらも通らない。<b>実測でも移設の前後で第2波は1セルも動いていない</b>（第60期 P5）。</para>
///
/// <para><b>自分が燃えていても自分は強化しない</b>（<c>a != self</c>）。自己完結させると
/// 「火の粉のそばに置く」以外の判断が消える。マイナス側は隣接の定義（<c>a != b</c>）から
/// 自然に自分を含まない。</para>
///
/// <para><b>召喚枠（<see cref="TraitId.Ephemeral"/>）も対象に含める</b>（鱗・第47期と同じ扱い）。
/// 胞子は <c>AcceptsSupport</c> を通るので、燃えていれば強化を受けるし、
/// ヒヨの隣（○中1 / ○中3 / ○前2 / ○後2 は編成枠と隣接する）にいて燃えていなければ鈍る。
/// <b>掛け算にはならない</b>——胞子は火を撒かないので供給は増えない。</para>
/// </summary>
public sealed class FavorTrait : Trait
{
    public override TraitId Id => TraitId.Favor;

    // **手番の行動として撃つ**（第60期）。第58期は `OnTurnStart` に置いたが、ターンの順序が
    // `TickStatuses` → `OnTurnStart` → 行動順ループなので、火の粉（`OnAfterAttack`）に対して
    // **構造的に1ターン遅れる**——第1ターンの発火時点では盤上の誰も燃えておらず、
    // 強化するはずの相手（熾のホタ）をそのターンだけ鈍らせていた（第58期 9-2）。
    // 手番へ降ろすと、ヒヨ（速6）の番が回る時点でボルグ（速8）が既に火を撒いている。
    //
    // **`ActsOnPattern` の分岐は保持者が1枚でも残す。** 継ぎ当て（`MenderTrait`）が
    // 記録している事故（味方のノノと敵の従軍司祭長が同じ特性を共有していて、無条件に移すと
    // 司祭長の回復だけが静かに消えた）と同じ形が、後で敵側にこの特性を配ったときに再発する。
    public override void OnAction(BattleContext ctx, UnitState self, UnitAction action)
        => Apply(ctx, self);

    // 行動パターンを持たない保持者は従来どおりターン頭に発火する。理由は Trait.ActsOnPattern。
    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (!ActsOnPattern(self)) Apply(ctx, self);
    }

    private static void Apply(BattleContext ctx, UnitState self)
    {
        if (!self.IsAlive) return;

        // **LivingMembers（スロット昇順・決定的）を使う。** LivingMembersShuffled は Shuffle が
        // 乱数を消費するので、効果が順序に依存しないこの機構で使うと
        // 「Gain = Loss = 0 の版が素体と1セルも違わない」という検算が壊れる。
        var allies = ctx.LivingMembers(self.TeamId);

        int gain = ctx.Favor.Gain, loss = ctx.Favor.Loss;
        int whetted = 0, dulled = 0;

        foreach (UnitState a in allies)
        {
            // LivingMembers はスナップショットなので、渡しの転嫁（自傷）で列挙中に
            // 誰かが落ちうる。**死体に配らない。**
            if (a == self || !a.IsAlive || !a.AcceptsSupport) continue;

            if (a.Counter(StatusKeys.Burn) > 0)
            {
                // プラス側は位置を問わない。
                whetted++;
                ctx.Whet(a, gain, WhetRoute.Favor);          // gain <= 0 なら Whet が即 return する
            }
            else if (FormationRules.AreAdjacent(self.Slot, a.Slot))
            {
                // マイナス側は隣接だけ。
                dulled++;
                ctx.Dull(a, loss, DullRoute.Favor);          // loss <= 0 なら Dull が即 return する
            }
        }

        // 空振り＝盤上に燃えている味方が1体もいなかった手番。**第1ターンは構造的にここへ落ちる。**
        ctx.NoteFavor(whetted, dulled, whetted == 0 ? 1 : 0, gain * whetted, loss * dulled);

        if (whetted > 0 || dulled > 0)
            ctx.Log($"    {self.Name} が火のそばを贔屓した（燃 {whetted} 体に +{gain} / 隣の非燃 {dulled} 体に -{loss}）",
                    LogKind.FriendlyFire);
    }
}

/// <summary>
/// 火選りの強度のノブ。<b>診断（favor）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない（既定は <see cref="Default"/>）。
/// static のノブにしない理由は同型の doc（<see cref="ColossusRule"/>）を参照。
///
/// <para><paramref name="Gain"/> がプラス側（燃えている味方全員への強化量）、
/// <paramref name="Loss"/> がマイナス側（隣接する非燃焼の味方への弱体量）。
/// <b>2つを別のノブにしてある</b>のは、掃引で
/// 「プラスを厚く」と「マイナスを厚く」を分けて測るため（指示書 §4 の V2 / V3）。</para>
///
/// <para><b><c>new FavorRule(0, 0)</c> は機構を完全に止める</b>
/// ——<see cref="BattleContext.Whet"/> / <see cref="BattleContext.Dull"/> は
/// <c>amount &lt;= 0</c> で即 return し、<see cref="FavorTrait"/> は乱数を1つも引かないので、
/// <b>同数値・特性なしの素体に差し替えた版と1セルも違わない</b>。これが診断の検算になる。</para>
/// </summary>
public readonly record struct FavorRule(int Gain, int Loss)
{
    /// <summary>
    /// 採用値（第58期）。<b>探索段階の初期値は指示書 §2-1 の <c>(2, 2)</c>（掃引の V1）だったが、
    /// 掃引で <c>(4, 2)</c>（V2）を採った</b>——V1 は3行とも素体より下（−3.5 / −2.6 / −1.8pt）で、
    /// V2 だけが2行で素体を上回る（+2.1 / +1.5 / −1.6pt）。
    /// <b>見返りの側だけを厚くすると通る</b>のは、代金（<see cref="Loss"/>）が
    /// <b>供給者と第1ターンの味方</b>という「構造的に非燃焼の相手」に落ちるからで、
    /// そこは <see cref="Gain"/> をいくら上げても減らない（掃引で `撒いた` が <see cref="Gain"/> に
    /// 対して完全に不動）。第25期の軛（計画 15 / 手当 20 → 実測で 25）と同じ形の採り方。
    /// </summary>
    public static FavorRule Default => new(4, 2);
}

/// <summary>
/// 横流し。<b>自分と隣の味方に来た強化を、自分の隣で一番遅い味方へすべて回す</b>（第62期）。
/// 自分も横取りされた側も育たない——<b>プラスとマイナスが1つの動作の表と裏</b>なので、
/// <see cref="TraitId"/> のどちらのブロックにも入らない
/// （置き去り・突き返し・引き受け・渡し・鱗・逸らし・駆り立て・止め・火選りと同じ扱い）。
///
/// <para><b>出発点は第56期の積み残し1。</b> 強化の供給の <b>47.1%</b> は吐き戻し1本で、
/// 行き先は「ゴルムが庇った相手」＝<b>編成が選んでいない</b>。号令は味方全体、縛めは縛った相手、
/// 火選りは燃えている相手——<b>いずれも行き先は機構が決める</b>。
/// <b>この駒は量を1点も増やさず、行き先だけを編成の判断に変える。</b></para>
///
/// <para><b>実装は <see cref="BattleContext.Whet"/> の中にある。</b> 第56期が
/// <c>receiver</c> の位置に空けておいた席で、<see cref="BattleContext.Dull"/> の
/// 集約（<see cref="BearTrait"/>）・転嫁（<see cref="RelayTrait"/>）と<b>同じ位置・同じ形</b>
/// ——「強化が入る<b>直前</b>に横取りする」は駒ごとのフックでは書けない。
/// <b>engine に新しい窓口は1つも足していない。</b> この Trait 本体は札にすぎない。</para>
///
/// <para><b>選択子を「一番遅い」にした理由:</b></para>
/// <list type="bullet">
///   <item>駆り立て（<see cref="GoadTrait"/>）の「隣接する <c>CurrentAttack</c> 最大」の<b>逆側</b>なので重ならない</item>
///   <item>遅い駒は手番が後ろなので、そのターンに配られた強化を<b>振る前に受け取れる</b>
///     （第60期の「移設で代金の相手が『自分より速い受け手』へ移った」の裏返し）</item>
///   <item><b>罠が盤上に既にある</b>——一番遅い隣が<b>不動のカド</b>なら 100% 死蔵
///     （第62期 Phase 0-1 の実測でカドは受けた強化の 100.0% を死蔵している）、
///     <b>ゴルム（速3）</b>なら吐き戻しの出どころへ戻る自己循環、<b>ガルド</b>は
///     <c>Stoic</c> で候補にすら入らず、<b>ホタ</b>なら燃焼中ちょうど 4 倍。
///     <b>同じ1点が行き先で 0 倍にも 4 倍にもなる。</b></item>
/// </list>
///
/// <para><b>1ホップで止める。</b> 回した先がさらに回さないのは、宛先の候補から
/// 横流し役そのものを除いてあるため（呪いの伝播を作らないのと同じ形）。
/// <b>量は加算のまま</b>——減衰も倍率も付けない。「行き先が本体」という主題を
/// 係数で薄めないための判断で、<b>この駒には強度のノブが無い</b>
/// （振るのは選択子だけ＝<see cref="FunnelRule.Slowest"/>）。</para>
///
/// <para><b>候補は自前で <see cref="UnitState.AcceptsSupport"/> 濾しする（隣へ漏らさない）。</b>
/// 駆り立て・縛め・移り木・火選りと同じ側で、<see cref="BattleContext.SupportTargets"/> は
/// 通さない——<b>漏らした先が「一番遅い隣」とは限らず、規則そのものが破れる</b>
/// （第58期の火選りが「流した先が燃えているとは限らない」で同じ判断をしている）。
/// 受け取れない相手（ガルドの <c>Stoic</c>）は候補から外れ、<b>次に遅い隣</b>が宛先になる。</para>
///
/// <para><b>自分に来た強化も回す。</b> 「自分は育たない」がマイナスの本体なので、
/// 横流し役が <c>Whet</c> の対象になったときも同じ経路で宛先を差し替える。
/// <b>宛先が元の対象と同じなら何もしない</b>（<c>dest != target</c>）ので、
/// 「隣に自分しかいない」状況で強化が消えることはない。</para>
///
/// <para><b>候補が 0 / 1 個では <c>Roll</c> を消費しない</b>（<see cref="BattleContext.PickOne"/>）。
/// <b>横流し役が1枚も盤上にいない行では乱数列が1ビットも動かない</b>——
/// これが第43期（渡し）と同じ受け入れ基準で、<c>compare</c> の 305 セルが 0 件であることで検算する。</para>
///
/// <para><b>召喚枠（スロット5〜8）も隣接表に載るので候補に入る</b>（鱗・火選りと同じ扱い）。
/// 胞子は速10 なので「一番遅い」側には来ないが、<b>速い側（<see cref="FunnelRule.Slowest"/> = false）
/// では宛先になりうる</b>。</para>
/// </summary>
public sealed class FunnelTrait : Trait
{
    public override TraitId Id => TraitId.Funnel;
}

/// <summary>
/// 横流しの<b>選択子</b>。<b>診断（funnel）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない（既定は <see cref="Default"/>）。
/// static のノブにしない理由は同型の doc（<see cref="ColossusRule"/>）を参照。
///
/// <para><b>強度のノブではない。</b> 横流しは量を1点も増減させないので、
/// 振れるのは<b>宛先の選び方</b>と<b>何を流すか</b>だけ。</para>
///
/// <para><paramref name="Slowest"/> が <c>true</c> なら隣で<b>一番遅い</b>味方（V1・本命）、
/// <c>false</c> なら隣で<b>一番速い</b>味方（V2・対照）。第62期の実測で
/// <b>選択子の向きは本体ではなかった</b>（V1 → V2 で +1.8→−0.4 / +12.2→−3.4 / <b>+8.9→+10.4</b>
/// と行ごとに符号が違う）——値段は<b>その端に誰が立っているか</b>で決まる。
/// <b>第63期以降は <c>true</c> で固定</b>（フレーバー「遅さで捨てられた層に力を回す」で残す）。</para>
///
/// <para><paramref name="Both"/> が <c>true</c> なら<b>弱体も同じ宛先へ流す</b>（V3・第63期）。
/// 規則が「隣で起きる攻撃力の上げ下げを、全部いちばん遅い隣に押し付ける」と対称になり、
/// 1文のまま表と裏になる。<b>既定は <c>false</c>（強化だけ＝ V1）。</b>
/// <c>false</c> のとき <see cref="BattleContext.Dull"/> の分岐は
/// <b>候補プールの述語ごと元の形に畳まれる</b>ので、既存の行は1バイトも動かない。</para>
///
/// <para><b>機構を 0 にするノブは置いていない。</b> 「量を 0 にする」ができない機構なので、
/// 対照は<b>同数値・特性なしの素体</b>（<c>NukiPlain</c>・診断のローカルの <c>UnitDef</c>）で取る
/// ——第47期の鱗が「その効果だけを 0 にできるノブが作れない機構では素体を対照に置く」で
/// 確立した形。<b>規則にノブを増やして機構を殺すと、供給も発揮も止まらないまま
/// 「体が入っただけか」が割れない。</b></para>
///
/// <para><b>既定を無効にしなくてよい。</b> 味方側の駒なので、<c>UnitCatalog.Nuki</c> を
/// 編成に入れない限り既存61行は1バイトも動かない（それ自体が回帰チェックになる）。</para>
/// </summary>
public readonly record struct FunnelRule(bool Slowest, bool Both)
{
    /// <summary>選択子だけを指定する（強化だけを流す＝ V1）。</summary>
    public FunnelRule(bool slowest) : this(slowest, false) { }

    /// <summary>本命（V1）＝隣で一番遅い味方へ、<b>強化だけ</b>を回す。</summary>
    public static FunnelRule Default => new(true, false);
}






/// <summary>澱み。既に積まれた毒を増幅する。毒が無ければ何もしない。</summary>
public sealed class AmplifierTrait : Trait
{
    public const int Step = 4;

    /// <summary>
    /// 傷口の着火で置く層（第87期・<see cref="IgniteRule"/>）。<b>定数 1。傷の数に比例させない。</b>
    /// </summary>
    public const int IgniteAmount = 1;

    /// <summary>
    /// 「傷を持ち毒を持たない敵」を一度でも見た印（計数専用の私有キー）。
    /// <b>版に依らず立てる</b>——実体数（<see cref="UnitTally.AmpIgnitableBodies"/>）を
    /// <see cref="IgniteRule"/> を切ったままでも数えるため。<c>StatusKeys</c> ではないので帳簿にも載らない。
    /// </summary>
    public const string SeenKey = "ampSeen";

    /// <summary>
    /// 着火された印（計数専用の私有キー）。<see cref="BattleContext.TickStatuses"/> が、
    /// この駒の以後の毒の刻みを<b>着火の下流</b>として数えるために読む（持続係数の分子）。
    /// <b>盤面の判断には一切使わない。</b>
    /// </summary>
    public const string IgnitedKey = "ampIgnited";

    public override TraitId Id => TraitId.Amplifier;

    // 手番の行動として撃つ（第11期 Phase BB）。毒の判定（TickStatuses）との前後は
    // 変わらない——tick はターン頭、濃縮はその後ろ、という関係は移す前と同じ。
    // 変わるのは**同じターンに味方が積んだ毒まで拾えるようになる**ことで、
    // 毒のダメージがターンごとずれるわけではない。
    public override void OnAction(BattleContext ctx, UnitState self, UnitAction action) => Thicken(ctx, self);

    // 保持者はいま澱みのミオだけだが、Mender と同じ形に揃えておく（Trait.ActsOnPattern）。
    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (!ActsOnPattern(self)) Thicken(ctx, self);
    }

    private static void Thicken(BattleContext ctx, UnitState self)
    {
        // 計数（第87期）。**盤面には一切影響しない。**
        UnitTally at = ctx.TallyOf(self);
        at.AmpFires++;

        foreach (UnitState foe in ctx.LivingMembers(ctx.Opponent(self.TeamId)))
        {
            int poison = foe.Counter(StatusKeys.Poison);
            if (poison <= 0)
            {
                // 傷口の着火（第87期・IgniteRule）。**観測は版に依らず取る**——
                // 「着火できる敵が何体いたか」は Y0（規則を切ったまま）でも数えられる（§1-2）。
                int w = foe.Counter(StatusKeys.Wound);
                if (w <= 0) continue;

                at.AmpIgnitable++;
                if (at.AmpFirstIgnitableTurn == 0) at.AmpFirstIgnitableTurn = Math.Max(1, ctx.Turn);
                if (foe.Counter(SeenKey) == 0) { foe.SetCounter(SeenKey, 1); at.AmpIgnitableBodies++; }

                if (!ctx.WoundIgnite.Enabled) continue;

                foe.SetCounter(StatusKeys.Poison, IgniteAmount);
                foe.SetCounter(IgnitedKey, 1);
                at.AmpIgnited++;
                at.AmpIgniteAmount += IgniteAmount;
                at.AmpIgniteWoundBefore += w;
                at.AmpIgniteWoundAfter += foe.Counter(StatusKeys.Wound);       // 傷は消費しない（自己検査 (g)）
                at.AmpIgnitePoisonAfter += foe.Counter(StatusKeys.Poison);     // 1 のはず（自己検査 (e)）
                if (at.AmpFirstIgniteTurn == 0) at.AmpFirstIgniteTurn = Math.Max(1, ctx.Turn);
                ctx.Log($"    {foe.Name} の傷口に澱みが流れ込む（傷 {w} → 毒 {IgniteAmount}）", LogKind.Status);
                continue;   // ★ 着火したターンは濃くしない
            }

            // 加算にすること。乗算だと戦闘が長引くほど指数的に伸びて、
            // 後から数値で抑えるのが不可能になる。
            int grown = poison + Step;
            foe.SetCounter(StatusKeys.Poison, grown);
            at.AmpThickened++;
            ctx.Log($"    {foe.Name} の毒が澱んで濃くなった（{poison} → {grown}）", LogKind.Status);
        }
    }
}

/// <summary>
/// 疫み。毒に侵された駒が倒れると、残った敵へ毒が飛ぶ。**敵味方どちらの死骸からでも飛ぶ。**
///
/// もとは敵の死でしか発火せず、第三波以降は一度も発火していなかった。瘴気（グザ）は全体へ
/// 均等に撒くので敵はほぼ同時に瀕死になり、最初の一体が落ちる前に味方が全滅していたため。
/// 発火条件のほうが噛み合っていなかった。
///
/// 味方の死でも飛ぶようにすると、**これまで最悪の出来事だった味方の全滅が拡散の起点に変わる。**
/// 第五波は3ターン目に味方が一斉に落ちる波なので、そこが一番効く（第5波 6% → 34%）。
/// 死の密度が高い編成ほど働くので、墓守（リィカ）や破裂（ゾト）と直接つながる
/// （毒×死の連鎖の第2波 18% → 49%）。「死体を運ばせると必ず疫病が出る」という由来にも合う。
///
/// 却下: 拡散先を敵味方の区別なしにする案。拡散量は死んだ駒の層の半分で、終盤の敵は
/// 1体あたり20〜60層まで積む。その半分がミオ(HP42)やラウ(HP50)に乗ると、毒ダメージは
/// 層の数そのものなので1〜2ティックで溶ける（毒軸の第2波 90% → 14%）。
/// 反対側だけ 1/8 に落とすと今度はほぼ無害になり(82%)、痛いか無害かの二択にしかならなかった。
///
/// 却下: 隣接する味方に毒を移す案（毎ターン+1 / 拡散時に同量）。どちらも毒軸を素直に下げる
/// だけだが（第4波 84% → 60% / 26%）、澱み喰い（ヴィオ）がいる編成では逆に上がる（+7pt）。
/// ヴィオはターン開始時に味方の毒を全部吸い上げるので、マイナスが燃料に反転する。
/// 形としては正しいが量が大きすぎ、「ヴィオを入れるかどうか」の二択になって判断にならない。
/// </summary>
public sealed class ContagionTrait : Trait
{
    public override TraitId Id => TraitId.Contagion;

    public override void OnAnyDeath(BattleContext ctx, UnitState self, UnitState dead)
    {
        int carried = dead.Counter(StatusKeys.Poison);
        if (carried <= 0) return;

        // 撒く先は常に敵側。味方の死骸から飛ぶときも、疫病が向かうのは敵。
        int spread = Math.Max(1, carried / 2);
        foreach (UnitState foe in ctx.LivingMembers(ctx.Opponent(self.TeamId)))
            ctx.Poison(foe, spread, self, PoisonRoute.Contagion);   // 毒の窓口（第90期）

        ctx.Log($"    {dead.Name} の死骸から毒が撒き散らされた（+{spread}）", LogKind.Highlight);
    }
}

/// <summary>
/// 瘴気。毎ターン敵全体へ薄く毒を撒く。1体ずつ積む毒撃では立ち上がりが遅すぎるので、
/// 毒軸が決着に間に合うかどうかはこの駒が握っている。
/// </summary>
public sealed class MiasmaTrait : Trait
{
    public const int PerTurn = 2;
    public const int AllyLeak = 1;

    public override TraitId Id => TraitId.Miasma;

    // **手番の行動として撒く**（第61期）。ターンの順序は
    // `TickStatuses` → `OnTurnStart`（席順の昇順）→ 行動順ループなので、
    // `OnTurnStart` に置くと**同じターン頭に発火する他の駒との前後が席順で決まる**。
    // 一番大きいのは澱み喰い（`BlightfedTrait`・ヴィオ）で、グザの席がヴィオより前なら
    // 撒いた毒はその場で吸い上げられて**味方は1点も払わない**——
    // 「毒の代金を誰が払うか」が席の左右で切り替わる隠れた判断になっていた。
    // 手番へ降ろすとヴィオが必ず先になり、味方は毎ターン必ず1回刻まれる。
    //
    // **`ActsOnPattern` の分岐は保持者が1枚でも残す**（`Trait.ActsOnPattern`）。
    // 継ぎ当て（`MenderTrait`）が記録している事故——同じ特性を敵側の駒が共有していて、
    // 無条件に移すとそちらの効果だけが静かに消える——と同じ形が後で再発する。
    public override void OnAction(BattleContext ctx, UnitState self, UnitAction action)
        => Spread(ctx, self);

    // 行動パターンを持たない保持者は従来どおりターン頭に発火する。理由は Trait.ActsOnPattern。
    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (!ActsOnPattern(self)) Spread(ctx, self);
    }

    private static void Spread(BattleContext ctx, UnitState self)
    {
        if (!self.IsAlive) return;

        var foes = ctx.LivingMembers(ctx.Opponent(self.TeamId));
        if (foes.Count == 0) return;

        foreach (UnitState foe in foes)
            ctx.Poison(foe, PerTurn, self, PoisonRoute.Miasma);      // 毒の窓口（第90期）

        // 瘴気は敵味方を選ばない。撒く側にも代償を負わせる。
        // **味方漏れも同じ窓口を通す**——両陣営に等しくかかるのが滲み則の要点（第90期 §2-2）。
        var allies = ctx.LivingMembers(self.TeamId);
        foreach (UnitState ally in allies)
            ctx.Poison(ally, AllyLeak, self, PoisonRoute.MiasmaLeak);

        // 第61期の計数。**盤面には触らない。**
        ctx.NoteMiasma(foes.Count * PerTurn, allies.Count * AllyLeak);

        ctx.Log($"    {self.Name} が瘴気を撒いた（敵 毒 +{PerTurn} / 味方 毒 +{AllyLeak}）", LogKind.FriendlyFire);
    }
}

/// <summary>
/// 惨禍。<b>本人以外の</b>味方全体の被ダメージが増える。自分だけが損をするマイナスは編成の幅を
/// 生まないが、味方全体に及ぶマイナスは「被弾を利益に変える駒」すべての燃料になる。
///
/// <para><b>なぜ本人を除くか。</b> 当初は本人にも乗せていたが、それは意図（他人に及ぶこと）
/// ではなく本人が代金を払う形になっていた——カドの実効HPは 96 ÷ 1.5 = 64。<c>life</c>（第19期）で、
/// <b>カドの寿命が波に依存せず 2.7〜3.6T で固定</b>されていることが分かった。決着ターンは
/// 2.66 → 4.95 と伸びるのに、カドの立ち会える時間だけが伸びない。<c>干渉/T</c> は落ちていない
/// （4.7〜9.3）ので、<b>反撃が出すぎているのではなく出す時間が無い</b>。反撃の回数制ではなく
/// ここを外した。「自分以外が死にやすくなる」形になり、部隊に入れてもらえないマイナス持ちという
/// フレーバーにも合う。</para>
///
/// <para>除外は <see cref="BattleEngine.ApplyDamage"/> 側で <c>u != target</c> と
/// <b>関係で</b>書いてある（駒の名前で書かない）。惨禍持ちが2体並べば互いに増幅し合うのが正しい
/// ——現ロスターの持ち主はカドのみで、敵側には無い。</para>
/// </summary>
public sealed class HavocTrait : Trait
{
    // 75 を試して戻した（2026-08-29）。狙いは「惨禍を燃料にする編成が得をする」傾斜
    // だったが、序列は**逆向きに**割れた——燃料型（ムドの被弾強化・リィカの死の連鎖）が
    // 下げ幅の1位と同率3位で、唯一のプラスは非燃料型（反撃 +1.0）。
    // **被弾を利益に変える駒は、被弾で死ぬ。** 変換器自身も味方なので、増えた代金を
    // 先に払う（死の連鎖のリィカは第五波の 落% 25.0→70.5 で、層を積む前に落ちる）。
    // 第二〜四波の天井も崩れず（第四波は8編成すべて 100.0% のまま）、総関与量は
    // 40通り中 37通りで低下＝傾斜ではなく縮小だった。60 も同じ方向の小さい版にしか
    // ならないので刻んでいない。全体被害を増やす向きのマイナスには上限がある。
    public const int Percent = 50;
    public override TraitId Id => TraitId.Havoc;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
        => ctx.Log($"  {self.Name} の周りでは傷が深くなる（味方全体 被ダメージ +{Percent}%）", LogKind.FriendlyFire);
}

/// <summary>痺れ。敵1体の行動を確率で封じる。毒を一方的に通す時間を作る。</summary>
public sealed class ParalyzeTrait : Trait
{
    public const int Chance = 45;

    public override TraitId Id => TraitId.Paralyze;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        if (!target.IsAlive) return;
        if (ctx.Roll(100) >= Chance) return;
        target.SetCounter(StatusKeys.Stun, 1);
        ctx.Log($"    {target.Name} の体が痺れて動かない", LogKind.Status);
    }
}

/// <summary>
/// 仇討ち。標的（Marked）に初めて付いた読み手。
///
/// **1つのルールの表と裏**: 標的にされた味方が殴られると、殴った相手へ割り込んで刺し返す。
/// 自分が殴られると怯んで（＝痺れて）次の手番を失う。
///
/// **マイナス側を痺れに乗せてあるのが要点。** これ1つで二つのことが同時に立つ:
/// (a) 飛んだ手番は IdleTurn になるので、号令（ガン）・据え（バン）が買い取る。
/// (b) 痺れている間は <see cref="BattleContext.CanActOutOfTurn"/> が閉じるので**反撃も止まる**
///     ——「ザン本人を殴れば黙る」という攻略の語彙が、追加のコードなしで敵側に立つ。
///
/// **「1ターンに1回」は撤去した（第26期の追補）。** あれは範囲攻撃で標的持ちが**複数**
/// 削れたときの多重発火を塞ぐ予防だったが、**標的の書き手はヒサ1体で付く標的は常に1つ**
/// ——塞いだ穴はロスター上どこにも存在しなかった。一方で実害は出ていた: 標的役には敵の
/// 単体攻撃が集中する（それが囃し立ての機能）ので、標的が1ターンに複数回殴られるのは
/// 例外ではなく常態で、その2発目以降を黙って捨てていた。
///
/// **再導入の条件**: 標的の書き手が2つ目以降現れたとき（味方側の新特性でも、敵側からの
/// 標的付与でも）、多重発火の前提が復活するのでそのとき改めて検討する。
///
/// 上限を外しても暴走はしない——連鎖は <see cref="BattleContext.InReaction"/> が止め、
/// 過熱の対抗手段は怯み（被弾 → 痺れ → CanActOutOfTurn 閉鎖）と断罪（第五波）が既に担う。
/// 残る増分は「標的が殴られた回数ぶん刺し返す」という当初設計そのもの。
///
/// 反撃量は**自分の攻撃力**。被ダメ量を参照する反射は棘（ThornsTrait）で不採用にした形で、
/// 敵の火力が低い波では何も起きず高い波では先に死ぬ、という挟み撃ちから抜けられない。
///
/// **破片が怯みを止める**（コード追加ゼロの創発）: 破片で受け切った被弾は
/// <see cref="OnDamaged"/> ごと走らないので、ヒビの破片を配られたザンは殴られても怯まない。
/// 破片に初めて実質的な読み手が付く。
///
/// 標的の引き寄せは単体攻撃にしか効かない（SelectTarget）ので、主戦場は単体攻撃の波になる。
/// </summary>
public sealed class AvengeTrait : Trait
{
    public override TraitId Id => TraitId.Avenge;

    public override void OnAllyDamaged(BattleContext ctx, UnitState self, UnitState ally,
                                       int dmg, UnitState? source)
    {
        if (ally == self) return;
        if (ally.Counter(StatusKeys.Marked) <= 0) return;               // 標的にされた味方だけ
        if (source is null || source.TeamId == self.TeamId) return;     // 味方の事故には出ない
        if (!source.IsAlive) return;
        if (ctx.InReaction) return;                                     // 反撃の連鎖を止める

        // 怯み（自傷の痺れ）はここで効く。棘・軋み・追い打ちと同じ門をくぐる。
        if (!ctx.CanActOutOfTurn(self)) return;

        ctx.Reaction(() =>
        {
            ctx.Log($"    {self.Name} が {ally.Name} の仇を討つ", LogKind.Trigger);
            ctx.NoteAttackRead(self);   // 攻撃力を出力に変換した（第64期・死蔵の判定）
            ctx.ApplyDamage(source, Math.Max(1, self.CurrentAttack), self);
        });
    }

    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        if (source is null || source.TeamId == self.TeamId) return;
        if (dmg <= 0) return;

        // 怯み。痺れに乗せているので、次の手番が飛ぶ（→ IdleTurn）だけでなく刺し返しも止まる。
        self.SetCounter(StatusKeys.Stun, 1);
        ctx.Log($"    {self.Name} は殴られて怯んだ", LogKind.FriendlyFire);
    }
}

/// <summary>
/// 責め苦。痺れ（Stun / IdleTurn）に読み手を付ける特性。
///
/// **1つのルールの表と裏として書く**（置き去り・盤面ルール駒と同型）:
/// 動きを封じられた敵を殴れば追い打ちが出る。動ける敵を殴れば、自分の動きが封じられる。
/// 供給役（トウの痺れ粉・クグの大縛り）がいなければ、1ターンおきにしか動けない駒になる。
/// それは仕様——差し出したターンは号令（ガン）・据え（バン）が買い取るので、
/// **マイナスの側も売り物になる**（可変コスト型）。
///
/// **判定は二重条件**（Stun 持ち または IdleTurn == 現在ターン）。片方だけでは足りない:
/// 痺れカウンタは**本人の手番が来た瞬間に消費される**（BattleEngine のターンループ）ので、
/// シガより速い敵はシガの手番にはもうカウンタを持っておらず、痕跡は IdleTurn に変わっている。
/// Stun だけを見ると「自分より遅い敵しか読めない」という速度順の罠が生まれ、
/// クグの大縛り（最速の敵を縛る）が一生読めない。二重条件なら、トウの粉（速11で撒く）も
/// クグの縛りも、シガの速さと無関係に全部読める。**原因ではなく結果で解決する**。
///
/// 追い打ちは倍率ではなく**加算**（同じ重さをもう1発）。倍率にすると強化を受けた瞬間に
/// 二乗で伸びる（README「増幅は必ず加算にする」と同じ穴）。
/// </summary>
public sealed class TormentTrait : Trait
{
    public override TraitId Id => TraitId.Torment;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        // 死んでいても判定は同じ（結果で解決。例外を作らない）。
        // 縛られた敵を殴り倒したなら追い打ちの条件は満たしている——出どころは既に消えているので
        // ApplyDamage は生存判定で自然に空振りするが、「動ける敵を殴った」側の自傷は正しく走る。
        bool bound = target.Counter(StatusKeys.Stun) > 0
                     || target.Counter(StatusKeys.IdleTurn) == ctx.Turn;

        if (bound)
        {
            // ApplyDamage の直呼びなので OnAfterAttack は再帰しない（追い打ちが追い打ちを呼ばない）。
            ctx.Log($"    {self.Name} が動けない {target.Name} に追い打ちを重ねる", LogKind.Highlight);
            ctx.NoteAttackRead(self);   // 攻撃力を出力に変換した（第64期・死蔵の判定）
            ctx.ApplyDamage(target, Math.Max(1, self.CurrentAttack), self);
            return;
        }

        // 動ける敵を殴ると怖気づく。痺れに乗せてあるので、次の手番が飛ぶ（→ IdleTurn → 号令・据え）
        // だけでなく、CanActOutOfTurn が閉じてターン外の行動も止まる。
        self.SetCounter(StatusKeys.Stun, 1);
        ctx.Log($"    {self.Name} は動ける {target.Name} に怖気づいた", LogKind.FriendlyFire);
    }
}

/// <summary>
/// 裂き。傷（<see cref="StatusKeys.Wound"/>）の供給源。物理側に初めて置いた「盤面に残る汚れ」。
///
/// **1つのルールの表と裏**（置き去り・責め苦・仇討ちと同型）: 刃が薄いから斬り口が残る。
/// 薄いから断てない。プラスとマイナスが同じ一文から出る。
///
/// **刻む数はダメージ量に依存しない。** ドルガの38もキリの1も等価に傷1。
/// 量に比例させた瞬間に「強い駒がもっと強くなる」乗算の道に入る（README「増幅は必ず加算にする」の
/// 物理版で、比例させるなら結局は与ダメージをもう一度読むだけになる）。
/// 「原因ではなく結果で解決する」——誰が刻んだ傷でも読み手は同じように読む。
///
/// <see cref="ModifyAttack"/> は**全経路で1に潰す**。反撃（棘）でも追い打ちでも割り込みでも
/// 1になるのは意図どおりで、例外を作らない。<c>ModifyAttack</c> は攻撃力そのものを
/// 書き換える窓口なので、ここで条件分岐を足すと「どの経路なら1でないか」を
/// 呼び出し側ごとに覚える必要が出る。
///
/// 単独では毎ターン1ダメージしか出ない**純粋な払い出し**。読み手がいない編成では
/// ほぼ無価値で、それが値段（可変コスト型。「この駒をどう使うんだ」が編成パズルそのもの）。
///
/// <para><b>一度差し替えて、測って、戻した（第29期）。</b> 「単独での出力 1/ターン」が
/// 5枠の予算に対して重すぎるという読みから、マイナスを
/// <c>ModifyIncomingDamage</c>（受けるダメージ1.5倍・<see cref="FrailTrait"/> と同じ式）へ
/// 移し、<c>MaxHp</c> を 44 → 48 にした版を実測した。**判定は不合格**——
/// (a) 第28期に 20.0〜20.4% で横並びになった「4枠が払い出し」の台は 20.0〜20.3% のままで
/// 1ビットも動かず、(b) 狙っていたシガのプラス側の発火も 0回のまま、
/// (c) 勝率もほぼ同値だった。<b>差し替えを消さずにここに残してあるのは、
/// この失敗そのものが下の反証の根拠だから。</b></para>
///
/// <para><b>反証（第29期の本体）: 配置の値段を作っていたのは、この極端なマイナスだった。</b>
/// キリに出力を戻したら、`裂き (キリ×エグ)` のキリ↔エグ席交換の価値が
/// <c>confirm</c>（seed 200..599）で <b>+15.1pt → +0.6pt</b> に落ち、
/// <c>reseat</c> 120通りの帯も 56.5〜38.7%（二峰）→ 57.3〜51.9%（単峰）に縮んだ。
/// 第28期にキリを前列へ出せたのは**キリが前で殴られても失うものが無かったから**で、
/// 出力を持った途端に「どちらを晒すか」が本物のトレードオフになり、選択肢が等価に近づいた。
/// <b>配置の判断は「失うものの差」から生まれる。</b> 極端なマイナスは予算を食うが、
/// **配置パズルの発生装置でもある**——性能上の代償としてではなく、
/// 編成・配置を考えさせるフックとして読むこと（README 第29期・第30期 §0）。</para>
///
/// <para><b>採らなかった案（第29期）。</b>
/// (1)「既に傷を持つ相手にはダメージが通らない」——狙いは良いが**窓口が無い**。
/// <c>OnAfterAttack</c> は着弾後に呼ばれるので打ち消せず、<c>ModifyIncomingDamage</c> は
/// 対象側の特性しか見ないので攻撃者の裂きを読めない。<c>ApplyDamage</c> に新しい窓口を
/// 足せば書けるが、**実装の都合で意味論を歪めるより既存の窓口で書ける形を採る。**
/// (2) 回復で打ち消す形——<c>ctx.Heal</c> を経由するので渇き（第三波）や
/// <c>AcceptsSupport</c>（ガルドの <c>Stoic</c>）と干渉する。
/// **状態異常の設計に回復経路を混ぜない。**</para>
///
/// <para><b>予算不足の正体は単価の総和ではなかった。</b> キリ1枠を出力役に戻しても
/// 勝率は +0.1pt しか動かず、台も生き返らなかった（第29期）。傷軸が5枠に収まらないのは
/// **連鎖の長さ**（供給 → 変換 → 出力役で枠が尽きる）の側の問題で、
/// 第30期はこの枝を「供給と出力を1枠に畳んだ」<see cref="CarveTrait"/>（刻みのノミ）で
/// もう1本作り、**同じ資源に長い入口と短い入口を並べて編成に選ばせる。**</para>
/// </summary>
public sealed class RendTrait : Trait
{
    /// <summary>1回の攻撃で刻む傷の数。**量ではなく回数**なので定数1。</summary>
    public const int Wounds = 1;

    public override TraitId Id => TraitId.Rend;

    /// <summary>
    /// <b>刃が薄い（＝与ダメは常に1）は第74期に <see cref="ThinBladeTrait"/> へ切り出した。</b>
    /// キリの <c>Traits</c> に <c>{ Rend, ThinBlade }</c> と並んでいるだけで**盤面は変わらない**
    /// ——切り出した理由は代金を独立に計量できるようにするため（第73期の器具不足）。
    /// </summary>
    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        // 死体には刻まない（痺れ＝ParalyzeTrait と同じ書き手側の作法）。責め苦が
        // 「死んでいても判定は同じ」にしているのは**読み手**だからで、あちらは倒した相手にも
        // 追い打ちの条件が成立していたと数える。書き手側で同じことをすると、二度と殴られない
        // 駒にカウンタを積んでログを濁すだけになる。
        if (!target.IsAlive) return;

        // **主目標のみ・攻撃1回に1度**（engine の規則）。薙ぎでも貫きでもここは1回しか
        // 呼ばれないので、範囲持ちが供給を独占して非線形に伸びることが原理的に起きない。
        // 毒で二度踏んだ穴（層が二次関数で伸びる）を構造的に避けているのがこの1行。
        int w = target.Counter(StatusKeys.Wound) + Wounds;
        target.SetCounter(StatusKeys.Wound, w);
        ctx.Log($"    {self.Name} の刃が {target.Name} に傷を残した（傷 {w}）", LogKind.Status);
    }
}

/// <summary>
/// 抉り。傷（<see cref="StatusKeys.Wound"/>）の読み手。
///
/// **1つのルールの表と裏**: 開いた傷にしか興味がないので、傷を抉れば深く入る。
/// 塞いだ（＝倒した）先へも踏み込みすぎて、次の手番を失う。
///
/// 上乗せは**加算**（傷1つにつき +<see cref="PerWound"/>）。倍率にすると強化を受けた瞬間に
/// 二乗で伸びる（README「増幅は必ず加算にする」）。傷の側が線形にしか伸びないので、
/// 加算で読む限り出力もターン数に対して線形に留まる。
///
/// **傷は消費しない。** 消費型（溜めた傷を全部使って一撃）は連鎖が供給と変換だけで
/// 立つかを先に見るために温存してある。誰が刻んだ傷でも読むので、供給源が増えれば
/// そのまま噛む（結果で解決する）。
///
/// <c>OnKill</c> は**味方側で初の実装**。痺れ機構に乗せてあるので、飛んだ手番は
/// <see cref="StatusKeys.IdleTurn"/> になって号令（ガン）・据え（バン）が買い取る
/// ——ザンの怯み・シガの怖気と同じ形で、これで3例目。
///
/// 「倒すほど止まる」ので、**エグ自身で倒し切るより傷を積んで一撃で通すほうが強い**という
/// 勾配が自己言及的に立つ。トドメを他の駒に譲る配置判断がそこから出る。
/// </summary>
public sealed class GougeTrait : Trait
{
    /// <summary>傷1つあたりの上乗せ。<b>加算</b>（倍率にしないこと。上の但し書き参照）。</summary>
    public const int PerWound = 3;

    public override TraitId Id => TraitId.Gouge;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        int w = target.Counter(StatusKeys.Wound);
        if (w <= 0) return;

        // ApplyDamage の直呼びなので OnAfterAttack は再帰しない（シガの追い打ちと同じ作法）。
        // 生死は ApplyDamage の生存判定に任せる——既に倒れているなら空振りするだけで、
        // 「傷を抉った」という判定に例外を作らない（結果で解決する）。
        ctx.Log($"    {self.Name} が {target.Name} の傷をこじ開ける（傷 {w} → +{PerWound * w}）",
            LogKind.Highlight);

        // 計数（第87期・持続係数の検算）。**盤面には一切影響しない。**
        // 上乗せは**この呼び出しの中で払い切る**（残るものが盤面に無い）ので、
        // 累積出力と即時出力が同じ1つの量になる＝持続係数が定義上 1.0。
        UnitTally gt = ctx.TallyOf(self);
        gt.GougeFires++;
        gt.GougeOut += PerWound * w;

        ctx.ApplyDamage(target, PerWound * w, self);
    }

    // **深追い（倒すと次の手番を失う）は第74期に OverreachTrait へ切り出した。**
    // エグの Traits に { Gouge, Overreach } と並んでいるだけで**盤面は変わらない**
    // ——切り出したのは代金を独立に計量できるようにするため（第73期の器具不足）。
}

/// <summary>
/// 刻み。傷（<see cref="StatusKeys.Wound"/>）の**2つ目の入口**で、
/// <see cref="RendTrait"/>（裂き）＋<see cref="GougeTrait"/>（抉り）を**1枠に畳んだ形**。
///
/// 攻撃した相手に傷を刻み、**その相手が既に負っている傷1つにつき +<see cref="PerWound"/>**（加算）。
/// 供給源と変換器が同じ1手に同居しているので、読み手がいない編成でも単独で回る
/// ——毒軸（スィド・グザ）が5枠で成立している理由そのものを、傷軸に移植した形。
///
/// <para><b>順序が仕様: 傷を足す<u>前</u>に読む。</b> 逆にすると自分の刻みを即座に自分で読む
/// 自己給餌になり、1発目から上乗せが乗って**単騎で二次関数に伸びる**（毒で二度踏んだ穴）。
/// この順序のおかげで、単騎の伸びは「2発目 +2 / 3発目 +4 / …」＝ターン数に対して線形に留まる。</para>
///
/// <para><b>上乗せはエグ（+3）より弱い +2。</b> 畳んだぶんだけ単価を下げてある。
/// 長い連鎖（キリ → エグ）は枠を2つ食う代わりに1発が重く、短い入口（ノミ単騎）は
/// 枠1つで回る代わりに軽い。**同じ資源に長短2つの入口を並べて、編成に選ばせる**のが第30期の狙い。</para>
///
/// <para>傷の分布はキリと**正反対**になる。キリは複数体に1つずつ撒くが、ノミは
/// <see cref="FixateTrait"/>（執着）で1体に食いつくので**1体へ積み上がる**。
/// 同じ資源でも編成の作り方が割れるのはここ。</para>
///
/// <para><c>ApplyDamage</c> の直呼びなので <c>OnAfterAttack</c> は再帰しない
/// （シガ・エグと同じ作法）。刻む数がダメージ量に依存しないのも裂きと同じ
/// ——量に比例させた瞬間に「強い駒がもっと強くなる」乗算になる。</para>
/// </summary>
public sealed class CarveTrait : Trait
{
    /// <summary>1回の攻撃で刻む傷の数。**量ではなく回数**なので定数1（裂きと同じ）。</summary>
    public const int Wounds = 1;

    /// <summary>傷1つあたりの上乗せ。<b>加算</b>。畳んだぶん抉り（+3）より低い。</summary>
    public const int PerWound = 2;

    public override TraitId Id => TraitId.Carve;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        // 死体には刻まない（裂き・痺れと同じ**書き手側**の作法）。読み手（責め苦・抉り）が
        // 死体にも判定を通すのは「結果で解決する」ためで、書き手で同じことをすると
        // 二度と殴られない駒にカウンタを積んでログを濁すだけになる。
        if (!target.IsAlive) return;

        // **足す前に読む。** ここを入れ替えると自己給餌で二次関数に伸びる（上の但し書き）。
        int w = target.Counter(StatusKeys.Wound);
        if (w > 0)
        {
            ctx.Log($"    {self.Name} が {target.Name} の古い傷をなぞる（傷 {w} → +{PerWound * w}）",
                LogKind.Highlight);
            ctx.ApplyDamage(target, PerWound * w, self);
        }

        // 上乗せで倒れたなら刻まない（上の生存判定と同じ理由。ApplyDamage を挟んだので取り直す）。
        if (!target.IsAlive) return;

        int next = w + Wounds;
        target.SetCounter(StatusKeys.Wound, next);
        ctx.Log($"    {self.Name} の鑿が {target.Name} を彫り込む（傷 {next}）", LogKind.Status);
    }
}

/// <summary>
/// 断ち。傷（<see cref="StatusKeys.Wound"/>）の**消費型の読み手**。
/// <see cref="GougeTrait"/>（抉り）が「維持して読み続ける」なら、こちらは「畳んで一撃で使う」。
///
/// **1つのルールの表と裏**（置き去り・責め苦・仇討ち・裂き・抉りと同型）:
/// **開いた傷しか断てない。** プラス（最も深い傷を狙ってまとめて断つ）とマイナス
/// （傷が閾値に届かない間は断てない）が同じ一文から出る。
///
/// <para><b>第74期に待ち方を変えた（V1・<see cref="SeverWait.Swing"/>）。</b>
/// 第38期〜第73期は「傷が無ければ<b>振らない</b>」で、決着の 26%（理想台 1.85 T/戦）・
/// ドラフト台では 5.70T のうち <b>3.40T</b> を手番ごと捨てていた。
/// V1 は<b>普通に振る</b>——断ちが下りるのは閾値に届いた傷にだけなので、
/// <b>マイナスは消えていない。捨てるものが「手番」から「断ちの機会」に変わっただけ</b>である。
/// 実測でも <c>傷/断ち</c> は 2.00 のまま（＝「畳んで一撃」は保たれている）で、
/// 増えたのは<b>ナタが普通に殴った回数</b>（0.13 → 3.42 振/戦）。
/// <b>ナタが強くなるのは傷が読めるからではなく、毎ターン殴るようになるからである</b>
/// （第73期の申し送りの実証）。</para>
///
/// <para><b>温存を解いた根拠。</b> <see cref="GougeTrait"/> の doc が
/// 「<i>傷は消費しない。消費型（溜めた傷を全部使って一撃）は連鎖が供給と変換だけで
/// 立つかを先に見るために温存してある</i>」と書いていた条件は、第30期の実測
/// （キリ×エグ と ノミ×エグ が別の行に並んだ＝傷の分布が出力に効いている）で満たされた。</para>
///
/// <para><b>上乗せは加算</b>（傷1つにつき +<see cref="PerWound"/>）。倍率にすると強化を
/// 受けた瞬間に二乗で伸びる（README「増幅は必ず加算にする」）。抉り（+3）・刻み（+2）より
/// 高い +5 なのは、**読んだ傷をその場で全部使い切る**から——次の手番の自分は同じ傷を
/// 二度と読めない。維持読みとの差はここにある。</para>
///
/// <para><b>周期は「時計」ではなく「閾値」で作る（第38期）。</b> 第37期の実測では
/// <c>傷/断ち</c> が 1.00 に張り付き、断ちは「畳んで一撃」ではなく
/// 「傷を持つ相手への +5 の定額上乗せ」として動いていた。原因は速さではない——
/// <b>維持読みは順序で立ち、消費読みは周期差で立つ。</b> 全消費の読み手が毎ターン振る限り、
/// 在庫の天井は1ターンの供給量なので、順序を入れ替えても定常在庫は 1 のまま。
/// そこで <see cref="Threshold"/> を置き、<b>在庫が閾値に達するまで振らない</b>ことで
/// 供給と消費の周期差を作る。系として: <b>介入の税額は1振りに載せた在庫に比例する</b>
/// （在庫 1 なら殉教者が逸らしても損は ±0。閾値を置いて初めて介入が税になる）。</para>
///
/// <para><b><c>Actions</c> の <see cref="ActionKind.Charge"/> 化は採らなかった</b>（第38期）。
/// 次に消費型を作る人のための記録で、理由は3つ:
/// <list type="number">
/// <item><b>第36期の先例に正面から抵触する。</b> ゴルムの腹は「毎ターン無条件に溜まる経路を
/// 混ぜると、腹が盤面の出来事から切れてただの周期になる」として時計型の蓄積を明示的に外した。
/// Charge は絶対時刻の周期で、傷という<b>盤面の出来事</b>から蓄積を切り離してしまう。</item>
/// <item><b>Charge の枠組みは増幅を内蔵している。</b> 実物は <c>[Charge, Attack(200)]</c>
/// （狙撃手・詠唱兵）で、Attack 側のパーセントが溜めの払い出し。100 にして外すことはできるが、
/// そのとき Charge は「何も溜まらない溜め」になり、説明がつかない駒になる。</item>
/// <item><b>可動部が少ない。</b> 閾値はこのクラスの判定1つで書けて、engine・<c>Actions</c>・
/// 周期スタック（種別依存の弾きは周期を進めない）のどれにも触らない。</item>
/// </list></para>
///
/// <para><b>標的選好の窓口は engine 側（<c>SelectTargetChain</c>）。</b> 攻撃者側の標的選択に
/// Trait のフックが無いので、執着（<see cref="FixateTrait"/>）とまったく同じ場所に置く。
/// **素の候補集合（pool）は1体も足さない・引かない**ので、「前列が生きている限り後列は
/// 狙われない」は破れない。**介入の鎖（標的 → 後備え → 庇う → 殉教 → 棘守り）は後段**なので、
/// 選好は上書きされる——狙いを定めた相手が庇われたら、刃は庇い手に落ちる。</para>
///
/// <para><b><see cref="SurrendersTurn"/> は false（重要）。</b> 傷が無くて捨てた手番を
/// 号令（ガン）・据え（バン）に**買わせない**。true のままだと、供給源を1枚も持たない編成で
/// ナタが「毎ターン <see cref="StatusKeys.IdleTurn"/> を産む無償の収入源」になり、
/// **マイナスが逆に資産化する**（<see cref="Trait.SurrendersTurn"/> の doc がまさにこの穴を
/// 警告している——カドが第五波で号令から無償の +8/ターンを受け取り続けていた例）。
/// のろま（ドルガ）が true でよいのは、あちらが**振れる相手がいるのに振れない**無力化だから。
/// 断ちが振らないのは「振る対象が構造的に存在しない」ためで、**不動（カド）・追い打ち（ハギ）の側**。</para>
///
/// <para><b>候補集合は engine の1箇所（<c>BattleContext.TargetPool</c>）を共有する。</b>
/// 選好（誰を狙うか）と放棄（そもそも振るか）が別々の集合を数えると、
/// 「振ると決めた手番に狙う相手がいない」が起こりうる。</para>
///
/// <para><b>消費は着弾した相手の傷だけ。</b> 殉教者の介入で振りが逸れたら、上乗せは
/// 殉教者の傷（＝ふつう 0）で計算され、意図した相手の傷はそのまま残る。
/// **例外処理を書かない**——「原因ではなく結果で解決する」（裂き・抉りと同じ作法）。</para>
///
/// <para><b>ナタは傷を書かない。</b> 自給させるとマイナス（手番の放棄）が空文になる。
/// 供給源（キリ＝広く薄く撒く／ノミ＝1体へ積み上げる）が要るのは設計そのもの。</para>
///
/// <para><b>エグとナタは同じ資源の維持読みと消費読み</b>なので、**併用すると資源の取り合いに
/// なる**。ナタが断った後の相手は傷 0 で、エグの上乗せも・ノミの「なぞり」も消える。
/// どちらを積むかを編成に選ばせるのが狙いで、両方積むのは足し算にならない。</para>
///
/// <para><c>ApplyDamage</c> の直呼びなので <c>OnAfterAttack</c> は再帰しない
/// （シガ・エグ・ノミと同じ作法）。</para>
/// </summary>
public sealed class SeverTrait : Trait
{
    /// <summary>傷1つあたりの上乗せ。<b>加算</b>（倍率にしないこと。上の但し書き参照）。</summary>
    public const int PerWound = 5;

    /// <summary>
    /// 振り始める傷の深さ（第38期）。狙える敵の最深の傷がこれに満たない間は振らない。
    /// <b>これが周期を作る唯一の可動部</b>——engine にも <c>Actions</c> にも触らない。
    ///
    /// <para>値 2 は第38期 Phase 0 の実測から。現行の <c>刻み×断ち</c> の盤面で
    /// 「ノミが同一の生存敵に刻んだ回数」の1戦あたり最大値の中央値は
    /// 第2波 2.0 / 第3波 2.0 / 第4波 3.0 / 第5波 2.0 で、<b>3 に届くのは第4波だけ</b>。
    /// 3 にすると第2・3・5波でナタが永久に沈黙する（在庫が閾値に届かない）。</para>
    /// </summary>
    public const int Threshold = 2;

    public override TraitId Id => TraitId.Sever;

    /// <summary>
    /// 候補の中で <see cref="StatusKeys.Wound"/> がいちばん深い深さ。傷持ちが1体もいなければ 0。
    /// <b>閾値の判定（<see cref="HasPrey"/>）と待ちのログの出し分けが同じ数を見る。</b>
    /// 2箇所で別々に走査すると「振らないと決めた理由」とログが食い違う。
    /// </summary>
    public static int DeepestWound(BattleContext ctx, UnitState self)
    {
        int best = 0;
        foreach (UnitState f in ctx.TargetPool(self))
        {
            int w = f.Counter(StatusKeys.Wound);
            if (w > best) best = w;
        }
        return best;
    }

    /// <summary>
    /// 候補の中で <see cref="StatusKeys.Wound"/> がいちばん深い駒。傷持ちが1体もいなければ null。
    /// 同数のタイブレークは <see cref="BattleContext.PickOne"/>（席番号の若い順にしないための唯一の窓口。
    /// 候補 0 個・1 個では <c>Roll</c> を消費しない）。
    /// </summary>
    public static UnitState? Preferred(BattleContext ctx, IReadOnlyList<UnitState> pool)
    {
        int best = 0;
        foreach (UnitState f in pool)
        {
            int w = f.Counter(StatusKeys.Wound);
            if (w > best) best = w;
        }
        if (best <= 0) return null;

        return ctx.PickOne(pool.Where(f => f.Counter(StatusKeys.Wound) == best).ToList());
    }

    /// <summary>
    /// <b>この選好（傷がいちばん深い敵を狙う）を使う駒か。</b> 第39期に2人目
    /// （縫いのハリ＝<see cref="SutureTrait"/>）が増えたので、
    /// <c>SelectTargetChain</c> の段を2つに割らずにここへ集めた。
    ///
    /// <para><b>閾値（<see cref="Threshold"/>）と手番の放棄は縫いには無い。</b>
    /// 共有するのは「誰を狙うか」だけで、「そもそも振るか」（<see cref="CanAct"/>）は
    /// 断ち固有——ハリは傷持ちがいなければ普通の標的を普通に殴る（繕いが出ないだけ）。</para>
    /// </summary>
    public static bool Prefers(UnitState u)
        => u.HasTrait(TraitId.Sever) || u.HasTrait(TraitId.Suture);

    /// <summary>
    /// <b>手番の放棄（傷が閾値に届くまで振らない）は第74期に <see cref="AwaitTrait"/> へ切り出した。</b>
    /// <c>CanAct</c> と <see cref="Trait.SurrendersTurn"/> は<b>同じ Trait に載っていないと意味を持たない</b>
    /// （<see cref="Trait.SurrenderedTurn"/> が「<c>CanAct</c> を偽にした Trait のうち
    /// <c>SurrendersTurn</c> が偽のものがあるか」を見る）ので、2つまとめて移してある。
    /// <b>選好（<see cref="Preferred"/> / <see cref="Prefers"/>）と消費はこちらに残す</b>
    /// ——標的選択は出力の一部で、代金ではない（第74期 §1-1）。
    /// </summary>
    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        // **着弾した相手の傷だけを読む。** 介入で逸れたなら殉教者の傷（ふつう 0）を読んで空振りする。
        int w = target.Counter(StatusKeys.Wound);
        if (w <= 0) return;

        // **待ち方 V1（第74期）。** 手番を捨てない版では、断ちは閾値に届いた傷にしか下りない
        // ——**捨てるものが「手番」から「断ちの機会」に変わるだけ**でマイナスは消えていない。
        // 既定（<see cref="SeverWait.Yield"/>）ではこの行は素通りするので、盤面は1ビットも変わらない。
        if (ctx.Sever.Wait == SeverWait.Swing && w < ctx.Sever.Threshold) return;

        // 生死は ApplyDamage に任せる（読み手側の作法。死体でも判定は同じ＝結果で解決する）。
        ctx.Log($"    {self.Name} が {target.Name} の傷をまとめて断つ（傷 {w} → +{PerWound * w}）",
            LogKind.Highlight);
        ctx.ApplyDamage(target, PerWound * w, self);

        // 消費。倒れていても 0 に戻すのは同じ（蘇生で戻ってきた駒が古い傷を抱えない）。
        target.SetCounter(StatusKeys.Wound, 0);
    }
}

/// <summary>
/// 縫い。傷（<see cref="StatusKeys.Wound"/>）の<b>防御側の維持読み</b>で、傷軸の四役目（第39期）。
///
/// <para>供給2（裂き＝<see cref="RendTrait"/> / 刻み＝<see cref="CarveTrait"/>）に対し、
/// 読み手はこれで3枚——**攻めの維持読み（抉り＝<see cref="GougeTrait"/>）／消費読み
/// （断ち＝<see cref="SeverTrait"/>）／防御の維持読み（縫い）**。上乗せ量 +3 は抉りと同じで、
/// <b>出力の代わりに回復に落ちる防御の鏡</b>になっている。</para>
///
/// <para><b>1つのルールの表と裏</b>（傷軸の作法）: 糸は開いた傷にしか通らない。
/// 通した糸を引けば、その傷は塞がる。</para>
///
/// <para><b>窓口は必ず <see cref="BattleContext.Heal"/>。</b> 繕いが渇き（<see cref="DroughtTrait"/>）に
/// 課税されることが第39期の目的そのもので、第三波はロスターの持続回復に課金する波なのに
/// 買い手が薄かった（第22/31/36期の残件）。<see cref="UnitState.AcceptsSupport"/> の濾しも
/// 窓口と <see cref="BattleContext.MostHurtAlly"/> に任せる——ここでは1つも判定を持たない。</para>
///
/// <para><b>塞ぎ（マイナス）は渇き下でも走る。</b> 繕いが封じられていても傷は 1 つ減る
/// ——「Heal が通らなかったら塞がない」と親切にしない（原因ではなく結果で解決する、の作法）。
/// <b>第三波はハリの編成に二重に課金する</b>（回復の封じ ＋ 傷という資源の目減り）。
/// これは仕様であって不具合ではない。</para>
///
/// <para><b>定常在庫: 繕いは 3/T の定額になる。</b> 読んで1つ塞ぐので消費は 1/T。
/// 供給1枚（キリ単独／ノミ単独）は 1/T なので在庫は 0↔1 に固定され、
/// <c>傷/繕い</c> は 1.00 に張り付く（第38期に断ちで踏んだのと同じ算術で、
/// **消費型かどうかは供給と消費の周期差で決まる**）。スケールさせるには供給2枚が要るが、
/// それは第28期の予算壁——だから台は組まない。</para>
///
/// <para><b>ナタとは同居させない。</b> ハリの塞ぎ（1/T）が供給（1/T）と等速なので在庫が
/// 天井 1 に張り付き、<see cref="SeverTrait.Threshold"/>（2）に構造的に届かない
/// ＝ ナタが永久に沈黙する。**取り合いではなく飢餓**（エグとの取り合いとはここが違う）。
/// **第85期に両側読み（下記）で測り直しても崩れなかった**——糸口が味方側へ逸れて敵側の在庫は 0.57/T まで積むが、
/// 閾値到達は 0.00 → 0.01 回/戦（カド × ハリ × ナタ の専用台・弱い波）。飢餓の原因は塞ぎではなく、
/// この台では傷を書く前に決着が付くこと（第3〜5波は 2.9〜4.0T）。</para>
///
/// <para><b>両側読み（第85期に測り、<u>第88期に採用した</u>）。</b> 糸口の候補を
/// 「殴った相手」から「殴った相手か、傷がいちばん深い味方（自分を除く）か、深いほう」へ広げる版。
/// 味方の傷の書き手（巻き込み則＝<see cref="SpillWoundRule"/>）と対で初めて働く
/// ——<b>両側読みだけを入れても盤面は1セルも動かない</b>（味方に傷が載る経路が他に無いので、
/// 味方側の候補が常に空。第88期の実測で 50 体 × 128 台の Δ相乗 が全部ちょうど 0.00）。
/// <b>第85期は当時の主判定（Δ相乗 の平均 ≥ +3.0pt）で落ちた</b>——6枚平均 +1.30pt。
/// その線は第80期の<b>水準</b>の分布から誤って<b>増分</b>へ引いた線で、第88期に特異性の判定へ置き換えたところ
/// <b>意図した6枚のうち上位2枚（吸いのゴルム +5.25 / 余波のボルグ +2.54）が 50 体の1位・2位を占め、
/// 意図しない 44 体は1体もノイズ床を超えなかった</b>ので採った。
/// 律速は依然として<b>ハリの振り回数（2.15 回/戦）</b>で、量そのものは小さい。
/// 経緯は design/PHASE85_SUTURE2.md と design/PHASE88_GAUGE.md。</para>
///
/// <para><b>消費しない読み手（抉り）とは共存できる。</b> 塞ぎは 1 ずつしか引かないので、
/// 同じターンに積まれた傷を抉りが読む余地は残る——ただし両方積むのは予算壁の側で止まる。</para>
///
/// <para>標的選好は<b>断ちと同じ段を共有する</b>（<see cref="SeverTrait.Prefers"/>）。
/// 貫き型は <c>SelectPierceEntry</c> が手前で分岐するので選好が働かない
/// ——執着・断ちとまったく同じ非対称。</para>
/// </summary>
public sealed class SutureTrait : Trait
{
    /// <summary>
    /// 傷1つあたりの繕い量。<b>加算</b>（<see cref="GougeTrait.PerWound"/> の防御鏡で同値）。
    /// 倍率にすると強化を受けた瞬間に二乗で伸びる（README「増幅は必ず加算にする」）。
    /// </summary>
    public const int PerWound = 3;

    public override TraitId Id => TraitId.Suture;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        // **着弾した相手の傷を読む**（断ちと同じ）。介入で逸れたなら殉教者の傷を読んで空振りする。
        UnitState donor = target;
        int w = target.Counter(StatusKeys.Wound);

        // 両側読み（第85期・`SutureRule.Both`）。**糸口の候補が味方にも広がる**——
        // 生存する味方のうち self を除いて傷がいちばん深い者。**深いほうを取り、同数なら敵側**（現行挙動を保つ）。
        // 同数のタイブレークだけ `PickOne`（候補 0 個・1 個では Roll を消費しない）。
        // **味方側の糸口に self を含めない**（ハリが巻き込まれて負った傷はハリ自身では引けない。配置の問いの本体）。
        // `AcceptsSupport` は見ない——傷を塞ぐのは支援ではなく、相手のマイナスを取り除く操作。
        // `SutureSide.Foe`（第87期までの既定）ではこのブロックは素通りする。
        // **第88期に `Both` が既定になった**——ただし単独では何も変えない（味方に傷が載る経路は
        // `SpillWoundRule` だけで、そちらも同時に既定になっている）。
        bool fromAlly = false;
        if (ctx.Suture.Side == SutureSide.Both)
        {
            var wounded = ctx.LivingMembers(self.TeamId)
                .Where(a => a != self && a.Counter(StatusKeys.Wound) > 0).ToList();
            if (wounded.Count > 0)
            {
                int best = wounded.Max(a => a.Counter(StatusKeys.Wound));
                if (best > w)
                {
                    donor = ctx.PickOne(wounded.Where(a => a.Counter(StatusKeys.Wound) == best).ToList())!;
                    w = best;
                    fromAlly = true;
                }
            }
        }
        if (w <= 0) return;

        // 糸は自分には通せない（MostHurtAlly が self を除く）。
        UnitState? patient = ctx.MostHurtAlly(self);
        if (patient is null) return;

        // **塞ぎは札（<see cref="TraitId.Seal"/>）で切り出してある**（第74期）。
        // 既定ではハリが必ず持っているので、盤面も文字列も1バイトも変わらない。
        bool seal = self.HasTrait(TraitId.Seal);

        // どちらから引いたかを必ず出す（第85期 Q3 の目視監査。計数は UnitTally の側）。
        ctx.Log($"    {self.Name} が {(fromAlly ? "味方の " : "")}{donor.Name} の傷口から糸を引き、{patient.Name} を縫い戻した"
            + (seal ? $"（傷 {w} → +{PerWound * w}、傷 {w - 1} へ）" : $"（傷 {w} → +{PerWound * w}）"),
            LogKind.Trigger);

        // 渇き下ではこの1行が何も返さない。**それでも下の塞ぎは走る**（クラスの doc 参照）。
        int before = patient.Hp;
        ctx.Heal(patient, PerWound * w);

        // 計数（第85期）。**盤面には一切影響しない**——糸口の内訳と「繕いは 0 だが塞ぎは走った」回数。
        UnitTally st = ctx.TallyOf(self);
        if (fromAlly) { st.SutureAlly++; st.SutureAllyDepth += w; if (w > st.SutureAllyDepthMax) st.SutureAllyDepthMax = w; }
        else st.SutureFoe++;
        if (patient.Hp == before) st.SutureDry++;
        st.SutureHealed += patient.Hp - before;

        // 塞ぎ。**糸を通したほう**の傷を**1つだけ**引く（全部消すのは断ちの側の役で、こちらは維持読み）。
        if (seal) donor.SetCounter(StatusKeys.Wound, w - 1);
    }
}

/// <summary>
/// 執着。<b>一度狙った敵が（狙える位置に）生きている限り、他の敵を狙えない。</b>
///
/// **出力でも耐久でもなく「対象選択」を縛るマイナス**で、ロスター初。第29期の反証
/// （<see cref="RendTrait"/> の doc）——**配置の値段は「失うものの差」から生まれる**——を
/// 踏まえた設計で、ノミは普通に殴れるので予算を食わないまま代金だけを払う。
///
/// <para><b>代金は敵の編成に依存して変動する（可変コスト型）。</b> 壁（軛の重装兵 145）に
/// 食いついたら、他の敵に一切触れないままターンが流れる。波ごとに価値が変わるので、
/// シガ・ザン・キリと同じ「この駒をどう使うんだ」の系統。</para>
///
/// <para><b>窓口は engine 側（<c>SelectTargetCore</c>）。</b> 攻撃者側の標的選択に
/// Trait のフックが無いのでここに置く——庇う・後備え・標的・棘守りが同じ層で働くのと同じ扱いで、
/// 盤面ルール（逆位・渇き・軛・粛）とは別の理由。Trait 本体は記憶の読み書きだけを持つ。</para>
///
/// <para><b>安全弁「現在の pool に含まれるなら」。</b> 前列が生きている限り後列は狙われない、
/// という盤面の中核規則を執着に破らせない。記憶した敵が後列に取り残されたら
/// **執着は自然に解ける**（pool に入らないので通常選択へ落ちる）。生存判定も pool 経由で兼ねる
/// ——pool は生存者からしか作られない。</para>
///
/// <para><b>介入の鎖（標的 → 後備え → 庇う → 棘守り）には触らない。</b> 執着が主目標を
/// 決めたあとも鎖はそのまま走り、**記憶するのは鎖を通ったあとの相手**。
/// つまり庇われたら次の手番からは庇った駒に執着が移る——<b>「庇うで執着を引き剥がす」</b>
/// という相互作用が、新しい規則を1つも足さずに立つ。逆に鎖の前で記憶すると、
/// 毎ターン庇われ続けて執着が永久に動かない駒になる。</para>
///
/// <para><b>効くのは単体攻撃だけ。</b> 貫きは <c>SelectPierceEntry</c> へ早期に分岐して
/// 標的選択自体を通らないが、**薙ぎ・全体は通る**ので <c>pattern == Single</c> を明示的に見ている
/// （「範囲は手前で分岐するから効かない」は誤り。行動パターンで型が変わる駒と組んだときに
/// 巻き込みの中心が固定されてしまう）。</para>
///
/// <para><b>記憶は <c>InstanceId + 1</c> で持つ</b>（0 を「未設定」に使うため。
/// <c>InstanceId</c> は 0 から振られるので、素で入れると1体目と未設定が区別できない）。
/// <c>Def.Id</c> では駒を指せない——胞子のように同じ def の駒が複数立つ。</para>
///
/// <para><b>会戦の境界で必ず捨てる</b>（<see cref="OnCarryOver"/>）。<c>InstanceId</c> は
/// 戦闘ごとに <c>ctx.Add</c> が振り直すので、持ち越すと**前の戦闘の番号が次の戦闘の
/// 無関係な駒に当たる**。エンジンが消すのは <c>StatusKeys</c> と <c>AtkBonus</c> だけで、
/// 特性私有のカウンタは既定で持ち越される（<c>Trait.OnCarryOver</c> の doc）。</para>
/// </summary>
public sealed class FixateTrait : Trait
{
    /// <summary>執着している敵の <c>InstanceId + 1</c>。0 は未設定。</summary>
    public const string MemoryKey = "fixate";

    public override TraitId Id => TraitId.Fixate;

    /// <summary>
    /// 記憶している敵が今の候補（pool）にいれば返す。いなければ null（＝執着は解ける）。
    /// <c>Roll</c> を一切消費しない——ここで乱数を引くと、執着している間と
    /// していない間で以降の乱数列がずれる。
    /// </summary>
    public static UnitState? Remembered(UnitState self, List<UnitState> pool)
    {
        int id = self.Counter(MemoryKey) - 1;
        if (id < 0) return null;

        // LINQ を使わないのは標的選択が最も熱い経路だから（layout は数百万戦を並列で回す）。
        foreach (UnitState f in pool)
            if (f.InstanceId == id) return f;
        return null;
    }

    /// <summary>鎖を通ったあとの相手を覚え直す。</summary>
    public static void Remember(UnitState self, UnitState target)
        => self.SetCounter(MemoryKey, target.InstanceId + 1);

    /// <summary>InstanceId は戦闘ごとに振り直されるので、境界で必ず捨てる（上の但し書き）。</summary>
    public override void OnCarryOver(UnitState self) => self.SetCounter(MemoryKey, 0);
}

/// <summary>
/// <b>断ちの待ち方（第74期）。</b> <see cref="AwaitTrait"/> と <see cref="SeverTrait"/> が読む。
///
/// <para><b>診断（<c>wcost</c>）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
/// （既定は <see cref="Default"/> ＝ 第38期の現行）。static のノブにしない理由は同型の doc を参照
/// ——Trait は共有シングルトンで、<c>layout</c> は戦闘を並列に回す。</para>
///
/// <para><b>2つのノブは別のものを動かす。</b> <see cref="Wait"/> は<b>待ち方</b>
/// （手番を捨てるか、振ってしまうか）、<see cref="Threshold"/> は<b>閾値</b>（第38期の周期の可動部）。
/// 第74期 §3 の V1 は前者だけ・V2 は後者だけを動かす——<b>「効いたのは待ち方か閾値か」を割るための対</b>。</para>
/// </summary>
public enum SeverWait
{
    /// <summary>
    /// V0（第38期〜第73期）。閾値に届くまで<b>手番を捨てる</b>。
    /// <b>第74期に採用しなかった側</b>——診断 <c>wcost</c> の対照として残す。
    /// </summary>
    Yield,

    /// <summary>
    /// V1（第74期・<b>採用</b>）。<b>閾値未満でも普通に振る。</b>断ちは閾値に届いた傷にしか下りない
    /// ——<b>マイナスは消えていない。捨てるものが「手番」から「断ちの機会」に変わるだけ</b>。
    /// </summary>
    Swing
}

/// <summary>断ちの待ち方と閾値（第74期）。<see cref="SeverWait"/> の doc を参照。</summary>
public readonly record struct SeverRule(SeverWait Wait, int Threshold)
{
    /// <summary>
    /// 既定は<b>第74期に採用した V1</b>（振るが、閾値に届いた傷にしか刃は下りない）。
    /// 閾値は <see cref="SeverTrait.Threshold"/>（第38期の 2 のまま。第74期の V2 で 1 も測って採らなかった）。
    ///
    /// <para><b>採用の根拠</b>（design/PHASE74_WOUND_COST.md）: ドラフト台 Pw で傷の枚数効果の傾きが
    /// <b>−15.6 → −10.4（改善 +5.15 / +5.16・両 seed 帯）</b>。線は +2.0、到達可能上限 +8.3 の 62%。
    /// 閾値だけを下げる V2 は +1.46 / +1.47 しか戻さない——<b>直すべきは閾値ではなく待ち方だった。</b></para>
    /// </summary>
    public static SeverRule Default => new(SeverWait.Swing, SeverTrait.Threshold);
}

// =====================================================================================
// 第74期に切り出したマイナス4枚（**器具**）。
//
// **どれも既定で保持者に付いたままで、盤面は1ビットも変わらない**（`compare` 305 セル 0 件）。
// 切り出したのは「プラスを残してマイナスだけを落とした版」を `Traits.cs` を触らずに作れるように
// するため——第73期はこれが無くて、5枚のうち1枚（ノミの執着）しか代金を計量できなかった。
//
// **前例は執着（`FixateTrait`）。** 刻み（`CarveTrait`）と最初から別の `TraitId` だったので、
// 第73期は「`Traits = { Carve }` の版」を診断のローカルに置くだけで代金が測れた。
// =====================================================================================

/// <summary>
/// 薄刃。<b>与えるダメージは常に1</b>——裂き（<see cref="RendTrait"/>）から切り出した代金。
///
/// <para><c>atk</c> を**まったく読まない**のが要点。<see cref="UnitState.CurrentAttack"/> は
/// <c>Def.Attack + AtkBonus</c> を作ってからここへ渡すので、号令の +4 も呪詛の −6 も
/// 分かちの「腕がなまる」も全部この 1 に潰れる。**床も天井も要らない**
/// ——引数を読まない限り、上流に何が乗っても結果は動かない。</para>
///
/// <para><b>0 を返さないこと。</b> <see cref="BattleContext.ApplyDamage"/> が
/// <c>amount &lt;= 0</c> で早期 return するので、ダメージも被弾強化も反撃も走らなくなる。
/// 一方で <c>OnAfterAttack</c> は <c>PerformAttack</c> の最後で必ず呼ばれるため
/// **傷だけは刻まれ続ける**——「1ダメージも通らないのに汚れだけ溜まる」という、
/// 説明のつかない駒になる。</para>
///
/// <para><b>これがキリの寄与のほぼ全部である</b>（第73期）。素体(攻12) − 素体(攻1) で測った
/// 「打点を潰す代金」がキリの寄与 −17.91 の **99%** を単独で説明し、
/// **傷を書くこと自体の値段は測定精度の中でゼロだった。**
/// 第74期はその代用（素体どうしの差）を、**本物の分割**に置き換える。</para>
/// </summary>
public sealed class ThinBladeTrait : Trait
{
    public override TraitId Id => TraitId.ThinBlade;

    /// <summary>
    /// <b>代金の本体。</b> <c>atk</c> を読まないので上流に何が乗っても 1 に潰れる（doc 参照）。
    ///
    /// <para><b>第75期の払い方（<see cref="ThinBladeRule"/>）はここには書けない。</b>
    /// 「相手に傷があるか」は対象を見る条件で、<see cref="Trait.ModifyAttack"/> は
    /// <c>self</c> しか受け取らない——止め（<c>FinisherTrait</c>・第53期）とまったく同じ形なので、
    /// <b>払い直しは <c>PerformAttack</c> が <c>atk</c> を作った直後</b>に置いてある。
    /// ここは<b>常に 1 を返し続ける</b>ので、<c>CurrentAttack</c> を読む他の全員
    /// （駆り立ての選択・転嫁の流し先・<c>StatSnapshot</c>・棘/仇討ち/責め苦の反撃量）から見た
    /// キリは版によらず攻撃力 1 のまま——<b>版が動かすのは「この一振りの打点」だけ</b>である。</para>
    /// </summary>
    public override int ModifyAttack(UnitState self, int atk) => 1;

    /// <summary>
    /// <b>薄刃を除いた <see cref="UnitState.CurrentAttack"/></b>（＝「素の打点」）。第75期。
    /// 驕りの <c>AttackWithout</c>（第46期）と同型で、<b>自分の <see cref="TraitId.ThinBlade"/> だけ</b>を
    /// 飛ばして残りの <c>ModifyAttack</c> は全部通す——号令の +4 も呪詛の −6 もここでは効く。
    ///
    /// <para><b>床は 1</b>（0 を返さない）。<see cref="BattleContext.ApplyDamage"/> が
    /// <c>amount &lt;= 0</c> で早期 return するので、0 にすると被弾強化も反撃も走らないまま
    /// 傷だけが刻まれる（<see cref="ThinBladeTrait"/> の doc の警告そのもの）。
    /// <b>払わない側が払う側より小さくなることも無い</b>——代金を免除したのに弱くなるのは意味が通らない。</para>
    /// </summary>
    public static int RawAttack(UnitState u)
    {
        int atk = u.Def.Attack + u.AtkBonus;
        foreach (Trait t in u.Traits)
            if (t.Id != TraitId.ThinBlade) atk = t.ModifyAttack(u, atk);
        return Math.Max(1, atk);
    }
}

/// <summary>
/// <b>薄刃の払い方（第75期）。</b> <see cref="ThinBladeTrait"/> の代金を<b>いつ払うか</b>だけを振る。
///
/// <para><b>どの版も代金を消していない。</b> キリは依然「斬れるが断てない駒」で、
/// 条件が真の側では <c>atk</c> を1ビットも読まずに 1 を返す（第29期の「床も天井も要らない設計」を保つ）
/// ——変わるのは<b>条件が偽のときに素の打点へ戻す</b>ことだけ。第74期のナタ
/// （<see cref="SeverWait"/>）が「手番を捨てる」を「振ってから待つ」に変えたのと同じ方針で、
/// <b>払う場面を絞るだけで代金そのものは残す。</b></para>
///
/// <para><b>診断（<c>blade</c>）が版を差し替えるためだけの窓口</b>で、通常の実行では誰も渡さない
/// （既定は <see cref="Default"/>）。static のノブにしない理由は同型の doc を参照
/// ——Trait は共有シングルトンで、<c>layout</c> は戦闘を並列に回す。</para>
///
/// <para><b>3版とも測って採用しなかった</b>（第75期）。ドラフト台 Pw の傾きの改善は
/// V1 <b>+0.99 / +0.96</b>・V3 +0.41 / +0.43・V2 +0.00 で、線 +2.0 に届かない
/// （薄刃を丸ごと外す上限は +3.81 / +3.67）。<b>回収額は「解除率 × 単価」で決まり、
/// 条件が何に接続しているかは値段を決めなかった</b>——解除率あたりの帰属は
/// V1 0.175 / V3 0.190 pt per 1% でほぼ同じ。経緯は design/PHASE75_THINBLADE.md。</para>
/// </summary>
public enum ThinBladeCost
{
    /// <summary>V0（第29期〜）。<b>常に 1。</b>既定。</summary>
    Always,

    /// <summary>
    /// V1（第75期の主判定）。<b>傷の無い相手には 1・傷のある相手には素の打点。</b>
    /// <b>自分で開けた傷に自分で入る</b>——傷は消えないので、キリが一度触った相手は以後ずっと条件を満たす。
    ///
    /// <para><b>実測の解除率は 37.0%</b>（ドラフト台）。<b>キリは執着も断ちの選好も持たないので
    /// 毎ターン pool を引き直し、しかも1戦に 3.9 回しか振らない</b>——
    /// 「同じ相手を殴り続ける」は成立しない（同じ相手を2手番続けて引く確率は全波 50.0%）。</para>
    /// </summary>
    Unwounded,

    /// <summary>
    /// V2（対照）。<b>傷を刻める攻撃だけ 1。</b> 刻めない攻撃＝相手がこの一撃で倒れる攻撃
    /// （<c>RendTrait.OnAfterAttack</c> は死体に刻まない）では素の打点になる。
    ///
    /// <para><b>判定は代金を払った価格（1）で行う予測</b>——実際に倒れるかは
    /// <c>ApplyDamage</c> の中（肩代わり・破片・上限）まで行かないと決まらないので、
    /// <c>Hp &lt;= atk</c> かつ破片なしを「刻めない見込み」と読む。
    /// <b>これはほぼ発火しないはずの版</b>で、V1 との差から
    /// 「効いたのは条件そのものか、1 になる回数が減っただけか」を割るためだけにある。</para>
    /// </summary>
    Carving,

    /// <summary>
    /// V3（対照）。<b>自分より遅い相手には 1・速い相手には素の打点。</b>
    /// 代金を「誰を殴るか」に紐づけた版で、<b>条件の接続先を傷から速さへ移す</b>。
    /// 同速は「遅い」ではないので素の打点側（境界の扱いは <c>blade phase0</c> が数える）。
    /// </summary>
    Slower
}

/// <summary>薄刃の払い方（第75期）。<see cref="ThinBladeCost"/> の doc を参照。</summary>
public readonly record struct ThinBladeRule(ThinBladeCost Cost)
{
    /// <summary>既定は V0（常に 1）＝第29期からの現行。</summary>
    public static ThinBladeRule Default => new(ThinBladeCost.Always);
}

/// <summary>
/// 深追い。<b>敵を倒すと次の手番を失う</b>——抉り（<see cref="GougeTrait"/>）から切り出した代金。
///
/// <para>痺れ機構に乗せてあるので、飛んだ手番は <see cref="StatusKeys.IdleTurn"/> になって
/// 号令（ガン）・据え（バン）が買い取る——ザンの怯み・シガの怖気と同じ形。
/// <c>OnKill</c> は**味方側で初の実装**だった（第28期）。</para>
///
/// <para>「倒すほど止まる」ので、**エグ自身で倒し切るより傷を積んで一撃で通すほうが強い**という
/// 勾配が自己言及的に立つ。トドメを他の駒に譲る配置判断がそこから出る。</para>
/// </summary>
public sealed class OverreachTrait : Trait
{
    public override TraitId Id => TraitId.Overreach;

    public override void OnKill(BattleContext ctx, UnitState self, UnitState victim)
    {
        // 深追い。痺れに乗せてあるので次の手番が飛び（→ IdleTurn → 号令・据え）、
        // ターン外の行動も CanActOutOfTurn が閉じて止まる。
        self.SetCounter(StatusKeys.Stun, 1);
        ctx.Log($"    {self.Name} は {victim.Name} の裂け目に踏み込みすぎた", LogKind.FriendlyFire);
    }
}

/// <summary>
/// 刃待ち。<b>狙える敵の傷が閾値に届くまで手番を捨てる</b>
/// ——断ち（<see cref="SeverTrait"/>）から切り出した代金。
///
/// <para><b><c>CanAct</c> と <see cref="Trait.SurrendersTurn"/> は必ず同じ Trait に載せる。</b>
/// <see cref="Trait.SurrenderedTurn"/> は「<c>CanAct</c> を偽にした Trait の**うち**
/// <c>SurrendersTurn</c> が偽のものがあるか」を見るので、2つを別の Trait に分けると
/// **捨てた手番が号令・据えに売れるようになり、マイナスが逆に資産化する**
/// （<see cref="Trait.SurrendersTurn"/> の doc がまさにこの穴を警告している）。</para>
///
/// <para><b>選好はここには無い</b>（<see cref="SeverTrait.Preferred"/> / <see cref="SeverTrait.Prefers"/>）。
/// 標的選択は出力の一部であって代金ではないので、プラス側に残してある（第74期 §1-1）。
/// ただし<b>候補集合は選好と共有する</b>（<c>BattleContext.TargetPool</c> の1箇所）
/// ——2箇所で数えると「振ると決めた手番に狙う相手がいない」が起こりうる。</para>
///
/// <para><b>実額は決着の 26%</b>（第73期・理想台）。放棄 0.53 + 待ち 1.32 = 1.85 T/戦。
/// **ナタの空振り率は 0.7% しかないが、それは在庫が足りている証拠ではなく
/// 「足りない間は振らない」という設計の帰結**——空振りと手番の放棄は同じ不足の表と裏。</para>
/// </summary>
public sealed class AwaitTrait : Trait
{
    public override TraitId Id => TraitId.Await;

    /// <summary>
    /// 狙える敵に閾値以上の傷を負った駒がいるか。**選好と同じ候補集合**を使う。
    ///
    /// <para><b>閾値は規則（<see cref="SeverRule"/>）から引く</b>（第74期）。
    /// <see cref="SeverTrait.Threshold"/> はその既定値で、<c>const</c> のまま残してある。</para>
    /// </summary>
    public static bool HasPrey(BattleContext ctx, UnitState self)
        => SeverTrait.DeepestWound(ctx, self) >= ctx.Sever.Threshold;

    /// <summary>
    /// 止めているのは<b>攻撃だけ</b>（不動＝<see cref="ImmobileTrait"/> と同じ側）。
    /// 溜めも術も通す——断ちが振れないのは対象が存在しないからで、無力化ではない。
    ///
    /// <para><b>ログを出すのは のろま（<see cref="SluggishTrait"/>）と同じ作法。</b>
    /// <see cref="Trait.SurrenderedTurn"/> からも呼ばれるので同じターンに2行出ることがあるが、
    /// この判定は副作用を持たない（盤面の値を1つも変えない）ので結果には影響しない。</para>
    /// </summary>
    public override bool CanAct(BattleContext ctx, UnitState self, ActionKind kind)
    {
        if (kind != ActionKind.Attack) return true;

        // **V1（第74期）は手番を捨てない。** 断ちの側が閾値で発火を絞る
        // （<see cref="SeverTrait.OnAfterAttack"/>）ので、マイナスは消えていない。
        if (ctx.Sever.Wait == SeverWait.Swing) return true;

        // **待ちを2種に分ける**（第38期）。「獲物がいない」と「まだ浅い」は
        // 同じ「振らない」でも意味が違う——前者は供給が止まっている（書き手が落ちた）、
        // 後者は在庫が積み上がっている最中。診断が別に数えられないと、
        // 周期が立ったのか供給が枯れたのかが決まらない。
        int deepest = SeverTrait.DeepestWound(ctx, self);
        if (deepest >= ctx.Sever.Threshold) return true;

        ctx.Log(deepest <= 0
            ? $"    {self.Name} は閉じた肌に刃を下ろさない"
            : $"    {self.Name} は傷がまだ浅いと刃を上げない", LogKind.Action);
        return false;
    }

    /// <summary>
    /// 捨てた手番は<b>売り物にならない</b>。理由はクラスの doc を参照（この1行が無いと
    /// マイナスが号令・据えへの無償の収入に化ける）。
    /// </summary>
    public override bool SurrendersTurn => false;
}

/// <summary>
/// 塞ぎ。<b>繕うたび、糸を通した敵の傷がひとつ塞がる</b>
/// ——縫い（<see cref="SutureTrait"/>）から切り出した代金。
///
/// <para><b>これだけは札（marker）で、本体は <see cref="SutureTrait.OnAfterAttack"/> の中にある。</b>
/// 引き受け（<see cref="BearTrait"/>）が「本体は <c>Dull</c> の中・Trait は空の札」なのと同型。
/// **フックとして切り出せない理由は2つあり、どちらも挙動が変わる:**
/// <list type="number">
/// <item>塞ぎは<b>患者がいたときだけ</b>走る（<c>MostHurtAlly</c> が null なら繕いも塞ぎも起きない）。
/// 別フックで書くには患者をもう一度引く必要があるが、<b>繕いで患者が満タンになると
/// 2度目の <c>MostHurtAlly</c> は null を返す</b>ので、塞ぐ／塞がないが入れ替わる。</item>
/// <item><c>MostHurtAlly</c> は同値のタイブレークに <c>PickOne</c> を使う。
/// **2度引くと <c>Roll</c> を余分に消費して以降の乱数列がまるごとずれる。**</item>
/// </list>
/// <b>だから「分離した」とは書かない</b>（同じ動作の表と裏を切ると別の機構になる）。
/// 札にすることで<b>計量だけができる</b>ようになっている——それがこの期の目的そのもの。</para>
///
/// <para><b>塞ぎは渇き下でも走る。</b> 繕いが封じられていても傷は 1 つ減る
/// ——「Heal が通らなかったら塞がない」と親切にしない（原因ではなく結果で解決する、の作法）。</para>
/// </summary>
public sealed class SealTrait : Trait
{
    public override TraitId Id => TraitId.Seal;
}

/// <summary>
/// 断罪。反撃で殴られたとき、反撃してきた相手を痺れさせる。敵側の語彙。
///
/// 反撃役（カド）の盤面への関与は反撃しかない。攻撃力が閾値を超えれば敵を倒し切って
/// ほぼ無傷で勝ち、超えなければ何もできないまま負ける。中間の勝率が構造的に存在せず、
/// 係数をどう刻んでも崖にしかならないことは4軸で測定済み（README 参照）。
///
/// なので値ではなく「反撃そのものに代金を請求する」形にする。刺し返すたびに痺れるので、
/// **反撃の回数が多い編成ほど多く払う。** 反撃しない編成には何も起きない。
///
/// 痺れをただ撒く敵（審問官に痺れを持たせる案）は却下した。支配している編成が無傷のまま
/// （反撃(ヒサ×カド) が -0.5pt）周辺だけが全滅する、裾野を刈るだけの形になる。
/// 痺れは棘・割り込み・追い打ちの門（CanActOutOfTurn）を閉じるので、
/// ターン外に動く駒だけが代金を払う。ターンを持たないカドが「痺れで失うものが無い」
/// 状態だったのを塞いだ 98f8947 と対になる変更。
/// </summary>
public sealed class CondemnTrait : Trait
{
    // トウ（痺れ粉）の ParalyzeTrait.Chance とは別勘定。同じ値だが連動させないこと。
    // 敵側の代金の頻度を刻むためにプレイヤー側の駒が動くと、前回の「係数が別勘定に漏れる」失敗に戻る。
    public const int Chance = 45;

    public override TraitId Id => TraitId.Condemn;

    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        if (source is null || source.TeamId == self.TeamId) return;

        // 反撃で殴られたときだけ。自分のターンの殴り合いに反応させると
        // ただの痺れ撒きに戻り、ターン外に動かない編成まで巻き込む。
        if (!ctx.InReaction) return;
        if (!source.IsAlive) return;
        if (ctx.Roll(100) >= Chance) return;

        source.SetCounter(StatusKeys.Stun, 1);
        ctx.Log($"    {self.Name} が {source.Name} の反撃を断罪した", LogKind.Status);
    }
}

/// <summary>毒喰らい。敵に積まれた毒の量に応じて味方を癒す。毒が無ければ何もしない。</summary>
public sealed class DevourTrait : Trait
{
    /// <summary>
    /// 味方が負った毒のダメージ倍率。安定しすぎるのを止めるための代金。
    /// x1.5 では 毒+耐久 の第5波が 92% → 90% とほぼ動かず、x3 では 18% まで落ちる。
    /// x2（92% → 66%）を採った。ベニを含まない27編成はどの倍率でも ±0.0。
    /// </summary>
    public const int AllyPoisonMultiplier = 2;

    public override TraitId Id => TraitId.Devour;

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        int poisoned = ctx.LivingMembers(ctx.Opponent(self.TeamId))
            .Count(f => f.Counter(StatusKeys.Poison) > 0);
        if (poisoned == 0) return;

        int amount = poisoned * 4;
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
            ctx.Heal(ally, amount);
        ctx.Log($"    {self.Name} が敵の澱みを啜った（味方全体 +{amount}）", LogKind.Trigger);
    }
}

/// <summary>
/// 号令。前のターンに行動しなかった味方を強化する。
/// のろま・不動・麻痺のいずれでもよく、「動けない」こと自体を資源に変える。
/// </summary>
public sealed class RallyTrait : Trait
{
    public const int Gain = 8;
    public const int OpeningGain = 4;

    public override TraitId Id => TraitId.Rally;

    // 開戦の鬨。1ターン目は誰もまだ「動かなかった」実績が無いので、明示的に別扱いにする。
    // カウンタ未設定の 0 が ctx.Turn - 1 と一致してしまう事故で全体強化されていたのを、
    // 意図した挙動として書き直したもの。
    public override void OnBattleStart(BattleContext ctx, UnitState self)
    {
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self) continue;
            foreach (UnitState t in ctx.SupportTargets(ally))
                ctx.Whet(t, OpeningGain, WhetRoute.RallyOpening);
        }
        ctx.Log($"  {self.Name} が鬨を上げた（味方全体 攻撃 +{OpeningGain}）", LogKind.Trigger);
    }

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self) continue;

            int idle = ally.Counter(StatusKeys.IdleTurn);
            if (idle <= 0 || idle != ctx.Turn - 1) continue;

            // 差し出したターンにだけ払う。不動（カド）は最初から振らない型で、
            // 差し出すものが無い。ここを見ないと静的なマイナスが毎ターンの収入になる。
            //
            // 据え（Bulwark）は積み上がらない一定の減衰なので、こちらの制限はかけない。
            // （2026-08-28 変更）据えにも同じ制限をかけた。規則の趣旨を
            // 「収入が雪だるまにならないこと」から「差し出した者だけが報酬を受け取れる」に
            // 統一したため。暴走防止は「加算であって乗算ではない」が別に担当しており、
            // IdleTurn の会計に2つの仕事を兼ねさせない（CanAct が2つの役割を担っていて
            // 分離したのと同じ理由）。積み上がらなくとも、ハギは毎ターン確実に −50% を
            // 受け取る＝実質HPが常時2倍になるので、恒久的な収入であることに変わりはない。
            //
            // 判定そのものは Trait.SurrenderedTurn に切り出してある（据え側と共有）。
            if (!SurrenderedTurn(ctx, ally)) continue;

            // 条件（差し出したターンかどうか）は ally 側で見て、乗せる先は拡散を通す。
            // 拡散持ちは自分では受け取らないが、差し出した事実は本人のものなので判定は動かさない。
            foreach (UnitState t in ctx.SupportTargets(ally))
                ctx.Whet(t, Gain, WhetRoute.RallyTurn);
            ctx.Log($"    {self.Name} の号令で {ally.Name} の溜めが乗った（攻撃 +{Gain}）", LogKind.Trigger);
        }
    }
}

/// <summary>
/// 澱み喰い。味方が負った毒を吸い取り、その層の分だけ自分が強くなる。
/// 毒を撒く駒の「味方にも漏れる」というマイナスを、そのまま資源に変える。
/// </summary>
public sealed class BlightfedTrait : Trait
{
    public const int GainPerStack = 4;
    public override TraitId Id => TraitId.Blightfed;

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        int drawn = 0;
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self) continue;
            int poison = ally.Counter(StatusKeys.Poison);
            if (poison <= 0) continue;
            ally.SetCounter(StatusKeys.Poison, 0);
            drawn += poison;
        }
        if (drawn == 0) return;

        self.AtkBonus += drawn * GainPerStack;
        ctx.Log($"    {self.Name} が味方の澱みを吸い上げた（{drawn}層 / 攻撃 +{drawn * GainPerStack}）", LogKind.Trigger);
    }
}

/// <summary>
/// 軋み。隊列を動かされるたびに強くなり、動かされた直後にその場で割り込んで攻撃する。
/// 前へ突き出されたときの上昇は特に大きい。
/// 逃亡・喧噪・庇いなど「隊列を乱す」挙動すべてが起点になる。
///
/// 割り込み攻撃は「積み上げた攻撃力を振る機会」そのもの。移動を累積ボーナスにしか変換しないと、
/// 動かし役を増やすほど火力枠が消えて、育てても振る回数が増えない
/// （移動改2 はヨミを攻撃70まで育てながら第四波 15%）。火力ではなく機会を足す変更。
/// 回数制限は設けず再入禁止のみ。移動は他駒のターン開始時にしか起きないので、
/// 供給量の上限は「盤面に何枚の動かし役を置けるか」で自然に決まる。
/// </summary>
public sealed class DisplacedTrait : Trait
{
    public const int Gain = 9;
    public const int PushedToFrontGain = 22;

    public override TraitId Id => TraitId.Displaced;

    /// <summary>
    /// 軋みが響く（第66期 → <b>第67期に条件の出どころを差し替え</b>
    /// → <b>第77期に供給元を選択子にした</b>）。条件の値が閾値を越えると、
    /// 単体の一撃が<b>薙ぎ</b>になる。<b>何を読むかは <see cref="CreakRule.Source"/></b>
    /// ——外から届いた累計（<see cref="UnitState.WhetReceived"/>・第67期の既定）／
    /// 攻撃力の補正そのもの（<see cref="UnitState.AtkBonus"/>・第66期の V9）／その合計。
    ///
    /// <para><b>軋み自身の上昇（<see cref="OnMoved"/> の <c>AtkBonus +=</c>）は条件に入らない。</b>
    /// 第66期は <c>AtkBonus</c> を読んだが、それは<b>この駒が自分で作れる値</b>で、
    /// 上昇量が 9 / 22 の2段しかないので<b>閾値が「0回か1回以上か」の起動スイッチに潰れた</b>
    /// （実測で閾値 9 の到達時点の内訳が 軋み 21.8 対 窓口 8.0、薙ぎ化率 85.9%）。
    /// <b>自分で作れる値を条件に使うと、条件の粒度はその駒自身の上昇量が決める。</b></para>
    ///
    /// <para><b>倍率も追加ダメージも足さない。</b> 変えるのは型だけで、
    /// 量の側は既存の <c>AtkBonus</c>（軋み 9 / 突き出し 22）のまま。</para>
    ///
    /// <para><b>閾値は規則（<see cref="CreakRule"/>）で <c>Run</c> に渡す。</b>
    /// static のノブを置かない理由は同型の doc を参照。<c>Threshold &lt;= 0</c> で完全に不活性
    /// ——<c>Board</c> が null（盤面の外で作られた <see cref="UnitState"/>）でも同じ扱いにする。</para>
    ///
    /// <para><b>割り込み（<see cref="OnMoved"/> の攻撃）にも同じ規則が乗る。</b>
    /// 割り込みは <c>ctx.PerformAttack</c> を通り、そこは <see cref="UnitState.CurrentPattern"/> を
    /// 読むので、型の書き換えは自動で乗る（第66期 Phase 0-1 で確認）。</para>
    /// </summary>
    public override AttackPattern ModifyPattern(UnitState self, AttackPattern p)
    {
        CreakRule rule = self.Board?.Creak ?? CreakRule.Default;
        if (rule.Threshold <= 0) return p;
        return CreakValueOf(self, rule.Source) >= rule.Threshold ? AttackPattern.Sweep : p;
    }

    /// <summary>
    /// 条件が読む値（第77期に選択子を足した）。<b>盤面には一切影響しない読み取りだけ。</b>
    /// <see cref="CreakSource"/> の3点は「自分で作れる値」「外から届いた値」「その合計」で、
    /// <b>V9（第66期）と V67（第67期）を1本の規則の上に並べるためにある</b>
    /// ——版の切り替えが駒の差し替えではなく規則の引数で済むので、
    /// 同じ標本・同じ席・同じ乱数列のまま両者を比べられる。
    /// </summary>
    public static int CreakValueOf(UnitState self, CreakSource source) => source switch
    {
        CreakSource.Bonus => self.AtkBonus,
        CreakSource.Both => self.AtkBonus + self.WhetReceived,
        _ => self.WhetReceived,
    };

    public override void OnMoved(BattleContext ctx, UnitState self, Row from, Row to)
    {
        bool pushedForward = FormationRules.DepthOf(to) < FormationRules.DepthOf(from);
        int gain = pushedForward ? PushedToFrontGain : Gain;
        self.AtkBonus += gain;
        // 軋み（第66期）の在庫の記録。**盤面には一切影響しない。**
        ctx.NoteCreakBonus(self, gain, selfGain: true);
        ctx.Log($"    {self.Name} は突き飛ばされるほど据わる（攻撃 +{gain} → {self.CurrentAttack}）", LogKind.Trigger);

        // 割り込み攻撃の最中に起きた移動は、さらなる割り込みを生まない（再入禁止）。
        // 回数制限ではなく再入禁止にしているのは「連続で動かされたらそのぶん攻撃できる」設計を残すため。
        // 再入だけ止めれば、将来「被弾したら味方と入れ替わる」ような駒が入っても無限ループにならない。
        // フラグは Trait（共有シングルトン）ではなく BattleContext 側に持つ。static にすると
        // layout モードの並列実行で別の戦闘同士が干渉する（BattleContext.Interrupt 参照）。
        if (ctx.InInterrupt) return;

        // 痺れ・のろまで無力化されている間は振れない。攻撃力の上昇は残す
        // （動かされた事実は起きているので、縛めが解けた後にまとめて振る形になる）。
        if (!ctx.CanActOutOfTurn(self)) return;

        // 毒のティックで敵が全滅した直後のターン開始に動かされることがある。振る相手がいなければ見せ場のログも出さない。
        if (!ctx.TeamAlive(ctx.Opponent(self.TeamId))) return;

        // 喧噪で自分が「動かす側」になった場合、この時点では押し出された相手のスロットがまだ更新されていない
        // （SwapSlots 参照）。敵を殴るだけなので味方側のスロットは見ないが、入れ替えの途中で振っていることは覚えておくこと。
        ctx.Interrupt(() =>
        {
            ctx.Log($"    {self.Name} はよろけた勢いのまま振り抜く", LogKind.Highlight);
            ctx.PerformAttack(self, "    ");
        });
    }
}

/// <summary>
/// 軋みが響く強度（第66期）。<b>診断（creak）が版を差し替えるためだけの窓口</b>で、
/// 通常の実行では誰も渡さない。static のノブにしない理由は同型の doc を参照。
///
/// <para><c>Threshold</c> は条件の閾値、<c>Source</c>（第77期）が<b>その閾値を何で測るか</b>
/// （<see cref="CreakSource"/>）。<b><c>0</c> 以下で完全に不活性</b>
/// ——<see cref="DisplacedTrait.ModifyPattern"/> が素通りするだけなので、
/// <b>供給元が何であれ乱数も計数も盤面も1ビットも動かない</b>。これが検算になる。</para>
/// </summary>
public readonly record struct CreakRule(int Threshold, CreakSource Source = CreakSource.Whet)
{
    /// <summary>既定は<b>無効</b>（第66・67・77期とも測定中）。採用したら採った閾値と供給元へ。</summary>
    public static CreakRule Default => new(0, CreakSource.Whet);
}

/// <summary>
/// 軋みが響く条件の<b>供給元</b>（第77期）。<b>engine には何も足していない</b>
/// ——<see cref="DisplacedTrait.ModifyPattern"/> が読む値を選ぶだけの選択子。
///
/// <para><see cref="Whet"/> が第67期の現行（<see cref="UnitState.WhetReceived"/> ＝
/// <c>Whet</c> 窓口を通って外から届いた累計）、<see cref="Bonus"/> が第66期の V9
/// （<see cref="UnitState.AtkBonus"/> ＝<b>軋み自身の上昇を含む</b>ので自分で満たせる）、
/// <see cref="Both"/> はその合計（<b>自足しているうえに外の供給も乗る</b>形）。</para>
///
/// <para><b><see cref="Both"/> は二重計上である</b>——<c>Whet</c> 窓口を通った量は
/// <c>AtkBonus</c> にも入っているので、外から届いたぶんだけ2回数える。
/// <b>それが狙い</b>で、<see cref="Bonus"/> との差がそのまま「外部供給の上積み」になる。</para>
/// </summary>
public enum CreakSource
{
    /// <summary>外から届いた累計だけを読む（第67期・現行）。</summary>
    Whet,
    /// <summary>攻撃力の補正そのものを読む（第66期の V9。<b>軋みで自分で満たせる</b>）。</summary>
    Bonus,
    /// <summary>両方の合計（自足＋外部供給。<b>外から届いたぶんは二重に効く</b>）。</summary>
    Both,
}

/// <summary>
/// 喧噪。毎ターン味方2体の位置を無作為に入れ替える。
/// 後列前提の駒や庇う駒の配置を自分から崩すので、素直な編成とは噛み合わない。
/// </summary>
public sealed class ShufflerTrait : Trait
{
    public override TraitId Id => TraitId.Shuffler;

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        var team = ctx.LivingMembers(self.TeamId).Where(u => u != self).ToList();
        if (team.Count < 2) return;

        UnitState a = team[ctx.Roll(team.Count)];
        var rest = team.Where(u => u != a).ToList();
        UnitState b = rest[ctx.Roll(rest.Count)];

        ctx.Log($"    {self.Name} が隊列をかき回した（{a.Name} ⇔ {b.Name}）", LogKind.FriendlyFire);
        ctx.SwapSlots(a, b.Slot);
    }
}

/// <summary>
/// 縛め。毎ターン味方1体を動けなくする代わりに大きく強化する。
/// 「動かない」を偶然ではなく意図的に作り出すので、溜め軸のエンジンになる。
///
/// <b>縄は1本しかない。</b> 大縛りは<b>開戦時に1回だけ</b>敵の最速1体へ投げ、
/// その代わり第1ターンは味方を縛らない。新しい <see cref="TraitId"/> を作らないのは、
/// 縄が1本であることを特性が1つであることで表すため。
///
/// <b>周期スキルは捨てた。</b> 2周期（攻撃→大縛り）で持たせた版は、稼働率が低い駒だと
/// そもそも発動しない——部隊戦は3〜6ターンしかないので、2周期目が来る前に決着するか本人が落ちる。
/// クグは第3波で 発火 0.12 回/戦（8戦に1回）だった。周期は「長く生きる駒」にだけ持たせる。
/// カドで寿命が制約になったのと同じ壁（README「検証で分かったこと」）。
///
/// 代金は攻撃ではなく<b>味方の縛り1回ぶん</b>。しかも味方の縛りは <see cref="UnitState.AtkBonus"/> に
/// 永続蓄積するので、<b>第1ターンの1回が最も価値が高い</b>（以降の全ターンに効く）。
/// 号令・据えのある編成では +16 / +8 / −50% の収入の立ち上がりを1ターン遅らせることになり、
/// 号令も据えも無い編成では味方の縛りはほぼ純粋な損なので、敵へ向け直すのはむしろ得になる。
/// 同じ縄が編成によって正反対の意味を持つのが狙い。
/// </summary>
public sealed class BindTrait : Trait
{
    public const int Gain = 16;
    public override TraitId Id => TraitId.Bind;

    // 開戦時に縄を敵へ投げる。会戦では部隊戦ごとに1回走る（OnCarryOver は書かない——
    // 1部隊につき1回で、ReviverTrait.charges のような回数の持ち越しとは性質が違う）。
    public override void OnBattleStart(BattleContext ctx, UnitState self)
        => BindEnemy(ctx, self);

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        // 第1ターンは味方を縛らない。縄はもう敵に使ってある（縄は1本）。
        if (ctx.Turn == 1) return;
        BindAlly(ctx, self);
    }

    private static void BindAlly(BattleContext ctx, UnitState self)
    {
        var candidates = ctx.LivingMembers(self.TeamId)
            .Where(u => u != self && u.AcceptsSupport && u.Counter(StatusKeys.Stun) == 0)
            .ToList();
        if (candidates.Count == 0) return;

        UnitState victim = candidates[ctx.Roll(candidates.Count)];
        victim.SetCounter(StatusKeys.Stun, 1);
        ctx.Whet(victim, Gain, WhetRoute.Bind);
        ctx.Log($"    {self.Name} が {victim.Name} を縛りつけた（動けない / 攻撃 +{Gain}）", LogKind.FriendlyFire);
    }

    /// <summary>
    /// 大縛り。最も速い敵を確定で縛る。無作為ではなく最速を選ぶのは読めるから——
    /// 敵ロスター最速は勇者候補（速14・撃破ごとに雪だるま）なので、
    /// 「クグは勇者候補を止められる」がプレイヤーの学べる交互作用になる。
    ///
    /// 味方側の +16 は「縛られて力を溜める」意味なので敵には移さない。敵へは拘束のみ。
    /// </summary>
    private static void BindEnemy(BattleContext ctx, UnitState self)
    {
        var living = ctx.LivingMembers(ctx.Opponent(self.TeamId))
            .Where(u => u.Counter(StatusKeys.Stun) == 0)   // 既に縛られている敵に重ねても無駄
            .ToList();
        if (living.Count == 0) return;

        int top = living.Max(u => u.Def.Speed);
        var fastest = living.Where(u => u.Def.Speed == top).ToList();

        // 同速が複数なら無作為。スロット順で選ぶと位置バイアスが入る（既存の標的選択の作法）。
        UnitState victim = fastest[ctx.Roll(fastest.Count)];
        victim.SetCounter(StatusKeys.Stun, 1);
        ctx.Log($"    {self.Name} が {victim.Name} を縛り上げた（動けない）", LogKind.Trigger);
    }
}

/// <summary>据え。動かなかった味方の被ダメージを半減する。溜め軸に足りていなかった耐久。</summary>
public sealed class BulwarkTrait : Trait
{
    public const int ReductionPercent = 50;
    public override TraitId Id => TraitId.Bulwark;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
        => ctx.Log($"  {self.Name} が構えを整えた（動かない味方の被ダメージ -{ReductionPercent}%）", LogKind.Trigger);
}

/// <summary>移り木。動かされた味方を癒し強化する。隊列崩しを火力だけでなく耐久にも繋げる。</summary>
public sealed class DrifterTrait : Trait
{
    public const int Heal = 10;
    public const int Gain = 5;

    public override TraitId Id => TraitId.Drifter;

    public override void OnAllyMoved(BattleContext ctx, UnitState self, UnitState moved)
    {
        if (!moved.AcceptsSupport) return;
        ctx.Heal(moved, Heal);
        ctx.Whet(moved, Gain, WhetRoute.Drifter);
        ctx.Log($"    {self.Name} が流された {moved.Name} を拾い上げた（+{Heal} / 攻撃 +{Gain}）", LogKind.Trigger);
    }
}

/// <summary>
/// 逆しま。強化されると弱くなり、弱体化されると強くなる。
/// 呪詛の「味方にも漏れる」というマイナスを、そのまま利益に変える駒。
/// 支援を積む編成には決して入らない。
/// </summary>
public sealed class PerverseTrait : Trait
{
    public const int DebuffMultiplier = 3;

    public override TraitId Id => TraitId.Perverse;

    public override int ModifyAttack(UnitState self, int atk)
    {
        int b = self.AtkBonus;
        int baseAtk = self.Def.Attack;

        if (b < 0) return baseAtk + (-b) * DebuffMultiplier;  // 呪われるほど冴える
        if (b > 0) return Math.Max(1, baseAtk / 2);           // 讃えられると鈍る
        return baseAtk;
    }
}

/// <summary>
/// 分かち。味方が受けたダメージの一部を肩代わりする。
/// 庇う（Guardian）が単体攻撃しか止められないのに対し、こちらは薙ぎでも全体でも働く。
/// 範囲攻撃に対する唯一の耐久手段なので、配りすぎないこと。
///
/// 見返り（2026-08-22 追加、測定済み）: 肩代わり込みで受けたダメージに応じて攻撃力が上がる。
/// Rage（ムド）と同じ DamagePerGain=2 を流用（耐えるほど育つ、という同じ設計言語）。
/// 追加前は自分の火力もなく、肩代わりした分を取り返す経路が一枚もなかった
/// （「耐久役には必ず見返りの経路を付けろ」README参照、ドハはそこで名指しされていた張本人）。
///
/// 単体4編成の全配置探索（reseat）で現行配置が既に最良と確認済みなので、
/// 散開耐久が沈んでいたのは配置ではなく特性の欠陥だった。
///
/// | DamagePerGain | 散開耐久 (ササ×ドハ) 第1〜5波 | 反撃改 第5波 |
/// |---|---|--:|
/// | なし（旧仕様） | 100/65.5/5.0/0.0/0.0 | 33.5% |
/// | 3 | 100/96.5/49.0/19.5/7.0 | 39.5% |
/// | **2（採用）** | **100/98.5/79.5/44.5/14.5** | **40.5%** |
/// | 1 | 100/100/94.0/95.5/47.0 | 42.5% |
///
/// 1 まで下げると伸びすぎる（反撃改の他、単体で見ても入れ得に近づく）。2 は Rage と同じ比率で、
/// 「殴られて死にかけながら追いつく」Mudo の着地感と揃う。反撃改（カド×ドハ）は元から機能していた
/// 編成なので、そちらの動きは小さいことも確認済み（+7.0pt、他の波は無変化）。
/// この変更は Sharer 特性にしか触れていないので、ドハを含まない27編成は無変化（compare で確認済み）。
/// </summary>
/// <summary>
/// 砕け。範囲攻撃を浴びると、受けた分の半分を破片（アーマー）にして味方全員へ配る。
///
/// <para><b>庇う（Guardian）のちょうど裏返し。</b> 庇う・標的の介入は
/// <c>SelectTarget</c>（標的選択）で働くので Single にしか効かず、薙ぎ・全体の巻き込みや
/// レーンを走る貫きには一切触れない。実測で敵の攻撃力に占める範囲の割合は
/// 第四波15% / <b>第五波53%</b>で、そこが丸ごと素通りしていた。
/// こちらは damage の層にいるので、その素通りしていた側だけを拾う。
/// 代わりに単体攻撃には何も起きない。</para>
///
/// <para><b>見返りは味方に配る。</b> README の「純粋な防御役は編成の火力を殺す」
/// （RearGuard 単独で 26.0% → 12.0%）を踏まえると、引き受けるだけの駒は必ず沈む。
/// ただし攻撃力に変える形（ガルド・カド）はロスターに既に9箇所ある自己強化の10番目に
/// なるだけで、支援の穴（他人を癒せるのはノノ1体、駒ごとの被ダメ軽減はゼロ）は埋まらない。
/// 破片は回復とも攻撃力とも別の資源なので、そのどちらとも競合しない。</para>
///
/// <para><b>マイナスは脆弱（受けるダメージ5割増し）。</b> 罰ではなく燃料で、
/// 浴びる量が増えるぶん配れる量も増える。代金は自分のHPという有限プールから払われる。
/// 「積み上げは積んだ量に比例するコストを持つべき」（README）を、
/// コスト側を先に決めることで満たしている。</para>
/// </summary>
public sealed class ShatterTrait : Trait
{
    /// <summary>
    /// 浴びた量のうち破片に変わる割合。
    ///
    /// 感度は急峻ではないが、上下とも壁がある（README に表）。
    /// 50 だと総合98.8%で全編成中2位に立ち、新軸が既存を押しのける。
    /// 15 まで下げると第四波が 96% → 40% と崖になる（第四波は範囲が15%しかなく、
    /// ここを下回るとドルガが完走できなくなる）。
    ///
    /// **25 を採ったのは勝率の最大化ではない。** 持続的な範囲攻撃の波を
    /// タンク1枚で凌ぎ切れてしまうと、回復役を足す余地が消える。
    /// 残存が中位（3.1/5）に留まるのは不足ではなく、支援役をもう1枚置ける幅として意図している。
    /// </summary>
    public const int ConvertPercent = 25;

    // 破片に上限は設けていない。供給が「ヒビが浴びた量」に縛られていて、
    // ヒビのHPという有限プールがそのまま天井になるため（実測で1戦の被弾110、
    // 配れる破片は味方1体あたり30弱）。上限20/12/30 を振っても差が出なかったので、
    // 効いていない定数を残すより外した。変換率を50まで上げると初めて効き始める。

    public override TraitId Id => TraitId.Shatter;

    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        // 範囲攻撃のときだけ働く。source の型を見れば足りるので ApplyDamage の引数を増やさずに済む。
        // 毒・燃焼の継続ダメージは source が null なので自然に外れる（あれは範囲攻撃ではない）。
        if (source is null || source.CurrentPattern == AttackPattern.Single) return;

        int shards = dmg * ConvertPercent / 100;
        if (shards <= 0) return;

        int given = 0;
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            // **自分には配らない。** 配ると自分の被弾を自分で吸ってしまい、
            // 「浴びた量 = 配れる量」という関係が切れる（実測で変換が目に見えて鈍った）。
            // 庇う（ガルド）が自分を庇えないのと同じ形。
            if (ally == self) continue;

            // **AcceptsSupport を見ない。** 破片は回復でも強化でもなく damage 側で消費される
            // だけなので、Stoic（ガルドの「回復も強化も一切受け付けない」）を貫通する。
            // 「誰の助けも届かない」駒に唯一届く支援、というのがこの駒の存在理由。
            ally.SetCounter(StatusKeys.Armor, ally.Counter(StatusKeys.Armor) + shards);
            given += shards;

            // 鱗（第47期）の獲得の内訳。**盤面には触らない**——読み手が2つ目の供給源から
            // どれだけ受け取っているかを、死からの供給と分けて数えるためだけの1行。
            if (ally.HasTrait(TraitId.Scale)) ctx.NoteScaleGain(shards, ScaleSource.Shatter);
        }

        if (given > 0)
            ctx.Log($"    ★ {self.Name} が砕けて破片が飛んだ（味方へ {shards} ずつ）", LogKind.Highlight);
    }
}

public sealed class SharerTrait : Trait
{
    public const int Percent = 40;

    /// 肩代わりした量のうち、守った相手の攻撃力から差し引く割合の逆数。
    ///
    /// 分かちのマイナス（自分の火力が無い・早く尽きる）はドハ自身の中で閉じており、
    /// 置くだけで味方全員が常時4割減という入れ得の駒になっていた。
    /// マイナスが他者に及ばないので、マイナスを利益に変える駒と組む余地も無い。
    ///
    /// 痛みを取り上げられた者は腕がなまる、という形で他者にマイナスを及ぼす。
    /// - 逆しま（ウツ・クビ）は弱体化を利益に反転するので、ここが噛み合う軸になる
    /// - 棘（カド）は反撃量が攻撃力そのものなので、守られるほど刺し返せなくなる。
    ///   肩代わりで代金だけ肩代わりして反撃は満額、という形が正面から潰れる
    ///
    /// 下げ幅の総量は肩代わり総量に比例し、肩代わり総量はドハのHPで上限される。
    /// 支払い元が尽きた時点で効果も止まるので、無償の毎ターン収入にはならない。
    public const int DullDivisor = 4;

    /// <summary>被ダメージ何点につき攻撃力+1か（Rage と同じ比率）。</summary>
    public const int DamagePerGain = 2;

    public override TraitId Id => TraitId.Sharer;

    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        if (dmg <= 0 || !self.IsAlive) return;
        int gain = Math.Max(1, dmg / DamagePerGain);
        self.AtkBonus += gain;
        ctx.Log($"    {self.Name} が痛みを飲み込んだ（攻撃 +{gain} → {self.CurrentAttack}）", LogKind.Trigger);
    }
}

/// <summary>
/// 散開。同じ列で隣り合う味方がいない駒を硬くする。
/// 薙ぎは「隣接」に当たるので、これは隊列を散らすこと自体が対策になるという設計。
/// </summary>
public sealed class LooseTrait : Trait
{
    public const int ReductionPercent = 35;
    public override TraitId Id => TraitId.Loose;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
        => ctx.Log($"  {self.Name} が隊列を散らした（孤立した味方の被ダメージ -{ReductionPercent}%）", LogKind.Trigger);
}

/// <summary>
/// 萎縮。味方全体の攻撃力を下げる代わりに、被ダメージを下げる。
/// 普通の編成にとっては純粋なコストだが、弱体化を力に変える駒にとっては
/// 耐久と火力を同時に供給する相棒になる。
/// </summary>
public sealed class CowerTrait : Trait
{
    public const int AttackPenalty = 9;
    public const int ReductionPercent = 30;

    public override TraitId Id => TraitId.Cower;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
    {
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self) continue;
            foreach (UnitState t in ctx.SupportTargets(ally))
                ctx.Dull(t, AttackPenalty, DullRoute.Cower);
        }
        ctx.Log($"  {self.Name} の怯えが伝染した（味方全体 攻撃 -{AttackPenalty} / 被ダメージ -{ReductionPercent}%）", LogKind.FriendlyFire);
    }
}

/// <summary>
/// 追い打ち。味方が敵を倒したとき、ターン順を無視して攻撃する。
/// カドが「受けに回る火力」なのに対し、こちらは「割り込む火力」。
///
/// 2026-08-23: 「時間の上限」（1ターン1回）から「有限の代金」へ置き換えた。
/// そのターン最初の1発は無料。2発目以降は連続するたびに ChainCost の反動ダメージを払う。
/// `ApplyDamage` を通すのは、庇う・分かちなどの肩代わり規則と、被弾で育つ特性の両方に
/// 等しく反応させるため（CLAUDE.md の「ApplyDamage がダメージ処理の単一窓口」の原則）。
/// 上限は「生きている敵の数」と「自分のHP」がそのまま担う。`PerformAttack` は死んだ actor と
/// 空の盤面をそれぞれ弾いて自然に止まるので、別途カウンタで打ち切る必要はない。
///
/// **ChainCost は0（完全無料）が最も勝率が高いが、あえて5を採る（測定済み）。**
/// 反撃改3・追撃×毒で 0/5/10/15/20/30 を振ると、コストが上がるほど単調に勝率が落ちた
/// （反撃改3 平均 80.5%→78.7%→76.4%→73.5%→72.9%→70.9%）。「無制限にすると連鎖して
/// 盤面が一方的に終わる」という旧コメントの懸念は、この規模（敵5〜7体）では確認されなかった
/// （コスト0でも第五波は反撃改3 19.5%・追撃×毒 1.5%止まりで、全30編成中17位・21位が上限）。
/// **数字だけならコスト0が正解だが、それは「反動」という有限資源の縛りを実質無くすことに
/// なり、連鎖に自制を持たせるという設計意図そのものが消える。** コスト5はコスト0との差が
/// 1〜3pt程度（反撃改3 78.7% / 追撃×毒 75.3%）に収まるので、勝率をほぼ落とさずに
/// 「反動」というフレーバーと制約を残せる帯として採用した。
/// なお「追撃×死 (ハギ×リィカ)」はコストを何に振っても完全に無風（55.3%固定）。
/// この編成は1ターンに複数体を同時に葬れる火力源を持たないため、連鎖の引き金
/// （2体目以降の撃破）自体がほぼ発生しない。ChainCost の値の話ではなく、
/// 「連鎖を機能させるには、まず複数体同時撃破の種が要る」という構造の問題として残る。
/// </summary>
public sealed class PursuerTrait : Trait
{
    public const int ChainCost = 5;

    public override TraitId Id => TraitId.Pursuer;

    // 種別を問わず false。追い打ちは「自分の手番を丸ごと割り込みに賭ける」型なので、
    // 攻撃だけでなく手番そのものを持たない（不動とはここが違う）。
    public override bool CanAct(BattleContext ctx, UnitState self, ActionKind kind) => false;

    // 割り込みで振るのが役割。自分のターンを差し出したわけではない
    public override bool SurrendersTurn => false;

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        self.SetCounter("pursuit_chain", 0);
    }

    public override void OnAnyDeath(BattleContext ctx, UnitState self, UnitState dead)
    {
        if (dead.TeamId == self.TeamId) return;
        if (!ctx.CanActOutOfTurn(self)) return;   // 縛められている間は追い打てない

        int chain = self.Counter("pursuit_chain");
        if (chain > 0)
        {
            ctx.Log($"    {self.Name} は踏み込みすぎて体勢を崩す", LogKind.FriendlyFire);
            ctx.ApplyDamage(self, ChainCost, null, isFriendlyFire: true);
            if (!self.IsAlive) return;   // 反動で力尽きたらそこで打ち止め
        }
        self.SetCounter("pursuit_chain", chain + 1);

        ctx.Log($"    {self.Name} が倒れた隙に踏み込む", LogKind.Highlight);
        ctx.PerformAttack(self, "    ");
    }
}

/// <summary>
/// 後備え。後列の味方への攻撃を肩代わりする。「庇う（Guardian）」が単体攻撃しか止められないのに対し、
/// こちらは貫きにも割り込む。後列に稼ぎ頭を隠す編成に対する唯一の防御手段。
/// </summary>
public sealed class RearGuardTrait : Trait
{
    public const int RedirectPercent = 45;
    public override TraitId Id => TraitId.RearGuard;
}

/// <summary>
/// 火の粉。攻撃した相手に燃焼を付け、自分に隣接する味方にも燃え移る。
///
/// ボルグの巻き込み（<see cref="SplashTrait"/>）と同じ隣接規則に乗せてある。
/// 同じ一振りが敵を焼き、隣に立った味方にも移る、という一つの動作として読める形。
///
/// **主目標にしか付かないのは engine の規則（攻撃1回につき特性は1度）に従ったもの。**
/// 薙ぎで3体まとめて撒けるようにすると、範囲持ちが燃焼の供給を独占して
/// 「誰に着火役をやらせるか」という判断が消える。
/// </summary>
public sealed class CinderTrait : Trait
{
    public override TraitId Id => TraitId.Cinder;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        if (dealt <= 0) return;

        ctx.Ignite(target, source: self);

        // 味方に及ぶものなので前後を含む隣接を見る（Models.cs の AreAdjacent の但し書き）。
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self || !FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;
            ctx.Ignite(ally, friendly: true, source: self);
        }
    }
}

/// <summary>
/// 熾火。自分が燃えている間だけ本領を発揮し、火が消えるとほぼ無力になる。
///
/// 毒の変換役（ヴィオ・ラウ＝量を読む / ベニ・ミオ＝数を読む）とは読むものが違う。
/// **燃焼は非スタックなので「量」が存在せず、「数」を読むと撒いた時点で飽和する**
/// （ミオが没になった形）。残るのは時間・事象・自分の状態で、これは自分の状態を読む型。
///
/// 自分では着火できないので条件を自給できない。ボルグの火の粉は隣接した味方にしか
/// 移らないため、**ボルグの隣に置くという配置判断**が発動条件そのものになる。
/// 「配置でマイナスが消える」（庇い・後備え）の逆で、配置で条件を満たしにいく形。
///
/// 代金は燃焼ダメージ＋着火役の巻き込みで、実HPから毎ターン払う。
/// 燃焼が上限つきだからこそ低火力の駒でも払い続けられる、というのがこの軸の要。
///
/// 燃焼中の形は**薙ぎではなく貫き**。ボルグと軸が完全に重複していたため縦へ割った
/// （<see cref="CinderTrait"/> が隣接味方を点火するので、この2体は必ず同じターンに
/// 同じ形の面を出していた。スィドをグザの下位互換にした失敗と同型）。
/// 新しい能力を足すのではなく既にある形を差し替えたので、代金は既に払われている
/// （「自分では火を点けられない」＝点火役を編成に入れる、が条件として立っている）。
/// 総量は同等で（横に並ぶ敵には薙ぎが、奥行きのあるレーンには貫きが勝つ）、
/// **波によって優劣が逆転する**のが狙い。
///
/// 却下した案:
/// - **貫きをアクティブスキルとして足す**: ホタは既に「燃えている間だけ」という外部条件を
///   持つので、周期を重ねると二重条件になる（セロの後退＋後列、クグの2周期案と同じ稼働率不足）。
///   加えて押しのける対象が燃焼中の主力出力そのもの（ablate で -57.5pt）で押しのけ量が大きすぎる
/// - **燃焼にカウンタを持たせて資源化**: 燃焼は非スタックで「量」が存在しない（上記の結論をそのまま適用）
/// - **ボルグ側を貫きにする**: 散開＋火の粉は横に広がることを前提に設計されているので、
///   縦にすると点火の配り方ごと壊れる
/// </summary>
public sealed class PyreTrait : Trait
{
    public const int Multiplier = 4;

    public override TraitId Id => TraitId.Pyre;

    public override int ModifyAttack(UnitState self, int atk)
        => self.Counter(StatusKeys.Burn) > 0 ? atk * Multiplier : atk;

    public override AttackPattern ModifyPattern(UnitState self, AttackPattern p)
        => self.Counter(StatusKeys.Burn) > 0 ? AttackPattern.Pierce : p;
}

/// <summary>
/// 置き去り。自分の速さを境に味方を二分し、速い側を癒して遅い側を削る。
/// **同速には何も起きない**——速さを揃えれば無効化できる、という編成側の逃げ道。
///
/// ロスターで唯一 Def.Speed を条件として読む駒（BindTrait は最速敵の選択にしか使わない）。
/// 効き方を決めるのは保持者の速さそのもので、数値ではない。速さ7のナラなら
/// 削り16体 / 無風6体 / 回復13体 に割れる。**調整ノブは Heal/Toll ではなく Nara.Speed。**
///
/// 符号の向きは意図的に「速い側が得」。逆向き（遅い側が得）にすると、削られる先が
/// 速さ8以上の13体になり、そこに被弾変換器が1体もいない＝削りがただの損になる
/// （ムド5 / ガルド4 / ドハ3 / セッキ2 / ムグ6 / ゾト7 / リィカ7 は全て速さ7以下）。
///
/// 削りは ApplyDamage を通す。惨禍で1.5倍になり、巨躯・庇う・分かち・後備えに
/// 肩代わりされ、被弾強化の燃料になる——全て意図した帰結。「ApplyDamage が
/// ダメージ処理の単一窓口」の原則（CLAUDE.md）から外れないこと。
/// 棘（ThornsTrait）は同陣営の source を弾くので反撃は起きない（確認済み）。
/// </summary>
public sealed class ForsakeTrait : Trait
{
    public const int Heal = 5;
    public const int Toll = 5;

    public override TraitId Id => TraitId.Forsake;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
        => ctx.Log($"  {self.Name} は歩調を計った（速い味方 +{Heal} / 遅い味方 -{Toll}）", LogKind.Trigger);

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        if (!self.IsAlive) return;

        // スナップショットを取る。削りで味方が倒れると分裂・自爆・蘇生が
        // 列挙の途中で盤面を触る（LivingMembers は既にスナップショットを返すが、
        // 「この時点の生存者」で固定する意図をここで明示しておく）。
        // 並びは Shuffled 側を使う。席順に効果を適用すると、同時に倒れうる駒の
        // 死亡順が席番号で決まる（既存の作法：LivingMembersShuffled のコメント参照）。
        var others = ctx.LivingMembersShuffled(self.TeamId)
                        .Where(a => a != self)
                        .ToList();

        foreach (UnitState ally in others)
        {
            if (!ally.IsAlive) continue;          // 直前の削りの余波で落ちている場合がある

            if (ally.Def.Speed > self.Def.Speed)
            {
                ctx.Heal(ally, Heal);             // AcceptsSupport の判定は ctx.Heal が持つ
            }
            else if (ally.Def.Speed < self.Def.Speed)
            {
                ctx.ApplyDamage(ally, Toll, self, isFriendlyFire: true, lethal: true);
            }
            // 同速は何もしない
        }

        ctx.Log($"    {self.Name} は隊列を置き去りにした", LogKind.FriendlyFire);
    }
}

/// <summary>
/// 逆位。**保持者が盤上に生きている間だけ、行動順が速さ昇順になる。両陣営に等しくかかる。**
///
/// 他の特性と種類が違う。損得を持つ効果ではなく、盤面の読み方そのものを書き換える盤面ルールで、
/// 保持者自身は何の得もしない（むしろ速さ7なので反転下では遅い側＝先に動く不利側に回る）。
/// 非対称なのは「プレイヤーはこのルールを知って編成を組めるが、敵は組めない」点だけ。
///
/// **判定は engine 側（<c>BattleEngine.Run</c> の order を組む直前）に置いてある。**
/// この Trait 本体はログを出すだけ。順序は全員に一度にかかる盤面の状態なので、
/// 駒ごとのフックでは表現できない（ApplyDamage が肩代わりを解決するのと同じ理由）。
///
/// 毎ターン評価するので、保持者を倒せばルールは消える。ただし <c>order</c> はターン頭に
/// 1回だけ組むので、**戻るのは倒したターンではなく次のターンから**。
/// 速さが変わったときと同じ扱いで、ターンの途中で並びが変わることはない。
///
/// 既存特性との噛み合いは監査済み（design/ENEMY_REBUILD_PHASE2_PLAN.md §3）。
/// **縛め（<see cref="BindTrait"/>）の「最も速い敵を縛る」は、反転下では「最後に動く敵を縛る」に
/// 意味が裏返る。これは直さない**——波によって同じ駒の意味が変わるのが狙っている効果そのもの。
/// </summary>
public sealed class InversionTrait : Trait
{
    public override TraitId Id => TraitId.Inversion;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
        => ctx.Log($"  {self.Name} が盤面を逆さにした（行動順が速さの遅い順になる）", LogKind.Highlight);

    // HandleDeath は OnDeath の前に Hp = 0 を入れているので、この時点で保持者は既に
    // 生存判定から外れている（＝次のターンの order は正順で組まれる）。
    public override void OnDeath(BattleContext ctx, UnitState self)
        => ctx.Log($"    {self.Name} が倒れ、次のターンから行動順が戻る", LogKind.Highlight);
}

/// <summary>
/// 渇き: 保持者が盤上に生きている間、**回復が一切通らない**。両陣営に等しくかかる。
///
/// **判定は engine 側（<c>BattleContext.Heal</c> の入口）に置いてある。**
/// この Trait 本体はログを出すだけ。回復が通るかどうかは盤面の状態であって、
/// 回復される側の駒ごとのフックでは表現できない（逆位が order に置いてあるのと同じ理由）。
/// 回復の単一窓口が <c>ctx.Heal</c> なので、止める場所は 1 箇所で足りる。
///
/// **止めないもの**（意図的。詳細は <c>BattleContext.Heal</c> のコメント）:
/// 蘇生（<c>ctx.Revive</c> は Hp を直接書く）・破片（<c>StatusKeys.Armor</c> は
/// <c>ApplyDamage</c> 側で消費される別資源）・攻撃力の強化（回復ではない）。
/// 死軸と破片軸には無風のままにしてある——狙いは持続回復軸への課税で、
/// そこまで巻き込むと「何に課税したのか」が分離できなくなる。
///
/// 保持者を倒せば回復は戻る。<c>Heal</c> は呼ばれるたびに評価するので、
/// 逆位（order がターン頭に1回だけ組まれる）と違い**倒したその場から**戻る。
/// </summary>
public sealed class DroughtTrait : Trait
{
    public override TraitId Id => TraitId.Drought;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
        => ctx.Log($"  {self.Name} が盤面を渇かせた（両陣営の回復が通らなくなる）", LogKind.Highlight);

    // HandleDeath は OnDeath の前に Hp = 0 を入れているので、この時点で保持者は既に
    // 生存判定から外れている（＝この直後の Heal はもう通る）。
    public override void OnDeath(BattleContext ctx, UnitState self)
        => ctx.Log($"    {self.Name} が倒れ、回復が戻った", LogKind.Highlight);
}

/// <summary>
/// 軛: 保持者が盤上に生きている間、**1回のダメージで減る HP が上限（<see cref="Cap"/>）を超えない**。
/// 両陣営に等しくかかる。
///
/// <para><b>判定は engine 側（<c>BattleContext.ApplyDamage</c> の、HP を引く直前）に置いてある。</b>
/// この Trait 本体はログを出すだけ。1発の重さは全員に一度にかかる盤面の状態で、
/// 受け手ごとのフックでは表現できない（逆位が order に、渇きが <c>Heal</c> の入口にあるのと同じ理由）。</para>
///
/// <para><b>切るのは増減が全部終わった後。</b> 入口で切ると惨禍（<see cref="HavocTrait"/> +50%）や
/// 脆弱（<see cref="FrailTrait"/>）が上限を押し戻して「1発は Cap を超えない」が守られない。
/// 増幅が無効化されているように見えるのは正しい——それがこの規則の意味。</para>
///
/// <para><b>切らないもの（意図的）:</b>
/// <list type="bullet">
/// <item>破片（<c>StatusKeys.Armor</c>）: 上限<b>より前</b>に引かれる別資源のプール。破片が 10 吸って
/// 残り 30 なら切られるのは 30 → Cap で、<b>破片は上限の外側で効く</b>（ヒビの価値が上がる）</item>
/// <item>肩代わりの各段: 巨躯・分かち・棘守り・後備えで分割された段はそれぞれ別の
/// <c>ApplyDamage</c> 呼び出しなので、<b>段ごとに独立して切られる</b>＝分割は上限を回避する経路になる。
/// これは意図した帰結で、「重い一撃は分けて受けろ」が肩代わり役の存在理由になる</item>
/// <item>毒・燃焼の刻み: 除外しない。渇きが <c>source == null</c> を除外したのとは違い、こちらは
/// <b>「1発の重さ」に課金する規則</b>なので出どころは関係ない（墓守の層のように伸びる削りは上限に当たる）</item>
/// </list></para>
///
/// <para>保持者を倒せば上限は外れる。<c>ApplyDamage</c> は呼ばれるたびに評価するので、
/// 逆位（order はターン頭に1回だけ組む）と違い<b>倒したその場から</b>戻る。
/// 上限がかかっている間は保持者自身の HP も割りにくいので、
/// 「早く割れば上限が外れる」という勾配が自己言及的に立つ。</para>
/// </summary>
public sealed class YokeTrait : Trait
{
    /// <summary>
    /// 1回のダメージの上限。<b>唯一の調整ノブ</b>（保持者の数値 145/12/3 は触らない
    /// ——逆位の失敗の直接の原因がそこだった）。
    ///
    /// <para><b>25 は計画（15）ではなく実測で選んだ。</b> `yoke sweep` で 12〜50 を振ると、
    /// 12 は 21編成が 0%・15 は 16編成が 0%・20 でも第四波の平均が 87.0 → 45.6 と
    /// <b>第五波（59.8）より難しい波</b>になる。25 なら平均 61.8 で波の並びが
    /// 100 / 85.8 / 72.5 / 61.8 / 59.8 と単調に落ち、中間帯 7 → 11・固有の敗者 0 → 3・
    /// 第2波との相関 +0.62 → +0.31 が同時に立つ。上げすぎ（40 以上）だと固有の敗者が消える。</para>
    ///
    /// <para>敵側の打点（重装 12・詠唱兵の溜め 16・従軍司祭 9）は全部この下なので、
    /// <b>この波で課税されるのは味方の大打点だけ。</b></para>
    /// </summary>
    public const int Cap = 25;

    public override TraitId Id => TraitId.Yoke;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
    {
        // 診断が規則を切っている版（yoke の V1「壁のみ」）では何も起きないので、ログも出さない。
        if (!ctx.Yoke.Active) return;
        ctx.Log($"  {self.Name} が盤面に軛をかけた（1回のダメージが {ctx.Yoke.Cap} で切られる）",
                LogKind.Highlight);
    }

    // HandleDeath は OnDeath の前に Hp = 0 を入れているので、この時点で保持者は既に
    // 生存判定から外れている（＝この直後の ApplyDamage はもう切られない）。
    public override void OnDeath(BattleContext ctx, UnitState self)
    {
        if (!ctx.Yoke.Active) return;
        ctx.Log($"    {self.Name} が倒れ、軛が外れた", LogKind.Highlight);
    }
}

/// <summary>
/// 軛の規則。<b>診断（yoke）が版を並べて 1 回の実行の中で比べるためだけに外から差せる。</b>
/// 既定は <see cref="Default"/> ＝ <see cref="YokeTrait.Cap"/> で有効、<b>これが本採用の規則</b>。
/// 渡さない限り盤面は常にこの規則で動く。
///
/// <para><b>書き換え可能な static の調整ノブにしないこと。</b> Trait は共有シングルトンで、
/// layout は戦闘を並列実行する——static に置くと版の切り替えが他のスレッドの戦闘へ漏れるし、
/// <c>BattleEngine.Run</c> の「副作用も外部依存もない」もそこで壊れる
/// （<see cref="ColossusRule"/> と同じ判断）。</para>
///
/// <para><see cref="Active"/> は<b>保持者を盤上に置いたまま規則だけを外す</b>ための切り替え。
/// 「壁が変わったのか、ルールが効いたのか」の切り分けに要る——逆位はここを分けなかったせいで
/// 追加測定が必要になった。</para>
/// </summary>
public readonly record struct YokeRule(int Cap, bool Active)
{
    public static YokeRule Default => new(YokeTrait.Cap, Active: true);
}

/// <summary>
/// 粛: 保持者が盤上に生きている間、**ターン外の行動が一切通らない**。両陣営に等しくかかる。
///
/// <para><b>判定は engine 側（<c>BattleContext.CanActOutOfTurn</c>）に置いてある。</b>
/// この Trait 本体はログを出すだけ。ターン外に振れるかどうかは全員に一度にかかる盤面の状態で、
/// 振る側の駒ごとのフックでは表現できない（逆位が order に、渇きが <c>Heal</c> の入口に、
/// 軛が <c>ApplyDamage</c> に置いてあるのと同じ理由）。
/// ターン外の行動の単一窓口が <c>CanActOutOfTurn</c> なので、止める場所は 1 箇所で足りる。</para>
///
/// <para><b>この窓口を通る経路は 4 本だけ:</b>
/// <list type="bullet">
/// <item>棘（<see cref="ThornsTrait"/>・カド）: 殴られたら殴り返す</item>
/// <item>仇討ち（<see cref="AvengeTrait"/>・ザン）: 標的の味方が殴られたら刺し返す</item>
/// <item>軋み（<see cref="DisplacedTrait"/>・ヨミ）: 動かされた直後に割り込む</item>
/// <item>追い打ち（<see cref="PursuerTrait"/>・ハギ）: 味方が敵を倒したら割り込む</item>
/// </list></para>
///
/// <para><b>止めないもの</b>（意図的）:
/// <list type="bullet">
/// <item><b>肩代わり全種</b>（庇う・分かち・巨躯・後備え・棘守り）: これは<b>ダメージの再分配</b>で
/// あって行動ではない。<c>CanActOutOfTurn</c> を通らないので粛の下でも普通に働く</item>
/// <item><b>自分の手番内の追撃</b>: 責め苦（<see cref="TormentTrait"/>・シガ）は
/// <c>OnAfterAttack</c> ＝自分の手番の中なので<b>無風</b>。
/// **同じフェーズで作った読み手2体（ザン／シガ）が、この規則ひとつで割れる。**</item>
/// <item>毒・燃焼の刻み: <c>TickStatuses</c> はターン頭にまとめて走るので窓口を通らない</item>
/// </list></para>
///
/// <para>保持者を倒せばターン外の行動は戻る。<c>CanActOutOfTurn</c> は呼ばれるたびに評価するので、
/// 逆位（order はターン頭に1回だけ組む）と違い<b>倒したその場から</b>戻る。
/// 「早く割れば解禁される」という勾配が自己言及的に立つ——軛と同じ狙い。</para>
/// </summary>
public sealed class HushTrait : Trait
{
    public override TraitId Id => TraitId.Hush;

    public override void OnBattleStart(BattleContext ctx, UnitState self)
    {
        // 診断が規則を切っている版（hush の V1「壁のみ」）では何も起きないので、ログも出さない。
        if (!ctx.Hush.Active) return;
        ctx.Log($"  {self.Name} が盤面を鎮めた（両陣営のターン外の行動が通らなくなる）", LogKind.Highlight);
    }

    // HandleDeath は OnDeath の前に Hp = 0 を入れているので、この時点で保持者は既に
    // 生存判定から外れている（＝この直後の CanActOutOfTurn はもう通る）。
    public override void OnDeath(BattleContext ctx, UnitState self)
    {
        if (!ctx.Hush.Active) return;
        ctx.Log($"    {self.Name} が倒れ、ターン外の行動が戻った", LogKind.Highlight);
    }
}

/// <summary>
/// 粛の規則。<b>診断（hush）が版を並べて 1 回の実行の中で比べるためだけに外から差せる。</b>
/// 既定は <see cref="Default"/> ＝有効で、<b>これが本採用の規則</b>。渡さない限り盤面は常にこれ。
///
/// <para><b>調整ノブは持たない。</b> 軛の <c>Cap</c> に当たる連続量がここには無い
/// ——「ターン外に振れるか」は二値なので、帯を振る余地が構造的に存在しない。
/// 緩めるとしたら係数ではなく窓口の絞り込み（回数制限など）になるが、それは別の規則。</para>
///
/// <para><b>書き換え可能な static の調整ノブにしないこと。</b> Trait は共有シングルトンで、
/// layout は戦闘を並列実行する（<see cref="ColossusRule"/> / <see cref="YokeRule"/> と同じ判断）。</para>
///
/// <para><see cref="Active"/> は<b>保持者を盤上に置いたまま規則だけを外す</b>ための切り替え。
/// 「壁が変わったのか、ルールが効いたのか」の切り分けに要る。</para>
/// </summary>
public readonly record struct HushRule(bool Active)
{
    public static HushRule Default => new(Active: true);
}

public static class TraitCatalog
{
    private static readonly Dictionary<TraitId, Trait> Map = new Trait[]
    {
        new SplashTrait(),
        new CowardTrait(),
        new StoicTrait(),
        new SacrificeTrait(),
        new DrainTrait(),
        new SluggishTrait(),
        new FrailTrait(),
        new RageTrait(),
        new SniperTrait(),
        new CurseTrait(),
        new GuardianTrait(),
        new NecroTrait(),
        new ColossusTrait(),
        new ExecutionerTrait(),
        new SplitterTrait(),
        new BomberTrait(),
        new ReviverTrait(),
        new EphemeralTrait(),
        new VenomTrait(),
        new ThornsTrait(),
        new MarkerTrait(),
        new MenderTrait(),
        new AlmsTrait(),
        new ExposeTrait(),
        new SlanderTrait(),
        new ShoveTrait(),
        new BearTrait(),
        new RelayTrait(),
        new OverbearTrait(),
        new ScaleTrait(),
        new ScapegoatTrait(),
        new DivertTrait(),
        new GoadTrait(),
        new FinisherTrait(),
        new FavorTrait(),
        new FunnelTrait(),
        new AmplifierTrait(),
        new ContagionTrait(),
        new MiasmaTrait(),
        new ImmobileTrait(),
        new HavocTrait(),
        new ParalyzeTrait(),
        new DevourTrait(),
        new RallyTrait(),
        new BlightfedTrait(),
        new DisplacedTrait(),
        new ShufflerTrait(),
        new BindTrait(),
        new BulwarkTrait(),
        new DrifterTrait(),
        new PerverseTrait(),
        new SharerTrait(),
        new ShatterTrait(),
        new LooseTrait(),
        new CowerTrait(),
        new PursuerTrait(),
        new RearGuardTrait(),
        new CinderTrait(),
        new PyreTrait(),
        new CondemnTrait(),
        new ThornGuardTrait(),
        new ForsakeTrait(),
        new TormentTrait(),
        new AvengeTrait(),
        new RendTrait(),
        new GougeTrait(),
        new CarveTrait(),
        new SeverTrait(),
        new SutureTrait(),
        new FixateTrait(),
        new ThinBladeTrait(),
        new OverreachTrait(),
        new AwaitTrait(),
        new SealTrait(),
        new MartyrTrait(),
        new InversionTrait(),
        new DroughtTrait(),
        new YokeTrait(),
        new HushTrait()
    }.ToDictionary(t => t.Id);

    public static Trait Get(TraitId id) => Map[id];

    public static IReadOnlyList<Trait> Resolve(IEnumerable<TraitId> ids)
        => ids.Select(Get).ToList();
}
