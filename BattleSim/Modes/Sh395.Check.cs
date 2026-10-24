using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh395 check —— 第395期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh395Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "f79f7422";   // 第394期の HEAD
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static readonly UnitDef Mate = new() { Id = "mate395", Name = "仲間", MaxHp = 50, Attack = 5, Speed = 8, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe395", Name = "的", MaxHp = 1000, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    /// <summary>ドルガ（版・後1）・仲間 2 体（前1 ／ 前3）と的 1 体。ターン <paramref name="turn"/>。</summary>
    static (BattleContext C, UnitState D, UnitState M, UnitState M2, UnitState E) Bench(UnitDef dolga, int turn)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); f[3] = dolga; f[0] = Mate; f[1] = Mate;
        var g = new Formation(); g[0] = Foe;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, turn);
        var ms = p.Where(u => u.Def.Id == "mate395").ToList();
        return (ctx, p.First(u => u.Def.Id == "dolga"), ms[0], ms[1], e[0]);
    }

    /// <summary>1発: 戻り値は（仲間が失った HP・ドルガが失った HP・壁の回数・下がった回数）。</summary>
    static (int Mate, int Dolga, long Hits, long Retreats) Hit(UnitDef def, int turn, int amount = 40, int? hp = null, AttackPattern? pat = AttackPattern.Single, bool waking = false)
    {
        var (c, dl, m, _, e) = Bench(def, turn);
        if (hp is { } h) dl.Hp = h;
        if (waking) c.TaillightWaking = dl;
        int mh = m.Hp, dh = dl.Hp;
        c.ApplyDamage(m, amount, e, pattern: pat);
        return (mh - m.Hp, dh - dl.Hp, Tl(c, dl).R394WallHits, Tl(c, dl).R395WallRetreats);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh395 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef d = UnitCatalog.Dolga, d2 = UnitCatalog.DolgaD2, e1 = UnitCatalog.DolgaE1, e2 = UnitCatalog.DolgaE2, e12 = UnitCatalog.DolgaE12;
        int max = d.MaxHp;   // 85

        // ---- 版の札と文面 ----
        {
            bool ok = new[] { e1, e2, e12 }.All(v => v.Id == "dolga" && v.Name == d.Name && v.MaxHp == d.MaxHp && v.Attack == d.Attack && v.Speed == d.Speed && v.Pattern == d.Pattern
                    && v.Traits.Take(d2.Traits.Count).SequenceEqual(d2.Traits) && v.MinusText == d2.MinusText && !UnitCatalog.Everyone.Contains(v))
                && e1.Traits.Skip(d2.Traits.Count).SequenceEqual(new[] { TraitId.DolgaWallRetreat }) && e2.Traits.Skip(d2.Traits.Count).SequenceEqual(new[] { TraitId.DolgaWallLight })
                && e12.Traits.Skip(d2.Traits.Count).SequenceEqual(new[] { TraitId.DolgaWallRetreat, TraitId.DolgaWallLight })
                && e1.PlusText.EndsWith("単体の攻撃を半分にして代わりに受ける（自分が半分まで傷つくと下がる）", StringComparison.Ordinal)
                && e2.PlusText.EndsWith("単体の攻撃の 3 割を代わりに受ける", StringComparison.Ordinal)
                && e12.PlusText.EndsWith("単体の攻撃の 3 割を代わりに受ける（自分が半分まで傷つくと下がる）", StringComparison.Ordinal)
                && !UnitCatalog.Everyone.Any(u => u.Traits.Contains(TraitId.DolgaWallRetreat) || u.Traits.Contains(TraitId.DolgaWallLight));
            Expect("版の札と文面: E1 ＝ D2 ＋ 倒れる前に下がる ／ E2 ＝ D2 ＋ 浅く受ける ／ E12 ＝ 両方・文面は叩き台・`All` ／ `Retired` の外・数値はドルガのまま", ok,
                string.Join(" ／ ", new[] { ("E1", e1), ("E2", e2), ("E12", e12) }.Select(x => $"{x.Item1} ＋{string.Join("・", x.Item2.Traits.Skip(d2.Traits.Count))}")));
        }

        // ---- E1 ----
        {
            var notes = new List<string>(); bool ok = true;
            var full = Hit(e1, 1); var at43 = Hit(e1, 1, hp: 43); var at42 = Hit(e1, 1, hp: 42); var at44 = Hit(e1, 1, hp: 44);
            bool g1 = full.Mate == 0 && full.Dolga == 20 && full.Retreats == 0 && at43.Mate == 0 && at43.Dolga == 20 && at42.Mate == 40 && at42.Dolga == 0 && at42.Retreats == 1 && at42.Hits == 0;
            ok &= g1; notes.Add($"受ける前の HP 85 ／ 43 は壁・42（最大 {max} の半分を切る）は下がって仲間が 40 を受ける{(g1 ? "" : " ×")}");
            bool g2 = at44.Dolga == 20 && at44.Mate == 0;   // 受けた結果で 24（半分を切る）になるのは構わない
            ok &= g2; notes.Add($"判定は受ける前の HP（44 → 24 でもその一撃は受ける）{(g2 ? "" : " ×")}");
            // 回復で戻れば再び壁: 同じ盤で 42 → 下がる、60 に戻す → 壁
            var (c, dl, m, _, e) = Bench(e1, 1);
            dl.Hp = 42; int m0 = m.Hp; c.ApplyDamage(m, 40, e, pattern: AttackPattern.Single); bool r1 = m0 - m.Hp == 40;
            dl.Hp = 60; int m1 = m.Hp, d1h = dl.Hp; c.ApplyDamage(m, 4, e, pattern: AttackPattern.Single); bool r2 = m1 - m.Hp == 0 && d1h - dl.Hp == 2;
            ok &= r1 && r2; notes.Add($"回復で半分以上に戻ると再び壁{(r1 && r2 ? "" : " ×")}");
            Expect("E1: 受ける前の HP が最大の半分未満なら壁をしない ／ 半分以上ならする ／ 回復で戻れば再び壁になる", ok, string.Join("・", notes));
        }

        // ---- E2 ----
        {
            var a = Hit(e2, 1); var b = Hit(e2, 1, amount: 41); var lo = Hit(e2, 1, hp: 30);
            bool ok = a.Dolga == 12 && a.Mate == 28 && a.Hits == 1 && b.Dolga == 12 && b.Mate == 29 && lo.Dolga == 12 && lo.Mate == 28;
            Expect("E2: 受ける量は 3 割（切り捨て）・殴られた味方は残りを受ける・HP が低くても下がらない", ok, $"40 → ドルガ {a.Dolga} ／ 仲間 {a.Mate}・41 → {b.Dolga} ／ {b.Mate}・HP 30 でも {lo.Dolga} ／ {lo.Mate}");
        }

        // ---- E12 ----
        {
            var a = Hit(e12, 1); var lo = Hit(e12, 1, hp: 42); var hi = Hit(e12, 1, hp: 43);
            bool ok = a.Dolga == 12 && a.Mate == 28 && lo.Mate == 40 && lo.Dolga == 0 && lo.Retreats == 1 && hi.Dolga == 12 && hi.Mate == 28;
            Expect("E12: 3 割で受け、受ける前の HP が半分未満なら下がる", ok, $"満タン {a.Dolga} ／ {a.Mate}・HP 42 は下がる（仲間 {lo.Mate}）・HP 43 は受ける（{hi.Dolga} ／ {hi.Mate}）");
        }

        // ---- D2 の壁の規則は今どおり（版の上でも）----
        {
            var notes = new List<string>(); bool ok = true;
            foreach (var (name, v) in new[] { ("D2", d2), ("E1", e1), ("E2", e2), ("E12", e12) })
            {
                var act = Hit(v, 2); var sw = Hit(v, 1, pat: AttackPattern.Sweep); var al = Hit(v, 1, pat: AttackPattern.All); var pi = Hit(v, 1, pat: AttackPattern.Pierce); var wk = Hit(v, 1, waking: true);
                bool g = act.Mate == 40 && act.Dolga == 0 && new[] { sw, al, pi }.All(x => x.Mate == 40 && x.Dolga == 0 && x.Hits == 0) && wk.Dolga > 0 && wk.Hits == 1;
                ok &= g; notes.Add($"{name}{(g ? "" : " ×")}");
            }
            var pl = Hit(d, 2);
            bool g0 = pl.Mate == 40 && pl.Dolga == 0 && d.Traits.SequenceEqual(new[] { TraitId.Sluggish, TraitId.GradeStep }) && d.MinusText == "2ターンに1回しか動けない" && UnitCatalog.All.Contains(d);
            string units = File.Exists("docs/units.md") ? File.ReadAllText("docs/units.md") : "", head = S390.GitShow($"{HeadSha}:docs/units.md");
            string Row(string s) => s.Split('\n').FirstOrDefault(l => l.StartsWith("| **のろまの巨兵ドルガ** |", StringComparison.Ordinal))?.TrimEnd('\r') ?? "";
            g0 &= Row(units) != "" && Row(units) == Row(head);
            ok &= g0; notes.Add($"規定のドルガは今どおり（壁なし・札・`docs/units.md` の行が第394期の HEAD と同じ）{(g0 ? "" : " ×")}");
            Expect("D2 の休み番（奇数ターン）／ 単体だけ ／ 起こされたターンも壁、は版の上でも今どおり・規定のドルガは今どおり", ok, string.Join("・", notes));
        }

        // ---- 規定の compare ----
        {
            var map = S390.BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var cc)) { miss++; continue; }
                for (int w = 0; w < 5; w++) if (cc[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            Expect("規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致（版の札は保持者 0）", diff == 0 && miss == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 替えた台 ／ 素体は器具の中だけ・試遊の台 ----
        {
            var vers = new[] { e1, e2, e12 };
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions).SelectMany(r => r.F.Occupied()).Select(o => o.Def).ToList();
            bool inside = !everywhere.Any(x => x.Id.EndsWith("_plain392", StringComparison.Ordinal));
            var verRows = Presets.Versions.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool rows = verRows.SequenceEqual(VersionRows395) && Presets.Versions[^1].Name == "版・壁 E" && Presets.Versions[^1].F.Occupied().Count(o => vers.Contains(o.Def)) == 1
                && !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => vers.Contains(o.Def)))
                && VersionsH394().Select(r => r.Name).SequenceEqual(Presets.Versions.Select(r => r.Name).SkipLast(1)) && VersionsH394()[^1].Name == "版・壁 ドルガ";
            Expect("替えた台 ／ 素体は器具の中だけ・E の版のドルガが居る試遊の台は「版・壁 E」（`Presets.Versions` の末尾）だけ・第394期の一覧は `VersionsH394()`", inside && rows, string.Join("、", verRows));
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var miss = new[] { TraitId.DolgaWallRetreat, TraitId.DolgaWallLight }.Where(t => !System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第395期[^""]*保持者 0 枚""")).ToList();
            Expect("`checkup` の札の分類表に第395期の札 2 枚（倒れる前に下がる ／ 浅く受ける）がある（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in BoardList().Take(5))
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            cells++;
                            var f = Put(b.F, b.Slot, v.D);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（台 5 × 版 5 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, tr0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1n = Now("BattleCore/BattleEngine.cs", "PickOne("), tr1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第394期の HEAD（{HeadSha}）と同じ）", e0 == e1n && tr0 == tr1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1n}・traits {tr0} → {tr1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
