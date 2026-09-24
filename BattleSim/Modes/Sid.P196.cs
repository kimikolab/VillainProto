using BattleCore;
using static Common;

// =====================================================================================
// sid モード（第196期） —— スィドの追補（吐く相手を散らす・前衛として立つ）の Phase 0
//
// 指示書は design/PHASE196_SID2_SPEC.md ／ 報告は design/PHASE196_SID2.md。
//
//     dotnet run --project BattleSim -c Release 0 sid phase196   # Q0-1〜Q0-4
//
// **Q0-1 と Q0-3 は第195期の帳簿と台本を読む**ので戦闘を回すが、盤面は1ビットも動かさない
// （実装の前に回す＝第195期のスィドのまま）。
// =====================================================================================

static partial class SidDiag
{
    static void Phase196()
    {
        Console.WriteLine("# 第196期 `sid phase196` —— Q0-1〜Q0-4（実装前・盤面は第195期のまま）");
        Console.WriteLine();
        var main = MainRows();

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 印の内訳と、手番で吐いた相手が既に印を持っていた割合（第195期・主表の新・第2〜5波 × seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("台本（verbose）を読む。`吐き` は `StatusGain`（毒・経路 `Spew`）の件数、`既に印` はそのうち相手に痺れ毒の印が付いた後だった件数。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 吐き/戦 | うち既に印 | 割合 | 印 吐き/戦 | 印 殴られ/戦 | 殴られの割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        long tSp = 0, tOn = 0, tMs = 0, tMv = 0;
        foreach (var (name, f) in main)
        {
            Formation g = GaldTo(f, SidNew);
            long sp = 0, on = 0, ms = 0, mv = 0; int n = 0;
            object gate = new();
            for (int st = 1; st < 5; st++)
            {
                Formation enemy = EnemyCatalog.Stages[st].Enemy;
                Parallel.For(0, Seeds, seed =>
                {
                    BattleResult r = BattleEngine.Run(g, enemy, seed, verbose: true);
                    int sid = SidInstance(g);
                    var marked = new HashSet<int>();
                    long a = 0, b = 0, c = 0, d = 0;
                    foreach (BattleEvent e in r.Events)
                    {
                        if (e.Kind != BattleEventKind.StatusGain || e.TargetId is not int t) continue;
                        if (e.Text == StatusKeys.Poison && e.PoisonRoute == PoisonRoute.Spew) { a++; if (marked.Contains(t)) b++; }
                        else if (e.Text == StatusKeys.Numbed)
                        {
                            marked.Add(t);
                            // 吐きの印は同じ手番で毒の直後に付く＝直前の StatusGain が同じ相手への Spew の毒
                            if (e.ActorId == sid) { if (LastSpewTarget(r.Events, e) == t) c++; else d++; }
                        }
                    }
                    lock (gate) { sp += a; on += b; ms += c; mv += d; n++; }
                });
            }
            tSp += sp; tOn += on; tMs += ms; tMv += mv;
            Console.WriteLine("| " + name + " | " + ((double)sp / n).ToString("F2") + " | " + ((double)on / n).ToString("F2") + " | "
                              + (sp == 0 ? "—" : (100.0 * on / sp).ToString("F0") + "%") + " | " + ((double)ms / n).ToString("F2") + " | "
                              + ((double)mv / n).ToString("F2") + " | " + (ms + mv == 0 ? "—" : (100.0 * mv / (ms + mv)).ToString("F0") + "%") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 7 行の合計: 吐いた相手が既に印を持っていた割合 **" + (tSp == 0 ? "—" : (100.0 * tOn / tSp).ToString("F1") + "%")
                          + "** ／ 印のうち殴られで付いた割合 **" + (tMs + tMv == 0 ? "—" : (100.0 * tMv / (tMs + tMv)).ToString("F1") + "%") + "**");
        Console.WriteLine();

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 第2〜5波の敵の数と攻撃力の並び（倍率 " + EnemyScaleRule.Adopted.AtkPercent + "% を掛けた盤面の値）");
        Console.WriteLine();
        Console.WriteLine("散らすと、吐きの k 回目は「印の無い敵のうち攻撃力 k 番目」に当たる。**殴られの印・敵の死・召喚を除いた机上の値**で、全員に印が付くのは吐き N 回目（N ＝ 敵の数）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵の数 | 攻撃力の並び（高い順・同値は席の小さい方・名前） | 全員に印（吐きだけ） |");
        Console.WriteLine("|---|--:|---|--:|");
        for (int st = 1; st < 5; st++)
        {
            var foes = EnemyCatalog.Stages[st].Enemy.Occupied()
                .Select(o => (o.Slot, D: EnemyScaleRule.Adopted.Apply(o.Def)))
                .OrderByDescending(x => x.D.Attack).ThenBy(x => x.Slot).ToList();
            Console.WriteLine("| " + EnemyCatalog.Stages[st].Name + " | " + foes.Count + " | "
                              + string.Join(" → ", foes.Select(x => x.D.Name + " " + x.D.Attack + "（" + SlotName(x.Slot) + "・" + PatternName(x.D.Pattern) + "）")) + " | "
                              + foes.Count + " 手番目 |");
        }
        Console.WriteLine();
        Console.WriteLine("- スィドは速9。**第1ターンの吐きは敵の多くより先**に来るので、1手番目の相手は第195期と同じ（攻撃力最大）。違いは2手番目から");
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 ガルドの席のスィドが倒れたターンと、倒した攻撃者（第195期・主表の新・第2〜5波 × seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("`倒した駒` は `Death` の `ActorId`。`何発目` はその駒がスィドに当てた何回目の `Damage` で倒したか。`攻撃者の数` は倒れるまでにスィドへ `Damage` を当てた敵の数、"
                          + "`最多の割合` はそのうち最も多く削った敵の取り分。`吐いた相手` は倒した駒がスィドの1回目の吐きの相手だったか。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 倒れた戦 | 1〜2T | 攻撃者の数 1〜2T ／ 3T〜 | 最多の割合 1〜2T ／ 3T〜 | 倒した駒が吐いた相手 1〜2T ／ 3T〜 | 倒した一撃は何発目（平均）1〜2T ／ 3T〜 | 倒した駒の上位（1〜2T） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (name, f) in main)
        {
            Formation g = GaldTo(f, SidNew);
            var early = new List<(int Attackers, double Top, bool Spewed, int Nth, string Killer)>();
            var late = new List<(int Attackers, double Top, bool Spewed, int Nth, string Killer)>();
            int n = 0;
            object gate = new();
            for (int st = 1; st < 5; st++)
            {
                Formation enemy = EnemyCatalog.Stages[st].Enemy;
                Parallel.For(0, Seeds, seed =>
                {
                    BattleResult r = BattleEngine.Run(g, enemy, seed, verbose: true);
                    int sid = SidInstance(g);
                    var names = NamesOf(g, enemy);
                    var byFoe = new Dictionary<int, int>();
                    var hits = new Dictionary<int, int>();
                    int firstSpew = -1;
                    (int, double, bool, int, string)? rec = null;
                    int turn = 0;
                    foreach (BattleEvent e in r.Events)
                    {
                        if (e.Kind == BattleEventKind.StatusGain && e.PoisonRoute == PoisonRoute.Spew && firstSpew < 0 && e.TargetId is int ft) firstSpew = ft;
                        if (e.Kind == BattleEventKind.Damage && e.TargetId == sid && e.ActorId is int a && a != sid && !e.FriendlyFire)
                        {
                            byFoe[a] = byFoe.GetValueOrDefault(a) + e.Amount;
                            hits[a] = hits.GetValueOrDefault(a) + 1;
                        }
                        if (e.Kind == BattleEventKind.Death && e.TargetId == sid && rec is null)
                        {
                            turn = e.Turn;
                            int k = e.ActorId ?? -1;
                            int tot = byFoe.Values.Sum();
                            rec = (byFoe.Count, tot == 0 ? 0 : (double)byFoe.Values.Max() / tot, k == firstSpew, hits.GetValueOrDefault(k),
                                   k >= 0 && names.TryGetValue(k, out string? nm) ? nm : "（出どころ無し）");
                        }
                    }
                    lock (gate)
                    {
                        n++;
                        if (rec is { } x) (turn <= 2 ? early : late).Add(x);
                    }
                });
            }
            string Avg<T>(List<T> l, Func<T, double> sel, string fmt) => l.Count == 0 ? "—" : l.Average(sel).ToString(fmt);
            string Pct<T>(List<T> l, Func<T, bool> sel) => l.Count == 0 ? "—" : (100.0 * l.Count(sel) / l.Count).ToString("F0") + "%";
            var top = early.GroupBy(x => x.Killer).OrderByDescending(gp => gp.Count()).Take(3)
                           .Select(gp => gp.Key + " " + (100.0 * gp.Count() / Math.Max(1, early.Count)).ToString("F0") + "%");
            int dead = early.Count + late.Count;
            Console.WriteLine("| " + name + " | " + (100.0 * dead / n).ToString("F1") + "% | " + (dead == 0 ? "—" : (100.0 * early.Count / dead).ToString("F0") + "%") + " | "
                              + Avg(early, x => x.Attackers, "F2") + " ／ " + Avg(late, x => x.Attackers, "F2") + " | "
                              + Avg(early, x => x.Top * 100, "F0") + "% ／ " + Avg(late, x => x.Top * 100, "F0") + "% | "
                              + Pct(early, x => x.Spewed) + " ／ " + Pct(late, x => x.Spewed) + " | "
                              + Avg(early, x => x.Nth, "F2") + " ／ " + Avg(late, x => x.Nth, "F2") + " | " + string.Join("・", top) + " |");
        }
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 版の切り替え");
        Console.WriteLine();
        Console.WriteLine("- `UnitDef` は静的で `Run` の引数は増やさない。**版は第195期と同じく診断のローカルに写した `UnitDef` で切り替える**（札の配列と `MaxHp` を差し替えた写し）");
        Console.WriteLine("- **散らし**: `SpewTrait.SpewSpreads`（`const`・既定 true）。偽の版を同じ実行の中で並べるため、**散らさない吐きを別の札 `SpewFixed`（保持者 0 枚・対照）**にする"
                          + "（第186期の `ThrustPlain` と同じ作法）。`SpewSpreads = false` と `SpewFixed` は同じ選び方を通る");
        Console.WriteLine("- **S+反**: 殴られたときの毒 +4 → +8 は `Venom` の `StackPerHit` を読み替える別の札 `VenomHeavy`（`const VenomTrait.HeavyStackPerHit = 8`・保持者 0 枚）。"
                          + "漏れ・印の付け方は `Venom` と同じ本体を通る");
        Console.WriteLine("- **S+HP**: `const UnitCatalog.SidHardyHp = 100` を写しの `MaxHp` に入れる（規定の版は 84 のまま）");
        Console.WriteLine("- 規定の版（`docs/` に載る版）は **S**（`Spew`（散らす）/ `Venom` / `Numb`・HP 84）");
        Console.WriteLine();
    }

    /// <summary>
    /// スィドの InstanceId。**InstanceId は `BattleContext.Add` の順（味方の編成 → 敵の編成 → 召喚）で振られる**ので、
    /// 味方は `Formation.Occupied()` の並びの 0.. 番（第192期 `beni phase192` と同じ引き方）。
    /// </summary>
    static int SidInstance(Formation f) => f.Occupied().Select((o, i) => (o, i)).First(x => x.o.Def.Id == "sid").i;

    static Dictionary<int, string> NamesOf(Formation f, Formation enemy)
    {
        var d = new Dictionary<int, string>();
        int i = 0;
        foreach (var o in f.Occupied()) d[i++] = o.Def.Name;
        foreach (var o in enemy.Occupied()) d[i++] = "敵・" + o.Def.Name;
        return d;
    }

    /// <summary>印の `StatusGain` の直前にある、同じ書き手の `Spew` の毒の相手（無ければ -1）。</summary>
    static int LastSpewTarget(IReadOnlyList<BattleEvent> ev, BattleEvent mark)
    {
        int at = -1;
        for (int i = 0; i < ev.Count; i++) if (ReferenceEquals(ev[i], mark)) { at = i; break; }
        for (int i = at - 1; i >= 0 && i >= at - 3; i--)
            if (ev[i].Kind == BattleEventKind.StatusGain && ev[i].PoisonRoute == PoisonRoute.Spew) return ev[i].TargetId ?? -1;
        return -1;
    }

    static string PatternName(AttackPattern p) => p switch
    {
        AttackPattern.Sweep => "薙ぎ", AttackPattern.Pierce => "貫き", AttackPattern.All => "全体", _ => "単体",
    };
}
