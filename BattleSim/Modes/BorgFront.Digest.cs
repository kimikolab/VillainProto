using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// borgfront digest —— 受け入れ 1 の台本の指紋。**engine を触る前に取り、触った後と突き合わせる**（G0 と B ＝ 第234期 G3）。
// 台 4 × 版 2 × 波 6 × 倍率 3 × seed 0..9（1,440 戦・verbose）の Log と台本（種類・ターン・書き手・対象・量・文）を1行ずつ吐く。
static partial class BorgFrontDiag
{
    internal static Formation[] DigestBoards(UnitDef borg) => new[]
    {
        BA.Seat(new[] { borg, UnitCatalog.HotaL0, UnitCatalog.HiyoF0, UnitCatalog.Shio, UnitCatalog.Sasa }),
        BA.Seat(new[] { UnitCatalog.HotaL0, UnitCatalog.HiyoF0, UnitCatalog.Shio, UnitCatalog.Sasa, borg }),
        BA.Seat(new[] { UnitCatalog.Golm, borg, UnitCatalog.HotaL0, UnitCatalog.HiyoF0, UnitCatalog.Beni }),
        BA.Seat(new[] { borg, UnitCatalog.Tsugi, UnitCatalog.Beni, UnitCatalog.KataS3, UnitCatalog.Mio }),
    };

    static void Digest()
    {
        string outPath = _args is { Length: > 3 } ? _args[3] : "borgfront_digest.txt";
        var lines = new List<string>();
        foreach (var (vn, borg) in new[] { ("G0", UnitCatalog.BorgF0), ("B", BorgGuardDiag.VerBorg("G3")) })
        {
            var boards = DigestBoards(borg);
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
