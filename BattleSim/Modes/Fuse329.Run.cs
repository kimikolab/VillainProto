using BattleCore;
using static Common;
using D326 = Dense326Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// fuse329 の Phase 0 と本測定（第329期・軽い期）。
static partial class Fuse329Diag
{
    static readonly int[] TurnCols = { 1, 2, 3, 4, 5 };

    /// <summary>トウの手番の頭の、粉の印のある生きている敵（糸玉を除く）の数（ターン t・その手番の平均）。</summary>
    static string MarkedAt(UnitTally t, int turn) => Per(H(t.Pb329MarkedByTurn, turn), H(t.Pb329HeadByTurn, turn));

    // ---------------------------------------------------------------------------------
    // §3 Phase 0（規定・ポンの台）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = new[] { "guard", "bat", "boss" }.Select(WaveOf).ToArray();
        var res = waves.ToDictionary(w => w.Name, w => Measure(PonBoard(), w));
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 規定 × ポンの台（{S307.Seats(PonBoard())}）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("ボスは参考（指示書の Phase 0 は近衛 ／ 大隊）。");
        Console.WriteLine();

        Console.WriteLine("## 1. トウの1手番あたり、粉が落ちた敵の数（主目標 ／ 舞う先）");
        Console.WriteLine();
        Console.WriteLine("手番 ＝ トウの手番の頭（`PowderHead`）の数。落ちた ＝ `PowderFell`（生きている駒だけ・舞う先は糸玉を除く）。新しく帯電 ＝ 粉で乾いていた駒が帯電した（`MarkShock` が付けた）。"
            + "すでに帯電 ＝ 落ちた − 新しく帯電（粉が落ちた時点で帯電していた）。そのうち手番の頭に帯電 ＝ 濃さ +1 の条件（`PowderFell` の head）。"
            + "**主目標の隣の敵 ＝ 舞う先に落ちた ÷ 主目標に落ちた**（舞う先は主目標の生きている隣すべてに1回ずつ落ちる）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 戦 | 手番 ／戦 | 主目標 ／手番（新しく ／ すでに ／ 頭に帯電） | 舞う先 ／手番（新しく ／ すでに ／ 頭に帯電） | 糸玉 ／手番 | **主目標の隣の敵** | 漏れ ／手番 |");
        Console.WriteLine("|---|--:|--:|---|---|--:|--:|--:|");
        foreach (var w in waves)
        {
            var z = res[w.Name]; var t = z.T;
            long hd = t.Pb329Heads;
            long mFell = H(t.PwFell, 0), mNew = t.PowderMain, mHead = H(t.PwFellShocked, 0);
            long sFell = H(t.PwFell, 1) - t.Dx327BallPowder, sNew = t.PowderSpread, sHead = H(t.PwFellShocked, 1);
            Console.WriteLine($"| {w.Name} | {z.N} | {Per2(hd, z.N)} | {Per2(mFell, hd)}（{Per2(mNew, hd)} ／ {Per2(mFell - mNew, hd)} ／ {Per2(mHead, hd)}） | {Per2(sFell, hd)}（{Per2(sNew, hd)} ／ {Per2(sFell - sNew, hd)} ／ {Per2(sHead, hd)}） | {Per2(t.Dx327BallPowder, hd)} | **{Per2(sFell, mFell)}** | {Per2(H(t.PwFell, 2), hd)} |");
        }
        Console.WriteLine();
        Console.WriteLine("手番の頭の、粉の印のある生きている敵（糸玉を除く・その手番の平均）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | " + string.Join(" | ", TurnCols.Select(c => $"T{c}")) + " |");
        Console.WriteLine("|---|" + string.Concat(TurnCols.Select(_ => "--:|")));
        foreach (var w in waves) Console.WriteLine($"| {w.Name} | " + string.Join(" | ", TurnCols.Select(c => MarkedAt(res[w.Name].T, c))) + " |");
        Console.WriteLine();

        Console.WriteLine("## 2. 弾けた敵の濃さの分布と、1戦の爆発の回数（いまの規定）");
        Console.WriteLine();
        Console.WriteLine("弾けた敵 ＝ 敵の側（召喚獣 ／ 糸玉を含む）の感電の弾け（1戦あたり）。濃さ ＝ 弾けた時点（`PowderPop`）。爆発 ＝ 粉塵爆発の回数（受け手 敵 ／ 召喚獣 ／ 糸玉 ／ 味方）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 決着T（勝ち） | 弾けた敵 ／戦 | 濃さ 0 | 1 | 2 | 3 | 4+ | うち粉の印あり | 爆発 ／戦（敵 ／ 召喚獣 ／ 糸玉 ／ 味方） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var w in waves)
        {
            var z = res[w.Name]; var t = z.T;
            long pops = Enumerable.Range(0, 9).Sum(i => H(t.Pb329PopDens, i));
            long p4 = Enumerable.Range(4, 5).Sum(i => H(t.Pb329PopDens, i));
            Console.WriteLine($"| {w.Name} | {Per(z.Base.WinT, z.Wins)} | {Per2(pops, z.N)} | {Pct(H(t.Pb329PopDens, 0), pops)}% | {Pct(H(t.Pb329PopDens, 1), pops)}% | {Pct(H(t.Pb329PopDens, 2), pops)}% | {Pct(H(t.Pb329PopDens, 3), pops)}% | {Pct(p4, pops)}% | {Pct(t.Pb329PopMarked, pops)}% | {Per2(DxN(t), z.N)}（{Per2(H(t.Dx327N, 1), z.N)} ／ {Per2(H(t.Dx327N, 2), z.N)} ／ {Per2(H(t.Dx327N, 3), z.N)} ／ {Per2(H(t.Dx327N, 0), z.N)}） |");
        }
        Console.WriteLine();

        Console.WriteLine("## 3. 粉の印は弾けたあとも残るか（`_pwTouched` の扱い）");
        Console.WriteLine();
        Console.WriteLine("**残る。** `BattleContext._pwTouched`（粉の印）は `PowderFell` ／ `PowderInfect`（TP-b）／ 第329期の `DustFall`（PB-b）が `Add` するだけで、`Remove` ／ `Clear` を呼ぶ口が無い（戦ごとに盤面が作り直されるので戦をまたがない）。"
            + "弾けても（`PowderPop` は `Contains` を読むだけ）倒れても消えない——倒れた駒は `LivingMembers` に入らないので放電が届かず、印は読まれない（＝ 実質「倒れるまで残る」）。PB-a の版でも挙動は変えていない。");
        Console.WriteLine($"確かめ（上の表）: 弾けた敵のうち粉の印ありの割合は 近衛 {Pct(res["近衛"].T.Pb329PopMarked, Enumerable.Range(0, 9).Sum(i => H(res["近衛"].T.Pb329PopDens, i)))}%——弾けて濃さ 0 のまま印だけ持つ敵が、いまの規定では爆ぜない。自己検査（`fuse329 check`）でも、弾けた後の印を直に確かめる。");
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
        var res = new Dictionary<(string, string, string), D326.Z>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("規定 ＝ いまのトウ（DX）。版はトウだけを差し替える。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. 爆竹の量（ポンの台 × 近衛 ／ 大隊 ／ 本編 第2波 ／ ボス）");
        Console.WriteLine();
        Console.WriteLine("爆発 ＝ 1戦の粉塵爆発の回数（受け手1つにつき1回）。連鎖 ＝ 爆発が1回以上あった連鎖の数 ／戦・**1つの連鎖で爆ぜた駒（異なる受け手）の平均 ／ 最大**。爆ぜた敵 ＝ 受け手が敵の側（敵 ／ 召喚獣 ／ 糸玉）の爆発の延べ ／戦。"
            + "印 T1〜T5 ＝ トウの手番の頭の、粉の印のある生きている敵（糸玉を除く）の数。降った ＝ PB-b で新しく印が付いた敵 ／戦。2歩目 ＝ PB-c で2歩目に舞った粉 ／戦。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 決着T | 爆発 ／戦 | 連鎖 ／戦 | 連鎖の爆ぜた駒 平均 ／ 最大 | 爆ぜた敵 ／戦 | 敵が浴びた ／戦 | " + string.Join(" | ", TurnCols.Select(c => $"印 T{c}")) + " | 降った | 2歩目 |");
        Console.WriteLine("|---|---|--:|--:|--:|---|--:|--:|" + string.Concat(TurnCols.Select(_ => "--:|")) + "--:|--:|");
        foreach (var w in new[] { "guard", "bat", "2", "boss" }.Select(WaveOf))
            foreach (var v in vers)
            {
                var z = res[("ポンの台", v.Name, w.Name)]; var t = z.T;
                Console.WriteLine($"| {w.Name} | {v.Name} | {Per(z.Base.WinT, z.Wins)} | {Per(DxN(t), z.N)} | {Per(t.Pb329Chains, z.N)} | {Per2(t.Pb329ChainRecv, t.Pb329Chains)} ／ {t.Pb329ChainMax} | {Per(DxFoeN(t), z.N)} | {Per0(DxTo(t, 1) + DxTo(t, 2), z.N)} | "
                    + string.Join(" | ", TurnCols.Select(c => MarkedAt(t, c))) + $" | {Per(t.Pb329Fall, z.N)} | {Per(t.Pb329FarFell, z.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. ボスの倒しT（勝った戦の平均・勝率）と、1ターンの与ダメが勇者の回復の上限（1,200）に初めて届いたターン");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", vers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(vers.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", vers.Select(v => { var z = res[(bn, v.Name, "ボス")]; return $"{Per(z.Base.WinT, z.Wins)}T（{F1(z.Win)}%）・{D326.Cross(z)}"; })) + " |");
        Console.WriteLine();
        Console.WriteLine("ボス: 爆発の回数の内訳（受け手 糸玉 ／ 召喚獣 ／ 勇者 ／ 味方・1戦あたり）と、爆発が勇者に入れた HP:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 糸玉 ／ 召喚獣 ／ 勇者 ／ 味方 | 勇者が浴びた爆発 | 勇者が浴びた合計 |");
        Console.WriteLine("|---|---|---|--:|--:|");
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
            {
                var z = res[(bn, v.Name, "ボス")]; var t = z.T;
                Console.WriteLine($"| {bn} | {v.Name} | {Per(H(t.Dx327N, 3), z.N)} ／ {Per(H(t.Dx327N, 2), z.N)} ／ {Per(H(t.Dx327N, 1), z.N)} ／ {Per(H(t.Dx327N, 0), z.N)} | {Per0(DxTo(t, 1), z.N)} | {Per0(z.HeroDmgBySrc.Sum(), z.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 3. 波 × 版: 勝率・決着T・1T 決着・味方が浴びた爆発");
        Console.WriteLine();
        Console.WriteLine("勝率（規定との差）。決着T ＝ 勝った戦 ／ 負けた戦。味方が浴びた爆発 ＝ 1戦の発数 ／ 減らした HP ／ 爆発で倒れた味方。1回の最大 ＝ 爆発1回（受け手1つぶんの隣の合計）で減らした HP の最大（全戦）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 決着T | 1T 決着 % | 爆発 ／戦 | 味方が浴びた 発 ／ HP ／ 倒れた | 1回の最大 |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|---|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var z = res[(bn, v.Name, w.Name)]; var t = z.T;
                    double b0 = res[(bn, vers[0].Name, w.Name)].Win;
                    string win = v == vers[0] || z.Win == b0 ? F1(z.Win) : $"{F1(z.Win)}（{Sg(z.Win - b0)}）";
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {win} | {Per(z.Base.WinT, z.Wins)} ／ {Per(z.Base.LoseT, z.N - z.Wins)} | {Pct(z.Base.OneT, z.N)} | {Per(DxN(t), z.N)} | {Per(DxHitsTo(t, 0), z.N)} ／ {Per0(DxTo(t, 0), z.N)} ／ {Per2(H(t.Dx327Kills, 0), z.N)} | {t.Dx327Max} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 4. 強すぎの兆し（ボス 4T 未満 ／ 精鋭の 1T 決着 5% 以上 ／ 1回の爆発が 1,000 超）");
        Console.WriteLine();
        foreach (var v in vers)
        {
            var strong = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)];
                    if (w.Boss && z.Wins > 0 && (double)z.Base.WinT / z.Wins < 4) strong.Add($"{bn} × ボス {Per(z.Base.WinT, z.Wins)}T");
                    if (IsElite(w) && z.Base.OneT * 20 >= z.N) strong.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.Base.OneT, z.N)}%");
                    if (z.T.Dx327Max > 1000) strong.Add($"{bn} × {w.Name} 1回の爆発 {z.T.Dx327Max}");
                }
            Console.WriteLine($"- **{v.Name}**: {(strong.Count == 0 ? "なし" : string.Join("・", strong))}");
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
        var b = Grid(vers[0]);
        Console.WriteLine($"# {Tag} §5 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"トウのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "tou")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), "tou");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
