using BattleCore;
using static Common;

// checkwave run —— 第261期 段2・段3（重装兵の体の癒し手 W-* ・動じないボス B-*）と採否の表。
static partial class CheckWaveDiag
{
    /// <summary>報告書 §2 の予測の順（倒しT は早い順・崩れ始めは早い順）。測る前に固定した。</summary>
    static readonly string[] PredKill = { "燃焼 T3-244", "燃焼 T3-255", "雷", "混ぜ-255", "移動" };

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第261期 段2・段3 —— 重装兵の体の癒し手 ＋ 動じないボス（6台 × 6版・倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"打ち切り {BossTurns} ターン（撃破が {BossTurns} ターンを超えた勝ちは負けに数える）。癒し手は城塞の重装兵（145/12/3）の写し＋札1枚。ボスは攻12・速5・単体＋「動じない」。");
        Console.WriteLine();
        foreach (var c in CheckWaves) Console.WriteLine($"- {c.Name}: {c.What}");
        Console.WriteLine("- 対照: `第四波`（倍率なし）・`260:T-50後`（司祭の体の癒し手）・`260:B-全4`（動じない無し・HP 3,000）");
        Console.WriteLine();

        var res = new Dictionary<(string, string), Agg>();
        var waves = CheckWaves.Concat(new[] { Copy4, CWave260("T-50後"), CWave260("B-全4") }).ToList();
        string Key(CWave c) => CheckWaves.Contains(c) || c == Copy4 ? c.Name : "260:" + c.Name;
        BossHp = ChosenBossHp;
        foreach (string b in Boards)
        {
            foreach (var c in waves) res[(b, Key(c))] = Measure(BoardOf(b), CheckWave(c), c.IsBoss ? Kind.Boss : Kind.Normal, c.Mend);
            res[(b, "第四波")] = Measure(BoardOf(b), InvWave(Wave4), Kind.Normal, 0);
        }

        // ---- 表A ----
        string[] cols = { "第四波", "第四波写し", "260:T-50後", "W-30後", "W-50後", "W-50奥", "260:B-全4", "B-全4", "B-半4", "B-参500" };
        Console.WriteLine("## 表A 勝率 ／ 全員生存（%）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", cols.Select(c => $"{F1(res[(b, c)].Win)} ／ {F1(res[(b, c)].SurvPct)}")) + " |");
        Console.WriteLine();

        // ---- 表B 手数チェック ----
        string[] tv = { "W-30後", "W-50後", "W-50奥" };
        Console.WriteLine("## 表B 手数チェック —— 窓の与ダメと命中・癒し手");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 決着T | 与ダメ ／ 窓 | 命中 ／ 窓 | 1発 | 回復を上回った窓 | 癒し手を割った戦 | 割ったターン | 癒し手が1窓で受けた最大 | 実回復 ／ 戦 | 最後の一撃 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (string b in Boards)
            foreach (string v in tv.Prepend("260:T-50後"))
            {
                var a = res[(b, v)];
                string killers = string.Join("・", a.HealerKiller.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {kv.Value}"));
                Console.WriteLine($"| {b} | {v} | {F1(a.Win)} | {Per(a.Turns, a.N)} | {Per(a.WinDmg, a.WinCnt)} | {Per(a.WinHits, a.WinCnt)} | {Per(a.WinDmg, a.WinHits)} | "
                    + $"{Pct(a.OverTurns, a.HealTurns)}（{a.HealTurns}）| {Pct(a.HealerDead, a.N)} | {(a.HealerDead == 0 ? "—" : "T" + Per(a.HealerDeadT, a.HealerDead))} | {Per(a.HealerWinMax, a.N)} | {Per(a.HealGiven, a.N)} | {killers} |");
            }
        Console.WriteLine();

        // ---- 表C ボス ----
        string[] bv = { "B-全4", "B-半4" };
        Console.WriteLine("## 表C ボス —— 振れた手番・倒しT・崩れ始め");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 全員生存 | 窓の最大（平均 ／ 最大）| 倒しT（勝ちの平均）| 崩れ始め T（最初の死亡・割合）| 全滅（20T 以内）| 打ち切り | ボスの攻撃 ／ 生きていたターン | 入った ／ 名目（1戦）|");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string b in Boards)
            foreach (string v in new[] { "260:B-全4", "B-全4", "B-半4", "B-参500" })
            {
                var a = res[(b, v)];
                long cut = a.N - a.Wins - a.Wiped;
                Console.WriteLine($"| {b} | {v} | {F1(a.Win)} | {F1(a.SurvPct)} | {Per(a.MaxWinSum, a.N)} ／ {a.MaxWinMax} | {(a.Kill == 0 ? "—" : "T" + Per(a.KillT, a.Kill))} | "
                    + $"{(a.FirstDeath == 0 ? "—" : "T" + Per(a.FirstDeathT, a.FirstDeath))}（{Pct(a.FirstDeath, a.N)}%）| {Pct(a.Wiped, a.N)} | {Pct(cut, a.N)} | {a.BossActs} ／ {a.BossAlive}（{Pct(a.BossActs, a.BossAlive)}%）| {Per(a.BossDealt, a.N)} ／ {Per(a.BossNominal, a.N)}（{Pct(a.BossDealt, a.BossNominal)}%）|");
            }
        Console.WriteLine();

        // ---- 表D 採否 ----
        double Dev(string b, string v) => res[(b, v)].Win - Boards.Average(x => res[(x, v)].Win);
        Console.WriteLine("## 表D 採否の物差し（r は出さない・指示書 §1-4）");
        Console.WriteLine();
        Console.WriteLine("### D-1 符号（台の勝率 − 6台平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", tv.Concat(bv)) + " |");
        Console.WriteLine("|---|" + string.Concat(tv.Concat(bv).Select(_ => "--:|")));
        foreach (string b in Boards) Console.WriteLine($"| {b} | " + string.Join(" | ", tv.Concat(bv).Select(v => Dev(b, v).ToString("+0.0;−0.0;0.0"))) + " |");
        Console.WriteLine();
        Console.WriteLine("### D-2 2本の波で符号が逆になった台（|偏差| < 0.05pt は符号なしとして数えない）");
        Console.WriteLine();
        Console.WriteLine("| 手数 ＼ ボス | " + string.Join(" | ", bv) + " |");
        Console.WriteLine("|---|" + string.Concat(bv.Select(_ => "---|")));
        foreach (string t in tv)
            Console.WriteLine($"| {t} | " + string.Join(" | ", bv.Select(bb =>
            {
                var flip = Boards.Where(b => Math.Abs(Dev(b, t)) >= 0.05 && Math.Abs(Dev(b, bb)) >= 0.05 && Math.Sign(Dev(b, t)) != Math.Sign(Dev(b, bb))).ToList();
                return $"{flip.Count}（{(flip.Count == 0 ? "—" : string.Join("・", flip))}）";
            })) + " |");
        Console.WriteLine();
        Console.WriteLine("### D-3 倒しT・崩れ始めの順位（予測は報告書 §2・毒は倒せないので順位から外す）");
        Console.WriteLine();
        Console.WriteLine($"予測の倒しT の順: {string.Join(" ＜ ", PredKill)}（燃焼の2台は同順）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 倒しT の順（勝った戦の平均）| 予測と一致 | 崩れ始めの順（最初の死亡の平均・死んだ戦のある台）|");
        Console.WriteLine("|---|---|:-:|---|");
        foreach (string v in bv)
        {
            var kill = PredKill.Select(b => (b, T: res[(b, v)].Kill == 0 ? double.PositiveInfinity : (double)res[(b, v)].KillT / res[(b, v)].Kill)).OrderBy(x => x.T).ToList();
            // 燃焼の2台は同順として扱う: 燃焼の2台が先頭2つを占め、残りが 雷 → 混ぜ → 移動 の順なら一致。
            var rest = kill.Where(x => !x.b.StartsWith("燃焼")).Select(x => x.b).ToList();
            bool fireFirst = kill.Take(2).All(x => x.b.StartsWith("燃焼"));
            bool ok = fireFirst && rest.SequenceEqual(new[] { "雷", "混ぜ-255", "移動" });
            var fall = Boards.Where(b => res[(b, v)].FirstDeath > 0).Select(b => (b, T: (double)res[(b, v)].FirstDeathT / res[(b, v)].FirstDeath)).OrderBy(x => x.T).ToList();
            Console.WriteLine($"| {v} | {string.Join(" ＜ ", kill.Select(x => $"{x.b} {(double.IsInfinity(x.T) ? "—" : "T" + x.T.ToString("F1"))}"))} | {(ok ? "○" : "×")} | "
                + $"{(fall.Count == 0 ? "—" : string.Join(" ＜ ", fall.Select(x => $"{x.b} T{x.T:F1}")))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
