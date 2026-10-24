using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S390 = Sh390Diag;

// sh400 check —— 第400期の自己検査（指示書 §6 の「新しい器具に最低限入れる」）。
static partial class Sh400Diag
{
    internal const string HeadSha = "a387e7c3";   // 第399期の HEAD
    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })!;
    static readonly FieldInfo TurnField = typeof(BattleContext).GetField("_turn", BindingFlags.Instance | BindingFlags.NonPublic)!;
    static readonly UnitDef Mate = new() { Id = "mate400", Name = "仲間", MaxHp = 50, Attack = 5, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }
    static bool Same(BattleResult a, BattleResult b) => a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;

    static void Check()
    {
        _fail = 0;
        Console.WriteLine($"# {Tag} sh400 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef t = UnitCatalog.Tomo, l1 = UnitCatalog.TomoL1, th = UnitCatalog.TomoH399, t3 = UnitCatalog.TomoT3;

        // ---- 段0 ----
        {
            bool cards = t.Traits.SequenceEqual(l1.Traits) && t.PlusText == l1.PlusText && t.MinusText == l1.MinusText && t.Name == l1.Name && t.Id == "tomo" && t.MaxHp == l1.MaxHp && t.Attack == l1.Attack && t.Speed == l1.Speed && t.Flavor == l1.Flavor
                && !ReferenceEquals(t, l1) && t.MinusText.Contains("灯した味方が前のターンに動けなかったら", StringComparison.Ordinal);
            bool old = th.Traits.SequenceEqual(t3.Traits) && th.PlusText == t3.PlusText && th.MinusText == t3.MinusText
                && UnitCatalog.All.Contains(t) && !UnitCatalog.Everyone.Contains(th) && new[] { l1, t3, UnitCatalog.DolgaX1, UnitCatalog.DolgaX2 }.All(v => !UnitCatalog.Everyone.Contains(v))
                && l1.Traits.SequenceEqual(th.Traits.Append(TraitId.TaillightIdleYield));
            bool pins = PinT399(t) == th && PinT399(UnitCatalog.Dolga) == UnitCatalog.Dolga && PinT393(th) == UnitCatalog.TomoH393 && PinT393(t) == UnitCatalog.TomoH393
                && Pin399(Presets.FourCore397()).Occupied().Any(o => o.Def == th);
            Expect("段0: 規定のトモ ＝ 第399期の L1（札 ／ 文面 ／ 名前 ／ 数値・別の定義）・旧は `TomoH399`（T3・`All` の外）・第399期の版の駒は旧から作ったまま・固定の口 `PinT399` ／ `Pin399`（`PinT393` は H399 も第393期までの旧へ）",
                cards && old && pins, $"規定のトモ {string.Join("・", t.Traits)}");
        }
        {
            long cells = 0, diff = 0;
            foreach (var (_, f) in Boards())
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
                    for (int sd = 0; sd < 10; sd++) { cells++; if (!Same(I.Raw(f, w, sd).R, I.Raw(Apply(f, l1), w, sd).R)) diff++; }
            Expect("段0: 規定は第399期の L1 と同じ動き（手番の軸の試遊の 5 台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 × seed 0..9 で勝敗 ／ T ／ 生存数の差 0）", diff == 0 && cells > 0, $"{cells} 戦・差 {diff}");
        }
        {
            var v = Presets.Versions; var h9 = VersionsH399();
            bool ok = !v.Any(r => r.Name == "版・目覚め ドルガ") && h9[^1].Name == "版・目覚め ドルガ" && h9[^1].F.Occupied().Any(o => o.Def == l1) && h9[^1].F.Occupied().Any(o => o.Def == UnitCatalog.DolgaX1)
                && !h9.Any(r => r.F.Occupied().Any(o => o.Def == t)) && v.First(r => r.Name == "試遊・手番 芯4枚").F.Occupied().Any(o => o.Def == t)
                && VersionsH398().Select(r => r.Name).Contains("版・耐える セッキ") && !VersionsH398().Any(r => r.Name == "版・目覚め ドルガ");
            Expect("段0: 試遊の台「版・目覚め ドルガ」を外した・「試遊・手番 芯4枚」は規定のトモ・第399期の一覧は `VersionsH399()`（その台を戻し、トモは旧）・`VersionsH398()` はそこから", ok, $"`Presets.Versions` {v.Length} 台");
        }

        // ---- 出来事 ----
        {
            // 台 × 物差し × seed 0..9 で、足した出来事が出るか・帳簿の回数と合うか
            var notes = new List<string>(); bool ok = true;
            var cnt = new Dictionary<string, long>();
            long wrapT = 0, burstT = 0, takeT = 0, yieldT = 0, lightT = 0, wrapE = 0, burstE = 0, takeE = 0, yieldE = 0, lightE = 0;
            foreach (var (_, f) in Boards())
                foreach (var w in new[] { I.Boss, I.Guard, I.M5 })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var (r, id) = One(f, w, sd);
                        foreach (var x in r.Events)
                            if (x.Kind is BattleEventKind.Slime or BattleEventKind.Wall or BattleEventKind.Lamp)
                            {
                                string key = $"{x.Kind}:{x.Text}{(x.Kind == BattleEventKind.Lamp && x.Text == LampLabels.Yield ? (x.Slot == LampLabels.ForIdle ? "・止まっていた" : "・倒れた") : "")}";
                                cnt[key] = cnt.GetValueOrDefault(key) + 1;
                            }
                        wrapE += r.Events.Count(x => x.Kind == BattleEventKind.Slime && x.Text == SlimeLabels.Wrap);
                        burstE += r.Events.Count(x => x.Kind == BattleEventKind.Slime && x.Text == SlimeLabels.Burst);
                        takeE += r.Events.Count(x => x.Kind == BattleEventKind.Wall && x.Text == WallLabels.Take);
                        yieldE += r.Events.Count(x => x.Kind == BattleEventKind.Lamp && x.Text == LampLabels.Yield);
                        lightE += r.Events.Count(x => x.Kind == BattleEventKind.Lamp && x.Text == LampLabels.Light);
                        foreach (var (uid, tl) in r.TallyByUnit)
                        {
                            if (uid == "sekki") { wrapT += tl.R396WrapHits; burstT += tl.R396Bursts; }
                            if (uid == "dolga") takeT += tl.R394WallHits;
                            if (uid == "tomo") { yieldT += tl.TaillightYields; lightT += tl.TaillightFires; }
                        }
                    }
            // 「消える」は規定のトモ（灯を外さない）では灯した味方が倒れたときだけ——台の上ではまれなので、下の1手の盤面で確かめる
            string[] need = { $"Slime:{SlimeLabels.Wrap}", $"Slime:{SlimeLabels.Swell}", $"Slime:{SlimeLabels.Burst}", $"Slime:{SlimeLabels.Idle}", $"Wall:{WallLabels.Rest}", $"Wall:{WallLabels.Take}", $"Wall:{WallLabels.Down}", $"Wall:{WallLabels.Focus}", $"Wall:{WallLabels.Heavy}",
                $"Lamp:{LampLabels.Light}", $"Lamp:{LampLabels.Yield}・止まっていた", $"Lamp:{LampLabels.Yield}・倒れた" };
            var missing = need.Where(k => cnt.GetValueOrDefault(k) == 0).ToList();
            ok &= missing.Count == 0; notes.Add(missing.Count == 0 ? "どの札も出た" : "出ない: " + string.Join("・", missing));
            bool eq = wrapE == wrapT && burstE == burstT && takeE == takeT && yieldE == yieldT && lightE == lightT && wrapE > 0;
            ok &= eq; notes.Add($"帳簿と同じ回数（包む {wrapE} ／ {wrapT}・弾き返す {burstE} ／ {burstT}・受ける {takeE} ／ {takeT}・譲る {yieldE} ／ {yieldT}・灯す {lightE} ／ {lightT}）{(eq ? "" : " ×")}");
            notes.Add(string.Join("・", cnt.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}")));
            Expect("出来事: 足した札（`Slime` 包む ／ 膨らむ ／ 弾き返す ／ ぷるぷる・`Wall` 構える ／ 受ける ／ 下がる ／ 集める ／ 重い・`Lamp` 灯す ／ 消える ／ 譲る（倒れた ／ 止まっていた））が台本に出る・回数が帳簿と合う（5 台 × ボス ／ 近衛 ／ 第5波 × seed 0..9）", ok, string.Join("・", notes));
        }
        {
            // 並び: 包む → セッキへの Damage（Relayed）→ 味方への Damage ／ 受ける → ドルガへの Damage（Relayed）／ 譲る → 譲られた味方の手番
            var (r, id) = One(Presets.FourCore397(), I.Boss, 0);
            var ev = r.Events.ToList();
            int iw = ev.FindIndex(x => x.Kind == BattleEventKind.Slime && x.Text == SlimeLabels.Wrap);
            bool a = iw >= 0 && ev.Skip(iw + 1).FirstOrDefault(x => x.Kind == BattleEventKind.Damage) is { } d1 && d1.TargetId == ev[iw].ActorId && d1.Relayed;
            int iy = ev.FindIndex(x => x.Kind == BattleEventKind.Lamp && x.Text == LampLabels.Yield);
            bool b = iy >= 0 && ev.Skip(iy + 1).FirstOrDefault(x => x.Kind is BattleEventKind.Attack or BattleEventKind.Slime && x.ActorId == ev[iy].TargetId) is not null;
            var (r2, _) = One(Presets.BindTempo395(), I.Guard, 0);
            var ev2 = r2.Events.ToList();
            int it = ev2.FindIndex(x => x.Kind == BattleEventKind.Wall && x.Text == WallLabels.Take);
            bool c = it >= 0 && ev2.Skip(it + 1).FirstOrDefault(x => x.Kind == BattleEventKind.Damage) is { } d2 && d2.TargetId == ev2[it].ActorId && d2.Relayed;
            Expect("出来事の並び: 「包む」の直後にセッキへの `Damage`（`Relayed`）・「譲る」の後に譲られた味方の手番・「受ける」の直後にドルガへの `Damage`（`Relayed`）", a && b && c, $"包む {(a ? "○" : "×")}・譲る {(b ? "○" : "×")}・受ける {(c ? "○" : "×")}");
        }

        {
            // 灯が消える（1手の盤面）: 灯していた仲間が倒れた次のターンの頭 → 「消える」（相手が変わった）→ 次の相手に「灯す」
            var ctx = new BattleContext(0, true);
            var f = new Formation(); f[0] = Mate; f[1] = UnitCatalog.Dolga; f[3] = t;
            var g = new Formation(); g[0] = new UnitDef { Id = "foe400", Name = "的", MaxHp = 1000, Attack = 1, Speed = 1, Traits = Array.Empty<TraitId>(), PlusText = "" };
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam); var e = BattleEngine.Materialize(g, BattleContext.EnemyTeam, EnemyScaleRule.None);
            foreach (var u in p.Concat(e)) AddUnit.Invoke(ctx, new object[] { u });
            TurnField.SetValue(ctx, 2);
            var tm = p.First(u => u.Def.Id == "tomo"); var mate = p.First(u => u.Def.Id == "mate400");
            tm.SetCounter(TaillightTrait.TargetKey, mate.InstanceId + 1); tm.SetCounter(TaillightTrait.LitKey, 10); mate.AtkBonus = 10; mate.Hp = 0;
            tm.Traits.First(x => x.Id == TraitId.Taillight).OnTurnStart(ctx, tm);
            var lamp = ctx.Events.Where(x => x.Kind == BattleEventKind.Lamp).ToList();
            bool ok = lamp.Count == 2 && lamp[0].Text == LampLabels.Douse && lamp[0].TargetId == mate.InstanceId && lamp[0].Amount == 10 && lamp[0].Slot == LampLabels.Switched
                && lamp[1].Text == LampLabels.Light && lamp[1].TargetId == p.First(u => u.Def.Id == "dolga").InstanceId;
            Expect("出来事（1手の盤面）: 灯していた仲間が倒れた次のターンの頭に「消える」（相手が変わった・消えた量 10）→ 次の相手に「灯す」", ok, string.Join("・", lamp.Select(x => $"{x.Text}（{x.Amount}・{x.Slot}）")));
        }

        // ---- 盤面を変えない ----
        {
            var map = S390.BalanceMap(File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "");
            var rows = CompareBuilds(); var g = CGrid(rows, CompareSeeds);
            int diff = 0, miss = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!map.TryGetValue(rows[ri].Name, out var cc)) { miss++; continue; }
                for (int w = 0; w < 5; w++) if (cc[w].TrimEnd('%') != F1(g[ri, w])) diff++;
            }
            Expect("盤面を変えない: 規定の `compare` 全行 × 5 波 × seed 0..199 が `docs/balance.md` と一致", diff == 0 && miss == 0, $"{rows.Length} 行・ずれ {diff}・表に無い行 {miss}");
        }
        {
            long diff = 0, det = 0, cells = 0;
            foreach (var (_, f) in Boards())
                foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5, I.StrongWaves[0] })
                    for (int sd = 0; sd < 4; sd++)
                    {
                        cells++;
                        var r0 = I.Raw(f, w, sd).R;
                        if (!Same(r0, I.Raw(f, w, sd, verbose: true).R)) diff++;
                        if (!Same(r0, I.Raw(f, w, sd).R)) det++;
                    }
            Expect("盤面を変えない: verbose の有無で勝敗 ／ 決着T ／ 生存数が同じ・同じ seed で同じ結果（5 台 × ボス ／ 近衛 ／ 大隊 ／ 第5波 ／ 強い条件 × seed 0..3）", diff == 0 && det == 0, $"{cells} 戦・verbose の差 {diff}・再現の差 {det}");
        }

        // ---- checkup ----
        {
            string src = File.ReadAllText("BattleSim/Modes/Checkup.cs");
            bool ok = System.Text.RegularExpressions.Regex.IsMatch(src, @"\[TraitId\.TaillightIdleYield\]\s*=\s*\(Hc[A-Za-z]+L,\s*""[^""]*第400期から規定（トモ）""");
            Expect("`checkup` の札の分類表: 規定になった L1 の札は「第400期から規定（トモ）」（ソースを読んで確かめる）", ok);
        }

        // ---- PickOne ／ Roll ----
        {
            int Now(string f, string tok) => File.ReadAllText(f).Split(tok).Length - 1;
            string he = S390.GitShow($"{HeadSha}:BattleCore/BattleEngine.cs"), ht = S390.GitShow($"{HeadSha}:BattleCore/Traits.cs");
            int e0 = he.Split("PickOne(").Length - 1, tr0 = ht.Split("PickOne(").Length - 1, r0 = ht.Split("Roll(").Length - 1, re0 = he.Split("Roll(").Length - 1;
            int e1n = Now("BattleCore/BattleEngine.cs", "PickOne("), tr1 = Now("BattleCore/Traits.cs", "PickOne("), r1 = Now("BattleCore/Traits.cs", "Roll("), re1 = Now("BattleCore/BattleEngine.cs", "Roll(");
            Expect($"`ctx.PickOne` ／ `Roll(` を新たに使っていない（BattleEngine.cs ／ Traits.cs の数が第399期の HEAD（{HeadSha}）と同じ）", e0 == e1n && tr0 == tr1 && r0 == r1 && re0 == re1 && e0 > 0,
                $"PickOne engine {e0} → {e1n}・traits {tr0} → {tr1}・Roll traits {r0} → {r1} ／ engine {re0} → {re1}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
