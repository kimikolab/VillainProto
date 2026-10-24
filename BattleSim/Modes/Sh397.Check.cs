using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh397 check —— 第397期の自己検査（指示書 §7 の「新しい器具に最低限入れる」）。
static partial class Sh397Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "4220eab3";   // 第396期の HEAD
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static readonly UnitDef Mate = new() { Id = "mate397", Name = "仲間", MaxHp = 50, Attack = 5, Speed = 8, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeOf(int hp) => new() { Id = "foe397_" + hp, Name = "的", MaxHp = hp, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

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
        return (ctx, p.First(u => u.Def.Id == mine.Id), p.First(u => u.Def.Id == "mate397"), e);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh397 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef k = UnitCatalog.Sekki, kh = UnitCatalog.SekkiH396, s2h = UnitCatalog.SekkiS2h, kb = UnitCatalog.SekkiB, kr = UnitCatalog.SekkiR;
        UnitDef d = UnitCatalog.Dolga, g1 = UnitCatalog.DolgaG1, g2 = UnitCatalog.DolgaG2, g12 = UnitCatalog.DolgaG12;

        // ---- 段0 ----
        {
            bool cards = k.Traits.SequenceEqual(s2h.Traits) && k.Traits.SequenceEqual(new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow, TraitId.SlimeDeep })
                && k.PlusText == s2h.PlusText && k.MinusText == s2h.MinusText && k.Name == "包みのセッキ" && k.Id == "sekki" && k.MaxHp == 70 && k.Attack == 2 && k.Speed == 2
                && UnitCatalog.All.Contains(k) && !UnitCatalog.All.Contains(kh) && !UnitCatalog.All.Contains(s2h) && kh.Traits.SequenceEqual(new[] { TraitId.RearGuard, TraitId.Rage }) && kh.Name == "後備えのセッキ"
                && new[] { UnitCatalog.SekkiS1, UnitCatalog.SekkiS2, s2h, kb, kr }.All(v => v.MaxHp == kh.MaxHp && v.Attack == kh.Attack && v.Speed == kh.Speed && !UnitCatalog.Everyone.Contains(v));
            bool pins = PinK396(k) == kh && PinK396(s2h) == s2h && PinD395(k) == kh && PinT393(k) == kh && PinT393(d) == UnitCatalog.DolgaH395 && Pin396(Presets.SlimeLamp396()).Occupied().Any(o => o.Def == kh);
            Expect("段0: 規定のセッキ ＝ 第396期の S2h（札 ／ 文面 ／ 名前「包みのセッキ」／ 数値）・旧は `SekkiH396`（後備え ＋ 憤怒・`All` の外）・第396期の版の駒は旧の数値から作ったまま・固定の口 `PinK396` ／ `Pin396`（`PinD395` の終端）", cards && pins, $"規定 {string.Join("・", k.Traits)}");
        }
        {
            long cells = 0, diff = 0;
            foreach (var bd in SBoardList())
            {
                int ks = Has(bd.F, "sekki") ? SlotOf(bd.F, "sekki") : 2;
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++) { cells++; if (!Same(I.Raw(Put(bd.F, ks, k), w, sd).R, I.Raw(Put(bd.F, ks, s2h), w, sd).R)) diff++; }
            }
            Expect("段0: 規定のセッキは第396期の S2h と同じ動き（3 台 × 4 物差し × seed 0..9 で勝敗 ／ T ／ 生存数の差 0）", diff == 0 && cells > 0, $"{cells} 戦・差 {diff}");
        }
        {
            var v = Presets.Versions.Select(r => r.Name).ToList(); var h6 = VersionsH396();
            bool ok = v.Contains("試遊・手番 包み") && !v.Contains("版・包み セッキ") && Presets.Versions.First(r => r.Name == "試遊・手番 包み").F.Occupied().Any(o => o.Def == k)
                && h6[^1].Name == "版・包み セッキ" && h6[^1].F.Occupied().Any(o => o.Def == s2h) && !h6.Any(r => r.F.Occupied().Any(o => o.Def == k)) && !h6.Any(r => VersionRows397.Contains(r.Name))
                && VersionsH395().Select(r => r.Name).SequenceEqual(h6.Select(r => r.Name).SkipLast(1).Take(h6.Length - 2).Concat(new[] { "版・壁 ドルガ", "版・壁 E" }));
            Expect("段0: 「版・包み セッキ」→「試遊・手番 包み」（規定のセッキ）・第396期の一覧は `VersionsH396()`（名前と S2h を戻し、ほかのセッキは旧）・`VersionsH395()` はそこから", ok, $"`Presets.Versions` {v.Count} 台");
        }

        // ---- 版の札と文面 ----
        {
            bool ok = kb.Traits.SequenceEqual(new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow, TraitId.SlimeDeep, TraitId.SlimeSwell }) && kr.Traits.SequenceEqual(new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow, TraitId.SlimeHaven })
                && kb.PlusText.EndsWith("吸うほど体が膨らむ", StringComparison.Ordinal) && kr.PlusText.StartsWith("範囲の攻撃が来た瞬間、ゼリーの体で仲間を丸ごと包み", StringComparison.Ordinal) && kr.MinusText.EndsWith("包まれた仲間はゼリーに絡まり、次の手番を失う", StringComparison.Ordinal)
                && new[] { g1, g2, g12 }.All(x => x.Traits.Take(d.Traits.Count).SequenceEqual(d.Traits) && x.MaxHp == d.MaxHp && x.Attack == d.Attack && x.Speed == d.Speed && !UnitCatalog.Everyone.Contains(x))
                && g1.Traits.Skip(d.Traits.Count).SequenceEqual(new[] { TraitId.DolgaFocus }) && g2.Traits.Skip(d.Traits.Count).SequenceEqual(new[] { TraitId.DolgaHeavy }) && g12.Traits.Skip(d.Traits.Count).SequenceEqual(new[] { TraitId.DolgaFocus, TraitId.DolgaHeavy })
                && g1.PlusText.Contains("敵全体を薙ぎ払う（敵が少ないほど、一体に力が集まる）", StringComparison.Ordinal) && g2.PlusText.EndsWith("巨体の一撃は、大きな相手ほど深く響く", StringComparison.Ordinal)
                && !UnitCatalog.Everyone.Any(u => u.Traits.Any(t => t is TraitId.SlimeSwell or TraitId.SlimeHaven or TraitId.DolgaFocus or TraitId.DolgaHeavy));
            string units = File.Exists("docs/units.md") ? File.ReadAllText("docs/units.md") : "", head = S390.GitShow($"{HeadSha}:docs/units.md");
            string Row(string s) => s.Split('\n').FirstOrDefault(l => l.StartsWith("| **のろまの巨兵ドルガ** |", StringComparison.Ordinal))?.TrimEnd('\r') ?? "";
            bool same = Row(units) != "" && Row(units) == Row(head);
            Expect("版の札と文面: B ＝ 規定 ＋ 膨らむ ／ R ＝ 包む ＋ 溢れ ＋ 避難所（深く包むの代わり）・G1 ＝ 規定のドルガ ＋ 力が一点に ／ G2 ＋ 大物ほど重い ／ G12 ＝ 両方・`All` ／ `Retired` の外・規定のドルガは今どおり（`docs/units.md` の行が第396期の HEAD と同じ）", ok && same, $"units.md のドルガの行 {(same ? "同じ" : "違う")}");
        }

        // ---- B ----
        {
            var (c, s, m, e) = Bench(kb, 1, 1000);
            int m0 = m.Hp;
            c.ApplyDamage(m, 40, e[0], pattern: AttackPattern.Sweep);   // 5 割 ＝ 20 を吸う → 膨らむ 4・HP で受ける 10
            bool g1ok = m0 - m.Hp == 20 && s.MaxHp == 74 && s.Hp == 64 && s.RawCounter(SlimeTrait.StoreKey) == 20 && Tl(c, s).R397SwellGain == 4;
            Trait Sl(UnitState u) => u.Traits.First(t => t.Id == TraitId.SlimeWrap);
            s.SetCounter(SlimeTrait.StoreKey, 69); bool lo = Sl(s).CanAct(c, s, ActionKind.Attack);
            s.SetCounter(SlimeTrait.StoreKey, 70); bool hi = Sl(s).CanAct(c, s, ActionKind.Attack);
            var (c0, s0, m00, e0) = Bench(k, 1, 1000); c0.ApplyDamage(m00, 40, e0[0], pattern: AttackPattern.Sweep);
            bool plain = s0.MaxHp == 70 && s0.Hp == 60;
            Expect("B: 吸った量の 2 割だけ最大 HP と HP が増える（薙ぎ 40 → 吸う 20 → 膨らむ 4・HP 70 → 74 − 10 ＝ 64）・溢れの線は素の 70 のまま（最大 HP 74 で帳簿 69 は動かない ／ 70 は動く）・規定は膨らまない", g1ok && !lo && hi && plain,
                $"最大 HP {s.MaxHp}・HP {s.Hp}・帳簿 69 {(lo ? "動く" : "動かない")} ／ 70 {(hi ? "動く" : "動かない")}");
        }

        // ---- R ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c, s, m, e) = Bench(kr, 1, 1000);
            int m0 = m.Hp, s0 = s.Hp;
            c.ApplyDamage(m, 40, e[0], pattern: AttackPattern.Sweep);
            bool a = m0 - m.Hp == 0 && s0 - s.Hp == 8 && s.RawCounter(SlimeTrait.StoreKey) == 40 && m.RawCounter(SlimeTrait.HeldKey) == 1;
            ok &= a; notes.Add($"薙ぎ 40 → 仲間 {m0 - m.Hp}・セッキ HP {s0 - s.Hp}（2 割）・帳簿 {s.RawCounter(SlimeTrait.StoreKey)}・印 {m.RawCounter(SlimeTrait.HeldKey)}{(a ? "" : " ×")}");
            c.ApplyDamage(m, 40, e[0], pattern: AttackPattern.All);
            bool b = m.RawCounter(SlimeTrait.HeldKey) == 1 && Tl(c, s).R397HavenHolds == 1;
            ok &= b; notes.Add($"同じターンにもう一度包まれても印は 1（1体1ターン1回）{(b ? "" : " ×")}");
            int mh = m.Hp; c.ApplyDamage(m, 10, e[0], pattern: AttackPattern.Single);
            bool sg = mh - m.Hp == 10;
            ok &= sg; notes.Add($"単体は包まない{(sg ? "" : " ×")}");
            var o1 = c.TakeTurn(m);
            bool lost = o1 == TurnOutcome.Stalled && m.RawCounter(StatusKeys.IdleTurn) == 1 && m.RawCounter(SlimeTrait.HeldKey) == 0 && Tl(c, m).R397HeldLost == 1 && Trait.SurrenderedTurn(c, m);
            TurnField.SetValue(c, 2); var o2 = c.TakeTurn(m);
            bool next = o2 != TurnOutcome.Stalled;
            ok &= lost && next; notes.Add($"包まれた仲間は次の手番を失う（{o1}・`IdleTurn` ＝ 1・差し出した手番）・その次は動く（{o2}）{(lost && next ? "" : " ×")}");
            Expect("R: 包まれた味方は範囲の一撃を受けない・セッキは 2 割を HP で受け、全部を溜める・包まれた味方は次の手番を失い `IdleTurn` が立つ・1体1ターン1回", ok, string.Join("・", notes));
        }

        // ---- G1 ／ G2 ----
        {
            var notes = new List<string>(); bool ok = true;
            (int[] Lost, long Tally, int Bonus) Swing(UnitDef def, int bonus, params int[] foes)
            {
                var (c, u, _, e) = Bench(def, 2, foes);
                u.AtkBonus = bonus;
                var h0 = e.Select(x => x.Hp).ToArray();
                c.PerformAttack(u);
                return (e.Select((x, i) => h0[i] - x.Hp).ToArray(), Tl(c, u).R397HeavyAdd, u.AtkBonus);
            }
            // 全体（力 30 で全体）・的 3 体
            var a0 = Swing(d, 30, 1000, 1000, 1000); var a1 = Swing(g1, 30, 1000, 1000, 1000);
            int prim0 = a0.Lost.Max(), sec0 = a0.Lost.Where(x => x > 0).Min();
            bool f1 = a1.Lost.Max() == prim0 * 5 / 3 && a1.Lost.Where(x => x > 0).Min() == sec0 * 5 / 3;
            ok &= f1; notes.Add($"G1 全体 × 5 ÷ 3: 主 {prim0} → {a1.Lost.Max()}・巻き込み {sec0} → {a1.Lost.Where(x => x > 0).Min()}{(f1 ? "" : " ×")}");
            var b0 = Swing(d, 0, 1000, 1000, 1000); var b1 = Swing(g1, 0, 1000, 1000, 1000);
            bool f2 = b0.Lost.SequenceEqual(b1.Lost);
            ok &= f2; notes.Add($"G1 薙ぎ（力 0）は変わらない（{string.Join("・", b0.Lost)}）{(f2 ? "" : " ×")}");
            var h0 = Swing(d, 0, 1000, 1000, 1000); var h2 = Swing(g2, 0, 1000, 1000, 1000);
            bool f3 = Enumerable.Range(0, 3).All(i => h0.Lost[i] == 0 ? h2.Lost[i] == 0 : h2.Lost[i] - h0.Lost[i] == 50) && h2.Bonus == 0 && h2.Tally == h2.Lost.Count(x => x > 0) * 50;
            ok &= f3; notes.Add($"G2 薙ぎ: 当たった的ごとに +50（最大 HP 1000 の 5%）・`AtkBonus` は 0 のまま{(f3 ? "" : " ×")}");
            var hb0 = Swing(d, 30, 3450, 1000, 1000); var hb12 = Swing(g12, 30, 3450, 1000, 1000);
            int hp3450i = Array.IndexOf(hb0.Lost, hb0.Lost.Max());
            bool f4 = Enumerable.Range(0, 3).All(i => hb0.Lost[i] == 0 || hb12.Lost[i] == hb0.Lost[i] * 5 / 3 + (i == 0 ? 172 : 50));
            ok &= f4; notes.Add($"G12: 配り直しの後に足す（最大 HP 3450 の的に +172）{(f4 ? "" : $" × {string.Join("・", hb0.Lost)} → {string.Join("・", hb12.Lost)}")}");
            Expect("G1: 全体の薙ぎの1体あたりが × 5 ÷ 当たる敵の数・薙ぎ ／ ふつうの一撃は変わらない ／ G2: 一撃に当たる敵の最大 HP の 5% が足される・`AtkBonus` は動かない ／ G12: 配り直しの後に足す", ok, string.Join("・", notes));
        }
        {
            // ボスの名前 ／ 種類で分岐していない: engine の G1 ／ G2 の口のソースにボス ／ 勇者の語が無い・勇者と同じ最大 HP の取り巻きで同じ量
            string src = File.ReadAllText("BattleCore/BattleEngine.cs");
            int a = src.IndexOf("int HeavyAdd397(", StringComparison.Ordinal), b = src.IndexOf("\n    }", a, StringComparison.Ordinal);
            int c = src.IndexOf("// 第397期（G1 ／ G2・ドルガの一撃）: G1 は", StringComparison.Ordinal), e = src.IndexOf("if (whip is not null) first = WhipAmount", c, StringComparison.Ordinal);
            string body = src[a..b] + src[c..e];
            bool noBoss = a > 0 && c > 0 && !body.Contains("Boss", StringComparison.Ordinal) && !body.Contains("勇者", StringComparison.Ordinal) && !body.Contains("IsBoss", StringComparison.Ordinal);
            var bossWave = EnemyCatalog.Stages.Count > 0 ? I.Boss : null;
            var heroHp = 3450;
            var x = Bench(g2, 2, heroHp, heroHp); x.U.AtkBonus = 0; var h0 = x.E.Select(u => u.Hp).ToArray(); x.C.PerformAttack(x.U);
            var lost = x.E.Select((u, i) => h0[i] - u.Hp).Where(v => v > 0).ToArray();
            bool sameAdd = Tl(x.C, x.U).R397HeavyAdd == lost.Length * (heroHp * 5 / 100);
            Expect("ボスの名前 ／ 種類で分岐していない（G1 ／ G2 の口のソースに「Boss ／ 勇者」が無い・勇者と同じ最大 HP 3,450 の的で足し分が 172 ずつ）", noBoss && sameAdd && bossWave is not null, $"足し分 {Tl(x.C, x.U).R397HeavyAdd}・当たった的 {lost.Length}");
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
            var vers = new[] { kb, kr, g1, g2, g12 };
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions).SelectMany(r => r.F.Occupied()).Select(o => o.Def).ToList();
            bool inside = !everywhere.Any(x => x.Id.EndsWith("_plain392", StringComparison.Ordinal));
            var verRows = Presets.Versions.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool rows = verRows.Count == 2 && verRows.All(n => VersionRows397.Contains(n)) && Presets.Versions.TakeLast(2).Select(r => r.Name).SequenceEqual(verRows)
                && Presets.Versions.TakeLast(2).All(r => r.F.Occupied().Count(o => vers.Contains(o.Def)) == 1)
                && !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => vers.Contains(o.Def)));
            Expect("替えた台 ／ 素体は器具の中だけ・版の駒が居る試遊の台は足した 2 台（`Presets.Versions` の末尾）だけ", inside && rows, string.Join("、", verRows));
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool Has2(TraitId id, string must) => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{id}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*{must}""");
            var miss = new List<string>();
            foreach (var id in new[] { TraitId.SlimeWrap, TraitId.SlimeOverflow, TraitId.SlimeDeep }) if (!Has2(id, "第397期から規定（セッキ）")) miss.Add(id.ToString());
            foreach (var id in new[] { TraitId.SlimeSwell, TraitId.SlimeHaven, TraitId.DolgaFocus, TraitId.DolgaHeavy }) if (!Has2(id, "第397期[^\"]*保持者 0 枚")) miss.Add(id.ToString());
            Expect("`checkup` の札の分類表: 規定のセッキの札 3 枚は「第397期から規定（セッキ）」・版の札 4 枚がある（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            var jobs = SBoardList().SelectMany(b => SVers.Select(v => Put(b.F, Has(b.F, "sekki") ? SlotOf(b.F, "sekki") : 2, v.D)))
                .Concat(DBoardList().Take(4).SelectMany(b => DVers.Select(v => Put(b.F, SlotOf(b.F, "dolga"), v.D))));
            foreach (var f in jobs)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 2; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（セッキの 3 台 × 版 3 ＋ ドルガの 4 台 × 版 4 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, tr0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1n = Now("BattleCore/BattleEngine.cs", "PickOne("), tr1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第396期の HEAD（{HeadSha}）と同じ）", e0 == e1n && tr0 == tr1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1n}・traits {tr0} → {tr1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
