using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;

// =====================================================================================
// markheal295 —— 第295期「ソラ SR-b（見切り）の規定化 ＋ ヒサの『あいつを狙え！』（HK-a ／ HK-b）」。
// 指示書は design/PHASE295_MARK_HEAL_SPEC.md ／ 報告は design/PHASE295_MARK_HEAL.md。
//
//     dotnet run --project BattleSim -c Release 0 markheal295 p0          # Phase 0（§4）: まとまりの数・回復先・ザンの倒れ方・見込みの回復量・手番の外の攻撃の多い駒
//     dotnet run --project BattleSim -c Release 0 markheal295 compare     # `compare` 64 行 × ヒサの版（その駒の在席行だけ・30T 上限の負けも）
//     dotnet run --project BattleSim -c Release 0 markheal295 boards      # 代表台 5 台 × ボス ／ 近衛 ／ 大隊 × ヒサの版（seed 0..199・verbose）と対照（R383）
//     dotnet run --project BattleSim -c Release 0 markheal295 gridboss    # ボスの格子（固定枠 ミサ ＋ ザン ＋ ソラ ＋ ヒサ・探索枠1 × 席 120 × 規定 ／ HK-a ／ HK-b）
//     dotnet run --project BattleSim -c Release 0 markheal295 grid <guard|bat> <hka|hkb>   # 精鋭の格子（固定枠 ミサ ＋ ヒサ ＋ ザン・探索枠2・規定と版）
//     dotnet run --project BattleSim -c Release 0 markheal295 memo <boss|guard|bat> <seed> <insight|rally> [版]   # Codex 向けの並び（循環の台）
//     dotnet run --project BattleSim -c Release 0 markheal295 check       # 自己検査
//
// 規定のソラは段0 の後（SR-b・見切り）のまま組む（固定しない）。版の名前（ASCII）: hk0（規定）／ hka ／ hkb。
// =====================================================================================
static class MarkHeal295Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(); return;
            case "compare": CompareAll(); return;
            case "boards": BoardsAll(); return;
            case "gridboss": GridBoss(); return;
            case "grid": GridElite(A(3, "guard"), A(4, "hka")); return;
            case "memo": Memo(A(3, "boss"), int.Parse(A(4, "0")), A(5, "insight"), A(6, "hk0")); return;
            case "check": Check(); return;
            default: Console.WriteLine("markheal295: モードは p0 / compare / boards / gridboss / grid / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・波・台（測る前に固定）
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef To);
    internal static readonly Ver[] Vers =
    {
        new("規定", "hk0", UnitCatalog.HisaHK0), new("HK-a", "hka", UnitCatalog.HisaHKa), new("HK-b", "hkb", UnitCatalog.HisaHKb),
    };
    static Ver VerOf(string n) => Vers.First(v => v.Ascii == n || v.Name == n);
    static Formation Apply(Formation f, Ver v) => ReferenceEquals(v.To, UnitCatalog.HisaHK0) ? f : FvSwap(f, UnitCatalog.HisaHK0, v.To);
    static bool Has(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));
    static S287.Wave WaveOf(string n) => S287.Waves.First(w => w.Name == (n switch { "boss" => "ボス", "guard" => "近衛", "bat" => "大隊", _ => n }));
    const int Seeds = 200, CutSeeds = 20;

    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static string OrderName(UnitDef[] o) => string.Join("・", o.Select(Short));
    static string Seats(Formation f) => string.Join("・", Enumerable.Range(0, 5).Select(i => f[i] is { } d ? Short(d) : "—"));
    static Formation Playtest(string n) => Pin296(Presets.Playtest.First(r => r.Name == n).F);
    /// <summary>第296期: 規定のヒサが HK-b になった——この器具の「規定」は第295期の規定（`HisaHK0`）に固定する（台・`compare` の行とも）。</summary>
    internal static Formation Pin296(Formation f)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Hisa)) g[slot] = UnitCatalog.HisaHK0;
        foreach (var (slot, d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Doha)) g[slot] = UnitCatalog.DohaD0;   // 第298期
        foreach (var (slot, d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Tome)) g[slot] = UnitCatalog.TomeMb;   // 第299期: ミサ ／ ザンも旧の規定に
        foreach (var (slot, d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Zan)) g[slot] = UnitCatalog.ZanZN0;
        return g;
    }
    static (string Name, Formation F)[] CompareBuilds() => Common.CompareBuilds().Select(r => (r.Name, Pin296(r.F))).ToArray();
    static Formation Cycle => FvSwap(Playtest("試遊・標 ボス台"), UnitCatalog.Ban, UnitCatalog.Sora);

    /// <summary>代表台（§5-2）。循環の台 ＝ 試遊・標 ボス台の バン → ソラ（ザン・ゴルム・ミサ・ソラ・ヒサ）。</summary>
    internal static (string Name, Formation F, bool Ref)[] Boards() => new (string, Formation, bool)[]
    {
        ("循環の台", Cycle, false),
        ("循環 ゴルム→ツギ（参考・ヒーラー同居）", FvSwap(Cycle, UnitCatalog.Golm, UnitCatalog.Tsugi), true),
        ("循環 ゴルム→ドルガ", FvSwap(Cycle, UnitCatalog.Golm, UnitCatalog.Dolga), false),
        ("標 ボス台", Playtest("試遊・標 ボス台"), false),
        ("標経済", CompareBuilds().First(r => r.Name == "標経済 (ヒサ×ザン×ミサ)").F, false),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Deep
    {
        public long N, Wins, WinT, LoseT, Caps, FirstN, FirstT, HeroN, HeroTurns, HeroOver;
        public long RDied, RDeathT, ZDied, ZDeathT, HDied, HDeathT;
        public long InsightTurns, Insights, InsightCut;
        public long RallyFires, RallyHeals, RallyHealed, RallyOver, RallyNone;
        public long[] RallyTo = new long[5];
        public long ZanTurn, ZanOut, MisaTurn, MisaOut, SoraTurn, SoraOut, OtherTurn, OtherOut;
        public long Vendettas, ZanSelf, ZanHealed;
        public long[] ZanCause = new long[4];   // 1 敵の全体 ／ 2 敵の単体 ／ 3 味方の刃（ミサの乱射ほか）／ 0 その他（放電・刻み）
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; FirstN += o.FirstN; FirstT += o.FirstT; HeroN += o.HeroN; HeroTurns += o.HeroTurns; HeroOver += o.HeroOver;
            RDied += o.RDied; RDeathT += o.RDeathT; ZDied += o.ZDied; ZDeathT += o.ZDeathT; HDied += o.HDied; HDeathT += o.HDeathT;
            InsightTurns += o.InsightTurns; Insights += o.Insights; InsightCut += o.InsightCut;
            RallyFires += o.RallyFires; RallyHeals += o.RallyHeals; RallyHealed += o.RallyHealed; RallyOver += o.RallyOver; RallyNone += o.RallyNone;
            for (int i = 0; i < 5; i++) RallyTo[i] += o.RallyTo[i];
            ZanTurn += o.ZanTurn; ZanOut += o.ZanOut; MisaTurn += o.MisaTurn; MisaOut += o.MisaOut; SoraTurn += o.SoraTurn; SoraOut += o.SoraOut; OtherTurn += o.OtherTurn; OtherOut += o.OtherOut;
            Vendettas += o.Vendettas; ZanSelf += o.ZanSelf; ZanHealed += o.ZanHealed;
            for (int i = 0; i < 4; i++) ZanCause[i] += o.ZanCause[i];
        }
    }

    internal static Deep FightDeep(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1 };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; } else { d.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) d.Caps = 1; }
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        UnitState? hi = p.FirstOrDefault(u => u.Def.Id == "hisa"), so = p.FirstOrDefault(u => u.Def.Id == "sora"), za = p.FirstOrDefault(u => u.Def.Id == "zan");
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!p.Any(u => u.Def.Id == id)) continue;
            switch (id)
            {
                case "zan": d.ZanTurn += t.BundleTurn; d.ZanOut += t.BundleOut; break;
                case "tome": d.MisaTurn += t.BundleTurn; d.MisaOut += t.BundleOut; break;
                case "sora": d.SoraTurn += t.BundleTurn; d.SoraOut += t.BundleOut; break;
                default: d.OtherTurn += t.BundleTurn; d.OtherOut += t.BundleOut; break;
            }
            if (id == "hisa")
            {
                d.RallyFires = t.RallyFires; d.RallyHeals = t.RallyHeals; d.RallyHealed = t.RallyHealed; d.RallyOver = t.RallyOver; d.RallyNone = t.RallyNone;
                for (int i = 0; i < 5; i++) d.RallyTo[i] = t.RallyTo?[i] ?? 0;
            }
        }
        UnitState? hero = e.FirstOrDefault(u => u.Def == EnemyCatalog.BossRegular);
        var dmgT = new long[32]; var healT = new long[32]; var insT = new HashSet<int>();
        (AttackPattern? Pat, int Kind) lastZ = default;   // Kind: 1 敵 ／ 2 味方 ／ 0 出どころ無し
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Insight: d.Insights++; d.InsightCut += ev.Amount; insT.Add(ev.Turn); break;
                case BattleEventKind.MarkRally when za is not null && ev.TargetId == za.InstanceId: d.ZanHealed += ev.Amount; break;
                case BattleEventKind.Damage when za is not null && ev.ActorId == za.InstanceId && ev.TargetId is int vt && !mine.Contains(vt) && ev.Reaction: d.Vendettas++; break;
                case BattleEventKind.Damage when za is not null && ev.TargetId == za.InstanceId:
                    if (ev.ActorId == za.InstanceId) d.ZanSelf += ev.Amount;
                    lastZ = (ev.Pattern, ev.ActorId is not int ai ? 0 : mine.Contains(ai) ? 2 : 1);
                    break;
                case BattleEventKind.Damage when hero is not null && ev.TargetId == hero.InstanceId: dmgT[Math.Min(31, ev.Turn)] += ev.Amount; break;
                case BattleEventKind.Heal when hero is not null && ev.TargetId == hero.InstanceId: healT[Math.Min(31, ev.Turn)] += ev.Amount; break;
                case BattleEventKind.Death when ev.TargetId is int t && mine.Contains(t):
                    if (d.FirstN == 0) { d.FirstN = 1; d.FirstT = ev.Turn; }
                    if (so is not null && t == so.InstanceId && d.RDied == 0) { d.RDied = 1; d.RDeathT = ev.Turn; }
                    if (hi is not null && t == hi.InstanceId && d.HDied == 0) { d.HDied = 1; d.HDeathT = ev.Turn; }
                    if (za is not null && t == za.InstanceId && d.ZDied == 0)
                    {
                        d.ZDied = 1; d.ZDeathT = ev.Turn;
                        d.ZanCause[lastZ.Kind == 2 ? 3 : lastZ.Kind == 0 ? 0 : lastZ.Pat == AttackPattern.All ? 1 : 2]++;
                    }
                    break;
            }
        }
        d.InsightTurns = insT.Count;
        if (hero is not null)
        {
            d.HeroN = 1;
            for (int t = 1; t <= Math.Min(31, r.Turns); t++) { d.HeroTurns++; if (dmgT[t] > healT[t]) d.HeroOver++; }
        }
        return d;
    }

    internal static Deep MeasureDeep(Formation f, S287.Wave w, int n)
    {
        var parts = new Deep[n];
        Parallel.For(0, n, i => parts[i] = FightDeep(f, w, i));
        var all = new Deep();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static int Wins(Formation f, S287.Wave w, int n, out long winT)
    {
        int wins = 0; winT = 0;
        for (int s = 0; s < n; s++) { var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false); if (r.PlayerWon) { wins++; winT += r.Turns; } }
        return wins;
    }
    static int WinsPar(Formation f, S287.Wave w, int n)
    {
        int wins = 0;
        Parallel.For(0, n, s => { if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
        return wins;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F0");
    static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1") + "%";
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";

    // ---------------------------------------------------------------------------------
    // Phase 0（§4）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第295期 Phase 0 —— あいつを狙え！の下調べ（段0 の後の規定・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("## §4 の 1 攻撃のひとまとまりの口");
        Console.WriteLine();
        Console.WriteLine("| 口 | 包み | まとまり | 備考 |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine("| 手番（攻撃・術・羽・雷・板ほか） | `BattleContext.TakeTurnFramed`（`TakeTurn` の枠） | 1つ（主 ＝ 手番の駒） | ミサの羽が何枚でも・豆鉄砲の連撃でも・再行動の手番は別の `TakeTurn` で別の1つ |");
        Console.WriteLine("| 反撃・仇討ち（ザンの仇指し）・棘・斬り返し ほか | `BattleContext.Reaction` | 1つ（主 ＝ 枠の中の最初の出どころ） | ザンの返り血（自傷 3）は同じ枠の中なので数を増やさない |");
        Console.WriteLine("| 割り込み（シガの割り込みの鞭・応急処置・暴発・追い撃ち ほか） | `BattleContext.Interrupt` | 1つ（同上） | |");
        Console.WriteLine("| 巻き込み・貫きの2体目・放電・刻み・呪いの共有 | 開いている枠の中のダメージ | 増やさない（標の敵に当たったかだけ） | 枠の外の刻み（ターンの頭の毒・燃焼）はどの枠にも入らない |");
        Console.WriteLine();
        Console.WriteLine("**3口とも入口と出口が1か所にある**（`TakeTurnFramed` の try／finally・`Reaction` ／ `Interrupt` の try／finally）——枠を積む列（`_bundles`）で「まとまりの始まりと終わり」を取れる。入れ子（勇者の手番の中のザンの仇討ち）は内側の枠にだけ数える。");
        Console.WriteLine();

        Console.WriteLine("## §4 の 2 ／ 3 ／ 5 標の敵に当たったまとまり・回復先・見込みの回復量（HK-a ／ HK-b を回して数える）");
        Console.WriteLine();
        Console.WriteLine("まとまり ＝ 標の敵に当たったまとまり（1ターンあたり・手番 ／ 手番の外）。回復先（HK-a）＝ 癒えた量の内訳。被ダメ ＝ 勇者の全体攻撃1回で味方が受けた量の合計（規定）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 決着T | ザン 手番 ／ 外 | ミサ | ソラ | ほか | 仇討ち／T | 回復 回 ／ 量（1T） | 回復先 ソラ ／ 矢面 ／ ザン ／ 攻撃の主 ／ ほか | 回復先なし | 溢れ |");
        Console.WriteLine("|---|---|---|--:|---|---|---|---|--:|---|---|--:|--:|");
        foreach (var (name, f, _) in Boards())
            foreach (var w in S287.Waves)
                foreach (var v in Vers.Skip(1))
                {
                    var a = MeasureDeep(Apply(f, v), w, Seeds);
                    long T = a.WinT + a.LoseT;
                    long healed = a.RallyTo.Sum();
                    Console.WriteLine($"| {name} | {w.Name} | {v.Name} | {Per1(T, a.N)} | {Per(a.ZanTurn, T)} ／ {Per(a.ZanOut, T)} | {Per(a.MisaTurn, T)} ／ {Per(a.MisaOut, T)} | {Per(a.SoraTurn, T)} ／ {Per(a.SoraOut, T)} | {Per(a.OtherTurn, T)} ／ {Per(a.OtherOut, T)} | {Per(a.Vendettas, T)} | "
                        + $"{Per(a.RallyHeals, T)} ／ {Per1(a.RallyHealed, T)} | {Pct(a.RallyTo[0], healed)} ／ {Pct(a.RallyTo[1], healed)} ／ {Pct(a.RallyTo[2], healed)} ／ {Pct(a.RallyTo[3], healed)} ／ {Pct(a.RallyTo[4], healed)} | {Per(a.RallyNone, a.N)} | {Pct(a.RallyOver, a.RallyHealed + a.RallyOver)} |");
                }
        Console.WriteLine();
        // 勇者の全体攻撃1回の被ダメ（規定・循環の台）
        {
            long dmg = 0, hits = 0;
            for (int s = 0; s < Seeds; s++)
            {
                var p = BattleEngine.Materialize(Cycle, BattleContext.PlayerTeam);
                var e = S287.Waves[0].Make();
                var r = BattleEngine.Run(p, e, s, verbose: true);
                var hero = e.First(u => u.Def == EnemyCatalog.BossRegular);
                var mine = p.Select(u => u.InstanceId).ToHashSet();
                hits += r.Events.Count(ev => ev.Kind == BattleEventKind.Attack && ev.ActorId == hero.InstanceId);
                dmg += r.Events.Where(ev => ev.Kind == BattleEventKind.Damage && ev.ActorId == hero.InstanceId && ev.TargetId is int t && mine.Contains(t)).Sum(ev => (long)ev.Amount);
            }
            Console.WriteLine($"循環の台 × ボス（規定）: 勇者の攻撃1回で味方が受ける量の合計 **{Per1(dmg, hits)}**（{hits} 回の平均・見切りの後）。");
        }
        Console.WriteLine();

        Console.WriteLine("## §4 の 3 標を持つ味方（engine の順）");
        Console.WriteLine();
        Console.WriteLine("ソラの逸らし（`DivertTrait.OnTurnStart`）はターンの頭に味方の標をすべて剥がし、自分に標を付ける（**表示の `StatusGain` を出さない**——台本では追えないので engine の回復先の計数で見る）。");
        Console.WriteLine("その後、勇者（速さ 14）→ ザン（5）・ミサ ほかの順に動き、ヒサ（速さ 10）が手番で隣の最も元気な味方を指差す（このターンの残りの間・ターンの頭でソラに剥がされる）。");
        Console.WriteLine("**勇者の手番の時点で標を持つ味方はソラだけ**、ヒサの手番の後はソラとヒサの矢面の相手の2体。HK-a の回復先の内訳は §4 の 2 の表の「回復先」（ソラ ／ 矢面）と「回復先なし」（標を持つ味方が全員満タン ／ いない）。");
        Console.WriteLine();

        Console.WriteLine("## §4 の 4 ザンの倒れる手番と原因（循環の台・ソラ SR-b・ボス）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 | ザン 倒れたT | 原因 敵の全体 ／ 敵の単体 ／ 味方の刃 ／ 出どころ無し | 仇討ち（1戦） | 自傷（1戦） | ザンへの回復（1戦） | ソラ 倒れたT | ヒサ 倒れたT |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|--:|--:|--:|");
        foreach (var v in Vers)
        {
            var a = MeasureDeep(Apply(Cycle, v), S287.Waves[0], Seeds);
            Console.WriteLine($"| {v.Name} | {F1(a.Win)} | {Dt(a.ZDied, a.ZDeathT, a.N)} | {a.ZanCause[1]} ／ {a.ZanCause[2]} ／ {a.ZanCause[3]} ／ {a.ZanCause[0]} | {Per1(a.Vendettas, a.N)} | {Per1(a.ZanSelf, a.N)} | {Per1(a.ZanHealed, a.N)} | {Dt(a.RDied, a.RDeathT, a.N)} | {Dt(a.HDied, a.HDeathT, a.N)} |");
        }
        Console.WriteLine();

        Console.WriteLine("## §4 の 6 手番の外の攻撃が多い駒（HK-a・`compare` のヒサの行 × 第2〜5波 × seed 0..39）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 標の敵に当たったまとまり 手番 ／ 外（1戦） |");
        Console.WriteLine("|---|---|---|");
        foreach (var (n, f) in CompareBuilds().Where(r => Has(r.F, UnitCatalog.HisaHK0)))
        {
            var acc = new Dictionary<string, (long T, long O)>();
            long N = 0;
            for (int wi = 1; wi < EnemyCatalog.Stages.Count; wi++)
                for (int s = 0; s < 40; s++)
                {
                    var r = BattleEngine.Run(Apply(f, Vers[1]), EnemyCatalog.Stages[wi].Enemy, s, verbose: false);
                    N++;
                    foreach (var (id, t) in r.TallyByUnit)
                        if (f.Occupied().Any(o => o.Def.Id == id)) { var x = acc.GetValueOrDefault(id); acc[id] = (x.T + t.BundleTurn, x.O + t.BundleOut); }
                }
            foreach (var (id, x) in acc.Where(kv => kv.Value.T + kv.Value.O > 0).OrderByDescending(kv => kv.Value.O))
                Console.WriteLine($"| {n} | {Short(UnitCatalog.Everyone.First(d => d.Id == id))} | {Per(x.T, N)} ／ {Per(x.O, N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` 64 行 × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll()
    {
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        (double[,] G, int[] Caps) Grid(Ver v)
        {
            var g = new double[rows.Length, nw]; var caps = new int[rows.Length];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, v);
                int wins = 0, cap = 0;
                for (int s = 0; s < Seeds; s++) { var r = BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false); if (r.PlayerWon) wins++; else if (wi > 0 && r.Turns >= BattleEngine.MaxTurns) cap++; }
                g[ri, wi] = 100.0 * wins / Seeds;
                if (cap > 0) Interlocked.Add(ref caps[ri], cap);
            });
            return (g, caps);
        }
        var res = Vers.Select(Grid).ToArray();
        var prim = Baseline.PrimaryRows.ToHashSet();
        Console.WriteLine("# 第295期 `compare` 64 行 × ヒサの版（seed 0..199・ヒサの在席行だけ差し替える・基準は段0 の後の規定）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち | 30T 上限の負け | 主判定 |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|--:|---|");
        int moved = 0;
        for (int ri = 0; ri < rows.Length; ri++)
        {
            if (!Has(rows[ri].F, UnitCatalog.HisaHK0))
            {
                for (int v = 1; v < Vers.Length; v++) for (int w = 0; w < nw; w++) if (res[v].G[ri, w] != res[0].G[ri, w]) moved++;
                continue;
            }
            double m0 = Enumerable.Range(1, nw - 1).Average(w => res[0].G[ri, w]);
            for (int v = 0; v < Vers.Length; v++)
            {
                var g = res[v].G;
                double m = Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
                double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - res[0].G[ri, w]);
                Console.WriteLine($"| {rows[ri].Name} | {Vers[v].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w]))) + $" | {F1(m)} | {(v == 0 ? "" : (m - m0).ToString("+0.0;-0.0;0.0"))} | {(v == 0 ? "" : drop.ToString("+0.0;-0.0;0.0"))} | {res[v].Caps[ri]} | {(v == 0 && prim.Contains(rows[ri].Name) ? "○" : "")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"ヒサのいない行で動いたセル: **{moved}**（0 であること）");
    }

    // ---------------------------------------------------------------------------------
    // 代表台
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new List<(string B, bool Ref, S287.Wave W, Ver V, Formation F, Deep D)>();
        foreach (var w in S287.Waves) foreach (var (n, f, rf) in Boards()) foreach (var v in Vers) cells.Add((n, rf, w, v, Apply(f, v), null!));
        for (int i = 0; i < cells.Count; i++) cells[i] = cells[i] with { D = MeasureDeep(cells[i].F, cells[i].W, Seeds) };
        Console.WriteLine("# 第295期 代表台 × 波 × ヒサの版（規定 ／ HK-a ／ HK-b・ソラは規定 SR-b・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (n, f, rf) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）{(rf ? "——**参考**（HK ＋ ヒーラー・採否の線に使わない）" : "")}");
        Console.WriteLine();
        Console.WriteLine("寿命 ＝ 味方が初めて倒れたT（倒れた戦だけの平均・括弧は倒れた戦の割合）。見切り ＝ 見切りが効いたターン（1戦）。勇者の上回り ＝ 味方の与ダメが勇者の回復を上回ったターン ÷ ターン。");
        foreach (var w in S287.Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 差 | 倒しT | 負けT（30T 上限） | 寿命 | ソラ ／ ザン ／ ヒサ 倒れたT | 見切り T ／ 回 ／ 量 | 回復 回 ／ 癒えた ／ 溢れ（1戦） | 回復先 ソラ ／ 矢面 ／ ザン ／ 攻撃の主 ／ ほか | 仇討ち ／ 自傷 ／ ザンへの回復 | 勇者の上回り |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|---|---|---|---|--:|");
            foreach (var c in cells.Where(c => c.W == w))
            {
                var a = c.D;
                var b0 = cells.First(x => x.W == w && x.B == c.B).D;
                long healed = a.RallyTo.Sum();
                Console.WriteLine($"| {c.B} | {c.V.Name} | {F1(a.Win)} | {(c.V == Vers[0] ? "" : (a.Win - b0.Win).ToString("+0.0;-0.0;0.0"))} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（{Pct(a.Caps, a.N)}）" : "")} | "
                    + $"{Dt(a.FirstN, a.FirstT, a.N)} | {Dt(a.RDied, a.RDeathT, a.N)} ／ {Dt(a.ZDied, a.ZDeathT, a.N)} ／ {Dt(a.HDied, a.HDeathT, a.N)} | {Per1(a.InsightTurns, a.N)} ／ {Per1(a.Insights, a.N)} ／ {Per0(a.InsightCut, a.N)} | "
                    + $"{Per1(a.RallyHeals, a.N)} ／ {Per0(a.RallyHealed, a.N)} ／ {Per0(a.RallyOver, a.N)} | {Pct(a.RallyTo[0], healed)} ／ {Pct(a.RallyTo[1], healed)} ／ {Pct(a.RallyTo[2], healed)} ／ {Pct(a.RallyTo[3], healed)} ／ {Pct(a.RallyTo[4], healed)} | "
                    + $"{Per1(a.Vendettas, a.N)} ／ {Per1(a.ZanSelf, a.N)} ／ {Per1(a.ZanHealed, a.N)} | {Pct(a.HeroOver, a.HeroTurns)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 対照（R383）: 勝率 50% 以上のセルで、台の駒それぞれ → ドルガ（「ヒサ → ドルガ」が HK の寄与）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | 版 | 勝率 | 前1 → | 前3 → | 中央 → | 後1 → | 後3 → |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var c in cells.Where(c => c.D.Win >= 50))
        {
            string C(int slot) => c.F[slot] is { } d && !ReferenceEquals(d, UnitCatalog.Dolga) ? $"{Short(d)} {F1(100.0 * WinsPar(FvSwap(c.F, d, UnitCatalog.Dolga), c.W, Seeds) / Seeds)}" : "—";
            Console.WriteLine($"| {c.W.Name} | {c.B} | {c.V.Name} | {F1(c.D.Win)} | {C(0)} | {C(1)} | {C(2)} | {C(3)} | {C(4)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 格子
    // ---------------------------------------------------------------------------------
    static readonly UnitDef[] Guards = { UnitCatalog.Golm, UnitCatalog.Gald, UnitCatalog.Doha, UnitCatalog.Ban, UnitCatalog.Kubi, UnitCatalog.Kado, UnitCatalog.Uke, UnitCatalog.Sekki };
    static string TypeOf(UnitDef d) => B283.HealPool295.Contains(d) ? "ヒーラー" : Guards.Contains(d) ? "守り" : d.Id is "gan" or "veru" or "hibi" or "sasa" ? "支え" : "火力・その他";

    sealed record GridRes(UnitDef[] Order, int[] Wins, long[] WinT, int[][] Ctl);

    static void GridCore(string title, S287.Wave w, UnitDef[] fixedU, UnitDef[] pool, int k)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var heal = B283.HealPool295.ToHashSet();
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedU.Concat(t).ToArray()));
        var res = new GridRes?[boards.Count];
        int cut = 0;
        Parallel.For(0, boards.Count, i =>
        {
            var f0 = B283.Seat(boards[i]);
            var fs = Vers.Select(v => Apply(f0, v)).ToArray();
            if (!fs.Any(f => Wins(f, w, CutSeeds, out _) >= 5)) return;
            Interlocked.Increment(ref cut);
            var wins = new int[Vers.Length]; var wt = new long[Vers.Length]; var ctl = new int[Vers.Length][];
            for (int v = 0; v < Vers.Length; v++)
            {
                wins[v] = Wins(fs[v], w, Seeds, out wt[v]);
                ctl[v] = fixedU.Select(_ => -1).ToArray();
                if (wins[v] * 2 < Seeds) continue;
                for (int c = 0; c < fixedU.Length; c++)
                {
                    var occ = fs[v].Occupied().First(o => o.Def.Id == fixedU[c].Id).Def;
                    ctl[v][c] = Wins(FvSwap(fs[v], occ, UnitCatalog.Dolga), w, Seeds, out _);
                }
            }
            res[i] = new GridRes(boards[i], wins, wt, ctl);
        });
        var all = res.Where(r => r is not null).Select(r => r!).ToList();
        bool Half(GridRes r, int v) => r.Wins[v] * 2 >= Seeds;
        bool Reach(GridRes r, int v) => Half(r, v) && r.Ctl[v].Any(c => c >= 0 && c * 2 <= r.Wins[v]);
        bool Ref(GridRes r, int v) => v > 0 && r.Order.Any(heal.Contains);   // HK ＋ ヒーラー（参考・§3-3）
        string Key(UnitDef[] o) => string.Join(",", o.Where(d => !fixedU.Any(x => x.Id == d.Id)).Select(d => d.Id).OrderBy(s => s, StringComparer.Ordinal));
        Console.WriteLine($"# 第295期 格子 × {w.Name} × {title}");
        Console.WriteLine();
        Console.WriteLine($"探索枠{k}（候補 {pool.Length} 枚: {OrderName(pool)}）＝ {lu.Count} 組 × 席 120 ＝ {boards.Count:N0} 台 × 版 {Vers.Length}（{string.Join(" ／ ", Vers.Select(v => v.Name))}）。足切り seed 0..19（どれかの版で 5 勝・{cut:N0} 台）→ seed 0..199 → 勝率 50% 以上で固定枠の駒それぞれ → ドルガ。");
        Console.WriteLine("**ヒサの HK 版はヒーラーに数える**（§3-3）——HK 版 ＋ ツギ ／ リリの台は「参考」の段に分け、本段の数に入れない。規定のヒサ ＋ ヒーラー1枚は本段に入れる。");
        Console.WriteLine();
        Console.WriteLine("## 表1 段ごとの台数（組の数）");
        Console.WriteLine();
        Console.WriteLine("| 段 | " + string.Join(" | ", Vers.Select(v => v.Name)) + " | " + string.Join(" | ", Vers.Skip(1).Select(v => v.Name + "（参考: ＋ヒーラー）")) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, Vers.Length * 2 - 1).Select(_ => "--:|")));
        void Row(string label, Func<GridRes, int, bool> pred)
        {
            string Cell(int v, bool refRow) { var l = all.Where(r => (refRow ? Ref(r, v) : !Ref(r, v)) && pred(r, v)).ToList(); return $"{l.Count:N0}（{l.Select(r => Key(r.Order)).Distinct().Count()}）"; }
            Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, Vers.Length).Select(v => Cell(v, false))) + " | " + string.Join(" | ", Enumerable.Range(1, Vers.Length - 1).Select(v => Cell(v, true))) + " |");
        }
        Row("勝率 ≧ 50%", Half);
        Row("**届いた**", Reach);
        for (int c = 0; c < fixedU.Length; c++) { int cc = c; Row($"{Short(fixedU[c])}が要る", (r, v) => Half(r, v) && r.Ctl[v][cc] * 2 <= r.Wins[v]); }
        Row("勝率 ≧ 90%", (r, v) => r.Wins[v] * 10 >= Seeds * 9);
        Console.WriteLine();
        Console.WriteLine("## 表2 自由枠の型ごとの組の数（勝率 50% 以上の組・参考の段を除く）");
        Console.WriteLine();
        Console.WriteLine("| 型 | " + string.Join(" | ", Vers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "--:|")));
        foreach (var ty in new[] { "守り", "ヒーラー", "支え", "火力・その他" })
            Console.WriteLine($"| {ty} | " + string.Join(" | ", Enumerable.Range(0, Vers.Length).Select(v => all.Where(r => !Ref(r, v) && Half(r, v) && r.Order.Where(d => !fixedU.Any(x => x.Id == d.Id)).Any(d => TypeOf(d) == ty)).Select(r => Key(r.Order)).Distinct().Count().ToString())) + " |");
        Console.WriteLine();
        Console.WriteLine("## 表3 組ごとの最大勝率（自由枠・参考の段は印）");
        Console.WriteLine();
        Console.WriteLine("| 自由枠 | 型 | " + string.Join(" | ", Vers.Select(v => v.Name + " 最大")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "--:|")));
        foreach (var g in all.GroupBy(r => Key(r.Order)).OrderByDescending(g => g.Max(r => r.Wins.Max())))
        {
            var free = g.First().Order.Where(d => !fixedU.Any(x => x.Id == d.Id)).ToArray();
            if (g.Max(r => r.Wins.Max()) * 2 < Seeds) continue;
            Console.WriteLine($"| {OrderName(free)} | {string.Join("・", free.Select(TypeOf).Distinct())}{(free.Any(heal.Contains) ? "（HK と同居は参考）" : "")} | " + string.Join(" | ", Enumerable.Range(0, Vers.Length).Select(v => F1(100.0 * g.Max(r => r.Wins[v]) / Seeds))) + " |");
        }
        for (int v = 0; v < Vers.Length; v++)
        {
            Console.WriteLine();
            Console.WriteLine($"## 表4-{v + 1} 上位の台（{Vers[v].Name}・上位 10）");
            Console.WriteLine();
            Console.WriteLine($"| # | 席 | 勝率 | 倒しT | 届いた | {string.Join(" | ", fixedU.Select(d => Short(d) + " →"))} |");
            Console.WriteLine("|--:|---|--:|--:|---|" + string.Concat(fixedU.Select(_ => "--:|")));
            var top = all.Where(r => Half(r, v)).OrderByDescending(r => r.Wins[v]).ThenBy(r => (double)r.WinT[v] / Math.Max(1, r.Wins[v])).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal).Take(10).ToList();
            for (int i = 0; i < top.Count; i++)
                Console.WriteLine($"| {i + 1} | {OrderName(top[i].Order)}{(Ref(top[i], v) ? "（参考）" : "")} | {F1(100.0 * top[i].Wins[v] / Seeds)} | {Per1(top[i].WinT[v], top[i].Wins[v])} | {(Reach(top[i], v) ? "○" : "")} | {string.Join(" | ", top[i].Ctl[v].Select(c => c < 0 ? "—" : F1(100.0 * c / Seeds)))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>ボスの格子（§5-2）: 固定枠 ミサ ＋ ザン ＋ ソラ ＋ ヒサ・探索枠1 ＝ 第294期のソラの格子の候補（第283期の寿命側 ＋ ヒーラーからソラを除いた枠）からヒサを除いた枠。</summary>
    static UnitDef[] BossPool => B283.LifePool.Concat(B283.HealPool295.Where(h => !B283.LifePool.Contains(h))).Where(d => d.Id is not ("sora" or "hisa")).ToArray();

    static void GridBoss() => GridCore("固定枠 ミサ・ザン・ソラ・ヒサ（ヒサ: 規定 ／ HK-a ／ HK-b）", S287.Waves[0], new[] { UnitCatalog.TomeMb, UnitCatalog.ZanZN0, UnitCatalog.Sora, UnitCatalog.HisaHK0 }, BossPool, 1);   // 第299期: ミサ ／ ザンは旧の規定

    static void GridElite(string wave, string ver) =>
        GridCore($"固定枠 ミサ・ヒサ・ザン（第294期 §4-5 の作り・探索枠2）", WaveOf(wave), new[] { UnitCatalog.TomeMb, UnitCatalog.HisaHK0, UnitCatalog.ZanZN0 },   // 第299期: ミサ ／ ザンは旧の規定
            S287.Pool.Select(d => d.Id switch { "kata" => UnitCatalog.Kata, "kugu" => UnitCatalog.Kugu, _ => d }).Where(d => d.Id is not ("tome" or "hisa" or "zan")).Distinct().ToArray(), 2);

    // ---------------------------------------------------------------------------------
    // Codex 向けの並び
    // ---------------------------------------------------------------------------------
    static void Memo(string wave, int seed, string kind, string ver)
    {
        var w = EnemyCatalog.PlaytestStages[wave switch { "boss" => 0, "guard" => 1, "bat" => 2, _ => int.Parse(wave) }];
        var f = Apply(Cycle, VerOf(ver));
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.MaterializeEnemy(w.Enemy, w.Scale);
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        var key = kind == "insight" ? BattleEventKind.Insight : BattleEventKind.MarkRally;
        Console.WriteLine($"# markheal295 memo —— 循環の台（{Seats(f)}）× {w.Name} × seed {seed} × {VerOf(ver).Name} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}（`{key}` {r.Events.Count(x => x.Kind == key)} 件）");
        Console.WriteLine();
        Console.WriteLine("```");
        var evs = r.Events;
        int shown = 0, lastT = -1;
        for (int i = 0; i < evs.Count && shown < 14; i++)
        {
            if (evs[i].Kind != key) continue;
            shown++;
            foreach (int j in new[] { i - 1, i, i + 1, i + 2 }.Where(j => j >= 0 && j < evs.Count))
            {
                var x = evs[j];
                if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
                string other = string.Join("  ", new[] { x.PartnerId is int pa ? $"Partner={N(pa)}" : null, x.StatusRemaining is int sr ? $"rem={sr}" : null, x.Reaction ? "Reaction" : null,
                    x.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.MarkRally ? $"hp={x.HpAfter}" : null }.Where(q => q is not null));
                Console.WriteLine($"#{j,-4} {x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}  {other}".TrimEnd());
            }
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# markheal295 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        Expect("(a) 規定のソラ ＝ SR-b の札（旧の規定 `SoraSR0` ＋ `DivertPressure`）・文面は「見切り」・`SoraSR0` ／ `SoraSRb` は `All` ／ `Retired` の外",
            UnitCatalog.Sora.Traits.SequenceEqual(UnitCatalog.SoraSR0.Traits.Append(TraitId.DivertPressure)) && UnitCatalog.Sora.Traits.SequenceEqual(UnitCatalog.SoraSRb.Traits)
            && UnitCatalog.Sora.PlusText == UnitCatalog.SoraSR0.PlusText + "。指差した敵の動きは読める。深く指差された敵の一撃ほど、仲間への分まで受け流す"
            && !UnitCatalog.Sora.PlusText.Contains("手元が狂う") && !new[] { UnitCatalog.SoraSR0, UnitCatalog.SoraSRb }.Any(UnitCatalog.Everyone.Contains) && UnitCatalog.All.Contains(UnitCatalog.Sora));
        Expect("(b) HK-a ／ HK-b は規定のヒサの末尾に札を1枚足しただけ・文面・`All` ／ `Retired` の外",
            UnitCatalog.HisaHKa.Traits.SequenceEqual(UnitCatalog.HisaHK0.Traits.Append(TraitId.MarkRally)) && UnitCatalog.HisaHKb.Traits.SequenceEqual(UnitCatalog.HisaHK0.Traits.Append(TraitId.MarkRallyWide))
            && UnitCatalog.HisaHKa.PlusText.EndsWith("『あいつを狙え！ まだ倒れるな！』と叫んで、標を背負う味方を癒す")
            && UnitCatalog.HisaHKb.PlusText.EndsWith("『あいつを狙え！ まだ倒れるな！』と叫んで、攻撃した味方と最も傷ついた味方を癒す")
            && !new[] { UnitCatalog.HisaHKa, UnitCatalog.HisaHKb }.Any(UnitCatalog.Everyone.Contains)
            // 第296期: 規定のヒサが HK-b になった——`All` で札を持つのは規定のヒサ（`MarkRallyWide`）だけ（第295期の版は「保持者 0」を確かめていた）
            && UnitCatalog.All.Where(u => u.Traits.Any(t => t is TraitId.MarkRally or TraitId.MarkRallyWide)).SequenceEqual(new[] { UnitCatalog.Hisa })
            && !UnitCatalog.Hisa.Traits.Contains(TraitId.MarkRally));

        // (c)〜(h) 循環の台 × 3 波 × 40 seed の台本で、まとまりと回復を突き合わせる
        int multiPerBundle = 0, badAmount = 0, bDup = 0, afterHisaDeath = 0, misaTurnHeals = 0, misaFeatherTurns = 0, vendettaRallies = 0, vendettas = 0, rallies = 0;
        foreach (var v in new[] { Vers[1], Vers[2] })
            foreach (var w in S287.Waves)
                for (int s = 0; s < 40; s++)
                {
                    var p = BattleEngine.Materialize(Apply(Cycle, v), BattleContext.PlayerTeam);
                    var e = w.Make();
                    var r = BattleEngine.Run(p, e, s, verbose: true);
                    var mine = p.ToDictionary(u => u.InstanceId);
                    var hisa = p.First(u => u.Def.Id == "hisa");
                    int hisaDeath = int.MaxValue;
                    for (int q = 0; q < r.Events.Count; q++) if (r.Events[q].Kind == BattleEventKind.Death && r.Events[q].TargetId == hisa.InstanceId) { hisaDeath = q; break; }
                    var evs = r.Events;
                    for (int i = 0; i < evs.Count; i++)
                    {
                        var x = evs[i];
                        if (x.Kind == BattleEventKind.Damage && x.ActorId is int za && mine.TryGetValue(za, out var zu) && zu.Def.Id == "zan" && x.Reaction && x.TargetId is int vt && !mine.ContainsKey(vt)) vendettas++;
                        if (x.Kind != BattleEventKind.MarkRally) continue;
                        rallies++;
                        if (i > hisaDeath) afterHisaDeath++;
                        if (x.Amount < 0 || x.Slot <= 0) badAmount++;
                        // 量: 癒えた量 ≦ 層 × 6（溢れは捨てる）
                        if (x.Amount > x.Slot * BattleContext.RallyPerLayer) badAmount++;
                        // HK-b: 同じ叫びの2件が同じ相手
                        if (v == Vers[2] && i + 1 < evs.Count && evs[i + 1].Kind == BattleEventKind.MarkRally && evs[i + 1].TargetId == x.TargetId && evs[i + 1].Turn == x.Turn && evs[i + 1].PartnerId == x.PartnerId) bDup++;
                        if (x.PartnerId is int pa && mine.TryGetValue(pa, out var pu) && pu.Def.Id == "zan" && evs.Take(i).Reverse().TakeWhile(q => q.Kind != BattleEventKind.Attack || q.ActorId != pa).Any(q => q.Kind == BattleEventKind.Damage && q.ActorId == pa && q.Reaction)) vendettaRallies++;
                    }
                    // ミサの手番: 1手番に「ミサが主の回復」は高々 1 回（HK-a）／ 2 回（HK-b）
                    var misa = p.First(u => u.Def.Id == "tome");
                    foreach (var g in evs.Where(x => x.Kind == BattleEventKind.MarkRally && x.PartnerId == misa.InstanceId).GroupBy(x => x.Turn))
                    {
                        int feathers = evs.Count(x => x.Turn == g.Key && x.Kind == BattleEventKind.Feather && x.ActorId == misa.InstanceId && x.Text == FeatherLabels.Chase);
                        if (feathers > 1) misaFeatherTurns++;
                        if (g.Count() > (v == Vers[1] ? 1 : 2)) multiPerBundle++;
                        misaTurnHeals++;
                    }
                }
        Expect("(c) ミサの羽が何枚でも、ミサの手番の回復は1回（HK-b は2体まで）", misaTurnHeals > 0 && misaFeatherTurns > 0 && multiPerBundle == 0, $"ミサが主の回復のある手番 {misaTurnHeals}（うち羽 2 枚以上 {misaFeatherTurns}）・超え {multiPerBundle}");
        Expect("(d) ザンの仇討ちは1回ごとに1つのまとまり（仇討ちの後に回復が出る）", vendettas > 0 && vendettaRallies > 0, $"仇討ち {vendettas}・仇討ちが主の回復 {vendettaRallies}");
        Expect("(e) ヒサが倒れた後は回復しない ／ 癒えた量は 層 × 6 以下", afterHisaDeath == 0 && badAmount == 0, $"回復 {rallies}・倒れた後 {afterHisaDeath}・量の外れ {badAmount}");
        Expect("(f) HK-b: 攻撃した駒と最も傷ついた味方が同じなら1回（同じ叫びで同じ相手を2度癒さない）", bDup == 0, $"{bDup} 件");
        // (g) 標の敵に当たらないまとまりでは回復しない: 標の書き手のいない台（ヒサだけ・ソラもザンもいない）で HK-a の回復が 0
        {
            long heals = 0;
            var noMark = B283.Seat(new[] { UnitCatalog.Golm, UnitCatalog.Gald, UnitCatalog.Borg, UnitCatalog.Dolga, UnitCatalog.HisaHKa });
            foreach (var w in S287.Waves) for (int s = 0; s < 20; s++) heals += BattleEngine.Run(BattleEngine.Materialize(noMark, BattleContext.PlayerTeam), w.Make(), s, verbose: false).TallyByUnit["hisa"].RallyHeals;
            Expect("(g) 敵に標を書く駒がいない台（ゴルム・ガルド・ボルグ・ドルガ・ヒサ HK-a）では回復 0", heals == 0, $"{heals}");
        }
        // (h) 量 ＝ 最も深い層 × 6: エンジンの1回の回復を ApplyDamage の外から再現する——ログの叫びの直後の Heal の名目量（溢れ前）を見る代わりに、層 1 の戦（ミサがいない）で癒えた量 ≦ 6
        {
            int over = 0, n = 0;
            var f = B283.Seat(new[] { UnitCatalog.Zan, UnitCatalog.Golm, UnitCatalog.Dolga, UnitCatalog.Sora, UnitCatalog.HisaHKa });   // ミサがいない＝層 1
            foreach (var w in S287.Waves) for (int s = 0; s < 20; s++)
                foreach (var x in BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: true).Events.Where(x => x.Kind == BattleEventKind.MarkRally)) { n++; if (x.Slot != 1 || x.Amount > 6) over++; }
            Expect("(h) ミサのいない戦では層 1・1回の回復は 6 以下", n > 0 && over == 0, $"{n} 回・外れ {over}");
        }
        // (i) HK-a は標を持つ味方がいなければ何もしない（回復先なしの数が立つ ／ 回復先が標を持たない駒になっていない）
        {
            long none = 0, wrong = 0, heals = 0;
            foreach (var w in S287.Waves)
                for (int s = 0; s < 30; s++)
                {
                    var p = BattleEngine.Materialize(Apply(Playtest("試遊・標 ボス台"), Vers[1]), BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    none += r.TallyByUnit["hisa"].RallyNone;
                    var marked = new HashSet<int>();
                    foreach (var x in r.Events)
                    {
                        if (x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Marked && x.TargetId is int t) marked.Add(t);
                        if (x.Kind == BattleEventKind.MarkRally) { heals++; if (!marked.Contains(x.TargetId!.Value)) wrong++; }
                    }
                }
            Expect("(i) HK-a の回復先は標を付けられたことのある味方だけ（標の無い戦では回復先なし）", heals > 0 && wrong == 0, $"回復 {heals}・標の無い相手 {wrong}・回復先なし {none}");
        }
        // (j) 見切りの出来事: 量 ＝ 削った量・層・規定のソラがいない台では出ない
        {
            int ins = 0, insNo = 0;
            for (int s = 0; s < 20; s++)
            {
                ins += BattleEngine.Run(BattleEngine.Materialize(Cycle, BattleContext.PlayerTeam), S287.Waves[0].Make(), s, verbose: true).Events.Count(x => x.Kind == BattleEventKind.Insight && x.Amount > 0 && x.Slot >= 1);
                insNo += BattleEngine.Run(BattleEngine.Materialize(Playtest("試遊・標 ボス台"), BattleContext.PlayerTeam), S287.Waves[0].Make(), s, verbose: true).Events.Count(x => x.Kind == BattleEventKind.Insight);
            }
            Expect("(j) 見切りの出来事（`Insight`）はソラのいる台でだけ出る", ins > 0 && insNo == 0, $"循環の台 {ins}・標 ボス台（ソラなし）{insNo}");
        }
        // (k) 決定性・verbose の有無
        {
            int nd = 0;
            foreach (var v in new[] { Vers[1], Vers[2] }) foreach (var w in S287.Waves) for (int s = 0; s < 15; s++)
            {
                var f = Apply(Cycle, v);
                var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
            }
            Expect("(k) 決定的・verbose の有無で勝敗と決着T が変わらない", nd == 0, $"{nd} 件");
        }
        // (l) PickOne ／ Roll の数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show HEAD:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var pr = System.Diagnostics.Process.Start(psi)!;
                    string o = pr.StandardOutput.ReadToEnd(); pr.WaitForExit();
                    return pr.ExitCode == 0 ? o : null;
                }
                catch { return null; }
            }
            bool clean = true; var notes = new List<string>();
            foreach (var path in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
            {
                string now = File.ReadAllText(path); string? head = Head(path);
                if (head is null) { clean = false; continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(l) `PickOne(` ／ `Roll(` を新たに使っていない", clean, string.Join("・", notes));
        }
        // (m) 規定の行では HK の口が働かない
        {
            long any = 0;
            foreach (var (n, f) in CompareBuilds()) for (int s = 0; s < 2; s++) foreach (var t in BattleEngine.Run(f, EnemyCatalog.Stages[1].Enemy, s, verbose: false).TallyByUnit.Values) any += t.RallyHeals + t.BundleTurn + t.BundleOut;
            Expect("(m) 規定の行（`compare` 64 行・第2波・seed 0..1）ではまとまりも回復も数えない（HK の保持者がいない）", any == 0, $"{any}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
