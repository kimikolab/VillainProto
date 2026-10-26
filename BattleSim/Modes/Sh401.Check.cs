using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S388 = Sh388Diag;
using S390 = Sh390Diag;

// sh401 check —— 第401期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh401Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo ReapTurnM = typeof(BattleContext).GetMethod("ReapTurn388", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "dc5a0d3b";   // 第400期の最後のコミット（この期の作業を始めたときの BattleCore ／ BattleSim・HEAD 7800d017 まで同じ）
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate401", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeDef(string id, int hp = 1000) => new() { Id = id, Name = "的" + id[^1], MaxHp = hp, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;

    static (BattleContext Ctx, UnitState H, UnitState M, List<UnitState> E) Bench(UnitDef hagi, (int Slot, UnitDef D, int Hp)[] foes, int soul = 0, int marks = 0, int bonus = 0)
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
        h.AtkBonus += bonus;
        return (ctx, h, p.First(u => u.Def.Id == "mate401"), e);
    }
    static void Turn(BattleContext c, UnitState h) => ReapTurnM.Invoke(c, new object[] { h });
    static UnitState At(List<UnitState> e, int slot) => e.First(u => u.Slot == slot);
    /// <summary>1000 の的 1 体にハギの手番を1回振らせ、減った HP を返す。</summary>
    static int Hit(UnitDef d, int soul = 0, int marks = 0, int bonus = 0, int hp = 1000)
    {
        var (c, h, _, e) = Bench(d, new[] { (0, FoeDef("foe401a"), hp) }, soul, marks, bonus);
        int hp0 = e[0].Hp; Turn(c, h); return hp0 - e[0].Hp;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh401 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef hagi = UnitCatalog.Hagi, h1 = UnitCatalog.HagiCarryH1, h2 = UnitCatalog.HagiCarryH2, h3 = UnitCatalog.HagiCarryH3, h123 = UnitCatalog.HagiCarryH123, h123l = UnitCatalog.HagiCarryH123L;
        var vers = new[] { h1, h2, h3, h123, h123l };

        // ---- 版の札と文面 ----
        {
            var cards = new[] { TraitId.HagiMarkWeight, TraitId.HagiStageMul, TraitId.HagiLineLow };
            bool ok = vers.All(d => d.Id == "hagi" && d.Name == hagi.Name && d.MaxHp == 62 && d.Attack == 16 && d.Speed == 7 && d.Pattern == AttackPattern.Single
                    && d.Traits.Take(hagi.Traits.Count - 1).SequenceEqual(hagi.Traits.SkipLast(1)) && d.Traits[^1] == TraitId.HagiSpillFixed && d.MinusText == hagi.MinusText && !UnitCatalog.Everyone.Contains(d))
                && h1.Traits.Skip(hagi.Traits.Count - 1).SkipLast(1).SequenceEqual(new[] { TraitId.HagiMarkWeight })
                && h2.Traits.SequenceEqual(hagi.Traits) && h2.PlusText == hagi.PlusText
                && h3.Traits.Skip(hagi.Traits.Count - 1).SkipLast(1).SequenceEqual(new[] { TraitId.HagiStageMul })
                && h123.Traits.Skip(hagi.Traits.Count - 1).SkipLast(1).SequenceEqual(new[] { TraitId.HagiMarkWeight, TraitId.HagiStageMul })
                && h123l.Traits.Skip(hagi.Traits.Count - 1).SkipLast(1).SequenceEqual(new[] { TraitId.HagiMarkWeight, TraitId.HagiStageMul, TraitId.HagiLineLow })
                && h1.PlusText.StartsWith(hagi.PlusText, StringComparison.Ordinal) && h1.PlusText.Contains("印が増えるほど鎌は深く食い込む", StringComparison.Ordinal)
                && h3.PlusText.StartsWith(hagi.PlusText, StringComparison.Ordinal) && h3.PlusText.Contains("魂が溜まるほど、鎌の一撃は重くなる", StringComparison.Ordinal)
                && h123.PlusText == h1.PlusText + h3.PlusText[hagi.PlusText.Length..]
                && h123l.PlusText.Contains("（1 割 → 最大 4 割）", StringComparison.Ordinal) && h123l.PlusText.Contains("合わせて最大 5 割", StringComparison.Ordinal) && !h123l.PlusText.Contains("最大 6 割", StringComparison.Ordinal)
                && hagi.PlusText.Contains("倒せば、もう一度振る", StringComparison.Ordinal)
                && !UnitCatalog.Everyone.Any(d => d.Traits.Any(cards.Contains));
            Expect("版の札と文面: H1 ＋ 印が重い ／ H2 ＝ 規定と同じ札と文面 ／ H3 ＋ 段で倍率 ／ H123 ＋ 両方 ／ H123L ＋ 線を下げる（文面の線を 1 → 最大 4 割・上限 5 割）・固定の巻き込みが末尾・`All` ／ `Retired` の外・札の保持者 0 枚", ok,
                string.Join(" ／ ", new[] { ("H1", h1), ("H2", h2), ("H3", h3), ("H123", h123), ("H123L", h123l) }.Select(x => $"{x.Item1} {string.Join("・", x.Item2.Traits.Skip(hagi.Traits.Count - 1).SkipLast(1).DefaultIfEmpty(TraitId.HagiSpillFixed).Select(t => t == TraitId.HagiSpillFixed ? "（足さない）" : t.ToString()))}")));
        }

        // ---- H1 ----
        {
            var notes = new List<string>(); bool ok = true;
            // 1発目は印 0 → 16。同じ的にもう一度振ると印 1 → 16 ＋ 1000 × 3% ＝ 46。
            var (c, h, _, e) = Bench(h1, new[] { (0, FoeDef("foe401a"), 1000) });
            Turn(c, h); int d1 = 1000 - e[0].Hp; int hp1 = e[0].Hp, m1 = e[0].RawCounter(HagiDepthTrait.MarkKey);
            Turn(c, h); int d2 = hp1 - e[0].Hp;
            bool g1 = d1 == 16 && m1 == 1 && d2 == 16 + 30 && Tl(c, h).R401MarkAdd == 30 && Tl(c, h).R401MarkHits == 1;
            ok &= g1; notes.Add($"1発目 {d1}（印 0）→ 2発目 {d2}（印 {m1}）{(g1 ? "" : " ×")}");
            // 印 3 ／ 最大 HP 777 の的: 3 × 23（777 × 3% の切り捨て）＝ 69。規定は 16 のまま
            int d3 = Hit(h1, marks: 3, hp: 777) ; int d0 = Hit(hagi, marks: 3, hp: 777);
            var (c4, h4, _, e4) = Bench(h1, new[] { (0, FoeDef("foe401b", 777), 777) }, marks: 3); Turn(c4, h4); int d4 = 777 - e4[0].Hp;
            bool g2 = d4 == 16 + 3 * (777 * 3 / 100) && d0 == 16;
            ok &= g2; notes.Add($"印 3 × 最大 HP 777 の 3%（{777 * 3 / 100}）＝ {d4 - 16} を足す（規定 {d0}）{(g2 ? "" : $" × d3={d3}")}");
            // 刈り取りには足さない（150 ／ 1000 ＝ 15% ≦ 2 割 → 残りの HP だけ）
            var (c5, h5, _, e5) = Bench(h1, new[] { (0, FoeDef("foe401a"), 150) }, marks: 5); Turn(c5, h5);
            bool g3 = !e5[0].IsAlive && Tl(c5, h5).R388Reaps == 1 && Tl(c5, h5).R401MarkHits == 0;
            ok &= g3; notes.Add($"刈り取りの一撃には足さない{(g3 ? "" : " ×")}");
            // 薙ぎの巻き込みにはその敵の印のぶんを丸ごと（魂 3 ＝ 薙ぎ・巻き込みは 6 割）
            var (c6, h6, _, e6) = Bench(h1, new[] { (0, FoeDef("foe401a"), 1000), (2, FoeDef("foe401c"), 1000) }, soul: 3); At(e6, 2).SetCounter(HagiDepthTrait.MarkKey, 2);
            Turn(c6, h6);
            int dm = 1000 - At(e6, 0).Hp, ds = 1000 - At(e6, 2).Hp;
            bool g4 = dm == 16 && ds == Math.Max(1, 16 * BattleContext.SecondaryPercent / 100) + 60;
            ok &= g4; notes.Add($"薙ぎ: 主目標（印 0）{dm} ／ 巻き込み（印 2）{ds} ＝ 6 割 ＋ 60{(g4 ? "" : " ×")}");
            Expect("H1: 印の数 × その敵の最大 HP の 3%（切り捨て）を一撃に足す・1発目は印 0・刈り取りには足さない・巻き込みの敵にもその敵の印のぶん", ok, string.Join("・", notes));
        }

        // ---- H2 ----
        {
            var notes = new List<string>(); bool ok = true;
            // 規定 ＝ H2: 40 の的 4 体を 15 ずつ（37.5% ＞ 線）——ふつうの斬撃（16）で倒して再行動、2 回まで（倒すのは 3 体）
            foreach (var (nm, d) in new[] { ("H2", h2), ("規定", hagi) })
            {
                var foes = new[] { (0, FoeDef("foe401a", 40), 15), (1, FoeDef("foe401b", 40), 15), (3, FoeDef("foe401c", 40), 15), (4, FoeDef("foe401d", 40), 15) };
                var (c, h, _, e) = Bench(d, foes); Turn(c, h);
                var t = Tl(c, h);
                bool g = t.Kills == 3 && t.R388Encores == 2 && t.R401EncoreStrike == 2 && t.R401EncoreReap == 0 && t.R388Reaps == 0 && e.Count(u => u.IsAlive) == 1;
                ok &= g; notes.Add($"{nm}: ふつうの斬撃で倒して再行動 {t.R401EncoreStrike} 回（上限 2・倒した {t.Kills}）{(g ? "" : " ×")}");
            }
            // 追撃で倒しても再行動しない: 仲間の一撃で 600 → 150（5 割と 2 割を割る）→ 追撃の刈り取り
            var (c2, h2u, m2, e2) = Bench(h2, new[] { (0, FoeDef("foe401a"), 600) }); c2.ApplyDamage(e2[0], 450, m2);
            var t2 = Tl(c2, h2u);
            bool g2 = !e2[0].IsAlive && t2.R391ChaseReaps == 1 && t2.R388Encores == 0;
            ok &= g2; notes.Add($"追撃で倒しても再行動 0{(g2 ? "" : " ×")}");
            Expect("H2: ふつうの斬撃で倒しても再行動（規定も同じ）・上限 2 回・追撃では再行動しない", ok, string.Join("・", notes));
        }

        // ---- H3 ----
        {
            var notes = new List<string>(); bool ok = true;
            var got = new[] { 0, 3, 6, 9 }.Select(s => Hit(h3, soul: s)).ToArray();
            bool g1 = got.SequenceEqual(new[] { 16, 32, 48, 80 }) && new[] { 0, 3, 6, 9 }.All(s => Hit(hagi, soul: s) == 16);
            ok &= g1; notes.Add($"魂 0 ／ 3 ／ 6 ／ 9: {string.Join(" ／ ", got)}（× 1 ／ 2 ／ 3 ／ 5・規定は 16）{(g1 ? "" : " ×")}");
            int gb = Hit(h3, soul: 3, bonus: 10);
            bool g2 = gb == (16 + 10) * 2;
            ok &= g2; notes.Add($"外から積まれた力 +10 も倍に（{gb}）{(g2 ? "" : " ×")}");
            var (c3, h3u, _, e3) = Bench(h3, new[] { (0, FoeDef("foe401a"), 150) }, soul: 3); Turn(c3, h3u);
            bool g3 = !e3[0].IsAlive && Tl(c3, h3u).R388Reaps == 1 && Tl(c3, h3u).R401MulHits == 0 && Tl(c3, h3u).R401DmgReap == 150;
            ok &= g3; notes.Add($"刈り取りには掛けない（残りの 150 だけ）{(g3 ? "" : " ×")}");
            // 追撃: 攻撃力 × 倍率 ＋ 失った HP の 2 割（倍率は上乗せに掛からない）。魂 3（＋ 5 割を割った +1 ＝ 4・段 1）
            var (c4, h4, m4, e4) = Bench(h3, new[] { (0, FoeDef("foe401a"), 600) }, soul: 3); c4.ApplyDamage(e4[0], 150, m4);
            int d4 = 450 - e4[0].Hp;
            bool g4 = d4 == 16 * 2 + 550 * 20 / 100 && Tl(c4, h4).R388Pursuits == 1;
            ok &= g4; notes.Add($"追撃 {d4} ＝ 16 × 2 ＋ 550 の 2 割{(g4 ? "" : " ×")}");
            Expect("H3: 段 0 ／ 3 ／ 6 ／ 9 で × 1 ／ 2 ／ 3 ／ 5・今の攻撃力（外から積まれた力を含む）に掛かる・刈り取りには掛からない・追撃は上乗せの前に掛かる", ok, string.Join("・", notes));
        }

        // ---- H123 ----
        {
            int d = Hit(h123, soul: 3, marks: 2);
            bool ok = d == 16 * 2 + 2 * 30 && Hit(h123, soul: 0, marks: 2) == 16 + 60;
            Expect("H123: H1 の足し分は倍率の後（魂 3 ・印 2 で 16 × 2 ＋ 60）", ok, $"{d}（倍率の前に足すと {(16 + 60) * 2}）");
        }

        // ---- H123L ----
        {
            var notes = new List<string>(); bool ok = true;
            var (c0, hh, _, _) = Bench(h123l, Array.Empty<(int, UnitDef, int)>());
            var lines = new[] { 0, 3, 6, 9 }.Select(s => { var (c, h, _, _) = Bench(h123l, new[] { (0, FoeDef("foe401a"), 1000) }, soul: s); return HagiDepthTrait.LinePermil(h); }).ToArray();
            bool g1 = lines.SequenceEqual(new[] { 100, 200, 300, 400 }) && HagiDepthTrait.MarkLineMax(hh) == 500 && HagiDepthTrait.MarkLineMax(Bench(hagi, Array.Empty<(int, UnitDef, int)>()).H) == 600;
            ok &= g1; notes.Add($"線 {string.Join(" ／ ", lines.Select(l => l / 10.0 + "%"))}・上限 {HagiDepthTrait.MarkLineMax(hh) / 10}%{(g1 ? "" : " ×")}");
            // 魂 0 で 15%: 規定は刈る（線 2 割）・H123L は刈らない（線 1 割）
            bool Reaps(UnitDef d, int soul, int marks, int hp) { var (c, h, _, e) = Bench(d, new[] { (0, FoeDef("foe401a"), hp) }, soul, marks); Turn(c, h); return Tl(c, h).R388Reaps == 1; }
            bool g2 = Reaps(hagi, 0, 0, 150) && !Reaps(h123l, 0, 0, 150) && Reaps(h123l, 0, 0, 100);
            ok &= g2; notes.Add($"魂 0: 15% を規定は刈る ／ H123L は刈らない ／ 10% は刈る{(g2 ? "" : " ×")}");
            // 魂 9 ＋ 印 5: 55% を規定は刈る（上限 6 割）・H123L は刈らない（上限 5 割）・50% は刈る
            bool g3 = Reaps(hagi, 9, 5, 550) && !Reaps(h123l, 9, 5, 550) && Reaps(h123l, 9, 5, 500);
            ok &= g3; notes.Add($"魂 9 ＋ 印 5: 55% を規定は刈る ／ H123L は刈らない ／ 50% は刈る{(g3 ? "" : " ×")}");
            Expect("H123L: 線が 1 ／ 2 ／ 3 ／ 4 割・印込みの上限 5 割", ok, string.Join("・", notes));
        }

        // ---- 規定のハギ ／ compare ----
        {
            var map = S390.BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0, noHagi = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var c)) { miss++; continue; }
                if (!Has(rows[ri].F, "hagi")) noHagi++;
                for (int w = 0; w < 5; w++) if (c[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            bool def = hagi.Traits.SequenceEqual(UnitCatalog.HagiP1h.Traits) && hagi.PlusText == UnitCatalog.HagiP1h.PlusText && hagi.Id == "hagi" && UnitCatalog.All.Contains(hagi);
            Expect("規定のハギは今どおり（札と文面 ＝ 第391期の P1h）・規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致（ハギのいない行も）", def && diff == 0 && miss == 0,
                $"{rows.Length} 行（うちハギのいない行 {noHagi}）・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 試遊の台 ／ 替えた台は器具の中だけ ----
        {
            var vrows = Presets.Versions;
            var hold = vrows.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool last = vrows[^1].Name == PlayName && hold.SequenceEqual(new[] { PlayName }) && vrows[^1].F.Occupied().Count(o => o.Def == PlayVersion) == 1
                && S307Seats(vrows[^1].F) == S307Seats(PlayBoard());
            bool past = !VersionsH400().Any(r => r.Name == PlayName) && VersionsH400().Select(r => r.Name).SequenceEqual(vrows.Take(vrows.Length - 1).Select(r => r.Name));
            bool inside = !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(vrows.SkipLast(1)).SelectMany(r => r.F.Occupied()).Any(o => vers.Contains(o.Def) || o.Def == S388.PlainHagi);
            Expect($"試遊の台: 末尾に「{PlayName}」（{PlayVersionName}）・第400期の一覧は `VersionsH400` ・替えた台 ／ 素体 ／ 版は器具の中だけ", last && past && inside, $"{string.Join("、", vrows.TakeLast(2).Select(r => r.Name))}・{S307Seats(vrows[^1].F)}");
        }

        // ---- checkup の札の表 ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var cards = new[] { TraitId.HagiMarkWeight, TraitId.HagiStageMul, TraitId.HagiLineLow };
            var miss = cards.Where(t => !System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(HcPlusL,\s*""[^""]*第401期[^""]*保持者 0 枚""")).ToList();
            Expect("`checkup` の札の分類表に第401期の札 3 枚（印が重い ／ 段で倍率 ／ 線を下げる）がある（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in StopBoards().Concat(CarryBoards()))
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            cells++;
                            var f = ColBoard(b, v.D);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（6 台 × 版 6 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- H2 ＝ 規定（台の上でも）----
        {
            long diff = 0, cells = 0;
            foreach (var b in StopBoards().Concat(CarryBoards()))
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(ColBoard(b, hagi), w, sd).R, I.Raw(ColBoard(b, h2), w, sd).R)) diff++;
                    }
            Expect("H2 は規定と同じ動き（6 台 × 4 波 × seed 0..9）", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第400期（{HeadSha}）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
    static string S307Seats(Formation f) => Som307Diag.Seats(f);
}
