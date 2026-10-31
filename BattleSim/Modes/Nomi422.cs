using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// nomi422 —— 第422期【軽い期・版だけ】刻みのノミの作り直し: 一点彫り（NM-c ／ NM-cs ／ NM-cb）。
// 指示書は design/PHASE422_NOMI_CHISEL_SPEC.md ／ 報告は design/PHASE422_NOMI_CHISEL.md。
//
//     dotnet run --project BattleSim -c Release 0 nomi422 boards   # §2-2 ／ §2-3: compare のノミを含む 4 行 × {規定, NM-c, NM-cs, NM-cb} × 4 波・seed 0..99 ＋ ノミ → ミサの勝率
//     dotnet run --project BattleSim -c Release 0 nomi422 mixed    # §2-4: 残りの 5 行 × {規定, NM-c} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 nomi422 log <0|c|cs|cb> <行名の頭> <波の鍵 boss|guard|bat|s4|m1..m5> [seed]
//     dotnet run --project BattleSim -c Release 0 nomi422 bomb     # §6 次の下見: 「試遊・燃焼 ポンの台」「試遊・自傷」× 4 波・燃焼の刻み ／ 被弾の回数 ／戦（新駒「爆弾」の起爆の口の大きさ）
//     dotnet run --project BattleSim -c Release 0 nomi422 check    # 自己検査
//
// **予測のファイルが無ければ boards ／ mixed を走らせない**（R397・`.tmp/p422/predict.md`）。
// =====================================================================================
static class Nomi422Diag
{
    const string PredictFile = ".tmp/p422/predict.md";
    const int Seeds = 100;

    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Nomi), ("c", "NM-c", UnitCatalog.NomiNMc), ("cs", "NM-cs", UnitCatalog.NomiNMcs), ("cb", "NM-cb", UnitCatalog.NomiNMcb),
    };
    static readonly string[] MainRows = { "刻み (ノミ単騎)", "逸らし改 (ソラ×ノミ)", "燃焼×刻み (ボルグ×ホタ×ノミ)", "傷×被弾 (カド×ノミ×リリ)" };
    static readonly string[] RestRows = { "刻み×抉り (ノミ×エグ)", "刻み×断ち (ノミ×ナタ)", "刻み×澱み (ノミ×ミオ)", "見境 (ミサ×ソラ)", "毒×傷 (ノミ×グザ×ミオ)" };
    static readonly I.W[] Four = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    static readonly (string Key, I.W W)[] WaveKeys = new[] { ("boss", I.Boss), ("guard", I.Guard), ("bat", I.Bat), ("s4", I.StrongWaves[2]) }
        .Concat(Enumerable.Range(1, 5).Select(i => ($"m{i}", L.Mains[i - 1]))).ToArray();

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");

    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;
    static Formation Swap(Formation f, UnitDef d)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "nomi") g[o.Slot] = d;
        return g;
    }

    sealed class Fx
    {
        public long N, Wins, WinT, Fell, Dmg, Volleys, Shots, Encore, Hits, DepthSum, DepthMax, Resets, Carry, Bonus, Neck, NeckAmt, NeckPassed, YokeCut;
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Dmg += o.Dmg; Volleys += o.Volleys; Shots += o.Shots; Encore += o.Encore;
            Hits += o.Hits; DepthSum += o.DepthSum; DepthMax = Math.Max(DepthMax, o.DepthMax); Resets += o.Resets; Carry += o.Carry; Bonus += o.Bonus;
            Neck += o.Neck; NeckAmt += o.NeckAmt; NeckPassed += o.NeckPassed; YokeCut += o.YokeCut;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => $"{F1(Win)}・{(Wins == 0 ? "—" : ((double)WinT / Wins).ToString("0.0"))}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
    }

    static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, _) = I.Raw(f, w, seed, verbose: false);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("nomi", out var t))
        {
            a.Dmg = t.DamageToEnemy; a.Volleys = t.PelletVolleys; a.Shots = t.PelletShots; a.Encore = t.EncoreFires;
            a.Hits = t.Ch422Hits; a.DepthSum = t.Ch422DepthSum; a.DepthMax = t.Ch422DepthMax; a.Resets = t.Ch422Resets; a.Carry = t.Ch422Carry; a.Bonus = t.Ch422Bonus;
            a.Neck = t.Ch422Neck; a.NeckAmt = t.Ch422NeckAmt; a.NeckPassed = t.Ch422NeckPassed; a.YokeCut = t.Ch422YokeCut;
        }
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"nomi422: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "boards": Boards(); break;
            case "mixed": Mixed(); break;
            case "log": Log(A(3, "c"), A(4, "刻み (ノミ単騎)"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "bomb": Bomb(); break;
            case "check": Check(); break;
            default: Console.WriteLine("nomi422: モードは boards / mixed / bomb / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Boards()
    {
        Console.WriteLine($"# 第422期 §3 —— ノミを含む 4 行 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", Four.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        var res = new Dictionary<(string, string, string), Fx>();
        var misa = new Dictionary<(string, string), Fx>();
        foreach (var row in MainRows)
            foreach (var w in Four)
            {
                foreach (var v in Vers) res[(row, w.Name, v.Key)] = Measure(Swap(Row(row), v.D), w);
                misa[(row, w.Name)] = Measure(Swap(Row(row), UnitCatalog.Tome), w);
            }
        foreach (var row in MainRows)
        {
            Console.WriteLine($"## {row}（{S307.Seats(Row(row))}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 勝率・倒しT | 規定との差 | ミサとの差 | 倒れた ／戦 | 倒れた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|---|");
            foreach (var w in Four)
            {
                foreach (var v in Vers)
                {
                    var a = res[(row, w.Name, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {(v.Key == "0" ? "—" : Delta(a.Win - res[(row, w.Name, "0")].Win))} | {Delta(a.Win - misa[(row, w.Name)].Win)} | {Per(a.Fell, a.N)} | {a.Fall} |");
                }
                var m = misa[(row, w.Name)];
                Console.WriteLine($"| {w.Name} | （ミサ）| {m.WT} | {Delta(m.Win - res[(row, w.Name, "0")].Win)} | — | {Per(m.Fell, m.N)} | {m.Fall} |");
            }
            Console.WriteLine();
            Console.WriteLine("ノミ: 与ダメ ／戦・一振り（規定は豆鉄砲の一振り ／ 版は彫った一撃）／戦・彫りの平均 ／ 最大・相手が替わって 0 に戻った ／戦・持ち越した彫り ／戦・再行動 ／戦・遮られない一撃（回 ／ 量 ／ 軛を抜けた）・軛で切られた一撃の割合。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 与ダメ ／戦 | 一振り ／戦 | 彫り 平均 ／ 最大 | 0 に戻った | 持ち越し | 再行動 ／戦 | 遮られない（回 ／ 量 ／ 抜けた）| 軛で切られた |");
            Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|---|--:|");
            foreach (var w in Four)
                foreach (var v in Vers)
                {
                    var a = res[(row, w.Name, v.Key)];
                    long swings = v.Key == "0" ? a.Volleys : a.Hits;
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per1(a.Dmg, a.N)} | {Per(swings, a.N)} | {(v.Key == "0" ? "—" : $"{Per1(a.DepthSum, a.Hits)} ／ {a.DepthMax}")} | {(v.Key == "0" ? "—" : Per(a.Resets, a.N))} | {(v.Key == "0" ? "—" : Per1(a.Carry, a.N))} | {Per(a.Encore, a.N)} | "
                        + $"{(v.Key == "cb" ? $"{Per(a.Neck, a.N)} ／ {Per1(a.NeckAmt, a.Neck)} ／ {Per(a.NeckPassed, a.N)}" : "—")} | {(v.Key == "0" ? "—" : Pc(a.YokeCut, a.Hits))} |");
                }
            Console.WriteLine();
        }
        Console.WriteLine("## まとめ（4 波の平均勝率・規定との差 ／ ミサに替えた方が良い波の数（4 波中））");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Vers.Select(v => v.Label)) + " | ミサ |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")) + "--:|");
        var misaBetter = Vers.ToDictionary(v => v.Key, _ => 0);
        foreach (var row in MainRows)
        {
            double b0 = Four.Average(w => res[(row, w.Name, "0")].Win);
            double mm = Four.Average(w => misa[(row, w.Name)].Win);
            var cells = Vers.Select(v =>
            {
                double x = Four.Average(w => res[(row, w.Name, v.Key)].Win);
                int mb = Four.Count(w => misa[(row, w.Name)].Win > res[(row, w.Name, v.Key)].Win);
                if (mm > x) misaBetter[v.Key]++;
                return $"{F1(x)}{(v.Key == "0" ? "" : $"（{Delta(x - b0)}）")} ／ {mb}";
            });
            Console.WriteLine($"| {row} | " + string.Join(" | ", cells) + $" | {F1(mm)} |");
        }
        Console.WriteLine();
        Console.WriteLine("ミサに替えた方が良い行の数（4 行中・4 波平均で）: " + string.Join("・", Vers.Select(v => $"{v.Label} {misaBetter[v.Key]}")));
        Console.WriteLine();
    }

    static void Mixed()
    {
        var waves = L.Mains.Append(I.Guard).Append(I.Bat).ToArray();
        Console.WriteLine($"# 第422期 §2-4 —— 残りの 5 行 × {{規定, NM-c}} × 本編 第1〜5波 ＋ 近衛 ／ 大隊・seed 0..199");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " | 第2〜5波の平均 |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|");
        foreach (var name in RestRows)
        {
            var f = Row(name);
            Fx[] b = waves.Select(w => Measure(f, w, 200)).ToArray();
            Fx[] x = waves.Select(w => Measure(Swap(f, UnitCatalog.NomiNMc), w, 200)).ToArray();
            double m0 = Enumerable.Range(1, 4).Average(i => b[i].Win), m1 = Enumerable.Range(1, 4).Average(i => x[i].Win);
            Console.WriteLine($"| {name} | 規定 | " + string.Join(" | ", b.Select(a => F1(a.Win))) + $" | {F1(m0)} |");
            Console.WriteLine($"| {name} | NM-c | " + string.Join(" | ", x.Select((a, i) => $"{F1(a.Win)}（{Delta(a.Win - b[i].Win)}）")) + $" | {F1(m1)}（{Delta(m1 - m0)}）|");
        }
        Console.WriteLine();
    }


    static void Bomb()
    {
        string[] boards = { "試遊・燃焼 ポンの台", "試遊・自傷" };
        Console.WriteLine($"# 第422期 §6 次の下見 —— 新駒「爆弾」の起爆の口の大きさ: 台 × 4 波・seed 0..{Seeds - 1}（いまの規定）");
        Console.WriteLine();
        Console.WriteLine("燃焼の刻み ＝ 燃えたターンの延べ数（`BurnTicks`・味方 ／ 敵）。被弾 ＝ 敵の直の一撃で味方が HP か破片を減らされた回数（台本の Damage・中継 ／ 刻みを除く）・そのうち範囲の一撃。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率・倒しT | 決着T | 燃焼の刻み 味方 ／戦 | 燃焼の刻み 敵 ／戦 | 被弾 ／戦 | うち範囲 | 被弾 ／ ターン |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var name in boards)
        {
            var f = Presets.Playtest.Concat(Presets.Versions).First(r => r.Name == name).F;
            foreach (var w in Four)
            {
                var parts = new (long Win, long WinT, long Turns, long BurnP, long BurnE, long Hits, long Aoe)[Seeds];
                Parallel.For(0, Seeds, sd =>
                {
                    var (r, p, e) = I.Raw(f, w, sd, verbose: true);
                    var pid = p.Select(u => u.InstanceId).ToHashSet(); var eid = e.Select(u => u.InstanceId).ToHashSet();
                    long hits = 0, aoe = 0;
                    foreach (var ev in r.Events)
                    {
                        if (ev.Kind != BattleEventKind.Damage || ev.Relayed || ev.TargetId is not int t || !pid.Contains(t) || ev.ActorId is not int a || !eid.Contains(a) || ev.Pattern is null) continue;
                        hits++; if (ev.Pattern != AttackPattern.Single) aoe++;
                    }
                    long bp = p.DistinctBy(u => u.Def.Id).Sum(u => r.TallyByUnit.TryGetValue(u.Def.Id, out var tt) ? tt.BurnTicks : 0);
                    long be = e.DistinctBy(u => u.Def.Id).Sum(u => r.TallyByUnit.TryGetValue(u.Def.Id, out var tt) ? tt.BurnTicks : 0);
                    parts[sd] = (r.PlayerWon ? 1 : 0, r.PlayerWon ? r.Turns : 0, r.Turns, bp, be, hits, aoe);
                });
                long n = Seeds, wins = parts.Sum(x => x.Win), turns = parts.Sum(x => x.Turns), hitsAll = parts.Sum(x => x.Hits);
                Console.WriteLine($"| {name} | {w.Name} | {F1(100.0 * wins / n)}・{(wins == 0 ? "—" : ((double)parts.Sum(x => x.WinT) / wins).ToString("0.0"))}T | {Per1(turns, n)} | {Per1(parts.Sum(x => x.BurnP), n)} | {Per1(parts.Sum(x => x.BurnE), n)} | {Per1(hitsAll, n)} | {Pc(parts.Sum(x => x.Aoe), hitsAll)} | {Per(hitsAll, turns)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("注意: `TallyByUnit` は駒の Id ごとの帳簿（同じ Id の敵が複数いれば合算）。燃焼の刻みは「燃えていた駒 × ターン」の延べ。");
        Console.WriteLine();
    }

    static void Log(string vk, string head, string wk, int seed)
    {
        var name = CompareBuilds().Concat(CrossBuilds()).First(r => r.Name.StartsWith(head)).Name;
        var f = Swap(Row(name), Vers.First(v => v.Key == vk).D);
        var w = WaveKeys.First(x => x.Key == wk).W;
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × {Vers.First(v => v.Key == vk).Label}・seed {seed}: PlayerWon={r.PlayerWon}・T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(new string(' ', l.Indent * 2) + l.Text);
    }

    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第422期 nomi422 check");
        Console.WriteLine();
        var nm = UnitCatalog.Nomi;
        var baseT = nm.Traits.Where(t => t != TraitId.Pellet).Append(TraitId.Chisel).ToArray();
        Req(UnitCatalog.NomiNMc.Traits.SequenceEqual(baseT), "NM-c ＝ 規定 − Pellet ＋ Chisel");
        Req(UnitCatalog.NomiNMcs.Traits.SequenceEqual(baseT.Append(TraitId.ChiselWound)), "NM-cs ＝ NM-c ＋ ChiselWound");
        Req(UnitCatalog.NomiNMcb.Traits.SequenceEqual(baseT.Append(TraitId.ChiselNeck)), "NM-cb ＝ NM-c ＋ ChiselNeck");
        Req(Vers.Skip(1).All(v => v.D.Id == "nomi" && v.D.MaxHp == nm.MaxHp && v.D.Attack == nm.Attack && v.D.Speed == nm.Speed && v.D.Name == nm.Name && v.D.Flavor == nm.Flavor && v.D.MinusText == nm.MinusText),
            "版の Id ／ 名前 ／ HP ／ 攻 ／ 速 ／ フレーバー ／ マイナスは規定のまま");
        Req(!UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.Chisel) || d.Traits.Contains(TraitId.ChiselWound) || d.Traits.Contains(TraitId.ChiselNeck)), "新しい札の保持者は `All` に 0 枚");
        var solo = Row("刻み (ノミ単騎)");
        var c = Measure(Swap(solo, UnitCatalog.NomiNMc), I.Boss, 10);
        Req(c.Volleys == 0 && c.Shots == 0 && c.Hits > 0, $"NM-c は連打しない（一振り 0・彫った一撃 {c.Hits}）");
        Req(c.DepthMax >= 5, $"NM-c はボスで彫りが深まる（最大 {c.DepthMax}）");
        var cb = Measure(Swap(solo, UnitCatalog.NomiNMcb), I.StrongWaves[2], 20);
        Req(Enumerable.Range(0, 20).All(s => One(Swap(solo, UnitCatalog.NomiNMcb), I.StrongWaves[2], s).Neck <= 1), $"NM-cb の遮られない一撃は 1 戦 1 回まで（強4 20 戦で {cb.Neck} 回）");
        bool same = true;
        foreach (var v in Vers.Skip(1))
            for (int s = 0; s < 6; s++)
            {
                var f = Swap(solo, v.D);
                var (r1, _, _) = I.Raw(f, I.Guard, s, verbose: false);
                var (r2, _, _) = I.Raw(f, I.Guard, s, verbose: true);
                if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
            }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（3 版 × ノミ単騎 × 近衛 × 6 seed）");
        string src = File.ReadAllText("BattleCore/BattleEngine.cs");
        int i0 = src.IndexOf("// ---- 第422期（ノミの版 一点彫り", StringComparison.Ordinal);
        int i1 = src.IndexOf("// ---- 第420期（セッキの版 SW-n ／ SW-nd）", StringComparison.Ordinal);
        Req(i0 > 0 && i1 > i0 && !src[i0..i1].Contains("PickOne") && !src[i0..i1].Contains("Roll("), "`Chisel422` は `PickOne` ／ `Roll` を使わない");
        var row = Presets.Versions.FirstOrDefault(r => r.Name == "版・彫り ノミ");
        Req(row.Name is not null && Presets.Versions[^1].Name == "版・彫り ノミ" && row.F.Occupied().Any(o => Vers.Skip(1).Any(v => v.D == o.Def)), "「版・彫り ノミ」は `Presets.Versions` の末尾で、ノミは版");
        Req(!VersionsH407().Any(r => r.Name == "版・彫り ノミ"), "鎖の終端（`VersionsH407`）は「版・彫り ノミ」を外す");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
