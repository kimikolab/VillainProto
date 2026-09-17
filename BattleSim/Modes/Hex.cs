using BattleCore;
using static Common;

// =====================================================================================
// hex モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "hex")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 hex
// =====================================================================================

static class HexDiag
{
// =====================================================================================
// hex モード（第96期）—— 呪い（点で刺すと、繋がった駒にも届く）
//
// 泥人形ムドにダメージを与えた駒に呪いが1つ付き、**単体攻撃**のダメージが呪い持ちに入ると
// **同じ陣営の他の呪い持ち全員**に 50% が届く。**単体攻撃を条件付きの範囲攻撃に変える**のが狙い。
//
//     dotnet run --project BattleSim -c Release 0 hex rename      # 表A（(R1) の改名が ±0.0）
//     dotnet run --project BattleSim -c Release 0 hex phase0      # 表B・F（門・紙・事実・書き込まない駒）
//     dotnet run --project BattleSim -c Release 0 hex run <v> [skip] [take]   # 2×2 を TSV へ
//     dotnet run --project BattleSim -c Release 0 hex tables <v0> <v1>        # 表C（主判定）
//     dotnet run --project BattleSim -c Release 0 hex veto        # 表D（拒否権・(G2) の分解・交差帯）
//     dotnet run --project BattleSim -c Release 0 hex fire        # 表E（分布・内訳・味方側の損・ネル）
//     dotnet run --project BattleSim -c Release 0 hex check       # 表G（自己検査）
//
// **既存の診断は書き換えていない**——(R1) の改名が強制する識別子の置き換えだけが `curse` に入る。
public static void Run(string[] args, int stageIndex)
{
    string hxMode = args.Length > 2 ? args[2] : "";
    var hxStages = EnemyCatalog.Stages;
    var hxCompare = CompareBuilds();
    var hxCross = CrossBuilds();
    var hxRoster = UnitCatalog.All.ToArray();
    int hxRN = hxRoster.Length;
    var hxIdx = new Dictionary<string, int>();
    for (int u = 0; u < hxRN; u++) hxIdx[hxRoster[u].Id] = u;
    const double HxEps = 1e-9;
    var hxInv = System.Globalization.CultureInfo.InvariantCulture;

    string? hxRoot = Directory.GetCurrentDirectory();
    while (hxRoot != null && !File.Exists(Path.Combine(hxRoot, "docs", "balance.md")))
        hxRoot = Path.GetDirectoryName(hxRoot);

    // 版。**v = 2 は「印は書くが共有量 0」**——`ApplyDamage` が `amount <= 0` で即返るので
    // **盤面は V0 と完全に同一のまま門の計数だけが立つ**（§1-1 の「版に依らない計数」）。
    CurseRule HxVer(int v) => v switch
    {
        0 => new CurseRule(false, 50),
        1 => new CurseRule(true, 50),
        _ => new CurseRule(true, 0)
    };
    string HxVName(int v) => v == 0 ? "V0（現行・共有しない）" : v == 1 ? "V1（呪い）" : "V2（印だけ・共有 0）";
    BattleResult HxRun(Formation f, Formation e, int seed, int v, bool verbose = false)
        => BattleEngine.Run(f, e, seed, verbose: verbose, curse: HxVer(v));

    const string HxAId = "mudo";

    // 意図した相手（§3-1）。**測る前に規則で固定する**——
    // 「常時の攻撃型が単体（`ModifyPattern` を持つ特性が1つも無い）かつ攻撃力 10 以上」。
    // 規則で選ぶので、指示書が名指しした駒（ドルガ・トメ・ナタ・ハネ・ザン）を機械が再現するかが検算になる。
    bool HxChangesPattern(UnitDef d) => d.Traits.Any(t =>
        TraitHookMap.TraitHooks.TryGetValue(t, out string[]? h) && h.Contains("ModifyPattern"));
    var hxIntended = hxRoster
        .Where(d => d.Id != HxAId && d.Pattern == AttackPattern.Single
                    && d.Attack >= 10 && !HxChangesPattern(d))
        .Select(d => d.Id).ToArray();

    // ---------------------------------------------------------------------------------
    // 表A —— (R1) の改名（§0-1）
    // ---------------------------------------------------------------------------------
    if (hxMode == "rename")
    {
        Console.WriteLine("# 第96期 表A —— (R1) `CurseRule` を `SoakRule` に畳んだ");
        Console.WriteLine();
        Console.WriteLine($"- `SoakRule.Default` = `{SoakRule.Default}`（第95期の採用状態をそのまま再現する）");
        Console.WriteLine($"- `CurseRule.Default` = `{CurseRule.Default}`（第96期の呪い。**測定中は共有しない**）");
        Console.WriteLine($"- なまりが読む汚れ: `SoakRule.Kinds` = {SoakRule.Kinds.Length} 本"
                          + $"（{string.Join("・", SoakRule.Kinds.Select(StatusKeys.LabelOf))}）");
        Console.WriteLine();

        int diff = -1;
        var cells = new List<string>();
        foreach (var b in hxCompare)
        {
            var row = new List<string>();
            foreach (var st in hxStages)
            {
                int w = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                row.Add($"{w * 100.0 / 200:F1}%");
            }
            cells.Add($"| {b.Name} | {string.Join(" | ", row)} |");
        }
        if (hxRoot is not null)
        {
            var doc = File.ReadAllLines(Path.Combine(hxRoot, "docs", "balance.md"))
                          .Where(l => l.StartsWith("| ") && l.Contains('%')).ToArray();
            diff = Math.Abs(doc.Length - cells.Count);
            for (int i = 0; i < Math.Min(doc.Length, cells.Count); i++)
                if (doc[i].Trim() != cells[i].Trim()) diff++;
        }
        Console.WriteLine($"- **`compare` {hxCompare.Length} 行 × {hxStages.Count} 波 ＝ "
                          + $"{hxCompare.Length * hxStages.Count} セルと `docs/balance.md`: ずれ {diff} 行**"
                          + (diff == 0 ? "（○）" : "（×）"));

        int xdiff = -1;
        string cr = hxRoot is not null ? Path.Combine(hxRoot, "docs", "crossing.md") : "";
        if (File.Exists(cr))
        {
            string txt = File.ReadAllText(cr);
            xdiff = 0;
            foreach (var b in hxCross)
            {
                var row = new List<string>();
                foreach (var st in hxStages)
                {
                    int w = 0;
                    for (int seed = 0; seed < 200; seed++)
                        if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                    row.Add($"{w * 100.0 / 200:F1}%");
                }
                if (!txt.Contains($"| {b.Name} | {string.Join(" | ", row)} |")) xdiff++;
            }
        }
        Console.WriteLine($"- **交差帯 {hxCross.Length} 行 × {hxStages.Count} 波と `docs/crossing.md`: "
                          + $"ずれ {xdiff} 行**" + (xdiff == 0 ? "（○）" : "（×）"));
        Console.WriteLine();
        Console.WriteLine("**この表は (R1) の改名と、祟りの札をムドに足したことの両方を同時に検算している**"
                          + "——`CurseRule.Default` が共有しないので、札を1枚足しても盤面は1ビットも動かない。");
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表B・F —— Phase 0（門・紙・事実・書き込まない駒）
    // ---------------------------------------------------------------------------------
    if (hxMode == "phase0")
    {
        const int HxP0Seeds = 200;
        var hxMudRows = hxCompare.Where(b => b.F.Occupied().Any(x => x.Item2.Id == HxAId)).ToArray();

        Console.WriteLine("# 第96期 表B —— Phase 0（門・紙・事実）");
        Console.WriteLine();
        Console.WriteLine($"台は `compare` {hxCompare.Length} 行のうち**ムドを含む {hxMudRows.Length} 行**"
                          + $" × {hxStages.Count} 波 × seed 0..{HxP0Seeds - 1}。"
                          + "**版は V2（印は書くが共有量 0）**——盤面は V0 と完全に同一のまま計数だけが立つ。");
        Console.WriteLine();

        Console.WriteLine("## B-1. 門（§1-1）—— 鎖が繋がっているか");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | ムド被弾/戦 | 敵から | 味方の刃 | 出どころ無し | 呪い/戦（味/敵） "
                          + "| 2体目のT（味/敵） | 2体以上のT率（味/敵） | 単体が呪い持ちへ/戦 | 空振り/戦 | 決着T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        double g1 = 0, g2p = 0, g2e = 0, g3 = 0; int gn = 0;
        var hitAlly = new Dictionary<string, int>();
        var hitFoe = new Dictionary<string, int>();
        double paper = 0, baseSum = 0, tgtSum = 0, dmgTotal = 0, stoic = 0;
        foreach (var (name, f) in hxMudRows)
            for (int wi = 0; wi < hxStages.Count; wi++)
            {
                double hits = 0, foe = 0, ally = 0, nos = 0, mp = 0, me = 0;
                double pt = 0, ptn = 0, et = 0, etn = 0, pairP = 0, pairE = 0, turns = 0;
                double sh = 0, dry = 0, dt = 0;
                for (int seed = 0; seed < HxP0Seeds; seed++)
                {
                    var r = HxRun(f, hxStages[wi].Enemy, seed, 2);
                    hits += r.HexHits; foe += r.HexHitsFromFoe; ally += r.HexHitsFromAlly; nos += r.HexHitsNoSource;
                    mp += r.HexMarksOnPlayer; me += r.HexMarksOnEnemy;
                    if (r.HexPairTurnPlayer > 0) { pt += r.HexPairTurnPlayer; ptn++; }
                    if (r.HexPairTurnEnemy > 0) { et += r.HexPairTurnEnemy; etn++; }
                    pairP += r.HexPairTurnsPlayer; pairE += r.HexPairTurnsEnemy; turns += r.HexCensusTurns;
                    sh += r.HexShareHits; dry += r.HexShareDry; dt += r.Turns;
                    paper += r.HexShareBaseTimesTargets * 0.5;
                    baseSum += r.HexShareBase; tgtSum += r.HexShareTargets; stoic += r.HexMarksOnStoic;
                    dmgTotal += r.TallyByUnit.Values.Sum(t => (double)t.DamageTaken);
                    foreach (var kv in r.HexHitByAlly) { hitAlly.TryGetValue(kv.Key, out int a); hitAlly[kv.Key] = a + kv.Value; }
                    foreach (var kv in r.HexHitByFoe) { hitFoe.TryGetValue(kv.Key, out int a); hitFoe[kv.Key] = a + kv.Value; }
                }
                double n = HxP0Seeds;
                g1 += hits / n; g2p += ptn; g2e += etn; g3 += sh / n; gn++;
                Console.WriteLine($"| {name} | {hxStages[wi].Name} | {hits / n:F2} | {foe / n:F2} | {ally / n:F2} | {nos / n:F2} "
                                  + $"| {mp / n:F2} / {me / n:F2} "
                                  + $"| {(ptn > 0 ? (pt / ptn).ToString("F2") : "—")}（{ptn * 100 / n:F0}%）"
                                  + $" / {(etn > 0 ? (et / etn).ToString("F2") : "—")}（{etn * 100 / n:F0}%） "
                                  + $"| {pairP * 100 / Math.Max(1, turns):F1}% / {pairE * 100 / Math.Max(1, turns):F1}% "
                                  + $"| {sh / n:F2} | {dry / n:F2} | {dt / n:F2} |");
            }
        Console.WriteLine();
        Console.WriteLine($"**門の判定（3つとも 0 より大きいこと）**——"
                          + $"1: ムド被弾 **{g1 / gn:F2} 回/戦**{(g1 > 0 ? "（○）" : "（×）")} ／ "
                          + $"2: 同陣営に2体以上が立った試行 **味方 {g2p * 100 / (gn * 200.0):F1}% / 敵 {g2e * 100 / (gn * 200.0):F1}%**"
                          + $"{(g2p + g2e > 0 ? "（○）" : "（×）")} ／ "
                          + $"3: 単体攻撃が呪い持ちへ **{g3 / gn:F2} 回/戦**{(g3 > 0 ? "（○）" : "（×）")}");
        Console.WriteLine();

        Console.WriteLine("## B-2. 紙のスループット（§1-2。**門ではなく出力**）");
        Console.WriteLine();
        Console.WriteLine("    共有由来の出力/戦 = Σ(元の打点 × 0.5 × 同陣営の他の呪い持ちの数)");
        Console.WriteLine();
        double battles = gn * 200.0;
        Console.WriteLine($"- 共有の発火 {g3 / gn:F2} 回/戦 ／ 共有先の延べ体数 **{tgtSum / battles:F2} 体/戦** "
                          + $"／ 元の打点の総和 {baseSum / battles:F1} /戦");
        Console.WriteLine($"- **紙 = {paper / battles:F2} 点/戦** ／ 総被ダメージ {dmgTotal / battles:F1} 点/戦 "
                          + $"＝ **{paper * 100 / Math.Max(1, dmgTotal):F2}%**");
        Console.WriteLine();
        Console.WriteLine("**分子について3つ（規約 (G7)）**: "
                          + "**線形**（元の一撃を割合で分けるので呪い体数に比例して打ち止め） ／ "
                          + "**門ではなく出力**（門は B-1） ／ "
                          + "**分母を削らない**（共有は元の一撃を減らさない）——"
                          + "第93期の「分母を削る機構では紙は上限になる」には当たらないので、**紙は下限のはず**。");
        Console.WriteLine();

        Console.WriteLine("## B-3. 事実の確定（§1-3）");
        Console.WriteLine();
        Console.WriteLine($"1. **`StatusKeys.All` は {StatusKeys.All.Length} 本**"
                          + $"（{string.Join("・", StatusKeys.All.Select(StatusKeys.LabelOf))}）。"
                          + $"呪いを足して {StatusKeys.All.Length - 1} → {StatusKeys.All.Length} 本。"
                          + $"`ScapegoatTrait.Kinds` は **{ScapegoatTrait.Kinds.Length} 本**（除外を並べる作法なので自動で増える）。"
                          + $"業は `UnitCatalog.All` に "
                          + $"{(hxRoster.Any(d => d.Traits.Contains(TraitId.Scapegoat)) ? "**居る**" : "居ない")}・"
                          + $"`EnemyCatalog.Stages` に "
                          + $"{(hxStages.Any(st => st.Enemy.Occupied().Any(x => x.Item2.Traits.Contains(TraitId.Scapegoat))) ? "**居る**" : "居ない")}"
                          + "——**どちらにも居ないので盤面は動かない。**");
        Console.WriteLine($"2. **`AttackPattern.Single` の判定は `ApplyDamage` の時点では取れなかった**ので、"
                          + "`singleHit` の札を1つ足した（既定 `false`。`PerformAttack` の単体の一振りだけが `true` を渡す）。"
                          + "**肩代わりで分割された段には引き継がない**——引き継ぐと「1ホップ」が肩代わり役の枚数だけ増える。");
        Console.WriteLine($"4. **`Stoic`（ガルド）は呪いを弾かない**——呪いは支援ではないので `AcceptsSupport` を見ない。"
                          + $"実測で支援拒否持ちに呪いが付いた回数は **{stoic / battles:F2} 回/戦**。"
                          + "**ガルドの説明文の「呪いも一切受け付けない」は呪詛（`CurseTrait`）のなまりを指す**ので、"
                          + "文言と実装は食い違っていない。");
        Console.WriteLine();
        Console.WriteLine("**3. ムドを叩く経路の全数**（駒の単位で数え直した。指示書の一覧は信用しない）:");
        Console.WriteLine();
        Console.WriteLine("| 側 | 駒 | 回/戦 |");
        Console.WriteLine("|---|---|--:|");
        foreach (var kv in hitAlly.OrderByDescending(x => x.Value))
            Console.WriteLine($"| **味方の刃** | {kv.Key} | {kv.Value / battles:F3} |");
        foreach (var kv in hitFoe.OrderByDescending(x => x.Value).Take(12))
            Console.WriteLine($"| 敵 | {kv.Key} | {kv.Value / battles:F3} |");
        Console.WriteLine();
        Console.WriteLine($"**味方の刃は {hitAlly.Count} 駒 ／ 敵は {hitFoe.Count} 駒。**");
        Console.WriteLine();

        Console.WriteLine("**5. 敵側の単体攻撃の体数（波別）**——マイナス側の大きさを決めるのはこれ:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 体数 | うち単体 | 単体の駒 |");
        Console.WriteLine("|---|--:|--:|---|");
        foreach (var st in hxStages)
        {
            var occ = st.Enemy.Occupied().Select(x => x.Item2).ToArray();
            var sing = occ.Where(d => d.Pattern == AttackPattern.Single && !HxChangesPattern(d)).ToArray();
            Console.WriteLine($"| {st.Name} | {occ.Length} | **{sing.Length}** "
                              + $"| {string.Join("・", sing.Select(d => d.Name).Distinct())} |");
        }
        Console.WriteLine();

        Console.WriteLine("## B-4. 意図した相手（§3-1。**測る前に規則で固定した**）");
        Console.WriteLine();
        Console.WriteLine("規則 ＝ **常時の攻撃型が単体（`ModifyPattern` を持つ特性が1つも無い）かつ 攻撃力 10 以上**（ムドを除く）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 攻 | HP | 速 | 特性 |");
        Console.WriteLine("|---|--:|--:|--:|---|");
        foreach (string id in hxIntended)
        {
            UnitDef d = hxRoster[hxIdx[id]];
            Console.WriteLine($"| {d.Name} | {d.Attack} | {d.MaxHp} | {d.Speed} "
                              + $"| {string.Join("・", d.Traits.Select(t => $"`{t}`"))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**{hxIntended.Length} 枚。** 指示書が名指しした5枚（ドルガ・トメ・ナタ・ハネ・ザン）が"
                          + "この規則で再現されるかが器具の検算になる。");
        Console.WriteLine();

        // ---- 表F: 盤面に書き込まない駒（§1-4。**採否と無関係。数えるだけ**）----
        Console.WriteLine("# 第96期 表F —— 盤面に書き込まない駒（§1-4。**この期では直さない**）");
        Console.WriteLine();
        var wrote = new HashSet<TraitId>();
        var readT = new Dictionary<TraitId, HashSet<string>>();
        void HxProbe(TraitId t, UnitState owner, UnitState target, string key, int delta)
        {
            if (delta != 0) wrote.Add(t);
            else { if (!readT.TryGetValue(t, out var h)) readT[t] = h = new HashSet<string>(); h.Add(key); }
        }
        foreach (var (_, f) in hxCompare)
            foreach (var st in hxStages)
                for (int seed = 0; seed < 8; seed++)
                    BattleEngine.Run(f, st.Enemy, seed, verbose: false, probe: HxProbe);
        var used = hxRoster.SelectMany(d => d.Traits).Distinct().OrderBy(t => (int)t).ToArray();
        var silent = used.Where(t => !wrote.Contains(t)
                                     && !TraitEntryMap.Supplies.ContainsKey(t)).ToArray();
        Console.WriteLine($"ロスターが使う特性 **{used.Length} 種**のうち、"
                          + $"**1戦のあいだ盤面に何も書かなかったのは {silent.Length} 種**"
                          + "（観測＝`CounterProbe` の書き込み ∪ `TraitEntryMap.Supplies`）。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 発火口 | 読んだキー（観測） | 保持者 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (TraitId t in silent)
            Console.WriteLine($"| `{t}` | {(TraitHookMap.TraitHooks.TryGetValue(t, out string[]? hh) ? string.Join("・", hh) : "—")} "
                              + $"| {(readT.TryGetValue(t, out var rk) ? string.Join("・", rk.Select(StatusKeys.LabelOf)) : "—")} "
                              + $"| {string.Join("・", hxRoster.Where(d => d.Traits.Contains(t)).Select(d => d.Name))} |");
        Console.WriteLine();
        Console.WriteLine("**A（完全な係数）と B（書かないが条件が戦闘中に動く）の切り分けは、"
                          + "この表の各行を読んで報告書で行う**——観測は「書いたか」しか区別できない。");
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 2×2（A ＝ ムド）。定数は第81期 `pairs2` の写し（第82〜95期と1つも変えていない）。
    // ---------------------------------------------------------------------------------
    const int HxTableSeed = 9_600_000;        // **第95期の 9,500,000 とは別の標本**（第89期の作法）
    const int HxK2 = 64, HxS2 = 2, HxBand = 0, HxM = 8;
    const int HxStrong = 7, HxWeakPct = 60, HxDrawCap = 20000;

    var hxWeakCache = new Dictionary<string, UnitDef>();
    UnitDef HxWeakOf(UnitDef d)
    {
        if (hxWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * HxWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits, Pattern = d.Pattern, Actions = d.Actions
        };
        hxWeakCache[d.Id] = w;
        return w;
    }
    var hxWeak = hxStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = HxWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef HxPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var hxPlainMap = hxRoster.ToDictionary(d => d.Id, HxPlain);

    UnitDef[] HxFill(UnitDef[] pool, int strong0, int seed)
    {
        int rn = pool.Length;
        var rng = new Random(seed);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        int remain = rn, strong = strong0;
        var picked = new UnitDef[3];
        for (int r = 0; r < 3; r++)
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
            if (sel.Attack >= HxStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int HxDrawSeed(int pairIx, int draw)
    {
        ulong x = (ulong)HxTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] HxSeats(UnitDef[] u)
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
    // TSV の添字: 0 = y11 ／ 1 = y01（**A 素体**）／ 2 = y10（**B 素体**）／ 3 = y00
    Formation HxForm(UnitDef[] u, int[] seats, int cell)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (cell & 1) != 0) || (k == 1 && (cell & 2) != 0);
            f[seats[k]] = plain ? hxPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double HxRate(Formation f, int v)
    {
        double sum = 0;
        for (int wi = 1; wi < hxStages.Count; wi++)
        {
            int wins = 0;
            for (int seed = HxBand; seed < HxBand + HxM; seed++)
                if (HxRun(f, hxWeak[wi].Enemy, seed, v).PlayerWon) wins++;
            sum += wins * 100.0 / HxM;
        }
        return sum / (hxStages.Count - 1);
    }
    var hxPairIxOf = new int[hxRN, hxRN];
    {
        int pi = 0;
        for (int a = 0; a < hxRN; a++) for (int b = a + 1; b < hxRN; b++) { hxPairIxOf[a, b] = hxPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> HxFills(int a, int b)
    {
        var pool = hxRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (hxRoster[a].Attack >= HxStrong ? 1 : 0) + (hxRoster[b].Attack >= HxStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < HxS2 * HxK2 && draw < HxDrawCap; draw++)
        {
            var f = HxFill(pool, strong0, HxDrawSeed(hxPairIxOf[a, b], draw));
            var t = f.Select(d => hxIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }

    if (hxMode == "run")
    {
        int hxV = args.Length > 3 ? int.Parse(args[3]) : 0;
        int hxSkip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int hxTake = args.Length > 5 ? int.Parse(args[5]) : int.MaxValue;
        int a0 = hxIdx[HxAId];
        var others = Enumerable.Range(0, hxRN).Where(u => u != a0).Skip(hxSkip).Take(hxTake).ToArray();
        foreach (int b in others)
        {
            var fills = HxFills(a0, b);
            var sb = new System.Text.StringBuilder();
            sb.Append(96).Append('\t').Append(hxV).Append('\t').Append(a0).Append('\t').Append(b)
              .Append('\t').Append(fills.Count);
            for (int t = 0; t < fills.Count; t++)
            {
                var team = new[] { hxRoster[a0], hxRoster[b], fills[t][0], fills[t][1], fills[t][2] };
                int[] seats = HxSeats(team);
                for (int cell = 0; cell < 4; cell++)
                    sb.Append('\t').Append(HxRate(HxForm(team, seats, cell), hxV).ToString("F6", hxInv));
            }
            Console.WriteLine(sb.ToString());
        }
        return;
    }

    if (hxMode == "tables")
    {
        (int V, int A, Dictionary<int, double[][]> D) HxRead(string path)
        {
            var d = new Dictionary<int, double[][]>();
            int vv = -1, aa = -1;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0) continue;
                var c = line.Split('\t');
                int v = int.Parse(c[1]), a = int.Parse(c[2]), b = int.Parse(c[3]), nT = int.Parse(c[4]);
                if (vv < 0) { vv = v; aa = a; }
                else if (v != vv || a != aa) throw new InvalidOperationException($"{path}: 版／A が混ざっている");
                var ys = new double[nT][];
                int at = 5;
                for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], hxInv); }
                d[b] = ys;
            }
            return (vv, aa, d);
        }
        double HxSyn(double[] y) => y[0] - y[2] - y[1] + y[3];
        bool HxInfo(double[] y) => !(y[3] < HxEps && y[1] < HxEps) && !(y[3] > 100 - HxEps && y[1] > 100 - HxEps);
        double HxPct(IReadOnlyList<double> xs, double q)
        {
            if (xs.Count == 0) return double.NaN;
            var s = xs.OrderBy(v => v).ToArray();
            if (s.Length == 1) return s[0];
            double pos = q * (s.Length - 1);
            int lo = (int)Math.Floor(pos), hi = Math.Min(lo + 1, s.Length - 1);
            return s[lo] + (pos - lo) * (s[hi] - s[lo]);
        }

        var r0 = HxRead(args[3]);
        var r1 = HxRead(args[4]);
        if (r0.V != 0 || r1.V != 1) { Console.WriteLine("V0 の TSV を先に、V1 を後に渡すこと。"); return; }
        int aIx = r0.A;
        var bs = r0.D.Keys.Where(r1.D.ContainsKey).OrderBy(x => x).ToArray();

        var dAll = new Dictionary<int, double>();
        var dSer = new Dictionary<int, double[]>();
        var dInfo = new Dictionary<int, double>();
        var nInfo = new Dictionary<int, int>();
        int floorCells = 0, ceilCells = 0, allCells = 0;
        foreach (int b in bs)
        {
            var y0 = r0.D[b]; var y1 = r1.D[b];
            int n = Math.Min(y0.Length, y1.Length);
            var all = new List<double>(); var s0 = new List<double>(); var s1 = new List<double>();
            var inf = new List<double>();
            for (int t = 0; t < n; t++)
            {
                double d = HxSyn(y1[t]) - HxSyn(y0[t]);
                all.Add(d); (t % HxS2 == 0 ? s0 : s1).Add(d);
                if (HxInfo(y0[t])) inf.Add(d);
                allCells++;
                if (y0[t][3] < HxEps && y0[t][1] < HxEps) floorCells++;
                else if (y0[t][3] > 100 - HxEps && y0[t][1] > 100 - HxEps) ceilCells++;
            }
            if (all.Count == 0) continue;
            dAll[b] = all.Average();
            dSer[b] = new[] { s0.Count > 0 ? s0.Average() : double.NaN, s1.Count > 0 ? s1.Average() : double.NaN };
            dInfo[b] = inf.Count > 0 ? inf.Average() : double.NaN;
            nInfo[b] = inf.Count;
        }

        var intendedIx = hxIntended.Where(hxIdx.ContainsKey).Select(x => hxIdx[x]).ToHashSet();
        var unintended = dAll.Keys.Where(b => !intendedIx.Contains(b)).ToArray();
        double floor = HxPct(unintended.Select(b => Math.Abs(dAll[b])).ToArray(), 0.95);
        var ranked = dAll.OrderByDescending(x => x.Value).ToArray();

        Console.WriteLine("# 第96期 表C —— 2×2 の主判定");
        Console.WriteLine();
        Console.WriteLine($"A ＝ {hxRoster[aIx].Name}（`{hxRoster[aIx].Id}`。規約 (G5)：呪いを配るのはこの駒だけ）"
                          + $" ／ B ＝ 残り {bs.Length} 体 × {HxK2 * HxS2} 台 × 4 セル × 4 波 × seed {HxBand}..{HxBand + HxM - 1}。");
        Console.WriteLine();
        Console.WriteLine($"**情報帯の割合 ＝ {(allCells - floorCells - ceilCells) * 100.0 / Math.Max(1, allCells):F1}%**"
                          + $"（床 {floorCells * 100.0 / Math.Max(1, allCells):F1}% ／ 天井 {ceilCells * 100.0 / Math.Max(1, allCells):F1}%）。"
                          + "第88期 §8-5 の並び: カド 65.2% / ハリ 70.1% / ミオ 56.5%。"
                          + "**ムドは攻3 なので床が厚いはず**という予測の答えがこれ。");
        Console.WriteLine();
        Console.WriteLine("**規約 (G3) により、engine の規則なので主判定は情報帯フィルタ<u>無し</u>で行う**"
                          + "（フィルタ有りは参考として併記する）。");
        Console.WriteLine();
        Console.WriteLine($"**増分尺度のノイズ床（第89期の規約：意図しない相手 {unintended.Length} 体の |Δ相乗| の 95%tile）"
                          + $" ＝ {floor:F2}pt。**");
        Console.WriteLine();
        Console.WriteLine("| 順位 | B | 意図 | Δ相乗 | 系列1 | 系列2 | 2系列とも正 | 床超え | 参考: フィルタ有り |");
        Console.WriteLine("|--:|---|:-:|--:|--:|--:|:-:|:-:|--:|");
        for (int i = 0; i < ranked.Length; i++)
        {
            int b = ranked[i].Key;
            bool both = dSer[b][0] > 0 && dSer[b][1] > 0;
            bool over = Math.Abs(dAll[b]) > floor;
            Console.WriteLine($"| {i + 1} | {hxRoster[b].Name} | {(intendedIx.Contains(b) ? "**○**" : "")} "
                              + $"| {dAll[b]:+0.00;-0.00;0.00} | {dSer[b][0]:+0.00;-0.00;0.00} | {dSer[b][1]:+0.00;-0.00;0.00} "
                              + $"| {(both ? "○" : "")} | {(over ? "○" : "")} "
                              + $"| {(double.IsNaN(dInfo[b]) ? "—" : dInfo[b].ToString("+0.00;-0.00;0.00"))}（{nInfo[b]} 台） |");
        }
        Console.WriteLine();
        var q11 = intendedIx.Where(dAll.ContainsKey)
                            .Where(b => dSer[b][0] > 0 && dSer[b][1] > 0 && Math.Abs(dAll[b]) > floor).ToArray();
        int q12 = unintended.Count(b => Math.Abs(dAll[b]) > floor);
        Console.WriteLine($"**Q1-1（意図した相手のうち少なくとも1枚が 2系列とも正 かつ 床超え）: "
                          + $"{q11.Length} / {intendedIx.Count(b => dAll.ContainsKey(b))} 枚**"
                          + $"{(q11.Length > 0 ? "（" + string.Join("・", q11.Select(b => hxRoster[b].Name)) + "）" : "")}"
                          + $" —— {(q11.Length > 0 ? "**○**" : "**×**")}");
        Console.WriteLine();
        Console.WriteLine($"**Q1-2（意図しない相手で床を超えた体数）: {q12} 体** —— "
                          + $"{(q12 <= 3 ? "**○**" : "**×**")}"
                          + $"（床は 95%tile なので構成上おおよそ {unintended.Length * 5 / 100} 体が必ず超える）");
        Console.WriteLine();
        int bestRank = ranked.Select((x, i) => (x.Key, i)).Where(x => intendedIx.Contains(x.Key))
                             .Select(x => x.i + 1).DefaultIfEmpty(-1).Min();
        Console.WriteLine($"**Q2（意図した組の最良順位）: {bestRank} 位 / {ranked.Length}**");
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表D —— 拒否権（分母 = `compare` 61 行全体。第91期 (G1)(G2)）＋ 交差帯12行
    // ---------------------------------------------------------------------------------
    if (hxMode == "veto")
    {
        const int HxVetoSeeds = 200;
        Console.WriteLine("# 第96期 表D —— 拒否権（`compare` 61行・交差帯12行）");
        Console.WriteLine();
        double[] HxRow(Formation f, int v)
        {
            var r = new double[hxStages.Count];
            for (int st = 0; st < hxStages.Count; st++)
            {
                int w = 0;
                for (int seed = 0; seed < HxVetoSeeds; seed++)
                    if (HxRun(f, hxStages[st].Enemy, seed, v).PlayerWon) w++;
                r[st] = w * 100.0 / HxVetoSeeds;
            }
            return r;
        }
        var prim = new HashSet<string>(Baseline.PrimaryRows);

        // `compare` 61 行を先に両版で回す（(G2) の分解に全行の平均が要る）。
        var v0 = new Dictionary<string, double[]>();
        var v1 = new Dictionary<string, double[]>();
        foreach (var (name, f) in hxCompare) { v0[name] = HxRow(f, 0); v1[name] = HxRow(f, 1); }

        Console.WriteLine($"## `compare` {hxCompare.Length} 行");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | V0 | V1 | Δ |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        int cells = 0, movedRows = 0;
        double p5v0 = 0, p5v1 = 0; int p5n = 0;
        var bigRows = new List<(string Name, int Wave, double D)>();
        foreach (var (name, _) in hxCompare)
        {
            var a = v0[name]; var b = v1[name];
            bool moved = false;
            for (int st = 0; st < hxStages.Count; st++)
            {
                if (Math.Abs(a[st] - b[st]) < HxEps) continue;
                cells++; moved = true;
                Console.WriteLine($"| {name} | {hxStages[st].Name} | {a[st]:F1}% | {b[st]:F1}% "
                                  + $"| **{b[st] - a[st]:+0.0;-0.0;0.0}** |");
                if (b[st] - a[st] <= -10.0) bigRows.Add((name, st, b[st] - a[st]));
            }
            if (moved) movedRows++;
            if (prim.Contains(name)) { p5v0 += a[^1]; p5v1 += b[^1]; p5n++; }
        }
        Console.WriteLine();
        Console.WriteLine($"**動いたセル {cells} 件 / {hxCompare.Length * hxStages.Count}・動いた行 {movedRows} / {hxCompare.Length}。**");
        Console.WriteLine();
        Console.WriteLine($"**拒否権2（主判定 {p5n} 行の第五波平均）: {p5v0 / Math.Max(1, p5n):F1}% → "
                          + $"{p5v1 / Math.Max(1, p5n):F1}%（歯止め {Baseline.PrimaryFifthFloor:F1}）"
                          + $" —— {(p5v1 / Math.Max(1, p5n) >= Baseline.PrimaryFifthFloor ? "**○**" : "**×**")}**");
        Console.WriteLine();

        int newHigh = hxCompare.Count(x => v1[x.Name][^1] > 95.0 && v0[x.Name][^1] <= 95.0);
        Console.WriteLine($"**拒否権3（第五波が 95% を超える行が新たに何行生まれたか）: {newHigh} 行"
                          + $" —— {(newHigh < 2 ? "**○**" : "**×**")}**");
        Console.WriteLine();

        // (G2) の分解: 落ちた行の駒それぞれについて「その駒を含む**他の**行」の全波平均の変化
        Console.WriteLine($"**拒否権1（(G1)(G2)：61 行の分母で −10.0pt 以上落ちたセル）: {bigRows.Count} 件**");
        Console.WriteLine();
        if (bigRows.Count == 0) Console.WriteLine("**該当なし（○）。**");
        else
        {
            var byRow = bigRows.GroupBy(x => x.Name).ToArray();
            Console.WriteLine("| 落ちた行 | 波 | Δ | 駒 | その駒を含む**他の**行 | 他の行の全波平均の変化 | 判定 |");
            Console.WriteLine("|---|---|--:|---|--:|--:|---|");
            foreach (var grp in byRow)
            {
                Formation f = hxCompare.First(x => x.Name == grp.Key).F;
                var members = f.Occupied().Select(x => x.Item2).ToArray();
                foreach (var m in members)
                {
                    var otherRows = hxCompare.Where(x => x.Name != grp.Key
                                                         && x.F.Occupied().Any(y => y.Item2.Id == m.Id)).ToArray();
                    double delta = 0;
                    foreach (var o in otherRows)
                        delta += v1[o.Name].Average() - v0[o.Name].Average();
                    string verdict = otherRows.Length == 0
                        ? "**他の行が 0 行**（分解が成立しない）"
                        : (delta / otherRows.Length <= -3.0 ? "**壊れ。拒否する**" : "制約（拒否しない）");
                    Console.WriteLine($"| {grp.Key} | {string.Join("・", grp.Select(x => hxStages[x.Wave].Name))} "
                                      + $"| {grp.Min(x => x.D):+0.0;-0.0;0.0} | {m.Name} | {otherRows.Length} "
                                      + $"| {(otherRows.Length == 0 ? "—" : (delta / otherRows.Length).ToString("+0.00;-0.00;0.00"))} "
                                      + $"| {verdict} |");
                }
            }
        }
        Console.WriteLine();

        Console.WriteLine($"## 交差帯 {hxCross.Length} 行");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | V0 | V1 | Δ |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        int xcells = 0, xrows = 0;
        foreach (var (name, f) in hxCross)
        {
            var a = HxRow(f, 0); var b = HxRow(f, 1);
            bool moved = false;
            for (int st = 0; st < hxStages.Count; st++)
            {
                if (Math.Abs(a[st] - b[st]) < HxEps) continue;
                xcells++; moved = true;
                Console.WriteLine($"| {name} | {hxStages[st].Name} | {a[st]:F1}% | {b[st]:F1}% "
                                  + $"| **{b[st] - a[st]:+0.0;-0.0;0.0}** |");
            }
            if (moved) xrows++;
        }
        Console.WriteLine();
        Console.WriteLine($"**動いたセル {xcells} 件 / {hxCross.Length * hxStages.Count}・動いた行 {xrows} / {hxCross.Length}。**");
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表E —— 副判定（Q2 分布 / Q3 内訳 / Q4 味方側の損 / Q5 ネルの自己課税）
    // ---------------------------------------------------------------------------------
    if (hxMode == "fire")
    {
        const int HxFireSeeds = 200;
        var rows = hxCompare.Where(b => b.F.Occupied().Any(x => x.Item2.Id == HxAId)).ToArray();
        Console.WriteLine("# 第96期 表E —— 呪いの分布・共有の内訳・味方側の損・ネルの自己課税");
        Console.WriteLine();
        Console.WriteLine($"台は `compare` のうち**ムドを含む {rows.Length} 行** × {hxStages.Count} 波 "
                          + $"× seed 0..{HxFireSeeds - 1}・**V1（呪い）**。");
        Console.WriteLine();

        Console.WriteLine("## Q2 / Q3 / Q4 —— 発火・共有・味方側の損");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 決着T | 呪い/戦（味/敵） | 最大同時（味/敵） | 発火/戦 | 空振り/戦 | 稼働率 "
                          + "| 共有/戦 | 共有量/戦 | うち味方が受けた量 | Δ勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var bySrc = new Dictionary<string, (int N, int Amt)>();
        double totShare = 0, totToPlayer = 0, battles = 0;
        foreach (var (name, f) in rows)
            for (int wi = 0; wi < hxStages.Count; wi++)
            {
                double mp = 0, me = 0, xp = 0, xe = 0, fire = 0, dry = 0, sh = 0, amt = 0, toP = 0, dt = 0;
                int w0 = 0, w1 = 0;
                for (int seed = 0; seed < HxFireSeeds; seed++)
                {
                    var r = HxRun(f, hxStages[wi].Enemy, seed, 1);
                    if (r.PlayerWon) w1++;
                    if (HxRun(f, hxStages[wi].Enemy, seed, 0).PlayerWon) w0++;
                    mp += r.HexMarksOnPlayer; me += r.HexMarksOnEnemy;
                    xp += r.HexMaxCursedPlayer; xe += r.HexMaxCursedEnemy;
                    fire += r.HexShareHits; dry += r.HexShareDry;
                    sh += r.HexShares; amt += r.HexShareDamage; toP += r.HexShareDamageToPlayer;
                    dt += r.Turns;
                    foreach (var kv in r.HexShareBySource)
                    {
                        bySrc.TryGetValue(kv.Key, out var cur);
                        r.HexShareDamageBySource.TryGetValue(kv.Key, out int a2);
                        bySrc[kv.Key] = (cur.N + kv.Value, cur.Amt + a2);
                    }
                }
                double n = HxFireSeeds;
                totShare += amt; totToPlayer += toP; battles += n;
                Console.WriteLine($"| {name} | {hxStages[wi].Name} | {dt / n:F2} | {mp / n:F2} / {me / n:F2} "
                                  + $"| {xp / n:F2} / {xe / n:F2} | {fire / n:F2} | {dry / n:F2} "
                                  + $"| {fire / Math.Max(1e-9, dt):F2} | {sh / n:F2} | {amt / n:F1} | {toP / n:F1} "
                                  + $"| **{(w1 - w0) * 100.0 / n:+0.0;-0.0;0.0}** |");
            }
        Console.WriteLine();
        Console.WriteLine($"**Q4（味方側の損）: 共有量の {totToPlayer * 100 / Math.Max(1e-9, totShare):F1}% が味方に落ちた。**");
        Console.WriteLine();
        Console.WriteLine("## Q3 —— 誰の単体攻撃が共有を起こしたか");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 共有/戦 | 共有量/戦 |");
        Console.WriteLine("|---|--:|--:|");
        foreach (var kv in bySrc.OrderByDescending(x => x.Value.Amt))
            Console.WriteLine($"| {kv.Key} | {kv.Value.N / battles:F3} | {kv.Value.Amt / battles:F2} |");
        Console.WriteLine();

        // Q5: ネルの自己課税
        Console.WriteLine("## Q5 —— ネルの自己課税（§1 の予測）");
        Console.WriteLine();
        Console.WriteLine("呪詛の味方漏れ（攻撃力 −5・味方全体）は**共有軸の入力そのもの**を削る"
                          + "——元の一撃が小さければ共有分も小さい。");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | Δ勝率（全波平均） | 共有量/戦 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (bool withNel in new[] { true, false })
        {
            var grp = rows.Where(b => b.F.Occupied().Any(x => x.Item2.Id == UnitCatalog.Nel.Id) == withNel).ToArray();
            if (grp.Length == 0) { Console.WriteLine($"| {(withNel ? "ネルを含む" : "ネルを含まない")} | 0 | — | — |"); continue; }
            double d = 0, amt = 0; int nn = 0;
            foreach (var (_, f) in grp)
                for (int wi = 0; wi < hxStages.Count; wi++)
                {
                    int w0 = 0, w1 = 0; double a = 0;
                    for (int seed = 0; seed < HxFireSeeds; seed++)
                    {
                        var r = HxRun(f, hxStages[wi].Enemy, seed, 1);
                        if (r.PlayerWon) w1++;
                        if (HxRun(f, hxStages[wi].Enemy, seed, 0).PlayerWon) w0++;
                        a += r.HexShareDamage;
                    }
                    d += (w1 - w0) * 100.0 / HxFireSeeds; amt += a / HxFireSeeds; nn++;
                }
            Console.WriteLine($"| {(withNel ? "**ネルを含む**" : "ネルを含まない")} | {grp.Length} "
                              + $"| **{d / nn:+0.00;-0.00;0.00}** | {amt / nn:F2} |");
        }
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表G —— 自己検査（必須4項目 ＋ 機構固有 (a)〜(h)）
    // ---------------------------------------------------------------------------------
    if (hxMode == "check")
    {
        Console.WriteLine("# 第96期 表G —— 自己検査");
        Console.WriteLine();

        // 必須1 / (a): compare 305 セルが docs/balance.md と 0 件
        var cells = new List<string>();
        foreach (var b in hxCompare)
        {
            var row = new List<string>();
            foreach (var st in hxStages)
            {
                int w = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                row.Add($"{w * 100.0 / 200:F1}%");
            }
            cells.Add($"| {b.Name} | {string.Join(" | ", row)} |");
        }
        int diff = -1;
        if (hxRoot is not null)
        {
            var doc = File.ReadAllLines(Path.Combine(hxRoot, "docs", "balance.md"))
                          .Where(l => l.StartsWith("| ") && l.Contains('%')).ToArray();
            diff = Math.Abs(doc.Length - cells.Count);
            for (int i = 0; i < Math.Min(doc.Length, cells.Count); i++)
                if (doc[i].Trim() != cells[i].Trim()) diff++;
        }
        Console.WriteLine($"- **必須1 / (a)** `compare` {hxCompare.Length * hxStages.Count} セルを `docs/balance.md` と"
                          + $"突き合わせ: **ずれ {diff} 行**{(diff == 0 ? "（○）" : "（×）")}"
                          + "——**(R1) の改名と祟りの札の追加が、どちらも盤面を1ビットも動かしていないこと。**");

        // 必須3: 触っていないノブの既定
        Console.WriteLine($"- **必須3** `SoakRule.Default` = `{SoakRule.Default}` ／ "
                          + $"`CurseRule.Default` = `{CurseRule.Default}` ／ "
                          + $"`SpillWoundRule.Default` = `{SpillWoundRule.Default}` ／ "
                          + $"`DeepRule.Default` = `{DeepRule.Default}` ／ "
                          + $"`GatherRule.Default` = `{GatherRule.Default}` ／ "
                          + $"`MendRule.Default` = `{MendRule.Default}` ／ "
                          + $"`SutureRule.Default` = `{SutureRule.Default}`"
                          + "（**`docs/rules.md` の差分で示す**）");

        // 必須4: ctx.PickOne
        int pick = 0;
        if (hxRoot is not null)
            foreach (string f in Directory.GetFiles(Path.Combine(hxRoot, "BattleCore"), "*.cs"))
                pick += System.Text.RegularExpressions.Regex.Matches(
                    string.Join("\n", File.ReadAllLines(f).Where(l => !l.TrimStart().StartsWith("//"))),
                    @"PickOne\(").Count;
        Console.WriteLine($"- **必須4** `BattleCore` の `PickOne(` 呼び出し **{pick} 箇所**"
                          + "（第94期は 26 箇所。**第96期は1つも足していない**）");
        Console.WriteLine();

        // (b)〜(h): 機構固有。**呪いが実際に走る台**で見る。
        var rows = hxCompare.Where(b => b.F.Occupied().Any(x => x.Item2.Id == HxAId)).ToArray();
        const int HxChkSeeds = 40;
        double hop = 0, nonSingle = 0, cross = 0, spill = 0, fired = 0, shares = 0, amt = 0;
        double paper = 0, marks = 0, reMark = 0, dt = 0; int n = 0;
        foreach (var (_, f) in rows)
            foreach (var st in hxStages)
                for (int seed = 0; seed < HxChkSeeds; seed++)
                {
                    var r = HxRun(f, st.Enemy, seed, 1);
                    hop += r.HexHopBlocked; nonSingle += r.HexNonSingleOnCursed; cross += r.HexCrossTeam;
                    spill += r.HexSpillSuppressed; fired += r.HexShareHits; shares += r.HexShares;
                    amt += r.HexShareDamage; marks += r.HexMarks; reMark += r.HexReMarkBlocked; dt += r.Turns;
                    paper += r.HexShareBaseTimesTargets * 0.5;
                    n++;
                }
        Console.WriteLine($"- **(b)** 呪いは二値: 既に呪われている相手への書き込みを弾いた回数 "
                          + $"**{reMark / n:F2} 回/戦**（付与 {marks / n:F2} 回/戦）。"
                          + "**カウンタは `RawCounter > 0` で弾いてから `SetCounter(_, 1)` するので 2 以上にならない**"
                          + "——弾いた回数が 0 でないことが、その分岐が実際に通っていることの証拠。");
        Console.WriteLine($"- **(c)** 1ホップのガードが止めた回数: **{hop / n:F2} 回/戦**"
                          + $"{(hop > 0 ? "（○。**ガードが実際に働いている**）" : "（—。共有先が呪い持ちの単体被弾にならなかった）")}"
                          + "——共有ダメージがさらに共有を起こしていないことの証拠。");
        Console.WriteLine($"- **(d)** 薙ぎ・貫き・全体が呪い持ちに入った回数: **{nonSingle / n:F2} 回/戦**。"
                          + "**そのすべてが共有を起こしていない**（`singleHit` の札が偽なので共有の段に入らない）。");
        Console.WriteLine($"- **(e)** 陣営をまたいだ共有: **{cross / n:F2} 回/戦**"
                          + $"{(cross == 0 ? "（○）" : "（×）")}");
        Console.WriteLine($"- **(f)** 巻き込み則の傷を抑えた回数（`spillWound: false`）: **{spill / n:F2} 回/戦**"
                          + "——**この数だけ「共有 → 傷 → 滲み」の経路を切っている。**");
        Console.WriteLine($"- **(g)** 紙と実測: 紙 **{paper / n:F2} 点/戦** 対 実測 **{amt / n:F2} 点/戦**"
                          + $" ＝ **{amt / Math.Max(1e-9, paper):F2} 倍**"
                          + "（§1-2 は「線形・分母を削らない ＝ 紙は下限のはず」と書いた。"
                          + "**1.00 を割ったら、その理由を報告書に書くこと**）");
        Console.WriteLine($"- 参考: 呪い {marks / n:F2} 回/戦 ／ 発火 {fired / n:F2} 回/戦 ／ 共有 {shares / n:F2} 本/戦 "
                          + $"／ 決着 {dt / n:F2}T（**稼働率 {fired / Math.Max(1e-9, dt):F2}**）");
        Console.WriteLine();

        // V0 で計数が全部 0
        bool zero = rows.Take(3).All(b => hxStages.All(st =>
        {
            var r = HxRun(b.F, st.Enemy, 0, 0);
            return r.HexShareHits == 0 && r.HexMarks == 0 && r.HexShares == 0;
        }));
        Console.WriteLine($"- **(i)** V0 で `HexMarks` / `HexShareHits` / `HexShares` が全部 0: {(zero ? "○" : "×")}"
                          + "——**既定では付与も走らない。**");

        // V2（印だけ・共有 0）が V0 と盤面で一致すること
        int same = 0, tot = 0;
        foreach (var b in rows)
            foreach (var st in hxStages)
            {
                int w0 = 0, w2 = 0;
                for (int seed = 0; seed < 100; seed++)
                {
                    if (HxRun(b.F, st.Enemy, seed, 0).PlayerWon) w0++;
                    if (HxRun(b.F, st.Enemy, seed, 2).PlayerWon) w2++;
                }
                tot++; if (w0 == w2) same++;
            }
        Console.WriteLine($"- **(j)** V2（印だけ・共有 0）が V0 と一致するセル: **{same} / {tot}**"
                          + $"{(same == tot ? "（○）" : "（×）")}"
                          + "——**門を数えた盤面が対照と同一であることの証拠**（第86期 X1P と同型）。");
        Console.WriteLine();
        return;
    }

    Console.WriteLine("使い方: `hex rename` / `hex phase0` / `hex run <v> [skip] [take]` / "
                      + "`hex tables <v0> <v1>` / `hex veto` / `hex fire` / `hex check`");
    return;
}
}
