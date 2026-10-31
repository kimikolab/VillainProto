using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// chiri427 —— 第427期【軽い期・版だけ】導火線のチリ②: 敵にだけ仕掛ける（BM-e ／ BM-ex ／ BM-em）。
// 指示書は design/PHASE427_CHIRI_SPEC.md ／ 報告は design/PHASE427_CHIRI.md。
//
//     dotnet run --project BattleSim -c Release 0 chiri427 boards   # §3-2 ／ §3-3: 台 4 つ（後1）＋ 参考（標 守り型の中央）× {BM-e, BM-ex, BM-em, 素体} × 4 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 chiri427 mixed    # §3-4: 寄せ集め 3 行（第426期と同じ行と席）× {元の行, 素体, BM-e} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 chiri427 log <e|ex|em|0> <台の頭> <席 後1|中央> <波の鍵 boss|guard|bat|s4|m1..m5> [seed]
//     dotnet run --project BattleSim -c Release 0 chiri427 check    # 自己検査
// =====================================================================================
static class Chiri427Diag
{
    const string PredictFile = ".tmp/p427/predict.md";
    const int Seeds = 100;
    internal const string PlayRow = "版・導火線 チリ";
    static readonly I.W[] Four = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    /// <summary>§3-2 の台と席（後1 に固定）。標 守り型は後1 がミサ本人なので、ミサを残す中央（ドハ）を参考に足した（報告 §1）。</summary>
    static readonly (string Board, int Slot, bool Ref)[] Cells =
    {
        ("試遊・刻み ノミ", 3, false), ("試遊・燃焼 ポンの台", 3, false), ("試遊・呪い", 3, false), ("試遊・標 守り型", 3, false), ("試遊・標 守り型", 2, true),
    };
    internal static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("e", "BM-e", UnitCatalog.ChiriBMe), ("ex", "BM-ex", UnitCatalog.ChiriBMex), ("em", "BM-em", UnitCatalog.ChiriBMem), ("0", "素体", UnitCatalog.ChiriBM0),
    };
    static readonly (string Row, int Slot)[] Mixed3 = { ("継ぎ当て×被弾強化", 4), ("継ぎ当て×分散回復", 2), ("耐久 (ガルド×リリ)", 2) };
    static readonly (string Key, I.W W)[] WaveKeys = new[] { ("boss", I.Boss), ("guard", I.Guard), ("bat", I.Bat), ("s4", I.StrongWaves[2]) }
        .Concat(Enumerable.Range(1, 5).Select(i => ($"m{i}", L.Mains[i - 1]))).ToArray();

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");
    static Formation Board(string n) => Presets.Versions.Concat(Presets.Playtest).First(r => r.Name == n).F;   // 「試遊・標 守り型」は `Presets.Playtest` の台
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    static string CellName((string Board, int Slot, bool Ref) c) => $"{c.Board}・{FormationRules.SeatNames[c.Slot]}（{Short(Board(c.Board)[c.Slot]!)} を替えた）{(c.Ref ? "【参考】" : "")}";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"chiri427: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "boards": BoardsRun(); break;
            case "mixed": MixedRun(); break;
            case "log": Log(A(3, "e"), A(4, "試遊・刻み ノミ"), A(5, "後1"), A(6, "boss"), int.Parse(A(7, "0"))); break;
            case "check": Check(); break;
            default: Console.WriteLine("chiri427: モードは boards / mixed / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    sealed class Fx
    {
        public long N, Wins, WinT, Fell;
        public UnitTally B = new();
        public readonly Dictionary<string, long> Death = new(), DeathT = new(), Fuse = new(), Det = new(), HeldN = new(), HeldW = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; B.Add(o.B);
            foreach (var (d, od) in new[] { (Death, o.Death), (DeathT, o.DeathT), (Fuse, o.Fuse), (Det, o.Det), (HeldN, o.HeldN), (HeldW, o.HeldW) })
                foreach (var (k, v) in od) d[k] = d.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => Wins == 0 ? F1(Win) : $"{F1(Win)}・{(double)WinT / Wins:0.0}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
        public long Blasts => B.Bm426BlastAlly + B.Bm426BlastFoe;
        public double MeanFoeW => B.Bm426BlastFoe == 0 ? double.NaN : (double)B.Bm426WeightFoe / B.Bm426BlastFoe;
        public string DetLine
        {
            get
            {
                long n = Det.Values.Sum() + B.Bm427DetBurn + B.Bm427DetPoison;
                var parts = Det.Select(kv => (kv.Key, kv.Value)).Append(("燃焼の刻み", B.Bm427DetBurn)).Append(("毒の刻み", B.Bm427DetPoison))
                    .Where(x => x.Item2 > 0).OrderByDescending(x => x.Item2).Select(x => $"{x.Item1} {Pc(x.Item2, n)}");
                return n == 0 ? "—" : string.Join("・", parts) + $"（{n}）";
            }
        }
    }

    static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("chiri", out var t)) a.B.Add(t);
        var allyIds = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, tt) in r.TallyByUnit)
        {
            bool ally = allyIds.Contains(id);
            string name = ally ? Short(p.First(u => u.Def.Id == id).Def) : "敵";
            if (ally && tt.Bm427Fuse > 0) a.Fuse[name] = a.Fuse.GetValueOrDefault(name) + tt.Bm427Fuse;
            if (ally && tt.Bm427Det > 0) a.Det[name] = a.Det.GetValueOrDefault(name) + tt.Bm427Det;
            if (tt.Bm426Held > 0) { a.HeldN[name] = a.HeldN.GetValueOrDefault(name) + tt.Bm426Held; a.HeldW[name] = a.HeldW.GetValueOrDefault(name) + tt.Bm426HeldWeight; }
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

    static void BoardsRun()
    {
        Console.WriteLine($"# 第427期 §3-2 ／ §3-3 —— 台 4 つ（後1）＋ 参考 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", Four.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        var res = new Dictionary<(int, string, string), Fx>();
        for (int c = 0; c < Cells.Length; c++)
            foreach (var v in Vers)
                foreach (var w in Four)
                    res[(c, v.Key, w.Name)] = Measure(Put(Board(Cells[c].Board), Cells[c].Slot, v.D), w);
        Fx Sum(int c, string v) { var s = new Fx(); foreach (var w in Four) s.Add(res[(c, v, w.Name)]); return s; }

        for (int c = 0; c < Cells.Length; c++)
        {
            Console.WriteLine($"## {CellName(Cells[c])}（{S307.Seats(Board(Cells[c].Board))}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 勝率・倒しT | 素体との差 | BM-e との差 | 倒れた ／戦 | 倒れた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|---|");
            foreach (var w in Four)
                foreach (var v in Vers)
                {
                    var a = res[(c, v.Key, w.Name)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {(v.Key == "0" ? "—" : Delta(a.Win - res[(c, "0", w.Name)].Win))} | {(v.Key is "e" or "0" ? "—" : Delta(a.Win - res[(c, "e", w.Name)].Win))} | {Per(a.Fell, a.N)} | {a.Fall} |");
                }
            Console.WriteLine();
            Console.WriteLine("爆弾（／戦）: 仕掛けた（敵 ／ 味方）・敵の爆弾が爆ぜた ／ 誘爆 ／ 不発・重さ（敵の爆弾の平均 ／ 全体の最大）・爆ぜた T（平均）・爆発で倒れた敵・BM-em の味方の爆弾（爆ぜた ／ 向かいに入った量）・起爆の主。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 仕掛けた | 爆ぜた ／ 誘爆 ／ 不発 | 重さ（平均 ／ 最大）| 爆ぜた T | 倒した敵 | 味方の爆弾 | 起爆の主 |");
            Console.WriteLine("|---|---|---|---|---|--:|--:|---|---|");
            foreach (var w in Four)
                foreach (var v in Vers.Where(v => v.Key != "0"))
                {
                    var a = res[(c, v.Key, w.Name)]; var b = a.B;
                    string mir = v.Key == "em" ? $"{Per(b.Bm427MirrorBlasts, a.N)} ／ {Per1(b.Bm427MirrorDealt, a.N)}" : "—";
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per(b.Bm426PlantFoe, a.N)} ／ {Per(b.Bm426PlantAlly, a.N)} | {Per(b.Bm426BlastFoe, a.N)} ／ {Per(b.Bm426Chain, a.N)} ／ {Per(b.Bm426Duds, a.N)} | {F1(a.MeanFoeW)} ／ {b.Bm426WeightMax} | {Per1(b.Bm427BlastTurnSum, b.Bm427BlastTurnN)} | {Per(b.Bm426FoeKilled, a.N)} | {mir} | {a.DetLine} |");
                }
            Console.WriteLine();
            Console.WriteLine("4 波の合計: 導火線を縮めた一撃の主（回 ／戦）・持ち主ごと（爆ぜた ／戦・平均の重さ）。");
            Console.WriteLine();
            foreach (var v in Vers.Where(v => v.Key != "0"))
            {
                var a = Sum(c, v.Key); var b = a.B;
                string fuse = string.Join("・", a.Fuse.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per1(kv.Value, a.N)}")
                    .Concat(new[] { b.Bm427FuseBurn > 0 ? $"燃焼の刻み {Per1(b.Bm427FuseBurn, a.N)}" : null, b.Bm427FusePoison > 0 ? $"毒の刻み {Per1(b.Bm427FusePoison, a.N)}" : null }.OfType<string>()));
                string held = string.Join("、", a.HeldN.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, a.N)}・{Per1(a.HeldW[kv.Key], kv.Value)}"));
                Console.WriteLine($"- {v.Label}: 縮めた主 {(fuse == "" ? "—" : fuse)} ／ 持ち主 {(held == "" ? "—" : held)} ／ 起爆の主 {a.DetLine}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("## まとめ（4 波の平均勝率・素体との差）");
        Console.WriteLine();
        Console.WriteLine("| 台・席 | BM-e | BM-ex | BM-em | 素体 | 爆ぜた ／戦（BM-e）| 誘爆 ／戦 | 重さの平均（BM-e）| 起爆の主（BM-e・4 波）|");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|---|");
        for (int c = 0; c < Cells.Length; c++)
        {
            double b0 = Four.Average(w => res[(c, "0", w.Name)].Win);
            string Cell(string k) { double x = Four.Average(w => res[(c, k, w.Name)].Win); return $"{F1(x)}（{Delta(x - b0)}）"; }
            var e = Sum(c, "e");
            Console.WriteLine($"| {CellName(Cells[c])} | {Cell("e")} | {Cell("ex")} | {Cell("em")} | {F1(b0)} | {Per(e.B.Bm426BlastFoe, e.N)} | {Per(e.B.Bm426Chain, e.N)} | {F1(e.MeanFoeW)} | {e.DetLine} |");
        }
        Console.WriteLine();
        Console.WriteLine("## ボス（§3-3）: 仕掛けた ／ 爆ぜた ／戦・重さ（平均 ／ 最大）・爆ぜた T");
        Console.WriteLine();
        Console.WriteLine("| 台・席 | 版 | 勝率 | 仕掛けた | 爆ぜた | 重さ（平均 ／ 最大）| 爆ぜた T |");
        Console.WriteLine("|---|---|--:|--:|--:|---|--:|");
        for (int c = 0; c < Cells.Length; c++)
            foreach (var v in Vers.Where(v => v.Key != "0"))
            {
                var a = res[(c, v.Key, I.Boss.Name)]; var b = a.B;
                Console.WriteLine($"| {CellName(Cells[c])} | {v.Label} | {F1(a.Win)} | {Per(b.Bm426PlantFoe, a.N)} | {Per(b.Bm426BlastFoe, a.N)} | {F1(a.MeanFoeW)} ／ {b.Bm426WeightMax} | {Per1(b.Bm427BlastTurnSum, b.Bm427BlastTurnN)} |");
            }
        Console.WriteLine();
    }

    static void MixedRun()
    {
        var waves = L.Mains.Concat(new[] { I.Guard, I.Bat }).ToArray();
        const int n = 200;
        Console.WriteLine($"# 第427期 §3-4 —— 寄せ集め 3 行（第426期と同じ行と席）× {{元の行, 素体, BM-e}} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..{n - 1}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 替えた席 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " | 元の行との差（最小）| 爆ぜた ／戦 | 誘爆 ／戦 | 起爆の主 |");
        Console.WriteLine("|---|---|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|--:|--:|---|");
        foreach (var (row, slot) in Mixed3)
        {
            var f0 = CompareBuilds().First(r => r.Name == row).F;
            var basis = waves.Select(w => Measure(f0, w, n)).ToArray();
            foreach (var (lab, f) in new[] { ("元の行", f0), ("素体", Put(f0, slot, UnitCatalog.ChiriBM0)), ("BM-e", Put(f0, slot, UnitCatalog.ChiriBMe)) })
            {
                var xs = lab == "元の行" ? basis : waves.Select(w => Measure(f, w, n)).ToArray();
                var all = new Fx(); foreach (var x in xs) all.Add(x);
                double worst = lab == "元の行" ? double.NaN : Enumerable.Range(0, waves.Length).Min(i => xs[i].Win - basis[i].Win);
                bool bm = lab == "BM-e";
                Console.WriteLine($"| {row} | {FormationRules.SeatNames[slot]}（{Short(f0[slot]!)}）| {lab} | " + string.Join(" | ", xs.Select(x => F1(x.Win))) + $" | {Delta(worst)} | {(bm ? Per(all.B.Bm426BlastFoe, all.N) : "—")} | {(bm ? Per(all.B.Bm426Chain, all.N) : "—")} | {(bm ? all.DetLine : "—")} |");
            }
        }
        Console.WriteLine();
    }

    static void Log(string vk, string boardHead, string seat, string waveKey, int seed)
    {
        var v = Vers.First(x => x.Key == vk);
        string bn = Cells.Select(c => c.Board).First(b => b.StartsWith(boardHead, StringComparison.Ordinal));
        int slot = Array.IndexOf(FormationRules.SeatNames, seat);
        var w = WaveKeys.First(x => x.Key == waveKey).W;
        var (r, _, _) = I.Raw(Put(Board(bn), slot, v.D), w, seed, verbose: true);
        foreach (var l in r.Log) Console.WriteLine(l.Text);
        Console.WriteLine($"→ {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
    }

    // ---- 自己検査 ----
    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第427期 chiri427 check");
        Console.WriteLine();
        var vs = new[] { UnitCatalog.ChiriBMe, UnitCatalog.ChiriBMex, UnitCatalog.ChiriBMem, UnitCatalog.ChiriBM0 };
        Req(vs.All(d => d.Id == "chiri" && d.MaxHp == 60 && d.Attack == 3 && d.Speed == 6 && d.Flavor == "" && d.Name.StartsWith("導火線のチリ")), "版と素体は Id \"chiri\" ／ 導火線のチリ ／ HP 60 ／ 攻 3 ／ 速 6 ／ フレーバーは空");
        Req(vs.All(d => !UnitCatalog.All.Contains(d) && !UnitCatalog.Retired.Contains(d)) && UnitCatalog.Retired.Contains(UnitCatalog.Uke), "版は `All` ／ `Retired` の外・旧のウケは `Retired` のまま");
        Req(UnitCatalog.ChiriBMe.Traits.SequenceEqual(new[] { TraitId.BombPlantFoe }) && UnitCatalog.ChiriBMex.Traits.SequenceEqual(new[] { TraitId.BombPlantFoe, TraitId.BombNoTick })
            && UnitCatalog.ChiriBMem.Traits.SequenceEqual(new[] { TraitId.BombPlantFoe, TraitId.BombMirror }) && UnitCatalog.ChiriBM0.Traits.Count == 0, "札: BM-e ＝ 敵だけ ／ BM-ex ＋ 刻みなし ／ BM-em ＋ 向かい ／ 素体 ＝ 札なし");
        Req(UnitCatalog.ChiriBMe.Actions is { Count: 1 } a1 && a1[0].Kind == ActionKind.Skill && UnitCatalog.ChiriBM0.Actions is null or { Count: 0 }, "版の手番は [Skill「仕掛ける」]・素体はふつうに殴る");
        Req(!CompareBuilds().Concat(CrossBuilds()).Any(r => r.F.Occupied().Any(o => o.Def.Traits.Contains(TraitId.BombPlantFoe) || o.Def.Traits.Contains(TraitId.BombPlant))), "`compare` ／ 交差帯に爆弾の保持者はいない（規定は動かない）");

        var fe = Put(Board("試遊・刻み ノミ"), 3, UnitCatalog.ChiriBMe);
        bool same = true, ally0 = true, bossPlant = false, rang = false, exNoTick = true;
        for (int s = 0; s < 6; s++)
        {
            var (r1, _, _) = I.Raw(fe, I.Bat, s, verbose: false, census: false);
            var (r2, _, _) = I.Raw(fe, I.Bat, s, verbose: true, census: false);
            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
            var t = r1.TallyByUnit["chiri"];
            if (t.Bm426PlantAlly > 0 || t.Bm426BlastAlly > 0) ally0 = false;
            if (t.Bm426BlastFoe > 0) rang = true;
            var (rb, _, _) = I.Raw(fe, I.Boss, s, census: false);
            if (rb.TallyByUnit.TryGetValue("chiri", out var tb) && tb.Bm426PlantFoe > 0) bossPlant = true;
            var (rx, _, _) = I.Raw(Put(Board("試遊・燃焼 ポンの台"), 3, UnitCatalog.ChiriBMex), I.Guard, s, census: false);
            if (rx.TallyByUnit.TryGetValue("chiri", out var tx) && tx.Bm427FuseBurn + tx.Bm427FusePoison > 0) exNoTick = false;
        }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（刻み ノミ・後1 BM-e × 大隊 × 6 seed）");
        Req(ally0, "BM-e は味方に 1 つも置かない");
        Req(rang, "敵の爆弾が爆ぜる（同じ 6 seed）");
        Req(bossPlant, "ボスにも置ける（前列が無ければ最も前の列）");
        Req(exNoTick, "BM-ex は刻みで導火線が縮まない（燃焼の台 × 近衛 × 6 seed）");
        // 敵どうしの一撃では縮まない: 直の一撃の主はすべて味方の駒
        var (rv, pv, _) = I.Raw(fe, I.Bat, 0, census: false);
        var ids = pv.Select(u => u.Def.Id).ToHashSet();
        Req(rv.TallyByUnit.Where(kv => kv.Value.Bm427Fuse > 0).All(kv => ids.Contains(kv.Key)), "導火線を縮めた直の一撃の主はすべてこちらの駒");
        var row = Presets.Versions.FirstOrDefault(r => r.Name == PlayRow);
        Req(row.Name is not null && row.F.Occupied().Any(o => o.Def.Traits.Contains(TraitId.BombPlantFoe)) && !Presets.Versions.Any(r => r.Name == "版・爆弾 ウケ"), $"試遊の台「{PlayRow}」がある・「版・爆弾 ウケ」は外した");
        Req(!VersionsH407().Any(r => r.Name == PlayRow), "鎖の終端（`VersionsH407`）は「版・導火線 チリ」を外す");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
