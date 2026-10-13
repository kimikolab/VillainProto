using BattleCore;
using static Common;
using B283 = Boss283Diag;
using D326 = Dense326Diag;
using D327 = Dust327Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// settle328 の本測定（第328期・重い期・規定の1列だけ）。
static partial class Settle328Diag
{
    // ---------------------------------------------------------------------------------
    // §5 代表台 × 10 波 × 旧 ／ 新
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var cols = new[] { ("旧", true), ("新", false) };
        var res = new Dictionary<(string, string, string), D327.Z>();
        foreach (var (bn, _, bf) in boards) foreach (var (cn, old) in cols) foreach (var w in Waves) res[(bn, cn, w.Name)] = D327.Measure(old ? Pin327(bf) : bf, w, Seeds);
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × {Waves.Length} 波 × 旧 ／ 新・seed 0..{Seeds - 1}（重い期）");
        Console.WriteLine();
        Console.WriteLine("旧 ＝ 第327期の規定（トウ DR-a ／ クグ 返す）。新 ＝ いまの規定（トウ ＋ 粉塵爆発 ／ クグ ＋ 電池）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, _, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. ボスの倒しT ／ 勝率 ／ 1,200 に届いたターン");
        Console.WriteLine();
        Console.WriteLine("| 台 | 旧 | 新 |");
        Console.WriteLine("|---|---|---|");
        foreach (var (bn, _, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", cols.Select(c => { var z = res[(bn, c.Item1, "ボス")]; return $"{Per(z.D.Base.WinT, z.Wins)}T（{F1(z.Win)}%）・{D326.Cross(z.D)}"; })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 2. ボス: 勇者が浴びたダメージの内訳（1戦あたり・第327期 §2-1 と同じ列）");
        Console.WriteLine();
        Console.WriteLine("放電（うち 自分の粉 ＝ DR-a の濃さの分の名目 8 × 勇者の濃さ）。爆発 糸玉から ／ 召喚獣から。勇者の粉 → 召喚獣（倒れた）。電池 ＝ 帯電し直した糸玉 ／ 戦。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 倒しT | 合計 | カタ | シガ | 焼 | 放電（うち 自分の粉） | 爆発 糸玉から | 爆発 召喚獣から | 勇者の粉 → 召喚獣（倒れた） | 糸玉の濃さ 最大 | 電池 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|--:|--:|---|--:|--:|");
        foreach (var (bn, _, _) in boards)
            foreach (var (cn, _) in cols)
            {
                var z = res[(bn, cn, "ボス")]; var t = z.T;
                long dis = Enumerable.Range(0, 4).Sum(s => H(t.Dr326Dealt, 4 + s)), self = 8 * Enumerable.Range(0, 4).Sum(s => H(t.Dr326RecvDens, 4 + s));
                Console.WriteLine($"| {bn} | {cn} | {Per(z.D.Base.WinT, z.Wins)} | {Per0(z.D.HeroDmgBySrc.Sum(), z.N)} | {Per0(z.D.HeroDmgBySrc[1], z.N)} | {Per0(z.D.HeroDmgBySrc[0], z.N)} | {Per0(z.D.HeroDmgBySrc[7], z.N)} | {Per0(dis, z.N)}（{Per0(self, z.N)}） | {Per0(Dx(t, 3, 1), z.N)} | {Per0(Dx(t, 2, 1), z.N)} | {Per0(Dx(t, 1, 2), z.N)}（{Per2(H(t.Dx327Kills, 2), z.N)}） | {t.Dx327BallDensMax} | {Per(z.G.Bat327Recharged, z.N)} |");
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

        Console.WriteLine("## 4. 決着T（勝った戦・旧 → 新）と、味方が浴びた爆発 ／ 爆発で倒れた味方（新・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _, _) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", Waves.Select(w =>
            {
                var a = res[(bn, "旧", w.Name)]; var b = res[(bn, "新", w.Name)];
                long ally = Enumerable.Range(0, 4).Sum(rk => Dx(b.T, rk, 0));
                return $"{Per(a.D.Base.WinT, a.Wins)} → {Per(b.D.Base.WinT, b.Wins)}" + (ally > 0 ? $"・味方 {Per0(ally, b.N)} ／ {Per2(H(b.T.Dx327Kills, 0), b.N)}" : "");
            })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 5. 強すぎの兆し（新: ボス 4T 未満 ／ 精鋭の 1T 決着 5% 以上 ／ 1回の爆発が 1,000 超）");
        Console.WriteLine();
        var strong = new List<string>();
        long maxBurst = 0;
        foreach (var (bn, _, _) in boards)
            foreach (var w in Waves)
            {
                var z = res[(bn, "新", w.Name)];
                maxBurst = Math.Max(maxBurst, z.T.Dx327Max);
                if (w.Boss && z.Wins > 0 && (double)z.D.Base.WinT / z.Wins < 4) strong.Add($"{bn} × ボス {Per(z.D.Base.WinT, z.Wins)}T");
                if (IsElite(w) && z.D.Base.OneT * 20 >= z.N) strong.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.D.Base.OneT, z.N)}%");
                if (z.T.Dx327Max > 1000) strong.Add($"{bn} × {w.Name} 1回の爆発 {z.T.Dx327Max}");
            }
        Console.WriteLine($"- {(strong.Count == 0 ? "なし" : string.Join("・", strong))}（1回の爆発の最大 {maxBurst}）");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5 格子1本: ボス・固定枠 トウ ＋ クグ（規定）・探索枠3（第324〜326期と同じ候補と間引き）
    // ---------------------------------------------------------------------------------
    static void Grid()
    {
        var w = WaveOf("boss");
        var fixedU = new[] { UnitCatalog.Tou, UnitCatalog.Kugu };
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
        const int step = 4;   // 第323〜326期と同じ間引き（120 席のうち 30 席）
        var boards = new List<UnitDef[]>();
        foreach (var c in combos) boards.AddRange(B283.Perms(fixedU.Concat(c).ToArray()).Where((_, i) => i % step == 0));
        const int cut = 20;
        var res = new (int W, long T, int Wo, long To)?[boards.Count];
        (int W, long T) Wins(Formation f, int n)
        {
            int c = 0; long t = 0;
            for (int sd = 0; sd < n; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                if (r.PlayerWon) { c++; t += r.Turns; }
            }
            return (c, t);
        }
        Parallel.For(0, boards.Count, i =>
        {
            var f = B283.Seat(boards[i]);
            var fo = Pin327(f);
            if (Wins(f, cut).W < 5 && Wins(fo, cut).W < 5) return;
            var (wn, tn) = Wins(f, Seeds); var (wo, to) = Wins(fo, Seeds);
            res[i] = (wn, tn, wo, to);
        });
        var all = Enumerable.Range(0, boards.Count).Where(i => res[i] is not null).Select(i => (B: boards[i], R: res[i]!.Value)).ToList();
        bool Half(int x) => x * 2 >= Seeds;
        double T(long t, int wn) => wn == 0 ? 99 : (double)t / wn;
        Console.WriteLine($"# {Tag} §5 格子 —— ボス × 規定（固定枠 {string.Join(" ＋ ", fixedU.Select(Short))}・探索枠{free}（候補 {pool.Length} 枚: {string.Join("・", pool.Select(Short))}・ヒーラーは 1 枚まで）{combos.Count} 組・席 {120 / step} ＝ {boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{cut - 1}（新か旧のどちらかで 5 勝以上・{all.Count:N0} 台）→ seed 0..{Seeds - 1}。旧 ＝ 同じ台を第327期の規定（`Pin327`）で。所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
        Console.WriteLine();
        var won = all.Where(x => Half(x.R.W)).ToList();
        var wonOld = all.Where(x => Half(x.R.Wo)).ToList();
        Console.WriteLine("| 段 | 旧 | 新 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 勝ちの延べ | {all.Sum(x => (long)x.R.Wo):N0} | {all.Sum(x => (long)x.R.W):N0} |");
        Console.WriteLine($"| 勝率 ≧ 50% の台 | {wonOld.Count:N0} | {won.Count:N0} |");
        Console.WriteLine($"| 倒しT（勝ちの平均） | {Per(all.Sum(x => x.R.To), all.Sum(x => (long)x.R.Wo))} | {Per(all.Sum(x => x.R.T), all.Sum(x => (long)x.R.W))} |");
        Console.WriteLine($"| **倒しT 4T 未満（勝率 ≧ 50% の台のうち）** | {wonOld.Count(x => T(x.R.To, x.R.Wo) < 4):N0} | **{won.Count(x => T(x.R.T, x.R.W) < 4):N0}** |");
        Console.WriteLine($"| 倒しT 5T 未満 | {wonOld.Count(x => T(x.R.To, x.R.Wo) < 5):N0} | {won.Count(x => T(x.R.T, x.R.W) < 5):N0} |");
        Console.WriteLine();
        int[] edges = { 4, 5, 6, 7, 8, 10, 12, 15, 20, 31 };
        Console.WriteLine("倒しT の分布（勝率 ≧ 50% の台・新）: " + string.Join(" ／ ", edges.Select((e, k) => { int lo = k == 0 ? 0 : edges[k - 1]; return $"{lo}〜{e}T {won.Count(x => T(x.R.T, x.R.W) >= lo && T(x.R.T, x.R.W) < e)}"; })));
        Console.WriteLine();
        Console.WriteLine("### 倒しT の上位10台（新・勝率 ≧ 50%・速い順）");
        Console.WriteLine();
        Console.WriteLine("| 順 | 台（前1・前3・中央・後1・後3） | 新 勝率 ／ 倒しT | 旧 勝率 ／ 倒しT |");
        Console.WriteLine("|--:|---|---|---|");
        int rank = 0;
        foreach (var x in won.OrderBy(x => T(x.R.T, x.R.W)).ThenByDescending(x => x.R.W).Take(10))
            Console.WriteLine($"| {++rank} | {string.Join("・", x.B.Select(Short))} | {Pct(x.R.W, Seeds)}% ／ {T(x.R.T, x.R.W):F1} | {Pct(x.R.Wo, Seeds)}% ／ {(x.R.Wo == 0 ? "—" : T(x.R.To, x.R.Wo).ToString("F1"))} |");
        var fast = won.Where(x => T(x.R.T, x.R.W) < 4).OrderBy(x => T(x.R.T, x.R.W)).Take(5).ToList();
        if (fast.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("### 4T 未満の上位5台: 勇者が浴びたダメージの内訳（seed 0..199・1戦あたり）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 倒しT | カタ | シガ | 焼 | 放電 | 爆発 糸玉から | 爆発 ほか | 電池 | いちばん多い出どころ |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
            foreach (var x in fast)
            {
                var z = D327.Measure(B283.Seat(x.B), w, Seeds); var t = z.T;
                long dis = Enumerable.Range(0, 4).Sum(s => H(t.Dr326Dealt, 4 + s));
                var src = D326.DmgSrc.Select((n, k) => (n, v: z.D.HeroDmgBySrc[k])).OrderByDescending(p => p.v).First();
                Console.WriteLine($"| {string.Join("・", x.B.Select(Short))} | {Per(z.D.Base.WinT, z.Wins)} | {Per0(z.D.HeroDmgBySrc[1], z.N)} | {Per0(z.D.HeroDmgBySrc[0], z.N)} | {Per0(z.D.HeroDmgBySrc[7], z.N)} | {Per0(dis, z.N)} | {Per0(Dx(t, 3, 1), z.N)} | {Per0(Dx(t, 0, 1) + Dx(t, 1, 1) + Dx(t, 2, 1), z.N)} | {Per(z.G.Bat327Recharged, z.N)} | {src.n} {Per0(src.v, z.N)} |");
            }
        }
    }
}
