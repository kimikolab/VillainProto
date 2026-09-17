using BattleCore;
using static Common;

// =====================================================================================
// suture モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "suture")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 suture
// =====================================================================================

static class SutureDiag
{
// suture モード（第39期・使い捨ての診断）: 縫い（ハリ）の繕いと塞ぎを数え、
// **第三波の値が渇きのせいであることを同数値対照で証明する。**
//
// **ここもログの文字列を数えている**（`gullet log` / `yoke log` / `hush` / `sever` と同じ理由）。
// 繕いは `ctx.Heal` を1回通るだけなので回復の総量に溶けるし、**渇きに封じられた繕いは
// 盤面の値に痕跡を1つも残さない**（`Heal` が入口で return するので tally も Events も動かない）。
// 「その行が出たか／何回出たか」を数える以外に発火を捕まえる方法が無い。
//
// **`docs/` には置かない。** 標準出力で読むだけ。
//
//     dotnet run --project BattleSim -c Release 0 suture [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    var sutBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> sutStages = EnemyCatalog.Stages;
    const int SutSeeds = 50;      // 機構の指標（verbose のログ行）を数える本数。sever と揃える
    const int SutRateSeeds = 200; // 勝率を測り直す本数。compare / spread と揃える（セルを突き合わせる）

    string hari = UnitCatalog.Hari.Name;
    string nomi = UnitCatalog.Nomi.Name;
    string droughter = EnemyCatalog.Droughter.Name;

    // 第39期に compare から落とした対照（`裂き×縫い (キリ×ハリ)`）を**診断のローカルに組む**
    // （`gradient` / `aim` / `route` / `sever` と同じ扱い）。**`CompareBuilds()` には戻さない**
    // ——戻すと `docs/balance.md` の行が増えて「既存43行 ±0.0」の分母が動く。
    // 配置は confirm で据え置きになった仮置き（reseat 1位は -2.2pt で不採用）。
    (string Name, Formation F) sutControl = ("裂き×縫い (キリ×ハリ)", Formation.Build(
        front1: UnitCatalog.Kiri, front3: UnitCatalog.Golm, center: UnitCatalog.Dolga,
        back1: UnitCatalog.Vel, back3: UnitCatalog.Hari));

    string sutFilter = args.Length > 2 && args[2].Length > 0 ? args[2] : "縫い,刻み×抉り";
    var sutRows = sutBuilds
        .Select(x => (Name: x.Name, F: x.F))
        .Append(sutControl)
        .Where(x => sutFilter.Split(',').Any(k => x.Name.Contains(k.Trim())))
        .ToList();

    // 「（傷 w → +x、傷 y へ）」の w を取り出す。書式の頭は裂き・抉り・刻み・断ちと共通。
    static int SutWoundOf(string text)
    {
        int a = text.IndexOf("（傷 ", StringComparison.Ordinal);
        if (a < 0) return 0;
        a += 3;
        int b = text.IndexOf(' ', a);
        return b > a && int.TryParse(text[a..b], out int w) ? w : 0;
    }

    //  0 繕いの発火 / 1 読んだ傷の総和 / 2 ハリの振り / 3 見定め / 4 逸れた振り
    //  5 空振り（振ったが繕えなかった）/ 6 封じられた発火（渇きの保持者が生きている間）
    //  7 封じられた繕い量 / 8 解禁後の発火 / 9 解禁後の繕い量 / 10 祭司を割った戦
    // 11 ノミのなぞり発火 / 12 なぞり上乗せ総量 / 13 戦数 / 14 塞ぎ（傷を1つ減らした回数）
    //
    // **6 と 8 を分けるのが第39期の主眼**——どちらも同じ発火だが、6 は `ctx.Heal` が
    // 入口で return して1点も届いていない。合算すると「繕いが細い」のか「封じられた」のかが決まらない。
    // **塞ぎ（14）は 6 でも走る**ので必ず発火数と一致する（一致しなければ実装が親切をしている）。
    var sacc = new Dictionary<(int Row, int Wave), double[]>();

    for (int r = 0; r < sutRows.Count; r++)
        for (int w = 0; w < sutStages.Count; w++)
        {
            var a = new double[15];
            for (int seed = 0; seed < SutSeeds; seed++)
            {
                BattleResult res = BattleEngine.Run(sutRows[r].F, sutStages[w].Enemy, seed, verbose: true);
                a[13]++;

                // 渇きの保持者が生きているか。**波に祭司がいなければ最初から解禁**。
                bool droughtAlive = sutStages[w].Enemy.Occupied()
                    .Any(x => x.Def.Id == EnemyCatalog.Droughter.Id);
                bool sawPriestDeath = false;

                string intended = "", swungAt = "";
                bool swinging = false, fired = false;

                void CloseSwing()
                {
                    if (!swinging) return;
                    if (!fired) a[5]++;
                    if (intended.Length > 0 && swungAt.Length > 0 && intended != swungAt) a[4]++;
                    swinging = false; fired = false; intended = ""; swungAt = "";
                }

                foreach (LogLine l in res.Log)
                {
                    string t = l.Text;
                    if (l.Kind == LogKind.Turn) { CloseSwing(); continue; }

                    // 祭司の死。**名前で引ける**（渇きの祭司は波に1体きりで、他の def と名前が衝突しない）。
                    if (l.Kind == LogKind.Death && t.Contains($"{droughter} は倒れた"))
                    { droughtAlive = false; sawPriestDeath = true; continue; }

                    if (t.Contains($"{hari} は ") && t.Contains(" の傷口を見定めた"))
                    {
                        a[3]++;
                        int p1 = t.IndexOf($"{hari} は ", StringComparison.Ordinal) + hari.Length + 3;
                        int p2 = t.IndexOf(" の傷口を見定めた", StringComparison.Ordinal);
                        intended = p2 > p1 ? t[p1..p2] : "";
                        continue;
                    }

                    if (t.Contains($"{hari} → "))
                    {
                        CloseSwing();
                        swinging = true;
                        a[2]++;
                        int p1 = t.IndexOf($"{hari} → ", StringComparison.Ordinal) + hari.Length + 3;
                        int p2 = t.IndexOf(" (攻撃", p1, StringComparison.Ordinal);
                        swungAt = p2 > p1 ? t[p1..p2] : "";
                        continue;
                    }

                    if (t.Contains($"{hari} が ") && t.Contains("の傷口から糸を引き"))
                    {
                        int wd = SutWoundOf(t);
                        a[0]++; a[1] += wd; a[14]++; fired = true;
                        if (droughtAlive) { a[6]++; a[7] += SutureTrait.PerWound * wd; }
                        else { a[8]++; a[9] += SutureTrait.PerWound * wd; }
                        continue;
                    }

                    if (t.Contains($"{nomi} が ") && t.Contains("の古い傷をなぞる"))
                    { a[11]++; a[12] += CarveTrait.PerWound * SutWoundOf(t); continue; }
                }
                CloseSwing();
                if (sawPriestDeath) a[10]++;
            }
            sacc[(r, w)] = a;
        }

    Console.WriteLine("# 縫い（第39期・診断。docs/ には置かない）");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{SutSeeds - 1} × 全波、verbose のログ行を数えた。数字は**1戦あたり**。");
    Console.WriteLine($"繕い量は `SutureTrait.PerWound`({SutureTrait.PerWound}) × 傷の**名目値**");
    Console.WriteLine("（HP上限で切られた分を含む。封じられた分は1点も届いていない）。");
    Console.WriteLine();
    Console.WriteLine("- `振` ハリが攻撃を振った回数 / `見定` 傷選好が働いた回数（傷持ちが狙えた手番）");
    Console.WriteLine("- `繕い` 発火した回数 / `傷/繕い` 1回で読んだ傷の平均（**塞ぎは常に1つ**）");
    Console.WriteLine("- `塞ぎ` 傷を1つ減らした回数。**発火数と必ず一致する**（渇き下でも走るのが仕様）");
    Console.WriteLine("- `逸れ` 見定めた相手と実際に殴った相手が違った振り（介入の鎖が上書きした）");
    Console.WriteLine("- `空振` 振ったが繕えなかった回数（傷が無い相手を殴った／患者がいない）");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 波 | 振 | 見定 | 繕い | 傷/繕い | 塞ぎ | 繕い量(名目) | 逸れ | 空振 | なぞり回 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int r = 0; r < sutRows.Count; r++)
        for (int w = 0; w < sutStages.Count; w++)
        {
            double[] a = sacc[(r, w)];
            double n = a[13];
            string per = a[0] > 0 ? $"{a[1] / a[0]:0.00}" : "—";
            Console.WriteLine($"| {sutRows[r].Name} | 第{w + 1}波 | {a[2] / n:0.00} | {a[3] / n:0.00} | "
                + $"{a[0] / n:0.00} | {per} | {a[14] / n:0.00} | {SutureTrait.PerWound * a[1] / n:0.0} | "
                + $"{a[4] / n:0.00} | {a[5] / n:0.00} | {a[11] / n:0.00} |");
        }

    Console.WriteLine();
    Console.WriteLine("## 第三波（渇き）の封じ");
    Console.WriteLine();
    Console.WriteLine("`封じ` は渇きの祭司が生きている間に出た繕い（**1点も届いていない**）。");
    Console.WriteLine("`解禁` は祭司を割った後の繕い。**塞ぎは封じの側でも走っている**ので、");
    Console.WriteLine("この波はハリの編成に**二重に**課金する（回復の封じ ＋ 傷という資源の目減り）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 繕い/戦 | 封じ/戦 | 封じ量 | 解禁/戦 | 解禁量 | 封じ率 | 祭司を割った戦 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    for (int r = 0; r < sutRows.Count; r++)
    {
        double[] a = sacc[(r, 2)];
        double n = a[13];
        string rate = a[0] > 0 ? $"{100.0 * a[6] / a[0]:0.0}%" : "—";
        Console.WriteLine($"| {sutRows[r].Name} | {a[0] / n:0.00} | {a[6] / n:0.00} | {a[7] / n:0.0} | "
            + $"{a[8] / n:0.00} | {a[9] / n:0.0} | {rate} | {100.0 * a[10] / n:0.0}% |");
    }

    Console.WriteLine();
    Console.WriteLine("## 第五波（殉教者 p=75）の介入");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 振 | 見定 | 逸れ | 逸れ率（見定めた振りのうち） |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int r = 0; r < sutRows.Count; r++)
    {
        double[] a = sacc[(r, sutStages.Count - 1)];
        double n = a[13];
        string rate = a[3] > 0 ? $"{100.0 * a[4] / a[3]:0.0}%" : "—";
        Console.WriteLine($"| {sutRows[r].Name} | {a[2] / n:0.00} | {a[3] / n:0.00} | {a[4] / n:0.00} | {rate} |");
    }

    // ---- 渇きの帰属（同数値対照）------------------------------------------------------
    //
    // **第三波の値が渇きのせいであることを、対照で証明する。** 渇きの祭司（Droughter）と
    // 巡礼騎士（Knight）は HP・攻・速さ・型が同一で、違いは盤面ルールを1つ持つかだけ
    // ——差し替えで動いた分は**渇きの税額そのもの**になる（第34期の交絡＝HP を動かして
    // しまう罠を、同数値の対照で構造的に避ける）。
    //
    // **対照行（回復を持たない `刻み×抉り`）が ±0.0 であることが診断の検算。**
    // ここが動いたら、差し替え自体が盤面を変えている（＝帰属に使えない測定）。
    Console.WriteLine();
    Console.WriteLine("## 渇きの帰属（同数値対照・第三波）");
    Console.WriteLine();
    Console.WriteLine("第三波の中央を **渇きの祭司 ↔ 巡礼騎士** に差し替えた（HP・攻・速さ・型は同一）。");
    Console.WriteLine($"seed 0..{SutRateSeeds - 1}（compare と同じ帯）。`渇きあり` は `docs/balance.md` の第三波と一致するはず。");
    Console.WriteLine();
    Console.WriteLine("**前提の訂正（第39期）**: 指示書は対照に `刻み×抉り` を指定していたが、");
    Console.WriteLine("**この行は回復を持っている**——ゴルムの吸い（`DrainTrait`）と巨躯の還し（`ColossusTrait`）が");
    Console.WriteLine("どちらも `ctx.Heal` を通る（README「駒の説明文から数えると必ず抜ける」の再演）。");
    Console.WriteLine("傷軸の5行はすべて土台にゴルムを持つので、**傷軸の中に回復ゼロの行は1つも無い。**");
    Console.WriteLine("そこで検算用に **`対照 (回復ゼロ)`** を診断のローカルに組んだ——`刻み×抉り` の");
    Console.WriteLine("ゴルムをガルド（`Guardian`+`Stoic`。回復経路なし）に差し替えただけの版で、");
    Console.WriteLine("**この行が ±0.0 であることが「差し替え自体は盤面を変えていない」の証明。**");
    Console.WriteLine("`刻み×抉り` の側は**土台（ゴルム）が払っている税額**として読む。");
    Console.WriteLine();
    Console.WriteLine("`回復回` は渇きなし版で実際に通った `Heal` の回数（1戦あたり）。**0 なら渇きは無風のはず。**");
    Console.WriteLine();

    Formation stage3 = sutStages[2].Enemy;
    var stage3NoDrought = new Formation();
    for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
    {
        UnitDef? d = stage3[i];
        stage3NoDrought[i] = d is null ? null
            : d.Id == EnemyCatalog.Droughter.Id ? EnemyCatalog.Knight : d;
    }

    // 回復経路ゼロの検算行。**ゴルム（吸い＋還し）だけを抜いてある**ので、
    // 渇きが触れる窓口が1つも無い＝差し替えは1試行も動かせない。
    var sutAttrib = sutRows.Append((Name: "対照 (回復ゼロ)", F: Formation.Build(
        front1: UnitCatalog.Egu, front3: UnitCatalog.Gald, center: UnitCatalog.Nomi,
        back1: UnitCatalog.Dolga, back3: UnitCatalog.Vel))).ToList();

    Console.WriteLine("| 編成 | 渇きあり | 渇きなし（巡礼騎士） | 税額 | 回復回（渇きなし） |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (var row in sutAttrib)
    {
        double with = 0, without = 0, heals = 0;
        for (int seed = 0; seed < SutRateSeeds; seed++)
        {
            if (BattleEngine.Run(row.F, stage3, seed, verbose: false).PlayerWon) with++;
            BattleResult free = BattleEngine.Run(row.F, stage3NoDrought, seed, verbose: true);
            if (free.PlayerWon) without++;
            heals += free.Events.Count(e => e.Kind == BattleEventKind.Heal);
        }
        double a = with * 100.0 / SutRateSeeds, b = without * 100.0 / SutRateSeeds;
        Console.WriteLine($"| {row.Name} | {a:0.0}% | {b:0.0}% | {b - a:+0.0;-0.0}pt | {heals / SutRateSeeds:0.00} |");
    }
    return;
}
}
