using System.Reflection;
using BattleCore;
using static Common;
using T323 = Tou323Diag;

// settle328 の自己検査（第328期）。盤は `tou323` と同じ作り（`T323.Ctx`・壁は HP 5000 ／ 攻 1）。
static partial class Settle328Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly FieldInfo DensityField = typeof(BattleContext).GetField("_density", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_density が見つからない");
    static void SetDensity(BattleContext ctx, UnitState u, int n) => ((Dictionary<int, int>)DensityField.GetValue(ctx)!)[u.InstanceId] = n;
    static int DmgFrom(BattleContext ctx, UnitState actor, int since) => ctx.Events.Skip(since).Count(x => x.Kind == BattleEventKind.Damage && x.ActorId == actor.InstanceId);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# settle328 check —— 第328期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var Wall = T323.Wall;
        UnitDef T = UnitCatalog.TouH328, TO = UnitCatalog.TouH327, K = UnitCatalog.Kugu, KO = UnitCatalog.KuguH327;   // 第330期: 「規定」のトウは第328〜329期の規定（`TouH328`）に固定

        // ---- 定義と固定の口
        {
            bool tou = T.Traits.SequenceEqual(TO.Traits.Append(TraitId.DustBurst)) && T.Traits.SequenceEqual(UnitCatalog.TouDX.Traits) && T.PlusText == UnitCatalog.TouDX.PlusText
                && T.PlusText == TO.PlusText + UnitCatalog.TouDustText && T.MinusText == TO.MinusText && T.Id == TO.Id && T.MaxHp == TO.MaxHp && T.Attack == TO.Attack && T.Speed == TO.Speed
                && TO.Traits.SequenceEqual(UnitCatalog.TouH326.Traits.Append(TraitId.DenseConduct));
            bool kugu = K.Traits.SequenceEqual(KO.Traits.Append(TraitId.SilkBattery)) && K.Traits.SequenceEqual(UnitCatalog.KuguBat.Traits) && K.PlusText == KO.PlusText + UnitCatalog.KuguBatteryText
                && K.MinusText == KO.MinusText && K.Id == KO.Id && K.MaxHp == KO.MaxHp && KO.Traits.SequenceEqual(UnitCatalog.KuguH324.Traits.Append(TraitId.ThreadPowderFlow));
            bool outside = new[] { TO, KO, UnitCatalog.TouDX, UnitCatalog.KuguBat, UnitCatalog.SomBc }.All(d => !UnitCatalog.Everyone.Contains(d)) && UnitCatalog.All.Contains(UnitCatalog.Tou) && !UnitCatalog.Everyone.Contains(T) && UnitCatalog.All.Contains(K)
                && UnitCatalog.All.Count == 52 && UnitCatalog.Everyone.All(d => !d.Traits.Contains(TraitId.BeastChargeSpread)) && UnitCatalog.Som.Traits.SequenceEqual(UnitCatalog.SomBc.Traits.Take(UnitCatalog.Som.Traits.Count));
            Expect("定義: 規定のトウ ＝ 第327期の規定（`TouH327`）＋ DX の札・規定のクグ ＝ `KuguH327` ＋ 電池の札（札と文面は第327期の `TouDX` ／ `KuguBat` と同じ）・旧は `Everyone` の外・隣帯電は規定にしない・`All` 52 枚",
                tou && kugu && outside, $"トウ {tou}・クグ {kugu}・外 {outside}");
            bool pin = ReferenceEquals(PinTK327(T), TO) && ReferenceEquals(PinTK327(K), KO) && ReferenceEquals(Pin327(Formation.Build(front1: T, front3: K))[1], KO)
                && ReferenceEquals(PinT326(T), UnitCatalog.TouH326) && ReferenceEquals(PinT326(TO), UnitCatalog.TouH326) && ReferenceEquals(PinT326(K), KO)
                && new[] { T, TO, UnitCatalog.TouH326 }.All(d => ReferenceEquals(PinTKK324(d), UnitCatalog.TouH324)) && new[] { K, KO }.All(d => ReferenceEquals(PinTKK324(d), UnitCatalog.KuguH324))
                && new Func<UnitDef, UnitDef>[] { PinS317, PinSS318, PinSH321, PinDS316 }.All(p => ReferenceEquals(p(K), UnitCatalog.KuguH324) && ReferenceEquals(p(T), UnitCatalog.TouH324))
                && ReferenceEquals(Kugu292Diag.Pin294(Formation.Build(front1: KO))[0], UnitCatalog.KuguKGb)
                && ReferenceEquals(Dust327Diag.Vers[0].Tou, TO) && ReferenceEquals(Dust327Diag.Vers[0].Kugu, KO) && ReferenceEquals(Dense326Diag.PonBoard()[3], KO)
                && CompareBuildsH327().Concat(PlaytestH327).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, T) && !ReferenceEquals(o.Def, K));
            Expect("固定の口: `PinTK327` ／ `Pin327` は規定のトウ ／ クグを第327期の規定へ・`PinT326` は第327期の規定のトウも `TouH326` へ ／ クグを `KuguH327` へ・`PinTKK324` ／ `Pin294` は新旧どちらも第324期以前へ・`dust327` ／ `dense326` の規定の列は旧", pin);
        }

        // ---- 規定のトウで爆発が起き、旧では起きない（敵の前1 が弾けて、濃さ 3 の中央へ放電 → 中央の隣3体に 24）
        foreach (var (def, tag, want) in new[] { (T, "規定", 3), (TO, "旧（TouH327）", 0) })
        {
            var ctx = T323.Ctx(Formation.Build(back3: def, front1: T323.Mate), Formation.Build(front1: Wall, center: Wall, front3: Wall, back3: Wall), out var p, out var e);
            var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2);
            ctx.MarkShock(ef, ef);
            SetDensity(ctx, ec, 3);
            int from = ctx.Events.Count;
            ctx.ApplyDamage(ef, 1, T323.Seat(p, 0));
            int n = DmgFrom(ctx, ec, from);
            Expect($"{tag}のトウ: 濃さ 3 の中央に放電が届くと、中央の粉が爆ぜて隣へ（{want} 発）", n == want, $"爆発 {n} 発");
        }

        // ---- 規定のトウで糸玉に粉が積もり、旧では積もらない
        foreach (var (def, tag, want) in new[] { (T, "規定", 1), (TO, "旧（TouH327）", 0) })
        {
            var ctx = T323.Ctx(Formation.Build(front1: def, back1: KO), Formation.Build(front1: Wall), out var p, out var e);
            var ball = ctx.PlaceSilkBall(T323.Seat(p, 3), e[0])!;
            ctx.TakeTurn(T323.Seat(p, 0));
            Expect($"{tag}のトウ: 主目標の隣の帯電した糸玉に粉が落ちて濃さ {want}", ctx.DensityOf(ball) == want && ctx.PowderMarked(ball) == (want == 1), $"濃さ {ctx.DensityOf(ball)}・印 {ctx.PowderMarked(ball)}");
        }

        // ---- 規定のクグで電池が働き、旧では働かない（クグは帯電・敵の前1 が弾けて隣の糸玉も弾ける）
        foreach (var (def, tag, want) in new[] { (K, "規定", 1), (KO, "旧（KuguH327）", 0) })
        {
            var ctx = T323.Ctx(Formation.Build(back3: TO, back1: def), Formation.Build(front1: Wall), out var p, out var e);
            var kugu = T323.Seat(p, 3);
            var ball = ctx.PlaceSilkBall(kugu, e[0])!;
            ctx.MarkShock(kugu, kugu); ctx.MarkShock(e[0], e[0]);
            int from = ctx.Events.Count;
            ctx.ApplyDamage(e[0], 1, T323.Seat(p, 4));
            Expect($"{tag}のクグ（帯電）: 連鎖で弾けた糸玉が連鎖の後に帯電し直す（{want}）", T323.Spent(ctx, ball, from) == 1 && T323.Shock(ball) == want, $"弾けた {T323.Spent(ctx, ball, from)}・糸玉の感電 {T323.Shock(ball)}");
        }

        // ---- 試遊の台
        {
            var v = Presets.Versions;
            var pl = Presets.Playtest;
            var rows = Presets.Compare.Concat(Presets.Cross).Concat(pl).Concat(v).ToArray();
            // 第329期: `Versions` は期ごとに「版・」の台を入れ替えるので、1行目（ポンの台）だけを見る（後ろの行は「版・」で始まること・第327期の DX の版の台は無いこと）。見出しの文は第328期のまま。
            bool ok = v.Length >= 1 && v[0].Name == "試遊・感電 ポンの台" && v.Skip(1).All(r => r.Name.StartsWith("版・", StringComparison.Ordinal)) && !v.Any(r => r.Name.StartsWith("版・DX", StringComparison.Ordinal))
                && v[0].F.Occupied().Select(o => o.Def).SequenceEqual(new[] { UnitCatalog.Kata, UnitCatalog.Shiga, UnitCatalog.Tou, UnitCatalog.Kugu, UnitCatalog.Som })
                && Enumerable.Range(0, 5).Select(i => v[0].F[i]?.Id).SequenceEqual(new[] { "kata", "shiga", "tou", "kugu", "som" })
                && pl.Length == 17 && !pl.Any(r => r.Name == v[0].Name)
                && rows.First(r => r.Name.Contains(v[0].Name, StringComparison.Ordinal)).Name == v[0].Name;
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」1台（前1 カタ・前3 シガ・中央 トウ・後1 クグ・後3 ソム・規定の駒）・第327期の「版・」の4台は外した・`Playtest` は 17 台のまま・DemoApp の `--demo-preset=` の部分一致で自分に当たる", ok);
        }

        // ---- 戦の中で: verbose の有無で勝敗 ／ 決着T が同じ（代表台 7 × ボス ／ 近衛 × seed 0..9・規定）
        {
            int diff = 0, n = 0;
            foreach (var (_, _, f) in Boards())
                foreach (var w in new[] { WaveOf("boss"), WaveOf("guard") })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        n++;
                        if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                    }
            Expect($"代表台 7 × ボス ／ 近衛 × seed 0..9（{n} 戦・規定）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 772f48c:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第327期（772f48c）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
