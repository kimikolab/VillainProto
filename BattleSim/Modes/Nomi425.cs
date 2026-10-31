using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;

// =====================================================================================
// nomi425 —— 第425期【規定化の期】刻みのノミ ＝ 第424期の NM-wc（毎発刻む・読みは一発に 20 まで・旧は `NomiH424`）。
// 指示書は design/PHASE425_NOMI_SETTLE_SPEC.md ／ 報告は design/PHASE425_NOMI_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 nomi425 expect   # §1-3 ／ §1-4: ノミを含む `compare` の行 × {旧（`NomiH424`・段0 の前の規定）, NM-wc} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199（`.tmp/p425/expect.tsv` に書く）
//     dotnet run --project BattleSim -c Release 0 nomi425 verify   # 段0 の後: `docs/balance.md` ／ `docs/elite.md` を期待値と段0 の前の写し（`.tmp/p425/*_before.md`）に突き合わせる
//     dotnet run --project BattleSim -c Release 0 nomi425 confirm  # §1-8: 「試遊・刻み ノミ」と 燃焼×刻み × 4 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 nomi425 bomb     # §3: 爆弾の新駒の材料（3 台 × 4 波・seed 0..99・規定のまま数えるだけ）
//     dotnet run --project BattleSim -c Release 0 nomi425 check    # 自己検査
// =====================================================================================
static class Nomi425Diag
{
    const string ExpectFile = ".tmp/p425/expect.tsv";
    const string BalanceBefore = ".tmp/p425/balance_before.md";
    const string EliteBefore = ".tmp/p425/elite_before.md";
    const string PlayRow = "試遊・刻み ノミ";
    static readonly I.W[] Four = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    static I.W[] Waves => L.Mains.Append(I.Guard).Append(I.Bat).ToArray();
    static readonly string[] BombBoards = { "試遊・燃焼 ポンの台", "試遊・自傷", "試遊・呪い" };

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");
    static Formation Swap(Formation f, UnitDef d)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "nomi") g[o.Slot] = d;
        return g;
    }
    /// <summary>ノミを含む行（`compare` の帯 ＋ 交差帯）。指示書 §1-3 の 9 行のうち 傷×被弾 ／ 毒×傷 は交差帯（`CrossBuilds`）にある。</summary>
    static (string Name, Formation F, bool Cross)[] NomiRows() =>
        CompareBuilds().Select(r => (r.Name, r.F, Cross: false)).Concat(CrossBuilds().Select(r => (r.Name, r.F, Cross: true)))
            .Where(r => r.F.Occupied().Any(o => o.Def.Id == "nomi")).ToArray();

    static double Win(Formation f, I.W w, int n)
    {
        var won = new bool[n];
        Parallel.For(0, n, s => won[s] = I.Raw(f, w, s, census: false).R.PlayerWon);
        return 100.0 * won.Count(x => x) / n;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "expect": Expect(); break;
            case "verify": Verify(); break;
            case "confirm": Confirm(); break;
            case "bomb": Bomb(); break;
            case "check": Check(); break;
            default: Console.WriteLine("nomi425: モードは expect / verify / confirm / bomb / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---- §1-3 ／ §1-4: 期待値 ----
    static void Expect()
    {
        var waves = Waves;
        Console.WriteLine("# 第425期 §1-3 ／ §1-4 —— ノミを含む `compare` の行 × {旧（`NomiH424`・段0 の前の規定）, NM-wc} × 本編 第1〜5波 ＋ 近衛 ／ 大隊・seed 0..199");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " | 第2〜5波の平均 |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|");
        var bal = ReadTable(File.Exists(BalanceBefore) ? BalanceBefore : "docs/balance.md", 5);
        var tsv = new List<string>();
        int mism = 0;
        foreach (var (name0, f, cross) in NomiRows())
        {
            string name = cross ? name0 + "（交差帯）" : name0;
            double[] b = waves.Select(w => Win(Swap(f, UnitCatalog.NomiH424), w, 200)).ToArray();   // 旧（段0 の前の規定）
            double[] x = waves.Select(w => Win(Swap(f, UnitCatalog.NomiNMwc), w, 200)).ToArray();
            Console.WriteLine($"| {name} | 旧 | " + string.Join(" | ", b.Select(F1)) + $" | {F1(b.Skip(1).Take(4).Average())} |");
            Console.WriteLine($"| {name} | NM-wc | " + string.Join(" | ", x.Select((v, i) => $"{F1(v)}（{Delta(v - b[i])}）")) + $" | {F1(x.Skip(1).Take(4).Average())}（{Delta(x.Skip(1).Take(4).Average() - b.Skip(1).Take(4).Average())}）|");
            tsv.Add(name + "\t" + string.Join("\t", x.Select(v => v.ToString("F1"))));
            if (!cross && bal.TryGetValue(name, out var bv))
                for (int i = 0; i < 5; i++) if (Math.Abs(bv[i] - b[i]) > 0.05) mism++;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(ExpectFile)!);
        File.WriteAllLines(ExpectFile, tsv);
        Console.WriteLine();
        Console.WriteLine($"- 旧の列と `{(File.Exists(BalanceBefore) ? BalanceBefore : "docs/balance.md")}` の食い違い: {mism} セル（0 なら器具が `compare` と同じ数え方）。");
        Console.WriteLine($"- NM-wc の列を `{ExpectFile}` に書いた（{tsv.Count} 行）。");
        Console.WriteLine();
    }

    /// <summary>`| 行 | v1 | v2 | ...` の表を、行名 → 先頭から n 個の数値に読む（% を外す）。</summary>
    static Dictionary<string, double[]> ReadTable(string path, int n, int skip = 0)
    {
        var d = new Dictionary<string, double[]>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ")) continue;
            var c = line.Split('|').Select(s => s.Trim()).ToArray();
            if (c.Length < 2 + skip + n) continue;
            var vals = new double[n]; bool ok = true;
            for (int i = 0; i < n; i++) ok &= double.TryParse(c[2 + skip + i].TrimEnd('%'), out vals[i]);
            if (ok && !d.ContainsKey(c[1])) d[c[1]] = vals;
        }
        return d;
    }

    static void Verify()
    {
        Console.WriteLine("# 第425期 段0 の後 —— `docs/balance.md` ／ `docs/elite.md` を期待値と段0 の前に突き合わせる");
        Console.WriteLine();
        var exp = File.ReadAllLines(ExpectFile).Select(l => l.Split('\t')).ToDictionary(c => c[0], c => c.Skip(1).Select(double.Parse).ToArray());
        var now = ReadTable("docs/balance.md", 5); var before = ReadTable(BalanceBefore, 5);
        int moved = 0, offExp = 0, outside = 0;
        Console.WriteLine("| 行 | 波 | 段0 の前 | 段0 の後 | 期待値（NM-wc） | 期待との差 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var (name, v) in now)
        {
            if (!before.TryGetValue(name, out var b)) { Console.WriteLine($"| {name} | — | 段0 の前に無い行 | | | |"); outside++; continue; }
            for (int i = 0; i < 5; i++)
            {
                bool inExp = exp.TryGetValue(name, out var e);
                if (Math.Abs(v[i] - b[i]) > 0.05)
                {
                    moved++;
                    if (!inExp) outside++;
                }
                if (inExp && Math.Abs(v[i] - e![i]) > 0.05) offExp++;
                if (Math.Abs(v[i] - b[i]) > 0.05 || (inExp && Math.Abs(v[i] - e![i]) > 0.05))
                    Console.WriteLine($"| {name} | 第{i + 1}波 | {F1(b[i])} | {F1(v[i])} | {(inExp ? F1(e![i]) : "（ノミのいない行）")} | {(inExp ? Delta(v[i] - e![i]) : "—")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"- 動いたセル {moved}・期待値と食い違うセル {offExp}・**ノミのいない行で動いたセル {outside}**（門は 0）。");
        // 精鋭: 主表の 五 勝率（列 3）／ 九 勝率（列 7）。軸の列を飛ばすので skip=1。
        var en = ReadTable("docs/elite.md", 5, 1); var eb = ReadTable(EliteBefore, 5, 1);
        Console.WriteLine();
        Console.WriteLine("## 精鋭（`docs/elite.md` の主表・五 ＝ 近衛 ／ 九 ＝ 大隊）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 近衛 前 → 後（期待） | 大隊 前 → 後（期待） |");
        Console.WriteLine("|---|--:|--:|");
        int eMoved = 0, eOutside = 0, eOff = 0;
        foreach (var (name, v) in en)
        {
            if (!eb.TryGetValue(name, out var b)) continue;
            bool inExp = exp.TryGetValue(name, out var e);
            bool mv = Math.Abs(v[0] - b[0]) > 0.05 || Math.Abs(v[4] - b[4]) > 0.05;
            if (mv) { eMoved++; if (!inExp) eOutside++; }
            if (inExp && (Math.Abs(v[0] - e![5]) > 0.05 || Math.Abs(v[4] - e[6]) > 0.05)) eOff++;
            if (mv || inExp)
                Console.WriteLine($"| {name} | {F1(b[0])} → {F1(v[0])}{(inExp ? $"（{F1(e![5])}）" : "")} | {F1(b[4])} → {F1(v[4])}{(inExp ? $"（{F1(e![6])}）" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"- 精鋭: 動いた行 {eMoved}・期待値と食い違う行 {eOff}・ノミのいない行で動いた行 {eOutside}。");
        if (outside > 0 || offExp > 0) Environment.ExitCode = 1;
    }

    // ---- §1-8: 段0 の確かめ ----
    static void Confirm()
    {
        Console.WriteLine("# 第425期 §1-8 —— 段0 の確かめ（seed 0..99・第424期 §3-1 の NM-wc の行と同じか）");
        Console.WriteLine();
        var play = Presets.Versions.First(r => r.Name == PlayRow).F;
        var burn = CompareBuilds().First(r => r.Name == "燃焼×刻み (ボルグ×ホタ×ノミ)").F;
        var solo = CompareBuilds().First(r => r.Name == "刻み (ノミ単騎)").F;
        Console.WriteLine("| 台 | " + string.Join(" | ", Four.Select(w => w.Name)) + " | 第424期の NM-wc |");
        Console.WriteLine("|---|" + string.Concat(Four.Select(_ => "--:|")) + "---|");
        foreach (var (label, f, want) in new[] { (PlayRow, play, "0 ／ 100 ／ 100 ／ 100"), ("刻み (ノミ単騎)・規定", solo, "0 ／ 100 ／ 100 ／ 100"), ("燃焼×刻み・規定", burn, "0 ／ 100 ／ 100 ／ 100") })
            Console.WriteLine($"| {label} | " + string.Join(" | ", Four.Select(w => F1(Win(f, w, 100)))) + $" | {want} |");
        Console.WriteLine();
    }

    // ---- §3: 爆弾の材料 ----
    sealed class Acc
    {
        public long N, Reach, Sum, T3, Cur, CurAll, CurNext, NextN;
        public readonly List<double> Sums = new();
        public void Add(Acc o) { N += o.N; Reach += o.Reach; Sum += o.Sum; T3 += o.T3; Cur += o.Cur; CurAll += o.CurAll; CurNext += o.CurNext; NextN += o.NextN; Sums.AddRange(o.Sums); }
    }

    /// <summary>
    /// 1 戦で、席 <paramref name="slot"/> の駒（味方なら <paramref name="ally"/>）が 3 回目の被弾を受けるまでの HP の和と、
    /// その手番の頭（`StatusSnapshot`）で「隣の敵」が持つ傷＋燃焼の数を数える。
    /// 味方の席の「隣の敵」＝ 向かいの敵の席（同じ席番号）とその隣。敵の前列の席の「隣の敵」＝ その席の隣の敵（自分を除く）。
    /// </summary>
    static Dictionary<(bool Ally, int Slot), Acc> One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true, census: false);
        var res = new Dictionary<(bool, int), Acc>();
        string wl = StatusKeys.LabelOf(StatusKeys.Wound), bl = StatusKeys.LabelOf(StatusKeys.Burn);
        var foeSlot = e.ToDictionary(u => u.InstanceId, u => u.Slot);
        var seats = p.Where(u => u.Slot <= 4 && u.Slot != 2 && FormationRules.AreAdjacent(2, u.Slot)).Select(u => (true, u))
            .Concat(e.Where(u => FormationRules.RowOf(u.Slot) == Row.Front).Select(u => (false, u)));
        foreach (var (ally, u) in seats)
        {
            var a = new Acc { N = 1 };
            var hits = r.Events.Where(ev => ev.Kind == BattleEventKind.Damage && ev.TargetId == u.InstanceId && ev.Amount > 0).Take(3).ToList();
            if (hits.Count == 3)
            {
                a.Reach = 1; a.Sum = hits.Sum(h => (long)h.Amount); a.Sums.Add(a.Sum); int t = hits[2].Turn; a.T3 = t;
                bool Near(int s) => ally ? (s == u.Slot || FormationRules.AreAdjacent(u.Slot, s)) : (s != u.Slot && FormationRules.AreAdjacent(u.Slot, s));
                if (r.Turns > t) a.NextN = 1;
                foreach (var ev in r.Events)
                {
                    if (ev.Turn == t + 1 && ev.Kind == BattleEventKind.StatusSnapshot && ev.TargetId is int id2 && foeSlot.TryGetValue(id2, out int s2)
                        && (ev.Text == wl || ev.Text == bl) && Near(s2)) a.CurNext += ev.Amount;
                    if (ev.Turn != t || ev.Kind != BattleEventKind.StatusSnapshot || ev.TargetId is not int id || !foeSlot.TryGetValue(id, out int s)) continue;
                    if (ev.Text != wl && ev.Text != bl) continue;
                    a.CurAll += ev.Amount;
                    if (Near(s)) a.Cur += ev.Amount;
                }
            }
            res[(ally, u.Slot)] = a;
        }
        return res;
    }

    static void Bomb()
    {
        Console.WriteLine("# 第425期 §3 —— 爆弾の新駒の材料（規定のまま数えるだけ・3 台 × 4 波・seed 0..99）");
        Console.WriteLine();
        Console.WriteLine("- **案1「浴びた量」**: その席の駒が 3 回目の被弾を受けるまでの HP の和（`Damage` の出来事の量・3 回届いた戦だけの平均）。");
        Console.WriteLine("- **案2「周りの印」**: 3 回目の被弾の手番の頭（`StatusSnapshot`）で、隣の敵が持つ傷＋燃焼の数。");
        Console.WriteLine("  - 味方の席の「隣の敵」＝ 向かいの敵の席（同じ席番号）とその隣 ／ 敵の前列の席は、その席の隣の敵（自分を除く）。");
        Console.WriteLine("  - 手番の中で積まれたぶんは次の手番の頭まで写らない（`StatusSnapshot` の性質）。そこで**次の手番の頭**の同じ数も並べる（爆発の後に積まれたぶんも入る上限側・戦が続いたものだけ）。");
        Console.WriteLine();
        var all = new Dictionary<(string, bool, int), Acc>();
        foreach (var bn in BombBoards)
        {
            var f = Presets.Versions.First(r => r.Name == bn).F;
            Console.WriteLine($"## {bn}（{Som307Diag.Seats(f)}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 席 | 3 回届いた | 3 回目の T | 案1 浴びた量 | 案2 隣の傷＋燃焼（その手番の頭） | 案2（次の手番の頭） | （敵全体・その手番の頭） |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
            foreach (var w in Four)
            {
                var parts = new Dictionary<(bool, int), Acc>[100];
                Parallel.For(0, 100, s => parts[s] = One(f, w, s));
                var sum = new SortedDictionary<(bool, int), Acc>(Comparer<(bool, int)>.Create((x, y) => x.Item1 != y.Item1 ? (x.Item1 ? -1 : 1) : x.Item2.CompareTo(y.Item2)));
                foreach (var d in parts) foreach (var (k, v) in d) { if (!sum.ContainsKey(k)) sum[k] = new Acc(); sum[k].Add(v); }
                foreach (var ((ally, slot), a) in sum)
                {
                    string seat = (ally ? "味方 " : "敵の前列 ") + FormationRules.SeatNames[slot];
                    Console.WriteLine($"| {w.Name} | {seat} | {a.Reach}/{a.N} | {Per(a.T3, a.Reach)} | {Per(a.Sum, a.Reach)} | {Per(a.Cur, a.Reach)} | {Per(a.CurNext, a.NextN)} | {Per(a.CurAll, a.Reach)} |");
                    var key = (bn, ally, slot);
                    if (!all.ContainsKey(key)) all[key] = new Acc();
                    all[key].Add(a);
                }
                if (sum.Count == 0) Console.WriteLine($"| {w.Name} | — | 席に駒がいない | | | | | |");
            }
            Console.WriteLine();
        }
        Console.WriteLine("## 席ごとのばらつき（4 波を合わせて・席ごとの平均の 最小〜最大 と 変動係数）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 置き場 | 案1 浴びた量 | 案1 の変動係数 | 案2 隣の傷＋燃焼（頭） | 案2 の変動係数 | 案2（次の頭） | 案2（次）の変動係数 |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|--:|");
        foreach (var bn in BombBoards)
            foreach (bool ally in new[] { true, false })
            {
                var per = all.Where(kv => kv.Key.Item1 == bn && kv.Key.Item2 == ally && kv.Value.Reach > 0).Select(kv => kv.Value).ToList();
                if (per.Count == 0) { Console.WriteLine($"| {bn} | {(ally ? "味方の中央の隣" : "敵の前列")} | — | — | — | — | — | — |"); continue; }
                double[] s1 = per.Select(a => (double)a.Sum / a.Reach).ToArray(), s2 = per.Select(a => (double)a.Cur / a.Reach).ToArray();
                double[] s3 = per.Where(a => a.NextN > 0).Select(a => (double)a.CurNext / a.NextN).ToArray();
                string r3 = s3.Length == 0 ? "— | —" : $"{s3.Min():F2}〜{s3.Max():F2} | {Cv(s3):F2}";
                Console.WriteLine($"| {bn} | {(ally ? "味方の中央の隣" : "敵の前列")} | {s1.Min():F1}〜{s1.Max():F1} | {Cv(s1):F2} | {s2.Min():F2}〜{s2.Max():F2} | {Cv(s2):F2} | {r3} |");
            }
        Console.WriteLine();
    }
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static double Cv(double[] x) { double m = x.Average(); if (m == 0) return double.NaN; return Math.Sqrt(x.Average(v => (v - m) * (v - m))) / m; }

    // ---- 自己検査 ----
    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第425期 nomi425 check");
        Console.WriteLine();
        var n = UnitCatalog.Nomi; var h = UnitCatalog.NomiH424; var c = UnitCatalog.NomiNMwc;
        Req(n.Traits.SequenceEqual(h.Traits.Where(t => t != TraitId.CarveOnce).Append(TraitId.CarveReadCap)), "規定 ＝ 旧（`NomiH424`）− CarveOnce ＋ CarveReadCap");
        Req(n.Traits.SequenceEqual(c.Traits) && !ReferenceEquals(n, c), "規定と NM-wc は同じ札の別の定義");
        Req(n.Id == h.Id && n.Name == h.Name && n.MaxHp == 52 && n.Attack == 10 && n.Speed == 7 && n.MinusText == h.MinusText && n.Flavor == h.Flavor, "Id ／ 名前 ／ HP 52 ／ 攻 10 ／ 速 7 ／ マイナス ／ フレーバーは旧のまま");
        Req(n.PlusText == "攻撃力の数だけ 1 ずつ連打する。一発ごとに傷を刻み、相手の傷 1 つにつき 2 を上乗せする（一発に 20 まで）。刻んだ相手が倒れると、もう一度動く（一振りに 1 回）", "文面は第424期 §1 の NM-wc");
        Req(UnitCatalog.All.Contains(n) && !UnitCatalog.All.Contains(h) && !UnitCatalog.Retired.Contains(h), "`All` は規定のノミ・旧は `All` ／ `Retired` の外");
        Req(UnitCatalog.NomiNMw.Traits.SequenceEqual(h.Traits.Where(t => t != TraitId.CarveOnce)), "第423〜424期の版は旧から作ったまま（NM-w ＝ 旧 − CarveOnce）");
        Req(ReferenceEquals(PinN424(n), h) && ReferenceEquals(PinN424(UnitCatalog.Sora), UnitCatalog.Sora), "`PinN424` は規定のノミだけを旧へ");
        var row = Presets.Versions.FirstOrDefault(r => r.Name == PlayRow);
        Req(row.Name is not null && row.F.Occupied().Any(o => ReferenceEquals(o.Def, n)) && !Presets.Versions.Any(r => r.Name == "版・刻み ノミ"), "「試遊・刻み ノミ」は規定のノミ・「版・刻み ノミ」は無い");
        var solo = CompareBuilds().First(r => r.Name == "刻み (ノミ単騎)").F;
        Req(row.Name is not null && row.F.Occupied().Select(o => (o.Slot, o.Def.Id)).SequenceEqual(solo.Occupied().Select(o => (o.Slot, o.Def.Id))), "「試遊・刻み ノミ」の並びはノミ単騎のまま");
        Req(!VersionsH407().Any(r => r.Name == PlayRow), "鎖の終端（`VersionsH407`）は「試遊・刻み ノミ」を外す");
        bool same = true;
        for (int s = 0; s < 6; s++)
        {
            var (r1, _, _) = I.Raw(solo, I.Guard, s, verbose: false, census: false);
            var (r2, _, _) = I.Raw(solo, I.Guard, s, verbose: true, census: false);
            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
        }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（ノミ単騎 × 近衛 × 6 seed・爆弾の材料は verbose で数えるため）");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
