using System.Globalization;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BG = BorgGuardDiag;

// fireward pick —— 段1（版ごとに T3 1,081 組 × 席 120 × seed 40）。
// **並び（指示書 §7.1）: 200/200 の全員生存 → 400/300 の全員生存 → 落ちた駒 → 決着T → 列挙順。**
// 400/300 は「その組の中で 200/200 の全員生存が最大の席」にだけ回す（200/200 が先に決めるので、ほかの席では順位に効かない）。
static partial class FireWardDiag
{
    /// <summary>段1 の得点: 200/200 の <see cref="BA.SeatScore"/> ＋ 400/300 の全員生存（回していない席は −1）。</summary>
    internal readonly record struct Score4(BA.SeatScore S, int Sv4)
    {
        public long Key => (long)S.Sv * 1_000_000_000_000L + (long)Math.Max(0, Sv4) * 1_000_000_000L + (S.Key - (long)S.Sv * 1_000_000_000L);
    }
    internal sealed record Pick(string Ver, string[] Partners, string Best, Score4 BestS, string Front, Score4 FrontS, bool BestIsFront);

    static string Dir => _args is { Length: > 3 } ? _args[3] : Path.GetTempPath();
    static string PickPath(string key) => Path.Combine(Dir, $"fireward238_pick_T3_{key}.tsv");

    static readonly HashSet<string> PrimeIds = new() { "shio", "sasa" };
    static bool IsT3P(Pick p) => !p.Partners.Any(PrimeIds.Contains);
    static bool IsT31(Pick p) => p.Partners.All(PrimeIds.Contains);

    /// <summary>その席の集合のうち 200/200 の全員生存が最大の席に 400/300 を回し、最良を返す（同値は並びの若い方）。</summary>
    static (Formation F, Score4 S) Best4(IReadOnlyList<(Formation F, BA.SeatScore S)> xs)
    {
        int max = xs.Max(x => x.S.Sv);
        var tied = xs.Select((x, i) => (x, i)).Where(z => z.x.S.Sv == max).ToArray();
        var sv4 = new int[tied.Length];
        Parallel.For(0, tied.Length, k => sv4[k] = BA.ScoreOf(tied[k].x.F, BA.PickSeed0, BA.PickSeeds, BA.MainWave, BA.Scales[1].Sc).Sv);
        (Formation F, Score4 S) best = default; long bk = long.MinValue; bool found = false;
        for (int k = 0; k < tied.Length; k++)
        {
            var s = new Score4(tied[k].x.S, sv4[k]);
            if (!found || s.Key > bk) { best = (tied[k].x.F, s); bk = s.Key; found = true; }
        }
        return best;
    }

    static List<Pick> RunPick(Ver v, List<UnitDef> cands, out double seconds)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var core = CoreOf(v);
        var jobs = new List<UnitDef[]>();
        for (int i = 0; i < cands.Count; i++) for (int j = i + 1; j < cands.Count; j++) jobs.Add(new[] { cands[i], cands[j] });
        var res = new Pick[jobs.Count];
        for (int n = 0; n < jobs.Count; n++)
        {
            var seats = BA.AllSeats(core.Concat(jobs[n]).ToArray(), BA.PickSeeds);
            var best = Best4(seats);
            var front = Best4(seats.Where(x => BG.BorgFront(x.F)).ToList());
            res[n] = new Pick(v.Name, jobs[n].Select(p => p.Id).ToArray(), Enc(best.F), best.S, Enc(front.F), front.S, BG.BorgFront(best.F));
            if ((n + 1) % 200 == 0) Console.Error.WriteLine($"  段1 {v.Name} {n + 1}/{jobs.Count}（{sw.Elapsed.TotalMinutes:F1} 分）");
        }
        seconds = sw.Elapsed.TotalSeconds;
        return res.ToList();
    }

    static string S4(Score4 s) => string.Join("\t", s.S.Sv, s.Sv4, s.S.Fell, s.S.W, s.S.T);
    static Score4 P4(string[] c, int i) => new(new BA.SeatScore(int.Parse(c[i]), int.Parse(c[i + 2]), int.Parse(c[i + 3]), long.Parse(c[i + 4])), int.Parse(c[i + 1]));

    static void SavePick(List<Pick> ps, string path, double seconds)
    {
        using var w = new StreamWriter(path);
        w.WriteLine($"# seconds\t{seconds.ToString("F1", CultureInfo.InvariantCulture)}");
        foreach (var p in ps)
            w.WriteLine(string.Join("\t", p.Ver, string.Join(",", p.Partners), p.Best, S4(p.BestS), p.Front, S4(p.FrontS), p.BestIsFront ? 1 : 0));
    }

    static List<Pick> LoadPick(string path, out double seconds)
    {
        seconds = double.NaN;
        var res = new List<Pick>();
        foreach (var line in File.ReadAllLines(path))
        {
            var c = line.Split('\t');
            if (c[0] == "# seconds") { seconds = double.Parse(c[1], CultureInfo.InvariantCulture); continue; }
            res.Add(new Pick(c[0], c[1].Split(','), c[2], P4(c, 3), c[8], P4(c, 9), c[14] == "1"));
        }
        return res;
    }

    static List<Pick> PicksOf(Ver v, out double sec)
    {
        string path = PickPath(v.Key);
        if (File.Exists(path)) return LoadPick(path, out sec);
        var ps = RunPick(v, BA.Candidates, out sec);
        SavePick(ps, path, sec);
        return ps;
    }

    static partial void PickImpl()
    {
        foreach (var v in Versions)
        {
            string path = PickPath(v.Key);
            if (File.Exists(path)) { Console.WriteLine($"{v.Name}: {path} は既にある（消せば回し直す）"); continue; }
            var ps = PicksOf(v, out double sec);
            Console.WriteLine($"{v.Name}: 段1 を {path} に書いた（{ps.Count} 組・{sec / 60:F1} 分）");
        }
    }

    static List<Pick> Ranked(IEnumerable<Pick> ps) => ps.Select((p, i) => (p, i))
        .OrderByDescending(z => z.p.BestS.Key).ThenBy(z => z.i).Select(z => z.p).ToList();
}
