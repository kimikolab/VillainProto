using BattleCore;
using static Common;
using D326 = Dense326Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// dust327 —— 第327期【軽い期】段0「DR-a の規定化」／ 本体「トウの粉の粉塵爆発」（DX ／ DX ＋ 電池 ／ DX ＋ 隣帯電 ／ DX ＋ 全部）。
// 指示書は design/PHASE327_DUST_BURST_SPEC.md ／ 報告は design/PHASE327_DUST_BURST.md。測定の作法は design/MEASURE_LIGHT.md（軽い期）。
//
//     dotnet run --project BattleSim -c Release 0 dust327 p0                     # §3 Phase 0（勇者の隣の糸玉・粉は糸玉に落ちるか）
//     dotnet run --project BattleSim -c Release 0 dust327 log [波] [seed] [版]   # ポンの台の1戦のログ（読み物）
//     dotnet run --project BattleSim -c Release 0 dust327 boards [版,…]          # §5 代表台 5 × 5 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 dust327 compare [版,…]         # §5 `compare` 64 行 × 版（seed 0..199・基準は規定 ＝ DR-a）
//     dotnet run --project BattleSim -c Release 0 dust327 check                  # §6 自己検査
//
// 版の名前（ASCII）: base（規定 ＝ DR-a）／ dx ／ dxb（DX ＋ 電池）／ dxn（DX ＋ 隣帯電）／ dxall（DX ＋ 全部）。波: boss ／ guard ／ bat ／ 2 ／ 5。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p327/predict.md`）。
// =====================================================================================
static partial class Dust327Diag
{
    internal const string PredictFile = ".tmp/p327/predict.md";
    const int Seeds = 100;          // 軽い期（design/MEASURE_LIGHT.md §1）
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第327期";
    internal const int MaxT = D326.MaxT;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"dust327: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "log": Log(A(3, "boss"), int.Parse(A(4, "10")), V(A(5, "dx"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("dust327: モードは p0 / log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long H(long[]? h, int i) => T323.H(h, i);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    /// <summary>版。トウ ／ クグ ／ ソムを Id で差し替える（いない台では何もしない）。</summary>
    internal sealed record Ver(string Name, string Ascii, UnitDef Tou, UnitDef Kugu, UnitDef Som);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.TouH327, UnitCatalog.KuguH327, UnitCatalog.SomH332),
        new("DX", "dx", UnitCatalog.TouDX, UnitCatalog.KuguH327, UnitCatalog.SomH332),
        new("DX＋電池", "dxb", UnitCatalog.TouDX, UnitCatalog.KuguBat, UnitCatalog.SomH332),
        new("DX＋隣帯電", "dxn", UnitCatalog.TouDX, UnitCatalog.KuguH327, UnitCatalog.SomBc),
        new("DX＋全部", "dxall", UnitCatalog.TouDX, UnitCatalog.KuguBat, UnitCatalog.SomBc),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(S316.SwapId(S316.SwapId(f, "tou", v.Tou), "kugu", v.Kugu), "som", v.Som);

    /// <summary>ポンの台（第326期の `PonBoard` と同じ席）: 前1 カタ・前3 シガ・中央 トウ・後1 クグ・後3 ソム。駒はいまの規定（トウは DR-a）。</summary>
    internal static Formation PonBoard() => Formation.Build(front1: UnitCatalog.KataH331, front3: UnitCatalog.Shiga, center: UnitCatalog.TouH327, back1: UnitCatalog.KuguH327, back3: UnitCatalog.SomH332);

    static Formation Playtest(string n) => PlaytestH327.First(r => r.Name == n).F;   // 第328期: トウ ／ クグを第327期の規定に固定

    /// <summary>§5 の代表台（駒はいまの規定）。ポンの台を最初に。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("ポンの台", PonBoard()),
        ("光の盾 鞭", Playtest("試遊・感電 光の盾 鞭")),
        ("光の盾", Playtest("試遊・感電 光の盾")),
        ("粉 隣あり", Playtest("試遊・感電 粉 隣あり")),
        ("糸", Playtest("試遊・感電 糸")),
    };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    static S287.Wave WaveOf(string n) => T323.WaveOf(n);
    static bool IsElite(S287.Wave w) => T323.IsElite(w);

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Z
    {
        public D326.Z D = new();
        public UnitTally Som = new();
        /// <summary>ターンの終わりの: 盤上の糸玉の数 ／ 勇者の隣の糸玉の数（ボスだけ）／ 糸玉の濃さの最大 ／ そのターンまで続いた戦。</summary>
        public long[] Balls = new long[MaxT], BallAdj = new long[MaxT], BallMax = new long[MaxT], Alive = new long[MaxT];
        public long N => D.N;
        public long Wins => D.Wins;
        public double Win => D.Win;
        public UnitTally T => D.T;
        public UnitTally G => D.Base.G;
        public void Merge(Z o)
        {
            D.Merge(o.D); Som.Add(o.Som);
            for (int i = 0; i < MaxT; i++) { Balls[i] += o.Balls[i]; BallAdj[i] += o.BallAdj[i]; BallMax[i] += o.BallMax[i]; Alive[i] += o.Alive[i]; }
        }
    }

    internal static Z Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var z = new Z { D = D326.CountZ(p, e, r, w) };
        if (r.TallyByUnit.TryGetValue("som", out var st)) z.Som.Add(st);
        UnitState? hero = w.Boss && e.Count == 1 ? e[0] : null;
        var slot = new Dictionary<int, int>();   // 糸玉 → 席
        var dens = new Dictionary<int, int>();   // 糸玉 → 濃さ
        int cur = 1, last = Math.Min(r.Turns, MaxT - 1);
        void Flush(int upto)
        {
            for (; cur <= Math.Min(upto, last); cur++)
            {
                z.Alive[cur]++;
                z.Balls[cur] += slot.Count;
                z.BallMax[cur] += dens.Count == 0 ? 0 : dens.Values.Max();
                if (hero is not null) z.BallAdj[cur] += slot.Values.Count(s => hero.Shape.AreAdjacent(hero.Slot, s));
            }
        }
        foreach (var ev in r.Events)
        {
            if (ev.Turn > cur) Flush(ev.Turn - 1);
            if (ev.Kind == BattleEventKind.SilkBall && ev.Text == SilkBallLabels.Place && ev.TargetId is int bi && ev.Team == BattleContext.EnemyTeam) slot[bi] = ev.Slot;
            else if (ev.Kind == BattleEventKind.ShockGauge && ev.Text == ShockGaugeLabels.Dense && ev.TargetId is int di && slot.ContainsKey(di)) dens[di] = ev.Amount;
        }
        Flush(last);
        return z;
    }

    internal static Z Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var parts = new Z[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, from + i));
        var z = new Z();
        foreach (var c in parts) z.Merge(c);
        return z;
    }

    /// <summary>爆発で減らした HP（受け手 <paramref name="rk"/>（0 味方 ／ 1 敵 ／ 2 召喚獣 ／ 3 糸玉）→ 浴びた駒 <paramref name="vk"/>（0 味方 ／ 1 敵 ／ 2 召喚獣））。</summary>
    static long Dx(UnitTally t, int rk, int vk) => H(t.Dx327Dealt, rk * 3 + vk);
    static long DxTo(UnitTally t, int vk) => Enumerable.Range(0, 4).Sum(rk => Dx(t, rk, vk));
    static long DxN(UnitTally t) => Enumerable.Range(0, 4).Sum(rk => H(t.Dx327N, rk));

    // ---------------------------------------------------------------------------------
    // log（読み物）
    // ---------------------------------------------------------------------------------
    static void Log(string wave, int seed, Ver v)
    {
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(Apply(PonBoard(), v), BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: true);
        Console.WriteLine($"# ポンの台 × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }
}
