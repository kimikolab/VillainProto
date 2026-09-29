using System.Reflection;
using System.Text.RegularExpressions;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// =====================================================================================
// firescale gift* —— 第241期「火勢の影の計算 その3（速く育てる・ヒヨの火でターンギフト）」。
// 指示書は design/PHASE241_FIRE_GIFT_SPEC.md ／ 報告は design/PHASE241_FIRE_GIFT.md。
// **盤面は変えない**。第240期の足跡（`Trace`）に「一振り」と「当たった敵」を足し、規則の組ごとに再生するだけ。
//
//     dotnet run --project BattleSim -c Release 0 firescale gift0     # Phase 0（規則を入れる前の材料）
//     dotnet run --project BattleSim -c Release 0 firescale gift      # 段1（T3・T3-1 × 九/新兵 × 倍率3 × 全96組）→ 段2（選んだ組 × 全台・全波・倍率）
//     dotnet run --project BattleSim -c Release 0 firescale giftcheck # 自己検査
// =====================================================================================
static partial class FireScaleDiag
{
    // ---------------------------------------------------------------------------------
    // 規則の組（§3・§4）。土台は第240期の W1・閾値 30・B+ に固定。
    // ---------------------------------------------------------------------------------
    /// <summary>F ＝ 加速の段（1 燃え広がり ／ 2 ＋開幕の火勢 ／ 3 ＋強い煽り ／ 4 ＋加速）・HS ＝ ヒヨ自身の育ち（1 煽る ／ 2 味方の燃え広がり ／ 3 両方）・
    /// G ＝ 撃つ時機（3 即撃ち ／ 4 待ち）・E ＝ 大技の出どころ（1 ギフトでだけ ／ 2 自分の手番でも）・Y ＝ 煽りの相手（1 火勢最大 ／ 4 攻撃力最大）。</summary>
    internal readonly record struct GiftRule(int F, int HS, int G, int E, int Y)
    {
        public string Name => $"F{F}・HS{HS}・G{G}・E{E}・Y{Y}";
        public bool Open => F >= 2;
        public int FeedAmt => F >= 3 ? 2 : 1;
        public bool Accel => F >= 4;
        public bool HsFeed => HS is 1 or 3;
        public bool HsSpread => HS is 2 or 3;
    }
    internal const int GiftHeat = 30;
    internal static readonly GiftRule[] GiftRules =
        (from f in new[] { 1, 2, 3, 4 } from hs in new[] { 1, 2, 3 } from g in new[] { 3, 4 } from e in new[] { 1, 2 } from y in new[] { 1, 4 }
         select new GiftRule(f, hs, g, e, y)).ToArray();

    // ---------------------------------------------------------------------------------
    // 影（味方）。第240期の Feeder と同じ土台（保つ・W1・余熱）に、量のある育ちと F2・F4 を足した。盤の外でも単体で検査できる。
    // ---------------------------------------------------------------------------------
    internal sealed class GiftFeeder
    {
        public readonly GiftRule R;
        public readonly Dictionary<int, int> S = new(), HeatAcc = new();
        public readonly HashSet<int> Grew = new();
        public long OverAmt;   // 4 の上で余った量（余熱）
        public GiftFeeder(GiftRule r) { R = r; }
        public int Of(int id) => S.GetValueOrDefault(id);
        /// <summary>保つ: 0 なら 1（F2 で開幕の着火なら 2）。1 以上なら上げない（開幕の 2 は 1 を 2 へ上げる）。</summary>
        public void Keep(int id, bool open)
        {
            int s = Of(id), to = open && R.Open ? 2 : 1;
            if (s < to && (s == 0 || open && R.Open)) S[id] = to;
        }
        /// <summary>育てる: +k（F4 なら火勢2以上で +1 多い・上限 4）。戻り値 0 ＝ 育った ／ 1 ＝ 4 だったので全部余熱 ／ 2 ＝ 燃えていない（0）ので捨てた。</summary>
        public int Grow(int id, int k = 1)
        {
            int s = Of(id);
            if (s == 0) return 2;
            Grew.Add(id);
            int amt = k + (R.Accel && s >= 2 ? 1 : 0);
            if (s >= Max) { OverAmt += amt; return 1; }
            int to = s + amt;
            if (to > Max) { OverAmt += to - Max; to = Max; }
            S[id] = to; return 0;
        }
        /// <summary>ボルグの余熱: 切った量の累計 30 ごとに +1。</summary>
        public int Heat(int id, int amount)
        {
            int acc = HeatAcc.GetValueOrDefault(id) + amount, g = 0;
            while (acc >= GiftHeat) { acc -= GiftHeat; if (Grow(id) != 2) g++; }
            HeatAcc[id] = acc;
            return g;
        }
        /// <summary>周回の終わり: W1（育たなければ −1・燃えている間は 1 未満にしない）。</summary>
        public void Wither(IEnumerable<int> ids)
        {
            foreach (int id in ids)
            {
                int s = Of(id);
                if (!Grew.Contains(id) && s > 1) S[id] = s - 1;
            }
            Grew.Clear();
        }
        public void Out(int id) { S.Remove(id); }
    }

    /// <summary>集計（規則の組 × 条件）。足し合わせはリフレクションで（数値の配列を全部足す）。</summary>
    internal sealed class GAgg
    {
        public const int TM = 8;
        public long N, Wins, AllSurv, Turns;
        public readonly long[] TurnHist = new long[11];                      // 決着T 1..9 ／ 10+（添字 10）
        // ギフト
        public long GiftByT2, GiftByT3, GiftBattles;
        public readonly long[] FirstGift = new long[TM + 1];                // 0 ＝ 出なかった ／ 1..7 ／ 8 ＝ 8+
        public long GiftEvents, Gift1, Gift2, Recipients, GiftNoCand, GiftRel, GiftFull, GiftNormal, OwnRel, OwnFull;
        public readonly long[] GiftTo = new long[5];
        public readonly long[] GiftAt = new long[TM + 1];                   // 周回ごとのギフトの回数
        // ヒヨ
        public long HiyoHands, HiyoBattles, Feeds, FeedOver, NoTarget, HiyoZero;
        public readonly long[] FeedTo = new long[5], HiyoHist = new long[Max + 1];
        public readonly long[,] HiyoAt = new long[TM + 1, Max + 1];           // 周回 × ヒヨの手番の頭の火勢
        public long HsFeedG, HsSpreadG;
        // 燃え広がり
        public long F1Hits, F1Waste;
        public readonly long[] F1By = new long[5];
        public readonly long[,] F1At = new long[5, TM + 1];
        // 影
        public readonly long[] SSum = new long[5], SCnt = new long[5], Eq4 = new long[5], UnitN = new long[5], Reach4 = new long[5], Reach4T = new long[5], Over = new long[5];
        public readonly long[,] Hist = new long[5, Max + 1];
        public readonly long[,] RSum = new long[5, TM + 1], RCnt = new long[5, TM + 1];   // 役 × 周回の終わり
        public readonly long[] Simul4 = new long[5];
        public long RoundsP, Heat, HeatG, OverAmt;
        public readonly double[,] Corr = new double[5, 6];
        public readonly long[] ClockN = new long[5], ClockHit = new long[5], ClockN2 = new long[5], ClockHit2 = new long[5];
        // ホタの段（手番の頭）
        public readonly long[] HotaStage = new long[Max + 1];
        public long HotaHands;
        // 敵の燃焼（周回の頭の写し ／ 周回の終わり）
        public readonly long[] FoeBurnHead = new long[TM + 1], FoeBurnEnd = new long[TM + 1], FoeAliveEnd = new long[TM + 1], FoeRounds = new long[TM + 1];
        public readonly Dictionary<string, PicAgg> Pics = new();

        static readonly FieldInfo[] Fields = typeof(GAgg).GetFields(BindingFlags.Public | BindingFlags.Instance);
        public void Merge(GAgg o)
        {
            foreach (var fi in Fields)
            {
                object a = fi.GetValue(this)!, b = fi.GetValue(o)!;
                switch (a)
                {
                    case long: fi.SetValue(this, (long)a + (long)b); break;
                    case long[] la: { var lb = (long[])b; for (int i = 0; i < la.Length; i++) la[i] += lb[i]; break; }
                    case long[,] l2: { var lb = (long[,])b; for (int i = 0; i < l2.GetLength(0); i++) for (int j = 0; j < l2.GetLength(1); j++) l2[i, j] += lb[i, j]; break; }
                    case double[,] d2: { var db = (double[,])b; for (int i = 0; i < d2.GetLength(0); i++) for (int j = 0; j < d2.GetLength(1); j++) d2[i, j] += db[i, j]; break; }
                    case Dictionary<string, PicAgg> pa:
                        foreach (var (k, v) in (Dictionary<string, PicAgg>)b) { if (!pa.TryGetValue(k, out var x)) pa[k] = x = new PicAgg(); x.Merge(v); }
                        break;
                }
            }
        }
    }

    /// <summary>1戦の中で起きたこと（自己検査が読む）。</summary>
    internal sealed class GiftLog
    {
        public readonly List<(int Round, int Hiyo, int HiyoS, int[] To, int[] ToS, long[] Atk)> Gifts = new();
        public readonly List<(int Round, int Id, string What)> Bigs = new();   // 放つ ／ 全体（ギフトの手番か自分の手番か）
        public readonly List<(int Attacker, int Target, bool Burning)> Hits = new();
    }

    /// <summary>規則の組 <paramref name="rule"/> で1戦（足跡）を再生して <paramref name="acc"/> に足す。</summary>
    internal static void GiftReplay(Trace tr, GiftRule rule, GAgg acc, bool pics, GiftLog? log = null)
    {
        acc.N++; acc.Turns += tr.Turns; if (tr.Won) acc.Wins++; if (tr.AllSurv) acc.AllSurv++;
        acc.TurnHist[Math.Min(10, Math.Max(1, tr.Turns))]++;
        var info = tr.Info;
        bool Mine(int id) => info.TryGetValue(id, out var i) && i.Team == BattleContext.PlayerTeam;
        int Role(int id) => info.TryGetValue(id, out var i) ? RoleIx(i.Id, i.Team) : 4;
        string IdOf(int id) => info.TryGetValue(id, out var i) ? i.Id : "";
        int SlotOf(int id) => info.TryGetValue(id, out var i) ? i.Slot : 99;
        var alive = new HashSet<int>(tr.Alive0);
        var burning = new HashSet<int>();
        var f = new GiftFeeder(rule);
        var k1 = new Shadow(0);
        var reach4 = new Dictionary<int, int>();
        var swingHit = new Dictionary<int, HashSet<int>>();   // 攻撃した駒 → この一振りで数えた敵
        int round = 0, firstGift = 0; bool hiyoSeen = false;
        int hiyoId = info.Where(kv => kv.Value.Id == "hiyo" && kv.Value.Team == BattleContext.PlayerTeam).Select(kv => kv.Key).DefaultIfEmpty(-1).First();
        var seqs = pics ? new Dictionary<int, List<(string, string)>>() : null;
        void Seq(int id, string pic, string bone) { if (seqs is not null) (seqs.TryGetValue(id, out var l) ? l : seqs[id] = new()).Add((pic, bone)); }
        int Ti(int t) => Math.Min(GAgg.TM, Math.Max(0, t));
        long AtkOf(int a) => tr.AtkAt.GetValueOrDefault((round, a));

        void Finalize(int t)
        {
            if (t <= 0) return;
            int n4 = 0, ti = Ti(t);
            foreach (int id in alive)
            {
                int ro = Role(id);
                int s = ro == 4 ? k1.Of(id) : f.Of(id);
                acc.SSum[ro] += s; acc.SCnt[ro]++; acc.Hist[ro, s]++;
                acc.RSum[ro, ti] += s; acc.RCnt[ro, ti]++;
                if (s >= Max) { acc.Eq4[ro]++; if (!reach4.ContainsKey(id)) reach4[id] = t; }
                if (ro != 4)
                {
                    if (s >= Max) n4++;
                    if (burning.Contains(id))
                    {
                        AddCorr(acc.Corr, ro, t, s);
                        acc.ClockN[ro]++; if (s == Math.Min(t, Max)) acc.ClockHit[ro]++;
                        if (t >= 2) { acc.ClockN2[ro]++; if (s == Math.Min(t, Max)) acc.ClockHit2[ro]++; }
                    }
                }
                else { acc.FoeAliveEnd[ti]++; if (burning.Contains(id)) acc.FoeBurnEnd[ti]++; }
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
            foreach (int id in alive.Where(Mine).ToList()) if (!burning.Contains(id) && f.Of(id) > 0) f.S[id] = 0;
            int ti = Ti(t);
            acc.FoeRounds[ti]++;
            acc.FoeBurnHead[ti] += alive.Count(id => !Mine(id) && burning.Contains(id));
        }
        // 大技（放つ／全体）。gift ＝ ギフトの手番か。
        string Big(int id, bool gift)
        {
            string me = IdOf(id); int s = f.Of(id);
            if (me == "borg" && s >= Max)
            {
                f.S[id] = 1;
                if (gift) acc.GiftRel++; else acc.OwnRel++;
                log?.Bigs.Add((round, id, gift ? "ギフト放つ" : "放つ"));
                return "放つ";
            }
            if (me == "hota" && s >= Max)
            {
                if (gift) acc.GiftFull++; else acc.OwnFull++;
                log?.Bigs.Add((round, id, gift ? "ギフト全体" : "全体"));
                return "全体";
            }
            if (gift) acc.GiftNormal++;
            return me == "hota" ? HotaType(s, false) : "通常";
        }
        static string HotaType(int s, bool full) => s switch { <= 1 => "単体", 2 => "貫き", 3 => "貫き＋着火", _ => full ? "全体" : "貫き＋着火" };

        foreach (var st in tr.Steps)
        {
            switch (st.K)
            {
                case StepK.Round:
                    Finalize(round); round = st.A; StartRound(round); break;
                case StepK.Ignite:
                {
                    bool open = st.B == 1;
                    burning.Add(st.A);
                    if (Mine(st.A)) f.Keep(st.A, open);
                    k1.Ignite(st.A);
                    if (open && rule.Open && !Mine(st.A) && k1.Of(st.A) < 2) k1.S[st.A] = 2;
                    break;
                }
                case StepK.SelfLit:
                    if (f.Of(st.A) > 0 && f.Grow(st.A) == 1) acc.Over[Role(st.A)]++;   // B+
                    break;
                case StepK.Heat:
                    acc.Heat += st.B; acc.HeatG += f.Heat(st.A, st.B); break;
                case StepK.Smolder:
                    burning.Remove(st.A); if (Mine(st.A)) f.S[st.A] = 0; break;
                case StepK.Death:
                    alive.Remove(st.A); burning.Remove(st.A); f.Out(st.A); k1.Kill(st.A); break;
                case StepK.Alive:
                    alive.Add(st.A); break;
                case StepK.Swing:
                    if (swingHit.TryGetValue(st.A, out var sh)) sh.Clear();
                    break;
                case StepK.Hit:
                {
                    // F1 燃え広がり: この一振りで当たった敵のうち、当たる前から燃えていた敵1体ごとに +1（同じ一振りで同じ敵は1回）
                    int a = st.A, t = st.B;
                    bool was = burning.Contains(t);
                    log?.Hits.Add((a, t, was));
                    if (!was || rule.F < 1) break;   // F0（参考の対照）は燃え広がりなし
                    if (!(swingHit.TryGetValue(a, out var set) ? set : swingHit[a] = new()).Add(t)) break;
                    int r = f.Grow(a);
                    if (r == 2) { acc.F1Waste++; break; }
                    acc.F1Hits++; acc.F1By[Role(a)]++; acc.F1At[Role(a), Ti(round)]++;
                    if (r == 1) acc.Over[Role(a)]++;
                    if (rule.HsSpread && hiyoId >= 0 && alive.Contains(hiyoId) && a != hiyoId && f.Grow(hiyoId) != 2) acc.HsSpreadG++;
                    break;
                }
                case StepK.Hand:
                {
                    int id = st.A;
                    if (!Mine(id)) break;
                    string me = IdOf(id);
                    string tag = "", bone = "";
                    if (me == "hota")
                    {
                        int s = f.Of(id);
                        acc.HotaStage[s]++; acc.HotaHands++;
                        tag = bone = rule.E == 2 && s >= Max ? Big(id, false) : HotaType(s, false);
                    }
                    else if (me == "borg")
                        tag = bone = rule.E == 2 && f.Of(id) >= Max ? Big(id, false) : "通常";
                    else if (me == "hiyo")
                    {
                        hiyoSeen = true; acc.HiyoHands++;
                        int hs = f.Of(id);
                        acc.HiyoHist[hs]++; acc.HiyoAt[Ti(round), hs]++; if (hs == 0) acc.HiyoZero++;
                        var cands = alive.Where(x => x != id && Mine(x) && burning.Contains(x) && f.Of(x) > 0).ToList();
                        int want = hs >= Max ? 2 : hs >= 3 && rule.G == 3 ? 1 : 0;
                        if (want > 0 && cands.Count > 0)
                        {
                            var to = cands.OrderByDescending(AtkOf).ThenBy(SlotOf).Take(want).ToArray();
                            var toS = to.Select(f.Of).ToArray();
                            log?.Gifts.Add((round, id, hs, to, toS, to.Select(AtkOf).ToArray()));
                            acc.GiftEvents++; acc.Recipients += to.Length; acc.GiftAt[Ti(round)]++;
                            if (to.Length == 1) acc.Gift1++; else acc.Gift2++;
                            if (firstGift == 0) firstGift = round;
                            foreach (int x in to)
                            {
                                acc.GiftTo[Role(x)]++;
                                string what = Big(x, true);
                                Seq(x, "ギフトの手番／" + what, "ギフトの手番／" + what);
                            }
                            f.S[id] = 1;
                            tag = "ギフト→" + string.Join("・", to.Select(x => Roles[Role(x)])); bone = "ギフト×" + to.Length;
                        }
                        else
                        {
                            if (want > 0) acc.GiftNoCand++;
                            var pool = rule.Y == 1 ? cands.Where(x => f.Of(x) < Max) : cands;
                            var ord = rule.Y == 1 ? pool.OrderByDescending(f.Of).ThenBy(SlotOf) : pool.OrderByDescending(AtkOf).ThenBy(SlotOf);
                            int to = ord.DefaultIfEmpty(-1).First();
                            if (to < 0) { acc.NoTarget++; tag = bone = "煽りなし"; }
                            else
                            {
                                acc.Feeds++; acc.FeedTo[Role(to)]++;
                                if (f.Grow(to, rule.FeedAmt) == 1) { acc.FeedOver++; acc.Over[Role(to)]++; }
                                if (rule.HsFeed && f.Grow(id) != 2) acc.HsFeedG++;
                                tag = "煽り→" + Roles[Role(to)]; bone = "煽り";
                            }
                        }
                    }
                    if (seqs is not null)
                    {
                        var (_, pic, b0) = tr.HandPics[st.B];
                        Seq(id, tag == "" ? pic : pic + "／" + tag, bone == "" ? b0 : b0 + "／" + bone);
                    }
                    break;
                }
            }
        }
        Finalize(round);
        acc.OverAmt += f.OverAmt;

        foreach (var (id, i2) in info)
        {
            if (i2.Id == "summon") continue;
            int ro = RoleIx(i2.Id, i2.Team);
            acc.UnitN[ro]++;
            if (reach4.TryGetValue(id, out int t4)) { acc.Reach4[ro]++; acc.Reach4T[ro] += t4; }
        }
        if (hiyoSeen)
        {
            acc.HiyoBattles++;
            acc.FirstGift[firstGift == 0 ? 0 : Ti(firstGift)]++;
            if (firstGift > 0) { acc.GiftBattles++; if (firstGift <= 2) acc.GiftByT2++; if (firstGift <= 3) acc.GiftByT3++; }
        }
        if (seqs is not null)
            foreach (var (id, seq) in seqs)
            {
                var ai = info[id];
                if (!acc.Pics.TryGetValue(ai.Id, out var pa)) acc.Pics[ai.Id] = pa = new PicAgg { Name = ai.Name };
                pa.TakeBattle(seq);
            }
    }

    internal static GAgg[] GiftReplayAll(Trace[] trs, GiftRule[] rules, bool pics)
    {
        var res = new GAgg[rules.Length];
        Parallel.For(0, rules.Length, k =>
        {
            var a = new GAgg();
            foreach (var t in trs) GiftReplay(t, rules[k], a, pics);
            res[k] = a;
        });
        return res;
    }

    static double GiftT2(GAgg a) => a.HiyoBattles == 0 ? double.NaN : 100.0 * a.GiftByT2 / a.HiyoBattles;
    static double RecipPer(GAgg a) => a.N == 0 ? 0 : (double)a.Recipients / a.N;
    static string P1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string PerN(long x, GAgg a) => a.N == 0 ? "—" : ((double)x / a.N).ToString("F2");

    // =================================================================================
    // Phase 0（規則を入れる前の材料）
    // =================================================================================
    static void Gift0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第241期 Phase 0 `firescale gift0` —— 規則を入れる前の材料");
        Console.WriteLine();
        Console.WriteLine("台 × 九/新兵 × 倍率 × seed 0..199（verbose）。台本と手番の枠を読むだけ（第240期の足跡に「一振り」「当たった敵」を足した）。");
        Console.WriteLine();

        // Q0-1 開幕（周回0）の着火の出どころ
        Console.WriteLine("## Q0-1 開幕（周回0）の着火 —— 誰が誰に点けたか（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 開幕の着火/戦 | 書き手の内訳 | 味方に ／ 敵に |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var bn in new[] { "T3", "T3-1", "雷＋ボルグ" })
        {
            var f = BoardOf(bn);
            for (int s = 0; s < BA.Scales.Length; s++)
            {
                var by = new Dictionary<string, long>(); long ally = 0, foe = 0, n = 0;
                var lk = new object();
                Parallel.For(0, BA.Seeds, i =>
                {
                    var (r, p, e) = Fight(f, BA.MainWave, BA.Scales[s].Sc, i);
                    var mine = p.Select(u => u.InstanceId).ToHashSet();
                    var nm = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Name);
                    lock (lk)
                    {
                        n++;
                        foreach (var x in r.Events)
                            if (x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Burn && x.Turn <= 0 && x.TargetId is int t)
                            {
                                string w = x.ActorId is int a && nm.TryGetValue(a, out var an) ? an : "（出どころなし）";
                                by[w] = by.GetValueOrDefault(w) + 1;
                                if (mine.Contains(t)) ally++; else foe++;
                            }
                    }
                });
                Console.WriteLine($"| {bn} | {BA.Scales[s].Name} | {(ally + foe) / (double)n:F2} | {(by.Count == 0 ? "—" : string.Join("・", by.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / (double)n:F2}")))} | {ally / (double)n:F2} ／ {foe / (double)n:F2} |");
            }
        }
        Console.WriteLine();

        // Q0-2〜Q0-5（T3・T3-1）
        foreach (var bn in new[] { "T3", "T3-1" })
        {
            var f = BoardOf(bn);
            for (int s = 0; s < BA.Scales.Length; s++)
            {
                var trs = Traces(f, BA.MainWave, BA.Scales[s].Sc);
                const int TM = GAgg.TM;
                var foeHead = new long[TM + 1]; var foeAlive = new long[TM + 1]; var rounds = new long[TM + 1];
                var hitB = new long[5, TM + 1]; var hitAll = new long[5, TM + 1];   // 役 × 周回: 燃えていた敵に当たった ／ 当たった
                var turnsH = new long[11];
                long hiyoH = 0, hiyoBurn = 0; var hiyoBurnAt = new long[TM + 1]; var hiyoHAt = new long[TM + 1];
                long hotaBeforeHiyo = 0, borgBeforeHiyo = 0, roundsWithHiyo = 0;
                var topAtk = new long[5]; long topN = 0;
                foreach (var t in trs)
                {
                    turnsH[Math.Min(10, Math.Max(1, t.Turns))]++;
                    var alive = new HashSet<int>(t.Alive0); var burning = new HashSet<int>();
                    bool Mine(int id) => t.Info.TryGetValue(id, out var i) && i.Team == BattleContext.PlayerTeam;
                    int Role(int id) => t.Info.TryGetValue(id, out var i) ? RoleIx(i.Id, i.Team) : 4;
                    var swingSet = new Dictionary<int, HashSet<int>>();
                    int round = 0; bool hotaDone = false, borgDone = false;
                    foreach (var st in t.Steps)
                    {
                        int ti = Math.Min(TM, Math.Max(0, round));
                        switch (st.K)
                        {
                            case StepK.Round:
                                round = st.A; ti = Math.Min(TM, round);
                                burning = new HashSet<int>(alive.Where(x => (t.BurnAt.GetValueOrDefault(round) ?? new()).Contains(x)));
                                rounds[ti]++;
                                foeHead[ti] += alive.Count(x => !Mine(x) && burning.Contains(x)); foeAlive[ti] += alive.Count(x => !Mine(x));
                                hotaDone = borgDone = false;
                                break;
                            case StepK.Ignite: burning.Add(st.A); break;
                            case StepK.Death: alive.Remove(st.A); burning.Remove(st.A); break;
                            case StepK.Smolder: burning.Remove(st.A); break;
                            case StepK.Alive: alive.Add(st.A); break;
                            case StepK.Swing: if (swingSet.TryGetValue(st.A, out var ss)) ss.Clear(); break;
                            case StepK.Hit:
                            {
                                if (!(swingSet.TryGetValue(st.A, out var set) ? set : swingSet[st.A] = new()).Add(st.B)) break;
                                int ro = Role(st.A);
                                hitAll[ro, ti]++; if (burning.Contains(st.B)) hitB[ro, ti]++;
                                break;
                            }
                            case StepK.Hand:
                            {
                                string id = t.Info.TryGetValue(st.A, out var i) && i.Team == BattleContext.PlayerTeam ? i.Id : "";
                                if (id == "hota") hotaDone = true;
                                if (id == "borg") borgDone = true;
                                if (id == "hiyo")
                                {
                                    hiyoH++; hiyoHAt[ti]++; roundsWithHiyo++;
                                    if (burning.Contains(st.A)) { hiyoBurn++; hiyoBurnAt[ti]++; }
                                    if (hotaDone) hotaBeforeHiyo++; if (borgDone) borgBeforeHiyo++;
                                    // 燃えている味方（ヒヨ以外）のうち攻撃力最大（周回の頭）
                                    var c = alive.Where(x => x != st.A && Mine(x) && burning.Contains(x)).ToList();
                                    if (c.Count > 0)
                                    {
                                        int best = c.OrderByDescending(x => t.AtkAt.GetValueOrDefault((round, x))).ThenBy(x => t.Info[x].Slot).First();
                                        topAtk[Role(best)]++; topN++;
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
                double n = trs.Length;
                Console.WriteLine($"## {bn} × 九/新兵 × {BA.Scales[s].Name}");
                Console.WriteLine();
                Console.WriteLine($"- **Q0-4 決着T**: 平均 {trs.Average(x => x.Turns):F2} ／ 分布（1〜9・10+）{string.Join(" ／ ", turnsH.Skip(1).Select(x => Pct(x, trs.Length)))} %");
                Console.WriteLine($"- **Q0-3 ヒヨ**: 手番/戦 {hiyoH / n:F2} ／ 手番の頭で自分が燃えている {Pct(hiyoBurn, hiyoH)}%（周回1〜4: {string.Join(" ／ ", Enumerable.Range(1, 4).Select(k => Pct(hiyoBurnAt[k], hiyoHAt[k])))}）／ その周回に先に動いていた: ホタ {Pct(hotaBeforeHiyo, roundsWithHiyo)}% ・ボルグ {Pct(borgBeforeHiyo, roundsWithHiyo)}%");
                Console.WriteLine($"- **Q0-5 ギフトの相手（燃えている味方のうち周回の頭の攻撃力が最大）**: ボルグ {Pct(topAtk[0], topN)} ／ ホタ {Pct(topAtk[1], topN)} ／ 相方 {Pct(topAtk[3], topN)} %");
                Console.WriteLine();
                Console.WriteLine("**Q0-2 周回ごと**: 燃えている敵（周回の頭の写し）／ 生きている敵、1戦あたりの「当たった敵（一振りごとに数える）」と「そのうち当たる前から燃えていた敵」＝ F1 の育ちの上限（燃えていない駒は育たないので上限）。");
                Console.WriteLine();
                Console.WriteLine("| 周回 | 周回に入った戦 % | 燃えている敵 ／ 生きている敵（平均） | ボルグ 燃えていた ／ 当たった | ホタ 燃えていた ／ 当たった | 相方 燃えていた ／ 当たった |");
                Console.WriteLine("|---|---|---|---|---|---|");
                for (int k = 1; k <= 6; k++)
                    Console.WriteLine($"| {k} | {Pct(rounds[k], trs.Length)} | {Avg(foeHead[k], rounds[k])} ／ {Avg(foeAlive[k], rounds[k])} | {hitB[0, k] / n:F2} ／ {hitAll[0, k] / n:F2} | {hitB[1, k] / n:F2} ／ {hitAll[1, k] / n:F2} | {hitB[3, k] / n:F2} ／ {hitAll[3, k] / n:F2} |");
                Console.WriteLine();
            }
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    // =================================================================================
    // 測定（段1 → 段2）
    // =================================================================================
    static void GiftRun()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第241期 `firescale gift` —— 段1・段2");
        Console.WriteLine();

        // ---------------- 段1 ----------------
        var s1Boards = new[] { "T3", "T3-1" };
        var s1 = new Dictionary<(string B, int S), GAgg[]>();
        foreach (var bn in s1Boards)
            for (int s = 0; s < BA.Scales.Length; s++)
                s1[(bn, s)] = GiftReplayAll(Traces(BoardOf(bn), BA.MainWave, BA.Scales[s].Sc), GiftRules, false);

        Console.WriteLine("## 段1 全96組 × T3・T3-1 × 九/新兵（倍率3）");
        Console.WriteLine();
        Console.WriteLine("**T2** ＝ 2 周回目までにギフトが出た戦 %（主な物差し）・**T3** ＝ 3 周回目まで。初回 ＝ 初めてギフトが出た周回の平均（出た戦）。相手/戦 ＝ 1戦に渡した相手の数（1体 ／ 2体のギフトの回数）。"
            + "大技/戦 ＝ ボルグの放つ ／ ホタの全体（括弧はそのうちギフトの手番）。ヒヨ 3+ ＝ ヒヨの手番の頭で火勢 3 以上の割合。時計 ＝ 燃えている味方の周回2 以降で 影 ＝ min(周回, 4) の %・r ＝ 影と周回の相関。");
        Console.WriteLine();
        foreach (var bn in s1Boards)
            for (int s = 0; s < BA.Scales.Length; s++)
            {
                var ag = s1[(bn, s)];
                Console.WriteLine($"### {bn} × 九/新兵 × {BA.Scales[s].Name}（決着T {(double)ag[0].Turns / ag[0].N:F2}）");
                Console.WriteLine();
                Console.WriteLine("| 組 | **T2 %** | T3 % | 初回 | 相手/戦（1体 ／ 2体） | 大技/戦 放つ（ギフト） ／ 全体（ギフト） | ヒヨ 3+ % | 時計 % ／ r |");
                Console.WriteLine("|---|---|---|---|---|---|---|---|");
                for (int k = 0; k < GiftRules.Length; k++)
                {
                    var a = ag[k];
                    long hi3 = a.HiyoHist[3] + a.HiyoHist[4];
                    Console.WriteLine($"| {GiftRules[k].Name} | **{P1(GiftT2(a))}** | {Pct(a.GiftByT3, a.HiyoBattles)} | {Avg(FirstSum(a), a.GiftBattles)} | {RecipPer(a):F2}（{PerN(a.Gift1, a)} ／ {PerN(a.Gift2, a)}） | "
                        + $"{PerN(a.GiftRel + a.OwnRel, a)}（{PerN(a.GiftRel, a)}） ／ {PerN(a.GiftFull + a.OwnFull, a)}（{PerN(a.GiftFull, a)}） | {Pct(hi3, a.HiyoHands)} | {Clock2G(a)} ／ {R2(CorrOf(SumCorr(a.Corr, PRoles), 0))} |");
                }
                Console.WriteLine();
            }

        // 表D（段1）: G3 と G4 の対
        Console.WriteLine("## 表D' G3（即撃ち）と G4（待ち）の対 —— 段1 の全組で、ほかの札（F・HS・E・Y）を揃えて比べる");
        Console.WriteLine();
        Console.WriteLine("相手/戦（G4 − G3）の分布と、T2 %（G4 − G3）。正 ＝ 待つ方が多く渡す／早い。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 対の数 | 相手/戦の差 平均（最小〜最大） | G4 の方が多い対 | T2 % の差 平均（最小〜最大） |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var bn in s1Boards)
            for (int s = 0; s < BA.Scales.Length; s++)
            {
                var ag = s1[(bn, s)];
                var d = new List<double>(); var dt = new List<double>();
                for (int k = 0; k < GiftRules.Length; k++)
                {
                    var r = GiftRules[k]; if (r.G != 3) continue;
                    int k4 = Array.IndexOf(GiftRules, r with { G = 4 });
                    d.Add(RecipPer(ag[k4]) - RecipPer(ag[k])); dt.Add(GiftT2(ag[k4]) - GiftT2(ag[k]));
                }
                Console.WriteLine($"| {bn} | {BA.Scales[s].Name} | {d.Count} | {d.Average():+0.00;−0.00}（{d.Min():+0.00;−0.00}〜{d.Max():+0.00;−0.00}） | {d.Count(x => x > 0)} | {dt.Average():+0.0;−0.0}（{dt.Min():+0.0;−0.0}〜{dt.Max():+0.0;−0.0}） |");
            }
        Console.WriteLine();

        // 段1 の要約: F の段 × HS（G・E・Y で平均）
        Console.WriteLine("## 段1 の要約 —— T2 %（T3 × 九/新兵 × 200/200）を F の段 × HS で（G・E・Y の 8 組の平均 ／ 最大）");
        Console.WriteLine();
        Console.WriteLine("| F の段 | HS1 | HS2 | HS3 |");
        Console.WriteLine("|---|---|---|---|");
        {
            var ag = s1[("T3", 0)];
            for (int fs = 1; fs <= 4; fs++)
                Console.WriteLine($"| F{fs} | " + string.Join(" | ", new[] { 1, 2, 3 }.Select(hs =>
                {
                    var ks = Enumerable.Range(0, GiftRules.Length).Where(k => GiftRules[k].F == fs && GiftRules[k].HS == hs).ToArray();
                    return $"{ks.Average(k => GiftT2(ag[k])):F1} ／ {ks.Max(k => GiftT2(ag[k])):F1}";
                })) + " |");
        }
        Console.WriteLine();

        // ---------------- 組の選び方（予測と一緒に、測る前に固定した） ----------------
        // T3 × 九/新兵 × 200/200 で、(1) T2 % が高い順 → 相手/戦が多い順 → 列挙順 の上位 3 組、
        // (2) G × E の 4 通りそれぞれで同じ並びの最上位（重なれば次）、(3) 対照 F1・HS1・G3・E1・Y1。
        var main = s1[("T3", 0)];
        int[] Rank(IEnumerable<int> ks) => ks.OrderByDescending(k => GiftT2(main[k])).ThenByDescending(k => RecipPer(main[k])).ThenBy(k => k).ToArray();
        var picked = new List<int>();
        foreach (int k in Rank(Enumerable.Range(0, GiftRules.Length)).Take(3)) picked.Add(k);
        foreach (int g in new[] { 3, 4 })
            foreach (int e in new[] { 1, 2 })
            {
                int k = Rank(Enumerable.Range(0, GiftRules.Length).Where(k2 => GiftRules[k2].G == g && GiftRules[k2].E == e)).First(k2 => !picked.Contains(k2));
                picked.Add(k);
            }
        int ctl = Array.IndexOf(GiftRules, new GiftRule(1, 1, 3, 1, 1));
        if (!picked.Contains(ctl)) picked.Insert(0, ctl);
        var rules = picked.Select(k => GiftRules[k]).Append(new GiftRule(0, 1, 3, 1, 1)).ToArray();   // 末尾は参考（規則の外）: F0 ＝ 燃え広がりなしの対照
        Console.WriteLine("## 段2 の組（規則は測る前に固定: T3 × 九/新兵 × 200/200 で T2 % の上位 3 組 ＋ G × E の各最上位 ＋ 対照 F1・HS1・G3・E1・Y1）");
        Console.WriteLine();
        foreach (var r in rules) Console.WriteLine(r.F == 0 ? $"- {r.Name}（参考・選び方の規則の外: 燃え広がりを外した対照）" : $"- {r.Name}（T2 {P1(GiftT2(main[Array.IndexOf(GiftRules, r)]))}%）");
        Console.WriteLine();

        // ---------------- 段2 ----------------
        var boards = Boards.Select(b => (b.Name, F: b.F())).ToArray();
        var res = new Dictionary<(int B, int W, int S), GAgg[]>();
        for (int b = 0; b < boards.Length; b++)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    res[(b, w, s)] = GiftReplayAll(Traces(boards[b].F, w, BA.Scales[s].Sc), rules, true);
        GAgg Cond(int b, int ci, int k)
        {
            var a = new GAgg();
            foreach (int w in FeedConds[ci].Waves) a.Merge(res[(b, w, FeedConds[ci].Sc)][k]);
            return a;
        }
        bool Hiyo(int b) => boards[b].F.Occupied().Any(x => x.Def.Id == "hiyo");
        bool Burny(int b) => boards[b].F.Occupied().Any(x => x.Def.Id is "borg" or "hota" or "hiyo");
        var mainConds = new[] { 0, 1, 2, 3, 4, 5 };

        // 表A・B
        foreach (int ci in mainConds)
        {
            Console.WriteLine($"## 表A・B ギフト —— {FeedConds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("T2 ／ T3 ＝ 2（3）周回目までにギフトが出た戦 %。初回の周回 ＝ 初めてギフトが出た周回の分布（出なかった ／ 1 ／ 2 ／ 3 ／ 4 ／ 5+ %）。ギフト/戦（1体 ／ 2体）・相手/戦・相手なし（撃てる火勢なのに燃えている味方がいない）/戦。大技/戦 ＝ 放つ（ギフト ／ 自分）・全体（ギフト ／ 自分）。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 組 | **T2 %** ／ T3 % | 初回の周回（なし ／ 1 ／ 2 ／ 3 ／ 4 ／ 5+） | ギフト/戦（1体 ／ 2体）・相手/戦 ・相手なし | 放つ（ギフト ／ 自分） | 全体（ギフト ／ 自分） | ギフトで大技にならなかった相手/戦 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                if (!Hiyo(b)) continue;
                for (int k = 0; k < rules.Length; k++)
                {
                    var a = Cond(b, ci, k);
                    long hb = a.HiyoBattles;
                    string first = $"{Pct(a.FirstGift[0], hb)} ／ {Pct(a.FirstGift[1], hb)} ／ {Pct(a.FirstGift[2], hb)} ／ {Pct(a.FirstGift[3], hb)} ／ {Pct(a.FirstGift[4], hb)} ／ {Pct(a.FirstGift.Skip(5).Sum(), hb)}";
                    Console.WriteLine($"| {(k == 0 ? boards[b].Name : "")} | {rules[k].Name} | **{P1(GiftT2(a))}** ／ {Pct(a.GiftByT3, hb)} | {first} | {PerN(a.GiftEvents, a)}（{PerN(a.Gift1, a)} ／ {PerN(a.Gift2, a)}）・{RecipPer(a):F2} ・{PerN(a.GiftNoCand, a)} | "
                        + $"{PerN(a.GiftRel, a)} ／ {PerN(a.OwnRel, a)} | {PerN(a.GiftFull, a)} ／ {PerN(a.OwnFull, a)} | {PerN(a.GiftNormal, a)} |");
                }
            }
            Console.WriteLine();
        }

        // 表C・D
        foreach (int ci in new[] { 0, 1, 4 })
        {
            Console.WriteLine($"## 表C・D ギフトの相手とヒヨの火勢 —— {FeedConds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("相手 ＝ ギフトを渡した相手の内訳 %（ボルグ ／ ホタ ／ 相方）。ヒヨの火勢 ＝ ヒヨの手番の頭の火勢 0 ／ 1 ／ 2 ／ 3 ／ 4 %。周回1〜3 の平均 ＝ その周回のヒヨの手番の頭の火勢の平均。"
                + "ヒヨが育った回数/戦（HS1 煽り ／ HS2 味方の燃え広がり）。煽りの相手 %（ボルグ ／ ホタ ／ 相方）。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 組 | ギフトの相手 %（ボルグ ／ ホタ ／ 相方） | ヒヨの火勢 0〜4 % | ヒヨ 周回1 ／ 2 ／ 3 | ヒヨが育った/戦（HS1 ／ HS2） | 煽り/戦・相手 %（ボルグ ／ ホタ ／ 相方） |");
            Console.WriteLine("|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                if (!Hiyo(b)) continue;
                for (int k = 0; k < rules.Length; k++)
                {
                    var a = Cond(b, ci, k);
                    long gt = a.Recipients;
                    string at(int t) { long c = 0, sm = 0; for (int v = 0; v <= Max; v++) { c += a.HiyoAt[t, v]; sm += a.HiyoAt[t, v] * v; } return Avg(sm, c); }
                    Console.WriteLine($"| {(k == 0 ? boards[b].Name : "")} | {rules[k].Name} | {Pct(a.GiftTo[0], gt)} ／ {Pct(a.GiftTo[1], gt)} ／ {Pct(a.GiftTo[3], gt)} | {string.Join(" ／ ", a.HiyoHist.Select(x => Pct(x, a.HiyoHands)))} | "
                        + $"{at(1)} ／ {at(2)} ／ {at(3)} | {PerN(a.HsFeedG, a)} ／ {PerN(a.HsSpreadG, a)} | {PerN(a.Feeds, a)}・{Pct(a.FeedTo[0], a.Feeds)} ／ {Pct(a.FeedTo[1], a.Feeds)} ／ {Pct(a.FeedTo[3], a.Feeds)} |");
                }
            }
            Console.WriteLine();
        }

        // 表E（＋ 燃え広がり）
        foreach (int ci in new[] { 0, 1, 4 })
        {
            Console.WriteLine($"## 表E 駒ごとの火勢・同時に火勢4・余熱・燃え広がり —— {FeedConds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("周回の終わりの火勢 平均（周回1 ／ 2 ／ 3）／ 火勢4 の周回 % ／ 届4 ＝ 4 に届いた駒 %（届いた周回の平均）。同時に火勢4 ＝ 周回の終わりに火勢 4 の味方の数 0 ／ 1 ／ 2 ／ 3 ／ 4+ %。"
                + "燃え広がり/戦 ＝ F1 で育った回数（ボルグ ／ ホタ ／ 相方）・燃えていず捨てた。余熱 ＝ 4 の上で余った量/戦・ボルグの切った量/戦と余熱で育った回数/戦。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 組 | ボルグ | ホタ | 相方 | 同時に火勢4 % | 燃え広がり/戦（ボルグ ／ ホタ ／ 相方 ・捨て） | 余熱/戦 ・切った量 ／ 余熱で育つ |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                if (!Burny(b)) continue;
                for (int k = 0; k < rules.Length; k++)
                {
                    var a = Cond(b, ci, k);
                    string U(int ro) => a.UnitN[ro] == 0 ? "—" : $"{Avg(a.RSum[ro, 1], a.RCnt[ro, 1])} ／ {Avg(a.RSum[ro, 2], a.RCnt[ro, 2])} ／ {Avg(a.RSum[ro, 3], a.RCnt[ro, 3])} ・4: {Pct(a.Eq4[ro], a.SCnt[ro])} ・届 {Pct(a.Reach4[ro], a.UnitN[ro])}（{Avg(a.Reach4T[ro], a.Reach4[ro])}）";
                    Console.WriteLine($"| {(k == 0 ? boards[b].Name : "")} | {rules[k].Name} | {U(0)} | {U(1)} | {U(3)} | {string.Join(" ／ ", a.Simul4.Select(x => Pct(x, a.RoundsP)))} | "
                        + $"{PerN(a.F1By[0], a)} ／ {PerN(a.F1By[1], a)} ／ {PerN(a.F1By[3], a)} ・{PerN(a.F1Waste, a)} | {PerN(a.OverAmt, a)} ・{PerN(a.Heat, a)} ／ {PerN(a.HeatG, a)} |");
                }
            }
            Console.WriteLine();
        }

        // 表F 時計
        foreach (int ci in new[] { 0, 4 })
        {
            Console.WriteLine($"## 表F 時計に戻っていないか —— {FeedConds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("燃えている味方の周回2 以降で 影 ＝ min(周回, 4) の %（ボルグ ／ ホタ ／ 相方 ／ 全体）と、影と周回の相関 r（役ごと ／ 全体）。第240期 W1・Y1・30・B+ は同じ標本の比較（F と HS とギフトを外した土台）。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 組 | 時計 %（ボルグ ／ ホタ ／ 相方 ／ 全体） | r（ボルグ ／ ホタ ／ 相方 ／ 全体） |");
            Console.WriteLine("|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                if (!Burny(b)) continue;
                for (int k = 0; k < rules.Length; k++)
                {
                    var a = Cond(b, ci, k);
                    Console.WriteLine($"| {(k == 0 ? boards[b].Name : "")} | {rules[k].Name} | {Pct(a.ClockHit2[0], a.ClockN2[0])} ／ {Pct(a.ClockHit2[1], a.ClockN2[1])} ／ {Pct(a.ClockHit2[3], a.ClockN2[3])} ／ {Clock2G(a)} | "
                        + $"{R2(CorrOf(a.Corr, 0))} ／ {R2(CorrOf(a.Corr, 1))} ／ {R2(CorrOf(a.Corr, 3))} ／ {R2(CorrOf(SumCorr(a.Corr, PRoles), 0))} |");
                }
            }
            Console.WriteLine();
        }

        // 表G 絵
        foreach (int ci in new[] { 0, 4 })
        {
            Console.WriteLine($"## 表G 影の絵の単調さ —— {FeedConds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("第239〜240期の絵に影の型を足した: ボルグ ＝ 放つ／通常、ホタ ＝ 単体／貫き／貫き＋着火／全体、ヒヨ ＝ 煽り→相手／ギフト→相手／煽りなし。**ギフトの手番は受け取った駒の列に「ギフトの手番／大技か通常」として足した**（台本の出来事は無い）。"
                + "参考の台（燃焼の3体がいない）は影の型が付かない。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 組 | 駒 | 手番/戦 | 絵の種類 | 最長（最大） | 最多の割合 | 続けて同じ絵 | 骨格の種類 | 骨格の最多 | 最も多い絵（全戦での割合） |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                var ks = Burny(b) ? Enumerable.Range(0, rules.Length) : new[] { 0 };
                foreach (int k in ks)
                {
                    var a = Cond(b, ci, k);
                    var ids = Burny(b) ? new[] { "borg", "hota", "hiyo" } : a.Pics.Keys.OrderBy(x => a.Pics[x].Name).ToArray();
                    bool firstRow = true;
                    foreach (var id in ids)
                    {
                        if (!a.Pics.TryGetValue(id, out var pa) || pa.Battles == 0) continue;
                        var top3 = pa.Freq.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key}（{100.0 * kv.Value / pa.Hands:F0}%）");
                        Console.WriteLine($"| {(k == 0 && firstRow ? boards[b].Name : "")} | {(firstRow ? (Burny(b) ? rules[k].Name : "（影なし）") : "")} | {pa.Name} | {(double)pa.Hands / pa.Battles:F2} | {Avg(pa.DistinctSum, pa.Battles)} | {Avg(pa.RunSum, pa.Battles)}（{pa.RunMax}） | "
                            + $"{TopShare(pa)} | {Pct(pa.Repeats, pa.Pairs)} | {Avg(pa.BoneDistinctSum, pa.Battles)} | {BoneTopShare(pa)} | {string.Join("<br>", top3)} |");
                        firstRow = false;
                    }
                }
            }
            Console.WriteLine();
        }

        // 表H 決着T
        Console.WriteLine("## 表H 決着ターンの分布（台 × 条件・組に依らない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 条件 | 平均 | 1 ／ 2 ／ 3 ／ 4 ／ 5 ／ 6 ／ 7 ／ 8 ／ 9 ／ 10+ % | 勝率 % |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int b = 0; b < boards.Length; b++)
            foreach (int ci in mainConds)
            {
                var a = Cond(b, ci, 0);
                Console.WriteLine($"| {boards[b].Name} | {FeedConds[ci].Name} | {Avg(a.Turns, a.N)} | {string.Join(" ／ ", a.TurnHist.Skip(1).Select(x => Pct(x, a.N)))} | {Pct(a.Wins, a.N)} |");
            }
        Console.WriteLine();

        // 表I 敵の燃焼
        Console.WriteLine("## 表I 敵の燃焼 —— 周回ごとの燃えている敵の数（周回の頭の写し ／ 周回の終わり ／ 生きている敵・組に依らない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 条件 | 周回1 | 周回2 | 周回3 | 周回4 | 周回5 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int b = 0; b < boards.Length; b++)
            foreach (int ci in mainConds)
            {
                var a = Cond(b, ci, 0);
                Console.WriteLine($"| {boards[b].Name} | {FeedConds[ci].Name} | " + string.Join(" | ", Enumerable.Range(1, 5).Select(t =>
                    a.FoeRounds[t] == 0 ? "—" : $"{Avg(a.FoeBurnHead[t], a.FoeRounds[t])} ／ {Avg(a.FoeBurnEnd[t], a.FoeRounds[t])} ／ {Avg(a.FoeAliveEnd[t], a.FoeRounds[t])}")) + " |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    static long FirstSum(GAgg a) { long s = 0; for (int t = 1; t <= GAgg.TM; t++) s += a.FirstGift[t] * t; return s; }
    static string Clock2G(GAgg a)
    {
        long n = PRoles.Sum(r => a.ClockN2[r]), h = PRoles.Sum(r => a.ClockHit2[r]);
        return n == 0 ? "—" : (100.0 * h / n).ToString("F1");
    }

    // =================================================================================
    // 自己検査
    // =================================================================================
    static void GiftCheck()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int ok = 0, all = 0;
        void Ok(bool c, string what) { all++; if (c) ok++; Console.WriteLine($"- {(c ? "○" : "×")} {what}"); }
        Console.WriteLine("# 第241期 `firescale giftcheck`");
        Console.WriteLine();

        // (a) 規則（盤の外で1駒ずつ）
        {
            var f = new GiftFeeder(new GiftRule(1, 1, 3, 1, 1));
            f.Keep(1, true); Ok(f.Of(1) == 1, "F1 では開幕の着火も 1");
            var g = new GiftFeeder(new GiftRule(2, 1, 3, 1, 1));
            g.Keep(1, true); g.Keep(2, false); Ok(g.Of(1) == 2 && g.Of(2) == 1, "F2: 開幕の着火は 2・ほかの着火は 1");
            g.Keep(2, false); Ok(g.Of(2) == 1, "F2: 開幕でない着火は 1 以上を上げない");
            var h = new GiftFeeder(new GiftRule(4, 1, 3, 1, 1)); h.Keep(1, false);
            h.Grow(1); bool a1 = h.Of(1) == 2; h.Grow(1); Ok(a1 && h.Of(1) == Max, "F4: 1 → 2（+1）→ 4（火勢2以上は +2）");
            var k = new GiftFeeder(new GiftRule(3, 1, 3, 1, 1)); k.Keep(1, false); k.Grow(1, 2);
            Ok(k.Of(1) == 3, "F3: 煽り +2 で 1 → 3");
            k.Grow(1, 2); Ok(k.Of(1) == Max && k.OverAmt == 1, "上限 4・溢れた 1 は余熱");
            k.Wither(new[] { 1 }); k.Wither(new[] { 1 }); Ok(k.Of(1) == 3, "W1: 育った周回は萎まず、育たない周回で −1");
            Ok(new GiftFeeder(new GiftRule(1, 1, 3, 1, 1)).Grow(1) == 2, "燃えていない（0）駒は育たない");
        }

        // (b) 盤面に触らない・乱数を引かない
        {
            string root = FindRoot();
            string mine = File.ReadAllText(Path.Combine(root, "BattleSim", "Modes", "FireScale.Gift.cs"));
            string rx = "ctx" + @"\.(Roll|PickOne)" + @"\(", nr = "new " + "Random(";
            Ok(!Regex.IsMatch(mine, rx) && !mine.Contains(nr), "器具（`FireScale.Gift.cs`）は乱数を引かない（ctx の Roll / PickOne と Random の生成を使わない）");
            long n = 0, diff = 0;
            foreach (var (bn, bf) in Boards)
            {
                var f = bf();
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int seed = 0; seed < 10; seed++)
                    {
                        var (r, p, e) = Fight(f, w, BA.Scales[0].Sc, seed);
                        int evc = r.Events.Count, hc = r.Hands.Count; string lg = string.Join("\n", r.Log.Select(l => l.Text));
                        var tr = BuildTrace(r, p, e);
                        foreach (var rule in GiftRules) GiftReplay(tr, rule, new GAgg(), true);
                        n++;
                        if (r.Events.Count != evc || r.Hands.Count != hc || string.Join("\n", r.Log.Select(l => l.Text)) != lg) diff++;
                        var (r2, _, _) = Fight(f, w, BA.Scales[0].Sc, seed);
                        if (r2.Events.Count != evc || r2.PlayerWon != r.PlayerWon || r2.Turns != r.Turns) diff++;
                    }
            }
            Ok(diff == 0, $"影を 96 組ぶん数えた後も台本・枠・ログが同じ／同じ seed の戦がもう一度同じになる {n - diff} ／ {n}");
        }

        // (c) 盤の上で: F1・ギフト・E1
        {
            long hitBad = 0, hitN = 0, giftBad = 0, giftN = 0, bigBad = 0, bigN = 0, e2Own = 0, recipBad = 0;
            var f = T3;
            foreach (int s in new[] { 0, 1, 2 })
                for (int seed = 0; seed < 40; seed++)
                {
                    var (r, p, e) = Fight(f, BA.MainWave, BA.Scales[s].Sc, seed);
                    var tr = BuildTrace(r, p, e);
                    // F1: 当たった敵が「当たる前から燃えていた」かを台本から直に読み直す（周回の頭の写し ＋ それまでの着火 − 倒れた）
                    var ev = r.Events;
                    var burning = new HashSet<int>(); var truth = new List<(int, int, bool)>();
                    var mine = p.Select(u => u.InstanceId).ToHashSet();
                    foreach (var x in ev)
                    {
                        if (x.Kind == BattleEventKind.TurnStart)
                            burning = ev.Where(y => y.Kind == BattleEventKind.StatusSnapshot && y.Turn == x.Turn && y.Text == BurnLabel && y.TargetId is int).Select(y => y.TargetId!.Value).ToHashSet();
                        else if (x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Burn && x.TargetId is int bt) burning.Add(bt);
                        else if (x.Kind == BattleEventKind.Death && x.TargetId is int dt) burning.Remove(dt);
                        else if (x.Kind == BattleEventKind.FireArmor && x.Text == FireArmorLabels.Smolder && x.TargetId is int smt) burning.Remove(smt);
                        else if (x.Kind == BattleEventKind.Damage && x.Pattern is not null && !x.Relayed && !x.FriendlyFire && x.ActorId is int ha && mine.Contains(ha) && x.TargetId is int ht && !mine.Contains(ht))
                            truth.Add((ha, ht, burning.Contains(ht)));
                    }
                    foreach (var rule in new[] { new GiftRule(1, 3, 3, 1, 1), new GiftRule(4, 3, 4, 1, 4), new GiftRule(3, 2, 3, 2, 1) })
                    {
                        var log = new GiftLog();
                        GiftReplay(tr, rule, new GAgg(), false, log);
                        hitN += truth.Count;
                        if (log.Hits.Count != truth.Count) hitBad++;
                        else for (int i = 0; i < truth.Count; i++) if (log.Hits[i] != truth[i]) hitBad++;
                        foreach (var gl in log.Gifts)
                        {
                            giftN++;
                            if (gl.HiyoS < 3 || (rule.G == 4 && gl.HiyoS < 4)) giftBad++;
                            if (gl.To.Length != (gl.HiyoS >= 4 ? Math.Min(2, gl.To.Length) : 1) || gl.To.Length == 0) recipBad++;
                            for (int i = 1; i < gl.Atk.Length; i++) if (gl.Atk[i] > gl.Atk[i - 1]) giftBad++;
                        }
                        foreach (var bg in log.Bigs)
                        {
                            bigN++;
                            if (rule.E == 1 && !bg.What.StartsWith("ギフト")) bigBad++;
                            if (rule.E == 2 && !bg.What.StartsWith("ギフト")) e2Own++;
                        }
                    }
                }
            Ok(hitBad == 0 && hitN > 0, $"F1: 足跡の「当たった敵が当たる前から燃えていたか」＝ 台本から直に読み直した値（不一致 {hitBad} ／ 当たり {hitN}）");
            Ok(giftBad == 0 && giftN > 0, $"ギフトはヒヨの火勢 3 以上（G4 は 4）で、相手は攻撃力の高い順（違反 {giftBad} ／ ギフト {giftN}）");
            Ok(recipBad == 0, $"相手は火勢 3 で1体・4 で2体まで（違反 {recipBad}）");
            Ok(bigBad == 0 && bigN > 0, $"E1 ではギフトの手番以外で大技が出ない（違反 {bigBad} ／ 大技 {bigN}）・E2 では自分の手番の大技も出る（{e2Own}）");
        }
        {
            // 撃った後ヒヨは 1: 同じ周回の中で次のヒヨの手番の頭の火勢を見る代わりに、ギフトの直後の値を再生の中で確かめる
            var tr = new Trace { Turns = 1 };
            tr.Info[1] = ("hiyo", "ヒヨ", BattleContext.PlayerTeam, 0); tr.Info[2] = ("hota", "ホタ", BattleContext.PlayerTeam, 1);
            tr.Info[3] = ("borg", "ボルグ", BattleContext.PlayerTeam, 2); tr.Info[9] = ("x", "敵", BattleContext.EnemyTeam, 0);
            foreach (int x in new[] { 1, 2, 3, 9 }) tr.Alive0.Add(x);
            tr.BurnAt[1] = new HashSet<int> { 1, 2, 3, 9 }; tr.BurnAt[2] = new HashSet<int> { 1, 2, 3, 9 };
            tr.AtkAt[(1, 2)] = 24; tr.AtkAt[(1, 3)] = 18; tr.AtkAt[(2, 2)] = 24; tr.AtkAt[(2, 3)] = 18;
            foreach (var x in new[] { 1, 2, 3 }) tr.Steps.Add(new Step(StepK.Ignite, x, 0));
            tr.Steps.Add(new Step(StepK.Round, 1, 0));
            for (int i = 0; i < 4; i++) { tr.HandPics.Add((2, "p", "b")); tr.Steps.Add(new Step(StepK.Swing, 2, 0)); tr.Steps.Add(new Step(StepK.Hit, 2, 9)); }
            tr.HandPics.Add((1, "p", "b")); tr.Steps.Add(new Step(StepK.Hand, 1, 4));
            tr.Steps.Add(new Step(StepK.Round, 2, 0));
            tr.HandPics.Add((1, "p", "b")); tr.Steps.Add(new Step(StepK.Hand, 1, 5));
            var log = new GiftLog();
            GiftReplay(tr, new GiftRule(1, 2, 3, 1, 1), new GAgg(), false, log);
            // ホタの一振り 4 回（燃えている敵に当たる）→ ホタ 1→4・HS2 でヒヨ 1→4 → 周回1 のヒヨの手番でギフト（2体）→ ヒヨ 1
            bool ok1 = log.Gifts.Count >= 1 && log.Gifts[0].HiyoS == 4 && log.Gifts[0].To.Length == 2 && log.Gifts[0].To[0] == 2 && log.Gifts[0].ToS[0] == 4;
            bool ok2 = log.Bigs.Any(b => b.What == "ギフト全体" && b.Id == 2);
            // 周回2 のヒヨの手番: 撃った後 1 に戻ったので、W1 で萎んでも 1 のまま → 撃てない
            bool ok3 = log.Gifts.Count == 1;
            Ok(ok1 && ok2 && ok3, "組んだ足跡で: ホタの一振り×4（燃えている敵）→ ホタ 4・HS2 でヒヨ 4 → ギフト 2 体（攻撃力順でホタが先・ホタは全体）→ 撃った後ヒヨは 1 に戻り、次の手番では撃てない");
        }

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {all}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
