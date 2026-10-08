using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// =====================================================================================
// firescale —— 第239期「規定化と、火勢の棚卸し（手番の絵の単調さも測る）」。
// 指示書は design/PHASE239_FIRE_SCALE_SPEC.md ／ 報告は design/PHASE239_FIRE_SCALE.md。
// **新しい機構は足さない**（前段の規定化＝ボルグ A+D2・ヒヨ V1 を除く）。火勢は**影の計算**だけで見積もる。
//
// 影の火勢・手番の絵は、**戦闘が終わった後に台本（`BattleResult.Events`）と手番の枠（`BattleResult.Hands`・計数のみ）を読んで数える**。
// engine の側には何も持たせない（盤面には原理的に影響しない）。着火は `StatusGain`（burn）の1件 ＝ `Ignite` の1回。
//
//     dotnet run --project BattleSim -c Release 0 firescale moved <旧 balance.md>   # 前段: 規定化で動いた行の一覧（受け入れ 1）
//     dotnet run --project BattleSim -c Release 0 firescale phase0                 # Q0-1〜Q0-4（§3.1 の一覧・影の規則の数え方・手番の枠・T3-1 の席）
//     dotnet run --project BattleSim -c Release 0 firescale run                    # 表A〜E（§3.2〜3.4）
//     dotnet run --project BattleSim -c Release 0 firescale check                  # 自己検査（受け入れ 2・3）
//     dotnet run --project BattleSim -c Release 0 firescale digest [path]          # 台本の指紋（手番の枠を足す前後で突き合わせる）
//     dotnet run --project BattleSim -c Release 0 firescale log <台> [seed] [波] [倍率 0/1/2]
// =====================================================================================
static partial class FireScaleDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        _args = args;
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "moved": Moved(); return;
            case "phase0": Phase0(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "digest": Digest(); return;
            case "feed0": Feed0(); return;   // 第240期
            case "feed": FeedRun(); return;
            case "feedcheck": FeedCheck(); return;
            case "gift0": Gift0(); return;   // 第241期
            case "gift": GiftRun(); return;
            case "giftcheck": GiftCheck(); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "T3", args.Length > 4 ? int.Parse(args[4]) : 0,
                    args.Length > 5 ? int.Parse(args[5]) : BA.MainWave, args.Length > 6 ? int.Parse(args[6]) : 0);
                return;
            default:
                Console.WriteLine("firescale: モードは moved / phase0 / run / check / digest / log ／ feed0 / feed / feedcheck（第240期）／ gift0 / gift / giftcheck（第241期）。");
                return;
        }
    }
    static string[]? _args;
    static partial void Moved();
    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 台（§4）。駒は**規定**（前段の後のボルグ・ヒヨ）。
    // ---------------------------------------------------------------------------------
    /// <summary>T3: 第238期の最良（A+D2+V1 の T3′-1・前1 ヒヨ ／ 前3 ホタ ／ 中央 ボルグ ／ 後1 ドハ ／ 後3 ソラ）。</summary>
    internal static Formation T3 => Formation.Build(front1: UnitCatalog.HiyoL0, front3: UnitCatalog.HotaL0, center: UnitCatalog.BorgL0,
        back1: UnitCatalog.Doha, back3: UnitCatalog.SoraSR0);
    /// <summary>雷＋ボルグ: ポンの席の前1 シガ → ボルグ（席はそのまま）。</summary>
    internal static Formation ThunderBorg => Formation.Build(front1: UnitCatalog.BorgL0, front3: UnitCatalog.Tsugi,
        center: UnitCatalog.Beni, back1: UnitCatalog.KataS3, back3: UnitCatalog.Mio);
    internal static readonly UnitDef[] T31Members = { UnitCatalog.BorgL0, UnitCatalog.HotaL0, UnitCatalog.HiyoL0, UnitCatalog.Shio, UnitCatalog.Sasa };

    /// <summary>T3-1 の席（規定で 120 通りを総当たり・並びは 200/200 → 400/300 → 落ちた駒 → 決着T → 列挙順）。一度だけ決める。</summary>
    static Formation? _t31;
    internal static Formation T31 => _t31 ??= PickSeat(T31Members).F;

    internal static (string Name, Func<Formation> F)[] Boards =>
    new (string, Func<Formation>)[]
    {
        ("T3", () => T3),
        ("T3-1", () => T31),
        ("雷＋ボルグ", () => ThunderBorg),
        ("参考 移動", () => BA.RefMove),
        ("参考 雷", () => BA.RefThunder),
    };
    internal static Formation BoardOf(string name) => Boards.First(b => b.Name == name).F();

    // ---------------------------------------------------------------------------------
    // 席の総当たり（規定の駒で。第238期 `Best4` と同じ並び）
    // ---------------------------------------------------------------------------------
    internal static BA.SeatScore ScoreOf(Formation f, int seed0, int seeds, EnemyScaleRule sc)
    {
        int sv = 0, fell = 0, w = 0; long t = 0;
        for (int i = 0; i < seeds; i++)
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BA.WaveOf(BA.MainWave, sc)(), seed0 + i, verbose: false, ember: EmberRule.Pre256);
            fell += r.PlayerStarterFallen.Count;
            if (!r.PlayerWon) continue;
            w++; t += r.Turns;
            if (r.PlayerStarterFallen.Count == 0) sv++;
        }
        return new BA.SeatScore(sv, fell, w, t);
    }

    internal static (Formation F, BA.SeatScore S, int Sv4, int Rank, int Tied) PickSeat(IReadOnlyList<UnitDef> members)
    {
        var perms = SeroDiag.Permute(members).Select(BA.Seat).ToArray();
        var s2 = new BA.SeatScore[perms.Length];
        Parallel.For(0, perms.Length, i => s2[i] = ScoreOf(perms[i], BA.PickSeed0, BA.PickSeeds, BA.Scales[0].Sc));
        int max = s2.Max(x => x.Sv);
        var tied = Enumerable.Range(0, perms.Length).Where(i => s2[i].Sv == max).ToArray();
        var sv4 = new int[tied.Length];
        Parallel.For(0, tied.Length, k => sv4[k] = ScoreOf(perms[tied[k]], BA.PickSeed0, BA.PickSeeds, BA.Scales[1].Sc).Sv);
        int bestK = 0;
        for (int k = 1; k < tied.Length; k++)
        {
            long a = Key4(s2[tied[k]], sv4[k]), b = Key4(s2[tied[bestK]], sv4[bestK]);
            if (a > b) bestK = k;
        }
        long bestKey = Key4(s2[tied[bestK]], sv4[bestK]);
        int same = Enumerable.Range(0, tied.Length).Count(k => Key4(s2[tied[k]], sv4[k]) == bestKey);
        return (perms[tied[bestK]], s2[tied[bestK]], sv4[bestK], tied[bestK], same);
    }
    static long Key4(BA.SeatScore s, int sv4) => (long)s.Sv * 1_000_000_000_000L + (long)sv4 * 1_000_000_000L + (s.Key - (long)s.Sv * 1_000_000_000L);

    // ---------------------------------------------------------------------------------
    // 戦闘（規定の駒のまま・固定しない）
    // ---------------------------------------------------------------------------------
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E) Fight(Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BA.WaveOf(w, sc)();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose, ember: EmberRule.Pre256);
        return (r, p, e);
    }

    static void LogOne(string board, int seed, int wave, int sc)
    {
        var f = BoardOf(board);
        var (r, _, _) = Fight(f, wave, BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {board} × {BA.SeatsNamed(f)} × {BA.WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    static void Digest()
    {
        string outPath = _args is { Length: > 3 } ? _args[3] : "firescale_digest.txt";
        var lines = new List<string>();
        foreach (var (bn, bf) in Boards)
        {
            var f = bf();
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    for (int seed = 0; seed < 10; seed++)
                    {
                        var (r, _, _) = Fight(f, w, BA.Scales[s].Sc, seed);
                        lines.Add($"## {bn} 波{w} 倍率{s} seed{seed} won={r.PlayerWon} T={r.Turns}");
                        foreach (var l in r.Log) lines.Add(l.Text);
                        foreach (var ev in r.Events)
                            lines.Add($"E {ev.Kind} {ev.Turn} {ev.ActorId} {ev.TargetId} {ev.Amount} {ev.HpAfter} {ev.Text}");
                    }
        }
        File.WriteAllLines(outPath, lines);
        Console.WriteLine($"digest: {lines.Count} 行を {outPath} に書いた（台 {Boards.Length} × 波 {BA.WaveNames.Length} × 倍率 {BA.Scales.Length} × seed 10）");
    }

    // ---------------------------------------------------------------------------------
    // 影の火勢（§3.2）と手番の絵（§3.3）・発火見込み（§3.4）
    // ---------------------------------------------------------------------------------
    internal static readonly string[] RuleNames = { "K1", "K2", "K3" };
    internal static readonly string[] Roles = { "ボルグ", "ホタ", "ヒヨ", "相方", "敵" };
    internal const int Max = 4;
    static readonly string BurnLabel = StatusKeys.LabelOf(StatusKeys.Burn);

    internal static int RoleIx(string id, int team) => team != BattleContext.PlayerTeam ? 4 : id switch { "borg" => 0, "hota" => 1, "hiyo" => 2, _ => 3 };

    /// <summary>1つの規則の影の火勢（駒ごと）。</summary>
    sealed class Shadow
    {
        public readonly int Rule;
        public readonly Dictionary<int, int> S = new(), Streak = new();
        public readonly HashSet<int> Lit = new();   // この周回で点いた（点け直された）駒
        public Shadow(int rule) { Rule = rule; }
        public int Of(int id) => S.GetValueOrDefault(id);
        public void Ignite(int id)
        {
            if (Rule == 1 && Lit.Contains(id)) return;   // K2: 1周回に +1 まで
            int s = S.GetValueOrDefault(id);
            S[id] = s == 0 ? 1 : Math.Min(Max, s + 1);
            Lit.Add(id);
        }
        public void Decay(IEnumerable<int> alive)
        {
            foreach (int id in alive)
            {
                int s = S.GetValueOrDefault(id);
                if (Lit.Contains(id)) { Streak[id] = 0; continue; }
                if (s == 0) continue;
                if (Rule == 2)
                {
                    int k = Streak.GetValueOrDefault(id) + 1;
                    if (k >= 2) { S[id] = s - 1; k = 0; }
                    Streak[id] = k;
                }
                else S[id] = s - 1;
            }
            Lit.Clear();
        }
        public void Kill(int id) { S.Remove(id); Streak.Remove(id); Lit.Remove(id); }
    }

    /// <summary>集計（台 × 波 × 倍率）。</summary>
    internal sealed class Agg
    {
        public long N, Wins, AllSurv, Turns;
        // §3.2: [規則, 役]
        public readonly long[,] SSum = new long[3, 5], SCnt = new long[3, 5], Ge3 = new long[3, 5], Eq4 = new long[3, 5];
        public readonly long[,,] Hist = new long[3, 5, Max + 1];
        public readonly long[,] UnitN = new long[3, 5], Reach3 = new long[3, 5], Reach4 = new long[3, 5], Reach3T = new long[3, 5], Reach4T = new long[3, 5];
        public readonly long[,] ShadowNoBurn = new long[3, 5], BurnNoShadow = new long[3, 5], StartCnt = new long[3, 5], BurnStart = new long[3, 5];
        public readonly long[] Ignites = new long[5], IgnitesRelit = new long[5];
        // 敵への着火の出どころ: ボルグの手番の中（火の粉・焼き返し）／ ボルグの手番の外（火の鎧の返し）／ ほかの駒 ／ 出どころなし
        public readonly long[] FoeIgniteSrc = new long[4];
        public long FoeReach3, FoeReach3OutBorg;   // K1 で火勢3 に届いた敵（駒・戦）と、そのうち着火の過半がボルグの手番の外だった敵
        public const int TMax = 8;
        public readonly long[,,] TurnSum = new long[3, 5, TMax + 1], TurnCnt = new long[3, 5, TMax + 1];   // ターン（8 以上はまとめる）ごとの影
        // §3.3: 駒（Id。敵は "敵"）→ 絵
        public readonly Dictionary<string, PicAgg> Pics = new();
        // §3.4
        public readonly long[,] HotaStage = new long[3, Max + 1], BorgStage = new long[3, Max + 1];
        public readonly long[] Releases = new long[3], ReleaseBattles = new long[3], ReleaseFirstT = new long[3], BorgHands = new long[3], BorgBattles = new long[3];
        public readonly long[,] HiyoSum = new long[3, 6];   // 合計 0-1 / 2-3 / 4-5 / 6-7 / 8-9 / 10+
        public readonly long[] HiyoSumTotal = new long[3];
        public readonly long[] HiyoMax = new long[3];

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; Turns += o.Turns;
            AddA(SSum, o.SSum); AddA(SCnt, o.SCnt); AddA(Ge3, o.Ge3); AddA(Eq4, o.Eq4);
            for (int a = 0; a < 3; a++) for (int b = 0; b < 5; b++) for (int c = 0; c <= Max; c++) Hist[a, b, c] += o.Hist[a, b, c];
            AddA(UnitN, o.UnitN); AddA(Reach3, o.Reach3); AddA(Reach4, o.Reach4); AddA(Reach3T, o.Reach3T); AddA(Reach4T, o.Reach4T);
            AddA(ShadowNoBurn, o.ShadowNoBurn); AddA(BurnNoShadow, o.BurnNoShadow); AddA(StartCnt, o.StartCnt); AddA(BurnStart, o.BurnStart);
            for (int i = 0; i < 5; i++) { Ignites[i] += o.Ignites[i]; IgnitesRelit[i] += o.IgnitesRelit[i]; }
            for (int i = 0; i < 4; i++) FoeIgniteSrc[i] += o.FoeIgniteSrc[i];
            FoeReach3 += o.FoeReach3; FoeReach3OutBorg += o.FoeReach3OutBorg;
            for (int a = 0; a < 3; a++) for (int b = 0; b < 5; b++) for (int c = 0; c <= TMax; c++) { TurnSum[a, b, c] += o.TurnSum[a, b, c]; TurnCnt[a, b, c] += o.TurnCnt[a, b, c]; }
            foreach (var (k, v) in o.Pics) { if (!Pics.TryGetValue(k, out var x)) Pics[k] = x = new PicAgg(); x.Merge(v); }
            AddA(HotaStage, o.HotaStage); AddA(BorgStage, o.BorgStage); AddA(HiyoSum, o.HiyoSum);
            for (int i = 0; i < 3; i++)
            {
                Releases[i] += o.Releases[i]; ReleaseBattles[i] += o.ReleaseBattles[i]; ReleaseFirstT[i] += o.ReleaseFirstT[i];
                BorgHands[i] += o.BorgHands[i]; BorgBattles[i] += o.BorgBattles[i]; HiyoSumTotal[i] += o.HiyoSumTotal[i];
                HiyoMax[i] = Math.Max(HiyoMax[i], o.HiyoMax[i]);
            }
        }
        static void AddA(long[,] a, long[,] b) { for (int i = 0; i < a.GetLength(0); i++) for (int j = 0; j < a.GetLength(1); j++) a[i, j] += b[i, j]; }
    }

    /// <summary>手番の絵の集計（1つの駒）。</summary>
    internal sealed class PicAgg
    {
        public string Name = "";
        public long Battles, Hands, DistinctSum, RunSum, RunMax, TopSum;   // TopSum は「最も多い絵の手番数」（戦ごと）の合計
        public long BoneDistinctSum, BoneRunSum, BoneTopSum;
        public long Pairs, Repeats, BoneRepeats;   // 隣り合う2手番（同じ戦）のうち同じ絵 ／ 同じ骨格
        public readonly Dictionary<string, long> Freq = new(), BoneFreq = new();
        public void Merge(PicAgg o)
        {
            if (Name == "") Name = o.Name;
            Battles += o.Battles; Hands += o.Hands; DistinctSum += o.DistinctSum; RunSum += o.RunSum; RunMax = Math.Max(RunMax, o.RunMax); TopSum += o.TopSum;
            BoneDistinctSum += o.BoneDistinctSum; BoneRunSum += o.BoneRunSum; BoneTopSum += o.BoneTopSum;
            Pairs += o.Pairs; Repeats += o.Repeats; BoneRepeats += o.BoneRepeats;
            foreach (var (k, v) in o.Freq) Freq[k] = Freq.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.BoneFreq) BoneFreq[k] = BoneFreq.GetValueOrDefault(k) + v;
        }
        public void TakeBattle(List<(string Pic, string Bone)> seq)
        {
            if (seq.Count == 0) return;
            Battles++; Hands += seq.Count;
            for (int i = 1; i < seq.Count; i++) { Pairs++; if (seq[i].Pic == seq[i - 1].Pic) Repeats++; if (seq[i].Bone == seq[i - 1].Bone) BoneRepeats++; }
            Take(seq.Select(x => x.Pic).ToList(), Freq, ref DistinctSum, ref RunSum, ref TopSum, true);
            Take(seq.Select(x => x.Bone).ToList(), BoneFreq, ref BoneDistinctSum, ref BoneRunSum, ref BoneTopSum, false);
        }
        void Take(List<string> seq, Dictionary<string, long> freq, ref long distinct, ref long runSum, ref long topSum, bool trackMax)
        {
            var c = new Dictionary<string, int>();
            int run = 1, best = 1;
            for (int i = 0; i < seq.Count; i++)
            {
                c[seq[i]] = c.GetValueOrDefault(seq[i]) + 1;
                freq[seq[i]] = freq.GetValueOrDefault(seq[i]) + 1;
                if (i > 0) { run = seq[i] == seq[i - 1] ? run + 1 : 1; best = Math.Max(best, run); }
            }
            distinct += c.Count; runSum += best; topSum += c.Values.Max();
            if (trackMax) RunMax = Math.Max(RunMax, best);
        }
    }

    internal static string OutcomeName(TurnOutcome o) => o switch
    {
        TurnOutcome.Attack => "攻撃", TurnOutcome.Skill => "術", TurnOutcome.Charge => "溜め", _ => "何もしない",
    };
    internal static string PatternName(AttackPattern? p) => p switch
    {
        AttackPattern.Single => "単体", AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き", AttackPattern.All => "全体", _ => "—",
    };

    /// <summary>出来事 → 絵の札（手番の中で起きた出来事の種類）。攻撃・ダメージ・区切り・写しは数えない（どの手番にもある／表示用）。</summary>
    internal static string? TagOf(BattleEvent ev) => ev.Kind switch
    {
        BattleEventKind.Attack or BattleEventKind.Damage or BattleEventKind.TurnStart or BattleEventKind.StatusSnapshot or BattleEventKind.StatSnapshot => null,
        BattleEventKind.StatusGain => ev.Text == StatusKeys.Burn ? "着火" : "付与:" + (StatusKeys.All.Contains(ev.Text ?? "") ? StatusKeys.LabelOf(ev.Text!) : ev.Text),
        BattleEventKind.Heal => "回復",
        BattleEventKind.Move => "移動",
        BattleEventKind.Death => "撃破",
        BattleEventKind.FireArmor => ev.Text,
        // 第242期（火勢）: 段は数字つき・育ちは1語にまとめる。第241期までの盤面には出ない。
        BattleEventKind.FireLevel => ev.Text == FireLevelLabels.Stage ? "段" + ev.Amount
            : ev.Text is FireLevelLabels.GrowSpread or FireLevelLabels.GrowStoke or FireLevelLabels.GrowSelf ? "育つ" : ev.Text,
        BattleEventKind.Skill => null,   // 手番の種類（術）で数える
        _ => KindJa.TryGetValue(ev.Kind.ToString(), out var ja) ? ja : ev.Kind.ToString(),
    };

    /// <summary>台本の種類の読み替え（表示だけ・読み替えの無い種類は英名のまま）。</summary>
    static readonly Dictionary<string, string> KindJa = new()
    {
        ["Whet"] = "強化", ["Intercept"] = "庇い", ["Sealed"] = "封じ", ["Highlight"] = "見せ場", ["Stagger"] = "転倒", ["Stun"] = "痺れ",
        ["Regroup"] = "組み替え", ["Overflow"] = "溢れ", ["Parry"] = "受け流し", ["Evade"] = "回避", ["Thunder"] = "雷", ["Plank"] = "板",
        ["GurenGain"] = "紅蓮", ["GurenRelease"] = "紅蓮の奔流", ["InverseSip"] = "啜り", ["HealInverted"] = "反転", ["Status"] = "刻み",
        ["Confused"] = "混乱", ["Tailwind"] = "追い風", ["Blast"] = "吹っ飛ばし", ["Disarray"] = "乱れ", ["KillImpact"] = "撃破の衝撃",
    };

    /// <summary>1戦を読む。<paramref name="acc"/> に足す。</summary>
    internal static void Analyze(BattleResult r, List<UnitState> p, List<UnitState> e, Agg acc)
    {
        acc.N++; acc.Turns += r.Turns;
        if (r.PlayerWon) { acc.Wins++; if (r.PlayerStarterFallen.Count == 0) acc.AllSurv++; }
        var ev = r.Events;
        // 駒（InstanceId → Id・陣営）
        var info = new Dictionary<int, (string Id, string Name, int Team)>();
        foreach (var u in p.Concat(e)) info[u.InstanceId] = (u.Def.Id, u.Def.Name, u.TeamId);
        foreach (var x in ev)
            if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && !info.ContainsKey(sid))
                info[sid] = ("summon", "召喚", x.Team ?? (x.ActorId is int aid && info.TryGetValue(aid, out var ai) ? ai.Team : BattleContext.EnemyTeam));
        int Role(int id) => info.TryGetValue(id, out var i) ? RoleIx(i.Id, i.Team) : 4;
        var alive = new HashSet<int>(p.Concat(e).Select(u => u.InstanceId));
        // 燃えている駒（周回の頭の写し）: 周回 t → 集合
        var burnAt = new Dictionary<int, HashSet<int>>();
        foreach (var x in ev)
            if (x.Kind == BattleEventKind.StatusSnapshot && x.Text == BurnLabel && x.TargetId is int bt)
                (burnAt.TryGetValue(x.Turn, out var set) ? set : burnAt[x.Turn] = new HashSet<int>()).Add(bt);

        var sh = new[] { new Shadow(0), new Shadow(1), new Shadow(2) };
        var rel = new[] { new Shadow(0), new Shadow(1), new Shadow(2) };   // ボルグが火勢4で「放つ」と1に戻る版（§3.4）
        var reached3 = new Dictionary<int, int>[3]; var reached4 = new Dictionary<int, int>[3];
        for (int k = 0; k < 3; k++) { reached3[k] = new(); reached4[k] = new(); }
        var relCount = new int[3]; var relFirst = new int[3];
        var hiyoMax = new long[3];
        var borgSeen = false;

        // 手番の頭で読む: EventStart → 手番
        var hands = r.Hands.OrderBy(h => h.EventStart).ThenByDescending(h => h.EventEnd).ToList();
        var handsAt = hands.GroupBy(h => h.EventStart).ToDictionary(g => g.Key, g => g.ToList());

        // 各出来事がボルグの手番の枠の中か（敵への着火の出どころを分けるため）
        var inBorgHand = new bool[ev.Count];
        foreach (var h in hands)
            if (info.TryGetValue(h.ActorId, out var hi) && hi.Id == "borg" && hi.Team == BattleContext.PlayerTeam)
                for (int j = h.EventStart; j < h.EventEnd && j < ev.Count; j++) inBorgHand[j] = true;
        var foeIgn = new Dictionary<int, (int In, int Out)>();

        int round = 1;
        void Track(int k, int id, int turn)
        {
            int s = sh[k].Of(id);
            if (s >= 3 && !reached3[k].ContainsKey(id)) reached3[k][id] = turn;
            if (s >= 4 && !reached4[k].ContainsKey(id)) reached4[k][id] = turn;
        }
        void Finalize(int t)
        {
            // 周回の終わり（その周回の着火の後・萎む前）を写す
            for (int k = 0; k < 3; k++)
            {
                foreach (int id in alive)
                {
                    int ro = Role(id), s = sh[k].Of(id);
                    acc.SSum[k, ro] += s; acc.SCnt[k, ro]++; acc.Hist[k, ro, s]++;
                    if (s >= 3) acc.Ge3[k, ro]++;
                    if (s >= 4) acc.Eq4[k, ro]++;
                    int ti = Math.Min(Agg.TMax, t); acc.TurnSum[k, ro, ti] += s; acc.TurnCnt[k, ro, ti]++;
                }
                sh[k].Decay(alive); rel[k].Decay(alive);
            }
        }
        void StartRound(int t)
        {
            var burning = burnAt.GetValueOrDefault(t) ?? new HashSet<int>();
            for (int k = 0; k < 3; k++)
                foreach (int id in alive)
                {
                    int ro = Role(id); bool b = burning.Contains(id); int s = sh[k].Of(id);
                    acc.StartCnt[k, ro]++;
                    if (b) acc.BurnStart[k, ro]++;
                    if (s > 0 && !b) acc.ShadowNoBurn[k, ro]++;
                    if (s == 0 && b) acc.BurnNoShadow[k, ro]++;
                }
        }

        for (int i = 0; i <= ev.Count; i++)
        {
            if (handsAt.TryGetValue(i, out var hs))
                foreach (var h in hs)
                {
                    if (!info.TryGetValue(h.ActorId, out var ai) || ai.Team != BattleContext.PlayerTeam) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        if (ai.Id == "hota") acc.HotaStage[k, sh[k].Of(h.ActorId)]++;
                        if (ai.Id == "borg")
                        {
                            int s = rel[k].Of(h.ActorId);
                            acc.BorgStage[k, s]++; acc.BorgHands[k]++;
                            if (s >= Max)
                            {
                                relCount[k]++; if (relFirst[k] == 0) relFirst[k] = h.Turn;
                                rel[k].S[h.ActorId] = 1;   // 放つと自分の火勢は1に戻る（影の中だけ）
                            }
                        }
                        if (ai.Id == "hiyo")
                        {
                            long sum = alive.Where(id => info.TryGetValue(id, out var ui) && ui.Team == BattleContext.PlayerTeam).Sum(id => (long)sh[k].Of(id));
                            acc.HiyoSum[k, (int)Math.Min(5, sum / 2)]++; acc.HiyoSumTotal[k] += sum;
                            hiyoMax[k] = Math.Max(hiyoMax[k], sum);
                        }
                    }
                    if (ai.Id == "borg") borgSeen = true;
                }
            if (i == ev.Count) break;
            var x = ev[i];
            switch (x.Kind)
            {
                case BattleEventKind.TurnStart:
                    if (x.Turn >= 2) Finalize(round);
                    round = x.Turn;
                    // 写し（StatusSnapshot）はこの直後に積まれるが、写しの中身は周回の頭の在庫なので先に比べてよい
                    StartRound(round);
                    break;
                case BattleEventKind.StatusGain when x.Text == StatusKeys.Burn && x.TargetId is int tid:
                {
                    int ro = Role(tid);
                    acc.Ignites[ro]++;
                    if (ro == 4)
                    {
                        bool byBorg = x.ActorId is int wa && info.TryGetValue(wa, out var wi) && wi.Id == "borg" && wi.Team == BattleContext.PlayerTeam;
                        int src = x.ActorId is null ? 3 : !byBorg ? 2 : inBorgHand[i] ? 0 : 1;
                        acc.FoeIgniteSrc[src]++;
                        var fi = foeIgn.GetValueOrDefault(tid);
                        foeIgn[tid] = src == 1 ? (fi.In, fi.Out + 1) : (fi.In + 1, fi.Out);
                    }
                    if (sh[0].Of(tid) > 0) acc.IgnitesRelit[ro]++;
                    for (int k = 0; k < 3; k++) { sh[k].Ignite(tid); rel[k].Ignite(tid); Track(k, tid, Math.Max(1, x.Turn)); }
                    break;
                }
                case BattleEventKind.Death when x.TargetId is int did:
                    alive.Remove(did);
                    for (int k = 0; k < 3; k++) { sh[k].Kill(did); rel[k].Kill(did); }
                    break;
                case BattleEventKind.Revive when x.TargetId is int vid:
                    alive.Add(vid); break;
                case BattleEventKind.Summon when x.TargetId is int sid2:
                    alive.Add(sid2); break;
            }
        }
        Finalize(round);

        // 駒ごと: 届いたか
        foreach (var (id, i2) in info)
        {
            if (i2.Id == "summon") continue;
            int ro = RoleIx(i2.Id, i2.Team);
            for (int k = 0; k < 3; k++)
            {
                acc.UnitN[k, ro]++;
                if (reached3[k].TryGetValue(id, out int t3)) { acc.Reach3[k, ro]++; acc.Reach3T[k, ro] += t3; }
                if (reached4[k].TryGetValue(id, out int t4)) { acc.Reach4[k, ro]++; acc.Reach4T[k, ro] += t4; }
            }
        }
        foreach (var (id, _) in reached3[0])
            if (Role(id) == 4) { acc.FoeReach3++; var fi = foeIgn.GetValueOrDefault(id); if (fi.Out * 2 > fi.In + fi.Out) acc.FoeReach3OutBorg++; }
        for (int k = 0; k < 3; k++)
        {
            acc.Releases[k] += relCount[k];
            if (relCount[k] > 0) { acc.ReleaseBattles[k]++; acc.ReleaseFirstT[k] += relFirst[k]; }
            if (borgSeen) acc.BorgBattles[k]++;
            acc.HiyoMax[k] = Math.Max(acc.HiyoMax[k], hiyoMax[k]);
        }

        // §3.3 手番の絵（味方の駒ごと）
        var seqs = new Dictionary<int, List<(string, string)>>();
        foreach (var h in hands)
        {
            if (!info.TryGetValue(h.ActorId, out var ai) || ai.Team != BattleContext.PlayerTeam) continue;
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
            string pic = bone + "／" + (tags.Count == 0 ? "（なし）" : string.Join("・", tags));
            (seqs.TryGetValue(h.ActorId, out var l) ? l : seqs[h.ActorId] = new()).Add((pic, bone));
        }
        foreach (var (id, seq) in seqs)
        {
            var ai = info[id];
            if (!acc.Pics.TryGetValue(ai.Id, out var pa)) acc.Pics[ai.Id] = pa = new PicAgg { Name = ai.Name };
            pa.TakeBattle(seq);
        }
    }

    /// <summary>台 × 波 × 倍率 を seed 帯で（並列）。</summary>
    internal static Agg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var a = new Agg();
            var (r, p, e) = Fight(f, w, sc, seed0 + i);
            Analyze(r, p, e, a);
            parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    internal static string F1(double x) => BA.F1(x);
    internal static string F2(double x) => BA.F2(x);
    internal static string Pct(long a, long b) => b == 0 ? "—" : (100.0 * a / b).ToString("F1");
    internal static string Avg(long a, long b) => b == 0 ? "—" : ((double)a / b).ToString("F2");
}
