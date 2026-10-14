using BattleCore;
using static Common;
using D326 = Dense326Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// veil332 —— 第332期【軽い期】段0: 痺れは1ターン1回（ST-c）を規定に ／ 本体: ソムの光の衣を積もらせない（LV-c 上限あり ／ LV-d 一時の衣）。
// 指示書は design/PHASE332_STUN_ONCE_SOM_VEIL_SPEC.md ／ 報告は design/PHASE332_STUN_ONCE_SOM_VEIL.md。測定の作法は design/MEASURE_LIGHT.md（軽い期）。
//
//     dotnet run --project BattleSim -c Release 0 veil332 p0                     # §4 Phase 0（衣に頼ってボスに勝つ台・ソム ＋ ツギの台の衣 ／ 板の割合）
//     dotnet run --project BattleSim -c Release 0 veil332 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 veil332 boards [版,…]          # §6 代表台 × 5 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 veil332 compare [版,…]         # §6 `compare` 64 行 × 版（seed 0..199・基準は規定）
//     dotnet run --project BattleSim -c Release 0 veil332 check                  # §8 自己検査
//
// 版の名前（ASCII）: base（規定 ＝ LV-a）／ c（LV-c）／ d（LV-d）。版はソムを差し替える。波: boss ／ guard ／ bat ／ 2 ／ 5。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p332/predict.md`）。
// =====================================================================================
static partial class Veil332Diag
{
    internal const string PredictFile = ".tmp/p332/predict.md";
    const int Seeds = 100;          // 軽い期（design/MEASURE_LIGHT.md §1）
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第332期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"veil332: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "log": Log(A(3, "pon"), A(4, "boss"), int.Parse(A(5, "0")), V(A(6, "d"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("veil332: モードは p0 / log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef Som);

    internal static readonly Ver[] Vers =
    {
        new("規定（LV-a）", "base", UnitCatalog.Som),
        new("LV-c", "c", UnitCatalog.SomLVc332),
        new("LV-d", "d", UnitCatalog.SomLVd332),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, "som", v.Som);

    /// <summary>Phase 0 の参考（版ではない）: 規定のソムから光の衣の札（`SparkVeil`）だけを外した駒。「衣に頼っているか」を測るためだけに器具の中で組む。</summary>
    internal static readonly UnitDef SomNoVeil = new()
    {
        Id = UnitCatalog.Som.Id, Name = UnitCatalog.Som.Name, MaxHp = UnitCatalog.Som.MaxHp, Attack = UnitCatalog.Som.Attack, Speed = UnitCatalog.Som.Speed,
        Advances = UnitCatalog.Som.Advances, Pattern = UnitCatalog.Som.Pattern, Traits = UnitCatalog.Som.Traits.Where(t => t != TraitId.SparkVeil).ToArray(),
        Actions = UnitCatalog.Som.Actions, PlusText = UnitCatalog.Som.PlusText, MinusText = UnitCatalog.Som.MinusText, Flavor = UnitCatalog.Som.Flavor,
    };

    /// <summary>ポンの台（第326〜331期と同じ席）。駒はいまの規定（段0 入り）。</summary>
    internal static Formation PonBoard() => Formation.Build(front1: UnitCatalog.Kata, front3: UnitCatalog.Shiga, center: UnitCatalog.Tou, back1: UnitCatalog.Kugu, back3: UnitCatalog.Som);
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;

    /// <summary>ソムのいる候補（`compare` の行 ＋ 試遊の台）。Phase 0 の §4-1 はここから選ぶ。</summary>
    internal static (string Name, Formation F)[] SomRows() => CompareBuilds().Concat(Presets.Playtest).Where(r => r.F.Occupied().Any(o => o.Def.Id == "som")).ToArray();

    /// <summary>§4-1 で選んだ台（Phase 0 の結果・衣を外すとボスの勝率がいちばん落ちる3台）。</summary>
    internal static readonly string[] VeilBossRows = { "感電 (シガ×カタ×ソム)", "試遊・感電 粉 隣あり", "試遊・感電 光の盾 重 鞭" };   // `試遊・感電 打ち返し` は `感電` と同じ並びなので外した

    /// <summary>§4-2 のソム ＋ ツギの台（試遊の台）。</summary>
    internal const string SomTsugiRow = "試遊・感電 瓦礫";

    /// <summary>§6 の代表台（駒はいまの規定）。ポンの台を最初に。</summary>
    internal static (string Name, Formation F)[] Boards()
    {
        var l = new List<(string, Formation)>
        {
            ("ポンの台", PonBoard()),
            ("光の盾 鞭", Playtest("試遊・感電 光の盾 鞭")),
            ("光の盾", Playtest("試遊・感電 光の盾")),
        };
        foreach (string n in VeilBossRows) l.Add((n, SomRows().First(r => r.Name == n).F));
        l.Add(("瓦礫（ソム ＋ ツギ）", Playtest(SomTsugiRow)));
        return l.ToArray();
    }

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    static S287.Wave WaveOf(string n) => T323.WaveOf(n);
    static bool IsElite(S287.Wave w) => T323.IsElite(w);

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Z
    {
        public D326.Z D = new();
        /// <summary>ソムの衣: 得た ／ 捨てた（LV-c）／ 消えた（LV-d）・味方の側: 衣が受け止めた ／ 破片が減った延べ（衣か板かを問わない）／ 板の印があった間に減った。</summary>
        public long Added, Capped, Expired, Soaked, ArmorLost, PlankSoaked, PlankPlaced;
        public long N => D.N;
        public long Wins => D.Wins;
        public double Win => D.Win;
        public long Left => Added - Soaked - Expired;
        public void Merge(Z o)
        {
            D.Merge(o.D);
            Added += o.Added; Capped += o.Capped; Expired += o.Expired; Soaked += o.Soaked; ArmorLost += o.ArmorLost; PlankSoaked += o.PlankSoaked; PlankPlaced += o.PlankPlaced;
        }
    }

    internal static Z Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var z = new Z { D = D326.CountZ(p, e, r, w) };
        var pDefs = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (id == "som") { z.Added += t.SparkVeilAdded; z.Capped += t.SparkVeilCapped; z.Expired += t.Lv332Expired; }
            if (pDefs.Contains(id)) { z.Soaked += t.SparkVeilSoaked; z.ArmorLost += t.Lv332ArmorLost; z.PlankSoaked += t.PlankSoaked; }
            if (id == "tsugi") z.PlankPlaced += t.PlankGiven;
        }
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

    static void Log(string board, string wave, int seed, Ver v)
    {
        var (name, f0) = board == "pon" ? ("ポンの台", PonBoard()) : Boards().First(b => b.Name == board);
        var w = WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, v), BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }
}
