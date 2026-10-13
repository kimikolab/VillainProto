using System.Reflection;
using BattleCore;
using static Common;
using T323 = Tou323Diag;

// dust327 の自己検査（第327期）。盤は `tou323` と同じ作り（`T323.Ctx`・壁は HP 5000 ／ 攻 1）。
static partial class Dust327Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly FieldInfo DensityField = typeof(BattleContext).GetField("_density", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_density が見つからない");
    static void SetDensity(BattleContext ctx, UnitState u, int n) => ((Dictionary<int, int>)DensityField.GetValue(ctx)!)[u.InstanceId] = n;
    static List<(int To, int Amount)> Dmg(BattleContext ctx, UnitState actor, int since)
        => ctx.Events.Skip(since).Where(x => x.Kind == BattleEventKind.Damage && x.ActorId == actor.InstanceId && x.TargetId is int).Select(x => (x.TargetId!.Value, x.Amount)).ToList();
    static int DmgIn(BattleContext ctx, UnitState u, int since) => ctx.Events.Skip(since).Where(x => x.Kind == BattleEventKind.Damage && x.TargetId == u.InstanceId).Sum(x => x.Amount);
    static int Bursts(BattleContext ctx, UnitState recv, int since) => ctx.Events.Skip(since).Count(x => x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.DustBurst && x.TargetId == recv.InstanceId);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# dust327 check —— 第327期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var Wall = T323.Wall;

        // ---- 定義と固定の口
        {
            var T = UnitCatalog.Tou; var O = UnitCatalog.TouH326;
            bool tou = T.Traits.SequenceEqual(O.Traits.Append(TraitId.DenseConduct)) && T.Traits.SequenceEqual(UnitCatalog.TouDRa.Traits) && T.PlusText == UnitCatalog.TouDRa.PlusText
                && T.PlusText.Contains("濃い駒は浴びる雷が重くなり", StringComparison.Ordinal) && T.MinusText == O.MinusText && T.Id == O.Id && T.MaxHp == O.MaxHp && T.Attack == O.Attack && T.Speed == O.Speed;
            bool old = O.Traits.SequenceEqual(UnitCatalog.TouH324.Traits.Concat(new[] { TraitId.PowderDense, TraitId.PowderDenseKeep, TraitId.PowderDenseBurn, TraitId.PowderContagion, TraitId.PowderMarkFoeOnly }))
                && UnitCatalog.TouDRb.Traits.SequenceEqual(O.Traits.Append(TraitId.DenseConductBoth));
            bool vers = UnitCatalog.TouDX.Traits.SequenceEqual(T.Traits.Append(TraitId.DustBurst)) && UnitCatalog.TouDX.PlusText == T.PlusText + UnitCatalog.TouDustText
                && UnitCatalog.KuguBat.Traits.SequenceEqual(UnitCatalog.Kugu.Traits.Append(TraitId.SilkBattery)) && UnitCatalog.SomBc.Traits.SequenceEqual(UnitCatalog.Som.Traits.Append(TraitId.BeastChargeSpread));
            var vs = new[] { O, UnitCatalog.TouDX, UnitCatalog.KuguBat, UnitCatalog.SomBc };
            bool outside = vs.All(d => !UnitCatalog.Everyone.Contains(d)) && UnitCatalog.All.Contains(T) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.All(d => !d.Traits.Contains(TraitId.DustBurst) && !d.Traits.Contains(TraitId.SilkBattery) && !d.Traits.Contains(TraitId.BeastChargeSpread));
            Expect("定義: 規定のトウ ＝ 第326期の規定（`TouH326`）＋ DR-a の札（札と文面は第326期の `TouDRa` と同じ）・DR-b は旧の規定から・DX ／ 電池 ／ 隣帯電 ＝ 規定 ＋ 札1枚（文面の末尾に句）・版は `Everyone` の外・`All` 52 枚",
                tou && old && vers && outside, $"トウ {tou}・旧 {old}・版 {vers}・外 {outside}");
            bool pin = ReferenceEquals(PinT326(T), O) && ReferenceEquals(PinTKK324(O), UnitCatalog.TouH324) && ReferenceEquals(PinTKK324(T), UnitCatalog.TouH324)
                && ReferenceEquals(Pin325(Formation.Build(front1: T))[0], O) && ReferenceEquals(Pin326(Formation.Build(front1: T))[0], O) && ReferenceEquals(Pin324(Formation.Build(front1: T))[0], UnitCatalog.TouH324)
                && new Func<UnitDef, UnitDef>[] { PinS317, PinSS318, PinSH321, PinDS316 }.All(p => ReferenceEquals(p(T), UnitCatalog.TouH324))
                && CompareBuildsH326().Concat(PlaytestH326).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, T))
                && ReferenceEquals(Dense326Diag.Vers[0].Tou, O) && ReferenceEquals(Dense326Diag.PonBoard()[2], O);
            Expect("固定の口: `PinT326` ／ `Pin326` ／ `Pin325` は規定のトウを `TouH326` へ・`PinTKK324` は規定も `TouH326` も `TouH324` へ（古い口の終端）・`dense326` の規定は `TouH326`", pin);
        }

        // ---- 爆発（敵の側）: 前1（出し手・帯電）が弾けて、中央（受け手・濃さ 3・乾いている）へ放電。中央の隣（前1 ／ 前3（帯電）／ 後3（濃さ 2））に 8 × 3。
        {
            const int n = 3;
            foreach (var (def, tag) in new[] { (UnitCatalog.Tou, "規定"), (UnitCatalog.TouDX, "DX") })
            {
                var ctx = T323.Ctx(Formation.Build(back3: def, front1: T323.Mate), Formation.Build(front1: Wall, center: Wall, front3: Wall, back3: Wall), out var p, out var e);
                var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2); var e3 = T323.Seat(e, 1); var eb = T323.Seat(e, 4);
                ctx.MarkShock(ef, ef); ctx.MarkShock(e3, e3);
                SetDensity(ctx, ec, n); SetDensity(ctx, eb, 2);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(ef, 1, T323.Seat(p, 0));
                var dx = Dmg(ctx, ec, from);   // 中央が出どころの Damage ＝ 爆発
                int inC = DmgIn(ctx, ec, from);
                bool dxTag = tag == "DX";
                var tt = T323.Tal(ctx, "tou");
                bool ok = dxTag
                    ? dx.Count == 3 && dx.All(d => d.Amount == 8 * n) && dx.Select(d => d.To).OrderBy(i => i).SequenceEqual(new[] { ef.InstanceId, e3.InstanceId, eb.InstanceId }.OrderBy(i => i))
                      && inC == 8 * (1 + n) && T323.Shock(e3) == 1 && T323.Spent(ctx, e3, from) == 0 && Bursts(ctx, eb, from) == 0 && Bursts(ctx, ec, from) == 1
                      && H(tt.Dx327N, 1) == 1 && H(tt.Dx327Dealt, 1 * 3 + 1) == 3 * 8 * n
                    : dx.Count == 0 && inC == 8 * (1 + n) && Bursts(ctx, ec, from) == 0;
                Expect($"{tag}（敵の側）: 濃さ {n} の中央に放電 {8 * (1 + n)}（DR-a のまま・自分には爆発を入れない）{(dxTag ? $"→ 中央の隣3体（出し手の前1 ／ 帯電した前3 ／ 濃さ 2 の後3）に {8 * n} ずつ・帯電した前3 は弾けない・濃さのある後3 は爆ぜない（爆発は爆発を呼ばない）" : "・爆発なし")}",
                    ok, $"爆発 {string.Join(",", dx.Select(d => d.Amount))}・中央が浴びた {inC}・前3 の感電 {T323.Shock(e3)}・後3 の爆発 {Bursts(ctx, eb, from)}");
            }
        }

        // ---- 爆発（味方の側・敵味方を問わない）: 味方の前1（帯電）が敵の一撃で弾けて、味方の中央（濃さ 2）へ。中央の隣（前1）に 8 × 2
        {
            var ctx = T323.Ctx(Formation.Build(back3: UnitCatalog.TouDX, front1: T323.Mate, center: T323.Mate), Formation.Build(front1: Wall), out var p, out var e);
            var af = T323.Seat(p, 0); var ac = T323.Seat(p, 2); var tou = T323.Seat(p, 4);
            ctx.MarkShock(af, af);
            SetDensity(ctx, ac, 2);
            int from = ctx.Events.Count;
            ctx.ApplyDamage(af, 1, e[0]);
            var dx = Dmg(ctx, ac, from);
            var tt = T323.Tal(ctx, "tou");
            Expect("DX（味方の側）: 濃さ 2 の味方の中央に放電が届くと、中央の隣の味方（前1 ／ 後3 のトウ）に 16 ずつ（敵味方を問わない）",
                dx.Count == 2 && dx.All(d => d.Amount == 16) && dx.Any(d => d.To == af.InstanceId) && dx.Any(d => d.To == tou.InstanceId) && H(tt.Dx327N, 0) == 1 && H(tt.Dx327Dealt, 0) == 32,
                $"爆発 {string.Join(",", dx.Select(d => $"{d.To}:{d.Amount}"))}");
        }

        // ---- 糸玉の粉が爆ぜる: 敵の前1（帯電）の隣に糸玉（濃さ 4・乾いている）。前1 が弾けて糸玉へ放電 → 糸玉の隣（前1 ／ 中央）に 32 ずつ・糸玉は HP を減らさない
        {
            foreach (var (def, tag) in new[] { (UnitCatalog.Tou, "規定"), (UnitCatalog.TouDX, "DX") })
            {
                var ctx = T323.Ctx(Formation.Build(back3: def, back1: UnitCatalog.Kugu), Formation.Build(front1: Wall, center: Wall), out var p, out var e);
                var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2);
                var ball = ctx.PlaceSilkBall(T323.Seat(p, 3), ef)!;
                ball.SetCounter(StatusKeys.Shock, 0);
                SetDensity(ctx, ball, 4);
                ctx.MarkShock(ef, ef);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(ef, 1, T323.Seat(p, 4));
                var dx = Dmg(ctx, ball, from);
                bool ok = tag == "DX"
                    ? dx.Count == 2 && dx.All(d => d.Amount == 32) && dx.Select(d => d.To).OrderBy(i => i).SequenceEqual(new[] { ef.InstanceId, ec.InstanceId }.OrderBy(i => i)) && ball.Hp == ball.MaxHp && Bursts(ctx, ball, from) == 1
                      && H(T323.Tal(ctx, "tou").Dx327N, 3) == 1
                    : dx.Count == 0 && Bursts(ctx, ball, from) == 0;
                Expect($"{tag}（糸玉）: 濃さ 4 の糸玉（席 {ball.Slot}）に放電が届くと{(tag == "DX" ? "、糸玉の粉が爆ぜて隣の敵（前1 ／ 中央）へ 32 ずつ・糸玉の HP は減らない" : "、何も起きない")}",
                    ok, $"爆発 {string.Join(",", dx.Select(d => $"{d.To}:{d.Amount}"))}・糸玉 HP {ball.Hp}/{ball.MaxHp}");
            }
        }

        // ---- 糸玉に粉が積もる: トウ（前1）が敵の前1 を殴る。敵の前1 の隣（中央）に帯電した糸玉。DX は濃さ +1・粉の印（敵の側）・規定は何も起きない
        {
            foreach (var (def, tag) in new[] { (UnitCatalog.Tou, "規定"), (UnitCatalog.TouDX, "DX") })
            {
                var ctx = T323.Ctx(Formation.Build(front1: def, back1: UnitCatalog.Kugu), Formation.Build(front1: Wall), out var p, out var e);
                var ef = e[0];
                var ball = ctx.PlaceSilkBall(T323.Seat(p, 3), ef)!;
                ctx.TakeTurn(T323.Seat(p, 0));
                var tt = T323.Tal(ctx, "tou");
                bool ok = tag == "DX"
                    ? ctx.DensityOf(ball) == 1 && ctx.PowderMarked(ball) && tt.Dx327BallPowder == 1 && tt.Dx327BallThick == 1 && T323.Shock(ball) == 1
                    : ctx.DensityOf(ball) == 0 && !ctx.PowderMarked(ball) && tt.Dx327BallPowder == 0;
                Expect($"{tag}（糸玉に粉）: トウの主目標の隣の糸玉（席 {ball.Slot}・手番の頭に帯電）{(tag == "DX" ? "に粉が落ちて濃さ 1・粉の印（敵の側）が付く" : "には粉が落ちない（糸玉は隣に数えない）")}",
                    ok, $"濃さ {ctx.DensityOf(ball)}・印 {ctx.PowderMarked(ball)}・落ちた {tt.Dx327BallPowder}");
            }
        }

        // ---- 電池: 敵の前1（帯電）の隣に帯電した糸玉。前1 が弾けて糸玉も弾ける → 連鎖の後に、クグ（電池）が帯電していれば帯電し直す
        {
            foreach (var (def, charged, tag, want) in new[] { (UnitCatalog.KuguBat, true, "電池・クグ帯電", 1), (UnitCatalog.KuguBat, false, "電池・クグ乾いている", 0), (UnitCatalog.Kugu, true, "規定のクグ・帯電", 0) })
            {
                var ctx = T323.Ctx(Formation.Build(back3: UnitCatalog.Tou, back1: def), Formation.Build(front1: Wall), out var p, out var e);
                var ef = e[0]; var kugu = T323.Seat(p, 3);
                var ball = ctx.PlaceSilkBall(kugu, ef)!;
                if (charged) ctx.MarkShock(kugu, kugu);
                ctx.MarkShock(ef, ef);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(ef, 1, T323.Seat(p, 4));
                var kt = T323.Tal(ctx, "kugu");
                bool popped = T323.Spent(ctx, ball, from) == 1;
                Expect($"{tag}: 連鎖で弾けた糸玉は連鎖の後に{(want == 1 ? "帯電し直す（クグの帯電は消費しない）" : "帯電し直さない")}",
                    popped && T323.Shock(ball) == want && kt.Bat327Recharged == want && T323.Shock(kugu) == (charged ? 1 : 0), $"糸玉が弾けた {popped}・糸玉の感電 {T323.Shock(ball)}・電池 {kt.Bat327Recharged}・クグの感電 {T323.Shock(kugu)}");
            }
        }

        // ---- 隣帯電: 敵の前1 に背いた召喚獣（帯電）、中央に乾いた壁。召喚獣が弾けて中央へ放電 → 中央が帯電（同じ連鎖では弾けない）→ 次の一撃で弾ける
        {
            foreach (var (def, tag) in new[] { (UnitCatalog.Som, "規定のソム"), (UnitCatalog.SomBc, "隣帯電") })
            {
                var ctx = T323.Ctx(Formation.Build(back3: def), Formation.Build(front1: UnitCatalog.Fodder, center: Wall), out var p, out var e);
                var fo = T323.Seat(e, 0); var ec = T323.Seat(e, 2); var som = T323.Seat(p, 4);
                ctx.MarkShock(fo, fo);
                int from = ctx.Events.Count;
                ctx.ApplyDamage(fo, 1, som);
                bool bc = tag == "隣帯電";
                int sh1 = T323.Shock(ec), sp1 = T323.Spent(ctx, ec, from);
                int from2 = ctx.Events.Count;
                ctx.ApplyDamage(ec, 1, som);
                var st = T323.Tal(ctx, "som");
                bool ok = T323.Spent(ctx, fo, from) == 1 && (bc
                    ? sh1 == 1 && sp1 == 0 && H(st.Bc327New, 1) == 1 && T323.Spent(ctx, ec, from2) == 1 && st.Bc327Popped == 1
                    : sh1 == 0 && sp1 == 0 && H(st.Bc327New, 1) == 0);
                Expect($"{tag}: 召喚獣の放電を浴びた乾いた中央は{(bc ? "帯電し、同じ連鎖では弾けず、次の一撃で弾ける" : "帯電しない")}", ok, $"中央の感電 {sh1}・同じ連鎖で弾けた {sp1}・新しく帯電 {H(st.Bc327New, 1)}・後で弾けた {st.Bc327Popped}");
            }
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
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 3c2282e:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第326期（3c2282e）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
