using BattleCore;
using static Common;

// =====================================================================================
// stage scout（第168期） —— 斥候級の敵を作り、道を「斥候 → 主力」で組む
//
// 指示書は design/PHASE168_SCOUT_SPEC.md。前提は第166期（R252・R253）／第167期（R254〜R256）／R251。
//
// **新しい敵の定義は1体も作らない。** 斥候級は `EnemyCatalog.Stages` の 5 席から
// 駒を抜くだけ（数値は1つも変えない・席は元のまま）で、`Stages` / `Columns` には足さない
// ——`slander` / `aim` が波をローカルに組んでいるのと同じ扱い。
// `BattleCore` / `DemoApp` / `UnitCatalog.All` / `Presets` には1行も触らない。
//
//     dotnet run --project BattleSim -c Release 0 stage scout phase0  # 前提（戦闘0回＋見積り）
//     dotnet run --project BattleSim -c Release 0 stage scout a       # 部A 体数の曲線
//     dotnet run --project BattleSim -c Release 0 stage scout b       # 部B マップを斥候→主力で
//     dotnet run --project BattleSim -c Release 0 stage scout check   # 自己検査
// =====================================================================================

static partial class StageDiag
{
    // ---- 形（指示書 §1-1。席は元の席のまま・抜いた席は空ける） ----
    static readonly (string Name, int[] Slots, string Note)[] ScoutForms =
    {
        ("M5",  new[] { 0, 1, 2, 3, 4 }, "5席すべて（対照＝現行の主力級）"),
        ("S4",  new[] { 0, 1, 2, 4 },    "後1 を抜く（支援役を1体減らす）"),
        ("S3r", new[] { 0, 1, 2 },       "前1・前3・中央（**中央＝波ルールの保持者を残す**）"),
        ("S3n", new[] { 0, 1, 4 },       "前1・前3・後3（**波ルールを持たない**）"),
    };

    const int ScoutSeeds = 200;          // 指示書 §1-2
    const double ScoutWinLine = 90.0;    // 斥候級: 勝率の中央値
    const double ScoutAliveLo = 4.0;     // 斥候級: 残り枚数の中央値（下）
    const double ScoutAliveHi = 4.7;     // 同（上）
    const int ScoutWavesNeeded = 2;      // 線A: 4波のうち何波で斥候級が立てばよいか
    const int ScoutRecover = 50;         // 部B の道中の回復（第167期: 50% で飽和）

    /// <summary>波 <paramref name="wave"/>（0 始まり）の形 <paramref name="form"/>。</summary>
    static Formation ScoutSquad(int wave, int form)
    {
        var src = EnemyCatalog.Stages[wave].Enemy;
        var f = new Formation();
        foreach (int s in ScoutForms[form].Slots) f[s] = src[s];
        return f;
    }

    /// <summary>ある編成の、ある敵に対する 勝率／勝った試行の平均生存枚数／同 残り HP 割合。</summary>
    static (double Win, double Alive, double Hp) ScoutOne(Formation f, Formation foe, int seeds)
    {
        int win = 0; double al = 0, hp = 0;
        for (int seed = 0; seed < seeds; seed++)
        {
            var pu = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var eu = BattleEngine.Materialize(foe, BattleContext.EnemyTeam);
            if (!BattleEngine.Run(pu, eu, seed, verbose: false).PlayerWon) continue;
            win++;
            al += pu.Count(u => u.IsAlive);
            hp += 100.0 * pu.Where(u => u.IsAlive).Sum(u => u.Hp) / pu.Sum(u => u.Def.MaxHp);
        }
        return (100.0 * win / seeds, win == 0 ? 0 : al / win, win == 0 ? 0 : hp / win);
    }

    /// <summary>`CompareBuilds()` 61 行ぶんの、1 敵編成に対する表。</summary>
    static (double[] Win, double[] Alive, double[] Hp) ScoutRows(Formation foe)
    {
        var rows = CompareBuilds();
        var w = new double[rows.Length]; var a = new double[rows.Length]; var h = new double[rows.Length];
        Parallel.For(0, rows.Length, i =>
        {
            var r = ScoutOne(rows[i].F, foe, ScoutSeeds);
            w[i] = r.Win; a[i] = r.Alive; h[i] = r.Hp;
        });
        return (w, a, h);
    }

    static double ScoutPct(double[] v, double q)
    {
        var s = v.OrderBy(x => x).ToArray();
        if (s.Length == 0) return 0;
        double p = q * (s.Length - 1);
        int lo = (int)Math.Floor(p), hi = (int)Math.Ceiling(p);
        return s[lo] + (s[hi] - s[lo]) * (p - lo);
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void ScoutPhase0()
    {
        Console.WriteLine("# 第168期 Phase 0 —— 斥候級");
        Console.WriteLine();

        Console.WriteLine("## Q0-1: 16 編成の中身（抜けた駒・残った駒・波ルールの保持者の席）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 形 | 残る駒（席:名前） | 抜けた駒 | 盤面ルール |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int w = 1; w <= 4; w++)
            for (int k = 0; k < ScoutForms.Length; k++)
            {
                var sq = ScoutSquad(w, k);
                var src = EnemyCatalog.Stages[w].Enemy;
                var gone = src.Occupied().Where(o => sq[o.Slot] is null)
                    .Select(o => $"{SlotName[o.Slot]}:{o.Def.Name}").ToArray();
                var rules = sq.Occupied()
                    .SelectMany(o => o.Def.Traits
                        .Where(t => t is TraitId.Hush or TraitId.Drought or TraitId.Yoke or TraitId.Inversion)
                        .Select(t => $"{t}（{SlotName[o.Slot]}:{o.Def.Name}）"))
                    .ToArray();
                Console.WriteLine($"| 第{w + 1}波 | {ScoutForms[k].Name} | {Show(sq)} "
                    + $"| {(gone.Length == 0 ? "—" : string.Join(" / ", gone))} "
                    + $"| {(rules.Length == 0 ? "**なし**" : string.Join(" / ", rules))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"参考（第一波・現行のまま）: {Show(EnemyCatalog.Stages[0].Enemy)}");
        Console.WriteLine();
        Console.WriteLine("**engine の盤面ルール（`TraitId` を走査して引いた）が中央にあるのは 第二〜四波の 3 波だけ。**");
        Console.WriteLine("第五波の中央（告発人・曝き）は engine 側の盤面ルールではなく**敵側の特性**なので、");
        Console.WriteLine("**第五波の `S3r` / `S3n` は「盤面ルールの有無」ではなく「曝きの有無」を切っている。**");
        Console.WriteLine("指示書 §1-1 の注記どおり **`S3n` でも殉教者（前1・庇う）と勇者候補（前3・断罪）は残る。**");
        Console.WriteLine();

        Console.WriteLine("## Q0-2: 5 席未満の `Formation` を敵として回せるか");
        Console.WriteLine();
        Console.WriteLine("**回せる。** 3 点を実装から確かめた:");
        Console.WriteLine();
        Console.WriteLine("1. **`Formation` は `UnitDef?[5]`** で、`Occupied()` は null 以外だけを返す。");
        Console.WriteLine("   `BattleEngine.Materialize` は `Occupied()` を回すので、**空席は「最初からいない」と同じ**。");
        Console.WriteLine($"   現に第一波は 3 体（{Show(EnemyCatalog.Stages[0].Enemy)}）で 168 期ぶん回っている。");
        Console.WriteLine("2. **貫きの経路は席ではなく生存者で引く**（`SelectPierceEntry` → `LaneOccupants` が");
        Console.WriteLine("   `alive.Where(u => u.Slot == slot)`）。生存者のいないレーンは候補から落ち、");
        Console.WriteLine("   両レーンとも空なら `lane = -1` で単体として 1 体刺す——**空席と死亡が同じ扱い**。");
        Console.WriteLine("3. **殉教者の庇いは `f.Row == Row.Front && f != target`** なので、");
        Console.WriteLine("   前列 2 枚が残るすべての形で窓は開く（**4 形とも 前1・前3 は残る**）。");
        Console.WriteLine();
        Console.WriteLine("**つまり「席を空ける」は「その駒が開戦前に死んでいる」と同値**で、新しい経路は 1 本も通らない。");
        Console.WriteLine();

        Console.WriteLine("## Q0-3: 波ルールは保持者がいなければ完全に消えるか");
        Console.WriteLine();
        Console.WriteLine("**消える。開戦時の焼き付けではなく、毎回の生存判定である。**");
        Console.WriteLine();
        Console.WriteLine("- 保持者は `BattleContext.Add`（＝`Materialize` の入口）で `_yokeHolders` /");
        Console.WriteLine("  `_droughtHolders` / `_hushHolders` / `_inversionHolders` に積まれる");
        Console.WriteLine("  ——**席を空ければ `Add` を通らないのでリストが空になる。**");
        Console.WriteLine("- 効いているかの判定は `AnyAlive(holders)`（`DroughtBinding` / `HushHolderAlive` /");
        Console.WriteLine("  `YokeBinding`）で、**呼ばれるたびに走る**。`holders.Count == 0` なら常に偽。");
        Console.WriteLine("- **だから `S3n` は「保持者が倒れた後」ではなく「最初から 1 度も効かない」版である。**");
        Console.WriteLine("  `S3r` は現行と同じく「割ればその場で解禁される」（第110期の粛・第118期の渇きの設計）。");
        Console.WriteLine();

        Console.WriteLine("## Q0-4: 敵の体数を減らして測った期");
        Console.WriteLine();
        Console.WriteLine("| 期 | 何をしたか | この期との違い |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 第51期 `wave2` | **敵を 1 体ずつ空席にして関門を数えた**（波の解剖） "
            + "| 器具の向きが逆——あちらは「どの 1 体が難度を作っているか」の帰属で、**斥候級を作るためではない** |");
        Console.WriteLine("| 第5〜8期 `gradient` / `aim` / `flip` | 体数と個体値を振って代金の向きを作った "
            + "| **新しい敵の定義を作っている**（農兵・人足・狂信者・従卒…）。この期は定義を 1 つも作らない |");
        Console.WriteLine("| 第9期 `ENGAGEMENT_PLAN_9.md` | 攻撃パターン軸を打ち切った "
            + "| 「体数を減らすと向きが一緒に消える」の実測。**難度を下げる目的で体数を減らすのは初めて** |");
        Console.WriteLine();
        Console.WriteLine("**`design/ENEMY_FORMATION_PLAN.md` はリポジトリに存在しない**（指示書 Q0-4 の想定どおり）。");
        Console.WriteLine();

        Console.WriteLine("## Q0-5: 走行時間の見積もり");
        Console.WriteLine();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ScoutRows(EnemyCatalog.Stages[1].Enemy);
        sw.Stop();
        double one = sw.Elapsed.TotalSeconds;
        Console.WriteLine($"1 編成（61 行 × seed {ScoutSeeds}）が **{one:F2} 秒**。");
        Console.WriteLine($"部A の表1 は 17 編成で **約 {one * 17:F0} 秒**、"
            + $"表3（3 隊 × 17 編成）が約 {one * 17 * 3 / 61:F1} 秒。");
        Console.WriteLine("部B は第167期の `stage band` と同形（1 点 ≒ 0.2 秒）で "
            + "**2 形 × 2 回復 × 2 割り当て × 2 帯 ＋ 対照 4 点 ＝ 20 点**。");
        Console.WriteLine($"**合計の見積もりは {one * 17 + one * 17 * 3 / 61 + 10:F0} 秒前後で、10 分の線を大きく下回る。**");
        Console.WriteLine();

        Console.WriteLine("## Phase 0 の後に足した予測（Claude Code 側）");
        Console.WriteLine();
        Console.WriteLine("| # | 予測 |");
        Console.WriteLine("|---|---|");
        foreach (var (k, t) in ScoutPredictions) Console.WriteLine($"| {k} | {t} |");
    }

    public static readonly (string Key, string Text)[] ScoutPredictions =
    {
        ("K1", "**`S4`（後1 を抜く）の効きは波で 2 桁違う** —— 後1 は "
             + "第二波＝施しの司祭長（毎ターン 14 を配る＝実効HP）／第三波＝狙撃手（溜め貫き）／"
             + "第四波＝詠唱兵（溜め全体）／第五波＝審問官（全体＋断罪）。"
             + "**第二波の `S4` は残り枚数をほとんど増やさず、第四・五波の `S4` は大きく増やす**"),
        ("K2", "**`S3r` と `S3n` の差は第四波で最大**（軛は味方の大打点を切る＝味方に高く付くので、"
             + "外すと残り枚数が跳ねる）。**第三波の渇きは回復役を持つ行にしか効かない**ので差が小さい"),
        ("K3", "**斥候級の線（残り枚数 4.0〜4.7）はいちばん軽い `S3n` でも通らない波がある** ——"
             + "第167期の実測で M5 が 2.67〜3.50 枚。**通るとしたら第二波と第三波**（元から軽い側）で、"
             + "第五波は `S3n` でも殉教者と断罪が残るので届かない"),
        ("K4", "**W3 は当たる** —— カド隊 × 第二波の 0.0% は粛（`S3r` では残る）だけが原因ではなく "
             + "**カドが自分の手番を持たない（`Immobile`）こと**が本体なので、"
             + "**`S3n`（粛なし）でも 100% には戻らない**。R251 は体数に依らない"),
        ("K5", "**部B の失敗の集中は北の 2 戦目（第三波）に残る** —— 第167期で 74〜99% がそこ。"
             + "1 戦目を軽くしても 2 戦目は M5 のままなので、**関門は動かない**"),
    };

    // =================================================================================
    // 部A —— 体数の曲線
    // =================================================================================

    static void ScoutPartA()
    {
        var rows = CompareBuilds();
        Console.WriteLine("# 第168期 部A —— 体数の曲線（斥候級を探す）");
        Console.WriteLine();
        Console.WriteLine($"`CompareBuilds()` **{rows.Length} 行** × **17 編成** × seed 0..{ScoutSeeds - 1}。"
            + "**新品どうしの単発戦**（持ち越しなし）。");
        Console.WriteLine();
        Console.WriteLine($"**斥候級 ＝ 勝率の中央値 ≥ {ScoutWinLine:F0}%、かつ残り枚数の中央値が "
            + $"{ScoutAliveLo:F1}〜{ScoutAliveHi:F1} 枚**（指示書 §1-3。測る前に固定した線）。");
        Console.WriteLine("**残り枚数・残り HP は「勝った試行」を分母にする**ので、"
            + "勝ちが 1 度も無い行はその列から外してある（0 は未観測であって 0 枚ではない）。");
        Console.WriteLine();

        var cells = new Dictionary<(int W, int K), (double[] W, double[] A, double[] H)>();
        for (int w = 0; w <= 4; w++)
            for (int k = 0; k < ScoutForms.Length; k++)
            {
                if (w == 0 && k > 0) continue;              // 第一波は現行のまま 1 つだけ
                cells[(w, k)] = ScoutRows(w == 0 ? EnemyCatalog.Stages[0].Enemy : ScoutSquad(w, k));
            }

        Console.WriteLine("## 1. 波 × 形（表1）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 形 | 体数 | 勝率 中央 | 勝率 平均 | 勝率 最小 | **残り枚数 中央** "
            + "| 残り HP | 情報セル | 残り枚数の幅(10〜90%) | 斥候級 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|:-:|");
        var pass = new bool[5, ScoutForms.Length];
        var aliveMed = new double[5, ScoutForms.Length];
        var winMed = new double[5, ScoutForms.Length];
        for (int w = 0; w <= 4; w++)
            for (int k = 0; k < ScoutForms.Length; k++)
            {
                if (!cells.TryGetValue((w, k), out var c)) continue;
                int n = (w == 0 ? EnemyCatalog.Stages[0].Enemy : ScoutSquad(w, k)).Count;
                var av = Enumerable.Range(0, rows.Length).Where(i => c.W[i] > 0).Select(i => c.A[i]).ToArray();
                var hv = Enumerable.Range(0, rows.Length).Where(i => c.W[i] > 0).Select(i => c.H[i]).ToArray();
                double wm = Median(c.W), am = av.Length == 0 ? 0 : Median(av);
                winMed[w, k] = wm; aliveMed[w, k] = am;
                int info = c.W.Count(x => x > 5.0 && x < 95.0);
                bool ok = w > 0 && wm >= ScoutWinLine && am >= ScoutAliveLo && am <= ScoutAliveHi;
                pass[w, k] = ok;
                Console.WriteLine($"| 第{w + 1}波 | {(w == 0 ? "（現行）" : ScoutForms[k].Name)} | {n} "
                    + $"| {wm:F1}% | {c.W.Average():F1}% | {c.W.Min():F1}% | **{am:F2}** 枚 "
                    + $"| {(hv.Length == 0 ? 0 : Median(hv)):F1}% | {info} "
                    + $"| {(av.Length == 0 ? 0 : ScoutPct(av, 0.9) - ScoutPct(av, 0.1)):F2} "
                    + $"| {(w == 0 ? "—" : ok ? "**○**" : "×")} |");
            }
        Console.WriteLine();

        Console.WriteLine("### 対照の確認 —— M5 の残り枚数の中央値は 2.5〜3.5 枚に入るか（指示書 §1-3）");
        Console.WriteLine();
        Console.WriteLine("| 波 | M5 の残り枚数 中央 | 2.5〜3.5 |");
        Console.WriteLine("|---|--:|:-:|");
        for (int w = 1; w <= 4; w++)
            Console.WriteLine($"| 第{w + 1}波 | {aliveMed[w, 0]:F2} 枚 "
                + $"| {(aliveMed[w, 0] >= 2.5 && aliveMed[w, 0] <= 3.5 ? "**○**" : "×")} |");
        Console.WriteLine();
        Console.WriteLine($"参考: 第一波（現行・3 体）は 残り枚数 中央 **{aliveMed[0, 0]:F2} 枚**"
            + $"・勝率 中央 {winMed[0, 0]:F1}%・情報セル "
            + $"{cells[(0, 0)].W.Count(x => x > 5.0 && x < 95.0)}。");
        Console.WriteLine();

        int waveOk = Enumerable.Range(1, 4)
            .Count(w => Enumerable.Range(0, ScoutForms.Length).Any(k => pass[w, k]));
        Console.WriteLine("## 2. 線A の判定");
        Console.WriteLine();
        Console.WriteLine($"**線A: 4 波のうち {ScoutWavesNeeded} 波以上で、斥候級の形が 1 つ以上ある。**");
        Console.WriteLine();
        Console.WriteLine("| 波 | 斥候級になった形 | 残り枚数が 4.0 に最も近い形 |");
        Console.WriteLine("|---|---|---|");
        for (int w = 1; w <= 4; w++)
        {
            var okForms = Enumerable.Range(0, ScoutForms.Length).Where(k => pass[w, k])
                .Select(k => ScoutForms[k].Name).ToArray();
            int near = Enumerable.Range(0, ScoutForms.Length)
                .OrderBy(k => Math.Abs(aliveMed[w, k] - ScoutAliveLo)).First();
            Console.WriteLine($"| 第{w + 1}波 | {(okForms.Length == 0 ? "**なし**" : string.Join(" / ", okForms))} "
                + $"| {ScoutForms[near].Name}（{aliveMed[w, near]:F2} 枚・勝率中央 {winMed[w, near]:F1}%） |");
        }
        Console.WriteLine();
        Console.WriteLine(waveOk >= ScoutWavesNeeded
            ? $"**線A: ○（{waveOk} 波）—— 部B へ進む。**"
            : $"**線A: ×（{waveOk} 波）—— 指示書 §1-3 により部B に進まない。**");
        Console.WriteLine();

        Console.WriteLine("## 3. 残り枚数が形で大きく変わる編成・変わらない編成（表2）");
        Console.WriteLine();
        Console.WriteLine("**幅 ＝ その行の 残り枚数 の（形の最大 − 最小）を 4 波で平均したもの**"
            + "（4 形のどれかに勝ちが 1 度も無い波は、その波を飛ばす）。");
        Console.WriteLine();
        var spread = new double[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var xs = new List<double>();
            for (int w = 1; w <= 4; w++)
            {
                var v = Enumerable.Range(0, ScoutForms.Length)
                    .Where(k => cells[(w, k)].W[i] > 0).Select(k => cells[(w, k)].A[i]).ToArray();
                if (v.Length == ScoutForms.Length) xs.Add(v.Max() - v.Min());
            }
            spread[i] = xs.Count == 0 ? double.NaN : xs.Average();
        }
        var valid = Enumerable.Range(0, rows.Length).Where(i => !double.IsNaN(spread[i])).ToArray();
        Console.WriteLine($"（幅が測れた行は **{valid.Length} / {rows.Length}**。"
            + "残りは 4 波のどこかで 4 形が揃わなかった行）");
        Console.WriteLine();
        foreach (var (label, ord) in new[]
                 {
                     ("大きく変わる", valid.OrderByDescending(i => spread[i])),
                     ("変わらない", valid.OrderBy(i => spread[i])),
                 })
        {
            Console.WriteLine($"### {label} 上位5行");
            Console.WriteLine();
            Console.WriteLine("| 行 | 幅（枚） | M5 | S4 | S3r | S3n |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|");
            foreach (int i in ord.Take(5))
            {
                var m = Enumerable.Range(0, ScoutForms.Length).Select(k =>
                    Enumerable.Range(1, 4).Where(w => cells[(w, k)].W[i] > 0)
                        .Select(w => cells[(w, k)].A[i]).DefaultIfEmpty(0).Average()).ToArray();
                Console.WriteLine($"| `{rows[i].Name}` | **{spread[i]:F2}** "
                    + $"| {m[0]:F2} | {m[1]:F2} | {m[2]:F2} | {m[3]:F2} |");
            }
            Console.WriteLine("（列は第2〜5波の平均 残り枚数）");
            Console.WriteLine();
        }

        Console.WriteLine("## 4. カド隊・ハネ隊・第3隊 × 17 編成（表3）");
        Console.WriteLine();
        var (sa, sb, subs) = ScoutSquads(out string fillerName, out var fillerTable);
        var (cand, med) = BandReserve(sa, sb, BandProbeSeeds);
        Console.WriteLine($"カド隊 {Show(sa)}");
        Console.WriteLine();
        Console.WriteLine($"ハネ隊 {Show(sb)}"
            + (subs.Length == 0 ? "" : $"（{string.Join(" / ", subs.Select(s => $"{s.Slot} {s.From} → {s.To}"))}）"));
        Console.WriteLine();
        Console.WriteLine($"第3隊 `{cand[0].Name}`（61 行の単発勝率の中央値 {med:F1}%）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 形 | カド隊 勝率 | 残り枚数 | ハネ隊 勝率 | 残り枚数 | 第3隊 勝率 | 残り枚数 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        for (int w = 0; w <= 4; w++)
            for (int k = 0; k < ScoutForms.Length; k++)
            {
                if (w == 0 && k > 0) continue;
                var foe = w == 0 ? EnemyCatalog.Stages[0].Enemy : ScoutSquad(w, k);
                var x = new[] { sa, sb, cand[0].F }.Select(f => ScoutOne(f, foe, ScoutSeeds)).ToArray();
                Console.WriteLine($"| 第{w + 1}波 | {(w == 0 ? "（現行）" : ScoutForms[k].Name)} | "
                    + string.Join(" | ", x.Select(v => $"{v.Win:F1}% | {v.Alive:F2}")) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("### ハネ隊の埋め草の選定（指示書 §2-2）");
        Console.WriteLine();
        Console.WriteLine("**「どこでも同じ」群の `checkup` 単独 上位5枚を試し、"
            + "ハネ隊の第2〜5波の単発勝率の平均が最も高い 1 枚を採る。**");
        Console.WriteLine();
        Console.WriteLine("| 順 | 駒 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 採用 |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|:-:|");
        foreach (var t in fillerTable)
            Console.WriteLine($"| {t.Rank} | {t.Name} | "
                + string.Join(" | ", t.Solo.Select(v => $"{v:F1}%"))
                + $" | **{t.Solo.Average():F1}%** | {(t.Name == fillerName ? "**○**" : "—")} |");
        Console.WriteLine();
        Console.WriteLine("（第167期は `MapSteadyBySolo` の先頭 1 枚を無条件に採っていた"
            + "——**この期は 5 枚を測ってから採る**。第167期 F1 の直しの続き）");
    }

    // =================================================================================
    // 部B —— マップを「斥候 → 主力」で回し直す
    // =================================================================================

    /// <summary>
    /// 2 隊を組む。カド隊は <c>反撃 (ヒサ×カド)</c> のまま。重なった枠は
    /// <b>「どこでも同じ」群の上位5枚を実際に測って、ハネ隊がいちばん強くなる 1 枚</b>で埋める。
    /// </summary>
    static (Formation A, Formation B, (string Slot, string From, string To)[] Sub) ScoutSquads(
        out string pickedName, out (int Rank, string Name, double[] Solo)[] table)
    {
        Formation a = FormOf(BandRowA), b0 = FormOf(BandRowB);
        var inA = new HashSet<string>(a.Occupied().Select(o => o.Def.Id));
        var used = new HashSet<string>(inA);
        foreach (var o in b0.Occupied()) used.Add(o.Def.Id);

        var cands = MapSteadyBySolo.Where(x => !used.Contains(x)).Take(5).ToArray();
        var rank = new List<(int, string, double[])>();
        string best = cands[0];
        double bestMean = double.NegativeInfinity;
        for (int c = 0; c < cands.Length; c++)
        {
            var trial = new Formation();
            foreach (var o in b0.Occupied())
                trial[o.Slot] = inA.Contains(o.Def.Id) ? Def(cands[c]) : o.Def;
            var solo = BandSolo(trial, BandProbeSeeds);
            rank.Add((c + 1, Def(cands[c]).Name, solo));
            if (solo.Average() > bestMean) { bestMean = solo.Average(); best = cands[c]; }
        }
        table = rank.ToArray();
        pickedName = Def(best).Name;

        var b = new Formation();
        var subs = new List<(string, string, string)>();
        foreach (var o in b0.Occupied())
        {
            if (!inA.Contains(o.Def.Id)) { b[o.Slot] = o.Def; continue; }
            b[o.Slot] = Def(best);
            subs.Add((SlotName[o.Slot], o.Def.Name, Def(best).Name));
        }
        return (a, b, subs.ToArray());
    }

    /// <summary>道ごとの形（北＝第二波の形／南＝第四波の形）。同じでも違ってもよい。</summary>
    readonly record struct ScoutPair(int North, int South)
    {
        public string Name => North == South ? ScoutForms[North].Name
            : $"{ScoutForms[North].Name}/{ScoutForms[South].Name}";
    }

    static void ScoutPartB(ScoutPair[] forms)
    {
        var (a, b, subs) = ScoutSquads(out string filler, out _);
        var (cand, med) = BandReserve(a, b, BandProbeSeeds);
        Formation res = cand[0].F;
        var squads = new[] { a, b };
        int[] straight = { 1, 0 };   // 正: カド隊 → 南(1) ／ ハネ隊 → 北(0)
        int[] cross = { 0, 1 };      // 逆

        Console.WriteLine("# 第168期 部B —— マップを「斥候 → 主力」で回し直す");
        Console.WriteLine();
        Console.WriteLine("**道: 北 ＝ 第二波の斥候級 → 第三波（M5）／ 南 ＝ 第四波の斥候級 → 第五波（M5）。**");
        Console.WriteLine();
        Console.WriteLine($"カド隊 {Show(a)}");
        Console.WriteLine();
        Console.WriteLine($"ハネ隊 {Show(b)}"
            + (subs.Length == 0 ? "" : $"（{string.Join(" / ", subs.Select(s => $"{s.Slot} {s.From} → {s.To}"))}）"));
        Console.WriteLine();
        Console.WriteLine($"第3隊 `{cand[0].Name}`（中央値 {med:F1}%）。"
            + $"道中の回復は **{ScoutRecover}%**（第167期: 50% で飽和）。対照として 0% も回す。");
        Console.WriteLine($"seed 帯 2 本（0..{BandSeeds - 1} / {BandSeeds}..{2 * BandSeeds - 1}）。");
        Console.WriteLine();

        int[] pcts = { 0, ScoutRecover };
        var P = new Dictionary<(ScoutPair K, int P, int B), BandStat>();
        var Q = new Dictionary<(ScoutPair K, int P, int B), BandStat>();
        foreach (var k in forms)
        {
            var roads = new[]
            {
                (IReadOnlyList<Formation>)new[] { ScoutSquad(1, k.North), EnemyCatalog.Stages[2].Enemy },
                (IReadOnlyList<Formation>)new[] { ScoutSquad(3, k.South), EnemyCatalog.Stages[4].Enemy },
            };
            for (int p = 0; p < pcts.Length; p++)
                for (int bd = 0; bd < 2; bd++)
                {
                    P[(k, p, bd)] = BandBand(squads, res, straight, roads, bd * BandSeeds, pcts[p]);
                    Q[(k, p, bd)] = BandBand(squads, res, cross, roads, bd * BandSeeds, pcts[p]);
                }
        }

        // 対照列（第167期・M5 → M5・回復 50%・第167期の隊）
        var (ma, mb, _) = BandSquads();
        var mroads = MapRoads.Select(r => (IReadOnlyList<Formation>)ColOf(r.Col).Squads).ToArray();
        var (mcand, _) = BandReserve(ma, mb, BandProbeSeeds);
        var C = new BandStat[2, 2];  // [正/逆, 帯]
        for (int bd = 0; bd < 2; bd++)
        {
            C[0, bd] = BandBand(new[] { ma, mb }, mcand[0].F, straight, mroads, bd * BandSeeds, ScoutRecover);
            C[1, bd] = BandBand(new[] { ma, mb }, mcand[0].F, cross, mroads, bd * BandSeeds, ScoutRecover);
        }

        Console.WriteLine("## 1. 2 本抜き率（線1）");
        Console.WriteLine();
        Console.WriteLine("**2 本抜き率 ＝ 1 隊が自分の道の 2 部隊を続けて抜いた割合**（第3隊の加勢なし）。"
            + "正の割り当ては カド隊 → 南 ／ ハネ隊 → 北、逆はその入れ替え。");
        Console.WriteLine();
        Console.WriteLine("| 形 | 回復 | カド隊×南（正） | ハネ隊×北（正） | 線1 "
            + "| カド隊×北（逆） | ハネ隊×南（逆） | 踏破率 正 | 踏破率 逆 |");
        Console.WriteLine("|---|--:|--:|--:|:-:|--:|--:|--:|--:|");
        var pass1 = new Dictionary<(ScoutPair, int), bool>();
        foreach (var k in forms)
            for (int p = 0; p < pcts.Length; p++)
            {
                double ka = P[(k, p, 0)].TwoA, ha = P[(k, p, 0)].TwoB;
                bool inA = ka >= BandLo && ka <= BandHi, inB = ha >= BandLo && ha <= BandHi;
                pass1[(k, p)] = inA && inB;
                Console.WriteLine($"| {k.Name} | {pcts[p]}% | {ka:F1}% | {ha:F1}% "
                    + $"| {(inA && inB ? "**○**" : inA || inB ? "△" : "×")} "
                    + $"| {Q[(k, p, 0)].TwoA:F1}% | {Q[(k, p, 0)].TwoB:F1}% "
                    + $"| {P[(k, p, 0)].Full:F1}% | {Q[(k, p, 0)].Full:F1}% |");
            }
        Console.WriteLine($"| **対照 M5→M5** | {ScoutRecover}% | {C[0, 0].TwoA:F1}% | {C[0, 0].TwoB:F1}% | — "
            + $"| {C[1, 0].TwoA:F1}% | {C[1, 0].TwoB:F1}% | {C[0, 0].Full:F1}% | {C[1, 0].Full:F1}% |");
        Console.WriteLine();
        foreach (var k in forms)
            for (int p = 0; p < pcts.Length; p++)
            {
                double ka = P[(k, p, 0)].TwoA, ha = P[(k, p, 0)].TwoB;
                bool inA = ka >= BandLo && ka <= BandHi, inB = ha >= BandLo && ha <= BandHi;
                if (inA ^ inB)
                    Console.WriteLine($"- {k.Name} / 回復 {pcts[p]}%: **△** —— "
                        + $"{(inA ? "ハネ隊×北" : "カド隊×南")} が "
                        + $"{((inA ? ha : ka) < BandLo ? "下" : "上")}へ外れた（{(inA ? ha : ka):F1}%）");
            }
        Console.WriteLine();
        Console.WriteLine("### 1 戦目の勝率と、抜いた時点の残り（**マップとは独立に新品どうしで 1 戦だけ**）");
        Console.WriteLine();
        Console.WriteLine("| 形 | 隊 | 道 | 1 戦目 | 勝率 | 残り HP 割合 | 生存枚数 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|");
        foreach (var k in forms)
            foreach (var (nm, f) in new[] { ("カド隊", a), ("ハネ隊", b) })
                foreach (var (rn, wv) in new[] { ("北の道", 1), ("南の道", 3) })
                {
                    var v = ScoutOne(f, ScoutSquad(wv, wv == 1 ? k.North : k.South), BandProbeSeeds);
                    Console.WriteLine($"| {k.Name} | {nm} | {rn} | 第{wv + 1}波 斥候 "
                        + $"| {v.Win:F1}% | {v.Hp:F1}% | {v.Alive:F2} 枚 |");
                }
        Console.WriteLine();

        Console.WriteLine("## 2. 部分点と対比（線2）");
        Console.WriteLine();
        Console.WriteLine("**部分点 ＝ 抜いた敵部隊の数（0〜4）＋ 残った敵部隊の削り**"
            + "（失った HP ÷ 定義上の総最大 HP）。**対比 ＝ 正 − 逆。**");
        Console.WriteLine();
        Console.WriteLine("| 形 | 回復 | 正 | 逆 | **対比** | 帯1 の再現（正/逆） | ノイズ | 対比÷ノイズ | 線2 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|:-:|");
        var pass2 = new Dictionary<(ScoutPair, int), bool>();
        foreach (var k in forms)
            for (int p = 0; p < pcts.Length; p++)
            {
                double c0 = P[(k, p, 0)].Partial - Q[(k, p, 0)].Partial;
                double c1 = P[(k, p, 1)].Partial - Q[(k, p, 1)].Partial;
                double noise = (Math.Abs(P[(k, p, 0)].Partial - P[(k, p, 1)].Partial)
                              + Math.Abs(Q[(k, p, 0)].Partial - Q[(k, p, 1)].Partial)) / 2.0;
                double ratio = noise <= 0 ? double.PositiveInfinity : Math.Abs(c0) / noise;
                pass2[(k, p)] = c0 >= BandContrastLine && ratio >= BandNoiseMul;
                Console.WriteLine($"| {k.Name} | {pcts[p]}% | {P[(k, p, 0)].Partial:F3} "
                    + $"| {Q[(k, p, 0)].Partial:F3} | **{c0:+0.000;-0.000}** "
                    + $"| {P[(k, p, 1)].Partial:F3} / {Q[(k, p, 1)].Partial:F3} | {noise:F3} "
                    + $"| {(double.IsInfinity(ratio) ? "∞" : ratio.ToString("F1"))} "
                    + $"| {(pass2[(k, p)] ? "**○**" : "×")} |（再現 {c1:+0.000;-0.000}）");
            }
        {
            double c0 = C[0, 0].Partial - C[1, 0].Partial;
            double noise = (Math.Abs(C[0, 0].Partial - C[0, 1].Partial)
                          + Math.Abs(C[1, 0].Partial - C[1, 1].Partial)) / 2.0;
            Console.WriteLine($"| **対照 M5→M5** | {ScoutRecover}% | {C[0, 0].Partial:F3} | {C[1, 0].Partial:F3} "
                + $"| **{c0:+0.000;-0.000}** | {C[0, 1].Partial:F3} / {C[1, 1].Partial:F3} | {noise:F3} "
                + $"| {(noise <= 0 ? "∞" : (Math.Abs(c0) / noise).ToString("F1"))} | — |");
        }
        Console.WriteLine();

        Console.WriteLine("## 3. 挽回率（線3）");
        Console.WriteLine();
        Console.WriteLine("**挽回率 ＝ 第3隊が出た seed のうち、第3隊がその道を抜き切った割合。**");
        Console.WriteLine();
        Console.WriteLine("| 形 | 回復 | 正 出た | 正 挽回率 | 逆 出た | 逆 挽回率 | 線3（逆） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|:-:|");
        var pass3 = new Dictionary<(ScoutPair, int), bool>();
        foreach (var k in forms)
            for (int p = 0; p < pcts.Length; p++)
            {
                double rq = Q[(k, p, 0)].Rally;
                pass3[(k, p)] = rq >= BandRallyLo && rq <= BandRallyHi;
                Console.WriteLine($"| {k.Name} | {pcts[p]}% | {P[(k, p, 0)].ReserveN} / {BandSeeds} "
                    + $"| {P[(k, p, 0)].Rally:F1}% | {Q[(k, p, 0)].ReserveN} / {BandSeeds} "
                    + $"| **{rq:F1}%** | {(pass3[(k, p)] ? "**○**" : "×")} |");
            }
        Console.WriteLine($"| **対照 M5→M5** | {ScoutRecover}% | {C[0, 0].ReserveN} / {BandSeeds} "
            + $"| {C[0, 0].Rally:F1}% | {C[1, 0].ReserveN} / {BandSeeds} | **{C[1, 0].Rally:F1}%** | — |");
        Console.WriteLine();

        Console.WriteLine("## 4. 失敗の内訳（どの道の何戦目で止まったか）");
        Console.WriteLine();
        Console.WriteLine("| 形 | 回復 | 割当 | 失敗数 | 北 1戦目(第二波 斥候) | 北 2戦目(第三波) "
            + "| 南 1戦目(第四波 斥候) | 南 2戦目(第五波) | 平均戦闘回数 |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|");
        foreach (var k in forms)
            for (int p = 0; p < pcts.Length; p++)
                foreach (var (nm, st) in new[] { ("正", P[(k, p, 0)]), ("逆", Q[(k, p, 0)]) })
                {
                    int fail = (int)Math.Round(BandSeeds * (100.0 - st.Full) / 100.0);
                    Console.WriteLine($"| {k.Name} | {pcts[p]}% | {nm} | {fail} "
                        + $"| {st.StopAt[0, 0]} | {st.StopAt[0, 1]} | {st.StopAt[1, 0]} | {st.StopAt[1, 1]} "
                        + $"| {st.Battles:F2} |");
                }
        foreach (var (nm, st) in new[] { ("正", C[0, 0]), ("逆", C[1, 0]) })
        {
            int fail = (int)Math.Round(BandSeeds * (100.0 - st.Full) / 100.0);
            Console.WriteLine($"| **対照 M5→M5** | {ScoutRecover}% | {nm} | {fail} "
                + $"| {st.StopAt[0, 0]} | {st.StopAt[0, 1]} | {st.StopAt[1, 0]} | {st.StopAt[1, 1]} "
                + $"| {st.Battles:F2} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 5. 判定");
        Console.WriteLine();
        Console.WriteLine("| 形 | 回復 | 線1 | 線2 | 線3 | 本丸（3 つ同時） |");
        Console.WriteLine("|---|--:|:-:|:-:|:-:|:-:|");
        bool main = false;
        foreach (var k in forms)
            for (int p = 0; p < pcts.Length; p++)
            {
                bool all = pass1[(k, p)] && pass2[(k, p)] && pass3[(k, p)];
                main |= all;
                Console.WriteLine($"| {k.Name} | {pcts[p]}% | {(pass1[(k, p)] ? "○" : "×")} "
                    + $"| {(pass2[(k, p)] ? "○" : "×")} | {(pass3[(k, p)] ? "○" : "×")} "
                    + $"| {(all ? "**○**" : "×")} |");
            }
        Console.WriteLine();
        Console.WriteLine(main
            ? "**本丸: ○ —— 斥候 → 主力の道で 線1・線2・線3 が同時に立つ。**"
            : "**本丸: × —— 3 つを同時に満たす（形 × 回復）は無い。線は動かさない（指示書 §2-4）。**");
        Console.WriteLine();
        Console.WriteLine($"（ハネ隊の埋め草は **{filler}**）");
    }

    // =================================================================================
    // 自己検査
    // =================================================================================

    static void ScoutCheck()
    {
        var rows = CompareBuilds();
        Console.WriteLine("# 第168期 自己検査");
        Console.WriteLine();
        Console.WriteLine("| # | 項目 | 結果 |");
        Console.WriteLine("|---|---|---|");

        bool same = true;
        for (int w = 1; w <= 4; w++)
        {
            var m5 = ScoutSquad(w, 0); var src = EnemyCatalog.Stages[w].Enemy;
            for (int s = 0; s < 5; s++) if (!ReferenceEquals(m5[s], src[s])) same = false;
        }
        Console.WriteLine($"| (b) | `M5` の中身が `EnemyCatalog.Stages` と同一（`UnitDef` の参照まで） "
            + $"| {(same ? "**○**" : "×")} |");

        int diffTotal = 0;
        for (int w = 1; w <= 4; w++)
        {
            var x = ScoutRows(ScoutSquad(w, 0));
            var y = ScoutRows(EnemyCatalog.Stages[w].Enemy);
            diffTotal += Enumerable.Range(0, rows.Length).Count(i => Math.Abs(x.Win[i] - y.Win[i]) > 1e-9);
        }
        Console.WriteLine($"| (b') | `M5` の 61 行 × 4 波 ＝ {rows.Length * 4} セルの勝率が `Stages` と一致 "
            + $"| {(diffTotal == 0 ? "**○** 0 件差分" : $"× {diffTotal} 件")} |");

        Console.WriteLine("| (d) | 波・形・回復量・割り当てを混ぜた集計で判定していない "
            + "| **○**（部A は 波 × 形 ごと、部B は 形 × 回復 × 割り当て ごとに線を当てている） |");
        Console.WriteLine("| (f) | `BattleCore` / `DemoApp` / `All` / `Presets` / `Stages` / `Columns` に触っていない "
            + "| **○**（この期が足したのは `BattleSim/Modes/StageScout.cs` と振り分け 1 行だけ） |");
        Console.WriteLine();
        Console.WriteLine("(a) `compare` 305 セル ／ (c) 第167期の対照列 ／ (e) `docs/rules.md` は"
            + "別コマンドで確かめる（報告書 §自己検査）。");
    }

    // =================================================================================
    // 入口
    // =================================================================================

    static int FormIdx(string name) => Array.FindIndex(ScoutForms,
        f => f.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    static void ScoutEntry(string arg)
    {
        string s = arg.Trim();
        if (s.StartsWith("phase0")) { ScoutPhase0(); return; }
        if (s.StartsWith("check")) { ScoutCheck(); return; }
        if (s.StartsWith("b"))
        {
            string rest = s.Length > 1 ? s.Substring(1).Trim() : "";
            // 書式: `S3n/S3r,S3n,S3r`（`北/南`。スラッシュ無しは両方の道に同じ形）
            var forms = rest.Length == 0
                ? new[] { new ScoutPair(3, 2), new ScoutPair(3, 3), new ScoutPair(2, 2), new ScoutPair(1, 1) }
                : rest.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(t =>
                      {
                          var q = t.Trim().Split('/');
                          int n = FormIdx(q[0]), so = FormIdx(q.Length > 1 ? q[1] : q[0]);
                          return new ScoutPair(n, so);
                      })
                      .Where(p => p.North >= 0 && p.South >= 0).ToArray();
            ScoutPartB(forms);
            return;
        }
        ScoutPartA();
    }
}
