using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh398 check —— 第398期の自己検査（指示書 §8 の「新しい器具に最低限入れる」）。
static partial class Sh398Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "34746591";   // 第397期の HEAD
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static readonly UnitDef Mate = new() { Id = "mate398", Name = "仲間", MaxHp = 50, Attack = 5, Speed = 8, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeOf(int hp) => new() { Id = "foe398_" + hp, Name = "的", MaxHp = hp, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    /// <summary>味方（後1 に <paramref name="mine"/>・前1 ／ 前3 に仲間）と的（<paramref name="foes"/> の最大 HP の数だけ）。ターン <paramref name="turn"/>。</summary>
    static (BattleContext C, UnitState U, UnitState M, List<UnitState> E) Bench(UnitDef mine, int turn, params int[] foes)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); f[3] = mine; f[0] = Mate; f[1] = Mate;
        var g = new Formation(); for (int i = 0; i < foes.Length; i++) g[i] = FoeOf(foes[i]);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, turn);
        return (ctx, p.First(u => u.Def.Id == mine.Id), p.First(u => u.Def.Id == "mate398"), e);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh398 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef k = UnitCatalog.SekkiH398,   /* 第399期: 第398期の規定は旧へ */ kb = UnitCatalog.SekkiB, kh = UnitCatalog.SekkiH397, s2h = UnitCatalog.SekkiS2h, k1 = UnitCatalog.SekkiK1, k2 = UnitCatalog.SekkiK2, k12 = UnitCatalog.SekkiK12;
        UnitDef d = UnitCatalog.Dolga, g12 = UnitCatalog.DolgaG12, dh = UnitCatalog.DolgaH397, e1 = UnitCatalog.DolgaE1;

        // ---- 段0 ----
        {
            bool cards = k.Traits.SequenceEqual(kb.Traits) && k.PlusText == kb.PlusText && k.MinusText == kb.MinusText && k.Name == kb.Name && k.Id == "sekki" && k.MaxHp == 70 && k.Attack == kb.Attack && k.Speed == kb.Speed && k.Flavor == kb.Flavor
                && d.Traits.SequenceEqual(g12.Traits) && d.PlusText == g12.PlusText && d.MinusText == g12.MinusText && d.Name == g12.Name && d.Id == "dolga" && d.MaxHp == g12.MaxHp && d.Attack == g12.Attack && d.Speed == g12.Speed && d.Pattern == g12.Pattern
                && !ReferenceEquals(k, kb) && !ReferenceEquals(d, g12);
            bool old = kh.Traits.SequenceEqual(s2h.Traits) && kh.PlusText == s2h.PlusText && kh.MinusText == s2h.MinusText && kh.Name == "包みのセッキ"
                && dh.Traits.SequenceEqual(e1.Traits) && dh.PlusText == e1.PlusText && dh.MinusText == e1.MinusText
                && UnitCatalog.All.Contains(UnitCatalog.Sekki) && !UnitCatalog.Everyone.Contains(k) && UnitCatalog.All.Contains(d) && !UnitCatalog.Everyone.Contains(kh) && !UnitCatalog.Everyone.Contains(dh)
                && new[] { kb, UnitCatalog.SekkiR, s2h, UnitCatalog.SekkiS1, UnitCatalog.SekkiS2, k1, k2, k12, g12, UnitCatalog.DolgaG1, UnitCatalog.DolgaG2, e1 }.All(v => !UnitCatalog.Everyone.Contains(v));
            bool pins = PinX397(k) == kh && PinX397(d) == dh && PinX397(UnitCatalog.Tomo) == UnitCatalog.TomoH399 /* 第400期: トモも旧へ */ && PinK396(kh) == UnitCatalog.SekkiH396 && PinK396(k) == UnitCatalog.SekkiH396
                && PinD395(dh) == UnitCatalog.DolgaH395 && PinD395(d) == UnitCatalog.DolgaH395 && PinT393(d) == UnitCatalog.DolgaH395 && PinT393(k) == UnitCatalog.SekkiH396
                && Pin397(Presets.FourCore397()).Occupied().Count(o => o.Def == kh || o.Def == dh) == 2;
            Expect("段0: 規定のセッキ ＝ 第397期の B ／ 規定のドルガ ＝ 第397期の G12（札 ／ 文面 ／ 名前 ／ 数値・別の定義）・旧は `SekkiH397`（S2h）／ `DolgaH397`（E1・`All` の外）・版の駒は `All` ／ `Retired` の外・固定の口 `PinX397` ／ `Pin397`（`PinK396` ／ `PinD395` は H397 も第396 ／ 395期までの旧へ）",
                cards && old && pins, $"規定のセッキ {string.Join("・", k.Traits)}・ドルガ {string.Join("・", d.Traits)}");
        }
        {
            // 第396〜397期の版の駒は旧から作ったまま（数値は旧の規定と同じ・札は第397期の定義のまま）
            bool ok = new[] { UnitCatalog.SekkiS1, UnitCatalog.SekkiS2, s2h, kb, UnitCatalog.SekkiR }.All(v => v.MaxHp == UnitCatalog.SekkiH396.MaxHp && v.Attack == UnitCatalog.SekkiH396.Attack && v.Speed == UnitCatalog.SekkiH396.Speed)
                && kb.Traits.SequenceEqual(new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow, TraitId.SlimeDeep, TraitId.SlimeSwell }) && UnitCatalog.SekkiR.Traits.SequenceEqual(new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow, TraitId.SlimeHaven })
                && new[] { UnitCatalog.DolgaG1, UnitCatalog.DolgaG2, g12, e1, UnitCatalog.DolgaE2, UnitCatalog.DolgaE12 }.All(v => v.MaxHp == UnitCatalog.DolgaH395.MaxHp && v.Attack == UnitCatalog.DolgaH395.Attack && v.Speed == UnitCatalog.DolgaH395.Speed)
                && g12.Traits.SequenceEqual(e1.Traits.Append(TraitId.DolgaFocus).Append(TraitId.DolgaHeavy));
            Expect("段0: 第396〜397期の版の駒（S1 ／ S2 ／ S2h ／ B ／ R ・ E1 ／ E2 ／ E12 ／ G1 ／ G2 ／ G12）は旧の数値と第397期の札のまま", ok);
        }
        {
            long cells = 0, diff = 0;
            var bds = new[] { Presets.SlimeLamp396(), Presets.FourCore397(), Presets.BindTempo395(), Presets.TempoLamp393() }.Select(Pin398).ToArray();   // 第399期: セッキは第398期の規定（B）
            foreach (var f in bds)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++) { cells++; if (!Same(I.Raw(f, w, sd).R, I.Raw(Swap2(f, Has(f, "sekki") ? kb : k, Has(f, "dolga") ? g12 : d), w, sd).R)) diff++; }
            Expect("段0: 規定は第397期の B ／ G12 と同じ動き（試遊の 4 台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..9 で勝敗 ／ T ／ 生存数の差 0）", diff == 0 && cells > 0, $"{cells} 戦・差 {diff}");
        }
        {
            var v = VersionsH398(); var h7 = VersionsH397(); var names7 = h7.Select(r => r.Name).ToList();   // 第399期: 第398期の一覧
            var four = v.FirstOrDefault(r => r.Name == "試遊・手番 芯4枚");
            bool ok = four.F is not null && four.F.Occupied().Any(o => o.Def == d) && four.F.Occupied().Any(o => o.Def == k)
                && !v.Any(r => r.Name is "版・膨らむ セッキ" or "版・必殺 ドルガ")
                && names7.SkipWhile(n => n != "試遊・手番 包み").SequenceEqual(new[] { "試遊・手番 包み", "版・膨らむ セッキ", "版・必殺 ドルガ" })
                && h7.First(r => r.Name == "版・膨らむ セッキ").F.Occupied().Any(o => o.Def == kb) && h7.First(r => r.Name == "版・必殺 ドルガ").F.Occupied().Any(o => o.Def == g12)
                && !h7.Any(r => r.F.Occupied().Any(o => o.Def == k || o.Def == d)) && !names7.Any(n => VersionRows398.Contains(n))
                && VersionsH396().Select(r => r.Name).Last() == "版・包み セッキ";
            Expect("段0: 試遊の台「版・膨らむ セッキ」を外し、「版・必殺 ドルガ」→「試遊・手番 芯4枚」（規定のドルガ）・第397期の一覧は `VersionsH397()`（2 台と名前を戻し、セッキ ／ ドルガは旧）・`VersionsH396()` はそこから", ok, $"`Presets.Versions` {v.Length} 台");
        }
        {
            bool ok = k1.Traits.SequenceEqual(k.Traits.Append(TraitId.SlimeMelt)) && k2.Traits.SequenceEqual(k.Traits.Append(TraitId.SlimeHarden)) && k12.Traits.SequenceEqual(k.Traits.Append(TraitId.SlimeMelt).Append(TraitId.SlimeHarden))
                && new[] { k1, k2, k12 }.All(x => x.Id == "sekki" && x.Name == k.Name && x.MaxHp == k.MaxHp && x.Attack == k.Attack && x.Speed == k.Speed && x.MinusText == k.MinusText)
                && k1.PlusText.Contains("衝撃の 5 割を吸い取る（ほとんどはゼリーに溶けて消える）", StringComparison.Ordinal) && !k2.PlusText.Contains("溶けて", StringComparison.Ordinal)
                && k2.PlusText.EndsWith("溜めた衝撃でゼリーが固まり、自分への攻撃が軽くなる。弾き返すと柔らかく戻る", StringComparison.Ordinal) && k12.PlusText.Contains("溶けて消える", StringComparison.Ordinal) && k12.PlusText.EndsWith("弾き返すと柔らかく戻る", StringComparison.Ordinal)
                && !UnitCatalog.Everyone.Where(u => u != UnitCatalog.Sekki).Any(u => u.Traits.Any(t => t is TraitId.SlimeMelt or TraitId.SlimeHarden));   // 第399期: 規定のセッキは K1 の札を持つ
            Expect("版の札と文面: K1 ＝ 規定 ＋ ゼリーが消す ／ K2 ＋ 固まる ／ K12 ＝ 両方・数値は規定のまま・`All` ／ `Retired` の外", ok);
        }

        // ---- K1 ----
        {
            var (c, s, m, e) = Bench(k1, 1, 1000);
            int m0 = m.Hp;
            c.ApplyDamage(m, 40, e[0], pattern: AttackPattern.Sweep);   // 5 割 ＝ 20 を吸う → 膨らむ 4・HP で受ける 2 割 ＝ 4
            bool a = m0 - m.Hp == 20 && s.MaxHp == 74 && s.Hp == 70 && s.RawCounter(SlimeTrait.StoreKey) == 20 && Tl(c, s).R398MeltSaved == 6 && Tl(c, s).R396WrapHp == 4;
            var (c0, s0, m00, e0) = Bench(k, 1, 1000); c0.ApplyDamage(m00, 40, e0[0], pattern: AttackPattern.Sweep);
            bool plain = s0.MaxHp == 74 && s0.Hp == 64;
            var (c3, s3, m3, e3) = Bench(k1, 1, 1000); c3.ApplyDamage(m3, 9, e3[0], pattern: AttackPattern.All);   // 吸う 4 → 2 割は 0（切り捨て）
            bool fl = s3.Hp == s3.MaxHp && s3.RawCounter(SlimeTrait.StoreKey) == 4;
            Expect("K1: 包むとき HP で受けるのは吸った量の 2 割（薙ぎ 40 → 吸う 20 → HP 4・規定は 10）・切り捨て（吸う 4 → 0）・帳簿は吸った量の全部", a && plain && fl,
                $"K1 HP {s.Hp}/{s.MaxHp}・規定 {s0.Hp}/{s0.MaxHp}・消した {Tl(c, s).R398MeltSaved}");
        }

        // ---- K2 ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c, s, m, e) = Bench(k2, 1, 1000);
            s.SetCounter(SlimeTrait.StoreKey, 35);
            int h0 = s.Hp; c.ApplyDamage(s, 10, e[0], pattern: AttackPattern.Single);
            bool a = h0 - s.Hp == 7 && Tl(c, s).R398HardenSaved == 3;
            ok &= a; notes.Add($"帳簿 35 で単体 10 → {h0 - s.Hp}（−3）{(a ? "" : " ×")}");
            int m0 = m.Hp; h0 = s.Hp; c.ApplyDamage(m, 40, e[0], pattern: AttackPattern.Sweep);   // 吸う 20 → 帳簿 55（先に積む）→ 膨らむ 4・HP で受ける 10 − 5 ＝ 5
            bool b = m0 - m.Hp == 20 && (h0 + 4) - s.Hp == 5 && s.RawCounter(SlimeTrait.StoreKey) == 55;
            ok &= b; notes.Add($"包みで受ける分にも（帳簿は吸った分を先に積んで 55・10 → {(h0 + 4) - s.Hp}）{(b ? "" : " ×")}");
            s.SetCounter(SlimeTrait.StoreKey, 200); h0 = s.Hp; c.ApplyDamage(s, 5, e[0], pattern: AttackPattern.Single);
            bool fl = s.Hp == h0;
            ok &= fl; notes.Add($"下限 0（帳簿 200 で 5 → 0）{(fl ? "" : " ×")}");
            h0 = s.Hp; c.ApplyDamage(s, 6, s, levy: true);
            bool lv = h0 - s.Hp == 6;
            ok &= lv; notes.Add($"徴収は軽くならない{(lv ? "" : " ×")}");
            c.PerformAttack(s);   // 帳簿が残っている → 弾き返して 0
            h0 = s.Hp; c.ApplyDamage(s, 10, e[0], pattern: AttackPattern.Single);
            bool back = s.RawCounter(SlimeTrait.StoreKey) == 0 && h0 - s.Hp == 10 && Tl(c, s).R396Bursts == 1;
            ok &= back; notes.Add($"弾き返して帳簿 0 → 元に戻る（10 → {h0 - s.Hp}）{(back ? "" : " ×")}");
            var (c0, s0, _, e0) = Bench(k, 1, 1000); s0.SetCounter(SlimeTrait.StoreKey, 35); int p0 = s0.Hp; c0.ApplyDamage(s0, 10, e0[0], pattern: AttackPattern.Single);
            bool plain = p0 - s0.Hp == 10;
            ok &= plain; notes.Add($"規定は軽くならない{(plain ? "" : " ×")}");
            Expect("K2: セッキが受ける一撃が帳簿の 1 割だけ軽い（単体にも・包みで受ける分にも）・下限 0・徴収は除く・弾き返すと帳簿 0 で元に戻る", ok, string.Join("・", notes));
        }
        {
            // ボスの名前 ／ 種類で分岐していない: K1 ／ K2 の口のソースにボス ／ 勇者の語が無い
            string src = File.ReadAllText("BattleCore/BattleEngine.cs");
            int a = src.IndexOf("// 第398期（セッキの版 K2 ／ K12・`SlimeHarden`）", StringComparison.Ordinal), b = src.IndexOf("if (amount <= 0) return;", a, StringComparison.Ordinal);
            int c = src.IndexOf("// 第398期（K1 ／ K12・`SlimeMelt`）", StringComparison.Ordinal), e = src.IndexOf("if (hpPart > 0)", c, StringComparison.Ordinal);
            string body = a > 0 && b > a && c > 0 && e > c ? src[a..b] + src[c..e] : "";
            bool noBoss = body.Length > 0 && !body.Contains("Boss", StringComparison.Ordinal) && !body.Contains("勇者", StringComparison.Ordinal) && !body.Contains("IsBoss", StringComparison.Ordinal);
            Expect("ボスの名前 ／ 種類で分岐していない（K1 ／ K2 の口のソースに「Boss ／ 勇者」が無い・条件は「範囲の一撃」「帳簿」だけ）", noBoss, $"{body.Length} 文字");
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
            var vers = new[] { k1, k2, k12 };
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(VersionsH398()).SelectMany(r => r.F.Occupied()).Select(o => o.Def).ToList();
            bool inside = !everywhere.Any(x => x.Id.EndsWith("_plain392", StringComparison.Ordinal));
            var v8 = VersionsH398();   // 第399期: 第398期の一覧
            var verRows = v8.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool rows = verRows.SequenceEqual(VersionRows398) && v8[^1].Name == VersionRows398[0] && v8[^1].F.Occupied().Count(o => vers.Contains(o.Def)) == 1
                && !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => vers.Contains(o.Def)));
            Expect("替えた台 ／ 素体は器具の中だけ・版の駒が居る試遊の台は「版・耐える セッキ」（`Presets.Versions` の末尾）だけ", inside && rows, string.Join("、", verRows));
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool Has2(TraitId id, string must) => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{id}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*{must}""");
            var miss = new List<string>();
            foreach (var id in new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow, TraitId.SlimeDeep }) if (!Has2(id, "第397期から規定（セッキ）")) miss.Add(id.ToString());
            if (!Has2(TraitId.SlimeSwell, "第398期から規定（セッキ）")) miss.Add("SlimeSwell");
            foreach (var id in new[] { TraitId.DolgaFocus, TraitId.DolgaHeavy }) if (!Has2(id, "第398期から規定（ドルガ）")) miss.Add(id.ToString());
            foreach (var id in new[] { TraitId.SluggishShift, TraitId.DolgaWall, TraitId.DolgaWallRetreat }) if (!Has2(id, "規定（ドルガ）")) miss.Add(id.ToString());
            foreach (var id in new[] { TraitId.SlimeMelt, TraitId.SlimeHarden }) if (!Has2(id, "第398期[^\"]*(保持者 0 枚|第399期から規定（[^）]*）)")) miss.Add(id.ToString());
            Expect("`checkup` の札の分類表: 規定になった B の札は「第398期から規定（セッキ）」・G12 の札 2 枚は「第398期から規定（ドルガ）」・版の札 2 枚がある（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            var jobs = BoardList().SelectMany(b => Cols.Select(v => Put(b.F, Has(b.F, "sekki") ? SlotOf(b.F, "sekki") : 3, v.D)));
            foreach (var f in jobs)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 2; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（6 台 × 列 5 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, tr0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1n = Now("BattleCore/BattleEngine.cs", "PickOne("), tr1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第397期の HEAD（{HeadSha}）と同じ）", e0 == e1n && tr0 == tr1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1n}・traits {tr0} → {tr1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
