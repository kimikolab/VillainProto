using System.Reflection;
using BattleCore;
using static Common;
using T323 = Tou323Diag;

// fuse329 の自己検査（第329期）。盤は `tou323` ／ `dust327` と同じ作り（`T323.Ctx`・壁は HP 5000 ／ 攻 1）。
static partial class Fuse329Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly FieldInfo DensityField = typeof(BattleContext).GetField("_density", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_density が見つからない");
    static readonly FieldInfo TouchedField = typeof(BattleContext).GetField("_pwTouched", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_pwTouched が見つからない");
    static void SetDensity(BattleContext ctx, UnitState u, int n) => ((Dictionary<int, int>)DensityField.GetValue(ctx)!)[u.InstanceId] = n;
    static void SetMark(BattleContext ctx, UnitState u) => ((HashSet<int>)TouchedField.GetValue(ctx)!).Add(u.InstanceId);
    static List<(int To, int Amount)> Dmg(BattleContext ctx, UnitState actor, int since)
        => ctx.Events.Skip(since).Where(x => x.Kind == BattleEventKind.Damage && x.ActorId == actor.InstanceId && x.TargetId is int).Select(x => (x.TargetId!.Value, x.Amount)).ToList();
    static int Bursts(BattleContext ctx, UnitState recv, int since) => ctx.Events.Skip(since).Count(x => x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.DustBurst && x.TargetId == recv.InstanceId);
    static string Amts(List<(int To, int Amount)> d) => d.Count == 0 ? "なし" : string.Join(",", d.Select(x => x.Amount));

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# fuse329 check —— 第329期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var Wall = T323.Wall;
        var T = UnitCatalog.Tou;

        // ---- 定義
        {
            bool a = UnitCatalog.TouPBa.Traits.SequenceEqual(T.Traits.Append(TraitId.PowderBurstMarked));
            bool ab = UnitCatalog.TouPBab.Traits.SequenceEqual(T.Traits.Concat(new[] { TraitId.PowderBurstMarked, TraitId.PowderBurstFall }));
            bool c = UnitCatalog.TouPBc.Traits.SequenceEqual(T.Traits.Append(TraitId.PowderSpreadFar));
            bool abc = UnitCatalog.TouPBabc.Traits.SequenceEqual(T.Traits.Concat(new[] { TraitId.PowderBurstMarked, TraitId.PowderBurstFall, TraitId.PowderSpreadFar }));
            var vs = new[] { UnitCatalog.TouPBa, UnitCatalog.TouPBab, UnitCatalog.TouPBc, UnitCatalog.TouPBabc };
            bool text = UnitCatalog.TouPBa.PlusText.EndsWith(UnitCatalog.TouFuseText, StringComparison.Ordinal) && UnitCatalog.TouPBab.PlusText.EndsWith(UnitCatalog.TouFuseFallText, StringComparison.Ordinal)
                && UnitCatalog.TouPBc.PlusText.EndsWith(UnitCatalog.TouDustText, StringComparison.Ordinal) && UnitCatalog.TouPBc.PlusText.Contains(UnitCatalog.TouSpreadFarClause, StringComparison.Ordinal)
                && UnitCatalog.TouPBabc.PlusText.Contains(UnitCatalog.TouSpreadFarClause, StringComparison.Ordinal) && UnitCatalog.TouPBabc.PlusText.EndsWith(UnitCatalog.TouFuseFallText, StringComparison.Ordinal)
                && vs.All(d => d.Id == T.Id && d.MaxHp == T.MaxHp && d.Attack == T.Attack && d.Speed == T.Speed && d.MinusText == T.MinusText && !d.PlusText.Contains(UnitCatalog.TouDustText + "。", StringComparison.Ordinal));
            bool outside = vs.All(d => !UnitCatalog.Everyone.Contains(d)) && UnitCatalog.All.Contains(T) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.All(d => !d.Traits.Contains(TraitId.PowderBurstMarked) && !d.Traits.Contains(TraitId.PowderBurstFall) && !d.Traits.Contains(TraitId.PowderSpreadFar));
            Expect("定義: PB-a ／ PB-a＋b ／ PB-c ／ 全部 ＝ 規定のトウ（DX）＋ 札（文面は粉塵爆発の句 ／ 粉の舞う句を差し替え）・版は `Everyone` の外・`All` 52 枚・規定の駒は PB の札を持たない",
                a && ab && c && abc && text && outside, $"a {a}・ab {ab}・c {c}・abc {abc}・文面 {text}・外 {outside}");
        }

        // ---- PB-a（敵の側）: 前1（出し手・帯電）が弾けて中央（受け手）へ放電。中央の隣（前1 ／ 前3 ／ 後3）に爆発。
        {
            foreach (var (def, tag) in new[] { (T, "規定"), (UnitCatalog.TouPBa, "PB-a") })
                foreach (var (mark, n) in new[] { (true, 0), (true, 2), (false, 0), (false, 2) })
                {
                    var ctx = T323.Ctx(Formation.Build(back3: def, front1: T323.Mate), Formation.Build(front1: Wall, center: Wall, front3: Wall, back3: Wall), out var p, out var e);
                    var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2); var e3 = T323.Seat(e, 1); var eb = T323.Seat(e, 4);
                    ctx.MarkShock(ef, ef);
                    if (mark) SetMark(ctx, ec);
                    if (n > 0) SetDensity(ctx, ec, n);
                    int from = ctx.Events.Count;
                    ctx.ApplyDamage(ef, 1, T323.Seat(p, 0));
                    var dx = Dmg(ctx, ec, from);
                    int want = tag == "PB-a" ? (mark || n > 0 ? 8 * (1 + n) : 0) : 8 * n;
                    bool ok = want == 0 ? dx.Count == 0 && Bursts(ctx, ec, from) == 0
                        : dx.Count == 3 && dx.All(d => d.Amount == want) && Bursts(ctx, ec, from) == 1
                          && dx.Select(d => d.To).OrderBy(i => i).SequenceEqual(new[] { ef.InstanceId, e3.InstanceId, eb.InstanceId }.OrderBy(i => i));
                    Expect($"{tag}（敵の側）: 中央（印 {(mark ? "あり" : "なし")}・濃さ {n}）に放電が届くと{(want == 0 ? "爆ぜない" : $"隣3体に {want} ずつ")}", ok, $"爆発 {Amts(dx)}");
                }
        }

        // ---- PB-a（味方の側）: 味方の前1（帯電）が敵の一撃で弾けて味方の中央へ。印があっても濃さ 0 なら爆ぜない・濃さ 1 なら 8 ×（1 ＋ 1）
        {
            foreach (int n in new[] { 0, 1 })
            {
                var ctx = T323.Ctx(Formation.Build(back3: UnitCatalog.TouPBa, front1: T323.Mate, center: T323.Mate), Formation.Build(front1: Wall), out var p, out var e);
                var af = T323.Seat(p, 0); var ac = T323.Seat(p, 2);
                ctx.MarkShock(af, af);
                SetMark(ctx, ac);
                if (n > 0) SetDensity(ctx, ac, n);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(af, 1, e[0]);
                var dx = Dmg(ctx, ac, from);
                bool ok = n == 0 ? dx.Count == 0 : dx.Count == 2 && dx.All(d => d.Amount == 16);
                Expect($"PB-a（味方の側）: 粉の印のある味方の中央（濃さ {n}）に放電が届くと{(n == 0 ? "爆ぜない（味方は濃さ ≧ 1 のときだけ）" : "隣の味方2体に 16 ずつ")}", ok, $"爆発 {Amts(dx)}");
            }
        }

        // ---- 糸玉（PB-a）: 印だけ（濃さ 0）の糸玉に放電が届くと、糸玉の隣の敵（前1 ／ 中央）へ 8 ずつ（規定は爆ぜない）
        {
            foreach (var (def, tag) in new[] { (T, "規定"), (UnitCatalog.TouPBa, "PB-a") })
            {
                var ctx = T323.Ctx(Formation.Build(back3: def, back1: UnitCatalog.Kugu), Formation.Build(front1: Wall, center: Wall), out var p, out var e);
                var ef = T323.Seat(e, 0);
                var ball = ctx.PlaceSilkBall(T323.Seat(p, 3), ef)!;
                ball.SetCounter(StatusKeys.Shock, 0);
                SetMark(ctx, ball);
                ctx.MarkShock(ef, ef);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(ef, 1, T323.Seat(p, 4));
                var dx = Dmg(ctx, ball, from);
                bool ok = tag == "PB-a" ? dx.Count == 2 && dx.All(d => d.Amount == 8) && ball.Hp == ball.MaxHp : dx.Count == 0;
                Expect($"{tag}（糸玉）: 粉の印だけの糸玉（濃さ 0）に放電が届くと{(tag == "PB-a" ? "隣の敵2体に 8 ずつ・糸玉の HP は減らない" : "爆ぜない")}", ok, $"爆発 {Amts(dx)}");
            }
        }

        // ---- PB-b: 爆発を浴びた敵に印が付く（帯電 ／ 濃さは変わらない）・同じ放電で二度爆ぜない・次の放電で爆ぜる
        {
            foreach (var (def, tag) in new[] { (UnitCatalog.TouPBa, "PB-a"), (UnitCatalog.TouPBab, "PB-a＋b") })
            {
                var ctx = T323.Ctx(Formation.Build(back3: def, front1: T323.Mate), Formation.Build(front1: Wall, center: Wall, front3: Wall, back3: Wall), out var p, out var e);
                var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2); var e3 = T323.Seat(e, 1); var eb = T323.Seat(e, 4);
                ctx.MarkShock(ef, ef);
                SetMark(ctx, ec);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(ef, 1, T323.Seat(p, 0));
                bool fall = tag == "PB-a＋b";
                bool marks = ctx.PowderMarked(e3) == fall && ctx.PowderMarked(eb) == fall && ctx.PowderMarked(ef) == fall;
                bool dry = T323.Shock(e3) == 0 && T323.Shock(eb) == 0 && ctx.DensityOf(e3) == 0 && ctx.DensityOf(eb) == 0;
                bool once = Bursts(ctx, ec, from) == 1 && Bursts(ctx, e3, from) == 0 && Bursts(ctx, eb, from) == 0 && Bursts(ctx, ef, from) == 0;
                int falls = ctx.Events.Skip(from).Count(x => x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.DustFall);
                // 次の放電: 中央を帯電させて弾けさせる → 隣（前1 ／ 前3 ／ 後3）へ放電。PB-b なら印が付いているので3体とも爆ぜる。
                ctx.MarkShock(ec, ec);
                int from2 = ctx.Events.Count;
                ctx.ApplyDamage(ec, 1, T323.Seat(p, 0));
                int next = Bursts(ctx, e3, from2) + Bursts(ctx, eb, from2) + Bursts(ctx, ef, from2);
                bool ok = marks && dry && once && falls == (fall ? 3 : 0) && next == (fall ? 3 : 0) && T323.Tal(ctx, "tou").Pb329Fall == (fall ? 3 : 0);
                Expect($"{tag}: 爆発を浴びた敵3体に{(fall ? "印が付く（帯電しない・濃さ 0 のまま）・同じ放電では爆ぜない・次の放電で3体とも爆ぜる" : "印は付かない・次の放電でも爆ぜない")}",
                    ok, $"印 {marks}・乾いたまま {dry}・1回だけ {once}・降った {falls}・次の放電の爆発 {next}");
            }
        }

        // ---- PB-c: 2歩目の敵に粉（帯電 ／ 印）が付く。トウ（前1）の粉の一撃を敵の前1 へ（`ChargedPowderTrait` を直に呼ぶ）。
        {
            foreach (var (def, tag) in new[] { (T, "規定"), (UnitCatalog.TouPBc, "PB-c") })
            {
                var ctx = T323.Ctx(Formation.Build(front1: def), Formation.Build(front1: Wall, front3: Wall, center: Wall, back1: Wall, back3: Wall), out var p, out var e);
                var ef = T323.Seat(e, 0);
                var ring1 = e.Where(u => u != ef && FormationRules.AreAdjacent(ef, u)).ToList();
                var ring2 = e.Where(u => u != ef && !ring1.Contains(u) && ring1.Any(r => FormationRules.AreAdjacent(r, u))).ToList();
                new ChargedPowderTrait().OnAfterAttack(ctx, T323.Seat(p, 0), ef, 1);
                bool r1 = ring1.All(u => T323.Shock(u) == 1 && ctx.PowderMarked(u));
                bool far = tag == "PB-c";
                bool r2 = ring2.Count > 0 && ring2.All(u => T323.Shock(u) == (far ? 1 : 0) && ctx.PowderMarked(u) == far);
                var tt = T323.Tal(ctx, "tou");
                Expect($"{tag}: 主目標（敵の前1）の隣（{ring1.Count} 体）に粉・2歩目（{ring2.Count} 体・席 {string.Join(",", ring2.Select(u => u.Slot))}）には{(far ? "粉が舞って帯電し、印が付く" : "付かない")}",
                    r1 && r2 && tt.Pb329FarFell == (far ? ring2.Count : 0), $"1歩目 {r1}・2歩目 {r2}・2歩目に舞った {tt.Pb329FarFell}");
            }
        }

        // ---- 粉の印は弾けたあとも倒れるまで残る（規定 ／ PB-a）
        {
            foreach (var (def, tag) in new[] { (T, "規定"), (UnitCatalog.TouPBa, "PB-a") })
            {
                var ctx = T323.Ctx(Formation.Build(front1: def, back3: T323.Mate), Formation.Build(front1: Wall, center: Wall), out var p, out var e);
                var ef = T323.Seat(e, 0);
                new ChargedPowderTrait().OnAfterAttack(ctx, T323.Seat(p, 0), ef, 1);
                bool m0 = ctx.PowderMarked(ef) && T323.Shock(ef) == 1;
                int from = ctx.Events.Count;
                ctx.ApplyDamage(ef, 1, T323.Seat(p, 4));
                bool popped = T323.Spent(ctx, ef, from) == 1;   // 弾けた（規定は伝染で隣から帯電し直すので、感電の値では見ない）
                Expect($"{tag}: 粉の付いた敵が弾けても粉の印は残る", m0 && popped && ctx.PowderMarked(ef) && ef.IsAlive, $"付いた {m0}・弾けた {popped}・弾けた後の印 {ctx.PowderMarked(ef)}");
            }
        }

        // ---- 試遊の台
        {
            var v = Presets.Versions;
            string[] names = { "試遊・感電 ポンの台", "版・PB-a ポンの台", "版・PB-a＋b ポンの台", "版・PB-c ポンの台", "版・PB-全部 ポンの台" };
            UnitDef[] tous = { T, UnitCatalog.TouPBa, UnitCatalog.TouPBab, UnitCatalog.TouPBc, UnitCatalog.TouPBabc };
            var rows = Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).Concat(v).ToArray();
            bool ok = v.Length == 5 && v.Select(r => r.Name).SequenceEqual(names)
                && Enumerable.Range(0, 5).All(i => v[i].F.Occupied().Select(o => o.Def).SequenceEqual(new[] { UnitCatalog.Kata, UnitCatalog.Shiga, tous[i], UnitCatalog.Kugu, UnitCatalog.Som }))
                && Presets.Playtest.Length == 17 && names.All(n => rows.First(r => r.Name.Contains(n, StringComparison.Ordinal)).Name == n);
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」＋「版・」の4台（並びはポンの台・トウだけ版）・`Playtest` は 17 台のまま・DemoApp の `--demo-preset=` の部分一致で自分に当たる", ok);
        }

        // ---- 戦の中で: verbose の有無で勝敗 ／ 決着T が同じ（ポンの台 × ボス ／ 近衛 × 5 版 × seed 0..9）
        {
            foreach (var v in Vers)
                foreach (var w in new[] { WaveOf("boss"), WaveOf("guard") })
                {
                    int diff = 0;
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var f = Apply(PonBoard(), v);
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                    }
                    Expect($"ポンの台 × {w.Name} × {v.Name}（seed 0..9）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
                }
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show a9d7078:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第328期（a9d7078）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
