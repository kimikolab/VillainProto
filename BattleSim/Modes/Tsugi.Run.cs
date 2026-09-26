using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第207期）の本体 —— 版の対照・差し替え診断・火の診断・帳簿・自己検査
//
// **旧ナラは `UnitCatalog.Nara`（対照として残した定義）**、T1・T2 はこの診断のローカルに写してある
// （`UnitCatalog` の `All` にはツギしか居ない）。版の切り替えは札の差し替えだけ（`Plank` / `PlankTinder` / `Scrap`）。
// =====================================================================================

static partial class TsugiDiag
{
    static partial void RunMore208(string mode, string arg, ref bool handled);

    static partial void RunMoreImpl(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run": RunVersions(); handled = true; return;
            case "swap": RunSwap(); handled = true; return;
            case "ledger": RunLedger(); handled = true; return;
            case "check": Check(arg); handled = true; return;
            default: RunMore208(mode, arg, ref handled); return;
        }
    }

    // =================================================================================
    // 版
    // =================================================================================

    static UnitDef Clone(UnitDef d, TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
        Advances = d.Advances, Actions = d.Actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    static readonly UnitDef T1 = Clone(UnitCatalog.Tsugi, new[] { TraitId.Plank });
    static readonly UnitDef T2 = Clone(UnitCatalog.Tsugi, new[] { TraitId.Plank, TraitId.PlankTinder });
    // 第208期に規定のツギ（U3）が変わったので、第207期の T3 は札で写す。
    static readonly UnitDef T3 = Clone(UnitCatalog.Tsugi, new[] { TraitId.Plank, TraitId.PlankTinder, TraitId.Scrap });

    static readonly (string Tag, UnitDef D)[] Versions =
    {
        ("T0", UnitCatalog.Nara), ("T1", T1), ("T2", T2), ("T3", T3),
    };

    static Formation Replace(Formation f, Func<UnitDef, bool> which, UnitDef d)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef u) in f.Occupied()) g[slot] = which(u) ? d : u;
        return g;
    }

    static Formation Apply(Formation f, UnitDef d) => Replace(f, IsTsugi, d);

    static IEnumerable<(string Name, Formation F)> TsugiRows()
        => CompareBuilds().Where(r => HasTsugi(r.F)).Select(r => (r.Name, r.F));

    static IEnumerable<(string Name, Formation F)> LiliRows()
        => CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "lili")).Select(r => (r.Name, r.F));

    // =================================================================================
    // 測る
    // =================================================================================

    sealed class Agg
    {
        public int N, Wins, Clean, Stall;
        public double FallenSum, TurnsSum;
        /// <summary>味方の駒の帳簿（<c>Def.Id</c> ごとに合算）。</summary>
        public readonly Dictionary<string, UnitTally> T = new();
        public int TsugiDied;
        /// <summary>第208期: 倒れていた駒（`PlayerStarterFallen`）の数を Id ごとに。</summary>
        public readonly Dictionary<string, int> FallenBy = new();
        /// <summary>第210期: 応急処置の条件を満たした駒（戦 × 駒）のうち、受けた／受けなかったの数と、その戦を生き延びた数。</summary>
        public int AidUnits, AidSurvived, NeedNoAidUnits, NeedNoAidSurvived;

        public double Win => 100.0 * Wins / Math.Max(1, N);
        /// <summary>全員生存勝ち（全戦に占める割合）。</summary>
        public double CleanAll => 100.0 * Clean / Math.Max(1, N);
        /// <summary>全員生存勝ち（勝った戦のうち）。</summary>
        public double CleanWin => Wins == 0 ? double.NaN : 100.0 * Clean / Wins;
        public double Fallen => FallenSum / Math.Max(1, N);
        public double Turns => TurnsSum / Math.Max(1, N);
        public UnitTally Of(string id) => T.TryGetValue(id, out var t) ? t : new UnitTally();
        public double Per(double x) => x / Math.Max(1, N);
    }

    static readonly Dictionary<(string, int), Agg> Cache = new();

    /// <summary>1台 × 1波（seed 0..199）。<b>全員生存勝ち</b> ＝ 勝って、編成の駒が1体も倒れていない（<c>PlayerStarterFallen</c> が空・第129期「無傷勝利」）。</summary>
    static Agg Measure(string key, Formation f, int st)
    {
        lock (Cache) if (Cache.TryGetValue((key, st), out var hit)) return hit;
        var a = new Agg();
        Formation enemy = EnemyCatalog.Stages[st].Enemy;
        var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        Parallel.For(0, Seeds, seed =>
        {
            BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
            lock (a)
            {
                a.N++;
                if (r.PlayerWon) a.Wins++;
                if (r.PlayerWon && r.PlayerStarterFallen.Count == 0) a.Clean++;
                if (!r.PlayerWon && r.Turns >= BattleEngine.MaxTurns) a.Stall++;
                a.FallenSum += r.PlayerStarterFallen.Count;
                a.TurnsSum += r.Turns;
                if (r.PlayerStarterFallen.Contains("tsugi")) a.TsugiDied++;
                foreach (string id in r.PlayerStarterFallen) a.FallenBy[id] = a.FallenBy.GetValueOrDefault(id) + 1;
                foreach (var (id, t) in r.TallyByUnit)
                {
                    if (!ids.Contains(id)) continue;
                    if (!a.T.TryGetValue(id, out var acc)) a.T[id] = acc = new UnitTally();
                    acc.Add(t);
                    bool alive = !r.PlayerStarterFallen.Contains(id);
                    if (t.FirstAidReceived > 0) { a.AidUnits++; if (alive) a.AidSurvived++; }
                    else if (t.FirstAidNeed > 0) { a.NeedNoAidUnits++; if (alive) a.NeedNoAidSurvived++; }
                }
            }
        });
        lock (Cache) Cache[(key, st)] = a;
        return a;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");
    static readonly int[] Waves = { 1, 2, 3, 4 };
    static double Mean(Func<int, double> f) => Waves.Average(st => f(st));

    // =================================================================================
    // run —— §4.1 ツギの在席行 × 版（T0〜T3）
    // =================================================================================

    static void RunVersions()
    {
        Console.WriteLine("# 第207期 `tsugi run` —— ツギの在席行 × 版（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**T0** ＝ 旧ナラ（第206期の盤面）／ **T1** ＝ 板だけ ／ **T2** ＝ 板 ＋ 燃えやすい ／ **T3**（規定）＝ ＋ 瓦礫拾い。seed 0..199・平均は第2〜5波（規約 (G10)）。");
        Console.WriteLine("セルは第2 / 3 / 4 / 5 波。**全員生存** ＝ 全員生存勝ち（全戦に占める割合）。**倒れた** ＝ 編成の駒が倒れた数の平均（戦の終わりに倒れている駒）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 勝率 | 平均 | 全員生存 | 平均 | 倒れた | 決着T | 膠着 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|");
        var sumW = new Dictionary<string, double>(); var sumC = new Dictionary<string, double>();
        int n = 0;
        foreach (var (name, f) in TsugiRows())
        {
            n++;
            foreach (var (tag, d) in Versions)
            {
                Formation g = Apply(f, d);
                var ag = Waves.Select(st => Measure(name + "|" + tag, g, st)).ToArray();
                double mw = ag.Average(x => x.Win), mc = ag.Average(x => x.CleanAll);
                sumW[tag] = sumW.GetValueOrDefault(tag) + mw; sumC[tag] = sumC.GetValueOrDefault(tag) + mc;
                Console.WriteLine("| " + name + " | " + tag + " | " + string.Join(" / ", ag.Select(x => F1(x.Win))) + " | **" + F1(mw) + "** | "
                                  + string.Join(" / ", ag.Select(x => F1(x.CleanAll))) + " | **" + F1(mc) + "** | "
                                  + ag.Average(x => x.Fallen).ToString("F2") + " | " + ag.Average(x => x.Turns).ToString("F2") + " | " + ag.Sum(x => x.Stall) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("在席 " + n + " 行の平均（勝率 ／ 全員生存）: " + string.Join(" ・ ", Versions.Select(v => v.Tag + " " + F1(sumW[v.Tag] / n) + " / " + F1(sumC[v.Tag] / n))) + "。");
        Console.WriteLine("差: T1 − T0 " + D((sumW["T1"] - sumW["T0"]) / n) + " ／ T2 − T1 " + D((sumW["T2"] - sumW["T1"]) / n) + " ／ T3 − T2 " + D((sumW["T3"] - sumW["T2"]) / n)
                          + "（勝率）・全員生存 " + D((sumC["T1"] - sumC["T0"]) / n) + " ／ " + D((sumC["T2"] - sumC["T1"]) / n) + " ／ " + D((sumC["T3"] - sumC["T2"]) / n) + "。");
        Console.WriteLine();
    }

    // =================================================================================
    // swap —— §4.2 リリの席にツギ ／ §4.3 火の診断 ／ §4.4 ベニ
    // =================================================================================

    static void RunSwap()
    {
        Console.WriteLine("# 第207期 `tsugi swap` —— リリの席にツギ（T3）を置く（`compare` には入れない）");
        Console.WriteLine();
        Console.WriteLine("**L** ＝ リリ（第206期の規定）／ **T** ＝ 同じ席にツギ（T3）。seed 0..199。**全員生存** は全戦に占める割合、（ ）は勝った戦のうち。**カド** ＝ カドの敵への与ダメ/戦。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 勝率 L → T | 全員生存 L → T | 倒れた L → T | 決着T L → T | 膠着 L → T | カド L → T |");
        Console.WriteLine("|---|--:|---|---|---|---|---|---|");
        var rows = LiliRows().ToList();
        var waveWin = new double[5, 2]; var waveClean = new double[5, 2]; var tBetter = new int[5]; var lBetter = new int[5];
        var kado = new List<(string Row, double L, double T)>();
        foreach (var (name, f) in rows)
        {
            Formation gt = Replace(f, d => d.Id == "lili", T3);
            bool hasKado = f.Occupied().Any(o => o.Def.Id == "kado");
            double kl = 0, kt = 0;
            foreach (int st in Waves)
            {
                Agg l = Measure(name + "|L", f, st), t = Measure(name + "|T", gt, st);
                waveWin[st, 0] += l.Win; waveWin[st, 1] += t.Win; waveClean[st, 0] += l.CleanAll; waveClean[st, 1] += t.CleanAll;
                if (t.Win > l.Win) tBetter[st]++; else if (l.Win > t.Win) lBetter[st]++;
                double klw = l.Per(l.Of("kado").DamageToEnemy), ktw = t.Per(t.Of("kado").DamageToEnemy);
                kl += klw; kt += ktw;
                Console.WriteLine("| " + name + " | " + (st + 1) + " | " + F1(l.Win) + " → **" + F1(t.Win) + "** | " + F1(l.CleanAll) + "（" + F1(l.CleanWin) + "）→ **" + F1(t.CleanAll) + "**（" + F1(t.CleanWin) + "） | "
                                  + l.Fallen.ToString("F2") + " → " + t.Fallen.ToString("F2") + " | " + l.Turns.ToString("F1") + " → " + t.Turns.ToString("F1") + " | "
                                  + l.Stall + " → " + t.Stall + " | " + (hasKado ? klw.ToString("F1") + " → " + ktw.ToString("F1") : "—") + " |");
            }
            if (hasKado) kado.Add((name, kl / 4, kt / 4));
        }
        Console.WriteLine();
        int n = rows.Count;
        Console.WriteLine("| 波 | 勝率 L → T（" + n + " 行平均） | ツギが上 / リリが上（行数） | 全員生存 L → T |");
        Console.WriteLine("|--:|---|---|---|");
        foreach (int st in Waves)
            Console.WriteLine("| " + (st + 1) + " | " + F1(waveWin[st, 0] / n) + " → **" + F1(waveWin[st, 1] / n) + "** | " + tBetter[st] + " / " + lBetter[st] + " | "
                              + F1(waveClean[st, 0] / n) + " → **" + F1(waveClean[st, 1] / n) + "** |");
        Console.WriteLine();
        Console.WriteLine("カドの与ダメ/戦（第2〜5波平均）: " + string.Join(" ／ ", kado.Select(k => k.Row + " " + k.L.ToString("F1") + " → " + k.T.ToString("F1"))) + "。");
        Console.WriteLine();

        FireDiag();
        BeniDiag();
    }

    const string FireRow = "燃焼 (ボルグ×ホタ)";

    static void FireDiag()
    {
        Console.WriteLine("## §4.3 火の診断 —— `" + FireRow + "` のリリの席にツギ");
        Console.WriteLine();
        var (_, f) = CompareBuilds().First(r => r.Name == FireRow);
        int borgSlot = f.Occupied().First(o => o.Def.Id == "borg").Slot;
        var borgAdj = f.Occupied().Where(o => o.Slot != borgSlot && FormationRules.AreAdjacent(borgSlot, o.Slot)).Select(o => o.Def).ToList();
        Console.WriteLine("ボルグの隣の味方（編成の席）: " + string.Join("・", borgAdj.Select(d => d.Id == "lili" ? "リリ／ツギ（差し替えた席）" : d.Name)) + "。");
        Console.WriteLine("**燃えたT** ＝ ホタの燃焼の刻みのターン数/戦（`BurnTicks`）／ **ホタ与ダメ** ／ **ホタ倒れ** ＝ ホタが倒れた戦の割合 ／ "
                          + "**隣の燃焼** ＝ ボルグの隣の味方が燃焼の刻みで失った HP/戦（`BurnTaken`）・（ ）は破片が吸った量 ／ **倍** ＝ 板の印で燃焼が倍になった回数/戦。第2〜5波の平均。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 | 全員生存 | 燃えたT | ホタ与ダメ | ホタ倒れ | 隣の燃焼 | 倍 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|--:|");
        foreach (var (tag, d) in new[] { ("L（リリ）", (UnitDef?)null), ("T1", T1), ("T2", T2), ("T3", T3) })
        {
            Formation g = d is null ? f : Replace(f, u => u.Id == "lili", d);
            var ag = Waves.Select(st => Measure(FireRow + "|" + tag, g, st)).ToArray();
            double burnT = ag.Average(a => a.Per(a.Of("hota").BurnTicks)), hdmg = ag.Average(a => a.Per(a.Of("hota").DamageToEnemy));
            double hdead = ag.Average(a => 100.0 * a.Of("hota").Deaths / Math.Max(1, a.N));
            string adjId(UnitDef u) => u.Id == "lili" ? (d is null ? "lili" : "tsugi") : u.Id;
            double nb = ag.Average(a => a.Per(borgAdj.Sum(u => a.Of(adjId(u)).BurnTaken)));
            double nbs = ag.Average(a => a.Per(borgAdj.Sum(u => a.Of(adjId(u)).BurnSoaked)));
            double fl = ag.Average(a => a.Per(a.T.Values.Sum(t => t.PlankFlares)));
            Console.WriteLine("| " + tag + " | " + F1(ag.Average(a => a.Win)) + " | " + F1(ag.Average(a => a.CleanAll)) + " | " + burnT.ToString("F2") + " | "
                              + hdmg.ToString("F1") + " | " + F1(hdead) + "% | " + nb.ToString("F1") + "（" + nbs.ToString("F1") + "） | " + fl.ToString("F2") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("（ホタ倒れ は `Deaths` ÷ 戦で、蘇生されて再び倒れると2回に数える。）");
        Console.WriteLine();
    }

    const string BeniRow = "毒+ベニ+ラウ";

    static void BeniDiag()
    {
        Console.WriteLine("## §4.4 ベニの診断 —— `" + BeniRow + "` のベニの隣（前3 のスィド）をツギに");
        Console.WriteLine();
        Console.WriteLine("ベニの隣は3枚とも毒の書き手なので、どれを替えても毒は減る（R016）。中央のグザ（主な書き手）を残して前3 のスィドを替えた。"
                          + "**反転の回復** ＝ ベニの反転で味方が癒えた量/戦（毒 ＋ 火）／ **紅蓮** ＝ 放った回数/戦。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 | 全員生存 | 反転の回復 | 紅蓮 | 倍 | ベニ倒れ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        var (_, f) = CompareBuilds().First(r => r.Name == BeniRow);
        foreach (var (tag, d) in new[] { ("元（スィド）", (UnitDef?)null), ("T1", T1), ("T2", T2), ("T3", T3) })
        {
            Formation g = d is null ? f : Replace(f, u => u.Id == "sid", d);
            var ag = Waves.Select(st => Measure(BeniRow + "|" + tag, g, st)).ToArray();
            double inv = ag.Average(a => a.Per(a.Of("beni").InversePoisonHealed + a.Of("beni").InverseBurnHealed));
            double gu = ag.Average(a => a.Per(a.Of("beni").GurenFires));
            double fl = ag.Average(a => a.Per(a.T.Values.Sum(t => t.PlankFlares)));
            double bd = ag.Average(a => 100.0 * a.Of("beni").Deaths / Math.Max(1, a.N));
            Console.WriteLine("| " + tag + " | " + F1(ag.Average(a => a.Win)) + " | " + F1(ag.Average(a => a.CleanAll)) + " | " + inv.ToString("F1") + " | "
                              + gu.ToString("F2") + " | " + fl.ToString("F2") + " | " + F1(bd) + "% |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ledger —— §4.5 帳簿（1戦あたり）
    // =================================================================================

    static void RunLedger()
    {
        Console.WriteLine("# 第207期 `tsugi ledger` —— 帳簿（1戦あたり・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**貼った** 回/戦 ／ **破片** 貼った破片の合計/戦 ／ **厚さ** 1回あたり ／ **うち在庫** 在庫から上乗せした分/戦 ／ **受け止めた** 板の印を持つ駒の破片が吸った量/戦 ／ "
                          + "**拾い 砕け** 拾った砕けた破片の量（生・×50% の前）/戦 ／ **拾い 倒れ** 倒れた駒の数/戦 ／ **倍** 燃焼が倍になった回数/戦 ／ **ツギ倒れ** ツギが倒れた戦の割合 ／ "
                          + "**自分** 自分に貼った割合 ／ **拒否** 支援を受け付けない駒（ガルド）に貼った割合 ／ **渇き中** 渇きの保持者が生きている間に貼った回/戦。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 版 | 貼った | 破片 | 厚さ | うち在庫 | 受け止めた | 拾い 砕け | 拾い 倒れ | 倍 | ツギ倒れ | 自分 | 拒否 | 渇き中 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        void Line(string band, string name, string tag, Agg[] ag)
        {
            var t = new UnitTally(); int n = 0; double soak = 0, flare = 0; int died = 0;
            foreach (var a in ag)
            {
                t.Add(a.Of("tsugi")); n += a.N; died += a.TsugiDied;
                soak += a.T.Values.Sum(x => x.PlankSoaked); flare += a.T.Values.Sum(x => x.PlankFlares);
            }
            string P(double x) => (x / Math.Max(1, n)).ToString("F2");
            string Pc(double x, double d) => d == 0 ? "—" : (100.0 * x / d).ToString("F1") + "%";
            Console.WriteLine("| " + band + " | " + name + " | " + tag + " | " + P(t.PlankPastes) + " | " + P(t.PlankGiven) + " | "
                              + (t.PlankPastes == 0 ? "—" : ((double)t.PlankGiven / t.PlankPastes).ToString("F1")) + " | " + P(t.PlankStockUsed) + " | " + P(soak) + " | "
                              + P(t.ScrapArmor) + " | " + P(t.ScrapFalls) + " | " + P(flare) + " | " + (100.0 * died / Math.Max(1, n)).ToString("F1") + "% | "
                              + Pc(t.PlankSelf, t.PlankPastes) + " | " + Pc(t.PlankOnStoic, t.PlankPastes) + " | " + P(t.PlankInDrought) + " |");
        }
        foreach (var (name, f) in TsugiRows())
            foreach (var (tag, d) in Versions.Skip(1))
                Line("compare", name, tag, Waves.Select(st => Measure(name + "|" + tag, Apply(f, d), st)).ToArray());
        foreach (var (name, f) in LiliRows())
            Line("差し替え", name, "T3", Waves.Select(st => Measure(name + "|T", Replace(f, d => d.Id == "lili", T3), st)).ToArray());
        Console.WriteLine();

        Console.WriteLine("## 板の厚さを波ごとに（T1 → T3・ツギの在席 3 行 ＋ 差し替え 8 行を合算）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 厚さ T1 | 厚さ T3 | 上げ幅 | 拾い 砕け/戦（生） | 拾い 倒れ/戦 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|");
        foreach (int st in Waves)
        {
            var t1 = new UnitTally(); var t3 = new UnitTally(); int n3 = 0;
            foreach (var (name, f) in TsugiRows())
            {
                t1.Add(Measure(name + "|T1", Apply(f, T1), st).Of("tsugi"));
                var a3 = Measure(name + "|T3", Apply(f, T3), st); t3.Add(a3.Of("tsugi")); n3 += a3.N;
            }
            foreach (var (name, f) in LiliRows())
            {
                var a3 = Measure(name + "|T", Replace(f, d => d.Id == "lili", T3), st); t3.Add(a3.Of("tsugi")); n3 += a3.N;
            }
            double th1 = (double)t1.PlankGiven / Math.Max(1, t1.PlankPastes), th3 = (double)t3.PlankGiven / Math.Max(1, t3.PlankPastes);
            Console.WriteLine("| " + (st + 1) + " | " + th1.ToString("F1") + " | " + th3.ToString("F1") + " | " + D(th3 - th1) + " | "
                              + ((double)t3.ScrapArmor / Math.Max(1, n3)).ToString("F2") + " | " + ((double)t3.ScrapFalls / Math.Max(1, n3)).ToString("F2") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("（T1 はツギの在席 3 行だけ、T3 は在席 3 行 ＋ 差し替え 8 行。T1 と T3 の差には行の違いも入る。）");
        Console.WriteLine();
    }

    // =================================================================================
    // check —— 自己検査（§6）
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第207期 `tsugi check` —— 自己検査");
        Console.WriteLine();
        bool all = true;
        void Req(bool ok, string what) { all &= ok; Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + what); }

        // (a) T0 が第206期の表と一致（ツギの行は旧ナラに戻して比べる）。ツギを含まない行は `compare` の差分で見る（報告に書く）。
        string path = string.IsNullOrWhiteSpace(arg) ? ".tmp/balance206.md" : arg.Trim();
        if (File.Exists(path))
        {
            var old = File.ReadAllLines(path).Where(l => l.StartsWith("| ")).Select(l => l.Split('|').Select(c => c.Trim()).ToArray())
                          .Where(c => c.Length >= 8).ToDictionary(c => c[1].Replace("置き去り×", "継ぎ当て×"), c => c.Skip(2).Take(5).ToArray());
            int bad = 0, cells = 0;
            foreach (var (name, f) in TsugiRows())
            {
                if (!old.TryGetValue(name, out var cellsOld)) { bad++; continue; }
                Formation g = Apply(f, UnitCatalog.Nara);
                for (int st = 0; st < 5; st++)
                {
                    int wins = 0;
                    Formation enemy = EnemyCatalog.Stages[st].Enemy;
                    Parallel.For(0, 200, seed => { if (BattleEngine.Run(g, enemy, seed, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                    cells++;
                    if ((100.0 * wins / 200).ToString("F1") + "%" != cellsOld[st]) bad++;
                }
            }
            Req(bad == 0, "(a) T0（旧ナラに戻す）が第206期の表と一致: " + cells + " セル中ずれ " + bad);
        }
        else Console.WriteLine("- (a) 第206期の表が無い（" + path + "）");

        // (b) 同値割り以外で乱数を引かない: 同じ seed を2回回して台本が同じ・T1 の版と verbose の有無で勝敗が同じ。
        int mism = 0;
        foreach (var (name, f) in TsugiRows())
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 40; seed++)
                {
                    var a = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    var b = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) mism++;
                }
        Req(mism == 0, "(b) verbose の有無で勝敗・決着T が同じ（イベントを積む処理が盤面を動かさない）: ずれ " + mism);
        Console.WriteLine("  - 新しい処理が乱数を引くのは `PlankTrait` の同値割り（`ctx.PickOne`・候補1なら引かない）だけ（コードで確認）。");

        // (c) 台本と最終状態の検査（T3・ツギの在席行 ＋ 差し替え行・第2〜5波・seed 0..39）
        long drought = 0, stoic = 0, pastes = 0, flaresT1 = 0, flaresT3 = 0, markNoArmor = 0, stockBad = 0, deadPick = 0, scraps = 0;
        var tables = TsugiRows().Select(r => r.F).Concat(LiliRows().Select(r => Replace(r.F, d => d.Id == "lili", T3))).ToList();
        foreach (Formation f in tables)
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 40; seed++)
                {
                    var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
                    var r = BattleEngine.Run(players, enemies, seed, verbose: true);
                    foreach (UnitState u in players)
                        if (u.RawCounter(StatusKeys.Plank) > 0 && u.RawCounter(StatusKeys.Armor) <= 0) markNoArmor++;
                    if (r.TallyByUnit.TryGetValue("tsugi", out var tt))
                    {
                        drought += tt.PlankInDrought; stoic += tt.PlankOnStoic; pastes += tt.PlankPastes;
                    }
                    flaresT3 += r.TallyByUnit.Values.Sum(t => t.PlankFlares);
                    // 在庫は貼ったら 0: 貼った直後の最初の拾いは、その拾いの量＝在庫の合計。倒れた後は拾わない（蘇生されたら再開）。
                    var ts = players.Where(p => p.Def.Id == "tsugi").Select(p => p.InstanceId).ToHashSet();
                    bool justPasted = true, alive = true;
                    foreach (var e in r.Events)
                    {
                        if (e.Kind == BattleEventKind.Death && e.TargetId is int dt && ts.Contains(dt)) alive = false;
                        if (e.Kind == BattleEventKind.Revive && e.TargetId is int rt && ts.Contains(rt)) alive = true;
                        if (e.Kind != BattleEventKind.Plank || e.ActorId is not int a || !ts.Contains(a)) continue;
                        if (e.Text == PlankLabels.Paste) justPasted = true;
                        else if (e.Text == PlankLabels.Scrap)
                        {
                            scraps++;
                            if (!alive) deadPick++;
                            if (justPasted && e.StatusRemaining != e.Amount) stockBad++;
                            justPasted = false;
                        }
                    }
                }
        // T1 の版は燃えやすくない（燃焼が倍にならない）: 火の行で確かめる
        var (_, fire) = CompareBuilds().First(r => r.Name == FireRow);
        foreach (var d in new[] { T1 })
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 100; seed++)
                    flaresT1 += BattleEngine.Run(Replace(fire, u => u.Id == "lili", d), EnemyCatalog.Stages[st].Enemy, seed, verbose: false)
                                            .TallyByUnit.Values.Sum(t => t.PlankFlares);
        long flaresT2 = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < 100; seed++)
                flaresT2 += BattleEngine.Run(Replace(fire, u => u.Id == "lili", T2), EnemyCatalog.Stages[st].Enemy, seed, verbose: false)
                                        .TallyByUnit.Values.Sum(t => t.PlankFlares);
        Req(drought > 0, "(c1) 渇きの下でも板が貼られる: 渇きの保持者が生きている間に貼った回数 " + drought + " / 貼った " + pastes);
        Req(stoic > 0, "(c2) 支援拒否の駒（ガルド）にも貼られる: " + stoic + " 回");
        Req(markNoArmor == 0, "(c3) 板の印は破片 0 で消える（戦の終わりに印があって破片が 0 の駒）: " + markNoArmor);
        Req(flaresT1 == 0 && flaresT2 > 0, "(c4) 燃焼の倍は燃えやすい板の印があるときだけ: T1 の版 " + flaresT1 + " 回 ／ T2 の版 " + flaresT2 + " 回（火の行・seed 0..99）");
        Req(stockBad == 0 && scraps > 0, "(c5) 在庫は貼ったら 0 に戻る（貼った直後の最初の拾いで 量 ＝ 在庫）: 拾い " + scraps + " 件中ずれ " + stockBad);
        Req(deadPick == 0, "(c6) ツギが倒れた後は拾わない（蘇生されるまで）: " + deadPick + " 件");
        Console.WriteLine();
        Console.WriteLine(all ? "**全項目 ○。**" : "**× がある。**");
    }
}
