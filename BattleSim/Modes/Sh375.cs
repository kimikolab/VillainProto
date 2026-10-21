using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S367 = Sh367Diag;
using S370 = Sh370Diag;

// =====================================================================================
// sh375 —— 第375期【軽い期】段0: スィドの開戦の衣 S10 を規定に ／ 本体: 飢えたヴィオを毒の外で使う（数えるだけ・版 C）。
// 指示書は design/PHASE375_POISON_CLOSE_SPEC.md ／ 報告は design/PHASE375_POISON_CLOSE.md ／ まとめは design/POISON_AXIS_SUMMARY.md。本体の版は規定にしない。
//
//     dotnet run --project BattleSim -c Release 0 sh375 boards     # §4-3: 4 台（試遊・自傷 ／ 自傷 ＋ ヴィオ ／ 台 B ／ 台 B ＋ ヴィオ）× 版（規定のヴィオ ／ C）× 5 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 sh375 summary    # §3 の 5: 毒軸の代表の台 × 4 波（段0 の後の規定・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh375 check      # 自己検査
//
// 1戦の計数は第370期の器具（`Sh370Diag.One` ／ `Agg`）＋ 啜り ／ アカ ／ カドの計数（`UnitTally`）。`compare` は回さない（ヴィオのいる行は毒の台で C が起きない）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p375/predict.md`）。
// =====================================================================================
static partial class Sh375Diag
{
    internal const string PredictFile = ".tmp/p375/predict.md";
    const string Tag = "第375期";
    const int Seeds = 100, LoopT = 6;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "summary" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh375: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "summary": Summary(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh375: モードは boards / summary / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Nm(string id) => Short(UnitCatalog.Everyone.First(d => d.Id == id));

    internal static readonly I.W[] Waves = { I.Boss, I.Guard, I.Bat, I.M2, I.M5 };

    /// <summary>台（名前・並び・版の列）。版の列が1つの台は「替える前の台」。</summary>
    internal static (string Name, Formation[] Vs)[] BoardList() => new (string, Formation[])[]
    {
        ("試遊・自傷（替える前）", new[] { Presets.SelfHarmPlay365() }),
        ("自傷 ＋ ヴィオ（ボルグ → ヴィオ）", new[] { Presets.SelfHarmVio375(UnitCatalog.Vio), Presets.SelfHarmVio375(UnitCatalog.VioC) }),
        ("台 B（替える前）", new[] { Presets.HisaBoard366() }),
        ("台 B ＋ ヴィオ（ボルグ → ヴィオ）", new[] { Presets.BoardBVio375(UnitCatalog.Vio), Presets.BoardBVio375(UnitCatalog.VioC) }),
    };
    static readonly string[] VerNames = { "規定のヴィオ", "C 血を啜る" };

    /// <summary>毒の供給役（この期の C の台にいてはいけない駒）。</summary>
    internal static readonly string[] PoisonSuppliers = { "guza", "sid", "beni", "mio", "rau" };

    internal sealed class Ex
    {
        public long N, SipTurns, SipHp, AkaFires, AkaSpent, AkaDry, KadoBareN, KadoBareT, KadoStrikes, KadoStrikeSum, KadoRiposte, Turns;
        public readonly Dictionary<string, long> SippedFrom = new();
        public void Add(Ex o)
        {
            N += o.N; SipTurns += o.SipTurns; SipHp += o.SipHp; AkaFires += o.AkaFires; AkaSpent += o.AkaSpent; AkaDry += o.AkaDry;
            KadoBareN += o.KadoBareN; KadoBareT += o.KadoBareT; KadoStrikes += o.KadoStrikes; KadoStrikeSum += o.KadoStrikeSum; KadoRiposte += o.KadoRiposte; Turns += o.Turns;
            foreach (var (k, v) in o.SippedFrom) SippedFrom[k] = SippedFrom.GetValueOrDefault(k) + v;
        }
    }

    internal static Ex One(Formation f, I.W w, int seed)
    {
        var x = new Ex { N = 1 };
        var (r, p, _) = S367.Raw(f, w, seed);
        x.Turns = r.Turns;
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!p.Any(u => u.Def.Id == id)) continue;
            if (id == "vio") { x.SipTurns = t.Z374SipTurns; x.SipHp = t.Z374SipHp; }
            if (id == "susu") { x.AkaFires = t.AshFires; x.AkaSpent = t.AshSpent; x.AkaDry = t.AshDry; }
            if (id == "kado") { if (t.Sh362BareTurn > 0) { x.KadoBareN = 1; x.KadoBareT = t.Sh362BareTurn; } x.KadoStrikes = t.Sh362Strikes; x.KadoStrikeSum = t.Sh362StrikeSum; x.KadoRiposte = t.Sh362RiposteSum; }
            if (t.Z374SippedFrom > 0) x.SippedFrom[id] = t.Z374SippedFrom;
        }
        return x;
    }

    static (S370.Agg A, Ex X) Measure(Formation f, I.W w)
    {
        var pa = new S370.Agg[Seeds]; var px = new Ex[Seeds];
        Parallel.For(0, Seeds, s => { pa[s] = S370.One(f, w, s); px[s] = One(f, w, s); });
        var a = new S370.Agg(); var x = new Ex();
        foreach (var q in pa) a.Add(q);
        foreach (var q in px) x.Add(q);
        return (a, x);
    }

    // ---------------------------------------------------------------------------------
    // §4-3
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        Console.WriteLine($"# {Tag} §4-3 飢えたヴィオを毒の外で使う —— 4 台 × 版 × {Waves.Length} 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.Vs[0])}）")) + "。駒は段0 の後の規定（スィドは S10・ここには出ない）。版はヴィオだけ（規定 ／ C）。");
        Console.WriteLine();
        var keys = new List<(int B, int V, int W)>();
        for (int bi = 0; bi < boards.Length; bi++) for (int vi = 0; vi < boards[bi].Vs.Length; vi++) for (int wi = 0; wi < Waves.Length; wi++) keys.Add((bi, vi, wi));
        var res = new (S370.Agg A, Ex X)[keys.Count];
        Parallel.For(0, keys.Count, k => res[k] = Measure(boards[keys[k].B].Vs[keys[k].V], Waves[keys[k].W]));
        var grid = keys.Select((k, i) => (k, res[i])).ToDictionary(t => t.k, t => t.Item2);
        string Ver(int bi, int vi) => boards[bi].Vs.Length == 1 ? "—" : VerNames[vi];

        Console.WriteLine("## 1. 台 × 版 × 波（勝率・倒しT（勝った戦）・倒れた味方（1戦あたり））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < boards[bi].Vs.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Ver(bi, vi)} | {string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(wi => grid[(bi, vi, wi)].A.Cell))} |");
        Console.WriteLine();

        Console.WriteLine("## 1'. 誰が何Tに倒れたか（駒 倒れた戦の割合%（倒れたターンの平均））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < boards[bi].Vs.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Ver(bi, vi)} | {string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(wi => grid[(bi, vi, wi)].A.FellCell))} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 飢えたターン・啜った HP（隣の誰から）・ヴィオの攻撃力（ターンの頭・T1〜T6・生きていた戦の平均）・連撃に上がったターン");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 決着T | 飢えたターン | 啜った HP | 啜られた（誰から） | ヴィオの攻撃力（頭） | 2連撃 ／ 3連撃 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|---|---|");
        foreach (int bi in new[] { 1, 3 })
            for (int wi = 0; wi < Waves.Length; wi++)
                for (int vi = 0; vi < 2; vi++)
                {
                    var (a, x) = grid[(bi, vi, wi)];
                    string st = $"{(a.Stage2N == 0 ? "—" : $"T{F1((double)a.Stage2T / a.Stage2N)}（{Pct(a.Stage2N, a.N)}%）")} ／ {(a.Stage3N == 0 ? "—" : $"T{F1((double)a.Stage3T / a.Stage3N)}（{Pct(a.Stage3N, a.N)}%）")}";
                    string from = x.SippedFrom.Count == 0 ? "—" : string.Join("・", x.SippedFrom.OrderByDescending(kv => kv.Value).Select(kv => $"{Nm(kv.Key)} {Per(kv.Value, x.N)}"));
                    string atk = string.Join(" ", Enumerable.Range(1, LoopT).Select(t => a.VAlive[t] == 0 ? "—" : Per(a.VAtk[t], a.VAlive[t])));
                    Console.WriteLine($"| {(vi == 0 && wi == 0 ? boards[bi].Name : "")} | {(vi == 0 ? Waves[wi].Name : "")} | {VerNames[vi]} | {Per(x.Turns, x.N)} | {Per(x.SipTurns, x.N)} | {Per(x.SipHp, x.N)} | {from} | {atk} | {st} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 3. 傷を読む駒の量（1戦）: アカの撃った血 ／ カドが棘を剥いた戦とターン ／ カドの殴りかかり ／ 反撃");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | アカ 撃った回数 | アカ 撃った血 | アカ 1回あたり | カド 棘を剥いた戦（T） | カド 殴りかかり（回 ／ 量） | カド 反撃の量 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|---|--:|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int wi = 0; wi < Waves.Length; wi++)
                for (int vi = 0; vi < boards[bi].Vs.Length; vi++)
                {
                    var (a, x) = grid[(bi, vi, wi)];
                    string bare = x.KadoBareN == 0 ? "0%" : $"{Pct(x.KadoBareN, x.N)}%（T{F1((double)x.KadoBareT / x.KadoBareN)}）";
                    Console.WriteLine($"| {(vi == 0 && wi == 0 ? boards[bi].Name : "")} | {(vi == 0 ? Waves[wi].Name : "")} | {Ver(bi, vi)} | {Per(x.AkaFires, x.N)} | {Per(x.AkaSpent, x.N)} | {(x.AkaFires == 0 ? "—" : F1((double)x.AkaSpent / x.AkaFires))} | {bare} | {Per(x.KadoStrikes, x.N)} ／ {Per(x.KadoStrikeSum, x.N)} | {Per(x.KadoRiposte, x.N)} |");
                }
        Console.WriteLine();

        var bad = new List<string>();
        foreach (int bi in new[] { 1, 3 })
            for (int wi = 0; wi < Waves.Length; wi++)
            {
                var b = grid[(bi, 0, wi)].A; var a = grid[(bi, 1, wi)].A; var w = Waves[wi]; string nm = $"{boards[bi].Name} × C × {w.Name}";
                if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{nm}: 1T 決着 {Pct(a.Win1, a.N)}%");
                if (w.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4)) bad.Add($"{nm}: 倒しT {a.WinTs}（規定のヴィオ {b.WinTs}）");
                if (100.0 * (a.Draw - b.Draw) / a.N >= 5) bad.Add($"{nm}: 30T の打ち切り {Pct(b.Draw, b.N)} → {Pct(a.Draw, a.N)}%");
            }
        Console.WriteLine("## 4. 壊れの兆し（C と規定のヴィオの比べ・精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満（規定のヴィオで既に 4T 未満は除く）／ 30T の打ち切りが 5pt 以上増える）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3 の 5: 毒軸の代表の台と今の数字
    // ---------------------------------------------------------------------------------
    internal static readonly I.W[] SummaryWaves = { I.Boss, Sh366Diag.BossNoTax, I.Guard, I.Bat };

    static void Summary()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = new (string Name, Formation F)[]
        {
            ("試遊・毒", Presets.Versions.First(r => r.Name == "試遊・毒").F),
            ("試遊・毒 席替え", Presets.Versions.First(r => r.Name == "試遊・毒 席替え").F),
            ("試遊・毒 紅", Presets.Versions.First(r => r.Name == "試遊・毒 紅").F),
            ("毒特化・集団", CompareBuilds().First(r => r.Name == S367.BombRow).F),
        };
        Console.WriteLine($"# {Tag} §3 の 5 毒軸の代表の台と今の数字（段0 の後の規定・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        var res = new S370.Agg[boards.Length, SummaryWaves.Length];
        Parallel.For(0, boards.Length * SummaryWaves.Length, k => res[k / SummaryWaves.Length, k % SummaryWaves.Length] = S370.Measure(boards[k / SummaryWaves.Length].F, SummaryWaves[k % SummaryWaves.Length], Seeds));
        Console.WriteLine("| 台 | 並び | " + string.Join(" | ", SummaryWaves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(SummaryWaves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {S307.Seats(boards[bi].F)} | {string.Join(" | ", Enumerable.Range(0, SummaryWaves.Length).Select(wi => $"{F1(res[bi, wi].Win)}・{res[bi, wi].WinTs}T"))} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
