using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh381 check —— 第381期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh381Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef MateA = new() { Id = "mateA381", Name = "仲間甲", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef MateB = new() { Id = "mateB381", Name = "仲間乙", MaxHp = 80, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe381", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(params (int Slot, UnitDef D)[] seats)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation();
        foreach (var (s, d) in seats) f[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(Formation.Build(center: Foe), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        return (ctx, p, e);
    }
    static void Start(BattleContext ctx, UnitState u) { foreach (var t in u.Traits) t.OnBattleStart(ctx, u); }
    static void Act(BattleContext ctx, UnitState u) { foreach (var t in u.Traits) t.OnAction(ctx, u, u.Def.Actions![0]); }
    static int Lives(UnitState u) => u.RawCounter(VelSoulTrait.LivesKey);
    /// <summary>第381期の HEAD（2e7fff5d）の `docs/balance.md`。</summary>
    static string[] BalanceH381()
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git", "show 2e7fff5d:docs/balance.md") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
        using var pr = System.Diagnostics.Process.Start(psi)!;
        string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit();
        return x.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
    }
    static UnitState At(List<UnitState> p, int slot) => p.First(u => u.Slot == slot);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh381 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の駒 ----
        {
            var v = UnitCatalog.VelH381;   // 第382期: 第381期までの規定のヴェル（規定は S2 になった）
            bool def = v.Traits.SequenceEqual(new[] { TraitId.Reviver, TraitId.Stitch }) && !UnitCatalog.All.Contains(v) && UnitCatalog.All.Count == 52;
            bool vers = UnitCatalog.VelS2.Traits.SequenceEqual(new[] { TraitId.SoulSplitHalf, TraitId.SoulSew }) && UnitCatalog.VelS2.Actions is { Count: 1 } a2 && a2[0].Kind == ActionKind.Skill
                && UnitCatalog.VelS2n.Traits.SequenceEqual(new[] { TraitId.SoulSplitHalf }) && UnitCatalog.VelS2n.Actions is null
                && UnitCatalog.VelS10.Traits.SequenceEqual(new[] { TraitId.SoulSplitTen, TraitId.SoulSew })
                && new[] { UnitCatalog.VelS2, UnitCatalog.VelS2n, UnitCatalog.VelS10 }.All(d => d.Id == v.Id && d.MaxHp == v.MaxHp && d.Attack == v.Attack && d.Speed == v.Speed && !UnitCatalog.All.Contains(d) && !d.Traits.Contains(TraitId.Reviver) && !d.Traits.Contains(TraitId.Stitch));
            Expect("版の駒: S2 ＝ 分ける（半分）＋ 縫い付け（手番 `[Skill]`）／ S2n ＝ 分ける（半分）だけ（手番は殴る）／ S10 ＝ 分ける（十）＋ 縫い付け・縫い合わせと蘇生を外した・体はヴェルのまま・`All` の外・規定のヴェルは今どおり", def && vers,
                $"S2「{UnitCatalog.VelS2.PlusText}」／「{UnitCatalog.VelS2.MinusText}」・S10「{UnitCatalog.VelS10.MinusText}」");
        }

        // ---- 開戦時 ----
        foreach (var (vd, ten) in new[] { (UnitCatalog.VelS2, false), (UnitCatalog.VelS10, true) })
        {
            var (ctx, p, _) = Board((0, vd), (1, MateA), (2, MateB), (3, MateA), (4, MateB));
            var vel = At(p, 0);
            var spore = ctx.Summon(UnitCatalog.Spore, BattleContext.PlayerTeam, at: 7)!;
            var max0 = p.ToDictionary(u => u.InstanceId, u => u.MaxHp);
            Start(ctx, vel);
            var team = ctx.LivingMembers(BattleContext.PlayerTeam);
            bool ok = team.All(u =>
            {
                bool should = u != vel && !u.HasTrait(TraitId.Ephemeral) && FormationRules.AreAdjacent(vel, u);
                if (!should) return Lives(u) == 0 && u.RawCounter(VelSoulTrait.SplitKey) == 0 && (u == spore || u.MaxHp == max0[u.InstanceId]);
                int mx = ten ? 10 : Math.Max(1, max0[u.InstanceId] / 2);
                return u.MaxHp == mx && u.Hp == mx && Lives(u) == (ten ? 3 : 2);
            });
            int adj = p.Count(u => u != vel && FormationRules.AreAdjacent(vel, u)), non = p.Count(u => u != vel && !FormationRules.AreAdjacent(vel, u));
            bool cover = adj > 0 && non > 0 && FormationRules.AreAdjacent(vel, spore) && Lives(vel) == 0;
            Expect($"開戦時（{(ten ? "S10" : "S2")}）: 隣の味方だけが分けられる（ヴェル自身 ／ 儚い胞子 ／ 隣でない駒は分けられない）・最大HPと HP が{(ten ? " 10" : "半分")}・起き上がり {(ten ? 3 : 2)} 度", ok && cover,
                $"隣 {adj} 体 ／ 隣でない {non} 体・隣の胞子 {(FormationRules.AreAdjacent(vel, spore) ? "あり" : "なし")}");
        }

        // ---- 起き上がり ----
        {
            var (ctx, p, e) = Board((0, UnitCatalog.RicaH378), (1, UnitCatalog.MugH379), (2, UnitCatalog.VelS2), (3, MateA), (4, MateB));
            var vel = At(p, 2); var rica = At(p, 0); var mug = At(p, 1); var m3 = At(p, 3);
            Start(ctx, vel);
            bool allSplit = new[] { rica, mug, m3, At(p, 4) }.All(u => Lives(u) == 2);
            m3.AtkBonus = 5;
            int l0 = rica.RawCounter("necro"), units0 = ctx.LivingMembers(BattleContext.PlayerTeam).Count, ev0 = ctx.Events.Count;
            ctx.ApplyDamage(m3, 9999, e[0]);
            bool deathEv = ctx.Events.Skip(ev0).Any(x => x.Kind == BattleEventKind.Death && x.TargetId == m3.InstanceId)
                && ctx.Events.Skip(ev0).Any(x => x.Kind == BattleEventKind.Revive && x.TargetId == m3.InstanceId && x.Text == BattleContext.SoulRiseText);
            bool rose = m3.IsAlive && m3.Hp == m3.MaxHp && m3.MaxHp == 25 && m3.Slot == 3 && Lives(m3) == 1 && m3.AtkBonus == 5;
            bool layer = rica.RawCounter("necro") == l0 + 1;
            bool noSeat = ctx.LivingMembers(BattleContext.PlayerTeam).Count == units0 && ctx.LivingMembers(BattleContext.PlayerTeam).All(u => u.Slot <= 4);
            Expect("起き上がり: 死の出来事が出る ／ リィカの層 +1 ／ 同じ席で分けられた後の最大HPの満タン ／ 回数 −1 ／ 召喚枠を使わない ／ 攻撃力の上乗せを保つ・台本に `Revive`（起き上がり）", allSplit && deathEv && rose && layer && noSeat,
                $"HP {m3.Hp} ／ {m3.MaxHp}・席 {m3.Slot}・残り {Lives(m3)}・上乗せ {m3.AtkBonus}・層 {l0} → {rica.RawCounter("necro")}");
            int Spores() => ctx.LivingMembers(BattleContext.PlayerTeam).Count(u => u.Def.Id == UnitCatalog.Spore.Id);
            ctx.ApplyDamage(mug, 9999, e[0]); int s1 = Spores();
            ctx.ApplyDamage(mug, 9999, e[0]); int s2 = Spores();
            bool twice = mug.IsAlive && Lives(mug) == 0;
            int l1 = rica.RawCounter("necro");
            ctx.ApplyDamage(mug, 9999, e[0]); int s3 = Spores();
            Expect("起き上がる死で「自分の死」の札が起きない（分けられたムグが起き上がるとき胞子が出ない・回数が尽きた本当の死でだけ胞子2体）・本当の死でも層 +1", s1 == 0 && s2 == 0 && twice && !mug.IsAlive && s3 == 2 && rica.RawCounter("necro") == l1 + 1,
                $"胞子 {s1} → {s2} → {s3}・層 {l0} → {rica.RawCounter("necro")}");
        }

        // ---- 手番 ----
        {
            var (ctx, p, e) = Board((0, MateA), (1, MateA), (2, UnitCatalog.VelS2), (3, MateB), (4, MateB));
            var vel = At(p, 2);
            Start(ctx, vel);
            var adj = p.Where(u => u != vel && FormationRules.AreAdjacent(vel, u)).ToList();
            foreach (var u in adj) u.SetCounter(VelSoulTrait.LivesKey, 2);
            At(p, 1).SetCounter(VelSoulTrait.LivesKey, 1); At(p, 3).SetCounter(VelSoulTrait.LivesKey, 1);   // 回数 1 が2体（最大HP 25 と 40）→ 40 の方
            Act(ctx, vel); bool pick1 = Lives(At(p, 3)) == 2 && Lives(At(p, 1)) == 1;
            At(p, 4).SetCounter(VelSoulTrait.LivesKey, 1); At(p, 3).SetCounter(VelSoulTrait.LivesKey, 1); At(p, 1).SetCounter(VelSoulTrait.LivesKey, 2);   // 回数 1・最大HP 40 が2体 → 席番号の小さい方（3）
            Act(ctx, vel); bool pick2 = Lives(At(p, 3)) == 2 && Lives(At(p, 4)) == 1;
            foreach (var u in adj) u.SetCounter(VelSoulTrait.LivesKey, VelSoulTrait.MaxLives);
            int hp0 = e[0].Hp;
            Act(ctx, vel); bool capped = adj.All(u => Lives(u) == VelSoulTrait.MaxLives) && e[0].Hp < hp0;
            Expect("手番: 隣の分けられた味方1体の回数 +1（上限 3）・回数の少ない者 → 最大HPの大きい者 → 席番号・全員が上限ならふつうに殴る", adj.Count == 4 && pick1 && pick2 && capped, $"的の HP {hp0} → {e[0].Hp}");
        }

        // ---- ヒーラーの定義 ----
        {
            bool velH = Boss283Diag.HealerDef(UnitCatalog.VelH381) && Boss283Diag.HealPool.Contains(UnitCatalog.VelH381);   // 第382期: 旧のヴェル ／ 旧の一覧
            bool vers = new[] { UnitCatalog.VelS2, UnitCatalog.VelS2n, UnitCatalog.VelS10 }.All(d => !Boss283Diag.HealerDef(d));
            Expect("版のヴェルはヒーラーの機械的な定義（第283期・味方に回復か破片を2回以上書ける札）に入らない・規定のヴェルは入る", velH && vers);
        }

        // ---- `compare` の規定 ----
        {
            var doc = new Dictionary<string, double[]>();
            int nw = EnemyCatalog.Stages.Count;
            foreach (string line in BalanceH381())   // 第382期: 第381期の HEAD の `docs/balance.md`（規定のヴェルが変わったので、旧に固定した行と比べる）
            {
                if (!line.StartsWith("| ")) continue;
                var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
                if (cells.Length != nw + 1 || !cells[1].EndsWith("%")) continue;
                var v = new double[nw]; bool ok = true;
                for (int w = 0; w < nw; w++) if (!double.TryParse(cells[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
                if (ok) doc[cells[0]] = v;
            }
            var rows = CompareBuildsH384().Select(r => (r.Name, F: Pin381(r.F))).ToArray();   // 第385期: エレの2行は第381期の `docs/balance.md` に無い
            var g = CGrid(rows);
            int mism = 0, miss = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                if (!doc.TryGetValue(rows[r].Name, out var d)) { miss++; continue; }
                for (int w = 0; w < nw; w++) if (Math.Abs(d[w] - g[r, w]) > 0.05) mism++;
            }
            bool noVelSame = rows.Where(r => !r.F.Occupied().Any(o => o.Def.Id == "vel")).All(r => Enumerable.Range(1, 3).All(vi => Apply(r.F, vi).Occupied().Select(o => o.Def).SequenceEqual(r.F.Occupied().Select(o => o.Def))));
            Expect("規定の `compare` 64 行 × 5 波（seed 0..199）が `docs/balance.md` と一致（engine に起き上がりを足しても規定は動かない）・ヴェルのいない行は版の写像で1枚も変わらない", mism == 0 && miss == 0 && noVelSame, $"{rows.Length * nw} セル・ずれ {mism}・無い行 {miss}");
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            // 第382期: S2 を規定にしたので「版・S2 ／ S10 死 探索1位」は外した（並びは `Presets.DeathSeek380(vel)` で組める）。
            var s2 = Presets.DeathSeek380(UnitCatalog.VelS2); var s10 = Presets.DeathSeek380(UnitCatalog.VelS10);
            bool rows = s2.Occupied().Select(o => o.Def).SequenceEqual(Apply(Presets.DeathSeek380(), 1).Occupied().Select(o => o.Def))
                && s10.Occupied().Select(o => o.Def).SequenceEqual(Apply(Presets.DeathSeek380(), 3).Occupied().Select(o => o.Def))
                && !vr.Contains("版・S2 死 探索1位") && !vr.Contains("版・S10 死 探索1位") && vr.Contains("試遊・死 探索1位") && vr.Contains("試遊・死の連鎖") && vr.Contains("試遊・死 ソム");
            var inner = HealBoards().Select(b => b.Name).Concat(new[] { "→" });
            bool only = !vr.Any(n => inner.Any(x => n.Contains(x)));
            Expect("試遊の台: 「版・S2 ／ S10 死 探索1位」（「試遊・死 探索1位」の並びでヴェルだけ版）は第382期に外した（`DeathSeek380(vel)` で組める）・今の台は残る・ヒーラー同席 ／ 死の外の替えた台は器具の中だけ", rows && only, string.Join("、", vr.Skip(11)));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var f in new[] { Apply(Presets.DeathSeek380(), 1), Apply(Presets.DeathSeek380(), 2), Apply(Presets.DeathSeek380(), 3), Apply(Presets.DeathChain377(), 1), Apply(Presets.DeathChain377(), 3) })
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（版のヴェルの5台 × ボス ／ 近衛 ／ 大隊 ／ 第5波・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool rows = new[] { TraitId.SoulSplitHalf, TraitId.SoulSplitTen, TraitId.SoulSew }.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第381期"));
            Expect("`checkup` の札の分類表に魂を分ける（半分 ／ 十・起き上がりを含む）／ 縫い付けの行がある（第381期・ソースを読んで確かめる）", rows);
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 38fbbed3:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第380期の HEAD（38fbbed3）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
