using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// =====================================================================================
// sh362 —— 第362期【軽い期】段0: ササの SS-n を規定に ／ 本体: 自傷軸の立て直し①（傷つくほど強く・カドの二段構え KD-t ／ KD-r）。
// 指示書は design/PHASE362_SELFHARM_KADO_SPEC.md ／ 報告は design/PHASE362_SELFHARM_KADO.md。
//
//     dotnet run --project BattleSim -c Release 0 sh362 p0                  # §3 Phase 0: 自傷の流れ（書き手 ／ 読み手）・カドの診断・傷の推移（規定・段0 入り）
//     dotnet run --project BattleSim -c Release 0 sh362 seats               # §5: カド ／ アカ ＋ 8枚から3枚（56 組）× 120 ＝ 6,720 並び × 規定 ／ KD-t ／ KD-r
//     dotnet run --project BattleSim -c Release 0 sh362 flow [版,…]         # §7 の 1・2: カドの一撃 ／ ボスへの届き方（代表台 × ボス ／ 大隊）
//     dotnet run --project BattleSim -c Release 0 sh362 boards [版,…]       # §7 の 3・6: 代表台 × 11 波 × 版（seed 0..99）・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh362 axes                # §7 の 4: 軸の横並び（第348期 §3 の物差し）
//     dotnet run --project BattleSim -c Release 0 sh362 compare [版,…]      # §7 の 5: `compare` 64 行 × 版 ＋ 段0 の前（`Pin361`）とのセルの差
//     dotnet run --project BattleSim -c Release 0 sh362 log <版> <台> <波> [seed]   # 1戦のログ（台は並びの名前「カド・アカ・…」か compare の行名の頭）
//     dotnet run --project BattleSim -c Release 0 sh362 check               # 自己検査
//
// 版の名前（ASCII）: base（規定・段0 入り）／ kdt ／ kdr。版はカドだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ本測定（seats ／ flow ／ boards ／ axes ／ compare）を走らせない**（R397・`.tmp/p362/predict.md`）。
// =====================================================================================
static partial class Sh362Diag
{
    internal const string PredictFile = ".tmp/p362/predict.md";
    const string Tag = "第362期";
    const int Seeds = 100, CompareSeeds = 200, BossSeeds = 5, EliteSeeds = 20, P0Seeds = 20, TrajT = 10;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "flow" or "boards" or "axes" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh362: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": Phase0(); return;
            case "seats": SeatsAll(); return;
            case "flow": FlowAll(Pick(A(3, ""))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "axes": AxesAll(); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "反撃 (ヒサ×カド)"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            case "diff": Diff(V(A(3, "kdt")), A(4, "反撃改3"), int.Parse(A(5, "5"))); return;
            default: Console.WriteLine("sh362: モードは p0 / seats / flow / boards / axes / compare / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    internal sealed record Ver(string Name, string Ascii, UnitDef Kado);
    internal static readonly Ver[] Vers =
    {
        new("規定（段0 入り）", "base", UnitCatalog.KadoH362),   // 第363期: 規定のカドが KD-r になったので第362期の規定（`KadoH362`）に固定
        new("KD-t 傷の棘", "kdt", UnitCatalog.KadoKDt),
        new("KD-r 棘を剥く", "kdr", UnitCatalog.KadoKDr),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, "kado", v.Kado);
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    /// <summary>§3-2 の対照: カドと同じ体（HP 96 ／ 攻 11 ／ 速 4・踏み込まない）で札を1枚も持たない素体（ふつうに殴る）。</summary>
    internal static readonly UnitDef KadoPlain = new()
    {
        Id = "kado_plain", Name = "素体のカド", MaxHp = UnitCatalog.Kado.MaxHp, Attack = UnitCatalog.Kado.Attack, Speed = UnitCatalog.Kado.Speed,
        Advances = UnitCatalog.Kado.Advances, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single,
    };

    /// <summary>§5 の固定2枚（カド ／ アカ）と残り3枚の母集団（8枚）。</summary>
    internal static readonly UnitDef[] Fixed = { UnitCatalog.KadoH362, UnitCatalog.Susu };   // 第363期: 旧のカドに固定
    internal static readonly UnitDef[] Pool = { UnitCatalog.Rica, UnitCatalog.Tome, UnitCatalog.Guza, UnitCatalog.Borg, UnitCatalog.Hisa, UnitCatalog.Doha, UnitCatalog.Mudo, UnitCatalog.Lili };
    internal static Formation ByKey(string key)
    {
        var defs = key.Split('・').Select(n => Fixed.Concat(Pool).First(d => Short(d) == n)).ToArray();
        var f = new Formation();
        for (int s = 0; s < 5; s++) f[s] = defs[s];
        return f;
    }
    internal static (string Name, Formation F)[] KadoRows() => CompareBuilds().Where(r => Has(r.F, "kado")).Select(r => (r.Name, Pin362(r.F))).ToArray();   // 第363期: 旧のカドに固定
    static Formation Row(string n) => Pin362(CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F);   // 第363期: 旧のカドに固定

    /// <summary>§7 の代表台（決めたこと: `p0` と `seats` の結果から選んだ・報告 §1）。</summary>
    // RowWorst: Phase 0 §3-2 でカドが素体より台を下げた量（ボス ／ 近衛 ／ 大隊の和）が最も大きい行。反撃改 (ドハ×カド) と 逸らし (ソラ×カド) が −65pt で並び、1つの波の落ち（近衛 −65）が大きい方を採った。
    // KeyTop: `seats` の KD-r の1位 ＝ 精鋭の最良 ＝ リリを含む最良（ボスに勝てる並びはどの版でも 0・両立も 0）。KeyNoLili: KD-r のリリを含まない最良。
    internal const string RowHisa = "反撃 (ヒサ×カド)", RowWorst = "反撃改 (ドハ×カド)";
    internal const string KeyTop = "ミサ・カド・ヒサ・アカ・リリ", KeyNoLili = "ドハ・カド・ミサ・アカ・ヒサ";
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("自傷", RowHisa, Row(RowHisa)),
        new I.Board("自傷", RowWorst, Row(RowWorst)),
        new I.Board("自傷", $"S KD-r の1位・リリを含む最良（{KeyTop}）", ByKey(KeyTop)),
        new I.Board("自傷", $"N KD-r のリリを含まない最良（{KeyNoLili}）", ByKey(KeyNoLili)),
    };
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

    /// <summary>`compare` の1行 × 1波（seed 0..199）で、規定と版の勝敗が割れた seed を並べる（落ちた行の理由を探す）。</summary>
    static void Diff(Ver v, string row, int wave)
    {
        var f0 = CompareBuilds().First(r => r.Name.StartsWith(row, StringComparison.Ordinal)).F;
        var en = EnemyCatalog.Stages[wave - 1].Enemy;
        var lost = new List<int>(); var won = new List<int>();
        int drawA = 0, drawB = 0, drawLost = 0;
        for (int s = 0; s < CompareSeeds; s++)
        {
            var a = BattleEngine.Run(Apply(f0, Vers[0]), en, s, verbose: false);
            var b = BattleEngine.Run(Apply(f0, v), en, s, verbose: false);
            if (!a.PlayerWon && a.Turns >= BattleEngine.MaxTurns && a.PlayerSurvivors > 0) drawA++;
            if (!b.PlayerWon && b.Turns >= BattleEngine.MaxTurns && b.PlayerSurvivors > 0) drawB++;
            if (a.PlayerWon && !b.PlayerWon) { lost.Add(s); if (b.Turns >= BattleEngine.MaxTurns && b.PlayerSurvivors > 0) drawLost++; }
            else if (!a.PlayerWon && b.PlayerWon) won.Add(s);
        }
        Console.WriteLine($"{row} × 第{wave}波 × {v.Name}: 規定で勝ち版で負け {lost.Count}（うち 30T の打ち切り {drawLost}・{string.Join(",", lost.Take(20))}）／ 規定で負け版で勝ち {won.Count}（{string.Join(",", won.Take(20))}）／ 30T の打ち切り 規定 {drawA} → 版 {drawB}（{CompareSeeds} 戦）");
        if (lost.Count > 0)
        {
            int s = lost[0];
            foreach (var (nm, vv) in new[] { ("規定", Vers[0]), (v.Name, v) })
            {
                var r = BattleEngine.Run(Apply(f0, vv), en, s, verbose: true);
                Console.WriteLine($"## seed {s} × {nm}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
                foreach (var l in r.Log) Console.WriteLine(l.ToString());
            }
        }
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose・帳簿と台本を読む・盤面は動かさない）
    // ---------------------------------------------------------------------------------
    internal sealed class Kx
    {
        public long N, Wins, WinT, Win1, Lost, WipeT, Fell, Draw, BossN;
        // カド
        public long KadoN, Rip, RipSum, Str, StrSum, StrMain, Gain, KToE, KToA, Bare, BareT, KDied, KDeathT, KWoundDeath, Guards, Havoc;
        // 自傷の流れ（書き手の短い名前 → 味方へ ／ 自分へ）・出どころなしの刻み（味方が受けた）・読み手
        public readonly Dictionary<string, long> FfOther = new(), FfSelf = new();
        public long Dot, AkaBlood, AkaN, MudoAllyHits, MudoN, HisaFf, HisaN, KadoGainN;
        // ターンごと（ボスの戦・そのターンまで続いた戦の和 ／ 数）: 与ダメ（カド ／ アカ ／ ほか）・ボスの回復・カドの傷
        public readonly int T;
        public readonly long[] TK, TA, TO, THeal, TCnt, TKW, TKWc;
        // 駒ごとの傷（ターンの頭・生きている駒だけ）
        public readonly Dictionary<string, long[]> UW = new(), UWc = new();
        public Kx(int t = TrajT) { T = t; TK = new long[t + 1]; TA = new long[t + 1]; TO = new long[t + 1]; THeal = new long[t + 1]; TCnt = new long[t + 1]; TKW = new long[t + 1]; TKWc = new long[t + 1]; }
        static void M(Dictionary<string, long> a, Dictionary<string, long> b) { foreach (var (k, x) in b) a[k] = a.GetValueOrDefault(k) + x; }
        static void MA(Dictionary<string, long[]> a, Dictionary<string, long[]> b, int t)
        {
            foreach (var (k, x) in b)
            {
                if (!a.TryGetValue(k, out var y)) a[k] = y = new long[t + 1];
                for (int i = 0; i <= t && i < x.Length; i++) y[i] += x[i];
            }
        }
        public void Add(Kx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1 += o.Win1; Lost += o.Lost; WipeT += o.WipeT; Fell += o.Fell; Draw += o.Draw; BossN += o.BossN;
            KadoN += o.KadoN; Rip += o.Rip; RipSum += o.RipSum; Str += o.Str; StrSum += o.StrSum; StrMain += o.StrMain; Gain += o.Gain; KToE += o.KToE; KToA += o.KToA;
            Bare += o.Bare; BareT += o.BareT; KDied += o.KDied; KDeathT += o.KDeathT; KWoundDeath += o.KWoundDeath; Guards += o.Guards; Havoc += o.Havoc;
            M(FfOther, o.FfOther); M(FfSelf, o.FfSelf);
            Dot += o.Dot; AkaBlood += o.AkaBlood; AkaN += o.AkaN; MudoAllyHits += o.MudoAllyHits; MudoN += o.MudoN; HisaFf += o.HisaFf; HisaN += o.HisaN; KadoGainN += o.KadoGainN;
            for (int t = 0; t <= Math.Min(T, o.T); t++) { TK[t] += o.TK[t]; TA[t] += o.TA[t]; TO[t] += o.TO[t]; THeal[t] += o.THeal[t]; TCnt[t] += o.TCnt[t]; TKW[t] += o.TKW[t]; TKWc[t] += o.TKWc[t]; }
            MA(UW, o.UW, T); MA(UWc, o.UWc, T);
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
        public long FfAll => FfOther.Values.Sum() + FfSelf.Values.Sum();
    }

    internal static Kx One(Formation f, I.W w, int seed, int tt = TrajT)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Kx(tt) { N = 1, Fell = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.Win1 = 1; }
        else { a.Lost = 1; a.WipeT = p.Max(u => u.LastDeathTurn); if (r.Turns >= BattleEngine.MaxTurns && p.Any(u => u.IsAlive)) a.Draw = 1; }
        var mine = p.ToDictionary(u => u.InstanceId);
        var hp = p.ToDictionary(u => u.InstanceId, u => u.MaxHp);
        var kado = p.FirstOrDefault(u => u.Def.Id == "kado");
        var aka = p.FirstOrDefault(u => u.Def.Id == "susu");
        var mudo = p.FirstOrDefault(u => u.Def.Id == "mudo");
        var boss = e.FirstOrDefault(u => u.HasTrait(TraitId.BossRise11));
        int last = r.Turns;
        if (boss is not null) { a.BossN = 1; for (int t = 1; t <= Math.Min(tt, last); t++) a.TCnt[t] = 1; }
        bool kadoDead = false;
        foreach (var x in r.Events)
        {
            int tn = x.Turn;
            switch (x.Kind)
            {
                case BattleEventKind.TurnStart:
                    if (tn < 1 || tn > tt) break;
                    foreach (var (id, u) in mine)
                    {
                        if (hp[id] <= 0) continue;
                        string k = Short(u.Def);
                        if (!a.UW.TryGetValue(k, out var uw)) { a.UW[k] = uw = new long[tt + 1]; a.UWc[k] = new long[tt + 1]; }
                        uw[tn] += Math.Max(0, u.MaxHp - hp[id]); a.UWc[k][tn]++;
                        if (u == kado) { a.TKW[tn] += Math.Max(0, u.MaxHp - hp[id]); a.TKWc[tn]++; }
                    }
                    break;
                case BattleEventKind.Damage:
                    if (x.TargetId is int ti && mine.TryGetValue(ti, out var tu))
                    {
                        int before = hp[ti];
                        hp[ti] = x.HpAfter;
                        if (tu == kado && !kadoDead && x.HpAfter <= 0) { kadoDead = true; a.KWoundDeath += Math.Max(0, tu.MaxHp - before); }
                        if (x.ActorId is int ai && mine.TryGetValue(ai, out var au))
                        {
                            var d = ai == ti ? a.FfSelf : a.FfOther;
                            string k = Short(au.Def);
                            d[k] = d.GetValueOrDefault(k) + x.Amount;
                            if (tu == mudo && ai != ti) a.MudoAllyHits++;
                        }
                        else if (x.ActorId is null) a.Dot += x.Amount;
                    }
                    else if (boss is not null && x.TargetId == boss.InstanceId && x.ActorId is int bi && mine.ContainsKey(bi) && tn >= 1 && tn <= tt && tn <= last)
                    {
                        if (kado is not null && bi == kado.InstanceId) a.TK[tn] += x.Amount;
                        else if (aka is not null && bi == aka.InstanceId) a.TA[tn] += x.Amount;
                        else a.TO[tn] += x.Amount;
                    }
                    break;
                case BattleEventKind.Heal:
                    if (x.TargetId is int hi && mine.ContainsKey(hi)) hp[hi] = x.HpAfter;
                    else if (boss is not null && x.TargetId == boss.InstanceId && tn >= 1 && tn <= tt && tn <= last) a.THeal[tn] += x.Amount;
                    break;
                case BattleEventKind.Intercept:
                    if (kado is not null && x.ActorId == kado.InstanceId && x.Text == InterceptLabels.ThornGuard) a.Guards++;
                    break;
            }
        }
        bool hisa = p.Any(u => u.Def.Id == "hisa");
        foreach (var u in p)
            if (r.TallyByUnit.TryGetValue(u.Def.Id, out var t))
            {
                a.Havoc += t.HavocTaken;
                if (hisa) a.HisaFf += t.FfOnMarked;   // 標の付いた駒への同士討ち（ヒサのいる台だけ・ほかの標の書き手の台は数えない）
            }
        if (hisa) a.HisaN = 1;
        if (mudo is not null) a.MudoN = 1;
        if (aka is not null) { a.AkaN = 1; if (r.TallyByUnit.TryGetValue("susu", out var at)) a.AkaBlood = at.AshGained; }
        if (kado is not null)
        {
            a.KadoN = 1; a.KadoGainN = 1;
            if (r.TallyByUnit.TryGetValue("kado", out var k))
            {
                a.Rip = k.ThornRipostes; a.RipSum = k.Sh362RiposteSum; a.Str = k.Sh362Strikes; a.StrSum = k.Sh362StrikeSum; a.StrMain = k.Sh362StrikeMain;
                a.Gain = k.Sh362ThornGain; a.KToE = k.DamageToEnemy; a.KToA = k.DamageToAlly;
                if (k.Sh362BareTurn > 0) { a.Bare = 1; a.BareT = k.Sh362BareTurn; }
            }
            if (kado.LastDeathTurn > 0) { a.KDied = 1; a.KDeathT = kado.LastDeathTurn; }
        }
        return a;
    }

    internal static Kx Measure(Formation f, I.W w, int n = Seeds, int tt = TrajT)
    {
        var parts = new Kx[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i, tt));
        var all = new Kx(tt);
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static string Kv(Dictionary<string, long> d, long n) => d.Count == 0 || d.Values.All(v => v == 0) ? "—" : string.Join("、", d.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, n)}"));

    /// <summary>カドの一撃の1行（反撃 ／ 殴りかかり ／ 剥いた T ／ 倒れた T）。</summary>
    internal static string KadoLine(Kx a)
        => $"反撃 {Per2(a.Rip, a.N)} 回・1回 {Per(a.RipSum, a.Rip)}・殴りかかり {Per2(a.Str, a.N)} 回・1回 {Per(a.StrSum, a.Str)}（主目標に {Per(a.StrMain, a.Str)}）・敵へ与えた計 {Per(a.KToE, a.N)}"
         + $"・巻き込んだ味方 {Per(a.KToA, a.N)}・攻撃力の伸び {Per(a.Gain, a.N)}・剥いた {Pct(a.Bare, a.N)}%（T {Per(a.BareT, a.Bare)}）・身代わり {Per2(a.Guards, a.N)} 回"
         + $"・カドが倒れた {Pct(a.KDied, a.N)}%（T {Per(a.KDeathT, a.KDied)}・倒れる直前の傷 {Per(a.KWoundDeath, a.KDied)}）・惨禍で増えた味方の傷 {Per(a.Havoc, a.N)}";

    internal static void PrintTraj(string title, Kx a)
    {
        Console.WriteLine($"#### {title}（勝率 {F1(a.Win)}%・倒しT {a.WinTs}・全滅 T {Per(a.WipeT, a.Lost)}）");
        Console.WriteLine();
        Console.WriteLine("ターン t まで戦が続いた戦の平均。カドの傷はターンの頭（生きている戦だけ）。");
        Console.WriteLine();
        Console.WriteLine("| T | 続いた戦 | 与ダメ 計 | うちカド | うちアカ | ほか | 勇者の回復 | 与ダメ − 回復 | カドの傷 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int t = 1; t <= a.T; t++)
        {
            long c = a.TCnt[t];
            if (c == 0) continue;
            long all = a.TK[t] + a.TA[t] + a.TO[t];
            Console.WriteLine($"| {t} | {c} | {Per(all, c)} | {Per(a.TK[t], c)} | {Per(a.TA[t], c)} | {Per(a.TO[t], c)} | {Per(a.THeal[t], c)} | {Per(all - a.THeal[t], c)} | {Per(a.TKW[t], a.TKWc[t])} |");
        }
        Console.WriteLine();
    }

    static void PrintWounds(string title, Kx a)
    {
        Console.WriteLine($"#### {title}");
        Console.WriteLine();
        var keys = a.UW.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Console.WriteLine("| 駒 | " + string.Join(" | ", Enumerable.Range(1, a.T).Select(t => $"T{t}")) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, a.T).Select(_ => "--:|")));
        foreach (var k in keys)
            Console.WriteLine($"| {k} | " + string.Join(" | ", Enumerable.Range(1, a.T).Select(t => a.UWc[k][t] == 0 ? "—" : Per(a.UW[k][t], a.UWc[k][t]))) + " |");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3 Phase 0（規定・段0 入り）
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Concat(Presets.Versions.Where(r => r.Name.StartsWith("試遊", StringComparison.Ordinal))).Select(r => (r.Name, F: Pin362(r.F))).ToArray();   // 第363期: 旧のカドに固定
        var waves = new[] { I.M5, I.Guard, I.Boss };
        Console.WriteLine($"# {Tag} §3 Phase 0（規定・段0 入り）");
        Console.WriteLine();
        Console.WriteLine($"## 3-1. 自傷の流れ —— `compare` {CompareBuilds().Length} 行 ＋ 試遊の台 {rows.Length - CompareBuilds().Length} × 本編 第5波 ／ 近衛 ／ ボス・seed 0..{P0Seeds - 1}（1戦あたり・3波の平均）");
        Console.WriteLine();
        Console.WriteLine("味方が味方に与えた傷は台本の `Damage`（出どころ ＝ 同じ陣営の駒）で数えた（中継 ／ 巻き込み ／ 徴収 ／ 羽 ／ 破裂を含む・HP を減らした実額）。出どころの無い刻み（毒 ／ 燃焼）は誰が付けたかを分けていない（敵が付けた分を含む）。");
        Console.WriteLine("読み手: アカの血（`AshGained`）／ カドの攻撃力の伸び（巻き込み）／ ムドの味方からの被弾（回数・怒りの帳簿）／ ヒサ（標の付いた味方への同士討ち・回数・指差しの引き金）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 書き手（味方へ ／ 自分へ） | 出どころなしの刻み | 読み手 | 書き手と読み手が同じ台 |");
        Console.WriteLine("|---|---|--:|---|---|");
        var tot = new Kx(); var both = new List<string>();
        foreach (var (name, f) in rows)
        {
            var a = new Kx();
            foreach (var w in waves) a.Add(Measure(f, w, P0Seeds));
            tot.Add(a);
            var readers = new List<string>();
            if (a.AkaN > 0) readers.Add($"アカの血 {Per(a.AkaBlood, a.N)}");
            if (a.KadoN > 0) readers.Add($"カドの伸び {Per(a.Gain, a.N)}");
            if (a.MudoN > 0) readers.Add($"ムドの味方からの被弾 {Per2(a.MudoAllyHits, a.N)} 回");
            if (a.HisaN > 0) readers.Add($"ヒサ（標の同士討ち）{Per2(a.HisaFf, a.N)} 回");
            bool read = a.AkaBlood + a.Gain + a.MudoAllyHits + a.HisaFf > 0;
            if (a.FfAll == 0 && readers.Count == 0) continue;
            bool same = a.FfAll > 0 && read;
            if (same) both.Add(name);
            string ff = Kv(a.FfOther, a.N) + (a.FfSelf.Count > 0 ? $" ／ 自分へ: {Kv(a.FfSelf, a.N)}" : "");
            Console.WriteLine($"| {name} | {ff} | {Per(a.Dot, a.N)} | {(readers.Count == 0 ? "—" : string.Join("・", readers))} | {(same ? "**○**" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"全行の合計（1戦あたり）: 味方へ {Kv(tot.FfOther, tot.N)} ／ 自分へ {Kv(tot.FfSelf, tot.N)}。");
        Console.WriteLine();
        Console.WriteLine($"書き手と読み手が同じ台にいる行（{both.Count}）: {string.Join("、", both)}。");
        Console.WriteLine();

        var kr = KadoRows();
        var kw = new[] { I.Boss, I.Guard, I.Bat };
        Console.WriteLine($"## 3-2. カドの診断 —— カドのいる `compare` の行 {kr.Length} × ボス ／ 近衛 ／ 大隊・seed 0..{P0Seeds - 1}（素体 ＝ 同じ席に同じ体で札の無い駒）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 勝率 カド ／ 素体（差）| 決着T カド ／ 素体 | カドの一撃 |");
        Console.WriteLine("|---|---|---|---|---|");
        var bossK = new List<(string, Kx)>();
        foreach (var (name, f) in kr)
            foreach (var w in kw)
            {
                var a = Measure(f, w, P0Seeds); var b = Measure(S316.SwapId(f, "kado", KadoPlain), w, P0Seeds);
                Console.WriteLine($"| {name} | {w.Name} | {F1(a.Win)} ／ {F1(b.Win)}（{(a.Win - b.Win >= 0 ? "+" : "")}{F1(a.Win - b.Win)}）| {a.WinTs} ／ {b.WinTs} | {KadoLine(a)} |");
                if (w == I.Boss) bossK.Add((name, a));
            }
        Console.WriteLine();
        Console.WriteLine("### 3-2 の 5 と 3-3. 勇者戦の1ターンの与ダメ（カド ／ アカ ／ ほか）と勇者の回復・駒ごとの傷の推移");
        Console.WriteLine();
        foreach (var (name, a) in bossK) { PrintTraj($"{name} × ボス", a); PrintWounds($"{name} × ボス: 駒ごとの傷（最大HP − 今のHP・ターンの頭・生きている駒）", a); }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
