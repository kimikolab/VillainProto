namespace BattleCore;

// =====================================================================================
// レリック（第270期）—— 編成のとき駒に後付けする「繋ぎ」の札。
// 指示書は design/PHASE270_RELIC_FOUNDATION_SPEC.md ／ 報告は design/PHASE270_RELIC_FOUNDATION.md。
//
// **器**: `Formation.SetRelic(枠, 札)` で枠ごとに1枚まで。`BattleEngine.Materialize` が駒の札列の<b>末尾</b>に足す
// （`RelicCatalog.Attach`）。付けない枠は従来の1本道（`TraitCatalog.Resolve(def.Traits)`）をそのまま通る。
// **`UnitDef` は書き換えない**——`Def.Traits` に札は入らない（説明文・ロスターの棚卸し・転生の評価は素の駒のまま）。
// 札は `UnitState.Traits` に入るので、engine の「保持者がいれば立つ」門（`BattleContext.Add` の `HasTrait`）はそのまま立つ。
//
// **発火順**: 素の札の後ろ。同じフックで素の札が先に動く（例: ヨミの軋みが +9 してから軋む足が +2）。
// **重複**: 素の札と同じ札を付けたら<b>何も足さない</b>（積まない）。シングルトンを2度並べると同じフックが2回走るため。
//
// 原則（指示書 §0）: 駒の1文は駒のもの・ただ強くする数値の札は作らない・マイナスの札は読み手がいること・軸をまたぐ繋ぎを優先・
// 駒に依存する数値は汎用化してから。**レリックの測定結果は転生の評価に書き戻さない。**
// =====================================================================================

/// <summary>レリック1枚の表示用の情報（第270期）。<b>どの規則もこれを読まない。</b></summary>
/// <param name="Kind">繋ぎ ／ ゴミ ／ 変換。</param>
/// <param name="Bridge">何を何に変えるか（繋ぎ）／ 読み手（ゴミ）。</param>
public sealed record RelicInfo(TraitId Id, string Name, string Kind, string Text, string Bridge);

public static class RelicCatalog
{
    /// <summary>
    /// 第270期の7枚（第273期に名前を付けて固定した）。<b>第270〜272期の器具（`relic sweep ／ grid ／ rejudge`）はこれだけを回す</b>
    /// ——札を足しても過去の期の出力が変わらないため。
    /// </summary>
    public static IReadOnlyList<RelicInfo> Initial { get; } = new RelicInfo[]
    {
        new(TraitId.RelicCreak, "軋む足", "繋ぎ", $"動かされるたび攻撃力 +{RelicCreakTrait.Gain}", "移動 → 火力（ヨミの軋みの汎用化・割り込みは無い）"),
        new(TraitId.RelicFireArrow, "火付けの矢", "繋ぎ", "攻撃が当たった敵に火を点ける（燃焼）", "攻撃 → 燃焼"),
        new(TraitId.RelicVenomStep, "毒の足跡", "繋ぎ", $"動かされるたび、向かいの敵（自分のレーンの最前）に毒 {RelicVenomStepTrait.Amount}", "移動 → 毒"),
        new(TraitId.Spring, "弾性", "繋ぎ", "敵に殴られると、殴った敵を自分のレーンで1つ後ろへ弾く（既存の弾き返しの札そのもの・弾かれた敵は転倒する）", "被弾 → 敵の移動（ハネの弾き返しの流用）"),
        new(TraitId.RelicWilt, "萎える心", "ゴミ", $"敵に殴られて削られると攻撃力 −{RelicWiltTrait.Loss}", "読み手: ウツ（逆しま・弱体を強化に逆転）"),
        new(TraitId.RelicNumbStep, "痺れる足", "ゴミ", "動かされると次の手番を失う（転倒）", "読み手: ガン（号令・目覚まし＝手番を失った味方を買い取る）"),
        new(TraitId.RelicHarden, "身を固める", "変換", $"開戦時に素の攻撃力 × {RelicHardenTrait.HpPerAtk} を最大HPに足し、攻撃力は 0 になる", "壁・挑発役化（攻 → HP）"),
    };

    /// <summary>
    /// 札の版（第272期）。<b><see cref="All"/> には入れない</b>——第270・271期の器具（`relic sweep ／ grid`）が回す7枚は変えない。
    /// <see cref="Formation.SetRelic"/> は受け付ける（測りで差し替えるため）。版の切り替えは札そのものを差し替える（`Run` の引数は増やさない）。
    /// </summary>
    public static IReadOnlyList<RelicInfo> Versions { get; } = new RelicInfo[]
    {
        new(TraitId.RelicFireArrow3, "火付けの矢・3", "繋ぎ", $"攻撃が当たった敵に火を点ける（1戦 {RelicFireArrow3Trait.Shots} 回まで）", "攻撃 → 燃焼（第272期の版 G1・弾数制）"),
        new(TraitId.RelicFireArrow5, "火付けの矢・5", "繋ぎ", $"攻撃が当たった敵に火を点ける（1戦 {RelicFireArrow5Trait.Shots} 回まで）", "攻撃 → 燃焼（第272期の版 G2・弾数制）"),
    };

    /// <summary>
    /// 第273期に足した札（橋の多様化）。
    /// </summary>
    public static IReadOnlyList<RelicInfo> Added273 { get; } = new RelicInfo[]
    {
        new(TraitId.RelicShockStep, "帯電の足", "繋ぎ", "動かされたとき自分が感電していれば、向かいの敵（自分のレーンの最前）に感電を付ける", "移動 → 感電"),
        new(TraitId.RelicVenomShove, "押し毒", "繋ぎ", "毒を帯びた敵を殴ると、その敵を自分のレーンで1つ後ろへ押す（後ろの駒とは入れ替わる）", "毒 → 敵の移動"),
        new(TraitId.RelicOverflowEdge, "溢れの刃", "繋ぎ", "回復が溢れた（最大HPで切られた）分だけ、そのターン攻撃力 +同値", "回復 → 火力"),
        new(TraitId.RelicMomentum, "勢い余り", "繋ぎ", "敵を倒すと、隣の味方（席番号の小さいほう）と入れ替わる", "撃破 → 移動"),
        new(TraitId.RelicPoisonMagnet, "毒を招く", "ゴミ", $"受ける毒の層が {RelicPoisonMagnetTrait.Factor} 倍", "読み手: ベニ（結界の中では毒の刻みが回復）・ヴィオ（味方の毒を吸って攻撃力と腹に変える）"),
    };

    /// <summary>レリックとして付けられる札の一覧（<see cref="Initial"/> ＋ <see cref="Added273"/>）。<b>ここと <see cref="Versions"/> に無い札は <see cref="Formation.SetRelic"/> が弾く。</b></summary>
    public static IReadOnlyList<RelicInfo> All { get; } = Initial.Concat(Added273).ToArray();

    private static readonly Dictionary<TraitId, RelicInfo> Map = All.Concat(Versions).ToDictionary(r => r.Id);

    public static bool IsRelic(TraitId id) => Map.ContainsKey(id);
    public static RelicInfo Info(TraitId id) => Map[id];

    /// <summary>
    /// 駒の素の札に、レリックを1枚足した特性の列（<see cref="BattleEngine.Materialize(Formation, int, EnemyScaleRule)"/> だけが呼ぶ）。
    /// <b>末尾に足す</b>（発火順は素の札の後ろ）。<b>素の札と同じ札なら足さない</b>（積まない）。
    /// </summary>
    public static IReadOnlyList<Trait> Attach(IReadOnlyList<TraitId> own, TraitId relic)
        => own.Contains(relic) ? TraitCatalog.Resolve(own) : TraitCatalog.Resolve(own.Append(relic));
}

/// <summary>
/// 軋む足（第270期・レリック・繋ぎ: 移動 → 火力）。動かされるたび（誰が動かしても・自分で動いても）攻撃力 +2。
/// ヨミの軋み（<see cref="DisplacedTrait"/> の +9 ／ 突き出し +22 と割り込み）を<b>数値だけ汎用化して割り込みを外した</b>もの。
/// <b>自己強化なので <c>AtkBonus</c> を直に足す</b>（窓口 <c>Whet</c> を通さない＝横取りされない・支援拒否にも止められない）。
/// 会戦の境界で <c>AtkBonus</c> と一緒に消える（持ち越さない）。<b>逆しま（ウツ）に付けると強化として読まれ、攻撃が半減する</b>（規則どおり）。
/// </summary>
public sealed class RelicCreakTrait : Trait
{
    public override TraitId Id => TraitId.RelicCreak;
    public const int Gain = 2;

    public override void OnMoved(BattleContext ctx, UnitState self, Row from, Row to)
    {
        self.AtkBonus += Gain;
        ctx.Log($"    {self.Name} の軋む足（攻撃 +{Gain} → {self.CurrentAttack}）", LogKind.Trigger);
    }
}

/// <summary>
/// 火付けの矢（第270期・レリック・繋ぎ: 攻撃 → 燃焼）。自分の攻撃が敵に通ったら（<c>dealt &gt; 0</c>）、その敵に火を点ける。
/// 燃焼は層ではなく残りターンなので「燃焼 1」は「着火 1回」（<see cref="BattleContext.Ignite"/>・滲み則などの既存の規則がそのまま乗る）。
/// <b>ホタの熾（<see cref="CinderTrait"/>）から味方への漏れを外したもの。</b> 発火は攻撃1回につき主目標に1度（<c>OnAfterAttack</c> の規約どおり）。
/// </summary>
public sealed class RelicFireArrowTrait : Trait
{
    public override TraitId Id => TraitId.RelicFireArrow;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        if (dealt <= 0 || !target.IsAlive || target.TeamId == self.TeamId) return;
        ctx.Ignite(target, friendly: false, source: self);
        ctx.Log($"    {self.Name} の火付けの矢が {target.Name} に火を点けた", LogKind.Trigger);
    }
}

/// <summary>
/// 毒の足跡（第270期・レリック・繋ぎ: 移動 → 毒）。動かされるたび（誰が動かしても・自分で動いても）、
/// <b>向かいの敵</b>（自分の席が属するレーンそれぞれの、敵の最前の駒）に毒 1。中央はレーンが2本なので最大2体。
/// <b>敵は隣接しない</b>（隣接は同じ陣営の席の表）ので、指示書の「隣の敵」はレーンの向かいに読み替えた。<b>乱数を引かない。</b>
/// 書き込みは <see cref="BattleContext.Poison"/> の1箇所・経路は <see cref="PoisonRoute.Arrow"/>（状態の矢・保持者 0 枚）に相乗り
/// ——経路を1本足すと <c>SoakRouteCount</c> と燃焼の添字がずれるため（計数の列だけの話で、盤面には影響しない）。
/// </summary>
public sealed class RelicVenomStepTrait : Trait
{
    public override TraitId Id => TraitId.RelicVenomStep;
    public const int Amount = 1;

    public override void OnMoved(BattleContext ctx, UnitState self, Row from, Row to)
    {
        if (!self.IsAlive) return;
        int opp = ctx.Opponent(self.TeamId);
        var foes = ctx.LivingMembers(opp);
        if (foes.Count == 0) return;
        FormationShape foeShape = foes[0].Shape;
        var hit = new List<UnitState>(2);
        foreach (int lane in self.Shape.LanesOf(self.Slot))
        {
            if (lane >= foeShape.LaneCount) continue;
            var line = ctx.LaneMembers(opp, lane, foeShape);
            if (line.Count > 0 && !hit.Contains(line[0])) hit.Add(line[0]);
        }
        foreach (UnitState f in hit)
        {
            ctx.Poison(f, Amount, self, PoisonRoute.Arrow);
            ctx.Log($"    {self.Name} の毒の足跡が {f.Name} に毒を残した（+{Amount}）", LogKind.Status);
        }
    }
}

/// <summary>
/// 萎える心（第270期・レリック・ゴミ）。<b>敵に</b>殴られて HP が削られるたび（<c>OnDamaged</c>・出どころが敵の駒）攻撃力 −2。
/// <b>自分で背負う弱体なので <c>AtkBonus</c> を直に引く</b>（窓口 <c>Dull</c> を通すと熊・中継・集約に横取りされ、状態の数で深くなる）。
/// 刻み（燃焼・毒）・味方の巻き込み・生贄では下がらない。<b>読み手はウツ</b>（<see cref="PerverseTrait"/> は <c>AtkBonus &lt; 0</c> を強化に逆転する）。
/// 会戦の境界で <c>AtkBonus</c> と一緒に消える。
/// </summary>
public sealed class RelicWiltTrait : Trait
{
    public override TraitId Id => TraitId.RelicWilt;
    public const int Loss = 2;

    public override void OnDamaged(BattleContext ctx, UnitState self, int dmg, UnitState? source)
    {
        if (dmg <= 0 || source is null || source.TeamId == self.TeamId) return;
        self.AtkBonus -= Loss;
        ctx.Log($"    {self.Name} の心が萎えた（攻撃 −{Loss} → {self.CurrentAttack}）", LogKind.Trigger);
    }
}

/// <summary>
/// 痺れる足（第270期・レリック・ゴミ）。動かされるたび（誰が動かしても・自分で動いても）転倒（<see cref="StatusKeys.Stagger"/> = 1）
/// ——<b>次の手番だけを失う</b>（痺れと違ってターン外の行動は止めない）。手番を奪う状態を受け付けない駒（<c>ControlProof</c>）には入らない（<c>SetCounter</c> の規則どおり）。
/// <b>読み手はガン</b>（号令・目覚まし＝手番を失った味方を <c>IdleTurn</c> で読んで買い取る）。
/// <b>指示書が挙げたバンは今は読み手ではない</b>（据え〈<c>Bulwark</c>〉の保持者は 0 枚で、いまのバンは据わり〈<c>Planted</c>〉なので動かされない）。
/// </summary>
public sealed class RelicNumbStepTrait : Trait
{
    public override TraitId Id => TraitId.RelicNumbStep;

    public override void OnMoved(BattleContext ctx, UnitState self, Row from, Row to)
    {
        if (!self.IsAlive) return;
        self.SetCounter(StatusKeys.Stagger, 1);
        if (self.RawCounter(StatusKeys.Stagger) > 0)
            ctx.Log($"    {self.Name} は足が痺れて転んだ（次の手番を失う）", LogKind.Trigger);
    }
}

/// <summary>
/// 身を固める（第270期・レリック・変換: 攻 → HP）。開戦時に<b>素の攻撃力</b>（<c>Def.Attack</c>）× 3 を最大HP と HP に足し、攻撃力は以後ずっと 0
/// （<see cref="ModifyAttack"/> が 0 を返す。札列の末尾なので素の札の修正も強化も全部 0 に潰れる）。
/// <b>純増ではなく選択</b>——殴る力と強化の受け皿を捨てて壁になる。<c>Math.Max(1, …)</c> で攻撃力を読む経路（復讐など）は 1 になる（規則どおり）。
/// 会戦では同じ駒が次の戦闘にも立つので、<b>足すのは1度だけ</b>（私有のカウンタ・<c>OnCarryOver</c> でも消さない）。
/// </summary>
public sealed class RelicHardenTrait : Trait
{
    public override TraitId Id => TraitId.RelicHarden;
    public const int HpPerAtk = 3;
    const string Done = "relicHardenDone";

    public override void OnBattleStart(BattleContext ctx, UnitState self)
    {
        if (self.RawCounter(Done) > 0) return;
        int add = self.Def.Attack * HpPerAtk;
        self.SetCounter(Done, 1);
        self.MaxHp += add;
        self.Hp += add;
        ctx.Log($"    {self.Name} は身を固めた（最大HP +{add} → {self.MaxHp}・攻撃 0）", LogKind.Trigger);
    }

    public override int ModifyAttack(UnitState self, int atk) => 0;
}

/// <summary>
/// 火付けの矢・弾数制（第272期・版 G1 ／ G2）。<see cref="RelicFireArrowTrait"/> と同じ着火を、<b>1戦 <see cref="ShotsOf"/> 回まで</b>に絞る。
/// 数えるのは<b>実際に着火した回数</b>（攻撃が通らなかった・相手が倒れた手番は数えない）。尽きた後は攻撃が当たっても燃焼を付けない。
/// 私有のカウンタ（<c>StatusKeys.All</c> に入れない）で持ち、会戦の境界（<c>OnCarryOver</c>）で 0 に戻す——「1戦」は部隊戦1回。
/// <b>燃焼の脆さ・被弾の燃焼の規則本体には触らない</b>（減るのは着火の回数だけ。点いた火の持続・燃え上がりは G0 と同じ）。
/// </summary>
public abstract class RelicFireArrowCappedTrait : Trait
{
    public const string ShotsKey = "relicFireShots";
    protected abstract int ShotsOf { get; }

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        if (dealt <= 0 || !target.IsAlive || target.TeamId == self.TeamId) return;
        int used = self.RawCounter(ShotsKey);
        if (used >= ShotsOf) return;
        self.SetCounter(ShotsKey, used + 1);
        ctx.Ignite(target, friendly: false, source: self);
        ctx.Log($"    {self.Name} の火付けの矢が {target.Name} に火を点けた（残り {ShotsOf - used - 1}）", LogKind.Trigger);
    }

    public override void OnCarryOver(UnitState self) => self.SetCounter(ShotsKey, 0);
}

/// <summary>火付けの矢・3（第272期・版 G1）。</summary>
public sealed class RelicFireArrow3Trait : RelicFireArrowCappedTrait
{
    public override TraitId Id => TraitId.RelicFireArrow3;
    public const int Shots = 3;
    protected override int ShotsOf => Shots;
}

/// <summary>火付けの矢・5（第272期・版 G2）。</summary>
public sealed class RelicFireArrow5Trait : RelicFireArrowCappedTrait
{
    public override TraitId Id => TraitId.RelicFireArrow5;
    public const int Shots = 5;
    protected override int ShotsOf => Shots;
}

// =====================================================================================
// 第273期の札（橋の多様化・指示書 design/PHASE273_RELIC_ADOPT_EXPAND_SPEC.md）
// =====================================================================================

/// <summary>
/// 帯電の足（第273期・繋ぎ: 移動 → 感電）。動かされるたび（誰が動かしても・自分で動いても）、<b>自分が感電していれば</b>、
/// 向かいの敵（自分の席が属するレーンそれぞれの敵の最前）に感電を付ける（<see cref="BattleContext.MarkShock"/>・感電は二値なので「感電 1」＝ 付ける）。
/// 自分の感電は消さない（起爆は被弾の側の規則のまま）。<b>乱数を引かない。</b> <c>MarkShock</c> が保持者の門（<c>_shockLive</c>）を自分で立てる。
/// </summary>
public sealed class RelicShockStepTrait : Trait
{
    public override TraitId Id => TraitId.RelicShockStep;

    public override void OnMoved(BattleContext ctx, UnitState self, Row from, Row to)
    {
        if (!self.IsAlive || self.RawCounter(StatusKeys.Shock) <= 0) return;
        foreach (UnitState f in RelicLanes.Facing(ctx, self))
            if (ctx.MarkShock(f, self))
                ctx.Log($"    {self.Name} の帯電の足が {f.Name} に感電を移した", LogKind.Status);
    }
}

/// <summary>向かいの敵（第270期の毒の足跡と同じ読み替え: 自分の席が属するレーンそれぞれの、敵の最前の駒・重複なし）。</summary>
internal static class RelicLanes
{
    public static List<UnitState> Facing(BattleContext ctx, UnitState self)
    {
        var hit = new List<UnitState>(2);
        int opp = ctx.Opponent(self.TeamId);
        var foes = ctx.LivingMembers(opp);
        if (foes.Count == 0) return hit;
        FormationShape foeShape = foes[0].Shape;
        foreach (int lane in self.Shape.LanesOf(self.Slot))
        {
            if (lane >= foeShape.LaneCount) continue;
            var line = ctx.LaneMembers(opp, lane, foeShape);
            if (line.Count > 0 && !hit.Contains(line[0])) hit.Add(line[0]);
        }
        return hit;
    }
}

/// <summary>
/// 押し毒（第273期・繋ぎ: 毒 → 敵の移動）。自分の攻撃が通った主目標（<c>dealt &gt; 0</c>）が<b>毒を帯びていれば</b>、その敵を
/// 自分のレーンの経路で1つ後ろへ押す（行き先は弾き返しと同じ <see cref="SpringTrait.DestOf"/>・後ろに駒がいれば入れ替わる・据わり〈バン〉は動かない）。
/// <b>転倒は付けない</b>（弾き返しとの違い）。最後尾・経路に乗っていない敵は押さない。<b>乱数を引かない。</b>
/// 発火は攻撃1回につき主目標に1度（<c>OnAfterAttack</c> の規約どおり）。札は素の札の後ろなので、同じ手番で自分の札が付けた毒も「帯びている」に入る。
/// </summary>
public sealed class RelicVenomShoveTrait : Trait
{
    public override TraitId Id => TraitId.RelicVenomShove;

    public override void OnAfterAttack(BattleContext ctx, UnitState self, UnitState target, int dealt)
    {
        if (dealt <= 0 || !target.IsAlive || target.TeamId == self.TeamId || target.RawCounter(StatusKeys.Poison) <= 0) return;
        int dest = SpringTrait.DestOf(ctx, target);
        if (dest < 0) return;
        if (ctx.SwapSlots(target, dest, self))
            ctx.Log($"    {self.Name} の押し毒が {target.Name} を後ろへ押した", LogKind.Trigger);
    }
}

/// <summary>
/// 溢れの刃（第273期・繋ぎ: 回復 → 火力）。自分への回復が溢れた（最大HPで切られた）分だけ、<b>そのターン（全員の手番が一巡する間）</b>攻撃力 +同値。
/// 同じターンに何度溢れても足し合わせる。判定は engine の <see cref="BattleContext.Heal"/> の1行（全快で 0 しか増えなかった回復も溢れに数える・
/// 支援拒否・渇き・反転の裏で止まった回復は来ない・<b>ベニの反転の回復〈刻みが回復に変わったもの〉も回復として数える</b>）。
/// 強化は窓口 <c>Whet</c> を通さない自己強化（<see cref="ModifyAttack"/>・札列の末尾なので他の倍率の後に足す）。会戦の境界で消す。
/// </summary>
public sealed class RelicOverflowEdgeTrait : Trait
{
    public override TraitId Id => TraitId.RelicOverflowEdge;
    const string TurnKey = "relicOverflowTurn", AmtKey = "relicOverflowAmt";

    internal static void Gain(BattleContext ctx, UnitState self, int over)
    {
        int stamp = ctx.Turn + 1;
        int had = self.RawCounter(TurnKey) == stamp ? self.RawCounter(AmtKey) : 0;
        self.SetCounter(TurnKey, stamp);
        self.SetCounter(AmtKey, had + over);
        ctx.Log($"    {self.Name} の溢れの刃（攻撃 +{over} → このターン +{had + over}）", LogKind.Trigger);
    }

    public override int ModifyAttack(UnitState self, int atk)
        => self.Board is { } b && self.RawCounter(TurnKey) == b.Turn + 1 ? atk + self.RawCounter(AmtKey) : atk;

    public override void OnCarryOver(UnitState self) { self.SetCounter(TurnKey, 0); self.SetCounter(AmtKey, 0); }
}

/// <summary>
/// 勢い余り（第273期・繋ぎ: 撃破 → 移動）。敵を倒すと、隣の味方（隣接の表・召喚枠は除く・<b>席番号の小さいほう</b>）と入れ替わる。
/// 据わり（バン）しかいなければ入れ替わらない。選び方は踏み込みの代金（<see cref="OverrunTrait"/>）と同じで<b>乱数を引かない</b>（ヨミの撃破の衝撃は乱数を引くので真似ない）。
/// 押し出しの再入は <c>ctx.Shoving</c> で止める。倒した瞬間（攻撃の途中）に動くので、入れ替わった2体の移動の読み手はその場で動く。
/// </summary>
public sealed class RelicMomentumTrait : Trait
{
    public override TraitId Id => TraitId.RelicMomentum;

    public override void OnKill(BattleContext ctx, UnitState self, UnitState victim)
    {
        if (!self.IsAlive || victim.TeamId == self.TeamId) return;
        UnitState? with = null;
        foreach (UnitState ally in ctx.LivingMembers(self.TeamId))
        {
            if (ally == self || FormationRules.IsSummonSlot(ally) || ally.HasTrait(TraitId.Planted)) continue;
            if (!FormationRules.AreAdjacent(self, ally)) continue;
            if (with is null || ally.Slot < with.Slot) with = ally;
        }
        if (with is null) return;
        bool moved = false;
        ctx.Shoving(() => moved = ctx.SwapSlots(self, with.Slot, self));
        if (moved) ctx.Log($"    {self.Name} は勢い余って {with.Name} と入れ替わった", LogKind.Trigger);
    }
}

/// <summary>
/// 毒を招く（第273期・ゴミ）。受ける毒の層が2倍。判定は engine の <see cref="BattleContext.Poison"/> の1行（滲みの後・書く直前）
/// ——<b><c>ctx.Poison</c> を通らない書き込み</b>（ミオの濃縮・リリの口移し・身代わり・会戦の持ち越し）は2倍にならない。
/// <b>読み手</b>: ベニ（結界の中＝ベニ本人と隣の味方では、毒の刻みが層と同じ量の回復になる——層が2倍なら回復も2倍）・
/// ヴィオ（ターン頭に味方全員の毒を吸って攻撃力と腹に変える——ヴィオ以外の味方に付ければ吸う量が増える。ヴィオ本人は自分の毒を吸わない）。
/// </summary>
public sealed class RelicPoisonMagnetTrait : Trait
{
    public override TraitId Id => TraitId.RelicPoisonMagnet;
    public const int Factor = 2;
}
