namespace BattleCore;

/// <summary>
/// 部隊戦の開始時点の盤面。持ち越した HP・攻撃力を再生側が知るための写し。verbose 時のみ。
///
/// <para><c>Attack</c> は <see cref="UnitState.CurrentAttack"/>（墓守の層を再適用した後の値）、
/// <c>Pattern</c> は <see cref="UnitState.CurrentPattern"/>（3層持ち越しのリィカは開幕から
/// 薙ぎで載る。Def.Pattern を直接読まない、の絶対ルールはここにも効く）。</para>
///
/// <para><c>BaseAttack</c>（Def.Attack）を持つのは再生側が「素 → 現在」を出すため。
/// 敵の UnitDef には一覧 API が無く、UnitId から素の値を引けない。</para>
///
/// <para>会戦を跨いで駒を同定する手段は (TeamId, Slot)。持ち越された駒は前の Battle を
/// **終えたときのスロット**のまま次の Opening に載る（境界で再配置しない）ので、
/// 前の Battle の Move イベントを再生し切った後の位置と 1:1 に対応する。
/// UnitId では足りない——敵部隊は同じ UnitDef の駒を複数含む（第一波の新兵×2 など）。
/// これは再生側（敵を含む全駒）の話。BattleSim の seats 診断は味方限定＋UnitId 重複ガード付きで
/// UnitId 同定する——「Slot がずれたか」を測る診断で Slot を同定キーに使えないため。</para>
///
/// <para><c>HasFallenBack</c> も入場時の値（Run の前に控える）。会戦を跨いで維持される（判断 D6）
/// ので、第2戦以降の開幕から立っている駒がどれだけあるかを診断で数えられる。</para>
/// </summary>
public sealed record BattleOpening(int InstanceId, int TeamId, string UnitId, string Name,
                                   int Slot, int Hp, int MaxHp, int Attack, int BaseAttack,
                                   AttackPattern Pattern, bool HasFallenBack);

/// <summary>
/// 部隊戦に入る時点の片側の戦力。<see cref="EngagementResult.Openings"/> と違い
/// verbose に関係なく積む（勝率計測は verbose=false で回すため、そちらからも読める）。
///
/// <para><c>MaxHpSum</c>（現在の最大HP＝継ぎ接ぎの損耗が乗った値）と <c>DefMaxHpSum</c>
/// （定義上の最大HP）を分けるのは、損耗した駒ほど Hp/MaxHp 比が健康に見えるため。
/// ヴェルは 46→11 まで削れるので、比率だけ見ると満タンに化ける。</para>
///
/// <para>注意: どのフィールドも「その戦闘に入った駒」だけの合計なので、死んだ駒は
/// 分子と分母から一緒に抜ける。部隊全体に対する残存戦力を出すときは、この record の
/// <c>DefMaxHpSum</c> ではなく編成側の定義上総最大HP（不変値）を分母に取ること
/// （BattleSim の engage がそうしている。1体だけ全快で残った部隊を 100% に見せないため）。</para>
/// </summary>
public sealed record SquadEntry(
    int Alive,        // 生存数
    int HpSum,        // 現在HPの合計
    int MaxHpSum,     // 現在の最大HPの合計（継ぎ接ぎの損耗が乗った値）
    int DefMaxHpSum); // 定義上の最大HPの合計（損耗前）

/// <summary>
/// 会戦の境界で味方を回復させる規則（第101期）。既定は <c>(0, false)</c> ＝ 現行（回復なし）。
///
/// <para><c>HpPercent</c>: 生き残った味方の HP を <c>MaxHp</c> の割合ぶん戻す（上限は MaxHp）。
/// <c>ReviveDead</c>: 倒れた味方も MaxHp で復帰させる（スロットは元のまま。再配置しない）。</para>
///
/// <para><b>敵側には一切適用しない。</b> 敵は 1 部隊を抜くたび
/// <see cref="BattleEngine.Materialize"/> で新品を投入するので、元から全快である
/// （持ち越すのは味方が敵部隊を抜けなかったときの敵だけで、そこは味方が負けて会戦が終わる側）。</para>
/// </summary>
public readonly record struct RecoverRule(int HpPercent, bool ReviveDead)
{
    /// <summary>現行。境界に回復は 1 点も無い（第99期）。</summary>
    public static RecoverRule Default => new(0, false);

    /// <summary>この規則が盤面を 1 ビットでも動かしうるか。偽なら <c>CarryOver</c> は第100期と同一。</summary>
    public bool Active => HpPercent > 0 || ReviveDead;
}

/// <summary>
/// 会戦の境界で免除する罰（第102期）。<b>新しい機構は1つも作っていない。</b>
/// 境界がしている3つのことを、<b>1つだけ免除する</b>形になっている。
///
/// <list type="table">
/// <item><term><see cref="Revive"/></term><description>死者は戻らない → <b>体</b>を返す</description></item>
/// <item><term><see cref="Heal"/></term><description>HP は削られたまま → <b>HP</b> を返す</description></item>
/// <item><term><see cref="Carry"/></term><description>積み上げは全消し → <b>積み上げ</b>を返す（負も一緒に）</description></item>
/// </list>
/// </summary>
public enum BoundaryChoice
{
    /// <summary>現行。境界は3つとも罰したまま。</summary>
    None,

    /// <summary><b>倒れた味方1体を <c>MaxHp / 2</c> で戻す。</b> 体は返すが HP は返さない
    /// （3つの通貨を分離するため）。端数は切り捨て・最低 1。選ぶのは<b>最後に倒れた駒</b>で、
    /// 同着はスロット昇順（<c>ctx.PickOne</c> を使わない・第89期 (h)）。
    /// 召喚された駒（胞子・亡者）は候補に入らない——<c>deployed</c> にそもそも載らない。</summary>
    Revive,

    /// <summary><b>生存者を <c>MaxHp</c> まで全快。</b> 死者は戻らない
    /// （第101期の <c>RecoverRule(100, false)</c> ＝ R100 と同じ処理）。</summary>
    Heal,

    /// <summary>
    /// <b><see cref="StatusKeys.All"/> の消去と <c>ResetAtkBonus()</c> と <c>WhetReceived = 0</c> をしない。</b>
    /// それ以外（<c>ActionIndex = 0</c> と <c>OnCarryOver</c>）は現行どおり。
    ///
    /// <para><b>負も一緒に持ち越す</b>のが要点——積み上げだけを持ち越すと常に得になり、
    /// 選択にならない。毒まみれの部隊で持ち越せば自殺行為になる（可変コスト型の判断）。</para>
    /// </summary>
    Carry,
}

/// <summary>
/// 境界の選択（第102期）。既定は <see cref="BoundaryChoice.None"/> ＝ 現行。
///
/// <para><b><see cref="RecoverRule"/>（第101期）とは併用しない。</b>
/// <c>RecoverRule.Active</c> が真のときは<b>この規則を無視する</b>——
/// どちらも同じ場所（境界の HP と体）を触るので、両方効かせると
/// 「どちらが効いたのか」が原理的に割れない。</para>
///
/// <para><b>敵側には一切適用しない</b>（<c>RecoverRule</c> と同じ。敵は 1 部隊を抜くたび
/// <see cref="BattleEngine.Materialize"/> で新品が投入されるので元から全快）。</para>
/// </summary>
/// <param name="Choice">全境界で使う選択。<paramref name="Plan"/> が無いときだけ読まれる。</param>
/// <param name="Plan">
/// 境界ごとの選択（0 番目 ＝ 第1戦の後）。<b>方策を「上限」として測るための窓口</b>で、
/// 通常の実行では誰も渡さない。要素が足りない境界は <paramref name="Choice"/> に落ちる。
/// </param>
public readonly record struct BoundaryRule(BoundaryChoice Choice,
                                           IReadOnlyList<BoundaryChoice>? Plan = null)
{
    /// <summary>現行。境界の免除は 1 つも無い。</summary>
    public static BoundaryRule Default => new(BoundaryChoice.None);

    /// <summary>この規則が盤面を 1 ビットでも動かしうるか。偽なら <c>CarryOver</c> は第101期と同一。</summary>
    public bool Active => Choice != BoundaryChoice.None || (Plan is { Count: > 0 }
        && Plan.Any(c => c != BoundaryChoice.None));

    /// <summary><paramref name="boundaryIndex"/> 番目の境界（0 ＝ 第1戦の後）で使う選択。</summary>
    public BoundaryChoice At(int boundaryIndex)
        => Plan is not null && boundaryIndex < Plan.Count ? Plan[boundaryIndex] : Choice;
}

/// <summary>会戦の結果。UI はこれを再生するだけでよい（BattleResult と同じ思想）。</summary>
public sealed class EngagementResult
{
    public required bool PlayerWon { get; init; }
    public required IReadOnlyList<BattleResult> Battles { get; init; }

    /// <summary>各 Battle の開始盤面。Battles と同じ長さ。verbose=false なら各要素が空。</summary>
    public required IReadOnlyList<IReadOnlyList<BattleOpening>> Openings { get; init; }

    /// <summary>各 Battle で戦った (味方部隊番号, 敵部隊番号)。Battles と同じ長さ。</summary>
    public required IReadOnlyList<(int PlayerSquad, int EnemySquad)> Pairings { get; init; }

    /// <summary>各 Battle の開始時点の味方戦力。Battles と同じ長さ。verbose=false でも積む。</summary>
    public required IReadOnlyList<SquadEntry> PlayerEntries { get; init; }

    /// <summary>
    /// 各 Battle を<b>終えた</b>時点の味方戦力（倒れた駒は Hp 0 のまま数に入る）。
    /// Battles と同じ長さ。verbose=false でも積む。
    ///
    /// <para>入場側（<see cref="PlayerEntries"/>）だけでは会戦の最後の Battle の後が読めない
    /// ——次の入場が無いので、払った HP の総額が最終戦だけ欠ける。代金の分解（第9期 bill）が
    /// 「失った HP = 定義上の総最大HP − 残 HP」を出すために足したもの。</para>
    ///
    /// <para>敵側は足していない。敵は味方1部隊の計測では毎回新規投入で、退場時の残 HP は
    /// <see cref="LastBattleAttrition"/> が既に割合で持っている。</para>
    /// </summary>
    public required IReadOnlyList<SquadEntry> PlayerExits { get; init; }

    /// <summary>同じく敵側。持ち越された敵部隊がどれだけ削れていたかを見る。
    /// 味方1部隊の計測では敵は毎回新規投入なので常に無傷のはず（検算に使う）。</summary>
    public required IReadOnlyList<SquadEntry> EnemyEntries { get; init; }

    public required int EnemySquadsCleared { get; init; }
    public required int PlayerSquadsLost { get; init; }

    /// <summary>最初の Battle で敵第1部隊の総 MaxHp のうち削った割合（0..1）。特攻隊の価値を測る。</summary>
    public required double FirstBattleAttrition { get; init; }

    /// <summary>
    /// 最後の Battle で、そのとき相手にしていた敵部隊の総 MaxHp のうち削った割合（0..1）。
    /// 突破数が整数に潰れて向きの差を吸収してしまうため、部分点として足す（第8期 Phase U）。
    ///
    /// <para>分母は<b>その敵部隊を投入した時点の定義上の MaxHp 合計</b>（Def.MaxHp の和）。
    /// 持ち越しで目減りした分母（死んだ駒が抜けた後の合計や、継ぎ接ぎで縮んだ MaxHp）は
    /// 使わない——「この部隊をどれだけ削ったか」を列全体で通した一つの尺度にしたいので、
    /// 分母が戦闘ごとに動くと部分点の意味が Battle ごとに変わってしまう。分子も同じ理由で
    /// 「その Battle で削った分」ではなく「投入時からの累計」を取る。</para>
    ///
    /// <para>最後の Battle に勝っている（＝全抜き）試行では 1.0 になる。突破度を組む側は
    /// そのまま足さず列長ちょうどに揃えること（列長を超えさせない）。列長1の会戦では
    /// <see cref="FirstBattleAttrition"/> と一致するはずで、これは検算に使える。</para>
    /// </summary>
    public required double LastBattleAttrition { get; init; }

    /// <summary>
    /// MaxTurns 到達の引き分けが起きた回数。引き分けは「味方部隊が退く」扱い（仮置き T1）で、
    /// 独立5戦では一度も観測されていないが、消耗した部隊同士の Battle では膠着し得るので数える。
    /// </summary>
    public required int Draws { get; init; }
}

/// <summary>
/// 会戦（Engagement）。同じ地点で、どちらかの部隊列が尽きるまで部隊戦（Battle）を連結する。
///
/// <para>勝った側の部隊は生存駒の状態（HP・最大HPの損耗・蘇生回数・墓守の層）を持ち越して
/// 次の相手と戦い、負けた側は次の部隊を新品で投入する。難度の源泉を「敵の強さ」から
/// 「消耗」へ移すための装置（design/ の会戦計画参照）。</para>
///
/// <para>境界で消えるもの: StatusKeys の全カウンタ（毒・燃焼・痺れ・標的・破片…）と
/// AtkBonus（恒久と一時が混ざっているので一律 0 にし、恒久分は各特性の
/// <see cref="Trait.OnCarryOver"/> が再構成する）。</para>
/// </summary>
public static class EngagementEngine
{
    /// <summary>
    /// 保険の上限。毎 Battle 必ずどちらかの部隊が尽きるので、実際の Battle 数は
    /// 高々 味方部隊数 + 敵部隊数 - 1。ここに到達したら味方敗北で打ち切る。
    /// </summary>
    public const int MaxBattles = 10;

    public static EngagementResult Run(IReadOnlyList<Formation> playerSquads,
                                       IReadOnlyList<Formation> enemySquads,
                                       int seed, bool verbose = true,
                                       RecoverRule? recover = null,
                                       BoundaryRule? boundary = null)
    {
        var battles = new List<BattleResult>();
        var openings = new List<IReadOnlyList<BattleOpening>>();
        var pairings = new List<(int, int)>();
        var playerEntries = new List<SquadEntry>();
        var playerExits = new List<SquadEntry>();
        var enemyEntries = new List<SquadEntry>();

        RecoverRule rec = recover ?? RecoverRule.Default;
        // 第102期。**併用しない。** RecoverRule が既定でないときは BoundaryRule を無視する
        // ——どちらも境界の HP と体を触るので、両方効かせると帰属が原理的に割れない。
        BoundaryRule bnd = rec.Active ? BoundaryRule.Default : (boundary ?? BoundaryRule.Default);

        int pi = 0, ei = 0;
        int cleared = 0, lost = 0, draws = 0;
        double firstAttrition = 0;

        List<UnitState> current = BattleEngine.Materialize(playerSquads[0], BattleContext.PlayerTeam);
        List<UnitState> enemyCur = BattleEngine.Materialize(enemySquads[0], BattleContext.EnemyTeam);
        int enemyFirstMaxHp = enemyCur.Sum(u => u.MaxHp);
        // 今の敵部隊を投入した時点の定義上の総最大HP。部隊を入れ替えたときだけ更新する
        // （持ち越した部隊では分母を動かさない。LastBattleAttrition の分母の判断）。
        int enemyDefMaxHp = enemyCur.Sum(u => u.Def.MaxHp);
        double lastAttrition = 0;

        for (int battleIndex = 0; battleIndex < MaxBattles; battleIndex++)
        {
            // Opening は Run の前に値を控え、Run の後に組む。InstanceId は Run の中の
            // ctx.Add が振るので、先に record にすると前の Battle の ID が写ってしまうし、
            // 振り順を外から推測して複製するのは前提の二重化になる。
            // HasFallenBack もここで控える。Run の後に Unit から読むと「その戦闘で下がったか」が
            // 混ざり、「入場時に立っていたか」でなくなる（Hp・Slot と同じ扱い）。
            var pending = verbose
                ? current.Concat(enemyCur)
                    .Select(u => (Unit: u, u.Hp, u.MaxHp, Attack: u.CurrentAttack, u.Slot,
                                  Pattern: u.CurrentPattern, u.HasFallenBack))
                    .ToList()
                : null;

            // 入場戦力は Openings と違い verbose に関係なく積む（勝率計測が読む集計）。
            // 読むだけで盤面には一切触らない（受け入れ条件: compare 差分ゼロ）。
            playerEntries.Add(Snapshot(current));
            enemyEntries.Add(Snapshot(enemyCur));

            BattleResult r = BattleEngine.Run(current, enemyCur, DeriveSeed(seed, battleIndex), verbose);

            battles.Add(r);
            pairings.Add((pi, ei));
            // 退場戦力は CarryOver の前に取る。境界は HP に触らないが、
            // 触るようになったときにここが黙って意味を変えないよう、Run の直後に固定する。
            playerExits.Add(Snapshot(current));
            openings.Add(pending is null
                ? Array.Empty<BattleOpening>()
                : pending.Select(p => new BattleOpening(p.Unit.InstanceId, p.Unit.TeamId,
                    p.Unit.Def.Id, p.Unit.Def.Name, p.Slot, p.Hp, p.MaxHp, p.Attack,
                    p.Unit.Def.Attack, p.Pattern, p.HasFallenBack)).ToList());

            int enemyLeft = enemyCur.Sum(u => Math.Max(0, u.Hp));
            lastAttrition = enemyDefMaxHp == 0
                ? 0 : (double)(enemyDefMaxHp - enemyLeft) / enemyDefMaxHp;

            if (battleIndex == 0)
            {
                firstAttrition = enemyFirstMaxHp == 0
                    ? 0 : (double)(enemyFirstMaxHp - enemyLeft) / enemyFirstMaxHp;
            }

            // 勝敗の分岐は「会戦が投入した駒」の生死で判定する。戦闘中に湧いた駒（胞子）は
            // ここに含まれず、持ち越しもしない（儚い）。したがって
            //   clearedE: 投入した敵駒の生存 0（PlayerWon=true なら敵チーム全滅なので必ず成立）
            //   lostP:    投入した味方駒の生存 0、または負け扱い
            //             （負け扱いには MaxTurns 引き分け＝味方が退く（T1）と、
            //               胞子だけを残して味方部隊が尽きた場合を含む）
            // の2フラグで、毎 Battle 少なくとも一方が必ず立つ。
            // 敵側に召喚持ちを足したら clearedE の判定（投入駒のみを見る）を見直すこと。
            var aliveP = current.Where(u => u.IsAlive).ToList();
            var aliveE = enemyCur.Where(u => u.IsAlive).ToList();

            bool clearedE = aliveE.Count == 0;
            bool lostP = aliveP.Count == 0 || !r.PlayerWon;

            if (clearedE) { ei++; cleared++; }
            if (lostP)
            {
                pi++; lost++;
                if (aliveP.Count > 0 && aliveE.Count > 0) draws++;   // MaxTurns 引き分け（T1）
            }

            bool playerOut = lostP && pi == playerSquads.Count;
            bool enemyOut = clearedE && ei == enemySquads.Count;
            if (playerOut || enemyOut)
            {
                // どちらかが尽きたら尽きていない側の勝ち。両方同時に尽きたら味方敗北。
                return Build(enemyOut && !playerOut);
            }

            // 味方だけが回復の対象。敵側は CarryOver(aliveE) に規則を渡さない（既定のまま）。
            current = lostP
                ? BattleEngine.Materialize(playerSquads[pi], BattleContext.PlayerTeam)
                : CarryOver(aliveP, current, rec, bnd.At(battleIndex));
            enemyCur = clearedE
                ? BattleEngine.Materialize(enemySquads[ei], BattleContext.EnemyTeam)
                : CarryOver(aliveE);
            if (clearedE) enemyDefMaxHp = enemyCur.Sum(u => u.Def.MaxHp);
        }

        return Build(playerWon: false);

        EngagementResult Build(bool playerWon) => new()
        {
            PlayerWon = playerWon,
            Battles = battles,
            Openings = openings,
            Pairings = pairings,
            PlayerEntries = playerEntries,
            PlayerExits = playerExits,
            EnemyEntries = enemyEntries,
            EnemySquadsCleared = cleared,
            PlayerSquadsLost = lost,
            FirstBattleAttrition = firstAttrition,
            LastBattleAttrition = lastAttrition,
            Draws = draws,
        };
    }

    /// <summary>入場時点の戦力の写し。読むだけで UnitState には一切触らない。
    /// 投入直後（Materialize / CarryOver の直後）に呼ぶので全員生存のはずだが、
    /// 生存数は仮定せず数える（前提が崩れたとき集計側で気づけるように）。</summary>
    private static SquadEntry Snapshot(List<UnitState> squad) => new(
        Alive: squad.Count(u => u.IsAlive),
        HpSum: squad.Sum(u => u.Hp),
        MaxHpSum: squad.Sum(u => u.MaxHp),
        DefMaxHpSum: squad.Sum(u => u.Def.MaxHp));

    /// <summary>
    /// 部隊戦の境界。状態異常（StatusKeys の全キー、破片も含む）と AtkBonus を一律に消し、
    /// 持ち越したい状態は各特性の <see cref="Trait.OnCarryOver"/> に再構成させる。
    /// エンジンはホワイトリストを持たない（Counters のキーは特性の私有物）。
    ///
    /// Slot・HasFallenBack・MaxHp・特性私有のカウンタは触らない。境界で再配置もしないので、
    /// 駒は前の Battle を終えたときの位置のまま次へ入る。
    /// 現スロット昇順に整列して返すのは、次の Run の Add 順＝InstanceId の振り順を
    /// 決定的に保つため（戦闘中の移動でリスト順と現在位置がずれている）。
    /// </summary>
    /// <param name="survivors">生き残った駒。回復なしのときはこれがそのまま次の部隊になる。</param>
    /// <param name="deployed">
    /// その戦闘へ投入した駒の全部（倒れた駒を含む）。<c>RecoverRule.ReviveDead</c> のときだけ読む。
    /// 戦闘中に湧いた駒（胞子・亡者）はこのリストに入らないので、復帰の対象にならない
    /// （儚い駒は持ち越さない、が現行の規則）。null なら復帰させない。
    /// </param>
    private static List<UnitState> CarryOver(List<UnitState> survivors,
                                             List<UnitState>? deployed = null,
                                             RecoverRule rec = default,
                                             BoundaryChoice choice = BoundaryChoice.None)
    {
        // 第102期: 倒れた味方を1体だけ MaxHp/2 で戻す。RecoverRule.ReviveDead と同じ位置に置き、
        // 状態異常の消去と OnCarryOver は生存側と同じ扱いにする（先にリストへ入れてからループを回す）。
        // 選ぶのは「最後に倒れた駒」、同着はスロット昇順——ctx.PickOne を使わない（第89期 (h)）。
        if (choice == BoundaryChoice.Revive && deployed != null)
        {
            UnitState? back = deployed.Where(u => !u.IsAlive)
                .OrderByDescending(u => u.LastDeathTurn).ThenBy(u => u.Slot).FirstOrDefault();
            if (back != null)
            {
                back.Hp = Math.Max(1, back.MaxHp / 2);
                survivors.Add(back);
            }
        }

        // 第101期: 倒れた味方を戻す。状態異常の消去と OnCarryOver は生存側と同じ扱いにするため、
        // 先にリストへ入れてから下のループを回す。Hp は下の回復の段では触らない（全快で戻す）。
        if (rec.ReviveDead && deployed != null)
        {
            foreach (UnitState u in deployed)
            {
                if (u.IsAlive) continue;
                u.Hp = u.MaxHp;
                survivors.Add(u);
            }
        }

        bool carry = choice == BoundaryChoice.Carry;

        foreach (UnitState u in survivors)
        {
            // 第102期 Carry。**既存の段は1文字も変えず、通した後で「消さなかったことにする」。**
            // 順序が要る——消さずに OnCarryOver を呼ぶと、帳簿を持つ特性（墓守 NecroTrait）が
            // 「AtkBonus はエンジンが 0 にした後」を前提に帳簿を 0 へ戻して層ぶんを積み直すので、
            // 層のぶんが二重計上される（積み上げが発散する。CLAUDE.md の乗算の罠そのもの）。
            var keptStatus = carry ? new Dictionary<string, int>() : null;
            int keptBonus = 0, keptWhet = 0;
            if (carry)
            {
                foreach (string key in StatusKeys.All)
                {
                    int v = u.RawCounter(key);
                    if (v != 0) keptStatus![key] = v;
                }
                keptBonus = u.AtkBonus;
                keptWhet = u.WhetReceived;
            }

            foreach (string key in StatusKeys.All) u.Counters.Remove(key);
            u.ResetAtkBonus();   // 第68期: 帳簿に載せずに戻す（AtkBonus = 0 と同じ効果）
            // 第67期。押された累計は配られた力と同じ寿命（AtkBonus と同じ行で消す）。
            u.WhetReceived = 0;
            // 行動周期は Battle スコープ。溜めかけたまま波が変わると、次の部隊戦の初手に
            // 大技が落ちる（「溜めを見てから合わせる」という体験が成立しなくなる）。
            // ターン番号に紐づくカウンタを境界で 0 に戻す NecroTrait.OnCarryOver と同じ扱い。
            u.ActionIndex = 0;
            foreach (Trait t in u.Traits) t.OnCarryOver(u);

            // 第101期: 境界の回復。ctx.Heal は通さない——境界は戦闘の外で、渇き（第三波の
            // 波ルール）も AcceptsSupport（支援拒否）も「戦闘中に味方が配るもの」に対する規則。
            // 境界の手当ては誰かが配っているのではないので、Hp を直接足す。
            if (rec.HpPercent > 0)
                u.Hp = Math.Min(u.MaxHp, u.Hp + u.MaxHp * rec.HpPercent / 100);

            // 第102期 Heal。生存者だけを全快（RecoverRule(100, false) ＝ R100 と同じ処理）。
            // ここも ctx.Heal を通さない（境界は戦闘の外。理由は上のコメント）。
            if (choice == BoundaryChoice.Heal) u.Hp = u.MaxHp;

            // 第102期 Carry。控えた値へ戻す。ResetAtkBonus は帳簿に載せずに書く窓口
            // （通常の代入だと境界の復元が「正の上昇」として NoteAtkGain に載る・第68期）。
            if (carry)
            {
                foreach ((string key, int v) in keptStatus!) u.SetCounter(key, v);
                u.ResetAtkBonus(keptBonus);
                u.WhetReceived = keptWhet;
            }
        }
        return survivors.OrderBy(u => u.Slot).ToList();
    }

    /// <summary>
    /// 各 Battle の seed を親 seed から決定的に導く。Random を跨いで共有しない
    /// （Run の純関数性を保つ。同じ引数なら同じ会戦）。
    /// </summary>
    private static int DeriveSeed(int seed, int battleIndex)
        => unchecked(seed * 1000003 + battleIndex);
}
