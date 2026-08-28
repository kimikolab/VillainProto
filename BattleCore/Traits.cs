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

    // --- プラス側 ---
    Rage,        // 被弾強化: ダメージを受けるたび攻撃力が上がる
    Sniper,      // 後衛特化: 一度後退してから後列にいると攻撃力2倍＋貫き化
    Curse,       // 呪詛: 開始時に敵全体の攻撃力を下げる（隣接味方にも漏れる）
    Guardian,    // 庇う: 味方への攻撃を肩代わりする
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
    ThornGuard   // 棘守り: 前か横の味方への単体攻撃を身代わりし、その味方と位置を入れ替える
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

        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
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

        UnitState? pushed = ctx.LivingMembers(self.TeamId).FirstOrDefault(u => u.Slot == dest.Value);
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
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId).ToList())
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
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId).ToList())
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
            foe.AtkBonus -= EnemyDebuff;
        }
        ctx.Log($"  {self.Name} の呪詛が敵全体を蝕む（攻撃 -{EnemyDebuff}）", LogKind.Trigger);

        // 漏れは味方全体へ。角に置いて隣を空ければ無償、という抜け道を塞ぐ。
        // これで呪詛は「支援を受け付けない駒」か「火力に頼らない構成」を要求するようになる。
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self) continue;
            foreach (UnitState t in ctx.SupportTargets(ally))
                t.AtkBonus -= AllyLeak;
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
public sealed class GuardianTrait : Trait
{
    public const int RedirectPercent = 50;

    /// <summary>被ダメージ何点につき攻撃力+1か（Rage・分かちと同じ比率）。</summary>
    public const int DamagePerGain = 2;

    /// <summary>engine が肩代わりの成立を伝えるための印。次の被弾1回で消費する。</summary>
    public const string PendingKey = "guardPending";

    public override TraitId Id => TraitId.Guardian;

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
/// <para><b>列の前後で効き目が変わる。</b> 前列に置けば中衛と後列を守り、中衛なら後列だけ、
/// 後列に置けば誰も守らない。5体を6枠に入れる限り必ず1枠空くので
/// 「デメリットの隣を空ける」が常に最適解になる、という既存の穴（README の未解決の課題）に対して、
/// **置き場所そのものが効き目を決める**駒になっている。</para>
/// </summary>
public sealed class ColossusTrait : Trait
{
    /// <summary>後ろの味方への攻撃を何割引き受けるか。</summary>
    public const int Percent = 90;

    public override TraitId Id => TraitId.Colossus;
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
    public override TraitId Id => TraitId.Splitter;

    public override void OnDeath(BattleContext ctx, UnitState self)
    {
        for (int i = 0; i < 2; i++)
            ctx.Summon(UnitCatalog.Spore, self.TeamId);
    }
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

        foreach (UnitState foe in ctx.LivingMembers(ctx.Opponent(self.TeamId)))
            ctx.ApplyDamage(foe, EnemyBlast, self);

        // 味方も巻き込む。これが他の駒の起点になる。
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self) continue;
            ctx.ApplyDamage(ally, AllyBlast, self, isFriendlyFire: true);
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

        source.SetCounter(StatusKeys.Poison, source.Counter(StatusKeys.Poison) + StackPerHit);
        ctx.Log($"    {source.Name} の毒が {source.Counter(StatusKeys.Poison)} 層になった", LogKind.Status);

        // 扱いが雑なので隣の味方にもかかる。漏れは前後を含む隣接（味方に及ぶものの定義）。
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self || !FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;
            ally.SetCounter(StatusKeys.Poison, ally.Counter(StatusKeys.Poison) + 1);
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

        // 反撃は範囲。自分から攻撃できず打点が自分しかない駒なので、
        // 見返りをここまで大きくして初めて軸として成立する。
        ctx.Reaction(() =>
        {
            ctx.Log($"    {self.Name} の棘が {source.Name} を刺し返す", LogKind.Trigger);
            ctx.ApplyDamage(source, back, self);

            foreach (UnitState other in ctx.LivingMembers(source.TeamId))
            {
                if (other == source) continue;
                // 敵に及ぶ範囲なので横のみ。味方に及ぶものと定義を分けている。
                if (!FormationRules.AreLateralNeighbors(source.Slot, other.Slot)) continue;
                ctx.ApplyDamage(other, Math.Max(1, back * SplashPercent / 100), self);
            }

            // 棘は味方も巻き込み、巻き込んだぶんだけ据わる。
            // 上昇源を「味方に与えた実ダメージ」だけに限っているのは、隣の味方が全員倒れたあとも
            // 敵ダメージで無償に伸び続ける穴を作らないため（号令がカドに毎ターン +8 を
            // 無償で払っていたのと同じ形になる）。代金は常に隣接味方のHPで支払われる。
            // 味方に及ぶ範囲は前後を含む（ボルグと同じ AreAdjacent）。敵に及ぶ範囲の左右のみとは定義を分けている。
            int spill = Math.Max(1, self.Def.Attack * FriendlySplashPercent / 100);
            int gained = 0;

            foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
            {
                if (ally == self) continue;
                if (!FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;

                int before = ally.Hp;
                ctx.Log($"    余波: {self.Name} の棘が {ally.Name} を巻き込む", LogKind.FriendlyFire);
                ctx.ApplyDamage(ally, spill, self, isFriendlyFire: true);
                gained += (before - ally.Hp) / GainDivisor;
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
    /// <para><b>値は暫定。</b> 敵の攻撃力分布は 荷駄7 / 重装12 / 槍騎17 / 狙撃18 / 勇者20 / 巡礼24 で、
    /// 16 は「荷駄・重装は完全に受け止め、槍騎以上には貫かれる」位置。方向を確かめるために
    /// 8 と 16 の2点だけを測った結果を採ったもので、最適値ではない（味方・敵の双方に今後調整が入る）。</para>
    /// </summary>
    public const int AbsorbCap = 16;

    /// <summary>構えている印。スキルの手番に立て、1回の肩代わりで消費する。</summary>
    public const string PendingKey = "thornGuardPending";

    /// <summary>
    /// 肩代わりした相手のスロット。<b>+1 して格納し、0 を「なし」とする</b>
    /// （<see cref="UnitState.Counters"/> は int で、未設定と スロット0 が区別できないため）。
    /// </summary>
    public const string PartnerKey = "thornGuardPartner";

    public override TraitId Id => TraitId.ThornGuard;

    /// <summary>
    /// 守れる位置か。<b>横</b>＝同じ列の隣、<b>前</b>＝同じレーンの1つ手前。
    ///
    /// <see cref="FormationRules"/> の既存の関数だけで書く。<b>新しい幾何関数を足さない</b>
    /// ——盤面の形は1箇所（FormationRules）にしか無い、という前提を崩すと
    /// 「隣接とは何か」の定義が特性ごとに散る。
    /// </summary>
    public static bool Covers(UnitState self, UnitState ally)
    {
        if (FormationRules.AreLateralNeighbors(self.Slot, ally.Slot)) return true;

        // 前だけ。同じレーンの後ろにいる味方は守らない（守れると後列から前列を
        // 素通しで守れることになり、隊列の意味が消える）。
        return FormationRules.AreDepthNeighbors(self.Slot, ally.Slot)
               && FormationRules.DepthOf(ally.Row) < FormationRules.DepthOf(self.Row);
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
        UnitState? partner = ctx.LivingMembers(self.TeamId)
            .FirstOrDefault(u => u != self && u.Slot == dest);

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
        UnitState? mark = ctx.LivingMembers(self.TeamId)
            .Where(a => a != self && FormationRules.AreAdjacent(self.Slot, a.Slot))
            .OrderByDescending(a => a.MaxHp)
            .FirstOrDefault();

        if (mark is null)
        {
            ctx.Log($"  {self.Name} は囃し立てる相手がいなかった", LogKind.Action);
            return;
        }

        mark.SetCounter(StatusKeys.Marked, 1);
        ctx.Log($"  {self.Name} が {mark.Name} を敵の前に押し出した", LogKind.Trigger);
    }
}

/// <summary>継ぎ当て。回復量と同じだけ自分が減る。等価交換なので無限には支えられない。</summary>
public sealed class MenderTrait : Trait
{
    public const int Amount = 14;

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

        UnitState? patient = ctx.LivingMembers(self.TeamId)
            .Where(a => a != self && a.AcceptsSupport && a.Hp < a.MaxHp)
            .OrderBy(a => a.Hp * 100 / Math.Max(1, a.MaxHp))
            .FirstOrDefault();
        if (patient is null) return;

        int amount = Math.Min(Amount, self.Hp - 1);
        ctx.Heal(patient, amount);
        self.Hp -= amount;
        ctx.Log($"    {self.Name} が自分を裂いて {patient.Name} を繕った（+{amount}）", LogKind.Trigger);
    }
}

/// <summary>澱み。既に積まれた毒を増幅する。毒が無ければ何もしない。</summary>
public sealed class AmplifierTrait : Trait
{
    public const int Step = 4;

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
        foreach (UnitState foe in ctx.LivingMembers(ctx.Opponent(self.TeamId)))
        {
            int poison = foe.Counter(StatusKeys.Poison);
            if (poison <= 0) continue;

            // 加算にすること。乗算だと戦闘が長引くほど指数的に伸びて、
            // 後から数値で抑えるのが不可能になる。
            int grown = poison + Step;
            foe.SetCounter(StatusKeys.Poison, grown);
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
            foe.SetCounter(StatusKeys.Poison, foe.Counter(StatusKeys.Poison) + spread);

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

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        var foes = ctx.LivingMembers(ctx.Opponent(self.TeamId));
        if (foes.Count == 0) return;

        foreach (UnitState foe in foes)
            foe.SetCounter(StatusKeys.Poison, foe.Counter(StatusKeys.Poison) + PerTurn);

        // 瘴気は敵味方を選ばない。撒く側にも代償を負わせる。
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
            ally.SetCounter(StatusKeys.Poison, ally.Counter(StatusKeys.Poison) + AllyLeak);

        ctx.Log($"    {self.Name} が瘴気を撒いた（敵 毒 +{PerTurn} / 味方 毒 +{AllyLeak}）", LogKind.FriendlyFire);
    }
}

/// <summary>
/// 惨禍。味方全体の被ダメージが増える。自分だけが損をするマイナスは編成の幅を生まないが、
/// 味方全体に及ぶマイナスは「被弾を利益に変える駒」すべての燃料になる。
/// </summary>
public sealed class HavocTrait : Trait
{
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
                t.AtkBonus += OpeningGain;
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
            // 据え（Bulwark）は積み上がらない一定の減衰なので、こちらの制限はかけない。
            // 種別は Attack 固定で問う。号令はターン開始に走るので ally.ActionIndex は
            // すでに次の行動へ進んでいて、そこの周期を読んでも「先ほど手番を差し出したか」の
            // 答えにはならない。ここが訊いているのは「その駒が通常の手番で殴りに行く型か」
            // なので、周期の現在位置ではなく種別の固定が正しい。
            if (ally.Traits.Where(t => !t.CanAct(ctx, ally, ActionKind.Attack))
                           .Any(t => !t.SurrendersTurn)) continue;

            // 条件（差し出したターンかどうか）は ally 側で見て、乗せる先は拡散を通す。
            // 拡散持ちは自分では受け取らないが、差し出した事実は本人のものなので判定は動かさない。
            foreach (UnitState t in ctx.SupportTargets(ally))
                t.AtkBonus += Gain;
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

    public override void OnMoved(BattleContext ctx, UnitState self, Row from, Row to)
    {
        bool pushedForward = FormationRules.DepthOf(to) < FormationRules.DepthOf(from);
        int gain = pushedForward ? PushedToFrontGain : Gain;
        self.AtkBonus += gain;
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
/// </summary>
public sealed class BindTrait : Trait
{
    public const int Gain = 16;
    public override TraitId Id => TraitId.Bind;

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        var candidates = ctx.LivingMembers(self.TeamId)
            .Where(u => u != self && u.AcceptsSupport && u.Counter(StatusKeys.Stun) == 0)
            .ToList();
        if (candidates.Count == 0) return;

        UnitState victim = candidates[ctx.Roll(candidates.Count)];
        victim.SetCounter(StatusKeys.Stun, 1);
        victim.AtkBonus += Gain;
        ctx.Log($"    {self.Name} が {victim.Name} を縛りつけた（動けない / 攻撃 +{Gain}）", LogKind.FriendlyFire);
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
        moved.AtkBonus += Gain;
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
                t.AtkBonus -= AttackPenalty;
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

        ctx.Ignite(target);

        // 味方に及ぶものなので前後を含む隣接を見る（Models.cs の AreAdjacent の但し書き）。
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self || !FormationRules.AreAdjacent(self.Slot, ally.Slot)) continue;
            ctx.Ignite(ally, friendly: true);
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
/// </summary>
public sealed class PyreTrait : Trait
{
    public const int Multiplier = 4;

    public override TraitId Id => TraitId.Pyre;

    public override int ModifyAttack(UnitState self, int atk)
        => self.Counter(StatusKeys.Burn) > 0 ? atk * Multiplier : atk;

    public override AttackPattern ModifyPattern(UnitState self, AttackPattern p)
        => self.Counter(StatusKeys.Burn) > 0 ? AttackPattern.Sweep : p;
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
        new ThornGuardTrait()
    }.ToDictionary(t => t.Id);

    public static Trait Get(TraitId id) => Map[id];

    public static IReadOnlyList<Trait> Resolve(IEnumerable<TraitId> ids)
        => ids.Select(Get).ToList();
}
