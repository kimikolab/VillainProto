using BattleCore;
using static Common;

// spring2 run243 / check243 —— 第243期「ハネ・バサの5つの版」。S2 ・①（同じ列）・②（着地の反動）・③（弾き返しの打撃）・④（半分の混乱・バサ）・
// ⑤（動かした敵の萎縮）の単独と、①②③④・①②③④⑤ を第232期と同じ台（M-ハネ 228）で並べる。**線は置かない**（規定はポンが決める）。
static partial class Spring2Diag
{
    /// <summary>版。ハネの札と、④ならバサに `ConfuseHalf` を足す。</summary>
    sealed record V243(string Tag, UnitDef Hane, bool Half);

    static readonly UnitDef BasaHalf = Plus(UnitCatalog.Basa, TraitId.ConfuseHalf);
    static UnitDef HaneS2Plus(params TraitId[] x) => Plus(HaneS2, x);

    static V243[] Vs243 => _vs243 ??= new[]
    {
        new V243("S2", HaneS2, false),
        new V243("①", HaneS3, false),
        new V243("②", HaneL, false),
        new V243("③", HaneS2Plus(TraitId.SpringStrike), false),
        new V243("④", HaneS2, true),
        new V243("⑤", HaneS2Plus(TraitId.SpringDaunt), false),
        new V243("①②③④", HaneS2Plus(TraitId.SpringRow, TraitId.Landing, TraitId.SpringStrike), true),
        new V243("①②③④⑤", HaneS2Plus(TraitId.SpringRow, TraitId.Landing, TraitId.SpringStrike, TraitId.SpringDaunt), true),
    };
    static V243[]? _vs243;

    static Formation Form243(V243 v)
    {
        var f = MHane228;
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied())
            g[slot] = d.Id == "hane" ? v.Hane : d.Id == "basa" && v.Half ? BasaHalf : d;
        return g;
    }

    sealed class Agg243
    {
        public readonly Agg A = new();
        public long HaneDealt, HaneAttacks, HaneBlasts, StrikeHits, StrikeDealt, StrikeKills, Daunts;
        public long SeroEvades, FoeFriendly, DauntCut, DauntSwings, HalfSelf, HalfNormal, ConfusedSwings;

        public void Merge(Agg243 o)
        {
            A.Merge(o.A);
            HaneDealt += o.HaneDealt; HaneAttacks += o.HaneAttacks; HaneBlasts += o.HaneBlasts; StrikeHits += o.StrikeHits; StrikeDealt += o.StrikeDealt; StrikeKills += o.StrikeKills; Daunts += o.Daunts;
            SeroEvades += o.SeroEvades; FoeFriendly += o.FoeFriendly; DauntCut += o.DauntCut; DauntSwings += o.DauntSwings; HalfSelf += o.HalfSelf; HalfNormal += o.HalfNormal; ConfusedSwings += o.ConfusedSwings;
        }
        public double Per(long x) => A.Per(x);

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            A.Take(r, p, e, slot0);
            var pIds = p.Select(u => u.Def.Id).ToHashSet();
            foreach (var (id, t) in r.TallyByUnit)
            {
                if (pIds.Contains(id))
                {
                    if (id == "hane")
                    {
                        HaneDealt += t.DamageToEnemy; HaneAttacks += t.Attacks; HaneBlasts += t.BlastCount;
                        StrikeHits += t.SpringStrikeHits; StrikeDealt += t.SpringStrikeDealt; StrikeKills += t.SpringStrikeKills; Daunts += t.SpringDaunts;
                    }
                    if (id == "sero") SeroEvades += t.Evades;
                    continue;
                }
                FoeFriendly += t.DamageToAlly; DauntCut += t.DauntedCut; DauntSwings += t.DauntedSwings;
                HalfSelf += t.ConfuseHalfSelf; HalfNormal += t.ConfuseHalfNormal; ConfusedSwings += t.ConfusedSwings;
            }
        }
    }

    static Agg243 Measure243(V243 v, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = Seeds)
    {
        var g = Form243(v);
        var parts = new Agg243[seeds];
        Parallel.For(0, seeds, i =>
        {
            var a = new Agg243();
            var (r, p, e, slot0) = Fight(g, w, sc, seed0 + i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new Agg243();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    const string Head243 = "| 版 | 全員生存 | 勝率 | ハネの与ダメ | ハネの攻撃回数 | 吹っ飛ばし | 弾き返し（うち隣） | 打撃（③）/与 | セロの回避 | 敵の同士討ち | 萎縮で軽くなった量（回） | 味方の移動 | シオの退避 | 混乱の振り 自軍/普段（④） |";
    const string Sep243 = "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|";

    static string Row243(string tag, Agg243 a)
    {
        var t = a.A.T;
        return $"| {tag} | **{F1(t.Surv)}** | {F1(t.Win)} | {F1(a.Per(a.HaneDealt))} | {F2(a.Per(a.HaneAttacks))} | {F2(a.Per(a.HaneBlasts))} | "
             + $"{F2(a.Per(t.SpringSelf + t.SpringGuard))}（{F2(a.Per(t.SpringGuard))}） | {F2(a.Per(a.StrikeHits))}/{F1(a.Per(a.StrikeDealt))} | {F2(a.Per(a.SeroEvades))} | "
             + $"{F1(a.Per(a.FoeFriendly))} | {F1(a.Per(a.DauntCut))}（{F2(a.Per(a.DauntSwings))}） | {F2(a.Per(a.A.AllyMoves))} | {F2(a.Per(t.RetreatSwaps))} | "
             + $"{F2(a.Per(a.HalfSelf))}/{F2(a.Per(a.HalfNormal))} |";
    }

    static partial void Run243()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第243期 ハネ・バサの5つの版（台 M-ハネ（228 H3））");
        Console.WriteLine();
        Console.WriteLine($"席: {SeatsNamed(MHane228)}（ハネは版ごとに差し替え・④ はバサに `ConfuseHalf`）");
        Console.WriteLine();
        Console.WriteLine("値はどれも1戦あたり。ハネの攻撃回数 ＝ `PerformAttack` を通った回数（吹っ飛ばしの貫きを含む）。敵の同士討ち ＝ 敵が敵に与えた量（混乱で自軍へ振った分）。"
            + "萎縮で軽くなった量 ＝ 敵の一撃から削った打点（括弧は萎縮で振った一撃の数）。味方の移動 ＝ 味方が動かされた `Move` の件数。");
        Console.WriteLine();

        foreach (int s in new[] { 0, 1 })
        {
            Console.WriteLine($"## {Scales[s].Name} × seed 0..{Seeds - 1} —— 波ごとの全員生存 ／ 勝率");
            Console.WriteLine();
            Console.WriteLine("| 波 | " + string.Join(" | ", Vs243.Select(v => v.Tag)) + " |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", Vs243.Length)));
            var main = Vs243.Select(_ => new Agg243()).ToArray();
            var mainSurv = Vs243.Select(_ => new List<double>()).ToArray();
            for (int w = 0; w < WaveNames.Length; w++)
            {
                var cells = Vs243.Select(v => Measure243(v, w, Scales[s].Sc)).ToList();
                if (w < 4) for (int k = 0; k < cells.Count; k++) { main[k].Merge(cells[k]); mainSurv[k].Add(cells[k].A.T.Surv); }
                Console.WriteLine($"| {WaveNames[w]} | " + string.Join(" | ", cells.Select(a => $"**{F1(a.A.T.Surv)}** ／ {F1(a.A.T.Win)}")) + " |");
            }
            Console.WriteLine("| 本編の平均（全員生存） | " + string.Join(" | ", mainSurv.Select(m => F1(m.Average()))) + " |");
            Console.WriteLine();
            Console.WriteLine($"### {Scales[s].Name} —— 本編の第2〜5波をまとめた内訳（{4 * Seeds} 戦）");
            Console.WriteLine();
            Console.WriteLine(Head243);
            Console.WriteLine(Sep243);
            for (int k = 0; k < Vs243.Length; k++) Console.WriteLine(Row243(Vs243[k].Tag, main[k]));
            Console.WriteLine();
        }

        foreach (int s in new[] { 1, 0 })
        {
            Console.WriteLine($"## 九/新兵 × {Scales[s].Name} × seed 0..999");
            Console.WriteLine();
            Console.WriteLine(Head243);
            Console.WriteLine(Sep243);
            var fell = new List<string>();
            foreach (var v in Vs243)
            {
                var a = Measure243(v, TuneDiag.MainWave, Scales[s].Sc, 0, 1000);
                Console.WriteLine(Row243(v.Tag, a));
                fell.Add($"| {v.Tag} | {F2(a.Per(a.A.T.Fell.Values.Sum()))} | " + string.Join(" | ", Units.Select(id => F1(100.0 * a.A.T.Fell.GetValueOrDefault(id) / a.A.T.N) + "%")) + " |");
            }
            Console.WriteLine();
            Console.WriteLine("| 版 | 倒れた/戦 | " + string.Join(" | ", UnitNames) + " |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Units.Length)));
            foreach (var l in fell) Console.WriteLine(l);
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    /// <summary>第243期の追記: 採用前の規定のハネ（①②込み・`HaneR2`）と、それに ③⑤ を足した版（＝採用後の規定）。</summary>
    static V243[] VsAdopt => _vsAdopt ??= new[]
    {
        new V243("規定", UnitCatalog.HaneR2, false),
        new V243("規定＋③⑤", Plus(UnitCatalog.HaneR2, TraitId.SpringStrike, TraitId.SpringDaunt), false),
    };
    static V243[]? _vsAdopt;

    static partial void RunAdopt()
    {
        Console.WriteLine("# 第243期 追記: 今の規定のハネ 対 規定＋③⑤（台 M-ハネ（228 H3））");
        Console.WriteLine();
        foreach (int s in new[] { 0, 1 })
        {
            Console.WriteLine($"## {Scales[s].Name} × seed 0..{Seeds - 1} —— 全員生存 ／ 勝率");
            Console.WriteLine();
            Console.WriteLine("| 波 | " + string.Join(" | ", VsAdopt.Select(v => v.Tag)) + " | 差 |");
            Console.WriteLine("|---|---|---|---|");
            var main = VsAdopt.Select(_ => new Agg243()).ToArray();
            var surv = VsAdopt.Select(_ => new List<double>()).ToArray();
            for (int w = 0; w < WaveNames.Length; w++)
            {
                var cells = VsAdopt.Select(v => Measure243(v, w, Scales[s].Sc)).ToList();
                if (w < 4) for (int k = 0; k < cells.Count; k++) { main[k].Merge(cells[k]); surv[k].Add(cells[k].A.T.Surv); }
                Console.WriteLine($"| {WaveNames[w]} | " + string.Join(" | ", cells.Select(a => $"**{F1(a.A.T.Surv)}** ／ {F1(a.A.T.Win)}")) + $" | {cells[1].A.T.Surv - cells[0].A.T.Surv:+0.0;-0.0;±0.0} |");
            }
            Console.WriteLine($"| 本編の平均 | {F1(surv[0].Average())} | {F1(surv[1].Average())} | {surv[1].Average() - surv[0].Average():+0.0;-0.0;±0.0} |");
            Console.WriteLine();
            Console.WriteLine(Head243);
            Console.WriteLine(Sep243);
            for (int k = 0; k < VsAdopt.Length; k++) Console.WriteLine(Row243(VsAdopt[k].Tag, main[k]));
            Console.WriteLine();
        }
        foreach (int s in new[] { 1, 0 })
        {
            Console.WriteLine($"## 九/新兵 × {Scales[s].Name} × seed 0..999");
            Console.WriteLine();
            Console.WriteLine(Head243);
            Console.WriteLine(Sep243);
            foreach (var v in VsAdopt) Console.WriteLine(Row243(v.Tag, Measure243(v, TuneDiag.MainWave, Scales[s].Sc, 0, 1000)));
            Console.WriteLine();
        }
    }

    static partial void Check243()
    {
        ok = ng = 0;
        Console.WriteLine("# 第243期 spring2 check243");
        Console.WriteLine();
        // (a) 札の無い版は今の器具（S2・①・②）と1ビットも違わない——③④⑤の札が無い戦で新しい口が盤面を動かしていない。
        {
            long diff = 0, n = 0;
            foreach (var (tag, h) in new[] { ("S2", HaneS2), ("①", HaneS3), ("②", HaneL) })
                for (int w = 0; w < WaveNames.Length; w++)
                    foreach (var (_, sc) in Scales)
                        for (int sd = 0; sd < 20; sd++)
                        {
                            var f1 = Apply(MHane228, new Ver(tag, h));
                            var f2 = Form243(new V243(tag, h, false));
                            var (r1, _, _, _) = Fight(f1, w, sc, sd, false);
                            var (r2, _, _, _) = Fight(f2, w, sc, sd, false);
                            n++;
                            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) diff++;
                        }
            Expect($"札の無い版で旧の組み立てと勝敗・決着T が違う戦（{n} 戦）", diff, 0L);
        }
        // (b) 版ごとに札が働いた／働かなかった
        long strike0 = 0, strike1 = 0, half0 = 0, half1 = 0, daunt0 = 0, daunt1 = 0, cut1 = 0, verb = 0, nb = 0, swingsGtMarks = 0;
        var lk = new object();
        foreach (var v in Vs243)
            for (int w = 0; w < WaveNames.Length; w++)
                foreach (var (_, sc) in Scales)
                    Parallel.For(0, 20, sd =>
                    {
                        var f = Form243(v);
                        var (r, p, _, _) = Fight(f, w, sc, sd, true);
                        var (r2, _, _, _) = Fight(f, w, sc, sd, false);
                        bool same = r.PlayerWon == r2.PlayerWon && r.Turns == r2.Turns;
                        var pIds = p.Select(u => u.Def.Id).ToHashSet();
                        long st = 0, hs = 0, dn = 0, ct = 0, bad = 0, marks = 0, swings = 0;
                        foreach (var (id, t) in r.TallyByUnit)
                        {
                            st += t.SpringStrikeHits; dn += t.SpringDaunts; marks += t.ConfusedMarks; swings += t.ConfusedSwings;
                            if (!pIds.Contains(id)) { hs += t.ConfuseHalfSelf + t.ConfuseHalfNormal; ct += t.DauntedCut; }
                        }
                        if (swings > marks) bad++;
                        bool hasS = v.Hane.Traits.Contains(TraitId.SpringStrike), hasD = v.Hane.Traits.Contains(TraitId.SpringDaunt);
                        lock (lk)
                        {
                            nb++; if (!same) verb++; swingsGtMarks += bad;
                            if (hasS) strike1 += st; else strike0 += st;
                            if (v.Half) half1 += hs; else half0 += hs;
                            if (hasD) { daunt1 += dn; cut1 += ct; } else daunt0 += dn;
                        }
                    });
        Expect($"verbose の有無で勝敗・決着T が違う戦（{nb} 戦）", verb, 0L);
        Expect("③の札の無い版で打撃が起きた回数", strike0, 0L);
        Expect("③の札のある版で打撃が起きた", strike1 > 0, true);
        Expect("④の札の無い版で半分の混乱の判定が起きた回数", half0, 0L);
        Expect("④の札のある版で半分の混乱の判定が起きた", half1 > 0, true);
        Expect("⑤の札の無い版で萎縮を付けた回数", daunt0, 0L);
        Expect("⑤の札のある版で萎縮を付け、敵の一撃を削った", daunt1 > 0 && cut1 > 0, true);
        Expect("混乱で自軍へ振った回数 > 混乱を付けた回数 になった戦", swingsGtMarks, 0L);
        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**");
    }
}
