namespace BattleCore;

/// <summary>
/// 戦闘中の盤面。特性はこれを通してのみ盤面に触る。
/// </summary>
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
    /// </summary>
    public bool CanActOutOfTurn(UnitState u)
        => u.IsAlive
           && u.Counter(StatusKeys.Stun) == 0
           && u.Traits.All(t => t.CanReact(this, u));

    public void Interrupt(Action body)
    {
        if (InInterrupt) return;
        InInterrupt = true;
        try { body(); }
        finally { InInterrupt = false; }   // 例外で立ちっぱなしになると以後の割り込みが永久に止まる
    }

    /// <summary>毒などの継続ダメージ。ターン開始時に engine から呼ばれる。</summary>
    public void TickStatuses()
    {
        foreach (UnitState u in _units.Where(x => x.IsAlive).ToList())
        {
            int poison = u.Counter(StatusKeys.Poison);
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
            ApplyDamage(u, poison, null);
        }

        // 燃焼は毒とは別のループで回す。固定量なので増幅も変換もされず、
        // 残りターンを減らすだけ。毒の後に置いてあるのは、同じターンに両方を負った駒が
        // 「積み上がる方」で先に落ちるようにするため（燃焼のほうが後から効く）。
        foreach (UnitState u in _units.Where(x => x.IsAlive).ToList())
        {
            int left = u.Counter(StatusKeys.Burn);
            if (left <= 0) continue;

            u.SetCounter(StatusKeys.Burn, left - 1);
            Log($"    {u.Name} が燃えている（残り {left - 1}）", LogKind.Status);
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.Status,
                Turn = _turn,
                TargetId = u.InstanceId,
                Amount = BurnRules.Damage,
                Text = "燃焼"
            });
            ApplyDamage(u, BurnRules.Damage, null);
        }
    }

    /// <summary>
    /// 着火。非スタックなので、量ではなく残りターンを更新する。
    /// 既に燃えている相手への再付与は持続のリセットにしかならない（ダメージは増えない）。
    /// </summary>
    public void Ignite(UnitState target, bool friendly = false)
    {
        if (!target.IsAlive) return;

        bool relit = target.Counter(StatusKeys.Burn) > 0;
        target.SetCounter(StatusKeys.Burn, BurnRules.Turns);
        Log(relit
                ? $"    {target.Name} の火が煽られた（残り {BurnRules.Turns}）"
                : $"    {target.Name} に火が点いた（残り {BurnRules.Turns}）",
            friendly ? LogKind.FriendlyFire : LogKind.Status);
    }

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

    private UnitTally TallyOf(UnitState u)
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

    public BattleContext(int seed, bool verbose)
    {
        _rng = new Random(seed);
        _verbose = verbose;
    }

    public IReadOnlyList<UnitState> AllUnits => _units;

    /// <summary>
    /// 盤面に駒を加える。増援・蘇生もここを通す（InstanceId を必ず振るため）。
    /// ID は verbose に関係なく振る。verbose のときだけ振ると、
    /// 一括シミュレーションと再生とで盤面の同一性が変わってしまう。
    /// </summary>
    internal void Add(UnitState u)
    {
        u.InstanceId = _nextInstanceId++;
        _units.Add(u);
    }

    public int Opponent(int teamId) => teamId == PlayerTeam ? EnemyTeam : PlayerTeam;

    /// <summary>
    /// 生存中の味方。必ずスナップショットを返す。
    /// 召喚や蘇生が特性の中から呼ばれるので、遅延評価のままだと列挙中に盤面が変わって落ちる。
    /// </summary>
    public IReadOnlyList<UnitState> LivingMembers(int teamId)
        => _units.Where(u => u.TeamId == teamId && u.IsAlive).ToList();

    public bool TeamAlive(int teamId) => LivingMembers(teamId).Any();

    public IReadOnlyList<LogLine> Log_ => _log;
    public IReadOnlyList<BattleEvent> Events => _events;

    /// <summary>
    /// 行頭の空白をインデント段数として取り込む。
    /// 呼び出し側は今まで通り空白付きの文字列を渡せばよい。
    ///
    /// 見せ場（Highlight）だけは構造化イベントにも流す。特性側は今まで通り
    /// ctx.Log を呼ぶだけでよく、演出の差し込み位置が自動的に台本へ乗る。
    /// </summary>
    public void Log(string line, LogKind kind = LogKind.Action)
    {
        if (!_verbose) return;
        int spaces = line.Length - line.TrimStart().Length;
        string text = line.Trim();
        _log.Add(new LogLine(kind, spaces / 2, text));

        if (kind == LogKind.Highlight)
            Emit(new BattleEvent { Kind = BattleEventKind.Highlight, Turn = _turn, Text = text });
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

    public int Roll(int maxExclusive) => _rng.Next(maxExclusive);

    /// <summary>攻撃対象を選ぶ。前列が生きている限り後列は狙われない。庇うはここで割り込む。</summary>
    /// <summary>
    /// 主目標を選ぶ。
    /// 庇う・標的の介入は「一人を狙う攻撃」＝ Single にしか効かない。
    /// 薙ぎや全体を体で止めることはできず、貫きは前列そのものを素通りする。
    /// この非対称が、単体高火力とそれ以外の使い分けを生む。
    /// </summary>
    public UnitState? SelectTarget(UnitState attacker)
    {
        List<UnitState> foes = LivingMembers(Opponent(attacker.TeamId)).ToList();
        if (foes.Count == 0) return null;

        AttackPattern pattern = attacker.CurrentPattern;

        if (pattern == AttackPattern.Pierce)
            return SelectPierceEntry(foes);

        // 前から順に、生き残っている最も前の列を狙う。
        List<UnitState> pool = foes.Where(f => f.Row == Row.Front).ToList();
        if (pool.Count == 0) pool = foes.Where(f => f.Row == Row.Mid).ToList();
        if (pool.Count == 0) pool = foes;

        UnitState target = pool[Roll(pool.Count)];

        UnitState? rearAny = foes.FirstOrDefault(
            f => f.HasTrait(TraitId.RearGuard) && f.Row == Row.Back && f != target);

        if (pattern != AttackPattern.Single)
        {
            // 後備えは範囲攻撃にも割り込む。貫きはレーン単位で解決するのでここを通らない。
            if (target.Row != Row.Front && rearAny is not null
                && Roll(100) < RearGuardTrait.RedirectPercent)
            {
                Log($"    {rearAny.Name} が後列の {target.Name} の前に入った", LogKind.Trigger);
                return rearAny;
            }
            return target;
        }

        UnitState? marked = foes.FirstOrDefault(f => f.Counter(StatusKeys.Marked) > 0);
        if (marked is not null && marked != target && Roll(100) < MarkPullPercent)
        {
            Log($"    敵は {marked.Name} に気を取られた", LogKind.Trigger);
            return marked;
        }

        UnitState? rear = foes.FirstOrDefault(
            f => f.HasTrait(TraitId.RearGuard) && f.Row == Row.Back && f != target);

        if (target.Row != Row.Front && rear is not null && Roll(100) < RearGuardTrait.RedirectPercent)
        {
            Log($"    {rear.Name} が後列の {target.Name} の前に入った", LogKind.Trigger);
            return rear;
        }

        UnitState? guardian = foes.FirstOrDefault(
            f => f.HasTrait(TraitId.Guardian) && f.Row == Row.Front && f != target);

        if (guardian is not null && Roll(100) < GuardianTrait.RedirectPercent)
        {
            Log($"    {guardian.Name} が {target.Name} を庇った", LogKind.Trigger);
            // 肩代わりで受けた分だけ伸びる（GuardianTrait 参照）。素の被弾と区別するための印。
            guardian.SetCounter(GuardianTrait.PendingKey, 1);
            return guardian;
        }

        return target;
    }

    /// <summary>
    /// 貫きが撃ち込むレーンを選び、その先頭（最も前の生存者）を返す。
    /// 後ろに誰かがいるレーンを優先する。「後ろに隠れる」への回答という役割を残すため。
    /// 隠れる者がいなければ、どのレーンでも構わない。
    /// </summary>
    private UnitState SelectPierceEntry(List<UnitState> foes)
    {
        var lanes = Enumerable.Range(0, FormationRules.LaneCount)
            .Where(l => foes.Any(f => FormationRules.LaneOf(f.Slot) == l))
            .ToList();

        var deep = lanes
            .Where(l => foes.Any(f => FormationRules.LaneOf(f.Slot) == l && f.Row != Row.Front))
            .ToList();

        List<int> pick = deep.Count > 0 ? deep : lanes;
        int lane = pick[Roll(pick.Count)];

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
        foreach (int slot in FormationRules.LaneTrack(lane))
            line.AddRange(alive.Where(u => u.Slot == slot));
        return line;
    }

    /// <summary>
    /// 一回の攻撃を最後まで解決する。通常のターン進行からも、追撃のようなターン外の割り込みからも呼ぶ。
    /// ターン順のループに攻撃処理を直書きすると、割り込み系の特性が一切書けなくなる。
    /// </summary>
    public void PerformAttack(UnitState actor, string prefix = "  ")
    {
        if (!actor.IsAlive) return;

        UnitState? target = SelectTarget(actor);
        if (target is null) return;

        int atk = actor.CurrentAttack;
        string label = actor.CurrentPattern switch
        {
            AttackPattern.Sweep => " 薙ぎ",
            AttackPattern.Pierce => " 貫き",
            AttackPattern.All => " 全体",
            _ => ""
        };
        // 手番の1回だけでなく、反撃・追い打ちのような手番外の攻撃もここを通る。
        // 「1ターンあたり何回振ったか」が、手番でしか動かない駒と反応する駒を分ける。
        TallyOf(actor).Attacks++;

        Log($"{prefix}{actor.Name} → {target.Name} (攻撃 {atk}{label})");
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Attack,
            Turn = _turn,
            ActorId = actor.InstanceId,
            TargetId = target.InstanceId,
            Amount = atk,
            Pattern = actor.CurrentPattern
        });

        if (actor.CurrentPattern == AttackPattern.Pierce)
        {
            ResolvePierce(actor, target, atk);
            return;
        }

        int dealt = atk;
        ApplyDamage(target, dealt, actor);

        foreach (UnitState extra in SecondaryTargets(actor, target))
        {
            if (!extra.IsAlive) continue;
            Log($"    刃が {extra.Name} まで届く", LogKind.Damage);
            ApplyDamage(extra, Math.Max(1, dealt * SecondaryPercent / 100), actor);
        }

        // 特性の発動は攻撃1回につき1度、主目標に対してのみ。
        // 範囲攻撃のたびに巻き込みや毒が複数回発動すると、範囲持ちが即座に壊れる。
        foreach (Trait t in actor.Traits.ToList())
            t.OnAfterAttack(this, actor, target, dealt);
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
    private void ResolvePierce(UnitState actor, UnitState entry, int atk)
    {
        int lane = FormationRules.LaneOf(entry.Slot);
        List<UnitState> line = LaneOccupants(LivingMembers(entry.TeamId), lane);

        int passed = 0;
        int primaryDealt = 0;

        foreach (UnitState u in line)
        {
            // 途中で倒れた駒はもう立ちはだかっていないので、減衰の数に入れない。
            if (!u.IsAlive) continue;

            int dmg = Math.Max(1, atk * Math.Max(0, 100 - PierceDecayPercent * passed) / 100);
            if (passed > 0)
                Log($"    刃は {u.Name} まで貫いた（威力 {dmg}）", LogKind.Damage);

            ApplyDamage(u, dmg, actor);
            if (u == entry) primaryDealt = dmg;
            passed++;
        }

        // 特性の発動は攻撃1回につき1度、レーンの先頭に対してのみ。
        // 貫いた全員に毒や巻き込みが乗ると、貫き持ちが即座に壊れる。
        foreach (Trait t in actor.Traits.ToList())
            t.OnAfterAttack(this, actor, entry, primaryDealt);
    }

    /// <summary>主目標以外に巻き添えになる敵。</summary>
    public IReadOnlyList<UnitState> SecondaryTargets(UnitState attacker, UnitState primary)
    {
        List<UnitState> foes = LivingMembers(Opponent(attacker.TeamId))
            .Where(f => f != primary).ToList();

        return attacker.CurrentPattern switch
        {
            // 薙ぎは横へ広がる。前後へ広げると貫きと区別がつかなくなる。
            AttackPattern.Sweep => foes
                .Where(f => FormationRules.AreLateralNeighbors(primary.Slot, f.Slot)).ToList(),
            AttackPattern.All => foes,
            _ => Array.Empty<UnitState>()
        };
    }

    /// <summary>
    /// ダメージ処理の単一窓口。味方からの巻き込みも生贄もここを通る。
    /// だから「被弾で強くなる」駒が、敵の攻撃でも味方の事故でも等しく反応する。
    /// </summary>
    public void ApplyDamage(UnitState target, int amount, UnitState? source,
                            bool isFriendlyFire = false, bool lethal = true)
    {
        if (!target.IsAlive || amount <= 0) return;

        foreach (Trait t in target.Traits)
            amount = t.ModifyIncomingDamage(target, amount);

        // 惨禍は「本人ではなく味方全体」に効くので、駒の特性ではなく盤面側で解決する。
        var teammates = LivingMembers(target.TeamId);

        if (teammates.Any(u => u.HasTrait(TraitId.Havoc)))
            amount += amount * HavocTrait.Percent / 100;

        // 据え: このターン動けなかった駒は硬くなる
        if (target.Counter(StatusKeys.IdleTurn) >= Turn
            && teammates.Any(u => u.HasTrait(TraitId.Bulwark)))
            amount -= amount * BulwarkTrait.ReductionPercent / 100;

        // 散開: 同じ列に隣り合う味方がいない駒は硬くなる。薙ぎへの対策。
        if (teammates.Any(u => u.HasTrait(TraitId.Loose))
            && !teammates.Any(u => u != target && FormationRules.AreAdjacent(target.Slot, u.Slot)))
            amount -= amount * LooseTrait.ReductionPercent / 100;

        // 萎縮: 火力と引き換えの被ダメージ減
        if (teammates.Any(u => u.HasTrait(TraitId.Cower)))
            amount -= amount * CowerTrait.ReductionPercent / 100;

        if (amount <= 0) return;

        // 分かち: 型を問わず肩代わりする。庇うと違い、薙ぎや全体でも働く。
        // 味方同士の巻き込み（isFriendlyFire）も引き受ける。ここを除外していたとき、
        // ドハはカドの代金（敵からの被弾）だけを4割肩代わりして守り、収入源（味方への巻き込み）は
        // 満額通していた。都合のいい側だけを助ける形になっていたので条件を外した。
        // 肩代わり先が自分自身になる再帰は下の HasTrait(Sharer) で止まる。
        if (!target.HasTrait(TraitId.Sharer))
        {
            UnitState? sharer = teammates.FirstOrDefault(u => u.HasTrait(TraitId.Sharer) && u != target);
            if (sharer is not null)
            {
                int taken = amount * SharerTrait.Percent / 100;
                if (taken > 0)
                {
                    amount -= taken;
                    Log($"    {sharer.Name} が {target.Name} の痛みを引き受けた", LogKind.Trigger);
                    ApplyDamage(sharer, taken, source, isFriendlyFire: true);

                    // 痛みを取り上げられた者は腕がなまる。肩代わり量に比例させているので、
                    // 代金はドハのHPという有限プールから払われる（SharerTrait.DullDivisor 参照）。
                    // 切り捨てのままにしてあるのは、Math.Max(1, ...) にすると小さいダメージの
                    // 連打で比例関係が崩れ、下げ幅が肩代わり量から切り離されるため。
                    int dull = taken / SharerTrait.DullDivisor;
                    if (dull > 0)
                    {
                        target.AtkBonus -= dull;
                        Log($"    痛みを取り上げられた {target.Name} の腕がなまる（攻撃 -{dull}）",
                            LogKind.FriendlyFire);
                    }
                }
            }
        }

        if (!lethal) amount = Math.Min(amount, Math.Max(0, target.Hp - 1));
        if (amount <= 0) return;

        target.Hp -= amount;
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
            FriendlyFire = isFriendlyFire
        });

        if (source is not null && !isFriendlyFire)
        {
            DamageByUnit.TryGetValue(source.Def.Id, out int prev);
            DamageByUnit[source.Def.Id] = prev + amount;
        }

        // 与ダメージは敵と味方を分けて数える。混ぜると破裂・生贄・吸いのような
        // 「味方を削ることで仕事をする駒」が出力の大きい優等生に見えてしまう。
        if (source is not null)
        {
            UnitTally st = TallyOf(source);
            st.Interventions++;
            if (isFriendlyFire || source.TeamId == target.TeamId) st.DamageToAlly += amount;
            else st.DamageToEnemy += amount;
        }

        UnitTally tt = TallyOf(target);
        tt.DamageTaken += amount;
        if (source is not null && (isFriendlyFire || source.TeamId == target.TeamId))
            tt.TakenFromAlly += amount;

        foreach (Trait t in target.Traits.ToList())
            t.OnDamaged(this, target, amount, source);

        if (target.Hp <= 0)
            HandleDeath(target, source);
    }

    private void HandleDeath(UnitState dead, UnitState? killer)
    {
        dead.Hp = 0;
        TallyOf(dead).Deaths++;
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

        if (dead.TeamId == EnemyTeam)
        {
            _enemyKillsThisTurn++;
            if (_enemyKillsThisTurn > MaxEnemyKillsInOneTurn) MaxEnemyKillsInOneTurn = _enemyKillsThisTurn;
        }

        if (killer is not null && killer.IsAlive)
            foreach (Trait t in killer.Traits.ToList())
                t.OnKill(this, killer, dead);

        // 本人の死亡時効果（分裂など）
        foreach (Trait t in dead.Traits.ToList())
            t.OnDeath(this, dead);

        // 敵味方を問わない死亡通知。墓守はこちらを見る。
        foreach (UnitState u in _units.Where(u => u.IsAlive).ToList())
            foreach (Trait t in u.Traits.ToList())
                t.OnAnyDeath(this, u, dead);

        // 味方限定の通知。蘇生はこちらで、墓守が強化を得た後に走る。
        foreach (UnitState ally in LivingMembers(dead.TeamId).ToList())
        {
            if (ally == dead) continue;
            foreach (Trait t in ally.Traits.ToList())
                t.OnAllyDeath(this, ally, dead);
        }
    }

    /// <summary>
    /// 空きスロットに増援を出す。空きが無ければ何も起きない。
    /// 空きの判定は生死を問わない。死者の枠を「空き」と見なすと、
    /// 増援がそこへ入った後に蘇生が走って1枠に2体が立つため。
    /// </summary>
    public UnitState? Summon(UnitDef def, int teamId)
    {
        var taken = _units.Where(u => u.TeamId == teamId).Select(u => u.Slot).ToHashSet();
        int slot = -1;
        for (int i = 0; i < FormationRules.TotalSlots; i++)
            if (!taken.Contains(i)) { slot = i; break; }
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
            TargetId = unit.InstanceId,
            Slot = slot,
            HpAfter = unit.Hp,
            Team = teamId,
            Text = def.Name
        });
        return unit;
    }

    /// <summary>倒れた駒を戦線に戻す。無制限にすると壊れるので回数制限は特性側で持つこと。</summary>
    public void Revive(UnitState target, int hp)
    {
        if (target.IsAlive) return;
        target.Hp = Math.Max(1, hp);
        target.AtkBonus = 0;
        Log($"    {target.Name} が繋ぎ直された（HP {target.Hp}）", LogKind.Summon);
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Revive,
            Turn = _turn,
            TargetId = target.InstanceId,
            Slot = target.Slot,
            HpAfter = target.Hp
        });
    }

    public void Heal(UnitState target, int amount)
    {
        if (!target.IsAlive || amount <= 0) return;
        if (!target.AcceptsSupport) return;

        int before = target.Hp;
        target.Hp = Math.Min(target.MaxHp, target.Hp + amount);
        if (target.Hp == before) return;

        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Heal,
            Turn = _turn,
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
        var slots = FormationRules.SlotsOfRow(next.Value).ToList();

        // 味方がいるなら必ず入れ替える（＝前へ押し出す）。
        // 空きへ逃げるだけだと誰も損をせず、逃亡が純粋な利益になってしまう。
        foreach (int slot in slots)
            if (team.Any(u => u.Slot == slot && u != self)) return slot;

        foreach (int slot in slots)
            if (team.All(u => u.Slot != slot)) return slot;

        return null;
    }

    /// <summary>
    /// 隊列を入れ替える。移動した駒すべてに OnMoved を通知するので、
    /// 逃亡・喧噪・庇いのどれが原因でも「動かされた」駒は等しく反応できる。
    /// </summary>
    public void SwapSlots(UnitState self, int destSlot)
    {
        UnitState? occupant = LivingMembers(self.TeamId).FirstOrDefault(u => u.Slot == destSlot);
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
                TargetId = u.InstanceId,
                Slot = u.Slot,
                HpAfter = u.Hp
            });

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
                    t.OnAllyMoved(this, ally, u);
            }

            foreach (Trait t in u.Traits.ToList())
                t.OnMoved(this, u, from, u.Row);
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
    public static BattleResult Run(Formation player, Formation enemy, int seed, bool verbose = true)
    {
        var ctx = new BattleContext(seed, verbose);

        Deploy(ctx, player, BattleContext.PlayerTeam);
        Deploy(ctx, enemy, BattleContext.EnemyTeam);

        ctx.Log("=== 戦闘開始 ===", LogKind.System);

        foreach (UnitState u in ctx.AllUnits.OrderByDescending(u => u.Def.Speed).ToList())
        {
            if (!u.IsAlive) continue;
            foreach (Trait t in u.Traits.ToList())
                t.OnBattleStart(ctx, u);
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

            foreach (UnitState u in ctx.AllUnits.Where(x => x.IsAlive).ToList())
                foreach (Trait t in u.Traits.ToList())
                    t.OnTurnStart(ctx, u);

            // 素早さ順。同値はチーム→スロットで安定させる（再現性のため）
            var order = ctx.AllUnits
                .Where(u => u.IsAlive)
                .OrderByDescending(u => u.Def.Speed)
                .ThenBy(u => u.TeamId)
                .ThenBy(u => u.Slot)
                .ToList();

            foreach (UnitState actor in order)
            {
                if (!actor.IsAlive) continue;
                if (!ctx.TeamAlive(ctx.Opponent(actor.TeamId))) break;

                if (actor.Counter(StatusKeys.Stun) > 0)
                {
                    actor.SetCounter(StatusKeys.Stun, 0);
                    actor.SetCounter(StatusKeys.IdleTurn, turn);
                    ctx.Log($"  {actor.Name} は痺れて動けない", LogKind.Status);
                    continue;
                }

                bool canAct = actor.Traits.All(t => t.CanAct(ctx, actor));
                if (!canAct)
                {
                    // 動けなかったことを記録する。ただし「差し出したターン」だけを数える。
                    // 不動（カド）・追い打ち（ハギ）は最初から自分のターンに振らない型なので、
                    // ここで数えると号令・据えが無償の毎ターン収入になる（Trait.SurrendersTurn 参照）。
                    actor.SetCounter(StatusKeys.IdleTurn, turn);
                    continue;
                }

                ctx.PerformAttack(actor);
            }
        }

        bool playerWon = ctx.TeamAlive(BattleContext.PlayerTeam)
                         && !ctx.TeamAlive(BattleContext.EnemyTeam);

        ctx.Log(playerWon ? "=== 勝利 ===" : "=== 敗北 ===", LogKind.System);

        return new BattleResult
        {
            PlayerWon = playerWon,
            Turns = Math.Min(turn, MaxTurns),
            Log = ctx.Log_.ToList(),
            PlayerSurvivors = ctx.LivingMembers(BattleContext.PlayerTeam).Count(),
            DamageByUnit = new Dictionary<string, int>(ctx.DamageByUnit),
            TallyByUnit = new Dictionary<string, UnitTally>(ctx.TallyByUnit),
            MaxEnemyKillsInOneTurn = ctx.MaxEnemyKillsInOneTurn,
            Events = ctx.Events.ToList()
        };
    }

    private static void Deploy(BattleContext ctx, Formation formation, int teamId)
    {
        foreach ((int slot, UnitDef def) in formation.Occupied())
        {
            ctx.Add(new UnitState
            {
                Def = def,
                TeamId = teamId,
                Slot = slot,
                Hp = def.MaxHp,
                MaxHp = def.MaxHp,
                Traits = TraitCatalog.Resolve(def.Traits)
            });
        }
    }
}
