using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// =====================================================================================
// sh364 —— 第364期【軽い期】アカの席の確かめ ／ 跳ね撃ち（全体攻撃しか持たない駒をボスに届かせる）——アカ ／ ガレ。規定は変えない。
// 指示書は design/PHASE364_SCATTER_SHOT_SPEC.md ／ 報告は design/PHASE364_SCATTER_SHOT.md。
//
//     dotnet run --project BattleSim -c Release 0 sh364 p0                  # §2 Phase 0: 台 B の後3（アカの席）を R0〜R5 に替える × ボス ／ 近衛 ／ 大隊・seed 0..99
//     dotnet run --project BattleSim -c Release 0 sh364 seats [aka|gare]    # §4: アカ 6,720 並び × AK-n ／ AKn-A ／ AKn-B ・ガレ 3,360 並び × 規定 ／ GR-A ／ GR-B
//     dotnet run --project BattleSim -c Release 0 sh364 reach               # §6 の 2: 勇者への届き方（代表台 × 版 × ボス・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh364 boards              # §6 の 3・5: 代表台 × 11 波 × 版（seed 0..99）・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh364 compare             # §6 の 4: `compare` 64 行 × 版（規定との差）
//     dotnet run --project BattleSim -c Release 0 sh364 log <版> <台> <波> [seed]
//     dotnet run --project BattleSim -c Release 0 sh364 check               # 自己検査
//
// 版の名前（ASCII）: アカ base ／ akn ／ akna ／ aknb（Id `susu` を差し替え）・ガレ gare ／ gra ／ grb（Id `gare` を差し替え）。
// **予測のファイルが無ければ本測定（seats ／ reach ／ boards ／ compare）を走らせない**（R397・`.tmp/p364/predict.md`）。
// =====================================================================================
static partial class Sh364Diag
{
    internal const string PredictFile = ".tmp/p364/predict.md";
    const string Tag = "第364期";
    const int Seeds = 100, CompareSeeds = 200, BossSeeds = 5, EliteSeeds = 20, TrajT = 10;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "reach" or "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh364: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": Phase0(); return;
            case "seats": SeatsAll(A(3, "")); return;
            case "reach": ReachAll(); return;
            case "boards": BoardsAll(); return;
            case "compare": CompareAll(); return;
            case "log": Log(V(A(3, "akn")), A(4, KeyB), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh364: モードは p0 / seats / reach / boards / compare / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    /// <summary>版（駒 Id ・差し替える定義）。アカの基準は AK-n（第363期の版）と規定、ガレの基準は規定。</summary>
    internal sealed record Ver(string Name, string Ascii, string Id, UnitDef Def);
    internal static readonly Ver[] AkaVers =
    {
        new("規定のアカ", "base", "susu", UnitCatalog.SusuH364),
        new("AK-n", "akn", "susu", UnitCatalog.SusuAKn),
        new("AKn-A 集まる全体", "akna", "susu", UnitCatalog.SusuAKnA),
        new("AKn-B 跳ね弾", "aknb", "susu", UnitCatalog.SusuAKnB),
    };
    internal static readonly Ver[] GareVers =
    {
        new("規定のガレ", "gare", "gare", UnitCatalog.GareH364),
        new("GR-A 集まる全体", "gra", "gare", UnitCatalog.GareGRA),
        new("GR-B 跳ね弾", "grb", "gare", UnitCatalog.GareGRB),
    };
    internal static Ver V(string n) => AkaVers.Concat(GareVers).First(v => v.Name == n || v.Ascii == n);
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, v.Id, v.Def);

    // ---------------------------------------------------------------------------------
    // 台
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] AkaFixed = { UnitCatalog.Kado, UnitCatalog.SusuH364 };
    internal static readonly UnitDef[] AkaPool = { UnitCatalog.Rica, UnitCatalog.Tome, UnitCatalog.Guza, UnitCatalog.Borg, UnitCatalog.Hisa, UnitCatalog.Doha, UnitCatalog.Mudo, UnitCatalog.Lili };
    internal static readonly UnitDef[] GareFixed = { UnitCatalog.Sasa, UnitCatalog.Hibi, UnitCatalog.GareH364 };
    internal static readonly UnitDef[] GarePool = { UnitCatalog.Uro, UnitCatalog.Gald, UnitCatalog.Dolga, UnitCatalog.Golm, UnitCatalog.Nel, UnitCatalog.Ban, UnitCatalog.Kubi, UnitCatalog.Som };
    internal static Formation ByKey(string key)
    {
        var all = AkaFixed.Concat(AkaPool).Concat(GareFixed).Concat(GarePool).ToArray();
        var defs = key.Split('・').Select(n => all.First(d => Short(d) == n)).ToArray();
        var f = new Formation();
        for (int s = 0; s < 5; s++) f[s] = defs[s];
        return f;
    }
    internal const string KeyB = "ボルグ・ミサ・ヒサ・カド・アカ", KeyS = "ミサ・カド・ヒサ・アカ・リリ", KeyN = "ドハ・カド・ミサ・アカ・ヒサ";
    internal const string KeyShard = "ネル・ササ・ソム・ヒビ・ガレ", KeyC360 = "ササ・ゴルム・ドルガ・ガレ・ヒビ", RowPebble = "礫 (ガレ×ヒビ×ウロ)";
    /// <summary>§4 の最良（`seats` の後に決めた・報告 §1）。アカ: ミサ＋ボルグ＋ヒサを含まない最良 ／ ガレ: ネルを含まない最良。</summary>
    // 決めたこと: アカは AKn-A の総当たりのミサ＋ボルグ＋ヒサを含まない最良（AK-n ／ AKn-B では含まない並びの勝ちが 0）・ガレは GR-A のネルを含まない最良（GR-B のネルなし1位より倒しT が短い）。
    internal static string AkaNoTrioKey = "カド・リリ・ムド・ボルグ・アカ", GareNoNelKey = "ソム・ササ・ゴルム・ヒビ・ガレ";
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    internal static I.Board[] AkaBoards() => new[]
    {
        new I.Board("アカ", $"B（{KeyB}）", ByKey(KeyB)),
        new I.Board("アカ", $"S（{KeyS}）", ByKey(KeyS)),
        new I.Board("アカ", $"N（{KeyN}）", ByKey(KeyN)),
    }.Concat(AkaNoTrioKey == "" ? Array.Empty<I.Board>() : new[] { new I.Board("アカ", $"T 連鎖なしの最良（{AkaNoTrioKey}）", ByKey(AkaNoTrioKey)) }).ToArray();
    internal static I.Board[] GareBoards() => new[]
    {
        new I.Board("ガレ", $"H 試遊・破片（{KeyShard}）", ByKey(KeyShard)),
        new I.Board("ガレ", $"C 第360期の C（{KeyC360}）", ByKey(KeyC360)),
        new I.Board("ガレ", RowPebble, Row(RowPebble)),
    }.Concat(GareNoNelKey == "" ? Array.Empty<I.Board>() : new[] { new I.Board("ガレ", $"E ネルなしの最良（{GareNoNelKey}）", ByKey(GareNoNelKey)) }).ToArray();
    internal static readonly I.W[] Waves = new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5, I.Nine }.Concat(I.StrongWaves).ToArray();

    static Formation BoardOf(string n) => n.Contains('・') && !n.Contains(' ')
        ? ByKey(n)
        : CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Versions).First(r => r.Name.StartsWith(n, StringComparison.Ordinal)).F;
    static void Log(Ver v, string board, string wave, int seed)
    {
        var f = Apply(BoardOf(board), v); var w = D.WaveOf(wave);
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {Seats(f)} × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose・台本と帳簿を読む・盤面は動かさない）
    // ---------------------------------------------------------------------------------
    internal sealed class Kx
    {
        public long N, Wins, WinT, Win1, Lost, WipeT, Fell, Draw, BossN;
        // 見る駒（席 ／ Id）の勇者への与ダメ・勇者への一撃の回数（その駒 ／ 全員・T1〜T3）・羽の標撃ち（T1〜T3）・勇者の標の層の最大
        public long FocusToBoss, FocusHits, BossHits13, FeatherOnBoss13, MaxLayerSum, FocusToFoe, FocusShots;
        public long MaxTurnSum, MaxTurnN;
        public readonly Dictionary<string, long> MaxTurnBy = new();
        public long Volleys, Shots, Gathered, GatherMult;
        public void Add(Kx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1 += o.Win1; Lost += o.Lost; WipeT += o.WipeT; Fell += o.Fell; Draw += o.Draw; BossN += o.BossN;
            FocusToBoss += o.FocusToBoss; FocusHits += o.FocusHits; BossHits13 += o.BossHits13; FeatherOnBoss13 += o.FeatherOnBoss13; MaxLayerSum += o.MaxLayerSum; FocusToFoe += o.FocusToFoe; FocusShots += o.FocusShots;
            MaxTurnSum += o.MaxTurnSum; MaxTurnN += o.MaxTurnN;
            foreach (var (k, x) in o.MaxTurnBy) MaxTurnBy[k] = MaxTurnBy.GetValueOrDefault(k) + x;
            Volleys += o.Volleys; Shots += o.Shots; Gathered += o.Gathered; GatherMult += o.GatherMult;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }

    /// <summary>1戦。<paramref name="focusId"/> の駒を「見る駒」として数える（台 B の後3 の替えはどれも Id が台の中で1枚だけ）。</summary>
    internal static Kx One(Formation f, I.W w, int seed, string focusId)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Kx { N = 1, Fell = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.Win1 = 1; }
        else { a.Lost = 1; a.WipeT = p.Max(u => u.LastDeathTurn); if (r.Turns >= BattleEngine.MaxTurns && p.Any(u => u.IsAlive)) a.Draw = 1; }
        var mine = p.ToDictionary(u => u.InstanceId);
        var focus = p.FirstOrDefault(u => u.Def.Id == focusId);
        var boss = e.FirstOrDefault(u => u.HasTrait(TraitId.BossRise11));
        var perTurn = new Dictionary<int, Dictionary<string, long>>();
        long maxLayer = 0;
        foreach (var x in r.Events)
        {
            int tn = x.Turn;
            if (x.Kind == BattleEventKind.Damage && x.ActorId is int ai && mine.TryGetValue(ai, out var au) && x.TargetId is int ti && !mine.ContainsKey(ti))
            {
                bool isFocus = focus is not null && ai == focus.InstanceId;
                if (isFocus) a.FocusToFoe += x.Amount;
                if (boss is not null && ti == boss.InstanceId)
                {
                    string k = isFocus ? "見る駒" : Short(au.Def);
                    if (!perTurn.TryGetValue(tn, out var d)) perTurn[tn] = d = new();
                    d[k] = d.GetValueOrDefault(k) + x.Amount;
                    if (isFocus) { a.FocusToBoss += x.Amount; a.FocusHits++; }
                    if (tn >= 1 && tn <= 3) a.BossHits13++;
                }
            }
            if (boss is not null && x.TargetId == boss.InstanceId)
            {
                if (x.Kind == BattleEventKind.FeatherMark && tn >= 1 && tn <= 3) a.FeatherOnBoss13++;
                if (x.Kind is BattleEventKind.FeatherMark or BattleEventKind.MarkLayer) maxLayer = Math.Max(maxLayer, x.Amount);
            }
        }
        if (boss is not null)
        {
            a.BossN = 1; a.MaxLayerSum = maxLayer;
            if (perTurn.Count > 0)
            {
                var best = perTurn.OrderByDescending(kv => kv.Value.Values.Sum()).First().Value;
                a.MaxTurnSum = best.Values.Sum(); a.MaxTurnN = 1;
                foreach (var (k, v) in best) a.MaxTurnBy[k] = v;
            }
        }
        if (focus is not null && r.TallyByUnit.TryGetValue(focus.Def.Id, out var t))
        {
            a.Volleys = t.Sh364Volleys; a.Shots = t.Sh364Shots; a.Gathered = t.Sh364Gathered; a.GatherMult = t.Sh364GatherMult;
        }
        return a;
    }

    internal static Kx Measure(Formation f, I.W w, string focusId, int n = Seeds)
    {
        var parts = new Kx[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i, focusId));
        var all = new Kx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static string MaxBy(Kx a) => a.MaxTurnN == 0 ? "—"
        : $"{Per(a.MaxTurnSum, a.MaxTurnN)}（" + string.Join("・", a.MaxTurnBy.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key} {Per(kv.Value, a.MaxTurnN)}")) + "）";

    // ---------------------------------------------------------------------------------
    // §2 Phase 0: 台 B の後3（アカの席）の替え
    // ---------------------------------------------------------------------------------
    internal static UnitDef Plain(string id, string name, AttackPattern pat, bool idle = false) => new()
    {
        Id = id, Name = name, MaxHp = UnitCatalog.SusuH364.MaxHp, Attack = UnitCatalog.SusuH364.Attack, Speed = UnitCatalog.SusuH364.Speed, Advances = false,
        Traits = Array.Empty<TraitId>(), Pattern = pat,
        Actions = idle ? new UnitAction[] { new(ActionKind.Skill, Label: "何もしない") } : Array.Empty<UnitAction>(),
    };
    internal static readonly UnitDef R2 = Plain("plain_all", "素体・毎手番全体", AttackPattern.All);
    internal static readonly UnitDef R3 = Plain("plain_one", "素体・毎手番単体", AttackPattern.Single);
    internal static readonly UnitDef R4 = Plain("plain_idle", "素体・何もしない", AttackPattern.Single, idle: true);
    internal static Formation BWith(UnitDef d) { var f = ByKey(KeyB); f[4] = d; return f; }

    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = new[] { I.Boss, I.Guard, I.Bat };
        var r5c = new[] { UnitCatalog.Rica, UnitCatalog.Guza, UnitCatalog.Doha, UnitCatalog.Mudo, UnitCatalog.Lili };
        Console.WriteLine($"# {Tag} §2 Phase 0 —— 台 B（{KeyB}）の後3（アカの席）を替える × ボス ／ 近衛 ／ 大隊・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("「見る駒」は後3 の駒。勇者への一撃の回数・羽の標撃ち（`FeatherMark`）は T1〜T3 の和、標の層は勇者に付いた層の最大（羽 ／ 層の出来事から）。1ターンの与ダメの最大はその戦で勇者への与ダメが最も多いターンの和と出どころ（1戦あたり）。");
        Console.WriteLine();
        // R5: 候補 5 枚から勇者戦が最も良いもの（勝ちの数 → 倒しT → 最大の1ターン）を選ぶ（決めたこと）。
        var r5m = r5c.Select(d => (d, k: Measure(BWith(d), I.Boss, d.Id))).ToArray();
        var r5 = r5m.OrderByDescending(x => x.k.Wins).ThenBy(x => x.k.Wins == 0 ? 99 : (double)x.k.WinT / x.k.Wins).ThenByDescending(x => x.k.MaxTurnSum).First().d;
        Console.WriteLine("R5 の候補（ボス・seed 0..99）: " + string.Join("、", r5m.Select(x => $"{Short(x.d)} {F1(x.k.Win)}%・{x.k.WinTs}T・最大 {Per(x.k.MaxTurnSum, x.k.MaxTurnN)}")) + $" → **{Short(r5)}**。");
        Console.WriteLine();
        var reps = new (string, UnitDef)[]
        {
            ("R0 AK-n のアカ（基準）", UnitCatalog.SusuAKn), ("R1 規定のアカ（灰）", UnitCatalog.SusuH364), ("R2 素体・毎手番全体（攻 3）", R2),
            ("R3 素体・毎手番単体（攻 3）", R3), ("R4 素体・何もしない", R4), ($"R5 {Short(r5)}", r5),
        };
        Console.WriteLine("| 替え | 波 | 勝率・倒しT（決着T） | 1ターンの与ダメの最大（出どころ） | 見る駒の勇者への与ダメ ／戦 | 勇者への一撃 T1〜T3（うち見る駒 ／戦） | 羽の標撃ち T1〜T3 | 勇者の標の層（最大） | 倒れた ／戦 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|--:|");
        foreach (var (name, d) in reps)
            foreach (var w in waves)
            {
                var a = Measure(BWith(d), w, d.Id);
                bool b = w == I.Boss;
                Console.WriteLine($"| {name} | {w.Name} | {F1(a.Win)}・{a.WinTs}T | {(b ? MaxBy(a) : "")} | {(b ? Per(a.FocusToBoss, a.N) : "")} | {(b ? $"{Per(a.BossHits13, a.N)}（{Per(a.FocusHits, a.N)}）" : "")} | {(b ? Per(a.FeatherOnBoss13, a.N) : "")} | {(b ? Per(a.MaxLayerSum, a.N) : "")} | {Per2(a.Fell, a.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
