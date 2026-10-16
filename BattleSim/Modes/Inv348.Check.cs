using BattleCore;
using static Common;
using FC = FireCycleDiag;

// inv348 の自己検査（第348期）: 器具の有無で勝敗 ／ 台本が変わらない・状態キーの帳簿の帳尻・`ctx.PickOne` を新たに使っていない・台と版の定義。
static partial class Inv348Diag
{
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static string Script(BattleResult r) => string.Join("\n", r.Events.Select(x => $"{x.Kind}|{x.Turn}|{x.ActorId}|{x.TargetId}|{x.Amount}|{x.HpAfter}|{x.Text}"));

    /// <summary>計数の差の切り分け（`inv348 iso <台> <波> <seed>`）: 害の帳簿 ／ ターンごとの与ダメを片方ずつ渡す。</summary>
    internal static void Isolate(string board, string wave, int seed)
    {
        var b = BoardOf(board); var w = WaveOf(wave);
        foreach (var (name, boss, harm) in new[] { ("なし", false, false), ("ボスの計数だけ", true, false), ("害の帳簿だけ", false, true), ("両方", true, true) })
        {
            var p = BattleEngine.Materialize(b.F, BattleContext.PlayerTeam);
            var r = BattleEngine.Run(p, w.Make(), seed, verbose: false, boss: new BossRule(boss) { Scale = w.Scale }, harm: new HarmRule(harm));
            Console.WriteLine($"{name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors} HP {string.Join(",", p.Select(u => u.Hp))}");
        }
    }

    static void Check()
    {
        Console.WriteLine($"# {Tag} inv348 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 判定 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var boards = Boards().Concat(MoveBoards().Skip(2)).ToArray();
        W[] ws = { Boss, Guard, M2, M5, Nine, T1, StrongWaves[0] };
        const int n = 10;

        // ---- 器具の有無で勝敗が変わらない（verbose なし）----
        {
            int cells = 0, diff = 0, extraBattles = 0; string first = ""; var extraKeys = new HashSet<string>();
            foreach (var b in boards) foreach (var w in ws) for (int s = 0; s < n; s++)
            {
                var (a, pa, _) = Raw(b.F, w, s, census: true);
                var (z, pz, _) = Raw(b.F, w, s, census: false);
                cells++;
                bool same = a.PlayerWon == z.PlayerWon && a.Turns == z.Turns && a.PlayerSurvivors == z.PlayerSurvivors
                    && pa.Select(u => u.Hp).SequenceEqual(pz.Select(u => u.Hp))
                    && z.TallyByUnit.Keys.All(a.TallyByUnit.ContainsKey)
                    && a.TallyByUnit.All(kv => z.TallyByUnit.TryGetValue(kv.Key, out var zt)
                        ? zt.DamageToEnemy == kv.Value.DamageToEnemy && zt.DamageTaken == kv.Value.DamageTaken && zt.Interventions == kv.Value.Interventions
                        : kv.Value.DamageToEnemy == 0 && kv.Value.DamageTaken == 0 && kv.Value.Interventions == 0);
                var extra = a.TallyByUnit.Keys.Except(z.TallyByUnit.Keys).ToArray();
                if (extra.Length > 0) { extraBattles++; foreach (var k in extra) extraKeys.Add(k); }
                if (!same)
                {
                    diff++;
                    if (first == "")
                    {
                        string why = a.PlayerWon != z.PlayerWon || a.Turns != z.Turns ? $"勝敗 ／ T {a.PlayerWon}/{a.Turns} vs {z.PlayerWon}/{z.Turns}"
                            : !pa.Select(u => u.Hp).SequenceEqual(pz.Select(u => u.Hp)) ? $"HP {string.Join(",", pa.Select(u => u.Hp))} vs {string.Join(",", pz.Select(u => u.Hp))}"
                            : string.Join(",", a.TallyByUnit.Keys.Except(z.TallyByUnit.Keys)) + "|" + string.Join(",", z.TallyByUnit.Keys.Except(a.TallyByUnit.Keys))
                              + "|" + string.Join(",", a.TallyByUnit.Where(kv => z.TallyByUnit.TryGetValue(kv.Key, out var zt) && (zt.DamageToEnemy != kv.Value.DamageToEnemy || zt.DamageTaken != kv.Value.DamageTaken)).Select(kv => $"{kv.Key} {kv.Value.DamageToEnemy}/{kv.Value.DamageTaken} vs {z.TallyByUnit[kv.Key].DamageToEnemy}/{z.TallyByUnit[kv.Key].DamageTaken}"));
                        first = $"{b.Name} × {w.Name} × seed {s}: {why}";
                    }
                }
            }
            Expect($"計数（害の帳簿 ＋ 第348期の状態キーの帳簿 ＋ ターンごとの与ダメ）の有無で勝敗 ／ T ／ 生存 ／ 味方の残り HP ／ 駒ごとの与ダメ・被ダメが変わらない（台 {boards.Length} × 波 {ws.Length} × seed 0..{n - 1}）",
                diff == 0, $"{cells} 戦・差 {diff}{(first == "" ? "" : $"（最初: {first}）")}・計数だけの帳簿が増えた戦 {extraBattles}（{string.Join(",", extraKeys)}・与ダメ ／ 被ダメ ／ 介入 0 の帳簿だけ）");
        }
        // ---- 台本が変わらない（verbose あり）----
        {
            int cells = 0, diff = 0, vdiff = 0; string first = "";
            foreach (var b in boards) foreach (var w in new[] { Boss, Guard, M5 }) for (int s = 0; s < 3; s++)
            {
                var (a, _, _) = Raw(b.F, w, s, verbose: true, census: true);
                var (z, _, _) = Raw(b.F, w, s, verbose: true, census: false);
                var (q, _, _) = Raw(b.F, w, s, verbose: false, census: true);
                cells++;
                if (Script(a) != Script(z)) { diff++; if (first == "") first = $"{b.Name} × {w.Name} × seed {s}"; }
                if (a.PlayerWon != q.PlayerWon || a.Turns != q.Turns) vdiff++;
            }
            Expect($"台本（出来事の列: 種類・T・行為者・対象・量・HP・札）が計数の有無で1件も変わらない（台 {boards.Length} × 3 波 × seed 0..2）", diff == 0, $"{cells} 戦・差 {diff}{(first == "" ? "" : $"（最初: {first}）")}");
            Expect("verbose の有無で勝敗 ／ T が変わらない（同じ戦）", vdiff == 0, $"{cells} 戦・差 {vdiff}");
        }
        // ---- 状態キーの帳簿の帳尻 ----
        {
            long outSum = 0, inOwned = 0, inNone = 0, offCensusArrays = 0; int battles = 0;
            foreach (var b in boards) foreach (var w in new[] { M2, Guard }) for (int s = 0; s < 3; s++)
            {
                var (r, _, _) = Raw(b.F, w, s);
                var (z, _, _) = Raw(b.F, w, s, census: false);
                battles++;
                foreach (var t in r.TallyByUnit.Values)
                {
                    if (t.Inv348Out is long[] o) for (int k = 0; k < StatusKeys.All.Length; k++) for (int rel = 0; rel < 3; rel++) outSum += o[k * 8 + rel * 2];
                    if (t.Inv348In is long[] i) for (int k = 0; k < StatusKeys.All.Length; k++) { for (int rel = 0; rel < 3; rel++) inOwned += i[k * 8 + rel * 2]; inNone += i[k * 8 + 6]; }
                }
                offCensusArrays += z.TallyByUnit.Values.Count(t => t.Inv348Out is not null || t.Inv348In is not null);
            }
            Expect("帳簿の帳尻: 受け手の側（自分 ＋ 味方 ＋ 敵から）の回数の合計 ＝ 書き手の側の合計（書き手なしは受け手の側だけ）", outSum == inOwned && outSum > 0, $"{battles} 戦・書き手 {outSum:N0} ／ 受け手 {inOwned:N0}（書き手なし {inNone:N0}）");
            Expect("害の帳簿を渡さない戦では、第348期の帳簿は1本も確保されない（既定の `compare` ／ `layout` は1行も走らない）", offCensusArrays == 0, $"確保 {offCensusArrays}");
        }
        // ---- 的の波 ----
        {
            var (r, p, _) = Raw(BoardOf("燃焼").F, T1, 0);
            var s = Take(r, p, T1);
            long direct = r.TallyByUnit.Where(kv => kv.Key != FC.Target.Id).Sum(kv => kv.Value.BossDmgByTurn is int[] d ? d.Skip(1).Take(TargetTurns).Sum() : 0);
            Expect("的・一: 8T の与ダメ ＝ 味方の駒の `BossDmgByTurn` の 1〜8 ターン目の和・的は倒れない（勝ちに数えない）", s.Dmg8 == direct && s.Dmg8 > 0 && s.Wins == 0, $"{s.Dmg8:N0}");
        }
        // ---- 台と版 ----
        {
            var bs = Boards();
            bool axes = AxisOrder.All(a => bs.Count(b => b.Axis == a) == 2) && bs.All(b => b.F.Occupied().Count() == 5);
            Expect($"代表台: 軸 {AxisOrder.Length} × 2 台・どれも5体", axes, string.Join("・", AxisOrder.Select(a => $"{a} {bs.Count(b => b.Axis == a)}")));
            var f = Pin232(Row("強化×燃 (ヒヨ×ホタ×ボルグ)"));
            var g = Pin232(MoveCheck);
            bool pin = f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.BorgF0)) && f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.HotaL0)) && f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.HiyoF0))
                && g.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.HaneR0)) && g.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.SeroS0))
                && g.Occupied().Count(o => ReferenceEquals(o.Def, UnitCatalog.Yomi) || ReferenceEquals(o.Def, UnitCatalog.Basa) || ReferenceEquals(o.Def, UnitCatalog.Shio)) == 3;
            Expect("第232期の駒: ハネ HaneR0 ／ セロ SeroS0 ／ ボルグ BorgF0 ／ ホタ HotaL0 ／ ヒヨ HiyoF0 に替わり、ヨミ ／ バサ ／ シオは今のまま", pin, $"{Seats(g)} ／ {Seats(f)}");
            var borgNo = Without(UnitCatalog.Borg, TraitId.FireArmor);
            Expect("札を外す: 規定の札の配列から1枚だけ抜く（並びと数値は同じ）", borgNo.Traits.Count == UnitCatalog.Borg.Traits.Count - 1 && borgNo.MaxHp == UnitCatalog.Borg.MaxHp
                && borgNo.Traits.SequenceEqual(UnitCatalog.Borg.Traits.Where(t => t != TraitId.FireArmor)), $"{UnitCatalog.Borg.Traits.Count} → {borgNo.Traits.Count}");
            Expect("16 枚がすべて `UnitCatalog.All` にいる", NoArt.All(nm => UnitCatalog.All.Any(d => Short(d) == nm)), string.Join("・", NoArt));
        }
        // ---- PickOne ----
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs"), models = Now("BattleCore/Models.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show ec628811:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string x = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return x; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1, hm = Head("BattleCore/Models.cs").Split("PickOne(").Length - 1;
            string tok = "ctx." + "PickOne(";
            int mine = Directory.GetFiles("BattleSim/Modes", "Inv348*.cs").Sum(f => File.ReadAllText(f).Split(tok).Length - 1);
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs ／ Models.cs の `PickOne(` の数が第347期（ec628811）と同じ・器具の中も 0）", engine == he && traits == ht && models == hm && he > 0 && mine == 0,
                $"engine {he} → {engine}・traits {ht} → {traits}・models {hm} → {models}・器具 {mine}");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
