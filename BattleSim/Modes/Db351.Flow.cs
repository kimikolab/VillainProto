using BattleCore;
using static Common;
using I = Inv348Diag;

// db351 §2-2 ／ §5-1 —— 呪いの流れ（帳簿 `UnitTally.Db351*` と台本を読む・盤面は1ビットも動かさない）。
static partial class Db351Diag
{
    internal const int TrajT = 8;   // 敵の攻撃力の推移を読むターン（1〜8）

    internal sealed class Fl
    {
        public long N, Wins, WinT, Fell, Lost;
        public readonly Dictionary<string, long> WroteFoe = new(), WroteAlly = new(), Dulled = new(), SwapGot = new(), Deaths = new(), LostDeaths = new(), LostDeathT = new();
        public long Picked, PickedFar, Sent, Fed, Swapped, SwapOnPerverse, UkeBore, PlGain, PlHits, PlMiss;
        public long UtsuIn, UtsuMaxSum, UtsuMaxMax, UtsuFullN, UtsuFullT, UtsuDealt, UtsuN;
        public long HexSrcHits, HexSplits, HexDealt, HexKills, UHexSrcHits, UHexSplits, UHexDealt, UHexKills;
        // 敵の攻撃力の推移（ターンごと・生きている敵の和 ／ 数）: 元 ／ 今 ／ 弱体の累計 ／ 強化（今 − 元 ＋ 弱体の累計・0 未満は 0）
        public readonly long[] TBase = new long[TrajT + 1], TCur = new long[TrajT + 1], TDull = new long[TrajT + 1], TBuff = new long[TrajT + 1], TCnt = new long[TrajT + 1];
        public long ZeroBattles, BuffSum, BuffN;
        public void Add(Fl o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Lost += o.Lost;
            foreach (var (d, s) in new[] { (WroteFoe, o.WroteFoe), (WroteAlly, o.WroteAlly), (Dulled, o.Dulled), (SwapGot, o.SwapGot), (Deaths, o.Deaths), (LostDeaths, o.LostDeaths), (LostDeathT, o.LostDeathT) })
                foreach (var (k, v) in s) d[k] = d.GetValueOrDefault(k) + v;
            Picked += o.Picked; PickedFar += o.PickedFar; Sent += o.Sent; Fed += o.Fed; Swapped += o.Swapped; SwapOnPerverse += o.SwapOnPerverse; UkeBore += o.UkeBore;
            PlGain += o.PlGain; PlHits += o.PlHits; PlMiss += o.PlMiss;
            UtsuIn += o.UtsuIn; UtsuMaxSum += o.UtsuMaxSum; UtsuMaxMax = Math.Max(UtsuMaxMax, o.UtsuMaxMax); UtsuFullN += o.UtsuFullN; UtsuFullT += o.UtsuFullT; UtsuDealt += o.UtsuDealt; UtsuN += o.UtsuN;
            HexSrcHits += o.HexSrcHits; HexSplits += o.HexSplits; HexDealt += o.HexDealt; HexKills += o.HexKills;
            UHexSrcHits += o.UHexSrcHits; UHexSplits += o.UHexSplits; UHexDealt += o.UHexDealt; UHexKills += o.UHexKills;
            for (int t = 0; t <= TrajT; t++) { TBase[t] += o.TBase[t]; TCur[t] += o.TCur[t]; TDull[t] += o.TDull[t]; TBuff[t] += o.TBuff[t]; TCnt[t] += o.TCnt[t]; }
            ZeroBattles += o.ZeroBattles; BuffSum += o.BuffSum; BuffN += o.BuffN;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }

    /// <summary>1戦（verbose・台本で敵の攻撃力の推移を読む）。</summary>
    internal static Fl FlowOne(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Fl { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else a.Lost = 1;
        var names = new Dictionary<string, string>();
        foreach (var u in p)
        {
            string n = Short(u.Def); names[u.Def.Id] = n;
            if (u.LastDeathTurn > 0)
            {
                a.Fell++; a.Deaths[n] = a.Deaths.GetValueOrDefault(n) + 1;
                if (!r.PlayerWon) { a.LostDeaths[n] = a.LostDeaths.GetValueOrDefault(n) + 1; a.LostDeathT[n] = a.LostDeathT.GetValueOrDefault(n) + u.LastDeathTurn; }
            }
            if (!r.TallyByUnit.TryGetValue(u.Def.Id, out var t)) continue;
            a.WroteFoe[n] = a.WroteFoe.GetValueOrDefault(n) + t.Db351DullWroteFoe;
            a.WroteAlly[n] = a.WroteAlly.GetValueOrDefault(n) + t.Db351DullWroteAlly;
            a.Dulled[n] = a.Dulled.GetValueOrDefault(n) + t.Dulled;
            a.SwapGot[n] = a.SwapGot.GetValueOrDefault(n) + t.Db351SwapGot;
            a.HexSrcHits += t.Db351HexSrcHits; a.HexSplits += t.Db351HexSplits; a.HexDealt += t.Db351HexDealt; a.HexKills += t.Db351HexKills;
            if (u.Def.Id == "wata") { a.Picked += t.Db351Picked; a.PickedFar += t.Db351PickedFar; a.Sent += t.Db351Sent; a.Fed += t.Db351Fed; a.Swapped += t.Db351Swapped; a.SwapOnPerverse += t.Db351SwapOnPerverse; }
            if (u.Def.Id == "uke") { if (u.HasTrait(TraitId.Bear)) a.UkeBore += t.Dulled; a.PlGain += t.Db351PlunderGain; a.PlHits += t.Db351PlunderHits; a.PlMiss += t.Db351PlunderMiss; }
            if (u.Def.Id == "utsu")
            {
                a.UtsuN = 1; a.UtsuIn += t.Dulled; a.UtsuMaxSum += t.Db351PerverseMax; a.UtsuMaxMax = t.Db351PerverseMax; a.UtsuFullN += t.Db351PerverseFullN; a.UtsuFullT += t.Db351PerverseFullT; a.UtsuDealt += t.DamageToEnemy;
                a.UHexSrcHits += t.Db351HexSrcHits; a.UHexSplits += t.Db351HexSplits; a.UHexDealt += t.Db351HexDealt; a.UHexKills += t.Db351HexKills;
            }
        }
        // 敵の攻撃力の推移: 台本の順に dull の StatusGain を足し、StatSnapshot（ターン頭）でその時点の値を写す
        var foeBase = new Dictionary<int, int>();
        foreach (var u in e) foeBase[u.InstanceId] = u.Def.Attack;
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team is int tm && tm != BattleContext.PlayerTeam)
            foeBase.TryAdd(sid, -1);   // 召喚された敵は元の攻撃力を引かない（推移に入れない）
        int? boss = w.Boss ? e.OrderByDescending(u => u.Def.Attack).First().InstanceId : null;
        var cum = new Dictionary<int, long>();
        bool zero = false;
        foreach (var x in r.Events)
        {
            if (x.Kind == BattleEventKind.StatusGain && x.Text == BattleContext.DullKey && x.TargetId is int dt && foeBase.ContainsKey(dt)) cum[dt] = cum.GetValueOrDefault(dt) + x.Amount;
            if (x.Kind != BattleEventKind.StatSnapshot || x.TargetId is not int id || !foeBase.TryGetValue(id, out int b) || b < 0) continue;
            if (boss is int bi && id != bi) continue;
            if (x.Amount <= 0) zero = true;
            if (x.Turn < 1 || x.Turn > TrajT) continue;
            long d = cum.GetValueOrDefault(id), buff = Math.Max(0, x.Amount - b + d);
            a.TBase[x.Turn] += b; a.TCur[x.Turn] += x.Amount; a.TDull[x.Turn] += d; a.TBuff[x.Turn] += buff; a.TCnt[x.Turn]++;
            a.BuffSum += buff; a.BuffN++;
        }
        if (zero) a.ZeroBattles = 1;
        return a;
    }

    internal static Fl FlowMeasure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fl[n];
        Parallel.For(0, n, i => parts[i] = FlowOne(f, w, i));
        var all = new Fl();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static string Kv(Dictionary<string, long> d, long n, Func<string, bool>? keep = null) =>
        string.Join("、", d.Where(kv => kv.Value > 0 && (keep is null || keep(kv.Key))).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, n)}"));

    internal static void PrintFlow(string title, IEnumerable<(string Name, Fl F)> rows)
    {
        var list = rows.ToList();
        Console.WriteLine($"#### {title} —— 1. 弱体の量（／戦）");
        Console.WriteLine();
        Console.WriteLine("書いた ＝ `Dull` の入口で書き手に付けた量（横取りされる前）。ワタが渡した分はワタの「敵へ」に入る。受けた ＝ 実際に攻撃力が下がった駒（横取りの後）。");
        Console.WriteLine();
        Console.WriteLine("| 列 | 勝率・T | 書いた 敵へ | 書いた 味方へ | 受けた（味方） | ワタ 拾った（うち隣でない）／ 敵へ ／ ウツへ | 交換で取った（受け取った駒）| ウケ 背負った | 奪う 上がった ／ 弱った敵 ／ 弱っていない敵（回） |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|---|");
        foreach (var (n, a) in list)
            Console.WriteLine($"| {n} | {F1(a.Win)}・{a.WinTs}T | {Kv(a.WroteFoe, a.N)} | {Kv(a.WroteAlly, a.N)} | {Kv(a.Dulled, a.N)} | {Per(a.Picked, a.N)}（{Per(a.PickedFar, a.N)}）／ {Per(a.Sent, a.N)} ／ {Per(a.Fed, a.N)} | "
                + $"{Per(a.Swapped, a.N)}（{Kv(a.SwapGot, a.N)}・ウツに付いた {a.SwapOnPerverse} 回）| {Per(a.UkeBore, a.N)} | {Per(a.PlGain, a.N)} ／ {Per2(a.PlHits, a.N)} ／ {Per2(a.PlMiss, a.N)} |");
        Console.WriteLine();
        Console.WriteLine($"#### {title} —— 2. ウツと ①（呪いの分け合い）");
        Console.WriteLine();
        Console.WriteLine("下げ幅 ＝ ウツの攻撃力低下（`-AtkBonus`）の戦ごとの最大。5発 ＝ 下げ幅 12 に届いた戦の割合 ／ 届いた T の平均。分け合い ＝ 呪われた敵への単体の一撃が、ほかの呪われた敵へ半分ずつ分かれたもの（出どころの駒の側）。");
        Console.WriteLine();
        Console.WriteLine("| 列 | ウツ 受けた | 下げ幅 平均 ／ 最大 | 5発（割合 ／ T） | ウツ 与ダメ | 分け合い 全員: 起こした一撃 ／ 分かれた先 ／ 量 ／ 倒した | うちウツ: 起こした一撃 ／ 分かれた先（1発あたり）／ 量 ／ 倒した |");
        Console.WriteLine("|---|--:|---|---|--:|---|---|");
        foreach (var (n, a) in list)
            Console.WriteLine($"| {n} | {Per(a.UtsuIn, a.UtsuN)} | {Per(a.UtsuMaxSum, a.UtsuN)} ／ {a.UtsuMaxMax} | {Pct(a.UtsuFullN, a.UtsuN)}% ／ {Per(a.UtsuFullT, a.UtsuFullN)} | {Per(a.UtsuDealt, a.UtsuN)} | "
                + $"{Per2(a.HexSrcHits, a.N)} ／ {Per2(a.HexSplits, a.N)} ／ {Per(a.HexDealt, a.N)} ／ {Per2(a.HexKills, a.N)} | {Per2(a.UHexSrcHits, a.N)} ／ {Per2(a.UHexSplits, a.N)}（{Per2(a.UHexSplits, a.UHexSrcHits)}）／ {Per(a.UHexDealt, a.N)} ／ {Per2(a.UHexKills, a.N)} |");
        Console.WriteLine();
        Console.WriteLine($"#### {title} —— 3. 敵の攻撃力の推移（ターン頭・生きている敵の平均・ボスはボスだけ）: 今（元 ／ 強化 ／ 弱体の累計）");
        Console.WriteLine();
        Console.WriteLine("強化 ＝ 今 − 元 ＋ 弱体の累計（0 未満は 0・攻撃力が 0 で底を打った敵では小さく出る）。0 以下 ＝ 敵（ボスはボス）の攻撃力が一度でも 0 になった戦の割合。");
        Console.WriteLine();
        Console.WriteLine("| 列 | " + string.Join(" | ", Enumerable.Range(1, TrajT).Select(t => $"T{t}")) + " | 0 以下 | 強化 平均 |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, TrajT).Select(_ => "---|")) + "--:|--:|");
        foreach (var (n, a) in list)
            Console.WriteLine($"| {n} | " + string.Join(" | ", Enumerable.Range(1, TrajT).Select(t => a.TCnt[t] == 0 ? "—"
                : $"{Per(a.TCur[t], a.TCnt[t])}（{Per(a.TBase[t], a.TCnt[t])} ／ +{Per(a.TBuff[t], a.TCnt[t])} ／ −{Per(a.TDull[t], a.TCnt[t])}）")) + $" | {Pct(a.ZeroBattles, a.N)}% | {Per(a.BuffSum, a.BuffN)} |");
        Console.WriteLine();
        Console.WriteLine($"#### {title} —— 4. 倒れた駒（全戦 ／戦・負けた戦: 倒れた割合 ／ 倒れた T）");
        Console.WriteLine();
        Console.WriteLine("| 列 | 倒れた ／戦 | 駒ごと（全戦・割合%） | 負けた戦（割合% ／ 平均T） |");
        Console.WriteLine("|---|--:|---|---|");
        foreach (var (n, a) in list)
            Console.WriteLine($"| {n} | {Per2(a.Fell, a.N)} | {string.Join("、", a.Deaths.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Pct(kv.Value, a.N)}"))} | "
                + $"{(a.Lost == 0 ? "—" : string.Join("、", a.LostDeaths.OrderBy(kv => (double)a.LostDeathT[kv.Key] / kv.Value).Select(kv => $"{kv.Key} {Pct(kv.Value, a.Lost)} ／ {Per(a.LostDeathT[kv.Key], kv.Value)}T")))} |");
        Console.WriteLine();
    }

    static readonly I.W[] FlowWavesP0 = { I.Boss, I.Bat, I.M2, I.M5 };

    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards().Take(2).ToArray();
        Console.WriteLine($"# {Tag} §2-2 Phase 0 —— 呪いの流れ（代表台 {boards.Length} × {string.Join(" ／ ", FlowWavesP0.Select(w => w.Name))}・規定・seed 0..{Seeds - 1}・1戦あたり）");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        foreach (var b in boards)
            PrintFlow(b.Name, FlowWavesP0.Select(w => (w.Name, FlowMeasure(b.F, w))));
        // 強化の材料（§2-2 の 4）: 本編 ／ 精鋭の敵の強化の量を、呪いの台 1 で全波
        Console.WriteLine("#### 敵の強化の量（② の材料の見込み）—— 呪いの台 1 × 全波（強化 平均 ＝ ターン頭の敵1体あたり・0 以下 ＝ 攻撃力が 0 になった戦の割合）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率・T | 強化 平均 | T1 ／ T3 ／ T5 の強化 | 0 以下 |");
        Console.WriteLine("|---|---|--:|---|--:|");
        foreach (var w in Waves)
        {
            var a = FlowMeasure(boards[0].F, w);
            Console.WriteLine($"| {w.Name} | {F1(a.Win)}・{a.WinTs}T | {Per(a.BuffSum, a.BuffN)} | {string.Join(" ／ ", new[] { 1, 3, 5 }.Select(t => Per(a.TBuff[t], a.TCnt[t])))} | {Pct(a.ZeroBattles, a.N)}% |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void FlowAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards().Take(3).ToArray();
        Console.WriteLine($"# {Tag} §5-1 呪いの流れ × 版（呪いの台 1 ／ 2 ／ ウケ入り × ボス ／ 大隊・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        foreach (var b in boards)
            foreach (var w in new[] { I.Boss, I.Bat })
                PrintFlow($"{b.Name} × {w.Name}", vers.Select(v => (v.Ascii, FlowMeasure(Apply(b.F, v), w))));
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
