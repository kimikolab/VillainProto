using System.Reflection;
using System.Text;
using BattleCore;
using static Common;

// =====================================================================================
// scorch check（第219期） —— 自己検査（受け入れ 2〜4）
//
// (2)  燃焼が1度も付かない戦は F1〜F4 で台本ごと変わらない（`compare` 61 行 × 第2〜5波 × seed 0..49）
// (3)  verbose の有無で勝敗・決着ターンが同じ（F0〜F4 × `compare` 61 行 ＋ H1・H2 の席）
// (4a) 盤面を直に組んで1発ずつ: 燃えていない駒に掛からない／F1・F2 で味方に掛からない／切り上げ／
//      破片・軛より前／中継（分かち）に2度乗らない／刻み1回ごと（濃縮の印）／最後の燃焼の刻み／反転（F3・F4）
// (4b) 台本: 脆さの欄が F0 で1件も無い／F1・F2 で味方に1件も無い／中継・共有に1件も無い
// =====================================================================================

static partial class ScorchDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");

    static BattleContext Ctx(string ver, Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, false, ember: EmberRule.Scorched(ver));
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        return ctx;
    }

    static int ok, ng;
    static void Expect(string what, long got, long want)
    {
        bool pass = got == want;
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }

    static partial void CheckImpl(string arg)
    {
        ok = ng = 0;
        Console.WriteLine("# 第219期 scorch check");
        Console.WriteLine();

        // ---------------------------------------------------------------- (4a)
        Console.WriteLine("## (4a) 盤面を直に組んで1発ずつ");
        Console.WriteLine();
        var knight = EnemyCatalog.KnightG;   // 第306期: 第二波の騎士に斬り返しが付いたので、特性の無い巡礼騎士を名指しで引く（第305期までの `Stages[1]` の特性なしの1体目と同じ物）
        Formation P1f = Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.TouT0);
        Formation E1f = Formation.Build(front1: knight, front3: knight);
        foreach (var (ver, pct) in new[] { ("F0", 0), ("F1", 25), ("F2", 50), ("F3", 25), ("F4", 50) })
        {
            bool allies = ver is "F3" or "F4";
            int Hit(bool burning, bool onPlayer, int amount, int armor = 0)
            {
                var ctx = Ctx(ver, P1f, E1f, out var p, out var e);
                UnitState t = onPlayer ? p[1] : e[0];
                UnitState src = onPlayer ? e[1] : p[1];
                t.Hp = 999; t.MaxHp = Math.Max(t.MaxHp, 999);
                if (burning) t.SetCounter(StatusKeys.Burn, 2);
                if (armor > 0) t.SetCounter(StatusKeys.Armor, armor);
                int before = t.Hp;
                ctx.ApplyDamage(t, amount, src);
                return before - t.Hp;
            }
            int Up(int a, int x) => a + (a * x + 99) / 100;
            Expect(ver + " 燃えていない敵 10", Hit(false, false, 10), 10);
            Expect(ver + " 燃えている敵 10", Hit(true, false, 10), Up(10, pct));
            Expect(ver + " 燃えている敵 1（切り上げ）", Hit(true, false, 1), Up(1, pct));
            Expect(ver + " 燃えている敵 6（燃焼の量）", Hit(true, false, 6), Up(6, pct));
            Expect(ver + " 燃えていない味方 10", Hit(false, true, 10), 10);
            Expect(ver + " 燃えている味方 10", Hit(true, true, 10), allies ? Up(10, pct) : 10);
            Expect(ver + " 燃えている敵 20・破片 8（脆さの後に破片）", Hit(true, false, 20, armor: 8), Up(20, pct) - 8);
        }
        Console.WriteLine();

        // 軛: 重装兵が生きていれば1発 25 で切る。脆さの後に切るので 20 × 1.5 = 30 → 25。
        {
            var yokeUnit = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied()).Select(o => o.Def).First(d => d.Traits.Contains(TraitId.Yoke));
            foreach (var (ver, want) in new[] { ("F0", 20L), ("F2", 25L) })
            {
                var ctx = Ctx(ver, P1f, Formation.Build(front1: knight, center: yokeUnit), out var p, out var e);
                UnitState t = e[0];
                t.Hp = 999; t.MaxHp = 999;
                t.SetCounter(StatusKeys.Burn, 2);
                ctx.ApplyDamage(t, 20, p[1]);
                Expect(ver + " 軛の波で燃えている敵 20（脆さの後に上限 25）", 999 - t.Hp, want);
            }
        }
        // 分かち: 殴られた味方の脆さは1度だけ（ドハの取り分は中継なので2度目は乗らない）。
        {
            var ctx = Ctx("F4", Formation.Build(front1: UnitCatalog.Doha, front3: UnitCatalog.TouT0), E1f, out var p, out var e);
            UnitState doha = p[0], tou = p[1];
            foreach (var u in p) { u.Hp = 999; u.MaxHp = 999; u.SetCounter(StatusKeys.Burn, 2); }
            ctx.ApplyDamage(tou, 20, e[0]);
            // 20 × 1.5 = 30 → ドハが 40% ＝ 12 を中継で受ける（燃えていても 12 のまま）・トウに 18。
            Expect("F4 分かち: トウ（燃えている）", 999 - tou.Hp, 18);
            Expect("F4 分かち: ドハ（燃えている・中継）", 999 - doha.Hp, 12);
        }
        // 刻み: 濃縮の印 2 の燃えている敵は燃焼の刻みを 3 回、1回ごとに脆さ。最後の刻み（残り 1）も掛かる。
        foreach (var (ver, per) in new[] { ("F0", 6), ("F1", 8), ("F2", 9) })
        {
            var ctx = Ctx(ver, P1f, E1f, out var p, out var e);
            UnitState t = e[0];
            t.Hp = 999; t.MaxHp = 999;
            t.SetCounter(StatusKeys.Burn, 1);
            ctx.MarkConcentrated(p[0], t, "検査");   // 印は窓口を通す（`_markLive` が立たないと繰り返しが走らない）
            ctx.MarkConcentrated(p[0], t, "検査");
            ctx.TickStatuses();
            Expect(ver + " 最後の燃焼の刻み × 印 2（3回）", 999 - t.Hp, 3 * per);
            Expect(ver + " 刻みの後の残りターン", t.RawCounter(StatusKeys.Burn), 0);
        }
        // 毒の刻み（燃えている敵）: 毒 3 → F1 で 4・F2 で 5。
        foreach (var (ver, want) in new[] { ("F0", 3 + 6), ("F1", 4 + 8), ("F2", 5 + 9) })
        {
            var ctx = Ctx(ver, P1f, E1f, out var p, out var e);
            UnitState t = e[0];
            t.Hp = 999; t.MaxHp = 999;
            t.SetCounter(StatusKeys.Burn, 3);
            t.SetCounter(StatusKeys.Poison, 3);
            ctx.TickStatuses();
            Expect(ver + " 燃えている敵の毒 3 ＋ 燃焼 6 の刻み", 999 - t.Hp, want);
        }
        // 反転（ベニの隣の味方の燃焼の刻み）: F0・F1 は 6、F3 は 8、F4 は 9 だけ回復する。
        foreach (var (ver, want) in new[] { ("F0", 6), ("F1", 6), ("F3", 8), ("F4", 9) })
        {
            var ctx = Ctx(ver, Formation.Build(front1: UnitCatalog.Beni, center: UnitCatalog.TouT0), E1f, out var p, out var e);
            UnitState tou = p.First(u => u.Def.Id == "tou");
            tou.MaxHp = 999; tou.Hp = 500;
            tou.SetCounter(StatusKeys.Burn, 2);
            ctx.TickStatuses();
            Expect(ver + " ベニの隣で燃える味方の刻み（反転の回復）", tou.Hp - 500, want);
        }
        Console.WriteLine();

        // ---------------------------------------------------------------- (2) (3) (4b)
        Console.WriteLine("## (2)(3)(4b) 戦闘を回して");
        Console.WriteLine();
        var rows = CompareBuilds();
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
                foreach (byte b in Encoding.UTF8.GetBytes(sb.ToString())) { h ^= b; h *= 1099511628211UL; }
            }
            return h.ToString("x16") + "/" + r.PlayerWon + "/" + r.Turns;
        }

        // (2)
        long noBurn = 0, noBurnDiff = 0;
        var gate = new object();
        Parallel.For(0, rows.Length * 4 * 50, j =>
        {
            var f = rows[j / 200].F; int st = 1 + (j / 50) % 4, s = j % 50;
            BattleResult r0 = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: true);
            var b = r0.Brittle!;
            if (b.IgniteFoe.Count + b.IgniteAlly.Count > 0) return;
            string d0 = Digest(r0);
            int diff = 0;
            foreach (string v in new[] { "F1", "F2", "F3", "F4" })
                if (Digest(BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: true, ember: EmberRule.Scorched(v))) != d0) diff++;
            lock (gate) { noBurn++; noBurnDiff += diff; }
        });
        Console.WriteLine("(2) 燃焼が1度も付かない戦 " + noBurn + " 戦 × F1〜F4:");
        Expect("台本が F0 と違った戦（延べ）", noBurnDiff, 0);

        // (3)
        long mism = 0, n3 = 0;
        var forms = rows.Select(r => r.F).Concat(RigSeats().Select(x => x.F)).Append(PonX()).ToArray();
        Parallel.For(0, forms.Length * Versions.Length * 4 * 20, j =>
        {
            int fi = j / (Versions.Length * 80), v = (j / 80) % Versions.Length, st = 1 + (j / 20) % 4, s = j % 20;
            var ember = EmberRule.Scorched(Versions[v]);
            var boss = BossOf(ShockDiag.Scale150);
            var a = BattleEngine.Run(forms[fi], EnemyCatalog.Stages[st].Enemy, s, verbose: true, ember: ember, boss: boss);
            var b = BattleEngine.Run(forms[fi], EnemyCatalog.Stages[st].Enemy, s, verbose: false, ember: ember, boss: boss);
            lock (gate) { n3++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) mism++; }
        });
        Console.WriteLine("(3) verbose の有無（" + n3 + " 戦・倍率 150）:");
        Expect("勝敗か決着ターンが違った戦", mism, 0);

        // (4b)
        long f0Tags = 0, allyTagsF12 = 0, relayTags = 0, tags = 0, tickTags = 0;
        var benches = RigSeats().Select(x => x.F).Append(PonX()).Concat(H3Rows().Select(x => x.F)).Concat(H4PrimeRows().Select(x => x.F)).ToArray();
        Parallel.For(0, benches.Length * Versions.Length * 4 * 25, j =>
        {
            int fi = j / (Versions.Length * 100), v = (j / 100) % Versions.Length, st = 1 + (j / 25) % 4, s = j % 25;
            var pl = BattleEngine.Materialize(benches[fi], BattleContext.PlayerTeam);
            var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, ShockDiag.Scale150);
            var r = BattleEngine.Run(pl, en, s, verbose: true, ember: EmberRule.Scorched(Versions[v]));
            var pid = pl.Select(u => u.InstanceId).ToHashSet();
            long a = 0, b = 0, c = 0, d = 0, tt = 0;
            BattleEvent? prev = null;
            foreach (BattleEvent ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.Damage && ev.BrittleExtra is int x && x > 0)
                {
                    d++;
                    if (v == 0) a++;
                    if (v is 1 or 2 && ev.TargetId is int t && pid.Contains(t)) b++;
                    if (ev.Relayed || ev.ShareFromId is not null) c++;
                    if (prev is { Kind: BattleEventKind.Status } && prev.TargetId == ev.TargetId) tt++;
                }
                prev = ev;
            }
            lock (gate) { f0Tags += a; allyTagsF12 += b; relayTags += c; tags += d; tickTags += tt; }
        });
        Console.WriteLine("(4b) 台本（H1・H2 の席・ポンの X字・H3・H4' × F0〜F4 × 第2〜5波 × seed 0..24・倍率 150）: 脆さの欄 " + tags + " 件（うち刻みの直後 " + tickTags + " 件）");
        Expect("F0 で脆さの欄が付いた Damage", f0Tags, 0);
        Expect("F1・F2 で味方に付いた脆さの欄", allyTagsF12, 0);
        Expect("中継・共有に付いた脆さの欄", relayTags, 0);
        Console.WriteLine();
        Console.WriteLine("SCORCH_CHECK_COMPLETE ok=" + (ng == 0) + "（○ " + ok + " ／ × " + ng + "）");
    }
}
