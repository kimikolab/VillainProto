using BattleCore;
using static Common;
using B283 = Boss283Diag;
using D326 = Dense326Diag;
using D327 = Dust327Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// settle328 —— 第328期【重い期・規定化だけ】粉塵爆発 ＋ クグの電池（X2）を規定にする。
// 指示書は design/PHASE328_DUST_BATTERY_SETTLE_SPEC.md ／ 報告は design/PHASE328_DUST_BATTERY_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 settle328 p0                         # §3 Phase 0: 糸の台がボスに勝てない理由（ターン・倒れた駒・勇者の残り）
//     dotnet run --project BattleSim -c Release 0 settle328 log [台] [波] [seed] [old|new] # 1戦のログ（台は ascii: pon ／ whip ／ shield ／ near ／ ito ／ torment ／ shock）
//     dotnet run --project BattleSim -c Release 0 settle328 boards                     # §5 代表台 7 × 10 波 × 旧 ／ 新（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 settle328 grid                       # §5 格子1本: ボス・固定枠 トウ ＋ クグ（規定）
//     dotnet run --project BattleSim -c Release 0 settle328 check                      # §8 自己検査
//
// 旧 ＝ 第327期の規定（`TouH327` ／ `KuguH327`・`Common.Pin327`）。新 ＝ いまの規定（DX ＋ 電池）。
// **予測のファイルが無ければ本測定（boards ／ grid）を走らせない**（R397・`.tmp/p328/predict.md`）。
// =====================================================================================
static partial class Settle328Diag
{
    internal const string PredictFile = ".tmp/p328/predict.md";
    const int Seeds = 200;   // 重い期（design/MEASURE_LIGHT.md §1）
    const string Tag = "第328期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"settle328: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "log": Log(A(3, "ito"), A(4, "boss"), int.Parse(A(5, "0")), A(6, "new") == "old"); return;
            case "boards": BoardsAll(); return;
            case "grid": Grid(); return;
            case "check": Check(); return;
            default: Console.WriteLine("settle328: モードは p0 / log / boards / grid / check。"); return;
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

    /// <summary>ポンの台（第326〜327期と同じ席）。駒はいまの規定。</summary>
    internal static Formation PonBoard() => Formation.Build(front1: UnitCatalog.Kata, front3: UnitCatalog.Shiga, center: UnitCatalog.Tou, back1: UnitCatalog.Kugu, back3: UnitCatalog.Som);
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;

    /// <summary>§5 の代表台（駒はいまの規定）。旧は <see cref="Pin327"/> を通す。</summary>
    internal static (string Name, string Ascii, Formation F)[] Boards() => new (string, string, Formation)[]
    {
        ("ポンの台", "pon", PonBoard()),
        ("光の盾 鞭", "whip", Playtest("試遊・感電 光の盾 鞭")),
        ("光の盾", "shield", Playtest("試遊・感電 光の盾")),
        ("粉 隣あり", "near", Playtest("試遊・感電 粉 隣あり")),
        ("糸", "ito", Playtest("試遊・感電 糸")),
        ("責め苦", "torment", Row("責め苦 (トウ×シガ)")),
        ("感電", "shock", Row("感電 (シガ×カタ×ソム)")),
    };

    internal static readonly S287.Wave[] Waves = T323.Waves;
    static S287.Wave WaveOf(string n) => T323.WaveOf(n);
    static bool IsElite(S287.Wave w) => T323.IsElite(w);

    static long Dx(UnitTally t, int rk, int vk) => H(t.Dx327Dealt, rk * 3 + vk);

    static void Log(string board, string wave, int seed, bool old)
    {
        var (name, _, f0) = Boards().First(b => b.Ascii == board || b.Name == board);
        var w = WaveOf(wave);
        var f = old ? Pin327(f0) : f0;
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {(old ? "旧（第327期の規定）" : "新（規定）")}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // §3 Phase 0: 糸の台がボスに勝てない理由
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var w = WaveOf("boss");
        var f0 = Boards().First(b => b.Ascii == "ito").F;
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 糸の台（{S307.Seats(f0)}）× ボス（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var (tag, f) in new[] { ("旧（第327期の規定）", Pin327(f0)), ("新（規定）", f0) })
        {
            int n = Seeds, wins = 0, cap = 0;
            var endT = new long[BattleEngine.MaxTurns + 2];
            var ids = f.Occupied().Select(o => o.Def.Id).ToArray();
            var died = ids.ToDictionary(i => i, _ => 0L); var diedT = ids.ToDictionary(i => i, _ => 0L);
            var first = ids.ToDictionary(i => i, _ => 0L);
            long heroHp = 0, heroMax = 0, heroLowSum = 0, dmgSum = 0, healSum = 0, turnSum = 0, dmgCap = 0;
            var lk = new object();
            Parallel.For(0, n, sd =>
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var e = w.Make();
                var r = BattleEngine.Run(p, e, sd, verbose: true);
                var z = D326.CountZ(p, e, r, w);
                var hero = e[0];
                var byId = p.ToDictionary(u => u.InstanceId, u => u.Def.Id);
                var deaths = r.Events.Where(ev => ev.Kind == BattleEventKind.Death && ev.TargetId is int t && byId.ContainsKey(t)).Select(ev => (Id: byId[ev.TargetId!.Value], ev.Turn)).ToList();
                int low = hero.MaxHp;
                foreach (var ev in r.Events) if (ev.Kind == BattleEventKind.Damage && ev.TargetId == hero.InstanceId && ev.HpAfter is int ha) low = Math.Min(low, ha);
                lock (lk)
                {
                    if (r.PlayerWon) wins++;
                    if (r.Turns >= BattleEngine.MaxTurns) cap++;
                    endT[Math.Min(r.Turns, BattleEngine.MaxTurns + 1)]++;
                    foreach (var d in deaths) { died[d.Id]++; diedT[d.Id] += d.Turn; }
                    if (deaths.Count > 0) first[deaths.OrderBy(d => d.Turn).First().Id]++;
                    heroHp += Math.Max(0, hero.Hp); heroMax += hero.MaxHp; heroLowSum += low;
                    dmgSum += z.HeroDmg.Sum(); healSum += z.HeroHeal.Sum(); turnSum += r.Turns;
                    for (int t = 1; t < D326.MaxT; t++) if (z.HeroDmg[t] >= 1200) dmgCap++;
                }
            });
            Console.WriteLine($"## {tag}");
            Console.WriteLine();
            Console.WriteLine($"- 勝率 {Pct(wins, n)}%・時間切れ（{BattleEngine.MaxTurns}T）{cap} 戦・決着T の平均 {Per(turnSum, n)}・決着T の分布: {string.Join(" ", Enumerable.Range(1, BattleEngine.MaxTurns).Where(t => endT[t] > 0).Select(t => $"T{t}:{endT[t]}"))}");
            Console.WriteLine($"- 勇者の残り HP（戦の終わり）平均 {Per0(heroHp, n)} ／ 最大 HP {Per0(heroMax, n)}（{Pct(heroHp, heroMax)}%）・戦の中の最低 平均 {Per0(heroLowSum, n)}");
            Console.WriteLine($"- 勇者へ 1ターン平均の与ダメ {Per0(dmgSum, turnSum)} ／ 回復 {Per0(healSum, turnSum)}（回復の上限 1,200）・与ダメが 1,200 以上のターン 延べ {dmgCap}");
            Console.WriteLine($"- 倒れた駒（1戦あたりの率・倒れたターンの平均・最初に倒れた回数）: " + string.Join(" ／ ", ids.Select(i => $"{Short(f.Occupied().First(o => o.Def.Id == i).Def)} {Pct(died[i], n)}%・T{Per(diedT[i], died[i])}・最初 {first[i]}")));
            Console.WriteLine();
        }
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }
}
