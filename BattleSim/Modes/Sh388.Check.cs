using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh388 check —— 第388期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh388Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    const string HeadSha = "69788e58";   // 第387期の HEAD（この期の作業を始めたコミット）
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate388", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Mate2 = new() { Id = "mate388b", Name = "仲間2", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeDef(string id, params TraitId[] tr) => new() { Id = id, Name = "的" + id[^1], MaxHp = 999, Attack = 1, Speed = 1, Traits = tr, PlusText = "" };

    /// <summary>盤を組む（敵は渡した席に・HP は <paramref name="hp"/> の順に書き換える・ターン 1）。</summary>
    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Bench((int Slot, UnitDef D)[] mine, (int Slot, UnitDef D, int Hp)[] foes)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); foreach (var (s, d) in mine) f[s] = d;
        var g = new Formation(); foreach (var (s, d, _) in foes) g[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var (s, _, hp) in foes) e.First(u => u.Slot == s).Hp = hp;
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        return (ctx, p, e);
    }
    static UnitState Hg(List<UnitState> p) => p.First(u => u.Def.Id == "hagi");
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;

    static string Git(string spec)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", $"show {spec}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x;
        }
        catch { return ""; }
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh388 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef fa = FoeDef("foe388a"), fb = FoeDef("foe388b"), fc = FoeDef("foe388c"), fd = FoeDef("foe388d"), fy = FoeDef("foe388y", TraitId.Yoke);

        // ---- 版の札と文面 ----
        {
            var h = UnitCatalog.HagiH389;   // 第390期: 第388期の規定（追い打ち）に固定
            bool ok = Vers.All(v => v.D.Id == "hagi" && v.D.Name == h.Name && v.D.MaxHp == 62 && v.D.Attack == 16 && v.D.Speed == 7 && v.D.Pattern == AttackPattern.Single && v.D.Actions is null
                                    && !v.D.Traits.Contains(TraitId.Pursuer) && v.D.Traits.Contains(TraitId.HagiReap) && v.D.Traits[^1] == TraitId.HagiScytheSpill
                                    && !UnitCatalog.Everyone.Contains(v.D))
                && UnitCatalog.HagiH1.Traits.Count == 2 && UnitCatalog.HagiH2.Traits.Contains(TraitId.HagiHalfPursuit) && !UnitCatalog.HagiH2.Traits.Contains(TraitId.HagiSoul)
                && UnitCatalog.HagiH3.Traits.Contains(TraitId.HagiSoul) && !UnitCatalog.HagiH3.Traits.Contains(TraitId.HagiHaste) && UnitCatalog.HagiH3s.Traits.Contains(TraitId.HagiHaste)
                && UnitCatalog.HagiH3s.PlusText.EndsWith("。刈るたび、速くなる", StringComparison.Ordinal) && UnitCatalog.HagiH3.PlusText.StartsWith(UnitCatalog.HagiH2.PlusText, StringComparison.Ordinal)
                && UnitCatalog.HagiH2.PlusText.StartsWith(UnitCatalog.HagiH1.PlusText, StringComparison.Ordinal) && UnitCatalog.HagiH3.MinusText.StartsWith(UnitCatalog.HagiH1.MinusText, StringComparison.Ordinal);
            bool def = h.Traits.SequenceEqual(new[] { TraitId.Pursuer }) && h.Pattern == AttackPattern.Sweep && h.Id == "hagi";   // 第390期: 旧の規定（`All` の外）に固定
            Expect("版の札と文面: 体は今のハギ（62 ／ 16 ／ 7）・追い打ちを外し単体・H1 刈り取り ＋ 巻き込み ／ H2 ＋ 追撃 ／ H3 ＋ 魂の段 ／ H3s ＋ 加速（文面は H3 から句を外す ／ 足す）・`All` ／ `Retired` の外・規定のハギは今どおり（追い打ち ／ 薙ぎ）",
                ok && def, string.Join(" ／ ", Vers.Select(v => $"{v.Name} {string.Join("・", v.D.Traits)}")));
        }

        // ---- 刈り取り ----
        {
            var notes = new List<string>(); bool ok = true;
            // (1) HP の割合が最も低い敵を狙う・2 割以下で残りを与えて倒す（軛の保持者がいても切られない）・倒せばもう一度（2 発目は線の上なのでふつうに斬る）
            {
                var (c, p, e) = Bench(new[] { (2, UnitCatalog.HagiH1), (0, Mate) }, new[] { (0, fy, 999), (1, fb, 150) });
                var h = Hg(p); c.TakeTurn(h);
                var y = e.First(u => u.Slot == 0); var b = e.First(u => u.Slot == 1); var t = Tl(c, h);
                bool g = !b.IsAlive && t.R388Reaps == 1 && t.R388ReapHp == 150 && t.R388Encores == 1 && t.R388Strikes == 1 && y.Hp == 999 - 16;
                ok &= g; notes.Add($"刈り取り 150（軛の下）→ 再行動で 16{(g ? "" : $" × y{y.Hp} b{b.Hp} r{t.R388Reaps} e{t.R388Encores} s{t.R388Strikes}")}");
            }
            // (2) 2 割より上ならふつうに斬る（攻撃力ぶん）・倒さなければ再行動なし
            {
                var (c, p, e) = Bench(new[] { (2, UnitCatalog.HagiH1) }, new[] { (0, fa, 999), (1, fb, 250) });
                var h = Hg(p); c.TakeTurn(h);
                var b = e.First(u => u.Slot == 1); var t = Tl(c, h);
                bool g = b.Hp == 250 - 16 && t.R388Reaps == 0 && t.R388Strikes == 1 && t.R388Encores == 0;
                ok &= g; notes.Add($"25% はふつうに 16{(g ? "" : $" × {b.Hp}")}");
            }
            // (3) 再行動は 2 回まで（4 体とも線の下 → 3 体刈って止まる）
            {
                var (c, p, e) = Bench(new[] { (2, UnitCatalog.HagiH1) }, new[] { (0, fa, 100), (1, fb, 110), (2, fc, 120), (3, fd, 130) });
                var h = Hg(p); c.TakeTurn(h);
                var t = Tl(c, h);
                bool g = e.Count(u => u.IsAlive) == 1 && e.First(u => u.IsAlive).Slot == 3 && t.R388Reaps == 3 && t.R388Encores == 2;
                ok &= g; notes.Add($"4 体 → 3 体刈って止まる{(g ? "" : $" × 生 {e.Count(u => u.IsAlive)} r{t.R388Reaps} e{t.R388Encores}")}");
            }
            Expect("刈り取り: HP の割合が最も低い敵を狙う ／ 2 割以下は残りの HP をそのまま与えて倒す（軛に切られない）／ それより上は攻撃力ぶん ／ 自分で倒せば同じ手番でもう一度（2 回まで）", ok, string.Join("・", notes));
        }

        // ---- 追撃 ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c, p, e) = Bench(new[] { (3, UnitCatalog.HagiH2), (2, Mate) }, new[] { (0, fa, 599), (1, fb, 520) });
            var h = Hg(p); var m = p.First(u => u.Def.Id == "mate388"); var a = e.First(u => u.Slot == 0); var b = e.First(u => u.Slot == 1); var t = Tl(c, h);
            // (1) 5 割を上から割った一撃の後に1回・量 ＝ 攻 16 ＋ 失った HP の 1 割
            c.ApplyDamage(a, 100, m);   // 599 → 499（割った）→ 追撃 16 ＋ (999 − 499) / 10 = 66 → 433
            bool g1 = a.Hp == 433 && t.R388Pursuits == 1 && t.R388PursuitDmg == 66;
            ok &= g1; notes.Add($"割った → 66（16 ＋ 50）{(g1 ? "" : $" × hp{a.Hp} n{t.R388Pursuits} d{t.R388PursuitDmg}")}");
            // (2) 1つの敵に1ターンに1回（上へ戻して同じターンにまた割っても追撃しない）・次のターンなら追撃する
            c.Heal(a, 500); c.ApplyDamage(a, 200, m);
            bool g2 = t.R388Pursuits == 1;
            TurnField.SetValue(c, 2); c.Heal(a, 500); int before = a.Hp; c.ApplyDamage(a, before - 450, m);
            bool g3 = t.R388Pursuits == 2;
            ok &= g2 && g3; notes.Add($"同じターンは 1 回 ／ 次のターンにまた{(g2 && g3 ? "" : $" × {t.R388Pursuits}")}");
            // (3) 追撃では刈り取らない（12% まで割られても 攻 ＋ 1 割を当てるだけ）
            c.ApplyDamage(b, 400, m);   // 520 → 120（割った・12%）→ 16 ＋ 87 = 103 → 17
            bool g4 = b.Hp == 17 && b.IsAlive && t.R388Pursuits == 3;
            ok &= g4; notes.Add($"追撃では刈り取らない（120 → 17）{(g4 ? "" : $" × {b.Hp}")}");
            // (4) ハギ自身の攻撃で割ったときは追撃しない
            var (c2, p2, e2) = Bench(new[] { (3, UnitCatalog.HagiH2) }, new[] { (0, fa, 510) });
            var h2 = Hg(p2); c2.ApplyDamage(e2[0], 20, h2);
            bool g5 = Tl(c2, h2).R388Pursuits == 0 && Tl(c2, h2).R388PursuitSelf == 1 && e2[0].Hp == 490;
            ok &= g5; notes.Add($"自分の一撃で割っても追撃しない{(g5 ? "" : " ×")}");
            // (5) H1（追撃の札なし）は追撃しない
            var (c3, p3, e3) = Bench(new[] { (3, UnitCatalog.HagiH1), (2, Mate) }, new[] { (0, fa, 599) });
            c3.ApplyDamage(e3[0], 100, p3.First(u => u.Def.Id == "mate388"));
            bool g6 = Tl(c3, Hg(p3)).R388Pursuits == 0 && e3[0].Hp == 499;
            ok &= g6; notes.Add($"H1 は追撃しない{(g6 ? "" : " ×")}");
            Expect("追撃: 5 割を上から下へ割った一撃の後に1回 ／ 量が攻撃力 ＋ 失った HP の 1 割 ／ 1つの敵に1ターン1回 ／ ハギ自身の攻撃では起きない ／ 追撃では刈り取らない", ok, string.Join("・", notes));
        }

        // ---- 魂 ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c, p, e) = Bench(new[] { (3, UnitCatalog.HagiH3), (2, Mate) }, new[] { (0, fa, 999), (1, fb, 599), (3, fc, 999) });
            var h = Hg(p); var m = p.First(u => u.Def.Id == "mate388");
            int atk0 = h.CurrentAttack;
            c.ApplyDamage(e.First(u => u.Slot == 0), 9999, m);   // 敵の死 +2 → 魂 2・攻 +15
            bool g1 = HagiReaperTrait.Soul(h) == 2 && h.CurrentAttack == atk0 + 15 && h.CurrentPattern == AttackPattern.Single;
            c.ApplyDamage(e.First(u => u.Slot == 1), 100, m);    // 割った +1 → 魂 3・攻 +30・薙ぎ（追撃も入る）
            bool g2 = HagiReaperTrait.Soul(h) == 3 && h.CurrentAttack == atk0 + 30 && h.CurrentPattern == AttackPattern.Sweep && Tl(c, h).R388SoulDeath == 2 && Tl(c, h).R388SoulCross == 1;
            ok &= g1 && g2; notes.Add($"死 +2 ／ 割る +1・攻 +5 × n(n+1)/2・3 で薙ぎ{(g1 && g2 ? "" : $" × 魂 {HagiReaperTrait.Soul(h)} 攻 {h.CurrentAttack}")}");
            // 6 ／ 9 の段
            HagiReaperTrait.Gain(c, h, 3, death: true);
            bool g3 = HagiReaperTrait.Soul(h) == 6 && HagiReaperTrait.ReapLine(h) == 30 && HagiReaperTrait.PursuitHits(h) == 1 && h.CurrentAttack == atk0 + 105;
            HagiReaperTrait.Gain(c, h, 3, death: true);
            bool g4 = HagiReaperTrait.Soul(h) == 9 && HagiReaperTrait.PursuitHits(h) == 2 && h.CurrentAttack == atk0 + 225;
            ok &= g3 && g4; notes.Add($"6 で線 3 割 ／ 9 で追撃 2 連{(g3 && g4 ? "" : " ×")}");
            // 薄れる（前のターンに何も無ければ 1 つ）・繋がっていれば薄れない
            var tr = h.Traits.First(x => x.Id == TraitId.HagiSoul);
            TurnField.SetValue(c, 2); tr.OnTurnStart(c, h);
            bool g5 = HagiReaperTrait.Soul(h) == 9;
            TurnField.SetValue(c, 3); tr.OnTurnStart(c, h);
            bool g6 = HagiReaperTrait.Soul(h) == 8 && h.CurrentAttack == atk0 + 180;
            ok &= g5 && g6; notes.Add($"繋がれば保つ ／ 途切れたら 1 つ薄れる{(g5 && g6 ? "" : $" × {HagiReaperTrait.Soul(h)}")}");
            // 会戦をまたがない
            tr.OnCarryOver(h);
            bool g7 = HagiReaperTrait.Soul(h) == 0 && h.RawCounter(HagiReaperTrait.SoulBonusKey) == 0;
            ok &= g7; notes.Add($"会戦の境界で 0{(g7 ? "" : " ×")}");
            // H2（魂の札なし）は魂を持たない
            var (c2, p2, e2) = Bench(new[] { (3, UnitCatalog.HagiH2), (2, Mate) }, new[] { (0, fa, 999) });
            c2.ApplyDamage(e2[0], 9999, p2.First(u => u.Def.Id == "mate388"));
            bool g8 = HagiReaperTrait.Soul(Hg(p2)) == 0 && Hg(p2).CurrentAttack == 16;
            ok &= g8; notes.Add($"H2 は魂なし{(g8 ? "" : " ×")}");
            Expect("魂: 敵の死 +2 ／ 割る +1 ／ 攻撃力が三角数（+5 × n(n+1)/2）／ 3 薙ぎ ／ 6 刈り取りの線 3 割 ／ 9 追撃 2 連 ／ 前のターンに何も無ければ薄れる ／ 会戦をまたがない", ok, string.Join("・", notes));
        }

        // ---- 加速 ／ 巻き込み ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c, p, e) = Bench(new[] { (2, UnitCatalog.HagiH3s) }, new[] { (0, fa, 100), (1, fb, 999) });
            var h = Hg(p); c.TakeTurn(h);
            bool g1 = !e[0].IsAlive && h.RawCounter(HagiReaperTrait.HasteKey) == 2 && c.TurnSpeed(h) == 9;
            var (c2, p2, e2) = Bench(new[] { (2, UnitCatalog.HagiH3) }, new[] { (0, fa, 100), (1, fb, 999) });
            c2.TakeTurn(Hg(p2));
            bool g2 = Hg(p2).RawCounter(HagiReaperTrait.HasteKey) == 0 && c2.TurnSpeed(Hg(p2)) == 7;
            ok &= g1 && g2; notes.Add($"H3s は刈って倒すたび速さ +2（7 → 9）・H3 は加速しない{(g1 && g2 ? "" : " ×")}");
            // 巻き込み: 隣の味方それぞれに攻撃力の 1 割（最低 1）・隣でない味方には無し
            var (c3, p3, e3) = Bench(new[] { (0, UnitCatalog.HagiH1), (2, Mate), (1, Mate2) }, new[] { (0, fa, 999) });
            var h3 = Hg(p3); c3.TakeTurn(h3);
            var near = p3.First(u => u.Def.Id == "mate388"); var far = p3.First(u => u.Def.Id == "mate388b");
            bool adjN = FormationRules.AreAdjacent(h3, near), adjF = FormationRules.AreAdjacent(h3, far);
            bool g3 = near.Hp == (adjN ? 49 : 50) && far.Hp == (adjF ? 49 : 50) && adjN && Tl(c3, h3).R388SpillHits == (adjN ? 1 : 0) + (adjF ? 1 : 0);
            // 攻撃力 46（魂 3）なら 4
            var (c4, p4, e4) = Bench(new[] { (2, UnitCatalog.HagiH3), (0, Mate) }, new[] { (0, fa, 999), (1, fb, 999) });
            var h4 = Hg(p4); HagiReaperTrait.Gain(c4, h4, 3, death: true); c4.TakeTurn(h4);
            bool g4 = p4.First(u => u.Def.Id == "mate388").Hp == 50 - 4;
            ok &= g3 && g4; notes.Add($"巻き込み 攻 16 → 1（隣だけ・隣でない味方は {(adjF ? "隣" : "無し")}）／ 攻 46 → 4{(g3 && g4 ? "" : $" × {near.Hp} {far.Hp} {p4.First(u => u.Def.Id == "mate388").Hp}")}");
            Expect("加速: 刈り取りで倒すたび速さ +2（行動順の速さ）・巻き込み: 攻撃のたび隣の味方に攻撃力の 1 割（最低 1）", ok, string.Join("・", notes));
        }

        // ---- 規定のハギ ／ ハギのいない compare の行 ----
        {
            string head = Git($"{HeadSha}:docs/units.md"), now = Git("0d48cccb:docs/units.md");   // 第390期: 規定のハギを替えたので、第389期の HEAD の表と比べる（第388 ／ 389期にハギの行が動いていないこと）
            string Row(string doc) => UnitRowOf(doc, UnitCatalog.Hagi.Name);   // 第406期: 改名（彼岸花のハギ）の前後どちらの表でも引く
            var bal = File.Exists("docs/balance.md") ? File.ReadAllLines("docs/balance.md") : Array.Empty<string>();
            var map = new Dictionary<string, string[]>();
            foreach (var l in bal) { var cc = l.Split('|').Select(x => x.Trim()).ToArray(); if (cc.Length >= 8 && cc[2].EndsWith('%')) map[cc[1]] = cc.Skip(2).Take(5).ToArray(); }
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0, hagiRows = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (Has(rows[ri].F, "hagi")) hagiRows++;
                if (!map.TryGetValue(rows[ri].Name, out var c)) { miss++; continue; }
                for (int w = 0; w < 5; w++) if (c[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            Expect($"規定のハギは今どおり（`docs/units.md` の行が第387期の HEAD（{HeadSha}）と同じ）・規定の `compare` 全行（ハギのいる行も・いない行も）× 5 波 × seed 0..199 が `docs/balance.md` と一致",
                Row(head) != "" && Row(head) == Row(now) && diff == 0 && miss == 0, $"{rows.Length} 行（ハギ {hagiRows}）・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 替えた台 ／ 素体は器具の中だけ・試遊の台の 2 台 ----
        {
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions).SelectMany(r => r.F.Occupied()).Select(o => o.Def).ToList();
            bool inside = !everywhere.Any(d => d.Id.EndsWith("_plain388", StringComparison.Ordinal)) && !UnitCatalog.Everyone.Any(d => d.Id.EndsWith("_plain388", StringComparison.Ordinal))
                && PlainHagi.Traits.Count == 0 && PlainHagi.MaxHp == 62 && PlainHagi.Attack == 16 && PlainHagi.Speed == 7;
            var v388 = VersionsH388();   // 第389期: 第388期の一覧（版の H3 の2台がいる・R5 の2台を外した）
            var verRows = v388.Where(r => r.F.Occupied().Any(o => Vers.Any(v => v.D == o.Def))).Select(r => r.Name).ToList();
            bool rows2 = verRows.SequenceEqual(new[] { "版・H3 ハギ", "版・H3 死神と墓守" })
                && v388.Where(r => verRows.Contains(r.Name)).All(r => r.F.Occupied().Count(o => o.Def == UnitCatalog.HagiH3) == 1)
                && VersionsH387().Length == v388.Length - 2 && !VersionsH387().Any(r => verRows.Contains(r.Name))
                && CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).All(r => !r.F.Occupied().Any(o => Vers.Any(v => v.D == o.Def)));
            var b0 = AtkBoards().Concat(RicaBoards()).Select(b => S307Seats(b.F)).ToArray();
            foreach (var b in AtkBoards()) _ = ColBoard(b, UnitCatalog.HagiH3);
            bool untouched = AtkBoards().Concat(RicaBoards()).Select(b => S307Seats(b.F)).SequenceEqual(b0);
            Expect("替えた台 ／ 素体は器具の中だけ（`compare` ／ 交差帯 ／ `Playtest` ／ `Versions` ／ `All` ／ `Retired` に無い・替えても元の台は変わらない）・版のハギが居る試遊の台は「版・H3 ハギ」「版・H3 死神と墓守」の 2 台だけ（`VersionsH387` は 2 台を外した第387期の一覧）",
                inside && rows2 && untouched, string.Join("、", verRows));
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var cards = new[] { TraitId.HagiReap, TraitId.HagiHalfPursuit, TraitId.HagiSoul, TraitId.HagiHaste, TraitId.HagiScytheSpill };
            var miss = cards.Where(t => !System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第388期")).ToList();
            bool minus = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.HagiScytheSpill\]\s*=\s*\(HcMinusL,");
            Expect("`checkup` の札の分類表に第388期の札 5 枚（刈り取り ／ 追撃 ／ 魂の段 ／ 加速 ／ 巻き込み・巻き込みはマイナス）がある（ソースを読んで確かめる）", miss.Count == 0 && minus, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in AtkBoards().Concat(RicaBoards()))
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.M5 })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            cells++;
                            var f = ColBoard(b, v.D);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（版のハギの台 7 × 版 4 × ボス ／ 近衛 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = Git($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = Git($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第387期の HEAD（{HeadSha}）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    static string S307Seats(Formation f) => Som307Diag.Seats(f);
}
