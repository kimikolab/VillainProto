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
    /// 全キーの一覧。会戦（Engagement）が部隊戦の境界で状態異常を一律に消すために使う
    /// （状態異常は Battle スコープ、という寿命規則。Armor も含めて消す——破片は
    /// Battle 内の供給に依存するプール）。**新しいキーを足したら必ずここにも足すこと。**
    /// </summary>
    public static readonly string[] All = { Poison, Marked, Stun, Burn, IdleTurn, Armor, Wound };

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
    public bool CanActOutOfTurn(UnitState u)
        => u.IsAlive
           && u.Counter(StatusKeys.Stun) == 0
           && u.Traits.All(t => t.CanReact(this, u))
           && !(Hush.Active && AllUnits.Any(x => x.IsAlive && x.HasTrait(TraitId.Hush)));

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
            // 業（第49期）の帰属。**保持者が盤上にいなければ1回も走らない**（短絡）。
            if (ScapegoatActive) NoteScapegoatDot(u, poison, StatusKeys.Poison);
            ApplyDamage(u, poison, null);
        }

        // 燃焼は毒とは別のループで回す。固定量なので増幅も変換もされず、
        // 残りターンを減らすだけ。毒の後に置いてあるのは、同じターンに両方を負った駒が
        // 「積み上がる方」で先に落ちるようにするため（燃焼のほうが後から効く）。
        foreach (UnitState u in _units.Where(x => x.IsAlive).ToList())
        {
            int left = u.Counter(StatusKeys.Burn);
            if (left <= 0) continue;

            // 燃焼の計数（第57期）。**盤面には触らない。**
            UnitTally bt = TallyOf(u);
            bt.BurnTicks++;

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
            if (ScapegoatActive) NoteScapegoatDot(u, BurnRules.Damage, StatusKeys.Burn);
            // burnTick: この刻みが破片に吸われた量・HP を削った量を、
            // 毒の刻み（同じく source が null）と混ぜずに数えるための札。**盤面には影響しない。**
            ApplyDamage(u, BurnRules.Damage, null, burnTick: true);
            if (!u.IsAlive) bt.BurnDeaths++;
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

        // 燃焼の計数（第57期）。**盤面には触らない。**
        // 「点いた」と「煽られた」を分けるのが要点——非スタックなので後者は
        // 残ターンを 3 に戻すだけで、供給としては捨てられている。
        UnitTally it = TallyOf(target);
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
        int o = u.Counter(owed);
        if (o <= 0) return;
        if (kind == StatusKeys.Poison)
        {
            int stacks = u.Counter(StatusKeys.Poison);
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
            if (u.Counter(StatusKeys.Burn) >= o) return;
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
        int o = u.Counter(owed);
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
        bool marked = target.Counter(StatusKeys.Marked) > 0;
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
            if (!u.IsAlive || u.Counter(StatusKeys.Marked) <= 0) { u.SetCounter(FinisherSinceKey, 0); continue; }
            if (u.Counter(FinisherSinceKey) == 0) u.SetCounter(FinisherSinceKey, Turn);
        }
    }

    internal void NoteFinisherIdle() => FinisherIdle++;

    internal void NoteFinisherFire(UnitState target, bool crossed)
    {
        FinisherFires++;
        if (crossed) FinisherCross++;
        int since = target.Counter(FinisherSinceKey);
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
            if (f.TeamId != PlayerTeam && f.IsAlive && f.Counter(StatusKeys.Marked) > 0) return;
        FinisherStarved++;
    }

    public BattleContext(int seed, bool verbose, ColossusRule? colossus = null, YokeRule? yoke = null,
                         HushRule? hush = null, MartyrRule? martyr = null, ExposeRule? expose = null,
                         ShoveRule? shove = null, BearRule? bear = null,
                         RelayRule? relay = null, SlanderRule? slander = null,
                         OverbearRule? overbear = null, ScaleRule? scale = null,
                         ScapegoatRule? scapegoat = null, DivertRule? divert = null,
                         GoadRule? goad = null, FinisherRule? finisher = null)
    {
        _rng = new Random(seed);
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

        return LivingMembers(u.TeamId)
            .Where(a => a != u && a.AcceptsSupport && FormationRules.AreAdjacent(u.Slot, a.Slot))
            .ToList();
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
    public UnitState? MostHurtAlly(UnitState self)
    {
        var hurt = LivingMembers(self.TeamId)
            .Where(a => a != self && a.AcceptsSupport && a.Hp < a.MaxHp).ToList();
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
                int v = u.Counter(key);
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
            Emit(new BattleEvent
            {
                Kind = BattleEventKind.StatSnapshot,
                Turn = _turn,
                TargetId = u.InstanceId,
                Amount = u.CurrentAttack,
            });
        }
    }

    /// <summary>スナップショットに出す継続効果と、その表示名。</summary>
    private static readonly (string Key, string Label)[] StatusLabels =
    {
        (StatusKeys.Poison, "毒"),
        (StatusKeys.Burn, "燃"),
        (StatusKeys.Stun, "痺"),
        (StatusKeys.Marked, "標"),
        (StatusKeys.Armor, "盾"),
        (StatusKeys.Wound, "傷"),
    };

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

        List<UnitState> foes = LivingMembers(Opponent(attacker.TeamId)).ToList();
        if (foes.Count == 0) return null;

        AttackPattern pattern = patternOverride ?? attacker.CurrentPattern;

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
            : PickOne(foes.Where(f => f.Counter(StatusKeys.Marked) > 0).ToList());

        // 止めのときは **`marked == target` でもここで返す**——標の段は鎖の1段目なので、
        // 庇い・後備え・殉教・棘守りを飛び越すのは標が元から持つ性質。
        // 「たまたま無作為の主目標が標持ちだった」ときだけ介入を許すのは非対称になる。
        if (marked is not null && (finisher || (marked != target && Roll(100) < MarkPullPercent)))
        {
            if (marked == target) return marked;   // 差し替えていないので鎖の計数は動かさない

            // 業（第49期）が敵へ書いた標が実際に引いた回数。**engine が標の読み手**なので、
            // 「敵側に読み手がいない」＝「効かない」ではない（第48期の棚卸しが数えたのは駒）。
            if (ScapegoatActive && marked.Counter(ScapegoatTrait.OwedKey(StatusKeys.Marked)) > 0)
                ScapegoatMarkPulls++;
            // 逸らし（第50期）。**engine の鎖が実際に主目標を差し替えた回数**を陣営別に数える。
            // ログの「気を取られた」と1対1で対応する（あちらは verbose のときしか出ない）。
            if (DivertActive)
            {
                if (attacker.TeamId == PlayerTeam) DivertAllyPulls++;
                else DivertFoePulls++;
            }
            Log($"    敵は {marked.Name} に気を取られた", LogKind.Trigger);
            return marked;
        }

        UnitState? rear = PickOne(foes.Where(
            f => f.HasTrait(TraitId.RearGuard) && f.Row == Row.Back && f != target).ToList());

        if (target.Row != Row.Front && rear is not null && Roll(100) < RearGuardTrait.RedirectPercent)
        {
            Log($"    {rear.Name} が後列の {target.Name} の前に入った", LogKind.Trigger);
            return rear;
        }

        UnitState? guardian = PickOne(foes.Where(
            f => f.HasTrait(TraitId.Guardian) && f.Row == Row.Front && f != target).ToList());

        if (guardian is not null && Roll(100) < GuardianTrait.RedirectPercent)
        {
            Log($"    {guardian.Name} が {target.Name} を庇った", LogKind.Trigger);
            // 肩代わりで受けた分だけ伸びる（GuardianTrait 参照）。素の被弾と区別するための印。
            guardian.SetCounter(GuardianTrait.PendingKey, 1);
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
                 && f.Counter(ThornGuardTrait.PendingKey) > 0
                 && ThornGuardTrait.Covers(f, target)).ToList());

        if (thornGuard is not null)
        {
            Log($"    {thornGuard.Name} が {target.Name} の前に棘を差し出した", LogKind.Trigger);
            thornGuard.SetCounter(ThornGuardTrait.PendingKey, 0);
            // スロット + 1 を格納し、0 を「なし」とする（スロット0 と未設定の区別）
            thornGuard.SetCounter(ThornGuardTrait.PartnerKey, target.Slot + 1);
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
        => PoolOf(LivingMembers(Opponent(attacker.TeamId)).ToList());

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
    public void PerformAttack(UnitState actor, string prefix = "  ",
                              int attackPercent = 100, AttackPattern? patternOverride = null)
    {
        if (!actor.IsAlive) return;

        AttackPattern pattern = patternOverride ?? actor.CurrentPattern;

        UnitState? target = SelectTargetCore(actor, patternOverride, out int pierceLane);
        if (target is null) return;

        // 逸らし（第50期）。**焦点の効きは「付けた回数」ではなく「実際にそこへ振られた割合」。**
        // 標は単体攻撃にしか効かないので、分母も単体振りだけで数える。
        if (DivertActive && pattern == AttackPattern.Single) NoteDivertSwing(actor, target);

        // CurrentAttack 自体は変えない。AtkBonus と混ぜると会戦の境界処理（第1期 D2/D3）や
        // 墓守の層の再適用と衝突する。
        // 100 のときに分岐を残してあるのは、攻撃力 0 の駒を 0 のまま通すため
        // （素の値をそのまま返す経路が、倍率導入前と1命令も違わないことの保証にもなる）。
        int atk = attackPercent == 100
            ? actor.CurrentAttack
            : actor.CurrentAttack * attackPercent / 100;

        // 止め（第53期）。**倍率は攻撃の解決時に掛ける**——`Trait.ModifyAttack` は対象を
        // 受け取らないので「相手が標を持つか」で分岐できない（`ModifyAttack` に書くと
        // 標の無い相手にも倍率が乗り、供給とのサイクルが消えて単なる高打点の駒になる）。
        // **単体攻撃だけ**（薙ぎ・全体・貫きは engine の鎖が標を1ビットも見ない）。
        //
        // **「発火」と「列越え」を分けて数える。** 列越え＝`PoolOf` の外（中列・後列）を殴った回数で、
        // 「標が無ければ狙えなかった敵」。標が持つ特権を実際に使えたかはこちらでしか読めない。
        if (FinisherActive && pattern == AttackPattern.Single && actor.HasTrait(TraitId.Finisher))
        {
            if (target.Counter(StatusKeys.Marked) > 0)
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
        // 燃えている状態で振った回数（第57期・Attacks の内数）。熾火の稼働率の分子。
        if (actor.Counter(StatusKeys.Burn) > 0) TallyOf(actor).BurnAttacks++;

        // 鱗（第47期）。**振った回数と、そのうち貫きだった回数を分けて数える。**
        // 「貫き」は成果ではないので、後列に当たった回数は ResolvePierce の側で別に数える。
        if (actor.HasTrait(TraitId.Scale))
        {
            ScaleSwings++;
            if (pattern == AttackPattern.Pierce) ScalePierceSwings++;
        }

        Log($"{prefix}{actor.Name} → {target.Name} (攻撃 {atk}{label})");
        Emit(new BattleEvent
        {
            Kind = BattleEventKind.Attack,
            Turn = _turn,
            ActorId = actor.InstanceId,
            TargetId = target.InstanceId,
            Amount = atk,
            Pattern = pattern
        });

        if (pattern == AttackPattern.Pierce)
        {
            ResolvePierce(actor, pierceLane, target, atk);
            return;
        }

        int dealt = atk;
        ApplyDamage(target, dealt, actor);

        // 適用順を混ぜる。同じ一振りで2体以上落ちるとき、死亡順（墓守の層・破裂の連鎖）が
        // 席番号で決まっていた。巻き込む相手の顔ぶれは変わらない——順番だけ。
        var extras = SecondaryTargets(actor, target, patternOverride).ToList();
        Shuffle(extras);
        foreach (UnitState extra in extras)
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
    /// <param name="patternOverride">SelectTarget と同じ。null なら CurrentPattern。</param>
    public IReadOnlyList<UnitState> SecondaryTargets(UnitState attacker, UnitState primary,
                                                    AttackPattern? patternOverride = null)
    {
        List<UnitState> foes = LivingMembers(Opponent(attacker.TeamId))
            .Where(f => f != primary).ToList();

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
    public void ApplyDamage(UnitState target, int amount, UnitState? source,
                            bool isFriendlyFire = false, bool lethal = true,
                            bool burnTick = false)
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
            && target.Counter(ThornGuardTrait.PartnerKey) > 0)
        {
            int covered = target.Counter(ThornGuardTrait.PartnerKey) - 1;
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
                ApplyDamage(behind, overflow, source, burnTick: burnTick);
            }
        }

        foreach (Trait t in target.Traits)
            amount = t.ModifyIncomingDamage(target, amount);

        // 惨禍は「本人ではなく味方全体」に効くので、駒の特性ではなく盤面側で解決する。
        var teammates = LivingMembers(target.TeamId);

        // u != target ＝ 惨禍は本人には乗らない（HavocTrait のコメント参照）。
        // 「カドを名指しで除外」ではなく関係で書いてあるので、惨禍持ちが2体並べば互いに増幅し合う。
        if (teammates.Any(u => u != target && u.HasTrait(TraitId.Havoc)))
            amount += amount * HavocTrait.Percent / 100;

        // 据え: このターン差し出された駒は硬くなる。
        // 「動けなかった」ではなく「差し出した」を見る（Trait.SurrenderedTurn。号令と同じ判定）。
        // ハギ（追い打ち）のように最初から自分の手番を持たない型は差し出すものが無いので、
        // ここを見ないと静的なマイナスが毎ターンの −50% に化ける。
        //
        // **ログを1行出す。** 据えはロスターで唯一「無言で効く」買い手で、盤面の値にも痕跡を残さない
        // （減った後の数字しか残らない）。まどろみ（第36期）が実際に売れたかを数える窓口がここしか
        // 無いので、他の割り込みと同じように出来事として記録する。
        // 引き算は `amount -= amount * p / 100` と1点も違わない（同じ式を変数に置いただけ）。
        if (target.Counter(StatusKeys.IdleTurn) >= Turn
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
            amount -= amount * LooseTrait.ReductionPercent / 100;

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

                    // 腹（第36期）。**吐き戻しと同じ場所・同じ量を積む**ので、
                    // 「返した先の増分」と「腹に溜まった量」が定義上ずれない。
                    // 大喰らいの吸いはここを通らない（あちらは ApplyDamage の呼び出し元）ので、
                    // 腹は「殴られた誰かを庇ったとき」にだけ増える。
                    wall.SetCounter(ColossusTrait.BellyKey,
                                    wall.Counter(ColossusTrait.BellyKey) + blocked);
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
                            foreach (UnitState t in back) Whet(t, gain, WhetRoute.Regurgitate);
                            Log($"    {wall.Name} が飲み込んだ力を {target.Name} へ返した（攻撃 +{gain}）",
                                LogKind.Trigger);
                        }
                    }

                    ApplyDamage(wall, blocked, source, isFriendlyFire: true, burnTick: burnTick);
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
                    ApplyDamage(sharer, taken, source, isFriendlyFire: true, burnTick: burnTick);

                    // 痛みを取り上げられた者は腕がなまる。肩代わり量に比例させているので、
                    // 代金はドハのHPという有限プールから払われる（SharerTrait.DullDivisor 参照）。
                    // 切り捨てのままにしてあるのは、Math.Max(1, ...) にすると小さいダメージの
                    // 連打で比例関係が崩れ、下げ幅が肩代わり量から切り離されるため。
                    int dull = taken / SharerTrait.DullDivisor;
                    if (dull > 0)
                    {
                        Dull(target, dull, DullRoute.Sharer);
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
        int armor = target.Counter(StatusKeys.Armor);
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
            if (amount <= 0) return;
        }

        if (!lethal) amount = Math.Min(amount, Math.Max(0, target.Hp - 1));
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
        if (Yoke.Active && amount > Yoke.Cap
            && AllUnits.Any(u => u.IsAlive && u.HasTrait(TraitId.Yoke)))
        {
            Log($"    軛が {target.Name} への一撃を {amount} から {Yoke.Cap} に切った", LogKind.Trigger);
            amount = Yoke.Cap;
        }

        target.Hp -= amount;
        // 燃焼の刻みが実際に削った量（第57期）。**すべての増減を通した後の値**。
        if (burnTick) TallyOf(target).BurnTaken += amount;
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

        // 味方への通知。OnAllyDeath の走査と同じ形で、本人以外の生存チームメイトへ流す。
        // 破片で受け切った被弾はここより上の early return で自然に外れる。
        foreach (UnitState ally in LivingMembers(target.TeamId))
        {
            if (ally == target) continue;
            foreach (Trait t in ally.Traits.ToList())
                t.OnAllyDamaged(this, ally, target, amount, source);
        }

        if (target.Hp <= 0)
            HandleDeath(target, source);
    }

    private void HandleDeath(UnitState dead, UnitState? killer)
    {
        dead.Hp = 0;
        TallyOf(dead).Deaths++;
        TallyOf(dead).LastActiveTurn = _turn;   // 蘇生されて再度倒れると上書きされる（後の値が勝つ）
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
        // 召喚専用の枠だけを走る。編成枠へ入れると、5体で満席の盤面では一度も湧かない。
        // **走査順（FormationRules.SummonSlots）は調整ノブ。** 貫き経路に入る 中1・中3 から
        // 埋めるので、湧いた駒が減衰1段ぶんの盾として働く。
        foreach (int i in FormationRules.SummonSlots)
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
    public void Dull(UnitState target, int amount, DullRoute route = DullRoute.Other)
    {
        if (amount <= 0) return;

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
        if (!target.HasTrait(TraitId.Bear) && !target.HasTrait(TraitId.Relay))
        {
            UnitState? taker = PickOne(
                LivingMembers(target.TeamId)
                    .Where(u => u != target
                                && (u.HasTrait(TraitId.Bear) || (u.HasTrait(TraitId.Relay) && !InRelay))
                                && FormationRules.AreAdjacent(u.Slot, target.Slot))
                    .ToList());
            if (taker is not null && taker.HasTrait(TraitId.Bear))
            {
                receiver = taker;
                BearTaken += amount;
                DullTakenByRoute[(int)route] += amount;
                BearFrom[target.Name] = BearFrom.TryGetValue(target.Name, out int prev) ? prev + amount : amount;

                int armor = amount * Bear.ArmorPerDull;
                if (armor > 0)
                {
                    taker.SetCounter(StatusKeys.Armor, taker.Counter(StatusKeys.Armor) + armor);
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
                RelayThrough(taker, target, amount);
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
                    Relaying(() => Dull(victim, sent, DullRoute.Relay));
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

        // 横取りはまだ無い。**Dull はちょうどここに集約（ウケ）と転嫁（ワタ）を置いている**——
        // 読み手を作る期が来たら receiver を差し替える形で同じ位置に立てられる。
        UnitState receiver = target;

        // 崖の検算（Dull の DullZeroed の裏返し）。逆しまは AtkBonus の**符号だけ**を読み、
        // 負なら3倍・正なら半減なので、「半減側へ落ちた瞬間」はここでしか観測できない。
        bool perverse = receiver.HasTrait(TraitId.Perverse);
        int bonusBefore = receiver.AtkBonus;

        TallyOf(receiver).Whetted += amount;
        receiver.AtkBonus += amount;

        if (perverse)
        {
            WhetToPerverse += amount;
            if (bonusBefore <= 0 && receiver.AtkBonus > 0) WhetPerverseFlips++;
        }
    }

    public void Heal(UnitState target, int amount)
    {
        if (!target.IsAlive || amount <= 0) return;
        if (!target.AcceptsSupport) return;

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
        if (AllUnits.Any(u => u.IsAlive && u.HasTrait(TraitId.Drought))) return;

        int before = target.Hp;
        target.Hp = Math.Min(target.MaxHp, target.Hp + amount);
        if (target.Hp == before) return;

        // 実際に増えた分だけを数える（上限で切られた分は「払い戻し」になっていない）。
        // 代金の分解（第9期 bill）が差し引く側の資源。回復役の側ではなく
        // **回復された駒**に付けるのは、代金が駒ごとの HP の増減だからで、
        // 「誰が回復したか」を見たいときは Interventions / DamageToAlly の側を見る。
        TallyOf(target).Healed += target.Hp - before;

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
    public void SwapSlots(UnitState self, int destSlot)
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
    public static BattleResult Run(Formation player, Formation enemy, int seed, bool verbose = true,
                                   ColossusRule? colossus = null, YokeRule? yoke = null,
                                   HushRule? hush = null, MartyrRule? martyr = null,
                                   ExposeRule? expose = null, ShoveRule? shove = null,
                                   BearRule? bear = null, RelayRule? relay = null,
                                   SlanderRule? slander = null, OverbearRule? overbear = null,
                                   ScaleRule? scale = null, ScapegoatRule? scapegoat = null,
                                   DivertRule? divert = null, GoadRule? goad = null,
                                   FinisherRule? finisher = null)
        => Run(Materialize(player, BattleContext.PlayerTeam),
               Materialize(enemy, BattleContext.EnemyTeam),
               seed, verbose, colossus, yoke, hush, martyr, expose, shove, bear, relay, slander,
               overbear, scale, scapegoat, divert, goad, finisher);

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
                                   GoadRule? goad = null, FinisherRule? finisher = null)
    {
        var ctx = new BattleContext(seed, verbose, colossus, yoke, hush, martyr, expose, shove, bear,
                                    relay, slander, overbear, scale, scapegoat, divert, goad, finisher);

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
            ctx.EmitStatusSnapshot();   // 削った後の残量を写す。表示用で、盤面には触らない

            foreach (UnitState u in ctx.AllUnits.Where(x => x.IsAlive).ToList())
                foreach (Trait t in u.Traits.ToList())
                    t.OnTurnStart(ctx, u);

            // 止め（第53期）の遊休（標が付いてから殴られるまで）を測るための印。
            // 標の書き手（逸らし・駆り立て・囃し立て）はここまでに全部書き終わっている。
            // **盤面は1つも動かさない**（私有カウンタを書くだけ）ので、保持者がいなければ走らない。
            if (ctx.FinisherActive) ctx.NoteFinisherMarkAges();

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

            foreach (UnitState actor in order)
            {
                if (!actor.IsAlive) continue;
                if (!ctx.TeamAlive(ctx.Opponent(actor.TeamId))) break;

                if (actor.Counter(StatusKeys.Stun) > 0)
                {
                    if (ctx.ScapegoatActive) ctx.NoteScapegoatSkip(actor);
                    actor.SetCounter(StatusKeys.Stun, 0);
                    actor.SetCounter(StatusKeys.IdleTurn, turn);
                    ctx.Log($"  {actor.Name} は痺れて動けない", LogKind.Status);
                    continue;
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
                // ctx.Colossus.Slumber を先に見るのは、既定（V0）で HasTrait の走査を
                // 1回も走らせないため（layout は数百万戦を並列で回す。軛の Cap 判定と同じ作法）。
                if (ctx.Colossus.Slumber && actor.HasTrait(TraitId.Colossus)
                    && actor.Counter(ColossusTrait.BellyKey) >= ctx.Colossus.SlumberThreshold)
                {
                    actor.SetCounter(ColossusTrait.BellyKey,
                        actor.Counter(ColossusTrait.BellyKey) - ctx.Colossus.SlumberThreshold);
                    actor.SetCounter(StatusKeys.IdleTurn, turn);
                    ctx.TallyOf(actor).Slumbers++;
                    ctx.Log($"  {actor.Name} は腹が満ちてまどろんだ", LogKind.Status);
                    continue;
                }

                // **行動種別を先に決めてから CanAct を問う。** 「動けない」には二種類あって、
                // 無力化（痺れ・のろま）は何をするのも止めるが、不動（カド）が止めているのは
                // 攻撃だけ。何をしようとしているかが分からないと、この二つを区別できない。
                // Actions を持たない駒は Attack で問われるので従来とまったく同じ答えになる。
                UnitAction? act = actor.CurrentAction;
                ActionKind kind = act?.Kind ?? ActionKind.Attack;

                bool canAct = actor.Traits.All(t => t.CanAct(ctx, actor, kind));
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
                    actor.SetCounter(StatusKeys.IdleTurn, turn);
                    continue;
                }

                if (act is null)
                {
                    ctx.PerformAttack(actor);   // 従来経路。Actions を持たない駒はここしか通らない
                    continue;
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
                    ctx.EmitCharge(actor, act, actor.CurrentAction);
                    ctx.TallyOf(actor).Charges++;
                    ctx.Log($"  {actor.Name} は{act.Label ?? "力を溜めている"}", LogKind.Status);
                    continue;
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
                    ctx.EmitSkill(actor, act);
                    ctx.Log($"  {actor.Name} は{act.Label ?? "術を使った"}", LogKind.Action);
                    foreach (Trait t in actor.Traits.ToList())
                        t.OnAction(ctx, actor, act);
                    continue;
                }

                ctx.PerformAttack(actor, attackPercent: act.AttackPercent,
                                  patternOverride: act.PatternOverride);
            }
        }

        bool playerWon = ctx.TeamAlive(BattleContext.PlayerTeam)
                         && !ctx.TeamAlive(BattleContext.EnemyTeam);

        ctx.Log(playerWon ? "=== 勝利 ===" : "=== 敗北 ===", LogKind.System);

        // 生き残った駒の「最後に活動していたターン」は決着ターン。
        // 集計（life 診断）専用で、誰もこの値を読んで分岐しない。
        int settled = Math.Min(turn, MaxTurns);
        foreach (UnitState u in ctx.AllUnits)
            if (u.IsAlive) ctx.TallyOf(u).LastActiveTurn = settled;

        return new BattleResult
        {
            PlayerWon = playerWon,
            Turns = Math.Min(turn, MaxTurns),
            Log = ctx.Log_.ToList(),
            PlayerSurvivors = ctx.LivingMembers(BattleContext.PlayerTeam).Count(),
            DamageByUnit = new Dictionary<string, int>(ctx.DamageByUnit),
            TallyByUnit = new Dictionary<string, UnitTally>(ctx.TallyByUnit),
            MaxEnemyKillsInOneTurn = ctx.MaxEnemyKillsInOneTurn,
            Events = ctx.Events.ToList(),
            ExposeCount = ctx.ExposeCount,
            ExposeMissed = ctx.ExposeMissed,
            DullTotal = ctx.DullTotal,
            DullByRoute = ctx.DullByRoute,
            DullTakenByRoute = ctx.DullTakenByRoute,
            WhetTotal = ctx.WhetTotal,
            WhetByRoute = ctx.WhetByRoute,
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
                .Sum(u => u.Counter(StatusKeys.Armor)),
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
            FinisherTargetTo = ctx.FinisherTargetTo
        };
    }

    /// <summary>
    /// 編成から新品の UnitState 群を作る（スロット昇順）。旧 Deploy の切り出し。
    /// 盤面には触らない（Add は Run 側でやる）ので、会戦が「部隊列から次の部隊を起こす」
    /// 用途にそのまま使える。
    /// </summary>
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
