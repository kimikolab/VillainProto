using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;
using H304 = Hush304Diag;

// =====================================================================================
// som307 の測定と自己検査（指示書 §4 ／ §5）。
//
//     dotnet run --project BattleSim -c Release 0 som307 compare     # `compare` 64 行 × SH-a ／ SH-b（ソムの在席行だけ差し替える・規定は動かさない）
//     dotnet run --project BattleSim -c Release 0 som307 boards      # 代表台 5 台 × ボス ／ 近衛 ／ 大隊 ／ 本編の5波 × 規定 ／ SH-a ／ SH-b（＋ 量の感度・元の勝ち台・対照 ソム → ドルガ）
//     dotnet run --project BattleSim -c Release 0 som307 gridboss <sha|shb>          # ボスの格子（固定枠 クグ ＋ カタ ＋ ソム・探索枠2・ヒーラーを除く・規定 ／ 版を同じ台で）
//     dotnet run --project BattleSim -c Release 0 som307 grid <guard|bat> <sha|shb>  # 精鋭の格子（固定枠 トウ ＋ ソム・探索枠3・ヒーラーを除く）
//     dotnet run --project BattleSim -c Release 0 som307 log <台の名前の一部> <波> <seed> <sh0|sha|shb> [件数]   # 1戦の光の並び
//     dotnet run --project BattleSim -c Release 0 som307 check       # 自己検査
// =====================================================================================
static partial class Som307Diag
{
    static partial void RunMoreCore(string mode, string[] args, Func<int, string, string> a, ref bool done)
    {
        done = true;
        switch (mode)
        {
            case "compare": CompareAll(); return;
            case "boards": BoardsAll(); return;
            case "gridboss": GridBoss(a(3, "sha")); return;
            case "grid": GridElite(a(3, "guard"), a(4, "sha")); return;
            case "log": LogOne(a(3, "ツギ"), a(4, "boss"), int.Parse(a(5, "0")), a(6, "sha"), int.Parse(a(7, "60"))); return;
            case "check": Check(); return;
            default: done = false; return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef To);
    internal static readonly Ver[] Vers =
    {
        new("規定", "sh0", UnitCatalog.SomH307), new("SH-a", "sha", UnitCatalog.SomSHa), new("SH-b", "shb", UnitCatalog.SomSHb),
    };
    /// <summary>量の感度の対照（指示書 §2-2・ボスの ツギ → ソム だけ）。</summary>
    internal static readonly Ver[] Sens =
    {
        new("SH-a × 0.5", "sha05", UnitCatalog.SomSHa05), new("SH-a × 2", "sha2", UnitCatalog.SomSHa2),
        new("SH-b × 0.5", "shb05", UnitCatalog.SomSHb05), new("SH-b × 2", "shb2", UnitCatalog.SomSHb2),
    };
    static Ver AnyVer(string n) => Vers.Concat(Sens).First(v => v.Ascii == n || v.Name == n);
    internal static Formation Apply(Formation f, Ver v) => ReferenceEquals(v.To, UnitCatalog.SomH307) ? f : FvSwap(f, UnitCatalog.SomH307, v.To);
    static bool HasSom(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.SomH307));

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Deep
    {
        public long N, Wins, WinT, LoseT, Caps, FirstDeathN, FirstDeathT, SomN, SomDied, SomDeathT;
        public long LFoe, LSummon, LBall, Chains, DeadPops, AllyPops, Hushed, HushedL, Blocked, BlockedL, Rains, RainL, Healed, Overflow, Inverted, Refused, Releases, Swings, PeakSum;
        public long[] BornT = new long[TMax + 1], ReachT = new long[TMax + 1], HealT = new long[TMax + 1];
        public long HeroN, HeroDmg, TsugiOut, HushN, ShatterN, ShatterT, Cracks, SomCracks;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long Born => LFoe + LSummon + LBall;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; SomN += o.SomN; SomDied += o.SomDied; SomDeathT += o.SomDeathT;
            LFoe += o.LFoe; LSummon += o.LSummon; LBall += o.LBall; Chains += o.Chains; DeadPops += o.DeadPops; AllyPops += o.AllyPops; Hushed += o.Hushed; HushedL += o.HushedL; Blocked += o.Blocked; BlockedL += o.BlockedL;
            Rains += o.Rains; RainL += o.RainL; Healed += o.Healed; Overflow += o.Overflow; Inverted += o.Inverted; Refused += o.Refused; Releases += o.Releases; Swings += o.Swings; PeakSum += o.PeakSum;
            for (int i = 0; i <= TMax; i++) { BornT[i] += o.BornT[i]; ReachT[i] += o.ReachT[i]; HealT[i] += o.HealT[i]; }
            HeroN += o.HeroN; HeroDmg += o.HeroDmg; TsugiOut += o.TsugiOut; HushN += o.HushN; ShatterN += o.ShatterN; ShatterT += o.ShatterT; Cracks += o.Cracks; SomCracks += o.SomCracks;
        }
    }

    internal static Deep FightDeep(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1 };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; } else { d.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) d.Caps = 1; }
        for (int t = 1; t <= Math.Min(TMax, r.Turns); t++) d.ReachT[t]++;
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), tsugi = p.FirstOrDefault(u => u.Def.Id == "tsugi");
        UnitState? hero = e.FirstOrDefault(u => u.Def == EnemyCatalog.BossRegular);
        UnitState? husher = e.FirstOrDefault(u => u.HasTrait(TraitId.Hush));
        if (som is not null && r.TallyByUnit.TryGetValue("som", out var mt))
        {
            d.SomN = 1;
            d.LFoe = mt.SparkLightFoe; d.LSummon = mt.SparkLightSummon; d.LBall = mt.SparkLightBall; d.Chains = mt.SparkChains; d.DeadPops = mt.SparkDeadPops; d.AllyPops = mt.SparkAllyPops;
            d.Hushed = mt.SparkHushed; d.HushedL = mt.SparkHushedLights; d.Blocked = mt.SparkBlocked; d.BlockedL = mt.SparkBlockedLights;
            d.Rains = mt.SparkRains; d.RainL = mt.SparkRainLights; d.Healed = mt.SparkHealed; d.Overflow = mt.SparkOverflow; d.Inverted = mt.SparkInverted; d.Refused = mt.SparkRefused;
            d.Releases = mt.SparkReleases; d.Swings = mt.SparkSwings; d.PeakSum = mt.SparkStorePeak;
        }
        if (husher is not null && r.TallyByUnit.TryGetValue(husher.Def.Id, out var ht)) { d.HushN = 1; if (ht.HushShatterTurn > 0) { d.ShatterN = 1; d.ShatterT = ht.HushShatterTurn; } }
        if (hero is not null) d.HeroN = 1;
        bool somAlive = som is not null;
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Death when ev.TargetId is int t && mine.Contains(t):
                    if (d.FirstDeathN == 0) { d.FirstDeathN = 1; d.FirstDeathT = ev.Turn; }
                    if (som is not null && t == som.InstanceId && d.SomDied == 0) { d.SomDied = 1; d.SomDeathT = ev.Turn; somAlive = false; }
                    break;
                case BattleEventKind.ShockSpent when somAlive && ev.Team != BattleContext.PlayerTeam && ev.Turn <= TMax: d.BornT[ev.Turn]++; break;
                case BattleEventKind.Heal when som is not null && ev.ActorId == som.InstanceId && ev.Turn <= TMax: d.HealT[ev.Turn] += ev.Amount; break;
                case BattleEventKind.Damage when hero is not null && ev.ActorId == hero.InstanceId && ev.TargetId is int ht2 && mine.Contains(ht2): d.HeroDmg += ev.Amount; break;
                case BattleEventKind.Plank when tsugi is not null && ev.ActorId == tsugi.InstanceId && (ev.Text == PlankLabels.Paste || ev.Text == PlankLabels.FirstAid): d.TsugiOut += ev.Amount; break;
                case BattleEventKind.Heal when tsugi is not null && ev.ActorId == tsugi.InstanceId: d.TsugiOut += ev.Amount; break;
                case BattleEventKind.HushState when ev.Text == HushStateLabels.Crack:
                    d.Cracks++;
                    if (som is not null && ev.TargetId == som.InstanceId) d.SomCracks++;
                    break;
            }
        }
        return d;
    }

    internal static Deep MeasureDeep(Formation f, S287.Wave w, int n)
    {
        var parts = new Deep[n];
        Parallel.For(0, n, i => parts[i] = FightDeep(f, w, i));
        var all = new Deep();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static bool FightLite(Formation f, S287.Wave w, int seed, out int turns)
    {
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: false);
        turns = r.Turns;
        return r.PlayerWon;
    }
    static int Wins(Formation f, S287.Wave w, int n, out long winT)
    {
        int wins = 0; winT = 0;
        for (int s = 0; s < n; s++) if (FightLite(f, w, s, out int t)) { wins++; winT += t; }
        return wins;
    }
    static int WinsPar(Formation f, S287.Wave w, int n)
    {
        int wins = 0;
        Parallel.For(0, n, s => { if (FightLite(f, w, s, out _)) Interlocked.Increment(ref wins); });
        return wins;
    }
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";

    // ---------------------------------------------------------------------------------
    // `compare`（§4-2）
    // ---------------------------------------------------------------------------------
    static double[,] CompareGrid(Ver v, int[] caps)
    {
        var rows = CompareBuildsH311().Select(r => (r.Name, F: Pin308(r.F))).ToArray();   // 第308期: ソムは旧の規定（`SomH307`）に
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            var f = Apply(rows[ri].F, v);
            int wins = 0, cap = 0;
            for (int s = 0; s < Seeds; s++) { var r = BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false); if (r.PlayerWon) wins++; else if (wi > 0 && r.Turns >= BattleEngine.MaxTurns) cap++; }
            g[ri, wi] = 100.0 * wins / Seeds;
            if (cap > 0) Interlocked.Add(ref caps[ri], cap);
        });
        return g;
    }

    static void CompareAll()
    {
        var rows = CompareBuildsH311().Select(r => (r.Name, F: Pin308(r.F))).ToArray();   // 第308期: ソムは旧の規定（`SomH307`）に
        int nw = EnemyCatalog.Stages.Count;
        var prim = Baseline.PrimaryRows.ToHashSet();
        Console.WriteLine("# 第307期 `compare` 64 行 × ソムの版（seed 0..199・ソムの在席行だけ差し替える・基準は第306期の規定）");
        Console.WriteLine();
        var caps = Vers.Select(_ => new int[rows.Length]).ToArray();
        var grids = Vers.Select((v, i) => CompareGrid(v, caps[i])).ToArray();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち | 30T 上限の負け（第2〜5波・戦） | 主判定 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|--:|---|");
        int movedOther = 0, somRows = 0;
        for (int ri = 0; ri < rows.Length; ri++)
        {
            var b = grids[0];
            if (!HasSom(rows[ri].F))
            {
                for (int vi = 1; vi < Vers.Length; vi++) for (int w = 0; w < nw; w++) if (grids[vi][ri, w] != b[ri, w]) movedOther++;
                continue;
            }
            somRows++;
            double m0 = Enumerable.Range(1, nw - 1).Average(w => b[ri, w]);
            for (int vi = 0; vi < Vers.Length; vi++)
            {
                var g = grids[vi];
                double m = Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
                double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - b[ri, w]);
                Console.WriteLine($"| {rows[ri].Name} | {Vers[vi].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w]))) + $" | {F1(m)} | {(vi == 0 ? "" : (m - m0).ToString("+0.0;-0.0;0.0"))} | {(vi == 0 ? "" : drop.ToString("+0.0;-0.0;0.0"))} | {caps[vi][ri]} | {(prim.Contains(rows[ri].Name) ? "○" : "")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"ソムのいる行: {somRows} 行。ソムのいない行で動いたセル: **{movedOther}**（0 であること）");
        // 規定の列が docs/balance.md（第306期）と同じか
        var doc = File.Exists("docs/balance.md") ? File.ReadAllLines("docs/balance.md") : Array.Empty<string>();
        int mism = 0, seen = 0;
        for (int ri = 0; ri < rows.Length; ri++)
        {
            var line = doc.FirstOrDefault(l => l.StartsWith($"| {rows[ri].Name} |", StringComparison.Ordinal));
            if (line is null) continue;
            seen++;
            var cells = line.Split('|').Select(c => c.Trim()).Where(c => c.Length > 0).Skip(1).Take(nw).ToArray();
            for (int w = 0; w < Math.Min(nw, cells.Length); w++)
                if (double.TryParse(cells[w].TrimEnd('%'), out double x) && Math.Abs(x - grids[0][ri, w]) > 0.05) mism++;
        }
        Console.WriteLine($"規定の列と `docs/balance.md` のずれ: **{mism}** セル（{seen} 行を照合）");
    }

    // ---------------------------------------------------------------------------------
    // 代表台 × 波 × 版（§4-1 ／ §4-4）
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new List<(string Board, S287.Wave W, Ver V, Formation F)>();
        foreach (var w in Waves)
        {
            foreach (var (name, f) in Boards())
                foreach (var v in Vers) cells.Add((name, w, v, Apply(f, v)));
            cells.Add(("元の勝ち台（ツギ）", w, Vers[0], PinKT311(B283.Seat(Order(WinBoard)))));
        }
        var tsBoard = Boards().First(b => b.Name == "勝ち台 ツギ→ソム").F;
        foreach (var v in Sens) cells.Add(("勝ち台 ツギ→ソム", Waves[0], v, Apply(tsBoard, v)));
        var res = new Deep[cells.Count];
        for (int i = 0; i < cells.Count; i++) res[i] = MeasureDeep(cells[i].F, cells[i].W, Seeds);

        Console.WriteLine($"# 第307期 代表台 × 波 × 版（規定 ／ SH-a ／ SH-b・量 {SparkTrait.Amount}・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (name, f) in Boards()) Console.WriteLine($"- {name}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine($"- 元の勝ち台（ツギ）: {Seats(PinKT311(B283.Seat(Order(WinBoard))))}（比べる相手・ソムはいない）");
        Console.WriteLine();
        Console.WriteLine("差 ＝ 同じ台・同じ波の規定との勝率の差。寿命 ＝ 味方が初めて倒れたT（倒れた戦だけの平均・括弧は倒れた戦の割合）。光は1戦あたり。");

        Console.WriteLine();
        Console.WriteLine("## 表A 勝率（台 × 波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var name in Boards().Select(b => b.Name).Append("元の勝ち台（ツギ）"))
            foreach (var v in name.StartsWith("元の") ? new[] { Vers[0] } : Vers)
                Console.WriteLine($"| {name} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    int i = cells.FindIndex(c => c.Board == name && c.W == w && c.V == v);
                    int bi = cells.FindIndex(c => c.Board == name && c.W == w && c.V == Vers[0]);
                    return F1(res[i].Win) + (v == Vers[0] ? "" : $"（{(res[i].Win - res[bi].Win):+0.0;-0.0;0.0}）");
                })) + " |");

        foreach (var w in Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("### 表B 勝率・寿命・光（1戦）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 差 | 倒しT | 負けT | 寿命 | ソム 倒れたT | 光 生まれた（敵 ／ 喚 ／ 糸玉） | 止まった光 粛 ／ 痺れ | 降った 回 ／ 光 | 癒した ／ 溢れた | 反転で傷 ／ 通らない | SH-b 放った ／ 殴った ／ 最大の溜まり |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].W != w) continue;
                var a = res[i];
                int bi = cells.FindIndex(c => c.Board == cells[i].Board && c.W == w && c.V == Vers[0]);
                string born = a.SomN == 0 ? "—" : $"{Per1(a.Born, a.N)}（{Per1(a.LFoe, a.N)} ／ {Per1(a.LSummon, a.N)} ／ {Per1(a.LBall, a.N)}）";
                Console.WriteLine($"| {cells[i].Board} | {cells[i].V.Name} | {F1(a.Win)} | {(bi == i ? "" : (a.Win - res[bi].Win).ToString("+0.0;-0.0;0.0"))} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（上限 {Pct(a.Caps, a.N)}）" : "")} | "
                    + $"{Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {(a.SomN == 0 ? "—" : Dt(a.SomDied, a.SomDeathT, a.N))} | {born} | {Per1(a.HushedL, a.N)} ／ {Per1(a.BlockedL, a.N)} | {Per1(a.Rains, a.N)} ／ {Per1(a.RainL, a.N)} | "
                    + $"{Per0(a.Healed, a.N)} ／ {Per0(a.Overflow, a.N)} | {Per0(a.Inverted, a.N)} ／ {Per0(a.Refused, a.N)} | {(cells[i].V.To.Traits.Contains(TraitId.SparkStore) ? $"{Per1(a.Releases, a.N)} ／ {Per1(a.Swings, a.N)} ／ {Per1(a.PeakSum, a.N)}" : "—")} |");
            }
            if (w.Boss)
            {
                Console.WriteLine();
                Console.WriteLine("### 表C ボス: 1ターンあたりの光（生まれた数 ／ 癒した量）と、勇者の与ダメに対する光の癒しの割合");
                Console.WriteLine();
                Console.WriteLine("T1〜T8 ＝ そのターンを迎えた戦で割った量（生まれた数は規定のソムでも数える＝ソムが生きている間に敵の側で弾けた数）。元の勝ち台の行は、癒しの代わりにツギが書いた量（板 ＋ 回復・戦の全体）。");
                Console.WriteLine();
                Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => $"T{t}")) + " | 勇者の与ダメ ／ 戦 | 癒し ÷ 勇者の与ダメ |");
                Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(1, TMax).Select(_ => "---|")) + "--:|--:|");
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].W != w) continue;
                    var a = res[i];
                    bool orig = cells[i].Board.StartsWith("元の");
                    long heal = orig ? a.TsugiOut : a.Healed;
                    Console.WriteLine($"| {cells[i].Board} | {cells[i].V.Name} | " + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => orig ? "" : $"{Per(a.BornT[t], a.ReachT[t])} ／ {Per0(a.HealT[t], a.ReachT[t])}"))
                        + $" | {Per0(a.HeroDmg, a.N)} | {Pct(heal, a.HeroDmg)} |");
                }
            }
            if (w.Name == "本編 第2波")
            {
                Console.WriteLine();
                Console.WriteLine("### 表D 本編 第2波: 粛（HCD15）");
                Console.WriteLine();
                Console.WriteLine("| 台 | 版 | 粛のひび ／ 戦 | うちソムの光 | 沈黙が砕けた戦 | 砕けたT |");
                Console.WriteLine("|---|---|--:|--:|--:|--:|");
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].W != w || cells[i].Board.StartsWith("元の")) continue;
                    var a = res[i];
                    Console.WriteLine($"| {cells[i].Board} | {cells[i].V.Name} | {Per1(a.Cracks, a.N)} | {Per1(a.SomCracks, a.N)} | {Pct(a.ShatterN, a.HushN)} | {Per1(a.ShatterT, a.ShatterN)} |");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("## 対照（勝率 50% 以上のセル）: ソム → ドルガ（同じ席・seed 0..199・非 verbose）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | 版 | 勝率 | ソム → ドルガ | ソムが要る（半分以下） |");
        Console.WriteLine("|---|---|---|--:|--:|---|");
        for (int i = 0; i < cells.Count; i++)
        {
            if (res[i].Win < 50 || res[i].SomN == 0) continue;
            var f = cells[i].F;
            var cur = f.Occupied().First(o => o.Def.Id == "som").Def;
            int dw = WinsPar(FvSwap(f, cur, UnitCatalog.Dolga), cells[i].W, Seeds);
            double dv = 100.0 * dw / Seeds;
            Console.WriteLine($"| {cells[i].W.Name} | {cells[i].Board} | {cells[i].V.Name} | {F1(res[i].Win)} | {F1(dv)} | {(dv * 2 <= res[i].Win ? "○" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 格子（§4-3）——第294期 `guard294` の `GridCore` と同じ作り（固定しない・版はソムだけ）
    // ---------------------------------------------------------------------------------
    sealed record GridRes(UnitDef[] Order, int[] Wins, long[] WinT, int[][] Ctl);

    static UnitDef[]? _heal;
    /// <summary>いまのヒーラー（第283期の機械的定義・規定のヒサを含む）。走査は1度だけ。</summary>
    internal static UnitDef[] Healers => _heal ??= B283.HealPool306;   // 第308期: 規定のソム（SH-a）を数えない第307期の一覧に固定
    /// <summary>候補を規定の駒に揃える（過去の器具が固定した旧の版 → 同じ Id の `All` の駒）。</summary>
    static UnitDef Cur(UnitDef d) => UnitCatalog.All.First(x => x.Id == d.Id);

    internal static void GridCore(string title, S287.Wave w, UnitDef[] fixedU, UnitDef[] pool, int k, (string Name, Func<Formation, Formation> Apply)[] vars, string phase = "第307期")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedU.Concat(t).ToArray()));
        var res = new GridRes?[boards.Count];
        int cutPass = 0;
        Parallel.For(0, boards.Count, i =>
        {
            var f0 = PinKT311(B283.Seat(boards[i]));
            var fs = vars.Select(v => v.Apply(f0)).ToArray();
            if (!fs.Any(f => Wins(f, w, CutSeeds, out _) >= (w.Boss ? 1 : 5))) return;
            Interlocked.Increment(ref cutPass);
            var wins = new int[vars.Length]; var wt = new long[vars.Length]; var ctl = new int[vars.Length][];
            for (int v = 0; v < vars.Length; v++)
            {
                wins[v] = Wins(fs[v], w, Seeds, out wt[v]);
                ctl[v] = fixedU.Select(_ => -1).ToArray();
                if (w.Boss ? wins[v] == 0 : wins[v] * 2 < Seeds) continue;   // ボスは勝った台すべてで対照（第287期の「届いた」の作法）
                var f = fs[v];
                for (int c = 0; c < fixedU.Length; c++)
                {
                    var occ = f.Occupied().First(o => o.Def.Id == fixedU[c].Id).Def;
                    ctl[v][c] = Wins(FvSwap(f, occ, UnitCatalog.Dolga), w, Seeds, out _);
                }
            }
            res[i] = new GridRes(boards[i], wins, wt, ctl);
        });
        var all = res.Where(r => r is not null).Select(r => r!).ToList();
        bool Half(GridRes r, int v) => r.Wins[v] * 2 >= Seeds;
        bool Reach(GridRes r, int v) => Half(r, v) && r.Ctl[v].Any(c => c >= 0 && c * 2 <= r.Wins[v]);
        bool Need(GridRes r, int v, int c) => Half(r, v) && r.Ctl[v][c] * 2 <= r.Wins[v];
        string Key(UnitDef[] o) => string.Join(",", o.Select(d => d.Id).OrderBy(s => s, StringComparer.Ordinal));

        Console.WriteLine($"# {phase} 格子 × {w.Name} × {title}");
        Console.WriteLine();
        Console.WriteLine($"探索枠{k}（候補 {pool.Length} 枚: {OrderName(pool)}・**ヒーラーを除いた**）＝ {lu.Count} 組 × 席 120 ＝ {boards.Count:N0} 台 × 版 {vars.Length}（{string.Join(" ／ ", vars.Select(v => v.Name))}）。");
        Console.WriteLine($"足切り seed 0..{CutSeeds - 1}（どれかの版で {(w.Boss ? 1 : 5)} 勝以上・{cutPass:N0} 台が通った）→ 全版 seed 0..{Seeds - 1} → 勝率 50% 以上の版で固定枠の駒それぞれ → ドルガ。");
        Console.WriteLine("**届いた ＝ 勝率 50% 以上で、固定枠の駒のどれか1枚 → ドルガで半分以下**（第294期 §4-5 と同じ）。");
        Console.WriteLine();
        Console.WriteLine("## 表1 段ごとの台数（組の数）");
        Console.WriteLine();
        Console.WriteLine("| 段 | " + string.Join(" | ", vars.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(vars.Select(_ => "--:|")));
        void RowP(string label, Func<GridRes, int, bool> pred) =>
            Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => { var l = all.Where(r => pred(r, v)).ToList(); return $"{l.Count:N0}（{l.Select(r => Key(r.Order)).Distinct().Count()}）"; })) + " |");
        RowP("勝率 ≧ 50%", Half);
        RowP("**届いた**", Reach);
        for (int c = 0; c < fixedU.Length; c++) { int cc = c; RowP($"{Short(fixedU[c])}が要る（→ ドルガで半分以下）", (r, v) => Need(r, v, cc)); }
        RowP("勝率 ≧ 90%", (r, v) => r.Wins[v] * 10 >= Seeds * 9);
        RowP("勝率 ＞ 0%", (r, v) => r.Wins[v] > 0);
        if (w.Boss) RowP("勝った台で、固定枠のどれか1枚 → ドルガが 0%（第287期のボスの「届いた」）", (r, v) => r.Wins[v] > 0 && r.Ctl[v].Any(c => c == 0));
        if (w.Boss) RowP("勝った台で、ソム → ドルガが 0%", (r, v) => r.Wins[v] > 0 && r.Ctl[v].Length > 2 && r.Ctl[v][2] == 0);
        Console.WriteLine($"| 最大の勝率（足切りを通った全台） | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => all.Count == 0 ? "—" : $"{F1(100.0 * all.Max(r => r.Wins[v]) / Seeds)}（{OrderName(all.OrderByDescending(r => r.Wins[v]).First().Order)}）")) + " |");
        Console.WriteLine();
        Console.WriteLine(w.Boss ? "## 表2 組（勝った台のある組・版ごとの最大勝率・届いた台の数）" : "## 表2 組（勝率 50% 以上の台のある組・版ごとの最大勝率・届いた台の数）");
        Console.WriteLine();
        Console.WriteLine("| 組（探索枠） | " + string.Join(" | ", vars.Select(v => v.Name + " 最大")) + " | " + string.Join(" | ", vars.Select(v => v.Name + " 届いた台")) + " |");
        Console.WriteLine("|---|" + string.Concat(vars.Select(_ => "--:|")) + string.Concat(vars.Select(_ => "--:|")));
        foreach (var g in all.Where(r => Enumerable.Range(0, vars.Length).Any(v => Half(r, v) || w.Boss && r.Wins[v] > 0)).GroupBy(r => Key(r.Order)).OrderByDescending(g => g.Max(r => r.Wins.Max())))
        {
            var o = g.First().Order.Where(d => !fixedU.Any(x => x.Id == d.Id));
            Console.WriteLine($"| {OrderName(o.ToArray())} | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => F1(100.0 * g.Max(r => r.Wins[v]) / Seeds)))
                + " | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => g.Count(r => Reach(r, v)).ToString())) + " |");
        }
        for (int v = 0; v < vars.Length; v++)
        {
            Console.WriteLine();
            Console.WriteLine($"## 表3-{v + 1} 上位の台（{vars[v].Name}・勝率 50% 以上・上位 15）");
            Console.WriteLine();
            Console.WriteLine($"| # | 席（前1・前3・中央・後1・後3） | 勝率 | 倒しT | 届いた | {string.Join(" | ", fixedU.Select(d => Short(d) + " →"))} | {string.Join(" | ", Enumerable.Range(0, vars.Length).Where(x => x != v).Select(x => vars[x].Name))} |");
            Console.WriteLine("|--:|---|--:|--:|---|" + string.Concat(fixedU.Select(_ => "--:|")) + string.Concat(Enumerable.Range(0, vars.Length - 1).Select(_ => "--:|")));
            var top = all.Where(r => w.Boss ? r.Wins[v] > 0 : Half(r, v)).OrderByDescending(r => r.Wins[v]).ThenBy(r => (double)r.WinT[v] / Math.Max(1, r.Wins[v])).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal).ToList();
            for (int i = 0; i < Math.Min(15, top.Count); i++)
            {
                var r = top[i];
                Console.WriteLine($"| {i + 1} | {OrderName(r.Order)} | {F1(100.0 * r.Wins[v] / Seeds)} | {Per1(r.WinT[v], r.Wins[v])} | {(Reach(r, v) ? "○" : "")} | {string.Join(" | ", r.Ctl[v].Select(c => c < 0 ? "—" : F1(100.0 * c / Seeds)))} | "
                    + string.Join(" | ", Enumerable.Range(0, vars.Length).Where(x => x != v).Select(x => F1(100.0 * r.Wins[x] / Seeds))) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>ボスの格子の候補: 第293期のボスの格子の候補（`Shock293Diag.BossPool`）からソムを除き、シガを足し、規定の駒に揃え、ヒーラーを除く。</summary>
    internal static UnitDef[] BossPool => S293.BossPool.Where(d => d.Id != "som").Append(UnitCatalog.Shiga).Select(Cur).DistinctBy(d => d.Id)
                                         .Where(d => !Healers.Contains(d) && d.Id is not ("kugu" or "kata")).ToArray();
    /// <summary>精鋭の格子の候補: 第287期の候補（`Shock287Diag.Pool`）を規定の駒に揃え、トウ ／ ソムとヒーラーを除く。</summary>
    internal static UnitDef[] ElitePool => S287.Pool.Select(Cur).DistinctBy(d => d.Id).Where(d => d.Id is not ("tou" or "som") && !Healers.Contains(d)).ToArray();

    static void GridBoss(string ver)
    {
        var v = AnyVer(ver);
        var vars = new (string, Func<Formation, Formation>)[] { ("規定", f => f), (v.Name, f => Apply(f, v)) };
        GridCore($"固定枠 クグ ＋ カタ ＋ ソム（規定 ／ {v.Name}）", Waves[0], new[] { UnitCatalog.Kugu, UnitCatalog.KataH311, UnitCatalog.SomH307 }, BossPool, 2, vars);
    }

    static void GridElite(string waveName, string ver)
    {
        var v = AnyVer(ver);
        var vars = new (string, Func<Formation, Formation>)[] { ("規定", f => f), (v.Name, f => Apply(f, v)) };
        GridCore($"固定枠 トウ ＋ ソム（規定 ／ {v.Name}）", WaveOf(waveName), new[] { UnitCatalog.TouH311, UnitCatalog.SomH307 }, ElitePool, 3, vars);
    }

    // ---------------------------------------------------------------------------------
    // 1戦の光の並び
    // ---------------------------------------------------------------------------------
    static void LogOne(string part, string wave, int seed, string ver, int limit)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(part, StringComparison.Ordinal));
        var v = AnyVer(ver);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(Apply(f0, v), BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "喚ばれたもの");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# som307 log —— {name} × {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int shown = 0, lastT = -1;
        bool inRain = false;
        foreach (var x in r.Events)
        {
            bool key = x.Kind == BattleEventKind.Spark || x.Kind == BattleEventKind.ShockSpent || (inRain && x.Kind is BattleEventKind.Heal or BattleEventKind.Damage) || x.Kind == BattleEventKind.Death && x.TargetId is int t && p.Any(u => u.InstanceId == t);
            if (x.Kind == BattleEventKind.Spark) inRain = x.Text == SparkLabels.Release;
            else if (x.Kind is not (BattleEventKind.Heal or BattleEventKind.Damage)) inRain = false;
            if (!key) continue;
            if (shown++ >= limit) { Console.WriteLine("…"); break; }
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}  Team {x.Team}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査（§5）
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    /// <summary>第306期の HEAD で走査したヒーラーの一覧（`HealPool` ／ `HealPool295`）。版のソムは `All` の外なので、この一覧は動かないはず。</summary>
    static readonly string[] HealPool306 = { "ヴェル", "リリ", "シオ", "ササ", "ヒビ", "ツギ", "ベニ", "ヒサ" };
    static readonly string[] HealPool295At306 = { "ヴェル", "リリ", "シオ", "ササ", "ヒビ", "ツギ", "ベニ" };

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som307 check —— 第307期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var som = UnitCatalog.SomH307;

        // (a) 定義
        {
            bool Same(UnitDef d) => d.Id == som.Id && d.Name == som.Name && d.MaxHp == som.MaxHp && d.Attack == som.Attack && d.Speed == som.Speed && d.Advances == som.Advances && d.MinusText == som.MinusText;
            var a = UnitCatalog.SomSHa; var b = UnitCatalog.SomSHb;
            bool ok = Same(a) && Same(b) && Sens.All(v => Same(v.To))
                && som.Traits.SequenceEqual(new[] { TraitId.BetrayedShockNoThunder })
                && a.Traits.SequenceEqual(som.Traits.Append(TraitId.SparkRain)) && b.Traits.SequenceEqual(som.Traits.Append(TraitId.SparkStore))
                && UnitCatalog.SomSHa05.Traits.SequenceEqual(a.Traits.Append(TraitId.SparkHalf)) && UnitCatalog.SomSHa2.Traits.SequenceEqual(a.Traits.Append(TraitId.SparkDouble))
                && UnitCatalog.SomSHb05.Traits.SequenceEqual(b.Traits.Append(TraitId.SparkHalf)) && UnitCatalog.SomSHb2.Traits.SequenceEqual(b.Traits.Append(TraitId.SparkDouble))
                && a.Actions == som.Actions && b.Actions is { Count: 1 } && b.Actions[0].Kind == ActionKind.Skill
                && a.PlusText == som.PlusText + "。敵の側で感電が弾けるたび、その光が仲間に降り注ぎ、傷を癒す" && b.PlusText.EndsWith("光を溜め、手番で仲間に降らせる")
                && Vers.Skip(1).Concat(Sens).All(v => !UnitCatalog.Everyone.Contains(v.To)) && !UnitCatalog.Everyone.Contains(som)
                // 第308期: 規定のソムが SH-a（`SparkRain`）になった——光の札を持つ `All` の駒は規定のソムだけで、中身は版の SH-a と同じ
                && !UnitCatalog.All.Any(d => !ReferenceEquals(d, UnitCatalog.Som) && d.Traits.Any(t => t is TraitId.SparkRain or TraitId.SparkStore or TraitId.SparkHalf or TraitId.SparkDouble))
                && UnitCatalog.SomH308.Traits.SequenceEqual(a.Traits) && UnitCatalog.SomH308.PlusText == a.PlusText && UnitCatalog.SomH308.Flavor == a.Flavor;   // 第309期: 第308期の規定（SH-a）は `SomH308` に残した
            Expect("(a) 定義: 版 ＝ 規定のソム ＋ 札（数値・マイナス・喚び出しは規定のまま）・SH-b は術の手番・`All` ／ `Everyone` の外・光の札の保持者は `All` に 0 枚", ok, $"量 {SparkTrait.Amount}");
        }

        // 直の盤面: 味方 ソム（版）・ドルガ・ゴルム ／ 敵 ドルガ ×3（ガルドは一撃で弾けない札を持つので使わない）
        BattleContext Ctx(UnitDef somDef, out List<UnitState> p, out List<UnitState> e, UnitDef? enemyCenter = null, Formation? pl = null)
        {
            var ctx = H304.Ctx(pl ?? Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Golm, center: somDef),
                               Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Dolga, center: enemyCenter ?? UnitCatalog.Dolga), out p, out e);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 500; }
            return ctx;
        }
        UnitState SomOf(List<UnitState> p) => p.First(u => u.Def.Id == "som");
        int Amt(int n) => n * SparkTrait.Amount;

        // (b) 敵の側の弾けで生まれ、味方の側の弾けでは生まれない
        {
            var ctx = Ctx(UnitCatalog.SomSHa, out var p, out var e);
            var s = SomOf(p); var d = p.First(u => u.Def.Id == "dolga");
            ctx.MarkShock(e[0], s);
            int dh0 = d.Hp;
            ctx.ApplyDamage(e[0], 10, d);
            var t1 = H304.Tal(ctx, "som");
            bool foe = t1.SparkLightFoe == 1 && t1.SparkRains == 1 && d.Hp == dh0 + Amt(1) && s.Hp == 500 + Amt(1);
            ctx.MarkShock(d, e[1]);
            long rains = t1.SparkRains;
            int sh = s.Hp;
            ctx.ApplyDamage(d, 10, e[1]);
            var t2 = H304.Tal(ctx, "som");
            bool ally = t2.SparkRains == rains && t2.SparkAllyPops == 1 && t2.SparkLightFoe == 1 && s.Hp <= sh;
            Expect("(b) 光は敵の側の弾けで生まれ（弾けた 1 → 味方全員・ソム自身も +量）、味方の側の弾けでは生まれない", foe && ally, $"敵 {t1.SparkLightFoe}・味方の弾け {t2.SparkAllyPops}・降った {t2.SparkRains}");
        }

        // (c) 喚ばれたもの ／ 糸玉の弾けも光になる
        {
            var ctx = Ctx(UnitCatalog.SomSHa, out var p, out var e);
            var s = SomOf(p);
            foreach (var tr in s.Traits) tr.OnTurnStart(ctx, s);   // 喚び出し（餌は感電を纏って立つ）
            var fod = ctx.AllUnits.FirstOrDefault(u => BetrayedTrait.IsFodder(u) && u.IsAlive);
            bool summoned = fod is not null && fod.RawCounter(StatusKeys.Shock) > 0;
            if (fod is not null) ctx.ApplyDamage(fod, 1, p.First(u => u.Def.Id == "dolga"));
            var t = H304.Tal(ctx, "som");
            // 糸玉: クグの組の盤面で、敵 e[0] の隣に糸玉を張り、e[0] を弾けさせる
            var ctx2 = Ctx(UnitCatalog.SomSHa, out var p2, out var e2, pl: Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Kugu, center: UnitCatalog.SomSHa));
            var ball = ctx2.PlaceSilkBall(p2.First(u => u.Def.Id == "kugu"), e2[0]);
            ctx2.MarkShock(e2[0], SomOf(p2));
            ctx2.ApplyDamage(e2[0], 10, p2.First(u => u.Def.Id == "dolga"));
            var t2 = H304.Tal(ctx2, "som");
            Expect("(c) 喚ばれたもの ／ 糸玉の弾けも光になる", summoned && t.SparkLightSummon >= 1 && ball is not null && t2.SparkLightBall == 1 && t2.SparkLightFoe >= 1,
                $"喚 {t.SparkLightSummon}（敵 {t.SparkLightFoe}）・糸玉 {t2.SparkLightBall}（敵 {t2.SparkLightFoe}）");
        }

        // (d) ソムが倒れた後は光が生まれない
        {
            var ctx = Ctx(UnitCatalog.SomSHa, out var p, out var e);
            var s = SomOf(p); var d = p.First(u => u.Def.Id == "dolga");
            s.Hp = 0;
            ctx.MarkShock(e[0], d);
            int dh = d.Hp;
            ctx.ApplyDamage(e[0], 10, d);
            var t = H304.Tal(ctx, "som");
            var ctxb = Ctx(UnitCatalog.SomSHb, out var pb, out var eb);
            var sb = SomOf(pb); sb.Hp = 0;
            ctxb.MarkShock(eb[0], pb[0]);
            ctxb.ApplyDamage(eb[0], 10, pb[0]);
            Expect("(d) ソムが倒れた後は光が生まれない（SH-a は降らず・SH-b は溜まらない）", t.SparkRains == 0 && t.SparkDeadPops == 1 && d.Hp == dh && SparkTrait.Of(sb) == 0,
                $"倒れた後の弾け {t.SparkDeadPops}");
        }

        // (e) SH-a: 粛 ／ 痺れの下では癒さず、止まった光は残らない
        {
            var ctx = Ctx(UnitCatalog.SomSHa, out var p, out var e);
            var s = SomOf(p); var d = p.First(u => u.Def.Id == "dolga");
            s.SetCounter(StatusKeys.Stun, 1);
            ctx.MarkShock(e[0], d);
            int dh = d.Hp;
            ctx.ApplyDamage(e[0], 10, d);
            var t = H304.Tal(ctx, "som");
            bool stun = t.SparkBlocked == 1 && t.SparkBlockedLights == 1 && t.SparkRains == 0 && d.Hp == dh;
            s.SetCounter(StatusKeys.Stun, 0);
            ctx.MarkShock(e[1], d);
            ctx.ApplyDamage(e[1], 10, d);
            bool after = H304.Tal(ctx, "som").SparkRains == 1 && H304.Tal(ctx, "som").SparkRainLights == 1;   // 止まった光は持ち越さない（次の連鎖は 1 つだけ）

            var ctxh = Ctx(UnitCatalog.SomSHa, out var ph, out var eh, enemyCenter: EnemyCatalog.HusherHD15);
            var dh2 = ph.First(u => u.Def.Id == "dolga");
            ctxh.MarkShock(eh[0], dh2);
            int hp0 = dh2.Hp;
            ctxh.ApplyDamage(eh[0], 10, dh2);
            var th = H304.Tal(ctxh, "som");
            bool hush = th.SparkHushed == 1 && th.SparkRains == 0 && dh2.Hp == hp0 && ctxh.HushByRoute[(int)OutOfTurnRoute.Spark].Sum() == 1;
            Expect("(e) SH-a: 痺れ ／ 粛の下では癒さず、止まった光は残らない（粛で止まった数は経路「光」に入る）", stun && after && hush,
                $"痺れ {t.SparkBlocked}・粛 {th.SparkHushed}・経路「{OutOfTurnRoutes.Names[(int)OutOfTurnRoute.Spark]}」");
        }

        // (f) SH-b: 光が 0 の手番は殴り、1 以上の手番は殴らずに全部放って 0 に戻る
        {
            var ctx = Ctx(UnitCatalog.SomSHb, out var p, out var e);
            var s = SomOf(p); var d = p.First(u => u.Def.Id == "dolga");
            var store = TraitCatalog.Get(TraitId.SparkStore);
            var act = UnitCatalog.SomSHb.Actions[0];
            int foeHp = e.Sum(u => u.Hp);
            store.OnAction(ctx, s, act);
            bool swung = e.Sum(u => u.Hp) < foeHp && H304.Tal(ctx, "som").SparkSwings == 1;
            ctx.MarkShock(e[0], d); ctx.ApplyDamage(e[0], 10, d);
            ctx.MarkShock(e[1], d); ctx.ApplyDamage(e[1], 10, d);
            int stored = SparkTrait.Of(s);
            int dh = d.Hp, foe2 = e.Sum(u => u.Hp);
            store.OnAction(ctx, s, act);
            var t = H304.Tal(ctx, "som");
            bool rel = stored >= 2 && SparkTrait.Of(s) == 0 && d.Hp == dh + Amt(stored) && e.Sum(u => u.Hp) == foe2 && t.SparkReleases == 1 && t.SparkRainLights == stored;
            Expect("(f) SH-b: 光 0 の手番は殴る・光がある手番は殴らずに全部放って 0 に戻る（光 × 量 を味方全員に）", swung && rel, $"溜まり {stored}・放った {t.SparkReleases}・殴った {t.SparkSwings}");
        }

        // (g) ベニの隣では光の癒しが傷になる
        {
            var ctx = Ctx(UnitCatalog.SomSHa, out var p, out var e, pl: Formation.Build(front1: UnitCatalog.Dolga, center: UnitCatalog.Beni, back3: UnitCatalog.SomSHa));
            var s = SomOf(p); var d = p.First(u => u.Def.Id == "dolga"); var beni = p.First(u => u.Def.Id == "beni");
            bool adj = FormationRules.AreAdjacent(d, beni);
            ctx.MarkShock(e[0], s);
            int dh = d.Hp;
            ctx.ApplyDamage(e[0], 10, s);
            var t = H304.Tal(ctx, "som");
            Expect("(g) ベニの隣では光の癒しが傷になる（いまの規則のまま）", adj && d.Hp < dh && t.SparkInverted >= Amt(1), $"ドルガ {dh} → {d.Hp}・反転 {t.SparkInverted}");
        }

        // (h) ヒーラーの一覧
        {
            var hp = B283.HealPool306.Select(Short).ToArray();   // 第308期: 第307期の一覧（`HealPool306`）に固定
            var hp295 = B283.HealPool295.Select(Short).ToArray();
            Expect("(h) `HealPool` ／ `HealPool295` の中身が第306期と同じ（版のソムは `All` の外）", hp.SequenceEqual(HealPool306) && hp295.SequenceEqual(HealPool295At306),
                $"{string.Join("・", hp)} ／ {string.Join("・", hp295)}");
        }

        // (i) 規定のソムでは光の口が働かない
        {
            long any = 0; int ev = 0;
            foreach (var (n, f) in Boards())
                foreach (var w in Waves.Take(4))
                    for (int sd = 0; sd < 3; sd++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        ev += r.Events.Count(x => x.Kind == BattleEventKind.Spark);
                        foreach (var t in r.TallyByUnit.Values) any += t.SparkChains + t.SparkRains + t.SparkAllyPops + t.SparkDeadPops + t.SparkReleases + t.SparkSwings;
                    }
            Expect("(i) 規定のソムの戦では光の口が1度も働かない（計数 0・`Spark` の出来事 0）", any == 0 && ev == 0, $"{any} ／ {ev}");
        }

        // (j) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var (bn, f) in Boards())
                foreach (var v in Vers.Skip(1).Concat(Sens))
                    foreach (var w in Waves)
                        for (int sd = 0; sd < 4; sd++)
                        {
                            var fv = Apply(f, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            var r3 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns || r2.Events.Count != r3.Events.Count) nd++;
                        }
            Expect("(j) 決定的・verbose の有無で勝敗と決着T が変わらない（版 6 枚 × 代表台 × 8 波）", nd == 0, $"{nd} ／ {n} 件");
        }

        // (k) SH-b の手番: 実戦で、放った手番の後は溜まりが 0・放った手番に攻撃していない
        {
            int bad = 0, rel = 0;
            var f = Apply(Boards().First(b => b.Name == "勝ち台 ツギ→ソム").F, Vers[2]);
            foreach (var w in Waves.Take(3))
                for (int sd = 0; sd < 20; sd++)
                {
                    var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), sd, verbose: true);
                    int sid = p.First(u => u.Def.Id == "som").InstanceId;
                    for (int i = 0; i < r.Events.Count; i++)
                    {
                        var x = r.Events[i];
                        if (x.Kind != BattleEventKind.Spark || x.Text != SparkLabels.Release) continue;
                        rel++;
                        // 直前の Skill（ソムの術）から次の TurnStart ／ 別の駒の Skill・Attack までに、ソムの Attack が無い
                        for (int j = i + 1; j < r.Events.Count; j++)
                        {
                            var y = r.Events[j];
                            if (y.Kind == BattleEventKind.Heal || y.Kind == BattleEventKind.Damage && y.ActorId == sid && y.Reaction) continue;
                            if (y.Kind == BattleEventKind.Attack && y.ActorId == sid) bad++;
                            break;
                        }
                    }
                }
            Expect("(k) SH-b: 実戦で、光を放った手番にソムは攻撃しない", rel > 0 && bad == 0, $"放った {rel}・その直後のソムの攻撃 {bad}");
        }

        // (l) 乱数: PickOne ／ Roll の出現数が HEAD 以下
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show HEAD:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var pr = System.Diagnostics.Process.Start(psi)!;
                    string o = pr.StandardOutput.ReadToEnd();
                    pr.WaitForExit();
                    return pr.ExitCode == 0 ? o : null;
                }
                catch { return null; }
            }
            var notes = new List<string>();
            bool clean = true;
            foreach (var path in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
            {
                string now = File.ReadAllText(path);
                string? head = Head(path);
                if (head is null) { clean = false; notes.Add($"{path}: HEAD を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" })
                {
                    int a0 = Count(head, pat), a1 = Count(now, pat);
                    if (a1 > a0) clean = false;
                    notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}");
                }
            }
            Expect("(l) `PickOne(` ／ `Roll(` を新たに使っていない（BattleCore の出現数が HEAD 以下）", clean, string.Join("・", notes));
        }

        // (m) 経路の名前と札の分類
        {
            bool route = OutOfTurnRoutes.Names.Length == Enum.GetValues<OutOfTurnRoute>().Length && OutOfTurnRoutes.Names[(int)OutOfTurnRoute.Spark] == "光" && OutOfTurnRoutes.Names[^1] == "その他";
            Expect("(m) 経路「光」が `OutOfTurnRoutes.Names` に並び、「その他」が末尾のまま", route, $"{OutOfTurnRoutes.Count} 本");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
