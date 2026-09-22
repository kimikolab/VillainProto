using BattleCore;
using static Common;

// =====================================================================================
// roster モード —— **ロスターの棚卸しを1枚の表にする**（第177期 §4）。
//
// **この期の測定ではない。** ここがするのは「**既にある数字を1つの表に集める**」ことだけで、
// 新しい量も新しい線も1つも作らない。**判断も書かない**（候補の選定は次の期）。
//
// 出どころは5つ。**手書きは0行**で、全部が走査か既存の診断の出力である:
//
//   理想台の帰属（素体差し替え）   `checkup ideal` を**そのまま呼ぶ**（第82期の器具・約 11 秒）
//   列レンジと3群                 `stage catalog` を**そのまま呼ぶ**（第164期の器具・約 2 分）
//   `checkup` の単独と3分          上の `stage catalog` が `design/PHASE152_CHECKUP.md` から読んだもの
//   固有の勝者の行数               `docs/balance.md`（**生成済みのものを読む**）× `Presets.Compare`
//   枠の値段                       `design/PHASE172_REFORM.md` の部C の表
//
// **既存の診断は1行も書き換えていない**——`Console.Out` を差し替えて呼び、出た表を読み直す。
// 写しを持たないので、器具を直せばこの表も一緒に直る（R036 / R259）。
// **走査が空なら止める**（R034）——「引けなかった」と「該当なし」を混ぜない。
//
//     dotnet run --project BattleSim -c Release 0 roster audit > docs/roster_audit.md
// =====================================================================================

static class RosterDiag
{
    /// <summary>`checkup` の単独の線（第119期）。<b>ここでは線を引かない</b>——象限の名前に使うだけ。</summary>
    const double SoloLow = 1.5;

    /// <summary>第164期 Q0-3 の「列レンジ 大」。<b>同じく象限の名前に使うだけ。</b></summary>
    const double ColBig = 17.0;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "audit";
        if (mode != "audit")
        {
            Console.WriteLine("roster: モードは audit だけ。");
            return;
        }
        Audit();
    }

    // ---------------- 既存の診断を呼んで、その出力を読む ----------------

    /// <summary>既存の診断を <b>そのまま</b> 呼び、標準出力を横取りして行に割る。</summary>
    static string[] Capture(Action run)
    {
        TextWriter keep = Console.Out;
        var buf = new StringWriter();
        try { Console.SetOut(buf); run(); }
        finally { Console.SetOut(keep); }
        return buf.ToString().Replace("\r\n", "\n").Split('\n');
    }

    /// <summary>Markdown の表の1行を欄に割る（空欄も残す）。</summary>
    static string[] Cells(string line)
        => line.Trim().Trim('|').Split('|').Select(x => x.Trim()).ToArray();

    static string Plain(string s) => s.Replace("**", "").Trim();

    static double? Num(string s)
    {
        string t = Plain(s).Replace("−", "-").Replace("＋", "+").TrimStart('+');
        return double.TryParse(t, out double v) ? v : null;
    }

    /// <summary>リポジトリ直下（`docs/balance.md` がある場所）を探す。</summary>
    static string Root()
    {
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Path.GetDirectoryName(root);
        return root ?? throw new DirectoryNotFoundException(
            "リポジトリ直下が引けない（docs/balance.md が見つからない）。");
    }

    // =================================================================================
    // 1) 理想台の帰属（素体差し替え）——`checkup ideal` の表をそのまま読む
    // =================================================================================

    sealed record Ideal(int Rows, double? Attr, double? Raw);

    static Dictionary<string, Ideal> ReadIdeal()
    {
        var d = new Dictionary<string, Ideal>(StringComparer.Ordinal);
        foreach (string line in Capture(() => CheckupDiag.Run(new[] { "0", "checkup", "ideal" }, 0)))
        {
            if (!line.StartsWith("| ")) continue;
            string[] c = Cells(line);
            if (c.Length < 5 || !int.TryParse(c[0], out _)) continue;
            d[Plain(c[1])] = new Ideal(int.TryParse(c[2], out int r) ? r : 0, Num(c[3]), Num(c[4]));
        }
        if (d.Count == 0) throw new InvalidOperationException("`checkup ideal` の表が読めなかった。");
        return d;
    }

    // =================================================================================
    // 2) 列レンジと3群 ——`stage catalog` の「F. 全 52 枚」の R0 の節を読む
    // =================================================================================

    sealed record Catalog(string Group, double? Solo, string CheckupGroup,
                          double? ColRange, double? BandRange, string Best, string Worst);

    static Dictionary<string, Catalog> ReadCatalog(out double seconds)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string[] lines = Capture(() => StageDiag.Run("catalog", ""));
        seconds = sw.Elapsed.TotalSeconds;

        var d = new Dictionary<string, Catalog>(StringComparer.Ordinal);
        bool inF = false, inR0 = false;
        foreach (string line in lines)
        {
            if (line.StartsWith("## ")) { inF = line.Contains("全 52 枚"); inR0 = false; continue; }
            if (!inF) continue;
            // 版ごとに同じ形の表が並ぶので、**R0 の節だけ**を読む（第164期の主版）。
            if (line.StartsWith("### ")) { inR0 = Plain(line[4..]) == "R0"; continue; }
            if (!inR0 || !line.StartsWith("| ")) continue;
            string[] c = Cells(line);
            if (c.Length < 9 || c[0] == "駒" || c[0].StartsWith("---")) continue;
            d[Plain(c[0])] = new Catalog(Plain(c[1]), Num(c[2]), Plain(c[3]),
                                         Num(c[4]), Num(c[6]), Plain(c[7]), Plain(c[8]));
        }
        if (d.Count == 0) throw new InvalidOperationException(
            "`stage catalog` の「F. 全 52 枚」（R0）の表が読めなかった。");
        return d;
    }

    // =================================================================================
    // 3) 固有の勝者の行数 ——`docs/balance.md`（**生成済みのものを読む**）
    //
    // 定義は `spread` §3 と同じ: **その波でだけ 100%**（他のどの波でも 100% 未満）の編成。
    // **第一波は分母に入れない**（規約 (G10)。全行が 100% なので入れると恒等的に 0 になる）。
    // =================================================================================

    static (Dictionary<string, int> PerUnit, string[] Rows) ReadSoleWinners(string root)
    {
        var rate = new Dictionary<string, double[]>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(Path.Combine(root, "docs", "balance.md")))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            string[] c = Cells(line);
            if (c.Length < 6) continue;
            var v = new double[5];
            bool ok = true;
            for (int w = 0; w < 5; w++)
                if (!double.TryParse(c[w + 1].TrimEnd('%'), out v[w])) { ok = false; break; }
            if (ok) rate[Plain(c[0])] = v;
        }
        if (rate.Count == 0) throw new InvalidOperationException("`docs/balance.md` の勝率表が読めなかった。");

        var sole = new List<string>();
        foreach (var (name, v) in rate)
        {
            int full = 0;
            for (int w = 1; w < 5; w++) if (v[w] >= 100.0) full++;
            if (full == 1) sole.Add(name);
        }

        var per = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in Presets.Compare)
        {
            if (!sole.Contains(row.Name)) continue;
            foreach ((int _, UnitDef def) in row.F.Occupied())
                per[def.Name] = per.GetValueOrDefault(def.Name) + 1;
        }
        return (per, sole.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    // =================================================================================
    // 4) 枠の値段 ——`design/PHASE172_REFORM.md` の部C の表
    //
    // 第172期 部C は「かき回し隊の **後3 の1枠だけ**」を振った表で、**駒の帰属ではなく
    // 枠の値段**を測っている（R266）。ここに出ている駒にだけ値が入る。
    // =================================================================================

    static Dictionary<string, string> ReadSeatPrice(string root, out string path)
    {
        path = Path.Combine(root, "design", "PHASE172_REFORM.md");
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return d;
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ")) continue;
            string[] c = Cells(line);
            if (c.Length < 8) continue;
            string kind = Plain(c[0]);
            if (kind != "対照" && kind != "候補") continue;
            d[Plain(c[1])] = kind == "対照" ? "対照 0.0pt" : Plain(c[6]) + "pt";
        }
        return d;
    }

    // =================================================================================
    // 本体
    // =================================================================================

    static void Audit()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string root = Root();

        var ideal = ReadIdeal();
        var cat = ReadCatalog(out double catSec);
        var (sole, soleRows) = ReadSoleWinners(root);
        var price = ReadSeatPrice(root, out string pricePath);

        // 在席枠（`Presets.Compare` の枠の数）。**戦闘を1回も回さずに数える。**
        var seats = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in Presets.Compare)
            foreach ((int _, UnitDef def) in row.F.Occupied())
                seats[def.Name] = seats.GetValueOrDefault(def.Name) + 1;

        UnitDef[] all = UnitCatalog.All.ToArray();
        // **並びは素体差し替えの寄与の昇順**（指示書 §4）。引けない駒（在席 0 枠）は末尾。
        var order = all
            .OrderBy(d => ideal.TryGetValue(d.Name, out Ideal? x) && x.Attr is { } a ? a : double.MaxValue)
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .ToArray();

        int weaker = all.Count(d => ideal.TryGetValue(d.Name, out Ideal? x) && x.Attr is { } a && a < 0);

        Console.WriteLine("# ロスターの棚卸し");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 roster audit > docs/roster_audit.md` の出力。手で編集しない。");
        Console.WriteLine();
        Console.WriteLine($"**素体より弱い駒は {weaker} / {all.Length} 枚**"
            + "（`理想台の帰属` が負 ＝ その駒を同数値・特性なしの素体に落としたほうが編成が強くなる）。");
        Console.WriteLine();
        Console.WriteLine("**判断は書かない。** この表は次の期（転生候補の洗い出し）の材料で、"
            + "**新しい測定は1つもしていない**——既にある数字を集めただけである。");
        Console.WriteLine();

        // ---- 出どころ ----
        Console.WriteLine("## 出どころ");
        Console.WriteLine();
        Console.WriteLine("| 列 | 定義 | 出どころ |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 理想台の帰属 | `CompareBuilds()` 61 行の在席枠で、**その駒だけを素体に落とした差**（第2〜5波・seed 0..199） | `checkup ideal`（第82期） |");
        Console.WriteLine("| `checkup` 単独 / 3分 | ドラフト台の 2×2 の `y11 − y01` と、それで割った 残す / 転生 / 差し替え | `design/PHASE152_CHECKUP.md` 表A（**再計算しない**） |");
        Console.WriteLine("| 在席枠 | `Presets.Compare` 61 行に何枠いるか | `Presets`（**戦闘0回**） |");
        Console.WriteLine("| 固有の勝者 | **その波でだけ 100%** の行（第2〜5波）に何枠いるか | `docs/balance.md`（生成済み） |");
        Console.WriteLine("| 列レンジ / 送り先 | 12 列で順位がどれだけ動くか・第164期の3群 | `stage catalog`（第164期・版 R0） |");
        Console.WriteLine("| 枠の値段 | かき回し隊の 後3 の**1枠だけ**を振ったときの踏破率の差 | `design/PHASE172_REFORM.md` 部C |");
        Console.WriteLine();
        Console.WriteLine($"固有の勝者の行（第2〜5波）は **{soleRows.Length} 行** —— "
            + (soleRows.Length == 0 ? "なし" : string.Join(" / ", soleRows)) + "。");
        Console.WriteLine();
        Console.WriteLine($"枠の値段の出どころは `design/{Path.GetFileName(pricePath)}`（{price.Count} 枚ぶん）。"
            + "**無い駒は空欄**——あの表は1つの枠でしか測っていないので、空欄は「0」ではない。");
        Console.WriteLine();

        // ---- 本表 ----
        Console.WriteLine("## 52 枚（**理想台の帰属の昇順**）");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | 理想台の帰属 | `checkup` 単独 | `checkup` 3分 | 在席枠 | 固有の勝者 "
            + "| 列レンジ | 送り先 | 象限 | 枠の値段 | 札 |");
        Console.WriteLine("|--:|---|--:|--:|---|--:|--:|--:|---|---|--:|---|");
        int rank = 0, missing = 0;
        foreach (UnitDef d in order)
        {
            ideal.TryGetValue(d.Name, out Ideal? id);
            cat.TryGetValue(d.Name, out Catalog? c);
            if (id?.Attr is null) missing++;
            string quad = c?.Solo is { } s && c.ColRange is { } cr
                ? (s < SoloLow ? (cr >= ColBig ? "左下（送り先を選べば働く）" : "右下（選んでも働かない）")
                               : "上（単独で強い）")
                : "—";
            Console.WriteLine($"| {++rank} | {d.Name} "
                + $"| {(id?.Attr is { } a ? a.ToString("+0.00;-0.00") : "—")} "
                + $"| {(c?.Solo is { } so ? so.ToString("+0.00;-0.00") : "—")} "
                + $"| {(string.IsNullOrEmpty(c?.CheckupGroup) ? "—" : c!.CheckupGroup)} "
                + $"| {seats.GetValueOrDefault(d.Name)} "
                + $"| {sole.GetValueOrDefault(d.Name)} "
                + $"| {(c?.ColRange is { } cr2 ? cr2.ToString("F1") : "—")} "
                + $"| {(string.IsNullOrEmpty(c?.Group) ? "—" : c!.Group)} "
                + $"| {quad} "
                + $"| {price.GetValueOrDefault(d.Name, "")} "
                + $"| {(d.Traits.Count == 0 ? "—" : string.Join(" / ", d.Traits))} |");
        }
        Console.WriteLine();

        // ---- 自己検査 ----
        Console.WriteLine("## 自己検査");
        Console.WriteLine();
        Console.WriteLine($"- `UnitCatalog.All` の {all.Length} 枚すべてに行がある: "
            + $"**{(rank == all.Length ? "○" : "×")}**（{rank} 行）");
        Console.WriteLine($"- `checkup ideal` が値を返した駒: {all.Length - missing} / {all.Length} "
            + "（返さないのは `CompareBuilds()` に在席 0 枠の駒だけ）");
        Console.WriteLine($"- `stage catalog` が引けた駒: {all.Count(d => cat.ContainsKey(d.Name))} / {all.Length}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒（うち `stage catalog` が {catSec:F1} 秒）。");
    }
}
