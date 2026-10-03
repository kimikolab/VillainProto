using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// checkwave run —— 段2（手数チェック）・段3（ボス）の測定と採否の表。
static partial class CheckWaveDiag
{
    /// <summary>ボスの HP の初版（指示書 §4.1 の候補）。HP を 3,000 にした根拠を示すための参考の版（判定には使わない）。</summary>
    internal const int DraftBossHp = 500;
    /// <summary>採った HP（報告書 §3: 段1 の的・一で、移動の窓の最大 2,870 を上回り、燃焼の窓の最大の平均 3,000〜4,000 台を下回る）。</summary>
    internal const int ChosenBossHp = 3000;

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        BossHp = ChosenBossHp;
        Console.WriteLine("# 第260期 段2・段3 —— チェック波（6台 × 手数チェック3版 ＋ ボス3版・倍率なし・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"ボスの HP {BossHp}・攻 12・速 5・単体。打ち切り {BossTurns} ターン（撃破が {BossTurns} ターンを超えた勝ちは負けに数える）。癒し手は従軍司祭（40/9/8）の写し＋札1枚。");
        Console.WriteLine();
        foreach (var c in CheckWaves) Console.WriteLine($"- {c.Name}: {c.What}");
        Console.WriteLine();

        var res = new Dictionary<(string, string), Agg>();
        foreach (string b in Boards)
        {
            foreach (var c in CheckWaves.Append(Copy4)) res[(b, c.Name)] = Measure(BoardOf(b), CheckWave(c), c.IsBoss ? Kind.Boss : Kind.Normal, c.Mend);
            res[(b, "第四波")] = Measure(BoardOf(b), InvWave(Wave4), Kind.Normal, 0);
            res[(b, "第四波 400/300")] = Measure(BoardOf(b), FireCycleDiag.WaveOf(Wave4, BA.Scales[1].Sc), Kind.Normal, 0);
        }
        // 参考: 初版の HP 500（判定には使わない）
        BossHp = DraftBossHp;
        foreach (string b in Boards) res[(b, "B-全4@500")] = Measure(BoardOf(b), CheckWave(CWaveOf("B-全4")), Kind.Boss, 0);
        BossHp = ChosenBossHp;

        // ---- 表A 勝率 ----
        string[] cols = { "第四波", "第四波 400/300", "第四波写し", "T-30後", "T-50後", "T-50奥", "B-全4", "B-全8", "B-半4", "B-全4@500" };
        Console.WriteLine("## 表A 勝率 ／ 全員生存（%）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", cols.Select(c => $"{F1(res[(b, c)].Win)} ／ {F1(res[(b, c)].SurvPct)}")) + " |");
        Console.WriteLine();
        Console.WriteLine("`第四波写し` は `第四波` と全セル一致すること（Phase 0-4）。`B-全4@500` は HP の初版（参考・判定に使わない）。");
        Console.WriteLine();

        // ---- 表B 手数チェック ----
        Console.WriteLine("## 表B 手数チェック —— 窓（手番 ＋ 次の頭の刻み）の与ダメと命中・回復");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 決着T | 与ダメ ／ 窓 | 命中 ／ 窓 | 1発 | 窓の最大（平均）| 回復を上回った窓 | 癒し手を割った戦 | 割ったターン | 実回復 ／ 戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string b in Boards)
            foreach (string v in new[] { "第四波写し", "T-30後", "T-50後", "T-50奥" })
            {
                var a = res[(b, v)];
                Console.WriteLine($"| {b} | {v} | {F1(a.Win)} | {Per(a.Turns, a.N)} | {Per(a.WinDmg, a.WinCnt)} | {Per(a.WinHits, a.WinCnt)} | {Per(a.WinDmg, a.WinHits)} | {Per(a.MaxWinSum, a.N)} | "
                    + $"{(v == "第四波写し" ? "—" : Pct(a.OverTurns, a.HealTurns) + $"（{a.HealTurns}）")} | {(v == "第四波写し" ? "—" : Pct(a.HealerDead, a.N))} | {(v == "第四波写し" ? "—" : "T" + Per(a.HealerDeadT, a.HealerDead))} | {(v == "第四波写し" ? "—" : Per(a.HealGiven, a.N))} |");
            }
        Console.WriteLine();
        Console.WriteLine("「回復を上回った窓」＝ 窓 t の与ダメ ＞ ターン t+1 の頭の名目回復（量 × 敵の生存数・癒し手が生きているときだけ）。括弧は分母の窓の数。");
        Console.WriteLine();
        Console.WriteLine("### B′ 癒し手に最後の一撃を入れた駒（200 戦の内訳）");
        Console.WriteLine();
        Console.WriteLine("| 台 | T-30後 | T-50後 | T-50奥 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", new[] { "T-30後", "T-50後", "T-50奥" }.Select(v => string.Join("・", res[(b, v)].HealerKiller.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")))) + " |");
        Console.WriteLine();

        // ---- 表C ボス ----
        Console.WriteLine("## 表C ボス —— 窓の最大・削り切ったターン・最初の死亡");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | 全員生存 | 窓の最大（平均 ／ 最大）| 削り切った T（勝ちの平均）| 最初の死亡 T（死んだ戦の平均・割合）| 全滅（20T 以内）| 打ち切り | ボスの攻撃 ／ 生きていたターン |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string b in Boards)
            foreach (string v in new[] { "B-全4", "B-全8", "B-半4", "B-全4@500" })
            {
                var a = res[(b, v)];
                long cut = a.N - a.Wins - a.Wiped;
                Console.WriteLine($"| {b} | {v} | {F1(a.Win)} | {F1(a.SurvPct)} | {Per(a.MaxWinSum, a.N)} ／ {a.MaxWinMax} | {(a.Kill == 0 ? "—" : "T" + Per(a.KillT, a.Kill))} | "
                    + $"{(a.FirstDeath == 0 ? "—" : "T" + Per(a.FirstDeathT, a.FirstDeath))}（{Pct(a.FirstDeath, a.N)}%）| {Pct(a.Wiped, a.N)} | {Pct(cut, a.N)} | {a.BossActs} ／ {a.BossAlive}（{Pct(a.BossActs, a.BossAlive)}%）|");
            }
        Console.WriteLine();

        // ---- 表D 採否の物差し ----
        Console.WriteLine("## 表D 採否の物差し");
        Console.WriteLine();
        string[] tv = { "T-30後", "T-50後", "T-50奥" }, bv = { "B-全4", "B-全8", "B-半4" };
        double Dev(string b, string v) => res[(b, v)].Win - Boards.Average(x => res[(x, v)].Win);
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
        Console.WriteLine("### D-3 本編第四波との r と max|Δ|（6台）");
        Console.WriteLine();
        Console.WriteLine("| 版 | r（第四波 倍率なし）| r（第四波 400/300）| max|Δ|（倍率なし）| 台 |");
        Console.WriteLine("|---|--:|--:|--:|---|");
        foreach (string v in tv.Concat(bv))
        {
            var xs = Boards.Select(b => res[(b, v)].Win).ToArray();
            var y0 = Boards.Select(b => res[(b, "第四波")].Win).ToArray();
            var y1 = Boards.Select(b => res[(b, "第四波 400/300")].Win).ToArray();
            var d = Boards.Select(b => (b, D: Math.Abs(res[(b, v)].Win - res[(b, "第四波")].Win))).OrderByDescending(x => x.D).First();
            Console.WriteLine($"| {v} | {R(xs, y0)} | {R(xs, y1)} | {d.D:F1} | {d.b} |");
        }
        Console.WriteLine();
        Console.WriteLine("### D-4 固有の勝者／敗者（勝ち ＝ 勝率 50% 以上）——第四波（倍率なし）で同じ立場でなかったものだけを「新規」と数える");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝者 | 敗者 | 固有の勝者（新規）| 固有の敗者（新規）|");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (string v in tv.Concat(bv))
        {
            var win = Boards.Where(b => res[(b, v)].Win >= 50).ToList(); var lose = Boards.Except(win).ToList();
            var win4 = Boards.Where(b => res[(b, "第四波")].Win >= 50).ToHashSet();
            string uw = win.Count == 1 && !(win4.Count == 1 && win4.Contains(win[0])) ? win[0] : "—";
            string ul = lose.Count == 1 && win4.Contains(lose[0]) ? lose[0] : "—";
            Console.WriteLine($"| {v} | {(win.Count == 0 ? "—" : string.Join("・", win))} | {(lose.Count == 0 ? "—" : string.Join("・", lose))} | {uw} | {ul} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>ピアソンの r。どちらかの分散が 0 なら「—（分散 0）」。</summary>
    internal static string R(double[] x, double[] y)
    {
        double mx = x.Average(), my = y.Average();
        double sxy = x.Zip(y, (a, b) => (a - mx) * (b - my)).Sum(), sxx = x.Sum(a => (a - mx) * (a - mx)), syy = y.Sum(b => (b - my) * (b - my));
        return sxx < 1e-9 || syy < 1e-9 ? "—（分散 0）" : (sxy / Math.Sqrt(sxx * syy)).ToString("+0.00;−0.00");
    }
}
