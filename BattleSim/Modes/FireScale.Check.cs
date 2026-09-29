using System.Text.RegularExpressions;
using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// firescale check —— 自己検査（受け入れ 2・3）。
static partial class FireScaleDiag
{
    static partial void CheckImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int ok = 0, all = 0;
        void Ok(bool c, string what) { all++; if (c) ok++; Console.WriteLine($"- {(c ? "○" : "×")} {what}"); }
        Console.WriteLine("# 第239期 `firescale check`");
        Console.WriteLine();

        // (a) 影の規則（盤の外で1駒ずつ）
        {
            var k1 = new Shadow(0); var k2 = new Shadow(1); var k3 = new Shadow(2);
            var alive = new[] { 1 };
            foreach (var s in new[] { k1, k2, k3 }) { s.Ignite(1); s.Ignite(1); }
            Ok(k1.Of(1) == 2 && k2.Of(1) == 1 && k3.Of(1) == 2, "1ターンに2回点く: K1 2 ／ K2 1 ／ K3 2");
            foreach (var s in new[] { k1, k2, k3 }) s.Decay(alive);
            Ok(k1.Of(1) == 2 && k2.Of(1) == 1 && k3.Of(1) == 2, "点いたターンの終わりは萎まない");
            foreach (var s in new[] { k1, k2, k3 }) s.Decay(alive);
            Ok(k1.Of(1) == 1 && k2.Of(1) == 0 && k3.Of(1) == 2, "点かないターン1回: K1 −1 ／ K2 −1 ／ K3 そのまま");
            foreach (var s in new[] { k1, k2, k3 }) s.Decay(alive);
            Ok(k1.Of(1) == 0 && k2.Of(1) == 0 && k3.Of(1) == 1, "点かないターン2回: K3 は2回目で −1");
            var c = new Shadow(0); for (int i = 0; i < 7; i++) c.Ignite(1);
            Ok(c.Of(1) == Max, "上限 4");
            c.Kill(1);
            Ok(c.Of(1) == 0, "倒れたら消える");
            var z = new Shadow(0); z.Ignite(1); z.Decay(alive); z.Decay(alive); z.Ignite(1);
            Ok(z.Of(1) == 1, "0 まで萎んだ後の着火は 1 から");
        }

        // (b) 手番の枠を足す行に乱数が無い（受け入れ 3）・影と絵は engine の外
        {
            string root = FindRoot();
            var el = File.ReadAllLines(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
            var lines = el.Where(l => l.Contains("handEv0") || l.Contains("Hands.Add") || l.Contains("Hands = ctx.Hands")).ToList();
            Ok(lines.Count == 3 && lines.All(l => !l.Contains("Roll(") && !l.Contains("PickOne(") && !l.Contains("_rng")), $"engine の手番の枠の {lines.Count} 行に `Roll(` / `PickOne(` / `_rng` が無い");
            var mine = Directory.GetFiles(Path.Combine(root, "BattleSim", "Modes"), "FireScale*.cs").Select(File.ReadAllText).ToList();
            Ok(mine.All(t => !Regex.IsMatch(t, @"ctx\.(Roll|PickOne)\(")), "器具（`FireScale*.cs`）は盤面の乱数に触らない（`ctx.Roll` / `ctx.PickOne` を呼ばない）");
        }

        // (c) verbose の有無で盤面が同じ（手番の枠は verbose のときだけ積む）・枠 ＝ TurnsTaken・着火 ＝ 帳簿
        {
            long n = 0, diff = 0, handDiff = 0, ignDiff = 0, overlap = 0, offFrames = 0;
            foreach (var (bn, bf) in Boards)
            {
                var f = bf();
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < BA.Scales.Length; s++)
                        for (int seed = 0; seed < 20; seed++)
                        {
                            var (r, p, e) = Fight(f, w, BA.Scales[s].Sc, seed, true);
                            var (r2, _, _) = Fight(f, w, BA.Scales[s].Sc, seed, false);
                            n++;
                            bool same = r.PlayerWon == r2.PlayerWon && r.Turns == r2.Turns && r.PlayerStarterFallen.Count == r2.PlayerStarterFallen.Count
                                        && r.TallyByUnit.Count == r2.TallyByUnit.Count
                                        && r.TallyByUnit.All(kv => r2.TallyByUnit.TryGetValue(kv.Key, out var t2) && t2.TurnsTaken == kv.Value.TurnsTaken
                                                                   && t2.BurnLit == kv.Value.BurnLit && t2.BurnRelit == kv.Value.BurnRelit && t2.DamageToEnemy == kv.Value.DamageToEnemy && t2.DamageTaken == kv.Value.DamageTaken);
                            if (!same) diff++;
                            offFrames += r2.Hands.Count;
                            if (r.Hands.Count != r.TallyByUnit.Values.Sum(t => t.TurnsTaken)) handDiff++;
                            long ign = r.Events.Count(x => x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Burn);
                            if (ign != r.TallyByUnit.Values.Sum(t => (long)t.BurnLit + t.BurnRelit)) ignDiff++;
                            var hs = r.Hands.OrderBy(h => h.EventStart).ToList();
                            for (int i = 1; i < hs.Count; i++) if (hs[i].EventStart < hs[i - 1].EventEnd && hs[i].EventEnd > hs[i - 1].EventEnd) overlap++;
                            // 影と絵を数えても結果は変わらない（読むだけ）
                            int evBefore = r.Events.Count, hBefore = r.Hands.Count;
                            Analyze(r, p, e, new Agg());
                            if (r.Events.Count != evBefore || r.Hands.Count != hBefore) diff++;
                        }
            }
            Ok(diff == 0, $"verbose の有無で勝敗・決着T・倒れた駒・駒ごとの手番／着火／与ダメ／被ダメが違う戦 {diff} ／ {n}（影を数えた後も台本と枠の件数は同じ）");
            Ok(offFrames == 0, $"verbose 偽の戦の手番の枠 {offFrames} 件（0）");
            Ok(handDiff == 0, $"手番の枠の数 ≠ 帳簿の `TurnsTaken` の戦 {handDiff} ／ {n}");
            Ok(ignDiff == 0, $"台本の着火（`StatusGain` burn）≠ 帳簿の 点いた＋煽られた の戦 {ignDiff} ／ {n}");
            Ok(overlap == 0, $"手番の枠が入れ子でなく交差する組 {overlap}");
        }

        // (d) 規定の駒 ＝ 第238期の版（A+D2+V1）
        {
            var d2 = FireWardDiag.D2; var v1 = FireWardDiag.V1;
            Ok(d2.Traits.SequenceEqual(UnitCatalog.BorgL0.Traits) && d2.MaxHp == UnitCatalog.BorgL0.MaxHp && d2.Attack == UnitCatalog.BorgL0.Attack && d2.Speed == UnitCatalog.BorgL0.Speed,
                "規定のボルグ ＝ 第238期の A+D2（札の並び・HP・攻・速）");
            Ok(v1.Traits.SequenceEqual(UnitCatalog.HiyoL0.Traits) && v1.MaxHp == UnitCatalog.HiyoL0.MaxHp, "規定のヒヨ ＝ 第238期の V1");
            Ok(!UnitCatalog.All.Contains(UnitCatalog.BorgF0) && !UnitCatalog.All.Contains(UnitCatalog.HiyoF0) && UnitCatalog.All.Contains(UnitCatalog.Borg),
                "旧（`BorgF0` / `HiyoF0`）は `All` に入れていない");
            // 同じ席・同じ seed で規定の駒と版の駒の台本が一致
            var fa = T3;
            var fb = Formation.Build(front1: v1, front3: UnitCatalog.HotaL0, center: d2, back1: UnitCatalog.Doha, back3: UnitCatalog.Sora);
            int bad = 0;
            for (int s = 0; s < 40; s++)
                foreach (int sc in new[] { 0, 1 })
                {
                    var a = Fight(fa, BA.MainWave, BA.Scales[sc].Sc, s).R; var b = Fight(fb, BA.MainWave, BA.Scales[sc].Sc, s).R;
                    if (a.Log.Count != b.Log.Count || a.Log.Zip(b.Log).Any(z => z.First.Text != z.Second.Text)) bad++;
                }
            Ok(bad == 0, $"T3 の席で 規定の駒 と 第238期 A+D2+V1 の駒 のログが違う戦 {bad} ／ 80");
        }

        Console.WriteLine();
        Console.WriteLine($"**{ok} / {all}**（所要 {sw.Elapsed.TotalSeconds:F1} 秒）");
    }
}
