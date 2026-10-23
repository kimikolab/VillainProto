using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S381 = Sh381Diag;

// =====================================================================================
// sh386 —— 第386期【軽い期】本体: 胞子らしい胞子（版 S0 殴らない ／ P 弾けて毒 ／ Q 寄生 ／ PQ）。規定は1つも変えない。
// 指示書は design/PHASE386_SPORE_SPEC.md、報告は design/PHASE386_SPORE.md。
//
//     dotnet run --project BattleSim -c Release 0 sh386 vers               # §3: 死の台 6 ＋ 毒と組む台（試遊・毒 のドルガ → ムグ）× 規定 ／ S0 ／ P ／ Q ／ PQ × 8 物差し（seed 0..99）——§3-3 の 1〜8
//     dotnet run --project BattleSim -c Release 0 sh386 compare [S0|P|Q|PQ] # §3-1 の 3: `compare` のムグのいる行だけを版で回す（seed 0..199・省略で 4 版すべて）
//     dotnet run --project BattleSim -c Release 0 sh386 check              # 自己検査
//
// 1戦の計数は第377期の器具（`Sh377Diag.One`）＋ 第378 ／ 379 ／ 381期の計数（`Sh381Diag.Extra`）＋ この期の計数（`Extra`）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p386/predict.md`）。版の台 ／ 替えた台は器具の中だけ（試遊の台に足した2台を除く）。
// =====================================================================================
static partial class Sh386Diag
{
    internal const string PredictFile = ".tmp/p386/predict.md";
    const string Tag = "第386期";
    const int Seeds = 100, CompareSeeds = 200, LoopT = 8;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "vers" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh386: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "vers": Versions(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : ""); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh386: モードは vers / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long Xv(S377.B b, string k) => b.X.GetValueOrDefault(k);

    // ---------------------------------------------------------------------------------
    // 版 ／ 台 ／ 波
    // ---------------------------------------------------------------------------------
    internal static readonly string[] VerNames = { "規定", "S0", "P", "Q", "PQ" };
    internal static readonly UnitDef[] MugOf = { UnitCatalog.Mug, UnitCatalog.MugS0, UnitCatalog.MugP, UnitCatalog.MugQ, UnitCatalog.MugPQ };
    internal static Formation Apply(Formation f, int vi) => vi == 0 ? f : I.Swap(f, "mug", MugOf[vi]);

    /// <summary>「試遊・毒」のドルガの席（後3）を規定のムグに替えた台（並びは固定・版の列はそのムグを版に替える）。</summary>
    internal static Formation PoisonMug() { var f = Presets.PoisonHero367().Clone(); f[4] = UnitCatalog.Mug; return f; }

    /// <summary>§3-1 の台（死の台 6 ＋ 毒と組む台 1）。毒と組む台の見どころは第380期 2 ／ 3 位・試遊・死 エレ（スィド入り）と試遊・毒（ムグ）。</summary>
    internal static (string Name, bool Sid, Formation F)[] Boards() => new (string, bool, Formation)[]
    {
        ("試遊・死の連鎖", false, Presets.DeathChain377()),
        ("試遊・死 探索1位", false, Presets.DeathSeek380()),
        ("試遊・死 ソム", false, Presets.DeathSom379()),
        ("試遊・死 エレ", true, Presets.DeathBard382(UnitCatalog.Ere)),
        ("第380期 2 位（スィド＋ドハ）", true, S381.Seat(S381.Top10[1].Seats)),
        ("第380期 3 位（ゴルム＋スィド）", true, S381.Seat(S381.Top10[2].Seats)),
        ("試遊・毒（ドルガ → ムグ）", true, PoisonMug()),
    };
    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;   // ボス ／ 近衛 ／ 大隊 ／ 本編 第2 ／ 4 ／ 5波 ／ 九体 ／ 強 第2〜5波
    const int WBoss = 0, WGuard = 1, WBat = 2, WM5 = 5;

    // ---------------------------------------------------------------------------------
    // この期の計数（`Sh377Diag.One` の `extra`）
    // ---------------------------------------------------------------------------------
    /// <summary>
    /// 第381期までの計数（起き上がり ／ 本当の死 ／ 層の出どころ ／ 勇者の倒しT ／ 隊の1ターンの最大）に、胞子の版の計数を足す:
    /// 湧いた胞子（全部 ／ 撒いた ／ 亡骸から）・胞子の死（撃破者が敵 ／ それ以外）・寄生（全部 ／ 亡骸から湧いた胞子の寄生 ＝ ループ）・寄生の数の推移（ターンの頭・生きた宿主の和）／ 1体の最大・
    /// 寄生の食いの量 ／ 亡骸から湧いた ／ 捨てた・弾けた毒（全部 ／ 勇者へ）・勇者の蝕みで減った回復・敵に書かれた毒 ／ 敵の毒の刻み・スィド ／ ヴィオが敵へ通した量。
    /// </summary>
    internal static void Extra(BattleResult r, List<UnitState> p, List<UnitState> e, S377.B b)
    {
        S381.Extra(r, p, e, b);
        var x = b.X;
        void Add(string k, long v) { if (v != 0) x[k] = x.GetValueOrDefault(k) + v; }
        if (r.TallyByUnit.TryGetValue("mug", out var mt)) Add("sown", mt.R379Sown);
        if (r.TallyByUnit.TryGetValue("spore", out var st)) { Add("bursts", st.R386Burst); Add("burstPsn", st.R386BurstPoison); }
        foreach (var t in r.TallyByUnit.Values) { Add("feedDmg", t.R386FeedDmg); Add("bloom", t.R386Bloom); Add("bloomLost", t.R386BloomLost); }
        UnitState? hero = e.Count == 0 ? null : e.MaxBy(u => u.MaxHp);
        if (hero is not null) Add("venomCut", hero.RawCounter(VenomTaxTrait.CutKey));
        var foe = e.Select(u => u.InstanceId).ToHashSet();
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        int sidId = p.FirstOrDefault(u => u.Def.Id == "sid")?.InstanceId ?? -1, vioId = p.FirstOrDefault(u => u.Def.Id == "vio")?.InstanceId ?? -1;
        var spores = new HashSet<int>(); var fromHost = new HashSet<int>(); var hosts = new Dictionary<int, int>();
        int parMax = 0;
        foreach (var ev in r.Events)
        {
            int t = ev.Turn;
            switch (ev.Kind)
            {
                case BattleEventKind.Summon when ev.TargetId is int id:
                    if (ev.Team == BattleContext.PlayerTeam) { mine.Add(id); if (ev.Text == UnitCatalog.Spore.Name) { spores.Add(id); Add("sporeAll", 1); if (ev.ActorId is int a && foe.Contains(a)) fromHost.Add(id); } }
                    else foe.Add(id);
                    break;
                case BattleEventKind.TurnStart when t is >= 1 and <= LoopT:
                    x[$"parN{t}"] = 1; Add($"par{t}", hosts.Values.Sum());
                    break;
                case BattleEventKind.Parasite when ev.TargetId is int h:
                    Add("parasite", 1); hosts[h] = ev.Amount; parMax = Math.Max(parMax, ev.Amount);
                    if (ev.ActorId is int ps && fromHost.Contains(ps)) Add("loop", 1);
                    break;
                case BattleEventKind.Death when ev.TargetId is int d:
                    hosts.Remove(d);
                    if (spores.Contains(d)) Add(ev.ActorId is int k && foe.Contains(k) ? "sdFoe" : "sdOther", 1);
                    break;
                case BattleEventKind.StatusGain when ev.TargetId is int g && foe.Contains(g) && ev.Text == StatusKeys.Poison:
                    Add("foeGain", ev.Amount);
                    if (ev.PoisonRoute == PoisonRoute.SporeBurst && g == hero?.InstanceId) Add("burstHero", ev.Amount);
                    break;
                case BattleEventKind.Status when ev.TargetId is int s && foe.Contains(s) && ev.Text == "毒":
                    Add("foeTick", ev.Amount);
                    break;
                case BattleEventKind.Damage when ev.TargetId is int dt && foe.Contains(dt):
                    if (ev.ActorId == sidId) Add("sidDmg", ev.Amount);
                    else if (ev.ActorId == vioId) Add("vioDmg", ev.Amount);
                    break;
            }
        }
        if (parMax > 0) x["parMax"] = parMax;
        if (parMax > 30) x["par30"] = 1;
    }

    static S377.B M(Formation f, I.W[] ws, int seeds) => S377.Measure(f, ws, seeds, Extra);

    /// <summary>勝率・決着T（勝った戦）・本当の死 ／ 起き上がり（1戦あたり）。</summary>
    static string Cell(S377.B b) => $"{F1(b.Win)}・{b.WinTs}T・死 {Per(Xv(b, "realDeaths"), b.N)} ／ 起 {Per(Xv(b, "rise"), b.N)}";
    static string TeamMax(S377.B b) => F0((double)Xv(b, "teamMax") / Math.Max(1, b.N));

    // ---------------------------------------------------------------------------------
    // §3 版
    // ---------------------------------------------------------------------------------
    static void Versions()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        int nb = boards.Length, nv = VerNames.Length, nw = W8.Length;
        var g = new S377.B[nb, nv, nw];
        Parallel.For(0, nb * nv * nw, k =>
        {
            int bi = k / (nv * nw), vi = k / nw % nv, wi = k % nw;
            g[bi, vi, wi] = M(Apply(boards[bi].F, vi), W8[wi].Ws, Seeds);
        });
        Console.WriteLine($"# {Tag} §3 胞子の版 —— 台 {nb} × 版（{string.Join(" ／ ", VerNames)}）× {nw} 物差し（seed 0..{Seeds - 1}・ムグだけ版）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。");
        Console.WriteLine();

        // ---- 1. 勝率 ----
        Console.WriteLine("## 1. 勝率・決着T（勝った戦）・倒れた編成の駒（本当の死 ／ 起き上がり・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", W8.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => Cell(g[bi, vi, wi])))} |");
        Console.WriteLine();
        Console.WriteLine("### 1'. ボスの倒しT（ボスに勝った戦の決着T・第385期までと同じ物差し）／ 隊の1ターンの最大の与ダメ（ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", VerNames) + " |");
        Console.WriteLine("|---|" + string.Concat(VerNames.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, nv).Select(vi => $"{g[bi, vi, WBoss].WinTs}T・{TeamMax(g[bi, vi, WBoss])} ／ {TeamMax(g[bi, vi, WGuard])} ／ {TeamMax(g[bi, vi, WBat])}"))} |");
        Console.WriteLine();
        Console.WriteLine("### 1''. 台の平均の勝率（死の台 6 台 ／ 毒と組む台 4 台（スィド入り 3 ＋ 試遊・毒））");
        Console.WriteLine();
        Console.WriteLine("| 台の組 | 版 | " + string.Join(" | ", W8.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "--:|")));
        var dead = Enumerable.Range(0, 6).ToArray(); var psn = Enumerable.Range(0, nb).Where(bi => boards[bi].Sid).ToArray();
        foreach (var (nm, set) in new[] { ("死の台 6", dead), ("毒と組む台 4", psn) })
            for (int vi = 0; vi < nv; vi++)
                Console.WriteLine($"| {(vi == 0 ? nm : "")} | {VerNames[vi]} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => F1(set.Average(bi => g[bi, vi, wi].Win))))} |");
        Console.WriteLine();

        // ---- 2. S0 と規定の差 ----
        Console.WriteLine("## 2. S0 − 規定（胞子の殴りを外した代金・勝率 pt ／ 決着T）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", W8.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(W8.Select(_ => "---|")));
        double maxDrop = 0;
        for (int bi = 0; bi < nb; bi++)
        {
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi =>
            {
                var a = g[bi, 0, wi]; var s = g[bi, 1, wi];
                maxDrop = Math.Min(maxDrop, s.Win - a.Win);
                string dt = a.Wins == 0 || s.Wins == 0 ? "—" : Sg((double)s.WinT / s.Wins - (double)a.WinT / a.Wins);
                return $"{Sg(s.Win - a.Win)}・{dt}T";
            }))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"いちばん大きい落ち: {Sg(maxDrop)}pt。胞子が敵へ通した量（規定の列・1戦あたり）: " + string.Join("・", Enumerable.Range(0, nb).Select(bi => $"{boards[bi].Name} ボス {Per(g[bi, 0, WBoss].SporeDmg, g[bi, 0, WBoss].N)} ／ 近衛 {Per(g[bi, 0, WGuard].SporeDmg, g[bi, 0, WGuard].N)}")) + "。");
        Console.WriteLine();

        // ---- 3. 胞子 ----
        Console.WriteLine("## 3. 胞子（1戦あたり）——湧いた（撒いた）／ 倒れた（撃破者が敵 ／ それ以外）／ 寄生（＝寄生で消えた）／ 亡骸から湧いた（捨てた）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ボス | 近衛 | 大隊 | 本編 第5波 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        string SporeCell(S377.B b) => $"{Per(Xv(b, "sporeAll"), b.N)}（{Per(Xv(b, "sown"), b.N)}）／ {Per(Xv(b, "sdFoe"), b.N)} ／ {Per(Xv(b, "sdOther"), b.N)} ／ {Per(Xv(b, "parasite"), b.N)} ／ {Per(Xv(b, "bloom"), b.N)}（{Per(Xv(b, "bloomLost"), b.N)}）";
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {string.Join(" | ", new[] { WBoss, WGuard, WBat, WM5 }.Select(wi => SporeCell(g[bi, vi, wi])))} |");
        Console.WriteLine();

        // ---- 4. P ----
        Console.WriteLine("## 4. 弾けて毒（P ／ PQ）——胞子の毒（1戦あたり・全部 ／ 勇者へ）・勇者の蝕みで減った回復・敵に書かれた毒 ／ 敵の毒の刻み・スィド ／ ヴィオが敵へ通した量");
        Console.WriteLine();
        Console.WriteLine("スィドは手番で殴らない（吐く ／ 衣で返す）ので、スィドが撃ち手の `Damage` は 0 になる——スィドの働きは「書かれた毒」に入る。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ボス: 胞子の毒（勇者）・蝕み −回復・書かれた毒 ／ 刻み・スィド ／ ヴィオ | 近衛: 胞子の毒・書かれた毒 ／ 刻み | 大隊: 胞子の毒・書かれた毒 ／ 刻み |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (int bi in Enumerable.Range(0, nb))
            foreach (int vi in new[] { 0, 2, 4 })
            {
                var bo = g[bi, vi, WBoss];
                string El(S377.B b) => $"{Per(Xv(b, "burstPsn"), b.N)}・{Per(Xv(b, "foeGain"), b.N)} ／ {Per(Xv(b, "foeTick"), b.N)}";
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {Per(Xv(bo, "burstPsn"), bo.N)}（{Per(Xv(bo, "burstHero"), bo.N)}）・{Per(Xv(bo, "venomCut"), bo.N)}・{Per(Xv(bo, "foeGain"), bo.N)} ／ {Per(Xv(bo, "foeTick"), bo.N)}・{Per(Xv(bo, "sidDmg"), bo.N)} ／ {Per(Xv(bo, "vioDmg"), bo.N)} | {El(g[bi, vi, WGuard])} | {El(g[bi, vi, WBat])} |");
            }
        Console.WriteLine();

        // ---- 5. Q ----
        Console.WriteLine($"## 5. 寄生（Q ／ PQ）——ボスの寄生の数の推移（ターンの頭・生きた宿主の和・T1〜T{LoopT}）・寄生の食い・ループ（亡骸から湧いた胞子がまた寄生した回数）");
        Console.WriteLine();
        Console.WriteLine($"| 台 | 版 | ボス T1〜T{LoopT} | ボス: 寄生 ／ 食い ／ 湧いた ／ ループ | 近衛: 寄生 ／ 食い ／ 湧いた（捨てた）／ ループ | 大隊: 同じ |");
        Console.WriteLine("|---|---|---|---|---|---|");
        string QCell(S377.B b) => $"{Per(Xv(b, "parasite"), b.N)} ／ {Per(Xv(b, "feedDmg"), b.N)} ／ {Per(Xv(b, "bloom"), b.N)}（{Per(Xv(b, "bloomLost"), b.N)}）／ {Per(Xv(b, "loop"), b.N)}";
        foreach (int bi in Enumerable.Range(0, nb))
            foreach (int vi in new[] { 3, 4 })
            {
                var bo = g[bi, vi, WBoss];
                string seq = string.Join(" ", Enumerable.Range(1, LoopT).Select(t => Xv(bo, $"parN{t}") == 0 ? "—" : F1((double)Xv(bo, $"par{t}") / Xv(bo, $"parN{t}"))));
                Console.WriteLine($"| {(vi == 3 ? boards[bi].Name : "")} | {VerNames[vi]} | {seq} | {QCell(bo)} | {QCell(g[bi, vi, WGuard])} | {QCell(g[bi, vi, WBat])} |");
            }
        Console.WriteLine();

        // ---- 6. リィカ ----
        Console.WriteLine($"## 6. リィカ（ボス）——層の推移（T1〜T{LoopT}・生きている戦の平均）・9 層に届いた戦 ／ T・層の出どころ（1戦あたり: 胞子 ／ 起き上がり ／ 亡者 ／ 本当の死）");
        Console.WriteLine();
        Console.WriteLine($"| 台 | 版 | 層 T1〜T{LoopT} | 9 層（戦% ／ T）| 出どころ 胞子 ／ 起 ／ 亡者 ／ 死 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (int bi in Enumerable.Range(0, nb))
        {
            if (!S377.HasRica(boards[bi].F)) continue;
            for (int vi = 0; vi < nv; vi++)
            {
                var bo = g[bi, vi, WBoss];
                string l9 = Xv(bo, "l9") == 0 ? "0 ／ —" : $"{Pct(Xv(bo, "l9"), bo.N)} ／ {F1((double)Xv(bo, "l9T") / Xv(bo, "l9"))}";
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {bo.Layers(LoopT)} | {l9} | {Per(Xv(bo, "srcSpore"), bo.N)} ／ {Per(Xv(bo, "srcRise"), bo.N)} ／ {Per(Xv(bo, "srcWight"), bo.N)} ／ {Per(Xv(bo, "srcReal"), bo.N)} |");
            }
        }
        Console.WriteLine();

        // ---- 7. 誰が何Tに倒れたか ----
        Console.WriteLine("## 7. 誰が何Tに倒れたか（規定と最良の版・駒 最初に倒れた戦の割合%（倒れたTの平均）・早い順）");
        Console.WriteLine();
        Console.WriteLine("最良の版 ＝ 版 4 つのうち、ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件の勝率の和が最も高い版（同じなら前の版）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ボス | 近衛 | 大隊 | 本編 第5波 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        var key = new[] { WBoss, WGuard, WBat, WM5, nw - 1 };
        var best = new int[nb];
        for (int bi = 0; bi < nb; bi++)
        {
            best[bi] = Enumerable.Range(1, nv - 1).OrderByDescending(vi => key.Sum(wi => g[bi, vi, wi].Win)).ThenBy(vi => vi).First();
            foreach (int vi in new[] { 0, best[bi] })
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {string.Join(" | ", new[] { WBoss, WGuard, WBat, WM5 }.Select(wi => g[bi, vi, wi].FellCell))} |");
        }
        Console.WriteLine();

        // ---- 8. 壊れの兆し ----
        Console.WriteLine("## 8. 壊れの兆し");
        Console.WriteLine();
        Console.WriteLine("精鋭の 1T 決着（近衛 ／ 大隊・勝った戦の %）・ボス 4T 未満（ボスの戦の %）・30T の打ち切り（8 物差しの合計の %・規定との差）・層 20 超（ボス ／ 近衛 ／ 大隊の戦の %）・寄生の数 30 超（1体の宿主・同じ）・int のあふれの兆し（全戦の件数）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 精鋭 1T | ボス 4T 未満 | 打ち切り（差）| 層 20 超 | 寄生 30 超 | あふれ |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        var warn = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                var gu = g[bi, vi, WGuard]; var ba = g[bi, vi, WBat]; var bo = g[bi, vi, WBoss];
                double e1 = (double)(gu.Win1T + ba.Win1T) / Math.Max(1, gu.Wins + ba.Wins) * 100, fast = 100.0 * bo.BossFast / bo.N;
                double Cap(int v) => 100.0 * Enumerable.Range(0, nw).Sum(wi => g[bi, v, wi].Cap) / Enumerable.Range(0, nw).Sum(wi => g[bi, v, wi].N);
                double cap = Cap(vi), capD = cap - Cap(0);
                long n3 = bo.N + gu.N + ba.N;
                double p20 = 100.0 * (Xv(bo, "peak20") + Xv(gu, "peak20") + Xv(ba, "peak20")) / n3, q30 = 100.0 * (Xv(bo, "par30") + Xv(gu, "par30") + Xv(ba, "par30")) / n3;
                long ovf = Enumerable.Range(0, nw).Sum(wi => g[bi, vi, wi].Overflow);
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {F1(e1)} | {F1(fast)} | {F1(cap)}（{Sg(capD)}）| {F1(p20)} | {F1(q30)} | {ovf} |");
                if (vi == 0) continue;
                if (e1 >= 5) warn.Add($"{boards[bi].Name} {VerNames[vi]}: 精鋭 1T {F1(e1)}%");
                if (fast > 0) warn.Add($"{boards[bi].Name} {VerNames[vi]}: ボス 4T 未満 {F1(fast)}%");
                if (capD >= 5) warn.Add($"{boards[bi].Name} {VerNames[vi]}: 打ち切り {Sg(capD)}pt");
                if (q30 > 0) warn.Add($"{boards[bi].Name} {VerNames[vi]}: 寄生 30 超 {F1(q30)}%");
                if (ovf > 0) warn.Add($"{boards[bi].Name} {VerNames[vi]}: あふれ {ovf}");
            }
        Console.WriteLine();
        Console.WriteLine(warn.Count == 0 ? "兆し: なし。" : "兆し: " + string.Join("・", warn) + "。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 3 `compare`（ムグのいる行だけ）
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw, wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareVer(string which)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vis = which == "" ? new[] { 1, 2, 3, 4 } : new[] { Array.IndexOf(VerNames, which) };
        if (vis[0] <= 0) { Console.WriteLine("sh386 compare: 版は S0 / P / Q / PQ。"); return; }
        var all = CompareBuilds();
        var rows = all.Where(r => S377.HasMug(r.F)).ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        bool others = all.Where(r => !S377.HasMug(r.F)).All(r => vis.All(vi => ReferenceEquals(Apply(r.F, vi), r.F) || Apply(r.F, vi).Occupied().Select(o => o.Def).SequenceEqual(r.F.Occupied().Select(o => o.Def))));
        var b = CGrid(rows);
        Console.WriteLine($"# {Tag} §3-1 の 3 `compare` —— ムグのいる行 {rows.Length} 行（全 {all.Length} 行）× 版（seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"ムグのいる行: {string.Join("、", rows.Select(r => r.Name))}。主判定19行に入っている行: {string.Join("、", rows.Where(r => Baseline.PrimaryRows.Contains(r.Name)).Select(r => r.Name).DefaultIfEmpty("なし"))}。");
        Console.WriteLine($"ムグのいない行は版の写像で1枚も変わらない（回さない）: {(others ? "○" : "**×（止めて報告）**")}。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 規定 第1〜5波 | " + string.Join(" | ", vis.Select(vi => VerNames[vi])) + " |");
        Console.WriteLine("|---|---|" + string.Concat(vis.Select(_ => "---|")));
        var grids = vis.Select(vi => CGrid(rows.Select(r => (r.Name, Apply(r.F, vi))).ToArray())).ToArray();
        for (int ri = 0; ri < rows.Length; ri++)
            Console.WriteLine($"| {rows[ri].Name} | {string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(b[ri, w])))} | {string.Join(" | ", grids.Select(gg => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => gg[ri, w] == b[ri, w] ? "=" : F1(gg[ri, w])))))} |");
        Console.WriteLine();
        Console.WriteLine("（「=」は規定と同じセル。）");
        Console.WriteLine();
        for (int k = 0; k < vis.Length; k++)
        {
            var gg = grids[k];
            int moved = 0, up = 0, down = 0, info0 = 0, info1 = 0;
            for (int ri = 0; ri < rows.Length; ri++)
                for (int w = 1; w < nw; w++)
                {
                    if (gg[ri, w] > b[ri, w]) { moved++; up++; } else if (gg[ri, w] < b[ri, w]) { moved++; down++; }
                    if (b[ri, w] is > 0 and < 100) info0++;
                    if (gg[ri, w] is > 0 and < 100) info1++;
                }
            var drops = Enumerable.Range(0, rows.Length).SelectMany(ri => Enumerable.Range(1, nw - 1).Where(w => b[ri, w] - gg[ri, w] >= 10.0).Select(w => $"{rows[ri].Name} 第{w + 1}波 {F1(b[ri, w])} → {F1(gg[ri, w])}")).ToList();
            Console.WriteLine($"- **{VerNames[vis[k]]}**: 動いたセル（第2〜5波）{moved}（上がった {up} ／ 落ちた {down}）・情報セル {info0} → {info1}・−10pt 以上の落ち: {(drops.Count == 0 ? "なし" : string.Join("、", drops))}。");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
