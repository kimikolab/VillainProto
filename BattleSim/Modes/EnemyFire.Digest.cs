using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// enemyfire digest —— 台本の指紋（受け入れ 2: E0 ＝ 前段の規定と盤面・台本が、敵の火勢の実装の前後で一致すること）。
// 規定の駒だけで組む（版の札を1枚も引かない）ので、実装の前のコミットにこのファイルを置いても同じものが回る。
static partial class EnemyFireDiag
{
    static Formation E0(Formation f) => Apply(f, UnitCatalog.BorgE0, UnitCatalog.HotaQ0, UnitCatalog.HiyoU0);
    static partial void Digest()
    {
        string outPath = _args is { Length: > 3 } ? _args[3] : "enemyfire_digest.txt";
        var boards = new (string Name, Formation F)[]
        {
            // 第249期 前段: 台はすべて E0（第245期 前段の規定 ＝ `UnitCatalog.BorgE0` / `HotaQ0` / `HiyoU0`）に固定した（規定が動いても台本が動かない）。
            ("T3-244", E0(T3244)), ("T3-238", E0(T3238)), ("雷＋ボルグ", E0(ThunderBorg)), ("燃焼（compare）", E0(BA.RefBurn)),
        };
        var lines = new List<string>();
        foreach (var (bn, f) in boards)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    for (int seed = 0; seed < 10; seed++)
                    {
                        var (r, _, _) = FB.Fight(f, w, BA.Scales[s].Sc, seed);
                        lines.Add($"## {bn} 波{w} 倍率{s} seed{seed} won={r.PlayerWon} T={r.Turns}");
                        foreach (var l in r.Log) lines.Add(l.Text);
                        foreach (var ev in r.Events)
                            lines.Add($"E {ev.Kind} {ev.Turn} {ev.ActorId} {ev.TargetId} {ev.Amount} {ev.HpAfter} {ev.Slot} {ev.Text} {ev.TickIndex} {ev.TickCount} {ev.BrittleExtra}");
                    }
        File.WriteAllLines(outPath, lines);
        Console.WriteLine($"digest: {lines.Count} 行を {outPath} に書いた（台 {boards.Length} × 波 {BA.WaveNames.Length} × 倍率 {BA.Scales.Length} × seed 10）");
    }
}
