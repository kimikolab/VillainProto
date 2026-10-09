using System.Reflection;
using System.Text;
using BattleCore;
using static Common;

// =====================================================================================
// burst check（第220期） —— 自己検査（受け入れ 2〜4）
//
// (4a) 盤面を直に組んで: 爆ぜる相手は同じ陣営の隣の生きている駒だけ／量（B1・B2）／燃焼の脆さが乗る／
//      1つの駒は1回しか爆ぜない・連鎖の段／爆発の後に印が移る（倒れた隣には移らない）／B1・B2 は味方側で爆ぜない・B2x は爆ぜる／
//      B2x の反転（ベニの結界の内側は回復）／B0 は爆ぜない
// (2)  印を持つ駒が1度も倒れない戦は B1〜B2x で台本ごと一致
// (3)  verbose の有無で勝敗・決着ターンが同じ
// (4b) 台本: 爆発の出来事の相手は爆ぜた駒と同じ陣営／B1・B2 で味方に無い／同じ駒が2度爆ぜない／直後に同じ相手への Damage（出どころ ＝ 爆ぜた駒）
// =====================================================================================

static partial class BurstDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static int ok, ng;
    static readonly Dictionary<string, long> NextKinds = new();
    static void Expect(string what, long got, long want)
    {
        bool pass = got == want;
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }

    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, false);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in p.Concat(e)) { u.MaxHp = 999; u.Hp = 999; }
        return ctx;
    }

    static partial void CheckImpl(string arg)
    {
        ok = ng = 0;
        Console.WriteLine("# 第220期 burst check");
        Console.WriteLine();
        Console.WriteLine("## (4a) 盤面を直に組んで");
        Console.WriteLine();
        var knight = EnemyCatalog.KnightG;   // 第306期: 第二波の騎士に斬り返しが付いたので、特性の無い巡礼騎士を名指しで引く（第305期までの `Stages[1]` の特性なしの1体目と同じ物）
        Formation E5 = Formation.Build(front1: knight, front3: knight, center: knight, back1: knight, back3: knight);

        foreach (var (ver, pois, marks, want) in new[] { ("B0", 8, 2, 0), ("B1", 8, 2, 4), ("B2", 8, 2, 6), ("B2x", 8, 2, 6), ("B2", 3, 1, 1), ("B1", 1, 5, 0) })
        {
            var mio = VerOf(ver);
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.TouT0, back3: mio), E5, out var p, out var e);
            UnitState dead = e[0];
            var adj = e.Where(u => u != dead && FormationRules.AreAdjacent(dead, u)).ToList();
            var far = e.Where(u => u != dead && !adj.Contains(u)).ToList();
            dead.SetCounter(StatusKeys.Poison, pois);
            for (int k = 0; k < marks; k++) ctx.MarkConcentrated(p[1], dead, "検査");
            UnitState burning = adj[0];
            burning.SetCounter(StatusKeys.Burn, 2);
            dead.Hp = 1;
            ctx.ApplyDamage(dead, 5, p[0]);
            int brittle = want == 0 ? 0 : (want * 25 + 99) / 100;
            string tag = ver + "（毒 " + pois + "・印 " + marks + "）";
            Expect(tag + " 隣（燃えていない）の減り", 999 - adj[1].Hp, want);
            Expect(tag + " 隣（燃えている・脆さ F1）の減り", 999 - burning.Hp, want + brittle);
            Expect(tag + " 隣でない敵の減り（計）", far.Sum(u => 999 - u.Hp), 0);
            Expect(tag + " 味方の減り（計）", p.Sum(u => 999 - u.Hp), 0);
            Expect(tag + " 爆ぜた数", ctx.BurstBook.Bursts[0], want > 0 ? 1 : 0);
            if (ver != "B0")
            {
                // 印は爆発の後に、生き残った隣のうち次の刻みが最大の1体へ（燃えている隣 ＝ 刻み 6）。
                Expect(tag + " 印が移った先（燃えている隣）の印", burning.RawCounter(StatusKeys.Concentrated), marks);
                Expect(tag + " 倒れた駒の印（移した後）", dead.RawCounter(StatusKeys.Concentrated), 0);
            }
        }
        Console.WriteLine();

        // 連鎖: 倒れた隣も印があれば爆ぜる・1回だけ・段・倒れた隣には外側の印が移らない
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.TouT0, back3: VerOf("B2")), E5, out var p, out var e);
            UnitState d0 = e[0];
            UnitState n1 = e.First(u => u != d0 && FormationRules.AreAdjacent(d0, u) && u.Slot == 2);   // 中央
            d0.SetCounter(StatusKeys.Poison, 8); ctx.MarkConcentrated(p[1], d0, "検査"); ctx.MarkConcentrated(p[1], d0, "検査");   // 8 × 3 ÷ 4 ＝ 6
            n1.SetCounter(StatusKeys.Poison, 4); ctx.MarkConcentrated(p[1], n1, "検査");                                          // 4 × 2 ÷ 4 ＝ 2
            n1.Hp = 3;
            d0.Hp = 1;
            var n1Nb = e.Where(u => u != n1 && u != d0 && FormationRules.AreAdjacent(n1, u)).ToList();
            var before = e.ToDictionary(u => u.InstanceId, u => u.Hp);
            ctx.ApplyDamage(d0, 5, p[0]);
            var b = ctx.BurstBook;
            Expect("連鎖: 爆ぜた数（2 体・d0 は1回だけ）", b.Bursts[0], 2);
            Expect("連鎖: 段 1 の爆発", b.StageBursts[0], 1);
            Expect("連鎖: 段 2 の爆発", b.StageBursts[1], 1);
            Expect("連鎖: 中央が倒れた", n1.IsAlive ? 0 : 1, 1);
            // 中央の隣（d0 を除く）は d0 の爆発（隣なら 6）と中央の爆発 2 を受ける
            long got = n1Nb.Sum(u => before[u.InstanceId] - u.Hp);
            long wantSum = n1Nb.Sum(u => 2 + (FormationRules.AreAdjacent(d0, u) ? 6 : 0));
            Expect("連鎖: 中央の隣の減り（計）", got, wantSum);
            Expect("連鎖: 倒れた中央に外側の印は移らない（中央の印 ＝ 0）", n1.RawCounter(StatusKeys.Concentrated), 0);
            Expect("連鎖: 連鎖の長さ 2 の連鎖", b.ChainLenHist[2], 1);
        }
        Console.WriteLine();

        // 味方側: B2 は爆ぜない・B2x は爆ぜる・ベニの結界の内側は回復
        foreach (var (ver, want) in new[] { ("B2", 0L), ("B2x", 1L) })
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.TouT0, center: UnitCatalog.Kubi, back1: UnitCatalog.Beni, back3: VerOf(ver)), E5, out var p, out var e);
            UnitState tou = p.First(u => u.Def.Id == "tou");
            tou.SetCounter(StatusKeys.Poison, 8); ctx.MarkConcentrated(p.First(u => u.Def.Id == "mio"), tou, "検査");   // 8 × 2 ÷ 4 ＝ 4
            tou.Hp = 1;
            var adj = p.Where(u => u != tou && FormationRules.AreAdjacent(tou, u)).ToList();
            foreach (var u in adj) u.Hp = 500;
            ctx.ApplyDamage(tou, 5, e[0]);
            Expect(ver + " 味方で爆ぜた数", ctx.BurstBook.Bursts[1], want);
            foreach (var u in adj)
            {
                bool inBarrier = u.Def.Id != "beni" && p.Any(x => x.Def.Id == "beni" && FormationRules.AreAdjacent(x, u)) || u.Def.Id == "beni";
                long delta = u.Hp - 500;
                long expect = want == 0 ? 0 : inBarrier ? 4 : -4;
                Expect(ver + " 隣の味方 " + u.Def.Name + (inBarrier ? "（結界の内側・回復）" : "（外側）"), delta, expect);
            }
        }
        Console.WriteLine();

        // ---------------------------------------------------------------- (2) (3) (4b)
        Console.WriteLine("## (2)(3)(4b) 戦闘を回して");
        Console.WriteLine();
        var forms = Seats().Select(s => s.F).Append(PonX()).Concat(R5Rows().Select(r => r.F)).ToArray();
        PropertyInfo[] props = typeof(BattleEvent).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        string Digest(BattleResult r)
        {
            ulong h = 1469598103934665603UL;
            foreach (BattleEvent ev in r.Events)
            {
                var sb = new StringBuilder();
                foreach (PropertyInfo pr in props)
                {
                    if (pr.Name == "Text" && ev.Kind == BattleEventKind.Highlight) continue;
                    object? v = pr.GetValue(ev);
                    sb.Append(pr.Name).Append('=').Append(v is System.Collections.IEnumerable en && v is not string ? string.Join(",", en.Cast<object>()) : v).Append('|');
                }
                foreach (byte bb in Encoding.UTF8.GetBytes(sb.ToString())) { h ^= bb; h *= 1099511628211UL; }
            }
            return h.ToString("x16") + "/" + r.PlayerWon + "/" + r.Turns;
        }
        var gate = new object();
        long noMarked = 0, noMarkedDiff = 0, n3 = 0, mism = 0, evTeam = 0, evAlly = 0, evTwice = 0, evNoDamage = 0, evTotal = 0;
        Parallel.For(0, forms.Length * 4 * 25, j =>
        {
            Formation f = forms[j / 100]; int st = 1 + (j / 25) % 4, s = j % 25;
            foreach (int sc in new[] { 115, 200 })
            {
                var boss = BossOf(sc);
                var r0 = BattleEngine.Run(WithMio(f, VerOf("B0")), EnemyCatalog.Stages[st].Enemy, s, verbose: true, boss: boss);
                long marked = r0.Burst!.DeathsMarked.Sum();
                string d0 = Digest(r0);
                int diff = 0;
                foreach (string v in Versions.Skip(1))
                {
                    var fv = WithMio(f, VerOf(v));
                    var pl = BattleEngine.Materialize(fv, BattleContext.PlayerTeam);
                    var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, Sc(sc));
                    var rv = BattleEngine.Run(pl, en, s, verbose: true);
                    var rq = BattleEngine.Run(fv, EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss);
                    if (marked == 0 && Digest(rv) != d0) diff++;
                    long mm = rv.PlayerWon != rq.PlayerWon || rv.Turns != rq.Turns ? 1 : 0;
                    // 台本
                    var team = pl.Select(u => (u.InstanceId, 0)).Concat(en.Select(u => (u.InstanceId, 1))).ToDictionary(x => x.InstanceId, x => x.Item2);
                    long tTeam = 0, tAlly = 0, tTwice = 0, tNoDmg = 0, tTot = 0;
                    var seenSrc = new HashSet<int>(); int? lastSrc = null;
                    var evs = rv.Events;
                    for (int k = 0; k < evs.Count; k++)
                    {
                        var ev = evs[k];
                        if (ev.Kind != BattleEventKind.MireBurst) continue;
                        tTot++;
                        int src = ev.SpreadFromId!.Value, tgt = ev.TargetId!.Value;
                        if (team.TryGetValue(src, out int ts) && team.TryGetValue(tgt, out int tt) && ts != tt) tTeam++;
                        if (v != "B2x" && team.TryGetValue(tgt, out int t2) && t2 == 0) tAlly++;
                        if (src != lastSrc) { if (!seenSrc.Add(src)) tTwice++; lastSrc = src; }
                        // 軛が切れば間に `Sealed` が1件入る（engine の並びのまま）。
                        int kn = k + 1;
                        while (kn < evs.Count && evs[kn].Kind == BattleEventKind.Sealed) kn++;
                        var nx = kn < evs.Count ? evs[kn] : null;
                        bool ok2 = ev.InverterId is not null
                            ? true
                            : nx is { Kind: BattleEventKind.Damage } && nx.TargetId == tgt && nx.ActorId == src;
                        if (!ok2)
                        {
                            tNoDmg++;
                            if (nx is not null) lock (gate) { NextKinds[nx.Kind.ToString()] = NextKinds.GetValueOrDefault(nx.Kind.ToString()) + 1; }
                        }
                    }
                    lock (gate) { n3++; mism += mm; evTeam += tTeam; evAlly += tAlly; evTwice += tTwice; evNoDamage += tNoDmg; evTotal += tTot; }
                }
                lock (gate) { if (marked == 0) { noMarked++; noMarkedDiff += diff; } }
            }
        });
        Console.WriteLine("(2) 印を持つ駒が1度も倒れなかった戦 " + noMarked + " 戦 × B1〜B2x:");
        Expect("台本が B0 と違った戦（延べ）", noMarkedDiff, 0);
        Console.WriteLine("(3) verbose の有無（" + n3 + " 戦）:");
        Expect("勝敗か決着ターンが違った戦", mism, 0);
        Console.WriteLine("(4b) 台本の爆発 " + evTotal + " 件:");
        Expect("相手が爆ぜた駒と違う陣営", evTeam, 0);
        Expect("B1・B2 で相手が味方", evAlly, 0);
        Expect("同じ駒が2度爆ぜた", evTwice, 0);
        Expect("直後に同じ相手への Damage（出どころ ＝ 爆ぜた駒）が無い", evNoDamage, 0);
        if (NextKinds.Count > 0) Console.WriteLine("  （直後に来た種類: " + string.Join(" ／ ", NextKinds.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value)) + "）");
        Console.WriteLine();
        Console.WriteLine("BURST_CHECK_COMPLETE ok=" + (ng == 0) + "（○ " + ok + " ／ × " + ng + "）");
    }
}
