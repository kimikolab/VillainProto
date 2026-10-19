using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// =====================================================================================
// sh358 —— 第358期【軽い期】破片軸の立て直し①: 返しの破片（ヒビ HB-r）／ 礫を拾って投げる（ガレ GR-r）。
// 指示書は design/PHASE358_SHARD_AXIS_SPEC.md ／ 報告は design/PHASE358_SHARD_AXIS.md。波と1戦の計数は `inv348` ／ `db351` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 sh358 p0                  # §2 Phase 0: 破片の流れ ／ ツギの板の返し ／ ヒビの砕け ／ ボスの1ターン（規定・代表台 3 × 5 波・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh358 seats [版]          # §4: ヒビ ／ ガレ ＋ 6枚から3枚（20 組）× 席の全並び 120 ＝ 2,400 並び × ボス（seed 0..4）／ 近衛 ／ 大隊（seed 0..19）・版は base ／ hbgr
//     dotnet run --project BattleSim -c Release 0 sh358 flow [版,…]         # §6 の 1・2: 破片の流れ ／ ボスへの届き方 × 版（代表台 × ボス ／ 大隊）
//     dotnet run --project BattleSim -c Release 0 sh358 boards [版,…]       # §6 の 3・6: 代表台 × 波 × 版（seed 0..99）・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh358 axes                # §6 の 4: 軸の横並び（第348期 §3）の破片の行 × 版
//     dotnet run --project BattleSim -c Release 0 sh358 compare [版,…]      # §6 の 5: `compare` 64 行 × 版（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh358 log <版> <台> <波> [seed]   # 1戦のログ（台 gare ／ uro ／ wall ／ 並び「ヒビ・ガレ・…」）
//     dotnet run --project BattleSim -c Release 0 sh358 check               # 自己検査
//
// 版の名前（ASCII）: base（規定）／ hb（HB-r）／ gr（GR-r）／ hbgr（HB-r ＋ GR-r）。版はヒビ ／ ガレだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ版の本測定（seats ／ flow ／ boards ／ axes ／ compare）を走らせない**（R397・`.tmp/p358/predict.md`）。
// =====================================================================================
static partial class Sh358Diag
{
    internal const string PredictFile = ".tmp/p358/predict.md";
    const string Tag = "第358期";
    const int Seeds = 100, CompareSeeds = 200, TrajT = 14, BossSeeds = 5, EliteSeeds = 20;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "flow" or "boards" or "axes" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh358: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": Phase0(); return;
            case "seats": SeatsAll(V(A(3, "hbgr"))); return;
            case "flow": FlowAll(Pick(A(3, ""))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "axes": AxesAll(); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "gare"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh358: モードは p0 / seats / flow / boards / axes / compare / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    // ---------------------------------------------------------------------------------
    // 版
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef Hibi, UnitDef Gare);
    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.Hibi, UnitCatalog.Gare),
        new("HB-r 返しの破片", "hb", UnitCatalog.HibiHBr, UnitCatalog.Gare),
        new("GR-r 礫を拾う", "gr", UnitCatalog.Hibi, UnitCatalog.GareGRr),
        new("HB-r ＋ GR-r", "hbgr", UnitCatalog.HibiHBr, UnitCatalog.GareGRr),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(S316.SwapId(f, "hibi", v.Hibi), "gare", v.Gare);
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static bool Touches(Formation f, Ver v) => (v.Hibi != UnitCatalog.Hibi && Has(f, "hibi")) || (v.Gare != UnitCatalog.Gare && Has(f, "gare"));

    // ---------------------------------------------------------------------------------
    // 台
    // ---------------------------------------------------------------------------------
    internal const string GareRow = "礫 (ガレ×ヒビ×ウロ)", UroRow = "鱗改 (ウロ×ヒビ)", WallRow = "試遊・呪い 壁";
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;
    /// <summary>ツギのいる台（決めたこと: 「試遊・呪い 壁」・第356期の総当たりでツギの板がボスへ届かせた台・ヒビ ／ ガレはいない）。</summary>
    static Formation Wall => Presets.Versions.First(r => r.Name == WallRow).F;
    internal static I.Board[] P0Boards() => new[]
    {
        new I.Board("破片", GareRow, Row(GareRow)),
        new I.Board("破片", UroRow, Row(UroRow)),
        new I.Board("板", WallRow, Wall),
    };
    internal static readonly I.W[] P0Waves = { I.Boss, I.Guard, I.Bat, I.M2, I.M5 };
    internal static readonly I.W[] Waves = new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5, I.Nine }.Concat(I.StrongWaves).ToArray();

    static Formation BoardOf(string n) => n switch
    {
        "gare" => Row(GareRow), "uro" => Row(UroRow), "wall" => Wall,
        _ when n.Contains('・') => ByKey(n),
        _ => CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Versions).First(r => r.Name.StartsWith(n, StringComparison.Ordinal)).F,
    };

    static void Log(Ver v, string board, string wave, int seed)
    {
        var f = Apply(BoardOf(board), v); var w = D.WaveOf(wave);
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {Seats(f)} × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose・帳簿と台本を読む・盤面は動かさない）
    // ---------------------------------------------------------------------------------
    internal sealed class Fx
    {
        public long N, Wins, WinT, Win1, Lost, WipeT, Fell, HibiDied, HibiDeathT, BossN;
        // 破片の流れ（駒の短い名前ごと）
        public readonly Dictionary<string, long> Wrote = new(), Recv = new(), Broken = new(), Left = new(), BossDmg = new();
        public long ShatterTicks, ShatterGiven, UroSpent, GareShards, Refused;
        // ツギの板の返し ／ ヒビの返し（回数・名目・実際）と、1回の攻撃で同じ主へ集まった塊（2本以上）
        public long PlankN, PlankAmt, PlankDealt, PlankGroups, PlankGroupAmt, PlankGroupHits;
        public long RetN, RetAmt, RetDealt, RetKills, RetGroups, RetGroupAmt, RetGroupHits, RetAfterDeath, RetYokeCut;
        // 礫
        public long Picked, Throws, Thrown, ThrowNominal, ThrowDealt, ThrowYokeCut, Gifts, Given;
        // ターンごと（ボスの戦・そのターンまで戦が続いた戦の和 ／ 数）: 与ダメ（返し ／ 礫 ／ 板 ／ ほか）／ ボスの回復
        public readonly int T;
        public readonly long[] TRet, TPeb, TPlank, TOther, THeal, TCnt;
        public Fx(int t = TrajT) { T = t; TRet = new long[t + 1]; TPeb = new long[t + 1]; TPlank = new long[t + 1]; TOther = new long[t + 1]; THeal = new long[t + 1]; TCnt = new long[t + 1]; }
        static void M(Dictionary<string, long> a, Dictionary<string, long> b) { foreach (var (k, x) in b) a[k] = a.GetValueOrDefault(k) + x; }
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1 += o.Win1; Lost += o.Lost; WipeT += o.WipeT; Fell += o.Fell; HibiDied += o.HibiDied; HibiDeathT += o.HibiDeathT; BossN += o.BossN;
            M(Wrote, o.Wrote); M(Recv, o.Recv); M(Broken, o.Broken); M(Left, o.Left); M(BossDmg, o.BossDmg);
            ShatterTicks += o.ShatterTicks; ShatterGiven += o.ShatterGiven; UroSpent += o.UroSpent; GareShards += o.GareShards; Refused += o.Refused;
            PlankN += o.PlankN; PlankAmt += o.PlankAmt; PlankDealt += o.PlankDealt; PlankGroups += o.PlankGroups; PlankGroupAmt += o.PlankGroupAmt; PlankGroupHits += o.PlankGroupHits;
            RetN += o.RetN; RetAmt += o.RetAmt; RetDealt += o.RetDealt; RetKills += o.RetKills; RetGroups += o.RetGroups; RetGroupAmt += o.RetGroupAmt; RetGroupHits += o.RetGroupHits;
            RetAfterDeath += o.RetAfterDeath; RetYokeCut += o.RetYokeCut;
            Picked += o.Picked; Throws += o.Throws; Thrown += o.Thrown; ThrowNominal += o.ThrowNominal; ThrowDealt += o.ThrowDealt; ThrowYokeCut += o.ThrowYokeCut; Gifts += o.Gifts; Given += o.Given;
            for (int t = 0; t <= Math.Min(T, o.T); t++) { TRet[t] += o.TRet[t]; TPeb[t] += o.TPeb[t]; TPlank[t] += o.TPlank[t]; TOther[t] += o.TOther[t]; THeal[t] += o.THeal[t]; TCnt[t] += o.TCnt[t]; }
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }

    static readonly int ArmorKi = Array.IndexOf(StatusKeys.All, StatusKeys.Armor);

    internal static Fx One(Formation f, I.W w, int seed, int tt = TrajT)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Fx(tt) { N = 1, Fell = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.Win1 = 1; } else a.Lost = 1;
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team == BattleContext.PlayerTeam) mine.Add(sid);
        var name = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        int last = r.Events.Where(x => x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int sn && mine.Contains(sn)).Select(x => x.Turn).DefaultIfEmpty(0).Max();
        if (!r.PlayerWon) a.WipeT = last;
        var hibi = p.FirstOrDefault(u => u.Def.Id == "hibi");
        if (hibi is not null && hibi.LastDeathTurn > 0) { a.HibiDied = 1; a.HibiDeathT = hibi.LastDeathTurn; }
        foreach (var u in p)
        {
            string k = Short(u.Def);
            if (!r.TallyByUnit.TryGetValue(u.Def.Id, out var t)) continue;
            if (t.ArmorOut > 0) a.Wrote[k] = a.Wrote.GetValueOrDefault(k) + t.ArmorOut;
            long recv = t.Inv348In is long[] inn ? inn[ArmorKi * 8 + 0 * 2 + 1] + inn[ArmorKi * 8 + 1 * 2 + 1] : 0;
            if (recv > 0) a.Recv[k] = a.Recv.GetValueOrDefault(k) + recv;
            if (t.Sh358ArmorBroken > 0) a.Broken[k] = a.Broken.GetValueOrDefault(k) + t.Sh358ArmorBroken;
            if (u.IsAlive && u.RawCounter(StatusKeys.Armor) > 0) a.Left[k] = a.Left.GetValueOrDefault(k) + u.RawCounter(StatusKeys.Armor);
            a.PlankN += t.ReflectCount; a.PlankAmt += t.ReflectNominal; a.PlankDealt += t.ReflectDealt;
            a.RetN += t.Sh358Returns; a.RetAmt += t.Sh358ReturnNominal; a.RetDealt += t.Sh358ReturnDealt; a.RetKills += t.Sh358ReturnKills; a.RetAfterDeath += t.Sh358ReturnAfterDeath; a.RetYokeCut += t.Sh358ReturnYokeCut;
            a.Picked += t.Sh358Picked; a.Throws += t.Sh358Throws; a.Thrown += t.Sh358Thrown; a.ThrowNominal += t.Sh358ThrowNominal; a.ThrowDealt += t.Sh358ThrowDealt; a.ThrowYokeCut += t.Sh358ThrowYokeCut;
            a.Gifts += t.Sh358Gifts; a.Given += t.Sh358Given; a.Refused += t.Sh358Refused;
        }
        a.ShatterTicks = r.ShatterTicks; a.ShatterGiven = r.ShatterGiven; a.UroSpent = r.ScaleSpentHit + r.ScaleSpentAttack; a.GareShards = r.ShrapnelShards;
        // 1回の攻撃で同じ主へ集まった塊（通し番号ごと）
        var pg = new Dictionary<int, (int N, long A)>(); var rg = new Dictionary<int, (int N, long A)>();
        foreach (var x in r.Events)
        {
            if (x.Slot <= 0) continue;
            if (x.Kind == BattleEventKind.Plank && x.Text == PlankLabels.Reflect) { var c = pg.GetValueOrDefault(x.Slot); pg[x.Slot] = (c.N + 1, c.A + x.Amount); }
            if (x.Kind == BattleEventKind.Shard && x.Text == ShardLabels.Return) { var c = rg.GetValueOrDefault(x.Slot); rg[x.Slot] = (c.N + 1, c.A + x.Amount); }
        }
        foreach (var c in pg.Values.Where(c => c.N >= 2)) { a.PlankGroups++; a.PlankGroupAmt += c.A; a.PlankGroupHits += c.N; }
        foreach (var c in rg.Values.Where(c => c.N >= 2)) { a.RetGroups++; a.RetGroupAmt += c.A; a.RetGroupHits += c.N; }

        var boss = e.FirstOrDefault(u => u.HasTrait(TraitId.BossRise11));
        if (boss is not null)
        {
            a.BossN = 1;
            for (int t = 1; t <= Math.Min(tt, last); t++) a.TCnt[t] = 1;
            var gare = p.FirstOrDefault(u => u.Def.Id == "gare");
            int pend = 0; int? pendTo = null;   // 1 板の返し ／ 2 欠片の返し（直後の同じ相手への Damage）
            foreach (var x in r.Events)
            {
                if (x.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart) { pend = 0; pendTo = null; }
                if (x.Kind == BattleEventKind.Plank && x.Text == PlankLabels.Reflect) { pend = 1; pendTo = x.TargetId; continue; }
                if (x.Kind == BattleEventKind.Shard && x.Text == ShardLabels.Return) { pend = 2; pendTo = x.TargetId; continue; }
                int tn = x.Turn;
                if (x.Kind == BattleEventKind.Heal && x.TargetId == boss.InstanceId && tn >= 1 && tn <= tt && tn <= last) a.THeal[tn] += x.Amount;
                if (x.Kind != BattleEventKind.Damage || x.TargetId != boss.InstanceId) continue;
                int kind = pend != 0 && pendTo == x.TargetId ? pend : 0;
                pend = 0; pendTo = null;
                if (x.ActorId is not int ai || !mine.Contains(ai)) continue;
                string who = name.GetValueOrDefault(ai, "召喚");
                a.BossDmg[who] = a.BossDmg.GetValueOrDefault(who) + x.Amount;
                if (tn < 1 || tn > tt || tn > last) continue;
                if (kind == 2) a.TRet[tn] += x.Amount;
                else if (kind == 1) a.TPlank[tn] += x.Amount;
                else if (gare is not null && ai == gare.InstanceId) a.TPeb[tn] += x.Amount;
                else a.TOther[tn] += x.Amount;
            }
        }
        return a;
    }

    internal static Fx Measure(Formation f, I.W w, int n = Seeds, int tt = TrajT)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i, tt));
        var all = new Fx(tt);
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static string Kv(Dictionary<string, long> d, long n) => d.Count == 0 ? "—" : string.Join("、", d.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, n)}"));

    internal static void PrintTraj(string title, Fx a)
    {
        Console.WriteLine($"#### {title}（勝率 {F1(a.Win)}%・倒しT {a.WinTs}・全滅 T {Per(a.WipeT, a.Lost)}）");
        Console.WriteLine();
        Console.WriteLine($"ボスへの与ダメ（1戦あたり・駒ごと）: {Kv(a.BossDmg, a.N)}。ターン t まで戦が続いた戦の平均。");
        Console.WriteLine();
        Console.WriteLine("| T | 続いた戦 | 与ダメ 計 | うち欠片の返し | うち礫（ガレ） | うち板の返し | ほか | ボスの回復 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int t = 1; t <= a.T; t++)
        {
            long c = a.TCnt[t];
            if (c == 0) continue;
            long all = a.TRet[t] + a.TPeb[t] + a.TPlank[t] + a.TOther[t];
            Console.WriteLine($"| {t} | {c} | {Per(all, c)} | {Per(a.TRet[t], c)} | {Per(a.TPeb[t], c)} | {Per(a.TPlank[t], c)} | {Per(a.TOther[t], c)} | {Per(a.THeal[t], c)} |");
        }
        Console.WriteLine();
    }

    internal static void PrintFlow(string title, Fx a)
    {
        long n = a.N;
        Console.WriteLine($"#### {title}（勝率 {F1(a.Win)}%・決着T {a.WinTs}・倒れた {Per2(a.Fell, n)}・ヒビが倒れた戦 {Pct(a.HibiDied, n)}%・倒れた T {Per(a.HibiDeathT, a.HibiDied)}）");
        Console.WriteLine();
        Console.WriteLine($"- 書いた（書き手・1戦あたり）: {Kv(a.Wrote, n)}。受けた（自分 ＋ 味方から）: {Kv(a.Recv, n)}");
        Console.WriteLine($"- 敵の一撃で割れた（受け手）: {Kv(a.Broken, n)}（計 {Per(a.Broken.Values.Sum(), n)}）。戦の終わりに残った: {Kv(a.Left, n)}（計 {Per(a.Left.Values.Sum(), n)}）");
        Console.WriteLine($"- 読んだ: ウロの支払い {Per(a.UroSpent, n)} ／ ガレ（規定）の砕き {Per(a.GareShards, n)} ／ ガレ（GR-r）の礫 拾った {Per(a.Picked, n)}・投げた {Per2(a.Throws, n)} 回 {Per(a.Thrown, n)}（1体あたりの名目 {Per(a.ThrowNominal, a.Throws)} ／ 投げ・実際に減らした {Per(a.ThrowDealt, n)}・軛に切られた {Per(a.ThrowYokeCut, n)}）");
        Console.WriteLine($"- 砕け（規定のヒビ）: 発火 {Per2(a.ShatterTicks, n)} 回 ／ 配った {Per(a.ShatterGiven, n)}。欠片を配る（HB-r）: {Per2(a.Gifts, n)} 回 ／ 配った {Per(a.Given, n)}・纏えずに捨てた {Per(a.Refused, n)}");
        Console.WriteLine($"- 板の返し（ツギ）: {Per2(a.PlankN, n)} 回・1回 {Per(a.PlankAmt, a.PlankN)}・実際に減らした {Per(a.PlankDealt, n)}・同じ主へ2本以上の塊 {Per2(a.PlankGroups, n)} 回（1塊 {Per2(a.PlankGroupHits, a.PlankGroups)} 本 ／ {Per(a.PlankGroupAmt, a.PlankGroups)}）");
        Console.WriteLine($"- 欠片の返し（HB-r）: {Per2(a.RetN, n)} 回・1回 {Per(a.RetAmt, a.RetN)}・実際に減らした {Per(a.RetDealt, n)}・倒した {Per2(a.RetKills, n)}・同じ主へ2本以上の塊 {Per2(a.RetGroups, n)} 回（1塊 {Per2(a.RetGroupHits, a.RetGroups)} 本 ／ {Per(a.RetGroupAmt, a.RetGroups)}）・ヒビが倒れた後 {Per(a.RetAfterDeath, n)}・軛に切られた {Per(a.RetYokeCut, n)}");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §2 Phase 0（規定）
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = P0Boards();
        Console.WriteLine($"# {Tag} §2 Phase 0 —— 規定 × 代表台 {boards.Length} × {P0Waves.Length} 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        var res = boards.SelectMany(b => P0Waves.Select(w => (b, w))).ToDictionary(x => (x.b.Name, x.w.Name), x => Measure(x.b.F, x.w));
        Console.WriteLine("## 1〜3. 破片の流れ ／ ツギの板の返し ／ ヒビの砕け（台 × 波）");
        Console.WriteLine();
        foreach (var b in boards) foreach (var w in P0Waves) PrintFlow($"{b.Name} × {w.Name}", res[(b.Name, w.Name)]);
        Console.WriteLine("## 4. ボスの戦（1ターンの与ダメ ／ 回復の推移・全滅 T）");
        Console.WriteLine();
        foreach (var b in boards) PrintTraj($"{b.Name} × ボス", res[(b.Name, I.Boss.Name)]);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
