using BattleCore;
using static Common;

// =====================================================================================
// demo モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "demo")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 demo
// =====================================================================================

static class DemoDiag
{
public static void Run(string[] args, int stageIndex)
{
    EnemyCatalog.Stage stage = EnemyCatalog.Stages[stageIndex];

    // 第3引数に編成名の部分一致を渡すと、compare の編成をそのまま1戦ぶん詳細ログで流す
    // （`... <n> demo "仇討ち"`）。省略時は従来どおり下の固定編成。
    // 新しい特性が**実際に発火しているか**はログの並びでしか読めない——勝率は
    // 「発火したが足りなかった」と「一度も発火しなかった」を区別しない。
    string demoWant = args.Length > 2 ? args[2] : "";
    int demoSeed = args.Length > 3 && int.TryParse(args[3], out int ds) ? ds : 7;
    if (demoWant.Length > 0)
    {
        var (demoName, demoF) = CompareBuilds().FirstOrDefault(b => b.Name.Contains(demoWant));
        if (demoF is null) { Console.Error.WriteLine($"編成が見つからない: {demoWant}"); return; }
        BattleResult picked = BattleEngine.Run(demoF, stage.Enemy, demoSeed, verbose: true);
        Console.WriteLine($"# {demoName} / 第{stageIndex + 1}波 / seed {demoSeed}");
        foreach (LogLine line in picked.Log) Console.WriteLine(line);
        Console.WriteLine($"結果: {(picked.PlayerWon ? "勝利" : "敗北")} / {picked.Turns}ターン");
        return;
    }

    var build = Formation.Build(
        front1: UnitCatalog.Kado,   // 反撃。範囲で返す
        front3: UnitCatalog.Hisa,   // 標的を付けてカドに殴らせる
        center: UnitCatalog.Gald,   // 壁。中央は前列が割れるまで単体攻撃が届かない席
        back1:  UnitCatalog.Hagi,   // 追い打ち。誰かが倒すと割り込む
        back3:  UnitCatalog.Gan     // 号令。動かないカドの攻撃を積む
    );
    BattleResult demo = BattleEngine.Run(build, stage.Enemy, demoSeed, verbose: true);
    foreach (LogLine line in demo.Log) Console.WriteLine(line);
    Console.WriteLine($"結果: {(demo.PlayerWon ? "勝利" : "敗北")} / {demo.Turns}ターン");
    return;
}
}
