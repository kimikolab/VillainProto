using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// =====================================================================================
// db353 —— 第353期【軽い期】デバフ軸の立て直し②: 呪いを火力に（UT-o ウツの上限超え ／ UK-c 奪うウケの作り直し・版）。
// 指示書は design/PHASE353_DEBUFF_BOSS_SPEC.md ／ 報告は design/PHASE353_DEBUFF_BOSS.md。波と1戦の計数は `inv348` ／ `db351` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 db353 reach               # §2-1 Phase 0: 机上の到達計算（試遊・呪い × ボス・規定・seed 0..19・T1〜T12）
//     dotnet run --project BattleSim -c Release 0 db353 ukseats             # §2-2 Phase 0: ウケ入りの台（ムド → ウケ ／ クビ → ウケ × ウケの席・規定のウケ・ボス ／ 近衛 ／ 大隊 × seed 0..19）
//     dotnet run --project BattleSim -c Release 0 db353 boards [版,…]       # §5 の 1〜3・5: 代表台 × 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 db353 compare [版,…]      # §5 の 4: `compare` 64 行 × 版（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 db353 log <版> <台> <波> [seed]   # 1戦のログ（台 play ／ uk ／ 逆しま）
//     dotnet run --project BattleSim -c Release 0 db353 check               # 自己検査
//
// 版の名前（ASCII）: base（規定）／ o（UT-o）／ c（UK-c）／ oc（UT-o ＋ UK-c）。版はウツ ／ ウケだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ版の本測定（boards ／ compare）を走らせない**（R397・`.tmp/p353/predict.md`）。
// =====================================================================================
static partial class Db353Diag
{
    internal const string PredictFile = ".tmp/p353/predict.md";
    const string Tag = "第353期";
    const int Seeds = 100, GridSeeds = 20, CompareSeeds = 200, TrajT = 12;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if ((mode is "boards" or "compare" || mode == "ukseats" && A(3, "base") != "base") && !File.Exists(PredictFile))
        {
            Console.WriteLine($"db353: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "reach": Reach(); return;
            case "ukseats": UkSeats(V(A(3, "base"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "play"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("db353: モードは reach / ukseats / boards / compare / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    // ---------------------------------------------------------------------------------
    // 版
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef Utsu, UnitDef Uke);
    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.UtsuH353, UnitCatalog.Uke),   // 第354期: 規定のウツが UT-o になったので、第353期の「規定」は旧の定義に固定
        new("UT-o 上限超え", "o", UnitCatalog.UtsuUTo, UnitCatalog.Uke),
        new("UK-c 奪う・累計", "c", UnitCatalog.UtsuH353, UnitCatalog.UkeUKc),
        new("UT-o ＋ UK-c", "oc", UnitCatalog.UtsuUTo, UnitCatalog.UkeUKc),
    };
    /// <summary>参考（版ではない）: 第351期の UK-r（元 − 今から奪う）。ウケ入りの台で UK-c と比べるためだけに回す（`boards r`）。</summary>
    internal static readonly Ver RefUKr = new("参考・UK-r（第351期）", "r", UnitCatalog.UtsuH353, UnitCatalog.UkeUKr);
    internal static Ver V(string n) => Vers.Append(RefUKr).First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(S316.SwapId(f, "utsu", v.Utsu), "uke", v.Uke);

    // ---------------------------------------------------------------------------------
    // 台
    // ---------------------------------------------------------------------------------
    internal const string PlayRow = D.PlayRow;   // 「試遊・呪い」（ワタ・ムド・クビ・ネル・ウツ）
    internal static Formation Play => D.Top1;

    /// <summary>
    /// §2-2 のウケ入りの台の候補: 「試遊・呪い」のムド ／ クビをウケに替え、ウケの席だけを残りの4席と入れ替える（2 × 5 ＝ 10 並び）。
    /// 入れ替えは席の交換（ウケと相手の駒が席を取り替える・ほかの3枚は動かない）。
    /// </summary>
    internal static (string Key, string Out, Formation F)[] UkCandidates()
    {
        var list = new List<(string, string, Formation)>();
        foreach (var (outId, outName) in new[] { ("mudo", "ムド"), ("kubi", "クビ") })
        {
            var b = S316.SwapId(Play, outId, UnitCatalog.Uke);
            int us = Enumerable.Range(0, 5).First(i => b[i]?.Id == "uke");
            for (int j = 0; j < 5; j++)
            {
                var f = new Formation();
                for (int i = 0; i < 5; i++) f[i] = b[i];
                (f[us], f[j]) = (f[j], f[us]);
                list.Add((Seats(f), outName, f));
            }
        }
        return list.ToArray();
    }

    /// <summary>
    /// §2-2 で選んだウケ入りの台（報告 §1 の決めたこと）。**規定のウケでは 10 並びすべてが 3波 0%** で順位が付かないので、版の結果に依らない決め方にした:
    /// 弱体の書き手でないクビを外し（第351期の UK-r の台と同じ）、ウケはクビの席（中央）——第351期の UK-r の列と直に比べられる。
    /// </summary>
    internal static Formation UkBoard => UkCandidates().First(c => c.Key == UkPick).F;
    internal const string UkPick = "ワタ・ムド・ウケ・ネル・ウツ";

    static Formation Row(string n) => Pin353(CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F);   // 第354期: ウツは旧
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("呪い", PlayRow, Play),
        new I.Board("呪い", "ウケ入りの台", UkBoard),
        new I.Board("呪い", "逆しま (ネル×ウツ)", Row("逆しま (ネル×ウツ)")),
    };
    static I.Board BoardOf(string n) => n switch
    {
        "play" => Boards()[0], "uk" => Boards()[1],
        _ => Boards().First(b => b.Name.StartsWith(n, StringComparison.Ordinal)),
    };

    internal static readonly I.W[] Waves = D.Waves;

    static void Log(Ver v, string board, string wave, int seed)
    {
        var b = BoardOf(board); var w = D.WaveOf(wave);
        var (r, _, _) = I.Raw(Apply(b.F, v), w, seed, verbose: true);
        Console.WriteLine($"# {b.Name}（{Seats(b.F)}）× {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
        Console.WriteLine();
        Console.WriteLine($"## 台本の最後の2ターン（{r.Events.Count} 件のうち）");
        int lt = r.Events.Count == 0 ? 0 : r.Events.Max(x => x.Turn);
        foreach (var x in r.Events.Where(x => x.Turn >= lt - 1)) Console.WriteLine($"T{x.Turn} {x.Kind} actor={x.ActorId} target={x.TargetId} amount={x.Amount} hp={x.HpAfter} text={x.Text}");
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose・台本を読む・盤面は動かさない）
    // ---------------------------------------------------------------------------------
    internal sealed class Fx
    {
        public long N, Wins, WinT, Fell, Lost, LostBossHp, LostBossHpMin = long.MaxValue, LostBossLow, BossMax, BossN;
        public long UtsuN, UtsuDealt, UtsuMax, UtsuOver, UtsuOverN, UkeN, UkeDealt, PlGain, PlHits, PlDry, PlMiss, PlBoss;
        public readonly Dictionary<string, long> PlFrom = new();
        // ターンごと（そのターンまで戦が続いた戦の和 ／ 数）: 味方 → ボスの与ダメ ／ ウツの与ダメ（ボスへ）／ ウケの与ダメ（ボスへ）／ ボスの回復 ／ ターン末のボスの HP ／ ウツの攻撃力（ターン頭）／ ウツの手数 ／ ボスの攻撃力（ターン頭）
        public readonly long[] TAlly = new long[TrajT + 1], TUtsu = new long[TrajT + 1], TUke = new long[TrajT + 1], THeal = new long[TrajT + 1], THp = new long[TrajT + 1],
            TUAtk = new long[TrajT + 1], TUSw = new long[TrajT + 1], TBAtk = new long[TrajT + 1], TBDull = new long[TrajT + 1], TCnt = new long[TrajT + 1], TUCnt = new long[TrajT + 1];
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Lost += o.Lost; LostBossHp += o.LostBossHp; LostBossHpMin = Math.Min(LostBossHpMin, o.LostBossHpMin); LostBossLow += o.LostBossLow;
            BossMax = Math.Max(BossMax, o.BossMax); BossN += o.BossN;
            UtsuN += o.UtsuN; UtsuDealt += o.UtsuDealt; UtsuMax += o.UtsuMax; UtsuOver += o.UtsuOver; UtsuOverN += o.UtsuOverN;
            UkeN += o.UkeN; UkeDealt += o.UkeDealt; PlGain += o.PlGain; PlHits += o.PlHits; PlDry += o.PlDry; PlMiss += o.PlMiss; PlBoss += o.PlBoss;
            foreach (var (k, v) in o.PlFrom) PlFrom[k] = PlFrom.GetValueOrDefault(k) + v;
            for (int t = 0; t <= TrajT; t++)
            {
                TAlly[t] += o.TAlly[t]; TUtsu[t] += o.TUtsu[t]; TUke[t] += o.TUke[t]; THeal[t] += o.THeal[t]; THp[t] += o.THp[t];
                TUAtk[t] += o.TUAtk[t]; TUSw[t] += o.TUSw[t]; TBAtk[t] += o.TBAtk[t]; TBDull[t] += o.TBDull[t]; TCnt[t] += o.TCnt[t]; TUCnt[t] += o.TUCnt[t];
            }
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }

    internal static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else a.Lost = 1;
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        var utsu = p.FirstOrDefault(u => u.Def.Id == "utsu"); var uke = p.FirstOrDefault(u => u.Def.Id == "uke");
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team == BattleContext.PlayerTeam) mine.Add(sid);
        if (utsu is not null && r.TallyByUnit.TryGetValue("utsu", out var tu))
        {
            a.UtsuN = 1; a.UtsuDealt = tu.DamageToEnemy; a.UtsuMax = tu.Db351PerverseMax;
            long over = Math.Max(0, tu.Db351PerverseMax - PerverseOverTrait.Threshold);
            a.UtsuOver = over; if (over > 0) a.UtsuOverN = 1;
        }
        if (uke is not null && r.TallyByUnit.TryGetValue("uke", out var tk))
        {
            a.UkeN = 1; a.UkeDealt = tk.DamageToEnemy; a.PlGain = tk.Db353PlunderGain; a.PlHits = tk.Db353PlunderHits; a.PlDry = tk.Db353PlunderDry; a.PlMiss = tk.Db353PlunderMiss; a.PlBoss = tk.Db353PlunderBoss;
        }
        var foeName = e.ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var x in r.Events)
            if (x.Kind == BattleEventKind.CursePass && x.Text == CursePassLabels.Plunder && uke is not null && x.ActorId == uke.InstanceId && x.PartnerId is int fid)
            {
                string n = foeName.GetValueOrDefault(fid, "召喚");
                a.PlFrom[n] = a.PlFrom.GetValueOrDefault(n) + x.Amount;
            }
        // ボス（天井の札の持ち主）の推移
        var boss = e.FirstOrDefault(u => u.HasTrait(TraitId.BossRise11));
        // 戦が続いたターン ＝ 味方の誰かがターン頭の写し（`StatSnapshot`）を持つ最後のターンまで（全滅した後の写しは数えない）
        int lastHit = r.Events.Where(x => x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int sn && mine.Contains(sn)).Select(x => x.Turn).DefaultIfEmpty(0).Max();
        for (int t = 1; t <= Math.Min(TrajT, lastHit); t++) a.TCnt[t] = 1;
        if (boss is not null)
        {
            a.BossN = 1; a.BossMax = boss.MaxHp;
            if (!r.PlayerWon) { a.LostBossHp = Math.Max(0, boss.Hp); a.LostBossHpMin = Math.Max(0, boss.Hp); }
            long hp = boss.MaxHp, low = boss.MaxHp;
            var hpAt = new long[TrajT + 1];
            int lastT = 0;
            long cumDull = 0;
            foreach (var x in r.Events)
            {
                if (x.Kind == BattleEventKind.StatusGain && x.Text == BattleContext.DullKey && x.TargetId == boss.InstanceId) cumDull += x.Amount;
                int t = x.Turn;
                while (lastT < t && lastT < TrajT) { lastT++; hpAt[lastT] = hp; }
                if (x.TargetId == boss.InstanceId && x.Kind is BattleEventKind.Damage or BattleEventKind.Heal) { hp = x.HpAfter; low = Math.Min(low, hp); }
                if (t < 1 || t > TrajT || t > lastHit) continue;
                if (x.Kind == BattleEventKind.Damage && x.TargetId == boss.InstanceId && x.ActorId is int ai && mine.Contains(ai))
                {
                    a.TAlly[t] += x.Amount;
                    if (utsu is not null && ai == utsu.InstanceId) a.TUtsu[t] += x.Amount;
                    if (uke is not null && ai == uke.InstanceId) a.TUke[t] += x.Amount;
                }
                if (x.Kind == BattleEventKind.Heal && x.TargetId == boss.InstanceId) a.THeal[t] += x.Amount;
                if (x.Kind == BattleEventKind.StatSnapshot && x.TargetId == boss.InstanceId) { a.TBAtk[t] += x.Amount; a.TBDull[t] += cumDull; }
            }
            while (lastT < TrajT) { lastT++; hpAt[lastT] = hp; }
            if (!r.PlayerWon) a.LostBossLow = low;
            // ターン t の「末」＝ ターン t+1 の最初の出来事の前（最後のターンは戦の終わり）
            for (int t = 1; t <= TrajT; t++) if (a.TCnt[t] > 0) a.THp[t] = t + 1 <= TrajT && a.TCnt[t + 1] > 0 ? hpAt[t + 1] : Math.Max(0, boss.Hp);
        }
        if (utsu is not null)
            foreach (var x in r.Events)
            {
                if (x.Turn < 1 || x.Turn > TrajT || x.Turn > lastHit) continue;
                if (x.Kind == BattleEventKind.StatSnapshot && x.TargetId == utsu.InstanceId) { a.TUAtk[x.Turn] += x.Amount; a.TUCnt[x.Turn]++; }
                if (x.Kind == BattleEventKind.Attack && x.ActorId == utsu.InstanceId) a.TUSw[x.Turn]++;
            }
        return a;
    }

    internal static Fx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    /// <summary>ボスをターン K までに倒すのに要る、1ターンあたりの与ダメ（一定と見て）。HP M・回復 ＝ ターン頭に 0.4M（満タンを越えない）。</summary>
    internal static double NeedPerTurn(double m, double healFrac, int k) => (m + (k - 1) * healFrac * m) / k;

    // ---------------------------------------------------------------------------------
    // §2-1 机上の到達計算
    // ---------------------------------------------------------------------------------
    static void PrintTraj(string title, Fx a)
    {
        Console.WriteLine($"#### {title}（勝率 {F1(a.Win)}%・負けた戦の最後のボスの HP 平均 {Per(a.LostBossHp, a.Lost)} ／ 最小 {(a.Lost == 0 ? "—" : a.LostBossHpMin.ToString())}・戦の中で最も減った HP の平均 {Per(a.LostBossLow, a.Lost)}）");
        Console.WriteLine();
        Console.WriteLine("ターン t まで戦が続いた（味方の誰かがターン頭の写しを持つ）戦の平均。与ダメはボスへの分だけ。ウツの攻撃力はターン頭（`StatSnapshot`）、手数は振った回数（`Attack`）。");
        Console.WriteLine();
        Console.WriteLine("| T | 続いた戦 | 味方の与ダメ | うちウツ | うちウケ | ボスの回復 | ターン末のボスの HP | ウツの攻撃力 | ウツの手数 | ボスの攻撃力 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int t = 1; t <= TrajT; t++)
        {
            long c = a.TCnt[t];
            Console.WriteLine($"| {t} | {c} | {Per(a.TAlly[t], c)} | {Per(a.TUtsu[t], c)} | {Per(a.TUke[t], c)} | {Per(a.THeal[t], c)} | {Per(a.THp[t], c)} | {Per(a.TUAtk[t], a.TUCnt[t])} | {Per(a.TUSw[t], a.TUCnt[t])} | {Per(a.TBAtk[t], c)} |");
        }
        Console.WriteLine();
    }

    static void Reach()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var a = Measure(Play, I.Boss, GridSeeds);
        double m = a.BossMax, heal = PartyMendPctTrait.Pct / 100.0;
        Console.WriteLine($"# {Tag} §2-1 机上の到達計算 —— 「{PlayRow}」（{Seats(Play)}・規定）× ボス・seed 0..{GridSeeds - 1}");
        Console.WriteLine();
        Console.WriteLine($"勇者の最大 HP は **{m:0}**（ボスの波は敵の倍率を掛けない・実測）・ターン頭に最大 HP の {heal * 100:0}%（{m * heal:0}）癒える（満タンを越えない）。");
        Console.WriteLine();
        PrintTraj("規定", a);
        long n = 0, ally = 0, utsu = 0, healed = 0;
        for (int t = 1; t <= TrajT; t++) if (a.TAlly[t] > 0) { n += a.TCnt[t]; ally += a.TAlly[t]; utsu += a.TUtsu[t]; healed += a.THeal[t]; }
        double now = (double)ally / Math.Max(1, n);
        Console.WriteLine("## 要る与ダメ（1ターンあたり・一定と見て）");
        Console.WriteLine();
        Console.WriteLine($"ターン K で削り切るには D ≥ (M + (K − 1) × {heal:0.0}M) ／ K（毎ターン {heal:0.0}M を上回らない限り HP は減らない）。");
        Console.WriteLine();
        Console.WriteLine("| K | 要る D | いまの平均との倍率 |");
        Console.WriteLine("|--:|--:|--:|");
        foreach (int k in new[] { 6, 8, 10, 12 }) Console.WriteLine($"| {k} | {NeedPerTurn(m, heal, k):0} | ×{NeedPerTurn(m, heal, k) / now:0.0} |");
        Console.WriteLine();
        Console.WriteLine($"いまの味方の与ダメ（味方が与ダメを出したターンの平均・続いた戦あたり）: **{now:0.0} ／T**（うちウツ {(double)utsu / Math.Max(1, n):0.0}）・ボスの実際の回復 {(double)healed / Math.Max(1, n):0.0} ／T。");
        Console.WriteLine();
        // 線形の見込み（§3 の数字で）
        Console.WriteLine("## A ／ B の線形の見込み");
        Console.WriteLine();
        Console.WriteLine("UT-o: 下げ幅 d が 12 を超えた分は 1 につき攻撃力 +2 増し（+1 → +3）・手数は 5 のまま。ターンごとのウツの攻撃力 a（ターン頭）から d ＝ a − 9 と読み、d > 12 の分 × 2 × 5 発を足す。");
        Console.WriteLine("UK-c: ボスがこの戦で受けた弱体の累計の半分がウケの攻撃力に乗る（ウケは1発・ボスは1体なので相手を選ばない）。累計はボスへの弱体の台本（`StatusGain` の `dull`）をターン頭まで足したもの。");
        Console.WriteLine();
        Console.WriteLine("| T | ウツの攻撃力 | d | UT-o の増し ／T | ボスが受けた弱体の累計（ターン頭） | UK-c の増し ／T（ウケ 1発・累計の半分） |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|");
        double addO = 0, addC = 0; long tn = 0;
        for (int t = 1; t <= TrajT; t++)
        {
            if (a.TUCnt[t] == 0 || a.TCnt[t] == 0) continue;
            double ua = (double)a.TUAtk[t] / a.TUCnt[t], d = Math.Max(0, ua - UnitCatalog.UtsuH353.Attack);
            double o = Math.Max(0, d - PerverseOverTrait.Threshold) * (PerverseOverTrait.OverAttackPerDull - PerverseTrait.AttackPerDull) * PerverseTrait.MaxHits;
            double cum = (double)a.TBDull[t] / a.TCnt[t], c = cum / PlunderCumTrait.Divisor;
            if (a.TAlly[t] > 0) { addO += o * a.TCnt[t]; addC += c * a.TCnt[t]; tn += a.TCnt[t]; }
            Console.WriteLine($"| {t} | {ua:0.0} | {d:0.0} | {o:0} | {cum:0.0} | {c:0.0} |");
        }
        Console.WriteLine();
        double o1 = addO / Math.Max(1, tn), c1 = addC / Math.Max(1, tn);
        Console.WriteLine($"見込み（味方が与ダメを出したターンの平均）: UT-o +{o1:0} ／T・UK-c +{c1:0} ／T（ウケが中央のクビの代わりに入る分の差し引きは入れていない）→ 両方で {now + o1 + c1:0} ／T（K ＝ 10 に要る {NeedPerTurn(m, heal, 10):0} の {100 * (now + o1 + c1) / NeedPerTurn(m, heal, 10):0}%）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §2-2 ウケ入りの台
    // ---------------------------------------------------------------------------------
    static void UkSeats(Ver v)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cands = UkCandidates();
        I.W[] ws = { I.Boss, I.Guard, I.Bat };
        var res = cands.Select(c => ws.Select(w => D.GridCell(Apply(c.F, v), w)).ToArray()).ToArray();
        Console.WriteLine($"# {Tag} §2-2 ウケ入りの台 —— 「{PlayRow}」のムド ／ クビ → ウケ（{v.Name}）× ウケの席（{cands.Length} 並び）× ボス ／ 近衛 ／ 大隊・seed 0..{GridSeeds - 1}");
        Console.WriteLine();
        Console.WriteLine("順位は「3波の勝ちの数の和 → ボスの勝ち → 3波の倒れた数 → 決着T」（第351期 `db351 seats` と同じ）。");
        Console.WriteLine();
        Console.WriteLine("| # | 並び（前1・前3・中央・後1・後3） | 外した駒 | ウケとウツが隣 | ボス | 近衛 | 大隊 |");
        Console.WriteLine("|--:|---|---|:-:|---|---|---|");
        var order = Enumerable.Range(0, cands.Length)
            .OrderByDescending(i => res[i].Sum(c => c.Wins)).ThenByDescending(i => res[i][0].Wins).ThenBy(i => res[i].Sum(c => c.Fell)).ThenBy(i => res[i].Sum(c => c.WinT))
            .ThenBy(i => cands[i].Key, StringComparer.Ordinal).ToArray();
        string WT(D.Cell c) => $"{Pct(c.Wins, c.N)}・{(c.Wins == 0 ? "—" : F1((double)c.WinT / c.Wins))}T・倒 {Per2(c.Fell, c.N)}";
        int k = 0;
        foreach (int i in order)
        {
            var f = cands[i].F;
            int us = Enumerable.Range(0, 5).First(s => f[s]?.Id == "uke"), ut = Enumerable.Range(0, 5).First(s => f[s]?.Id == "utsu");
            Console.WriteLine($"| {++k} | {cands[i].Key}{(cands[i].Key == UkPick ? " **（選んだ）**" : "")} | {cands[i].Out} | {(FormationRules.AreAdjacent(us, ut) ? "隣" : "—")} | {WT(res[i][0])} | {WT(res[i][1])} | {WT(res[i][2])} |");
        }
        Console.WriteLine();
        Console.WriteLine($"比べる台: 「{PlayRow}」（{Seats(Play)}）: " + string.Join(" ／ ", ws.Select(w => $"{w.Name} {WT(D.GridCell(Play, w))}")));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
