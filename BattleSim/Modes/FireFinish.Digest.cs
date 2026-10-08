using BattleCore;
using static Common;

// firefinish digest —— 台本の指紋（受け入れ 2: K0 ＝ 規定と盤面・台本が、第249期の札の実装の前後で一致すること）。
// **このファイルだけで閉じている**（第249期の札も `FireFinishDiag` も引かない・駒は規定そのもの）ので、実装の前のコミットに置いても同じものが回る。
// 第250期 前段: K4 が規定になったので、駒を第249期までの規定（`BorgK0` / `HotaK0` / `HiyoK0`）に固定した（第250期より前のコミットでは `K0` を外して回す）。
// 波は第245期の6波 ＋ 重い波（城塞の重装兵 ×3）＋ 的・一 ／ 的・九。
static class FireFinishDigestDiag
{
    public static void Run(string[] args)
    {
        string outPath = args.Length > 3 ? args[3] : "firefinish_digest.txt";
        var boards = new (string Name, Formation F)[]
        {
            ("T3-244", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Hisa, center: UnitCatalog.BorgK0, back1: UnitCatalog.HotaK0, back3: UnitCatalog.HiyoK0)),
            ("T3-238", Formation.Build(front1: UnitCatalog.HiyoK0, front3: UnitCatalog.HotaK0, center: UnitCatalog.BorgK0, back1: UnitCatalog.Doha, back3: UnitCatalog.SoraSR0)),
            ("雷＋ボルグ", Formation.Build(front1: UnitCatalog.BorgK0, front3: UnitCatalog.Tsugi, center: UnitCatalog.Beni, back1: UnitCatalog.KataS3, back3: UnitCatalog.Mio)),
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
