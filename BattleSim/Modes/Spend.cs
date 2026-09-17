using BattleCore;
using static Common;

// =====================================================================================
// spend モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "spend")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 spend
// =====================================================================================

static class SpendDiag
{
// 強化の使い道の解剖（第65期・調査）。**この期は新しい機構を1つも作らない。駒も作らない。**
// `Traits.cs` の挙動 / `UnitCatalog.All` / `Stages` / `CompareBuilds()` は1行も動かさず、
// 足したのは **(a) 誰も読んで分岐しない計数**（到着ターン・使用・経路別の受け手）と
// **(b) 窓口 `Whet` の加算だけを経路ごとに落とすノブ `WhetMask`**（既定は空＝現行）。
//
// 主題は「**強化の供給の偏りは、編成・配置の判断を減らしているか**」。
// 積み残し1（第56期）は「供給の 47.1% が吐き戻し1本＝編成が選べる供給が無い」として立ち、
// **「要検討」のまま3期（62〜64）を「行き先」の駒に使って3回落ちた。**
// 偏りが問題だと確かめた数字は1つも無い——ここで測る。
//
// 立てた仮説（測って否定してよい。**測る前に指示書 §0-3 で固定**）:
//   H1 偏り説 —— 吐き戻しが半分を占めるので編成が強化について判断する余地が無い
//   H2 無価値説 —— **吐き戻しの帰属は ≈ 0**（第23期の再現）。偏りは価値の無い通貨の帳簿上の偏り
//   H3 遅さ説 —— 律速は偏りではなく**到着の遅さ**。受け手が振る前に戦闘が終わる
//
// **自由度 (e) を構造的に消してある**（第64期の教訓）:
//   行は選ばない（その経路の供給者を含む**全行**）／席は動かさない（`reseat` は帰属の器具ではない）／
//   枠は差し替えない（**落とすのは窓口の加算1行だけ**で、供給者の他の特性は残る）。
//
//     dotnet run --project BattleSim -c Release 0 spend        # 主表（A 帯 seed 0..199）
//     dotnet run --project BattleSim -c Release 0 spend map    # 判断の地図（**戦闘0回**）
//     dotnet run --project BattleSim -c Release 0 spend alt    # 再現帯（seed 200..599）
public static void Run(string[] args, int stageIndex)
{
    var spBuilds = CompareBuilds().ToArray();
    IReadOnlyList<EnemyCatalog.Stage> spStages = EnemyCatalog.Stages;
    string spArg = args.Length > 2 ? args[2] : "";
    bool spAlt = spArg == "alt";
    int spSeed0 = spAlt ? 200 : 0;
    int spSeedN = spAlt ? 400 : 200;

    // 経路の台帳。**「編成が動かせるか」は測る前にコードを読んで固定した**（指示書 §1-3）。
    // Q3（H1 の判定）はこの分類の上で読むので、**実測を見てから書き換えないこと。**
    var spRoutes = new (WhetRoute Route, TraitId Need, string Supplier, string Dest, string Decided, string Lever)[]
    {
        (WhetRoute.Goad,         TraitId.Goad,     "カリ",   "隣接する `CurrentAttack` 最大の味方1体",
         "カリの隣に誰を置くか", "配置"),
        (WhetRoute.RallyOpening, TraitId.Rally,    "ガン",   "味方全体（自分以外・`SupportTargets`）",
         "—（無条件）", "動かせない"),
        (WhetRoute.RallyTurn,    TraitId.Rally,    "ガン",   "手番を差し出した味方（`SupportTargets`）",
         "`IdleTurn` の供給（痺れ・据え・不動）", "編成"),
        (WhetRoute.Bind,         TraitId.Bind,     "クグ",   "`AcceptsSupport` を通り痺れていない味方から**無作為**",
         "候補集合の枚数だけ（`Roll`）", "編成（弱い）"),
        (WhetRoute.Drifter,      TraitId.Drifter,  "シオ",   "動かされた味方",
         "移動の供給（バサ・ハネ・曝き）", "編成"),
        (WhetRoute.Regurgitate,  TraitId.Colossus, "ゴルム", "ゴルムが庇った相手（`SupportTargets`）",
         "巨躯の被覆（誰がゴルムより後ろにいるか）", "配置"),
        (WhetRoute.Favor,        TraitId.Favor,    "ヒヨ",   "燃えている味方全員（自分を除く・**位置を問わない**）",
         "燃焼の供給（ボルグの隣・破裂）", "配置（間接）"),
    };

    var spName = new Dictionary<string, string>();
    foreach (UnitDef d in UnitCatalog.All) spName[d.Id] = d.Name;
    foreach (EnemyCatalog.Stage st in spStages)
        foreach ((int _, UnitDef d) in st.Enemy.Occupied()) spName[d.Id] = d.Name;
    string SpLabel(string id) => spName.TryGetValue(id, out string? n) ? n : id;

    bool SpHas((string Name, Formation F) row, TraitId t)
        => row.F.Occupied().Any(o => o.Item2.Traits.Contains(t));

    // ---- map: 判断の地図。**戦闘を1回も回さない。** -----------------------------
    if (spArg == "map")
    {
        Console.WriteLine("# 強化の使い道 —— 判断の地図（第65期 Phase 0。**戦闘0回**）");
        Console.WriteLine();
        Console.WriteLine("## 0-2. 出力経路（強化1点が「使われる」ための分母）");
        Console.WriteLine();
        Console.WriteLine("`CurrentAttack` を自分の出力量に変換する箇所は**ロスター全体で4つ**"
                          + "（`BattleContext.NoteAttackRead` の呼び出し元）。");
        Console.WriteLine();
        Console.WriteLine("| # | 場所 | 型 | `CurrentAttack` の読み | 手番 |");
        Console.WriteLine("|--:|---|---|---|---|");
        Console.WriteLine("| 1 | `BattleEngine.cs:1815` `PerformAttack` | 攻撃（単体・薙ぎ・貫き・全体） | "
                          + "1回（`atk` を作る所。倍率＝止めはこの後） | 自分の手番 |");
        Console.WriteLine("| 2 | `Traits.cs:1221` 棘 `ThornsTrait.OnDamaged` | 反撃（範囲） | "
                          + "1回（`CurrentAttack * Multiplier`） | **ターン外**（`CanActOutOfTurn`） |");
        Console.WriteLine("| 3 | `Traits.cs:3453` 仇討ち `AvengeTrait.OnAllyDamaged` | 反撃（単体） | "
                          + "1回（`Max(1, CurrentAttack)`） | **ターン外**（`CanActOutOfTurn`） |");
        Console.WriteLine("| 4 | `Traits.cs:3504` 責め苦の追撃 `TormentTrait.OnAfterAttack` | 追撃（単体） | "
                          + "1回（`Max(1, CurrentAttack)`） | 自分の手番の中 |");
        Console.WriteLine();
        Console.WriteLine("> **乗算はこの4本の外側にある。** `UnitState.CurrentAttack` は "
                          + "`Def.Attack + AtkBonus` を作ってから `Trait.ModifyAttack` を通すので、");
        Console.WriteLine("> **熾火（ホタ・燃焼中は4倍＋貫き）に配った強化1点は 4 点になる**（第58期の乗算監査）。");
        Console.WriteLine("> 逆に `Actions = [Skill]` の3枚（ノノ・ミオ・ヒヨ）は**4本のどれも通らない**"
                          + "＝真の捨て場（第64期）。");
        Console.WriteLine();

        Console.WriteLine("## 0-3. 経路ごとの「行き先の決まり方」");
        Console.WriteLine();
        Console.WriteLine("**Q3（H1 の判定）はこの分類の上で読む。実測を見てから書き換えない。**");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 供給者 | 行き先 | 決めているもの | 編成が動かせるか |");
        Console.WriteLine("|---|:-:|---|---|:-:|");
        foreach ((WhetRoute r, TraitId _, string sup, string dest, string dec, string lev) in spRoutes)
            Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | {sup} | {dest} | {dec} | **{lev}** |");
        Console.WriteLine();
        Console.WriteLine("> **縛めの「?」を埋めた**（指示書 §1-3）——`BindTrait.BindAlly` は");
        Console.WriteLine("> `AcceptsSupport` を通り痺れていない味方から `ctx.Roll` で**無作為に**選ぶ。");
        Console.WriteLine("> 席も攻撃力も隣接も1ビットも読まないので、**配置では動かせない**"
                          + "（動かせるのは候補集合の枚数だけ）。");
        Console.WriteLine();

        Console.WriteLine("## 0-5 / 0-6. 各経路の供給者を含む行（陰性対照の分母）");
        Console.WriteLine();
        var spPrimary = new HashSet<string>(Baseline.PrimaryRows);
        Console.WriteLine("| 経路 | 供給者を含む行 | 含まない行（陰性対照） | 主判定19行のうち |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach ((WhetRoute r, TraitId need, string _, string __, string ___, string ____) in spRoutes)
        {
            int with = spBuilds.Count(b => SpHas(b, need));
            int prim = spBuilds.Count(b => SpHas(b, need) && spPrimary.Contains(b.Name));
            Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | {with} | {spBuilds.Length - with} | {prim} |");
        }
        Console.WriteLine();
        Console.WriteLine($"全 **{spBuilds.Length} 行**（`docs/balance.md` = {spBuilds.Length} × 5 波 = "
                          + $"**{spBuilds.Length * 5} セル**）。");
        Console.WriteLine();
        return;
    }

    // ---- 測定 -------------------------------------------------------------
    // 1戦ぶんの器。**行 × 波の勝率**と、経路別の量・到着・使用・受け手を同じ走査で取る。
    double[] SpWin(Formation f, WhetMask? mask, double[]? route, double[]? turnSum,
                   double[]? firstSum, double[]? firstCnt, double[]? used,
                   Dictionary<string, double>[]? to, Dictionary<string, double>? got,
                   Dictionary<string, double>? hoardNew, double[]? battleTurns)
    {
        var win = new double[spStages.Count];
        for (int w = 0; w < spStages.Count; w++)
        {
            int wins = 0;
            for (int seed = spSeed0; seed < spSeed0 + spSeedN; seed++)
            {
                BattleResult r = BattleEngine.Run(f, spStages[w].Enemy, seed, verbose: false,
                                                  whetMask: mask);
                if (r.PlayerWon) wins++;
                if (route is null) continue;

                if (battleTurns is not null) battleTurns[0] += r.Turns;
                for (int i = 0; i < WhetRoutes.Count; i++)
                {
                    route[i] += r.WhetByRoute[i];
                    turnSum![i] += r.WhetTurnSumByRoute[i];
                    firstSum![i] += r.WhetFirstTurnSumByRoute[i];
                    firstCnt![i] += r.WhetFirstTurnCountByRoute[i];
                    used![i] += r.WhetUsedByRoute[i];
                    foreach ((string id, int v) in r.WhetToByRoute[i])
                        to![i][id] = to[i].TryGetValue(id, out double had) ? had + v : v;
                }
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (t.Whetted <= 0) continue;
                    got![id] = got.TryGetValue(id, out double g) ? g + t.Whetted : t.Whetted;
                    if (t.AttackReads == 0)
                        hoardNew![id] = hoardNew.TryGetValue(id, out double h) ? h + t.Whetted : t.Whetted;
                }
            }
            win[w] = wins * 100.0 / spSeedN;
        }
        return win;
    }

    var spSw = System.Diagnostics.Stopwatch.StartNew();

    // 現行（`WhetMask` 空）。**ここが 305 セルの `compare` と一致する**（受け入れ基準1）。
    var spBase = new Dictionary<string, double[]>();
    var spRouteAmt = new double[WhetRoutes.Count];
    var spTurnSum = new double[WhetRoutes.Count];
    var spFirstSum = new double[WhetRoutes.Count];
    var spFirstCnt = new double[WhetRoutes.Count];
    var spUsed = new double[WhetRoutes.Count];
    var spTo = Enumerable.Range(0, WhetRoutes.Count).Select(_ => new Dictionary<string, double>()).ToArray();
    var spGot = new Dictionary<string, double>();
    var spHoard = new Dictionary<string, double>();
    var spTurns = new double[1];
    var spRowRoute = new Dictionary<string, double[]>();
    var spRowHoard = new Dictionary<string, double>();

    foreach ((string rn, Formation f) in spBuilds)
    {
        var rr = new double[WhetRoutes.Count];
        var rh = new Dictionary<string, double>();
        var rowTo = Enumerable.Range(0, WhetRoutes.Count).Select(_ => new Dictionary<string, double>()).ToArray();
        spBase[rn] = SpWin(f, null, rr, spTurnSum, spFirstSum, spFirstCnt, spUsed, rowTo, spGot, rh, spTurns);
        for (int i = 0; i < WhetRoutes.Count; i++)
        {
            spRouteAmt[i] += rr[i];
            foreach ((string id, double v) in rowTo[i])
                spTo[i][id] = spTo[i].TryGetValue(id, out double had) ? had + v : v;
        }
        spRowRoute[rn] = rr;
        spRowHoard[rn] = rh.Values.Sum();
        foreach ((string id, double v) in rh)
            spHoard[id] = spHoard.TryGetValue(id, out double h) ? h + v : v;
    }

    // 経路を1本ずつ落とす。**全 61 行**（陰性対照＝供給者を含まない行も同じ表で出す）。
    var spDrop = new Dictionary<(WhetRoute, string), double[]>();
    foreach ((WhetRoute r, TraitId _, string __, string ___, string ____, string _____) in spRoutes)
        foreach ((string rn, Formation f) in spBuilds)
            spDrop[(r, rn)] = SpWin(f, WhetMask.Of(r), null, null, null, null, null, null, null, null, null);

    spSw.Stop();

    int spBattles = spBuilds.Length * spStages.Count * spSeedN * (1 + spRoutes.Length);
    double spN = spBuilds.Length * spStages.Count * (double)spSeedN;
    double Mean(double[] w) => w.Average();

    Console.WriteLine("# 強化は何に使われているのか（第65期・調査）");
    Console.WriteLine();
    Console.WriteLine($"全 **{spBuilds.Length} 行** × {spStages.Count} 波 × seed {spSeed0}..{spSeed0 + spSeedN - 1}"
                      + $"、現行 + 7 経路落とし = **{spBattles} 戦**（{spSw.Elapsed.TotalSeconds:F1} 秒）。");
    Console.WriteLine($"帯は **{(spAlt ? "B（再現・200..599）" : "A（主・0..199）")}**。");
    Console.WriteLine();
    Console.WriteLine("**行を選ばない・席を動かさない・枠を差し替えない**（第64期の自由度 (e) を構造的に消してある）。");
    Console.WriteLine("落とすのは **`BattleContext.Whet` の `AtkBonus +=` の1行だけ**で、");
    Console.WriteLine("縛めの痺れ・移り木の回復・吐き戻しのログ・乱数の消費は1ビットも動かない。");
    Console.WriteLine();

    // ---- 表A -------------------------------------------------------------
    Console.WriteLine("## 表A. 経路の帰属（現行 − その経路を落とした版）");
    Console.WriteLine();
    Console.WriteLine("帰属は**5波平均の勝率(pt)**。**`|帰属| < 1.5pt` は 0 と読む**（第57期の線）。");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 供給量/戦 | 占有率 | 該当行 | 帰属の平均 | 正の行 | \\|帰属\\| ≥ 1.5 の行 | 陰性対照（最大\\|Δ\\|） |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    double spTotal = spRouteAmt.Sum();
    var spAttr = new Dictionary<WhetRoute, List<(string Row, double D)>>();
    foreach ((WhetRoute r, TraitId need, string _, string __, string ___, string ____) in spRoutes)
    {
        var with = new List<(string, double)>();
        double negMax = 0;
        foreach ((string rn, Formation f) in spBuilds)
        {
            double d = Mean(spBase[rn]) - Mean(spDrop[(r, rn)]);
            if (SpHas((rn, f), need)) with.Add((rn, d));
            else negMax = Math.Max(negMax, Math.Abs(d));
        }
        spAttr[r] = with;
        Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | {spRouteAmt[(int)r] / spN:F2} | "
                          + $"{(spTotal > 0 ? spRouteAmt[(int)r] * 100.0 / spTotal : 0):F1}% | {with.Count} | "
                          + $"**{with.Average(x => x.Item2):+0.0;-0.0;0.0}** | "
                          + $"{with.Count(x => x.Item2 > 0)} | {with.Count(x => Math.Abs(x.Item2) >= 1.5)} | "
                          + $"{negMax:F1} |");
    }
    Console.WriteLine($"| **合計** | **{spTotal / spN:F2}** | 100.0% | — | — | — | — | — |");
    Console.WriteLine();

    Console.WriteLine("### 表A-2. 行ごとの帰属（該当行を全部・選んでいない）");
    Console.WriteLine();
    foreach ((WhetRoute r, TraitId _, string __, string ___, string ____, string _____) in spRoutes)
    {
        Console.WriteLine($"**{WhetRoutes.Names[(int)r]}**（{spAttr[r].Count} 行）: "
                          + string.Join(" / ", spAttr[r].OrderByDescending(x => x.D)
                                .Select(x => $"{x.Row} {x.D:+0.0;-0.0;0.0}")));
        Console.WriteLine();
    }

    // ---- 表B -------------------------------------------------------------
    Console.WriteLine("## 表B. 到着と使用");
    Console.WriteLine();
    Console.WriteLine("**使用率** = 受け取った**後**に受け手が `AttackReads` を1度でも通した量の割合。");
    Console.WriteLine("死蔵（`AttackReads == 0`・第64期）より厳しい——最後の一撃の後に届いた強化は死蔵に数えられない。");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 供給量/戦 | 到着の平均T | 初到着の平均T | 使用率 | 到着T ÷ 決着T |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    double spAvgTurns = spTurns[0] / spN;
    foreach ((WhetRoute r, TraitId _, string __, string ___, string ____, string _____) in spRoutes)
    {
        int i = (int)r;
        double amt = spRouteAmt[i];
        double arr = amt > 0 ? spTurnSum[i] / amt : 0;
        double first = spFirstCnt[i] > 0 ? spFirstSum[i] / spFirstCnt[i] : 0;
        Console.WriteLine($"| {WhetRoutes.Names[i]} | {amt / spN:F2} | {arr:F2} | {first:F2} | "
                          + $"**{(amt > 0 ? spUsed[i] * 100.0 / amt : 0):F1}%** | "
                          + $"{(spAvgTurns > 0 ? arr / spAvgTurns : 0):F2} |");
    }
    Console.WriteLine();
    Console.WriteLine($"参考: 全 {spBuilds.Length} 行 × 5 波の**決着ターンの平均は {spAvgTurns:F2}T**。");
    Console.WriteLine("開戦時（`OnBattleStart`）の到着はターン **0**。");
    Console.WriteLine();

    // ---- 表C -------------------------------------------------------------
    Console.WriteLine("## 表C. 判断の地図（Phase 0-3 の分類 + 実測）");
    Console.WriteLine();
    Console.WriteLine("**分類は測る前に固定した**（`spend map`）。**実測を見てから書き換えていない。**");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 決めているもの | 編成が動かせるか | 帰属の平均 | 受け手の種類数 | 最大の受け手 |");
    Console.WriteLine("|---|---|:-:|--:|--:|---|");
    foreach ((WhetRoute r, TraitId _, string __, string ___, string dec, string lev) in spRoutes)
    {
        int i = (int)r;
        var top = spTo[i].OrderByDescending(kv => kv.Value).FirstOrDefault();
        double sum = spTo[i].Values.Sum();
        Console.WriteLine($"| {WhetRoutes.Names[i]} | {dec} | **{lev}** | "
                          + $"{spAttr[r].Average(x => x.D):+0.0;-0.0;0.0} | {spTo[i].Count} | "
                          + (top.Key is null ? "—"
                             : $"{SpLabel(top.Key)} {(sum > 0 ? top.Value * 100.0 / sum : 0):F0}%") + " |");
    }
    Console.WriteLine();
    Console.WriteLine("### Q3 の判定（群ごとの帰属の平均）");
    Console.WriteLine();
    Console.WriteLine("| 群 | 経路 | 帰属の平均（経路の単純平均） |");
    Console.WriteLine("|---|---|--:|");
    foreach (string lev in new[] { "配置", "配置（間接）", "編成", "編成（弱い）", "動かせない" })
    {
        var rs = spRoutes.Where(x => x.Lever == lev).ToArray();
        if (rs.Length == 0) continue;
        Console.WriteLine($"| {lev} | {string.Join(" / ", rs.Select(x => WhetRoutes.Names[(int)x.Route]))} | "
                          + $"**{rs.Average(x => spAttr[x.Route].Average(y => y.D)):+0.0;-0.0;0.0}** |");
    }
    Console.WriteLine();

    // ---- 表D -------------------------------------------------------------
    Console.WriteLine("## 表D. 死蔵（新定義・`AttackReads == 0`）の行ごと分布");
    Console.WriteLine();
    double spHoardAll = spHoard.Values.Sum();
    Console.WriteLine($"死蔵総量 **{spHoardAll / spN:F2}** /戦 ＝ 強化総量の "
                      + $"**{(spTotal > 0 ? spHoardAll * 100.0 / spTotal : 0):F1}%**。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 死蔵(新)/戦 | 受けた総量/戦 | 死蔵率 | 真の捨て場か |");
    Console.WriteLine("|---|--:|--:|--:|:-:|");
    var spDump = new[] { "nono", "mio", "hiyo" };
    foreach ((string id, double v) in spHoard.OrderByDescending(kv => kv.Value).Take(10))
    {
        double g = spGot.TryGetValue(id, out double gv) ? gv : 0;
        Console.WriteLine($"| {SpLabel(id)} | **{v / spN:F2}** | {g / spN:F2} | "
                          + $"{(g > 0 ? v * 100.0 / g : 0):F1}% | {(spDump.Contains(id) ? "○" : "")} |");
    }
    Console.WriteLine();
    Console.WriteLine("捨て場3枚（ノノ・ミオ・ヒヨ）が受けた量: "
                      + string.Join(" / ", spDump.Select(id =>
                          $"{SpLabel(id)} {(spGot.TryGetValue(id, out double g) ? g / spN : 0):F2}"))
                      + " /戦。");
    Console.WriteLine();
    Console.WriteLine("死蔵が多い行（上位8・量/戦）: "
                      + string.Join(" / ", spRowHoard.OrderByDescending(kv => kv.Value).Take(8)
                          .Select(kv => $"{kv.Key} {kv.Value / (spStages.Count * (double)spSeedN):F2}")));
    Console.WriteLine();

    // ---- 表E -------------------------------------------------------------
    Console.WriteLine("## 表E. 乗算・反撃への流入");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 読み方 | 受けた強化/戦 | 実効 | その駒を含む行 |");
    Console.WriteLine("|---|---|--:|---|--:|");
    var spReaders = new (string Id, string How, string Eff)[]
    {
        ("hota", "熾火（燃焼中は攻撃力4倍＋貫き）", "**×4**（第58期の乗算監査。与ダメの実効は 7.4 倍）"),
        ("kado", "棘（`PerformAttack` を通らず反撃量を `CurrentAttack` で決める）", "×1（反撃・範囲）"),
        ("zan",  "仇討ち（ターン外・`CurrentAttack` をそのまま）", "×1（ターン外）"),
        ("shiga", "責め苦の追撃（自分の手番の中）", "×1（追撃）"),
    };
    foreach ((string id, string how, string eff) in spReaders)
    {
        double g = spGot.TryGetValue(id, out double gv) ? gv : 0;
        int rows = spBuilds.Count(b => b.F.Occupied().Any(o => o.Item2.Id == id));
        Console.WriteLine($"| {SpLabel(id)} | {how} | {g / spN:F2} | {eff} | {rows} |");
    }
    Console.WriteLine();
    Console.WriteLine("乗算持ち（ホタ）を含む行と含まない行の帰属:");
    Console.WriteLine();
    Console.WriteLine("| 経路 | ホタを含む行 | 含まない行 |");
    Console.WriteLine("|---|--:|--:|");
    foreach ((WhetRoute r, TraitId _, string __, string ___, string ____, string _____) in spRoutes)
    {
        var withH = spAttr[r].Where(x => spBuilds.First(b => b.Name == x.Row).F.Occupied()
                                          .Any(o => o.Item2.Id == "hota")).ToArray();
        var noH = spAttr[r].Where(x => !spBuilds.First(b => b.Name == x.Row).F.Occupied()
                                        .Any(o => o.Item2.Id == "hota")).ToArray();
        Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | "
                          + (withH.Length > 0 ? $"{withH.Average(x => x.D):+0.0;-0.0;0.0}（{withH.Length} 行）" : "—")
                          + " | "
                          + (noH.Length > 0 ? $"{noH.Average(x => x.D):+0.0;-0.0;0.0}（{noH.Length} 行）" : "—")
                          + " |");
    }
    Console.WriteLine();

    // ---- 陰性対照 ---------------------------------------------------------
    Console.WriteLine("## Q6. 陰性対照（供給者を含まない行が ±0.0 であること）");
    Console.WriteLine();
    Console.WriteLine("| 経路 | 対照の行数 | 最大 \\|Δ\\| | 0 でない行 |");
    Console.WriteLine("|---|--:|--:|--:|");
    foreach ((WhetRoute r, TraitId need, string _, string __, string ___, string ____) in spRoutes)
    {
        var neg = spBuilds.Where(b => !SpHas(b, need))
                          .Select(b => Math.Abs(Mean(spBase[b.Name]) - Mean(spDrop[(r, b.Name)]))).ToArray();
        Console.WriteLine($"| {WhetRoutes.Names[(int)r]} | {neg.Length} | {neg.Max():F1} | "
                          + $"{neg.Count(x => x > 0.0001)} |");
    }
    Console.WriteLine();

    return;
}
}
