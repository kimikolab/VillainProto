using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using K8 = Kari408Diag;

// =====================================================================================
// kari409 —— 第409期【軽い期・版だけ】カリの転生③: K-d の絞り込み（引きずり出しは要るか・往復止め・席の総当たり）。
// 指示書は design/PHASE409_KARI_KD_SPEC.md ／ 報告は design/PHASE409_KARI_KD.md。1戦の計数は `kari408` の `One` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 kari409 seats    # §2-2: 「試遊・呪い」から1枚外し × カリを挿す位置 5 ＝ 25 並び × {K-d, K-d1, K-d0, 素体 攻4} × 4 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 kari409 mixed    # §2-4: compare のカリの2行のカリを K-d1 ／ K-d0 に差し替えた写し × 7 波・seed 0..199
//     dotnet run --project BattleSim -c Release 0 kari409 log <版 kd|kd1|kd0|p4> <外す駒> <位置 0〜4> <波> [seed]
//     dotnet run --project BattleSim -c Release 0 kari409 check    # 自己検査
//
// **予測のファイルが無ければ seats ／ mixed を走らせない**（R397・`.tmp/p409/predict.md`）。
// 並びの作り方: 「試遊・呪い」の席 0〜4（ワタ・ムド・ネル・クビ・ウツ）から1枚を外し、残り4枚を相対順のまま席 0〜4 に詰め、
// カリを位置 p（0 前1 ／ 1 前3 ／ 2 中央 ／ 3 後1 ／ 4 後3）に挿す（p より後ろの駒は1席ずつ後ろへずれる）。外した席に挿すと第408期の並びになる。
// =====================================================================================
static class Kari409Diag
{
    internal const string PredictFile = ".tmp/p409/predict.md";
    const int Seeds = 100, CompareSeeds = 200;
    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");

    static Formation Board => Presets.Versions.First(r => r.Name == D.PlayRow).F;
    static I.W[] Waves => K8.Waves;
    static readonly string[] Ids = { "wata", "mudo", "nel", "kubi", "utsu" };
    static readonly string[] PosName = { "前1", "前3", "中央", "後1", "後3" };
    internal static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("kd", "K-d", UnitCatalog.KariKd), ("kd1", "K-d1", UnitCatalog.KariKd1), ("kd0", "K-d0", UnitCatalog.KariKd0), ("p4", "素体 攻4", K8.PlainOf(UnitCatalog.KariKd)),
    };

    internal sealed record Arr(string Out, int Pos)
    {
        public string Label => $"−{Short(UnitCatalog.Everyone.First(d => d.Id == Out))}・カリ{PosName[Pos]}";
    }
    internal static IEnumerable<Arr> AllArrs() => Ids.SelectMany(o => Enumerable.Range(0, 5).Select(p => new Arr(o, p)));
    static string Short(UnitDef d) => S307.Short(d);

    internal static Formation Build(Arr a, UnitDef kari)
    {
        var rest = Enumerable.Range(0, 5).Select(i => Board[i]!).Where(d => d.Id != a.Out).ToList();
        rest.Insert(a.Pos, kari);
        var g = new Formation();
        for (int s = 0; s < 5; s++) g[s] = rest[s];
        return g;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"kari409: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "seats": Seats(); return;
            case "mixed": Mixed(); return;
            case "log": Log(A(3, "kd0"), A(4, "wata"), int.Parse(A(5, "3")), A(6, "s4"), int.Parse(A(7, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("kari409: モードは seats / mixed / log / check。"); return;
        }
    }

    static I.W WaveOf(string k) => k switch { "boss" => I.Boss, "guard" => I.Guard, "bat" => I.Bat, _ => I.StrongWaves[2] };
    static void Log(string ver, string outId, int pos, string wave, int seed)
    {
        var v = Vers.First(x => x.Key == ver);
        var f = Build(new Arr(outId, pos), v.D); var w = WaveOf(wave);
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {S307.Seats(f)}（{v.Label}）× {w.Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // §2-2 25 並び
    // ---------------------------------------------------------------------------------
    static string Cell(K8.Fx a) => $"{a.WT}・倒 {Per2(a.Fell, a.N)}・尻尾 {K8.Tail(a)}";
    static double KillT(K8.Fx a) => a.Wins == 0 ? 99 : (double)a.WinT / a.Wins;
    static IEnumerable<Arr> RankArrs(Dictionary<(Arr, string), K8.Fx[]> res, string v, IEnumerable<Arr> arrs) => arrs
        .OrderByDescending(a => res[(a, v)][3].Win).ThenByDescending(a => res[(a, v)][0].Win).ThenBy(a => KillT(res[(a, v)][0]))
        .ThenByDescending(a => res[(a, v)][1].Win + res[(a, v)][2].Win).ThenBy(a => a.Out, StringComparer.Ordinal).ThenBy(a => a.Pos);

    static void Seats()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var arrs = AllArrs().ToArray();
        Console.WriteLine($"# 第409期 §2-2 —— 「{D.PlayRow}」（{S307.Seats(Board)}）から1枚を外し、カリを5通りの位置に挿した {arrs.Length} 並び × {string.Join(" ／ ", Vers.Select(v => v.Label))} × {string.Join(" ／ ", Waves.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("並び ＝「−外した駒・カリの位置」。残り4枚は「試遊・呪い」の相対順のまま詰め、カリを挿した位置より後ろの駒が1席ずつ後ろへずれる（外した駒の席に挿すと第408期の並び）。");
        Console.WriteLine("K-d ＝ 第408期のまま ／ K-d1 ＝ 同じ敵は二度引きずり出さない ／ K-d0 ＝ 引きずり出さず、累計の大きい呪われた敵に −4 の印を1手番1体ずつ付ける（的の固定なし）／ 素体 攻4 ＝ 札なし。");
        Console.WriteLine();
        var baseRes = Waves.Select(w => K8.Measure(Board, w)).ToArray();
        var res = new Dictionary<(Arr, string), K8.Fx[]>();
        foreach (var a in arrs) foreach (var v in Vers) res[(a, v.Key)] = Waves.Select(w => K8.Measure(Build(a, v.D), w)).ToArray();
        Console.WriteLine($"「{D.PlayRow}」そのもの: " + string.Join(" ／ ", Waves.Select((w, i) => $"{w.Name} {Cell(baseRes[i])}")));
        Console.WriteLine();

        // 0. 第408期の5並び
        var w408 = res[(new Arr("wata", 0), "kd")];
        bool same408 = arrs.Where(a => Ids[a.Pos] == a.Out).All(a => S307.Seats(Build(a, UnitCatalog.KariKd)) == S307.Seats(K8.Seat(a.Out, UnitCatalog.KariKd)));
        string[] exp408 = { "100.0・10.0T", "100.0・6.3T", "100.0・5.3T", "62.0・10.6T" };
        bool val408 = Enumerable.Range(0, 4).All(i => w408[i].WT == exp408[i]);
        Console.WriteLine($"## 0. 第408期の5並びが含まれるか: 並び {(same408 ? "はい" : "**いいえ**")}・K-d −ワタ・カリ前1 の4波（第408期 {string.Join(" ／ ", exp408)}）＝ {string.Join(" ／ ", w408.Select(x => x.WT))} → {(val408 ? "同じ" : "**違う**")}");
        Console.WriteLine();

        // 1. 強4 とボスは全並び
        foreach (int wi in new[] { 3, 0 })
        {
            Console.WriteLine($"## 1-{(wi == 3 ? "a" : "b")}. {Waves[wi].Name}（全 {arrs.Length} 並び・勝率・倒しT・倒れた味方 ／戦・尻尾の直 ／T・尻尾の長さ）");
            Console.WriteLine();
            Console.WriteLine("| 並び | " + string.Join(" | ", Vers.Select(v => v.Label)) + " | K-d − K-d0 | K-d1 − K-d | K-d0 − 素体 |");
            Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")) + "--:|--:|--:|");
            foreach (var a in arrs)
            {
                var c = Vers.Select(v => res[(a, v.Key)][wi]).ToArray();
                Console.WriteLine($"| {a.Label} | " + string.Join(" | ", c.Select(Cell)) + $" | {Sg(c[0].Win - c[2].Win)} | {Sg(c[1].Win - c[0].Win)} | {Sg(c[2].Win - c[3].Win)} |");
            }
            Console.WriteLine();
        }

        // 2. 近衛 ／ 大隊のまとめ
        Console.WriteLine("## 2. 近衛 ／ 大隊のまとめ（25 並び）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 100% の並び | 素体より上 ／ 下の並び | 素体との差の平均 | 上位3並び |");
        Console.WriteLine("|---|---|--:|---|--:|---|");
        foreach (var v in Vers.Take(3))
            foreach (int wi in new[] { 1, 2 })
            {
                var xs = arrs.Select(a => (a, x: res[(a, v.Key)][wi], z: res[(a, "p4")][wi])).ToArray();
                var top = xs.OrderByDescending(t => t.x.Win).ThenBy(t => KillT(t.x)).Take(3).Select(t => $"{t.a.Label} {t.x.WT}");
                Console.WriteLine($"| {v.Label} | {Waves[wi].Name} | {xs.Count(t => t.x.Win >= 100)} | {xs.Count(t => t.x.Win > t.z.Win)} ／ {xs.Count(t => t.x.Win < t.z.Win)} | {Sg(xs.Average(t => t.x.Win - t.z.Win))} | {string.Join("、", top)} |");
            }
        Console.WriteLine();

        // 3. 引きずり出しの寄与（K-d − K-d0）
        Console.WriteLine("## 3. 引きずり出しの寄与 ＝ K-d − K-d0（同じ並び・同じ seed・勝率の差）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 25 並びの平均 | K-d が上 ／ K-d0 が上 ／ 同じ | 最大（並び） | 最小（並び） |");
        Console.WriteLine("|---|--:|---|---|---|");
        foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
        {
            var d = arrs.Select(a => (a, d: res[(a, "kd")][wi].Win - res[(a, "kd0")][wi].Win)).ToArray();
            var mx = d.OrderByDescending(t => t.d).First(); var mn = d.OrderBy(t => t.d).First();
            Console.WriteLine($"| {w.Name} | {Sg(d.Average(t => t.d))} | {d.Count(t => t.d > 0)} ／ {d.Count(t => t.d < 0)} ／ {d.Count(t => t.d == 0)} | {Sg(mx.d)}（{mx.a.Label}） | {Sg(mn.d)}（{mn.a.Label}） |");
        }
        Console.WriteLine();

        // 4. 席の効き（同じ外す駒で、カリ前列（前1 ／ 前3）と後列（後1 ／ 後3）の最良）
        Console.WriteLine("## 4. 席の効き（同じ外す駒で、カリ前列（前1 ／ 前3）の最良と後列（後1 ／ 後3）の最良・強4 の勝率 → ボス）");
        Console.WriteLine();
        Console.WriteLine("| 外した駒 | 版 | 前列の最良 | 後列の最良 | 後列 − 前列（強4 ／ ボス） |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var o in Ids)
            foreach (var v in Vers)
            {
                var fr = RankArrs(res, v.Key, arrs.Where(a => a.Out == o && a.Pos <= 1)).First();
                var bk = RankArrs(res, v.Key, arrs.Where(a => a.Out == o && a.Pos >= 3)).First();
                var f = res[(fr, v.Key)]; var b = res[(bk, v.Key)];
                Console.WriteLine($"| −{o} | {v.Label} | {fr.Label}: 強4 {f[3].WT} ／ ボス {f[0].WT} | {bk.Label}: 強4 {b[3].WT} ／ ボス {b[0].WT} | {Sg(b[3].Win - f[3].Win)} ／ {Sg(b[0].Win - f[0].Win)} |");
            }
        Console.WriteLine();

        // 5. 版ごとの中身（最良 ／ 前列の代表 ／ 後列の代表）
        Console.WriteLine("## 5. 版ごとの中身（最良の並び ／ カリ前列の代表 ／ カリ後列の代表・戦あたり）");
        Console.WriteLine();
        Console.WriteLine("代表 ＝ その列にカリがいる並びのうち順位（強4 の勝率 → ボスの勝率 → ボスの倒しT → 近衛 ＋ 大隊）が最も高い並び。−4 を付けた敵 ＝ K-d ／ K-d1 は引きずり出した敵の数（引きずり出し − 往復）、K-d0 は印を付けた敵の数。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 代表 | 並び | 波 | 勝率・T | 引きずり出し（うち往復） | −4 を付けた敵 | 深めた呪い | 的がカリに | ふつうに殴った | カリ: 受けた ／ 倒れた・T | 後列の敵: 倒れた・T | ワタ T2 まで ／ ムド T3 まで |");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|--:|--:|---|---|---|");
        var best = new Dictionary<string, Arr>();
        foreach (var v in Vers.Take(3))
        {
            var b0 = RankArrs(res, v.Key, arrs).First(); best[v.Key] = b0;
            var fr = RankArrs(res, v.Key, arrs.Where(a => a.Pos <= 1)).First();
            var bk = RankArrs(res, v.Key, arrs.Where(a => a.Pos >= 3)).First();
            foreach (var (tag, a) in new[] { ("最良", b0), ("前列", fr), ("後列", bk) })
                foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
                {
                    var x = res[(a, v.Key)][wi];
                    long tagged = v.Key == "kd0" ? x.Marks : x.Drags - x.Redrags;
                    Console.WriteLine($"| {v.Label} | {tag} | {a.Label} | {w.Name} | {x.WT} | {Per2(x.Drags, x.N)}（{Per2(x.Redrags, x.N)}） | {Per2(tagged, x.N)} | {Per(x.Deepened, x.N)} | {Per(x.DragAimed, x.N)} | {Per(x.Plain, x.N)} | {Per(x.KariTaken, x.N)} ／ {Pct(x.KariDied, x.N)}%・{Per(x.KariDeathT, x.KariDied)} | {Pct(x.BackDied, x.BackN)}%・{Per(x.BackDeathT, x.BackDied)} | {Pct(x.WataEarly, x.WataN)}% ／ {Pct(x.MudoEarly, x.MudoN)}% |");
                }
        }
        var b4 = baseRes[3];
        Console.WriteLine();
        Console.WriteLine($"「{D.PlayRow}」そのもの（強4）: 後列の敵 {Pct(b4.BackDied, b4.BackN)}%・{Per(b4.BackDeathT, b4.BackDied)}T ／ ワタ T2 まで {Pct(b4.WataEarly, b4.WataN)}% ／ ムド T3 まで {Pct(b4.MudoEarly, b4.MudoN)}%。");
        Console.WriteLine();

        // 6. 最良（試遊の台の候補）
        Console.WriteLine("## 6. 版ごとの最良の並び（4 波）と素体 攻4 との差");
        Console.WriteLine();
        Console.WriteLine("| 版 | 最良の並び（席） | " + string.Join(" | ", Waves.Select(w => w.Name)) + " | 素体との差（4 波） |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")) + "---|");
        foreach (var v in Vers.Take(3))
        {
            var a = best[v.Key]; var x = res[(a, v.Key)]; var z = res[(a, "p4")];
            Console.WriteLine($"| {v.Label} | {a.Label}（{S307.Seats(Build(a, v.D))}） | " + string.Join(" | ", x.Select(c => c.WT)) + $" | {string.Join(" ／ ", Enumerable.Range(0, 4).Select(i => Sg(x[i].Win - z[i].Win)))} |");
        }
        Console.WriteLine();
        // 7. 往復（第408期の5並び・K-d と K-d1）
        Console.WriteLine("## 7. 往復（第408期の5並び ＝ 外した駒の席にカリ・戦あたり・引きずり出し（うち同じ敵の2度目））");
        Console.WriteLine();
        Console.WriteLine("| 並び | 波 | K-d | K-d1 | 勝率 K-d ／ K-d1 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var a in arrs.Where(a => Ids[a.Pos] == a.Out))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
            {
                var x = res[(a, "kd")][wi]; var y = res[(a, "kd1")][wi];
                Console.WriteLine($"| {a.Label} | {w.Name} | {Per2(x.Drags, x.N)}（{Per2(x.Redrags, x.N)}） | {Per2(y.Drags, y.N)}（{Per2(y.Redrags, y.N)}） | {x.WT} ／ {y.WT} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §2-4 寄せ集め
    // ---------------------------------------------------------------------------------
    static Formation SwapKari(Formation f, UnitDef d)
    {
        var g = new Formation();
        foreach (var o in f.Occupied()) g[o.Slot] = o.Def.Id == "kari" ? d : o.Def;
        return g;
    }
    static void Mixed()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = Presets.Compare.Where(b => b.F.Occupied().Any(o => o.Def.Id == "kari")).ToArray();
        var waves = EnemyCatalog.Stages.Select((st, i) => ($"第{i + 1}波", (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(f, st.Enemy, s, verbose: false))))
            .Concat(new[] { ("近衛", EnemyCatalog.EliteGuardWave), ("大隊", EnemyCatalog.EliteBattalionWave) }
                .Select(x => (x.Item1, (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(x.Item2, EnemyCatalog.EliteScale), s, verbose: false)))))
            .ToArray();
        var vs = new (string Label, UnitDef? D)[] { ("規定（旧のカリ）", null), ("K-d（第408期）", UnitCatalog.KariKd), ("K-d1", UnitCatalog.KariKd1), ("K-d0", UnitCatalog.KariKd0), ("素体 攻4", K8.PlainOf(UnitCatalog.KariKd)) };
        Console.WriteLine($"# 第409期 §2-4 寄せ集め —— `compare` のカリを含む {rows.Length} 行のカリを版に差し替えた写し × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..{CompareSeeds - 1}");
        Console.WriteLine();
        foreach (var (name, f) in rows)
        {
            Console.WriteLine($"**{name}**（{S307.Seats(f)}）");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", waves.Select(w => w.Item1)) + " | 平均 |");
            Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|");
            foreach (var (label, d) in vs)
            {
                var g = d is null ? f : SwapKari(f, d);
                var wins = waves.Select(w =>
                {
                    var o = new int[CompareSeeds];
                    Parallel.For(0, CompareSeeds, s => o[s] = w.Item2(g, s).PlayerWon ? 1 : 0);
                    return 100.0 * o.Sum() / CompareSeeds;
                }).ToArray();
                Console.WriteLine($"| {label} | " + string.Join(" | ", wins.Select(F1)) + $" | {F1(wins.Average())} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string what, bool ok, string detail = "")
    {
        Console.WriteLine($"- [{(ok ? "OK" : "NG")}] {what}{(detail == "" ? "" : $"（{detail}）")}");
        if (!ok) _fail++;
    }
    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# 第409期 kari409 check —— 自己検査");
        Console.WriteLine();
        var k0 = UnitCatalog.Kari; var kd = UnitCatalog.KariKd; var kd1 = UnitCatalog.KariKd1; var kd0 = UnitCatalog.KariKd0;
        bool body(UnitDef d) => d.Id == k0.Id && d.MaxHp == k0.MaxHp && d.Attack == 4 && d.Speed == k0.Speed && d.Actions.Count == 1 && d.Actions[0].Kind == ActionKind.Skill;
        Expect("版の定義: Id ＝ kari・HP 62 ／ 攻 4 ／ 速 9・手番は Skill・K-d1 ＝ K-d の札 ＋ KariDragOnce・K-d0 ＝ KariMarkDeepen だけ",
            body(kd1) && body(kd0) && kd1.Traits.SequenceEqual(kd.Traits.Append(TraitId.KariDragOnce)) && kd0.Traits.SequenceEqual(new[] { TraitId.KariMarkDeepen }));
        var held = UnitCatalog.Everyone.Where(d => d.Traits.Any(t => t is TraitId.KariDragOnce or TraitId.KariMarkDeepen)).ToArray();
        Expect("保持者 0 枚（`All` ／ `Retired` ／ `Presets.Compare` に版が無い・規定のカリは Goad のまま）",
            held.Length == 0 && !UnitCatalog.All.Contains(kd1) && !UnitCatalog.All.Contains(kd0) && k0.Traits.SequenceEqual(new[] { TraitId.Goad }), $"保持者 {held.Length}");
        Expect("25 並びに第408期の5並び（外した駒の席にカリ）が含まれる",
            AllArrs().Where(a => Ids[a.Pos] == a.Out).All(a => S307.Seats(Build(a, kd)) == S307.Seats(K8.Seat(a.Out, kd))) && AllArrs().Count() == 25
            && AllArrs().Select(a => S307.Seats(Build(a, kd))).Distinct().Count() == 25);
        long redrag1 = 0, drags1 = 0, drags0 = 0, aimed0 = 0, marks0 = 0, deep0 = 0, foes0 = 0, redragK = 0;
        foreach (var a in new[] { new Arr("wata", 0), new Arr("wata", 3), new Arr("utsu", 4) })
            foreach (var w in Waves)
                for (int s = 0; s < 20; s++)
                {
                    var (r1, _, _) = I.Raw(Build(a, kd1), w, s); var t1 = r1.TallyByUnit["kari"]; redrag1 += t1.K409Redrags; drags1 += t1.K408Drags;
                    var (rk, _, _) = I.Raw(Build(a, kd), w, s); redragK += rk.TallyByUnit["kari"].K409Redrags;
                    var (r0, _, e0) = I.Raw(Build(a, kd0), w, s); var t0 = r0.TallyByUnit["kari"];
                    drags0 += t0.K408Drags; aimed0 += t0.K408DragAimed; marks0 += t0.K409Marks; deep0 += t0.K408Deepened; foes0 += e0.Count;
                }
        Expect("K-d1: 往復 0（同じ敵は二度引きずり出さない）・K-d には往復がある（3 並び × 4 波 × seed 0..19）", redrag1 == 0 && drags1 > 0 && redragK > 0, $"K-d1 引きずり出し {drags1}・往復 {redrag1} ／ K-d の往復 {redragK}");
        Expect("K-d0: 引きずり出さない・的の固定 0・印は敵の数まで・深めた呪いは 4 の倍数", drags0 == 0 && aimed0 == 0 && marks0 > 0 && marks0 <= foes0 && deep0 % KariDragTrait.Deepen == 0 && deep0 > 0,
            $"印 {marks0}（敵の延べ {foes0}）・深めた {deep0}");
        int diff = 0;
        foreach (var d in new[] { kd1, kd0 })
            foreach (var w in Waves)
                for (int s = 0; s < 10; s++)
                {
                    var f = Build(new Arr("wata", 3), d);
                    var (x, _, _) = I.Raw(f, w, s, verbose: true); var (y, _, _) = I.Raw(f, w, s, verbose: false);
                    if (x.PlayerWon != y.PlayerWon || x.Turns != y.Turns || x.PlayerSurvivors != y.PlayerSurvivors) diff++;
                }
        Expect("verbose の有無で勝敗 ／ T ／ 生存が変わらない（2 版 × 4 波 × seed 0..9）", diff == 0, $"違い {diff}");
        string src = File.Exists("BattleCore/BattleEngine.cs") ? File.ReadAllText("BattleCore/BattleEngine.cs") : "";
        int i0 = src.IndexOf("public void KariMark409", StringComparison.Ordinal);
        int i1 = i0 < 0 ? -1 : src.IndexOf("/// <summary>K-d: 引きずり出された敵の単体の的", i0, StringComparison.Ordinal);
        string b = i0 >= 0 && i1 > i0 ? src[i0..i1] : "";
        Expect("第409期の口（`KariMark409`）に `PickOne` ／ `Roll(` が無い", b.Length > 0 && !b.Contains("PickOne") && !b.Contains("Roll("), $"{b.Length} 文字");
        var vrows = Presets.Versions.Select(r => r.Name).ToList();
        int iK = vrows.IndexOf("版・K-k 呪い"), iD = Math.Max(vrows.IndexOf("版・K-d 呪い"), vrows.IndexOf("版・カーズ 呪い"));   // 第410期に「版・カーズ 呪い」へ改名
        Expect("試遊の台「版・K-k 呪い」（K-k のまま）「版・K-d 呪い」（第409期の版）が `Presets.Versions` の末尾にある",
            iK >= 0 && iD >= 0 && vrows.Skip(Math.Min(iK, iD)).All(n => n.StartsWith("版・"))
            && Presets.Versions[iK].F.Occupied().Any(o => o.Def == UnitCatalog.KariKk)
            && Presets.Versions[iD].F.Occupied().Any(o => o.Def == kd || o.Def == kd1 || o.Def == kd0 || o.Def == UnitCatalog.KariKz62 || o.Def == UnitCatalog.KariKz90));
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて OK**" : $"**NG {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
