using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using T323 = Tou323Diag;

// =====================================================================================
// tou324 —— 第324期「伝染の印は敵にだけ（TP-e）＋ 濃さ・焼との重ね（TD-b焼 × TP-e）」。
// 指示書は design/PHASE324_TOU_POWDER2_SPEC.md ／ 報告は design/PHASE324_TOU_POWDER2.md。
//
//     dotnet run --project BattleSim -c Release 0 tou324 p0                 # §3 Phase 0（第323期の版だけで測る）
//     dotnet run --project BattleSim -c Release 0 tou324 boards [版,…]      # §4-2: 代表台 5 × 10 波 × 版（`tou323` と同じ表）
//     dotnet run --project BattleSim -c Release 0 tou324 confirm            # §4-2: 本編の第2 ／ 5波で落ちたセルを seed 200..999 で確かめる
//     dotnet run --project BattleSim -c Release 0 tou324 compare [版,…]     # §4-3: `compare` 64 行 × 版
//     dotnet run --project BattleSim -c Release 0 tou324 grid <boss|guard> <版>   # §4-4: 第323期と同じ作りの格子
//     dotnet run --project BattleSim -c Release 0 tou324 memo <台の一部> <波> <seed> <版>   # §5: 伝染 ／ 濃さ ／ 返す ／ 焼の並び
//     dotnet run --project BattleSim -c Release 0 tou324 check              # §6 自己検査
//
// 版の名前（ASCII）: base（規定）／ tpa（TP-a）／ tdbb（TD-b焼）／ tpe（TP-e）／ tped（TP-e′）／ x（TD-b焼 × TP-e）／ xd（TD-b焼 × TP-e′）。
// **予測のファイルが無ければ本測定（boards ／ confirm ／ compare ／ grid）を走らせない**（R397・`.tmp/p324/predict.md`）。
// =====================================================================================
static partial class Tou324Diag
{
    internal const string PredictFile = ".tmp/p324/predict.md";
    const int Seeds = 200;
    const string Tag = "第324期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "confirm" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"tou324: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            default: RunMore(mode, A); return;
        }
    }

    static partial void RunMoreImpl(string mode, Func<int, string, string> a, ref bool done);
    static void RunMore(string mode, Func<int, string, string> a)
    {
        bool done = false;
        RunMoreImpl(mode, a, ref done);
        if (!done) Console.WriteLine("tou324: モードは p0 / boards / confirm / compare / grid / memo / check。");
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    static (string Name, Formation F) Board(string n) => T323.Boards().First(b => b.Name == n);
    static T323.Ver V323(string ascii) => T323.Vers.First(v => v.Ascii == ascii);

    // ---------------------------------------------------------------------------------
    // §3 Phase 0（第323期の版だけで測る・実装の前）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new (string B, string W)[]
        {
            ("雷の型", "5"), ("雷の型", "2"), ("責め苦", "5"), ("責め苦", "2"), ("光の盾", "5"), ("光の盾", "2"),
        };
        var vb = V323("base"); var va = V323("tpa"); var vd = V323("tpad"); var vt = V323("tdbb");
        Console.WriteLine("# 第324期 Phase 0 —— 第323期の版（TP-a ／ TP-a′ ／ TD-b焼）で、本編の落ちの中身を分ける（seed 0..199・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("## 1. TP-a の本編の落ちのうち、味方の側の伝染が原因の分（§3-1）");
        Console.WriteLine();
        Console.WriteLine("同じ seed で規定と TP-a を並べ、TP-a の戦を「味方に伝染が1度でも起きた戦」と「起きなかった戦」に分ける。味方の側の伝染で落ちた戦 ＝ 規定で勝って TP-a で負けた戦のうち、味方に伝染が起きた戦。"
            + "伝染した味方の弾け ＝ 伝染で帯電した味方が後で弾けた延べ。");
        Console.WriteLine();
        Console.WriteLine("| 台 × 波 | 規定 | TP-a | 味方に伝染した戦（勝率） | 起きなかった戦（勝率） | 規定で勝ち → TP-a で負け（うち味方に伝染） | 伝染 敵 ／ 味方 | 伝染した味方の弾け | 味方の弾け 規定 → TP-a | 倒れた味方 規定 → TP-a |");
        Console.WriteLine("|---|--:|--:|---|---|---|---|--:|---|---|");
        foreach (var (bn, wn) in cells)
        {
            var (_, f) = Board(bn);
            var w = T323.WaveOf(wn);
            var b = new T323.Z[Seeds]; var a = new T323.Z[Seeds];
            Parallel.For(0, Seeds, sd => { b[sd] = T323.Fight(T323.Apply(f, vb), w, sd); a[sd] = T323.Fight(T323.Apply(f, va), w, sd); });
            int nWith = 0, wWith = 0, nWo = 0, wWo = 0, flips = 0, flipsAlly = 0;
            var zb = new T323.Z(); var za = new T323.Z();
            for (int sd = 0; sd < Seeds; sd++)
            {
                zb.Merge(b[sd]); za.Merge(a[sd]);
                bool ally = a[sd].T.TpNewAlly > 0;
                if (ally) { nWith++; wWith += (int)a[sd].Wins; } else { nWo++; wWo += (int)a[sd].Wins; }
                if (b[sd].Wins == 1 && a[sd].Wins == 0) { flips++; if (ally) flipsAlly++; }
            }
            Console.WriteLine($"| {bn} × {w.Name} | {F1(zb.Win)} | {F1(za.Win)} | {nWith} 戦（{Pct(wWith, nWith)}%） | {nWo} 戦（{Pct(wWo, nWo)}%） | {flips}（{flipsAlly}） | {Per(za.T.TpNewFoe, za.N)} ／ {Per(za.T.TpNewAlly, za.N)} | {Per(za.T.TpInfPopsAlly, za.N)} | {Per(zb.AllyPopsEv, zb.N)} → {Per(za.AllyPopsEv, za.N)} | {Per2(zb.AllyDeaths, zb.N)} → {Per2(za.AllyDeaths, za.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 2. TP-a′ が `責め苦` × 第5波を TP-a より落とした理由（§3-2）");
        Console.WriteLine();
        Console.WriteLine("| 台 × 波 | 版 | 勝率 | 倒しT ／ 負けT | 伝染 敵 ／ 味方 | うち帯電し直した | 敵の弾け ／ 連鎖の大きさ | 味方の弾け | 倒れた味方 | 味方の放電被弾 |");
        Console.WriteLine("|---|---|--:|---|---|--:|---|--:|--:|--:|");
        foreach (var (bn, wn) in new[] { ("責め苦", "5"), ("責め苦", "2"), ("雷の型", "5") })
        {
            var (_, f) = Board(bn);
            var w = T323.WaveOf(wn);
            foreach (var v in new[] { vb, va, vd })
            {
                var z = T323.Measure(T323.Apply(f, v), w);
                Console.WriteLine($"| {bn} × {w.Name} | {v.Name} | {F1(z.Win)} | {Per(z.WinT, z.Wins)} ／ {Per(z.LoseT, z.N - z.Wins)} | {Per(z.T.TpNewFoe, z.N)} ／ {Per(z.T.TpNewAlly, z.N)} | {Per(z.T.TpReArm, z.N)} | {Per(z.FoePops, z.N)} ／ {(z.FoeRoots == 0 ? "—" : (z.FoePops / (double)z.FoeRoots).ToString("F2"))} | {Per(z.AllyPopsEv, z.N)} | {Per2(z.AllyDeaths, z.N)} | {Per(z.AllyDisTaken, z.N)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 3. 重ねた版の見込み: 漏れ先の味方に付く濃さと、味方が弾けたときの焼（§3-3・TD-b焼で測った）");
        Console.WriteLine();
        Console.WriteLine("濃さ 付いた ＝ 味方に付いた濃さ +1 の延べ。味方が濃さ ≧1 で弾けた ＝ 重い放電 ／ 焼の起点。焼 ＝ 味方が自分を焼いた量。重い放電 ＝ 濃い味方の放電が味方に与えた量。");
        Console.WriteLine();
        Console.WriteLine("| 台 × 波 | 版 | 勝率 | 濃さ 付いた 敵 ／ 味方 | 弾けた（濃さ ≧1）敵 ／ 味方 | 焼 敵 ／ 味方 | 重い放電 味方 | 味方の弾け | 倒れた味方 ／ うち放電で |");
        Console.WriteLine("|---|---|--:|---|---|---|--:|--:|---|");
        foreach (var (bn, wn) in new[] { ("光の盾", "2"), ("光の盾", "3"), ("光の盾", "boss"), ("雷の型", "5"), ("糸", "bat") })
        {
            var (_, f) = Board(bn);
            var w = T323.WaveOf(wn);
            foreach (var v in new[] { vb, vt })
            {
                var z = T323.Measure(T323.Apply(f, v), w);
                Console.WriteLine($"| {bn} × {w.Name} | {v.Name} | {F1(z.Win)} | {Per(z.T.TdGainFoe, z.N)} ／ {Per(z.T.TdGainAlly, z.N)} | {Per(z.T.TdPopFoe, z.N)} ／ {Per(z.T.TdPopAlly, z.N)} | {Per(z.T.TdBurnFoe, z.N)} ／ {Per(z.T.TdBurnAlly, z.N)} | {Per(z.T.TdHeavyAllyDealt, z.N)} | {Per(z.AllyPopsEv, z.N)} | {Per2(z.AllyDeaths, z.N)} ／ {Per2(z.AllyDisDeaths, z.N)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }
}
