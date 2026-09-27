using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// =====================================================================================
// scorch モード（第219期） —— 燃焼で脆くなる（＋ミオを M5 に規定化）
//
// 指示書は design/PHASE219_SCORCH_SPEC.md ／ 報告は design/PHASE219_SCORCH.md。
// **線は置かない**（燃焼の版の採否はポンが遊んで決める）。台は `Presets` に足さない（この診断のローカル）。
//
//     dotnet run --project BattleSim -c Release 0 scorch phase0   # Q0-1〜Q0-6（実装前・盤面はミオ M5 の規定化の後）
//     dotnet run --project BattleSim -c Release 0 scorch run      # 表A〜E（実装後）
//     dotnet run --project BattleSim -c Release 0 scorch check    # 自己検査（受け入れ 2〜4）
// =====================================================================================

static partial class ScorchDiag
{
    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(arg); return;
            default:
                Console.WriteLine("scorch: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunImpl(string arg);
    static partial void CheckImpl(string arg);

    internal const int MeasSeeds = 200;
    internal static readonly int[] Waves25 = { 1, 2, 3, 4 };
    internal static BossRule BossOf(EnemyScaleRule r) => new(false) { Scale = r };

    // =================================================================================
    // 台（指示書 §6.2）
    // =================================================================================

    internal static List<UnitDef> H1Members() => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, UnitCatalog.Guza, UnitCatalog.Kubi };
    internal static List<UnitDef> H2Members() => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, UnitCatalog.Tou, UnitCatalog.Kugu };

    /// <summary>ポンの X字の席（第216期の台A・H1 の顔ぶれ）。</summary>
    internal static Formation PonX() => Formation.Build(
        front1: UnitCatalog.Kubi, front3: UnitCatalog.Guza, center: UnitCatalog.Beni, back1: UnitCatalog.Mio, back3: UnitCatalog.Kata);
    /// <summary>ポンのパターン2の席（第216期の台B・<b>クビではなくスィド</b>——H1 と顔ぶれが1枚違う参考）。</summary>
    internal static Formation PonP2() => Formation.BuildDiamond(
        a: UnitCatalog.Kata, b: UnitCatalog.Guza, c: UnitCatalog.Beni, d: UnitCatalog.Sid, e: UnitCatalog.Mio);

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    /// <summary>H3 ＝ `compare` でホタ・ボルグ・ヒヨのどれかを含む行（元の席）。</summary>
    internal static List<(string Name, Formation F)> H3Rows()
        => CompareBuilds().Where(r => Has(r.F, "hota") || Has(r.F, "borg") || Has(r.F, "hiyo")).Select(r => (r.Name, r.F)).ToList();

    /// <summary>H4 ＝ `compare` でツギを含む行（元の席）。ツギとベニが同じ行にいる行は `compare` に無い（Q0-6）。</summary>
    internal static List<(string Name, Formation F)> H4Rows()
        => CompareBuilds().Where(r => Has(r.F, "tsugi")).Select(r => (r.Name, r.F)).ToList();

    /// <summary>
    /// H4' ＝ ベニのいる `compare` の2行（`毒+耐久` ／ `毒+ベニ+ラウ`）で、<b>ガルドの席にツギ</b>（測る前に固定した規則）。
    /// 2行ともガルドを含み、ガルドは毒も火も書かない壁なので、書き手を1枚も抜かずに「ツギとベニが同じ隊にいる」を作れる。
    /// </summary>
    internal static List<(string Name, Formation F)> H4PrimeRows()
        => CompareBuilds().Where(r => Has(r.F, "beni") && Has(r.F, "gald"))
            .Select(r => (r.Name.Split(' ')[0] + "（ガルド→ツギ）", Swap(r.F, "gald", UnitCatalog.Tsugi))).ToList();

    internal static Formation Swap(Formation f, string id, UnitDef to)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == id ? to : d;
        return g;
    }

    /// <summary>
    /// 席の総当たり（指示書 §6.2: 倍率 150・版 <paramref name="ember"/> で選ぶ）。第2〜5波 × seed 1000..1049 の<b>勝ち数</b>が最大の並び。
    /// <b>同値は 全員生存の数 → 決着ターンの和（少ない方）→ 列挙順</b>（R276: 天井で同値が大きい。測る前に固定した割り方）。
    /// </summary>
    internal static (Formation Best, int Wins, int Ties, int TiesAfter) PickSeat(List<UnitDef> members, FormationShape sh, EmberRule ember, EnemyScaleRule scale)
    {
        var all = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation { Shape = sh };
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            all.Add(f);
        }
        var win = new int[all.Count]; var surv = new int[all.Count]; var turns = new long[all.Count];
        var boss = BossOf(scale);
        Parallel.For(0, all.Count * 4, j =>
        {
            int i = j / 4, st = 1 + j % 4;
            int w = 0, a = 0; long t = 0;
            for (int s = 1000; s < 1050; s++)
            {
                BattleResult r = BattleEngine.Run(all[i], EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss, ember: ember);
                if (r.PlayerWon) { w++; t += r.Turns; if (r.PlayerStarterFallen.Count == 0) a++; } else t += 31;
            }
            Interlocked.Add(ref win[i], w); Interlocked.Add(ref surv[i], a); Interlocked.Add(ref turns[i], t);
        });
        int best = 0;
        for (int i = 1; i < all.Count; i++)
            if (win[i] > win[best] || (win[i] == win[best] && (surv[i] > surv[best] || (surv[i] == surv[best] && turns[i] < turns[best])))) best = i;
        int ties = win.Count(x => x == win[best]);
        int tiesAfter = Enumerable.Range(0, all.Count).Count(i => win[i] == win[best] && surv[i] == surv[best] && turns[i] == turns[best]);
        return (all[best], win[best], ties, tiesAfter);
    }

    // =================================================================================
    // 集計
    // =================================================================================

    internal sealed class SAgg
    {
        public readonly long[] Wins = new long[5], AllSurv = new long[5], N = new long[5];
        public long WinTurns, WinN;
        /// <summary>燃焼が1度でも（相手陣営に）付いた戦 ／ 味方に付いた戦。</summary>
        public readonly long[] FoeLitBattles = new long[5], AllyLitBattles = new long[5];
        public readonly long[] UnitTurns = new long[2], BurnUnitTurns = new long[2], TurnsAnyBurn = new long[2];
        public readonly long[] TurnsByWave = new long[5], FoeBurnUT = new long[5], FoeUT = new long[5], FoeAnyTurns = new long[5];
        public long Turns;
        public readonly long[][] BurnByTurn = { new long[31], new long[31] }, AliveByTurn = { new long[31], new long[31] };
        public readonly Dictionary<string, long> IgniteFoe = new(), IgniteAlly = new();
        public readonly long[,] Extra = new long[2, 8], Hits = new long[2, 8], Base = new long[2, 8];
        public long InverseBase, InverseExtra, InverseHits, PlankTimesBrittle, PyreExtra;
        public readonly UnitTally P = new(), E = new();
        public readonly Dictionary<string, UnitTally> ByUnit = new();

        public void Take(BattleResult r, int st, HashSet<string> playerIds)
        {
            N[st]++;
            if (r.PlayerWon)
            {
                Wins[st]++; WinTurns += r.Turns; WinN++;
                if (r.PlayerStarterFallen.Count == 0) AllSurv[st]++;
            }
            foreach (var (id, t) in r.TallyByUnit)
            {
                bool pl = playerIds.Contains(id);
                (pl ? P : E).Add(t);
                if (pl) { if (!ByUnit.TryGetValue(id, out var u)) ByUnit[id] = u = new UnitTally(); u.Add(t); }
            }
            BrittleLedger? b = r.Brittle;
            if (b is null) return;
            Turns += b.Turns; TurnsByWave[st] += b.Turns;
            for (int i = 0; i < 2; i++)
            {
                UnitTurns[i] += b.UnitTurns[i]; BurnUnitTurns[i] += b.BurnUnitTurns[i]; TurnsAnyBurn[i] += b.TurnsAnyBurn[i];
                for (int t = 0; t < 31; t++) { BurnByTurn[i][t] += b.BurnByTurn[i][t]; AliveByTurn[i][t] += b.AliveByTurn[i][t]; }
                for (int k = 0; k < 8; k++) { Extra[i, k] += b.Extra[i, k]; Hits[i, k] += b.Hits[i, k]; Base[i, k] += b.Base[i, k]; }
            }
            FoeBurnUT[st] += b.BurnUnitTurns[0]; FoeUT[st] += b.UnitTurns[0]; FoeAnyTurns[st] += b.TurnsAnyBurn[0];
            // 味方（player）が燃やした敵 ＝ 書き手が味方の駒で、相手陣営に付けた分。
            long foeLit = b.IgniteFoe.Where(kv => playerIds.Contains(kv.Key) || kv.Key == "-e").Sum(kv => kv.Value);
            long allyLit = b.IgniteAlly.Where(kv => playerIds.Contains(kv.Key)).Sum(kv => kv.Value)
                         + b.IgniteFoe.Where(kv => !playerIds.Contains(kv.Key) && kv.Key != "-e").Sum(kv => kv.Value);
            if (foeLit > 0) FoeLitBattles[st]++;
            if (allyLit > 0) AllyLitBattles[st]++;
            foreach (var (k, v) in b.IgniteFoe) IgniteFoe[k] = IgniteFoe.GetValueOrDefault(k) + v;
            foreach (var (k, v) in b.IgniteAlly) IgniteAlly[k] = IgniteAlly.GetValueOrDefault(k) + v;
            InverseBase += b.InverseBase; InverseExtra += b.InverseExtra; InverseHits += b.InverseHits; PlankTimesBrittle += b.PlankTimesBrittle; PyreExtra += b.PyreExtra;
        }

        public void Merge(SAgg o)
        {
            for (int i = 0; i < 5; i++)
            {
                Wins[i] += o.Wins[i]; AllSurv[i] += o.AllSurv[i]; N[i] += o.N[i];
                FoeLitBattles[i] += o.FoeLitBattles[i]; AllyLitBattles[i] += o.AllyLitBattles[i];
                TurnsByWave[i] += o.TurnsByWave[i]; FoeBurnUT[i] += o.FoeBurnUT[i]; FoeUT[i] += o.FoeUT[i]; FoeAnyTurns[i] += o.FoeAnyTurns[i];
            }
            WinTurns += o.WinTurns; WinN += o.WinN; Turns += o.Turns;
            for (int i = 0; i < 2; i++)
            {
                UnitTurns[i] += o.UnitTurns[i]; BurnUnitTurns[i] += o.BurnUnitTurns[i]; TurnsAnyBurn[i] += o.TurnsAnyBurn[i];
                for (int t = 0; t < 31; t++) { BurnByTurn[i][t] += o.BurnByTurn[i][t]; AliveByTurn[i][t] += o.AliveByTurn[i][t]; }
                for (int k = 0; k < 8; k++) { Extra[i, k] += o.Extra[i, k]; Hits[i, k] += o.Hits[i, k]; Base[i, k] += o.Base[i, k]; }
            }
            foreach (var (k, v) in o.IgniteFoe) IgniteFoe[k] = IgniteFoe.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.IgniteAlly) IgniteAlly[k] = IgniteAlly.GetValueOrDefault(k) + v;
            InverseBase += o.InverseBase; InverseExtra += o.InverseExtra; InverseHits += o.InverseHits; PlankTimesBrittle += o.PlankTimesBrittle; PyreExtra += o.PyreExtra;
            P.Add(o.P); E.Add(o.E);
            foreach (var (k, v) in o.ByUnit) { if (!ByUnit.TryGetValue(k, out var u)) ByUnit[k] = u = new UnitTally(); u.Add(v); }
        }

        public long Battles => N.Sum();
        public double WinPct(int st) => N[st] == 0 ? double.NaN : 100.0 * Wins[st] / N[st];
        public double Mean25 => Waves25.Average(WinPct);
        public double AllSurvPct => 100.0 * AllSurv.Sum() / Math.Max(1, N.Sum());
        public double MeanWinT => WinN == 0 ? double.NaN : (double)WinTurns / WinN;
        public double Per(long x) => (double)x / Math.Max(1, Battles);
    }

    internal static SAgg Measure(Formation f, EnemyScaleRule scale, EmberRule ember, int seed0 = 0, int seeds = MeasSeeds, IReadOnlyList<int>? waves = null)
    {
        waves ??= Waves25;
        var playerIds = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        var boss = BossOf(scale);
        var total = new SAgg();
        var gate = new object();
        int nw = waves.Count;
        Parallel.For(0, nw * seeds, () => new SAgg(), (j, _, local) =>
        {
            int st = waves[j / seeds], s = seed0 + j % seeds;
            local.Take(BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss, ember: ember), st, playerIds);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    internal static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    internal static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");
    internal static string P1(long a, long b) => b == 0 ? "—" : (100.0 * a / b).ToString("F1");
    internal static string D1(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;±0.0");
    internal static string Short(UnitDef d) => d.Name.Split('の').Last();
    internal static string ShapeName(FormationShape s) => s == FormationShape.X ? "X字" : "P2";
    internal static string SeatsNamed(Formation f) => string.Join(" ／ ", f.Occupied().Select(o => f.Shape.FrameNames[o.Slot] + ":" + Short(o.Def)));
    internal static string NameOf(string id) => id == "-p" ? "（書き手なし・味方に付いた）" : id == "-e" ? "（書き手なし・敵に付いた）"
        : UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id)?.Name
          ?? EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied()).Select(o => o.Def).FirstOrDefault(d => d.Id == id)?.Name ?? id;

    static string FindRoot()
    {
        string d = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(d, "CLAUDE.md"))) d = Path.GetDirectoryName(d) ?? throw new InvalidOperationException("CLAUDE.md が見つからない");
        return d;
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        string root = FindRoot();
        string eng = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
        string trs = File.ReadAllText(Path.Combine(root, "BattleCore", "Traits.cs"));
        Console.WriteLine("# 第219期 Phase 0 —— 燃焼で脆くなる（盤面はミオ M5 の規定化の後・脆さは未実装）");
        Console.WriteLine();
        Console.WriteLine("- ミオの札: " + string.Join(", ", UnitCatalog.Mio.Traits));
        Console.WriteLine("- ベニの札: " + string.Join(", ", UnitCatalog.Beni.Traits));
        Console.WriteLine("- カタの札: " + string.Join(", ", UnitCatalog.Kata.Traits));
        Console.WriteLine();

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1 被ダメの口");
        Console.WriteLine();
        int CountCalls(string src, string pat) => Regex.Matches(src, pat).Count;
        Console.WriteLine("| ファイル | `ApplyDamage(` の呼び出し | `.Hp -=` の直書き | `InverseHeal(` |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine("| BattleEngine.cs | " + CountCalls(eng, @"(?<!void )\bApplyDamage\(") + " | " + CountCalls(eng, @"\.Hp\s*-=") + " | " + CountCalls(eng, @"(?<!void )\bInverseHeal\(") + " |");
        Console.WriteLine("| Traits.cs | " + CountCalls(trs, @"\bApplyDamage\(") + " | " + CountCalls(trs, @"\.Hp\s*-=") + " | " + CountCalls(trs, @"\bInverseHeal\(") + " |");
        Console.WriteLine();
        Console.WriteLine("`.Hp -=` の行:");
        foreach (var (file, src) in new[] { ("BattleEngine.cs", eng), ("Traits.cs", trs) })
        {
            string[] lines = src.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (Regex.IsMatch(lines[i], @"\.Hp\s*-=") && !lines[i].TrimStart().StartsWith("//") && !lines[i].TrimStart().StartsWith("///"))
                    Console.WriteLine("- `" + file + ":" + (i + 1) + "` " + lines[i].Trim());
        }
        Console.WriteLine();
        Console.WriteLine("`InverseHeal(` の呼び出し（反転で回復に化ける刻み・放電の口）:");
        {
            string[] lines = eng.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if (Regex.IsMatch(lines[i], @"(?<!void )\bInverseHeal\(") && !lines[i].TrimStart().StartsWith("//"))
                    Console.WriteLine("- `BattleEngine.cs:" + (i + 1) + "` " + lines[i].Trim());
        }
        Console.WriteLine();
        Console.WriteLine("`relayed: true` ／ `hexShare: true` で呼ぶ口（一撃の重さが既に決まった後の中継・共有）:");
        {
            string[] lines = eng.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                if ((lines[i].Contains("relayed: true") || lines[i].Contains("hexShare: true")) && lines[i].Contains("ApplyDamage"))
                    Console.WriteLine("- `BattleEngine.cs:" + (i + 1) + "` " + lines[i].Trim());
        }
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4 燃焼（`StatusKeys.Burn`）を読む札");
        Console.WriteLine();
        Console.WriteLine("| 札のクラス | 行 | TraitId | 保持者（味方 `All`） | 保持者（敵・5波） |");
        Console.WriteLine("|---|--:|---|---|---|");
        {
            string[] lines = trs.Split('\n');
            string cls = "?"; int clsLine = 0;
            var seen = new HashSet<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                var m = Regex.Match(lines[i], @"^public (?:sealed |static |readonly )?(?:class|record struct|struct) (\w+)");
                if (m.Success) { cls = m.Groups[1].Value; clsLine = i; }
                if (!lines[i].Contains("StatusKeys.Burn") || lines[i].TrimStart().StartsWith("///")) continue;
                if (!seen.Add(cls)) continue;
                string id = "—";
                for (int k = clsLine; k < Math.Min(lines.Length, clsLine + 400); k++)
                {
                    var mm = Regex.Match(lines[k], @"TraitId Id => TraitId\.(\w+)");
                    if (mm.Success) { id = mm.Groups[1].Value; break; }
                    if (k > clsLine && Regex.IsMatch(lines[k], @"^public (?:sealed |static |readonly )?(?:class|record struct|struct) ")) break;
                }
                string ally = "—", foe = "—";
                if (id == "—") ally = "（札ではない・engine の規則の器）";
                else if (Enum.TryParse(id, out TraitId tid))
                {
                    ally = string.Join("・", UnitCatalog.All.Where(d => d.Traits.Contains(tid)).Select(Short).DefaultIfEmpty("0 枚"));
                    foe = string.Join("・", EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied()).Select(o => o.Def)
                        .Where(d => d.Traits.Contains(tid)).Select(d => d.Name).Distinct().DefaultIfEmpty("0 体"));
                }
                Console.WriteLine("| `" + cls + "` | " + (i + 1) + " | " + id + " | " + ally + " | " + foe + " |");
            }
        }
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3 燃焼の在り方（`compare` 61 行 × 第2〜5波 × seed 0..199・倍率 115・M5 の後）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var aggs = new SAgg[rows.Length];
        for (int i = 0; i < rows.Length; i++) aggs[i] = Measure(rows[i].F, EnemyScaleRule.Adopted, EmberRule.Default);
        var all = new SAgg();
        foreach (var a in aggs) all.Merge(a);
        Console.WriteLine("| 波 | 敵の駒×ターンのうち燃えていた % | 1体でも燃えていたターン % | 敵に火が付いた戦 % | 味方に火が付いた戦 % |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (int st in Waves25)
            Console.WriteLine("| 第" + (st + 1) + "波 | " + P1(all.FoeBurnUT[st], all.FoeUT[st]) + " | " + P1(all.FoeAnyTurns[st], all.TurnsByWave[st]) + " | "
                              + P1(all.FoeLitBattles[st], all.N[st]) + " | " + P1(all.AllyLitBattles[st], all.N[st]) + " |");
        Console.WriteLine("| 計 | " + P1(all.BurnUnitTurns[0], all.UnitTurns[0]) + " | " + P1(all.TurnsAnyBurn[0], all.Turns) + " | "
                          + P1(all.FoeLitBattles.Sum(), all.Battles) + " | " + P1(all.AllyLitBattles.Sum(), all.Battles) + " |");
        Console.WriteLine();
        Console.WriteLine("味方の駒×ターンのうち燃えていた %: " + P1(all.BurnUnitTurns[1], all.UnitTurns[1]));
        Console.WriteLine();
        Console.WriteLine("### 行の分け方（P2 の分母）");
        Console.WriteLine();
        int noFoe = 0, noAny = 0;
        var foeRows = new List<(string, double, double)>();
        for (int i = 0; i < rows.Length; i++)
        {
            long fl = aggs[i].FoeLitBattles.Sum(), al = aggs[i].AllyLitBattles.Sum();
            if (fl == 0) noFoe++;
            if (fl == 0 && al == 0) noAny++;
            if (fl > 0) foeRows.Add((rows[i].Name, 100.0 * fl / aggs[i].Battles, 100.0 * aggs[i].BurnUnitTurns[0] / Math.Max(1, aggs[i].UnitTurns[0])));
        }
        Console.WriteLine("- 敵に1度も火が付かない行: **" + noFoe + " / " + rows.Length + "**（F1・F2 で 0 セルになるはずの行）");
        Console.WriteLine("- 敵にも味方にも1度も火が付かない行: **" + noAny + " / " + rows.Length + "**（F3・F4 でも 0 セルになるはずの行）");
        Console.WriteLine();
        Console.WriteLine("敵に火が付く行（" + foeRows.Count + " 行）:");
        Console.WriteLine();
        Console.WriteLine("| 行 | 敵に火が付いた戦 % | 敵の駒×ターンのうち燃えていた % |");
        Console.WriteLine("|---|--:|--:|");
        foreach (var (n, a, b) in foeRows.OrderByDescending(x => x.Item3)) Console.WriteLine("| " + n + " | " + F1(a) + " | " + F1(b) + " |");
        Console.WriteLine();
        Console.WriteLine("### 書き手ごとの付与（相手陣営に付けた回数 ／ 同じ陣営に付けた回数・`compare` 61 行の1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 書き手 | 相手陣営へ /戦 | 同じ陣営へ /戦 |");
        Console.WriteLine("|---|--:|--:|");
        foreach (string k in all.IgniteFoe.Keys.Union(all.IgniteAlly.Keys).OrderByDescending(k => all.IgniteFoe.GetValueOrDefault(k) + all.IgniteAlly.GetValueOrDefault(k)))
            Console.WriteLine("| " + NameOf(k) + " | " + F2(all.Per(all.IgniteFoe.GetValueOrDefault(k))) + " | " + F2(all.Per(all.IgniteAlly.GetValueOrDefault(k))) + " |");
        Console.WriteLine();
        Console.WriteLine("### ターンごとの「燃えている敵の割合」（`compare` 61 行・全波）");
        Console.WriteLine();
        Console.WriteLine("| ターン | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        Console.WriteLine("| 敵 % | " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => P1(all.BurnByTurn[0][t], all.AliveByTurn[0][t]))) + " |");
        Console.WriteLine("| 味方 % | " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => P1(all.BurnByTurn[1][t], all.AliveByTurn[1][t]))) + " |");
        Console.WriteLine();

        // ---- Q0-6 ----
        Console.WriteLine("## Q0-6 台（燃焼の在り方・倍率 115 と 150）");
        Console.WriteLine();
        Console.WriteLine("### 仮の席（F0 × 倍率 150 × 第2〜5波 × seed 1000..1049・同値は 全員生存 → 決着T → 列挙順）と天井の確認");
        Console.WriteLine();
        Console.WriteLine("本番の席は実装の後に **F2 × 倍率 150** で選び直す（指示書 §6.2）。ここは燃焼の在り方と天井を読むための F0 の仮の席。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 席 | 勝ち数/200 | 勝ち数が同値 | 3段で同値 | 勝率 115 | 150 | 200 | 250 | 全員生存 150 | 200 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var provisional = new List<(string, Formation)>();
        foreach (var (tag, mem) in new[] { ("H1", H1Members()), ("H2", H2Members()) })
            foreach (var sh in new[] { FormationShape.X, FormationShape.Diamond })
            {
                var (best, w, ties, ta) = PickSeat(mem, sh, EmberRule.Default, ShockDiag.Scale150);
                provisional.Add((tag + " " + ShapeName(sh), best));
                var byScale = new[] { 115, 150, 200, 250 }.Select(x => Measure(best, new EnemyScaleRule(x, x), EmberRule.Default)).ToArray();
                Console.WriteLine("| " + tag + " | " + ShapeName(sh) + " | " + SeatsNamed(best) + " | " + w + " | " + ties + " | " + ta + " | "
                                  + string.Join(" | ", byScale.Select(a => F1(a.Mean25))) + " | " + F1(byScale[1].AllSurvPct) + " | " + F1(byScale[2].AllSurvPct) + " |");
            }
        {
            var byScale = new[] { 115, 150, 200, 250 }.Select(x => Measure(PonX(), new EnemyScaleRule(x, x), EmberRule.Default)).ToArray();
            Console.WriteLine("| H1 | ポンの X字 | " + SeatsNamed(PonX()) + " | — | — | — | "
                              + string.Join(" | ", byScale.Select(a => F1(a.Mean25))) + " | " + F1(byScale[1].AllSurvPct) + " | " + F1(byScale[2].AllSurvPct) + " |");
        }
        Console.WriteLine();
        var benches = new List<(string, Formation)> { ("H1 ポンX", PonX()), ("ポンP2（スィド）", PonP2()) };
        benches.AddRange(provisional.Select(p => ("仮 " + p.Item1, p.Item2)));
        foreach (var (n, f) in H3Rows()) benches.Add(("H3 " + n, f));
        foreach (var (n, f) in H4Rows()) benches.Add(("H4 " + n, f));
        foreach (var (n, f) in H4PrimeRows()) benches.Add(("H4' " + n, f));
        Console.WriteLine("- H3（ホタ・ボルグ・ヒヨを含む `compare` の行）: " + H3Rows().Count + " 行");
        Console.WriteLine("- H4（ツギを含む `compare` の行）: " + H4Rows().Count + " 行。**ツギとベニが同じ行にいる行は "
                          + CompareBuilds().Count(r => Has(r.F, "tsugi") && Has(r.F, "beni")) + " 行**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 勝率（第2〜5波） | 敵の駒×ターン 燃えていた % | 味方 燃えていた % | 敵に火が付いた戦 % | 主な書き手（相手陣営へ /戦） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|");
        foreach (var (n, f) in benches)
            foreach (var sc in new[] { ShockDiag.Scale115, ShockDiag.Scale150 })
            {
                var a = Measure(f, sc, EmberRule.Default);
                var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
                string top = string.Join("・", a.IgniteFoe.Where(kv => ids.Contains(kv.Key)).OrderByDescending(kv => kv.Value).Take(3)
                    .Select(kv => Short(UnitCatalog.Everyone.First(d => d.Id == kv.Key)) + " " + F2(a.Per(kv.Value))).DefaultIfEmpty("—"));
                Console.WriteLine("| " + n + " | " + sc.HpPercent + " | " + F1(a.Mean25) + " | " + P1(a.BurnUnitTurns[0], a.UnitTurns[0]) + " | "
                                  + P1(a.BurnUnitTurns[1], a.UnitTurns[1]) + " | " + P1(a.FoeLitBattles.Sum(), a.Battles) + " | " + top + " |");
            }
        Console.WriteLine();
    }
}
