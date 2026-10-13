using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// dense326 —— 第326期 段0「シガの怖気の判定のずれ」／ 本体「濃い駒は放電をよく通す」（DR-a ／ DR-b）。
// 指示書は design/PHASE326_DENSE_CONDUCT_SPEC.md ／ 報告は design/PHASE326_DENSE_CONDUCT.md。
//
//     dotnet run --project BattleSim -c Release 0 dense326 log [波] [seed] [版]   # ポンの台の1戦のログ（読み物）
//     dotnet run --project BattleSim -c Release 0 dense326 p0s                    # §2-1 段0 の Phase 0（段0 前 ／ 段0 の後・代表台 × 波）
//     dotnet run --project BattleSim -c Release 0 dense326 p0                     # §3-2 本体の Phase 0（規定 ＝ 段0 入り）
//     dotnet run --project BattleSim -c Release 0 dense326 boards [版,…]          # §5-1 代表台 9 × 10 波 × 版
//     dotnet run --project BattleSim -c Release 0 dense326 compare [版,…]         # §5-2 `compare` 64 行 × 版
//     dotnet run --project BattleSim -c Release 0 dense326 grid <boss|guard> <版> # §5-3 格子（固定枠 トウ ＋ カタ・ボスはさらにソム）
//     dotnet run --project BattleSim -c Release 0 dense326 memo [波] [seed] [版]   # §6 ポンの台の放電の並び（受け手の濃さ）
//     dotnet run --project BattleSim -c Release 0 dense326 check                  # §7 自己検査
//
// 版の名前（ASCII）: base（規定 ＝ 段0 入り）／ h325（段0 前・第325期の規定）／ dra ／ drb ／ drbs（DR-b ＋ SA+）／ drac ／ drbc（上限の版・足したときだけ）。
// 波: boss ／ guard ／ bat ／ nine ／ nine2 ／ 1..5。
// **予測のファイルが無ければ本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p326/predict.md`）。
// =====================================================================================
static partial class Dense326Diag
{
    internal const string PredictFile = ".tmp/p326/predict.md";
    const int Seeds = 200;
    const string Tag = "第326期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"dense326: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "log": Log(A(3, "boss"), int.Parse(A(4, "10")), V(A(5, "base"))); return;
            case "p0s": P0Stage0(); return;
            case "p0": P0(); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "grid": Grid(A(3, "boss"), V(A(4, "dra"))); return;
            case "memo": Memo(A(3, "boss"), int.Parse(A(4, "10")), V(A(5, "dra"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("dense326: モードは log / p0s / p0 / boards / compare / grid / memo / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long H(long[]? h, int i) => T323.H(h, i);
    static string Short(UnitDef d) => S307.Short(d);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    /// <summary>版。トウとシガを Id で差し替える（いない台では何もしない）。</summary>
    internal sealed record Ver(string Name, string Ascii, UnitDef Tou, UnitDef Shiga);

    internal static readonly Ver Base = new("規定", "base", UnitCatalog.TouH326, UnitCatalog.Shiga);   // 第327期: 第326期の規定のトウ（DR-a の前）
    internal static readonly Ver Old = new("段0 前", "h325", UnitCatalog.TouH326, UnitCatalog.ShigaH325);
    internal static readonly Ver[] Vers =
    {
        Base,
        new("DR-a", "dra", UnitCatalog.TouDRa, UnitCatalog.Shiga),
        new("DR-b", "drb", UnitCatalog.TouDRb, UnitCatalog.Shiga),
        new("DR-b＋SA+", "drbs", UnitCatalog.TouDRb, UnitCatalog.ShigaSApF),
    };
    static Ver V(string n) => Vers.Append(Old).First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(S316.SwapId(f, "tou", v.Tou), "shiga", v.Shiga);

    /// <summary>ポンの台（指示書 §0 ／ §5-1）: 前1 カタ・前3 シガ・中央 トウ・後1 クグ・後3 ソム（試遊・感電 光の盾 鞭 を改変）。駒はいまの規定。試遊には足さない（決めたこと）。</summary>
    internal static Formation PonBoard() => Formation.Build(front1: UnitCatalog.Kata, front3: UnitCatalog.Shiga, center: UnitCatalog.TouH326, back1: UnitCatalog.KuguH327, back3: UnitCatalog.Som);   // 第327期: トウは第326期の規定

    static Formation Playtest(string n) => PlaytestH326.First(r => r.Name == n).F;   // 第327期: トウを第326期の規定に固定
    static Formation Row(string n) => CompareBuildsH326().First(r => r.Name == n).F;   // 第327期

    /// <summary>§5-1 の代表台（駒はいまの規定）。ポンの台を最初に。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("ポンの台", PonBoard()),
        ("光の盾", Playtest("試遊・感電 光の盾")),
        ("光の盾 鞭", Playtest("試遊・感電 光の盾 鞭")),
        ("打ち返し", Playtest("試遊・感電 打ち返し")),
        ("粉 隣あり", Playtest("試遊・感電 粉 隣あり")),
        ("粉 隣なし", Playtest("試遊・感電 粉 隣なし")),
        ("糸", Playtest("試遊・感電 糸")),
        ("責め苦", Row("責め苦 (トウ×シガ)")),
        ("感電", Row("感電 (シガ×カタ×ソム)")),
    };

    internal static readonly S287.Wave[] Waves = T323.Waves;
    static S287.Wave WaveOf(string n) => T323.WaveOf(n);
    static bool IsElite(S287.Wave w) => T323.IsElite(w);

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal const int MaxT = 31;
    /// <summary>勇者に入った与ダメの出どころ（台本の `Damage` の <c>ActorId</c> の駒）。</summary>
    internal static readonly string[] DmgSrc = { "シガ", "カタ", "トウ", "クグ", "ソム", "召喚獣", "糸玉", "勇者自身（焼）", "ほか" };
    static int SrcOf(UnitState u, UnitState hero) => u == hero ? 7 : u.Def.Id switch
    {
        "shiga" => 0, "kata" => 1, "tou" => 2, "kugu" => 3, "som" => 4, "fodder" => 5, "silkball" => 6, _ => 8,
    };

    internal sealed class Z
    {
        public T323.Z Base = new();
        public UnitTally S = new();
        public long ShigaN;
        // ボスの勇者（ボス波だけ）
        public long HeroN;
        public long[] HeroDmg = new long[MaxT], HeroHeal = new long[MaxT], HeroDens = new long[MaxT], HeroAlive = new long[MaxT];
        public long[] HeroDmgBySrc = new long[9];
        public long HeroDensMax, HealCap;
        public long N => Base.N;
        public long Wins => Base.Wins;
        public double Win => Base.Win;
        public UnitTally T => Base.T;
        public void Merge(Z o)
        {
            Base.Merge(o.Base); S.Add(o.S); ShigaN += o.ShigaN; HeroN += o.HeroN;
            for (int i = 0; i < MaxT; i++) { HeroDmg[i] += o.HeroDmg[i]; HeroHeal[i] += o.HeroHeal[i]; HeroDens[i] += o.HeroDens[i]; HeroAlive[i] += o.HeroAlive[i]; }
            for (int i = 0; i < HeroDmgBySrc.Length; i++) HeroDmgBySrc[i] += o.HeroDmgBySrc[i];
            HeroDensMax = Math.Max(HeroDensMax, o.HeroDensMax); HealCap += o.HealCap;
        }
    }

    internal static Z Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        return CountZ(p, e, r, w);
    }

    /// <summary>1戦の計数（第327期に <see cref="Fight"/> から切り出した・`dust327` が使い回す）。</summary>
    internal static Z CountZ(List<UnitState> p, List<UnitState> e, BattleResult r, S287.Wave w)
    {
        var z = new Z { Base = T323.Count(p, e, r) };
        if (r.TallyByUnit.TryGetValue("shiga", out var st)) { z.S.Add(st); z.ShigaN = 1; }
        if (w.Boss && e.Count == 1)
        {
            var hero = e[0];
            z.HeroN = 1;
            z.HealCap = hero.Traits.OfType<BossMendTrait>().Select(m => (long)(m.Percent >= 100 ? hero.MaxHp : hero.MaxHp * m.Percent / 100)).DefaultIfEmpty(0).Max();   // 勇者の回復の上限（1ターン）
            var byId = new Dictionary<int, UnitState>();
            foreach (var u in p.Concat(e)) byId[u.InstanceId] = u;
            int dens = 0, lastT = 0;
            var densAt = new int[MaxT];
            var late = new Dictionary<int, int>();
            foreach (var ev in r.Events)
            {
                int t = Math.Min(ev.Turn, MaxT - 1);
                if (t > lastT) { for (int k = lastT + 1; k <= t; k++) densAt[k] = dens; lastT = t; }
                switch (ev.Kind)
                {
                    case BattleEventKind.Summon when ev.TargetId is int si && ev.Text == UnitCatalog.Fodder.Name:
                        late[si] = 5; break;   // 喚ばれた召喚獣（戦の途中で湧くので最初の名簿にいない）
                    case BattleEventKind.SilkBall when ev.TargetId is int bi && ev.Text == SilkBallLabels.Place:
                        late[bi] = 6; break;   // 張られた糸玉
                    case BattleEventKind.Damage when ev.TargetId == hero.InstanceId:
                        z.HeroDmg[t] += ev.Amount;
                        z.HeroDmgBySrc[ev.ActorId is int a ? (byId.TryGetValue(a, out var au) ? SrcOf(au, hero) : late.GetValueOrDefault(a, 8)) : 8] += ev.Amount;
                        break;
                    case BattleEventKind.Heal when ev.TargetId == hero.InstanceId:
                        z.HeroHeal[t] += ev.Amount; break;
                    case BattleEventKind.ShockGauge when ev.TargetId == hero.InstanceId && ev.Text is ShockGaugeLabels.Dense or ShockGaugeLabels.DenseFlow or ShockGaugeLabels.DenseMove:
                        dens = ev.Amount; densAt[t] = dens; if (dens > z.HeroDensMax) z.HeroDensMax = dens; break;
                }
            }
            for (int k = lastT + 1; k <= Math.Min(r.Turns, MaxT - 1); k++) densAt[k] = dens;
            for (int k = 1; k <= Math.Min(r.Turns, MaxT - 1); k++) { z.HeroAlive[k]++; z.HeroDens[k] += densAt[k]; }
        }
        return z;
    }

    internal static Z Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var parts = new Z[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, from + i));
        var z = new Z();
        foreach (var c in parts) z.Merge(c);
        return z;
    }

    static int WinsLite(Formation f, S287.Wave w, int n = Seeds)
    {
        int wins = 0;
        Parallel.For(0, n, sd =>
        {
            if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) Interlocked.Increment(ref wins);
        });
        return wins;
    }

    static string WinT(Z z) => $"{Per(z.Base.WinT, z.Wins)} ／ {Per(z.Base.LoseT, z.N - z.Wins)} ／ {Per(z.Base.TouLife, z.Base.TouN)}";

    // 放電の受け手 × 出し手（`UnitTally.Dr326*`・添字 ＝ 受け手 × 4 ＋ 出し手）
    internal static readonly string[] Recv = { "味方", "敵", "召喚獣" };
    internal static readonly string[] Send = { "召喚獣", "糸玉", "糸", "ほか" };
    static long Sum(long[]? h, int recv) => Enumerable.Range(0, 4).Sum(s => H(h, recv * 4 + s));

    /// <summary>
    /// 勇者の回復を与ダメが上回り始めたターン: 1ターンの与ダメ（生きている戦の平均）が勇者の回復の上限（最大HP の 40% ＝ 1,200）に初めて届いたターン。
    /// 回復は「減った分まで」なので、与ダメが上限に届くまでは毎ターン全部戻される（生きている戦が 1/4 以上のターンだけ・届かなければ —）。
    /// </summary>
    internal static string Cross(Z z)
    {
        if (z.HeroN == 0 || z.HealCap == 0) return "—";
        double cap = (double)z.HealCap / z.HeroN;
        for (int t = 1; t < MaxT; t++)
            if (z.HeroAlive[t] > 0 && z.HeroAlive[t] * 4 >= z.HeroN && (double)z.HeroDmg[t] / z.HeroAlive[t] >= cap) return $"T{t}";
        return "—";
    }

    // ---------------------------------------------------------------------------------
    // log（読み物）
    // ---------------------------------------------------------------------------------
    static void Log(string wave, int seed, Ver v)
    {
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(Apply(PonBoard(), v), BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: true);
        Console.WriteLine($"# ポンの台 × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    static string Why(int b) => b == 0 ? "動ける" : string.Join("＋", new[] { "痺れ", "差し出し", "組み付き", "竦み", "転倒", "萎縮" }.Where((_, i) => (b & (1 << i)) != 0));
}
