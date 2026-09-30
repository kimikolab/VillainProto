using BattleCore;
using static Common;

// =====================================================================================
// hanereach —— ハネの「突き返す」の空振り（ポンの観察・移動改の編成 × 検証・九 / 農兵）。
//
//     dotnet run --project BattleSim -c Release 0 hanereach trace [seed] [hp] [atk]   # 1戦の engine ログと台本を並べる
//     dotnet run --project BattleSim -c Release 0 hanereach run                       # 版 V0〜V3 × 台 × 倍率（空振り・決着T）
//     dotnet run --project BattleSim -c Release 0 hanereach check                     # 自己検査
// =====================================================================================
static partial class HaneReachDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "trace": Trace(args); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            default: Console.WriteLine("hanereach: モードは trace / run / check。"); return;
        }
    }

    static partial void RunImpl();
    static partial void CheckImpl();

    /// <summary>ポンの画面の編成（前1 リリ ／ 前3 ササ ／ 中央 ツギ ／ 後1 ハネ ／ 後3 シオ）。</summary>
    internal static Formation Pon(UnitDef hane) => Formation.Build(front1: UnitCatalog.Lili, front3: UnitCatalog.Sasa,
        center: UnitCatalog.Tsugi, back1: hane, back3: UnitCatalog.Shio);

    internal static EnemyWave Levy9 => EnemyCatalog.TestStages.First(t => t.Name == "検証・九 / 農兵").Enemy;

    static void Trace(string[] args)
    {
        int seed = args.Length > 3 ? int.Parse(args[3]) : 12;
        int hp = args.Length > 4 ? int.Parse(args[4]) : 1000;
        int atk = args.Length > 5 ? int.Parse(args[5]) : 300;
        string tag = args.Length > 6 ? args[6] : "V0";
        var hane = VerOf(tag).Hane;
        var p = BattleEngine.Materialize(Pon(hane), BattleContext.PlayerTeam);
        var e = BattleEngine.MaterializeEnemy(Levy9, new EnemyScaleRule(hp, atk));
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => $"{u.Name}#{u.InstanceId}");
        Console.WriteLine($"# trace seed={seed} 敵 {hp}/{atk} 版 {tag} —— 勝ち={r.PlayerWon} ターン={r.Turns}");
        Console.WriteLine("## engine ログ");
        foreach (var l in r.Log) Console.WriteLine($"[{l.Kind}] {l}");
        Console.WriteLine("## 台本");
        string N(int? id) => id is int i && names.TryGetValue(i, out var s) ? s : (id?.ToString() ?? "-");
        foreach (var ev in r.Events)
            Console.WriteLine($"T{ev.Turn} {ev.Kind} a={N(ev.ActorId)} t={N(ev.TargetId)} amt={ev.Amount} slot={ev.Slot}"
                + (ev.Reaction ? " R" : "") + (ev.Relayed ? " Rel" : "") + (string.IsNullOrEmpty(ev.Text) ? "" : $" \"{ev.Text}\""));
    }
}
