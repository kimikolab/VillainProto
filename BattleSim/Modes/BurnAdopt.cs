using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using BH = BurnHitDiag;

// =====================================================================================
// burnadopt —— 第256期「被弾の燃焼を規定に・セロの状態の矢を抜く・ドハの落ちやすさの確認」。
// 指示書は design/PHASE256_BURN_HIT_ADOPT_SPEC.md ／ 報告は design/PHASE256_BURN_HIT_ADOPT.md。
//   規定化: `EmberRule.Default` に `BurnHit = true`（第255期 H-分担 を札の持ち主に依らず両陣営に）。
//           第255期までは `EmberRule.Pre256`。セロは状態の矢を外し、旧は `UnitCatalog.SeroS0`。
//
//     dotnet run --project BattleSim -c Release 0 burnadopt check     # 自己検査（受け入れ 2・3）
//     dotnet run --project BattleSim -c Release 0 burnadopt compare   # `compare` 61 行の動いたセルを原因（燃焼 ／ セロ）で分ける
//     dotnet run --project BattleSim -c Release 0 burnadopt doha      # §3 ドハの落ちやすさ（T3-238）
//     dotnet run --project BattleSim -c Release 0 burnadopt log <版> [seed] [波 0〜3] [倍率 0/1/2]   # T3-238 の1戦
// =====================================================================================
static partial class BurnAdoptDiag
{
    static partial void Check();
    static partial void CompareSplit();
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "check": Check(); return;
            case "compare": CompareSplit(); return;
            case "doha": Doha(); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "H-分担", args.Length > 4 ? int.Parse(args[4]) : 0,
                    args.Length > 5 ? int.Parse(args[5]) : 0, args.Length > 6 ? int.Parse(args[6]) : 1);
                return;
            default:
                Console.WriteLine("burnadopt: モードは check / compare / doha / log。");
                return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版（§3 の比較）。札は第255期の器具と同じくボルグに足す（`BH.Mark`）。
    // ---------------------------------------------------------------------------------
    internal sealed record AVer(string Name, string What, EmberRule Ember, TraitId? Card);
    internal static readonly AVer[] Versions =
    {
        new("H0", "第255期までの規定（`EmberRule.Pre256`）", EmberRule.Pre256, null),
        new("H-分担", "第256期の規定（`EmberRule.Default`）", EmberRule.Default, null),
        new("H-敵だけ", "`Pre256` ＋ 札 `BurnHitFoeOnly`（第255期の対照）", EmberRule.Pre256, TraitId.BurnHitFoeOnly),
    };
    internal static AVer VerOf(string n) => Versions.First(v => v.Name == n);

    internal static Formation Board => FC.T3238;   // ヒヨ前1・ホタ前3・ボルグ中央・ドハ後1・ソラ後3
    internal const int Seeds = 200;

    static BattleResult Fight(AVer v, Formation f, int w, int s, int seed, bool verbose, bool census)
        => BattleEngine.Run(BattleEngine.Materialize(BH.Mark(f, v.Card), BattleContext.PlayerTeam), FC.WaveOf(w, BA.Scales[s].Sc)(), seed,
            verbose: verbose, ember: v.Ember, harm: census ? new HarmRule(true) : null);

    static void LogOne(string ver, int seed, int w, int s)
    {
        var r = Fight(VerOf(ver), Board, w, s, seed, true, true);
        Console.WriteLine($"# {ver} × T3-238 × {FC.WaveNames[w]} × {BA.Scales[s].Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    internal static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
    internal static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
}
