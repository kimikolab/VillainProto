using BattleCore;
using static Common;
using T323 = Tou323Diag;

// stun331 の自己検査（第331期）。盤は `tou323` と同じ作り（`T323.Ctx`）。的は特性の無い木偶（HP 5000 ／ 攻 1・痺れる）。
static partial class Stun331Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    /// <summary>特性の無い的（痺れる・動じない札を持たない）。</summary>
    static readonly UnitDef Dummy = new()
    {
        Id = "dummy331", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static int Gains(BattleContext ctx, UnitState u, string key, int since) => ctx.Events.Skip(since).Count(x => x.Kind == BattleEventKind.StatusGain && x.TargetId == u.InstanceId && x.Text == key);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# stun331 check —— 第331期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 定義
        {
            bool tou = UnitCatalog.TouSTa.Traits.SequenceEqual(UnitCatalog.Tou.Traits.Append(TraitId.LiveWireGuard))
                && UnitCatalog.TouSTb.Traits.SequenceEqual(UnitCatalog.Tou.Traits.Concat(new[] { TraitId.LiveWireGuard, TraitId.ShockGuardDaunt }))
                && UnitCatalog.TouSTc.Traits.SequenceEqual(UnitCatalog.Tou.Traits.Append(TraitId.ShockStunOnce));
            bool kata = UnitCatalog.KataSTa.Traits.SequenceEqual(UnitCatalog.Kata.Traits.Append(TraitId.LiveWireGuard))
                && UnitCatalog.KataSTb.Traits.SequenceEqual(UnitCatalog.Kata.Traits.Concat(new[] { TraitId.LiveWireGuard, TraitId.ShockGuardDaunt }))
                && UnitCatalog.KataSTc.Traits.SequenceEqual(UnitCatalog.Kata.Traits.Append(TraitId.ShockStunOnce));
            var vs = new[] { UnitCatalog.TouSTa, UnitCatalog.TouSTb, UnitCatalog.TouSTc, UnitCatalog.KataSTa, UnitCatalog.KataSTb, UnitCatalog.KataSTc };
            string[] cl = { UnitCatalog.StunGuardClause, UnitCatalog.StunGuardDauntClause, UnitCatalog.StunOnceClause };
            bool text = Enumerable.Range(0, 6).All(i => vs[i].PlusText.Contains(UnitCatalog.StunHalfClause + cl[i % 3], StringComparison.Ordinal));
            bool outside = vs.All(d => !UnitCatalog.Everyone.Contains(d)) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.All(d => !d.Traits.Contains(TraitId.LiveWireGuard) && !d.Traits.Contains(TraitId.ShockGuardDaunt) && !d.Traits.Contains(TraitId.ShockStunOnce));
            Expect("定義: ST-a ＝ 規定のトウ ／ カタ ＋ `LiveWireGuard`・ST-b ＝ ST-a ＋ `ShockGuardDaunt`・ST-c ＝ 規定 ＋ `ShockStunOnce`（文面は痺れの句の後に括弧）・版は `Everyone` の外・規定の駒は ST の札を持たない・`All` 52 枚",
                tou && kata && text && outside, $"トウ {tou}・カタ {kata}・文面 {text}・外 {outside}");
        }

        // ---- ST-a ／ ST-b: 痺れで手番を失った駒は、次の自分の手番の頭まで弾けても痺れない（ST-b は萎縮する）・手番の頭で戻る
        foreach (var (tou, tag) in new[] { (UnitCatalog.Tou, "規定"), (UnitCatalog.TouSTa, "ST-a"), (UnitCatalog.TouSTb, "ST-b") })
        {
            var ctx = T323.Ctx(Formation.Build(back3: tou, front1: UnitCatalog.Shiga), Formation.Build(front1: Dummy), out var p, out var e);
            var x = e[0];
            x.SetCounter(StatusKeys.Stun, 1);
            ctx.TakeTurn(x);                               // 痺れで手番を失う
            int guard = x.RawCounter(ShockRule.GuardKey);
            ctx.MarkShock(x, x);
            int from = ctx.Events.Count;
            ctx.ApplyDamage(x, 1, T323.Seat(p, 4));        // 弾ける
            bool popped = T323.Spent(ctx, x, from) == 1;
            int stun = x.RawCounter(StatusKeys.Stun), daunt = x.RawCounter(StatusKeys.Daunted), dauntEv = Gains(ctx, x, StatusKeys.Daunted, from);
            bool bound = TormentTrait.IsBound(ctx, x);
            var xt = T323.Tal(ctx, x.Def.Id);
            T323.TurnProp.SetValue(ctx, 2);
            ctx.TakeTurn(x);                               // 次の自分の手番の頭で戻る
            int guardAfter = x.RawCounter(ShockRule.GuardKey);
            bool ok = tag == "規定"
                ? popped && guard == 0 && xt.ShockStunGuarded == 0
                : popped && guard == 1 && stun == 0 && xt.ShockStunGuarded == 1 && guardAfter == 0
                  && (tag == "ST-a" ? daunt == 0 && dauntEv == 0 && xt.St331GuardDaunted == 0 : daunt == 1 && dauntEv == 1 && xt.St331GuardDaunted == 1 && bound);
            Expect($"{tag}: 痺れで手番を失った敵が次の手番の前に弾けると{(tag == "規定" ? "（ハメ防止は立たない・痺れの判定へ）" : tag == "ST-a" ? "痺れない・次の自分の手番の頭で戻る" : "痺れず萎縮する（萎縮の `StatusGain`・シガの2倍で縛られた的）・手番の頭で戻る")}",
                ok, $"弾けた {popped}・ハメ防止 {guard} → {guardAfter}・痺れ {stun}・萎縮 {daunt}（出来事 {dauntEv}）・縛られた的 {bound}");
        }

        // ---- ST-c: 同じラウンドの2回目以降の弾けで痺れの判定をしない・次のラウンドは判定する
        foreach (var (tou, tag) in new[] { (UnitCatalog.Tou, "規定"), (UnitCatalog.TouSTc, "ST-c") })
        {
            var ctx = T323.Ctx(Formation.Build(back3: tou, front1: T323.Mate), Formation.Build(front1: Dummy), out var p, out var e);
            var x = e[0];
            long Judged() { var t = T323.Tal(ctx, x.Def.Id); return t.ShockStunned + t.ShockStunMissed + t.ShockStunAlready; }
            for (int k = 0; k < 3; k++) { ctx.MarkShock(x, x); ctx.ApplyDamage(x, 1, T323.Seat(p, 0)); }
            long j1 = Judged(), skip1 = T323.Tal(ctx, x.Def.Id).St331OnceSkip;
            T323.TurnProp.SetValue(ctx, 2);
            ctx.MarkShock(x, x); ctx.ApplyDamage(x, 1, T323.Seat(p, 0));
            long j2 = Judged(), skip2 = T323.Tal(ctx, x.Def.Id).St331OnceSkip;
            bool ok = tag == "ST-c" ? j1 == 1 && skip1 == 2 && j2 == 2 && skip2 == 2 : j1 == 3 && skip1 == 0 && j2 == 4;
            Expect($"{tag}: 同じラウンドに3回弾けると{(tag == "ST-c" ? "判定は1回（2回目以降は判定しない・乱数も引かない）・次のラウンドでまた判定する" : "3回とも判定する")}", ok, $"判定 {j1} → {j2}・判定しなかった {skip1} → {skip2}");
        }

        // ---- 痺れない敵（ボス）は今のまま萎縮（ST-c でも同じラウンドの2回目以降も萎縮の判定へ）
        {
            var hero = WaveOf("boss").Make()[0].Def;
            foreach (var (tou, tag) in new[] { (UnitCatalog.Tou, "規定"), (UnitCatalog.TouSTb, "ST-b"), (UnitCatalog.TouSTc, "ST-c") })
            {
                var ctx = T323.Ctx(Formation.Build(back3: tou, front1: T323.Mate), Formation.Build(front1: hero), out var p, out var e, seed: 3);
                var x = e[0];
                for (int k = 0; k < 4; k++) { ctx.MarkShock(x, x); ctx.ApplyDamage(x, 1, T323.Seat(p, 0)); x.SetCounter(StatusKeys.Daunted, 0); }
                var t = T323.Tal(ctx, x.Def.Id);
                Expect($"{tag}: 痺れない敵（ボス）は弾けるたびに萎縮の判定（痺れない・ハメ防止も1ターン1回も効かない）", x.ControlBlockedAtEntry && t.ShockStunned == 0 && t.St331OnceSkip == 0 && t.ShockStunGuarded == 0 && t.ShockDaunted + t.ShockStunMissed == 4,
                    $"萎縮 {t.ShockDaunted}・外れ {t.ShockStunMissed}・判定しなかった {t.St331OnceSkip}");
            }
        }

        // ---- 試遊の台
        {
            var v = Presets.Versions;
            string[] names = { "試遊・感電 ポンの台", "版・ST-a ポンの台", "版・ST-b ポンの台", "版・ST-c ポンの台" };
            var tk = new[] { (UnitCatalog.Tou, UnitCatalog.Kata), (UnitCatalog.TouSTa, UnitCatalog.KataSTa), (UnitCatalog.TouSTb, UnitCatalog.KataSTb), (UnitCatalog.TouSTc, UnitCatalog.KataSTc) };
            var rows = Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).Concat(v).ToArray();
            bool ok = v.Select(r => r.Name).SequenceEqual(names)
                && Enumerable.Range(0, 4).All(i => v[i].F.Occupied().Select(o => o.Def).SequenceEqual(new[] { tk[i].Item2, UnitCatalog.Shiga, tk[i].Item1, UnitCatalog.Kugu, UnitCatalog.Som }))
                && Presets.Playtest.Length == 17 && names.All(n => rows.First(r => r.Name.Contains(n, StringComparison.Ordinal)).Name == n);
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」＋「版・ST-a ／ ST-b ／ ST-c ポンの台」（並びはポンの台・トウ ／ カタだけ版）・`Playtest` は 17 台のまま・`--demo-preset=` の部分一致で自分に当たる", ok);
        }

        // ---- 戦の中で: verbose の有無で勝敗 ／ 決着T が同じ（代表台 5 × ボス ／ 近衛 ／ 大隊 × 4 版 × seed 0..9）
        {
            int diff = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, bf) in Boards())
                    foreach (var w in new[] { WaveOf("boss"), WaveOf("guard"), WaveOf("bat") })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            var f = Apply(bf, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            n++;
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        }
            Expect($"代表台 5 × ボス ／ 近衛 ／ 大隊 × 4 版 × seed 0..9（{n} 戦）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 61c5ccb:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第330期（61c5ccb）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
