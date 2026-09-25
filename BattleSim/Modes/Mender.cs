using BattleCore;
using static Common;

// =====================================================================================
// mender モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "mender")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 mender
// =====================================================================================

static class MenderDiag
{
// 繕いに傷を読ませる（第86期）——継ぎ当てのノノ × 巻き込み則。
// 器具は第81期の 2×2（第85期 `suture2` の写し）で、**A ＝ ノノ固定**・B ＝ 残り 50 体。版は3つ:
//   X0（対照）  SpillWound 無効        / MendSide.Plain   ＝ 現行
//   X1（本命）  SpillWound 有効・All   / MendSide.Wound   ＝ 書き手6枚・読み手ノノ
//   X2          SpillWound 有効・Dense / MendSide.Wound   ＝ 吸い（ゴルム）と余波（ボルグ）だけ
//   X1P（検査） SpillWound 有効・All   / MendSide.Plain   ＝ 自己検査 (b)。読み手が居なければ盤面は動かない
// **既存の診断（suture2 / thorn / pairs2 / breadth / checkup）は1文字も書き換えていない。**
//
//     dotnet run --project BattleSim -c Release 0 mender phase0                  # §1（紙のスループット・窓口の全数・交わり）
//     dotnet run --project BattleSim -c Release 0 mender run <x> [skip] [take]   # 2×2（x = 0/1/2）。50 組 × 128 台・TSV を標準出力へ
//     dotnet run --project BattleSim -c Release 0 mender tables <x0> <x1> <x2>   # 表A〜F・Q1〜Q5・副判定 (A)
//     dotnet run --project BattleSim -c Release 0 mender check                   # `compare` 61 行の X0/X1/X2/X1P・拒否権1〜3・(e)
public static void Run(string[] args, int stageIndex)
{
    string mdArg = args.Length > 2 ? args[2] : "";
    var mdSw = System.Diagnostics.Stopwatch.StartNew();
    var mdInv = System.Globalization.CultureInfo.InvariantCulture;

    IReadOnlyList<EnemyCatalog.Stage> mdStages = EnemyCatalog.Stages;
    int mdW = mdStages.Count;
    var mdRoster = UnitCatalog.All.ToArray();
    int mdRN = mdRoster.Length;                       // 51

    // ---- 第81期 `pairs2` の定数の写し（**1つも変えていない**）----------------------------------
    const int MdTableSeed = 8_100_000;
    const int MdK = 64;                               // 1組・1系列あたりの台数
    const int MdS = 2;                                // 独立系列の本数
    const int MdBand = 0, MdM = 8;                    // 戦闘 seed 0..7
    const int MdStrong = 7, MdWeakPct = 60, MdDrawCap = 20000;
    const int MdTop = 20;
    const double MdQ1Line = 3.0;                      // 主判定の線（§3-1。第84・85期と同じ。測る前に固定）
    const double MdPaperFloor = 5.0;                  // 紙の停止条件 1: 傷由来の増分/戦 ÷ 総被ダメ/戦（§1-1）
    const double MdHariFires = 2.15;                  // 紙の停止条件 2: 第85期のハリの振り 2.15 回/戦
    const int MdProbeTables = 16;                     // 表A・B・D・E を verbose で測る台数（系列から 8 ずつ）

    var mdIdx = new Dictionary<string, int>();
    for (int u = 0; u < mdRN; u++) mdIdx[mdRoster[u].Id] = u;
    string[] mdName = mdRoster.Select(d => d.Name).ToArray();
    int mdNono = mdIdx["lili"];
    string[] mdWriterIds = { "kado", "borg", "rica", "golm", "zoto", "tsugi" };   // 味方に傷を書く6枚
    int[] mdWriters = mdWriterIds.Select(i => mdIdx[i]).ToArray();
    string[] mdDenseIds = { "golm", "borg" };                                    // X2 で残る2枚
    int[] mdOthers = Enumerable.Range(0, mdRN).Where(u => u != mdNono).ToArray();   // 50 体
    string mdGaldName = mdName[mdIdx["gald"]];

    (SpillWoundRule Spill, MendRule Mend) MdRulesOf(int x) => x switch
    {
        0 => (new SpillWoundRule(false), new MendRule(MendSide.Plain)),
        1 => (new SpillWoundRule(true, SpillScope.All), new MendRule(MendSide.Wound)),
        2 => (new SpillWoundRule(true, SpillScope.Dense), new MendRule(MendSide.Wound)),
        _ => (new SpillWoundRule(true, SpillScope.All), new MendRule(MendSide.Plain)),   // X1P（自己検査 (b)）
    };
    string MdVName(int x) => x switch { 0 => "X0（現行）", 1 => "X1（巻き込み則・全 ＋ 傷読み）", 2 => "X2（吸い・余波だけ ＋ 傷読み）", _ => "X1P（供給だけ・読み手なし）" };
    BattleResult MdRun(Formation f, Formation e, int seed, bool verbose, int x)
    {
        var r = MdRulesOf(x);
        return BattleEngine.Run(f, e, seed, verbose: verbose, spillWound: r.Spill, mend: r.Mend);
    }

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜85期と同一。`Stages` は書き換えない）----------------------
    var mdWeakCache = new Dictionary<string, UnitDef>();
    UnitDef MdWeakOf(UnitDef d)
    {
        if (mdWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * MdWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        mdWeakCache[d.Id] = w;
        return w;
    }
    var mdWeak = mdStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = MdWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef MdPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var mdPlainMap = mdRoster.ToDictionary(d => d.Id, MdPlain);

    UnitDef[] MdFill(UnitDef[] pool, int strong0, int seed, int count)
    {
        int rn = pool.Length;
        var rng = new Random(seed);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        int remain = rn, strong = strong0;
        var picked = new UnitDef[count];
        for (int r = 0; r < count; r++)
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
            if (sel.Attack >= MdStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int MdSeed(int pairIx, int draw)
    {
        ulong x = (ulong)MdTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] MdSeats(UnitDef[] u)
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
    // 版 v: bit0 = A（添字0＝**ノノ**）を素体に / bit1 = B（添字1）を素体に
    // **TSV の添字**: 0 = y11 ／ 1 = y01（**ノノ素体**・B 本物）／ 2 = y10（ノノ本物・**B 素体**）／ 3 = y00
    // 第85期の罠（A・B の割り当てで添字の意味が変わる）——この期は A ＝ ノノ。
    Formation MdForm(UnitDef[] u, int[] seats, int v)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (v & 1) != 0) || (k == 1 && (v & 2) != 0);
            f[seats[k]] = plain ? mdPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double MdRate(Formation f, int x)
    {
        double sum = 0;
        for (int wi = 1; wi < mdW; wi++)
        {
            int wins = 0;
            for (int seed = MdBand; seed < MdBand + MdM; seed++)
                if (MdRun(f, mdWeak[wi].Enemy, seed, false, x).PlayerWon) wins++;
            sum += wins * 100.0 / MdM;
        }
        return sum / (mdW - 1);
    }
    string MdP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double MdSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double MdSe(IReadOnlyList<double> xs) => xs.Count < 2 ? double.NaN : MdSd(xs) / Math.Sqrt(xs.Count);

    var mdPairIxOf = new int[mdRN, mdRN];
    {
        int pi = 0;
        for (int a = 0; a < mdRN; a++) for (int b = a + 1; b < mdRN; b++) { mdPairIxOf[a, b] = mdPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> MdFills(int b)
    {
        int a = mdNono;
        var pool = mdRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (mdRoster[a].Attack >= MdStrong ? 1 : 0) + (mdRoster[b].Attack >= MdStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < MdS * MdK && draw < MdDrawCap; draw++)
        {
            var f = MdFill(pool, strong0, MdSeed(mdPairIxOf[a, b], draw), 3);
            var t = f.Select(d => mdIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }
    UnitDef[] MdTeam(int b, UnitDef[] fill) => new[] { mdRoster[mdNono], mdRoster[b], fill[0], fill[1], fill[2] };
    double[][] MdMeasure(int b, int x)
    {
        var fills = MdFills(b);
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = MdTeam(b, fills[t]);
            int[] seats = MdSeats(team);
            ys[t] = new double[4];
            for (int v = 0; v < 4; v++) ys[t][v] = MdRate(MdForm(team, seats, v), x);
        }
        return ys;
    }

    // ---- verbose の1戦から供給・在庫・繕い・寿命を読む（**盤面は動かさない**）----
    void MdProbe(Formation f, Formation enemy, int seed, int x, MdStat acc)
    {
        var r = MdRun(f, enemy, seed, true, x);
        var team = new Dictionary<int, int>();
        {
            int i = 0;
            foreach ((int sl, UnitDef d) in f.Occupied()) { team[i] = BattleContext.PlayerTeam; i++; }
            foreach ((int sl, UnitDef d) in enemy.Occupied()) { team[i] = BattleContext.EnemyTeam; i++; }
        }
        // Events: 在庫（ターン頭の StatusSnapshot「傷」を陣営別に合計）・味方の刃の着弾回数（自己検査 (f)）
        {
            double stA = 0, stF = 0, mxA = 0; int turns = 0, allyTurns = 0; int curTurn = -1; double curA = 0, curF = 0;
            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind == BattleEventKind.Summon && e.TargetId is int sid && e.Team is int tm) team[sid] = tm;
                else if (e.Kind == BattleEventKind.TurnStart)
                {
                    if (curTurn >= 0) { stA += curA; stF += curF; turns++; if (curA > 0) allyTurns++; mxA = Math.Max(mxA, curA); }
                    curTurn = e.Turn; curA = 0; curF = 0;
                }
                else if (e.Kind == BattleEventKind.StatusSnapshot && e.Text == "傷" && e.TargetId is int wid && team.TryGetValue(wid, out int wt))
                {
                    if (wt == BattleContext.PlayerTeam) curA += e.Amount; else curF += e.Amount;
                }
                else if (e.Kind == BattleEventKind.Damage && e.FriendlyFire && !e.Relayed && e.ActorId is int aid && e.TargetId is int tid && aid != tid
                         && team.TryGetValue(aid, out int at) && at == BattleContext.PlayerTeam && e.HpAfter > 0)
                    acc.SpillHits++;
            }
            if (curTurn >= 0) { stA += curA; stF += curF; turns++; if (curA > 0) allyTurns++; mxA = Math.Max(mxA, curA); }
            acc.StockAlly += turns > 0 ? stA / turns : 0; acc.StockFoe += turns > 0 ? stF / turns : 0;
            acc.StockAllyMax += mxA; acc.AllyWoundTurns += allyTurns;
        }
        // Log: 巻き込みの傷（書き手別）・繕いの患者（自己検査 (e)）
        foreach (LogLine ll in r.Log)
        {
            string sx = ll.Text;
            if (sx.Contains("巻き込みの傷: "))
            {
                acc.SpillWounds++;
                for (int k = 0; k < mdWriterIds.Length; k++)
                    if (sx.Contains($"巻き込みの傷: {mdName[mdWriters[k]]} の刃が ")) { acc.SpillByWriter[k]++; break; }
            }
            else if (sx.Contains(" が自分を裂いて ") && sx.Contains(" を繕った"))
            {
                int p = sx.IndexOf(" が自分を裂いて ", StringComparison.Ordinal) + 8;
                int q = sx.IndexOf(" を繕った", p, StringComparison.Ordinal);
                if (q > p && sx.Substring(p, q - p) == mdGaldName) acc.GaldPatient++;
            }
        }
        // Tally: 繕い・寿命・傷の在庫の出入り
        foreach ((int sl, UnitDef d) in f.Occupied())
        {
            if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? pt)) continue;
            acc.TeamHealed += pt.Healed; acc.TeamTaken += pt.DamageTaken;
            acc.CarryWoundAlly += pt.CarryCount is null ? 0 : pt.CarryCount[UnitTally.CarryWound];
            acc.DeathWoundAlly += pt.WoundsAtDeath; acc.EndWoundAlly += pt.WoundsAtEnd;
            if (d.Id == "lili")
            {
                acc.MendFires += pt.MendFires; acc.MendSeen += pt.MendWoundSeen; acc.MendDepth += pt.MendWoundDepth;
                acc.MendDry += pt.MendDry; acc.MendHealed += pt.MendHealed; acc.MendPaid += pt.MendPaid;
                acc.MendFoePatient += pt.MendFoePatient;
                acc.NonoLife += pt.LastActiveTurn;
                if (pt.Deaths > 0) acc.NonoDeaths++;
            }
        }
        foreach ((int sl, UnitDef d) in enemy.Occupied())
            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? et))
            {
                acc.CarryWoundFoe += et.CarryCount is null ? 0 : et.CarryCount[UnitTally.CarryWound];
                acc.DeathWoundFoe += et.WoundsAtDeath; acc.EndWoundFoe += et.WoundsAtEnd;
                acc.FoeMendFires += et.MendFires; acc.FoeMendSeen += et.MendWoundSeen;
            }
        if (r.PlayerWon) acc.Wins++;
        acc.Turns += r.Turns;
        acc.N++;
    }
    MdStat[] MdProbePair(int b, int x)
    {
        var fills = MdFills(b);
        var loc = new MdStat[mdW];
        for (int wv = 0; wv < mdW; wv++) loc[wv] = new MdStat();
        int taken = 0;
        for (int t = 0; t < fills.Count && taken < MdProbeTables; t++)
        {
            var teamU = MdTeam(b, fills[t]);
            taken++;
            int[] seats = MdSeats(teamU);
            Formation f = MdForm(teamU, seats, 0);
            for (int wv = 1; wv < mdW; wv++)
                for (int seed = MdBand; seed < MdBand + MdM; seed++) MdProbe(f, mdWeak[wv].Enemy, seed, x, loc[wv]);
        }
        return loc;
    }
    MdStat MdSum(MdStat[] xs, int from = 1) { var s = new MdStat(); for (int wv = from; wv < xs.Length; wv++) s.AddFrom(xs[wv]); return s; }

    var mdAllRows = CompareBuilds();
    bool MdHas(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    var mdNonoRows = mdAllRows.Where(rw => MdHas(rw.F, "lili")).ToArray();
    var mdX1Rows = mdNonoRows.Where(rw => mdWriterIds.Any(id => MdHas(rw.F, id))).ToArray();
    var mdX2Rows = mdNonoRows.Where(rw => mdDenseIds.Any(id => MdHas(rw.F, id))).ToArray();
    var mdPrimary = new HashSet<string>(Baseline.PrimaryRows);

    double[] MdCompare(Formation f, int x)
    {
        var v = new double[mdW];
        for (int wv = 0; wv < mdW; wv++)
        {
            int wins = 0;
            for (int seed = 0; seed < 200; seed++)
                if (MdRun(f, mdStages[wv].Enemy, seed, false, x).PlayerWon) wins++;
            v[wv] = wins * 100.0 / 200;
        }
        return v;
    }
    var mdBalance = new Dictionary<string, double[]>();
    if (File.Exists("docs/balance.md"))
        foreach (string line in File.ReadAllLines("docs/balance.md"))
        {
            if (!line.StartsWith("| ")) continue;
            var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
            if (cells.Length != mdW + 1 || !cells[1].EndsWith("%")) continue;
            var v = new double[mdW];
            bool ok = true;
            for (int wv = 0; wv < mdW; wv++)
                if (!double.TryParse(cells[wv + 1].TrimEnd('%'), out v[wv])) { ok = false; break; }
            if (ok) mdBalance[cells[0]] = v;
        }

    // ---- 紙のスループット（§1-1）--------------------------------------------------------------
    // 傷由来の増分/戦 = 発火回数/戦 × 患者に傷があった率 × 平均の深さ × PerWound
    //                 = （観測した傷の総深さ ÷ 戦数）× PerWound          ← 恒等式。X1 を1戦も回さずに出る
    // **観測は X1P（供給だけ・読み手なし）で取る**——盤面が X0 と1セルも違わないことが自己検査 (b)。
    (double fires, double rate, double depth, double paper, double taken, double turns, double heal)
        MdPaper(MdStat s)
    {
        double fires = s.MendFires / s.N;
        double rate = s.MendFires > 0 ? s.MendSeen / s.MendFires : 0;
        double depth = s.MendSeen > 0 ? s.MendDepth / s.MendSeen : 0;
        return (fires, rate, depth, fires * rate * depth * MenderTrait.PerWound, s.TeamTaken / s.N, s.Turns / s.N, s.TeamHealed / s.N);
    }

    // =====================================================================================
    // phase0
    // =====================================================================================
    if (mdArg == "phase0")
    {
        Console.WriteLine("# 第86期 Phase 0 —— 繕いに傷を読ませる前の地図");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 mender phase0`。**盤面を動かす版（X1・X2）は1戦も回さない**——"
                          + "分子（ノノの発火回数・患者に傷があった率・平均の深さ）は **X1P（巻き込み則だけを入れて読み手を入れない版）** で取る。"
                          + "X1P は読み手が居ないので盤面が X0 と1セルも違わない（自己検査 (b)）。");
        Console.WriteLine();

        // ---- 1-1 紙のスループット --------------------------------------------------------------
        Console.WriteLine("## 1-1. 紙のスループット（**ここで落ちたら実装しない**）");
        Console.WriteLine();
        Console.WriteLine($"組 (ノノ, 書き手) の 2×2 の台のうち各系列の先頭 {MdProbeTables / MdS} 台 = {MdProbeTables} 台の y11（両方が本物）を、"
                          + $"弱い波の第2〜5波 × seed {MdBand}..{MdBand + MdM - 1} で回した（書き手6枚 × {MdProbeTables} 台 × 4 波 × {MdM} seed）。");
        Console.WriteLine();
        Console.WriteLine($"    傷由来の増分/戦 = 発火回数/戦 × 患者に傷があった率 × 平均の深さ × PerWound({MenderTrait.PerWound})");
        Console.WriteLine("    分母            = その台の総被ダメージ/戦（**基礎の 14 は分子に入れない**）");
        Console.WriteLine();
        var mp = new MdStat[mdWriters.Length][];
        var mp0 = new MdStat[mdWriters.Length][];
        Parallel.For(0, mdWriters.Length * 2, k =>
        {
            int i = k / 2;
            if (k % 2 == 0) mp[i] = MdProbePair(mdWriters[i], 3); else mp0[i] = MdProbePair(mdWriters[i], 0);
        });
        Console.WriteLine("| B（書き手） | 発火/戦 | 傷があった率 | 平均の深さ | **増分/戦** | 総被ダメ/戦 | **増分/被ダメ** | 味方傷/戦 | 在庫(味)/T | 現行の回復/戦 | 決着T | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var pTot = new MdStat();
        for (int i = 0; i < mdWriters.Length; i++)
        {
            var sv = MdSum(mp[i]); pTot.AddFrom(sv);
            var pp = MdPaper(sv);
            Console.WriteLine($"| {mdName[mdWriters[i]]} | {pp.fires:F2} | {pp.rate * 100:F1}% | {pp.depth:F2} | **{pp.paper:F1}** | {pp.taken:F1} | **{pp.paper / pp.taken * 100:F1}%** | {sv.CarryWoundAlly / sv.N:F2} | {sv.StockAlly / sv.N:F2} | {pp.heal:F1} | {pp.turns:F1} | {sv.Wins * 100.0 / sv.N:F1}% |");
        }
        var pA = MdPaper(pTot);
        Console.WriteLine($"| **6枚合算** | **{pA.fires:F2}** | **{pA.rate * 100:F1}%** | **{pA.depth:F2}** | **{pA.paper:F1}** | {pA.taken:F1} | **{pA.paper / pA.taken * 100:F1}%** | {pTot.CarryWoundAlly / pTot.N:F2} | {pTot.StockAlly / pTot.N:F2} | {pA.heal:F1} | {pA.turns:F1} | {pTot.Wins * 100.0 / pTot.N:F1}% |");
        Console.WriteLine();
        Console.WriteLine("| 波 | 発火/戦 | 傷があった率 | 平均の深さ | 増分/戦 | 総被ダメ/戦 | 増分/被ダメ | 決着T | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv < mdW; wv++)
        {
            var sv = new MdStat(); for (int i = 0; i < mdWriters.Length; i++) sv.AddFrom(mp[i][wv]);
            var pp = MdPaper(sv);
            Console.WriteLine($"| 第{wv + 1}波 | {pp.fires:F2} | {pp.rate * 100:F1}% | {pp.depth:F2} | {pp.paper:F1} | {pp.taken:F1} | {pp.paper / pp.taken * 100:F1}% | {pp.turns:F1} | {sv.Wins * 100.0 / sv.N:F1}% |");
        }
        Console.WriteLine();
        var t0 = new MdStat(); for (int i = 0; i < mdWriters.Length; i++) t0.AddFrom(MdSum(mp0[i]));
        var p0 = MdPaper(t0);
        Console.WriteLine($"**X0（巻き込み則なし）との対照**: 発火/戦 {p0.fires:F2}（X1P {pA.fires:F2}）・患者に傷があった率 **{p0.rate * 100:F1}%**（X1P {pA.rate * 100:F1}%）・"
                          + $"味方傷/戦 {t0.CarryWoundAlly / t0.N:F2}（X1P {pTot.CarryWoundAlly / pTot.N:F2}）。**X0 で率が 0 であることが「味方に傷を書く経路が現行に無い」の再確認**（第49・68・84期）。");
        Console.WriteLine();
        bool pass1 = pA.paper / pA.taken * 100 >= MdPaperFloor;
        bool pass2 = pA.fires > MdHariFires;
        Console.WriteLine($"> **停止条件 1: 傷由来の増分/戦 {pA.paper:F1} ÷ 総被ダメ/戦 {pA.taken:F1} = {pA.paper / pA.taken * 100:F1}%** ≥ {MdPaperFloor:F0}% → **{(pass1 ? "○" : "×")}**");
        Console.WriteLine($"> **停止条件 2: ノノの発火回数/戦 {pA.fires:F2}** > ハリの {MdHariFires:F2}（第85期） → **{(pass2 ? "○" : "×")}**");
        Console.WriteLine(">");
        Console.WriteLine($"> **{(pass1 && pass2 ? "2つとも満たす。実装して測る" : "満たさない。§5 の分岐へ（紙の計算だけを報告して閉じる）")}**");
        Console.WriteLine();
        Console.WriteLine("**推定で埋めた項は無い**（発火回数・率・深さ・総被ダメはすべてこの実行の実測）。"
                          + "ただし深さは**塞ぎが走らない世界**の値なので上限側——X1 では繕うたびに 1 つ塞ぐので実際の深さはこれより浅くなる。");
        Console.WriteLine();

        // ---- 1-2-1 isFriendlyFire の呼び出し口の全数 ------------------------------------------------
        Console.WriteLine("## 1-2-1. `isFriendlyFire: true` の呼び出し口の全数（第85期の再取得。**指示書の数字を信用しない**）");
        Console.WriteLine();
        Console.WriteLine("| ファイル:行 | 特性（駒） | 周期 | `source` | X1 | X2 | 行 |");
        Console.WriteLine("|---|---|---|---|:-:|:-:|---|");
        var ffMap = new Dictionary<string, (string who, string period, string src, bool x1, bool x2)>
        {
            ["SplashTrait"] = ("余波（ボルグ）", "攻撃ごと（隣接の味方）", "自分（同陣営）", true, true),
            ["SacrificeTrait"] = ("生贄（リィカ）", "開戦時1回（隣接の味方・`lethal: false`）", "自分（同陣営）", true, false),
            ["DrainTrait"] = ("吸い（ゴルム）", "毎ターン（味方全員）", "自分（同陣営）", true, true),
            ["BomberTrait"] = ("破裂の味方巻き込み（ゾト）", "死亡時（味方全員）", "自分（同陣営）", true, false),
            ["ThornsTrait"] = ("棘の巻き込み（カド）", "被弾ごと（隣接の味方）", "自分（同陣営）", true, false),
            ["PursuerTrait"] = ("深追いの反動（ハギ）", "追い打ちの連鎖ごと（自分）", "**null**", false, false),
            ["ForsakeTrait"] = ("置き去りの削り（ナラ）", "毎ターン（自分より遅い味方・`lethal: true`）", "自分（同陣営）", true, false),
        };
        int ffN = 0, ffX1 = 0, ffX2 = 0;
        foreach (string file in new[] { "BattleCore/Traits.cs", "BattleCore/BattleEngine.cs" })
        {
            if (!File.Exists(file)) continue;
            string[] ls = File.ReadAllLines(file);
            string cls = "";
            for (int i = 0; i < ls.Length; i++)
            {
                string t = ls[i].Trim();
                var m = System.Text.RegularExpressions.Regex.Match(ls[i], @"^(?:public |internal |file )?(?:sealed |abstract )?class (\w+)");
                if (m.Success) cls = m.Groups[1].Value;
                if (!t.Contains("isFriendlyFire: true")) continue;
                if (!t.Contains("ApplyDamage(")) continue;
                ffN++;
                string who, period, src; bool x1, x2;
                if (file.EndsWith("BattleEngine.cs"))
                {
                    if (t.Contains("wall,")) { who = "巨躯の中継（engine）"; period = "被弾ごと（肩代わり）"; src = "**元の攻撃者**（味方の刃なら同陣営）"; }
                    else if (t.Contains("sharer,")) { who = "分かちの中継（engine）"; period = "被弾ごと（肩代わり）"; src = "**元の攻撃者**（味方の刃なら同陣営）"; }
                    else if (t.Contains("relayer,")) { who = "転嫁の代金（engine・ワタ）"; period = "横取りごと（自分の HP）"; src = "**null**"; }
                    else { who = "（未分類）"; period = "—"; src = "—"; }
                    x1 = false; x2 = false;
                }
                else if (ffMap.TryGetValue(cls, out var e)) { who = e.who; period = e.period; src = e.src; x1 = e.x1; x2 = e.x2; }
                else { who = $"（未分類 {cls}）"; period = "—"; src = "—"; x1 = false; x2 = false; }
                if (x1) ffX1++;
                if (x2) ffX2++;
                Console.WriteLine($"| `{file}:{i + 1}` | {who} | {period} | {src} | {(x1 ? "**○**" : "×")} | {(x2 ? "**○**" : "×")} | `{t.Replace("|", "\\|")}` |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"**呼び出し口 {ffN}・X1 の書き手 {ffX1}・X2 の書き手 {ffX2}。**"
                          + " 中継（巨躯・分かち）は札 `relayed` で外れ、転嫁の代金と深追いの反動は `source` が `null` で外れる（第85期の実装のまま）。");
        Console.WriteLine();
        Console.WriteLine("**書き手が対応する駒**（`UnitCatalog.All` から数え直した。特性の保持者）: "
                          + string.Join(" ／ ", new[] { TraitId.Splash, TraitId.Sacrifice, TraitId.Drain, TraitId.Bomber, TraitId.Thorns, TraitId.Forsake }
                              .Select(tid => $"`{tid}` {string.Join("・", mdRoster.Where(d => d.Traits.Contains(tid)).Select(d => d.Name))}")) + "。");
        Console.WriteLine();
        Console.WriteLine("**`SutureRule`（ハリの両側読み）は既定 `Foe` のまま触っていない**（この期は起こさない）。");
        Console.WriteLine("第85期の自己検査 (j) の再掲: **読まれないまま落ちた傷は 味方側 84% / 敵側 78%**。");
        Console.WriteLine();

        // ---- 1-3 拒否権が立つ行 -----------------------------------------------------------------
        Console.WriteLine("## 1-3. 拒否権が立つ行（**測る前に名指しする**）");
        Console.WriteLine();
        Console.WriteLine($"ノノを含む行 **{mdNonoRows.Length}**: " + string.Join(" / ", mdNonoRows.Select(rw => $"`{rw.Name}`{(mdPrimary.Contains(rw.Name) ? "**［主判定］**" : "")}")));
        Console.WriteLine();
        Console.WriteLine($"うち **X1 で動くはずの行（書き手6枚のいずれかが同席）= {mdX1Rows.Length} 行**: "
                          + string.Join(" / ", mdX1Rows.Select(rw => $"`{rw.Name}`（{string.Join("・", mdWriterIds.Where(id => MdHas(rw.F, id)).Select(id => mdName[mdIdx[id]]))}）{(mdPrimary.Contains(rw.Name) ? "**［主判定］**" : "")}")));
        Console.WriteLine();
        Console.WriteLine($"うち **X2 で動くはずの行（吸い・余波が同席）= {mdX2Rows.Length} 行**: "
                          + string.Join(" / ", mdX2Rows.Select(rw => $"`{rw.Name}`")));
        Console.WriteLine();
        Console.WriteLine($"**主判定 {Baseline.PrimaryRows.Length} 行に入るもの = {mdX1Rows.Count(rw => mdPrimary.Contains(rw.Name))} 行**: "
                          + string.Join(" / ", mdX1Rows.Where(rw => mdPrimary.Contains(rw.Name)).Select(rw => $"`{rw.Name}`"))
                          + "。**この期は拒否権が原理的に立つ**（第84期・第85期 W1 は交わり 0 行だった）。");
        Console.WriteLine();

        // ---- 1-4 潰れる相互作用 -----------------------------------------------------------------
        Console.WriteLine("## 1-4. 潰れる相互作用");
        Console.WriteLine();
        {
            var galdRows = mdNonoRows.Where(rw => MdHas(rw.F, "gald")).ToArray();
            var g = new MdStat();
            foreach (var rw in galdRows)
                for (int wv = 1; wv < mdW; wv++)
                    for (int seed = MdBand; seed < MdBand + MdM; seed++) MdProbe(rw.F, mdWeak[wv].Enemy, seed, 3, g);
            Console.WriteLine($"1. **ガルドは患者にならない**（`Stoic` が `MostHurtAlly` の `AcceptsSupport` で落ちる）。"
                              + $"ノノとガルドが同席する行 {galdRows.Length}（{string.Join(" / ", galdRows.Select(rw => rw.Name))}）を X1P で {g.N:F0} 戦回して、"
                              + $"**繕いの患者がガルドだった回数 {g.GaldPatient:F0}**（発火 {g.MendFires:F0} 回）→ {(g.GaldPatient == 0 ? "**0。確認**" : "**0 でない。要調査**")}");
        }
        Console.WriteLine();
        {
            var d3 = new MdStat(); for (int i = 0; i < mdWriters.Length; i++) d3.AddFrom(mp[i][2]);
            var pd = MdPaper(d3);
            Console.WriteLine($"2. **渇き（第三波）でノノは一方的に減る。** X1P の第三波は 発火 {pd.fires:F2} 回/戦・そのうち 1 点も届かなかった回数 {d3.MendDry / d3.N:F2} 回/戦"
                              + $"（払った HP {d3.MendPaid / d3.N:F1}／届いた HP {d3.MendHealed / d3.N:F1}）。傷を読むと繕いが増えるので X1 ではここが**傷の深さのぶんだけ**速くなる"
                              + $"（紙の見積り: 払う HP が {pd.paper:F1} 増える ＝ 第三波の現行の払い {d3.MendPaid / d3.N:F1} の {(d3.MendPaid > 0 ? pd.paper / (d3.MendPaid / d3.N) * 100 : 0):F0}%）。"
                              + $"ノノの寿命 {d3.NonoLife / d3.N:F2}T・死亡率 {d3.NonoDeaths * 100.0 / d3.N:F1}%。**崖になっていないかは拒否権3（第三波 −10.0pt）で見る。**");
        }
        Console.WriteLine();
        Console.WriteLine("3. **塞ぎの競合は起きない。** 味方の傷の読み手はこの期でもノノ 1 枚だけ（ガルド・ドハ・ザンは §6 で触らないと決めてある）。"
                          + "**起きないことを記録して次期の材料にする。**");
        Console.WriteLine();
        Console.WriteLine($"4. **敵の従軍司祭長も `Mender` を持つ。** X1P の実測で敵側の繕い {pTot.FoeMendFires / pTot.N:F2} 回/戦・**そのうち患者に傷があった回数 {pTot.FoeMendSeen:F0}**"
                          + $" → {(pTot.FoeMendSeen == 0 ? "**0。味方の傷を書く手段が敵側に無いので実害は無い**" : "**0 でない。要調査**")}（§7）。");
        Console.WriteLine();

        Console.WriteLine("## 予測（測る前に書いた。§1 の写し）");
        Console.WriteLine();
        Console.WriteLine("- `compare` で最も動くのは `燃焼 (ボルグ×ホタ)`、次が `耐久 (ガルド×ノノ)`。どちらも主判定19行。");
        Console.WriteLine("- カド同席の行は第二波で動かない（粛で棘が止まる。第84期 (g)）。");
        Console.WriteLine("- リィカ（開戦1回）とゾト（死亡時）は X2 で外れても差が出ない（第67期・長い周期）。");
        Console.WriteLine("- 第三波は下がる（渇き × 繕いの増加）。唯一 X0 を下回る波になるはず。");
        Console.WriteLine("- 患者に傷があった率は、カド・ゴルム・ボルグのいずれかが同席する台で 6 割以上。これが低ければ主判定は落ちる。");
        Console.WriteLine();
        Console.WriteLine($"所要 {mdSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // run <x> [skip] [take]
    // =====================================================================================
    if (mdArg == "run")
    {
        int x = args.Length > 3 ? int.Parse(args[3]) : 0;
        int skip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int take = args.Length > 5 ? int.Parse(args[5]) : mdOthers.Length;
        skip = Math.Clamp(skip, 0, mdOthers.Length);
        take = Math.Clamp(take, 0, mdOthers.Length - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"mender run x{x} {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            int b = mdOthers[skip + j];
            var ys = MdMeasure(b, x);
            var sb = new System.Text.StringBuilder();
            sb.Append(x).Append('\t').Append(mdNono).Append('\t').Append(b).Append('\t').Append(ys.Length);
            foreach (double[] y in ys) for (int k = 0; k < 4; k++) sb.Append('\t').Append(y[k].ToString("R", mdInv));
            rows[j] = sb.ToString();
            if (Interlocked.Increment(ref doneR) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {mdSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    Dictionary<int, double[][]> MdRead(string path, int expectX)
    {
        var d = new Dictionary<int, double[][]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int x = int.Parse(c[0]);
            if (x != expectX) throw new InvalidOperationException($"{path}: 版 {x} の行が混ざっている（期待 {expectX}）");
            int b = int.Parse(c[2]); int nT = int.Parse(c[3]);
            var ys = new double[nT][];
            int at = 4;
            for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], mdInv); }
            d[b] = ys;
        }
        return d;
    }
    // 添字: 0 = y11 / 1 = y01（ノノ素体）/ 2 = y10（B 素体）/ 3 = y00
    double MdSyn(double[] y) => y[0] - y[2] - y[1] + y[3];

    // =====================================================================================
    // tables <x0> <x1> <x2>
    // =====================================================================================
    if (mdArg == "tables")
    {
        if (args.Length < 6 || !File.Exists(args[3]) || !File.Exists(args[4]) || !File.Exists(args[5]))
        {
            Console.WriteLine("mender tables <x0 の TSV> <x1 の TSV> <x2 の TSV>");
            return;
        }
        var Y = new[] { MdRead(args[3], 0), MdRead(args[4], 1), MdRead(args[5], 2) };
        var missing = mdOthers.Where(b => Y.Any(y => !y.ContainsKey(b))).ToArray();

        Console.WriteLine("# 第86期 —— 繕いに傷を読ませる（表A〜F）");
        Console.WriteLine();
        Console.WriteLine($"器具は第81期の 2×2 の写し（K = {MdK} 台 × {MdS} 系列・弱い波 {MdWeakPct}%・第2〜5波・seed {MdBand}..{MdBand + MdM - 1}）。"
                          + $"**A はノノ固定**・B は残り {mdOthers.Length} 体。**台・席・戦闘 seed・台の抽選は X0 / X1 / X2 で同一**。"
                          + "TSV の添字は **0 = y11 ／ 1 = y01（ノノ素体）／ 2 = y10（B 素体）／ 3 = y00**"
                          + "（第85期の罠: A ＝ ノノなので、A ＝ ハリだった第85期とは添字 1 と 2 の意味が入れ替わる）。"
                          + (missing.Length > 0 ? $" **TSV に無い組 {missing.Length}**: " + string.Join(" / ", missing.Select(b => mdName[b])) : ""));
        Console.WriteLine();

        Dictionary<int, double[]>[] dAll = { new(), new() };
        Dictionary<int, double[][]>[] dSer = { new(), new() };
        foreach (int b in mdOthers)
        {
            if (Y.Any(y => !y.ContainsKey(b))) continue;
            int nT = Y.Min(y => y[b].Length);
            for (int x = 1; x <= 2; x++)
            {
                var d = new double[nT];
                var ser = new List<double>[MdS];
                for (int sx = 0; sx < MdS; sx++) ser[sx] = new List<double>();
                for (int t = 0; t < nT; t++) { d[t] = MdSyn(Y[x][b][t]) - MdSyn(Y[0][b][t]); ser[t % MdS].Add(d[t]); }
                dAll[x - 1][b] = d; dSer[x - 1][b] = ser.Select(l => l.ToArray()).ToArray();
            }
        }

        // verbose の probe（書き手6枚 × 4版）
        var cst = new MdStat[mdWriters.Length, 4, mdW];
        Parallel.For(0, mdWriters.Length * 4, k =>
        {
            int i = k / 4, x = k % 4;
            var loc = MdProbePair(mdWriters[i], x);
            for (int wv = 0; wv < mdW; wv++) cst[i, x, wv] = loc[wv];
        });
        MdStat Agg(int x, int wv)
        {
            var s = new MdStat();
            for (int i = 0; i < mdWriters.Length; i++)
            {
                if (wv > 0) s.AddFrom(cst[i, x, wv]);
                else for (int v2 = 1; v2 < mdW; v2++) s.AddFrom(cst[i, x, v2]);
            }
            return s;
        }

        // ---- 表A: 紙と実測（自己検査 (h)）--------------------------------------------------------
        Console.WriteLine("## 表A —— 紙のスループット（§1-1）と実測の突き合わせ【自己検査 (h)】");
        Console.WriteLine();
        Console.WriteLine("紙は X1P（供給だけ・読み手なし）の分子から出す。実測は **X1 の `MendHealed` − X0 の `MendHealed`**（傷由来の増分そのもの）。"
                          + "**1.5 倍以内で一致すれば (h) は通る。**");
        Console.WriteLine();
        Console.WriteLine("| 波 | 紙: 発火/戦 | 率 | 深さ | **紙の増分/戦** | X0 繕い/戦 | **X1 繕い/戦** | **実測の増分** | 実測/紙 | X2 繕い/戦 | 総被ダメ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv <= mdW; wv++)
        {
            MdStat sP = Agg(3, wv < mdW ? wv : 0), s0 = Agg(0, wv < mdW ? wv : 0), s1 = Agg(1, wv < mdW ? wv : 0), s2 = Agg(2, wv < mdW ? wv : 0);
            var pp = MdPaper(sP);
            double m0 = s0.MendHealed / s0.N, m1 = s1.MendHealed / s1.N, m2 = s2.MendHealed / s2.N;
            Console.WriteLine($"| {(wv < mdW ? $"第{wv + 1}波" : "**第2〜5波**")} | {pp.fires:F2} | {pp.rate * 100:F1}% | {pp.depth:F2} | **{pp.paper:F1}** | {m0:F1} | **{m1:F1}** | **{m1 - m0:F1}** | {(pp.paper > 0 ? ((m1 - m0) / pp.paper).ToString("F2") : "—")} | {m2:F1} | {pp.taken:F1} |");
        }
        {
            MdStat sP = Agg(3, 0), s0 = Agg(0, 0), s1 = Agg(1, 0);
            var pp = MdPaper(sP); double inc = s1.MendHealed / s1.N - s0.MendHealed / s0.N;
            bool hOk = pp.paper > 0 && inc >= pp.paper / 1.5 && inc <= pp.paper * 1.5;
            Console.WriteLine();
            Console.WriteLine($"**(h)**: 紙 {pp.paper:F1} 対 実測 {inc:F1}（比 {(pp.paper > 0 ? inc / pp.paper : double.NaN):F2}）→ **{(hOk ? "○（1.5 倍以内）" : "×")}**。"
                              + $"増分が総被ダメ {pp.taken:F1} に占める割合は **{inc / pp.taken * 100:F1}%**（紙は {pp.paper / pp.taken * 100:F1}%・線 {MdPaperFloor:F0}%）。");
        }
        Console.WriteLine();

        // ---- 表B: 読み手の発火回数（副判定 (A)）----------------------------------------------------
        Console.WriteLine("## 表B —— 読み手の発火回数【副判定 (A)。この期から常設】");
        Console.WriteLine();
        Console.WriteLine($"ノノの繕い発火回数/戦（波別）と、そのうち**傷を読めた回数**。第85期のハリは **{MdHariFires:F2} 回/戦**（同じ台・同じ seed 帯）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | **発火/戦** | 傷を読めた/戦 | 率 | 平均の深さ | 繕い/戦 | 払った HP/戦 | 乾き/戦 | ハリ 2.15 との比 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv <= mdW; wv++)
            for (int x = 0; x < 3; x++)
            {
                MdStat sv = Agg(x, wv < mdW ? wv : 0);
                if (sv.N == 0) continue;
                double fires = sv.MendFires / sv.N;
                Console.WriteLine($"| {(wv < mdW ? $"第{wv + 1}波" : "**第2〜5波**")} | X{x} | **{fires:F2}** | {sv.MendSeen / sv.N:F2} | {(sv.MendFires > 0 ? sv.MendSeen / sv.MendFires * 100 : 0):F1}% | {(sv.MendSeen > 0 ? sv.MendDepth / sv.MendSeen : 0):F2} | {sv.MendHealed / sv.N:F1} | {sv.MendPaid / sv.N:F1} | {sv.MendDry / sv.N:F2} | **{fires / MdHariFires:F2}×** |");
            }
        {
            MdStat s1 = Agg(1, 0);
            double fires = s1.MendFires / s1.N;
            Console.WriteLine();
            Console.WriteLine($"**副判定 (A)**: ノノの発火 **{fires:F2} 回/戦** 対 ハリ {MdHariFires:F2} 回/戦 → **{fires / MdHariFires:F2} 倍**"
                              + $"（{(fires > MdHariFires ? "**上回った**" : "下回った")}）。傷を読めた回数 **{s1.MendSeen / s1.N:F2} 回/戦**。");
        }
        Console.WriteLine();

        // ---- 表C: 主判定 ------------------------------------------------------------------------
        Console.WriteLine("## 表C —— 味方に傷を書く6枚の 相乗 X0 / X1 / X2 / Δ（系列別・SE 併記）【主判定 Q1】");
        Console.WriteLine();
        Console.WriteLine("`相乗(ノノ,B) = y11 − y10 − y01 + y00`／`Δ相乗_X = 相乗_X − 相乗_X0`（**同じ台・同じ seed の対**なので Δ の SE は台ごとの Δ から出す）。");
        Console.WriteLine();
        Console.WriteLine("| B | 系列 | 台数 | 相乗 X0 | SE | 相乗 X1 | **Δ X1** | SE(Δ) | 相乗 X2 | **Δ X2** | SE(Δ) | y11 X0 | y11 X1 | y11 X2 | y00 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var q1Ser = new double[MdS]; var q2Ser = new double[MdS];
        var q1Vals = new List<double>(); var q2Vals = new List<double>();
        foreach (int b in mdWriters)
        {
            if (!dAll[0].ContainsKey(b)) { Console.WriteLine($"| {mdName[b]} | — | 0 | | | | | | | | | | | | |"); continue; }
            int nT = dAll[0][b].Length;
            for (int sx = 0; sx < MdS; sx++)
            {
                var ts = Enumerable.Range(0, nT).Where(t => t % MdS == sx).ToArray();
                var s0 = ts.Select(t => MdSyn(Y[0][b][t])).ToList(); var s1 = ts.Select(t => MdSyn(Y[1][b][t])).ToList(); var s2 = ts.Select(t => MdSyn(Y[2][b][t])).ToList();
                var d1 = dSer[0][b][sx]; var d2 = dSer[1][b][sx];
                Console.WriteLine($"| {mdName[b]} | {sx + 1} | {ts.Length} | {MdP2(s0.Average())} | {MdSe(s0):F2} | {MdP2(s1.Average())} | **{MdP2(d1.Average())}** | {MdSe(d1):F2} | {MdP2(s2.Average())} | **{MdP2(d2.Average())}** | {MdSe(d2):F2} | {ts.Average(t => Y[0][b][t][0]):F1} | {ts.Average(t => Y[1][b][t][0]):F1} | {ts.Average(t => Y[2][b][t][0]):F1} | {ts.Average(t => Y[0][b][t][3]):F1} |");
                q1Ser[sx] += d1.Average() / mdWriters.Length;
                q2Ser[sx] += d2.Average() / mdWriters.Length;
            }
            {
                var s0 = Y[0][b].Take(nT).Select(MdSyn).ToList(); var s1 = Y[1][b].Take(nT).Select(MdSyn).ToList(); var s2 = Y[2][b].Take(nT).Select(MdSyn).ToList();
                Console.WriteLine($"| **{mdName[b]}** | 合算 | {nT} | {MdP2(s0.Average())} | {MdSe(s0):F2} | {MdP2(s1.Average())} | **{MdP2(dAll[0][b].Average())}** | {MdSe(dAll[0][b]):F2} | {MdP2(s2.Average())} | **{MdP2(dAll[1][b].Average())}** | {MdSe(dAll[1][b]):F2} | {Y[0][b].Take(nT).Average(y => y[0]):F1} | {Y[1][b].Take(nT).Average(y => y[0]):F1} | {Y[2][b].Take(nT).Average(y => y[0]):F1} | {Y[0][b].Take(nT).Average(y => y[3]):F1} |");
                q1Vals.Add(dAll[0][b].Average()); q2Vals.Add(dAll[1][b].Average());
            }
        }
        double q1 = q1Vals.Count > 0 ? q1Vals.Average() : double.NaN;
        double q2 = q2Vals.Count > 0 ? q2Vals.Average() : double.NaN;
        Console.WriteLine();
        Console.WriteLine($"**Q1（X1）: 6枚の Δ相乗 の平均 = {MdP2(q1)}pt**（系列1 {MdP2(q1Ser[0])} / 系列2 {MdP2(q1Ser[1])}）。線 +{MdQ1Line:F1} → **{(q1 >= MdQ1Line ? "○" : "×")}**");
        Console.WriteLine($"**Q1（X2）: 6枚の Δ相乗 の平均 = {MdP2(q2)}pt**（系列1 {MdP2(q2Ser[0])} / 系列2 {MdP2(q2Ser[1])}）。線 +{MdQ1Line:F1} → **{(q2 >= MdQ1Line ? "○" : "×")}**");
        Console.WriteLine($"**吸い・余波の2枚だけの Δ 平均**: X1 {MdP2(mdDenseIds.Where(id => dAll[0].ContainsKey(mdIdx[id])).Average(id => dAll[0][mdIdx[id]].Average()))} ／ "
                          + $"X2 {MdP2(mdDenseIds.Where(id => dAll[1].ContainsKey(mdIdx[id])).Average(id => dAll[1][mdIdx[id]].Average()))}。"
                          + $"**X2 で外した4枚（棘・生贄・破裂・置き去り）の X1 の Δ 平均**: {MdP2(mdWriterIds.Where(id => !mdDenseIds.Contains(id) && dAll[0].ContainsKey(mdIdx[id])).Average(id => dAll[0][mdIdx[id]].Average()))}。");
        Console.WriteLine();

        // ---- 表C-x: ノノ × 50 --------------------------------------------------------------------
        for (int x = 1; x <= 2; x++)
        {
            Console.WriteLine($"## 表C-{x} —— ノノ × 全 {mdOthers.Length} 体の Δ相乗 {MdVName(x)}（降順・上位 {MdTop} と下位 {MdTop}）");
            Console.WriteLine();
            Console.WriteLine("★ は味方に傷を書く6枚。`有意` は |Δ| > 2SE(Δ)。**個数は主判定に混ぜない。**");
            Console.WriteLine();
            var dA = dAll[x - 1]; var dS = dSer[x - 1];
            var order = dA.Keys.OrderByDescending(b => dA[b].Average()).ToArray();
            Console.WriteLine($"| 順位 | B | Δ相乗 X{x} | SE(Δ) | 有意 | 相乗 X0 | 相乗 X{x} | 系列1 Δ | 系列2 Δ | y00 |");
            Console.WriteLine("|--:|---|--:|--:|:-:|--:|--:|--:|--:|--:|");
            void CRow(int rank, int b)
            {
                double m = dA[b].Average(), se = MdSe(dA[b]);
                int nT = dA[b].Length;
                Console.WriteLine($"| {rank} | {(mdWriters.Contains(b) ? "★" : "")}{mdName[b]} | **{MdP2(m)}** | {se:F2} | {(Math.Abs(m) > 2 * se ? "○" : "")} | {MdP2(Y[0][b].Take(nT).Select(MdSyn).Average())} | {MdP2(Y[x][b].Take(nT).Select(MdSyn).Average())} | {MdP2(dS[b][0].Average())} | {MdP2(dS[b][1].Average())} | {Y[0][b].Take(nT).Average(y => y[3]):F1} |");
            }
            for (int i = 0; i < Math.Min(MdTop, order.Length); i++) CRow(i + 1, order[i]);
            if (order.Length > 2 * MdTop) Console.WriteLine("| … | | | | | | | | | |");
            for (int i = Math.Max(MdTop, order.Length - MdTop); i < order.Length; i++) CRow(i + 1, order[i]);
            Console.WriteLine();
            int nPos = order.Count(b => dA[b].Average() > 2 * MdSe(dA[b])), nNeg = order.Count(b => dA[b].Average() < -2 * MdSe(dA[b]));
            Console.WriteLine($"全 {order.Length} 体の Δ相乗 の平均 {MdP2(order.Average(b => dA[b].Average()))}・中央値 {MdP2(order.Select(b => dA[b].Average()).OrderBy(v => v).ElementAt(order.Length / 2))}・"
                              + $"有意な正 {nPos} / 有意な負 {nNeg}（参考）。6枚の順位: "
                              + string.Join(" / ", mdWriters.Where(b => dA.ContainsKey(b)).Select(b => $"{mdName[b]} {Array.IndexOf(order, b) + 1} 位")) + "。");
            Console.WriteLine();
        }

        // ---- 表D: 供給と在庫（Q2・Q3）------------------------------------------------------------
        Console.WriteLine("## 表D —— 味方側の供給と在庫（Q2・Q3。波別・書き手別）");
        Console.WriteLine();
        Console.WriteLine("| B | 波 | 版 | 味方傷/戦 | 巻(B)/戦 | 巻(全)/戦 | **在庫(味)/T** | 最大 | **患者に傷があった率** | 深さ | 決着T | 勝率 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < mdWriters.Length; i++)
            for (int wv = 1; wv < mdW; wv++)
                for (int x = 0; x < 3; x++)
                {
                    MdStat sx = cst[i, x, wv];
                    if (sx.N == 0) continue;
                    Console.WriteLine($"| {mdName[mdWriters[i]]} | 第{wv + 1}波 | X{x} | {sx.CarryWoundAlly / sx.N:F2} | {sx.SpillByWriter[i] / sx.N:F2} | {sx.SpillWounds / sx.N:F2} | **{sx.StockAlly / sx.N:F2}** | {sx.StockAllyMax / sx.N:F2} | **{(sx.MendFires > 0 ? sx.MendSeen / sx.MendFires * 100 : 0):F1}%** | {(sx.MendSeen > 0 ? sx.MendDepth / sx.MendSeen : 0):F2} | {sx.Turns / sx.N:F1} | {sx.Wins * 100.0 / sx.N:F1}% |");
                }
        Console.WriteLine();
        Console.WriteLine("| 書き手 | X1 巻き込みで書いた/戦 | X2 | 第2波(X1) | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int k = 0; k < mdWriters.Length; k++)
        {
            double t1 = 0, t2 = 0, n1 = 0, n2 = 0; var byW = new double[mdW];
            for (int i = 0; i < mdWriters.Length; i++)
                for (int wv = 1; wv < mdW; wv++)
                {
                    t1 += cst[i, 1, wv].SpillByWriter[k]; n1 += cst[i, 1, wv].N;
                    t2 += cst[i, 2, wv].SpillByWriter[k]; n2 += cst[i, 2, wv].N;
                    byW[wv] += cst[i, 1, wv].N > 0 ? cst[i, 1, wv].SpillByWriter[k] / cst[i, 1, wv].N / mdWriters.Length : 0;
                }
            Console.WriteLine($"| {mdName[mdWriters[k]]}{(mdDenseIds.Contains(mdWriterIds[k]) ? "（X2 で残る）" : "")} | {t1 / n1:F3} | {t2 / n2:F3} | {byW[1]:F3} | {byW[2]:F3} | {byW[3]:F3} | {byW[4]:F3} |");
        }
        Console.WriteLine();
        {
            MdStat a1 = Agg(1, 0), a2 = Agg(2, 0);
            bool subset = Enumerable.Range(0, mdWriters.Length).All(k => a2.SpillByWriter[k] <= a1.SpillByWriter[k] + 1e-9)
                          && Enumerable.Range(0, mdWriters.Length).All(k => mdDenseIds.Contains(mdWriterIds[k]) || a2.SpillByWriter[k] == 0);
            Console.WriteLine($"**(c) X2 の供給は X1 の部分集合か**: X2 で書いた書き手 = "
                              + string.Join(" / ", Enumerable.Range(0, mdWriters.Length).Where(k => a2.SpillByWriter[k] > 0).Select(k => mdName[mdWriters[k]]))
                              + $" → **{(subset ? "○" : "×")}**。供給の総量 X1 {a1.SpillWounds / a1.N:F2} → X2 {a2.SpillWounds / a2.N:F2} 回/戦（{(a1.SpillWounds > 0 ? a2.SpillWounds / a1.SpillWounds * 100 : 0):F0}%）。");
        }
        Console.WriteLine();

        // ---- 表E: 寿命（Q4）と未読の傷（Q5）--------------------------------------------------------
        Console.WriteLine("## 表E —— ノノの寿命（Q4）と未読の傷（Q5）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | **ノノの寿命(T)** | 死亡% | 払った HP/戦 | 繕い/戦 | 乾き/戦 | 味方全体の回復/戦 | **未読(味)** | 未読(敵) | 決着T | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv <= mdW; wv++)
            for (int x = 0; x < 3; x++)
            {
                MdStat sv = Agg(x, wv < mdW ? wv : 0);
                if (sv.N == 0) continue;
                Console.WriteLine($"| {(wv < mdW ? $"第{wv + 1}波" : "**第2〜5波**")} | X{x} | **{sv.NonoLife / sv.N:F2}** | {sv.NonoDeaths * 100.0 / sv.N:F1}% | {sv.MendPaid / sv.N:F1} | {sv.MendHealed / sv.N:F1} | {sv.MendDry / sv.N:F2} | {sv.TeamHealed / sv.N:F1} | {(sv.CarryWoundAlly > 0 ? ((sv.DeathWoundAlly + sv.EndWoundAlly) / sv.CarryWoundAlly * 100).ToString("F1") + "%" : "—")} | {(sv.CarryWoundFoe > 0 ? ((sv.DeathWoundFoe + sv.EndWoundFoe) / sv.CarryWoundFoe * 100).ToString("F1") + "%" : "—")} | {sv.Turns / sv.N:F1} | {sv.Wins * 100.0 / sv.N:F1}% |");
            }
        Console.WriteLine();
        {
            MdStat a0 = Agg(0, 0), a1 = Agg(1, 0), t3a = Agg(0, 2), t3b = Agg(1, 2);
            Console.WriteLine($"**Q4**: 第2〜5波の寿命 X0 {a0.NonoLife / a0.N:F2}T → X1 {a1.NonoLife / a1.N:F2}T（Δ {a1.NonoLife / a1.N - a0.NonoLife / a0.N:+0.00;-0.00}）。"
                              + $"**第三波（渇き）だけ**: X0 {t3a.NonoLife / t3a.N:F2}T → X1 {t3b.NonoLife / t3b.N:F2}T（Δ {t3b.NonoLife / t3b.N - t3a.NonoLife / t3a.N:+0.00;-0.00}・死亡率 {t3a.NonoDeaths * 100.0 / t3a.N:F1}% → {t3b.NonoDeaths * 100.0 / t3b.N:F1}%）。");
            Console.WriteLine();
            Console.WriteLine($"**Q5**: 未読の傷（味方側）X1 **{(a1.CarryWoundAlly > 0 ? (a1.DeathWoundAlly + a1.EndWoundAlly) / a1.CarryWoundAlly * 100 : double.NaN):F1}%**"
                              + "（第85期は 84%。**下がらないなら読み手をもう1枚足しても同じ場所で止まる**）。");
        }
        Console.WriteLine();

        // ---- 表F: 自己検査 ----------------------------------------------------------------------
        Console.WriteLine("## 表F —— 自己検査（(a)(c)(d)(e)(f)(h)(i)。(b)(g)(j) と拒否権は `mender check`）");
        Console.WriteLine();
        double maxA = 0; int cellsA = 0;
        foreach (int b in mdOthers)
        {
            if (Y.Any(y => !y.ContainsKey(b))) continue;
            int nT = Y.Min(y => y[b].Length);
            for (int t = 0; t < nT; t++)
                foreach (int k in new[] { 1, 3 })
                    for (int x = 1; x <= 2; x++) { cellsA++; maxA = Math.Max(maxA, Math.Abs(Y[0][b][t][k] - Y[x][b][t][k])); }
        }
        MdStat A0 = Agg(0, 0), A1 = Agg(1, 0), A2 = Agg(2, 0), AP = Agg(3, 0);
        var pAll2 = MdPaper(AP); double incAll = A1.MendHealed / A1.N - A0.MendHealed / A0.N;
        bool hOk2 = pAll2.paper > 0 && incAll >= pAll2.paper / 1.5 && incAll <= pAll2.paper * 1.5;
        bool cOk = Enumerable.Range(0, mdWriters.Length).All(k => A2.SpillByWriter[k] <= A1.SpillByWriter[k] + 1e-9 && (mdDenseIds.Contains(mdWriterIds[k]) || A2.SpillByWriter[k] == 0));
        Console.WriteLine("| 検査 | 内容 | 値 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        Console.WriteLine($"| (a) | **ノノ素体のセル**（添字 1・3）が X0 / X1 / X2 で完全一致 | {cellsA} セル・最大差 {maxA:F10}pt | {(maxA == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (c) | X2 の供給が X1 の部分集合 | X2 の書き手 {string.Join("・", Enumerable.Range(0, mdWriters.Length).Where(k => A2.SpillByWriter[k] > 0).Select(k => mdName[mdWriters[k]]))} | {(cOk ? "○" : "**×**")} |");
        Console.WriteLine($"| (d) | 味方の傷の書き込み回数 > 0 | X1 {A1.CarryWoundAlly / A1.N:F2} ／ X2 {A2.CarryWoundAlly / A2.N:F2} 回/戦（X0 {A0.CarryWoundAlly / A0.N:F2}） | {(A1.CarryWoundAlly > 0 && A2.CarryWoundAlly > 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (e) | ガルドが患者になった回数が 0 | X1 {A1.GaldPatient:F0} 回（発火 {A1.MendFires:F0}） | {(A1.GaldPatient == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (f) | 1巻き込み = 1傷（X1: 味方の刃の着弾（生存）÷ 巻き込み則の書き込み = 1.00） | {A1.SpillHits} / {A1.SpillWounds} = {(A1.SpillWounds > 0 ? A1.SpillHits / A1.SpillWounds : double.NaN):F4} | {(A1.SpillHits == A1.SpillWounds ? "○" : "**×**")} |");
        Console.WriteLine($"| (h) | 紙のスループットと実測の増分が 1.5 倍以内 | 紙 {pAll2.paper:F1} ／ 実測 {incAll:F1}（比 {(pAll2.paper > 0 ? incAll / pAll2.paper : double.NaN):F2}） | {(hOk2 ? "○" : "**×**")} |");
        Console.WriteLine($"| (i) | 主判定が2系列で同符号 | X1 {MdP2(q1Ser[0])} / {MdP2(q1Ser[1])} ／ X2 {MdP2(q2Ser[0])} / {MdP2(q2Ser[1])} | {(Math.Sign(q1Ser[0]) == Math.Sign(q1Ser[1]) ? "○" : "**×**")} / {(Math.Sign(q2Ser[0]) == Math.Sign(q2Ser[1]) ? "○" : "**×**")} |");
        Console.WriteLine($"| 敵側 | 敵の従軍司祭長の繕いが味方の傷を読んだ回数（§7） | X1 敵の発火 {A1.FoeMendFires / A1.N:F2} 回/戦・傷を読んだ {A1.FoeMendSeen:F0} 回 | {(A1.FoeMendSeen == 0 ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine("## 判定表");
        Console.WriteLine();
        Console.WriteLine("| | 判定 | 値 | 線 | 結果 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1（X1）** | **6枚の Δ相乗 の平均** | **{MdP2(q1)}** | ≥ +{MdQ1Line:F1} | **{(q1 >= MdQ1Line ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1（X2）** | **同（吸い・余波だけ）** | **{MdP2(q2)}** | ≥ +{MdQ1Line:F1} | **{(q2 >= MdQ1Line ? "○" : "×")}** |");
        Console.WriteLine($"| **(A)** | **読み手の発火回数/戦** | **{A1.MendFires / A1.N:F2}** | > {MdHariFires:F2}（ハリ） | **{(A1.MendFires / A1.N > MdHariFires ? "○" : "×")}** |");
        Console.WriteLine($"| Q2 | 患者に傷があった率（X1・第2〜5波） | {(A1.MendFires > 0 ? A1.MendSeen / A1.MendFires * 100 : 0):F1}% | 記録（予測 60%） | — |");
        Console.WriteLine($"| Q3 | 味方側の供給/戦・在庫/T | X1 {A1.SpillWounds / A1.N:F2}・{A1.StockAlly / A1.N:F2} ／ X2 {A2.SpillWounds / A2.N:F2}・{A2.StockAlly / A2.N:F2} | 記録 | — |");
        Console.WriteLine($"| Q4 | ノノの寿命（第2〜5波） | X0 {A0.NonoLife / A0.N:F2}T → X1 {A1.NonoLife / A1.N:F2}T | 記録 | — |");
        Console.WriteLine($"| Q5 | 未読の傷（味方側・X1） | {(A1.CarryWoundAlly > 0 ? (A1.DeathWoundAlly + A1.EndWoundAlly) / A1.CarryWoundAlly * 100 : double.NaN):F1}% | 第85期 84% | — |");
        Console.WriteLine();
        Console.WriteLine($"所要 {mdSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check
    // =====================================================================================
    if (mdArg == "check")
    {
        Console.WriteLine("# 第86期 —— 受け入れ基準: `compare` 61 行の X0 / X1 / X2 / X1P の突き合わせと拒否権");
        Console.WriteLine();
        var c = new double[4][][];
        for (int x = 0; x < 4; x++) c[x] = new double[mdAllRows.Length][];
        Parallel.For(0, mdAllRows.Length * 4, k =>
        {
            int i = k / 4, x = k % 4;
            c[x][i] = MdCompare(mdAllRows[i].F, x);
        });
        int mismDoc = 0, cellsDoc = 0, missingDoc = 0;
        var mism = new int[4]; var mismNoNono = new int[4]; var movedRows = new HashSet<string>[4];
        for (int x = 1; x < 4; x++) movedRows[x] = new HashSet<string>();
        var lines = new List<string>();
        for (int i = 0; i < mdAllRows.Length; i++)
        {
            bool hasNono = MdHas(mdAllRows[i].F, "lili");
            if (mdBalance.TryGetValue(mdAllRows[i].Name, out double[]? doc))
                for (int wv = 0; wv < mdW; wv++) { cellsDoc++; if (Math.Abs(doc[wv] - c[0][i][wv]) > 0.05) { mismDoc++; lines.Add($"| {mdAllRows[i].Name} | 第{wv + 1}波 | docs {doc[wv]:F1} | X0 {c[0][i][wv]:F1} |"); } }
            else missingDoc++;
            for (int x = 1; x < 4; x++)
                for (int wv = 0; wv < mdW; wv++)
                    if (Math.Abs(c[0][i][wv] - c[x][i][wv]) > 0.001)
                    {
                        mism[x]++; movedRows[x].Add(mdAllRows[i].Name);
                        if (!hasNono) mismNoNono[x]++;
                        lines.Add($"| {mdAllRows[i].Name} | 第{wv + 1}波 | X0 {c[0][i][wv]:F1} | {(x < 3 ? "X" + x : "**X1P**")} {c[x][i][wv]:F1} |");
                    }
        }
        Console.WriteLine($"`CompareBuilds()` {mdAllRows.Length} 行 × {mdW} 波 × seed 0..199 を X0 / X1 / X2 / X1P で回した（{mdAllRows.Length * mdW * 200 * 4:N0} 戦）。");
        Console.WriteLine();
        Console.WriteLine("| 検査 | 内容 | 値 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        Console.WriteLine($"| 受け入れ | X0 と `docs/balance.md` の突き合わせ | {cellsDoc} セル・ずれ {mismDoc} 件（`docs` に無い行 {missingDoc}） | {(mismDoc == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| **(b)** | **巻き込み則 on ＋ ノノ Plain（X1P）が X0 と全セル ±0.0** | ずれ {mism[3]} 件 | {(mism[3] == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (d') | ノノを含まない行が X1 で全セル ±0.0 | ずれ {mismNoNono[1]} 件 | {(mismNoNono[1] == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (d') | ノノを含まない行が X2 で全セル ±0.0 | ずれ {mismNoNono[2]} 件 | {(mismNoNono[2] == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| X1 | 動いたセル／行 と 1-3 の予告 | {mism[1]} セル・{movedRows[1].Count} 行（{string.Join(" / ", movedRows[1])}） ／ 予告 {mdX1Rows.Length} 行（{string.Join(" / ", mdX1Rows.Select(r => r.Name))}） | {(movedRows[1].All(n => mdX1Rows.Any(r => r.Name == n)) ? "○（予告の外は動いていない）" : "**×**（予告の外が動いた）")} |");
        Console.WriteLine($"| X2 | 動いたセル／行 と 1-3 の予告 | {mism[2]} セル・{movedRows[2].Count} 行（{string.Join(" / ", movedRows[2])}） ／ 予告 {mdX2Rows.Length} 行（{string.Join(" / ", mdX2Rows.Select(r => r.Name))}） | {(movedRows[2].All(n => mdX2Rows.Any(r => r.Name == n)) ? "○（予告の外は動いていない）" : "**×**（予告の外が動いた）")} |");
        double Fifth(double[][] cc, IEnumerable<string> names)
        {
            var xs = new List<double>();
            foreach (string n in names) { int i = Array.FindIndex(mdAllRows, rw => rw.Name == n); if (i >= 0) xs.Add(cc[i][mdW - 1]); }
            return xs.Count == 0 ? double.NaN : xs.Average();
        }
        double f0 = Fifth(c[0], Baseline.PrimaryRows), f1 = Fifth(c[1], Baseline.PrimaryRows), f2 = Fifth(c[2], Baseline.PrimaryRows);
        int Over(double[][] cc) => Enumerable.Range(0, mdAllRows.Length).Count(i => cc[i][mdW - 1] > 95.0);
        int o0 = Over(c[0]), o1 = Over(c[1]), o2 = Over(c[2]);
        Console.WriteLine($"| 拒否権1 | 主判定 {Baseline.PrimaryRows.Length} 行の第五波平均 ≥ {Baseline.PrimaryFifthFloor:F1}% | X0 {f0:F2}% → X1 {f1:F2}% → X2 {f2:F2}% | {(f1 >= Baseline.PrimaryFifthFloor && f2 >= Baseline.PrimaryFifthFloor ? "立たない" : "**立つ**")} |");
        Console.WriteLine($"| 拒否権2 | `compare` {mdAllRows.Length} 行で第五波 > 95% が新たに 2 行以上 | X0 {o0} → X1 {o1} → X2 {o2} 行 | {(o1 - o0 >= 2 || o2 - o0 >= 2 ? "**立つ**" : "立たない")} |");
        var v3 = new List<string>[3];
        for (int x = 1; x < 3; x++)
        {
            v3[x] = new List<string>();
            foreach (string n in Baseline.PrimaryRows)
            {
                int i = Array.FindIndex(mdAllRows, rw => rw.Name == n);
                if (i < 0) continue;
                for (int wv = 0; wv < mdW; wv++)
                    if (c[x][i][wv] - c[0][i][wv] <= -10.0) v3[x].Add($"{n} 第{wv + 1}波 {c[0][i][wv]:F1} → {c[x][i][wv]:F1}");
            }
        }
        Console.WriteLine($"| 拒否権3 | 主判定19行が、いずれかの波で −10.0pt 以上落ちる | X1 {v3[1].Count} 件{(v3[1].Count > 0 ? "（" + string.Join(" / ", v3[1]) + "）" : "")} ／ X2 {v3[2].Count} 件{(v3[2].Count > 0 ? "（" + string.Join(" / ", v3[2]) + "）" : "")} | {(v3[1].Count > 0 || v3[2].Count > 0 ? "**立つ**" : "立たない")} |");
        if (lines.Count > 0)
        {
            Console.WriteLine(); Console.WriteLine("| 行 | 波 | 左 | 右 |"); Console.WriteLine("|---|---|--:|--:|");
            foreach (string l in lines) Console.WriteLine(l);
        }
        Console.WriteLine();
        Console.WriteLine($"| 行（ノノを含む {mdNonoRows.Length} 行） | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var rw in mdNonoRows)
        {
            int i = Array.FindIndex(mdAllRows, xx => xx.Name == rw.Name);
            for (int x = 0; x < 3; x++)
                Console.WriteLine($"| {rw.Name}{(mdPrimary.Contains(rw.Name) ? "［主判定］" : "")} | X{x} | " + string.Join(" | ", c[x][i].Select(v => $"{v:F1}%")) + $" | {c[x][i].Average():F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {mdSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("mender: 引数は phase0 / run <x> [skip] [take] / tables <x0> <x1> <x2> / check。");
    return;
}
}
