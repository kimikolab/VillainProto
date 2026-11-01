using BattleCore;
using static Common;
using I = Inv348Diag;
using S430 = Som430Diag;

// =====================================================================================
// boss435 —— 第435期【Phase 0・測るだけ】反撃 ／ 破片 ／ 自傷のボス 0% は同じ理由か。規定も版も動かさない。engine も触らない（台本と汎用の計数だけで数える）。
// 指示書は design/PHASE435_BOSS_ZERO_PHASE0_SPEC.md ／ 報告は design/PHASE435_BOSS_ZERO_PHASE0.md。
//
//     dotnet run --project BattleSim -c Release 0 boss435 run     # §2: 3 軸 × 2 台 ＋ 対照 2 台 × ボス（seed 0..199）・起点は 本編 第4波 ／ 近衛 ／ 大隊 とも比べる
//     dotnet run --project BattleSim -c Release 0 boss435 check   # 自己検査（台の顔ぶれ・勇者の識別・数えた量の帳尻）
// =====================================================================================
static class Boss435Diag
{
    const int Seeds = 200;

    internal sealed record Board(string Axis, string Name, string Why);
    /// <summary>§1 の台（3 軸 × 2 ＋ 対照 2）。行の名前は `compare` ／ `Presets.Playtest` ／ `Presets.Versions` から引く。</summary>
    internal static readonly Board[] Boards =
    {
        new("反撃", "反撃 (ヒサ×カド)", "第414期の反撃の代表 1（指示書の名指し）"),
        new("反撃", "反撃改 (ドハ×カド)", "第414期の反撃の代表 2（ドハの中継でカドが返す）"),
        new("破片", "礫 (ガレ×ヒビ×ウロ)", "第414期の破片の代表 1（ウロは第429期から旧のまま）"),
        new("破片", "試遊・破片", "ウロのいない破片の台（ササ・ヒビ・ガレ・第361期）"),
        new("自傷", "試遊・自傷", "カド ／ アカの自傷の試遊の台（第365期）"),
        new("自傷", "逆しま (ネル×ウツ)", "第414期の自傷の代表（ボス 12%・ウツの自傷）"),
        new("対照", "試遊・光の衣 ソム", "感電（ボスを塞いだ軸・第321〜328期 ／ 第434期の規定）"),
        new("対照", "試遊・呪い", "デバフ（ボスを塞いだ軸・第411〜413期）"),
        new("対照", "試遊・感電 ポンの台", "感電でボスに勝つ台（指示書の対照「試遊・光の衣 ソム」はボス 3.0 なので、塞いだ側の感電を 1 台足した）"),
    };

    internal static Formation RowOf(string name)
        => CompareBuilds().Concat(Presets.Playtest).Concat(Presets.Versions).First(r => r.Name == name).F;

    static bool IsHero(UnitState u) => u.Def.Id is "boss_regular" or "boss_hero";

    sealed class Fx
    {
        public long N, Wins, WinT, Turns;
        public long Dealt, Healed, ReachBattles, RatioOver, MaxTurnSum, MaxTurnMax;
        public long CounterAtk, CounterDmg, ArmorLost, Reflects, SelfHits, SelfDmg, BloodCasts;
        public readonly double[] DealtByTurn = new double[31], HealByTurn = new double[31];
        public readonly long[] TurnN = new long[31];
        public long HealCap;   // 勇者の 1 手番の回復の名目（開始時の最大 HP の 40%）
        public long EndHp;     // 勇者の終わりの HP
        public long MaxCut;    // 勇者の最大 HP の削り（開始時 − 終わり・回復の上限そのものが下がる）
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public long WipeT, Wipes;
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Turns += o.Turns;
            Dealt += o.Dealt; Healed += o.Healed; ReachBattles += o.ReachBattles; RatioOver += o.RatioOver; MaxTurnSum += o.MaxTurnSum; MaxTurnMax = Math.Max(MaxTurnMax, o.MaxTurnMax);
            CounterAtk += o.CounterAtk; CounterDmg += o.CounterDmg; ArmorLost += o.ArmorLost; Reflects += o.Reflects; SelfHits += o.SelfHits; SelfDmg += o.SelfDmg; BloodCasts += o.BloodCasts;
            for (int i = 0; i < 31; i++) { DealtByTurn[i] += o.DealtByTurn[i]; HealByTurn[i] += o.HealByTurn[i]; TurnN[i] += o.TurnN[i]; }
            HealCap = Math.Max(HealCap, o.HealCap); MaxCut += o.MaxCut; EndHp += o.EndHp;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
            WipeT += o.WipeT; Wipes += o.Wipes;
        }
    }

    static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Fx { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var pIds = p.Select(u => u.InstanceId).ToHashSet();
        var hero = e.FirstOrDefault(IsHero);
        if (hero is not null)
        {
            a.HealCap = hero.Def.MaxHp * 40 / 100;   // 最大 HP は戦いの中で削られる（第435期の帳尻の調べ）ので、開始時の値で名目を取る
            a.MaxCut = hero.Def.MaxHp - hero.MaxHp;
            a.EndHp = Math.Max(0, hero.Hp);
            var dealt = new long[31]; var healed = new long[31];
            foreach (var ev in r.Events)
            {
                if (ev.TargetId != hero.InstanceId || ev.Turn < 0 || ev.Turn > 30) continue;
                if (ev.Kind == BattleEventKind.Damage) dealt[ev.Turn] += ev.Amount;
                else if (ev.Kind == BattleEventKind.Heal) healed[ev.Turn] += ev.Amount;
            }
            for (int t = 1; t <= Math.Min(30, r.Turns); t++) { a.DealtByTurn[t] = dealt[t]; a.HealByTurn[t] = healed[t]; a.TurnN[t] = 1; }
            long mx = dealt.Max();
            a.Dealt = dealt.Sum(); a.Healed = healed.Sum(); a.MaxTurnSum = mx; a.MaxTurnMax = mx;
            if (mx >= a.HealCap) a.ReachBattles = 1;
            if (a.Dealt > a.Healed) a.RatioOver = 1;
        }
        // 起点（味方の側）: 反撃 ＝ 味方の `Reaction` の傷（敵に入った段） ／ 破片 ＝ 減った破片の延べ（`Lv332ArmorLost`）と板の返し ／ 自傷 ＝ 味方が味方（自分を含む）に入れた傷と血詠み
        for (int i = 0; i < r.Events.Count; i++)
        {
            var ev = r.Events[i];
            bool fromAlly = ev.ActorId is int aid && pIds.Contains(aid);
            if (ev.Kind == BattleEventKind.Damage && ev.Reaction && fromAlly && ev.TargetId is int tid && !pIds.Contains(tid)) { a.CounterAtk++; a.CounterDmg += ev.Amount; }   // 棘・仇討ち ／ 軋みの段（カドの棘は `Attack` を出さない）
            if (ev.Kind == BattleEventKind.Damage && fromAlly && !ev.Relayed && ev.TargetId is int sid && pIds.Contains(sid)) { a.SelfHits++; a.SelfDmg += ev.Amount; }
        }
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!p.Any(u => u.Def.Id == id)) continue;
            a.ArmorLost += t.Lv332ArmorLost; a.Reflects += t.ReflectByPlank; a.BloodCasts += t.Sh363Casts;
        }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { string k = u.Def.Name; a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (!r.PlayerWon && p.All(u => !u.IsAlive)) { a.Wipes = 1; a.WipeT = p.Max(u => u.LastDeathTurn); }
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
    static string Frac(double boss, double other) => other <= 0 ? "—" : boss <= 0 ? "0" : $"1/{other / boss:F1}";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "run": RunAll(); break;
            case "check": Check(); break;
            case "probe": Probe(int.Parse(args.Length > 3 ? args[3] : "0")); break;
            default: Console.WriteLine("boss435: モードは run / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void RunAll()
    {
        var waves = new[] { ("ボス", I.Boss), ("本編 第4波", S430.Waves[4]), ("近衛", I.Guard), ("大隊", I.Bat) };
        var res = new Dictionary<(int B, int W), Fx>();
        for (int bi = 0; bi < Boards.Length; bi++)
            for (int wi = 0; wi < waves.Length; wi++)
                res[(bi, wi)] = Measure(RowOf(Boards[bi].Name), waves[wi].Item2);

        Console.WriteLine($"# 第435期 —— 反撃 ／ 破片 ／ 自傷のボス 0% は同じ理由か（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var (b, i) in Boards.Select((b, i) => (b, i))) Console.WriteLine($"- {i + 1}. 【{b.Axis}】{b.Name}（{Som307Diag.Seats(RowOf(b.Name))}）——{b.Why}");
        Console.WriteLine();

        Console.WriteLine("## 表1 ボス: 勝率 ／ 勇者に入れた量と回復（1 戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 軸 | 勝率・倒しT | 決着T | 1 手番の回復の名目 | 1 手番の最大（平均 ／ 最大） | 最大が回復に届いた戦 | 入れた量の合計 | 回復した量の合計 | 比 入れた ÷ 回復 | 正味（入れた − 回復） | 最大 HP の削り | 勇者の終わりの HP |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            var x = res[(bi, 0)];
            string wt = x.Wins == 0 ? Pc(x.Wins, x.N) : $"{Pc(x.Wins, x.N)}・{(double)x.WinT / x.Wins:F1}T";
            Console.WriteLine($"| {bi + 1} {Boards[bi].Name} | {Boards[bi].Axis} | {wt} | {Per(x.Turns, x.N)} | {x.HealCap:N0} | {Per(x.MaxTurnSum, x.N)} ／ {x.MaxTurnMax:N0} | {Pc(x.ReachBattles, x.N)}% | {Per(x.Dealt, x.N)} | {Per(x.Healed, x.N)} | {(x.Healed == 0 ? "—" : ((double)x.Dealt / x.Healed).ToString("F2"))} | {Per(x.Dealt - x.Healed, x.N)} | {Per(x.MaxCut, x.N)} | {Per(x.EndHp, x.N)} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表2 ボス: 手番ごとに勇者へ入れた量 ／ 勇者が回復した量（その手番まで続いた戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | T1 | T2 | T3 | T4 | T5 | T6 | T8 | T10 | T12 | T15 | T20 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
        int[] ts = { 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20 };
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            var x = res[(bi, 0)];
            string C(int t) => x.TurnN[t] == 0 ? "—" : $"{x.DealtByTurn[t] / x.TurnN[t]:F0} ／ {x.HealByTurn[t] / x.TurnN[t]:F0}（{100.0 * x.TurnN[t] / x.N:F0}%）";
            Console.WriteLine($"| {bi + 1} | {string.Join(" | ", ts.Select(C))} |");
        }
        Console.WriteLine();
        Console.WriteLine("（各セル ＝ 入れた量 ／ 回復した量（その手番まで戦が続いた割合））");
        Console.WriteLine();

        Console.WriteLine("## 表3 起点の回数（1 戦あたり ／ 1 手番あたり）と、本編 第4波 ・ 近衛 ・ 大隊 に対する何分の 1 か（1 手番あたりで比べる）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 軸 | 波 | 決着T | 反撃の傷（回数） | 反撃で入れた量 | 減った破片 | 板の返し | 自傷の傷（回数 ／ 量） | 血詠み |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|--:|");
        for (int bi = 0; bi < Boards.Length; bi++)
            for (int wi = 0; wi < waves.Length; wi++)
            {
                var x = res[(bi, wi)];
                string P(long v) => $"{Per(v, x.N)} ／ {(x.Turns == 0 ? "—" : ((double)v / x.Turns).ToString("F2"))}";
                Console.WriteLine($"| {bi + 1} | {Boards[bi].Axis} | {waves[wi].Item1} | {Per(x.Turns, x.N)} | {P(x.CounterAtk)} | {Per(x.CounterDmg, x.N)} | {P(x.ArmorLost)} | {Per(x.Reflects, x.N)} | {Per(x.SelfHits, x.N)} ／ {Per(x.SelfDmg, x.N)} | {Per(x.BloodCasts, x.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("### 表3b その軸の起点の 1 手番あたりの量: ボス ÷ ほかの波（反撃 ＝ 反撃の傷の回数 ／ 破片 ＝ 減った破片 ／ 自傷 ＝ 自傷の回数）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 軸 | ボス | 本編 第4波 | 近衛 | 大隊 | ボスは 第4波 ／ 近衛 ／ 大隊 の |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            string axis = Boards[bi].Axis;
            double V(Fx x) => x.Turns == 0 ? 0 : (axis switch { "反撃" => x.CounterAtk, "破片" => x.ArmorLost, "自傷" => x.SelfHits, _ => x.CounterAtk + x.SelfHits }) / (double)x.Turns;
            if (axis == "対照") continue;
            double b0 = V(res[(bi, 0)]), m4 = V(res[(bi, 1)]), g = V(res[(bi, 2)]), bt = V(res[(bi, 3)]);
            Console.WriteLine($"| {bi + 1} {Boards[bi].Name} | {axis} | {b0:F2} | {m4:F2} | {g:F2} | {bt:F2} | {Frac(b0, m4)} ／ {Frac(b0, g)} ／ {Frac(b0, bt)} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表4 ボス: 倒れ方（倒れた割合・平均の倒れたT・早い順）と全滅のT");
        Console.WriteLine();
        Console.WriteLine("| 台 | 全滅した戦 ／ 全滅のT | 倒れた味方 |");
        Console.WriteLine("|---|---|---|");
        for (int bi = 0; bi < Boards.Length; bi++)
        {
            var x = res[(bi, 0)];
            string fall = x.Death.Count == 0 ? "—" : string.Join("、", x.Death.OrderBy(kv => (double)x.DeathT[kv.Key] / kv.Value).Select(kv => $"{kv.Key} {Pc(kv.Value, x.N)}%・T{Per(x.DeathT[kv.Key], kv.Value)}"));
            Console.WriteLine($"| {bi + 1} {Boards[bi].Name} | {Pc(x.Wipes, x.N)}% ／ T{Per(x.WipeT, x.Wipes)} | {fall} |");
        }
        Console.WriteLine();
    }

    /// <summary>勇者の HP の動きを台本の出来事で追い、`HpAfter` と量が食い違う出来事を出す（帳尻の調べ）。</summary>
    static void Probe(int seed)
    {
        var (r, _, e) = I.Raw(RowOf(Boards[0].Name), I.Boss, seed, verbose: true);
        var hero = e.First(IsHero);
        int hp = hero.MaxHp;
        foreach (var ev in r.Events.Where(ev => ev.TargetId == hero.InstanceId && ev.Kind is BattleEventKind.Damage or BattleEventKind.Heal))
        {
            int want = ev.Kind == BattleEventKind.Damage ? hp - ev.Amount : hp + ev.Amount;
            if (want != ev.HpAfter) Console.WriteLine($"T{ev.Turn} {ev.Kind} 量 {ev.Amount} 前 {hp} 後 {ev.HpAfter}（差 {ev.HpAfter - want}）{ev.Text}");
            hp = ev.HpAfter;
        }
        Console.WriteLine($"終わり: 台本の最後 {hp} ／ 駒 {hero.Hp}");
        foreach (var l in r.Log.Where(l => l.Text.Contains("勇者") && (l.Text.Contains("回復") || l.Text.Contains("塞が") || l.Text.Contains("+"))).Take(12)) Console.WriteLine("  " + l.Text);
    }

    static void Check()
    {
        bool ok = true;
        void Req(bool c, string what) { Console.WriteLine($"{(c ? "○" : "×")} {what}"); ok &= c; }
        foreach (var b in Boards)
        {
            bool found = CompareBuilds().Concat(Presets.Playtest).Concat(Presets.Versions).Any(r => r.Name == b.Name);
            Req(found, $"台「{b.Name}」が `compare` ／ 試遊の台にある");
        }
        var (r, p, e) = I.Raw(RowOf(Boards[0].Name), I.Boss, 0, verbose: true);
        var hero = e.FirstOrDefault(IsHero);
        Req(hero is not null && e.Count(IsHero) == 1, $"ボスの波の勇者は 1 体（{string.Join("・", e.Select(u => u.Def.Id))}）");
        if (hero is not null)
        {
            Req(hero.Def.MaxHp == 3000, $"勇者の開始時の最大 HP {hero.Def.MaxHp:N0}・1 手番の回復の名目 {hero.Def.MaxHp * 40 / 100:N0}");
            var evs = r.Events.Where(ev => ev.TargetId == hero.InstanceId && ev.Kind is BattleEventKind.Damage or BattleEventKind.Heal).ToList();
            Req(evs.Count > 0 && evs[^1].HpAfter == hero.Hp, $"台本の勇者の最後の HP（{(evs.Count > 0 ? evs[^1].HpAfter : -1):N0}）＝ 駒の終わりの HP（{hero.Hp:N0}）");
            Req(hero.Def.MaxHp >= hero.MaxHp, $"勇者の最大 HP は開始 {hero.Def.MaxHp:N0} → 終わり {hero.MaxHp:N0}（削られることはあっても増えない）");
        }
        var (r0, _, _) = I.Raw(RowOf(Boards[0].Name), I.Boss, 0, verbose: false);
        Req(r0.PlayerWon == r.PlayerWon && r0.Turns == r.Turns, "verbose の有無で勝敗 ／ T が変わらない");
        Console.WriteLine(ok ? "boss435 check: すべて ○" : "boss435 check: × あり");
        if (!ok) Environment.ExitCode = 1;
    }
}
