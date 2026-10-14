using System.Reflection;
using BattleCore;
using static Common;
using T323 = Tou323Diag;

// kata342 の自己検査（第342期）。盤を直に組み、連鎖の後の口（`AfterChain`）と起爆（`ShockTrigger`）を反射で呼ぶ（`T323.Ctx`）。
static partial class Kata342Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly UnitDef Dummy = new()
    {
        Id = "dummy342", Name = "木偶", MaxHp = 5000, Attack = 1, Speed = 1, Pattern = AttackPattern.Single, Traits = Array.Empty<TraitId>(),
        PlusText = "", MinusText = "", Flavor = "",
    };
    static readonly Formation Foes = Formation.Build(front1: Dummy, front3: Dummy, center: Dummy, back1: Dummy, back3: Dummy);
    const BindingFlags NP = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo AfterChainM = typeof(BattleContext).GetMethod("AfterChain", NP) ?? throw new InvalidOperationException("AfterChain が見つからない");
    static readonly MethodInfo TriggerM = typeof(BattleContext).GetMethod("ShockTrigger", NP) ?? throw new InvalidOperationException("ShockTrigger が見つからない");

    /// <summary>カタ（前1）＋ 壁の味方（前3 ／ 中央 ／ 後1・カタの隣）。敵は木偶 5 体。</summary>
    static (BattleContext Ctx, UnitState Kata, List<UnitState> P, List<UnitState> E) Board(UnitDef kata)
    {
        var ctx = T323.Ctx(Formation.Build(front1: kata, front3: T323.Mate, center: T323.Mate, back1: T323.Mate), Foes, out var p, out var e);
        return (ctx, T323.Seat(p, 0), p, Enumerable.Range(0, 5).Select(i => T323.Seat(e, i)).ToList());
    }
    static void Trigger(BattleContext ctx, UnitState u, UnitState? ini) => TriggerM.Invoke(ctx, new object?[] { u, ini, 0, null });
    static int Cloud(UnitState k) => ThundercloudTrait.Of(k);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# kata342 check —— 第342期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義と試遊の台
        {
            UnitDef K = UnitCatalog.Kata;
            bool def = UnitCatalog.KataKTb.Traits.SequenceEqual(K.Traits.Append(TraitId.ThundercloudAllyPops))
                && UnitCatalog.KataKTr1.Traits.SequenceEqual(K.Traits.Append(TraitId.ThundercloudAllyPops).Append(TraitId.KataRod))
                && UnitCatalog.KataKTrJ.Traits.SequenceEqual(K.Traits.Append(TraitId.ThundercloudAllyPops).Append(TraitId.KataRod).Append(TraitId.KataRodChain))
                && UnitCatalog.KataKTb.PlusText == K.PlusText + "。味方の感電が弾けても雷雲は育つ" && UnitCatalog.KataKTb.MinusText == K.MinusText
                && UnitCatalog.KataKTr1.PlusText.EndsWith("。感電した身で殴られると、雷は避雷針を伝って敵に落ちる（自分は痺れない）", StringComparison.Ordinal)
                && UnitCatalog.KataKTr1.MinusText.Contains("その味方（自分を除く）が半々の確率で痺れる", StringComparison.Ordinal) && UnitCatalog.KataKTr1.MinusText.Contains("雷を落とすたび、隣の味方すべてに感電が付く", StringComparison.Ordinal)
                && new[] { UnitCatalog.KataKTb, UnitCatalog.KataKTr1, UnitCatalog.KataKTrJ }.All(d => !UnitCatalog.Everyone.Contains(d))
                && UnitCatalog.Everyone.All(d => !d.Traits.Any(t => t is TraitId.ThundercloudAllyPops or TraitId.KataRod or TraitId.KataRodChain));
            Expect("版の定義: KT-b ＝ 規定のカタ ＋ `ThundercloudAllyPops`・避雷針 ＝ ＋ `KataRod`（跳ねる ＝ ＋ `KataRodChain`）・文面は叩き台（マイナスの痺れの句は「自分を除く」）・`Everyone` の外・保持者 0", def);
            var v = Presets.Versions; var pon = v.First(r => r.Name == PonRow).F;
            (string Name, UnitDef Kata)[] want = { ("版・KT-b ポンの台", UnitCatalog.KataKTb), ("版・避雷針1発 ポンの台", UnitCatalog.KataKTr1), ("版・避雷針跳ね ポンの台", UnitCatalog.KataKTrJ) };
            bool rows = v[0].Name == PonRow && v.Any(r => r.Name == "試遊・ミサ 書き手なし") && Presets.Playtest.Length == 17
                && want.All(x => v.Any(r => r.Name == x.Name && r.F.Occupied().Select(o => o.Def).SequenceEqual(pon.Occupied().Select(o => o.Def.Id == "kata" ? x.Kata : o.Def))
                    && r.F.Occupied().Select(o => o.Slot).SequenceEqual(pon.Occupied().Select(o => o.Slot))));
            Expect("試遊の台: `Presets.Versions` ＝ 「試遊・感電 ポンの台」「試遊・ミサ 書き手なし」＋「版・KT-b ／ 避雷針1発 ／ 避雷針跳ね ポンの台」（ポンの台の並びでカタだけ版）・`Playtest` は 17 台のまま", rows);
        }

        // ---- KT-b: 味方（カタ自身を含む）の弾けも雷雲 +1・規定では敵だけ
        {
            int Grow(UnitDef d, bool ally, bool self)
            {
                var (ctx, k, p, e) = Board(d);
                var popped = new List<UnitState> { ally ? (self ? k : p[1]) : e[0] };
                int c0 = Cloud(k);
                AfterChainM.Invoke(ctx, new object?[] { popped[0].TeamId, popped, null });
                return Cloud(k) - c0;
            }
            int bA = Grow(UnitCatalog.KataKTb, true, false), bS = Grow(UnitCatalog.KataKTb, true, true), bE = Grow(UnitCatalog.KataKTb, false, false);
            int dA = Grow(UnitCatalog.Kata, true, false), dS = Grow(UnitCatalog.Kata, true, true), dE = Grow(UnitCatalog.Kata, false, false);
            Expect("KT-b: 味方の弾けでも ／ カタ自身の弾けでも雷雲 +1（敵の弾けも +1）・規定は敵の弾けだけ +1", bA == 1 && bS == 1 && bE == 1 && dA == 0 && dS == 0 && dE == 1,
                $"KT-b 味方 {bA} ／ 自身 {bS} ／ 敵 {bE}・規定 味方 {dA} ／ 自身 {dS} ／ 敵 {dE}");
        }

        // ---- 避雷針（1発）: 殴った敵へ1発・隣の味方へ放電しない・雷雲を消費しない・痺れない ／ 萎縮しない・当たった敵に感電・雷は感電を弾けさせない
        {
            var (ctx, k, p, e) = Board(UnitCatalog.KataKTr1);
            k.SetCounter(ThundercloudTrait.Key, 3);
            k.SetCounter(StatusKeys.Shock, 1);
            e[2].SetCounter(StatusKeys.Shock, 1);   // 殴った敵は感電している（雷で弾けないことを見る）
            var hp0 = p.Concat(e).ToDictionary(u => u, u => u.Hp);
            int kinds = ThunderTrait.KindsOf(e[2]);
            Trigger(ctx, k, e[2]);
            int want = k.CurrentAttack * (1 + kinds + 3);
            bool allyClean = p.Where(u => u != k).All(u => u.Hp == hp0[u]);
            bool hit = hp0[e[2]] - e[2].Hp == want && e.Where(u => u != e[2]).All(u => u.Hp == hp0[u]);
            var t = T323.Tal(ctx, "kata");
            bool ok = allyClean && hit && Cloud(k) == 3 + 1 && k.RawCounter(StatusKeys.Stun) == 0   // 逆雷は雷雲 3 で落ち（消費しない）、連鎖の後に KT-b がカタ自身の弾けで +1 && k.RawCounter(StatusKeys.Daunted) == 0 && k.RawCounter(StatusKeys.Shock) == 0
                && e[2].RawCounter(StatusKeys.Shock) > 0 && t.Kt342RodFires == 1 && t.Kt342RodOnAttacker == 1 && t.Kt342RodHits == 1;
            Expect("避雷針: カタの感電が弾けると隣の味方へ放電せず、殴った敵へ 1 発（攻 ×（1 ＋ 種類 ＋ 雷雲））・雷雲は消費しない（連鎖の後に KT-b がカタ自身の弾けで +1 ＝ 3 → 4）・カタは痺れない ／ 萎縮しない・当たった敵の感電は弾けずに残る", ok,
                $"殴った敵の減り {hp0[e[2]] - e[2].Hp}（期待 {want}）・味方の減り {(allyClean ? 0 : 1)}・雷雲 {Cloud(k)}・痺れ {k.RawCounter(StatusKeys.Stun)}・萎縮 {k.RawCounter(StatusKeys.Daunted)}・敵の感電 {e[2].RawCounter(StatusKeys.Shock)}");

            // 殴った敵がいない（刻み・味方の一撃）: いつもの最初の的（帯びた敵・種類が多い順）
            var (ctx2, k2, p2, e2) = Board(UnitCatalog.KataKTr1);
            k2.SetCounter(StatusKeys.Shock, 1);
            e2[1].SetCounter(StatusKeys.Poison, 3); e2[1].SetCounter(StatusKeys.Burn, 2);
            var h2 = e2.ToDictionary(u => u, u => u.Hp);
            Trigger(ctx2, k2, p2[1]);
            bool pick = h2[e2[1]] > e2[1].Hp && e2.Where(u => u != e2[1]).All(u => u.Hp == h2[u]) && T323.Tal(ctx2, "kata").Kt342RodOnAttacker == 0;
            Expect("避雷針: 殴った敵がいなければ、手番の雷と同じ選び方の最初の的（帯びた種類の最も多い敵）へ", pick, $"的の減り {h2[e2[1]] - e2[1].Hp}");

            // 対照: KT-b（避雷針なし）は隣の味方へ放電する
            var (ctx3, k3, p3, _) = Board(UnitCatalog.KataKTb);
            k3.SetCounter(StatusKeys.Shock, 1);
            var h3 = p3.ToDictionary(u => u, u => u.Hp);
            Trigger(ctx3, k3, null);
            Expect("対照: 避雷針の無いカタ（KT-b）は弾けると隣の味方へ放電する", p3.Where(u => u != k3).Any(u => u.Hp < h3[u]), "");
        }

        // ---- 避雷針（跳ねる）: 逆雷が帯びた隣の敵へ跳ねる
        {
            var (ctx, k, _, e) = Board(UnitCatalog.KataKTrJ);
            k.SetCounter(StatusKeys.Shock, 1);
            var nb = Enumerable.Range(1, 4).Where(i => FormationRules.AreAdjacent(e[0], e[i])).ToArray();
            foreach (int i in nb) e[i].SetCounter(StatusKeys.Poison, 2);
            var h = e.ToDictionary(u => u, u => u.Hp);
            Trigger(ctx, k, e[0]);
            var t = T323.Tal(ctx, "kata");
            int struck = e.Count(u => u.Hp < h[u]);
            Expect("避雷針（跳ねる）: 逆雷が殴った敵から、帯びた隣の敵へ跳ねる（1発の版は跳ねない）", nb.Length > 0 && t.Kt342RodHits >= 2 && struck == t.Kt342RodHits && h[e[0]] > e[0].Hp,
                $"当たった延べ {t.Kt342RodHits}・減った敵 {struck}・隣の帯びた敵 {nb.Length}");
        }

        // ---- verbose の有無で勝敗 ／ 決着T が同じ（代表台 4 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 3 版）
        {
            int diff = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, f) in Boards())
                    foreach (var w in new[] { Waves[0], Waves[1], Waves[2] })
                        for (int sd = 0; sd < 10; sd++)
                        {
                            var fv = Apply(f, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            n++;
                            if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        }
            Expect($"代表台 4 × ボス ／ 近衛 ／ 大隊 × seed 0..9 × 規定 ＋ 3 版（{n} 戦）: verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"違い {diff}");
        }

        // ---- 乱数を新たに引いていない
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show ffdefd25:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が第341期（ffdefd25）と同じ）", engine == he && traits == ht && he > 0, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
