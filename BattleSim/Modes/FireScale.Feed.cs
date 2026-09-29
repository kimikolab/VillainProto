using System.Text.RegularExpressions;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// =====================================================================================
// firescale feed* —— 第240期「火勢の影の計算 その2（保つ火と育てる火を分ける）」。
// 指示書は design/PHASE240_FIRE_FEED_SPEC.md ／ 報告は design/PHASE240_FIRE_FEED.md。
// **盤面は変えない**。第239期と同じく、戦闘の後に台本（`Events`）と手番の枠（`Hands`）を読んで影を数えるだけ。
//
// 1戦を一度だけ読んで「足跡」（周回の頭・着火・手番の頭・切った量・自火・倒れた）にし、規則の組ごとに足跡を再生する。
//
//     dotnet run --project BattleSim -c Release 0 firescale feed0     # Phase 0（規則を入れる前の材料）
//     dotnet run --project BattleSim -c Release 0 firescale feed      # 段1（T3・T3-1 × 九/新兵 × 倍率3 × 全72組）→ 段2（選んだ組 × 全台・全波・倍率）
//     dotnet run --project BattleSim -c Release 0 firescale feedcheck # 自己検査
// =====================================================================================
static partial class FireScaleDiag
{
    // ---------------------------------------------------------------------------------
    // 規則の組（§2）
    // ---------------------------------------------------------------------------------
    internal readonly record struct FeedRule(int W, int Y, int Heat, bool BPlus)
    {
        public string Name => $"W{W}・Y{Y}・{Heat}・{(BPlus ? "B+" : "B0")}";
    }
    internal static readonly int[] HeatSteps = { 20, 30, 50 };
    internal static readonly FeedRule[] AllRules =
        (from w in new[] { 0, 1, 2 } from y in new[] { 1, 2, 3, 4 } from h in HeatSteps from b in new[] { false, true } select new FeedRule(w, y, h, b)).ToArray();
    static readonly string[] YNames = { "", "Y1 集中", "Y2 均し", "Y3 ホタ優先", "Y4 火力" };

    // ---------------------------------------------------------------------------------
    // 足跡（1戦を一度だけ読む）
    // ---------------------------------------------------------------------------------
    internal enum StepK { Round, Ignite, Hand, Heat, SelfLit, Death, Alive, Smolder }
    internal readonly record struct Step(StepK K, int A, int B);   // Round: A=周回 ／ Ignite: A=駒 ／ Hand: A=駒, B=手番の番号 ／ Heat: A=ボルグ, B=量 ／ SelfLit: A=ボルグ ／ Death/Alive/Smolder: A=駒

    internal sealed class Trace
    {
        public bool Won, AllSurv; public int Turns;
        public readonly List<Step> Steps = new();
        public readonly Dictionary<int, HashSet<int>> BurnAt = new();          // 周回 → 頭で燃えている駒
        public readonly Dictionary<(int T, int Id), int> AtkAt = new();       // 周回の頭の攻撃力（Y4）
        public readonly Dictionary<int, (string Id, string Name, int Team, int Slot)> Info = new();
        public readonly List<(int Actor, string Pic, string Bone)> HandPics = new();   // 手番の番号 → 第239期の絵
        public readonly HashSet<int> Alive0 = new();
        public long HeatGuard, HeatWard;   // Phase 0 の材料
    }

    internal static Trace BuildTrace(BattleResult r, List<UnitState> p, List<UnitState> e)
    {
        var tr = new Trace { Won = r.PlayerWon, AllSurv = r.PlayerWon && r.PlayerStarterFallen.Count == 0, Turns = r.Turns };
        var ev = r.Events;
        foreach (var u in p.Concat(e)) tr.Info[u.InstanceId] = (u.Def.Id, u.Def.Name, u.TeamId, u.Slot);
        foreach (var x in ev)
            if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && !tr.Info.ContainsKey(sid))
                tr.Info[sid] = ("summon", "召喚", x.Team ?? (x.ActorId is int aid && tr.Info.TryGetValue(aid, out var ai) ? ai.Team : BattleContext.EnemyTeam), 99);
        foreach (var u in p.Concat(e)) tr.Alive0.Add(u.InstanceId);
        foreach (var x in ev)
        {
            if (x.Kind == BattleEventKind.StatusSnapshot && x.Text == BurnLabel && x.TargetId is int bt)
                (tr.BurnAt.TryGetValue(x.Turn, out var set) ? set : tr.BurnAt[x.Turn] = new HashSet<int>()).Add(bt);
            if (x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int st) tr.AtkAt[(x.Turn, st)] = x.Amount;
        }
        var hands = r.Hands.OrderBy(h => h.EventStart).ThenByDescending(h => h.EventEnd).ToList();
        // 第239期の絵（手番ごと）
        foreach (var h in hands)
        {
            string pat = "—";
            var tags = new SortedSet<string>(StringComparer.Ordinal);
            for (int j = h.EventStart; j < h.EventEnd && j < ev.Count; j++)
            {
                var y = ev[j];
                if (pat == "—" && y.Kind == BattleEventKind.Attack && y.ActorId == h.ActorId && y.Pattern is not null) pat = PatternName(y.Pattern);
                string? t = TagOf(y);
                if (t is not null) tags.Add(t);
            }
            string bone = OutcomeName(h.Outcome) + "／" + pat;
            tr.HandPics.Add((h.ActorId, bone + "／" + (tags.Count == 0 ? "（なし）" : string.Join("・", tags)), bone));
        }
        var handsAt = new Dictionary<int, List<int>>();
        for (int k = 0; k < hands.Count; k++) (handsAt.TryGetValue(hands[k].EventStart, out var l) ? l : handsAt[hands[k].EventStart] = new()).Add(k);
        for (int i = 0; i <= ev.Count; i++)
        {
            if (handsAt.TryGetValue(i, out var hs))
                foreach (int k in hs) tr.Steps.Add(new Step(StepK.Hand, hands[k].ActorId, k));
            if (i == ev.Count) break;
            var x = ev[i];
            switch (x.Kind)
            {
                case BattleEventKind.TurnStart: tr.Steps.Add(new Step(StepK.Round, x.Turn, 0)); break;
                case BattleEventKind.StatusGain when x.Text == StatusKeys.Burn && x.TargetId is int tid: tr.Steps.Add(new Step(StepK.Ignite, tid, 0)); break;
                case BattleEventKind.FireArmor when x.Text == FireArmorLabels.Guard && x.ActorId is int ga:
                    tr.Steps.Add(new Step(StepK.Heat, ga, x.Amount)); tr.HeatGuard += x.Amount; break;
                case BattleEventKind.FireArmor when x.Text == FireArmorLabels.Ward && x.ActorId is int wa:
                    tr.Steps.Add(new Step(StepK.Heat, wa, x.Amount)); tr.HeatWard += x.Amount; break;
                case BattleEventKind.FireArmor when x.Text == FireArmorLabels.Self && x.ActorId is int sa: tr.Steps.Add(new Step(StepK.SelfLit, sa, 0)); break;
                case BattleEventKind.FireArmor when x.Text == FireArmorLabels.Smolder && x.TargetId is int sm: tr.Steps.Add(new Step(StepK.Smolder, sm, 0)); break;
                case BattleEventKind.Death when x.TargetId is int did: tr.Steps.Add(new Step(StepK.Death, did, 0)); break;
                case BattleEventKind.Revive when x.TargetId is int vid: tr.Steps.Add(new Step(StepK.Alive, vid, 0)); break;
                case BattleEventKind.Summon when x.TargetId is int s2: tr.Steps.Add(new Step(StepK.Alive, s2, 0)); break;
            }
        }
        return tr;
    }

    // ---------------------------------------------------------------------------------
    // 影の火勢（第240期）: 1つの規則の組で1戦を再生する
    // ---------------------------------------------------------------------------------
    /// <summary>味方の影（保つ・育てる・萎む）。盤の外でも単体で検査できるように分けてある。</summary>
    internal sealed class Feeder
    {
        public readonly FeedRule R;
        public readonly Dictionary<int, int> S = new(), Idle = new(), HeatAcc = new();
        public readonly HashSet<int> Grew = new();
        public Feeder(FeedRule r) { R = r; }
        public int Of(int id) => S.GetValueOrDefault(id);
        /// <summary>保つ: 0 なら 1。1 以上なら上げない。</summary>
        public void Keep(int id) { if (Of(id) == 0) S[id] = 1; }
        /// <summary>育てる: +1（上限 4）。戻り値 0 ＝ 育った ／ 1 ＝ 4 だったので余熱 ／ 2 ＝ 燃えていない（0）ので捨てた。</summary>
        public int Grow(int id)
        {
            int s = Of(id);
            if (s == 0) return 2;
            Grew.Add(id);
            if (s >= Max) return 1;
            S[id] = s + 1; return 0;
        }
        /// <summary>ボルグの余熱: 切った量を溜め、閾値ごとに育てる。戻り値は (育った, 余熱, 捨てた)。</summary>
        public (int G, int O, int X) Heat(int id, int amount)
        {
            int acc = HeatAcc.GetValueOrDefault(id) + amount, g = 0, o = 0, x = 0;
            while (acc >= R.Heat) { acc -= R.Heat; switch (Grow(id)) { case 0: g++; break; case 1: o++; break; default: x++; break; } }
            HeatAcc[id] = acc;
            return (g, o, x);
        }
        /// <summary>周回の終わり: 萎む（燃えている間は 1 未満にしない）。</summary>
        public void Wither(IEnumerable<int> ids)
        {
            foreach (int id in ids)
            {
                int s = Of(id);
                if (Grew.Contains(id)) { Idle[id] = 0; continue; }
                if (s <= 1) { Idle[id] = 0; continue; }
                if (R.W == 1) S[id] = s - 1;
                else if (R.W == 2)
                {
                    int k = Idle.GetValueOrDefault(id) + 1;
                    if (k >= 2) { S[id] = s - 1; k = 0; }
                    Idle[id] = k;
                }
            }
            Grew.Clear();
        }
        public void Out(int id) { S.Remove(id); Idle.Remove(id); }
    }

    /// <summary>集計（規則の組 × 条件）。</summary>
    internal sealed class FAgg
    {
        public long N, Wins, AllSurv, Turns;
        public readonly long[] SSum = new long[5], SCnt = new long[5], Eq4 = new long[5], UnitN = new long[5], Reach4 = new long[5], Reach4T = new long[5];
        public readonly long[,] Hist = new long[5, Max + 1];
        // 時計かどうか（燃えている味方・周回の終わり）: 相関の和（x ＝ 周回 ／ y ＝ 影）と一致（影 ＝ min(周回, 4)）
        public readonly double[,] Corr = new double[5, 6];   // n, Σx, Σy, Σxy, Σx², Σy²
        public readonly double[,] CorrK1 = new double[5, 6]; // 第239期の K1（同じ標本）
        public readonly long[] ClockN = new long[5], ClockHit = new long[5], ClockHitK1 = new long[5], ClockN2 = new long[5], ClockHit2 = new long[5], ClockHitK12 = new long[5];
        public long BattleReach4;   // 味方のだれかが影 4 に届いた戦
        public readonly double[] BMeanSum = new double[5], BMeanSq = new double[5]; public readonly long[] BMeanN = new long[5];   // 戦ごとの平均の散らばり
        public readonly long[] Simul4 = new long[5];   // 周回の終わりに影 4 の味方の数 0 / 1 / 2 / 3 / 4+
        public long RoundsP;
        // ヒヨ
        public long HiyoHands, Feeds, FeedOver, Gifts, NoTarget;
        public readonly long[] FeedTo = new long[4], GiftTo = new long[4];
        public long FeedTargetsDistinctSum, HiyoBattles;
        // ボルグ
        public long Heat, HeatG, HeatO, HeatX, SelfG, SelfO, Releases, RelBattles, RelFirstT, BorgHands, BorgBattles;
        // ホタ
        public readonly long[] HotaStage = new long[Max + 1]; public long HotaHands, HotaRunSum, HotaRunMax, HotaBattles, HotaDistinctSum;
        // 余熱（4 への育ち）役ごと
        public readonly long[] Over = new long[5];
        public readonly Dictionary<string, PicAgg> Pics = new();

        public void Merge(FAgg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; Turns += o.Turns; RoundsP += o.RoundsP; BattleReach4 += o.BattleReach4;
            for (int i = 0; i < 5; i++)
            {
                SSum[i] += o.SSum[i]; SCnt[i] += o.SCnt[i]; Eq4[i] += o.Eq4[i]; UnitN[i] += o.UnitN[i]; Reach4[i] += o.Reach4[i]; Reach4T[i] += o.Reach4T[i];
                for (int j = 0; j <= Max; j++) Hist[i, j] += o.Hist[i, j];
                for (int j = 0; j < 6; j++) { Corr[i, j] += o.Corr[i, j]; CorrK1[i, j] += o.CorrK1[i, j]; }
                ClockN[i] += o.ClockN[i]; ClockHit[i] += o.ClockHit[i]; ClockHitK1[i] += o.ClockHitK1[i]; ClockN2[i] += o.ClockN2[i]; ClockHit2[i] += o.ClockHit2[i]; ClockHitK12[i] += o.ClockHitK12[i];
                BMeanSum[i] += o.BMeanSum[i]; BMeanSq[i] += o.BMeanSq[i]; BMeanN[i] += o.BMeanN[i];
                Simul4[i] += o.Simul4[i]; Over[i] += o.Over[i];
            }
            HiyoHands += o.HiyoHands; Feeds += o.Feeds; FeedOver += o.FeedOver; Gifts += o.Gifts; NoTarget += o.NoTarget;
            for (int i = 0; i < 4; i++) { FeedTo[i] += o.FeedTo[i]; GiftTo[i] += o.GiftTo[i]; }
            FeedTargetsDistinctSum += o.FeedTargetsDistinctSum; HiyoBattles += o.HiyoBattles;
            Heat += o.Heat; HeatG += o.HeatG; HeatO += o.HeatO; HeatX += o.HeatX; SelfG += o.SelfG; SelfO += o.SelfO;
            Releases += o.Releases; RelBattles += o.RelBattles; RelFirstT += o.RelFirstT; BorgHands += o.BorgHands; BorgBattles += o.BorgBattles;
            for (int j = 0; j <= Max; j++) HotaStage[j] += o.HotaStage[j];
            HotaHands += o.HotaHands; HotaRunSum += o.HotaRunSum; HotaRunMax = Math.Max(HotaRunMax, o.HotaRunMax); HotaBattles += o.HotaBattles; HotaDistinctSum += o.HotaDistinctSum;
            foreach (var (k, v) in o.Pics) { if (!Pics.TryGetValue(k, out var x)) Pics[k] = x = new PicAgg(); x.Merge(v); }
        }
    }

    static void AddCorr(double[,] c, int ro, double x, double y) { c[ro, 0]++; c[ro, 1] += x; c[ro, 2] += y; c[ro, 3] += x * y; c[ro, 4] += x * x; c[ro, 5] += y * y; }
    internal static double CorrOf(double[,] c, int ro)
    {
        double n = c[ro, 0]; if (n < 2) return double.NaN;
        double vx = c[ro, 4] - c[ro, 1] * c[ro, 1] / n, vy = c[ro, 5] - c[ro, 2] * c[ro, 2] / n;
        if (vx <= 0 || vy <= 0) return double.NaN;
        return (c[ro, 3] - c[ro, 1] * c[ro, 2] / n) / Math.Sqrt(vx * vy);
    }
    static double[,] SumCorr(double[,] c, int[] roles)
    {
        var o = new double[1, 6];
        foreach (int ro in roles) for (int j = 0; j < 6; j++) o[0, j] += c[ro, j];
        return o;
    }

    /// <summary>規則の組 <paramref name="rule"/> で1戦（足跡）を再生して <paramref name="acc"/> に足す。<paramref name="pics"/> なら絵も数える。</summary>
    internal static void Replay(Trace tr, FeedRule rule, FAgg acc, bool pics)
    {
        acc.N++; acc.Turns += tr.Turns; if (tr.Won) acc.Wins++; if (tr.AllSurv) acc.AllSurv++;
        var info = tr.Info;
        bool Mine(int id) => info.TryGetValue(id, out var i) && i.Team == BattleContext.PlayerTeam;
        int Role(int id) => info.TryGetValue(id, out var i) ? RoleIx(i.Id, i.Team) : 4;
        string IdOf(int id) => info.TryGetValue(id, out var i) ? i.Id : "";
        int SlotOf(int id) => info.TryGetValue(id, out var i) ? i.Slot : 99;
        var alive = new HashSet<int>(tr.Alive0);
        var burning = new HashSet<int>();
        var f = new Feeder(rule);
        var k1 = new Shadow(0);   // 第239期 K1（敵の影と、味方の比較用）
        var reach4 = new Dictionary<int, int>();
        int round = 0, relCount = 0, relFirst = 0; bool borgSeen = false, hiyoSeen = false;
        var feedTargets = new HashSet<int>();
        var hotaStages = new List<int>();
        var bSum = new double[5]; var bN = new long[5];
        var seqs = pics ? new Dictionary<int, List<(string, string)>>() : null;

        void Finalize(int t)
        {
            if (t <= 0) return;
            int n4 = 0;
            foreach (int id in alive)
            {
                int ro = Role(id);
                int s = ro == 4 ? k1.Of(id) : f.Of(id);
                acc.SSum[ro] += s; acc.SCnt[ro]++; acc.Hist[ro, s]++;
                if (s >= Max) { acc.Eq4[ro]++; if (!reach4.ContainsKey(id)) reach4[id] = t; }
                if (ro != 4)
                {
                    if (s >= Max) n4++;
                    bSum[ro] += s; bN[ro]++;
                    if (burning.Contains(id))
                    {
                        AddCorr(acc.Corr, ro, t, s); AddCorr(acc.CorrK1, ro, t, k1.Of(id));
                        acc.ClockN[ro]++;
                        if (s == Math.Min(t, Max)) acc.ClockHit[ro]++;
                        if (k1.Of(id) == Math.Min(t, Max)) acc.ClockHitK1[ro]++;
                        if (t >= 2)
                        {
                            acc.ClockN2[ro]++;
                            if (s == Math.Min(t, Max)) acc.ClockHit2[ro]++;
                            if (k1.Of(id) == Math.Min(t, Max)) acc.ClockHitK12[ro]++;
                        }
                    }
                }
            }
            acc.Simul4[Math.Min(4, n4)]++; acc.RoundsP++;
            f.Wither(alive.Where(Mine).ToList());
            k1.Decay(alive);
        }
        void StartRound(int t)
        {
            var b0 = tr.BurnAt.GetValueOrDefault(t) ?? new HashSet<int>();
            burning.Clear();
            foreach (int id in alive) if (b0.Contains(id)) burning.Add(id);
            // 消える: 燃焼が切れた味方は 0
            foreach (int id in alive.Where(Mine).ToList()) if (!burning.Contains(id) && f.Of(id) > 0) f.S[id] = 0;
        }
        foreach (var st in tr.Steps)
        {
            switch (st.K)
            {
                case StepK.Round:
                    Finalize(round); round = st.A; StartRound(round); break;
                case StepK.Ignite:
                    burning.Add(st.A);
                    if (Mine(st.A)) f.Keep(st.A);
                    k1.Ignite(st.A);
                    break;
                case StepK.SelfLit:
                    if (rule.BPlus && f.Of(st.A) > 0)
                    {
                        int r = f.Grow(st.A);
                        if (r == 0) acc.SelfG++; else if (r == 1) { acc.SelfO++; acc.Over[Role(st.A)]++; }
                    }
                    break;
                case StepK.Heat:
                {
                    acc.Heat += st.B;
                    var (g, o, x) = f.Heat(st.A, st.B);
                    acc.HeatG += g; acc.HeatO += o; acc.HeatX += x; acc.Over[Role(st.A)] += o;
                    break;
                }
                case StepK.Smolder:
                    burning.Remove(st.A); if (Mine(st.A)) f.S[st.A] = 0; break;
                case StepK.Death:
                    alive.Remove(st.A); burning.Remove(st.A); f.Out(st.A); k1.Kill(st.A); break;
                case StepK.Alive:
                    alive.Add(st.A); break;
                case StepK.Hand:
                {
                    int id = st.A;
                    if (!Mine(id)) break;
                    string me = IdOf(id);
                    string shadowTag = "", shadowBone = "";
                    if (me == "hota")
                    {
                        int s = f.Of(id);
                        acc.HotaStage[s]++; acc.HotaHands++; hotaStages.Add(s);
                        shadowTag = shadowBone = $"段{s}";
                    }
                    else if (me == "borg")
                    {
                        borgSeen = true; acc.BorgHands++;
                        if (f.Of(id) >= Max)
                        {
                            relCount++; if (relFirst == 0) relFirst = round;
                            f.S[id] = 1;
                            shadowTag = shadowBone = "放つ";
                        }
                        else shadowTag = shadowBone = "通常";
                    }
                    else if (me == "hiyo")
                    {
                        hiyoSeen = true; acc.HiyoHands++;
                        var cands = alive.Where(a => a != id && Mine(a) && burning.Contains(a) && f.Of(a) > 0).ToList();
                        int Pri(int a) => IdOf(a) switch { "hota" => 0, "borg" => 1, _ => 2 };
                        IEnumerable<int> Order(IEnumerable<int> xs) => rule.Y switch
                        {
                            1 => xs.OrderByDescending(a => f.Of(a)).ThenBy(SlotOf),
                            2 => xs.OrderBy(a => f.Of(a)).ThenBy(SlotOf),
                            3 => xs.OrderBy(Pri).ThenBy(SlotOf),
                            _ => xs.OrderByDescending(a => tr.AtkAt.GetValueOrDefault((round, a))).ThenBy(SlotOf),
                        };
                        var fours = cands.Where(a => f.Of(a) >= Max).ToList();
                        if (fours.Count > 0)
                        {
                            int to = Order(fours).First();
                            acc.Gifts++; acc.GiftTo[Math.Min(3, Role(to))]++;
                            shadowTag = "ギフト→" + Roles[Role(to)]; shadowBone = "ギフト";
                        }
                        else
                        {
                            var pool = rule.Y == 1 ? cands.Where(a => f.Of(a) < Max) : cands;
                            int to = Order(pool).DefaultIfEmpty(-1).First();
                            if (to < 0) { acc.NoTarget++; shadowTag = shadowBone = "煽りなし"; }
                            else
                            {
                                acc.Feeds++; acc.FeedTo[Math.Min(3, Role(to))]++; feedTargets.Add(to);
                                if (f.Grow(to) == 1) { acc.FeedOver++; acc.Over[Role(to)]++; }
                                shadowTag = "煽り→" + Roles[Role(to)]; shadowBone = "煽り";
                            }
                        }
                    }
                    if (seqs is not null)
                    {
                        var (_, pic, bone) = tr.HandPics[st.B];
                        string p2 = shadowTag == "" ? pic : pic + "／" + shadowTag, b2 = shadowBone == "" ? bone : bone + "／" + shadowBone;
                        (seqs.TryGetValue(id, out var l) ? l : seqs[id] = new()).Add((p2, b2));
                    }
                    break;
                }
            }
        }
        Finalize(round);

        if (reach4.Keys.Any(Mine)) acc.BattleReach4++;
        foreach (var (id, i2) in info)
        {
            if (i2.Id == "summon") continue;
            int ro = RoleIx(i2.Id, i2.Team);
            acc.UnitN[ro]++;
            if (reach4.TryGetValue(id, out int t4)) { acc.Reach4[ro]++; acc.Reach4T[ro] += t4; }
        }
        for (int ro = 0; ro < 4; ro++)
            if (bN[ro] > 0) { double m = bSum[ro] / bN[ro]; acc.BMeanSum[ro] += m; acc.BMeanSq[ro] += m * m; acc.BMeanN[ro]++; }
        if (borgSeen) { acc.BorgBattles++; acc.Releases += relCount; if (relCount > 0) { acc.RelBattles++; acc.RelFirstT += relFirst; } }
        if (hiyoSeen) { acc.HiyoBattles++; acc.FeedTargetsDistinctSum += feedTargets.Count; }
        if (hotaStages.Count > 0)
        {
            acc.HotaBattles++;
            int run = 1, best = 1;
            for (int i = 1; i < hotaStages.Count; i++) { run = hotaStages[i] == hotaStages[i - 1] ? run + 1 : 1; best = Math.Max(best, run); }
            acc.HotaRunSum += best; acc.HotaRunMax = Math.Max(acc.HotaRunMax, best); acc.HotaDistinctSum += hotaStages.Distinct().Count();
        }
        if (seqs is not null)
            foreach (var (id, seq) in seqs)
            {
                var ai = info[id];
                if (!acc.Pics.TryGetValue(ai.Id, out var pa)) acc.Pics[ai.Id] = pa = new PicAgg { Name = ai.Name };
                pa.TakeBattle(seq);
            }
    }

    // ---------------------------------------------------------------------------------
    // 測定の土台
    // ---------------------------------------------------------------------------------
    /// <summary>1つの条件（台 × 波 × 倍率）の足跡を seed 0..199 で（並列）。</summary>
    internal static Trace[] Traces(Formation f, int w, EnemyScaleRule sc, int seeds = BA.Seeds)
    {
        var res = new Trace[seeds];
        Parallel.For(0, seeds, i => { var (r, p, e) = Fight(f, w, sc, i); res[i] = BuildTrace(r, p, e); });
        return res;
    }

    internal static FAgg[] ReplayAll(Trace[] trs, FeedRule[] rules, bool pics)
    {
        var res = new FAgg[rules.Length];
        Parallel.For(0, rules.Length, k =>
        {
            var a = new FAgg();
            foreach (var t in trs) Replay(t, rules[k], a, pics);
            res[k] = a;
        });
        return res;
    }

    static string R2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");
    static string Sd(FAgg a, int ro)
    {
        long n = a.BMeanN[ro]; if (n < 2) return "—";
        double m = a.BMeanSum[ro] / n; return Math.Sqrt(Math.Max(0, a.BMeanSq[ro] / n - m * m)).ToString("F2");
    }

    // =================================================================================
    // Phase 0（規則を入れる前の材料）
    // =================================================================================
    static void Feed0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第240期 Phase 0 `firescale feed0` —— 規則を入れる前の材料");
        Console.WriteLine();
        Console.WriteLine("台 × 九/新兵 × 倍率 × seed 0..199（verbose）。台本と手番の枠を読むだけ。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 決着T | ヒヨの手番/戦 | ヒヨの手番の頭で燃えている味方（ヒヨ除く）0 ／ 1 ／ 2 ／ 3 ／ 4+ % | 切った量/戦（半減 ／ 盾の配り） | 切った量/周回 | 閾値 20 ／ 30 ／ 50 で育つ回数/戦 | 自火/戦 | ボルグの手番/戦 | 燃焼が切れた味方/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var bn in new[] { "T3", "T3-1" })
        {
            var f = BoardOf(bn);
            for (int s = 0; s < BA.Scales.Length; s++)
            {
                var trs = Traces(f, BA.MainWave, BA.Scales[s].Sc);
                long turns = 0, hiyo = 0, guard = 0, ward = 0, self = 0, borgH = 0, outs = 0; var nb = new long[5];
                foreach (var t in trs)
                {
                    turns += t.Turns; guard += t.HeatGuard; ward += t.HeatWard;
                    var alive = new HashSet<int>(t.Alive0); var burning = new HashSet<int>();
                    bool Mine(int id) => t.Info.TryGetValue(id, out var i) && i.Team == BattleContext.PlayerTeam;
                    var prev = new HashSet<int>();
                    foreach (var st in t.Steps)
                    {
                        switch (st.K)
                        {
                            case StepK.Round:
                            {
                                var b0 = t.BurnAt.GetValueOrDefault(st.A) ?? new HashSet<int>();
                                foreach (int id in burning) if (alive.Contains(id) && Mine(id) && !b0.Contains(id)) outs++;
                                burning = new HashSet<int>(alive.Where(b0.Contains));
                                break;
                            }
                            case StepK.Ignite: burning.Add(st.A); break;
                            case StepK.Death: alive.Remove(st.A); burning.Remove(st.A); break;
                            case StepK.Smolder: burning.Remove(st.A); break;
                            case StepK.Alive: alive.Add(st.A); break;
                            case StepK.SelfLit: self++; break;
                            case StepK.Hand:
                            {
                                string id = t.Info.TryGetValue(st.A, out var i) && i.Team == BattleContext.PlayerTeam ? i.Id : "";
                                if (id == "borg") borgH++;
                                if (id == "hiyo")
                                {
                                    hiyo++;
                                    int c = burning.Count(a => a != st.A && alive.Contains(a) && Mine(a));
                                    nb[Math.Min(4, c)]++;
                                }
                                break;
                            }
                        }
                    }
                }
                double n = trs.Length;
                long heat = guard + ward;
                // 閾値ごとの育つ回数（戦ごとに切り捨て・ボルグの影が 0 かどうかは見ない＝上限）
                string grows = string.Join(" ／ ", HeatSteps.Select(h => (trs.Sum(t => (t.HeatGuard + t.HeatWard) / h) / n).ToString("F2")));
                long hs = nb.Sum();
                Console.WriteLine($"| {bn} | {BA.Scales[s].Name} | {turns / n:F2} | {hiyo / n:F2} | {string.Join(" ／ ", nb.Select(x => Pct(x, hs)))} | {heat / n:F1}（{guard / n:F1} ／ {ward / n:F1}） | {(double)heat / Math.Max(1, turns):F1} | {grows} | {self / n:F2} | {borgH / n:F2} | {outs / n:F2} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    // =================================================================================
    // 測定（段1 → 段2）
    // =================================================================================
    static readonly (string Name, int[] Waves, int Sc)[] FeedConds =
    {
        ("九/新兵 × 200/200（主判定）", new[] { BA.MainWave }, 0),
        ("九/新兵 × 400/300", new[] { BA.MainWave }, 1),
        ("九/新兵 × 115/115", new[] { BA.MainWave }, 2),
        ("九/農兵 × 200/200", new[] { 5 }, 0),
        ("本編 第2〜5波 × 200/200", new[] { 0, 1, 2, 3 }, 0),
        ("本編 第2〜5波 × 400/300", new[] { 0, 1, 2, 3 }, 1),
    };

    static readonly int[] PRoles = { 0, 1, 2, 3 };

    /// <summary>燃えている味方の周回の終わりで、影 ＝ min(周回, 4) の割合（全役）。</summary>
    static double ClockAll(FAgg a, bool k1 = false)
    {
        long n = PRoles.Sum(r => a.ClockN[r]), h = PRoles.Sum(r => k1 ? a.ClockHitK1[r] : a.ClockHit[r]);
        return n == 0 ? double.NaN : 100.0 * h / n;
    }
    /// <summary>周回 2 以降だけの時計（周回1 は保つ火でも 1 ＝ min(1,4) なので必ず一致する）。</summary>
    static double Clock2(FAgg a, bool k1 = false)
    {
        long n = PRoles.Sum(r => a.ClockN2[r]), h = PRoles.Sum(r => k1 ? a.ClockHitK12[r] : a.ClockHit2[r]);
        return n == 0 ? double.NaN : 100.0 * h / n;
    }
    static double Reach4Battle(FAgg a) => a.N == 0 ? 0 : 100.0 * a.BattleReach4 / a.N;
    static double Reach4Any(FAgg a) => a.N == 0 ? 0 : 100.0 * (a.Reach4[0] + a.Reach4[1] + a.Reach4[3]) / Math.Max(1, a.UnitN[0] + a.UnitN[1] + a.UnitN[3]);

    static void FeedRun()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第240期 `firescale feed` —— 段1・段2");
        Console.WriteLine();

        // ---------------- 段1 ----------------
        var s1Boards = new[] { "T3", "T3-1" };
        var s1 = new Dictionary<(string B, int S), FAgg[]>();
        foreach (var bn in s1Boards)
            for (int s = 0; s < BA.Scales.Length; s++)
                s1[(bn, s)] = ReplayAll(Traces(BoardOf(bn), BA.MainWave, BA.Scales[s].Sc), AllRules, false);

        Console.WriteLine("## 段1 全72組 × T3・T3-1 × 九/新兵（倍率3）");
        Console.WriteLine();
        Console.WriteLine("**時計** ＝ 燃えている味方の周回の終わりで 影 ＝ min(周回, 4) の割合（第239期 K1 は同じ標本で併記）。**r** ＝ 影と周回の相関（同じ標本）。**届4** ＝ ボルグ・ホタ・相方のうち影 4 に届いた駒の割合。放つ/戦・ギフト/戦・煽りの相手の種類/戦（ヒヨ）。");
        Console.WriteLine();
        foreach (var bn in s1Boards)
            for (int s = 0; s < BA.Scales.Length; s++)
            {
                var ag = s1[(bn, s)];
                Console.WriteLine($"### {bn} × 九/新兵 × {BA.Scales[s].Name}（第239期 K1 の時計 {ClockAll(ag[0], true):F1}% ・周回2〜 {Clock2(ag[0], true):F1}% ／ r {R2(CorrOf(SumCorr(ag[0].CorrK1, PRoles), 0))}）");
                Console.WriteLine();
                Console.WriteLine("| 組 | 時計 %（周回2〜） | r | 届4 駒 % ／ **戦 %** | 影4 の周回 %（ボルグ ／ ホタ ／ 相方） | 放つ/戦 | ギフト/戦 | 煽り/戦（余熱 %） | 煽りの相手の種類/戦 | 余熱で育つ/戦（4 で余った） |");
                Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
                for (int k = 0; k < AllRules.Length; k++)
                {
                    var a = ag[k];
                    Console.WriteLine($"| {AllRules[k].Name} | {ClockAll(a):F1}（{Clock2(a):F1}） | {R2(CorrOf(SumCorr(a.Corr, PRoles), 0))} | {Reach4Any(a):F1} ／ **{Reach4Battle(a):F1}** | {Pct(a.Eq4[0], a.SCnt[0])} ／ {Pct(a.Eq4[1], a.SCnt[1])} ／ {Pct(a.Eq4[3], a.SCnt[3])} | "
                        + $"{(double)a.Releases / Math.Max(1, a.BorgBattles):F2} | {(double)a.Gifts / a.N:F2} | {(double)a.Feeds / a.N:F2}（{Pct(a.FeedOver, a.Feeds)}） | {Avg(a.FeedTargetsDistinctSum, a.HiyoBattles)} | {(double)a.HeatG / a.N:F2}（{(double)a.HeatO / a.N:F2}） |");
                }
                Console.WriteLine();
            }

        // ---------------- 組の選び方 ----------------
        // 測る前に固定した規則は「届4（駒の割合）≥ 50%」だったが、相方2体が分母に入るので上限がちょうど 50% で、1組も選ばなかった（報告に記録）。
        // 直した規則（段1 を見た後）: 「味方のだれかが影 4 に届いた戦 ≥ 50%」を満たす組のうち、W1・W2 それぞれで時計 % が最も低い 3 組（同値は放つ/戦の多い方 → 列挙順）＋ 対照 W0・Y1・30・B0。
        var main = s1[("T3", 0)];
        int oldPass = Enumerable.Range(0, AllRules.Length).Count(k => Reach4Any(main[k]) >= 50);
        var picked = new List<int>();
        foreach (int w in new[] { 1, 2 })
        {
            var cand = Enumerable.Range(0, AllRules.Length).Where(k => AllRules[k].W == w && Reach4Battle(main[k]) >= 50)
                .OrderBy(k => ClockAll(main[k])).ThenByDescending(k => (double)main[k].Releases / Math.Max(1, main[k].BorgBattles)).ThenBy(k => k).Take(3);
            picked.AddRange(cand);
        }
        int ctl = Array.FindIndex(AllRules, r => r.W == 0 && r.Y == 1 && r.Heat == 30 && !r.BPlus);
        picked.Insert(0, ctl);
        // 参考（選び方の規則の外）: 選ばれた組は Y3 に寄るので、煽りの選び方を比べるために W1・閾値 30・B+ の Y2・Y4 を足す
        foreach (int y in new[] { 2, 4 })
        {
            int k = Array.FindIndex(AllRules, r => r.W == 1 && r.Y == y && r.Heat == 30 && r.BPlus);
            if (!picked.Contains(k)) picked.Add(k);
        }
        var rules = picked.Select(k => AllRules[k]).ToArray();
        Console.WriteLine("## 段2 の組（規則: T3 × 九/新兵 × 200/200 で 届4（戦）≥ 50% を満たし、W1・W2 それぞれ時計 % が最も低い 3 組 ＋ 対照 W0・Y1・30・B0）");
        Console.WriteLine();
        Console.WriteLine($"測る前に固定した線（届4 駒 ≥ 50%）を通った組: {oldPass} ／ {AllRules.Length}。直した線（届4 戦 ≥ 50%）を通った組: {Enumerable.Range(0, AllRules.Length).Count(k => Reach4Battle(main[k]) >= 50)} ／ {AllRules.Length}。");
        Console.WriteLine();
        foreach (var r in rules) Console.WriteLine($"- {r.Name}");
        Console.WriteLine();

        // ---------------- 段2 ----------------
        var boards = Boards.Select(b => (b.Name, F: b.F())).ToArray();
        var res = new Dictionary<(int B, int W, int S), FAgg[]>();
        for (int b = 0; b < boards.Length; b++)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    res[(b, w, s)] = ReplayAll(Traces(boards[b].F, w, BA.Scales[s].Sc), rules, true);
        FAgg Cond(int b, int ci, int k)
        {
            var a = new FAgg();
            foreach (int w in FeedConds[ci].Waves) a.Merge(res[(b, w, FeedConds[ci].Sc)][k]);
            return a;
        }
        bool Burny(int b) => boards[b].F.Occupied().Any(x => x.Def.Id is "borg" or "hota" or "hiyo");

        // 表A
        foreach (int ci in new[] { 0, 1, 4 })
        {
            Console.WriteLine($"## 表A 影の火勢 —— {FeedConds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("周回の終わり（その周回の育ちの後・萎む前）の生きている駒: 平均 ／ 影4 %。届4 ＝ 影 4 に届いた駒 %（届いた周回の平均）。敵は第239期の K1。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 組 | " + string.Join(" | ", Roles) + " |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Roles.Length)));
            for (int b = 0; b < boards.Length; b++)
            {
                if (!Burny(b)) continue;
                for (int k = 0; k < rules.Length; k++)
                {
                    var a = Cond(b, ci, k);
                    Console.WriteLine($"| {(k == 0 ? boards[b].Name : "")} | {rules[k].Name} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(ro =>
                        a.UnitN[ro] == 0 ? "—" : $"{Avg(a.SSum[ro], a.SCnt[ro])} ／ {Pct(a.Eq4[ro], a.SCnt[ro])} ／ 届 {Pct(a.Reach4[ro], a.UnitN[ro])}（{Avg(a.Reach4T[ro], a.Reach4[ro])}）")) + " |");
                }
            }
            Console.WriteLine();
        }

        // 表B
        foreach (int ci in new[] { 0, 1, 4 })
        {
            Console.WriteLine($"## 表B 時計でなくなったか —— {FeedConds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("燃えている味方の周回の終わり: 時計 ＝ 影 ＝ min(周回, 4) の %（括弧は同じ標本の第239期 K1・「2〜」は周回2 以降だけ）／ r ＝ 影と周回の相関（括弧は K1）。SD ＝ 戦ごとの平均の影の、戦をまたいだ標準偏差。同時に影4 ＝ 周回の終わりに影 4 の味方の数の分布。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 組 | ボルグ 時計（K1）／ r（K1） | ホタ 時計（K1）／ r（K1） | 相方 時計（K1）／ r（K1） | SD ボルグ ／ ホタ ／ 相方 | 同時に影4 0 ／ 1 ／ 2 ／ 3 ／ 4+ % |");
            Console.WriteLine("|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                if (!Burny(b)) continue;
                for (int k = 0; k < rules.Length; k++)
                {
                    var a = Cond(b, ci, k);
                    string C(int ro) => a.ClockN[ro] == 0 ? "—" : $"{Pct(a.ClockHit[ro], a.ClockN[ro])}（{Pct(a.ClockHitK1[ro], a.ClockN[ro])}）・2〜 {Pct(a.ClockHit2[ro], a.ClockN2[ro])}（{Pct(a.ClockHitK12[ro], a.ClockN2[ro])}）／ {R2(CorrOf(a.Corr, ro))}（{R2(CorrOf(a.CorrK1, ro))}）";
                    Console.WriteLine($"| {(k == 0 ? boards[b].Name : "")} | {rules[k].Name} | {C(0)} | {C(1)} | {C(3)} | {Sd(a, 0)} ／ {Sd(a, 1)} ／ {Sd(a, 3)} | {string.Join(" ／ ", a.Simul4.Select(x => Pct(x, a.RoundsP)))} |");
                }
            }
            Console.WriteLine();
        }

        // 表C・D・E・G
        foreach (int ci in new[] { 0, 1, 4, 5 })
        {
            Console.WriteLine($"## 表C〜E・G ヒヨ・ボルグ・ホタ・余熱 —— {FeedConds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("ヒヨ: 手番/戦 ／ 煽りの相手（ボルグ ／ ホタ ／ 相方 %）／ ギフトの機会（＝ギフト）/戦（相手 ボルグ ／ ホタ ／ 相方）／ 煽りなし %。ボルグ: 切った量/戦 ／ 余熱で育つ/戦（4 で余った ／ 燃えていず捨てた）／ 自火で育つ/戦 ／ 放つ/戦（放った戦 % ・最初の周回）。ホタ: 手番の頭の段 0〜4 % ／ 同じ段が続いた最長（最大）／ 段の種類/戦。余熱 ＝ 影 4 の駒への育ち/戦。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 組 | ヒヨ 手番 ／ 煽りの相手 % | ギフト/戦（相手） ／ 煽りなし % | ボルグ 切った量 ／ 余熱で育つ（余り ／ 捨て） ／ 自火で育つ | 放つ/戦（戦 % ・T） | ホタ 段 0 ／ 1 ／ 2 ／ 3 ／ 4 % | ホタ 最長（最大）／ 種類 | 余熱/戦（ボルグ ／ ホタ ／ 相方） |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                if (!Burny(b)) continue;
                for (int k = 0; k < rules.Length; k++)
                {
                    var a = Cond(b, ci, k);
                    double n = a.N;
                    string hiyo = a.HiyoHands == 0 ? "—" : $"{a.HiyoHands / n:F2} ／ {Pct(a.FeedTo[0], a.Feeds)} ／ {Pct(a.FeedTo[1], a.Feeds)} ／ {Pct(a.FeedTo[3], a.Feeds)}";
                    string gift = a.HiyoHands == 0 ? "—" : $"{a.Gifts / n:F2}（{a.GiftTo[0] / n:F2} ／ {a.GiftTo[1] / n:F2} ／ {a.GiftTo[3] / n:F2}） ／ {Pct(a.NoTarget, a.HiyoHands)}";
                    string borg = a.BorgBattles == 0 ? "—" : $"{a.Heat / n:F1} ／ {a.HeatG / n:F2}（{a.HeatO / n:F2} ／ {a.HeatX / n:F2}） ／ {a.SelfG / n:F2}";
                    string rel = a.BorgBattles == 0 ? "—" : $"{(double)a.Releases / a.BorgBattles:F2}（{Pct(a.RelBattles, a.BorgBattles)} ・ {Avg(a.RelFirstT, a.RelBattles)}）";
                    string hota = a.HotaHands == 0 ? "—" : string.Join(" ／ ", a.HotaStage.Select(x => Pct(x, a.HotaHands)));
                    string hrun = a.HotaBattles == 0 ? "—" : $"{Avg(a.HotaRunSum, a.HotaBattles)}（{a.HotaRunMax}） ／ {Avg(a.HotaDistinctSum, a.HotaBattles)}";
                    Console.WriteLine($"| {(k == 0 ? boards[b].Name : "")} | {rules[k].Name} | {hiyo} | {gift} | {borg} | {rel} | {hota} | {hrun} | {a.Over[0] / n:F2} ／ {a.Over[1] / n:F2} ／ {a.Over[3] / n:F2} |");
                }
            }
            Console.WriteLine();
        }

        // 表F 絵
        foreach (int ci in new[] { 0, 4 })
        {
            Console.WriteLine($"## 表F 影の絵の単調さ —— {FeedConds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("第239期の絵（種類／攻撃型／手番の中の出来事）に影の型を足した: ボルグ ＝ 放つ／通常、ホタ ＝ 段n、ヒヨ ＝ 煽り→相手／ギフト→相手／煽りなし。骨格 ＝ 種類／攻撃型／影の型（相手を除く）。第239期と同じ定義（戦ごとに取って平均）。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 組 | 駒 | 手番/戦 | 絵の種類 | 最長（最大） | 最多の割合 | 続けて同じ絵 | 骨格の種類 | 骨格の最多 | 最も多い絵（全戦での割合） |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                if (!Burny(b)) continue;
                for (int k = 0; k < rules.Length; k++)
                {
                    var a = Cond(b, ci, k);
                    foreach (var id in new[] { "borg", "hota", "hiyo" })
                    {
                        if (!a.Pics.TryGetValue(id, out var pa) || pa.Battles == 0) continue;
                        var top3 = pa.Freq.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key}（{100.0 * kv.Value / pa.Hands:F0}%）");
                        Console.WriteLine($"| {(k == 0 && id == "borg" ? boards[b].Name : "")} | {(id == "borg" || !a.Pics.ContainsKey("borg") ? rules[k].Name : "")} | {pa.Name} | {(double)pa.Hands / pa.Battles:F2} | {Avg(pa.DistinctSum, pa.Battles)} | {Avg(pa.RunSum, pa.Battles)}（{pa.RunMax}） | "
                            + $"{TopShare(pa)} | {Pct(pa.Repeats, pa.Pairs)} | {Avg(pa.BoneDistinctSum, pa.Battles)} | {BoneTopShare(pa)} | {string.Join("<br>", top3)} |");
                    }
                }
            }
            Console.WriteLine();
        }
        // 参考の台（燃焼の3体がいない）の絵は第239期の定義のまま（影の型は付かない）
        Console.WriteLine("## 表F' 参考の台の絵（影の型は付かない・第239期の定義）—— 九/新兵 × 200/200 ／ 本編 第2〜5波 × 200/200");
        Console.WriteLine();
        Console.WriteLine("| 台 | 条件 | 駒 | 手番/戦 | 絵の種類 | 最多の割合 | 続けて同じ絵 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int b = 0; b < boards.Length; b++)
        {
            if (Burny(b)) continue;
            foreach (int ci in new[] { 0, 4 })
            {
                var a = Cond(b, ci, 0);
                foreach (var (id, pa) in a.Pics.OrderBy(kv => kv.Value.Name))
                    Console.WriteLine($"| {boards[b].Name} | {FeedConds[ci].Name} | {pa.Name} | {(double)pa.Hands / pa.Battles:F2} | {Avg(pa.DistinctSum, pa.Battles)} | {TopShare(pa)} | {Pct(pa.Repeats, pa.Pairs)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    // =================================================================================
    // 自己検査
    // =================================================================================
    static void FeedCheck()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int ok = 0, all = 0;
        void Ok(bool c, string what) { all++; if (c) ok++; Console.WriteLine($"- {(c ? "○" : "×")} {what}"); }
        Console.WriteLine("# 第240期 `firescale feedcheck`");
        Console.WriteLine();

        // (a) 規則（盤の外で1駒ずつ）
        {
            var f = new Feeder(new FeedRule(1, 1, 30, false));
            f.Keep(1); f.Keep(1); f.Keep(1);
            Ok(f.Of(1) == 1, "保つ: 何度点いても 1 のまま");
            Ok(f.Grow(1) == 0 && f.Of(1) == 2, "育てる: +1");
            f.Wither(new[] { 1 });
            Ok(f.Of(1) == 2, "W1: 育った周回の終わりは萎まない");
            f.Wither(new[] { 1 });
            Ok(f.Of(1) == 1, "W1: 育たない周回で −1");
            f.Wither(new[] { 1 });
            Ok(f.Of(1) == 1, "W1: 燃えている間は 1 未満にしない");
            var g = new Feeder(new FeedRule(2, 1, 30, false)); g.Keep(1); g.Grow(1); g.Grow(1); g.Wither(new[] { 1 });
            g.Wither(new[] { 1 }); bool a1 = g.Of(1) == 3; g.Wither(new[] { 1 });
            Ok(a1 && g.Of(1) == 2, "W2: 育たない周回が2回続いて −1");
            var z = new Feeder(new FeedRule(0, 1, 30, false)); z.Keep(1); z.Grow(1); for (int i = 0; i < 5; i++) z.Wither(new[] { 1 });
            Ok(z.Of(1) == 2, "W0: 萎まない");
            var c = new Feeder(new FeedRule(0, 1, 30, false)); c.Keep(1); for (int i = 0; i < 5; i++) c.Grow(1);
            Ok(c.Of(1) == Max && c.Grow(1) == 1, "上限 4・4 への育ちは余熱");
            Ok(new Feeder(new FeedRule(0, 1, 30, false)).Grow(1) == 2, "燃えていない（0）駒は育たない");
            var h = new Feeder(new FeedRule(0, 1, 30, false)); h.Keep(9);
            var (g1, _, _) = h.Heat(9, 29); var (g2, _, _) = h.Heat(9, 32);
            Ok(g1 == 0 && g2 == 2 && h.Of(9) == 3 && h.HeatAcc[9] == 1, "余熱: 29 → 0 回 ／ +32（累計 61）→ 2 回・残り 1");
        }

        // (b) 足跡は盤面に触らない・乱数を引かない
        {
            string root = FindRoot();
            string mine = File.ReadAllText(Path.Combine(root, "BattleSim", "Modes", "FireScale.Feed.cs"));
            // 検索文字列は連結で組む（このファイル自身を走査するので・R035）
            string rx = "ctx" + @"\.(Roll|PickOne)" + @"\(", nr = "new " + "Random(";
            Ok(!Regex.IsMatch(mine, rx) && !mine.Contains(nr), "器具（`FireScale.Feed.cs`）は乱数を引かない（ctx の Roll / PickOne と Random の生成を使わない）");
            long n = 0, diff = 0;
            foreach (var (bn, bf) in Boards)
            {
                var f = bf();
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int seed = 0; seed < 10; seed++)
                    {
                        var (r, p, e) = Fight(f, w, BA.Scales[0].Sc, seed);
                        int evc = r.Events.Count, hc = r.Hands.Count; string log = string.Join("\n", r.Log.Select(l => l.Text));
                        var tr = BuildTrace(r, p, e);
                        foreach (var rule in AllRules) Replay(tr, rule, new FAgg(), true);
                        n++;
                        if (r.Events.Count != evc || r.Hands.Count != hc || string.Join("\n", r.Log.Select(l => l.Text)) != log) diff++;
                        var (r2, _, _) = Fight(f, w, BA.Scales[0].Sc, seed);
                        if (r2.Events.Count != evc || r2.PlayerWon != r.PlayerWon || r2.Turns != r.Turns) diff++;
                    }
            }
            Ok(diff == 0, $"影を 72 組ぶん数えた後も台本・枠・ログが同じ／同じ seed の戦がもう一度同じになる {n - diff} ／ {n}");
        }

        // (c) 足跡の読み: 余熱の量 ＝ 帳簿（半減 ＋ 盾の配り）・W0 の影は着火が無ければ 1 を超えない
        {
            long bad = 0, n = 0, over = 0;
            var f = T3;
            for (int s = 0; s < BA.Scales.Length; s++)
                for (int seed = 0; seed < 40; seed++)
                {
                    var (r, p, e) = Fight(f, BA.MainWave, BA.Scales[s].Sc, seed);
                    var tr = BuildTrace(r, p, e);
                    long led = r.TallyByUnit.Values.Sum(t => (long)t.FireArmorSaved + t.FireWardSaved);
                    n++;
                    if (tr.HeatGuard + tr.HeatWard != led) bad++;
                    // 育てる手が無い組（W0・余熱の閾値が届かない・B0）で、ヒヨを抜いた影は 1 を超えない
                    var a = new FAgg();
                    var trNoHiyo = new Trace();
                    foreach (var (k, v) in tr.Info) trNoHiyo.Info[k] = v.Id == "hiyo" ? ("x", v.Name, v.Team, v.Slot) : v;
                    foreach (var x in tr.Alive0) trNoHiyo.Alive0.Add(x);
                    foreach (var (k, v) in tr.BurnAt) trNoHiyo.BurnAt[k] = v;
                    trNoHiyo.Steps.AddRange(tr.Steps.Where(st => st.K != StepK.Heat));
                    trNoHiyo.HandPics.AddRange(tr.HandPics);
                    Replay(trNoHiyo, new FeedRule(0, 1, 30, false), a, false);
                    for (int ro = 0; ro < 4; ro++) for (int v = 2; v <= Max; v++) over += a.Hist[ro, v];
                }
            Ok(bad == 0, $"足跡の切った量 ≠ 帳簿の `FireArmorSaved` ＋ `FireWardSaved` の戦 {bad} ／ {n}");
            Ok(over == 0, $"育てる手（煽り・余熱）を抜くと、保つ火だけでは影が 2 以上にならない（2 以上の標本 {over}）");
        }

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {all}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
