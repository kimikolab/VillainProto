using BattleCore;
using static Common;

// decoy run —— 表A〜F（第226期）。台 M-ハネ（K3 × 九/新兵 × 200/200 で総当たり）・M-ハネ（第225期の席）・参考（雷の編成）
// × 版 K0〜K4（＋ シオの段の遅い版 K3s・K4s）× 波（九/新兵・九/農兵・本編の第2〜5波）× 倍率（200/200・200/115・115/115）・seed 0..199。
static partial class DecoyDiag
{
    // =================================================================================
    // 席の総当たり（§8.1）: 版 × 九/新兵 × 200/200 × seed 1000..1099・120 通り。
    // 全員生存 → 落ちた駒の数（少ない方）→ 決着T（勝った戦・短い方）→ 列挙順。
    // =================================================================================

    internal const int PickSeed0 = 1000, PickSeeds = 100;

    internal static List<(Formation F, int W, int Sv, int Fell, long T)> PickSeats(Formation raw, Ver v)
    {
        var sc = Scales[0].Sc;
        var members = Apply(raw, v).Occupied().Select(o => o.Def).ToList();
        var perms = SeroDiag.Permute(members).Select(p => Formation.Build(front1: p[0], front3: p[1], center: p[2], back1: p[3], back3: p[4])).ToList();
        var res = new (Formation F, int W, int Sv, int Fell, long T)[perms.Count];
        Parallel.For(0, perms.Count, i =>
        {
            int w = 0, sv = 0, fell = 0; long t = 0;
            for (int s = PickSeed0; s < PickSeed0 + PickSeeds; s++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(OldYomiShio(perms[i]), BattleContext.PlayerTeam), WaveOf(MainWave, sc)(), s, verbose: false);
                fell += r.PlayerStarterFallen.Count;
                if (!r.PlayerWon) continue;
                w++; t += r.Turns; if (r.PlayerStarterFallen.Count == 0) sv++;
            }
            res[i] = (perms[i], w, sv, fell, t);
        });
        return res.Select((x, i) => (x, i)).OrderByDescending(z => z.x.Sv).ThenBy(z => z.x.Fell)
            .ThenBy(z => z.x.W == 0 ? long.MaxValue : z.x.T * 1000 / z.x.W).ThenBy(z => z.i).Select(z => z.x).ToList();
    }

    static void PrintPick(string key, string ver, List<(Formation F, int W, int Sv, int Fell, long T)> list)
    {
        var top = list[0];
        Console.WriteLine($"### {key} の席（{ver} × 九/新兵 × 200/200 × seed {PickSeed0}..{PickSeed0 + PickSeeds - 1}・120 通り）");
        Console.WriteLine();
        Console.WriteLine($"全員生存が最大と同値の並び: {list.Count(x => x.Sv == top.Sv)} 通り ／ 全員生存と落ちた駒の数が同値: {list.Count(x => x.Sv == top.Sv && x.Fell == top.Fell)} 通り。");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 席 | 全員生存/100 | 落ちた駒（計） | 勝ち数/100 | 決着T |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|");
        for (int i = 0; i < 5; i++)
            Console.WriteLine($"| {i + 1} | {SeatsNamed(list[i].F)} | {list[i].Sv} | {list[i].Fell} | {list[i].W} | {(list[i].W == 0 ? "—" : ((double)list[i].T / list[i].W).ToString("F2"))} |");
        Console.WriteLine();
    }

    // =================================================================================
    // 帳簿
    // =================================================================================

    internal static readonly string[] SrcNames = { "バサ", "ハネ", "セロ", "シオ", "ヨミ", "ほかの味方", "敵", "不明" };

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns, Turns, FellTotal;
        public readonly Dictionary<string, long> Fell = new(), FellT = new(), Dealt = new();
        public readonly Dictionary<string, string> Names = new();
        // セロ（表B）
        public long Drew, Stolen, EvRolls, Evades, Ripostes, RipDealt, SeroFell, SeroLowered, SeroFront, SeroTurns;
        /// <summary>セロが倒れた一撃の直前の HP が最大HPの 4 割以上だった（緊急退避の窓を跨がずに倒れた）／ セロが受けた一撃（HP に届いたもの）。</summary>
        public long SeroSkipWindow, SeroHits;
        public readonly long[] SeroStageReach = new long[4], SeroStageTurn = new long[4];
        // 敵の乱れ（表C）
        public long FoeMoves, FoeAdv, Confused, ConfusedOther, Capped, Struck, StruckDmgFoe, StruckDmgSelfSide, Staggers, PushTwo, Squalls, SquallCapped;
        public readonly long[] MoveSrc = new long[8];
        public readonly long[] BasaReach = new long[4], BasaTurn = new long[4], HaneReach = new long[4], HaneTurn = new long[4];
        // 敵の手番（表D）
        public long LostStagger, LostStun, LostStruck, RunSum, Run2; public int RunMax;
        // シオ（表E）
        public readonly long[] ShioReach = new long[4], ShioTurn = new long[4];
        public long ShioMoves, Retreats;

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns; Turns += o.Turns; FellTotal += o.FellTotal;
            Add(Fell, o.Fell); Add(FellT, o.FellT); Add(Dealt, o.Dealt);
            foreach (var (k, v) in o.Names) Names[k] = v;
            Drew += o.Drew; Stolen += o.Stolen; EvRolls += o.EvRolls; Evades += o.Evades; Ripostes += o.Ripostes; RipDealt += o.RipDealt;
            SeroFell += o.SeroFell; SeroLowered += o.SeroLowered; SeroFront += o.SeroFront; SeroTurns += o.SeroTurns;
            SeroSkipWindow += o.SeroSkipWindow; SeroHits += o.SeroHits;
            FoeMoves += o.FoeMoves; FoeAdv += o.FoeAdv; Confused += o.Confused; ConfusedOther += o.ConfusedOther; Capped += o.Capped;
            Struck += o.Struck; StruckDmgFoe += o.StruckDmgFoe; StruckDmgSelfSide += o.StruckDmgSelfSide; Staggers += o.Staggers; PushTwo += o.PushTwo;
            Squalls += o.Squalls; SquallCapped += o.SquallCapped;
            LostStagger += o.LostStagger; LostStun += o.LostStun; LostStruck += o.LostStruck; RunSum += o.RunSum; Run2 += o.Run2; RunMax = Math.Max(RunMax, o.RunMax);
            ShioMoves += o.ShioMoves; Retreats += o.Retreats;
            for (int i = 0; i < 8; i++) MoveSrc[i] += o.MoveSrc[i];
            for (int i = 0; i < 4; i++)
            {
                SeroStageReach[i] += o.SeroStageReach[i]; SeroStageTurn[i] += o.SeroStageTurn[i];
                BasaReach[i] += o.BasaReach[i]; BasaTurn[i] += o.BasaTurn[i]; HaneReach[i] += o.HaneReach[i]; HaneTurn[i] += o.HaneTurn[i];
                ShioReach[i] += o.ShioReach[i]; ShioTurn[i] += o.ShioTurn[i];
            }
        }
        static void Add(Dictionary<string, long> a, Dictionary<string, long> b) { foreach (var (k, v) in b) a[k] = a.GetValueOrDefault(k) + v; }
        public double Per(long x) => (double)x / Math.Max(1, N);
        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;
        public string Reach(long[] reach, long[] turn, int k) => reach[k] == 0 ? "0%" : $"{100.0 * reach[k] / N:F0}% ／ T{(double)turn[k] / reach[k]:F1}";

        public void Take(BattleResult r, List<UnitState> player, List<UnitState> enemy, Dictionary<int, int> slot0)
        {
            N++; Turns += r.Turns; FellTotal += r.PlayerStarterFallen.Count;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var byId = player.ToDictionary(u => u.InstanceId);
            var enemyIds = enemy.Select(u => u.InstanceId).ToHashSet();
            foreach (var u in player) Names[u.Def.Id] = u.Def.Name;

            foreach (var (id, t) in r.TallyByUnit)
            {
                if (!Names.ContainsKey(id)) continue;
                Dealt[id] = Dealt.GetValueOrDefault(id) + t.DamageToEnemy;
                if (id == "sero")
                {
                    Drew += t.DecoyDrew; Stolen += t.DecoyStolen; EvRolls += t.EvRolls; Evades += t.Evades; Ripostes += t.EvRipostes; RipDealt += t.EvRiposteDealt;
                    SeroLowered += t.RetreatLowered;
                    if (t.EvStageTurn is not null) for (int k = 1; k < 4; k++) if (t.EvStageTurn[k] > 0) { SeroStageReach[k]++; SeroStageTurn[k] += t.EvStageTurn[k]; }
                }
                if (id == "basa")
                {
                    Confused += t.DisarrayConfuses; ConfusedOther += t.DisarrayConfusesOther; Capped += t.DisarrayCapped;
                    Squalls += t.SquallFires; SquallCapped += t.SquallCapped;
                    if (t.DisarrayStageTurn is not null) for (int k = 1; k < 4; k++) if (t.DisarrayStageTurn[k] > 0) { BasaReach[k]++; BasaTurn[k] += t.DisarrayStageTurn[k]; }
                    if (t.DisarrayConfuses == 0) Confused += t.ShuffleConfuses;   // K0/K1 は今の混乱（バサ自身の入れ替えだけ）
                }
                if (id == "hane")
                {
                    PushTwo += t.DisarrayPushTwo;
                    if (t.DisarrayStageTurn is not null) for (int k = 1; k < 4; k++) if (t.DisarrayStageTurn[k] > 0) { HaneReach[k]++; HaneTurn[k] += t.DisarrayStageTurn[k]; }
                }
                if (id == "shio")
                {
                    Retreats += t.RetreatSwaps;
                    if (t.ShioStageTurn is not null) for (int k = 1; k < 4; k++) if (t.ShioStageTurn[k] > 0) { ShioReach[k]++; ShioTurn[k] += t.ShioStageTurn[k]; }
                }
            }

            var tr = new Tracker(slot0, player);
            var nameOf = player.Concat(enemy).ToDictionary(u => u.InstanceId, u => u.Def.Id);
            int? seroId = player.FirstOrDefault(u => u.Def.Id == "sero")?.InstanceId;
            var lostTurns = new Dictionary<int, SortedSet<int>>();
            // 同士討ち: `Confused`（同士討ち）は `PerformAttack` の**出口**で出るので、その一撃の Attack から後の Damage を溜めておき、出口で敵（自軍）に入った分を数える。
            int? curAttacker = null; long curToFoe = 0;
            UnitState? seroU = player.FirstOrDefault(u => u.Def.Id == "sero");
            int seroHp = seroU?.MaxHp ?? 0;
            foreach (BattleEvent e in r.Events)
            {
                switch (e.Kind)
                {
                    case BattleEventKind.TurnStart:
                        curAttacker = null; curToFoe = 0;
                        if (seroId is int si && tr.Alive.Contains(si))
                        {
                            SeroTurns++;
                            if (tr.Pool(true).Contains(si)) SeroFront++;
                        }
                        break;
                    case BattleEventKind.Death when e.TargetId is int d && byId.TryGetValue(d, out var du):
                        Fell[du.Def.Id] = Fell.GetValueOrDefault(du.Def.Id) + 1; FellT[du.Def.Id] = FellT.GetValueOrDefault(du.Def.Id) + e.Turn;
                        if (du.Def.Id == "sero") SeroFell++;
                        break;
                    case BattleEventKind.Move when e.TargetId is int m && enemyIds.Contains(m):
                    {
                        FoeMoves++;
                        string src = e.ActorId is int a && nameOf.TryGetValue(a, out var id)
                            ? (enemyIds.Contains(a) ? "敵" : id switch { "basa" => "バサ", "hane" => "ハネ", "sero" => "セロ", "shio" => "シオ", "yomi" => "ヨミ", _ => "ほかの味方" })
                            : "不明";
                        MoveSrc[Array.IndexOf(SrcNames, src)]++;
                        if (FormationRules.DepthOf(FormationRules.RowOf(e.Slot)) < FormationRules.DepthOf(FormationRules.RowOf(tr.Slot[m]))) FoeAdv++;
                        break;
                    }
                    case BattleEventKind.Move when e.TargetId is int m2 && byId.ContainsKey(m2):
                        ShioMoves++;
                        break;
                    case BattleEventKind.Confused when e.TargetId is int c && enemyIds.Contains(c) && e.Text == ConfusedLabels.Struck:
                        Struck++; Lose(c, e.Turn);
                        if (curAttacker == c) StruckDmgFoe += curToFoe;
                        curAttacker = null; curToFoe = 0;
                        break;
                    case BattleEventKind.Attack when !e.Reaction:
                        curAttacker = e.ActorId; curToFoe = 0;
                        break;
                    case BattleEventKind.Damage:
                        if (curAttacker is int ca && e.ActorId == ca && e.TargetId is int dt && enemyIds.Contains(dt)) curToFoe += e.Amount;
                        if (seroU is not null && e.TargetId == seroU.InstanceId && e.Amount > 0)
                        {
                            SeroHits++;
                            if (e.HpAfter <= 0 && seroHp * 100 >= seroU.MaxHp * RetreatTrait.Percent) SeroSkipWindow++;
                            seroHp = e.HpAfter;
                        }
                        break;
                    case BattleEventKind.Heal when seroU is not null && e.TargetId == seroU.InstanceId:
                        seroHp = e.HpAfter;
                        break;
                    case BattleEventKind.Stagger when e.TargetId is int g && enemyIds.Contains(g):
                        if (e.Text == StaggerLabels.Lost) { LostStagger++; Lose(g, e.Turn); } else if (e.Text == StaggerLabels.Fell) Staggers++;
                        break;
                    case BattleEventKind.Stun when e.Text == StunLabels.Lost && e.TargetId is int h && enemyIds.Contains(h):
                        LostStun++; Lose(h, e.Turn);
                        break;
                }
                tr.Apply(e);
            }
            int mx = 0;
            foreach (var set in lostTurns.Values)
            {
                int cur = 0, prev = -9;
                foreach (int t in set) { cur = t == prev + 1 ? cur + 1 : 1; prev = t; if (cur > mx) mx = cur; }
            }
            RunSum += mx; if (mx > RunMax) RunMax = mx; if (mx >= 2) Run2++;

            void Lose(int id, int t) { if (!lostTurns.TryGetValue(id, out var set)) lostTurns[id] = set = new(); set.Add(t); }
        }
    }

    internal static Agg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = Seeds)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var a = new Agg();
            var (r, p, e, slot0) = Fight(f, w, sc, seed0 + i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    // =================================================================================
    // 表A〜F
    // =================================================================================

    static partial void RunImpl(string arg)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第226期 —— 回避盾のセロ・バサとハネの対・シオの段の時期（`0 decoy run`）");
        Console.WriteLine();
        var vers = Versions;
        Console.WriteLine("版: " + string.Join(" ／ ", vers.Select(v => v.Tag)) + "。**線は置かない。**");
        Console.WriteLine();

        // 席
        var pickK3 = PickSeats(MHane225, VerOf("K3"));
        PrintPick("M-ハネ", "K3", pickK3);
        var pickK0 = PickSeats(MHane225, VerOf("K0"));
        PrintPick("M-ハネ（参考: K0 で選んだ席）", "K0", pickK0);
        Console.WriteLine("M-ハネ（セロ前）＝ 上の K3 の順位でセロが前列（前1・前3）にいる最上位の並び。");
        Console.WriteLine();
        var pickTh = PickSeats(Thunder, VerOf("K0"));
        PrintPick("参考 雷", "K0", pickTh);

        Formation raw(Formation picked) => Apply(picked, VerOf("K0"));   // 席は版の駒のまま返るので K0 の駒に戻す
        var benches = new List<(string Name, Formation F, bool Versions)>
        {
            ("M-ハネ", raw(pickK3[0].F), true),
            ("M-ハネ（225）", MHane225, true),
            ("M-ハネ（K0の席）", raw(pickK0[0].F), true),
            ("M-ハネ（セロ前）", raw(pickK3.First(x => x.F.Occupied().Any(o => o.Def.Id == "sero" && FormationRules.RowOf(o.Slot) == Row.Front)).F), true),
            ("参考 雷（ポンの席）", Thunder, false),
            ("参考 雷（選んだ席）", pickTh[0].F, false),
        };
        foreach (var b in benches) Console.WriteLine($"- {b.Name}: {SeatsNamed(b.F)}");
        Console.WriteLine();

        var cells = new List<(int B, Ver V, int W, int S)>();
        for (int b = 0; b < benches.Count; b++)
            foreach (var v in benches[b].Versions ? vers : vers.Take(1).ToArray())
                for (int w = 0; w < WaveNames.Length; w++)
                    for (int s = 0; s < Scales.Length; s++)
                        cells.Add((b, v, w, s));
        var res = new Dictionary<(int, string, int, int), Agg>();
        foreach (var c in cells)
            res[(c.B, c.V.Tag, c.W, c.S)] = Measure(Apply(benches[c.B].F, c.V), c.W, Scales[c.S].Sc);

        int[] waveOrder = { 4, 5, 0, 1, 2, 3 };

        // ---- 表A ----
        Console.WriteLine("## 表A. 全員生存 ／ 勝率（%）");
        Console.WriteLine();
        foreach (int s in Enumerable.Range(0, Scales.Length))
        {
            Console.WriteLine($"### 倍率 {Scales[s].Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", waveOrder.Select(w => WaveNames[w])) + " | 本編の平均 |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", waveOrder.Length)) + "---|");
            for (int b = 0; b < benches.Count; b++)
                foreach (var v in benches[b].Versions ? vers : vers.Take(1).ToArray())
                {
                    var cellsS = waveOrder.Select(w => res[(b, v.Tag, w, s)]).ToList();
                    double ms = Enumerable.Range(0, 4).Average(w => res[(b, v.Tag, w, s)].Surv), mw = Enumerable.Range(0, 4).Average(w => res[(b, v.Tag, w, s)].Win);
                    string bold(string x) => b == 0 && waveOrder[0] == 4 ? x : x;
                    Console.WriteLine($"| {benches[b].Name} | {v.Tag} | " + string.Join(" | ", cellsS.Select(a => $"{F1(a.Surv)} ／ {F1(a.Win)}")) + $" | {F1(ms)} ／ {F1(mw)} |");
                }
            Console.WriteLine();
        }

        Console.WriteLine("### 表A'. 九/新兵 × 200/200 の決着T と落ちた駒（1戦あたり ／ 落ちた平均ターン）");
        Console.WriteLine();
        string[] ids = { "hane", "basa", "yomi", "shio", "sero" };
        Console.WriteLine("| 台 | 版 | 全員生存 | 勝率 | 決着T | 落ちた駒 | " + string.Join(" | ", ids) + " |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|" + string.Concat(Enumerable.Repeat("---|", ids.Length)));
        for (int b = 0; b < 4; b++)
            foreach (var v in vers)
            {
                var a = res[(b, v.Tag, MainWave, 0)];
                Console.WriteLine($"| {benches[b].Name} | {v.Tag} | {F1(a.Surv)} | {F1(a.Win)} | {F2(a.WinT)} | {F2(a.Per(a.FellTotal))} | "
                                  + string.Join(" | ", ids.Select(id => a.Fell.GetValueOrDefault(id) == 0 ? "0%" : $"{100.0 * a.Fell[id] / a.N:F0}% ／ T{(double)a.FellT[id] / a.Fell[id]:F1}")) + " |");
            }
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B. セロ（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("前列にいた ＝ ターンの頭にセロが pool（単体攻撃の的になれる列）にいたターンの割合。引きつけた ＝ 挑発で的になった（介入の後も）。回避率 ＝ 避けた ÷ 判定。"
                          + "下げられた ＝ 緊急退避で下げられた。うち窓を跨がず ＝ 倒れた一撃の直前の HP が最大HPの 4 割以上（緊急退避の判定に一度も来ずに倒れた）。受けた一撃 ＝ HP に届いた被弾。段 ＝ セロの移動の段（到達した戦の割合 ／ 平均ターン）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 前列にいた | 引きつけた | 引き剥がされた | 判定 | 避けた | 回避率 | 撃ち返し | 撃ち返しの与ダメ | セロの与ダメ | 倒れた | うち窓を跨がず | 受けた一撃 | 下げられた | 段1 | 段2 | 段3 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|---|");
        foreach (var (s, w) in new[] { (0, 4), (1, 4), (0, 0) })
            for (int b = 0; b < 4; b++)
                foreach (var v in vers)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {100.0 * a.SeroFront / Math.Max(1, a.SeroTurns):F0}% | {F2(a.Per(a.Drew))} | {F2(a.Per(a.Stolen))} | {F2(a.Per(a.EvRolls))} | {F2(a.Per(a.Evades))} | "
                                      + $"{(a.EvRolls == 0 ? "—" : (100.0 * a.Evades / a.EvRolls).ToString("F0") + "%")} | {F2(a.Per(a.Ripostes))} | {F1(a.Per(a.RipDealt))} | {F1(a.Per(a.Dealt.GetValueOrDefault("sero")))} | "
                                      + $"{100.0 * a.SeroFell / a.N:F0}% | {(a.SeroFell == 0 ? "—" : (100.0 * a.SeroSkipWindow / a.SeroFell).ToString("F0") + "%")} | {F2(a.Per(a.SeroHits))} | {F2(a.Per(a.SeroLowered))} | {a.Reach(a.SeroStageReach, a.SeroStageTurn, 1)} | {a.Reach(a.SeroStageReach, a.SeroStageTurn, 2)} | {a.Reach(a.SeroStageReach, a.SeroStageTurn, 3)} |");
                }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. 敵の乱れ（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("動かされた ＝ 敵の `Move`（入れ替えなら2体で2回）。混乱 ＝ バサが立てた混乱（K0/K1 は今のバサ自身の入れ替えの分）／ うちバサ以外 ＝ ほかの駒の移動で前へ出た敵。"
                          + "同士討ち ＝ 混乱したまま振った一撃 ／ 敵に ＝ その一撃が敵（自軍）に入れた与ダメ。転倒 ＝ 転ばせた。段 ＝ バサ／ハネの敵の乱れの段（到達した戦の割合 ／ 平均ターン）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 動かされた | " + string.Join(" | ", SrcNames.Take(5)) + " | ほか | 前へ | 混乱 | うちバサ以外 | 上限で止まった | 同士討ち | 敵に | 転倒 | 2体目の突き返し | 突風 | バサ段1 | 段2 | 段3 |");
        Console.WriteLine("|---|---|---|--:|" + string.Concat(Enumerable.Repeat("--:|", 6)) + "--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|---|");
        foreach (var (s, w) in new[] { (0, 4), (1, 4), (0, 0), (0, 2) })
            for (int b = 0; b < 4; b++)
                foreach (var v in vers)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(a.Per(a.FoeMoves))} | " + string.Join(" | ", a.MoveSrc.Take(5).Select(x => F2(a.Per(x)))) + $" | {F2(a.Per(a.MoveSrc.Skip(5).Sum()))} | "
                                      + $"{F2(a.Per(a.FoeAdv))} | {F2(a.Per(a.Confused))} | {F2(a.Per(a.ConfusedOther))} | {F2(a.Per(a.Capped))} | {F2(a.Per(a.Struck))} | {F1(a.Per(a.StruckDmgFoe))} | {F2(a.Per(a.Staggers))} | "
                                      + $"{F2(a.Per(a.PushTwo))} | {F2(a.Per(a.Squalls))} | {a.Reach(a.BasaReach, a.BasaTurn, 1)} | {a.Reach(a.BasaReach, a.BasaTurn, 2)} | {a.Reach(a.BasaReach, a.BasaTurn, 3)} |");
                }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. 敵の手番（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("失った ＝ 転倒 ／ 痺れ ／ 同士討ち（混乱のまま自軍へ振った手番）。続けて ＝ 同じ敵が続けて失った最大（戦ごとの最大を平均）／ 2以上の戦 ／ 最大。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 失った 計 | 転倒 | 痺れ | 同士討ち | 続けて（平均） | 2以上の戦 | 最大 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (s, w) in new[] { (0, 4), (1, 4), (0, 0), (0, 2) })
            for (int b = 0; b < benches.Count; b++)
                foreach (var v in benches[b].Versions ? vers : vers.Take(1).ToArray())
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(a.Per(a.LostStagger + a.LostStun + a.Struck))} | {F2(a.Per(a.LostStagger))} | {F2(a.Per(a.LostStun))} | {F2(a.Per(a.Struck))} | "
                                      + $"{F2(a.Per(a.RunSum))} | {100.0 * a.Run2 / a.N:F1}% | {a.RunMax} |");
                }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E. シオの段の時期（規定 4/8/14 ／ 遅い 8/16/26）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 味方の移動 | 段1 | 段2 | 段3 | 緊急退避 | 全員生存 | 決着T |");
        Console.WriteLine("|---|---|---|--:|---|---|---|--:|--:|--:|");
        foreach (var (s, w) in new[] { (0, 4), (1, 4), (0, 5), (0, 0), (0, 1), (0, 2), (0, 3) })
            for (int b = 0; b < 4; b++)
                foreach (var v in vers.Where(v => v.Tag is "K0" or "K3" or "K3s" or "K4" or "K4s"))
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F1(a.Per(a.ShioMoves))} | {a.Reach(a.ShioReach, a.ShioTurn, 1)} | {a.Reach(a.ShioReach, a.ShioTurn, 2)} | {a.Reach(a.ShioReach, a.ShioTurn, 3)} | "
                                      + $"{F2(a.Per(a.Retreats))} | {F1(a.Surv)} | {F2(a.WinT)} |");
                }
        Console.WriteLine();

        // ---- 表F ----
        Console.WriteLine("## 表F. 目標（九/新兵 × 200/200・全員生存）");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 台 | 版 | 全員生存 | 勝率 | 決着T |");
        Console.WriteLine("|--:|---|---|--:|--:|--:|");
        var rank = res.Where(kv => kv.Key.Item3 == MainWave && kv.Key.Item4 == 0)
            .Select(kv => (Bench: benches[kv.Key.Item1].Name, V: kv.Key.Item2, A: kv.Value))
            .OrderByDescending(x => x.A.Surv).ThenByDescending(x => x.A.Win).ThenBy(x => x.A.WinT).ToList();
        for (int i = 0; i < rank.Count; i++)
            Console.WriteLine($"| {i + 1} | {rank[i].Bench} | {rank[i].V} | {F1(rank[i].A.Surv)} | {F1(rank[i].A.Win)} | {F2(rank[i].A.WinT)} |");
        Console.WriteLine();

        // 与ダメ（参考）
        Console.WriteLine("### 参考: 与ダメ（九/新兵 × 200/200・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", ids) + " | 計 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", ids.Length + 1)));
        for (int b = 0; b < 4; b++)
            foreach (var v in vers)
            {
                var a = res[(b, v.Tag, MainWave, 0)];
                Console.WriteLine($"| {benches[b].Name} | {v.Tag} | " + string.Join(" | ", ids.Select(id => F1(a.Per(a.Dealt.GetValueOrDefault(id))))) + $" | {F1(a.Per(a.Dealt.Values.Sum()))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
