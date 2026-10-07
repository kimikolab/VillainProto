using BattleCore;
using static Common;

// sero run —— 表A〜F（第223期）。台 S1〜S4 × 波（第2〜5波 ＋ 九/新兵）× 版 × 倍率（115/115・150/115）・seed 0..199。
static partial class SeroDiag
{
    // =================================================================================
    // 版（指示書 §4）。**札の差し替え**だけ（数値・型は今のセロのまま）。E1− は参考（代金＝入れ替えを外した `yP`）。
    // =================================================================================

    internal static UnitDef Copy(UnitDef d, TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
        Pattern = d.Pattern, Actions = d.Actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    internal static readonly UnitDef E0 = UnitCatalog.SeroOld;
    internal static readonly UnitDef E1 = Copy(UnitCatalog.SeroOld, new[] { TraitId.Evade, TraitId.EvadeSwap });
    internal static readonly UnitDef E2 = Copy(UnitCatalog.SeroOld, new[] { TraitId.Evade, TraitId.EvadeSwap, TraitId.StatusArrow });
    internal static readonly UnitDef E1NoSwap = Copy(UnitCatalog.SeroOld, new[] { TraitId.Evade });

    internal static readonly (string Tag, UnitDef Def)[] Versions =
    {
        ("E0", E0), ("E1", E1), ("E2", E2), ("E1−", E1NoSwap),
    };

    // =================================================================================
    // 席の総当たり（§7.1: E1 × 九/新兵 × 150/115 で選ぶ）。測る seed（0..199）とは別の帯（1000..1049）で選ぶ。
    // =================================================================================

    static readonly Dictionary<string, List<(Formation F, int W, int Sv, long T)>> _picked = new();

    internal static Formation PickSeats(string key, Formation raw, bool print)
    {
        if (!_picked.TryGetValue(key, out var list))
        {
            var sc = Scales[1].Sc;
            var members = raw.Occupied().Select(o => o.Def.Id == "sero" ? E1 : o.Def).ToList();
            list = new();
            foreach (var p in Permute(members))
            {
                var f = Formation.Build(front1: p[0], front3: p[1], center: p[2], back1: p[3], back3: p[4]);
                var q = DriftDiag.Quick(f, WaveOf(4, sc), 1000, 50);
                list.Add((f, q.Wins, q.Surv, q.WinT));
            }
            list = list.Select((x, i) => (x, i)).OrderByDescending(z => z.x.W).ThenByDescending(z => z.x.Sv)
                       .ThenBy(z => z.x.W == 0 ? long.MaxValue : z.x.T * 1000 / z.x.W).ThenBy(z => z.i).Select(z => z.x).ToList();
            _picked[key] = list;
        }
        if (print)
        {
            var top = list[0];
            Console.WriteLine($"### {key} の席（E1 × 九/新兵 × 150/115 × seed 1000..1049・120 通り）");
            Console.WriteLine();
            Console.WriteLine($"勝ち数が最大と同値の並び: {list.Count(x => x.W == top.W)} 通り ／ 勝ち数と全員生存が同値: {list.Count(x => x.W == top.W && x.Sv == top.Sv)} 通り。");
            Console.WriteLine();
            Console.WriteLine("| 順位 | 席 | 勝ち数/50 | 全員生存/50 |");
            Console.WriteLine("|--:|---|--:|--:|");
            for (int i = 0; i < 5; i++) Console.WriteLine($"| {i + 1} | {SeatsNamed(list[i].F)} | {list[i].W} | {list[i].Sv} |");
            Console.WriteLine();
        }
        return WithSero(list[0].F, E0);
    }

    // =================================================================================
    // 帳簿
    // =================================================================================

    internal static readonly string[] MoveSrcNames = { "回避", "バサ", "シオ", "ハネ", "ほかの味方", "敵", "不明" };
    internal static readonly string[] DeathNames = { "敵の攻撃", "毒と燃焼の刻み", "放電・爆発", "味方の刃", "中継ほか" };

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns;
        public readonly Dictionary<string, long> Fell = new(), FellTurnSum = new(), Dealt = new();
        public readonly Dictionary<string, string> Names = new();
        // セロ
        public long Rolls, Evades, EvadedAmt, Swaps, SwapNone, SwapRefused, Ripostes, RipPierce, RipHushed, RipInReaction,
                    RipDealt, Barrages, Arrows, BarDealt, SeroDealt, ArrowP, ArrowB, ArrowS;
        public readonly long[] StageReach = new long[4], StageTurn = new long[4];
        public readonly long[] MoveSrc = new long[7];
        public readonly long[] DeathEv = new long[5];   // 台本から（全版）
        public long SeroDeaths, SeroDeathT;
        public readonly Dictionary<string, long> Partner = new();
        public readonly long[] AtkSum = new long[31], AtkCnt = new long[31];
        public long YomiMoved;
        // 表E（E2）
        public long FoeDischarges, FoeShockSpent, FoeBrittle, FoeBurnTicks;
        // R119: 避けた一撃の量の分布
        public readonly long[] EvadedHist = new long[6];   // 〜9 / 10〜19 / 20〜29 / 30〜49 / 50〜 / 不明

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns;
            Add(Fell, o.Fell); Add(FellTurnSum, o.FellTurnSum); Add(Dealt, o.Dealt); Add(Partner, o.Partner);
            foreach (var (k, v) in o.Names) Names[k] = v;
            Rolls += o.Rolls; Evades += o.Evades; EvadedAmt += o.EvadedAmt; Swaps += o.Swaps; SwapNone += o.SwapNone; SwapRefused += o.SwapRefused;
            Ripostes += o.Ripostes; RipPierce += o.RipPierce; RipHushed += o.RipHushed; RipInReaction += o.RipInReaction;
            RipDealt += o.RipDealt; Barrages += o.Barrages; Arrows += o.Arrows; BarDealt += o.BarDealt; SeroDealt += o.SeroDealt;
            ArrowP += o.ArrowP; ArrowB += o.ArrowB; ArrowS += o.ArrowS;
            for (int i = 0; i < 4; i++) { StageReach[i] += o.StageReach[i]; StageTurn[i] += o.StageTurn[i]; }
            for (int i = 0; i < 7; i++) MoveSrc[i] += o.MoveSrc[i];
            for (int i = 0; i < 5; i++) DeathEv[i] += o.DeathEv[i];
            for (int i = 0; i < 6; i++) EvadedHist[i] += o.EvadedHist[i];
            SeroDeaths += o.SeroDeaths; SeroDeathT += o.SeroDeathT;
            for (int i = 0; i < AtkSum.Length; i++) { AtkSum[i] += o.AtkSum[i]; AtkCnt[i] += o.AtkCnt[i]; }
            YomiMoved += o.YomiMoved;
            FoeDischarges += o.FoeDischarges; FoeShockSpent += o.FoeShockSpent; FoeBrittle += o.FoeBrittle; FoeBurnTicks += o.FoeBurnTicks;
        }

        static void Add(Dictionary<string, long> a, Dictionary<string, long> b) { foreach (var (k, v) in b) a[k] = a.GetValueOrDefault(k) + v; }
        static void Inc(Dictionary<string, long> a, string k, long v = 1) => a[k] = a.GetValueOrDefault(k) + v;

        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;
        public double Per(long x) => (double)x / Math.Max(1, N);

        public string FellText(IEnumerable<(string Id, string Name)> units) => string.Join(" ", units
            .Where(u => Fell.GetValueOrDefault(u.Id) > 0)
            .Select(u => $"{u.Name[^Math.Min(2, u.Name.Length)..]} {100.0 * Fell[u.Id] / N:F0}%/{(double)FellTurnSum[u.Id] / Fell[u.Id]:F1}"));

        public void Take(BattleResult r, List<UnitState> player)
        {
            N++;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var idOf = player.ToDictionary(u => u.InstanceId, u => u.Def.Id);
            foreach (var u in player) Names[u.Def.Id] = u.Def.Name;
            UnitState sero = player.First(u => u.Def.Id == "sero");
            UnitState? yomi = player.FirstOrDefault(u => u.Def.Id == "yomi");

            foreach (var (id, t) in r.TallyByUnit)
            {
                if (!Names.ContainsKey(id)) continue;
                Inc(Dealt, id, t.DamageToEnemy);
                if (id != "sero") continue;
                SeroDealt += t.DamageToEnemy;
                Rolls += t.EvRolls; Evades += t.Evades; EvadedAmt += t.EvadedAmount; Swaps += t.EvSwaps; SwapNone += t.EvSwapNone; SwapRefused += t.EvSwapRefused;
                Ripostes += t.EvRipostes; RipPierce += t.EvRipostePierce; RipHushed += t.EvRiposteHushed; RipInReaction += t.EvRiposteInReaction;
                RipDealt += t.EvRiposteDealt; Barrages += t.EvBarrages; Arrows += t.EvArrows; BarDealt += t.EvBarrageDealt;
                ArrowP += t.ArrowPoison; ArrowB += t.ArrowBurn; ArrowS += t.ArrowShock;
                if (t.EvStageTurn is not null) for (int k = 1; k < 4; k++) if (t.EvStageTurn[k] > 0) { StageReach[k]++; StageTurn[k] += t.EvStageTurn[k]; }
                if (t.EvMoveSrc is not null) for (int k = 0; k < 7; k++) MoveSrc[k] += t.EvMoveSrc[k];
            }

            BattleEvent? lastDmgSero = null;
            bool pendStatus = false, lastDmgStatus = false;
            foreach (BattleEvent e in r.Events)
            {
                switch (e.Kind)
                {
                    case BattleEventKind.Death:
                        if (e.TargetId is int dt && idOf.TryGetValue(dt, out var did))
                        {
                            Inc(Fell, did); Inc(FellTurnSum, did, e.Turn);
                            if (dt == sero.InstanceId)
                            {
                                SeroDeaths++; SeroDeathT += e.Turn;
                                int k = 4;
                                if (lastDmgSero is not null)
                                {
                                    var le = lastDmgSero;
                                    k = le.Relayed ? 4
                                      : le.ActorId is null ? 1
                                      : !idOf.ContainsKey(le.ActorId.Value) ? (le.FriendlyFire ? 4 : 0)
                                      : 2;   // 味方が出どころ（放電・爆発・味方の刃）——下で割る
                                    if (k == 2 && !lastDmgStatus) k = 3;
                                }
                                DeathEv[k]++;
                            }
                        }
                        break;
                    case BattleEventKind.Damage:
                        if (e.TargetId == sero.InstanceId) { lastDmgSero = e; lastDmgStatus = pendStatus; }
                        pendStatus = false;
                        if (e.TargetId is int tg && !idOf.ContainsKey(tg) && e.BrittleExtra is int be) FoeBrittle += be;
                        break;
                    case BattleEventKind.Evade:
                        if (e.PartnerId is int pid && idOf.TryGetValue(pid, out var pn)) Inc(Partner, Names[pn]); else Inc(Partner, "（なし）");
                        EvadedHist[e.Amount < 10 ? 0 : e.Amount < 20 ? 1 : e.Amount < 30 ? 2 : e.Amount < 50 ? 3 : 4]++;
                        break;
                    case BattleEventKind.StatSnapshot:
                        if (e.TargetId == sero.InstanceId && e.Turn < AtkSum.Length) { AtkSum[e.Turn] += e.Amount; AtkCnt[e.Turn]++; }
                        break;
                    case BattleEventKind.Move:
                        if (yomi is not null && e.TargetId == yomi.InstanceId) YomiMoved++;
                        break;
                    case BattleEventKind.MireBurst:
                        pendStatus = true;
                        break;
                    case BattleEventKind.Discharge:
                        pendStatus = true;
                        if (e.TargetId is int dg && !idOf.ContainsKey(dg)) FoeDischarges++;
                        break;
                    case BattleEventKind.ShockSpent:
                        if (e.TargetId is int sp && !idOf.ContainsKey(sp)) FoeShockSpent++;
                        break;
                }
            }
        }

    }

    internal static Agg Measure(Formation f, Func<List<UnitState>> enemy, int seed0 = 0, int seeds = Seeds)
    {
        var total = new Agg();
        var gate = new object();
        Parallel.For(0, seeds, () => new Agg(), (j, _, local) =>
        {
            var player = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            local.Take(BattleEngine.Run(player, enemy(), seed0 + j, verbose: true), player);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    // =================================================================================
    // 本体
    // =================================================================================

    static partial void RunImpl(string arg)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第223期 —— 逃げ上手のセロ（`0 sero run`）");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{Seeds - 1}。版: E0 今のセロ（`Sniper` / `Coward`）／ E1 回避・追い撃ち・段・入れ替え ／ E2 ＋ 状態の矢 ／ **E1− 参考**（E1 から入れ替えを外した `yP`＝代金なし）。");
        Console.WriteLine("シオ・ヨミは前段で規定になった V3（`Regroup` / `CreakSweep`）。");
        Console.WriteLine();
        Console.WriteLine("## 台");
        Console.WriteLine();
        var benches = new List<(string Name, Formation F)> { ("S1 ポン", BenchS1Pon) };
        benches.Add(("S1 選", PickSeats("S1", BenchS1Pon, true)));
        benches.Add(("S2 選", PickSeats("S2", BenchS2Raw, true)));
        benches.Add(("S3 選", PickSeats("S3", BenchS3Raw, true)));
        // 参考: S3 は天井で 120 通りが同値（R276）。状態の矢を見るため、セロを中央（隣 4 ＝ カタ・ベニの両方の隣）に置いた並びも測る
        //（`PickSeats` の3位と同じ並び・測る前に固定）。
        benches.Add(("S3 中央（参考）", Formation.Build(front1: UnitCatalog.Kubi, front3: UnitCatalog.Beni, center: UnitCatalog.SeroOld,
                                                       back1: UnitCatalog.KataS3, back3: UnitCatalog.Mio)));
        foreach (var r in CompareRowsWithSero()) benches.Add(("S4 " + r.Name, r.F));
        foreach (var (n, f) in benches) Console.WriteLine($"- {n}: {SeatsNamed(f)}");
        Console.WriteLine();

        int B = benches.Count, S = Scales.Length, W = WaveNames.Length, V = Versions.Length;
        var res = new Agg[B, S, W, V];
        for (int b = 0; b < B; b++) for (int s = 0; s < S; s++) for (int w = 0; w < W; w++) for (int v = 0; v < V; v++)
            res[b, s, w, v] = Measure(WithSero(benches[b].F, Versions[v].Def), WaveOf(w, Scales[s].Sc));

        Agg Sum(int b, int s, IEnumerable<int> ws, int v) { var a = new Agg(); foreach (int w in ws) a.Merge(res[b, s, w, v]); return a; }
        var groups = new (string Name, int[] Ws)[] { ("本編 第2〜5波", new[] { 0, 1, 2, 3 }), ("九/新兵", new[] { 4 }) };
        var s4 = Enumerable.Range(0, B).Where(b => benches[b].Name.StartsWith("S4")).ToList();
        var s13 = Enumerable.Range(0, B).Where(b => !benches[b].Name.StartsWith("S4")).ToList();

        // ---- 表A0 要約 ----
        Console.WriteLine("## 表A0 —— 要約（勝率 ／ 全員生存）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | " + string.Join(" | ", Versions.Select(v => v.Tag)) + " | E1−E0 | E2−E1 | E1−(E1−) |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("--:|", V + 3)));
        for (int b = 0; b < B; b++) for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups)
        {
            var a = Enumerable.Range(0, V).Select(v => Sum(b, s, ws, v)).ToArray();
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {string.Join(" | ", a.Select(x => $"{F1(x.Win)} ／ {F1(x.Surv)}"))} | {D1(a[1].Win - a[0].Win)} | {D1(a[2].Win - a[1].Win)} | {D1(a[1].Win - a[3].Win)} |");
        }
        Console.WriteLine();
        Console.WriteLine("S4（`compare` のセロの12行）の平均:");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | " + string.Join(" | ", Versions.Select(v => v.Tag)) + " | E1−E0 | 下がった行（E1−E0 ≤ −3.0） |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", V + 1)) + "---|");
        for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups)
        {
            var mean = Enumerable.Range(0, V).Select(v => s4.Average(b => Sum(b, s, ws, v).Win)).ToArray();
            var down = s4.Where(b => Sum(b, s, ws, 1).Win - Sum(b, s, ws, 0).Win <= -3.0).Select(b => benches[b].Name[3..]).ToList();
            Console.WriteLine($"| {Scales[s].Name} | {gn} | {string.Join(" | ", mean.Select(F1))} | {D1(mean[1] - mean[0])} | {down.Count} 行: {string.Join(" ／ ", down)} |");
        }
        Console.WriteLine();

        // ---- 表A ----
        Console.WriteLine("## 表A —— 台 × 波 × 版 × 倍率（勝率 ／ 全員生存 ／ 勝った戦の決着T ／ 落ちた駒: 落ちた率/平均ターン）");
        Console.WriteLine();
        foreach (int b in s13)
        {
            var units = benches[b].F.Occupied().Select(o => (o.Def.Id, o.Def.Name)).ToList();
            Console.WriteLine($"### {benches[b].Name}");
            Console.WriteLine();
            Console.WriteLine("| 倍率 | 波 | 版 | 勝率 | 全員生存 | 決着T | 落ちた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|---|");
            for (int s = 0; s < S; s++) for (int w = 0; w < W; w++) for (int v = 0; v < V; v++)
            {
                var a = res[b, s, w, v];
                Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {Versions[v].Tag} | {F1(a.Win)} | {F1(a.Surv)} | {F2(a.WinT)} | {a.FellText(units)} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine("### S4（`compare` のセロの行・波ごとの勝率 E0 → E1 → E2）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 倍率 | 第二波 | 第三波 | 第四波 | 第五波 | 九/新兵 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (int b in s4) for (int s = 0; s < S; s++)
            Console.WriteLine($"| {benches[b].Name[3..]} | {Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(0, W).Select(w =>
                $"{F1(res[b, s, w, 0].Win)} → {F1(res[b, s, w, 1].Win)} → {F1(res[b, s, w, 2].Win)}")) + " |");
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B —— セロの帳簿（E1・E2・E1−。／ 戦）");
        Console.WriteLine();
        Console.WriteLine("回避率 ＝ 避けた ÷ 判定。追い撃ちは本数（段3 は1回の回避で2本）。攻撃力はターン頭の値（生きているとき）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | 判定 | 避けた | 回避率 | 避けた量 ／ 回 | 入れ替え（なし・空振り） | 追い撃ち（うち貫き） | 撃てず（粛ほか ／ 反撃中） | 入れ替えた相手（／ 戦） | 攻 T1 | T2 | T3 | T4 | T5 | T6 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|---|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (int b in s13.Concat(s4)) for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups) foreach (int v in new[] { 1, 2, 3 })
        {
            var a = Sum(b, s, ws, v);
            string pt = string.Join(" ", a.Partner.OrderByDescending(kv => kv.Value).Select(kv => $"{Short(kv.Key)} {a.Per(kv.Value):F2}"));
            string atk = string.Join(" | ", Enumerable.Range(1, 6).Select(t => a.AtkCnt[t] == 0 ? "—" : ((double)a.AtkSum[t] / a.AtkCnt[t]).ToString("F1")));
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {Versions[v].Tag} | {F2(a.Per(a.Rolls))} | {F2(a.Per(a.Evades))} | {F1(100.0 * a.Evades / Math.Max(1, a.Rolls))}% | {F1(a.Evades == 0 ? double.NaN : (double)a.EvadedAmt / a.Evades)} | {F2(a.Per(a.Swaps))}（{F2(a.Per(a.SwapNone))}・{F2(a.Per(a.SwapRefused))}） | {F2(a.Per(a.Ripostes))}（{F2(a.Per(a.RipPierce))}） | {F2(a.Per(a.RipHushed))} ／ {F2(a.Per(a.RipInReaction))} | {pt} | {atk} |");
        }
        Console.WriteLine();
        Console.WriteLine("避けた一撃の量の分布（R119: 確率の回避は弾いた量が敵の火力に比例する。E1・S1〜S3・両倍率・全波）:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 〜9 | 10〜19 | 20〜29 | 30〜49 | 50〜 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (int b in s13)
        {
            var a = new Agg(); for (int s = 0; s < S; s++) a.Merge(Sum(b, s, Enumerable.Range(0, W), 1));
            long n = Math.Max(1, a.EvadedHist.Take(5).Sum());
            Console.WriteLine($"| {benches[b].Name} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(i => $"{100.0 * a.EvadedHist[i] / n:F0}%")) + " |");
        }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C —— 段（E1・E2・E1−）");
        Console.WriteLine();
        Console.WriteLine("動かされた回数の出どころ（／ 戦）と、段1〜3 に届いた率 ／ 平均ターン。「回避」はセロ自身の入れ替え。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | 動かされた | " + string.Join(" | ", MoveSrcNames) + " | 段1 | 段2 | 段3 |");
        Console.WriteLine("|---|---|---|---|--:|" + string.Concat(Enumerable.Repeat("--:|", 7)) + "---|---|---|");
        foreach (int b in s13.Concat(s4)) for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups) foreach (int v in new[] { 1, 2, 3 })
        {
            var a = Sum(b, s, ws, v);
            string R(int k) => $"{F1(100.0 * a.StageReach[k] / a.N)}% ／ {F2(a.StageReach[k] == 0 ? double.NaN : (double)a.StageTurn[k] / a.StageReach[k])}";
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {Versions[v].Tag} | {F2(a.Per(a.MoveSrc.Sum()))} | {string.Join(" | ", a.MoveSrc.Select(x => F2(a.Per(x))))} | {R(1)} | {R(2)} | {R(3)} |");
        }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D —— 与ダメ（／ 戦）と同じ台のほかの駒との順位");
        Console.WriteLine();
        Console.WriteLine("セロの与ダメを 手番 ／ 追い撃ち ／ 乱れ撃ち に割る（手番 ＝ 計 − 追い撃ち − 乱れ撃ち）。順位は5枚の与ダメ（敵へ）の平均の順。ヨミが動かされた回数は P3。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | セロ 計 | 手番 | 追い撃ち | 乱れ撃ち | 順位 | 5枚の与ダメ | ヨミが動かされた |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|---|--:|");
        foreach (int b in s13.Concat(s4)) for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups) for (int v = 0; v < V; v++)
        {
            var a = Sum(b, s, ws, v);
            var order = a.Dealt.OrderByDescending(kv => kv.Value).ToList();
            int rank = order.FindIndex(kv => kv.Key == "sero") + 1;
            string all = string.Join(" ", order.Select(kv => $"{Short(a.Names[kv.Key])} {a.Per(kv.Value):F0}"));
            double turn = a.Per(a.SeroDealt - a.RipDealt - a.BarDealt);
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {Versions[v].Tag} | {F1(a.Per(a.SeroDealt))} | {F1(turn)} | {F1(a.Per(a.RipDealt))} | {F1(a.Per(a.BarDealt))} | {rank} | {all} | {(benches[b].F.Occupied().Any(o => o.Def.Id == "yomi") ? F2(a.Per(a.YomiMoved)) : "—")} |");
        }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E —— 状態の矢（E2 と E1 の比較。／ 戦）");
        Console.WriteLine();
        Console.WriteLine("敵の放電 ＝ 敵が敵の隣へ放電した本数（連鎖の大きさ）、弾けた ＝ 敵の感電が弾けた数、脆さ ＝ 敵が受けた一撃に燃焼の脆さで足された量（全員の一撃）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | 勝率 | 毒の矢 | 燃焼の矢 | 感電の矢 | 敵の放電 | 敵の感電が弾けた | 脆さの分 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        // S4 は E2 で矢を1本でも撃った行だけ（味方が毒・火をセロに書く行）。
        foreach (int b in s13.Concat(s4.Where(b => Enumerable.Range(0, S).Any(s => Sum(b, s, Enumerable.Range(0, W), 2) is var x && x.ArrowP + x.ArrowB + x.ArrowS > 0))))
            for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups) foreach (int v in new[] { 1, 2 })
        {
            var a = Sum(b, s, ws, v);
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {Versions[v].Tag} | {F1(a.Win)} | {F2(a.Per(a.ArrowP))} | {F2(a.Per(a.ArrowB))} | {F2(a.Per(a.ArrowS))} | {F2(a.Per(a.FoeDischarges))} | {F2(a.Per(a.FoeShockSpent))} | {F1(a.Per(a.FoeBrittle))} |");
        }
        Console.WriteLine();

        // ---- 表F ----
        Console.WriteLine("## 表F —— セロが倒れた原因（台本の最後の一撃から・全版）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | 倒れた率 ／ T | " + string.Join(" | ", DeathNames) + " |");
        Console.WriteLine("|---|---|---|---|---|" + string.Concat(Enumerable.Repeat("--:|", 5)));
        foreach (int b in s13.Concat(s4)) for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups) for (int v = 0; v < V; v++)
        {
            var a = Sum(b, s, ws, v);
            long d = Math.Max(1, a.SeroDeaths);
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {Versions[v].Tag} | {F1(100.0 * a.SeroDeaths / a.N)}% ／ {F2(a.SeroDeaths == 0 ? double.NaN : (double)a.SeroDeathT / a.SeroDeaths)} | {string.Join(" | ", a.DeathEv.Select(x => $"{100.0 * x / d:F0}%"))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }

    static string Short(string name) => name.Length <= 2 ? name : name[^2..];
}
