using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh404 check —— 第404期の自己検査（指示書 §7 の「新しい器具に最低限入れる」）。
static partial class Sh404Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo ReapTurnM = typeof(BattleContext).GetMethod("ReapTurn388", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "350ebf20";   // 第403期の HEAD（この期の作業を始めたときの BattleCore ／ BattleSim）
    // ---- §6 試遊の台（「版・刈り続け ハギ」）----
    internal const string PlayName = "版・刈り続け ハギ";
    internal static UnitDef PlayVersion => UnitCatalog.HagiReapR45;
    internal const string PlayVersionName = "R45";
    internal static Formation PlayBoard() => Presets.HagiReap404();
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate404", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
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
        return (ctx, h, p.First(u => u.Def.Id == "mate404"), e);
    }
    static void Turn(BattleContext c, UnitState h) => ReapTurnM.Invoke(c, new object[] { h });
    static UnitState At(List<UnitState> e, int slot) => e.First(u => u.Slot == slot);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh404 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef hagi = UnitCatalog.Hagi, old = UnitCatalog.HagiH403, l45 = UnitCatalog.HagiReapL45, r50 = UnitCatalog.HagiReapR50, r45 = UnitCatalog.HagiReapR45;
        var vers = new[] { l45, r50, r45, UnitCatalog.HagiBigA404 };

        // ---- 段0 ----
        {
            bool cards = hagi.Traits.SequenceEqual(UnitCatalog.HagiSingleT1.Traits) && hagi.PlusText == UnitCatalog.HagiSingleT1.PlusText && hagi != UnitCatalog.HagiSingleT1 && UnitCatalog.All.Contains(hagi) && hagi.Id == "hagi";
            bool oldOk = old.Traits.SequenceEqual(UnitCatalog.HagiSoulB.Traits) && old.PlusText == UnitCatalog.HagiSoulB.PlusText && !UnitCatalog.Everyone.Contains(old);
            bool pins = PinH403(hagi) == old && PinH403(old) == old && PinH402(hagi) == UnitCatalog.HagiH402 && PinH402(old) == UnitCatalog.HagiH402 && PinH401(old) == UnitCatalog.HagiH401
                && PinT399(hagi) == UnitCatalog.HagiH401 && PinH391(old) == UnitCatalog.HagiH391 && PinH389(old) == UnitCatalog.HagiH389 && Pin403(Presets.HagiTwin403())[3] == old;
            bool v403 = new[] { UnitCatalog.HagiSingleT1, UnitCatalog.HagiSingleT2, UnitCatalog.HagiSingleT12, UnitCatalog.HagiSingleT12A, UnitCatalog.HagiBigA403 }
                .All(x => x.Traits.Take(old.Traits.Count - 1).SequenceEqual(old.Traits.SkipLast(1)));
            Expect("段0: 規定のハギ ＝ 第403期の T1 と同じ札と文面（別の定義・`All` の中）・旧 `HagiH403` ＝ H123 ＋ B（`All` ／ `Retired` の外）・固定の口（`PinH403` ／ `Pin403`・`PinH402` ／ `PinH401` ／ `PinT399` ／ `PinH391` ／ `PinH389` は旧も古い旧へ）・第403期の版の駒は旧から作ったまま",
                cards && oldOk && pins && v403, $"札 {cards}・旧 {oldOk}・口 {pins}・第403期の版 {v403}");
        }
        {
            long diff = 0, cells = 0;
            foreach (var bd in StopBoards().Concat(CarryHand()))
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(ColBoard(bd, hagi), w, sd).R, I.Raw(ColBoard(bd, UnitCatalog.HagiSingleT1), w, sd).R)) diff++;
                    }
            Expect("段0: 規定のハギは第403期の T1 と同じ動き（7 台 × 4 波 × seed 0..9）", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- 版の札と文面 ----
        {
            int n = hagi.Traits.Count - 1;
            bool ok = vers.All(x => x.Id == "hagi" && x.Name == hagi.Name && x.Traits.Take(n).SequenceEqual(hagi.Traits.SkipLast(1)) && x.Traits[^1] == TraitId.HagiSpillFixed && x.MinusText == hagi.MinusText && !UnitCatalog.Everyone.Contains(x))
                && l45.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiLineCap45 }) && r50.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiLineCap50, TraitId.HagiReapAfterHit })
                && r45.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiLineCap45, TraitId.HagiReapAfterHit })
                && l45.PlusText.Contains("（2 割 → 最大 45%）", StringComparison.Ordinal) && l45.PlusText.Contains("合わせて最大 45%", StringComparison.Ordinal) && l45.PlusText.StartsWith("大鎌で、弱った敵を刈り取る", StringComparison.Ordinal)
                && r50.PlusText.StartsWith("大鎌で斬り、線を切った敵はその一振りで刈り取る", StringComparison.Ordinal) && r50.PlusText.Contains("合わせて最大 5 割", StringComparison.Ordinal)
                && r45.PlusText.StartsWith("大鎌で斬り、線を切った敵はその一振りで刈り取る", StringComparison.Ordinal) && r45.PlusText.Contains("合わせて最大 45%", StringComparison.Ordinal)
                && !UnitCatalog.Everyone.Any(x => x.Traits.Contains(TraitId.HagiLineCap45) || x.Traits.Contains(TraitId.HagiLineCap50) || x.Traits.Contains(TraitId.HagiReapAfterHit));
            Expect("版の札と文面: L45 ＋ 線の上限 45% ／ R50 ＋ 上限 5 割 ＋ 斬った直後に刈る ／ R45 ＋ 上限 45% ＋ 斬った直後に刈る・線の数字と R の頭の句を替えた・固定の巻き込みが末尾・`All` ／ `Retired` の外・札の保持者 0 枚", ok);
        }

        // ---- L45 ----
        {
            // 隣（中央）に敵を置いて孤立の二度斬りを止め、振る前の刈り取りだけを見る（前1 の HP の割合がいちばん低いので的は前1）
            bool Reaps(UnitDef d, int hp) { var (c, h, _, e) = Bench(d, new[] { (0, FoeDef("foe404a"), hp), (2, FoeDef("foe404b"), 1000) }, soul: 9, marks: 5); e.First(u => u.Slot == 2).SetCounter(HagiDepthTrait.MarkKey, 0); Turn(c, h); return Tl(c, h).R388Reaps >= 1; }
            bool ok = !Reaps(l45, 460) && Reaps(l45, 450) && Reaps(hagi, 550) && !Reaps(l45, 550);
            var (_, hl, _, _) = Bench(l45, Array.Empty<(int, UnitDef, int)>());
            Expect("L45: 線の合計が 45% で止まる（魂 9 ＋ 印 5 本: 46% は刈らない ／ 45% は刈る・規定は 55% も刈る）", ok && HagiDepthTrait.LineCapOf(hl) == 450, $"L45 46% {Reaps(l45, 460)} ／ 45% {Reaps(l45, 450)}・規定 55% {Reaps(hagi, 550)}");
        }

        // ---- R50 ／ R45 ----
        {
            var notes = new List<string>(); bool ok = true;
            // 当たった後に線以下なら刈る（470 ＋ 印 5: 16 ＋ 150 → 304 ＝ 30.4% ≦ 45%）・規定は残る
            (int, UnitDef, int)[] adjFoes = { (0, FoeDef("foe404a"), 470), (2, FoeDef("foe404b"), 1000) };
            var (c1, h1, _, e1) = Bench(r45, adjFoes, marks: 5); At(e1, 2).SetCounter(HagiDepthTrait.MarkKey, 0); Turn(c1, h1);
            var (c0, h0, _, e0) = Bench(hagi, adjFoes, marks: 5); At(e0, 2).SetCounter(HagiDepthTrait.MarkKey, 0); Turn(c0, h0);
            bool g1 = !At(e1, 0).IsAlive && Tl(c1, h1).R404AfterReaps == 1 && Tl(c1, h1).R404AfterReapKills == 1 && At(e0, 0).IsAlive && Tl(c0, h0).R404AfterReaps == 0;
            ok &= g1; notes.Add($"当たった後に線以下なら刈る（470 → 304 ＝ 30.4% ≦ 45%・規定は {At(e0, 0).Hp} で残る）{(g1 ? "" : " ×")}");
            var (c1b, h1b, _, e1b) = Bench(r45, new[] { (0, FoeDef("foe404a"), 470) }, marks: 5); Turn(c1b, h1b);
            bool g1b = !e1b[0].IsAlive && Tl(c1b, h1b).R404AfterReaps == 1 && Tl(c1b, h1b).R403Twins == 0;
            ok &= g1b; notes.Add($"二度斬りの 1 発目で刈れば 2 発目は振らない（孤立・二度斬り {Tl(c1b, h1b).R403Twins}）{(g1b ? "" : " ×")}");
            // 線より上なら刈らない（1000 → 984）
            var (c2, h2, _, e2) = Bench(r45, new[] { (0, FoeDef("foe404a"), 1000) }); Turn(c2, h2);
            bool g2 = e2[0].IsAlive && Tl(c2, h2).R404AfterReaps == 0;
            ok &= g2; notes.Add($"線より上なら刈らない{(g2 ? "" : " ×")}");
            // 二度斬りの 2 発目の後にも判定（650 ＋ 印 5: 484 ＝ 48.4% → 2 発目 318 → 刈る）
            var (c3, h3, _, e3) = Bench(r45, new[] { (0, FoeDef("foe404a"), 650) }, marks: 5); Turn(c3, h3);
            bool g3 = !e3[0].IsAlive && Tl(c3, h3).R403Twins == 1 && Tl(c3, h3).R404AfterReaps == 1;
            ok &= g3; notes.Add($"二度斬りの 2 発目の後にも判定{(g3 ? "" : " ×")}");
            // 薙ぎの巻き込みも判定・手番で刈って倒せば再行動（魂 3 ＝ 薙ぎ・線 3 割 ＋ 印 2 ＝ 4 割）: 前1 420 → 328・中央 450 → 371 を刈る → 後3 へ再行動
            var (c4, h4, _, e4) = Bench(r50, new[] { (0, FoeDef("foe404a"), 420), (2, FoeDef("foe404b"), 450), (4, FoeDef("foe404c"), 1000) }, soul: 3, marks: 2);
            At(e4, 4).SetCounter(HagiDepthTrait.MarkKey, 0);
            Turn(c4, h4);
            var t4 = Tl(c4, h4);
            bool g4 = !At(e4, 0).IsAlive && !At(e4, 2).IsAlive && t4.R404AfterReapKills == 2 && t4.R388Encores >= 1 && At(e4, 4).Hp < 1000;
            ok &= g4; notes.Add($"薙ぎの巻き込みも判定（刈った {t4.R404AfterReapKills}）・再行動 {t4.R388Encores}（後3 1000 → {At(e4, 4).Hp}）{(g4 ? "" : " ×")}");
            // 追撃の後の刈り取りでは再行動しない: 仲間の一撃で 600 → 450 → 追撃（16 ＋ 110 ＋ 印 4 × 30）→ 204 ＝ 20.4% ≦ 4 割 → 刈る
            var (c5, h5, m5, e5) = Bench(r45, new[] { (0, FoeDef("foe404a"), 600) }, marks: 4); c5.ApplyDamage(e5[0], 150, m5);
            var t5 = Tl(c5, h5);
            bool g5 = !e5[0].IsAlive && t5.R404AfterReapsChase == 1 && t5.R404AfterReapKillsChase == 1 && t5.R388Encores == 0;
            ok &= g5; notes.Add($"追撃の後の刈り取り（{t5.R404AfterReapsChase}）では再行動しない{(g5 ? "" : " ×")}");
            // 再行動の上限 2: 前1 ／ 前3 ／ 後1 ／ 後3 に 1000 の敵 4 体（印 5・470）——刈り続けても 3 振り目まで
            var (c6, h6, _, e6) = Bench(r45, new[] { (0, FoeDef("foe404a"), 470), (1, FoeDef("foe404b"), 470), (3, FoeDef("foe404c"), 470), (4, FoeDef("foe404d"), 470) }, marks: 5); Turn(c6, h6);
            var t6 = Tl(c6, h6);
            bool g6 = t6.R388Encores == 2 && e6.Count(u => !u.IsAlive) >= 3;   // 魂が溜まると薙ぎになり、3 振り目で 2 体まとめて倒すこともある
            ok &= g6; notes.Add($"手番で刈って倒せば再行動（上限 2: 再行動 {t6.R388Encores}・倒した {e6.Count(u => !u.IsAlive)}）{(g6 ? "" : " ×")}");
            Expect("R50 ／ R45: 当たった後に線以下なら刈る・線より上なら刈らない・薙ぎの巻き込みも判定・手番で刈って倒せば再行動（上限 2）・追撃の刈り取りでは再行動しない・二度斬りの 1 発目で刈れば 2 発目を振らない", ok, string.Join("・", notes));
        }

        // ---- ボスの名前 ／ 種類で分岐していない ----
        {
            var hits = new List<string>();
            foreach (var f in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
                foreach (var l in File.ReadAllLines(f))
                {
                    if (!(l.Contains("第404期", StringComparison.Ordinal) || l.Contains("LineCap", StringComparison.Ordinal) || l.Contains("ReapAfterHit", StringComparison.Ordinal) || l.Contains("AfterHitReap404", StringComparison.Ordinal) || l.Contains("SwingCollect404", StringComparison.Ordinal))) continue;
                    string code = l.Contains("//") ? l[..l.IndexOf("//", StringComparison.Ordinal)] : l;
                    if (code.TrimStart().StartsWith("///", StringComparison.Ordinal)) continue;
                    if (code.Contains("Boss", StringComparison.Ordinal) || code.Contains("勇者", StringComparison.Ordinal) || code.Contains("Hero", StringComparison.Ordinal) || code.Contains("Def.Id", StringComparison.Ordinal) || code.Contains("Def.Name", StringComparison.Ordinal)) hits.Add(l.Trim());
                }
            Expect("ボスの名前 ／ 種類で分岐していない（第404期の札を読むコードの行に Boss ／ 勇者 ／ Hero ／ Def.Id ／ Def.Name が無い）", hits.Count == 0, hits.Count == 0 ? "" : string.Join(" ／ ", hits.Take(3)));
        }

        // ---- 規定の compare ----
        {
            var map = S390.BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var head = S390.BalanceMap(S390.GitShow($"{HeadSha}:docs/balance.md"));
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
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
            Expect($"規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致・ハギのいない行は第403期（{HeadSha}）の表と同じ", diff == 0 && miss == 0 && noHagiMoved == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}・ハギのいない行で動いたセル {noHagiMoved}");
        }

        // ---- 試遊の台 ----
        {
            var vrows = Presets.Versions;
            var hold = vrows.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool last = vrows[^1].Name == PlayName && vrows[^1].F.Occupied().Count(o => o.Def == PlayVersion) == 1 && Som307Diag.Seats(vrows[^1].F) == Som307Diag.Seats(PlayBoard())
                && hold.SequenceEqual(new[] { "版・大物 ハギ", PlayName }) && vrows.First(r => r.Name == "版・大物 ハギ").F[1] == UnitCatalog.HagiBigA404
                && vrows.First(r => r.Name == "試遊・ハギ 哀歌").F[3] == hagi && !vrows.Any(r => r.Name == "版・二度斬り ハギ");
            var h403 = VersionsH403();
            bool past = !h403.Any(r => VersionRows404.Contains(r.Name) || r.Name == "試遊・ハギ 哀歌") && h403.First(r => r.Name == "版・二度斬り ハギ").F[3] == UnitCatalog.HagiSingleT12A
                && h403.First(r => r.Name == "版・大物 ハギ").F[1] == UnitCatalog.HagiBigA403 && h403.SelectMany(r => r.F.Occupied()).All(o => o.Def != hagi) && VersionsH402().Length == h403.Length - 1;
            bool inside = !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(vrows.Where(r => r.Name is not ("版・大物 ハギ" or PlayName))).SelectMany(r => r.F.Occupied()).Any(o => vers.Contains(o.Def));
            Expect($"試遊の台: 「版・二度斬り ハギ」→「試遊・ハギ 哀歌」（規定のハギ）・「版・大物 ハギ」は規定 ＋ A（`HagiBigA404`）・末尾に「{PlayName}」（{PlayVersionName}）・第403期の一覧は `VersionsH403`・替えた台 ／ 版は器具の中だけ",
                last && past && inside, $"{string.Join("、", vrows.TakeLast(2).Select(r => r.Name))}・{Som307Diag.Seats(vrows[^1].F)}・第403期 {past}・中だけ {inside}");
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool Row(TraitId t, string pat) => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(HcPlusL,\s*""[^""]*{pat}""");
            var miss = new[] { TraitId.HagiLineCap45, TraitId.HagiLineCap50, TraitId.HagiReapAfterHit }.Where(t => !Row(t, "第404期[^\"]*保持者 0 枚")).Concat(new[] { TraitId.HagiTwinIsolated }.Where(t => !Row(t, "第403期[^\"]*第404期から規定（ハギ）"))).ToList();
            Expect("`checkup` の札の分類表: 第404期の札 3 枚（保持者 0 枚）・規定になった T1（孤立した敵は二度斬る）は「第404期から規定（ハギ）」（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var bd in StopBoards().Concat(CarryHand()))
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
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（7 台 × 版 4 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1c = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（第403期（{HeadSha}）と同じ数）", e0 == e1 && t0 == t1c && r0 == r1 && re0 == re1 && e0 > 0, $"PickOne engine {e0} → {e1}・traits {t0} → {t1c}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
