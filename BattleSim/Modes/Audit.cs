using BattleCore;
using static Common;

// =====================================================================================
// audit モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "audit")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 audit
// =====================================================================================

static class AuditDiag
{
// audit モード: docs/ の生成物が現行の編成数と整合しているかを判定する（第55期）。
//
// 生成物は「作った時点の編成数」で固まるので、CompareBuilds() に行を足すたびに
// 測り直さないと静かに腐る。第54期に docs/chain.md が 35 行（当時の現行は 56 行）と
// 判明したのが発端で、20期以上前の編成表を根拠に判断しかけた。
//
// **戦闘を1回も回さない。docs/ に何も書かない**（生成物を増やすと腐るものが増える）。
// 判定は「現行の編成名がその生成物に1つ残らず現れるか」の1本だけ——行数を直に比べると
// 表の書式を変えるたびに閾値が嘘になるが、名前の有無は書式に依存しない。
//
//     dotnet run --project BattleSim -c Release 0 audit
public static void Run(string[] args, int stageIndex)
{
    string[] names = CompareBuilds().Select(b => b.Name).ToArray();

    // リポジトリ直下から実行するのが既定だが、どこから叩かれても docs/ を見つけられるようにする。
    string? root = Directory.GetCurrentDirectory();
    while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
        root = Path.GetDirectoryName(root);
    if (root == null)
    {
        Console.WriteLine("docs/ が見つからない（リポジトリの外から実行している）。");
        return;
    }
    string docs = Path.Combine(root, "docs");

    // Depends: 行数が編成数に依存するか。Sections: `## ` の見出しが編成名か。
    var targets = new (string File, string Cmd, bool Depends, bool Sections)[]
    {
        ("balance.md",  "compare", true,  false),
        ("quality.md",  "compare quality", true, false),   // 第126期 段1
        ("units.md",    "dump",    false, false),
        ("chain.md",    "chain",   true,  false),
        ("ablation.md", "ablate",  true,  true),
        ("pulse.md",    "pulse",   true,  true),
        ("engage.md",   "engage",  true,  false),
        ("layout.md",   "layout",  true,  true),
        ("reseat.md",   "reseat",  true,  true),
        ("elite.md",    "elite",   true,  false),   // 第284期
    };

    Console.WriteLine($"現行の編成数: {names.Length}");
    Console.WriteLine();
    Console.WriteLine("| ファイル | 生成コマンド | 行数 | 編成数依存 | 現れた編成 | 節(編成/他) | 判定 |");
    Console.WriteLine("|---|---|--:|:-:|--:|--:|:-:|");

    var stale = new List<(string File, string[] Missing)>();
    foreach ((string file, string cmd, bool depends, bool sections) in targets)
    {
        string path = Path.Combine(docs, file);
        if (!File.Exists(path))
        {
            Console.WriteLine($"| `{file}` | `{cmd}` | — | {(depends ? "○" : "×")} | — | — | **欠落** |");
            stale.Add((file, names));
            continue;
        }

        string[] lines = File.ReadAllLines(path);
        string text = string.Join("\n", lines);
        string[] missing = depends ? names.Where(n => !text.Contains(n)).ToArray() : Array.Empty<string>();

        string secCol = "—";
        if (sections)
        {
            var heads = lines.Where(l => l.StartsWith("## ")).Select(l => l.Substring(3).Trim()).ToArray();
            // ablate は見出しに「（フル編成 43.9%）」を足すので、完全一致では引けない。
            int known = heads.Count(h => names.Any(n => h.StartsWith(n)));
            secCol = $"{known}/{heads.Length - known}";
        }

        string hit = depends ? $"{names.Length - missing.Length}/{names.Length}" : "—";
        Console.WriteLine($"| `{file}` | `{cmd}` | {lines.Length} | {(depends ? "○" : "×")} | {hit} | {secCol} "
                          + $"| {(missing.Length == 0 ? "OK" : "**ずれ**")} |");
        if (missing.Length > 0) stale.Add((file, missing));
    }

    Console.WriteLine();
    Console.WriteLine($"ずれているファイル {stale.Count} 件");
    foreach ((string file, string[] missing) in stale)
    {
        Console.WriteLine();
        Console.WriteLine($"## {file} — {missing.Length} 編成が現れない");
        foreach (string n in missing) Console.WriteLine($"- {n}");
    }

    // 第259期: `CLAUDE.md` の門。**500 行 / 50 KB を超えたら ×**（毎セッション丸ごと載るファイルなので、
    // 履歴を書き足すと読む側の注意が薄まる。履歴は `design/` へ、本文は索引で引く）。
    // 警告（× にしない）: 「n本目」「第nn期に…足した」の文——数は生成物で引くものなので `CLAUDE.md` に書かない（R151）。
    const int MaxLines = 500, MaxBytes = 50 * 1024, WarnHistoryRefs = 5;
    string claudePath = Path.Combine(root, "CLAUDE.md");
    if (File.Exists(claudePath))
    {
        string[] cl = File.ReadAllLines(claudePath);
        long bytes = new FileInfo(claudePath).Length;
        int refs = cl.Count(l => l.Contains("本目", StringComparison.Ordinal)
                              || System.Text.RegularExpressions.Regex.IsMatch(l, @"第\d+期に.*足した"));
        bool over = cl.Length > MaxLines || bytes > MaxBytes;
        Console.WriteLine();
        Console.WriteLine("## `CLAUDE.md` の門（第259期）");
        Console.WriteLine();
        Console.WriteLine($"| 項目 | 値 | 上限 | 判定 |");
        Console.WriteLine($"|---|--:|--:|:-:|");
        Console.WriteLine($"| 行数 | {cl.Length} | {MaxLines} | {(cl.Length > MaxLines ? "**×**" : "OK")} |");
        Console.WriteLine($"| バイト | {bytes:N0} | {MaxBytes:N0} | {(bytes > MaxBytes ? "**×**" : "OK")} |");
        Console.WriteLine($"| 「本目」／「第nn期に…足した」の行 | {refs} | 警告 {WarnHistoryRefs} | {(refs >= WarnHistoryRefs ? "**警告**（履歴は design/ へ）" : "OK")} |");
        if (over)
        {
            Console.WriteLine();
            Console.WriteLine("**× `CLAUDE.md` が上限を超えている。** 履歴は `design/HISTORY_PHASES.md` / `design/ENGINE_HOOKS.md` / `design/PHASE_INDEX.md` へ、新しいコマンドは `design/COMMANDS.md` へ。");
            Environment.ExitCode = 1;
        }
    }
    return;
}
}
