using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;

// sh387 check —— 第387期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh387Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    const string HeadSha = "b98823d4";   // 第386期の HEAD（この期の作業を始めたコミット）
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static readonly UnitDef Mate = new() { Id = "mate387", Name = "仲間", MaxHp = 50, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe387", Name = "的", MaxHp = 999, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(params (int Slot, UnitDef D)[] seats)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation();
        foreach (var (s, d) in seats) f[s] = d;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(Formation.Build(center: Foe), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, 1);
        return (ctx, p, e);
    }
    static List<UnitState> Living(BattleContext ctx, string id) => ctx.LivingMembers(BattleContext.PlayerTeam).Where(u => u.Def.Id == id).ToList();

    static string Git(string spec)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", $"show {spec}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x;
        }
        catch { return ""; }
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh387 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 段0: 規定の胞子 ／ ムグの札と文面 ----
        {
            var s = UnitCatalog.Spore; var m = UnitCatalog.Mug; var h = UnitCatalog.MugH386; var sh = UnitCatalog.SporeH386;
            bool spore = ReferenceEquals(s, UnitCatalog.SporeP) && s.Id == "spore" && s.Name == "胞子" && s.MaxHp == 14 && s.Attack == 0 && s.Speed == 10
                && s.Traits.SequenceEqual(new[] { TraitId.Ephemeral, TraitId.SporeIdle, TraitId.SporeBurst }) && s.Actions is null;
            bool mug = m.Id == "mug" && m.Name == h.Name && m.MaxHp == h.MaxHp && m.Attack == h.Attack && m.Speed == h.Speed && m.Actions == h.Actions
                && m.Traits.SequenceEqual(UnitCatalog.MugP.Traits) && m.Traits.SequenceEqual(h.Traits.Append(TraitId.MugSporeP))
                && m.PlusText == h.PlusText + "／胞子は殴られて弾けると、殴った敵に毒を撒く" && m.MinusText == h.MinusText && m.Flavor == h.Flavor
                && UnitCatalog.All.Contains(m) && !UnitCatalog.Everyone.Contains(h) && !UnitCatalog.Everyone.Contains(sh);
            bool old = h.Traits.SequenceEqual(UnitCatalog.MugSowAll.Traits) && h.PlusText == UnitCatalog.MugSowAll.PlusText
                && sh.Id == "spore" && sh.Name == "胞子" && sh.MaxHp == 14 && sh.Attack == 4 && sh.Speed == 10 && sh.Traits.SequenceEqual(new[] { TraitId.Ephemeral }) && sh.Actions is null
                && new[] { UnitCatalog.MugS0, UnitCatalog.MugP, UnitCatalog.MugQ, UnitCatalog.MugPQ }.All(d => d.Traits.Take(h.Traits.Count).SequenceEqual(h.Traits) && d.MinusText == h.MinusText && d.PlusText.StartsWith(h.PlusText, StringComparison.Ordinal));
            Expect("段0 の札と文面: 規定の胞子 ＝ 第386期の `SporeP`（同じ駒・攻撃力 0・殴らない ＋ 弾けて毒・HP 14 ／ 速 10 ／ 儚い）・規定のムグ ＝ 旧（撒き全 ＋ 分裂）＋ 印 P（札 ／ 文面は `MugP` と同じ・プラスに「胞子は殴られて弾けると、殴った敵に毒を撒く」）・旧 `MugH386` ／ `SporeH386` は `All` ／ `Retired` の外・第386期の版のムグは旧から作ったまま",
                spore && mug && old, $"規定のムグの札 {string.Join("・", m.Traits)}");
        }

        // ---- 段0: 出す胞子 ----
        {
            var notes = new List<string>(); bool ok = true;
            var cases = new (string Name, UnitDef Mug, UnitDef Want)[]
            {
                ("規定", UnitCatalog.Mug, UnitCatalog.Spore), ("旧 H386", UnitCatalog.MugH386, UnitCatalog.SporeH386), ("旧 H379", UnitCatalog.MugH379, UnitCatalog.SporeH386),
                ("第379期 撒き全", UnitCatalog.MugSowAll, UnitCatalog.SporeH386), ("第386期 S0", UnitCatalog.MugS0, UnitCatalog.SporeS0), ("第386期 Q", UnitCatalog.MugQ, UnitCatalog.SporeQ),
            };
            foreach (var (name, md, want) in cases)
            {
                var (c1, p1, e1) = Board((2, md), (4, Mate));
                var mu = p1.First(u => u.Def.Id == "mug");
                bool sows = md.Actions is { Count: > 0 };
                if (sows) foreach (var t in mu.Traits) t.OnAction(c1, mu, md.Actions![0]);
                var sown = Living(c1, "spore");
                var (c2, p2, e2) = Board((2, md), (4, Mate));
                c2.ApplyDamage(p2.First(u => u.Def.Id == "mug"), 9999, e2[0]);
                var split = Living(c2, "spore");
                bool good = (!sows || (sown.Count == 4 && sown.All(u => u.Def == want))) && split.Count == 2 && split.All(u => u.Def == want);
                ok &= good;
                notes.Add($"{name} 撒 {sown.Count}・分裂 {split.Count}{(good ? "" : " ×")}");
            }
            // 第377期の芽吹き（版 M）は旧の胞子
            {
                var (c, p, e) = Board((2, UnitCatalog.MugSproutM), (4, Mate), (0, Mate));
                c.ApplyDamage(p.First(u => u.Slot == 4), 9999, e[0]);
                var sp = Living(c, "spore");
                bool good = sp.Count == 1 && sp[0].Def == UnitCatalog.SporeH386; ok &= good; notes.Add($"第377期 芽吹き {sp.Count}{(good ? "" : " ×")}");
            }
            Expect("段0 の出す胞子: 規定のムグは規定の胞子を撒き ／ 分裂で出す・旧のムグ（H386 ／ H379 ／ 第379期の撒き全 ／ 第377期の芽吹き）は旧の胞子・第386期の版のムグは版の胞子", ok, string.Join("・", notes));
        }

        // ---- 段0: 規定のムグ ＝ 第386期の MugP（台を比べて差 0）----
        {
            long cells = 0, diff = 0, evd = 0;
            var boards = new[] { Presets.DeathChain377(), Presets.DeathSeek380(), Presets.DeathSom379(), Presets.DeathBard382(UnitCatalog.Ere) };
            foreach (var f in boards)
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 5; sd++)
                    {
                        cells++;
                        var a = I.Raw(f, w, sd, verbose: true).R; var b = I.Raw(Replace(f, "mug", UnitCatalog.MugP), w, sd, verbose: true).R;
                        if (!Same(a, b)) diff++;
                        if (a.Events.Count != b.Events.Count || a.Log.Count != b.Log.Count) evd++;
                    }
            var rows = CompareBuilds().Where(r => Has(r.F, "mug")).ToArray();
            var g0 = CGrid(rows, 20); var g1 = CGrid(rows.Select(r => (r.Name, Replace(r.F, "mug", UnitCatalog.MugP))).ToArray(), 20);
            int cd = 0; for (int i = 0; i < rows.Length; i++) for (int w = 0; w < EnemyCatalog.Stages.Count; w++) if (g0[i, w] != g1[i, w]) cd++;
            Expect("段0: 規定のムグは第386期の `MugP` と同じ動き（ムグのいる試遊の台 4 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..4 の勝敗 ／ T ／ 生存数 ／ 出来事の数 ／ ログの数・`compare` のムグのいる行 × 5 波 × seed 0..19）", diff == 0 && evd == 0 && cd == 0 && rows.Length > 0,
                $"{cells} 戦・差 {diff}・出来事 ／ ログの数の差 {evd}・`compare` {rows.Length} 行のずれ {cd}");
        }

        // ---- 段0: 固定の口 ／ 試遊の台 ----
        {
            var f = Presets.DeathChain377();
            bool pin = PinMug386(UnitCatalog.Mug) == UnitCatalog.MugH386 && PinMug386(UnitCatalog.MugQ) == UnitCatalog.MugQ && Pin386(f)[2] == UnitCatalog.MugH386 && Pin386(f)[0] == f[0]
                && PinM379(UnitCatalog.Mug) == UnitCatalog.MugH379;
            var vr = Presets.Versions; var v386 = VersionsH386();
            bool play = !vr.Any(r => r.Name == "版・弾け 死の連鎖") && vr[^1].Name == "版・寄生 死の連鎖" && vr[^1].F[2] == UnitCatalog.MugQ
                && v386.Length == vr.Length + 1 && v386[^2].Name == "版・弾け 死の連鎖" && v386[^2].F[2] == UnitCatalog.MugP && v386[^1].Name == "版・寄生 死の連鎖"
                && v386.Where(r => r.Name.StartsWith("試遊・", StringComparison.Ordinal)).All(r => !r.F.Occupied().Any(o => o.Def == UnitCatalog.Mug))
                && VersionsH385().Length == vr.Length - 1;
            Expect("段0 の固定の口: `PinMug386` ／ `Pin386` は規定のムグだけを旧（`MugH386`）へ・`PinM379` は今も第379期までの旧へ・試遊の台は「版・弾け」を外し「版・寄生」を残す・`VersionsH386` は第386期の一覧（ムグは旧・「版・弾け」を足し戻す）",
                pin && play, string.Join("、", vr.Select(r => r.Name)));
        }

        // ---- 段0: 過去の器具の胞子の固定（ソース）／ checkup の札の表 ----
        {
            var files = new[] { "BattleSim/Modes/Relic.cs", "BattleSim/Modes/Som314.cs", "BattleSim/Modes/Sh377.Check.cs", "BattleSim/Modes/Sh378.Check.cs", "BattleSim/Modes/Sh381.Check.cs", "BattleSim/Modes/Sh382.Check.cs", "BattleSim/Modes/Sh383.Check.cs", "BattleSim/Modes/Sh384.Check.cs" };
            var rx = new System.Text.RegularExpressions.Regex(@"UnitCatalog\.Spore[,;)!]");
            var bad = files.Where(p => rx.IsMatch(File.ReadAllText(p))).ToList();
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool cu = new[] { TraitId.SporeIdle, TraitId.SporeBurst, TraitId.MugSporeP }.All(t => System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第387期から規定（胞子 ／ ムグ）"));
            Expect("過去の器具（芽吹き ／ 寄生以外で胞子を直に呼ぶ 8 本）は旧の胞子（`SporeH386`）を呼ぶ・`checkup` の札の分類表で殴らない ／ 弾けて毒 ／ 印 P が「第387期から規定（胞子 ／ ムグ）」（ソースを読んで確かめる）",
                bad.Count == 0 && cu, bad.Count == 0 ? "" : "今の胞子を呼ぶ: " + string.Join("・", bad));
        }

        // ---- 棚卸し: 素体 ／ 替えた台は器具の中だけ・規定の駒を変えていない ----
        {
            var plains = Ten.Select(Plain).ToArray();
            bool plain = plains.All(p => p.Traits.Count == 0 && p.Id.EndsWith("_plain387", StringComparison.Ordinal) && p.Actions is null)
                && Ten.Zip(plains).All(x => x.First.MaxHp == x.Second.MaxHp && x.First.Attack == x.Second.Attack && x.First.Speed == x.Second.Speed && x.First.Pattern == x.Second.Pattern && x.First.Advances == x.Second.Advances);
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(Presets.Versions).SelectMany(r => r.F.Occupied()).Select(o => o.Def.Id).ToHashSet();
            bool inside = !everywhere.Any(id => id.EndsWith("_plain387", StringComparison.Ordinal)) && !UnitCatalog.Everyone.Any(d => d.Id.EndsWith("_plain387", StringComparison.Ordinal));
            var b0 = Play(); var before = b0.Select(r => S307Seats(r.F)).ToArray();
            _ = Put(b0[0].F, 0, Ten[0]); _ = Replace(b0[0].F, b0[0].F[0]!.Id, Plain(Ten[0]));
            bool untouched = Play().Select(r => S307Seats(r.F)).SequenceEqual(before);
            string head = Git($"{HeadSha}:docs/units.md"), now = File.Exists("docs/units.md") ? File.ReadAllText("docs/units.md") : "";
            string Row(string doc, UnitDef d) => doc.Split('\n').FirstOrDefault(l => l.StartsWith($"| **{d.Name}** |", StringComparison.Ordinal))?.TrimEnd('\r') ?? "";
            var moved = Ten.Where(d => Row(head, d) == "" || Row(head, d) != Row(now, d)).Select(d => Short(d)).ToList();
            Expect($"棚卸し: 素体（同じ体・札なし・ふつうに殴る）／ 替えた台は器具の中だけ（`compare` ／ 交差帯 ／ `Playtest` ／ `Versions` ／ `All` ／ `Retired` に無い・替えても元の台は変わらない）・10 枚の `docs/units.md` の行が第386期の HEAD（{HeadSha}）と同じ",
                plain && inside && untouched && moved.Count == 0, moved.Count == 0 ? "" : "動いた行: " + string.Join("・", moved));
        }

        // ---- 札の働きの数え方が 0 でない（台本）----
        {
            var notes = new List<string>(); bool ok = true;
            var boards = Play();
            foreach (var d in Ten)
            {
                var w = WorkOf(d.Id); long a = 0, n = 0;
                foreach (var (_, f0) in boards.Take(6))
                {
                    if (Has(f0, d.Id)) continue;
                    var f = Put(f0, Seats5(f0)[0], d);
                    foreach (var ww in new[] { I.Boss, I.Guard })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            var (r, p, _) = I.Raw(f, ww, sd, verbose: true);
                            var u = p.First(x => x.Def.Id == d.Id); a += w.FA(r, u) + w.FB(r, u); n++;
                        }
                }
                notes.Add($"{Short(d)} {a}");
                ok &= n > 0;
            }
            Expect("札の働きの数え方（駒ごとに2つ）が台本から引ける（試遊の台 6 × 前1 に置いて ボス ／ 近衛 × seed 0..1 の和・0 は札が働かなかったことを示す）", ok, string.Join("・", notes));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            var boards = Play();
            foreach (var d in Ten)
                foreach (var (_, f0) in boards.Where((_, i) => i % 4 == 0))
                {
                    if (Has(f0, d.Id)) continue;
                    var f = Put(f0, Seats5(f0)[2], d);
                    foreach (var w in new[] { I.Boss, I.Bat })
                    {
                        cells++;
                        var r0 = I.Raw(f, w, 1).R;
                        if (!Same(r0, I.Raw(f, w, 1, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, 1).R)) det++;
                    }
                }
            foreach (var f in new[] { Presets.DeathChain377(), Presets.DeathBard382(UnitCatalog.Ere) })
                foreach (var w in new[] { I.Boss, I.Guard, I.M5 })
                    for (int sd = 0; sd < 3; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（替えた台 ／ 規定のムグの台）", diff == 0 && det == 0 && cells > 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- 規定の compare 全行 ----
        {
            var bal = File.Exists("docs/balance.md") ? File.ReadAllLines("docs/balance.md") : Array.Empty<string>();
            var map = new Dictionary<string, string[]>();
            foreach (var l in bal) { var c = l.Split('|').Select(x => x.Trim()).ToArray(); if (c.Length >= 8 && c[2].EndsWith('%')) map[c[1]] = c.Skip(2).Take(5).ToArray(); }
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var c)) { miss++; continue; }
                for (int w = 0; w < 5; w++) if (c[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            Expect("規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致（段0 の後に再生成した表）", diff == 0 && miss == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = Git($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = Git($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, t0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), t1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第386期の HEAD（{HeadSha}）と同じ）", e0 == e1 && t0 == t1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {t0} → {t1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    static string S307Seats(Formation f) => Som307Diag.Seats(f);
}
