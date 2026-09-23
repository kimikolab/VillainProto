using BattleCore;
using static Common;

// =====================================================================================
// sora186 モード（第186期 追補）—— 突き・逸らし先の内訳・席替えの追試
//
//     dotnet run --project BattleSim -c Release 0 sora186 thrust  # 逸らし先の内訳（指差した敵／ほかの標持ち／補助）と突きの威力の分布
//     dotnet run --project BattleSim -c Release 0 sora186 carry   # 診断台: ソラ×ガン×ガルド＋ヒサ（ハイパーキャリー）
//     dotnet run --project BattleSim -c Release 0 sora186 seat    # `逸らし (ソラ×カド)` の席替えを帯B（seed 200..599）で追試
// =====================================================================================

static partial class Sora186Diag
{
    // =================================================================================
    // thrust —— 逸らし先の内訳と突きの威力の分布
    // =================================================================================

    static void ThrustLedger()
    {
        Console.WriteLine("# 第186期 追補 `sora186 thrust` —— 逸らし先の内訳と突きの威力（新・第2〜5波・seed 0..199）");
        Console.WriteLine();
        var rows = Bands().Where(r => Mine(r.F)).Concat(Benches.Select(b => ("台", b.Name, b.F)))
                          .Concat(CarryBenches.Select(b => ("台", b.Name, b.F))).ToList();

        Console.WriteLine("## 表T1. 逸らした宛先の内訳（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("`指差した敵` ＝ ソラが標を付けた敵 ／ `ほかの標持ち` ＝ 殴ってきた本人が指差した敵だったので、ほかの標持ちへ ／ "
                          + "`補助` ＝ ほかに標持ちがいないので、殴ってきた本人以外の現在HP最大へ ／ `宛先なし` ＝ 逸らせずソラが全部受けた単体の一撃。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 逸らした | 指差した敵 | ほかの標持ち | 補助 | 宛先なし |");
        Console.WriteLine("|---|--:|---|---|---|--:|");
        foreach (var r in rows)
        {
            double hits = 0, p = 0, o = 0, f = 0, none = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    UnitTally t = T(Fight(r.Item3, st, seed, New), "sora");
                    n++;
                    hits += t.DeflectHits; p += t.DeflectToPointed; o += t.DeflectToOtherMarked; f += t.DeflectToFallback; none += t.DeflectNoTarget;
                }
            string P(double x) => (x / n).ToString("F2") + "（" + (hits > 0 ? (100.0 * x / hits).ToString("F0") : "—") + "%）";
            Console.WriteLine("| " + r.Item2 + " | " + (hits / n).ToString("F2") + " | " + P(p) + " | " + P(o) + " | " + P(f) + " | "
                              + (none / n).ToString("F2") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("(B) の後は**殴った本人へ返る一撃は構造的に 0**（`DeflectTrait.Redirect` が本人を除く）。");
        Console.WriteLine();

        Console.WriteLine("## 表T2. 突きの威力の分布（新 ＝ 現在攻撃力 × (1＋回数) ／ 素 ＝ 現在攻撃力 ＋ 素の攻撃力 × 回数）");
        Console.WriteLine();
        Console.WriteLine("威力は `PerformAttack` が振る前の値（貫きの減衰・§1・軛の前）。`回数` は1回の突きに乗った逸らしの数。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 突き/戦 | 指差しの列 | 回数 平均 | 回数 最大 | 威力 平均 | 中央値 | 上位5% | 最大 | ≤6 | 7〜12 | 13〜24 | 25〜48 | 49以上 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in rows)
            foreach (Version v in new[] { New, VP })
            {
                var atks = new List<int>(); long sw = 0, forced = 0, chargeSum = 0, chargeMax = 0; int n = 0;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        UnitTally t = T(Fight(r.Item3, st, seed, v), "sora");
                        n++;
                        sw += t.ThrustSwings; forced += t.ThrustForced; chargeSum += t.ThrustChargeSum;
                        chargeMax = Math.Max(chargeMax, t.ThrustChargeMax);
                        if (t.ThrustAtks is not null) atks.AddRange(t.ThrustAtks);
                    }
                atks.Sort();
                string Q(double q) => atks.Count == 0 ? "—" : atks[Math.Min(atks.Count - 1, (int)(q * atks.Count))].ToString();
                string B(int lo, int hi) => atks.Count == 0 ? "—" : (100.0 * atks.Count(a => a >= lo && a <= hi) / atks.Count).ToString("F0") + "%";
                Console.WriteLine("| " + r.Item2 + " | " + (v == New ? "新" : "素") + " | " + ((double)sw / n).ToString("F2") + " | "
                                  + (sw > 0 ? (100.0 * forced / sw).ToString("F0") + "%" : "—") + " | "
                                  + (sw > 0 ? ((double)chargeSum / sw).ToString("F2") : "—") + " | " + chargeMax + " | "
                                  + (atks.Count > 0 ? atks.Average().ToString("F1") : "—") + " | " + Q(0.5) + " | " + Q(0.95) + " | "
                                  + (atks.Count > 0 ? atks[^1].ToString() : "—") + " | "
                                  + B(0, 6) + " | " + B(7, 12) + " | " + B(13, 24) + " | " + B(25, 48) + " | " + B(49, int.MaxValue) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表T3. 波別の突きの威力（新・6 行と台の合算）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 突き | 威力 平均 | 上位5% | 最大 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int st = 1; st < 5; st++)
        {
            var atks = new List<int>();
            foreach (var r in rows)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    UnitTally t = T(Fight(r.Item3, st, seed, New), "sora");
                    if (t.ThrustAtks is not null) atks.AddRange(t.ThrustAtks);
                }
            atks.Sort();
            Console.WriteLine("| 第" + (st + 1) + "波 | " + atks.Count + " | " + (atks.Count > 0 ? atks.Average().ToString("F1") : "—") + " | "
                              + (atks.Count > 0 ? atks[Math.Min(atks.Count - 1, (int)(0.95 * atks.Count))].ToString() : "—") + " | "
                              + (atks.Count > 0 ? atks[^1].ToString() : "—") + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // carry —— ハイパーキャリーの診断台
    // =================================================================================

    static readonly (string Name, Formation F)[] CarryBenches =
    {
        // ソラ中央（隣4）。前1 ガルド（中継）・前3 ガン（号令）・後3 ヒサ（隣はソラ 96 とガン 52 → ソラを指す）・後1 ドルガ。
        ("台3 ソラ×ガン×ガルド＋ヒサ", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gan,
            center: UnitCatalog.Sora, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hisa)),
        // 同じ台でヒサだけノミに替えた（ヒサの半減が重ならない対照）。
        ("台4 台3 のヒサ → ノミ", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gan,
            center: UnitCatalog.Sora, back1: UnitCatalog.Dolga, back3: UnitCatalog.Nomi)),
    };

    static void CarryBench()
    {
        Console.WriteLine("# 第186期 追補 `sora186 carry` —— ハイパーキャリーの診断台（`Presets` には足さない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1〜5波 | 第2〜5波平均 |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var b in CarryBenches)
            foreach (Version v in new[] { V0, VD, New, VP })
            {
                double[] w = Rates(b.F, v);
                Console.WriteLine("| " + b.Name + " | " + v.Name + " | " + string.Join(" / ", w.Select(x => x.ToString("F1"))) + " | " + Mean25(w).ToString("F1") + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 帳簿（第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ソラの死亡率 | ソラの生存T | 逸らした | ヒサ重なり | 突き | 威力 平均 | 最大 | ソラの与害 | ソラの強化（外） | 決着T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in CarryBenches)
            foreach (Version v in new[] { VD, New, VP })
            {
                double dead = 0, life = 0, hits = 0, stack = 0, sw = 0, atk = 0, dmg = 0, whet = 0, turns = 0; long max = 0; int n = 0;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = Fight(b.F, st, seed, v);
                        UnitTally s = T(r, "sora");
                        n++;
                        dead += s.Deaths > 0 ? 1 : 0; life += LifeOf(r, "sora"); hits += s.DeflectHits; stack += s.DeflectBeckonStack;
                        sw += s.ThrustSwings; atk += s.ThrustAtkSum; max = Math.Max(max, s.ThrustAtkMax);
                        dmg += s.DamageToEnemy; whet += s.Whetted; turns += r.Turns;
                    }
                Console.WriteLine("| " + b.Name + " | " + v.Name + " | " + (100.0 * dead / n).ToString("F1") + "% | " + (life / n).ToString("F2") + " | "
                                  + (hits / n).ToString("F2") + " | " + (stack / n).ToString("F2") + " | " + (sw / n).ToString("F2") + " | "
                                  + (sw > 0 ? (atk / sw).ToString("F1") : "—") + " | " + max + " | " + (dmg / n).ToString("F1") + " | "
                                  + (whet / n).ToString("F1") + " | " + (turns / n).ToString("F2") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("`ソラの強化（外）` ＝ `UnitTally.Whetted`（窓口 `Whet` を通って他者から届いた量）。");
        Console.WriteLine();

        Console.WriteLine("## 1戦のログ（台3・第5波・seed 1・新）——突きと逸らしの行だけ");
        Console.WriteLine();
        Console.WriteLine("```");
        BattleResult one = Fight(CarryBenches[0].F, 4, 1, New, verbose: true);
        foreach (var line in one.Log.Where(l => l.Text.Contains("逸らした") || l.Text.Contains("突いた") || l.Text.Contains("ターン")
                                                 || (l.Text.Contains("逸らしのソラ →"))).Take(70))
            Console.WriteLine(line);
        Console.WriteLine("```");
        Console.WriteLine();
    }

    // =================================================================================
    // seat —— `逸らし (ソラ×カド)` の席替えを別の seed 帯で追試する
    // =================================================================================

    static void SeatConfirm()
    {
        Console.WriteLine("# 第186期 追補 `sora186 seat` —— `逸らし (ソラ×カド)` の席替えの追試（新ソラ）");
        Console.WriteLine();
        Console.WriteLine("`confirm` と同じ規則: **選定に使っていない seed 200..599（400 試行）で、5 波平均が +5.0pt 以上**なら採る。"
                          + "情報セルは帯A（seed 0..199）の第2〜5波で数える（(G14)）。");
        Console.WriteLine();
        var seats = new (string Name, Formation F)[]
        {
            ("現行（前1 カド／前3 ボルグ／中央 グザ／後1 ヒサ／後3 ソラ）", Formation.Build(front1: UnitCatalog.Kado, front3: UnitCatalog.Borg,
                center: UnitCatalog.Guza, back1: UnitCatalog.Hisa, back3: UnitCatalog.Sora)),
            ("候補（前1 ソラ／前3 カド／中央 グザ／後1 ヒサ／後3 ボルグ）", Formation.Build(front1: UnitCatalog.Sora, front3: UnitCatalog.Kado,
                center: UnitCatalog.Guza, back1: UnitCatalog.Hisa, back3: UnitCatalog.Borg)),
        };
        Console.WriteLine("| 席 | 帯A 5波平均 | 帯A 第1〜5波 | 帯B 5波平均 | 帯B 第1〜5波 | 情報セル（帯A） | ヒサが指した相手（回/戦） | ヒサ重なり | ソラの死亡率 |");
        Console.WriteLine("|---|--:|---|--:|---|--:|---|--:|--:|");
        var bandB = new List<double>();
        foreach (var s in seats)
        {
            double[] a = Rates(s.F, New), b = Rates(s.F, New, 400, 200);
            bandB.Add(b.Average());
            int info = Enumerable.Range(1, 4).Count(i => a[i] > 0 && a[i] < 100);
            var picked = new Dictionary<string, double>(); double stack = 0, dead = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = Fight(s.F, st, seed, New);
                    n++;
                    stack += T(r, "sora").DeflectBeckonStack; dead += T(r, "sora").Deaths > 0 ? 1 : 0;
                    foreach ((int _, UnitDef d) in s.F.Occupied())
                        if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? u) && u.BeckonPicked > 0)
                            picked[Short(d)] = picked.GetValueOrDefault(Short(d)) + u.BeckonPicked;
                }
            Console.WriteLine("| " + s.Name + " | " + a.Average().ToString("F1") + " | " + string.Join(" / ", a.Select(x => x.ToString("F1"))) + " | "
                              + b.Average().ToString("F1") + " | " + string.Join(" / ", b.Select(x => x.ToString("F1"))) + " | " + info + " | "
                              + string.Join(" ", picked.OrderByDescending(x => x.Value).Select(x => x.Key + " " + (x.Value / n).ToString("F2"))) + " | "
                              + (stack / n).ToString("F2") + " | " + (100.0 * dead / n).ToString("F1") + "% |");
        }
        Console.WriteLine();
        double d0 = bandB[1] - bandB[0];
        Console.WriteLine("- **帯B の差: " + D(d0) + "pt** → " + (d0 >= 5.0 ? "**閾値 +5.0pt を超える（採る）**" : "閾値 +5.0pt に届かない（据え置く）"));
        Console.WriteLine();
    }
}
