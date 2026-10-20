using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// =====================================================================================
// sh363 —— 第363期【軽い期】段0: カドの KD-r を規定に ／ 本体: 自傷軸の立て直し②（アカの作り直し・傷を血に変えて一気に撃つ・AK-n ／ AK-s）。
// 指示書は design/PHASE363_SELFHARM_AKA_SPEC.md ／ 報告は design/PHASE363_SELFHARM_AKA.md。
//
//     dotnet run --project BattleSim -c Release 0 sh363 p0                  # §3 Phase 0: 味方が受けた傷（出どころ別・惨禍の分）・今の傷の合計・勇者の残りHP・アカの手番（規定・段0 入り）
//     dotnet run --project BattleSim -c Release 0 sh363 seats               # §5: カド ／ アカ ＋ 8枚から3枚（56 組）× 120 ＝ 6,720 並び × 規定 ／ AK-n ／ AK-s
//     dotnet run --project BattleSim -c Release 0 sh363 flow [版,…]         # §7 の 1・2: アカの一撃 ／ ボスへの届き方（代表台 × ボス ／ 大隊）
//     dotnet run --project BattleSim -c Release 0 sh363 boards [版,…]       # §7 の 3・6: 代表台 × 11 波 × 版（seed 0..99）・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh363 axes                # §7 の 4: 軸の横並び（第348期 §3 の物差し）
//     dotnet run --project BattleSim -c Release 0 sh363 compare [版,…]      # §7 の 5: `compare` 64 行 × 版 ＋ 段0 の前（`Pin362`）とのセルの差
//     dotnet run --project BattleSim -c Release 0 sh363 log <版> <台> <波> [seed]   # 1戦のログ（台は並びの名前「カド・アカ・…」か compare ／ 試遊の行名の頭）
//     dotnet run --project BattleSim -c Release 0 sh363 check               # 自己検査
//
// 版の名前（ASCII）: base（規定・段0 入り）／ akn ／ aks。版はアカだけを差し替える（Id `susu` で引く・規定の上）。
// **予測のファイルが無ければ本測定（seats ／ flow ／ boards ／ axes ／ compare）を走らせない**（R397・`.tmp/p363/predict.md`）。
// =====================================================================================
static partial class Sh363Diag
{
    internal const string PredictFile = ".tmp/p363/predict.md";
    const string Tag = "第363期";
    const int Seeds = 100, CompareSeeds = 200, BossSeeds = 5, EliteSeeds = 20, P0Seeds = 20, TrajT = 10;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "flow" or "boards" or "axes" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh363: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
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
            case "log": Log(V(A(3, "base")), A(4, KeyS), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh363: モードは p0 / seats / flow / boards / axes / compare / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    internal sealed record Ver(string Name, string Ascii, UnitDef Aka);
    internal static readonly Ver[] Vers =
    {
        new("規定（段0 入り）", "base", UnitCatalog.SusuH364),
        new("AK-n 今の傷を詠む", "akn", UnitCatalog.SusuAKn),
        new("AK-s 溜め血", "aks", UnitCatalog.SusuAKs),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, "susu", v.Aka);
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    /// <summary>§5 の固定2枚（カド ／ アカ）と残り3枚の母集団（8枚・第362期 §5 と同じ）。</summary>
    internal static readonly UnitDef[] Fixed = { UnitCatalog.Kado, UnitCatalog.SusuH364 };
    internal static readonly UnitDef[] Pool = { UnitCatalog.Rica, UnitCatalog.Tome, UnitCatalog.Guza, UnitCatalog.Borg, UnitCatalog.Hisa, UnitCatalog.Doha, UnitCatalog.Mudo, UnitCatalog.Lili };
    internal static Formation ByKey(string key)
    {
        var defs = key.Split('・').Select(n => Fixed.Concat(Pool).First(d => Short(d) == n)).ToArray();
        var f = new Formation();
        for (int s = 0; s < 5; s++) f[s] = defs[s];
        return f;
    }
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;

    /// <summary>第362期の代表台 S ／ N ／ 反撃（指示書 §3 ／ §7）。§7 の「§5 の最良」は `seats` の後に決めた（報告 §1）。</summary>
    internal const string RowHisa = "反撃 (ヒサ×カド)";
    internal const string KeyS = "ミサ・カド・ヒサ・アカ・リリ", KeyN = "ドハ・カド・ミサ・アカ・ヒサ";
    internal static I.Board[] P0Boards() => new[]
    {
        new I.Board("自傷", $"S（{KeyS}）", ByKey(KeyS)),
        new I.Board("自傷", $"N（{KeyN}）", ByKey(KeyN)),
        new I.Board("自傷", RowHisa, Row(RowHisa)),
    };
    /// <summary>§7 の代表台。§5 の最良（AK-s の総当たりから・ボス ／ 両立 ／ リリなし の最良・重なれば減らす）は `seats` の後に `BestKeys` に書いた。</summary>
    // 決めたこと（報告 §1）: AK-n の総当たりでは「ボスの最良 ＝ 両立の最良 ＝ リリなしの最良」が同じ並び（B）。AK-s はボスに勝つ並び ／ 両立が 0 なので、
    // AK-s のリリなしの最良（C）と精鋭の最良 ＝ リリを含む最良（D）を足した。
    internal const string KeyB = "ボルグ・ミサ・ヒサ・カド・アカ", KeyC = "カド・ボルグ・アカ・ヒサ・ドハ", KeyD = "ドハ・カド・リリ・アカ・ヒサ";
    internal static readonly (string Label, string Key)[] BestKeys =
    {
        ("B AK-n のボス ／ 両立 ／ リリなしの最良", KeyB),
        ("C AK-s のリリなしの最良", KeyC),
        ("D AK-s の精鋭の最良 ＝ リリを含む最良", KeyD),
    };
    internal static I.Board[] Boards() => P0Boards().Concat(BestKeys.Select(b => new I.Board("自傷", $"{b.Label}（{b.Key}）", ByKey(b.Key)))).ToArray();
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
    // 1戦の計数（verbose・帳簿と台本を読む・盤面は動かさない）
    // ---------------------------------------------------------------------------------
    internal sealed class Kx
    {
        public long N, Wins, WinT, Win1, Lost, WipeT, Fell, Draw, BossN;
        // アカ
        public long AkaN, Casts, CastSum, FirstCastT, CastBattles, AkaDied, AkaDeathT, AkaTurns, Fallout, AtDeath, Gained, BFoe, BAlly, BSelf, BDot, BHav, AkaToE;
        // カド
        public long KadoN, Str, StrSum, Bare, BareT, KDied, KDeathT;
        public readonly int T;
        // ターンごと（そのターンまで続いた戦の和 ／ 数）
        // 味方が実際に減らした HP（出どころ: 敵 ／ 味方 ／ 自分 ／ 刻み）・うち惨禍の増分・ターンの頭の今の傷の合計（生きている味方）
        public readonly long[] LFoe, LAlly, LSelf, LDot, LHav, WSum, PCnt;
        // ボス戦: 勇者への与ダメ（カド ／ アカ ／ ほか）・勇者の回復・勇者の残りHP（回復の前 ／ 後）・続いた戦 ／ 勇者が生きていた戦
        public readonly long[] TK, TA, TO, THeal, TCnt, HBefore, HAfter, HCnt;
        // アカ: 撃った量（ターンごと）・血（ターンの頭・AK-s ／ 規定）
        public readonly long[] TCast, TCastN, TBlood, TBloodC;
        public Kx(int t = TrajT)
        {
            T = t;
            long[] A() => new long[t + 1];
            LFoe = A(); LAlly = A(); LSelf = A(); LDot = A(); LHav = A(); WSum = A(); PCnt = A();
            TK = A(); TA = A(); TO = A(); THeal = A(); TCnt = A(); HBefore = A(); HAfter = A(); HCnt = A();
            TCast = A(); TCastN = A(); TBlood = A(); TBloodC = A();
        }
        static void Add(long[] a, long[] b) { for (int i = 0; i < a.Length && i < b.Length; i++) a[i] += b[i]; }
        public void Add(Kx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1 += o.Win1; Lost += o.Lost; WipeT += o.WipeT; Fell += o.Fell; Draw += o.Draw; BossN += o.BossN;
            AkaN += o.AkaN; Casts += o.Casts; CastSum += o.CastSum; FirstCastT += o.FirstCastT; CastBattles += o.CastBattles; AkaDied += o.AkaDied; AkaDeathT += o.AkaDeathT; AkaTurns += o.AkaTurns;
            Fallout += o.Fallout; AtDeath += o.AtDeath; Gained += o.Gained; BFoe += o.BFoe; BAlly += o.BAlly; BSelf += o.BSelf; BDot += o.BDot; BHav += o.BHav; AkaToE += o.AkaToE;
            KadoN += o.KadoN; Str += o.Str; StrSum += o.StrSum; Bare += o.Bare; BareT += o.BareT; KDied += o.KDied; KDeathT += o.KDeathT;
            Add(LFoe, o.LFoe); Add(LAlly, o.LAlly); Add(LSelf, o.LSelf); Add(LDot, o.LDot); Add(LHav, o.LHav); Add(WSum, o.WSum); Add(PCnt, o.PCnt);
            Add(TK, o.TK); Add(TA, o.TA); Add(TO, o.TO); Add(THeal, o.THeal); Add(TCnt, o.TCnt); Add(HBefore, o.HBefore); Add(HAfter, o.HAfter); Add(HCnt, o.HCnt);
            Add(TCast, o.TCast); Add(TCastN, o.TCastN); Add(TBlood, o.TBlood); Add(TBloodC, o.TBloodC);
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
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
        var boss = e.FirstOrDefault(u => u.HasTrait(TraitId.BossRise11));
        // 勇者の始まりの HP は新しく立てた波から（戦の後の `MaxHp` はネルの呪われた傷などで削れている）。
        int bossHp = boss is null ? 0 : w.Make().FirstOrDefault(u => u.HasTrait(TraitId.BossRise11))?.MaxHp ?? boss.MaxHp;
        int last = r.Turns;
        string ashLabel = StatusKeys.LabelOf(StatusKeys.Ash);
        if (boss is not null) { a.BossN = 1; for (int t = 1; t <= Math.Min(tt, last); t++) a.TCnt[t] = 1; }
        for (int t = 1; t <= Math.Min(tt, last); t++) a.PCnt[t] = 1;
        var akaTurns = new HashSet<int>();
        foreach (var x in r.Events)
        {
            int tn = x.Turn;
            bool inT = tn >= 1 && tn <= tt && tn <= last;
            switch (x.Kind)
            {
                case BattleEventKind.TurnStart:
                    if (!inT) break;
                    foreach (var (id, u) in mine)
                        if (hp[id] > 0) a.WSum[tn] += Math.Max(0, u.MaxHp - hp[id]);
                    if (boss is not null && bossHp > 0) { a.HBefore[tn] += bossHp; a.HAfter[tn] += bossHp; a.HCnt[tn]++; }
                    break;
                case BattleEventKind.Damage:
                    if (x.TargetId is int ti && mine.TryGetValue(ti, out var tu))
                    {
                        int lost = Math.Max(0, hp[ti] - x.HpAfter);
                        hp[ti] = x.HpAfter;
                        if (!inT || lost == 0) break;
                        if (x.ActorId is null) a.LDot[tn] += lost;
                        else if (x.ActorId == ti) a.LSelf[tn] += lost;
                        else if (mine.ContainsKey(x.ActorId.Value)) a.LAlly[tn] += lost;
                        else a.LFoe[tn] += lost;
                    }
                    else if (boss is not null && x.TargetId == boss.InstanceId)
                    {
                        bossHp = x.HpAfter;
                        if (inT && x.ActorId is int bi && mine.ContainsKey(bi))
                        {
                            if (kado is not null && bi == kado.InstanceId) a.TK[tn] += x.Amount;
                            else if (aka is not null && bi == aka.InstanceId) a.TA[tn] += x.Amount;
                            else a.TO[tn] += x.Amount;
                        }
                    }
                    if (aka is not null && x.ActorId == aka.InstanceId && x.TargetId is int et && !mine.ContainsKey(et)) a.AkaToE += x.Amount;
                    break;
                case BattleEventKind.Heal:
                    if (x.TargetId is int hi && mine.ContainsKey(hi)) hp[hi] = x.HpAfter;
                    else if (boss is not null && x.TargetId == boss.InstanceId)
                    {
                        // 勇者の回復（ターン頭の `BossMendTrait` ほか）。「回復の後」はその回復が入った後の残りHP。
                        if (inT) { a.THeal[tn] += x.Amount; a.HAfter[tn] += x.HpAfter - bossHp; }
                        bossHp = x.HpAfter;
                    }
                    break;
                case BattleEventKind.StatusSnapshot:
                    if (aka is not null && x.TargetId == aka.InstanceId && x.Text == ashLabel && inT) { a.TBlood[tn] += x.Amount; }
                    break;
                case BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.Charge:
                    if (aka is not null && x.ActorId == aka.InstanceId) akaTurns.Add(tn);
                    break;
            }
        }
        // 血のスナップショットは 0 のとき出ないので、「アカが生きていたターン」を分母にする。
        if (aka is not null)
            for (int t = 1; t <= Math.Min(tt, last); t++)
                if (aka.LastDeathTurn == 0 || aka.LastDeathTurn >= t) a.TBloodC[t]++;
        foreach (var u in p)
            if (r.TallyByUnit.TryGetValue(u.Def.Id, out var t) && t.Sh363HavocLost is { } hv)
                for (int k = 1; k <= Math.Min(tt, last) && k < hv.Length; k++) a.LHav[k] += hv[k];
        if (aka is not null)
        {
            a.AkaN = 1; a.AkaTurns = akaTurns.Count(t => t >= 1);
            if (aka.LastDeathTurn > 0) { a.AkaDied = 1; a.AkaDeathT = aka.LastDeathTurn; }
            if (r.TallyByUnit.TryGetValue("susu", out var at))
            {
                a.Casts = at.Sh363Casts; a.CastSum = at.Sh363CastSum; a.Fallout = at.AshFalloutOut; a.AtDeath = at.AshAtDeath; a.Gained = at.AshGained;
                a.BFoe = at.Sh363BloodFoe; a.BAlly = at.Sh363BloodAlly; a.BSelf = at.Sh363BloodSelf; a.BDot = at.Sh363BloodDot; a.BHav = at.Sh363BloodHavoc;
                if (at.Sh363FirstCastTurn > 0) { a.CastBattles = 1; a.FirstCastT = at.Sh363FirstCastTurn; }
                if (at.Sh363CastByTurn is { } cb)
                    for (int k = 1; k <= tt && k < cb.Length; k++) if (cb[k] > 0) { a.TCast[k] += cb[k]; a.TCastN[k]++; }
                // 規定のアカ（`Ash`）は `NoteBloodCast` を通らないので、撒いた回数 ／ 量は灰の帳簿から。
                if (at.Sh363Casts == 0 && at.AshFires > 0) { a.Casts = at.AshFires; a.CastSum = at.AshSpent; }
            }
        }
        if (kado is not null)
        {
            a.KadoN = 1;
            if (r.TallyByUnit.TryGetValue("kado", out var k))
            {
                a.Str = k.Sh362Strikes; a.StrSum = k.Sh362StrikeSum;
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

    /// <summary>アカの一撃の1行。</summary>
    internal static string AkaLine(Kx a)
        => $"撃った {Per2(a.Casts, a.N)} 回・1回 {Per(a.CastSum, a.Casts)}・最初に撃った T {Per(a.FirstCastT, a.CastBattles)}（撃った戦 {Pct(a.CastBattles, a.N)}%）・敵へ与えた計 {Per(a.AkaToE, a.N)}"
         + $"・アカの手番 {Per2(a.AkaTurns, a.N)}・アカが倒れた {Pct(a.AkaDied, a.N)}%（T {Per(a.AkaDeathT, a.AkaDied)}）・倒れたとき抱えていた血 {Per(a.AtDeath, a.N)}・降りかかった血 {Per(a.Fallout, a.N)}"
         + (a.BFoe + a.BAlly + a.BSelf + a.BDot > 0 ? $"・溜めた血 {Per(a.Gained, a.N)}（敵 {Per(a.BFoe, a.N)} ／ 味方 {Per(a.BAlly, a.N)} ／ 自分 {Per(a.BSelf, a.N)} ／ 刻み {Per(a.BDot, a.N)}・うち惨禍 {Per(a.BHav, a.N)}）" : "")
         + $"・カドが剥いた {Pct(a.Bare, a.N)}%（T {Per(a.BareT, a.Bare)}）・殴りかかり {Per2(a.Str, a.N)} 回・1回 {Per(a.StrSum, a.Str)}・カドが倒れた {Pct(a.KDied, a.N)}%（T {Per(a.KDeathT, a.KDied)}）";

    /// <summary>勇者戦のターンごと（与ダメ ／ 回復 ／ 勇者の残りHP ／ アカの血 ／ 撃った量）。</summary>
    internal static void PrintTraj(string title, Kx a)
    {
        Console.WriteLine($"#### {title}（勝率 {F1(a.Win)}%・倒しT {a.WinTs}・全滅 T {Per(a.WipeT, a.Lost)}）");
        Console.WriteLine();
        Console.WriteLine("ターン t まで戦が続いた戦の平均。勇者の残りHP はターンの頭（回復の前）と、その回復が入った後（勇者が生きていた戦）。アカの血はターンの頭（アカが生きていた戦）。撃った量はそのターンに撃った戦の平均。");
        Console.WriteLine();
        Console.WriteLine("| T | 続いた戦 | 与ダメ 計 | うちカド | うちアカ | ほか | 勇者の回復 | 与ダメ − 回復 | 勇者 HP（回復の前 → 後） | アカの血 | アカが撃った量（撃った戦） |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|---|--:|---|");
        for (int t = 1; t <= a.T; t++)
        {
            long c = a.TCnt[t];
            if (c == 0) continue;
            long all = a.TK[t] + a.TA[t] + a.TO[t];
            Console.WriteLine($"| {t} | {c} | {Per(all, c)} | {Per(a.TK[t], c)} | {Per(a.TA[t], c)} | {Per(a.TO[t], c)} | {Per(a.THeal[t], c)} | {Per(all - a.THeal[t], c)} | {Per(a.HBefore[t], a.HCnt[t])} → {Per(a.HAfter[t], a.HCnt[t])} | {Per(a.TBlood[t], a.TBloodC[t])} | {Per(a.TCast[t], a.TCastN[t])}（{a.TCastN[t]}） |");
        }
        Console.WriteLine();
    }

    /// <summary>§3 の 1・2: 味方が受けた傷（ターンごと・出どころ別・累計）と今の傷の合計。</summary>
    static void PrintWounds(string title, Kx a)
    {
        Console.WriteLine($"#### {title}（勝率 {F1(a.Win)}%・決着T {a.WinTs}・全滅 T {Per(a.WipeT, a.Lost)}）");
        Console.WriteLine();
        Console.WriteLine("ターン t まで戦が続いた戦の平均（実際に減った HP・過剰分を除く・破片で受けた分は入らない）。累計は T1 からの和（続いた戦の平均の和）。今の傷の合計はターンの頭の生きている味方の 最大HP − 今のHP（治された分を引いた残り）。");
        Console.WriteLine();
        Console.WriteLine("| T | 続いた戦 | 敵から | 味方から | 自分から | 刻み | 計 | うち惨禍 | 累計 | 累計（惨禍） | 今の傷の合計 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        double cum = 0, cumH = 0;
        for (int t = 1; t <= a.T; t++)
        {
            long c = a.PCnt[t];
            if (c == 0) continue;
            long all = a.LFoe[t] + a.LAlly[t] + a.LSelf[t] + a.LDot[t];
            cum += (double)all / c; cumH += (double)a.LHav[t] / c;
            Console.WriteLine($"| {t} | {c} | {Per(a.LFoe[t], c)} | {Per(a.LAlly[t], c)} | {Per(a.LSelf[t], c)} | {Per(a.LDot[t], c)} | {Per(all, c)} | {Per(a.LHav[t], c)} | {F1(cum)} | {F1(cumH)} | {Per(a.WSum[t], c)} |");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3 Phase 0（規定・段0 入り）
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = new[] { I.Boss, I.Guard, I.Bat };
        Console.WriteLine($"# {Tag} §3 Phase 0（規定・段0 入り）—— 代表台 3 × ボス ／ 近衛 ／ 大隊・seed 0..{P0Seeds - 1}");
        Console.WriteLine();
        foreach (var b in P0Boards()) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        foreach (var b in P0Boards())
        {
            Console.WriteLine($"## {b.Name}");
            Console.WriteLine();
            foreach (var w in waves)
            {
                var a = Measure(b.F, w, P0Seeds);
                Console.WriteLine($"### × {w.Name}");
                Console.WriteLine();
                Console.WriteLine($"- {AkaLine(a)}");
                Console.WriteLine();
                PrintWounds($"{b.Name} × {w.Name}: 味方が受けた傷", a);
                if (w == I.Boss) PrintTraj($"{b.Name} × ボス: 勇者への与ダメと勇者の残りHP", a);
            }
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
