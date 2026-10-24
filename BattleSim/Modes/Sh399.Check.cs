using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh399 check —— 第399期の自己検査（指示書 §7 の「新しい器具に最低限入れる」）。
static partial class Sh399Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "b550c45e";   // 第398期の HEAD
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static readonly UnitDef Mate = new() { Id = "mate399", Name = "仲間", MaxHp = 50, Attack = 5, Speed = 8, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeOf(int hp) => new() { Id = "foe399_" + hp, Name = "的", MaxHp = hp, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    /// <summary>味方（席 → 駒）と的 1 体（最大 HP <paramref name="foeHp"/>）。ターン <paramref name="turn"/>。</summary>
    static (BattleContext C, List<UnitState> P, UnitState E) Bench(int turn, int foeHp, params (int Slot, UnitDef D)[] mine)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); foreach (var (s, d) in mine) f[s] = d;
        var g = new Formation(); g[0] = FoeOf(foeHp);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, turn);
        return (ctx, p, e[0]);
    }
    static UnitState U(List<UnitState> p, string id) => p.First(u => u.Def.Id == id);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh399 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef k = UnitCatalog.Sekki, k1 = UnitCatalog.SekkiK1, kh = UnitCatalog.SekkiH398, kb = UnitCatalog.SekkiB;
        UnitDef d = UnitCatalog.Dolga, x1 = UnitCatalog.DolgaX1, x2 = UnitCatalog.DolgaX2, t0 = UnitCatalog.Tomo, l1 = UnitCatalog.TomoL1;

        // ---- 段0 ----
        {
            bool cards = k.Traits.SequenceEqual(k1.Traits) && k.PlusText == k1.PlusText && k.MinusText == k1.MinusText && k.Name == k1.Name && k.Id == "sekki" && k.MaxHp == k1.MaxHp && k.Attack == k1.Attack && k.Speed == k1.Speed && k.Flavor == k1.Flavor
                && !ReferenceEquals(k, k1) && k.PlusText.Contains("（ほとんどはゼリーに溶けて消える）", StringComparison.Ordinal);
            bool old = kh.Traits.SequenceEqual(kb.Traits) && kh.PlusText == kb.PlusText && kh.MinusText == kb.MinusText
                && UnitCatalog.All.Contains(k) && !UnitCatalog.Everyone.Contains(kh)
                && new[] { kb, k1, UnitCatalog.SekkiK2, UnitCatalog.SekkiK12, x1, x2, l1 }.All(v => !UnitCatalog.Everyone.Contains(v))
                && k1.Traits.SequenceEqual(kh.Traits.Append(TraitId.SlimeMelt)) && UnitCatalog.SekkiK2.Traits.SequenceEqual(kh.Traits.Append(TraitId.SlimeHarden));
            bool pins = PinS398(k) == kh && PinS398(d) == d && PinX397(kh) == UnitCatalog.SekkiH397 && PinX397(k) == UnitCatalog.SekkiH397 && PinK396(kh) == UnitCatalog.SekkiH396
                && Pin398(Presets.FourCore397()).Occupied().Any(o => o.Def == kh) && PinSekki397(Pin398(Presets.FourCore397())).Occupied().Any(o => o.Def == UnitCatalog.SekkiH397);
            Expect("段0: 規定のセッキ ＝ 第398期の K1（札 ／ 文面 ／ 名前 ／ 数値・別の定義）・旧は `SekkiH398`（B・`All` の外）・版の駒は `All` ／ `Retired` の外・第398期の版の駒は旧から作ったまま・固定の口 `PinS398` ／ `Pin398`（`PinX397` ／ `PinK396` は H398 も旧へ）",
                cards && old && pins, $"規定のセッキ {string.Join("・", k.Traits)}");
        }
        {
            long cells = 0, diff = 0;
            var bds = new[] { Presets.SlimeLamp396(), Presets.FourCore397() };
            foreach (var f in bds)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++) { cells++; if (!Same(I.Raw(f, w, sd).R, I.Raw(I.Swap(f, "sekki", k1), w, sd).R)) diff++; }
            Expect("段0: 規定は第398期の K1 と同じ動き（セッキのいる試遊の 2 台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..9 で勝敗 ／ T ／ 生存数の差 0）", diff == 0 && cells > 0, $"{cells} 戦・差 {diff}");
        }
        {
            var v = Presets.Versions; var h8 = VersionsH398(); var n8 = h8.Select(r => r.Name).ToList();
            bool ok = !v.Any(r => r.Name == "版・耐える セッキ") && n8.SkipWhile(n => n != "試遊・手番 芯4枚").Take(2).SequenceEqual(new[] { "試遊・手番 芯4枚", "版・耐える セッキ" })
                && h8.First(r => r.Name == "版・耐える セッキ").F.Occupied().Any(o => o.Def == k1) && !h8.Any(r => r.F.Occupied().Any(o => o.Def == k)) && !n8.Any(n => VersionRows399.Contains(n))
                && VersionsH397().Select(r => r.Name).TakeLast(2).SequenceEqual(new[] { "版・膨らむ セッキ", "版・必殺 ドルガ" }) && !VersionsH397().Any(r => r.F.Occupied().Any(o => o.Def == k || o.Def == kh));
            Expect("段0: 試遊の台「版・耐える セッキ」を外した・第398期の一覧は `VersionsH398()`（その台を戻し、セッキは旧）・`VersionsH397()` はそこから", ok, $"`Presets.Versions` {v.Length} 台");
        }
        {
            bool ok = l1.Traits.SequenceEqual(t0.Traits.Append(TraitId.TaillightIdleYield)) && x1.Traits.SequenceEqual(d.Traits.Append(TraitId.DolgaWakeDouble)) && x2.Traits.SequenceEqual(d.Traits.Append(TraitId.DolgaWakeStack))
                && l1.MinusText.Contains("敵が倒れていたか、灯した味方が前のターンに動けなかったら、", StringComparison.Ordinal) && l1.PlusText == t0.PlusText
                && x1.PlusText.EndsWith("休み明けの一撃は倍の重さ", StringComparison.Ordinal) && x2.PlusText.EndsWith("長く止められるほど、目覚めの一撃に力がこもる", StringComparison.Ordinal)
                && new[] { l1, x1, x2 }.All(v => v.MaxHp == (v.Id == "tomo" ? t0 : d).MaxHp && v.Attack == (v.Id == "tomo" ? t0 : d).Attack && v.Speed == (v.Id == "tomo" ? t0 : d).Speed)
                && !UnitCatalog.Everyone.Any(u => u.Traits.Any(t => t is TraitId.TaillightIdleYield or TraitId.DolgaWakeDouble or TraitId.DolgaWakeStack));
            Expect("版の札と文面: L1 ＝ 規定のトモ ＋ 起きた背を押す ／ X1 ＝ 規定のドルガ ＋ 休み明けは倍 ／ X2 ＋ 止められるほど重い・数値は規定のまま・`All` ／ `Retired` の外", ok);
        }

        // ---- L1 ----
        {
            var notes = new List<string>(); bool ok = true;
            (UnitTally T, long Dealt, TurnOutcome O) TomoTurn(UnitDef tomo, string litId, int idleAt, bool stun = false, bool hop = false)
            {
                var (c, p, e) = Bench(4, 100000, (0, Mate), (1, d), (3, tomo));
                var tm = U(p, "tomo"); var lit = U(p, litId);
                tm.SetCounter(TaillightTrait.TargetKey, lit.InstanceId + 1);
                lit.SetCounter(StatusKeys.IdleTurn, idleAt);
                if (stun) lit.SetCounter(StatusKeys.Stun, 1);
                if (hop) c.Yielding = true;
                int h0 = e.Hp; var o = c.TakeTurn(tm);
                return (Tl(c, tm), h0 - e.Hp, o);
            }
            var a = TomoTurn(l1, "dolga", 3);
            bool ya = a.T.R399IdleYields == 1 && a.T.TaillightYields == 1 && a.T.TaillightYieldAttack == 1 && a.Dealt > 0;
            ok &= ya; notes.Add($"灯の相手（ドルガ）が前のターン（T3）に動けなかった → 敵が倒れていなくても譲る（譲り {a.T.TaillightYields}・振った {a.T.TaillightYieldAttack}・与ダメ {a.Dealt}）{(ya ? "" : " ×")}");
            var b0 = TomoTurn(t0, "dolga", 3);
            bool nb = b0.T.TaillightYields == 0 && b0.T.TaillightNoDeath == 1;
            ok &= nb; notes.Add($"規定のトモは譲らない{(nb ? "" : " ×")}");
            var c0 = TomoTurn(l1, "dolga", 4);
            bool nc = c0.T.TaillightYields == 0;
            ok &= nc; notes.Add($"動けなかったのがこのターン（T4）なら譲らない{(nc ? "" : " ×")}");
            var c2 = TomoTurn(l1, "dolga", 2);
            bool nc2 = c2.T.TaillightYields == 0;
            ok &= nc2; notes.Add($"2 ターン前（T2）なら譲らない{(nc2 ? "" : " ×")}");
            var m = TomoTurn(l1, "mate399", 3);
            bool ym = m.T.R399IdleYields == 1 && m.T.TaillightYields == 1;
            ok &= ym; notes.Add($"相手がドルガ以外でも譲る（駒の名前で分岐しない）{(ym ? "" : " ×")}");
            var s = TomoTurn(l1, "dolga", 3, stun: true);
            bool ns = s.T.TaillightYields == 1 && s.T.TaillightYieldStalls == 1 && s.Dealt == 0;
            ok &= ns; notes.Add($"痺れた相手には今どおり振れない（譲った手番が潰れる）{(ns ? "" : " ×")}");
            var hp = TomoTurn(l1, "dolga", 3, hop: true);
            bool nh = hp.T.TaillightYields == 0 && hp.T.TaillightBlockedHop == 1;
            ok &= nh; notes.Add($"1ホップ（譲られた手番の中からは譲らない）{(nh ? "" : " ×")}");
            Expect("L1: 灯の相手が前のターンに動けなかったら、敵が倒れていなくても譲る・このターン ／ 2 ターン前では譲らない・駒の名前で分岐しない・痺れの相手は今どおり振れない・1ホップ（1ターン1回はトモの手番が1回なので今どおり）", ok, string.Join("・", notes));
        }

        // ---- X1 ／ X2 ----
        {
            var notes = new List<string>(); bool ok = true;
            (long Hit1, long Hit2, int RunAfter, int Bonus, UnitTally T) Swing(UnitDef def, int run, int bonus)
            {
                var (c, p, e) = Bench(4, 100000, (1, def));
                var u = U(p, "dolga"); u.AtkBonus = bonus; u.SetCounter(DolgaWakeTrait.RunKey, run);
                int h0 = e.Hp; c.PerformAttack(u); long a1 = h0 - e.Hp;
                int run1 = u.RawCounter(DolgaWakeTrait.RunKey);
                int h1 = e.Hp; c.PerformAttack(u); long a2 = h1 - e.Hp;
                return (a1, a2, run1, u.AtkBonus, Tl(c, u));
            }
            var p0 = Swing(d, 1, 20); var p1 = Swing(x1, 1, 20);
            int heavy = 100000 * 5 / 100;
            bool a = p1.Hit1 - heavy == 2 * (p0.Hit1 - heavy) && p1.Hit2 == p0.Hit2 && p1.RunAfter == 0 && p1.T.R399DoubleHits == 1;
            ok &= a; notes.Add($"X1: 休み明けの最初の一振り {p0.Hit1} → {p1.Hit1}（G2 の足し分 {heavy} を除いて × 2）・2 振り目 {p1.Hit2}（元 {p0.Hit2}）・連続は 0 に{(a ? "" : " ×")}");
            var q1 = Swing(x2, 1, 20);
            bool b = q1.Hit1 == p0.Hit1 && q1.T.R399StackHits == 0 && q1.RunAfter == 0;
            ok &= b; notes.Add($"X2: 1 ターンの休みでは上乗せ 0（{q1.Hit1}）{(b ? "" : " ×")}");
            var q2 = Swing(x2, 2, 20); var q3 = Swing(x2, 3, 20);
            bool c2 = q2.Hit1 - p0.Hit1 == 20 && q3.Hit1 - p0.Hit1 == 40 && q2.Bonus == 20 && q2.RunAfter == 0 && q2.Hit2 == p0.Hit2;
            ok &= c2; notes.Add($"X2: 2 ターン続けて → +20（積まれた力 20 × 1）・3 ターン → +40・`AtkBonus` は 20 のまま・振ると 0 に戻る・2 振り目は元{(c2 ? "" : " ×")}");
            var q0 = Swing(x2, 3, 0);
            bool c0 = q0.Hit1 == Swing(d, 0, 0).Hit1;
            ok &= c0; notes.Add($"X2: 積まれた力が 0 なら上乗せ 0{(c0 ? "" : " ×")}");
            // 連続の数え方: 手番の枠で数える（休み番 → 痺れ ＝ 2）
            var (cc, pp, _) = Bench(3, 100000, (1, x2));
            var du = U(pp, "dolga");
            var o3 = cc.TakeTurn(du); int r3 = du.RawCounter(DolgaWakeTrait.RunKey);
            cc.TakeTurn(du); int r3b = du.RawCounter(DolgaWakeTrait.RunKey);   // 同じターンの二度目は数えない
            TurnField.SetValue(cc, 4); du.SetCounter(StatusKeys.Stun, 1);
            cc.TakeTurn(du); int r4 = du.RawCounter(DolgaWakeTrait.RunKey);
            TurnField.SetValue(cc, 6); du.SetCounter(StatusKeys.Stun, 1);
            cc.TakeTurn(du); int r6 = du.RawCounter(DolgaWakeTrait.RunKey);   // 間が空いたら 1 から
            bool cnt = o3 == TurnOutcome.Stalled && r3 == 1 && r3b == 1 && r4 == 2 && r6 == 1;
            ok &= cnt; notes.Add($"数え方: 休み番 T3 → 1・同じターンの二度目は数えない・T4 に痺れ → 2・T5 を飛ばして T6 に痺れ → 1{(cnt ? "" : $" × ({o3}・{r3}・{r3b}・{r4}・{r6})")}");
            Expect("X1: 休み明けの最初の一振りが 2 倍・2 振り目からは元 ／ X2: 1 ターンの休みでは上乗せ 0・2 ターン続けて動けなければ積まれた力を 1 回ぶん上乗せ・振ると 0 に戻る・`AtkBonus` は動かない", ok, string.Join("・", notes));
        }
        {
            // ボスの名前 ／ 種類で分岐していない: L1 ／ X の口のソースにボス ／ 勇者の語が無い
            string eng = File.ReadAllText("BattleCore/BattleEngine.cs"), tr = File.ReadAllText("BattleCore/Traits.cs");
            int a = eng.IndexOf("void NoteIdle399(", StringComparison.Ordinal), b = eng.IndexOf("int HeavyAdd397(", a, StringComparison.Ordinal);
            int c = eng.IndexOf("// 第399期（X1 ／ X2・休み明けの一撃）", StringComparison.Ordinal), e2 = eng.IndexOf("if (heavy) first += HeavyAdd397", c, StringComparison.Ordinal);
            int f = tr.IndexOf("// 第399期（トモの版 L1・`TaillightIdleYield`）", StringComparison.Ordinal), h = tr.IndexOf("if (window) t.TaillightSaw2++;", f, StringComparison.Ordinal);
            string body = a > 0 && b > a && c > 0 && e2 > c && f > 0 && h > f ? eng[a..b] + eng[c..e2] + tr[f..h] : "";
            bool noBoss = body.Length > 0 && !body.Contains("Boss", StringComparison.Ordinal) && !body.Contains("勇者", StringComparison.Ordinal) && !body.Contains("\"dolga\"", StringComparison.Ordinal);
            Expect("ボスの名前 ／ 種類で分岐していない（L1 ／ X1 ／ X2 の口のソースに「Boss ／ 勇者 ／ 駒の Id」が無い）", noBoss, $"{body.Length} 文字");
        }

        // ---- 規定の compare ----
        {
            var map = S390.BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var cc)) { miss++; continue; }
                for (int w = 0; w < 5; w++) if (cc[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            Expect("規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md`（段0 で再生成）と一致", diff == 0 && miss == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 替えた台 ／ 素体は器具の中だけ・試遊の台 ----
        {
            var vers = new[] { l1, x1, x2 };
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions).SelectMany(r => r.F.Occupied()).Select(o => o.Def).ToList();
            bool inside = !everywhere.Any(x => x.Id.EndsWith("_plain392", StringComparison.Ordinal));
            var verRows = Presets.Versions.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool rows = verRows.SequenceEqual(VersionRows399) && Presets.Versions[^1].Name == VersionRows399[0]
                && !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => vers.Contains(o.Def)));
            Expect("替えた台 ／ 素体は器具の中だけ・版の駒が居る試遊の台は「版・目覚め ドルガ」（`Presets.Versions` の末尾）だけ", inside && rows, string.Join("、", verRows));
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool Has2(TraitId id, string must) => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{id}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*{must}""");
            var miss = new List<string>();
            if (!Has2(TraitId.SlimeMelt, "第399期から規定（セッキ）")) miss.Add("SlimeMelt");
            if (!Has2(TraitId.SlimeSwell, "第398期から規定（セッキ）")) miss.Add("SlimeSwell");
            foreach (var id in new[] { TraitId.TaillightIdleYield, TraitId.DolgaWakeDouble, TraitId.DolgaWakeStack }) if (!Has2(id, "第399期[^\"]*保持者 0 枚")) miss.Add(id.ToString());
            Expect("`checkup` の札の分類表: 規定になった K1 の札は「第399期から規定（セッキ）」・版の札 3 枚がある（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            var jobs = BoardList().SelectMany(b => Cols.Select(c => Apply(b.F, c.Tomo, c.Dolga)));
            foreach (var f in jobs)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 2; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（7 台 × 列 7 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, tr0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1n = Now("BattleCore/BattleEngine.cs", "PickOne("), tr1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第398期の HEAD（{HeadSha}）と同じ）", e0 == e1n && tr0 == tr1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1n}・traits {tr0} → {tr1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
