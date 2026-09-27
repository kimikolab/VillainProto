using BattleCore;
using static Common;

// retreat run —— 表A〜F（第225期）。台 M-ハネ・M-カド（席は J4 × 九/新兵 × 200/115 で総当たり）・参考（ガルド入り・D1 の席）
// × 版 J0〜J4 × 波（九/新兵・九/農兵・本編の第2〜5波）× 倍率（200/115・150/115・115/115）・seed 0..199。
static partial class RetreatDiag
{
    // =================================================================================
    // 席の総当たり（§6.1）: J4 × 九/新兵 × 200/115 × seed 1000..1099。全員生存 → 落ちた駒の数（少ない方）→ 決着T（勝った戦・短い方）→ 列挙順。
    // =================================================================================

    internal const int PickSeed0 = 1000, PickSeeds = 100;
    static readonly Dictionary<string, List<(Formation F, int W, int Sv, int Fell, long T)>> _picked = new();

    internal static Formation PickSeats(string key, Formation raw, bool print)
    {
        if (!_picked.TryGetValue(key, out var list))
        {
            var sc = Scales[0].Sc;
            var members = raw.Occupied().Select(o => o.Def.Id == "shio" ? J4! : o.Def).ToList();
            var perms = SeroDiag.Permute(members).Select(p => Formation.Build(front1: p[0], front3: p[1], center: p[2], back1: p[3], back3: p[4])).ToList();
            var res = new (Formation F, int W, int Sv, int Fell, long T)[perms.Count];
            Parallel.For(0, perms.Count, i =>
            {
                int w = 0, sv = 0, fell = 0; long t = 0;
                for (int s = PickSeed0; s < PickSeed0 + PickSeeds; s++)
                {
                    var r = BattleEngine.Run(BattleEngine.Materialize(perms[i], BattleContext.PlayerTeam), WaveOf(4, sc)(), s, verbose: false);
                    fell += r.PlayerStarterFallen.Count;
                    if (!r.PlayerWon) continue;
                    w++; t += r.Turns; if (r.PlayerStarterFallen.Count == 0) sv++;
                }
                res[i] = (perms[i], w, sv, fell, t);
            });
            list = res.Select((x, i) => (x, i)).OrderByDescending(z => z.x.Sv).ThenBy(z => z.x.Fell)
                .ThenBy(z => z.x.W == 0 ? long.MaxValue : z.x.T * 1000 / z.x.W).ThenBy(z => z.i).Select(z => z.x).ToList();
            _picked[key] = list;
        }
        if (print)
        {
            var top = list[0];
            Console.WriteLine($"### {key} の席（J4 × 九/新兵 × 200/115 × seed {PickSeed0}..{PickSeed0 + PickSeeds - 1}・120 通り）");
            Console.WriteLine();
            Console.WriteLine($"全員生存が最大と同値の並び: {list.Count(x => x.Sv == top.Sv)} 通り ／ 全員生存と落ちた駒の数が同値: {list.Count(x => x.Sv == top.Sv && x.Fell == top.Fell)} 通り。");
            Console.WriteLine();
            Console.WriteLine("| 順位 | 席 | 全員生存/100 | 落ちた駒（計） | 勝ち数/100 | 決着T |");
            Console.WriteLine("|--:|---|--:|--:|--:|--:|");
            for (int i = 0; i < 5; i++)
                Console.WriteLine($"| {i + 1} | {SeatsNamed(list[i].F)} | {list[i].Sv} | {list[i].Fell} | {list[i].W} | {(list[i].W == 0 ? "—" : ((double)list[i].T / list[i].W).ToString("F2"))} |");
            Console.WriteLine();
        }
        return WithShio(list[0].F, J0);
    }

    // =================================================================================
    // 帳簿
    // =================================================================================

    internal static readonly string[] MoveSrcNames = { "バサ", "シオの手番", "緊急退避", "ハネ", "カド", "セロ", "ほかの味方", "敵", "不明" };

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns, Turns, FellTotal;
        public readonly Dictionary<string, long> Fell = new(), FellT = new(), Dealt = new();
        public readonly Dictionary<string, string> Names = new();
        // シオ（表B）
        public long DrNom, DrGain, DrRetNom, DrRetGain, TendNom, TendGain, RgSwaps, RgSelf, ShioHealToLow, ShioHealTotal;
        public long RChances, RNoPartner, RSpent, RHeld, RHushed, RSwaps, RSelf, RInReaction;
        public readonly long[] StageReach = new long[4], StageTurn = new long[4];
        public readonly long[] RetreatByTurn = new long[31];
        public readonly Dictionary<string, long> Lowered = new();
        // 移動（表C）
        public long Moves;
        public readonly long[] MoveSrc = new long[9];
        public readonly long[] MovesByTurn = new long[31];
        // 火力（表D）
        public long YomiSweeps, YomiAttacks;
        public readonly long[] SeroStageReach = new long[4], SeroStageTurn = new long[4];
        public readonly long[] YomiAtkSum = new long[31], YomiAtkCnt = new long[31];
        // カド（表E）
        public long ThornGuard, KadoSplash, KadoSplashN, HavocTaken, KadoLowered;

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns; Turns += o.Turns; FellTotal += o.FellTotal;
            Add(Fell, o.Fell); Add(FellT, o.FellT); Add(Dealt, o.Dealt); Add(Lowered, o.Lowered);
            foreach (var (k, v) in o.Names) Names[k] = v;
            DrNom += o.DrNom; DrGain += o.DrGain; DrRetNom += o.DrRetNom; DrRetGain += o.DrRetGain; TendNom += o.TendNom; TendGain += o.TendGain;
            RgSwaps += o.RgSwaps; RgSelf += o.RgSelf; ShioHealToLow += o.ShioHealToLow; ShioHealTotal += o.ShioHealTotal;
            RChances += o.RChances; RNoPartner += o.RNoPartner; RSpent += o.RSpent; RHeld += o.RHeld; RHushed += o.RHushed;
            RSwaps += o.RSwaps; RSelf += o.RSelf; RInReaction += o.RInReaction;
            for (int i = 0; i < 4; i++) { StageReach[i] += o.StageReach[i]; StageTurn[i] += o.StageTurn[i]; SeroStageReach[i] += o.SeroStageReach[i]; SeroStageTurn[i] += o.SeroStageTurn[i]; }
            for (int i = 0; i < 31; i++) { RetreatByTurn[i] += o.RetreatByTurn[i]; MovesByTurn[i] += o.MovesByTurn[i]; YomiAtkSum[i] += o.YomiAtkSum[i]; YomiAtkCnt[i] += o.YomiAtkCnt[i]; }
            Moves += o.Moves; for (int i = 0; i < 9; i++) MoveSrc[i] += o.MoveSrc[i];
            YomiSweeps += o.YomiSweeps; YomiAttacks += o.YomiAttacks;
            ThornGuard += o.ThornGuard; KadoSplash += o.KadoSplash; KadoSplashN += o.KadoSplashN; HavocTaken += o.HavocTaken; KadoLowered += o.KadoLowered;
        }
        static void Add(Dictionary<string, long> a, Dictionary<string, long> b) { foreach (var (k, v) in b) a[k] = a.GetValueOrDefault(k) + v; }
        public double Per(long x) => (double)x / Math.Max(1, N);
        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;
        public string Reach(long[] reach, long[] turn, int k) => reach[k] == 0 ? "0%" : $"{100.0 * reach[k] / N:F0}% ／ T{(double)turn[k] / reach[k]:F1}";

        public void Take(BattleResult r, List<UnitState> player)
        {
            N++; Turns += r.Turns; FellTotal += r.PlayerStarterFallen.Count;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var byId = player.ToDictionary(u => u.InstanceId);
            foreach (var u in player) Names[u.Def.Id] = u.Def.Name;
            UnitState? shio = player.FirstOrDefault(u => u.Def.Id == "shio");
            UnitState? yomi = player.FirstOrDefault(u => u.Def.Id == "yomi");

            foreach (var (id, t) in r.TallyByUnit)
            {
                if (!Names.ContainsKey(id)) continue;
                Dealt[id] = Dealt.GetValueOrDefault(id) + t.DamageToEnemy;
                HavocTaken += t.HavocTaken;
                if (id == "kado") KadoLowered += t.RetreatLowered;
                if (t.RetreatLowered > 0) Lowered[id] = Lowered.GetValueOrDefault(id) + t.RetreatLowered;
                if (id == "shio")
                {
                    DrNom += t.DrifterNominal; DrGain += t.DrifterGained; TendNom += t.TendNominal; TendGain += t.TendGained;
                    if (t.DrifterBySrc is not null && t.DrifterBySrc.Length > 7) { DrRetGain += t.DrifterBySrc[7]; DrRetNom += t.DrifterNomBySrc![7]; }
                    RgSwaps += t.RegroupSwaps; RgSelf += t.RegroupSelf;
                    RChances += t.RetreatChances; RNoPartner += t.RetreatNoPartner; RSpent += t.RetreatSpent; RHeld += t.RetreatHeld; RHushed += t.RetreatHushed;
                    RSwaps += t.RetreatSwaps; RSelf += t.RetreatSelf; RInReaction += t.RetreatInReaction;
                    if (t.ShioStageTurn is not null) for (int k = 1; k < 4; k++) if (t.ShioStageTurn[k] > 0) { StageReach[k]++; StageTurn[k] += t.ShioStageTurn[k]; }
                }
                if (id == "sero" && t.EvStageTurn is not null)
                    for (int k = 1; k < 4; k++) if (t.EvStageTurn[k] > 0) { SeroStageReach[k]++; SeroStageTurn[k] += t.EvStageTurn[k]; }
            }

            int retreatMoves = 0;
            foreach (BattleEvent e in r.Events)
            {
                switch (e.Kind)
                {
                    case BattleEventKind.Death when e.TargetId is int d && byId.TryGetValue(d, out var du):
                        Fell[du.Def.Id] = Fell.GetValueOrDefault(du.Def.Id) + 1; FellT[du.Def.Id] = FellT.GetValueOrDefault(du.Def.Id) + e.Turn;
                        break;
                    case BattleEventKind.Retreat:
                        retreatMoves = 2;
                        if (e.Turn < 31) RetreatByTurn[e.Turn]++;
                        break;
                    case BattleEventKind.Move when e.TargetId is int mt && byId.ContainsKey(mt):
                    {
                        Moves++; if (e.Turn < 31) MovesByTurn[e.Turn]++;
                        int src;
                        if (e.ActorId is not int a) src = 8;
                        else if (!byId.TryGetValue(a, out var au)) src = 7;
                        else if (au.Def.Id == "shio") { if (retreatMoves > 0) { retreatMoves--; src = 2; } else src = 1; }
                        else src = au.Def.Id switch { "basa" => 0, "hane" => 3, "kado" => 4, "sero" => 5, _ => 6 };
                        MoveSrc[src]++;
                        break;
                    }
                    case BattleEventKind.Heal when shio is not null && e.ActorId == shio.InstanceId && e.TargetId is int ht && byId.TryGetValue(ht, out var hu):
                        ShioHealTotal += e.Amount;
                        if ((e.HpAfter - e.Amount) * 100 < hu.MaxHp * 40) ShioHealToLow += e.Amount;
                        break;
                    case BattleEventKind.Attack when yomi is not null && e.ActorId == yomi.InstanceId:
                        YomiAttacks++;
                        if (e.Pattern == AttackPattern.Sweep) YomiSweeps++;
                        break;
                    case BattleEventKind.Intercept when e.Text == InterceptLabels.ThornGuard && e.ActorId is int ka && byId.ContainsKey(ka):
                        ThornGuard++;
                        break;
                    case BattleEventKind.Damage when e.ActorId is int fa && byId.TryGetValue(fa, out var fu) && fu.Def.Id == "kado"
                                                   && e.TargetId is int ft && byId.ContainsKey(ft) && ft != fa:
                        KadoSplash += e.Amount; KadoSplashN++;
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
        Console.WriteLine("# 第225期 表A〜F（`retreat run`）");
        Console.WriteLine();
        if (J4 is null) { Console.WriteLine("版の札がまだ無い（実装の前）。"); return; }
        Console.WriteLine("## 席（§6.1）");
        Console.WriteLine();
        var mh = PickSeats("M-ハネ", RawHane, true);
        var mk = PickSeats("M-カド", RawKado, true);
        var benches = new (string Name, Formation F)[] { ("M-ハネ", mh), ("M-カド", mk), ("参考 ガルド", RefGald) };
        foreach (var (n, f) in benches) Console.WriteLine($"- {n}: {SeatsNamed(f)}");
        Console.WriteLine();

        var vers = Versions;
        var res = new Agg[benches.Length, vers.Length, Scales.Length, WaveNames.Length];
        for (int b = 0; b < benches.Length; b++)
            for (int v = 0; v < vers.Length; v++)
                for (int s = 0; s < Scales.Length; s++)
                    for (int w = 0; w < WaveNames.Length; w++)
                        res[b, v, s, w] = Measure(WithShio(benches[b].F, vers[v].Def), WaveOf(w, Scales[s].Sc));
        Agg G(int b, int v, int s, int[] ws) { var a = new Agg(); foreach (int w in ws) a.Merge(res[b, v, s, w]); return a; }
        string[] ids = { "basa", "yomi", "shio", "sero", "hane", "kado", "gald" };

        // ---------------- 表A ----------------
        Console.WriteLine("## 表A —— 全員生存 ／ 勝率（主判定は 九/新兵 × 200/115）");
        Console.WriteLine();
        Console.Write("| 台 | 版 |");
        foreach (var sc in Scales) foreach (var (gn, _) in Groups) Console.Write($" {sc.Name} {gn} |");
        Console.WriteLine();
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Scales.Length * Groups.Length)));
        for (int b = 0; b < benches.Length; b++)
            for (int v = 0; v < vers.Length; v++)
            {
                Console.Write($"| {benches[b].Name} | {vers[v].Tag} |");
                for (int s = 0; s < Scales.Length; s++) foreach (var (_, ws) in Groups) { var a = G(b, v, s, ws); Console.Write($" **{F1(a.Surv)}** ／ {F1(a.Win)} |"); }
                Console.WriteLine();
            }
        Console.WriteLine();
        Console.WriteLine("### 表A' —— 九/新兵 × 200/115: 決着T・落ちた駒（落ちた率 ／ 平均ターン）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 全員生存 | 決着T | 落ちた駒/戦 | " + string.Join(" | ", ids.Select(i => i)) + " |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|" + string.Concat(ids.Select(_ => "---|")));
        for (int b = 0; b < benches.Length; b++)
            for (int v = 0; v < vers.Length; v++)
            {
                var a = res[b, v, 0, 4];
                Console.WriteLine($"| {benches[b].Name} | {vers[v].Tag} | {F1(a.Win)} | {F1(a.Surv)} | {F2(a.WinT)} | {F2(a.Per(a.FellTotal))} | "
                    + string.Join(" | ", ids.Select(i => !a.Names.ContainsKey(i) ? "—" : a.Fell.GetValueOrDefault(i) == 0 ? "0%" : $"{100.0 * a.Fell[i] / a.N:F0}% ／ T{(double)a.FellT[i] / a.Fell[i]:F1}")) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("### 表A'' —— 波ごとの全員生存 ／ 勝率（200/115）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", WaveNames) + " | 緊急退避が粛で止まった（第二波・1戦あたり） |");
        Console.WriteLine("|---|---|" + string.Concat(WaveNames.Select(_ => "---|")) + "--:|");
        for (int b = 0; b < benches.Length; b++)
            for (int v = 0; v < vers.Length; v++)
                Console.WriteLine($"| {benches[b].Name} | {vers[v].Tag} | " + string.Join(" | ", Enumerable.Range(0, WaveNames.Length).Select(w => $"{F1(res[b, v, 0, w].Surv)} ／ {F1(res[b, v, 0, w].Win)}"))
                    + $" | {res[b, v, 0, 0].Per(res[b, v, 0, 0].RHushed):F2} |");
        Console.WriteLine();

        // ---------------- 表B ----------------
        var focus = new (int S, int[] Ws, string Name)[] { (0, new[] { 4 }, "200/115 九/新兵"), (1, new[] { 0, 1, 2, 3 }, "150/115 本編 第2〜5波") };
        Console.WriteLine("## 表B —— シオ（1戦あたり。回復は実際に増えた HP）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 移り木 増えた ／ 名目 | 溢れの割合 | うち緊急退避 増えた ／ 名目 | 手当て | 4割未満の駒へ | 段1 | 段2 | 段3 | 判定 | 相手なし | 使い切り | 止まった（粛） | 下げた（うち自分・反撃の中） | T1 | T2 | T3 | T4〜 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|---|---|---|--:|--:|--:|---|---|--:|--:|--:|--:|");
        for (int b = 0; b < benches.Length; b++)
            foreach (var (s, ws, fn) in focus)
                for (int v = 0; v < vers.Length; v++)
                {
                    var a = G(b, v, s, ws);
                    long late = Enumerable.Range(4, 27).Sum(t => a.RetreatByTurn[t]);
                    Console.WriteLine($"| {benches[b].Name} | {fn} | {vers[v].Tag} | {a.Per(a.DrGain):F1} ／ {a.Per(a.DrNom):F1} | {(a.DrNom == 0 ? "—" : $"{100.0 * (a.DrNom - a.DrGain) / a.DrNom:F0}%")} "
                        + $"| {a.Per(a.DrRetGain):F1} ／ {a.Per(a.DrRetNom):F1} | {a.Per(a.TendGain):F1} | {a.Per(a.ShioHealToLow):F1} "
                        + $"| {a.Reach(a.StageReach, a.StageTurn, 1)} | {a.Reach(a.StageReach, a.StageTurn, 2)} | {a.Reach(a.StageReach, a.StageTurn, 3)} "
                        + $"| {a.Per(a.RChances):F2} | {a.Per(a.RNoPartner):F2} | {a.Per(a.RSpent):F2} | {a.Per(a.RHeld):F2}（{a.Per(a.RHushed):F2}） "
                        + $"| **{a.Per(a.RSwaps):F2}**（{a.Per(a.RSelf):F2}・{a.Per(a.RInReaction):F2}） | {a.Per(a.RetreatByTurn[1]):F2} | {a.Per(a.RetreatByTurn[2]):F2} | {a.Per(a.RetreatByTurn[3]):F2} | {a.Per(late):F2} |");
                }
        Console.WriteLine();
        Console.WriteLine("「判定」＝味方が4割を切った被弾で緊急退避の判定に来た回数（J3/J4 だけ）。「4割未満の駒へ」＝シオの回復（移り木・手当て）のうち、回復の前に HP が4割未満だった駒に入った量。");
        Console.WriteLine();
        Console.WriteLine("### 表B' —— 緊急退避で下げた駒（1戦あたり・九/新兵 × 200/115）");
        Console.WriteLine();
        for (int b = 0; b < benches.Length; b++)
            foreach (int v in new[] { 3, 4 }.Where(x => x < vers.Length))
            {
                var a = res[b, v, 0, 4];
                Console.WriteLine($"- {benches[b].Name} {vers[v].Tag}: " + string.Join(" ／ ", a.Lowered.OrderByDescending(kv => kv.Value).Select(kv => $"{a.Names[kv.Key]} {a.Per(kv.Value):F2}")));
            }
        Console.WriteLine();

        // ---------------- 表C ----------------
        Console.WriteLine("## 表C —— 移動の帳簿（味方が動かされた回数・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 計 | " + string.Join(" | ", MoveSrcNames) + " | T1 | T2 | T3 | T4〜 |");
        Console.WriteLine("|---|---|---|--:|" + string.Concat(MoveSrcNames.Select(_ => "--:|")) + "--:|--:|--:|--:|");
        for (int b = 0; b < benches.Length; b++)
            foreach (var (s, ws, fn) in focus)
                for (int v = 0; v < vers.Length; v++)
                {
                    var a = G(b, v, s, ws);
                    long late = Enumerable.Range(4, 27).Sum(t => a.MovesByTurn[t]);
                    Console.WriteLine($"| {benches[b].Name} | {fn} | {vers[v].Tag} | **{a.Per(a.Moves):F2}** | " + string.Join(" | ", a.MoveSrc.Select(x => $"{a.Per(x):F2}"))
                        + $" | {a.Per(a.MovesByTurn[1]):F2} | {a.Per(a.MovesByTurn[2]):F2} | {a.Per(a.MovesByTurn[3]):F2} | {a.Per(late):F2} |");
                }
        Console.WriteLine();
        Console.WriteLine("「セロ」は回避の入れ替え、「カド」は棘守りの身代わり、「ハネ」は勢い余って（突き返しの代金）。");
        Console.WriteLine();

        // ---------------- 表D ----------------
        Console.WriteLine("## 表D —— 火力（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | ヨミ 与ダメ | セロ 与ダメ | 台の合計 | ヨミの薙ぎ ／ 振った | セロ 段1 | 段2 | 段3 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|---|---|---|");
        for (int b = 0; b < benches.Length; b++)
            foreach (var (s, ws, fn) in focus)
                for (int v = 0; v < vers.Length; v++)
                {
                    var a = G(b, v, s, ws);
                    Console.WriteLine($"| {benches[b].Name} | {fn} | {vers[v].Tag} | {a.Per(a.Dealt.GetValueOrDefault("yomi")):F1} | {a.Per(a.Dealt.GetValueOrDefault("sero")):F1} | {a.Per(a.Dealt.Values.Sum()):F1} "
                        + $"| {a.Per(a.YomiSweeps):F2} ／ {a.Per(a.YomiAttacks):F2} "
                        + $"| {a.Reach(a.SeroStageReach, a.SeroStageTurn, 1)} | {a.Reach(a.SeroStageReach, a.SeroStageTurn, 2)} | {a.Reach(a.SeroStageReach, a.SeroStageTurn, 3)} |");
                }
        Console.WriteLine();

        // ---------------- 表E ----------------
        Console.WriteLine("## 表E —— カド（M-カド・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 倍率・波 | 版 | 身代わり | 棘の巻き込み（味方へ）回 ／ 量 | 惨禍で増えた被ダメ（名目） | カドが下げられた | 全員生存 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|");
        foreach (var (s, ws, fn) in focus)
            for (int v = 0; v < vers.Length; v++)
            {
                var a = G(1, v, s, ws);
                Console.WriteLine($"| {fn} | {vers[v].Tag} | {a.Per(a.ThornGuard):F2} | {a.Per(a.KadoSplashN):F2} ／ {a.Per(a.KadoSplash):F1} | {a.Per(a.HavocTaken):F1} | {a.Per(a.KadoLowered):F2} | {F1(a.Surv)} |");
            }
        Console.WriteLine();

        // ---------------- 参考: 緊急退避の効きの出どころ ----------------
        Console.WriteLine("## 参考 —— 緊急退避の効きは回復か、列か（九/新兵 × 200/115）");
        Console.WriteLine();
        Console.WriteLine("J3′ ＝ J3 から移り木（`Drifter` / `DrifterMend`）を外した版（組み替え・手当て・緊急退避は残す）。J1′ ＝ J1 から同じく移り木を外した版。**下げても回復も攻撃の上乗せも入らない。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | J1 | J1′ | J3 | J3′ | J3′ の緊急退避/戦 | J3′ − J1′ | J3 − J1 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        var noDrift = new[] { TraitId.Drifter, TraitId.DrifterMend };
        UnitDef Strip(UnitDef d) => Copy(d, d.Traits.Where(t => !noDrift.Contains(t)).ToArray());
        for (int b = 0; b < benches.Length; b++)
        {
            var a1 = res[b, 1, 0, 4]; var a3 = res[b, 3, 0, 4];
            var p1 = Measure(WithShio(benches[b].F, Strip(J1!)), WaveOf(4, Scales[0].Sc));
            var p3 = Measure(WithShio(benches[b].F, Strip(J3!)), WaveOf(4, Scales[0].Sc));
            Console.WriteLine($"| {benches[b].Name} | {F1(a1.Surv)} | {F1(p1.Surv)} | {F1(a3.Surv)} | {F1(p3.Surv)} | {p3.Per(p3.RSwaps):F2} | {D1(p3.Surv - p1.Surv)} | {D1(a3.Surv - a1.Surv)} |");
        }
        Console.WriteLine();

        // ---------------- 表F ----------------
        Console.WriteLine("## 表F —— 目標（9体・200/115・全員生存）");
        Console.WriteLine();
        var cells = new List<(string B, string V, Agg A)>();
        for (int b = 0; b < benches.Length; b++) for (int v = 0; v < vers.Length; v++) cells.Add((benches[b].Name, vers[v].Tag, res[b, v, 0, 4]));
        Console.WriteLine("| 順位 | 台 | 版 | 全員生存 | 勝率 | 決着T |");
        Console.WriteLine("|--:|---|---|--:|--:|--:|");
        int rank = 0;
        foreach (var c in cells.OrderByDescending(c => c.A.AllSurv).ThenByDescending(c => c.A.Wins))
            Console.WriteLine($"| {++rank} | {c.B} | {c.V} | {F1(c.A.Surv)} | {F1(c.A.Win)} | {F2(c.A.WinT)} |");
        Console.WriteLine();
        foreach (var (n, f) in benches.Take(2)) Console.WriteLine($"- {n} の席: {SeatsNamed(f)}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }
}
