using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;

// sh379 check —— 第379期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh379Diag
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
    static readonly UnitDef Mate = new() { Id = "mate379", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe379", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

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
    static void Kill(BattleContext ctx, UnitState u, UnitState by) => ctx.ApplyDamage(u, 9999, by);
    static List<UnitState> Living(BattleContext ctx, string id) => ctx.LivingMembers(BattleContext.PlayerTeam).Where(u => u.Def.Id == id).ToList();
    static void Act(BattleContext ctx, UnitState u) { foreach (var t in u.Traits) t.OnAction(ctx, u, u.Def.Actions![0]); }
    static void TurnStart(BattleContext ctx, int t) { TurnField.SetValue(ctx, t); foreach (var u in ctx.LivingMembers(BattleContext.PlayerTeam)) foreach (var tr in u.Traits) tr.OnTurnStart(ctx, u); }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh379 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0 ----
        {
            var r = UnitCatalog.Rica; var h = UnitCatalog.RicaH378;
            bool cards = r.Traits.SequenceEqual(h.Traits.Concat(new[] { TraitId.RicaRaiseDeadStack, TraitId.RicaAwakenRaise, TraitId.RicaLayerFlurry, TraitId.UncapRaise }))
                && r.Traits.SequenceEqual(UnitCatalog.RicaELU.Traits) && r.PlusText == UnitCatalog.RicaELU.PlusText && r.MinusText == h.MinusText
                && h.Traits.SequenceEqual(new[] { TraitId.Necro, TraitId.Sacrifice }) && UnitCatalog.All.Contains(r) && !UnitCatalog.All.Contains(h) && !UnitCatalog.Retired.Contains(h) && UnitCatalog.All.Count == 52
                && UnitCatalog.MugH379.Traits.SequenceEqual(new[] { TraitId.Splitter }) && UnitCatalog.MugH379.Actions is null;   // 第380期: 第379期までの規定のムグ
            bool pins = PinR378(r) == h && PinR378(UnitCatalog.Zoto) == UnitCatalog.Zoto && PinV375(r) == h && PinS371(r) == h && PinGVS371(r) == h
                && Pin378(Row("死の連鎖 (リィカ軸)")).Occupied().Select(o => o.Def).SequenceEqual(Presets.DeathChain377(h, UnitCatalog.MugH379).Occupied().Select(o => o.Def));
            bool olds = UnitCatalog.RicaStack.Traits.SequenceEqual(h.Traits.Append(TraitId.RicaRaiseDeadStack)) && UnitCatalog.RicaLink.Traits.SequenceEqual(h.Traits.Append(TraitId.RicaRaiseDead))
                && UnitCatalog.RicaELU.Traits.SequenceEqual(h.Traits.Concat(new[] { TraitId.RicaRaiseDeadStack, TraitId.RicaAwakenRaise, TraitId.RicaLayerFlurry, TraitId.UncapRaise }));
            Expect("段0: 規定のリィカ ＝ 第378期の ELU のリィカ（札と文面）・旧 `RicaH378`（`All` の外）・固定の口（`PinR378` ／ `Pin378` ／ 古い口の終端 `PinV375` ／ `PinS371` ／ `PinGVS371`）・第377 ／ 378期の版の駒は旧から作ったまま・ムグは規定のまま", cards && pins && olds);
        }
        {
            // 第378期の版の駒の結果が変わらない: ELU の死の連鎖 × 4 波 × seed 0..9 で、版の駒そのもの（ムグ MU）の勝敗 ／ T が、規定の札と同じ札のリィカ（＝ 新しい規定 ＋ ムグ MU）と一致
            long diff = 0, cells = 0;
            var a = Presets.DeathChain377(UnitCatalog.RicaELU, UnitCatalog.MugSproutMU); var b = Presets.DeathChain377(UnitCatalog.Rica, UnitCatalog.MugSproutMU);
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                for (int sd = 0; sd < 10; sd++) { cells++; if (!Same(I.Raw(a, w, sd).R, I.Raw(b, w, sd).R)) diff++; }
            Expect("段0: 規定のリィカは第378期の ELU のリィカと同じ動き（死の連鎖・ムグ MU × 4 波 × seed 0..9 で勝敗 ／ T ／ 生存数が同じ）・第378期の版の駒の結果は `sh378 check` ／ 過去の器具の照合で見る", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- 撒き1 ／ 撒き全 ----
        {
            var (ctx, p, e) = Board((2, UnitCatalog.MugSow1), (4, Mate));
            var mug = p.First(u => u.Def.Id == "mug");
            Act(ctx, mug);
            int s1 = Living(ctx, "spore").Count;
            for (int i = 0; i < 3; i++) Act(ctx, mug);
            int s4 = Living(ctx, "spore").Count; int hp0 = e[0].Hp;
            Act(ctx, mug);   // 空きが無い → 殴る
            bool hit = e[0].Hp < hp0 && Living(ctx, "spore").Count == 4 && Tal(ctx, mug).R379SowNoSeat == 1 && Tal(ctx, mug).R379Sown == 4;
            Expect("撒き1: 手番で胞子が1体湧く（4 回で4枠が埋まる）・空きが無ければふつうに殴る", s1 == 1 && s4 == 4 && hit, $"1 回目 {s1} 体・4 回目 {s4} 体・空きなしの手番で的の HP {hp0} → {e[0].Hp}");
        }
        {
            var (ctx, p, e) = Board((2, UnitCatalog.MugSowAll), (4, Mate));
            var mug = p.First(u => u.Def.Id == "mug");
            Act(ctx, mug);
            int n = Living(ctx, "spore").Count;
            Kill(ctx, Living(ctx, "spore")[0], e[0]);
            Act(ctx, mug);
            Expect("撒き全: 手番で空いた枠がすべて胞子で埋まる（倒れた胞子の枠も次の手番で埋まる）", n == 4 && Living(ctx, "spore").Count == 4 && Tal(ctx, mug).R379Sown == 5, $"1 回目 {n} 体・1体倒れた後の手番で {Living(ctx, "spore").Count} 体");
        }

        // ---- 育つ ----
        {
            var (ctx, p, e) = Board((2, UnitCatalog.MugSowGrow), (4, UnitCatalog.Rica));
            var mug = p.First(u => u.Def.Id == "mug"); var rica = p.First(u => u.Def.Id == "rica");
            Act(ctx, mug);   // T1 に撒く
            var seed = Living(ctx, "spore").Single();
            int deaths0 = ctx.Events.Count(x => x.Kind == BattleEventKind.Death); int layer0 = rica.RawCounter("necro"); int hpR = rica.Hp;
            TurnStart(ctx, 1);   // 同じターンの頭では育たない
            bool notYet = seed.IsAlive && Living(ctx, UnitCatalog.YoungMug.Id).Count == 0;
            TurnStart(ctx, 2);   // 次のターンの頭に育つ
            var young = Living(ctx, UnitCatalog.YoungMug.Id);
            bool grew = !seed.IsAlive && young.Count == 1 && young[0].Slot == seed.Slot && ctx.Events.Count(x => x.Kind == BattleEventKind.Death) == deaths0
                && rica.RawCounter("necro") == layer0 && rica.Hp == hpR && ctx.Events.Any(x => x.Kind == BattleEventKind.SporeGrow && x.ActorId == seed.InstanceId && x.TargetId == young[0].InstanceId);
            Expect("育つ: 撒かれた次のターンの頭に生きている胞子が若いムグに替わる（同じ枠・同じターンの頭では育たない）・死の出来事が出ない・層 ／ 回復が動かない・表示専用の出来事 `SporeGrow`", notYet && grew, $"育った {young.Count}・層 {layer0} → {rica.RawCounter("necro")}");
            var ym = young[0];
            bool ymDef = ym.MaxHp == 20 && ym.Def.Attack == 3 && ym.Def.Speed == 6 && ym.HasTrait(TraitId.Ephemeral) && !ym.HasTrait(TraitId.Splitter) && !ym.HasTrait(TraitId.SporeGrow);
            Act(ctx, ym);
            var ySpores = Living(ctx, "spore");
            bool ySow = ySpores.Count == 1 && !ySpores[0].HasTrait(TraitId.SporeGrow);
            TurnStart(ctx, 3); TurnStart(ctx, 4);
            bool noGrow = Living(ctx, UnitCatalog.YoungMug.Id).Count == 1 && Living(ctx, "spore").Count == 1;
            int sum0 = ctx.Events.Count(x => x.Kind == BattleEventKind.Summon);
            Kill(ctx, ym, e[0]);
            bool noSplit = ctx.Events.Count(x => x.Kind == BattleEventKind.Summon) == sum0;
            Expect("若いムグ: HP 20 ／ 攻 3 ／ 速 6・儚い・手番で胞子を1体撒く（育たない胞子）・それ以上は育たない（撒いた胞子も育たない）・倒れても分裂しない", ymDef && ySow && noGrow && noSplit, $"撒いた {ySpores.Count}・分裂 {(noSplit ? "なし" : "あり")}");
        }

        // ---- 胞子 ／ 若いムグの死で層 +1・亡者は起きない ----
        {
            var (ctx, p, e) = Board((2, UnitCatalog.MugSowGrow), (4, UnitCatalog.Rica));
            var mug = p.First(u => u.Def.Id == "mug"); var rica = p.First(u => u.Def.Id == "rica");
            Act(ctx, mug); TurnStart(ctx, 2);
            var ym = Living(ctx, UnitCatalog.YoungMug.Id).Single();
            Act(ctx, ym);
            var sp = Living(ctx, "spore").Single();
            int l0 = rica.RawCounter("necro");
            Kill(ctx, sp, e[0]); int l1 = rica.RawCounter("necro");
            Kill(ctx, ym, e[0]); int l2 = rica.RawCounter("necro");
            int wights = Living(ctx, UnitCatalog.Wight.Id).Count + ctx.Events.Count(x => x.Kind == BattleEventKind.Summon && x.Text == UnitCatalog.Wight.Name);
            Expect("胞子 ／ 若いムグの死で層 +1 ずつ・儚いのでリィカは亡者を起こさない", l1 == l0 + 1 && l2 == l0 + 2 && wights == 0, $"層 {l0} → {l1} → {l2}・亡者 {wights}");
        }

        // ---- ムグの分裂は今どおり ----
        {
            foreach (var def in new[] { UnitCatalog.MugSow1, UnitCatalog.MugSowAll, UnitCatalog.MugSowGrow })
            {
                var (ctx, p, e) = Board((2, def), (4, Mate));
                var mug = p.First(u => u.Def.Id == "mug");
                Kill(ctx, mug, e[0]);
                var sp = ctx.Events.Where(x => x.Kind == BattleEventKind.Summon && x.Text == UnitCatalog.Spore.Name).ToList();
                if (sp.Count != 2) { Expect($"ムグが倒れたときの胞子2体（{def.PlusText[^10..]}）", false, $"{sp.Count} 体"); goto next; }
            }
            Expect("撒き1 ／ 撒き全 ／ 育つのムグが倒れると胞子2体（今どおり・既定の走査）", true);
            next:;
        }

        // ---- ムグのいない行は版で変わらない ----
        {
            bool same = CompareBuilds().Where(r => !S377.HasMug(r.F)).All(r => Enumerable.Range(1, 3).All(vi => ReferenceEquals(Apply(r.F, vi), r.F) || Apply(r.F, vi).Occupied().Select(o => o.Def).SequenceEqual(r.F.Occupied().Select(o => o.Def))));
            Expect("ムグのいない `compare` の行は版の写像で1枚も変わらない", same);
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            // 第380期: 「版・撒き全 ／ 育つ 死の連鎖」は `Presets.Versions` から外した・規定のムグが撒き全になったので、4台は第379期の規定（ムグは旧）で組む。
            foreach (var f in new[] { Presets.DeathChain377(mug: UnitCatalog.MugSowAll), Presets.DeathChain377(mug: UnitCatalog.MugSowGrow), Pin379(Presets.DeathSom379()), Pin379(Presets.DeathChain377()) })
            {
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（試遊の4台 × 勇者 ／ 近衛 ／ 大隊 ／ 第5波・seed 0..4）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool news = new[] { TraitId.MugSow1, TraitId.MugSowAll, TraitId.MugSowGrow, TraitId.SporeGrow }.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第379期"));
            bool rica = new[] { TraitId.RicaRaiseDeadStack, TraitId.RicaAwakenRaise, TraitId.RicaLayerFlurry, TraitId.UncapRaise }.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第379期から規定（リィカ）"))
                && UnitCatalog.Rica.Traits.All(t => src.Contains($"[TraitId.{t}]", StringComparison.Ordinal)) && src.Contains("[TraitId.Rot]", StringComparison.Ordinal);
            Expect("`checkup` の札の分類表: 規定のリィカの札（亡者起こし・積む ／ 目覚め ／ 連撃 ／ 上限なし）が「第379期から規定（リィカ）」・朽ちの行がある・撒く ／ 撒き散らす ／ 育つ ／ 育つ胞子が第379期の行（ソースを読んで確かめる）", news && rica);
        }

        // ---- 試遊の台 ----
        {
            var vr = Presets.Versions.Select(r => r.Name).ToArray();
            // 第380期: 「版・撒き全 ／ 育つ 死の連鎖」を外し、後ろに第380期の台が足された——頭の 12 台と「試遊・死 ソム」を見て、版の並びは関数から組む。
            bool rows = vr.Take(12).SequenceEqual(new[] { "試遊・感電 ポンの台", "試遊・ミサ 書き手なし", "試遊・燃焼 ポンの台", Db351Diag.PlayRow, "試遊・呪い 壁", "試遊・破片", "試遊・自傷", "試遊・毒", "試遊・毒 席替え", "試遊・毒 紅", "試遊・ヴィオ 台B", "試遊・死の連鎖" }) && vr.Contains("試遊・死 ソム");
            var basis = Row("死の連鎖 (リィカ軸)");
            bool vers = Presets.DeathChain377(mug: UnitCatalog.MugSowAll).Occupied().Select(o => o.Def).SequenceEqual(Apply(basis, 2).Occupied().Select(o => o.Def))
                && Presets.DeathChain377(mug: UnitCatalog.MugSowGrow).Occupied().Select(o => o.Def).SequenceEqual(Apply(basis, 3).Occupied().Select(o => o.Def))
                && S307.Seats(Presets.Versions.First(r => r.Name == "試遊・死 ソム").F) == "ソム・ゴルム・ムグ・ヴェル・リィカ"
                && Pin379(Presets.Versions.First(r => r.Name == "試遊・死の連鎖").F).Occupied().Select(o => o.Def).SequenceEqual(basis.Occupied().Select(o => o.Def));
            Expect("`Presets.Versions` の頭 ＝ 第376期の11台 ＋「試遊・死の連鎖」・「試遊・死 ソム」が残る（第380期に「版・撒き全 ／ 育つ 死の連鎖」を外した・並びは `DeathChain377`）", rows && vers, string.Join("、", vr));
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 97ef8579:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            string he = Head("BattleCore/BattleEngine.cs"), ht = Head("BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect("`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第378期の HEAD（97ef8579）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
