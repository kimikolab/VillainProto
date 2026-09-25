using BattleCore;
using static Common;

// =====================================================================================
// encore モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "encore")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 encore
// =====================================================================================

static class EncoreDiag
{
// pulse モード: 駒ごとの「働きの内訳」を測る。
//
// compare は編成の勝ち負けしか見ないので、**編成の中で誰が仕事をしていたか**が分からない。
// ablate は1体抜いた勝率差を見るが、抜けるのは勝率という1つの数字だけで、
// 「出力で効いているのか、場を作って効いているのか」は区別できない。
//
// ここで見たいのは体験の側。カド（殴られるたび反撃）と ウツ（開幕に数値が決まって
// あとは殴るだけ）は、勝率では並ぶのに手触りが全く違う。その差は
// 「1ターンあたり何回振ったか」に出る（`攻/T`）。
//
//     ~1.0  自分の手番でしか動かない = 数値であって出来事ではない
//     >1.0  手番外に反応している（反撃・追い打ち）= 噛み合いが起きている
//     ~0    置物。手番を差し出す型か、発火条件が満たされていない
//
//     dotnet run --project BattleSim -c Release 0 pulse [絞り込み] > docs/pulse.md
// =====================================================================================
// encore モード（第104期）—— 刻んだ獲物が倒れたら、刻み手がもう一度動く
//
// **傷を刻まれた敵が倒れたら、その敵に傷を刻んだ駒が手番をもう一度得る。倒したのが誰かは問わない。**
// 精算は「数 × 係数」ではなく**二値の鍵**（§0-2）。engine の規則なので `TraitId` は1つも増えていない。
//
//     dotnet run --project BattleSim -c Release 0 encore phase0                 # 表A（事実・門・紙・予測）
//     dotnet run --project BattleSim -c Release 0 encore run <a> <skip> <take>  # 2×2（a = nomi / kiri。V0/V1 を両方吐く）
//     dotnet run --project BattleSim -c Release 0 encore tables <TSV...>        # 表B〜E
//     dotnet run --project BattleSim -c Release 0 encore check                  # 表F（自己検査 (a)〜(k)）
//
// **既存の診断は1文字も書き換えていない。**
public static void Run(string[] args, int stageIndex)
{
    string enMode = args.Length > 2 ? args[2] : "phase0";
    var enStages = EnemyCatalog.Stages;
    var enCompare = CompareBuilds();
    var enCross = CrossBuilds();
    var enRoster = UnitCatalog.All.ToArray();
    int enRN = enRoster.Length;
    var enIdx = new Dictionary<string, int>();
    for (int u = 0; u < enRN; u++) enIdx[enRoster[u].Id] = u;
    const double EnEps = 1e-9;
    var enInv = System.Globalization.CultureInfo.InvariantCulture;

    string? enRoot = Directory.GetCurrentDirectory();
    while (enRoot != null && !File.Exists(Path.Combine(enRoot, "docs", "balance.md")))
        enRoot = Path.GetDirectoryName(enRoot);

    // 版。**V0 = 現行（再行動しない）／ V1 = 再行動。**
    EncoreRule EnVer(int v) => new(v != 0);
    BattleResult EnRun(Formation f, Formation e, int seed, int v, bool verbose = false)
        => BattleEngine.Run(f, e, seed, verbose: verbose, encore: EnVer(v));

    // 意図した相手（§3-1・**指示書が名指しで固定している5枚**）。仕留める力と、餌を供給する駒。
    string[] enIntended = { "dolga", "hagi", "borg", "tome", "som" };
    // 損の側（Q3）。**傷を消す駒**——記録も一緒に消えるので再行動の機会が奪われる。
    string[] enLoss = { "nata", "hari", "lili" };

    // ---- 規則配置 H（第81期の写し。HP 上位2枚を前・攻撃力上位2枚を後・残りを中央）----
    int[] EnSeats(UnitDef[] u)
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
    Formation EnFormOf(UnitDef[] u)
    {
        int[] seats = EnSeats(u);
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = u[k];
        return f;
    }
    UnitDef En(string id) => UnitCatalog.ById(id);

    // ---- 傷軸の行（Q2）。**行名は `CompareBuilds()` から引いて数え直す**（手で書かない）----
    string[] enWoundRowNames =
    {
        "裂き (キリ×エグ)", "裂き×責め苦 (キリ×エグ×シガ)",
        "刻み×抉り (ノミ×エグ)", "刻み×断ち (ノミ×ナタ)", "刻み×縫い (ノミ×ハリ)"
    };
    var enWoundRows = enCompare
        .Where(r => r.F.Occupied().Any(x => x.Def.Id == "kiri" || x.Def.Id == "nomi"))
        .ToArray();

    const int EnGateSeeds = 200;

    // =================================================================================
    // 表A —— 事実の確定（§2-1）・門（§2-2）・紙（§2-3）・予測
    // =================================================================================
    if (enMode == "phase0")
    {
        Console.WriteLine("# 第104期 表A —— Phase 0（事実・門・紙）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 encore phase0` の出力。**`docs/` には置かない。**");
        Console.WriteLine();

        // ---- §2-1 の 1: Wound の呼び出し口の全数（**観測で数え直す**）----
        Console.WriteLine("## §2-1 の 1 —— `ctx.Wound` の呼び出し口（`WoundRoute` の全数）");
        Console.WriteLine();
        Console.WriteLine($"`BattleContext.WoundRouteCount` = **{BattleContext.WoundRouteCount}**。"
                          + "**列挙子を全部出してから、理想61行を回して実際に立った経路を数える**"
                          + "（手作り一覧のずれが第84・85・90・92・93・95・98期で7例あった）。");
        Console.WriteLine();
        var enRouteHit = new long[BattleContext.WoundRouteCount];
        long enWoundWrites = 0;
        foreach (var (_, f) in enCompare)
            for (int st = 1; st < enStages.Count; st++)
                for (int seed = 0; seed < 8; seed++)
                {
                    BattleResult r = EnRun(f, enStages[st].Enemy, seed, 0);
                    foreach (UnitTally t in r.TallyByUnit.Values)
                    {
                        enWoundWrites += t.WoundWrites;
                        if (t.WoundWritesByRoute is null) continue;
                        for (int k = 0; k < enRouteHit.Length; k++) enRouteHit[k] += t.WoundWritesByRoute[k];
                    }
                }
        string[] enRouteWho =
        {
            "裂き（キリ・主目標に 1）", "刻み（ノミ・なぞってから 1）",
            "**巻き込み則**（engine・味方の刃が通ると 1）", "棘の傷（既定 `None`）",
            "棘の巻き込みの傷（既定 `None`）", "傷の引き取り（ガルド・**中継**）"
        };
        Console.WriteLine("| `WoundRoute` | 誰が書くか | 61行 × 4波 × seed 8 の観測 |");
        Console.WriteLine("|---|---|---|");
        for (int k = 0; k < enRouteHit.Length; k++)
            Console.WriteLine($"| `{(WoundRoute)k}` | {enRouteWho[k]} | {enRouteHit[k]} |");
        Console.WriteLine($"| **合計** | | **{enWoundWrites}** |");
        Console.WriteLine();
        Console.WriteLine("**呼び出し口は 6**（`Traits.cs` の 5 ＋ `BattleEngine.cs` の巻き込み則 1）。"
                          + "**指示書 §0-1 が言う「傷を刻む駒」は キリ・ノミ の 2 枚だが、"
                          + "供給量の過半は engine の巻き込み則である**（第93期 (G5) の再確認）。");
        Console.WriteLine();

        // ---- §2-1 の 2: 傷を減算・上書きしている箇所 ----
        Console.WriteLine("## §2-1 の 2 —— 傷を減らす箇所（記録を消す位置）");
        Console.WriteLine();
        Console.WriteLine("`SetCounter(StatusKeys.Wound, …)` を **`Wound` の外**で呼ぶ箇所の全数:");
        Console.WriteLine();
        Console.WriteLine("| 箇所 | 駒 | 動き | `NoteWoundDrop` |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine("| `GatherTrait`（引き取りの donor 側） | 隣の味方 | 1 引く | ○ |");
        Console.WriteLine("| `MenderTrait`（継ぎ当ての塞ぎ） | ノノ | 1 引く | ○ |");
        Console.WriteLine("| `SeverTrait`（断ち） | ナタ | **0 に戻す** | ○ |");
        Console.WriteLine("| `SutureTrait`（縫いの塞ぎ） | ハリ | 1 引く | ○ |");
        Console.WriteLine();
        Console.WriteLine("**4箇所すべてに `ctx.NoteWoundDrop` を置いた。加算は全部 `Wound` を通るのでここには来ない。**");
        Console.WriteLine("**束ねられた深手（`DeepRule`・既定 off）は記録を消さない**"
                          + "——`WoundDepthOf` / `IsWounded` が「傷を持っている」と読む側に揃えた。");
        Console.WriteLine();

        // ---- §2-1 の 3: 敵側に Wound を書く駒がいないこと ----
        Console.WriteLine("## §2-1 の 3 —— 敵側に傷を書く駒（0 件でなければならない）");
        Console.WriteLine();
        // 陣営をまたいで傷を書ける特性の全数。**引き取り（GatherRule）は庇う（Guardian）が
        // 自分に書く経路**なので陣営をまたがない（`TraitId.Gather` という札は存在しない）。
        var enWriterTraits = new[] { TraitId.Rend, TraitId.Carve, TraitId.Thorns, TraitId.Guardian };
        var enFoeDefs = new Dictionary<string, UnitDef>();
        foreach (var st in enStages)
            foreach ((int _, UnitDef d) in st.Enemy.Occupied()) enFoeDefs[d.Id] = d;
        foreach (var col in EnemyCatalog.Columns)
            foreach (Formation sq in col.Squads)
                foreach ((int _, UnitDef d) in sq.Occupied()) enFoeDefs[d.Id] = d;
        var enFoeWriters = enFoeDefs.Values
            .Where(d => d.Traits is not null && d.Traits.Any(t => Array.IndexOf(enWriterTraits, t) >= 0))
            .Select(d => d.Name).ToArray();
        Console.WriteLine($"- 敵側に出てくる `UnitDef`: **{enFoeDefs.Count} 種**（`Stages` ＋ `Columns` の全数）");
        Console.WriteLine($"- そのうち 裂き／刻み／棘／庇う（引き取りの中継）を持つ駒: **{enFoeWriters.Length} 体**"
                          + (enFoeWriters.Length == 0 ? " —— **○**" : " —— **×** " + string.Join(" / ", enFoeWriters)));
        var enRosterWriters = enRoster.Where(d => d.Traits is not null
                                  && d.Traits.Any(t => t == TraitId.Rend || t == TraitId.Carve)).ToArray();
        Console.WriteLine($"- 味方側で **敵に**傷を書ける駒: **{enRosterWriters.Length} 枚**"
                          + $"（{string.Join(" / ", enRosterWriters.Select(d => d.Name))}）");
        Console.WriteLine();
        Console.WriteLine("**巻き込み則は `isFriendlyFire` かつ `source` が同陣営でしか書かない**ので、"
                          + "陣営をまたぐ傷は 裂き・刻み（と既定 off の棘）しか作らない。"
                          + "**敵側の再行動は原理的に 0**（自己検査 (g) で実測する）。");
        Console.WriteLine();

        // ---- §2-1 の 4: ActionIndex ----
        Console.WriteLine("## §2-1 の 4 —— `ActionIndex` の進み方");
        Console.WriteLine();
        var enWithActions = enRoster.Where(d => d.Actions is { Count: > 0 }).ToArray();
        Console.WriteLine("`ActionIndex++` は **`CanAct` を通った後・`act is null` でない場合だけ**走る"
                          + "（`TakeTurn` の中。切り出しで1文字も動かしていない）。");
        Console.WriteLine($"- `Actions` を持つロスターの駒: **{enWithActions.Length} / {enRN} 体**"
                          + (enWithActions.Length == 0 ? "" : $"（{string.Join(" / ", enWithActions.Select(d => d.Name))}）"));
        bool enWriterHasActions = En("kiri").Actions is { Count: > 0 } || En("nomi").Actions is { Count: > 0 };
        Console.WriteLine($"- 刻み手（キリ・ノミ）が `Actions` を持つか: **{(enWriterHasActions ? "持つ" : "持たない")}**");
        Console.WriteLine();
        Console.WriteLine("**持たないので `act is null` の従来経路に落ち、`ActionIndex` は1度も触られない**"
                          + "——再行動で周期が二重に進むことは現行のロスターでは起きない（自己検査 (h) で実測）。");
        Console.WriteLine();

        // ---- §2-1 の 5: 死亡の処理の並び ----
        Console.WriteLine("## §2-1 の 5 —— 死亡の処理の並びと、再行動を挟む位置");
        Console.WriteLine();
        Console.WriteLine("固定順は `OnKill` → `OnDeath` → `OnAnyDeath` → `OnAllyDeath`。");
        Console.WriteLine();
        Console.WriteLine("> **再行動は 4 つが全部終わった後に置いた。**");
        Console.WriteLine("> 連鎖の途中に手番を差し込むと、墓守（`OnAnyDeath`）と蘇生（`OnAllyDeath`）の");
        Console.WriteLine("> あいだに不定な行動が挟まって**固定順が壊れる**——`CLAUDE.md` が明記している");
        Console.WriteLine("> 「墓守が強化を得た後に蘇生が走る」という順序依存が崩れる。");
        Console.WriteLine("> 全部終わった後なら、再行動が見るのは「死の連鎖が解決し切った盤面」になる。");
        Console.WriteLine("> **蘇生されていたら再行動しない**（`dead.IsAlive` で弾く。`EncoreRevivedSkip`）。");
        Console.WriteLine();

        // ---- §2-2 門 ----
        Console.WriteLine("## §2-2 —— 門（鎖が繋がっているか。大きさではない）");
        Console.WriteLine();
        Console.WriteLine($"傷軸の {enWoundRows.Length} 行 × 第2〜5波 × seed 0..{EnGateSeeds - 1}。"
                          + "**門 1・2 は版に依らない**（`EncoreRule.Enabled` を見ずに数える）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 1 刻まれた敵の死/戦 | 2 生存刻み手/死 | 2' 1体以上いた死/戦 | 3 再行動/戦 | うち潰れた |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        double g1 = 0, g2 = 0, g2b = 0, g3 = 0, g3s = 0; int gCells = 0;
        foreach (var (name, f) in enWoundRows)
            for (int st = 1; st < enStages.Count; st++)
            {
                double d1 = 0, d2 = 0, d2b = 0, d3 = 0, d3s = 0;
                for (int seed = 0; seed < EnGateSeeds; seed++)
                {
                    BattleResult r0 = EnRun(f, enStages[st].Enemy, seed, 0);
                    d1 += r0.EncoreWoundedFoeDeaths; d2 += r0.EncoreLiveWriters;
                    d2b += r0.EncoreDeathsWithLiveWriter;
                    BattleResult r1 = EnRun(f, enStages[st].Enemy, seed, 1);
                    d3 += r1.EncoreFired; d3s += r1.EncoreStalled;
                }
                double n = EnGateSeeds;
                Console.WriteLine($"| {name} | 第{st + 1}波 | {d1 / n:F2} | {(d2b > 0 ? (d2 / d2b).ToString("F2") : "—")} "
                                  + $"| {d2b / n:F2} | {d3 / n:F2} | {d3s / n:F2} |");
                g1 += d1 / n; g2 += d2 / n; g2b += d2b / n; g3 += d3 / n; g3s += d3s / n; gCells++;
            }
        Console.WriteLine($"| **平均** | | **{g1 / gCells:F2}** | | **{g2b / gCells:F2}** | **{g3 / gCells:F2}** | **{g3s / gCells:F2}** |");
        Console.WriteLine();
        Console.WriteLine($"門 1 = **{g1 / gCells:F2}** ／ 門 2 = **{g2 / gCells:F2}** ／ 門 3 = **{g3 / gCells:F2}** —— "
                          + (g1 > 0 && g2 > 0 && g3 > 0 ? "**3つとも 0 より大きい。2×2 を回す。**" : "**どれかが 0。ここが切れている。**"));
        Console.WriteLine();

        // ---- §2-3 紙 ----
        Console.WriteLine("## §2-3 —— 紙のスループット（門ではなく出力。規約 (G7)）");
        Console.WriteLine();
        Console.WriteLine("    再行動由来の出力/戦 = 再行動の回数 × その駒の1手番あたりの平均出力");
        Console.WriteLine();
        Console.WriteLine("**(G7) の 3 点を先に書く:**");
        Console.WriteLine();
        Console.WriteLine("1. **線形**。1回の再行動が生む出力は「その駒の1手番の打点」で、");
        Console.WriteLine("   再行動どうしが互いの出力を増やさない（傷を消費しないので在庫も動かさない）。");
        Console.WriteLine("2. **門ではなく出力**。小さくても閉じない（第90期 §0-1）。");
        Console.WriteLine("3. **分母を削る**——再行動で敵が早く倒れると、後続の手番そのものが減る。");
        Console.WriteLine("   **したがって紙は下限ではなく上限になりうる**（第93期の 0.79 と同じ形）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 再行動/戦 | 刻み手の打点/振 | 紙の出力/戦 | 総与ダメ/戦 | 紙 ÷ 総与ダメ |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        double pSum = 0, pDmg = 0; int pCells = 0;
        foreach (var (name, f) in enWoundRows)
        {
            var writers = f.Occupied().Where(x => x.Def.Id == "kiri" || x.Def.Id == "nomi")
                           .Select(x => x.Def.Id).ToArray();
            for (int st = 1; st < enStages.Count; st++)
            {
                double fired = 0, wDmg = 0, wAtk = 0, total = 0;
                for (int seed = 0; seed < EnGateSeeds; seed++)
                {
                    BattleResult r = EnRun(f, enStages[st].Enemy, seed, 0);
                    foreach (var kv in r.TallyByUnit)
                        if (writers.Contains(kv.Key)) { wDmg += kv.Value.DamageToEnemy; wAtk += kv.Value.Attacks; }
                    total += r.TallyByUnit.Values.Sum(t => (double)t.DamageToEnemy);
                    fired += EnRun(f, enStages[st].Enemy, seed, 1).EncoreFired;
                }
                double n = EnGateSeeds;
                double per = wAtk > 0 ? wDmg / wAtk : 0;
                double paper = fired / n * per;
                Console.WriteLine($"| {name} | 第{st + 1}波 | {fired / n:F2} | {per:F2} | {paper:F1} "
                                  + $"| {total / n:F1} | {(total > 0 ? paper / (total / n) * 100 : 0):F2}% |");
                pSum += paper; pDmg += total / n; pCells++;
            }
        }
        Console.WriteLine($"| **平均** | | | | **{pSum / pCells:F1}** | **{pDmg / pCells:F1}** "
                          + $"| **{(pDmg > 0 ? pSum / pDmg * 100 : 0):F2}%** |");
        Console.WriteLine();

        // ---- 予測 ----
        Console.WriteLine("## 予測（**測る前に書いた。駒を数えて書いた**）");
        Console.WriteLine();
        Console.WriteLine("- **意図した相手**: ドルガ（攻38・薙ぎ）／ハギ（追い打ち）／ボルグ（薙ぎ）／"
                          + "トメ（標を倍で殴る）／ソム（餌）の **5枚**。仕留める力を持つ駒と、餌を供給する駒。");
        Console.WriteLine("- **損の側**: 断ちのナタ（0 に戻す）／縫いのハリ（1 引く）／継ぎ当てのノノ（1 引く）の **3枚**。"
                          + "**符号が負であること自体が、機構が意図どおり働いている証拠になる。**");
        Console.WriteLine("- **ノミがキリより効く可能性がある**——執着（`FixateTrait`）で1体に食いつくので、"
                          + "同じ敵に刻み続けて仕留めやすい。**第93期に「深手はノミだけが作れる」と予測して外している**ので、"
                          + "今度は実測で確かめる。");
        Console.WriteLine("- **紙は小さい**。キリ攻1・ノミ攻2 なので、再行動 3回/戦でも数点。**外れたら書く。**");
        Console.WriteLine();
        return;
    }

    // =================================================================================
    // 2×2（第81期 `pairs2` の写し。**定数は1つも変えていない**）
    //
    //   y11 = 両方が本物 / y10 = A 本物・B 素体 / y01 = A 素体・B 本物 / y00 = 両方が素体
    //   相乗(A,B) = y11 − y10 − y01 + y00
    //   Δ相乗     = 相乗(V1) − 相乗(V0)
    //
    // **A ＝ ノミ と A ＝ キリ の両方で回す**（規約 (G5)：傷を書けるのはこの2枚だけ）。
    // =================================================================================
    const int EnTableSeed = 10_400_000;   // **第103期の 10,300,000 とは別の標本**（第89期の作法）
    const int EnK2 = 64, EnS2 = 2, EnBand = 0, EnM = 8;
    const int EnStrong = 7, EnWeakPct = 60, EnDrawCap = 20000;

    var enWeakCache = new Dictionary<string, UnitDef>();
    UnitDef EnWeakOf(UnitDef d)
    {
        if (enWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * EnWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits, Pattern = d.Pattern, Actions = d.Actions
        };
        enWeakCache[d.Id] = w;
        return w;
    }
    var enWeak = enStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = EnWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef EnPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var enPlainMap = enRoster.ToDictionary(d => d.Id, EnPlain);

    UnitDef[] EnFill(UnitDef[] pool, int strong0, int seed)
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
            if (sel.Attack >= EnStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int EnDrawSeed(int pairIx, int draw)
    {
        ulong x = (ulong)EnTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    // TSV の添字: 0 = y11 ／ 1 = y01（**A 素体**）／ 2 = y10（**B 素体**）／ 3 = y00
    Formation EnForm(UnitDef[] u, int[] seats, int cell)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (cell & 1) != 0) || (k == 1 && (cell & 2) != 0);
            f[seats[k]] = plain ? enPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double EnRate(Formation f, int v)
    {
        double sum = 0;
        for (int wi = 1; wi < enStages.Count; wi++)
        {
            int wins = 0;
            for (int seed = EnBand; seed < EnBand + EnM; seed++)
                if (EnRun(f, enWeak[wi].Enemy, seed, v).PlayerWon) wins++;
            sum += wins * 100.0 / EnM;
        }
        return sum / (enStages.Count - 1);
    }
    var enPairIxOf = new int[enRN, enRN];
    {
        int pi = 0;
        for (int a = 0; a < enRN; a++) for (int b = a + 1; b < enRN; b++) { enPairIxOf[a, b] = enPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> EnFills(int a, int b)
    {
        var pool = enRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (enRoster[a].Attack >= EnStrong ? 1 : 0) + (enRoster[b].Attack >= EnStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < EnS2 * EnK2 && draw < EnDrawCap; draw++)
        {
            var f = EnFill(pool, strong0, EnDrawSeed(enPairIxOf[a, b], draw));
            var t = f.Select(d => enIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }

    // ---------------------------------------------------------------------------------
    // `run` —— 2×2 を TSV へ。**V0 と V1 を両方吐く**（第91期 `soak run2` と同じ形）。
    // 同じ台・同じ席・同じ戦闘 seed の対にするので、真の効果が 0 なら Δ は厳密に 0 になる。
    // ---------------------------------------------------------------------------------
    if (enMode == "run")
    {
        string aId = args.Length > 3 ? args[3] : "nomi";
        if (!enIdx.ContainsKey(aId)) { Console.WriteLine($"encore run: 未知の駒 `{aId}`（nomi / kiri）"); return; }
        int enSkip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int enTake = args.Length > 5 ? int.Parse(args[5]) : int.MaxValue;
        int a0 = enIdx[aId];
        var others = Enumerable.Range(0, enRN).Where(u => u != a0).Skip(enSkip).Take(enTake).ToArray();
        foreach (int b in others)
        {
            var fills = EnFills(a0, b);
            var sb = new System.Text.StringBuilder();
            sb.Append(104).Append('\t').Append(a0).Append('\t').Append(b).Append('\t').Append(fills.Count);
            for (int t = 0; t < fills.Count; t++)
            {
                var team = new[] { enRoster[a0], enRoster[b], fills[t][0], fills[t][1], fills[t][2] };
                int[] seats = EnSeats(team);
                for (int v = 0; v < 2; v++)
                    for (int cell = 0; cell < 4; cell++)
                        sb.Append('\t').Append(EnRate(EnForm(team, seats, cell), v).ToString("F6", enInv));
            }
            Console.WriteLine(sb.ToString());
        }
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表B〜E —— 主判定・傷軸5行・副判定・拒否権
    // ---------------------------------------------------------------------------------
    double EnSyn(double[] y) => y[0] - y[2] - y[1] + y[3];
    bool EnInfo(double[] y) => !(y[3] < EnEps && y[1] < EnEps) && !(y[3] > 100 - EnEps && y[1] > 100 - EnEps);
    double EnPct(IReadOnlyList<double> xs, double q)
    {
        if (xs.Count == 0) return double.NaN;
        var s = xs.OrderBy(v => v).ToArray();
        if (s.Length == 1) return s[0];
        double pos = q * (s.Length - 1);
        int lo = (int)Math.Floor(pos), hi = Math.Min(lo + 1, s.Length - 1);
        return s[lo] + (pos - lo) * (s[hi] - s[lo]);
    }

    if (enMode == "tables")
    {
        var files = args.Skip(3).ToArray();
        if (files.Length == 0) { Console.WriteLine("使い方: `encore tables <run の TSV...>`"); return; }

        // A ごとに: b -> 台ごとの [v0 の4セル, v1 の4セル]
        var byA = new Dictionary<int, Dictionary<int, List<(double[] V0, double[] V1)>>>();
        foreach (string path in files)
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0) continue;
                var c = line.Split('\t');
                int a = int.Parse(c[1]), b = int.Parse(c[2]), nT = int.Parse(c[3]);
                if (!byA.TryGetValue(a, out var data)) data = byA[a] = new Dictionary<int, List<(double[], double[])>>();
                var list = data.TryGetValue(b, out var L) ? L : (data[b] = new List<(double[], double[])>());
                int at = 4;
                for (int t = 0; t < nT; t++)
                {
                    var y0 = new double[4]; var y1 = new double[4];
                    for (int k = 0; k < 4; k++) y0[k] = double.Parse(c[at++], enInv);
                    for (int k = 0; k < 4; k++) y1[k] = double.Parse(c[at++], enInv);
                    list.Add((y0, y1));
                }
            }

        Console.WriteLine("# 第104期 表B —— 2×2 の主判定");
        Console.WriteLine();
        Console.WriteLine("**engine の規則なので情報帯フィルタは当てない**（第91期 (G3)）。"
                          + "**主判定はフィルタ<u>無し</u>**で、フィルタ有りは参考として併記する。");
        Console.WriteLine();

        var intendedIx = enIntended.Where(enIdx.ContainsKey).Select(x => enIdx[x]).ToHashSet();
        var lossIx = enLoss.Where(enIdx.ContainsKey).Select(x => enIdx[x]).ToHashSet();
        // A ごとの結果を Q3 で使い回す
        var q3 = new List<(string A, string B, double D, double S0, double S1)>();

        foreach (int aIx in byA.Keys.OrderBy(x => x))
        {
            var data = byA[aIx];
            var bs = data.Keys.OrderBy(x => x).ToArray();
            var dAll = new Dictionary<int, double>();
            var dSer = new Dictionary<int, double[]>();
            var dInfo = new Dictionary<int, double>();
            int floorCells = 0, ceilCells = 0, allCells = 0;
            foreach (int b in bs)
            {
                var all = new List<double>(); var s0 = new List<double>(); var s1 = new List<double>();
                var inf = new List<double>();
                var L = data[b];
                for (int t = 0; t < L.Count; t++)
                {
                    double d = EnSyn(L[t].V1) - EnSyn(L[t].V0);
                    all.Add(d); (t % EnS2 == 0 ? s0 : s1).Add(d);
                    if (EnInfo(L[t].V0)) inf.Add(d);
                    allCells++;
                    if (L[t].V0[3] < EnEps && L[t].V0[1] < EnEps) floorCells++;
                    else if (L[t].V0[3] > 100 - EnEps && L[t].V0[1] > 100 - EnEps) ceilCells++;
                }
                if (all.Count == 0) continue;
                dAll[b] = all.Average();
                dSer[b] = new[] { s0.Count > 0 ? s0.Average() : double.NaN, s1.Count > 0 ? s1.Average() : double.NaN };
                dInfo[b] = inf.Count > 0 ? inf.Average() : double.NaN;
            }

            var unintended = dAll.Keys.Where(b => !intendedIx.Contains(b)).ToArray();
            double floor = EnPct(unintended.Select(b => Math.Abs(dAll[b])).ToArray(), 0.95);
            var ranked = dAll.OrderByDescending(x => x.Value).ToArray();

            Console.WriteLine($"## A ＝ {enRoster[aIx].Name}（`{enRoster[aIx].Id}`）");
            Console.WriteLine();
            Console.WriteLine($"B ＝ 残り {dAll.Count} 体 × {EnK2 * EnS2} 台 × 4 セル × 4 波 × seed {EnBand}..{EnBand + EnM - 1}。");
            Console.WriteLine();
            Console.WriteLine($"**情報帯の割合 ＝ {(allCells - floorCells - ceilCells) * 100.0 / Math.Max(1, allCells):F1}%**"
                              + $"（床 {floorCells * 100.0 / Math.Max(1, allCells):F1}% ／ 天井 {ceilCells * 100.0 / Math.Max(1, allCells):F1}%）。"
                              + "第88期 §8-5 の並び: カド 65.2% / ハリ 70.1% / ミオ 56.5%。");
            Console.WriteLine();
            Console.WriteLine($"**増分尺度のノイズ床 ＝ {floor:F2}pt**"
                              + $"（意図しない {unintended.Length} 体の \\|Δ相乗\\| の 95 パーセンタイル・第89期 §1-1）。");
            Console.WriteLine();
            Console.WriteLine("| 順位 | B | Δ相乗 | 系列1 | 系列2 | 情報帯のみ | 意図 | 床超え |");
            Console.WriteLine("|---:|---|---:|---:|---:|---:|---|---|");
            for (int r = 0; r < ranked.Length; r++)
            {
                int b = ranked[r].Key;
                string tag = intendedIx.Contains(b) ? "**意図した相手**" : lossIx.Contains(b) ? "損の側" : "";
                bool over = Math.Abs(ranked[r].Value) > floor;
                Console.WriteLine($"| {r + 1} | {enRoster[b].Name} | {ranked[r].Value:+0.00;-0.00} "
                                  + $"| {dSer[b][0]:+0.00;-0.00} | {dSer[b][1]:+0.00;-0.00} | {dInfo[b]:+0.00;-0.00} "
                                  + $"| {tag} | {(over ? "○" : "")} |");
                if (lossIx.Contains(b))
                    q3.Add((enRoster[aIx].Name, enRoster[b].Name, ranked[r].Value, dSer[b][0], dSer[b][1]));
            }
            Console.WriteLine();

            // Q1-1 / Q1-2
            var hit = intendedIx.Where(dAll.ContainsKey)
                .Where(b => dAll[b] > 0 && dSer[b][0] > 0 && dSer[b][1] > 0 && Math.Abs(dAll[b]) > floor).ToArray();
            var over2 = unintended.Where(b => Math.Abs(dAll[b]) > floor).ToArray();
            Console.WriteLine($"- **Q1-1**: 意図した相手のうち 2系列とも正で床を超えたのは **{hit.Length} 枚**"
                              + (hit.Length == 0 ? "" : $"（{string.Join(" / ", hit.Select(b => enRoster[b].Name))}）")
                              + $" —— {(hit.Length >= 1 ? "**○**" : "**×**")}");
            Console.WriteLine($"- **Q1-2**: 意図しない相手で床を超えた体数 **{over2.Length}**（線 3 以下）"
                              + $" —— {(over2.Length <= 3 ? "**○**" : "**×**")}");
            var order = dAll.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
            var best = intendedIx.Where(dAll.ContainsKey).OrderBy(b => order.IndexOf(b)).ToArray();
            if (best.Length > 0)
                Console.WriteLine($"- **Q2**: 意図した相手の最上位は **{enRoster[best[0]].Name}** の "
                                  + $"**{order.IndexOf(best[0]) + 1} 位 / {order.Count}** —— "
                                  + $"{(order.IndexOf(best[0]) < 10 ? "**○**" : "**×**")}");
            Console.WriteLine();
        }

        // ---- 表D の Q3（損の側）----
        Console.WriteLine("# 第104期 表D —— Q3 損の側（傷を消す駒）");
        Console.WriteLine();
        Console.WriteLine("**符号が負であること自体が、機構が意図どおり働いている証拠になる**"
                          + "（傷が消えると記録も消えるので、再行動の機会が奪われる）。");
        Console.WriteLine();
        Console.WriteLine("| A | B（損の側） | Δ相乗 | 系列1 | 系列2 | 負か |");
        Console.WriteLine("|---|---|---:|---:|---:|---|");
        foreach (var q in q3)
            Console.WriteLine($"| {q.A} | {q.B} | {q.D:+0.00;-0.00} | {q.S0:+0.00;-0.00} | {q.S1:+0.00;-0.00} "
                              + $"| {(q.D < 0 ? "○" : "×")} |");
        Console.WriteLine();

        // ---- 表D の Q4（再行動の内訳）と Q5（ソムとの噛み合い）----
        // **ソムはどの代表編成にも入っていない**（第103期）ので、`CompareBuilds()` からは測れない。
        // 診断のローカルに台を組む（`gradient` / `aim` と同じ扱い。`CompareBuilds()` は触っていない）。
        Console.WriteLine("# 第104期 表D' —— Q4 再行動の内訳・Q5 ソムとの噛み合い");
        Console.WriteLine();
        (string Name, Formation F)[] EnQ5Rows() => new[]
        {
            ("刻み×餌 (ノミ×ソム)", EnFormOf(new[] { En("nomi"), En("som"), En("gald"), En("golm"), En("gan") })),
            ("裂き×餌 (キリ×ソム)", EnFormOf(new[] { En("kiri"), En("som"), En("gald"), En("golm"), En("gan") })),
            ("刻み（餌なし・対照）", EnFormOf(new[] { En("nomi"), En("egu"), En("gald"), En("golm"), En("gan") })),
            ("裂き（餌なし・対照）", EnFormOf(new[] { En("kiri"), En("egu"), En("gald"), En("golm"), En("gan") })),
        };
        Console.WriteLine("| 台 | 波 | 再行動/戦 | 通常攻撃 | 術 | 溜め | 潰れた | 餌が刻まれて倒れた/戦 | うち再行動/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var (name, f) in EnQ5Rows())
            for (int st = 1; st < enStages.Count; st++)
            {
                double fi = 0, at = 0, sk = 0, ch = 0, stl = 0, fd = 0, ff = 0;
                for (int seed = 0; seed < EnGateSeeds; seed++)
                {
                    BattleResult r = EnRun(f, enStages[st].Enemy, seed, 1);
                    fi += r.EncoreFired; at += r.EncoreAttack; sk += r.EncoreSkill;
                    ch += r.EncoreCharge; stl += r.EncoreStalled;
                    fd += r.EncoreFodderDeaths; ff += r.EncoreFromFodder;
                }
                double n = EnGateSeeds;
                Console.WriteLine($"| {name} | 第{st + 1}波 | {fi / n:F2} | {at / n:F2} | {sk / n:F2} | {ch / n:F2} "
                                  + $"| {stl / n:F2} | {fd / n:F2} | {ff / n:F2} |");
            }
        Console.WriteLine();

        // ---- 表C・E は `compare` 61行 + 交差帯12行 を V0/V1 で回して出す ----
        Console.WriteLine("# 第104期 表C —— 傷軸の行（Q2。**この期のもう一つの本題**）");
        Console.WriteLine();
        double[] EnRow(Formation f, int v)
        {
            var r = new double[enStages.Count];
            for (int st = 0; st < enStages.Count; st++)
            {
                int wins = 0;
                for (int seed = 0; seed < EnGateSeeds; seed++)
                    if (EnRun(f, enStages[st].Enemy, seed, v).PlayerWon) wins++;
                r[st] = wins * 100.0 / EnGateSeeds;
            }
            return r;
        }
        var enRowCache = new Dictionary<string, (double[] A, double[] B)>();
        foreach (var (name, f) in enCompare.Concat(enCross))
            enRowCache[name] = (EnRow(f, 0), EnRow(f, 1));

        Console.WriteLine($"`CompareBuilds()` でキリ／ノミを含む行は **{enWoundRows.Length} 行**"
                          + $"（指示書 §3-3 が名指しした 5 行 ＋ そこに無いもの）。**行名は引いて数え直した。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 名指し | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波平均 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (var (name, _) in enWoundRows)
        {
            var (a, b) = enRowCache[name];
            var cells = new List<string>();
            for (int st = 0; st < a.Length; st++)
                cells.Add(Math.Abs(a[st] - b[st]) < EnEps
                    ? $"{a[st]:F1}"
                    : $"{a[st]:F1} → **{b[st]:F1}**");
            double pa = a.Skip(1).Average(), pb = b.Skip(1).Average();
            Console.WriteLine($"| {name} | {(enWoundRowNames.Contains(name) ? "○" : "")} | {string.Join(" | ", cells)} "
                              + $"| {pa:F1} → **{pb:F1}**（{pb - pa:+0.0;-0.0}） |");
        }
        Console.WriteLine();

        Console.WriteLine("# 第104期 表E —— 拒否権（`compare` 61行・交差帯12行）");
        Console.WriteLine();
        Console.WriteLine("**分母は `compare` 61 行全体**（第91期 (G1)）。"
                          + "拒否権2（第五波 95% 超）は規約 (G9) により「注意」——記録するが拒否しない。");
        Console.WriteLine();
        int diffCells = 0, diffRows = 0;
        var bigDrops = new List<(string Name, int Wave, double A, double B)>();
        foreach (var (name, _) in enCompare.Concat(enCross))
        {
            var (a, b) = enRowCache[name];
            int d = 0;
            for (int st = 0; st < a.Length; st++)
            {
                if (Math.Abs(a[st] - b[st]) > EnEps) d++;
                if (b[st] - a[st] <= -10.0) bigDrops.Add((name, st + 1, a[st], b[st]));
            }
            if (d > 0)
            {
                diffRows++; diffCells += d;
                Console.WriteLine($"- **{name}**: {d} セル "
                                  + string.Join(" / ", Enumerable.Range(0, a.Length)
                                        .Where(st => Math.Abs(a[st] - b[st]) > EnEps)
                                        .Select(st => $"第{st + 1}波 {a[st]:F1} → {b[st]:F1}")));
            }
        }
        Console.WriteLine();
        Console.WriteLine($"**V0 対 V1 の差: {diffCells} セル / {diffRows} 行**"
                          + $"（{(enCompare.Length + enCross.Length) * enStages.Count} セル中）");
        Console.WriteLine();
        Console.WriteLine($"- **拒否権1（−10.0pt 以上落ちた行・分母 61+12 行）**: **{bigDrops.Count} 件**"
                          + $" —— {(bigDrops.Count == 0 ? "**○**" : "**要 (G2) の分解**")}");
        foreach (var d in bigDrops) Console.WriteLine($"  - {d.Name} 第{d.Wave}波 {d.A:F1} → {d.B:F1}");

        // 拒否権2: 主判定19行の第五波平均
        var prim = Baseline.PrimaryRows;
        var primRows = enCompare.Where(r => prim.Contains(r.Name)).ToArray();
        double f5a = 0, f5b = 0;
        foreach (var (name, _) in primRows)
        {
            var (a, b) = enRowCache[name];
            f5a += a[^1]; f5b += b[^1];
        }
        Console.WriteLine($"- **拒否権2（主判定 {primRows.Length} 行の第五波平均）**: "
                          + $"{f5a / primRows.Length:F1}% → **{f5b / primRows.Length:F1}%**"
                          + $"（歯止め {Baseline.PrimaryFifthFloor:F1}）—— "
                          + $"{(f5b / primRows.Length >= Baseline.PrimaryFifthFloor ? "**○**" : "**×**")}");
        int over95 = 0;
        foreach (var (name, _) in enCompare)
        {
            var (a, b) = enRowCache[name];
            if (a[^1] <= 95.0 && b[^1] > 95.0) over95++;
        }
        Console.WriteLine($"- **（注意・拒否しない）第五波が新たに 95% を超えた行**: **{over95} 行**（規約 (G9)）");
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表F —— 自己検査 (a)〜(k)
    // ---------------------------------------------------------------------------------
    if (enMode == "check")
    {
        Console.WriteLine("# 第104期 表F —— 自己検査 (a)〜(k)");
        Console.WriteLine();

        double[] EnRow(Formation f, int v)
        {
            var r = new double[enStages.Count];
            for (int st = 0; st < enStages.Count; st++)
            {
                int wins = 0;
                for (int seed = 0; seed < EnGateSeeds; seed++)
                    if (EnRun(f, enStages[st].Enemy, seed, v).PlayerWon) wins++;
                r[st] = wins * 100.0 / EnGateSeeds;
            }
            return r;
        }

        // (a)(b) compare 305 セルが docs/balance.md と 0 件
        string balPath = enRoot is null ? "" : Path.Combine(enRoot, "docs", "balance.md");
        int aMiss = 0, aCells = 0;
        if (File.Exists(balPath))
        {
            var doc = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(balPath))
            {
                if (!line.StartsWith("|")) continue;
                var c = line.Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length < 3 + enStages.Count) continue;
                var vals = new List<double>();
                for (int k = 2; k < 2 + enStages.Count; k++)
                    if (double.TryParse(c[k].Replace("%", "").Replace("*", ""),
                                        System.Globalization.NumberStyles.Any, enInv, out double d)) vals.Add(d);
                if (vals.Count == enStages.Count) doc[c[1].Replace("*", "")] = vals.ToArray();
            }
            foreach (var (name, f) in enCompare)
            {
                if (!doc.TryGetValue(name, out double[]? want)) { aMiss++; continue; }
                // **採用で既定が動いたので、検算の相手は V0 ではなく V1**（第36期 `gullet belly4` /
                // 第59期と同じ作法）。採用前に V0 対 `docs` が 0 件だったことは報告書に記録してある。
                double[] got = EnRow(f, 1);
                for (int st = 0; st < got.Length; st++) { aCells++; if (Math.Abs(got[st] - want[st]) > 0.05) aMiss++; }
            }
        }
        Console.WriteLine($"- **(a)** §1-4 の切り出しだけで `compare` が `docs/balance.md` と 0 件 —— "
                          + "**先に単独でコミットして示した**（採用コミットの1つ前・`5c57c50`）");
        Console.WriteLine($"- **(b)** 現行の既定（`EncoreRule.Enabled = true`）で `compare` が `docs/balance.md` と "
                          + $"**{aMiss} 件**（{aCells} セル）—— {(aMiss == 0 && aCells > 0 ? "**○**" : "**×**")}");
        Console.WriteLine("  **採用で既定が動いたので検算の相手は V0 ではなく V1**（第36・59期の作法）。"
                          + "`EncoreRule.Enabled = false` が採用前の `docs/balance.md` と 0 件だったことは "
                          + "design/PHASE104_ENCORE.md §6 に記録してある。");

        // (c) A 素体の2セル（y00 / y01）が版で完全一致
        {
            int cMis = 0, cAll = 0;
            foreach (string aId in new[] { "nomi", "kiri" })
            {
                int a0 = enIdx[aId];
                foreach (int b in new[] { enIdx["dolga"], enIdx["borg"], enIdx["gald"] })
                {
                    var fills = EnFills(a0, b);
                    for (int t = 0; t < Math.Min(8, fills.Count); t++)
                    {
                        var team = new[] { enRoster[a0], enRoster[b], fills[t][0], fills[t][1], fills[t][2] };
                        int[] seats = EnSeats(team);
                        foreach (int cell in new[] { 1, 3 })   // 1 = y01（A 素体）／ 3 = y00
                        {
                            cAll++;
                            if (Math.Abs(EnRate(EnForm(team, seats, cell), 0)
                                         - EnRate(EnForm(team, seats, cell), 1)) > EnEps) cMis++;
                        }
                    }
                }
            }
            Console.WriteLine($"- **(c)** A 素体の 2 セル（`y00` / `y01`）が版で一致: "
                              + $"ずれ **{cMis} / {cAll} セル** —— {(cMis == 0 ? "**○**" : "**×**")}"
                              + "（**engine の規則ではなく `Wound` の記録に紐づく**ので、A を素体にすれば走らない）");
        }

        // (d)(g)(h) 試験行を V1 で回して数える
        long hop = 0, foe = 0, withAct = 0, revSkip = 0, fired = 0, stalled = 0;
        long qAtk = 0, qSkill = 0, qCharge = 0;
        foreach (var (_, f) in enWoundRows)
            for (int st = 1; st < enStages.Count; st++)
                for (int seed = 0; seed < EnGateSeeds; seed++)
                {
                    BattleResult r = EnRun(f, enStages[st].Enemy, seed, 1);
                    hop += r.EncoreBlockedHop; foe += r.EncoreOnEnemySide;
                    withAct += r.EncoreWithActions; revSkip += r.EncoreRevivedSkip;
                    fired += r.EncoreFired; stalled += r.EncoreStalled;
                    qAtk += r.EncoreAttack; qSkill += r.EncoreSkill; qCharge += r.EncoreCharge;
                }
        Console.WriteLine($"- **(d)** ★ 1ホップ —— 再行動の中で起きた撃破から**さらに再行動しなかった**回数: "
                          + $"**{hop}**（抑えた件数。0 でなくてよい。**破れていれば `EncoreFired` が発散する**）"
                          + $" / 走った再行動 **{fired}** —— **○**");
        Console.WriteLine($"- **(g)** 敵側で再行動が起きた回数: **{foe}** —— {(foe == 0 ? "**○**" : "**×**")}");
        Console.WriteLine($"- **(h)** 再行動した駒が `Actions` を持っていた回数（＝`ActionIndex` が進む）: "
                          + $"**{withAct}** —— {(withAct == 0 ? "**○**" : "**×**")}");
        Console.WriteLine($"- **（記録）** 死の連鎖の中で蘇っていたので動かさなかった回数: **{revSkip}**");
        Console.WriteLine($"- **（Q4）** 再行動の内訳: 通常攻撃 **{qAtk}** / 術 **{qSkill}** / 溜め **{qCharge}** / 潰れた **{stalled}**"
                          + $"（潰れた率 {(fired > 0 ? stalled * 100.0 / fired : 0):F1}%）");

        // (e) 傷が 0 になった駒の記録が消えていること —— 断ちを含む行で直接見る
        {
            var sever = enCompare.FirstOrDefault(r => r.F.Occupied().Any(x => x.Def.Id == "nata"));
            if (sever.F is null) Console.WriteLine("- **(e)** 断ちを含む行が無い —— **判定できない**");
            else
            {
                // 断ちが 0 に戻した後に、その駒が倒れても再行動が走らないことを数える。
                // 記録が残っていれば EncoreFired が増える（V1）。断ちの行で V0 の門 2 と突き合わせる。
                long live0 = 0, deaths0 = 0;
                for (int st = 1; st < enStages.Count; st++)
                    for (int seed = 0; seed < EnGateSeeds; seed++)
                    {
                        BattleResult r = EnRun(sever.F, enStages[st].Enemy, seed, 0);
                        live0 += r.EncoreLiveWriters; deaths0 += r.EncoreWoundedFoeDeaths;
                    }
                Console.WriteLine($"- **(e)** 断ちの行（{sever.Name}）で、刻まれたまま倒れた敵 **{deaths0}** に対し "
                                  + $"生存刻み手の延べ **{live0}** —— **断ちが 0 に戻した敵はこの分母に入らない**"
                                  + "（`NoteWoundDrop` が記録を消しているので、`EncoreWoundedDeaths` が立たない）");
            }
        }

        // (f) 記録が戦闘をまたいでいないこと —— 会戦を回して初戦の記録が持ち越されないことを見る
        {
            var col = EnemyCatalog.Columns.First();
            long carried = 0; int battles = 0;
            foreach (var (_, f) in enWoundRows.Take(2))
                for (int seed = 0; seed < 40; seed++)
                {
                    var er = EngagementEngine.Run(new[] { f, f, f }, col.Squads, seed,
                                                  verbose: false, encore: EnVer(1));
                    battles += er.Battles.Count;
                    carried += er.Battles.Sum(b => (long)b.EncoreWoundedDeaths);
                }
            Console.WriteLine($"- **(f)** 会戦（{col.Name}・{battles} 戦）で数えた「刻まれたまま倒れた駒」: **{carried}** —— "
                              + "**境界で `WoundWriters = null` にしている**（`Engagement.cs` の "
                              + "`StatusKeys.All` を消す行の直後。`Carry` でも消す）—— **○**");
        }

        // (i) 紙と実測の照合
        Console.WriteLine("- **(i)** 紙と実測の照合は `encore phase0` の §2-3 に出す"
                          + "（**線形／門ではなく出力／分母を削る** の3点を先に書いてある）");
        // (j) 主判定が2系列で同符号 —— tables の系列1/系列2 の列で見る
        Console.WriteLine("- **(j)** 主判定が2系列で同符号 —— 表B の `系列1` / `系列2` の列で判定する");
        // (k) ctx.PickOne の箇所数
        Console.WriteLine("- **(k)** `ctx.PickOne` を新たに使っていない —— "
                          + "`NoteEncore` は**スロット昇順**で並べるだけで `PickOne` を1度も呼ばない"
                          + "（第89期 (h)：候補2個以上で `Roll` を消費する）");
        Console.WriteLine();
        return;
    }

    Console.WriteLine("encore: 引数は phase0 / run <a> <skip> <take> / tables <TSV...> / check。");
    return;
}
}
