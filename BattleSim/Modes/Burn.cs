using BattleCore;
using static Common;

// =====================================================================================
// burn モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "burn")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 burn
// =====================================================================================

static class BurnDiag
{
// 燃焼の解剖（第57期）。**この期は新しい機構を1つも作らない。**
// `Traits.cs` の挙動 / `UnitCatalog.All` / `Stages` / `CompareBuilds()` は1行も動かさず、
// 足したのは**誰も読んで分岐しない計数フィールド**（`UnitTally` の Burn* 9本）だけ。
//
// 主題は「燃焼は通貨か、それとも遅いダメージか」。書き手は火の粉（ボルグ）1枚、
// 駒の読み手は熾火（ホタ）1枚で、**両者が同席する行は 56 行中1つしかない。**
//
// 立てた仮説（測って否定してよい）:
//   H1 供給不足 —— 足りないのは2枚目の書き手である
//   H2 供給過剰 —— 非スタックなので再着火は残ターンを 3 に戻すだけ。**着火の大半は捨てられている**
//   H3 接続の不在 —— 「燃えている」という事実を読んで分岐するのはホタ1枚だけ
//
// **対照は同数値・特性だけを落とした素体**（`UnitCatalog` には足さない。第49期の `SgPlain` と同型）。
// **`Splash`（巻き込み）は残す**——一緒に落とすと「巻き込みが消えたから勝った」と混ざる
// （第34期の「HP を振ったつもりが波の総HPも振っていた」と同じ穴）。
//
//     dotnet run --project BattleSim -c Release 0 burn [絞り込み]
//     dotnet run --project BattleSim -c Release 0 burn phase0   # 窓口の一覧と接続の地図（戦闘0回）
public static void Run(string[] args, int stageIndex)
{
    var buBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> buStages = EnemyCatalog.Stages;
    const int BuSeeds = 200;   // compare / spread / whet と同じ帯
    string buArg = args.Length > 2 ? args[2] : "";

    // ---- phase0: 窓口の一覧と接続の地図。**戦闘を1回も回さない。** -------------------
    if (buArg == "phase0")
    {
        Console.WriteLine("# 燃焼の地図（第57期 Phase 0）—— 戦闘0回");
        Console.WriteLine();
        Console.WriteLine("## 0-1. 燃焼の窓口一覧（call-site grep）");
        Console.WriteLine();
        Console.WriteLine("| 種別 | 場所 | 中身 | 盤面を動かすか |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine("| 書き（駒） | `Traits.cs:4286` | 火の粉 `CinderTrait.OnAfterAttack` → `ctx.Ignite(target)`（主目標＝敵） | 動かす |");
        Console.WriteLine("| 書き（駒） | `Traits.cs:4292` | 同 → `ctx.Ignite(ally, friendly: true)`（隣接する味方全員） | 動かす |");
        Console.WriteLine("| 窓口 | `BattleEngine.cs` `Ignite` | 再付与は残ターンの**設定**（加算ではない） | 動かす |");
        Console.WriteLine("| 読み（engine） | `BattleEngine.cs` `TickStatuses` | 燃焼ループ。残ターン −1 | 動かす |");
        Console.WriteLine("| 読み（engine） | 同 | `ApplyDamage(u, BurnRules.Damage, null)` | 動かす |");
        Console.WriteLine("| 読み（engine） | `BattleEngine.cs` `NoteScapegoatDot` | 業の帰属。**保持者不在なら短絡で走らない**（棄却駒） | 動かさない |");
        Console.WriteLine("| 読み（駒） | `Traits.cs:4334` | 熾火 `PyreTrait.ModifyAttack`（燃えていれば攻撃 ×4） | 動かす |");
        Console.WriteLine("| 読み（駒） | `Traits.cs:4337` | 熾火 `PyreTrait.ModifyPattern`（燃えていれば貫き） | 動かす |");
        Console.WriteLine("| 読み（駒） | `Traits.cs` `FavorTrait.OnTurnStart` | **火選り（第58期）**。燃えている味方を `ctx.Whet` / 燃えていない隣接味方を `ctx.Dull` | 動かす |");
        Console.WriteLine("| 表示 | `BattleEngine.cs` `StatusSnapshot` | 見出し（「燃」） | 動かさない |");
        Console.WriteLine("| 計数 | `Ignite` / `TickStatuses` / `ApplyDamage` / `PerformAttack` | **第57期に足した `UnitTally.Burn*`** | 動かさない |");
        Console.WriteLine();
        Console.WriteLine($"`BurnRules.Damage` = **{BurnRules.Damage}** / `BurnRules.Turns` = **{BurnRules.Turns}**（非スタック・量を持たない）。");
        Console.WriteLine();
        Console.WriteLine("**盤面を動かす窓口は 8 本**（第57期は 7 本）。うち書きは 2（どちらも火の粉の同じ1回の発火）、");
        Console.WriteLine("engine の読みは 2（同じ刻みの前半・後半）、**駒の読みは 3**");
        Console.WriteLine("——ホタの同じ1条件が 2 本と、**第58期に足した火選りの 1 本**。");
        Console.WriteLine();

        Console.WriteLine("## 0-2. 燃焼の刻みが通るダメージ層の段");
        Console.WriteLine();
        Console.WriteLine("刻みは `source == null` で呼ばれる。**各段が「燃焼を読んでいる」のか");
        Console.WriteLine("「ダメージを読んでいる」のかを分ける**——これが Q3 の分母になる。");
        Console.WriteLine();
        Console.WriteLine("| # | 段 | 燃焼の刻みに効くか | 何を読んでいるか |");
        Console.WriteLine("|--:|---|:-:|---|");
        Console.WriteLine("| 1 | 棘守りの上限（`AbsorbCap` = 8） | **効かない** | ダメージ量（6 < 8 なので分割が起きない） |");
        Console.WriteLine("| 2 | `Trait.ModifyIncomingDamage` | 効く | ダメージ |");
        Console.WriteLine("| 3 | 惨禍（+50%） | 効く | ダメージ |");
        Console.WriteLine("| 4 | 据え（`IdleTurn` で −50%） | 効く | ダメージ |");
        Console.WriteLine("| 5 | 散開（−%） | 効く | ダメージ |");
        Console.WriteLine("| 6 | 萎縮（−%） | 効く | ダメージ |");
        Console.WriteLine("| 7 | 巨躯の肩代わり | 効く | ダメージ |");
        Console.WriteLine("| 7' | └ 吐き戻し | **効かない** | `source is not null` を読む（**刻みを名指しで外している**） |");
        Console.WriteLine("| 8 | 分かち（＋なまり） | 効く | ダメージ |");
        Console.WriteLine("| 9 | 破片（`Armor`） | 効く | ダメージ |");
        Console.WriteLine("| 10 | 軛（`Cap` = 25） | **効かない** | ダメージ量（6 < 25） |");
        Console.WriteLine("| 11 | `Trait.OnDamaged` | 効く | ダメージ（`source` が null なので反撃・吐き戻しは自然に外れる） |");
        Console.WriteLine("| 12 | `Trait.OnAllyDamaged` | 効く | ダメージ |");
        Console.WriteLine();
        Console.WriteLine("**12 段のうち、燃焼という事実を読んでいる段は 0。** どの段も読んでいるのは");
        Console.WriteLine("「ダメージが来た」であって「燃えている」ではない（7' だけが `source == null` を読むが、");
        Console.WriteLine("これは毒の刻みと燃焼の刻みを区別していない）。");
        Console.WriteLine();

        Console.WriteLine("## 0-3. 波ルールと燃焼");
        Console.WriteLine();
        Console.WriteLine("| 波ルール | 燃焼に課金するか | 理由 |");
        Console.WriteLine("|---|:-:|---|");
        Console.WriteLine("| 粛（第二波・ターン外の行動禁止） | **しない** | 刻みは `TickStatuses`。`CanActOutOfTurn` を通らない |");
        Console.WriteLine("| 渇き（第三波・回復禁止） | **しない** | 刻みは `ctx.Heal` を通らない |");
        Console.WriteLine("| 軛（第四波・1発 25 上限） | **しない** | 6 < 25 |");
        Console.WriteLine("| 殉教（第五波・標的の付け替え） | **しない** | 刻みは `SelectTarget` を通らない |");
        Console.WriteLine();
        Console.WriteLine("**予測（表A' で検算する）: 燃焼はどの波ルールにも課金されない。");
        Console.WriteLine("したがって波ごとの燃焼量は波ルールではなく「敵の数」と「決着ターン」で決まる。**");
        Console.WriteLine();

        Console.WriteLine("## 0-4. 在庫");
        Console.WriteLine();
        int p0Borg = buBuilds.Count(b => b.F.Occupied().Any(o => o.Def.Id == "borg"));
        int p0Hota = buBuilds.Count(b => b.F.Occupied().Any(o => o.Def.Id == "hota"));
        int p0Both = buBuilds.Count(b => b.F.Occupied().Any(o => o.Def.Id == "borg")
                                      && b.F.Occupied().Any(o => o.Def.Id == "hota"));
        Console.WriteLine($"- `CompareBuilds()` の行数: **{buBuilds.Length}**");
        Console.WriteLine($"- ボルグを含む行: **{p0Borg}** ／ ホタを含む行: **{p0Hota}** ／ 両方: **{p0Both}**");
        Console.WriteLine($"- `UnitCatalog.All` の実数: **{UnitCatalog.All.Count}** 体（上限 52 に対して残り **{52 - UnitCatalog.All.Count}**）");
        Console.WriteLine();
        Console.WriteLine("**「ロスター56行」は `CompareBuilds()` の行数であって駒の数ではない。**");
        Console.WriteLine();

        Console.WriteLine("## 0-5. 表E: 通貨どうしの接続");
        Console.WriteLine();
        Console.WriteLine("**「燃えている」という事実を読んで分岐する箇所**だけを数える。");
        Console.WriteLine("**ダメージ層の各段（0-2 の 12 段）は数えない**——あれはダメージを読んでいる。");
        Console.WriteLine();
        Console.WriteLine("| 相手の通貨 | 燃焼 → 相手 | 相手 → 燃焼 | 根拠 |");
        Console.WriteLine("|---|--:|--:|---|");
        Console.WriteLine("| 毒 | 0 | 0 | 別ループ（`TickStatuses`）。互いに条件に入らない |");
        Console.WriteLine("| 標 | 0 | 0 | `SelectTargetChain` は `Burn` を1ビットも見ない |");
        Console.WriteLine("| 痺 | 0 | 0 | 行動順ループは `Burn` を見ない |");
        Console.WriteLine("| 破片 | 0 | 0 | 刻みは吸われるが、破片は「ダメージ」を読んでいる |");
        Console.WriteLine("| 傷 | 0 | 0 | 供給は `OnAfterAttack`。燃焼を条件にしない |");
        Console.WriteLine("| `IdleTurn` | 0 | 0 | 据え・号令は `Burn` を見ない |");
        Console.WriteLine("| 位置 | 0 | 0 | 曝き・突き返し・軋みは `Burn` を見ない |");
        Console.WriteLine("| 死 | 0 | 0 | `OnDeath` / `OnAnyDeath` に燃焼の分岐は無い |");
        Console.WriteLine($"| **強化** | **1** | 0 | **第58期**: 火選りが `Burn > 0` の味方へ `ctx.Whet(WhetRoute.Favor)`。逆向きは無し |");
        Console.WriteLine($"| **弱体** | **1** | 0 | **第58期**: 火選りが `Burn == 0` の隣接味方へ `ctx.Dull(DullRoute.Favor)`。逆向きは無し |");
        Console.WriteLine("| **自分の状態** | — | **1** | `PyreTrait`（`ModifyAttack` / `ModifyPattern`。同じ1条件） |");
        Console.WriteLine("| **隣接（位置）** | — | **1** | `FavorTrait` のマイナス側（`AreAdjacent`）。**燃焼と位置の積を読む唯一の箇所** |");
        Console.WriteLine("| **engine** | — | **1** | `TickStatuses` の燃焼ループ（削って残ターンを減らす） |");
        Console.WriteLine();
        Console.WriteLine("**第57期は 18 セル（9通貨 × 双方向）がすべて 0 だった。**");
        Console.WriteLine("**第58期に 2 セルが埋まった**（`燃焼 → 強化` / `燃焼 → 弱体`）。逆向き（強化・弱体 → 燃焼）は");
        Console.WriteLine("依然 0 で、**火選りは燃焼を読むが、強化・弱体を読んで火を点けることはしない**（一方向）。");
        Console.WriteLine("残る 7 通貨（毒・標・痺・破片・傷・`IdleTurn`・位置・死）との接続は**双方向とも 0 のまま**");
        Console.WriteLine("——ただし火選りのマイナス側は隣接（位置）を条件に読むので、");
        Console.WriteLine("**燃焼と位置の積を読む箇所が1本だけできた**（表の最下段）。");
        return;
    }

    // ---- 対照（同数値・特性だけを落とした素体。`UnitCatalog` には足さない）-------------
    // **`Splash` は残す。** 一緒に落とすと「巻き込みが消えたから勝った」と混ざる。
    UnitDef BorgNoCinder = new()
    {
        Id = "borg_nc", Name = "ボルグ（火の粉なし）",
        MaxHp = 60, Attack = 18, Speed = 8,
        Traits = new[] { TraitId.Splash }, Pattern = AttackPattern.Sweep
    };
    // ホタは `Pattern` を指定していない（既定の Single）ので、素体も Single。
    UnitDef HotaPlain = new()
    {
        Id = "hota_plain", Name = "ホタ（特性なし）",
        MaxHp = 78, Attack = 6, Speed = 7,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single
    };

    Formation BuSwap(Formation src, params (string Id, UnitDef Rep)[] subs)
    {
        Formation f = src.Clone();
        foreach ((int slot, UnitDef d) in src.Occupied())
            foreach ((string id, UnitDef rep) in subs)
                if (d.Id == id) f[slot] = rep;
        return f;
    }

    // 対象はボルグを含む行（ホタを含む行はその内数）。絞り込みは重ねて効く
    // （`alt` はモード名なので絞り込みには使わない）。
    string buFilter = buArg == "alt" ? "" : buArg;
    var buTargets = buBuilds
        .Where(b => b.F.Occupied().Any(o => o.Def.Id == "borg"))
        .Where(b => buFilter.Length == 0 || buFilter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    var buHotaRows = buTargets
        .Where(b => b.F.Occupied().Any(o => o.Def.Id == "hota")).ToArray();

    // 1条件ぶんの測定。波別の勝率・決着Tと、駒ごとの計数の合算を返す。
    (double[] Win, Dictionary<string, UnitTally> Tally, double[] Turns)
        BuRun(Formation src, params (string Id, UnitDef Rep)[] subs)
    {
        Formation f = subs.Length == 0 ? src : BuSwap(src, subs);
        var win = new double[buStages.Count];
        var turns = new double[buStages.Count];
        var sum = new Dictionary<string, UnitTally>();
        for (int w = 0; w < buStages.Count; w++)
        {
            int wins = 0; long tt = 0;
            for (int seed = 0; seed < BuSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, buStages[w].Enemy, seed, verbose: false);
                if (r.PlayerWon) wins++;
                tt += r.Turns;
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    if (!sum.TryGetValue(id, out UnitTally? acc)) sum[id] = acc = new UnitTally();
                    acc.Add(t);
                }
            }
            win[w] = wins * 100.0 / BuSeeds;
            turns[w] = (double)tt / BuSeeds;
        }
        return (win, sum, turns);
    }

    var buSw = System.Diagnostics.Stopwatch.StartNew();

    // 条件0（現行）・条件1（火の粉なし）は 8 行すべてで、条件2・3 はホタを含む行だけで測る。
    var c0 = new Dictionary<string, (double[] Win, Dictionary<string, UnitTally> Tally, double[] Turns)>();
    var c1 = new Dictionary<string, (double[] Win, Dictionary<string, UnitTally> Tally, double[] Turns)>();
    var c2 = new Dictionary<string, (double[] Win, Dictionary<string, UnitTally> Tally, double[] Turns)>();
    var c3 = new Dictionary<string, (double[] Win, Dictionary<string, UnitTally> Tally, double[] Turns)>();

    var swx = System.Diagnostics.Stopwatch.StartNew();
    foreach (var row in buTargets) c0[row.Name] = BuRun(row.F);
    double t0 = swx.Elapsed.TotalSeconds; swx.Restart();
    foreach (var row in buTargets) c1[row.Name] = BuRun(row.F, ("borg", BorgNoCinder));
    double t1 = swx.Elapsed.TotalSeconds; swx.Restart();
    foreach (var row in buHotaRows) c2[row.Name] = BuRun(row.F, ("hota", HotaPlain));
    double t2 = swx.Elapsed.TotalSeconds; swx.Restart();
    foreach (var row in buHotaRows) c3[row.Name] = BuRun(row.F, ("borg", BorgNoCinder), ("hota", HotaPlain));
    double t3 = swx.Elapsed.TotalSeconds;
    buSw.Stop();

    // 陣営の引き当て。TallyByUnit は両陣営を Def.Id で混ぜて持つ。
    var buEnemyIds = new HashSet<string>();
    foreach (EnemyCatalog.Stage st in buStages)
        foreach ((int _, UnitDef d) in st.Enemy.Occupied()) buEnemyIds.Add(d.Id);

    // ---- alt: 帰属の符号を別 seed 帯（200..599）で追試する -------------------------
    // **Q4 の成果物は行ごとの符号**なので、選定に使っていない帯で裏を取る
    // （第44期の誹りは「符号反転が再現しない」でここに落ちた）。
    if (buArg == "alt")
    {
        const int AltFrom = 200, AltTo = 600;
        double[] AltWin(Formation src, params (string Id, UnitDef Rep)[] subs)
        {
            Formation f = subs.Length == 0 ? src : BuSwap(src, subs);
            var win = new double[buStages.Count];
            for (int w = 0; w < buStages.Count; w++)
            {
                int wins = 0;
                for (int seed = AltFrom; seed < AltTo; seed++)
                    if (BattleEngine.Run(f, buStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                win[w] = wins * 100.0 / (AltTo - AltFrom);
            }
            return win;
        }

        var altSw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 帰属の符号の追試（第57期・seed 200..599）");
        Console.WriteLine();
        Console.WriteLine("**選定に使っていない帯**で条件0と条件1（火の粉なし）を測り直す。");
        Console.WriteLine("A帯（0..199）と符号が一致しない行は、そこで読みを止める。");
        Console.WriteLine();
        Console.WriteLine("| 行 | A帯 現行 | A帯 火の粉なし | A帯 帰属 | B帯 現行 | B帯 火の粉なし | B帯 帰属 | 符号一致 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|:-:|");
        int agree = 0;
        foreach (var row in buTargets)
        {
            double a0 = c0[row.Name].Win.Average(), a1 = c1[row.Name].Win.Average();
            double[] b0v = AltWin(row.F), b1v = AltWin(row.F, ("borg", BorgNoCinder));
            double b0 = b0v.Average(), b1 = b1v.Average();
            bool ok = Math.Sign(a0 - a1) == Math.Sign(b0 - b1);
            if (ok) agree++;
            Console.WriteLine($"| {row.Name} | {a0:F1}% | {a1:F1}% | **{a0 - a1:+0.0;-0.0;0.0}** | "
                              + $"{b0:F1}% | {b1:F1}% | **{b0 - b1:+0.0;-0.0;0.0}** | {(ok ? "○" : "**×**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"符号の一致 **{agree} / {buTargets.Length}** 行（{altSw.Elapsed.TotalSeconds:F1} 秒）。");
        Console.WriteLine();

        // 熾火（条件2）と軸まるごと（条件3）も同じ帯で。
        Console.WriteLine("| 行 | 条件 | B帯 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | B帯 帰属 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var row in buHotaRows)
        {
            double[] b0v = AltWin(row.F);
            double[] b2v = AltWin(row.F, ("hota", HotaPlain));
            double[] b3v = AltWin(row.F, ("borg", BorgNoCinder), ("hota", HotaPlain));
            void Line(string cond, double[] v, double? attr)
                => Console.WriteLine($"| {row.Name} | {cond} | "
                    + string.Join(" | ", v.Select(x => $"{x:F1}%"))
                    + $" | **{v.Average():F1}%** | "
                    + (attr is null ? "—" : $"**{attr:+0.0;-0.0;0.0}**") + " |");
            Line("0 現行", b0v, null);
            Line("2 熾火なし", b2v, b0v.Average() - b2v.Average());
            Line("3 両方素体", b3v, b0v.Average() - b3v.Average());
        }
        altSw.Stop();
        Console.WriteLine();
        return;
    }

    Console.WriteLine("# 燃焼は何に繋がっているのか（第57期・調査）");
    Console.WriteLine();
    Console.WriteLine($"対象 **{buTargets.Length} 編成**（ボルグを含む行）× 全 {buStages.Count} 波 × seed 0..{BuSeeds - 1}。");
    Console.WriteLine($"条件0 {t0:F1}s ／ 条件1 {t1:F1}s ／ 条件2 {t2:F1}s ／ 条件3 {t3:F1}s"
                      + $"（合計 {buSw.Elapsed.TotalSeconds:F1} 秒）。");
    Console.WriteLine();
    Console.WriteLine("**盤面は1つも動かしていない**——足したのは誰も読んで分岐しない計数だけで、");
    Console.WriteLine("`compare` の 280 セルは差し替え前と 0 件で一致する。");
    Console.WriteLine();

    // ================= 表A: 供給の実測 =================
    Console.WriteLine("## 表A. 供給の実測（条件0・行 × 波）");
    Console.WriteLine();
    Console.WriteLine("`捨て率` = 再着火 ÷ (着火 + 再着火)。**これが H2 の判定値。**");
    Console.WriteLine();
    Console.WriteLine("| 行 | 波 | 着火(敵) | 着火(味) | 再着火 | **捨て率** | 燃T(敵/味) | 燃ダメ(敵/味) | 破片が吸った | 落ちた(敵/味) | 決着T |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

    double aLitF = 0, aLitA = 0, aRelit = 0;
    var aWaveLit = new double[buStages.Count];
    var aWaveRelit = new double[buStages.Count];
    var aWaveDmg = new double[buStages.Count];
    var aWaveTurn = new double[buStages.Count];
    var aWaveFoes = new double[buStages.Count];
    for (int w = 0; w < buStages.Count; w++) aWaveFoes[w] = buStages[w].Enemy.Count;

    // 波ごとの内訳を取るには波を跨がずに集計する必要があるので、行 × 波でもう一度回す
    // （BuRun は行ごとに全波を合算してしまう）。
    foreach (var row in buTargets)
    {
        var playerIds = new HashSet<string>(row.F.Occupied().Select(o => o.Def.Id));
        for (int w = 0; w < buStages.Count; w++)
        {
            double litF = 0, litA = 0, relitF = 0, relitA = 0;
            double tickF = 0, tickA = 0, dmgF = 0, dmgA = 0, soak = 0, deadF = 0, deadA = 0, turns = 0;
            for (int seed = 0; seed < BuSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(row.F, buStages[w].Enemy, seed, verbose: false);
                turns += r.Turns;
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                {
                    bool ally = playerIds.Contains(id) || !buEnemyIds.Contains(id);
                    if (ally) { litA += t.BurnLit; relitA += t.BurnRelit; tickA += t.BurnTicks; dmgA += t.BurnTaken; deadA += t.BurnDeaths; }
                    else      { litF += t.BurnLit; relitF += t.BurnRelit; tickF += t.BurnTicks; dmgF += t.BurnTaken; deadF += t.BurnDeaths; }
                    soak += t.BurnSoaked;
                }
            }
            double n = BuSeeds;
            double lit = litF + litA, relit = relitF + relitA;
            double waste = lit + relit > 0 ? relit * 100.0 / (lit + relit) : 0;
            Console.WriteLine($"| {row.Name} | {w + 1} | {litF / n:F2} | {litA / n:F2} | {relit / n:F2} | "
                              + $"**{waste:F1}%** | {tickF / n:F2}/{tickA / n:F2} | {dmgF / n:F1}/{dmgA / n:F1} | "
                              + $"{soak / n:F2} | {deadF / n:F2}/{deadA / n:F2} | {turns / n:F1} |");
            aLitF += litF; aLitA += litA; aRelit += relit;
            aWaveLit[w] += lit; aWaveRelit[w] += relit; aWaveDmg[w] += dmgF + dmgA; aWaveTurn[w] += turns / n;
        }
    }
    Console.WriteLine();
    double aCells = buTargets.Length * buStages.Count * BuSeeds;
    double aTotLit = aLitF + aLitA;
    double aWaste = aTotLit + aRelit > 0 ? aRelit * 100.0 / (aTotLit + aRelit) : 0;
    Console.WriteLine($"**全体: 着火 {aTotLit / aCells:F2}/戦（敵 {aLitF / aCells:F2} / 味 {aLitA / aCells:F2}）"
                      + $" ／ 再着火 {aRelit / aCells:F2}/戦 ／ 捨て率 {aWaste:F1}%**");
    Console.WriteLine();

    Console.WriteLine("### 表A'. Phase 0-3 の予測の検算（波ルールではなく敵の数と決着Tで決まるか）");
    Console.WriteLine();
    Console.WriteLine("| 波 | 敵の数 | 平均決着T | 着火/戦 | 再着火/戦 | 燃ダメ/戦 | 燃ダメ ÷ (敵数 × 決着T) |");
    Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|");
    for (int w = 0; w < buStages.Count; w++)
    {
        double n = buTargets.Length * BuSeeds;
        double tAvg = aWaveTurn[w] / buTargets.Length;
        Console.WriteLine($"| {w + 1} | {aWaveFoes[w]:F0} | {tAvg:F1} | {aWaveLit[w] / n:F2} | "
                          + $"{aWaveRelit[w] / n:F2} | {aWaveDmg[w] / n:F1} | {aWaveDmg[w] / n / (aWaveFoes[w] * tAvg):F2} |");
    }
    Console.WriteLine();

    // ================= 表B: 位置との接続 =================
    Console.WriteLine("## 表B. 位置との接続（油の判定材料）");
    Console.WriteLine();
    Console.WriteLine("火の粉（`ctx.Ignite(ally)`）と巻き込み（`SplashTrait`）は**同じ隣接規則**"
                      + "（`FormationRules.AreAdjacent`）に乗っている。");
    Console.WriteLine();
    Console.WriteLine("| 行 | ボルグの席 | 次数 | 着火(味)/戦 | 再着火(味)/戦 | 巻き込み量(味)/戦 | 火の粉の帰属(pt) |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|");
    foreach (var row in buTargets)
    {
        int slot = row.F.Occupied().First(o => o.Def.Id == "borg").Slot;
        int deg = FormationRules.PlayableSlots.Count(x => x != slot && FormationRules.AreAdjacent(slot, x));
        var playerIds = new HashSet<string>(row.F.Occupied().Select(o => o.Def.Id));
        double litA = 0, relitA = 0;
        foreach ((string id, UnitTally t) in c0[row.Name].Tally)
        {
            if (buEnemyIds.Contains(id) && !playerIds.Contains(id)) continue;
            litA += t.BurnLit; relitA += t.BurnRelit;
        }
        double n = buStages.Count * BuSeeds;
        double splash = c0[row.Name].Tally.TryGetValue("borg", out UnitTally? bt) ? bt.DamageToAlly / n : 0;
        double attr = c0[row.Name].Win.Average() - c1[row.Name].Win.Average();
        Console.WriteLine($"| {row.Name} | {FormationRules.SeatNames[slot]} | {deg} | {litA / n:F2} | {relitA / n:F2} | "
                          + $"{splash:F1} | **{attr:+0.0;-0.0;0.0}** |");
    }
    Console.WriteLine();

    // ================= 表C: ホタの稼働 =================
    Console.WriteLine("## 表C. ホタの稼働（燃焼 の行・波別）");
    Console.WriteLine();
    Console.WriteLine("**「振った回数のうち燃えていた割合」が稼働率の本体。**");
    Console.WriteLine("ボルグ（速8）はホタ（速7）より速いので、第1ターンから既に点いている可能性がある");
    Console.WriteLine("——供給待ちが存在しないなら **H1 は成立しない**。");
    Console.WriteLine();
    foreach (var row in buHotaRows)
    {
        Console.WriteLine($"### {row.Name}");
        Console.WriteLine();
        Console.WriteLine("| 波 | 振った回数 | **うち燃えていた** | 割合 | 初着火T | 受けた燃焼ダメ | 落ちた率 | 熾火の帰属(pt) |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int w = 0; w < buStages.Count; w++)
        {
            double at = 0, bat = 0, first = 0, firstN = 0, taken = 0, died = 0;
            for (int seed = 0; seed < BuSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(row.F, buStages[w].Enemy, seed, verbose: false);
                if (!r.TallyByUnit.TryGetValue("hota", out UnitTally? t)) continue;
                at += t.Attacks; bat += t.BurnAttacks; taken += t.BurnTaken;
                if (t.FirstBurnTurn > 0) { first += t.FirstBurnTurn; firstN++; }
                if (t.Deaths > 0) died++;
            }
            double n = BuSeeds;
            double attr = c0[row.Name].Win[w] - c2[row.Name].Win[w];
            Console.WriteLine($"| {w + 1} | {at / n:F2} | {bat / n:F2} | **{(at > 0 ? bat * 100.0 / at : 0):F1}%** | "
                              + $"{(firstN > 0 ? first / firstN : 0):F2} | {taken / n:F1} | {died * 100.0 / n:F1}% | "
                              + $"**{attr:+0.0;-0.0;0.0}** |");
        }
        Console.WriteLine();
    }

    // ================= 条件0〜3 の勝率と交互作用 =================
    Console.WriteLine("## 条件0〜3（勝率・波別）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 条件 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    string Row5(string name, string cond, double[] v)
        => $"| {name} | {cond} | " + string.Join(" | ", v.Select(x => $"{x:F1}%")) + $" | **{v.Average():F1}%** |";
    foreach (var row in buTargets)
    {
        Console.WriteLine(Row5(row.Name, "0 現行", c0[row.Name].Win));
        Console.WriteLine(Row5("", "1 火の粉なし", c1[row.Name].Win));
        if (c2.ContainsKey(row.Name)) Console.WriteLine(Row5("", "2 熾火なし", c2[row.Name].Win));
        if (c3.ContainsKey(row.Name)) Console.WriteLine(Row5("", "3 両方素体", c3[row.Name].Win));
    }
    Console.WriteLine();
    foreach (var row in buHotaRows)
    {
        double d1 = c0[row.Name].Win.Average() - c1[row.Name].Win.Average();
        double d2 = c0[row.Name].Win.Average() - c2[row.Name].Win.Average();
        double d3 = c0[row.Name].Win.Average() - c3[row.Name].Win.Average();
        Console.WriteLine($"**{row.Name}**: 火の粉 {d1:+0.0;-0.0;0.0} ／ 熾火 {d2:+0.0;-0.0;0.0} ／ "
                          + $"軸まるごと {d3:+0.0;-0.0;0.0}。**交互作用 = {d3 - d1 - d2:+0.0;-0.0;0.0}pt**（3 −(1+2)）。");
    }
    Console.WriteLine();

    // ================= 表D: 燃焼軸の予算 =================
    Console.WriteLine("## 表D. 燃焼軸の予算（爆弾の判定材料）");
    Console.WriteLine();
    Console.WriteLine("5枠を **供給1 / 読み手1 / それ以外3** に分け、1枚抜きの勝率差（`ablate` 相当）を出す。");
    Console.WriteLine("**3枠を要求する案が入る余地があるかの材料。**");
    Console.WriteLine();
    foreach (var row in buHotaRows)
    {
        Console.WriteLine($"### {row.Name}");
        Console.WriteLine();
        Console.WriteLine("| 抜いた駒 | 役 | 席 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 寄与(pt) |");
        Console.WriteLine("|---|---|:-:|--:|--:|--:|--:|--:|--:|--:|");
        double baseAvg = c0[row.Name].Win.Average();
        foreach ((int slot, UnitDef d) in row.F.Occupied().ToList())
        {
            Formation f = row.F.Clone();
            f[slot] = null;
            var win = new double[buStages.Count];
            for (int w = 0; w < buStages.Count; w++)
            {
                int wins = 0;
                for (int seed = 0; seed < BuSeeds; seed++)
                    if (BattleEngine.Run(f, buStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                win[w] = wins * 100.0 / BuSeeds;
            }
            string role = d.Id == "borg" ? "**供給**" : d.Id == "hota" ? "**読み手**" : "それ以外";
            Console.WriteLine($"| {d.Name} | {role} | {FormationRules.SeatNames[slot]} | "
                              + string.Join(" | ", win.Select(x => $"{x:F1}")) + $" | {win.Average():F1} | "
                              + $"**{baseAvg - win.Average():+0.0;-0.0;0.0}** |");
        }
        Console.WriteLine();
        Console.WriteLine($"土台（5枚そろい）の平均 **{baseAvg:F1}%**。");
        Console.WriteLine();
    }

    // ================= 判定 =================
    Console.WriteLine("## 判定（Q1〜Q5）");
    Console.WriteLine();
    Console.WriteLine($"- **Q1（供給は過剰か・H2）**: 捨て率 **{aWaste:F1}%** → "
                      + (aWaste >= 50 ? "**過剰**" : aWaste < 20 ? "**不足**" : "**中間（波別に分けて読む）**"));
    foreach (var row in buHotaRows)
    {
        double at = 0, bat = 0;
        if (c0[row.Name].Tally.TryGetValue("hota", out UnitTally? ht)) { at = ht.Attacks; bat = ht.BurnAttacks; }
        double pct = at > 0 ? bat * 100.0 / at : 0;
        Console.WriteLine($"- **Q2（ホタは供給を待っているか・H1）**: 燃えている状態で振った割合 **{pct:F1}%** → "
                          + (pct >= 80 ? "**待ちは存在しない ＝ H1 は否定**" : "**待ちが存在する**"));
    }
    Console.WriteLine("- **Q3（燃焼は通貨か・H3）**: 燃焼を読んで分岐する箇所は "
                      + "**engine 1本（`TickStatuses` の刻み）＋ 駒1本（`PyreTrait`）＝ 2本**"
                      + "（`burn phase0` の 0-5）。**他の9通貨との接続は双方向とも 0 本。**");
    Console.Write("- **Q4（火の粉は代金か資産か）**: ホタを含まない行の帰属 → ");
    Console.WriteLine(string.Join(" ／ ", buTargets.Where(r => !c2.ContainsKey(r.Name))
        .Select(r => $"{r.Name} {c0[r.Name].Win.Average() - c1[r.Name].Win.Average():+0.0;-0.0;0.0}")));
    foreach (var row in buHotaRows)
        Console.WriteLine($"- **Q5（第五波の犯人）**: 条件2 で第五波は "
                          + $"{c0[row.Name].Win[4]:F1}% → **{c2[row.Name].Win[4]:F1}%** "
                          + (c2[row.Name].Win[4] > c0[row.Name].Win[4]
                             ? "（**上がった** ＝ 落としているのは台ではなくホタ側）"
                             : "（**上がらない** ＝ 落としているのはホタ側ではない）"));
    Console.WriteLine();
    return;
}
}
