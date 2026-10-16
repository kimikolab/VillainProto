using BattleCore;
using static Common;

// inv348 §5 —— 移動の内訳（ヨミ ／ ハネ ／ バサ ／ シオ ／ セロ）と、第232期（移動軸の作り直しの最後）との比べ。
static partial class Inv348Diag
{
    static readonly string[] Movers = { "yomi", "hane", "basa", "shio", "sero" };

    static Board[] MoveBoards() => new[]
    {
        new Board("移動", "移動 (チェック波の台)", MoveCheck),
        new Board("移動", "移動改 (バサ×ヨミ×シオ)", Row("移動改 (バサ×ヨミ×シオ)")),
        new Board("移動", "隊列崩し (バサ×ヨミ×セロ)", Row("隊列崩し (バサ×ヨミ×セロ)")),
        new Board("移動", "突き返し (ハネ×ウツ)", Row("突き返し (ハネ×ウツ)")),
    };

    /// <summary>
    /// 第232期の終わり（f60da491・移動軸の作り直しの最後）の規定に駒を戻す（決めたこと）: ハネ → <see cref="UnitCatalog.HaneR0"/>（第232期の規定 S2）／ セロ → <see cref="UnitCatalog.SeroS0"/>（第232期 前段・状態の矢つき）。
    /// ヨミ（第230期）・バサ（第230期）・シオ（第232期）・ウツ（第178期）はその後に手が入っていない。
    /// 燃焼の3枚は第233期の棚卸しの前の規定（<see cref="UnitCatalog.BorgF0"/> ／ <see cref="UnitCatalog.HotaL0"/> ／ <see cref="UnitCatalog.HiyoF0"/>）——火勢・大技・陣営の規則の札はボルグの札なので、まとめて抜ける。
    /// </summary>
    static Formation Pin232(Formation f) =>
        Swap(Swap(Swap(Swap(Swap(f, "hane", UnitCatalog.HaneR0), "sero", UnitCatalog.SeroS0), "borg", UnitCatalog.BorgF0), "hota", UnitCatalog.HotaL0), "hiyo", UnitCatalog.HiyoF0);

    sealed record Era(string Name, Func<Formation, Formation> Map, EmberRule? Ember);
    static readonly Era[] Eras =
    {
        new("今", f => f, null),
        new("今の駒・被弾の燃焼なし", f => f, EmberRule.Pre256),
        new("第232期の駒", Pin232, null),
        new("第232期の駒・被弾の燃焼なし（≒ 第232期の盤面）", Pin232, EmberRule.Pre256),
    };

    sealed class MvAgg
    {
        public long N;
        public readonly Dictionary<string, long[]> U = new();   // id → [与ダメ, 手番の外の与ダメ, 手番の外で振った, 動かした, 動かされた, 生きていたT]
        public void Merge(MvAgg o) { N += o.N; foreach (var (k, v) in o.U) { if (!U.TryGetValue(k, out var a)) U[k] = a = new long[6]; for (int i = 0; i < 6; i++) a[i] += v[i]; } }
    }

    static MvAgg MoveCensus(BattleResult r, List<UnitState> p)
    {
        var m = new MvAgg { N = 1 };
        var byInst = p.ToDictionary(u => u.InstanceId, u => u.Def.Id);
        var ev = r.Events;
        var own = new int[ev.Count];
        for (int i = 0; i < own.Length; i++) own[i] = -1;
        foreach (var h in r.Hands.OrderByDescending(h => h.EventEnd - h.EventStart))
            for (int i = h.EventStart; i < Math.Min(h.EventEnd, ev.Count); i++) own[i] = h.ActorId;
        foreach (var u in p.Where(u => Movers.Contains(u.Def.Id)))
        {
            var a = m.U[u.Def.Id] = new long[6];
            if (r.TallyByUnit.TryGetValue(u.Def.Id, out var t)) { a[0] = t.DamageToEnemy; a[1] = t.DmgOutOffTurn; }
            a[5] = u.LastDeathTurn > 0 ? u.LastDeathTurn : r.Turns;
        }
        for (int i = 0; i < ev.Count; i++)
        {
            var x = ev[i];
            if (x.Kind == BattleEventKind.Attack && x.ActorId is int ai && byInst.TryGetValue(ai, out var aid) && m.U.TryGetValue(aid, out var aa) && own[i] != ai) aa[2]++;
            if (x.Kind == BattleEventKind.Move)
            {
                if (x.ActorId is int mi && byInst.TryGetValue(mi, out var mid) && m.U.TryGetValue(mid, out var ma)) ma[3]++;
                if (x.TargetId is int ti && byInst.TryGetValue(ti, out var tid) && m.U.TryGetValue(tid, out var ta)) ta[4]++;
            }
        }
        return m;
    }

    static void Move()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = MoveBoards();
        W[] mw = { Boss, Bat, Nine, M2 };
        Console.WriteLine($"# {Tag} §5 移動の内訳 —— 台 {boards.Length} × {mw.Length} 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();

        Console.WriteLine("## 5-1. 移動の駒の働き（規定・1戦あたり・台本を読む）");
        Console.WriteLine();
        Console.WriteLine("与ダメ（うち手番の外）／ 手番の外で振った回数（割り込み・反撃・撃ち返し・追撃・吹っ飛ばしの打撃など・自分の手番の枠の外の `Attack`）／ 動かした回数（`Move` の行為者）／ 動かされた回数 ／ 生きていた T。");
        Console.WriteLine();
        foreach (var b in boards)
        {
            var ids = b.F.Occupied().Select(o => o.Def).Where(d => Movers.Contains(d.Id)).ToArray();
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率・T | " + string.Join(" | ", ids.Select(Short)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(ids.Select(_ => "---|")));
            foreach (var w in mw)
            {
                var parts = new (MvAgg M, St S)[Seeds];
                Parallel.For(0, Seeds, i => { var (r, p, _) = Raw(b.F, w, i, verbose: true); parts[i] = (MoveCensus(r, p), Take(r, p, w)); });
                var m = new MvAgg(); var s = new St();
                foreach (var (a, z) in parts) { m.Merge(a); s.Add(z); }
                Console.WriteLine($"| {w.Name} | {F1(s.Win)}・{s.WinTs}T | " + string.Join(" | ", ids.Select(d =>
                {
                    var a = m.U.GetValueOrDefault(d.Id) ?? new long[6];
                    return $"{Per(a[0], m.N)}（外 {Per(a[1], m.N)}）／ 外振 {Per(a[2], m.N)} ／ 動 {Per(a[3], m.N)} ／ 被動 {Per(a[4], m.N)} ／ {Per(a[5], m.N)}T";
                })) + " |");
            }
            Console.WriteLine();
        }

        // ---- 5-2: 第232期との比べ（移動の台 ／ 燃焼の台）----
        var cmp = boards.Take(2).Concat(BurnBoards()).ToArray();
        Console.WriteLine("## 5-2. 第232期（f60da491）の駒・規則と今を、同じ物差しで（§3 の物差し全部）");
        Console.WriteLine();
        Console.WriteLine("「第232期の駒」＝ ハネ `HaneR0` ／ セロ `SeroS0` ／ ボルグ `BorgF0` ／ ホタ `HotaL0` ／ ヒヨ `HiyoF0`（その後に手が入っていない駒は今のまま）。「被弾の燃焼なし」＝ `EmberRule.Pre256`（第256期の規則を抜く）。"
            + "敵の倍率 115 ／ 115 は第187期（第232期より前）なので両方に掛かっている。ボス ／ 精鋭 ／ 強い条件は第232期には無かった物差し。");
        Console.WriteLine();
        foreach (var b in cmp)
        {
            Console.WriteLine($"### {b.Axis}: {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | ボス 勝率・T | 近衛 | 大隊 | 本編2 | 本編4 | 本編5 | 九体 | 的・一 8T | 的・九 8T | 強 第2 ／ 3 ／ 4 ／ 5波 | 倒れた（ボス ／ 大隊 ／ 第5波） |");
            Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|--:|---|---|");
            foreach (var era in Eras)
            {
                var f = era.Map(b.F);
                var r = AxisWaves.ToDictionary(w => w.Name, w => Measure(f, w, ember: era.Ember));
                string WT(W w) => $"{F1(r[w.Name].Win)}・{r[w.Name].WinTs}T";
                Console.WriteLine($"| {era.Name} | {WT(Boss)} | {WT(Guard)} | {WT(Bat)} | {F1(r[M2.Name].Win)} | {F1(r[M4.Name].Win)} | {F1(r[M5.Name].Win)} | {WT(Nine)} | "
                    + $"{Per(r[T1.Name].Dmg8, r[T1.Name].N)} | {Per(r[T9.Name].Dmg8, r[T9.Name].N)} | {string.Join(" ／ ", StrongWaves.Select(w => F1(r[w.Name].Win)))} | "
                    + $"{Per2(r[Boss.Name].AllyFell, r[Boss.Name].N)} ／ {Per2(r[Bat.Name].AllyFell, r[Bat.Name].N)} ／ {Per2(r[M5.Name].AllyFell, r[M5.Name].N)} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
