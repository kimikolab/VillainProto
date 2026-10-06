using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// =====================================================================================
// sweep モード（第141期） —— 全診断の exit 検査
//
// **第140期の走査（Python の一時スクリプト・CRLF で1度壊れた）をリポジトリの器具にしたもの。**
// コマンド表（`design/COMMANDS.md`・第259期までは `CLAUDE.md`）を**自分で読んで**「引数の穴が無い」コマンドを組み、上限つきで
// 子プロセスとして走らせ、exit code と所要を記録する。**戦闘はすべて子プロセスの中**で、
// このモード自身は盤面に触らない。子の標準出力は表には出さない（指紋＝ハッシュだけを控える。`> docs/*.md` の向き先も含めて、ファイルは書かない）。
//
//   合格 = **異常終了（0 でも上限でもない exit）が 0 本**。
//   上限（124）は失敗に数えない——ただし一覧に出し、**文書上「長い」と分かっている本（`KnownLong`）と
//   新しく上限に当たった本を分けて出す**。
//
// **第278期に速くした（判定の意味・表の行は変えない）。** 3つ:
//   (A) 指紋照合 —— 本ごとに「入力の鍵」（BattleCore の全ソース ＋ その本の器具が参照するソース ＋ ファイルを読む器具なら docs/・design/ 等）の
//       ハッシュを作り、**前回その鍵で走らせた結果がキャッシュ（`.sweep/cache.tsv`・コミットしない）にあれば走らせずに照合で返す**。
//       表の「秒」の列に `（照合）` と出し、集計に「実行 n 本・指紋照合 m 本」を出す。**鍵が1つでも動いた本は必ず走らせる**（部分適用の影響グラフは作らない）。
//   (B) 並列化 —— **既定は直列のまま**。`j=N` で明示したときだけ、単独で走った回の CPU 使用（コア数）を予算（物理コア数）として詰め込む。
//       **同じ系統（2語目）の本は一覧の順に直列**（pick が書いた TSV を run が読む）。上限・上限付近・重い本は単独。
//       **並列の回は上限の判定が直列と揃わない**（第278期の実測: 並べ方を2通り試して 33 本 ／ 21 本が食い違った・速さは 1.59 倍 ／ 1.0 倍）——早見用。
//   (C) 層 —— `sweep fast`（主判定系の系統だけ・期中）／ `sweep full`（全量・コミット前）。引数なしの `sweep` は `full`。
//
// **運用（第278期）: 期中は `sweep fast`、`UnitCatalog.All` ／ `Retired` ／ `Presets` に触る期はコミット前に `sweep full`。**
//
// **第279期: 凍結庫（`design/SWEEP_FREEZER.md`）。** 第278期の full で上限に当たった 78 本を仕分けた:
//   凍結 70 本（`Frozen`・火の段1探索 24 ／ 重い総当たり 25 ／ 旧期の1コア器具 21）は sweep の一覧から外し、別一覧として持つ。
//   docs の生成器のうち `audit` が出力の整合を見ている 2 本（`layout` ／ `reseat`）は外す（**docs のずれは `audit` が正**）。
//   `roster audit` は `audit` が見ていないので残し、上限だけを個別に延ばす（`LimitOverride`）。
//   `relic` の重い 5 本は `smoke`（seed を 1/10）で回す（`SmokeReplaced`・縮めた中身は `design/COMMANDS.md`）。
//   既定の上限は 90 → 150 秒（直列で 84〜89 秒の境目の本が揺れていた——`creak` は第277期に上限・第278期に 84.2 秒）。
//   **残った本は全部完走するので、上限は異常終了に数える**（`KnownLong` は役目を終えて消した）。
//   凍結した本は `sweep seal`（封印＝完走した出力の指紋）と `sweep thaw <絞り込み>`（解凍＝走らせて封印と照合）で扱う。
//
// CRLF の穴（第140期）: 一覧は**行を読んでトークンに割ってから `ArgumentList` で渡す**ので、
// 行末の `\r` が引数の末尾に紛れ込む経路が無い（第123期「一覧は連結で組む」）。
//
//     dotnet run --project BattleSim -c Release 0 sweep                  # = full（上限 90 秒・指紋照合あり・直列）
//     dotnet run --project BattleSim -c Release 0 sweep full             # 全量
//     dotnet run --project BattleSim -c Release 0 sweep fast             # 主判定系の系統だけ（`FastFamilies`）
//     dotnet run --project BattleSim -c Release 0 sweep full nocache     # 指紋照合を使わずに全部走らせる（キャッシュは更新する）
//     dotnet run --project BattleSim -c Release 0 sweep full j=16        # 並列（早見用・上限の判定が直列と揃わない）。並列度は j=N か環境変数 SWEEP_JOBS
//     dotnet run --project BattleSim -c Release 0 sweep 120 suture2,gather   # 上限を秒で・部分一致で絞る（カンマ区切り）
//     dotnet run --project BattleSim -c Release 0 sweep list             # 一覧だけ出す（戦闘0回・子プロセス0本）
//     dotnet run --project BattleSim -c Release 0 sweep check            # 自己検査（鍵の作り方。ファイルは書き換えない・子プロセス0本）
//     dotnet run --project BattleSim -c Release 0 sweep frozen           # 第279期 凍結庫の一覧と封印（子プロセス0本）
//     dotnet run --project BattleSim -c Release 0 sweep seal 600 j=1 G3  # 第279期 凍結した本を上限つきで完走させ、出力の指紋を封印に書く（`design/freezer/`）
//     dotnet run --project BattleSim -c Release 0 sweep thaw draft3      # 第279期 解凍: 凍結した本を走らせて封印と照合（既定の上限 7200 秒）
// =====================================================================================
static class SweepDiag
{
    /// <summary>既定の上限（第279期に 90 → 150 秒）。残った本は直列で最長 89 秒——境目で判定が揺れないだけの余白。</summary>
    const int DefaultLimitSec = 150;

    // 第141〜278期の `KnownLong`（文書上「長い」と分かっている本——上限に当たっても「既知」と出した）は第279期に消した。
    // 載っていた 26 本はすべて凍結（`Frozen`）か、`audit` が正の docs の生成器（`DocsByAudit`）へ移った。

    /// <summary>
    /// **凍結庫（第279期・`design/SWEEP_FREEZER.md`）**——sweep の一覧から外した本。コードと器具は消さない。
    /// 第278期の直列の full で 3 回とも（`yoke` は 2 回）上限 90 秒に当たり、**一度も最後まで走っていなかった**本。
    /// 群: G2 火の段1 探索（pick ／ run）／ G3 重い総当たり（26〜30 コア）／ G4 旧期の1コアの器具。期はコマンド表の注記から。
    /// 解凍は `sweep thaw <絞り込み>`（走らせて `design/freezer/seals.tsv` の封印と照合・不一致なら git bisect）。
    /// </summary>
    internal static readonly (string Cmd, string Group, int Phase)[] Frozen =
    {
        ("0 burnaudit pick", "G2", 233), ("0 burnaudit run", "G2", 233),
        ("0 borgguard pick", "G2", 234), ("0 borgguard run", "G2", 234),
        ("0 borgfront pick", "G2", 235), ("0 borgfront run", "G2", 235),
        ("0 fireward pick", "G2", 238), ("0 fireward run", "G2", 238),
        ("0 firelevel pick", "G2", 242), ("0 firelevel run", "G2", 242),
        ("0 fireburst pick", "G2", 244), ("0 fireburst run", "G2", 244),
        ("0 enemyfire pick", "G2", 245), ("0 enemyfire run", "G2", 245),
        ("0 firecycle pick", "G2", 246), ("0 firecycle run", "G2", 246),
        ("0 firetri pick", "G2", 247), ("0 firetri run", "G2", 247),
        ("0 firefinish pick", "G2", 249), ("0 firefinish run", "G2", 249),
        ("0 fireatk pick", "G2", 250), ("0 fireatk run", "G2", 250),
        ("0 firekindle pick", "G2", 252), ("0 firekindle run", "G2", 252),

        ("0 draft", "G3", 69), ("0 draft alt", "G3", 69),
        ("0 draft2", "G3", 70), ("0 draft2 alt", "G3", 70),
        ("0 draft3", "G3", 71), ("0 draft3 alt", "G3", 71),
        ("0 slope", "G3", 72), ("0 slope alt", "G3", 72),
        ("0 creak3", "G3", 77), ("0 creak3 alt", "G3", 77),
        ("0 traits", "G3", 78), ("0 traits alt", "G3", 78),
        ("0 pairs", "G3", 80), ("0 pairs2", "G3", 81),
        ("0 soak redo89", "G3", 90), ("0 hold2 seats", "G3", 107), ("0 lit reseat", "G3", 113),
        ("0 stage rho", "G3", 158), ("0 stage perm", "G3", 159), ("0 stage permsd", "G3", 159),
        ("0 stage cross", "G3", 163), ("0 stage catalog", "G3", 164), ("0 stage short", "G3", 165),
        ("0 stage catalog seeds=800", "G3", 165), ("0 form2 run202", "G3", 202),

        ("0 gradient", "G4", 5), ("0 bridge", "G4", 7), ("0 wave", "G4", 15), ("0 dissect", "G4", 16),
        ("0 output", "G4", 17), ("0 convert", "G4", 18), ("0 seats2 degree", "G4", 45), ("0 seats2", "G4", 45),
        ("0 scapegoat", "G4", 49), ("0 divert", "G4", 50), ("0 divert seats", "G4", 50),
        ("0 spend", "G4", 65), ("0 spend alt", "G4", 65), ("0 carry solo", "G4", 68), ("0 creak alt", "G4", 77),
        ("0 hold tables", "G4", 106), ("0 wall run", "G4", 136), ("0 shard check docs/balance.md", "G4", 138),
        ("0 tumult scan probe", "G4", 144), ("0 gust check", "G4", 151), ("0 yoke", "G4", 25),
    };

    /// <summary>
    /// **docs の生成器のうち `audit` が出力の整合を見ている本（第279期・sweep から外す）。**
    /// `audit` は `docs/layout.md` ／ `docs/reseat.md` に `compare` の現行の編成名が1つ残らず現れるか（節の見出しも）を戦闘0回で見る。
    /// 生成器そのものは docs の再生成で毎回最後まで走る——sweep の 90 秒はその部分集合でしかなかった。**docs のずれは `audit` が正。**
    /// `roster audit` は `audit` の対象に無い（`docs/roster_audit.md` を見ていない）ので外さない。
    /// </summary>
    internal static readonly string[] DocsByAudit = { "0 layout", "0 reseat" };

    /// <summary>
    /// **煙試験の形に置き換えた本（第279期）**——sweep は右の形を回す（seed を 200 → 20 に縮めた版・`RelicDiag.Smoke`）。
    /// 目的は「完走して同じ指紋を返す」こと。左の本そのものは測定の本体として残る（sweep では回さない）。
    /// </summary>
    internal static readonly (string Full, string Smoke)[] SmokeReplaced =
    {
        ("0 relic grid", "0 relic grid smoke"), ("0 relic junk", "0 relic junk smoke"), ("0 relic mainwin", "0 relic mainwin smoke"),
        ("0 relic rejudge", "0 relic rejudge smoke"), ("0 relic expand", "0 relic expand smoke"),
    };

    /// <summary>本ごとの上限（秒・第279期）。既定より長く要る本だけ。`roster audit` は単独で 130〜280 秒（全コア）。</summary>
    static readonly Dictionary<string, int> LimitOverride = new(StringComparer.Ordinal)
    {
        ["0 roster audit"] = 600,
    };

    static int LimitOf(string cmd, int limit) => LimitOverride.TryGetValue(cmd, out int s) ? Math.Max(s, limit) : limit;

    /// <summary>sweep の一覧から外す本（凍結 ＋ audit が正 ＋ 煙試験に置き換えた本体）。</summary>
    static bool Excluded(string cmd) => Frozen.Any(f => f.Cmd == cmd) || DocsByAudit.Contains(cmd) || SmokeReplaced.Any(s => s.Full == cmd);

    /// <summary>
    /// **`sweep fast` の系統（第278期）**——主判定系（`compare` と、それを読む生成物・現役の物差し）。期中の判定用。
    /// 系統はコマンドの2語目（`0 compare quality` なら `compare`）。
    /// </summary>
    internal static readonly string[] FastFamilies =
    {
        "compare", "dump", "audit", "derive", "spread", "chain", "cross", "checkup",
        "checkwave", "bosswave", "relic",
    };

    /// <summary>コマンド表の行頭。**連結で組む**（この診断自身がコマンド表＝`design/COMMANDS.md` に載るので、素直に書くと自分の行に当たる＝第123期）。</summary>
    static readonly string Prefix = string.Concat("    dotnet run --project ", "BattleSim -c Release ");

    /// <summary>1本の結果。<c>Cached</c> は指紋照合で返したもの（走らせていない）。<c>Cpu</c> は子プロセスの CPU 秒、<c>Out</c> は標準出力の指紋。</summary>
    sealed record Result(string Cmd, double Sec, int Exit, string Tail, double Cpu, string Out, bool Cached);

    public static void Run(string[] rest)
    {
        int limit = DefaultLimitSec;
        var filters = new List<string>();
        string mode = "full";
        bool listOnly = false, noCache = false;
        // 第278期: **既定は直列**（j=1）。並列は j=N か環境変数 SWEEP_JOBS で明示したときだけ——上限 90 秒が壁時計なので、並べると 54〜90 秒の本の判定が揺れる（報告 §3）。
        int jobs = int.TryParse(Environment.GetEnvironmentVariable("SWEEP_JOBS"), out int ej) && ej > 0 ? ej : 1;
        if (rest.Length > 0 && rest[0] is "frozen" or "seal" or "thaw") { Freezer(rest); return; }
        foreach (string a in rest)
        {
            if (a == "list") listOnly = true;
            else if (a is "fast" or "full") mode = a;
            else if (a == "check") { Check(); return; }
            else if (a == "nocache") noCache = true;
            else if (a.StartsWith("j=", StringComparison.Ordinal) && int.TryParse(a[2..], out int j) && j > 0) jobs = j;
            else if (int.TryParse(a, out int n) && n > 0) limit = n;
            else filters.AddRange(a.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        string root = FindRoot() ?? throw new InvalidOperationException("CLAUDE.md と BattleSim/ を持つディレクトリが見つからない（リポジトリの中で回すこと）");
        string dll = typeof(SweepDiag).Assembly.Location;
        // 第259期: コマンド表は `CLAUDE.md` から `design/COMMANDS.md` へ逐語で移った（行の書式は同じ）。
        var all = Extract(Path.Combine(root, "design", "COMMANDS.md"));
        // 第279期: 凍結庫 ／ audit が正の生成器 ／ 煙試験に置き換えた本体は一覧から外す。
        var cmds = all.Where(c => !Excluded(c)).ToList();
        int nFrozen = all.Count(c => Frozen.Any(f => f.Cmd == c)), nDocs = all.Count(DocsByAudit.Contains), nSmoke = all.Count(c => SmokeReplaced.Any(s => s.Full == c));
        if (mode == "fast") cmds = cmds.Where(c => FastFamilies.Contains(FamilyOf(c))).ToList();
        if (filters.Count > 0) cmds = cmds.Where(c => filters.Any(f => c.Contains(f, StringComparison.Ordinal))).ToList();

        Console.WriteLine("# 全診断の exit 検査（`sweep`・第141期）");
        Console.WriteLine();
        Console.WriteLine($"`design/COMMANDS.md` のコマンド表から引数の穴（`<...>`）の無い行を抜き出し、`[...]` を落として **{cmds.Count} 本**。");
        Console.WriteLine($"外した本（第279期）: 凍結庫 {nFrozen} ／ audit が正の docs 生成器 {nDocs} ／ 煙試験に置き換えた本体 {nSmoke}（`design/SWEEP_FREEZER.md`）。");
        Console.WriteLine($"1本ずつ **{limit} 秒**の上限で子プロセスとして走らせる（{string.Join(" ／ ", LimitOverride.Select(kv => $"`{kv.Key}` は {kv.Value} 秒"))}・標準出力は捨てる・ファイルは書かない）。**上限は異常終了に数える**（第279期）。");
        Console.WriteLine($"`{Path.GetFileName(dll)}` を `{root}` で回す。");
        Console.WriteLine($"層 **{mode}** ／ 並列度 {jobs}（CPU 予算 {PhysicalCores} コア＝物理）／ 指紋照合 {(noCache ? "**使わない**（nocache）" : "あり（`.sweep/cache.tsv`）")}（第278期）。");
        Console.WriteLine();
        if (cmds.Count == 0) { Console.WriteLine("**一覧が空。止める**（第117期）。"); Environment.ExitCode = 2; return; }

        if (listOnly)
        {
            Console.WriteLine("| # | コマンド | 上限（秒） |");
            Console.WriteLine("|--:|---|--:|");
            for (int i = 0; i < cmds.Count; i++) Console.WriteLine($"| {i + 1} | `{cmds[i]}` | {LimitOf(cmds[i], limit)} |");
            return;
        }

        var keys = new KeyBuilder(root, null);
        var cache = SweepCache.Load(root);
        var total = Stopwatch.StartNew();
        var rows = RunAll(dll, root, cmds, limit, jobs, keys, cache, noCache);
        total.Stop();
        cache.Save();

        var ok = rows.Where(r => r.Exit == 0).ToList();
        var timeouts = rows.Where(r => r.Exit == 124).ToList();
        var bad = rows.Where(r => r.Exit != 0 && r.Exit != 124).ToList();

        Console.WriteLine();
        Console.WriteLine("## 集計");
        Console.WriteLine();
        Console.WriteLine("| 判定 | 本数 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| 正常（exit 0） | {ok.Count} |");
        Console.WriteLine($"| **上限（{limit} 秒・第279期から異常）** | **{timeouts.Count}** |");
        Console.WriteLine($"| **異常終了** | **{bad.Count}** |");
        Console.WriteLine($"| 合計 | {rows.Count}（{total.Elapsed.TotalMinutes:F1} 分） |");
        Console.WriteLine($"| うち実行 ／ 指紋照合（ハッシュ一致） | {rows.Count(r => !r.Cached)} ／ {rows.Count(r => r.Cached)} |");
        Console.WriteLine();

        Console.WriteLine("## 異常終了" + (bad.Count == 0 ? " —— **0 本**" : ""));
        Console.WriteLine();
        if (bad.Count > 0)
        {
            Console.WriteLine("| コマンド | exit | 最後の例外行 |");
            Console.WriteLine("|---|--:|---|");
            foreach (var r in bad) Console.WriteLine($"| `{r.Cmd}` | {ExitText(r.Exit)} | {r.Tail} |");
            Console.WriteLine();
        }

        Console.WriteLine("## 上限に当たった本" + (timeouts.Count == 0 ? " —— 0 本" : "（第279期から異常。遅くなったか止まった——凍結するかは期の判断）"));
        Console.WriteLine();
        if (timeouts.Count > 0)
        {
            Console.WriteLine("| コマンド | 上限（秒） |");
            Console.WriteLine("|---|--:|");
            foreach (var r in timeouts) Console.WriteLine($"| `{r.Cmd}` | {LimitOf(r.Cmd, limit)} |");
            Console.WriteLine();
        }
        Console.WriteLine();

        Console.WriteLine("## 正常のうち遅い上位 10");
        Console.WriteLine();
        foreach (var r in ok.OrderByDescending(r => r.Sec).Take(10)) Console.WriteLine($"- `{r.Cmd}` {r.Sec:F1}s{(r.Cached ? "（照合）" : "")}");
        Console.WriteLine();
        int fail = bad.Count + timeouts.Count;
        Console.WriteLine(fail == 0 ? "**合格**（異常終了 0 本・上限 0 本）。" : $"**不合格**（異常終了 {bad.Count} 本・上限 {timeouts.Count} 本）。");
        Environment.ExitCode = fail == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------------------------
    // (B) 走らせ方 —— CPU 予算の詰め込み。結果の行は一覧の順に出す（終わった順ではない）。
    // ---------------------------------------------------------------------------------

    // **判定（exit 0 か上限か）を直列と揃えるための並べ方**（第278期の実測で決めた）:
    //   1本目の並列の full（予算＝論理 32 コア）では、直列なら 35〜84 秒で終わる 1コアの本 33 本が上限を超えた——
    //   物理 16 コアを SMT で分け合い、全コアが埋まってターボも落ちて、1本あたりの速さが単独の半分以下になったため。
    //   → 予算は**物理コア数**。単独で走らせるのは「履歴が無い」「上限に当たった」「上限の 60% を超える」「重くて上限の 25% を超える」本。

    /// <summary>重い（コアを使い切る）とみなすコア数。</summary>
    const double HeavyCores = 4.0;
    /// <summary>単独で走らせる所要（直前の単独の所要 ÷ 上限）。</summary>
    const double NearLimit = 0.6;
    /// <summary>重い本を単独で走らせる所要（同）。</summary>
    const double HeavyNear = 0.25;

    /// <summary>CPU 予算（物理コア数）。.NET は物理コア数を返さないので、論理コア数の半分（SMT 2 way）。環境変数 SWEEP_CORES で上書きできる。</summary>
    static int PhysicalCores => int.TryParse(Environment.GetEnvironmentVariable("SWEEP_CORES"), out int c) && c > 0 ? c : Math.Max(1, Environment.ProcessorCount / 2);

    static List<Result> RunAll(string dll, string root, List<string> cmds, int limit, int jobs, KeyBuilder keys, SweepCache cache, bool noCache)
    {
        int n = cmds.Count, budget = PhysicalCores;
        var results = new Result?[n];
        var keyOf = new string[n];
        var started = new bool[n];
        var cost = new int[n];
        var exclusive = new bool[n];
        for (int i = 0; i < n; i++)
        {
            keyOf[i] = keys.KeyOf(cmds[i], LimitOf(cmds[i], limit));
            if (!noCache && cache.TryHit(cmds[i], keyOf[i], out var hit))
            {
                results[i] = hit with { Cached = true };
                started[i] = true;
                continue;
            }
            // 予算: 並べ方用の履歴（**単独で走ったときの**所要・判定・CPU 秒）。履歴が無ければ単独。
            if (cache.TrySched(cmds[i], out var last) && last.Sec > 0)
            {
                double cores = Math.Max(1.0, last.Cpu / Math.Max(0.1, last.Sec));
                int lim = LimitOf(cmds[i], limit);
                exclusive[i] = jobs == 1 || last.Exit == 124 || last.Sec > NearLimit * lim || (cores >= HeavyCores && last.Sec > HeavyNear * lim);
                cost[i] = exclusive[i] ? budget : Math.Min(budget, (int)Math.Ceiling(cores));
            }
            else { exclusive[i] = true; cost[i] = budget; }
        }

        Console.WriteLine("| # | コマンド | 秒 | exit | 判定 |");
        Console.WriteLine("|--:|---|--:|--:|---|");
        int printed = 0;
        void Flush()
        {
            lock (results)
                while (printed < n && results[printed] is { } r)
                {
                    Console.WriteLine($"| {printed + 1} | `{r.Cmd}` | {(r.Cached ? "（照合）" : r.Sec.ToString("F1"))} | {ExitText(r.Exit)} | {Verdict(r.Exit)} |");
                    printed++;
                }
            Console.Out.Flush();
        }

        var running = new Dictionary<int, Task>();
        int used = 0;
        string FamilyAt(int i) => FamilyOf(cmds[i]);
        while (true)
        {
            Flush();
            if (printed == n && running.Count == 0) break;
            // 起動できる本を一覧の順に探す。同じ系統で前に未完の本があれば飛ばす。単独の本が先頭で待っていたら後ろは起動しない（飢えさせない）。
            var busyFamilies = new HashSet<string>(running.Keys.Select(FamilyAt));
            for (int i = 0; i < n; i++)
            {
                if (started[i]) continue;
                string fam = FamilyAt(i);
                if (busyFamilies.Contains(fam)) continue;
                busyFamilies.Add(fam);   // 同じ系統の後ろの本はこの周回では起動しない
                bool fits = running.Count == 0 || (!exclusive[i] && running.Count < jobs && used + cost[i] <= budget && !running.Keys.Any(k => exclusive[k]));
                if (!fits)
                {
                    if (exclusive[i]) break;
                    continue;
                }
                started[i] = true;
                used += cost[i];
                int idx = i;
                Console.Error.WriteLine($"[{idx + 1}/{n}] {cmds[idx]}");
                bool alone = running.Count == 0 && exclusive[idx];   // 単独で走った回だけ並べ方用の履歴を更新する
                running[idx] = Task.Run(() =>
                {
                    int lim = LimitOf(cmds[idx], limit);
                    var r = RunOne(dll, root, cmds[idx], lim);
                    cache.Put(cmds[idx], keyOf[idx], lim, r, alone || jobs == 1);
                    lock (results) results[idx] = r;
                });
            }
            if (running.Count == 0) continue;
            int done = Task.WaitAny(running.Values.ToArray(), 500);
            foreach (var k in running.Where(kv => kv.Value.IsCompleted).Select(kv => kv.Key).ToList())
            {
                running[k].GetAwaiter().GetResult();
                running.Remove(k);
                used -= cost[k];
            }
        }
        return results.Select(r => r!).ToList();
    }

    static string FamilyOf(string cmd) { var t = Tokens(cmd); return t.Length > 1 ? t[1] : cmd; }

    static string ExitText(int e) => e == 0 ? "0" : e == 124 ? "124（上限）" : unchecked((uint)e) >= 0x80000000u ? $"0x{unchecked((uint)e):X8}" : e.ToString();
    static string Verdict(int e) => e == 0 ? "" : e == 124 ? "上限" : "**異常終了**";

    /// <summary>コマンド表（第259期からは `design/COMMANDS.md`）から走らせるコマンドを組む。**行を読んでトークンに割る**（連結や文字列の再解釈をしない）。</summary>
    public static List<string> Extract(string claudeMd)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>();
        foreach (string raw in File.ReadLines(claudeMd))
        {
            string line = raw.TrimEnd('\r');
            if (!line.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            string rest = line.Substring(Prefix.Length);
            int c = rest.IndexOf(" #", StringComparison.Ordinal); if (c >= 0) rest = rest[..c];
            int g = rest.IndexOf(" > ", StringComparison.Ordinal); if (g >= 0) rest = rest[..g];
            if (rest.Contains('<')) continue;                                   // 必須の穴
            rest = Regex.Replace(rest, @"\[[^\]]*\]", " ");                     // 任意の穴は落とす
            string[] variants;
            if (rest.Contains(" / "))
            {
                var parts = rest.Split(" / ", StringSplitOptions.TrimEntries);
                var head = Tokens(parts[0]);
                string[] baseToks = head[..^1];
                variants = new[] { head[^1] }.Concat(parts.Skip(1)).Select(v => string.Join(' ', baseToks.Concat(Tokens(v)))).ToArray();
            }
            else variants = new[] { string.Join(' ', Tokens(rest)) };
            foreach (string v in variants)
            {
                var t = Tokens(v);
                if (t.Length < 2 || t[0] != "0") continue;                      // ステージ番号が固定の行だけ
                if (t[1] == "sweep") continue;                                  // 自分自身
                if (seen.Add(v)) list.Add(v);
            }
        }
        return list;
    }

    /// <summary>空白で割る。**`"..."` は1つのトークン**（引用符ごと残し、子へ渡すときに <see cref="Unquote"/> で外す）。
    /// 第232期までは引用符を字のまま子へ渡していたので、`whip log "W1 X字 …"` の第1語が `"W1` になって落ちていた。</summary>
    static string[] Tokens(string s) => Regex.Matches(s, "\"[^\"]*\"|[^ \t\"]+").Select(m => m.Value.Trim('\r'))
                                          .Where(t => t.Length > 0).ToArray();

    static string Unquote(string t) => t.Length >= 2 && t[0] == '"' && t[^1] == '"' ? t[1..^1] : t;

    static Result RunOne(string dll, string root, string cmd, int limitSec, Action<string>? keepOut = null)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(dll);
        foreach (string t in Tokens(cmd)) psi.ArgumentList.Add(Unquote(t));

        var err = new StringBuilder();
        var outSb = new StringBuilder();
        var sw = Stopwatch.StartNew();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (outSb) outSb.Append(e.Data).Append('\n'); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        int exit;
        double cpu = 0;
        if (p.WaitForExit(limitSec * 1000))
        {
            p.WaitForExit();   // 非同期の読み取りを流し切る
            exit = p.ExitCode;
            try { cpu = p.TotalProcessorTime.TotalSeconds; } catch { }
        }
        else
        {
            try { cpu = p.TotalProcessorTime.TotalSeconds; } catch { }
            try { p.Kill(entireProcessTree: true); } catch { }
            try { p.WaitForExit(5000); } catch { }
            exit = 124;
        }
        sw.Stop();
        string tail;
        lock (err)
        {
            var lines = err.ToString().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToArray();
            tail = lines.FirstOrDefault(l => l.Contains("Exception", StringComparison.Ordinal)) ?? lines.LastOrDefault() ?? "";
        }
        string fp;
        lock (outSb) { fp = exit == 124 ? "—" : OutFingerprint(outSb.ToString()); keepOut?.Invoke(outSb.ToString()); }
        return new Result(cmd, sw.Elapsed.TotalSeconds, exit, tail.Replace("|", "\\|"), cpu, fp, false);
    }

    /// <summary>標準出力の指紋（第278期・表には出さずキャッシュに控える）。所要（秒・分）を書いた行は落としてから SHA-256 の先頭 16 桁。</summary>
    static string OutFingerprint(string stdout)
    {
        var sb = new StringBuilder();
        foreach (string l in stdout.Split('\n'))
            if (!TimeLine.IsMatch(l)) sb.Append(l.TrimEnd('\r')).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }
    static readonly Regex TimeLine = new(@"所要|経過|elapsed|\d+(\.\d+)?\s*(秒|分|ms\b|s\b)", RegexOptions.Compiled);

    static string? FindRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var d = new DirectoryInfo(start);
            while (d != null)
            {
                if (File.Exists(Path.Combine(d.FullName, "CLAUDE.md")) && Directory.Exists(Path.Combine(d.FullName, "BattleSim"))) return d.FullName;
                d = d.Parent;
            }
        }
        return null;
    }

    // ---------------------------------------------------------------------------------
    // 凍結庫（第279期）—— `sweep frozen` ／ `sweep seal` ／ `sweep thaw`。封印は `design/freezer/seals.tsv`（コミットする）。
    //   封印 ＝ 凍結した時点のソースで**上限を延ばして完走させた**標準出力の指紋（所要の行を落とした SHA-256 の先頭 16 桁・sweep の照合と同じ関数）。
    //   第278期の full では 70 本とも上限 90 秒で切られていて**出力の指紋が1つも無かった**（`—`）ので、封印は凍結のときに取り直す。
    //   完走しなかった本の封印は「未完」——解凍の期に凍結時の版（封印の sha）と HEAD の両方で走らせて照合する。
    // ---------------------------------------------------------------------------------
    sealed record Seal(string Cmd, string Group, int Phase, string Sha, string Date, int Limit, int Exit, double Sec, string Fp, int Lines);

    static string SealPath(string root) => Path.Combine(root, "design", "freezer", "seals.tsv");
    static string Slug(string cmd) => Regex.Replace(cmd.StartsWith("0 ", StringComparison.Ordinal) ? cmd[2..] : cmd, @"[^\w]+", "_").Trim('_');

    static Dictionary<string, Seal> LoadSeals(string root)
    {
        var d = new Dictionary<string, Seal>(StringComparer.Ordinal);
        string p = SealPath(root);
        if (!File.Exists(p)) return d;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (string line in File.ReadAllLines(p))
        {
            if (line.StartsWith('#') || line.Length == 0) continue;
            var f = line.Split('\t');
            if (f.Length < 10) continue;
            d[f[0]] = new Seal(f[0], f[1], int.Parse(f[2]), f[3], f[4], int.Parse(f[5]), int.Parse(f[6]), double.Parse(f[7], inv), f[8], int.Parse(f[9]));
        }
        return d;
    }

    static void SaveSeals(string root, Dictionary<string, Seal> seals)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var lines = new List<string> { "# sweep の凍結庫の封印（第279期・`sweep seal` が書く。手で編集しない）: コマンド\t群\t期\t封印の版\t日付\t上限\texit\t秒\t出力の指紋\t出力の行数" };
        foreach (var (cmd, _, _) in Frozen)
            if (seals.TryGetValue(cmd, out var s))
                lines.Add(string.Join('\t', s.Cmd, s.Group, s.Phase, s.Sha, s.Date, s.Limit, s.Exit, s.Sec.ToString("F1", inv), s.Fp, s.Lines));
        Directory.CreateDirectory(Path.GetDirectoryName(SealPath(root))!);
        File.WriteAllText(SealPath(root), string.Join('\n', lines) + "\n");
    }

    /// <summary>封印の版: HEAD の短い sha。BattleCore ／ BattleSim に未コミットの変更があれば `+作業中` を付ける。</summary>
    static string HeadSha(string root)
    {
        string Git(params string[] a)
        {
            var psi = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, UseShellExecute = false };
            foreach (string x in a) psi.ArgumentList.Add(x);
            using var p = Process.Start(psi)!;
            string o = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return o;
        }
        try
        {
            string sha = Git("rev-parse", "--short", "HEAD");
            string dirty = Git("status", "--porcelain", "--", "BattleCore", "BattleSim");
            return dirty.Length > 0 ? sha + "+作業中" : sha;
        }
        catch { return "?"; }
    }

    static void Freezer(string[] rest)
    {
        string root = FindRoot() ?? throw new InvalidOperationException("リポジトリの中で回すこと");
        string dll = typeof(SweepDiag).Assembly.Location;
        string verb = rest[0];
        int limit = verb == "thaw" ? 7200 : 600, jobs = 1;
        var filters = new List<string>();
        foreach (string a in rest.Skip(1))
        {
            if (a.StartsWith("j=", StringComparison.Ordinal) && int.TryParse(a[2..], out int j) && j > 0) jobs = j;
            else if (int.TryParse(a, out int n) && n > 0) limit = n;
            else filters.AddRange(a.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        var table = Extract(Path.Combine(root, "design", "COMMANDS.md"));
        var seals = LoadSeals(root);
        var pick = Frozen.Where(f => filters.Count == 0 || filters.Any(x => x == f.Group || f.Cmd.Contains(x, StringComparison.Ordinal))).ToList();

        if (verb == "frozen")
        {
            Console.WriteLine($"# sweep の凍結庫（第279期）—— {Frozen.Length} 本（G2 {Frozen.Count(f => f.Group == "G2")} ／ G3 {Frozen.Count(f => f.Group == "G3")} ／ G4 {Frozen.Count(f => f.Group == "G4")}）・封印 `design/freezer/seals.tsv`");
            Console.WriteLine();
            Console.WriteLine("| コマンド | 群 | 期 | コマンド表 | 封印の版 | 封印 |");
            Console.WriteLine("|---|---|--:|:-:|---|---|");
            foreach (var f in pick)
            {
                string st = seals.TryGetValue(f.Cmd, out var s) ? (s.Exit == 0 ? $"`{s.Fp}`（{s.Sec:F0} 秒・{s.Lines} 行）" : s.Exit == 124 ? $"未完（{s.Limit} 秒で切った）" : $"異常終了 {ExitText(s.Exit)}") : "—";
                Console.WriteLine($"| `{f.Cmd}` | {f.Group} | {f.Phase} | {(table.Contains(f.Cmd) ? "○" : "**×**")} | {(s?.Sha ?? "—")} | {st} |");
            }
            Console.WriteLine();
            Console.WriteLine($"audit が正の docs 生成器（sweep から外した）: {string.Join(" ／ ", DocsByAudit.Select(c => $"`{c}`"))}");
            Console.WriteLine($"煙試験に置き換えた本体: {string.Join(" ／ ", SmokeReplaced.Select(s => $"`{s.Full}` → `{s.Smoke}`"))}");
            return;
        }

        // 子の標準出力は端末のコードページで符号化・復号される。cp932 では cp932 に無い字（−・〜 など）が `?` に化けて指紋が変わるので、封印も解凍も UTF-8 で回す。
        if (Console.OutputEncoding.CodePage != 65001)
        {
            Console.WriteLine("**端末のコードページが UTF-8 ではない。`chcp 65001` の後で回すこと**（封印と照合の指紋が字化けで食い違う）。");
            Environment.ExitCode = 2;
            return;
        }
        string sha = HeadSha(root), date = DateTime.Now.ToString("yyyy-MM-dd");
        Console.WriteLine(verb == "seal"
            ? $"# sweep seal（第279期）—— 凍結した {pick.Count} 本を上限 {limit} 秒・並列度 {jobs} で走らせて封印する（版 {sha}）"
            : $"# sweep thaw（第279期）—— 凍結した {pick.Count} 本を上限 {limit} 秒・並列度 {jobs} で走らせて封印と照合する（版 {sha}・出力は `.sweep/thaw/`）");
        Console.WriteLine();
        var res = new Result?[pick.Count];
        var outs = new string?[pick.Count];
        Parallel.For(0, pick.Count, new ParallelOptions { MaxDegreeOfParallelism = jobs }, i =>
        {
            Console.Error.WriteLine($"[{i + 1}/{pick.Count}] {pick[i].Cmd}");
            res[i] = RunOne(dll, root, pick[i].Cmd, limit, o => outs[i] = o);
            Console.Error.WriteLine($"[{i + 1}/{pick.Count}] {pick[i].Cmd} → {ExitText(res[i]!.Exit)} {res[i]!.Sec:F0}s");
            if (verb == "seal")
                lock (seals)
                {
                    // 完走した封印は、切れた・落ちた回で上書きしない。
                    var r = res[i]!;
                    if (r.Exit == 0 || !seals.TryGetValue(pick[i].Cmd, out var old) || old.Exit != 0)
                    {
                        int nLines = outs[i] is { } t ? t.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length : 0;
                        seals[pick[i].Cmd] = new Seal(pick[i].Cmd, pick[i].Group, pick[i].Phase, sha, date, limit, r.Exit, r.Sec, r.Out, nLines);
                        if (r.Exit == 0)
                        {
                            Directory.CreateDirectory(Path.Combine(root, "design", "freezer", "out"));
                            File.WriteAllText(Path.Combine(root, "design", "freezer", "out", Slug(pick[i].Cmd) + ".txt"), outs[i] ?? "");
                        }
                    }
                    SaveSeals(root, seals);
                }
            else
            {
                Directory.CreateDirectory(Path.Combine(root, ".sweep", "thaw"));
                File.WriteAllText(Path.Combine(root, ".sweep", "thaw", Slug(pick[i].Cmd) + ".txt"), outs[i] ?? "");
            }
        });

        Console.WriteLine("| コマンド | 群 | 秒 | exit | 指紋 | 封印 | 照合 |");
        Console.WriteLine("|---|---|--:|--:|---|---|---|");
        int mismatch = 0;
        for (int i = 0; i < pick.Count; i++)
        {
            var r = res[i]!;
            seals.TryGetValue(pick[i].Cmd, out var s);
            string verdict = verb == "seal" ? (r.Exit == 0 ? "封印した" : r.Exit == 124 ? "未完" : "**異常終了**")
                : s is null || s.Exit != 0 ? "封印なし（凍結時の版 " + (s?.Sha ?? "?") + " でも走らせて比べる）"
                : r.Exit != 0 ? "**完走せず**"
                : r.Out == s.Fp ? "一致" : "**不一致**（git bisect で原因のコミットを探す）";
            if (verdict.StartsWith("**", StringComparison.Ordinal)) mismatch++;
            Console.WriteLine($"| `{r.Cmd}` | {pick[i].Group} | {r.Sec:F1} | {ExitText(r.Exit)} | `{r.Out}` | {(s is { Exit: 0 } ? $"`{s.Fp}`" : "—")} | {verdict} |");
        }
        Console.WriteLine();
        Console.WriteLine(mismatch == 0 ? "異常なし。" : $"**要確認 {mismatch} 本。**");
        Environment.ExitCode = mismatch == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------------------------
    // (A) 鍵 —— 本ごとの入力のハッシュ。
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// 本ごとの鍵（第278期）。**3つの層の連結のハッシュ**:
    /// <list type="number">
    /// <item><b>エンジン層</b>: <c>BattleCore/</c> の全 <c>.cs</c> ／ <c>.csproj</c>。1文字でも動けば全本の鍵が動く。</item>
    /// <item><b>器具層</b>: <c>BattleSim/</c> の「共通ファイル」全部（列0の型宣言に <c>*Diag</c> 以外を持つファイル。<c>Program.cs</c> は振り分けの行 <c>if (focusId == "…")</c> と注釈行を除いて読む）
    ///   ＋ その本の系統の振り分けが参照する <c>*Diag</c> クラスのファイルを、<c>*Diag</c> の名前の参照で推移的に辿った集合。</item>
    /// <item><b>データ層</b>: 辿った集合のどれかが <c>File.</c> ／ <c>Directory.</c> を使うときだけ、<c>docs/</c> ／ <c>design/</c> ／ リポジトリ直下の <c>.md</c> と、
    ///   <c>BattleCore/</c> ／ <c>BattleSim/</c> のソースを<b>生のまま</b>（注釈も振り分け行も）足す。</item>
    /// </list>
    /// 系統が振り分けから引けない本は、BattleSim の全ファイル ＋ データ層を鍵にする（安全側）。改行は LF に揃えてから読む（CRLF の揺れで鍵が動かない）。
    /// </summary>
    internal sealed class KeyBuilder
    {
        readonly string _root;
        readonly Func<string, string> _read;
        readonly string _engine, _common, _allSim;
        /// <summary>データ層の4種（docs ／ design ／ 直下の文書 ／ BattleSim の生ソース）。器具のソースにその種類のパスが書かれているときだけ鍵に足す。</summary>
        readonly string[] _dataKinds = new string[4];
        static readonly Regex[] KindPattern =
        {
            new(@"""docs""|docs/", RegexOptions.Compiled),
            new(@"""design""|design/", RegexOptions.Compiled),
            new(@"CLAUDE\.md|README|CONTRIBUTING", RegexOptions.Compiled),
            new(@"""BattleSim""|BattleSim/|Program\.cs|""Modes""", RegexOptions.Compiled),
        };
        readonly Dictionary<string, int> _kindMask = new();               // ファイル → 書かれている種類（ビット）
        readonly Dictionary<string, string> _fileHash = new();
        readonly Dictionary<string, List<string>> _diagFiles = new();     // Diag クラス名 → 宣言しているファイル
        readonly Dictionary<string, HashSet<string>> _refs = new();        // ファイル → 参照している Diag クラス名
        readonly HashSet<string> _io = new();                              // File. / Directory. を使うファイル
        readonly Dictionary<string, HashSet<string>> _familyDiags = new();  // 系統 → 振り分けが参照する Diag
        readonly Dictionary<string, (string Key, int Data, int Files)> _famKey = new();
        public int UnresolvedFamilies => _famKey.Values.Count(v => v.Files < 0);

        static readonly Regex DiagRef = new(@"\b([A-Z]\w*Diag)\b", RegexOptions.Compiled);
        static readonly Regex TopDecl = new(@"^(?:public |internal |file )?(?:static |sealed |abstract |readonly |partial |record )*(?:class|record|struct|enum|interface) (\w+)", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex Dispatch = new(@"focusId == ""([\w.-]+)""", RegexOptions.Compiled);

        /// <param name="overrides">自己検査用: 相対パス → 中身（ディスクの代わりに読む）。null ならディスクだけ。</param>
        public KeyBuilder(string root, IReadOnlyDictionary<string, string>? overrides)
        {
            _root = root;
            _read = rel => overrides is not null && overrides.TryGetValue(rel, out var s) ? s : File.ReadAllText(Path.Combine(root, rel)).Replace("\r\n", "\n");
            var core = Files("BattleCore", "*.cs").Concat(Files("BattleCore", "*.csproj")).ToList();
            var sim = Files("BattleSim", "*.cs").Concat(Files("BattleSim", "*.csproj")).ToList();
            var docs = Files("docs", "*").ToList();
            var design = Files("design", "*.md").ToList();
            var rootMd = Directory.GetFiles(root, "*.md").Select(p => Path.GetFileName(p)).OrderBy(p => p, StringComparer.Ordinal).ToList();

            _engine = Hash(core.Select(f => f + "\0" + _read(f)));
            var commonFiles = new List<string>();
            foreach (string f in sim)
            {
                string text = _read(f);
                if (f.EndsWith(".csproj")) { commonFiles.Add(f); _fileHash[f] = Hash(new[] { f + "\0" + text }); continue; }
                var decl = TopDecl.Matches(text).Select(m => m.Groups[1].Value).ToList();
                foreach (string d in decl.Where(d => d.EndsWith("Diag", StringComparison.Ordinal)))
                    (_diagFiles.TryGetValue(d, out var l) ? l : _diagFiles[d] = new()).Add(f);
                // 参照・I/O・パスの種類は**注釈を落としてから**数える（各ファイルの冒頭の「指示書は design/…」「`docs/balance.md` を…」に当たらないように）。
                // 鍵そのもの（`_fileHash`）は注釈ごと生で取る（文字列の中の `//` で行を切り落とす危険を鍵に持ち込まない）。
                string code = StripComments(text);
                _refs[f] = DiagRef.Matches(code).Select(m => m.Groups[1].Value).ToHashSet();
                if (code.Contains("File.", StringComparison.Ordinal) || code.Contains("Directory.", StringComparison.Ordinal)) _io.Add(f);
                int mask = 0;
                for (int b = 0; b < KindPattern.Length; b++) if (KindPattern[b].IsMatch(code)) mask |= 1 << b;
                _kindMask[f] = mask;
                bool isMode = decl.Count > 0 && decl.All(d => d.EndsWith("Diag", StringComparison.Ordinal));
                if (!isMode) commonFiles.Add(f);
                _fileHash[f] = Hash(new[] { f + "\0" + (f == "BattleSim/Program.cs" ? NormalizeProgram(text) : text) });
            }
            _common = Hash(commonFiles.Select(f => _fileHash[f]));
            _allSim = Hash(sim.Select(f => f + "\0" + _read(f)));
            // docs/ は生成物なので、所要時間の行（`roster_audit.md` の「所要 … 秒」など）を落としてから取る——再生成のたびに鍵が動かないように。
            _dataKinds[0] = Hash(docs.Select(f => f + "\0" + string.Join('\n', _read(f).Split('\n').Where(l => !TimeLine.IsMatch(l)))));
            _dataKinds[1] = Hash(design.Select(f => f + "\0" + _read(f)));
            _dataKinds[2] = Hash(rootMd.Select(f => f + "\0" + _read(f)));
            _dataKinds[3] = _allSim;

            // 系統 → 振り分けのブロック（その `focusId == "x"` の行から次の `focusId ==` の行の手前まで）が参照する Diag。
            var lines = _read("BattleSim/Program.cs").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var ms = Dispatch.Matches(lines[i]);
                if (ms.Count == 0) continue;
                int end = i + 1;
                while (end < lines.Length && !lines[end].Contains("focusId ==", StringComparison.Ordinal)) end++;
                var diags = new HashSet<string>();
                for (int k = i; k < end; k++) foreach (Match m in DiagRef.Matches(StripComments(lines[k]))) diags.Add(m.Groups[1].Value);
                foreach (Match m in ms)
                    (_familyDiags.TryGetValue(m.Groups[1].Value, out var s) ? s : _familyDiags[m.Groups[1].Value] = new()).UnionWith(diags);
            }
        }

        IEnumerable<string> Files(string dir, string pattern)
        {
            string full = Path.Combine(_root, dir);
            if (!Directory.Exists(full)) return Array.Empty<string>();
            return Directory.GetFiles(full, pattern, SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(_root, p).Replace('\\', '/'))
                .Where(p => !p.Contains("/bin/") && !p.Contains("/obj/"))
                .OrderBy(p => p, StringComparer.Ordinal);
        }

        /// <summary>行注釈（行頭か空白の後の <c>//</c> から行末まで）を落とす。解析にだけ使う（鍵には使わない）。</summary>
        static string StripComments(string text) => LineComment.Replace(text, "");
        static readonly Regex LineComment = new(@"(^|(?<=\s))//.*$", RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>Program.cs から「振り分けの行」と注釈行を落とす（新しいモードを足しても他の本の鍵が動かないように）。</summary>
        static string NormalizeProgram(string text)
            => string.Join('\n', text.Split('\n').Where(l =>
            {
                string t = l.TrimStart();
                return !t.StartsWith("//", StringComparison.Ordinal) && !t.StartsWith("if (focusId == \"", StringComparison.Ordinal);
            }));

        static string Hash(IEnumerable<string> parts)
        {
            using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (string p in parts) { h.AppendData(Encoding.UTF8.GetBytes(p)); h.AppendData(new byte[] { 0xFF }); }
            return Convert.ToHexString(h.GetHashAndReset())[..20];
        }

        /// <summary>系統の器具ファイルの集合（推移閉包）。引けなければ null。</summary>
        public HashSet<string>? Closure(string family)
        {
            if (!_familyDiags.TryGetValue(family, out var seeds) || seeds.Count == 0) return null;
            var files = new HashSet<string>();
            var queue = new Queue<string>(seeds);
            var seen = new HashSet<string>();
            while (queue.Count > 0)
            {
                string d = queue.Dequeue();
                if (!seen.Add(d) || !_diagFiles.TryGetValue(d, out var fs)) continue;
                foreach (string f in fs)
                    if (files.Add(f)) foreach (string r in _refs[f]) queue.Enqueue(r);
            }
            return files.Count == 0 ? null : files;
        }

        public string KeyOf(string cmd, int limit)
        {
            string fam = FamilyOf(cmd);
            if (!_famKey.TryGetValue(fam, out var fk))
            {
                var closure = Closure(fam);
                if (closure is null) fk = (Hash(new[] { _engine, _allSim }.Concat(_dataKinds)), 0b1111, -1);
                else
                {
                    // 読むファイルの種類: 閉包のどれかが File. ／ Directory. を使うとき、閉包に書かれているパスの種類（docs ／ design ／ 直下の文書 ／ BattleSim）。
                    int data = closure.Any(_io.Contains) ? closure.Aggregate(0, (m, f) => m | _kindMask[f]) : 0;
                    var parts = new List<string> { _engine, _common };
                    parts.AddRange(closure.OrderBy(f => f, StringComparer.Ordinal).Select(f => _fileHash[f]));
                    for (int b = 0; b < 4; b++) if ((data & (1 << b)) != 0) parts.Add(_dataKinds[b]);
                    fk = (Hash(parts), data, closure.Count);
                }
                _famKey[fam] = fk;
            }
            return Hash(new[] { fk.Key, cmd, limit.ToString() });
        }

        /// <summary>鍵に含むデータ層の種類（ビット: 1 docs ／ 2 design ／ 4 直下の文書 ／ 8 BattleSim の生ソース）。</summary>
        public int DataKinds(string cmd) { KeyOf(cmd, DefaultLimitSec); return _famKey[FamilyOf(cmd)].Data; }
        public int ClosureSize(string cmd) { KeyOf(cmd, DefaultLimitSec); return _famKey[FamilyOf(cmd)].Files; }
    }

    // ---------------------------------------------------------------------------------
    // キャッシュ —— `.sweep/cache.tsv`（リポジトリ直下・`.gitignore` 済み・機械ごと）。
    // 1行 ＝ 鍵 \t コマンド \t 上限 \t exit \t 秒 \t CPU 秒 \t 出力の指紋 \t 最後の例外行 \t 単独の exit \t 単独の秒 \t 単独の CPU 秒。コマンドごとに最新の1行だけ持つ。
    // 「単独の」3列は並べ方用の履歴で、**単独（または直列）で走った回だけ**更新する（並列の回の所要は膨らむので、それで見積もると毎回保守的に寄っていく）。
    // ---------------------------------------------------------------------------------
    sealed class SweepCache
    {
        readonly string _path;
        readonly Dictionary<string, (string Key, int Limit, Result R, Result Sched)> _rows = new(StringComparer.Ordinal);
        SweepCache(string path) => _path = path;

        public static SweepCache Load(string root)
        {
            var c = new SweepCache(Path.Combine(root, ".sweep", "cache.tsv"));
            if (File.Exists(c._path))
                foreach (string line in File.ReadAllLines(c._path))
                {
                    if (line.StartsWith('#')) continue;
                    var f = line.Split('\t');
                    if (f.Length < 8) continue;
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    var res = new Result(f[1], double.Parse(f[4], inv), int.Parse(f[3]), f[7], double.Parse(f[5], inv), f[6], false);
                    var sched = f.Length >= 11 ? res with { Exit = int.Parse(f[8]), Sec = double.Parse(f[9], inv), Cpu = double.Parse(f[10], inv) } : res;
                    c._rows[f[1]] = (f[0], int.Parse(f[2]), res, sched);
                }
            return c;
        }

        /// <summary>鍵が一致した前回の結果。<b>上限で切られた結果も返す</b>（同じ入力なら同じ所で切られる——判定は「上限」のまま）。</summary>
        public bool TryHit(string cmd, string key, out Result r)
        {
            lock (_rows)
                if (_rows.TryGetValue(cmd, out var v) && v.Key == key) { r = v.R; return true; }
            r = null!; return false;
        }

        /// <summary>並べ方用の履歴（単独で走った回の結果・鍵に依らない）。</summary>
        public bool TrySched(string cmd, out Result r)
        {
            lock (_rows)
                if (_rows.TryGetValue(cmd, out var v)) { r = v.Sched; return true; }
            r = null!; return false;
        }

        /// <summary>1本終わるごとに書き出す（途中で止めても、そこまでの結果が残る）。</summary>
        public void Put(string cmd, string key, int limit, Result r, bool alone)
        {
            lock (_rows)
            {
                var sched = alone || !_rows.TryGetValue(cmd, out var old) ? r : old.Sched;
                _rows[cmd] = (key, limit, r, sched);
                Save();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var lines = new List<string> { "# sweep cache v2（第278期）: 鍵\tコマンド\t上限\texit\t秒\tCPU秒\t出力の指紋\t最後の例外行\t単独のexit\t単独の秒\t単独のCPU秒" };
            lock (_rows)
            {
                foreach (var (cmd, v) in _rows.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    lines.Add(string.Join('\t', v.Key, cmd, v.Limit, v.R.Exit, v.R.Sec.ToString("F1", inv), v.R.Cpu.ToString("F1", inv), v.R.Out, v.R.Tail.Replace('\t', ' '),
                                          v.Sched.Exit, v.Sched.Sec.ToString("F1", inv), v.Sched.Cpu.ToString("F1", inv)));
                File.WriteAllLines(_path, lines);
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // 自己検査（`sweep check`・第278期）。ディスクのファイルは書き換えない——中身を差し替えた版の鍵を作って比べる。
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        string root = FindRoot() ?? throw new InvalidOperationException("リポジトリの中で回すこと");
        var cmds = Extract(Path.Combine(root, "design", "COMMANDS.md"));
        var kb = new KeyBuilder(root, null);
        string[] Keys(KeyBuilder k) => cmds.Select(c => k.KeyOf(c, DefaultLimitSec)).ToArray();
        var k0 = Keys(kb);
        int bad = 0;
        void Ok(string what, bool ok) { Console.WriteLine($"{(ok ? "○" : "×")} {what}"); if (!ok) bad++; }
        string Rd(string rel) => File.ReadAllText(Path.Combine(root, rel)).Replace("\r\n", "\n");
        int Moved(Dictionary<string, string> ov, out string[] k1) { k1 = Keys(new KeyBuilder(root, ov)); var kk = k1; return k0.Where((k, i) => k != kk[i]).Count(); }

        var fams = cmds.Select(FamilyOf).Distinct().ToList();
        var unresolved = fams.Where(f => kb.Closure(f) is null).ToList();
        int designCmds = cmds.Count(c => (kb.DataKinds(c) & 2) != 0), simCmds = cmds.Count(c => (kb.DataKinds(c) & 8) != 0), docsCmds = cmds.Count(c => (kb.DataKinds(c) & 1) != 0);
        Console.WriteLine($"# sweep の自己検査（第278期）—— {cmds.Count} 本 ／ 系統 {fams.Count}（振り分けから引けない系統 {unresolved.Count}: {string.Join(" ", unresolved.Take(20))}）／ 鍵にデータ層を含む本: docs {docsCmds} ／ design {designCmds} ／ BattleSim の生ソース {simCmds}");
        Console.WriteLine();

        Ok("(a) 同じ入力なら鍵は同じ（2回作って全本一致）", Keys(new KeyBuilder(root, null)).SequenceEqual(k0));
        Ok("(b) BattleCore に1文字足すと全本の鍵が動く（再実行に戻る）",
           Moved(new() { ["BattleCore/Traits.cs"] = Rd("BattleCore/Traits.cs") + " " }, out _) == cmds.Count);
        int m = Moved(new() { ["BattleSim/Modes/Nomi277.cs"] = Rd("BattleSim/Modes/Nomi277.cs") + "// x\n" }, out var k2);
        var movedFams = cmds.Where((c, i) => k0[i] != k2[i]).Select(FamilyOf).Distinct().ToList();
        int expectC = cmds.Count(c => (kb.DataKinds(c) & 8) != 0 || (kb.Closure(FamilyOf(c))?.Contains("BattleSim/Modes/Nomi277.cs") ?? true));
        Ok($"(c) 器具ファイル1本（Nomi277.cs）を変えると動くのは、それを辿る系統と BattleSim の生ソースを読む本だけ（{m} 本 ＝ 見込み {expectC} 本・系統 {movedFams.Count}）",
           m == expectC && m < cmds.Count && cmds.Where(c => FamilyOf(c) == "nomi277").All(c => k0[cmds.IndexOf(c)] != k2[cmds.IndexOf(c)]));
        int md = Moved(new() { ["design/PHASE277_NOMI_REBIRTH.md"] = Rd("design/PHASE277_NOMI_REBIRTH.md") + "x" }, out var k3);
        Ok($"(d) design/ の文書を1文字変えると動くのは design を読む本だけ（{md} 本 ＝ {designCmds} 本）", md == designCmds && md < cmds.Count);
        string prog = Rd("BattleSim/Program.cs");
        int i0 = prog.IndexOf("if (focusId == \"nomi277\")", StringComparison.Ordinal);
        string prog2 = prog.Insert(i0, "// 第999期\nif (focusId == \"zzz999\") { Nomi277Diag.Run(args, stageIndex); return; }   // 第999期\n");
        int mp = Moved(new() { ["BattleSim/Program.cs"] = prog2 }, out _);
        Ok($"(e) Program.cs に振り分けを1行足しても、動くのは BattleSim の生ソースを読む本だけ（{mp} 本 ＝ {simCmds} 本）", mp == simCmds);
        int mc = Moved(new() { ["BattleSim/Common.cs"] = Rd("BattleSim/Common.cs") + " " }, out _);
        Ok($"(f) 共通ファイル（Common.cs）を変えると振り分けから引けた本も含めて全本の鍵が動く（{mc} 本）", mc == cmds.Count);
        Ok("(g) `sweep fast` の系統はすべてコマンド表にある", FastFamilies.All(f => fams.Contains(f)));
        // 第279期: 凍結庫。凍結した本・外した本・煙試験の形はコマンド表に残っている（凍結は「消す」ではない）。
        var table = cmds;
        Ok($"(h) 凍結した {Frozen.Length} 本はすべてコマンド表にある（重複なし）", Frozen.All(f => table.Contains(f.Cmd)) && Frozen.Select(f => f.Cmd).Distinct().Count() == Frozen.Length);
        Ok("(i) audit が正の生成器 ／ 煙試験の本体と煙試験の形はすべてコマンド表にある", DocsByAudit.All(table.Contains) && SmokeReplaced.All(x => table.Contains(x.Full) && table.Contains(x.Smoke)));
        Ok("(j) 凍結した本に `sweep fast` の系統は無い", Frozen.All(f => !FastFamilies.Contains(FamilyOf(f.Cmd))));
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "自己検査: すべて ○" : $"自己検査: × が {bad} 件");
        Environment.ExitCode = bad == 0 ? 0 : 1;
    }
}
