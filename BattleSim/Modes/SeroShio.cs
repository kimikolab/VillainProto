using BattleCore;
using static Common;

// =====================================================================================
// seroshio —— 第224期「セロの火力を移動に寄せる／シオの回復量をリリ・ツギと比べる」。
// 指示書は design/PHASE224_SERO_SHIO_SPEC.md ／ 報告は design/PHASE224_SERO_SHIO.md。
//
//     dotnet run --project BattleSim -c Release 0 seroshio phase0   # Q0-1〜Q0-5（F0・H0 だけで回る）
//     dotnet run --project BattleSim -c Release 0 seroshio run      # 表A〜E
//     dotnet run --project BattleSim -c Release 0 seroshio check    # 自己検査（受け入れ 2・3）
//
// **線は置かない。** 版は札の差し替え（`Run` の引数は増やさない）。規定のセロ（E0）・シオ（V3）は触らない。
//   セロ F0 ＝ 第223期の E2（`Evade` / `EvadeSwap` / `StatusArrow`）／ F1 ＋ 段 2/4/7 ／ F2 ＋ 動かされるたび +2 ／ F3 ＋ 段3 で 7 本
//   シオ H0 ＝ 規定（`Drifter` / `Regroup`）／ H1 移り木を最大HPの 20% ／ H2 ＋ 下げた味方を最大HPの 20%（手当て）
// =====================================================================================
static partial class SeroShioDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        string arg = args.Length > 3 ? string.Join(" ", args.Skip(3)) : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(); return;
            default:
                Console.WriteLine("seroshio: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunImpl(string arg);
    static partial void CheckImpl();
    static partial void Phase0();

    internal const int Seeds = 200;

    // =================================================================================
    // 版
    // =================================================================================

    internal static UnitDef Copy(UnitDef d, TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
        Pattern = d.Pattern, Actions = d.Actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    /// <summary>F0 ＝ 第223期の E2（`SeroDiag.E2` と同じ札）。</summary>
    internal static readonly UnitDef F0 = SeroDiag.E2;

    // 版の札は本体（`Traits.cs`）に足してから引く。名前で引くのは Phase 0 のコミット（札が無い）でも回るように。
    static TraitId[] F0Traits => F0.Traits.ToArray();
    static TraitId? T(string name) => Enum.TryParse<TraitId>(name, out var t) ? t : null;
    static UnitDef? SeroWith(params string[] names)
    {
        var extra = names.Select(T).ToList();
        if (extra.Any(x => x is null)) return null;
        return Copy(F0, F0Traits.Concat(extra.Select(x => x!.Value)).ToArray());
    }
    static UnitDef? ShioWith(params string[] names)
    {
        var extra = names.Select(T).ToList();
        if (extra.Any(x => x is null)) return null;
        return Copy(UnitCatalog.Shio, UnitCatalog.Shio.Traits.Concat(extra.Select(x => x!.Value)).ToArray());
    }

    internal static readonly UnitDef? F1 = SeroWith("EvadeQuick");
    internal static readonly UnitDef? F2 = SeroWith("EvadeQuick", "EvadeDrift");
    internal static readonly UnitDef? F3 = SeroWith("EvadeQuick", "EvadeDrift", "EvadeVolley");

    internal static readonly UnitDef H0 = UnitCatalog.Shio;
    internal static readonly UnitDef? H1 = ShioWith("DrifterMend");
    internal static readonly UnitDef? H2 = ShioWith("DrifterMend", "RegroupTend");

    // =================================================================================
    // 台（§5.1・§3）
    // =================================================================================

    /// <summary>S1: ポンの移動改（第222期 D1 の席）。＝ K-シオ。</summary>
    internal static Formation S1 => SeroDiag.BenchS1Pon;
    /// <summary>S2: 第223期 S2（庇わない移動軸・ネル）。席は第223期が E1 × 九/新兵 × 150/115 で選んだ並びそのまま。</summary>
    internal static Formation S2 => SeroDiag.PickSeats("S2", SeroDiag.BenchS2Raw, false);
    /// <summary>K-リリ・K-ツギ: S1 の中央（シオの枠）だけを差し替えた台。</summary>
    internal static Formation K(UnitDef healer)
    {
        var g = S1.Clone();
        foreach (var (slot, d) in S1.Occupied()) if (d.Id == "shio") g[slot] = healer;
        return g;
    }
    internal static List<(string Name, Formation F)> S4Rows() => SeroDiag.CompareRowsWithSero();

    /// <summary>セロとシオだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, UnitDef sero, UnitDef shio)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied())
            g[slot] = d.Id == "sero" ? sero : d.Id == "shio" ? shio : d;
        return g;
    }

    internal static readonly string[] WaveNames = SeroDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => SeroDiag.WaveOf(w, sc);
    internal static (string Name, EnemyScaleRule Sc)[] Scales => SeroDiag.Scales;
    internal static string F1s(double x) => DriftDiag.F1(x);
    internal static string F2s(double x) => DriftDiag.F2(x);
    internal static string D1(double x) => DriftDiag.D1(x);

    // =================================================================================
    // 帳簿
    // =================================================================================

    internal static readonly string[] SrcNames = { "シオの手番", "バサ", "セロ", "ハネ", "ほかの味方", "敵", "不明" };

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns;
        public readonly Dictionary<string, long> Dealt = new(), Fell = new(), FellT = new();
        public readonly Dictionary<string, string> Names = new();
        // セロ
        public long SeroDealt, RipDealt, BarDealt, ArrowTick, Barrages, Arrows, Evades, SeroMoved;
        public readonly long[] StageReach = new long[4], StageTurn = new long[4];
        public readonly long[] MoveSrc = new long[7];
        public readonly long[] MovesByTurn = new long[31];     // セロが動かされた回数（ターン別・全戦の和）
        public readonly long[] ReachTurn = new long[8];        // 動かされた累計が 1..7 回に届いたターンの和
        public readonly long[] ReachCount = new long[8];
        public readonly long[] Reach10Turn = new long[11], Reach10Count = new long[11];
        // シオ
        public long DrFires, DrNom, DrGain, TendFires, TendNom, TendGain, RgSwaps, RgSelf;
        public readonly long[] DrBySrc = new long[7], DrNomBySrc = new long[7];
        // リリ・ツギ
        public long LiliHeal, LiliDealt, TsugiArmorOut, PlankSoaked, HealAllByUnit;
        public readonly Dictionary<string, long> HealBy = new();

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns;
            Add(Dealt, o.Dealt); Add(Fell, o.Fell); Add(FellT, o.FellT); Add(HealBy, o.HealBy);
            foreach (var (k, v) in o.Names) Names[k] = v;
            SeroDealt += o.SeroDealt; RipDealt += o.RipDealt; BarDealt += o.BarDealt; ArrowTick += o.ArrowTick;
            Barrages += o.Barrages; Arrows += o.Arrows; Evades += o.Evades; SeroMoved += o.SeroMoved;
            for (int i = 0; i < 4; i++) { StageReach[i] += o.StageReach[i]; StageTurn[i] += o.StageTurn[i]; }
            for (int i = 0; i < 7; i++) { MoveSrc[i] += o.MoveSrc[i]; DrBySrc[i] += o.DrBySrc[i]; DrNomBySrc[i] += o.DrNomBySrc[i]; }
            for (int i = 0; i < MovesByTurn.Length; i++) MovesByTurn[i] += o.MovesByTurn[i];
            for (int i = 0; i < 8; i++) { ReachTurn[i] += o.ReachTurn[i]; ReachCount[i] += o.ReachCount[i]; }
            for (int i = 0; i < 11; i++) { Reach10Turn[i] += o.Reach10Turn[i]; Reach10Count[i] += o.Reach10Count[i]; }
            DrFires += o.DrFires; DrNom += o.DrNom; DrGain += o.DrGain; TendFires += o.TendFires; TendNom += o.TendNom; TendGain += o.TendGain; RgSwaps += o.RgSwaps; RgSelf += o.RgSelf;
            LiliHeal += o.LiliHeal; LiliDealt += o.LiliDealt; TsugiArmorOut += o.TsugiArmorOut; PlankSoaked += o.PlankSoaked; HealAllByUnit += o.HealAllByUnit;
        }

        static void Add(Dictionary<string, long> a, Dictionary<string, long> b) { foreach (var (k, v) in b) a[k] = a.GetValueOrDefault(k) + v; }

        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;
        public double Per(long x) => (double)x / Math.Max(1, N);
        public double DealtOf(string id) => Per(Dealt.GetValueOrDefault(id));
        public int RankOf(string id)
        {
            var order = Dealt.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
            int i = order.IndexOf(id);
            return i < 0 ? 0 : i + 1;
        }
        /// <summary>シオの回復の総量（移り木 ＋ 手当て・実際に増えた HP）。</summary>
        public long ShioHeal => DrGain + TendGain;

        public void Take(BattleResult r, List<UnitState> player)
        {
            N++;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            var idOf = player.ToDictionary(u => u.InstanceId, u => u.Def.Id);
            foreach (var u in player) Names[u.Def.Id] = u.Def.Name;
            UnitState? sero = player.FirstOrDefault(u => u.Def.Id == "sero");

            foreach (var (id, t) in r.TallyByUnit)
            {
                if (!Names.ContainsKey(id)) continue;
                Dealt[id] = Dealt.GetValueOrDefault(id) + t.DamageToEnemy;
                PlankSoaked += t.PlankSoaked;
                if (id == "sero")
                {
                    SeroDealt += t.DamageToEnemy; RipDealt += t.EvRiposteDealt; BarDealt += t.EvBarrageDealt; ArrowTick += t.ArrowTickDealt;
                    Barrages += t.EvBarrages; Arrows += t.EvArrows; Evades += t.Evades;
                    if (t.EvStageTurn is not null) for (int k = 1; k < 4; k++) if (t.EvStageTurn[k] > 0) { StageReach[k]++; StageTurn[k] += t.EvStageTurn[k]; }
                    if (t.EvMoveSrc is not null) for (int k = 0; k < 7; k++) { MoveSrc[k] += t.EvMoveSrc[k]; SeroMoved += t.EvMoveSrc[k]; }
                }
                else if (id == "shio")
                {
                    DrFires += t.DrifterFires; DrNom += t.DrifterNominal; DrGain += t.DrifterGained;
                    TendFires += t.TendFires; TendNom += t.TendNominal; TendGain += t.TendGained; RgSwaps += t.RegroupSwaps; RgSelf += t.RegroupSelf;
                    if (t.DrifterBySrc is not null) for (int k = 0; k < 7; k++) { DrBySrc[k] += t.DrifterBySrc[k]; DrNomBySrc[k] += t.DrifterNomBySrc![k]; }
                }
                else if (id == "lili") { LiliDealt += t.DamageToEnemy; }
                else if (id == "tsugi") { TsugiArmorOut += t.ArmorOut; }
            }

            int seroMoves = 0;
            foreach (BattleEvent e in r.Events)
            {
                switch (e.Kind)
                {
                    case BattleEventKind.Death:
                        if (e.TargetId is int dt && idOf.TryGetValue(dt, out var did))
                        { Fell[did] = Fell.GetValueOrDefault(did) + 1; FellT[did] = FellT.GetValueOrDefault(did) + e.Turn; }
                        break;
                    case BattleEventKind.Heal:
                        if (e.ActorId is int ha && idOf.TryGetValue(ha, out var hid) && e.TargetId is int ht && idOf.ContainsKey(ht))
                        {
                            HealBy[hid] = HealBy.GetValueOrDefault(hid) + e.Amount;
                            if (hid == "lili") LiliHeal += e.Amount;
                        }
                        break;
                    case BattleEventKind.Move:
                        if (sero is not null && e.TargetId == sero.InstanceId)
                        {
                            seroMoves++;
                            if (e.Turn < MovesByTurn.Length) MovesByTurn[e.Turn]++;
                            if (seroMoves <= 7) { ReachTurn[seroMoves] += e.Turn; ReachCount[seroMoves]++; }
                            if (seroMoves <= 10) { Reach10Turn[seroMoves] += e.Turn; Reach10Count[seroMoves]++; }
                        }
                        break;
                }
            }
        }

        public string ReachText(int k) => ReachCount[k] == 0 ? "0%" : $"{100.0 * ReachCount[k] / N:F0}% ／ T{(double)ReachTurn[k] / ReachCount[k]:F1}";
        public string Reach10Text(int k) => Reach10Count[k] == 0 ? "0%" : $"{100.0 * Reach10Count[k] / N:F0}% ／ T{(double)Reach10Turn[k] / Reach10Count[k]:F1}";
        public string StageText(int k) => StageReach[k] == 0 ? "0%" : $"{100.0 * StageReach[k] / N:F0}% ／ T{(double)StageTurn[k] / StageReach[k]:F1}";
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

    internal static readonly (string Name, int[] Ws)[] Groups = { ("本編 第2〜5波", new[] { 0, 1, 2, 3 }), ("九/新兵", new[] { 4 }) };

    internal static Agg Sum(Func<int, Agg> perWave, IEnumerable<int> ws)
    {
        var a = new Agg();
        foreach (int w in ws) a.Merge(perWave(w));
        return a;
    }
}
