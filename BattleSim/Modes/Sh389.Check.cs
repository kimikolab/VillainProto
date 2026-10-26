using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S388 = Sh388Diag;

// sh389 check —— 第389期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh389Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    const string HeadSha = "024184c3";   // 第388期の HEAD（この期の作業を始めたコミット）
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate389", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeDef(string id) => new() { Id = id, Name = "的" + id[^1], MaxHp = 1000, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;

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
    static UnitState Mt(List<UnitState> p) => p.First(u => u.Def.Id == "mate389");

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
        Console.WriteLine($"# {Tag} sh389 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef fa = FoeDef("foe389a"), fb = FoeDef("foe389b"), fc = FoeDef("foe389c"), fd = FoeDef("foe389d");
        UnitDef r5 = UnitCatalog.HagiR5, r4 = UnitCatalog.HagiR4, r5n = UnitCatalog.HagiR5n, r5m = UnitCatalog.HagiR5m;

        // ---- 版の札と文面 ----
        {
            var h2 = UnitCatalog.HagiH2;
            bool body = new[] { r5, r4, r5n }.All(d => d.Id == "hagi" && SameUnitName(d.Name, UnitCatalog.Hagi.Name) && d.MaxHp == 62 && d.Attack == 16 && d.Speed == 7 && d.Pattern == AttackPattern.Single && d.Actions is null
                && d.Traits.Take(2).SequenceEqual(new[] { TraitId.HagiReap, TraitId.HagiHalfPursuit }) && d.Traits.Contains(TraitId.HagiDepthSoul) && d.Traits[^1] == TraitId.HagiSpillFixed
                && !d.Traits.Contains(TraitId.HagiSoul) && !d.Traits.Contains(TraitId.HagiScytheSpill) && !UnitCatalog.Everyone.Contains(d));
            bool cards = r5.Traits.Contains(TraitId.HagiSoulMoves) && !r5.Traits.Contains(TraitId.HagiDepthShallow) && r4.Traits.Contains(TraitId.HagiDepthShallow) && r4.Traits.Contains(TraitId.HagiSoulMoves)
                && !r5n.Traits.Contains(TraitId.HagiSoulMoves) && !r5n.Traits.Contains(TraitId.HagiDepthShallow);
            bool text = r5.PlusText.Contains("最大 5 割", StringComparison.Ordinal) && r4.PlusText.Contains("最大 4 割", StringComparison.Ordinal) && r5.PlusText.EndsWith("ついには一振りで列ごと刈る", StringComparison.Ordinal)
                && !r5n.PlusText.Contains("列ごと", StringComparison.Ordinal) && r5.PlusText.StartsWith("大鎌で、弱った敵を刈り取る（HP が線を切った敵は一振りで倒す）", StringComparison.Ordinal)
                && r5.MinusText == "大鎌の振りは、隣にいる仲間も巻き込む。刈る相手がいなければ、魂は薄れていく" && r4.MinusText == r5.MinusText && r5n.MinusText == r5.MinusText;
            bool mk = r5m.Traits.SequenceEqual(new[] { TraitId.HagiReap, TraitId.HagiHalfPursuit, TraitId.HagiDepthSoul, TraitId.HagiSoulMoves, TraitId.HagiReapMark, TraitId.HagiSpillFixed })
                && r5m.PlusText.StartsWith(r5.PlusText, StringComparison.Ordinal) && r5m.PlusText.EndsWith("（1つ +5%・合わせて最大 6 割）", StringComparison.Ordinal) && r5m.MinusText == r5.MinusText
                && r5m.MaxHp == 62 && r5m.Attack == 16 && r5m.Speed == 7 && !UnitCatalog.Everyone.Contains(r5m) && !r5.Traits.Contains(TraitId.HagiReapMark);
            bool def = UnitCatalog.HagiH389.Traits.SequenceEqual(new[] { TraitId.Pursuer }) && UnitCatalog.HagiH389.Pattern == AttackPattern.Sweep && UnitCatalog.HagiH389.Id == "hagi";   // 第390期: 第389期の規定（追い打ち）に固定
            Expect("版の札と文面: 体は今のハギ（62 ／ 16 ／ 7・単体）・H2 の札（刈り取り ／ 追撃）＋ 魂の深さ ＋ 固定の巻き込み・R5 ＋ 段の動き ／ R4 ＋ 浅い線 ＋ 段の動き ／ R5n は段の動き無し ／ R5m ＝ R5 ＋ 刈りの印・`All` ／ `Retired` の外・規定のハギは今どおり",
                body && cards && text && mk && def, string.Join(" ／ ", new[] { ("R5", r5), ("R4", r4), ("R5n", r5n), ("R5m", r5m) }.Select(v => $"{v.Item1} {string.Join("・", v.Item2.Traits)}")));
        }

        // ---- 魂 ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c, p, e) = Bench(new[] { (3, r5), (2, Mate) }, new[] { (0, fa, 900), (1, fb, 950), (2, fc, 1000) });
            var h = Hg(p); var m = Mt(p); var a = e.First(u => u.Slot == 0); var b = e.First(u => u.Slot == 1); var cc = e.First(u => u.Slot == 2);
            c.ApplyDamage(a, 200, m);                       // 900 → 700: 8割を割った → +1
            bool g1 = HagiDepthTrait.Soul(h) == 1;
            c.Heal(a, 200); c.ApplyDamage(a, 200, m);       // 同じターンにもう一度 8割を割っても数えない
            bool g2 = HagiDepthTrait.Soul(h) == 1;
            c.ApplyDamage(a, 550, m);                       // 700 → 150: 5割と2割を一度に割った → +2（追撃も入る・追撃は 5割の判定のまま）
            bool g3 = HagiDepthTrait.Soul(h) == 3 && Tl(c, h).R389Cross80 == 1 && Tl(c, h).R389Cross50 == 1 && Tl(c, h).R389Cross20 == 1;
            c.ApplyDamage(b, 9999, m);                      // 950 → 倒れた: 線は数えず敵の死 +2
            bool g4 = HagiDepthTrait.Soul(h) == 5 && Tl(c, h).R389SoulDeath == 2 && Tl(c, h).R389Cross80 == 1;
            ok &= g1 && g2 && g3 && g4; notes.Add($"8割 +1 ／ 同じターン2度目は 0 ／ 5割 ＋ 2割を一度に +2 ／ 倒れた一撃は死の +2 だけ{(g1 && g2 && g3 && g4 ? "" : $" × 魂 {HagiDepthTrait.Soul(h)}")}");
            TurnField.SetValue(c, 2); c.Heal(a, 900); int hb = a.Hp; c.ApplyDamage(a, hb - 700, m);   // 次のターンならまた 8割で +1
            bool g5 = HagiDepthTrait.Soul(h) == 6;
            HagiDepthTrait.Gain(c, h, 10, 0);
            bool g6 = HagiDepthTrait.Soul(h) == 9;
            bool g7 = h.CurrentAttack == 16 && h.AtkBonus == 0;
            var tr = h.Traits.First(x => x.Id == TraitId.HagiDepthSoul);
            TurnField.SetValue(c, 3); tr.OnTurnStart(c, h); bool g8 = HagiDepthTrait.Soul(h) == 9;   // 前のターン（2）に魂が動いた
            TurnField.SetValue(c, 5); tr.OnTurnStart(c, h); bool g9 = HagiDepthTrait.Soul(h) == 8;   // 途切れた → 1 つ薄れる
            tr.OnCarryOver(h); bool g10 = HagiDepthTrait.Soul(h) == 0;
            ok &= g5 && g6 && g7 && g8 && g9 && g10;
            notes.Add($"次のターンにまた +1 ／ 上限 9 ／ 攻撃力は 16 のまま ／ 繋がれば保つ ／ 途切れたら薄れる ／ 会戦で 0{(g5 && g6 && g7 && g8 && g9 && g10 ? "" : $" × {g5}{g6}{g7}{g8}{g9}{g10}")}");
            _ = cc;
            Expect("魂: 敵の死 +2 ／ 8割 ／ 5割 ／ 2割を上から割るたび +1（1つの敵の1つの線につき1ターン1回・倒れた一撃は死として数える）／ 上限 9 ／ 薄れる ／ 攻撃力が上がらない", ok, string.Join("・", notes));
        }

        // ---- 線 ----
        {
            var notes = new List<string>(); bool ok = true;
            int[] want5 = { 200, 300, 400, 500 }, want4 = { 200, 250, 300, 400 };
            int[] souls = { 0, 3, 6, 9 };
            for (int i = 0; i < 4; i++)
            {
                foreach (var (d, want) in new[] { (r5, want5[i]), (r4, want4[i]), (r5n, want5[i]) })
                {
                    var (c, p, _) = Bench(new[] { (2, d) }, new[] { (0, fa, 1000) });
                    var h = Hg(p); if (souls[i] > 0) HagiDepthTrait.Gain(c, h, souls[i], 0);
                    ok &= HagiReaperTrait.ReapLinePermil(h) == want;
                }
            }
            notes.Add("R5 ／ R5n 2 ／ 3 ／ 4 ／ 5 割・R4 2 ／ 2.5 ／ 3 ／ 4 割");
            // 線で刈る（魂 9 の R5: 50% は刈る・51% は刈らない）・R4 の魂 3（25%）
            {
                var (c, p, e) = Bench(new[] { (2, r5n) }, new[] { (0, fa, 500), (1, fb, 1000) });
                var h = Hg(p); HagiDepthTrait.Gain(c, h, 9, 0); c.TakeTurn(h);
                var (c2, p2, e2) = Bench(new[] { (2, r5n) }, new[] { (0, fa, 510), (1, fb, 1000) });
                var h2 = Hg(p2); HagiDepthTrait.Gain(c2, h2, 9, 0); c2.TakeTurn(h2);
                var (c3, p3, e3) = Bench(new[] { (2, r4) }, new[] { (0, fa, 250), (1, fb, 1000) });
                var h3 = Hg(p3); HagiDepthTrait.Gain(c3, h3, 3, 0); c3.TakeTurn(h3);
                bool g = !e[0].IsAlive && Tl(c, h).R388Reaps == 1 && e2[0].IsAlive && e2[0].Hp == 510 - 16 && Tl(c2, h2).R388Reaps == 0 && !e3[0].IsAlive && Tl(c3, h3).R389LastReapLine == 250;
                ok &= g; notes.Add($"魂 9 の線 5 割で 500 ／ 1000 を刈り 510 は刈らない・R4 魂 3 で 250 を刈る{(g ? "" : " ×")}");
            }
            Expect("線: 魂 0 ／ 3 ／ 6 ／ 9 で R5 は 2 ／ 3 ／ 4 ／ 5 割・R4 は 2 ／ 2.5 ／ 3 ／ 4 割（その時点の最大HPに対する割合）", ok, string.Join("・", notes));
        }

        // ---- 動き ----
        {
            var notes = new List<string>(); bool ok = true;
            // 魂 3: 斬撃が薙ぎ（R5）・R5n は単体・刈り取りの一撃は単体
            {
                var (c, p, e) = Bench(new[] { (2, r5) }, new[] { (0, fa, 1000), (1, fb, 1000) });
                var h = Hg(p); HagiDepthTrait.Gain(c, h, 3, 0);
                var (c2, p2, _) = Bench(new[] { (2, r5n) }, new[] { (0, fa, 1000) });
                var h2 = Hg(p2); HagiDepthTrait.Gain(c2, h2, 3, 0);
                bool sweep = h.CurrentPattern == AttackPattern.Sweep && h2.CurrentPattern == AttackPattern.Single;
                c.TakeTurn(h);   // 薙ぎ: 的 16 ＋ 巻き込み 60% ＝ 9
                bool hit = e.Count(u => u.Hp < 1000) == 2 && Tl(c, h).R389Sweeps == 1;
                var (c3, p3, e3) = Bench(new[] { (2, r5) }, new[] { (0, fa, 200), (1, fb, 1000) });
                var h3 = Hg(p3); HagiDepthTrait.Gain(c3, h3, 3, 0); c3.TakeTurn(h3);   // 刈り取り（単体）→ 再行動は薙ぎ
                bool reapSingle = !e3[0].IsAlive && Tl(c3, h3).R388Reaps == 1 && Tl(c3, h3).R389ColumnReaps == 0;
                ok &= sweep && hit && reapSingle; notes.Add($"魂 3 で斬撃が薙ぎ（R5n は単体）・刈り取りは単体{(sweep && hit && reapSingle ? "" : " ×")}");
            }
            // 魂 6: 追撃 2 連（R5）／ 1 回（R5n）
            {
                var (c, p, e) = Bench(new[] { (3, r5), (2, Mate) }, new[] { (0, fa, 600) });
                var h = Hg(p); HagiDepthTrait.Gain(c, h, 6, 0); c.ApplyDamage(e[0], 150, Mt(p));
                var (c2, p2, e2) = Bench(new[] { (3, r5n), (2, Mate) }, new[] { (0, fa, 600) });
                var h2 = Hg(p2); HagiDepthTrait.Gain(c2, h2, 6, 0); c2.ApplyDamage(e2[0], 150, Mt(p2));
                bool g = Tl(c, h).R388Pursuits == 2 && Tl(c2, h2).R388Pursuits == 1 && HagiReaperTrait.PursuitHits(h) == 2 && HagiReaperTrait.PursuitHits(h2) == 1;
                ok &= g; notes.Add($"魂 6 で追撃 2 連（R5n は 1 回）{(g ? "" : $" × {Tl(c, h).R388Pursuits} {Tl(c2, h2).R388Pursuits}")}");
            }
            // 魂 9: 刈り取りが的と同じ列 ＋ 中列の線以下の敵をまとめて刈る（R5）／ R5n は1体ずつ
            {
                (int, UnitDef, int)[] foes = { (0, fa, 300), (1, fb, 450), (2, fc, 400), (3, fd, 600) };
                var (c, p, e) = Bench(new[] { (2, r5) }, foes);
                var h = Hg(p); HagiDepthTrait.Gain(c, h, 9, 0); c.TakeTurn(h);
                var (c2, p2, e2) = Bench(new[] { (2, r5n) }, foes);
                var h2 = Hg(p2); HagiDepthTrait.Gain(c2, h2, 9, 0); c2.TakeTurn(h2);
                var t = Tl(c, h); var t2 = Tl(c2, h2);
                bool g = !e.First(u => u.Slot == 0).IsAlive && !e.First(u => u.Slot == 1).IsAlive && !e.First(u => u.Slot == 2).IsAlive && e.First(u => u.Slot == 3).IsAlive
                         && t.R389ColumnReaps == 1 && t.R389ColumnUnits == 2 && t.R389ColumnHp == 850 && t.R388Reaps == 1
                         && t2.R389ColumnReaps == 0 && t2.R388Reaps == 3 && t2.R388Encores == 2;
                ok &= g; notes.Add($"魂 9 の R5 は一振りで 3 体（列 ＋ 中列・後列の 60% は残る）・R5n は1体ずつ 3 振り{(g ? "" : $" × col {t.R389ColumnReaps}/{t.R389ColumnUnits} reaps {t.R388Reaps} ／ R5n reaps {t2.R388Reaps} enc {t2.R388Encores}")}");
            }
            Expect("動き: 魂 3 で斬撃が薙ぎ（刈り取りは単体）／ 魂 6 で追撃 2 連 ／ 魂 9 で刈り取りが列の線以下の敵をまとめて刈る ／ R5n は変化なし", ok, string.Join("・", notes));
        }

        // ---- 刈りの印（R5m）----
        {
            var notes = new List<string>(); bool ok = true;
            // 斬撃（薙ぎ）が当たった敵に印・R5 には付かない
            var (c, p, e) = Bench(new[] { (2, r5m) }, new[] { (0, fa, 1000), (1, fb, 1000) });
            var h = Hg(p); HagiDepthTrait.Gain(c, h, 3, 0); c.TakeTurn(h);
            bool g1 = e.All(u => u.RawCounter(HagiDepthTrait.MarkKey) == 1) && Tl(c, h).R389MarksPlaced == 2;
            var (c2, p2, e2) = Bench(new[] { (2, r5) }, new[] { (0, fa, 1000) });
            c2.TakeTurn(Hg(p2)); bool g2 = e2[0].RawCounter(HagiDepthTrait.MarkKey) == 0;
            // 追撃も印を付ける・上限 5
            var (c3, p3, e3) = Bench(new[] { (3, r5m), (2, Mate) }, new[] { (0, fa, 1000) });
            var h3 = Hg(p3); var a3 = e3[0];
            for (int i = 0; i < 7; i++) { TurnField.SetValue(c3, i + 1); c3.TakeTurn(h3); }
            bool g3 = a3.RawCounter(HagiDepthTrait.MarkKey) == 5;
            var (c4, p4, e4) = Bench(new[] { (3, r5m), (2, Mate) }, new[] { (0, fa, 600) });
            c4.ApplyDamage(e4[0], 150, Mt(p4));
            bool g4 = Tl(c4, Hg(p4)).R388Pursuits == 1 && e4[0].RawCounter(HagiDepthTrait.MarkKey) == 1;
            ok &= g1 && g2 && g3 && g4; notes.Add($"斬撃 ／ 薙ぎ ／ 追撃が当たった敵に +1・R5 には付かない・上限 5{(g1 && g2 && g3 && g4 ? "" : $" × {g1}{g2}{g3}{g4}")}");
            // 線: 印1つ +5%・合わせて最大 6 割・印で深くなった線で刈った回数
            var (c5, p5, e5) = Bench(new[] { (2, r5m) }, new[] { (0, fa, 290), (1, fb, 1000) });
            var h5 = Hg(p5); e5[0].SetCounter(HagiDepthTrait.MarkKey, 2); c5.TakeTurn(h5);   // 魂 0（線 2 割）＋ 印 2 ＝ 3 割 → 290 を刈る
            bool g5 = !e5[0].IsAlive && Tl(c5, h5).R389MarkReaps == 1 && Tl(c5, h5).R389LastReapLine == 300;
            var (c6, p6, e6) = Bench(new[] { (2, r5m) }, new[] { (0, fa, 610), (1, fb, 1000) });
            var h6 = Hg(p6); HagiDepthTrait.Gain(c6, h6, 9, 0); e6[0].SetCounter(HagiDepthTrait.MarkKey, 5); c6.TakeTurn(h6);   // 5 割 ＋ 25% → 上限 6 割 → 610 は刈らない
            bool g6 = e6[0].IsAlive && Tl(c6, h6).R388Reaps == 0;
            var (c7, p7, e7) = Bench(new[] { (2, r5) }, new[] { (0, fa, 290), (1, fb, 1000) });
            var h7 = Hg(p7); e7[0].SetCounter(HagiDepthTrait.MarkKey, 2); c7.TakeTurn(h7);   // 札の無い R5 は印を読まない
            bool g7 = e7[0].IsAlive && Tl(c7, h7).R388Reaps == 0;
            ok &= g5 && g6 && g7; notes.Add($"印 2 で線 2 → 3 割（印で刈った 1）・魂 9 ＋ 印 5 でも 6 割まで・R5 は印を読まない{(g5 && g6 && g7 ? "" : $" × {g5}{g6}{g7}")}");
            Expect("刈りの印（R5m）: 斬撃 ／ 薙ぎ ／ 追撃が当たった敵に印 +1（上限 5）／ 印1つで線 +5%（合わせて最大 6 割）／ 印で深くなった線で刈った回数を数える", ok, string.Join("・", notes));
        }

        // ---- 巻き込み ----
        {
            var (c, p, e) = Bench(new[] { (2, r5), (0, Mate) }, new[] { (0, fa, 1000), (1, fb, 1000) });
            var h = Hg(p); HagiDepthTrait.Gain(c, h, 9, 0); c.TakeTurn(h);
            var (c2, p2, e2) = Bench(new[] { (3, r5n), (2, Mate) }, new[] { (0, fa, 600) });
            c2.ApplyDamage(e2[0], 150, Mt(p2));   // 追撃も巻き込む
            bool g = Mt(p).Hp == 48 && Tl(c, h).R388SpillHits == 1 && Mt(p2).Hp == 48 && h.CurrentAttack == 16;
            Expect("巻き込み: 攻撃のたび隣の味方に 2（斬撃 ／ 追撃・魂 9 でも 2）", g, $"斬撃 {50 - Mt(p).Hp} ／ 追撃 {50 - Mt(p2).Hp}");
        }

        // ---- 第388期の H2 の列が第388期と同じ動き ----
        {
            var bs = S388.RicaBoards();
            var got = new List<string>(); bool ok = true;
            var want = new (double A4, double Str)[] { (97.5, 50.0), (100.0, 73.5) };   // 第388期の報告（`rica.md` §1・H2）
            for (int i = 0; i < bs.Length; i++)
            {
                var g = M8(ColBoard(bs[i], UnitCatalog.HagiH2));
                ok &= Math.Abs(A4(g) - want[i].A4) < 1e-9 && Math.Abs(g[WStrong].Win - want[i].Str) < 1e-9;
                got.Add($"{bs[i].Name} {F1(A4(g))} ／ {F1(g[WStrong].Win)}");
            }
            Expect("第388期の H2 の列が第388期と同じ動き（リィカと並べる台 2 × 8 物差し × seed 0..99 の 4 波の平均 ／ 強い条件が第388期の報告の値と一致）", ok, string.Join("・", got));
        }

        // ---- 規定のハギ ／ ハギのいない compare の行 ----
        {
            string head = Git($"{HeadSha}:docs/units.md"), now = Git("0d48cccb:docs/units.md");   // 第390期: 規定のハギを替えたので、第389期の HEAD の表と比べる
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
            Expect($"規定のハギは今どおり（`docs/units.md` の行が第388期の HEAD（{HeadSha}）と同じ）・規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致",
                Row(head) != "" && Row(head) == Row(now) && diff == 0 && miss == 0, $"{rows.Length} 行（ハギ {hagiRows}）・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 替えた台 ／ 素体は器具の中だけ・試遊の台 ----
        {
            var vers = new[] { r5, r4, r5n, r5m, UnitCatalog.HagiH1, UnitCatalog.HagiH2, UnitCatalog.HagiH3, UnitCatalog.HagiH3s };
            var verRows = VersionsH389().Where(   /* 第390期: 第389期の一覧 */ r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool rows2 = verRows.SequenceEqual(new[] { "版・R5 ハギ", "版・R5 死神と墓守" })
                && VersionsH389().Where(r => verRows.Contains(r.Name)).All(r => r.F.Occupied().Count(o => o.Def == r5 || o.Def == r5m) == 1)
                && VersionsH389().First(r => r.Name == "版・R5 死神と墓守").F[0] is { } z && (z == r5 || z == r5m)
                && VersionsH388().Count(r => r.Name is "版・H3 ハギ" or "版・H3 死神と墓守") == 2 && !VersionsH388().Any(r => verRows.Contains(r.Name))
                && VersionsH387().Length == VersionsH388().Length - 2
                && CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).All(r => !r.F.Occupied().Any(o => vers.Contains(o.Def)));
            bool inside = !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(VersionsH389()).SelectMany(r => r.F.Occupied()).Any(o => o.Def.Id.EndsWith("_plain388", StringComparison.Ordinal));
            var b0 = S388.RicaBoards().Select(b => Som307Diag.Seats(b.F)).ToArray();
            foreach (var b in S388.RicaBoards()) _ = ColBoard(b, r5);
            bool untouched = S388.RicaBoards().Select(b => Som307Diag.Seats(b.F)).SequenceEqual(b0);
            Expect("替えた台 ／ 素体は器具の中だけ・版のハギが居る試遊の台は「版・R5 ハギ」「版・R5 死神と墓守」の 2 台だけ（第388期の「版・H3」の2台は外して `VersionsH388` が足し戻す・`VersionsH387` はその2台も外した一覧）",
                rows2 && inside && untouched, string.Join("、", verRows));
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var cards = new[] { TraitId.HagiDepthSoul, TraitId.HagiDepthShallow, TraitId.HagiSoulMoves, TraitId.HagiSpillFixed };
            var miss = cards.Where(t => !System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第389期")).ToList();
            bool minus = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.HagiSpillFixed\]\s*=\s*\(HcMinusL,");
            Expect("`checkup` の札の分類表に第389期の札 4 枚（魂の深さ（線を割る）／ 浅い線 ／ 段の動き（列をまとめて刈る）／ 固定の巻き込み・巻き込みはマイナス）がある（ソースを読んで確かめる）", miss.Count == 0 && minus, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in S388.RicaBoards())
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
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（リィカと並べる台 2 × 版 5 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = Git($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = Git($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第388期の HEAD（{HeadSha}）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
