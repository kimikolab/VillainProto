using BattleCore;
using static Common;
using D326 = Dense326Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// stun331 の Phase 0 と本測定（第331期・軽い期）。
static partial class Stun331Diag
{
    static string LostPct(Z z) => Pct(z.FoeShockLost, z.FoeTurns);
    static string PopsPer(Z z) => $"{Per2(z.Pops, z.PopPairs)} ／ {z.PopMax}";
    static string VeilLine(Z z) => $"{Per0(z.VeilAdded, z.N)} ／ {Per0(z.VeilUsed, z.N)} ／ {Per0(z.VeilAdded - z.VeilUsed, z.N)}";

    // ---------------------------------------------------------------------------------
    // §3 Phase 0（規定・ポンの台）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = new[] { "guard", "bat", "2", "5", "boss" }.Select(WaveOf).ToArray();
        var res = waves.ToDictionary(w => w.Name, w => Measure(Apply(PonBoard(), Vers[0]), w));   // 第332期: 「規定」の列（第330〜331期の規定）に固定
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 規定 × ポンの台（{S307.Seats(PonBoard())}）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("ボスは参考（痺れない・萎縮だけ）。敵 ＝ 波の駒（ソムの喚ぶ召喚獣 ／ 糸玉を除く）。");
        Console.WriteLine();

        Console.WriteLine("## 1. 敵の手番のうち感電の痺れで失った割合・続けて失った最長・1体が1ターンに弾けた回数");
        Console.WriteLine();
        Console.WriteLine("手番 ＝ 敵に回ってきた手番（`TurnsTaken`・潰れた手番を含む）。失った ＝ 感電の痺れで潰れた手番（`StallShockStun`）。動いた ＝ 潰れなかった手番。続けて失った最長 ＝ 1体が続くターンで手番を失った回数の最長（1戦の最大の平均 ／ 全戦の最大）。"
            + "弾けた回数 ＝ 敵1体が1ターンに弾けた回数（弾けたことのある (駒, ターン) の平均 ／ 最大）。判定 ＝ 敵の弾けのうち 痺れた ／ 50% で外れた（1戦あたり）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 決着T | 敵の手番 ／戦 | **痺れで失った** | 動いた ／戦 | ほかで潰れた ／戦 | 続けて失った最長 平均 ／ 最大 | 1体が1ターンに弾けた 平均 ／ 最大 | 痺れた ／ 外れた ／戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|---|---|");
        foreach (var w in waves)
        {
            var z = res[w.Name];
            Console.WriteLine($"| {w.Name} | {Per(z.D.Base.WinT, z.Wins)} | {Per(z.FoeTurns, z.N)} | **{LostPct(z)}%** | {Per(z.FoeActed, z.N)} | {Per(z.FoeOtherStall, z.N)} | {Per2(z.LostRunMaxSum, z.N)} ／ {z.LostRunMax} | {PopsPer(z)} | {Per(z.FoeStunned, z.N)} ／ {Per(z.FoeStunMissed, z.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("ほかで潰れた手番の内訳（1戦あたり）: " + string.Join("・", waves.Select(w => { var z = res[w.Name]; return $"{w.Name} クグの組み付き ＋ シガの竦み {Per(z.FoeStallCanAct, z.N)} ／ 感電以外の痺れ（クグの縛り上げ など） {Per(z.FoeStallOtherStun, z.N)} ／ そのほか {Per(z.FoeOtherStall - z.FoeStallCanAct - z.FoeStallOtherStun, z.N)}"; })) + "。");
        Console.WriteLine();

        Console.WriteLine("## 2. ソムの破片（光の衣）");
        Console.WriteLine();
        Console.WriteLine("得た ＝ 光の溢れが味方の破片になった量（`SparkVeilAdded`）。使った ＝ 衣の破片が減った量（＝ 受け止めた・`SparkVeilSoaked`）。溢れた ＝ 得た − 使った（戦の終わりに使われずに残った・倒れた味方の分を含む）。1戦あたり。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 得た | 使った | 溢れた（残った） | 使った割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var w in waves) { var z = res[w.Name]; Console.WriteLine($"| {w.Name} | {Per0(z.VeilAdded, z.N)} | {Per0(z.VeilUsed, z.N)} | {Per0(z.VeilAdded - z.VeilUsed, z.N)} | {Pct(z.VeilUsed, z.VeilAdded)}% |"); }
        Console.WriteLine();
        Console.WriteLine("**溢れたときに何が起きるか（いまの規則）**: 規定のソムは光の衣（`SparkVeil`・上限なしの LV-a）で、満ちた味方に降った光の溢れはそのまま破片に足され、**上限も捨てる口も無い**（上限つきの LV-c `SparkVeilCap` は保持者 0 枚）。"
            + "破片は被弾で HP より先に減るだけなので、殴られない味方の破片は戦の終わりまで積もり続け、会戦の境界（`StatusKeys.All`）で消える。");
        Console.WriteLine();

        Console.WriteLine("## 3. 味方が敵から受けた攻撃 ／ ダメージ（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("攻撃 ＝ 敵が振った `Attack` のうち主目標が味方（巻き込みは数えない）。ダメージ ＝ 敵の駒が出どころの、味方が浴びた `Damage`（放電 ／ 爆発の味方の刃は含まない）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 攻撃 ／戦 | ダメージ ／戦 |");
        Console.WriteLine("|---|--:|--:|");
        foreach (var w in waves) { var z = res[w.Name]; Console.WriteLine($"| {w.Name} | {Per(z.AllyAttacked, z.N)} | {Per0(z.AllyDmg, z.N)} |"); }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = new Dictionary<(string, string, string), Z>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("規定 ＝ いまのトウ ／ カタ。版はトウ ／ カタ（痺れの札の保持者）だけを差し替える（規則は盤面全体に効く）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. 敵の手番を痺れで失った割合と、敵が実際に動いた手番 ／戦");
        Console.WriteLine();
        Console.WriteLine("セル ＝ 失った %（動いた ／戦）。ボスは痺れない（萎縮だけ）ので省く。");
        Console.WriteLine();
        var nb = Waves.Where(w => !w.Boss).ToArray();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", nb.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(nb.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", nb.Select(w => { var z = res[(bn, v.Name, w.Name)]; return $"{LostPct(z)}%（{Per(z.FoeActed, z.N)}）"; })) + " |");
        Console.WriteLine();
        Console.WriteLine("ポンの台: 弾けの判定の内訳（敵・1戦あたり）——痺れた ／ 外れた ／ ハメ防止で痺れなかった（うち萎縮 ST-b）／ 2回目以降で判定しなかった（ST-c）、続けて失った最長（平均 ／ 最大）、1体が1ターンに弾けた（平均 ／ 最大）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 痺れた ／ 外れた | ハメ防止（萎縮） | 判定しなかった | 続けて失った最長 | 1体1ターンの弾け |");
        Console.WriteLine("|---|---|---|---|--:|---|---|");
        foreach (var w in nb)
            foreach (var v in vers)
            {
                var z = res[("ポンの台", v.Name, w.Name)];
                Console.WriteLine($"| {w.Name} | {v.Name} | {Per(z.FoeStunned, z.N)} ／ {Per(z.FoeStunMissed, z.N)} | {Per(z.FoeGuarded, z.N)}（{Per(z.FoeGuardDaunted, z.N)}） | {Per(z.FoeOnceSkip, z.N)} | {Per2(z.LostRunMaxSum, z.N)} ／ {z.LostRunMax} | {PopsPer(z)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. 味方が受けた攻撃 ／ ダメージと、ソムの破片（得た ／ 使った ／ 溢れた・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w => { var z = res[(bn, v.Name, w.Name)]; return $"攻 {Per(z.AllyAttacked, z.N)} ／ {Per0(z.AllyDmg, z.N)}・破片 {VeilLine(z)}"; })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 3. 勝率（規定との差）と決着T（勝ち）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var z = res[(bn, v.Name, w.Name)]; double b0 = res[(bn, vers[0].Name, w.Name)].Win;
                    string win = v == vers[0] || z.Win == b0 ? F1(z.Win) : $"{F1(z.Win)}（{Sg(z.Win - b0)}）";
                    return $"{win}・{Per(z.D.Base.WinT, z.Wins)}T";
                })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 4. シガ: 2倍が入った鞭の割合（縛られた的の理由）・怖気");
        Console.WriteLine();
        Console.WriteLine("2倍 ＝ 2倍で入った延べ ÷ 当たった延べ。理由 ＝ 2倍の延べのうち 痺れ ／ 手番を失った・萎縮だけ・感電（当たる前）・ほか。怖気 ＝ 怖気づいた ÷ 振った。シガのいる台だけ（1戦あたり）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 当たった ／戦 | 2倍 | 痺れ ／ 萎縮 ／ 感電 ／ ほか | 怖気 |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var z = res[(bn, v.Name, w.Name)]; var s = z.Shiga;
                    if (z.ShigaN == 0) continue;
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {Per(s.WhipHits, z.N)} | {Pct(s.WhipDoubled, s.WhipHits)}% | {Per(H(s.Whip331Why, 0), z.N)} ／ {Per(H(s.Whip331Why, 1), z.N)} ／ {Per(s.WhipDoubledShock, z.N)} ／ {Per(H(s.Whip331Why, 2), z.N)} | {Pct(s.WhipCowered, s.WhipSwings)}% |");
                }
        Console.WriteLine();

        Console.WriteLine("## 5. 壊れの兆し（精鋭の勝率が規定から 10pt 以上落ちる台）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves.Where(IsElite))
                {
                    double d = res[(bn, v.Name, w.Name)].Win - res[(bn, vers[0].Name, w.Name)].Win;
                    if (d <= -10) bad.Add($"{bn} × {w.Name} {Sg(d)}");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        double[,] Grid(Ver v)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, v);
                int wins = 0;
                for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / CompareSeeds;
            });
            return g;
        }
        bool Holds(Formation f) => f.Occupied().Any(o => o.Def.Id is "tou" or "kata");
        var b = Grid(vers[0]);
        Console.WriteLine($"# {Tag} §5 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"トウ ／ カタのいる行（版の札の持ち主）: {string.Join("・", rows.Where(r => Holds(r.F)).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            var g = Grid(vers[vi]);
            S316.PrintCompare(rows, b, g, "kata");
            int outside = Enumerable.Range(0, rows.Length).Where(ri => !Holds(rows[ri].F)).Sum(ri => Enumerable.Range(0, nw).Count(w => g[ri, w] != b[ri, w]));
            Console.WriteLine($"（上の「持ち主のいない行」はカタだけで数えている。トウ ／ カタのどちらもいない行で動いたセル: **{outside}**）");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
