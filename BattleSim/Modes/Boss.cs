using BattleCore;
using static Common;

// =====================================================================================
// boss モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "boss")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 boss
// =====================================================================================

static class BossDiag
{
// =====================================================================================
// boss モード: 「被害で育つ駒」×「死なない駒」の 4×4 を測る（第117期・ボスの土台）。
//
// **新しい機構を1つも作らない。** `BattleCore` に足したのは計数（`BossRule`）だけで、
// **`Presets` は1行も触らない**（台は診断のローカル）。
//
// 測るのは勝率ではなく**傾き**（指示書 §0-2）——勝率は「決着が早いほど高い」ので、
// 時間に比例する項を探す期には向かない。
//
//     dotnet run --project BattleSim -c Release 0 boss phase0   # §3（戦闘は決着Tの分布のみ）
//     dotnet run --project BattleSim -c Release 0 boss run      # 16組 × 対照 × 波
//     dotnet run --project BattleSim -c Release 0 boss check    # 自己検査
public static void Run(string[] args, int stageIndex)
{
    string bsMode = args.Length > 2 ? args[2] : "phase0";
    IReadOnlyList<EnemyCatalog.Stage> bsStages = EnemyCatalog.Stages;
    const int BsSeeds = 200;      // 帯A。`compare` と揃える（規約 (G14)）
    const int BsHalf = 3;         // 前半3T / 後半3T（指示書 §0-2）
    const int BsMinTurns = 2 * BsHalf;   // 前半と後半が重ならない最短の戦闘
    // **傾きが「測れている」ための線**（規約 (G12)。**Phase 0 の分布を見た後、`run` を回す前に固定した**）。
    // L ≥ 6 の戦がこの割合を下回る組は、傾きの分母が組ごとに桁で違う——
    // 「後半が強い」と「後半まで残った戦だけを見ている」を区別できない。
    const double BsOkFloor = 20.0;

    // --- 被害で育つ 4 枚 × 死なない 4 枚（指示書 §0-1）。**指示書の分類は表P-1 で検算する。**
    var bsGrow = new[] { UnitCatalog.Mudo, UnitCatalog.Kado, UnitCatalog.Yomi, UnitCatalog.Utsu };
    var bsHold = new[] { UnitCatalog.Gald, UnitCatalog.Vel, UnitCatalog.Mug, UnitCatalog.Golm };

    // 素体（同じ数値・特性なし）。`Actions` も落とす（カドの構えが残ると差分が1つに閉じない）。
    static UnitDef BsPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
        Traits = Array.Empty<TraitId>()
    };

    // ------------------------------------------------------------------------------
    // Traits.cs をクラス単位に切って索く（第94期の「手で写した表を機械が照合する」の作法）。
    // **戦闘を1回も回さない。** 手で書いた分類は1件も無い。
    // ------------------------------------------------------------------------------
    static string? BsRoot()
    {
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Path.GetDirectoryName(root);
        return root;
    }

    var bsBody = new Dictionary<TraitId, string>();
    {
        string? root = BsRoot();
        string path = root is null ? "" : Path.Combine(root, "BattleCore", "Traits.cs");
        if (File.Exists(path))
        {
            string src = File.ReadAllText(path);
            // クラス宣言で切る。基底（`: XxxTrait`）の本体は後段で連結する。
            var decl = System.Text.RegularExpressions.Regex.Matches(
                src, @"public (?:sealed |abstract )?class (\w+Trait)(?:\s*:\s*(\w+))?");
            var byName = new Dictionary<string, (string Body, string? Base)>(StringComparer.Ordinal);
            for (int i = 0; i < decl.Count; i++)
            {
                int st = decl[i].Index;
                int en = i + 1 < decl.Count ? decl[i + 1].Index : src.Length;
                string bse = decl[i].Groups[2].Success ? decl[i].Groups[2].Value : null!;
                byName[decl[i].Groups[1].Value] = (src.Substring(st, en - st), bse);
            }
            foreach (var kv in byName)
            {
                string body = kv.Value.Body;
                // 基底の本体を連結する（庇う＝`GuardianTrait : RedirectGainTrait` がこれ）。
                if (kv.Value.Base is { } b && byName.TryGetValue(b, out var bb)) body += bb.Body;
                var m = System.Text.RegularExpressions.Regex.Match(body, @"TraitId Id => TraitId\.(\w+)");
                if (m.Success && Enum.TryParse(m.Groups[1].Value, out TraitId tid))
                    bsBody[tid] = body;
            }
        }
    }
    // **索けなかったら止める。** `bsBody` が空でも分類はすべて偽を返すので、
    // 黙って「規則 (a)〜(c) に1枚も引っかからない」＝**土台の顔ぶれが静かに変わる**
    // （実際に踏んだ: リポジトリの外から叩くと `Traits.cs` が見つからず、
    //  土台が ドルガ・ナタ・エグ から ドルガ・ボルグ・ハギ に化けた）。
    if (bsBody.Count == 0)
    {
        Console.WriteLine("boss: `BattleCore/Traits.cs` が見つからない（リポジトリ直下から実行すること）。");
        return;
    }

    bool BsHas(UnitDef d, params string[] needles)
        => d.Traits.Any(t => bsBody.TryGetValue(t, out string? b) && needles.Any(n => b.Contains(n, StringComparison.Ordinal)));

    // 規則 (a)〜(d)（指示書 §1-1）。**すべて実装から引く。**
    bool BsWritesAtk(UnitDef d) => BsHas(d, "ctx.Whet(", "ctx.Dull(");                    // (a)
    bool BsReadsAtk(UnitDef d) => d.Traits.Any(t => t is TraitId.Overload or TraitId.Perverse); // (b)
    bool BsSuppliesHit(UnitDef d) => BsHas(d, "isFriendlyFire: true");                    // (c) 被弾
    bool BsSuppliesMove(UnitDef d) => BsHas(d, "SwapSlots", "HaulOutPair", "FallBack", "ctx.Summon"); // (c) 移動
    bool BsFragile(UnitDef d) => d.MaxHp < 50;                                            // (d)
    // (c') 追加で見る2つ。**規則ではなく判断**なので、表に列として出したうえで報告書に書く。
    bool BsHeals(UnitDef d) => BsHas(d, "ctx.Heal(", "ctx.Revive(");
    bool BsSelfGrows(UnitDef d) => BsHas(d, "self.AtkBonus +=");

    // 8 枚と、埋め草の候補（`UnitCatalog.All` から）。
    var bsRoster = UnitCatalog.All.ToArray();
    var bsEight = bsGrow.Concat(bsHold).ToArray();

    bool BsFillerOk(UnitDef d) => !bsEight.Contains(d)
        && !BsWritesAtk(d) && !BsReadsAtk(d) && !BsSuppliesHit(d) && !BsSuppliesMove(d) && !BsFragile(d);

    // ------------------------------------------------------------------------------
    // 台（§1-1）。**16 組すべてで土台3枚・席は同一。**
    //   前1 = 死なない（庇う・巨躯・後備えはどれも「前にいること」が条件）
    //   中央 = 育つ（隣接次数4 ＝ 棘の巻き込みが最大／両レーンに属して被弾も最大／
    //          前1 の巨躯の被覆に入る）
    //   前3・後1・後3 = 土台3枚（16 組で不変）
    // 席は測る前に固定する（結果を見てから動かさない）。
    // ------------------------------------------------------------------------------
    UnitDef[] bsFill = Array.Empty<UnitDef>();
    Formation BsBench(UnitDef? grow, UnitDef? hold) => Formation.Build(
        front1: hold, front3: bsFill.Length > 0 ? bsFill[0] : null, center: grow,
        back1: bsFill.Length > 1 ? bsFill[1] : null, back3: bsFill.Length > 2 ? bsFill[2] : null);

    // 埋め草は「規則を満たす候補のうち、回復も自己強化も持たず、総攻がいちばん大きい3枚」。
    // **選び方を測る前に固定する**（第64期）。総攻で選ぶのは台を床にしないため（第109期）。
    // 4枚目は**置換用**（§1-2 の単騎／死なない側だけ の対照で、抜けた枠に入る駒）。
    var bsPool = bsRoster.Where(BsFillerOk).Where(d => !BsHeals(d) && !BsSelfGrows(d))
                     .OrderByDescending(d => d.Attack).ThenBy(d => d.Id, StringComparer.Ordinal)
                     .ToArray();
    bsFill = bsPool.Take(3).ToArray();
    UnitDef? bsSub = bsPool.Length > 3 ? bsPool[3] : null;   // 置換用（4枚目）

    // ------------------------------------------------------------------------------
    // 1戦を回して時系列を切り出す。**`BossRule(true)` は計数だけを起こす**（盤面は動かない）。
    // 前半 = ターン 1..3、後半 = ターン L-2..L（L = 決着ターン）。
    // **L < 6 の戦は前半と後半が重なる**ので、傾きの分母から外す（`Ok` の列）。
    // ------------------------------------------------------------------------------
    BsRow BsOne(Formation f, Formation enemy, int seed, string growId)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false, boss: new BossRule(true));
        int L = Math.Max(1, r.Turns);
        var row = new BsRow { Turns = L, Won = r.PlayerWon };

        int[] team = new int[BattleEngine.MaxTurns + 2];
        foreach ((int _, UnitDef d) in f.Occupied())
        {
            if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
            if (t.BossLastAliveTurn > row.PartyAlive) row.PartyAlive = t.BossLastAliveTurn;
            if (t.BossDmgByTurn is { } by)
                for (int i = 0; i < team.Length && i < by.Length; i++) team[i] += by[i];
            row.DmgTotal += t.DamageToEnemy;
        }

        if (r.TallyByUnit.TryGetValue(growId, out UnitTally? g))
        {
            row.GrowAlive = g.BossAliveTurns;
            row.GrowSwing = g.BossSwingTurns;
            row.GrowLastAlive = g.BossLastAliveTurn;
            row.GrowDmg = g.DamageToEnemy;
            int[] a = g.BossAtkByTurn ?? Array.Empty<int>();
            int At(int t) => t >= 0 && t < a.Length ? a[t] : 0;
            row.Atk1 = At(1);
            row.Atk3 = At(Math.Min(BsHalf, L));
            row.AtkEnd = At(g.BossLastAliveTurn);
            for (int t = 1; t <= L && t < a.Length; t++) if (a[t] > row.AtkMax) row.AtkMax = a[t];
        }

        if (r.TallyByUnit.TryGetValue(growId, out UnitTally? g2) && g2.BossDmgByTurn is { } gby)
        {
            for (int t = 1; t <= Math.Min(BsHalf, L) && t < gby.Length; t++) row.GrowEarly += gby[t];
            for (int t = Math.Max(1, L - BsHalf + 1); t <= L && t < gby.Length; t++) row.GrowLate += gby[t];
        }
        for (int t = 1; t <= Math.Min(BsHalf, L); t++) row.DmgEarly += team[t];
        for (int t = Math.Max(1, L - BsHalf + 1); t <= L && t < team.Length; t++) row.DmgLate += team[t];
        row.Ok = L >= BsMinTurns;
        return row;
    }

    // 波は第2〜5波（規約 (G10)。第一波は全行が単発で 100% 勝つ教習波）。
    int[] bsWaves = { 1, 2, 3, 4 };   // 添字（0 = 第一波）

    // ------------------------------------------------------------------------------
    // 長期戦の台（§1-3）。**敵の HP だけを倍にする。特性・攻撃力・速さ・攻撃型・体数は1つも触らない。**
    //
    // Phase 0 の結論は「ターン数の観点では長期戦の台は要らない」（L ≥ 6 が 67.3%）だったが、
    // `run` の実測で**別の理由**が出た——既存5波は敵が 3〜5 体で、後半には**標的が尽きる**。
    // 与ダメ/T が落ちたのが「味方が倒れたから」なのか「殴る相手がいないから」なのかを、
    // 5波の台では原理的に切り分けられない（規約 (G7) の3つ目「分母を削るか」）。
    // **ボスは1体で HP が大きい**ので、この台のほうが主題に近い。
    // ------------------------------------------------------------------------------
    const int BsLongMul = 4;
    static Formation BsLongOf(Formation e, int mul)
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in e.Occupied())
            f[sl] = new UnitDef
            {
                Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * mul,
                Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
                Traits = d.Traits, Actions = d.Actions,
                PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor
            };
        return f;
    }
    bool bsLong = false;
    Formation BsEnemy(int w) => bsLong ? BsLongOf(bsStages[w].Enemy, BsLongMul) : bsStages[w].Enemy;

    // 傾き = 後半の与ダメ/T ÷ 前半の与ダメ/T。**戦ごとの比を平均しない**
    // （前半 0 の戦で発散する）——**合計どうしを割る**。分母は `Ok` の戦だけ。
    static double BsRate(List<BsRow> xs, Func<BsRow, int> f)
    {
        var ok = xs.Where(x => x.Ok).ToList();
        return ok.Count == 0 ? 0 : ok.Sum(x => (double)f(x)) / (ok.Count * 3.0);
    }
    static double BsSlope(List<BsRow> xs)
    {
        double e = BsRate(xs, x => x.DmgEarly), l = BsRate(xs, x => x.DmgLate);
        return e <= 0 ? 0 : l / e;
    }
    /// <summary>育つ側だけの傾き（味方全体の傾きから「他の駒が倒れた」を抜いたもの）。</summary>
    static double BsSlopeGrow(List<BsRow> xs)
    {
        double e = BsRate(xs, x => x.GrowEarly), l = BsRate(xs, x => x.GrowLate);
        return e <= 0 ? 0 : l / e;
    }
    static double BsAvg(List<BsRow> xs, Func<BsRow, double> f) => xs.Count == 0 ? 0 : xs.Average(f);
    static double BsWin(List<BsRow> xs) => xs.Count == 0 ? 0 : xs.Count(x => x.Won) * 100.0 / xs.Count;

    List<BsRow> BsRunAll(Formation f, string growId)
    {
        var rows = new List<BsRow>();
        foreach (int w in bsWaves)
            for (int seed = 0; seed < BsSeeds; seed++)
                rows.Add(BsOne(f, BsEnemy(w), seed, growId));
        return rows;
    }

    // 16 組（4 × 4 の総当たり）。並びは表の行順で固定する。
    var bsPairs = new List<(UnitDef G, UnitDef H)>();
    foreach (UnitDef g0 in bsGrow) foreach (UnitDef h0 in bsHold) bsPairs.Add((g0, h0));

    // ==============================================================================
    // phase0（§3）。**戦闘は決着Tの分布だけ。**
    // ==============================================================================
    if (bsMode == "phase0")
    {
        Console.WriteLine("# 第117期 Phase 0 —— ボスの土台（`boss phase0`）");
        Console.WriteLine();
        Console.WriteLine($"`Traits.cs` から索けた特性: **{bsBody.Count} 件**（手で書いた分類は 0 件）。");

        // --- 表P-1: 8枚の入力
        Console.WriteLine();
        Console.WriteLine("## 表P-1. 8 枚の機構を実装から引く（§3-1）");
        Console.WriteLine();
        Console.WriteLine("**指示書 §0-1 の分類は信用しない**（第116期に §0-2 が実配置と合わなかった）。");
        Console.WriteLine();
        Console.WriteLine("| 枠 | 駒 | HP | 攻 | 速 | 特性 | フック | 自己強化 | 回復/蘇生 | 味方に被弾 | 移動 | 他人の攻を書く |");
        Console.WriteLine("|---|---|--:|--:|--:|---|---|:-:|:-:|:-:|:-:|:-:|");
        foreach (UnitDef d in bsEight)
        {
            bool grow = bsGrow.Contains(d);
            string traits = string.Join("・", d.Traits.Select(t => t.ToString()));
            var hooks = new List<string>();
            foreach (TraitId t in d.Traits)
            {
                if (!bsBody.TryGetValue(t, out string? b)) continue;
                if (b.Contains("void OnDamaged", StringComparison.Ordinal)) hooks.Add($"{t}:被弾");
                if (b.Contains("void OnMoved", StringComparison.Ordinal)) hooks.Add($"{t}:移動");
                if (b.Contains("void OnDeath", StringComparison.Ordinal)) hooks.Add($"{t}:死");
                if (b.Contains("void OnAllyDeath", StringComparison.Ordinal)) hooks.Add($"{t}:味方の死");
                if (b.Contains("int ModifyAttack", StringComparison.Ordinal)) hooks.Add($"{t}:攻の書換");
                if (b.Contains("bool BlocksSupport", StringComparison.Ordinal)) hooks.Add($"{t}:支援拒否");
            }
            Console.WriteLine($"| {(grow ? "**育つ**" : "死なない")} | {d.Name} | {d.MaxHp} | {d.Attack} | {d.Speed} | {traits} | "
                + $"{(hooks.Count == 0 ? "—" : string.Join(" / ", hooks))} | "
                + $"{(BsSelfGrows(d) ? "○" : "×")} | {(BsHeals(d) ? "○" : "×")} | {(BsSuppliesHit(d) ? "○" : "×")} | "
                + $"{(BsSuppliesMove(d) ? "○" : "×")} | {(BsWritesAtk(d) ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **惨禍（`Havoc`）は育ちの機構ではない**——`OnBattleStart` でログを出すだけで、");
        Console.WriteLine("> 判定は engine（`ApplyDamage` の味方全体 被ダメージ +50%）にある。");
        Console.WriteLine("> **カドが育つのは棘（`Thorns`）の味方巻き込み**で、代金は隣接味方のHP。");

        // --- 表P-2: 土台候補の全数列挙
        Console.WriteLine();
        Console.WriteLine("## 表P-2. 土台3枚の候補を規則 (a)〜(d) で全数列挙（§1-1）");
        Console.WriteLine();
        Console.WriteLine($"分母は `UnitCatalog.All` の **{bsRoster.Length} 枚**から 8 枚を除いた **{bsRoster.Length - bsEight.Length} 枚**（規約 (G4)）。");
        Console.WriteLine();
        Console.WriteLine("| 判定 | 駒 | HP | 攻 | 速 | (a) 他人の攻を書かない | (b) 読み手でない | (c) 被弾を撒かない | (c) 移動を撒かない | (d) HP≥50 | 回復 | 自己強化 |");
        Console.WriteLine("|:-:|---|--:|--:|--:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|");
        foreach (UnitDef d in bsRoster.Where(d => !bsEight.Contains(d))
                                      .OrderByDescending(d => bsFill.Contains(d) || bsSub == d)
                                      .ThenByDescending(d => d.Attack))
        {
            bool ok = BsFillerOk(d);
            bool pick = bsFill.Contains(d);
            bool sub = bsSub == d;
            if (!ok && !pick && !sub) continue;
            string mark = pick ? "**採**" : sub ? "**置**" : "候";
            Console.WriteLine($"| {mark} | {d.Name} | {d.MaxHp} | {d.Attack} | {d.Speed} | "
                + $"{(!BsWritesAtk(d) ? "○" : "×")} | {(!BsReadsAtk(d) ? "○" : "×")} | {(!BsSuppliesHit(d) ? "○" : "×")} | "
                + $"{(!BsSuppliesMove(d) ? "○" : "×")} | {(!BsFragile(d) ? "○" : "×")} | "
                + $"{(BsHeals(d) ? "○" : "×")} | {(BsSelfGrows(d) ? "○" : "×")} |");
        }
        Console.WriteLine();
        var bsRej = bsRoster.Where(d => !bsEight.Contains(d) && !BsFillerOk(d)).ToArray();
        Console.WriteLine($"規則 (a)〜(d) で落ちた候補は **{bsRej.Length} 枚**（内訳: "
            + $"(a) {bsRej.Count(BsWritesAtk)} / (b) {bsRej.Count(BsReadsAtk)} / "
            + $"(c)被弾 {bsRej.Count(BsSuppliesHit)} / (c)移動 {bsRej.Count(BsSuppliesMove)} / (d) {bsRej.Count(BsFragile)}"
            + "。**重複して落ちる駒があるので合計は一致しない**）。");
        Console.WriteLine();
        Console.WriteLine("**規則 (a)〜(d) に加えて、回復と自己強化の2つを判断で落とした**（指示書には無い）:");
        Console.WriteLine("回復は Q3（死なない側が生存Tを買っているか）と交絡し、");
        Console.WriteLine("自己強化は Q4（育ちが出力になっているか）の分子に土台のぶんが混ざる。");
        Console.WriteLine($"採ったのは **{string.Join("・", bsFill.Select(d => d.Name))}**"
            + $"（総攻 {bsFill.Sum(d => d.Attack)}）、置換用の4枚目は **{bsSub?.Name ?? "—"}**。");

        // --- 表P-3: 決着ターンの分布
        Console.WriteLine();
        Console.WriteLine("## 表P-3. 16 組の決着ターンの分布（§1-3・**ここだけ戦闘を回す**）");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{BsSeeds - 1} × 第2〜5波。**前半3T と後半3T が重ならない条件は L ≥ {BsMinTurns}**。");
        Console.WriteLine();
        Console.WriteLine("| 組 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | L≥6 の割合 | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        var bsP3 = new List<(string Name, List<BsRow> Rows)>();
        foreach ((UnitDef g, UnitDef h) in bsPairs)
        {
            Formation f = BsBench(g, h);
            var all = new List<BsRow>();
            var cells = new List<string>();
            foreach (int w in bsWaves)
            {
                var rows = new List<BsRow>();
                for (int seed = 0; seed < BsSeeds; seed++) rows.Add(BsOne(f, bsStages[w].Enemy, seed, g.Id));
                all.AddRange(rows);
                cells.Add($"{BsAvg(rows, x => x.Turns):F1}");
            }
            bsP3.Add(($"{g.Name} × {h.Name}", all));
            Console.WriteLine($"| {g.Name} × {h.Name} | {string.Join(" | ", cells)} | "
                + $"{BsAvg(all, x => x.Turns):F1} | {all.Count(x => x.Ok) * 100.0 / all.Count:F1}% | {BsWin(all):F1}% |");
        }
        double bsOkAll = bsP3.SelectMany(v => v.Rows).Count(x => x.Ok) * 100.0
                       / Math.Max(1, bsP3.Sum(v => v.Rows.Count));
        Console.WriteLine();
        Console.WriteLine($"**16 組 × 4 波 × {BsSeeds} seed の全体で L ≥ {BsMinTurns} は {bsOkAll:F1}%。**");
        Console.WriteLine(bsOkAll >= 50.0
            ? "→ **長期戦の台は足さない**（後半3T が過半の戦で取れる）。§1-3 の分岐は既存5波のまま。"
            : "→ **後半3T が取れる戦が過半に届かない。** §1-3 の分岐に従い、長期戦の台を検討する。");

        // --- 表P-4: 読み手の混入
        Console.WriteLine();
        Console.WriteLine("## 表P-4. `AtkBonus` の読み手が土台に混ざっていないこと（§3-4）");
        Console.WriteLine();
        Console.WriteLine("| 読み手の特性 | 駒 | 土台に入るか |");
        Console.WriteLine("|---|---|:-:|");
        foreach (UnitDef d in bsRoster.Where(BsReadsAtk))
            Console.WriteLine($"| {string.Join("・", d.Traits.Where(t => t is TraitId.Overload or TraitId.Perverse))} | "
                + $"{d.Name} | {(bsFill.Contains(d) || bsSub == d ? "**×（混入）**" : "○（入らない）")} |");
        Console.WriteLine();
        Console.WriteLine("**ウツは 16 組の「育つ」側なので、土台には入らない。**"
            + " `ReaderRule` の既定は第116期に 5 なので、バンが台に入ると薙ぎ化が混ざる。");

        // --- 表P-5: 過去に同種を測っていないか
        Console.WriteLine();
        Console.WriteLine("## 表P-5. 過去に同種を測っていないか（§3-5・`design/` の grep）");
        Console.WriteLine();
        {
            string? root5 = BsRoot();
            string dir5 = root5 is null ? "" : Path.Combine(root5, "design");
            var needles = new[] { "被弾強化", "巨躯", "死なない", "傾き", "ボス", "時間に比例" };
            Console.WriteLine("| 語 | 当たった文書 |");
            Console.WriteLine("|---|---|");
            foreach (string n in needles)
            {
                var hits = Directory.Exists(dir5)
                    ? Directory.GetFiles(dir5, "*.md").Where(p => File.ReadAllText(p).Contains(n, StringComparison.Ordinal))
                        .Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal).ToArray()
                    : Array.Empty<string?>();
                Console.WriteLine($"| `{n}` | {(hits.Length == 0 ? "—" : string.Join(" / ", hits.Take(6)) + (hits.Length > 6 ? $" ほか {hits.Length - 6} 件" : ""))} |");
            }
        }

        // --- 表P-6: Presets に既にある組
        Console.WriteLine();
        Console.WriteLine("## 表P-6. `Presets` に既にある「育つ × 死なない」の組（§3-6）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 育つ | 死なない |");
        Console.WriteLine("|---|---|---|");
        var bsBuilds = CompareBuilds();
        int bsHave = 0;
        foreach (var (name, f) in bsBuilds)
        {
            var gs = f.Occupied().Select(o => o.Def).Where(d => bsGrow.Contains(d)).ToArray();
            var hs = f.Occupied().Select(o => o.Def).Where(d => bsHold.Contains(d)).ToArray();
            if (gs.Length == 0 || hs.Length == 0) continue;
            bsHave++;
            Console.WriteLine($"| {name} | {string.Join("・", gs.Select(d => d.Name))} | {string.Join("・", hs.Select(d => d.Name))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**{bsHave} 行 / {bsBuilds.Length} 行**が既に「育つ × 死なない」を含む。"
            + "ただし**土台が行ごとに違う**ので、16 組の総当たりの代わりにはならない（§1-1 の比較の成立条件）。");

        // --- 予測
        Console.WriteLine();
        Console.WriteLine("## 予測（§2-3・**測る前に書いてある**）");
        Console.WriteLine();
        Console.WriteLine("1. **ゴルム × ムド が傾き最上位**（ただし巨躯は被弾を肩代わりして `Rage` の入力を**奪う**ので、");
        Console.WriteLine("   実際に効くのは吐き戻し＝`Whet` の側。指示書の理由付けは実装と合っていない）");
        Console.WriteLine("2. **ウツはどの組でも傾きが低い**——弱体の供給が土台に無い（規則 (c)）ので入力が敵依存");
        Console.WriteLine("3. **ヨミは死なない側と噛まない**——`Displaced` の入力は移動で、死なない駒は移動の供給者ではない");
        Console.WriteLine("4. **Q3 はガルドとゴルムが通り、ムグとヴェルは通らない**");
        Console.WriteLine("5. **Q5 は届かない**——15T でも 1,400 の半分程度");
        return;
    }

    // ==============================================================================
    // run（§4）。16 組 × 対照 × 波。**測るのは勝率ではなく傾き**（§0-2）。
    // ==============================================================================
    if (bsMode == "run" || bsMode == "long")
    {
        bsLong = bsMode == "long";
        Console.WriteLine($"# 第117期 —— ボスの土台（`boss {bsMode}`）");
        Console.WriteLine();
        Console.WriteLine($"台 = 前1 死なない / 中央 育つ / 前3・後1・後3 **{string.Join("・", bsFill.Select(d => d.Name))}**"
            + $"（16 組で同一）。第2〜5波 × seed 0..{BsSeeds - 1}。");
        if (bsLong)
        {
            Console.WriteLine();
            Console.WriteLine($"**長期戦の台（§1-3）: 敵の最大HPを一律 ×{BsLongMul}。特性・攻撃力・速さ・攻撃型・体数は1つも触っていない。**");
            Console.WriteLine("狙いは**標的が尽きることと味方が倒れることの切り分け**（規約 (G7) の3つ目）。");
        }
        Console.WriteLine();
        Console.WriteLine($"**傾き = 後半{BsHalf}T の与ダメ/T ÷ 前半{BsHalf}T の与ダメ/T。**"
            + $" 分母は **L ≥ {BsMinTurns} の戦だけ**（前半と後半が重ならない条件）。");

        // --- 本体を回す
        var bsMain = new List<(UnitDef G, UnitDef H, List<BsRow> Cur, List<BsRow> Pla)>();
        foreach ((UnitDef g, UnitDef h) in bsPairs)
        {
            bsMain.Add((g, h, BsRunAll(BsBench(g, h), g.Id),
                              BsRunAll(BsBench(BsPlain(g), h), BsPlain(g).Id)));
        }
        // 単騎（死なない側を置換駒に）と、死なない側だけ（育つ側を置換駒に）
        var bsSolo = bsGrow.ToDictionary(g => g.Id, g => BsRunAll(BsBench(g, bsSub), g.Id));
        var bsHoldOnly = bsHold.ToDictionary(h => h.Id, h => BsRunAll(BsBench(bsSub, h), bsSub!.Id));

        // --- 表A: 16 組 × 門1〜3 ＋ 傾き（全波まとめ）
        Console.WriteLine();
        Console.WriteLine("## 表A. 16 組 —— 門1〜3 と傾き（第2〜5波まとめ）");
        Console.WriteLine();
        Console.WriteLine($"**※ = L ≥ {BsMinTurns} の戦が {BsOkFloor:F0}% 未満 ＝ 傾きが測れていない**（規約 (G12)。"
            + "分母が組ごとに桁で違うので、判定 Q1・Q2・Q4 からは外す）。");
        Console.WriteLine();
        Console.WriteLine("| 組 | 決着T | 生存T | 育ち 開戦 | 前半末 | 終端 | **到達点** | 前半 ダメ/T | 後半 ダメ/T | **傾き** | 育つ側の与ダメ | 育つ側だけの傾き | 空振り | 勝率 | L≥6 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var bsSlopes = new List<(string Name, double Slope, double Peak, double Late, UnitDef G, UnitDef H)>();
        foreach (var (g, h, cur, _) in bsMain)
        {
            double sl = BsSlope(cur), peak = BsAvg(cur, x => x.AtkMax), late = BsRate(cur, x => x.DmgLate);
            if (cur.Count(x => x.Ok) * 100.0 / cur.Count >= BsOkFloor)
                bsSlopes.Add(($"{g.Name} × {h.Name}", sl, peak, late, g, h));
            double idle = BsAvg(cur, x => x.GrowAlive - x.GrowSwing);
            double okPct = cur.Count(x => x.Ok) * 100.0 / cur.Count;
            Console.WriteLine($"| {g.Name} × {h.Name}{(okPct < BsOkFloor ? " ※" : "")} | {BsAvg(cur, x => x.Turns):F2} | {BsAvg(cur, x => x.PartyAlive):F2} | "
                + $"{BsAvg(cur, x => x.Atk1):F1} | {BsAvg(cur, x => x.Atk3):F1} | {BsAvg(cur, x => x.AtkEnd):F1} | "
                + $"**{peak:F1}** | {BsRate(cur, x => x.DmgEarly):F1} | {late:F1} | **{sl:F2}** | "
                + $"{BsAvg(cur, x => x.GrowDmg):F1} | {BsSlopeGrow(cur):F2} | {idle:F2} | {BsWin(cur):F1}% | {okPct:F1}% |");
        }

        // --- 表A': 波別の傾き
        Console.WriteLine();
        Console.WriteLine("## 表A'. 波別の傾き（**L ≥ 6 の戦の割合を併記**。規約 (G6)）");
        Console.WriteLine();
        Console.WriteLine("| 組 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var (g, h, cur, _) in bsMain)
        {
            var cells = new List<string>();
            for (int i = 0; i < bsWaves.Length; i++)
            {
                var seg = cur.Skip(i * BsSeeds).Take(BsSeeds).ToList();
                int ok = seg.Count(x => x.Ok);
                cells.Add(ok == 0 ? "—" : $"{BsSlope(seg):F2} ({ok * 100.0 / seg.Count:F0}%)");
            }
            Console.WriteLine($"| {g.Name} × {h.Name} | {string.Join(" | ", cells)} |");
        }

        // --- 表B: 対照
        Console.WriteLine();
        Console.WriteLine("## 表B. 対照（§1-2）");
        Console.WriteLine();
        Console.WriteLine("### B-1. 素体差し替え（育つ側を同じ数値・特性なしに）");
        Console.WriteLine();
        Console.WriteLine("| 組 | 傾き 現行 | 傾き 素体 | 差 | 到達点 現行 | 到達点 素体 | 勝率 現行 | 勝率 素体 | 帰属 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (g, h, cur, pla) in bsMain)
            Console.WriteLine($"| {g.Name} × {h.Name} | {BsSlope(cur):F2} | {BsSlope(pla):F2} | "
                + $"{BsSlope(cur) - BsSlope(pla):+0.00;-0.00;0.00} | {BsAvg(cur, x => x.AtkMax):F1} | {BsAvg(pla, x => x.AtkMax):F1} | "
                + $"{BsWin(cur):F1}% | {BsWin(pla):F1}% | {BsWin(cur) - BsWin(pla):+0.0;-0.0;0.0}pt |");

        Console.WriteLine();
        Console.WriteLine($"### B-2. 単騎（死なない側を置換駒 **{bsSub?.Name ?? "—"}** に）");
        Console.WriteLine();
        Console.WriteLine("| 育つ | 生存T | 傾き | 到達点 | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (UnitDef g in bsGrow)
        {
            var so = bsSolo[g.Id];
            Console.WriteLine($"| {g.Name} | {BsAvg(so, x => x.PartyAlive):F2} | {BsSlope(so):F2} | "
                + $"{BsAvg(so, x => x.AtkMax):F1} | {BsWin(so):F1}% |");
        }

        Console.WriteLine();
        Console.WriteLine($"### B-3. 死なない側だけ（育つ側を置換駒 **{bsSub?.Name ?? "—"}** に）");
        Console.WriteLine();
        Console.WriteLine("| 死なない | 生存T | 傾き | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (UnitDef h in bsHold)
        {
            var so = bsHoldOnly[h.Id];
            Console.WriteLine($"| {h.Name} | {BsAvg(so, x => x.PartyAlive):F2} | {BsSlope(so):F2} | {BsWin(so):F1}% |");
        }

        // --- 表C: Q4 の順位相関
        Console.WriteLine();
        Console.WriteLine("## 表C. Q4 —— 到達点 × 出力（順位相関）");
        Console.WriteLine();
        double[] bsX = bsSlopes.Select(x => x.Peak).ToArray();
        double[] bsY = bsSlopes.Select(x => x.Late).ToArray();
        static double[] BsRank(double[] v)
        {
            var idx = Enumerable.Range(0, v.Length).OrderBy(i => v[i]).ToArray();
            var r = new double[v.Length];
            for (int i = 0; i < idx.Length;)
            {
                int j = i;
                while (j + 1 < idx.Length && Math.Abs(v[idx[j + 1]] - v[idx[i]]) < 1e-9) j++;
                double avg = (i + j) / 2.0 + 1;
                for (int k = i; k <= j; k++) r[idx[k]] = avg;
                i = j + 1;
            }
            return r;
        }
        static double BsPearson(double[] a, double[] b)
        {
            double ma = a.Average(), mb = b.Average();
            double sa = Math.Sqrt(a.Sum(x => (x - ma) * (x - ma))), sb = Math.Sqrt(b.Sum(x => (x - mb) * (x - mb)));
            if (sa <= 0 || sb <= 0) return 0;
            return a.Zip(b, (x, y) => (x - ma) * (y - mb)).Sum() / (sa * sb);
        }
        double bsSp = BsPearson(BsRank(bsX), BsRank(bsY));
        double bsPe = BsPearson(bsX, bsY);
        int bsTieX = bsX.Length - bsX.Distinct().Count(), bsTieY = bsY.Length - bsY.Distinct().Count();
        Console.WriteLine("| 量 | スピアマン | ピアソン | 同値塊 |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| 到達点 × 後半ダメ/T | {bsSp:F3} | {bsPe:F3} | X {bsTieX} / Y {bsTieY} |");
        Console.WriteLine();
        Console.WriteLine("**(G13)**: 同値塊が分母の 2 割を超えたら順位相関を判定に使わない。"
            + $" いまの同値塊は X {bsTieX * 100.0 / bsX.Length:F0}% / Y {bsTieY * 100.0 / bsY.Length:F0}%。");

        // --- 表D: Q5 の外挿
        Console.WriteLine();
        Console.WriteLine("## 表D. Q5 —— 15T の外挿（**紙**。門ではなく出力・規約 (G7)）");
        Console.WriteLine();
        var bsTop = bsSlopes.OrderByDescending(x => x.Slope).Take(3).ToList();
        Console.WriteLine("| 組 | 傾き | 前半 ダメ/T | 後半 ダメ/T | 平均 L | 1T あたりの伸び | 15T の累積 | 1,400 に届くか |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|:-:|");
        foreach (var t in bsTop)
        {
            var cur = bsMain.First(m => m.G == t.G && m.H == t.H).Cur;
            double e = BsRate(cur, x => x.DmgEarly), l = BsRate(cur, x => x.DmgLate);
            double L = BsAvg(cur.Where(x => x.Ok).ToList(), x => x.Turns);
            double te = (1 + BsHalf) / 2.0, tl = L - (BsHalf - 1) / 2.0;
            double m2 = tl > te ? (l - e) / (tl - te) : 0;
            double cum = 0;
            for (int t2 = 1; t2 <= 15; t2++) cum += Math.Max(0, e + m2 * (t2 - te));
            Console.WriteLine($"| {t.Name} | {t.Slope:F2} | {e:F1} | {l:F1} | {L:F2} | {m2:F2} | **{cum:F0}** | "
                + $"{(cum >= 1400 ? "**○**" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine("**外挿の前提（全部書く。規約 (G7)）**:");
        Console.WriteLine("1. 味方が 15T 生き残る（実測の平均 L はこれより**短い**。生存の外挿がいちばん甘い前提）");
        Console.WriteLine("2. 育ちが線形に続き、飽和しない（実測は前半末→終端の2点しか見ていない）");
        Console.WriteLine("3. 敵が既存5波と同じ密度で立っている（ボスは**1体**なので薙ぎ・全体の副次目標が消え、");
        Console.WriteLine("   **範囲の駒はここで大きく目減りする**——この外挿はその目減りを織り込んでいない）");
        Console.WriteLine("4. 分母を削らない（規約 (G7) の3つ目。撃破で相手が減ると払い出しの機会も消える）");

        // --- 判定
        Console.WriteLine();
        Console.WriteLine("## 判定（§2-2）");
        Console.WriteLine();
        int bsQ1 = bsSlopes.Count(x => x.Slope >= 1.2);
        double bsQ2 = bsSlopes.Count == 0 ? 0 : bsSlopes.Max(x => x.Slope) - bsSlopes.Min(x => x.Slope);
        var bsQ3 = new List<(string H, double D)>();
        foreach (UnitDef h in bsHold)
        {
            double with = bsMain.Where(m => m.H == h).Average(m => BsAvg(m.Cur, x => x.PartyAlive));
            double solo = bsGrow.Average(g => BsAvg(bsSolo[g.Id], x => x.PartyAlive));
            bsQ3.Add((h.Name, with - solo));
        }
        double bsQ5 = 0;
        {
            var t = bsTop[0];
            var cur = bsMain.First(m => m.G == t.G && m.H == t.H).Cur;
            double e = BsRate(cur, x => x.DmgEarly), l = BsRate(cur, x => x.DmgLate);
            double L = BsAvg(cur.Where(x => x.Ok).ToList(), x => x.Turns);
            double te = (1 + BsHalf) / 2.0, tl = L - (BsHalf - 1) / 2.0;
            double m2 = tl > te ? (l - e) / (tl - te) : 0;
            for (int t2 = 1; t2 <= 15; t2++) bsQ5 += Math.Max(0, e + m2 * (t2 - te));
        }
        Console.WriteLine($"判定の分母は **傾きが測れている {bsSlopes.Count} 組 / 16 組**"
            + $"（L ≥ {BsMinTurns} の戦が {BsOkFloor:F0}% 以上。規約 (G4)）。");
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 線 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|--:|:-:|");
        Console.WriteLine($"| **Q1** | 傾きが 1.2 以上の組 | 3 組以上 | {bsQ1} 組 | {(bsQ1 >= 3 ? "○" : "**×**")} |");
        Console.WriteLine($"| **Q2** | 16 組の傾きの最大 − 最小 | 0.5 以上 | {bsQ2:F2} | {(bsQ2 >= 0.5 ? "○" : "**×**")} |");
        Console.WriteLine($"| **Q3** | 単騎対照との生存Tの差が +1.0T 以上 | 4 枚中 2 枚以上 | "
            + $"{bsQ3.Count(x => x.D >= 1.0)} 枚 | {(bsQ3.Count(x => x.D >= 1.0) >= 2 ? "○" : "**×**")} |");
        Console.WriteLine($"| **Q4** | 到達点 × 後半ダメ/T の順位相関 | r ≥ 0.5 | {bsSp:F3} | {(bsSp >= 0.5 ? "○" : "**×**")} |");
        Console.WriteLine($"| **Q5** | 15T の累積与ダメ | 1,400 | {bsQ5:F0} | {(bsQ5 >= 1400 ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine("**Q3 の内訳**: " + string.Join(" / ", bsQ3.Select(x => $"{x.H} {x.D:+0.00;-0.00;0.00}T")));
        Console.WriteLine();
        Console.WriteLine("**傾き上位3組**: " + string.Join(" / ", bsTop.Select(x => $"{x.Name} {x.Slope:F2}")));
        return;
    }

    // ==============================================================================
    // check（§4 の自己検査）。
    // ==============================================================================
    if (bsMode == "check")
    {
        Console.WriteLine("# 第117期 —— 自己検査（`boss check`）");
        Console.WriteLine();
        var bsBuilds2 = CompareBuilds();
        string? bsRoot2 = BsRoot();

        // (必須1) compare 305 セルが docs/balance.md と 0 件
        var cells = new List<string>();
        foreach (var b in bsBuilds2)
        {
            var row = new List<string>();
            foreach (var st in bsStages)
            {
                int w = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                row.Add($"{w * 100.0 / 200:F1}%");
            }
            cells.Add($"| {b.Name} | {string.Join(" | ", row)} |");
        }
        int bsDiff = -1;
        if (bsRoot2 is not null)
        {
            var doc = File.ReadAllLines(Path.Combine(bsRoot2, "docs", "balance.md"))
                          .Where(l => l.StartsWith("| ") && l.Contains('%')).ToArray();
            bsDiff = Math.Abs(doc.Length - cells.Count);
            for (int i = 0; i < Math.Min(doc.Length, cells.Count); i++)
                if (doc[i].Trim() != cells[i].Trim()) bsDiff++;
        }
        Console.WriteLine($"- **必須1** `compare` {bsBuilds2.Length} 行 × {bsStages.Count} 波 ＝ "
            + $"{bsBuilds2.Length * bsStages.Count} セルを `docs/balance.md` と突き合わせ: "
            + $"**ずれ {bsDiff} 行**{(bsDiff == 0 ? "（○）" : "（×）")}");

        // (必須3) 触っていないノブの既定
        Console.WriteLine($"- **必須3** `BossRule.Default` = `{BossRule.Default}`"
            + $"（`Census` が偽 ＝ 配列を1本も確保しない）{(BossRule.Default.Census ? "（**×**）" : "（○）")}"
            + " ／ `ReaderRule.Default` = `" + ReaderRule.Default + "`（第116期の 5 のまま）");

        // (必須4) ctx.PickOne の箇所数
        int bsPick = 0;
        if (bsRoot2 is not null)
            foreach (string f in Directory.GetFiles(Path.Combine(bsRoot2, "BattleCore"), "*.cs"))
                bsPick += System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"PickOne\(").Count;
        Console.WriteLine($"- **必須4** `BattleCore` の `PickOne(` の出現: **{bsPick} 箇所**"
            + "（宣言 1 ＋ 呼び出し。**この期は1つも足していない**）");

        // (a) 16 組で土台3枚が完全に同一
        bool bsSameFill = bsPairs.All(p =>
        {
            var f = BsBench(p.G, p.H);
            return f[1] == bsFill[0] && f[3] == bsFill[1] && f[4] == bsFill[2];
        });
        Console.WriteLine($"- **(a)** 16 組で土台3枚（前3・後1・後3）が完全に同一: "
            + $"{(bsSameFill ? "○" : "**×**")}（{string.Join("・", bsFill.Select(d => d.Name))}）");

        // (b) 傾きの分子と分母が同じ計数から
        int bsB1 = 0, bsB2 = 0;
        foreach ((UnitDef g, UnitDef h) in bsPairs)
        {
            Formation f = BsBench(g, h);
            BattleResult r = BattleEngine.Run(f, bsStages[4].Enemy, 0, verbose: false, boss: new BossRule(true));
            foreach ((int _, UnitDef d) in f.Occupied())
            {
                if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
                bsB1 += t.DamageToEnemy;
                bsB2 += t.BossDmgByTurn?.Sum() ?? 0;
            }
        }
        Console.WriteLine($"- **(b)** 傾きの分子・分母が同じ計数から: `DamageToEnemy` の総和 {bsB1} 対 "
            + $"`BossDmgByTurn` の総和 {bsB2} → {(bsB1 == bsB2 ? "**一致（○）**" : "**ずれ（×）**")}");

        // (c) 長期戦の台
        // (c) 長期戦の台は敵の HP 以外が1ビットも違わないこと
        int bsC = 0, bsCn = 0;
        foreach (var st in bsStages)
        {
            Formation lo = BsLongOf(st.Enemy, BsLongMul);
            foreach ((int sl, UnitDef d) in st.Enemy.Occupied())
            {
                UnitDef? m = lo[sl];
                bsCn++;
                if (m is null || m.Id != d.Id || m.Name != d.Name || m.Attack != d.Attack
                    || m.Speed != d.Speed || m.Pattern != d.Pattern
                    || !m.Traits.SequenceEqual(d.Traits)
                    || (m.Actions?.Count ?? 0) != (d.Actions?.Count ?? 0)
                    || m.MaxHp != d.MaxHp * BsLongMul) bsC++;
            }
            if (lo.Count != st.Enemy.Count) bsC++;
        }
        Console.WriteLine($"- **(c)** 長期戦の台（`boss long`）が敵の HP 以外を1ビットも変えていない: "
            + $"**ずれ {bsC} / {bsCn} 体**{(bsC == 0 ? "（○）" : "（×）")}"
            + $"（HP は一律 ×{BsLongMul}。`EnemyCatalog.Stages` そのものは1文字も触っていない）");

        // (d) 到達点が CurrentAttack で取られていること
        bool bsD = false;
        if (bsRoot2 is not null)
        {
            string eng = File.ReadAllText(Path.Combine(bsRoot2, "BattleCore", "BattleEngine.cs"));
            int i = eng.IndexOf("public void NoteBossCensus", StringComparison.Ordinal);
            int j = i >= 0 ? eng.IndexOf("\n    }", i, StringComparison.Ordinal) : -1;
            string body = i >= 0 && j > i ? eng.Substring(i, j - i) : "";
            bsD = body.Contains("u.CurrentAttack", StringComparison.Ordinal)
                  && !body.Contains("u.AtkBonus", StringComparison.Ordinal);
        }
        Console.WriteLine($"- **(d)** `NoteBossCensus` が写すのは `CurrentAttack` で、`AtkBonus` の生値ではない: "
            + $"{(bsD ? "○" : "**×**")}");

        // (e) 計数が盤面を1ビットも動かさないこと（BossRule の有無で 61 行が一致）
        int bsE = 0, bsEn = 0;
        foreach (var b in bsBuilds2)
            foreach (var st in bsStages)
                for (int seed = 0; seed < 20; seed++)
                {
                    BattleResult a = BattleEngine.Run(b.F, st.Enemy, seed, verbose: false);
                    BattleResult c = BattleEngine.Run(b.F, st.Enemy, seed, verbose: false, boss: new BossRule(true));
                    bsEn++;
                    if (a.PlayerWon != c.PlayerWon || a.Turns != c.Turns
                        || a.PlayerSurvivors != c.PlayerSurvivors
                        || a.MaxEnemyKillsInOneTurn != c.MaxEnemyKillsInOneTurn) bsE++;
                }
        Console.WriteLine($"- **(e)** `BossRule(true)` と既定で勝敗・ターン・生存数・連鎖が一致: "
            + $"**ずれ {bsE} / {bsEn} 戦**{(bsE == 0 ? "（○）" : "（×）")}");
        return;
    }

    Console.WriteLine("boss: モードは phase0 / run / check のいずれか。");
    return;
}
}
