using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S382 = Sh382Diag;

// sh384 check —— 第384期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh384Diag
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
    static readonly UnitDef MateHi = new() { Id = "mateHi384", Name = "仲間強", MaxHp = 60, Attack = 9, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef MateLo = new() { Id = "mateLo384", Name = "仲間弱", MaxHp = 60, Attack = 2, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef FoeHi = new() { Id = "foeHi384", Name = "的強", MaxHp = 999, Attack = 7, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef FoeLo = new() { Id = "foeLo384", Name = "的弱", MaxHp = 999, Attack = 2, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

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
    static int Snd(UnitState u) => u.RawCounter(DirgeSoundTrait.SoundKey);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh384 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef g1 = UnitCatalog.Bard384G1, g2 = UnitCatalog.Bard384G2;

        {
            bool def = g1.Traits.SequenceEqual(new[] { TraitId.DirgeSoundG1, TraitId.DirgeEchoAlways }) && g2.Traits.SequenceEqual(new[] { TraitId.DirgeSoundG2, TraitId.DirgeEchoAlways })
                && new[] { g1, g2 }.All(d => d.Id == UnitCatalog.Bard383.Id && d.MaxHp == 40 && d.Attack == 3 && d.Speed == 9 && !d.Advances && d.MinusText == UnitCatalog.Bard383.MinusText && !UnitCatalog.Everyone.Contains(d))
                && TraitCatalog.Resolve(g1.Traits)[0] is DirgeSoundTrait s1 && s1.PerDeath == 20 && s1.Threshold == 50
                && TraitCatalog.Resolve(g2.Traits)[0] is DirgeSoundTrait s2 && s2.PerDeath == 50 && s2.Threshold == 50
                && UnitCatalog.Bard383.Traits.SequenceEqual(new[] { TraitId.Dirge, TraitId.DirgeEchoAlways });
            Expect("G1 ／ G2: 哀歌の音（傷 1 ＝ 1・倒れるたび +20 ／ +50・閾値 50）＋ 哀歌は敵の耳にも届く・体は楽士のまま・`All` の外・D2 は第383期のまま", def, $"「{g1.PlusText}」");
        }
        {
            // 音: 味方の傷の量だけ（はみ出しは数えない）／ 自分の傷も ／ 倒れるたび +20 ／ 儚い駒 ／ 起き上がる死も ／ 上限 100
            var (ctx, p, e) = Bd(new[] { (0, g1), (1, MateLo), (2, UnitCatalog.Vel), (3, MateHi) }, new[] { (2, FoeHi) });
            var bard = At(p, 0); var lo = At(p, 1); var vel = At(p, 2);
            foreach (var t in vel.Traits) t.OnBattleStart(ctx, vel);   // 規定のヴェル（魂を分ける）が隣を分ける
            int s0 = Snd(bard);
            ctx.ApplyDamage(lo, 10, e[0]); int s1 = Snd(bard);                      // +10
            ctx.ApplyDamage(bard, 4, e[0]); int s2 = Snd(bard);                     // 自分 +4
            var sp = ctx.Summon(UnitCatalog.SporeH386, BattleContext.PlayerTeam)!; int spHp = sp.Hp;
            ctx.ApplyDamage(sp, 999, e[0]); int s3 = Snd(bard);                     // 儚い駒: 実際に減った HP（はみ出しは数えない）＋ 20
            bool rising = lo.RawCounter(VelSoulTrait.LivesKey) > 0; int loHp = lo.Hp;
            ctx.ApplyDamage(lo, 999, e[0]); int s4 = Snd(bard);                     // 起き上がる死: 残りの HP ＋ 20（上限 100）
            bool acc = s0 == 0 && s1 == 10 && s2 == 14 && s3 == Math.Min(100, 14 + spHp + 20) && rising && lo.IsAlive && s4 == Math.Min(100, s3 + loHp + 20);
            ctx.ApplyDamage(At(p, 3), 50, e[0]); bool cap = Snd(bard) == 100 && Tal(ctx, bard).R384SndWaste > 0;
            Expect("音: 味方の傷の量だけ溜まる（自分の傷も・とどめのはみ出しは数えない）／ 倒れるたび +20（儚い駒 ／ 起き上がる死も）／ 上限は閾値の2倍（100）", acc && cap,
                $"0 → {s1} → {s2} → {s3}（胞子 HP {spHp}）→ {s4}（起き上がり・HP {loHp}）→ {Snd(bard)}・捨てた {Tal(ctx, bard).R384SndWaste}");
        }
        {
            // 奏でる: 閾値以上で閾値ぶんを使って1回・味方の後に最も強い敵が1回 ／ 届かなければ殴る ／ 余りは持ち越す
            var (ctx, p, e) = Bd(new[] { (0, g2), (1, MateHi), (2, MateLo) }, new[] { (0, FoeLo), (1, FoeHi) });
            var bard = At(p, 0);
            bard.SetCounter(DirgeSoundTrait.SoundKey, 70);
            int ev0 = ctx.Events.Count;
            ctx.TakeTurn(bard);
            var dmg = ctx.Events.Skip(ev0).Where(x => x.Kind == BattleEventKind.Damage && x.ActorId is int).Select(x => x.ActorId!.Value).ToList();
            int iMate = dmg.IndexOf(At(p, 1).InstanceId), iFoe = dmg.IndexOf(At(e, 1).InstanceId);
            int after = Snd(bard);   // 70 − 50 ＋（敵の再行動で味方が受けた傷）
            bool sang = Tal(ctx, At(p, 1)).R382EncoreTurns == 1 && Tal(ctx, At(p, 2)).R382EncoreTurns == 0 && Tal(ctx, At(e, 1)).R382EchoTurns == 1 && Tal(ctx, At(e, 0)).R382EchoTurns == 0
                && iMate >= 0 && iFoe > iMate && after >= 20 && Tal(ctx, bard).R382Songs == 1;
            bard.SetCounter(DirgeSoundTrait.SoundKey, 49);
            int hp0 = At(e, 0).Hp + At(e, 1).Hp;
            ctx.TakeTurn(bard);
            bool shortHit = Tal(ctx, bard).R382Songs == 1 && Tal(ctx, bard).R384Short == 1 && At(e, 0).Hp + At(e, 1).Hp < hp0 && Snd(bard) == 49 && Tal(ctx, At(e, 1)).R382EchoTurns == 1;
            Expect("奏でる: 閾値以上で閾値ぶんを使って1回（いちばん攻撃力の高い味方）・味方の後に攻撃力の最も高い敵が1回 ／ 届かなければ殴る（敵は動かない）／ 余りは持ち越す", sang && shortHit,
                $"70 → {after}・一撃の順 味方 {iMate} ／ 敵 {iFoe}・49 は持ち越し {Snd(bard)}");
        }
        {
            // G2: 倒れるたび +50
            var (ctx, p, e) = Bd(new[] { (0, g2), (1, MateHi) }, new[] { (2, FoeHi) });
            var bard = At(p, 0); var sp = ctx.Summon(UnitCatalog.SporeH386, BattleContext.PlayerTeam)!; int spHp = sp.Hp;
            ctx.ApplyDamage(sp, 999, e[0]);
            Expect("G2: 倒れるたび +50（儚い駒の死も）", Snd(bard) == Math.Min(100, spHp + 50), $"胞子 HP {spHp} → 音 {Snd(bard)}");
        }
        {
            // D2 の列は第383期と同じ（第383期の報告 §3-1 の D2 の列: ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件・seed 0..99）
            var d2 = UnitCatalog.Bard383;
            var exp = new (string Name, Formation F, double[] Win)[]
            {
                ("スィド", S382.Board(new[] { UnitCatalog.MugH386, UnitCatalog.Sid, UnitCatalog.Vel, UnitCatalog.Rica, d2 }, new[] { 0, 1, 2, 3, 4 }), new[] { 100.0, 100, 100, 100, 95.8 }),
                ("ゴルム", S382.Board(new[] { d2, UnitCatalog.Golm, UnitCatalog.Rica, UnitCatalog.MugH386, UnitCatalog.Vel }, new[] { 0, 1, 2, 3, 4 }), new[] { 100.0, 100, 100, 100, 61.0 }),
                ("ボルグ", S382.Board(new[] { d2, UnitCatalog.MugH386, UnitCatalog.Vel, UnitCatalog.Borg, UnitCatalog.Rica }, new[] { 0, 1, 2, 3, 4 }), new[] { 100.0, 100, 100, 100, 87.8 }),
                ("死の連鎖", S382.ChainWith(d2), new[] { 100.0, 100, 100, 66, 53.2 }),
            };
            var ws = new[] { new[] { I.Boss }, new[] { I.Guard }, new[] { I.Bat }, new[] { I.M5 }, I.StrongWaves };
            int bad = 0; var notes = new List<string>();
            foreach (var (name, f, win) in exp)
            {
                var got = new double[ws.Length];
                Parallel.For(0, ws.Length, wi => { int w = 0, n = 0; foreach (var wave in ws[wi]) for (int s = 0; s < 100; s++) { n++; if (I.Raw(f, wave, s, census: false).R.PlayerWon) w++; } got[wi] = Math.Round(100.0 * w / n, 1); });
                for (int wi = 0; wi < ws.Length; wi++) if (Math.Abs(got[wi] - win[wi]) > 0.05) { bad++; notes.Add($"{name} {wi}: {win[wi]} → {got[wi]}"); }
            }
            Expect("D2 は第383期と同じ動き（`sh383 dead` の D2 の列 4 台 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件の勝率が一致）", bad == 0, bad == 0 ? "20 セル・ずれ 0" : string.Join("・", notes));
        }
        {
            // 第385期: 「版・楽士」の台は「試遊・… エレ」に改名した——第384期の名前と版の楽士に戻して見る（`VersionsH384`）
            var vr = VersionsH384().Select(r => r.Name).ToArray();
            var dead = VersionsH384().FirstOrDefault(r => r.Name == "版・楽士 死").F;
            var outs = VersionsH384().FirstOrDefault(r => r.Name == "版・楽士 外").F;
            bool deadOk = dead is not null && dead.Occupied().Select(o => o.Def.Id).SequenceEqual(Presets.DeathBard382().Occupied().Select(o => o.Def.Id))
                && dead.Occupied().Any(o => ReferenceEquals(o.Def, g1) || ReferenceEquals(o.Def, g2));
            bool outOk = outs is not null && outs.Occupied().Count(o => o.Def.Id == g1.Id) == 1 && outs.Occupied().Where(o => o.Def.Id != g1.Id).All(o => UnitCatalog.All.Contains(o.Def))
                && outs.Occupied().Any(o => ReferenceEquals(o.Def, g1) || ReferenceEquals(o.Def, g2) || ReferenceEquals(o.Def, UnitCatalog.Bard383));
            bool only = vr.Count(n => n.StartsWith("版・")) == 2 && !VersionsH384().Any(r => r.F.Occupied().Any(o => o.Def.Id == S382.Plain.Id));
            Expect("試遊の台: 「版・楽士 死」は G1 ／ G2 のよかった方（並びは第382期のまま）・「版・楽士 外」（規定の駒 ＋ 楽士）を足した・素体の替え ／ 並びの探索 ／ 死の外の替えた台はほかに出ない", deadOk && outOk && only, string.Join("、", vr.Skip(11)));
        }
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var f in new[] { S382.ChainWith(g1), S382.ChainWith(g2), Presets.DeathBard382(g1), Put(Sh381Diag.Named("試遊・燃焼 ポンの台"), 2, g1) })
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（G1 ／ G2 の4台 × ボス ／ 近衛 ／ 大隊 ／ 第5波・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool ok = new[] { TraitId.DirgeSoundG1, TraitId.DirgeSoundG2 }.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(HcPlusL,\s*""[^""]*第384期"));
            Expect("`checkup` の札の分類表に哀歌の音（G1 ／ G2・プラス）の行がある（ソースを読んで確かめる）", ok);
        }
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 4efbb421:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第383期の HEAD（4efbb421）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
