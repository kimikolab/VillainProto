using BattleCore;
using static Common;

// =====================================================================================
// sora186 モード（第186期）の測定側 —— run / ledger / bench / check
//
//     dotnet run --project BattleSim -c Release 0 sora186 run     # 対照（旧ソラ 対 新ソラ）・(G2)・勝ち方・全体の指標
//     dotnet run --project BattleSim -c Release 0 sora186 ledger  # 帳簿（逸らした回数と量・§1・倒した数・ソラの生存と被害）
//     dotnet run --project BattleSim -c Release 0 sora186 bench   # 診断台（ヒサ×ソラ×ザン・`Presets` には足さない）
//     dotnet run --project BattleSim -c Release 0 sora186 check [採用前のbalance.md]  # 自己検査
//
// 旧版の対照は「ソラだけ第185期の姿（`Divert` だけ）に戻したローカルの `UnitDef`」で作る（第180期と同じ作法）。
// =====================================================================================

static partial class Sora186Diag
{
    // =================================================================================
    // 版
    // =================================================================================

    /// <summary>第185期のソラ（逸らし `Divert` だけ）。</summary>
    static readonly UnitDef SoraOld = new()
    {
        Id = UnitCatalog.Sora.Id, Name = UnitCatalog.Sora.Name, MaxHp = UnitCatalog.Sora.MaxHp,
        Attack = UnitCatalog.Sora.Attack, Speed = UnitCatalog.Sora.Speed, Pattern = AttackPattern.Single,   // 第185期は単体
        Advances = UnitCatalog.Sora.Advances, Actions = UnitCatalog.Sora.Actions,
        Traits = new[] { TraitId.Divert },
        PlusText = "毎ターン味方に向いた視線を引き剥がし、いちばん硬い敵へ向け直す",
        MinusText = "引き剥がした視線は自分に刺さる。毎ターン狙われ続ける",
        Flavor = UnitCatalog.Sora.Flavor
    };

    /// <summary>第186期 追補の版を作る（ソラの札と攻撃型だけを差し替える）。</summary>
    static UnitDef SoraAs(TraitId[] traits, AttackPattern pattern) => new()
    {
        Id = UnitCatalog.Sora.Id, Name = UnitCatalog.Sora.Name, MaxHp = UnitCatalog.Sora.MaxHp,
        Attack = UnitCatalog.Sora.Attack, Speed = UnitCatalog.Sora.Speed, Pattern = pattern,
        Advances = UnitCatalog.Sora.Advances, Actions = UnitCatalog.Sora.Actions,
        Traits = traits, PlusText = UnitCatalog.Sora.PlusText, MinusText = UnitCatalog.Sora.MinusText, Flavor = UnitCatalog.Sora.Flavor
    };

    sealed record Version(string Name, UnitDef Def);
    static readonly Version V0 = new("旧（第185期）", SoraOld);
    /// <summary>逸らしだけ（突きなし・単体）。第186期 本編の姿に、追補の (B) 宛先の差し替えだけが入った版。</summary>
    static readonly Version VD = new("逸らしだけ", SoraAs(new[] { TraitId.Divert, TraitId.Deflect }, AttackPattern.Single));
    static readonly Version New = new("新（逸らし＋突き）", UnitCatalog.Sora);
    /// <summary>突きの対照: 倍率を素の攻撃力で掛ける。</summary>
    static readonly Version VP = new("素の倍率", SoraAs(new[] { TraitId.Divert, TraitId.Deflect, TraitId.ThrustPlain }, AttackPattern.Pierce));

    static Formation Apply(Formation f, Version v)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "sora" ? v.Def : d;
        return g;
    }

    static bool Mine(Formation f) => Has(f, "sora");

    static string Who(Formation f)
        => string.Join("・", new[] { "hisa", "zan", "tome", "kari" }.Where(id => Has(f, id)).Select(id => Short(UnitCatalog.ById(id))));

    static BattleResult Fight(Formation f, int st, int seed, Version v, bool verbose = false)
        => BattleEngine.Run(Apply(f, v), EnemyCatalog.Stages[st].Enemy, seed, verbose: verbose);

    static UnitTally T(BattleResult r, string id) => r.TallyByUnit.TryGetValue(id, out UnitTally? t) ? t : new UnitTally();

    static double LifeOf(BattleResult r, string id)
        => r.TallyByUnit.TryGetValue(id, out UnitTally? t) && t.LastActiveTurn > 0 ? t.LastActiveTurn : r.Turns;

    // =================================================================================
    // run
    // =================================================================================

    static void StageRun()
    {
        Console.WriteLine("# 第186期 `sora186 run` —— 対照（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**旧** ＝ 第185期のソラ（`Divert` だけ）。**新** ＝ この期の既定（`Divert` ＋ `Deflect`）。第2〜5波・seed 0..199。");
        Console.WriteLine();

        var rows = Bands().ToList();
        var res = new Dictionary<(string, string), double[]>();
        Parallel.ForEach(rows.SelectMany(r => new[] { V0, VD, New, VP }.Select(v => (r, v))), x =>
        {
            if (!Mine(x.r.F) && (x.v == VD || x.v == VP)) return;
            double[] w = Rates(x.r.F, x.v);
            lock (res) res[(x.r.Band + "/" + x.r.Name, x.v.Name)] = w;
        });
        double A(string key, Version v) => Mean25(res[(key, v.Name)]);

        Console.WriteLine("## 表A. ソラを含む行（第2〜5波の平均と波別）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 席 | 同席 | 旧 | **新** | 新−旧 | 旧 第2〜5波 | 新 第2〜5波 | ソラの生存T 旧 → 新 | ソラの死亡率 旧 → 新 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|---|---|---|");
        foreach (var r in rows)
        {
            if (!Mine(r.F)) continue;
            string key = r.Band + "/" + r.Name;
            (double lo, double ln) = (LifeAvg(r.F, V0), LifeAvg(r.F, New));
            (double dO, double dN) = (DeathRate(r.F, V0), DeathRate(r.F, New));
            Console.WriteLine("| " + r.Name + " | " + SeatOf(r.F, "sora") + " | " + Who(r.F) + " | " + A(key, V0).ToString("F1") + " | **"
                              + A(key, New).ToString("F1") + "** | **" + D(A(key, New) - A(key, V0)) + "** | "
                              + Cells(res[(key, V0.Name)]) + " | " + Cells(res[(key, New.Name)]) + " | "
                              + lo.ToString("F2") + " → **" + ln.ToString("F2") + "** | "
                              + dO.ToString("F1") + "% → **" + dN.ToString("F1") + "%** |");
        }
        Console.WriteLine();
        Console.WriteLine("`ソラの生存T` ＝ `LastActiveTurn`（倒れなければ決着T）の第2〜5波平均。");
        Console.WriteLine();

        Console.WriteLine("## 表A'. 追補の版（第2〜5波の平均）");
        Console.WriteLine();
        Console.WriteLine("**逸らしだけ** ＝ 突きなし・単体（(B) の宛先の差し替えは入っている）。**素の倍率** ＝ 突きの倍率を素の攻撃力で掛けた対照。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 旧 | 逸らしだけ | **新（突き）** | 素の倍率 | 突きの寄与（新−逸らしだけ） | 強化が倍率に乗る寄与（新−素の倍率） | 新 第2〜5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
        foreach (var r in rows)
        {
            if (!Mine(r.F)) continue;
            string key = r.Band + "/" + r.Name;
            Console.WriteLine("| " + r.Name + " | " + A(key, V0).ToString("F1") + " | " + A(key, VD).ToString("F1") + " | **" + A(key, New).ToString("F1") + "** | "
                              + A(key, VP).ToString("F1") + " | " + D(A(key, New) - A(key, VD)) + " | " + D(A(key, New) - A(key, VP)) + " | " + Cells(res[(key, New.Name)]) + " |");
        }
        Console.WriteLine();

        int still = 0, stillRows = 0;
        foreach (var r in rows)
        {
            if (Mine(r.F)) continue;
            stillRows++;
            string key = r.Band + "/" + r.Name;
            double[] a = res[(key, V0.Name)], b = res[(key, New.Name)];
            if (Enumerable.Range(0, 5).Any(i => Math.Abs(a[i] - b[i]) > 0.001)) still++;
        }
        Console.WriteLine("- ソラを含まない " + stillRows + " 行（compare ＋ 交差帯）で旧と新が違う行: **" + still + " 行**（0 が正。"
                          + "前期版との 0 件差分は `check` の (a)(b) が `docs/balance.md` に対して取る）");
        Console.WriteLine();

        // ---- (G2) ----
        Console.WriteLine("## 表B. (G2) の「壊れ／制約」の分解（`compare` 61 行の分母・**報告のみ**）");
        Console.WriteLine();
        var builds = CompareBuilds().ToList();
        double[] O(string n) => res[("compare/" + n, V0.Name)];
        double[] N(string n) => res[("compare/" + n, New.Name)];
        var dropped = new List<(string Name, int Wave, double Delta)>();
        foreach ((string name, Formation _) in builds)
            for (int w = 1; w < 5; w++)
                if (N(name)[w] - O(name)[w] <= -10.0) dropped.Add((name, w, N(name)[w] - O(name)[w]));
        if (dropped.Count == 0) Console.WriteLine("**−10.0pt 以上落ちたセルは 0 件**（分解する対象が無い）。");
        else
        {
            Console.WriteLine("| 行 | 波 | Δ |");
            Console.WriteLine("|---|--:|--:|");
            foreach (var d in dropped) Console.WriteLine("| " + d.Name + " | 第" + (d.Wave + 1) + "波 | " + D(d.Delta) + " |");
            Console.WriteLine();
            Console.WriteLine("| 駒 | 他の行の数 | 平均変化 | 判定 |");
            Console.WriteLine("|---|--:|--:|---|");
            var units = new SortedSet<string>();
            foreach (var d in dropped.Select(x => x.Name).Distinct())
                foreach ((int _, UnitDef u) in builds.First(b => b.Name == d).F.Occupied()) units.Add(u.Id);
            foreach (string id in units)
            {
                var others = builds.Where(b => Has(b.F, id) && !dropped.Any(x => x.Name == b.Name)).ToList();
                if (others.Count == 0) { Console.WriteLine("| " + UnitCatalog.ById(id).Name + " | 0 | — | 分解が成立しない |"); continue; }
                double avg = others.Average(b => Mean25(N(b.Name)) - Mean25(O(b.Name)));
                string verdict = avg <= -3.0 ? "**壊れ**" : Math.Abs(avg) < 3.0 ? "制約" : "（上振れ）";
                Console.WriteLine("| " + UnitCatalog.ById(id).Name + " | " + others.Count + " | " + D(avg) + " | " + verdict + " |");
            }
        }
        Console.WriteLine();

        // ---- 勝ち方 ----
        Console.WriteLine("## 表C. 勝ち方（第2〜5波・勝った試行だけ・`docs/chain.md` と同じ定義）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 残存 旧 | 残存 新 | 全滅勝ち 旧 | 全滅勝ち 新 | 決着T 旧 | 決着T 新 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in builds)
        {
            if (!Mine(f)) continue;
            var a = Quality(f, V0); var b = Quality(f, New);
            Console.WriteLine("| " + name + " | " + a.Surv.ToString("F2") + " | " + b.Surv.ToString("F2") + " | "
                              + a.Edge.ToString("F1") + "% | " + b.Edge.ToString("F1") + "% | " + a.T.ToString("F2") + " | " + b.T.ToString("F2") + " |");
        }
        Console.WriteLine();

        // ---- 指標 ----
        Console.WriteLine("## 表D. 全体の指標（旧 → 新）");
        Console.WriteLine();
        double[] allO = new double[5], allN = new double[5], primO = new double[5], primN = new double[5];
        foreach ((string name, Formation _) in builds)
            for (int w = 0; w < 5; w++) { allO[w] += O(name)[w] / builds.Count; allN[w] += N(name)[w] / builds.Count; }
        int pc = 0;
        foreach (string p in Baseline.PrimaryRows)
            if (builds.Any(b => b.Name == p)) { pc++; for (int w = 0; w < 5; w++) { primO[w] += O(p)[w]; primN[w] += N(p)[w]; } }
        for (int w = 0; w < 5; w++) { primO[w] /= Math.Max(1, pc); primN[w] /= Math.Max(1, pc); }
        string J(double[] x) => string.Join(" / ", x.Select(v => v.ToString("F1")));
        Console.WriteLine("- 全" + builds.Count + "行: " + J(allO) + " → **" + J(allN) + "**");
        Console.WriteLine("- 主判定" + pc + "行: " + J(primO) + " → **" + J(primN) + "**（歯止め 33.2% との余裕 " + D(primN[4] - 33.2) + "pt）");
        int infoOld = 0, infoNew = 0, hiOld = 0, hiNew = 0;
        foreach ((string name, Formation _) in builds)
        {
            for (int w = 1; w < 5; w++) { if (O(name)[w] > 0 && O(name)[w] < 100) infoOld++; if (N(name)[w] > 0 && N(name)[w] < 100) infoNew++; }
            if (O(name)[4] > 95) hiOld++; if (N(name)[4] > 95) hiNew++;
        }
        Console.WriteLine("- 情報セル（`0 < x < 100`・第2〜5波）: " + infoOld + " → **" + infoNew + "** ／ 第五波 95% 超: " + hiOld + " → **" + hiNew + "**");
        Console.WriteLine();
    }

    static double LifeAvg(Formation f, Version v)
    {
        double s = 0; int n = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++) { s += LifeOf(Fight(f, st, seed, v), "sora"); n++; }
        return s / n;
    }

    static double DeathRate(Formation f, Version v)
    {
        int d = 0, n = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++) { if (T(Fight(f, st, seed, v), "sora").Deaths > 0) d++; n++; }
        return 100.0 * d / n;
    }

    static (double Surv, double Edge, double T) Quality(Formation f, Version v)
    {
        long wins = 0, surv = 0, edge = 0, turns = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = Fight(f, st, seed, v);
                if (!r.PlayerWon) continue;
                wins++; surv += r.PlayerSurvivors; turns += r.Turns;
                if (r.PlayerSurvivors == 1) edge++;
            }
        return wins == 0 ? (0, 0, 0) : ((double)surv / wins, 100.0 * edge / wins, (double)turns / wins);
    }

    // =================================================================================
    // ledger
    // =================================================================================

    static void Ledger()
    {
        Console.WriteLine("# 第186期 `sora186 ledger` —— 帳簿（1戦あたり・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("`逸らした` ＝ 回数 ／ `名目` ＝ 逸らした素の量 ／ `届いた` ＝ 宛先が実際に受けた量（敵の段・§1 込み）／ "
                          + "`§1` ＝ そのうち標の +50% ／ `倒した` ＝ 逸らした分で宛先が倒れた回数 ／ `宛先なし` ＝ 宛先がいなくて全部受けた単体の一撃 ／ "
                          + "`ヒサ重なり` ＝ 逸らした一撃にヒサの半減も重なった回数 ／ `ソラが受けた` ＝ ソラの総被ダメ（旧 → 新）。");
        Console.WriteLine();
        var rows = Bands().Where(r => Mine(r.F)).ToList();

        Console.WriteLine("## 表E. 行別（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 逸らした | うち殴った本人へ | 名目 | 届いた | §1 | §1 の割合 | 倒した | うち処刑持ちが育った | 宛先なし | ヒサ重なり | ソラが受けた 旧 → 新 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var r in rows)
        {
            double hits = 0, self = 0, exec = 0, moved = 0, landed = 0, vuln = 0, kills = 0, none = 0, stack = 0, takenO = 0, takenN = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult b = Fight(r.F, st, seed, New), o = Fight(r.F, st, seed, V0);
                    n++;
                    UnitTally t = T(b, "sora");
                    hits += t.DeflectHits; moved += t.DeflectMoved; landed += t.DeflectLanded; vuln += t.DeflectVulnAdded;
                    kills += t.DeflectKills; none += t.DeflectNoTarget; stack += t.DeflectBeckonStack;
                    takenN += t.DamageTaken; takenO += T(o, "sora").DamageTaken;
                    self += t.DeflectSelf; exec += t.DeflectExecFeed;
                }
            Console.WriteLine("| " + r.Name + " | " + (hits / n).ToString("F2") + " | " + (self / n).ToString("F2") + "（" + (hits > 0 ? (100.0 * self / hits).ToString("F0") : "—") + "%） | "
                              + (moved / n).ToString("F1") + " | " + (landed / n).ToString("F1") + " | "
                              + (vuln / n).ToString("F1") + " | " + (landed > 0 ? (100.0 * vuln / landed).ToString("F1") + "%" : "—") + " | "
                              + (kills / n).ToString("F3") + " | " + (exec / n).ToString("F3") + " | " + (none / n).ToString("F2") + " | " + (stack / n).ToString("F2") + " | "
                              + (takenO / n).ToString("F1") + " → **" + (takenN / n).ToString("F1") + "** |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表E''. ソラが何で削られ、何で倒れたか（第2〜5波・1戦あたり・`HarmRule.Census`）");
        Console.WriteLine();
        Console.WriteLine("`単体` は逸らした後にソラに残った量。範囲（薙ぎ・貫き・全体）は逸らさない。`致命` は倒れた一撃の経路（倒れた回数に対する割合）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 単体 | 薙ぎ | 貫き | 全体 | その他 | 致命: 単体 | 致命: 範囲 | 致命: その他 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in rows)
            foreach (Version v in new[] { V0, New })
            {
                var amt = new double[DamageRoutes.Count]; var fat = new double[DamageRoutes.Count]; int n = 0;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult b = BattleEngine.Run(Apply(r.F, v), EnemyCatalog.Stages[st].Enemy, seed, harm: new HarmRule(true));
                        n++;
                        UnitTally t = T(b, "sora");
                        if (t.HarmAmount is not null) for (int i = 0; i < amt.Length; i++) amt[i] += t.HarmAmount[i];
                        if (t.HarmFatal is not null) for (int i = 0; i < fat.Length; i++) fat[i] += t.HarmFatal[i];
                    }
                double A(DamageRoute d) => amt[(int)d] / n;
                double other = amt.Sum() / n - A(DamageRoute.Single) - A(DamageRoute.Sweep) - A(DamageRoute.Pierce) - A(DamageRoute.All);
                double fs = fat.Sum();
                string F(double x) => fs > 0 ? (100.0 * x / fs).ToString("F0") + "%" : "—";
                double fArea = fat[(int)DamageRoute.Sweep] + fat[(int)DamageRoute.Pierce] + fat[(int)DamageRoute.All];
                Console.WriteLine("| " + r.Name + " | " + v.Name + " | " + A(DamageRoute.Single).ToString("F1") + " | " + A(DamageRoute.Sweep).ToString("F1") + " | "
                                  + A(DamageRoute.Pierce).ToString("F1") + " | " + A(DamageRoute.All).ToString("F1") + " | " + other.ToString("F1") + "（"
                                  + string.Join("・", Enumerable.Range(0, amt.Length)
                                      .Where(i => i > (int)DamageRoute.All && amt[i] / n >= 0.5)
                                      .OrderByDescending(i => amt[i]).Select(i => (DamageRoute)i + " " + (amt[i] / n).ToString("F1"))) + "） | "
                                  + F(fat[(int)DamageRoute.Single]) + " | " + F(fArea) + " | " + F(fs - fat[(int)DamageRoute.Single] - fArea) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表E'. 波別の逸らした回数（新）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        var perWave = new double[5];
        foreach (var r in rows)
        {
            var w = new double[5];
            for (int st = 0; st < 5; st++)
            {
                for (int seed = 0; seed < Seeds; seed++) w[st] += T(Fight(r.F, st, seed, New), "sora").DeflectHits;
                w[st] /= Seeds; perWave[st] += w[st] / rows.Count;
            }
            Console.WriteLine("| " + r.Name + " | " + string.Join(" | ", w.Select(x => x.ToString("F2"))) + " |");
        }
        Console.WriteLine("| **平均** | " + string.Join(" | ", perWave.Select(x => "**" + x.ToString("F2") + "**")) + " |");
        Console.WriteLine();
    }

    // =================================================================================
    // 診断台
    // =================================================================================

    static readonly (string Name, Formation F)[] Benches =
    {
        // ソラ中央（隣4）。ヒサは後1 で、隣はソラ（96）と前1 のザン（56）——開戦時はソラを指す。
        ("台1 ヒサ×ソラ×ザン（ソラ中央・ヒサ後1）", Formation.Build(front1: UnitCatalog.Zan, front3: UnitCatalog.Borg,
            center: UnitCatalog.Sora, back1: UnitCatalog.Hisa, back3: UnitCatalog.Dolga)),
        // 同じ台でヒサだけノミに替えた（ヒサの半減が重ならない対照）。
        ("台2 ヒサなし（台1 のヒサ → ノミ）", Formation.Build(front1: UnitCatalog.Zan, front3: UnitCatalog.Borg,
            center: UnitCatalog.Sora, back1: UnitCatalog.Nomi, back3: UnitCatalog.Dolga)),
    };

    static void Bench()
    {
        Console.WriteLine("# 第186期 `sora186 bench` —— 診断台（`Presets` には足さない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1〜5波 | 第2〜5波平均 |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var b in Benches)
            foreach (Version v in new[] { V0, New })
            {
                double[] w = Rates(b.F, v);
                Console.WriteLine("| " + b.Name + " | " + v.Name + " | " + string.Join(" / ", w.Select(x => x.ToString("F1"))) + " | " + Mean25(w).ToString("F1") + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 帳簿（第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ソラの生存T | ソラの死亡率 | ソラが受けた | 逸らした | 届いた | ヒサ重なり | ヒサがソラを指した | ザンの倍返し | ザンの与害 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in Benches)
            foreach (Version v in new[] { V0, New })
            {
                double life = 0, dead = 0, taken = 0, hits = 0, landed = 0, stack = 0, picked = 0, vf = 0, zd = 0; int n = 0;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = Fight(b.F, st, seed, v);
                        n++;
                        UnitTally s = T(r, "sora");
                        life += LifeOf(r, "sora"); dead += s.Deaths > 0 ? 1 : 0; taken += s.DamageTaken;
                        hits += s.DeflectHits; landed += s.DeflectLanded; stack += s.DeflectBeckonStack; picked += s.BeckonPicked;
                        vf += T(r, "zan").VendettaFires; zd += T(r, "zan").DamageToEnemy;
                    }
                Console.WriteLine("| " + b.Name + " | " + v.Name + " | " + (life / n).ToString("F2") + " | " + (100.0 * dead / n).ToString("F1") + "% | "
                                  + (taken / n).ToString("F1") + " | " + (hits / n).ToString("F2") + " | " + (landed / n).ToString("F1") + " | "
                                  + (stack / n).ToString("F2") + " | " + (picked / n).ToString("F2") + " | " + (vf / n).ToString("F2") + " | " + (zd / n).ToString("F1") + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 1戦のログ（台1・第4波・seed 3・新）——逸らしの行だけ抜き出す");
        Console.WriteLine();
        Console.WriteLine("```");
        BattleResult one = Fight(Benches[0].F, 3, 3, New, verbose: true);
        foreach (var line in one.Log.Where(l => l.Text.Contains("逸らした") || l.Text.Contains("矢面") || l.Text.Contains("ターン")).Take(60))
            Console.WriteLine(line);
        Console.WriteLine("```");
        int deflEvents = one.Events.Count(e => e.Kind == BattleEventKind.Damage && e.DeflectFromId is not null);
        Console.WriteLine();
        Console.WriteLine("- 台本の `Damage` のうち `DeflectFromId` が載った件数: **" + deflEvents + "**（同じ戦の `DeflectHits` = "
                          + T(one, "sora").DeflectHits + "）");
        Console.WriteLine();
    }

    // =================================================================================
    // check
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第186期 `sora186 check` —— 自己検査");
        Console.WriteLine();
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        if (File.Exists(path))
        {
            var want = ReadBalance(path);
            int cellsOld = 0, cellsNot = 0, rowsSeen = 0, rowsNot = 0;
            foreach ((string name, Formation f) in CompareBuilds())
            {
                if (!want.TryGetValue(name, out double[]? w)) continue;
                rowsSeen++;
                double[] a = Rates(f, V0);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - w[i]) > 0.001) cellsOld++;
                if (Mine(f)) continue;
                rowsNot++;
                double[] b = Rates(f, New);
                for (int i = 0; i < 5; i++) if (Math.Abs(b[i] - w[i]) > 0.001) cellsNot++;
            }
            Console.WriteLine("- (a) 旧ソラの " + rowsSeen + " 行 × 5波 と `" + path + "` の差: **" + cellsOld + " セル**（採用前の balance を渡したとき 0 が正）");
            Console.WriteLine("- (b) ソラを含まない " + rowsNot + " 行 × 5波（新）と `" + path + "` の差: **" + cellsNot + " セル**（0 が正）");
        }
        else Console.WriteLine("- (a)(b) `" + path + "` が無いので飛ばした");

        // (c) 逸らしは単体の主目標だけ・宛先は敵・§1 の上乗せを敵が入れていない
        {
            long battles = 0, vulnByFoe = 0, deflectNonSora = 0;
            foreach ((string _, string _, Formation f) in Bands().Concat(Benches.Select(b => ("台", b.Name, b.F))))
            {
                if (!Mine(f)) continue;
                var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = Fight(f, st, seed, New);
                        battles++;
                        foreach (var kv in r.TallyByUnit)
                        {
                            if (!ids.Contains(kv.Key)) vulnByFoe += kv.Value.MarkVulnDealt;
                            if (kv.Key != "sora") deflectNonSora += kv.Value.DeflectHits;
                        }
                    }
            }
            Console.WriteLine("- (c) " + battles + " 戦: **§1 の上乗せを敵が入れた量 " + vulnByFoe + "**（0 が正）／ **ソラ以外が逸らした回数 " + deflectNonSora + "**（0 が正）");
        }

        // (d) PickOne を新しく使っていない
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
            string pick = "Pick" + "One(";
            int t = Count(traits, pick), e = Count(engine, pick);
            Console.WriteLine("- (d) `" + pick + "` の出現数: `Traits.cs` " + t + " ／ `BattleEngine.cs` " + e
                              + "（採用前と比べるのは報告書の側。`DeflectTrait` の本文には " + CountIn(traits, "Deflect" + "Trait", pick) + " 件・0 が正）");
        }
        Console.WriteLine();
    }

    static int Count(string s, string needle) => (s.Length - s.Replace(needle, "").Length) / needle.Length;

    static int CountIn(string src, string cls, string needle)
    {
        int i = src.IndexOf("class " + cls + " ");
        if (i < 0) return 0;
        int j = src.IndexOf("\npublic ", i + 10);
        string body = src.Substring(i, (j < 0 ? src.Length : j) - i);
        return Count(body, needle);
    }

    static Dictionary<string, double[]> ReadBalance(string path)
    {
        var d = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            string[] c = line.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            if (c.Length < 6) continue;
            var w = new double[5];
            bool ok = true;
            for (int i = 0; i < 5; i++)
                ok &= double.TryParse(c[1 + i].TrimEnd('%'), System.Globalization.NumberStyles.Float,
                                      System.Globalization.CultureInfo.InvariantCulture, out w[i]);
            if (ok) d[c[0]] = w;
        }
        return d;
    }

    static double[] Rates(Formation f, Version v, int seeds = Seeds, int baseSeed = 0)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = baseSeed; seed < baseSeed + seeds; seed++)
                if (Fight(f, st, seed, v).PlayerWon) wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells(double[] w) => string.Join(" / ", Enumerable.Range(1, 4).Select(i => w[i].ToString("F1")));
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");
}
