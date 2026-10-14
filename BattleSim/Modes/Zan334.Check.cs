using BattleCore;
using static Common;
using T323 = Tou323Diag;

// zan334 の自己検査（第334期）。割り込みの規則は盤を直に組んで ApplyDamage を呼ぶ（`T323.Ctx`・ターンは `T323.TurnProp`）。
static partial class Zan334Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    /// <summary>札の無い木偶の味方（標を付けて殴られる役・同士討ちの撃ち手）。</summary>
    static readonly UnitDef Pal = new()
    {
        Id = "pal334", Name = "木偶の味方", MaxHp = 500, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly UnitDef Pal2 = new()
    {
        Id = "pal334b", Name = "木偶の撃ち手", MaxHp = 500, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly UnitDef Dummy = new()
    {
        Id = "dummy334", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };

    static UnitTally ZanT(BattleContext ctx) => T323.Tal(ctx, "zan");

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# zan334 check —— 第334期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef M = UnitCatalog.TomeH339, MO = UnitCatalog.TomeH333, Z = UnitCatalog.ZanH334;   // 第340期: 第334〜339期の規定のミサ（`TomeH339`・いまの規定は FG-b）で見る   // 第335期: 版の土台は第301〜334期の規定のザン（いまの規定は ZC-a）

        // ---- 段0: 定義・レリック・固定の口
        {
            var old = new[] { TraitId.Rupture, TraitId.RuptureScar, TraitId.RuptureKeep, TraitId.Feathers, TraitId.FeatherLoss, TraitId.FeatherMarkLayer };   // 第299〜333期の規定のミサの札
            bool def = MO.Traits.SequenceEqual(old) && M.Traits.SequenceEqual(old.Where(t => t != TraitId.RuptureScar)) && M.Id == MO.Id && M.MaxHp == MO.MaxHp && M.Attack == MO.Attack && M.Speed == MO.Speed
                && MO.PlusText.Contains("抉った傷は塞がらない", StringComparison.Ordinal) && !M.PlusText.Contains("塞がらない", StringComparison.Ordinal) && M.MinusText == MO.MinusText && M.Flavor == MO.Flavor
                && UnitCatalog.All.Contains(UnitCatalog.Tome) && !UnitCatalog.Everyone.Contains(MO) && UnitCatalog.All.Count == 52 && UnitCatalog.Everyone.All(d => !d.Traits.Contains(TraitId.RuptureScar));
            Expect("段0 定義: 規定のミサ ＝ 第299〜333期の規定（`TomeH333`）から爪痕（`RuptureScar`）だけを外した札・文面から「抉った傷は塞がらない」を外した・旧は `Everyone` の外・`Everyone` に爪痕の保持者 0・`All` 52 枚", def);
            bool relic = RelicCatalog.IsRelic(TraitId.RuptureScar) && RelicCatalog.Info(TraitId.RuptureScar).Name == "抉りの鏃" && RelicCatalog.Added334.Count == 1
                && RelicCatalog.All.Count == 12 && RelicCatalog.All.All(r => r.Id != TraitId.RuptureScar) && Formation.Build(back1: M).WithRelic(3, TraitId.RuptureScar).RelicAt(3) == TraitId.RuptureScar;
            Expect("段0 レリック: 「抉りの鏃」＝ 札 `RuptureScar`（`RelicCatalog.Added334`・`SetRelic` が受け付ける）・`RelicCatalog.All` は 12 枚のまま（第273〜274期の器具の札は変えない）", relic);
            bool pin = ReferenceEquals(PinM333(M), MO) && ReferenceEquals(Pin333(Formation.Build(back1: M))[3], MO)
                && new Func<UnitDef, UnitDef>[] { PinS332, PinTK331, PinT328, PinTK327, PinT326, PinTKK324, PinS317, PinDS316 }.All(p => ReferenceEquals(p(M), MO))
                && ReferenceEquals(Pin299(Formation.Build(back1: M))[3], UnitCatalog.TomeMb)
                && CompareBuildsH333().Concat(PlaytestH333).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, M));
            Expect("段0 固定の口: `PinM333` ／ `Pin333` は規定のミサを `TomeH333` へ・第325〜333期の口（`PinS332` ／ `PinTK331` ／ `PinT328` ／ `PinTK327` ／ `PinT326`）と古い口の終端（`PinTKK324`・`PinS317` ／ `PinDS316` も通る）も・`Pin299` は `TomeMb` のまま", pin);
        }

        // ---- 段0: 戦の中で（規定のミサは削らない・旧は削る・レリックを付けた規定のミサは旧と同じ量だけ削る＝台本が全行同じ）
        {
            var rows = PlaytestH334.Concat(CompareBuildsH334()).Where(r => r.F.Occupied().Any(o => o.Def.Id == "tome")).ToArray();   // 第335期: ザンを第301〜334期の規定に固定
            int n = 0, same = 0; long scarNew = 0, scarOld = 0, scarRelic = 0;
            foreach (var (_, f) in rows)
            {
                int slot = f.Occupied().First(o => o.Def.Id == "tome").Slot;
                var fo = Pin333(f); var fr = f.WithRelic(slot, TraitId.RuptureScar);
                foreach (var w in new[] { Waves[0], Waves[1], Waves[2] })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        var rn = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        var ro = BattleEngine.Run(BattleEngine.Materialize(fo, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var rr = BattleEngine.Run(BattleEngine.Materialize(fr, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        n++;
                        scarNew += rn.TallyByUnit.GetValueOrDefault("tome")?.RuptureScar ?? 0;
                        long so = ro.TallyByUnit.GetValueOrDefault("tome")?.RuptureScar ?? 0, sr = rr.TallyByUnit.GetValueOrDefault("tome")?.RuptureScar ?? 0;
                        scarOld += so; scarRelic += sr;
                        if (ro.PlayerWon == rr.PlayerWon && ro.Turns == rr.Turns && so == sr && ro.Log.Select(l => l.ToString()).SequenceEqual(rr.Log.Select(l => l.ToString()))) same++;
                    }
            }
            Expect($"段0 戦の中で: ミサのいる台 {rows.Length} × ボス ／ 近衛 ／ 大隊 × seed 0..4（{n} 戦）——規定のミサは最大HPを削らない・旧（`TomeH333`）は削る・規定 ＋ 抉りの鏃は旧と同じ量（台本の全行・勝敗・決着T が同じ）",
                scarNew == 0 && scarOld > 0 && scarRelic == scarOld && same == n, $"爪痕 規定 {scarNew}・旧 {scarOld}・鏃 {scarRelic}・台本が同じ {same} ／ {n}");
        }

        // ---- 版の定義と試遊の台
        {
            var cards = new[] { (UnitCatalog.ZanZCa, TraitId.VendettaDirectOnly), (UnitCatalog.ZanZCb, TraitId.VendettaRoundCap3), (UnitCatalog.ZanZCc, TraitId.RecoilLethal), (UnitCatalog.ZanZCd, TraitId.VendettaFrameOnce) };
            bool vers = cards.All(c => c.Item1.Traits.SequenceEqual(Z.Traits.Append(c.Item2)) && c.Item1.Id == Z.Id && c.Item1.MaxHp == Z.MaxHp && c.Item1.Attack == Z.Attack && c.Item1.Speed == Z.Speed && !UnitCatalog.Everyone.Contains(c.Item1))
                && UnitCatalog.Everyone.All(d => cards.All(c => !d.Traits.Contains(c.Item2) || (c.Item2 == TraitId.VendettaDirectOnly && ReferenceEquals(d, UnitCatalog.Zan))))   // 第335期: ZC-a は規定のザンの札になった
                && UnitCatalog.ZanZCa.PlusText.Contains("直に殴られるたび", StringComparison.Ordinal) && UnitCatalog.ZanZCb.PlusText.Contains("（1ターンに3度まで）", StringComparison.Ordinal)
                && UnitCatalog.ZanZCc.MinusText.Contains("倒れることもある", StringComparison.Ordinal) && UnitCatalog.ZanZCd.PlusText.Contains("ヒサが指差した敵を斬る（1ターンに1度）", StringComparison.Ordinal);
            Expect("版の定義: ZC-a ／ b ／ c ／ d ＝ 規定のザン ＋ 札1枚（文面は指示書の叩き台）・`Everyone` の外・`Everyone` に新しい札の保持者 0", vers);
            var v = Presets.Versions;
            // 第335期: `Versions` は期ごとに「版・」の台を入れ替えるので、1行目（ポンの台）だけを見る（後ろの行は「版・」で始まること）。見出しの文は第334期のまま。
            bool rows = v.Length >= 1 && v[0].Name == "試遊・感電 ポンの台" && v.Skip(1).All(r => r.Name.StartsWith("版・", StringComparison.Ordinal) || r.Name.StartsWith("試遊・", StringComparison.Ordinal)) && Presets.Playtest.Length == 17;   /* 第340期: 「試遊・ミサ 書き手なし」も */
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」＋「版・ZC-a〜d 守り型」（守り型の並びでザンだけ版）・`Playtest` は 17 台のまま", rows);
        }

        // ---- ZC-a: 中継の一撃では仇討ちしない・直の一撃ではする（ドハの分かち ／ ゴルムの巨躯）
        foreach (var (zan, tag) in new[] { (Z, "規定"), (UnitCatalog.ZanZCa, "ZC-a") })
        {
            bool a = zan != Z;
            // ドハ: 中央のドハ（標）が前3 の木偶の痛みを肩代わり → 中継の一撃
            var ctx = T323.Ctx(Formation.Build(front1: zan, front3: Pal, center: UnitCatalog.DohaH335), Formation.Build(front1: Dummy), out var p, out var e);   // 第336期: 第317〜335期の規定のドハ
            var doha = T323.Seat(p, 2); var pal = T323.Seat(p, 1); var foe = e[0];
            doha.SetCounter(StatusKeys.Marked, 1);
            ctx.ApplyDamage(pal, 20, foe);
            long relay = ZanT(ctx).VendettaFires;
            ctx.ApplyDamage(doha, 20, foe);
            long direct = ZanT(ctx).VendettaFires - relay;
            long[] kind = ZanT(ctx).Zc334Kind ?? new long[5];
            bool okD = a ? relay == 0 && direct == 1 && ZanT(ctx).Zc334RelaySkipped == 1 : relay == 1 && direct == 1 && kind[1] == 1 && kind[0] == 1;
            // ゴルム: 前1 のゴルム（標）が後1 の木偶への一撃を巨躯で肩代わり → 中継の一撃
            var ctx2 = T323.Ctx(Formation.Build(front1: UnitCatalog.Golm, front3: zan, back1: Pal), Formation.Build(front1: Dummy), out var p2, out var e2);
            var golm = T323.Seat(p2, 0); var pal2 = T323.Seat(p2, 3);
            golm.SetCounter(StatusKeys.Marked, 1);
            ctx2.ApplyDamage(pal2, 20, e2[0]);
            long relay2 = ZanT(ctx2).VendettaFires;
            ctx2.ApplyDamage(golm, 20, e2[0]);
            long direct2 = ZanT(ctx2).VendettaFires - relay2;
            long[] kind2 = ZanT(ctx2).Zc334Kind ?? new long[5];
            bool okG = a ? relay2 == 0 && direct2 == 1 : relay2 == 1 && direct2 == 1 && kind2[2] == 1 && kind2[0] == 1;
            Expect($"{tag}: 標の付いたドハ ／ ゴルムが肩代わりした中継の一撃では{(a ? "仇討ちしない" : "仇討ちする（ドハ経由 ／ ゴルム経由に数える）")}・標の付いた駒への直の一撃では仇討ちする",
                okD && okG, $"ドハ 中継 {relay} ／ 直 {direct}・ゴルム 中継 {relay2} ／ 直 {direct2}");
        }

        // ---- ZC-b: 1ラウンド3回で止まる・次のラウンドで戻る・仇巡り（手番）は数えない
        foreach (var (zan, tag) in new[] { (Z, "規定"), (UnitCatalog.ZanZCb, "ZC-b") })
        {
            bool b = zan != Z;
            var ctx = T323.Ctx(Formation.Build(front1: zan, front3: Pal), Formation.Build(front1: Dummy), out var p, out var e);
            var z = T323.Seat(p, 0); var pal = T323.Seat(p, 1); var foe = e[0];
            pal.SetCounter(StatusKeys.Marked, 1);
            for (int i = 0; i < 5; i++) ctx.ApplyDamage(pal, 5, foe);
            long r1 = ZanT(ctx).VendettaFires;
            T323.TurnProp.SetValue(ctx, 2);
            ctx.ApplyDamage(pal, 5, foe);
            long slashes0 = ZanT(ctx).RoundSlashes;
            ctx.RuptureSkill(z);   // ザンの手番の一振り（標の敵がいるので仇巡り）
            long slashes = ZanT(ctx).RoundSlashes - slashes0;
            for (int i = 0; i < 4; i++) ctx.ApplyDamage(pal, 5, foe);
            long r2 = ZanT(ctx).VendettaFires - r1;
            bool ok = b ? r1 == 3 && slashes > 0 && r2 == 3 && ZanT(ctx).Zc334Capped == 4 : r1 == 5 && slashes > 0 && r2 == 5 && ZanT(ctx).Zc334Capped == 0;
            Expect($"{tag}: 標の味方が1ラウンドに5回殴られると仇討ち{(b ? " 3 回で止まる" : " 5 回")}・次のラウンドは{(b ? "また 3 回まで（間に挟んだ手番の仇巡りの太刀は数えない）" : " 5 回")}",
                ok, $"ラウンド1 {r1}・ラウンド2 {r2}（仇巡りの太刀 {slashes}）・止めた {ZanT(ctx).Zc334Capped}");
        }

        // ---- ZC-c: 返り血で HP 0 になれば倒れる（規定は HP 1 で止まる）
        foreach (var (zan, tag) in new[] { (Z, "規定"), (UnitCatalog.ZanZCc, "ZC-c") })
        {
            bool c = zan != Z;
            var ctx = T323.Ctx(Formation.Build(front1: zan, front3: Pal), Formation.Build(front1: Dummy), out var p, out var e);
            var z = T323.Seat(p, 0); var pal = T323.Seat(p, 1);
            pal.SetCounter(StatusKeys.Marked, 1);
            z.Hp = 2;
            ctx.ApplyDamage(pal, 5, e[0]);
            bool ok = c ? !z.IsAlive && ZanT(ctx).RecoilDeaths == 1 && ZanT(ctx).VendettaFires == 1 : z.IsAlive && z.Hp == 1 && ZanT(ctx).RecoilDeaths == 0;
            Expect($"{tag}: HP 2 のザンが仇討ちして返り血 3 を浴びると{(c ? "倒れる" : " HP 1 で止まる")}", ok, $"HP {z.Hp}・倒れた {ZanT(ctx).RecoilDeaths}");
        }

        // ---- ZC-d: 濡れ衣の仇討ちは1ラウンド1回で止まる・ヒサの指差し（敵に標）は止まらない
        foreach (var (zan, tag) in new[] { (Z, "規定"), (UnitCatalog.ZanZCd, "ZC-d") })
        {
            bool d = zan != Z;
            var ctx = T323.Ctx(Formation.Build(front1: zan, front3: Pal, center: Pal2, back3: UnitCatalog.Hisa), Formation.Build(front1: Dummy), out var p, out var e);
            var pal = T323.Seat(p, 1); var shooter = T323.Seat(p, 2);
            pal.SetCounter(StatusKeys.Marked, 1);
            for (int i = 0; i < 3; i++) ctx.ApplyDamage(pal, 5, shooter, isFriendlyFire: true);
            long f1 = ZanT(ctx).FrameVendettas, acc1 = T323.Tal(ctx, "hisa").AccuseWithZan;
            T323.TurnProp.SetValue(ctx, 2);
            ctx.ApplyDamage(pal, 5, shooter, isFriendlyFire: true);
            long f2 = ZanT(ctx).FrameVendettas - f1;
            bool ok = d ? f1 == 1 && acc1 == 3 && f2 == 1 && ZanT(ctx).Zc334FrameCapped == 2 && e[0].RawCounter(StatusKeys.Marked) > 0
                        : f1 == 3 && acc1 == 3 && f2 == 1 && ZanT(ctx).Zc334FrameCapped == 0;
            Expect($"{tag}: 標の味方が1ラウンドに3回同士討ちされると、ヒサの指差しは3回・濡れ衣の仇討ちは{(d ? " 1 回で止まる（次のラウンドでまた 1 回）" : " 3 回")}",
                ok, $"濡れ衣 ラウンド1 {f1} ／ ラウンド2 {f2}・指差し {acc1}・止めた {ZanT(ctx).Zc334FrameCapped}");
        }

        // ---- 計数は盤面を変えない: verbose の有無で勝敗 ／ 決着T が同じ（代表台 5 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 4 版）
        {
            int diff = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, f) in Boards())
                    foreach (var w in new[] { Waves[0], Waves[1], Waves[2] })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            var fv = Apply(f, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            n++;
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        }
            Expect($"代表台 5 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 4 版（{n} 戦）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 1060ea4:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第333期（1060ea4）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
