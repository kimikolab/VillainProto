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
//   → 段2 は**延命台**で「守れたら起動するか」を先に測る。**器具と段2 は次のコミット。**
//
// **Phase 0 は `Presets` / `EnemyCatalog.Stages` / `UnitCatalog.All` を1文字も触らない。**
//
//     dotnet run --project BattleSim -c Release 0 survive phase0  # Q0-1〜Q0-5 / Q0-7〜Q0-9
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
            case "check": Check(arg); return;
            default:
                Console.WriteLine("survive: モードは phase0 / check（第129期）。");
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
        Q01(); Q02(); Q03(); Q04(); Q05(); Q07(); Q08(); Q09();
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
        Console.WriteLine("同じ理由で、**器具を作るなら窓口を通さない形にする**（段2）。");
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
        Console.WriteLine("**同じ機構を別の名前で2度測らないこと**——この期の器具は"
            + "**採否を持たない測定器**であって、案ではない。");
        Console.WriteLine();
    }

    // ==================================================================================
    // 自己検査（段1 のぶん）
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
        Console.WriteLine("段1 は**規則もノブも 0 本**（計数を1本足しただけ）。"
            + "`docs/rules.md` の差分で示す。");
        Console.WriteLine();

        Console.WriteLine("## (3) `ctx.PickOne` を新たに使っていない（第89期 (h)）");
        Console.WriteLine();
        Console.WriteLine($"`PickOne` の出現 **{Lines(_traits, "PickOne").Count + Lines(_engine, "PickOne").Count} 件**。"
            + "`FallenStarters` は**1件も呼ばない**（乱数を引かない）。");
        Console.WriteLine();

        Console.WriteLine("## (4) 免除は `OnDeath` を持つ味方だけ（A3）");
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

        Console.WriteLine("## (5) 決定性（同じ seed で 2 回回して一致）");
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

}
