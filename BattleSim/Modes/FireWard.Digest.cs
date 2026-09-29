using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BF = BorgFrontDiag;

// fireward digest —— 受け入れ 1 の台本の指紋。**engine を触る前に取り、触った後と突き合わせる**（G0 と A ＝ 第235期「全部」）。
// 台 4 × 版 2 × 波 6 × 倍率 3 × seed 0..9（1,440 戦・verbose）の Log と台本を1行ずつ吐く（第235期の `borgfront digest` と同じ形）。
static partial class FireWardDiag
{
    static void Digest()
    {
        string outPath = _args is { Length: > 3 } ? _args[3] : "fireward_digest.txt";
        var lines = new List<string>();
        foreach (var (vn, borg) in new[] { ("G0", UnitCatalog.BorgF0), ("A", BF.VerBorg("全部")) })
        {
            var boards = BF.DigestBoards(borg);
            for (int bi = 0; bi < boards.Length; bi++)
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < BA.Scales.Length; s++)
                        for (int seed = 0; seed < 10; seed++)
                        {
                            var (r, _, _, _) = BA.Fight(boards[bi], w, BA.Scales[s].Sc, seed);
                            lines.Add($"## {vn} 台{bi} 波{w} 倍率{s} seed{seed} won={r.PlayerWon} T={r.Turns}");
                            foreach (var l in r.Log) lines.Add(l.Text);
                            foreach (var ev in r.Events)
                                lines.Add($"E {ev.Kind} {ev.Turn} {ev.ActorId} {ev.TargetId} {ev.Amount} {ev.HpAfter} {ev.Text}");
                        }
        }
        File.WriteAllLines(outPath, lines);
        Console.WriteLine($"digest: {lines.Count} 行を {outPath} に書いた");
    }
}
