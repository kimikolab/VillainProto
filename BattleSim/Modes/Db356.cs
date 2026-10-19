using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;

// =====================================================================================
// db356 —— 第356期【軽い期】段0: 呪われた傷は癒えない（N-100）を規定に ／ 本体: 壁を入れた呪いの台を探す（席の総当たり）。
// 指示書は design/PHASE356_CURSE_WALL_SPEC.md ／ 報告は design/PHASE356_CURSE_WALL.md。波と1戦の計数は `inv348` ／ `db351` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 db356 seats               # §3: ネル ／ ワタ ／ ウツ ＋ 6枚から2枚（15 組）× 席の全並び 120 ＝ 1,800 並び × ボス（seed 0..4）／ 近衛 ／ 大隊（seed 0..19）
//     dotnet run --project BattleSim -c Release 0 db356 boards              # §5: 代表台（§3 の上位）＋ 試遊・呪い × 波（九体を含む）・seed 0..99・勇者の最大HPの推移（T1〜T14）
//     dotnet run --project BattleSim -c Release 0 db356 axes                # §5: 軸の横並び（第348期 §3）のデバフの行に代表台を足す
//     dotnet run --project BattleSim -c Release 0 db356 log <台> <波> [seed]  # 1戦のログ（台 play ／ wall ／ 並びの名前「ワタ・ムド・…」）
//     dotnet run --project BattleSim -c Release 0 db356 check               # 自己検査
//
// **予測のファイルが無ければ本測定（seats ／ boards ／ axes）を走らせない**（R397・`.tmp/p356/predict.md`）。
// =====================================================================================
static partial class Db356Diag
{
    internal const string PredictFile = ".tmp/p356/predict.md";
    const string Tag = "第356期";
    const int Seeds = 100, BossSeeds = 5, EliteSeeds = 20, TrajT = 14;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "boards" or "axes" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"db356: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "seats": SeatsAll(); return;
            case "boards": BoardsAll(); return;
            case "axes": AxesAll(); return;
            case "log": Log(A(3, "play"), A(4, "boss"), int.Parse(A(5, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("db356: モードは seats / boards / axes / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    // ---------------------------------------------------------------------------------
    // 母集団（§3-1）
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] Fixed = { UnitCatalog.Nel, UnitCatalog.Wata, UnitCatalog.Utsu };
    internal static readonly UnitDef[] Pool = { UnitCatalog.Mudo, UnitCatalog.Kubi, UnitCatalog.Golm, UnitCatalog.Gald, UnitCatalog.Ban, UnitCatalog.Tsugi };
    internal sealed record Seat(string Key, string Pair, Formation F);

    /// <summary>15 組 × 5枚の席の全並び 120 ＝ 1,800 並び（同じ組の2枚の入れ替えも別に数える・決めたこと）。並びの順は組 → 席の辞書順（乱数を引かない）。</summary>
    internal static Seat[] AllSeats()
    {
        var list = new List<Seat>();
        for (int i = 0; i < Pool.Length; i++)
            for (int j = i + 1; j < Pool.Length; j++)
            {
                var units = Fixed.Append(Pool[i]).Append(Pool[j]).ToArray();
                string pair = $"{Short(Pool[i])}＋{Short(Pool[j])}";
                foreach (var perm in Perms(5))
                {
                    var f = new Formation();
                    for (int s = 0; s < 5; s++) f[s] = units[perm[s]];
                    list.Add(new Seat(Seats(f), pair, f));
                }
            }
        return list.ToArray();
    }
    static IEnumerable<int[]> Perms(int n)
    {
        var a = Enumerable.Range(0, n).ToArray();
        IEnumerable<int[]> Go(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var p in Go(k + 1)) yield return p;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Go(0).OrderBy(p => string.Join(",", p), StringComparer.Ordinal).ToList();
    }

    internal static Formation Play => Presets.Versions.First(r => r.Name == D.PlayRow).F;
    internal const string WallRow = "試遊・呪い 壁";
    static Formation ByKey(string key)
    {
        var defs = key.Split('・').Select(n => Fixed.Concat(Pool).First(d => Short(d) == n)).ToArray();
        var f = new Formation();
        for (int s = 0; s < 5; s++) f[s] = defs[s];
        return f;
    }

    static void Log(string board, string wave, int seed)
    {
        var f = board switch { "play" => Play, "wall" => Presets.Versions.First(r => r.Name == WallRow).F, _ => ByKey(board) };
        var w = D.WaveOf(wave);
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {Seats(f)} × {w.Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // 1戦（計数を渡さない素の戦・verbose なし）: 勝ち ／ 倒しT ／ 倒れた数 ／ 全滅 T（最後に倒れた T）／ 最初に倒れた駒
    // ---------------------------------------------------------------------------------
    internal sealed class Cell
    {
        public long N, Wins, WinT, Fell, Lost, WipeT;
        public readonly Dictionary<string, long> First = new();
        public void Add(Cell o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Lost += o.Lost; WipeT += o.WipeT;
            foreach (var (k, v) in o.First) First[k] = First.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }
    internal static Cell Fight(Formation f, I.W w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false, boss: new BossRule(false) { Scale = w.Scale });
        var c = new Cell { N = 1, Fell = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { c.Wins = 1; c.WinT = r.Turns; }
        else { c.Lost = 1; c.WipeT = p.Max(u => u.LastDeathTurn); }
        var first = p.Where(u => u.LastDeathTurn > 0).OrderBy(u => u.LastDeathTurn).ThenBy(u => u.Slot).FirstOrDefault();
        if (first is not null) c.First[Short(first.Def)] = 1;
        return c;
    }
    internal static Cell CellOf(Formation f, I.W w, int n)
    {
        var parts = new Cell[n];
        Parallel.For(0, n, s => parts[s] = Fight(f, w, s));
        var all = new Cell();
        foreach (var x in parts) all.Add(x);
        return all;
    }
}
