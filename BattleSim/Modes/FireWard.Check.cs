using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// fireward check —— 自己検査（受け入れ 2〜5）。1 は `digest` と `compare` の突き合わせで見る。
static partial class FireWardDiag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly PropertyInfo TurnP = typeof(BattleContext).GetProperty("Turn")!;
    static int ok, ng;
    static void Expect(string what, object got, object want)
    {
        bool pass = Equals(got, want);
        if (pass) ok++; else ng++;
        Console.WriteLine("- " + (pass ? "○" : "**×**") + " " + what + ": " + got + (pass ? "" : "（期待 " + want + "）"));
    }
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 100, int atk = 10, params TraitId[] tr)
        => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = tr, Pattern = AttackPattern.Single };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static UnitTally Tly(BattleContext c, string id) => ((Dictionary<string, UnitTally>)typeof(BattleContext)
        .GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!)[id];
    static void Tick(BattleContext ctx) => typeof(BattleContext).GetMethod("TickStatuses", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(ctx, Array.Empty<object>());
    static void Burn(UnitState u) => u.SetCounter(StatusKeys.Burn, 3);

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第238期 fireward check");
        Console.WriteLine();
        var e1 = Formation.Build(front1: Plain("e1", hp: 1000));
        // X 字の隣接: 前1 は 中央・後1 と隣、後3 とは隣でない
        Expect("（前提）前1 と中央・後1 は隣、後3 は隣でない", $"{FormationRules.AreAdjacent(0, 2)}/{FormationRules.AreAdjacent(0, 3)}/{FormationRules.AreAdjacent(0, 4)}", "True/True/False");

        // ---- D: 盾の配り ----
        foreach (var (nm, borg, adjHalf, farHalf) in new[] { ("D1", D1, true, false), ("D2", D2, true, true) })
        {
            var ctx = Ctx(Formation.Build(front1: borg, center: Plain("adj"), back3: Plain("far"), back1: Plain("cold")), e1, out var p, out var e);
            var b = U(p, "borg"); var adj = U(p, "adj"); var far = U(p, "far"); var cold = U(p, "cold"); var en = U(e, "e1");
            Burn(adj); Burn(far);
            int Hit(UnitState u) { int h = u.Hp; ctx.ApplyDamage(u, 20, en); return h - u.Hp; }
            Expect($"{nm}: 隣の燃えている味方への敵の一撃 20", Hit(adj), adjHalf ? 10 : 20);
            Expect($"{nm}: 隣でない燃えている味方", Hit(far), farHalf ? 10 : 20);
            Expect($"{nm}: 隣の燃えていない味方", Hit(cold), 20);
            Burn(b);
            Expect($"{nm}: ボルグ自身（燃えている）は火の鎧の半減だけ（重ねない）", Hit(b), 10);
            int h0 = adj.Hp; ctx.ApplyDamage(adj, 20, cold, isFriendlyFire: true);
            Expect($"{nm}: 味方の刃には掛からない", h0 - adj.Hp, 20);
            h0 = adj.Hp; ctx.ApplyDamage(adj, 6, null, burnTick: true);
            Expect($"{nm}: 燃焼ダメージ（刻み）には掛からない", h0 - adj.Hp, 6);
            var t = Tly(ctx, "borg");
            Expect($"{nm}: 帳簿（回数 ／ 切った ／ 受け手の側）", $"{t.FireWardHits}/{t.FireWardSaved}/{Tly(ctx, "adj").FireWardTaken}", adjHalf && farHalf ? "2/20/10" : "1/10/10");
            int evs = ctx.Events.Count(x => x.Kind == BattleEventKind.FireArmor && x.Text == FireArmorLabels.Ward);
            Expect($"{nm}: 台本の「盾の配り」の件数 ＝ 帳簿の回数", evs, (int)t.FireWardHits);
            ctx.ApplyDamage(b, 5000, en);
            Expect($"{nm}: ボルグが倒れた後は掛からない", $"{b.IsAlive}/{Hit(adj)}", "False/20");
        }
        // 切り上げ: 21 → 11
        {
            var ctx = Ctx(Formation.Build(front1: D1, center: Plain("adj")), e1, out var p, out var e);
            var adj = U(p, "adj"); Burn(adj); int h = adj.Hp; ctx.ApplyDamage(adj, 21, U(e, "e1"));
            Expect("D: 半分は切り上げ（21 → 11）", h - adj.Hp, 11);
        }

        // ---- V: 火の変換（刻み）----
        foreach (var (nm, hiyo, gain) in new[] { ("V1", V1, BurnRules.Damage), ("V2", V2, BurnRules.Damage / 2) })
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a1"), back3: hiyo, center: UnitCatalog.Hota, back1: BorgA), e1, out var p, out _);
            var a1 = U(p, "a1"); var hota = U(p, "hota"); var borg = U(p, "borg");
            foreach (var u in new[] { a1, hota, borg }) { u.Hp = u.MaxHp - 20; Burn(u); }
            a1.SetCounter(StatusKeys.Poison, 0);
            int ha = a1.Hp, hh = hota.Hp, hb = borg.Hp;
            Tick(ctx);
            var t = Tly(ctx, "hiyo");
            Expect($"{nm}: 燃えている味方の刻みは回復（{BurnRules.Damage} → +{gain}）", a1.Hp - ha, gain);
            Expect($"{nm}: ホタ（熾火）は変わらない・優先は焼かれない", $"{hota.Hp - hh}/{t.FireConvPrecPyre}", $"0/{BurnRules.Damage}");
            Expect($"{nm}: ボルグは火の癒しが先（刻みの量だけ回復・V は数えない）", $"{borg.Hp - hb}/{t.FireConvPrecMend}", $"{BurnRules.Damage}/{BurnRules.Damage}");
            Expect($"{nm}: 帳簿（変換 名目 ／ 癒えた ／ 優先の変換）", $"{t.FireConvTickNominal}/{t.FireConvTickHealed}/{t.FireConvPrecV}", $"{BurnRules.Damage}/{gain}/{BurnRules.Damage}");
            int evs = ctx.Events.Where(x => x.Kind == BattleEventKind.FireArmor && x.Text == FireArmorLabels.Convert).Sum(x => x.Amount);
            Expect($"{nm}: 台本の「火の変換」の量 ＝ 帳簿の名目", (long)evs, t.FireConvTickNominal + t.FireConvSplashNominal);
            a1.Hp = a1.MaxHp - 1; Burn(a1); Tick(ctx);
            Expect($"{nm}: 上限を超えない", a1.Hp, a1.MaxHp);
        }
        // V: ベニの結界の内側はベニが先
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a1"), center: UnitCatalog.Beni, back3: V1), e1, out var p, out _);
            var a1 = U(p, "a1"); a1.Hp = a1.MaxHp - 20; Burn(a1); int h = a1.Hp;
            Tick(ctx);
            var t = Tly(ctx, "hiyo");
            Expect("V: ベニの隣は反転（回復）が先・V は数えない", $"{a1.Hp - h > 0}/{t.FireConvPrecBeni}/{t.FireConvPrecV}", $"True/{BurnRules.Damage}/0");
        }
        // V: 毒と物理には掛からない・ヒヨが倒れたら掛からない
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a1"), back3: V1), e1, out var p, out var e);
            var a1 = U(p, "a1"); var hiyo = U(p, "hiyo");
            a1.Hp = a1.MaxHp - 20; a1.SetCounter(StatusKeys.Poison, 4); Burn(a1);
            int h = a1.Hp; Tick(ctx);
            Expect("V: 毒の刻みはダメージのまま（毒 4 と燃焼 +6 の差）", a1.Hp - h, BurnRules.Damage - 4);
            h = a1.Hp; ctx.ApplyDamage(a1, 20, U(e, "e1"));
            Expect("V: 敵の攻撃はダメージのまま", h - a1.Hp, 20);
            ctx.ApplyDamage(hiyo, 5000, U(e, "e1"));
            a1.SetCounter(StatusKeys.Poison, 0); Burn(a1); h = a1.Hp; Tick(ctx);
            Expect("V: ヒヨが倒れた後は燃焼の刻みがダメージに戻る", h - a1.Hp, BurnRules.Damage);
        }
        // V: 燃える巻き込み（燃えている隣だけが回復・燃えていない隣は巻き込みのまま）
        {
            var ctx = Ctx(Formation.Build(front1: Plain("hot"), front3: Plain("cold"), center: BorgA, back3: V1, back1: UnitCatalog.Hota), e1, out var p, out _);
            var borg = U(p, "borg"); var hot = U(p, "hot"); var cold = U(p, "cold");
            hot.Hp = hot.MaxHp - 30; cold.Hp = cold.MaxHp - 30; Burn(hot);
            int h1 = hot.Hp, h2 = cold.Hp;
            ctx.PerformAttack(borg);
            int spill = Math.Max(1, borg.Def.Attack / 2);
            var t = Tly(ctx, "hiyo");
            Expect("V: 燃えている隣への燃える巻き込みは回復", hot.Hp - h1, spill);
            Expect("V: 燃えていない隣は巻き込みの量そのまま", h2 - cold.Hp, spill);
            Expect("V: 帳簿（巻き込み 名目 ／ 癒えた）", $"{t.FireConvSplashNominal}/{t.FireConvSplashHealed}", $"{spill}/{spill}");
        }
        // V の a ／ b: 渇き
        foreach (var (nm, hiyo, want) in new[] { ("a", V1, BurnRules.Damage), ("b", DryOf(V1), 0) })
        {
            var ctx = Ctx(Formation.Build(front1: Plain("a1"), back3: hiyo), Formation.Build(front1: Plain("dr", tr: TraitId.Drought)), out var p, out _);
            var a1 = U(p, "a1"); a1.Hp = a1.MaxHp - 20; Burn(a1); int h = a1.Hp;
            Tick(ctx);
            Expect($"V {nm}: 渇きの保持者がいるときの変換（受けない・回復は {want}）", a1.Hp - h, want);
        }

        // 実戦: verbose の有無・死因・台本 ＝ 帳簿
        var boards = new List<Formation>();
        foreach (var v in Versions.Skip(1))
        {
            boards.Add(Dec(ProbeSeats[0].Enc, v));
            boards.Add(Dec(ProbeSeats[1].Enc, v));
            boards.Add(Dec(ProbeSeats[2].Enc, v));
        }
        long battles = 0, diff = 0, deathMismatch = 0, deaths = 0, evMismatch = 0, evs2 = 0;
        var lk = new object();
        foreach (var f in boards)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                foreach (var (_, sc) in BA.Scales)
                    Parallel.For(0, 4, s =>
                    {
                        var (r, p, e, slot0) = BA.Fight(f, w, sc, s, true);
                        var (r2, _, _, _) = BA.Fight(f, w, sc, s, false);
                        bool same = r.PlayerWon == r2.PlayerWon && r.Turns == r2.Turns
                                    && string.Join(",", r.PlayerStarterFallen.OrderBy(x => x)) == string.Join(",", r2.PlayerStarterFallen.OrderBy(x => x));
                        var tb = r.TallyByUnit["borg"]; var tb2 = r2.TallyByUnit["borg"]; var th = r.TallyByUnit["hiyo"]; var th2 = r2.TallyByUnit["hiyo"];
                        same &= tb.FireWardSaved == tb2.FireWardSaved && th.FireConvTickNominal == th2.FireConvTickNominal && th.FireConvSplashHealed == th2.FireConvSplashHealed;
                        var z = new ZAgg(); z.Take(r, p, e, slot0);
                        var ev = r.Events.Where(x => x.Kind == BattleEventKind.FireArmor).ToList();
                        bool evOk = ev.Count(x => x.Text == FireArmorLabels.Ward) == tb.FireWardHits
                                    && ev.Where(x => x.Text == FireArmorLabels.Ward).Sum(x => (long)x.Amount) == tb.FireWardSaved
                                    && ev.Where(x => x.Text == FireArmorLabels.Convert).Sum(x => (long)x.Amount) == th.FireConvTickNominal + th.FireConvSplashNominal;
                        lock (lk)
                        {
                            battles++; if (!same) diff++;
                            var a = z.A;
                            deaths += a.DeathEvents;
                            if (a.Cause.Sum() != a.DeathEvents || a.FellSum != a.DeathEvents || z.FellBy.Values.Sum() != a.DeathEvents
                                || z.Y.SplashPhysDeaths + z.Y.SplashBurnDeaths != a.Cause[BA.CBorgSplash]) deathMismatch++;
                            if (!evOk) evMismatch++;
                            evs2 += ev.Count(x => x.Text is FireArmorLabels.Ward or FireArmorLabels.Convert);
                        }
                    });
        Expect($"verbose の有無で勝敗・決着T・倒れた駒・この期の帳簿が違う戦（{battles} 戦）", diff, 0L);
        Expect($"死因の合計 ≠ 倒れた駒（役ごと・巻き込みの物理 ＋ 燃焼 を含む）の戦（倒れた {deaths} 件）", deathMismatch, 0L);
        Expect($"台本（盾の配りの件数と量・火の変換の量）≠ 帳簿の戦（盾の配り・火の変換の台本 {evs2} 件）", evMismatch, 0L);

        // 乱数: 新しい処理は Roll / PickOne を呼ばない（本文の走査）
        string src = File.ReadAllText(Path.Combine("BattleCore", "BattleEngine.cs")).Replace("\r\n", "\n");
        var spans = new List<string>();
        foreach (var (a, b) in new[] { ("    // 第238期 —— 盾の配り（ボルグ", "    /// <summary>くすぶり（第235期"), ("        // 盾の配り（第238期", "        if (amount <= 0) return;") })
        {
            int i = src.IndexOf(a, StringComparison.Ordinal), j = i < 0 ? -1 : src.IndexOf(b, i, StringComparison.Ordinal);
            if (i < 0 || j < 0) throw new InvalidOperationException("走査の目印が見つからない（R034）: " + a);
            spans.Add(src[i..j]);
        }
        Expect("新しい処理の本文に Roll( / PickOne( が無い（engine の2区間）", spans.Count(s => s.Contains("Roll(") || s.Contains("PickOne(")), 0);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
