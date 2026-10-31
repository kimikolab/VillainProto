using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// nomi423 —— 第423期【軽い期・版だけ】刻みのノミ②: 連打は残し、1 発ごとに傷を刻む（NM-w ／ NM-w3）。
// 指示書は design/PHASE423_NOMI_WOUND_SPEC.md ／ 報告は design/PHASE423_NOMI_WOUND.md。
//
//     dotnet run --project BattleSim -c Release 0 nomi423 p0       # Phase 0: 傷の上限 ／ 書き手ごとの傷の最大（5 行 × 4 波）・ボスの傷とノミの与ダメの推移（ノミ単騎・T1〜T6）
// =====================================================================================
static partial class Nomi423Diag
{
    const string PredictFile = ".tmp/p423/predict.md";
    const int Seeds = 100;
    static readonly I.W[] Four = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    static readonly string[] MainRows = { "刻み (ノミ単騎)", "逸らし改 (ソラ×ノミ)", "燃焼×刻み (ボルグ×ホタ×ノミ)", "傷×被弾 (カド×ノミ×リリ)", "刻み×澱み (ノミ×ミオ)" };

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
            Console.WriteLine($"nomi423: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        BattleContext.WoundTrace423 = true;   // 計数のみ（盤面は動かない・check で確かめる）
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (!RunMore(mode, A))
        {
            switch (mode)
            {
                case "p0": Phase0(); break;
                default: Console.WriteLine("nomi423: モードは p0 / boards / mixed / seats / log / check。"); return;
            }
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
    static partial void More(string mode, Func<int, string, string> a, ref bool handled);
    static bool RunMore(string mode, Func<int, string, string> a) { bool h = false; More(mode, a, ref h); return h; }

    // ---------------------------------------------------------------------------------
    // Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        Console.WriteLine($"# 第423期 Phase 0 —— 傷（`Wound`）の上限と、書き手ごとの積み・seed 0..{Seeds - 1}（いまの規定）");
        Console.WriteLine();
        Console.WriteLine("- 傷の上限: engine の `Wound` は、深手の束ね（`DeepRule`・傷 3 で束ねて深手）が**既定で無効**（`DeepRule.Default = new(false)`）なので、上限は無い。");
        Console.WriteLine("- 敵の傷の最大 ＝ その戦で敵のどれかに書かれた傷の最大（`Wd423Peak`・書かれた側）。書いた量 ＝ 味方の書き手ごとの `WoundWrites` ／戦。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 勝率 | 敵の傷の最大（平均 ／ 最大）| 書いた量 ／戦（書き手ごと）| ノミの傷の読み（回 ／ 量 ／戦）|");
        Console.WriteLine("|---|---|--:|---|---|---|");
        foreach (var name in MainRows)
            foreach (var w in Four)
            {
                var f = Row(name);
                var parts = new (int Win, long Peak, Dictionary<string, long> Writes, long Reads, long ReadAmt)[Seeds];
                Parallel.For(0, Seeds, s =>
                {
                    var (r, p, e) = I.Raw(f, w, s, verbose: false);
                    long peak = e.DistinctBy(u => u.Def.Id).Max(u => r.TallyByUnit.TryGetValue(u.Def.Id, out var t) ? t.Wd423Peak : 0);
                    var wr = new Dictionary<string, long>();
                    foreach (var u in p.DistinctBy(u => u.Def.Id)) if (r.TallyByUnit.TryGetValue(u.Def.Id, out var t) && t.WoundWrites > 0) wr[Short(u.Def)] = t.WoundWrites;
                    r.TallyByUnit.TryGetValue("nomi", out var nt);
                    parts[s] = (r.PlayerWon ? 1 : 0, peak, wr, nt?.Wd423Reads ?? 0, nt?.Wd423ReadAmt ?? 0);
                });
                var writes = new Dictionary<string, long>();
                foreach (var x in parts) foreach (var (k, v) in x.Writes) writes[k] = writes.GetValueOrDefault(k) + v;
                string ws = writes.Count == 0 ? "—" : string.Join("、", writes.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per1(kv.Value, Seeds)}"));
                Console.WriteLine($"| {name} | {w.Name} | {F1(100.0 * parts.Sum(x => x.Win) / Seeds)} | {Per1(parts.Sum(x => x.Peak), Seeds)} ／ {parts.Max(x => x.Peak)} | {ws} | {Per(parts.Sum(x => x.Reads), Seeds)} ／ {Per1(parts.Sum(x => x.ReadAmt), Seeds)} |");
            }
        Console.WriteLine();
        BossTrace(UnitCatalog.Nomi, "規定");
    }

    /// <summary>ボス（勇者 1 体）× ノミ単騎: ターンごとのノミの与ダメ（ノミの名で入った分）と勇者の傷（その戦の平均・T1〜T6）。</summary>
    static void BossTrace(UnitDef nomi, string label)
    {
        var f = Swap(Row("刻み (ノミ単騎)"), nomi);
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
        Console.WriteLine($"## ボス × ノミ単騎（{label}）—— ターンごとのノミの与ダメ（台本の Damage・ノミが出どころ）と勇者の傷（その戦が続いていた戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| T | " + string.Join(" | ", Enumerable.Range(1, T)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, T).Select(_ => "--:|")));
        Console.WriteLine("| ノミの与ダメ ／戦 | " + string.Join(" | ", Enumerable.Range(1, T).Select(t => Per1(dmg[t], Seeds))) + " |");
        Console.WriteLine("| 勇者の傷 | " + string.Join(" | ", Enumerable.Range(1, T).Select(t => Per1(wnd[t], alive[t]))) + " |");
        Console.WriteLine("| 続いていた戦 | " + string.Join(" | ", Enumerable.Range(1, T).Select(t => alive[t].ToString())) + " |");
        Console.WriteLine();
    }
}
