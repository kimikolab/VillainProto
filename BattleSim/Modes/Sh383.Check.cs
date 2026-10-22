using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S382 = Sh382Diag;

// sh383 check —— 第383期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh383Diag
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
    static readonly UnitDef MateHi = new() { Id = "mateHi383", Name = "仲間強", MaxHp = 60, Attack = 9, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef FoeHi = new() { Id = "foeHi383", Name = "的強", MaxHp = 999, Attack = 7, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef FoeWeak = new() { Id = "foeWeak383", Name = "的脆", MaxHp = 5, Attack = 7, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef FoeLo = new() { Id = "foeLo383", Name = "的弱", MaxHp = 999, Attack = 2, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Bd((int Slot, UnitDef D)[] me, (int Slot, UnitDef D)[] foe)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); foreach (var (s, d) in me) f[s] = d;
        var g = new Formation(); foreach (var (s, d) in foe) g[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        return (ctx, p, e);
    }
    static UnitState At(List<UnitState> p, int slot) => p.First(u => u.Slot == slot);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh383 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef d2 = UnitCatalog.Bard383;

        {
            bool def = d2.Traits.SequenceEqual(new[] { TraitId.Dirge, TraitId.DirgeEchoAlways }) && d2.Id == UnitCatalog.Bard382.Id && d2.MaxHp == 40 && d2.Attack == 3 && d2.Speed == 9 && !d2.Advances
                && d2.PlusText == UnitCatalog.Bard382.PlusText && !UnitCatalog.Everyone.Contains(d2)
                && UnitCatalog.Bard382n.Traits.SequenceEqual(new[] { TraitId.Dirge }) && UnitCatalog.Bard382.Traits.SequenceEqual(new[] { TraitId.Dirge, TraitId.DirgeEcho });
            Expect("D2: 哀歌 ＋ 哀歌は敵の耳にも届く（体と文面のプラスは D1 と同じ・`All` の外）・D1 ／ D1n の札は第382期のまま", def, $"「{d2.MinusText}」");
        }
        {
            // 奏でた手番: 味方の後に、攻撃力の最も高い敵が1回動く（敵は1体も倒れていない）
            var (ctx, p, e) = Bd(new[] { (0, d2), (1, MateHi) }, new[] { (0, FoeLo), (1, FoeHi) });
            var bard = At(p, 0); var sp = ctx.Summon(UnitCatalog.Spore, BattleContext.PlayerTeam)!;
            ctx.ApplyDamage(sp, 999, At(e, 1));
            int ev0 = ctx.Events.Count;
            ctx.TakeTurn(bard);
            var dmg = ctx.Events.Skip(ev0).Where(x => x.Kind == BattleEventKind.Damage && x.ActorId is int).Select(x => x.ActorId!.Value).ToList();
            int iMate = dmg.IndexOf(At(p, 1).InstanceId), iFoe = dmg.IndexOf(At(e, 1).InstanceId);
            bool sang = Tal(ctx, At(e, 1)).R382EchoTurns == 1 && Tal(ctx, At(e, 0)).R382EchoTurns == 0 && iMate >= 0 && iFoe > iMate && Tal(ctx, bard).R382Echoes == 1;
            // 殴った手番（新たに味方は倒れていない・敵を倒しても）: 敵は動かない
            ctx.ApplyDamage(At(e, 0), 9999, At(p, 1));
            ctx.TakeTurn(bard);
            bool hit = Tal(ctx, At(e, 1)).R382EchoTurns == 1 && Tal(ctx, bard).R382Echoes == 1 && Tal(ctx, bard).R382NoDeath == 1;
            Expect("D2: 奏でた手番では必ず、味方の再行動の後に攻撃力の最も高い敵が1回動く（敵が倒れたかは見ない）／ 殴った手番では動かない（敵が倒れていても）", sang && hit, $"一撃の順 味方 {iMate} ／ 敵 {iFoe}");
        }
        {
            // 控えた敵が先に倒れていれば動かない（味方の再行動がその敵を倒す）
            var (ctx, p, e) = Bd(new[] { (0, d2), (1, MateHi) }, new[] { (0, FoeWeak), (3, FoeLo) });
            var bard = At(p, 0); var sp = ctx.Summon(UnitCatalog.Spore, BattleContext.PlayerTeam)!;
            ctx.ApplyDamage(sp, 999, At(e, 0));
            ctx.TakeTurn(bard);
            bool ok = !At(e, 0).IsAlive && Tal(ctx, At(e, 0)).R382EchoTurns == 0 && Tal(ctx, At(e, 3)).R382EchoTurns == 0 && Tal(ctx, bard).R382Echoes == 1 && Tal(ctx, bard).R382Skipped == 1;
            Expect("D2: 控えた敵が味方の再行動で倒れていれば動かない（ほかの敵に替えない）", ok, $"的脆 {(At(e, 0).IsAlive ? "生" : "倒")}・飛ばした {Tal(ctx, bard).R382Skipped}");
        }
        {
            // D1n は第382期と同じ動き（第382期の報告 §3-1 の D1n の列: ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件・seed 0..99）
            var exp = new (string Name, Formation F, double[] Win)[]
            {
                ("スィド", S382.Board(new[] { UnitCatalog.Mug, UnitCatalog.Sid, UnitCatalog.Vel, UnitCatalog.Rica, UnitCatalog.Bard382n }, new[] { 0, 1, 2, 3, 4 }), new[] { 100.0, 100, 100, 100, 98.2 }),
                ("ゴルム", S382.Board(new[] { UnitCatalog.Mug, UnitCatalog.Golm, UnitCatalog.Vel, UnitCatalog.Rica, UnitCatalog.Bard382n }, new[] { 0, 1, 2, 3, 4 }), new[] { 100.0, 100, 100, 100, 79.5 }),
                ("ボルグ", S382.Board(new[] { UnitCatalog.Vel, UnitCatalog.Mug, UnitCatalog.Bard382n, UnitCatalog.Borg, UnitCatalog.Rica }, new[] { 0, 1, 2, 3, 4 }), new[] { 96.0, 100, 100, 100, 65.8 }),
                ("死の連鎖", S382.ChainWith(UnitCatalog.Bard382n), new[] { 100.0, 100, 100, 82, 53.2 }),
            };
            var ws = new[] { new[] { I.Boss }, new[] { I.Guard }, new[] { I.Bat }, new[] { I.M5 }, I.StrongWaves };
            int bad = 0; var notes = new List<string>();
            foreach (var (name, f, win) in exp)
            {
                var got = new double[ws.Length];
                Parallel.For(0, ws.Length, wi => { int w = 0, n = 0; foreach (var wave in ws[wi]) for (int s = 0; s < 100; s++) { n++; if (I.Raw(f, wave, s, census: false).R.PlayerWon) w++; } got[wi] = Math.Round(100.0 * w / n, 1); });
                for (int wi = 0; wi < ws.Length; wi++) if (Math.Abs(got[wi] - win[wi]) > 0.05) { bad++; notes.Add($"{name} {wi}: {win[wi]} → {got[wi]}"); }
            }
            Expect("D1n は第382期と同じ動き（`sh382 cmp` の D1n の列 4 台 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件の勝率が一致）", bad == 0, bad == 0 ? "20 セル・ずれ 0" : string.Join("・", notes));
        }
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            var bard = Presets.Versions.FirstOrDefault(r => r.Name == "版・楽士 死").F;
            bool ok = bard is not null && bard.Occupied().Select(o => o.Def.Id).SequenceEqual(Presets.DeathBard382(d2).Occupied().Select(o => o.Def.Id))   /* 第384期: 楽士は G1 に替わった——並び（Id）だけを見る */
                && vr.Count(n => n.StartsWith("版・")) >= 1 && !Presets.Versions.Any(r => r.F.Occupied().Any(o => o.Def.Id == S382.Plain.Id || ReferenceEquals(o.Def, UnitCatalog.Bard382n)));
            Expect("試遊の台: 「版・楽士 死」は D2（並びは第382期のまま）・D1n の台は足さない・素体の替え ／ 並びの探索 ／ 死の外の替えた台は器具の中だけ", ok, string.Join("、", vr.Skip(11)));
        }
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var f in new[] { S382.ChainWith(d2), Presets.DeathBard382(d2), Presets.DeathBard382(UnitCatalog.Bard382n) })
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（楽士の3台 × ボス ／ 近衛 ／ 大隊 ／ 第5波・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool ok = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.DirgeEchoAlways\]\s*=\s*\(HcMinusL,\s*""[^""]*第383期");
            Expect("`checkup` の札の分類表に D2 のマイナス（哀歌は敵の耳にも届く・マイナス）の行がある（ソースを読んで確かめる）", ok);
        }
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show ec6cb43d:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第382期の HEAD（ec6cb43d）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
