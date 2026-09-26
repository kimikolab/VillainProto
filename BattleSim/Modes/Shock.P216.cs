using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// =====================================================================================
// shock phase216 / run216 / check216（第216期）—— ベニの開戦の撒き・感電で痺れる・雷の編成の比較。
// 指示書は design/PHASE216_OPENING_STUN_SPEC.md ／ 報告は design/PHASE216_OPENING_STUN.md。
// **線は置かない**（採否はポンが遊んで決める）。台は `Presets` に足さない（この診断のローカル）。
//
// このファイルは Phase 0（実装前・盤面は第215期のまま）と、表の集計に共有する器具だけを持つ。
// =====================================================================================

static partial class ShockDiag
{
    // =================================================================================
    // 台（指示書 §5.1）
    // =================================================================================

    /// <summary>台A（ポンの X字）: 前1 クビ ／ 前3 グザ ／ 中央 ベニ ／ 後1 ミオ ／ 後3 カタ。</summary>
    internal static Formation TableA216(UnitDef beni, UnitDef kata) => Formation.Build(
        front1: UnitCatalog.Kubi, front3: UnitCatalog.Guza, center: beni, back1: UnitCatalog.Mio, back3: kata);

    /// <summary>台B（ポンのパターン2）: 中衛・上 カタ ／ 後衛 グザ ／ 中衛・中央 ベニ ／ 前衛 スィド ／ 中衛・下 ミオ。</summary>
    internal static Formation TableB216(UnitDef beni, UnitDef kata) => Formation.BuildDiamond(
        a: kata, b: UnitCatalog.Guza, c: beni, d: UnitCatalog.Sid, e: UnitCatalog.Mio);

    /// <summary>台C（3体で完結の試し・グザ無し）の顔ぶれ。席は総当たりで選ぶ。</summary>
    internal static List<UnitDef> TableCMembers => new() { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, UnitCatalog.Kubi, UnitCatalog.Tou };

    /// <summary>その陣形で「前衛」にあたる枠（X字 ＝ 前1・前3 ／ パターン2 ＝ 前衛）。</summary>
    static int[] FrontFrames(FormationShape s) => s == FormationShape.X ? new[] { 0, 1 } : new[] { 3 };

    internal static readonly EnemyScaleRule Scale115 = EnemyScaleRule.Adopted, Scale150 = new(150, 150);
    internal static readonly (string Tag, EnemyScaleRule Rule)[] Scales216 = { ("115", Scale115), ("150", Scale150) };

    static BossRule BossOf(EnemyScaleRule r) => new(false) { Scale = r };

    // =================================================================================
    // 集計（第216期）
    // =================================================================================

    internal sealed class Agg216
    {
        public readonly long[] Wins = new long[5], AllSurv = new long[5], N = new long[5];
        public long WinTurns, WinN, FrontT1, FrontFell;
        public readonly UnitTally P = new(), E = new();
        /// <summary>味方の駒ごと: 倒れた戦の数・倒れたターンの合計（最初の1回）。</summary>
        public readonly Dictionary<string, (long Fell, long TurnSum)> Fallen = new();

        public void Take(BattleResult r, int st, HashSet<string> playerIds, HashSet<string> frontIds)
        {
            N[st]++;
            if (r.PlayerWon)
            {
                Wins[st]++; WinTurns += r.Turns; WinN++;
                if (r.PlayerStarterFallen.Count == 0) AllSurv[st]++;
            }
            bool t1 = false, fell = false;
            foreach (var (id, t) in r.TallyByUnit)
            {
                bool pl = playerIds.Contains(id);
                (pl ? P : E).Add(t);
                if (!pl || t.DeathTurnHist is null) continue;
                int first = Array.FindIndex(t.DeathTurnHist, x => x > 0);
                if (first < 0) continue;
                var (f0, s0) = Fallen.GetValueOrDefault(id);
                Fallen[id] = (f0 + 1, s0 + first);
                if (frontIds.Contains(id)) { fell = true; if (first <= 1) t1 = true; }
            }
            if (t1) FrontT1++;
            if (fell) FrontFell++;
        }

        public void Merge(Agg216 o)
        {
            for (int i = 0; i < 5; i++) { Wins[i] += o.Wins[i]; AllSurv[i] += o.AllSurv[i]; N[i] += o.N[i]; }
            WinTurns += o.WinTurns; WinN += o.WinN; FrontT1 += o.FrontT1; FrontFell += o.FrontFell;
            P.Add(o.P); E.Add(o.E);
            foreach (var (k, v) in o.Fallen)
            {
                var (a, b) = Fallen.GetValueOrDefault(k);
                Fallen[k] = (a + v.Fell, b + v.TurnSum);
            }
        }

        public long Battles => N.Sum();
        public double WinPct(int st) => N[st] == 0 ? double.NaN : 100.0 * Wins[st] / N[st];
        public double Mean25 => Enumerable.Range(1, 4).Average(WinPct);
        public double AllSurvPct => 100.0 * AllSurv.Skip(1).Sum() / Math.Max(1, N.Skip(1).Sum());
        public double AllSurvAt(int st) => N[st] == 0 ? double.NaN : 100.0 * AllSurv[st] / N[st];
        public double MeanWinT => WinN == 0 ? double.NaN : (double)WinTurns / WinN;
        public double Per(long x) => (double)x / Math.Max(1, Battles);
    }

    internal static Agg216 Measure216(Formation f, EnemyScaleRule scale, IReadOnlyList<int>? waves = null, int seed0 = 0, int seeds = MeasSeeds)
    {
        waves ??= Waves25;
        var playerIds = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        var frontIds = FrontFrames(f.Shape).Select(i => f[i]?.Id).Where(x => x is not null).Select(x => x!).ToHashSet();
        var boss = BossOf(scale);
        var total = new Agg216();
        var gate = new object();
        Parallel.For(0, waves.Count * seeds, () => new Agg216(), (j, _, local) =>
        {
            int st = waves[j / seeds], s = seed0 + j % seeds;
            local.Take(BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss), st, playerIds, frontIds);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    /// <summary>席の総当たり（第2〜5波 × seed 1000..1049・勝ち数最大・同値は列挙順で最初）。</summary>
    internal static Formation PickSeats216(List<UnitDef> members, FormationShape shape, EnemyScaleRule scale)
    {
        var all = new List<Formation>();
        foreach (int[] assign in SlotAssignments(members.Count))
        {
            var f = new Formation { Shape = shape };
            for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
            all.Add(f);
        }
        var boss = BossOf(scale);
        var score = new int[all.Count];
        Parallel.For(0, all.Count * 4, j =>
        {
            int i = j / 4, st = 1 + j % 4;
            Formation en = EnemyCatalog.Stages[st].Enemy;
            int w = 0;
            for (int s = PickSeed0; s < PickSeed0 + PickSeeds; s++)
                if (BattleEngine.Run(all[i], en, s, verbose: false, boss: boss).PlayerWon) w++;
            Interlocked.Add(ref score[i], w);
        });
        int best = 0;
        for (int i = 1; i < all.Count; i++) if (score[i] > score[best]) best = i;
        return all[best];
    }

    static string DeathLine(Agg216 a, Formation f)
        => string.Join(" ／ ", f.Occupied().Select(o =>
        {
            var (fell, ts) = a.Fallen.GetValueOrDefault(o.Def.Id);
            return o.Def.Name.Split('の').Last() + " " + (100.0 * fell / Math.Max(1, a.Battles)).ToString("F0") + "%" + (fell > 0 ? "@" + ((double)ts / fell).ToString("F1") : "");
        }));

    // =================================================================================
    // Phase 0（実装前・盤面は第215期のまま）
    // =================================================================================

    static void Phase216()
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第216期 `shock phase216` —— Q0-1〜Q0-5（実装前・盤面は第215期のまま）");
        Console.WriteLine();
        string root = FindRoot();
        string engine = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
        string traits = File.ReadAllText(Path.Combine(root, "BattleCore", "Traits.cs"));
        var bodies = ClassBodies(traits);

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1 毒1の寿命・燃焼1");
        Console.WriteLine();
        Console.WriteLine("- `TickStatuses` の毒のループは層を**読むだけ**（`SetCounter(StatusKeys.Poison` を書くのは下の箇所だけ）:");
        foreach (var (file, src) in new[] { ("BattleEngine.cs", engine), ("Traits.cs", traits) })
            foreach (Match m in Regex.Matches(src, @"(?m)^.*SetCounter\(StatusKeys\.Poison.*$"))
            {
                string owner = file == "Traits.cs" ? bodies.Where(kv => src.IndexOf(kv.Value, StringComparison.Ordinal) <= m.Index)
                                                           .OrderByDescending(kv => src.IndexOf(kv.Value, StringComparison.Ordinal)).Select(kv => kv.Key).FirstOrDefault() ?? "?" : "engine";
                Console.WriteLine("  - " + file + " `" + owner + "`: `" + m.Value.Trim() + "`");
            }
        Console.WriteLine("- 燃焼は量を持たない（`Ignite` は残りターンを `BurnRules.Turns` = " + BurnRules.Turns + " に**設定**・1回の刻み " + BurnRules.Damage
                          + "）。「燃焼 1」は着火1回（3 ターン・刻み 3 回）として実装する");
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2 開戦時の順番（台に出る駒の `OnBattleStart`）");
        Console.WriteLine();
        Console.WriteLine("`BattleEngine.Run` は開戦の通知を**速さの降順**で回し、同速の群だけを `ctx.Shuffle` で混ぜる（行動順と同じ）。");
        Console.WriteLine();
        var pool = new List<UnitDef> { UnitCatalog.Beni, UnitCatalog.Mio, UnitCatalog.Kata, UnitCatalog.Guza, UnitCatalog.Kubi, UnitCatalog.Sid,
                                       UnitCatalog.Tou, UnitCatalog.Nel, UnitCatalog.Kugu };
        Console.WriteLine("| 駒 | 速 | `OnBattleStart` を上書きする札 |");
        Console.WriteLine("|---|--:|---|");
        foreach (UnitDef d in pool.OrderByDescending(d => d.Speed))
        {
            var ob = d.Traits.Where(t =>
            {
                var tr = TraitCatalog.Resolve(new[] { t }).FirstOrDefault();
                return tr?.GetType().GetMethod("OnBattleStart")?.DeclaringType == tr?.GetType();
            }).ToList();
            Console.WriteLine("| " + d.Name + " | " + d.Speed + " | " + (ob.Count == 0 ? "—" : string.Join(" / ", ob)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 同速6 はベニ・カタ（ともに開戦時は撒きの札だけ）。敵の開戦時の札は第2〜5波の駒ごとに下の表:");
        var enemyOb = new SortedDictionary<string, SortedSet<int>>();
        for (int st = 1; st < 5; st++)
            foreach (var o in EnemyCatalog.Stages[st].Enemy.Occupied())
                foreach (TraitId t in o.Def.Traits)
                {
                    var tr = TraitCatalog.Resolve(new[] { t }).FirstOrDefault();
                    if (tr?.GetType().GetMethod("OnBattleStart")?.DeclaringType == tr?.GetType())
                    {
                        string k = o.Def.Name + "（速" + o.Def.Speed + "）: " + t;
                        (enemyOb.TryGetValue(k, out var set) ? set : enemyOb[k] = new()).Add(st + 1);
                    }
                }
        foreach (var (k, v) in enemyOb) Console.WriteLine("  - " + k + " ／ 第" + string.Join("・", v) + "波");
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3 痺れの付き方");
        Console.WriteLine();
        Console.WriteLine("- 痺れを**書く**札（`SetCounter(StatusKeys.Stun, 1)`・二値で重ならない）: "
                          + string.Join(" ／ ", bodies.Where(kv => kv.Value.Contains("SetCounter(StatusKeys.Stun, 1)")).Select(kv => kv.Key + "（" + (IdOf(kv.Value)?.ToString() ?? "?") + "）")));
        Console.WriteLine("- 痺れを**読む**札（`StatusKeys.Stun` を条件に読む）: "
                          + string.Join(" ／ ", bodies.Where(kv => Regex.IsMatch(kv.Value, @"Counter\(StatusKeys\.Stun\)")).Select(kv => kv.Key)));
        Console.WriteLine("- engine: `TakeTurnCore` の頭で `Stun > 0` なら手番を失い（`IdleTurn` を立てて 0 に戻す）、"
                          + (Regex.IsMatch(engine, @"RawCounter\(StatusKeys\.Stun\) == 0") ? "`CanActOutOfTurn` も痺れている間は閉じる" : "（`CanActOutOfTurn` の痺れの条件が見つからない）"));
        Console.WriteLine("- 手番を既に終えた駒に付いた痺れは**次のターンの手番**で消費される（消えるのは消費したときだけ・ターンの頭で消えない）");
        Console.WriteLine("- 痺れを弾く・無効にする札: "
                          + string.Join(" ／ ", bodies.Where(kv => Regex.IsMatch(kv.Value, @"SetCounter\(StatusKeys\.Stun, 0\)")).Select(kv => kv.Key)) + "（engine 以外で 0 に戻す札）");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5 台の現状（O0・S0 ＝ 第215期の盤面・第2〜5波 × seed 0..199）");
        Console.WriteLine();
        var cSeats = new Dictionary<FormationShape, Formation>();
        foreach (FormationShape sh in Shapes) cSeats[sh] = PickSeats216(TableCMembers, sh, Scale115);
        var tabs = new List<(string Tag, Formation F)>
        {
            ("台A", TableA216(UnitCatalog.Beni, UnitCatalog.Kata)),
            ("台B", TableB216(UnitCatalog.Beni, UnitCatalog.Kata)),
            ("台C（参考・O0 で選んだ席）", cSeats[FormationShape.X]),
            ("台C（参考・O0 で選んだ席）", cSeats[FormationShape.Diamond]),
        };
        foreach (var (tag, f) in tabs) Console.WriteLine("- " + tag + " × " + ShapeName(f.Shape) + ": " + SeatsNamed(f));
        Console.WriteLine();
        Console.WriteLine("| 台 | 陣形 | 倍率 | 第2波 | 第3波 | 第4波 | 第5波 | **平均** | 全員生存 | 第2波の全員生存 | 決着T | 前衛が倒れた戦 | うち1ターン目まで | 1T目の雷/戦 | 1T目の当たり/雷 | 1T目の1発の種類 | 敵の連鎖/戦 | 味方の連鎖/戦・起爆 | 倒れた割合@倒れたT |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (tag, f) in tabs)
            foreach (var (st, rule) in Scales216)
            {
                Agg216 a = Measure216(f, rule);
                UnitTally k = a.P;
                Console.WriteLine("| " + tag + " | " + ShapeName(f.Shape) + " | " + st + " | " + string.Join(" | ", Waves25.Select(w => F1(a.WinPct(w)))) + " | **"
                                  + F1(a.Mean25) + "** | " + F1(a.AllSurvPct) + " | " + F1(a.AllSurvAt(1)) + " | " + F2(a.MeanWinT) + " | "
                                  + F1(100.0 * a.FrontFell / a.Battles) + "% | " + F1(100.0 * a.FrontT1 / a.Battles) + "% | "
                                  + F2(a.Per(k.ThunderCastsT1)) + " | " + F2((double)k.ThunderHitsT1 / Math.Max(1, k.ThunderCastsT1)) + " | "
                                  + F2((double)k.ThunderKindsT1 / Math.Max(1, k.ThunderHitsT1)) + " | " + F2(a.Per(a.E.ChainRoots)) + " | "
                                  + F2(a.Per(a.P.ChainRoots)) + "・" + F2(a.Per(a.P.ShockSpent)) + " | " + DeathLine(a, f) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("### 連鎖を起こした一撃の主（倍率 115・第2〜5波 × seed 0..199・敵陣と味方陣の連鎖を合わせた延べ・1戦あたり）");
        Console.WriteLine();
        foreach (var (tag, f) in tabs)
        {
            Agg a = Measure(f, Waves25);
            long tick = a.P.ShockTriggeredTick + a.E.ShockTriggeredTick, other = a.P.ShockTriggeredOther + a.E.ShockTriggeredOther;
            Console.WriteLine("- " + tag + " × " + ShapeName(f.Shape) + ": " + string.Join(" ／ ", a.TriggeredBy.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => NameOf(kv.Key) + " " + F2(Per(kv.Value, a)))) + " ／ 刻み " + F2(Per(tick, a)) + " ／ 出どころなし " + F2(Per(other, a)));
        }
        Console.WriteLine();
        Console.WriteLine("所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }
}
