using BattleCore;
using static Common;

// =====================================================================================
// ledger モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "ledger")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 ledger
// =====================================================================================

static class LedgerDiag
{
// ==================================================================================
// 第111期 —— 帳簿の期。**機構を1つも変えない。** engine・`Traits.cs`・`UnitCatalog.All` は
// 1文字も触らない。動かすのは `Presets.Compare` の1行と `Presets.Cross` の1行だけ。
//
// **行数は 61 のまま、1行を差し替える**（第60期・第108期と同じ形）——`compare` の
// 61 行 × 5 波 = 305 セルは器具の定数として十数箇所に組み込まれているので、
// 62 行にすると計測器と測定対象を同時に動かす期になる（指示書 §0-1）。
//
//     dotnet run --project BattleSim -c Release 0 ledger phase0   # §4（冗長表・一意性・速さ・不変量）
//     dotnet run --project BattleSim -c Release 0 ledger unique   # §4-2 の実測版（規則を1本ずつ切る）
//     dotnet run --project BattleSim -c Release 0 ledger seat     # §1-3 の席探索（120通り・情報セルつき）
//     dotnet run --project BattleSim -c Release 0 ledger cross    # §2 の交差帯（ハリの席の候補）
//     dotnet run --project BattleSim -c Release 0 ledger check    # §5 と自己検査
//
// **既存の診断は1文字も書き換えていない。**
public static void Run(string[] args, int stageIndex)
{
    string ldMode = args.Length > 2 ? args[2] : "phase0";
    var ldCompare = CompareBuilds();
    var ldCross = CrossBuilds();
    IReadOnlyList<EnemyCatalog.Stage> ldStages = EnemyCatalog.Stages;
    const int LdSeeds = 200;      // `compare` と揃える
    const int LdScan = 50;        // 粗探索。`reseat` / `layout` と揃える
    const int LdCfBase = 200;     // 追試の帯（選定に使っていない seed）
    const int LdCfSeeds = 400;
    const double LdLine = 5.0;    // 第46期の採否閾値
    var ldPrim = new HashSet<string>(Baseline.PrimaryRows);

    // --- 情報セル: `0 < x < 100` を**第2〜5波**で数える（第59期 9-1 の定義。規約 (G10)）
    static int LdInfo(double[] cells)
    {
        int n = 0;
        for (int i = 1; i < cells.Length; i++) if (cells[i] > 0.0 && cells[i] < 100.0) n++;
        return n;
    }

    static double LdCorr(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        int n = a.Count;
        double ma = a.Average(), mb = b.Average(), num = 0, da = 0, db = 0;
        for (int i = 0; i < n; i++) { num += (a[i] - ma) * (b[i] - mb); da += (a[i] - ma) * (a[i] - ma); db += (b[i] - mb) * (b[i] - mb); }
        if (da <= 0 || db <= 0) return double.NaN;
        return num / Math.Sqrt(da * db);
    }

    static string LdN(UnitDef? d) => d?.Name ?? "−";
    static string LdSeats(Formation f) => LdN(f[0]) + "/" + LdN(f[1]) + " | " + LdN(f[2]) + " | " + LdN(f[3]) + "/" + LdN(f[4]);

    // 5枚組（順不同）の指紋。第108期 §A-1 の作法——**席だけ違う行は「作り直し」なので採らない。**
    static string LdKey(Formation f)
        => string.Join(",", f.Occupied().Select(o => o.Def.Id).OrderBy(x => x, StringComparer.Ordinal));

    // `reseat` の狙（ガルドが前列 / セッキが後列）の写し。**判定は1文字も変えていない。**
    static bool LdIntent(Formation f)
    {
        foreach (var (slot, def) in f.Occupied())
        {
            if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
            if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
        }
        return true;
    }

    double[] LdCells(Formation f, int seedFrom, int seeds)
        => ldStages.Select(st =>
        {
            int w = 0;
            for (int seed = seedFrom; seed < seedFrom + seeds; seed++)
                if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) w++;
            return w * 100.0 / seeds;
        }).ToArray();

    // --- 指示書 §1-1 の5枚組（**第110期で唯一まともに測れた台**。席は §1-3 で測り直す）
    const string LdTomoName = "灯×薙ぎ (トモ×ドルガ)";
    Formation LdTomoBase() => Formation.Build(
        front1: UnitCatalog.Dolga, front3: UnitCatalog.Gald, center: UnitCatalog.Sora,
        back1: UnitCatalog.Tomo, back3: UnitCatalog.Borg);
    var ldTomoMembers = new[] { UnitCatalog.Dolga, UnitCatalog.Gald, UnitCatalog.Sora, UnitCatalog.Tomo, UnitCatalog.Borg };

    // --- 指示書 §2-2 の候補（交差帯 `傷×被弾` のハリの席＝後1 に入れる駒）
    var ldCrossCands = new[] { UnitCatalog.Nomi, UnitCatalog.Kiri, UnitCatalog.Nata };
    int ldWoundHitIdx = Array.FindIndex(ldCross, b => b.Name.StartsWith("傷×被弾", StringComparison.Ordinal));
    Formation LdCrossWith(UnitDef d) => Formation.Build(
        front1: UnitCatalog.Gald, front3: UnitCatalog.Kado, center: UnitCatalog.Lili,
        back1: d, back3: UnitCatalog.Egu);

    // --- `docs/balance.md` の現行値（**戦闘不要**。指示書 §4-1）
    Dictionary<string, double[]> LdBalance()
    {
        var map = new Dictionary<string, double[]>(StringComparer.Ordinal);
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Path.GetDirectoryName(root);
        if (root == null) return map;
        foreach (string line in File.ReadAllLines(Path.Combine(root, "docs", "balance.md")))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            var parts = line.Split('|', StringSplitOptions.None).Select(p => p.Trim()).ToArray();
            if (parts.Length < 8) continue;
            var v = new List<double>();
            for (int i = 2; i < parts.Length - 1; i++)
                if (parts[i].EndsWith("%") && double.TryParse(parts[i].TrimEnd('%'), out double d)) v.Add(d);
            if (v.Count == ldStages.Count) map[parts[1]] = v.ToArray();
        }
        return map;
    }

    // ==============================================================================
    // phase0 —— 指示書 §4。**戦闘は1回も回さない。**
    // ==============================================================================
    if (ldMode == "phase0")
    {
        Console.WriteLine("# 第111期 Phase 0 —— 帳簿の期");
        Console.WriteLine();
        Console.WriteLine("**戦闘は1回も回していない。** 表A は `docs/balance.md` の現行値から、"
            + "表B〜F は実装（`Presets` / `UnitCatalog` / `TraitEntryMap` / `TraitKeyMap`）から derive した。");
        Console.WriteLine();

        var bal = LdBalance();
        int readable = ldCompare.Count(b => bal.ContainsKey(b.Name));
        Console.WriteLine($"`docs/balance.md` から読めた行 **{readable} / {ldCompare.Length}**"
            + $"（{ldCompare.Length} 行 × {ldStages.Count} 波 ＝ **{ldCompare.Length * ldStages.Count} セル**"
            + $"・指示書の 305: {(ldCompare.Length * ldStages.Count == 305 ? "○" : "**×**")}）。");

        // ---- 表A: 冗長表（§1-2 (c)）
        // r と max|Δ| は**第2〜5波**で取る（規約 (G10)。第一波は 61 行すべて 100.0% の教習波で、
        // 分母に入れると r が構造的に膨らむ）。参考として全5波の r も併記する。
        Console.WriteLine();
        Console.WriteLine("## 表A —— 61 行の冗長表（`docs/balance.md` の現行値・**戦闘不要**）");
        Console.WriteLine();
        Console.WriteLine("`r` と `max|Δ|` は**第2〜5波**で取る（規約 (G10)）。参考の `r(全5波)` は第一波を含めた値"
            + "——**61 行すべてが第一波 100.0% なので、含めると r が構造的に膨らむ。**");
        Console.WriteLine();
        Console.WriteLine("| # | 行 | 主判定 | 情報セル | 最も近い相手 | r | max\\|Δ\\| | **線** | r(全5波) |");
        Console.WriteLine("|--:|---|:-:|--:|---|--:|--:|:-:|--:|");

        var ldRed = new List<(string Name, int Info, string Near, double R, double MaxD, bool Prim, double R5)>();
        foreach (var b in ldCompare)
        {
            if (!bal.TryGetValue(b.Name, out double[]? v)) continue;
            string near = "—"; double bestR = double.NegativeInfinity, bestD = 0, bestR5 = double.NaN;
            foreach (var o in ldCompare)
            {
                if (o.Name == b.Name || !bal.TryGetValue(o.Name, out double[]? w)) continue;
                double r = LdCorr(v.Skip(1).ToArray(), w.Skip(1).ToArray());
                if (double.IsNaN(r)) continue;
                if (r > bestR)
                {
                    bestR = r; near = o.Name; bestR5 = LdCorr(v, w);
                    bestD = v.Skip(1).Zip(w.Skip(1), (x, y) => Math.Abs(x - y)).Max();
                }
            }
            ldRed.Add((b.Name, LdInfo(v), near, bestR, bestD, ldPrim.Contains(b.Name), bestR5));
        }
        int rank = 0;
        foreach (var e in ldRed.OrderByDescending(e => e.R))
        {
            rank++;
            bool line = e.R >= 0.95 && e.MaxD <= 10.0;
            Console.WriteLine($"| {rank} | {e.Name} | {(e.Prim ? "**P**" : "")} | {e.Info} | {e.Near} "
                + $"| {e.R:+0.0000;-0.0000} | {e.MaxD:F1} | {(line ? "**○**" : "")} "
                + $"| {(double.IsNaN(e.R5) ? "—" : e.R5.ToString("+0.0000;-0.0000"))} |");
        }

        Console.WriteLine();
        Console.WriteLine("### §1-2 の規則を当てる");
        Console.WriteLine();
        var pool = ldRed.Where(e => !e.Prim).ToList();                       // (a) 主判定19行を外す
        var hits = pool.Where(e => e.R >= 0.95 && e.MaxD <= 10.0).OrderByDescending(e => e.R).ToList();
        Console.WriteLine($"(a) 主判定19行を外した残り **{pool.Count} 行**。"
            + $"うち線（r ≥ 0.95 かつ max\\|Δ\\| ≤ 10pt）を満たす行 **{hits.Count} 行**。");
        Console.WriteLine();
        Console.WriteLine("| 順 | 行 | 情報セル | 最も近い相手 | r | max\\|Δ\\| |");
        Console.WriteLine("|--:|---|--:|---|--:|--:|");
        for (int i = 0; i < hits.Count; i++)
            Console.WriteLine($"| {i + 1} | {hits[i].Name} | {hits[i].Info} | {hits[i].Near} "
                + $"| {hits[i].R:+0.0000;-0.0000} | {hits[i].MaxD:F1} |");
        Console.WriteLine();
        var minInfo = pool.OrderBy(e => e.Info).ThenByDescending(e => e.R).ToList();
        int lowest = minInfo.Count == 0 ? -1 : minInfo[0].Info;
        Console.WriteLine($"**情報セル最少は {lowest}**（該当 {minInfo.Count(e => e.Info == lowest)} 行）: "
            + string.Join(" / ", minInfo.Where(e => e.Info == lowest).Select(e => e.Name)));
        Console.WriteLine();
        Console.WriteLine("> 線を満たす行が複数あるので、(c)（**「r が最大の行」＝いちばん冗長な行**）で割る。"
            + "**情報セル最少の側から割っても同じ行になる**——どちらの経路でも一意に決まる。");

        // ---- 表B: 唯一の観測点（特性の側・静的）
        Console.WriteLine();
        Console.WriteLine("## 表B —— 唯一の観測点（特性の側・`Presets.Compare` 61 行）");
        Console.WriteLine();
        Console.WriteLine("**その特性の保持者を含む行が 1 行しかない特性**を列挙する（指示書 §4-2）。"
            + "1 行しか無い特性を持つ行は、その特性の唯一の観測点なので (b) で外す。");
        Console.WriteLine();
        var traitRows = new Dictionary<TraitId, List<string>>();
        foreach (var b in ldCompare)
            foreach (var id in b.F.Occupied().SelectMany(o => o.Def.Traits).Distinct())
            {
                if (!traitRows.TryGetValue(id, out var l)) traitRows[id] = l = new List<string>();
                if (!l.Contains(b.Name)) l.Add(b.Name);
            }
        var soleTraits = traitRows.Where(kv => kv.Value.Count == 1)
                                  .OrderBy(kv => kv.Value[0], StringComparer.Ordinal).ToList();
        Console.WriteLine($"61 行に現れる特性 **{traitRows.Count} 種**。うち **1 行しか無いのは {soleTraits.Count} 種**。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 唯一の行 |");
        Console.WriteLine("|---|---|");
        foreach (var kv in soleTraits) Console.WriteLine($"| `{kv.Key}` | {kv.Value[0]} |");
        if (soleTraits.Count == 0) Console.WriteLine("| （該当なし） | |");
        Console.WriteLine();
        var soleRows = soleTraits.Select(kv => kv.Value[0]).Distinct().ToHashSet();
        Console.WriteLine($"**(b) で外れる行 {soleRows.Count}**: " + string.Join(" / ", soleRows));
        Console.WriteLine();
        foreach (var h in hits)
            Console.WriteLine($"- 線を満たす行 `{h.Name}`: (b) {(soleRows.Contains(h.Name) ? "**× 外れる**" : "○ 通る")}");
        Console.WriteLine();
        Console.WriteLine("> **この検査は下限でしかない。** 第109期の `刻み×澱み` が着火の唯一の観測点だったのは"
            + "「ミオを含む他の 4 行がグザ同席で条件を潰す」という**組み合わせ**の話で、特性の数え上げには出ない。"
            + "**規則の側から実測する `ledger unique` を併せて読むこと。**");

        // ---- 表C: 5枚組の一意性（§4-3）
        Console.WriteLine();
        Console.WriteLine("## 表C —— 5枚組の一意性（§4-3）");
        Console.WriteLine();
        Console.WriteLine("**席ではなく5枚組（順不同）で照合する**——席だけ違う行は「既存行の作り直し」なので採らない（第108期 §A-1）。");
        Console.WriteLine();
        var known = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var b in ldCompare) known[LdKey(b.F)] = "compare: " + b.Name;
        foreach (var b in ldCross) known.TryAdd(LdKey(b.F), "交差帯: " + b.Name);
        Console.WriteLine("| 候補 | 5枚組 | 既存と一致するか |");
        Console.WriteLine("|---|---|---|");
        string tk = LdKey(LdTomoBase());
        Console.WriteLine($"| {LdTomoName} | {tk} | {(known.TryGetValue(tk, out string? h0) ? "**× " + h0 + "**" : "**○ 一致なし**")} |");
        foreach (var d in ldCrossCands)
        {
            string k = LdKey(LdCrossWith(d));
            Console.WriteLine($"| 交差帯 `傷×被弾` の後1 に {d.Name} | {k} "
                + $"| {(known.TryGetValue(k, out string? h1) ? "**× " + h1 + "**" : "**○ 一致なし**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"（現行の `傷×被弾 (カド×ハリ×ノノ)` の5枚組は `{LdKey(ldCross[ldWoundHitIdx].F)}`。"
            + $"**ハリは `UnitCatalog.All` に居ない**: {(UnitCatalog.All.Any(u => ReferenceEquals(u, UnitCatalog.Hari)) ? "**×**" : "○")}）");

        // ---- 表D: 灯の対象（§4-4）
        Console.WriteLine();
        Console.WriteLine("## 表D —— 灯の対象がドルガに一意に決まるか（§4-4・実装から引いた速さ）");
        Console.WriteLine();
        Console.WriteLine("尾灯は「自分を除いて最も遅い味方」に灯す。**`Stoic`（支援拒否）は候補から外れる**"
            + "（`AcceptsSupport` が偽なので `ctx.Whet` が届かない）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 速さ | `Stoic` | 灯の候補 | `CanAct` を通るか（`Actions`/`Immobile`） |");
        Console.WriteLine("|---|--:|:-:|:-:|---|");
        foreach (var d in ldTomoMembers.OrderBy(d => d.Speed).ThenBy(d => d.Id, StringComparer.Ordinal))
        {
            bool stoic = d.Traits.Contains(TraitId.Stoic);
            bool self = ReferenceEquals(d, UnitCatalog.Tomo);
            bool cand = !self && !stoic;
            string act = d.Traits.Contains(TraitId.Immobile) ? "**不動**"
                       : d.Actions is { Count: > 0 } && d.Actions.All(a => a.Kind != ActionKind.Attack) ? "`Actions = [Skill]`"
                       : "通常攻撃";
            Console.WriteLine($"| {d.Name}{(self ? "（本人）" : "")} | {d.Speed} | {(stoic ? "○" : "")} "
                + $"| {(cand ? "○" : "×")} | {act} |");
        }
        var lamp = ldTomoMembers.Where(d => !ReferenceEquals(d, UnitCatalog.Tomo) && !d.Traits.Contains(TraitId.Stoic))
                                .OrderBy(d => d.Speed).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
        Console.WriteLine();
        Console.WriteLine($"**候補のうち最も遅いのは {lamp[0].Name}（速{lamp[0].Speed}）**"
            + $"——2番目は {lamp[1].Name}（速{lamp[1].Speed}）で、"
            + $"{(lamp[0].Speed < lamp[1].Speed ? "**同速が無いので一意**" : "**同速がある。一意ではない**")}。");

        // ---- 表E: 軸の数（§1-4 の予測4）
        Console.WriteLine();
        Console.WriteLine("## 表E —— トモの行が持つ軸（`TraitKeyMap`）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 特性 | キー |");
        Console.WriteLine("|---|---|---|");
        var tomoKeys = new HashSet<int>();
        foreach (var d in ldTomoMembers)
        {
            var keys = d.Traits.SelectMany(id => TraitKeyMap.TraitKeys.TryGetValue(id, out int[]? k) ? k : Array.Empty<int>())
                               .Distinct().OrderBy(x => x).ToArray();
            foreach (int k in keys) tomoKeys.Add(k);
            Console.WriteLine($"| {d.Name} | {string.Join(" / ", d.Traits.Select(t => "`" + t + "`"))} "
                + $"| {(keys.Length == 0 ? "—" : string.Join(" / ", keys.Select(x => UnitTally.CarryKeys[x])))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**軸の数 {tomoKeys.Count}**: " + string.Join(" / ", tomoKeys.OrderBy(x => x).Select(x => UnitTally.CarryKeys[x])));
        Console.WriteLine();
        Console.WriteLine("`TraitEntryMap.Supplies[Taillight]` = "
            + string.Join(" / ", (TraitEntryMap.Supplies.TryGetValue(TraitId.Taillight, out var sup) ? sup : Array.Empty<(int, TraitEntryMap.Where)>())
                .Select(e => UnitTally.CarryKeys[e.Item1] + "（" + e.Item2 + "）"))
            + " —— **手番（`IdleTurn`）は1つも書かない。**");
        Console.WriteLine();
        Console.WriteLine("> **指示書 §0-2 の確認**: `Presets.Cross` に `強化×手番 (ガン×ドルガ×バン)` が"
            + $"{(ldCross.Any(b => b.Name.StartsWith("強化×手番", StringComparison.Ordinal)) ? "**既にある**" : "**無い**")}。"
            + "トモは新しい交差ではなく**強化軸の新しい書き手**なので、行き先は `compare` の側。");

        // ---- 表F: 不変量（§4-5・§4-6）
        Console.WriteLine();
        Console.WriteLine("## 表F —— 不変量（§4-5・§4-6）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 値 | 期待 | 判定 |");
        Console.WriteLine("|---|--:|--:|:-:|");
        Console.WriteLine($"| `Presets.Compare` の行数 | {ldCompare.Length} | 61 | {(ldCompare.Length == 61 ? "○" : "**×**")} |");
        Console.WriteLine($"| `Presets.Cross` の行数 | {ldCross.Length} | 12 | {(ldCross.Length == 12 ? "○" : "**×**")} |");
        Console.WriteLine($"| `compare` のセル数 | {ldCompare.Length * ldStages.Count} | 305 | {(ldCompare.Length * ldStages.Count == 305 ? "○" : "**×**")} |");
        Console.WriteLine($"| `UnitCatalog.All` の枚数 | {UnitCatalog.All.Count} | 52 | {(UnitCatalog.All.Count == 52 ? "○" : "**×**")} |");
        Console.WriteLine($"| `Baseline.PrimaryRows` | {Baseline.PrimaryRows.Length} | 19 | {(Baseline.PrimaryRows.Length == 19 ? "○" : "**×**")} |");
        int unresolved = Baseline.PrimaryRows.Count(n => !ldCompare.Any(b => b.Name == n));
        Console.WriteLine($"| 主判定19行が `Presets.Compare` に引けない数 | {unresolved} | 0 | {(unresolved == 0 ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine("**行数が 61 のまま・セル数が 305 のままなので、`305` / `61` を決め打ちしている器具は1つも触らない**"
            + "（§4-6。実行される決め打ちは 第76期 `body` の `byAllRows.Length == 61`、"
            + "第78期 `traits` / 第80期 `pairs` の `== 305`、第73/74期 `wound` / `wcost` の予算計算の 3 箇所）。");
        return;
    }

    // ==============================================================================
    // unique —— §4-2 の実測版。**規則を1本ずつ切って、動く行を数える。**
    //
    // 特性の数え上げ（表B）は下限でしかない（第109期の `刻み×澱み` は特性では引けない）。
    // **「その規則を切ったときに動く行が 1 行だけ」なら、その行がその規則の唯一の観測点。**
    // ==============================================================================
    if (ldMode == "unique")
    {
        // 切った版。**「何もしない状態」が一意に決まる規則だけを並べる**
        // ——強度のノブ（`ScaleRule` / `ThinBladeRule` / `SeverRule.Wait` など）は
        // 「切る」が定義できないので入れない。off の中身は列に出してあるので後から検算できる。
        var ldKnobs = new (string Name, string Off, Func<Formation, Formation, int, BattleResult> Run)[]
        {
            ("`yoke`",        "Active = False",                    (p, e, s) => BattleEngine.Run(p, e, s, false, yoke: new YokeRule(YokeTrait.Cap, false))),
            ("`hush`",        "Active = False",                    (p, e, s) => BattleEngine.Run(p, e, s, false, hush: new HushRule(false))),
            ("`martyr`",      "RedirectPercent = 0",               (p, e, s) => BattleEngine.Run(p, e, s, false, martyr: new MartyrRule(0))),
            ("`expose`",      "MaxPerBattle = 0",                  (p, e, s) => BattleEngine.Run(p, e, s, false, expose: new ExposeRule(0))),
            ("`shove`",       "Penalty = 0",                       (p, e, s) => BattleEngine.Run(p, e, s, false, shove: new ShoveRule(0))),
            ("`bear`",        "ArmorPerDull = 0",                  (p, e, s) => BattleEngine.Run(p, e, s, false, bear: new BearRule(0))),
            ("`goad`",        "Boost = 0, Mark = False",           (p, e, s) => BattleEngine.Run(p, e, s, false, goad: new GoadRule(0, false))),
            ("`finisher`",    "Multiplier = 1, Consume = False",   (p, e, s) => BattleEngine.Run(p, e, s, false, finisher: new FinisherRule(1, false))),
            ("`favor`",       "Gain = 0, Loss = 0",                (p, e, s) => BattleEngine.Run(p, e, s, false, favor: new FavorRule(0, 0))),
            ("`blaze`",       "Targets = None",                    (p, e, s) => BattleEngine.Run(p, e, s, false, blaze: new BlazeRule(BlazeTargets.None))),
            ("`suture`",      "対照:Side = Foe",                        (p, e, s) => BattleEngine.Run(p, e, s, false, suture: new SutureRule(SutureSide.Foe))),
            ("`spillWound`",  "Enabled = False",                   (p, e, s) => BattleEngine.Run(p, e, s, false, spillWound: new SpillWoundRule(false))),
            ("`mend`",        "対照:Side = Plain",                      (p, e, s) => BattleEngine.Run(p, e, s, false, mend: new MendRule(MendSide.Plain))),
            ("`woundIgnite`", "Enabled = False",                   (p, e, s) => BattleEngine.Run(p, e, s, false, woundIgnite: new IgniteRule(false))),
            ("`gather`",      "Enabled = False",                   (p, e, s) => BattleEngine.Run(p, e, s, false, gather: new GatherRule(false))),
            ("`soak`",        "Poison/Burn = False, DullPerKind = 0", (p, e, s) => BattleEngine.Run(p, e, s, false, soak: new SoakRule(false, false, 0))),
            ("`betray`",      "Enabled = False",                   (p, e, s) => BattleEngine.Run(p, e, s, false, betray: new BetrayRule(false))),
            ("`encore`",      "Enabled = False",                   (p, e, s) => BattleEngine.Run(p, e, s, false, encore: new EncoreRule(false))),
            ("`loose`",       "Shove = False",                     (p, e, s) => BattleEngine.Run(p, e, s, false, loose: new LooseRule(false))),
            ("`menderCost`",  "対照:Percent = 100（第106期より前）",     (p, e, s) => BattleEngine.Run(p, e, s, false, menderCost: new MenderCostRule(100))),
            ("`colossus`",    "Regurgitate/Refund = False",        (p, e, s) => BattleEngine.Run(p, e, s, false, colossus: new ColossusRule(ColossusRule.Default.Percent, ColossusRule.Default.DamagePerGain, false, false, ColossusRule.Default.SlumberThreshold, false, ColossusRule.Default.RefundPercent))),
            ("`taillight`",   "対照:Mode = OwnTurn（V0）",              (p, e, s) => BattleEngine.Run(p, e, s, false, taillight: new TaillightRule(YieldMode.OwnTurn))),
        };

        Console.WriteLine("# 第111期 §4-2（実測）—— 規則を1本ずつ切って、動く行を数える");
        Console.WriteLine();
        Console.WriteLine($"`compare` {ldCompare.Length} 行 × {ldStages.Count} 波 × seed 0..{LdSeeds - 1}。"
            + "**「切った版と既定が1セルでも違う行」を数える。動く行が 1 行だけなら、その行がその規則の唯一の観測点。**");
        Console.WriteLine();
        Console.WriteLine("`種別` の **off** は「その規則が何もしない版」、**対照** は「採用前の版」"
            + "（`mend` / `suture` は側を戻す・`menderCost` は代金を戻す・`taillight` は V0）。**どちらも (b) には同じに効く**"
            + "——問いは「その規則の判断がどの行に届いているか」だから。");
        Console.WriteLine();
        Console.WriteLine("強度のノブ（`scale` / `thinBlade` / `sever` / `divert` / `relay` / `funnel` など）は"
            + "**「切る」が一意に定義できない**ので入れていない。**`rage` も入れていない**"
            + "——`RageMode.Amount` では `Gain` が読まれないので、`Gain = 0` は「切った版」にならない（初版はここを取り違えた）。");
        Console.WriteLine();

        var baseCells = new double[ldCompare.Length][];
        Parallel.For(0, ldCompare.Length, b => baseCells[b] = LdCells(ldCompare[b].F, 0, LdSeeds));

        Console.WriteLine("| 規則 | 種別 | 当てた版 | 動いた行 | 唯一の観測点 | 行名（10 行まで） |");
        Console.WriteLine("|---|:-:|---|--:|:-:|---|");
        var soleBy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var k in ldKnobs)
        {
            var moved = new bool[ldCompare.Length];
            Parallel.For(0, ldCompare.Length, b =>
            {
                for (int w = 0; w < ldStages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < LdSeeds; seed++)
                        if (k.Run(ldCompare[b].F, ldStages[w].Enemy, seed).PlayerWon) wins++;
                    if (Math.Abs(wins * 100.0 / LdSeeds - baseCells[b][w]) > 1e-9) { moved[b] = true; return; }
                }
            });
            var names = Enumerable.Range(0, ldCompare.Length).Where(b => moved[b]).Select(b => ldCompare[b].Name).ToList();
            if (names.Count == 1)
            {
                if (!soleBy.TryGetValue(names[0], out var l)) soleBy[names[0]] = l = new List<string>();
                l.Add(k.Name);
            }
            Console.WriteLine($"| {k.Name} | {(k.Off.StartsWith("対照:") ? "対照" : "off")} "
                + $"| {k.Off.Replace("対照:", "")} | {names.Count} | {(names.Count == 1 ? "**○**" : "")} "
                + $"| {(names.Count == 0 ? "（なし）" : string.Join(" / ", names.Take(10)) + (names.Count > 10 ? " …" : ""))} |");
            Console.Error.WriteLine("[ledger unique] " + k.Name + " done: " + names.Count);
        }

        Console.WriteLine();
        Console.WriteLine("## 唯一の観測点になっている行（(b) で外す行）");
        Console.WriteLine();
        if (soleBy.Count == 0) Console.WriteLine("（該当なし）");
        foreach (var kv in soleBy.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            Console.WriteLine($"- **{kv.Key}** —— {string.Join(" / ", kv.Value)} の唯一の観測点");
        Console.WriteLine();
        Console.WriteLine("> **動いた行が 0 の規則は「既定で何もしない」か「盤面に保持者が居ない」**"
            + "——どちらも (b) には効かない。");
        return;
    }

    // ==============================================================================
    // seat —— §1-3。**「勝つ席」ではなく「測れる席」**（第59期・第89期の作法）。
    // ==============================================================================
    if (ldMode == "seat")
    {
        Console.WriteLine("# 第111期 §1-3 —— トモの行の席");
        Console.WriteLine();
        Console.WriteLine($"5枚組は第110期の台のまま（**§1-1 で固定してある。動かさない**）。"
            + $"120 通りを粗探索 seed 0..{LdScan - 1} → 追試 seed 0..{LdSeeds - 1} で測り、"
            + $"別 seed 帯（{LdCfBase}..{LdCfBase + LdCfSeeds - 1}）を安定の確認に併記する。");
        Console.WriteLine();
        Console.WriteLine("採る席は**3つの条件の積**（第107期 (S1) の器具の写し）: "
            + "(a) 情報セル（`0 < x < 100`・第2〜5波）を **2 以上**に保つ ／ "
            + "(b) **狙**（ガルドが前列） ／ (c) 出発点との差が **" + LdLine.ToString("F1") + "pt 以上**。");
        Console.WriteLine();
        Console.WriteLine("`巻` 列は**ボルグの隣接にドルガかトモが居るか**（第109期の台の作法。"
            + "居ない席を優先する——ただし**判定には使わない。列に出すだけ**）。");

        var perms = new List<Formation>();
        foreach (int[] assign in SlotAssignments(ldTomoMembers.Length))
        {
            var f = new Formation();
            for (int m = 0; m < ldTomoMembers.Length; m++) f[assign[m]] = ldTomoMembers[m];
            perms.Add(f);
        }

        var scan = new int[perms.Count];
        Parallel.For(0, perms.Count, i =>
        {
            int wins = 0;
            foreach (EnemyCatalog.Stage st in ldStages)
                for (int seed = 0; seed < LdScan; seed++)
                    if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
            scan[i] = wins;
        });
        var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
        var pool = order.Take(20)
            .Concat(order.Where(i => LdIntent(perms[i])).Take(10))
            .Append(order.First(i => SameFormation(perms[i], LdTomoBase())))
            .Distinct().ToList();

        var cellsA = new double[pool.Count][];
        var cellsB = new double[pool.Count][];
        Parallel.For(0, pool.Count, k => cellsA[k] = LdCells(perms[pool[k]], 0, LdSeeds));
        Parallel.For(0, pool.Count, k => cellsB[k] = LdCells(perms[pool[k]], LdCfBase, LdCfSeeds));

        // 平均は**第2〜5波**で取る（規約 (G10)。第一波は教習波）。
        static double LdAvg25(double[] c) => c.Skip(1).Average();
        bool Splash(Formation f)
        {
            int borg = -1, dolga = -1, tomo = -1;
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
            {
                if (ReferenceEquals(f[i], UnitCatalog.Borg)) borg = i;
                if (ReferenceEquals(f[i], UnitCatalog.Dolga)) dolga = i;
                if (ReferenceEquals(f[i], UnitCatalog.Tomo)) tomo = i;
            }
            return borg >= 0 && ((dolga >= 0 && FormationRules.AreAdjacent(borg, dolga))
                              || (tomo >= 0 && FormationRules.AreAdjacent(borg, tomo)));
        }

        var ranked = Enumerable.Range(0, pool.Count).OrderByDescending(k => LdAvg25(cellsA[k])).ToList();
        int curK = pool.FindIndex(i => SameFormation(perms[i], LdTomoBase()));

        Console.WriteLine();
        Console.WriteLine($"## 追試（seed 0..{LdSeeds - 1}）—— 候補 {pool.Count} 通り");
        Console.WriteLine();
        Console.WriteLine("| 追順 | 粗順 | 狙 | 巻 | 前1/前3 | 中央 | 後1/後3 | 平均(2〜5波) | 情報セル |"
            + string.Concat(ldStages.Select((_, i) => $" 第{i + 1}波 |")) + $" 平均({LdCfBase}..) | 情報セル({LdCfBase}..) |");
        Console.WriteLine("|--:|--:|:-:|:-:|---|---|---|--:|--:|" + string.Concat(ldStages.Select(_ => "---:|")) + "---:|--:|");
        for (int r = 0; r < ranked.Count; r++)
        {
            int k = ranked[r];
            Formation f = perms[pool[k]];
            Console.WriteLine($"| {r + 1}{(k == curK ? "★出発点" : "")} | {order.IndexOf(pool[k]) + 1} "
                + $"| {(LdIntent(f) ? "○" : "×")} | {(Splash(f) ? "当" : "—")} | {LdSeats(f)} "
                + $"| {LdAvg25(cellsA[k]):F1}% | {LdInfo(cellsA[k])} |"
                + string.Concat(cellsA[k].Select(c => $" {c:F1}% |"))
                + $" {LdAvg25(cellsB[k]):F1}% | {LdInfo(cellsB[k])} |");
        }

        int pick = -1, free = -1;
        foreach (int k in ranked) { if (free < 0 && LdInfo(cellsA[k]) >= 2) free = k; }
        foreach (int k in ranked) { if (LdInfo(cellsA[k]) >= 2 && LdIntent(perms[pool[k]])) { pick = k; break; } }

        Console.WriteLine();
        Console.WriteLine($"**出発点（第110期の席）**: 追順 **{ranked.IndexOf(curK) + 1} 位** / 粗順 {order.IndexOf(pool[curK]) + 1}、"
            + $"平均(2〜5波) **{LdAvg25(cellsA[curK]):F1}%**、情報セル **{LdInfo(cellsA[curK])}**"
            + $"（{LdCfBase}.. 帯では {LdAvg25(cellsB[curK]):F1}% / 情報セル {LdInfo(cellsB[curK])}）。");
        Console.WriteLine();
        if (free >= 0 && free != pick)
            Console.WriteLine($"（参考）**狙を外した**最上位は 追順 **{ranked.IndexOf(free) + 1} 位**"
                + $"（{LdAvg25(cellsA[free]):F1}% / 情報セル {LdInfo(cellsA[free])}）。**採らない。**");
        if (pick < 0)
            Console.WriteLine("**判定: 出発点を据え置き** —— 狙を満たし情報セルを 2 以上に保つ席が1つも無い。");
        else if (pick == curK)
            Console.WriteLine("**判定: 出発点を据え置き** —— 狙を満たし情報セルを 2 以上に保つ最上位が出発点そのもの。");
        else
        {
            double gain = LdAvg25(cellsA[pick]) - LdAvg25(cellsA[curK]);
            double gainB = LdAvg25(cellsB[pick]) - LdAvg25(cellsB[curK]);
            Formation f = perms[pool[pick]];
            Console.WriteLine($"狙を満たし情報セルを 2 以上に保つ最上位は 追順 **{ranked.IndexOf(pick) + 1} 位**"
                + $"（{LdAvg25(cellsA[pick]):F1}% / 情報セル {LdInfo(cellsA[pick])}）で、出発点との差は "
                + $"**{gain:+0.0;-0.0}pt**（{LdCfBase}.. 帯では {gainB:+0.0;-0.0}pt）。");
            Console.WriteLine();
            Console.WriteLine($"**判定: {(gain >= LdLine ? "差し替え" : "据え置き")}** —— 閾値 {LdLine:F1}pt に対して {gain:+0.0;-0.0}pt。");
            if (gain >= LdLine)
                Console.WriteLine($"採る配置: `front1: {LdN(f[0])}, front3: {LdN(f[1])}, center: {LdN(f[2])}, "
                    + $"back1: {LdN(f[3])}, back3: {LdN(f[4])}`");
        }
        return;
    }

    // ==============================================================================
    // drop —— 落とす行が「なぜ測らなくなったか」を帯ごとに出す。**指示書には無い。足した理由は §1-2 の
    // 判定の根拠が「情報セル 1」だから**——その 1 がいつ・どの帯で決まったのかを併記しないと、
    // 「たまたま seed の揺れで 1 に見えた」と区別が付かない。
    // ==============================================================================
    if (ldMode == "drop")
    {
        // 第89期 (P2) が動かす**前**の席（`git show aa42e6b^` の `Presets.cs` から引いた）。
        var ldDropVers = new (string Tag, Formation F)[]
        {
            ("第89期より前", Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Hota,
                                             center: UnitCatalog.Golm, back1: UnitCatalog.Rica, back3: UnitCatalog.Vel)),
            ("現行（第89期 (P2)）", Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Golm,
                                             center: UnitCatalog.Rica, back1: UnitCatalog.Hota, back3: UnitCatalog.Vel)),
        };

        Console.WriteLine("# 第111期 —— 落とす行（`死軸×ホタ (ゾト×熾)`）が測らなくなった経緯");
        Console.WriteLine();
        Console.WriteLine($"帯A = seed 0..{LdSeeds - 1}（**`compare` が測る帯。`docs/balance.md` はこれ**）／"
            + $"帯B = seed {LdCfBase}..{LdCfBase + LdCfSeeds - 1}（**第46期の追試の帯**）。");
        Console.WriteLine();
        Console.WriteLine("| 席 | 帯 | 前1/前3 | 中央 | 後1/後3 | 平均(2〜5波) | **情報セル** |"
            + string.Concat(ldStages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|---|:-:|---|---|---|--:|--:|" + string.Concat(ldStages.Select(_ => "---:|")));
        foreach (var v in ldDropVers)
            foreach (var (tag, from, n) in new[] { ("A", 0, LdSeeds), ("B", LdCfBase, LdCfSeeds) })
            {
                double[] c = LdCells(v.F, from, n);
                Console.WriteLine($"| {v.Tag} | {tag} | {LdSeats(v.F)} | {c.Skip(1).Average():F1}% "
                    + $"| **{LdInfo(c)}** |" + string.Concat(c.Select(x => $" {x:F1}% |")));
            }
        Console.WriteLine();
        Console.WriteLine("> 第89期 (P2) は**帯B で情報セル 2** を確かめて席を採ったが、"
            + "**`compare` が測る帯A では 1** になる。`docs/balance.md` に載る値は帯A なので、"
            + "**この行はそのとき以来「勝率表の上では 1 セルしか情報を持たない行」だった。**");
        return;
    }

    // ==============================================================================
    // cross —— §2。交差帯 `傷×被弾` のハリの席に入れる駒を測る。**席は現行を据え置く**（後1）。
    // ==============================================================================
    if (ldMode == "cross")
    {
        Console.WriteLine("# 第111期 §2 —— 交差帯 `傷×被弾` のハリの席");
        Console.WriteLine();
        Console.WriteLine("**行が測る交差（傷 × 被弾）は変えない。ハリの席だけを `All` に居る駒に差し替える。**"
            + "他の 11 行は1文字も触らない。**席は現行を据え置く**（後1。交差帯の席の測り直しは第92期 `cross quality` の作法に従い、この期はやらない）。");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{LdSeeds - 1}（`compare` と同じ帯）。");
        Console.WriteLine();
        Console.WriteLine("| 後1 | `All` | 5枚組の一意性 | 平均(2〜5波) | 情報セル |"
            + string.Concat(ldStages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|---|:-:|---|--:|--:|" + string.Concat(ldStages.Select(_ => "---:|")));

        var known = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var b in ldCompare) known[LdKey(b.F)] = "compare: " + b.Name;
        for (int i = 0; i < ldCross.Length; i++) if (i != ldWoundHitIdx) known.TryAdd(LdKey(ldCross[i].F), "交差帯: " + ldCross[i].Name);

        foreach (var d in new[] { UnitCatalog.Hari }.Concat(ldCrossCands))
        {
            Formation f = LdCrossWith(d);
            double[] c = LdCells(f, 0, LdSeeds);
            string k = LdKey(f);
            Console.WriteLine($"| {d.Name}{(ReferenceEquals(d, UnitCatalog.Hari) ? "（現行）" : "")} "
                + $"| {(UnitCatalog.All.Any(u => ReferenceEquals(u, d)) ? "○" : "**×**")} "
                + $"| {(known.TryGetValue(k, out string? h) ? "**× " + h + "**" : "○ 一致なし")} "
                + $"| {c.Skip(1).Average():F1}% | {LdInfo(c)} |" + string.Concat(c.Select(x => $" {x:F1}% |")));
        }
        Console.WriteLine();
        Console.WriteLine("**他の 11 行が ±0.0 であること**（交差帯は行ごとに独立に測るので原理的にそうなる。実測で確かめる）:");
        Console.WriteLine();
        Console.WriteLine("| 行 |" + string.Concat(ldStages.Select((_, i) => $" 第{i + 1}波 |")));
        Console.WriteLine("|---|" + string.Concat(ldStages.Select(_ => "---:|")));
        for (int i = 0; i < ldCross.Length; i++)
        {
            if (i == ldWoundHitIdx) continue;
            double[] c = LdCells(ldCross[i].F, 0, LdSeeds);
            Console.WriteLine($"| {ldCross[i].Name} |" + string.Concat(c.Select(x => $" {x:F1}% |")));
        }
        return;
    }

    // ==============================================================================
    // check —— §5 の判定と自己検査。
    // ==============================================================================
    if (ldMode == "check")
    {
        Console.WriteLine("# 第111期 §5 —— 判定と自己検査");
        Console.WriteLine();

        var bal = LdBalance();
        var now = new double[ldCompare.Length][];
        Parallel.For(0, ldCompare.Length, b => now[b] = LdCells(ldCompare[b].F, 0, LdSeeds));

        // Q3: 残る 60 行が ±0.0（差し替えた1行だけが `docs/balance.md` に無いか値が違う）
        var changed = new List<string>();
        var absent = new List<string>();
        for (int b = 0; b < ldCompare.Length; b++)
        {
            if (!bal.TryGetValue(ldCompare[b].Name, out double[]? v)) { absent.Add(ldCompare[b].Name); continue; }
            for (int w = 0; w < ldStages.Count; w++)
                if (Math.Abs(v[w] - now[b][w]) > 1e-9) { changed.Add(ldCompare[b].Name + " 第" + (w + 1) + "波"); break; }
        }
        var gone = bal.Keys.Where(n => !ldCompare.Any(b => b.Name == n)).ToList();

        var tomoRow = ldCompare.FirstOrDefault(b => b.Name == LdTomoName);
        int tomoIdx = Array.FindIndex(ldCompare, b => b.Name == LdTomoName);
        int tomoInfo = tomoIdx >= 0 ? LdInfo(now[tomoIdx]) : -1;

        double fifthPrim = 0; int nPrim = 0;
        double[] waveAll = new double[ldStages.Count];
        double[] wavePrim = new double[ldStages.Count];
        for (int b = 0; b < ldCompare.Length; b++)
        {
            for (int w = 0; w < ldStages.Count; w++) waveAll[w] += now[b][w];
            if (!ldPrim.Contains(ldCompare[b].Name)) continue;
            nPrim++; fifthPrim += now[b][ldStages.Count - 1];
            for (int w = 0; w < ldStages.Count; w++) wavePrim[w] += now[b][w];
        }
        double fifth = fifthPrim / Math.Max(1, nPrim);

        Console.WriteLine("| | 内容 | 実測 | 線 | 判定 |");
        Console.WriteLine("|---|---|---|---|:-:|");
        Console.WriteLine($"| **Q1** | トモの行の情報セル（第2〜5波） | {(tomoIdx < 0 ? "**行が無い**" : tomoInfo.ToString())} "
            + $"| 2 以上 | {(tomoInfo >= 2 ? "**○**" : "**×**")} |");
        Console.WriteLine($"| **Q3** | `docs/balance.md` と値が違う行 | {changed.Count} 行"
            + $"（新しい行 {absent.Count} / 消えた行 {gone.Count}） | 差し替えた1行だけ "
            + $"| {(changed.Count == 0 && absent.Count <= 1 && gone.Count <= 1 ? "**○**" : "**×**")} |");
        Console.WriteLine($"| **Q4** | 主判定{nPrim}行の第五波平均 | **{fifth:F1}%** "
            + $"| 歯止め {Baseline.PrimaryFifthFloor:F1}% | {(fifth >= Baseline.PrimaryFifthFloor ? "**○**" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine($"**Q3 の内訳**——値が違う行: {(changed.Count == 0 ? "（なし）" : string.Join(" / ", changed))} ／ "
            + $"`docs/balance.md` に無い行: {(absent.Count == 0 ? "（なし）" : string.Join(" / ", absent))} ／ "
            + $"`docs/balance.md` にしか無い行: {(gone.Count == 0 ? "（なし）" : string.Join(" / ", gone))}");
        Console.WriteLine();
        Console.WriteLine("**`Baseline`（規約：主判定19行／全61行の両方を併記する）**");
        Console.WriteLine();
        Console.WriteLine("| 分母 |" + string.Concat(ldStages.Select((_, i) => $" 第{i + 1}波 |")) + " 歯止めとの余裕 |");
        Console.WriteLine("|---|" + string.Concat(ldStages.Select(_ => "---:|")) + "---:|");
        Console.WriteLine($"| 主判定{nPrim}行 |" + string.Concat(wavePrim.Select(x => $" {x / Math.Max(1, nPrim):F1} |"))
            + $" **{fifth - Baseline.PrimaryFifthFloor:+0.0;-0.0}pt** |");
        Console.WriteLine($"| 全{ldCompare.Length}行 |" + string.Concat(waveAll.Select(x => $" {x / ldCompare.Length:F1} |")) + " — |");

        // 拒否権（規約 (G9)）: 第五波 95% 超の行が新たに出たか
        var ceil = new List<string>();
        for (int b = 0; b < ldCompare.Length; b++)
        {
            double v5 = now[b][ldStages.Count - 1];
            bool wasCeil = bal.TryGetValue(ldCompare[b].Name, out double[]? old) && old[ldStages.Count - 1] > 95.0;
            if (v5 > 95.0 && !wasCeil) ceil.Add($"{ldCompare[b].Name}（{v5:F1}%）");
        }
        Console.WriteLine();
        Console.WriteLine($"**拒否権（規約 (G9)・「注意」）**: 第五波 95% 超が新たに出た行 **{ceil.Count} 行**"
            + $"{(ceil.Count == 0 ? "" : ": " + string.Join(" / ", ceil))}");

        // --- 自己検査
        Console.WriteLine();
        Console.WriteLine("## 自己検査");
        Console.WriteLine();
        Console.WriteLine("| | 項目 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine($"| (a) | `UnitCatalog.All` の枚数 | {UnitCatalog.All.Count} | {(UnitCatalog.All.Count == 52 ? "○" : "**×**")} |");
        Console.WriteLine($"| (b) | `Presets.Compare` / `Presets.Cross` の行数 | {ldCompare.Length} / {ldCross.Length} "
            + $"| {(ldCompare.Length == 61 && ldCross.Length == 12 ? "○" : "**×**")} |");
        int unres = Baseline.PrimaryRows.Count(n => !ldCompare.Any(b => b.Name == n));
        Console.WriteLine($"| (d) | 主判定19行が `Presets.Compare` に引ける | 引けない {unres} 行 | {(unres == 0 ? "○" : "**×**")} |");
        bool hariGone = !UnitCatalog.All.Any(u => ReferenceEquals(u, UnitCatalog.Hari));
        bool hariInCross = ldCross.Any(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hari)));
        bool hariInCompare = ldCompare.Any(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hari)));
        Console.WriteLine($"| (e) | ハリが `All` に居ない / `Cross` に居ない / `Compare` に居ない "
            + $"| {(hariGone ? "○" : "×")} / {(hariInCross ? "**×**" : "○")} / {(hariInCompare ? "**×**" : "○")} "
            + $"| {(hariGone && !hariInCross && !hariInCompare ? "○" : "**×**")} |");
        bool tomoInCompare = ldCompare.Any(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Tomo)));
        Console.WriteLine($"| (Q6) | トモが `Presets.Compare` に居る（`derive scan` の供給が 0 でなくなる） "
            + $"| {(tomoInCompare ? "○" : "**×**")} | {(tomoInCompare ? "○" : "**×**")} |");

        // (c) トモが最遅だった回数・灯の対象
        if (tomoRow.F is not null)
        {
            Console.WriteLine();
            Console.WriteLine("### (c) トモの行——灯の対象");
            Console.WriteLine();
            var cands = tomoRow.F.Occupied().Select(o => o.Def)
                .Where(d => !ReferenceEquals(d, UnitCatalog.Tomo) && !d.Traits.Contains(TraitId.Stoic))
                .OrderBy(d => d.Speed).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
            var slower = tomoRow.F.Occupied().Select(o => o.Def)
                .Where(d => !ReferenceEquals(d, UnitCatalog.Tomo) && d.Speed <= UnitCatalog.Tomo.Speed).ToList();
            Console.WriteLine($"- トモ（速{UnitCatalog.Tomo.Speed}）より遅いか同速の味方: "
                + $"**{slower.Count} 枚**{(slower.Count == 0 ? "（＝トモが最遅。100%）" : ": " + string.Join(" / ", slower.Select(d => d.Name)))}");
            Console.WriteLine($"- 灯の候補（`Stoic` を除く）のうち最も遅い: **{cands[0].Name}（速{cands[0].Speed}）** "
                + $"——2番目は {cands[1].Name}（速{cands[1].Speed}）で、"
                + $"{(cands[0].Speed < cands[1].Speed ? "**同速が無いので、ドルガ生存中は 100% ドルガ**" : "**同速がある**")}");
        }
        return;
    }

    Console.WriteLine("mode: phase0 / unique / seat / cross / check");
    return;
}
}
