using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// fireburst digest —— 台本の指紋（受け入れ 2: S0 ＝ 規定化後の盤面と台本が、大技の実装の前後で一致すること）。
// 規定の駒だけで組む（版の札を1枚も引かない）ので、実装の前のコミットにこのファイルを置いても同じものが回る。
static partial class FireBurstDiag
{
    static void Digest()
    {
        string outPath = _args is { Length: > 3 } ? _args[3] : "fireburst_digest.txt";
        var boards = new (string Name, Formation F)[]
        {
            ("T3-238", T3238), ("T3（242 R3 の1位）", T3r3), ("雷＋ボルグ", ThunderBorg), ("燃焼（compare）", BA.RefBurn),
        };
        var lines = new List<string>();
        foreach (var (bn, f) in boards)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    for (int seed = 0; seed < 10; seed++)
                    {
                        var (r, _, _) = Fight(f, w, BA.Scales[s].Sc, seed);
                        lines.Add($"## {bn} 波{w} 倍率{s} seed{seed} won={r.PlayerWon} T={r.Turns}");
                        foreach (var l in r.Log) lines.Add(l.Text);
                        foreach (var ev in r.Events)
                            lines.Add($"E {ev.Kind} {ev.Turn} {ev.ActorId} {ev.TargetId} {ev.Amount} {ev.HpAfter} {ev.Slot} {ev.Text}");
                    }
        File.WriteAllLines(outPath, lines);
        Console.WriteLine($"digest: {lines.Count} 行を {outPath} に書いた（台 {boards.Length} × 波 {BA.WaveNames.Length} × 倍率 {BA.Scales.Length} × seed 10）");
    }
}
