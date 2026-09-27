using BattleCore;
using static Common;

// =====================================================================================
// decoy —— 第226期「攻200%に耐える移動軸：回避盾のセロ・バサとハネの対・シオの段の時期」。
// 指示書は design/PHASE226_DECOY_SPEC.md ／ 報告は design/PHASE226_DECOY.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 decoy phase0   # Q0-1〜Q0-8（K0 だけで回る）
//     dotnet run --project BattleSim -c Release 0 decoy run      # 表A〜F
//     dotnet run --project BattleSim -c Release 0 decoy check    # 自己検査（受け入れ 2〜4・7）
// =====================================================================================
static partial class DecoyDiag
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
                Console.WriteLine("decoy: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void Phase0();
    static partial void RunImpl(string arg);
    static partial void CheckImpl();

    internal const int Seeds = 200;

    // =================================================================================
    // 版（§5）。**札の差し替え**だけ。名前で引くのは Phase 0 のコミット（札が無い）でも回るように。
    //   K0 規定 ／ K1 セロに回避盾（`Decoy`）／ K2 バサ・ハネに対＋段（`Disarray`）／ K3 両方 ／ K4 K3 ＋ バサの突風（`Squall`）
    //   ＋ シオの段の「遅い」版（`ShioStageSlow`）を K3・K4 に重ねた K3s・K4s。
    // =================================================================================

    static TraitId? T(string name) => Enum.TryParse<TraitId>(name, out var t) ? t : null;
    internal static UnitDef Copy(UnitDef d, TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
        Pattern = d.Pattern, Actions = d.Actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };
    static UnitDef? With(UnitDef d, params string[] names)
    {
        var extra = names.Select(T).ToList();
        if (extra.Any(x => x is null)) return null;
        return Copy(d, d.Traits.Concat(extra.Select(x => x!.Value)).ToArray());
    }

    internal sealed record Ver(string Tag, UnitDef Sero, UnitDef Basa, UnitDef Hane, UnitDef Shio);

    internal static Ver[] Versions
    {
        get
        {
            UnitDef s0 = UnitCatalog.Sero, b0 = UnitCatalog.Basa, h0 = UnitCatalog.Hane, sh0 = UnitCatalog.Shio;
            UnitDef? s1 = With(s0, "Decoy"), b2 = With(b0, "Disarray"), h2 = With(h0, "Disarray"), b4 = With(b0, "Disarray", "Squall");
            UnitDef? shS = With(sh0, "ShioStageSlow");
            var list = new List<Ver> { new("K0", s0, b0, h0, sh0) };
            if (s1 is not null) list.Add(new("K1", s1, b0, h0, sh0));
            if (b2 is not null && h2 is not null) list.Add(new("K2", s0, b2, h2, sh0));
            if (s1 is not null && b2 is not null && h2 is not null) list.Add(new("K3", s1, b2, h2, sh0));
            if (s1 is not null && b4 is not null && h2 is not null) list.Add(new("K4", s1, b4, h2, sh0));
            if (s1 is not null && b2 is not null && h2 is not null && shS is not null) list.Add(new("K3s", s1, b2, h2, shS));
            if (s1 is not null && b4 is not null && h2 is not null && shS is not null) list.Add(new("K4s", s1, b4, h2, shS));
            return list.ToArray();
        }
    }
    internal static Ver VerOf(string tag) => Versions.First(v => v.Tag == tag);

    /// <summary>編成のセロ・バサ・ハネ・シオだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied())
            g[slot] = d.Id switch { "sero" => v.Sero, "basa" => v.Basa, "hane" => v.Hane, "shio" => v.Shio, _ => d };
        return g;
    }

    // =================================================================================
    // 台（§8.1）。M-ハネ（第225期の席）と、総当たりで選ぶ M-ハネ（`run`）・参考（雷の編成）。
    // =================================================================================

    internal static Formation MHane225 => Formation.Build(front1: UnitCatalog.Hane, front3: UnitCatalog.Basa,
        center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Sero);

    /// <summary>
    /// 参考: 雷の編成（ベニ・ミオ・カタ・シガ・ツギ）の**ポンの席**（第226期の報告の後にポンの画面から: 前1 シガ ／ 前3 ツギ ／ 中央 ベニ ／ 後1 カタ ／ 後3 ミオ）。
    /// 報告の最初の版は記録が無かったので後1 ミオ ／ 後3 カタ の仮の席で測っていた（`shockdigest k226` の台もこの席に替えた）。
    /// </summary>
    internal static Formation Thunder => Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Tsugi,
        center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Mio);

    // 波: 本編の第2〜5波 ＋ 検証・九 / 新兵（主判定）＋ 検証・九 / 農兵（`DriftDiag.WaveOf` の 0..5）
    internal static readonly string[] WaveNames = DriftDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => DriftDiag.WaveOf(w, sc);
    internal const int MainWave = 4;
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales =
    {
        ("200/200", new EnemyScaleRule(200, 200)),
        ("200/115", new EnemyScaleRule(200, 115)),
        ("115/115", new EnemyScaleRule(115, 115)),
    };
    internal static string SeatsNamed(Formation f) => DriftDiag.SeatsNamed(f);
    internal static string F1(double x) => DriftDiag.F1(x);
    internal static string F2(double x) => DriftDiag.F2(x);
    internal static string D1(double x) => DriftDiag.D1(x);

    /// <summary>1戦を回す（台本つき・`verbose: true`）。味方の <see cref="UnitState"/> と開幕の席（InstanceId → 席）も返す。</summary>
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E, Dictionary<int, int> Slot0) Fight(Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = WaveOf(w, sc)();
        var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
        return (r, p, e, slot0);
    }

    /// <summary>
    /// 台本から盤面の席と生死を追う（Move の <c>Slot</c> ＝ 動いた先・Death / Revive）。
    /// <see cref="Visit"/> は各出来事の<b>直前</b>の状態で呼ばれる（ターンの頭なら TurnStart の直前）。
    /// </summary>
    internal sealed class Tracker
    {
        public readonly Dictionary<int, int> Slot;
        public readonly HashSet<int> Alive;
        public readonly HashSet<int> Players;
        public Tracker(Dictionary<int, int> slot0, IEnumerable<UnitState> players)
        {
            Slot = new(slot0);
            Alive = new(slot0.Keys);
            Players = new(players.Select(u => u.InstanceId));
        }
        public void Apply(BattleEvent e)
        {
            switch (e.Kind)
            {
                case BattleEventKind.Move when e.TargetId is int m: Slot[m] = e.Slot; break;
                case BattleEventKind.Summon when e.TargetId is int n: Slot[n] = e.Slot; Alive.Add(n); if (e.Team == BattleContext.PlayerTeam) Players.Add(n); break;
                case BattleEventKind.Death when e.TargetId is int d: Alive.Remove(d); break;
                case BattleEventKind.Revive when e.TargetId is int v: Alive.Add(v); break;
            }
        }
        /// <summary>陣営の「単体攻撃が狙える駒」（前列 → 中列 → 全員・<c>PoolOf</c> と同じ）。X 字の席で引く。</summary>
        public List<int> Pool(bool players)
        {
            var team = Alive.Where(i => Players.Contains(i) == players && Slot.ContainsKey(i)).ToList();
            var front = team.Where(i => FormationRules.RowOf(Slot[i]) == Row.Front).ToList();
            if (front.Count > 0) return front;
            var mid = team.Where(i => FormationRules.RowOf(Slot[i]) == Row.Mid).ToList();
            return mid.Count > 0 ? mid : team;
        }
    }
}
