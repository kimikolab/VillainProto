using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;
using S367 = Sh367Diag;
using S370 = Sh370Diag;
using S373 = Sh373Diag;

// =====================================================================================
// sh374 —— 第374期【軽い期】毒の立ち上がりを早める: スィドの開戦の衣（S10 ／ S20）／ ヴィオのマイナス「飢えると血を啜る」（C）／ 両方（S10C）。
// 指示書は design/PHASE374_POISON_EARLY_SPEC.md ／ 報告は design/PHASE374_POISON_EARLY.md。**規定は1つも変えない。**
//
//     dotnet run --project BattleSim -c Release 0 sh374 boards                     # §4: 6 台 × 版 × 6 波（seed 0..99・紅の台は強い第4波も）・誰が何Tに倒れたか・スィドの層 ／ 衣 ／ 返し・ヴィオの攻撃力 ／ 段 ／ 血を啜った・傷の台のアカ・紅の台・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh374 compare <S10|S20|C|S10C>  # `compare` 64 行 × 版（スィド ／ ヴィオのいる行だけを版で回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh374 check                      # 自己検査
//
// 1戦の計数は第370期の器具（`Sh370Diag.One` ／ `Agg`）＋ この期の札の計数（`UnitTally.Z374*`）＋ 紅の台は第373期の器具（`Sh373Diag.GurenOne`）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p374/predict.md`）。
// =====================================================================================
static partial class Sh374Diag
{
    internal const string PredictFile = ".tmp/p374/predict.md";
    const string Tag = "第374期";
    const int Seeds = 100, CompareSeeds = 200, LoopT = 6;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh374: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "S10"); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh374: モードは boards / compare <S10|S20|C|S10C> / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Nm(string id) => Short(UnitCatalog.Everyone.First(d => d.Id == id));
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    internal static readonly S370.Ver V0 = new("規定", null, null, null), VS10 = new("S10 開戦の衣 +10", null, null, UnitCatalog.SidS10),
        VS20 = new("S20 開戦の衣 +20", null, null, UnitCatalog.SidS20), VC = new("C 血を啜る", null, UnitCatalog.VioC, null), VS10C = new("S10C 両方", null, UnitCatalog.VioC, UnitCatalog.SidS10);
    internal static readonly S370.Ver[] Vers = { V0, VS10, VS20, VC, VS10C };
    internal static Formation Apply(Formation f, S370.Ver v) => S370.Apply(f, v);

    /// <summary>
    /// 傷の台: 試遊・毒のドルガ → 血詠みのアカ（アカは生きている味方全員の今の傷を詠んで撃つ ＝ 啜った傷がそのまま弾になる）。
    /// ドルガの席（後3）は後1 のヴィオの隣ではない（後1 の隣は前1 と中央）ので、**アカを前1 に置き、前1 のガルドを後3 へ回した**（アカ・スィド・グザ・ヴィオ・ガルド）。
    /// </summary>
    internal static Formation WoundBoard()
    {
        var f = Presets.Versions.First(r => r.Name == "試遊・毒").F.Clone();
        f[0] = UnitCatalog.Susu;
        f[4] = UnitCatalog.Gald;
        return f;
    }

    internal static (string Name, Formation F, S370.Ver[] Vs)[] BoardList() => new (string, Formation, S370.Ver[])[]
    {
        ("試遊・毒", Presets.Versions.First(r => r.Name == "試遊・毒").F, Vers),
        ("試遊・毒 席替え", Presets.Versions.First(r => r.Name == "試遊・毒 席替え").F, Vers),
        ("毒特化・集団", Row(S367.BombRow), Vers),
        ("澱み喰い (グザ×ヴィオ)", Row("澱み喰い (グザ×ヴィオ)"), Vers),
        ("試遊・毒 紅", Presets.Versions.First(r => r.Name == "試遊・毒 紅").F, new[] { V0, VS10, VS20 }),
        ("傷の台（アカ・スィド・グザ・ヴィオ・ガルド）", WoundBoard(), new[] { V0, VC, VS10C }),
    };
    internal static readonly I.W[] Waves = S367.PWaves;
    internal static readonly I.W StrongM4 = S373.GurenWaves[^1];
    static I.W[] WavesOf(int bi, int nb) => bi == 4 ? Waves.Append(StrongM4).ToArray() : Waves;

    // ---------------------------------------------------------------------------------
    // この期の札の計数（1戦）
    // ---------------------------------------------------------------------------------
    internal sealed class Ex
    {
        public long N, Opening, CloakSaved, Retort, SipTurns, SipHp, AkaFires, AkaSpent, AkaDry, FoeMax;
        public readonly Dictionary<string, long> SippedFrom = new();
        public void Add(Ex o)
        {
            N += o.N; Opening += o.Opening; CloakSaved += o.CloakSaved; Retort += o.Retort; SipTurns += o.SipTurns; SipHp += o.SipHp; AkaFires += o.AkaFires; AkaSpent += o.AkaSpent; AkaDry += o.AkaDry;
            FoeMax = Math.Max(FoeMax, o.FoeMax);
            foreach (var (k, v) in o.SippedFrom) SippedFrom[k] = SippedFrom.GetValueOrDefault(k) + v;
        }
    }

    internal static Ex One(Formation f, I.W w, int seed)
    {
        var x = new Ex { N = 1 };
        var (r, p, e) = S367.Raw(f, w, seed);
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!p.Any(u => u.Def.Id == id)) continue;
            if (id == "sid") { x.Opening = t.Z374OpeningCloak; x.CloakSaved = t.Pw368CloakSaved; x.Retort = t.Z369Retort; }
            if (id == "vio") { x.SipTurns = t.Z374SipTurns; x.SipHp = t.Z374SipHp; }
            if (id == "susu") { x.AkaFires = t.AshFires; x.AkaSpent = t.AshSpent; x.AkaDry = t.AshDry; }
            if (t.Z374SippedFrom > 0) x.SippedFrom[id] = t.Z374SippedFrom;
        }
        x.FoeMax = e.Count == 0 ? 0 : e.Max(u => (long)u.RawCounter(StatusKeys.Poison));
        return x;
    }

    // ---------------------------------------------------------------------------------
    // §4 台 × 版 × 波
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        int nb = boards.Length;
        Console.WriteLine($"# {Tag} §4 毒の立ち上がりを早める —— {nb} 台 × 版 × 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。版はスィド ／ ヴィオだけを替える（紅の台はスィドの版だけ・傷の台はヴィオの版だけ）。駒は規定（この期は規定を変えない）。");
        Console.WriteLine();
        var grid = new Dictionary<(int, int, int), (S370.Agg A, Ex X)>();
        var keys = new List<(int, int, int)>();
        for (int bi = 0; bi < nb; bi++) for (int vi = 0; vi < boards[bi].Vs.Length; vi++) for (int wi = 0; wi < WavesOf(bi, nb).Length; wi++) keys.Add((bi, vi, wi));
        var res = new (S370.Agg A, Ex X)[keys.Count];
        Parallel.For(0, keys.Count, k =>
        {
            var (bi, vi, wi) = keys[k];
            var f = Apply(boards[bi].F, boards[bi].Vs[vi]);
            var w = WavesOf(bi, nb)[wi];
            var pa = new S370.Agg[Seeds]; var px = new Ex[Seeds];
            Parallel.For(0, Seeds, s => { pa[s] = S370.One(f, w, s); px[s] = One(f, w, s); });
            var a = new S370.Agg(); var x = new Ex();
            foreach (var q in pa) a.Add(q);
            foreach (var q in px) x.Add(q);
            res[k] = (a, x);
        });
        for (int k = 0; k < keys.Count; k++) grid[keys[k]] = res[k];

        Console.WriteLine("## 1. 台 × 版 × 波（勝率・倒しT（勝った戦）・倒れた味方（1戦あたり））");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            var ws = WavesOf(bi, nb);
            Console.WriteLine($"### {boards[bi].Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", ws.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(ws.Select(_ => "---|")));
            for (int vi = 0; vi < boards[bi].Vs.Length; vi++)
                Console.WriteLine($"| {boards[bi].Vs[vi].Name} | {string.Join(" | ", Enumerable.Range(0, ws.Length).Select(wi => grid[(bi, vi, wi)].A.Cell))} |");
            Console.WriteLine();
        }

        Console.WriteLine("## 2. 誰が何Tに倒れたか（駒 倒れた戦の割合%（倒れたターンの平均））");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            var ws = WavesOf(bi, nb);
            Console.WriteLine($"### {boards[bi].Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", ws.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(ws.Select(_ => "---|")));
            for (int vi = 0; vi < boards[bi].Vs.Length; vi++)
                Console.WriteLine($"| {boards[bi].Vs[vi].Name} | {string.Join(" | ", Enumerable.Range(0, ws.Length).Select(wi => grid[(bi, vi, wi)].A.FellCell))} |");
            Console.WriteLine();
        }
        Console.WriteLine("### 精鋭で前の駒とスィドが倒れた戦の割合 ／ 倒れたターンの平均（近衛 ／ 大隊）");
        Console.WriteLine();
        string FellOf(S370.Agg a, string id) => a.FellBy.TryGetValue(id, out var s) ? $"{Pct(s.N, a.N)}%・T{F1((double)s.TSum / s.N)}" : "0%";
        Console.WriteLine("| 台 | 波 | 版 | スィド | ガルド | ドルガ | グザ | ヴィオ |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < 4; bi++)
            foreach (int wi in new[] { 1, 2 })
                for (int vi = 0; vi < boards[bi].Vs.Length; vi++)
                {
                    var a = grid[(bi, vi, wi)].A;
                    Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {(vi == 0 ? Waves[wi].Name : "")} | {boards[bi].Vs[vi].Name} | {FellOf(a, "sid")} | {FellOf(a, "gald")} | {FellOf(a, "dolga")} | {FellOf(a, "guza")} | {FellOf(a, "vio")} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 3. スィドの層（ターンの頭・T1〜T6・生きていた戦の平均）・衣で減った傷 ／ 返し（1戦）・開戦の衣で付けた層（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | スィドの層（頭） | 衣で減った傷 | 返し | 開戦の衣 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|");
        foreach (int bi in new[] { 0, 1, 2 })
            foreach (int wi in new[] { 0, 1, 2, 5 })
                for (int vi = 0; vi < boards[bi].Vs.Length; vi++)
                {
                    var (a, x) = grid[(bi, vi, wi)];
                    Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {(vi == 0 ? Waves[wi].Name : "")} | {boards[bi].Vs[vi].Name} | {T6(a.SLayer, a.SAlive, a)} | {Per(x.CloakSaved, x.N)} | {Per(x.Retort, x.N)} | {Per(x.Opening, x.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 4. ヴィオの攻撃力（ターンの頭・T1〜T6）・2連撃 ／ 3連撃に上がったターン・血を啜ったターンの数と啜った HP（1戦・隣の誰から）");
        Console.WriteLine();
        Console.WriteLine("ヴィオの攻撃力（頭）はターンの頭の写し（そのターンの吸い ／ 啜りの前）——段はその後で上がるので、表の数字より1ターン早く上がって見える。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | ヴィオの攻撃力（頭） | 2連撃 ／ 3連撃 | 啜ったターン | 啜った HP | 啜られた（隣の誰から） |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|---|");
        foreach (int bi in new[] { 0, 1, 2, 5 })
            foreach (int wi in new[] { 0, 1, 2, 5 })
                for (int vi = 0; vi < boards[bi].Vs.Length; vi++)
                {
                    var (a, x) = grid[(bi, vi, wi)];
                    string st = $"{(a.Stage2N == 0 ? "—" : $"T{F1((double)a.Stage2T / a.Stage2N)}（{Pct(a.Stage2N, a.N)}%）")} ／ {(a.Stage3N == 0 ? "—" : $"T{F1((double)a.Stage3T / a.Stage3N)}（{Pct(a.Stage3N, a.N)}%）")}";
                    string from = x.SippedFrom.Count == 0 ? "—" : string.Join("・", x.SippedFrom.OrderByDescending(kv => kv.Value).Select(kv => $"{Nm(kv.Key)} {Per(kv.Value, x.N)}"));
                    Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {(vi == 0 ? Waves[wi].Name : "")} | {boards[bi].Vs[vi].Name} | {T6(a.VAtk, a.VAlive, a)} | {st} | {Per(x.SipTurns, x.N)} | {Per(x.SipHp, x.N)} | {from} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 5. 傷の台: アカが詠んで撃った血（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 勝率・倒しT | 撃った回数 | 撃った血の合計 | 1回あたり | 傷が無くて素振り | 啜った HP |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|");
        for (int wi = 0; wi < Waves.Length; wi++)
            for (int vi = 0; vi < boards[5].Vs.Length; vi++)
            {
                var (a, x) = grid[(5, vi, wi)];
                Console.WriteLine($"| {(vi == 0 ? Waves[wi].Name : "")} | {boards[5].Vs[vi].Name} | {F1(a.Win)}・{a.WinTs}T | {Per(x.AkaFires, x.N)} | {Per(x.AkaSpent, x.N)} | {(x.AkaFires == 0 ? "—" : F1((double)x.AkaSpent / x.AkaFires))} | {Per(x.AkaDry, x.N)} | {Per(x.SipHp, x.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 6. 紅の台（スィドの版）: 勝率・決着T・スィドの返し・敵の毒の最大（その波の 100 戦の最大・戦の終わりの値）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 勝率・倒しT | 倒れた味方 | スィドの返し（1戦） | 敵の毒の最大 | 開戦の衣 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|");
        var gws = WavesOf(4, nb);
        for (int wi = 0; wi < gws.Length; wi++)
            for (int vi = 0; vi < boards[4].Vs.Length; vi++)
            {
                var (a, x) = grid[(4, vi, wi)];
                Console.WriteLine($"| {(vi == 0 ? gws[wi].Name : "")} | {boards[4].Vs[vi].Name} | {F1(a.Win)}・{a.WinTs}T | {Per(a.Fell, a.N)} | {Per(x.Retort, x.N)} | {x.FoeMax} | {Per(x.Opening, x.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("ポンの試遊の条件（第4波・HP 1000% ／ 攻 300%・seed 7）の1戦: " + string.Join("・", boards[4].Vs.Select(v =>
        {
            var g = S373.GurenOne(Apply(boards[4].F, v), S373.MainW(4, S373.Pon, "pon"), 7);
            return $"{v.Name} {(g.Wins > 0 ? "勝ち" : "負け")} {g.Turns}T・スィドの毒 {g.MaxSid}・紅 {g.MaxGuren}・敵の毒 {g.MaxFoe}";
        })) + "。");
        Console.WriteLine();

        var bad = new List<string>();
        for (int bi = 0; bi < nb; bi++)
        {
            var ws = WavesOf(bi, nb);
            for (int wi = 0; wi < ws.Length; wi++)
            {
                var b = grid[(bi, 0, wi)].A;
                for (int vi = 1; vi < boards[bi].Vs.Length; vi++)
                {
                    var a = grid[(bi, vi, wi)].A; var w = ws[wi]; string nm = $"{boards[bi].Name} × {boards[bi].Vs[vi].Name} × {w.Name}";
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{nm}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (w.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4)) bad.Add($"{nm}: 倒しT {a.WinTs}（規定 {b.WinTs}）");
                    if (100.0 * (a.Draw - b.Draw) / a.N >= 5) bad.Add($"{nm}: 30T の打ち切り {Pct(b.Draw, b.N)} → {Pct(a.Draw, a.N)}%");
                    if (b.Win - a.Win >= 15) bad.Add($"{nm}: 勝率 {F1(b.Win)} → {F1(a.Win)}（参考・落ちた側）");
                }
            }
        }
        Console.WriteLine("## 8. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満（規定で既に 4T 未満は除く）／ 30T の打ち切りが 5pt 以上増える ／ 参考: 勝率が規定から 15pt 以上落ちる）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static string T6(long[] v, long[]? alive, S370.Agg a) => string.Join(" ", Enumerable.Range(1, LoopT).Select(t => alive is null ? Per(v[t], a.N) : alive[t] == 0 ? "—" : Per(v[t], alive[t])));

    // ---------------------------------------------------------------------------------
    // `compare` × 版
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows, Func<Formation, Formation> map, Func<int, bool> only)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            if (!only(ri)) { g[ri, wi] = double.NaN; return; }
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareVer(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = ver switch { "S10" => VS10, "S20" => VS20, "C" => VC, "S10C" => VS10C, _ => throw new ArgumentException("sh374 compare の版は S10 / S20 / C / S10C") };
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        Func<int, bool> only = ri => (v.Sid is not null && Has(rows[ri].F, "sid")) || (v.Vio is not null && Has(rows[ri].F, "vio"));
        var b = CGrid(rows, f => f, _ => true);
        var g0 = CGrid(rows, f => Apply(f, v), only);
        var g = new double[rows.Length, nw];
        int touched = 0;
        for (int r = 0; r < rows.Length; r++) { if (only(r)) touched++; for (int w = 0; w < nw; w++) g[r, w] = only(r) ? g0[r, w] : b[r, w]; }
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        Console.WriteLine($"# {Tag} `compare` {rows.Length} 行 × 版 {v.Name}（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"版で回した行 {touched}（{(v.Sid is not null ? "スィド" : "")}{(v.Sid is not null && v.Vio is not null ? " ／ " : "")}{(v.Vio is not null ? "ヴィオ" : "")}のいる行・{string.Join("、", Enumerable.Range(0, rows.Length).Where(only).Select(ri => rows[ri].Name))}）。");
        Console.WriteLine();
        int upR = 0, upC = 0, dnR = 0, dnC = 0;
        var lines = new List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            bool u = false, d = false;
            for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
            if (u) upR++; if (d) dnR++;
            if (u || d) lines.Add($"{rows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
        }
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        Console.WriteLine($"| {v.Name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
        Console.WriteLine();
        foreach (var l in lines) Console.WriteLine("- " + l);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
