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
    Colossus,    // 巨躯: 最大HPが極めて高い（数値で表現済み、フラグ用）
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
    Condemn      // 断罪: 反撃してきた相手を痺れさせる（敵側の語彙）
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
    /// <summary>自分のターンに動くか。false は「自分からは動かない」という設計であって、無力化とは限らない。</summary>
    public virtual bool CanAct(BattleContext ctx, UnitState self) => true;

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

/// <summary>大食い。毎ターン味方からHPを吸う。Necro と組むと味方の死が資源になる。</summary>
public sealed class DrainTrait : Trait
{
    public const int Amount = 4;
    public override TraitId Id => TraitId.Drain;

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        int gained = 0;
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId).ToList())
        {
            if (ally == self) continue;
            ctx.ApplyDamage(ally, Amount, self, isFriendlyFire: true);
            gained += Amount;
        }
        if (gained > 0)
        {
            ctx.Heal(self, gained);
            ctx.Log($"    {self.Name} が味方から精気を吸った（+{gained}）", LogKind.FriendlyFire);
        }
    }
}

/// <summary>のろま。奇数ターンしか動かない。</summary>
public sealed class SluggishTrait : Trait
{
    public override TraitId Id => TraitId.Sluggish;

    public override bool CanAct(BattleContext ctx, UnitState self)
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
            if (!ally.AcceptsSupport)
            {
                ctx.Log($"    {ally.Name} は呪詛を受け付けなかった", LogKind.Trigger);
                continue;
            }
            ally.AtkBonus -= AllyLeak;
        }
        ctx.Log($"    呪詛は味方にも漏れた（攻撃 -{AllyLeak}）", LogKind.FriendlyFire);
    }
}

/// <summary>庇う。前列にいるとき、味方への攻撃を一定確率で肩代わりする。</summary>
public sealed class GuardianTrait : Trait
{
    public const int RedirectPercent = 50;
    public override TraitId Id => TraitId.Guardian;
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

    /// <summary>層に応じた累積ボーナスを再計算して差分だけ反映する。</summary>
    private static void SetStack(BattleContext ctx, UnitState self, int stack, bool decayed)
    {
        stack = Math.Max(0, stack);
        int applied = self.Counter("necroBonus");
        int desired = AllyStep * stack * (stack + 1) / 2;
        self.AtkBonus += desired - applied;
        self.SetCounter("necro", stack);
        self.SetCounter("necroBonus", desired);
        if (decayed) ctx.Log($"    {self.Name} の層が薄れた（{stack}層）", LogKind.Status);
    }
}

/// <summary>巨躯。フラグ用。実効性能は MaxHp の数値側で表現する。</summary>
public sealed class ColossusTrait : Trait
{
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

/// <summary>自分からは攻撃しない。反撃役に持たせて「殴られなければ無価値」を成立させる。</summary>
public sealed class ImmobileTrait : Trait
{
    public override TraitId Id => TraitId.Immobile;

    public override bool CanAct(BattleContext ctx, UnitState self) => false;

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

    public override void OnTurnStart(BattleContext ctx, UnitState self)
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

    public override void OnTurnStart(BattleContext ctx, UnitState self)
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
            if (ally == self || !ally.AcceptsSupport) continue;
            ally.AtkBonus += OpeningGain;
        }
        ctx.Log($"  {self.Name} が鬨を上げた（味方全体 攻撃 +{OpeningGain}）", LogKind.Trigger);
    }

    public override void OnTurnStart(BattleContext ctx, UnitState self)
    {
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self || !ally.AcceptsSupport) continue;

            int idle = ally.Counter(StatusKeys.IdleTurn);
            if (idle <= 0 || idle != ctx.Turn - 1) continue;

            // 差し出したターンにだけ払う。不動（カド）は最初から振らない型で、
            // 差し出すものが無い。ここを見ないと静的なマイナスが毎ターンの収入になる。
            // 据え（Bulwark）は積み上がらない一定の減衰なので、こちらの制限はかけない。
            if (ally.Traits.Where(t => !t.CanAct(ctx, ally)).Any(t => !t.SurrendersTurn)) continue;

            ally.AtkBonus += Gain;
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
            if (ally == self || !ally.AcceptsSupport) continue;
            ally.AtkBonus -= AttackPenalty;
        }
        ctx.Log($"  {self.Name} の怯えが伝染した（味方全体 攻撃 -{AttackPenalty} / 被ダメージ -{ReductionPercent}%）", LogKind.FriendlyFire);
    }
}

/// <summary>
/// 追い打ち。味方が敵を倒したとき、ターン順を無視して攻撃する。
/// カドが「受けに回る火力」なのに対し、こちらは「割り込む火力」。
/// 1ターン1回に制限しないと、連鎖して盤面が一方的に終わる。
/// </summary>
public sealed class PursuerTrait : Trait
{
    public override TraitId Id => TraitId.Pursuer;

    public override bool CanAct(BattleContext ctx, UnitState self) => false;

    // 割り込みで振るのが役割。自分のターンを差し出したわけではない
    public override bool SurrendersTurn => false;

    public override void OnAnyDeath(BattleContext ctx, UnitState self, UnitState dead)
    {
        if (dead.TeamId == self.TeamId) return;
        if (!ctx.CanActOutOfTurn(self)) return;            // 縛められている間は追い打てない
        if (self.Counter("pursued") >= ctx.Turn) return;   // 1ターン1回

        self.SetCounter("pursued", ctx.Turn);
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
        new LooseTrait(),
        new CowerTrait(),
        new PursuerTrait(),
        new RearGuardTrait(),
        new CondemnTrait()
    }.ToDictionary(t => t.Id);

    public static Trait Get(TraitId id) => Map[id];

    public static IReadOnlyList<Trait> Resolve(IEnumerable<TraitId> ids)
        => ids.Select(Get).ToList();
}
