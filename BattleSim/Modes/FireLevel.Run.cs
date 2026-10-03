using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FS = FireScaleDiag;

// firelevel run —— 段2 ＋ 表A〜H（指示書 §5.3）と、`compare` 61 行の版ごとの動き（受け入れ 1・2）。
static partial class FireLevelDiag
{
    internal const int TM = 7;
    static readonly string[] RoleNames = { "ボルグ", "ホタ", "ヒヨ", "相方" };
    static int RoleOf(string id) => id switch { "borg" => 0, "hota" => 1, "hiyo" => 2, _ => 3 };

    /// <summary>1つのセル（台 × 版 × 波 × 倍率）の集計。</summary>
    internal sealed class LAgg
    {
        public long N, Wins, AllSurv, Turns, Fell;
        public readonly long[] FellRole = new long[4], DealtRole = new long[4];
        public readonly BA.Agg Death = new();
        public long HiyoBattles, GiftT2, GiftBattles, Gifts, Recips, Gifts1, Gifts2, GiftTurns, GiftStalled, GiftHush, GiftNoTarget, GiftSkipped;
        public long Stokes, StokeNoTarget, Spreads, SpreadGrowth, SpreadWasted, SelfGrowth, Wilts, Outs, Lit, FireKeeps;
        public readonly long[] FirstGift = new long[TM + 1];
        public readonly long[] RecipRole = new long[4];
        public readonly long[] RecipLevel = new long[5];
        public readonly long[,] LvSum = new long[4, TM + 1], LvCnt = new long[4, TM + 1], Lv4 = new long[4, TM + 1], Burn = new long[4, TM + 1];
        public readonly long[,] LvHist = new long[4, 5];
        public readonly long[] Simul4 = new long[TM + 1], SimulCnt = new long[TM + 1];
        public readonly long[] FoeBurn = new long[TM + 1], FoeAlive = new long[TM + 1];
        public readonly long[] Stage = new long[5];
        public readonly long[] Reached = new long[TM + 1];   // その周回まで戦が続いた数（F の分母）
        public readonly Dictionary<string, long[]> OutSum = new(), OutMax = new();   // 駒 Id → 周回ごとの総量の和・最大
        public readonly Dictionary<string, long> HitMax = new(), HitSum = new(), HitN = new();
        public readonly Dictionary<string, FS.PicAgg> Pics = new();

        public void Merge(LAgg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; Turns += o.Turns; Fell += o.Fell;
            for (int i = 0; i < 4; i++) { FellRole[i] += o.FellRole[i]; DealtRole[i] += o.DealtRole[i]; RecipRole[i] += o.RecipRole[i]; }
            Death.Merge(o.Death);
            HiyoBattles += o.HiyoBattles; GiftT2 += o.GiftT2; GiftBattles += o.GiftBattles; Gifts += o.Gifts; Recips += o.Recips; Gifts1 += o.Gifts1; Gifts2 += o.Gifts2;
            GiftTurns += o.GiftTurns; GiftStalled += o.GiftStalled; GiftHush += o.GiftHush; GiftNoTarget += o.GiftNoTarget; GiftSkipped += o.GiftSkipped;
            Stokes += o.Stokes; StokeNoTarget += o.StokeNoTarget; Spreads += o.Spreads; SpreadGrowth += o.SpreadGrowth; SpreadWasted += o.SpreadWasted; SelfGrowth += o.SelfGrowth;
            Wilts += o.Wilts; Outs += o.Outs; Lit += o.Lit; FireKeeps += o.FireKeeps;
            for (int t = 0; t <= TM; t++)
            {
                FirstGift[t] += o.FirstGift[t]; Simul4[t] += o.Simul4[t]; SimulCnt[t] += o.SimulCnt[t]; FoeBurn[t] += o.FoeBurn[t]; FoeAlive[t] += o.FoeAlive[t]; Reached[t] += o.Reached[t];
                for (int r = 0; r < 4; r++) { LvSum[r, t] += o.LvSum[r, t]; LvCnt[r, t] += o.LvCnt[r, t]; Lv4[r, t] += o.Lv4[r, t]; Burn[r, t] += o.Burn[r, t]; }
            }
            for (int r = 0; r < 4; r++) for (int l = 0; l < 5; l++) LvHist[r, l] += o.LvHist[r, l];
            for (int l = 0; l < 5; l++) { Stage[l] += o.Stage[l]; RecipLevel[l] += o.RecipLevel[l]; }
            foreach (var (k, v) in o.OutSum) { if (!OutSum.TryGetValue(k, out var a)) OutSum[k] = a = new long[TM + 1]; for (int t = 0; t <= TM; t++) a[t] += v[t]; }
            foreach (var (k, v) in o.OutMax) { if (!OutMax.TryGetValue(k, out var a)) OutMax[k] = a = new long[TM + 1]; for (int t = 0; t <= TM; t++) a[t] = Math.Max(a[t], v[t]); }
            foreach (var (k, v) in o.HitMax) HitMax[k] = Math.Max(HitMax.GetValueOrDefault(k), v);
            foreach (var (k, v) in o.HitSum) HitSum[k] = HitSum.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.HitN) HitN[k] = HitN.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Pics) { if (!Pics.TryGetValue(k, out var x)) Pics[k] = x = new FS.PicAgg(); x.Merge(v); }
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            N++; Turns += r.Turns;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            Fell += r.PlayerStarterFallen.Count;
            foreach (string id in r.PlayerStarterFallen) FellRole[RoleOf(id)]++;
            Death.Take(r, p, e, slot0);
            var ev = r.Events;
            var info = new Dictionary<int, (string Id, int Team)>();
            foreach (var u in p.Concat(e)) info[u.InstanceId] = (u.Def.Id, u.TeamId);
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && !info.ContainsKey(sid))
                    info[sid] = ("summon", x.Team ?? BattleContext.EnemyTeam);
            bool Mine(int id) => info.TryGetValue(id, out var i) && i.Team == BattleContext.PlayerTeam;
            int? hiyo = p.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId;
            for (int t = 1; t <= TM; t++) if (r.Turns >= t) Reached[t]++;

            // 表D（燃えている敵・周回の頭）
            for (int t = 1; t <= TM; t++) { FoeBurn[t] += r.Brittle!.BurnByTurn[0][t]; FoeAlive[t] += r.Brittle.AliveByTurn[0][t]; }

            // 表C（火勢の写し）
            if (r.FireLevels is FireLevelLedger fl)
            {
                var simul = new long[TM + 1];
                var seen = new bool[TM + 1];
                foreach (var s in fl.Snaps)
                {
                    if (s.Team != BattleContext.PlayerTeam || !info.TryGetValue(s.Id, out var ui) || ui.Id == "summon") continue;
                    int ro = RoleOf(ui.Id), t = Math.Min(TM, s.Turn);
                    LvSum[ro, t] += s.Level; LvCnt[ro, t]++; if (s.Level >= 4) { Lv4[ro, t]++; simul[t]++; } if (s.Burning) Burn[ro, t]++;
                    LvHist[ro, s.Level]++;
                    seen[t] = true;
                }
                for (int t = 1; t <= TM; t++) if (seen[t]) { Simul4[t] += simul[t]; SimulCnt[t]++; }
                for (int l = 0; l < 5; l++) Stage[l] += fl.StageHands[l];
                Gifts += fl.Gifts; Recips += fl.GiftRecipients; GiftTurns += fl.GiftTurns; GiftStalled += fl.GiftStalled; GiftHush += fl.GiftHushTurns;
                GiftNoTarget += fl.GiftNoTarget; GiftSkipped += fl.GiftTurnsSkipped;
                Stokes += fl.Stokes; StokeNoTarget += fl.StokeNoTarget; Spreads += fl.SpreadHits; SpreadGrowth += fl.SpreadGrowth; SpreadWasted += fl.SpreadWasted;
                SelfGrowth += fl.SelfGrowth; Wilts += fl.Wilts; Outs += fl.Outs; Lit += fl.Lit; FireKeeps += fl.FireKeeps;
                if (fl.Gifts > 0) GiftBattles++;
                if (fl.FirstGiftTurn is > 0 and <= 2) GiftT2++;
                if (fl.FirstGiftTurn > 0) FirstGift[Math.Min(TM, fl.FirstGiftTurn)]++;
            }
            else
                foreach (var h in r.Hands)   // 燃えている間の熾火（段の札が無い版）は「段1 相当」、燃えていなければ 0（表E の参考）
                    if (info.TryGetValue(h.ActorId, out var hi) && hi.Id == "hota" && hi.Team == BattleContext.PlayerTeam) { }
            if (hiyo is int hy && r.Hands.Any(h => h.ActorId == hy)) HiyoBattles++;

            // 表B（ギフトの相手）・表F（総量）
            var perTurn = new Dictionary<(string Id, int T), long>();
            var giftN = new Dictionary<(int T, int Giver), int>();
            foreach (var x in ev)
            {
                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Gift && x.TargetId is int gt && info.TryGetValue(gt, out var gi))
                {
                    RecipRole[RoleOf(gi.Id)]++;
                    // 相手の火勢は写しでなく台本の直前の値が要る——ギフトの見出しの Amount はヒヨの火勢なので、相手の火勢は次の「ギフトの手番」で読む
                    giftN[(x.Turn, x.ActorId ?? -1)] = Math.Max(giftN.GetValueOrDefault((x.Turn, x.ActorId ?? -1)), x.Slot);
                }
                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.GiftTurn) RecipLevel[Math.Clamp(x.Amount, 0, 4)]++;
                if (x.Kind == BattleEventKind.Damage && x.ActorId is int a && Mine(a) && x.TargetId is int tg && info.TryGetValue(tg, out var ti) && ti.Team != BattleContext.PlayerTeam && !x.Relayed)
                {
                    string id = info[a].Id;
                    perTurn[(id, x.Turn)] = perTurn.GetValueOrDefault((id, x.Turn)) + x.Amount;
                    HitMax[id] = Math.Max(HitMax.GetValueOrDefault(id), x.Amount);
                    HitSum[id] = HitSum.GetValueOrDefault(id) + x.Amount; HitN[id] = HitN.GetValueOrDefault(id) + 1;
                    DealtRole[RoleOf(id)] += x.Amount;
                }
            }
            foreach (var n in giftN.Values) { if (n >= 2) Gifts2++; else Gifts1++; }
            foreach (var ((id, t), v) in perTurn)
            {
                if (t < 1) continue;
                int ti = Math.Min(TM, t);
                if (!OutSum.TryGetValue(id, out var s)) OutSum[id] = s = new long[TM + 1];
                if (!OutMax.TryGetValue(id, out var m)) OutMax[id] = m = new long[TM + 1];
                s[ti] += v; m[ti] = Math.Max(m[ti], v);
                s[0] += v; m[0] = Math.Max(m[0], v);
            }

            // 表G（手番の絵・第239期の定義）
            var seqs = new Dictionary<int, List<(string, string)>>();
            foreach (var h in r.Hands.OrderBy(h => h.EventStart))
            {
                if (!info.TryGetValue(h.ActorId, out var ai) || ai.Team != BattleContext.PlayerTeam) continue;
                string pat = "—";
                var tags = new SortedSet<string>(StringComparer.Ordinal);
                for (int j = h.EventStart; j < h.EventEnd && j < ev.Count; j++)
                {
                    var y = ev[j];
                    if (pat == "—" && y.Kind == BattleEventKind.Attack && y.ActorId == h.ActorId && y.Pattern is not null) pat = FS.PatternName(y.Pattern);
                    string? tg = FS.TagOf(y);
                    if (tg is not null) tags.Add(tg);
                }
                string bone = FS.OutcomeName(h.Outcome) + "／" + pat;
                string pic = bone + "／" + (tags.Count == 0 ? "（なし）" : string.Join("・", tags));
                (seqs.TryGetValue(h.ActorId, out var l) ? l : seqs[h.ActorId] = new()).Add((pic, bone));
            }
            foreach (var (id, seq) in seqs)
            {
                string key = info[id].Id;
                if (!Pics.TryGetValue(key, out var pa)) Pics[key] = pa = new FS.PicAgg { Name = key };
                pa.TakeBattle(seq);
            }
        }
    }

    internal static LAgg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var parts = new LAgg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = BA.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true, ember: EmberRule.Pre256);
            var a = new LAgg();
            a.Take(r, p, e, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
            parts[i] = a;
        });
        var all = new LAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // 台（§5.1）
    // ---------------------------------------------------------------------------------
    internal static readonly string[] BoardNames = { "T3", "T3′", "T3-238", "雷＋ボルグ" };
    internal static Formation BoardOf(string name, Ver v, Dictionary<string, List<Pick>> ranked) => name switch
    {
        "T3" => Apply(Dec(ranked[v.Name][0].Best), v),
        "T3′" => Apply(Dec(ranked[v.Name].First(IsT3P).Best), v),
        "T3-238" => Apply(FS.T3, v),
        "雷＋ボルグ" => Apply(FS.ThunderBorg, v),
        _ => throw new ArgumentException(name),
    };
    internal static readonly (string Name, Func<Formation> F)[] Refs = { ("参考 移動", () => BA.RefMove), ("参考 雷", () => BA.RefThunder) };

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = new Dictionary<string, List<Pick>>();
        foreach (var v in Versions) ranked[v.Name] = Ranked(PicksOf(v, out _));
        Console.WriteLine("# 第242期 段2 —— 火勢を入れる（版 R0〜R3g4）");
        Console.WriteLine();
        Stage1Summary(ranked);
        var cells = new Dictionary<(string B, string V, int W, int S), LAgg>();
        foreach (string bn in BoardNames)
            foreach (var v in Versions)
            {
                var f = BoardOf(bn, v, ranked);
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < BA.Scales.Length; s++)
                        cells[(bn, v.Name, w, s)] = Measure(f, w, BA.Scales[s].Sc);
            }
        foreach (var (rn, rf) in Refs)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    cells[(rn, "R0", w, s)] = Measure(rf(), w, BA.Scales[s].Sc);
        Console.Error.WriteLine($"  段2 の測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Boards(ranked);
        TableA(cells);
        TableB(cells);
        TableC(cells);
        TableD(cells);
        TableE(cells);
        TableF(cells);
        TableG(cells);
        TableH(cells);
        CompareMoves();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static string Named(string enc) => BA.SeatsNamed(Dec(enc));
    static string Sc(Score4 s) => $"{s.S.Sv}/40・400/300 {s.Sv4}/40・落 {s.S.Fell}・勝 {s.S.W}";

    static void Stage1Summary(Dictionary<string, List<Pick>> ranked)
    {
        Console.WriteLine("## 段1（T3 1,081 組 × 席 120 × seed 1000..1039・九/新兵 × 200/200 → 400/300 → 落 → 決着T）");
        Console.WriteLine();
        Console.WriteLine("| 版 | T3 の1位（相方） | 席 | 得点 | T3′ の1位 | 席 | 得点 | 上位10 の相方（シオ・ササを含む組） | 200/200 で 40/40 の組 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|--:|");
        foreach (var v in Versions)
        {
            var rk = ranked[v.Name];
            var b = rk[0]; var bp = rk.First(IsT3P);
            int prime10 = rk.Take(10).Count(p => !IsT3P(p));
            int full = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds);
            Console.WriteLine($"| {v.Name} | {string.Join("・", b.Partners)} | {Named(b.Best)} | {Sc(b.BestS)} | {string.Join("・", bp.Partners)} | {Named(bp.Best)} | {Sc(bp.BestS)} | {prime10} / 10 | {full} |");
        }
        Console.WriteLine();
        Console.WriteLine("上位10 組（版ごと・相方 ／ 得点 ／ ボルグの席）:");
        Console.WriteLine();
        foreach (var v in Versions)
            Console.WriteLine($"- {v.Name}: " + string.Join(" ／ ", ranked[v.Name].Take(10).Select(p => $"{string.Join("・", p.Partners)} {p.BestS.S.Sv}/{p.BestS.Sv4}（ボルグ {BorgSeat(p.Best)}）")));
        Console.WriteLine();
    }
    static string BorgSeat(string enc)
    {
        var ids = enc.Split(',');
        int i = Array.IndexOf(ids, "borg");
        return i < 0 ? "—" : FormationRules.SeatNames[i];
    }

    static void Boards(Dictionary<string, List<Pick>> ranked)
    {
        Console.WriteLine("## 台（版ごとの席）");
        Console.WriteLine();
        foreach (var v in Versions)
            foreach (string bn in BoardNames)
                Console.WriteLine($"- {v.Name} × {bn}: {BA.SeatsNamed(BoardOf(bn, v, ranked))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
    }

    static IEnumerable<(string B, string V)> BV()
    {
        foreach (string bn in BoardNames) foreach (var v in Versions) yield return (bn, v.Name);
        foreach (var (rn, _) in Refs) yield return (rn, "R0");
    }

    static void TableA(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表A —— 全員生存 ／ 勝率 ／ 決着T ／ 落ちた駒（/戦）");
        Console.WriteLine();
        Console.WriteLine("主判定（九/新兵）の3倍率:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 200/200 | 400/300 | 115/115 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (b, v) in BV())
            Console.WriteLine($"| {b} | {v} | " + string.Join(" | ", Enumerable.Range(0, 3).Select(s => ACell(cells[(b, v, BA.MainWave, s)]))) + " |");
        Console.WriteLine();
        Console.WriteLine("ほかの波（全員生存 ／ 勝率 ／ 決着T ／ 落）:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | " + string.Join(" | ", Enumerable.Range(0, BA.WaveNames.Length).Where(w => w != BA.MainWave).Select(w => BA.WaveNames[w])) + " | 本編 第2〜5波の平均（全員生存 ／ 勝率） |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", BA.WaveNames.Length)) + "");
        foreach (var (b, v) in BV())
            for (int s = 0; s < 3; s++)
            {
                var ws = Enumerable.Range(0, 4).Select(w => cells[(b, v, w, s)]).ToList();
                string avg = $"{(ws.Average(a => 100.0 * a.AllSurv / a.N)):F1} ／ {(ws.Average(a => 100.0 * a.Wins / a.N)):F1}";
                Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(0, BA.WaveNames.Length).Where(w => w != BA.MainWave).Select(w => ACell(cells[(b, v, w, s)]))) + $" | {avg} |");
            }
        Console.WriteLine();
    }
    static string ACell(LAgg a) => $"{Pct(a.AllSurv, a.N)} ／ {Pct(a.Wins, a.N)} ／ {Per(a.Turns, a.N)} ／ {Per(a.Fell, a.N)}";

    static readonly string[] FireVers = { "R3", "R3g4" };

    static void TableB(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表B —— ターンギフト（R3 ／ R3g4）");
        Console.WriteLine();
        Console.WriteLine("分母はヒヨが手番を持った戦。「2T まで」＝ 初めてのギフトが周回 1〜2。相手の内訳はギフトの見出しの相手（のべ）。相手の火勢はギフトの手番の頭の値。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | **2T まで %** | 初回 周回1 ／ 2 ／ 3 ／ 4+ % | 出た戦 % | ギフト/戦（1体 ／ 2体） | 相手/戦 | 相手 ボルグ ／ ホタ ／ 相方 % | 相手の火勢 1 ／ 2 ／ 3 ／ 4 % | 動けなかった手番 | 粛の下のギフトの手番/戦 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|---|--:|---|---|--:|--:|");
        foreach (string b in BoardNames)
            foreach (string v in FireVers)
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 0) continue;
                        var a = cells[(b, v, w, s)];
                        long hb = a.HiyoBattles;
                        string first = string.Join(" ／ ", Pct(a.FirstGift[1], hb), Pct(a.FirstGift[2], hb), Pct(a.FirstGift[3], hb), Pct(a.FirstGift.Skip(4).Sum(), hb));
                        long rr = a.RecipRole.Sum(), rl = a.RecipLevel.Sum();
                        string role = $"{Pct(a.RecipRole[0], rr)} ／ {Pct(a.RecipRole[1], rr)} ／ {Pct(a.RecipRole[3], rr)}";
                        string lv = string.Join(" ／ ", Enumerable.Range(1, 4).Select(l => Pct(a.RecipLevel[l], rl)));
                        Console.WriteLine($"| {b} | {v} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | **{Pct(a.GiftT2, hb)}** | {first} | {Pct(a.GiftBattles, hb)} | {Per(a.Gifts, a.N)}（{Per(a.Gifts1, a.N)} ／ {Per(a.Gifts2, a.N)}） | {Per(a.Recips, a.N)} | {role} | {lv} | {Per(a.GiftStalled, a.N)} | {Per(a.GiftHush, a.N)} |");
                    }
        Console.WriteLine();
    }

    static void TableC(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表C —— 火勢の分布（周回の頭・刻みの後）");
        Console.WriteLine();
        Console.WriteLine("九/新兵 × 200/200 と 400/300。役ごとに 周回1〜5 の平均火勢（燃えていない駒は 0）・火勢4 の割合・分布、周回ごとの「同時に火勢4 の味方の数」、ボルグが燃えていた割合。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | 役 | 平均 T1 ／ T2 ／ T3 ／ T4 ／ T5 | 火勢4 % T1〜T5 | 分布 0 ／ 1 ／ 2 ／ 3 ／ 4 % |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (string b in BoardNames)
            foreach (var v in Versions.Skip(1))
                for (int s = 0; s < 2; s++)
                {
                    var a = cells[(b, v.Name, BA.MainWave, s)];
                    for (int r = 0; r < 4; r++)
                    {
                        long hs = Enumerable.Range(0, 5).Sum(l => a.LvHist[r, l]);
                        if (hs == 0) continue;
                        string avg = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Per(a.LvSum[r, t], a.LvCnt[r, t])));
                        string p4 = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Pct(a.Lv4[r, t], a.LvCnt[r, t])));
                        string hist = string.Join(" ／ ", Enumerable.Range(0, 5).Select(l => Pct(a.LvHist[r, l], hs)));
                        Console.WriteLine($"| {b} | {v.Name} | {BA.Scales[s].Name} | {RoleNames[r]} | {avg} | {p4} | {hist} |");
                    }
                }
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | 同時に火勢4 の味方 T1 ／ T2 ／ T3 ／ T4 ／ T5 | ボルグが燃えていた % T1 ／ T2 ／ T3 ／ T4 ／ T5 | 萎む/戦 | 消える/戦 | 燃え広がり（相手）/戦 | うち捨てた | 煽り/戦 | ヒヨ自身の育ち/戦 |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (string b in BoardNames)
            foreach (var v in Versions)
                for (int s = 0; s < 2; s++)
                {
                    var a = cells[(b, v.Name, BA.MainWave, s)];
                    string sim = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Per(a.Simul4[t], a.SimulCnt[t])));
                    string burn = v.Name == "R0" ? "—" : string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Pct(a.Burn[0, t], a.LvCnt[0, t])));
                    Console.WriteLine($"| {b} | {v.Name} | {BA.Scales[s].Name} | {sim} | {burn} | {Per(a.Wilts, a.N)} | {Per(a.Outs, a.N)} | {Per(a.Spreads, a.N)} | {Per(a.SpreadWasted, a.N)} | {Per(a.Stokes, a.N)} | {Per(a.SelfGrowth, a.N)} |");
                }
        Console.WriteLine();
    }

    static void TableD(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表D —— 燃えている敵（周回の頭・刻みの前）／ 生きている敵");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | T1 | T2 | T3 | T4 | T5 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var (b, v) in BV())
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < 3; s++)
                {
                    if (w != BA.MainWave && s != 0) continue;
                    var a = cells[(b, v, w, s)];
                    Console.WriteLine($"| {b} | {v} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(1, 5).Select(t => $"{Per(a.FoeBurn[t], a.N)} ／ {Per(a.FoeAlive[t], a.N)}")) + " |");
                }
        Console.WriteLine();
    }

    static void TableE(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表E —— ホタの段ごとの手番（R2 以降・手番の頭の火勢）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 手番/戦 | 段0（燃えていない）／ 1 ／ 2 ／ 3 ／ 4 % | 段3 以上 % |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|");
        foreach (string b in new[] { "T3", "T3′", "T3-238" })
            foreach (var v in Versions.Skip(2))
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 0) continue;
                        var a = cells[(b, v.Name, w, s)];
                        long n = a.Stage.Sum();
                        if (n == 0) continue;
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Per(n, a.N)} | " + string.Join(" ／ ", a.Stage.Select(x => Pct(x, n))) + $" | {Pct(a.Stage[3] + a.Stage[4], n)} |");
                    }
        Console.WriteLine();
    }

    static void TableF(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表F —— 1発の最大 ／ 1ターンの総量（敵への `Damage`・中継を除く）");
        Console.WriteLine();
        Console.WriteLine("1ターンの総量は、その周回まで戦が続いた戦の平均（0 を含む）と、全戦の最大。九/新兵。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | 駒 | 1発 最大 ／ 平均 | 1Tの総量 平均 T1 ／ T2 ／ T3 ／ T4 ／ T5 | 1Tの総量の最大 | 1戦の与ダメ |");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|");
        var rows = new List<(string B, string V, string[] Ids)>();
        foreach (string b in new[] { "T3", "T3-238" }) foreach (var v in Versions) rows.Add((b, v.Name, new[] { "hota", "borg" }));
        rows.Add(("参考 移動", "R0", new[] { "yomi", "sero" }));
        rows.Add(("参考 雷", "R0", new[] { "kata", "shiga" }));
        foreach (var (b, v, ids) in rows)
            for (int s = 0; s < 3; s++)
            {
                var a = cells[(b, v, BA.MainWave, s)];
                foreach (string id in ids)
                {
                    if (!a.HitN.ContainsKey(id)) continue;
                    var sum = a.OutSum[id]; var mx = a.OutMax[id];
                    string turns = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Per(sum[t], a.Reached[t])));
                    Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | {id} | {a.HitMax[id]} ／ {Per(a.HitSum[id], a.HitN[id])} | {turns} | {mx[0]} | {Per(sum[0], a.N)} |");
                }
            }
        Console.WriteLine();
    }

    static void TableG(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表G —— 手番の絵の単調さ（第239期の定義・九/新兵）");
        Console.WriteLine();
        Console.WriteLine("最多の絵 % ＝ 戦ごとに「最も多い絵の手番数」の和 ÷ 手番の和。続けて同じ ＝ 隣り合う2手番が同じ絵。骨格 ＝ 種類／攻撃型。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | 駒 | 手番/戦 | 絵の種類/戦 | 最多の絵 % | 続けて同じ % | 骨格の最多 % | 骨格が続けて同じ % | 多い絵（上位3） |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|---|");
        var rows = new List<(string B, string V, string[] Ids)>();
        foreach (string b in new[] { "T3", "T3-238" }) foreach (var v in Versions) rows.Add((b, v.Name, new[] { "borg", "hota", "hiyo" }));
        rows.Add(("参考 移動", "R0", new[] { "yomi", "sero", "basa", "hane", "shio" }));
        rows.Add(("参考 雷", "R0", new[] { "kata", "shiga", "beni", "mio" }));
        foreach (var (b, v, ids) in rows)
            for (int s = 0; s < 2; s++)
            {
                var a = cells[(b, v, BA.MainWave, s)];
                foreach (string id in ids)
                {
                    if (!a.Pics.TryGetValue(id, out var pa) || pa.Hands == 0) continue;
                    var top = pa.Freq.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {Pct(kv.Value, pa.Hands)}");
                    Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | {id} | {Per(pa.Hands, pa.Battles)} | {Per(pa.DistinctSum, pa.Battles)} | {Pct(pa.TopSum, pa.Hands)} | {Pct(pa.Repeats, pa.Pairs)} | {Pct(pa.BoneTopSum, pa.Hands)} | {Pct(pa.BoneRepeats, pa.Pairs)} | {string.Join(" ／ ", top)} |");
                }
            }
        Console.WriteLine();
    }

    static void TableH(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表H —— 粛の波（本編 第二波）・軛の波（本編 第四波）での3体");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 全員生存 ／ 勝率 ／ 決着T | 落ちた % ボルグ ／ ホタ ／ ヒヨ | 与ダメ/戦 ボルグ ／ ホタ ／ 相方 | ギフトの手番/戦（うち粛の下） | 2T まで % |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|--:|");
        foreach (string b in BoardNames)
            foreach (var v in Versions)
                foreach (int w in new[] { WaveHush, WaveYoke })
                    for (int s = 0; s < 3; s++)
                    {
                        var a = cells[(b, v.Name, w, s)];
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Pct(a.AllSurv, a.N)} ／ {Pct(a.Wins, a.N)} ／ {Per(a.Turns, a.N)} | "
                            + $"{Pct(a.FellRole[0], a.N)} ／ {Pct(a.FellRole[1], a.N)} ／ {Pct(a.FellRole[2], a.N)} | {Per(a.DealtRole[0], a.N)} ／ {Per(a.DealtRole[1], a.N)} ／ {Per(a.DealtRole[3], a.N)} | "
                            + $"{Per(a.GiftTurns, a.N)}（{Per(a.GiftHush, a.N)}） | {(a.HiyoBattles == 0 || a.Gifts == 0 && v.Name is not ("R3" or "R3g4") ? "—" : Pct(a.GiftT2, a.HiyoBattles))} |");
                    }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // `compare` 61 行（受け入れ 1・2）: 版ごとにボルグ・ホタ・ヒヨを差し替え、第1〜5波 × seed 0..199 の勝率を R0 と比べる。
    // ---------------------------------------------------------------------------------
    internal static double[,,] CompareRates()
    {
        var rows = CompareBuilds();
        var w = new double[Versions.Length, rows.Length, 5];
        for (int v = 0; v < Versions.Length; v++)
            for (int i = 0; i < rows.Length; i++)
            {
                var f = Apply(rows[i].F, Versions[v]);
                for (int st = 0; st < 5; st++)
                {
                    int wins = 0;
                    var stage = EnemyCatalog.Stages[st].Enemy;
                    var res = new bool[BA.Seeds];
                    Parallel.For(0, BA.Seeds, seed => res[seed] = BattleEngine.Run(f, stage, seed, verbose: false, ember: EmberRule.Pre256).PlayerWon);
                    wins = res.Count(x => x);
                    w[v, i, st] = 100.0 * wins / BA.Seeds;
                }
            }
        return w;
    }

    static void CompareMoves()
    {
        Console.WriteLine("## `compare` 61 行 × 第1〜5波 × seed 0..199（受け入れ 1・2）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var w = CompareRates();
        bool Has(Formation f) => f.Occupied().Any(o => o.Def.Id is "borg" or "hota" or "hiyo");
        Console.WriteLine("| 版 | 動いたセル | うち3枚のいない行 | 動いた行 | 上がったセル ／ 下がったセル | 全61行の第2〜5波の平均 |");
        Console.WriteLine("|---|--:|--:|--:|---|---|");
        for (int v = 0; v < Versions.Length; v++)
        {
            int moved = 0, movedNo = 0, rowsMoved = 0, up = 0, down = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                bool rm = false;
                for (int st = 0; st < 5; st++)
                {
                    double d = w[v, i, st] - w[0, i, st];
                    if (d == 0) continue;
                    moved++; rm = true; if (d > 0) up++; else down++;
                    if (!Has(rows[i].F)) movedNo++;
                }
                if (rm) rowsMoved++;
            }
            string avg = string.Join(" ／ ", Enumerable.Range(1, 4).Select(st => Enumerable.Range(0, rows.Length).Average(i => w[v, i, st]).ToString("F1")));
            Console.WriteLine($"| {Versions[v].Name} | {moved} | {movedNo} | {rowsMoved} | {up} ／ {down} | {avg} |");
        }
        Console.WriteLine();
        Console.WriteLine("動いた行（第1〜5波・R0 → R1 / R2 / R3 / R3g4）:");
        Console.WriteLine();
        for (int i = 0; i < rows.Length; i++)
        {
            bool any = false;
            for (int v = 1; v < Versions.Length; v++) for (int st = 0; st < 5; st++) if (w[v, i, st] != w[0, i, st]) any = true;
            if (!any) continue;
            Console.WriteLine($"- {rows[i].Name}: " + string.Join(" ／ ", Enumerable.Range(0, 5).Select(st =>
                $"{w[0, i, st]:F1}→" + string.Join("/", Enumerable.Range(1, Versions.Length - 1).Select(v => w[v, i, st].ToString("F1"))))));
        }
        Console.WriteLine();
    }
}
