using BattleCore;
using static Common;
using D326 = Dense326Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using T323 = Tou323Diag;
using V332 = Veil332Diag;

// =====================================================================================
// settle333 —— 第333期【軽い期・規定化だけ】ソムの光の衣に上限（LV-c）を規定にする。
// 指示書は design/PHASE333_SOM_VEIL_CAP_SETTLE_SPEC.md ／ 報告は design/PHASE333_SOM_VEIL_CAP_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 settle333 boards                     # §4 代表台 7 × 5 波 × 旧 ／ 新（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 settle333 log [台] [波] [seed] [old|new] # 1戦のログ（台の名前・pon）
//     dotnet run --project BattleSim -c Release 0 settle333 check                      # §7 自己検査
//
// 旧 ＝ 第320〜332期の規定のソム（`SomH332`・`Common.Pin332`）。新 ＝ いまの規定（LV-c）。
// **予測のファイルが無ければ本測定（boards）を走らせない**（R397・`.tmp/p333/predict.md`）。
// =====================================================================================
static partial class Settle333Diag
{
    internal const string PredictFile = ".tmp/p333/predict.md";
    const int Seeds = 100;   // 軽い期
    const string Tag = "第333期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"settle333: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "log": Log(A(3, "pon"), A(4, "boss"), int.Parse(A(5, "0")), A(6, "new") == "old"); return;
            case "boards": BoardsAll(); return;
            case "check": Check(); return;
            default: Console.WriteLine("settle333: モードは log / boards / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    /// <summary>ポンの台（第326〜332期と同じ席）。駒はいまの規定。</summary>
    internal static Formation PonBoard() => Formation.Build(front1: UnitCatalog.Kata, front3: UnitCatalog.Shiga, center: UnitCatalog.Tou, back1: UnitCatalog.Kugu, back3: UnitCatalog.Som);
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;

    /// <summary>§4 の代表台（駒はいまの規定）。旧は <see cref="Pin332"/> を通す。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("ポンの台", PonBoard()),
        ("光の盾 鞭", Playtest("試遊・感電 光の盾 鞭")),
        ("光の盾", Playtest("試遊・感電 光の盾")),
        ("粉 隣あり", Playtest("試遊・感電 粉 隣あり")),
        ("光の盾 重 鞭", Playtest("試遊・感電 光の盾 重 鞭")),
        ("瓦礫（ソム ＋ ツギ）", Playtest("試遊・感電 瓦礫")),
        ("感電", CompareBuilds().First(r => r.Name == "感電 (シガ×カタ×ソム)").F),
    };

    internal static readonly S287.Wave[] Waves = V332.Waves;
    static S287.Wave WaveOf(string n) => T323.WaveOf(n);

    /// <summary>1戦の計数: `veil332` の計数 ＋ 最初に倒れた味方（駒の名前 → 回数 ／ ターンの和）。</summary>
    internal sealed class Z
    {
        public V332.Z V = new();
        public Dictionary<string, (long N, long T)> First = new();
        public long FirstN;
        public void Merge(Z o)
        {
            V.Merge(o.V); FirstN += o.FirstN;
            foreach (var (k, (n, t)) in o.First) { var (a, b) = First.GetValueOrDefault(k); First[k] = (a + n, b + t); }
        }
    }

    internal static Z Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var z = new Z { V = new V332.Z { D = D326.CountZ(p, e, r, w) } };
        var pDefs = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (id == "som") { z.V.Added += t.SparkVeilAdded; z.V.Capped += t.SparkVeilCapped; z.V.Expired += t.Lv332Expired; }
            if (pDefs.Contains(id)) { z.V.Soaked += t.SparkVeilSoaked; z.V.ArmorLost += t.Lv332ArmorLost; z.V.PlankSoaked += t.PlankSoaked; }
            if (id == "tsugi") z.V.PlankPlaced += t.PlankGiven;
        }
        var byId = p.ToDictionary(u => u.InstanceId, u => S307.Short(u.Def));
        var first = r.Events.FirstOrDefault(ev => ev.Kind == BattleEventKind.Death && ev.TargetId is int t && byId.ContainsKey(t));
        if (first is not null) { z.FirstN = 1; z.First[byId[first.TargetId!.Value]] = (1, first.Turn); }
        return z;
    }

    internal static Z Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new Z[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var z = new Z();
        foreach (var c in parts) z.Merge(c);
        return z;
    }

    static void Log(string board, string wave, int seed, bool old)
    {
        var (name, f0) = Boards().First(b => b.Name == board || (board == "pon" && b.Name == "ポンの台"));
        var w = WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(old ? Pin332(f0) : f0, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {(old ? "旧（第320〜332期の規定のソム）" : "新（規定）")}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }
}
