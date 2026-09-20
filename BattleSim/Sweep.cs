using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

// =====================================================================================
// sweep モード（第141期） —— 全診断の exit 検査
//
// **第140期の走査（Python の一時スクリプト・CRLF で1度壊れた）をリポジトリの器具にしたもの。**
// `CLAUDE.md` のコマンド表を**自分で読んで**「引数の穴が無い」コマンドを組み、1本ずつ上限つきで
// 子プロセスとして走らせ、exit code と所要を記録する。**戦闘はすべて子プロセスの中**で、
// このモード自身は盤面に触らない。**標準出力は捨てる**（`> docs/*.md` の向き先も含めて、ファイルは1つも書かない）。
//
//   合格 = **異常終了（0 でも上限でもない exit）が 0 本**。
//   上限（124）は失敗に数えない——ただし一覧に出し、**文書上「長い」と分かっている本（`KnownLong`）と
//   新しく上限に当たった本を分けて出す**。
//
// **運用: 毎期は回さない（80 分前後）。`UnitCatalog.All` ／ `Retired` ／ `Presets` に触る期の受け入れ条件に入れる。**
//
// CRLF の穴（第140期）: 一覧は**行を読んでトークンに割ってから `ArgumentList` で渡す**ので、
// 行末の `\r` が引数の末尾に紛れ込む経路が無い（第123期「一覧は連結で組む」）。
//
//     dotnet run --project BattleSim -c Release 0 sweep                  # 全部（上限 90 秒）
//     dotnet run --project BattleSim -c Release 0 sweep 120              # 上限を秒で
//     dotnet run --project BattleSim -c Release 0 sweep 120 suture2,gather   # 部分一致で絞る（カンマ区切り）
//     dotnet run --project BattleSim -c Release 0 sweep list             # 一覧だけ出す（戦闘0回・子プロセス0本）
// =====================================================================================
static class SweepDiag
{
    const int DefaultLimitSec = 90;

    /// <summary>
    /// **文書上「長い」と書いてある診断**（第140期 §5 で 90 秒の上限に当たった 10 本）。
    /// 上限に当たっても「既知」として出す。**ここに無い本が上限に当たったら「新規」**——遅くなった合図。
    /// </summary>
    static readonly string[] KnownLong =
    {
        "0 spend alt", "0 creak alt", "0 draft alt", "0 draft2", "0 draft2 alt",
        "0 slope", "0 slope alt", "0 pairs", "0 pairs2", "0 hold2 seats",
        // 第141期の初回の走行で新しく上限に当たった 11 本のうち、文書か実測に所要が書いてある 6 本。
        // `reseat` 93 秒（CONTRIBUTING.md）／`seats2` 約 7 分／`draft` 169 秒・`draft3` 288 秒・`draft3 alt` 239 秒
        // （第141期 §2-2 の実測）／`wall run` 3 分。残る 5 本（gradient / bridge / wave / divert / spend）は
        // 所要の記録が無い（`bridge` は「30 秒前後」と書いてある）ので「新規」のまま残す——次の走行で再現するかを見る。
        "0 reseat", "0 seats2", "0 draft", "0 draft3", "0 draft3 alt", "0 wall run",
        // 第163期。`stage cross` は 12 列 × 版2 × 帯 12 本 ＝ 35 点で **125 秒**（報告書 §1 の実測）。
        "0 stage cross",
        // 第164期。`stage catalog` は `stage cross` と同じ 35 点を回すので **150 秒**（報告書 §1 の実測）。
        "0 stage catalog",
    };

    /// <summary>コマンド表の行頭。**連結で組む**（この診断自身が `CLAUDE.md` に載るので、素直に書くと自分の行に当たる＝第123期）。</summary>
    static readonly string Prefix = string.Concat("    dotnet run --project ", "BattleSim -c Release ");

    public static void Run(string[] rest)
    {
        int limit = DefaultLimitSec;
        var filters = new List<string>();
        bool listOnly = false;
        foreach (string a in rest)
        {
            if (a == "list") listOnly = true;
            else if (int.TryParse(a, out int n) && n > 0) limit = n;
            else filters.AddRange(a.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        string root = FindRoot() ?? throw new InvalidOperationException("CLAUDE.md と BattleSim/ を持つディレクトリが見つからない（リポジトリの中で回すこと）");
        string dll = typeof(SweepDiag).Assembly.Location;
        var cmds = Extract(Path.Combine(root, "CLAUDE.md"));
        if (filters.Count > 0) cmds = cmds.Where(c => filters.Any(f => c.Contains(f, StringComparison.Ordinal))).ToList();

        Console.WriteLine("# 全診断の exit 検査（`sweep`・第141期）");
        Console.WriteLine();
        Console.WriteLine($"`CLAUDE.md` のコマンド表から引数の穴（`<...>`）の無い行を抜き出し、`[...]` を落として **{cmds.Count} 本**。");
        Console.WriteLine($"1本ずつ **{limit} 秒**の上限で子プロセスとして走らせる（標準出力は捨てる・ファイルは書かない）。");
        Console.WriteLine($"`{Path.GetFileName(dll)}` を `{root}` で回す。");
        Console.WriteLine();
        if (cmds.Count == 0) { Console.WriteLine("**一覧が空。止める**（第117期）。"); Environment.ExitCode = 2; return; }

        if (listOnly)
        {
            Console.WriteLine("| # | コマンド | 既知の長い本 |");
            Console.WriteLine("|--:|---|:-:|");
            for (int i = 0; i < cmds.Count; i++) Console.WriteLine($"| {i + 1} | `{cmds[i]}` | {(KnownLong.Contains(cmds[i]) ? "既知" : "")} |");
            return;
        }

        var total = Stopwatch.StartNew();
        var rows = new List<(string Cmd, double Sec, int Exit, string Tail)>();
        Console.WriteLine("| # | コマンド | 秒 | exit | 判定 |");
        Console.WriteLine("|--:|---|--:|--:|---|");
        for (int i = 0; i < cmds.Count; i++)
        {
            Console.Error.WriteLine($"[{i + 1}/{cmds.Count}] {cmds[i]}");
            var (sec, exit, tail) = RunOne(dll, root, cmds[i], limit);
            rows.Add((cmds[i], sec, exit, tail));
            Console.WriteLine($"| {i + 1} | `{cmds[i]}` | {sec:F1} | {ExitText(exit)} | {Verdict(exit)} |");
            Console.Out.Flush();
        }
        total.Stop();

        var ok = rows.Where(r => r.Exit == 0).ToList();
        var timeouts = rows.Where(r => r.Exit == 124).ToList();
        var bad = rows.Where(r => r.Exit != 0 && r.Exit != 124).ToList();

        Console.WriteLine();
        Console.WriteLine("## 集計");
        Console.WriteLine();
        Console.WriteLine("| 判定 | 本数 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| 正常（exit 0） | {ok.Count} |");
        Console.WriteLine($"| 上限（{limit} 秒） | {timeouts.Count}（既知 {timeouts.Count(r => KnownLong.Contains(r.Cmd))} ／ 新規 {timeouts.Count(r => !KnownLong.Contains(r.Cmd))}） |");
        Console.WriteLine($"| **異常終了** | **{bad.Count}** |");
        Console.WriteLine($"| 合計 | {rows.Count}（{total.Elapsed.TotalMinutes:F1} 分） |");
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

        Console.WriteLine("## 上限に当たった本" + (timeouts.Count == 0 ? " —— 0 本" : ""));
        Console.WriteLine();
        if (timeouts.Count > 0)
        {
            Console.WriteLine("| コマンド | 既知／新規 |");
            Console.WriteLine("|---|---|");
            foreach (var r in timeouts) Console.WriteLine($"| `{r.Cmd}` | {(KnownLong.Contains(r.Cmd) ? "既知（`KnownLong`）" : "**新規**")} |");
            Console.WriteLine();
        }
        var knownMissing = KnownLong.Where(k => cmds.Contains(k) && !timeouts.Any(t => t.Cmd == k)).ToList();
        if (knownMissing.Count > 0)
            Console.WriteLine("既知の長い本のうち上限に当たらなかったもの（速くなった。`KnownLong` から外してよい）: " + string.Join(" / ", knownMissing.Select(k => $"`{k}`")));
        Console.WriteLine();

        Console.WriteLine("## 正常のうち遅い上位 10");
        Console.WriteLine();
        foreach (var r in ok.OrderByDescending(r => r.Sec).Take(10)) Console.WriteLine($"- `{r.Cmd}` {r.Sec:F1}s");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "**合格**（異常終了 0 本）。" : $"**不合格**（異常終了 {bad.Count} 本）。");
        Environment.ExitCode = bad.Count == 0 ? 0 : 1;
    }

    static string ExitText(int e) => e == 0 ? "0" : e == 124 ? "124（上限）" : unchecked((uint)e) >= 0x80000000u ? $"0x{unchecked((uint)e):X8}" : e.ToString();
    static string Verdict(int e) => e == 0 ? "" : e == 124 ? "上限" : "**異常終了**";

    /// <summary>`CLAUDE.md` から走らせるコマンドを組む。**行を読んでトークンに割る**（連結や文字列の再解釈をしない）。</summary>
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

    static string[] Tokens(string s) => s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                          .Select(t => t.Trim('\r')).Where(t => t.Length > 0).ToArray();

    static (double Sec, int Exit, string Tail) RunOne(string dll, string root, string cmd, int limitSec)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(dll);
        foreach (string t in Tokens(cmd)) psi.ArgumentList.Add(t);

        var err = new StringBuilder();
        var sw = Stopwatch.StartNew();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, _) => { };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        int exit;
        if (p.WaitForExit(limitSec * 1000))
        {
            p.WaitForExit();   // 非同期の読み取りを流し切る
            exit = p.ExitCode;
        }
        else
        {
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
        return (sw.Elapsed.TotalSeconds, exit, tail.Replace("|", "\\|"));
    }

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
}
