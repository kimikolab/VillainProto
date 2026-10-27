using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;

// =====================================================================================
// kari407 —— 第407期【軽い期・Phase 0 だけ】カリの転生（デバフ軸へ）の前に、呪いの鎖とカリの現状を数字で出す。
// 指示書は design/PHASE407_KARI_PHASE0_SPEC.md ／ 報告は design/PHASE407_KARI_PHASE0.md。**規則も版も足さない。数えるだけ。**
//
//     dotnet run --project BattleSim -c Release 0 kari407 all      # 確かめ ＋ Q1〜Q4（台は いまの「試遊・呪い」・4波 × seed 0..99）
//     dotnet run --project BattleSim -c Release 0 kari407 check    # 確かめだけ（第357期 §2 の勝率・倒しT と同じか）
//     dotnet run --project BattleSim -c Release 0 kari407 cum      # 第408期 段0: 敵ごとの弱体の累計（`Dull` が実際に下げた量の和）の推移と尻尾の1体の累計
//
// 1戦は `inv348` の `Raw`（verbose・計数の規則あり＝`db356 boards` ／ `s357` と同じ）。台本と帳簿を読むだけで盤面は動かさない。
// =====================================================================================
static class Kari407Diag
{
    const int Seeds = 100, TT = 10, CompareSeeds = 200;
    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);

    static Formation Board => Presets.Versions.First(r => r.Name == D.PlayRow).F;
    static readonly I.W[] Waves = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    /// <summary>第357期 §2 の数字（勝率・倒しT）。</summary>
    static readonly string[] Expect357 = { "100.0・9.0T", "100.0・5.0T", "100.0・4.7T", "79.0・10.1T" };

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode == "cum") { Cum408(); return; }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var res = Waves.Select(w => Measure(Board, w)).ToArray();
        bool ok = Check(res);
        if (mode == "check" || !ok) { if (!ok) Environment.ExitCode = 1; return; }
        Q1(res); Q2(res); Q3(); Q4(res);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class Fx
    {
        public long N, Wins, WinT, Fell, Foes, Turns;
        // 鎖（`BattleResult` の呪いの計数）
        public long ShareHits, ShareDry, Shares, ShareDmg, ShareBase, ShareBaseX, ShareTargets, HopBlocked, NonSingle, ReMark;
        public long Direct;   // 味方 → 敵の直の与ダメ（共有を除く・台本の Damage）
        public readonly Dictionary<string, long> ByN = new(), ByDmg = new(), RangBy = new();
        // ターンごと（そのターンまで続いた戦の和 ／ 数）: 頭の呪われた敵 ／ 頭の生きた敵 ／ 届いた本数 ／ 量 ／ 直の与ダメ ／ ウツの下げ幅（頭の写し）
        public readonly long[] TCur = new long[TT + 1], TLive = new long[TT + 1], TSh = new long[TT + 1], TShD = new long[TT + 1], TDir = new long[TT + 1], TUtsuD = new long[TT + 1], TCnt = new long[TT + 1];
        // ウツ
        public long USwings, URang, USplits, UDealt;
        // 死因（味方）: 「駒 ← 倒した駒・型」→ 回 ／ T の和
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        // 列ごとの敵（数 ／ 味方へ入れた量 ／ 味方から受けた量 ／ 初めて殴られた T の和・数）
        public readonly long[] RowN = new long[3], RowOut = new long[3], RowIn = new long[3], RowInShare = new long[3], RowFirstT = new long[3], RowFirstN = new long[3], RowDirT = new long[3], RowDirN = new long[3], RowDieT = new long[3], RowDieN = new long[3];
        public readonly Dictionary<string, long> LastFoe = new();   // 最後まで立っていた敵（倒した戦は最後に倒れた敵・負けた戦は生き残りの先頭）
        // ムド
        public long MudoN, Fires, ESwings, MDmg, MScar, CurseRage, Fuel, MudoDied, MudoDeathT, RageAtDeath, MudoHex, NelHex, NelOnMudo, MudoTaken;
        // 勝ち負けで分ける（強第4波の §Q2）: T1〜T3 の頭の呪われた敵の和 ／ 鎖の量 ／ ウツの下げ幅 T1〜T3 の和
        public long Cur13, Sh13, UD13;
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Foes += o.Foes; Turns += o.Turns;
            ShareHits += o.ShareHits; ShareDry += o.ShareDry; Shares += o.Shares; ShareDmg += o.ShareDmg; ShareBase += o.ShareBase; ShareBaseX += o.ShareBaseX;
            ShareTargets += o.ShareTargets; HopBlocked += o.HopBlocked; NonSingle += o.NonSingle; ReMark += o.ReMark; Direct += o.Direct;
            foreach (var (k, v) in o.ByN) ByN[k] = ByN.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.ByDmg) ByDmg[k] = ByDmg.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.RangBy) RangBy[k] = RangBy.GetValueOrDefault(k) + v;
            for (int t = 0; t <= TT; t++) { TCur[t] += o.TCur[t]; TLive[t] += o.TLive[t]; TSh[t] += o.TSh[t]; TShD[t] += o.TShD[t]; TDir[t] += o.TDir[t]; TUtsuD[t] += o.TUtsuD[t]; TCnt[t] += o.TCnt[t]; }
            USwings += o.USwings; URang += o.URang; USplits += o.USplits; UDealt += o.UDealt;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
            for (int i = 0; i < 3; i++) { RowN[i] += o.RowN[i]; RowOut[i] += o.RowOut[i]; RowIn[i] += o.RowIn[i]; RowInShare[i] += o.RowInShare[i]; RowFirstT[i] += o.RowFirstT[i]; RowFirstN[i] += o.RowFirstN[i]; RowDirT[i] += o.RowDirT[i]; RowDirN[i] += o.RowDirN[i]; RowDieT[i] += o.RowDieT[i]; RowDieN[i] += o.RowDieN[i]; }
            foreach (var (k, v) in o.LastFoe) LastFoe[k] = LastFoe.GetValueOrDefault(k) + v;
            MudoN += o.MudoN; Fires += o.Fires; ESwings += o.ESwings; MDmg += o.MDmg; MScar += o.MScar; CurseRage += o.CurseRage; Fuel += o.Fuel;
            MudoDied += o.MudoDied; MudoDeathT += o.MudoDeathT; RageAtDeath += o.RageAtDeath; MudoHex += o.MudoHex; NelHex += o.NelHex; NelOnMudo += o.NelOnMudo; MudoTaken += o.MudoTaken;
            Cur13 += o.Cur13; Sh13 += o.Sh13; UD13 += o.UD13;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
        public string WT => $"{F1(Win)}・{WinTs}T";
    }

    sealed record Pair(Fx All, Fx Won, Fx Lost);

    static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Fx { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events)
            if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid)
                (x.Team == BattleContext.PlayerTeam ? mine : foes).Add(sid);
        a.Foes = foes.Count;
        var units = p.Concat(e).ToDictionary(u => u.InstanceId);
        string Name(int? id) => id is int i ? (units.TryGetValue(i, out var u) ? Short(u.Def) : "召喚") : "（出どころなし）";
        var utsu = p.FirstOrDefault(u => u.Def.Id == "utsu"); var mudo = p.FirstOrDefault(u => u.Def.Id == "mudo"); var nel = p.FirstOrDefault(u => u.Def.Id == "nel");

        // 鎖（帳簿）
        a.ShareHits = r.HexShareHits; a.ShareDry = r.HexShareDry; a.Shares = r.HexShares; a.ShareDmg = r.HexShareDamage;
        a.ShareBase = r.HexShareBase; a.ShareBaseX = r.HexShareBaseTimesTargets; a.ShareTargets = r.HexShareTargets;
        a.HopBlocked = r.HexHopBlocked; a.NonSingle = r.HexNonSingleOnCursed; a.ReMark = r.HexReMarkBlocked;
        foreach (var (k, v) in r.HexShareBySource) a.ByN[k] = v;
        foreach (var (k, v) in r.HexShareDamageBySource) a.ByDmg[k] = v;
        foreach (var (id, t) in r.TallyByUnit) if (t.Db351HexSrcHits > 0) a.RangBy[id] = t.Db351HexSrcHits;
        if (utsu is not null && r.TallyByUnit.TryGetValue("utsu", out var tu)) { a.URang = tu.Db351HexSrcHits; a.USplits = tu.Db351HexSplits; a.UDealt = tu.Db351HexDealt; }

        // 台本を順に読む
        int last = r.Events.Where(x => x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int sn && mine.Contains(sn)).Select(x => x.Turn).DefaultIfEmpty(0).Max();
        for (int t = 1; t <= Math.Min(TT, last); t++) a.TCnt[t] = 1;
        var cursed = new Dictionary<int, int?>();   // 呪われた敵 → 最初の書き手
        var dead = new HashSet<int>();
        var rowOf = e.ToDictionary(u => u.InstanceId, u => (int)FormationRules.RowOf(u.Slot));
        foreach (var (id, row) in rowOf) a.RowN[row]++;
        var firstHit = new Dictionary<int, int>(); var firstDir = new Dictionary<int, int>();
        var lastAtk = new Dictionary<int, AttackPattern?>();
        int curT = 0; int? lastFoe = null;
        void Head(int t)
        {
            if (t < 1 || t > TT || a.TCnt[t] == 0) return;
            a.TCur[t] = cursed.Keys.Count(id => !dead.Contains(id));
            a.TLive[t] = foes.Count(id => !dead.Contains(id) && units.ContainsKey(id));
        }
        foreach (var x in r.Events)
        {
            while (curT < x.Turn) { curT++; Head(curT); }
            int t = x.Turn;
            switch (x.Kind)
            {
                case BattleEventKind.Attack when x.ActorId is int ai:
                    lastAtk[ai] = x.Pattern;
                    if (utsu is not null && ai == utsu.InstanceId) a.USwings++;
                    break;
                case BattleEventKind.StatusGain when x.Text == StatusKeys.Curse && x.TargetId is int ci && foes.Contains(ci):
                    if (!cursed.ContainsKey(ci)) cursed[ci] = x.ActorId;
                    if (mudo is not null && x.ActorId == mudo.InstanceId) a.MudoHex++;
                    if (nel is not null && x.ActorId == nel.InstanceId) a.NelHex++;
                    break;
                case BattleEventKind.Death when x.TargetId is int di:
                    dead.Add(di);
                    if (rowOf.TryGetValue(di, out int drw)) { a.RowDieT[drw] += t; a.RowDieN[drw]++; lastFoe = di; }
                    if (mine.Contains(di))
                    {
                        string pat = x.ActorId is int ki && lastAtk.TryGetValue(ki, out var pp) && pp is AttackPattern pv ? PatName(pv) : "—";
                        string k = $"{Name(di)} ← {Name(x.ActorId)}・{pat}";
                        a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + t;
                    }
                    break;
                case BattleEventKind.Revive when x.TargetId is int ri:
                    dead.Remove(ri);
                    break;
                case BattleEventKind.StatSnapshot when utsu is not null && x.TargetId == utsu.InstanceId && t >= 1 && t <= TT:
                    a.TUtsuD[t] += Math.Max(0, x.Amount - utsu.Def.Attack);
                    break;
                case BattleEventKind.Damage when x.TargetId is int ti && x.Amount > 0:
                    if (foes.Contains(ti) && x.ActorId is int src && mine.Contains(src))
                    {
                        bool share = x.ShareFromId is not null;
                        if (!share) a.Direct += x.Amount;
                        if (t >= 1 && t <= TT && a.TCnt[t] > 0)
                        {
                            if (share) { a.TSh[t]++; a.TShD[t] += x.Amount; } else a.TDir[t] += x.Amount;
                        }
                        if (share && t <= 3) a.Sh13 += x.Amount;
                        if (rowOf.TryGetValue(ti, out int rw))
                        {
                            a.RowIn[rw] += x.Amount;
                            if (share) a.RowInShare[rw] += x.Amount;
                            if (!firstHit.ContainsKey(ti)) { firstHit[ti] = t; a.RowFirstT[rw] += t; a.RowFirstN[rw]++; }
                            if (!share && !firstDir.ContainsKey(ti)) { firstDir[ti] = t; a.RowDirT[rw] += t; a.RowDirN[rw]++; }
                        }
                    }
                    if (mine.Contains(ti) && x.ActorId is int fa && rowOf.TryGetValue(fa, out int fr)) a.RowOut[fr] += x.Amount;
                    if (mudo is not null && ti == mudo.InstanceId && x.ActorId is int ma && foes.Contains(ma))
                    {
                        a.MudoTaken++;
                        if (nel is not null && cursed.TryGetValue(ma, out var w0) && w0 == nel.InstanceId) a.NelOnMudo++;
                    }
                    break;
            }
        }
        while (curT < TT) { curT++; Head(curT); }
        int? lf = r.PlayerWon ? lastFoe : e.Where(u => u.IsAlive).OrderBy(u => u.Slot).Select(u => (int?)u.InstanceId).FirstOrDefault();
        if (lf is int lfi) { string k = $"{Name(lfi)}（{(r.PlayerWon ? "勝ち" : "負け")}）"; a.LastFoe[k] = 1; }
        for (int t = 1; t <= 3; t++) { a.Cur13 += a.TCur[t]; a.UD13 += a.TUtsuD[t]; }

        if (mudo is not null && r.TallyByUnit.TryGetValue("mudo", out var tm))
        {
            a.MudoN = 1; a.Fires = tm.EruptFires; a.ESwings = tm.EruptSwings; a.MDmg = tm.DamageToEnemy; a.CurseRage = tm.Db354CurseRage; a.Fuel = tm.EruptFuel;
            a.MScar = r.Events.Where(x => x.Kind == BattleEventKind.Scar && x.ActorId == mudo.InstanceId).Sum(x => (long)x.Amount);
            if (mudo.LastDeathTurn > 0) { a.MudoDied = 1; a.MudoDeathT = mudo.LastDeathTurn; a.RageAtDeath = EruptTrait.RageOf(mudo); }
        }
        return a;
    }
    static string PatName(AttackPattern p) => p switch { AttackPattern.Single => "単体", AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き", _ => "全体" };

    static Pair Measure(Formation f, I.W w)
    {
        var parts = new Fx[Seeds];
        Parallel.For(0, Seeds, s => parts[s] = One(f, w, s));
        Fx all = new(), won = new(), lost = new();
        foreach (var x in parts) { all.Add(x); (x.Wins > 0 ? won : lost).Add(x); }
        return new Pair(all, won, lost);
    }

    // ---------------------------------------------------------------------------------
    // 確かめ（第357期 §2）
    // ---------------------------------------------------------------------------------
    static bool Check(Pair[] res)
    {
        Console.WriteLine($"# 第407期 kari407 —— 台 いまの「{D.PlayRow}」（{S307.Seats(Board)}）× {string.Join(" ／ ", Waves.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("## 0. 確かめ（第357期 §2 の勝率・倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 第357期 §2 | いま | 同じ |");
        Console.WriteLine("|---|---|---|---|");
        int same = 0;
        for (int i = 0; i < Waves.Length; i++)
        {
            bool s = res[i].All.WT == Expect357[i]; if (s) same++;
            Console.WriteLine($"| {Waves[i].Name} | {Expect357[i]} | {res[i].All.WT} | {(s ? "はい" : "**いいえ**")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"{Waves.Length} 波のうち同じ {same}。{(same == Waves.Length ? "" : "**違う——ここで止める。**")}");
        Console.WriteLine();
        return same == Waves.Length;
    }

    // ---------------------------------------------------------------------------------
    // Q1 鎖の実態
    // ---------------------------------------------------------------------------------
    static void Q1(Pair[] res)
    {
        Console.WriteLine("## Q1 鎖の実態（戦あたり）");
        Console.WriteLine();
        Console.WriteLine("反応 ＝ 単体の一撃が呪われた敵に入った回数（`HexShareHits`）・空振り ＝ うち他に呪われた者がいなかった（`HexShareDry`）・届いた ＝ 配った本数 ／ 量（`HexShares` ／ `HexShareDamage`）。"
            + "直 ＝ 味方 → 敵の与ダメのうち共有でないもの（台本の Damage・`ShareFromId` なし）。紙 100% ＝ `HexShareBaseTimesTargets`（共有が 100% なら届いた量・出力であって門ではない）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵の総数 | 決着T | 反応 | 空振り（割合） | 届いた 本 ／ 量 | 直の与ダメ | 鎖 ÷ 直 | 紙 100% の量（÷ 直） | 薙ぎ・全体が呪われた者に入った |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|--:|--:|---|--:|");
        for (int i = 0; i < Waves.Length; i++)
        {
            var a = res[i].All;
            Console.WriteLine($"| {Waves[i].Name} | {Per(a.Foes, a.N)} | {Per(a.Turns, a.N)} | {Per(a.ShareHits, a.N)} | {Per(a.ShareDry, a.N)}（{Pct(a.ShareDry, a.ShareHits)}%） | {Per(a.Shares, a.N)} ／ {Per(a.ShareDmg, a.N)} | {Per(a.Direct, a.N)} | {Pct(a.ShareDmg, a.Direct)}% | {Per(a.ShareBaseX, a.N)}（{Pct(a.ShareBaseX, a.Direct)}%） | {Per(a.NonSingle, a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 誰の一撃が鎖を鳴らしたか（戦あたり・反応 ／ 届いた本数 ／ 量）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 駒ごと（反応 ／ 届いた ／ 量） |");
        Console.WriteLine("|---|---|");
        for (int i = 0; i < Waves.Length; i++)
        {
            var a = res[i].All;
            var keys = a.RangBy.Keys.Select(id => UnitCatalog.Everyone.First(d => d.Id == id)).ToArray();
            string cell = string.Join("、", keys.OrderByDescending(d => a.RangBy[d.Id]).Select(d =>
                $"{Short(d)} {Per(a.RangBy[d.Id], a.N)} ／ {Per(a.ByN.GetValueOrDefault(d.Name), a.N)} ／ {Per(a.ByDmg.GetValueOrDefault(d.Name), a.N)}"));
            Console.WriteLine($"| {Waves[i].Name} | {cell} |");
        }
        Console.WriteLine();
        Console.WriteLine("### ウツの発のうち鎖に乗った数（戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 波 | ウツの発（Attack） | 呪われた敵に入った（反応） | 割合 | 分かれた先の延べ | 分けた量 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        for (int i = 0; i < Waves.Length; i++)
        {
            var a = res[i].All;
            Console.WriteLine($"| {Waves[i].Name} | {Per(a.USwings, a.N)} | {Per(a.URang, a.N)} | {Pct(a.URang, a.USwings)}% | {Per(a.USplits, a.N)} | {Per(a.UDealt, a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### ターンごと（そのターンまで続いた戦の平均・頭 ＝ そのターンの最初の出来事の手前）");
        Console.WriteLine();
        for (int i = 0; i < Waves.Length; i++)
        {
            var a = res[i].All;
            Console.WriteLine($"**{Waves[i].Name}**");
            Console.WriteLine();
            Console.WriteLine("| T | 続いた戦 | 頭の呪われた敵 ／ 生きた敵 | 鎖 本 ／ 量 | 直の与ダメ | 鎖 ÷ 直 | ウツの下げ幅（頭） |");
            Console.WriteLine("|--:|--:|---|---|--:|--:|--:|");
            for (int t = 1; t <= TT; t++)
            {
                long c = a.TCnt[t];
                if (c == 0) break;
                Console.WriteLine($"| {t} | {c} | {Per2(a.TCur[t], c)} ／ {Per2(a.TLive[t], c)} | {Per2(a.TSh[t], c)} ／ {Per(a.TShD[t], c)} | {Per(a.TDir[t], c)} | {Pct(a.TShD[t], a.TDir[t])}% | {Per(a.TUtsuD[t], c)} |");
            }
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    // Q2 強第4波の中身
    // ---------------------------------------------------------------------------------
    static void Q2(Pair[] res)
    {
        var w = Waves[3]; var x = res[3];
        Console.WriteLine($"## Q2 {w.Name} の {F1(x.All.Win)}% の中身");
        Console.WriteLine();
        Console.WriteLine("### 敵の構成（倍率 500 / 300・`EnemyCatalog.Stages[3]`）");
        Console.WriteLine();
        Console.WriteLine("| 席 | 列 | 駒 | HP | 攻 | 速 | 型 |");
        Console.WriteLine("|--:|---|---|--:|--:|--:|---|");
        foreach (var u in w.Make().OrderBy(u => u.Slot))
            Console.WriteLine($"| {u.Slot} | {FormationRules.RowOf(u.Slot)} | {u.Def.Name} | {u.MaxHp} | {u.CurrentAttack} | {u.Def.Speed} | {PatName(u.CurrentPattern)} |");
        Console.WriteLine();
        Console.WriteLine("### 勝ち ／ 負けで分けた（戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 組 | 戦 | 決着T | T1〜T3 の頭の呪われた敵（和） | T1〜T3 の鎖の量 | 鎖の量（全体） | 直の与ダメ | ウツの下げ幅 T1〜T3（和） | 倒れた味方 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (label, a) in new[] { ("勝ち", x.Won), ("負け", x.Lost), ("全体", x.All) })
            Console.WriteLine($"| {label} | {a.N} | {Per(a.Turns, a.N)} | {Per2(a.Cur13, a.N)} | {Per(a.Sh13, a.N)} | {Per(a.ShareDmg, a.N)} | {Per(a.Direct, a.N)} | {Per(a.UD13, a.N)} | {Per2(a.Fell, a.N)} |");
        Console.WriteLine();
        foreach (var (label, a) in new[] { ("負けた戦", x.Lost), ("勝った戦", x.Won) })
        {
            Console.WriteLine($"### 死因（{label}・延べ上位 10・倒した駒の型は直前の振り）");
            Console.WriteLine();
            Console.WriteLine("| 倒れた ← 倒した・型 | 回（／戦） | 平均T |");
            Console.WriteLine("|---|--:|--:|");
            foreach (var (k, v) in a.Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(10))
                Console.WriteLine($"| {k} | {v}（{Per2(v, a.N)}） | {Per(a.DeathT[k], v)} |");
            Console.WriteLine();
        }
        Console.WriteLine("### 列ごとの敵（全 100 戦・戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 列 | 体数 | 味方へ入れた量 | 味方から受けた量（うち鎖） | 初めて何かが入った T（割合） | 初めて直に殴られた T（割合） | 倒れた T（倒れた割合） |");
        Console.WriteLine("|---|--:|--:|---|---|---|---|");
        var a0 = x.All;
        for (int i = 0; i < 3; i++)
            Console.WriteLine($"| {(Row)i} | {Per(a0.RowN[i], a0.N)} | {Per(a0.RowOut[i], a0.N)} | {Per(a0.RowIn[i], a0.N)}（{Per(a0.RowInShare[i], a0.N)}） | {Per(a0.RowFirstT[i], a0.RowFirstN[i])}（{Pct(a0.RowFirstN[i], a0.RowN[i])}%） | {Per(a0.RowDirT[i], a0.RowDirN[i])}（{Pct(a0.RowDirN[i], a0.RowN[i])}%） | {Per(a0.RowDieT[i], a0.RowDieN[i])}（{Pct(a0.RowDieN[i], a0.RowN[i])}%） |");
        Console.WriteLine();
        Console.WriteLine("最後まで立っていた敵（勝った戦 ＝ 最後に倒れた敵 ／ 負けた戦 ＝ 生き残りのうち席が最も前）: "
            + string.Join("、", a0.LastFoe.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} {kv.Value}")) + "。");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Q3 カリの現状（compare ／ elite の行・素体に替えた差）
    // ---------------------------------------------------------------------------------
    static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + Short(d), MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Advances = d.Advances, Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    static Formation Swap(Formation f, string id, UnitDef to)
    {
        var g = new Formation();
        foreach (var o in f.Occupied()) g[o.Slot] = o.Def.Id == id ? to : o.Def;
        return g;
    }

    sealed class KC { public long N, Wins, Dmg, Fires, Idle, Given, Switches, Deaths, DeathT; }
    static KC KariCell(Formation f, Func<int, BattleResult> run, string id)
    {
        var parts = new KC[CompareSeeds];
        Parallel.For(0, CompareSeeds, s =>
        {
            var r = run(s);
            var c = new KC { N = 1, Wins = r.PlayerWon ? 1 : 0, Fires = r.GoadFires, Idle = r.GoadIdle, Given = r.GoadGiven, Switches = r.GoadSwitches };
            if (r.TallyByUnit.TryGetValue(id, out var t)) { c.Dmg = t.DamageToEnemy; c.Deaths = t.Deaths; c.DeathT = t.Deaths > 0 ? t.LastActiveTurn : 0; }
            parts[s] = c;
        });
        var a = new KC();
        foreach (var c in parts) { a.N += c.N; a.Wins += c.Wins; a.Dmg += c.Dmg; a.Fires += c.Fires; a.Idle += c.Idle; a.Given += c.Given; a.Switches += c.Switches; a.Deaths += c.Deaths; a.DeathT += c.DeathT; }
        return a;
    }

    static void Q3()
    {
        Console.WriteLine("## Q3 カリの現状");
        Console.WriteLine();
        var rows = CompareBuildsH410().Where(b => b.F.Occupied().Any(o => o.Def.Id == "kari")).ToArray();   // 第411期: 旧の行名 ＋ 旧のカリ（駆り立て）
        var plain = Plain(UnitCatalog.KariH406);   // 第411期: 旧のカリの素体
        var waves = EnemyCatalog.Stages.Select((st, i) => ($"第{i + 1}波", (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(f, st.Enemy, s, verbose: false))))
            .Concat(new[] { ("近衛", EnemyCatalog.EliteGuardWave), ("大隊", EnemyCatalog.EliteBattalionWave) }
                .Select(x => (x.Item1, (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(x.Item2, EnemyCatalog.EliteScale), s, verbose: false)))))
            .ToArray();
        Console.WriteLine($"`compare` の行のうちカリを含む {rows.Length} 行 × 本編 5 波（`compare` と同じ `Run(f, 波, seed)`）＋ 精鋭 2 波（`elite` と同じ）・seed 0..{CompareSeeds - 1}。"
            + "カリの欄は戦あたり: 与ダメ ／ 押し出した回数（`GoadFires`・空振り）／ 渡した量（`GoadGiven`）／ 付け替え ／ 倒れた割合・倒れた戦の最後に動いた T。素体 ＝ 同じ HP ／ 攻 ／ 速 ／ 型・札なし。");
        Console.WriteLine();
        foreach (var (name, f) in rows)
        {
            Console.WriteLine($"**{name}**（{S307.Seats(f)}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 素体の勝率 | 差 | カリの与ダメ | 押し出し（空振り） | 渡した量 | 付け替え | 倒れた・T |");
            Console.WriteLine("|---|--:|--:|--:|--:|---|--:|--:|---|");
            var fp = Swap(f, "kari", plain);
            foreach (var (wn, run) in waves)
            {
                var k = KariCell(f, s => run(f, s), "kari");
                var z = KariCell(fp, s => run(fp, s), plain.Id);
                double d = 100.0 * (k.Wins - z.Wins) / k.N;
                Console.WriteLine($"| {wn} | {Pct(k.Wins, k.N)} | {Pct(z.Wins, z.N)} | {d:+0.0;-0.0;0.0} | {Per(k.Dmg, k.N)} | {Per(k.Fires, k.N)}（{Per(k.Idle, k.N)}） | {Per(k.Given, k.N)} | {Per(k.Switches, k.N)} | {Pct(k.Deaths, k.N)}%・{Per(k.DeathT, k.Deaths)} |");
            }
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    // Q4 ムドの数字（第408期の下見）
    // ---------------------------------------------------------------------------------
    static void Q4(Pair[] res)
    {
        Console.WriteLine("## Q4 ムドの数字（戦あたり）");
        Console.WriteLine();
        Console.WriteLine("暴発 ＝ `EruptFires` ／ 発 ＝ `EruptSwings`・与ダメ ＝ `DamageToEnemy`・削った最大HP ＝ 台本の Scar（書き手がムド）・怒り: 呪いから ＝ `Db354CurseRage` ／ 殴られて ＝ `EruptFuel`。"
            + "倒れたときの不発の怒り ＝ 倒れた戦の終わりの怒りの残り。呪った ＝ 台本の呪いの StatusGain（敵へ・書き手ごと）。重なり ＝ ネルが先に呪っている敵がムドを殴った回数（祟りが「もう呪われている」で止まる側）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 暴発 回 ／ 発 | 与ダメ | 削った最大HP | 怒り 呪いから ／ 殴られて | 倒れた・T | 倒れたときの不発の怒り | ムドが呪った ／ ネルが呪った | ムドを殴った敵の回 ／ うちネルが先に呪った敵 | 祟りの重ね止め（全体） |");
        Console.WriteLine("|---|---|--:|--:|---|---|--:|---|---|--:|");
        for (int i = 0; i < Waves.Length; i++)
        {
            var a = res[i].All;
            Console.WriteLine($"| {Waves[i].Name} | {Per(a.Fires, a.N)} ／ {Per(a.ESwings, a.N)} | {Per(a.MDmg, a.N)} | {Per(a.MScar, a.N)} | {Per(a.CurseRage, a.N)} ／ {Per(a.Fuel, a.N)} | {Pct(a.MudoDied, a.N)}%・{Per(a.MudoDeathT, a.MudoDied)} | {Per2(a.RageAtDeath, a.MudoDied)} | {Per(a.MudoHex, a.N)} ／ {Per(a.NelHex, a.N)} | {Per(a.MudoTaken, a.N)} ／ {Per(a.NelOnMudo, a.N)} | {Per(a.ReMark, a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### ムドを素体に替えた差（同じ seed）");
        Console.WriteLine();
        var m = UnitCatalog.Mudo;
        var vs = new List<(string Label, UnitDef D)> { ("素体（札なし）", Plain(m)) };
        foreach (var t in m.Traits)
            vs.Add(($"{t} を抜く", new UnitDef { Id = m.Id + "_no" + t, Name = m.Name, MaxHp = m.MaxHp, Attack = m.Attack, Speed = m.Speed, Advances = m.Advances, Pattern = m.Pattern, Traits = m.Traits.Where(x => x != t).ToArray() }));
        Console.WriteLine($"いまのムドの札: {string.Join(" ／ ", m.Traits)}。札を1枚ずつ抜いた版も同じ seed で並べる（ワタの配りはムドを暴発の札 `Erupt` で引くので、`Erupt` を抜いた版 ／ 素体ではワタからムドへは回らない）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        Console.WriteLine("| いま | " + string.Join(" | ", res.Select(r => r.All.WT)) + " |");
        foreach (var (label, d) in vs)
        {
            var fp = Swap(Board, "mudo", d);
            Console.WriteLine($"| {label} | " + string.Join(" | ", Waves.Select((w, i) => { var z = Measure(fp, w).All; return $"{z.WT}（{z.Win - res[i].All.Win:+0.0;-0.0;0.0}）"; })) + " |");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // 第408期 段0: 敵ごとの弱体の累計（台本の弱体の StatusGain ＝ `Dull` が実際に攻撃力を下げた量・ボスの伸びは入らない）
    // ---------------------------------------------------------------------------------
    sealed class Cx
    {
        public long N;
        // ターンの頭: 生きた敵の累計の最大（呪われた敵の中）／ 生きた敵の累計の平均の和 ／ 数
        public readonly long[] TMaxCur = new long[TT + 1], TMeanSum100 = new long[TT + 1], TCnt = new long[TT + 1];
        public long TailN, TailStartSum, TailCumAtStart, TailCumEnd, TailLen;
        public readonly Dictionary<string, long> TailWho = new();
        public void Add(Cx o)
        {
            N += o.N; for (int t = 0; t <= TT; t++) { TMaxCur[t] += o.TMaxCur[t]; TMeanSum100[t] += o.TMeanSum100[t]; TCnt[t] += o.TCnt[t]; }
            TailN += o.TailN; TailStartSum += o.TailStartSum; TailCumAtStart += o.TailCumAtStart; TailCumEnd += o.TailCumEnd; TailLen += o.TailLen;
            foreach (var (k, v) in o.TailWho) TailWho[k] = TailWho.GetValueOrDefault(k) + v;
        }
    }
    static Cx CumOne(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Cx { N = 1 };
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid) (x.Team == BattleContext.PlayerTeam ? mine : foes).Add(sid);
        var names = e.ToDictionary(u => u.InstanceId, u => u.Def.Name);
        int last = r.Events.Where(x => x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int sn && mine.Contains(sn)).Select(x => x.Turn).DefaultIfEmpty(0).Max();
        var cum = foes.ToDictionary(id => id, _ => 0L);
        var cursed = new HashSet<int>(); var dead = new HashSet<int>();
        int curT = 0; int tailStart = 0; int tailId = -1;
        void Head(int t)
        {
            if (t < 1 || t > TT || t > last) return;
            a.TCnt[t] = 1;
            var live = foes.Where(id => !dead.Contains(id)).ToArray();
            var lc = live.Where(cursed.Contains).ToArray();
            a.TMaxCur[t] = lc.Length == 0 ? 0 : lc.Max(id => cum[id]);
            a.TMeanSum100[t] = live.Length == 0 ? 0 : (long)(100.0 * live.Average(id => cum[id]));
        }
        foreach (var x in r.Events)
        {
            while (curT < x.Turn) { curT++; Head(curT); }
            if (x.Kind == BattleEventKind.StatusGain && x.TargetId is int ti && foes.Contains(ti))
            {
                if (x.Text == BattleContext.DullKey) cum[ti] = cum.GetValueOrDefault(ti) + x.Amount;
                if (x.Text == StatusKeys.Curse) cursed.Add(ti);
            }
            if (x.Kind == BattleEventKind.Death && x.TargetId is int di && foes.Contains(di))
            {
                dead.Add(di);
                var live = foes.Where(id => !dead.Contains(id)).ToArray();
                if (live.Length == 1 && tailId < 0) { tailId = live[0]; tailStart = x.Turn; a.TailCumAtStart = cum[tailId]; }
            }
            if (x.Kind == BattleEventKind.Revive && x.TargetId is int ri) dead.Remove(ri);
        }
        while (curT < TT) { curT++; Head(curT); }
        if (tailId >= 0)
        {
            a.TailN = 1; a.TailStartSum = tailStart; a.TailCumEnd = cum[tailId]; a.TailLen = Math.Max(0, r.Turns - tailStart);
            string k = names.GetValueOrDefault(tailId, "召喚"); a.TailWho[k] = 1;
        }
        else if (foes.Count == 1)
        {
            int only = foes.First();
            a.TailN = 1; a.TailStartSum = 0; a.TailCumAtStart = 0; a.TailCumEnd = cum[only]; a.TailLen = r.Turns; a.TailWho[names.GetValueOrDefault(only, "?")] = 1;
        }
        return a;
    }
    static void Cum408()
    {
        Console.WriteLine($"# 第408期 段0 —— 敵ごとの弱体の累計（いまの「{D.PlayRow}」・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("累計 ＝ その戦でその敵の攻撃力を弱体の窓口 `Dull` が実際に下げた量の和（台本の弱体の StatusGain・書き手を問わない・ボスの伸びは入らない・第353期 UK-c と同じ定義）。"
            + "頭の最大 ＝ ターンの頭に生きていて呪われた敵のうち累計が最も大きい1体の累計（K-k が読む量）。尻尾 ＝ 生きた敵が1体になった後（ボスは開戦から）。");
        Console.WriteLine();
        foreach (var w in Waves)
        {
            var parts = new Cx[Seeds];
            Parallel.For(0, Seeds, s => parts[s] = CumOne(Board, w, s));
            var a = new Cx(); foreach (var x in parts) a.Add(x);
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| T | 続いた戦 | 頭の最大（呪われた敵） | 生きた敵の累計の平均 |");
            Console.WriteLine("|--:|--:|--:|--:|");
            for (int t = 1; t <= TT; t++)
            {
                if (a.TCnt[t] == 0) break;
                Console.WriteLine($"| {t} | {a.TCnt[t]} | {Per(a.TMaxCur[t], a.TCnt[t])} | {F1(a.TMeanSum100[t] / 100.0 / a.TCnt[t])} |");
            }
            Console.WriteLine();
            Console.WriteLine($"尻尾: {a.TailN} 戦・始まり T{Per(a.TailStartSum, a.TailN)}・長さ {Per(a.TailLen, a.TailN)}T・その1体の累計 始まり {Per(a.TailCumAtStart, a.TailN)} → 終わり {Per(a.TailCumEnd, a.TailN)}・その1体: "
                + string.Join("、", a.TailWho.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")) + "。");
            Console.WriteLine();
        }
    }
}
