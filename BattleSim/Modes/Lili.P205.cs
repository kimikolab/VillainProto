using BattleCore;
using static Common;

// =====================================================================================
// lili モード（第205期）—— リリの吸う量を「味方が受けた痛み」から取る
//
// 指示書は design/PHASE205_LILI2_SPEC.md ／ 報告は design/PHASE205_LILI2.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 lili phase205   # Q0-2・Q0-3・Q0-5 の数え物（第204期の盤面 ＝ V0 で回す）
// =====================================================================================

static partial class LiliDiag
{
    /// <summary>敵の <c>AtkBonus</c> を自分で動かす札（Q0-5・<c>Traits.cs</c> の <c>AtkBonus +=</c> の走査から）。</summary>
    static readonly TraitId[] SelfBonusTraits =
    {
        TraitId.Rage, TraitId.Guardian, TraitId.Necro, TraitId.Executioner, TraitId.Thorns,
        TraitId.Blightfed, TraitId.Displaced, TraitId.Sharer, TraitId.Taillight,
    };

    static void Phase205()
    {
        Console.WriteLine("# 第205期 `lili phase205` —— Q0-2・Q0-3・Q0-5（第204期の盤面 ＝ V0・seed 0..199）");
        Console.WriteLine();
        var rows = LiliRows().ToList();

        // ---- Q0-2 / Q0-3 ----
        Console.WriteLine("## Q0-2 味方が失った HP の出どころ（第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("**被ダメ** は味方全員の `DamageTaken`（破片の後・過剰分を含む）。**味方から** はそのうち味方が出どころの分（`TakenFromAlly`）。"
                          + "**味方を削った駒** は `DamageToAlly` が 1 戦 1.0 以上の駒。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 被ダメ/戦 | 味方から/戦 | 割合 | 味方を削った駒（/戦） |");
        Console.WriteLine("|---|---|--:|--:|--:|---|");
        var q3 = new List<string>();
        foreach (var (band, name, f) in rows)
        {
            var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
            double taken = 0, fromAlly = 0; int n = 0;
            var ff = new Dictionary<string, double>();
            var perWave = new (double Pain, double Acts, double Nom, double Fires, int N)[5];
            var gate = new object();
            foreach (int st in Waves25)
            {
                Formation enemy = EnemyCatalog.Stages[st].Enemy;
                Parallel.For(0, Seeds, seed =>
                {
                    BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
                    lock (gate)
                    {
                        n++;
                        foreach (var (id, u) in r.TallyByUnit)
                        {
                            if (!ids.Contains(id)) continue;
                            taken += u.DamageTaken; fromAlly += u.TakenFromAlly;
                            if (u.DamageToAlly > 0) ff[id] = ff.GetValueOrDefault(id) + u.DamageToAlly;
                        }
                        if (r.TallyByUnit.TryGetValue("lili", out UnitTally? me))
                        {
                            var w = perWave[st];
                            perWave[st] = (w.Pain + me.KissPainSum, w.Acts + me.KissActs, w.Nom + me.KissNominal, w.Fires + me.KissFires, w.N + 1);
                        }
                    }
                });
            }
            var who = ff.Where(kv => kv.Value / n >= 1.0).OrderByDescending(kv => kv.Value)
                        .Select(kv => (UnitCatalog.Everyone.FirstOrDefault(u => u.Id == kv.Key)?.Name ?? kv.Key) + " " + (kv.Value / n).ToString("F1"));
            Console.WriteLine("| " + band + " | " + name + " | " + (taken / n).ToString("F1") + " | " + (fromAlly / n).ToString("F1") + " | "
                              + (taken == 0 ? "—" : (100 * fromAlly / taken).ToString("F1") + "%") + " | " + string.Join("・", who.DefaultIfEmpty("—")) + " |");

            string cell(int st)
            {
                var w = perWave[st];
                if (w.Acts == 0) return "— | — | — |";
                double pain = w.Pain / w.Acts;
                double amount = Math.Max(KissPainFloorProbe, pain * KissPainPercentProbe / 100.0);
                return pain.ToString("F1") + " | " + amount.ToString("F1") + " | " + (w.Fires == 0 ? "—" : (w.Nom / w.Fires).ToString("F1")) + " |";
            }
            double acts25 = perWave.Sum(w => w.Acts), pain25 = perWave.Sum(w => w.Pain), nom25 = perWave.Sum(w => w.Nom), fires25 = perWave.Sum(w => w.Fires);
            q3.Add("| " + name + " | " + cell(3) + " " + cell(4) + " "
                   + (acts25 == 0 ? "— | — |" : (pain25 / acts25).ToString("F1") + " | " + (fires25 == 0 ? "—" : (nom25 / fires25).ToString("F1")) + " |"));
        }
        Console.WriteLine();

        Console.WriteLine("## Q0-3 1手番あいだに味方が失った HP（痛み）と、`PainPercent = " + KissPainPercentProbe + "`・`PainFloor = " + KissPainFloorProbe + "` の吸う量の見当");
        Console.WriteLine();
        Console.WriteLine("**痛み** ＝ リリが吸った手番（儀式を含む）の頭に測った「前の手番の終わりから味方が失った HP」の平均（`BattleContext.PainLostOf`・破片で受けた分は入らない）。"
                          + "**見当** ＝ max(床, 痛み × 割合) を**平均の痛みに**当てた値（手番ごとに当てた平均より小さく出うる）。**V0 名目** ＝ 第204期の1体あたりの名目（最大HPの 20%）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 四 痛み | 四 見当 | 四 V0 名目 | 五 痛み | 五 見当 | 五 V0 名目 | 2〜5 痛み | 2〜5 V0 名目 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string l in q3) Console.WriteLine(l);
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5 敵の攻撃力の上げ下げ");
        Console.WriteLine();
        Console.WriteLine("### 自分で `AtkBonus` を動かす札を持つ敵（`EnemyCatalog.Stages` 第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵 | 札 |");
        Console.WriteLine("|---|---|---|");
        int holders = 0;
        foreach (int st in Waves25)
        foreach (var (slot, d) in EnemyCatalog.Stages[st].Enemy.Occupied())
        {
            var hit = d.Traits.Where(SelfBonusTraits.Contains).ToList();
            if (hit.Count == 0) continue;
            holders++;
            Console.WriteLine("| 第" + (st + 1) + "波 | " + d.Name + "（" + SlotName(slot) + "） | " + string.Join("・", hit) + " |");
        }
        if (holders == 0) Console.WriteLine("| — | **0 体** | — |");
        Console.WriteLine();
        Console.WriteLine("### 1体ずつ吸った敵の `AtkBonus`（第2〜5波・吸った回あたり）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 吸った回/戦 | 正だった回/戦 | 正の平均 | 負だった回/戦 | 負の平均（絶対値） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var (band, name, f) in rows)
        {
            Led l = Collect(f, Waves25); UnitTally t = l.T;
            string P(double x) => (x / Math.Max(1, l.N)).ToString("F2");
            Console.WriteLine("| " + name + " | " + P(t.KissFires) + " | " + P(t.KissFoeBonusPos) + " | "
                              + (t.KissFoeBonusPos == 0 ? "—" : ((double)t.KissFoeBonusPosSum / t.KissFoeBonusPos).ToString("F1")) + " | "
                              + P(t.KissFoeBonusNeg) + " | " + (t.KissFoeBonusNeg == 0 ? "—" : ((double)t.KissFoeBonusNegSum / t.KissFoeBonusNeg).ToString("F1")) + " |");
        }
        Console.WriteLine();
    }

    /// <summary>Q0-3 の見当に使う値（指示書 §2.1 の規定。実装前なので診断の側に写す）。</summary>
    const int KissPainPercentProbe = 50, KissPainFloorProbe = 8;
}
