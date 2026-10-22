using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;

// sh378 check —— 第378期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh378Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static UnitTally Tal(BattleContext ctx, UnitState u) => (UnitTally)TallyM.Invoke(ctx, new object[] { u })!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate378", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe378", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    /// <summary>直に組む盤面（味方は席 → 駒・敵は <paramref name="foes"/> 体）。</summary>
    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(int foes, params (int Slot, UnitDef D)[] seats)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation();
        foreach (var (s, d) in seats) f[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var ef = new Formation();
        for (int i = 0; i < foes; i++) ef[i] = Foe;
        var e = BattleEngine.Materialize(ef, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        return (ctx, p, e);
    }
    static void Kill(BattleContext ctx, UnitState u, UnitState by) => ctx.ApplyDamage(u, 9999, by);
    static int Summons(BattleContext ctx, string name, int? by = null) => ctx.Events.Count(x => x.Kind == BattleEventKind.Summon && x.Text == name && (by is null || x.ActorId == by));

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh378 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 定義 ----
        {
            var stack = UnitCatalog.RicaStack.Traits;
            bool defs = UnitCatalog.RicaE.Traits.SequenceEqual(stack.Append(TraitId.RicaAwakenRaise))
                && UnitCatalog.RicaL.Traits.SequenceEqual(stack.Append(TraitId.RicaLayerFlurry))
                && UnitCatalog.RicaEL.Traits.SequenceEqual(stack.Concat(new[] { TraitId.RicaAwakenRaise, TraitId.RicaLayerFlurry }))
                && UnitCatalog.RicaELU.Traits.SequenceEqual(stack.Concat(new[] { TraitId.RicaAwakenRaise, TraitId.RicaLayerFlurry, TraitId.UncapRaise }))
                && UnitCatalog.MugSproutMU.Traits.SequenceEqual(UnitCatalog.MugSproutM.Traits.Append(TraitId.UncapRaise))
                && new[] { UnitCatalog.RicaE, UnitCatalog.RicaL, UnitCatalog.RicaEL, UnitCatalog.RicaELU, UnitCatalog.MugSproutMU }.All(d => !UnitCatalog.All.Contains(d) && !UnitCatalog.Retired.Contains(d))
                && UnitCatalog.RicaE.PlusText.EndsWith(UnitCatalog.RicaAwakenClause378) && UnitCatalog.RicaL.PlusText.EndsWith(UnitCatalog.RicaFlurryClause378)
                && UnitCatalog.RicaELU.PlusText.Contains(UnitCatalog.RicaRaiseUncapClause378) && UnitCatalog.MugSproutMU.PlusText.EndsWith(UnitCatalog.MugSproutUncapClause378)
                && UnitCatalog.Rica.Traits.SequenceEqual(new[] { TraitId.Necro, TraitId.Sacrifice }) && UnitCatalog.Mug.Traits.SequenceEqual(new[] { TraitId.Splitter }) && UnitCatalog.All.Count == 52
                && UnitCatalog.RicaStack.Traits.SequenceEqual(UnitCatalog.Rica.Traits.Append(TraitId.RicaRaiseDeadStack)) && UnitCatalog.MugSproutM.Traits.SequenceEqual(UnitCatalog.Mug.Traits.Append(TraitId.MugSprout));
            Expect("定義: 版の駒 E ／ L ／ EL ／ ELU（リィカ）＝ 土台の R-積 ＋ 札・MU（ムグ）＝ M ＋ U（`All` の外）・文面は版ごとに叩き台をつなぐ・規定のリィカ ／ ムグと第377期の土台の駒は今どおり", defs);
        }

        // ---- E ----
        {
            var (ctx, p, e) = Board(4, (0, Mate), (4, UnitCatalog.RicaE));
            var rica = p.First(u => u.Def.Id == "rica");
            rica.SetCounter("necro", 2);
            Kill(ctx, e[0], rica);
            int under = Summons(ctx, UnitCatalog.Wight.Name);
            rica.SetCounter("necro", 3); rica.Hp = 30;
            Kill(ctx, e[1], rica);
            int over = Summons(ctx, UnitCatalog.Wight.Name);
            int hp1 = rica.Hp; long taken = Tal(ctx, rica).DamageTaken;
            rica.Hp = 5;
            Kill(ctx, e[2], rica);
            int hp2 = rica.Hp, over2 = Summons(ctx, UnitCatalog.Wight.Name);
            var t = Tal(ctx, rica);
            Expect("E: 3 層未満では敵の死から起きない・3 層以上で敵が倒れると亡者が起きる・代償 8 を直接払い（ダメージに数えない）HP 5 からは HP 1（倒れない）", under == 0 && over == 1 && hp1 == 22 && taken == 0 && hp2 == 1 && over2 == 2 && t.R378FoeRaises == 2,
                $"2 層: {under} 体 ／ 3 層: {over} 体・HP 30 → {hp1} ／ HP 5 → {hp2}・敵の亡骸から {t.R378FoeRaises}");
        }
        {
            var (ctx, p, e) = Board(2, (0, Mate), (4, UnitCatalog.RicaE));
            var rica = p.First(u => u.Def.Id == "rica");
            for (int i = 0; i < 4; i++) ctx.Summon(UnitCatalog.Fodder, BattleContext.PlayerTeam);
            rica.SetCounter("necro", 3);
            int n0 = Summons(ctx, UnitCatalog.Wight.Name);
            Kill(ctx, e[0], rica);
            Expect("E: 召喚枠が生きた駒で埋まっていれば起きない（空きなしを数える）", Summons(ctx, UnitCatalog.Wight.Name) == n0 && Tal(ctx, rica).R378FoeNoSeat == 1 && Tal(ctx, rica).R378FoeRaises == 0);
        }
        {
            var (ctx, p, e) = Board(2, (0, Mate), (4, UnitCatalog.RicaE));
            var rica = p.First(u => u.Def.Id == "rica");
            rica.SetCounter("necro", 3);
            Kill(ctx, e[0], rica);
            var w = ctx.LivingMembers(BattleContext.PlayerTeam).Single(u => u.Def.Id == UnitCatalog.Wight.Id);
            int l0 = rica.RawCounter("necro");
            ctx.LoseHp(w, 999, TraitId.Rot);
            Expect("E: 敵の亡骸から起きた亡者の死で層 +1（土台の R-積 と同じ）", rica.RawCounter("necro") == l0 + 1, $"{l0} → {rica.RawCounter("necro")}");
        }

        // ---- L ----
        {
            var (ctx, p, e) = Board(3, (4, UnitCatalog.RicaL));
            var rica = p.First(u => u.Def.Id == "rica");
            var fl = rica.Traits.OfType<VioFlurryTrait>().Single();
            int H(int layer) { rica.SetCounter("necro", layer); return fl.ModifyHitCount(rica, 1); }
            var hits = new[] { H(5), H(6), H(8), H(9), H(12) };
            rica.SetCounter(VioFlurryTrait.FirstKey, e[1].InstanceId + 1);
            var t2 = fl.NextTarget(ctx, rica, 1); var t3 = fl.NextTarget(ctx, rica, 2);
            var (ctx1, p1, e1) = Board(1, (4, UnitCatalog.RicaL));
            var r1 = p1.First(u => u.Def.Id == "rica");
            r1.SetCounter(VioFlurryTrait.FirstKey, e1[0].InstanceId + 1);
            var s2 = fl.NextTarget(ctx1, r1, 1); var s3 = fl.NextTarget(ctx1, r1, 2);
            Expect("L: 5 層は1発 ／ 6 層で2発 ／ 9 層で3発・2発目からは別の敵（席番号の小さい順）・敵が1体なら全部同じ敵", hits.SequenceEqual(new[] { 1, 2, 2, 3, 3 }) && t2 == e[0] && t3 == e[2] && s2 == e1[0] && s3 == e1[0],
                $"層 5/6/8/9/12 → {string.Join("/", hits)}・3 体: 2発目 {t2?.Slot} ／ 3発目 {t3?.Slot}（1発目 {e[1].Slot}）・1 体: {s2 == e1[0]} ／ {s3 == e1[0]}");
        }
        {
            // 台の上で: ボス（勇者1体）で L のリィカが同じターンに2回以上振り、的はすべて勇者
            long multi = 0, wrong = 0;
            var f = Apply(Row("追撃×死 (ハギ×リィカ)"), 3);
            for (int sd = 0; sd < 10; sd++)
            {
                var (r, p, e) = I.Raw(f, I.Boss, sd, verbose: true);
                var rica = p.First(u => u.Def.Id == "rica");
                var hero = e.MaxBy(u => u.MaxHp)!;
                foreach (var grp in r.Events.Where(x => x.Kind == BattleEventKind.Attack && x.ActorId == rica.InstanceId).GroupBy(x => x.Turn))
                {
                    if (grp.Count() >= 2) multi++;
                    if (e.Count(u => true) == 1 && grp.Any(x => x.TargetId != hero.InstanceId)) wrong++;
                }
            }
            Expect("L（台の上で）: 追撃×死 × ボス × seed 0..9 でリィカが1ターンに2回以上振るターンがある", multi > 0 && wrong == 0, $"2回以上振ったターン {multi}");
        }

        // ---- U ----
        foreach (var (name, def, id, spawn) in new[] { ("U 亡者起こし", UnitCatalog.RicaELU, "rica", UnitCatalog.Wight), ("U 亡骸の芽吹き", UnitCatalog.MugSproutMU, "mug", UnitCatalog.Spore) })
        {
            var (ctx, p, e) = Board(1, (0, Mate), (1, Mate), (2, Mate), (3, Mate), (4, def));
            var holder = p.First(u => u.Def.Id == id);
            var eph = ctx.Summon(UnitCatalog.Fodder, BattleContext.PlayerTeam)!;
            Kill(ctx, eph, e[0]);
            int afterEph = Summons(ctx, spawn.Name, holder.InstanceId);
            foreach (var m in p.Where(u => u.Def.Id == Mate.Id).ToList()) Kill(ctx, m, e[0]);
            int n = Summons(ctx, spawn.Name, holder.InstanceId);
            Expect($"{name}: 編成の駒が 4 体倒れると 4 体湧く（1戦に3回の上限なし）・儚い駒の死からは湧かない", afterEph == 0 && n == 4, $"儚い駒の死の後 {afterEph}・4 体の死の後 {n}");
        }

        // ---- 土台 ＝ 第377期の R-積M ----
        {
            long cells = 0, diff = 0;
            foreach (var name in S377.DeathRows)
            {
                var f = Row(name);
                bool sameDefs = Apply(f, 1).Occupied().Select(o => o.Def).SequenceEqual(S377.Apply(f, S377.HasMug(f) ? 4 : 2).Occupied().Select(o => o.Def));
                if (!sameDefs) diff += 1000;
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        var a = S377.One(Apply(f, 1), w, sd); var b = S377.One(S377.Apply(f, S377.HasMug(f) ? 4 : 2), w, sd);
                        if (a.Wins != b.Wins || a.Turns != b.Turns || a.FormDeaths != b.FormDeaths || a.PeakSum != b.PeakSum || a.Wights != b.Wights || a.Sprouts != b.Sprouts) diff++;
                    }
            }
            Expect("土台: 版の駒が第377期の R-積M（ムグなしは R-積）と同じで、`sh377` の数え方で結果が一致（死の4台 × 4 波 × seed 0..9）", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- 規定は今どおり ----
        {
            var bal = File.ReadAllLines("docs/balance.md").Where(l => l.StartsWith("| ")).Select(l => l.Split('|').Select(c => c.Trim()).ToArray()).Where(c => c.Length > 7).ToDictionary(c => c[1], c => c[2..7]);
            int rows = 0, bad = 0;
            foreach (var (name, f) in CompareBuilds().Where(r => S377.HasRica(r.F) || S377.HasMug(r.F)))
            {
                rows++;
                for (int wi = 0; wi < EnemyCatalog.Stages.Count; wi++)
                {
                    int wins = 0;
                    for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                    if (!bal.TryGetValue(name, out var cs) || cs[wi] != $"{100.0 * wins / CompareSeeds:F1}%") bad++;
                }
            }
            Expect("規定のリィカ ／ ムグのいる `compare` の行が `docs/balance.md` と一致（seed 0..199・いない行は版の写像で変わらない・全行は `checkup check`）", bad == 0 && rows > 0, $"{rows} 行 × {EnemyCatalog.Stages.Count} 波・ずれ {bad}");
        }

        // ---- 寄せ集め ／ 素体は器具の中だけ ----
        {
            bool ok = !UnitCatalog.Everyone.Any(d => d.Id.EndsWith("_plain", StringComparison.Ordinal))
                && !CompareBuilds().Concat(Presets.Versions).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => o.Def.Id.EndsWith("_plain", StringComparison.Ordinal)))
                && S377.MixBoardsOf(UnitCatalog.RicaELU).Select(S307.Seats).SequenceEqual(S377.MixBoardsOf(UnitCatalog.Rica).Select(S307.Seats));
            Expect("寄せ集めの台 ／ 素体は器具の中だけ（カタログ ／ `compare` ／ 試遊の台に素体が無い）・寄せ集めは第377期と同じ 50 台（リィカの版を替えても並びが同じ）", ok);
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var n in new[] { "版・E 死の連鎖", "版・EL 死の連鎖", "版・ELU 死の連鎖" })
            {
                var f = Presets.Versions.First(r => r.Name == n).F;
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（版の3台 × 勇者 ／ 近衛 ／ 大隊 ／ 第5波・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool ok = new[] { TraitId.RicaAwakenRaise, TraitId.RicaLayerFlurry, TraitId.UncapRaise }.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第378期"));
            Expect("`checkup` の札の分類表に E ／ L ／ U が第378期の行として入っている（ソースを読んで確かめる）", ok);
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            bool rows = vr.SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "試遊・毒 席替え", "試遊・毒 紅", "試遊・ヴィオ 台B", "試遊・死の連鎖", "版・R-積M 死の連鎖", "版・E 死の連鎖", "版・EL 死の連鎖", "版・ELU 死の連鎖" });
            var basis = Row("死の連鎖 (リィカ軸)");
            bool vers = new[] { ("版・E 死の連鎖", 2), ("版・EL 死の連鎖", 4), ("版・ELU 死の連鎖", 5) }.All(q => Presets.Versions.First(r => r.Name == q.Item1).F.Occupied().Select(o => o.Def).SequenceEqual(Apply(basis, q.Item2).Occupied().Select(o => o.Def)));
            Expect("`Presets.Versions` ＝ 第377期の台から「版・R-繋 死の連鎖」を外し、「版・E ／ EL ／ ELU 死の連鎖」を足した（並びは試遊・死の連鎖・リィカ ／ ムグだけ版）", rows && vers, string.Join("、", vr));
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 168d6224:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第377期の HEAD（168d6224）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    static Formation Row(string n) => S377.Row(n);
}
