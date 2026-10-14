using System.Reflection;
using BattleCore;
using static Common;
using T323 = Tou323Diag;

// settle330 の自己検査（第330期）。盤は `tou323` ／ `fuse329` と同じ作り（`T323.Ctx`・壁は HP 5000 ／ 攻 1）。
static partial class Settle330Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly FieldInfo TouchedField = typeof(BattleContext).GetField("_pwTouched", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_pwTouched が見つからない");
    static void SetMark(BattleContext ctx, UnitState u) => ((HashSet<int>)TouchedField.GetValue(ctx)!).Add(u.InstanceId);
    static int Bursts(BattleContext ctx, UnitState recv, int since) => ctx.Events.Skip(since).Count(x => x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.DustBurst && x.TargetId == recv.InstanceId);
    static List<int> DmgFrom(BattleContext ctx, UnitState actor, int since)
        => ctx.Events.Skip(since).Where(x => x.Kind == BattleEventKind.Damage && x.ActorId == actor.InstanceId && x.TargetId is int).Select(x => x.Amount).ToList();

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# settle330 check —— 第330期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var Wall = T323.Wall;
        UnitDef T = UnitCatalog.TouH331, TO = UnitCatalog.TouH328;   // 第332期: 「規定」は第330〜331期の規定（`TouH331`）に固定

        // ---- 定義と固定の口
        {
            bool tou = T.Traits.SequenceEqual(TO.Traits.Concat(new[] { TraitId.PowderBurstMarked, TraitId.PowderBurstFall })) && T.Traits.SequenceEqual(UnitCatalog.TouPBab.Traits)
                && T.PlusText == TO.PlusText[..^UnitCatalog.TouDustText.Length] + UnitCatalog.TouFuseSettleText && T.MinusText == TO.MinusText && T.Id == TO.Id && T.MaxHp == TO.MaxHp && T.Attack == TO.Attack && T.Speed == TO.Speed;
            bool old = TO.Traits.SequenceEqual(UnitCatalog.TouH327.Traits.Append(TraitId.DustBurst)) && TO.PlusText == UnitCatalog.TouH327.PlusText + UnitCatalog.TouDustText
                && TO.Traits.SequenceEqual(UnitCatalog.TouDX.Traits) && TO.PlusText == UnitCatalog.TouDX.PlusText;
            bool vers = UnitCatalog.TouPBa.Traits.SequenceEqual(TO.Traits.Append(TraitId.PowderBurstMarked)) && UnitCatalog.TouPBc.Traits.SequenceEqual(TO.Traits.Append(TraitId.PowderSpreadFar))
                && UnitCatalog.TouPBabc.Traits.SequenceEqual(TO.Traits.Concat(new[] { TraitId.PowderBurstMarked, TraitId.PowderBurstFall, TraitId.PowderSpreadFar }))
                && new[] { UnitCatalog.TouPBa, UnitCatalog.TouPBab, UnitCatalog.TouPBc, UnitCatalog.TouPBabc }.All(d => d.Traits.Count(x => x == TraitId.DustBurst) == 1);
            bool outside = new[] { TO, UnitCatalog.TouPBa, UnitCatalog.TouPBab, UnitCatalog.TouPBc, UnitCatalog.TouPBabc }.All(d => !UnitCatalog.Everyone.Contains(d)) && UnitCatalog.All.Contains(UnitCatalog.Tou) && !UnitCatalog.Everyone.Contains(T) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.All(d => (d == UnitCatalog.Tou || !d.Traits.Contains(TraitId.PowderBurstMarked) && !d.Traits.Contains(TraitId.PowderBurstFall)) && !d.Traits.Contains(TraitId.PowderSpreadFar));
            Expect("定義: 規定のトウ ＝ 第328〜329期の規定（`TouH328`・DX）＋ PB-a ＋ PB-b の札（札は第329期の `TouPBab` と同じ・文面は粉塵爆発の句を差し替え）・`TouH328` は第329期までの規定と同じ札と文面・第329期の版は `TouH328` から（DX の札は1枚）・PB-c は規定にしない・旧と版は `Everyone` の外・`All` 52 枚",
                tou && old && vers && outside, $"トウ {tou}・旧 {old}・版 {vers}・外 {outside}");
            bool pin = ReferenceEquals(PinT328(T), TO) && ReferenceEquals(Pin328(Formation.Build(front1: T))[0], TO)
                && ReferenceEquals(PinTK327(T), UnitCatalog.TouH327) && ReferenceEquals(PinTK327(TO), UnitCatalog.TouH327)
                && ReferenceEquals(PinT326(T), UnitCatalog.TouH326) && ReferenceEquals(PinT326(TO), UnitCatalog.TouH326)
                && new[] { T, TO }.All(d => ReferenceEquals(PinTKK324(d), UnitCatalog.TouH324))
                && new Func<UnitDef, UnitDef>[] { PinS317, PinSS318, PinSH321, PinDS316 }.All(p => ReferenceEquals(p(T), UnitCatalog.TouH324))
                && ReferenceEquals(Fuse329Diag.Vers[0].Tou, TO) && ReferenceEquals(Fuse329Diag.PonBoard()[2], TO) && ReferenceEquals(Settle328Diag.PonBoard()[2], TO)
                && CompareBuildsH328().Concat(PlaytestH328).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, T));
            Expect("固定の口: `PinT328` ／ `Pin328` は規定のトウを `TouH328` へ・`PinTK327` ／ `PinT326` ／ `PinTKK324` ／ 古い口は `TouH328` も受ける・`fuse329` の「規定」と `settle328` の「新」は `TouH328`", pin);
        }

        // ---- PB-a: 印だけ（濃さ 0）の敵の中央に放電 → 規定は隣3体に 8 ずつ・旧は爆ぜない
        foreach (var (def, tag) in new[] { (T, "規定"), (TO, "旧（TouH328）") })
        {
            var ctx = T323.Ctx(Formation.Build(back3: def, front1: T323.Mate), Formation.Build(front1: Wall, center: Wall, front3: Wall, back3: Wall), out var p, out var e);
            var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2);
            ctx.MarkShock(ef, ef);
            SetMark(ctx, ec);
            int from = ctx.Events.Count;
            ctx.ApplyDamage(ef, 1, T323.Seat(p, 0));
            var dx = DmgFrom(ctx, ec, from);
            bool now = def == T;
            Expect($"{tag}（PB-a）: 粉の印だけの中央（濃さ 0）に放電が届くと{(now ? "隣3体に 8 ずつ爆ぜる" : "爆ぜない")}", now ? dx.Count == 3 && dx.All(a => a == 8) && Bursts(ctx, ec, from) == 1 : dx.Count == 0, $"爆発 {(dx.Count == 0 ? "なし" : string.Join(",", dx))}");
        }

        // ---- PB-b: 爆発を浴びた敵に印が降る（帯電しない）→ 次の放電で爆ぜる。旧は降らない
        foreach (var (def, tag) in new[] { (T, "規定"), (TO, "旧（TouH328）") })
        {
            var ctx = T323.Ctx(Formation.Build(back3: def, front1: T323.Mate), Formation.Build(front1: Wall, center: Wall, front3: Wall, back3: Wall), out var p, out var e);
            var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2); var e3 = T323.Seat(e, 1); var eb = T323.Seat(e, 4);
            ctx.MarkShock(ef, ef);
            SetMark(ctx, ec);
            if (def == TO) ((Dictionary<int, int>)typeof(BattleContext).GetField("_density", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ctx)!)[ec.InstanceId] = 1;   // 旧でも中央を爆ぜさせる（濃さ 1）
            ctx.ApplyDamage(ef, 1, T323.Seat(p, 0));
            bool now = def == T;
            bool marks = new[] { ef, e3, eb }.All(u => ctx.PowderMarked(u) == now) && T323.Shock(e3) == 0 && T323.Shock(eb) == 0;
            ctx.MarkShock(ec, ec);
            int from2 = ctx.Events.Count;
            ctx.ApplyDamage(ec, 1, T323.Seat(p, 0));
            int next = Bursts(ctx, e3, from2) + Bursts(ctx, eb, from2) + Bursts(ctx, ef, from2);
            Expect($"{tag}（PB-b）: 爆発を浴びた敵3体に{(now ? "印が降り（帯電しない）、次の放電で3体とも爆ぜる" : "印は降らず、次の放電でも爆ぜない")}", marks && next == (now ? 3 : 0), $"印 {marks}・次の放電の爆発 {next}");
        }

        // ---- 試遊の台
        {
            var v = Presets.Versions;
            var rows = Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).Concat(v).ToArray();
            // 第331期: `Versions` は期ごとに「版・」の台を入れ替えるので、1行目（ポンの台）だけを見る（後ろの行は「版・」で始まること）。見出しの文は第330期のまま。
            bool ok = v.Length >= 1 && v[0].Name == "試遊・感電 ポンの台" && v.Skip(1).All(r => r.Name.StartsWith("版・", StringComparison.Ordinal))
                && v[0].F.Occupied().Select(o => o.Def).SequenceEqual(new[] { UnitCatalog.Kata, UnitCatalog.Shiga, UnitCatalog.Tou, UnitCatalog.Kugu, UnitCatalog.Som })
                && !v.Any(r => r.Name.StartsWith("版・PB", StringComparison.Ordinal)) && Presets.Playtest.Length == 17 && rows.First(r => r.Name.Contains(v[0].Name, StringComparison.Ordinal)).Name == v[0].Name;
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」1台（規定の駒・トウは PB-a ＋ b）・第329期の「版・PB」の4台は外した・`Playtest` は 17 台のまま", ok);
        }

        // ---- 戦の中で: verbose の有無で勝敗 ／ 決着T が同じ（代表台 5 × ボス ／ 近衛 ／ 大隊 × seed 0..9・規定）
        {
            int diff = 0, n = 0;
            foreach (var (_, _, f) in Boards())
                foreach (var w in new[] { WaveOf("boss"), WaveOf("guard"), WaveOf("bat") })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        n++;
                        if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                    }
            Expect($"代表台 5 × ボス ／ 近衛 ／ 大隊 × seed 0..9（{n} 戦・規定）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show fe686e9:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第329期（fe686e9）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
