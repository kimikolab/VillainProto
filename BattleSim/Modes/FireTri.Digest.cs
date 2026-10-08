using BattleCore;
using static Common;

// firetri digest —— 台本の指紋（受け入れ 2: T0 ＝ 前段の規定と盤面・台本が、第247期の札の実装の前後で一致すること）。
// **このファイルだけで閉じている**（第247期の札も `FireTriDiag` も引かない・駒は規定そのもの）ので、実装の前のコミット（＋前段の規定化だけ）に置いても同じものが回る。
// 波は第245期の6波 ＋ 重い波（城塞の重装兵 ×3）＋ 的・一 ／ 的・九（追記 A）。
static class FireTriDigestDiag
{
    public static void Run(string[] args)
    {
        // 第249期 前段: ホタは第247期 前段の規定（第246期の規定 ＋ 大火槍・臨界）に固定した（規定が動いても台本が動かない）。
        var hota = new UnitDef
        {
            Id = UnitCatalog.HotaQ0.Id, Name = UnitCatalog.HotaQ0.Name, MaxHp = UnitCatalog.HotaQ0.MaxHp, Attack = UnitCatalog.HotaQ0.Attack, Speed = UnitCatalog.HotaQ0.Speed,
            Traits = UnitCatalog.HotaQ0.Traits.Concat(new[] { TraitId.PyreLance, TraitId.PyreCritical }).ToArray(), Pattern = UnitCatalog.HotaQ0.Pattern,
            Advances = UnitCatalog.HotaQ0.Advances, Actions = UnitCatalog.HotaQ0.Actions, PlusText = UnitCatalog.HotaQ0.PlusText, MinusText = UnitCatalog.HotaQ0.MinusText, Flavor = UnitCatalog.HotaQ0.Flavor,
        };
        string outPath = args.Length > 3 ? args[3] : "firetri_digest.txt";
        var boards = new (string Name, Formation F)[]
        {
            ("T3-244", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.HisaHK0, center: UnitCatalog.BorgU0, back1: hota, back3: UnitCatalog.HiyoU0)),
            ("T3-238", Formation.Build(front1: UnitCatalog.HiyoU0, front3: hota, center: UnitCatalog.BorgU0, back1: UnitCatalog.Doha, back3: UnitCatalog.SoraSR0)),
            ("雷＋ボルグ", Formation.Build(front1: UnitCatalog.BorgU0, front3: UnitCatalog.Tsugi, center: UnitCatalog.Beni, back1: UnitCatalog.KataS3, back3: UnitCatalog.Mio)),
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
