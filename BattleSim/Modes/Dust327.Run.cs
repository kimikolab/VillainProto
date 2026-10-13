using BattleCore;
using static Common;
using D326 = Dense326Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// dust327 の Phase 0 と本測定（第327期・軽い期）。
static partial class Dust327Diag
{
    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var w = WaveOf("boss");
        Console.WriteLine($"# {Tag} §3 Phase 0 —— ポンの台 × ボス（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("## 1. 勇者の隣の糸玉（いまの規則・規定 ＝ DR-a）");
        Console.WriteLine();
        Console.WriteLine("セル ＝ そのターンの終わりに勇者が生きている戦の平均。糸玉 ＝ 盤上の糸玉の数 ／ 隣 ＝ そのうち勇者の隣の席の糸玉（勇者の陣の X 字の表）。");
        Console.WriteLine();
        var z = Measure(Apply(PonBoard(), Vers[0]), w);
        int[] ts = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12 };
        Console.WriteLine("| | " + string.Join(" | ", ts.Select(t => $"T{t}")) + " |");
        Console.WriteLine("|---|" + string.Concat(ts.Select(_ => "--:|")));
        Console.WriteLine("| 生きている戦 | " + string.Join(" | ", ts.Select(t => z.Alive[t].ToString())) + " |");
        Console.WriteLine("| 糸玉 | " + string.Join(" | ", ts.Select(t => Per2(z.Balls[t], z.Alive[t]))) + " |");
        Console.WriteLine("| 勇者の隣の糸玉 | " + string.Join(" | ", ts.Select(t => Per2(z.BallAdj[t], z.Alive[t]))) + " |");
        Console.WriteLine();
        Console.WriteLine($"勝率 {F1(z.Win)}%・倒しT {Per(z.D.Base.WinT, z.Wins)}・糸玉 張った {Per2(z.G.SilkPlaced, z.N)} ／戦（隣に空きが無く遠くへ {Per2(z.G.SilkFar, z.N)}・張れず {Per2(z.G.SilkNoRoom, z.N)}）・糸玉に届いた放電 {Per(z.G.SilkDisIn, z.N)} ／戦・糸玉が弾けた {Per(z.G.SilkPops, z.N)} ／戦。");
        Console.WriteLine();
        Console.WriteLine("## 2. トウの粉が「主目標の隣」に舞うとき、糸玉は隣に数えられるか");
        Console.WriteLine();
        Console.WriteLine("**いまは数えられない**: 粉の舞う先は `ChargedPowderTrait` の `ctx.LivingMembers(target.TeamId)` の隣で、糸玉は盤面の駒の列 `_units` の外（`_silkBalls`）なので、この列に入らない（第292期の作り）。");
        Console.WriteLine("DX では、粉が舞った後に **主目標の隣の糸玉にも**（同じ陣営・`FormationRules.AreAdjacent(主目標, 糸玉)`）落とす口（`BattleContext.PowderOnBalls`）を足した——帯電は駒と同じ `MarkShock`、粉が落ちたは `PowderFell`（トウの手番の頭に帯電していれば濃さ +1・印は敵の側として付く）。手番の頭の控え（`PowderHead`）にも、DX の保持者がいる戦だけ糸玉を入れる。");
        Console.WriteLine();
        var x = Measure(Apply(PonBoard(), Vers[1]), w);
        Console.WriteLine($"確かめ（DX・同じ seed）: 糸玉に落ちた粉 {Per2(x.T.Dx327BallPowder, x.N)} ／戦（うち濃さ +1 {Per2(x.T.Dx327BallThick, x.N)}）・糸玉の濃さの最大 {x.T.Dx327BallDensMax}・規定では {Per2(z.T.Dx327BallPowder, z.N)}。");
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
        Console.WriteLine("規定 ＝ 段0 入り（トウ ＝ DR-a）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. ボスの倒しT と、1ターンの与ダメが勇者の回復の上限（1,200）に初めて届いたターン");
        Console.WriteLine();
        Console.WriteLine("倒しT ＝ 勝った戦の平均。上回り ＝ 1ターンの与ダメ（勇者が生きている戦の平均）が 1,200 に初めて届いたターン（生きている戦が 1/4 以上のターンだけ・第326期と同じ物差し）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", vers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(vers.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", vers.Select(v => { var z = res[(bn, v.Name, "ボス")]; return $"{Per(z.D.Base.WinT, z.Wins)}T（{F1(z.Win)}%）・{D326.Cross(z.D)}"; })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 2. ボス: 勇者が浴びたダメージの内訳（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("カタ ／ シガ ＝ その駒の `Damage`。焼 ＝ 勇者自身の濃い雷の焼。放電 ＝ 勇者が浴びた放電で減らした HP（出し手を問わない・うち 自分の粉 ＝ DR-a の濃さの分の名目 8 × 勇者の濃さ）。"
            + "爆発 ＝ 粉塵爆発で減らした HP（糸玉から ／ 召喚獣から ／ ほかの駒から）——**勇者自身の粉の爆発は受け手自身には入れない規則なので、勇者の隣の駒（召喚獣）に飛ぶ**。合計 ＝ 勇者が浴びた `Damage` の全部。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 合計 | カタ | シガ | 焼 | 放電（うち 自分の粉） | 爆発 糸玉から | 爆発 召喚獣から | 爆発 ほか | 爆発の回数（糸玉 ／ 召喚獣 ／ 勇者 ／ 味方） | 勇者の粉 → 召喚獣（倒れた） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|--:|--:|--:|---|---|");
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
            {
                var z = res[(bn, v.Name, "ボス")];
                var t = z.T;
                long total = z.D.HeroDmgBySrc.Sum();
                long dis = Enumerable.Range(0, 4).Sum(s => H(t.Dr326Dealt, 4 + s)), self = 8 * Enumerable.Range(0, 4).Sum(s => H(t.Dr326RecvDens, 4 + s));
                Console.WriteLine($"| {bn} | {v.Name} | {Per0(total, z.N)} | {Per0(z.D.HeroDmgBySrc[1], z.N)} | {Per0(z.D.HeroDmgBySrc[0], z.N)} | {Per0(z.D.HeroDmgBySrc[7], z.N)} | {Per0(dis, z.N)}（{Per0(self, z.N)}） | {Per0(Dx(t, 3, 1), z.N)} | {Per0(Dx(t, 2, 1), z.N)} | {Per0(Dx(t, 0, 1) + Dx(t, 1, 1), z.N)} | {Per(H(t.Dx327N, 3), z.N)} ／ {Per(H(t.Dx327N, 2), z.N)} ／ {Per(H(t.Dx327N, 1), z.N)} ／ {Per(H(t.Dx327N, 0), z.N)} | {Per0(Dx(t, 1, 2), z.N)}（{Per2(H(t.Dx327Kills, 2), z.N)}） |");
            }
        Console.WriteLine();

        Console.WriteLine("## 3. ボス: 糸玉の濃さ（ターンの終わりの最大・生きている戦の平均）と電池 ／ 隣帯電");
        Console.WriteLine();
        Console.WriteLine("糸玉 ＝ 盤上の数 ／ 勇者の隣。電池 ＝ 電池で帯電し直した糸玉 ／ 戦（そのうち働いた連鎖）・弾けた糸玉 ＝ 1戦の糸玉の弾け。隣帯電 ＝ 召喚獣の雷で新しく帯電した敵 ／ そのうち後で弾けた。");
        Console.WriteLine();
        int[] ts = { 1, 3, 5, 7, 10 };
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", ts.Select(t => $"濃さ T{t}")) + " | 最大 | 糸玉 T5（隣） | 粉が落ちた（濃く ／ 帯電させた） | 弾けた糸玉 | 電池（連鎖） | 隣帯電（弾けた） |");
        Console.WriteLine("|---|---|" + string.Concat(ts.Select(_ => "--:|")) + "--:|---|---|--:|---|---|");
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
            {
                var z = res[(bn, v.Name, "ボス")];
                if (z.G.SilkPlaced == 0 && v != vers[0]) continue;
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", ts.Select(t => Per(z.BallMax[t], z.Alive[t]))) + $" | {z.T.Dx327BallDensMax} | {Per(z.Balls[5], z.Alive[5])}（{Per(z.BallAdj[5], z.Alive[5])}） | {Per(z.T.Dx327BallPowder, z.N)}（{Per(z.T.Dx327BallThick, z.N)} ／ {Per(z.T.Dx327BallCharged, z.N)}） | {Per(z.G.SilkPops, z.N)} | {Per(z.G.Bat327Recharged, z.N)}（{Per(z.G.Bat327Chains, z.N)}） | {Per(H(z.Som.Bc327New, 1), z.N)}（{Per(z.Som.Bc327Popped, z.N)}） |");
            }
        Console.WriteLine();

        Console.WriteLine("## 4. 波 × 版: 勝率・決着T・1T 決着・爆発（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("勝率（規定との差）。決着T ＝ 勝った戦 ／ 負けた戦。爆発 ＝ 回数 ／ 敵が浴びた（召喚獣を除く）／ **味方が浴びた** ／ 爆発で倒れた味方。1回の最大 ＝ 爆発1回（受け手1つぶんの隣の合計）で減らした HP の最大（全戦）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 決着T | 1T 決着 % | 爆発 回数 ／ 敵 ／ 味方 ／ 倒れた味方 | 1回の最大 | 味方の放電被弾 |");
        Console.WriteLine("|---|---|---|---|---|--:|---|--:|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var z = res[(bn, v.Name, w.Name)];
                    double b0 = res[(bn, vers[0].Name, w.Name)].Win;
                    string win = v == vers[0] || z.Win == b0 ? F1(z.Win) : $"{F1(z.Win)}（{Sg(z.Win - b0)}）";
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {win} | {Per(z.D.Base.WinT, z.Wins)} ／ {Per(z.D.Base.LoseT, z.N - z.Wins)} | {Pct(z.D.Base.OneT, z.N)} | {Per(DxN(z.T), z.N)} ／ {Per0(DxTo(z.T, 1), z.N)} ／ {Per0(DxTo(z.T, 0), z.N)} ／ {Per2(H(z.T.Dx327Kills, 0), z.N)} | {z.T.Dx327Max} | {Per0(z.D.Base.AllyDisTaken, z.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 5. 強すぎの兆し（ボス 4T 未満 ／ 精鋭の 1T 決着 5% 以上 ／ 1回の爆発が 1,000 超）");
        Console.WriteLine();
        foreach (var v in vers)
        {
            var strong = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)];
                    if (w.Boss && z.Wins > 0 && (double)z.D.Base.WinT / z.Wins < 4) strong.Add($"{bn} × ボス {Per(z.D.Base.WinT, z.Wins)}T");
                    if (IsElite(w) && z.D.Base.OneT * 20 >= z.N) strong.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.D.Base.OneT, z.N)}%");
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
        Console.WriteLine($"# {Tag} §5 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定 ＝ DR-a）");
        Console.WriteLine();
        Console.WriteLine($"トウのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "tou")).Select(r => r.Name))}");
        Console.WriteLine();
        Console.WriteLine($"クグ ／ ソムのいる行（電池 ／ 隣帯電で動きうる）: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id is "kugu" or "som")).Select(r => r.Name))}");
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
