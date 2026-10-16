using BattleCore;
using static Common;
using FC = FireCycleDiag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// inv348 —— 第348期【数えるだけの期】駒と軸の棚卸し: 絵の無い駒 ／ 軸の横並び ／ 燃焼と移動の内訳。
// 指示書は design/PHASE348_AXIS_INVENTORY_SPEC.md ／ 報告は design/PHASE348_AXIS_INVENTORY.md。**規則を1つも変えない**（版も並べない）。
//
//     dotnet run --project BattleSim -c Release 0 inv348 axes        # §3 軸の横並び（代表台 × 物差し・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 inv348 burn        # §4 燃焼の内訳（ボルグの札を1枚ずつ外す ／ ホタの技ごとの与ダメと育ちを外す）
//     dotnet run --project BattleSim -c Release 0 inv348 move        # §5 移動の内訳（ヨミ ／ ハネ ／ バサ ／ シオ ／ セロ・第232期の駒と今）
//     dotnet run --project BattleSim -c Release 0 inv348 roster      # §2 駒の表（16枚）と資源の地図
//     dotnet run --project BattleSim -c Release 0 inv348 log <台> <波> [seed]   # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 inv348 check       # 自己検査
//
// 数えるのは `HarmRule(true)`（害の帳簿・第348期の状態キーの帳簿 `UnitTally.Inv348Out` ／ `Inv348In` もここで回る）と
// `BossRule(true)`（ターンごとの与ダメ）の計数だけ。どちらも盤面を動かさない（自己検査で「器具の有無で勝敗 ／ 台本が変わらない」を見る）。
// =====================================================================================
static partial class Inv348Diag
{
    internal const int Seeds = 100;
    const string Tag = "第348期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "axes": Axes(); return;
            case "burn": Burn(); return;
            case "move": Move(); return;
            case "roster": Roster(); return;
            case "log": Log(A(3, "燃焼"), A(4, "boss"), int.Parse(A(5, "0"))); return;
            case "check": Check(); return;
            case "iso": Isolate(A(3, "感電"), A(4, "guard"), int.Parse(A(5, "2"))); return;
            default: Console.WriteLine("inv348: モードは axes / burn / move / roster / log / check。"); return;
        }
    }

    internal static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    // ---------------------------------------------------------------------------------
    // 波（§3-2 の物差し）
    // ---------------------------------------------------------------------------------
    internal sealed record W(string Name, Func<List<UnitState>> Make, EnemyScaleRule Scale, bool Boss = false, bool Target = false);

    static readonly EnemyScaleRule Strong = new(500, 300);
    static W FromS287(S287.Wave w) => new(w.Name, w.Make, EnemyScaleRule.Default, w.Boss);
    static W Main(int i, EnemyScaleRule sc, string prefix) =>
        new($"{prefix}第{i}波", () => BattleEngine.Materialize(EnemyCatalog.Stages[i - 1].Enemy, BattleContext.EnemyTeam, sc), sc);

    internal static readonly W Boss = FromS287(T323.WaveOf("boss"));
    internal static readonly W Guard = FromS287(T323.WaveOf("guard"));
    internal static readonly W Bat = FromS287(T323.WaveOf("bat"));
    internal static readonly W M2 = Main(2, EnemyScaleRule.Default, "本編 ");
    internal static readonly W M4 = Main(4, EnemyScaleRule.Default, "本編 ");
    internal static readonly W M5 = Main(5, EnemyScaleRule.Default, "本編 ");
    internal static readonly W Nine = FromS287(T323.WaveOf("nine"));
    internal static readonly W T1 = new("的・一", () => BattleEngine.MaterializeEnemy(FC.Target1, EnemyScaleRule.None), EnemyScaleRule.None, Target: true);
    internal static readonly W T9 = new("的・九", () => BattleEngine.MaterializeEnemy(FC.Target9, EnemyScaleRule.None), EnemyScaleRule.None, Target: true);
    internal static readonly W[] StrongWaves = Enumerable.Range(2, 4).Select(i => Main(i, Strong, "強 ")).ToArray();
    internal static readonly W[] AxisWaves = new[] { Boss, Guard, Bat, M2, M4, M5, Nine, T1, T9 }.Concat(StrongWaves).ToArray();
    internal const int TargetTurns = FC.TargetTurns;   // 的の波は 1〜8 ターン目だけを読む（第246期の追記 A と同じ）

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, Turns, AllyTaken, AllyFell, FirstFellSum, FirstFellN, Dmg8, Max1T, Max1TMax;
        public readonly Dictionary<string, UnitTally> Tal = new();
        public readonly Dictionary<string, long> Deaths = new(), DeathT = new(), AliveT = new();
        /// <summary>`UnitTally.Add` が足さない欄（火の鎧 ／ 火の癒しなど・ターンごとの与ダメ）を、その戦の生の帳簿から抜いて持つ（添字は <see cref="Xi"/>）。</summary>
        public readonly Dictionary<string, long[]> X = new();
        public long Xv(string id, int i) => X.TryGetValue(id, out var a) ? a[i] : 0;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Turns += o.Turns; AllyTaken += o.AllyTaken; AllyFell += o.AllyFell; FirstFellSum += o.FirstFellSum; FirstFellN += o.FirstFellN;
            Dmg8 += o.Dmg8; Max1T += o.Max1T; Max1TMax = Math.Max(Max1TMax, o.Max1TMax);
            foreach (var (k, v) in o.Tal) { if (!Tal.TryGetValue(k, out var t)) Tal[k] = t = new UnitTally(); t.Add(v); }
            foreach (var (k, v) in o.Deaths) Deaths[k] = Deaths.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.AliveT) AliveT[k] = AliveT.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.X) { if (!X.TryGetValue(k, out var a)) X[k] = a = new long[XN]; for (int i = 0; i < XN; i++) a[i] += v[i]; }
        }
        public UnitTally T(string id) => Tal.TryGetValue(id, out var t) ? t : new UnitTally();
        public string WinTs => Wins == 0 ? "—" : F1((double)WinT / Wins);
    }

    /// <summary>1戦。<paramref name="census"/> が偽なら計数の規則を渡さない（自己検査の対照）。</summary>
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E) Raw(Formation f, W w, int seed, bool verbose = false, bool census = true, EmberRule? ember = null, CounterProbe? probe = null)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = census
            ? BattleEngine.Run(p, e, seed, verbose: verbose, boss: new BossRule(true) { Scale = w.Scale }, harm: new HarmRule(true), ember: ember, probe: probe)
            : BattleEngine.Run(p, e, seed, verbose: verbose, boss: new BossRule(false) { Scale = w.Scale }, ember: ember, probe: probe);
        return (r, p, e);
    }

    internal static St Fight(Formation f, W w, int seed, EmberRule? ember = null)
    {
        var (r, p, e) = Raw(f, w, seed, ember: ember);
        return Take(r, p, w);
    }

    internal static St Take(BattleResult r, List<UnitState> p, W w)
    {
        var a = new St { N = 1, Turns = r.Turns };
        foreach (var u in p) a.AliveT[u.Def.Id] = a.AliveT.GetValueOrDefault(u.Def.Id) + (u.LastDeathTurn > 0 ? u.LastDeathTurn : r.Turns);
        if (r.PlayerWon && !w.Target) { a.Wins = 1; a.WinT = r.Turns; }
        var ids = p.Select(u => u.Def.Id).Distinct().ToArray();
        foreach (var id in ids) if (r.TallyByUnit.TryGetValue(id, out var t)) { var c = new UnitTally(); c.Add(t); a.Tal[id] = c; a.AllyTaken += t.DamageTaken; a.X[id] = Extract(t); }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0))
        {
            a.AllyFell++;
            a.Deaths[u.Def.Id] = a.Deaths.GetValueOrDefault(u.Def.Id) + 1;
            a.DeathT[u.Def.Id] = a.DeathT.GetValueOrDefault(u.Def.Id) + u.LastDeathTurn;
        }
        var fell = p.Where(u => u.LastDeathTurn > 0).Select(u => u.LastDeathTurn).ToList();
        if (fell.Count > 0) { a.FirstFellN = 1; a.FirstFellSum = fell.Min(); }
        if (w.Target)
        {
            var by = new long[TargetTurns + 1];
            foreach (var (id, t) in r.TallyByUnit)
            {
                if (id == FC.Target.Id || t.BossDmgByTurn is not int[] d) continue;
                for (int i = 1; i <= TargetTurns && i < d.Length; i++) by[i] += d[i];
            }
            a.Dmg8 = by.Sum(); a.Max1T = by.Max(); a.Max1TMax = a.Max1T;
        }
        return a;
    }

    /// <summary><see cref="St.X"/> の添字。</summary>
    internal static class Xi
    {
        public const int ArmorSaved = 0, ArmorHits = 1, WardTaken = 2, MendHealed = 3, FeedHealed = 4, Smolder = 5, ConvTick = 6, ConvSplash = 7, Dmg8 = 8, WardSaved = 9, OffTurn = 10;
    }
    const int XN = 11;
    static long[] Extract(UnitTally t) => new long[]
    {
        t.FireArmorSaved, t.FireArmorGuardHits, t.FireWardTaken, t.FireMendHealed, t.FireFeedHealed, t.SmolderUsed, t.FireConvTickHealed, t.FireConvSplashHealed,
        t.BossDmgByTurn is int[] d ? d.Skip(1).Take(TargetTurns).Sum() : 0, t.FireWardSaved, t.DmgOutOffTurn,
    };

    internal static St Measure(Formation f, W w, int n = Seeds, EmberRule? ember = null)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i, ember));
        var all = new St();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // 代表台（§3-1・決めたこと: 各軸の `compare` の行か試遊の台から 1〜2 台）
    // ---------------------------------------------------------------------------------
    internal sealed record Board(string Axis, string Name, Formation F);
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;   // 交差帯 12 行（強化×燃 ／ 弱体×破片）も引く
    static Formation Play(string n) => Presets.Playtest.Concat(Presets.Versions).First(r => r.Name == n).F;
    /// <summary>チェック波の移動の台（第232期の規定の席・`checkwave` の「移動」・駒は今の規定）。</summary>
    internal static Formation MoveCheck => CheckWaveDiag.BoardOf("移動");

    internal static Board[] Boards() => new[]
    {
        new Board("燃焼", "強化×燃 (ヒヨ×ホタ×ボルグ)", Row("強化×燃 (ヒヨ×ホタ×ボルグ)")),
        new Board("燃焼", "燃焼 (ボルグ×ホタ)", Row("燃焼 (ボルグ×ホタ)")),
        new Board("移動", "移動 (チェック波の台)", MoveCheck),
        new Board("移動", "移動改 (バサ×ヨミ×シオ)", Row("移動改 (バサ×ヨミ×シオ)")),
        new Board("感電", "試遊・感電 ポンの台", Play("試遊・感電 ポンの台")),
        new Board("感電", "感電 (シガ×カタ×ソム)", Row("感電 (シガ×カタ×ソム)")),
        new Board("標", "試遊・標 守り型", Play("試遊・標 守り型")),
        new Board("標", "標経済 (ヒサ×ザン×ミサ)", Row("標経済 (ヒサ×ザン×ミサ)")),
        new Board("毒", "毒 (グザ×ミオ×ラウ)", Row("毒 (グザ×ミオ×ラウ)")),
        new Board("毒", "毒+耐久 (ベニ×トウ)", Row("毒+耐久 (ベニ×トウ)")),
        new Board("反撃", "反撃 (ヒサ×カド)", Row("反撃 (ヒサ×カド)")),
        new Board("反撃", "反撃改 (ドハ×カド)", Row("反撃改 (ドハ×カド)")),
        new Board("自傷", "逆しま (ネル×ウツ)", Row("逆しま (ネル×ウツ)")),
        new Board("自傷", "逆しま改 (クビ×ウツ)", Row("逆しま改 (クビ×ウツ)")),
        new Board("死", "死の連鎖 (リィカ軸)", Row("死の連鎖 (リィカ軸)")),
        new Board("死", "惨禍×死の連鎖", Row("惨禍×死の連鎖")),
        new Board("破片", "礫 (ガレ×ヒビ×ウロ)", Row("礫 (ガレ×ヒビ×ウロ)")),
        new Board("破片", "鱗改 (ウロ×ヒビ)", Row("鱗改 (ウロ×ヒビ)")),
        new Board("デバフ", "弱体×破片 (ウケ×ネル×ウロ)", Row("弱体×破片 (ウケ×ネル×ウロ)")),
        new Board("デバフ", "渡し (ワタ×ドハ)", Row("渡し (ワタ×ドハ)")),
    };
    internal static readonly string[] AxisOrder = { "燃焼", "移動", "感電", "標", "毒", "反撃", "自傷", "死", "破片", "デバフ" };

    static Board BoardOf(string n) => Boards().First(b => b.Name == n || b.Axis == n || b.Name.StartsWith(n, StringComparison.Ordinal));
    static W WaveOf(string n) => n switch
    {
        "boss" => Boss, "guard" => Guard, "bat" => Bat, "2" => M2, "4" => M4, "5" => M5, "nine" => Nine, "t1" => T1, "t9" => T9,
        _ => AxisWaves.First(w => w.Name == n),
    };

    static void Log(string board, string wave, int seed)
    {
        var b = BoardOf(board); var w = WaveOf(wave);
        var (r, _, _) = Raw(b.F, w, seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {w.Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    internal static UnitDef Without(UnitDef d, params TraitId[] drop) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Traits = d.Traits.Where(t => !drop.Contains(t)).ToArray(),
        Pattern = d.Pattern, Advances = d.Advances, Actions = d.Actions, PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };
    /// <summary>素体（同数値・特性なし・`checkup ideal` の `HcPlain` と同じ作り）。</summary>
    internal static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Traits = Array.Empty<TraitId>(), Pattern = d.Pattern,
    };
    internal static Formation Swap(Formation f, string id, UnitDef to) => S316.SwapId(f, id, to);
}
