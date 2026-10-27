using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using K8 = Kari408Diag;
using K9 = Kari409Diag;

// =====================================================================================
// kari410 —— 第410期【軽い期・版だけ】カリの転生④: 祟りのカーズ（K-d0）の HP 上げ版（KZ-62 ／ KZ-90）を測る。
// 指示書は design/PHASE410_KARZ_HP_SPEC.md ／ 報告は design/PHASE410_KARZ_HP.md。並びは `kari409` の 25 並び・1戦の計数は `kari408` の `One`。
//
//     dotnet run --project BattleSim -c Release 0 kari410 seats    # §2-2: 25 並び × {KZ-62, KZ-90}（＋ 確かめの K-d0）× 4 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 kari410 mixed    # §2-4: compare のカリの2行のカリを KZ-90 ／ 素体 HP 90 攻 4 に差し替えた写し × 7 波・seed 0..199
//     dotnet run --project BattleSim -c Release 0 kari410 check    # 自己検査
//     dotnet run --project BattleSim -c Release 0 kari410 s411     # 第411期: 「試遊・カーズ 呪い」（規定のカーズ）× 4 波・seed 0..99 が第410期 §3-6 の KZ-90 の行と同じか
//
// **予測のファイルが無ければ seats ／ mixed を走らせない**（R397・`.tmp/p410/predict.md`）。
// =====================================================================================
static class Kari410Diag
{
    internal const string PredictFile = ".tmp/p410/predict.md";
    const int CompareSeeds = 200;
    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static I.W[] Waves => K8.Waves;
    static Formation Board => Presets.Versions.First(r => r.Name == D.PlayRow).F;
    internal const string Row410 = "版・カーズ 呪い";

    internal static UnitDef PlainOf(UnitDef d) => new()
    {
        Id = $"kari_plain_hp{d.MaxHp}", Name = $"素体のカーズ（HP {d.MaxHp}）", MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Advances = d.Advances, Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    static readonly (string Key, string Label, UnitDef D)[] Vers = { ("kz62", "KZ-62", UnitCatalog.KariKz62), ("kz90", "KZ-90", UnitCatalog.KariKz90) };

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "seats" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"kari410: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "seats": Seats(); return;
            case "mixed": Mixed(); return;
            case "check": Check(); return;
            case "s411": S411(); return;
            default: Console.WriteLine("kari410: モードは seats / mixed / check / s411。"); return;
        }
    }

    static string Cell(K8.Fx a) => $"{a.WT}・倒 {Per2(a.Fell, a.N)}・尻尾 {K8.Tail(a)}";
    static double KillT(K8.Fx a) => a.Wins == 0 ? 99 : (double)a.WinT / a.Wins;
    static IEnumerable<K9.Arr> Rank(Dictionary<(K9.Arr, string), K8.Fx[]> res, string v, IEnumerable<K9.Arr> arrs) => arrs
        .OrderByDescending(a => res[(a, v)][3].Win).ThenByDescending(a => res[(a, v)][0].Win).ThenBy(a => KillT(res[(a, v)][0]))
        .ThenByDescending(a => res[(a, v)][1].Win + res[(a, v)][2].Win).ThenBy(a => a.Out, StringComparer.Ordinal).ThenBy(a => a.Pos);

    static void Seats()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var arrs = K9.AllArrs().ToArray();
        Console.WriteLine($"# 第410期 §2-2 —— 祟りのカーズ KZ-62 ／ KZ-90 × 第409期の {arrs.Length} 並び × {string.Join(" ／ ", Waves.Select(w => w.Name))}・seed 0..99");
        Console.WriteLine();
        Console.WriteLine("並び ＝「−外した駒・カーズの位置」（作り方は `kari409` と同じ）。KZ-62 ＝ 第409期の K-d0 の名前だけ「祟りのカーズ」に替えた版 ／ KZ-90 ＝ HP を 90 に。");
        Console.WriteLine();
        var baseRes = Waves.Select(w => K8.Measure(Board, w)).ToArray();
        var res = new Dictionary<(K9.Arr, string), K8.Fx[]>();
        foreach (var a in arrs)
        {
            foreach (var v in Vers) res[(a, v.Key)] = Waves.Select(w => K8.Measure(K9.Build(a, v.D), w)).ToArray();
            res[(a, "kd0")] = Waves.Select(w => K8.Measure(K9.Build(a, UnitCatalog.KariKd0), w)).ToArray();
        }
        Console.WriteLine($"「{D.PlayRow}」そのもの: " + string.Join(" ／ ", Waves.Select((w, i) => $"{w.Name} {Cell(baseRes[i])}")));
        Console.WriteLine();

        // 0. 確かめ
        string Sig(K8.Fx x) => $"{x.WT}|{x.Fell}|{x.TailDirect}|{x.Marks}|{x.Deepened}|{x.KariDeathT}";
        int same = arrs.Sum(a => Enumerable.Range(0, 4).Count(i => Sig(res[(a, "kz62")][i]) == Sig(res[(a, "kd0")][i])));
        Console.WriteLine($"## 0. 確かめ: KZ-62 と第409期の K-d0 が同じか（25 並び × 4 波 ＝ 100 セル・勝率 ／ T ／ 倒れた数 ／ 尻尾の直 ／ 印 ／ 深めた呪い ／ カーズの倒れたT）: **{same} ／ 100 セル同じ**");
        Console.WriteLine();

        foreach (int wi in new[] { 3, 0 })
        {
            Console.WriteLine($"## 1-{(wi == 3 ? "a" : "b")}. {Waves[wi].Name}（全 {arrs.Length} 並び）");
            Console.WriteLine();
            Console.WriteLine("| 並び | KZ-62 | KZ-90 | KZ-90 − KZ-62 | カーズの倒れた割合・T（62 → 90） | 印（62 → 90） |");
            Console.WriteLine("|---|---|---|--:|---|---|");
            foreach (var a in arrs)
            {
                var x = res[(a, "kz62")][wi]; var y = res[(a, "kz90")][wi];
                Console.WriteLine($"| {a.Label} | {Cell(x)} | {Cell(y)} | {Sg(y.Win - x.Win)} | {Pct(x.KariDied, x.N)}%・{Per(x.KariDeathT, x.KariDied)} → {Pct(y.KariDied, y.N)}%・{Per(y.KariDeathT, y.KariDied)} | {Per2(x.Marks, x.N)} → {Per2(y.Marks, y.N)} |");
            }
            Console.WriteLine();
        }

        Console.WriteLine("## 2. 近衛 ／ 大隊のまとめ（25 並び）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 100% の並び | 0% の並び | 上位3並び |");
        Console.WriteLine("|---|---|--:|--:|---|");
        foreach (var v in Vers)
            foreach (int wi in new[] { 1, 2 })
            {
                var xs = arrs.Select(a => (a, x: res[(a, v.Key)][wi])).ToArray();
                Console.WriteLine($"| {v.Label} | {Waves[wi].Name} | {xs.Count(t => t.x.Win >= 100)} | {xs.Count(t => t.x.Win <= 0)} | {string.Join("、", xs.OrderByDescending(t => t.x.Win).ThenBy(t => KillT(t.x)).Take(3).Select(t => $"{t.a.Label} {t.x.WT}"))} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 3. KZ-90 − KZ-62（同じ並び・同じ seed・勝率の差）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 25 並びの平均 | 上 ／ 下 ／ 同じ | 最大（並び） | 最小（並び） |");
        Console.WriteLine("|---|--:|---|---|---|");
        foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
        {
            var d = arrs.Select(a => (a, d: res[(a, "kz90")][wi].Win - res[(a, "kz62")][wi].Win)).ToArray();
            var mx = d.OrderByDescending(t => t.d).First(); var mn = d.OrderBy(t => t.d).First();
            Console.WriteLine($"| {w.Name} | {Sg(d.Average(t => t.d))} | {d.Count(t => t.d > 0)} ／ {d.Count(t => t.d < 0)} ／ {d.Count(t => t.d == 0)} | {Sg(mx.d)}（{mx.a.Label}） | {Sg(mn.d)}（{mn.a.Label}） |");
        }
        Console.WriteLine();

        Console.WriteLine("## 4. 版ごとの中身（最良 ／ カーズ前列の代表 ／ カーズ後列の代表・戦あたり）");
        Console.WriteLine();
        Console.WriteLine("代表 ＝ その列にカーズがいる並びのうち順位（強4 の勝率 → ボスの勝率 → ボスの倒しT → 近衛 ＋ 大隊）が最も高い並び。印 ＝ −4 の印を付けた敵の数（敵ごとに1回）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 代表 | 並び | 波 | 勝率・T | カーズ: 倒れた・T ／ 受けた | 印 | 深めた呪い | ふつうに殴った | 後列の敵: 倒れた・T | ムド T3 まで |");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|--:|---|---|");
        var best = new Dictionary<string, K9.Arr>();
        foreach (var v in Vers)
        {
            var b0 = Rank(res, v.Key, arrs).First(); best[v.Key] = b0;
            var fr = Rank(res, v.Key, arrs.Where(a => a.Pos <= 1)).First();
            var bk = Rank(res, v.Key, arrs.Where(a => a.Pos >= 3)).First();
            foreach (var (tag, a) in new[] { ("最良", b0), ("前列", fr), ("後列", bk) })
                foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
                {
                    var x = res[(a, v.Key)][wi];
                    Console.WriteLine($"| {v.Label} | {tag} | {a.Label} | {w.Name} | {x.WT} | {Pct(x.KariDied, x.N)}%・{Per(x.KariDeathT, x.KariDied)} ／ {Per(x.KariTaken, x.N)} | {Per2(x.Marks, x.N)} | {Per(x.Deepened, x.N)} | {Per(x.Plain, x.N)} | {Pct(x.BackDied, x.BackN)}%・{Per(x.BackDeathT, x.BackDied)} | {Pct(x.MudoEarly, x.MudoN)}% |");
                }
        }
        var b4 = baseRes[3];
        Console.WriteLine();
        Console.WriteLine($"「{D.PlayRow}」そのもの（強4）: 後列の敵 {Pct(b4.BackDied, b4.BackN)}%・{Per(b4.BackDeathT, b4.BackDied)}T ／ ムド T3 まで {Pct(b4.MudoEarly, b4.MudoN)}%。");
        Console.WriteLine();

        Console.WriteLine("## 5. ワタを残した並び（−ムド ／ −ネル ／ −クビ ／ −ウツ × カーズの位置 5）で成り立つもの（強4 > 0 または ボス 100%）");
        Console.WriteLine();
        Console.WriteLine("| 並び | KZ-62: ボス ／ 強4 | KZ-90: ボス ／ 強4 | KZ-90 で新たに成り立った |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var a in arrs.Where(a => a.Out != "wata"))
        {
            var x = res[(a, "kz62")]; var y = res[(a, "kz90")];
            bool okx = x[3].Win > 0 || x[0].Win >= 100, oky = y[3].Win > 0 || y[0].Win >= 100;
            if (!okx && !oky) continue;
            Console.WriteLine($"| {a.Label} | {x[0].WT} ／ {x[3].WT} | {y[0].WT} ／ {y[3].WT} | {(oky && !okx ? "**はい**" : okx && !oky ? "（崩れた）" : "—")} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 6. 版ごとの最良の並び（4 波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 最良の並び（席） | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var v in Vers)
        {
            var a = best[v.Key];
            Console.WriteLine($"| {v.Label} | {a.Label}（{S307.Seats(K9.Build(a, v.D))}） | " + string.Join(" | ", res[(a, v.Key)].Select(c => c.WT)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 7. 体か札か（カーズ前1 ／ 前3 の並びで、素体 HP 62 ／ 90・攻 4・札なしと並べる・同じ seed）");
        Console.WriteLine();
        Console.WriteLine("| 並び | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var a in arrs.Where(a => a.Out == "wata" && a.Pos <= 1))
            foreach (var (label, d) in new[] { ("KZ-62", UnitCatalog.KariKz62), ("素体 HP 62", PlainOf(UnitCatalog.KariKz62)), ("KZ-90", UnitCatalog.KariKz90), ("素体 HP 90", PlainOf(UnitCatalog.KariKz90)) })
            {
                var xs = label.StartsWith("KZ") ? res[(a, label == "KZ-62" ? "kz62" : "kz90")] : Waves.Select(w => K8.Measure(K9.Build(a, d), w)).ToArray();
                Console.WriteLine($"| {a.Label} | {label} | " + string.Join(" | ", xs.Select(Cell)) + " |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static Formation SwapKari(Formation f, UnitDef d)
    {
        var g = new Formation();
        foreach (var o in f.Occupied()) g[o.Slot] = o.Def.Id == "kari" ? d : o.Def;
        return g;
    }
    static void Mixed()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuildsH410().Where(b => b.F.Occupied().Any(o => o.Def.Id == "kari")).ToArray();   // 第411期: 旧の行名 ＋ 旧のカリ（駆り立て）
        var waves = EnemyCatalog.Stages.Select((st, i) => ($"第{i + 1}波", (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(f, st.Enemy, s, verbose: false))))
            .Concat(new[] { ("近衛", EnemyCatalog.EliteGuardWave), ("大隊", EnemyCatalog.EliteBattalionWave) }
                .Select(x => (x.Item1, (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(x.Item2, EnemyCatalog.EliteScale), s, verbose: false)))))
            .ToArray();
        var vs = new (string Label, UnitDef? D)[] { ("規定（旧のカリ）", null), ("KZ-90", UnitCatalog.KariKz90), ("素体 HP 90 攻 4", PlainOf(UnitCatalog.KariKz90)), ("（参考）KZ-62 ＝ 第409期の K-d0", UnitCatalog.KariKz62), ("（参考）素体 HP 62 攻 4", PlainOf(UnitCatalog.KariKz62)) };
        Console.WriteLine($"# 第410期 §2-4 寄せ集め —— `compare` のカリを含む {rows.Length} 行のカリを差し替えた写し × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..{CompareSeeds - 1}");
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

    /// <summary>第411期: KZ-90 を規定にした確かめ。「試遊・カーズ 呪い」× 4 波が第410期 §3-6 の KZ-90 の行と同じか（違えば exit 1）。</summary>
    static void S411()
    {
        string[] exp = { "100.0・10.0T", "100.0・6.3T", "100.0・5.4T", "100.0・10.1T" };
        var f = Presets.Versions.First(r => r.Name == "試遊・カーズ 呪い").F;
        var got = Waves.Select(w => K8.Measure(f, w).WT).ToArray();
        bool seat = S307.Seats(f) == S307.Seats(Presets.KarzBoard410()) && f.Occupied().Any(o => o.Def == UnitCatalog.Kari) && UnitCatalog.All.Contains(UnitCatalog.Kari);
        int same = Enumerable.Range(0, 4).Count(i => got[i] == exp[i]);
        Console.WriteLine($"# 第411期 「試遊・カーズ 呪い」（{S307.Seats(f)}・規定のカーズ {(seat ? "はい" : "**いいえ**")}）× {string.Join(" ／ ", Waves.Select(w => w.Name))}・seed 0..99");
        Console.WriteLine();
        Console.WriteLine($"第410期 §3-6 の KZ-90: {string.Join(" ／ ", exp)}");
        Console.WriteLine($"いま: {string.Join(" ／ ", got)} → 4 波のうち同じ {same}");
        if (!seat || same != 4) Environment.ExitCode = 1;
    }

    static int _fail;
    static void Expect(string what, bool ok, string detail = "")
    {
        Console.WriteLine($"- [{(ok ? "OK" : "NG")}] {what}{(detail == "" ? "" : $"（{detail}）")}");
        if (!ok) _fail++;
    }
    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# 第410期 kari410 check —— 自己検査");
        Console.WriteLine();
        var k0 = UnitCatalog.KariH406; var z62 = UnitCatalog.KariKz62; var z90 = UnitCatalog.KariKz90; var d0 = UnitCatalog.KariKd0;   // 第411期: 規定のカリは祟りのカーズになった（k0 は旧の駆り立てで見る）
        Expect("版の定義: Id ＝ kari・名前「祟りのカーズ」・HP 62 ／ 90・攻 4 ／ 速 9・札は K-d0 と同じ（`KariMarkDeepen`）・手番は Skill・フレーバーは空",
            new[] { z62, z90 }.All(d => d.Id == k0.Id && d.Name == "祟りのカーズ" && d.Attack == d0.Attack && d.Speed == d0.Speed && d.Traits.SequenceEqual(d0.Traits)
                && d.Actions.Count == 1 && d.Actions[0].Kind == ActionKind.Skill && d.Flavor == "") && z62.MaxHp == 62 && z90.MaxHp == 90);
        Expect("保持者 0 枚（`All` ／ `Retired` に版が無い・規定のカリは Goad のまま）",
            !UnitCatalog.All.Contains(z62) && !UnitCatalog.All.Contains(z90) && !UnitCatalog.Everyone.Any(d => d != UnitCatalog.Kari && d.Traits.Contains(TraitId.KariMarkDeepen)) && k0.Traits.SequenceEqual(new[] { TraitId.Goad }));
        int diff = 0;
        foreach (var a in K9.AllArrs().Where((_, i) => i % 5 == 0))
            foreach (var w in Waves)
                for (int s = 0; s < 10; s++)
                {
                    var (x, _, _) = I.Raw(K9.Build(a, z62), w, s); var (y, _, _) = I.Raw(K9.Build(a, d0), w, s);
                    if (x.PlayerWon != y.PlayerWon || x.Turns != y.Turns || x.PlayerSurvivors != y.PlayerSurvivors || x.TallyByUnit["kari"].K409Marks != y.TallyByUnit["kari"].K409Marks) diff++;
                }
        Expect("KZ-62 は第409期の K-d0 と同じ戦をする（5 並び × 4 波 × seed 0..9・勝敗 ／ T ／ 生存 ／ 印）", diff == 0, $"違い {diff}");
        var names = VersionsH410().Select(r => r.Name).ToList();   // 第411期: 第410期の一覧で見る
        int iz = names.IndexOf(Row410);
        Expect($"試遊の台「{Row410}」が末尾の「版・」の並びにあり、カーズ（KZ-62 か KZ-90）を置く・「版・K-d 呪い」は無い・「版・K-k 呪い」は K-k のまま",
            iz >= 0 && !names.Contains("版・K-d 呪い") && names.Skip(names.IndexOf("版・K-k 呪い")).All(n => n.StartsWith("版・"))
            && VersionsH410()[iz].F.Occupied().Any(o => o.Def == z62 || o.Def == z90)
            && VersionsH410().First(r => r.Name == "版・K-k 呪い").F.Occupied().Any(o => o.Def == UnitCatalog.KariKk));
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて OK**" : $"**NG {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
