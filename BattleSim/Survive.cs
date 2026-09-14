using System.Reflection;
using BattleCore;

// =====================================================================================
// survive モード（第129期） —— 指標の直しと、「起動まで守れるか」
//
// 2本立て:
//   **A. 指標が緩い。** 現行の「完全勝利」は `PlayerSurvivors >= 出撃数` で、`PlayerSurvivors` は
//   戦闘中に湧いた駒（胞子・餌）も数える。分母も「勝った試行」だけ。
//   → 段1 は `docs/quality.md` に **無傷勝利 / 実質無傷勝利 / 決着Tの分布** を足す（`Program.cs` 側）。
//
//   **B. 起動前に落ちる。** 第126期の実測でソラ 2.32T・ガルド 2.17T・トメ 3.20T。
//   **この状態では「組み合わせが弱い」のか「実演する時間がなかった」のかが混ざる。**
//   → 段2 は**延命台**（`TraitId.Undying`）で「守れたら起動するか」を先に測る。
//   **起動しない駒は段3 の対象から外す。**
//
// **Phase 0 と段2 は `Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` を1文字も触らない。**
// 台は診断のローカルで、`Undying` の保持者は `UnitCatalog.All` に1枚もいない。
//
//     dotnet run --project BattleSim -c Release 0 survive phase0  # Q0-1〜Q0-9
//     dotnet run --project BattleSim -c Release 0 survive run     # 段2（延命台）
//     dotnet run --project BattleSim -c Release 0 survive check [採用前のbalance.md]
// =====================================================================================

/// <summary>
/// 第129期の走査。<b>段1（`Program.cs` の `compare quality`）と段2 が共有する</b>ので
/// 診断クラスの外に出してある。<b>すべて実装から引く</b>（第94期の作法。走査が空なら止める＝第117期）。
/// </summary>
static class SurviveScan
{
    /// <summary>
    /// <b><c>OnDeath</c> を上書きしている札</b>＝「自分の死が起動条件になる」型。
    /// <b>宣言の走査ではなくリフレクションで引く</b>（1クラスが複数の <c>TraitId</c> を持つ札があり、
    /// 宣言の走査では落ちる＝第127期）。
    /// </summary>
    public static readonly List<TraitId> DeathTraits = Overriders(nameof(Trait.OnDeath));

    /// <summary>
    /// <b>他人の死を読む側</b>（<c>OnAnyDeath</c> / <c>OnAllyDeath</c>）。
    /// <b>免除には1つも使わない。</b> 墓守リィカ・疫みのラウ・追い打ちのハギは
    /// 「味方の死を要求する軸」であって「自分が死ぬ駒」ではない
    /// ——混ぜると死軸が丸ごと免除されて、指標を直した意味が消える。
    /// </summary>
    public static readonly List<TraitId> OtherDeathTraits =
        Overriders(nameof(Trait.OnAnyDeath)).Concat(Overriders(nameof(Trait.OnAllyDeath)))
            .Distinct().ToList();

    static List<TraitId> Overriders(string method)
    {
        var hit = new List<TraitId>();
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            Trait tr;
            try { tr = TraitCatalog.Get(id); }
            catch (KeyNotFoundException) { continue; }
            MethodInfo? m = tr.GetType().GetMethod(method);
            if (m is not null && m.DeclaringType != typeof(Trait)) hit.Add(id);
        }
        return hit;
    }

    /// <summary>その札を持つ<b>味方の駒</b>（<c>UnitCatalog.All</c> の中だけ）。</summary>
    public static List<UnitDef> Holders(TraitId t)
        => UnitCatalog.All.Where(d => d.Traits.Contains(t)).ToList();

    /// <summary>
    /// その編成で<b>免除する駒の <c>Def.Id</c></b>。
    /// <b><c>OnDeath</c> を持つ味方だけ</b>で、<c>OnAnyDeath</c> 側は1つも入れない。
    /// </summary>
    public static HashSet<string> ExcusedIds(Formation f)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach ((int _, UnitDef d) in f.Occupied())
            if (d.Traits.Any(DeathTraits.Contains)) set.Add(d.Id);
        return set;
    }

    public static string NameList(IEnumerable<TraitId> ids)
    {
        var l = ids.Select(i => i.ToString()).ToList();
        return l.Count == 0 ? "—" : string.Join(" / ", l);
    }

    public static double Mean(IReadOnlyList<int> v) => v.Count == 0 ? 0 : v.Average();

    public static double Sd(IReadOnlyList<int> v)
    {
        if (v.Count < 2) return 0;
        double m = v.Average();
        return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / (v.Count - 1));
    }

    /// <summary>四分位（<b>線形補間なしの順位法</b>。決着Tは整数なので補間しない）。</summary>
    public static int Quantile(List<int> v, double q)
    {
        if (v.Count == 0) return 0;
        var s = v.OrderBy(x => x).ToList();
        int i = (int)Math.Floor(q * (s.Count - 1) + 0.5);
        return s[Math.Clamp(i, 0, s.Count - 1)];
    }
}

static class SurviveDiag
{
    const int Seeds = 200;   // 帯A。`compare` と揃える（規約 (G14)）

    static string? _root;
    static string _traits = "", _engine = "", _program = "";

    public static void Run(string mode, string arg)
    {
        if (!Init()) return;
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunStage2(); return;
            case "hp": Sweep(arg); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("survive: モードは phase0 / run / hp <駒Id> / check（第129期）。");
                return;
        }
    }

    // ==================================================================================
    // 索く。**索けなかったら止める**（第117期）
    // ==================================================================================
    static bool Init()
    {
        _root = Directory.GetCurrentDirectory();
        while (_root != null && !File.Exists(Path.Combine(_root, "docs", "balance.md")))
            _root = Path.GetDirectoryName(_root);
        if (_root is null)
        {
            Console.WriteLine("survive: リポジトリが見つからない。**走査が空なら止める**（第117期）。");
            return false;
        }
        _traits = Read("BattleCore", "Traits.cs");
        _engine = Read("BattleCore", "BattleEngine.cs");
        _program = Read("BattleSim", "Program.cs");
        if (_traits.Length == 0 || _engine.Length == 0 || _program.Length == 0)
        {
            Console.WriteLine("survive: ソースが読めない。**止める**（第117期）。");
            return false;
        }
        return true;
    }

    static string Read(string dir, string file)
    {
        string p = Path.Combine(_root ?? ".", dir, file);
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    /// <summary>その語を含む行を全部返す。<b>件数を出して 0 なら止める</b>（第117期）。</summary>
    static List<string> Lines(string src, string needle)
        => src.Split('\n').Select(l => l.TrimEnd('\r'))
              .Where(l => l.Contains(needle, StringComparison.Ordinal)).ToList();

    static string Seat(int slot) => slot switch
    {
        0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3",
        5 => "○中1", 6 => "○中3", 7 => "○前2", 8 => "○後2", _ => slot.ToString()
    };

    // ==================================================================================
    // Phase 0
    // ==================================================================================
    static void Phase0()
    {
        Console.WriteLine("# 第129期 Phase 0 —— 指標の直しと、起動まで守れるか");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 survive phase0` の出力。");
        Console.WriteLine("**盤面は1ビットも動かない。** Q0-1 / Q0-4 / Q0-5 だけ戦闘を回す"
            + "（既存の計数を読み直すだけで、規則もノブも1つも触らない）。");
        Console.WriteLine("**手で並べた表は1つも無い**（第94期の作法。走査が空なら止める＝第117期）。");
        Console.WriteLine();
        Q01(); Q02(); Q03(); Q04(); Q05(); Q06(); Q07(); Q08(); Q09();
    }

    static void Q01()
    {
        Console.WriteLine("## Q0-1 —— 現行「完全勝利」の実装と、`PlayerSurvivors` が数えているもの");
        Console.WriteLine();
        var judge = Lines(_program, "qPerf[bi]++");
        var src = Lines(_engine, "PlayerSurvivors = ");
        Console.WriteLine($"判定式（`BattleSim/Program.cs`・**{judge.Count} 件**）:");
        Console.WriteLine();
        foreach (string l in judge) Console.WriteLine($"    {l.Trim()}");
        Console.WriteLine();
        Console.WriteLine($"`PlayerSurvivors` の定義（`BattleCore/BattleEngine.cs`・**{src.Count} 件**）:");
        Console.WriteLine();
        foreach (string l in src) Console.WriteLine($"    {l.Trim()}");
        Console.WriteLine();
        if (judge.Count == 0 || src.Count == 0)
        {
            Console.WriteLine("→ **走査が空。止める**（第117期）。");
            Console.WriteLine();
            return;
        }

        // 湧く駒の全数（実装から）。
        var summonable = UnitCatalog.All.Concat(new[] { UnitCatalog.Spore, UnitCatalog.Fodder })
            .Distinct().Where(d => d.Traits.Contains(TraitId.Ephemeral)).ToList();
        Console.WriteLine("`ctx.LivingMembers(PlayerTeam)` は**盤上の味方を全部数える**ので、"
            + $"湧いた駒（`TraitId.Ephemeral` 持ち **{summonable.Count} 種**"
            + $"——{string.Join("・", summonable.Select(d => d.Name))}）も入る。");
        Console.WriteLine();

        // **実証**: compare 61 行 × 第2〜5波 × seed 0..199 を回して数える。
        Console.WriteLine("### 実証（`compare` 61 行 × 第2〜5波 × seed 0..199）");
        Console.WriteLine();
        int perf = 0, perfDirty = 0, over = 0, alive = 0, wins = 0, trials = 0, clean = 0;
        var dirtyRows = new List<(string Row, int N)>();
        foreach ((string name, Formation f) in Presets.Compare)
        {
            int party = f.Occupied().Count();
            int rowDirty = 0;
            for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false);
                    trials++;
                    // **湧いた駒が数えられていることの直接の証拠**は
                    // 「`PlayerSurvivors` > 出撃数」ではなく
                    // **「`PlayerSurvivors` ≠ 出撃数 − 欠けた数」**で見る
                    // ——前者は「欠けた数 ≦ 湧いた数」まで要求するので、
                    // **1枚欠けて1体湧いた戦を数え落とす。**
                    if (r.PlayerSurvivors != party - r.PlayerStarterFallen.Count) alive++;
                    if (r.PlayerSurvivors > party) over++;
                    if (!r.PlayerWon) continue;
                    wins++;
                    if (r.PlayerStarterFallen.Count == 0) clean++;
                    if (r.PlayerSurvivors < party) continue;
                    perf++;
                    if (r.PlayerStarterFallen.Count > 0) { perfDirty++; rowDirty++; }
                }
            if (rowDirty > 0) dirtyRows.Add((name, rowDirty));
        }
        Console.WriteLine($"- 全試行 **{trials}** ／ 勝った試行 **{wins}** ／ "
            + $"現行の「完全勝利」**{perf}**（勝った試行の {perf * 100.0 / Math.Max(1, wins):F1}%）");
        Console.WriteLine($"- そのうち**出撃した駒が1枚以上欠けている試行 {perfDirty} 件**"
            + $"（{perfDirty * 100.0 / Math.Max(1, perf):F1}%）"
            + "——**湧いた駒が欠けを埋めた戦**");
        Console.WriteLine($"- **`PlayerSurvivors` ≠ 出撃数 − 欠けた数**の試行 **{alive} 件**"
            + $"（{alive * 100.0 / Math.Max(1, trials):F1}%）"
            + "——**決着時に湧いた駒が盤上に残っていた戦**。湧いた駒が数えられていることの直接の証拠");
        Console.WriteLine($"- そのうち `PlayerSurvivors` が**出撃数を上回った**試行 **{over} 件**"
            + "（＝湧いた数 ≧ 欠けた数。**現行の判定式を実際に通せる形**）");
        Console.WriteLine($"- 出撃した駒が1枚も欠けずに勝った試行 **{clean} 件**"
            + $"（全試行の {clean * 100.0 / Math.Max(1, trials):F1}%）");
        Console.WriteLine();
        if (dirtyRows.Count > 0)
        {
            Console.WriteLine($"「完全勝利」なのに出撃駒が欠けている行 **{dirtyRows.Count} 行**:");
            Console.WriteLine();
            Console.WriteLine("| 編成 | 件数 |");
            Console.WriteLine("|---|--:|");
            foreach ((string n, int c) in dirtyRows.OrderByDescending(x => x.N))
                Console.WriteLine($"| {n} | {c} |");
            Console.WriteLine();
        }
        Console.WriteLine(perfDirty > 0
            ? "→ **§3-1 の前提どおり。指標は召喚体のぶんだけ緩い。**"
            : "→ **召喚体は現行の「完全勝利」に1件も混ざっていない**（0 件）。"
              + "**§3-1 の前提のうち「召喚体が欠けを埋める」は実測では起きていない**"
              + "ので、止めて報告する（P1 の判定に効く）。"
              + $"**残る緩さは分母だけ**——勝った試行 {wins} 対 全試行 {trials} で "
              + $"{perf * 100.0 / Math.Max(1, wins):F1}% 対 {clean * 100.0 / Math.Max(1, trials):F1}%。");
        Console.WriteLine();
    }

    static void Q02()
    {
        Console.WriteLine("## Q0-2 —— 駒ごとの生死が取れるか");
        Console.WriteLine();
        var fallen = Lines(_engine, "PlayerStarterFallen = ");
        Console.WriteLine("`BattleResult` から**出撃5枚それぞれの生死**は、第128期までは**引けなかった**:");
        Console.WriteLine();
        Console.WriteLine("| 候補 | 引けるか | 理由 |");
        Console.WriteLine("|---|:-:|---|");
        Console.WriteLine("| `PlayerSurvivors` | × | 数だけ。しかも**湧いた駒を含む**（Q0-1） |");
        Console.WriteLine("| `TallyByUnit[id].Deaths` | △ | **倒れた回数**なので蘇生で戻った駒も 1 以上になる。"
            + "キーは `Def.Id` なので同じ駒が複数立つと潰れる |");
        Console.WriteLine("| `TallyByUnit[id].LastActiveTurn` | △ | 生存なら決着ターン。"
            + "**決着ターンちょうどで倒れた駒と区別が付かない** |");
        Console.WriteLine();
        Console.WriteLine($"→ **計数を1本足した**（`BattleResult.PlayerStarterFallen`・**{fallen.Count} 件**）。"
            + "出撃した `UnitState` そのものを見るので、湧いた駒は入らず、蘇生で戻った駒は「欠け」にならない。");
        Console.WriteLine("**誰も読んで分岐しない**（`verbose` にも依存しない）。");
        Console.WriteLine();
        foreach (string l in fallen) Console.WriteLine($"    {l.Trim()}");
        Console.WriteLine();
    }

    static void Q03()
    {
        Console.WriteLine("## Q0-3 —— 「意図した犠牲」の駒を機械で引く");
        Console.WriteLine();
        Console.WriteLine("**`OnDeath` を上書きする札**＝自分の死が起動条件になる型。"
            + "**リフレクションで引く**（宣言の走査では1クラス複数 `TraitId` の札が落ちる＝第127期）。");
        Console.WriteLine();
        Console.WriteLine("| 札 | クラス | 味方の保持者 |");
        Console.WriteLine("|---|---|---|");
        foreach (TraitId t in SurviveScan.DeathTraits)
        {
            var h = SurviveScan.Holders(t);
            Console.WriteLine($"| `{t}` | `{TraitCatalog.Get(t).GetType().Name}` | "
                + (h.Count == 0 ? "**0 枚**（敵側／器具）" : string.Join("・", h.Select(d => d.Name))) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"**`OnDeath` は {SurviveScan.DeathTraits.Count} 本**。味方の保持者がいるのは "
            + $"**{SurviveScan.DeathTraits.Count(t => SurviveScan.Holders(t).Count > 0)} 本**。");
        Console.WriteLine();
        Console.WriteLine("**必ず分ける**——`OnAnyDeath` / `OnAllyDeath` は**他人の死を読む側**で、免除には1つも使わない:");
        Console.WriteLine();
        Console.WriteLine("| 札 | クラス | 味方の保持者 |");
        Console.WriteLine("|---|---|---|");
        foreach (TraitId t in SurviveScan.OtherDeathTraits)
        {
            var h = SurviveScan.Holders(t);
            Console.WriteLine($"| `{t}` | `{TraitCatalog.Get(t).GetType().Name}` | "
                + (h.Count == 0 ? "**0 枚**" : string.Join("・", h.Select(d => d.Name))) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"**`OnAnyDeath` / `OnAllyDeath` は {SurviveScan.OtherDeathTraits.Count} 本。**"
            + "リィカ（墓守）・ラウ（疫み）・ハギ（追い打ち）はここに出る"
            + "——**「味方の死を要求する軸」であって「自分が死ぬ駒」ではない。**");
        Console.WriteLine();

        var rows = Presets.Compare
            .Select(b => (b.Name, Ex: SurviveScan.ExcusedIds(b.F), b.F))
            .Where(x => x.Ex.Count > 0).ToList();
        Console.WriteLine($"`compare` {Presets.Compare.Length} 行のうち**免除対象を1枚以上含む行 {rows.Count} 行**"
            + $"（{rows.Count * 100.0 / Presets.Compare.Length:F1}%・規約 (G4)）:");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 免除する駒 |");
        Console.WriteLine("|---|---|");
        foreach ((string n, HashSet<string> ex, Formation f) in rows)
            Console.WriteLine($"| {n} | "
                + string.Join("・", f.Occupied().Where(o => ex.Contains(o.Def.Id)).Select(o => o.Def.Name))
                + " |");
        Console.WriteLine();
    }

    static void Q04()
    {
        Console.WriteLine("## Q0-4 —— 決着ターンの分布が取れるか");
        Console.WriteLine();
        var t = Lines(_engine, "Turns = Math.Min(turn, MaxTurns)");
        Console.WriteLine($"`BattleResult.Turns`（**{t.Count} 件**）がそのまま決着ターン。"
            + "**分布は既存の値から作れる**（新しい計数は要らない）。");
        Console.WriteLine();
        foreach (string l in t) Console.WriteLine($"    {l.Trim()}");
        Console.WriteLine();
        Console.WriteLine("→ 段1 で `docs/quality.md` に **平均・σ・四分位・波別**を足す（§0-1-3 / P5）。");
        Console.WriteLine();

        var row = Presets.Compare.FirstOrDefault(b => b.Name.StartsWith("責め苦", StringComparison.Ordinal));
        if (row.F is null)
        {
            Console.WriteLine("**`責め苦` の行が `Presets` に無い。走査が空なので止める**（第117期）。");
            Console.WriteLine();
            return;
        }
        Console.WriteLine("### 責め苦 第四波の決着ターン分布（第128期の宿題・P5）");
        Console.WriteLine();
        var win = new List<int>(); var lose = new List<int>();
        for (int seed = 0; seed < Seeds; seed++)
        {
            BattleResult r = BattleEngine.Run(row.F, EnemyCatalog.Stages[3].Enemy, seed, verbose: false);
            (r.PlayerWon ? win : lose).Add(r.Turns);
        }
        var all = win.Concat(lose).ToList();
        Console.WriteLine("| 群 | 試行 | 平均T | σ | 最小 | 最大 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach ((string g, List<int> v) in new[] { ("全部", all), ("勝ち", win), ("負け", lose) })
            Console.WriteLine($"| {g} | {v.Count} | "
                + (v.Count == 0 ? "— | — | — | — |"
                   : $"{SurviveScan.Mean(v):F2} | {SurviveScan.Sd(v):F2} | {v.Min()} | {v.Max()} |"));
        Console.WriteLine();
        var ts = all.Distinct().OrderBy(x => x).ToList();
        Console.WriteLine("ターンごとの度数:");
        Console.WriteLine();
        Console.WriteLine("| 決着T |" + string.Concat(ts.Select(x => $" {x} |")));
        Console.WriteLine("|---|" + string.Concat(ts.Select(_ => "--:|")));
        Console.WriteLine("| 勝ち |" + string.Concat(ts.Select(x => $" {win.Count(y => y == x)} |")));
        Console.WriteLine("| 負け |" + string.Concat(ts.Select(x => $" {lose.Count(y => y == x)} |")));
        Console.WriteLine();
    }

    static void Q05()
    {
        Console.WriteLine("## Q0-5 —— 第126期の生存Tの表を取り直す（第128期でドルガが変わった）");
        Console.WriteLine();
        var rows = new List<(string Name, int Stage, Formation F)>();
        var missing = new List<string>();
        foreach ((string n, int st, int _) in OffturnScan.WatchRows)
        {
            var hit = Presets.Compare.FirstOrDefault(b => b.Name == n);
            if (hit.F is null) { missing.Add(n); continue; }
            rows.Add((n, st, hit.F));
        }
        Console.WriteLine("推奨6行（第123〜126期と同じ行・同じ波。`OffturnScan.WatchRows` から引く）"
            + $"× seed 0..{Seeds - 1}。`Presets` に無い行 **{missing.Count} 件** ／ 測る行 **{rows.Count} 行**。");
        Console.WriteLine();
        if (rows.Count == 0) { Console.WriteLine("**走査が空。止める**（第117期）。"); Console.WriteLine(); return; }

        var perUnit = new Dictionary<string, (string Name, double Alive, double Fell, double Dealt, int N)>(StringComparer.Ordinal);
        foreach ((string name, int stage, Formation f) in rows)
        {
            var seats = f.Occupied().ToArray();
            var alive = new Dictionary<string, double>(StringComparer.Ordinal);
            var fell = new Dictionary<string, double>(StringComparer.Ordinal);
            var dealt = new Dictionary<string, double>(StringComparer.Ordinal);
            double settle = 0;
            foreach ((int _, UnitDef d) in seats) { alive[d.Id] = 0; fell[d.Id] = 0; dealt[d.Id] = 0; }
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[stage].Enemy, seed, verbose: false);
                settle += r.Turns;
                foreach ((int _, UnitDef d) in seats)
                {
                    if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
                    alive[d.Id] += t.LastActiveTurn;
                    dealt[d.Id] += t.DamageToEnemy;
                    if (t.Deaths >= 1) fell[d.Id]++;
                }
            }
            Console.WriteLine($"### {name} — 第{stage + 1}波（決着T {settle / Seeds:F2}）");
            Console.WriteLine();
            Console.WriteLine("| 席 | 駒 | HP | 攻 | 生存T | 落ち率 | 与ダメ/戦 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
            foreach ((int slot, UnitDef d) in seats)
            {
                double a = alive[d.Id] / Seeds, fl = fell[d.Id] * 100.0 / Seeds, dd = dealt[d.Id] / Seeds;
                Console.WriteLine($"| {Seat(slot)} | {d.Name} | {d.MaxHp} | {d.Attack} "
                    + $"| **{a:F2}** | {fl:F1}% | {dd:F1} |");
                var cur = perUnit.TryGetValue(d.Id, out var v)
                    ? v : (Name: d.Name, Alive: 0.0, Fell: 0.0, Dealt: 0.0, N: 0);
                perUnit[d.Id] = (d.Name, cur.Alive + a, cur.Fell + fl, cur.Dealt + dd, cur.N + 1);
            }
            Console.WriteLine();
        }
        Console.WriteLine("### 指示書 §0-2 が名指しした駒（推奨6行の在席ぶんの平均）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席 | 生存T | 落ち率 | 与ダメ/戦 | 第126期の値（指示書 §0-2） |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        foreach ((string id, string was) in new[]
                 { ("sora", "2.32T / 100%"), ("gald", "2.17T / 98.5%"),
                   ("hagi", "2.84T / — ／ 与ダメ 0.8"), ("tome", "3.20T / 100%"), ("sero", "— / —") })
        {
            if (!perUnit.TryGetValue(id, out var v))
            {
                UnitDef? d = UnitCatalog.All.FirstOrDefault(x => x.Id == id);
                Console.WriteLine($"| {d?.Name ?? id} | **0**（推奨6行に不在） | — | — | — | {was} |");
                continue;
            }
            Console.WriteLine($"| {v.Name} | {v.N} | **{v.Alive / v.N:F2}** | {v.Fell / v.N:F1}% "
                + $"| {v.Dealt / v.N:F1} | {was} |");
        }
        Console.WriteLine();
    }

    static void Q06()
    {
        Console.WriteLine("## Q0-6 —— 延命台をどう作るか");
        Console.WriteLine();
        Console.WriteLine("**既存の対照札を先に見る**（指示書 Q0-6）:");
        Console.WriteLine();
        Console.WriteLine("| 札 | 効き | 味方の保持者 | 延命台に使えるか |");
        Console.WriteLine("|---|---|--:|---|");
        foreach ((TraitId t, string what, string ok) in new (TraitId, string, string)[]
                 {
                     (TraitId.Regen, "毎ターン自分を回復（`ctx.Heal` を通る）",
                      "**×**——渇き（第三波）で止まり、`Stoic`（ガルド）にも届かない"),
                     (TraitId.Reprieve, "致死の一撃を**1戦に1度だけ** HP1 で耐える",
                      "**×**——1度きりでは決着まで守れない"),
                     (TraitId.Tempered, "`AtkBonus` が閾値を越えている間だけ被ダメ減",
                      "**×**——`AtkBonus` の供給が要る（対象の4枚は自己強化を1本も持たない）"),
                     (TraitId.Undying, "**ダメージでは HP が 1 未満にならない** ＋ ターン頭に満タンへ戻す",
                      "**○**——この期の器具として足した"),
                 })
            Console.WriteLine($"| `{t}` | {what} | {SurviveScan.Holders(t).Count} 枚 | {ok} |");
        Console.WriteLine();
        Console.WriteLine("**HP を極端に上げる形は採らなかった。** `MaxHp` を読む機構が盤上にあるので、"
            + "**延命以外のものまで動く**:");
        Console.WriteLine();
        var hp = Lines(_traits, "MaxHp")
            .Where(l => !l.TrimStart().StartsWith("///", StringComparison.Ordinal)).ToList();
        Console.WriteLine($"`BattleCore/Traits.cs` で `MaxHp` を読む行（コメントを除く）**{hp.Count} 件**:");
        Console.WriteLine();
        foreach (string l in hp) Console.WriteLine($"    {l.Trim()}");
        Console.WriteLine();
        Console.WriteLine("うち**他人の `MaxHp` を読むのは囃し立て（ヒサ）の1箇所**"
            + "（`隣接する最大HP最大の味方`）で、残りは自分の割合を見る型"
            + "——**逃亡兵セロの後退（`Hp * 3 > MaxHp * 2`）もここにいる**ので、"
            + "HP を膨らませると**セロ自身の起動条件が壊れる。**");
        Console.WriteLine();
        var clamp = Lines(_engine, "TraitId.Undying");
        Console.WriteLine($"採った形（`BattleEngine.cs`・**{clamp.Count} 件**）"
            + "——**猶予の直後・同じ出口**（`target.Hp -= amount` の直前）:");
        Console.WriteLine();
        foreach (string l in clamp) Console.WriteLine($"    {l.Trim()}");
        Console.WriteLine();
        Console.WriteLine("**出口のクランプだけでは足りない。** HP が 1 に張り付くと以降の被弾が "
            + "`amount <= 0` で早期に返り、**`OnDamaged` が呼ばれなくなる**"
            + "——庇い（ガルド）のように**被弾そのものが起動条件**の駒では、"
            + "延命したつもりで機構を止めてしまう（測りたいものの逆を測る）。"
            + "だから**ターン頭に満タンへ戻す**のを対にした（`UndyingTrait.OnTurnStart`）。");
        Console.WriteLine();
        Console.WriteLine("**戻しは `ctx.Heal` を通さない**——窓口を通すと渇きで止まり `Stoic` に届かず、"
            + "**いちばん測りたい2枚にだけ効かない器具**になる。"
            + "これは回復ではなく**台の初期化**で、`UnitTally.Healed` にも1点も乗らない。");
        Console.WriteLine();
    }

    static void Q07()
    {
        Console.WriteLine("## Q0-7 —— トメの鍵（標）の供給");
        Console.WriteLine();
        var w = Lines(_traits, "SetCounter(StatusKeys.Marked, 1)");
        var z = Lines(_traits, "SetCounter(StatusKeys.Marked, 0)");
        Console.WriteLine($"標を**立てる**行 **{w.Count} 件** ／ **落とす**行 **{z.Count} 件**（`Traits.cs`）:");
        Console.WriteLine();
        foreach (string l in w.Concat(z)) Console.WriteLine($"    {l.Trim()}");
        Console.WriteLine();
        Console.WriteLine("| 書き手 | 対象 | 周期 | トメの鍵になるか |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine("| 囃し立てのヒサ（`Marker`） | **隣接する最大HPの味方** | 開戦時1回 "
            + "| **×**——味方に付くので、敵の標を読むトメには届かない |");
        Console.WriteLine("| 逸らしのソラ（`Divert`） | **敵の現在HP最大 `TargetCount` 体** ＋ 自分 | 毎ターン "
            + "| **○**——敵に標を立てる唯一の書き手 |");
        Console.WriteLine("| 駆り立てのカリ（`Goad`） | **隣接する `CurrentAttack` 最大の味方** | 毎ターン "
            + "| **×**——味方に付く |");
        Console.WriteLine();
        Console.WriteLine("→ **指示書の「標の書き手はヒサ1枚」は誤り。書き手は3枚だが、"
            + "敵に立てるのはソラ1枚**——トメの鍵はソラにしか無い。");
        Console.WriteLine();
        var read = Lines(_engine, "RawCounter(StatusKeys.Marked)");
        Console.WriteLine($"`BattleEngine.cs` の `Marked` の参照 **{read.Count} 件**。"
            + "**トメが殴ると外れる**実装（`FinisherTrait.OnAfterAttack`）:");
        Console.WriteLine();
        foreach (string l in Lines(_traits, "target.SetCounter(StatusKeys.Marked, 0)"))
            Console.WriteLine($"    {l.Trim()}");
        Console.WriteLine();
        Console.WriteLine("`FinisherRule.Consume` は**ノブではなく対照**（`DivertRule.SelfMark` と同じ扱い）。");
        Console.WriteLine();

        var tRows = Presets.Compare
            .Where(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Tome.Id)).ToList();
        Console.WriteLine($"`compare` でトメを含む行 **{tRows.Count} 行**（うちソラを含む行 "
            + $"**{tRows.Count(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Sora.Id))} 行**）:");
        Console.WriteLine();
        foreach ((string n, Formation f) in tRows)
            Console.WriteLine($"- {n} — " + string.Join("・", f.Occupied().Select(o => o.Def.Name)));
        Console.WriteLine();
    }

    static void Q08()
    {
        Console.WriteLine("## Q0-8 —— ガルドの `Stoic` は HP を弾くか");
        Console.WriteLine();
        var acc = Lines(_engine, "AcceptsSupport");
        Console.WriteLine($"`Stoic` が効くのは `AcceptsSupport`（`ctx.Heal` と支援の窓口）で、"
            + $"`BattleEngine.cs` の参照 **{acc.Count} 件**:");
        Console.WriteLine();
        foreach (string l in acc) Console.WriteLine($"    {l.Trim()}");
        Console.WriteLine();
        Console.WriteLine($"`UnitDef.MaxHp` は**窓口ではなく数値**なので `Stoic` を通らない。"
            + $"ガルドの現行値は **HP {UnitCatalog.Gald.MaxHp} / 攻 {UnitCatalog.Gald.Attack} "
            + $"/ 速 {UnitCatalog.Gald.Speed}**。");
        Console.WriteLine("→ **段3 で `MaxHp` を上げるのは弾かれない**"
            + "（第126期に `Regen` が載らなかったのとは別の話）。");
        Console.WriteLine("同じ理由で、この期の器具（`Undying`）も**窓口を通さない**のでガルドに効く（Q0-6）。");
        Console.WriteLine();
    }

    static void Q09()
    {
        Console.WriteLine("## Q0-9 —— 過去に延命して起動を測った期が無いか");
        Console.WriteLine();
        string dir = Path.Combine(_root ?? ".", "design");
        if (!Directory.Exists(dir))
        {
            Console.WriteLine("**`design/` が無い。走査が空なので止める**（第117期）。");
            Console.WriteLine();
            return;
        }
        var files = Directory.GetFiles(dir, "*.md");
        Console.WriteLine($"`design/` の **{files.Length} ファイル**を走査:");
        Console.WriteLine();
        Console.WriteLine("| 語 | 件数 | ファイル |");
        Console.WriteLine("|---|--:|---|");
        foreach (string word in new[] { "延命", "不死", "落ちないように", "Undying", "Reprieve", "Tempered" })
        {
            int total = 0; var where = new List<string>();
            foreach (string f in files)
            {
                int c = File.ReadAllText(f).Split(word).Length - 1;
                if (c > 0) { total += c; where.Add(Path.GetFileName(f)); }
            }
            Console.WriteLine($"| `{word}` | {total} | "
                + (where.Count == 0 ? "—"
                   : string.Join("・", where.Take(5)) + (where.Count > 5 ? $" ほか {where.Count - 5} 件" : ""))
                + " |");
        }
        Console.WriteLine();
        Console.WriteLine("→ **第126期（`time`）が「時間を買う3案」を測っているが、"
            + "あれは代金つきの<u>機構</u>の案で、「代金ゼロで落ちなくしたら起動するか」は測っていない。**");
        Console.WriteLine("**同じ機構を別の名前で2度測らないこと**——この期の `Undying` は"
            + "**採否を持たない測定器**であって、案ではない。");
        Console.WriteLine();
    }

    // ==================================================================================
    // 段2 —— 延命台
    // ==================================================================================

    /// <summary>段2 の対象（第126期の実測で早く落ちた駒。指示書 §3-2 の表）。</summary>
    static readonly (UnitDef D, string Key, string Trigger)[] Targets =
    {
        (UnitCatalog.Sora, "逸らし",     "自前（毎ターン無条件）"),
        (UnitCatalog.Gald, "庇う",       "不要（味方が単体攻撃を受けるたび）"),
        (UnitCatalog.Hagi, "追い打ち",   "撃破（希少）"),
        (UnitCatalog.Tome, "止め",       "標（敵に立てるのはソラ1枚・殴ると外れる）"),
        (UnitCatalog.Sero, "後退＋狙撃", "自分の被弾（HP の 1/3）"),
    };

    /// <summary>その駒の「発火回数」。<b>実装から引いた計数を駒ごとに指定する</b>（A4）。</summary>
    static double Fires(UnitDef d, BattleResult r)
    {
        UnitTally? t = r.TallyByUnit.TryGetValue(d.Id, out UnitTally? v) ? v : null;
        if (d.Id == UnitCatalog.Sora.Id) return r.DivertFires;
        if (d.Id == UnitCatalog.Gald.Id) return t?.Intercepts ?? 0;
        if (d.Id == UnitCatalog.Hagi.Id) return t?.Attacks ?? 0;
        if (d.Id == UnitCatalog.Tome.Id) return r.FinisherFires;
        if (d.Id == UnitCatalog.Sero.Id) return t?.SniperSwings ?? 0;
        return t?.Interventions ?? 0;
    }

    static string FireLabel(UnitDef d)
    {
        if (d.Id == UnitCatalog.Sora.Id) return "`BattleResult.DivertFires`（外し＋焦点の1回の発火）";
        if (d.Id == UnitCatalog.Gald.Id) return "`UnitTally.Intercepts`（主目標を引き受けた回数）";
        if (d.Id == UnitCatalog.Hagi.Id) return "`UnitTally.Attacks`（**自分の手番では振らない型**なので全数が追い打ち）";
        if (d.Id == UnitCatalog.Tome.Id) return "`BattleResult.FinisherFires`（標持ちを狙って倍で振った回数）";
        if (d.Id == UnitCatalog.Sero.Id) return "`UnitTally.SniperSwings`（狙撃が成立したまま振った回数）";
        return "`UnitTally.Interventions`";
    }

    /// <summary>
    /// 段2 の集計。<b>クラス名に接頭辞を付けてある</b>——`derive rules` の索引は
    /// <c>(?:static|sealed) class (\w+)</c> で<b>入れ子のクラスまで拾い</b>、
    /// その名前 ＋ <c>"."</c> が現れたモードにこのファイル全体を結び付ける。
    /// <c>Acc</c> のような短い名前だと**無関係なモードに結び付いて `docs/rules.md` の
    /// `利用者` 列が汚れる**（実測で `pairs` が付いた）。第121期「引けているのに結ばれない」の裏返しで、
    /// <b>今度は結ばれ過ぎる</b>側。
    /// </summary>
    sealed class SurviveAcc
    {
        public double Fires, Dealt, Alive, Turns;
        public int Fell, Wins, N;
    }

    static void RunStage2()
    {
        Console.WriteLine("# 第129期 段2 —— 延命台（守れたら起動するか）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 survive run` の出力。");
        Console.WriteLine("**`Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` を1文字も触らない。**");
        Console.WriteLine("台は `compare` の行そのもの（対象駒が在席する行を機械で引く）で、"
            + "**V1 は対象駒の `UnitDef` に `Undying` を足した写しへ差し替えるだけ**。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 内容 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| V0（対照） | そのまま |");
        Console.WriteLine("| V1 | **対象駒だけ落ちないようにする**（`TraitId.Undying`。代金も上限も無い） |");
        Console.WriteLine();
        Console.WriteLine("**代金を付けない。閾値を掃引しない**（第118・126・127期と同じ）。素の効き方だけを見る。");
        Console.WriteLine();
        Console.WriteLine("> **判定: 延命しても発火回数と与ダメが増えない駒は、守っても意味がない。"
            + "段3 の対象から外す。**");
        Console.WriteLine();

        var verdicts = new List<(string Unit, int Rows, double F0, double F1, double D0, double D1, double A0, double A1)>();

        foreach ((UnitDef d, string key, string trig) in Targets)
        {
            var rows = Presets.Compare
                .Where(b => b.F.Occupied().Any(o => o.Def.Id == d.Id)).ToList();
            Console.WriteLine($"## {d.Name}（{key}・鍵の供給: {trig}）");
            Console.WriteLine();
            Console.WriteLine($"HP {d.MaxHp} / 攻 {d.Attack} / 速 {d.Speed} ／ "
                + $"`compare` の在席行 **{rows.Count} 行**。");
            Console.WriteLine();
            if (rows.Count == 0)
            {
                Console.WriteLine("**在席行が 0。走査が空なので止める**（第117期）。");
                Console.WriteLine();
                continue;
            }
            Console.WriteLine($"`発火` は {FireLabel(d)}。`生存T` は `UnitTally.LastActiveTurn` の平均。");
            Console.WriteLine();
            Console.WriteLine("| 編成 | 波 | 版 | 勝率 | 発火/戦 | 与ダメ(敵)/戦 | 生存T | 落ち率 | 決着T |");
            Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|");

            UnitDef live = Undy(d);
            var tot = new[] { new SurviveAcc(), new SurviveAcc() };
            foreach ((string name, Formation f) in rows)
            {
                Formation f1 = Swap(f, d, live);
                for (int w = 1; w < EnemyCatalog.Stages.Count; w++)
                {
                    var a = new[] { new SurviveAcc(), new SurviveAcc() };
                    for (int v = 0; v < 2; v++)
                    {
                        Formation ff = v == 0 ? f : f1;
                        for (int seed = 0; seed < Seeds; seed++)
                        {
                            BattleResult r = BattleEngine.Run(ff, EnemyCatalog.Stages[w].Enemy, seed, verbose: false);
                            a[v].N++; tot[v].N++;
                            if (r.PlayerWon) { a[v].Wins++; tot[v].Wins++; }
                            a[v].Turns += r.Turns; tot[v].Turns += r.Turns;
                            double fr = Fires(d, r);
                            a[v].Fires += fr; tot[v].Fires += fr;
                            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t))
                            {
                                a[v].Dealt += t.DamageToEnemy; tot[v].Dealt += t.DamageToEnemy;
                                a[v].Alive += t.LastActiveTurn; tot[v].Alive += t.LastActiveTurn;
                                if (t.Deaths >= 1) { a[v].Fell++; tot[v].Fell++; }
                            }
                        }
                    }
                    for (int v = 0; v < 2; v++)
                        Console.WriteLine($"| {(v == 0 ? name : "")} | {(v == 0 ? (w + 1).ToString() : "")} "
                            + $"| {(v == 0 ? "V0" : "**V1**")} | {a[v].Wins * 100.0 / a[v].N:F1}% "
                            + $"| {a[v].Fires / a[v].N:F2} | {a[v].Dealt / a[v].N:F1} "
                            + $"| {a[v].Alive / a[v].N:F2} | {a[v].Fell * 100.0 / a[v].N:F1}% "
                            + $"| {a[v].Turns / a[v].N:F2} |");
                }
            }
            Console.WriteLine();
            Console.WriteLine($"**通算**（{rows.Count} 行 × 第2〜5波 × seed 0..{Seeds - 1} ＝ {tot[0].N} 戦/版）:");
            Console.WriteLine();
            Console.WriteLine("| 版 | 勝率 | 発火/戦 | 与ダメ(敵)/戦 | 生存T | 落ち率 | 決着T |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
            for (int v = 0; v < 2; v++)
                Console.WriteLine($"| {(v == 0 ? "V0" : "**V1**")} | {tot[v].Wins * 100.0 / tot[v].N:F1}% "
                    + $"| {tot[v].Fires / tot[v].N:F2} | {tot[v].Dealt / tot[v].N:F1} "
                    + $"| {tot[v].Alive / tot[v].N:F2} | {tot[v].Fell * 100.0 / tot[v].N:F1}% "
                    + $"| {tot[v].Turns / tot[v].N:F2} |");
            Console.WriteLine();
            double f0 = tot[0].Fires / tot[0].N, f1v = tot[1].Fires / tot[1].N;
            double d0 = tot[0].Dealt / tot[0].N, d1 = tot[1].Dealt / tot[1].N;
            double a0 = tot[0].Alive / tot[0].N, a1 = tot[1].Alive / tot[1].N;
            Console.WriteLine($"**買った時間**: 生存T {a0:F2} → {a1:F2}（**+{a1 - a0:F2}T**）。");
            Console.WriteLine($"**発火**: {f0:F2} → {f1v:F2}（{(f0 <= 0 ? "—" : $"×{f1v / f0:F2}")}）"
                + (f1v <= 0 ? "。**0 回。起動していない。**" : "。"));
            Console.WriteLine($"**与ダメ(敵)**: {d0:F1} → {d1:F1}（{(d0 <= 0 ? "—" : $"×{d1 / d0:F2}")}）。");
            Console.WriteLine();
            Console.WriteLine($"**発火の密度**（発火 ÷ 生存T）: {(a0 <= 0 ? 0 : f0 / a0):F3} → "
                + $"{(a1 <= 0 ? 0 : f1v / a1):F3}"
                + $"（×{(a0 <= 0 || f0 <= 0 ? 0 : (f1v / Math.Max(1e-9, a1)) / (f0 / a0)):F2}）"
                + "——**1 なら発火は時間に比例している。1 を下回るほど、"
                + "時間ではなく鍵（発火の材料）が律速している。**");
            Console.WriteLine();
            Console.WriteLine("→ " + Verdict(f0, f1v, d0, d1));
            Console.WriteLine();
            verdicts.Add((d.Name, rows.Count, f0, f1v, d0, d1, a0, a1));
        }

        Console.WriteLine("## 判定のまとめ（§5-2 の条件1）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 鍵の供給 | 在席行 | 生存T V0 → V1 | 発火 V0 → V1 "
            + "| 密度 V0 → V1 | 与ダメ V0 → V1 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|");
        for (int i = 0; i < verdicts.Count; i++)
        {
            (string u, int rr, double f0, double f1, double d0, double d1, double a0, double a1) = verdicts[i];
            double p0 = a0 <= 0 ? 0 : f0 / a0, p1 = a1 <= 0 ? 0 : f1 / a1;
            Console.WriteLine($"| {u} | {Targets[i].Trigger} | {rr} "
                + $"| {a0:F2} → {a1:F2}（+{a1 - a0:F2}） | {f0:F2} → {f1:F2}（×{(f0 <= 0 ? 0 : f1 / f0):F2}） "
                + $"| {p0:F3} → {p1:F3}（**×{(p0 <= 0 ? 0 : p1 / p0):F2}**） "
                + $"| {d0:F1} → {d1:F1}（×{(d0 <= 0 ? 0 : d1 / d0):F2}） | {Verdict(f0, f1, d0, d1)} |");
        }
        Console.WriteLine();
        Console.WriteLine("**`密度` は 発火 ÷ 生存T。** `×1.00` なら発火は買った時間にきれいに比例している。"
            + "**下回るほど、律速しているのは時間ではなく鍵（発火の材料）**"
            + "——判定の線（発火・与ダメの両方が増えたか）は通っても、"
            + "**密度が落ちている駒は「守れば働く」ではなく「守っても働きが薄まる」**。");
        Console.WriteLine();
        Console.WriteLine("**発火回数と与ダメの両方が増えた駒だけが段3 の候補**（§5-2 の条件1）。"
            + "**1 が無ければ段3 に進まない。**");
        Console.WriteLine();
    }

    /// <summary>判定の線は**事前に固定した**（§3-2）: 発火と与ダメの**両方**が増えたか。</summary>
    static string Verdict(double f0, double f1, double d0, double d1)
    {
        if (f1 <= 0) return "**起動していない**（発火 0 回）";
        bool fUp = f1 > f0, dUp = d1 > d0;
        return fUp && dUp ? "**守れば起動する**（発火・与ダメとも増）"
             : fUp ? "発火は増えるが**与ダメは増えない**（段3 の対象から外す）"
             : dUp ? "与ダメは増えるが**発火は増えない**（段3 の対象から外す）"
             : "**守っても意味がない**（どちらも増えない）";
    }

    /// <summary><c>Undying</c> を足した写し。<b>元の <c>UnitDef</c> は1文字も触らない。</b></summary>
    static UnitDef Undy(UnitDef d) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = d.Traits.Concat(new[] { TraitId.Undying }).ToArray(),
        Pattern = d.Pattern, Actions = d.Actions,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor
    };

    static Formation Swap(Formation f, UnitDef from, UnitDef to)
    {
        Formation g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied()) if (d.Id == from.Id) g[slot] = to;
        return g;
    }

    // ==================================================================================
    // 段3 の逆算 —— `MaxHp` の掃引（**採否は拒否権で決める**）
    // ==================================================================================

    /// <summary>
    /// 対象駒の <c>MaxHp</c> だけを振って、<c>compare</c> 61 行 × 5 波を測り直す。
    /// <b>触るのは <c>MaxHp</c> 1つだけ</b>（攻撃力・速度・特性・席・`Presets` は1文字も触らない）。
    /// <b><c>UnitCatalog</c> は書き換えない</b>——写しを作って <c>Formation</c> を差し替えるだけ。
    /// </summary>
    static void Sweep(string id)
    {
        if (id.Length == 0) id = UnitCatalog.Gald.Id;
        UnitDef? baseDef = UnitCatalog.All.FirstOrDefault(d => d.Id == id);
        if (baseDef is null)
        {
            Console.WriteLine($"survive hp: 駒 `{id}` が `UnitCatalog.All` に無い。**止める**（第117期）。");
            return;
        }
        var rows = Presets.Compare.Where(b => b.F.Occupied().Any(o => o.Def.Id == id)).ToList();
        Console.WriteLine($"# 第129期 段3 の逆算 —— {baseDef.Name} の `MaxHp` 掃引");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 survive hp {id}` の出力。");
        Console.WriteLine($"**触るのは `MaxHp` だけ**（攻 {baseDef.Attack} / 速 {baseDef.Speed} / "
            + $"特性 {string.Join("・", baseDef.Traits)} は1文字も触らない）。");
        Console.WriteLine($"`compare` の在席行 **{rows.Count} / {Presets.Compare.Length} 行**"
            + $"（{rows.Count * 100.0 / Presets.Compare.Length:F1}%）／ "
            + $"主判定19行のうち **{Baseline.PrimaryRows.Count(n => rows.Any(r => r.Name == n))} 行**"
            + "（規約 (G4)）。");
        Console.WriteLine();

        int[] hps = { baseDef.MaxHp, baseDef.MaxHp + 10, baseDef.MaxHp + 20, baseDef.MaxHp + 30 };
        var cell = new Dictionary<int, double[,]>();
        var alive = new Dictionary<int, double>();
        var fires = new Dictionary<int, double>();
        var dealt = new Dictionary<int, double>();

        foreach (int hp in hps)
        {
            UnitDef d = WithHp(baseDef, hp);
            var g = new double[Presets.Compare.Length, EnemyCatalog.Stages.Count];
            double av = 0, fi = 0, de = 0; int n = 0;
            for (int bi = 0; bi < Presets.Compare.Length; bi++)
            {
                (string name, Formation f0) = Presets.Compare[bi];
                Formation f = Swap(f0, baseDef, d);
                bool here = rows.Any(r => r.Name == name);
                for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false);
                        if (r.PlayerWon) wins++;
                        if (!here || w == 0) continue;
                        n++;
                        fi += Fires(baseDef, r);
                        if (r.TallyByUnit.TryGetValue(id, out UnitTally? t))
                        { av += t.LastActiveTurn; de += t.DamageToEnemy; }
                    }
                    g[bi, w] = wins * 100.0 / Seeds;
                }
            }
            cell[hp] = g;
            alive[hp] = n == 0 ? 0 : av / n;
            fires[hp] = n == 0 ? 0 : fi / n;
            dealt[hp] = n == 0 ? 0 : de / n;
        }

        int b0 = baseDef.MaxHp;
        Console.WriteLine($"## 在席行での効き（第2〜5波・{rows.Count} 行 × seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`発火` は {FireLabel(baseDef)}。`密度` は 発火 ÷ 生存T。");
        Console.WriteLine();
        Console.WriteLine("| `MaxHp` | 生存T | 買った時間 | 発火/戦 | 密度 | 与ダメ(敵)/戦 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|");
        foreach (int hp in hps)
            Console.WriteLine($"| {hp}{(hp == b0 ? "（現行）" : "")} | {alive[hp]:F2} "
                + $"| {(hp == b0 ? "—" : $"+{alive[hp] - alive[b0]:F2}T")} | {fires[hp]:F2} "
                + $"| {(alive[hp] <= 0 ? 0 : fires[hp] / alive[hp]):F3} | {dealt[hp]:F1} |");
        Console.WriteLine();

        Console.WriteLine("## 盤面（`compare` 61 行 × 5 波）");
        Console.WriteLine();
        Console.WriteLine("`主判定` は `Baseline.PrimaryRows` 19 行の平均、`歯止め` は "
            + $"{Baseline.PrimaryFifthFloor:F1}%（主判定19行の第五波）。"
            + "`拒否権3` は**いずれかの波で −10.0pt 以上落ちた行**の数。"
            + "`余波` は**その駒を含まない行**で動いたセルの数（A6。**0 でなければならない**）。"
            + "`情報セル` は第2〜5波で 0% でも 100% でもないセルの合計（規約 (G14)）。");
        Console.WriteLine();
        Console.WriteLine("| `MaxHp` | 主判定 第2〜5波 | 第五波 | 余裕 | 全61行 第2〜5波 "
            + "| 拒否権3 | 余波 | 情報セル |");
        Console.WriteLine("|--:|---|--:|--:|---|--:|--:|--:|");
        foreach (int hp in hps)
        {
            double[,] g = cell[hp], g0 = cell[b0];
            var pri = new double[EnemyCatalog.Stages.Count];
            var all = new double[EnemyCatalog.Stages.Count];
            int pn = 0;
            for (int bi = 0; bi < Presets.Compare.Length; bi++)
            {
                bool isPri = Baseline.PrimaryRows.Contains(Presets.Compare[bi].Name);
                if (isPri) pn++;
                for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                { all[w] += g[bi, w]; if (isPri) pri[w] += g[bi, w]; }
            }
            int veto = 0, spill = 0, info = 0;
            for (int bi = 0; bi < Presets.Compare.Length; bi++)
            {
                bool here = rows.Any(r => r.Name == Presets.Compare[bi].Name);
                bool bad = false;
                for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                {
                    if (g[bi, w] - g0[bi, w] <= -10.0) bad = true;
                    if (!here && Math.Abs(g[bi, w] - g0[bi, w]) > 1e-9) spill++;
                    if (w >= 1 && g[bi, w] > 0.0 && g[bi, w] < 100.0) info++;
                }
                if (bad) veto++;
            }
            double fifth = pri[EnemyCatalog.Stages.Count - 1] / pn;
            Console.WriteLine($"| {hp}{(hp == b0 ? "（現行）" : "")} | "
                + string.Join(" / ", Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
                    .Select(w => $"{pri[w] / pn:F1}"))
                + $" | {fifth:F1}% | {fifth - Baseline.PrimaryFifthFloor:+0.0;-0.0}pt | "
                + string.Join(" / ", Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
                    .Select(w => $"{all[w] / Presets.Compare.Length:F1}"))
                + $" | {veto} | {spill} | {info} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 動いた行（現行との差・いずれかの波で ±0.5pt 以上）");
        Console.WriteLine();
        foreach (int hp in hps.Skip(1))
        {
            Console.WriteLine($"### `MaxHp` {b0} → {hp}");
            Console.WriteLine();
            Console.WriteLine("| 編成 |" + string.Concat(Enumerable.Range(0, EnemyCatalog.Stages.Count)
                .Select(i => $" 第{i + 1}波 |")) + " 主判定 |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, EnemyCatalog.Stages.Count)
                .Select(_ => "---:|")) + ":-:|");
            int moved = 0;
            for (int bi = 0; bi < Presets.Compare.Length; bi++)
            {
                var ds = new double[EnemyCatalog.Stages.Count];
                bool any = false;
                for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                {
                    ds[w] = cell[hp][bi, w] - cell[b0][bi, w];
                    if (Math.Abs(ds[w]) >= 0.5) any = true;
                }
                if (!any) continue;
                moved++;
                Console.WriteLine($"| {Presets.Compare[bi].Name} |"
                    + string.Concat(ds.Select(x => Math.Abs(x) < 1e-9 ? " ±0.0 |" : $" {x:+0.0;-0.0} |"))
                    + (Baseline.PrimaryRows.Contains(Presets.Compare[bi].Name) ? " ○ |" : "  |"));
            }
            Console.WriteLine();
            Console.WriteLine($"**動いた行 {moved} 行。**");
            Console.WriteLine();
        }
    }

    static UnitDef WithHp(UnitDef d, int hp) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = hp, Attack = d.Attack, Speed = d.Speed,
        Traits = d.Traits, Pattern = d.Pattern, Actions = d.Actions,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor
    };

    // ==================================================================================
    // 自己検査（規約 (G8) の必須4項目 ＋ この期の (5)(6)）
    // ==================================================================================
    static void Check(string before)
    {
        Console.WriteLine("# 第129期 —— 自己検査");
        Console.WriteLine();

        string path = before.Length > 0 ? before : Path.Combine(_root ?? ".", "docs", "balance.md");
        Console.WriteLine($"## (1) `compare` 305 セルが `{Path.GetFileName(path)}` と 0 件");
        Console.WriteLine();
        if (!File.Exists(path)) Console.WriteLine($"**`{path}` が無い。止める**（第117期）。");
        else
        {
            var want = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (string l in File.ReadAllLines(path))
            {
                if (!l.StartsWith("| ", StringComparison.Ordinal) || !l.Contains('%')) continue;
                string[] c = l.Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length < 8) continue;
                want[c[1]] = c.Skip(2).Take(5).ToArray();
            }
            int cells = 0, bad = 0, miss = 0;
            foreach ((string name, Formation f) in Presets.Compare)
            {
                if (!want.TryGetValue(name, out string[]? exp))
                { miss++; Console.WriteLine($"- 行名が引けない: {name}"); continue; }
                for (int w = 0; w < EnemyCatalog.Stages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                        if (BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                    string got = $"{wins * 100.0 / Seeds:F1}%";
                    cells++;
                    if (got != exp[w]) { bad++; Console.WriteLine($"- **差分** {name} 第{w + 1}波: {exp[w]} → {got}"); }
                }
            }
            Console.WriteLine($"→ **{cells} セル中 {bad} 件差分**（引けなかった行 {miss}）"
                + (bad == 0 && miss == 0 ? "。**0 件**。" : "。**止めて報告する。**"));
        }
        Console.WriteLine();

        Console.WriteLine("## (2) この期はノブを1本も足していない");
        Console.WriteLine();
        Console.WriteLine("段1・段2 は**規則もノブも 0 本**。器具（`Undying`）は札で、"
            + "強度のノブを持たない（`Reprieve` と同じ）。`docs/rules.md` の差分で示す。");
        Console.WriteLine();

        Console.WriteLine("## (3) `ctx.PickOne` を新たに使っていない（第89期 (h)）");
        Console.WriteLine();
        Console.WriteLine($"`PickOne` の出現 **{Lines(_traits, "PickOne").Count + Lines(_engine, "PickOne").Count} 件**。"
            + "`UndyingTrait` / `FallenStarters` / `SniperSwings` は**1件も呼ばない**（どれも乱数を引かない）。");
        Console.WriteLine();

        Console.WriteLine("## (4) 器具は保持者がいなければ不活性");
        Console.WriteLine();
        Console.WriteLine($"- `Undying` の `UnitCatalog.All` の保持者 **{SurviveScan.Holders(TraitId.Undying).Count} 枚**");
        Console.WriteLine($"- `EnemyCatalog.Stages` 側の保持者 **{EnemyHolders(TraitId.Undying)} 枚**");
        Console.WriteLine();

        Console.WriteLine("## (5) 免除は `OnDeath` を持つ味方だけ（A3）");
        Console.WriteLine();
        Console.WriteLine($"- `OnDeath` を上書きする札 **{SurviveScan.DeathTraits.Count} 本**"
            + $"（{SurviveScan.NameList(SurviveScan.DeathTraits)}）");
        Console.WriteLine($"- `OnAnyDeath` / `OnAllyDeath` **{SurviveScan.OtherDeathTraits.Count} 本**"
            + $"（{SurviveScan.NameList(SurviveScan.OtherDeathTraits)}）——**免除に 0 本使っている**");
        var overlap = SurviveScan.DeathTraits.Intersect(SurviveScan.OtherDeathTraits).ToList();
        Console.WriteLine($"- 両方を上書きする札 **{overlap.Count} 本**"
            + (overlap.Count == 0 ? "" : $"（{SurviveScan.NameList(overlap)}）"
                + "——**`OnDeath` 側に入るので免除される。報告書に書く。**"));
        Console.WriteLine();

        Console.WriteLine("## (6) 決定性（同じ seed で 2 回回して一致）");
        Console.WriteLine();
        int diff = 0, n = 0;
        foreach ((string _, Formation f) in Presets.Compare.Take(10))
            for (int seed = 0; seed < 20; seed++)
            {
                BattleResult a = BattleEngine.Run(f, EnemyCatalog.Stages[3].Enemy, seed, verbose: false);
                BattleResult b = BattleEngine.Run(f, EnemyCatalog.Stages[3].Enemy, seed, verbose: false);
                n++;
                if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns
                    || !a.PlayerStarterFallen.SequenceEqual(b.PlayerStarterFallen)) diff++;
            }
        Console.WriteLine($"→ {n} 戦 × 2 回で **{diff} 件差分**。");
        Console.WriteLine();
    }

    static int EnemyHolders(TraitId t)
    {
        int n = 0;
        foreach (EnemyCatalog.Stage st in EnemyCatalog.Stages)
            foreach ((int _, UnitDef d) in st.Enemy.Occupied())
                if (d.Traits.Contains(t)) n++;
        return n;
    }
}
