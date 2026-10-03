using BattleCore;
using static Common;

// fireatk digest —— 台本の指紋（受け入れ 2: L0 ＝ 前段の規定と盤面・台本が、第250期の札の実装の前後で一致すること）。
// **このファイルだけで閉じている**（第250期の札も `FireAtkDiag` も引かない・駒は規定そのもの）ので、前段だけを入れたコードにも置ける。
// 第251期: L3 が規定になったので、駒を第250期までの規定（`BorgK4` / `HotaK4` / `Hiyo`）に固定した（第251期より前のコミットでは `K4` を外して回す）。
// 台・波は `firefinish digest` と同じ（第245期の6波 ＋ 重い波 ＋ 的・一 ／ 的・九）。
static class FireAtkDigestDiag
{
    public static void Run(string[] args)
    {
        string outPath = args.Length > 3 ? args[3] : "fireatk_digest.txt";
        var boards = new (string Name, Formation F)[]
        {
            ("T3-244", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Hisa, center: UnitCatalog.BorgK4, back1: UnitCatalog.HotaK4, back3: UnitCatalog.HiyoM0)),
            ("T3-238", Formation.Build(front1: UnitCatalog.HiyoM0, front3: UnitCatalog.HotaK4, center: UnitCatalog.BorgK4, back1: UnitCatalog.Doha, back3: UnitCatalog.Sora)),
            ("雷＋ボルグ", Formation.Build(front1: UnitCatalog.BorgK4, front3: UnitCatalog.Tsugi, center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Mio)),
        };
        var target = new UnitDef { Id = "mato", Name = "的", MaxHp = 9999, Attack = 1, Speed = 5, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single };
        var heavy = Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Warden, center: EnemyCatalog.Warden);
        var scales = new[] { new EnemyScaleRule(200, 200), new EnemyScaleRule(400, 300), new EnemyScaleRule(115, 115) };
        Func<List<UnitState>> Wave(int w, EnemyScaleRule sc) => w switch
        {
            < 4 => () => BattleEngine.Materialize(EnemyCatalog.Stages[w + 1].Enemy, BattleContext.EnemyTeam, sc),
            < 6 => () => BattleEngine.MaterializeEnemy(EnemyCatalog.TestStages[w - 4].Enemy, sc),
            6 => () => BattleEngine.Materialize(heavy, BattleContext.EnemyTeam, sc),
            7 => () => BattleEngine.MaterializeEnemy(EnemyWave.Of((2, target)), EnemyScaleRule.None),
            _ => () => BattleEngine.MaterializeEnemy(EnemyWave.FillAll(target), EnemyScaleRule.None),
        };
        var lines = new List<string>();
        foreach (var (bn, f) in boards)
            for (int w = 0; w < 9; w++)
                for (int s = 0; s < (w >= 7 ? 1 : scales.Length); s++)
                    for (int seed = 0; seed < 6; seed++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), Wave(w, scales[s])(), seed, verbose: true, ember: EmberRule.Pre256);
                        lines.Add($"## {bn} 波{w} 倍率{s} seed{seed} won={r.PlayerWon} T={r.Turns}");
                        foreach (var l in r.Log) lines.Add(l.Text);
                        foreach (var ev in r.Events)
                            lines.Add($"E {ev.Kind} {ev.Turn} {ev.ActorId} {ev.TargetId} {ev.Amount} {ev.HpAfter} {ev.Slot} {ev.Text} {ev.TickIndex} {ev.TickCount} {ev.BrittleExtra}");
                    }
        File.WriteAllLines(outPath, lines);
        Console.WriteLine($"digest: {lines.Count} 行を {outPath} に書いた（台 {boards.Length} × 波 9 × 倍率 × seed 6）");
    }
}
