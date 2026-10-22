using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh382 check —— 第382期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh382Diag
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
    static readonly UnitDef MateHi = new() { Id = "mateHi382", Name = "仲間強", MaxHp = 60, Attack = 9, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef MateLo = new() { Id = "mateLo382", Name = "仲間弱", MaxHp = 60, Attack = 5, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef MateLo2 = new() { Id = "mateLo2382", Name = "仲間弱二", MaxHp = 60, Attack = 5, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };   // 帳簿は `Id` ごとなので、同じ攻撃力の別の駒
    static readonly UnitDef FoeHi = new() { Id = "foeHi382", Name = "的強", MaxHp = 999, Attack = 7, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef FoeLo = new() { Id = "foeLo382", Name = "的弱", MaxHp = 999, Attack = 2, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

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
        Console.WriteLine($"# {Tag} sh382 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            var v = UnitCatalog.Vel; var h = UnitCatalog.VelH381; var s2 = UnitCatalog.VelS2;
            bool cards = v.Traits.SequenceEqual(s2.Traits) && v.Traits.SequenceEqual(new[] { TraitId.SoulSplitHalf, TraitId.SoulSew }) && ReferenceEquals(v.Actions, s2.Actions)
                && v.PlusText == s2.PlusText && v.MinusText == s2.MinusText && v.Id == h.Id && v.MaxHp == h.MaxHp && v.Attack == h.Attack && v.Speed == h.Speed
                && UnitCatalog.All.Contains(v) && !UnitCatalog.All.Contains(h) && !UnitCatalog.Retired.Contains(h) && UnitCatalog.All.Count == 52;
            bool old = h.Traits.SequenceEqual(new[] { TraitId.Reviver, TraitId.Stitch });
            bool pins = PinV381(v) == h && PinM379(v) == h && PinR378(v) == h && PinV375(v) == h && PinS371(v) == h && PinGVS371(v) == h && PinV381(UnitCatalog.Rica) == UnitCatalog.Rica
                && Pin381(Presets.DeathSeek380()).Occupied().Any(o => ReferenceEquals(o.Def, h)) && !Pin381(Presets.DeathSeek380()).Occupied().Any(o => ReferenceEquals(o.Def, v));
            bool vers = new[] { UnitCatalog.VelS2, UnitCatalog.VelS2n, UnitCatalog.VelS10 }.All(d => d.MaxHp == h.MaxHp && d.Flavor == h.Flavor && !UnitCatalog.All.Contains(d))
                && UnitCatalog.VelS2n.Traits.SequenceEqual(new[] { TraitId.SoulSplitHalf }) && UnitCatalog.VelS10.Traits.SequenceEqual(new[] { TraitId.SoulSplitTen, TraitId.SoulSew });
            Expect("段0: 規定のヴェル ＝ 第381期の S2（札・手番・文面）・旧 `VelH381`（蘇生 ＋ 縫い合わせ・`All` の外）・固定の口（`PinV381` ／ `Pin381` ／ `PinM379` ／ `PinR378` の後ろ・古い口の終端 `PinV375` ／ `PinS371` ／ `PinGVS371` も旧へ）・第381期の版の駒は旧から作ったまま", cards && old && pins && vers);
        }
        {
            long cells = 0, diff = 0;
            foreach (var f in new[] { Presets.DeathSeek380(), Presets.DeathChain377(), Presets.DeathSom379(), CompareBuilds().First(r => r.Name == "裂き (キリ×エグ)").F })
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(f, w, sd).R, I.Raw(I.Swap(f, "vel", UnitCatalog.VelS2), w, sd).R)) diff++;
                    }
            Expect("規定のヴェルは第381期の `VelS2` と同じ動き（ヴェルのいる4台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..9 で勝敗 ／ T ／ 生存数が同じ）", diff == 0 && cells == 160, $"{cells} 戦・差 {diff}");
        }
        {
            bool now = !Boss283Diag.HealPoolNow.Any(d => d.Id == "vel") && !Boss283Diag.HealerDef(UnitCatalog.Vel);
            bool old = Boss283Diag.HealPool.Any(d => ReferenceEquals(d, UnitCatalog.VelH381)) && Boss283Diag.HealPool295.Any(d => d.Id == "vel") && Boss283Diag.HealPool306.Any(d => d.Id == "vel");
            bool same = Boss283Diag.HealPoolNow.Select(d => d.Id).SequenceEqual(Boss283Diag.HealPool.Where(d => d.Id != "vel").Select(d => d.Id));
            Expect("ヒーラーの一覧: 今の一覧（`HealPoolNow`）に規定のヴェルが入らない・過去の器具の一覧（`HealPool` ／ `HealPool295` ／ `HealPool306`）はヴェルを旧（`VelH381`）として数える・ほかの顔ぶれは同じ", now && old && same,
                string.Join("・", Boss283Diag.HealPoolNow.Select(Som307Diag.Short)));
        }

        // ---- 楽士 ----
        UnitDef d1 = UnitCatalog.Bard382, d1n = UnitCatalog.Bard382n;
        {
            bool defs = d1.Traits.SequenceEqual(new[] { TraitId.Dirge, TraitId.DirgeEcho }) && d1n.Traits.SequenceEqual(new[] { TraitId.Dirge }) && d1.MaxHp == 40 && d1.Attack == 3 && d1.Speed == 9 && !d1.Advances
                && d1.Actions is { Count: 1 } a && a[0].Kind == ActionKind.Skill && !UnitCatalog.All.Contains(d1) && !UnitCatalog.All.Contains(d1n) && !UnitCatalog.Everyone.Contains(d1);
            Expect("楽士: D1 ＝ 哀歌 ＋ 哀歌は敵にも届く ／ D1n ＝ 哀歌だけ・HP 40 ／ 攻 3 ／ 速 9 ／ 据置・手番 `[Skill]`・`All` の外", defs, $"「{d1.PlusText}」／「{d1.MinusText}」");
        }
        {
            // 味方が倒れていなければ殴る
            var (ctx, p, e) = Bd(new[] { (0, d1), (1, MateHi), (2, MateLo) }, new[] { (2, FoeHi) });
            var bard = At(p, 0); int hp0 = e[0].Hp;
            ctx.TakeTurn(bard);
            bool hit = e[0].Hp < hp0 && Tal(ctx, At(p, 1)).R382EncoreTurns == 0 && Tal(ctx, bard).R382NoDeath == 1;
            // 味方（儚い胞子）が2体倒れた → いちばん攻撃力の高い味方が1回だけ・楽士自身は選ばれない（攻撃力を上げても）
            var sp1 = ctx.Summon(UnitCatalog.Spore, BattleContext.PlayerTeam)!; var sp2 = ctx.Summon(UnitCatalog.Spore, BattleContext.PlayerTeam)!;
            ctx.ApplyDamage(sp1, 999, e[0]); ctx.ApplyDamage(sp2, 999, e[0]);
            bard.AtkBonus = 50;
            ctx.TakeTurn(bard);
            bool one = Tal(ctx, At(p, 1)).R382EncoreTurns == 1 && Tal(ctx, At(p, 2)).R382EncoreTurns == 0 && Tal(ctx, bard).R382Songs == 1 && Tal(ctx, bard).R382EncoreTurns == 0;
            // 次の手番: 新たに倒れていなければ殴る（窓は手番ごとに 0）
            ctx.TakeTurn(bard);
            bool reset = Tal(ctx, At(p, 1)).R382EncoreTurns == 1 && Tal(ctx, bard).R382NoDeath == 2;
            Expect("楽士: 味方が倒れていなければ殴る ／ 倒れていれば（儚い胞子の死も数える）攻撃力の最も高い味方が1回だけ再行動（2体倒れても1回）・楽士自身は選ばれない・窓は手番ごとに 0 に戻る", hit && one && reset,
                $"的の HP {hp0} → {e[0].Hp}・仲間強の再行動 {Tal(ctx, At(p, 1)).R382EncoreTurns}");
        }
        {
            // 同じ攻撃力なら席番号 ／ 起き上がる死も数える
            var (ctx, p, e) = Bd(new[] { (0, d1), (1, MateLo), (2, UnitCatalog.VelS2), (3, MateLo2), (4, MateHi) }, new[] { (2, FoeHi) });
            var bard = At(p, 0); var hi = At(p, 4); var vel = At(p, 2);
            foreach (var t in vel.Traits) t.OnBattleStart(ctx, vel);   // 中央のヴェルは隣の4枚の魂を分ける
            int lives0 = hi.RawCounter(VelSoulTrait.LivesKey);
            ctx.ApplyDamage(hi, 9999, e[0]);   // 起き上がる死（倒れた味方はこれ1回だけ）
            bool rose = lives0 > 0 && hi.IsAlive;
            hi.AtkBonus = -hi.Def.Attack + 5; vel.AtkBonus = -vel.Def.Attack + 3;   // 攻撃力 5 にそろえる（ヴェルは 3）→ 仲間弱（席 1・席 3）と仲間強（席 4）が同じ → 席 1
            ctx.TakeTurn(bard);
            bool tie = Tal(ctx, At(p, 1)).R382EncoreTurns == 1 && Tal(ctx, At(p, 3)).R382EncoreTurns == 0 && Tal(ctx, hi).R382EncoreTurns == 0;
            Expect("楽士: 同じ攻撃力なら席番号の小さい方 ／ 起き上がる死も「味方の死」として数える", tie && rose, $"後3 の起き上がり {lives0} → {hi.RawCounter(VelSoulTrait.LivesKey)}・再行動 " + string.Join(" ", p.Select(u => $"{u.Slot}:{u.CurrentAttack}/{Tal(ctx, u).R382EncoreTurns}")));
        }
        foreach (var (bd, echo) in new[] { (d1, true), (d1n, false) })
        {
            var (ctx, p, e) = Bd(new[] { (0, bd), (1, MateHi) }, new[] { (0, FoeLo), (1, FoeHi), (2, FoeLo) });
            var bard = At(p, 0); var sp = ctx.Summon(UnitCatalog.Spore, BattleContext.PlayerTeam)!;
            ctx.ApplyDamage(sp, 999, e[1]); ctx.ApplyDamage(At(e, 2), 9999, At(p, 1));
            int ev0 = ctx.Events.Count;
            ctx.TakeTurn(bard);
            var dmg = ctx.Events.Skip(ev0).Where(x => x.Kind == BattleEventKind.Damage && x.ActorId is int).Select(x => x.ActorId!.Value).ToList();
            int iMate = dmg.IndexOf(At(p, 1).InstanceId), iFoe = dmg.IndexOf(At(e, 1).InstanceId);
            bool ok = echo
                ? Tal(ctx, At(e, 1)).R382EchoTurns == 1 && Tal(ctx, At(e, 0)).R382EchoTurns == 0 && iMate >= 0 && iFoe > iMate && Tal(ctx, bard).R382Echoes == 1
                : Tal(ctx, At(e, 1)).R382EchoTurns == 0 && Tal(ctx, bard).R382Echoes == 0 && iMate >= 0;
            Expect(echo ? "D1: 敵が倒れていれば攻撃力の最も高い敵が1回だけ再行動（味方の再行動の後）" : "D1n: 敵が倒れていても敵は再行動しない", ok, $"一撃の順 味方 {iMate} ／ 敵 {iFoe}");
        }

        // ---- 器具の中だけの台 ／ 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            var bard = Presets.Versions.FirstOrDefault(r => r.Name == "版・楽士 死").F;
            bool ok = bard is not null && bard.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Dirge))   /* 第383期: 楽士を D2 に替えた */ && Core.All(c => bard.Occupied().Any(o => ReferenceEquals(o.Def, c)))
                && vr.Count(n => n.StartsWith("版・")) == 1 && !vr.Any(n => n.Contains("素体")) && !Presets.Versions.Any(r => r.F.Occupied().Any(o => o.Def.Id == Plain.Id))
                && !vr.Contains("版・S2 死 探索1位") && !vr.Contains("版・S10 死 探索1位") && vr.Contains("試遊・死 探索1位");
            Expect("試遊の台: 「版・楽士 死」（芯 ＋ 楽士 D1・規定の駒）だけを足した・「版・S2 ／ S10 死 探索1位」は外した・素体の替え ／ 並びの探索の台は器具の中だけ", ok, string.Join("、", vr.Skip(11)));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var f in new[] { ChainWith(d1), ChainWith(d1n), ChainWith(UnitCatalog.Gan), Board(Core.Concat(new[] { UnitCatalog.Sid, d1 }).ToArray(), Perms[0]), Presets.DeathSeek380() })
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（楽士 ／ ガン ／ 規定のヴェルの5台 × ボス ／ 近衛 ／ 大隊 ／ 第5波・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool vel = new[] { TraitId.SoulSplitHalf, TraitId.SoulSew }.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第382期から規定（ヴェル）"))
                && System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.SoulSplitHalf\]\s*=\s*\(HcBothL,");
            bool bard = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.Dirge\]\s*=\s*\(HcPlusL,\s*""[^""]*第382期")
                && System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.DirgeEcho\]\s*=\s*\(HcMinusL,\s*""[^""]*第382期");
            Expect("`checkup` の札の分類表: 規定のヴェルの札（魂を分ける ＝ 表と裏 ／ 縫い付け ＝ プラス）が「第382期から規定（ヴェル）」・哀歌（プラス）／ 哀歌は敵にも届く（マイナス）の行がある（ソースを読んで確かめる）", vel && bard);
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 2e7fff5d:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第381期の HEAD（2e7fff5d）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
