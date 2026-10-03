using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FS = FireScaleDiag;

// burnhit pick —— 段1。
//   T3  : ボルグ・ホタ・ヒヨ ＋ 相方2枠（1,081 組 × 席 120 × seed 1000..1039）。器具は第246期の `FC.RunPick` そのまま（版はボルグの札）。
//   混ぜ: ボルグ・ヒヨ ＋ 3枠（候補は 52 枚からボルグ・ヒヨ・ガルド・ツギを除いた 48 枚 ＝ 17,296 組 × 席 120）。
//         **総当たりを2段で**: 一次は 400/300 × seed 1000..1009（10 本）で組ごとの最良の席を出し、上位 300 組だけを
//         T3 と同じ並び（200/200 × 40 本 → 400/300 × 40 本 → 落 → 決着T）で選び直す。一次の 10 本と 400/300 は所要の都合（報告に書く）。
static partial class BurnHitDiag
{
    static string Dir => _args is { Length: > 3 } ? _args[3] : Path.GetTempPath();
    static string PickPath(string kind, string ver) => Path.Combine(Dir, $"burnhit255_pick_{(kind == "T3" ? "T3" : "mix")}_{ver}.tsv");
    internal static readonly string[] PickVersions = { "H0", "H-分担" };
    internal const int MixFirstSeeds = 10, MixKeep = 300;

    internal static List<FB.Pick> PicksOf(string kind, HVer v, out double sec)
    {
        string path = PickPath(kind, v.Name);
        if (File.Exists(path)) return FC.LoadPick(path, out sec);
        var ps = kind == "T3" ? FC.RunPick(PickVer(v), out sec) : RunMix(v, out sec);
        FC.SavePick(ps, path, sec);
        return ps;
    }

    static readonly HashSet<string> MixExcluded = new() { "borg", "hiyo", "gald", "tsugi" };
    internal static List<UnitDef> MixCandidates => UnitCatalog.All.Where(u => !MixExcluded.Contains(u.Id)).ToList();

    static List<FB.Pick> RunMix(HVer v, out double seconds)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pv = PickVer(v);
        var core = new[] { pv.Borg, pv.Hiyo };
        var cands = MixCandidates;
        var jobs = new List<UnitDef[]>();
        for (int i = 0; i < cands.Count; i++) for (int j = i + 1; j < cands.Count; j++) for (int k = j + 1; k < cands.Count; k++)
            jobs.Add(new[] { cands[i], cands[j], cands[k] });
        // 一次: 組ごとに 120 席 × 400/300 × 10 本。最良の席の (全員生存, −落, −決着T)。
        var first = new (long Key, string Enc)[jobs.Count];
        int done = 0;
        Parallel.For(0, jobs.Count, n =>
        {
            var perms = SeroDiag.Permute(core.Concat(jobs[n]).ToArray()).Select(BA.Seat).ToArray();
            long bestK = long.MinValue; string bestE = "";
            foreach (var f in perms)
            {
                var s = FS.ScoreOf(f, BA.PickSeed0, MixFirstSeeds, BA.Scales[1].Sc);
                long key = (long)s.Sv * 1_000_000_000L - (long)s.Fell * 1_000_000L + s.W * 1000L - s.T;
                if (key > bestK) { bestK = key; bestE = FB.Enc(f); }
            }
            first[n] = (bestK, bestE);
            int d = Interlocked.Increment(ref done);
            if (d % 2000 == 0) Console.Error.WriteLine($"  混ぜ 一次 {v.Name} {d}/{jobs.Count}（{sw.Elapsed.TotalMinutes:F1} 分）");
        });
        var keep = Enumerable.Range(0, jobs.Count).OrderByDescending(n => first[n].Key).ThenBy(n => n).Take(MixKeep).ToList();
        // 二次: T3 と同じ並び。
        var res = new List<FB.Pick>();
        foreach (int n in keep)
        {
            var perms = SeroDiag.Permute(core.Concat(jobs[n]).ToArray()).Select(BA.Seat).ToArray();
            var s2 = new BA.SeatScore[perms.Length];
            Parallel.For(0, perms.Length, i => s2[i] = FS.ScoreOf(perms[i], BA.PickSeed0, BA.PickSeeds, BA.Scales[0].Sc));
            int max = s2.Max(x => x.Sv);
            var tied = Enumerable.Range(0, perms.Length).Where(i => s2[i].Sv == max).ToArray();
            var sv4 = new int[tied.Length];
            Parallel.For(0, tied.Length, t => sv4[t] = FS.ScoreOf(perms[tied[t]], BA.PickSeed0, BA.PickSeeds, BA.Scales[1].Sc).Sv);
            FB.Score4 best = default; string bestE = ""; long bk = long.MinValue;
            for (int t = 0; t < tied.Length; t++)
            {
                var sc = new FB.Score4(s2[tied[t]], sv4[t]);
                if (sc.Key > bk) { bk = sc.Key; best = sc; bestE = FB.Enc(perms[tied[t]]); }
            }
            res.Add(new FB.Pick(v.Name, jobs[n].Select(p => p.Id).ToArray(), bestE, best));
        }
        seconds = sw.Elapsed.TotalSeconds;
        return res;
    }

    static partial void PickImpl()
    {
        string? only = _args is { Length: > 4 } ? _args[4] : null;   // "T3" ／ "混ぜ" ／ "T3:H0" など
        foreach (string kind in new[] { "T3", "混ぜ" })
            foreach (string vn in PickVersions)
            {
                string key = PickKey(kind, vn);
                if (only is not null && only != kind && only != key) continue;
                string path = PickPath(kind, vn);
                if (File.Exists(path)) { Console.WriteLine($"{key}: {path} は既にある（消せば回し直す）"); continue; }
                var ps = PicksOf(kind, VerOf(vn), out double sec);
                Console.WriteLine($"{key}: 段1 を {path} に書いた（{ps.Count} 組・{sec / 60:F1} 分）");
            }
    }

    internal static Dictionary<string, List<FB.Pick>> Ranked()
    {
        var d = new Dictionary<string, List<FB.Pick>>();
        foreach (string kind in new[] { "T3", "混ぜ" })
            foreach (string vn in PickVersions)
                d[PickKey(kind, vn)] = FB.Ranked(PicksOf(kind, VerOf(vn), out _));
        return d;
    }
}
