using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;
using S307 = Som307Diag;
using S309 = Som309Diag;
using S310 = Som310Diag;

// =====================================================================================
// som317 —— 第317期「背いた獣の裏切りの一撃（BT-a）・カタの避雷針（BT-b ／ BT-c）・獣の弾けを敵の死に数える（BD）」。
// 指示書は design/PHASE317_SOM_BETRAY_SPEC.md ／ 報告は design/PHASE317_SOM_BETRAY.md。
//
//     dotnet run --project BattleSim -c Release 0 som317 p0          # Phase 0（§4）: 敵の手でこちらに付く感電・裏切りの一撃の相手の見込み（SB-a ＋ 計数器 `ThrowCensus`）・味方の感電の弾け・死の読み手
//     dotnet run --project BattleSim -c Release 0 som317 boards [版,…]  # §5-2: 代表台 7 × 10 波 × 版（規定 ／ SB-a ／ BT-a ／ BT-b ／ BT-c ／ BD-E2 ／ BD-BTx ほか）
//     dotnet run --project BattleSim -c Release 0 som317 compare [版,…] # §5-3: `compare` 64 行 × 版
//     dotnet run --project BattleSim -c Release 0 som317 grid <版> <boss|guard|bat>   # §5-4: ソムの格子（第311期と同じ作り・駒は規定）
//     dotnet run --project BattleSim -c Release 0 som317 find        # §6: 裏切りの一撃 ／ 避雷針のある seed（台本の例）
//     dotnet run --project BattleSim -c Release 0 som317 memo <版> <台の一部> <boss|guard|bat|nine|nine2|1..5> <seed> <T0> <T1>
//     dotnet run --project BattleSim -c Release 0 som317 check       # 自己検査（§7）
//
// **予測のファイルが無ければ本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p317/predict.md`）。
// 台の駒は第317期の規定のまま（固定していない）——規定が動いたら、この器具は第317期の規定に固定し直すこと。
// =====================================================================================
static class Som317Diag
{
    const string PredictFile = ".tmp/p317/predict.md";
    const int Seeds = S307.Seeds;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"som317: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "compare": CompareAll(A(3, "")); return;
            case "grid": Grid(A(3, "BT-b"), A(4, "boss")); return;
            case "find": Find(); return;
            case "memo": Memo(A(3, "BT-b"), A(4, "光の盾"), A(5, "boss"), int.Parse(A(6, "0")), int.Parse(A(7, "1")), int.Parse(A(8, "5"))); return;
            case "noise": Noise(A(3, "BT-c"), A(4, "感電 行"), A(5, "guard"), int.Parse(A(6, "200")), int.Parse(A(7, "400"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("som317: モードは p0 / boards / compare / grid / find / memo / check。"); return;
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

    /// <summary>版（ソムの版 ＋ カタの版）。カタの版が null なら規定のカタのまま。</summary>
    internal sealed record Ver(string Name, UnitDef Som, UnitDef? Kata = null);

    static readonly UnitDef BtBd = UnitCatalog.SomWith(new[] { TraitId.BeastStand, TraitId.BeastBetray, TraitId.BeastDeathCount }, UnitCatalog.SomBTa.PlusText + UnitCatalog.SomBeastDeathText, UnitCatalog.SomBetrayMinusText);
    static readonly UnitDef BtBdCap = UnitCatalog.SomWith(new[] { TraitId.BeastStand, TraitId.BeastBetray, TraitId.BeastDeathCount, TraitId.BeastDeathCap }, BtBd.PlusText, BtBd.MinusText);
    static readonly UnitDef E2BdCap = UnitCatalog.SomWith(new[] { TraitId.BeastDeathCount, TraitId.BeastDeathCap }, UnitCatalog.SomBD.PlusText, UnitCatalog.SomBD.MinusText);

    internal static readonly Ver[] Vers =
    {
        new("規定", UnitCatalog.SomH317),
        new("SB-a", UnitCatalog.SomSBa),
        new("BT-a", UnitCatalog.SomBTa),
        new("BT-b", UnitCatalog.SomBTa, UnitCatalog.KataLR),
        new("BT-c", UnitCatalog.SomBTa, UnitCatalog.KataLRall),
        new("BD-E2", UnitCatalog.SomBD),
        new("BD-BTa", BtBd),
        new("BD-BTb", BtBd, UnitCatalog.KataLR),
        new("BD-BTc", BtBd, UnitCatalog.KataLRall),
        new("BD-E2c", E2BdCap),
        new("BD-BTbc", BtBdCap, UnitCatalog.KataLR),
        new("BD-BTcc", BtBdCap, UnitCatalog.KataLRall),
    };
    /// <summary>`boards` ／ `compare` を引数なしで回したときの版（BD の BT 側と上限の版は名前で足す）。</summary>
    static readonly string[] DefaultVers = { "規定", "SB-a", "BT-a", "BT-b", "BT-c", "BD-E2" };
    static Ver V(string n) => Vers.First(v => v.Name == n || (n == "base" && v.Name == "規定"));   // "base" ＝ 規定（コンソールの文字コードを避ける別名）
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? DefaultVers : new[] { "規定" }.Concat(only.Split(',').Where(x => x is not ("規定" or "base")))).Select(V).ToArray();

    internal static Formation Apply(Formation f, Ver v)
    {
        var g = ReferenceEquals(v.Som, UnitCatalog.SomH317) ? f : S309.Swap(f, UnitCatalog.SomH317, v.Som);
        return v.Kata is null ? g : S309.Swap(g, UnitCatalog.Kata, v.Kata);
    }

    internal static readonly S287.Wave[] Waves = S310.Waves;
    static S287.Wave WaveOf(string n) => n switch { "nine" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };

    static Formation Playtest(string n) => PlaytestH317.First(r => r.Name == n).F;
    static Formation RowF(string n) => CompareBuildsH317().First(r => r.Name == n).F;
    /// <summary>第316期までの器具（`S307.Order` ／ `S307.BossPool` ほか）は第317期にドハ ／ シガを旧に固定したので、この器具は規定の駒に戻して使う。</summary>
    static UnitDef Cur(UnitDef d) => ReferenceEquals(d, UnitCatalog.ShigaSWa) ? UnitCatalog.Shiga : ReferenceEquals(d, UnitCatalog.DohaH316) ? UnitCatalog.Doha : d;
    static Formation WinSeat() => B283.Seat(S307.Order(S307.WinBoard).Select(Cur).ToArray());
    internal const string HagiBoard = "光の盾 クビ→ハギ";

    /// <summary>§5-2 の代表台（規定の駒・席は出典のまま）。BD 用の台は光の盾の後1（クビ）をハギにした（光の盾 鞭のクビ → シガと同じ席）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("光の盾", Playtest(S309.Shield)),
        ("光の盾 鞭", Playtest(Som312Diag.Whip)),
        ("光の盾 重 鞭", Playtest(Som312Diag.WhipHeavy)),
        ("感電 行", RowF("感電 (シガ×カタ×ソム)")),
        ("勝ち台 ツギ→ソム", S309.Swap(WinSeat(), UnitCatalog.Tsugi, UnitCatalog.SomH317)),
        ("火の型 ソラ→ソム", S309.Swap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, UnitCatalog.SomH317)),
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
        public long StandLife, StandLifeN, AtkOnStand, DmgOnStand, StandPopped, StandKilled, StandRod, StandBdPop, StandAtEnd;
        // 裏切りの一撃
        public long Thrown, NoTarget, ThrowShocked, ThrowAlready, ThrowThread, ThrowThreadShocked, RodCaught, RodOut, RodBlocked, RodHushed;
        public Dictionary<string, long> ThrowTo = new();
        public long[] ThrowSlot = new long[9];
        public long DestShiga, DestPopped, DestLeft, DestUsed;
        // 避雷針（カタ）
        public long KataRod, KataRodUnits, KataRodDry, KataCloud, KataN;
        // 獣の死
        public long BdCounted, BdOnKill, BdOnAnyDeath, BdSkipped, BdCapped;
        public long[] BdKind = new long[4];
        public long HagiAtk, HagiDmg, HagiSelf, HagiDied, HagiN, HagiRecoil, HagiTopTurn;
        // シガ・味方の感電・光
        public long ShigaTurns, ShigaStalls, ShigaN, SwFires, AllyPops, AllyStuns, Lights, VeilAdded;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long FoeDmgAll => FoeDmg.Values.Sum();
        public void Merge(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; Turns += o.Turns; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; SomDied += o.SomDied; SomDeathT += o.SomDeathT;
            foreach (var (k, v) in o.FoeDmg) FoeDmg[k] = FoeDmg.GetValueOrDefault(k) + v;
            FoeHeal += o.FoeHeal;
            Calls += o.Calls; Called += o.Called; Charged += o.Charged; NoSeat += o.NoSeat; NoFoe += o.NoFoe; Stood += o.Stood; Bursts += o.Bursts; BurstUnits += o.BurstUnits; EmergFired += o.EmergFired;
            AlivePeak = Math.Max(AlivePeak, o.AlivePeak); PeakSum += o.PeakSum; AliveSum += o.AliveSum; AliveN += o.AliveN;
            StandLife += o.StandLife; StandLifeN += o.StandLifeN; AtkOnStand += o.AtkOnStand; DmgOnStand += o.DmgOnStand; StandPopped += o.StandPopped; StandKilled += o.StandKilled; StandRod += o.StandRod; StandBdPop += o.StandBdPop; StandAtEnd += o.StandAtEnd;
            Thrown += o.Thrown; NoTarget += o.NoTarget; ThrowShocked += o.ThrowShocked; ThrowAlready += o.ThrowAlready; ThrowThread += o.ThrowThread; ThrowThreadShocked += o.ThrowThreadShocked;
            RodCaught += o.RodCaught; RodOut += o.RodOut; RodBlocked += o.RodBlocked; RodHushed += o.RodHushed;
            foreach (var (k, v) in o.ThrowTo) ThrowTo[k] = ThrowTo.GetValueOrDefault(k) + v;
            for (int i = 0; i < 9; i++) ThrowSlot[i] += o.ThrowSlot[i];
            DestShiga += o.DestShiga; DestPopped += o.DestPopped; DestLeft += o.DestLeft; DestUsed += o.DestUsed;
            KataRod += o.KataRod; KataRodUnits += o.KataRodUnits; KataRodDry += o.KataRodDry; KataCloud += o.KataCloud; KataN += o.KataN;
            BdCounted += o.BdCounted; BdOnKill += o.BdOnKill; BdOnAnyDeath += o.BdOnAnyDeath; BdSkipped += o.BdSkipped; BdCapped += o.BdCapped;
            for (int i = 0; i < 4; i++) BdKind[i] += o.BdKind[i];
            HagiAtk += o.HagiAtk; HagiDmg += o.HagiDmg; HagiSelf += o.HagiSelf; HagiDied += o.HagiDied; HagiN += o.HagiN; HagiRecoil += o.HagiRecoil; HagiTopTurn = Math.Max(HagiTopTurn, o.HagiTopTurn);
            ShigaTurns += o.ShigaTurns; ShigaStalls += o.ShigaStalls; ShigaN += o.ShigaN; SwFires += o.SwFires; AllyPops += o.AllyPops; AllyStuns += o.AllyStuns; Lights += o.Lights; VeilAdded += o.VeilAdded;
        }
    }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var a = new St { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else { a.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) a.Caps = 1; }
        var mine = p.ToDictionary(u => u.InstanceId, u => u);
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), shiga = p.FirstOrDefault(u => u.Def.Id == "shiga"), kata = p.FirstOrDefault(u => u.Def.Id == "kata"), hagi = p.FirstOrDefault(u => u.Def.Id == "hagi");
        // 1巡目: 喚んだ獣 ／ 置物の獣（その場で弾けた）
        var summoned = new HashSet<int>(); var phantom = new HashSet<int>();
        foreach (var ev in r.Events)
        {
            if (ev.Kind == BattleEventKind.Summon && som is not null && ev.ActorId == som.InstanceId && ev.TargetId is int sid) summoned.Add(sid);
            else if (ev.Kind == BattleEventKind.BeastBurst && ev.Text is not (BeastBurstLabels.Overflow or BeastBurstLabels.Rod or BeastBurstLabels.Popped) && ev.TargetId is int bid) phantom.Add(bid);
        }
        var standing = new Dictionary<int, int>();   // 立っている獣 → 立ったT
        var flagged = new HashSet<int>();            // 裏切りの一撃で感電した味方（弾けるか、戦の終わりまで）
        int peak = 0;
        var hagiPerTurn = new Dictionary<int, int>();   // ハギの追い打ち（「踏み込む」の見せ場）/ターン
        void Leave(int id, int turn) { if (standing.Remove(id, out int t0)) { a.StandLife += turn - t0; a.StandLifeN++; } }
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Summon when ev.TargetId is int sid && summoned.Contains(sid) && !phantom.Contains(sid):
                    standing[sid] = ev.Turn;
                    if (standing.Count > peak) peak = standing.Count;
                    break;
                case BattleEventKind.BeastBurst when ev.Text == BeastBurstLabels.Rod && ev.TargetId is int rb && standing.ContainsKey(rb):
                    a.StandRod++; Leave(rb, ev.Turn); break;
                case BattleEventKind.BeastBurst when ev.Text == BeastBurstLabels.Popped && ev.TargetId is int pb0 && standing.ContainsKey(pb0):
                    a.StandBdPop++; Leave(pb0, ev.Turn); break;
                case BattleEventKind.BeastBurst when ev.Text == BeastBurstLabels.Overflow && ev.TargetId is int ob && standing.ContainsKey(ob):
                    Leave(ob, ev.Turn); break;
                case BattleEventKind.Death when ev.TargetId is int t && mine.ContainsKey(t):
                    if (a.FirstDeathN == 0) { a.FirstDeathN = 1; a.FirstDeathT = ev.Turn; }
                    if (som is not null && t == som.InstanceId && a.SomDied == 0) { a.SomDied = 1; a.SomDeathT = ev.Turn; }
                    if (hagi is not null && t == hagi.InstanceId) a.HagiDied = 1;
                    break;
                case BattleEventKind.Death when ev.TargetId is int db && standing.ContainsKey(db):
                    a.StandKilled++; Leave(db, ev.Turn); break;
                case BattleEventKind.ShockSpent when ev.TargetId is int pb && standing.ContainsKey(pb):
                    a.StandPopped++; break;
                case BattleEventKind.ShockSpent when ev.TargetId is int fa && flagged.Remove(fa):
                    a.DestPopped++; break;
                case BattleEventKind.BeastThrow when ev.TargetId is int tt && mine.TryGetValue(tt, out var tu):
                    {
                        string k = Short(tu.Def);
                        a.ThrowTo[k] = a.ThrowTo.GetValueOrDefault(k) + 1;
                        a.ThrowSlot[Math.Clamp(tu.Slot, 0, 8)]++;
                        if (ev.Text == BeastThrowLabels.Hit && ev.Amount == 1 && !flagged.Add(tt)) a.DestUsed++;   // 前の一撃の感電が弾けずに消えていた（シガの電気鞭で移した など）
                        break;
                    }
                case BattleEventKind.ShockGauge when shiga is not null && ev.TargetId == shiga.InstanceId && ev.Text == ShockGaugeLabels.ChargeGain && ev.ActorId is int ga && summoned.Contains(ga):
                    a.DestShiga++; break;
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
        foreach (int id in flagged) { if (mine[id].RawCounter(StatusKeys.Shock) > 0 && mine[id].IsAlive) a.DestLeft++; else a.DestUsed++; }
        a.PeakSum = peak;
        if (som is not null && r.TallyByUnit.TryGetValue("som", out var mt))
        {
            a.Calls = mt.BeastCalls; a.Called = mt.StandBeasts; a.Charged = mt.StandCharged; a.NoSeat = mt.StandNoSeat; a.NoFoe = mt.StandNoFoe; a.Stood = mt.StandStood;
            a.AliveSum = mt.StandAliveSum; a.AliveN = mt.StandAliveN; a.AlivePeak = mt.StandAlivePeak;
            a.Bursts = mt.BeastBursts; a.BurstUnits = mt.BeastBurstUnits; a.EmergFired = mt.EmergFired;
            a.Thrown = mt.BtThrown; a.NoTarget = mt.BtNoTarget; a.ThrowShocked = mt.BtShocked; a.ThrowAlready = mt.BtAlready; a.ThrowThread = mt.BtThread; a.ThrowThreadShocked = mt.BtThreadShocked;
            a.RodCaught = mt.BtRodCaught; a.RodOut = mt.BtRodOut; a.RodBlocked = mt.BtRodBlocked; a.RodHushed = mt.BtRodHushed;
            a.BdCounted = mt.BdCounted; a.BdOnKill = mt.BdOnKill; a.BdOnAnyDeath = mt.BdOnAnyDeath; a.BdSkipped = mt.BdSkipped; a.BdCapped = mt.BdCapped;
            if (mt.BdKind is { } bk) for (int i = 0; i < Math.Min(4, bk.Length); i++) a.BdKind[i] = bk[i];
            a.Lights = mt.SparkRainLights; a.VeilAdded = mt.SparkVeilAdded;
        }
        if (kata is not null) { a.KataN = 1; a.KataCloud = ThundercloudTrait.Of(kata); if (r.TallyByUnit.TryGetValue("kata", out var kt)) { a.KataRod = kt.RodCaught; a.KataRodUnits = kt.RodChainUnits; a.KataRodDry = kt.RodDry; } }
        if (hagi is not null) a.HagiN = 1;
        if (shiga is not null && r.TallyByUnit.TryGetValue("shiga", out var st)) { a.ShigaN = 1; a.ShigaTurns = st.TurnsTaken; a.ShigaStalls = st.TurnStalls; a.SwFires = st.SwFires; }
        foreach (var id in p.Select(u => u.Def.Id).Distinct())
            if (r.TallyByUnit.TryGetValue(id, out var ut)) { a.AllyPops += ut.ShockSpent; a.AllyStuns += ut.ShockStunned; }
        return a;
    }

    internal static St Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new St();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static int WinsLite(Formation f, S287.Wave w, int n, out long winT)
    {
        int wins = 0; long wt = 0;
        Parallel.For(0, n, s =>
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
            if (r.PlayerWon) { Interlocked.Increment(ref wins); Interlocked.Add(ref wt, r.Turns); }
        });
        winT = wt;
        return wins;
    }

    static string FoeDmgLine(St a, Formation f)
    {
        var order = f.Occupied().Select(o => Short(o.Def)).Distinct().Append("獣の放電").Append("刻み").Append("敵の側");
        return string.Join("・", order.Where(k => a.FoeDmg.GetValueOrDefault(k) > 0).Select(k => $"{k} {Per0(a.FoeDmg[k], a.Turns)}"));
    }

    static string Top(Dictionary<string, long> d, long total, int k = 3) =>
        d.Count == 0 || total == 0 ? "—" : string.Join("・", d.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Take(k).Select(x => $"{x.Key} {Pct(x.Value, total).Replace("%", "")}%"));

    // ---------------------------------------------------------------------------------
    // Phase 0（§4）
    // ---------------------------------------------------------------------------------
    sealed class P0St
    {
        public long N, Wins, Turns, Bursts, Emerg, ShockOnAllyByFoe, ShockOnAllyAll, AllyPops, AllyStuns, AllyStalls, ShigaPops, ShigaStuns;
        public Dictionary<string, long> FoeWriters = new(), AllyWriters = new(), PopsBy = new();
        public void Merge(P0St o)
        {
            N += o.N; Wins += o.Wins; Turns += o.Turns; Bursts += o.Bursts; Emerg += o.Emerg; ShockOnAllyByFoe += o.ShockOnAllyByFoe; ShockOnAllyAll += o.ShockOnAllyAll;
            AllyPops += o.AllyPops; AllyStuns += o.AllyStuns; AllyStalls += o.AllyStalls; ShigaPops += o.ShigaPops; ShigaStuns += o.ShigaStuns;
            foreach (var (k, v) in o.FoeWriters) FoeWriters[k] = FoeWriters.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.AllyWriters) AllyWriters[k] = AllyWriters.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.PopsBy) PopsBy[k] = PopsBy.GetValueOrDefault(k) + v;
        }
    }

    static P0St P0Measure(Formation f, S287.Wave w)
    {
        var parts = new P0St[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = w.Make();
            var r = BattleEngine.Run(p, e, i, verbose: true);
            var a = new P0St { N = 1, Turns = r.Turns, Wins = r.PlayerWon ? 1 : 0 };
            var mine = p.ToDictionary(u => u.InstanceId, u => u);
            var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
            foreach (var ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.StatusGain && ev.Text == StatusKeys.Shock && ev.TargetId is int t && mine.ContainsKey(t))
                {
                    a.ShockOnAllyAll++;
                    string wn = ev.ActorId is int wa && names.TryGetValue(wa, out var s0) ? s0 : ev.ActorId is null ? "（出どころなし）" : "（盤の外）";
                    if (ev.ActorId is int wb && mine.ContainsKey(wb)) a.AllyWriters[wn] = a.AllyWriters.GetValueOrDefault(wn) + 1;
                    else { a.ShockOnAllyByFoe++; a.FoeWriters[wn] = a.FoeWriters.GetValueOrDefault(wn) + 1; }
                }
                else if (ev.Kind == BattleEventKind.ShockSpent && ev.TargetId is int pt && mine.TryGetValue(pt, out var pu))
                {
                    string k = Short(pu.Def);
                    a.PopsBy[k] = a.PopsBy.GetValueOrDefault(k) + 1;
                }
            }
            foreach (var id in p.Select(u => u.Def.Id).Distinct())
                if (r.TallyByUnit.TryGetValue(id, out var ut))
                {
                    a.AllyPops += ut.ShockSpent; a.AllyStuns += ut.ShockStunned; a.AllyStalls += ut.TurnStalls;
                    if (id == "shiga") { a.ShigaPops = ut.ShockSpent; a.ShigaStuns = ut.ShockStunned; }
                }
            if (r.TallyByUnit.TryGetValue("som", out var mt)) { a.Bursts = mt.BeastBursts; a.Emerg = mt.EmergFired; }
            parts[i] = a;
        });
        var all = new P0St();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static IEnumerable<Trait> AllTraits()
    {
        foreach (var id in Enum.GetValues<TraitId>())
        {
            Trait? t = null;
            try { t = TraitCatalog.Get(id); } catch (KeyNotFoundException) { }
            if (t is not null) yield return t;
        }
    }

    sealed class CensusSt { public long N, Wins, Stood, KataW, RodW; public long[] W = new long[9]; }

    static CensusSt CensusMeasure(Formation f, S287.Wave w)
    {
        var all = new CensusSt();
        var lk = new object();
        Parallel.For(0, Seeds, i =>
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), i, verbose: false);
            lock (lk)
            {
                all.N++; if (r.PlayerWon) all.Wins++;
                if (!r.TallyByUnit.TryGetValue("som", out var t)) return;
                all.Stood += t.ThrowCensusN; all.KataW += t.ThrowCensusKataW; all.RodW += t.ThrowCensusRodW;
                if (t.ThrowCensusW is { } cw) for (int k = 0; k < 9; k++) all.W[k] += cw[k];
            }
        });
        return all;
    }

    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var cells = new List<(string B, Formation F, S287.Wave W, P0St S)>();
        foreach (var (n, f) in boards) foreach (var w in Waves) cells.Add((n, f, w, P0Measure(f, w)));
        Console.WriteLine($"# 第317期 Phase 0（§4）—— 規定（ソム ＝ E2 ＋ 群れ ＋ 緊急 K-a・ドハ ＝ DP-s・シガ ＝ SF-AC）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        foreach (var (n, f) in boards) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("## §4-1 いまの「敵の手でこちらに付く感電」（規定・/戦）");
        Console.WriteLine();
        Console.WriteLine("味方に新しく付いた感電（`StatusGain` 感電）を書き手で分けた。敵の手 ＝ 書き手が味方でない（敵の駒 ／ 盤の外 ／ 出どころなし）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 味方に付いた感電 | うち敵の手 | 敵の手の書き手 | 味方の書き手（上位3） |");
        Console.WriteLine("|---|---|--:|--:|--:|---|---|");
        foreach (var c in cells)
        {
            var s = c.S;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Pct(s.Wins, s.N)} | {Per2(s.ShockOnAllyAll, s.N)} | {Per2(s.ShockOnAllyByFoe, s.N)} | {(s.FoeWriters.Count == 0 ? "—" : string.Join("・", s.FoeWriters.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {Per2(x.Value, s.N)}")))} | {string.Join("・", s.AllyWriters.OrderByDescending(x => x.Value).Take(3).Select(x => $"{x.Key} {Per2(x.Value, s.N)}"))} |");
        }
        long foeAll = cells.Sum(c => c.S.ShockOnAllyByFoe);
        Console.WriteLine();
        Console.WriteLine($"敵の手でこちらに付いた感電の合計: **{foeAll}**（{cells.Count} セル × {Seeds} 戦）。");

        Console.WriteLine();
        Console.WriteLine("## §4-2 裏切りの一撃の相手の見込み（SB-a の立った獣・計数器 `ThrowCensus`・狙える味方に等分・介入は数えない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率（SB-a） | 立った獣 /戦 | 前1 | 前3 | 中央 | 後1 | 後3 | カタ自身 | **カタ自身か隣（BT-b の範囲）** |");
        Console.WriteLine("|---|---|--:|--:|---|---|---|---|---|--:|--:|");
        BattleContext.ThrowCensus = true;
        try
        {
            foreach (var (n, f) in boards)
            {
                var fs = Apply(f, V("SB-a"));
                string Nm(int slot) => f[slot] is { } d ? Short(d) : "—";
                foreach (var w in Waves)
                {
                    var c = CensusMeasure(fs, w);
                    long tot = c.W.Sum();
                    string S(int k) => tot == 0 ? "—" : $"{Nm(k)} {Pct(c.W[k], tot).Replace("%", "")}";
                    bool hasKata = f.Occupied().Any(o => o.Def.Id == "kata");
                    Console.WriteLine($"| {n} | {w.Name} | {Pct(c.Wins, c.N)} | {Per1(c.Stood, c.N)} | {S(0)} | {S(1)} | {S(2)} | {S(3)} | {S(4)} | {(hasKata ? Pct(c.KataW, tot) : "—")} | {(hasKata ? Pct(c.RodW, tot) : "—")} |");
                }
            }
        }
        finally { BattleContext.ThrowCensus = false; }
        Console.WriteLine();
        Console.WriteLine("（席の % ＝ 立った獣1体ごとに、狙える味方（前列が生きていれば前列）へ等分した重みの割合。召喚枠の味方は 0。庇い ／ 標 ／ 挑発で差し替わる分は入らない。）");

        Console.WriteLine();
        Console.WriteLine("## §4-4 こちらの感電の行き先（規定・/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 味方の感電が弾けた | 弾けた駒（上位3） | それで痺れた（＝失った手番） | 味方の失った手番（全原因） | シガ 弾けた ／ 痺れた |");
        Console.WriteLine("|---|---|--:|---|--:|--:|---|");
        foreach (var c in cells)
        {
            var s = c.S;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per2(s.AllyPops, s.N)} | {string.Join("・", s.PopsBy.OrderByDescending(x => x.Value).Take(3).Select(x => $"{x.Key} {Per2(x.Value, s.N)}"))} | {Per2(s.AllyStuns, s.N)} | {Per2(s.AllyStalls, s.N)} | {(c.F.Occupied().Any(o => o.Def.Id == "shiga") ? $"{Per2(s.ShigaPops, s.N)} ／ {Per2(s.ShigaStuns, s.N)}" : "—")} |");
        }

        Console.WriteLine();
        Console.WriteLine("## §4-5 死の読み手（「敵を倒した」を読む札）と、獣を数えたときの見込み");
        Console.WriteLine();
        var hooks = new[] { "OnKill", "OnAnyDeath", "OnAllyDeath", "OnDeath" };
        Console.WriteLine("| 口 | 札（`Trait` の上書き） | 保持者（`UnitCatalog.All`） | BD で渡すか |");
        Console.WriteLine("|---|---|---|---|");
        var traitTypes = typeof(Trait).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Trait)) && !t.IsAbstract).ToList();
        foreach (var h in hooks)
        {
            foreach (var tt in traitTypes.Where(t => t.GetMethod(h)?.DeclaringType == t).OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                var ids = AllTraits().Where(x => x.GetType() == tt).Select(x => x.Id).ToHashSet();
                var holders = UnitCatalog.All.Where(d => d.Traits.Any(ids.Contains)).Select(Short).ToList();
                bool skip = ids.Any(id => Array.IndexOf(BattleContext.BeastDeathSkips, id) >= 0);
                string pass = h switch { "OnKill" => skip ? "**外す**（撃破数で際限なく育つ）" : "渡す（倒した駒＝起こし手）", "OnAnyDeath" => skip ? "**外す**（撃破数で際限なく育つ）" : "渡す（ソムの側だけ）", _ => "渡さない（獣の側の口）" };
                Console.WriteLine($"| {h} | {tt.Name}（{string.Join(",", ids)}） | {(holders.Count == 0 ? "—" : string.Join("・", holders))} | {pass} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("BD で数える獣の見込み（規定 E2 の弾けた獣 /戦・緊急を含む）と、代表台にいる死の読み手（ソムの側）:");
        Console.WriteLine();
        Console.WriteLine("| 台 | ボス | 近衛 | 大隊 | 九体 新兵 | 九体 農兵 | 本編 第1〜5波 | 台にいる読み手 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|---|");
        foreach (var (n, f) in boards)
        {
            var row = cells.Where(c => c.B == n).ToList();
            var readers = f.Occupied().Select(o => o.Def).Where(d => d.Traits.Any(id => TraitCatalog.Get(id) is var tr && (tr.GetType().GetMethod("OnKill")?.DeclaringType == tr.GetType() || tr.GetType().GetMethod("OnAnyDeath")?.DeclaringType == tr.GetType()))).Select(Short).ToList();
            Console.WriteLine($"| {n} | {string.Join(" | ", row.Take(5).Select(c => Per1(c.S.Bursts, c.S.N)))} | {string.Join(" ／ ", row.Skip(5).Select(c => Per1(c.S.Bursts, c.S.N)))} | {(readers.Count == 0 ? "—" : string.Join("・", readers))} |");
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
            ctl[(c.B, c.W.Name, c.V.Name)] = F1(100.0 * WinsLite(S309.Swap(fv, c.V.Som, UnitCatalog.Dolga), c.W, Seeds, out _) / Seeds);
        }

        Console.WriteLine($"# 第317期 §5-2 代表台 × 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}・seed 0..{Seeds - 1}・verbose）");
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

        foreach (var w in Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 寿命 | ソム 倒れたT | 獣 喚んだ ・ その場で弾けた（隣帯電 ／ 枠なし ／ 敵なし）・ **立った** ・ 避雷針で弾けた ・ 獣の死で消えた ・ 倒された ・ 残った | 同時に立つ 最大 ／ 戦の最大の平均 ／ 喚び出し後の平均 | 立っていたT | 立った獣への一撃 ・ 量 | 投げた ・ 感電が付いた ／ 既に ・ 糸（付いた）・ 避雷針（範囲外 ／ 止まった・うち粛） | 狙われた（上位3） | 行き先 シガの蓄電 ／ 弾けた ／ 残った ／ 使われた | 避雷針 連鎖の駒 /回 | カタ 雷雲 | BD 数えた（場 ／ 緊急 ／ 立ち ／ 針）・ハギ 追い打ち（1T の最多）・ 与ダメ ・ 反動の回数 ／ HP が減った自傷 ・ 倒れた | シガ 手番 ／ 失った | 味方の感電 弾けた ／ 痺れた | 光 ・ 衣 |"
                + (w.Boss ? " 味方の与ダメ /T ・ 勇者の回復 /T ・ 差 |" : ""));
            Console.WriteLine("|---|---|--:|---|---|---|---|--:|---|---|---|---|--:|--:|---|---|---|---|" + (w.Boss ? "---|" : ""));
            foreach (var c in cells.Where(x => x.W == w))
            {
                var a = c.S;
                long spot = a.Called == 0 ? a.Bursts : a.Charged + a.NoSeat + a.NoFoe;
                string beast = $"{Per1(a.Called == 0 ? a.Calls : a.Called, a.N)} ・ {Per1(spot, a.N)}（{Per1(a.Charged, a.N)} ／ {Per1(a.NoSeat, a.N)} ／ {Per1(a.NoFoe, a.N)}）・ **{Per1(a.Stood, a.N)}** ・ {Per1(a.StandRod, a.N)} ・ {Per1(a.StandBdPop, a.N)} ・ {Per1(a.StandKilled, a.N)} ・ {Per1(a.StandAtEnd, a.N)}";
                string conc = a.AliveN == 0 ? "—" : $"{a.AlivePeak} ／ {Per1(a.PeakSum, a.N)} ／ {Per1(a.AliveSum, a.AliveN)}";
                string th = a.Thrown == 0 ? "—" : $"{Per1(a.Thrown, a.N)} ・ {Per1(a.ThrowShocked, a.N)} ／ {Per1(a.ThrowAlready, a.N)} ・ {Per1(a.ThrowThread, a.N)}（{Per1(a.ThrowThreadShocked, a.N)}）・ {Per1(a.RodCaught, a.N)}（{Per1(a.RodOut, a.N)} ／ {Per1(a.RodBlocked, a.N)}・{Per1(a.RodHushed, a.N)}）";
                string dest = a.ThrowShocked == 0 ? "—" : $"{Per1(a.DestShiga, a.N)} ／ {Per1(a.DestPopped, a.N)} ／ {Per1(a.DestLeft, a.N)} ／ {Per1(a.DestUsed, a.N)}";
                string bd = a.BdCounted == 0 ? (a.HagiN > 0 ? $"— ・ {Per1(a.HagiAtk, a.N)}（{a.HagiTopTurn}）・ {Per0(a.HagiDmg, a.N)} ・ {Per1(a.HagiRecoil, a.N)} ／ {Per1(a.HagiSelf, a.N)} ・ {Pct(a.HagiDied, a.N)}" : "—")
                    : $"{Per1(a.BdCounted, a.N)}（{string.Join(" ／ ", a.BdKind.Select(x => Per1(x, a.N)))}{(a.BdCapped > 0 ? $"・上限で外した {Per1(a.BdCapped, a.N)}" : "")}）" + (a.HagiN > 0 ? $"・ {Per1(a.HagiAtk, a.N)}（{a.HagiTopTurn}）・ {Per0(a.HagiDmg, a.N)} ・ {Per1(a.HagiRecoil, a.N)} ／ {Per1(a.HagiSelf, a.N)} ・ {Pct(a.HagiDied, a.N)}" : "");
                var fv = Apply(c.F0, c.V);
                Console.WriteLine($"| {c.B} | {c.V.Name} | {F1(a.Win)} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Dt(a.SomDied, a.SomDeathT, a.N)} | {beast} | {conc} | {Per1(a.StandLife, a.StandLifeN)} | {Per1(a.AtkOnStand, a.N)} ・ {Per0(a.DmgOnStand, a.N)} | {th} | {Top(a.ThrowTo, a.ThrowTo.Values.Sum())} | {dest} | "
                    + $"{(a.KataRod == 0 ? "—" : Per1(a.KataRodUnits, a.KataRod))} | {(a.KataN > 0 ? Per1(a.KataCloud, a.KataN) : "—")} | {bd} | {(a.ShigaN > 0 ? $"{Per1(a.ShigaTurns, a.ShigaN)} ／ {Per1(a.ShigaStalls, a.ShigaN)}" : "—")} | {Per1(a.AllyPops, a.N)} ／ {Per1(a.AllyStuns, a.N)} | {Per1(a.Lights, a.N)} ・ {Per0(a.VeilAdded, a.N)} |"
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
        var rows = CompareBuildsH317().ToArray();
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
        Console.WriteLine($"# 第317期 §5-3 `compare` {rows.Length} 行 × 版（seed 0..{Seeds - 1}・基準は規定）");
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
                bool holds = rows[ri].F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.SomH317));
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
            Console.WriteLine($"動いた行 {movedRows} ／ セル {movedCells}・ソムがいない行で動いたセル **{outside}**（カタだけの行・ハギだけの行を含む）・全{rows.Length}行の第2〜5波平均 {F1(allB)} → {F1(all)}（{Sg(all - allB)}）・主判定19行の第五波 {F1(p5b)} → {F1(p5)}（歯止め {F1(Baseline.PrimaryFifthFloor)}）");
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
    // §5-4 ソムの格子（第311期と同じ作り・駒は規定。版のカタは固定枠のカタに当てる）
    // ---------------------------------------------------------------------------------
    static void Grid(string ver, string wave)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = V(ver);
        var w = S307.WaveOf(wave);
        UnitDef[] fixedBase = w.Boss ? new[] { UnitCatalog.Kugu, UnitCatalog.Kata } : new[] { UnitCatalog.Tou };
        var pool = (w.Boss ? S307.BossPool : S307.ElitePool).Select(Cur).ToArray();
        int k = w.Boss ? 2 : 3, need = w.Boss ? 1 : 5;
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedBase.Append(UnitCatalog.SomH317).Concat(t).ToArray()));
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
        Console.WriteLine($"# 第317期 §5-4 ソムの格子 × {w.Name} × 版 {v.Name}（固定枠 {S307.OrderName(fixedBase)} ＋ ソム・探索枠{k}・{boards.Count:N0} 台）");
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
    // §6 台本の例
    // ---------------------------------------------------------------------------------
    static void Find()
    {
        Console.WriteLine("# som317 find —— 裏切りの一撃（`BeastThrow`）・避雷針（`BeastThrow 避雷針` → `BeastBurst 避雷針`）・糸 のある seed");
        Console.WriteLine();
        foreach (var vn in new[] { "BT-a", "BT-b", "BD-BTb" })
            foreach (var (bn, f0) in Boards().Where(b => b.Name is "光の盾" or "光の盾 鞭" or "火の型 ソラ→ソム" or HagiBoard))
                foreach (var wn in new[] { "boss", "guard", "3" })
                {
                    var w = WaveOf(wn); var f = Apply(f0, V(vn));
                    for (int sd = 0, found = 0; sd < 20 && found < 1; sd++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var th = r.Events.Where(x => x.Kind == BattleEventKind.BeastThrow).ToList();
                        if (th.Count == 0) continue;
                        found++;
                        string T(string lb) { var ts = th.Where(x => x.Text == lb).Select(x => x.Turn).Distinct().Take(5).ToList(); return ts.Count == 0 ? "—" : "T" + string.Join(",", ts); }
                        Console.WriteLine($"- {vn} × {bn} × {w.Name} × seed {sd}: 投げた {T(BeastThrowLabels.Hit)}・避雷針 {T(BeastThrowLabels.Rod)}・糸 {T(BeastThrowLabels.Thread)}（{(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}）");
                    }
                }
    }

    /// <summary>別の seed の帯（<paramref name="from"/> から <paramref name="n"/> 戦）で、規定と版の勝率を並べる（差が seed の当たり外れかを見る）。</summary>
    static void Noise(string ver, string part, string wave, int from, int n)
    {
        var v = V(ver);
        var (name, f0) = Boards().First(b => b.Name.Contains(part, StringComparison.Ordinal));
        var w = WaveOf(wave);
        int Wins(Formation f) { int k = 0; Parallel.For(from, from + n, s => { if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false).PlayerWon) Interlocked.Increment(ref k); }); return k; }
        int b = Wins(f0), a = Wins(Apply(f0, v));
        Console.WriteLine($"| {name} | {w.Name} | seed {from}..{from + n - 1} | 規定 {F1(100.0 * b / n)} | {v.Name} {F1(100.0 * a / n)} | 差 {Sg(100.0 * (a - b) / n)} |");
    }

    static void Memo(string ver, string part, string wave, int seed, int t0, int t1)
    {
        var v = V(ver);
        var (name, f0) = Boards().First(b => b.Name.Contains(part, StringComparison.Ordinal));
        var f = Apply(f0, v);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "背いた獣");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# som317 memo —— {name}（{v.Name}・{Seats(f)}）× {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・T{t0}〜T{t1}");
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

    static List<UnitState> Wave3() => BattleEngine.Materialize(EnemyCatalog.Stages[2].Enemy, BattleContext.EnemyTeam);
    static void TurnStart(BattleContext ctx, UnitState som) { foreach (var t in som.Traits.ToList()) t.OnTurnStart(ctx, som); }
    static List<UnitState> Stood(BattleContext ctx, UnitState som) => ctx.AllUnits.Where(u => u.IsAlive && BetrayedTrait.IsFodder(u) && u.RawCounter(BattleContext.StandOwnerKey) == som.InstanceId + 1).ToList();
    static List<BattleEvent> After(BattleContext ctx, int ev0) => ctx.Events.Skip(ev0).ToList();

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som317 check —— 第317期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef bta = UnitCatalog.SomBTa;

        // (a) 段0: 規定のドハ ＝ DP-s・規定のシガ ＝ SF-AC・旧の定義が残っている
        {
            var doha = UnitCatalog.Doha; var h316 = UnitCatalog.DohaH316; var dps = UnitCatalog.DohaDPs;
            bool d = doha.Traits.SequenceEqual(dps.Traits) && doha.Actions is { Count: 1 } && doha.Actions[0].Kind == ActionKind.Skill && ReferenceEquals(doha.Actions, dps.Actions)
                && doha.PlusText == dps.PlusText && doha.MinusText == dps.MinusText && doha.MaxHp == 104 && doha.Attack == 4 && doha.Speed == 3
                && h316.Traits.SequenceEqual(new[] { TraitId.Sharer, TraitId.SharerArmored, TraitId.ShareBack, TraitId.SharerNoDull }) && h316.Actions is null
                && dps.Traits.SequenceEqual(h316.Traits.Concat(new[] { TraitId.SharePush, TraitId.SharePushPower, TraitId.SharePushStrikers }))
                && UnitCatalog.DohaDPb.Traits.SequenceEqual(h316.Traits.Concat(new[] { TraitId.SharePush, TraitId.SharePushPower }))
                && UnitCatalog.All.Contains(doha) && !UnitCatalog.Everyone.Contains(h316) && !UnitCatalog.Everyone.Contains(dps);
            var sh = UnitCatalog.Shiga; var swa = UnitCatalog.ShigaSWa;
            bool s = sh.Traits.SequenceEqual(swa.Traits.Concat(new[] { TraitId.FearNoStun, TraitId.ScourgeDaunt })) && sh.MaxHp == 52 && sh.Attack == 9 && sh.Speed == 3 && sh.Pattern == AttackPattern.Sweep
                && sh.PlusText == swa.PlusText + "。自分の雷では痺れない" && sh.MinusText == "感電していないとき、動ける敵を主目標に打つと、怖気づいて自分が1ターン動けなくなる（萎縮した敵は動けない敵とみなす）"
                && swa.MinusText == UnitCatalog.ShigaMinusH316 && swa.Traits.Count == 8 && swa.Traits[^1] == TraitId.ShockWhipChain
                && UnitCatalog.ShigaSFa.Traits.SequenceEqual(swa.Traits.Append(TraitId.FearNoStun)) && UnitCatalog.ShigaSGf.Traits.SequenceEqual(swa.Traits.Append(TraitId.ShameSkipFodder))
                && UnitCatalog.All.Contains(sh) && !UnitCatalog.Everyone.Contains(swa) && UnitCatalog.All.Count == 52;
            bool pin = ReferenceEquals(PinDS316(UnitCatalog.Doha), h316) && ReferenceEquals(PinDS316(UnitCatalog.Shiga), swa) && ReferenceEquals(PinKataTou311(UnitCatalog.Shiga), swa)
                && Pin316(Presets.Playtest.First(r => r.Name == "試遊・感電 雷の型").F).Occupied().Any(o => ReferenceEquals(o.Def, h316));
            Expect("(a) 段0: 規定のドハ ＝ DP-s（札 ／ 手番 ／ 文面が `DohaDPs` と同じ）・規定のシガ ＝ SF-AC（SW-a ＋ `FearNoStun` ＋ `ScourgeDaunt`・文面）・旧の定義 `DohaH316` ／ `ShigaSWa` が `Everyone` の外に残り、版はそこから作る・`Pin316` が旧へ替える",
                d && s && pin, $"ドハ {d}・シガ {s}・固定 {pin}");
        }
        // (b) BT-a: 立った獣が直後に1体1回だけ投げる・当たった味方に感電（ダメージ 0）・既に感電していれば何も起きない・シガの蓄電が +1
        {
            // 盤: 前1 シガだけ（狙える味方はシガ1体）・後3 ソム（BT-a）
            var f = Formation.Build(front1: UnitCatalog.Shiga, back3: bta);
            var (ctx, p, e) = Board(f, Wave3());
            var som = p.First(u => u.Def.Id == "som"); var sh = p.First(u => u.Def.Id == "shiga");
            int hp0 = sh.Hp, c0 = StoredChargeTrait.Of(sh);
            int ev0 = ctx.Events.Count;
            TurnStart(ctx, som);
            var ev = After(ctx, ev0);
            var throws = ev.Where(x => x.Kind == BattleEventKind.BeastThrow).ToList();
            var st = Stood(ctx, som);
            bool b1 = st.Count == 1 && throws.Count == 1 && throws[0].ActorId == st[0].InstanceId && throws[0].TargetId == sh.InstanceId && throws[0].Text == BeastThrowLabels.Hit && throws[0].Amount == 1
                && sh.RawCounter(StatusKeys.Shock) > 0 && sh.Hp == hp0 && StoredChargeTrait.Of(sh) == c0 + 1 && !ev.Any(x => x.Kind == BattleEventKind.Damage && x.TargetId == sh.InstanceId)
                && T(ctx, som).BtThrown == 1 && T(ctx, som).BtShocked == 1;
            // 2回目: 新しい獣だけが投げる（前の獣は投げない）・シガは既に感電 → 何も起きない（蓄電もそのまま）
            int c1 = StoredChargeTrait.Of(sh);
            ev0 = ctx.Events.Count;
            TurnStart(ctx, som);
            var ev2 = After(ctx, ev0);
            var throws2 = ev2.Where(x => x.Kind == BattleEventKind.BeastThrow).ToList();
            var st2 = Stood(ctx, som);
            bool b2 = st2.Count == 2 && throws2.Count == 1 && throws2[0].ActorId != st[0].InstanceId && throws2[0].Amount == 0 && StoredChargeTrait.Of(sh) == c1 && T(ctx, som).BtAlready == 1;
            Expect("(b) BT-a: 立った獣が立った直後に1体1回だけ投げる・当たった味方に感電が付く（ダメージ 0）・既に感電していれば何も起きない・シガの蓄電が +1（盤を直に組む）", b1 && b2,
                $"1回目 {b1}（立った {st.Count}・投げた {throws.Count}・蓄電 {c0}→{StoredChargeTrait.Of(sh)}）・2回目 {b2}（立った {st2.Count}・投げた {throws2.Count}）");
        }
        // (c) BT-a: 獣の席から敵の普通の攻撃と同じ選び方（前列が生きていれば前列）・クグの糸は糸の先の敵へ流れる
        {
            var f = Formation.Build(front1: UnitCatalog.Golm, back1: UnitCatalog.Tou, back3: bta);
            var (ctx, p, e) = Board(f, Wave3());
            var som = p.First(u => u.Def.Id == "som"); var golm = p.First(u => u.Def.Id == "golm"); var tou = p.First(u => u.Def.Id == "tou");
            TurnStart(ctx, som);
            bool front = golm.RawCounter(StatusKeys.Shock) > 0 && tou.RawCounter(StatusKeys.Shock) == 0;
            // クグ: 前1 クグだけ・組み付いた敵（敵の前列の1体）が生きている
            var f2 = Formation.Build(front1: UnitCatalog.Kugu, back3: bta);
            var (ctx2, p2, e2) = Board(f2, Wave3());
            var som2 = p2.First(u => u.Def.Id == "som"); var kugu = p2.First(u => u.Def.Id == "kugu");
            var foe = e2.First(u => u.IsAlive);
            kugu.SetCounter(GrappleTrait.TargetKey, foe.InstanceId + 1);
            bool fresh = foe.RawCounter(StatusKeys.Shock) == 0;
            int ev0 = ctx2.Events.Count;
            TurnStart(ctx2, som2);
            var th = After(ctx2, ev0).Where(x => x.Kind == BattleEventKind.BeastThrow).ToList();
            bool thread = fresh && th.Count == 1 && th[0].Text == BeastThrowLabels.Thread && th[0].PartnerId == foe.InstanceId && kugu.RawCounter(StatusKeys.Shock) == 0 && foe.RawCounter(StatusKeys.Shock) > 0
                && T(ctx2, som2).BtThread == 1 && T(ctx2, som2).BtThreadShocked == 1;
            Expect("(c) BT-a: 相手は敵の普通の攻撃と同じ選び方（前列のゴルムが生きていれば後列のトウは狙わない）・クグに向かった雷は受けずに糸の先の敵へ流れて感電させる", front && thread,
                $"前列 {front}・糸 {thread}（{(th.Count > 0 ? th[0].Text : "—")}）");
        }
        // (d) BT-b: カタ自身 ／ 隣の味方に向かう雷だけ受け止める・狙われた味方に感電が付かない・投げた獣が弾ける（起こし手はカタ・光になる）・痺れ ／ 倒れていれば働かない・範囲の外は受け止めない
        {
            var shape = FormationShape.X;
            // 前列のどちらかに1体だけ置き、カタはその隣の席 ／ 隣でない席に置く
            int tgt = 0;
            int adj = Enumerable.Range(0, 5).First(s => s != tgt && FormationRules.RowOf(s) != Row.Front && shape.AreAdjacent(tgt, s));
            int far = Enumerable.Range(0, 5).First(s => s != tgt && FormationRules.RowOf(s) != Row.Front && !shape.AreAdjacent(tgt, s));
            (BattleContext, UnitState, UnitState, UnitState, List<UnitState>) Make(int kataSlot, UnitDef kataDef)
            {
                var f = new Formation();
                f[tgt] = UnitCatalog.Golm; f[kataSlot] = kataDef; f[kataSlot == 4 ? 3 : 4] = bta;
                var (c, p, e) = Board(f, Wave3());
                foreach (var u in p) u.Hp = u.MaxHp / 2;   // 光が降る先
                return (c, p.First(u => u.Def.Id == "som"), p.First(u => u.Def.Id == "golm"), p.First(u => u.Def.Id == "kata"), e);
            }
            // d1 隣: 受け止める
            var (c1, s1, g1, k1, _) = Make(adj, UnitCatalog.KataLR);
            int ev0 = c1.Events.Count;
            TurnStart(c1, s1);
            var ev = After(c1, ev0);
            var beastId = ev.First(x => x.Kind == BattleEventKind.BeastThrow).ActorId;
            bool d1 = ev.Any(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Rod && x.PartnerId == k1.InstanceId && x.TargetId == g1.InstanceId)
                && ev.Any(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Rod && x.ActorId == k1.InstanceId && x.TargetId == beastId)
                && ev.Any(x => x.Kind == BattleEventKind.ShockSpent && x.TargetId == beastId && x.ActorId == k1.InstanceId && x.Slot == 0)
                && ev.Any(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Release)
                && !ev.Any(x => x.Kind == BattleEventKind.Death && x.TargetId == beastId)
                && g1.RawCounter(StatusKeys.Shock) == 0 && Stood(c1, s1).Count == 0 && T(c1, k1).RodCaught == 1 && T(c1, s1).BtRodCaught == 1;
            // d2 カタ自身が狙われる（前1 にカタだけ）
            var f2 = Formation.Build(front1: UnitCatalog.KataLR, back3: bta);
            var (c2, p2, _) = Board(f2, Wave3());
            var s2 = p2.First(u => u.Def.Id == "som"); var k2 = p2.First(u => u.Def.Id == "kata");
            TurnStart(c2, s2);
            bool d2 = T(c2, s2).BtRodCaught == 1 && k2.RawCounter(StatusKeys.Shock) == 0;
            // d3 範囲の外: 受け止めない
            var (c3, s3, g3, k3, _) = Make(far, UnitCatalog.KataLR);
            TurnStart(c3, s3);
            bool d3 = T(c3, s3).BtRodCaught == 0 && T(c3, s3).BtRodOut == 1 && g3.RawCounter(StatusKeys.Shock) > 0 && Stood(c3, s3).Count == 1;
            // d4 痺れていれば働かない
            var (c4, s4, g4, k4, _) = Make(adj, UnitCatalog.KataLR);
            k4.SetCounter(StatusKeys.Stun, 1);
            TurnStart(c4, s4);
            bool d4 = T(c4, s4).BtRodCaught == 0 && T(c4, s4).BtRodBlocked == 1 && g4.RawCounter(StatusKeys.Shock) > 0;
            // d5 倒れていれば働かない
            var (c5, s5, g5, k5, _) = Make(adj, UnitCatalog.KataLR);
            k5.Hp = 0;
            TurnStart(c5, s5);
            bool d5 = T(c5, s5).BtRodCaught == 0 && g5.RawCounter(StatusKeys.Shock) > 0;
            // d6 規定のカタ（避雷針なし）は受け止めない
            var (c6, s6, g6, _, _) = Make(adj, UnitCatalog.Kata);
            TurnStart(c6, s6);
            bool d6 = T(c6, s6).BtRodCaught == 0 && g6.RawCounter(StatusKeys.Shock) > 0;
            Expect("(d) BT-b: カタ自身 ／ 隣の味方に向かう雷だけ受け止める（`BeastThrow 避雷針` → `BeastBurst 避雷針` → 獣が起点・起こし手はカタ・`Death` なし・光が降る）・狙われた味方に感電が付かない・範囲の外 ／ 痺れ ／ 倒れていれば働かない・規定のカタは受け止めない",
                d1 && d2 && d3 && d4 && d5 && d6, $"隣 {d1}（席 {tgt}↔{adj}）・自身 {d2}・範囲外 {d3}（席 {far}）・痺れ {d4}・倒れた {d5}・規定 {d6}");
        }
        // (e) BT-c: 全員の雷を受け止める（範囲の外の席でも）
        {
            var shape = FormationShape.X;
            int far = Enumerable.Range(0, 5).First(s => s != 0 && FormationRules.RowOf(s) != Row.Front && !shape.AreAdjacent(0, s));
            var f = new Formation(); f[0] = UnitCatalog.Golm; f[far] = UnitCatalog.KataLRall; f[far == 4 ? 3 : 4] = bta;
            var (ctx, p, _) = Board(f, Wave3());
            var som = p.First(u => u.Def.Id == "som"); var g = p.First(u => u.Def.Id == "golm");
            TurnStart(ctx, som);
            Expect("(e) BT-c: 範囲の外の席の味方に向かう雷も受け止める", T(ctx, som).BtRodCaught == 1 && g.RawCounter(StatusKeys.Shock) == 0, $"受け止めた {T(ctx, som).BtRodCaught}");
        }
        // (f) 粛（実戦: 本編 第2波 × BT-b の光の盾）: 避雷針が粛で止まる
        {
            long caught = 0, hushed = 0, blocked = 0;
            var fs = Apply(Playtest(S309.Shield), V("BT-b"));
            for (int sd = 0; sd < 60; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(fs, BattleContext.PlayerTeam), Waves[6].Make(), sd, verbose: false);
                var t = r.TallyByUnit["som"]; caught += t.BtRodCaught; hushed += t.BtRodHushed; blocked += t.BtRodBlocked;
            }
            Expect("(f) BT-b: 避雷針は粛で止まる（本編 第2波・粛の伝令 × 光の盾 × seed 0..59・止まった数を数えるだけ）", caught > 0 && hushed > 0 && hushed <= blocked, $"受け止めた {caught}・止まった {blocked}（うち粛 {hushed}）");
        }
        // (g) BD: 獣の弾けでハギの追い打ちが出る・外した読み手（処刑 ／ 墓守）は発火しない・規定（E2）では出ない
        {
            var necroHolder = UnitCatalog.All.FirstOrDefault(d => d.Traits.Contains(TraitId.Necro));
            (long Atk, int NecroAtk, long Counted) One(UnitDef somDef)
            {
                var f = Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Hagi, back1: necroHolder ?? UnitCatalog.Tou, back3: somDef);
                var (ctx, p, _) = Board(f, Wave3());
                var som = p.First(u => u.Def.Id == "som");
                var nh = necroHolder is null ? null : p.First(u => u.Def.Id == necroHolder.Id);
                int a0 = nh?.AtkBonus ?? 0;
                int ev0 = ctx.Events.Count;
                TurnStart(ctx, som);
                var hagi = p.First(u => u.Def.Id == "hagi");
                long atk = After(ctx, ev0).Count(x => x.Kind == BattleEventKind.Attack && x.ActorId == hagi.InstanceId);
                return (atk, (nh?.AtkBonus ?? 0) - a0, T(ctx, som).BdCounted);
            }
            var e2 = One(UnitCatalog.SomH317); var bd = One(UnitCatalog.SomBD);
            bool ok = e2.Atk == 0 && e2.Counted == 0 && bd.Atk >= 1 && bd.Counted >= 1 && bd.NecroAtk == 0 && necroHolder is not null;
            // 立った獣が弾けたら消えて数える（BD-BTa・獣に感電が残ったまま、味方の一撃 1 で弾けさせる）
            var fb = Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Hagi, back3: BtBd);
            var (cb, pb, _) = Board(fb, Wave3());
            var sb = pb.First(u => u.Def.Id == "som"); var hb = pb.First(u => u.Def.Id == "hagi"); var gb = pb.First(u => u.Def.Id == "golm");
            TurnStart(cb, sb);
            var beast = Stood(cb, sb).FirstOrDefault();
            bool popOk = false;
            if (beast is not null)
            {
                int ev0 = cb.Events.Count;
                cb.ApplyDamage(beast, 1, gb);
                var ev = After(cb, ev0);
                popOk = !beast.IsAlive && ev.Any(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Popped && x.TargetId == beast.InstanceId)
                    && !ev.Any(x => x.Kind == BattleEventKind.Death && x.TargetId == beast.InstanceId) && ev.Any(x => x.Kind == BattleEventKind.Attack && x.ActorId == hb.InstanceId)
                    && (T(cb, sb).BdKind?[2] ?? 0) == 1;
            }
            Expect("(g) BD: 獣の弾けでハギの追い打ちが出る（規定 E2 では出ない）・外した読み手（墓守 `Necro` の敵の死の攻撃力）は発火しない・立った獣は弾けたら消えて数える（`BeastBurst 弾けて消えた`・`Death` なし）",
                ok && popOk, $"E2 追い打ち {e2.Atk}・BD 追い打ち {bd.Atk}（数えた {bd.Counted}）・墓守 {(necroHolder is null ? "保持者なし" : Short(necroHolder))} の攻撃力 +{bd.NecroAtk}・立った獣 {popOk}");
        }
        // (h) 緊急の喚び出しはいつもその場で弾け、投げない
        {
            var f = Formation.Build(front1: UnitCatalog.Golm, back3: bta);
            var (ctx, p, _) = Board(f, Wave3());
            var som = p.First(u => u.Def.Id == "som"); var ally = p.First(u => u != som);
            int ev0 = ctx.Events.Count;
            ctx.EmergencyBurst(som, ally);
            var ev = After(ctx, ev0);
            Expect("(h) 緊急の喚び出しはいつもその場で弾け、投げない（BT-a）", Stood(ctx, som).Count == 0 && !ev.Any(x => x.Kind == BattleEventKind.BeastThrow) && ev.Count(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Emergency) == 1,
                $"立った {Stood(ctx, som).Count}・投げた {ev.Count(x => x.Kind == BattleEventKind.BeastThrow)}");
        }
        // (i) 計数器 `ThrowCensus` は盤面を変えない（SB-a で有無が同じ勝敗と決着T）
        {
            int nd = 0, n = 0; long census = 0;
            foreach (var (_, f0) in Boards().Take(4))
                foreach (var w in Waves.Take(3))
                    for (int sd = 0; sd < 3; sd++)
                    {
                        var f = Apply(f0, V("SB-a"));
                        BattleContext.ThrowCensus = false;
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        BattleContext.ThrowCensus = true;
                        var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        BattleContext.ThrowCensus = false;
                        n++;
                        if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                        census += r2.TallyByUnit.GetValueOrDefault("som")?.ThrowCensusN ?? 0;
                        if ((r1.TallyByUnit.GetValueOrDefault("som")?.ThrowCensusN ?? 0) > 0) nd++;
                    }
            Expect("(i) 計数器 `ThrowCensus` は盤面を変えない（有無で勝敗と決着T が同じ・無いときは数えない）", nd == 0 && census > 0, $"ずれ {nd} ／ {n} 件・数えた獣 {census}");
        }
        // (j) 決定的・verbose の有無で勝敗と決着T が同じ
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
            Expect($"(j) 決定的・verbose の有無で勝敗と決着T が変わらない（{Vers.Length - 1} 版 × 代表台 {Boards().Length} × {Waves.Length} 波 × seed 0..1）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (k) 版の定義・規定のソム ／ カタは動かない・経路の名前
        {
            bool defs = bta.Traits.SequenceEqual(UnitCatalog.SomH317.Traits.Concat(new[] { TraitId.BeastStand, TraitId.BeastBetray })) && UnitCatalog.SomBD.Traits.SequenceEqual(UnitCatalog.SomH317.Traits.Append(TraitId.BeastDeathCount))
                && UnitCatalog.KataLR.Traits.SequenceEqual(UnitCatalog.Kata.Traits.Append(TraitId.LightningRod)) && UnitCatalog.KataLRall.Traits.SequenceEqual(UnitCatalog.Kata.Traits.Append(TraitId.LightningRodAll))
                && new[] { bta, UnitCatalog.SomBD, UnitCatalog.KataLR, UnitCatalog.KataLRall, BtBd }.All(d => !UnitCatalog.Everyone.Contains(d))
                && bta.MinusText == UnitCatalog.SomBetrayMinusText && UnitCatalog.KataLR.PlusText.EndsWith("隣の仲間に落ちる雷は、受け止めて撃ち手に落とし直す");
            bool dflt = UnitCatalog.SomH317.Traits.SequenceEqual(UnitCatalog.SomSWKa.Traits) && !UnitCatalog.Kata.Traits.Contains(TraitId.LightningRod) && UnitCatalog.Fodder.MaxHp == 12 && UnitCatalog.Fodder.Attack == 0 && UnitCatalog.Fodder.Speed == 1;
            bool route = OutOfTurnRoutes.Names.Length == Enum.GetValues<OutOfTurnRoute>().Length && OutOfTurnRoutes.Names[(int)OutOfTurnRoute.LightningRod] == "避雷針" && OutOfTurnRoutes.Names[^1] == "その他";
            Expect("(k) 版の定義（BT-a ＝ 規定 ＋ 立つ ＋ 投げる・BD ＝ 規定 ＋ 獣の死・避雷針 ＝ 規定のカタ ＋ 札1枚・`Everyone` の外）・規定のソム ／ カタ ／ 獣は動かない・経路 `避雷針`",
                defs && dflt && route, $"版 {defs}・規定 {dflt}・経路 {route}（{OutOfTurnRoutes.Count}）");
        }
        // (l) 乱数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show c528874:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
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
                if (head is null) { clean = false; notes.Add($"{path}: 第316期の版を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(l) `PickOne(` ／ `Roll(` を新たに使っていない（第316期の後のコミット c528874 と比べる）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
