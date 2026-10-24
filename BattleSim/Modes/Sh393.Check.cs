using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh393 check —— 第393期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh393Diag
{
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly MethodInfo TallyOfM = typeof(BattleContext).GetMethod("TallyOf", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal const string HeadSha = "0a430eb5";   // 第392期の HEAD
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
    static UnitTally Tl(BattleContext c, UnitState u) => (UnitTally)TallyOfM.Invoke(c, new object[] { u })!;
    static readonly UnitDef Mate = new() { Id = "mate393", Name = "仲間", MaxHp = 50, Attack = 5, Speed = 8, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static readonly UnitDef Foe = new() { Id = "foe393", Name = "的", MaxHp = 1000, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };

    /// <summary>トモ（版）・ドルガ・仲間（速 8）・（あれば）ガン ／ バン と的 1 体。ターン <paramref name="turn"/>。</summary>
    static (BattleContext C, UnitState T, UnitState D, UnitState M, List<UnitState> P) Bench(UnitDef tomo, int turn, bool gan = false, bool ban = false)
    {
        var ctx = new BattleContext(0, true);
        var f = new Formation(); f[4] = tomo; f[0] = UnitCatalog.DolgaH395; f[1] = Mate;   // 第396期: ドルガは第395期までの規定に固定
        if (gan) f[2] = UnitCatalog.Gan;
        if (ban) f[3] = UnitCatalog.Ban;
        var g = new Formation(); g[0] = Foe;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
        TurnField.SetValue(ctx, turn);
        return (ctx, p.First(u => u.Def.Id == "tomo"), p.First(u => u.Def.Id == "dolga"), p.First(u => u.Def.Id == "mate393"), p);
    }
    static Trait TL(UnitState t) => t.Traits.First(x => x.Id == TraitId.Taillight);
    static int Lit(UnitState t) => t.RawCounter(TaillightTrait.LitKey);
    static int LitTarget(UnitState t) => t.RawCounter(TaillightTrait.TargetKey) - 1;

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh393 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef t0 = UnitCatalog.TomoH393, t1 = UnitCatalog.TomoT1, t2 = UnitCatalog.TomoT2, t3 = UnitCatalog.TomoT3;

        // ---- 版の札と文面 ----
        {
            bool ok = new[] { t1, t2, t3 }.All(d => d.Id == "tomo" && d.Name == t0.Name && d.MaxHp == t0.MaxHp && d.Attack == 0 && d.Speed == 3 && d.Actions == t0.Actions && d.Traits[0] == TraitId.Taillight && !UnitCatalog.Everyone.Contains(d))
                && t1.Traits.SequenceEqual(new[] { TraitId.Taillight, TraitId.TaillightKeep }) && t2.Traits.SequenceEqual(new[] { TraitId.Taillight, TraitId.TaillightKeep, TraitId.TaillightWake })
                && t3.Traits.SequenceEqual(new[] { TraitId.Taillight, TraitId.TaillightKeep, TraitId.TaillightWake, TraitId.TaillightCarry })
                && t1.PlusText.EndsWith("休んでいる味方からも灯は外れない", StringComparison.Ordinal) && t2.MinusText.Contains("灯した味方に手番を譲る（休んでいても叩き起こす）", StringComparison.Ordinal)
                && t3.PlusText.EndsWith("自分に積まれた力は、灯に乗せて灯した味方へ渡す", StringComparison.Ordinal) && t0.Traits.SequenceEqual(new[] { TraitId.Taillight })
                && !UnitCatalog.Everyone.Where(d => d != UnitCatalog.Tomo).Any(d => d.Traits.Contains(TraitId.TaillightKeep) || d.Traits.Contains(TraitId.TaillightWake) || d.Traits.Contains(TraitId.TaillightCarry));
            Expect("版の札と文面: T1 ＝ 規定 ＋ 灯を外さない ／ T2 ＋ 灯が起こす ／ T3 ＋ 灯が力を運ぶ（積み上げ）・文面は叩き台・`All` ／ `Retired` の外・規定のトモは今どおり", ok,
                string.Join(" ／ ", new[] { ("T1", t1), ("T2", t2), ("T3", t3) }.Select(x => $"{x.Item1} {string.Join("・", x.Item2.Traits.Skip(1))}")));
        }

        // ---- T1 ----
        {
            var notes = new List<string>(); bool ok = true;
            foreach (var (name, d, keep) in new[] { ("規定", t0, false), ("T1", t1, true) })
            {
                var (c, t, dl, m, _) = Bench(d, 1);
                TL(t).OnTurnStart(c, t);                                 // T1（奇数）: ドルガ（速 6）に灯 5
                bool a1 = LitTarget(t) == dl.InstanceId && dl.AtkBonus == 5;
                TurnField.SetValue(c, 2); TL(t).OnTurnStart(c, t);       // T2（偶数・ドルガの休み番）
                bool a2 = keep ? LitTarget(t) == dl.InstanceId && dl.AtkBonus == 10 && Lit(t) == 10 : LitTarget(t) == m.InstanceId && dl.AtkBonus == 0;
                TurnField.SetValue(c, 3); dl.SetCounter(StatusKeys.Stun, 1); TL(t).OnTurnStart(c, t);   // T3 痺れていても外れない（W2 は痺れを見ない）
                bool a3 = !keep || (LitTarget(t) == dl.InstanceId && dl.AtkBonus == 15);
                ok &= a1 && a2 && a3; notes.Add($"{name}: T2 の灯 {(keep ? "ドルガに 10" : "仲間へ跳ぶ・ドルガ 0")}{(a1 && a2 && a3 ? "" : " ×")}");
            }
            // W1（恒久的に殴らない駒）は今どおり飛ばす: バン（速 2・術だけ）がいても灯はドルガへ
            var (c2, tt, dd, _, _) = Bench(t1, 1, ban: true); TL(tt).OnTurnStart(c2, tt);
            bool w1 = LitTarget(tt) == dd.InstanceId;
            ok &= w1; notes.Add($"W1: 術だけのバン（速 2）は飛ばす{(w1 ? "" : " ×")}");
            Expect("T1: 休み番 ／ 痺れの味方から灯が外れない・W1 の駒は今どおり飛ばす", ok, string.Join("・", notes));
        }

        // ---- T2 ----
        {
            var notes = new List<string>(); bool ok = true;
            (UnitTally T, UnitState D, int Foe) Yield(UnitDef d, bool stun = false, bool yielding = false, bool gan = false)
            {
                var (c, t, dl, _, p) = Bench(d, 1, gan: gan);
                TL(t).OnTurnStart(c, t);                                 // T1 にドルガへ灯
                TurnField.SetValue(c, 2); TL(t).OnTurnStart(c, t);       // T2（T1 ／ T2 は灯が外れない）
                if (!ReferenceEquals(d, t0)) { }
                dl.SetCounter(StatusKeys.IdleTurn, 2);                   // ドルガの休み番（手番が潰れた）
                if (stun) dl.SetCounter(StatusKeys.Stun, 1);
                t.SetCounter(TaillightTrait.SawKey, 2);                  // このターンに敵が倒れていた
                c.Yielding = yielding;
                var foe = c.LivingMembers(BattleContext.EnemyTeam)[0]; int hp0 = foe.Hp;
                TL(t).OnAction(c, t, t.Def.Actions![0]);
                return (Tl(c, t), dl, hp0 - foe.Hp);
            }
            var a = Yield(t2); var b = Yield(t1);
            bool g1 = a.T.TaillightYields == 1 && a.T.TaillightYieldAttack == 1 && a.T.R393Wakes == 1 && a.Foe > 0 && a.D.RawCounter(StatusKeys.IdleTurn) == 0 && a.T.R393WakeClearedIdle == 1
                   && b.T.TaillightYields == 1 && b.T.TaillightYieldStalls == 1 && b.Foe == 0 && b.T.R393Wakes == 0;
            ok &= g1; notes.Add($"休み番のドルガに譲ると振る（T2 与ダメ {a.Foe}・`IdleTurn` を消す）／ T1 は潰れる{(g1 ? "" : " ×")}");
            var s = Yield(t2, stun: true);
            bool g2 = s.T.TaillightYieldStalls == 1 && s.Foe == 0 && s.T.R393Wakes == 0;
            ok &= g2; notes.Add($"痺れのドルガは起こせない{(g2 ? "" : " ×")}");
            var h = Yield(t2, yielding: true);
            bool g3 = h.T.TaillightYields == 0 && h.T.TaillightBlockedHop == 1;
            ok &= g3; notes.Add($"1ホップ（譲った手番の中では譲らない）{(g3 ? "" : " ×")}");
            var gn = Yield(t2, gan: true);
            bool g4 = gn.T.R393WakeAteRally == 1;
            ok &= g4; notes.Add($"ガンがいれば食い合いに数える{(g4 ? "" : " ×")}");
            // 粛: トモの譲りは自分の手番の中なので粛の窓口を通らない（今どおり）——ここでは数えない
            Expect("T2: 休み番のドルガにトモが譲ると振る・痺れの相手は今どおり起こせない・1ホップ（1ターン1回はトモの手番が1回なので構造で立つ）", ok, string.Join("・", notes));
        }

        // ---- T3 ----
        {
            var notes = new List<string>(); bool ok = true;
            // ① ガンの +8 をトモが受ける（T2 以降）・規定 ／ T2 は受けない・叩き起こしの候補には入らない
            int Rally(UnitDef d, out UnitTally tl, out bool woke)
            {
                var (c, t, dl, _, p) = Bench(d, 2, gan: true);
                var gan = p.First(u => u.Def.Id == "gan");
                int a0 = t.AtkBonus;
                gan.Traits.First(x => x.Id == TraitId.Rally).OnTurnStart(c, gan);
                tl = Tl(c, t);
                var foe = c.LivingMembers(BattleContext.EnemyTeam)[0];
                gan.Traits.First(x => x.Id == TraitId.Reveille).OnAfterAttack(c, gan, foe, 1);
                woke = Tl(c, t).ReveilleWoken > 0;
                return t.AtkBonus - a0;
            }
            int r3 = Rally(t3, out var tl3, out bool w3), r2 = Rally(t2, out _, out _), r0 = Rally(t0, out _, out _);
            bool g1 = r3 == RallyTrait.Gain && tl3.R393RallyToLamp == 1 && r2 == 0 && r0 == 0 && !w3;
            ok &= g1; notes.Add($"T3 のトモはガンの +8 を受ける（{r3}・T2 ／ 規定 {r2} ／ {r0}）・叩き起こしの候補に入らない{(g1 ? "" : " ×")}");
            // ② トモに積まれた力が灯に乗って移る（写しではない）・消えるとき引くのは積んだ灯の累計
            var (c, t, dl, m, _) = Bench(t3, 1);
            t.AtkBonus = 12;
            TL(t).OnTurnStart(c, t);
            bool g2 = dl.AtkBonus == 17 && t.AtkBonus == 0 && Lit(t) == 17 && Tl(c, t).R393Carried == 12;
            dl.Hp = 0;                                                    // 対象が倒れた → 次のターンに仲間へ替わって消える
            TurnField.SetValue(c, 2); TL(t).OnTurnStart(c, t);
            bool g3 = dl.AtkBonus == 0 && LitTarget(t) == m.InstanceId && m.AtkBonus == 5 && Lit(t) == 5;
            ok &= g2 && g3; notes.Add($"積まれた 12 が灯に乗って 17（トモは 0）・消えると 17 を引く{(g2 && g3 ? "" : $" × {dl.AtkBonus} {t.AtkBonus} {Lit(t)}")}");
            Expect("T3: トモがガンの +8 を受ける（叩き起こしの候補には入らない）・トモに積まれた力が灯に乗って移り、トモから引かれる・灯が消えるとき引くのは積んだ灯の累計", ok, string.Join("・", notes));
        }

        // ---- 規定のトモ ／ ガンは今どおり ----
        {
            var map = S390.BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var cc)) { miss++; continue; }
                for (int w = 0; w < 5; w++) if (cc[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            Expect("規定のトモ ／ ガンは今どおり（規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致・どちらもいない行も）", diff == 0 && miss == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}");
        }

        // ---- 替えた台 ／ 素体は器具の中だけ・試遊の台 ----
        {
            var vers = new[] { t1, t2, t3 };
            var everywhere = CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Concat(VersionsH393()).SelectMany(r => r.F.Occupied()).Select(o => o.Def).ToList();
            bool inside = !everywhere.Any(d => d.Id.EndsWith("_plain392", StringComparison.Ordinal));
            var verRows = VersionsH393().Where(r => r.F.Occupied().Any(o => vers.Contains(o.Def))).Select(r => r.Name).ToList();
            bool rows = verRows.SequenceEqual(new[] { "版・灯 手番" }) && VersionsH393()[^1].Name == "版・灯 手番" && VersionsH393()[^1].F.Occupied().Count(o => vers.Contains(o.Def)) == 1
                && !CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Playtest).Any(r => r.F.Occupied().Any(o => vers.Contains(o.Def)))
                && VersionsH392().Select(r => r.Name).SequenceEqual(VersionsH393().Select(r => r.Name).SkipLast(1));
            Expect("替えた台 ／ 素体は器具の中だけ・版のトモが居る試遊の台は「版・灯 手番」（`VersionsH393()` の末尾）だけ・第392期の一覧は `VersionsH392`", inside && rows, string.Join("、", verRows));
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            var miss = new[] { TraitId.TaillightKeep, TraitId.TaillightWake, TraitId.TaillightCarry }.Where(t => !System.Text.RegularExpressions.Regex.IsMatch(src, $@"\[TraitId\.{t}\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第393期[^""]*(保持者 0 枚|第394期から規定（トモ）)""")).ToList();
            Expect("`checkup` の札の分類表に第393期の札 3 枚（灯を外さない ／ 灯が起こす ／ 灯が力を運ぶ）がある（ソースを読んで確かめる）", miss.Count == 0, miss.Count == 0 ? "" : "無い: " + string.Join("・", miss));
        }

        // ---- verbose ／ 再現性 ----
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var b in BoardList())
                foreach (var v in Vers)
                    foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                        for (int sd = 0; sd < 2; sd++)
                        {
                            cells++;
                            var f = Put(b.F, b.Slot, v.D);
                            var r0 = I.Raw(f, w, sd).R;
                            if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                            if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                        }
            Expect("verbose の有無で勝敗 ／ T ／ 生存数が同じ・同じ seed で同じ結果（台 5 × 版 4 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..1）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, tr0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1 = Now("BattleCore/BattleEngine.cs", "PickOne("), tr1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第392期の HEAD（{HeadSha}）と同じ）", e0 == e1 && tr0 == tr1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1}・traits {tr0} → {tr1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
