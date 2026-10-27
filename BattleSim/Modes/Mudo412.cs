using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// =====================================================================================
// mudo412 —— 第412期【軽い期・版だけ】ムドの見直し①: 呪いの一撃で怒る（M-u）／ 殴り返しが敵を弱らせる（M-d）／ 両方（M-ud）。
// 指示書は design/PHASE412_MUDO_LOOP_SPEC.md ／ 報告は design/PHASE412_MUDO_LOOP.md。**規定は動かさない**（版はムドの写し・`All` の外）。
//
//     dotnet run --project BattleSim -c Release 0 mudo412 phase0   # §2-1 (b): 規定の2台 × 4 波で、呪われた敵に入った味方の一撃（ムド除く）／ターン（M-u の燃料の見積もり）
//     dotnet run --project BattleSim -c Release 0 mudo412 boards   # §2-3: 2台 × {規定, M-u, M-d, M-ud} × ボス ／ 近衛 ／ 大隊 ／ 強 第4波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 mudo412 mixed    # §2-4: compare のムドを含む行のムドを M-ud に替えた写し × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 mudo412 log <版 0|u|d|ud> <台 play|karz> <波 boss|guard|bat|s4> [seed]
//     dotnet run --project BattleSim -c Release 0 mudo412 wata     # §6-5 第413期の下見: 「試遊・呪い」× 4 波 × {規定, M-ud} で、ワタが拾った重荷をターンごとに誰へ回したか（ウツ ／ ムド ／ 敵）
//     dotnet run --project BattleSim -c Release 0 mudo412 check    # 自己検査
//
// **予測のファイルが無ければ boards ／ mixed を走らせない**（R397・`.tmp/p412/predict.md`）。1戦は `inv348` の `Raw`（verbose）。台本と帳簿を読むだけで盤面は動かさない。
// =====================================================================================
static class Mudo412Diag
{
    const string PredictFile = ".tmp/p412/predict.md";
    const int Seeds = 100, TT = 10, CumT = 6, CompareSeeds = 200;
    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);

    internal static readonly I.W[] Waves = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Mudo), ("u", "M-u", UnitCatalog.MudoMu), ("d", "M-d", UnitCatalog.MudoMd), ("ud", "M-ud", UnitCatalog.MudoMud),
    };
    static readonly (string Key, string Name)[] BoardRows = { ("play", "試遊・呪い"), ("karz", "試遊・カーズ 呪い") };
    internal static Formation BoardOf(string name) => Presets.Versions.First(r => r.Name == name).F;
    static Formation Swap(Formation f, UnitDef mudo)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "mudo") g[o.Slot] = mudo;
        return g;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"mudo412: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "phase0": Phase0(); break;
            case "boards": Boards(); break;
            case "mixed": Mixed(); break;
            case "log": Log(A(3, "ud"), A(4, "karz"), A(5, "s4"), int.Parse(A(6, "0"))); return;
            case "wata": Wata(); break;
            case "check": Check(); break;
            default: Console.WriteLine("mudo412: モードは phase0 / boards / mixed / wata / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static I.W WaveOf(string k) => k switch { "boss" => I.Boss, "guard" => I.Guard, "bat" => I.Bat, _ => I.StrongWaves[2] };
    static void Log(string ver, string board, string wave, int seed)
    {
        var v = Vers.First(x => x.Key == ver);
        var f = Swap(BoardOf(BoardRows.First(b => b.Key == board).Name), v.D); var w = WaveOf(wave);
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {S307.Seats(f)}（ムド {v.Label}）× {w.Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Fx
    {
        public long N, Wins, WinT, Fell, Turns;
        public long TailN, TailTurns, TailDirect;          // 尻尾（生きた敵が1体になった次のターンから・ボスは開戦から）
        public long CurHits, CurHitsTurns;                 // 呪われた敵に入った味方の一撃（ムド除く・`Db351HexSrcHits`）／ 戦のターン数
        public readonly Dictionary<string, long> CurBy = new();
        // ムド
        public long MudoN, Fires, ESwings, MDmg, Fuel, CurseRage, StrikeRage, MudoDied, MudoDeathT, RageAtDeath, FiredBeforeDeath, FiredAny, FirstFireT, FirstFireN;
        // M-d
        public long MdSwings, MdDull, MdOnMarked;
        // ウツ
        public long USwings, UDrop13, UDrop13N;
        // 敵の弱体の累計（頭・生きた敵の最大）
        public readonly long[] TCumMax = new long[CumT + 1], TCnt = new long[CumT + 1];
        // 倒れた味方（駒 → 回 ／ T の和）
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Turns += o.Turns;
            TailN += o.TailN; TailTurns += o.TailTurns; TailDirect += o.TailDirect; CurHits += o.CurHits; CurHitsTurns += o.CurHitsTurns;
            foreach (var (k, v) in o.CurBy) CurBy[k] = CurBy.GetValueOrDefault(k) + v;
            MudoN += o.MudoN; Fires += o.Fires; ESwings += o.ESwings; MDmg += o.MDmg; Fuel += o.Fuel; CurseRage += o.CurseRage; StrikeRage += o.StrikeRage;
            MudoDied += o.MudoDied; MudoDeathT += o.MudoDeathT; RageAtDeath += o.RageAtDeath; FiredBeforeDeath += o.FiredBeforeDeath; FiredAny += o.FiredAny; FirstFireT += o.FirstFireT; FirstFireN += o.FirstFireN;
            MdSwings += o.MdSwings; MdDull += o.MdDull; MdOnMarked += o.MdOnMarked;
            USwings += o.USwings; UDrop13 += o.UDrop13; UDrop13N += o.UDrop13N;
            for (int t = 0; t <= CumT; t++) { TCumMax[t] += o.TCumMax[t]; TCnt[t] += o.TCnt[t]; }
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
        public string WT => $"{F1(Win)}・{WinTs}T";
        public string Tail => TailTurns == 0 ? "—" : $"{Per(TailDirect, TailTurns)} ／ {Per(TailTurns, TailN)}T";
        public string Fall => string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pct(kv.Value, N)}%・T{Per(DeathT[kv.Key], kv.Value)}"));
    }

    internal static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Fx { N = 1, Turns = r.Turns, CurHitsTurns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { string k = Short(u.Def); a.Death[k] = 1; a.DeathT[k] = u.LastDeathTurn; }
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid) (x.Team == BattleContext.PlayerTeam ? mine : foes).Add(sid);
        var mudo = p.FirstOrDefault(u => u.Def.Id == "mudo"); var utsu = p.FirstOrDefault(u => u.Def.Id == "utsu");

        // 呪われた敵に入った味方の一撃（ムド除く）
        foreach (string id in p.Select(u => u.Def.Id).Distinct())
            if (id != "mudo" && r.TallyByUnit.TryGetValue(id, out var t) && t.Db351HexSrcHits > 0) { a.CurHits += t.Db351HexSrcHits; a.CurBy[id] = t.Db351HexSrcHits; }

        var dead = new HashSet<int>();
        var cum = foes.ToDictionary(id => id, _ => 0L);
        int tailStart = foes.Count == 1 ? 0 : -1;
        int curT = 0; int firstFire = 0;
        int last = r.Events.Where(x => x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int sn && mine.Contains(sn)).Select(x => x.Turn).DefaultIfEmpty(0).Max();
        void Head(int t)
        {
            if (t < 1 || t > CumT || t > last) return;
            a.TCnt[t] = 1;
            var live = foes.Where(id => !dead.Contains(id)).ToArray();
            a.TCumMax[t] = live.Length == 0 ? 0 : live.Max(id => cum.GetValueOrDefault(id));
        }
        foreach (var x in r.Events)
        {
            while (curT < x.Turn) { curT++; Head(curT); }
            int t = x.Turn;
            switch (x.Kind)
            {
                case BattleEventKind.Death when x.TargetId is int di && foes.Contains(di):
                    dead.Add(di);
                    if (tailStart < 0 && foes.Count(id => !dead.Contains(id)) == 1) tailStart = t;
                    break;
                case BattleEventKind.Revive when x.TargetId is int ri: dead.Remove(ri); break;
                case BattleEventKind.Damage when x.Amount > 0 && x.TargetId is int ti && foes.Contains(ti) && x.ActorId is int ai && mine.Contains(ai) && x.ShareFromId is null:
                    if (tailStart >= 0 && t > tailStart) a.TailDirect += x.Amount;
                    break;
                case BattleEventKind.StatusGain when x.Text == BattleContext.DullKey && x.TargetId is int ci && foes.Contains(ci):
                    cum[ci] = cum.GetValueOrDefault(ci) + x.Amount;
                    break;
                case BattleEventKind.Attack when utsu is not null && x.ActorId == utsu.InstanceId:
                    a.USwings++;
                    break;
                case BattleEventKind.StatSnapshot when utsu is not null && x.TargetId == utsu.InstanceId && t is >= 1 and <= 3:
                    a.UDrop13 += Math.Max(0, x.Amount - utsu.Def.Attack); a.UDrop13N++;
                    break;
                case BattleEventKind.Highlight when mudo is not null && x.ActorId == mudo.InstanceId && firstFire == 0:
                    firstFire = t;
                    break;
            }
        }
        while (curT < CumT) { curT++; Head(curT); }
        if (tailStart >= 0 && r.Turns > tailStart) { a.TailN = 1; a.TailTurns = r.Turns - tailStart; } else a.TailDirect = 0;

        if (mudo is not null && r.TallyByUnit.TryGetValue("mudo", out var tm))
        {
            a.MudoN = 1; a.Fires = tm.EruptFires; a.ESwings = tm.EruptSwings; a.MDmg = tm.DamageToEnemy;
            a.Fuel = tm.EruptFuel; a.CurseRage = tm.Db354CurseRage; a.StrikeRage = tm.Mu412Rage;
            a.MdSwings = tm.Md412Swings; a.MdDull = tm.Md412Dull; a.MdOnMarked = tm.Md412OnMarked;
            if (tm.EruptFires > 0) a.FiredAny = 1;
            if (firstFire > 0) { a.FirstFireT = firstFire; a.FirstFireN = 1; }
            if (mudo.LastDeathTurn > 0)
            {
                a.MudoDied = 1; a.MudoDeathT = mudo.LastDeathTurn; a.RageAtDeath = EruptTrait.RageOf(mudo);
                if (tm.EruptFires > 0) a.FiredBeforeDeath = 1;
            }
        }
        return a;
    }

    internal static Fx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // §2-1 (b) 燃料の見積もり（規定）
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        Console.WriteLine($"# 第412期 Phase 0 (b) —— 規定のムドの2台 × {string.Join(" ／ ", Waves.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("燃料 ＝ 単体の一撃が呪われた敵に入った回数のうち、ムド以外の味方が打ったもの（駒ごとの `Db351HexSrcHits`・鎖で届いた分は入らない）。／T ＝ 戦あたり ÷ 決着T。M-u ならこれがそのままムドの怒り。");
        Console.WriteLine();
        foreach (var (_, name) in BoardRows)
        {
            var f = BoardOf(name);
            Console.WriteLine($"## {name}（{S307.Seats(f)}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率・倒しT | 決着T | 燃料 ／戦 | 燃料 ／T | 駒ごと（／戦） | ムドの怒り 殴られて ／ 呪いから（／戦） | 暴発 ／戦 | ムドが倒れた・T | 倒れたときの不発の怒り |");
            Console.WriteLine("|---|---|--:|--:|--:|---|---|--:|---|--:|");
            foreach (var w in Waves)
            {
                var a = Measure(f, w);
                string by = string.Join("、", a.CurBy.OrderByDescending(kv => kv.Value).Select(kv => $"{Short(UnitCatalog.ById(kv.Key))} {Per(kv.Value, a.N)}"));
                Console.WriteLine($"| {w.Name} | {a.WT} | {Per(a.Turns, a.N)} | {Per(a.CurHits, a.N)} | {Per(a.CurHits, a.CurHitsTurns)} | {by} | {Per(a.Fuel, a.N)} ／ {Per(a.CurseRage, a.N)} | {Per(a.Fires, a.N)} | {Pct(a.MudoDied, a.N)}%・T{Per(a.MudoDeathT, a.MudoDied)} | {Per2(a.RageAtDeath, a.MudoDied)} |");
            }
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    // §2-3 台 2 つ × 版 × 4 波
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        Console.WriteLine($"# 第412期 §2-3 —— 2台 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", Waves.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("尻尾 ＝ 生きた敵が1体になった次のターンから決着まで（ボスは開戦から）。尻尾の直 ／T ＝ 味方 → 敵の直の与ダメ（共有を除く）÷ 尻尾のターン数（第408期と同じ）。"
            + "怒りの内訳: 殴られて ＝ `EruptFuel` ／ 呪いから ＝ `Db354CurseRage`（M1）／ 一撃から ＝ `Mu412Rage`（M-u）。倒れる前に暴発 ＝ ムドが倒れた戦のうち、1回以上暴発していた割合。"
            + "敵の累計 ＝ その敵の攻撃力を `Dull` が実際に下げた量の和（台本の弱体・書き手を問わない）・頭に生きている敵の最大。");
        Console.WriteLine();
        foreach (var (_, name) in BoardRows)
        {
            var b = BoardOf(name);
            Console.WriteLine($"## {name}（{S307.Seats(b)}）");
            Console.WriteLine();
            var res = Vers.Select(v => Waves.Select(w => Measure(Swap(b, v.D), w)).ToArray()).ToArray();

            Console.WriteLine("### 1. 勝率・倒しT ／ 倒れた味方（／戦）／ 尻尾の直 ／T・尻尾の長さ（括弧は規定との差）");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
            for (int v = 0; v < Vers.Length; v++)
                Console.WriteLine($"| {Vers[v].Label} | " + string.Join(" | ", Waves.Select((w, i) =>
                {
                    var a = res[v][i]; var z = res[0][i];
                    string d = v == 0 ? "" : $"（{a.Win - z.Win:+0.0;-0.0;0.0}）";
                    return $"{a.WT}{d} ／ {Per2(a.Fell, a.N)} ／ {a.Tail}";
                })) + " |");
            Console.WriteLine();

            Console.WriteLine("### 2. 倒れた味方（駒・倒れた割合・平均T）");
            Console.WriteLine();
            Console.WriteLine("| 版 | 波 | 倒れた |");
            Console.WriteLine("|---|---|---|");
            for (int v = 0; v < Vers.Length; v++)
                for (int i = 0; i < Waves.Length; i++)
                    Console.WriteLine($"| {Vers[v].Label} | {Waves[i].Name} | {(res[v][i].Death.Count == 0 ? "—" : res[v][i].Fall)} |");
            Console.WriteLine();

            Console.WriteLine("### 3. ムド（戦あたり）");
            Console.WriteLine();
            Console.WriteLine("| 版 | 波 | 暴発 回 ／ 発 | 初の暴発 T（暴発した戦の割合） | 与ダメ | 怒り 殴られて ／ 呪いから ／ 一撃から | 倒れた・T | 倒れたときの不発の怒り | 倒れる前に暴発 |");
            Console.WriteLine("|---|---|---|---|--:|---|---|--:|--:|");
            for (int v = 0; v < Vers.Length; v++)
                for (int i = 0; i < Waves.Length; i++)
                {
                    var a = res[v][i];
                    Console.WriteLine($"| {Vers[v].Label} | {Waves[i].Name} | {Per(a.Fires, a.N)} ／ {Per(a.ESwings, a.N)} | T{Per(a.FirstFireT, a.FirstFireN)}（{Pct(a.FiredAny, a.N)}%） | {Per(a.MDmg, a.N)} | {Per(a.Fuel, a.N)} ／ {Per(a.CurseRage, a.N)} ／ {Per(a.StrikeRage, a.N)} | {Pct(a.MudoDied, a.N)}%・T{Per(a.MudoDeathT, a.MudoDied)} | {Per2(a.RageAtDeath, a.MudoDied)} | {Pct(a.FiredBeforeDeath, a.MudoDied)}% |");
                }
            Console.WriteLine();

            Console.WriteLine("### 4. M-d と敵の累計（戦あたり・累計は頭に生きている敵の最大・そのターンまで続いた戦の平均）");
            Console.WriteLine();
            Console.WriteLine("| 版 | 波 | 下げた発 ／ 下げた量 | うちカーズの印の敵（割合） | " + string.Join(" | ", Enumerable.Range(1, CumT).Select(t => $"累計 T{t}")) + " |");
            Console.WriteLine("|---|---|---|---|" + string.Concat(Enumerable.Range(1, CumT).Select(_ => "--:|")));
            for (int v = 0; v < Vers.Length; v++)
                for (int i = 0; i < Waves.Length; i++)
                {
                    var a = res[v][i];
                    Console.WriteLine($"| {Vers[v].Label} | {Waves[i].Name} | {Per(a.MdSwings, a.N)} ／ {Per(a.MdDull, a.N)} | {Per(a.MdOnMarked, a.N)}（{Pct(a.MdOnMarked, a.MdSwings)}%） | "
                        + string.Join(" | ", Enumerable.Range(1, CumT).Select(t => a.TCnt[t] == 0 ? "—" : Per(a.TCumMax[t], a.TCnt[t]))) + " |");
                }
            Console.WriteLine();

            Console.WriteLine("### 5. ウツ（戦あたり）と燃料");
            Console.WriteLine();
            Console.WriteLine("| 版 | 波 | ウツの発 | ウツの下げ幅（T1〜T3 の頭の平均） | 燃料（呪われた敵に入った味方の一撃・ムド除く）／戦 ／ ／T |");
            Console.WriteLine("|---|---|--:|--:|---|");
            for (int v = 0; v < Vers.Length; v++)
                for (int i = 0; i < Waves.Length; i++)
                {
                    var a = res[v][i];
                    Console.WriteLine($"| {Vers[v].Label} | {Waves[i].Name} | {Per(a.USwings, a.N)} | {Per(a.UDrop13, a.UDrop13N)} | {Per(a.CurHits, a.N)} ／ {Per(a.CurHits, a.CurHitsTurns)} |");
                }
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    // §2-4 寄せ集め（compare のムドを含む行 × M-ud）
    // ---------------------------------------------------------------------------------
    static void Mixed()
    {
        var rows = CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "mudo")).ToArray();
        var waves = EnemyCatalog.Stages.Select((st, i) => ($"第{i + 1}波", (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(f, st.Enemy, s, verbose: false))))
            .Concat(new[] { ("近衛", EnemyCatalog.EliteGuardWave), ("大隊", EnemyCatalog.EliteBattalionWave) }
                .Select(x => (x.Item1, (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(x.Item2, EnemyCatalog.EliteScale), s, verbose: false)))))
            .ToArray();
        Console.WriteLine($"# 第412期 §2-4 —— `compare` のムドを含む {rows.Length} 行のムドを版に替えた写し × 本編 5 波（`compare` と同じ）＋ 精鋭 2 波（`elite` と同じ）・seed 0..{CompareSeeds - 1}");
        Console.WriteLine();
        Console.WriteLine("各セル: 勝率（規定との差）。ムドの欄は戦あたり: 暴発 回 ／ 一撃からの怒り ／ M-d が下げた量。");
        Console.WriteLine();
        foreach (var (name, f) in rows)
        {
            Console.WriteLine($"**{name}**（{S307.Seats(f)}）");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", waves.Select(w => w.Item1)) + " |");
            Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "---|")));
            var base0 = waves.Select(w => Cell(f, w.Item2)).ToArray();
            foreach (var v in Vers)
            {
                var g = Swap(f, v.D);
                var cells = v.Key == "0" ? base0 : waves.Select(w => Cell(g, w.Item2)).ToArray();
                Console.WriteLine($"| {v.Label} | " + string.Join(" | ", cells.Select((c, i) =>
                    $"{Pct(c.Wins, c.N)}{(v.Key == "0" ? "" : $"（{100.0 * (c.Wins - base0[i].Wins) / c.N:+0.0;-0.0;0.0}）")} ・{Per(c.Fires, c.N)} ／ {Per(c.Strike, c.N)} ／ {Per(c.Dull, c.N)}")) + " |");
            }
            Console.WriteLine();
        }
    }
    sealed class MC { public long N, Wins, Fires, Strike, Dull; }
    static MC Cell(Formation f, Func<Formation, int, BattleResult> run)
    {
        var parts = new MC[CompareSeeds];
        Parallel.For(0, CompareSeeds, s =>
        {
            var r = run(f, s);
            var c = new MC { N = 1, Wins = r.PlayerWon ? 1 : 0 };
            if (r.TallyByUnit.TryGetValue("mudo", out var t)) { c.Fires = t.EruptFires; c.Strike = t.Mu412Rage; c.Dull = t.Md412Dull; }
            parts[s] = c;
        });
        var a = new MC();
        foreach (var c in parts) { a.N += c.N; a.Wins += c.Wins; a.Fires += c.Fires; a.Strike += c.Strike; a.Dull += c.Dull; }
        return a;
    }

    // ---------------------------------------------------------------------------------
    // 第413期の下見: ワタの配り先（台本の CursePass「回す」＝ ウツ ／ ムド・ワタが書き手の敵の弱体 ＝ 敵へ渡した分）
    // ---------------------------------------------------------------------------------
    sealed class Wx
    {
        public long N;
        public readonly long[] ToU = new long[TT + 1], ToM = new long[TT + 1], ToE = new long[TT + 1], NU = new long[TT + 1], NM = new long[TT + 1], NE = new long[TT + 1], Live = new long[TT + 1];
        public void Add(Wx o) { N += o.N; for (int t = 0; t <= TT; t++) { ToU[t] += o.ToU[t]; ToM[t] += o.ToM[t]; ToE[t] += o.ToE[t]; NU[t] += o.NU[t]; NM[t] += o.NM[t]; NE[t] += o.NE[t]; Live[t] += o.Live[t]; } }
    }
    static Wx WataOne(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Wx { N = 1 };
        var wata = p.FirstOrDefault(u => u.Def.Id == "wata"); var utsu = p.FirstOrDefault(u => u.Def.Id == "utsu"); var mudo = p.FirstOrDefault(u => u.Def.Id == "mudo");
        if (wata is null) return a;
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        // ワタが生きているターン（ワタの StatSnapshot が出たターン）
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.StatSnapshot && x.TargetId == wata.InstanceId && x.Turn is >= 1 and <= TT) a.Live[x.Turn] = 1;
        foreach (var x in r.Events)
        {
            int t = x.Turn; if (t < 1 || t > TT) continue;
            if (x.Kind == BattleEventKind.CursePass && x.Text == CursePassLabels.Feed && x.ActorId == wata.InstanceId)
            {
                if (utsu is not null && x.TargetId == utsu.InstanceId) { a.ToU[t] += x.Amount; a.NU[t] = 1; }
                else if (mudo is not null && x.TargetId == mudo.InstanceId) { a.ToM[t] += x.Amount; a.NM[t] = 1; }
            }
            if (x.Kind == BattleEventKind.StatusGain && x.Text == BattleContext.DullKey && x.ActorId == wata.InstanceId && x.TargetId is int ti && foes.Contains(ti)) { a.ToE[t] += x.Amount; a.NE[t] = 1; }
        }
        return a;
    }
    static void Wata()
    {
        var b = BoardOf("試遊・呪い");
        Console.WriteLine($"# 第412期 §6-5 —— 第413期（ワタの渡し手番）の下見: 「試遊・呪い」（{S307.Seats(b)}）× {string.Join(" ／ ", Waves.Select(w => w.Name))} × {{規定, M-ud}}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("ワタが拾った重荷の行き先（ターンごと・戦の平均）: ウツへ回す ／ ムドへ回す ＝ 台本の「回す」（`CursePass`）・敵へ ＝ ワタが書き手の敵の弱体。"
            + "括弧はそのターンにその先へ1回でも回した戦の割合（ワタが生きていた戦のうち）。ワタが生きていた戦 ＝ そのターンの頭にワタの写しが出た戦。");
        Console.WriteLine();
        foreach (var w in Waves)
            foreach (var v in new[] { Vers[0], Vers[3] })
            {
                var parts = new Wx[Seeds];
                var f = Swap(b, v.D);
                Parallel.For(0, Seeds, s => parts[s] = WataOne(f, w, s));
                var a = new Wx(); foreach (var x in parts) a.Add(x);
                Console.WriteLine($"**{w.Name} × ムド {v.Label}**");
                Console.WriteLine();
                Console.WriteLine("| T | ワタが生きていた戦 | ウツへ（割合） | ムドへ（割合） | 敵へ（割合） |");
                Console.WriteLine("|--:|--:|---|---|---|");
                for (int t = 1; t <= 6; t++)
                {
                    long n = a.Live[t]; if (n == 0) { Console.WriteLine($"| {t} | 0 | — | — | — |"); continue; }
                    Console.WriteLine($"| {t} | {n} | {Per(a.ToU[t], n)}（{Pct(a.NU[t], n)}%） | {Per(a.ToM[t], n)}（{Pct(a.NM[t], n)}%） | {Per(a.ToE[t], n)}（{Pct(a.NE[t], n)}%） |");
                }
                Console.WriteLine();
            }
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int bad = 0;
        void Expect(string what, bool ok) { Console.WriteLine($"- {(ok ? "○" : "**×**")} {what}"); if (!ok) bad++; }
        Console.WriteLine("# 第412期 mudo412 check");
        Console.WriteLine();
        var m = UnitCatalog.Mudo;
        foreach (var (_, label, d) in Vers.Skip(1))
        {
            Expect($"{label}: `Id` ／ HP ／ 攻 ／ 速 ／ 型は規定と同じ・札は規定の後ろに足しただけ・`All` に無い",
                d.Id == m.Id && d.MaxHp == m.MaxHp && d.Attack == m.Attack && d.Speed == m.Speed && d.Pattern == m.Pattern
                && d.Traits.Take(m.Traits.Count).SequenceEqual(m.Traits) && !UnitCatalog.All.Contains(d));
        }
        Expect("ロスター（`All`）に M-u ／ M-d の札の保持者が 0 枚",
            !UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.MudoStrikeRage) || d.Traits.Contains(TraitId.MudoMireDull)));
        Expect("「版・M-ud 呪い」が `Presets.Versions` の末尾にあり、ムドが M-ud・ほかは「試遊・カーズ 呪い」と同じ",
            Presets.Versions[^1].Name == "版・M-ud 呪い"
            && Presets.Versions[^1].F.Occupied().Select(o => (o.Slot, o.Def)).SequenceEqual(Swap(BoardOf("試遊・カーズ 呪い"), UnitCatalog.MudoMud).Occupied().Select(o => (o.Slot, o.Def))));
        Expect("過去の器具の一覧（`VersionsH407`）に「版・M-ud 呪い」が無い", !VersionsH407().Any(r => r.Name == "版・M-ud 呪い"));
        // 規定の台で版の札が1つも鳴らない（保持者がいない戦は不活性）
        var b = BoardOf("試遊・カーズ 呪い");
        var z = Measure(b, I.StrongWaves[2]);
        Expect("規定のムドの台で M-u の怒り ／ M-d の発が 0", z.StrikeRage == 0 && z.MdSwings == 0);
        // M-u は一撃から怒る・M-d は下げる・M-d だけでは一撃から怒らない
        var u = Measure(Swap(b, UnitCatalog.MudoMu), I.Boss);
        var d0 = Measure(Swap(b, UnitCatalog.MudoMd), I.Boss);
        Expect($"M-u: ボスで一撃からの怒りが立つ（{Per(u.StrikeRage, u.N)} ／戦）・M-d の発は 0", u.StrikeRage > 0 && u.MdSwings == 0);
        Expect($"M-d: ボスで殴り返しが相手を下げる（{Per(d0.MdDull, d0.N)} ／戦）・一撃からの怒りは 0", d0.MdDull > 0 && d0.StrikeRage == 0);
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "**全部 ○**" : $"**× {bad} 件**");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
