using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// sh366 §3 —— 毒軸の棚卸し（数えるだけ・毒の駒の規則 ／ 数値には触らない）。
static partial class Sh366Diag
{
    const int FlowSeeds = 20, HeroT = 10;

    /// <summary>毒の駒（規定）。</summary>
    internal static readonly string[] PoisonIds = { "guza", "rau", "vio", "sid", "beni", "mio" };
    static UnitDef Cur(string id) => UnitCatalog.All.First(d => d.Id == id);

    /// <summary>§3-2 ／ 3-3 の台: `compare` の行のうち名前に「毒」か「澱」がある行（第348期の毒の台2つ ＝ 頭の2行を含む）。毒の試遊の台は無い（決めたこと）。</summary>
    internal static (string Name, Formation F)[] PoisonBoards() => CompareBuilds().Where(r => r.Name.Contains('毒') || r.Name.Contains('澱')).ToArray();

    static readonly I.W[] FlowWaves = { I.M5, I.Guard, I.Bat, I.Boss };

    /// <summary>勇者から蝕み（`VenomTaxSeal`）だけを外した波（§3-2 の 3 の対照・器具の中だけ）。</summary>
    static readonly UnitDef HeroNoTax = new()
    {
        Id = EnemyCatalog.BossRegular.Id, Name = EnemyCatalog.BossRegular.Name, MaxHp = EnemyCatalog.BossRegular.MaxHp, Attack = EnemyCatalog.BossRegular.Attack,
        Speed = EnemyCatalog.BossRegular.Speed, Pattern = EnemyCatalog.BossRegular.Pattern,
        Traits = EnemyCatalog.BossRegular.Traits.Where(t => t != TraitId.VenomTaxSeal).ToArray(), PlusText = EnemyCatalog.BossRegular.PlusText,
    };
    internal static readonly I.W BossNoTax = new("ボス（蝕みなし）", () => BattleEngine.MaterializeEnemy(EnemyWave.Of((2, HeroNoTax)), EnemyScaleRule.None), I.Boss.Scale, Boss: true);

    // ---------------------------------------------------------------------------------
    // 1戦の毒の流れ
    // ---------------------------------------------------------------------------------
    internal sealed class PF
    {
        public long N, Wins, WinT;
        /// <summary>駒（Def.Id・敵の駒は "敵"）× 向き（0 敵へ ／ 1 味方へ ／ 2 自分へ）の書いた層 ／ 減らした層。</summary>
        public readonly Dictionary<string, long[]> Wrote = new(), Took = new();
        /// <summary>毒の刻み（名目）: 敵へ ／ 味方へ ／ うち濃縮の印で増えた回（2回目以降）の敵へ ／ ベニの反転で癒しに変わった量（味方）。</summary>
        public long TickFoe, TickAlly, TickFoeExtra, TickInverted;
        /// <summary>駒ごとの敵への与ダメ（`Damage` の出来事・その駒が出どころのもの）。</summary>
        public readonly Dictionary<string, long> DealtFoe = new();
        /// <summary>勇者（ボスだけ）: ターンの頭の毒の層 ／ 蝕みの名目（min(回復, 層 × 37)）／ 入った回復 ／ 受けた量 ／ うち毒の刻み（名目）。添字はターン 1..HeroT。</summary>
        public readonly long[] HLayer = new long[HeroT + 1], HCut = new long[HeroT + 1], HHeal = new long[HeroT + 1], HTaken = new long[HeroT + 1], HTick = new long[HeroT + 1], HAlive = new long[HeroT + 1];
        public long MireDealt, ConcExtra, InverseHealed, Detonate;
        public void Add(PF o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT;
            foreach (var (k, v) in o.Wrote) { if (!Wrote.TryGetValue(k, out var a)) Wrote[k] = a = new long[3]; for (int i = 0; i < 3; i++) a[i] += v[i]; }
            foreach (var (k, v) in o.Took) { if (!Took.TryGetValue(k, out var a)) Took[k] = a = new long[3]; for (int i = 0; i < 3; i++) a[i] += v[i]; }
            TickFoe += o.TickFoe; TickAlly += o.TickAlly; TickFoeExtra += o.TickFoeExtra; TickInverted += o.TickInverted;
            foreach (var (k, v) in o.DealtFoe) DealtFoe[k] = DealtFoe.GetValueOrDefault(k) + v;
            for (int t = 0; t <= HeroT; t++) { HLayer[t] += o.HLayer[t]; HCut[t] += o.HCut[t]; HHeal[t] += o.HHeal[t]; HTaken[t] += o.HTaken[t]; HTick[t] += o.HTick[t]; HAlive[t] += o.HAlive[t]; }
            MireDealt += o.MireDealt; ConcExtra += o.ConcExtra; InverseHealed += o.InverseHealed; Detonate += o.Detonate;
        }
    }

    static string Key(UnitState u) => u.TeamId == BattleContext.PlayerTeam ? u.Def.Id : "敵";

    internal static PF FlowOne(Formation f, I.W w, int seed)
    {
        var a = new PF { N = 1 };
        // 計数の器具（`CounterProbe`）で毒の層の書き込み ／ 減りを、そのとき動いていた札の持ち主に帰属させる。**読むだけ**（盤面に触らない）。
        CounterProbe probe = (trait, owner, target, key, delta) =>
        {
            // 増えた分は `NoteCarry` から帳簿の名前（`UnitTally.CarryKeys` の「毒」）で、減った分は `SetCounter` から状態のキー（`poison`）で来る。
            if (delta == 0 || !(delta > 0 ? key == UnitTally.CarryKeys[UnitTally.CarryPoison] : key == StatusKeys.Poison)) return;
            int rel = ReferenceEquals(owner, target) ? 2 : owner.TeamId == target.TeamId ? 1 : 0;
            var d = delta > 0 ? a.Wrote : a.Took;
            string k = Key(owner);
            if (!d.TryGetValue(k, out var arr)) d[k] = arr = new long[3];
            arr[rel] += Math.Abs(delta);
        };
        var (r, p, e) = I.Raw(f, w, seed, verbose: true, probe: probe);
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var mine = p.ToDictionary(u => u.InstanceId);
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        var hero = w.Boss ? e.FirstOrDefault(u => u.HasTrait(TraitId.BossMend40)) : null;
        // 召喚 ／ 蘇生で後から立った駒も含める（イベントの TargetId から陣営を引く）。
        foreach (var x in r.Events)
        {
            bool onFoe = x.TargetId is int ti && !mine.ContainsKey(ti);
            switch (x.Kind)
            {
                case BattleEventKind.Status when x.Text == "毒":
                    if (x.InverterId is not null) { a.TickInverted += x.Amount; break; }
                    if (onFoe) { a.TickFoe += x.Amount; if (x.TickIndex is int ix && ix > 1) a.TickFoeExtra += x.Amount; }
                    else a.TickAlly += x.Amount;
                    if (hero is not null && x.TargetId == hero.InstanceId && x.Turn is >= 1 and <= HeroT) a.HTick[x.Turn] += x.Amount;
                    break;
                case BattleEventKind.Damage:
                    if (x.ActorId is int ai && mine.TryGetValue(ai, out var src) && onFoe) a.DealtFoe[src.Def.Id] = a.DealtFoe.GetValueOrDefault(src.Def.Id) + x.Amount;
                    if (hero is not null && x.TargetId == hero.InstanceId && x.Turn is >= 1 and <= HeroT) a.HTaken[x.Turn] += x.Amount;
                    break;
                case BattleEventKind.StatusSnapshot when hero is not null && x.Text == "毒" && x.TargetId == hero.InstanceId && x.Turn is >= 1 and <= HeroT:
                    a.HLayer[x.Turn] += x.Amount;
                    break;
                case BattleEventKind.Heal when hero is not null && x.TargetId == hero.InstanceId && x.ActorId == hero.InstanceId && x.Turn is >= 1 and <= HeroT:
                    a.HHeal[x.Turn] += x.Amount;
                    break;
            }
        }
        if (hero is not null)
        {
            int mend = hero.MaxHp * BossMend(hero) / 100;
            int last = hero.LastDeathTurn > 0 ? hero.LastDeathTurn : r.Turns;
            for (int t = 1; t <= Math.Min(HeroT, last); t++) { a.HAlive[t] = 1; a.HCut[t] = Math.Min(mend, a.HLayer[t] * VenomTaxTrait.Seal); }
        }
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (id == "mio") { a.MireDealt += t.MireSlamDealt; }
            if (id == "beni") a.InverseHealed += t.InversePoisonHealed;
            a.Detonate += t.DetonatePoisonNominal;
        }
        a.ConcExtra = a.TickFoeExtra;
        return a;
    }
    static int BossMend(UnitState hero) => hero.Traits.OfType<BossMendTrait>().FirstOrDefault()?.Percent ?? 0;

    internal static PF FlowMeasure(Formation f, I.W w, int n = FlowSeeds)
    {
        var parts = new PF[n];
        Parallel.For(0, n, i => parts[i] = FlowOne(f, w, i));
        var all = new PF();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static string Nm(string id) => id == "敵" ? "敵" : Short(UnitCatalog.Everyone.First(d => d.Id == id));

    static void Flow()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = PoisonBoards();
        Console.WriteLine($"# {Tag} §3-2 毒の流れ —— 毒の行 {boards.Length} 台 × {string.Join(" ／ ", FlowWaves.Select(w => w.Name))}・seed 0..{FlowSeeds - 1}・1戦あたり");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。");
        Console.WriteLine();
        Console.WriteLine("書いた層 ／ 減らした層は計数の器具（`CounterProbe`）で、そのとき動いていた札の持ち主に帰属させた（札の外で engine が直に書いた分は入らない）。刻みは名目（`Status` の出来事の量・ベニの反転で癒しに変わった分は別に数えた）。与ダメはその駒が出どころの `Damage`（刻みは出どころなし）。");
        Console.WriteLine();
        var res = new PF[boards.Length, FlowWaves.Length];
        Parallel.For(0, boards.Length * FlowWaves.Length, k => res[k / FlowWaves.Length, k % FlowWaves.Length] = FlowMeasure(boards[k / FlowWaves.Length].F, FlowWaves[k % FlowWaves.Length]));

        Console.WriteLine("## 1〜2. 書いた毒の層・刻み・与ダメ（駒ごと）");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
        {
            Console.WriteLine($"### {boards[bi].Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 書いた層（敵へ ／ 味方へ ／ 自分へ）| 減らした層（味方から ／ 自分から ／ 敵から）| 刻み 敵へ（うち濃縮の2回目以降）／ 味方へ ／ 反転で癒し | 敵への与ダメ（駒ごと）| ミオの澱み ／ 澱みの爆発（名目） |");
            Console.WriteLine("|---|--:|---|---|---|---|---|");
            for (int wi = 0; wi < FlowWaves.Length; wi++)
            {
                var a = res[bi, wi];
                string wrote = string.Join("・", a.Wrote.OrderByDescending(kv => kv.Value.Sum()).Select(kv => $"{Nm(kv.Key)} {Per(kv.Value[0], a.N)} ／ {Per(kv.Value[1], a.N)} ／ {Per(kv.Value[2], a.N)}"));
                string took = string.Join("・", a.Took.OrderByDescending(kv => kv.Value.Sum()).Select(kv => $"{Nm(kv.Key)} {Per(kv.Value[1], a.N)} ／ {Per(kv.Value[2], a.N)} ／ {Per(kv.Value[0], a.N)}"));
                string dealt = string.Join("・", a.DealtFoe.OrderByDescending(kv => kv.Value).Select(kv => $"{Nm(kv.Key)} {Per(kv.Value, a.N)}"));
                Console.WriteLine($"| {FlowWaves[wi].Name} | {F1(100.0 * a.Wins / a.N)} | {wrote} | {(took.Length == 0 ? "—" : took)} | {Per(a.TickFoe, a.N)}（{Per(a.TickFoeExtra, a.N)}）／ {Per(a.TickAlly, a.N)} ／ {Per(a.TickInverted, a.N)} | {dealt} | {Per(a.MireDealt, a.N)} ／ {Per(a.Detonate, a.N)} |");
            }
            Console.WriteLine();
        }

        // 3〜4. 勇者
        int bw = Array.IndexOf(FlowWaves, I.Boss);
        Console.WriteLine("## 3. 勇者の蝕み（ボス・ターンごと・勇者が生きていたターンの平均）");
        Console.WriteLine();
        Console.WriteLine($"層はターンの頭（刻みの後）の毒の層。蝕みの名目 ＝ min(回復の名目 {I.Boss.Make().First().MaxHp * 40 / 100}, 層 × {VenomTaxTrait.Seal})。入った回復は勇者の自前の回復が実際に戻した HP（満タンに近ければ名目より少ない）。");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
        {
            var a = res[bi, bw];
            Console.WriteLine($"- **{boards[bi].Name}**（ボス {F1(100.0 * a.Wins / a.N)}%・{(a.Wins == 0 ? "—" : F1((double)a.WinT / a.Wins))}T）: " + string.Join(" ／ ", Enumerable.Range(1, HeroT).Where(t => a.HAlive[t] > 0).Select(t => $"T{t} 層 {Per(a.HLayer[t], a.HAlive[t])}・蝕み {Per(a.HCut[t], a.HAlive[t])}・回復 {Per(a.HHeal[t], a.HAlive[t])}")));
        }
        Console.WriteLine();
        Console.WriteLine("### 蝕みを外した勇者（器具の中だけの対照・seed 0..99）");
        Console.WriteLine();
        Console.WriteLine("| 台 | ボス（規定）| ボス（蝕みなし）| 差 |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var bd in boards)
        {
            var b0 = Measure(bd.F, I.Boss); var b1 = Measure(bd.F, BossNoTax);
            Console.WriteLine($"| {bd.Name} | {F1(b0.Win)}・{b0.WinTs}T | {F1(b1.Win)}・{b1.WinTs}T | {Sg(b1.Win - b0.Win)} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 4. 1ターンの勇者への与ダメの推移（ボス）と、そのうち毒の刻み（名目）の割合");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Enumerable.Range(1, HeroT).Select(t => $"T{t}")) + " | 合計の刻みの割合 |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, HeroT).Select(_ => "---|")) + "--:|");
        for (int bi = 0; bi < boards.Length; bi++)
        {
            var a = res[bi, bw];
            long tk = a.HTick.Sum(), tt = a.HTaken.Sum();
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(1, HeroT).Select(t => a.HAlive[t] == 0 ? "—" : $"{Per(a.HTaken[t], a.HAlive[t])}（{Pct(Math.Min(a.HTick[t], a.HTaken[t]), a.HTaken[t])}%）"))} | {Pct(Math.Min(tk, tt), tt)}% |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-3 駒ごとの効き（素体に替える）
    // ---------------------------------------------------------------------------------
    static readonly I.W[] SwapWaves = { I.M5, I.Guard, I.Bat, I.Boss };

    /// <summary>毒の駒を同じ席の素体（同数値・札なし・`checkup ideal` の作り）に替えた台（器具の中だけ・規定の駒は変えない）。</summary>
    internal static Formation ToPlain(Formation f, string id) => S316.SwapId(f, id, I.Plain(f.Occupied().First(o => o.Def.Id == id).Def));

    static void SwapAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = PoisonBoards();
        Console.WriteLine($"# {Tag} §3-3 駒ごとの効き —— 毒の駒を同じ席の素体（同数値・札なし・ふつうに殴る）に替えた差（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("各セルは「規定 → 素体に替えた台」の勝率（差）。替えたのはその行の毒の駒（" + string.Join("・", PoisonIds.Select(i => Short(Cur(i)))) + "）1枚ずつ。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 替えた駒 | " + string.Join(" | ", SwapWaves.Select(w => w.Name)) + " | 4波の差の平均 |");
        Console.WriteLine("|---|---|" + string.Concat(SwapWaves.Select(_ => "---|")) + "--:|");
        var perUnit = new Dictionary<string, List<double>>();
        foreach (var bd in boards)
        {
            var b = SwapWaves.Select(w => Measure(bd.F, w)).ToArray();
            Console.WriteLine($"| {bd.Name} | （規定） | {string.Join(" | ", b.Select(a => $"{F1(a.Win)}・{a.WinTs}T"))} | |");
            foreach (var id in PoisonIds.Where(i => Has(bd.F, i)))
            {
                var g = SwapWaves.Select(w => Measure(ToPlain(bd.F, id), w)).ToArray();
                double avg = Enumerable.Range(0, SwapWaves.Length).Average(i => g[i].Win - b[i].Win);
                (perUnit.TryGetValue(id, out var l) ? l : perUnit[id] = new List<double>()).Add(avg);
                Console.WriteLine($"| | {Short(Cur(id))} → 素体 | {string.Join(" | ", Enumerable.Range(0, SwapWaves.Length).Select(i => $"{F1(g[i].Win)}（{Sg(g[i].Win - b[i].Win)}）"))} | {Sg(avg)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 駒ごとのまとめ（在席した毒の行での 4 波の差の平均）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席 | 差の平均 | 最も落ちた行の差 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var id in PoisonIds)
            if (perUnit.TryGetValue(id, out var l)) Console.WriteLine($"| {Short(Cur(id))} | {l.Count} | {Sg(l.Average())} | {Sg(l.Min())} |");
            else Console.WriteLine($"| {Short(Cur(id))} | 0 | — | — |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
