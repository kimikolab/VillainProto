using BattleCore;
using static Common;

// =====================================================================================
// drift モード（第222期） —— 移動軸の加速：シオに手番を・ヨミの割り込みを薙ぎに
//
// 指示書は design/PHASE222_DRIFT_SPEC.md ／ 報告は design/PHASE222_DRIFT.md。
// **線は置かない。** 版は札の差し替え（`Run` の引数は増やさない）。規定のシオ・ヨミは触らない。
//
//     dotnet run --project BattleSim -c Release 0 drift phase0   # Q0-1〜Q0-5（Q0-1 だけ戦闘を回す）
//     dotnet run --project BattleSim -c Release 0 drift run      # 表A〜E（台 D1〜D4 × 波 × 版 × 倍率）
//     dotnet run --project BattleSim -c Release 0 drift check    # 自己検査（受け入れ 2・3）
// =====================================================================================

static partial class DriftDiag
{
    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(); return;
            default:
                Console.WriteLine("drift: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunImpl(string arg);
    static partial void CheckImpl();

    internal const int Seeds = 200;

    // =================================================================================
    // 版（指示書 §4）
    // =================================================================================

    internal static UnitDef Copy(UnitDef d, TraitId[] traits, IReadOnlyList<UnitAction>? actions) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
        Pattern = d.Pattern, Actions = actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    internal const string RegroupLabel = "隊を組み替えた";
    // 第223期 前段で V3 が規定になったので、V0 は**規定から版の札を抜いた駒**に固定する（`drift run` が規定化の前と全文一致する）。
    internal static readonly UnitDef ShioV0 = Copy(UnitCatalog.Shio,
        UnitCatalog.Shio.Traits.Where(t => t != TraitId.Regroup).ToArray(), null);
    internal static readonly UnitDef ShioReg = Copy(ShioV0,
        ShioV0.Traits.Append(TraitId.Regroup).ToArray(),
        new UnitAction[] { new(ActionKind.Skill, Label: RegroupLabel) });
    internal static readonly UnitDef YomiV0 = Copy(UnitCatalog.Yomi,
        UnitCatalog.Yomi.Traits.Where(t => t != TraitId.CreakSweep).ToArray(), UnitCatalog.Yomi.Actions);
    internal static readonly UnitDef YomiS30 = Copy(YomiV0, YomiV0.Traits.Append(TraitId.CreakSweep).ToArray(), YomiV0.Actions);
    internal static readonly UnitDef YomiS20 = Copy(YomiV0, YomiV0.Traits.Append(TraitId.CreakSweep20).ToArray(), YomiV0.Actions);

    internal static readonly (string Tag, UnitDef Shio, UnitDef Yomi)[] Versions =
    {
        ("V0", ShioV0, YomiV0),
        ("V1", ShioReg, YomiV0),
        ("V2", ShioV0, YomiS30),
        ("V3", ShioReg, YomiS30),
        ("V3b", ShioReg, YomiS20),
    };

    /// <summary>編成のシオ・ヨミだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, UnitDef shio, UnitDef yomi)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied())
            g[slot] = d.Id == "shio" ? shio : d.Id == "yomi" ? yomi : d;
        return g;
    }

    // =================================================================================
    // 台（指示書 §7.1）
    // =================================================================================

    internal static Formation BenchD1 => Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald, center: UnitCatalog.Shio, back1: UnitCatalog.Sero, back3: UnitCatalog.Basa);
    internal static List<(string Name, Formation F)> CompareRowsWith() =>
        CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id is "shio" or "yomi")).ToList();

    // 波: 本編の第2〜5波 ＋ 検証・九 / 新兵 ＋ 検証・九 / 農兵
    internal static readonly string[] WaveNames = { "第二波", "第三波", "第四波", "第五波", "九/新兵", "九/農兵" };
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => w < 4
        ? () => BattleEngine.Materialize(EnemyCatalog.Stages[w + 1].Enemy, BattleContext.EnemyTeam, sc)
        : () => BattleEngine.MaterializeEnemy(EnemyCatalog.TestStages[w - 4].Enemy, sc);
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales =
    {
        ("115/115", new EnemyScaleRule(115, 115)),
        ("150/115", new EnemyScaleRule(150, 115)),
    };

    internal static string SeatsNamed(Formation f)
        => string.Join(" ／ ", f.Occupied().Select(o => $"{FormationRules.SeatNames[f.Shape.PlayableSlots[o.Slot]]} {o.Def.Name}"));

    // =================================================================================
    // 帳簿（verbose の台本と計数から）
    // =================================================================================

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns;
        public readonly Dictionary<string, long> Fell = new(), FellTurnSum = new();
        // 表B（ヨミ）
        public readonly Dictionary<string, long> YomiMovedBy = new();
        public long YomiMoved, YomiPushedFwd, YomiBattles, Reach20, Reach20Sum, Reach30, Reach30Sum;
        public readonly long[] AtkSum = new long[31], AtkCnt = new long[31];
        public readonly long[] MovedTurn = new long[31], Reach20Hist = new long[31], Reach30Hist = new long[31];
        // 表C（シオ）
        public long RgSwaps, RgSelf, RgStuck, RgAllFull, RgHeal, RgWhet, RgReaderSwings, ShioSkillTurns;
        public readonly Dictionary<string, long> Lowered = new(), Pushed = new();
        // 表D（ヨミの攻撃）
        public long YAtkSingle, YAtkSweep, YHitsSweep, YDmgSingle, YDmgSweep;
        // 表E（ガルド）
        public long GaldOutByShio, GaldGuards, DmgGaldFront, DmgGaldAway, TurnsGaldFront, TurnsGaldAway;

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns;
            Add(Fell, o.Fell); Add(FellTurnSum, o.FellTurnSum); Add(YomiMovedBy, o.YomiMovedBy);
            YomiMoved += o.YomiMoved; YomiPushedFwd += o.YomiPushedFwd; YomiBattles += o.YomiBattles;
            Reach20 += o.Reach20; Reach20Sum += o.Reach20Sum; Reach30 += o.Reach30; Reach30Sum += o.Reach30Sum;
            for (int i = 0; i < AtkSum.Length; i++)
            {
                AtkSum[i] += o.AtkSum[i]; AtkCnt[i] += o.AtkCnt[i];
                MovedTurn[i] += o.MovedTurn[i]; Reach20Hist[i] += o.Reach20Hist[i]; Reach30Hist[i] += o.Reach30Hist[i];
            }
            RgSwaps += o.RgSwaps; RgSelf += o.RgSelf; RgStuck += o.RgStuck; RgAllFull += o.RgAllFull;
            RgHeal += o.RgHeal; RgWhet += o.RgWhet; RgReaderSwings += o.RgReaderSwings; ShioSkillTurns += o.ShioSkillTurns;
            Add(Lowered, o.Lowered); Add(Pushed, o.Pushed);
            YAtkSingle += o.YAtkSingle; YAtkSweep += o.YAtkSweep; YHitsSweep += o.YHitsSweep; YDmgSingle += o.YDmgSingle; YDmgSweep += o.YDmgSweep;
            GaldOutByShio += o.GaldOutByShio; GaldGuards += o.GaldGuards; DmgGaldFront += o.DmgGaldFront; DmgGaldAway += o.DmgGaldAway;
            TurnsGaldFront += o.TurnsGaldFront; TurnsGaldAway += o.TurnsGaldAway;
        }

        static void Add(Dictionary<string, long> a, Dictionary<string, long> b)
        {
            foreach (var (k, v) in b) a[k] = a.GetValueOrDefault(k) + v;
        }
        static void Inc(Dictionary<string, long> a, string k, long v = 1) => a[k] = a.GetValueOrDefault(k) + v;

        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;
        public double Per(long x) => (double)x / Math.Max(1, N);

        /// <summary>1戦を取り込む。<paramref name="player"/> は Run に渡した味方の駒（InstanceId が振られた後）。</summary>
        public void Take(BattleResult r, List<UnitState> player, List<int> startSlots)
        {
            N++;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var idOf = player.ToDictionary(u => u.InstanceId, u => u.Def.Id);
            var nameOf = player.ToDictionary(u => u.Def.Id, u => u.Def.Name);
            UnitState? yomi = player.FirstOrDefault(u => u.Def.Id == "yomi");
            UnitState? gald = player.FirstOrDefault(u => u.Def.Id == "gald");
            UnitState? shio = player.FirstOrDefault(u => u.Def.Id == "shio");

            foreach (var (id, t) in r.TallyByUnit)
            {
                if (!nameOf.ContainsKey(id)) continue;
                if (id == "shio") { RgSwaps += t.RegroupSwaps; RgSelf += t.RegroupSelf; RgStuck += t.RegroupStuck; RgAllFull += t.RegroupAllFull; RgHeal += t.RegroupHeal; RgWhet += t.RegroupWhet; RgReaderSwings += t.RegroupReaderSwings; }
                if (t.RegroupLowered > 0) Inc(Lowered, id, t.RegroupLowered);
                if (t.RegroupPushed > 0) Inc(Pushed, id, t.RegroupPushed);
            }

            // Run の後なので UnitState.Slot は終わりの席。初めの席は Run の前に控えたもの。
            var slot = new Dictionary<int, int>();
            for (int i = 0; i < player.Count; i++) slot[player[i].InstanceId] = startSlots[i];
            bool galdAlive = gald is not null;
            bool galdFront = gald is not null && FormationRules.RowOf(slot[gald.InstanceId]) == Row.Front;
            int lastTurn = 0;
            bool yomiAlive = yomi is not null;
            int? reach20 = null, reach30 = null;
            int curAttackActor = -1; AttackPattern curPat = AttackPattern.Single; bool curYomiSweep = false;
            if (yomi is not null) YomiBattles++;

            foreach (BattleEvent e in r.Events)
            {
                if (e.Kind == BattleEventKind.TurnStart)
                {
                    if (gald is not null && galdAlive) { if (galdFront) TurnsGaldFront++; else TurnsGaldAway++; }
                    lastTurn = e.Turn;
                    continue;
                }
                if (e.Kind == BattleEventKind.Skill && shio is not null && e.ActorId == shio.InstanceId) ShioSkillTurns++;
                if (e.Kind == BattleEventKind.StatSnapshot && yomi is not null && e.TargetId == yomi.InstanceId && yomiAlive && e.Turn < AtkSum.Length)
                { AtkSum[e.Turn] += e.Amount; AtkCnt[e.Turn]++; }
                if (e.Kind == BattleEventKind.Move && e.TargetId is int mv && slot.ContainsKey(mv))
                {
                    int from = slot[mv];
                    slot[mv] = e.Slot;
                    if (yomi is not null && mv == yomi.InstanceId)
                    {
                        YomiMoved++;
                        if (e.Turn < MovedTurn.Length) MovedTurn[e.Turn]++;
                        string src = e.ActorId is int a ? (idOf.TryGetValue(a, out var sid) ? (a == mv ? "（自分）" : nameOf[sid]) : "敵") : "（不明）";
                        Inc(YomiMovedBy, src);
                        if (FormationRules.DepthOf(FormationRules.RowOf(e.Slot)) < FormationRules.DepthOf(FormationRules.RowOf(from))) YomiPushedFwd++;
                    }
                    if (gald is not null && mv == gald.InstanceId)
                    {
                        bool nowFront = FormationRules.RowOf(e.Slot) == Row.Front;
                        if (galdFront && !nowFront && shio is not null && e.ActorId == shio.InstanceId) GaldOutByShio++;
                        galdFront = nowFront;
                    }
                }
                if (e.Kind == BattleEventKind.Death && e.TargetId is int dd && idOf.TryGetValue(dd, out var did))
                {
                    Inc(Fell, did); Inc(FellTurnSum, did, e.Turn);
                    if (gald is not null && dd == gald.InstanceId) galdAlive = false;
                    if (yomi is not null && dd == yomi.InstanceId) yomiAlive = false;
                }
                if (e.Kind == BattleEventKind.Intercept && gald is not null && e.ActorId == gald.InstanceId && e.Text == InterceptLabels.Guardian) GaldGuards++;
                if (e.Kind == BattleEventKind.Attack)
                {
                    curAttackActor = e.ActorId ?? -1;
                    if (yomi is not null && curAttackActor == yomi.InstanceId)
                    {
                        curPat = e.Pattern ?? AttackPattern.Single;
                        curYomiSweep = curPat == AttackPattern.Sweep;
                        if (curYomiSweep) YAtkSweep++; else YAtkSingle++;
                        if (reach20 is null && e.Amount >= 20) reach20 = e.Turn;
                        if (reach30 is null && e.Amount >= 30) reach30 = e.Turn;
                    }
                }
                if (e.Kind == BattleEventKind.Damage && e.TargetId is int tg)
                {
                    if (yomi is not null && e.ActorId == yomi.InstanceId && curAttackActor == yomi.InstanceId && !idOf.ContainsKey(tg))
                    {
                        if (curYomiSweep) { YHitsSweep++; YDmgSweep += e.Amount; } else YDmgSingle += e.Amount;
                    }
                    if (gald is not null && idOf.ContainsKey(tg) && tg != gald.InstanceId && e.ActorId is int src2 && !idOf.ContainsKey(src2))
                    {
                        if (galdAlive && galdFront) DmgGaldFront += e.Amount; else if (galdAlive) DmgGaldAway += e.Amount;
                    }
                }
            }
            if (reach20 is int r20) { Reach20++; Reach20Sum += r20; Reach20Hist[Math.Min(30, r20)]++; }
            if (reach30 is int r30) { Reach30++; Reach30Sum += r30; Reach30Hist[Math.Min(30, r30)]++; }
        }

        public string FellText(IEnumerable<(string Id, string Name)> units)
            => string.Join(" ", units.Select(u => Fell.TryGetValue(u.Id, out long n) && n > 0
                ? $"{Short(u.Name)} {100.0 * n / N:F0}%/{(double)FellTurnSum[u.Id] / n:F1}T"
                : $"{Short(u.Name)} 0%"));
    }

    /// <summary>表の短い名前（「XのY」なら Y、でなければ末尾の片仮名）。</summary>
    internal static string Short(string name)
    {
        int no = name.LastIndexOf('の');
        if (no >= 0 && no < name.Length - 1) return name[(no + 1)..];
        int i = name.Length;
        while (i > 0 && name[i - 1] >= '゠' && name[i - 1] <= 'ヿ') i--;
        return i < name.Length ? name[i..] : name;
    }

    internal static Agg Measure(Formation f, Func<List<UnitState>> enemy, int seed0 = 0, int seeds = Seeds)
    {
        var total = new Agg();
        var gate = new object();
        Parallel.For(0, seeds, () => new Agg(), (j, _, local) =>
        {
            var player = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var start = player.Select(u => u.Slot).ToList();
            local.Take(BattleEngine.Run(player, enemy(), seed0 + j, verbose: true), player, start);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    /// <summary>勝敗・決着T・全員生存だけ（verbose なし・席の総当たり用）。</summary>
    internal static (int Wins, int Surv, long WinT) Quick(Formation f, Func<List<UnitState>> enemy, int seed0, int seeds, bool verbose = false)
    {
        int w = 0, s = 0; long t = 0;
        var gate = new object();
        Parallel.For(0, seeds, j =>
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), enemy(), seed0 + j, verbose: verbose);
            if (!r.PlayerWon) return;
            lock (gate) { w++; t += r.Turns; if (r.PlayerStarterFallen.Count == 0) s++; }
        });
        return (w, s, t);
    }

    internal static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    internal static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");
    internal static string D1(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;±0.0");
}
