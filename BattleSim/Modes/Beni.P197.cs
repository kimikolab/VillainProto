using BattleCore;
using static Common;

// =====================================================================================
// beni phase197（第197期） —— 紅蓮の前の数え物
//
// 指示書は design/PHASE197_BENI_GUREN_SPEC.md ／ 報告は design/PHASE197_BENI_GUREN.md。
//
//     dotnet run --project BattleSim -c Release 0 beni phase197   # Q0-1〜Q0-4（紅蓮の無い盤面＝第196期のまま・計数だけ）
// =====================================================================================

static partial class BeniDiag
{
    /// <summary>1行ぶんの「満タンで入りきらなかった溢れ」（第2〜5波・seed 0..199）。</summary>
    sealed class Waste197
    {
        public int N, WithWaste;
        public double Total;
        public double[] ByTurn = new double[31];
        public double[] Fires = new double[2], FirstSum = new double[2];
        public int[] FiredN = new int[2];
    }

    static readonly int[] Thresholds197 = { 12, 6 };

    static Waste197 WasteOf(Formation f)
    {
        var w = new Waste197();
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                w.N++;
                if (!r.TallyByUnit.TryGetValue("beni", out UnitTally? b)) continue;
                if (b.SipWaste > 0) w.WithWaste++;
                w.Total += b.SipWaste;
                if (b.SipWasteByTurn is null) continue;
                for (int t = 0; t < 31; t++) w.ByTurn[t] += b.SipWasteByTurn[t];
                // Q0-2 の見積もり: ターン t の頭の刻みで溜まった分を、そのターンのベニの手番で読む（倒れたターンの後は放たない）。
                // **放った後の盤面の変化（敵の毒・決着の短縮）は入れていない**——溜まる側だけの机上の回数。
                for (int k = 0; k < Thresholds197.Length; k++)
                {
                    long acc = 0; int fires = 0, first = 0;
                    for (int t = 1; t <= 30; t++)
                    {
                        acc += b.SipWasteByTurn[t];
                        if (b.Deaths > 0 && t > b.LastActiveTurn) break;
                        if (acc >= Thresholds197[k]) { fires++; acc = 0; if (first == 0) first = t; }
                    }
                    w.Fires[k] += fires;
                    if (fires > 0) { w.FiredN[k]++; w.FirstSum[k] += first; }
                }
            }
        return w;
    }

    static void Phase197()
    {
        Console.WriteLine("# 第197期 `beni phase197` —— Q0-1〜Q0-4（紅蓮の無い盤面＝第196期のまま・計数だけ）");
        Console.WriteLine();

        // ---------------- Q0-1 / Q0-2 ----------------
        Console.WriteLine("## Q0-1 / Q0-2 ベニが満タンで捨てた溢れと、放てる回数の見積もり");
        Console.WriteLine();
        Console.WriteLine("`捨てた` ＝ 啜りでベニへ流したのに、ベニが満タンで入りきらなかった量（`UnitTally.SipWaste`・第197期に足した計数。"
                          + "ベニへの流し込みが渇き・支援拒否で止まった回は数えない）。第2〜5波・seed 0..199・**1戦あたり**。"
                          + "`ターンの分布` は捨てた量の何割がそのターンに出たか。`見積もり` はターンごとの捨てた量を足していき、"
                          + "閾値に届いたターンのベニの手番で放って 0 に戻す机上の回数（倒れたターンの後は放たない・**放った後の盤面の変化は入れていない**）。");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行 | 捨てた/戦 | 捨てた戦 | 1T | 2T | 3T | 4〜6T | 7T〜 | 平均T | 12: 回/戦 | 12: 放った戦 | 12: 初回T | 6: 回/戦 | 6: 放った戦 | 6: 初回T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var groups = new List<(string Group, Waste197 W)>();
        foreach (var (band, name, f) in BeniRows()) groups.Add((band == "compare" ? "在席" : "在席（交差帯）", Print197(band == "compare" ? "在席" : "在席（交差帯）", name, f)));
        foreach (var (name, g) in SwapRows()) groups.Add(("差し替え", Print197("差し替え", name, g)));
        foreach (var (name, _, _, sf) in SidDiag.MainForms197()) groups.Add(("主表S", Print197("主表S", name, sf)));
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | 捨てた/戦 | 捨てた戦 | 1〜3T の割合 | 12: 回/戦 | 12: 放った戦 | 6: 回/戦 | 6: 放った戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string grp in new[] { "在席", "差し替え", "主表S" })
        {
            var l = groups.Where(x => x.Group.StartsWith(grp)).Select(x => x.W).ToList();
            if (l.Count == 0) { Console.WriteLine("| " + grp + " | 0 | | | | | | | |"); continue; }
            double tot = l.Sum(x => x.Total), early = l.Sum(x => x.ByTurn[1] + x.ByTurn[2] + x.ByTurn[3]);
            int n = l.Sum(x => x.N);
            Console.WriteLine("| " + grp + " | " + l.Count + " | " + (tot / n).ToString("F1") + " | " + P(l.Sum(x => x.WithWaste), n) + " | "
                              + (tot == 0 ? "—" : (100 * early / tot).ToString("F0") + "%") + " | "
                              + (l.Sum(x => x.Fires[0]) / n).ToString("F2") + " | " + P(l.Sum(x => x.FiredN[0]), n) + " | "
                              + (l.Sum(x => x.Fires[1]) / n).ToString("F2") + " | " + P(l.Sum(x => x.FiredN[1]), n) + " |");
        }
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 敵への着火・敵への毒で反応する札（`Traits.cs` の走査）");
        Console.WriteLine();
        Console.WriteLine("クラスの本文に `StatusKeys.Burn` / `StatusKeys.Poison` の読み取り（`Counter(` / `RawCounter(`）が出る札。"
                          + "保持者は味方（`UnitCatalog.All`）と第2〜5波の敵（`EnemyCatalog.Stages`）。**0 枚の札は盤面に出ない。**");
        Console.WriteLine();
        if (!ParryScan.Init()) return;
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        Console.WriteLine("| クラス | 燃焼を読む | 毒を読む | 味方の保持者 | 敵の保持者 |");
        Console.WriteLine("|---|:-:|:-:|---|---|");
        {
            string[] lines = traits.Split('\n');
            var bodies = new List<(string Cls, string Body)>();
            string cls = ""; var sb = new System.Text.StringBuilder();
            foreach (string l in lines)
            {
                int ci = l.IndexOf("class ");
                if (ci >= 0 && (l.Contains("sealed class") || l.Contains("abstract class")))
                {
                    if (cls.Length > 0) bodies.Add((cls, sb.ToString()));
                    cls = l.Substring(ci + 6).Split(' ', ':', '\r')[0]; sb.Clear();
                }
                if (!l.TrimStart().StartsWith("//")) sb.Append(l).Append('\n');
            }
            if (cls.Length > 0) bodies.Add((cls, sb.ToString()));
            int hits = 0;
            foreach (var (c, body) in bodies)
            {
                bool burn = body.Contains("Counter(" + "StatusKeys.Burn)"), poison = body.Contains("Counter(" + "StatusKeys.Poison)");
                if (!burn && !poison) continue;
                hits++;
                Console.WriteLine("| " + c + " | " + (burn ? "○" : "") + " | " + (poison ? "○" : "") + " | " + HoldersOf(c) + " | " + EnemyHoldersOf(c) + " |");
            }
            Console.WriteLine();
            if (hits == 0) { Console.WriteLine("**走査が空。止める**（R034）。"); return; }
        }
        Console.WriteLine("engine 側の窓口（本文から）:");
        Console.WriteLine();
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
        foreach (string needle in new[] { "bool relit = target.RawCounter(" + "StatusKeys.Burn) > 0;", "target.SetCounter(" + "StatusKeys.Burn, turns);",
                                          "if (Soak.Poison && " + "wounded)", "int turns = " + "BurnRules.Turns;" })
            Console.WriteLine("- `" + needle + "`: " + (engine.Contains(needle) ? "ある" : "**無い**"));
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 手番の頭で放つ入口");
        Console.WriteLine();
        Console.WriteLine("- ベニの札の並び: " + string.Join(" → ", UnitCatalog.Beni.Traits));
        Console.WriteLine("- ベニの手番の周期: " + string.Join(" → ", UnitCatalog.Beni.Actions!.Select(a => a.Label)));
        string skill = "foreach (Trait t in actor.Traits." + "ToList())";
        Console.WriteLine("- `TakeTurnCore` の `Skill` の枝は `EmitSkill` の後に `" + skill + "` で札を**並び順に** `OnAction` へ回す: "
                          + (engine.Contains(skill) ? "ある" : "**無い**"));
        Console.WriteLine();
    }

    static Waste197 Print197(string grp, string name, Formation f)
    {
        Waste197 w = WasteOf(f);
        double tot = w.Total;
        string Sh(double x) => tot == 0 ? "—" : (100 * x / tot).ToString("F0") + "%";
        double mid = w.ByTurn[4] + w.ByTurn[5] + w.ByTurn[6], late = w.ByTurn.Skip(7).Sum();
        double meanT = tot == 0 ? 0 : Enumerable.Range(0, 31).Sum(t => t * w.ByTurn[t]) / tot;
        string F(int k) => (w.Fires[k] / w.N).ToString("F2") + " | " + P(w.FiredN[k], w.N) + " | " + (w.FiredN[k] == 0 ? "—" : (w.FirstSum[k] / w.FiredN[k]).ToString("F2"));
        Console.WriteLine("| " + grp + " | " + name + " | " + (tot / w.N).ToString("F1") + " | " + P(w.WithWaste, w.N) + " | "
                          + Sh(w.ByTurn[1]) + " | " + Sh(w.ByTurn[2]) + " | " + Sh(w.ByTurn[3]) + " | " + Sh(mid) + " | " + Sh(late) + " | "
                          + (tot == 0 ? "—" : meanT.ToString("F2")) + " | " + F(0) + " | " + F(1) + " |");
        return w;
    }

    static string P(int a, int n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1") + "%";

    static string EnemyHoldersOf(string cls)
    {
        var hit = new List<string>();
        for (int st = 1; st < 5; st++)
            foreach ((int _, UnitDef d) in EnemyCatalog.Stages[st].Enemy.Occupied())
                if (d.Traits.Any(t => { try { return TraitCatalog.Get(t).GetType().Name == cls; } catch (KeyNotFoundException) { return false; } }))
                    hit.Add("第" + (st + 1) + "波 " + d.Name);
        return hit.Count == 0 ? "0 体" : string.Join("・", hit.Distinct());
    }
}
