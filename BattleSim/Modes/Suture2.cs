using BattleCore;
using static Common;

// =====================================================================================
// suture2 モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "suture2")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 suture2
// =====================================================================================

static class Suture2Diag
{
// 糸を味方にも通す（第85期）——縫いのハリの両側読み × 巻き込み則。
// 器具は第81期の 2×2（第84期 `thorn` の写し）で、A ＝ ハリ固定・B ＝ 残り 50 体。版は3つ:
//   W0（対照）  SutureSide.Foe  / SpillWound 無効 / ThornWound.None   ＝ 現行
//   W1          SutureSide.Both / SpillWound 無効 / ThornWound.Both   ＝ 味方に傷を書くのはカドだけ
//   W2          SutureSide.Both / SpillWound 有効 / ThornWound.Foe    ＝ 味方に傷を書くのが6枚（巻き込み則がカドの巻き込みも拾う）
// **既存の診断（thorn / pairs2 / breadth / checkup / wound / suture）は1文字も書き換えていない。**
//
//     dotnet run --project BattleSim -c Release 0 suture2 phase0                  # §1（紙のスループット・窓口の全数・交わり。戦闘は分母のぶんだけ W0 で回す）
//     dotnet run --project BattleSim -c Release 0 suture2 run <w> [skip] [take]   # 2×2（w = 0/1/2）。50 組 × 128 台・TSV を標準出力へ
//     dotnet run --project BattleSim -c Release 0 suture2 tables <w0> <w1> <w2>   # 表A〜F・Q1〜Q3・Q5・自己検査
//     dotnet run --project BattleSim -c Release 0 suture2 nata                    # Q4 の専用台（カド × ハリ × ナタ）
//     dotnet run --project BattleSim -c Release 0 suture2 check                   # `compare` 61 行の W0/W1/W2・拒否権1・2・(e)
public static void Run(string[] args, int stageIndex)
{
    string suArg = args.Length > 2 ? args[2] : "";
    var suSw = System.Diagnostics.Stopwatch.StartNew();
    var suInv = System.Globalization.CultureInfo.InvariantCulture;

    IReadOnlyList<EnemyCatalog.Stage> suStages = EnemyCatalog.Stages;
    int suW = suStages.Count;
    // **第141期: ロスターは `Everyone`（`All ∪ Retired`）。** A ＝ ハリは第108期に `All` から外れているので、
    // `All` のままでは `suIdx["hari"]` で落ちる（第109期以降 32 期ぶん）。ロスターが B の集合と引き表を兼ねるので
    // ロスターごと広げる（`thorn` と同じ理由）。第85期の測定当時は 51 枚すべてが `All` に居た。
    var suRoster = UnitCatalog.Everyone.ToArray();
    int suRN = suRoster.Length;                       // 54（第85期は 51）

    // ---- 第81期 `pairs2` の定数の写し（**1つも変えていない**）----------------------------------
    const int SuTableSeed = 8_100_000;
    const int SuK = 64;                               // 1組・1系列あたりの台数
    const int SuS = 2;                                // 独立系列の本数
    const int SuBand = 0, SuM = 8;                    // 戦闘 seed 0..7
    const int SuStrong = 7, SuWeakPct = 60, SuDrawCap = 20000;
    const int SuTop = 20;
    const double SuQ1Line = 3.0;                      // 主判定の線（§3-1。第84期と同じ。測る前に固定）
    const double SuPaperFloor = 5.0;                  // 紙の停止条件: 回復/戦 が総被ダメ/戦 の 5% 未満なら実装しない（§1-1）
    const int SuProbeTables = 16;                     // 表A・D・E を verbose で測る台数（系列から 8 ずつ）

    var suIdx = new Dictionary<string, int>();
    for (int u = 0; u < suRN; u++) suIdx[suRoster[u].Id] = u;
    string[] suName = suRoster.Select(d => d.Name).ToArray();
    int suHari = suIdx["hari"], suKado = suIdx["kado"], suNata = suIdx["nata"];
    string[] suWriterIds = { "kado", "borg", "rica", "golm", "zoto", "nara" };   // 味方に傷を書く6枚（W2）
    int[] suWriters = suWriterIds.Select(i => suIdx[i]).ToArray();
    string[] suFoeReaderIds = { "egu", "nomi", "nata" };                        // 敵の傷を読む駒（ハリ以外）。自己検査 (b) の限定に使う
    int[] suOthers = Enumerable.Range(0, suRN).Where(u => u != suHari).ToArray();   // 50 体

    (SutureRule Suture, SpillWoundRule Spill, ThornRule Thorn) SuRulesOf(int w) => w switch
    {
        0 => (new SutureRule(SutureSide.Foe), new SpillWoundRule(false), new ThornRule(ThornWound.None)),
        1 => (new SutureRule(SutureSide.Both), new SpillWoundRule(false), new ThornRule(ThornWound.Both)),
        _ => (new SutureRule(SutureSide.Both), new SpillWoundRule(true), new ThornRule(ThornWound.Foe)),
    };
    string SuVName(int w) => w switch { 0 => "W0（現行）", 1 => "W1（両側・カドだけ）", _ => "W2（両側・巻き込み則）" };
    BattleResult SuRun(Formation f, Formation e, int seed, bool verbose, int w)
    {
        var r = SuRulesOf(w);
        return BattleEngine.Run(f, e, seed, verbose: verbose, thorn: r.Thorn, suture: r.Suture, spillWound: r.Spill);
    }

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜84期と同一。`Stages` は書き換えない）----------------------
    var suWeakCache = new Dictionary<string, UnitDef>();
    UnitDef SuWeakOf(UnitDef d)
    {
        if (suWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * SuWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        suWeakCache[d.Id] = w;
        return w;
    }
    var suWeak = suStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = SuWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef SuPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var suPlainMap = suRoster.ToDictionary(d => d.Id, SuPlain);

    UnitDef[] SuFill(UnitDef[] pool, int strong0, int seed, int count)
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
            if (sel.Attack >= SuStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int SuSeed(int pairIx, int draw)
    {
        ulong x = (ulong)SuTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] SuSeats(UnitDef[] u)
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
    // 版 v: bit0 = A（添字0＝ハリ）を素体に / bit1 = B（添字1）を素体に
    Formation SuForm(UnitDef[] u, int[] seats, int v)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (v & 1) != 0) || (k == 1 && (v & 2) != 0);
            f[seats[k]] = plain ? suPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double SuRate(Formation f, int w)
    {
        double sum = 0;
        for (int wi = 1; wi < suW; wi++)
        {
            int wins = 0;
            for (int seed = SuBand; seed < SuBand + SuM; seed++)
                if (SuRun(f, suWeak[wi].Enemy, seed, false, w).PlayerWon) wins++;
            sum += wins * 100.0 / SuM;
        }
        return sum / (suW - 1);
    }
    string SuP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double SuSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double SuSe(IReadOnlyList<double> xs) => xs.Count < 2 ? double.NaN : SuSd(xs) / Math.Sqrt(xs.Count);

    var suPairIxOf = new int[suRN, suRN];
    {
        int pi = 0;
        for (int a = 0; a < suRN; a++) for (int b = a + 1; b < suRN; b++) { suPairIxOf[a, b] = suPairIxOf[b, a] = pi; pi++; }
    }
    // 台（埋め草3枚）を 2K 通り集める。引いた順 t の系列は t % SuS
    List<UnitDef[]> SuFills(int b)
    {
        int a = suHari;
        var pool = suRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (suRoster[a].Attack >= SuStrong ? 1 : 0) + (suRoster[b].Attack >= SuStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < SuS * SuK && draw < SuDrawCap; draw++)
        {
            var f = SuFill(pool, strong0, SuSeed(suPairIxOf[a, b], draw), 3);
            var t = f.Select(d => suIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }
    UnitDef[] SuTeam(int b, UnitDef[] fill) => new[] { suRoster[suHari], suRoster[b], fill[0], fill[1], fill[2] };
    // 1組ぶんの 2×2。Y[t][v]: v0 = y11 / v1 = y01（ハリ素体）/ v2 = y10（B 素体）/ v3 = y00
    double[][] SuMeasure(int b, int w)
    {
        var fills = SuFills(b);
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = SuTeam(b, fills[t]);
            int[] seats = SuSeats(team);
            ys[t] = new double[4];
            for (int v = 0; v < 4; v++) ys[t][v] = SuRate(SuForm(team, seats, v), w);
        }
        return ys;
    }
    // 台の中身の検査（自己検査 (b)(c) の限定に使う）
    bool SuHasId(UnitDef[] team, string id) => team.Any(d => d.Id == id);

    // ---- verbose の1戦から供給・在庫・糸口・読み手の出力を読む（**盤面は動かさない**。Events・Log・Tally を読むだけ）----
    void SuProbe(Formation f, Formation enemy, int seed, int w, SuStat acc)
    {
        var r = SuRun(f, enemy, seed, true, w);
        var pNames = f.Occupied().Select(o => o.Def.Name).ToHashSet();
        var team = new Dictionary<int, int>();
        {
            int i = 0;
            foreach ((int sl, UnitDef d) in f.Occupied()) { team[i] = BattleContext.PlayerTeam; i++; }
            foreach ((int sl, UnitDef d) in enemy.Occupied()) { team[i] = BattleContext.EnemyTeam; i++; }
        }
        // Events: 在庫（ターン頭の StatusSnapshot「傷」を陣営別に合計）・味方の刃の着弾回数（自己検査 (f)）
        {
            double stA = 0, stF = 0, mxA = 0, mxF = 0; int turns = 0, allyTurns = 0; int curTurn = -1; double curA = 0, curF = 0;
            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind == BattleEventKind.Summon && e.TargetId is int sid && e.Team is int tm) team[sid] = tm;
                else if (e.Kind == BattleEventKind.TurnStart)
                {
                    if (curTurn >= 0) { stA += curA; stF += curF; turns++; if (curA > 0) allyTurns++; mxA = Math.Max(mxA, curA); mxF = Math.Max(mxF, curF); }
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
            if (curTurn >= 0) { stA += curA; stF += curF; turns++; if (curA > 0) allyTurns++; mxA = Math.Max(mxA, curA); mxF = Math.Max(mxF, curF); }
            acc.StockAlly += turns > 0 ? stA / turns : 0; acc.StockFoe += turns > 0 ? stF / turns : 0;
            acc.StockAllyMax += mxA; acc.StockFoeMax += mxF; acc.AllyWoundTurns += allyTurns;
        }
        // Log: 棘の発火・棘の傷（味方／敵）・巻き込みの傷（書き手別）・断ち
        foreach (LogLine ll in r.Log)
        {
            string sx = ll.Text;
            if (sx.Contains(" の棘が ") && sx.Contains(" を刺し返す")) acc.KadoFires++;
            else if (sx.Contains(" の棘が ") && sx.Contains(" に残る（傷 "))
            {
                bool ally = pNames.Any(n => sx.Contains($" の棘が {n} に残る"));
                if (ally) acc.ThornWoundAlly++; else acc.ThornWoundFoe++;
            }
            else if (sx.Contains("巻き込みの傷: "))
            {
                acc.SpillWounds++;
                for (int k = 0; k < suWriterIds.Length; k++)
                    if (sx.Contains($"巻き込みの傷: {suName[suWriters[k]]} の刃が ")) { acc.SpillByWriter[k]++; break; }
            }
            else if (sx.Contains(" の傷をまとめて断つ（傷 "))
            {
                acc.Sever++;
                int p = sx.IndexOf("（傷 ", StringComparison.Ordinal) + 3;
                int q = sx.IndexOf(' ', p);
                if (q > p && int.TryParse(sx.AsSpan(p, q - p), out int wd)) { acc.SeverWounds += wd; if (wd >= SeverTrait.Threshold) acc.SeverReached++; }
            }
        }
        // Tally: 糸口・回復・被ダメ・傷の在庫の出入り
        foreach ((int sl, UnitDef d) in f.Occupied())
        {
            if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? pt)) continue;
            acc.TeamHealed += pt.Healed; acc.TeamTaken += pt.DamageTaken;
            acc.CarryWoundAlly += pt.CarryCount is null ? 0 : pt.CarryCount[UnitTally.CarryWound];
            acc.DeathWoundAlly += pt.WoundsAtDeath; acc.EndWoundAlly += pt.WoundsAtEnd;
            if (d.Id == "hari")
            {
                acc.SutureFoe += pt.SutureFoe; acc.SutureAlly += pt.SutureAlly; acc.SutureDry += pt.SutureDry;
                acc.SutureHealed += pt.SutureHealed; acc.HariAttacks += pt.Attacks;
                if (pt.Deaths > 0) acc.HariDeaths++;
            }
            if (d.Id == "kado")
            {
                acc.KadoSpill += pt.SpillWoundsWritten;
                if (pt.Deaths > 0) acc.KadoDeaths++;
            }
        }
        foreach ((int sl, UnitDef d) in enemy.Occupied())
            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? et))
            {
                acc.CarryWoundFoe += et.CarryCount is null ? 0 : et.CarryCount[UnitTally.CarryWound];
                acc.DeathWoundFoe += et.WoundsAtDeath; acc.EndWoundFoe += et.WoundsAtEnd;
            }
        if (r.PlayerWon) acc.Wins++;
        acc.Turns += r.Turns;
        acc.N++;
    }
    // 組 (ハリ, b) の 2×2 の台のうち各系列の先頭 SuProbeTables/2 台の y11 を、弱い波 × seed で verbose に回す
    SuStat[] SuProbePair(int b, int w, Func<UnitDef[], bool>? filter = null)
    {
        var fills = SuFills(b);
        var loc = new SuStat[suW];
        for (int wv = 0; wv < suW; wv++) loc[wv] = new SuStat();
        int taken = 0;
        for (int t = 0; t < fills.Count && taken < SuProbeTables; t++)
        {
            var team = SuTeam(b, fills[t]);
            if (filter is not null && !filter(team)) continue;
            taken++;
            int[] seats = SuSeats(team);
            Formation f = SuForm(team, seats, 0);
            // カドの隣接数（開戦時。規則配置 H の席から数える）
            int kadoK = Array.FindIndex(team, d => d.Id == "kado");
            if (kadoK >= 0)
                for (int k = 0; k < 5; k++)
                    if (k != kadoK && FormationRules.AreAdjacent(seats[kadoK], seats[k])) for (int wv = 1; wv < suW; wv++) loc[wv].KadoAdjacent += SuM;
            for (int wv = 1; wv < suW; wv++)
                for (int seed = SuBand; seed < SuBand + SuM; seed++) SuProbe(f, suWeak[wv].Enemy, seed, w, loc[wv]);
        }
        return loc;
    }
    SuStat SuSum(SuStat[] xs, int from = 1) { var s = new SuStat(); for (int wv = from; wv < xs.Length; wv++) s.AddFrom(xs[wv]); return s; }

    var suAllRows = CompareBuilds();
    bool SuHas(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    var suHariRows = suAllRows.Where(rw => SuHas(rw.F, "hari")).ToArray();
    var suKadoRows = suAllRows.Where(rw => SuHas(rw.F, "kado")).ToArray();
    string[] suWoundIds = { "kiri", "nomi", "egu", "nata", "hari" };
    var suWoundRows = suAllRows.Where(rw => suWoundIds.Any(id => SuHas(rw.F, id))).ToArray();
    var suHariKadoRows = suHariRows.Where(rw => SuHas(rw.F, "kado")).ToArray();
    var suW2Rows = suHariRows.Where(rw => suWriterIds.Any(id => SuHas(rw.F, id))).ToArray();     // W2 で動くはずの行

    double[] SuCompare(Formation f, int w)
    {
        var v = new double[suW];
        for (int wv = 0; wv < suW; wv++)
        {
            int wins = 0;
            for (int seed = 0; seed < 200; seed++)
                if (SuRun(f, suStages[wv].Enemy, seed, false, w).PlayerWon) wins++;
            v[wv] = wins * 100.0 / 200;
        }
        return v;
    }
    var suBalance = new Dictionary<string, double[]>();
    if (File.Exists("docs/balance.md"))
        foreach (string line in File.ReadAllLines("docs/balance.md"))
        {
            if (!line.StartsWith("| ")) continue;
            var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
            if (cells.Length != suW + 1 || !cells[1].EndsWith("%")) continue;
            var v = new double[suW];
            bool ok = true;
            for (int wv = 0; wv < suW; wv++)
                if (!double.TryParse(cells[wv + 1].TrimEnd('%'), out v[wv])) { ok = false; break; }
            if (ok) suBalance[cells[0]] = v;
        }

    // ---- 紙のスループット（§1-1）。W0 の実測から分子を引き、W1 を回さずに回復/戦 を出す ----------------------
    // 供給/戦 = カドの反撃/戦 × カドの隣接数 ／ 読める回数 = min(ハリの振り/戦, 供給/戦) ／ 回復 = 読める回数 × 深さ × PerWound
    // 深さは 1（下限）と「供給された傷が全部読まれる」（上限 = 供給 × PerWound）の2点で挟む。
    (double fires, double adj, double supply, double swings, double readable, double lo, double hi, double taken, double hariHeal, double teamHeal, double turns)
        SuPaper(SuStat s)
    {
        double fires = s.KadoFires / s.N, adj = s.KadoAdjacent / s.N, supply = fires * adj;
        double swings = s.HariAttacks / s.N, readable = Math.Min(swings, supply);
        return (fires, adj, supply, swings, readable, readable * SutureTrait.PerWound, supply * SutureTrait.PerWound,
                s.TeamTaken / s.N, s.SutureHealed / s.N, s.TeamHealed / s.N, s.Turns / s.N);
    }

    // =====================================================================================
    // phase0
    // =====================================================================================
    if (suArg == "phase0")
    {
        Console.WriteLine("# 第85期 Phase 0 —— 糸を味方にも通す前の地図");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 suture2 phase0`。**規則は既定（W0）のまま**——1-1 の分母（総被ダメ・現行の回復）のぶんだけ"
                          + " W0 で戦闘を回し、W1・W2 は1戦も回さない。");
        Console.WriteLine();

        // ---- 1-1 紙のスループット --------------------------------------------------------------
        Console.WriteLine("## 1-1. 紙のスループット（**第84期の穴。ここで落ちたら実装しない**）");
        Console.WriteLine();
        Console.WriteLine($"組 (ハリ, カド) の 2×2 の台のうち各系列の先頭 {SuProbeTables / SuS} 台 = {SuProbeTables} 台の y11（両方が本物）を、"
                          + $"弱い波の第2〜5波 × seed {SuBand}..{SuBand + SuM - 1} で **W0（現行）** を verbose に回し、分子（カドの反撃/戦・カドの隣接数・ハリの振り/戦）と"
                          + "分母（総被ダメ/戦・ハリの現行の回復/戦・味方全体の回復/戦）を取った。");
        Console.WriteLine();
        Console.WriteLine("    味方側の供給/戦     = カドの反撃/戦 × カドの隣接数（開戦時の席から）");
        Console.WriteLine("    ハリが読める回数/戦 = min(ハリの振り/戦, 供給/戦)        ← 「味方側に傷がある回数」を供給の数で押さえる");
        Console.WriteLine($"    回復/戦（下限）     = 読める回数 × 1（深さ）× {SutureTrait.PerWound}");
        Console.WriteLine($"    回復/戦（上限）     = 供給/戦 × {SutureTrait.PerWound}                ← 供給された傷が全部読まれる");
        Console.WriteLine();
        var p0 = SuProbePair(suKado, 0);
        Console.WriteLine("| 波 | 反撃/戦 | 隣接 | **供給/戦** | ハリ振/戦 | 読める/戦 | **回復 下限** | 回復 上限 | 総被ダメ/戦 | 下限/被ダメ | 上限/被ダメ | ハリ現行の回復/戦 | 味方全体の回復/戦 | 決着T | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv < suW; wv++)
        {
            var sp = p0[wv]; var pp = SuPaper(sp);
            Console.WriteLine($"| 第{wv + 1}波 | {pp.fires:F2} | {pp.adj:F2} | **{pp.supply:F2}** | {pp.swings:F2} | {pp.readable:F2} | **{pp.lo:F1}** | {pp.hi:F1} | {pp.taken:F1} | {pp.lo / pp.taken * 100:F1}% | {pp.hi / pp.taken * 100:F1}% | {pp.hariHeal:F2} | {pp.teamHeal:F1} | {pp.turns:F1} | {sp.Wins * 100.0 / sp.N:F1}% |");
        }
        var pAll = SuPaper(SuSum(p0));
        Console.WriteLine($"| **第2〜5波** | {pAll.fires:F2} | {pAll.adj:F2} | **{pAll.supply:F2}** | {pAll.swings:F2} | {pAll.readable:F2} | **{pAll.lo:F1}** | {pAll.hi:F1} | {pAll.taken:F1} | **{pAll.lo / pAll.taken * 100:F1}%** | {pAll.hi / pAll.taken * 100:F1}% | {pAll.hariHeal:F2} | {pAll.teamHeal:F1} | {pAll.turns:F1} | {SuSum(p0).Wins * 100.0 / SuSum(p0).N:F1}% |");
        Console.WriteLine();
        // ノノの回復/戦（比較の分母）: `耐久 (ガルド×ノノ)` の味方全体の Healed（弱い波・同じ seed 帯）
        {
            var nonoRow = suAllRows.FirstOrDefault(rw => rw.Name == "耐久 (ガルド×リリ)");
            if (nonoRow.F is not null)
            {
                double healed = 0, n = 0, taken = 0;
                for (int wv = 1; wv < suW; wv++)
                    for (int seed = SuBand; seed < SuBand + SuM; seed++)
                    {
                        var r = SuRun(nonoRow.F, suWeak[wv].Enemy, seed, false, 0);
                        foreach ((int sl, UnitDef d) in nonoRow.F.Occupied())
                            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) { healed += t.Healed; taken += t.DamageTaken; }
                        n++;
                    }
                Console.WriteLine($"比較の分母（ノノ）: `耐久 (ガルド×ノノ)` を同じ弱い波 × seed で回した**味方全体の回復/戦 {healed / n:F1}**（総被ダメ/戦 {taken / n:F1}・{healed / taken * 100:F1}%）。"
                                  + $"ノノの継ぎ当ては `MenderTrait.Amount` = {MenderTrait.Amount}／T（自分の HP を同じだけ払う）。");
                Console.WriteLine();
            }
        }
        bool paperPass = pAll.lo / pAll.taken * 100 >= SuPaperFloor;
        Console.WriteLine($"> **停止条件（§1-1）: 紙の回復/戦（下限）{pAll.lo:F1} は 総被ダメ/戦 {pAll.taken:F1} の {pAll.lo / pAll.taken * 100:F1}%** → 線 {SuPaperFloor:F0}% に対して **{(paperPass ? "届く。実装して測る" : "届かない。実装しない（§6 の分岐へ）")}**。");
        Console.WriteLine($"> 第84期の敵側（傷1つ = 3 点が反撃の打点 22〜45 の 7〜14%）と比べる: 味方側の巻き込みは攻 {suRoster[suKado].Attack} × 50% = {Math.Max(1, suRoster[suKado].Attack * ThornsTrait.FriendlySplashPercent / 100)} 点"
                          + $"（惨禍で {Math.Max(1, suRoster[suKado].Attack * ThornsTrait.FriendlySplashPercent / 100) * 3 / 2}）で、傷1つ = 3 点はその **{3.0 / Math.Max(1, suRoster[suKado].Attack * ThornsTrait.FriendlySplashPercent / 100) * 100:F0}%**。");
        Console.WriteLine($"> 第84期の実測との照合: 第四波のカドの反撃 3.67 回/戦（第84期・組 (カド, 傷の5枚) 平均）に対し、この台（組 (ハリ, カド)）の第四波は {p0[3].KadoFires / p0[3].N:F2} 回/戦。"
                          + $" 第84期 V2 の「味方傷 6.00 回/戦」は供給の上限そのものと読める（この台の供給 {pAll.supply:F2}）。");
        Console.WriteLine();

        // ---- 1-2-1 isFriendlyFire の呼び出し口の全数 ------------------------------------------------
        Console.WriteLine("## 1-2-1. `isFriendlyFire: true` の呼び出し口の全数（call-site grep を実行時に再現）");
        Console.WriteLine();
        Console.WriteLine("`grep -rn \"isFriendlyFire\" --include=*.cs BattleCore/` の**呼び出し行**（定義・doc・計数の行を除く）。特性はその行を囲むクラスで、"
                          + "周期は特性の doc から、W2 の対象は `source` が同陣営か（巻き込み則の1条件）で機械的に決まる。");
        Console.WriteLine();
        Console.WriteLine("| ファイル:行 | 特性（駒） | 周期 | `source` | W2 の対象 | 行 |");
        Console.WriteLine("|---|---|---|---|:-:|---|");
        var ffMap = new Dictionary<string, (string who, string period, string src, bool w2)>
        {
            ["SplashTrait"] = ("余波（ボルグ）", "攻撃ごと（隣接の味方）", "自分（同陣営）", true),
            ["SacrificeTrait"] = ("生贄（リィカ）", "開戦時1回（隣接の味方・`lethal: false`）", "自分（同陣営）", true),
            ["DrainTrait"] = ("吸い（ゴルム）", "毎ターン（味方全員）", "自分（同陣営）", true),
            ["BomberTrait"] = ("破裂の味方巻き込み（ゾト）", "死亡時（味方全員）", "自分（同陣営）", true),
            ["ThornsTrait"] = ("棘の巻き込み（カド）", "被弾ごと（隣接の味方）", "自分（同陣営）", true),
            ["PursuerTrait"] = ("深追いの反動（ハギ）", "追い打ちの連鎖ごと（自分）", "**null**", false),
            ["ForsakeTrait"] = ("置き去りの削り（ナラ）", "毎ターン（自分より遅い味方・`lethal: true`）", "自分（同陣営）", true),
        };
        int ffN = 0, ffW2 = 0;
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
                string who, period, src; bool w2;
                if (file.EndsWith("BattleEngine.cs"))
                {
                    if (t.Contains("wall,")) { who = "巨躯の中継（engine）"; period = "被弾ごと（肩代わり）"; src = "**元の攻撃者**（味方の刃なら同陣営）"; w2 = false; }
                    else if (t.Contains("sharer,")) { who = "分かちの中継（engine）"; period = "被弾ごと（肩代わり）"; src = "**元の攻撃者**（味方の刃なら同陣営）"; w2 = false; }
                    else if (t.Contains("relayer,")) { who = "転嫁の代金（engine・ワタ）"; period = "横取りごと（自分の HP）"; src = "**null**"; w2 = false; }
                    else { who = "（未分類）"; period = "—"; src = "—"; w2 = false; }
                }
                else if (ffMap.TryGetValue(cls, out var e)) { who = e.who; period = e.period; src = e.src; w2 = e.w2; }
                else { who = $"（未分類 {cls}）"; period = "—"; src = "—"; w2 = false; }
                if (w2) ffW2++;
                Console.WriteLine($"| `{file}:{i + 1}` | {who} | {period} | {src} | {(w2 ? "**○**" : "×")} | `{t.Replace("|", "\\|")}` |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"**呼び出し口 {ffN}・W2 の書き手 {ffW2}**（指示書の「6箇所」は特性側の刃の数で正しいが、**呼び出し口の全数は {ffN}**——"
                          + "深追いの反動（ハギ・`source` null）と engine の中継3本が加わる）。");
        Console.WriteLine("**中継は外す。** 根拠: 中継は肩代わりであって刃ではない。転嫁の代金と深追いの反動は `source` が `null` なので「`isFriendlyFire` かつ `source` が同陣営」で外れるが、"
                          + "**巨躯・分かちの中継は元の刃が味方（棘の巻き込み・吸い）だと `source` も同陣営になる**ので、中継の2段に札（`relayed`）を付けて外す"
                          + "（最初の実装は札を持たず、カドの巻き込みが巨躯に中継された段を二重に数えて自己検査 (c) が 2962 対 3104 で落ちた）。");
        Console.WriteLine();

        // ---- 1-2-2 StatusKeys.Wound の窓口 ------------------------------------------------------
        Console.WriteLine("## 1-2-2. `StatusKeys.Wound` を読む箇所の全数（第84期 Phase 0-1 の再確認）");
        Console.WriteLine();
        Console.WriteLine("| ファイル:行 | 種別 | 対象 | 行 |");
        Console.WriteLine("|---|:-:|:-:|---|");
        int nWrite = 0, nRead = 0, nReadAlly = 0, nReadAlly85 = 0, nOther = 0;
        foreach (string file in new[] { "BattleCore/Models.cs", "BattleCore/Traits.cs", "BattleCore/BattleEngine.cs" })
        {
            if (!File.Exists(file)) continue;
            string[] ls = File.ReadAllLines(file);
            bool inSuture = false;
            for (int i = 0; i < ls.Length; i++)
            {
                if (ls[i].Contains("class SutureTrait")) inSuture = true;
                else if (inSuture && System.Text.RegularExpressions.Regex.IsMatch(ls[i], @"^public sealed class ")) inSuture = false;
                string sx = ls[i];
                if (!sx.Contains("StatusKeys.Wound")) continue;
                string t = sx.Trim();
                string kind, who;
                if (t.StartsWith("///") || t.StartsWith("//")) { kind = "doc"; who = "—"; nOther++; }
                else if (t.Contains("SetCounter(StatusKeys.Wound"))
                {
                    kind = "**書き**"; nWrite++;
                    who = t.Contains("ally.") ? "**味方**" : t.Contains("donor.") ? "**糸口（敵か味方）**" : t.Contains("target.SetCounter") && file.EndsWith("BattleEngine.cs") ? "**味方（巻き込み則・W2）**" : "敵";
                }
                else if (t.Contains("Counter(StatusKeys.Wound)"))
                {
                    kind = "読み"; nRead++;
                    bool ally = t.Contains("self.") || t.Contains("ally.") || t.Contains("a.Counter") || t.Contains("a =>") || t.Contains("dead.") || t.Contains("u.Counter");
                    bool tally = t.Contains("WoundsAt");
                    who = tally ? "計数（両陣営・第85期）" : ally ? (inSuture ? "**味方（第85期・両側読み）**" : "**味方**") : "敵";
                    if (ally && !tally) { nReadAlly++; if (inSuture) nReadAlly85++; }
                }
                else { kind = "その他"; who = "—"; nOther++; }
                Console.WriteLine($"| `{file}:{i + 1}` | {kind} | {who} | `{t.Replace("|", "\\|")}` |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"**書き {nWrite} 箇所・読み {nRead} 箇所（うち味方を読む箇所 {nReadAlly}・**そのうち第85期の両側読みが {nReadAlly85}**・計数を除く）・doc／その他 {nOther}。**");
        Console.WriteLine($"**第85期の追加を除くと味方の傷を読む窓口は {nReadAlly - nReadAlly85} 本**——第84期 Phase 0-1・第68期の表（傷は 305 セル全部が不発）・第49期の全数確認と整合する。"
                          + "`HandleDeath` / `Run` 終端の `Counter(StatusKeys.Wound)` は自己検査 (j) の計数で、誰も読んで分岐しない。");
        Console.WriteLine();

        // ---- 1-2-3 compare の行 -----------------------------------------------------------------
        Console.WriteLine("## 1-2-3. `compare` でハリ・カド・傷を含む行（数え直し）");
        Console.WriteLine();
        Console.WriteLine($"ハリを含む行 **{suHariRows.Length}**: " + string.Join(" / ", suHariRows.Select(rw => $"`{rw.Name}`")));
        Console.WriteLine();
        Console.WriteLine($"カドを含む行 **{suKadoRows.Length}**: " + string.Join(" / ", suKadoRows.Select(rw => $"`{rw.Name}`")));
        Console.WriteLine();
        Console.WriteLine($"傷の駒（キリ・ノミ・エグ・ナタ・ハリ）を含む行 **{suWoundRows.Length}**: " + string.Join(" / ", suWoundRows.Select(rw => $"`{rw.Name}`")));
        Console.WriteLine();
        Console.WriteLine($"**ハリ ∩ カド = {suHariKadoRows.Length} 行**{(suHariKadoRows.Length == 0 ? "。**W1 では §3 の拒否権は原理的に立たない**（`compare` にカドとハリが同席する行が無い。ただし W1 の `ThornWound.Both` は敵にも傷を書くので、カドと敵側の読み手が同席する行があれば動く——第84期の交わり 0 行のとおり無い）。" : ": " + string.Join(" / ", suHariKadoRows.Select(rw => $"`{rw.Name}`")))}");
        Console.WriteLine();
        Console.WriteLine($"**W2 で動くはずの行（ハリ ∩ 書き手6枚のいずれか）= {suW2Rows.Length} 行**: "
                          + string.Join(" / ", suW2Rows.Select(rw => $"`{rw.Name}`（{string.Join("・", suWriterIds.Where(id => SuHas(rw.F, id)).Select(id => suName[suIdx[id]]))}）"))
                          + "。**`suture2 check` の実測と突き合わせる**——この行以外が W2 で1セルでも動いたら、巻き込み則が刃以外を拾っている。");
        Console.WriteLine();

        // ---- 1-2-4 渇き -------------------------------------------------------------------------
        Console.WriteLine("## 1-2-4. 渇き（第三波）の扱い");
        Console.WriteLine();
        Console.WriteLine("ハリの繕いは `ctx.Heal` を通るので第三波では封じられる。**塞ぎは走る**（第39期の仕様）。両側読みでは**味方の傷だけが目減りする**"
                          + "（敵側の糸口は敵の傷、味方側の糸口は味方の傷を塞ぐ——どちらから引いたかで塞がる側が決まる）。"
                          + "仕様として維持し、`tables` の表E で「繕いは 0 だが塞ぎは走った」回数（`UnitTally.SutureDry`）を第三波で単独に出す。");
        Console.WriteLine();

        // ---- 1-2-5 過去の類似機構 ---------------------------------------------------------------
        Console.WriteLine("## 1-2-5. 過去に同じ機構を測っていないか");
        Console.WriteLine();
        var hits = new List<string>();
        if (Directory.Exists("design"))
            foreach (string fp in Directory.GetFiles("design", "*.md").OrderBy(x => x, StringComparer.Ordinal))
            {
                if (Path.GetFileName(fp).StartsWith("PHASE85")) continue;
                foreach (string sx in File.ReadAllLines(fp))
                    if (System.Text.RegularExpressions.Regex.IsMatch(sx, "両側|味方の傷|isFriendlyFire.*傷"))
                    { hits.Add($"`{fp.Replace('\\', '/')}`"); break; }
            }
        Console.WriteLine($"`grep -rln \"両側\\|味方の傷\\|isFriendlyFire.*傷\" design/` に当たるファイル {hits.Count}: " + string.Join(" / ", hits));
        Console.WriteLine();
        Console.WriteLine("**測った期は無い。** 当たるのは (1) 第84期の V2（味方にも傷を書いたが読み手 0 で 1 セルも動かなかった＝この期の供給側の陰性対照）、"
                          + "(2) 第49・68期の「傷は味方に載る経路が1つも無い」の全数確認、(3) 「両側」が別の意味（器具の床と天井・波の両側）で出る報告書。");
        Console.WriteLine();

        Console.WriteLine("## 予測（測る前に書いた。§1 の写し）");
        Console.WriteLine();
        Console.WriteLine("- W1 の Δ相乗(カド, ハリ) が正で最大。現行の相乗は +8.0 / +7.8（第80期）。");
        Console.WriteLine("- W1 で ハリの単独の帰属は動かない。カド不在では味方に傷が1つも載らないので、y01（カド素体）のセルは W0 と完全一致するはず（自己検査 (a)）。");
        Console.WriteLine("- W2 − W1 で最も効くのはゴルム（毎ターン全味方から吸う＝供給が最も密）。次がボルグ（攻撃ごと）。リィカとゾトはほぼ効かない（開戦1回／死亡時）。");
        Console.WriteLine("- 第三波（渇き）だけは W1・W2 とも W0 を下回りうる。この期に味方の傷の2枚目の読み手はいないので勝率には出ないはず。");
        Console.WriteLine("- カド × ハリ × ナタ: 敵側在庫が 1 を超え、閾値 2 に届く回数が W0 の 0.10回/戦 から増える。");
        Console.WriteLine();
        Console.WriteLine($"所要 {suSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // run <w> [skip] [take]: 2×2 を組ごとに TSV で吐く
    // =====================================================================================
    if (suArg == "run")
    {
        int w = args.Length > 3 ? int.Parse(args[3]) : 0;
        int skip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int take = args.Length > 5 ? int.Parse(args[5]) : suOthers.Length;
        skip = Math.Clamp(skip, 0, suOthers.Length);
        take = Math.Clamp(take, 0, suOthers.Length - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"suture2 run w{w} {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            int b = suOthers[skip + j];
            var ys = SuMeasure(b, w);
            var sb = new System.Text.StringBuilder();
            sb.Append(w).Append('\t').Append(suHari).Append('\t').Append(b).Append('\t').Append(ys.Length);
            foreach (double[] y in ys) for (int k = 0; k < 4; k++) sb.Append('\t').Append(y[k].ToString("R", suInv));
            rows[j] = sb.ToString();
            if (Interlocked.Increment(ref doneR) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {suSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    Dictionary<int, double[][]> SuRead(string path, int expectW)
    {
        var d = new Dictionary<int, double[][]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int w = int.Parse(c[0]);
            if (w != expectW) throw new InvalidOperationException($"{path}: 版 {w} の行が混ざっている（期待 {expectW}）");
            int b = int.Parse(c[2]); int nT = int.Parse(c[3]);
            var ys = new double[nT][];
            int at = 4;
            for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], suInv); }
            d[b] = ys;
        }
        return d;
    }
    double SuSyn(double[] y) => y[0] - y[2] - y[1] + y[3];

    // =====================================================================================
    // tables <w0> <w1> <w2>: 表A〜F
    // =====================================================================================
    if (suArg == "tables")
    {
        if (args.Length < 6 || !File.Exists(args[3]) || !File.Exists(args[4]) || !File.Exists(args[5]))
        {
            Console.WriteLine("suture2 tables <w0 の TSV> <w1 の TSV> <w2 の TSV>");
            return;
        }
        var Y = new[] { SuRead(args[3], 0), SuRead(args[4], 1), SuRead(args[5], 2) };
        var missing = suOthers.Where(b => Y.Any(y => !y.ContainsKey(b))).ToArray();

        Console.WriteLine("# 第85期 —— 糸を味方にも通す（表A〜F）");
        Console.WriteLine();
        Console.WriteLine($"器具は第81期の 2×2 の写し（K = {SuK} 台 × {SuS} 系列・弱い波 {SuWeakPct}%・第2〜5波・seed {SuBand}..{SuBand + SuM - 1}）。"
                          + $"A はハリ固定・B は残り {suOthers.Length} 体。**台・席・戦闘 seed・台の抽選は W0 / W1 / W2 で同一**（同じ台で規則だけを差し替える）。"
                          + (missing.Length > 0 ? $" **TSV に無い組 {missing.Length}**: " + string.Join(" / ", missing.Select(b => suName[b])) : ""));
        Console.WriteLine();

        // Δ per table, per version
        double[][] SynOf(int w, int b) => Y[w][b].Select(SuSyn).Select(x => new[] { x }).ToArray();
        Dictionary<int, double[]>[] dAll = { new(), new() };   // [w-1][b] → Δ per table
        Dictionary<int, double[][]>[] dSer = { new(), new() }; // [w-1][b] → [series][t]
        foreach (int b in suOthers)
        {
            if (Y.Any(y => !y.ContainsKey(b))) continue;
            int nT = Y.Min(y => y[b].Length);
            for (int w = 1; w <= 2; w++)
            {
                var d = new double[nT];
                var ser = new List<double>[SuS];
                for (int sx = 0; sx < SuS; sx++) ser[sx] = new List<double>();
                for (int t = 0; t < nT; t++) { d[t] = SuSyn(Y[w][b][t]) - SuSyn(Y[0][b][t]); ser[t % SuS].Add(d[t]); }
                dAll[w - 1][b] = d; dSer[w - 1][b] = ser.Select(l => l.ToArray()).ToArray();
            }
        }

        // ---- 表A: 紙と実測（自己検査 (h)）--------------------------------------------------------
        Console.WriteLine("## 表A —— 紙のスループット（§1-1）と実測の突き合わせ【自己検査 (h)】");
        Console.WriteLine();
        Console.WriteLine($"組 (ハリ, カド) の台 {SuProbeTables} 台の y11 を W0 / W1 / W2 で verbose に回し直した。紙は W0 の分子から出す（`phase0` と同じ式）。"
                          + "実測は W1 の `SutureHealed`（ハリの繕いで実際に増えた HP）／戦。**紙の下限と上限の間に実測が入り、下限の 2 倍以内なら (h) は通る。**");
        Console.WriteLine();
        var pk = new SuStat[3][];
        Parallel.For(0, 3, w => pk[w] = SuProbePair(suKado, w));
        Console.WriteLine("| 波 | 反撃/戦 W0 | 供給/戦（紙） | 回復 下限（紙） | 回復 上限（紙） | **実測 回復/戦 W1** | 実測/下限 | W2 | 味方傷/戦 W1（実測） | 総被ダメ/戦 W0 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv <= suW; wv++)
        {
            SuStat s0 = wv < suW ? pk[0][wv] : SuSum(pk[0]), s1 = wv < suW ? pk[1][wv] : SuSum(pk[1]), s2 = wv < suW ? pk[2][wv] : SuSum(pk[2]);
            var pp = SuPaper(s0);
            double m1 = s1.SutureHealed / s1.N, m2 = s2.SutureHealed / s2.N;
            Console.WriteLine($"| {(wv < suW ? $"第{wv + 1}波" : "**第2〜5波**")} | {pp.fires:F2} | {pp.supply:F2} | {pp.lo:F1} | {pp.hi:F1} | **{m1:F1}** | {(pp.lo > 0 ? (m1 / pp.lo).ToString("F2") : "—")} | {m2:F1} | {s1.ThornWoundAlly / s1.N:F2} | {pp.taken:F1} |");
        }
        {
            var s0 = SuSum(pk[0]); var s1 = SuSum(pk[1]); var pp = SuPaper(s0);
            double m1 = s1.SutureHealed / s1.N;
            bool hOk = pp.lo > 0 && m1 >= pp.lo / 2 && m1 <= pp.lo * 2 || (m1 >= pp.lo && m1 <= pp.hi);
            Console.WriteLine();
            Console.WriteLine($"**(h)**: 紙の下限 {pp.lo:F1} ／ 上限 {pp.hi:F1} に対し実測 {m1:F1}（比 {(pp.lo > 0 ? m1 / pp.lo : double.NaN):F2}）→ **{(hOk ? "○（2 倍以内）" : "×")}**。"
                              + $" 実測の供給 {s1.ThornWoundAlly / s1.N:F2} 対 紙の供給 {pp.supply:F2}（比 {(pp.supply > 0 ? s1.ThornWoundAlly / s1.N / pp.supply : double.NaN):F2}）。");
        }
        Console.WriteLine();

        // ---- 表B: 主判定 ------------------------------------------------------------------------
        Console.WriteLine("## 表B —— 味方に傷を書く6枚の 相乗 W0 / W1 / W2 / Δ（系列別・SE 併記）【主判定 Q1a・Q1b】");
        Console.WriteLine();
        Console.WriteLine("`相乗(ハリ,B) = y11 − y10 − y01 + y00`／`Δ相乗_W = 相乗_W − 相乗_W0`（**同じ台・同じ seed の対**なので Δ の SE は台ごとの Δ から出す）。");
        Console.WriteLine();
        Console.WriteLine("| B | 系列 | 台数 | 相乗 W0 | SE | 相乗 W1 | **Δ W1** | SE(Δ) | 相乗 W2 | **Δ W2** | SE(Δ) | y11 W0 | y11 W1 | y11 W2 | y00 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var q1bSer = new double[SuS]; var q1aSer = new double[SuS];
        var q1bVals = new List<double>(); double q1a = double.NaN;
        foreach (int b in suWriters)
        {
            if (!dAll[0].ContainsKey(b)) { Console.WriteLine($"| {suName[b]} | — | 0 | | | | | | | | | | | | |"); continue; }
            int nT = dAll[0][b].Length;
            for (int sx = 0; sx < SuS; sx++)
            {
                var ts = Enumerable.Range(0, nT).Where(t => t % SuS == sx).ToArray();
                var s0 = ts.Select(t => SuSyn(Y[0][b][t])).ToList(); var s1 = ts.Select(t => SuSyn(Y[1][b][t])).ToList(); var s2 = ts.Select(t => SuSyn(Y[2][b][t])).ToList();
                var d1 = dSer[0][b][sx]; var d2 = dSer[1][b][sx];
                Console.WriteLine($"| {suName[b]} | {sx + 1} | {ts.Length} | {SuP2(s0.Average())} | {SuSe(s0):F2} | {SuP2(s1.Average())} | **{SuP2(d1.Average())}** | {SuSe(d1):F2} | {SuP2(s2.Average())} | **{SuP2(d2.Average())}** | {SuSe(d2):F2} | {ts.Average(t => Y[0][b][t][0]):F1} | {ts.Average(t => Y[1][b][t][0]):F1} | {ts.Average(t => Y[2][b][t][0]):F1} | {ts.Average(t => Y[0][b][t][3]):F1} |");
                q1bSer[sx] += d2.Average() / suWriters.Length;
                if (b == suKado) q1aSer[sx] = d1.Average();
            }
            {
                var s0 = Y[0][b].Take(nT).Select(SuSyn).ToList(); var s1 = Y[1][b].Take(nT).Select(SuSyn).ToList(); var s2 = Y[2][b].Take(nT).Select(SuSyn).ToList();
                Console.WriteLine($"| **{suName[b]}** | 合算 | {nT} | {SuP2(s0.Average())} | {SuSe(s0):F2} | {SuP2(s1.Average())} | **{SuP2(dAll[0][b].Average())}** | {SuSe(dAll[0][b]):F2} | {SuP2(s2.Average())} | **{SuP2(dAll[1][b].Average())}** | {SuSe(dAll[1][b]):F2} | {Y[0][b].Take(nT).Average(y => y[0]):F1} | {Y[1][b].Take(nT).Average(y => y[0]):F1} | {Y[2][b].Take(nT).Average(y => y[0]):F1} | {Y[0][b].Take(nT).Average(y => y[3]):F1} |");
                q1bVals.Add(dAll[1][b].Average());
                if (b == suKado) q1a = dAll[0][b].Average();
            }
        }
        double q1b = q1bVals.Count > 0 ? q1bVals.Average() : double.NaN;
        Console.WriteLine();
        Console.WriteLine($"**Q1a（W1）: Δ相乗(カド) = {SuP2(q1a)}pt**（系列1 {SuP2(q1aSer[0])} / 系列2 {SuP2(q1aSer[1])}）。線 +{SuQ1Line:F1} → **{(q1a >= SuQ1Line ? "○" : "×")}**。");
        Console.WriteLine($"**Q1b（W2）: 6枚の Δ相乗 の平均 = {SuP2(q1b)}pt**（系列1 {SuP2(q1bSer[0])} / 系列2 {SuP2(q1bSer[1])}）。線 +{SuQ1Line:F1} → **{(q1b >= SuQ1Line ? "○" : "×")}**。");
        Console.WriteLine($"**W2 − W1 の帰属**（同じ B の Δ W2 − Δ W1・**カド以外の5枚**）: "
                          + string.Join(" / ", suWriters.Where(b => b != suKado && dAll[0].ContainsKey(b)).Select(b => $"{suName[b]} {SuP2(dAll[1][b].Average() - dAll[0][b].Average())}")) + "。");
        Console.WriteLine();

        // ---- 表C: ハリ × 50 ---------------------------------------------------------------------
        for (int w = 1; w <= 2; w++)
        {
            Console.WriteLine($"## 表C-{w} —— ハリ × 全 {suOthers.Length} 体の Δ相乗 {SuVName(w)}（降順・上位 {SuTop} と下位 {SuTop}）");
            Console.WriteLine();
            Console.WriteLine("★ は味方に傷を書く6枚。`有意` は |Δ| > 2SE(Δ)。**個数は主判定に混ぜない。**");
            Console.WriteLine();
            var dA = dAll[w - 1]; var dS = dSer[w - 1];
            var order = dA.Keys.OrderByDescending(b => dA[b].Average()).ToArray();
            Console.WriteLine($"| 順位 | B | Δ相乗 W{w} | SE(Δ) | 有意 | 相乗 W0 | 相乗 W{w} | 系列1 Δ | 系列2 Δ | y00 |");
            Console.WriteLine("|--:|---|--:|--:|:-:|--:|--:|--:|--:|--:|");
            void CRow(int rank, int b)
            {
                double m = dA[b].Average(), se = SuSe(dA[b]);
                int nT = dA[b].Length;
                Console.WriteLine($"| {rank} | {(suWriters.Contains(b) ? "★" : "")}{suName[b]} | **{SuP2(m)}** | {se:F2} | {(Math.Abs(m) > 2 * se ? "○" : "")} | {SuP2(Y[0][b].Take(nT).Select(SuSyn).Average())} | {SuP2(Y[w][b].Take(nT).Select(SuSyn).Average())} | {SuP2(dS[b][0].Average())} | {SuP2(dS[b][1].Average())} | {Y[0][b].Take(nT).Average(y => y[3]):F1} |");
            }
            for (int i = 0; i < Math.Min(SuTop, order.Length); i++) CRow(i + 1, order[i]);
            if (order.Length > 2 * SuTop) Console.WriteLine("| … | | | | | | | | | |");
            for (int i = Math.Max(SuTop, order.Length - SuTop); i < order.Length; i++) CRow(i + 1, order[i]);
            Console.WriteLine();
            int nPos = order.Count(b => dA[b].Average() > 2 * SuSe(dA[b])), nNeg = order.Count(b => dA[b].Average() < -2 * SuSe(dA[b]));
            Console.WriteLine($"全 {order.Length} 体の Δ相乗 の平均 {SuP2(order.Average(b => dA[b].Average()))}・中央値 {SuP2(order.Select(b => dA[b].Average()).OrderBy(x => x).ElementAt(order.Length / 2))}・"
                              + $"有意な正 {nPos} / 有意な負 {nNeg}（参考）。6枚の順位: "
                              + string.Join(" / ", suWriters.Where(b => dA.ContainsKey(b)).Select(b => $"{suName[b]} {Array.IndexOf(order, b) + 1} 位")) + "。");
            Console.WriteLine();
        }

        // ---- Q5: ハリの単独の帰属 --------------------------------------------------------------
        Console.WriteLine("## Q5 —— ハリの単独の帰属（y11 − y01）の W0 → W1 → W2");
        Console.WriteLine();
        Console.WriteLine("全 50 組 × 128 台の平均（第82期の「単独」と同じ量。第83期でハリは「切れる」判定が出た唯一の駒・床は −1.5）。");
        Console.WriteLine();
        Console.WriteLine("| | W0 | W1 | W2 | Δ W1 | Δ W2 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        {
            var solo = new double[3];
            for (int w = 0; w < 3; w++)
            {
                var xs = new List<double>();
                foreach (int b in suOthers) if (Y[w].ContainsKey(b)) xs.AddRange(Y[w][b].Select(y => y[0] - y[1]));
                solo[w] = xs.Average();
            }
            Console.WriteLine($"| 単独(ハリ)・全組 | {SuP2(solo[0])} | {SuP2(solo[1])} | {SuP2(solo[2])} | **{SuP2(solo[1] - solo[0])}** | **{SuP2(solo[2] - solo[0])}** |");
            foreach (int b in suWriters)
            {
                if (Y.Any(y => !y.ContainsKey(b))) continue;
                var sb3 = new double[3];
                for (int w = 0; w < 3; w++) sb3[w] = Y[w][b].Average(y => y[0] - y[1]);
                Console.WriteLine($"| 単独(ハリ)・B = {suName[b]} | {SuP2(sb3[0])} | {SuP2(sb3[1])} | {SuP2(sb3[2])} | {SuP2(sb3[1] - sb3[0])} | {SuP2(sb3[2] - sb3[0])} |");
            }
            Console.WriteLine();
            Console.WriteLine($"床（−1.5）との位置: W0 {SuP2(solo[0])} → W1 {SuP2(solo[1])} → W2 {SuP2(solo[2])}。**{(solo[2] >= -1.5 && solo[0] < -1.5 ? "床から離れた" : solo[0] >= -1.5 ? "W0 の時点で床の上にいる（第83期の「切れる」は在席行 1 行・理想台の判定）" : "床の下のまま")}**。");
        }
        Console.WriteLine();

        // ---- 表D: 供給と在庫（Q2）--------------------------------------------------------------
        Console.WriteLine("## 表D —— 味方側の供給と在庫（Q2。波別・書き手別）");
        Console.WriteLine();
        Console.WriteLine($"6枚の組それぞれについて、2×2 と同じ台のうち各系列の先頭 {SuProbeTables / SuS} 台 = {SuProbeTables} 台の y11 を弱い波の第2〜5波 × seed {SuBand}..{SuBand + SuM - 1} で verbose に回し直した。"
                          + "`味方傷/戦` ＝ 味方に傷が書かれた回数（`CarryCount[CarryWound]` の味方側。書き手を問わない）／`棘(味)` ＝ カドの棘が味方に書いた回数（W1）／"
                          + "`巻(B)` ＝ 巻き込み則で B が書いた回数（W2）／`巻(全)` ＝ 巻き込み則の全回数（W2）／`在庫(味)/T` ＝ ターン頭の味方側の傷の合計のターン平均／`在庫(敵)/T` ＝ 同・敵側。");
        Console.WriteLine();
        var cst = new SuStat[suWriters.Length, 3, suW];
        Parallel.For(0, suWriters.Length * 3, k =>
        {
            int i = k / 3, w = k % 3;
            var loc = SuProbePair(suWriters[i], w);
            for (int wv = 0; wv < suW; wv++) cst[i, w, wv] = loc[wv];
        });
        Console.WriteLine("| B | 波 | 版 | 味方傷/戦 | 棘(味)/戦 | 巻(B)/戦 | 巻(全)/戦 | **在庫(味)/T** | 最大 | 在庫(敵)/T | 傷あるT/決着T | 決着T | 勝率 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < suWriters.Length; i++)
            for (int wv = 1; wv < suW; wv++)
                for (int w = 0; w < 3; w++)
                {
                    SuStat sx = cst[i, w, wv];
                    if (sx.N == 0) continue;
                    int wi = i;   // 書き手の添字（suWriters の並び ＝ suWriterIds の並び）
                    Console.WriteLine($"| {suName[suWriters[i]]} | 第{wv + 1}波 | W{w} | {sx.CarryWoundAlly / sx.N:F2} | {sx.ThornWoundAlly / sx.N:F2} | {sx.SpillByWriter[wi] / sx.N:F2} | {sx.SpillWounds / sx.N:F2} | **{sx.StockAlly / sx.N:F2}** | {sx.StockAllyMax / sx.N:F2} | {sx.StockFoe / sx.N:F2} | {(sx.Turns > 0 ? sx.AllyWoundTurns / sx.Turns : 0):F2} | {sx.Turns / sx.N:F1} | {sx.Wins * 100.0 / sx.N:F1}% |");
                }
        Console.WriteLine();
        // 書き手別の帰属（W2・6組合算・第2〜5波）
        Console.WriteLine("| 書き手（W2・6組合算） | 巻き込みで書いた/戦 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        {
            for (int k = 0; k < suWriters.Length; k++)
            {
                double tot = 0, n = 0; var byW = new double[suW];
                for (int i = 0; i < suWriters.Length; i++) for (int wv = 1; wv < suW; wv++) { tot += cst[i, 2, wv].SpillByWriter[k]; n += cst[i, 2, wv].N; byW[wv] += cst[i, 2, wv].N > 0 ? cst[i, 2, wv].SpillByWriter[k] / cst[i, 2, wv].N / suWriters.Length : 0; }
                Console.WriteLine($"| {suName[suWriters[k]]} | {tot / n:F3} | {byW[1]:F3} | {byW[2]:F3} | {byW[3]:F3} | {byW[4]:F3} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("**リィカの生贄は開戦時1回・`lethal: false`** なので、リィカ同席の台では t=0 の在庫が隣接数ぶん立つ（第1ターン頭の `StatusSnapshot` に載る）。"
                          + "**ナラの削りは `lethal: true`** で、削りで倒れた味方には書かれない（生存確認の後に置いてある）。");
        Console.WriteLine();

        // ---- 表E: 糸口の内訳（Q3）・(j) ------------------------------------------------------------
        Console.WriteLine("## 表E —— 糸口の内訳（Q3）と読まれないまま落ちた傷（自己検査 (j)）");
        Console.WriteLine();
        Console.WriteLine("同じ verbose の実行から（6組合算）。`敵から`／`味方から` ＝ ハリが糸を引いた回数（`UnitTally.SutureFoe` / `SutureAlly`）／`乾き` ＝ 繕いが 1 点も届かなかった回数（渇きの下で塞ぎだけ走った）／"
                          + "`繕い/戦` ＝ ハリの繕いで実際に増えた HP／`ハリ振/戦` ＝ `Attacks`／`未読(味)` ＝ 味方側で「倒れた時点 ＋ 戦闘終了時」に残っていた傷 ÷ 味方に書かれた傷／`未読(敵)` ＝ 同・敵側。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 敵から/戦 | 味方から/戦 | 乾き/戦 | 繕い/戦 | ハリ振/戦 | 味方全体の回復/戦 | 未読(味) | 未読(敵) | ハリ死亡% | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv <= suW; wv++)
            for (int w = 0; w < 3; w++)
            {
                var sx = new SuStat();
                for (int i = 0; i < suWriters.Length; i++) { if (wv < suW) sx.AddFrom(cst[i, w, wv]); else for (int v2 = 1; v2 < suW; v2++) sx.AddFrom(cst[i, w, v2]); }
                if (sx.N == 0) continue;
                Console.WriteLine($"| {(wv < suW ? $"第{wv + 1}波" : "**第2〜5波**")} | W{w} | {sx.SutureFoe / sx.N:F2} | **{sx.SutureAlly / sx.N:F2}** | {sx.SutureDry / sx.N:F2} | {sx.SutureHealed / sx.N:F1} | {sx.HariAttacks / sx.N:F2} | {sx.TeamHealed / sx.N:F1} | {(sx.CarryWoundAlly > 0 ? ((sx.DeathWoundAlly + sx.EndWoundAlly) / sx.CarryWoundAlly * 100).ToString("F1") + "%" : "—")} | {(sx.CarryWoundFoe > 0 ? ((sx.DeathWoundFoe + sx.EndWoundFoe) / sx.CarryWoundFoe * 100).ToString("F1") + "%" : "—")} | {sx.HariDeaths * 100.0 / sx.N:F1}% | {sx.Wins * 100.0 / sx.N:F1}% |");
            }
        Console.WriteLine();
        {
            var d3 = new SuStat[3]; for (int w = 0; w < 3; w++) { d3[w] = new SuStat(); for (int i = 0; i < suWriters.Length; i++) d3[w].AddFrom(cst[i, w, 2]); }
            Console.WriteLine($"**Q3**（第三波・渇き）: 「繕いは 0 だが塞ぎは走った」回数 W0 {d3[0].SutureDry / d3[0].N:F2} → W1 {d3[1].SutureDry / d3[1].N:F2} → W2 {d3[2].SutureDry / d3[2].N:F2} 回/戦"
                              + $"（糸を引いた回数 W0 {(d3[0].SutureFoe + d3[0].SutureAlly) / d3[0].N:F2} → W1 {(d3[1].SutureFoe + d3[1].SutureAlly) / d3[1].N:F2} → W2 {(d3[2].SutureFoe + d3[2].SutureAlly) / d3[2].N:F2}）。"
                              + "第三波では糸を引いた回数 ＝ 乾きの回数になるはず（渇きの祭司が生きている限り `Heal` は 1 点も通らない）。");
        }
        Console.WriteLine();
        Console.WriteLine("**Q4（ナタの解凍）は `suture2 nata` で別に出す**（`CompareBuilds()` を触らない専用台）。");
        Console.WriteLine();

        // ---- 表F: 自己検査 ----------------------------------------------------------------------
        Console.WriteLine("## 表F —— 自己検査（(a)(b)(c)(d)(f)(g)(h)(i)(j)。(e) と拒否権は `suture2 check`）");
        Console.WriteLine();
        // (a) B = カド: カド素体のセル（TSV の添字 2 = ハリ本物・カド素体、3 = 両方素体）が W0 と W1 で一致。
        //     W2 は添字 3 だけ（添字 2 は埋め草の書き手が巻き込み則で書いた傷をハリが読むので、動いてよい）
        double maxA = 0, maxA2 = 0; int cellsA = 0, cellsA2 = 0;
        if (Y.All(y => y.ContainsKey(suKado)))
        {
            int nT = Y.Min(y => y[suKado].Length);
            for (int t = 0; t < nT; t++)
            {
                foreach (int k in new[] { 2, 3 }) { cellsA++; maxA = Math.Max(maxA, Math.Abs(Y[0][suKado][t][k] - Y[1][suKado][t][k])); }
                cellsA2++; maxA2 = Math.Max(maxA2, Math.Abs(Y[0][suKado][t][3] - Y[2][suKado][t][3]));
            }
        }
        // (b) ハリ素体のセル（TSV の添字 1 = ハリ素体・B 本物、3 = 両方素体）が W0/W1/W2 で一致。全体と、「カドと敵の傷の読み手が同席しない台」に限定した値
        double maxB = 0, maxBClean = 0; int cellsB = 0, cellsBClean = 0;
        foreach (int b in suOthers)
        {
            if (Y.Any(y => !y.ContainsKey(b))) continue;
            var fills = SuFills(b);
            int nT = Math.Min(Y.Min(y => y[b].Length), fills.Count);
            for (int t = 0; t < nT; t++)
            {
                var team = SuTeam(b, fills[t]);
                bool clean = !SuHasId(team, "kado") || !suFoeReaderIds.Any(id => SuHasId(team, id));
                foreach (int k in new[] { 1, 3 }) for (int w = 1; w <= 2; w++)
                    {
                        double dd = Math.Abs(Y[0][b][t][k] - Y[w][b][t][k]);
                        cellsB++; maxB = Math.Max(maxB, dd);
                        if (clean) { cellsBClean++; maxBClean = Math.Max(maxBClean, dd); }
                    }
            }
        }
        // (c) カドの味方側の書き込みが W1 と W2 で同数（他の5枚の書き手を含まない台に限る。含む台では W2 の盤面が W1 と分岐する）
        var c1 = SuSum(SuProbePair(suKado, 1, team => !suWriterIds.Skip(1).Any(id => SuHasId(team, id))));
        var c2 = SuSum(SuProbePair(suKado, 2, team => !suWriterIds.Skip(1).Any(id => SuHasId(team, id))));
        // (d)(f)(g)(j) は表D・E の集計から
        var all1 = new SuStat(); var all2 = new SuStat(); var w2a = new SuStat(); var w1a = new SuStat(); var w2b = new SuStat();
        for (int i = 0; i < suWriters.Length; i++) for (int wv = 1; wv < suW; wv++) { all1.AddFrom(cst[i, 1, wv]); all2.AddFrom(cst[i, 2, wv]); }
        w1a.AddFrom(cst[0, 1, 1]);                                                        // W1・カド組・第二波
        var w1d = new SuStat(); w1d.AddFrom(cst[0, 1, 3]);                                 // W1・カド組・第四波（粛の無い波との比）
        for (int i = 1; i < suWriters.Length; i++) w2b.AddFrom(cst[i, 2, 1]);            // W2・カド以外の5組・第二波
        Console.WriteLine("| 検査 | 内容 | 値 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        Console.WriteLine($"| (a) | 組 (ハリ, カド) の カド素体のセル（ハリ本物・カド素体 と 両方素体）が W0 と W1 で完全一致 ／ 両方素体 は W2 とも一致 | W1 {cellsA} セル・最大差 {maxA:F10}pt ／ W2（両方素体）{cellsA2} セル・最大差 {maxA2:F10}pt | {(maxA == 0 && maxA2 == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (b) | ハリ素体のセル（ハリ素体・B 本物 と 両方素体）が W0・W1・W2 で完全一致 | 全体 {cellsB} セル・最大差 {maxB:F4}pt ／ **カドと敵の傷の読み手（エグ・ノミ・ナタ）が同席しない台 {cellsBClean} セル・最大差 {maxBClean:F10}pt** | {(maxBClean == 0 ? "○" : "**×**")}{(maxB > 0 ? "（全体は W1/W2 の `ThornWound` が敵に傷を書くぶん動く。第84期 V1 と同じ）" : "")} |");
        Console.WriteLine($"| (c) | カドの味方側の書き込みが W1（棘）と W2（巻き込み則）で同数（他の5枚を含まない台・{c1.N} 戦） | W1 {c1.ThornWoundAlly} 対 W2 {c2.KadoSpill}（W2 の棘(味) {c2.ThornWoundAlly}・W1 の巻 {c1.SpillWounds}）／勝敗一致 {(c1.Wins == c2.Wins && c1.Turns == c2.Turns ? "○" : "×")} | {(c1.ThornWoundAlly == c2.KadoSpill && c2.ThornWoundAlly == 0 && c1.SpillWounds == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (d) | 味方の傷の書き込み回数 > 0 | W1 {all1.CarryWoundAlly / all1.N:F2} ／ W2 {all2.CarryWoundAlly / all2.N:F2} 回/戦 | {(all1.CarryWoundAlly > 0 && all2.CarryWoundAlly > 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (f) | 1巻き込み = 1傷（W2: 味方の刃の着弾（生存）÷ 巻き込み則の書き込み = 1.00） | {all2.SpillHits} / {all2.SpillWounds} = {(all2.SpillWounds > 0 ? all2.SpillHits / all2.SpillWounds : double.NaN):F4} | {(all2.SpillHits == all2.SpillWounds ? "○" : "**×**")} |");
        Console.WriteLine($"| (g) | 第二波（粛）でカド由来の味方側供給が 0 に近い（第四波の 5% 未満）／ W2 ではカド以外の書き手は第二波でも出る | W1・カド組 棘(味) 第二波 {w1a.ThornWoundAlly / w1a.N:F3} 対 第四波 {w1d.ThornWoundAlly / w1d.N:F2} 回/戦（比 {(w1d.ThornWoundAlly > 0 ? w1a.ThornWoundAlly / w1a.N / (w1d.ThornWoundAlly / w1d.N) * 100 : double.NaN):F1}%・粛の伝令が落ちた後の反撃）／ W2・5組 巻(全) 第二波 {w2b.SpillWounds / w2b.N:F2} 回/戦 | {(w1a.ThornWoundAlly / w1a.N < 0.05 * (w1d.ThornWoundAlly / w1d.N) && w2b.SpillWounds > 0 ? "○" : "**×**")} |");
        {
            var s0 = SuSum(pk[0]); var s1 = SuSum(pk[1]); var pp = SuPaper(s0); double m1 = s1.SutureHealed / s1.N;
            bool hOk = pp.lo > 0 && m1 >= pp.lo / 2 && m1 <= pp.lo * 2 || (m1 >= pp.lo && m1 <= pp.hi);
            Console.WriteLine($"| (h) | 紙のスループットと実測の回復/戦 が 2 倍以内 | 紙 下限 {pp.lo:F1}・上限 {pp.hi:F1} ／ 実測 {m1:F1}（比 {(pp.lo > 0 ? m1 / pp.lo : double.NaN):F2}） | {(hOk ? "○" : "**×**")} |");
        }
        Console.WriteLine($"| (i) | 主判定が2系列で同符号 | Q1a {SuP2(q1aSer[0])} / {SuP2(q1aSer[1])} ／ Q1b {SuP2(q1bSer[0])} / {SuP2(q1bSer[1])} | {(Math.Sign(q1aSer[0]) == Math.Sign(q1aSer[1]) ? "○" : "**×**")} / {(Math.Sign(q1bSer[0]) == Math.Sign(q1bSer[1]) ? "○" : "**×**")} |");
        Console.WriteLine($"| (j) | 読まれないまま落ちた傷の割合（味方側／敵側） | W1 味 {(all1.CarryWoundAlly > 0 ? (all1.DeathWoundAlly + all1.EndWoundAlly) / all1.CarryWoundAlly * 100 : double.NaN):F1}% / 敵 {(all1.CarryWoundFoe > 0 ? (all1.DeathWoundFoe + all1.EndWoundFoe) / all1.CarryWoundFoe * 100 : double.NaN):F1}% ／ W2 味 {(all2.CarryWoundAlly > 0 ? (all2.DeathWoundAlly + all2.EndWoundAlly) / all2.CarryWoundAlly * 100 : double.NaN):F1}% / 敵 {(all2.CarryWoundFoe > 0 ? (all2.DeathWoundFoe + all2.EndWoundFoe) / all2.CarryWoundFoe * 100 : double.NaN):F1}% | 記録 |");
        Console.WriteLine();
        Console.WriteLine("## 判定表");
        Console.WriteLine();
        Console.WriteLine("| | 判定 | 値 | 線 | 結果 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1a** | **W1: Δ相乗(カド)** | **{SuP2(q1a)}** | ≥ +{SuQ1Line:F1} | **{(q1a >= SuQ1Line ? "○" : "×")}** |");
        Console.WriteLine($"| **Q1b** | **W2: 6枚の Δ相乗 の平均** | **{SuP2(q1b)}** | ≥ +{SuQ1Line:F1} | **{(q1b >= SuQ1Line ? "○" : "×")}** |");
        {
            var s1 = SuSum(pk[1]); var s2 = SuSum(pk[2]);
            Console.WriteLine($"| Q2 | 味方側の供給/戦・在庫/T（カド組） | W1 供給 {s1.ThornWoundAlly / s1.N:F2}・在庫 {s1.StockAlly / s1.N:F2} ／ W2 供給 {s2.SpillWounds / s2.N:F2}・在庫 {s2.StockAlly / s2.N:F2} | 記録 | — |");
            Console.WriteLine($"| Q3 | 糸口の内訳（カド組・第2〜5波） | W1 敵 {s1.SutureFoe / s1.N:F2} / 味 {s1.SutureAlly / s1.N:F2} ／ W2 敵 {s2.SutureFoe / s2.N:F2} / 味 {s2.SutureAlly / s2.N:F2} 回/戦 | 記録 | — |");
        }
        Console.WriteLine("| Q4 | ナタの解凍 | `suture2 nata` | 記録 | — |");
        {
            var solo = new double[3];
            for (int w = 0; w < 3; w++) { var xs = new List<double>(); foreach (int b in suOthers) if (Y[w].ContainsKey(b)) xs.AddRange(Y[w][b].Select(y => y[0] - y[1])); solo[w] = xs.Average(); }
            Console.WriteLine($"| Q5 | ハリの単独の帰属 W0 → W1 → W2 | {SuP2(solo[0])} → {SuP2(solo[1])} → {SuP2(solo[2])} | 床 −1.5 | {(solo[2] >= -1.5 ? "床の上" : "床の下")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {suSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // nata: Q4 の専用台（カド × ハリ × ナタ ＋ 埋め草2枚）
    // =====================================================================================
    if (suArg == "nata")
    {
        Console.WriteLine("# 第85期 —— Q4: ナタの解凍（カド × ハリ × ナタ の専用台）");
        Console.WriteLine();
        int NT = SuProbeTables;
        var pool = suRoster.Where((_, u) => u != suHari && u != suKado && u != suNata).ToArray();
        int strong0 = new[] { suHari, suKado, suNata }.Count(u => suRoster[u].Attack >= SuStrong);
        var seen = new HashSet<(int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < NT && draw < SuDrawCap; draw++)
        {
            var f = SuFill(pool, strong0, SuSeed(suPairIxOf[suKado, suNata], draw), 2);
            var t = f.Select(d => suIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1]))) fills.Add(f);
        }
        Console.WriteLine($"台 ＝ カド・ハリ・ナタ ＋ 埋め草2枚（残り 48 体・規則 P・{NT} 通り・席は規則配置 H）× 弱い波の第2〜5波 × seed {SuBand}..{SuBand + SuM - 1}。"
                          + "`CompareBuilds()` は触っていない。第39期の「ハリとナタは同居させない」（塞ぎ 1/T が供給 1/T と等速で敵側在庫が天井 1・閾値 2 に届かない）を、"
                          + "**塞ぎが味方側へ逸れれば敵側の在庫は減らない**という予測で測る。");
        Console.WriteLine();
        Console.WriteLine("埋め草: " + string.Join(" / ", fills.Select(f => $"{f[0].Name}・{f[1].Name}")));
        Console.WriteLine();
        var st = new SuStat[3, suW];
        for (int w = 0; w < 3; w++) for (int wv = 0; wv < suW; wv++) st[w, wv] = new SuStat();
        Parallel.For(0, 3, w =>
        {
            var loc = new SuStat[suW]; for (int wv = 0; wv < suW; wv++) loc[wv] = new SuStat();
            foreach (var fl in fills)
            {
                var team = new[] { suRoster[suHari], suRoster[suKado], suRoster[suNata], fl[0], fl[1] };
                int[] seats = SuSeats(team);
                var f = SuForm(team, seats, 0);
                for (int wv = 1; wv < suW; wv++)
                    for (int seed = SuBand; seed < SuBand + SuM; seed++) SuProbe(f, suWeak[wv].Enemy, seed, w, loc[wv]);
            }
            for (int wv = 0; wv < suW; wv++) st[w, wv] = loc[wv];
        });
        Console.WriteLine("| 波 | 版 | 断ち/戦 | **閾値到達/戦** | 傷/断ち | 在庫(敵)/T | 在庫(味)/T | 糸 敵/戦 | 糸 味/戦 | 繕い/戦 | 反撃/戦 | 決着T | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv <= suW; wv++)
            for (int w = 0; w < 3; w++)
            {
                SuStat sx = wv < suW ? st[w, wv] : SuSum(Enumerable.Range(0, suW).Select(v => st[w, v]).ToArray());
                Console.WriteLine($"| {(wv < suW ? $"第{wv + 1}波" : "**第2〜5波**")} | W{w} | {sx.Sever / sx.N:F2} | **{sx.SeverReached / sx.N:F2}** | {(sx.Sever > 0 ? (sx.SeverWounds / sx.Sever).ToString("F2") : "—")} | {sx.StockFoe / sx.N:F2} | {sx.StockAlly / sx.N:F2} | {sx.SutureFoe / sx.N:F2} | {sx.SutureAlly / sx.N:F2} | {sx.SutureHealed / sx.N:F1} | {sx.KadoFires / sx.N:F2} | {sx.Turns / sx.N:F1} | {sx.Wins * 100.0 / sx.N:F1}% |");
            }
        Console.WriteLine();
        {
            var a0 = SuSum(Enumerable.Range(0, suW).Select(v => st[0, v]).ToArray()); var a1 = SuSum(Enumerable.Range(0, suW).Select(v => st[1, v]).ToArray()); var a2 = SuSum(Enumerable.Range(0, suW).Select(v => st[2, v]).ToArray());
            Console.WriteLine($"**Q4**: 閾値 {SeverTrait.Threshold} に届いた回数/戦 W0 {a0.SeverReached / a0.N:F2} → W1 {a1.SeverReached / a1.N:F2} → W2 {a2.SeverReached / a2.N:F2}"
                              + $"（第84期のナタ行の W0 は 0.10 回/戦）。敵側在庫/T W0 {a0.StockFoe / a0.N:F2} → W1 {a1.StockFoe / a1.N:F2} → W2 {a2.StockFoe / a2.N:F2}。"
                              + $"ハリの糸口 W1: 敵 {a1.SutureFoe / a1.N:F2} / 味 {a1.SutureAlly / a1.N:F2}。勝率 W0 {a0.Wins * 100.0 / a0.N:F1}% → W1 {a1.Wins * 100.0 / a1.N:F1}% → W2 {a2.Wins * 100.0 / a2.N:F1}%。");
            Console.WriteLine();
            Console.WriteLine($"第39期の「同居させない」の訂正条件（§5）: 閾値到達が W0 から **大きく動いた**か → {(a1.SeverReached / a1.N >= 2 * Math.Max(0.05, a0.SeverReached / a0.N) ? "**動いた**（2 倍以上）" : "動いていない（2 倍未満）")}。");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {suSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check: `compare` 61 行の W0 / W1 / W2・拒否権1・2・(e)・`docs/balance.md` との突き合わせ
    // =====================================================================================
    if (suArg == "check")
    {
        Console.WriteLine("# 第85期 —— 受け入れ基準: `compare` 61 行の W0 / W1 / W2 の突き合わせと拒否権");
        Console.WriteLine();
        var c = new double[3][][];
        for (int w = 0; w < 3; w++) c[w] = new double[suAllRows.Length][];
        Parallel.For(0, suAllRows.Length * 3, k =>
        {
            int i = k / 3, w = k % 3;
            c[w][i] = SuCompare(suAllRows[i].F, w);
        });
        int mismDoc = 0, cellsDoc = 0, missingDoc = 0;
        var mism = new int[3]; var mismNoHari = new int[3]; var movedRows = new HashSet<string>[3];
        for (int w = 1; w < 3; w++) movedRows[w] = new HashSet<string>();
        var lines = new List<string>();
        for (int i = 0; i < suAllRows.Length; i++)
        {
            bool hasHari = SuHas(suAllRows[i].F, "hari");
            if (suBalance.TryGetValue(suAllRows[i].Name, out double[]? doc))
                for (int wv = 0; wv < suW; wv++) { cellsDoc++; if (Math.Abs(doc[wv] - c[0][i][wv]) > 0.05) { mismDoc++; lines.Add($"| {suAllRows[i].Name} | 第{wv + 1}波 | docs {doc[wv]:F1} | W0 {c[0][i][wv]:F1} |"); } }
            else missingDoc++;
            for (int w = 1; w < 3; w++)
                for (int wv = 0; wv < suW; wv++)
                    if (Math.Abs(c[0][i][wv] - c[w][i][wv]) > 0.001)
                    {
                        mism[w]++; movedRows[w].Add(suAllRows[i].Name);
                        if (!hasHari) mismNoHari[w]++;
                        lines.Add($"| {suAllRows[i].Name} | 第{wv + 1}波 | W0 {c[0][i][wv]:F1} | W{w} {c[w][i][wv]:F1} |");
                    }
        }
        Console.WriteLine($"`CompareBuilds()` {suAllRows.Length} 行 × {suW} 波 × seed 0..199 を W0 / W1 / W2 で回した（{suAllRows.Length * suW * 200 * 3:N0} 戦）。");
        Console.WriteLine();
        Console.WriteLine("| 検査 | 内容 | 値 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        Console.WriteLine($"| 受け入れ | W0 と `docs/balance.md` の突き合わせ | {cellsDoc} セル・ずれ {mismDoc} 件（`docs` に無い行 {missingDoc}） | {(mismDoc == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (e) | ハリを含まない行が W1 で全セル ±0.0 | ずれ {mismNoHari[1]} 件（動いた行 {movedRows[1].Count(n => !suHariRows.Any(r => r.Name == n))}） | {(mismNoHari[1] == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (e) | ハリを含まない行が W2 で全セル ±0.0 | ずれ {mismNoHari[2]} 件（動いた行 {movedRows[2].Count(n => !suHariRows.Any(r => r.Name == n))}） | {(mismNoHari[2] == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| W1 | 動いたセル／行 | {mism[1]} セル・{movedRows[1].Count} 行{(movedRows[1].Count > 0 ? "（" + string.Join(" / ", movedRows[1]) + "）" : "")} | {(suHariKadoRows.Length == 0 && mism[1] == 0 ? "○（交わり 0 行の論理的帰結）" : "記録")} |");
        Console.WriteLine($"| W2 | 動いたセル／行 と phase0 の「動くはずの行」との突き合わせ | {mism[2]} セル・{movedRows[2].Count} 行{(movedRows[2].Count > 0 ? "（" + string.Join(" / ", movedRows[2]) + "）" : "")} ／ 予告 {suW2Rows.Length} 行（{string.Join(" / ", suW2Rows.Select(r => r.Name))}） | {(movedRows[2].All(n => suW2Rows.Any(r => r.Name == n)) ? "○（予告の外は動いていない）" : "**×**（予告の外が動いた）")} |");
        double Fifth(double[][] cc, IEnumerable<string> names)
        {
            var xs = new List<double>();
            foreach (string n in names) { int i = Array.FindIndex(suAllRows, rw => rw.Name == n); if (i >= 0) xs.Add(cc[i][suW - 1]); }
            return xs.Count == 0 ? double.NaN : xs.Average();
        }
        double f0 = Fifth(c[0], Baseline.PrimaryRows), f1 = Fifth(c[1], Baseline.PrimaryRows), f2 = Fifth(c[2], Baseline.PrimaryRows);
        int Over(double[][] cc) => Enumerable.Range(0, suAllRows.Length).Count(i => cc[i][suW - 1] > 95.0);
        int o0 = Over(c[0]), o1 = Over(c[1]), o2 = Over(c[2]);
        Console.WriteLine($"| 拒否権1 | 主判定 {Baseline.PrimaryRows.Length} 行の第五波平均 ≥ {Baseline.PrimaryFifthFloor:F1}% | W0 {f0:F2}% → W1 {f1:F2}% → W2 {f2:F2}% | {(f1 >= Baseline.PrimaryFifthFloor && f2 >= Baseline.PrimaryFifthFloor ? "立たない" : "**立つ**")} |");
        Console.WriteLine($"| 拒否権2 | `compare` {suAllRows.Length} 行で第五波 > 95% が新たに 2 行以上 | W0 {o0} → W1 {o1} → W2 {o2} 行 | {(o1 - o0 >= 2 || o2 - o0 >= 2 ? "**立つ**" : "立たない")} |");
        if (lines.Count > 0)
        {
            Console.WriteLine(); Console.WriteLine("| 行 | 波 | 左 | 右 |"); Console.WriteLine("|---|---|--:|--:|");
            foreach (string l in lines) Console.WriteLine(l);
        }
        Console.WriteLine();
        Console.WriteLine($"| 行（ハリを含む {suHariRows.Length} 行） | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var rw in suHariRows)
        {
            int i = Array.FindIndex(suAllRows, x => x.Name == rw.Name);
            for (int w = 0; w < 3; w++)
                Console.WriteLine($"| {rw.Name} | W{w} | " + string.Join(" | ", c[w][i].Select(x => $"{x:F1}%")) + $" | {c[w][i].Average():F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {suSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("suture2: 引数は phase0 / run <w> [skip] [take] / tables <w0> <w1> <w2> / nata / check。");
    return;
}
}
