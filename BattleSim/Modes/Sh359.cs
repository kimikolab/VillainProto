using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S8 = Sh358Diag;

// =====================================================================================
// sh359 —— 第359期【軽い期】段0: ガレの GR-r（礫を拾って投げる）を規定に ／ 本体: ヒビの作り直し（HB-s 倒れない ／ HB-f 全量を返す）。
// 指示書は design/PHASE359_SHARD_HIBI_SPEC.md ／ 報告は design/PHASE359_SHARD_HIBI.md。1戦の計数は `sh358`（`Sh358Diag.One` ／ `Measure`）をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 sh359 p0                  # §3 Phase 0: HB-r のヒビが受けたダメージの内訳 ／ ターンごとの累計 ／ 倒れた T・脆弱なしで T5 に立つ HP（机上）・開戦の配りで勇者の T1 を何本で受けるか
//     dotnet run --project BattleSim -c Release 0 sh359 seats               # §5: ヒビ ／ ガレ ＋ 7枚から3枚（35 組）× 席の全並び 120 ＝ 4,200 並び × 規定 ／ HB-s ／ HB-f × ボス（seed 0..4）／ 近衛 ／ 大隊（seed 0..19）
//     dotnet run --project BattleSim -c Release 0 sh359 flow [版,…]         # §7 の 1・2: ヒビの生存と配り ／ 返しと礫 ／ ボスへの届き方 × 版（代表台 × ボス ／ 大隊）
//     dotnet run --project BattleSim -c Release 0 sh359 boards [版,…]       # §7 の 3・6: 代表台 × 11 波 × 版（seed 0..99）・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh359 axes                # §7 の 4: 軸の横並び（第348期 §3）の破片の行 × 版
//     dotnet run --project BattleSim -c Release 0 sh359 compare [版,…]      # §7 の 5: `compare` 64 行 × 版（seed 0..199）＋ 段0 の前とのセルの差
//     dotnet run --project BattleSim -c Release 0 sh359 log <版> <台> <波> [seed]   # 1戦のログ（台 gare ／ uro ／ a ／ b ／ 並び「ドルガ・ササ・ゴルム・ヒビ・ガレ」）
//     dotnet run --project BattleSim -c Release 0 sh359 check               # 自己検査
//
// 版の名前（ASCII）: base（規定・段0 入り）／ hbr（HB-r・参照）／ hbs ／ hbf。版はヒビだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ本測定（seats ／ flow ／ boards ／ axes ／ compare）を走らせない**（R397・`.tmp/p359/predict.md`）。
// =====================================================================================
static partial class Sh359Diag
{
    internal const string PredictFile = ".tmp/p359/predict.md";
    const string Tag = "第359期";
    const int Seeds = 100, CompareSeeds = 200, BossSeeds = 5, EliteSeeds = 20, P0Seeds = 20;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "seatdiff" or "flow" or "boards" or "axes" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh359: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": Phase0(); return;
            case "seats": SeatsAll(); return;
            case "seatdiff": SeatDiff(); return;
            case "flow": FlowAll(Pick(A(3, ""))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "axes": AxesAll(); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "gare"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh359: モードは p0 / seats / flow / boards / axes / compare / log / check。"); return;
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
    internal sealed record Ver(string Name, string Ascii, UnitDef Hibi);
    internal static readonly Ver[] Vers =
    {
        new("規定（段0 入り）", "base", UnitCatalog.HibiH359),
        new("HB-r（参照）", "hbr", UnitCatalog.HibiHBr),
        new("HB-s 倒れないヒビ", "hbs", UnitCatalog.HibiHBs),
        new("HB-f 全量を返す", "hbf", UnitCatalog.HibiHBf),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, "hibi", v.Hibi);
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    // ---------------------------------------------------------------------------------
    // 台
    // ---------------------------------------------------------------------------------
    internal const string GareRow = S8.GareRow, UroRow = S8.UroRow;
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;
    internal const string KeyA = "ウロ・ガルド・ガレ・ツギ・ヒビ", KeyB = "ドルガ・ササ・ゴルム・ヒビ・ガレ";   // 第358期の A ／ B（駒は規定・段0 入り）
    internal static readonly UnitDef[] Fixed = { UnitCatalog.HibiH359, UnitCatalog.GareH364 };
    internal static readonly UnitDef[] Pool = { UnitCatalog.Uro, UnitCatalog.Tsugi, UnitCatalog.SasaH360, UnitCatalog.Gald, UnitCatalog.DolgaH395, UnitCatalog.Golm, UnitCatalog.Nel };
    internal static Formation ByKey(string key)
    {
        var defs = key.Split('・').Select(n => Fixed.Concat(Pool).First(d => Short(d) == n)).ToArray();
        var f = new Formation();
        for (int s = 0; s < 5; s++) f[s] = defs[s];
        return f;
    }
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("破片", GareRow, Row(GareRow)),
        new I.Board("破片", UroRow, Row(UroRow)),
        new I.Board("破片", $"A 第358期のツギあり（{KeyA}）", ByKey(KeyA)),
        new I.Board("破片", $"B 第358期のツギなし（{KeyB}）", ByKey(KeyB)),
    }.Concat(RepKeys.Select(r => new I.Board("破片", $"{r.Label}（{r.Key}）", ByKey(r.Key)))).ToArray();
    internal static readonly I.W[] Waves = S8.Waves;

    static Formation BoardOf(string n) => n switch
    {
        "gare" => Row(GareRow), "uro" => Row(UroRow), "a" => ByKey(KeyA), "b" => ByKey(KeyB),
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
    // 1戦（素の戦・verbose なし）: 勝ち ／ 倒しT ／ 倒れた数 ／ 全滅 T ／ 最初に倒れた駒 ／ ヒビが倒れた T
    // ---------------------------------------------------------------------------------
    internal sealed class Cell
    {
        public long N, Wins, WinT, Fell, Lost, WipeT, HibiDied, HibiT;
        public readonly Dictionary<string, long> First = new();
        public void Add(Cell o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Lost += o.Lost; WipeT += o.WipeT; HibiDied += o.HibiDied; HibiT += o.HibiT;
            foreach (var (k, v) in o.First) First[k] = First.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }
    internal static Cell Fight(Formation f, I.W w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false, boss: new BossRule(false) { Scale = w.Scale });
        var c = new Cell { N = 1, Fell = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { c.Wins = 1; c.WinT = r.Turns; }
        else { c.Lost = 1; c.WipeT = p.Max(u => u.LastDeathTurn); }
        var first = p.Where(u => u.LastDeathTurn > 0).OrderBy(u => u.LastDeathTurn).ThenBy(u => u.Slot).FirstOrDefault();
        if (first is not null) c.First[Short(first.Def)] = 1;
        var hibi = p.FirstOrDefault(u => u.Def.Id == "hibi");
        if (hibi is not null && hibi.LastDeathTurn > 0) { c.HibiDied = 1; c.HibiT = hibi.LastDeathTurn; }
        return c;
    }
    internal static Cell CellOf(Formation f, I.W w, int n)
    {
        var parts = new Cell[n];
        Parallel.For(0, n, s => parts[s] = Fight(f, w, s));
        var all = new Cell();
        foreach (var x in parts) all.Add(x);
        return all;
    }
    static string FirstOf(Cell c) => c.First.Count == 0 ? "—" : string.Join("、", c.First.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {Pct(kv.Value, c.N)}%"));

    // ---------------------------------------------------------------------------------
    // §3 Phase 0（段0 入りの規定の上で HB-r）
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vr = V("hbr");
        var boards = new[] { (GareRow, Row(GareRow)), ($"B（{KeyB}）", ByKey(KeyB)) };
        var waves = new[] { I.Boss, I.Guard, I.Bat };
        const int TT = 8;
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 段0 入りの規定（ガレ ＝ GR-r）の上で HB-r のヒビ × {boards.Length} 台 × ボス ／ 近衛 ／ 大隊・seed 0..{P0Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("## 1. ヒビが受けたダメージ（1戦あたり・範囲 ＝ 薙ぎ ／ 貫き ／ 全体・単体・ほか ＝ 型なし（刻みなど））とターンごとの累計・倒れた T");
        Console.WriteLine();
        foreach (var (name, f0) in boards)
            foreach (var w in waves)
            {
                var f = Apply(f0, vr);
                long aoe = 0, single = 0, other = 0, died = 0, diedT = 0; var cum = new long[TT + 1]; var alive = new long[TT + 1];
                var lk = new object();
                Parallel.For(0, P0Seeds, s =>
                {
                    var (r, p, _) = I.Raw(f, w, s, verbose: true);
                    var h = p.First(u => u.Def.Id == "hibi");
                    long a = 0, si = 0, o = 0; var c = new long[TT + 1];
                    foreach (var x in r.Events)
                    {
                        if (x.Kind != BattleEventKind.Damage || x.TargetId != h.InstanceId) continue;
                        if (x.Pattern is AttackPattern.Single) si += x.Amount; else if (x.Pattern is null) o += x.Amount; else a += x.Amount;
                        if (x.Turn >= 1 && x.Turn <= TT) c[x.Turn] += x.Amount;
                    }
                    lock (lk)
                    {
                        aoe += a; single += si; other += o;
                        if (h.LastDeathTurn > 0) { died++; diedT += h.LastDeathTurn; }
                        long run = 0;
                        for (int t = 1; t <= TT; t++) { run += c[t]; if (h.LastDeathTurn == 0 || t <= h.LastDeathTurn) { cum[t] += run; alive[t]++; } }
                    }
                });
                Console.WriteLine($"- **{name} × {w.Name}**: 範囲 {Per(aoe, P0Seeds)} ／ 単体 {Per(single, P0Seeds)} ／ ほか {Per(other, P0Seeds)}・倒れた {Pct(died, P0Seeds)}%（T {Per(diedT, died)}）・累計（立っていた戦の平均）"
                    + string.Join(" ／ ", Enumerable.Range(1, TT).Select(t => $"T{t} {Per(cum[t], alive[t])}")));
            }
        Console.WriteLine();
        Console.WriteLine("## 2. 脆弱なしで、ヒビがボス戦の T5 に立っているのに要る HP（机上）");
        Console.WriteLine();
        Console.WriteLine("勇者の全体攻撃の1発の名目（攻 12 ＋ 11 × T・倍率なし・脆弱なし）。勇者は速 14 でヒビ（速 3）より先に動くので、T5 の頭に立つ（T4 の手番まで配る）には T1〜T4 の4発、T5 の手番に立つには T1〜T5 の5発を受けて残る HP が要る。"
            + "ボスの戦に単体の一撃は無い（勇者1体・全体攻撃）。§1 の実測の累計が名目より小さいのは、ドルガ（巨躯）／ ゴルムの肩代わりがヒビの分を引き受けるため（B の「ほか」は肩代わりの中継）——机上は肩代わりの無い台に合わせて名目で引く。");
        Console.WriteLine();
        Console.WriteLine("| T | 1発の名目 | 累計 | 脆弱ありの累計（× 1.5） |");
        Console.WriteLine("|--:|--:|--:|--:|");
        int sum = 0;
        for (int t = 1; t <= 6; t++) { sum += 12 + 11 * t; Console.WriteLine($"| {t} | {12 + 11 * t} | {sum} | {sum * 3 / 2} |"); }
        Console.WriteLine();
        Console.WriteLine($"→ 版の HP ＝ **{UnitCatalog.HibiSturdyHp}**（T1〜T4 の4発 ＝ 23 ＋ 34 ＋ 45 ＋ 56 ＝ 158 を受けて T5 の頭に立つ・開戦の配り ＋ T1〜T4 の手番で 5 回配る）。");
        Console.WriteLine();
        Console.WriteLine("## 3. 開戦に配ると、勇者の T1 の全体攻撃を何本の欠片で受けられるか");
        Console.WriteLine();
        foreach (var (name, f0) in boards)
            foreach (var v in new[] { vr, V("hbs") })
            {
                var f = Apply(f0, v);
                long rets = 0, giftBefore = 0, n = 0;
                for (int s = 0; s < P0Seeds; s++)
                {
                    var (r, p, e) = I.Raw(f, I.Boss, s, verbose: true);
                    var boss = e.First(u => u.HasTrait(TraitId.BossRise11));
                    int firstAtk = r.Events.ToList().FindIndex(x => x.Kind == BattleEventKind.Attack && x.ActorId == boss.InstanceId);
                    var ev = r.Events.ToList();
                    int serialAt = -1;
                    for (int k = firstAtk; k >= 0 && k < ev.Count; k++)
                    {
                        var x = ev[k];
                        if (k > firstAtk && x.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart) break;
                        if (x.Kind == BattleEventKind.Shard && x.Text == ShardLabels.Return && x.TargetId == boss.InstanceId) { rets++; serialAt = x.Slot; }
                    }
                    giftBefore += ev.Take(Math.Max(0, firstAtk)).Count(x => x.Kind == BattleEventKind.Shard && x.Text == ShardLabels.Gift);
                    n++;
                }
                Console.WriteLine($"- {name} × {v.Name}: 勇者の最初の攻撃の前に配られた欠片 {Per2(giftBefore, n)} 体 ／ その攻撃で返った欠片 {Per2(rets, n)} 本");
            }
        Console.WriteLine();
        Console.WriteLine("ヒビの手番は勇者より後（速 3 ＜ 14）なので、HB-r では T1 の全体攻撃は欠片の無い盤で受ける。開戦の配り（HB-s ／ HB-f）なら T1 から欠片 4 本で受ける。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
