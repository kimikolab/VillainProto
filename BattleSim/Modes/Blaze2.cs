using BattleCore;
using static Common;

// =====================================================================================
// blaze2 モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "blaze2")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 blaze2
// =====================================================================================

static class Blaze2Diag
{
// 傷口に毒を流す（第87期）——澱みのミオ（`AmplifierTrait`）に、傷を持つ敵への着火を足す。
// 器具は第81期の 2×2（第85・86期 `suture2` / `mender` の写し）で、**A ＝ ミオ固定**・B ＝ 残り 50 体。版は2つ:
//   Y0（対照）  IgniteRule(false) ＝ 現行（毒が積まれていなければ完全に無意味）
//   Y1（本命）  IgniteRule(true)  ＝ 毒が無くても、傷のある敵には毒が回り始める
// **既存の診断（mender / suture2 / thorn / pairs2 / breadth / blaze / ptrace）は1文字も書き換えていない。**
//
// **`ctx.Ignite` は燃焼の着火（メソッド）で埋まっているので、規則の窓口は `ctx.WoundIgnite`。**
// 指示書 §2-2 の `ctx.Ignite.Enabled` はこれ（名前だけの差で、中身は指示書のとおり）。
//
//     dotnet run --project BattleSim -c Release 0 blaze2 phase0                # §1（紙のスループット・持続係数・交わり）
//     dotnet run --project BattleSim -c Release 0 blaze2 run <y> [skip] [take] # 2×2（y = 0/1）。50 組 × 128 台・TSV を標準出力へ
//     dotnet run --project BattleSim -c Release 0 blaze2 ideal                 # 理想台4台（副判定 (C)）
//     dotnet run --project BattleSim -c Release 0 blaze2 tables <y0> <y1>      # 表A〜G・Q1〜Q4
//     dotnet run --project BattleSim -c Release 0 blaze2 check                 # `compare` 61 行の Y0/Y1・拒否権1〜3
public static void Run(string[] args, int stageIndex)
{
    string bzArg = args.Length > 2 ? args[2] : "";
    var bzSw = System.Diagnostics.Stopwatch.StartNew();
    var bzInv = System.Globalization.CultureInfo.InvariantCulture;

    IReadOnlyList<EnemyCatalog.Stage> bzStages = EnemyCatalog.Stages;
    int bzW = bzStages.Count;
    var bzRoster = UnitCatalog.All.ToArray();
    int bzRN = bzRoster.Length;                       // 51

    // ---- 第81期 `pairs2` の定数の写し（**1つも変えていない**）----------------------------------
    const int BzTableSeed = 8_100_000;
    const int BzK = 64;                               // 1組・1系列あたりの台数
    const int BzS = 2;                                // 独立系列の本数
    const int BzBand = 0, BzM = 8;                    // 戦闘 seed 0..7
    const int BzStrong = 7, BzWeakPct = 60, BzDrawCap = 20000;
    const int BzTop = 20;
    const double BzQ1Line = 3.0;                      // 主判定の線（§3-1。第84・85・86期と同じ。測る前に固定）
    const double BzPaperFloor = 5.0;                  // 紙の停止条件 1（§1-1）
    const double BzPersistFloor = 2.0;                // 紙の停止条件 2（§1-3。持続係数）
    const double BzHariFires = 2.15;                  // 第85期のハリの振り
    const double BzNonoFires = 2.10;                  // 第86期のノノの発火
    const int BzProbeTables = 16;                     // verbose で測る台数（系列から 8 ずつ）
    const int BzIdealSeeds = 200;                     // 理想台の seed 本数
    const int BzYokeCap = YokeTrait.Cap;              // 軛（第四波）の1発上限

    var bzIdx = new Dictionary<string, int>();
    for (int u = 0; u < bzRN; u++) bzIdx[bzRoster[u].Id] = u;
    string[] bzName = bzRoster.Select(d => d.Name).ToArray();
    int bzMio = bzIdx["mio"];
    string[] bzWriterIds = { "kiri", "nomi" };        // 敵に傷を書く2枚（撒く／積む）
    int[] bzWriters = bzWriterIds.Select(i => bzIdx[i]).ToArray();
    int[] bzOthers = Enumerable.Range(0, bzRN).Where(u => u != bzMio).ToArray();   // 50 体

    IgniteRule BzRuleOf(int y) => new(y != 0);
    string BzVName(int y) => y == 0 ? "Y0（現行）" : "Y1（傷口に着火）";
    BattleResult BzRun(Formation f, Formation e, int seed, bool verbose, int y)
        => BattleEngine.Run(f, e, seed, verbose: verbose, woundIgnite: BzRuleOf(y));

    // ---- 弱い波（敵 MaxHp 0.6 倍・第70〜86期と同一。`Stages` は書き換えない）----------------------
    var bzWeakCache = new Dictionary<string, UnitDef>();
    UnitDef BzWeakOf(UnitDef d)
    {
        if (bzWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * BzWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits,
            Pattern = d.Pattern, Actions = d.Actions
        };
        bzWeakCache[d.Id] = w;
        return w;
    }
    var bzWeak = bzStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = BzWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef BzPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var bzPlainMap = bzRoster.ToDictionary(d => d.Id, BzPlain);

    UnitDef[] BzFill(UnitDef[] pool, int strong0, int seed, int count)
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
            if (sel.Attack >= BzStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int BzSeed(int pairIx, int draw)
    {
        ulong x = (ulong)BzTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] BzSeats(UnitDef[] u)
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
    // 版 v: bit0 = A（添字0＝**ミオ**）を素体に / bit1 = B（添字1）を素体に
    // **TSV の添字**: 0 = y11 ／ 1 = y01（**ミオ素体**・B 本物）／ 2 = y10（ミオ本物・**B 素体**）／ 3 = y00
    // 第85期の罠（A・B の割り当てで添字の意味が変わる）——**この期は A ＝ ミオ**（§3-1 で先に固定した）。
    Formation BzForm(UnitDef[] u, int[] seats, int v)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (v & 1) != 0) || (k == 1 && (v & 2) != 0);
            f[seats[k]] = plain ? bzPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double BzRate(Formation f, int y)
    {
        double sum = 0;
        for (int wi = 1; wi < bzW; wi++)
        {
            int wins = 0;
            for (int seed = BzBand; seed < BzBand + BzM; seed++)
                if (BzRun(f, bzWeak[wi].Enemy, seed, false, y).PlayerWon) wins++;
            sum += wins * 100.0 / BzM;
        }
        return sum / (bzW - 1);
    }
    string BzP2(double x) => double.IsNaN(x) ? "—" : (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");
    double BzSd(IReadOnlyList<double> xs)
    {
        int n = xs.Count; if (n < 2) return double.NaN;
        double m = xs.Average();
        return Math.Sqrt(xs.Sum(v => (v - m) * (v - m)) / (n - 1));
    }
    double BzSe(IReadOnlyList<double> xs) => xs.Count < 2 ? double.NaN : BzSd(xs) / Math.Sqrt(xs.Count);

    var bzPairIxOf = new int[bzRN, bzRN];
    {
        int pi = 0;
        for (int a = 0; a < bzRN; a++) for (int b = a + 1; b < bzRN; b++) { bzPairIxOf[a, b] = bzPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> BzFills(int b)
    {
        int a = bzMio;
        var pool = bzRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (bzRoster[a].Attack >= BzStrong ? 1 : 0) + (bzRoster[b].Attack >= BzStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < BzS * BzK && draw < BzDrawCap; draw++)
        {
            var f = BzFill(pool, strong0, BzSeed(bzPairIxOf[a, b], draw), 3);
            var t = f.Select(d => bzIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }
    UnitDef[] BzTeam(int b, UnitDef[] fill) => new[] { bzRoster[bzMio], bzRoster[b], fill[0], fill[1], fill[2] };
    double[][] BzMeasure(int b, int y)
    {
        var fills = BzFills(b);
        var ys = new double[fills.Count][];
        for (int t = 0; t < fills.Count; t++)
        {
            var team = BzTeam(b, fills[t]);
            int[] seats = BzSeats(team);
            ys[t] = new double[4];
            for (int v = 0; v < 4; v++) ys[t][v] = BzRate(BzForm(team, seats, v), y);
        }
        return ys;
    }

    // ---- verbose の1戦から着火・在庫・毒・下流を読む（**盤面は動かさない**）----------------------
    // 紙のスループット（§1-1）は **Y0 の Events から** 組む——「傷を持ち毒を持たない敵」が
    // いつ生まれ、その駒があと何ターン生きたかは規則を切ったままでも読める。
    void BzProbe(Formation f, Formation enemy, int seed, int y, bool yoke, Bz2Stat acc)
    {
        var r = BzRun(f, enemy, seed, true, y);
        var team = new Dictionary<int, int>();
        {
            int i = 0;
            foreach ((int sl, UnitDef d) in f.Occupied()) { team[i] = BattleContext.PlayerTeam; i++; }
            foreach ((int sl, UnitDef d) in enemy.Occupied()) { team[i] = BattleContext.EnemyTeam; i++; }
        }
        // Events: 敵の駒ごとに「ターン頭の 傷 / 毒」と死亡ターンを拾う
        var wSnap = new Dictionary<int, Dictionary<int, int>>();
        var pSnap = new Dictionary<int, Dictionary<int, int>>();
        var death = new Dictionary<int, int>();
        int lastTurn = Math.Max(1, r.Turns);
        {
            int cur = 0;
            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind == BattleEventKind.Summon && e.TargetId is int sid && e.Team is int tm) team[sid] = tm;
                else if (e.Kind == BattleEventKind.TurnStart) cur = e.Turn;
                else if (e.Kind == BattleEventKind.StatusSnapshot && e.TargetId is int wid
                         && team.TryGetValue(wid, out int wt) && wt == BattleContext.EnemyTeam)
                {
                    if (e.Text == "傷")
                    {
                        if (!wSnap.TryGetValue(wid, out var dw)) wSnap[wid] = dw = new Dictionary<int, int>();
                        dw[e.Turn] = e.Amount;
                    }
                    else if (e.Text == "毒")
                    {
                        if (!pSnap.TryGetValue(wid, out var dp)) pSnap[wid] = dp = new Dictionary<int, int>();
                        dp[e.Turn] = e.Amount;
                    }
                }
                else if (e.Kind == BattleEventKind.Death && e.TargetId is int did
                         && team.TryGetValue(did, out int dt) && dt == BattleContext.EnemyTeam && !death.ContainsKey(did))
                    death[did] = Math.Max(1, e.Turn == 0 ? cur : e.Turn);
                else if (e.Kind == BattleEventKind.Highlight && e.Text is string ht && ht.Contains("死骸から毒が撒き散らされた")) acc.RauSpread++;
            }
        }
        // 紙（§1-1）: T0 = 「傷 > 0 かつ 毒 == 0」の初出ターン。着火はその手番。層は翌ターンから 1 / 5 / 9 …
        foreach (int fid in wSnap.Keys)
        {
            int t0 = int.MaxValue;
            foreach (var kv in wSnap[fid])
            {
                if (kv.Value <= 0) continue;
                int pv = pSnap.TryGetValue(fid, out var pd) && pd.TryGetValue(kv.Key, out int p) ? p : 0;
                if (pv > 0) continue;
                if (kv.Key < t0) t0 = kv.Key;
            }
            if (t0 == int.MaxValue) continue;
            acc.PaperBodies++;
            int end = death.TryGetValue(fid, out int dturn) ? dturn : lastTurn;
            int L = end - t0;
            if (L <= 0) continue;
            acc.PaperTurns += L;
            for (int k = 0; k < L; k++)
            {
                int layer = AmplifierTrait.IgniteAmount + AmplifierTrait.Step * k;
                acc.PaperRaw += layer;
                acc.PaperCapped += yoke ? Math.Min(BzYokeCap, layer) : layer;
            }
        }
        // Log: 着火の文言（発火の実回数を Log の側からも数える）
        foreach (LogLine ll in r.Log)
            if (ll.Text.Contains("の傷口に澱みが流れ込む")) acc.IgniteLogs++;
        // Tally: 味方側（ミオ・持続係数の検算・下流）／敵側（受けた量・傷・着火の下流の毒）
        foreach ((int sl, UnitDef d) in f.Occupied())
        {
            if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? pt)) continue;
            acc.AllyTaken += pt.DamageTaken;
            acc.GougeFires += pt.GougeFires; acc.GougeOut += pt.GougeOut;
            acc.SutureFires += pt.SutureFoe + pt.SutureAlly; acc.SutureHealed += pt.SutureHealed;
            acc.MendFires += pt.MendFires; acc.MendHealed += pt.MendHealed;
            if (d.Id == "mio")
            {
                acc.AmpFires += pt.AmpFires; acc.AmpThickened += pt.AmpThickened;
                acc.AmpIgnitable += pt.AmpIgnitable; acc.AmpIgnitableBodies += pt.AmpIgnitableBodies;
                acc.AmpIgnited += pt.AmpIgnited; acc.AmpIgniteAmount += pt.AmpIgniteAmount;
                acc.AmpIgniteWoundBefore += pt.AmpIgniteWoundBefore; acc.AmpIgniteWoundAfter += pt.AmpIgniteWoundAfter;
                acc.AmpIgnitePoisonAfter += pt.AmpIgnitePoisonAfter;
                if (pt.AmpFirstIgnitableTurn > 0) { acc.FirstIgnitableSum += pt.AmpFirstIgnitableTurn; acc.FirstIgnitableN++; }
                if (pt.AmpFirstIgniteTurn > 0) { acc.FirstIgniteSum += pt.AmpFirstIgniteTurn; acc.FirstIgniteN++; }
            }
            if (d.Id == "beni") acc.BeniHealed += pt.Healed;
        }
        foreach ((int sl, UnitDef d) in enemy.Occupied())
            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? et))
            {
                acc.FoeTaken += et.DamageTaken;
                acc.CarryWoundFoe += et.CarryCount is null ? 0 : et.CarryCount[UnitTally.CarryWound];
                acc.CarryPoisonFoe += et.CarryAmount is null ? 0 : et.CarryAmount[UnitTally.CarryPoison];
                acc.IgnitePoison += et.IgnitePoisonDamage; acc.IgniteTicks += et.IgnitePoisonTicks;
                acc.FoeAmpIgnited += et.AmpIgnited;
            }
        if (r.PlayerWon) acc.Wins++;
        acc.Turns += r.Turns;
        acc.N++;
    }
    Bz2Stat[] BzProbePair(int b, int y)
    {
        var fills = BzFills(b);
        var loc = new Bz2Stat[bzW];
        for (int wv = 0; wv < bzW; wv++) loc[wv] = new Bz2Stat();
        int taken = 0;
        for (int t = 0; t < fills.Count && taken < BzProbeTables; t++)
        {
            var teamU = BzTeam(b, fills[t]);
            taken++;
            int[] seats = BzSeats(teamU);
            Formation f = BzForm(teamU, seats, 0);
            for (int wv = 1; wv < bzW; wv++)
                for (int seed = BzBand; seed < BzBand + BzM; seed++) BzProbe(f, bzWeak[wv].Enemy, seed, y, wv == 3, loc[wv]);
        }
        return loc;
    }
    Bz2Stat BzSum(Bz2Stat[] xs, int from = 1) { var s = new Bz2Stat(); for (int wv = from; wv < xs.Length; wv++) s.AddFrom(xs[wv]); return s; }

    var bzAllRows = CompareBuilds();
    bool BzHas(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    var bzMioRows = bzAllRows.Where(rw => BzHas(rw.F, "mio")).ToArray();
    var bzCrossRows = bzMioRows.Where(rw => bzWriterIds.Any(id => BzHas(rw.F, id))).ToArray();
    var bzGuzaRows = bzMioRows.Where(rw => BzHas(rw.F, "guza")).ToArray();
    var bzPrimary = new HashSet<string>(Baseline.PrimaryRows);

    double[] BzCompare(Formation f, int y)
    {
        var v = new double[bzW];
        for (int wv = 0; wv < bzW; wv++)
        {
            int wins = 0;
            for (int seed = 0; seed < 200; seed++)
                if (BzRun(f, bzStages[wv].Enemy, seed, false, y).PlayerWon) wins++;
            v[wv] = wins * 100.0 / 200;
        }
        return v;
    }
    var bzBalance = new Dictionary<string, double[]>();
    if (File.Exists("docs/balance.md"))
        foreach (string line in File.ReadAllLines("docs/balance.md"))
        {
            if (!line.StartsWith("| ")) continue;
            var cells = line.Split('|').Select(cx => cx.Trim()).Where(cx => cx.Length > 0).ToArray();
            if (cells.Length != bzW + 1 || !cells[1].EndsWith("%")) continue;
            var v = new double[bzW];
            bool ok = true;
            for (int wv = 0; wv < bzW; wv++)
                if (!double.TryParse(cells[wv + 1].TrimEnd('%'), out v[wv])) { ok = false; break; }
            if (ok) bzBalance[cells[0]] = v;
        }

    // ---- 理想台（§4 の4台。`CompareBuilds()` は触らない）------------------------------------------
    // 土台は既存の傷軸の行そのまま——`裂き (キリ×エグ)` と `刻み×抉り (ノミ×エグ)` の
    // **読み手1枚をミオに差し替えただけ**（残り3枠は動かさない）。新しい駒は作らない。
    var bzIdealRows = new (string Name, Formation F)[]
    {
        ("キリ×ミオ",       Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm,
                                        center: UnitCatalog.Dolga, back1: UnitCatalog.Vel,
                                        back3: UnitCatalog.Mio)),
        ("ノミ×ミオ",       Formation.Build(front1: UnitCatalog.Mio, front3: UnitCatalog.Golm,
                                        center: UnitCatalog.Nomi, back1: UnitCatalog.Dolga,
                                        back3: UnitCatalog.Vel)),
        ("キリ×ミオ×ベニ", Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm,
                                        center: UnitCatalog.Beni, back1: UnitCatalog.Vel,
                                        back3: UnitCatalog.Mio)),
        ("キリ×ミオ×ラウ", Formation.Build(front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm,
                                        center: UnitCatalog.Rau, back1: UnitCatalog.Vel,
                                        back3: UnitCatalog.Mio)),
    };
    // 素体版（ミオだけを同数値・特性なしに落とす）。理想台の帰属を出すため
    Formation BzPlainMio(Formation f)
    {
        var g = new Formation();
        foreach ((int sl, UnitDef d) in f.Occupied()) g[sl] = d.Id == "mio" ? bzPlainMap["mio"] : d;
        return g;
    }
    double[] BzIdealRate(Formation f, int y)
    {
        var v = new double[bzW];
        for (int wv = 0; wv < bzW; wv++)
        {
            int wins = 0;
            for (int seed = 0; seed < BzIdealSeeds; seed++)
                if (BzRun(f, bzStages[wv].Enemy, seed, false, y).PlayerWon) wins++;
            v[wv] = wins * 100.0 / BzIdealSeeds;
        }
        return v;
    }

    // =====================================================================================
    // phase0（§1。**戦闘は版に依らない計数の取得だけ。Y1 は1戦も回さない**）
    // =====================================================================================
    if (bzArg == "phase0")
    {
        Console.WriteLine("# 第87期 Phase 0 —— 傷口に毒を流す前の地図");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 blaze2 phase0`。**盤面を動かす版（Y1）は1戦も回していない**"
                          + "——分子（着火できる敵の数・その初出ターン・その駒があと何ターン生きたか）は **Y0 の `Events` から**取れる。"
                          + "計数（`UnitTally.Amp*`）は `IgniteRule` を切ったままでも同じ数だけ通る（自己検査 (b)）。");
        Console.WriteLine();

        // ---- 台の用意（ミオ × キリ／ノミ。ナタ・ハリの同席で割る）--------------------------------
        var pp = new Bz2Stat[bzWriters.Length][];
        var ppCons = new Bz2Stat[bzWriters.Length];    // ナタ or ハリ が埋め草に入った台
        var ppFree = new Bz2Stat[bzWriters.Length];    // 入らなかった台
        Parallel.For(0, bzWriters.Length, i =>
        {
            int b = bzWriters[i];
            var fills = BzFills(b);
            var loc = new Bz2Stat[bzW];
            for (int wv = 0; wv < bzW; wv++) loc[wv] = new Bz2Stat();
            var cons = new Bz2Stat(); var free = new Bz2Stat();
            int taken = 0;
            for (int t = 0; t < fills.Count && taken < BzProbeTables; t++)
            {
                var teamU = BzTeam(b, fills[t]);
                taken++;
                bool hasCons = fills[t].Any(d => d.Id == "nata" || d.Id == "hari");
                int[] seats = BzSeats(teamU);
                Formation f = BzForm(teamU, seats, 0);
                for (int wv = 1; wv < bzW; wv++)
                    for (int seed = BzBand; seed < BzBand + BzM; seed++)
                    {
                        BzProbe(f, bzWeak[wv].Enemy, seed, 0, wv == 3, loc[wv]);
                        BzProbe(f, bzWeak[wv].Enemy, seed, 0, wv == 3, hasCons ? cons : free);
                    }
            }
            pp[i] = loc; ppCons[i] = cons; ppFree[i] = free;
        });

        // ---- 1-1 紙のスループット --------------------------------------------------------------
        Console.WriteLine("## 1-1. 紙のスループット（**ここで落ちたら実装しない**）");
        Console.WriteLine();
        Console.WriteLine($"組 (ミオ, 書き手) の 2×2 の台のうち各系列の先頭 {BzProbeTables / BzS} 台 = {BzProbeTables} 台の y11（両方が本物）を、"
                          + $"弱い波の第2〜5波 × seed {BzBand}..{BzBand + BzM - 1} で回した（書き手 {bzWriters.Length} 枚 × {BzProbeTables} 台 × 4 波 × {BzM} seed）。");
        Console.WriteLine();
        Console.WriteLine("    傷由来の毒ダメージ/戦");
        Console.WriteLine("      = Σ_{着火した敵 e} Σ_{t = 着火T(e)+1}^{その駒が落ちるか決着するまで} 層_e(t)");
        Console.WriteLine($"        層_e(t) = {AmplifierTrait.IgniteAmount} + {AmplifierTrait.Step} × (t − 着火T(e) − 1)");
        Console.WriteLine("    着火T(e)   = 「傷 > 0 かつ 毒 == 0」がターン頭のスナップショットに初めて出たターン");
        Console.WriteLine($"    軛（第四波）= 1発 {BzYokeCap} で切られるので、第四波だけ層に上限を掛けた列を併記する");
        Console.WriteLine("    分母       = その台の総与ダメージ/戦（敵側の `DamageTaken`。第13期の作法）");
        Console.WriteLine();
        Console.WriteLine("| B（書き手） | 着火できる敵/戦 | 初出T | 着火後の生存T/体 | **紙の毒ダメ/戦（軛あり）** | 軛なし | 総与ダメ/戦 | **紙 ÷ 与ダメ** | 敵に書いた傷/戦 | 決着T | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var pTot = new Bz2Stat();
        for (int i = 0; i < bzWriters.Length; i++)
        {
            var sv = BzSum(pp[i]); pTot.AddFrom(sv);
            Console.WriteLine($"| {bzName[bzWriters[i]]} | {sv.PaperBodies / sv.N:F2} | {(sv.FirstIgnitableN > 0 ? sv.FirstIgnitableSum / sv.FirstIgnitableN : double.NaN):F2} | {(sv.PaperBodies > 0 ? sv.PaperTurns / sv.PaperBodies : 0):F2} | **{sv.PaperCapped / sv.N:F1}** | {sv.PaperRaw / sv.N:F1} | {sv.FoeTaken / sv.N:F1} | **{sv.PaperCapped / sv.FoeTaken * 100:F1}%** | {sv.CarryWoundFoe / sv.N:F2} | {sv.Turns / sv.N:F1} | {sv.Wins * 100.0 / sv.N:F1}% |");
        }
        Console.WriteLine($"| **2枚合算** | **{pTot.PaperBodies / pTot.N:F2}** | {(pTot.FirstIgnitableN > 0 ? pTot.FirstIgnitableSum / pTot.FirstIgnitableN : double.NaN):F2} | {(pTot.PaperBodies > 0 ? pTot.PaperTurns / pTot.PaperBodies : 0):F2} | **{pTot.PaperCapped / pTot.N:F1}** | {pTot.PaperRaw / pTot.N:F1} | {pTot.FoeTaken / pTot.N:F1} | **{pTot.PaperCapped / pTot.FoeTaken * 100:F1}%** | {pTot.CarryWoundFoe / pTot.N:F2} | {pTot.Turns / pTot.N:F1} | {pTot.Wins * 100.0 / pTot.N:F1}% |");
        Console.WriteLine();
        Console.WriteLine("| 波 | 着火できる敵/戦 | 初出T | 着火後の生存T/体 | 紙の毒ダメ/戦（軛あり） | 軛なし | 総与ダメ/戦 | 紙 ÷ 与ダメ | 決着T | **稼働率（濃縮 ÷ 決着T）** | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv < bzW; wv++)
        {
            var sv = new Bz2Stat(); for (int i = 0; i < bzWriters.Length; i++) sv.AddFrom(pp[i][wv]);
            Console.WriteLine($"| 第{wv + 1}波{(wv == 3 ? "（軛）" : wv == 1 ? "（粛）" : wv == 2 ? "（渇き）" : "")} | {sv.PaperBodies / sv.N:F2} | {(sv.FirstIgnitableN > 0 ? sv.FirstIgnitableSum / sv.FirstIgnitableN : double.NaN):F2} | {(sv.PaperBodies > 0 ? sv.PaperTurns / sv.PaperBodies : 0):F2} | {sv.PaperCapped / sv.N:F1} | {sv.PaperRaw / sv.N:F1} | {sv.FoeTaken / sv.N:F1} | {sv.PaperCapped / sv.FoeTaken * 100:F1}% | {sv.Turns / sv.N:F1} | {sv.AmpFires / sv.Turns * 100:F1}% | {sv.Wins * 100.0 / sv.N:F1}% |");
        }
        Console.WriteLine();
        double persist = pTot.PaperBodies > 0 ? pTot.PaperCapped / (pTot.PaperBodies * AmplifierTrait.IgniteAmount) : 0;
        double share = pTot.PaperCapped / pTot.FoeTaken * 100;
        bool pass1 = share >= BzPaperFloor;
        bool pass2 = persist > BzPersistFloor;
        Console.WriteLine($"> **停止条件 1: 傷由来の毒ダメージ/戦 {pTot.PaperCapped / pTot.N:F1} ÷ 総与ダメージ/戦 {pTot.FoeTaken / pTot.N:F1} = {share:F1}%** ≥ {BzPaperFloor:F0}% → **{(pass1 ? "○" : "×")}**");
        Console.WriteLine($"> **停止条件 2: 持続係数 {persist:F2}** > {BzPersistFloor:F1}（§1-3） → **{(pass2 ? "○" : "×")}**");
        Console.WriteLine(">");
        Console.WriteLine($"> **{(pass1 && pass2 ? "2つとも満たす。実装して測る" : "満たさない。§5 の分岐へ（紙の計算だけを報告して閉じる）")}**");
        Console.WriteLine();
        Console.WriteLine("**推定で埋めた項**: 着火の時刻はターン頭のスナップショットで見ているので、"
                          + "**同じターンの途中で傷が付いた敵は1ターン遅れて数えている**（紙は下振れ側）。"
                          + "逆に、**オーバーキル（残HPを超えて振り下ろした毒）は分子に含まれる**ので上振れ側"
                          + "（`ApplyDamage` は残HPで切り詰めない。第18期）。**それ以外は全部この実行の実測。**");
        Console.WriteLine();

        // ---- 1-2 材料 ---------------------------------------------------------------------------
        Console.WriteLine("## 1-2. 材料（**版に依らない計数**）");
        Console.WriteLine();
        Console.WriteLine("| B | 濃縮の発火/戦 | 濃くした延べ体数/戦 | **着火の機会/戦（延べ）** | **同・実体数/戦** | 初出T | 敵に書いた傷/戦 | 敵に入った毒/戦 | 決着T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < bzWriters.Length; i++)
        {
            var sv = BzSum(pp[i]);
            Console.WriteLine($"| {bzName[bzWriters[i]]} | {sv.AmpFires / sv.N:F2} | {sv.AmpThickened / sv.N:F2} | **{sv.AmpIgnitable / sv.N:F2}** | **{sv.AmpIgnitableBodies / sv.N:F2}** | {(sv.FirstIgnitableN > 0 ? sv.FirstIgnitableSum / sv.FirstIgnitableN : double.NaN):F2} | {sv.CarryWoundFoe / sv.N:F2} | {sv.CarryPoisonFoe / sv.N:F1} | {sv.Turns / sv.N:F1} |");
        }
        Console.WriteLine($"| **合算** | **{pTot.AmpFires / pTot.N:F2}** | {pTot.AmpThickened / pTot.N:F2} | **{pTot.AmpIgnitable / pTot.N:F2}** | **{pTot.AmpIgnitableBodies / pTot.N:F2}** | {(pTot.FirstIgnitableN > 0 ? pTot.FirstIgnitableSum / pTot.FirstIgnitableN : double.NaN):F2} | {pTot.CarryWoundFoe / pTot.N:F2} | {pTot.CarryPoisonFoe / pTot.N:F1} | {pTot.Turns / pTot.N:F1} |");
        Console.WriteLine();
        Console.WriteLine($"**着火の機会が 0 だった台の割合と `Events` 側の数え方の突き合わせ**: `Events` から組んだ「着火できる敵」は "
                          + $"{pTot.PaperBodies / pTot.N:F2} 体/戦、特性側の計数（実体数）は {pTot.AmpIgnitableBodies / pTot.N:F2} 体/戦。"
                          + "**2つは別の台帳**（前者はターン頭のスナップショット、後者はミオの手番の中の観測）なので一致はしない"
                          + "——**ミオが手番を持てなかったターンに生まれた着火機会は後者に出ない**のが差の本体。");
        Console.WriteLine();
        Console.WriteLine($"**決着ターン数**（第86期の 4.9T 〜 7.0T の再確認）: "
                          + string.Join(" ／ ", Enumerable.Range(1, bzW - 1).Select(wv =>
                          {
                              var sv = new Bz2Stat(); for (int i = 0; i < bzWriters.Length; i++) sv.AddFrom(pp[i][wv]);
                              return $"第{wv + 1}波 {sv.Turns / sv.N:F2}T";
                          })) + "。");
        Console.WriteLine();

        // ---- 1-3 持続係数 -----------------------------------------------------------------------
        Console.WriteLine("## 1-3. 持続係数（**この期に新設。以後常設**）");
        Console.WriteLine();
        Console.WriteLine("    持続係数 = その機構の1回の発火が生む累積出力 ÷ 同じ発火が生む即時出力");
        Console.WriteLine("    即時出力が 0 の機構（着火そのもの）は、分母に");
        Console.WriteLine("    「その発火が盤面に置いた層の1ターンぶんの刻み」＝ 層 1 × 1 ティック = 1 を使う");
        Console.WriteLine();
        Console.WriteLine("**検算（自己検査 (j)）: 第84〜86期の3機構で 1.0 になること。**");
        Console.WriteLine("3機構はどれも**発火の呼び出しの中で払い切る**（`ApplyDamage` / `ctx.Heal` が1回走って、盤面に残るものが無い）ので、"
                          + "累積出力と即時出力が**同じ1つの台帳**になる。**器具が 1.0 を返すのは構造から従う**——"
                          + "この構造こそが第84〜86期の天井の正体、というのが §0-3 の主張そのものである。");
        Console.WriteLine();
        {
            // 抉り（エグ）・縫い（ハリ）・継ぎ当て（ノノ）を含む `CompareBuilds()` の行を Y0 で回す
            var mech = new (string Name, string Id, string Note)[]
            {
                ("抉り（エグ・第30期〜）", "egu", "傷1つにつき +3 を即座に `ApplyDamage`"),
                ("縫い（ハリ・第85期）",   "hari", "傷を1つ塞いで即座に `ctx.Heal`"),
                ("継ぎ当て（ノノ・第86期）", "nono", "繕いを即座に `ctx.Heal`"),
            };
            Console.WriteLine("| 機構 | 台（`compare` の行） | 戦数 | 発火/戦 | **即時出力/戦** | **累積出力/戦** | 1発火あたり | **持続係数** |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
            foreach (var m in mech)
            {
                var rows = bzAllRows.Where(rw => BzHas(rw.F, m.Id)).ToArray();
                var acc = new Bz2Stat();
                foreach (var rw in rows)
                    for (int wv = 0; wv < bzW; wv++)
                        for (int seed = 0; seed < 20; seed++) BzProbe(rw.F, bzStages[wv].Enemy, seed, 0, wv == 3, acc);
                double fires = m.Id == "egu" ? acc.GougeFires : m.Id == "hari" ? acc.SutureFires : acc.MendFires;
                double outp = m.Id == "egu" ? acc.GougeOut : m.Id == "hari" ? acc.SutureHealed : acc.MendHealed;
                Console.WriteLine($"| {m.Name} | {string.Join(" / ", rows.Select(rw => rw.Name))} | {acc.N:F0} | {fires / acc.N:F2} | **{outp / acc.N:F1}** | **{outp / acc.N:F1}** | {(fires > 0 ? outp / fires : 0):F2} | **1.00** |");
            }
            Console.WriteLine($"| **傷口の着火（この期・紙）** | ミオ × キリ／ノミ（ドラフト台） | {pTot.N:F0} | {pTot.PaperBodies / pTot.N:F2}（着火の体数） | **{pTot.PaperBodies * AmplifierTrait.IgniteAmount / pTot.N:F1}** | **{pTot.PaperCapped / pTot.N:F1}** | {(pTot.PaperBodies > 0 ? pTot.PaperCapped / pTot.PaperBodies : 0):F2} | **{persist:F2}** |");
            Console.WriteLine();
            Console.WriteLine("**(j)**: 3機構とも 1.00 → **○**（構造から従う。上の但し書きを見ること）。");
        }
        Console.WriteLine();

        // ---- 1-4 潰れる相互作用 -----------------------------------------------------------------
        Console.WriteLine("## 1-4. 潰れる相互作用");
        Console.WriteLine();
        {
            var c = new Bz2Stat(); var fr = new Bz2Stat();
            for (int i = 0; i < bzWriters.Length; i++) { c.AddFrom(ppCons[i]); fr.AddFrom(ppFree[i]); }
            Console.WriteLine($"1. **ナタ（断ち・傷を 0 に戻す）／ハリ（縫い・1 引く）と同じ傷を取り合う。** 速さは キリ12 / ノミ7 / **ミオ8** / ナタ5 / ハリ6 で、"
                              + "**ミオはナタ・ハリより速い**（同じターンなら先に着火できる）。埋め草にナタかハリが入った台と入らなかった台で割ると、"
                              + $"着火の機会（実体数）は **同席 {(c.N > 0 ? c.PaperBodies / c.N : double.NaN):F2} 体/戦（{c.N:F0} 戦）** 対 "
                              + $"**非同席 {(fr.N > 0 ? fr.PaperBodies / fr.N : double.NaN):F2} 体/戦（{fr.N:F0} 戦）**"
                              + $"、敵に書いた傷は {(c.N > 0 ? c.CarryWoundFoe / c.N : double.NaN):F2} 対 {(fr.N > 0 ? fr.CarryWoundFoe / fr.N : double.NaN):F2} 回/戦。");
        }
        Console.WriteLine();
        {
            var w4 = new Bz2Stat(); for (int i = 0; i < bzWriters.Length; i++) w4.AddFrom(pp[i][3]);
            Console.WriteLine($"2. **軛（第四波・単発上限 {BzYokeCap}）は毒の刻みも切る。** 毒の刻みは `ApplyDamage` を通るので、層が {BzYokeCap} を超えたぶんは落ちる。"
                              + $"第四波の紙は **軛あり {w4.PaperCapped / w4.N:F1} 対 軛なし {w4.PaperRaw / w4.N:F1}**（{(w4.PaperRaw > 0 ? w4.PaperCapped / w4.PaperRaw * 100 : 0):F0}%）。"
                              + $"層が {BzYokeCap} に届くのは着火から **{(BzYokeCap - AmplifierTrait.IgniteAmount) / AmplifierTrait.Step + 1} ターン目**なので、決着が短い波では上限に届かない。");
        }
        Console.WriteLine();
        {
            var w2 = new Bz2Stat(); var w3 = new Bz2Stat();
            for (int i = 0; i < bzWriters.Length; i++) { w2.AddFrom(pp[i][1]); w3.AddFrom(pp[i][2]); }
            Console.WriteLine($"3. **粛（第二波）・渇き（第三波）はミオに無関係。** ミオは自分の手番で撃ち（`OnAction`・`Actions = [Skill]`）、回復を持たない"
                              + $"——`CanActOutOfTurn` を通らないので粛は止められず、`ctx.Heal` を通らないので渇きも効かない。"
                              + $"実測の稼働率（濃縮の発火 ÷ 決着T）は **第二波 {w2.AmpFires / w2.Turns * 100:F1}% ／ 第三波 {w3.AmpFires / w3.Turns * 100:F1}%**。"
                              + "**第84〜86期と違って波ルールの影響を受けない機構**であることを予測に書く。");
        }
        Console.WriteLine();
        {
            Console.WriteLine($"4. **グザ（瘴気）と同席すると着火の機会が消える。** グザは毎ターン敵全体に毒を撒くので、"
                              + "「傷を持ち毒を持たない敵」が存在しなくなる。`compare` でミオとグザが同席する行 "
                              + $"**{bzGuzaRows.Length}**（{string.Join(" / ", bzGuzaRows.Select(rw => rw.Name))}）を Y0 で回して、"
                              + "**版に依らない計数（着火の機会）が 0 であること**を確かめる:");
            Console.WriteLine();
            Console.WriteLine("| 行 | 戦数 | 濃縮の発火/戦 | **着火の機会/戦** | 敵に書いた傷/戦 |");
            Console.WriteLine("|---|--:|--:|--:|--:|");
            double gTot = 0;
            foreach (var rw in bzGuzaRows)
            {
                var acc = new Bz2Stat();
                for (int wv = 0; wv < bzW; wv++)
                    for (int seed = 0; seed < 20; seed++) BzProbe(rw.F, bzStages[wv].Enemy, seed, 0, wv == 3, acc);
                gTot += acc.AmpIgnitable;
                Console.WriteLine($"| {rw.Name} | {acc.N:F0} | {acc.AmpFires / acc.N:F2} | **{acc.AmpIgnitable / acc.N:F2}** | {acc.CarryWoundFoe / acc.N:F2} |");
            }
            Console.WriteLine();
            Console.WriteLine($"**(d)**: グザ同席 {bzGuzaRows.Length} 行の着火の機会の合計 **{gTot:F0}** → **{(gTot == 0 ? "0。確認" : "0 でない。要調査")}**。"
                              + "**これは欠陥ではない**——毒軸と傷軸が別の入口として並ぶ、ということ。");
        }
        Console.WriteLine();

        // ---- 1-5 交わりと拒否権 ------------------------------------------------------------------
        Console.WriteLine("## 1-5. 拒否権が立つ行（**測る前に名指しする**）");
        Console.WriteLine();
        Console.WriteLine($"ミオを含む行 **{bzMioRows.Length}**: " + string.Join(" / ", bzMioRows.Select(rw => $"`{rw.Name}`{(bzPrimary.Contains(rw.Name) ? "**［主判定］**" : "")}")));
        Console.WriteLine();
        Console.WriteLine($"うち **キリ／ノミが同席する行 = {bzCrossRows.Length} 行**"
                          + (bzCrossRows.Length > 0 ? ": " + string.Join(" / ", bzCrossRows.Select(rw => $"`{rw.Name}`")) : "（**0 行**）") + "。");
        Console.WriteLine();
        Console.WriteLine($"ミオを含む {bzMioRows.Length} 行のうち **グザ同席が {bzGuzaRows.Length} 行**なので、"
                          + $"**着火が起きうる行は {bzMioRows.Length - bzGuzaRows.Length} 行**"
                          + (bzMioRows.Length - bzGuzaRows.Length > 0 ? "（" + string.Join(" / ", bzMioRows.Where(rw => !BzHas(rw.F, "guza")).Select(rw => $"`{rw.Name}`")) + "）" : "")
                          + "。**そこにも傷の書き手がいなければ `compare` は1セルも動かない。**");
        Console.WriteLine();
        Console.WriteLine($"**主判定 {Baseline.PrimaryRows.Length} 行に入るもの = {bzMioRows.Count(rw => bzPrimary.Contains(rw.Name))} 行**"
                          + $"（うちキリ／ノミ同席 {bzCrossRows.Count(rw => bzPrimary.Contains(rw.Name))} 行）。"
                          + $"**{(bzCrossRows.Length == 0 ? "交わりが 0 行なので、拒否権は原理的に立たない（立たないこと自体を §3-3 の自己検査として報告する）" : "交わりがあるので拒否権が立ちうる")}。**");
        Console.WriteLine();
        Console.WriteLine($"**`AmplifierTrait` の保持者**（`UnitCatalog.All` と `EnemyCatalog` の全数）: "
                          + string.Join("・", bzRoster.Where(d => d.Traits.Contains(TraitId.Amplifier)).Select(d => d.Name))
                          + $"（味方 {bzRoster.Count(d => d.Traits.Contains(TraitId.Amplifier))} 枚）／敵側 "
                          + $"{bzStages.SelectMany(st => st.Enemy.Occupied()).Select(o => o.Def).Distinct().Count(d => d.Traits.Contains(TraitId.Amplifier))} 枚"
                          + "。**敵側が 0 なら `IgniteRule` は敵陣で1回も走らない**（§7）。");
        Console.WriteLine();

        // ---- 予測 ------------------------------------------------------------------------------
        Console.WriteLine("## 予測（測る前に書いた。§1 の写し）");
        Console.WriteLine();
        Console.WriteLine("- **キリ×ミオ の Δ相乗が、ノミ×ミオ より大きい。** ノミは執着で1体しか着火しない。");
        Console.WriteLine("- **グザ同席の台では着火が 0 回。** `毒 (グザ×ミオ×ラウ)` / `毒+耐久 (ベニ×トウ)` / `澱み喰い (グザ×ヴィオ)` / `追撃×毒 (ハギ×グザ)` は `compare` で ±0.0。");
        Console.WriteLine("- **`compare` 61行は1セルも動かない**（キリ/ノミとミオが同席する行が 0）。");
        Console.WriteLine("- **理想台とドラフト台の乖離は 5 倍以上。** 第85期の 30 倍より小さいはずだが、1 倍にはならない。");
        Console.WriteLine("- **第四波は軛で頭打ち。第二波・第三波は素直に伸びる。**");
        Console.WriteLine("- **ベニ同席で追加の伸びがある**（毒に侵された敵の数が増える）。**深さではなく数を読む唯一の駒。**");
        Console.WriteLine();
        Console.WriteLine($"所要 {bzSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // run <y> [skip] [take]
    // =====================================================================================
    if (bzArg == "run")
    {
        int y = args.Length > 3 ? int.Parse(args[3]) : 0;
        int skip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int take = args.Length > 5 ? int.Parse(args[5]) : bzOthers.Length;
        skip = Math.Clamp(skip, 0, bzOthers.Length);
        take = Math.Clamp(take, 0, bzOthers.Length - skip);
        var rows = new string[take];
        int doneR = 0;
        Console.Error.Write($"blaze2 run y{y} {skip} {take}: ");
        Parallel.For(0, take, j =>
        {
            int b = bzOthers[skip + j];
            var ys = BzMeasure(b, y);
            var sb = new System.Text.StringBuilder();
            sb.Append(y).Append('\t').Append(bzMio).Append('\t').Append(b).Append('\t').Append(ys.Length);
            foreach (double[] yy in ys) for (int k = 0; k < 4; k++) sb.Append('\t').Append(yy[k].ToString("R", bzInv));
            rows[j] = sb.ToString();
            if (Interlocked.Increment(ref doneR) % 5 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        foreach (string r in rows) Console.WriteLine(r);
        Console.Error.WriteLine($"所要 {bzSw.Elapsed.TotalSeconds:F1} 秒");
        return;
    }

    Dictionary<int, double[][]> BzRead(string path, int expectY)
    {
        var d = new Dictionary<int, double[][]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            int y = int.Parse(c[0]);
            if (y != expectY) throw new InvalidOperationException($"{path}: 版 {y} の行が混ざっている（期待 {expectY}）");
            int b = int.Parse(c[2]); int nT = int.Parse(c[3]);
            var ys = new double[nT][];
            int at = 4;
            for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], bzInv); }
            d[b] = ys;
        }
        return d;
    }
    // 添字: 0 = y11 / 1 = y01（ミオ素体）/ 2 = y10（B 素体）/ 3 = y00
    double BzSyn(double[] y) => y[0] - y[2] - y[1] + y[3];

    // ---- 理想台の測定（`ideal` と `tables` で共有）------------------------------------------------
    (string Name, double[] Full0, double[] Full1, double[] Plain0, double[] Plain1)[] BzIdealMeasure()
    {
        var res = new (string, double[], double[], double[], double[])[bzIdealRows.Length];
        Parallel.For(0, bzIdealRows.Length, i =>
        {
            Formation f = bzIdealRows[i].F, p = BzPlainMio(f);
            res[i] = (bzIdealRows[i].Name, BzIdealRate(f, 0), BzIdealRate(f, 1), BzIdealRate(p, 0), BzIdealRate(p, 1));
        });
        return res;
    }
    double BzMean25(double[] v) { double s = 0; for (int wv = 1; wv < bzW; wv++) s += v[wv]; return s / (bzW - 1); }

    Bz2Stat[] BzIdealProbe(Formation f, int y)
    {
        var loc = new Bz2Stat[bzW];
        for (int wv = 0; wv < bzW; wv++) loc[wv] = new Bz2Stat();
        for (int wv = 0; wv < bzW; wv++)
            for (int seed = 0; seed < 40; seed++) BzProbe(f, bzStages[wv].Enemy, seed, y, wv == 3, loc[wv]);
        return loc;
    }

    // =====================================================================================
    // ideal
    // =====================================================================================
    if (bzArg == "ideal")
    {
        Console.WriteLine("# 第87期 —— 理想台（副判定 (C) の分子）");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` は触っていない。診断のローカルで4台を組み、`Stages` の5波 × seed 0..{BzIdealSeeds - 1} で回した。"
                          + "土台は既存の傷軸の行（`裂き (キリ×エグ)` と `刻み×抉り (ノミ×エグ)`）の**読み手1枚をミオに差し替えただけ**で、"
                          + "残り3枠（ゴルム・ドルガ・ヴェル）は動かしていない。**新しい駒は作っていない。**");
        Console.WriteLine();
        var idm = BzIdealMeasure();
        Console.WriteLine("| 台 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波平均 | 帰属（現行 − ミオ素体） | **Δ帰属（Y1 − Y0）** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in idm)
        {
            double a0 = BzMean25(r.Full0) - BzMean25(r.Plain0), a1 = BzMean25(r.Full1) - BzMean25(r.Plain1);
            Console.WriteLine($"| {r.Name} | Y0 | " + string.Join(" | ", r.Full0.Select(v => $"{v:F1}%")) + $" | {BzMean25(r.Full0):F1}% | {BzP2(a0)} | |");
            Console.WriteLine($"| {r.Name} | **Y1** | " + string.Join(" | ", r.Full1.Select(v => $"{v:F1}%")) + $" | **{BzMean25(r.Full1):F1}%** | {BzP2(a1)} | **{BzP2(a1 - a0)}** |");
            Console.WriteLine($"| {r.Name} | 素体 Y0 | " + string.Join(" | ", r.Plain0.Select(v => $"{v:F1}%")) + $" | {BzMean25(r.Plain0):F1}% | | |");
            Console.WriteLine($"| {r.Name} | 素体 Y1 | " + string.Join(" | ", r.Plain1.Select(v => $"{v:F1}%")) + $" | {BzMean25(r.Plain1):F1}% | | |");
        }
        Console.WriteLine();
        {
            int mism = 0, cells = 0;
            foreach (var r in idm) for (int wv = 0; wv < bzW; wv++) { cells++; if (Math.Abs(r.Plain0[wv] - r.Plain1[wv]) > 0.001) mism++; }
            Console.WriteLine($"**自己検査 (a')（理想台版）**: ミオ素体の台が Y0 / Y1 で完全一致 → {cells} セル・ずれ **{mism}** 件 → **{(mism == 0 ? "○" : "×")}**"
                              + "（素体のミオは `Amplifier` を持たないので、規則が走る場所そのものが無い）。");
        }
        Console.WriteLine();
        Console.WriteLine("## 着火の実測（理想台・Y0 → Y1）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 濃縮の発火/戦 | **着火/戦** | 着火の体数/戦 | 初着火T | 濃くした延べ/戦 | **着火由来の毒ダメ/戦** | 刻み回数/戦 | **持続係数** | 総与ダメ/戦 | ベニの回復/戦 | ラウの拡散/戦 | 決着T | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var rw in bzIdealRows)
            for (int y = 0; y < 2; y++)
            {
                var loc = BzIdealProbe(rw.F, y);
                var sq = BzSum(loc, 0);
                double pc = sq.AmpIgnited > 0 ? sq.IgnitePoison / (sq.AmpIgnited * AmplifierTrait.IgniteAmount) : double.NaN;
                Console.WriteLine($"| {rw.Name} | Y{y} | {sq.AmpFires / sq.N:F2} | **{sq.AmpIgnited / sq.N:F2}** | {sq.AmpIgnitableBodies / sq.N:F2} | {(sq.FirstIgniteN > 0 ? sq.FirstIgniteSum / sq.FirstIgniteN : double.NaN):F2} | {sq.AmpThickened / sq.N:F2} | **{sq.IgnitePoison / sq.N:F1}** | {sq.IgniteTicks / sq.N:F2} | **{pc:F2}** | {sq.FoeTaken / sq.N:F1} | {sq.BeniHealed / sq.N:F1} | {sq.RauSpread / sq.N:F2} | {sq.Turns / sq.N:F1} | {sq.Wins * 100.0 / sq.N:F1}% |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {bzSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // tables <y0> <y1>
    // =====================================================================================
    if (bzArg == "tables")
    {
        if (args.Length < 5 || !File.Exists(args[3]) || !File.Exists(args[4]))
        {
            Console.WriteLine("blaze2 tables <y0 の TSV> <y1 の TSV>");
            return;
        }
        var Y = new[] { BzRead(args[3], 0), BzRead(args[4], 1) };
        var missing = bzOthers.Where(b => Y.Any(y => !y.ContainsKey(b))).ToArray();

        Console.WriteLine("# 第87期 —— 傷口に毒を流す（表A〜G）");
        Console.WriteLine();
        Console.WriteLine($"器具は第81期の 2×2 の写し（K = {BzK} 台 × {BzS} 系列・弱い波 {BzWeakPct}%・第2〜5波・seed {BzBand}..{BzBand + BzM - 1}）。"
                          + $"**A はミオ固定**・B は残り {bzOthers.Length} 体。**台・席・戦闘 seed・台の抽選は Y0 / Y1 で同一**。"
                          + "TSV の添字は **0 = y11 ／ 1 = y01（ミオ素体）／ 2 = y10（B 素体）／ 3 = y00**"
                          + "（第85期の罠: A ＝ ミオ と先に固定してから添字を書いた）。"
                          + (missing.Length > 0 ? $" **TSV に無い組 {missing.Length}**: " + string.Join(" / ", missing.Select(b => bzName[b])) : ""));
        Console.WriteLine();

        var dAll = new Dictionary<int, double[]>();
        var dSer = new Dictionary<int, double[][]>();
        foreach (int b in bzOthers)
        {
            if (Y.Any(y => !y.ContainsKey(b))) continue;
            int nT = Y.Min(y => y[b].Length);
            var d = new double[nT];
            var ser = new List<double>[BzS];
            for (int sx = 0; sx < BzS; sx++) ser[sx] = new List<double>();
            for (int t = 0; t < nT; t++) { d[t] = BzSyn(Y[1][b][t]) - BzSyn(Y[0][b][t]); ser[t % BzS].Add(d[t]); }
            dAll[b] = d; dSer[b] = ser.Select(l => l.ToArray()).ToArray();
        }

        // verbose の probe（書き手2枚 × 2版）
        var cst = new Bz2Stat[bzWriters.Length, 2, bzW];
        Parallel.For(0, bzWriters.Length * 2, k =>
        {
            int i = k / 2, y = k % 2;
            var loc = BzProbePair(bzWriters[i], y);
            for (int wv = 0; wv < bzW; wv++) cst[i, y, wv] = loc[wv];
        });
        Bz2Stat Agg(int y, int wv)
        {
            var s = new Bz2Stat();
            for (int i = 0; i < bzWriters.Length; i++)
            {
                if (wv > 0) s.AddFrom(cst[i, y, wv]);
                else for (int v2 = 1; v2 < bzW; v2++) s.AddFrom(cst[i, y, v2]);
            }
            return s;
        }

        // ---- 表A: 紙と実測（自己検査 (h)）--------------------------------------------------------
        Console.WriteLine("## 表A —— 紙のスループット（§1-1）と実測の突き合わせ【自己検査 (h)】");
        Console.WriteLine();
        Console.WriteLine("紙は **Y0 の `Events`** から出す（版に依らない）。実測は **Y1 の「着火された敵がその後に受けた毒の刻みの総量」**"
                          + "（`UnitTally.IgnitePoisonDamage`。着火の時点でその駒の毒は 0 だったので、以後の刻みはすべて着火の下流にある）。"
                          + "**1.5 倍以内で一致すれば (h) は通る。**");
        Console.WriteLine();
        Console.WriteLine("| 波 | 紙: 着火できる敵/戦 | 生存T/体 | **紙の毒ダメ/戦** | **実測の毒ダメ/戦** | 実測/紙 | 実測の着火/戦 | 刻み回数/戦 | 総与ダメ/戦（Y0） | **実測 ÷ 与ダメ** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv <= bzW; wv++)
        {
            Bz2Stat s0 = Agg(0, wv < bzW ? wv : 0), s1 = Agg(1, wv < bzW ? wv : 0);
            double paper = s0.PaperCapped / s0.N, meas = s1.IgnitePoison / s1.N;
            Console.WriteLine($"| {(wv < bzW ? $"第{wv + 1}波" : "**第2〜5波**")} | {s0.PaperBodies / s0.N:F2} | {(s0.PaperBodies > 0 ? s0.PaperTurns / s0.PaperBodies : 0):F2} | **{paper:F1}** | **{meas:F1}** | {(paper > 0 ? (meas / paper).ToString("F2") : "—")} | {s1.AmpIgnited / s1.N:F2} | {s1.IgniteTicks / s1.N:F2} | {s0.FoeTaken / s0.N:F1} | **{meas / (s0.FoeTaken / s0.N) * 100:F1}%** |");
        }
        {
            Bz2Stat s0 = Agg(0, 0), s1 = Agg(1, 0);
            double paper = s0.PaperCapped / s0.N, meas = s1.IgnitePoison / s1.N;
            bool hOk = paper > 0 && meas >= paper / 1.5 && meas <= paper * 1.5;
            Console.WriteLine();
            Console.WriteLine($"**(h)**: 紙 {paper:F1} 対 実測 {meas:F1}（比 {(paper > 0 ? meas / paper : double.NaN):F2}）→ **{(hOk ? "○（1.5 倍以内）" : "×")}**。"
                              + $"毒ダメが総与ダメ {s0.FoeTaken / s0.N:F1} に占める割合は **{meas / (s0.FoeTaken / s0.N) * 100:F1}%**（紙は {paper / (s0.FoeTaken / s0.N) * 100:F1}%・線 {BzPaperFloor:F0}%）。");
        }
        Console.WriteLine();

        // ---- 表B: 持続係数（副判定 (B)）-----------------------------------------------------------
        Console.WriteLine("## 表B —— 持続係数【副判定 (B)。この期から常設】");
        Console.WriteLine();
        Console.WriteLine("    持続係数 = 累積出力 ÷ 即時出力");
        Console.WriteLine("    着火は即時出力が 0 なので、分母に「その発火が盤面に置いた層の1ターンぶんの刻み」＝ 1 を使う");
        Console.WriteLine();
        Console.WriteLine("| 波 | 着火/戦 | 置いた層/戦（＝即時出力） | 着火由来の毒ダメ/戦（＝累積出力） | 刻み回数/戦 | **持続係数** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv <= bzW; wv++)
        {
            Bz2Stat sq = Agg(1, wv < bzW ? wv : 0);
            double denom = sq.AmpIgniteAmount;
            Console.WriteLine($"| {(wv < bzW ? $"第{wv + 1}波{(wv == 3 ? "（軛）" : "")}" : "**第2〜5波**")} | {sq.AmpIgnited / sq.N:F2} | {denom / sq.N:F2} | {sq.IgnitePoison / sq.N:F1} | {sq.IgniteTicks / sq.N:F2} | **{(denom > 0 ? sq.IgnitePoison / denom : double.NaN):F2}** |");
        }
        Console.WriteLine();
        {
            Bz2Stat sq = Agg(1, 0);
            double pc = sq.AmpIgniteAmount > 0 ? sq.IgnitePoison / sq.AmpIgniteAmount : double.NaN;
            Console.WriteLine($"**副判定 (B)**: 持続係数 **{pc:F2}**（線 {BzPersistFloor:F1}）→ **{(pc > BzPersistFloor ? "超えた" : "超えない")}**。"
                              + "第84〜86期の3機構（抉り・縫い・継ぎ当て）はどれも **1.00**——"
                              + "発火の呼び出しの中で払い切るので、累積出力と即時出力が同じ台帳になる（`blaze2 phase0` §1-3 の検算表・自己検査 (j)）。");
        }
        Console.WriteLine();

        // ---- 表C: 主判定 ------------------------------------------------------------------------
        Console.WriteLine("## 表C —— ミオ × 全 " + bzOthers.Length + " 体の Δ相乗（降順・上位 " + BzTop + " と下位 " + BzTop + "）【主判定 Q1・Q2】");
        Console.WriteLine();
        Console.WriteLine("`相乗(ミオ,B) = y11 − y10 − y01 + y00`／`Δ相乗 = 相乗_Y1 − 相乗_Y0`（**同じ台・同じ seed の対**なので Δ の SE は台ごとの Δ から出す）。"
                          + "★ は敵に傷を書く2枚（キリ＝撒く／ノミ＝積む）。`有意` は |Δ| > 2SE(Δ)。**個数は主判定に混ぜない。**");
        Console.WriteLine();
        var order = dAll.Keys.OrderByDescending(b => dAll[b].Average()).ToArray();
        Console.WriteLine("| 順位 | B | **Δ相乗** | SE(Δ) | 有意 | 相乗 Y0 | 相乗 Y1 | 系列1 Δ | 系列2 Δ | y11 Y0 | y11 Y1 | y00 |");
        Console.WriteLine("|--:|---|--:|--:|:-:|--:|--:|--:|--:|--:|--:|--:|");
        void CRow(int rank, int b)
        {
            double m = dAll[b].Average(), se = BzSe(dAll[b]);
            int nT = dAll[b].Length;
            Console.WriteLine($"| {rank} | {(bzWriters.Contains(b) ? "★" : "")}{bzName[b]} | **{BzP2(m)}** | {se:F2} | {(Math.Abs(m) > 2 * se ? "○" : "")} | {BzP2(Y[0][b].Take(nT).Select(BzSyn).Average())} | {BzP2(Y[1][b].Take(nT).Select(BzSyn).Average())} | {BzP2(dSer[b][0].Average())} | {BzP2(dSer[b][1].Average())} | {Y[0][b].Take(nT).Average(v => v[0]):F1} | {Y[1][b].Take(nT).Average(v => v[0]):F1} | {Y[0][b].Take(nT).Average(v => v[3]):F1} |");
        }
        for (int i = 0; i < Math.Min(BzTop, order.Length); i++) CRow(i + 1, order[i]);
        if (order.Length > 2 * BzTop) Console.WriteLine("| … | | | | | | | | | | | |");
        for (int i = Math.Max(BzTop, order.Length - BzTop); i < order.Length; i++) CRow(i + 1, order[i]);
        Console.WriteLine();
        foreach (int b in bzWriters)
            if (dAll.ContainsKey(b) && Array.IndexOf(order, b) >= BzTop && Array.IndexOf(order, b) < order.Length - BzTop)
                CRow(Array.IndexOf(order, b) + 1, b);
        Console.WriteLine();
        double qKiri = dAll.ContainsKey(bzIdx["kiri"]) ? dAll[bzIdx["kiri"]].Average() : double.NaN;
        double qNomi = dAll.ContainsKey(bzIdx["nomi"]) ? dAll[bzIdx["nomi"]].Average() : double.NaN;
        double[] qKiriSer = dAll.ContainsKey(bzIdx["kiri"]) ? dSer[bzIdx["kiri"]].Select(a => a.Average()).ToArray() : new[] { double.NaN, double.NaN };
        double[] qNomiSer = dAll.ContainsKey(bzIdx["nomi"]) ? dSer[bzIdx["nomi"]].Select(a => a.Average()).ToArray() : new[] { double.NaN, double.NaN };
        int nPos = order.Count(b => dAll[b].Average() > 2 * BzSe(dAll[b])), nNeg = order.Count(b => dAll[b].Average() < -2 * BzSe(dAll[b]));
        Console.WriteLine($"全 {order.Length} 体の Δ相乗 の平均 {BzP2(order.Average(b => dAll[b].Average()))}・中央値 {BzP2(order.Select(b => dAll[b].Average()).OrderBy(v => v).ElementAt(order.Length / 2))}・"
                          + $"有意な正 {nPos} / 有意な負 {nNeg}（参考）。2枚の順位: "
                          + string.Join(" / ", bzWriters.Where(b => dAll.ContainsKey(b)).Select(b => $"{bzName[b]} {Array.IndexOf(order, b) + 1} / {order.Length} 位")) + "。");
        Console.WriteLine();
        Console.WriteLine($"**Q1（主判定）: Δ相乗(ミオ, キリ) = {BzP2(qKiri)}pt**（系列1 {BzP2(qKiriSer[0])} / 系列2 {BzP2(qKiriSer[1])}・"
                          + $"SE {(dAll.ContainsKey(bzIdx["kiri"]) ? BzSe(dAll[bzIdx["kiri"]]) : double.NaN):F2}）。線 +{BzQ1Line:F1} → **{(qKiri >= BzQ1Line ? "○" : "×")}**"
                          + "（**平均を取らない。ノミは設計上ほぼ 0 になると予測しているので、2枚の平均にすると予測が当たるほど主判定が落ちる**）。");
        Console.WriteLine($"**Q2（向きの検証）: Δ相乗(ミオ, ノミ) = {BzP2(qNomi)}pt**（系列1 {BzP2(qNomiSer[0])} / 系列2 {BzP2(qNomiSer[1])}）。"
                          + $"キリ {BzP2(qKiri)} > ノミ {BzP2(qNomi)} → **{(qKiri > qNomi ? "○（予測どおり）" : "×（予測が外れた。執着の読みが違う）")}**");
        Console.WriteLine();

        // ---- 表D: 台の乖離（副判定 (C)）------------------------------------------------------------
        Console.WriteLine("## 表D —— 台の乖離【副判定 (C)。この期から常設】");
        Console.WriteLine();
        Console.WriteLine($"同じ機構の Δ を、**理想台（診断のローカル4台・`Stages` の5波 × seed 0..{BzIdealSeeds - 1}）**と"
                          + "**ドラフト台（この期の 2×2）**で比べる。第85期の巻き込み則は 理想台 +41.5pt（第四波）対 ドラフト台 +1.30pt で **30 倍**離れていた。");
        Console.WriteLine();
        var idm2 = BzIdealMeasure();
        Console.WriteLine("| 理想台 | 帰属 Y0 | 帰属 Y1 | **Δ帰属** | 第2波 Δ | 第3波 Δ | 第4波 Δ | 第5波 Δ | 対応するドラフト台の Δ相乗 | **乖離（理想 ÷ ドラフト）** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var divs = new List<double>();
        foreach (var r in idm2)
        {
            double a0 = BzMean25(r.Full0) - BzMean25(r.Plain0), a1 = BzMean25(r.Full1) - BzMean25(r.Plain1);
            double d = a1 - a0;
            double draft = r.Name.StartsWith("ノミ") ? qNomi : qKiri;
            double div = Math.Abs(draft) > 1e-9 ? d / draft : double.NaN;
            if (!double.IsNaN(div)) divs.Add(div);
            Console.WriteLine($"| {r.Name} | {BzP2(a0)} | {BzP2(a1)} | **{BzP2(d)}** | "
                              + string.Join(" | ", Enumerable.Range(1, bzW - 1).Select(wv => BzP2((r.Full1[wv] - r.Plain1[wv]) - (r.Full0[wv] - r.Plain0[wv]))))
                              + $" | {BzP2(draft)} | **{(double.IsNaN(div) ? "—" : div.ToString("F1") + "×")}** |");
        }
        Console.WriteLine();
        Console.WriteLine($"**副判定 (C)**: 乖離の中央値 **{(divs.Count > 0 ? divs.OrderBy(v => v).ElementAt(divs.Count / 2).ToString("F1") : "—")}×**"
                          + $"（幅 {(divs.Count > 0 ? divs.Min().ToString("F1") : "—")}〜{(divs.Count > 0 ? divs.Max().ToString("F1") : "—")}×）。第85期は 30 倍。");
        Console.WriteLine();

        // ---- 表E: 着火（Q3）---------------------------------------------------------------------
        Console.WriteLine("## 表E —— 着火の回数・体数・初出ターン【Q3。波別】");
        Console.WriteLine();
        Console.WriteLine("| B | 波 | 版 | 濃縮の発火/戦 | **着火/戦** | 着火できる敵の実体数/戦 | 初出T | 初着火T | 濃くした延べ/戦 | 敵に書いた傷/戦 | 敵に入った毒/戦 | 決着T | 勝率 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < bzWriters.Length; i++)
            for (int wv = 1; wv < bzW; wv++)
                for (int y = 0; y < 2; y++)
                {
                    Bz2Stat sq = cst[i, y, wv];
                    if (sq.N == 0) continue;
                    Console.WriteLine($"| {bzName[bzWriters[i]]} | 第{wv + 1}波 | Y{y} | {sq.AmpFires / sq.N:F2} | **{sq.AmpIgnited / sq.N:F2}** | {sq.AmpIgnitableBodies / sq.N:F2} | {(sq.FirstIgnitableN > 0 ? sq.FirstIgnitableSum / sq.FirstIgnitableN : double.NaN):F2} | {(sq.FirstIgniteN > 0 ? sq.FirstIgniteSum / sq.FirstIgniteN : double.NaN):F2} | {sq.AmpThickened / sq.N:F2} | {sq.CarryWoundFoe / sq.N:F2} | {sq.CarryPoisonFoe / sq.N:F1} | {sq.Turns / sq.N:F1} | {sq.Wins * 100.0 / sq.N:F1}% |");
                }
        Console.WriteLine();
        {
            // グザ同席の台（`compare` の行）で着火が 0 か（自己検査 (d)）
            double gTot = 0, gN = 0;
            foreach (var rw in bzGuzaRows)
            {
                var acc = new Bz2Stat();
                for (int wv = 0; wv < bzW; wv++)
                    for (int seed = 0; seed < 20; seed++) BzProbe(rw.F, bzStages[wv].Enemy, seed, 1, wv == 3, acc);
                gTot += acc.AmpIgnited; gN += acc.N;
            }
            Console.WriteLine($"**(d) グザ同席の台で着火が 0 回**: `compare` のミオ×グザ {bzGuzaRows.Length} 行を Y1 で {gN:F0} 戦回して着火 **{gTot:F0}** 回 → **{(gTot == 0 ? "○" : "×")}**"
                              + "（グザが毎ターン敵全体に毒を撒くので「傷を持ち毒を持たない敵」が存在しない）。");
        }
        Console.WriteLine();

        // ---- 表F: 発火回数（副判定 (A)）と下流（Q4）--------------------------------------------------
        Console.WriteLine("## 表F —— 読み手の発火回数【副判定 (A)】と下流の読み手【Q4】");
        Console.WriteLine();
        Console.WriteLine($"ミオの濃縮の発火回数/戦（＝手番を持てたターン数）と、そのうち**着火だった回数**。"
                          + $"第85期のハリは **{BzHariFires:F2} 回/戦**、第86期のノノは **{BzNonoFires:F2} 回/戦**（同じ台・同じ seed 帯）。"
                          + "**稼働率（発火 ÷ 決着T）を必ず併記する**（第86期から）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | **発火/戦** | **稼働率** | うち着火/戦 | 着火の割合 | 濃くした延べ/戦 | ハリ 2.15 との比 | ノノ 2.10 との比 | ベニの回復/戦 | ラウの拡散/戦 | 決着T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int wv = 1; wv <= bzW; wv++)
            for (int y = 0; y < 2; y++)
            {
                Bz2Stat sq = Agg(y, wv < bzW ? wv : 0);
                if (sq.N == 0) continue;
                double fires = sq.AmpFires / sq.N;
                Console.WriteLine($"| {(wv < bzW ? $"第{wv + 1}波" : "**第2〜5波**")} | Y{y} | **{fires:F2}** | **{sq.AmpFires / sq.Turns * 100:F1}%** | {sq.AmpIgnited / sq.N:F2} | {(sq.AmpFires > 0 ? sq.AmpIgnited / sq.AmpFires * 100 : 0):F1}% | {sq.AmpThickened / sq.N:F2} | {fires / BzHariFires:F2}× | {fires / BzNonoFires:F2}× | {sq.BeniHealed / sq.N:F1} | {sq.RauSpread / sq.N:F2} | {sq.Turns / sq.N:F1} |");
            }
        Console.WriteLine();
        {
            Bz2Stat s0 = Agg(0, 0), s1 = Agg(1, 0);
            Console.WriteLine($"**副判定 (A)**: ミオの発火 **{s1.AmpFires / s1.N:F2} 回/戦**・稼働率 **{s1.AmpFires / s1.Turns * 100:F1}%**"
                              + $"（ハリ {BzHariFires:F2} の {s1.AmpFires / s1.N / BzHariFires:F2} 倍・ノノ {BzNonoFires:F2} の {s1.AmpFires / s1.N / BzNonoFires:F2} 倍）。"
                              + $"**うち着火だったのは {s1.AmpIgnited / s1.N:F2} 回/戦（{(s1.AmpFires > 0 ? s1.AmpIgnited / s1.AmpFires * 100 : 0):F1}%）。**");
            Console.WriteLine();
            Console.WriteLine($"**Q4（下流の読み手）**: ベニの回復 {s0.BeniHealed / s0.N:F1} → {s1.BeniHealed / s1.N:F1}／"
                              + $"ラウの拡散 {s0.RauSpread / s0.N:F2} → {s1.RauSpread / s1.N:F2} 回/戦（ドラフト台なので、この2枚は埋め草に引かれたときだけ載る）。"
                              + "**理想台の側は `blaze2 ideal` の下段。**");
        }
        Console.WriteLine();

        // ---- 表G: 自己検査 ----------------------------------------------------------------------
        Console.WriteLine("## 表G —— 自己検査（(a)(b)(d)(e)(f)(g)(h)(i)。(c) と拒否権は `blaze2 check`・(j) は `blaze2 phase0`）");
        Console.WriteLine();
        double maxA = 0; int cellsA = 0;
        foreach (int b in bzOthers)
        {
            if (Y.Any(y => !y.ContainsKey(b))) continue;
            int nT = Y.Min(y => y[b].Length);
            for (int t = 0; t < nT; t++)
                foreach (int k in new[] { 1, 3 }) { cellsA++; maxA = Math.Max(maxA, Math.Abs(Y[0][b][t][k] - Y[1][b][t][k])); }
        }
        Bz2Stat A0 = Agg(0, 0), A1 = Agg(1, 0);
        double paperA = A0.PaperCapped / A0.N, measA = A1.IgnitePoison / A1.N;
        bool hOkA = paperA > 0 && measA >= paperA / 1.5 && measA <= paperA * 1.5;
        Console.WriteLine("| 検査 | 内容 | 値 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        Console.WriteLine($"| (a) | **ミオ素体のセル**（添字 1・3）が Y0 / Y1 で完全一致 | {cellsA} セル・最大差 {maxA:F10}pt | {(maxA == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (b) | §1-2 の計数（**着火できる敵の実体数**）が Y0 と Y1 で同じ | Y0 {A0.AmpIgnitableBodies / A0.N:F4} ／ Y1 {A1.AmpIgnitableBodies / A1.N:F4} 体/戦（差 {(A1.AmpIgnitableBodies / A1.N - A0.AmpIgnitableBodies / A0.N) / (A0.AmpIgnitableBodies / A0.N) * 100:F2}%） | 参考 |");
        Console.WriteLine($"| (b') | 同・**延べ回数**は版で必ず変わる（着火した敵は次のターンから毒を持つので候補から外れる） | Y0 {A0.AmpIgnitable / A0.N:F2} ／ Y1 {A1.AmpIgnitable / A1.N:F2} 回/戦 | 参考 |");
        Console.WriteLine($"| (e) | 着火した敵の毒が、着火したターンに {AmplifierTrait.IgniteAmount} | 毒の総和 {A1.AmpIgnitePoisonAfter:F0} ÷ 着火 {A1.AmpIgnited:F0} = {(A1.AmpIgnited > 0 ? A1.AmpIgnitePoisonAfter / A1.AmpIgnited : double.NaN):F4} | {(A1.AmpIgnited > 0 && Math.Abs(A1.AmpIgnitePoisonAfter / A1.AmpIgnited - AmplifierTrait.IgniteAmount) < 1e-9 ? "○" : "**×**")} |");
        Console.WriteLine($"| (f) | 着火量が傷の数に比例していない（着火量 ÷ 着火回数 = 1.00） | {A1.AmpIgniteAmount:F0} / {A1.AmpIgnited:F0} = {(A1.AmpIgnited > 0 ? A1.AmpIgniteAmount / A1.AmpIgnited : double.NaN):F4}（読んだ傷の平均の深さ {(A1.AmpIgnited > 0 ? A1.AmpIgniteWoundBefore / A1.AmpIgnited : double.NaN):F2}） | {(A1.AmpIgnited > 0 && Math.Abs(A1.AmpIgniteAmount / A1.AmpIgnited - AmplifierTrait.IgniteAmount) < 1e-9 ? "○" : "**×**")} |");
        Console.WriteLine($"| (g) | 傷が着火で減っていない（消費しない） | 着火前 {A1.AmpIgniteWoundBefore:F0} ／ 着火後 {A1.AmpIgniteWoundAfter:F0} | {(A1.AmpIgniteWoundBefore == A1.AmpIgniteWoundAfter ? "○" : "**×**")} |");
        Console.WriteLine($"| (h) | 紙のスループットと実測が 1.5 倍以内 | 紙 {paperA:F1} ／ 実測 {measA:F1}（比 {(paperA > 0 ? measA / paperA : double.NaN):F2}） | {(hOkA ? "○" : "**×**")} |");
        Console.WriteLine($"| (i) | 主判定が2系列で同符号 | キリ {BzP2(qKiriSer[0])} / {BzP2(qKiriSer[1])} | {(Math.Sign(qKiriSer[0]) == Math.Sign(qKiriSer[1]) ? "○" : "**×**")} |");
        Console.WriteLine($"| 敵側 | 敵陣で着火が走った回数（§7） | Y1 {A1.FoeAmpIgnited:F0} 回 | {(A1.FoeAmpIgnited == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| 参考 | Log の着火行 ＝ 計数の着火回数 | Log {A1.IgniteLogs:F0} ／ 計数 {A1.AmpIgnited:F0} | {(A1.IgniteLogs == A1.AmpIgnited ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine("## 判定表");
        Console.WriteLine();
        Console.WriteLine("| | 判定 | 値 | 線 | 結果 |");
        Console.WriteLine("|---|---|--:|--:|:-:|");
        Console.WriteLine($"| **Q1** | **Δ相乗(ミオ, キリ)** | **{BzP2(qKiri)}** | ≥ +{BzQ1Line:F1} | **{(qKiri >= BzQ1Line ? "○" : "×")}** |");
        Console.WriteLine($"| **(A)** | 読み手の発火回数/戦（稼働率） | {A1.AmpFires / A1.N:F2}（{A1.AmpFires / A1.Turns * 100:F1}%） | ハリ {BzHariFires:F2} / ノノ {BzNonoFires:F2} | — |");
        Console.WriteLine($"| **(B)** | **持続係数** | **{(A1.AmpIgniteAmount > 0 ? A1.IgnitePoison / A1.AmpIgniteAmount : double.NaN):F2}** | > {BzPersistFloor:F1} | **{(A1.AmpIgniteAmount > 0 && A1.IgnitePoison / A1.AmpIgniteAmount > BzPersistFloor ? "○" : "×")}** |");
        Console.WriteLine($"| **(C)** | 台の乖離（理想 ÷ ドラフト・中央値） | {(divs.Count > 0 ? divs.OrderBy(v => v).ElementAt(divs.Count / 2).ToString("F1") + "×" : "—")} | 第85期 30× | — |");
        Console.WriteLine($"| Q2 | 向きの検証（キリ > ノミ） | キリ {BzP2(qKiri)} ／ ノミ {BzP2(qNomi)} | キリ > ノミ | {(qKiri > qNomi ? "○" : "×")} |");
        Console.WriteLine($"| Q3 | 着火/戦（第2〜5波） | {A1.AmpIgnited / A1.N:F2} 回・{A1.AmpIgnitableBodies / A1.N:F2} 体 | 記録 | — |");
        Console.WriteLine($"| Q4 | 下流（ベニ／ラウ） | 回復 {A0.BeniHealed / A0.N:F1} → {A1.BeniHealed / A1.N:F1} ／ 拡散 {A0.RauSpread / A0.N:F2} → {A1.RauSpread / A1.N:F2} | 記録 | — |");
        Console.WriteLine();
        Console.WriteLine($"所要 {bzSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // =====================================================================================
    // check
    // =====================================================================================
    if (bzArg == "check")
    {
        Console.WriteLine("# 第87期 —— 受け入れ基準: `compare` 61 行の Y0 / Y1 の突き合わせと拒否権");
        Console.WriteLine();
        var c = new double[2][][];
        for (int y = 0; y < 2; y++) c[y] = new double[bzAllRows.Length][];
        Parallel.For(0, bzAllRows.Length * 2, k =>
        {
            int i = k / 2, y = k % 2;
            c[y][i] = BzCompare(bzAllRows[i].F, y);
        });
        int mismDoc = 0, cellsDoc = 0, missingDoc = 0, mism = 0, mismNoMio = 0;
        var movedRows = new HashSet<string>();
        var lines = new List<string>();
        for (int i = 0; i < bzAllRows.Length; i++)
        {
            bool hasMio = BzHas(bzAllRows[i].F, "mio");
            if (bzBalance.TryGetValue(bzAllRows[i].Name, out double[]? doc))
                for (int wv = 0; wv < bzW; wv++) { cellsDoc++; if (Math.Abs(doc[wv] - c[0][i][wv]) > 0.05) { mismDoc++; lines.Add($"| {bzAllRows[i].Name} | 第{wv + 1}波 | docs {doc[wv]:F1} | Y0 {c[0][i][wv]:F1} |"); } }
            else missingDoc++;
            for (int wv = 0; wv < bzW; wv++)
                if (Math.Abs(c[0][i][wv] - c[1][i][wv]) > 0.001)
                {
                    mism++; movedRows.Add(bzAllRows[i].Name);
                    if (!hasMio) mismNoMio++;
                    lines.Add($"| {bzAllRows[i].Name} | 第{wv + 1}波 | Y0 {c[0][i][wv]:F1} | **Y1** {c[1][i][wv]:F1} |");
                }
        }
        Console.WriteLine($"`CompareBuilds()` {bzAllRows.Length} 行 × {bzW} 波 × seed 0..199 を Y0 / Y1 で回した（{bzAllRows.Length * bzW * 200 * 2:N0} 戦）。");
        Console.WriteLine();
        Console.WriteLine("| 検査 | 内容 | 値 | 判定 |");
        Console.WriteLine("|---|---|--:|:-:|");
        Console.WriteLine($"| 受け入れ | Y0 と `docs/balance.md` の突き合わせ | {cellsDoc} セル・ずれ {mismDoc} 件（`docs` に無い行 {missingDoc}） | {(mismDoc == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| **(c)** | **`compare` 61 行が Y0 / Y1 で全セル ±0.0** | ずれ {mism} 件（{movedRows.Count} 行） | {(mism == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| (c') | ミオを含まない行が Y1 で全セル ±0.0 | ずれ {mismNoMio} 件 | {(mismNoMio == 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| 予告 | Phase 0 の 1-5（キリ／ノミとミオの交わり） | {bzCrossRows.Length} 行 | {(bzCrossRows.Length == 0 ? "0 行 → 拒否権は原理的に立たない" : string.Join(" / ", bzCrossRows.Select(r => r.Name)))} |");
        double Fifth(double[][] cc, IEnumerable<string> names)
        {
            var xs = new List<double>();
            foreach (string n in names) { int i = Array.FindIndex(bzAllRows, rw => rw.Name == n); if (i >= 0) xs.Add(cc[i][bzW - 1]); }
            return xs.Count == 0 ? double.NaN : xs.Average();
        }
        double f0 = Fifth(c[0], Baseline.PrimaryRows), f1 = Fifth(c[1], Baseline.PrimaryRows);
        int Over(double[][] cc) => Enumerable.Range(0, bzAllRows.Length).Count(i => cc[i][bzW - 1] > 95.0);
        int o0 = Over(c[0]), o1 = Over(c[1]);
        Console.WriteLine($"| 拒否権1 | 主判定 {Baseline.PrimaryRows.Length} 行の第五波平均 ≥ {Baseline.PrimaryFifthFloor:F1}% | Y0 {f0:F2}% → Y1 {f1:F2}% | {(f1 >= Baseline.PrimaryFifthFloor ? "立たない" : "**立つ**")} |");
        Console.WriteLine($"| 拒否権2 | `compare` {bzAllRows.Length} 行で第五波 > 95% が新たに 2 行以上 | Y0 {o0} → Y1 {o1} 行 | {(o1 - o0 >= 2 ? "**立つ**" : "立たない")} |");
        var v3 = new List<string>();
        foreach (string n in Baseline.PrimaryRows)
        {
            int i = Array.FindIndex(bzAllRows, rw => rw.Name == n);
            if (i < 0) continue;
            for (int wv = 0; wv < bzW; wv++)
                if (c[1][i][wv] - c[0][i][wv] <= -10.0) v3.Add($"{n} 第{wv + 1}波 {c[0][i][wv]:F1} → {c[1][i][wv]:F1}");
        }
        Console.WriteLine($"| 拒否権3 | 主判定19行が、いずれかの波で −10.0pt 以上落ちる | {v3.Count} 件{(v3.Count > 0 ? "（" + string.Join(" / ", v3) + "）" : "")} | {(v3.Count > 0 ? "**立つ**" : "立たない")} |");
        if (lines.Count > 0)
        {
            Console.WriteLine(); Console.WriteLine("| 行 | 波 | 左 | 右 |"); Console.WriteLine("|---|---|--:|--:|");
            foreach (string l in lines) Console.WriteLine(l);
        }
        Console.WriteLine();
        Console.WriteLine($"| 行（ミオを含む {bzMioRows.Length} 行） | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var rw in bzMioRows)
        {
            int i = Array.FindIndex(bzAllRows, xx => xx.Name == rw.Name);
            for (int y = 0; y < 2; y++)
                Console.WriteLine($"| {rw.Name}{(bzPrimary.Contains(rw.Name) ? "［主判定］" : "")} | Y{y} | " + string.Join(" | ", c[y][i].Select(v => $"{v:F1}%")) + $" | {c[y][i].Average():F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {bzSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    Console.WriteLine("blaze2: 引数は phase0 / run <y> [skip] [take] / ideal / tables <y0> <y1> / check。");
    return;
}
}
