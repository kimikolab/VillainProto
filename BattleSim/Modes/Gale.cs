using BattleCore;
using static Common;

// =====================================================================================
// gale —— 第229期「バサの嵐が味方も揺らす・追い風・転倒は隊列の穴」。
// 指示書は design/PHASE229_GALE_SPEC.md ／ 報告は design/PHASE229_GALE.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 gale phase0   # Q0-1〜Q0-7（G0 ＝ 前段の規定だけで回る）
//     dotnet run --project BattleSim -c Release 0 gale run      # 表A〜E
//     dotnet run --project BattleSim -c Release 0 gale check    # 自己検査（受け入れ 2〜4・7）
// =====================================================================================
static partial class GaleDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            default:
                Console.WriteLine("gale: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    internal const int Seeds = 200;

    // 台（§8.1）
    /// <summary>第228期 H3 の総当たりの1位（前1 バサ ／ 前3 セロ ／ 中央 ヨミ ／ 後1 シオ ／ 後3 ハネ）。ハネは前段で H3 が規定。</summary>
    internal static Formation MHane228 => Formation.Build(front1: UnitCatalog.BasaG0, front3: UnitCatalog.SeroC0,
        center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.HaneG0);
    /// <summary>参考: ポンの席（前1 シガ ／ 前3 ツギ ／ 中央 ベニ ／ 後1 カタ ／ 後3 ミオ）。</summary>
    internal static Formation Thunder => Formation.Build(front1: UnitCatalog.ShigaG3K, front3: UnitCatalog.Tsugi,
        center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Mio);

    // 波: 本編の第2〜5波 ＋ 検証・九 / 新兵（主判定）＋ 検証・九 / 農兵
    internal static readonly string[] WaveNames = DriftDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => DriftDiag.WaveOf(w, sc);
    internal const int MainWave = 4;
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales = DecoyDiag.Scales;
    internal static string SeatsNamed(Formation f) => DriftDiag.SeatsNamed(f);
    internal static string F1(double x) => DriftDiag.F1(x);
    internal static string F2(double x) => DriftDiag.F2(x);

    /// <summary>後の期に足した帳簿の欄（名前で引く・Phase 0 のコミットでも回るように）。</summary>
    internal static long Tally(UnitTally t, string name)
        => typeof(UnitTally).GetField(name)?.GetValue(t) is { } v ? Convert.ToInt64(v) : 0;

    // =================================================================================
    // 台本から盤面を追う（席・生死・HP・転倒）。<see cref="Visit"/> 相当の読みは呼び出し側が各出来事の直前に行う。
    // =================================================================================

    internal sealed class Board
    {
        public readonly Dictionary<int, int> Slot = new();
        public readonly Dictionary<int, int> Hp = new();
        public readonly Dictionary<int, int> MaxHp = new();
        public readonly Dictionary<int, int> Team = new();
        public readonly Dictionary<int, string> Id = new();
        public readonly HashSet<int> Alive = new();
        public readonly HashSet<int> Fallen = new();   // 転倒（Stagger・転倒 → 手番喪失 ／ 死亡で消える）

        public Board(IEnumerable<UnitState> units, Dictionary<int, int> slot0)
        {
            foreach (var u in units)
            {
                Slot[u.InstanceId] = slot0[u.InstanceId];
                Hp[u.InstanceId] = u.Def.MaxHp; MaxHp[u.InstanceId] = u.Def.MaxHp;
                Team[u.InstanceId] = u.TeamId; Id[u.InstanceId] = u.Def.Id;
                Alive.Add(u.InstanceId);
            }
        }

        public void Apply(BattleEvent e)
        {
            switch (e.Kind)
            {
                case BattleEventKind.Move when e.TargetId is int m: Slot[m] = e.Slot; break;
                case BattleEventKind.Summon when e.TargetId is int n:
                    Slot[n] = e.Slot; Alive.Add(n); Team[n] = e.Team ?? 0; Hp[n] = e.HpAfter; MaxHp[n] = Math.Max(1, e.HpAfter); Id[n] = "summon"; break;
                case BattleEventKind.Death when e.TargetId is int d: Alive.Remove(d); Fallen.Remove(d); break;
                case BattleEventKind.Revive when e.TargetId is int v: Alive.Add(v); if (e.HpAfter > 0) Hp[v] = e.HpAfter; break;
                case BattleEventKind.Damage or BattleEventKind.Heal when e.TargetId is int h && Hp.ContainsKey(h): Hp[h] = e.HpAfter; break;
                case BattleEventKind.Stagger when e.TargetId is int s:
                    if (e.Text == StaggerLabels.Fell) Fallen.Add(s); else if (e.Text == StaggerLabels.Lost) Fallen.Remove(s);
                    break;
            }
        }

        public Row RowOfUnit(int id) => FormationRules.RowOf(Slot[id]);
        public IEnumerable<int> Living(int team) => Alive.Where(i => Team.GetValueOrDefault(i, -1) == team);

        /// <summary>
        /// その陣営の「一番前の列が全員転倒していて、その後ろに誰か立っている」か（転倒の穴が効く局面）。
        /// 一番前の列 ＝ 生きている駒がいる最も前の列。
        /// </summary>
        public bool Hole(int team)
        {
            var live = Living(team).ToList();
            if (live.Count == 0) return false;
            int front = live.Min(i => FormationRules.DepthOf(RowOfUnit(i)));
            var row = live.Where(i => FormationRules.DepthOf(RowOfUnit(i)) == front).ToList();
            return row.All(Fallen.Contains) && live.Any(i => FormationRules.DepthOf(RowOfUnit(i)) > front);
        }

        /// <summary>経路（X 字）の上の生きている味方（前 → 後）。</summary>
        public List<int> LaneAllies(int team, int lane)
        {
            var path = FormationRules.LanePath(lane);
            var res = new List<int>();
            foreach (int s in path)
                foreach (int i in Living(team)) if (Slot[i] == s) res.Add(i);
            return res;
        }
    }

    /// <summary>1戦を回す（台本つき）。<paramref name="shuffler"/> を渡せば規則（転倒の穴）を掛ける。</summary>
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E, Dictionary<int, int> Slot0) Fight(
        Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true, ShufflerRule? shuffler = null)
    {
        var p = BattleEngine.Materialize(OldTune(OldYomiShio(f)), BattleContext.PlayerTeam);
        var e = WaveOf(w, sc)();
        var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
        var r = BattleEngine.Run(p, e, seed, verbose: verbose, shuffler: shuffler ?? PreHole);
        var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
        return (r, p, e, slot0);
    }
}
