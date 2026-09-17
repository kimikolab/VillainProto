using BattleCore;
using static Common;

// =====================================================================================
// gauge モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "gauge")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 gauge
// =====================================================================================

static class GaugeDiag
{
// 物差しを直す（第88期）——**盤面を1ビットも動かさない。** `UnitCatalog` / `Traits.cs` / `BattleEngine.cs` は触っていない。
// 第84・85・87期の 2×2 を**同じノブ・同じ A と B・同じ定数**で回し直し、2つの器具の欠陥を直したうえで判定し直す:
//   (1) **情報帯** —— `y00` / `y01`（**A を素体にしたセル ＝ 版に依らない**）が床／天井に張り付いた台を分母から外す
//   (2) **陰性対照** —— 同じ版を台の抽選だけ変えた2系列で回し、ノイズ床（|Δ| の 95 パーセンタイル）を測る
//   (3) **判定規約** —— 主判定を大きさ（Δ相乗 +3.0pt）から**特異性**へ。大きさは拒否権にだけ残す
// **既存の診断（blaze2 / mender / suture2 / thorn / pairs2 / breadth）は1文字も書き換えていない。器具は写しを持つ。**
//
//     dotnet run --project BattleSim -c Release 0 gauge phase0                     # §1（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 gauge run <p> <v> [skip] [take]  # 2×2 を TSV へ（p = 84/85/87・v = 0/1・分割実行）
//     dotnet run --project BattleSim -c Release 0 gauge null <87v0.tsv>            # §3（陰性対照。**§5 より先に回す**）
//     dotnet run --project BattleSim -c Release 0 gauge redo <p> <v0> <v1> <null>  # §5（1機構ぶん）
//     dotnet run --project BattleSim -c Release 0 gauge ideal                      # §2-3（理想台にも情報帯を当てる。副判定 (C)）
//     dotnet run --project BattleSim -c Release 0 gauge tables <6つの TSV>         # 表A〜F
//     dotnet run --project BattleSim -c Release 0 gauge veto <p> <v>               # §4-5 拒否権（compare 61 行）
//     dotnet run --project BattleSim -c Release 0 gauge check                      # 自己検査 (e)
public static void Run(string[] args, int stageIndex)
{
    string ggArg = args.Length > 2 ? args[2] : "";
    var ggSw = System.Diagnostics.Stopwatch.StartNew();
    var ggInv = System.Globalization.CultureInfo.InvariantCulture;

    IReadOnlyList<EnemyCatalog.Stage> ggStages = EnemyCatalog.Stages;
    int ggW = ggStages.Count;
    // **第141期: ロスターは `Everyone`（`All ∪ Retired`）。** 第85期の機構は A ＝ ハリ、第84期の意図した相手に
    // エグ・ハリが入っていて、どちらも `All` に居ない。ロスターが B の集合と引き表を兼ねるのでロスターごと広げる。
    var ggRoster = UnitCatalog.Everyone.ToArray();
    int ggRN = ggRoster.Length;                       // 54（第88期は 51）

    // ---- 第81期 `pairs2` の定数の写し（**1つも変えていない**。第88期が変えるのは分母の取り方だけ）----
    const int GgTableSeed = 8_100_000;
    const int GgK = 64;                               // 1組・1系列あたりの台数
    const int GgS = 2;                                // 独立系列の本数
    const int GgBand = 0, GgM = 8;                    // 戦闘 seed 0..7
    const int GgStrong = 7, GgWeakPct = 60, GgDrawCap = 20000;
    const int GgTop = 20;
    const int GgMinInfo = 20;                         // §2-2: 情報帯の台がこれ未満の組は「測れていない」
    const double GgOldLine = 3.0;                     // 第84〜87期の線（**この期に外す**。記録のためだけ）
    const int GgIdealSeeds = 200;
    const double GgEps = 1e-9;

    var ggIdx = new Dictionary<string, int>();
    for (int u = 0; u < ggRN; u++) ggIdx[ggRoster[u].Id] = u;
    string[] ggName = ggRoster.Select(d => d.Name).ToArray();

    // ---- 機構の定義（§4-3 の表。**測る前に固定してある**）----------------------------------------
    (int P, string AId, string[] Intended, string Knob, string Label)[] ggMech =
    {
        (84, "kado", new[] { "kiri", "nomi", "egu", "nata", "hari" },
             "ThornRule.Foe", "第84期 棘に傷を載せる"),
        (85, "hari", new[] { "kado", "golm", "borg", "rica", "zoto", "nara" },
             "SutureRule.Both ＋ SpillWoundRule(true, All)", "第85期 糸を味方にも通す"),
        (87, "mio",  new[] { "kiri", "nomi" },
             "IgniteRule(true)（ctx.WoundIgnite）", "第87期 傷口に毒を流す"),
    };
    int GgMechIx(int p) => Array.FindIndex(ggMech, m => m.P == p);
    int GgA(int p) => ggIdx[ggMech[GgMechIx(p)].AId];
    int[] GgOthers(int p) { int a = GgA(p); return Enumerable.Range(0, ggRN).Where(u => u != a).ToArray(); }

    // 版 v: 0 = 対照（**現行の既定と完全に同じ**）／ 1 = 本命／ 2・3 = 第85期の内訳（§8 の分岐用）
    BattleResult GgRun(Formation f, Formation e, int seed, bool verbose, int p, int v)
    {
        // **3機構ぶんのノブを毎回すべて明示して渡す。** 第88期 §8 で `SutureRule` / `SpillWoundRule` の
        // 既定が動いたので、省略すると第84・87期の測定が「当時の対照」でなくなり、自己検査 (b) が落ちる。
        var pin = (su: new SutureRule(SutureSide.Foe), sp: new SpillWoundRule(false), th: new ThornRule(ThornWound.None));
        if (p == 84)
            return BattleEngine.Run(f, e, seed, verbose: verbose,
                                    thorn: new ThornRule(v == 0 ? ThornWound.None : ThornWound.Foe),
                                    suture: pin.su, spillWound: pin.sp, woundIgnite: new IgniteRule(false));
        if (p == 85)
        {
            // W0 / W2 は第85期 `suture2` の `SuRulesOf` の写し。2・3 は **W2 の内訳**（§8 の分岐用）
            (SutureRule su, SpillWoundRule sp, ThornRule th) = v switch
            {
                0 => (new SutureRule(SutureSide.Foe),  new SpillWoundRule(false), new ThornRule(ThornWound.None)),
                1 => (new SutureRule(SutureSide.Both), new SpillWoundRule(true),  new ThornRule(ThornWound.Foe)),
                2 => (new SutureRule(SutureSide.Both), new SpillWoundRule(false), new ThornRule(ThornWound.None)),
                _ => (new SutureRule(SutureSide.Foe),  new SpillWoundRule(true),  new ThornRule(ThornWound.Foe)),
            };
            return BattleEngine.Run(f, e, seed, verbose: verbose, thorn: th, suture: su, spillWound: sp);
        }
        return BattleEngine.Run(f, e, seed, verbose: verbose, woundIgnite: new IgniteRule(v != 0),
                                thorn: pin.th, suture: pin.su, spillWound: pin.sp);
    }
    string GgVName(int p, int v) => p switch
    {
        84 => v == 0 ? "V0（現行）" : "V1（敵に傷 1）",
        85 => v switch { 0 => "W0（現行）", 1 => "W2（両側・巻き込み則）", 2 => "W2s（縫いだけ）", _ => "W2p（巻き込み則だけ）" },
        _  => v == 0 ? "Y0（現行）" : "Y1（傷口に着火）",
    };

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜87期と同一。`Stages` は書き換えない）----------------------
    var ggWeakCache = new Dictionary<string, UnitDef>();
    UnitDef GgWeakOf(UnitDef d)
    {
        if (ggWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * GgWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        ggWeakCache[d.Id] = w;
        return w;
    }
    var ggWeak = ggStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = GgWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef GgPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var ggPlainMap = ggRoster.ToDictionary(d => d.Id, GgPlain);

    UnitDef[] GgFill(UnitDef[] pool, int strong0, int seed)
    {
        int rn = pool.Length;
        var rng = new Random(seed);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        int remain = rn, strong = strong0;
        var picked = new UnitDef[3];
        for (int r = 0; r < 3; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = pool[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= GgStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int GgSeed(int pairIx, int draw)
    {
        ulong x = (ulong)GgTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] GgSeats(UnitDef[] u)
    {
        var all5 = new[] { 0, 1, 2, 3, 4 };
        var front = all5.OrderByDescending(k => u[k].MaxHp)
                        .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        var rest = all5.Where(k => k != front[0] && k != front[1]).ToArray();
        var back = rest.OrderByDescending(k => u[k].Attack)
                       .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        int center = rest.Single(k => k != back[0] && k != back[1]);
        var r = new int[5];
        r[front[0]] = 0; r[front[1]] = 1; r[center] = 2; r[back[0]] = 3; r[back[1]] = 4;
        return r;
    }
    // 版 cell: bit0 = A（添字0）を素体に / bit1 = B（添字1）を素体に
    // **TSV の添字**: 0 = y11 ／ 1 = y01（**A 素体**・B 本物）／ 2 = y10（A 本物・**B 素体**）／ 3 = y00
    Formation GgForm(UnitDef[] u, int[] seats, int cell)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (cell & 1) != 0) || (k == 1 && (cell & 2) != 0);
            f[seats[k]] = plain ? ggPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double GgRate(Formation f, int p, int v)
    {
        double sum = 0;
        for (int wi = 1; wi < ggW; wi++)
        {
            int wins = 0;
            for (int seed = GgBand; seed < GgBand + GgM; seed++)
                if (GgRun(f, ggWeak[wi].Enemy, seed, false, p, v).PlayerWon) wins++;
            sum += wins * 100.0 / GgM;
        }
        return sum / (ggW - 1);
    }
    string GgP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double GgSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double GgSe(IReadOnlyList<double> xs) => xs.Count < 2 ? double.NaN : GgSd(xs) / Math.Sqrt(xs.Count);
    double GgPct(IReadOnlyList<double> xs, double q)
    {
        if (xs.Count == 0) return double.NaN;
        var s = xs.OrderBy(v => v).ToArray();
        if (s.Length == 1) return s[0];
        double pos = q * (s.Length - 1);
        int lo = (int)Math.Floor(pos), hi = Math.Min(lo + 1, s.Length - 1);
        return s[lo] + (pos - lo) * (s[hi] - s[lo]);
    }
    double GgMed(IReadOnlyList<double> xs) => GgPct(xs, 0.5);

    var ggPairIxOf = new int[ggRN, ggRN];
    {
        int pi = 0;
        for (int a = 0; a < ggRN; a++) for (int b = a + 1; b < ggRN; b++) { ggPairIxOf[a, b] = ggPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> GgFills(int a, int b)
    {
        var pool = ggRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (ggRoster[a].Attack >= GgStrong ? 1 : 0) + (ggRoster[b].Attack >= GgStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < GgS * GgK && draw < GgDrawCap; draw++)
        {
            var f = GgFill(pool, strong0, GgSeed(ggPairIxOf[a, b], draw));
            var t = f.Select(d => ggIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }
    double[][] GgMeasure(int p, int b, int v)
    {
        int a = GgA(p);
        var fills = GgFills(a, b);
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = new[] { ggRoster[a], ggRoster[b], fills[t][0], fills[t][1], fills[t][2] };
            int[] seats = GgSeats(team);
            ys[t] = new double[4];
            for (int cell = 0; cell < 4; cell++) ys[t][cell] = GgRate(GgForm(team, seats, cell), p, v);
        }
        return ys;
    }

    // ---- TSV --------------------------------------------------------------------------------------
    (int P, int V, int A, Dictionary<int, double[][]> D) GgRead(string path)
    {
        var d = new Dictionary<int, double[][]>();
        int pp = -1, vv = -1, aa = -1;
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int p = int.Parse(c[0]), v = int.Parse(c[1]), a = int.Parse(c[2]), b = int.Parse(c[3]), nT = int.Parse(c[4]);
            if (pp < 0) { pp = p; vv = v; aa = a; }
            else if (p != pp || v != vv || a != aa)
                throw new InvalidOperationException($"{path}: 期／版／A が混ざっている");
            var ys = new double[nT][];
            int at = 5;
            for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], ggInv); }
            d[b] = ys;
        }
        return (pp, vv, aa, d);
    }
    // 添字: 0 = y11 / 1 = y01（A 素体）/ 2 = y10（B 素体）/ 3 = y00
    double GgSyn(double[] y) => y[0] - y[2] - y[1] + y[3];
    // 情報帯（§4-1）: y00 と y01 の**両方が 0.0%**、または**両方が 100.0%** の台を外す
    bool GgFloor(double[] y) => y[3] < GgEps && y[1] < GgEps;
    bool GgCeil(double[] y) => y[3] > 100 - GgEps && y[1] > 100 - GgEps;
    bool GgInfo(double[] y) => !GgFloor(y) && !GgCeil(y);

    (double M, double Se, double[] Ser, int N) GgAgg(double[] d, bool[]? use)
    {
        var all = new List<double>(); var s0 = new List<double>(); var s1 = new List<double>();
        for (int t = 0; t < d.Length; t++)
        {
            if (use is not null && !use[t]) continue;
            all.Add(d[t]); (t % GgS == 0 ? s0 : s1).Add(d[t]);
        }
        return (all.Count > 0 ? all.Average() : double.NaN, GgSe(all),
                new[] { s0.Count > 0 ? s0.Average() : double.NaN, s1.Count > 0 ? s1.Average() : double.NaN }, all.Count);
    }

    // 1機構ぶんの畳み込み。B → (Δ, 情報帯マスク, 台の内訳)、および自己検査 (a) のずれ件数
    (Dictionary<int, double[]> D, Dictionary<int, bool[]> Info, Dictionary<int, (int All, int Floor, int Ceil, int Info)> Cnt, int Mism)
        GgFold(Dictionary<int, double[][]> v0, Dictionary<int, double[][]> v1, int[] others)
    {
        var D = new Dictionary<int, double[]>();
        var Info = new Dictionary<int, bool[]>();
        var Cnt = new Dictionary<int, (int, int, int, int)>();
        int mism = 0;
        foreach (int b in others)
        {
            if (!v0.ContainsKey(b) || !v1.ContainsKey(b)) continue;
            int nT = Math.Min(v0[b].Length, v1[b].Length);
            var d = new double[nT]; var inf = new bool[nT];
            int fl = 0, ce = 0, ok = 0;
            for (int t = 0; t < nT; t++)
            {
                d[t] = GgSyn(v1[b][t]) - GgSyn(v0[b][t]);
                // 自己検査 (a): A 素体の2セル（y01 / y00）が版で完全一致すること
                if (Math.Abs(v0[b][t][1] - v1[b][t][1]) > 1e-9) mism++;
                if (Math.Abs(v0[b][t][3] - v1[b][t][3]) > 1e-9) mism++;
                inf[t] = GgInfo(v0[b][t]);
                if (GgFloor(v0[b][t])) fl++; else if (GgCeil(v0[b][t])) ce++; else ok++;
            }
            D[b] = d; Info[b] = inf; Cnt[b] = (nT, fl, ce, ok);
        }
        return (D, Info, Cnt, mism);
    }

    // ---- 陰性対照（§4-2）。**版は両側とも V0。台の抽選（系列）だけを振る** ------------------------
    (Dictionary<int, (double D, double Se, int N0, int N1)> Raw,
     Dictionary<int, (double D, double Se, int N0, int N1)> Fil) GgNull(Dictionary<int, double[][]> v0, int[] others)
    {
        var raw = new Dictionary<int, (double, double, int, int)>();
        var fil = new Dictionary<int, (double, double, int, int)>();
        double SeOf(List<double> x, List<double> y)
        {
            double e0 = GgSe(x), e1 = GgSe(y);
            return double.IsNaN(e0) || double.IsNaN(e1) ? double.NaN : Math.Sqrt(e0 * e0 + e1 * e1);
        }
        foreach (int b in others)
        {
            if (!v0.ContainsKey(b)) continue;
            var a0 = new List<double>(); var a1 = new List<double>();
            var f0 = new List<double>(); var f1 = new List<double>();
            for (int t = 0; t < v0[b].Length; t++)
            {
                double s = GgSyn(v0[b][t]);
                (t % GgS == 0 ? a0 : a1).Add(s);
                if (GgInfo(v0[b][t])) (t % GgS == 0 ? f0 : f1).Add(s);
            }
            raw[b] = (a0.Count > 0 && a1.Count > 0 ? a0.Average() - a1.Average() : double.NaN, SeOf(a0, a1), a0.Count, a1.Count);
            fil[b] = (f0.Count > 0 && f1.Count > 0 ? f0.Average() - f1.Average() : double.NaN, SeOf(f0, f1), f0.Count, f1.Count);
        }
        return (raw, fil);
    }

    // =====================================================================================
    // phase0（§1。**戦闘0回**）
    // =====================================================================================
    if (ggArg == "phase0")
    {
        Console.WriteLine("# 第88期 Phase 0 —— 物差しを直す前の地図");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 gauge phase0`。**戦闘は1回も回していない。**");
        Console.WriteLine();
        Console.WriteLine("## §1-1 —— ノブが全部生きていること（既定は全部現行）");
        Console.WriteLine();
        Console.WriteLine("| ノブ | 型 | 既定 | 既定は現行か | この期で使う版 |");
        Console.WriteLine("|---|---|---|:-:|---|");
        Console.WriteLine($"| `ThornRule`（第84期） | `ThornRule(ThornWound)` | `{ThornRule.Default.Wound}` | {(ThornRule.Default.Wound == ThornWound.None ? "○" : "×")} | V0 `None` / V1 `Foe` |");
        Console.WriteLine($"| `SutureRule`（第85期） | `SutureRule(SutureSide)` | `{SutureRule.Default.Side}` | {(SutureRule.Default.Side == SutureSide.Foe ? "○" : "×")} | W0 `Foe` / W2 `Both` |");
        Console.WriteLine($"| `SpillWoundRule`（第85期） | `SpillWoundRule(bool, SpillScope)` | `{SpillWoundRule.Default.Enabled}` / `{SpillWoundRule.Default.Scope}` | {(!SpillWoundRule.Default.Enabled ? "○" : "×")} | W0 無効 / W2 `true, All` |");
        Console.WriteLine($"| `MendRule`（第86期） | `MendRule(MendSide)` | `{MendRule.Default.Side}` | {(MendRule.Default.Side == MendSide.Plain ? "○" : "×")} | **使わない**（2×2 が無い） |");
        Console.WriteLine($"| `IgniteRule`（第87期・`ctx.WoundIgnite`） | `IgniteRule(bool)` | `{IgniteRule.Default.Enabled}` | {(!IgniteRule.Default.Enabled ? "○" : "×")} | Y0 `false` / Y1 `true` |");
        Console.WriteLine();
        Console.WriteLine("**5つとも既定が現行（＝対照の側）。** 第88期は既定を1つも動かさない"
                          + "——ノブは `BattleEngine.Run` の引数で渡す（static のノブは置かない）。");
        Console.WriteLine();

        Console.WriteLine("## §1-2 —— 各期の 2×2 の A と B");
        Console.WriteLine();
        Console.WriteLine("| 期 | ノブ | A（固定） | B | 意図した相手 | 意図しない相手 |");
        Console.WriteLine("|---|---|---|--:|---|--:|");
        foreach (var m in ggMech)
        {
            int a = ggIdx[m.AId];
            int nOth = GgOthers(m.P).Length;
            Console.WriteLine($"| {m.Label} | `{m.Knob}` | {ggName[a]} | 残り {nOth} 体 | "
                              + string.Join("・", m.Intended.Select(i => ggName[ggIdx[i]])) + $"（{m.Intended.Length}枚） | {nOth - m.Intended.Length} 体 |");
        }
        Console.WriteLine();
        Console.WriteLine("**第86期（`MendRule`）は再検定の対象外。** 紙のスループット（傷由来の増分 2.0% < 線 5%・"
                          + "ノノの発火 2.10 ≤ ハリの 2.15）で停止し、**2×2 を1戦も回していない**ので、"
                          + "情報帯を当てる分母そのものが存在しない。");
        Console.WriteLine();

        Console.WriteLine("## §1-3 —— 2×2 の定数（第81期 `pairs2` の写し。**1つも変えない**）");
        Console.WriteLine();
        Console.WriteLine("| 定数 | 値 | 意味 |");
        Console.WriteLine("|---|--:|---|");
        Console.WriteLine($"| `TableSeed` | {GgTableSeed:N0} | 台の抽選の起点（組と引き番号だけで決まる） |");
        Console.WriteLine($"| `K` | {GgK} | 1組・1系列あたりの台数 |");
        Console.WriteLine($"| `S` | {GgS} | 独立系列の本数（**編成の共有 0** が構成から保証される） |");
        Console.WriteLine($"| `M` | {GgM} | 戦闘 seed {GgBand}..{GgBand + GgM - 1} |");
        Console.WriteLine($"| `WeakPct` | {GgWeakPct} | 弱い波（敵 MaxHp を 0.{GgWeakPct} 倍）。`Stages` は書き換えない |");
        Console.WriteLine($"| `Strong` | {GgStrong} | 規則 P の「強い駒」の線（攻撃力） |");
        Console.WriteLine($"| `DrawCap` | {GgDrawCap:N0} | 相異なる台を集める引きの上限 |");
        Console.WriteLine();
        Console.WriteLine($"1組・1版あたり **{GgS * GgK} 台 × 4 セル × {ggW - 1} 波 × {GgM} seed = {GgS * GgK * 4 * (ggW - 1) * GgM:N0} 戦**。"
                          + $"50 体で **{50L * GgS * GgK * 4 * (ggW - 1) * GgM:N0} 戦/版**。"
                          + "**この期に回すのは 6 版**（3機構 × 2版。陰性対照は第87期 Y0 の使い回しで**追加費用ゼロ**）。");
        Console.WriteLine();

        Console.WriteLine("## §1-4 —— A を素体にしたセルが版に依らないことの確認");
        Console.WriteLine();
        Console.WriteLine("**紙の根拠**: 3つのノブはどれも **A の特性の中**でしか読まれない——");
        Console.WriteLine();
        Console.WriteLine("| 期 | ノブが読まれる場所 | A を素体にすると |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 第84期 | `ThornsTrait.OnDamaged`（カドの棘） | カドが `Thorns` を持たない → 規則が走る場所が無い |");
        Console.WriteLine("| 第85期 | `SutureTrait`（ハリの縫い）＋ **`ApplyDamage` の巻き込み則** | ハリが `Suture` を持たない。**巻き込み則は A に依らない**ので、そこは実測で確かめる |");
        Console.WriteLine("| 第87期 | `AmplifierTrait`（ミオの澱み） | ミオが `Amplifier` を持たない → 規則が走る場所が無い |");
        Console.WriteLine();
        Console.WriteLine("**第85期だけは紙で閉じない**（`SpillWoundRule` は engine 側の `ApplyDamage` にあり、A が素体でも味方の刃は通る）。");
        Console.WriteLine("**だから実測の確認は標本を絞らず全数でやる**——`gauge redo` / `gauge tables` が");
        Console.WriteLine($"**3機構それぞれ 50 体 × {GgS * GgK} 台 × 2 セル = {50 * GgS * GgK * 2:N0} セル**を突き合わせて自己検査 (a) に出す。");
        Console.WriteLine("**ここで数台の抜き取りをやると、全数の検査より弱い証拠を先に置くことになるので回さない**"
                          + "（この節を「戦闘0回」に保っているのはそのため。指示書 §1-4 との差はこの1点で、**確認そのものは強くなっている**）。");
        Console.WriteLine();
        Console.WriteLine("**もし第85期で (a) が落ちたら**、情報帯の正当性はその機構については成立しない"
                          + "——そのときは「巻き込み則は A 素体のセルも動かす」と書いて、第85期だけフィルタ前で判定する。");
        Console.WriteLine();
        Console.WriteLine($"所要 {ggSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // run <p> <v> [skip] [take]
    // =====================================================================================
    if (ggArg == "run")
    {
        int p = args.Length > 3 ? int.Parse(args[3]) : 87;
        int v = args.Length > 4 ? int.Parse(args[4]) : 0;
        if (GgMechIx(p) < 0) { Console.Error.WriteLine("gauge run <84|85|87> <v> [skip] [take]"); return; }
        int a = GgA(p);
        var others = GgOthers(p);
        int skip = args.Length > 5 ? int.Parse(args[5]) : 0;
        int take = args.Length > 6 ? int.Parse(args[6]) : others.Length;
        skip = Math.Clamp(skip, 0, others.Length);
        take = Math.Clamp(take, 0, others.Length - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"gauge run p{p} v{v} {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            int b = others[skip + j];
            var ys = GgMeasure(p, b, v);
            var sb = new System.Text.StringBuilder();
            sb.Append(p).Append('\t').Append(v).Append('\t').Append(a).Append('\t').Append(b).Append('\t').Append(ys.Length);
            foreach (double[] yy in ys) for (int k = 0; k < 4; k++) sb.Append('\t').Append(yy[k].ToString("R", ggInv));
            rows[j] = sb.ToString();
            if (Interlocked.Increment(ref doneR) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {ggSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    // =====================================================================================
    // null <87 の V0 の TSV>（§3。**§5 より先に回す**）
    // =====================================================================================
    if (ggArg == "null")
    {
        if (args.Length < 4 || !File.Exists(args[3])) { Console.WriteLine("gauge null <87 の v0 の TSV>"); return; }
        var (np, nv, na, nd) = GgRead(args[3]);
        var others = GgOthers(np);
        var (raw, fil) = GgNull(nd, others);

        Console.WriteLine("# 第88期 §3 —— 陰性対照（ノイズ床）");
        Console.WriteLine();
        Console.WriteLine($"A ＝ **{ggName[na]}**・版は**両側とも {GgVName(np, nv)}**。振ったのは**台の抽選だけ**"
                          + $"——系列1（引き番号が偶数の {GgK} 台）の相乗の平均 − 系列2（奇数の {GgK} 台）の相乗の平均。"
                          + "**真の効果はゼロ**なので、ここに出る幅がノイズ床である。"
                          + "**第87期 Y0 の測定をそのまま使うので追加費用ゼロ。**");
        Console.WriteLine();
        var rawV = others.Where(b => raw.ContainsKey(b) && !double.IsNaN(raw[b].D)).Select(b => raw[b].D).ToArray();
        var filV = others.Where(b => fil.ContainsKey(b) && !double.IsNaN(fil[b].D) && fil[b].N0 + fil[b].N1 >= GgMinInfo).Select(b => fil[b].D).ToArray();
        var rawA = rawV.Select(Math.Abs).ToArray();
        var filA = filV.Select(Math.Abs).ToArray();
        int n3raw = others.Count(b => raw.ContainsKey(b) && !double.IsNaN(raw[b].Se) && Math.Abs(raw[b].D) > 2 * raw[b].Se);
        int n3fil = others.Count(b => fil.ContainsKey(b) && !double.IsNaN(fil[b].Se) && fil[b].N0 + fil[b].N1 >= GgMinInfo && Math.Abs(fil[b].D) > 2 * fil[b].Se);

        Console.WriteLine("## 表B —— ノイズ床（N1〜N4）");
        Console.WriteLine();
        Console.WriteLine("| | 体数 | 平均 | 中央値 | 標準偏差 | \\|Δ\\| の中央値 | **\\|Δ\\| の 95%tile ＝ ノイズ床** | \\|Δ\\| の最大 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| **N1 フィルタ前** | {rawV.Length} | {GgP2(rawV.Average())} | {GgP2(GgMed(rawV))} | {GgSd(rawV):F2} | {GgMed(rawA):F2} | **{GgPct(rawA, 0.95):F2}** | {rawA.Max():F2} |");
        Console.WriteLine($"| **N1 フィルタ後** | {filV.Length} | {GgP2(filV.Average())} | {GgP2(GgMed(filV))} | {GgSd(filV):F2} | {GgMed(filA):F2} | **{GgPct(filA, 0.95):F2}** | {filA.Max():F2} |");
        Console.WriteLine();
        Console.WriteLine($"**N2（ノイズ床）**: フィルタ前 **{GgPct(rawA, 0.95):F2}pt** / フィルタ後 **{GgPct(filA, 0.95):F2}pt**。"
                          + "**主判定にはフィルタ後の値を使う**（§4-2）。");
        Console.WriteLine();
        Console.WriteLine($"**N3（現行の有意判定 \\|Δ\\| > 2SE が誤って拾う体数）**: フィルタ前 **{n3raw} / {rawV.Length}** ／ "
                          + $"フィルタ後 **{n3fil} / {filV.Length}**。");
        Console.WriteLine();
        Console.WriteLine($"**N4（フィルタでノイズ床は下がるか）**: {GgPct(rawA, 0.95):F2} → {GgPct(filA, 0.95):F2} "
                          + $"= **{(GgPct(filA, 0.95) < GgPct(rawA, 0.95) ? "下がった" : "下がらなかった")}**"
                          + $"（比 {GgPct(filA, 0.95) / GgPct(rawA, 0.95):F2} 倍）。標準偏差は {GgSd(rawV):F2} → {GgSd(filV):F2}。");
        Console.WriteLine();
        Console.WriteLine($"**自己検査 (c)**: 陰性対照の Δ の平均は フィルタ前 {GgP2(rawV.Average())} / フィルタ後 {GgP2(filV.Average())}"
                          + $" → **{(Math.Abs(rawV.Average()) < GgPct(rawA, 0.95) && Math.Abs(filV.Average()) < GgPct(filA, 0.95) ? "○（どちらもノイズ床の内側＝0 の近く）" : "×")}**。");
        Console.WriteLine();
        Console.WriteLine($"**§4-6 の退避線の判定**: N3（フィルタ後）= {n3fil} 体 → "
                          + $"**{(n3fil >= 5 ? $"5 体以上。特異性の判定は成立しない。大きさの線を 2 × N2 = {2 * GgPct(filA, 0.95):F2}pt として残す" : "5 体未満。特異性の判定は成立する")}**。");
        Console.WriteLine();
        Console.WriteLine($"## 陰性対照の全 {rawV.Length} 体（|Δ| 降順・上位 {GgTop}）");
        Console.WriteLine();
        Console.WriteLine("| 順位 | B | Δ（前） | SE（前） | 2SE 超 | **Δ（後）** | SE（後） | 2SE 超 | 情報帯（系列1/系列2） |");
        Console.WriteLine("|--:|---|--:|--:|:-:|--:|--:|:-:|--:|");
        var ord = others.Where(b => raw.ContainsKey(b)).OrderByDescending(b => Math.Abs(raw[b].D)).ToArray();
        for (int i = 0; i < Math.Min(GgTop, ord.Length); i++)
        {
            int b = ord[i];
            var r = raw[b]; var f = fil[b];
            Console.WriteLine($"| {i + 1} | {ggName[b]} | {GgP2(r.D)} | {r.Se:F2} | {(Math.Abs(r.D) > 2 * r.Se ? "○" : "")} | **{GgP2(f.D)}** | {f.Se:F2} | {(!double.IsNaN(f.Se) && Math.Abs(f.D) > 2 * f.Se ? "○" : "")} | {f.N0} / {f.N1} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {ggSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ---- 1機構ぶんの再検定の中身（redo と tables で共有）------------------------------------------
    void GgReport(int p, string f0, string f1, double floorFil, double floorRaw, int n3, int n3raw, bool head)
    {
        int mi = GgMechIx(p);
        var m = ggMech[mi];
        var (p0, v0i, a0, d0) = GgRead(f0);
        var (p1, v1i, a1, d1) = GgRead(f1);
        if (p0 != p || p1 != p || a0 != a1) { Console.WriteLine($"**{p} 期: TSV の期／A が合わない**"); return; }
        var others = GgOthers(p);
        var (D, Info, Cnt, mism) = GgFold(d0, d1, others);
        var intended = new HashSet<int>(m.Intended.Select(i => ggIdx[i]));

        var agAll = new Dictionary<int, (double M, double Se, double[] Ser, int N)>();
        var agFil = new Dictionary<int, (double M, double Se, double[] Ser, int N)>();
        foreach (int b in D.Keys) { agAll[b] = GgAgg(D[b], null); agFil[b] = GgAgg(D[b], Info[b]); }
        var measurable = D.Keys.Where(b => Cnt[b].Info >= GgMinInfo).ToHashSet();

        if (head)
        {
            Console.WriteLine($"# 第88期 §5 —— {m.Label} の再検定");
            Console.WriteLine();
        }
        Console.WriteLine($"ノブ `{m.Knob}`・A ＝ **{ggName[a0]}**。版は {GgVName(p, v0i)} 対 {GgVName(p, v1i)}。"
                          + "**台・席・戦闘 seed・台の抽選は両版で同一。** "
                          + $"意図した相手は {string.Join("・", m.Intended.Select(i => ggName[ggIdx[i]]))}（{intended.Count}枚）。"
                          + $"ノイズ床は **{floorFil:F2}pt**（フィルタ後）・N3 = {n3}。");
        Console.WriteLine();

        // ---- 台の内訳 -------------------------------------------------------------------------
        int cAll = D.Keys.Sum(b => Cnt[b].All), cFl = D.Keys.Sum(b => Cnt[b].Floor),
            cCe = D.Keys.Sum(b => Cnt[b].Ceil), cIn = D.Keys.Sum(b => Cnt[b].Info);
        Console.WriteLine("### 台の内訳（§2-2）");
        Console.WriteLine();
        Console.WriteLine("| | 台数 | 割合 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 全台（{D.Count} 体 × {GgS * GgK} 台） | {cAll:N0} | 100.0% |");
        Console.WriteLine($"| 床で死んだ台（`y00` = `y01` = 0.0%） | {cFl:N0} | {cFl * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| 天井で死んだ台（`y00` = `y01` = 100.0%） | {cCe:N0} | {cCe * 100.0 / cAll:F1}% |");
        Console.WriteLine($"| **情報帯** | **{cIn:N0}** | **{cIn * 100.0 / cAll:F1}%** |");
        Console.WriteLine();
        Console.WriteLine($"情報帯の台数は 1組あたり 中央値 **{GgMed(D.Keys.Select(b => (double)Cnt[b].Info).ToArray()):F1}** / {GgS * GgK} 台"
                          + $"（最小 {D.Keys.Min(b => Cnt[b].Info)} / 最大 {D.Keys.Max(b => Cnt[b].Info)}）。"
                          + $"**{GgMinInfo} 台を下回って「測れていない」組は {D.Count - measurable.Count} 体**"
                          + (D.Count - measurable.Count > 0 ? "（" + string.Join("・", D.Keys.Where(b => !measurable.Contains(b)).Select(b => $"{ggName[b]} {Cnt[b].Info}")) + "）" : "") + "。");
        Console.WriteLine();
        Console.WriteLine($"**自己検査 (a)**: A 素体の2セル（`y01` / `y00`）が版で完全一致 → "
                          + $"{cAll * 2:N0} セル・ずれ **{mism}** 件 → **{(mism == 0 ? "○" : "×")}**"
                          + $"{(mism == 0 ? "（情報帯が「版に依らない量だけで台を選ぶ」ことの証拠）" : "（この機構では情報帯の正当性が成立しない。フィルタ前で判定すること）")}。");
        Console.WriteLine();

        // ---- Δ相乗 ----------------------------------------------------------------------------
        var ordF = measurable.OrderByDescending(b => agFil[b].M).ToArray();
        var ordA = D.Keys.OrderByDescending(b => agAll[b].M).ToArray();
        Console.WriteLine("### Δ相乗（フィルタ前 / 後）");
        Console.WriteLine();
        Console.WriteLine("`相乗(A,B) = y11 − y10 − y01 + y00`／`Δ相乗 = 相乗(本命) − 相乗(対照)`。"
                          + $"★ ＝ 意図した相手。`床超` ＝ フィルタ後の \\|Δ\\| がノイズ床 {floorFil:F2}pt を超えたもの。");
        Console.WriteLine();
        Console.WriteLine("| 順位（後） | B | **Δ（後）** | SE | 情報帯 | 系列1 | 系列2 | 床超 | Δ（前） | SE（前） | 順位（前） |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|--:|--:|--:|");
        void Row(int b)
        {
            int rf = Array.IndexOf(ordF, b) + 1, ra = Array.IndexOf(ordA, b) + 1;
            var af = agFil[b]; var aa = agAll[b];
            bool over = measurable.Contains(b) && Math.Abs(af.M) > floorFil;
            Console.WriteLine($"| {(rf > 0 ? rf.ToString() : "—")} | {(intended.Contains(b) ? "★" : "")}{ggName[b]} | **{GgP2(af.M)}** | {af.Se:F2} | {Cnt[b].Info} | {GgP2(af.Ser[0])} | {GgP2(af.Ser[1])} | {(over ? "○" : "")} | {GgP2(aa.M)} | {aa.Se:F2} | {ra} |");
        }
        var shown = new HashSet<int>();
        for (int i = 0; i < Math.Min(10, ordF.Length); i++) { Row(ordF[i]); shown.Add(ordF[i]); }
        if (ordF.Length > 20) Console.WriteLine("| … | | | | | | | | | | |");
        for (int i = Math.Max(10, ordF.Length - 10); i < ordF.Length; i++) { Row(ordF[i]); shown.Add(ordF[i]); }
        Console.WriteLine();
        Console.WriteLine("意図した相手（上の表に出ていないぶん）と「測れていない」組:");
        Console.WriteLine();
        Console.WriteLine("| 順位（後） | B | **Δ（後）** | SE | 情報帯 | 系列1 | 系列2 | 床超 | Δ（前） | SE（前） | 順位（前） |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|--:|--:|--:|");
        foreach (int b in intended.Concat(D.Keys.Where(b => !measurable.Contains(b))).Distinct())
            if (D.ContainsKey(b) && !shown.Contains(b)) Row(b);
        Console.WriteLine();

        // ---- 自己検査 (b) ----------------------------------------------------------------------
        Console.WriteLine("**自己検査 (b)（フィルタ前の Δ が当時の報告と一致するか）**:");
        Console.WriteLine();
        Console.WriteLine("| 相手 | 当時の報告 | この期のフィルタ前 | 差 |");
        Console.WriteLine("|---|--:|--:|--:|");
        var known = p switch
        {
            84 => new (string, double)[] { ("kiri", -0.10), ("nomi", 0.00), ("egu", 0.17), ("nata", -0.02), ("hari", 0.15) },
            85 => new (string, double)[] { ("golm", 5.25), ("borg", 2.54), ("nara", 0.63), ("zoto", 0.17), ("kado", 0.12), ("rica", -0.90) },
            _  => new (string, double)[] { ("kiri", 0.98), ("nomi", 0.32) },
        };
        double worst = 0;
        foreach (var (id, want) in known)
        {
            int b = ggIdx[id];
            if (!agAll.ContainsKey(b)) continue;
            double got = agAll[b].M;
            worst = Math.Max(worst, Math.Abs(got - want));
            Console.WriteLine($"| {ggName[b]} | {GgP2(want)} | {GgP2(got)} | {Math.Abs(got - want):F3} |");
        }
        double mean0 = known.Where(k => agAll.ContainsKey(ggIdx[k.Item1])).Average(k => agAll[ggIdx[k.Item1]].M);
        double meanW = known.Average(k => k.Item2);
        Console.WriteLine($"| **平均** | **{GgP2(meanW)}** | **{GgP2(mean0)}** | {Math.Abs(mean0 - meanW):F3} |");
        Console.WriteLine();
        Console.WriteLine($"最大差 **{worst:F3}pt** → **{(worst <= 0.02 ? "○（丸めの範囲。器具の写しは同じ）" : "×（器具の写しが違う。ここで止まる）")}**。");
        Console.WriteLine();

        // ---- Q1〜Q3 ----------------------------------------------------------------------------
        var intM = intended.Where(b => measurable.Contains(b)).ToArray();
        var passers = intM.Where(b => agFil[b].Ser[0] > 0 && agFil[b].Ser[1] > 0 && agFil[b].M > floorFil).ToArray();
        var unint = measurable.Where(b => !intended.Contains(b)).ToArray();
        var fpPos = unint.Where(b => agFil[b].M > floorFil).ToArray();
        var fpNeg = unint.Where(b => agFil[b].M < -floorFil).ToArray();
        var fpAll = unint.Where(b => Math.Abs(agFil[b].M) > floorFil).ToArray();
        bool q11 = passers.Length >= 1, q12 = fpAll.Length <= n3;

        Console.WriteLine("### 判定（§4-3・§4-4）");
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 実測 | 線 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1-1** | 意図した相手のうち **2系列とも正 かつ \\|Δ\\| > ノイズ床** | **{passers.Length} / {intM.Length} 枚**"
                          + (passers.Length > 0 ? "（" + string.Join("・", passers.Select(b => $"{ggName[b]} {GgP2(agFil[b].M)}")) + "）" : "")
                          + $" | ≥ 1 | **{(q11 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1-2** | 意図しない相手で \\|Δ\\| > ノイズ床 の体数 | **{fpAll.Length} / {unint.Length} 体**（正 {fpPos.Length} / 負 {fpNeg.Length}） | ≤ {n3} | **{(q12 ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1** | **特異性（主判定）** | | | **{(q11 && q12 ? "○" : "×")}** |");
        int bestRank = intM.Length == 0 ? 0 : intM.Min(b => Array.IndexOf(ordF, b) + 1);
        Console.WriteLine($"| Q2 | 意図した組の最良順位（{ordF.Length} 体中） | **{(bestRank > 0 ? bestRank + " 位" : "—")}**"
                          + (bestRank > 0 ? $"（{ggName[intM.OrderBy(b => Array.IndexOf(ordF, b)).First()]}）" : "")
                          + $" | ≤ 10 | **{(bestRank > 0 && bestRank <= 10 ? "○" : "×")}** |");
        Console.WriteLine($"| Q3 | 負の側で床を超えた体数 | **{fpNeg.Length} 体**"
                          + (fpNeg.Length > 0 ? "（" + string.Join("・", fpNeg.OrderBy(b => agFil[b].M).Take(6).Select(b => $"{ggName[b]} {GgP2(agFil[b].M)}")) + (fpNeg.Length > 6 ? " …" : "") + "）" : "") + " | — | 記録 |");
        Console.WriteLine($"| 参考 | 旧主判定（意図した相手の Δ の平均 ≥ +{GgOldLine:F1}） | 前 {GgP2(intended.Where(agAll.ContainsKey).Average(b => agAll[b].M))} / 後 {GgP2(intM.Length > 0 ? intM.Average(b => agFil[b].M) : double.NaN)} | ≥ +{GgOldLine:F1} | **×** |");
        Console.WriteLine();
        Console.WriteLine($"**{m.Label} の §4 の規約での結論: {(q11 && q12 ? "通る" : "通らない")}**"
                          + $"（Q1-1 {(q11 ? "○" : "×")} / Q1-2 {(q12 ? "○" : "×")}）。");
        Console.WriteLine();

        // ---- フィルタ前での判定（Phase 0 §1-4 で先に書いてある規則。**(a) が落ちた機構ではこちらが正**）----
        var intMr = intended.Where(agAll.ContainsKey).ToArray();
        var pass0 = intMr.Where(b => agAll[b].Ser[0] > 0 && agAll[b].Ser[1] > 0 && agAll[b].M > floorRaw).ToArray();
        var unint0 = D.Keys.Where(b => !intended.Contains(b)).ToArray();
        var fp0 = unint0.Where(b => Math.Abs(agAll[b].M) > floorRaw).ToArray();
        bool q11r = pass0.Length >= 1, q12r = fp0.Length <= n3raw;
        Console.WriteLine($"**フィルタ前での同じ判定**（ノイズ床 {floorRaw:F2}pt・N3 = {n3raw}）: "
                          + $"Q1-1 **{pass0.Length} / {intMr.Length} 枚**"
                          + (pass0.Length > 0 ? "（" + string.Join("・", pass0.Select(b => $"{ggName[b]} {GgP2(agAll[b].M)}")) + "）" : "")
                          + $" {(q11r ? "○" : "×")} ／ Q1-2 **{fp0.Length} / {unint0.Length} 体** {(q12r ? "○" : "×")} → **{(q11r && q12r ? "通る" : "通らない")}**。"
                          + (mism > 0
                             ? "**この機構は自己検査 (a) が落ちているので、Phase 0 §1-4 の先決めどおり<u>こちらが正</u>。**"
                             : "（(a) が通っているので参考。主判定はフィルタ後）。"));
        Console.WriteLine();

        // ---- 判定 (ii)（**事後の別読み**。§4-7 の「両方を並べる」に従って併記する）------------------
        // §3 のノイズ床は **相乗（水準）の台の抽選による揺れ**で、Δ相乗（増分）とは尺度が違う
        // ——§0-1 が指摘した「水準の分布から引いた線を増分に当てる」がそのまま再発している。
        // 増分の側の物差しは**同じ実験の中の意図しない相手の分布**（そこは真の効果が 0 のはず）で取る。
        double floorIn = GgPct(unint.Select(b => Math.Abs(agFil[b].M)).ToArray(), 0.95);
        var pass2 = intM.Where(b => agFil[b].Ser[0] > 0 && agFil[b].Ser[1] > 0
                                    && agFil[b].M > floorIn && agFil[b].M > 2 * agFil[b].Se).ToArray();
        Console.WriteLine("**判定 (ii)（事後の別読み・§4-7 に従って併記）**: §3 のノイズ床は**相乗（水準）**の揺れで、"
                          + "**Δ相乗（増分）**とは尺度が違う。増分の物差しを**同じ実験の中の意図しない相手の分布**から取ると:");
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 実測 | 線 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| 床 | 意図しない {unint.Length} 体の \\|Δ\\| の 95%tile | **{floorIn:F2}pt** | | |");
        Console.WriteLine($"| **Q1-1'** | 意図した相手が 2系列とも正・床超・\\|Δ\\| > 2SE | **{pass2.Length} / {intM.Length} 枚**"
                          + (pass2.Length > 0 ? "（" + string.Join("・", pass2.Select(b => $"{ggName[b]} {GgP2(agFil[b].M)}")) + "）" : "")
                          + $" | ≥ 1 | **{(pass2.Length >= 1 ? "○" : "×")}** |");
        Console.WriteLine($"| 参考 | 意図しない相手の \\|Δ\\| の中央値 / 最大 | {GgMed(unint.Select(b => Math.Abs(agFil[b].M)).ToArray()):F2} / {(unint.Length > 0 ? unint.Max(b => Math.Abs(agFil[b].M)) : double.NaN):F2} | | |");
        Console.WriteLine();
    }

    // =====================================================================================
    // redo <p> <v0> <v1> <null の TSV>
    // =====================================================================================
    if (ggArg == "redo")
    {
        if (args.Length < 7 || !File.Exists(args[4]) || !File.Exists(args[5]) || !File.Exists(args[6]))
        { Console.WriteLine("gauge redo <84|85|87> <v0 の TSV> <v1 の TSV> <陰性対照（87 の v0）の TSV>"); return; }
        int p = int.Parse(args[3]);
        var (np, nv, na, nd) = GgRead(args[6]);
        var nOth = GgOthers(np);
        var (nraw, nfil) = GgNull(nd, nOth);
        var filA = nOth.Where(b => nfil.ContainsKey(b) && !double.IsNaN(nfil[b].D) && nfil[b].N0 + nfil[b].N1 >= GgMinInfo).Select(b => Math.Abs(nfil[b].D)).ToArray();
        var rawA = nOth.Where(b => nraw.ContainsKey(b) && !double.IsNaN(nraw[b].D)).Select(b => Math.Abs(nraw[b].D)).ToArray();
        int n3 = nOth.Count(b => nfil.ContainsKey(b) && !double.IsNaN(nfil[b].Se) && nfil[b].N0 + nfil[b].N1 >= GgMinInfo && Math.Abs(nfil[b].D) > 2 * nfil[b].Se);
        int n3raw = nOth.Count(b => nraw.ContainsKey(b) && !double.IsNaN(nraw[b].Se) && Math.Abs(nraw[b].D) > 2 * nraw[b].Se);
        GgReport(p, args[4], args[5], GgPct(filA, 0.95), GgPct(rawA, 0.95), n3, n3raw, true);
        Console.WriteLine($"所要 {ggSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // tables <t84v0> <t84v1> <t85v0> <t85v1> <t87v0> <t87v1>
    // =====================================================================================
    if (ggArg == "tables")
    {
        if (args.Length < 9 || Enumerable.Range(3, 6).Any(i => !File.Exists(args[i])))
        { Console.WriteLine("gauge tables <84v0> <84v1> <85v0> <85v1> <87v0> <87v1>"); return; }

        var (np, nv, na, nd) = GgRead(args[7]);   // 陰性対照 ＝ 第87期 Y0
        var nOth = GgOthers(np);
        var (nraw, nfil) = GgNull(nd, nOth);
        var rawV = nOth.Where(b => nraw.ContainsKey(b) && !double.IsNaN(nraw[b].D)).Select(b => nraw[b].D).ToArray();
        var filV = nOth.Where(b => nfil.ContainsKey(b) && !double.IsNaN(nfil[b].D) && nfil[b].N0 + nfil[b].N1 >= GgMinInfo).Select(b => nfil[b].D).ToArray();
        double floorRaw = GgPct(rawV.Select(Math.Abs).ToArray(), 0.95);
        double floorFil = GgPct(filV.Select(Math.Abs).ToArray(), 0.95);
        int n3 = nOth.Count(b => nfil.ContainsKey(b) && !double.IsNaN(nfil[b].Se) && nfil[b].N0 + nfil[b].N1 >= GgMinInfo && Math.Abs(nfil[b].D) > 2 * nfil[b].Se);

        Console.WriteLine("# 第88期 —— 物差しを直す（表A〜F）");
        Console.WriteLine();
        Console.WriteLine($"器具は第81期の 2×2 の写し（K = {GgK} 台 × {GgS} 系列・弱い波 {GgWeakPct}%・第2〜5波・seed {GgBand}..{GgBand + GgM - 1}）。"
                          + "**定数は1つも変えていない。変えたのは分母の取り方（情報帯）と判定の書き方だけ。**");
        Console.WriteLine();

        // ---- 表A: 台の内訳 -------------------------------------------------------------------
        Console.WriteLine("## 表A —— 台の内訳（全台 / 床 / 天井 / 情報帯）【自己検査 (d)】");
        Console.WriteLine();
        Console.WriteLine("| 機構 | A | 体数 | 全台 | 床 | 天井 | **情報帯** | 情報帯の割合 | 1組の情報帯（中央値） | 測れていない組 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        var shareList = new List<double>();
        for (int mi = 0; mi < ggMech.Length; mi++)
        {
            int p = ggMech[mi].P;
            var (pa, va, aa, da) = GgRead(args[3 + mi * 2]);
            var (pb, vb, ab, db) = GgRead(args[4 + mi * 2]);
            var oth = GgOthers(p);
            var (D, Info, Cnt, mism) = GgFold(da, db, oth);
            int cAll = D.Keys.Sum(b => Cnt[b].All), cFl = D.Keys.Sum(b => Cnt[b].Floor),
                cCe = D.Keys.Sum(b => Cnt[b].Ceil), cIn = D.Keys.Sum(b => Cnt[b].Info);
            shareList.Add(cIn * 100.0 / cAll);
            Console.WriteLine($"| {ggMech[mi].Label} | {ggName[aa]} | {D.Count} | {cAll:N0} | {cFl:N0}（{cFl * 100.0 / cAll:F1}%） | {cCe:N0}（{cCe * 100.0 / cAll:F1}%） | **{cIn:N0}** | **{cIn * 100.0 / cAll:F1}%** | {GgMed(D.Keys.Select(b => (double)Cnt[b].Info).ToArray()):F1} | {D.Count - D.Keys.Count(b => Cnt[b].Info >= GgMinInfo)} |");
        }
        {
            int cAll = nOth.Where(nd.ContainsKey).Sum(b => nd[b].Length);
            int cFl = nOth.Where(nd.ContainsKey).Sum(b => nd[b].Count(GgFloor));
            int cCe = nOth.Where(nd.ContainsKey).Sum(b => nd[b].Count(GgCeil));
            int cIn = cAll - cFl - cCe;
            Console.WriteLine($"| **陰性対照**（第87期 Y0 単独） | {ggName[na]} | {nOth.Count(nd.ContainsKey)} | {cAll:N0} | {cFl:N0}（{cFl * 100.0 / cAll:F1}%） | {cCe:N0}（{cCe * 100.0 / cAll:F1}%） | **{cIn:N0}** | **{cIn * 100.0 / cAll:F1}%** | — | — |");
        }
        Console.WriteLine();
        Console.WriteLine($"**自己検査 (d)**: 3機構の情報帯の割合は {string.Join(" / ", shareList.Select(v => v.ToString("F1") + "%"))}"
                          + $"——幅 **{shareList.Max() - shareList.Min():F1}pt** → "
                          + $"**{(shareList.Max() - shareList.Min() < 10.0 ? "○（大きく違わない）" : "×（A ごとに情報帯の厚みが違う。A の体の強さが台に効いている）")}**"
                          + "（陰性対照は第87期と同じ標本なので、両者が一致するのは定義どおり）。");
        Console.WriteLine();

        // ---- 表B: ノイズ床 ---------------------------------------------------------------------
        Console.WriteLine("## 表B —— ノイズ床（§3。詳しくは `gauge null`）");
        Console.WriteLine();
        Console.WriteLine("| | 体数 | 平均 | 中央値 | 標準偏差 | **95%tile（ノイズ床）** | 2SE 超（N3） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        int n3raw = nOth.Count(b => nraw.ContainsKey(b) && !double.IsNaN(nraw[b].Se) && Math.Abs(nraw[b].D) > 2 * nraw[b].Se);
        Console.WriteLine($"| フィルタ前 | {rawV.Length} | {GgP2(rawV.Average())} | {GgP2(GgMed(rawV))} | {GgSd(rawV):F2} | **{floorRaw:F2}** | {n3raw} |");
        Console.WriteLine($"| **フィルタ後** | {filV.Length} | {GgP2(filV.Average())} | {GgP2(GgMed(filV))} | {GgSd(filV):F2} | **{floorFil:F2}** | **{n3}** |");
        Console.WriteLine();

        // ---- 表C〜E ---------------------------------------------------------------------------
        for (int mi = 0; mi < ggMech.Length; mi++)
        {
            Console.WriteLine($"## 表{(char)('C' + mi)} —— {ggMech[mi].Label} の再検定");
            Console.WriteLine();
            GgReport(ggMech[mi].P, args[3 + mi * 2], args[4 + mi * 2], floorFil, floorRaw, n3, n3raw, false);
        }

        // ---- 表F: 判定の異同 -------------------------------------------------------------------
        Console.WriteLine("## 表F —— 判定の異同（当時 / 新しい規約）");
        Console.WriteLine();
        Console.WriteLine("| 期 | 当時の主判定 | 当時の値 | 当時 | 新しい主判定（特異性） | Q1-1 | Q1-2 | **新しい結論** |");
        Console.WriteLine("|---|---|--:|:-:|---|--:|--:|:-:|");
        for (int mi = 0; mi < ggMech.Length; mi++)
        {
            int p = ggMech[mi].P;
            var (pa, va, aa, da) = GgRead(args[3 + mi * 2]);
            var (pb, vb, ab, db) = GgRead(args[4 + mi * 2]);
            var oth = GgOthers(p);
            var (D, Info, Cnt, mism) = GgFold(da, db, oth);
            var intended = new HashSet<int>(ggMech[mi].Intended.Select(i => ggIdx[i]));
            var agFil = D.Keys.ToDictionary(b => b, b => GgAgg(D[b], Info[b]));
            var measurable = D.Keys.Where(b => Cnt[b].Info >= GgMinInfo).ToHashSet();
            var intM = intended.Where(measurable.Contains).ToArray();
            var passers = intM.Where(b => agFil[b].Ser[0] > 0 && agFil[b].Ser[1] > 0 && agFil[b].M > floorFil).ToArray();
            var fpAll = measurable.Where(b => !intended.Contains(b) && Math.Abs(agFil[b].M) > floorFil).ToArray();
            bool ok = passers.Length >= 1 && fpAll.Length <= n3;
            (string q, string v) was = p switch
            {
                84 => ("傷の5枚の Δ相乗 の平均 ≥ +3.0", "+0.04"),
                85 => ("6枚の Δ相乗 の平均 ≥ +3.0", "+1.30"),
                _  => ("Δ相乗(ミオ, キリ) ≥ +3.0", "+0.98"),
            };
            Console.WriteLine($"| {ggMech[mi].Label} | {was.q} | {was.v}pt | × | 意図した相手が床超・意図しない相手が動かない | {passers.Length}/{intM.Length} | {fpAll.Length} ≤ {n3} | **{(ok ? "○" : "×")}** |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {ggSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // ideal（§2-3。理想台にも情報帯を当てる。副判定 (C)）
    // =====================================================================================
    if (ggArg == "ideal")
    {
        // 第87期 `blaze2 ideal` の4台の写し（`CompareBuilds()` は触らない）
        var rows = new (string Name, Formation F)[]
        {
            ("キリ×ミオ",       Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm,
                                            center: UnitCatalog.Dolga, back1: UnitCatalog.Vel, back3: UnitCatalog.Mio)),
            ("ノミ×ミオ",       Formation.Build(front1: UnitCatalog.Mio, front3: UnitCatalog.Golm,
                                            center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Vel)),
            ("キリ×ミオ×ベニ", Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm,
                                            center: UnitCatalog.Beni, back1: UnitCatalog.Vel, back3: UnitCatalog.Mio)),
            ("キリ×ミオ×ラウ", Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm,
                                            center: UnitCatalog.Rau, back1: UnitCatalog.Vel, back3: UnitCatalog.Mio)),
        };
        Formation PlainA(Formation f)
        {
            var g = new Formation();
            foreach ((int sl, UnitDef d) in f.Occupied()) g[sl] = d.Id == "mio" ? ggPlainMap["mio"] : d;
            return g;
        }
        double[] Rate(Formation f, int v)
        {
            var r = new double[ggW];
            for (int wv = 0; wv < ggW; wv++)
            {
                int wins = 0;
                for (int seed = 0; seed < GgIdealSeeds; seed++)
                    if (GgRun(f, ggStages[wv].Enemy, seed, false, 87, v).PlayerWon) wins++;
                r[wv] = wins * 100.0 / GgIdealSeeds;
            }
            return r;
        }
        double Mean25(double[] v) { double s = 0; for (int wv = 1; wv < ggW; wv++) s += v[wv]; return s / (ggW - 1); }

        var res = new (double[] F0, double[] F1, double[] P0, double[] P1)[rows.Length];
        Parallel.For(0, rows.Length, i =>
        {
            Formation f = rows[i].F, pl = PlainA(f);
            res[i] = (Rate(f, 0), Rate(f, 1), Rate(pl, 0), Rate(pl, 1));
        });

        Console.WriteLine("# 第88期 §2-3 —— 理想台にも情報帯を当てる（副判定 (C)）");
        Console.WriteLine();
        Console.WriteLine($"第87期 `blaze2 ideal` の4台の写し（`CompareBuilds()` は触らない）。5波 × seed 0..{GgIdealSeeds - 1}。"
                          + "**フィルタは 2×2 と同じ定義**——`素体`（ミオを同数値・特性なしに落とした版）の第2〜5波平均が"
                          + " **0.0% か 100.0%** の台は「測定になっていない」として分母から外す。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 素体 第2〜5波 | 情報帯 | 現行 Y0 | 現行 Y1 | 帰属 Y0 | 帰属 Y1 | **Δ帰属** | 波ごとの Y0 → Y1 |");
        Console.WriteLine("|---|--:|:-:|--:|--:|--:|--:|--:|---|");
        var keep = new List<double>(); var drop = new List<double>();
        for (int i = 0; i < rows.Length; i++)
        {
            var r = res[i];
            double pm = Mean25(r.P0);
            bool info = pm > GgEps && pm < 100 - GgEps;
            double a0 = Mean25(r.F0) - Mean25(r.P0), a1 = Mean25(r.F1) - Mean25(r.P1);
            (info ? keep : drop).Add(a1 - a0);
            Console.WriteLine($"| {rows[i].Name} | {pm:F1}% | {(info ? "○" : "×")} | {Mean25(r.F0):F1}% | {Mean25(r.F1):F1}% | {GgP2(a0)} | {GgP2(a1)} | **{GgP2(a1 - a0)}** | "
                              + string.Join(" / ", Enumerable.Range(0, ggW).Select(wv => $"{r.F0[wv]:F0}→{r.F1[wv]:F0}")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"**情報帯に残った理想台は {keep.Count} / {rows.Length}。** 外した {drop.Count} 台の Δ帰属は "
                          + (drop.Count > 0 ? string.Join(" / ", drop.Select(GgP2)) : "—")
                          + "（**測定になっていないので分母から除く**。第87期の中央値 0.8× はこれを含んだ値）。");
        Console.WriteLine();
        Console.WriteLine($"残った台の Δ帰属: {(keep.Count > 0 ? string.Join(" / ", keep.Select(GgP2)) + $"・中央値 {GgP2(GgMed(keep))}" : "—")}。");
        Console.WriteLine();
        Console.WriteLine($"所要 {ggSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // veto <p> <v>（§4-5。採用の可否を大きさで見る。**既定は動かさない**）
    // =====================================================================================
    if (ggArg == "veto")
    {
        int p = args.Length > 3 ? int.Parse(args[3]) : 87;
        int v = args.Length > 4 ? int.Parse(args[4]) : 1;
        var allRows = CompareBuilds();
        var primary = new HashSet<string>(Baseline.PrimaryRows);
        var c = new double[2][][];
        for (int k = 0; k < 2; k++) c[k] = new double[allRows.Length][];
        Parallel.For(0, allRows.Length * 2, k =>
        {
            int i = k / 2, side = k % 2;
            int ver = side == 0 ? 0 : v;
            var res = new double[ggW];
            for (int wv = 0; wv < ggW; wv++)
            {
                int wins = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (GgRun(allRows[i].F, ggStages[wv].Enemy, seed, false, p, ver).PlayerWon) wins++;
                res[wv] = wins * 100.0 / 200;
            }
            c[side][i] = res;
        });
        Console.WriteLine($"# 第88期 §4-5 —— 拒否権（{ggMech[GgMechIx(p)].Label} / {GgVName(p, v)}）");
        Console.WriteLine();
        var pr = allRows.Select((rw, i) => (rw.Name, i)).Where(t => primary.Contains(t.Name)).ToArray();
        double f0 = pr.Average(t => c[0][t.i][4]), f1 = pr.Average(t => c[1][t.i][4]);
        Console.WriteLine($"**拒否権1**（主判定 {pr.Length} 行の第五波平均 ≥ {Baseline.PrimaryFifthFloor:F1}%）: "
                          + $"{f0:F1}% → **{f1:F1}%** → **{(f1 >= Baseline.PrimaryFifthFloor ? "○（下回らない）" : "×")}**");
        int n95a = Enumerable.Range(0, allRows.Length).Count(i => c[0][i][4] > 95), n95b = Enumerable.Range(0, allRows.Length).Count(i => c[1][i][4] > 95);
        Console.WriteLine();
        Console.WriteLine($"**拒否権2**（第五波が 95% を超える行が新たに2行以上）: {n95a} 行 → **{n95b} 行**（{n95b - n95a:+0;-0;0}）→ "
                          + $"**{(n95b - n95a < 2 ? "○" : "×")}**");
        Console.WriteLine();
        var drops = new List<string>();
        foreach (var (name, i) in pr)
            for (int wv = 0; wv < ggW; wv++)
                if (c[1][i][wv] - c[0][i][wv] <= -10.0) drops.Add($"{name} 第{wv + 1}波 {c[0][i][wv]:F1} → {c[1][i][wv]:F1}");
        Console.WriteLine($"**拒否権3**（主判定行がいずれかの波で −10.0pt 以上落ちる）: **{drops.Count} 件**"
                          + (drops.Count > 0 ? "（" + string.Join(" / ", drops) + "）" : "") + $" → **{(drops.Count == 0 ? "○" : "×")}**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        int moved = 0;
        for (int i = 0; i < allRows.Length; i++)
        {
            if (Enumerable.Range(0, ggW).All(wv => Math.Abs(c[0][i][wv] - c[1][i][wv]) < 0.001)) continue;
            moved++;
            for (int k = 0; k < 2; k++)
                Console.WriteLine($"| {allRows[i].Name}{(primary.Contains(allRows[i].Name) ? "［主判定］" : "")} | {(k == 0 ? GgVName(p, 0) : GgVName(p, v))} | "
                                  + string.Join(" | ", c[k][i].Select(x => $"{x:F1}%")) + $" | {c[k][i].Average():F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine($"動いた行 **{moved} / {allRows.Length}**。");
        Console.WriteLine();
        Console.WriteLine($"所要 {ggSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check（自己検査 (e)）
    // =====================================================================================
    if (ggArg == "check")
    {
        var allRows = CompareBuilds();
        var bal = new Dictionary<string, double[]>();
        if (File.Exists("docs/balance.md"))
            foreach (string line in File.ReadAllLines("docs/balance.md"))
            {
                if (!line.StartsWith("| ")) continue;
                var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
                if (cells.Length != ggW + 1 || !cells[1].EndsWith("%")) continue;
                var v = new double[ggW];
                bool ok = true;
                for (int wv = 0; wv < ggW; wv++)
                    if (!double.TryParse(cells[wv + 1].TrimEnd('%'), out v[wv])) { ok = false; break; }
                if (ok) bal[cells[0]] = v;
            }
        var cur = new double[allRows.Length][];
        Parallel.For(0, allRows.Length, i =>
        {
            var res = new double[ggW];
            for (int wv = 0; wv < ggW; wv++)
            {
                int wins = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(allRows[i].F, ggStages[wv].Enemy, seed, verbose: false).PlayerWon) wins++;
                res[wv] = wins * 100.0 / 200;
            }
            cur[i] = res;
        });
        int cells2 = 0, mism = 0;
        var missing = new List<string>();
        for (int i = 0; i < allRows.Length; i++)
        {
            if (!bal.TryGetValue(allRows[i].Name, out double[]? b)) { missing.Add(allRows[i].Name); continue; }
            for (int wv = 0; wv < ggW; wv++) { cells2++; if (Math.Abs(b[wv] - cur[i][wv]) > 0.001) mism++; }
        }
        Console.WriteLine("# 第88期 —— 自己検査 (e)");
        Console.WriteLine();
        Console.WriteLine($"**`compare` {cells2} セルが `docs/balance.md` と一致** → ずれ **{mism}** 件 → **{(mism == 0 && missing.Count == 0 ? "○" : "×")}**"
                          + (missing.Count > 0 ? "（balance.md に無い行: " + string.Join(" / ", missing) + "）" : "") + "。");
        Console.WriteLine();
        Console.WriteLine("**この期に動かしたのは `SutureRule` / `SpillWoundRule` の既定2つだけ**（§8 の採用）。"
                          + "`docs/balance.md` は採用後に再生成してあるので、ここが 0 件なのは"
                          + "「再生成した表と現行の盤面が一致している」ことの確認になる。");
        Console.WriteLine();
        Console.WriteLine($"所要 {ggSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("gauge: 引数は phase0 / run <p> <v> [skip] [take] / null <tsv> / redo <p> <v0> <v1> <null> / tables <6つの TSV> / ideal / veto <p> <v> / check。");
    return;
}
}
