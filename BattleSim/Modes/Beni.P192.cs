using BattleCore;
using static Common;

// =====================================================================================
// beni phase192（第192期） —— ベニ自身を結界の内側に入れる前の数え物
//
// 指示書は design/PHASE192_BENI_SELF_SPEC.md ／ 報告は design/PHASE192_BENI_SELF.md。
//
//     dotnet run --project BattleSim -c Release 0 beni phase192   # Q0-2 / Q0-3（**台本を読むだけ**・verbose で回す）
//
// 駒の InstanceId は `BattleContext.Add` の順（味方の編成 → 敵の編成 → 召喚）で振られるので、
// 味方は `Formation.Occupied()` の並びの 0.. 番、敵はその後ろに並ぶ。**ベニの `Skill` の書き手と
// 突き合わせて、ずれた戦の数を出す**（ずれていたら表は信用できない）。
// =====================================================================================

static partial class BeniDiag
{
    static void Phase192()
    {
        Console.WriteLine("# 第192期 `beni phase192` —— Q0-2 / Q0-3（ベニ自身に入ったもの・第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("在席2行と第190期 §5.2 の差し替え表 25 行。**盤面は今の版（ベニ自身は結界の外）**で、台本（verbose）の");
        Console.WriteLine("`StatusGain`（付いた毒・燃焼。書き手つき）／ `Status`（刻みの額面）／ `Heal`（ベニが受けた回復。出どころつき）を読むだけ。");
        Console.WriteLine();

        var rows = new List<(string Name, Formation F)>();
        foreach (var (_, n, f) in BeniRows()) rows.Add((n, f));
        foreach (var (n, g) in SwapRows()) rows.Add((n, g));

        var poisonBy = new Dictionary<string, (long Times, long Amount)>();
        var burnBy = new Dictionary<string, long>();
        var healBy = new Dictionary<string, (long Times, long Amount)>();
        long mismatch = 0, battles = 0;

        Console.WriteLine("| 行 | 毒の付与（層/戦・書き手の上位） | 火の付与（回/戦） | 刻みの額面 毒 / 燃焼（/戦） | 起爆の額面（/戦） | ベニが受けた回復（/戦・出どころ） | ベニの被ダメ総量（/戦） |");
        Console.WriteLine("|---|---|--:|---|--:|---|--:|");
        foreach (var (name, f) in rows)
        {
            var pBy = new Dictionary<string, long>(); var hBy = new Dictionary<string, long>();
            long burnGain = 0, tickP = 0, tickB = 0, det = 0, taken = 0; int n = 0;
            var players = f.Occupied().ToList();
            int beniIx = players.FindIndex(o => o.Def.Id == "beni");
            for (int st = 1; st < 5; st++)
            {
                Formation enemy = EnemyCatalog.Stages[st].Enemy;
                var foes = enemy.Occupied().ToList();
                string NameOf(int? id)
                {
                    if (id is null) return "（出どころなし）";
                    int i = id.Value;
                    if (i < players.Count) return players[i].Def.Name;
                    if (i < players.Count + foes.Count) return "敵・" + foes[i - players.Count].Def.Name;
                    return "召喚";
                }
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true);
                    n++; battles++;
                    if (r.TallyByUnit.TryGetValue("beni", out UnitTally? bt)) taken += bt.DamageTaken;
                    var sk = r.Events.FirstOrDefault(e => e.Kind == BattleEventKind.Skill
                                                          && (e.Text == KindleTrait.Label || e.Text == "澱みを分けた"));
                    if (sk is not null && sk.ActorId != beniIx) mismatch++;
                    foreach (BattleEvent e in r.Events)
                    {
                        if (e.TargetId != beniIx) continue;
                        if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Poison)
                        {
                            string w = NameOf(e.ActorId);
                            pBy[w] = pBy.GetValueOrDefault(w) + e.Amount;
                            var cur = poisonBy.GetValueOrDefault(w); poisonBy[w] = (cur.Times + 1, cur.Amount + e.Amount);
                        }
                        else if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Burn)
                        {
                            burnGain++;
                            string w = NameOf(e.ActorId); burnBy[w] = burnBy.GetValueOrDefault(w) + 1;
                        }
                        else if (e.Kind == BattleEventKind.Status && e.ActorId is null)
                        {
                            if (e.Text == "毒") tickP += e.Amount; else if (e.Text == "燃焼") tickB += e.Amount;
                        }
                        else if (e.Kind == BattleEventKind.Status) det += e.Amount;   // 起爆（書き手 ＝ カタ）
                        else if (e.Kind == BattleEventKind.Heal)
                        {
                            string w = NameOf(e.ActorId);
                            hBy[w] = hBy.GetValueOrDefault(w) + e.Amount;
                            var cur = healBy.GetValueOrDefault(w); healBy[w] = (cur.Times + 1, cur.Amount + e.Amount);
                        }
                    }
                }
            }
            string Top(Dictionary<string, long> d) => d.Count == 0 ? "—"
                : string.Join("・", d.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key + " " + ((double)kv.Value / n).ToString("F1")));
            Console.WriteLine("| " + name + " | " + ((double)pBy.Values.Sum() / n).ToString("F1") + "（" + Top(pBy) + "） | "
                              + ((double)burnGain / n).ToString("F2") + " | " + ((double)tickP / n).ToString("F1") + " / " + ((double)tickB / n).ToString("F1")
                              + " | " + ((double)det / n).ToString("F1") + " | " + ((double)hBy.Values.Sum() / n).ToString("F1") + "（" + Top(hBy) + "） | " + ((double)taken / n).ToString("F1") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 突き合わせ: ベニの `Skill` の書き手が推定の InstanceId とずれた戦 **" + mismatch + " / " + battles + "**（0 が正）");
        Console.WriteLine();

        Console.WriteLine("## 27 行の合計（書き手別・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 種類 | 書き手／出どころ | 回/戦 | 量/戦 |");
        Console.WriteLine("|---|---|--:|--:|");
        foreach (var kv in poisonBy.OrderByDescending(kv => kv.Value.Amount))
            Console.WriteLine("| 毒の付与 | " + kv.Key + " | " + ((double)kv.Value.Times / battles).ToString("F2") + " | " + ((double)kv.Value.Amount / battles).ToString("F2") + " |");
        foreach (var kv in burnBy.OrderByDescending(kv => kv.Value))
            Console.WriteLine("| 火の付与 | " + kv.Key + " | " + ((double)kv.Value / battles).ToString("F2") + " | — |");
        foreach (var kv in healBy.OrderByDescending(kv => kv.Value.Amount))
            Console.WriteLine("| 回復 | " + kv.Key + " | " + ((double)kv.Value.Times / battles).ToString("F2") + " | " + ((double)kv.Value.Amount / battles).ToString("F2") + " |");
        Console.WriteLine();
    }
}
