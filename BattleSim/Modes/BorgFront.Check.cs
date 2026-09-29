using System.Reflection;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using BG = BorgGuardDiag;

// borgfront check —— 自己検査（受け入れ 2〜5）。1 は `digest` と `compare` の突き合わせで見る。
static partial class BorgFrontDiag
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
    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e, bool verbose = true)
    {
        var ctx = new BattleContext(0, verbose);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnP.SetValue(ctx, 1);
        return ctx;
    }
    static UnitDef Plain(string id, int hp = 100, int atk = 10, AttackPattern pat = AttackPattern.Single, params TraitId[] tr)
        => new() { Id = id, Name = id, MaxHp = hp, Attack = atk, Speed = 1, Traits = tr, Pattern = pat };
    static UnitState U(List<UnitState> xs, string id) => xs.First(u => u.Def.Id == id);
    static UnitTally Tly(BattleContext c, string id = "borg") => ((Dictionary<string, UnitTally>)typeof(BattleContext)
        .GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(c)!)[id];
    static int Burn(UnitState u) => u.RawCounter(StatusKeys.Burn);
    static void Tick(BattleContext ctx) => typeof(BattleContext).GetMethod("TickStatuses", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(ctx, Array.Empty<object>());

    static partial void CheckImpl()
    {
        ok = ng = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第235期 borgfront check");
        Console.WriteLine();
        var all = VerBorg("全部");

        // S: 燃える巻き込み（ボルグ中央 ＝ 角4つ全員が隣）
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Hota, front3: Plain("a1"), center: VerBorg("B+S"), back1: UnitCatalog.Beni, back3: Plain("a2")),
                Formation.Build(front1: Plain("e1", hp: 1000)), out var p, out var e);
            var borg = U(p, "borg"); var hota = U(p, "hota"); var a1 = U(p, "a1"); var a2 = U(p, "a2"); var beni = U(p, "beni");
            foreach (var u in new[] { hota, a1, a2, beni }) u.Hp = u.MaxHp - 30;
            a1.SetCounter(StatusKeys.Burn, 2); a2.SetCounter(StatusKeys.Burn, 2);   // 燃えている味方にも脆さは掛からない（規定は敵だけ）
            var hp0 = p.ToDictionary(u => u, u => u.Hp);
            ctx.PerformAttack(borg);
            int spill = Math.Max(1, borg.Def.Attack / 2);
            string Delta(UnitState u) => (u.Hp - hp0[u]).ToString();
            var inv = new[] { a1, a2 }.Select(u => ctx.InvertsTick(u) is not null).ToArray();
            Expect("S: ホタ（焼かれない）は 0", Delta(hota), "0");
            for (int k = 0; k < 2; k++)
            {
                var u = k == 0 ? a1 : a2;
                Expect($"S: {u.Def.Id}（{(inv[k] ? "ベニの結界の内側 → 回復" : "結界の外 → 巻き込みの量そのまま・脆さなし")}）", Delta(u), inv[k] ? $"+{spill}".TrimStart('+') : $"-{spill}");
            }
            Expect("S: ベニ自身（結界の内側・保持者自身も含む）→ 回復に反転", $"{Delta(beni)}/{ctx.InvertsTick(beni) is not null}", $"{spill}/True");
            var t = Tly(ctx);
            Expect("S: 帳簿の反転（名目 ／ 癒えた）", $"{t.FireSplashInverted}/{t.FireSplashInvHealed}", $"{spill}/{spill}");
            Expect("S: 帳簿（回数 ／ 名目 ／ 焼かれない）", $"{t.FireSplashHits}/{t.FireSplashNominal}/{t.FireSplashImmune}", $"4/{4 * spill}/{spill}");
            int evs = ctx.Events.Count(x => x.Kind == BattleEventKind.FireArmor && x.Text == FireArmorLabels.Splash);
            Expect("S: 台本の「燃える巻き込み」の件数 ＝ 帳簿の回数", evs, (int)t.FireSplashHits);
            Expect("S: 着火（火の粉）は今までどおり隣に点く（ホタ）", Burn(hota) > 0, true);
        }
        // S の対照: B（S なし）ではホタも削られる
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Hota, center: VerBorg("B")), Formation.Build(front1: Plain("e1", hp: 1000)), out var p, out _);
            var hota = U(p, "hota"); int h0 = hota.Hp;
            ctx.PerformAttack(U(p, "borg"));
            Expect("（対照）B の巻き込みはホタも削る", h0 - hota.Hp, UnitCatalog.BorgF0.Attack / 2);
        }
        // O: くすぶりは開戦時に1回（実戦で）
        {
            long battles = 0, bad = 0;
            foreach (var w in new[] { 0, 4 })
                for (int s = 0; s < 20; s++)
                {
                    var f = BA.Seat(new[] { all, UnitCatalog.Hota, UnitCatalog.HiyoF0, UnitCatalog.Golm, UnitCatalog.Susu });
                    var (r, p, _, _) = BA.Fight(f, w, BA.Scales[0].Sc, s);
                    var k = r.Events.Where(x => x.Kind == BattleEventKind.FireArmor && x.Text == FireArmorLabels.Kindle).ToList();
                    battles++;
                    if (k.Count != 1 || k[0].Turn != 0 || r.TallyByUnit["borg"].SelfKindleLit != 1) bad++;
                }
            Expect($"O: くすぶりは開戦時（ターン 0）に1回（{battles} 戦）", bad, 0L);
        }
        // H1: 刻みの量だけ回復し、上限を超えない
        {
            var ctx = Ctx(Formation.Build(front1: all), Formation.Build(front1: Plain("e1")), out var p, out _);
            var borg = U(p, "borg");
            borg.Hp = borg.MaxHp - 20; ctx.Ignite(borg); int h0 = borg.Hp;
            Tick(ctx);
            Expect("H1: 燃焼の刻みで刻みの量だけ回復", borg.Hp - h0, BurnRules.Damage);
            borg.Hp = borg.MaxHp - 2; Tick(ctx);
            Expect("H1: 上限を超えない", borg.Hp, borg.MaxHp);
            var ctx2 = Ctx(Formation.Build(front1: VerBorg("全部−H1")), Formation.Build(front1: Plain("e1")), out var p2, out _);
            var b2 = U(p2, "borg"); b2.Hp = b2.MaxHp - 20; ctx2.Ignite(b2); int h2 = b2.Hp; Tick(ctx2);
            Expect("（対照）全部−H1 は焼かれないだけ（増えも減りもしない）", b2.Hp - h2, 0);
        }
        // H1a / H1b: 渇き
        {
            foreach (var (nm, def, want) in new[] { ("H1a", all, BurnRules.Damage), ("H1b", H1b, 0) })
            {
                var ctx = Ctx(Formation.Build(front1: def), Formation.Build(front1: Plain("dr", tr: TraitId.Drought)), out var p, out _);
                var borg = U(p, "borg"); borg.Hp = borg.MaxHp - 20; ctx.Ignite(borg); int h0 = borg.Hp;
                Tick(ctx);
                Expect($"{nm}: 渇きの保持者がいるときの火の癒し", borg.Hp - h0, want);
            }
        }
        // H1: ベニの反転の裏（回復がダメージに化ける）は通らない
        {
            var ctx = Ctx(Formation.Build(front1: all, center: UnitCatalog.Beni), Formation.Build(front1: Plain("e1")), out var p, out _);
            var borg = U(p, "borg"); borg.Hp = borg.MaxHp - 20; ctx.Ignite(borg); int h0 = borg.Hp;
            Tick(ctx);
            Expect("H1: ベニの隣でも刻みの量だけ回復（二重にも逆にもならない）", borg.Hp - h0, BurnRules.Damage);
        }
        // H2: 殴る前に燃えていた主目標のときだけ
        {
            var ctx = Ctx(Formation.Build(front1: all), Formation.Build(front1: Plain("e1", hp: 1000)), out var p, out var e);
            var borg = U(p, "borg"); var e1 = U(e, "e1");
            borg.Hp = 50;
            ctx.PerformAttack(borg);   // 主目標は燃えていない（火の粉で点くのは攻撃の後）
            var t = Tly(ctx);
            Expect("H2: 燃えていない主目標では働かない（回復 ／ 自分の火）", $"{borg.Hp}/{t.FireFeedFires}/{Burn(borg)}", "50/0/0");
            ctx.PerformAttack(borg);   // さっきの火の粉で燃えている主目標
            int heal = Math.Max(1, borg.Def.Attack / 2);
            Expect("H2: 燃えていた主目標を殴ると与えた量の半分を回復し、自分に火", $"{borg.Hp}/{t.FireFeedFires}/{Burn(borg) > 0}", $"{50 + heal}/1/True");
            borg.Hp = borg.MaxHp - 1; ctx.PerformAttack(borg);
            Expect("H2: 上限を超えない", borg.Hp, borg.MaxHp);
        }

        // 実戦: verbose の有無・死因の合計 ＝ 倒れた駒（巻き込みの物理 ＋ 燃焼 ＝ 巻き込み）・台本の件数 ＝ 帳簿
        var boards = new List<Formation>();
        foreach (var (_, _, _, borg) in Versions.Skip(1))
        {
            boards.Add(BA.Seat(new[] { borg, UnitCatalog.Hota, UnitCatalog.HiyoF0, UnitCatalog.Beni, UnitCatalog.Sasa }));
            boards.Add(BA.Seat(new[] { UnitCatalog.Golm, borg, UnitCatalog.Hota, UnitCatalog.HiyoF0, UnitCatalog.Shio }));
            boards.Add(ThunderSwap(borg, true));
        }
        long battles2 = 0, diff = 0, deathMismatch = 0, deaths = 0, evMismatch = 0, evs2 = 0;
        var lk = new object();
        foreach (var f in boards)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                foreach (var (_, sc) in BA.Scales)
                    Parallel.For(0, 6, s =>
                    {
                        var (r, p, e, slot0) = BA.Fight(f, w, sc, s, true);
                        var (r2, _, _, _) = BA.Fight(f, w, sc, s, false);
                        bool same = r.PlayerWon == r2.PlayerWon && r.Turns == r2.Turns
                                    && string.Join(",", r.PlayerStarterFallen.OrderBy(x => x)) == string.Join(",", r2.PlayerStarterFallen.OrderBy(x => x));
                        var t1 = r.TallyByUnit["borg"]; var t2 = r2.TallyByUnit["borg"];
                        same &= t1.FireSplashNominal == t2.FireSplashNominal && t1.FireMendHealed == t2.FireMendHealed && t1.FireFeedHealed == t2.FireFeedHealed && t1.SelfKindleLit == t2.SelfKindleLit;
                        var y = new YAgg(); y.Take(r, p, e, slot0);
                        var ev = r.Events.Where(x => x.Kind == BattleEventKind.FireArmor).ToList();
                        bool evOk = ev.Count(x => x.Text == FireArmorLabels.Splash) == t1.FireSplashHits
                                    && ev.Count(x => x.Text == FireArmorLabels.Kindle) == t1.SelfKindleLit
                                    && ev.Where(x => x.Text == FireArmorLabels.Mend).Sum(x => (long)x.Amount) == t1.FireMendNominal
                                    && ev.Count(x => x.Text == FireArmorLabels.Feed) == t1.FireFeedFires;
                        lock (lk)
                        {
                            battles2++; if (!same) diff++;
                            deaths += y.X.A.DeathEvents;
                            var a = y.X.A;
                            if (a.Cause.Sum() != a.DeathEvents || a.FellSum != a.DeathEvents || y.SplashPhysDeaths + y.SplashBurnDeaths != a.Cause[BA.CBorgSplash]) deathMismatch++;
                            if (!evOk) evMismatch++;
                            evs2 += ev.Count;
                        }
                    });
        Expect($"verbose の有無で勝敗・決着T・倒れた駒・この期の帳簿が違う戦（{battles2} 戦）", diff, 0L);
        Expect($"死因の合計 ≠ 倒れた駒（巻き込みの物理 ＋ 燃焼 ≠ 巻き込み を含む）の戦（倒れた {deaths} 件）", deathMismatch, 0L);
        Expect($"台本の件数（燃える巻き込み・くすぶり・火の癒しの量・焼き返し）≠ 帳簿の戦（FireArmor の台本 {evs2} 件）", evMismatch, 0L);

        // 乱数: 新しい処理は Roll / PickOne を呼ばない（本文の走査）
        string src = File.ReadAllText(Path.Combine("BattleCore", "BattleEngine.cs")).Replace("\r\n", "\n");
        string tsrc = File.ReadAllText(Path.Combine("BattleCore", "Traits.cs")).Replace("\r\n", "\n");
        var spans = new List<string>();
        foreach (var (s0, a, b) in new[] { (src, "// 第235期 —— 燃える巻き込み（S）", "void PerformAttackFramed("), (tsrc, "public sealed class FireSplashTrait", "/// 熾火。") })
        {
            int i = s0.IndexOf(a, StringComparison.Ordinal), j = i < 0 ? -1 : s0.IndexOf(b, i, StringComparison.Ordinal);
            if (i < 0 || j < 0) throw new InvalidOperationException("走査の目印が見つからない（R034）: " + a);
            spans.Add(s0[i..j]);
        }
        Expect("新しい処理の本文に Roll( / PickOne( が無い（engine ／ 札の2区間）", spans.Count(s => s.Contains("Roll(") || s.Contains("PickOne(")), 0);

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {ok + ng}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
