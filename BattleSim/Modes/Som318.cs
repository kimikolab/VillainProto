using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;
using S307 = Som307Diag;
using S309 = Som309Diag;
using S310 = Som310Diag;

// =====================================================================================
// som318 —— 第318期「段0: ソム BD-E2 の規定化 ／ 本体: 裏切りの雷の共鳴返し（CR-a ／ CR-b）＋ シガの打ち返し（SG）」。
// 指示書は design/PHASE318_SOM_RESONANCE_SPEC.md ／ 報告は design/PHASE318_SOM_RESONANCE.md。
//
//     dotnet run --project BattleSim -c Release 0 som318 p0          # Phase 0（§4）: BT-a（新しい規定の上）の裏切りの雷の相手の内訳（帯電していた ／ いない × 駒）・シガ ／ クグ・粛・獣の隣の敵
//     dotnet run --project BattleSim -c Release 0 som318 boards [版,…]  # §5-2: 代表台 7 × 10 波 × 版（規定 ／ BT-a ／ CR-a ／ CR-b ／ CR-a+SG ／ CR-b+SG）
//     dotnet run --project BattleSim -c Release 0 som318 compare [版,…] # §5-3: `compare` 64 行 × 版
//     dotnet run --project BattleSim -c Release 0 som318 grid <版> <boss|guard|bat>   # §5-4: ソムの格子（第311期と同じ作り・駒は規定）
//     dotnet run --project BattleSim -c Release 0 som318 noise <版> <台の一部> <波> <from> <n>   # 別の seed の帯で規定と版を並べる
//     dotnet run --project BattleSim -c Release 0 som318 find        # §6: 共鳴返し ／ 打ち返しのある seed（台本の例）
//     dotnet run --project BattleSim -c Release 0 som318 memo <版> <台の一部> <boss|guard|bat|nine|nine2|1..5> <seed> <T0> <T1>
//     dotnet run --project BattleSim -c Release 0 som318 check       # 自己検査（§7）
//
// **予測のファイルが無ければ本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p318/predict.md`）。
// 台の駒は第318期の規定（第319期に `Common.Pin318` で固定した: ソム → `SomH318`・シガ → `ShigaH318`）。
// 版の名前はコンソールの文字コードを避けて ASCII で引ける（`base` ＝ 規定・`CR-a+SG` ほか）。
// =====================================================================================
static class Som318Diag
{
    const string PredictFile = ".tmp/p318/predict.md";
    const int Seeds = S307.Seeds;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"som318: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "compare": CompareAll(A(3, "")); return;
            case "grid": Grid(A(3, "CR-b"), A(4, "boss")); return;
            case "find": Find(); return;
            case "memo": Memo(A(3, "CR-b"), A(4, "光の盾"), A(5, "boss"), int.Parse(A(6, "0")), int.Parse(A(7, "1")), int.Parse(A(8, "5"))); return;
            case "noise": Noise(A(3, "CR-b"), A(4, "光の盾"), A(5, "2"), int.Parse(A(6, "200")), int.Parse(A(7, "800"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("som318: モードは p0 / boards / compare / grid / noise / find / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Per1(long a, long n) => S307.Per1(a, n);
    static string Per2(long a, long n) => S307.Per(a, n);
    static string Per0(long a, long n) => S307.Per0(a, n);
    static string Pct(long a, long n) => S307.Pct(a, n);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";

    /// <summary>版（ソムの版 ＋ シガの版）。シガの版が null なら規定のシガのまま。</summary>
    internal sealed record Ver(string Name, UnitDef Som, UnitDef? Shiga = null);

    internal static readonly Ver[] Vers =
    {
        new("規定", UnitCatalog.SomH318),
        new("BT-a", UnitCatalog.SomBT318),
        new("CR-a", UnitCatalog.SomCRa),
        new("CR-b", UnitCatalog.SomCRb),
        new("CR-a+SG", UnitCatalog.SomCRa, UnitCatalog.ShigaSG),
        new("CR-b+SG", UnitCatalog.SomCRb, UnitCatalog.ShigaSG),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || (n == "base" && v.Name == "規定"));   // "base" ＝ 規定（コンソールの文字コードを避ける別名）
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { V("規定") }.Concat(only.Split(',').Where(x => x is not ("規定" or "base")).Select(V))).ToArray();

    internal static Formation Apply(Formation f, Ver v)
    {
        var g = ReferenceEquals(v.Som, UnitCatalog.SomH318) ? f : S309.Swap(f, UnitCatalog.SomH318, v.Som);
        return v.Shiga is null ? g : S309.Swap(g, UnitCatalog.ShigaH318, v.Shiga);
    }

    internal static readonly S287.Wave[] Waves = S310.Waves;
    static S287.Wave WaveOf(string n) => n switch { "nine" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };

    static Formation Playtest(string n) => PlaytestH318.First(r => r.Name == n).F;
    static Formation RowF(string n) => CompareBuildsH318().First(r => r.Name == n).F;
    /// <summary>第317期までの器具（`S307.Order` ／ `S307.BossPool` ほか）は第317〜318期にドハ ／ シガ ／ ソムを旧に固定したので、この器具は規定の駒に戻して使う。</summary>
    /// 第319期: ソム ／ シガの規定が動いたので、戻す先を第318期の規定（`SomH318` ／ `ShigaH318`）にした。
    static UnitDef Cur(UnitDef d) => ReferenceEquals(d, UnitCatalog.ShigaSWa) ? UnitCatalog.ShigaH318 : ReferenceEquals(d, UnitCatalog.DohaH316) ? UnitCatalog.Doha : ReferenceEquals(d, UnitCatalog.SomH317) ? UnitCatalog.SomH318 : d;
    static Formation WinSeat() => B283.Seat(S307.Order(S307.WinBoard).Select(Cur).ToArray());
    internal const string HagiBoard = "光の盾 クビ→ハギ";

    /// <summary>§5-2 の代表台（規定の駒・席は出典のまま・第317期と同じ7台）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("光の盾", Playtest(S309.Shield)),
        ("光の盾 鞭", Playtest(Som312Diag.Whip)),
        ("光の盾 重 鞭", Playtest(Som312Diag.WhipHeavy)),
        ("感電 行", RowF("感電 (シガ×カタ×ソム)")),
        ("勝ち台 ツギ→ソム", S309.Swap(WinSeat(), UnitCatalog.Tsugi, UnitCatalog.SomH318)),
        ("火の型 ソラ→ソム", S309.Swap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, UnitCatalog.SomH318)),
        (HagiBoard, S309.Swap(Playtest(S309.Shield), UnitCatalog.Kubi, UnitCatalog.Hagi)),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose の台本 ＋ 帳簿）
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, LoseT, Caps, Turns, FirstDeathN, FirstDeathT, SomDied, SomDeathT;
        public Dictionary<string, long> FoeDmg = new();
        public long FoeHeal;
        // 獣（帳簿）
        public long Calls, Called, Charged, NoSeat, NoFoe, Stood, Bursts, BurstUnits, EmergFired;
        public int AlivePeak;
        public long PeakSum, AliveSum, AliveN;
        // 獣（台本）
        public long StandLife, StandLifeN, AtkOnStand, DmgOnStand, StandKilled, StandRes, StandSg, StandBdPop, StandAtEnd;
        // 裏切りの雷（帳簿）
        public long Thrown, NoTarget, ThrowShocked, ThrowAlready, ThrowThread, ThrowThreadShocked, AtCharged, AtChargedHush, ThreadCharged, AdjFoes, AdjFoesDry;
        public long Res, ResHushed, ResDry, ResChain, ArcFoes, ArcShocked, SgFired, SgHushed, SgBlocked, SgNested, SgDealt;
        // 裏切りの雷（台本）: 相手の駒 ／ うち帯電していた（糸の分はクグの帯電が台本に出ないので帳簿の `ThreadCharged`）
        public Dictionary<string, long> ThrowTo = new(), ThrowToCharged = new();
        public long ShigaDry, ShigaHit, KuguThrows, SgAfterWhip;
        // 獣の死
        public long BdCounted, BdOnKill, BdOnAnyDeath, BdSkipped;
        public long[] BdKind = new long[5];
        public long HagiAtk, HagiDmg, HagiSelf, HagiDied, HagiN, HagiRecoil, HagiTopTurn, TsugiFalls, TsugiN;
        // シガ・味方の感電・光・カタ
        public long ShigaTurns, ShigaStalls, ShigaN, SwFires, AllyPops, AllyStuns, Lights, VeilAdded, KataCloud, KataN;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long FoeDmgAll => FoeDmg.Values.Sum();
        public void Merge(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; Turns += o.Turns; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; SomDied += o.SomDied; SomDeathT += o.SomDeathT;
            foreach (var (k, v) in o.FoeDmg) FoeDmg[k] = FoeDmg.GetValueOrDefault(k) + v;
            FoeHeal += o.FoeHeal;
            Calls += o.Calls; Called += o.Called; Charged += o.Charged; NoSeat += o.NoSeat; NoFoe += o.NoFoe; Stood += o.Stood; Bursts += o.Bursts; BurstUnits += o.BurstUnits; EmergFired += o.EmergFired;
            AlivePeak = Math.Max(AlivePeak, o.AlivePeak); PeakSum += o.PeakSum; AliveSum += o.AliveSum; AliveN += o.AliveN;
            StandLife += o.StandLife; StandLifeN += o.StandLifeN; AtkOnStand += o.AtkOnStand; DmgOnStand += o.DmgOnStand; StandKilled += o.StandKilled; StandRes += o.StandRes; StandSg += o.StandSg; StandBdPop += o.StandBdPop; StandAtEnd += o.StandAtEnd;
            Thrown += o.Thrown; NoTarget += o.NoTarget; ThrowShocked += o.ThrowShocked; ThrowAlready += o.ThrowAlready; ThrowThread += o.ThrowThread; ThrowThreadShocked += o.ThrowThreadShocked;
            AtCharged += o.AtCharged; AtChargedHush += o.AtChargedHush; ThreadCharged += o.ThreadCharged; AdjFoes += o.AdjFoes; AdjFoesDry += o.AdjFoesDry;
            Res += o.Res; ResHushed += o.ResHushed; ResDry += o.ResDry; ResChain += o.ResChain; ArcFoes += o.ArcFoes; ArcShocked += o.ArcShocked;
            SgFired += o.SgFired; SgHushed += o.SgHushed; SgBlocked += o.SgBlocked; SgNested += o.SgNested; SgDealt += o.SgDealt;
            foreach (var (k, v) in o.ThrowTo) ThrowTo[k] = ThrowTo.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.ThrowToCharged) ThrowToCharged[k] = ThrowToCharged.GetValueOrDefault(k) + v;
            ShigaDry += o.ShigaDry; ShigaHit += o.ShigaHit; KuguThrows += o.KuguThrows; SgAfterWhip += o.SgAfterWhip;
            BdCounted += o.BdCounted; BdOnKill += o.BdOnKill; BdOnAnyDeath += o.BdOnAnyDeath; BdSkipped += o.BdSkipped;
            for (int i = 0; i < 5; i++) BdKind[i] += o.BdKind[i];
            HagiAtk += o.HagiAtk; HagiDmg += o.HagiDmg; HagiSelf += o.HagiSelf; HagiDied += o.HagiDied; HagiN += o.HagiN; HagiRecoil += o.HagiRecoil; HagiTopTurn = Math.Max(HagiTopTurn, o.HagiTopTurn);
            TsugiFalls += o.TsugiFalls; TsugiN += o.TsugiN;
            ShigaTurns += o.ShigaTurns; ShigaStalls += o.ShigaStalls; ShigaN += o.ShigaN; SwFires += o.SwFires; AllyPops += o.AllyPops; AllyStuns += o.AllyStuns; Lights += o.Lights; VeilAdded += o.VeilAdded;
            KataCloud += o.KataCloud; KataN += o.KataN;
        }
    }

    static bool IsThrowLabel(string? t) => t is BeastThrowLabels.Hit or BeastThrowLabels.Thread or BeastThrowLabels.Rod or BeastThrowLabels.Resonance or BeastThrowLabels.Hushed;

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        return Count(p, e, r);
    }

    /// <summary>1戦の計数（第319期に <see cref="Fight"/> から切り出した。中身は1文字も変えていない——`som319` が同じ戦の台本で別の計数も取るため）。</summary>
    internal static St Count(List<UnitState> p, List<UnitState> e, BattleResult r)
    {
        var a = new St { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else { a.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) a.Caps = 1; }
        var mine = p.ToDictionary(u => u.InstanceId, u => u);
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), shiga = p.FirstOrDefault(u => u.Def.Id == "shiga"), kata = p.FirstOrDefault(u => u.Def.Id == "kata"),
                   hagi = p.FirstOrDefault(u => u.Def.Id == "hagi"), kugu = p.FirstOrDefault(u => u.Def.Id == "kugu"), tsugi = p.FirstOrDefault(u => u.Def.Id == "tsugi");
        // 1巡目: 喚んだ獣 ／ 置物の獣（その場で弾けた）。盤に立っていた獣を消す `Text`（暴発 ／ 避雷針 ／ 弾けて消えた ／ 共鳴返し）は置物に数えない。
        var summoned = new HashSet<int>(); var phantom = new HashSet<int>();
        foreach (var ev in r.Events)
        {
            if (ev.Kind == BattleEventKind.Summon && som is not null && ev.ActorId == som.InstanceId && ev.TargetId is int sid) summoned.Add(sid);
            else if (ev.Kind == BattleEventKind.BeastBurst && ev.Text is not (BeastBurstLabels.Overflow or BeastBurstLabels.Rod or BeastBurstLabels.Popped or BeastBurstLabels.Resonance) && ev.TargetId is int bid) phantom.Add(bid);
        }
        var standing = new Dictionary<int, int>();   // 立っている獣 → 立ったT
        int peak = 0;
        int? swingAt = null;                          // 打ち返しの鞭の的（その鞭で弾けた獣を「打ち返しで弾けた」に数える）
        var hagiPerTurn = new Dictionary<int, int>();
        void Leave(int id, int turn) { if (standing.Remove(id, out int t0)) { a.StandLife += turn - t0; a.StandLifeN++; } }
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Summon when ev.TargetId is int sid && summoned.Contains(sid) && !phantom.Contains(sid):
                    standing[sid] = ev.Turn;
                    if (standing.Count > peak) peak = standing.Count;
                    break;
                case BattleEventKind.BeastBurst when ev.Text == BeastBurstLabels.Resonance && ev.TargetId is int rb && standing.ContainsKey(rb):
                    a.StandRes++; Leave(rb, ev.Turn); break;
                case BattleEventKind.BeastBurst when ev.Text == BeastBurstLabels.Popped && ev.TargetId is int pb0 && standing.ContainsKey(pb0):
                    if (swingAt == pb0) a.StandSg++; else a.StandBdPop++;
                    Leave(pb0, ev.Turn); break;
                case BattleEventKind.BeastBurst when ev.Text is BeastBurstLabels.Overflow or BeastBurstLabels.Rod && ev.TargetId is int ob && standing.ContainsKey(ob):
                    Leave(ob, ev.Turn); break;
                case BattleEventKind.Death when ev.TargetId is int t && mine.ContainsKey(t):
                    if (a.FirstDeathN == 0) { a.FirstDeathN = 1; a.FirstDeathT = ev.Turn; }
                    if (som is not null && t == som.InstanceId && a.SomDied == 0) { a.SomDied = 1; a.SomDeathT = ev.Turn; }
                    if (hagi is not null && t == hagi.InstanceId) a.HagiDied = 1;
                    break;
                case BattleEventKind.Death when ev.TargetId is int db && standing.ContainsKey(db):
                    if (swingAt == db) a.StandSg++; else a.StandKilled++;
                    Leave(db, ev.Turn); break;
                case BattleEventKind.BeastThrow when ev.Text == BeastThrowLabels.Backswing:
                    swingAt = ev.ActorId; break;
                case BattleEventKind.BeastThrow when IsThrowLabel(ev.Text) && ev.TargetId is int tt && mine.TryGetValue(tt, out var tu):
                    {
                        swingAt = null;
                        string k = Short(tu.Def);
                        a.ThrowTo[k] = a.ThrowTo.GetValueOrDefault(k) + 1;
                        bool chargedNow = ev.Text is BeastThrowLabels.Resonance or BeastThrowLabels.Hushed || (ev.Text == BeastThrowLabels.Hit && ev.Amount == 0);
                        if (chargedNow) a.ThrowToCharged[k] = a.ThrowToCharged.GetValueOrDefault(k) + 1;
                        if (shiga is not null && tt == shiga.InstanceId && ev.Text == BeastThrowLabels.Hit) { a.ShigaHit++; if (ev.Amount == 1) a.ShigaDry++; }
                        if (kugu is not null && tt == kugu.InstanceId) a.KuguThrows++;
                        break;
                    }
                case BattleEventKind.Attack when ev.ActorId is int at && mine.ContainsKey(at):
                    if (ev.TargetId is int tb2 && standing.ContainsKey(tb2)) a.AtkOnStand++;
                    if (hagi is not null && at == hagi.InstanceId) a.HagiAtk++;
                    break;
                case BattleEventKind.Damage when ev.TargetId is int dt && standing.ContainsKey(dt):
                    if (ev.ActorId is int da && mine.ContainsKey(da)) a.DmgOnStand += ev.Amount;
                    break;
                case BattleEventKind.Damage when hagi is not null && ev.TargetId == hagi.InstanceId && ev.ActorId is null && ev.FriendlyFire:
                    a.HagiSelf += ev.Amount; break;
                case BattleEventKind.Damage when ev.TargetId is int ft && foes.Contains(ft):
                    {
                        string k = ev.ActorId is int ka && mine.TryGetValue(ka, out var ku) ? Short(ku.Def) : ev.ActorId is int kb && summoned.Contains(kb) ? "獣の放電" : ev.ActorId is null ? "刻み" : "敵の側";
                        a.FoeDmg[k] = a.FoeDmg.GetValueOrDefault(k) + ev.Amount;
                        if (hagi is not null && ev.ActorId == hagi.InstanceId) a.HagiDmg += ev.Amount;
                        break;
                    }
                case BattleEventKind.Heal when ev.TargetId is int ht && foes.Contains(ht): a.FoeHeal += ev.Amount; break;
                case BattleEventKind.Highlight when hagi is not null && ev.ActorId == hagi.InstanceId: hagiPerTurn[ev.Turn] = hagiPerTurn.GetValueOrDefault(ev.Turn) + 1; break;
            }
        }
        foreach (var (_, k) in hagiPerTurn) { a.HagiRecoil += Math.Max(0, k - 1); if (k > a.HagiTopTurn) a.HagiTopTurn = k; }
        foreach (var (_, t0) in standing) { a.StandLife += r.Turns - t0; a.StandLifeN++; a.StandAtEnd++; }
        a.PeakSum = peak;
        if (som is not null && r.TallyByUnit.TryGetValue("som", out var mt))
        {
            a.Calls = mt.BeastCalls; a.Called = mt.StandBeasts; a.Charged = mt.StandCharged; a.NoSeat = mt.StandNoSeat; a.NoFoe = mt.StandNoFoe; a.Stood = mt.StandStood;
            a.AliveSum = mt.StandAliveSum; a.AliveN = mt.StandAliveN; a.AlivePeak = mt.StandAlivePeak;
            a.Bursts = mt.BeastBursts; a.BurstUnits = mt.BeastBurstUnits; a.EmergFired = mt.EmergFired;
            a.Thrown = mt.BtThrown; a.NoTarget = mt.BtNoTarget; a.ThrowShocked = mt.BtShocked; a.ThrowAlready = mt.BtAlready; a.ThrowThread = mt.BtThread; a.ThrowThreadShocked = mt.BtThreadShocked;
            a.AtCharged = mt.BtAtCharged; a.AtChargedHush = mt.BtAtChargedHush; a.ThreadCharged = mt.BtThreadCharged; a.AdjFoes = mt.BtAdjFoes; a.AdjFoesDry = mt.BtAdjFoesDry;
            a.Res = mt.CrFired; a.ResHushed = mt.CrHushed; a.ResDry = mt.CrDry; a.ResChain = mt.CrChainUnits; a.ArcFoes = mt.CrArcFoes; a.ArcShocked = mt.CrArcShocked;
            a.SgFired = mt.SgFired; a.SgHushed = mt.SgHushed; a.SgBlocked = mt.SgBlocked; a.SgNested = mt.SgNested; a.SgDealt = mt.SgDealt; a.SgAfterWhip = mt.SgWhipNested;
            a.BdCounted = mt.BdCounted; a.BdOnKill = mt.BdOnKill; a.BdOnAnyDeath = mt.BdOnAnyDeath; a.BdSkipped = mt.BdSkipped;
            if (mt.BdKind is { } bk) for (int i = 0; i < Math.Min(5, bk.Length); i++) a.BdKind[i] = bk[i];
            a.Lights = mt.SparkRainLights; a.VeilAdded = mt.SparkVeilAdded;
        }
        if (kata is not null) { a.KataN = 1; a.KataCloud = ThundercloudTrait.Of(kata); }
        if (hagi is not null) a.HagiN = 1;
        if (tsugi is not null && r.TallyByUnit.TryGetValue("tsugi", out var tt2)) { a.TsugiN = 1; a.TsugiFalls = tt2.ScrapFalls; }
        if (shiga is not null && r.TallyByUnit.TryGetValue("shiga", out var st)) { a.ShigaN = 1; a.ShigaTurns = st.TurnsTaken; a.ShigaStalls = st.TurnStalls; a.SwFires = st.SwFires; }
        foreach (var id in p.Select(u => u.Def.Id).Distinct())
            if (r.TallyByUnit.TryGetValue(id, out var ut)) { a.AllyPops += ut.ShockSpent; a.AllyStuns += ut.ShockStunned; }
        return a;
    }

    internal static St Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, from + i));
        var all = new St();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static int WinsLite(Formation f, S287.Wave w, int n, int from = 0)
    {
        int wins = 0;
        Parallel.For(from, from + n, s =>
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
            if (r.PlayerWon) Interlocked.Increment(ref wins);
        });
        return wins;
    }

    static string FoeDmgLine(St a, Formation f)
    {
        var order = f.Occupied().Select(o => Short(o.Def)).Distinct().Append("獣の放電").Append("刻み").Append("敵の側");
        return string.Join("・", order.Where(k => a.FoeDmg.GetValueOrDefault(k) > 0).Select(k => $"{k} {Per0(a.FoeDmg[k], a.Turns)}"));
    }

    static string ThrowSplit(St a, int k = 4)
    {
        long tot = a.ThrowTo.Values.Sum();
        if (tot == 0) return "—";
        return string.Join("・", a.ThrowTo.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Take(k).Select(x =>
        {
            long c = x.Key == "クグ" ? -1 : a.ThrowToCharged.GetValueOrDefault(x.Key);
            return $"{x.Key} {Pct(x.Value, tot).Replace("%", "")}%" + (c >= 0 ? $"（帯電 {Pct(c, x.Value).Replace("%", "")}%）" : "");
        }));
    }

    // ---------------------------------------------------------------------------------
    // Phase 0（§4）: BT-a（新しい規定 ＝ BD の上）を計数器つきで回す
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var bt = V("BT-a");
        var cells = new List<(string B, Formation F, S287.Wave W, St S)>();
        foreach (var (n, f) in boards) { var fv = Apply(f, bt); foreach (var w in Waves) cells.Add((n, fv, w, Measure(fv, w))); }
        Console.WriteLine($"# 第318期 Phase 0（§4）—— BT-a（規定 ＝ ソム E2 ＋ 群れ ＋ 緊急 K-a ＋ 萎縮 ＋ BD の上に 立つ ＋ 投げる）・seed 0..{Seeds - 1}・verbose");
        Console.WriteLine();
        foreach (var (n, f) in boards) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("## §4-1〜4-4 投げた雷の相手（/戦）");
        Console.WriteLine();
        Console.WriteLine("「帯電していた」＝ 雷が向かった時点で味方に感電が残っていた（帳簿 `BtAtCharged`・糸の保持者に向かった分も含む）＝ **CR-a なら跳ね返る見込み**。相手の駒の括弧は台本の「既に感電」（クグは糸へ流れるので台本に帯電が出ない——帳簿の糸の列で）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 投げた | **帯電していた（割合）** | うち粛が効いていた | 相手（上位4・帯電の割合） | シガに落ちた ／ うち帯電していない（SG の見込み） | クグに向かった ／ 糸 ／ うち帯電していた | 投げた獣の隣の敵 /投 ／ うち帯電していない（CR-b の見込み） |");
        Console.WriteLine("|---|---|--:|--:|---|--:|---|---|---|---|");
        foreach (var c in cells)
        {
            var s = c.S;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {F1(s.Win)} | {Per1(s.Thrown, s.N)} | **{Per1(s.AtCharged, s.N)}（{Pct(s.AtCharged, s.Thrown)}）** | {Per1(s.AtChargedHush, s.N)} | {ThrowSplit(s)} | {Per1(s.ShigaHit, s.N)} ／ {Per1(s.ShigaDry, s.N)} | {Per1(s.KuguThrows, s.N)} ／ {Per1(s.ThrowThread, s.N)} ／ {Per1(s.ThreadCharged, s.N)} | {Per2(s.AdjFoes, s.Thrown)} ／ {Per2(s.AdjFoesDry, s.Thrown)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 台ごとの和（10 波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 投げた /戦 | 帯電していた | 粛の下で帯電していた | シガ 帯電していない | 糸 | 隣の敵 ／ うち帯電していない（/投） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|");
        foreach (var (n, _) in boards)
        {
            var s = new St(); foreach (var c in cells.Where(x => x.B == n)) s.Merge(c.S);
            long nn = s.N / Waves.Length;
            Console.WriteLine($"| {n} | {Per1(s.Thrown, nn)} | {Pct(s.AtCharged, s.Thrown)} | {Per1(s.AtChargedHush, nn)} | {Per1(s.ShigaDry, nn)} | {Per1(s.ThrowThread, nn)} | {Per2(s.AdjFoes, s.Thrown)} ／ {Per2(s.AdjFoesDry, s.Thrown)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5-2 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var cells = new List<(string B, Formation F0, S287.Wave W, Ver V, St S)>();
        foreach (var (n, f) in Boards()) foreach (var v in vers) { var fv = Apply(f, v); foreach (var w in Waves) cells.Add((n, f, w, v, Measure(fv, w))); }
        var ctl = new Dictionary<(string, string, string), string>();
        foreach (var c in cells.Where(c => c.S.Win >= 50))
        {
            var fv = Apply(c.F0, c.V);
            ctl[(c.B, c.W.Name, c.V.Name)] = F1(100.0 * WinsLite(S309.Swap(fv, c.V.Som, UnitCatalog.Dolga), c.W, Seeds) / Seeds);
        }

        Console.WriteLine($"# 第318期 §5-2 代表台 × 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}・seed 0..{Seeds - 1}・verbose）");
        Console.WriteLine();
        foreach (var (n, f) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("## 表A 勝率（括弧は規定との差・[ ] は ソム → ドルガ・勝率 50% 以上のセル）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var (n, _) in Boards())
            foreach (var v in vers)
                Console.WriteLine($"| {n} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var c = cells.First(x => x.B == n && x.V == v && x.W == w);
                    var b = cells.First(x => x.B == n && x.V == vers[0] && x.W == w);
                    return $"{F1(c.S.Win)}{(v == vers[0] ? "" : $"（{Sg(c.S.Win - b.S.Win)}）")}{(ctl.TryGetValue((n, w.Name, v.Name), out var k) ? $" [{k}]" : "")}";
                })) + " |");

        Console.WriteLine();
        Console.WriteLine("## 表B 倒しT ／ 負けT（上限の割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (n, _) in Boards())
            foreach (var v in vers)
                Console.WriteLine($"| {n} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var a = cells.First(x => x.B == n && x.V == v && x.W == w).S;
                    return $"{Per1(a.WinT, a.Wins)} ／ {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（{Pct(a.Caps, a.N)}）" : "")}";
                })) + " |");

        Console.WriteLine();
        Console.WriteLine("## 表C 裏切りの雷と獣（10 波の和・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 投げた | 跳ね返った（粛で止まった）| **跳ね返った割合** | 感電が付いた | 糸 | 打ち返し（止まった: 粛 ／ 痺れほか） | 立った | 跳ね返りで弾けた | 打ち返しで弾けた | ほかで弾けた ／ 倒された | 立ったまま残った | 立った獣への一撃 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|---|--:|--:|--:|---|--:|--:|");
        foreach (var (n, _) in Boards())
            foreach (var v in vers.Skip(1))
            {
                var a = new St(); foreach (var c in cells.Where(x => x.B == n && x.V == v)) a.Merge(c.S);
                long nn = a.N / Waves.Length;
                Console.WriteLine($"| {n} | {v.Name} | {Per1(a.Thrown, nn)} | {Per1(a.Res, nn)}（{Per1(a.ResHushed, nn)}） | **{Pct(a.Res, a.Thrown)}** | {Per1(a.ThrowShocked, nn)} | {Per1(a.ThrowThread, nn)} | {Per1(a.SgFired, nn)}（{Per1(a.SgHushed, nn)} ／ {Per1(a.SgBlocked + a.SgNested, nn)}） | {Per1(a.Stood, nn)} | {Per1(a.StandRes, nn)} | {Per1(a.StandSg, nn)} | {Per1(a.StandBdPop, nn)} ／ {Per1(a.StandKilled, nn)} | {Per1(a.StandAtEnd, nn)} | {Per1(a.AtkOnStand, nn)} |");
            }

        foreach (var w in Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 寿命 | ソム 倒れたT | 獣 喚んだ ・ その場で弾けた ・ **立った** ・ 跳ね返りで弾けた ・ 打ち返しで弾けた ・ ほかで弾けた ・ 倒された ・ 残った | 同時に立つ 最大 ／ 戦の最大の平均 ／ 喚び出し後の平均 | 立っていたT | 立った獣への一撃 ・ 量 | 投げた ・ 跳ね返った（粛）・ 獣が乾いていた ・ 感電が付いた ／ 既に ・ 糸（付いた） | 相手（上位4・帯電の割合） | CR-b 走った敵 ／ 新しく感電 ・ 跳ね返りの連鎖の駒 /回 | SG 打ち返し（粛 ／ 痺れほか）・ 与ダメ /回 ・ その後の割り込みが入れ子で止まった | 獣の死 数えた（場 ／ 緊急 ／ 立ち ／ 針 ／ 共鳴）・ハギ 追い打ち（1T の最多）・ 与ダメ ・ 倒れた ・ ツギ 瓦礫 | シガ 手番 ／ 失った | 味方の感電 弾けた ／ 痺れた | カタ 雷雲 | 光 ・ 衣 |"
                + (w.Boss ? " 味方の与ダメ /T ・ 勇者の回復 /T ・ 差 |" : ""));
            Console.WriteLine("|---|---|--:|---|---|---|---|--:|---|---|---|---|---|---|---|---|--:|---|" + (w.Boss ? "---|" : ""));
            foreach (var c in cells.Where(x => x.W == w))
            {
                var a = c.S;
                long spot = a.Called == 0 ? a.Bursts : a.Charged + a.NoSeat + a.NoFoe;
                string beast = $"{Per1(a.Called == 0 ? a.Calls : a.Called, a.N)} ・ {Per1(spot, a.N)} ・ **{Per1(a.Stood, a.N)}** ・ {Per1(a.StandRes, a.N)} ・ {Per1(a.StandSg, a.N)} ・ {Per1(a.StandBdPop, a.N)} ・ {Per1(a.StandKilled, a.N)} ・ {Per1(a.StandAtEnd, a.N)}";
                string conc = a.AliveN == 0 ? "—" : $"{a.AlivePeak} ／ {Per1(a.PeakSum, a.N)} ／ {Per1(a.AliveSum, a.AliveN)}";
                string th = a.Thrown == 0 ? "—" : $"{Per1(a.Thrown, a.N)} ・ {Per1(a.Res, a.N)}（{Per1(a.ResHushed, a.N)}）・ {Per1(a.ResDry, a.N)} ・ {Per1(a.ThrowShocked, a.N)} ／ {Per1(a.ThrowAlready, a.N)} ・ {Per1(a.ThrowThread, a.N)}（{Per1(a.ThrowThreadShocked, a.N)}）";
                string arc = a.ArcFoes == 0 && a.Res == 0 ? "—" : $"{Per1(a.ArcFoes, a.N)} ／ {Per1(a.ArcShocked, a.N)} ・ {Per1(a.ResChain, a.Res - a.ResDry)}";
                string sg = a.SgFired + a.SgHushed + a.SgBlocked + a.SgNested == 0 ? "—" : $"{Per1(a.SgFired, a.N)}（{Per1(a.SgHushed, a.N)} ／ {Per1(a.SgBlocked + a.SgNested, a.N)}）・ {Per0(a.SgDealt, a.SgFired)} ・ {Per1(a.SgAfterWhip, a.N)}";
                string bd = (a.BdCounted == 0 ? "—" : $"{Per1(a.BdCounted, a.N)}（{string.Join(" ／ ", a.BdKind.Select(x => Per1(x, a.N)))}）")
                    + (a.HagiN > 0 ? $"・ {Per1(a.HagiAtk, a.N)}（{a.HagiTopTurn}）・ {Per0(a.HagiDmg, a.N)} ・ {Pct(a.HagiDied, a.N)}" : "")
                    + (a.TsugiN > 0 ? $"・ ツギ {Per1(a.TsugiFalls, a.TsugiN)}" : "");
                var fv = Apply(c.F0, c.V);
                Console.WriteLine($"| {c.B} | {c.V.Name} | {F1(a.Win)} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Dt(a.SomDied, a.SomDeathT, a.N)} | {beast} | {conc} | {Per1(a.StandLife, a.StandLifeN)} | {Per1(a.AtkOnStand, a.N)} ・ {Per0(a.DmgOnStand, a.N)} | {th} | {ThrowSplit(a)} | {arc} | {sg} | {bd} | "
                    + $"{(a.ShigaN > 0 ? $"{Per1(a.ShigaTurns, a.ShigaN)} ／ {Per1(a.ShigaStalls, a.ShigaN)}" : "—")} | {Per1(a.AllyPops, a.N)} ／ {Per1(a.AllyStuns, a.N)} | {(a.KataN > 0 ? Per1(a.KataCloud, a.KataN) : "—")} | {Per1(a.Lights, a.N)} ・ {Per0(a.VeilAdded, a.N)} |"
                    + (w.Boss ? $" {Per0(a.FoeDmgAll, a.Turns)}（{FoeDmgLine(a, fv)}）・ {Per0(a.FoeHeal, a.Turns)} ・ {Sg((double)(a.FoeDmgAll - a.FoeHeal) / Math.Max(1, a.Turns))} |" : ""));
            }
        }

        Console.WriteLine();
        Console.WriteLine("## §5-4 格子を回す版（代表台で規定から ±10pt 以上・ボスは ±5pt 以上。落ちる向きも数える）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
            foreach (var wn in new[] { "ボス", "近衛", "大隊" })
            {
                var moved = new List<string>();
                foreach (var c in cells.Where(x => x.V == v && (wn == "ボス" ? x.W.Boss : x.W.Name == wn)))
                {
                    double d = c.S.Win - cells.First(x => x.B == c.B && x.V == vers[0] && x.W == c.W).S.Win;
                    if (Math.Abs(d) >= (c.W.Boss ? 5 : 10)) moved.Add($"{c.B}（{Sg(d)}）");
                }
                Console.WriteLine($"- {v.Name} × {wn}: {(moved.Count == 0 ? "**該当なし**" : $"**該当**（{string.Join("・", moved)}）")}");
            }
        Console.WriteLine();
        Console.WriteLine("（九体 ／ 本編の波は格子が無いので、門には入れずに表だけ見る。）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5-3 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var rows = CompareBuildsH318().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var grids = vers.Select(v =>
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, v);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }).ToArray();
        Console.WriteLine($"# 第318期 §5-3 `compare` {rows.Length} 行 × 版（seed 0..{Seeds - 1}・基準は規定）");
        double M(double[,] g, int ri) => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
        for (int vi = 1; vi < vers.Length; vi++)
        {
            var v = vers[vi]; var g = grids[vi]; var b = grids[0];
            Console.WriteLine();
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|");
            int movedRows = 0, movedCells = 0, outside = 0;
            var drops = new List<int>();
            for (int ri = 0; ri < rows.Length; ri++)
            {
                bool any = Enumerable.Range(0, nw).Any(w => g[ri, w] != b[ri, w]);
                bool holds = rows[ri].F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.SomH318));
                if (any && !holds) outside++;
                if (!any) continue;
                movedRows++;
                movedCells += Enumerable.Range(0, nw).Count(w => g[ri, w] != b[ri, w]);
                double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - b[ri, w]);
                if (drop <= -10) drops.Add(ri);
                Console.WriteLine($"| {rows[ri].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => g[ri, w] == b[ri, w] ? F1(g[ri, w]) : $"{F1(g[ri, w])}（{Sg(g[ri, w] - b[ri, w])}）")) + $" | {F1(M(g, ri))} | {Sg(M(g, ri) - M(b, ri))} | {Sg(drop)} |");
            }
            if (movedRows == 0) Console.WriteLine("| （動いた行なし） |" + string.Concat(Enumerable.Range(0, nw + 3).Select(_ => " |")));
            Console.WriteLine();
            var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
            double p5b = pr.Average(ri => b[ri, nw - 1]), p5 = pr.Average(ri => g[ri, nw - 1]);
            double all = Enumerable.Range(0, rows.Length).Average(ri => M(g, ri)), allB = Enumerable.Range(0, rows.Length).Average(ri => M(b, ri));
            Console.WriteLine($"動いた行 {movedRows} ／ セル {movedCells}・ソムがいない行で動いたセル **{outside}**（シガだけの行を含む）・全{rows.Length}行の第2〜5波平均 {F1(allB)} → {F1(all)}（{Sg(all - allB)}）・主判定19行の第五波 {F1(p5b)} → {F1(p5)}（歯止め {F1(Baseline.PrimaryFifthFloor)}）");
            if (drops.Count == 0) { Console.WriteLine("(G2): −10.0pt 以上落ちた行なし。"); continue; }
            foreach (int ri in drops)
            {
                var parts = new List<string>(); bool broken = false;
                foreach (var d in rows[ri].F.Occupied().Select(o => o.Def).Distinct())
                {
                    var others = Enumerable.Range(0, rows.Length).Where(rj => rj != ri && rows[rj].F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToList();
                    if (others.Count == 0) { parts.Add($"{Short(d)} 他の行 0"); continue; }
                    double ch = others.Average(rj => M(g, rj) - M(b, rj));
                    if (ch <= -3.0) broken = true;
                    parts.Add($"{Short(d)} {Sg(ch)}（{others.Count} 行）");
                }
                Console.WriteLine($"- (G2) {rows[ri].Name}: {string.Join("・", parts)} → **{(broken ? "壊れ" : "編成上の制約")}**");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5-4 ソムの格子（第311期と同じ作り・駒は規定。版のシガは候補 ／ 固定枠のシガに当てる）
    // ---------------------------------------------------------------------------------
    static void Grid(string ver, string wave)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = V(ver);
        var w = S307.WaveOf(wave);
        UnitDef[] fixedBase = w.Boss ? new[] { UnitCatalog.KuguH324, UnitCatalog.KataH324 } : new[] { UnitCatalog.TouH324 };
        var pool = (w.Boss ? S307.BossPool : S307.ElitePool).Select(Cur).ToArray();
        int k = w.Boss ? 2 : 3, need = w.Boss ? 1 : 5;
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedBase.Append(UnitCatalog.SomH318).Concat(t).ToArray()));
        var res = new (int Wins, long WinT, int Ctl)?[boards.Count];
        int cutPass = 0;
        static int W(Formation f, S287.Wave w, int n, out long wt)
        {
            int wins = 0; wt = 0;
            for (int s = 0; s < n; s++) { var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false); if (r.PlayerWon) { wins++; wt += r.Turns; } }
            return wins;
        }
        Parallel.For(0, boards.Count, i =>
        {
            var f = Apply(B283.Seat(boards[i]), v);
            if (W(f, w, S307.CutSeeds, out _) < need) return;
            Interlocked.Increment(ref cutPass);
            int wins = W(f, w, Seeds, out long wt);
            int ctl = -1;
            if (wins * 2 >= Seeds) ctl = W(S309.Swap(f, v.Som, UnitCatalog.Dolga), w, Seeds, out _);
            res[i] = (wins, wt, ctl);
        });
        var all = res.Where(r => r is not null).Select(r => r!.Value).ToList();
        int half = all.Count(r => r.Wins * 2 >= Seeds), reach = all.Count(r => r.Wins * 2 >= Seeds && r.Ctl >= 0 && r.Ctl * 2 <= r.Wins), ninety = all.Count(r => r.Wins * 10 >= Seeds * 9);
        Console.WriteLine($"# 第318期 §5-4 ソムの格子 × {w.Name} × 版 {v.Name}（固定枠 {S307.OrderName(fixedBase)} ＋ ソム・探索枠{k}・{boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{S307.CutSeeds - 1} で {need} 勝以上（{cutPass:N0} 台）→ seed 0..{Seeds - 1} → 勝率 50% 以上でソム → ドルガ。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 ≧ 50% | **届いた（ソム → ドルガで半分以下）** | 勝率 ≧ 90% | 勝率 ＞ 0% | 最大の勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| {v.Name} | {half:N0} | {reach:N0} | {ninety:N0} | {all.Count(r => r.Wins > 0):N0} | {(all.Count == 0 ? "—" : F1(100.0 * all.Max(r => r.Wins) / Seeds))} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §6 台本の例 ／ 別の seed の帯 ／ 1戦の並び
    // ---------------------------------------------------------------------------------
    static void Find()
    {
        Console.WriteLine("# som318 find —— 共鳴返し（`BeastThrow 共鳴` → `BeastBurst 共鳴返し`）・走る（CR-b）・打ち返し（SG）・獣の死の読み手（ハギ ／ ツギ）のある seed");
        Console.WriteLine();
        foreach (var vn in new[] { "CR-a", "CR-b", "CR-b+SG" })
            foreach (var (bn, f0) in Boards().Where(b => b.Name is "光の盾" or "光の盾 鞭" or "勝ち台 ツギ→ソム" or HagiBoard))
                foreach (var wn in new[] { "boss", "guard", "2" })
                {
                    var w = WaveOf(wn); var f = Apply(f0, V(vn));
                    for (int sd = 0, found = 0; sd < 20 && found < 1; sd++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var th = r.Events.Where(x => x.Kind == BattleEventKind.BeastThrow).ToList();
                        if (!th.Any(x => x.Text is BeastThrowLabels.Resonance or BeastThrowLabels.Backswing)) continue;
                        found++;
                        string T(string lb) { var ts = th.Where(x => x.Text == lb).Select(x => x.Turn).Distinct().Take(5).ToList(); return ts.Count == 0 ? "—" : "T" + string.Join(",", ts); }
                        Console.WriteLine($"- {vn} × {bn} × {w.Name} × seed {sd}: 共鳴 {T(BeastThrowLabels.Resonance)}・止まった {T(BeastThrowLabels.Hushed)}・走る {T(BeastThrowLabels.Arc)}・打ち返し {T(BeastThrowLabels.Backswing)}・投げた {T(BeastThrowLabels.Hit)}（{(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}）");
                    }
                }
    }

    /// <summary>別の seed の帯（<paramref name="from"/> から <paramref name="n"/> 戦）で、規定と版の勝率を並べる（差が seed の当たり外れかを見る）。</summary>
    static void Noise(string ver, string part, string wave, int from, int n)
    {
        var v = V(ver);
        var (name, f0) = Boards().First(b => b.Name.Contains(part, StringComparison.Ordinal) || (part == "shield" && b.Name == "光の盾"));
        var w = WaveOf(wave);
        int b = WinsLite(f0, w, n, from), a = WinsLite(Apply(f0, v), w, n, from);
        Console.WriteLine($"| {name} | {w.Name} | seed {from}..{from + n - 1} | 規定 {F1(100.0 * b / n)} | {v.Name} {F1(100.0 * a / n)} | 差 {Sg(100.0 * (a - b) / n)} |");
    }

    static void Memo(string ver, string part, string wave, int seed, int t0, int t1)
    {
        var v = V(ver);
        var (name, f0) = Boards().First(b => b.Name.Contains(part, StringComparison.Ordinal) || (part == "shield" && b.Name == "光の盾") || (part == "hagi" && b.Name == HagiBoard) || (part == "win" && b.Name.StartsWith("勝ち台", StringComparison.Ordinal)));
        var f = Apply(f0, v);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "背いた獣");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# som318 memo —— {name}（{v.Name}・{Seats(f)}）× {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・T{t0}〜T{t1}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        for (int i = 0; i < r.Events.Count; i++)
        {
            var x = r.Events[i];
            if (x.Turn < t0 || x.Turn > t1) continue;
            if (x.Kind is BattleEventKind.StatSnapshot or BattleEventKind.StatusSnapshot) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"#{i,-4} {x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  hp={x.HpAfter}  Slot {x.Slot}{(x.PartnerId is int pp ? $"  partner {N(pp)}" : "")}{(x.StatusRemaining is int sr ? $"  rem {sr}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査（§7）
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation f, List<UnitState> enemy)
    {
        var ctx = new BattleContext(0, true);
        var add = typeof(BattleContext).GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        foreach (var u in p.Concat(enemy)) add.Invoke(ctx, new object[] { u });
        foreach (var u in ctx.AllUnits) u.SetCounter(StatusKeys.IdleTurn, 99);
        return (ctx, p, enemy);
    }

    static UnitTally T(BattleContext ctx, UnitState u)
    {
        var d = (Dictionary<string, UnitTally>)typeof(BattleContext).GetProperty("TallyByUnit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ctx)!;
        return d.TryGetValue(u.Def.Id, out var t) ? t : new UnitTally();
    }

    static List<UnitState> WaveN(int i) => BattleEngine.Materialize(EnemyCatalog.Stages[i].Enemy, BattleContext.EnemyTeam);
    static void TurnStart(BattleContext ctx, UnitState som) { foreach (var t in som.Traits.ToList()) t.OnTurnStart(ctx, som); }
    static List<UnitState> Stood(BattleContext ctx, UnitState som) => ctx.AllUnits.Where(u => u.IsAlive && BetrayedTrait.IsFodder(u) && u.RawCounter(BattleContext.StandOwnerKey) == som.InstanceId + 1).ToList();
    static List<BattleEvent> After(BattleContext ctx, int ev0) => ctx.Events.Skip(ev0).ToList();

    /// <summary>前1 に味方1体（狙われるのはその1体）・後3 にソムの版・前の味方以外は HP 半分（光が降る先）。<paramref name="charged"/> なら前の味方を先に帯電させる。</summary>
    static (BattleContext Ctx, UnitState Som, UnitState Ally, List<UnitState> E) One(UnitDef ally, UnitDef som, bool charged, int wave = 2)
    {
        var f = Formation.Build(front1: ally, back3: som);
        var (ctx, p, e) = Board(f, WaveN(wave));
        var s = p.First(u => u.Def.Id == "som"); var a = p.First(u => u != s);
        s.Hp = s.MaxHp / 2;
        if (charged) a.SetCounter(StatusKeys.Shock, 1);
        return (ctx, s, a, e);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som318 check —— 第318期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef cra = UnitCatalog.SomCRa, crb = UnitCatalog.SomCRb, sg = UnitCatalog.ShigaSG;

        // (a) 段0: 規定のソム ＝ BD-E2・旧の定義 `SomH317` が残っている・固定の口・ハギの追い打ちが獣で出る
        {
            var som = UnitCatalog.SomH318; var h = UnitCatalog.SomH317;   // 第319期: 第318期の規定は `SomH318`
            bool d = som.Traits.SequenceEqual(h.Traits.Append(TraitId.BeastDeathCount)) && som.Traits.SequenceEqual(UnitCatalog.SomBD.Traits) && h.Traits.SequenceEqual(UnitCatalog.SomSWKa.Traits)
                && som.PlusText == h.PlusText + "。弾けた下僕も、倒れた敵に数える" && som.MinusText == h.MinusText && som.MaxHp == h.MaxHp && som.Attack == h.Attack && som.Speed == h.Speed && ReferenceEquals(som.Actions, h.Actions)
                && UnitCatalog.All.Contains(UnitCatalog.Som) && !UnitCatalog.Everyone.Contains(som) && !UnitCatalog.Everyone.Contains(h) && UnitCatalog.All.Count == 52
                && UnitCatalog.SomBTa.Traits.SequenceEqual(h.Traits.Concat(new[] { TraitId.BeastStand, TraitId.BeastBetray })) && UnitCatalog.SomSBa.Traits.SequenceEqual(h.Traits.Append(TraitId.BeastStand));
            bool pin = ReferenceEquals(PinS317(som), h) && ReferenceEquals(PinDS316(som), h) && ReferenceEquals(PinKataTou311(som), h)
                && Pin316(Presets.Playtest.First(r => r.Name == S309.Shield).F).Occupied().Any(o => ReferenceEquals(o.Def, h))
                && Pin311(Presets.Playtest.First(r => r.Name == S309.Shield).F).Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.SomH311))
                && Pin308(PlaytestH317.First(r => r.Name == S309.Shield).F).Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.SomH307));
            // ハギの追い打ちが獣の弾けで出る（規定）・旧の定義では出ない
            long Hagi(UnitDef sd)
            {
                var f = Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Hagi, back3: sd);
                var (ctx, p, _) = Board(f, WaveN(2));
                var s = p.First(u => u.Def.Id == "som"); var hg = p.First(u => u.Def.Id == "hagi");
                int ev0 = ctx.Events.Count;
                TurnStart(ctx, s);
                return After(ctx, ev0).Count(x => x.Kind == BattleEventKind.Attack && x.ActorId == hg.InstanceId);
            }
            long hNew = Hagi(som), hOld = Hagi(h);
            Expect("(a) 段0: 規定のソム ＝ BD-E2（旧の規定 ＋ `BeastDeathCount`・札は第317期の `SomBD` と同じ・PlusText の末尾「弾けた下僕も、倒れた敵に数える」）・旧の定義 `SomH317` が `Everyone` の外に残り、第314〜317期の版はそこから作る・`PinS317` ／ `Pin316` ／ `Pin308`〜`Pin311` が旧へ替える・ハギの追い打ちが獣で出る（旧では出ない）",
                d && pin && hNew >= 1 && hOld == 0, $"定義 {d}・固定 {pin}・ハギ 規定 {hNew} ／ 旧 {hOld}");
        }
        // (b) CR-a: 帯電した味方に落ちた雷は付かず、投げた獣が弾ける（起こし手 ＝ 受けた味方・光になる・獣の死）・帯電していない味方には感電が付き獣は立つ
        {
            var (ctx, som, golm, _) = One(UnitCatalog.Golm, cra, charged: true);
            int ev0 = ctx.Events.Count;
            TurnStart(ctx, som);
            var ev = After(ctx, ev0);
            var thr = ev.Where(x => x.Kind == BattleEventKind.BeastThrow).ToList();
            int? beastId = thr.FirstOrDefault()?.ActorId;
            int iRes = ev.FindIndex(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Resonance && x.TargetId == golm.InstanceId);
            int iBurst = ev.FindIndex(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Resonance && x.ActorId == golm.InstanceId && x.TargetId == beastId);
            int iSpent = ev.FindIndex(x => x.Kind == BattleEventKind.ShockSpent && x.TargetId == beastId && x.ActorId == golm.InstanceId);
            bool b1 = thr.Count == 1 && iRes >= 0 && iBurst > iRes && iSpent > iBurst
                && ev.Any(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Release)
                && !ev.Any(x => x.Kind == BattleEventKind.Death && x.TargetId == beastId)
                && golm.RawCounter(StatusKeys.Shock) > 0 && Stood(ctx, som).Count == 0
                && T(ctx, som).CrFired == 1 && (T(ctx, som).BdKind?[4] ?? 0) == 1 && T(ctx, som).BtShocked == 0;
            var (c2, s2, g2, _) = One(UnitCatalog.Golm, cra, charged: false);
            int e2 = c2.Events.Count;
            TurnStart(c2, s2);
            var th2 = After(c2, e2).Where(x => x.Kind == BattleEventKind.BeastThrow).ToList();
            bool b2 = th2.Count == 1 && th2[0].Text == BeastThrowLabels.Hit && th2[0].Amount == 1 && g2.RawCounter(StatusKeys.Shock) > 0 && Stood(c2, s2).Count == 1 && T(c2, s2).CrFired == 0;
            Expect("(b) CR-a: 帯電した味方に落ちた雷は付かず（味方の感電は残る）、`BeastThrow 共鳴` → `BeastBurst 共鳴返し` → 獣の `ShockSpent`（起こし手 ＝ 受けた味方）・光が降る・`Death` なし・獣の死に数える（種類 4）／ 帯電していない味方には感電が付き、獣は立つ",
                b1 && b2, $"跳ね返り {b1}（並び {iRes} → {iBurst} → {iSpent}）・帯電していない {b2}");
        }
        // (c) CR-a: 粛で止まる（雷は何もせず獣は立ったまま）・痺れ ／ 組み付きでは止まらない
        {
            // 粛: 第2波（粛の伝令）を盤に置き、粛が効いていることを確かめてから
            var (ctx, som, golm, e) = One(UnitCatalog.Golm, cra, charged: true, wave: 1);
            bool hushOn = ctx.HushBindingNow;
            int ev0 = ctx.Events.Count;
            TurnStart(ctx, som);
            var ev = After(ctx, ev0);
            bool c1 = hushOn && ev.Any(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Hushed) && ev.Any(x => x.Kind == BattleEventKind.Sealed && x.TargetId == golm.InstanceId)
                && Stood(ctx, som).Count == 1 && T(ctx, som).CrHushed == 1 && T(ctx, som).CrFired == 0 && golm.RawCounter(StatusKeys.Shock) > 0;
            // 痺れ: 受けた味方が痺れていても跳ね返る
            var (c2, s2, g2, _) = One(UnitCatalog.Golm, cra, charged: true);
            g2.SetCounter(StatusKeys.Stun, 1);
            TurnStart(c2, s2);
            bool c2ok = T(c2, s2).CrFired == 1 && Stood(c2, s2).Count == 0;
            // 組み付かれていても跳ね返る
            var (c3, s3, g3, e3) = One(UnitCatalog.Golm, cra, charged: true);
            g3.SetCounter(StatusKeys.Grappled, 1);
            TurnStart(c3, s3);
            bool c3ok = T(c3, s3).CrFired == 1;
            Expect("(c) CR-a: 跳ね返りは粛で止まる（`Sealed 粛` → `BeastThrow 共鳴が止まった`・獣は立ったまま・味方の感電は残る）・痺れ ／ 組み付きでは止まらない",
                c1 && c2ok && c3ok, $"粛 {c1}（粛が効いていた {hushOn}）・痺れ {c2ok}・組み付き {c3ok}");
        }
        // (d) CR-b: 跳ね返りの前に獣の隣の敵すべてに感電が付く（書き手 ＝ 受けた味方）・そのあと獣が弾ける
        {
            var (ctx, som, golm, e) = One(UnitCatalog.Golm, crb, charged: true, wave: 2);
            int ev0 = ctx.Events.Count;
            TurnStart(ctx, som);
            var ev = After(ctx, ev0);
            var burst = ev.FirstOrDefault(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Resonance);
            int iBurst = burst is null ? -1 : ev.IndexOf(burst);
            int beastSlot = burst?.Slot ?? -1;
            var shape = FormationShape.X;
            var adj = e.Where(u => u.Slot != beastSlot && shape.AreAdjacent(beastSlot, u.Slot)).ToList();
            var arcs = ev.Where(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Arc).ToList();
            bool order = arcs.Count > 0 && arcs.All(x => ev.IndexOf(x) < iBurst);
            bool set = adj.Count > 0 && adj.All(u => arcs.Any(x => x.TargetId == u.InstanceId && x.PartnerId == golm.InstanceId)) && arcs.Count == adj.Count(u => u.IsAlive || ev.Any(x => x.Kind == BattleEventKind.Death && x.TargetId == u.InstanceId))
                && adj.All(u => ev.Any(x => x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Shock && x.TargetId == u.InstanceId && x.ActorId == golm.InstanceId && ev.IndexOf(x) < iBurst));
            bool spread = ev.Count(x => x.Kind == BattleEventKind.ShockSpent && ev.IndexOf(x) > iBurst) >= 2 && T(ctx, som).CrArcShocked == adj.Count;
            // CR-a は走らない
            var (c2, s2, _, _) = One(UnitCatalog.Golm, cra, charged: true, wave: 2);
            int e2 = c2.Events.Count;
            TurnStart(c2, s2);
            bool noArc = !After(c2, e2).Any(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Arc) && T(c2, s2).CrArcFoes == 0;
            Expect("(d) CR-b: 跳ね返りの前に獣の隣の敵（陣形の表で隣）すべてに雷が走り感電が付く（書き手 ＝ 受けた味方・`BeastBurst 共鳴返し` より前）→ 獣の弾けの放電がいま付いた感電を拾って広がる・CR-a は走らない（糸玉は `_units` の外なので数えない）",
                order && set && spread && noArc, $"並び {order}・隣 {adj.Count} 体に付いた {set}・連鎖 {spread}（新しく感電 {T(ctx, som).CrArcShocked}）・CR-a {noArc}");
        }
        // (e) SG: 帯電していないシガは蓄電 +1 して獣へ鞭を1回（蓄電を使わない）・帯電したシガは共鳴返し・粛 ／ 痺れで打ち返しが止まる
        {
            var (ctx, som, sh, _) = One(sg, cra, charged: false);
            int c0 = StoredChargeTrait.Of(sh);
            int ev0 = ctx.Events.Count;
            TurnStart(ctx, som);
            var ev = After(ctx, ev0);
            var hit = ev.FirstOrDefault(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Hit);
            int? beastId = hit?.ActorId;
            int iHit = hit is null ? -1 : ev.IndexOf(hit);
            int iSw = ev.FindIndex(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Backswing && x.TargetId == sh.InstanceId);
            int iAtk = ev.FindIndex(x => x.Kind == BattleEventKind.Attack && x.ActorId == sh.InstanceId && x.TargetId == beastId);
            bool e1 = hit is not null && hit.Amount == 1 && iSw > iHit && iAtk > iSw && ev.Count(x => x.Kind == BattleEventKind.Attack && x.ActorId == sh.InstanceId) == 1
                && StoredChargeTrait.Of(sh) == c0 + 1 && Stood(ctx, som).Count == 0 && T(ctx, som).SgFired == 1;
            // 帯電したシガ: 共鳴返し（起こし手はシガ）・打ち返さない
            var (c2, s2, sh2, _) = One(sg, cra, charged: true);
            int e2 = c2.Events.Count;
            TurnStart(c2, s2);
            var ev2 = After(c2, e2);
            bool e2ok = T(c2, s2).CrFired == 1 && T(c2, s2).SgFired == 0 && ev2.Any(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Resonance && x.ActorId == sh2.InstanceId);
            // 粛: 打ち返しは止まる（感電は付く）
            var (c3, s3, sh3, _) = One(sg, cra, charged: false, wave: 1);
            bool hushOn = c3.HushBindingNow;
            TurnStart(c3, s3);
            bool e3 = hushOn && T(c3, s3).SgFired == 0 && T(c3, s3).SgHushed == 1 && sh3.RawCounter(StatusKeys.Shock) > 0 && Stood(c3, s3).Count == 1;
            // 痺れ: 打ち返しは止まる
            var (c4, s4, sh4, _) = One(sg, cra, charged: false);
            sh4.SetCounter(StatusKeys.Stun, 1);
            TurnStart(c4, s4);
            bool e4 = T(c4, s4).SgFired == 0 && T(c4, s4).SgBlocked == 1 && Stood(c4, s4).Count == 1;
            // 規定のシガは打ち返さない
            var (c5, s5, _, _) = One(UnitCatalog.ShigaH318, cra, charged: false);
            TurnStart(c5, s5);
            bool e5 = T(c5, s5).SgFired == 0 && Stood(c5, s5).Count == 1;
            Expect("(e) SG: 帯電していないシガは感電が付き蓄電 +1 → `BeastThrow 打ち返し` → 獣へ鞭を1回（蓄電はそのまま・獣は弾けて消える）・帯電したシガは共鳴返し（起こし手はシガ）・粛 ／ 痺れで打ち返しが止まる・規定のシガは打ち返さない",
                e1 && e2ok && e3 && e4 && e5, $"打ち返し {e1}（並び {iHit} → {iSw} → {iAtk}・蓄電 {c0}→{StoredChargeTrait.Of(sh)}）・帯電 {e2ok}・粛 {e3}（粛が効いていた {hushOn}）・痺れ {e4}・規定 {e5}");
        }
        // (f) 判定の順: 共鳴返し → クグの糸 → 感電が付く
        {
            (string Text, bool KuguCharged, bool FoeCharged) Kugu(bool charged)
            {
                var (ctx, som, kugu, e) = One(UnitCatalog.KuguH324, cra, charged);
                var foe = e.First(u => u.IsAlive && FormationRules.RowOf(u.Slot) == Row.Front);
                kugu.SetCounter(GrappleTrait.TargetKey, foe.InstanceId + 1);
                int ev0 = ctx.Events.Count;
                TurnStart(ctx, som);
                var th = After(ctx, ev0).FirstOrDefault(x => x.Kind == BattleEventKind.BeastThrow);
                return (th?.Text ?? "—", kugu.RawCounter(StatusKeys.Shock) > 0, foe.RawCounter(StatusKeys.Shock) > 0);
            }
            var k1 = Kugu(true); var k2 = Kugu(false);
            Expect("(f) 判定の順: 帯電したクグ（糸の先が生きている）に向かった雷は共鳴返し（糸へ流れない）・帯電していないクグなら糸へ流れる（クグは帯電しない）",
                k1.Text == BeastThrowLabels.Resonance && k2.Text == BeastThrowLabels.Thread && !k2.KuguCharged && k2.FoeCharged, $"帯電 {k1.Text}・帯電していない {k2.Text}（クグ {k2.KuguCharged}・糸の先 {k2.FoeCharged}）");
        }
        // (g) 緊急の喚び出しはいつもその場で弾け、投げない
        {
            var f = Formation.Build(front1: UnitCatalog.Golm, back3: crb);
            var (ctx, p, _) = Board(f, WaveN(2));
            var som = p.First(u => u.Def.Id == "som"); var ally = p.First(u => u != som);
            int ev0 = ctx.Events.Count;
            ctx.EmergencyBurst(som, ally);
            var ev = After(ctx, ev0);
            Expect("(g) 緊急の喚び出しはいつもその場で弾け、投げない（CR-b）", Stood(ctx, som).Count == 0 && !ev.Any(x => x.Kind == BattleEventKind.BeastThrow) && ev.Count(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Emergency) == 1,
                $"立った {Stood(ctx, som).Count}・投げた {ev.Count(x => x.Kind == BattleEventKind.BeastThrow)}");
        }
        // (h) 実戦: 本編 第2波（粛）で共鳴返しが粛で止まる
        {
            long fired = 0, hushed = 0;
            var fs = Apply(Playtest(S309.Shield), V("CR-a"));
            for (int sd = 0; sd < 60; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(fs, BattleContext.PlayerTeam), Waves[6].Make(), sd, verbose: false);
                var t = r.TallyByUnit["som"]; fired += t.CrFired; hushed += t.CrHushed;
            }
            Expect("(h) CR-a: 本編 第2波（粛の伝令）× 光の盾 × seed 0..59 で、跳ね返りが粛で止まる（数を数えるだけ）", fired > 0 && hushed > 0, $"跳ね返った {fired}・粛で止まった {hushed}");
        }
        // (i) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var v in Vers.Skip(1))
                foreach (var (_, f0) in Boards())
                    foreach (var w in Waves)
                        for (int sd = 0; sd < 2; sd++)
                        {
                            var f = Apply(f0, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                        }
            Expect($"(i) 決定的・verbose の有無で勝敗と決着T が変わらない（{Vers.Length - 1} 版 × 代表台 {Boards().Length} × {Waves.Length} 波 × seed 0..1）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (j) 版の定義・規定のソム ／ シガ ／ カタは動かない・経路の名前
        {
            var som = UnitCatalog.SomH318;   // 第319期: 第318期の規定
            bool defs = UnitCatalog.SomBT318.Traits.SequenceEqual(som.Traits.Concat(new[] { TraitId.BeastStand, TraitId.BeastBetray }))
                && cra.Traits.SequenceEqual(som.Traits.Concat(new[] { TraitId.BeastStand, TraitId.BeastBetray, TraitId.BeastResonance }))
                && crb.Traits.SequenceEqual(cra.Traits.Append(TraitId.BeastResonanceArc))
                && sg.Traits.SequenceEqual(UnitCatalog.ShigaH318.Traits.Append(TraitId.BetrayBackswing)) && sg.PlusText == UnitCatalog.ShigaH318.PlusText + "。背いた召喚獣の雷を浴びると、その場で打ち返す"
                && cra.MinusText == UnitCatalog.SomResonanceMinusText && crb.MinusText == UnitCatalog.SomResonanceMinusText + UnitCatalog.SomResonanceArcText
                && new[] { UnitCatalog.SomBT318, cra, crb, sg }.All(d => !UnitCatalog.Everyone.Contains(d));
            bool dflt = !som.Traits.Any(t => t is TraitId.BeastStand or TraitId.BeastBetray or TraitId.BeastResonance or TraitId.BeastResonanceArc)
                && !UnitCatalog.ShigaH318.Traits.Contains(TraitId.BetrayBackswing) && !UnitCatalog.KataH324.Traits.Contains(TraitId.LightningRod)
                && UnitCatalog.Fodder.MaxHp == 12 && UnitCatalog.Fodder.Attack == 0 && UnitCatalog.Fodder.Speed == 1;
            bool route = OutOfTurnRoutes.Names.Length == Enum.GetValues<OutOfTurnRoute>().Length && OutOfTurnRoutes.Names[(int)OutOfTurnRoute.Resonance] == "共鳴返し"
                && OutOfTurnRoutes.Names[(int)OutOfTurnRoute.Backswing] == "打ち返し" && OutOfTurnRoutes.Names[^1] == "その他";
            Expect("(j) 版の定義（BT-a ＝ 規定 ＋ 立つ ＋ 投げる・CR-a ＝ BT-a ＋ 共鳴返し・CR-b ＝ CR-a ＋ 走る・SG ＝ 規定のシガ ＋ 打ち返し・`Everyone` の外）・規定のソム ／ シガ ／ カタ ／ 獣は動かない・経路 `共鳴返し` ／ `打ち返し`",
                defs && dflt && route, $"版 {defs}・規定 {dflt}・経路 {route}（{OutOfTurnRoutes.Count}）");
        }
        // (k) 乱数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 8db459b:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var pr = System.Diagnostics.Process.Start(psi)!;
                    string o = pr.StandardOutput.ReadToEnd();
                    pr.WaitForExit();
                    return pr.ExitCode == 0 ? o : null;
                }
                catch { return null; }
            }
            var notes = new List<string>(); bool clean = true;
            foreach (var path in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
            {
                string now = File.ReadAllText(path); string? head = Head(path);
                if (head is null) { clean = false; notes.Add($"{path}: 第317期の版を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(k) `PickOne(` ／ `Roll(` を新たに使っていない（第317期のコミット 8db459b と比べる）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
