using BattleCore;
using static Common;
using S287 = Shock287Diag;
using T323 = Tou323Diag;

// tou324 の本測定と自己検査（第324期）。表は `tou323` と同じ口（`BoardsWith` ／ `CompareWith` ／ `GridWith` ／ `MemoWith`）を版の列だけ替えて使う。
static partial class Tou324Diag
{
    internal static readonly T323.Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.TouH324, false),
        new("TP-a", "tpa", UnitCatalog.TouTPa, false),
        new("TD-b焼", "tdbb", UnitCatalog.TouTDbB, true),
        new("TP-e", "tpe", UnitCatalog.TouTPe, false),
        new("TP-e′", "tped", UnitCatalog.TouTPeDry, false),
        new("TD-b焼×TP-e", "x", UnitCatalog.TouTDbBTPe, true),
        new("TD-b焼×TP-e′", "xd", UnitCatalog.TouTDbBTPeDry, true),
    };
    static T323.Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static T323.Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();

    static partial void RunMoreImpl(string mode, Func<int, string, string> a, ref bool done)
    {
        done = true;
        switch (mode)
        {
            case "boards": T323.BoardsWith(Pick(a(3, "")), Tag); return;
            case "confirm": Confirm(); return;
            case "compare": T323.CompareWith(Pick(a(3, "")), Tag); return;
            case "grid": T323.GridWith(a(3, "boss"), V(a(4, "tpe")), Tag); return;
            case "memo": T323.MemoWith(a(3, "光の盾"), a(4, "guard"), int.Parse(a(5, "0")), V(a(6, "x")), "tou324"); return;
            case "check": Check(); return;
            default: done = false; return;
        }
    }

    // ---------------------------------------------------------------------------------
    // §4-2 本編の第2 ／ 5波で落ちたセルを別の seed の帯（200..999）で確かめる
    // ---------------------------------------------------------------------------------
    static int Wins(Formation f, S287.Wave w, int from, int n)
    {
        int c = 0;
        Parallel.For(from, from + n, sd =>
        {
            if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) Interlocked.Increment(ref c);
        });
        return c;
    }

    static void Confirm()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §4-2 本編の第2 ／ 5波で落ちたセルの追試（seed 0..199 で規定より下 → seed 200..999 で規定と版を測り直す）");
        Console.WriteLine();
        Console.WriteLine("| 台 × 波 | 版 | 帯A 規定 → 版（差） | 帯B 規定 → 版（差） |");
        Console.WriteLine("|---|---|---|---|");
        int rows = 0;
        foreach (var (bn, bf) in T323.Boards())
            foreach (string wn in new[] { "2", "5" })
            {
                var w = T323.WaveOf(wn);
                int b0 = Wins(T323.Apply(bf, Vers[0]), w, 0, Seeds);
                int? bB = null;
                foreach (var v in Vers.Skip(1))
                {
                    int v0 = Wins(T323.Apply(bf, v), w, 0, Seeds);
                    if (v0 >= b0) continue;
                    bB ??= Wins(T323.Apply(bf, Vers[0]), w, Seeds, 800);
                    int vB = Wins(T323.Apply(bf, v), w, Seeds, 800);
                    double a0 = 100.0 * b0 / Seeds, a1 = 100.0 * v0 / Seeds, c0 = 100.0 * bB.Value / 800, c1 = 100.0 * vB / 800;
                    Console.WriteLine($"| {bn} × {w.Name} | {v.Name} | {F1(a0)} → {F1(a1)}（{Sg(a1 - a0)}） | {F1(c0)} → {F1(c1)}（{Sg(c1 - c0)}） |");
                    rows++;
                }
            }
        if (rows == 0) Console.WriteLine("| （落ちたセルなし） | | | |");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §6 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# tou324 check —— 第324期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var Mate = T323.Mate; var Wall = T323.Wall;

        // ---- 版の定義
        {
            var T = UnitCatalog.TouH324;
            bool tpe = UnitCatalog.TouTPe.Traits.SequenceEqual(UnitCatalog.TouTPa.Traits.Append(TraitId.PowderMarkFoeOnly));
            bool tped = UnitCatalog.TouTPeDry.Traits.SequenceEqual(UnitCatalog.TouTPe.Traits.Append(TraitId.PowderContagionDry));
            bool x = UnitCatalog.TouTDbBTPe.Traits.SequenceEqual(UnitCatalog.TouTDbB.Traits.Concat(new[] { TraitId.PowderContagion, TraitId.PowderMarkFoeOnly }));
            bool xd = UnitCatalog.TouTDbBTPeDry.Traits.SequenceEqual(UnitCatalog.TouTDbBTPe.Traits.Append(TraitId.PowderContagionDry));
            var defs = new[] { UnitCatalog.TouTPe, UnitCatalog.TouTPeDry, UnitCatalog.TouTDbBTPe, UnitCatalog.TouTDbBTPeDry };
            bool stats = defs.All(d => d.Id == "tou" && d.MaxHp == T.MaxHp && d.Attack == T.Attack && d.Speed == T.Speed);
            bool text = UnitCatalog.TouTPe.PlusText == T.PlusText + UnitCatalog.TouContagionFoeText && UnitCatalog.TouTDbBTPe.PlusText == UnitCatalog.TouStackText
                && UnitCatalog.TouTDbBTPe.MinusText == UnitCatalog.TouTDbB.MinusText;
            bool outside = defs.All(d => !UnitCatalog.Everyone.Contains(d)) && UnitCatalog.Everyone.Select(PinTKK324).All(d => !d.Traits.Contains(TraitId.PowderMarkFoeOnly)) && UnitCatalog.All.Count == 52;   // 第325期: 規定のトウが札を持つようになった——第324期の規定（`PinTKK324`）で見る
            bool ret = Vers.Where(v => v.Tou.Traits.Contains(TraitId.PowderDense)).All(v => v.Return) && Vers.Where(v => !v.Tou.Traits.Contains(TraitId.PowderDense)).All(v => !v.Return);
            Expect("版: TP-e ＝ TP-a ＋ 印は敵にだけ ／ TP-e′ ＝ TP-e ＋ 弾けたばかりには移らない ／ 重ねた版 ＝ TD-b焼 ＋ TP-e（′）・カタ ／ クグは返す版・文面（叩き台）・数値は規定のまま・`Everyone` の外",
                tpe && tped && x && xd && stats && text && outside && ret, $"TP-e {tpe}・TP-e′ {tped}・重ね {x}/{xd}・数値 {stats}・文面 {text}・外 {outside}・返す {ret}");
        }

        // ---- TP-e: 漏れ先の味方に印が付かない（帯電はする）・敵の主目標 ／ 舞う先には付く ／ TP-a は味方にも付く
        foreach (var (def, tag, foeOnly) in new[] { (UnitCatalog.TouTPa, "TP-a", false), (UnitCatalog.TouTPe, "TP-e", true) })
        {
            var ctx = T323.Ctx(Formation.Build(center: def, front1: Mate, back3: Mate), Formation.Build(front1: Wall, center: Wall, back1: Wall), out var p, out var e);
            var tou = T323.Seat(p, 2); var af = T323.Seat(p, 0); var ab = T323.Seat(p, 4);
            var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2); var eb = T323.Seat(e, 3);
            ctx.TakeTurn(tou);
            bool foe = ctx.PowderMarked(ef) && ctx.PowderMarked(ec) && ctx.PowderMarked(eb);
            bool ally = ctx.PowderMarked(af) || ctx.PowderMarked(ab);
            bool shocked = T323.Shock(af) > 0 && T323.Shock(ab) > 0;
            Expect($"{tag}: トウの粉で敵の主目標 ／ 舞う先に印が付く・漏れ先の味方は帯電し、印は{(foeOnly ? "付かない" : "付く")}", foe && shocked && ally == !foeOnly,
                $"敵の印 {foe}・味方の印 {ally}・味方の帯電 {shocked}");
            // 味方の前1 を敵の一撃で弾けさせる → 隣のトウ（乾いている）は TP-a なら伝染で帯電・TP-e では帯電しない
            int from = ctx.Events.Count;
            ctx.ApplyDamage(af, 1, ef);
            var t = T323.Tal(ctx, "tou");
            Expect($"{tag}: 漏れ先の味方が弾けたとき、帯電していない隣（トウ）は{(foeOnly ? "帯電しない（味方の側の伝染 0）" : "伝染で帯電する")}",
                T323.Spent(ctx, af, from) == 1 && (T323.Shock(tou) > 0) == !foeOnly && t.TpNewAlly == (foeOnly ? 0 : 1), $"味方 前1 弾けた {T323.Spent(ctx, af, from)}・トウ {T323.Shock(tou)}・伝染 味方 {t.TpNewAlly}");
        }

        // ---- TP-e: 印の付いた敵の放電は帯電していない隣の敵を帯電させる（同じ連鎖では弾けない）
        {
            var ctx = T323.Ctx(Formation.Build(back3: UnitCatalog.TouTPe, front1: Mate), Formation.Build(front1: Wall, center: Wall, back1: Wall), out var p, out var e);
            var tou = T323.Seat(p, 4); var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2); var eb = T323.Seat(e, 3);
            T323.Shock(ec);
            ctx.MarkShock(ec, ec); ctx.MarkShock(eb, eb);
            ctx.PowderFell(tou, ec, PowderRoute.Main);
            int from = ctx.Events.Count;
            ctx.ApplyDamage(ec, 1, T323.Seat(p, 0));
            var t = T323.Tal(ctx, "tou");
            Expect("TP-e: 印の付いた敵（中央）の放電は、帯電していない隣の敵（前1）を帯電させる・その前1 は同じ連鎖では弾けない",
                T323.Shock(ef) > 0 && T323.Spent(ctx, ef, from) == 0 && t.TpNewFoe == 1 && t.TpFreshSkip == 1, $"前1 {T323.Shock(ef)}・弾けた {T323.Spent(ctx, ef, from)}・伝染 {t.TpNewFoe}・同じ連鎖で止めた {t.TpFreshSkip}");
        }

        // ---- TP-e′: 弾けたばかりの駒には移らない ／ TP-e は帯電し直す
        foreach (var (def, tag, dry) in new[] { (UnitCatalog.TouTPe, "TP-e", false), (UnitCatalog.TouTPeDry, "TP-e′", true) })
        {
            var ctx = T323.Ctx(Formation.Build(back3: def, front1: Mate), Formation.Build(front1: Wall, center: Wall, back1: Wall), out var p, out var e);
            var tou = T323.Seat(p, 4); var ef = T323.Seat(e, 0); var ec = T323.Seat(e, 2); var eb = T323.Seat(e, 3);
            ctx.MarkShock(ef, ef); ctx.MarkShock(ec, ec);
            ctx.PowderFell(tou, ef, PowderRoute.Main); ctx.PowderFell(tou, ec, PowderRoute.Main);
            ctx.ApplyDamage(ef, 1, T323.Seat(p, 0));
            var t = T323.Tal(ctx, "tou");
            bool ok = T323.Shock(eb) > 0 && (dry ? T323.Shock(ef) == 0 && t.TpReArm == 0 : T323.Shock(ef) > 0 && t.TpReArm >= 1);
            Expect($"{tag}: 乾いた後1 は伝染で帯電する・中央の放電が届いた前1（同じ連鎖で先に弾けた）は{(dry ? "帯電させない" : "帯電し直す")}", ok, $"前1 {T323.Shock(ef)}・後1 {T323.Shock(eb)}・帯電し直した {t.TpReArm}");
        }

        // ---- 重ねた版: 濃さ ／ 焼 ／ 返す ／ 伝染が同じ盤で同時に働く（盤を直に組む）
        //      味方: 中央 トウ（重ね）・前1 カタ（返す版・帯電）・前3 クグ（返す版・帯電・前3 の敵に組み付き）。敵: 前1（帯電）・前3・中央（帯電）・後1（乾いている）。
        {
            var ctx = T323.Ctx(Formation.Build(center: UnitCatalog.TouTDbBTPe, front1: UnitCatalog.KataTDR, front3: UnitCatalog.KuguTDR),
                               Formation.Build(front1: Wall, front3: Wall, center: Wall, back1: Wall), out var p, out var e);
            var tou = T323.Seat(p, 2); var kata = T323.Seat(p, 0); var kugu = T323.Seat(p, 1);
            var ef = T323.Seat(e, 0); var e3 = T323.Seat(e, 1); var ec = T323.Seat(e, 2); var eb = T323.Seat(e, 3);
            kugu.SetCounter(GrappleTrait.TargetKey, e3.InstanceId + 1);
            ctx.MarkShock(kata, kata); ctx.MarkShock(kugu, kugu); ctx.MarkShock(ef, ef); ctx.MarkShock(e3, e3); ctx.MarkShock(ec, ec);
            ctx.TakeTurn(tou);   // 主目標は前列（前1 か 前3）
            int dKata = ctx.DensityOf(kata), dKugu = ctx.DensityOf(kugu), dE3 = ctx.DensityOf(e3);
            bool allyMark = ctx.PowderMarked(kata) || ctx.PowderMarked(kugu);
            var t1 = T323.Tal(ctx, "tou");
            long gainFoe = t1.TdGainFoe;
            // 濃さの付いた敵の中央を弾けさせる → 濃い放電 ／ 焼 ／ 伝染
            int nC = ctx.DensityOf(ec);
            int from = ctx.Events.Count;
            ctx.ApplyDamage(ec, 1, kata);
            var dis = ctx.Events.Skip(from).Where(x => x.Kind == BattleEventKind.Discharge && x.ActorId == ec.InstanceId).Select(x => x.Amount).ToList();
            int burned = T323.Burned(ctx, ec, from);
            var t2 = T323.Tal(ctx, "tou");
            TurnPropSet(ctx, 2);
            ctx.TakeTurn(kata);   // 雷の1発目の的へカタの濃さを移す
            var kt = T323.Tal(ctx, "kata");
            bool ok = dKata == 1 && dKugu == 0 && dE3 >= 1 && !allyMark && gainFoe >= 1 && nC >= 1
                && dis.Count >= 1 && dis.All(a => a == 8 * (1 + nC)) && burned == 8 * nC && t2.TpNewFoe >= 1 && t2.TpNewAlly == 0
                && kt.TdKataMoved == 1 && ctx.DensityOf(kata) == 0;
            Expect("重ねた版（盤を直に組む）: 漏れで濃くなったカタ（印なし）／ クグの濃さは組み付いた敵へ・敵に濃さ → 弾けて 8 ×（1 ＋ n）の放電 ＋ 本人に 8 × n ＋ 伝染（敵だけ）・カタの雷で濃さが敵へ移る",
                ok, $"カタ {dKata}・クグ {dKugu}・組み付いた敵 {dE3}・味方の印 {allyMark}・中央の濃さ {nC}・放電 {string.Join(",", dis)}・焼 {burned}・伝染 敵 {t2.TpNewFoe} ／ 味方 {t2.TpNewAlly}・カタが移した {kt.TdKataMoved}（後 {ctx.DensityOf(kata)}）");
        }

        // ---- 戦の中で（代表台 × 近衛 ／ ボス／ 本編第5波・seed 0..19）: TP-e 系は味方に伝染 ／ 印を出さない・帳簿と台本が一致・verbose の有無で勝敗 ／ 決着T が同じ
        {
            foreach (var (bn, bf) in new[] { T323.Boards()[0], T323.Boards()[1] })
                foreach (var w in new[] { T323.WaveOf("guard"), T323.WaveOf("boss"), T323.WaveOf("5") })
                    foreach (var v in Vers)
                    {
                        int diff = 0; long tpT = 0, tpEv = 0, allyT = 0, allyEv = 0, allyMark = 0;
                        for (int sd = 0; sd < 20; sd++)
                        {
                            var f = T323.Apply(bf, v);
                            var p1 = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var r1 = BattleEngine.Run(p1, w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                            var t = r1.TallyByUnit["tou"];
                            tpT += t.TpNewFoe + t.TpNewAlly; allyT += t.TpNewAlly;
                            var mine = p1.Select(u => u.InstanceId).ToHashSet();
                            foreach (var x in r1.Events.Where(x => x.Kind == BattleEventKind.ShockGauge))
                            {
                                if (x.Text == ShockGaugeLabels.Contagion) { tpEv++; if (x.TargetId is int ti && mine.Contains(ti)) allyEv++; }
                                if (x.Text == ShockGaugeLabels.PowderMark && x.TargetId is int tm && mine.Contains(tm)) allyMark++;
                            }
                        }
                        bool foeOnly = v.Tou.Traits.Contains(TraitId.PowderMarkFoeOnly);
                        bool ok = diff == 0 && tpT == tpEv && allyT == allyEv && (!foeOnly || allyT == 0 && allyMark == 0);
                        Expect($"{bn} × {w.Name} × {v.Name}（seed 0..19）: 伝染の帳簿と台本が一致{(foeOnly ? "・味方に伝染も印も 0" : "")}・verbose の有無で勝敗 ／ 決着T が同じ",
                            ok, $"伝染 {tpT}（台本 {tpEv}）・うち味方 {allyT}（台本 {allyEv}）・味方の印 {allyMark}・違い {diff}");
                    }
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show HEAD:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string s = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return s; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が HEAD と同じ）", engine == he && traits == ht, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    static void TurnPropSet(BattleContext ctx, int turn) => T323.TurnProp.SetValue(ctx, turn);
}
