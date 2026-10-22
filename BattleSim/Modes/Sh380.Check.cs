using BattleCore;
using static Common;
using I = Inv348Diag;
using S377 = Sh377Diag;

// sh380 check —— 第380期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh380Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static Formation Swap(Formation f, UnitDef from, UnitDef to) { var g = f.Clone(); foreach ((int s, UnitDef d) in f.Occupied()) if (ReferenceEquals(d, from)) g[s] = to; return g; }
    static Formation Row(string n) => Pin381(CompareBuilds().First(r => r.Name == n).F);   // 第382期: ヴェルは第381期までの規定に固定

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh380 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            var m = UnitCatalog.Mug; var h = UnitCatalog.MugH379; var a = UnitCatalog.MugSowAll;
            bool cards = m.Traits.SequenceEqual(a.Traits) && m.Traits.SequenceEqual(new[] { TraitId.Splitter, TraitId.MugSowAll })
                && m.Actions is { Count: 1 } && m.Actions[0].Kind == ActionKind.Skill && ReferenceEquals(m.Actions, a.Actions)
                && m.PlusText == a.PlusText && m.MinusText == h.MinusText + UnitCatalog.MugSowMinusTail380 && m.MinusText.EndsWith("ほかの者が呼ぶ仲間の居場所が無くなる")
                && m.Id == h.Id && m.Name == h.Name && m.MaxHp == h.MaxHp && m.Attack == h.Attack && m.Speed == h.Speed
                && UnitCatalog.All.Contains(m) && !UnitCatalog.All.Contains(h) && !UnitCatalog.Retired.Contains(h) && UnitCatalog.All.Count == 52;
            bool old = h.Traits.SequenceEqual(new[] { TraitId.Splitter }) && h.Actions is null;
            bool pins = PinM379(m) == h && PinM379(UnitCatalog.Rica) == UnitCatalog.Rica && PinR378(m) == h && PinR378(UnitCatalog.Rica) == UnitCatalog.RicaH378
                && PinV375(m) == h && PinS371(m) == h && PinGVS371(m) == h
                && Pin379(Row("死の連鎖 (リィカ軸)")).Occupied().Select(o => o.Def).Contains(h) && !Pin379(Row("死の連鎖 (リィカ軸)")).Occupied().Select(o => o.Def).Contains(m);
            bool vers = new[] { UnitCatalog.MugSow1, UnitCatalog.MugSowAll, UnitCatalog.MugSowGrow, UnitCatalog.MugSproutM, UnitCatalog.MugSproutMU }
                .All(v => v.Traits[0] == TraitId.Splitter && v.Traits.Skip(1).All(t => t != TraitId.Splitter) && v.MinusText == h.MinusText && v.MaxHp == h.MaxHp && !UnitCatalog.All.Contains(v));
            Expect("段0: 規定のムグ ＝ 第379期の撒き全（札・手番 `[Skill]`・プラスの文面）・マイナスの尾に「撒いた胞子が空いた場所を埋めるので…」・旧 `MugH379`（分裂だけ・`All` の外）・固定の口（`PinM379` ／ `Pin379` ／ `PinR378` の後ろ・古い口の終端 `PinV375` ／ `PinS371` ／ `PinGVS371` も旧へ）・第377〜379期の版のムグは旧から作ったまま", cards && old && pins && vers, m.MinusText);
        }
        {
            long cells = 0, diff = 0;
            var boards = new[] { Row("死の連鎖 (リィカ軸)"), Row("継ぎ当て×死の連鎖"), Pin381(Presets.DeathSom379()), Row("鱗 (ウロ×死軸)") };
            foreach (var f in boards)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(f, w, sd).R, I.Raw(Swap(f, UnitCatalog.Mug, UnitCatalog.MugSowAll), w, sd).R)) diff++;
                    }
            Expect("規定のムグは第379期の `MugSowAll` と同じ動き（ムグのいる4台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..9 で勝敗 ／ T ／ 生存数が同じ）", diff == 0 && cells == 160, $"{cells} 戦・差 {diff}");
        }

        // ---- 探索の母集団 ----
        {
            var hp = Healers.Select(d => d.Name).ToArray();
            bool heal = Healers.Any(d => d.Id == "vel") && new[] { "shio", "sasa", "hibi", "beni", "hisa", "som" }.All(id => Healers.Any(d => d.Id == id));
            var main = Pairs(reference: false); var refr = Pairs(reference: true);
            bool noTL = main.Concat(refr).All(p => p.A.Id is not ("tsugi" or "lili") && p.B.Id is not ("tsugi" or "lili"))
                && Cands.All(d => AllH384().Contains(d) && d.Id is not ("rica" or "vel" or "mug"));   // 第385期: 第384期までの `All`
            bool mainH = main.All(p => !IsHealer(p.A) && !IsHealer(p.B));
            bool refH = refr.All(p => (IsHealer(p.A) ? 1 : 0) + (IsHealer(p.B) ? 1 : 0) == 1);
            bool perms = Perms.Length == 120 && Perms.Select(p => string.Join(",", p)).Distinct().Count() == 120;
            var samples = main.Take(30).Concat(refr.Take(10)).SelectMany(p => Perms.Select(pm => Board(Fixed.Concat(new[] { p.A, p.B }).ToArray(), pm))).ToList();
            bool fixedIn = samples.All(f => f.Occupied().Count() == 5 && Fixed.All(x => f.Occupied().Any(o => ReferenceEquals(o.Def, x))) && Enumerable.Range(0, 5).All(s => f[s] is not null));
            bool mainOne = main.Take(30).SelectMany(p => Perms.Take(3).Select(pm => Board(Fixed.Concat(new[] { p.A, p.B }).ToArray(), pm))).All(f => f.Occupied().Count(o => IsHealer(o.Def)) == 1);
            Expect("探索: 2枠の候補にツギ ／ リリ ／ 固定の3枚が無い・本体の台のヒーラーはヴェルの1枚だけ（2枠にヒーラー 0）・参考の台は2枠の1枚だけがヒーラー・席の並び 120 ／ 5 席が全部埋まる・リィカ ／ ヴェル ／ ムグが全部の台にいる",
                heal && noTL && mainH && refH && perms && fixedIn && mainOne, $"ヒーラー {string.Join("・", hp)}・候補 {Cands.Length} 枚・本体 {main.Length} 組 ／ 参考 {refr.Length} 組");
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            var found = vr.Where(n => n.Contains("探索") && !n.StartsWith("版・")).ToArray();   // 第381期: 「版・S2 ／ S10 死 探索1位」（ヴェルの版の台）は数えない
            var top = Pin381(Presets.Versions.First(r => r.Name == "試遊・死 探索1位").F);   // 第382期: ヴェルは第381期までの規定に固定
            bool topOk = top is not null && Fixed.All(x => top.Occupied().Any(o => ReferenceEquals(o.Def, x))) && top.Occupied().Count() == 5
                && top.Occupied().Count(o => IsHealer(o.Def)) == 1 && top.Occupied().All(o => UnitCatalog.All.Contains(o.Def) || ReferenceEquals(o.Def, UnitCatalog.VelH381))   /* 第382期: ヴェルは旧（`All` の外）に固定 */
                && Som307Diag.Seats(top) == "リィカ・ヒヨ・ボルグ・ヴェル・ムグ";
            // 第380期の探索で本体の上位 10 にソムは入らない（ソムはヒーラーで本体の2枠に入らない・参考の上位 5 にも入らない）——「試遊・死 探索ソム」は足さない
            bool only = found.SequenceEqual(new[] { "試遊・死 探索1位" }) && !vr.Any(n => n.StartsWith("版・撒き") || n.StartsWith("版・育つ"));
            bool keep = vr.Contains("試遊・死の連鎖") && vr.Contains("試遊・死 ソム")
                && Pin381(Presets.Versions.First(r => r.Name == "試遊・死の連鎖").F).Occupied().Select(o => o.Def).SequenceEqual(Row("死の連鎖 (リィカ軸)").Occupied().Select(o => o.Def));
            Expect("試遊の台: 探索の台は「試遊・死 探索1位」だけ（ソムは上位に入らず「探索ソム」は足さない）・規定の駒・リィカ ／ ヴェル ／ ムグ ＋ ヒーラー1枚・「版・撒き全 ／ 育つ」は外した・「試遊・死の連鎖」「試遊・死 ソム」は規定の駒のまま", topOk && only && keep, string.Join("、", vr.Skip(11)));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var f in Presets.Versions.Where(r => r.Name.Contains("死")).Select(r => r.F))
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（死の試遊の台 × ボス ／ 近衛 ／ 大隊 ／ 第5波・seed 0..4）", diff == 0 && det == 0 && cells > 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool sow = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.MugSowAll\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第380期から規定（ムグ）");
            bool all = UnitCatalog.Mug.Traits.All(t => src.Contains($"[TraitId.{t}]", StringComparison.Ordinal));
            Expect("`checkup` の札の分類表: 規定になったムグの札（撒き散らす）が「第380期から規定（ムグ）」・規定のムグの札が全部表にある（ソースを読んで確かめる）", sow && all);
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 252a8cf8:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第379期の HEAD（252a8cf8）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
