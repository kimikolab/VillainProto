using BattleCore;
using static Common;

// =====================================================================================
// burnaudit —— 第233期「燃焼の軸の棚卸し（ボルグ・ホタ・ヒヨ）」。
// 指示書は design/PHASE233_BURN_AUDIT_SPEC.md ／ 報告は design/PHASE233_BURN_AUDIT.md。
// **機構は1つも足さない（盤面は第232期の規定のまま）。線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 burnaudit phase0   # Q0-1〜Q0-3（仕様・台の駒・役割・所要の見積もり）
//     dotnet run --project BattleSim -c Release 0 burnaudit pick     # 段1（B3 の全ての2枚の組・B4 の全ての1枚 × 席の総当たり）
//     dotnet run --project BattleSim -c Release 0 burnaudit run      # 段2 ＋ 表A〜F（段1 を内部で回し直す）
//     dotnet run --project BattleSim -c Release 0 burnaudit check    # 自己検査（受け入れ 1〜3）
// =====================================================================================
static partial class BurnAuditDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        _args = args;
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "pick": PickImpl(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "log": LogOne(args.Length > 3 ? args[3] : "", args.Length > 4 ? int.Parse(args[4]) : 0, args.Length > 5 ? int.Parse(args[5]) : MainWave); return;
            default:
                Console.WriteLine("burnaudit: モードは phase0 / pick / run / check。");
                return;
        }
    }

    static string[]? _args;

    /// <summary>1戦のログ（席を「前1,前3,中央,後1,後3」の駒 Id で渡す・倍率 200/200）。</summary>
    static void LogOne(string seats, int seed, int wave)
    {
        var f = Seat(seats.Split(',').Select(UnitCatalog.ById).ToArray());
        var (r, _, _, _) = Fight(f, wave, Scales[0].Sc, seed);
        Console.WriteLine($"# {SeatsNamed(f)} × {WaveNames[wave]} × 200/200 × seed {seed}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();

    internal const int Seeds = 200;
    /// <summary>主判定の波（検証・九 / 新兵）の添字。</summary>
    internal const int MainWave = 4;
    internal static readonly string[] WaveNames = DriftDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => DriftDiag.WaveOf(w, sc);

    /// <summary>敵の倍率（§5）: 200/200（主判定）・400/300（負荷試験）・115/115。</summary>
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales =
    {
        ("200/200", new EnemyScaleRule(200, 200)),
        ("400/300", new EnemyScaleRule(400, 300)),
        ("115/115", new EnemyScaleRule(115, 115)),
    };

    internal static string SeatsNamed(Formation f) => DriftDiag.SeatsNamed(f);
    internal static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    internal static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");

    // ---------------------------------------------------------------------------------
    // 台（§3）
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] Core = { UnitCatalog.BorgF0, UnitCatalog.HotaL0, UnitCatalog.HiyoF0 };
    static readonly HashSet<string> Excluded = new() { "borg", "hota", "hiyo", "gald", "tsugi" };
    /// <summary>相方の候補: 52 枚から ボルグ・ホタ・ヒヨ・ガルド・ツギ を除いた全員（召喚専用は `All` に居ない）。B3 はベニを含む。</summary>
    internal static List<UnitDef> Candidates => UnitCatalog.All.Where(u => !Excluded.Contains(u.Id))
        .Select(u => ReferenceEquals(u, UnitCatalog.Sero) ? UnitCatalog.SeroS0 : u).ToList();   // 第256期: セロは状態の矢ありの旧に固定
    internal static List<UnitDef> CandidatesB4 => Candidates.Where(u => u.Id != "beni").ToList();

    /// <summary>参考 燃焼: `compare` の `燃焼 (ボルグ×ホタ)` の行そのまま（ガルド入り）。</summary>
    internal static Formation RefBurn => CompareBuilds().First(r => r.Name.StartsWith("燃焼 (ボルグ×ホタ)")).F;
    /// <summary>参考 雷: ポンの席（前1 シガ ／ 前3 ツギ ／ 中央 ベニ ／ 後1 カタ ／ 後3 ミオ）。</summary>
    internal static Formation RefThunder => Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Tsugi,
        center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Mio);
    /// <summary>参考 移動: 第228期 H3 の1位の席（前1 バサ ／ 前3 セロ ／ 中央 ヨミ ／ 後1 シオ ／ 後3 ハネ）。駒は第232期の規定。</summary>
    internal static Formation RefMove => Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.SeroS0,   // 第256期: 状態の矢ありの旧セロに固定
        center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.HaneR0);

    internal static Formation Seat(UnitDef[] p) => Formation.Build(front1: p[0], front3: p[1], center: p[2], back1: p[3], back3: p[4]);

    /// <summary>ボルグとホタが隣り合う席か（X 字・編成の枠 0〜4）。</summary>
    internal static bool BorgHotaAdjacent(Formation f)
    {
        int b = -1, h = -1;
        foreach (var (slot, d) in f.Occupied()) { if (d.Id == "borg") b = slot; if (d.Id == "hota") h = slot; }
        return b >= 0 && h >= 0 && FormationRules.AreAdjacent(b, h);
    }

    // ---------------------------------------------------------------------------------
    // 段1 の席の総当たり（主判定: 九 / 新兵 × 200/200 の全員生存 → 落ちた駒 → 決着T）
    // ---------------------------------------------------------------------------------
    /// <summary>段1 の seed 帯（段2・表の seed 0..199 と重ならない）。</summary>
    internal const int PickSeed0 = 1000;

    internal readonly record struct SeatScore(int Sv, int Fell, int W, long T)
    {
        public long Key => (long)Sv * 1_000_000_000L - (long)Fell * 100_000L - (W == 0 ? 99_999 : Math.Min(99_999, T * 100 / Math.Max(1, W)));
    }

    internal static SeatScore ScoreOf(Formation f, int seed0, int seeds, int wave = MainWave, EnemyScaleRule? sc = null)
    {
        var s = sc ?? Scales[0].Sc;
        int sv = 0, fell = 0, w = 0; long t = 0;
        for (int i = 0; i < seeds; i++)
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(OldFire(f), BattleContext.PlayerTeam), WaveOf(wave, s)(), seed0 + i, verbose: false, ember: EmberRule.Pre256);
            fell += r.PlayerStarterFallen.Count;
            if (!r.PlayerWon) continue;
            w++; t += r.Turns;
            if (r.PlayerStarterFallen.Count == 0) sv++;
        }
        return new SeatScore(sv, fell, w, t);
    }

    /// <summary>5 枚の 120 通りを段1 の seed で測る（並列）。戻り値は席の並び順（`Permute` の順）。</summary>
    internal static (Formation F, SeatScore S)[] AllSeats(IReadOnlyList<UnitDef> members, int seeds)
    {
        var perms = SeroDiag.Permute(members).Select(Seat).ToArray();
        var res = new SeatScore[perms.Length];
        Parallel.For(0, perms.Length, i => res[i] = ScoreOf(perms[i], PickSeed0, seeds));
        return perms.Select((f, i) => (f, res[i])).ToArray();
    }

    /// <summary>最良（同値は並びの若い方）。<paramref name="adjOnly"/> ならボルグとホタが隣り合う席だけから。</summary>
    internal static (Formation F, SeatScore S) Best((Formation F, SeatScore S)[] xs, bool adjOnly = false)
    {
        (Formation F, SeatScore S) best = default; long bk = long.MinValue; bool found = false;
        foreach (var x in xs)
        {
            if (adjOnly && !BorgHotaAdjacent(x.F)) continue;
            if (!found || x.S.Key > bk) { best = x; bk = x.S.Key; found = true; }
        }
        return best;
    }

    // ---------------------------------------------------------------------------------
    // 1戦と集計（表A〜F）
    // ---------------------------------------------------------------------------------
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E, Dictionary<int, int> Slot0) Fight(
        Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(OldFire(f), BattleContext.PlayerTeam);
        var e = WaveOf(w, sc)();
        var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);   // InstanceId は `Run` の中で振られる
        var r = BattleEngine.Run(p, e, seed, verbose: verbose, ember: EmberRule.Pre256);
        return (r, p, e, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
    }

    /// <summary>死因（表B）。</summary>
    internal static readonly string[] Causes = { "敵の単体", "敵の薙ぎ", "敵の貫き", "敵の全体", "敵のその他", "ボルグの巻き込み", "味方のその他", "燃焼の刻み", "毒の刻み", "その他・不明" };
    internal const int CBorgSplash = 5, CBurnTick = 7, CPoisonTick = 8, CUnknown = 9;
    /// <summary>被ダメの内訳（表C）。</summary>
    internal static readonly string[] Takes = { "敵の攻撃", "ボルグの巻き込み", "味方のその他", "燃焼の刻み", "毒の刻み", "その他" };

    internal sealed class Agg
    {
        public long N, Wins, AllSurv, WinTurns, StarterFallenSum;
        public readonly Dictionary<string, long> Fell = new(), FellT = new();
        // 表B
        public readonly long[] Cause = new long[Causes.Length], CauseOneShot = new long[Causes.Length];
        public readonly Dictionary<string, long[]> CauseBy = new();
        public long DeathEvents;
        // 表C（駒の名前 → 内訳）・ベニの反転
        public readonly Dictionary<string, long[]> Taken = new();
        public readonly Dictionary<string, long> BurnInverted = new(), PoisonInverted = new();
        // 表D
        public long HotaAttacks, HotaPierce, HotaAttacksT1, HotaPierceT1, HotaAliveTurns;
        public long FavorCalls, FavorIdle, FavorGiven, FavorWhetted, FavorTaken, FavorToPyre;
        public readonly Dictionary<string, long> FavorWhetTo = new();
        public long BTurns; public readonly long[] BurnUnitTurns = new long[2], UnitTurns = new long[2], BurnT2 = new long[2], AliveT2 = new long[2];
        public long BrittleExtraFoe, BrittleExtraAlly, PyreBrittle, IgniteFoeBorg, IgniteAllyBorg;
        // 表E
        public readonly Dictionary<string, long> Dealt = new();
        public long HotaPierceDealt;
        // 表F
        public readonly BurnLinkLedger Link = new();

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns; StarterFallenSum += o.StarterFallenSum; DeathEvents += o.DeathEvents;
            Add(Fell, o.Fell); Add(FellT, o.FellT);
            for (int i = 0; i < Causes.Length; i++) { Cause[i] += o.Cause[i]; CauseOneShot[i] += o.CauseOneShot[i]; }
            AddArr(CauseBy, o.CauseBy, Causes.Length); AddArr(Taken, o.Taken, Takes.Length);
            Add(BurnInverted, o.BurnInverted); Add(PoisonInverted, o.PoisonInverted);
            HotaAttacks += o.HotaAttacks; HotaPierce += o.HotaPierce; HotaAttacksT1 += o.HotaAttacksT1; HotaPierceT1 += o.HotaPierceT1; HotaAliveTurns += o.HotaAliveTurns;
            FavorCalls += o.FavorCalls; FavorIdle += o.FavorIdle; FavorGiven += o.FavorGiven; FavorWhetted += o.FavorWhetted; FavorTaken += o.FavorTaken; FavorToPyre += o.FavorToPyre;
            Add(FavorWhetTo, o.FavorWhetTo);
            BTurns += o.BTurns;
            for (int i = 0; i < 2; i++) { BurnUnitTurns[i] += o.BurnUnitTurns[i]; UnitTurns[i] += o.UnitTurns[i]; BurnT2[i] += o.BurnT2[i]; AliveT2[i] += o.AliveT2[i]; }
            BrittleExtraFoe += o.BrittleExtraFoe; BrittleExtraAlly += o.BrittleExtraAlly; PyreBrittle += o.PyreBrittle; IgniteFoeBorg += o.IgniteFoeBorg; IgniteAllyBorg += o.IgniteAllyBorg;
            Add(Dealt, o.Dealt); HotaPierceDealt += o.HotaPierceDealt;
            MergeLink(Link, o.Link);
        }
        static void Add(Dictionary<string, long> a, Dictionary<string, long> b) { foreach (var (k, v) in b) a[k] = a.GetValueOrDefault(k) + v; }
        static void AddArr(Dictionary<string, long[]> a, Dictionary<string, long[]> b, int n)
        {
            foreach (var (k, v) in b) { if (!a.TryGetValue(k, out var x)) a[k] = x = new long[n]; for (int i = 0; i < n; i++) x[i] += v[i]; }
        }
        internal static void MergeLink(BurnLinkLedger a, BurnLinkLedger b)
        {
            for (int i = 0; i < 2; i++)
            {
                a.FoeDeaths[i] += b.FoeDeaths[i]; a.FoeBurnDeaths[i] += b.FoeBurnDeaths[i]; a.FoeBurnDeathNeighbor[i] += b.FoeBurnDeathNeighbor[i];
                a.FoeBurnDeathUnburntNeighbor[i] += b.FoeBurnDeathUnburntNeighbor[i]; a.FoeBurnDeathUnburntSum[i] += b.FoeBurnDeathUnburntSum[i];
                a.PyrePierces[i] += b.PyrePierces[i]; a.PyreExtraHits[i] += b.PyreExtraHits[i]; a.PyreExtraUnburnt[i] += b.PyreExtraUnburnt[i];
                a.AllyKills[i] += b.AllyKills[i]; a.AllyBurnKills[i] += b.AllyBurnKills[i];
                a.AllyBurnKillUnburntNeighbor[i] += b.AllyBurnKillUnburntNeighbor[i]; a.AllyBurnKillUnburntSum[i] += b.AllyBurnKillUnburntSum[i];
            }
            a.FavorCalls += b.FavorCalls;
        }

        public double Per(long x) => (double)x / Math.Max(1, N);
        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinT => Wins == 0 ? double.NaN : (double)WinTurns / Wins;
        public long FellSum => Fell.Values.Sum();

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            N++;
            StarterFallenSum += r.PlayerStarterFallen.Count;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }

            var players = p.Select(u => u.InstanceId).ToHashSet();
            var nameOf = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Name);
            var idOf = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Id);
            int hota = p.FirstOrDefault(u => u.Def.Id == "hota")?.InstanceId ?? -1;
            var b = new GaleDiag.Board(p.Concat(e), slot0);
            var lastFatal = new Dictionary<int, (int Cause, bool One)>();
            var lastTick = new Dictionary<int, string>();
            var hitsSince = new Dictionary<int, (int Hits, int HpHead)>();

            foreach (BattleEvent ev in r.Events)
            {
                switch (ev.Kind)
                {
                    case BattleEventKind.TurnStart:
                        hitsSince.Clear();
                        if (hota >= 0 && b.Alive.Contains(hota)) HotaAliveTurns++;
                        break;
                    case BattleEventKind.Attack:
                        hitsSince.Clear();
                        if (ev.ActorId == hota && hota >= 0 && !ev.Reaction)
                        {
                            HotaAttacks++; bool pr = ev.Pattern == AttackPattern.Pierce; if (pr) HotaPierce++;
                            if (ev.Turn == 1) { HotaAttacksT1++; if (pr) HotaPierceT1++; }
                        }
                        break;
                    case BattleEventKind.Status when ev.TargetId is int st:
                        lastTick[st] = ev.Text ?? "";
                        if (ev.InverterId is not null && players.Contains(st))
                        {
                            var d = ev.Text == "燃焼" ? BurnInverted : ev.Text == "毒" ? PoisonInverted : null;
                            if (d is not null) d[nameOf[st]] = d.GetValueOrDefault(nameOf[st]) + ev.Amount;
                        }
                        break;
                    case BattleEventKind.Damage when ev.TargetId is int dt && b.Hp.ContainsKey(dt):
                        {
                            int before = b.Hp[dt], loss = Math.Max(0, before - ev.HpAfter);
                            int? actor = ev.ActorId;
                            bool actorPlayer = actor is int a1 && players.Contains(a1);
                            // 与ダメ（表E）: 味方の駒が敵に与えた HP の減り
                            if (actorPlayer && !players.Contains(dt) && idOf.ContainsKey(dt))
                            {
                                string an = nameOf[actor!.Value];
                                Dealt[an] = Dealt.GetValueOrDefault(an) + loss;
                                if (actor == hota && ev.Pattern == AttackPattern.Pierce) HotaPierceDealt += loss;
                            }
                            if (players.Contains(dt))
                            {
                                int take, cause;
                                if (actor is null)
                                {
                                    string tk = lastTick.GetValueOrDefault(dt, "");
                                    take = tk == "燃焼" ? 3 : tk == "毒" ? 4 : 5;
                                    cause = tk == "燃焼" ? CBurnTick : tk == "毒" ? CPoisonTick : CUnknown;
                                }
                                else if (!actorPlayer)
                                {
                                    take = 0;
                                    cause = ev.Pattern switch
                                    {
                                        AttackPattern.Single => 0, AttackPattern.Sweep => 1, AttackPattern.Pierce => 2, AttackPattern.All => 3, _ => 4,
                                    };
                                }
                                else if (idOf.GetValueOrDefault(actor.Value) == "borg" && ev.FriendlyFire && ev.Pattern is null && !ev.Relayed)
                                { take = 1; cause = CBorgSplash; }
                                else { take = 2; cause = 6; }
                                string vn = nameOf[dt];
                                if (!Taken.TryGetValue(vn, out var tv)) Taken[vn] = tv = new long[Takes.Length];
                                tv[take] += loss;
                                var hs = hitsSince.TryGetValue(dt, out var h0) ? h0 : (0, before);
                                hitsSince[dt] = (hs.Item1 + 1, hs.Item2);
                                if (ev.HpAfter <= 0)
                                {
                                    bool one = hs.Item1 == 0 && hs.Item2 * 2 >= b.MaxHp[dt];
                                    lastFatal[dt] = (cause, one);
                                }
                            }
                            lastTick.Remove(dt);
                            break;
                        }
                    case BattleEventKind.Death when ev.TargetId is int dd && players.Contains(dd):
                        {
                            DeathEvents++;
                            string id = idOf[dd], nm = nameOf[dd];
                            Fell[id] = Fell.GetValueOrDefault(id) + 1;
                            FellT[id] = FellT.GetValueOrDefault(id) + ev.Turn;
                            var (c, one) = lastFatal.TryGetValue(dd, out var lf) ? lf : (CUnknown, false);
                            lastFatal.Remove(dd);
                            Cause[c]++; if (one) CauseOneShot[c]++;
                            if (!CauseBy.TryGetValue(nm, out var cb)) CauseBy[nm] = cb = new long[Causes.Length];
                            cb[c]++;
                            break;
                        }
                }
                b.Apply(ev);
            }

            if (p.Any(u => u.Def.Id == "hiyo"))   // 贔屓の保持者はヒヨ1枚（盤面全体の帳簿を読む）
            {
                FavorIdle += r.FavorIdle; FavorGiven += r.FavorGiven; FavorWhetted += r.FavorWhetted; FavorTaken += r.FavorTaken; FavorToPyre += r.FavorToPyre;
                foreach (var (k, v) in r.FavorWhetTo) FavorWhetTo[k] = FavorWhetTo.GetValueOrDefault(k) + v;
            }
            if (r.BurnLink is { } bl) { MergeLink(Link, bl); FavorCalls += bl.FavorCalls; }
            if (r.Brittle is { } br)
            {
                BTurns += br.Turns;
                for (int i = 0; i < 2; i++)
                {
                    BurnUnitTurns[i] += br.BurnUnitTurns[i]; UnitTurns[i] += br.UnitTurns[i];
                    BurnT2[i] += br.BurnByTurn[i][2]; AliveT2[i] += br.AliveByTurn[i][2];
                }
                for (int k = 0; k < br.Extra.GetLength(1); k++) { BrittleExtraFoe += br.Extra[0, k]; BrittleExtraAlly += br.Extra[1, k]; }
                PyreBrittle += br.PyreExtra;
                IgniteFoeBorg += br.IgniteFoe.GetValueOrDefault("borg"); IgniteAllyBorg += br.IgniteAlly.GetValueOrDefault("borg");
            }
        }
    }

    internal static Agg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = Seeds)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var a = new Agg();
            var (r, p, e, slot0) = Fight(f, w, sc, seed0 + i);
            a.Take(r, p, e, slot0);
            parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }
}
