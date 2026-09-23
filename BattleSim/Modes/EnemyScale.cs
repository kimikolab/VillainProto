using System.Globalization;
using BattleCore;
using static Common;

// =====================================================================================
// escale モード（第187期） —— 敵の難易度のつまみ（数値で一律に強くする）
//
// 指示書は design/PHASE187_ENEMY_SCALE_SPEC.md ／ 報告は design/PHASE187_ENEMY_SCALE.md。
//
//     dotnet run --project BattleSim -c Release 0 escale phase0   # 現行 docs/balance.md の数え物（**戦闘0回**）
// =====================================================================================

static partial class EnemyScaleDiag
{
    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            default:
                Console.WriteLine("escale: モードは phase0。");
                return;
        }
    }

    /// <summary>第178〜186期に転生・手直しで触った駒（指示書 §4-3）。</summary>
    static readonly (string Id, string Name)[] Reborn =
    {
        ("hota", "ホタ"), ("utsu", "ウツ"), ("susu", "スス"), ("mudo", "ムド"), ("vio", "ヴィオ"),
        ("gan", "ガン"), ("vel", "ヴェル"), ("rau", "ラウ"), ("hisa", "ヒサ"), ("zan", "ザン"),
        ("kugu", "クグ"), ("shiga", "シガ"), ("ban", "バン"), ("sora", "ソラ"),
    };

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    /// <summary><c>docs/balance.md</c> を行名 → 5波の勝率で読む（位置ではなく行名で引く）。</summary>
    static Dictionary<string, double[]> ReadBalance(string path)
    {
        var d = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            string[] c = line.Split('|', StringSplitOptions.TrimEntries);
            // c[0] は空、c[1] は行名、c[2..6] が第1〜5波
            var w = new double[5];
            for (int i = 0; i < 5; i++)
                w[i] = double.Parse(c[2 + i].TrimEnd('%'), CultureInfo.InvariantCulture);
            d[c[1]] = w;
        }
        return d;
    }

    static void Phase0()
    {
        var bal = ReadBalance("docs/balance.md");
        var rows = CompareBuilds();
        Console.WriteLine("# 第187期 `escale phase0` —— 現行 `docs/balance.md` の数え物（戦闘0回）");
        Console.WriteLine();
        Console.WriteLine($"`compare` の行 {rows.Length} ／ `docs/balance.md` で引けた行 {rows.Count(r => bal.ContainsKey(r.Name))}");
        Console.WriteLine();

        // ---- 波ごとのセルの分布（第2〜5波） ----
        Console.WriteLine("## セルの分布（`compare` 61 行・波別）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 0.0% | (0,40) | [40,95] | (95,100) | 100.0% | 情報セル (0<x<100) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        int infoTotal = 0;
        for (int w = 1; w < 5; w++)
        {
            var v = rows.Where(r => bal.ContainsKey(r.Name)).Select(r => bal[r.Name][w]).ToList();
            int z = v.Count(x => x == 0), lo = v.Count(x => x > 0 && x < 40), mid = v.Count(x => x >= 40 && x <= 95),
                hi = v.Count(x => x > 95 && x < 100), top = v.Count(x => x == 100);
            infoTotal += v.Count(x => x > 0 && x < 100);
            Console.WriteLine($"| 第{w + 1}波 | {z} | {lo} | {mid} | {hi} | {top} | {v.Count(x => x > 0 && x < 100)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"- **情報セル（第2〜5波・0<x<100）合計 {infoTotal}**");
        int w5over = rows.Count(r => bal.TryGetValue(r.Name, out var x) && x[4] > 95);
        Console.WriteLine($"- **第五波 95% 超の行 {w5over}**");
        var prim = Baseline.PrimaryRows.Where(bal.ContainsKey).ToList();
        Console.WriteLine($"- 主判定19行の第五波平均 **{prim.Average(n => bal[n][4]):F1}%**（引けた {prim.Count} 行）");
        Console.Write("- 全61行の波平均 ");
        Console.WriteLine(string.Join(" / ", Enumerable.Range(0, 5).Select(w =>
            rows.Where(r => bal.ContainsKey(r.Name)).Average(r => bal[r.Name][w]).ToString("F1"))));
        int allTop = rows.Count(r => bal.TryGetValue(r.Name, out var x) && x.Skip(1).All(y => y == 100));
        Console.WriteLine($"- 第2〜5波がすべて 100.0% の行 **{allTop}**");
        Console.WriteLine();

        // ---- 転生した駒の在席 ----
        Console.WriteLine("## 転生した駒の在席（`compare` 61 行）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席行 | うち第2〜5波が全部 100% | 在席行の第2〜5波平均 | 在席行の情報セル |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach ((string id, string nm) in Reborn)
        {
            var rs = rows.Where(r => Has(r.F, id) && bal.ContainsKey(r.Name)).ToList();
            if (rs.Count == 0) { Console.WriteLine($"| {nm} | 0 | — | — | — |"); continue; }
            int top = rs.Count(r => bal[r.Name].Skip(1).All(y => y == 100));
            double avg = rs.Average(r => bal[r.Name].Skip(1).Average());
            int info = rs.Sum(r => bal[r.Name].Skip(1).Count(y => y > 0 && y < 100));
            Console.WriteLine($"| {nm} | {rs.Count} | {top} | {avg:F1} | {info} |");
        }
        int anyReborn = rows.Count(r => Reborn.Any(u => Has(r.F, u.Id)));
        Console.WriteLine();
        Console.WriteLine($"- 転生した駒を1枚でも含む行 **{anyReborn}** / {rows.Length}");
        Console.WriteLine();

        // ---- 敵の駒（5波） ----
        Console.WriteLine("## 敵の駒（`EnemyCatalog.Stages`）と倍率後の攻撃力");
        Console.WriteLine();
        Console.WriteLine("| 波 | 駒 | HP | 攻 | 型 | ×1.15 | ×1.30 | ×1.50 | ×1.75 | 札 |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|--:|---|");
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
            foreach ((int slot, UnitDef d) in EnemyCatalog.Stages[st].Enemy.Occupied())
            {
                string S(int p) { int a = Math.Max(1, d.Attack * p / 100); return a > 25 && st == 3 ? $"**{a}**" : a.ToString(); }
                Console.WriteLine($"| 第{st + 1}波 | {d.Name} | {d.MaxHp} | {d.Attack} | {d.Pattern} | {S(115)} | {S(130)} | {S(150)} | {S(175)} | "
                                  + string.Join("・", d.Traits) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("（第四波の太字は軛の上限 25 を超える一撃。第四波以外は軛の保持者がいないので太字にしない）");
    }
}
