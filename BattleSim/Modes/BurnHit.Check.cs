using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FC = FireCycleDiag;

// burnhit check —— 自己検査（受け入れ 1〜4）。盤面を直に組んで1発ずつ（`BattleContext` ＋ 反射で `Add`）と、台で回す戦の不変量。
static partial class BurnHitDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
        ?? throw new InvalidOperationException("BattleContext.Add が見つからない");

    static int _ok, _ng;
    static void Ok(string what, bool cond, string detail = "")
    {
        if (cond) _ok++; else _ng++;
        Console.WriteLine($"- {(cond ? "○" : "**×**")} {what}{(detail.Length > 0 ? $"（{detail}）" : "")}");
    }

    static UnitDef Plain(string id, string name, int hp = 1000) => new()
    {
        Id = id, Name = name, MaxHp = hp, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single,
        PlusText = "", MinusText = "", Flavor = "",
    };
    static UnitDef WithCard(UnitDef d, TraitId? c) => c is null ? d : FC.With(d, d.Traits.Concat(new[] { c.Value }));

    /// <summary>盤面を直に組む。味方: 枠0 札の持ち主（素の駒）・枠1 的（燃やす）・枠2 中央（任意）。敵: 枠0 殴り手。</summary>
    static (BattleContext Ctx, UnitState Holder, UnitState Target, UnitState Foe, UnitState? Center) Bench(TraitId? card, UnitDef? targetDef = null, UnitDef? center = null, bool foeSideTarget = false)
    {
        var ctx = new BattleContext(0, true);
        var tdef = targetDef ?? Plain("tgt", "的A");
        var pl = Formation.Build(front1: WithCard(Plain("hold", "持ち主"), card), front3: foeSideTarget ? null : tdef, center: center);
        var en = Formation.Build(front1: Plain("foe", "殴り手"), front3: foeSideTarget ? tdef : null);
        var p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        var target = (foeSideTarget ? e : p).First(u => u.Def.Id == tdef.Id);
        var foe = e.First(u => u.Def.Id == "foe");
        var holder = p.First(u => u.Def.Id == "hold");
        var c = center is null ? null : p.FirstOrDefault(u => u.Def.Id == center.Id);
        return (ctx, holder, target, foe, c);
    }

    static partial void CheckImpl()
    {
        Console.WriteLine("# 第255期 自己検査（burnhit check）");
        Console.WriteLine();
        Console.WriteLine("## 1. 盤面を直に組んで1発ずつ");
        Console.WriteLine();
        // (a) 前から燃えていた的への一撃 → 6 × 火勢・残りターンは減らない
        foreach (int lv in new[] { 0, 1, 3 })
        {
            var (ctx, _, t, foe, _) = Bench(TraitId.BurnHitAdd);
            t.SetCounter(StatusKeys.Burn, 3); t.SetCounter(FireLevelRule.LvKey, lv);
            int hb = t.Hp;
            ctx.ApplyDamage(t, 10, foe);
            int exp = 10 + 6 * Math.Max(1, lv);
            Ok($"燃えている味方への一撃（火勢 {lv}）で HP が 10 ＋ 6 × {Math.Max(1, lv)} 減る・残りターン 3 のまま", hb - t.Hp == exp && t.RawCounter(StatusKeys.Burn) == 3, $"減った {hb - t.Hp} ／ 残り {t.RawCounter(StatusKeys.Burn)}");
        }
        {   // (b) 燃えていない → 起きない
            var (ctx, _, t, foe, _) = Bench(TraitId.BurnHitAdd);
            int hb = t.Hp; ctx.ApplyDamage(t, 10, foe);
            Ok("燃えていない駒への一撃では起きない", hb - t.Hp == 10 && ctx.BurnHitBook.Fires.Sum() == 0);
        }
        // (c) 刻み・出どころなし・徴収・中継・呪いの共有・自分の一撃 → 起きない
        foreach (var (nm, act) in new (string, Action<BattleContext, UnitState, UnitState>)[]
        {
            ("出どころ null（刻みと同じ形）", (c, t, f) => c.ApplyDamage(t, 10, null)),
            ("燃焼の刻み（burnTick）", (c, t, f) => c.ApplyDamage(t, 10, null, burnTick: true)),
            ("徴収（levy）", (c, t, f) => c.ApplyDamage(t, 10, f, levy: true)),
            ("肩代わりの中継（relayed）", (c, t, f) => c.ApplyDamage(t, 10, f, relayed: true)),
            ("呪いの共有（hexShare）", (c, t, f) => c.ApplyDamage(t, 10, f, hexShare: true)),
            ("自分の一撃（source == target）", (c, t, f) => c.ApplyDamage(t, 10, t, isFriendlyFire: true)),
        })
        {
            var (ctx, _, t, foe, _) = Bench(TraitId.BurnHitAdd);
            t.SetCounter(StatusKeys.Burn, 3);
            int hb = t.Hp; act(ctx, t, foe);
            Ok($"{nm} では起きない", hb - t.Hp == 10 && ctx.BurnHitBook.Fires.Sum() == 0, $"減った {hb - t.Hp}");
        }
        {   // (d) 味方の一撃（巻き込み）は数える
            var (ctx, h, t, _, _) = Bench(TraitId.BurnHitAdd);
            t.SetCounter(StatusKeys.Burn, 3);
            int hb = t.Hp; ctx.ApplyDamage(t, 10, h, isFriendlyFire: true);
            Ok("味方の一撃（巻き込み・isFriendlyFire）でも起きる", hb - t.Hp == 16 && ctx.BurnHitBook.Fires[0] == 1);
        }
        {   // (e) 破片で受け切っても数える（燃焼も破片を削る）
            var (ctx, _, t, foe, _) = Bench(TraitId.BurnHitAdd);
            t.SetCounter(StatusKeys.Burn, 3); t.SetCounter(StatusKeys.Armor, 50);
            int hb = t.Hp; ctx.ApplyDamage(t, 10, foe);
            Ok("破片で受け切った一撃でも起きる（破片 50 → 34・HP は減らない）", hb == t.Hp && t.RawCounter(StatusKeys.Armor) == 34 && ctx.BurnHitBook.Fires[0] == 1, $"破片 {t.RawCounter(StatusKeys.Armor)}");
        }
        {   // (f) 札の無い戦では起きない（H0）・計数の札でも盤面は動かない
            var (ctx, _, t, foe, _) = Bench(null);
            t.SetCounter(StatusKeys.Burn, 3);
            int hb = t.Hp; ctx.ApplyDamage(t, 10, foe);
            Ok("札が無ければ起きない（H0）", hb - t.Hp == 10);
            var (c2, _, t2, f2, _) = Bench(TraitId.BurnHitCount);
            t2.SetCounter(StatusKeys.Burn, 3);
            int hb2 = t2.Hp; c2.ApplyDamage(t2, 10, f2);
            Ok("計数の札は機会を数えるだけ（HP は 10 だけ減る・機会 1）", hb2 - t2.Hp == 10 && c2.BurnHitBook.Chances[0] == 1 && c2.BurnHitBook.Fires.Sum() == 0);
        }
        {   // (g) 敵の脆さ（敵だけ 25%・切り上げ）は被弾の燃焼にも乗る
            var (ctx, _, t, _, _) = Bench(TraitId.BurnHitAdd, foeSideTarget: true);
            var hitter = ctx.LivingMembers(BattleContext.PlayerTeam).First();
            t.SetCounter(StatusKeys.Burn, 3);
            int hb = t.Hp; ctx.ApplyDamage(t, 10, hitter);
            Ok("燃えている敵: 一撃 10 → 13・被弾の燃焼 6 → 8（脆さ 25%・切り上げ）", hb - t.Hp == 21, $"減った {hb - t.Hp}");
        }
        {   // (h) ヒヨの火の変換 → 回復になる
            var (ctx, _, t, foe, _) = Bench(TraitId.BurnHitAdd, center: UnitCatalog.Hiyo);
            t.SetCounter(StatusKeys.Burn, 3); t.Hp = 500;
            ctx.ApplyDamage(t, 10, foe);
            Ok("ヒヨの火の変換: 被弾の燃焼 6 が回復になる（500 − 10 ＋ 6）", t.Hp == 496 && ctx.BurnHitBook.HitHeal[0] == 6, $"HP {t.Hp}");
        }
        {   // (i) ベニの反転 → 回復
            var (ctx, _, t, foe, _) = Bench(TraitId.BurnHitAdd, center: UnitCatalog.Beni);
            t.SetCounter(StatusKeys.Burn, 3); t.Hp = 500;
            ctx.ApplyDamage(t, 10, foe);
            Ok("ベニの反転: 隣の味方の被弾の燃焼 6 が回復になる", t.Hp == 496, $"HP {t.Hp}");
        }
        {   // (j) 熾のホタ（火の癒し）→ 回復
            var (ctx, _, t, foe, _) = Bench(TraitId.BurnHitAdd, targetDef: UnitCatalog.Hota);
            t.SetCounter(StatusKeys.Burn, 3); t.Hp = 30;
            ctx.ApplyDamage(t, 10, foe);
            Ok("熾のホタ（火の癒し）: 被弾の燃焼 6 が回復になる", t.Hp == 26, $"HP {t.Hp}");
        }
        {   // (k) 敵だけ: 持ち主の陣営（味方）では起きない・相手（敵）では起きる
            var (ctx, _, t, foe, _) = Bench(TraitId.BurnHitFoeOnly);
            t.SetCounter(StatusKeys.Burn, 3);
            int hb = t.Hp; ctx.ApplyDamage(t, 10, foe);
            Ok("敵だけ: 持ち主の陣営の燃えている駒では起きない", hb - t.Hp == 10 && ctx.BurnHitBook.GateOff[0] == 1);
            var (c2, _, t2, _, _) = Bench(TraitId.BurnHitFoeOnly, foeSideTarget: true);
            var hitter = c2.LivingMembers(BattleContext.PlayerTeam).First();
            t2.SetCounter(StatusKeys.Burn, 3);
            int hb2 = t2.Hp; c2.ApplyDamage(t2, 10, hitter);
            Ok("敵だけ: 持ち主の相手の燃えている駒では起きる", hb2 - t2.Hp == 21 && c2.BurnHitBook.Fires[1] == 1);
        }
        // (l) 分担: ターン頭の刻みは火勢に関わらず 6 × 1（足すは 6 × 火勢）。ボルグ（E2: 敵の刻みを火勢の回数に）がいる盤面。
        foreach (var (card, expTicks) in new (TraitId?, int)[] { (null, 3), (TraitId.BurnHitAdd, 3), (TraitId.BurnHitSplit, 1), (TraitId.BurnHitSplitOnce, 1) })
        {
            var ctx = new BattleContext(0, true);
            var pl = Formation.Build(front1: WithCard(UnitCatalog.Borg, card));
            var en = Formation.Build(front1: Plain("foe", "殴り手"));
            var p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
            var e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
            foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
            foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
            var foe = e[0];
            foe.SetCounter(StatusKeys.Burn, 3); foe.SetCounter(FireLevelRule.LvKey, 3);
            int n0 = ctx.Events.Count;
            ctx.TickStatuses();
            int ticks = ctx.Events.Skip(n0).Count(x => x.Kind == BattleEventKind.Status && x.TargetId == foe.InstanceId && x.Text == "燃焼");
            Ok($"ターン頭の刻み（敵の火勢 3）: {(card is null ? "H0" : card.ToString())} で {expTicks} 回", ticks == expTicks, $"{ticks} 回");
        }
        {   // (m) 回避した一撃では起きない（逃げ上手のセロ・回避は乱数・seed を振って避けた回を拾う）
            int evaded = 0, bad = 0, hits = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var ctx = new BattleContext(seed, true);
                var pl = Formation.Build(front1: WithCard(Plain("hold", "持ち主"), TraitId.BurnHitAdd), front3: UnitCatalog.Sero);
                var en = Formation.Build(front1: Plain("foe", "殴り手"));
                var p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
                var e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
                foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
                foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
                var sero = p.First(u => u.Def.Id == "sero");
                sero.SetCounter(StatusKeys.Burn, 3);
                int hb = sero.Hp;
                ctx.ApplyDamage(sero, 5, e[0]);
                if (sero.Hp == hb) { evaded++; if (ctx.BurnHitBook.Fires.Sum() > 0) bad++; }
                else { hits++; if (ctx.BurnHitBook.Fires.Sum() != 1) bad++; }
            }
            Ok("回避した一撃では起きない・当たった一撃では1回", evaded > 0 && hits > 0 && bad == 0, $"避けた {evaded} ／ 当たった {hits} ／ 外れ {bad}");
        }
        Console.WriteLine();

        // ---------------------------------------------------------------------------------
        Console.WriteLine("## 2. 台で回す戦の不変量（T3-244・T3-238・雷＋ボルグ・毒・参考 移動 × 九/新兵 400/300・本編 第2〜5波 400/300・的・九 × seed 0..49）");
        Console.WriteLine();
        var boards = FixedBoards.Concat(RefBoards).ToArray();
        var waves = new (int W, int S)[] { (BA.MainWave, 1), (0, 1), (1, 1), (2, 1), (3, 1), (FC.WaveTarget9, 0) };
        long verbBad = 0, zeroBad = 0, zeroN = 0, rep1 = 0, repS = 0, skip1 = 0, allyFoe = 0, foeFoe = 0, cause = 0, dev = 0, fell = 0, battles = 0, refBad = 0;
        long hitEvBad = 0, hitEv = 0, hitAdj = 0, armorOnly = 0;
        foreach (string bn in boards)
        {
            var bf = BaseOf(bn, null);
            foreach (var (w, s) in waves)
                for (int seed = 0; seed < 50; seed++)
                {
                    var (r0, _, _) = FC.Fight(bf, w, ScOf(w, s), seed);
                    ulong sig0 = SigOf(r0);
                    foreach (var v in Versions.Skip(1))
                    {
                        var f = Mark(bf, v.Card);
                        var (r, p, _) = FC.Fight(f, w, ScOf(w, s), seed);
                        var (rq, _, _) = FC.Fight(f, w, ScOf(w, s), seed, verbose: false);
                        battles++;
                        if (r.PlayerWon != rq.PlayerWon || r.Turns != rq.Turns || r.PlayerStarterFallen.Count != rq.PlayerStarterFallen.Count) verbBad++;
                        var b = r.BurnHit!;
                        if (v.Name == "H-足す" && b.Fires.Sum() == 0) { zeroN++; if (SigOf(r) != sig0) zeroBad++; }
                        if (v.Name == "H-分担1") { rep1 += b.Repeats.Sum(); skip1 += b.Skipped.Sum(); }
                        if (v.Name == "H-分担") repS += b.Repeats.Sum();
                        if (v.Name == "H-敵だけ") { allyFoe += b.Fires[0]; foeFoe += b.Fires[1]; }
                        if (bn is "毒" or "参考 移動" && SigOf(r) != sig0) refBad++;
                        var a = new Agg(); a.Take(r, p, w);
                        cause += a.Cause.Sum(); dev += a.DeathEv; fell += a.FellRes;
                        // 被弾の燃焼の出来事（燃焼の Status に一撃の主）は、同じターンの中で前に同じ主から同じ駒への Damage がある。
                        // 隣接 ＝ その Damage と燃焼の間に、その被弾の燃焼の出来事（同じ駒の燃焼の Status・出どころ null の Damage）しか無い。
                        var ev = r.Events;
                        for (int i = 0; i < ev.Count; i++)
                        {
                            var x = ev[i];
                            if (x.Kind != BattleEventKind.Status || x.Text != "燃焼" || x.ActorId is null) continue;
                            if (i > 0 && ev[i - 1].Kind == BattleEventKind.Damage && ev[i - 1].ActorId is null && ev[i - 1].TargetId == x.TargetId
                                && i > 1 && ev[i - 2].Kind == BattleEventKind.Status && ev[i - 2].ActorId == x.ActorId) continue;   // 火勢の2回目以降
                            hitEv++;
                            int j = i - 1; bool adj = true;
                            for (; j >= 0 && ev[j].Turn == x.Turn; j--)
                            {
                                if (ev[j].Kind == BattleEventKind.Damage && ev[j].TargetId == x.TargetId && ev[j].ActorId == x.ActorId) break;
                                if (ev[j].Kind == BattleEventKind.Attack && ev[j].ActorId == x.ActorId) { armorOnly++; break; }   // 破片で受け切った一撃（Damage の出来事が無い）
                                adj = false;
                            }
                            if (j < 0 || ev[j].Turn != x.Turn) hitEvBad++;
                            else if (adj) hitAdj++;
                        }
                    }
                }
        }
        Ok("verbose の有無で勝敗・決着T・落ちた駒が変わらない（4 版）", verbBad == 0, $"{battles} 戦・ずれ {verbBad}");
        Ok("H-足す で被弾の燃焼が1度も起きなかった戦は、H0 と台本の指紋が一致（乱数・副作用なし）", zeroN > 0 && zeroBad == 0, $"{zeroN} 戦・ずれ {zeroBad}");
        Ok("H-分担1 は跳ね 0（同じ攻撃で同じ駒は1回まで）・止めた回数 > 0・H-分担 は跳ね > 0", rep1 == 0 && skip1 > 0 && repS > 0, $"分担1 跳ね {rep1} ／ 止めた {skip1} ／ 分担 跳ね {repS}");
        Ok("H-敵だけ は味方に起きず、敵に起きる", allyFoe == 0 && foeFoe > 0, $"味方 {allyFoe} ／ 敵 {foeFoe}");
        Ok("燃焼を使わない台（毒・参考 移動）は全版で H0 と台本の指紋が一致", refBad == 0, $"ずれ {refBad}");
        Ok("被弾の燃焼の出来事（1回の被弾の燃焼の最初の1件）は、同じターンの前に同じ主から同じ駒への Damage（破片で受け切った一撃は同じ主の Attack）がある", hitEv > 0 && hitEvBad == 0, $"{hitEv} 件・外れ {hitEvBad}・Attack で拾った {armorOnly}・直後（間に別の出来事が無い）{Pct(hitAdj, hitEv)}%");
        Ok("死因の合計 ＝ 倒れた出来事 ＝ 落ちた駒（受け入れ 4）", cause == dev && dev == fell, $"{cause} ／ {dev} ／ {fell}");
        Console.WriteLine();
        Console.WriteLine($"合計 ○ {_ok} ／ × {_ng}");
    }

    static ulong SigOf(BattleResult r)
    {
        ulong h = 14695981039346656037UL;
        void Mix(long x) { unchecked { h ^= (ulong)x; h *= 1099511628211UL; } }
        Mix(r.PlayerWon ? 1 : 0); Mix(r.Turns);
        foreach (var e in r.Events) { Mix((long)e.Kind); Mix(e.Turn); Mix(e.ActorId ?? -1); Mix(e.TargetId ?? -1); Mix(e.Amount); Mix(e.HpAfter); }
        foreach (var l in r.Log) Mix(l.Text.GetHashCode());
        return h;
    }
}
