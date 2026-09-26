using BattleCore;
using static Common;

// =====================================================================================
// shock run / check（第214期）—— 表A〜E と自己検査。指示書 §6・§8。
//
// 席: 台ごと・陣形ごとに 120 通りを **K1** で総当たり（第2〜5波 × seed 1000..1049 の勝ち数最大・同値は列挙順で最初）。
//     **同じ席を全版に使う**（版ごとに選ぶと代金の差に席の差が混ざる）。
// 測る: 第2〜5波 × seed 0..199（verbose: false）。
// =====================================================================================

static partial class ShockDiag
{
    const int MeasSeeds = 200, PickSeed0 = 1000, PickSeeds = 50;

    static UnitDef WithTraits(UnitDef d, params TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
        Pattern = d.Pattern, Actions = d.Actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    internal static readonly UnitDef KataK2 = WithTraits(UnitCatalog.Kata, TraitId.Thunder, TraitId.ThunderLeak, TraitId.ShockTick);
    internal static readonly UnitDef KataK1m = WithTraits(UnitCatalog.Kata, TraitId.Thunder);

    internal static readonly (string Tag, UnitDef Def)[] Versions =
    {
        ("K1", UnitCatalog.Kata), ("K2", KataK2), ("K1−", KataK1m), ("K0", UnitCatalog.KataOld),
    };

    internal static readonly FormationShape[] Shapes = { FormationShape.X, FormationShape.Diamond };
    static string ShapeName(FormationShape s) => s == FormationShape.X ? "X字" : "P2";

    internal sealed record Table(string Tag, List<UnitDef> Members, Dictionary<FormationShape, Formation> Seats, UnitDef? Original);

    static List<Table>? _tables;

    /// <summary>台（Phase 0 の規則で枠を替え、席を K1 で総当たりして固定）。</summary>
    internal static List<Table> Tables()
    {
        if (_tables is not null) return _tables;
        var list = new List<Table>();
        for (int i = 0; i < BaseRows.Length; i++)
        {
            var (tag, row, _, _) = BaseRows[i];
            var rep = ReplacedOf(i, print: false);
            var members = RowOf(row).Occupied().Select(o => o.Def).ToList();
            UnitDef? original = null;
            if (i == 3)
            {
                members[members.IndexOf(rep[0].Def)] = UnitCatalog.Susu;   // 血詠みのアカ
                members[members.IndexOf(rep[1].Def)] = UnitCatalog.Kata;
            }
            else
            {
                original = rep[0].Def;
                members[members.IndexOf(rep[0].Def)] = UnitCatalog.Kata;
            }
            var seats = new Dictionary<FormationShape, Formation>();
            foreach (FormationShape sh in Shapes) seats[sh] = PickSeats(members, sh);
            list.Add(new Table(tag, members, seats, original));
        }
        return _tables = list;
    }

    static Formation PickSeats(List<UnitDef> members, FormationShape shape)
    {
        var all = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation { Shape = shape };
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            all.Add(f);
        }
        var score = new int[all.Count];
        Parallel.For(0, all.Count * 4, j =>
        {
            int i = j / 4, st = 1 + j % 4;
            Formation en = EnemyCatalog.Stages[st].Enemy;
            int w = 0;
            for (int s = PickSeed0; s < PickSeed0 + PickSeeds; s++)
                if (BattleEngine.Run(all[i], en, s, verbose: false).PlayerWon) w++;
            Interlocked.Add(ref score[i], w);
        });
        int best = 0;
        for (int i = 1; i < all.Count; i++) if (score[i] > score[best]) best = i;
        return all[best];
    }

    static string SeatsNamed(Formation f) => string.Join(" ／ ", f.Occupied().Select(o => f.Shape.FrameNames[o.Slot] + ":" + o.Def.Name));

    // =================================================================================
    // 集計
    // =================================================================================

    internal sealed class Agg
    {
        public readonly long[] Wins = new long[5], AllSurv = new long[5], N = new long[5];
        public long WinTurns, WinN;
        public readonly UnitTally P = new(), E = new();
        public readonly Dictionary<string, long> TriggeredBy = new();
        public readonly Dictionary<string, long> KataDealtBy = new();

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
                (playerIds.Contains(id) ? P : E).Add(t);
                if (t.ShockTriggered > 0) TriggeredBy[id] = TriggeredBy.GetValueOrDefault(id) + t.ShockTriggered;
            }
        }

        public void Merge(Agg o)
        {
            for (int i = 0; i < 5; i++) { Wins[i] += o.Wins[i]; AllSurv[i] += o.AllSurv[i]; N[i] += o.N[i]; }
            WinTurns += o.WinTurns; WinN += o.WinN;
            P.Add(o.P); E.Add(o.E);
            foreach (var (k, v) in o.TriggeredBy) TriggeredBy[k] = TriggeredBy.GetValueOrDefault(k) + v;
        }

        public long Battles => N.Sum();
        public double WinPct(int st) => N[st] == 0 ? double.NaN : 100.0 * Wins[st] / N[st];
        public double Mean25 => Enumerable.Range(1, 4).Average(WinPct);
        public double AllSurvPct => 100.0 * AllSurv.Skip(1).Sum() / Math.Max(1, N.Skip(1).Sum());
        public double MeanWinT => WinN == 0 ? double.NaN : (double)WinTurns / WinN;
    }

    static Agg Measure(Formation f, IReadOnlyList<int> waves, Func<int, Formation>? enemyOf = null, int seeds = MeasSeeds)
    {
        var playerIds = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        var total = new Agg();
        var gate = new object();
        int n = waves.Count * seeds;
        Parallel.For(0, n, () => new Agg(), (j, _, local) =>
        {
            int st = waves[j / seeds], s = j % seeds;
            Formation en = enemyOf?.Invoke(st) ?? EnemyCatalog.Stages[st].Enemy;
            local.Take(BattleEngine.Run(f, en, s, verbose: false), st, playerIds);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    static readonly int[] Waves25 = { 1, 2, 3, 4 };

    static Formation WithKata(Formation f, UnitDef kata) => SwapDef(f, UnitCatalog.Kata, kata);

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");
    static double Per(long x, Agg a) => (double)x / Math.Max(1, a.Battles);

    static string Hist(long[]? h, int from, int to, bool pct = true)
    {
        if (h is null) return "—";
        long tot = h.Sum();
        if (tot == 0) return "—";
        var parts = new List<string>();
        for (int i = from; i <= to && i < h.Length; i++)
        {
            long v = i == to ? h.Skip(i).Sum() : h[i];
            parts.Add((i == to ? i + "+" : i.ToString()) + ":" + (pct ? (100.0 * v / tot).ToString("F0") + "%" : v.ToString()));
        }
        return string.Join(" ", parts);
    }

    static double MeanHist(long[]? h)
    {
        if (h is null || h.Sum() == 0) return double.NaN;
        double s = 0; long n = 0;
        for (int i = 0; i < h.Length; i++) { s += i * h[i]; n += h[i]; }
        return s / n;
    }

    // =================================================================================
    // run
    // =================================================================================

    static partial void RunImpl(string arg)
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第214期 `shock run` —— 表A〜E（**線は置かない**）");
        Console.WriteLine();
        var tables = Tables();
        Console.WriteLine("## 台と席（K1 で総当たり・第2〜5波 × seed " + PickSeed0 + ".." + (PickSeed0 + PickSeeds - 1) + "）");
        Console.WriteLine();
        foreach (Table tb in tables)
            foreach (FormationShape sh in Shapes)
                Console.WriteLine("- " + tb.Tag + " × " + ShapeName(sh) + ": " + SeatsNamed(tb.Seats[sh]));
        Console.WriteLine();

        // ---- 全部測る ----
        var res = new Dictionary<(int T, FormationShape S, string V), Agg>();
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
            {
                Formation f = tables[i].Seats[sh];
                foreach (var (tag, def) in Versions) res[(i, sh, tag)] = Measure(WithKata(f, def), Waves25);
                if (i == 0)
                {
                    res[(i, sh, "ミオ")] = Measure(WithKata(f, UnitCatalog.Mio), Waves25);
                    res[(i, sh, "元")] = Measure(WithKata(f, tables[i].Original!), Waves25);
                }
            }

        // ---- 表A ----
        Console.WriteLine("## 表A. 勝率（第2〜5波 × seed 0..199）・全員生存勝ち・決着T（勝った戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | **平均** | 全員生存 | 決着T |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
                foreach (string v in Versions.Select(x => x.Tag).Concat(i == 0 ? new[] { "ミオ", "元" } : Array.Empty<string>()))
                {
                    Agg a = res[(i, sh, v)];
                    Console.WriteLine("| " + tables[i].Tag + " | " + ShapeName(sh) + " | " + (v == "K1" ? "**K1**" : v) + " | "
                                      + string.Join(" | ", Waves25.Select(st => F1(a.WinPct(st)))) + " | **" + F1(a.Mean25) + "** | "
                                      + F1(a.AllSurvPct) + " | " + F2(a.MeanWinT) + " |");
                }
        Console.WriteLine();
        Console.WriteLine("- 台1 の **ミオ** ＝ カタの枠にミオ（同じ席）、**元** ＝ 元の駒（疫みのラウ）を同じ席に戻したもの");
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B. カタの雷（K1・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 雷/戦 | 当たり/戦 | 1回で当たった数の平均 | その分布 | 1発の種類の平均 | 種類の分布 | 与ダメ/戦 | 雷で倒した/戦 | 最大の1発 | 帯びた敵なし（1発だけ） |");
        Console.WriteLine("|---|---|--:|--:|--:|---|--:|---|--:|--:|--:|--:|");
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
            {
                Agg a = res[(i, sh, "K1")];
                UnitTally k = a.P;
                Console.WriteLine("| " + tables[i].Tag + " | " + ShapeName(sh) + " | " + F2(Per(k.ThunderCasts, a)) + " | " + F2(Per(k.ThunderHits, a)) + " | "
                                  + F2((double)k.ThunderHits / Math.Max(1, k.ThunderCasts)) + " | " + Hist(k.ThunderPerCastHist, 1, 5) + " | "
                                  + F2(MeanHist(k.ThunderKindsHist)) + " | " + Hist(k.ThunderKindsHist, 0, 4) + " | "
                                  + F1(Per(k.ThunderDealt, a)) + " | " + F2(Per(k.ThunderKills, a)) + " | " + k.ThunderMax + " | "
                                  + F1(100.0 * k.ThunderFallback / Math.Max(1, k.ThunderCasts)) + "% |");
            }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. 感電の帳簿（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("**敵**＝敵陣の感電と放電、**味方**＝味方陣の感電と放電。起爆 ＝ 放電した駒の延べ（1つの連鎖で複数）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 版 | 付いた 敵/味方 | 連鎖 敵/味方 | 起爆 敵/味方 | 連鎖の大きさ（敵）平均・分布 | 最深 | 放電で削った 敵/味方 | 放電で倒れた 敵/味方 | 残った感電 敵/味方 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|---|---|---|");
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
                foreach (string v in new[] { "K1", "K2", "K1−" })
                {
                    Agg a = res[(i, sh, v)];
                    UnitTally p = a.P, e = a.E;
                    Console.WriteLine("| " + tables[i].Tag + " | " + ShapeName(sh) + " | " + v + " | "
                                      + F2(Per(p.ShockOnFoe, a)) + " / " + F2(Per(p.ShockOnAlly, a)) + " | "
                                      + F2(Per(e.ChainRoots, a)) + " / " + F2(Per(p.ChainRoots, a)) + " | "
                                      + F2(Per(e.ShockSpent, a)) + " / " + F2(Per(p.ShockSpent, a)) + " | "
                                      + F2((double)e.ChainUnits / Math.Max(1, e.ChainRoots)) + " ・ " + Hist(e.ChainSizeHist, 1, 4) + " | "
                                      + Math.Max(e.ChainDepthMax, p.ChainDepthMax) + " | "
                                      + F1(Per(e.DischargeTaken, a)) + " / " + F1(Per(p.DischargeTaken, a)) + " | "
                                      + F2(Per(e.DischargeDeaths, a)) + " / " + F2(Per(p.DischargeDeaths, a)) + " | "
                                      + F2(Per(e.ShockLeftAlive, a)) + " / " + F2(Per(p.ShockLeftAlive, a)) + " |");
                }
        Console.WriteLine();

        Console.WriteLine("### 表C'. 連鎖を起こした一撃の主（敵陣・味方陣の連鎖を合わせた延べ・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 版 | 起こした駒（多い順） | 刻み | 出どころなし |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
                foreach (string v in new[] { "K1", "K2", "K1−" })
                {
                    Agg a = res[(i, sh, v)];
                    long all = a.TriggeredBy.Values.Sum() + a.P.ShockTriggeredTick + a.E.ShockTriggeredTick + a.P.ShockTriggeredOther + a.E.ShockTriggeredOther;
                    string who = string.Join(" ／ ", a.TriggeredBy.OrderByDescending(kv => kv.Value).Take(5)
                        .Select(kv => NameOf(kv.Key) + " " + F2(Per(kv.Value, a)) + "（" + (100.0 * kv.Value / Math.Max(1, all)).ToString("F0") + "%）"));
                    long tick = a.P.ShockTriggeredTick + a.E.ShockTriggeredTick, other = a.P.ShockTriggeredOther + a.E.ShockTriggeredOther;
                    Console.WriteLine("| " + tables[i].Tag + " | " + ShapeName(sh) + " | " + v + " | " + (who == "" ? "—" : who) + " | "
                                      + F2(Per(tick, a)) + "（" + (100.0 * tick / Math.Max(1, all)).ToString("F0") + "%） | " + F2(Per(other, a)) + " |");
                }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. 味方側の連鎖（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 版 | 味方への放電（本） | 削った HP | 放電で倒れた味方 | ベニの反転で癒えた | 血（アカ）の総量 | うち放電から |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < tables.Count; i++)
            foreach (FormationShape sh in Shapes)
                foreach (string v in new[] { "K1", "K2", "K1−" })
                {
                    Agg a = res[(i, sh, v)];
                    UnitTally p = a.P;
                    Console.WriteLine("| " + tables[i].Tag + " | " + ShapeName(sh) + " | " + v + " | " + F2(Per(p.DischargeHits, a)) + " | "
                                      + F1(Per(p.DischargeTaken, a)) + " | " + F2(Per(p.DischargeDeaths, a)) + " | " + F1(Per(p.InverseDischargeHealed, a)) + " | "
                                      + F1(Per(p.AshGained, a)) + " | " + F1(Per(p.AshFromDischarge, a)) + " |");
                }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E. 敵の陣形別の連鎖（K1・味方は X字の席・第三波と第五波 × seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 敵の陣形 | 勝率 | 敵の連鎖/戦 | 連鎖の大きさ 平均・分布 | 最深 | 1回の雷で当たった数 |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|--:|");
        for (int i = 0; i < tables.Count; i++)
        {
            Formation f = tables[i].Seats[FormationShape.X];
            foreach (int st in new[] { 2, 4 })
                foreach (bool p3 in new[] { false, true })
                {
                    Agg a = Measure(f, new[] { st }, p3 ? s => EnemyCatalog.Pattern3Of(s)! : null);
                    UnitTally e = a.E, k = a.P;
                    Console.WriteLine("| " + tables[i].Tag + " | " + (st + 1) + " | " + (p3 ? "パターン3" : "X字") + " | " + F1(a.WinPct(st)) + " | "
                                      + F2(Per(e.ChainRoots, a)) + " | " + F2((double)e.ChainUnits / Math.Max(1, e.ChainRoots)) + " ・ " + Hist(e.ChainSizeHist, 1, 4) + " | "
                                      + e.ChainDepthMax + " | " + F2((double)k.ThunderHits / Math.Max(1, k.ThunderCasts)) + " |");
                }
        }
        Console.WriteLine();

        // ---- 表E'（事後・予測の判定に使わない）----
        Console.WriteLine("### 表E'（事後）. 雷が生きている敵を1体だけ取り残した回で、取り残した席（K1・味方 X字・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("跳ねの同点は席番号の順で割る。X 字の前1 からは「中央（席2）→ 前3 → 後3」と進み、後1 を取り残すはず（読み）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 敵の陣形 | 雷（跳ねた回） | 全員に当たった | 1体だけ取り残した | 取り残した席（多い順） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|");
        foreach (int i in new[] { 0, 2 })
        {
            Formation f = tables[i].Seats[FormationShape.X];
            foreach (int st in new[] { 2, 4 })
                foreach (bool p3 in new[] { false, true })
                {
                    Formation enF = p3 ? EnemyCatalog.Pattern3Of(st)! : EnemyCatalog.Stages[st].Enemy;
                    long castsN = 0, full = 0, missOne = 0;
                    var missed = new Dictionary<string, long>();
                    for (int s = 0; s < MeasSeeds; s++)
                    {
                        var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                        var en = BattleEngine.Materialize(enF, BattleContext.EnemyTeam);
                        BattleResult r = BattleEngine.Run(pl, en, s, verbose: true);
                        var byId = en.ToDictionary(u => u.InstanceId);
                        var dead = new HashSet<int>();
                        HashSet<int>? hit = null; HashSet<int>? aliveAtStart = null;
                        void Close()
                        {
                            if (hit is null || aliveAtStart is null || hit.Count < 2) { hit = null; return; }
                            castsN++;
                            var miss = aliveAtStart.Where(x => !hit.Contains(x)).ToList();
                            if (miss.Count == 0) full++;
                            else if (miss.Count == 1)
                            {
                                missOne++;
                                UnitState m = byId[miss[0]];
                                string seat = m.Shape.SeatName(m.Slot);
                                missed[seat] = missed.GetValueOrDefault(seat) + 1;
                            }
                            hit = null;
                        }
                        foreach (BattleEvent e in r.Events)
                        {
                            if (e.Kind == BattleEventKind.Death && e.TargetId is int d) { if (hit is null) dead.Add(d); else dead.Add(d); }
                            if (e.Kind == BattleEventKind.Thunder)
                            {
                                if (e.Slot == 1)
                                {
                                    Close();
                                    hit = new HashSet<int>();
                                    aliveAtStart = byId.Keys.Where(x => !dead.Contains(x)).ToHashSet();
                                }
                                if (e.TargetId is int t && hit is not null) hit.Add(t);
                            }
                            else if (e.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart) Close();
                        }
                        Close();
                    }
                    Console.WriteLine("| " + tables[i].Tag + " | " + (st + 1) + " | " + (p3 ? "パターン3" : "X字") + " | " + castsN + " | "
                                      + F1(100.0 * full / Math.Max(1, castsN)) + "% | " + F1(100.0 * missOne / Math.Max(1, castsN)) + "% | "
                                      + string.Join(" ／ ", missed.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value)) + " |");
                }
        }
        Console.WriteLine();
        Console.WriteLine("所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }

    static string NameOf(string id)
    {
        UnitDef? d = UnitCatalog.Everyone.FirstOrDefault(x => x.Id == id);
        if (d is not null) return d.Name;
        foreach (var st in EnemyCatalog.Stages)
            foreach (var o in st.Enemy.Occupied())
                if (o.Def.Id == id) return "敵:" + o.Def.Name;
        return id;
    }
}
