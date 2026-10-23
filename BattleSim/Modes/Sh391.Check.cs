using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh391 check —— 第391期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh391Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "8d3f6ba0";   // 第390期の HEAD（この期の作業を始めたときの BattleCore ／ BattleSim）
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate391", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeDef(string id) => new() { Id = id, Name = "的" + id[^1], MaxHp = 1000, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;

    static (BattleContext Ctx, UnitState H, UnitState M, List<UnitState> E) Bench(UnitDef hagi, (int Slot, UnitDef D, int Hp)[] foes, int soul = 0)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); f[3] = hagi; f[2] = Mate;
        var g = new Formation(); foreach (var (s, d, _) in foes) g[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var (s, _, hp) in foes) e.First(u => u.Slot == s).Hp = hp;
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        var h = p.First(u => u.Def.Id == "hagi");
        if (soul > 0) HagiDepthTrait.Gain(ctx, h, soul, 0);
        return (ctx, h, p.First(u => u.Def.Id == "mate391"), e);
    }
    static UnitState At(List<UnitState> e, int slot) => e.First(u => u.Slot == slot);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh391 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef hagi = UnitCatalog.Hagi, p1 = UnitCatalog.HagiP1, p1w = UnitCatalog.HagiP1w, p1h = UnitCatalog.HagiP1h;
        UnitDef fa = FoeDef("foe391a"), fb = FoeDef("foe391b"), fc = FoeDef("foe391c"), fd = FoeDef("foe391d");

        // ---- 版の札と文面 ----
        {
            bool ok = new[] { p1, p1w, p1h }.All(d => d.Id == "hagi" && d.Name == hagi.Name && d.MaxHp == 62 && d.Attack == 16 && d.Speed == 7 && d.Pattern == AttackPattern.Single
                    && d.Traits.Take(5).SequenceEqual(hagi.Traits.Take(5)) && d.Traits.Contains(TraitId.HagiPursuitReap) && d.Traits[^1] == TraitId.HagiSpillFixed
                    && d.PlusText.Contains("（失った命が深いほど重く・線を切った敵なら、その一振りで刈り取る）", StringComparison.Ordinal) && d.MinusText == hagi.MinusText && !UnitCatalog.Everyone.Contains(d))
                && p1.Traits.Count == 7 && p1w.Traits.Contains(TraitId.HagiPursuitColumn) && !p1w.Traits.Contains(TraitId.HagiPursuitHeavy) && p1h.Traits.Contains(TraitId.HagiPursuitHeavy) && !p1h.Traits.Contains(TraitId.HagiPursuitColumn)
                && p1w.PlusText.StartsWith(p1.PlusText, StringComparison.Ordinal) && p1h.PlusText.StartsWith(p1.PlusText, StringComparison.Ordinal) && p1h.PlusText.EndsWith("2 割を乗せる", StringComparison.Ordinal)
                && !UnitCatalog.Everyone.Any(d => d.Traits.Contains(TraitId.HagiPursuitReap) || d.Traits.Contains(TraitId.HagiPursuitColumn) || d.Traits.Contains(TraitId.HagiPursuitHeavy));
            Expect("版の札と文面: P1 ＝ 規定 ＋ 追撃の刈り取り ／ P1w ＋ 追撃の列ごと ／ P1h ＋ 追撃 2 割・文面は追撃の句の括弧に「線を切った敵なら、その一振りで刈り取る」・`All` ／ `Retired` の外・札の保持者 0 枚", ok,
                string.Join(" ／ ", new[] { ("P1", p1), ("P1w", p1w), ("P1h", p1h) }.Select(x => $"{x.Item1} {string.Join("・", x.Item2.Traits.Skip(5).SkipLast(1))}")));
        }

        // ---- P1 ----
        {
            var notes = new List<string>(); bool ok = true;
            // 線以下（150 ＝ 15% ≦ 線 2 割）: 仲間の一撃で 600 → 150（5 割 ＋ 2 割を割った）→ 追撃が刈り取る。規定はふつうに斬る
            var (c1, h1, m1, e1) = Bench(p1, new[] { (0, fa, 600) }); c1.ApplyDamage(e1[0], 450, m1);
            var (c0, h0, m0, e0) = Bench(hagi, new[] { (0, fa, 600) }); c0.ApplyDamage(e0[0], 450, m0);
            var t1 = Tl(c1, h1); var t0 = Tl(c0, h0);
            bool g1 = !e1[0].IsAlive && t1.R391ChaseReaps == 1 && t1.R391ChaseReapKills == 1 && t1.R388Reaps == 0 && t1.R388Encores == 0 && t1.Kills == 1 && t1.R391ChaseReapHp == 150
                   && e0[0].IsAlive && e0[0].Hp == 150 - (16 + 850 / 10) && t0.R391ChaseReaps == 0
                   && HagiDepthTrait.Soul(h1) == HagiDepthTrait.Soul(h0) + HagiDepthTrait.PerDeath;
            ok &= g1; notes.Add($"線以下なら刈る（規定は 150 → {e0[0].Hp}）・再行動なし・魂は死の +2 だけ多い（P1 {HagiDepthTrait.Soul(h1)} ／ 規定 {HagiDepthTrait.Soul(h0)}）{(g1 ? "" : " ×")}");
            // 線より上（450 ＝ 45%）: ふつうに斬る（攻撃力 16 ＋ 失った 550 の 1 割）
            var (c2, h2, m2, e2) = Bench(p1, new[] { (0, fa, 600) }); c2.ApplyDamage(e2[0], 150, m2);
            bool g2 = e2[0].Hp == 450 - (16 + 55) && Tl(c2, h2).R391ChaseReaps == 0 && Tl(c2, h2).R388Pursuits == 1;
            ok &= g2; notes.Add($"線より上ならふつうに斬る（450 → {e2[0].Hp}）{(g2 ? "" : " ×")}");
            // 2 連（魂 6・線 4 割）: 1発目は 45% で斬る → 379（37.9%）→ 2発目の前にもう一度判定して刈る
            var (c3, h3, m3, e3) = Bench(p1, new[] { (0, fa, 600) }, soul: 6); c3.ApplyDamage(e3[0], 150, m3);
            var t3 = Tl(c3, h3);
            bool g3 = !e3[0].IsAlive && t3.R388Pursuits == 2 && t3.R391ChaseReaps == 1 && t3.R391ChaseReapSecond == 1 && t3.R388Encores == 0;
            ok &= g3; notes.Add($"2 連の2発目の前にもう一度判定して刈る（追撃 {t3.R388Pursuits}・2発目で {t3.R391ChaseReapSecond}）{(g3 ? "" : " ×")}");
            Expect("P1: 追撃の一撃の前に線以下なら刈る ／ 線より上ならふつうに斬る ／ 2 連の2発目の前にもう一度判定 ／ 追撃の刈り取りで倒しても再行動しない ／ 魂 +2", ok, string.Join("・", notes));
        }

        // ---- P1w ----
        {
            var notes = new List<string>(); bool ok = true;
            (int, UnitDef, int)[] foes = { (0, fa, 600), (1, fb, 450), (2, fc, 400), (3, fd, 600) };
            // 魂 9（線 5 割）: 前1 を 600 → 300（5 割を割る）→ 追撃の刈り取り ＋ 同じ列 ＋ 中列の線以下（45% ／ 40%）をまとめて刈る（後列の 60% は残る）
            var (c1, h1, m1, e1) = Bench(p1w, foes, soul: 9); c1.ApplyDamage(At(e1, 0), 300, m1);
            var (c2, h2, m2, e2) = Bench(p1, foes, soul: 9); c2.ApplyDamage(At(e2, 0), 300, m2);
            var t1 = Tl(c1, h1); var t2 = Tl(c2, h2);
            bool g1 = !At(e1, 0).IsAlive && !At(e1, 1).IsAlive && !At(e1, 2).IsAlive && At(e1, 3).IsAlive && t1.R391ChaseColumn == 1 && t1.R391ChaseColumnUnits == 2 && t1.R391ChaseReapKills == 3
                   && !At(e2, 0).IsAlive && At(e2, 1).IsAlive && At(e2, 2).IsAlive && t2.R391ChaseColumn == 0;
            ok &= g1; notes.Add($"魂 9 で追撃の刈り取りが列の線以下をまとめて刈る（3 体・P1 は 1 体）{(g1 ? "" : $" × col {t1.R391ChaseColumn}/{t1.R391ChaseColumnUnits} kills {t1.R391ChaseReapKills}")}");
            // 魂 8 以下（7 ＋ 5 割を割った +1 ＝ 8・線 4 割）: 単体
            var (c3, h3, m3, e3) = Bench(p1w, foes, soul: 7); c3.ApplyDamage(At(e3, 0), 300, m3);
            var t3 = Tl(c3, h3);
            bool g2 = HagiDepthTrait.Soul(h3) >= 8 && !At(e3, 0).IsAlive && At(e3, 2).IsAlive && At(e3, 1).IsAlive && t3.R391ChaseColumn == 0 && t3.R391ChaseReaps == 1;
            ok &= g2; notes.Add($"魂 8 以下では単体（中列の 40% は残る）{(g2 ? "" : " ×")}");
            Expect("P1w: 魂 9 で追撃の刈り取りが列の線以下の敵をまとめて刈る ／ 魂 8 以下では単体", ok, string.Join("・", notes));
        }

        // ---- P1h ----
        {
            var (c1, h1, m1, e1) = Bench(p1h, new[] { (0, fa, 600) }); c1.ApplyDamage(e1[0], 150, m1);
            var (c2, h2, m2, e2) = Bench(p1, new[] { (0, fa, 600) }); c2.ApplyDamage(e2[0], 150, m2);
            bool ok = e1[0].Hp == 450 - (16 + 550 * 20 / 100) && e2[0].Hp == 450 - (16 + 550 * 10 / 100) && HagiDepthTrait.PursuitHeavyPercent == 20;
            Expect("P1h: 追撃の量が攻撃力 ＋ 失った HP の 2 割（P1 は 1 割）", ok, $"P1h 450 → {e1[0].Hp} ／ P1 450 → {e2[0].Hp}");
        }

        // ---- 規定のハギ ／ compare ----
        {
            var map = S390.BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var c)) { miss++; continue; }
                for (int w = 0; w < 5; w++) if (c[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            string Row(string doc) => doc.Split('\n').FirstOrDefault(l => l.StartsWith($"| **{hagi.Name}** |", StringComparison.Ordinal))?.TrimEnd('\r') ?? "";
            string head = S390.GitShow($"{HeadSha}:docs/units.md"), now = File.Exists("docs/units.md") ? File.ReadAllText("docs/units.md") : "";
            bool def = hagi.Traits.SequenceEqual(UnitCatalog.HagiR5m.Traits) && UnitCatalog.All.Contains(hagi);
            Expect($"規定のハギは今どおり（札 ＝ R5m・`docs/units.md` の行が第390期の HEAD（{HeadSha}）と同じ）・規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致（ハギのいない行も）",
                def && Row(head) != "" && Row(head) == Row(now) && diff == 0 && miss == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 試遊の台 ／ 替えた台は器具の中だけ ----
        {
            var names = Presets.Versions.Select(r => r.Name).ToList();
            var vers = new[] { p1, p1w, p1h, UnitCatalog.HagiV1, UnitCatalog.HagiV2, UnitCatalog.HagiV3, UnitCatalog.HagiVA };
            var verRows = Presets.Versions.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool rows = !names.Contains("版・VA ハギ") && names.Contains("版・移動 ハギ") && names[^1] == "版・P1 ハギ" && verRows.SequenceEqual(new[] { "版・P1 ハギ" })
                && Presets.Versions[^1].F.Occupied().Count(o => o.Def == p1 || o.Def == p1w || o.Def == p1h) == 1;
            var h390 = VersionsH390();
            bool past = !h390.Any(r => r.Name == "版・P1 ハギ") && h390.Select(r => r.Name).TakeLast(2).SequenceEqual(VersionRows390)
                && Som307Diag.Seats(h390.First(r => r.Name == "版・VA ハギ").F) == Som307Diag.Seats(Presets.HagiDeath389(UnitCatalog.HagiVA))
                && h390.First(r => r.Name == "版・VA ハギ").F[0] == UnitCatalog.HagiVA
                && !VersionsH389().Any(r => VersionRows390.Contains(r.Name) || r.Name == "版・P1 ハギ");
            bool inside = !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions.SkipLast(1)).SelectMany(r => r.F.Occupied()).Any(o => vers.Contains(o.Def) || o.Def.Id.EndsWith("_plain388", StringComparison.Ordinal));
            Expect("試遊の台: 「版・VA ハギ」を外し（第390期の一覧は `VersionsH390` が足し戻す）、末尾に「版・P1 ハギ」・「版・移動 ハギ」は残す・替えた台 ／ 素体 ／ 版は器具の中だけ", rows && past && inside, string.Join("、", names.TakeLast(3)));
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var cards = new[] { TraitId.HagiPursuitReap, TraitId.HagiPursuitColumn, TraitId.HagiPursuitHeavy };
            var miss = cards.Where(t => !System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(HcPlusL,\s*""[^""]*第391期[^""]*保持者 0 枚""")).ToList();
            Expect("`checkup` の札の分類表に第391期の札 3 枚（追撃の刈り取り ／ 追撃の列ごと ／ 追撃 2 割）がある（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in S390.MainBoards())
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            cells++;
                            var f = ColBoard(b, v.D);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（3 台 × 版 4 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第390期の HEAD（{HeadSha}）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
