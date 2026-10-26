using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh402 check —— 第402期の自己検査（指示書 §7 の「新しい器具に最低限入れる」）。
static partial class Sh402Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo ReapTurnM = typeof(BattleContext).GetMethod("ReapTurn388", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "c8871366";   // 第401期の HEAD（この期の作業を始めたときの BattleCore ／ BattleSim）
    // ---- §6 試遊の台（「版・大物 ハギ」）----
    internal const string PlayName = "版・大物 ハギ";
    internal static UnitDef PlayVersion => UnitCatalog.HagiSoulA;
    internal const string PlayVersionName = "A";
    internal static Formation PlayBoard() => Presets.HagiBig402();
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate402", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitDef FoeDef(string id, int hp) => new() { Id = id, Name = "的" + id[^1], MaxHp = hp, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static (BattleContext Ctx, UnitState H, UnitState M, List<UnitState> E) Bench(UnitDef hagi, (int Slot, UnitDef D, int Hp)[] foes, int marks = 0)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); f[3] = hagi; f[2] = Mate;
        var g = new Formation(); foreach (var (s, d, _) in foes) g[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var (s, _, hp) in foes) e.First(u => u.Slot == s).Hp = hp;
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        if (marks > 0) foreach (var u in e) u.SetCounter(HagiDepthTrait.MarkKey, marks);
        return (ctx, p.First(u => u.Def.Id == "hagi"), p.First(u => u.Def.Id == "mate402"), e);
    }
    static int Soul(UnitState h) => HagiDepthTrait.Soul(h);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh402 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef hagi = UnitCatalog.Hagi, old = UnitCatalog.HagiH401, a = UnitCatalog.HagiSoulA, d = UnitCatalog.HagiSoulD, ad = UnitCatalog.HagiSoulAD, b = UnitCatalog.HagiSoulB;
        var vers = new[] { a, d, ad, b };

        // ---- 段0 ----
        {
            bool cards = hagi.Traits.SequenceEqual(UnitCatalog.HagiCarryH123.Traits) && hagi.PlusText == UnitCatalog.HagiCarryH123.PlusText && hagi.MinusText == UnitCatalog.HagiCarryH123.MinusText
                && hagi != UnitCatalog.HagiCarryH123 && UnitCatalog.All.Contains(hagi) && hagi.Id == "hagi" && hagi.Name == old.Name;
            bool oldOk = old.Traits.SequenceEqual(UnitCatalog.HagiP1h.Traits) && old.PlusText == UnitCatalog.HagiP1h.PlusText && !UnitCatalog.Everyone.Contains(old) && old.Id == "hagi";
            bool pins = PinH401(hagi) == old && PinH401(old) == old && PinT399(hagi) == old && PinH391(hagi) == UnitCatalog.HagiH391 && PinH391(old) == UnitCatalog.HagiH391
                && PinH389(old) == UnitCatalog.HagiH389 && Pin401(Presets.HagiCarry401())[3] == old && PinS398(hagi) == old && PinX397(hagi) == old;
            bool v401 = new[] { UnitCatalog.HagiCarryH1, UnitCatalog.HagiCarryH2, UnitCatalog.HagiCarryH3, UnitCatalog.HagiCarryH123, UnitCatalog.HagiCarryH123L }
                .All(x => x.Traits.Take(old.Traits.Count - 1).SequenceEqual(old.Traits.SkipLast(1)) && (x == UnitCatalog.HagiCarryH123L || x.PlusText.StartsWith(old.PlusText, StringComparison.Ordinal)))   // H123L は旧の文面の線の数字を替えたもの
                && UnitCatalog.HagiCarryH123L.PlusText.StartsWith(old.PlusText.Replace("（2 割 → 最大 5 割）", "（1 割 → 最大 4 割）").Replace("合わせて最大 6 割", "合わせて最大 5 割"), StringComparison.Ordinal)
                && UnitCatalog.HagiCarryH2.Traits.SequenceEqual(old.Traits);
            Expect("段0: 規定のハギ ＝ 第401期の H123 と同じ札と文面（別の定義・`All` の中）・旧 `HagiH401` ＝ P1h（`All` ／ `Retired` の外）・固定の口（`PinH401` ／ `Pin401`・`PinT399` の終端 ／ `PinH391` ／ `PinH389` も旧へ）・第401期の版の駒は旧から作ったまま",
                cards && oldOk && pins && v401, $"規定 {string.Join("・", hagi.Traits.Skip(old.Traits.Count - 1).SkipLast(1))} ／ 札 {cards}・旧 {oldOk}・口 {pins}・第401期の版 {v401}");
        }
        {
            long diff = 0, cells = 0;
            foreach (var bd in CarryHand().Concat(StopBoards()).Concat(DeathBoards()))
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        cells++;
                        if (!Same(I.Raw(ColBoard(bd, hagi), w, sd).R, I.Raw(ColBoard(bd, UnitCatalog.HagiCarryH123), w, sd).R)) diff++;
                    }
            Expect("段0: 規定のハギは第401期の H123 と同じ動き（9 台 × 4 波 × seed 0..9）", diff == 0, $"{cells} 戦・差 {diff}");
        }

        // ---- 版の札と文面 ----
        {
            var cards = new[] { TraitId.HagiBigSoul, TraitId.HagiMarkSoul, TraitId.HagiAllySoul };
            int n = hagi.Traits.Count - 1;
            bool ok = vers.All(x => x.Id == "hagi" && x.Name == hagi.Name && x.Traits.Take(n).SequenceEqual(hagi.Traits.SkipLast(1)) && x.Traits[^1] == TraitId.HagiSpillFixed
                    && x.PlusText.StartsWith(hagi.PlusText, StringComparison.Ordinal) && x.MinusText == hagi.MinusText && !UnitCatalog.Everyone.Contains(x))
                && a.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiBigSoul }) && d.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiMarkSoul })
                && ad.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiBigSoul, TraitId.HagiMarkSoul }) && b.Traits.Skip(n).SkipLast(1).SequenceEqual(new[] { TraitId.HagiAllySoul })
                && a.PlusText.Contains("大きな命ほど、魂は重い", StringComparison.Ordinal) && d.PlusText.Contains("印を刻み切ると、その命から魂を刈り取る", StringComparison.Ordinal)
                && b.PlusText.Contains("仲間が倒れても、魂は鎌に宿る", StringComparison.Ordinal) && ad.PlusText == a.PlusText + d.PlusText[hagi.PlusText.Length..]
                && !UnitCatalog.Everyone.Any(x => x.Traits.Any(cards.Contains));
            Expect("版の札と文面: A ＋ 大物の魂 ／ D ＋ 印が魂に ／ AD ＋ 両方 ／ B ＋ 味方の死でも魂・規定の文面の末尾に叩き台・固定の巻き込みが末尾・`All` ／ `Retired` の外・札の保持者 0 枚", ok);
        }

        // ---- A ----
        {
            var notes = new List<string>(); bool ok = true;
            int DeathSoul(UnitDef h, int maxHp) { var (c, hh, m, e) = Bench(h, new[] { (0, FoeDef("foe402a", maxHp), 10) }); c.ApplyDamage(e[0], 10, m); return e[0].IsAlive ? -1 : Soul(hh); }
            int LineSoul(UnitDef h, int maxHp) { var (c, hh, m, e) = Bench(h, new[] { (0, FoeDef("foe402a", maxHp), maxHp) }); c.ApplyDamage(e[0], maxHp / 4, m); return Soul(hh); }
            int a1 = DeathSoul(a, 1000), a0 = DeathSoul(a, 999), h1 = DeathSoul(hagi, 1000), l1 = LineSoul(a, 1000), l0 = LineSoul(a, 999), lh = LineSoul(hagi, 1000);
            bool g = a1 == 6 && a0 == 2 && h1 == 2 && l1 == 3 && l0 == 1 && lh == 1;
            ok &= g; notes.Add($"倒れる: 最大 HP 1,000 → +{a1} ／ 999 → +{a0}（規定 +{h1}）・8 割を割る: 1,000 → +{l1} ／ 999 → +{l0}（規定 +{lh}）");
            var (c2, h2, m2, e2) = Bench(a, new[] { (0, FoeDef("foe402a", 1000), 10) }); HagiDepthTrait.Gain(c2, h2, 5, 0); c2.ApplyDamage(e2[0], 10, m2);
            bool g2 = Soul(h2) == HagiDepthTrait.Cap && Tl(c2, h2).R402SoulBig == 4;
            ok &= g2; notes.Add($"上限 9 は今どおり（魂 5 ＋ 6 → {Soul(h2)}）{(g2 ? "" : " ×")}");
            Expect("A: 最大 HP 1,000 以上の敵で魂の増え（倒れる ／ 線を割る）が 3 倍・999 では今どおり・上限 9", ok, string.Join("・", notes));
        }

        // ---- D ----
        {
            var notes = new List<string>(); bool ok = true;
            foreach (var (nm, def) in new[] { ("D", d), ("AD", ad) })
            {
                var (c, h, _, e) = Bench(def, new[] { (0, FoeDef("foe402a", 1000), 1000) }, marks: 4);
                ReapTurnM.Invoke(c, new object[] { h });
                var t = Tl(c, h);
                int m1 = e[0].RawCounter(HagiDepthTrait.MarkKey), s1 = Soul(h);
                e[0].SetCounter(HagiDepthTrait.MarkKey, 4);
                ReapTurnM.Invoke(c, new object[] { h });
                int m2 = e[0].RawCounter(HagiDepthTrait.MarkKey);
                bool g = m1 == 1 && s1 == 3 && t.R402MarkSouls == 2 && t.R402SoulMark == 6 && m2 == 1 && t.R402SoulBig == (nm == "AD" ? Tl(c, h).R402SoulBig : 0) && t.R402MarkSoulLostAdd == 2 * 4 * 30;
                ok &= g; notes.Add($"{nm}: 印 4 → 5 で 1 に戻り魂 {s1}・もう一度刻んで {t.R402MarkSouls} 回（魂の D の分 {t.R402SoulMark}・A の上乗せ {t.R402SoulBig}）{(g ? "" : " ×")}");
            }
            { var (c, h, _, e) = Bench(ad, new[] { (0, FoeDef("foe402a", 1000), 1000) }, marks: 4); ReapTurnM.Invoke(c, new object[] { h }); bool g = Soul(h) == 3 && Tl(c, h).R402SoulBig == 0; ok &= g; notes.Add($"AD の D の +3 は 3 倍にならない（最大 HP 1,000 の敵で魂 {Soul(h)}）{(g ? "" : " ×")}"); }
            { var (c, h, _, e) = Bench(hagi, new[] { (0, FoeDef("foe402a", 1000), 1000) }, marks: 4); ReapTurnM.Invoke(c, new object[] { h }); bool g = e[0].RawCounter(HagiDepthTrait.MarkKey) == 5 && Soul(h) == 0; ok &= g; notes.Add($"規定は印 5 のまま・魂 0{(g ? "" : " ×")}"); }
            Expect("D: ハギの一撃で印が 5 に達すると 1 に戻り魂 +3・何度でも・AD の +3 は A の 3 倍の対象にしない", ok, string.Join("・", notes));
        }

        // ---- B ----
        {
            var (c, h, m, _) = Bench(b, new[] { (0, FoeDef("foe402a", 1000), 1000) }); c.ApplyDamage(m, 999, null);
            var (c0, h0, m0, _) = Bench(hagi, new[] { (0, FoeDef("foe402a", 1000), 1000) }); c0.ApplyDamage(m0, 999, null);
            var (c1, h1, m1, e1) = Bench(b, new[] { (0, FoeDef("foe402a", 1000), 1000) }); c1.ApplyDamage(h1, 999, null);
            bool ok = !m.IsAlive && Soul(h) == 1 && Tl(c, h).R402SoulAlly == 1 && Soul(h0) == 0 && !h1.IsAlive && Tl(c1, h1).R402SoulAlly == 0;
            Expect("B: 味方が倒れると魂 +1・規定は 0・ハギ自身は除く", ok, $"B {Soul(h)} ／ 規定 {Soul(h0)} ／ ハギ自身の死で {Tl(c1, h1).R402SoulAlly}");
        }

        // ---- ボスの名前 ／ 種類で分岐していない ----
        {
            var hits = new List<string>();
            foreach (var f in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
                foreach (var l in File.ReadAllLines(f))
                {
                    if (!l.Contains("第402期", StringComparison.Ordinal) && !l.Contains("HagiBigSoul", StringComparison.Ordinal) && !l.Contains("HagiMarkSoul", StringComparison.Ordinal) && !l.Contains("HagiAllySoul", StringComparison.Ordinal) && !l.Contains("BigExtra", StringComparison.Ordinal)) continue;
                    string code = l.Contains("//") ? l[..l.IndexOf("//", StringComparison.Ordinal)] : l;
                    if (code.TrimStart().StartsWith("///", StringComparison.Ordinal)) continue;
                    if (code.Contains("Boss", StringComparison.Ordinal) || code.Contains("勇者", StringComparison.Ordinal) || code.Contains("Hero", StringComparison.Ordinal) || code.Contains("Def.Id", StringComparison.Ordinal) || code.Contains("Def.Name", StringComparison.Ordinal)) hits.Add(l.Trim());
                }
            Expect("ボスの名前 ／ 種類で分岐していない（第402期の札を読むコードの行に Boss ／ 勇者 ／ Hero ／ Def.Id ／ Def.Name が無い・判定は最大 HP ／ 印 ／ 味方の死だけ）", hits.Count == 0, hits.Count == 0 ? "" : string.Join(" ／ ", hits.Take(3)));
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
            Expect($"規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致・ハギのいない行は第401期（{HeadSha}）の表と同じ", diff == 0 && miss == 0 && noHagiMoved == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}・ハギのいない行で動いたセル {noHagiMoved}");
        }

        // ---- 試遊の台 ----
        {
            var vrows = Presets.Versions;
            var hold = vrows.Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool last = vrows[^1].Name == PlayName && hold.SequenceEqual(new[] { PlayName }) && vrows[^1].F.Occupied().Count(o => o.Def == PlayVersion) == 1 && Som307Diag.Seats(vrows[^1].F) == Som307Diag.Seats(PlayBoard());
            var carry = vrows.FirstOrDefault(r => r.Name == "試遊・ハギ キャリー");
            bool renamed = carry.F is not null && carry.F[3] == hagi && !vrows.Any(r => r.Name == "版・死神 キャリー");
            var h401 = VersionsH401();
            bool past = h401[^1].Name == "版・死神 キャリー" && h401[^1].F[3] == UnitCatalog.HagiCarryH123 && !h401.Any(r => VersionRows402.Contains(r.Name) || r.Name == "試遊・ハギ キャリー")
                && h401.SelectMany(r => r.F.Occupied()).All(o => o.Def != hagi) && VersionsH400().Length == h401.Length - 1;
            bool inside = !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(vrows.SkipLast(1)).SelectMany(r => r.F.Occupied()).Any(o => vers.Contains(o.Def));
            Expect($"試遊の台: 「版・死神 キャリー」→「試遊・ハギ キャリー」（規定のハギ）・末尾に「{PlayName}」（{PlayVersionName}）・第401期の一覧は `VersionsH401`（名前と版の H123 を戻し、ほかのハギは旧）・替えた台 ／ 版は器具の中だけ",
                last && renamed && past && inside, $"{string.Join("、", vrows.TakeLast(2).Select(r => r.Name))}・{Som307Diag.Seats(vrows[^1].F)}・改名 {renamed}・第401期 {past}・中だけ {inside}");
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool Row(TraitId t, string pat) => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(HcPlusL,\s*""[^""]*{pat}""");
            var miss = new[] { TraitId.HagiBigSoul, TraitId.HagiMarkSoul, TraitId.HagiAllySoul }.Where(t => !Row(t, "第402期[^\"]*保持者 0 枚")).Concat(new[] { TraitId.HagiMarkWeight, TraitId.HagiStageMul }.Where(t => !Row(t, "第401期[^\"]*第402期から規定（ハギ）"))).ToList();
            Expect("`checkup` の札の分類表: 第402期の札 3 枚（保持者 0 枚）・規定になった 2 枚（印が重い ／ 段で倍率）は「第402期から規定（ハギ）」（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var bd in CarryHand().Concat(StopBoards()).Concat(DeathBoards()))
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
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（9 台 × 版 5 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（第401期（{HeadSha}）と同じ数）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0, $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
