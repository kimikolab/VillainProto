using BattleCore;
using static Common;

static partial class HaneReachDiag
{
    // 版。札の差し替えだけ（規定のハネ ＋ 保持者 0 枚の札）。
    internal sealed record Ver(string Tag, string Label, UnitDef Hane);
    static UnitDef Plus(UnitDef d, params TraitId[] extra) => DecoyDiag.Copy(d, d.Traits.Concat(extra).ToArray());
    internal static Ver[] Versions => _versions ??= new[]
    {
        new Ver("V0", "採用前の規定", UnitCatalog.HaneR3),
        new Ver("V1", "① 一番前の列", Plus(UnitCatalog.HaneR3, TraitId.BlastReach)),
        new Ver("V2", "② 押せなくても当てる", Plus(UnitCatalog.HaneR3, TraitId.BlastStay)),
        new Ver("V3", "③ 狙えなければ殴る", Plus(UnitCatalog.HaneR3, TraitId.BlastFallback)),
        new Ver("V12", "①＋②", Plus(UnitCatalog.HaneR3, TraitId.BlastReach, TraitId.BlastStay)),
        new Ver("V123", "①＋②＋③", Plus(UnitCatalog.HaneR3, TraitId.BlastReach, TraitId.BlastStay, TraitId.BlastFallback)),
    };
    static Ver[]? _versions;
    internal static Ver VerOf(string tag) => Versions.First(v => v.Tag == tag);

    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied()) g[slot] = d.Id == "hane" ? v.Hane : d;
        return g;
    }

    // 台（編成）。ポンの台 ＝ 画面の編成、M-ハネ228 ＝ 第228期以来の移動の台。
    internal static (string Name, Formation F)[] Boards => new[]
    {
        ("ポン（リリ・ササ・ツギ・ハネ・シオ）", Pon(UnitCatalog.HaneR3)),
        ("M-ハネ228（バサ・セロ・ヨミ・シオ・ハネ）", Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Sero,
            center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.HaneR3)),
    };
    // 波: 本編の第2〜5波（0..3）・九/新兵（4）・九/農兵（5）。
    internal static readonly string[] WaveNames = { "第二波", "第三波", "第四波", "第五波", "九/新兵", "九/農兵" };
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => DriftDiag.WaveOf(w, sc);
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales =
    {
        ("115/115", new EnemyScaleRule(115, 115)),
        ("400/300", new EnemyScaleRule(400, 300)),
        ("1000/300", new EnemyScaleRule(1000, 300)),
    };
    internal const int Seeds = 200;

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, TurnsSum, WinTurnsSum;
        public long HaneHands, HaneIdle;            // 台本から: ハネの手番 ／ 何も起きなかった手番（打撃・移動・転倒・通常攻撃が1件も無い）
        public long NoFront, NoLane, Pinned, Fallbacks, Reached, BlastDealt, HaneDealt;
        public long TsugiHands, TsugiIdle, LiliHands, LiliIdle, PlankReflects, OffTurnPlank;
        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; TurnsSum += o.TurnsSum; WinTurnsSum += o.WinTurnsSum;
            HaneHands += o.HaneHands; HaneIdle += o.HaneIdle;
            NoFront += o.NoFront; NoLane += o.NoLane; Pinned += o.Pinned; Fallbacks += o.Fallbacks; Reached += o.Reached;
            BlastDealt += o.BlastDealt; HaneDealt += o.HaneDealt;
            TsugiHands += o.TsugiHands; TsugiIdle += o.TsugiIdle; LiliHands += o.LiliHands; LiliIdle += o.LiliIdle;
            PlankReflects += o.PlankReflects; OffTurnPlank += o.OffTurnPlank;
        }
        public double Per(long x) => N == 0 ? 0 : (double)x / N;
        public double Pct(long x) => N == 0 ? 0 : 100.0 * x / N;

        public void Take(BattleResult r, List<UnitState> p)
        {
            N++;
            if (r.PlayerWon) { Wins++; WinTurnsSum += r.Turns; }
            if (r.PlayerWon && r.PlayerSurvivors >= p.Count) AllSurv++;
            TurnsSum += r.Turns;
            if (r.TallyByUnit.TryGetValue("hane", out var t))
            {
                NoFront += t.ReboundNoFront; NoLane += t.BlastNoLane; Pinned += t.BlastPinned;
                Fallbacks += t.BlastFallbacks; Reached += t.BlastReached; BlastDealt += t.BlastDealt; HaneDealt += t.DamageToEnemy;
            }
            int hane = p.FirstOrDefault(u => u.Def.Id == "hane")?.InstanceId ?? -1;
            int tsugi = p.FirstOrDefault(u => u.Def.Id == "tsugi")?.InstanceId ?? -1;
            int lili = p.FirstOrDefault(u => u.Def.Id == "lili")?.InstanceId ?? -1;
            var ev = r.Events;
            foreach (var h in r.Hands)
            {
                if (h.Outcome == TurnOutcome.Stalled) continue;
                bool Any(Func<BattleEvent, bool> f) { for (int i = h.EventStart; i < h.EventEnd; i++) if (f(ev[i])) return true; return false; }
                if (h.ActorId == hane)
                {
                    HaneHands++;
                    if (!Any(e => e.ActorId == hane && e.Kind is BattleEventKind.Blast or BattleEventKind.Attack or BattleEventKind.Damage
                                  || e.Kind == BattleEventKind.Stagger && e.ActorId == hane && e.Text == StaggerLabels.Fell)) HaneIdle++;
                }
                else if (h.ActorId == tsugi)
                {
                    TsugiHands++;
                    if (!Any(e => e.Kind == BattleEventKind.Plank && e.ActorId == tsugi && e.Text == PlankLabels.Paste)) TsugiIdle++;
                }
                else if (h.ActorId == lili)
                {
                    LiliHands++;
                    if (!Any(e => e.Kind == BattleEventKind.Kiss && e.ActorId == lili)) LiliIdle++;
                }
            }
            // 板の反射: 手番の外の Damage（Reaction）で、直前が「板の破片が飛んだ」のもの。画面では中身の無い「⚡ 追加攻撃」になる。
            for (int i = 1; i < ev.Count; i++)
                if (ev[i].Kind == BattleEventKind.Damage && ev[i].Reaction)
                {
                    bool plank = ev[i - 1].Kind == BattleEventKind.Plank && ev[i - 1].Text == PlankLabels.Reflect;
                    if (plank) PlankReflects++;
                    if (plank && !(ev[i - 2].Kind == BattleEventKind.Damage && ev[i - 2].Reaction)) OffTurnPlank++;
                }
        }
    }

    internal static Agg Cell(Formation f, int w, EnemyScaleRule sc, int seed0, int seeds, bool verbose = true)
    {
        var cells = new Agg[seeds];
        Parallel.For(0, seeds, s =>
        {
            var a = new Agg();
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var r = BattleEngine.Run(p, WaveOf(w, sc)(), seed0 + s, verbose: verbose);
            a.Take(r, p);
            cells[s] = a;
        });
        var sum = new Agg();
        foreach (var c in cells) sum.Merge(c);
        return sum;
    }

    static string F1(double x) => x.ToString("0.0");
    static string F2(double x) => x.ToString("0.00");

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# hanereach run —— ハネの吹っ飛ばしの空振り（版 V0〜V123）");
        Console.WriteLine();
        Console.WriteLine("空振り ＝ ハネの手番（転倒で潰れた手番を除く）のうち、ハネの吹っ飛ばし・攻撃・打撃・転倒が台本に1件も無かった手番。");
        Console.WriteLine("決着T ＝ 勝った戦の平均ターン（負け戦は入れない）。seed 0..199。");
        Console.WriteLine();
        foreach (var (bname, bf) in Boards)
            foreach (var (sname, sc) in Scales)
            {
                Console.WriteLine($"## {bname} × 敵 {sname}");
                Console.WriteLine();
                Console.WriteLine("| 波 | 版 | 勝率 | 全員生存 | 決着T | 空振り/戦 | 空振り率 | 前列不在 | 経路の外 | 押せず当てた | 通常攻撃 | 前1・3 の外を狙った | ハネ与ダメ/戦 |");
                Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
                for (int w = 0; w < WaveNames.Length; w++)
                    foreach (var v in Versions)
                    {
                        var a = Cell(Apply(bf, v), w, sc, 0, Seeds);
                        double idleRate = a.HaneHands == 0 ? 0 : 100.0 * a.HaneIdle / a.HaneHands;
                        Console.WriteLine($"| {WaveNames[w]} | {v.Tag} | {F1(a.Pct(a.Wins))} | {F1(a.Pct(a.AllSurv))} | {(a.Wins == 0 ? "—" : F2((double)a.WinTurnsSum / a.Wins))} | "
                            + $"{F2(a.Per(a.HaneIdle))} | {F1(idleRate)}% | {F2(a.Per(a.NoFront - a.Fallbacks))} | {F2(a.Per(a.NoLane))} | {F2(a.Per(a.Pinned))} | "
                            + $"{F2(a.Per(a.Fallbacks))} | {F2(a.Per(a.Reached))} | {F1(a.Per(a.HaneDealt))} |");
                    }
                Console.WriteLine();
            }

        // ツギ・リリ・「⚡」（ポンの台・今の規定だけ・版に依らない問い）。
        Console.WriteLine("## ツギ・リリの手番と「⚡ 追加攻撃」の中身（ポンの台 × 今の規定）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵 | ツギの手番/戦 | うち板を貼らなかった | リリの手番/戦 | うち吸わなかった | 板の反射/戦 | うち画面で「⚡」を開いた |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|");
        foreach (var (sname, sc) in Scales)
            for (int w = 0; w < WaveNames.Length; w++)
            {
                var a = Cell(Pon(UnitCatalog.HaneR3), w, sc, 0, Seeds);
                Console.WriteLine($"| {WaveNames[w]} | {sname} | {F2(a.Per(a.TsugiHands))} | {a.TsugiIdle} | {F2(a.Per(a.LiliHands))} | {a.LiliIdle} | {F2(a.Per(a.PlankReflects))} | {F2(a.Per(a.OffTurnPlank))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:0} 秒");
    }

    static partial void CheckImpl()
    {
        int bad = 0;
        void Ok(bool c, string what) { Console.WriteLine($"{(c ? "○" : "×")} {what}"); if (!c) bad++; }

        // (a) 札を持たないハネは規定と台本まで一致（札を足しただけで盤面が動かない）。V0 はもちろん、発火しない戦では版も一致する。
        var f0 = Pon(UnitCatalog.HaneR3);
        bool same = true;
        for (int w = 0; w < WaveNames.Length && same; w++)
            for (int s = 0; s < 60 && same; s++)
            {
                var sc = Scales[2].Sc;
                var r1 = BattleEngine.Run(BattleEngine.Materialize(f0, 0), WaveOf(w, sc)(), s, verbose: true);
                var r2 = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, VerOf("V0")), 0), WaveOf(w, sc)(), s, verbose: true);
                same = r1.Events.Count == r2.Events.Count && r1.Turns == r2.Turns && r1.PlayerWon == r2.PlayerWon;
            }
        Ok(same, "V0（規定）は同じ seed で台本の件数・ターン・勝敗が一致");

        // (b) 版の札が働かない戦（空振りの口が1度も開かない戦）では、版の台本は V0 と件数まで一致する。
        long diffWhenNoWhiff = 0, checkedN = 0;
        foreach (var v in Versions.Skip(1))
            for (int w = 0; w < 4; w++)
                for (int s = 0; s < 60; s++)
                {
                    var sc = Scales[0].Sc;
                    var p0 = BattleEngine.Materialize(f0, 0);
                    var r0 = BattleEngine.Run(p0, WaveOf(w, sc)(), s, verbose: true);
                    var t0 = r0.TallyByUnit["hane"];
                    bool trig = t0.ReboundNoFront > 0 || t0.BlastNoLane > 0;
                    if (trig) continue;
                    // ② は「押せなかった」最後尾でも萎縮を足すので、最後尾の吹っ飛ばしがあった戦は除く（別に数える）
                    var r1 = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, v), 0), WaveOf(w, sc)(), s, verbose: true);
                    var t1 = r1.TallyByUnit["hane"];
                    if (t1.BlastPinned > 0 || t1.BlastReached > 0) continue;
                    checkedN++;
                    if (r1.Events.Count != r0.Events.Count || r1.Turns != r0.Turns) diffWhenNoWhiff++;
                }
        Ok(diffWhenNoWhiff == 0, $"空振りの口が開かない戦（{checkedN} 戦）では版の台本が V0 と一致");

        // (c) ポンの戦（seed 12・1000/300）で V0 の空振りが 4 回、V123 で 0 回。
        Agg one(string tag) { var a = new Agg(); var p = BattleEngine.Materialize(Pon(VerOf(tag).Hane), 0);
            a.Take(BattleEngine.Run(p, WaveOf(5, Scales[2].Sc)(), 12, verbose: true), p); return a; }
        var a0 = one("V0"); var a3 = one("V123");
        Ok(a0.HaneIdle == 4 && a0.NoFront == 4, $"ポンの戦（seed 12）: V0 の空振り {a0.HaneIdle}（前列不在 {a0.NoFront}）＝ 4");
        Ok(a3.HaneIdle == 0, $"ポンの戦（seed 12）: V123 の空振り {a3.HaneIdle} ＝ 0");

        // (d) verbose の有無で勝敗・ターンが変わらない（版 V123）。
        bool vb = true;
        for (int w = 0; w < WaveNames.Length && vb; w++)
            for (int s = 0; s < 40 && vb; s++)
            {
                var f = Pon(VerOf("V123").Hane); var sc = Scales[1].Sc;
                var r1 = BattleEngine.Run(BattleEngine.Materialize(f, 0), WaveOf(w, sc)(), s, verbose: true);
                var r2 = BattleEngine.Run(BattleEngine.Materialize(f, 0), WaveOf(w, sc)(), s, verbose: false);
                vb = r1.Turns == r2.Turns && r1.PlayerWon == r2.PlayerWon && r1.PlayerSurvivors == r2.PlayerSurvivors;
            }
        Ok(vb, "V123 は verbose の有無で勝敗・ターン・生存が一致");

        // (e) 規定のハネ（①②を採用後）は V12 と台本の件数・ターン・勝敗・生存まで一致。
        bool adopt = true;
        foreach (var (_, bf0) in Boards)
            for (int w = 0; w < WaveNames.Length && adopt; w++)
                for (int s = 0; s < 40 && adopt; s++)
                {
                    var sc = Scales[1].Sc;
                    var fa = Apply(bf0, new Ver("規定", "", UnitCatalog.Hane));
                    var r1 = BattleEngine.Run(BattleEngine.Materialize(fa, 0), WaveOf(w, sc)(), s, verbose: true);
                    var r2 = BattleEngine.Run(BattleEngine.Materialize(Apply(bf0, VerOf("V12")), 0), WaveOf(w, sc)(), s, verbose: true);
                    adopt = r1.Events.Count == r2.Events.Count && r1.Turns == r2.Turns && r1.PlayerWon == r2.PlayerWon && r1.PlayerSurvivors == r2.PlayerSurvivors;
                }
        Ok(adopt, "規定のハネは V12（①＋②）と台本の件数・ターン・勝敗・生存が一致");

        Console.WriteLine(bad == 0 ? "check ok=True" : $"check ok=False（× {bad}）");
    }
}
