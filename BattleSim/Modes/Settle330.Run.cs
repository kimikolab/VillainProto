using BattleCore;
using static Common;
using B283 = Boss283Diag;
using D326 = Dense326Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;

// settle330 の本測定（第330期・重い期・規定の1列だけ）。
static partial class Settle330Diag
{
    static readonly int[] TurnCols = { 2 };

    // ---------------------------------------------------------------------------------
    // §4 代表台 × 10 波 × 旧 ／ 新
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var cols = new[] { ("旧", true), ("新", false) };
        var res = new Dictionary<(string, string, string), D326.Z>();
        foreach (var (bn, _, bf) in boards) foreach (var (cn, old) in cols) foreach (var w in Waves) res[(bn, cn, w.Name)] = Measure(old ? Pin328(bf) : bf, w);
        Console.WriteLine($"# {Tag} §4 代表台 {boards.Length} × {Waves.Length} 波 × 旧 ／ 新・seed 0..{Seeds - 1}（重い期）");
        Console.WriteLine();
        Console.WriteLine("旧 ＝ 第328〜329期の規定（トウ DX・`TouH328`）。新 ＝ いまの規定（トウ ＋ PB-a ＋ PB-b）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, _, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. 爆竹の量（ポンの台 × 近衛 ／ 大隊 ／ 本編 第2波・第329期 §2-1 と同じ列）");
        Console.WriteLine();
        Console.WriteLine("爆発 ＝ 1戦の粉塵爆発の回数。連鎖 ＝ 爆発が1回以上あった連鎖 ／戦と、その連鎖で爆ぜた駒（異なる受け手）の平均 ／ 最大。印 T2 ＝ トウの2手番目の頭の、粉の印のある生きている敵（糸玉を除く）。降った ＝ PB-b で新しく印が付いた敵 ／戦。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 列 | 決着T | 爆発 ／戦 | 連鎖 ／戦 | 連鎖の爆ぜた駒 平均 ／ 最大 | 爆ぜた敵 ／戦 | 敵が浴びた HP ／戦 | 印 T2 | 降った |");
        Console.WriteLine("|---|---|--:|--:|--:|---|--:|--:|--:|--:|");
        foreach (var w in new[] { "guard", "bat", "2" }.Select(WaveOf))
            foreach (var (cn, _) in cols)
            {
                var z = res[("ポンの台", cn, w.Name)]; var t = z.T;
                Console.WriteLine($"| {w.Name} | {cn} | {Per(z.Base.WinT, z.Wins)} | {Per(DxN(t), z.N)} | {Per(t.Pb329Chains, z.N)} | {Per2(t.Pb329ChainRecv, t.Pb329Chains)} ／ {t.Pb329ChainMax} | {Per(DxFoeN(t), z.N)} | {Per0(DxTo(t, 1) + DxTo(t, 2), z.N)} | {Per(H(t.Pb329MarkedByTurn, 2), H(t.Pb329HeadByTurn, 2))} | {Per(t.Pb329Fall, z.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. ボスの倒しT ／ 勝率 ／ 1,200 に届いたターン");
        Console.WriteLine();
        Console.WriteLine("| 台 | 旧 | 新 |");
        Console.WriteLine("|---|---|---|");
        foreach (var (bn, _, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", cols.Select(c => { var z = res[(bn, c.Item1, "ボス")]; return $"{Per(z.Base.WinT, z.Wins)}T（{F1(z.Win)}%）・{D326.Cross(z)}"; })) + " |");
        Console.WriteLine();
        Console.WriteLine("ボス: 爆発の回数の内訳（受け手 糸玉 ／ 召喚獣 ／ 勇者 ／ 味方・1戦あたり）と、勇者が浴びた爆発 ／ 合計:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 糸玉 ／ 召喚獣 ／ 勇者 ／ 味方 | 勇者が浴びた爆発 | 勇者が浴びた合計 |");
        Console.WriteLine("|---|---|---|--:|--:|");
        foreach (var (bn, _, _) in boards)
            foreach (var (cn, _) in cols)
            {
                var z = res[(bn, cn, "ボス")]; var t = z.T;
                Console.WriteLine($"| {bn} | {cn} | {Per(H(t.Dx327N, 3), z.N)} ／ {Per(H(t.Dx327N, 2), z.N)} ／ {Per(H(t.Dx327N, 1), z.N)} ／ {Per(H(t.Dx327N, 0), z.N)} | {Per0(DxTo(t, 1), z.N)} | {Per0(z.HeroDmgBySrc.Sum(), z.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 3. 勝率（旧 → 新・括弧は差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", Waves.Select(w =>
            {
                double a = res[(bn, "旧", w.Name)].Win, b = res[(bn, "新", w.Name)].Win;
                return a == b ? F1(b) : $"{F1(a)} → {F1(b)}（{Sg(b - a)}）";
            })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 4. 決着T（勝った戦・旧 → 新）と、味方が浴びた爆発 発 ／ 爆発で倒れた味方（新・1戦あたり・あるときだけ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", Waves.Select(w =>
            {
                var a = res[(bn, "旧", w.Name)]; var b = res[(bn, "新", w.Name)];
                long hits = DxHitsTo(b.T, 0);
                return $"{Per(a.Base.WinT, a.Wins)} → {Per(b.Base.WinT, b.Wins)}" + (hits > 0 ? $"・味方 {Per2(hits, b.N)} ／ {Per2(H(b.T.Dx327Kills, 0), b.N)}" : "");
            })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 5. 強すぎの兆し（新: ボス 4T 未満 ／ 精鋭の 1T 決着 5% 以上 ／ 1回の爆発が 1,000 超）");
        Console.WriteLine();
        var strong = new List<string>();
        long maxBurst = 0, maxOne = 0;
        foreach (var (bn, _, _) in boards)
            foreach (var w in Waves)
            {
                var z = res[(bn, "新", w.Name)];
                maxBurst = Math.Max(maxBurst, z.T.Dx327Max);
                if (IsElite(w)) maxOne = Math.Max(maxOne, z.Base.OneT);
                if (w.Boss && z.Wins > 0 && (double)z.Base.WinT / z.Wins < 4) strong.Add($"{bn} × ボス {Per(z.Base.WinT, z.Wins)}T");
                if (IsElite(w) && z.Base.OneT * 20 >= z.N) strong.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.Base.OneT, z.N)}%");
                if (z.T.Dx327Max > 1000) strong.Add($"{bn} × {w.Name} 1回の爆発 {z.T.Dx327Max}");
            }
        Console.WriteLine($"- {(strong.Count == 0 ? "なし" : string.Join("・", strong))}（1回の爆発の最大 {maxBurst}・精鋭の 1T 決着の最多 {maxOne} 戦 ／ {Seeds}）");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §4 格子1本: 近衛・固定枠 トウ ＋ カタ（規定）・探索枠3（第328期の格子と同じ候補と間引き）。精鋭が壊れていないか。
    // ---------------------------------------------------------------------------------
    static void Grid()
    {
        var w = WaveOf("guard");
        var fixedU = new[] { UnitCatalog.Tou, UnitCatalog.Kata };
        int free = 5 - fixedU.Length;
        UnitDef Now(UnitDef d) => UnitCatalog.All.FirstOrDefault(x => x.Id == d.Id) ?? d;
        var heal = S287.Heal.Select(d => d.Id).ToHashSet();
        var fixedIds = fixedU.Select(d => d.Id).ToHashSet();
        var pool = S287.Pool.Select(Now).Append(UnitCatalog.Shiga).Where(d => !fixedIds.Contains(d.Id)).GroupBy(d => d.Id).Select(g => g.First()).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var combos = new List<UnitDef[]>();
        void Rec(int start, List<UnitDef> cur)
        {
            if (cur.Count == free) { if (cur.Count(d => heal.Contains(d.Id)) <= 1) combos.Add(cur.ToArray()); return; }
            for (int i = start; i < pool.Length; i++) { cur.Add(pool[i]); Rec(i + 1, cur); cur.RemoveAt(cur.Count - 1); }
        }
        Rec(0, new List<UnitDef>());
        const int step = 4;   // 第323〜328期と同じ間引き（120 席のうち 30 席）
        var boards = new List<UnitDef[]>();
        foreach (var c in combos) boards.AddRange(B283.Perms(fixedU.Concat(c).ToArray()).Where((_, i) => i % step == 0));
        const int cut = 20;
        var res = new (int W, long T, int One, int Wo, long To, int Oneo)?[boards.Count];
        (int W, long T, int One) Wins(Formation f, int n)
        {
            int c = 0, one = 0; long t = 0;
            for (int sd = 0; sd < n; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                if (r.PlayerWon) { c++; t += r.Turns; if (r.Turns <= 1) one++; }
            }
            return (c, t, one);
        }
        Parallel.For(0, boards.Count, i =>
        {
            var f = B283.Seat(boards[i]);
            var fo = Pin328(f);
            if (Wins(f, cut).W < 5 && Wins(fo, cut).W < 5) return;
            var (wn, tn, on) = Wins(f, Seeds); var (wo, to, oo) = Wins(fo, Seeds);
            res[i] = (wn, tn, on, wo, to, oo);
        });
        var all = Enumerable.Range(0, boards.Count).Where(i => res[i] is not null).Select(i => (B: boards[i], R: res[i]!.Value)).ToList();
        bool Half(int x) => x * 2 >= Seeds;
        double T(long t, int wn) => wn == 0 ? 99 : (double)t / wn;
        Console.WriteLine($"# {Tag} §4 格子 —— 近衛 × 規定（固定枠 {string.Join(" ＋ ", fixedU.Select(Short))}・探索枠{free}（候補 {pool.Length} 枚: {string.Join("・", pool.Select(Short))}・ヒーラーは 1 枚まで）{combos.Count} 組・席 {120 / step} ＝ {boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{cut - 1}（新か旧のどちらかで 5 勝以上・{all.Count:N0} 台）→ seed 0..{Seeds - 1}。旧 ＝ 同じ台を第328〜329期の規定（`Pin328`）で。所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
        Console.WriteLine();
        var won = all.Where(x => Half(x.R.W)).ToList();
        var wonOld = all.Where(x => Half(x.R.Wo)).ToList();
        Console.WriteLine("| 段 | 旧 | 新 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 勝ちの延べ | {all.Sum(x => (long)x.R.Wo):N0} | {all.Sum(x => (long)x.R.W):N0} |");
        Console.WriteLine($"| 勝率 ≧ 50% の台 | {wonOld.Count:N0} | {won.Count:N0} |");
        Console.WriteLine($"| 勝率 100% の台 | {all.Count(x => x.R.Wo == Seeds):N0} | {all.Count(x => x.R.W == Seeds):N0} |");
        Console.WriteLine($"| 決着T（勝ちの平均） | {Per(all.Sum(x => x.R.To), all.Sum(x => (long)x.R.Wo))} | {Per(all.Sum(x => x.R.T), all.Sum(x => (long)x.R.W))} |");
        Console.WriteLine($"| **1T 決着 5% 以上の台** | {all.Count(x => x.R.Oneo * 20 >= Seeds):N0} | **{all.Count(x => x.R.One * 20 >= Seeds):N0}** |");
        Console.WriteLine($"| 1T 決着が1戦でもあった台 | {all.Count(x => x.R.Oneo > 0):N0} | {all.Count(x => x.R.One > 0):N0} |");
        Console.WriteLine($"| 1T 決着の延べ | {all.Sum(x => (long)x.R.Oneo):N0} | {all.Sum(x => (long)x.R.One):N0} |");
        Console.WriteLine();
        double[] edges = { 2, 2.5, 3, 3.5, 4, 4.5, 5, 6, 8, 31 };
        string Dist(Func<(UnitDef[] B, (int W, long T, int One, int Wo, long To, int Oneo) R), double> f, List<(UnitDef[] B, (int W, long T, int One, int Wo, long To, int Oneo) R)> src)
            => string.Join(" ／ ", edges.Select((e, k) => { double lo = k == 0 ? 0 : edges[k - 1]; return $"{lo}〜{e}T {src.Count(x => f(x) >= lo && f(x) < e)}"; }));
        Console.WriteLine("決着T の分布（勝率 ≧ 50% の台）:");
        Console.WriteLine();
        Console.WriteLine($"- 旧: {Dist(x => T(x.R.To, x.R.Wo), wonOld)}");
        Console.WriteLine($"- 新: {Dist(x => T(x.R.T, x.R.W), won)}");
        Console.WriteLine();
        Console.WriteLine("### 決着T の上位10台（新・勝率 ≧ 50%・速い順）");
        Console.WriteLine();
        Console.WriteLine("| 順 | 台（前1・前3・中央・後1・後3） | 新 勝率 ／ 決着T ／ 1T 決着 | 旧 勝率 ／ 決着T ／ 1T 決着 |");
        Console.WriteLine("|--:|---|---|---|");
        int rank = 0;
        foreach (var x in won.OrderBy(x => T(x.R.T, x.R.W)).ThenByDescending(x => x.R.W).Take(10))
            Console.WriteLine($"| {++rank} | {string.Join("・", x.B.Select(Short))} | {Pct(x.R.W, Seeds)}% ／ {T(x.R.T, x.R.W):F2} ／ {x.R.One} | {Pct(x.R.Wo, Seeds)}% ／ {(x.R.Wo == 0 ? "—" : T(x.R.To, x.R.Wo).ToString("F2"))} ／ {x.R.Oneo} |");
    }
}
