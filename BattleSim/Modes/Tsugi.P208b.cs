using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第208期）の本体 —— 版 U0〜U3 の対照・差し替え・反射の帳簿・火とベニ・自己検査
//
//     dotnet run --project BattleSim -c Release 0 tsugi run2     # ツギの在席 3 行 × U0〜U3
//     dotnet run --project BattleSim -c Release 0 tsugi swap2    # リリの席にツギ（L / U0〜U3）＋ 火の診断（U1 / U2）＋ ベニ（U1 / U2）
//     dotnet run --project BattleSim -c Release 0 tsugi ledger2  # 反射の帳簿（U1〜U3）
//     dotnet run --project BattleSim -c Release 0 tsugi check2 [第207期のbalance.md]  # 自己検査
//
// 版は札の差し替えだけ: ツギ U0 ＝ [Plank, PlankTinder, Scrap]（第207期 T3）／ U1 ＝ U0 ＋ PlankRebound ／
// U2 ＝ [Plank, PlankScorch, Scrap, PlankRebound] ／ U3 ＝ U2 ＋ ドハの SharerArmored（＝ `UnitCatalog` の規定）。
// U0〜U2 のドハは `SharerArmored` を外した写し。
// =====================================================================================

static partial class TsugiDiag
{
    static readonly UnitDef U0 = Clone(UnitCatalog.Tsugi, new[] { TraitId.Plank, TraitId.PlankTinder, TraitId.Scrap });
    static readonly UnitDef U1 = Clone(UnitCatalog.Tsugi, new[] { TraitId.Plank, TraitId.PlankTinder, TraitId.Scrap, TraitId.PlankRebound });
    static readonly UnitDef U2 = Clone(UnitCatalog.Tsugi, new[] { TraitId.Plank, TraitId.PlankScorch, TraitId.Scrap, TraitId.PlankRebound });
    // 第209期に規定のツギ（R2）が変わったので、第208期の U3 は札で写す。
    static readonly UnitDef U3 = Clone(UnitCatalog.Tsugi, new[] { TraitId.Plank, TraitId.PlankScorch, TraitId.Scrap, TraitId.PlankRebound });
    static readonly UnitDef DohaOld = Clone(UnitCatalog.Doha, new[] { TraitId.Sharer });

    static readonly (string Tag, UnitDef Tsugi, UnitDef Doha)[] UVersions =
    {
        ("U0", U0, DohaOld), ("U1", U1, DohaOld), ("U2", U2, DohaOld), ("U3", U3, UnitCatalog.Doha),
    };

    /// <summary>ツギ（または <paramref name="seat"/> の駒）とドハを版の定義に差し替える。</summary>
    static Formation ApplyU(Formation f, string seat, UnitDef tsugi, UnitDef doha)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef u) in f.Occupied())
            g[slot] = u.Id == seat ? tsugi : u.Id == "doha" ? doha : u;
        return g;
    }

    static partial void RunMore209(string mode, string arg, ref bool handled);

    static partial void RunMore208(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run2": Run208(); handled = true; return;
            case "swap2": Swap208(); handled = true; return;
            case "ledger2": Ledger208(); handled = true; return;
            case "check2": Check208(arg); handled = true; return;
            default: RunMore209(mode, arg, ref handled); return;
        }
    }

    // =================================================================================
    // run2 —— §4.1
    // =================================================================================

    static void Run208()
    {
        Console.WriteLine("# 第208期 `tsugi run2` —— ツギの在席 3 行 × U0〜U3（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("セルは第2 / 3 / 4 / 5 波・seed 0..199。**全員生存** ＝ 全員生存勝ち（全戦に占める割合）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 勝率 | 平均 | 全員生存 | 平均 | 倒れた | 決着T | 膠着 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|");
        var sumW = new Dictionary<string, double>(); var sumC = new Dictionary<string, double>();
        int n = 0;
        foreach (var (name, f) in TsugiRows())
        {
            n++;
            foreach (var (tag, t, d) in UVersions)
            {
                var ag = Waves.Select(st => Measure(name + "|" + tag, ApplyU(f, "tsugi", t, d), st)).ToArray();
                double mw = ag.Average(x => x.Win), mc = ag.Average(x => x.CleanAll);
                sumW[tag] = sumW.GetValueOrDefault(tag) + mw; sumC[tag] = sumC.GetValueOrDefault(tag) + mc;
                Console.WriteLine("| " + name + " | " + tag + " | " + string.Join(" / ", ag.Select(x => F1(x.Win))) + " | **" + F1(mw) + "** | "
                                  + string.Join(" / ", ag.Select(x => F1(x.CleanAll))) + " | **" + F1(mc) + "** | "
                                  + ag.Average(x => x.Fallen).ToString("F2") + " | " + ag.Average(x => x.Turns).ToString("F2") + " | " + ag.Sum(x => x.Stall) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("在席 " + n + " 行の平均（勝率 ／ 全員生存）: " + string.Join(" ・ ", UVersions.Select(v => v.Tag + " " + F1(sumW[v.Tag] / n) + " / " + F1(sumC[v.Tag] / n))) + "。");
        Console.WriteLine("差（勝率）: U1 − U0 " + D((sumW["U1"] - sumW["U0"]) / n) + " ／ U2 − U1 " + D((sumW["U2"] - sumW["U1"]) / n) + " ／ U3 − U2 " + D((sumW["U3"] - sumW["U2"]) / n)
                          + "・（全員生存）" + D((sumC["U1"] - sumC["U0"]) / n) + " ／ " + D((sumC["U2"] - sumC["U1"]) / n) + " ／ " + D((sumC["U3"] - sumC["U2"]) / n) + "。");
        Console.WriteLine();
    }

    // =================================================================================
    // swap2 —— §4.2・§4.4・§4.5
    // =================================================================================

    static void Swap208()
    {
        Console.WriteLine("# 第208期 `tsugi swap2` —— リリの席にツギ（L ＝ リリ・第206期の規定 ／ U0〜U3）");
        Console.WriteLine();
        Console.WriteLine("セルは **勝率 / 全員生存**（全戦に占める割合）。seed 0..199。**L のドハは規定（U3 の分かち）**——リリの行では分かちの段の位置は破片が無ければ効かない。");
        Console.WriteLine();
        var rows = LiliRows().ToList();
        var tags = new[] { "L", "U0", "U1", "U2", "U3" };
        Agg Get(string name, Formation f, string tag, int st)
        {
            if (tag == "L") return Measure(name + "|L", f, st);
            var v = UVersions.First(x => x.Tag == tag);
            return Measure(name + "|S" + tag, ApplyU(f, "lili", v.Tsugi, v.Doha), st);
        }
        Console.WriteLine("| 行 | 波 | L | U0 | U1 | U2 | U3 |");
        Console.WriteLine("|---|--:|---|---|---|---|---|");
        var beat = tags.ToDictionary(t => t, _ => 0);
        var cleanSum = new Dictionary<(string, int), double>();
        var winSum = new Dictionary<(string, int), double>();
        foreach (var (name, f) in rows)
            foreach (int st in Waves)
            {
                var a = tags.ToDictionary(t => t, t => Get(name, f, t, st));
                foreach (var t in tags)
                {
                    cleanSum[(t, st)] = cleanSum.GetValueOrDefault((t, st)) + a[t].CleanAll;
                    winSum[(t, st)] = winSum.GetValueOrDefault((t, st)) + a[t].Win;
                    if (t != "L" && a[t].Win > a["L"].Win) beat[t]++;
                }
                Console.WriteLine("| " + name + " | " + (st + 1) + " | " + string.Join(" | ", tags.Select(t => F1(a[t].Win) + " / " + F1(a[t].CleanAll))) + " |");
            }
        Console.WriteLine();
        int n = rows.Count;
        Console.WriteLine("| 波 | " + string.Join(" | ", tags.Select(t => t + " 勝率 / 全員生存")) + " |");
        Console.WriteLine("|--:|" + string.Concat(tags.Select(_ => "---|")));
        foreach (int st in Waves)
            Console.WriteLine("| " + (st + 1) + " | " + string.Join(" | ", tags.Select(t => F1(winSum[(t, st)] / n) + " / " + F1(cleanSum[(t, st)] / n))) + " |");
        Console.WriteLine();
        Console.WriteLine("**ツギがリリを勝率で上回る行・波（32 セル中）**: " + string.Join(" ／ ", tags.Skip(1).Select(t => t + " " + beat[t])) + "。");
        Console.WriteLine();

        Console.WriteLine("## 決着ターン・膠着・カド・ドハ（第2〜5波の平均 ／ 合計）");
        Console.WriteLine();
        Console.WriteLine("**決着T** は平均、**膠着** は 800 戦中の 30 ターン上限、**カド** はカドの敵への与ダメ/戦、**ドハ第四波** は第四波でドハが倒れた戦（200 戦中）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 決着T " + string.Join(" / ", tags) + " | 膠着 " + string.Join(" / ", tags) + " | カド " + string.Join(" / ", tags) + " | ドハ第四波 " + string.Join(" / ", tags) + " |");
        Console.WriteLine("|---|---|---|---|---|");
        int shrink = 0;
        foreach (var (name, f) in rows)
        {
            var ag = tags.ToDictionary(t => t, t => Waves.Select(st => Get(name, f, t, st)).ToArray());
            bool kado = f.Occupied().Any(o => o.Def.Id == "kado"), doha = f.Occupied().Any(o => o.Def.Id == "doha");
            double T(string t) => ag[t].Average(x => x.Turns);
            if (T("U1") < T("U0")) shrink++;
            Console.WriteLine("| " + name + " | " + string.Join(" / ", tags.Select(t => T(t).ToString("F1"))) + " | "
                              + string.Join(" / ", tags.Select(t => ag[t].Sum(x => x.Stall))) + " | "
                              + (kado ? string.Join(" / ", tags.Select(t => ag[t].Average(x => x.Per(x.Of("kado").DamageToEnemy)).ToString("F0"))) : "—") + " | "
                              + (doha ? string.Join(" / ", tags.Select(t => ag[t][2].FallenBy.GetValueOrDefault("doha").ToString())) : "—") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("決着ターンが U0 より U1 で縮んだ行: **" + shrink + " / " + n + "**。");
        Console.WriteLine();

        Fire208();
        Beni208();
    }

    static void Fire208()
    {
        Console.WriteLine("## §4.4 火の診断 —— `" + FireRow + "` のリリの席にツギ（U1 ／ U2）");
        Console.WriteLine();
        var (_, f) = CompareBuilds().First(r => r.Name == FireRow);
        int borgSlot = f.Occupied().First(o => o.Def.Id == "borg").Slot;
        var adj = f.Occupied().Where(o => o.Slot != borgSlot && FormationRules.AreAdjacent(borgSlot, o.Slot)).Select(o => o.Def.Id == "lili" ? "tsugi" : o.Def.Id).ToList();
        Console.WriteLine("**焼け落ちた板** ＝ 味方の破片が燃焼の刻みで吸われた量/戦（`BurnSoaked`）／ **隣の燃焼** ＝ ボルグの隣の味方（ツギ・ホタ）が燃焼の刻みで失った HP/戦 ／ **倍** ＝ 燃焼の刻みが倍になった回数/戦（U2）。第2〜5波。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 | 全員生存 | 焼け落ちた板 | 隣の燃焼 | ホタの燃えたT | ホタ与ダメ | 倍 | 反射/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var tag in new[] { "U1", "U2" })
        {
            var v = UVersions.First(x => x.Tag == tag);
            var ag = Waves.Select(st => Measure(FireRow + "|F" + tag, ApplyU(f, "lili", v.Tsugi, v.Doha), st)).ToArray();
            Console.WriteLine("| " + tag + " | " + F1(ag.Average(a => a.Win)) + " | " + F1(ag.Average(a => a.CleanAll)) + " | "
                              + ag.Average(a => a.Per(a.T.Values.Sum(t => t.BurnSoaked))).ToString("F1") + " | "
                              + ag.Average(a => a.Per(adj.Sum(id => a.Of(id).BurnTaken))).ToString("F1") + " | "
                              + ag.Average(a => a.Per(a.Of("hota").BurnTicks)).ToString("F2") + " | " + ag.Average(a => a.Per(a.Of("hota").DamageToEnemy)).ToString("F1") + " | "
                              + ag.Average(a => a.Per(a.T.Values.Sum(t => t.PlankScorched))).ToString("F2") + " | "
                              + ag.Average(a => a.Per(a.T.Values.Sum(t => t.ReflectCount))).ToString("F2") + " |");
        }
        Console.WriteLine();
    }

    static void Beni208()
    {
        Console.WriteLine("## §4.5 ベニの診断 —— `" + BeniRow + "` の前3（スィド）をツギに（U1 ／ U2）");
        Console.WriteLine();
        var (_, f) = CompareBuilds().First(r => r.Name == BeniRow);
        Console.WriteLine("| 版 | 勝率 | 全員生存 | 反転の回復/戦（毒 ／ 火） | 紅蓮/戦 | 倍 | ベニ倒れ |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|--:|");
        foreach (var tag in new[] { "U1", "U2" })
        {
            var v = UVersions.First(x => x.Tag == tag);
            var ag = Waves.Select(st => Measure(BeniRow + "|B" + tag, ApplyU(f, "sid", v.Tsugi, v.Doha), st)).ToArray();
            Console.WriteLine("| " + tag + " | " + F1(ag.Average(a => a.Win)) + " | " + F1(ag.Average(a => a.CleanAll)) + " | "
                              + ag.Average(a => a.Per(a.Of("beni").InversePoisonHealed)).ToString("F1") + " ／ " + ag.Average(a => a.Per(a.Of("beni").InverseBurnHealed)).ToString("F1") + " | "
                              + ag.Average(a => a.Per(a.Of("beni").GurenFires)).ToString("F2") + " | "
                              + ag.Average(a => a.Per(a.T.Values.Sum(t => t.PlankScorched))).ToString("F2") + " | "
                              + F1(ag.Average(a => 100.0 * a.FallenBy.GetValueOrDefault("beni") / Math.Max(1, a.N))) + "% |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ledger2 —— §4.3 反射の帳簿
    // =================================================================================

    static void Ledger208()
    {
        Console.WriteLine("# 第208期 `tsugi ledger2` —— 反射の帳簿（1戦あたり・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**回** 返した回数/戦 ／ **量** 返した量（砕けた破片）/戦 ／ **削った** 実際に削った HP/戦 ／ **倒した** 反射で倒れた敵/戦 ／ **空振り** 殴った敵が先に倒れて返らなかった量/戦 ／ "
                          + "**混ざった** ほかの書き手の破片も受けた板からの反射の割合 ／ **本数** 1回の敵の攻撃で返った本数 1・2・3・4以上の割合 ／ **主が倒れた** その攻撃の主が倒れた割合（本数ごと）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 版 | 回 | 量 | 削った | 倒した | 空振り | 混ざった | 本数 1/2/3/4+ | 主が倒れた 1/2/3/4+ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|---|---|");
        void Line(string band, string name, string tag, IEnumerable<Agg> ags)
        {
            var sum = new UnitTally(); var ts = new UnitTally(); int n = 0;
            foreach (var a in ags)
            {
                n += a.N;
                foreach (var t in a.T.Values) sum.Add(t);
                ts.Add(a.Of("tsugi"));
            }
            string P(double x) => (x / Math.Max(1, n)).ToString("F2");
            long[] h = ts.ReflectGroupHist ?? new long[4], k = ts.ReflectGroupKilled ?? new long[4];
            long hs = h.Sum();
            Console.WriteLine("| " + band + " | " + name + " | " + tag + " | " + P(sum.ReflectCount) + " | " + P(sum.ReflectNominal) + " | " + P(sum.ReflectDealt) + " | "
                              + P(sum.ReflectKills) + " | " + P(sum.ReflectWasted) + " | "
                              + (sum.ReflectCount == 0 ? "—" : (100.0 * sum.ReflectMixed / sum.ReflectCount).ToString("F0") + "%") + " | "
                              + (hs == 0 ? "—" : string.Join("/", h.Select(x => (100.0 * x / hs).ToString("F0")))) + " | "
                              + (hs == 0 ? "—" : string.Join("/", h.Zip(k).Select(p => p.First == 0 ? "—" : (100.0 * p.Second / p.First).ToString("F0")))) + " |");
        }
        foreach (var (name, f) in TsugiRows())
            foreach (var (tag, t, d) in UVersions.Skip(1))
                Line("compare", name, tag, Waves.Select(st => Measure(name + "|" + tag, ApplyU(f, "tsugi", t, d), st)));
        foreach (var (name, f) in LiliRows())
            Line("差し替え", name, "U3", Waves.Select(st => Measure(name + "|SU3", ApplyU(f, "lili", U3, UnitCatalog.Doha), st)));
        Console.WriteLine();

        Console.WriteLine("## 波ごと（U3・ツギの在席 3 行 ＋ 差し替え 8 行を合算）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 回/戦 | 量/回 | 倒した/戦 | 本数 1/2/3/4+ | 主が倒れた 1/2/3/4+ |");
        Console.WriteLine("|--:|--:|--:|--:|---|---|");
        foreach (int st in Waves)
        {
            var ags = TsugiRows().Select(r => Measure(r.Name + "|U3", ApplyU(r.F, "tsugi", U3, UnitCatalog.Doha), st))
                .Concat(LiliRows().Select(r => Measure(r.Name + "|SU3", ApplyU(r.F, "lili", U3, UnitCatalog.Doha), st))).ToList();
            var sum = new UnitTally(); var ts = new UnitTally(); int n = 0;
            foreach (var a in ags) { n += a.N; foreach (var t in a.T.Values) sum.Add(t); ts.Add(a.Of("tsugi")); }
            long[] h = ts.ReflectGroupHist ?? new long[4], k = ts.ReflectGroupKilled ?? new long[4];
            long hs = h.Sum();
            Console.WriteLine("| " + (st + 1) + " | " + ((double)sum.ReflectCount / n).ToString("F2") + " | "
                              + (sum.ReflectCount == 0 ? "—" : ((double)sum.ReflectNominal / sum.ReflectCount).ToString("F1")) + " | "
                              + ((double)sum.ReflectKills / n).ToString("F2") + " | "
                              + (hs == 0 ? "—" : string.Join("/", h.Select(x => (100.0 * x / hs).ToString("F0")))) + " | "
                              + (hs == 0 ? "—" : string.Join("/", h.Zip(k).Select(p => p.First == 0 ? "—" : (100.0 * p.Second / p.First).ToString("F0")))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## U3 の分かち（ドハのいる行・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 破片の後ろで肩代わりした回/戦 U2 → U3 | ドハが倒れた戦 U2 → U3（800 戦中） |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (name, f) in CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "doha")))
        {
            bool li = f.Occupied().Any(o => o.Def.Id == "lili");
            string seat = li ? "lili" : "tsugi";
            string k2 = li ? "|SU2" : "|U2c", k3 = li ? "|SU3" : "|U3c";
            var a2 = Waves.Select(st => Measure(name + k2, ApplyU(f, seat, U2, DohaOld), st)).ToArray();
            var a3 = Waves.Select(st => Measure(name + k3, ApplyU(f, seat, U3, UnitCatalog.Doha), st)).ToArray();
            Console.WriteLine("| " + (li ? "差し替え" : "compare") + " | " + name + " | " + a2.Average(a => a.Per(a.Of("doha").SharerLate)).ToString("F2") + " → "
                              + a3.Average(a => a.Per(a.Of("doha").SharerLate)).ToString("F2") + " | " + a2.Sum(a => a.FallenBy.GetValueOrDefault("doha")) + " → "
                              + a3.Sum(a => a.FallenBy.GetValueOrDefault("doha")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("（`compare` の行はリリもツギもいない行なので、ツギの差し替えは起きず、ドハの版だけが違う。）");
        Console.WriteLine();
    }

    // =================================================================================
    // check2 —— §6
    // =================================================================================

    static void Check208(string arg)
    {
        Console.WriteLine("# 第208期 `tsugi check2` —— 自己検査");
        Console.WriteLine();
        bool all = true;
        void Req(bool ok, string what) { all &= ok; Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + what); }

        // (a) U0 が第207期の表と一致 ／ U2（ドハは旧）でツギを含まない行が第207期と一致
        string path = string.IsNullOrWhiteSpace(arg) ? ".tmp/balance207h.md" : arg.Trim();
        if (File.Exists(path))
        {
            var old = File.ReadAllLines(path).Where(l => l.StartsWith("| ")).Select(l => l.Split('|').Select(c => c.Trim()).ToArray())
                          .Where(c => c.Length >= 8).GroupBy(c => c[1]).ToDictionary(g => g.Key, g => g.First().Skip(2).Take(5).ToArray());
            int badU0 = 0, cellsU0 = 0, badOther = 0, cellsOther = 0;
            foreach (var (name, f) in CompareBuilds())
            {
                if (!old.TryGetValue(name, out var o)) { badOther++; continue; }
                bool ts = HasTsugi(f);
                Formation g = ts ? ApplyU(f, "tsugi", U0, DohaOld) : ApplyU(f, "tsugi", U2, DohaOld);
                for (int st = 0; st < 5; st++)
                {
                    int wins = 0;
                    Formation enemy = EnemyCatalog.Stages[st].Enemy;
                    Parallel.For(0, 200, seed => { if (BattleEngine.Run(g, enemy, seed, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                    bool same = (100.0 * wins / 200).ToString("F1") + "%" == o[st];
                    if (ts) { cellsU0++; if (!same) badU0++; } else { cellsOther++; if (!same) badOther++; }
                }
            }
            Req(badU0 == 0, "(a1) U0 が第207期の表と一致（ツギの 3 行）: " + cellsU0 + " セル中ずれ " + badU0);
            Req(badOther == 0, "(a2) ドハを旧に戻すと、ツギを含まない行が第207期と一致（U0〜U2 は動かない）: " + cellsOther + " セル中ずれ " + badOther);
        }
        else Console.WriteLine("- (a) 第207期の表が無い（" + path + "）");

        // (b) verbose の有無で勝敗・決着T が同じ
        int mism = 0;
        foreach (var (name, f) in TsugiRows().Concat(LiliRows().Select(r => (r.Name, ApplyU(r.F, "lili", U3, UnitCatalog.Doha)))))
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 30; seed++)
                {
                    var a = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    var b = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) mism++;
                }
        Req(mism == 0, "(b) verbose の有無で勝敗・決着T が同じ（台本の処理が盤面を動かさない）: ずれ " + mism);
        Console.WriteLine("  - 新しい処理は乱数を1つも引かない（反射・燃焼の倍・分かちの後置。分かちの `PickOne` は元の位置のまま）。");

        // (c) 台本と帳簿
        long reflects = 0, noMark = 0, notFoe = 0, overLost = 0, yokeOver = 0, yokeRefl = 0, serialGroups = 0, multi = 0;
        var tables = TsugiRows().Select(r => ApplyU(r.F, "tsugi", U3, UnitCatalog.Doha))
            .Concat(LiliRows().Select(r => ApplyU(r.F, "lili", U3, UnitCatalog.Doha))).ToList();
        foreach (Formation f in tables)
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 40; seed++)
                {
                    var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
                    var r = BattleEngine.Run(players, enemies, seed, verbose: true);
                    var foes = enemies.Select(e => e.InstanceId).ToHashSet();
                    var planked = new HashSet<int>();
                    var bySerial = new Dictionary<int, int>();
                    for (int i = 0; i < r.Events.Count; i++)
                    {
                        var e = r.Events[i];
                        if (e.Kind != BattleEventKind.Plank) continue;
                        if (e.Text == PlankLabels.Paste && e.TargetId is int pt) planked.Add(pt);
                        if (e.Text != PlankLabels.Reflect) continue;
                        reflects++;
                        if (e.ActorId is not int h || !planked.Contains(h)) noMark++;
                        if (e.TargetId is not int ft || !foes.Contains(ft)) notFoe++;
                        if (e.Slot > 0) bySerial[e.Slot] = bySerial.GetValueOrDefault(e.Slot) + 1;
                        if (st == 3)
                        {
                            // 第四波（軛）: 反射の直後の敵への Damage は 25 を超えない
                            yokeRefl++;
                            var d = r.Events.Skip(i + 1).FirstOrDefault(x => x.Kind == BattleEventKind.Damage && x.TargetId == e.TargetId);
                            if (d is not null && d.Amount > 25) yokeOver++;
                        }
                    }
                    serialGroups += bySerial.Count; multi += bySerial.Count(kv => kv.Value >= 2);
                    foreach (var t in r.TallyByUnit.Values)
                        if (t.ReflectNominal + t.ReflectWasted > t.PlankSoaked) overLost++;
                }
        Req(reflects > 0 && noMark == 0, "(c1) 板の印の無い味方は返さない（返した駒は全員、その戦で板を貼られている）: 反射 " + reflects + " 件中 " + noMark);
        Req(notFoe == 0, "(c2) 返す先は必ず敵（毒・燃焼の刻みや味方の攻撃では返さない）: " + notFoe);
        Req(overLost == 0, "(c3) 返した量は、印がある間に失った破片の量を超えない（駒ごと）: 超えた駒 " + overLost);
        Req(yokeRefl > 0 && yokeOver == 0, "(c4) 第四波（軛）の反射は1回 25 以下: " + yokeRefl + " 件中 25 超 " + yokeOver);
        Req(multi > 0, "(c5) 同じ攻撃の反射に同じ通し番号が付く（台本）: 攻撃 " + serialGroups + " 回のうち 2 本以上返った " + multi + " 回");

        // (d) 燃焼の倍は印（U2 以降）のときだけ ／ U3 の分かちは破片の後ろ
        var (_, fire) = CompareBuilds().First(x => x.Name == FireRow);
        long sc1 = 0, sc2 = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < 100; seed++)
            {
                sc1 += BattleEngine.Run(ApplyU(fire, "lili", U1, DohaOld), EnemyCatalog.Stages[st].Enemy, seed, verbose: false).TallyByUnit.Values.Sum(t => t.PlankScorched);
                sc2 += BattleEngine.Run(ApplyU(fire, "lili", U2, DohaOld), EnemyCatalog.Stages[st].Enemy, seed, verbose: false).TallyByUnit.Values.Sum(t => t.PlankScorched);
            }
        Req(sc1 == 0 && sc2 > 0, "(d1) 燃焼の刻みの倍は燃えやすい板（ダメージ倍）の印があるときだけ: U1 " + sc1 + " 回 ／ U2 " + sc2 + " 回（火の行・seed 0..99）");
        long late2 = 0, late3 = 0;
        foreach (var (name, f) in LiliRows().Where(r => r.F.Occupied().Any(o => o.Def.Id == "doha")))
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    late2 += BattleEngine.Run(ApplyU(f, "lili", U2, DohaOld), EnemyCatalog.Stages[st].Enemy, seed, verbose: false).TallyByUnit.GetValueOrDefault("doha")?.SharerLate ?? 0;
                    late3 += BattleEngine.Run(ApplyU(f, "lili", U3, UnitCatalog.Doha), EnemyCatalog.Stages[st].Enemy, seed, verbose: false).TallyByUnit.GetValueOrDefault("doha")?.SharerLate ?? 0;
                }
        Req(late2 == 0 && late3 > 0, "(d2) U3 でドハの取り分は殴られた味方の破片の段を通った後（U2 " + late2 + " 回 ／ U3 " + late3 + " 回）");
        Console.WriteLine();
        Console.WriteLine(all ? "**全項目 ○。**" : "**× がある。**");
    }
}
