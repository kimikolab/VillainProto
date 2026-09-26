using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// whip モード（第217期） —— 電気鞭のシガ（責め苦のシガの作り直し）
//
// 指示書は design/PHASE217_SHIGA_WHIP_SPEC.md ／ 報告は design/PHASE217_SHIGA_WHIP.md。
// **線は置かない**（採否はポンが遊んで決める）。台は `Presets` に足さない（この診断のローカル）。
//
//     dotnet run --project BattleSim -c Release 0 whip phase0   # Q0-1〜Q0-6（実装前・盤面は第216期の追記のまま）
//     dotnet run --project BattleSim -c Release 0 whip run      # 表A〜E（実装後）
//     dotnet run --project BattleSim -c Release 0 whip check [balance.md]  # 自己検査（受け入れ 1〜3・6）
// =====================================================================================

static partial class WhipDiag
{
    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(arg); return;
            case "log": LogImpl(arg); return;
            default:
                Console.WriteLine("whip: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunImpl(string arg);
    static partial void CheckImpl(string arg);
    static partial void LogImpl(string arg);

    // =================================================================================
    // 台（指示書 §6.1）
    // =================================================================================

    /// <summary>W1〜W3 の顔ぶれ（席は総当たりで選ぶ）。</summary>
    internal static readonly (string Tag, string Aim, Func<UnitDef, List<UnitDef>> Members)[] Rigs =
    {
        ("W1", "弾く役（トウ）", s => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, s, UnitCatalog.Tou }),
        ("W2", "止める役（クグ）", s => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, s, UnitCatalog.Kugu }),
        ("W3", "台A の枠（グザ）", s => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, s, UnitCatalog.Guza }),
    };

    /// <summary>W4 ＝ `compare` でシガのいる行（元の席のまま）。</summary>
    internal static List<(string Name, Formation F)> W4Rows()
        => CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "shiga")).Select(r => (r.Name, r.F)).ToList();

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");

    static string Short(UnitDef d) => d.Name.Split('の').Last();

    static string FindRoot()
    {
        string d = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(d, "CLAUDE.md"))) d = Path.GetDirectoryName(d) ?? throw new InvalidOperationException("CLAUDE.md が見つからない");
        return d;
    }

    static bool Overrides(Trait t, string method)
    {
        foreach (MethodInfo m in t.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public))
            if (m.Name == method && m.DeclaringType != typeof(Trait)) return true;
        return false;
    }

    // =================================================================================
    // Phase 0（実装前）
    // =================================================================================

    static void Phase0()
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第217期 `whip phase0` —— Q0-1〜Q0-6（実装前・盤面は第216期の追記のまま）");
        Console.WriteLine();
        string root = FindRoot();
        string engine = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));

        // ---- 前提 ----
        Console.WriteLine("## 前提（O4・S2 が規定か）");
        Console.WriteLine();
        Console.WriteLine("- ベニの札: " + string.Join(", ", UnitCatalog.Beni.Traits) + " → O4（`GurenOpeningBurn`）"
                          + (UnitCatalog.Beni.Traits.Contains(TraitId.GurenOpeningBurn) ? " **あり**" : " **なし**"));
        Console.WriteLine("- カタの札: " + string.Join(", ", UnitCatalog.Kata.Traits) + " → S2（`ShockStunAll`）"
                          + (UnitCatalog.Kata.Traits.Contains(TraitId.ShockStunAll) ? " **あり**" : " **なし**"));
        Console.WriteLine("- シガ: HP" + UnitCatalog.Shiga.MaxHp + "・攻" + UnitCatalog.Shiga.Attack + "・速" + UnitCatalog.Shiga.Speed + "・" + UnitCatalog.Shiga.Pattern
                          + "・札 " + string.Join(", ", UnitCatalog.Shiga.Traits));
        Console.WriteLine();

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1 標的選び（見せしめの「動けない敵を優先」は薙ぎに効くか）");
        Console.WriteLine();
        string key = "pattern == AttackPattern.Single && attacker.HasTrait(TraitId.Shame)";
        int at = engine.IndexOf(key, StringComparison.Ordinal);
        int line = at < 0 ? -1 : engine.Take(at).Count(c => c == '\n') + 1;
        Console.WriteLine("- `BattleEngine.cs:" + line + "` の条件 `" + key + "`（" + (at < 0 ? "**見つからない**" : "あり") + "）");
        Console.WriteLine("  ——**薙ぎは素の選び方（`pool[Roll]`）に落ちる**。執着・断ちと同じ段（`pool` の内側）なので、薙ぎにも通すなら条件に「鞭の札の保持者」を足す（`SweepTargets` は主目標から引くので、主目標を動けない敵にすれば巻き込みもその列）。");
        Console.WriteLine("- 巻き込みの表（X 字）: " + string.Join(" ／ ", new[] { 0, 1, 2, 3, 4 }.Select(s => FormationRules.SeatNames[s] + "→" + string.Join("・",
                              FormationRules.SweepTargets(s).Where(x => x < 5).Select(x => FormationRules.SeatNames[x])))));
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2 追い打ち（2発）と2倍（1発）の違い");
        Console.WriteLine();
        Console.WriteLine("敵（第2〜5波）の札のうち、**1回の被弾ごとに走る**もの（`OnDamaged` / `ModifyIncomingDamage` を上書き）と盤面ルール:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵 | 攻 | 速 | 札 | 被弾ごとに走る札 |");
        Console.WriteLine("|---|---|--:|--:|---|---|");
        for (int st = 1; st < 5; st++)
            foreach (var (slot, d) in EnemyCatalog.Stages[st].Enemy.Occupied())
            {
                var per = d.Traits.Where(t => Overrides(TraitCatalog.Get(t), "OnDamaged") || Overrides(TraitCatalog.Get(t), "ModifyIncomingDamage")).ToList();
                Console.WriteLine("| 第" + (st + 1) + "波 | " + d.Name + " | " + d.Attack + " | " + d.Speed + " | " + string.Join(", ", d.Traits) + " | " + (per.Count == 0 ? "—" : string.Join(", ", per)) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("- **軛（上限 25）**: 追い打ちは 攻 × 2 発を1発ずつ切る。2倍は 攻 × 2 を1発で切る。**攻 ≥ 13 で差が出る**（素の攻 9 では 18 < 25 で同じ）。");
        Console.WriteLine("- **感電の起爆**: 追い打ちは1発目で弾け、2発目は弾けた後の相手に入る（起爆の回数はどちらも1回）。"
                          + "**ただし「動けない」の判定の時点が違う**——追い打ちは**当てた後**（`OnAfterAttack`）に判定するので、"
                          + "S2 で1発目に弾けて痺れた相手にも追い打ちが出る。2倍は**当てる前**に決めるしかない（1発だから）ので、弾けて痺れる相手は2倍にならない。");
        Console.WriteLine("- 破片・身構え・庇い・受け流し: 敵側に保持者がいなければ同じ（上の表で確かめる）。庇い（殉教）は主目標の差し替えで、追い打ちも2倍も差し替えた後の相手に入る。");
        Console.WriteLine("- 被弾ごとに走る敵の札は2つ: **殉教（`Martyr` ＝ `RedirectGainTrait`）は受けた量の半分（切り捨て）を攻撃力に足す**——追い打ち 9 ＋ 9 は 4 ＋ 4、2倍 18 は 9（**1点ぶん敵が強くなる**）。"
                          + "**断罪（`Condemn`）は反撃（`InReaction`）で殴られたときだけ**なので、シガの手番の一撃では走らない。棘のような反撃札は敵に 0 本。");
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3 電気鞭の判定の時点");
        Console.WriteLine();
        Console.WriteLine("- 起爆は `ApplyDamageBody` の1箇所で「感電している駒の HP に届いた一撃」。シガの一撃は敵に入り、放電は**弾けた駒と同じ陣営の隣**へ走る"
                          + "——**シガ自身の感電がシガの一撃で弾けることは無い**。");
        var hitBack = new List<string>();
        for (int st = 1; st < 5; st++)
            foreach (var (_, d) in EnemyCatalog.Stages[st].Enemy.Occupied())
                foreach (TraitId t in d.Traits)
                    if (Overrides(TraitCatalog.Get(t), "OnDamaged") && !hitBack.Contains(t.ToString())) hitBack.Add(t.ToString());
        Console.WriteLine("- シガの一撃の途中でシガが殴られうるのは、敵の被弾反応（`OnDamaged`）だけ: " + (hitBack.Count == 0 ? "**0 本**" : string.Join(", ", hitBack))
                          + "（断罪は `InReaction` の中だけで、シガの手番の一撃には反応しない）。");
        Console.WriteLine("- だから「手番の頭で感電していれば」は**一撃を振り始める瞬間**に読めば同じになる（`PerformAttackBody` の入口で控える）。叩き起こし（ガン）の手番の外の一撃も同じ口を通る。");
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4 感電の痺れの連続（手で1例）");
        Console.WriteLine();
        Console.WriteLine("行動順（速さ）: トウ 11 ／ クグ 10 ／ ミオ 8 ／ **カタ 6 ／ ベニ 6** ／ グザ 5 ／ **シガ 3**。敵の速さは Q0-2 の表。");
        Console.WriteLine();
        Console.WriteLine("第3波の巡礼騎士（攻15・速7）を例に:");
        Console.WriteLine("1. T1: 騎士が動く（速7）→ カタ（速6）の雷が騎士に落ちて感電 → シガ（速3）の鞭が騎士に当たり弾ける → S2 で騎士は痺れる。"
                          + "**電気鞭ならここで当たった敵すべてにもう一度感電を付ける**。");
        Console.WriteLine("2. T2: 騎士は痺れで手番を失う（1回目）。カタの雷（毎手番・種類最多を狙う）がまた騎士に当たれば、**雷は起爆しない**ので感電はそのまま。シガの鞭で弾けて**また痺れる**。");
        Console.WriteLine("3. T3: 騎士は手番を失う（2回目）。以下、シガの鞭が騎士に届き続ける限り毎ターン繰り返す。シガより速い敵は"
                          + "弾けたターンの次のターンの手番を失う。**同じ速さ 3 の城塞の重装兵（第4波）はシガの後に動く**（同速は陣営の番号 → 味方が先）ので、弾けた**そのターン**の手番を失う。");
        Console.WriteLine("- **G3 でループを保つ条件**は「毎ターン誰かが騎士の感電を弾く」こと。鞭（毎ターン）・トウ（速11 の粉は殴り）・放電（隣が弾ければ連鎖）のどれでも弾ける。"
                          + "シガが感電していなければ電気鞭は出ないが、カタの雷が毎手番1体以上に感電を残すので、その敵は次の一撃でまた弾ける。");
        Console.WriteLine("- G3H（痺れが明けた駒は次の自分の手番まで感電の痺れを受けない）なら、T2 に手番を失った騎士は T3 の頭まで守られる"
                          + "——T2 のシガの鞭では痺れず、T3 は動ける。**連続は最大 1 回**になる（竦み・組み付き・トウの粉の痺れは別）。");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5 過去の測定");
        Console.WriteLine();
        Console.WriteLine("- 第185期（見せしめを足した）: 「4対2」はクグ×シガの台でしか起きず（2体以上止まったターン 0.0 → 14.8%）、`compare` にクグ×シガの同席は 0 行。"
                          + "シガ×ガンの叩き起こしループは回るが見せしめを足しても1ビットも変わらない（見せしめはトウかクグが同席するときだけ働く）。");
        Console.WriteLine("- 第152期 `checkup`: 単独 −5.67（転生）。札が両義（封じられた敵には追い打ち・動ける敵には自滅が1つの規則）なので**マイナスだけを外した版が作れない**。");
        Console.WriteLine("- 第177期 `roster_audit`: 理想台の帰属 −0.25・列レンジ 25.0（ノイズ群・左下＝送り先を選べば働く）。");
        Console.WriteLine("- 第128期: `責め苦 (トウ×シガ)` の第4波 +64.5pt は**ドルガの段**で、シガの薙ぎ化ではない。**シガの攻撃型を変えた実験は過去に 0 件**。");
        Console.WriteLine();

        // ---- Q0-6 ----
        Console.WriteLine("## Q0-6 台と駒");
        Console.WriteLine();
        var w4 = W4Rows();
        Console.WriteLine("- W4（`compare` でシガのいる行）: " + w4.Count + " 行 —— " + string.Join(" ／ ", w4.Select(r => "`" + r.Name + "`")));
        foreach (var (tag, aim, mem) in Rigs)
            Console.WriteLine("- " + tag + "（" + aim + "）: " + string.Join("・", mem(UnitCatalog.Shiga).Select(d => Short(d) + "(速" + d.Speed + "・" + d.Pattern + ")")));
        Console.WriteLine();
        Console.WriteLine("### 参考: 今のシガ（G0）で席を選んだ W1〜W3（**G3 の席は実装後に選び直す**）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 倍率 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 全員生存 | シガ 振り/戦 | 動けない敵を責めた/戦 | 怖気づいた手番/戦 | シガの感電 もらった/弾けた/痺れた | 敵の失った手番 感電/竦み/ほか痺れ/組み付き | 席 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|---|");
        var provisional = new List<(string Tag, Formation F)>();
        foreach (var (tag, _, mem) in Rigs)
            foreach (FormationShape sh in ShockDiag.Shapes)
            {
                Formation f = ShockDiag.PickSeats216(mem(UnitCatalog.Shiga), sh, ShockDiag.Scale115);
                provisional.Add((tag, f));
                foreach (var (sc, rule) in ShockDiag.Scales216)
                {
                    var a = ShockDiag.Measure216(f, rule);
                    UnitTally s = ShigaTally(f, rule);
                    Console.WriteLine("| " + tag + " | " + (sh == FormationShape.X ? "X字" : "P2") + " | " + sc + " | "
                                      + string.Join(" | ", new[] { 1, 2, 3, 4 }.Select(w => F1(a.WinPct(w)))) + " | **" + F1(a.Mean25) + "** | " + F1(a.AllSurvPct) + " | "
                                      + F2(a.Per(s.Attacks)) + " | " + F2(a.Per(s.ShameFires)) + " | " + F2(a.Per(s.StallStun - s.StallShockStun)) + " | "
                                      + F2(a.Per(s.ShockSpent)) + "（弾けた）／" + F2(a.Per(s.StallShockStun)) + "（痺れで失った） | "
                                      + F2(a.Per(a.E.StallShockStun)) + "／" + F2(a.Per(a.E.StallCowed)) + "／" + F2(a.Per(a.E.StallStun - a.E.StallShockStun)) + "／" + F2(a.Per(a.E.StallGrappled)) + " | "
                                      + string.Join(" ", f.Occupied().Select(o => f.Shape.FrameNames[o.Slot] + ":" + Short(o.Def))) + " |");
                }
            }
        Console.WriteLine();
        Console.WriteLine("（動けない敵を責めた ＝ 見せしめの判定が通った回数 ＝ 追い打ちの回数。怖気づいた手番 ＝ 感電以外の痺れで失ったシガの手番）");
        Console.WriteLine();

        // ---- 追い打ちのうち「当てた一撃で弾けて痺れた相手」（G1 で2倍にならない分） ----
        Console.WriteLine("### 追い打ちの内訳（台本・倍率 115・第2〜5波 × seed 0..49）");
        Console.WriteLine();
        Console.WriteLine("「弾けて痺れた」＝ 主目標がシガの一撃で弾けて（`ShockSpent` の起点）痺れ、その後に追い打ちが出た。**当てる前の判定（2倍）では出ない分。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | シガの振り | 追い打ち | うち弾けて痺れた | 主目標が感電していた振り | 怖気づいた振り |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var (tag, f) in provisional)
        {
            long swings = 0, follows = 0, popFollow = 0, shockedPrim = 0, cowered = 0;
            for (int st = 1; st < 5; st++)
                for (int s = 0; s < 50; s++)
                {
                    var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, EnemyScaleRule.Adopted);
                    BattleResult r = BattleEngine.Run(pl, en, s, verbose: true);
                    int shigaId = pl.First(u => u.Def.Id == "shiga").InstanceId;
                    var ev = r.Events;
                    for (int i = 0; i < ev.Count; i++)
                    {
                        if (ev[i].Kind != BattleEventKind.Attack || ev[i].ActorId != shigaId) continue;
                        swings++;
                        int? prim = ev[i].TargetId;
                        bool popped = false, followed = false;
                        for (int j = i + 1; j < ev.Count; j++)
                        {
                            BattleEvent e = ev[j];
                            if (e.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart or BattleEventKind.Skill) break;
                            if (e.Kind == BattleEventKind.ShockSpent && e.Slot == 0 && e.TargetId == prim && e.ActorId == shigaId) popped = true;
                            if (e.Kind == BattleEventKind.Highlight && e.ActorId == shigaId && (e.Text?.Contains("追い打ち") ?? false)) followed = true;
                        }
                        if (popped) shockedPrim++;
                        if (followed) { follows++; if (popped) popFollow++; }
                        else cowered++;
                    }
                }
            Console.WriteLine("| " + tag + " | " + (f.Shape == FormationShape.X ? "X字" : "P2") + " | " + swings + " | " + follows + " | " + popFollow + " | " + shockedPrim + " | " + cowered + " |");
        }
        Console.WriteLine();
        Console.WriteLine("（怖気づいた振り ＝ 追い打ちの出なかった振り。倒した相手への振りも含む）");
        Console.WriteLine();

        // ---- W4 の G0 ----
        Console.WriteLine("### W4（元の席・G0）");
        Console.WriteLine();
        foreach (var (name, f) in w4)
            foreach (var (sc, rule) in ShockDiag.Scales216)
            {
                var a = ShockDiag.Measure216(f, rule);
                Console.WriteLine("- `" + name + "` × " + sc + ": " + string.Join(" / ", new[] { 1, 2, 3, 4 }.Select(w => F1(a.WinPct(w)))) + "（平均 " + F1(a.Mean25) + "・全員生存 " + F1(a.AllSurvPct) + "）");
            }
        Console.WriteLine();
        Console.WriteLine("所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }

    /// <summary>シガの帳簿だけを足し込む（第2〜5波 × seed 0..199）。</summary>
    internal static UnitTally ShigaTally(Formation f, EnemyScaleRule scale, int seed0 = 0, int seeds = 200)
    {
        var total = new UnitTally();
        var gate = new object();
        var boss = new BossRule(false) { Scale = scale };
        Parallel.For(0, 4 * seeds, () => new UnitTally(), (j, _, local) =>
        {
            int st = 1 + j / seeds, s = seed0 + j % seeds;
            BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss);
            if (r.TallyByUnit.TryGetValue("shiga", out var t)) local.Add(t);
            return local;
        }, local => { lock (gate) total.Add(local); });
        return total;
    }
}
