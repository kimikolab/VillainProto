using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第211期）の本体 —— run5 / swap5 / ledger5 / y3diff / check5
// 版の定義（Y0〜Y3・`ApplyY`・`YRows`）は Tsugi.P211.cs。
// =====================================================================================

static partial class TsugiDiag
{
    static partial void RunMore211b(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run5": Run211(); handled = true; return;
            case "swap5": Swap211(); handled = true; return;
            case "ledger5": Ledger211(); handled = true; return;
            case "y3diff": Y3Diff(); handled = true; return;
            case "check5": Check211(arg); handled = true; return;
            case "yokeprobe": YokeProbe(); handled = true; return;
        }
    }

    // =================================================================================
    // run5 —— §4.1
    // =================================================================================

    static void Run211()
    {
        Console.WriteLine("# 第211期 `tsugi run5` —— ツギの在席 3 行 × Y0〜Y3（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("セルは第2 / 3 / 4 / 5 波・seed 0..199。**全員生存** ＝ 全員生存勝ち（全戦に占める割合）。**膠着** は 800 戦中の 30 ターン上限。");
        Console.WriteLine("**Y0** ＝ 第210期 W3 ／ **Y1** ＝ 板を実質の残り体力が最も低い味方へ ／ **Y2** ＝ ＋ 応急処置を (HP＋破片) で ／ **Y3**（規定）＝ ＋ カドの棘は破片で受けても鳴る。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 勝率 | 平均 | 全員生存 | 平均 | 倒れた | 決着T | 膠着 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|");
        var sumW = new Dictionary<string, double>(); var sumC = new Dictionary<string, double>(); var sumT = new Dictionary<string, double>(); var sumF = new Dictionary<string, double>();
        int n = 0;
        foreach (var (name, _) in TsugiRows())
        {
            n++;
            foreach (var tag in YTags)
            {
                var r = YRows(tag).First(x => x.Band == "compare" && x.Name == name);
                var ag = Waves.Select(r.At).ToArray();
                double mw = ag.Average(x => x.Win), mc = ag.Average(x => x.CleanAll), mt = ag.Average(x => x.Turns), mf = ag.Average(x => x.Fallen);
                sumW[tag] = sumW.GetValueOrDefault(tag) + mw; sumC[tag] = sumC.GetValueOrDefault(tag) + mc;
                sumT[tag] = sumT.GetValueOrDefault(tag) + mt; sumF[tag] = sumF.GetValueOrDefault(tag) + mf;
                Console.WriteLine("| " + name + " | " + tag + " | " + string.Join(" / ", ag.Select(x => F1(x.Win))) + " | **" + F1(mw) + "** | "
                                  + string.Join(" / ", ag.Select(x => F1(x.CleanAll))) + " | **" + F1(mc) + "** | "
                                  + mf.ToString("F2") + " | " + mt.ToString("F2") + " | " + ag.Sum(x => x.Stall) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("在席 " + n + " 行の平均（勝率 ／ 全員生存 ／ 倒れた ／ 決着T）: "
                          + string.Join(" ・ ", YTags.Select(t => t + " " + F1(sumW[t] / n) + " / " + F1(sumC[t] / n) + " / " + (sumF[t] / n).ToString("F2") + " / " + (sumT[t] / n).ToString("F2"))) + "。");
        Console.WriteLine();
        Console.WriteLine("差（勝率 ／ 全員生存）: Y1 − Y0 " + D((sumW["Y1"] - sumW["Y0"]) / n) + " / " + D((sumC["Y1"] - sumC["Y0"]) / n)
                          + " ・ Y2 − Y1 " + D((sumW["Y2"] - sumW["Y1"]) / n) + " / " + D((sumC["Y2"] - sumC["Y1"]) / n)
                          + " ・ Y3 − Y2 " + D((sumW["Y3"] - sumW["Y2"]) / n) + " / " + D((sumC["Y3"] - sumC["Y2"]) / n) + "。");
        Console.WriteLine();
    }

    // =================================================================================
    // swap5 —— §4.2
    // =================================================================================

    static void Swap211()
    {
        Console.WriteLine("# 第211期 `tsugi swap5` —— リリの席にツギ（L ＝ リリ・第206期の規定 ／ Y0〜Y3）");
        Console.WriteLine();
        Console.WriteLine("セルは **勝率 / 全員生存**（全戦に占める割合）。seed 0..199。**L のカドは第210期のカド**（Y3 のカドと比べるのは Y3 の列だけ）。");
        Console.WriteLine();
        var rows = LiliRows().ToList();
        var tags = new[] { "L", "Y0", "Y1", "Y2", "Y3" };
        Agg Get(string name, Formation f, string tag, int st)
            => tag == "L" ? Measure(name + "|L0", ReplaceKado(f, Kado0), st) : YRows(tag).First(r => r.Band == "差し替え" && r.Name == name).At(st);

        Console.WriteLine("| 行 | 波 | " + string.Join(" | ", tags) + " |");
        Console.WriteLine("|---|--:|" + string.Concat(tags.Select(_ => "---|")));
        var beatW = tags.ToDictionary(t => t, _ => 0); var beatC = tags.ToDictionary(t => t, _ => 0);
        var winSum = new Dictionary<(string, int), double>(); var cleanSum = new Dictionary<(string, int), double>();
        foreach (var (name, f) in rows)
            foreach (int st in Waves)
            {
                var a = tags.ToDictionary(t => t, t => Get(name, f, t, st));
                foreach (var t in tags)
                {
                    winSum[(t, st)] = winSum.GetValueOrDefault((t, st)) + a[t].Win;
                    cleanSum[(t, st)] = cleanSum.GetValueOrDefault((t, st)) + a[t].CleanAll;
                    if (t == "L") continue;
                    if (a[t].Win > a["L"].Win) beatW[t]++;
                    if (a[t].CleanAll > a["L"].CleanAll) beatC[t]++;
                }
                Console.WriteLine("| " + name + " | " + (st + 1) + " | " + string.Join(" | ", tags.Select(t => F1(a[t].Win) + " / " + F1(a[t].CleanAll))) + " |");
            }
        Console.WriteLine();
        int n = rows.Count;
        Console.WriteLine("## 波ごとの平均（" + n + " 行）");
        Console.WriteLine();
        Console.WriteLine("| 波 | " + string.Join(" | ", tags.Select(t => t + " 勝率 / 全員生存")) + " |");
        Console.WriteLine("|--:|" + string.Concat(tags.Select(_ => "---|")));
        foreach (int st in Waves)
            Console.WriteLine("| " + (st + 1) + " | " + string.Join(" | ", tags.Select(t => F1(winSum[(t, st)] / n) + " / " + F1(cleanSum[(t, st)] / n))) + " |");
        Console.WriteLine();
        Console.WriteLine("**ツギがリリを上回るセル（" + n * 4 + " セル中）**: 勝率 " + string.Join(" ／ ", tags.Skip(1).Select(t => t + " " + beatW[t]))
                          + "・全員生存 " + string.Join(" ／ ", tags.Skip(1).Select(t => t + " " + beatC[t])) + "。");
        Console.WriteLine();

        Console.WriteLine("## 倒れた味方・決着ターン・膠着（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 倒れた " + string.Join(" / ", tags) + " | 決着T " + string.Join(" / ", tags) + " | 膠着 " + string.Join(" / ", tags) + " |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (name, f) in rows)
        {
            var ag = tags.ToDictionary(t => t, t => Waves.Select(st => Get(name, f, t, st)).ToArray());
            Console.WriteLine("| " + name + " | " + string.Join(" / ", tags.Select(t => ag[t].Average(x => x.Fallen).ToString("F2"))) + " | "
                              + string.Join(" / ", tags.Select(t => ag[t].Average(x => x.Turns).ToString("F1"))) + " | "
                              + string.Join(" / ", tags.Select(t => ag[t].Sum(x => x.Stall))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## カドの行の第四波（第210期 75.5 ／ 7.0）と棘");
        Console.WriteLine();
        Console.WriteLine("**棘/戦** ＝ カドが棘を返した回数（どちらの口も）／ **うち受け切り** ＝ 破片で受け切った一撃に返した棘 ／ **鳴らず** ＝ 破片で受け切って棘が鳴らなかった一撃 ／ "
                          + "**カド与** ＝ カドの敵への与ダメ/戦 ／ **膠着** は 200 戦中。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 版 | 勝率 | 全員生存 | 膠着 | 棘/戦 | うち受け切り | 鳴らず | カド与 |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "kado")))
            foreach (int st in Waves)
                foreach (var t in tags)
                {
                    var a = Get(name, f, t, st);
                    var k = a.Of("kado");
                    Console.WriteLine("| " + name + " | " + (st + 1) + " | " + t + " | " + F1(a.Win) + " | " + F1(a.CleanAll) + " | " + a.Stall + " | "
                                      + a.Per(k.ThornRipostes).ToString("F2") + " | " + a.Per(k.ThornArmorRiposte).ToString("F2") + " | "
                                      + a.Per(k.ThornArmorMuted - k.ThornArmorRiposte).ToString("F2") + " | " + a.Per(k.DamageToEnemy).ToString("F1") + " |");
                }
        Console.WriteLine();
    }

    static Formation ReplaceKado(Formation f, UnitDef kado)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef u) in f.Occupied()) g[slot] = u.Id == "kado" ? kado : u;
        return g;
    }

    // =================================================================================
    // ledger5 —— §4.3・§4.4
    // =================================================================================

    static void Ledger211()
    {
        Console.WriteLine("# 第211期 `tsugi ledger5` —— 板の行き先・前衛の倒れ・応急処置・棘の帳簿（1戦あたり）");
        Console.WriteLine();

        Console.WriteLine("## 手番の板と応急処置の行き先（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("列は貼った瞬間の相手の席の列。**自分** ＝ ツギ自身（列にも含まれる）。**候補と一致** ＝ 貼った相手が「(HP＋破片)÷最大HP が最小」の候補に入っていた割合（Y1 以降は 100% になるはず）。"
                          + "**受け切り後** ＝ 破片で受け切った一撃の後に応急処置の条件を満たした回数/戦（Y2 の窓口）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 帯 | 板/戦 | 前 / 中 / 後 | 自分 | 候補と一致 | 偏り | 応急/戦 | 前 / 中 / 後 | 自分 | 受け切り後 | 応急の量 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|---|--:|--:|--:|");
        foreach (var tag in YTags)
            foreach (var band in new[] { "compare", "差し替え" })
            {
                var ags = Waves.SelectMany(w => YRows(tag).Where(r => r.Band == band).Select(r => r.At(w))).ToList();
                SumAll(ags, out int n, out var ts);
                long[] pr = ts.PlankToRow ?? new long[3], fr = ts.FirstAidToRow ?? new long[3];
                int hb = ags.Sum(a => a.HypBattles);
                Console.WriteLine("| " + tag + " | " + band + " | " + ((double)ts.PlankPastes / Math.Max(1, n)).ToString("F2") + " | "
                                  + string.Join(" / ", pr.Select(x => Pct(x, ts.PlankPastes))) + " | " + Pct(ts.PlankSelf, ts.PlankPastes) + " | "
                                  + Pct(ts.PlankHypSame, ts.PlankHypPicks) + " | " + (hb == 0 ? "—" : (100 * ags.Sum(a => a.HypTopShareSum) / hb).ToString("F1") + "%") + " | "
                                  + ((double)ts.FirstAidFired / Math.Max(1, n)).ToString("F2") + " | "
                                  + string.Join(" / ", fr.Select(x => Pct(x, ts.FirstAidFired))) + " | " + Pct(ts.FirstAidSelf, ts.FirstAidFired) + " | "
                                  + ((double)ts.FirstAidArmoredHits / Math.Max(1, n)).ToString("F2") + " | "
                                  + (ts.FirstAidFired == 0 ? "—" : ((double)ts.FirstAidPaste / ts.FirstAidFired).ToString("F1")) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 前衛が倒れた戦・倒れた一撃の直前の破片・応急処置を受けた駒の生存");
        Console.WriteLine();
        Console.WriteLine("**前衛倒れ** ＝ 前列で始めた駒が1体以上倒れた戦の割合（第2〜5波の平均）／ **直前の破片** ＝ 倒れた一撃を受ける直前に持っていた破片の平均 ／ "
                          + "**受けた駒の生存** ＝ 応急処置を受けた（戦 × 駒）がその戦を生き延びた割合 ／ **受けない駒の生存** ＝ 条件を満たしたが受けなかった駒（第210期と同じ作り）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 帯 | 前衛倒れ | 倒れた/戦 | 直前の破片 | 受けた駒の生存 | 受けない駒の生存 | 出番/戦 | 貼った/戦 | 使い切り/戦 | 粛/戦 | 痺れ等/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|---|---|--:|--:|--:|--:|--:|");
        foreach (var tag in YTags)
            foreach (var band in new[] { "compare", "差し替え" })
            {
                var ags = Waves.SelectMany(w => YRows(tag).Where(r => r.Band == band).Select(r => r.At(w))).ToList();
                int n = ags.Sum(a => a.N);
                long dc = ags.Sum(a => a.T.Values.Sum(t => t.DiedCount)), da = ags.Sum(a => a.T.Values.Sum(t => t.DiedArmorBefore));
                int au = ags.Sum(a => a.AidUnits), asv = ags.Sum(a => a.AidSurvived), nu = ags.Sum(a => a.NeedNoAidUnits), ns = ags.Sum(a => a.NeedNoAidSurvived);
                SumAll(ags, out _, out var ts);
                string P(long x) => ((double)x / Math.Max(1, n)).ToString("F2");
                Console.WriteLine("| " + tag + " | " + band + " | " + F1(100.0 * ags.Sum(a => a.FrontFell) / Math.Max(1, n)) + "% | "
                                  + (ags.Sum(a => a.FallenSum) / Math.Max(1, n)).ToString("F2") + " | " + (dc == 0 ? "—" : ((double)da / dc).ToString("F1")) + " | "
                                  + Pct(asv, au) + "（" + au + "） | " + Pct(ns, nu) + "（" + nu + "） | "
                                  + P(ts.FirstAidChance + ts.FirstAidArmoredHits) + " | " + P(ts.FirstAidFired) + " | " + P(ts.FirstAidSpent) + " | " + P(ts.FirstAidHushed) + " | " + P(ts.FirstAidHeld) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("### 行ごとの前衛倒れ（第2 / 3 / 4 / 5 波）");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | " + string.Join(" | ", YTags) + " |");
        Console.WriteLine("|---|---|" + string.Concat(YTags.Select(_ => "---|")));
        foreach (var r0 in YRows("Y0"))
            Console.WriteLine("| " + r0.Band + " | " + r0.Name + " | " + string.Join(" | ", YTags.Select(t =>
            {
                var r = YRows(t).First(x => x.Band == r0.Band && x.Name == r0.Name);
                return string.Join(" / ", Waves.Select(w => F1(100.0 * r.At(w).FrontFell / Math.Max(1, r.At(w).N))));
            })) + " |");
        Console.WriteLine();

        Console.WriteLine("### 板を受けた駒（行ごと・第2〜5波・回/戦）");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | Y0 | Y1 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var r0 in YRows("Y0"))
        {
            string Recv(string tag)
            {
                var r = YRows(tag).First(x => x.Band == r0.Band && x.Name == r0.Name);
                var ags = Waves.Select(r.At).ToList();
                int n = ags.Sum(a => a.N);
                var by = new Dictionary<string, long>();
                foreach (var a in ags) foreach (var (id, t) in a.T) by[id] = by.GetValueOrDefault(id) + t.PlankReceived;
                return string.Join(" ／ ", by.Where(p => p.Value > 0).OrderByDescending(p => p.Value)
                    .Select(p => (UnitCatalog.Everyone.FirstOrDefault(u => u.Id == p.Key)?.Name ?? p.Key) + " " + ((double)p.Value / n).ToString("F2")));
            }
            Console.WriteLine("| " + r0.Band + " | " + r0.Name + " | " + Recv("Y0") + " | " + Recv("Y1") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## §4.4 `継ぎ当て×分散回復` の第五波（ポンが遊んだ行）");
        Console.WriteLine();
        Console.WriteLine("**ササ倒れ** ＝ ササが倒れた戦の割合（200 戦）。X 字は `compare` の席のまま、**パターン2** は編成画面の切り替えと同じ詰め替え（前1 → 前衛・前3 → 中衛・上・中央 → 中衛・中央・後1 → 中衛・下・後3 → 後衛）。"
                          + "**seed 6** はポンが遊んだ seed の結果（勝敗・ターン・ササの残りHP）。");
        Console.WriteLine();
        Console.WriteLine("| 陣形 | 版 | 勝率 | 全員生存 | ササ倒れ | ササが受けた板/戦 | ササへの応急/戦 | ツギ自身への板/戦 | ツギ自身への応急/戦 | ツギの破片（戦の終わり・平均） | 倒れた駒（戦の割合） | seed 6 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|---|");
        const string pon = "継ぎ当て×分散回復";
        var (_, pf) = TsugiRows().First(r => r.Name == pon);
        foreach (var (shape, fx) in new[] { ("X 字", pf), ("パターン2", ToDiamond(pf)) })
            foreach (var tag in YTags)
            {
                Formation g = ApplyY(fx, "tsugi", tag);
                var a = Measure(pon + "|" + shape + "|" + tag, g, 4);
                var s = a.Of("sasa");
                double tsugiArmor = TsugiEndArmor(g);
                var r6 = BattleEngine.Run(g, EnemyCatalog.Stages[4].Enemy, 6, verbose: false);
                var ts = a.Of("tsugi");
                string fallen = string.Join("・", a.FallenBy.Where(p => p.Value > 0).OrderByDescending(p => p.Value)
                    .Select(p => (UnitCatalog.Everyone.FirstOrDefault(u => u.Id == p.Key)?.Name ?? p.Key) + " " + F1(100.0 * p.Value / Math.Max(1, a.N)) + "%"));
                Console.WriteLine("| " + shape + " | " + tag + " | " + F1(a.Win) + " | " + F1(a.CleanAll) + " | " + F1(100.0 * a.FallenBy.GetValueOrDefault("sasa") / Math.Max(1, a.N)) + "% | "
                                  + a.Per(s.PlankReceived).ToString("F2") + " | " + a.Per(s.FirstAidReceived).ToString("F2") + " | "
                                  + a.Per(ts.PlankSelf).ToString("F2") + " | " + a.Per(ts.FirstAidSelf).ToString("F2") + " | " + tsugiArmor.ToString("F1") + " | "
                                  + (fallen == "" ? "—" : fallen) + " | "
                                  + (r6.PlayerWon ? "勝" : "負") + " " + r6.Turns + "T・ササ" + (r6.PlayerStarterFallen.Contains("sasa") ? "倒れ" : "生存") + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 棘（Y3）");
        Console.WriteLine();
        Console.WriteLine("カドを含む行（`compare` のツギ在席 1 行 ＋ 差し替え 3 行）の第2〜5波。**鳴らず** ＝ 受け切ったが棘が出なかった（粛・痺れ・反撃の中）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 版 | 棘/戦 | うち受け切り | 受け切り/戦 | 鳴らず | カド与/戦 | カド被/戦 | カド倒れ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r0 in YRows("Y0"))
            foreach (var tag in new[] { "Y2", "Y3" })
            {
                var r = YRows(tag).First(x => x.Band == r0.Band && x.Name == r0.Name);
                var ags = Waves.Select(r.At).ToList();
                if (!ags.Any(a => a.T.ContainsKey("kado"))) continue;
                int n = ags.Sum(a => a.N);
                var k = new UnitTally(); foreach (var a in ags) k.Add(a.Of("kado"));
                string P(double x) => (x / Math.Max(1, n)).ToString("F2");
                Console.WriteLine("| " + r0.Band + " | " + r0.Name + " | " + tag + " | " + P(k.ThornRipostes) + " | " + P(k.ThornArmorRiposte) + " | " + P(k.ThornArmorMuted) + " | "
                                  + P(k.ThornArmorMuted - k.ThornArmorRiposte) + " | " + P(k.DamageToEnemy) + " | " + P(k.DamageTaken) + " | "
                                  + F1(100.0 * ags.Sum(a => a.FallenBy.GetValueOrDefault("kado")) / Math.Max(1, n)) + "% |");
            }
        Console.WriteLine();
    }

    /// <summary>編成画面の X 字 → パターン2 の詰め替え（`Main.Shape.cs` の `XToDiamond` と同じ）。</summary>
    static Formation ToDiamond(Formation x)
    {
        UnitDef?[] a = new UnitDef?[5];
        foreach ((int slot, UnitDef u) in x.Occupied()) if (slot < 5) a[slot] = u;
        return Formation.BuildDiamond(a[1], a[4], a[2], a[0], a[3]);
    }

    /// <summary>ツギが戦の終わりに持っていた破片の平均（seed 0..199・第五波）。</summary>
    static double TsugiEndArmor(Formation g)
    {
        long sum = 0;
        object lk = new();
        Parallel.For(0, Seeds, seed =>
        {
            var players = BattleEngine.Materialize(g, BattleContext.PlayerTeam);
            var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[4].Enemy, BattleContext.EnemyTeam);
            BattleEngine.Run(players, enemies, seed, verbose: false);
            int a = players.First(p => p.Def.Id == "tsugi").RawCounter(StatusKeys.Armor);
            lock (lk) sum += a;
        });
        return (double)sum / Seeds;
    }

    // =================================================================================
    // y3diff —— §4.5 Y3 の全61行の差分
    // =================================================================================

    static void Y3Diff()
    {
        Console.WriteLine("# 第211期 `tsugi y3diff` —— `compare` 61 行 × 5 波で Y2 と Y3 を並べる（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("Y2 ＝ ツギは Y2・カドは第210期 ／ Y3 ＝ ツギは Y2・カドは破片で受けても棘が鳴る。**違いはカドの札1枚だけ。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | ツギ | カド | 波 | Y2 | Y3 | 差 |");
        Console.WriteLine("|---|:-:|:-:|--:|--:|--:|--:|");
        int moved = 0, movedNoTsugi = 0, cells = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            Formation a = ApplyY(f, "tsugi", "Y2"), b = ApplyY(f, "tsugi", "Y3");
            for (int st = 0; st < 5; st++)
            {
                cells++;
                double wa = WinRate(a, st), wb = WinRate(b, st);
                if (wa == wb) continue;
                moved++;
                if (!HasTsugi(f)) movedNoTsugi++;
                Console.WriteLine("| " + name + " | " + (HasTsugi(f) ? "○" : "") + " | " + (f.Occupied().Any(o => o.Def.Id == "kado") ? "○" : "") + " | "
                                  + (st + 1) + " | " + F1(wa) + " | " + F1(wb) + " | " + D(wb - wa) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("**動いたセル " + moved + " / " + cells + "（うちツギのいない行 " + movedNoTsugi + "）。**");
        Console.WriteLine();
    }

    static double WinRate(Formation f, int st)
    {
        int wins = 0;
        Formation enemy = EnemyCatalog.Stages[st].Enemy;
        Parallel.For(0, Seeds, seed => { if (BattleEngine.Run(f, enemy, seed, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
        return 100.0 * wins / Seeds;
    }

    // =================================================================================
    // check5 —— §6
    // =================================================================================

    static void Check211(string arg)
    {
        Console.WriteLine("# 第211期 `tsugi check5` —— 自己検査");
        Console.WriteLine();
        bool all = true;
        void Req(bool ok, string what) { all &= ok; Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + what); }

        // (1) Y0 は第210期の表と一致（61 行すべて）・Y2 でもツギを含まない行は一致
        string path = string.IsNullOrWhiteSpace(arg) ? ".tmp/balance210.md" : arg.Trim();
        if (File.Exists(path))
        {
            var old = File.ReadAllLines(path).Where(l => l.StartsWith("| ")).Select(l => l.Split('|').Select(c => c.Trim()).ToArray())
                          .Where(c => c.Length >= 8).GroupBy(c => c[1]).ToDictionary(g => g.Key, g => g.First().Skip(2).Take(5).ToArray());
            foreach (var tag in new[] { "Y0", "Y1", "Y2" })
            {
                int bad = 0, cells = 0, badNo = 0;
                foreach (var (name, f) in CompareBuilds())
                {
                    if (tag != "Y0" && HasTsugi(f)) continue;
                    if (!old.TryGetValue(name, out var o)) { bad++; continue; }
                    Formation g = ApplyY(f, "tsugi", tag);
                    for (int st = 0; st < 5; st++)
                    {
                        cells++;
                        if (WinRate(g, st).ToString("F1") + "%" != o[st]) { bad++; if (!HasTsugi(f)) badNo++; }
                    }
                }
                Req(bad == 0, "(1) " + tag + " が第210期の `docs/balance.md` と一致（" + (tag == "Y0" ? "61 行すべて" : "ツギを含まない行") + "）: " + cells + " セル中ずれ " + bad);
            }
        }
        else Console.WriteLine("- (1) 第210期の表が無い（" + path + "）");

        // (2) verbose の有無で勝敗・決着T が同じ（Y3）
        int mism = 0;
        var tablesY3 = TsugiRows().Select(r => ApplyY(r.F, "tsugi", "Y3")).Concat(LiliRows().Select(r => ApplyY(r.F, "lili", "Y3"))).ToList();
        foreach (var f in tablesY3)
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 30; seed++)
                {
                    var a = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    var b = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) mism++;
                }
        Req(mism == 0, "(2) verbose の有無で勝敗・決着T が同じ（Y3・台本と計数が盤面を動かさない）: ずれ " + mism);
        Console.WriteLine("  - 新しい処理で乱数を引くのは手番の板の同値割り（`ctx.PickOne`・候補 1 体なら引かない）だけ。候補の計算（`PlankTrait.Neediest`）・応急処置の条件・受け切りの棘は `Roll` も `PickOne` も呼ばない。");

        // (3) 貼る相手が (HP＋破片)÷最大HP の最小（計数: 貼った相手が候補に入っていた割合が Y1 以降 100%）
        foreach (var tag in YTags)
        {
            SumAll(YRows(tag).SelectMany(r => Waves.Select(w => r.At(w))), out _, out var ts);
            bool y1 = tag != "Y0";
            Req(!y1 || (ts.PlankHypPicks > 0 && ts.PlankHypSame == ts.PlankHypPicks),
                "(3) " + tag + ": 手番の板の相手が「(HP＋破片)÷最大HP が最小」の候補に入っていた " + ts.PlankHypSame + " / " + ts.PlankHypPicks + (y1 ? "（100% であること）" : "（Y0 は対照）"));
        }

        // (4)〜(6) 台本（Y1〜Y3）: 応急処置の条件・1ターン1回・粛・破片を持つ駒・受け切りの棘
        foreach (var tag in YTags)
        {
            var v = YVersions.First(x => x.Tag == tag);
            bool armored = v.Tsugi.Traits.Contains(TraitId.FirstAidArmored);
            long aids = 0, aidArmoredBefore = 0, aidBadCond = 0, aidDead = 0, aidTwice = 0, aidHushed = 0, riposteArmor = 0, riposteHushed = 0, battles = 0;
            var tables = TsugiRows().Select(r => ApplyY(r.F, "tsugi", tag)).Concat(LiliRows().Select(r => ApplyY(r.F, "lili", tag))).ToList();
            foreach (Formation f in tables)
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 30; seed++)
                    {
                        battles++;
                        var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                        var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
                        var maxHp = players.ToDictionary(p => p, p => p.MaxHp);
                        var r = BattleEngine.Run(players, enemies, seed, verbose: true);
                        var byId = players.ToDictionary(p => p.InstanceId, p => maxHp[p]);
                        var hushIds = enemies.Where(e => e.HasTrait(TraitId.Hush)).Select(e => e.InstanceId).ToHashSet();
                        var aidTurns = new HashSet<int>(); var deadHush = new HashSet<int>();
                        foreach (var e in r.Events)
                        {
                            if (e.Kind == BattleEventKind.Death && e.TargetId is int d && hushIds.Contains(d)) deadHush.Add(d);
                            if (e.Kind != BattleEventKind.Plank || e.Text != PlankLabels.FirstAid) continue;
                            aids++;
                            int armorBefore = (e.StatusRemaining ?? 0) - e.Amount;
                            if (armorBefore > 0) aidArmoredBefore++;
                            // 湧いた駒（胞子・餌）は開戦時の最大HPを持たないので上限なしとして読む（long で比べる・int だと ×40 であふれる）
                            long mhp = e.TargetId is int tid && byId.TryGetValue(tid, out int m) ? m : int.MaxValue;
                            bool cond = armored ? (e.HpAfter + armorBefore) * 100L < mhp * FirstAidTrait.Percent : armorBefore == 0 && e.HpAfter * 100L < mhp * FirstAidTrait.Percent;
                            if (!cond) aidBadCond++;
                            if (e.HpAfter <= 0) aidDead++;
                            if (!aidTurns.Add(e.Turn)) aidTwice++;
                            if (hushIds.Count > 0 && deadHush.Count < hushIds.Count) aidHushed++;
                        }
                        var k = r.TallyByUnit.GetValueOrDefault("kado");
                        if (k is not null) riposteArmor += k.ThornArmorRiposte;
                    }
            Req(aids > 0 && aidBadCond == 0 && aidDead == 0 && aidTwice == 0 && aidHushed == 0 && (armored ? aidArmoredBefore > 0 : aidArmoredBefore == 0),
                "(4) " + tag + ": 応急処置は " + (armored ? "(HP＋破片) < 最大HP × 40%（破片を持つ駒にも出る）" : "破片 0 ＆ HP < 40%") + "・倒れた味方に貼らない・1ターン1回・粛の間は出ない（"
                + aids + " 件中 条件のずれ " + aidBadCond + " ／ 破片を持つ駒 " + aidArmoredBefore + " ／ 倒れた " + aidDead + " ／ 同じターン2回目 " + aidTwice + " ／ 粛の間 " + aidHushed + "・" + battles + " 戦）");
            bool thorn3 = v.Kado.Traits.Contains(TraitId.ThornsArmored);
            Req(thorn3 ? riposteArmor > 0 : riposteArmor == 0,
                "(5) " + tag + ": 破片で受け切った一撃に返した棘 " + riposteArmor + " 回（" + (thorn3 ? "Y3 は 1 回以上" : "Y3 以外は 0") + "）");
        }

        // (6) 受け切りの棘の門: 粛・痺れ・反撃の中では返さない（計数: 受け切り ≥ 返した・第二波は粛の保持者が生きている間）
        {
            long muted = 0, rip = 0, w2rip = 0, w2mut = 0;
            foreach (var r in YRows("Y3"))
                foreach (int w in Waves)
                {
                    var k = r.At(w).Of("kado");
                    muted += k.ThornArmorMuted; rip += k.ThornArmorRiposte;
                    if (w == 1) { w2rip += k.ThornArmorRiposte; w2mut += k.ThornArmorMuted; }
                }
            Req(rip <= muted && rip > 0, "(6) Y3: 受け切り " + muted + " 回のうち棘を返した " + rip + " 回（残りは粛・痺れ・反撃の中）・第二波（粛）は受け切り " + w2mut + " 回に返した " + w2rip + " 回");
        }

        // (7) カド以外の被弾で動く駒は変わらない: カドのいない行は Y2 と Y3 で同じ台本
        {
            int diff = 0, n = 0;
            foreach (var (name, f) in CompareBuilds().Concat(CrossBuilds()))
            {
                if (f.Occupied().Any(o => o.Def.Id == "kado")) continue;
                Formation a = ApplyY(f, "tsugi", "Y2"), b = ApplyY(f, "tsugi", "Y3");
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 20; seed++)
                    {
                        n++;
                        var ra = BattleEngine.Run(a, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                        var rb = BattleEngine.Run(b, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                        if (ra.Events.Count != rb.Events.Count || ra.PlayerWon != rb.PlayerWon || ra.Turns != rb.Turns) diff++;
                    }
            }
            Req(diff == 0, "(7) カドのいない行（`compare` ＋ 交差帯）は Y2 と Y3 で台本の長さ・勝敗・決着T が同じ（" + n + " 戦中ずれ " + diff + "）——受け切りの口はカドの札でしか開かない");
        }

        Console.WriteLine();
        Console.WriteLine(all ? "**全項目 ○。**" : "**× がある。**");
    }

    /// <summary>第211期（事後）: 第208期の自己検査 (c4) が Y3 のカドで 1 件だけ落ちた理由を1件ずつ出す。</summary>
    static void YokeProbe()
    {
        Console.WriteLine("# 第211期 `tsugi yokeprobe` —— 第208期 (c4) の 25 超の反射を1件ずつ");
        Console.WriteLine();
        var tables = TsugiRows().Select(r => (r.Name, F: ApplyU(r.F, "tsugi", U3, UnitCatalog.Doha)))
            .Concat(LiliRows().Select(r => (r.Name, F: ApplyU(r.F, "lili", U3, UnitCatalog.Doha)))).ToList();
        foreach (var (name, f) in tables)
            for (int seed = 0; seed < 40; seed++)
            {
                var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[3].Enemy, BattleContext.EnemyTeam);
                var r = BattleEngine.Run(players, enemies, seed, verbose: true);
                var yokeIds = enemies.Where(e => e.HasTrait(TraitId.Yoke)).Select(e => e.InstanceId).ToHashSet();
                var dead = new HashSet<int>();
                string Nm(int? id) => players.Concat(enemies).FirstOrDefault(u => u.InstanceId == id)?.Name ?? "?";
                for (int i = 0; i < r.Events.Count; i++)
                {
                    var e = r.Events[i];
                    if (e.Kind == BattleEventKind.Death && e.TargetId is int d) dead.Add(d);
                    if (e.Kind != BattleEventKind.Plank || e.Text != PlankLabels.Reflect) continue;
                    int j = -1; for (int k = i + 1; k < r.Events.Count; k++) if (r.Events[k].Kind == BattleEventKind.Damage && r.Events[k].TargetId == e.TargetId) { j = k; break; }
                    if (j < 0 || r.Events[j].Amount <= 25) continue;
                    var dd = r.Events[j];
                    Console.WriteLine("- " + name + " seed " + seed + " T" + e.Turn + ": 反射 " + Nm(e.ActorId) + " → " + Nm(e.TargetId) + " 名目 " + e.Amount
                                      + " ／ 次の Damage は " + (j - i) + " 件後・" + Nm(dd.ActorId) + " → " + Nm(dd.TargetId) + " " + dd.Amount
                                      + " ／ 軛の保持者 生存 " + yokeIds.Count(y => !dead.Contains(y)) + " / " + yokeIds.Count
                                      + " ／ 間の出来事: " + string.Join("・", r.Events.Skip(i + 1).Take(j - i).Select(x => x.Kind + (x.Text is null ? "" : "(" + x.Text + ")") + (x.Amount != 0 ? " " + x.Amount : ""))));
                }
            }
    }
}
