using BattleCore;
using static Common;
using T323 = Tou323Diag;

// veil332 の自己検査（第332期）。盤は `tou323` と同じ作り（`T323.Ctx`）。光の雨は `SparkTrait.Rain` を直に呼ぶ。
static partial class Veil332Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    /// <summary>特性の無い味方（最大HP 100・満タン・痺れる）。衣の上限（4 割 ＝ 40）を読みやすくするため。</summary>
    static readonly UnitDef Pal = new()
    {
        Id = "pal332", Name = "木偶の味方", MaxHp = 100, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly UnitDef Dummy = new()
    {
        Id = "dummy332", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# veil332 check —— 第332期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 定義と固定の口
        {
            bool tou = UnitCatalog.Tou.Traits.SequenceEqual(UnitCatalog.TouH331.Traits.Append(TraitId.ShockStunOnce)) && UnitCatalog.Tou.Traits.SequenceEqual(UnitCatalog.TouSTc.Traits)
                && UnitCatalog.Tou.PlusText == UnitCatalog.TouSTc.PlusText && UnitCatalog.TouH331.Traits.SequenceEqual(UnitCatalog.TouH328.Traits.Concat(new[] { TraitId.PowderBurstMarked, TraitId.PowderBurstFall }));
            bool kata = UnitCatalog.Kata.Traits.SequenceEqual(UnitCatalog.KataH331.Traits.Append(TraitId.ShockStunOnce)) && UnitCatalog.Kata.Traits.SequenceEqual(UnitCatalog.KataSTc.Traits)
                && UnitCatalog.Kata.PlusText == UnitCatalog.KataSTc.PlusText && UnitCatalog.KataH331.Traits.SequenceEqual(UnitCatalog.KataH324.Traits.Append(TraitId.ThunderPowderReturn));
            bool vers = UnitCatalog.TouSTa.Traits.SequenceEqual(UnitCatalog.TouH331.Traits.Append(TraitId.LiveWireGuard)) && UnitCatalog.KataSTb.Traits.SequenceEqual(UnitCatalog.KataH331.Traits.Concat(new[] { TraitId.LiveWireGuard, TraitId.ShockGuardDaunt }))
                && UnitCatalog.SomLVc332.Traits.SequenceEqual(UnitCatalog.Som.Traits.Append(TraitId.SparkVeilCap)) && UnitCatalog.SomLVd332.Traits.SequenceEqual(UnitCatalog.Som.Traits.Append(TraitId.SparkVeilTemp))
                && UnitCatalog.SomLVc332.PlusText.Contains(UnitCatalog.SomVeilCapClause, StringComparison.Ordinal) && UnitCatalog.SomLVd332.PlusText.Contains(UnitCatalog.SomVeilTempClause, StringComparison.Ordinal);
            var old = new[] { UnitCatalog.TouH331, UnitCatalog.KataH331, UnitCatalog.SomLVc332, UnitCatalog.SomLVd332, UnitCatalog.TouSTa, UnitCatalog.KataSTa };
            bool outside = old.All(d => !UnitCatalog.Everyone.Contains(d)) && UnitCatalog.All.Contains(UnitCatalog.Tou) && UnitCatalog.All.Contains(UnitCatalog.Kata) && UnitCatalog.All.Count == 52
                && UnitCatalog.Everyone.All(d => (d == UnitCatalog.Tou || d == UnitCatalog.Kata || !d.Traits.Contains(TraitId.ShockStunOnce)) && !d.Traits.Contains(TraitId.SparkVeilCap) && !d.Traits.Contains(TraitId.SparkVeilTemp));
            Expect("定義: 規定のトウ ／ カタ ＝ 第330〜331期の規定（`TouH331` ／ `KataH331`）＋ ST-c（札と文面は第331期の `TouSTc` ／ `KataSTc` と同じ）・第331期の版は旧から・LV-c ＝ 規定のソム ＋ `SparkVeilCap`・LV-d ＝ ＋ `SparkVeilTemp`・旧と版は `Everyone` の外・`All` 52 枚",
                tou && kata && vers && outside, $"トウ {tou}・カタ {kata}・版 {vers}・外 {outside}");
            bool pin = ReferenceEquals(PinTK331(UnitCatalog.Tou), UnitCatalog.TouH331) && ReferenceEquals(PinTK331(UnitCatalog.Kata), UnitCatalog.KataH331)
                && ReferenceEquals(PinT328(UnitCatalog.TouH331), UnitCatalog.TouH328) && ReferenceEquals(PinT328(UnitCatalog.Kata), UnitCatalog.KataH331)
                && ReferenceEquals(PinTK327(UnitCatalog.TouH331), UnitCatalog.TouH327) && ReferenceEquals(PinTK327(UnitCatalog.Kata), UnitCatalog.KataH331)
                && ReferenceEquals(PinT326(UnitCatalog.TouH331), UnitCatalog.TouH326) && ReferenceEquals(PinT326(UnitCatalog.Kata), UnitCatalog.KataH331)
                && new[] { UnitCatalog.Kata, UnitCatalog.KataH331 }.All(d => ReferenceEquals(PinTKK324(d), UnitCatalog.KataH324)) && ReferenceEquals(PinTKK324(UnitCatalog.TouH331), UnitCatalog.TouH324)
                && ReferenceEquals(PinKataTou311(UnitCatalog.KataH331), UnitCatalog.KataH311)
                && ReferenceEquals(Stun331Diag.Vers[0].Tou, UnitCatalog.TouH331) && ReferenceEquals(Stun331Diag.Vers[0].Kata, UnitCatalog.KataH331)
                && ReferenceEquals(Settle330Diag.PonBoard()[2], UnitCatalog.TouH331) && ReferenceEquals(Settle330Diag.PonBoard()[0], UnitCatalog.KataH331)
                && ReferenceEquals(Fuse329Diag.PonBoard()[0], UnitCatalog.KataH331) && ReferenceEquals(Settle328Diag.PonBoard()[0], UnitCatalog.KataH331) && ReferenceEquals(Dust327Diag.PonBoard()[0], UnitCatalog.KataH331)
                && CompareBuildsH331().Concat(PlaytestH331).SelectMany(r => r.F.Occupied()).All(o => !ReferenceEquals(o.Def, UnitCatalog.Tou) && !ReferenceEquals(o.Def, UnitCatalog.Kata));
            Expect("固定の口: `PinTK331` ／ `Pin331` は規定のトウ ／ カタを第330〜331期の規定へ・`PinT328` ／ `PinTK327` ／ `PinT326` は規定のカタを `KataH331` へ・`PinTKK324` ／ `PinKataTou311` は `TouH331` ／ `KataH331` も受ける・`stun331` ／ `settle330` ／ `fuse329` ／ `settle328` ／ `dust327` のカタは `KataH331`", pin);
        }

        // ---- 段0: 規定のトウ（カタ）で ST-c が働く・旧（TouH331）では働かない（同じラウンドに3回弾ける）
        foreach (var (tou, tag, on) in new[] { (UnitCatalog.Tou, "規定のトウ", true), (UnitCatalog.TouH331, "旧（TouH331）", false), (UnitCatalog.Kata, "規定のカタ", true), (UnitCatalog.KataH331, "旧（KataH331）", false) })
        {
            var ctx = T323.Ctx(Formation.Build(back3: tou, front1: T323.Mate), Formation.Build(front1: Dummy), out var p, out var e);
            var x = e[0];
            for (int k = 0; k < 3; k++) { ctx.MarkShock(x, x); ctx.ApplyDamage(x, 1, T323.Seat(p, 0)); }
            var t = T323.Tal(ctx, x.Def.Id);
            long judged = t.ShockStunned + t.ShockStunMissed + t.ShockStunAlready;
            Expect($"段0 {tag}: 同じラウンドに3回弾けると痺れの判定は {(on ? "1 回（ST-c）" : "3 回")}", on ? judged == 1 && t.St331OnceSkip == 2 : judged == 3 && t.St331OnceSkip == 0, $"判定 {judged}・判定しなかった {t.St331OnceSkip}");
        }

        // ---- 衣（LV-a ／ LV-c ／ LV-d）: 味方（最大HP 100・満タン・ほかの書き手の破片 30）に光 10（＝ 70）が降る → 溢れ 70
        foreach (var (som, tag) in new[] { (UnitCatalog.Som, "規定（LV-a）"), (UnitCatalog.SomLVc332, "LV-c"), (UnitCatalog.SomLVd332, "LV-d") })
        {
            var ctx = T323.Ctx(Formation.Build(front1: Pal, back3: som, back1: UnitCatalog.Susu), Formation.Build(front1: Dummy), out var p, out var e);
            var a = T323.Seat(p, 0); var s = T323.Seat(p, 4); var susu = T323.Seat(p, 3);
            a.SetCounter(StatusKeys.Armor, 30);                         // ほかの書き手の破片（板など）
            int from = ctx.Events.Count;
            SparkTrait.Rain(ctx, s, 10);                                 // 1回目の雨
            int armor1 = a.RawCounter(StatusKeys.Armor), veil1 = a.RawCounter(SparkTrait.VeilKey);
            ctx.ApplyDamage(a, 20, e[0]);                                // 20 を受ける（衣から先に減る）
            int armor2 = a.RawCounter(StatusKeys.Armor), veil2 = a.RawCounter(SparkTrait.VeilKey);
            long scrap0 = T323.Tal(ctx, "susu").ScrapArmor;
            int others = s.RawCounter(SparkTrait.VeilKey) + susu.RawCounter(SparkTrait.VeilKey), othersN = (s.RawCounter(SparkTrait.VeilKey) > 0 ? 1 : 0) + (susu.RawCounter(SparkTrait.VeilKey) > 0 ? 1 : 0);   // ソム ／ ススも満タンなので衣を持つ
            SparkTrait.Rain(ctx, s, 10);                                 // 2回目の雨
            int armor3 = a.RawCounter(StatusKeys.Armor), veil3 = a.RawCounter(SparkTrait.VeilKey);
            var st = T323.Tal(ctx, "som"); var at = T323.Tal(ctx, a.Def.Id);
            long scrap1 = T323.Tal(ctx, "susu").ScrapArmor;
            int fades = ctx.Events.Skip(from).Count(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.VeilFade);
            bool ok = tag switch
            {
                "規定（LV-a）" => armor1 == 100 && veil1 == 70 && armor2 == 80 && veil2 == 50 && armor3 == 150 && veil3 == 120 && st.SparkVeilCapped == 0 && st.Lv332Expired == 0 && fades == 0,
                "LV-c" => armor1 == 40 && veil1 == 10 && armor2 == 20 && veil2 == 0 && armor3 == 40 && veil3 == 20 && st.SparkVeilCapped >= 60 + 50 && st.Lv332Expired == 0,   // 捨てた量にはソム ／ ススの分も入る
                _ => armor1 == 100 && veil1 == 70 && armor2 == 80 && veil2 == 50 && armor3 == 100 && veil3 == 70 && st.Lv332Expired == 50 + others && fades == 1 + othersN && st.SparkVeilCapped == 0,   // 消えた量 ＝ 味方の 50 ＋ ソム ／ ススの衣
            };
            bool book = at.SparkVeilSoaked == (tag == "LV-c" ? 10 : 20) && at.Lv332ArmorLost == 20 && scrap1 == scrap0;   // 消えた衣は「受け止めた」にも「砕けた（瓦礫拾い）」にも数えない
            Expect($"{tag}: 破片 30 の満タンの味方に光 70 が2回降る（間に 20 を受ける）→ {(tag == "LV-c" ? "衣は破片が最大HPの4割（40）まで・超えた分は捨てる（板と合算）" : tag == "LV-d" ? "2回目の雨の頭で前の衣の残り（50）だけが消え、板の 30 は残る・帳簿と合う・瓦礫拾いは拾わない" : "上限なく積もる")}",
                ok && book, $"破片 {armor1} → {armor2} → {armor3}・衣 {veil1} → {veil2} → {veil3}・捨てた {st.SparkVeilCapped}・消えた {st.Lv332Expired}・受け止めた {at.SparkVeilSoaked}・減った {at.Lv332ArmorLost}・瓦礫 {scrap1 - scrap0}");
        }

        // ---- 試遊の台
        {
            var v = Presets.Versions;
            string[] names = { "試遊・感電 ポンの台", "版・LV-c ポンの台", "版・LV-d ポンの台" };
            UnitDef[] soms = { UnitCatalog.Som, UnitCatalog.SomLVc332, UnitCatalog.SomLVd332 };
            var rows = Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).Concat(v).ToArray();
            bool ok = v.Select(r => r.Name).SequenceEqual(names)
                && Enumerable.Range(0, 3).All(i => v[i].F.Occupied().Select(o => o.Def).SequenceEqual(new[] { UnitCatalog.Kata, UnitCatalog.Shiga, UnitCatalog.Tou, UnitCatalog.Kugu, soms[i] }))
                && Presets.Playtest.Length == 17 && Presets.Playtest.Any(r => r.Name == SomTsugiRow) && names.All(n => rows.First(r => r.Name.Contains(n, StringComparison.Ordinal)).Name == n);
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」（規定の駒・段0 入り）＋「版・LV-c ／ LV-d ポンの台」（ソムだけ版）・第331期の「版・ST」の3台は外した・ソム ＋ ツギの台は試遊の「試遊・感電 瓦礫」", ok);
        }

        // ---- 戦の中で: verbose の有無で勝敗 ／ 決着T が同じ（代表台 × ボス ／ 近衛 ／ 大隊 × 3 版 × seed 0..9）
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
            Expect($"代表台 × ボス ／ 近衛 ／ 大隊 × 3 版 × seed 0..9（{n} 戦）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 3f795f8:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第331期（3f795f8）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
