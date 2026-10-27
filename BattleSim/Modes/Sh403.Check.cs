using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh403 check —— 第403期の自己検査（指示書 §7 の「新しい器具に最低限入れる」）。
static partial class Sh403Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo ReapTurnM = typeof(BattleContext).GetMethod("ReapTurn388", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "660fbb61";   // 第402期の HEAD（この期の作業を始めたときの BattleCore ／ BattleSim）
    // ---- §6 試遊の台（「版・二度斬り ハギ」）----
    internal const string PlayName = "版・二度斬り ハギ";
    internal static UnitDef PlayVersion => UnitCatalog.HagiSingleT12A;
    internal const string PlayVersionName = "T12A";
    internal static Formation PlayBoard() => Presets.HagiTwin403(UnitCatalog.HagiSingleT12A);   // 第404期: 既定が規定のハギになったので版を明示
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate403", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeDef(string id, int hp = 1000) => new() { Id = id, Name = "的" + id[^1], MaxHp = hp, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static (BattleContext Ctx, UnitState H, UnitState M, List<UnitState> E) Bench(UnitDef hagi, (int Slot, UnitDef D, int Hp)[] foes, int soul = 0, int marks = 0)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); f[3] = hagi; f[2] = Mate;
        var g = new Formation(); foreach (var (s, d, _) in foes) g[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var (s, _, hp) in foes) e.First(u => u.Slot == s).Hp = hp;
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        var h = p.First(u => u.Def.Id == "hagi");
        if (soul > 0) HagiDepthTrait.Gain(ctx, h, soul, 0);
        if (marks > 0) foreach (var u in e) u.SetCounter(HagiDepthTrait.MarkKey, marks);
        return (ctx, h, p.First(u => u.Def.Id == "mate403"), e);
    }
    static void Turn(BattleContext c, UnitState h) => ReapTurnM.Invoke(c, new object[] { h });
    static UnitState At(List<UnitState> e, int slot) => e.First(u => u.Slot == slot);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh403 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef hagi = UnitCatalog.HagiH403, old = UnitCatalog.HagiH402, t1 = UnitCatalog.HagiSingleT1, t2 = UnitCatalog.HagiSingleT2, t12 = UnitCatalog.HagiSingleT12, t12a = UnitCatalog.HagiSingleT12A;
        var vers = new[] { t1, t2, t12, t12a, UnitCatalog.HagiBigA403 };

        // ---- 段0 ----
        {
            bool cards = hagi.Traits.SequenceEqual(UnitCatalog.HagiSoulB.Traits) && hagi.PlusText == UnitCatalog.HagiSoulB.PlusText && hagi != UnitCatalog.HagiSoulB && !UnitCatalog.All.Contains(hagi) && UnitCatalog.All.Contains(UnitCatalog.Hagi) && hagi.Id == "hagi";   // 第404期: 旧の規定（`All` の外）
            bool oldOk = old.Traits.SequenceEqual(UnitCatalog.HagiCarryH123.Traits) && old.PlusText == UnitCatalog.HagiCarryH123.PlusText && !UnitCatalog.Everyone.Contains(old);
            bool pins = PinH402(hagi) == old && PinH402(old) == old && PinH401(hagi) == UnitCatalog.HagiH401 && PinH401(old) == UnitCatalog.HagiH401 && PinT399(hagi) == UnitCatalog.HagiH401
                && PinH391(old) == UnitCatalog.HagiH391 && PinH389(old) == UnitCatalog.HagiH389 && Pin402(Presets.HagiCarry401())[3] == old;
            bool v402 = new[] { UnitCatalog.HagiSoulA, UnitCatalog.HagiSoulD, UnitCatalog.HagiSoulAD, UnitCatalog.HagiSoulB }
                .All(x => x.Traits.Take(old.Traits.Count - 1).SequenceEqual(old.Traits.SkipLast(1)) && x.PlusText.StartsWith(old.PlusText, StringComparison.Ordinal));
            Expect("段0: 規定のハギ ＝ 第402期の B と同じ札と文面（別の定義・`All` の中）・旧 `HagiH402` ＝ H123（`All` ／ `Retired` の外）・固定の口（`PinH402` ／ `Pin402`・`PinH401` ／ `PinT399` ／ `PinH391` ／ `PinH389` は旧も古い旧へ）・第402期の版の駒は旧から作ったまま",
                cards && oldOk && pins && v402, $"札 {cards}・旧 {oldOk}・口 {pins}・第402期の版 {v402}");
        }
        {
            long diff = 0, cells = 0;
            foreach (var bd in CarryHand().Concat(StopBoards()))
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(ColBoard(bd, hagi), w, sd).R, I.Raw(ColBoard(bd, UnitCatalog.HagiSoulB), w, sd).R)) diff++;
                    }
            Expect("段0: 規定のハギは第402期の B と同じ動き（7 台 × 4 波 × seed 0..9）", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- 版の札と文面 ----
        {
            int n = hagi.Traits.Count - 1;
            bool ok = vers.All(x => x.Id == "hagi" && x.Name == hagi.Name && x.Traits.Take(n).SequenceEqual(hagi.Traits.SkipLast(1)) && x.Traits[^1] == TraitId.HagiSpillFixed && x.MinusText == hagi.MinusText && !UnitCatalog.Everyone.Contains(x))
                && t1.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiTwinIsolated }) && t2.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiDeepMark })
                && t12.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiTwinIsolated, TraitId.HagiDeepMark }) && t12a.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiTwinIsolated, TraitId.HagiDeepMark, TraitId.HagiBigSoul })
                && t1.PlusText.StartsWith(hagi.PlusText, StringComparison.Ordinal) && t1.PlusText.Contains("孤立した敵には、鎌を二度振るう", StringComparison.Ordinal)
                && t2.PlusText.Contains("印は 9 つまで刻む", StringComparison.Ordinal) && t2.PlusText.Contains("（印1つ +5%・合わせて最大 6 割）", StringComparison.Ordinal) && !t1.PlusText.Contains("9 つまで", StringComparison.Ordinal)
                && !UnitCatalog.Everyone.Where(x => x != UnitCatalog.Hagi).Any(x => x.Traits.Contains(TraitId.HagiTwinIsolated) || x.Traits.Contains(TraitId.HagiDeepMark));   // 第404期: 規定のハギ（T1）は除いて数える
            Expect("版の札と文面: T1 ＋ 孤立した敵は二度斬る ／ T2 ＋ 印を深く刻む（H1 の句に「印は 9 つまで刻む」・線の句は今どおり）／ T12 ／ T12A（＋ A）・固定の巻き込みが末尾・`All` ／ `Retired` の外・札の保持者 0 枚", ok);
        }

        // ---- T1 ----
        {
            var notes = new List<string>(); bool ok = true;
            // 孤立（1 体だけ）: 2 振り（16 → 印 1 で 16 ＋ 30）。規定は 1 振り
            var (c1, h1, _, e1) = Bench(t1, new[] { (0, FoeDef("foe403a"), 1000) }); Turn(c1, h1);
            var (c0, h0, _, e0) = Bench(hagi, new[] { (0, FoeDef("foe403a"), 1000) }); Turn(c0, h0);
            bool g1 = 1000 - e1[0].Hp == 16 + 46 && Tl(c1, h1).R403Twins == 1 && 1000 - e0[0].Hp == 16 && Tl(c0, h0).R403Twins == 0;
            ok &= g1; notes.Add($"孤立: 2 振り（{1000 - e1[0].Hp} ＝ 16 ＋ 46）・規定は {1000 - e0[0].Hp}{(g1 ? "" : " ×")}");
            // 隣に敵がいる: 1 振り
            var (c2, h2, _, e2) = Bench(t1, new[] { (0, FoeDef("foe403a"), 1000), (2, FoeDef("foe403b"), 1000) });
            bool adj = FormationRules.AreAdjacent(At(e2, 0), At(e2, 2));
            Turn(c2, h2);
            bool g2 = adj && Tl(c2, h2).R403Twins == 0 && (1000 - At(e2, 0).Hp) + (1000 - At(e2, 2).Hp) == 16;
            ok &= g2; notes.Add($"隣（前1 と中央・隣接 {adj}）に敵がいれば 1 振り{(g2 ? "" : " ×")}");
            // 2 発目の前に刈り取りの判定: 230（23% ＞ 線 2 割）→ 214（印 1 で線 25%）→ 2 発目で刈る。隣接しない後3 の敵へ再行動（1 振り）
            var (c3, h3, _, e3) = Bench(t1, new[] { (0, FoeDef("foe403a"), 230), (4, FoeDef("foe403c"), 1000) });
            bool far = !FormationRules.AreAdjacent(At(e3, 0), At(e3, 4));
            Turn(c3, h3);
            var t3 = Tl(c3, h3);
            bool g3 = far && !At(e3, 0).IsAlive && t3.R403Twins == 1 && t3.R403TwinReaps == 1 && t3.R403TwinKills == 1 && t3.R388Encores == 1 && At(e3, 4).Hp == 1000 - 16;   // 再行動は 1 振り（倒れて魂 2 → 段 0 のまま）
            ok &= g3; notes.Add($"2 発目の前に刈り取りの判定（刈った {t3.R403TwinReaps}）・再行動は 1 振り（後3 の敵 1000 → {At(e3, 4).Hp}・二度斬り {t3.R403Twins} 回）{(g3 ? "" : " ×")}");
            // 追撃は 1 発: 仲間の一撃で 600 → 450（5 割を割る）→ 追撃 1 発
            var (c4, h4, m4, e4) = Bench(t1, new[] { (0, FoeDef("foe403a"), 600) }); c4.ApplyDamage(e4[0], 150, m4);
            bool g4 = Tl(c4, h4).R388Pursuits == 1 && Tl(c4, h4).R403Twins == 0;
            ok &= g4; notes.Add($"追撃は 1 発{(g4 ? "" : " ×")}");
            Expect("T1: 主目標の隣に敵がいなければ手番の振りが 2 回・隣にいれば 1 回・2 発目の前に刈り取りの判定・追撃 ／ 再行動は 1 発", ok, string.Join("・", notes));
        }

        // ---- T2 ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c1, h1, _, e1) = Bench(t2, new[] { (0, FoeDef("foe403a"), 1000) }, marks: 8);
            Turn(c1, h1); int d1 = 1000 - e1[0].Hp, m1 = e1[0].RawCounter(HagiDepthTrait.MarkKey);
            int hp1 = e1[0].Hp; Turn(c1, h1); int d2 = hp1 - e1[0].Hp, m2 = e1[0].RawCounter(HagiDepthTrait.MarkKey);
            bool g1 = d1 == 16 + 8 * 30 && m1 == 9 && d2 == 16 + 9 * 30 && m2 == 9 && HagiDepthTrait.MarkCapOf(h1) == 9;
            ok &= g1; notes.Add($"印 8 → 9（{d1} ＝ 16 ＋ 240）・9 のまま（{d2} ＝ 16 ＋ 270）{(g1 ? "" : $" × m1={m1} m2={m2}")}");
            var (c2, h2, _, e2) = Bench(hagi, new[] { (0, FoeDef("foe403a"), 1000) }, marks: 5); Turn(c2, h2);
            bool g2 = e2[0].RawCounter(HagiDepthTrait.MarkKey) == 5;
            ok &= g2; notes.Add($"規定は 5 のまま{(g2 ? "" : " ×")}");
            bool Reaps(int hp) { var (c, h, _, e) = Bench(t2, new[] { (0, FoeDef("foe403a"), hp) }, soul: 9, marks: 9); Turn(c, h); return Tl(c, h).R388Reaps >= 1 && !e[0].IsAlive; }
            bool g3 = !Reaps(610) && Reaps(600);
            ok &= g3; notes.Add($"線の上乗せの上限 6 割は今どおり（魂 9 ＋ 印 9: 61% は刈らない ／ 60% は刈る）{(g3 ? "" : " ×")}");
            Expect("T2: 印が 9 まで・H1 の足し分が印 × 3%・線の上乗せの上限 6 割は変わらない", ok, string.Join("・", notes));
        }

        // ---- T12A ----
        {
            var (c, h, m, e) = Bench(t12a, new[] { (0, FoeDef("foe403a", 1000), 10) }); c.ApplyDamage(e[0], 10, m);
            var (c0, h0, m0, e0) = Bench(t12, new[] { (0, FoeDef("foe403a", 1000), 10) }); c0.ApplyDamage(e0[0], 10, m0);
            bool ok = HagiDepthTrait.Soul(h) == 6 && HagiDepthTrait.Soul(h0) == 2;
            Expect("T12A: A の 3 倍が乗る（最大 HP 1,000 の敵の死で魂 +6・T12 は +2）", ok, $"T12A {HagiDepthTrait.Soul(h)} ／ T12 {HagiDepthTrait.Soul(h0)}");
        }

        // ---- ボスの名前 ／ 種類で分岐していない ----
        {
            var hits = new List<string>();
            foreach (var f in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
                foreach (var l in File.ReadAllLines(f))
                {
                    if (!(l.Contains("第403期", StringComparison.Ordinal) || l.Contains("TwinIsolated", StringComparison.Ordinal) || l.Contains("DeepMark", StringComparison.Ordinal) || l.Contains("Isolated403", StringComparison.Ordinal) || l.Contains("TwinSwing403", StringComparison.Ordinal))) continue;
                    string code = l.Contains("//") ? l[..l.IndexOf("//", StringComparison.Ordinal)] : l;
                    if (code.TrimStart().StartsWith("///", StringComparison.Ordinal)) continue;
                    if (code.Contains("Boss", StringComparison.Ordinal) || code.Contains("勇者", StringComparison.Ordinal) || code.Contains("Hero", StringComparison.Ordinal) || code.Contains("Def.Id", StringComparison.Ordinal) || code.Contains("Def.Name", StringComparison.Ordinal)) hits.Add(l.Trim());
                }
            Expect("ボスの名前 ／ 種類で分岐していない（第403期の札を読むコードの行に Boss ／ 勇者 ／ Hero ／ Def.Id ／ Def.Name が無い・孤立は隣接表だけで決める）", hits.Count == 0, hits.Count == 0 ? "" : string.Join(" ／ ", hits.Take(3)));
        }

        // ---- 規定の compare ----
        {
            var map = S390.BalanceMap(S390.GitShow("350ebf20:docs/balance.md"));   // 第404期: 第403期の表と、ハギを旧に固定した行で比べる
            var head = S390.BalanceMap(S390.GitShow($"{HeadSha}:docs/balance.md"));
            var rows = CompareBuildsH410().Select(r => (r.Name, F: Pin403(r.F))).ToArray(); var g = CGrid(rows, CompareSeeds);   // 第411期: カーズの2行は旧の行名 ＋ 旧のカリで
            int diff = 0, miss = 0, noHagiMoved = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var c)) { miss++; continue; }
                for (int w = 0; w < 5; w++)
                {
                    if (c[w].TrimEnd('%') != F1(g[ri, w])) diff++;
                    if (!Has(rows[ri].F, "hagi") && head.TryGetValue(rows[ri].Name, out var h0) && h0[w] != c[w]) noHagiMoved++;
                }
            }
            Expect($"規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致・ハギのいない行は第402期（{HeadSha}）の表と同じ", diff == 0 && miss == 0 && noHagiMoved == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}・ハギのいない行で動いたセル {noHagiMoved}");
        }

        // ---- 試遊の台 ----
        {
            var vrows = VersionsH403();   // 第404期: 第403期の一覧
            var hold = vrows.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool last = vrows[^1].Name == PlayName && vrows[^1].F.Occupied().Count(o => o.Def == PlayVersion) == 1 && Som307Diag.Seats(vrows[^1].F) == Som307Diag.Seats(PlayBoard())
                && hold.SequenceEqual(new[] { "版・大物 ハギ", PlayName }) && vrows.First(r => r.Name == "版・大物 ハギ").F[1] == UnitCatalog.HagiBigA403;
            var h402 = VersionsH402();
            bool past = !h402.Any(r => VersionRows403.Contains(r.Name)) && h402.First(r => r.Name == "版・大物 ハギ").F[1] == UnitCatalog.HagiSoulA && h402.SelectMany(r => r.F.Occupied()).All(o => o.Def != hagi)
                && VersionsH401().Length == h402.Length - 1;
            bool inside = !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(vrows.Where(r => r.Name is not ("版・大物 ハギ" or PlayName))).SelectMany(r => r.F.Occupied()).Any(o => vers.Contains(o.Def));
            Expect($"試遊の台: 「版・大物 ハギ」は規定 ＋ A（`HagiBigA403`）・末尾に「{PlayName}」（{PlayVersionName}）・第402期の一覧は `VersionsH402`（足した台を外し、版・大物 ハギは旧の A・ほかのハギは旧）・替えた台 ／ 版は器具の中だけ",
                last && past && inside, $"{string.Join("、", vrows.TakeLast(2).Select(r => r.Name))}・{Som307Diag.Seats(vrows[^1].F)}・第402期 {past}・中だけ {inside}");
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool Row(TraitId t, string pat) => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(HcPlusL,\s*""[^""]*{pat}""");
            var miss = new[] { TraitId.HagiTwinIsolated, TraitId.HagiDeepMark }.Where(t => !Row(t, "第403期[^\"]*(保持者 0 枚|第404期から規定（ハギ）)")).Concat(new[] { TraitId.HagiAllySoul }.Where(t => !Row(t, "第402期[^\"]*第403期から規定（ハギ）"))).ToList();
            Expect("`checkup` の札の分類表: 第403期の札 2 枚（保持者 0 枚）・規定になった B（味方の死でも魂）は「第403期から規定（ハギ）」（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var bd in CarryHand().Concat(StopBoards()))
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            cells++;
                            var f = ColBoard(bd, v.D);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（7 台 × 版 5 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1c = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（第402期（{HeadSha}）と同じ数）", e0 == e1 && t0 == t1c && r0 == r1 && re0 == re1 && e0 > 0, $"PickOne engine {e0} → {e1}・traits {t0} → {t1c}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
