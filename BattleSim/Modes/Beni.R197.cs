using BattleCore;
using static Common;

// =====================================================================================
// beni run197 / ledger197 / check197（第197期） —— 紅蓮（満タンの余りを、敵全体を火と澱みで染める奔流に）
//
// 指示書は design/PHASE197_BENI_GUREN_SPEC.md ／ 報告は design/PHASE197_BENI_GUREN.md。
//
//     dotnet run --project BattleSim -c Release 0 beni run197      # §4-1〜§4-3（在席行 × 5版・差し替え表・第196期の主表）
//     dotnet run --project BattleSim -c Release 0 beni ledger197   # §4-4 帳簿（1戦あたり・第2〜5波）
//     dotnet run --project BattleSim -c Release 0 beni check197 [第196期のbalance.md]  # 自己検査
//
// **版は札で切り替える**（第196期 `SpewFixed` と同じ作法・対照の札は `UnitCatalog.All` に保持者 0 枚）。規定の版（`UnitCatalog.Beni`）は「毒」。
// =====================================================================================

static partial class BeniDiag
{
    /// <summary>「無」＝第196期のベニ（紅蓮の札なし）。</summary>
    static UnitDef Beni196 => Clone(UnitCatalog.Beni, new[] { TraitId.Inverse, TraitId.Kindle, TraitId.Taint, TraitId.InverseLeak }, UnitCatalog.Beni.Actions);
    static UnitDef BeniWith(TraitId g) => Clone(UnitCatalog.Beni, new[] { TraitId.Inverse, g, TraitId.Kindle, TraitId.Taint, TraitId.InverseLeak }, UnitCatalog.Beni.Actions);

    /// <summary>**プロパティ**（partial クラスの静的フィールドの初期化順はファイルをまたいで決まらない・第196期に1度踏んだ）。</summary>
    static (string Tag, UnitDef D)[] Versions197 => new[]
    {
        ("無", Beni196), ("毒", UnitCatalog.Beni), ("直撃", BeniWith(TraitId.GurenStrike)),
        ("低閾", BeniWith(TraitId.GurenLow)), ("全額", BeniWith(TraitId.GurenFull)),
    };
    static (string Tag, UnitDef D)[] Versions197Main => Versions197.Take(3).ToArray();

    static void RunMore197(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run197": Run197(); handled = true; return;
            case "ledger197": Ledger197(); handled = true; return;
            case "check197": Check197(arg); handled = true; return;
            case "waves197": Waves197(); handled = true; return;
        }
    }

    /// <summary>差し替え表（第190期 5.2）の行と替えた席。選び方は <see cref="SwapRows"/> と同じ規則。</summary>
    static List<(string Name, Formation F, int Slot)> SwapSeats197()
    {
        var list = new List<(string, Formation, int)>();
        foreach (var (name, f) in CompareBuilds())
        {
            var occ = f.Occupied().ToList();
            if (!occ.Any(o => Writers.Contains(o.Def.Id)) || HasBeni(f)) continue;
            double baseW = Mean25(Rates(f));
            int slot = occ.Where(o => !Writers.Contains(o.Def.Id))
                          .Select(o => (o.Slot, A: baseW - Mean25(Rates(SwapAt(f, o.Slot, Plain(o.Def))))))
                          .OrderBy(x => x.A).ThenBy(x => x.Slot).First().Slot;
            list.Add((name, f, slot));
        }
        return list;
    }

    // =================================================================================
    // run197
    // =================================================================================

    static void Run197()
    {
        Console.WriteLine("# 第197期 `beni run197` —— 紅蓮（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("seed 0..199・平均は第2〜5波。**無** ＝ 紅蓮なし（第196期のベニ）／ **毒**（規定）＝ 閾値 " + GurenTrait.Threshold
                          + "・着火＋等分した毒の層 ／ **直撃** ＝ 着火＋等分した直撃ダメージ ／ **低閾** ＝ 毒の版で閾値 " + GurenTrait.LowThreshold
                          + " ／ **全額** ＝ 毒の版で等分せず満額。スィドは規定の版（S）。");
        Console.WriteLine();

        Console.WriteLine("## 表A. `compare` のベニの在席行 × 5版（§4-1）");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 版 | 第2 / 3 / 4 / 5 波 | 第2〜5波 | 無との差 |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        foreach (var (band, name, f) in BeniRows())
        {
            double baseW = 0; bool first = true;
            foreach (var (tag, d) in Versions197)
            {
                double[] w = Rates(Apply(f, d));
                if (tag == "無") baseW = Mean25(w);
                Console.WriteLine("| " + (first ? band : "") + " | " + (first ? name : "") + " | " + tag + " | " + Cells(w) + " | "
                                  + Mean25(w).ToString("F1") + " | " + (tag == "無" ? "—" : D(Mean25(w) - baseW)) + " |");
                first = false;
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 表B. 差し替え表（§4-2・第190期 5.2 と同じ 25 行・同じ席）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 替えた駒・席 | 無 | **毒** | 直撃 | 毒−無 | 直撃−無 | 毒−直撃 | 放った戦（毒） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        var agg = new List<(double N, double P, double S, double Fired)>();
        foreach (var (name, f, slot) in SwapSeats197())
        {
            var r = Versions197Main.ToDictionary(v => v.Tag, v => Mean25(Rates(SwapAt(f, slot, v.D))));
            double fired = FiredShare(SwapAt(f, slot, UnitCatalog.Beni));
            agg.Add((r["無"], r["毒"], r["直撃"], fired));
            Console.WriteLine("| " + name + " | " + f[slot]!.Name + "・" + SlotName(slot) + " | " + r["無"].ToString("F1") + " | **" + r["毒"].ToString("F1") + "** | "
                              + r["直撃"].ToString("F1") + " | " + D(r["毒"] - r["無"]) + " | " + D(r["直撃"] - r["無"]) + " | " + D(r["毒"] - r["直撃"]) + " | "
                              + (100 * fired).ToString("F1") + "% |");
        }
        Console.WriteLine();
        if (agg.Count == 0) { Console.WriteLine("**行が 0 本。止める**（R034）。"); return; }
        Console.WriteLine("25 行の平均: 無 " + agg.Average(x => x.N).ToString("F1") + " ／ 毒 " + agg.Average(x => x.P).ToString("F1") + " ／ 直撃 " + agg.Average(x => x.S).ToString("F1")
                          + " ／ 毒−無 " + D(agg.Average(x => x.P - x.N)) + " ／ 直撃−無 " + D(agg.Average(x => x.S - x.N))
                          + " ／ 放った戦 " + (100 * agg.Average(x => x.Fired)).ToString("F1") + "%");
        var busy = agg.Where(x => x.Fired >= 0.2).ToList();
        Console.WriteLine("- 放った戦が 20% 以上の行（" + busy.Count + " 行）: 毒−無 " + (busy.Count == 0 ? "—" : D(busy.Average(x => x.P - x.N)))
                          + " ／ 直撃−無 " + (busy.Count == 0 ? "—" : D(busy.Average(x => x.S - x.N)))
                          + " ／ それ以外（" + (agg.Count - busy.Count) + " 行）: 毒−無 " + D(agg.Where(x => x.Fired < 0.2).DefaultIfEmpty().Average(x => x.P - x.N)));
        Console.WriteLine();

        Console.WriteLine("## 表C. 第196期の主表（§4-3・前列スィド＋中央ベニの7行）");
        Console.WriteLine();
        Console.WriteLine("**元** ＝ `compare` の行のまま（ガルドあり・ベニのいる2行だけ版が効く）／ **元'** ＝ ガルドあり・ベニ中央（参考）／ "
                          + "**S** ＝ ベニ中央・ガルドの席に規定の版のスィド（主）。`倒れ` はガルドの席のスィドが倒れた戦の割合。"
                          + "**`追撃×毒` の元' は除外**（中央のグザ＝唯一の毒の書き手をベニに替えるので 0.0% に張り付く・第196期 R016 の再発・ポンの指示）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 元 無 ／ 毒 ／ 直撃 | 元' 無 ／ 毒 ／ 直撃 | **S 無** | **S 毒** | **S 直撃** | S 毒−無 | S 直撃−無 | 倒れ 無 ／ 毒 ／ 直撃 | 放った戦（S 毒） | S 毒の波別 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|--:|---|");
        var sums = new Dictionary<string, double>();
        var sumsBC = new Dictionary<string, double>(); int nBC = 0;
        var rows = SidDiag.MainForms197();
        foreach (var (name, orig, bc, s) in rows)
        {
            var o = Versions197Main.ToDictionary(v => v.Tag, v => Mean25(Rates(Apply(orig, v.D))));
            bool exclude = name.StartsWith("追撃×毒");
            var b = exclude ? null : Versions197Main.ToDictionary(v => v.Tag, v => Mean25(Rates(Apply(bc, v.D))));
            var m = Versions197Main.ToDictionary(v => v.Tag, v => SidMeasure(Apply(s, v.D)));
            foreach (var (tag, _) in Versions197Main) sums[tag] = sums.GetValueOrDefault(tag) + Mean25(m[tag].W);
            if (b is not null) { nBC++; foreach (var (tag, _) in Versions197Main) sumsBC[tag] = sumsBC.GetValueOrDefault(tag) + b[tag]; }
            string Tri(Dictionary<string, double> x) => x["無"].ToString("F1") + " ／ " + x["毒"].ToString("F1") + " ／ " + x["直撃"].ToString("F1");
            Console.WriteLine("| " + name + " | " + Tri(o) + " | " + (b is null ? "—（除外）" : Tri(b)) + " | "
                              + Mean25(m["無"].W).ToString("F1") + " | **" + Mean25(m["毒"].W).ToString("F1") + "** | " + Mean25(m["直撃"].W).ToString("F1") + " | "
                              + D(Mean25(m["毒"].W) - Mean25(m["無"].W)) + " | " + D(Mean25(m["直撃"].W) - Mean25(m["無"].W)) + " | "
                              + P(m["無"].SidDead, m["無"].N) + " ／ " + P(m["毒"].SidDead, m["毒"].N) + " ／ " + P(m["直撃"].SidDead, m["直撃"].N) + " | "
                              + P(m["毒"].Fired, m["毒"].N) + " | " + Cells(m["毒"].W) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("7 行の平均（S）: " + string.Join(" ／ ", Versions197Main.Select(v => v.Tag + " " + (sums[v.Tag] / rows.Count).ToString("F1")))
                          + "。元' の平均（`追撃×毒` を除く " + nBC + " 行）: " + string.Join(" ／ ", Versions197Main.Select(v => v.Tag + " " + (sumsBC[v.Tag] / Math.Max(1, nBC)).ToString("F1"))));
        Console.WriteLine();
    }

    sealed class SM { public double[] W = new double[5]; public int N, SidDead, Fired; }

    /// <summary>勝率（5波）と、第2〜5波のスィドの倒れ・紅蓮を放った戦。</summary>
    static SM SidMeasure(Formation f)
    {
        var m = new SM();
        var lk = new object();
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            int stage = st;
            Parallel.For(0, Seeds, seed =>
            {
                BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
                if (r.PlayerWon) Interlocked.Increment(ref wins);
                if (stage == 0) return;
                bool dead = r.TallyByUnit.TryGetValue("sid", out UnitTally? s) && s.Deaths > 0;
                bool fired = r.TallyByUnit.TryGetValue("beni", out UnitTally? b) && b.GurenFires > 0;
                lock (lk) { m.N++; if (dead) m.SidDead++; if (fired) m.Fired++; }
            });
            m.W[st] = 100.0 * wins / Seeds;
        }
        return m;
    }

    static double FiredShare(Formation f)
    {
        int n = 0, k = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                n++;
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).TallyByUnit.TryGetValue("beni", out UnitTally? b) && b.GurenFires > 0) k++;
            }
        return (double)k / n;
    }

    // =================================================================================
    // ledger197 —— §4-4
    // =================================================================================

    sealed class GL
    {
        public int N, FiredN; public double FirstSum;
        public UnitTally T = new();
    }

    static GL GurenLedger(Formation f)
    {
        var g = new GL();
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                g.N++;
                if (!r.TallyByUnit.TryGetValue("beni", out UnitTally? b)) continue;
                g.T.Add(b);
                if (b.GurenFires > 0) { g.FiredN++; g.FirstSum += b.GurenFirstTurn; }
            }
        return g;
    }

    static void Ledger197()
    {
        Console.WriteLine("# 第197期 `beni ledger197` —— 紅蓮の帳簿（§4-4・1戦あたり・第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("`溜めた` 紅蓮の総量 ／ `放った` 回数 ／ `放った戦` 1回以上放った戦の割合 ／ `初回T` 放った戦の初めて放ったターンの平均 ／ "
                          + "`新／既` 1回の奔流で新しく火が点いた敵・既に燃えていた敵（1回あたり）／ `層` 奔流で積んだ毒の層 ／ "
                          + "`毒の刻み 本／印` 奔流の層の刻みの額面（1回目 ／ 濃縮の印の2回目以降）／ `ミオ +4 層／刻み` 奔流の毒しか無かった敵にミオが足した +4 と、その層の刻み（印の分込み）／ "
                          + "`火の刻み 新／煽／印` 奔流が点けた火・煽った火の刻み（1回目）と印の2回目以降 ／ `直撃` 直撃の版の実額（名目）。");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行 | 版 | 溜めた | 放った | 放った戦 | 初回T | 新／既 | 層 | 毒の刻み 本／印 | ミオ +4 層／刻み | 火の刻み 新／煽／印 | 直撃 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|--:|---|---|---|---|");
        var groups = new List<(string Group, Formation F, string Name)>();
        foreach (var (band, name, f) in BeniRows()) groups.Add(("在席", f, name));
        foreach (var (name, _, _, s) in SidDiag.MainForms197()) groups.Add(("主表S", s, name));
        foreach (var (name, f, slot) in SwapSeats197()) groups.Add(("差し替え", SwapAt(f, slot, UnitCatalog.Beni), name));
        var tot = new Dictionary<(string, string), GL>();
        foreach (var (grp, f, name) in groups)
            foreach (var (tag, d) in Versions197.Skip(1))
            {
                GL g = GurenLedger(Apply(f, d));
                var key = (grp, tag);
                if (!tot.TryGetValue(key, out GL? a)) tot[key] = a = new GL();
                a.N += g.N; a.FiredN += g.FiredN; a.FirstSum += g.FirstSum; a.T.Add(g.T);
                if (grp == "差し替え" && g.FiredN < 0.05 * g.N) continue;   // 放った戦が 5% 未満の差し替え行は群の合計にだけ入れる
                PrintGL(grp, name, tag, g);
            }
        Console.WriteLine();
        Console.WriteLine("### 群の合計");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行 | 版 | 溜めた | 放った | 放った戦 | 初回T | 新／既 | 層 | 毒の刻み 本／印 | ミオ +4 層／刻み | 火の刻み 新／煽／印 | 直撃 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|--:|---|---|---|---|");
        foreach (var ((grp, tag), g) in tot) PrintGL(grp, "（計）", tag, g);
        Console.WriteLine();
    }

    static void PrintGL(string grp, string name, string tag, GL g)
    {
        UnitTally t = g.T; double n = g.N;
        string F1(double x) => (x / n).ToString("F1");
        double fires = t.GurenFires;
        Console.WriteLine("| " + grp + " | " + name + " | " + tag + " | " + F1(t.GurenGained) + " | " + (fires / n).ToString("F2") + " | " + P(g.FiredN, g.N) + " | "
                          + (g.FiredN == 0 ? "—" : (g.FirstSum / g.FiredN).ToString("F2")) + " | "
                          + (fires == 0 ? "—" : (t.GurenLitNew / fires).ToString("F2") + "／" + (t.GurenRelit / fires).ToString("F2")) + " | "
                          + F1(t.GurenLayers) + " | " + F1(t.GurenPoisonTick) + "／" + F1(t.GurenPoisonTickMark) + " | "
                          + F1(t.GurenMioLayers) + "／" + F1(t.GurenMioTick + t.GurenMioTickMark) + " | "
                          + F1(t.GurenBurnTickNew) + "／" + F1(t.GurenBurnTickRelit) + "／" + F1(t.GurenBurnTickMark) + " | "
                          + (t.GurenStrikeNominal == 0 ? "—" : F1(t.GurenStrikeDealt) + "（" + F1(t.GurenStrikeNominal) + "）") + " |");
    }

    // =================================================================================
    // check197 —— 自己検査
    // =================================================================================

    static void Check197(string arg)
    {
        Console.WriteLine("# 第197期 `beni check197` —— 自己検査");
        Console.WriteLine();
        string path = arg.Length > 0 ? arg : Path.Combine("docs", "balance.md");
        var before = File.Exists(path) ? File.ReadAllLines(path).Where(l => l.StartsWith("| ") && l.Contains('%')).ToList() : new List<string>();
        var rows = CompareBuilds();

        // (a)(b) —— 第196期の表（`balance.md` は第196期の盤面で生成されている前提）
        int diffNon = 0, cellsNon = 0, diffNone = 0, cells = 0;
        foreach (var (name, f) in rows)
        {
            string? line = before.FirstOrDefault(l => l.StartsWith("| " + name + " |"));
            if (line is null) { Console.WriteLine("- 行が引けない: " + name); continue; }
            var old = line.Split('|').Skip(2).Take(5).Select(c => double.Parse(c.Trim().TrimEnd('%'))).ToArray();
            double[] now = Rates(f);
            double[] none = Rates(Apply(f, Beni196));
            for (int i = 0; i < 5; i++)
            {
                cells++;
                if (Math.Abs(none[i] - old[i]) > 0.05) diffNone++;
                if (!HasBeni(f)) { cellsNon++; if (Math.Abs(now[i] - old[i]) > 0.05) diffNon++; }
            }
        }
        Console.WriteLine("- (a) ベニを含まない行 × 5波: **" + diffNon + " / " + cellsNon + "** セルが `" + path + "` と違う");
        Console.WriteLine("- (b) 「無」（紅蓮の札を外したベニ）で組んだ全行 × 5波: **" + diffNone + " / " + cells + "** セルが違う");

        // (c) 乱数を引かない
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            int a = traits.IndexOf("public sealed class " + "GurenTrait"), e = traits.IndexOf("/// 突きの対照（第186期 追補）", a);
            string body = a < 0 || e < 0 ? "" : traits.Substring(a, e - a);
            string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
            int ea = engine.IndexOf("readonly List<UnitState> " + "_gurenHolders"), ee = engine.IndexOf("/// <summary>反転で癒えた味方の", ea);
            string ebody = ea < 0 || ee < 0 ? "" : engine.Substring(ea, ee - ea);
            int rng = new[] { "PickOne(", "Roll(", "Shuffle(" }.Sum(w => Count(body, w) + Count(ebody, w));
            Console.WriteLine("- (c) `GurenTrait` 系の本文（" + body.Length + " 字）と engine の紅蓮の計数（" + ebody.Length + " 字）に `PickOne` / `Roll` / `Shuffle`: **" + rng + "**"
                              + (body.Length == 0 || ebody.Length == 0 ? "（**切り出しが空**）" : ""));
        }

        // (d)(e)(f) —— 台本（verbose）
        int gainAfterSip = 0, gainTotal = 0, gainOnGald = 0, sipOnGald = 0, gainInDrought = 0, releases = 0, releaseAfterSkill = 0, gainNone = 0;
        var forms = new List<Formation>();
        foreach (var (_, _, f) in BeniRows()) forms.Add(f);
        foreach (var (_, _, bc, s) in SidDiag.MainForms197()) { forms.Add(bc); forms.Add(s); }
        foreach (Formation f0 in forms)
            foreach (var (tag, d) in new[] { ("無", Beni196), ("毒", UnitCatalog.Beni) })
            {
                Formation f = Apply(f0, d);
                var players = f.Occupied().ToList();
                int beni = players.FindIndex(o => o.Def.Id == "beni");
                int gald = players.FindIndex(o => o.Def.Id == "gald");
                for (int st = 1; st < 5; st++)
                {
                    var foes = EnemyCatalog.Stages[st].Enemy.Occupied().ToList();
                    int drought = foes.FindIndex(o => o.Def.Traits.Contains(TraitId.Drought));
                    int droughtId = drought < 0 ? -1 : players.Count + drought;
                    for (int seed = 0; seed < 50; seed++)
                    {
                        var ev = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: true).Events;
                        int droughtDeadTurn = droughtId < 0 ? 0 : ev.FirstOrDefault(x => x.Kind == BattleEventKind.Death && x.TargetId == droughtId)?.Turn ?? int.MaxValue;
                        int lastSkillTurn = -1;
                        for (int i = 0; i < ev.Count; i++)
                        {
                            var x = ev[i];
                            if (x.Kind == BattleEventKind.Skill && x.ActorId == beni) lastSkillTurn = x.Turn;
                            if (x.Kind == BattleEventKind.InverseSip && x.TargetId == gald && gald >= 0) sipOnGald++;
                            if (x.Kind == BattleEventKind.GurenGain)
                            {
                                gainTotal++;
                                if (tag == "無") gainNone++;
                                if (x.TargetId == gald && gald >= 0) gainOnGald++;
                                if (droughtId >= 0 && x.Turn < droughtDeadTurn) gainInDrought++;
                                // 直前（同じ駒の `Heal` を挟んでよい）に啜りがあること
                                for (int j = i - 1; j >= 0 && j >= i - 3; j--)
                                    if (ev[j].Kind == BattleEventKind.InverseSip && ev[j].TargetId == x.TargetId) { gainAfterSip++; break; }
                            }
                            if (x.Kind == BattleEventKind.GurenRelease && x.TargetId is null)
                            {
                                releases++;
                                if (lastSkillTurn == x.Turn) releaseAfterSkill++;
                            }
                        }
                    }
                }
            }
        Console.WriteLine("- (d) 渇きの司祭が生きているターンの `GurenGain`: **" + gainInDrought + "** ／ ガルド（支援拒否）への啜り・紅蓮: **" + sipOnGald + " ／ " + gainOnGald + "**"
                          + "（在席行・主表の元'／S × 無・毒 × 第2〜5波 × seed 0..49・台本）");
        Console.WriteLine("- (e) `GurenGain` " + gainTotal + " 件のうち、直前に同じ隣への啜り（`InverseSip`）があるもの **" + gainAfterSip + "**。「無」の版の `GurenGain`: **" + gainNone + "**");
        Console.WriteLine("- (f) 放った見出し（`GurenRelease`・`TargetId` null）" + releases + " 件のうち、同じターンにベニの `Skill` の後にあるもの **" + releaseAfterSkill + "**");
        Console.WriteLine("- (g) `PoisonRoute` " + Enum.GetValues<PoisonRoute>().Length + " 本 ／ `SoakRouteCount` " + BattleContext.SoakRouteCount
                          + " ／ 燃焼の添字 " + BattleContext.SoakBurnRouteIx + "（重ならないこと: "
                          + (BattleContext.SoakBurnRouteIx >= Enum.GetValues<PoisonRoute>().Length && BattleContext.SoakRouteCount == BattleContext.SoakBurnRouteIx + 1 ? "○" : "**×**") + "）");
        int holders = UnitCatalog.All.Count(u => u.Traits.Contains(TraitId.GurenStrike) || u.Traits.Contains(TraitId.GurenLow) || u.Traits.Contains(TraitId.GurenFull));
        Console.WriteLine("- (h) 対照の札（直撃・低閾・全額）の `UnitCatalog.All` の保持者: **" + holders + "** 枚");
        Console.WriteLine();

        static int Count(string s, string w) { int c = 0, i = 0; while ((i = s.IndexOf(w, i, StringComparison.Ordinal)) >= 0) { c++; i += w.Length; } return c; }
    }

    // =================================================================================
    // waves197 —— 事後・参考: 波ごとの放った戦と勝率（毒の版と無の版）
    // =================================================================================

    static void Waves197()
    {
        Console.WriteLine("# 第197期 `beni waves197` —— 事後・参考: 波ごとに、放った戦・初回T・ベニが倒れた戦と勝率（毒 ／ 無）");
        Console.WriteLine();
        Console.WriteLine("seed 0..199。`放った戦` は毒の版。`勝率` は 無 → 毒。`倒れT` は毒の版でベニが倒れた戦の倒れたターンの平均。`決着T` は毒の版の平均。");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行 | 波 | 放った戦 | 初回T | ベニが倒れた戦 | 倒れT | 決着T | 勝率 無 → 毒 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|");
        var groups = new List<(string, string, Formation)>();
        foreach (var (_, name, f) in BeniRows()) groups.Add(("在席", name, f));
        foreach (var (name, _, _, s) in SidDiag.MainForms197()) groups.Add(("主表S", name, s));
        foreach (var (grp, name, f) in groups)
            for (int st = 1; st < 5; st++)
            {
                int fired = 0, dead = 0, wN = 0, wP = 0; double first = 0, deadT = 0, turns = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(Apply(f, UnitCatalog.Beni), EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    BattleResult r0 = BattleEngine.Run(Apply(f, Beni196), EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    if (r.PlayerWon) wP++; if (r0.PlayerWon) wN++;
                    turns += r.Turns;
                    if (r.TallyByUnit.TryGetValue("beni", out UnitTally? b))
                    {
                        if (b.GurenFires > 0) { fired++; first += b.GurenFirstTurn; }
                        if (b.Deaths > 0) { dead++; deadT += b.LastActiveTurn; }
                    }
                }
                Console.WriteLine("| " + grp + " | " + name + " | 第" + (st + 1) + "波 " + EnemyCatalog.Stages[st].Name + " | " + P(fired, Seeds) + " | "
                                  + (fired == 0 ? "—" : (first / fired).ToString("F2")) + " | " + P(dead, Seeds) + " | " + (dead == 0 ? "—" : (deadT / dead).ToString("F2")) + " | "
                                  + (turns / Seeds).ToString("F2") + " | " + (100.0 * wN / Seeds).ToString("F1") + " → " + (100.0 * wP / Seeds).ToString("F1") + " |");
            }
        Console.WriteLine();
    }

}
