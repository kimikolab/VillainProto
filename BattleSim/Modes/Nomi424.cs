using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// nomi424 —— 第424期【軽い期・版だけ】刻みのノミ③: 毎発刻む（NM-w）に読みの上限を足す（NM-wa ／ NM-wc ／ NM-w1）。
// 指示書は design/PHASE424_NOMI_CAP_SPEC.md ／ 報告は design/PHASE424_NOMI_CAP.md。
//
//     dotnet run --project BattleSim -c Release 0 nomi424 boards   # §2-2 ／ §2-3: 5 行 × {規定, NM-w, NM-wa, NM-wc, NM-w1} × 4 波・seed 0..99 ＋ ミサの列 ＋ 軸の最良との横並び ＋ ボスの推移（版ごと）
//     dotnet run --project BattleSim -c Release 0 nomi424 mixed    # §2-4: 残りの 4 行 × {規定, NM-wa, NM-wc（参考）} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 nomi424 log <0|w|wa|wc|w1> <行名の頭> <波の鍵 boss|guard|bat|s4|m1..m5> [seed]
//     dotnet run --project BattleSim -c Release 0 nomi424 check    # 自己検査
// =====================================================================================
static class Nomi424Diag
{
    const string PredictFile = ".tmp/p424/predict.md";
    const int Seeds = 100;
    static readonly I.W[] Four = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    static readonly string[] MainRows = { "刻み (ノミ単騎)", "逸らし改 (ソラ×ノミ)", "燃焼×刻み (ボルグ×ホタ×ノミ)", "傷×被弾 (カド×ノミ×リリ)", "刻み×澱み (ノミ×ミオ)" };
    static readonly string[] RestRows = { "刻み×抉り (ノミ×エグ)", "刻み×断ち (ノミ×ナタ)", "見境 (ミサ×ソラ)", "毒×傷 (ノミ×グザ×ミオ)" };
    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Nomi), ("w", "NM-w", UnitCatalog.NomiNMw), ("wa", "NM-wa", UnitCatalog.NomiNMwa),
        ("wc", "NM-wc", UnitCatalog.NomiNMwc), ("w1", "NM-w1", UnitCatalog.NomiNMw1),
    };
    /// <summary>第414期 §3-2 の軸ごとの代表台の 4 波平均（指示書 §2-3 が挙げた値・死まで）。</summary>
    static readonly (string Axis, double Best)[] AxisBest414 =
        { ("燃焼", 100.0), ("感電", 100.0), ("毒", 100.0), ("デバフ", 100.0), ("標", 99.8), ("移動", 99.5), ("死", 74.2) };
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

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"nomi424: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        BattleContext.WoundTrace423 = true;   // 計数のみ（盤面は動かない・check で確かめる）
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "boards": Boards(); break;
            case "mixed": Mixed(); break;
            case "log": Log(A(3, "wa"), A(4, "刻み (ノミ単騎)"), A(5, "boss"), int.Parse(A(6, "0"))); break;
            case "check": Check(); break;
            default: Console.WriteLine("nomi424: モードは boards / mixed / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    sealed class Fx
    {
        public long N, Wins, WinT, Fell, Dmg, Volleys, Shots, Writes, Peak, PeakMax, Reads, ReadAmt, TurnMax, TurnMaxMax, Encore, YokeCut, Hits;
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Dmg += o.Dmg; Volleys += o.Volleys; Shots += o.Shots; Writes += o.Writes;
            Peak += o.Peak; PeakMax = Math.Max(PeakMax, o.PeakMax); Reads += o.Reads; ReadAmt += o.ReadAmt;
            TurnMax += o.TurnMax; TurnMaxMax = Math.Max(TurnMaxMax, o.TurnMaxMax); Encore += o.Encore; YokeCut += o.YokeCut; Hits += o.Hits;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => $"{F1(Win)}・{(Wins == 0 ? "—" : ((double)WinT / Wins).ToString("0.0"))}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
    }

    static Fx One(Formation f, I.W w, int seed, bool yoke = false)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: yoke);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("nomi", out var t))
        {
            a.Dmg = t.DamageToEnemy; a.Volleys = t.PelletVolleys; a.Shots = t.PelletShots; a.Writes = t.WoundWrites; a.Reads = t.Wd423Reads; a.ReadAmt = t.Wd423ReadAmt; a.Encore = t.EncoreFires;
            a.TurnMax = t.Wd424TurnMax; a.TurnMaxMax = t.Wd424TurnMax;
        }
        long pk = e.DistinctBy(u => u.Def.Id).Max(u => r.TallyByUnit.TryGetValue(u.Def.Id, out var tt) ? tt.Wd423Peak : 0);
        a.Peak = pk; a.PeakMax = pk;
        if (yoke)
        {
            int nid = p.First(u => u.Def.Id == "nomi").InstanceId;
            foreach (var ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.Damage && ev.ActorId == nid && !ev.Relayed) a.Hits++;
                if (ev.Kind == BattleEventKind.Sealed && ev.ActorId == nid && ev.Text == SealedLabels.Yoke) a.YokeCut++;
            }
        }
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n = Seeds, bool yoke = false)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s, yoke));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static void Boards()
    {
        Console.WriteLine($"# 第424期 §3 —— ノミを含む 5 行 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", Four.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        var res = new Dictionary<(string, string, string), Fx>();
        var misa = new Dictionary<(string, string), Fx>();
        foreach (var row in MainRows)
            foreach (var w in Four)
            {
                foreach (var v in Vers) res[(row, w.Name, v.Key)] = Measure(Swap(Row(row), v.D), w, Seeds, yoke: w == I.StrongWaves[2]);
                misa[(row, w.Name)] = Measure(Swap(Row(row), UnitCatalog.Tome), w);
            }
        foreach (var row in MainRows)
        {
            Console.WriteLine($"## {row}（{S307.Seats(Row(row))}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 勝率・倒しT | 規定との差 | NM-w との差 | ミサとの差 | 倒れた ／戦 | 倒れた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|");
            foreach (var w in Four)
            {
                foreach (var v in Vers)
                {
                    var a = res[(row, w.Name, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {(v.Key == "0" ? "—" : Delta(a.Win - res[(row, w.Name, "0")].Win))} | {(v.Key is "0" or "w" ? "—" : Delta(a.Win - res[(row, w.Name, "w")].Win))} | {Delta(a.Win - misa[(row, w.Name)].Win)} | {Per(a.Fell, a.N)} | {a.Fall} |");
                }
                var m = misa[(row, w.Name)];
                Console.WriteLine($"| {w.Name} | （ミサ）| {m.WT} | {Delta(m.Win - res[(row, w.Name, "0")].Win)} | — | — | {Per(m.Fell, m.N)} | {m.Fall} |");
            }
            Console.WriteLine();
            Console.WriteLine("ノミ: 与ダメ ／戦（ノミの名で入った分）・一振り ／戦・発 ／ 一振り・刻んだ傷 ／戦・相手の傷の最大（平均 ／ 最大）・傷の読み（回 ／ 量 ／戦）・1 手番の読みの最大（戦ごとの最大の平均 ／ 全戦の最大）・再行動 ／戦・軛で切られた発の割合（強4 だけ）。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 与ダメ | 一振り | 発 ／ 一振り | 刻んだ傷 | 傷の最大 | 傷の読み（回 ／ 量）| 1 手番の読みの最大 | 再行動 | 軛で切られた |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|---|--:|--:|");
            foreach (var w in Four)
                foreach (var v in Vers)
                {
                    var a = res[(row, w.Name, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per1(a.Dmg, a.N)} | {Per(a.Volleys, a.N)} | {Per1(a.Shots, a.Volleys)} | {Per1(a.Writes, a.N)} | {Per1(a.Peak, a.N)} ／ {a.PeakMax} | {Per1(a.Reads, a.N)} ／ {Per1(a.ReadAmt, a.N)} | {Per1(a.TurnMax, a.N)} ／ {a.TurnMaxMax} | {Per(a.Encore, a.N)} | {(w == I.StrongWaves[2] ? Pc(a.YokeCut, a.Hits) : "—")} |");
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
        Console.WriteLine("ミサに替えた方が良い行の数（5 行中・4 波平均で）: " + string.Join("・", Vers.Select(v => $"{v.Label} {misaBetter[v.Key]}")));
        Console.WriteLine();

        // §2-3 横並び: ノミ単騎の 4 波平均 vs 第414期の軸ごとの最良
        Console.WriteLine("## 横並び —— ノミ単騎の 4 波平均（ボス ／ 近衛 ／ 大隊 ／ 強4）と、第414期 §3-2 の軸ごとの代表台の 4 波平均");
        Console.WriteLine();
        Console.WriteLine("| 版 | ノミ単騎（4 波）| 4 波平均 | 上にいる軸 | 並ぶ軸 |");
        Console.WriteLine("|---|---|--:|---|---|");
        foreach (var v in Vers)
        {
            var xs = Four.Select(w => res[(MainRows[0], w.Name, v.Key)].Win).ToArray();
            double avg = xs.Average();
            string above = string.Join("・", AxisBest414.Where(b => b.Best > avg + 0.05).Select(b => $"{b.Axis} {F1(b.Best)}"));
            string tie = string.Join("・", AxisBest414.Where(b => Math.Abs(b.Best - avg) <= 0.05).Select(b => b.Axis));
            Console.WriteLine($"| {v.Label} | {string.Join(" ／ ", xs.Select(F1))} | {F1(avg)} | {(above == "" ? "—" : above)} | {(tie == "" ? "—" : tie)} |");
        }
        Console.WriteLine();
        Console.WriteLine("（第414期の値: " + string.Join("・", AxisBest414.Select(b => $"{b.Axis} {F1(b.Best)}")) + "。反撃 43.2・自傷 3.0・破片 1.2 は下にある。）");
        Console.WriteLine();
        foreach (var v in Vers) BossTrace(v.D, v.Label);
    }

    /// <summary>ボス（勇者 1 体）× ノミ単騎: ターンごとのノミの与ダメと勇者の傷（その戦が続いていた戦の平均・T1〜T8）。</summary>
    static void BossTrace(UnitDef nomi, string label)
    {
        var f = Swap(Row(MainRows[0]), nomi);
        const int T = 8;
        var dmg = new long[T + 1]; var wnd = new long[T + 1]; var alive = new long[T + 1];
        object gate = new();
        Parallel.For(0, Seeds, s =>
        {
            var (r, p, e) = I.Raw(f, I.Boss, s, verbose: true);
            int nid = p.First(u => u.Def.Id == "nomi").InstanceId;
            var d = new long[T + 1];
            int turn = 0;
            foreach (var ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.TurnStart) turn = ev.Turn;
                if (ev.Kind == BattleEventKind.Damage && ev.ActorId == nid && turn >= 1 && turn <= T) d[turn] += ev.Amount;
            }
            var bt = e.Select(u => r.TallyByUnit.TryGetValue(u.Def.Id, out var t) ? t.Wd423ByTurn : null).FirstOrDefault(x => x is not null);
            lock (gate)
            {
                int last = 0;
                for (int t = 1; t <= T; t++)
                {
                    dmg[t] += d[t];
                    if (bt is not null && t < bt.Length && bt[t] > 0) last = bt[t];
                    if (t <= r.Turns) { wnd[t] += last; alive[t]++; }
                }
            }
        });
        Console.WriteLine($"## ボス × ノミ単騎（{label}）—— ターンごとのノミの与ダメ（台本の Damage・ノミが出どころ・全戦の平均）と勇者の傷（その戦が続いていた戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| T | " + string.Join(" | ", Enumerable.Range(1, T)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, T).Select(_ => "--:|")));
        Console.WriteLine("| ノミの与ダメ ／戦 | " + string.Join(" | ", Enumerable.Range(1, T).Select(t => Per1(dmg[t], Seeds))) + " |");
        Console.WriteLine("| 勇者の傷 | " + string.Join(" | ", Enumerable.Range(1, T).Select(t => Per1(wnd[t], alive[t]))) + " |");
        Console.WriteLine("| 続いていた戦 | " + string.Join(" | ", Enumerable.Range(1, T).Select(t => alive[t].ToString())) + " |");
        Console.WriteLine();
    }

    static void Mixed()
    {
        var waves = L.Mains.Append(I.Guard).Append(I.Bat).ToArray();
        var xs = new[] { ("NM-wa", UnitCatalog.NomiNMwa), ("NM-wc", UnitCatalog.NomiNMwc) };
        Console.WriteLine($"# 第424期 §2-4 —— 残りの 4 行 × {{規定, NM-wa（指示書）, NM-wc（試遊の台の候補・参考）}} × 本編 第1〜5波 ＋ 近衛 ／ 大隊・seed 0..199");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " | 第2〜5波の平均 |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|");
        var drops = xs.ToDictionary(x => x.Item1, _ => 0); var misaBetter = xs.ToDictionary(x => x.Item1, _ => 0);
        foreach (var name in RestRows)
        {
            var f = Row(name);
            bool hasNomi = f.Occupied().Any(o => o.Def.Id == "nomi");
            Fx[] b = waves.Select(w => Measure(f, w, 200)).ToArray();
            double m0 = Enumerable.Range(1, 4).Average(i => b[i].Win);
            Fx[]? ms = hasNomi ? waves.Select(w => Measure(Swap(f, UnitCatalog.Tome), w, 200)).ToArray() : null;
            double mm = ms is null ? double.NaN : Enumerable.Range(1, 4).Average(i => ms[i].Win);
            Console.WriteLine($"| {name} | 規定 | " + string.Join(" | ", b.Select(a => F1(a.Win))) + $" | {F1(m0)} |");
            foreach (var (label, d) in xs)
            {
                Fx[] x = waves.Select(w => Measure(Swap(f, d), w, 200)).ToArray();
                double m1 = Enumerable.Range(1, 4).Average(i => x[i].Win);
                drops[label] += Enumerable.Range(0, waves.Length).Count(i => x[i].Win - b[i].Win <= -10.0);
                if (hasNomi && mm > m1) misaBetter[label]++;
                Console.WriteLine($"| {name} | {label} | " + string.Join(" | ", x.Select((a, i) => $"{F1(a.Win)}（{Delta(a.Win - b[i].Win)}）")) + $" | {F1(m1)}（{Delta(m1 - m0)}）|");
            }
            if (ms is not null) Console.WriteLine($"| {name} | （ミサ）| " + string.Join(" | ", ms.Select(a => F1(a.Win))) + $" | {F1(mm)} |");
        }
        Console.WriteLine();
        foreach (var (label, _) in xs)
            Console.WriteLine($"- {label}: −10pt 以上落ちたセル {drops[label]}・ミサに替えた方が良い行（第2〜5波の平均で・ノミのいる行）{misaBetter[label]}。");
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
        Console.WriteLine("# 第424期 nomi424 check");
        Console.WriteLine();
        var nm = UnitCatalog.Nomi; var w = UnitCatalog.NomiNMw;
        Req(UnitCatalog.NomiNMwa.Traits.SequenceEqual(w.Traits.Append(TraitId.CarveReadFirst)), "NM-wa ＝ NM-w ＋ CarveReadFirst");
        Req(UnitCatalog.NomiNMwc.Traits.SequenceEqual(w.Traits.Append(TraitId.CarveReadCap)), "NM-wc ＝ NM-w ＋ CarveReadCap");
        Req(UnitCatalog.NomiNMw1.Traits.SequenceEqual(w.Traits.Append(TraitId.CarveRead1)), "NM-w1 ＝ NM-w ＋ CarveRead1");
        Req(new[] { UnitCatalog.NomiNMwa, UnitCatalog.NomiNMwc, UnitCatalog.NomiNMw1 }.All(d => d.Id == "nomi" && d.MaxHp == nm.MaxHp && d.Attack == nm.Attack && d.Speed == nm.Speed && d.Name == nm.Name && d.Flavor == nm.Flavor && d.MinusText == nm.MinusText),
            "版の Id ／ 名前 ／ HP ／ 攻 ／ 速 ／ フレーバー ／ マイナスは規定のまま");
        Req(UnitCatalog.NomiNMwa.PlusText.Contains("一振りの最初の一発は") && UnitCatalog.NomiNMwc.PlusText.Contains("一発に 20 まで") && UnitCatalog.NomiNMw1.PlusText.Contains("1 を上乗せ"),
            "文面: NM-wa は最初の一発 ／ NM-wc は一発に 20 まで ／ NM-w1 は 1 を上乗せ");
        Req(!UnitCatalog.All.Any(d => d.Traits.Any(t => t is TraitId.CarveReadFirst or TraitId.CarveReadCap or TraitId.CarveRead1)), "3 枚の札の保持者は `All` に 0 枚");
        var solo = Row(MainRows[0]);
        var aw = Measure(Swap(solo, w), I.Boss, 10); var aa = Measure(Swap(solo, UnitCatalog.NomiNMwa), I.Boss, 10);
        var ac = Measure(Swap(solo, UnitCatalog.NomiNMwc), I.Boss, 10); var a1 = Measure(Swap(solo, UnitCatalog.NomiNMw1), I.Boss, 10);
        Req(aa.Writes > aa.Volleys * 3 && aa.Reads <= aa.Volleys, $"NM-wa は毎発刻み、読むのは一振りに 1 回まで（ボス 10 戦: 刻んだ {aa.Writes}・読んだ {aa.Reads}・一振り {aa.Volleys}）");
        Req(ac.ReadAmt <= ac.Reads * CarveTrait.ReadCap && ac.ReadAmt > 0, $"NM-wc の 1 発の読みは {CarveTrait.ReadCap} まで（読み {ac.ReadAmt} ≤ {CarveTrait.ReadCap} × {ac.Reads}・1 手番の最大 {ac.TurnMaxMax}・発は攻撃力の数なので手番の上限は発の数で伸びる）");
        Req(a1.Writes > 0 && aw.TurnMaxMax > aa.TurnMaxMax, $"1 手番の読みの最大: NM-w {aw.TurnMaxMax} ＞ NM-wa {aa.TurnMaxMax}（NM-w1 {a1.TurnMaxMax}）");
        bool same = true;
        foreach (var v in Vers.Skip(2))
            for (int s = 0; s < 6; s++)
            {
                var f = Swap(solo, v.D);
                var (r1, _, _) = I.Raw(f, I.Guard, s, verbose: false);
                var (r2, _, _) = I.Raw(f, I.Guard, s, verbose: true);
                if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
            }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（3 版 × ノミ単騎 × 近衛 × 6 seed）");
        bool cens = true;
        foreach (var s in Enumerable.Range(0, 8))
        {
            BattleContext.WoundTrace423 = false; var x = I.Raw(Swap(solo, UnitCatalog.NomiNMwa), I.Bat, s).R;
            BattleContext.WoundTrace423 = true; var y = I.Raw(Swap(solo, UnitCatalog.NomiNMwa), I.Bat, s).R;
            cens &= x.PlayerWon == y.PlayerWon && x.Turns == y.Turns && x.PlayerSurvivors == y.PlayerSurvivors;
        }
        Req(cens, "傷の計数（`WoundTrace423`）の有無で勝敗 ／ T ／ 生存が同じ（NM-wa × ノミ単騎 × 大隊 × 8 seed）");
        var row = Presets.Versions.FirstOrDefault(r => r.Name == "版・刻み ノミ");
        Req(row.Name is not null && Presets.Versions[^1].Name == "版・刻み ノミ" && row.F.Occupied().Any(o => o.Def == PlaytestPick), $"「版・刻み ノミ」が末尾で、ノミはこの期の版（{Vers.First(v => v.D == PlaytestPick).Label}）");
        Req(!VersionsH407().Any(r => r.Name == "版・刻み ノミ"), "鎖の終端（`VersionsH407`）は「版・刻み ノミ」を外す");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }

    /// <summary>試遊の台「版・刻み ノミ」に入れた版（この期の線を満たす版）。</summary>
    static UnitDef PlaytestPick => UnitCatalog.NomiNMwc;
}
