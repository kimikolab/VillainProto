using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// bomb426 —— 第426期【軽い期・版だけ】ウケの枠の新駒「爆弾」（BM-h ／ BM-f ／ BM-hc）。
// 指示書は design/PHASE426_BOMB_SPEC.md ／ 報告は design/PHASE426_BOMB.md。
//
//     dotnet run --project BattleSim -c Release 0 bomb426 boards   # §3-3: 台 4 つ × 席 {中央, 後1} × {BM-h, BM-f, BM-hc, 素体} × 4 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 bomb426 rows     # §3-4 の候補（compare の行のうち、ツギ ／ ササ ／ ゴルム ／ ガルドを含む行）
//     dotnet run --project BattleSim -c Release 0 bomb426 mixed    # §3-4: 寄せ集め 3 行 × {元の行, 素体, BM-h} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 bomb426 log <h|f|hc|0> <台の頭> <中央|後1> <波の鍵 boss|guard|bat|s4|m1..m5> [seed]
//     dotnet run --project BattleSim -c Release 0 bomb426 check    # 自己検査
// =====================================================================================
static class Bomb426Diag
{
    const string PredictFile = ".tmp/p426/predict.md";
    const int Seeds = 100;
    internal const string PlayRow = "版・爆弾 ウケ";
    static readonly I.W[] Four = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    static readonly string[] Boards = { "試遊・自傷", "試遊・燃焼 ポンの台", "試遊・手番 芯4枚", "試遊・刻み ノミ" };
    static readonly (int Slot, string Name)[] SeatsTried = { (2, "中央"), (3, "後1") };
    internal static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("h", "BM-h", UnitCatalog.UkeBMh), ("f", "BM-f", UnitCatalog.UkeBMf), ("hc", "BM-hc", UnitCatalog.UkeBMhc), ("0", "素体", UnitCatalog.UkeBM0),
    };
    /// <summary>§3-4 の寄せ集め（`rows` の候補から選んだ 3 行と、爆弾に替える席）。選んだ理由は報告 §4-5。</summary>
    static readonly (string Row, int Slot)[] Mixed3 =
    {
        ("継ぎ当て×被弾強化", 4), ("継ぎ当て×分散回復", 2), ("耐久 (ガルド×リリ)", 2),
    };
    static readonly (string Key, I.W W)[] WaveKeys = new[] { ("boss", I.Boss), ("guard", I.Guard), ("bat", I.Bat), ("s4", I.StrongWaves[2]) }
        .Concat(Enumerable.Range(1, 5).Select(i => ($"m{i}", L.Mains[i - 1]))).ToArray();
    static readonly string[] Receivers = { "sekki", "tsugi", "sasa", "golm", "gald", "kado" };

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");
    static Formation Board(string n) => Presets.Versions.First(r => r.Name == n).F;
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"bomb426: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "boards": BoardsRun(); break;
            case "rows": Rows(); break;
            case "mixed": MixedRun(); break;
            case "log": Log(A(3, "h"), A(4, "試遊・自傷"), A(5, "中央"), A(6, "boss"), int.Parse(A(7, "0"))); break;
            case "check": Check(); break;
            default: Console.WriteLine("bomb426: モードは boards / rows / mixed / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    sealed class Fx
    {
        public long N, Wins, WinT, Fell, SelfKillBattles;
        public UnitTally B = new();   // 爆弾の駒の帳簿（仕掛けた側）
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public readonly Dictionary<string, long> HeldN = new(), HeldW = new(), Taken = new();   // 持ち主ごと（味方は駒名・敵は「敵」）・受け手ごと
        public long SekkiWrap, TsugiReflect;
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; SelfKillBattles += o.SelfKillBattles; B.Add(o.B);
            SekkiWrap += o.SekkiWrap; TsugiReflect += o.TsugiReflect;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.HeldN) HeldN[k] = HeldN.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.HeldW) HeldW[k] = HeldW.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Taken) Taken[k] = Taken.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => $"{F1(Win)}・{(Wins == 0 ? "—" : ((double)WinT / Wins).ToString("0.0"))}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
        public long Blasts => B.Bm426BlastAlly + B.Bm426BlastFoe;
        public double MeanW(bool ally) { long n = ally ? B.Bm426BlastAlly : B.Bm426BlastFoe; return n == 0 ? double.NaN : (double)(ally ? B.Bm426WeightAlly : B.Bm426WeightFoe) / n; }
    }

    static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("uke", out var t)) { a.B.Add(t); if (t.Bm426AllyKilled > 0) a.SelfKillBattles = 1; }
        var allyIds = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, tt) in r.TallyByUnit)
        {
            bool ally = allyIds.Contains(id);
            string name = ally ? Short(p.First(u => u.Def.Id == id).Def) : "敵";
            if (tt.Bm426Held > 0) { a.HeldN[name] = a.HeldN.GetValueOrDefault(name) + tt.Bm426Held; a.HeldW[name] = a.HeldW.GetValueOrDefault(name) + tt.Bm426HeldWeight; }
            if (tt.Bm426BlastTaken > 0) a.Taken[name] = a.Taken.GetValueOrDefault(name) + tt.Bm426BlastTaken;
            if (ally && id == "sekki") a.SekkiWrap = tt.R396WrapAmt;
            if (ally && id == "tsugi") a.TsugiReflect = tt.ReflectDealt;
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
        Console.WriteLine($"# 第426期 §3-3 —— 台 4 つ × 席 {{中央, 後1}} × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", Four.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        var res = new Dictionary<(string, int, string, string), Fx>();
        foreach (var bn in Boards)
            foreach (var (slot, _) in SeatsTried)
                foreach (var v in Vers)
                    foreach (var w in Four)
                        res[(bn, slot, v.Key, w.Name)] = Measure(Put(Board(bn), slot, v.D), w);
        Fx Sum(string bn, int slot, string v) { var s = new Fx(); foreach (var w in Four) s.Add(res[(bn, slot, v, w.Name)]); return s; }

        foreach (var bn in Boards)
        {
            Console.WriteLine($"## {bn}（{S307.Seats(Board(bn))}）");
            Console.WriteLine();
            foreach (var (slot, sn) in SeatsTried)
            {
                Console.WriteLine($"### 爆弾の席: {sn}（{Short(Board(bn)[slot]!)} を替えた）");
                Console.WriteLine();
                Console.WriteLine("| 波 | 版 | 勝率・倒しT | 素体との差 | BM-h との差 | 倒れた ／戦 | 倒れた駒 |");
                Console.WriteLine("|---|---|---|--:|--:|--:|---|");
                foreach (var w in Four)
                    foreach (var v in Vers)
                    {
                        var a = res[(bn, slot, v.Key, w.Name)];
                        Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {(v.Key == "0" ? "—" : Delta(a.Win - res[(bn, slot, "0", w.Name)].Win))} | {(v.Key is "h" or "0" ? "—" : Delta(a.Win - res[(bn, slot, "h", w.Name)].Win))} | {Per(a.Fell, a.N)} | {a.Fall} |");
                    }
                Console.WriteLine();
                Console.WriteLine("爆弾（／戦）: 仕掛けた（味方 ／ 敵）・爆ぜた（味方側 ／ 敵側）・誘爆・重さの平均（味方側 ／ 敵側）・最大・爆発で倒れた（味方 ／ 敵）・味方が自分の爆弾で倒れた戦・不発。");
                Console.WriteLine();
                Console.WriteLine("| 波 | 版 | 仕掛けた | 爆ぜた | 誘爆 | 重さ（味方 ／ 敵）| 最大 | 倒れた（味方 ／ 敵）| 自爆の戦 | 不発 |");
                Console.WriteLine("|---|---|---|---|--:|---|--:|---|--:|--:|");
                foreach (var w in Four)
                    foreach (var v in Vers.Where(v => v.Key != "0"))
                    {
                        var a = res[(bn, slot, v.Key, w.Name)]; var b = a.B;
                        Console.WriteLine($"| {w.Name} | {v.Label} | {Per(b.Bm426PlantAlly, a.N)} ／ {Per(b.Bm426PlantFoe, a.N)} | {Per(b.Bm426BlastAlly, a.N)} ／ {Per(b.Bm426BlastFoe, a.N)} | {Per(b.Bm426Chain, a.N)} | {F1(a.MeanW(true))} ／ {F1(a.MeanW(false))} | {b.Bm426WeightMax} | {Per(b.Bm426AllyKilled, a.N)} ／ {Per(b.Bm426FoeKilled, a.N)} | {Pc(a.SelfKillBattles, a.N)} | {Per(b.Bm426Duds, a.N)} |");
                    }
                Console.WriteLine();
                Console.WriteLine("起爆した一撃（4 波の合計に占める割合）・持ち主ごとの重さ（爆ぜた回数 ／戦・平均）・爆発で受けた量（／戦）。");
                Console.WriteLine();
                Console.WriteLine("| 版 | 敵の爆弾（ノミ ／ ホタ ／ 燃焼の刻み ／ 他）| 味方の爆弾（敵の単体 ／ 範囲 ／ 燃焼の刻み ／ 他）| 持ち主ごと | 受けた量 |");
                Console.WriteLine("|---|---|---|---|---|");
                foreach (var v in Vers.Where(v => v.Key != "0"))
                {
                    var a = Sum(bn, slot, v.Key); var b = a.B;
                    long fn = b.Bm426TrigNomi + b.Bm426TrigHota + b.Bm426TrigBurn + b.Bm426TrigOther, an = b.Bm426TrigSingle + b.Bm426TrigArea + b.Bm426TrigAllyBurn + b.Bm426TrigAllyOther;
                    string held = string.Join("、", a.HeldN.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, a.N)}・{Per1(a.HeldW[kv.Key], kv.Value)}"));
                    string taken = string.Join("、", a.Taken.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per1(kv.Value, a.N)}"));
                    Console.WriteLine($"| {v.Label} | {Pc(b.Bm426TrigNomi, fn)} ／ {Pc(b.Bm426TrigHota, fn)} ／ {Pc(b.Bm426TrigBurn, fn)} ／ {Pc(b.Bm426TrigOther, fn)}（{fn}）| {Pc(b.Bm426TrigSingle, an)} ／ {Pc(b.Bm426TrigArea, an)} ／ {Pc(b.Bm426TrigAllyBurn, an)} ／ {Pc(b.Bm426TrigAllyOther, an)}（{an}）| {(held == "" ? "—" : held)} | {(taken == "" ? "—" : taken)} |");
                }
                var s0 = Sum(bn, slot, "0"); var sh = Sum(bn, slot, "h");
                if (Board(bn).Occupied().Any(o => o.Def.Id == "sekki") && Board(bn)[slot]?.Id != "sekki")
                    Console.WriteLine($"\nセッキが包んだ量 ／戦（4 波）: 素体 {Per1(s0.SekkiWrap, s0.N)} → BM-h {Per1(sh.SekkiWrap, sh.N)}");
                Console.WriteLine();
            }
        }

        Console.WriteLine("## まとめ（4 波の平均勝率・素体との差）と線の材料");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席 | BM-h | BM-f | BM-hc | 素体 | 爆ぜた ／戦（BM-h）| 誘爆 ／戦（BM-h）| 重さの平均（BM-h 味方 ／ 敵）| 自爆の戦（BM-h ／ BM-f）|");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|---|---|");
        foreach (var bn in Boards)
            foreach (var (slot, sn) in SeatsTried)
            {
                double b0 = Four.Average(w => res[(bn, slot, "0", w.Name)].Win);
                string Cell(string k) { double x = Four.Average(w => res[(bn, slot, k, w.Name)].Win); return $"{F1(x)}（{Delta(x - b0)}）"; }
                var h = Sum(bn, slot, "h"); var f = Sum(bn, slot, "f");
                Console.WriteLine($"| {bn} | {sn} | {Cell("h")} | {Cell("f")} | {Cell("hc")} | {F1(b0)} | {Per(h.Blasts, h.N)} | {Per(h.B.Bm426Chain, h.N)} | {F1(h.MeanW(true))} ／ {F1(h.MeanW(false))} | {Pc(h.SelfKillBattles, h.N)} ／ {Pc(f.SelfKillBattles, f.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("線 2 の材料（BM-h・味方の爆弾の重さの平均・中央 ÷ 後1）: " + string.Join("・", Boards.Select(bn =>
        {
            double c = Sum(bn, 2, "h").MeanW(true), b = Sum(bn, 3, "h").MeanW(true);
            return $"{bn} {F1(c)} ／ {F1(b)}（×{(double.IsNaN(c) || double.IsNaN(b) || b == 0 ? "—" : (c / b).ToString("F2"))}）";
        })));
        Console.WriteLine();
    }

    static void Rows()
    {
        Console.WriteLine("# 第426期 §3-4 の候補（compare の行のうち、ツギ ／ ササ ／ ゴルム ／ ガルドを含む行）");
        Console.WriteLine();
        foreach (var (n, f) in CompareBuilds())
            if (f.Occupied().Any(o => o.Def.Id is "tsugi" or "sasa" or "golm" or "gald"))
                Console.WriteLine($"- {n}（{S307.Seats(f)}）");
    }

    static void MixedRun()
    {
        var waves = L.Mains.Concat(new[] { I.Guard, I.Bat }).ToArray();
        const int n = 200;
        Console.WriteLine($"# 第426期 §3-4 —— 寄せ集め 3 行 × {{元の行, 素体, BM-h}} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..{n - 1}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 替えた席 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " | 元の行との差（最小）| 爆ぜた ／戦 | 誘爆 ／戦 | 自爆の戦 |");
        Console.WriteLine("|---|---|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|--:|--:|--:|");
        foreach (var (row, slot) in Mixed3)
        {
            var f0 = CompareBuilds().First(r => r.Name == row).F;
            var basis = waves.Select(w => Measure(f0, w, n)).ToArray();
            foreach (var (lab, f) in new[] { ("元の行", f0), ("素体", Put(f0, slot, UnitCatalog.UkeBM0)), ("BM-h", Put(f0, slot, UnitCatalog.UkeBMh)) })
            {
                var xs = lab == "元の行" ? basis : waves.Select(w => Measure(f, w, n)).ToArray();
                var all = new Fx(); foreach (var x in xs) all.Add(x);
                double worst = lab == "元の行" ? double.NaN : Enumerable.Range(0, waves.Length).Min(i => xs[i].Win - basis[i].Win);
                Console.WriteLine($"| {row} | {FormationRules.SeatNames[slot]}（{Short(f0[slot]!)}）| {lab} | " + string.Join(" | ", xs.Select(x => F1(x.Win))) + $" | {Delta(worst)} | {(lab == "BM-h" ? Per(all.Blasts, all.N) : "—")} | {(lab == "BM-h" ? Per(all.B.Bm426Chain, all.N) : "—")} | {(lab == "BM-h" ? Pc(all.SelfKillBattles, all.N) : "—")} |");
                if (lab == "BM-h")
                {
                    string taken = string.Join("、", all.Taken.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per1(kv.Value, all.N)}"));
                    Console.WriteLine($"|  |  | （受けた量 ／戦・ツギの撃ち返し {Per1(all.TsugiReflect, all.N)}）| {taken} |" + string.Concat(Enumerable.Repeat(" |", waves.Length + 3)));
                }
            }
        }
        Console.WriteLine();
    }

    static void Log(string vk, string boardHead, string seat, string waveKey, int seed)
    {
        var v = Vers.First(x => x.Key == vk);
        string bn = Boards.First(b => b.StartsWith(boardHead, StringComparison.Ordinal));
        int slot = SeatsTried.First(s => s.Name == seat).Slot;
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
        Console.WriteLine("# 第426期 bomb426 check");
        Console.WriteLine();
        var vs = new[] { UnitCatalog.UkeBMh, UnitCatalog.UkeBMf, UnitCatalog.UkeBMhc, UnitCatalog.UkeBM0 };
        Req(vs.All(d => d.Id == "uke" && d.MaxHp == 60 && d.Attack == 3 && d.Speed == 6), "版と素体は Id \"uke\" ／ HP 60 ／ 攻 3 ／ 速 6");
        Req(vs.All(d => !UnitCatalog.All.Contains(d) && !UnitCatalog.Retired.Contains(d)) && UnitCatalog.Retired.Contains(UnitCatalog.Uke), "版は `All` ／ `Retired` の外・旧のウケは `Retired` に残る");
        Req(UnitCatalog.UkeBMh.Traits.SequenceEqual(new[] { TraitId.BombPlant }) && UnitCatalog.UkeBMf.Traits.SequenceEqual(new[] { TraitId.BombPlant, TraitId.BombFull })
            && UnitCatalog.UkeBMhc.Traits.SequenceEqual(new[] { TraitId.BombPlant, TraitId.BombNoChain }) && UnitCatalog.UkeBM0.Traits.Count == 0, "札: BM-h ＝ 仕掛ける ／ BM-f ＋ 全量 ／ BM-hc ＋ 誘爆なし ／ 素体 ＝ 札なし");
        Req(UnitCatalog.UkeBMh.Actions is { Count: 1 } a1 && a1[0].Kind == ActionKind.Skill && UnitCatalog.UkeBM0.Actions is null or { Count: 0 }, "版の手番は [Skill「仕掛ける」]・素体はふつうに殴る");
        Req(!CompareBuilds().Concat(CrossBuilds()).Any(r => r.F.Occupied().Any(o => o.Def.Traits.Contains(TraitId.BombPlant))), "`compare` ／ 交差帯に爆弾の保持者はいない（規定は動かない）");

        // 爆弾が鳴る ／ verbose の有無で勝敗 ／ T が変わらない ／ 素体の戦には爆弾が 1 つも無い
        var fh = Put(Board("試遊・自傷"), 2, UnitCatalog.UkeBMh);
        bool same = true, rang = false, chainOk = true;
        for (int s = 0; s < 6; s++)
        {
            var (r1, _, _) = I.Raw(fh, I.Guard, s, verbose: false, census: false);
            var (r2, _, _) = I.Raw(fh, I.Guard, s, verbose: true, census: false);
            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
            if (r1.TallyByUnit.TryGetValue("uke", out var t) && t.Bm426BlastAlly + t.Bm426BlastFoe > 0) rang = true;
            var (r3, _, _) = I.Raw(Put(Board("試遊・自傷"), 2, UnitCatalog.UkeBMhc), I.Guard, s, census: false);
            if (r3.TallyByUnit.TryGetValue("uke", out var t3) && t3.Bm426Chain > 0) chainOk = false;
        }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（自傷の台・中央 BM-h × 近衛 × 6 seed）");
        Req(rang, "爆弾が爆ぜる（同じ 6 seed のどこかで 1 回以上）");
        Req(chainOk, "BM-hc は誘爆しない（同じ 6 seed）");
        bool plain = true;
        for (int s = 0; s < 6; s++)
        {
            var (r0, _, _) = I.Raw(Put(Board("試遊・自傷"), 2, UnitCatalog.UkeBM0), I.Guard, s, census: false);
            if (r0.TallyByUnit.Values.Any(t => t.Bm426PlantAlly + t.Bm426PlantFoe + t.Bm426Held > 0)) plain = false;
        }
        Req(plain, "素体の戦には爆弾が 1 つも無い");
        // 1 戦の台本: 爆発は Highlight の出来事だけ（新しい出来事の種類を足していない）
        var (rv, pv, _) = I.Raw(fh, I.Guard, 0, verbose: true, census: false);
        int uid = pv.First(u => u.Def.Id == "uke").InstanceId;
        Req(rv.Events.Any(e => e.Kind == BattleEventKind.Skill && e.ActorId == uid), "台本に「仕掛ける」の Skill が乗る");
        var row = Presets.Versions.FirstOrDefault(r => r.Name == PlayRow);
        Req(row.Name is null, $"試遊の台「{PlayRow}」は第427期に外した（「版・導火線 チリ」に入れ替え）");
        Req(!VersionsH407().Any(r => r.Name == PlayRow), "鎖の終端（`VersionsH407`）は「版・爆弾 ウケ」を外す");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
