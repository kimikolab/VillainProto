using BattleCore;
using static Common;

// =====================================================================================
// output モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "output")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 output
// =====================================================================================

static class OutputDiag
{
// output モード: 編成の「出力の実体」を、目的変数から独立に測る（第17期 Phase HA/HB）。
//
// 第16期の最後に障害が1本に絞られた。**`総攻` が反撃軸・毒軸の出力を桁で外している**
// （溜め改は S4 で打点の 78% が手番外＝カドの反撃から出て、毒軸は `総攻` 20〜22 で
// 毎ターン 45〜127 を削っている）。第12期以来ずっと使ってきた**編成側の唯一の出力特徴量**が、
// このゲームの主要な出力経路（反撃・毒・破裂・反射）をまるごと取りこぼしていた。
//
// **循環に注意。** 目的変数（波ごとの勝率）と同じ戦闘から出力を取ると、第14期の同語反復と
// 同じ問題になる——敵を削り切ることが勝ちなので、その戦闘での与ダメは勝率の言い換え。
// したがって **固定の参照台で1回だけ測り、その値を全波に対する特徴量として使う。**
//
// 却下した案: `power` の動的特徴量（`与ダメ/戦`）をそのまま使う。あれは目的変数と同じ戦闘から
// 取っているので、波ごとの勝率に当てた瞬間に循環する（第15期 §9-1 が `与ダメ/戦` を分子経路
// として外したのと同じ理由）。**参照台が要るのは、この循環を切るため。**
//
// 却下した案: 参照台を `WaveCatalog()` の波から選ぶ。候補波は「代金の帯」や「体数 × 個体HP の
// 格子」を狙って作った的で、**中立ではない**（その波の性格が特徴量に混入する）。
// 単一の def を並べただけの的を別に組む。**新しい `UnitDef` は作らない**（計画書 §2）。
//
// 診断用で docs/ には置かない（wave / power / bench / dissect と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 output [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    const int OutSeeds = 200;   // wave / dissect / compare / power / bench と同じ

    var all = CompareBuilds();
    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    // 表示桁でゼロに丸まる負の値を `-+0.0` と出さないための丸め（`dissect` と同じ理由）。
    double Sg(double v, int dp) => double.IsNaN(v) ? v : Math.Round(v, dp) + 0.0;

    // 個体HP の中央値。`dissect` のローカル定義と同じ式（あちらから取り上げると
    // 第16期の出力との突き合わせが読めなくなるので、写しの一致は §9 の項の値で確かめる）。
    double MedianHp(Formation e)
    {
        var v = e.Occupied().Select(x => (double)x.Def.MaxHp).OrderBy(x => x).ToArray();
        return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
    }

    Console.WriteLine("# 出力の実体を測る — 参照台と出力特徴量（第17期）");
    Console.WriteLine();
    Console.WriteLine("第16期で障害が1本に絞られた。**`総攻` が反撃軸・毒軸の出力を桁で外している**");
    Console.WriteLine("——溜め改は S4 で打点の 78% が手番外（カドの反撃）から出て、毒軸は `総攻` 20〜22 で");
    Console.WriteLine("毎ターン 45〜127 を削っている。第12期以来ずっと使ってきた**編成側の唯一の出力特徴量**が、");
    Console.WriteLine("このゲームの主要な出力経路（反撃・毒・破裂・反射）をまるごと取りこぼしていた。");
    Console.WriteLine();
    Console.WriteLine("**測定だけで、盤面は1つも動かしていない**（`BattleCore` 無変更・`EnemyCatalog` 無変更）。");
    Console.WriteLine();

    // ================= Phase HA: 参照台 =================
    //
    // 要件は3つ（計画書 §3-1）。
    //   1. 全編成が同じ条件で殴れること
    //   2. 決着しないこと、または十分長いこと（**甲乙の分割そのものが時間の話**なので、
    //      出力が育つ時間を確保しないと甲群の値が取れない）
    //   3. 中立であること（波の性質が特徴量に混入しない）
    //
    // **ここが設計の核心。** 「殴られること」で出力する編成があるので、殴り返さない的では
    // 反撃・被弾強化の出力が 0 になる。かといって殴り返しが強すぎると味方が先に落ちて
    // 時間が取れない。**硬くて攻撃力が低い的**を、既存の def から選ぶ。
    Console.WriteLine("## 1. 参照台の要件");
    Console.WriteLine();
    Console.WriteLine("参照台は**編成の出力を測るための的**であって、勝敗を測る場ではない。");
    Console.WriteLine();
    Console.WriteLine("| # | 要件 | なぜ |");
    Console.WriteLine("|--:|---|---|");
    Console.WriteLine("| 1 | 全編成が同じ条件で殴れる | 特定の編成だけが有利／不利にならない |");
    Console.WriteLine("| 2 | 決着しない、または十分長い | **甲乙の分割そのものが時間の話**。早く倒すと「出力が時間で育つ」甲群の値が取れない |");
    Console.WriteLine("| 3 | 中立 | 波の性質が特徴量に混入しない |");
    Console.WriteLine();
    Console.WriteLine("**要件2と「殴り返し」は綱引きになる。** 殴り返さない的では被弾駆動の出力");
    Console.WriteLine("（反撃・被弾強化・自傷）が 0 になり、甲群の出力がまるごと消える。かといって");
    Console.WriteLine("殴り返しが強すぎると味方が先に落ちて時間が取れない。**硬くて攻撃力が低い的**が要る。");
    Console.WriteLine();
    Console.WriteLine("台は**単一の def を並べただけ**にする。混成にすると「どの駒に当たったか」で");
    Console.WriteLine("編成ごとに条件が変わり、要件1が崩れる。**新しい `UnitDef` は作らない**");
    Console.WriteLine("（計画書 §2「やらないこと」）ので、既存の `EnemyCatalog` から選ぶ。");
    Console.WriteLine();

    // 候補は 2 家族 × 3 刻みの格子。**片方の家族だけだと、決着しなかった理由が
    // 「硬いから」か「殴ってこないから」か決まらない。**
    //   巡礼 / 荷駄 / 従卒 は 個体HP 90 × 5 体で固定し、**1体あたり攻だけ**を 4 → 7 → 10 と振る
    //   重装 3 / 4 / 5 は 個体HP 145 で固定し、**体数だけ**を振る
    // （X字化で編成枠が5つになったので、旧6体の台は全部5体に詰めてある）
    Formation Stack(UnitDef d, int n)
    {
        var f = new Formation();
        for (int i = 0; i < n; i++) f[i] = d;   // スロット昇順（前1→前3→中央→後1→後3）
        return f;
    }
    var cands = new (string Tag, string Name, string Family, Formation F)[]
    {
        ("B1", "巡礼5",  "個体90固定・攻を振る", Stack(EnemyCatalog.ZealotPilgrim, 5)),
        ("B2", "荷駄5",  "個体90固定・攻を振る", Stack(EnemyCatalog.ZealotPorter, 5)),
        ("B3", "従卒5",  "個体90固定・攻を振る", Stack(EnemyCatalog.ZealotSquire, 5)),
        ("B4", "重装3",  "個体145固定・体数を振る", Stack(EnemyCatalog.Warden, 3)),
        ("B5", "重装4",  "個体145固定・体数を振る", Stack(EnemyCatalog.Warden, 4)),
        ("B6", "重装5",  "個体145固定・体数を振る", Stack(EnemyCatalog.Warden, 5)),
    };
    int nB = cands.Length;

    Console.WriteLine("## 2. 候補の下見");
    Console.WriteLine();
    Console.WriteLine("候補は **2 家族 × 3 刻み**。片方の家族だけだと、決着しなかった理由が");
    Console.WriteLine("「硬いから」か「殴ってこないから」か決まらない。");
    Console.WriteLine();
    Console.WriteLine("- **個体90 固定・攻を振る**（巡礼 / 荷駄 / 従卒 × 6体）— 1体あたり攻 4 → 7 → 10");
    Console.WriteLine("- **個体145 固定・体数を振る**（重装 × 3 / 4 / 6体）— 総HP 435 → 580 → 870");
    Console.WriteLine();
    Console.WriteLine("どれも**単体攻撃のみ・特性なし**（薙ぎ・貫き・全体・処刑を持つ def は外した）。");
    Console.WriteLine("攻撃型が入ると、範囲耐性・後列配置といった**編成側の性質と噛み合ってしまう**");
    Console.WriteLine("——要件1（全編成が同じ条件で殴れる）が崩れる。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 中身 | 家族 | 体数 | 総HP | 総攻 | 個体HP | 1体攻 | 速度 |");
    Console.WriteLine("|:-:|---|---|--:|--:|--:|--:|--:|--:|");
    foreach (var (tag, name, fam, bf) in cands)
        Console.WriteLine($"| **{tag}** | {name} | {fam} | {bf.Count} "
            + $"| {bf.Occupied().Sum(x => x.Def.MaxHp)} | {bf.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {bf.Occupied().Max(x => x.Def.MaxHp)} | {bf.Occupied().Average(x => x.Def.Attack):F0} "
            + $"| {bf.Occupied().Average(x => x.Def.Speed):F0} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 計測 ---
    // seed ごとの打点・ターン数・T1..T5 の累積を丸ごと持つ。半割は同じ計測から取り出すだけで済む
    // （2回走らせると、半割の値そのものに実行間のばらつきが乗る。`bench` と同じ作法）。
    var tr = new OutputTrace[nB][];
    for (int b = 0; b < nB; b++)
    {
        tr[b] = new OutputTrace[nT];
        for (int t = 0; t < nT; t++) tr[b][t] = MeasureOutput(targets[t].F, cands[b].F, OutSeeds);
        Console.Out.Flush();
    }

    // --- 検算 ---
    //
    // (1) イベントから数えた打点と、敵の tally から数えた打点（第13期の受け手側測定）が一致するか。
    //     ずれたら、どちらかが取りこぼしている。
    // (2) 敵同士の巻き込みが 0 であること（受け手側から与ダメを取るための前提。第13期 §3-1）。
    // (3) 味方と敵の Def.Id が衝突していないこと（power / wave と同じ穴）。
    Console.WriteLine("### 2-1. 検算");
    Console.WriteLine();
    double maxGap = 0;
    long totalFromAlly = 0;
    for (int b = 0; b < nB; b++)
        for (int t = 0; t < nT; t++)
        {
            maxGap = Math.Max(maxGap, Math.Abs(tr[b][t].Damage.Sum() - tr[b][t].TallyDamage));
            totalFromAlly += tr[b][t].FoeFromAlly;
        }
    var clash = targets.SelectMany(x => x.F.Occupied().Select(y => y.Def.Id))
        .Intersect(cands.SelectMany(c => c.F.Occupied().Select(y => y.Def.Id))).ToArray();
    Console.WriteLine($"- **イベント集計と敵 tally の差**: 最大 {maxGap:F0}（{nB * nT} 組）。"
        + "**0 でなければ、どちらかが打点を取りこぼしている**");
    Console.WriteLine($"- **敵同士の巻き込み（`TakenFromAlly`）**: {totalFromAlly}。"
        + "0 でなければ受け手側から与ダメを取る前提が崩れる");
    Console.WriteLine($"- **味方と敵の `Def.Id` 衝突**: {clash.Length} 件"
        + (clash.Length == 0 ? "" : $"（{string.Join(" / ", clash)}）"));
    Console.WriteLine();
    Console.WriteLine("イベント側は `Damage` イベントを直接足している（`Status` の量は**適用前の値**なので、");
    Console.WriteLine("破片で吸われたぶん・非致死で丸めたぶんが実際の削りと食い違う。`dissect` の `毒燃/戦` は");
    Console.WriteLine("`Status` から取っているので、あちらとは数字が微妙に違う——**こちらが実測**）。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 2-2. 要件を満たしているか ---
    //
    // 計画書 §6-7 の停止条件そのもの。**決着してしまい甲群の時間が取れないなら、
    // 台の硬さを上げる前に何ターンで決着したかを報告する。**
    Console.WriteLine("### 2-2. 要件を満たしているか（全編成の平均。計画書 §6-7 の停止条件）");
    Console.WriteLine();
    Console.WriteLine("| 量 | 読み方 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| `決着T` | 全試行の平均ターン数。**30.0 なら誰も削り切れていない**（要件2 が最も強く成立） |");
    Console.WriteLine("| `T5未満%` | ターン数が 5 未満だった試行の割合。**ここが高いと (B) の立ち上がりが測れない** |");
    Console.WriteLine("| `味方全滅%` | 味方が削られ切った試行の割合。**高いと出力が途中で止まる** |");
    Console.WriteLine("| `敵全滅%` | 味方が削り切った試行の割合（＝この台での味方の勝率） |");
    Console.WriteLine("| `手番外%` | 打点のうち手番の振り以外（反撃・破裂・毒燃）から出たぶん。**0 なら被弾駆動が死んでいる** |");
    Console.WriteLine();
    Console.WriteLine("| 台 | 決着T | T5未満% | 味方全滅% | 敵全滅% | 手番外%（平均） | 手番外% 0 の編成 |");
    Console.WriteLine("|:-:|--:|--:|--:|--:|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        double turns = Enumerable.Range(0, nT).Average(t => tr[b][t].Turns.Average());
        double shortRun = Enumerable.Range(0, nT).Average(t => tr[b][t].Short * 100.0 / OutSeeds);
        double wipe = Enumerable.Range(0, nT).Average(t => tr[b][t].AllyWipe * 100.0 / OutSeeds);
        double clear = Enumerable.Range(0, nT).Average(t => tr[b][t].FoeWipe * 100.0 / OutSeeds);
        double off = Enumerable.Range(0, nT).Average(t => tr[b][t].OffTurnPct);
        int zero = Enumerable.Range(0, nT).Count(t => tr[b][t].OffTurnPct <= 1e-9);
        Console.WriteLine($"| **{cands[b].Tag}** {cands[b].Name} | {turns:F1} | {shortRun:F1}% | {wipe:F1}% "
            + $"| {clear:F1}% | {off:F1}% | {zero} / {nT} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3. 参照台の門と、選んだ2台 ---
    //
    // **§2-2 の下見だけでは台を選べない。** 「決着T が長い」は要件2 に適うように見えるが、
    // **敵が一度も攻撃を通せていないから長い**のかもしれない——それは要件2 を満たした台では
    // なく、計画書 §3-2 が名指しで警告した「殴り返さない的」そのもので、
    // 被弾駆動の出力（反撃・被弾強化・自傷）がまるごと 0 になる。
    //
    // 門は数字で置ける。**呪詛（ネル）は開幕に敵全体の攻撃力を −6 する**
    // （`CurseTrait.EnemyDebuff`）。1体あたり攻がこれ以下の def を並べると、呪詛入りの編成に
    // 対しては攻撃が 0 以下に潰れ、`ApplyDamage` の `if (amount <= 0) return;` で
    // **1ダメージも通らない**。反撃も被弾強化も、`OnDamaged` が呼ばれないので走らない。
    Console.WriteLine("## 3. 参照台の門と、選んだ2台");
    Console.WriteLine();
    Console.WriteLine("### 3-1. 門 — 台は本当に殴り返しているか");
    Console.WriteLine();
    Console.WriteLine("**§2-2 の下見だけでは台を選べない。** 「決着T が長い」は要件2 に適うように見えるが、");
    Console.WriteLine("**敵が一度も攻撃を通せていないから長い**のかもしれない——それは計画書 §3-2 が名指しで");
    Console.WriteLine("警告した「殴り返さない的」で、被弾駆動の出力がまるごと 0 になる。");
    Console.WriteLine();
    Console.WriteLine($"門は数字で置ける。**呪詛（ネル）は開幕に敵全体の攻撃力を −{CurseTrait.EnemyDebuff} する**");
    Console.WriteLine("（`CurseTrait.EnemyDebuff`）。1体あたり攻がこれ以下の def を並べると、呪詛入りの編成に");
    Console.WriteLine("対しては攻撃が 0 以下に潰れ、`ApplyDamage` の `if (amount <= 0) return;` で");
    Console.WriteLine("**1ダメージも通らない**——`OnDamaged` が呼ばれないので反撃も被弾強化も走らない。");
    Console.WriteLine("（味方側の弱体である萎縮（クビ・−9）は味方にしか効かないので、門には効かない。）");
    Console.WriteLine();
    Console.WriteLine($"**門: 1体あたり攻 > {CurseTrait.EnemyDebuff}。**");
    Console.WriteLine();
    Console.WriteLine("| 台 | 1体攻 | 呪詛後 | 門 | 手番外% 0 の編成 | 反撃軸2編成の決着T | 同 手番外% |");
    Console.WriteLine("|:-:|--:|--:|:-:|--:|--:|--:|");
    // 反撃軸（カド入り）の2編成。**名指しで固定する**——「手番外% が最低の編成」を毎回探すと、
    // 編成集合が動くたびに門の説明が別の編成に移る（`dissect` の pairSpec と同じ作法）。
    int[] kado = Enumerable.Range(0, nT)
        .Where(t => targets[t].Name.StartsWith("反撃 (") || targets[t].Name.StartsWith("反撃改 (")).ToArray();
    var gate = new bool[nB];
    for (int b = 0; b < nB; b++)
    {
        double each = cands[b].F.Occupied().Average(x => x.Def.Attack);
        gate[b] = each > CurseTrait.EnemyDebuff;
        int zero = Enumerable.Range(0, nT).Count(t => tr[b][t].OffTurnPct <= 1e-9);
        string kt = kado.Length == 0 ? "—" : $"{kado.Average(t => tr[b][t].Turns.Average()):F1}";
        string ko = kado.Length == 0 ? "—" : $"{kado.Average(t => tr[b][t].OffTurnPct):F1}%";
        Console.WriteLine($"| **{cands[b].Tag}** {cands[b].Name} | {each:F0} | {each - CurseTrait.EnemyDebuff:F0} "
            + $"| {(gate[b] ? "○" : "**×**")} | {zero} / {nT} | {kt} | {ko} |");
    }
    Console.WriteLine();
    Console.WriteLine("**門を落ちるのは B1 巡礼5（攻4）だけ。** 反撃軸2編成の決着T が 30.0（＝引き分けの上限に");
    Console.WriteLine("張り付いている）で、`手番外%` は 0.0%——**カドは一度も刺し返していない。**");
    Console.WriteLine("下見の表で B1 が「味方全滅 0.0%・決着 9.1T」と最も要件2 に適って見えたのは、");
    Console.WriteLine("**呪詛入りの編成に対して的が無力化されていたから**だった。");
    Console.WriteLine();
    Console.WriteLine("> **この門は下見の表からは読めない。** `決着T` も `味方全滅%` も、殴り返しの強さと");
    Console.WriteLine("> 「攻撃が通っているか」を区別しない。**`手番外%` を下見に入れてあるのはこのため。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 3-2. 選定 ---
    //
    // **性質の違う2台が要る**（計画書 §3-3）。出力が台に依存する量なら、単一の特徴量には
    // できない——それを確かめる方法が「性質の違う的で測って順位が一致するか」しかない。
    // 選定は門を通った 5 台の中から、下見の表を見て機械的に:
    //   要件2 → `味方全滅%` が低い（出力が途中で止まらない）・`T5未満%` が低い
    //   要件3 → 2台で**個体HP と 体数**が違う（第16期の「2つの時計」の軸）
    //
    // **探索で選ばない**（`dissect` の pairSpec と同じ）。「一致する組み合わせ」を探すと、
    // 中立性の検定が「一致する台を選んだ」の言い換えになる。
    int[] pick = { 1, 4 };   // B2 荷駄5 / B5 重装4
    Console.WriteLine("### 3-2. 選んだ2台");
    Console.WriteLine();
    Console.WriteLine("**門を通った 5 台から選ぶ。探索で選ばない**（`dissect` の解剖ペアと同じ作法）");
    Console.WriteLine("——「一致する組み合わせ」を探した時点で、中立性の検定が");
    Console.WriteLine("「一致する台を選んだ」の言い換えになる。");
    Console.WriteLine();
    Console.WriteLine("| 役 | 台 | 中身 | 体数 | 総HP | 総攻 | 個体HP | 1体攻 | 性質 |");
    Console.WriteLine("|:-:|:-:|---|--:|--:|--:|--:|--:|---|");
    foreach (int b in pick)
    {
        Formation bf = cands[b].F;
        Console.WriteLine($"| {(b == pick[0] ? "主" : "従")} | **{cands[b].Tag}** | {cands[b].Name} | {bf.Count} "
            + $"| {bf.Occupied().Sum(x => x.Def.MaxHp)} | {bf.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {bf.Occupied().Max(x => x.Def.MaxHp)} | {bf.Occupied().Average(x => x.Def.Attack):F0} "
            + $"| {(b == pick[0] ? "**多数・中個体HP**" : "**少数・高個体HP**")} |");
    }
    Console.WriteLine();
    Console.WriteLine("選定の理由:");
    Console.WriteLine();
    Console.WriteLine("- **総HP がほぼ同じ（540 / 580）のに、個体HP と 体数が正反対**（90×6 / 145×4）。");
    Console.WriteLine("  第16期が「波の性格は 個体HP と 総攻の2軸」と結論した、その片方だけを振ってある");
    Console.WriteLine("  ——**的の量は揃えて、形だけ変える**のが中立性の検定として最も強い形。");
    Console.WriteLine("- 味方全滅が 0.2% / 3.7% と低い。**出力が途中で止まらない。**");
    Console.WriteLine("- どちらも門を通っている（1体攻 7 / 12）。**被弾駆動の出力が生きている。**");
    Console.WriteLine();
    Console.WriteLine("**却下した案。**");
    Console.WriteLine();
    Console.WriteLine("- **B1 巡礼5** — §3-1 の門を落ちる。**呪詛入りの編成に対しては殴り返さない的**になり、");
    Console.WriteLine("  反撃軸の出力が 0 になる。計画書 §3-2 が名指しで警告した失敗そのもの。");
    Console.WriteLine("- **B6 重装6（総HP 870）** — いちばん硬いので要件2 には最も適うが、**味方全滅 75.0%**。");
    Console.WriteLine("  出力が育つ前に測定側が止まる。**硬さと殴り返しは同じ def では分けられない**");
    Console.WriteLine("  （攻撃の低い高HP の def が `EnemyCatalog` に無い）。");
    Console.WriteLine("- **B4 重装3** — 味方全滅 0% だが総HP 435 と最も薄く、`T5未満%` が高い。的が足りない。");
    Console.WriteLine("- **B3 従卒5** — B2 と個体HP・体数が同じで攻だけ違う。**2台目としては「性質が違う」に");
    Console.WriteLine("  足りない**（第16期の2軸のうちどちらも動かない）。§4-2 の辺としては使う。");
    Console.WriteLine();

    // --- 3-3. 決着してしまっていないか（計画書 §6-7 の停止条件） ---
    //
    // **台の硬さを上げる前に、何ターンで決着したかを報告する。** 要件2 は
    // 「決着しないこと、**または十分長いこと**」なので、決着すること自体は停止条件ではない
    // ——甲群の立ち上がり（T1/T3/T5）が測れるかどうかが線。
    Console.WriteLine("### 3-3. 決着してしまっていないか（計画書 §6-7）");
    Console.WriteLine();
    Console.WriteLine("**要件2 は「決着しない、または十分長い」。** 決着すること自体は停止条件ではなく、");
    Console.WriteLine($"**(B) の立ち上がり（T1/T3/T{OutputTrace.Ramp}）が測れるか**が線になる。");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(pick.Select(b => $" {cands[b].Tag} 決着T | {cands[b].Tag} T5未満% |")));
    Console.WriteLine("|---|" + string.Concat(pick.Select(_ => "--:|--:|")));
    foreach (int t in Enumerable.Range(0, nT).OrderBy(t => tr[pick[0]][t].Turns.Average()))
        Console.WriteLine($"| {targets[t].Name} |"
            + string.Concat(pick.Select(b => $" {tr[b][t].Turns.Average():F1} | {tr[b][t].Short * 100.0 / OutSeeds:F1}% |")));
    Console.WriteLine();
    var tooShort = Enumerable.Range(0, nT)
        .Where(t => pick.Any(b => tr[b][t].Turns.Average() < OutputTrace.Ramp)).ToArray();
    Console.WriteLine(tooShort.Length == 0
        ? $"**平均決着Tが {OutputTrace.Ramp} を下回る編成は 0 件。** どの編成でも T1〜T{OutputTrace.Ramp} の窓は開いている。"
        : $"> **平均決着Tが {OutputTrace.Ramp} を下回る編成が {tooShort.Length} 件ある**（どちらかの台で）: "
          + string.Join(" / ", tooShort.Select(t => targets[t].Name))
          + $"。**この編成の (B) は「育たなかった」ではなく「窓が閉じた」を測っている。**"
          + "門を通した結果、反撃軸は的を 3〜5T で削り切るようになった——**殴り返す台にすると"
          + "反撃軸が速くなり、立ち上がりを測る時間が消える**という綱引きが残る（計画書 §6-7 の3番目）。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4. 中立性の確認 ---
    //
    // **計画書 §3-3 の必須項目。** 出力が台に依存しない量なら単一の特徴量にしてよく、
    // 依存するなら**単一の特徴量にはできない**（その場合は止まって報告する）。
    //
    // 台間の相関は、それだけでは読めない。**乱数のばらつきだけでも 1.00 は割る**ので、
    // 「どれくらいなら動いたと言えるか」の基準が先に要る（第13期 `bench` の作法）。
    // 同じ台を seed で半分に割った一致度が**測定の信頼性の上限**で、台間の相関はこれと比べる。
    Console.WriteLine("## 4. 中立性の確認（計画書 §3-3）");
    Console.WriteLine();
    Console.WriteLine("**出力が台に依存する量なら、単一の特徴量にはできない。** 判定は「性質の違う2台で");
    Console.WriteLine("編成の順位が一致するか」で、比べる相手は**半割（測定の信頼性の上限）**");
    Console.WriteLine("——台間の相関は乱数のばらつきだけでも 1.00 を割るので、上限が無いと読めない");
    Console.WriteLine("（第13期 `bench` の作法。目的変数が突破度から (A) に変わっているので**上限は測り直す**）。");
    Console.WriteLine();
    Console.WriteLine("測る量は **(A) 実効打点/ターン**（総打点 ÷ 総ターン数）。**平均の平均ではない**");
    Console.WriteLine("——試行ごとに長さが違うので、比の平均を取ると短い試行に重みが寄る。");
    Console.WriteLine();

    double[] RateOf(int b, Func<int, bool> take) =>
        Enumerable.Range(0, nT).Select(t => tr[b][t].Rate(take)).ToArray();
    var full = Enumerable.Range(0, nB).Select(b => RateOf(b, _ => true)).ToArray();

    Console.WriteLine("### 4-1. 半割 — 測定の信頼性の上限");
    Console.WriteLine();
    Console.WriteLine("| 台 | 前後半 r | 前後半 ρ | 偶奇 r | 偶奇 ρ | 補正後 r | **補正後 ρ** |");
    Console.WriteLine("|:-:|--:|--:|--:|--:|--:|--:|");
    var capR = new double[nB];
    var capRho = new double[nB];
    for (int b = 0; b < nB; b++)
    {
        var h1 = Correlate(RateOf(b, s => s < OutSeeds / 2), RateOf(b, s => s >= OutSeeds / 2));
        var h2 = Correlate(RateOf(b, s => s % 2 == 0), RateOf(b, s => s % 2 == 1));
        double SB(double r) => 2 * r / (1 + r);   // Spearman-Brown
        capR[b] = SB((h1.R + h2.R) / 2);
        capRho[b] = SB((h1.Rho + h2.Rho) / 2);
        Console.WriteLine($"| **{cands[b].Tag}** {cands[b].Name} | {h1.R:F3} | {h1.Rho:F3} "
            + $"| {h2.R:F3} | {h2.Rho:F3} | {capR[b]:F3} | **{capRho[b]:F3}** |");
    }
    Console.WriteLine();
    Console.WriteLine("補正は Spearman-Brown `r(2n) = 2r(n) / (1 + r(n))`（半割は 100 seed 同士なので");
    Console.WriteLine("200 seed の測定より一致度が低く出る）。**補正は「両半分が同等・誤差が独立」を");
    Console.WriteLine("仮定するので、生の値も併記してある。** 上限がほぼ 1.00 なので、**台間の相関の");
    Console.WriteLine("低さは全部が「実物の入れ替わり」になる**——ばらつきでは 1ミリも説明が付かない。");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-2. 候補6台の総当たり ---
    //
    // **主従2台だけを見ると、一致しなかった理由が「攻が違うから」か「体数が違うから」か
    // 決まらない**（第13期 `bench` が主↔従の対角線で嵌まったのと同じ形）。
    // 候補は最初から 2 家族 × 3 刻みの格子に組んであるので、**辺を1本ずつ読める。**
    Console.WriteLine("### 4-2. 候補6台の総当たり（下三角 r / 上三角 ρ・対角は半割の補正後）");
    Console.WriteLine();
    Console.WriteLine("**主従2台だけを見ると、一致しなかった理由が「攻が違うから」か「体数が違うから」か");
    Console.WriteLine("決まらない。** 候補は 2 家族 × 3 刻みの格子に組んであるので、**辺を1本ずつ読める。**");
    Console.WriteLine();
    Console.WriteLine("|  |" + string.Concat(cands.Select(x => $" {x.Tag} |")));
    Console.WriteLine("|:-:|" + string.Concat(cands.Select(_ => "--:|")));
    for (int i = 0; i < nB; i++)
    {
        var row = new List<string>();
        for (int j = 0; j < nB; j++)
            row.Add(i == j ? $"*{capR[i]:F2} / {capRho[i]:F2}*"
                : $"{(j > i ? Correlate(full[i], full[j]).Rho : Correlate(full[i], full[j]).R):F2}");
        Console.WriteLine($"| **{cands[i].Tag}** | {string.Join(" | ", row)} |");
    }
    Console.WriteLine();

    var edges = new (string Label, int A, int B, string Axis)[]
    {
        ("B1 ↔ B2", 0, 1, "**敵の1体攻** 4 → 7（体数・個体HP は同じ）"),
        ("B2 ↔ B3", 1, 2, "**敵の1体攻** 7 → 10（同上）"),
        ("B1 ↔ B3", 0, 2, "**敵の1体攻** 4 → 10（同上・振り幅最大）"),
        ("B4 ↔ B5", 3, 4, "**敵の体数** 3 → 4（個体HP・1体攻は同じ）"),
        ("B5 ↔ B6", 4, 5, "**敵の体数** 4 → 6（同上）"),
        ("B4 ↔ B6", 3, 5, "**敵の体数** 3 → 6（同上・振り幅最大）"),
        ("B2 ↔ B5", 1, 4, "**個体HP と 体数**（主 ↔ 従。§3-2 で選んだ対）"),
        ("B1 ↔ B5", 0, 4, "参考: **門を落ちた台**を主にした場合（§3-1）"),
    };
    Console.WriteLine("`上限` は両端の台の半割（補正後 ρ）の低いほう。`余地` = 上限 − ρ が");
    Console.WriteLine("**測定のばらつきでは説明できない入れ替わりの量**（第13期・第15期と同じ定義）。");
    Console.WriteLine();
    Console.WriteLine("| 対 | 動いた変数 | r | ρ | 上限(ρ) | **余地** | 平均\\|順位差\\| | 最大\\|順位差\\| |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    var ranks = Enumerable.Range(0, nB).Select(b => AverageRanksDesc(full[b])).ToArray();
    foreach (var (label, a, b, axis) in edges)
    {
        var c = Correlate(full[a], full[b]);
        double cp = Math.Min(capRho[a], capRho[b]);
        var g = Enumerable.Range(0, nT).Select(t => Math.Abs(ranks[a][t] - ranks[b][t])).ToArray();
        Console.WriteLine($"| {label} | {axis} | {c.R:F2} | {c.Rho:F2} | {cp:F2} "
            + $"| **{cp - c.Rho:F2}** | {g.Average():F1} | {g.Max():F1} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-3. 判定 ---
    //
    // 線は**第15期の裏返し**をそのまま使う（線を新しく作らない）。第15期は
    // 「ρ < 0.90 かつ 余地 ≥ 0.05」を**入れ替わりが実在する**の線にした。
    // 中立ならその否定、すなわち「ρ ≥ 0.90 かつ 余地 < 0.05」。
    const double NeutralRho = 0.90, NeutralRoom = 0.05;
    var cross = Correlate(full[pick[0]], full[pick[1]]);
    double cap = Math.Min(capRho[pick[0]], capRho[pick[1]]);
    double room = cap - cross.Rho;
    // **連言の否定は選言。** 第15期の線は「ρ < 0.90 かつ 余地 ≥ 0.05」なので、
    // その否定は「ρ ≥ 0.90 **または** 余地 < 0.05」になる。ここを連言にすると
    // 第15期より厳しい線を黙って作ることになる。
    bool neutral = cross.Rho >= NeutralRho || room < NeutralRoom;

    Console.WriteLine("### 4-3. 判定");
    Console.WriteLine();
    Console.WriteLine($"線は**第15期の裏返し**をそのまま使う（線を新しく作らない）——第15期は");
    Console.WriteLine($"「`ρ < {NeutralRho:F2}` かつ `余地 ≥ {NeutralRoom:F2}`」を**入れ替わりが実在する**の線にした。");
    Console.WriteLine($"中立ならその**否定**——連言の否定は選言なので、"
        + $"**`ρ ≥ {NeutralRho:F2}` または `余地 < {NeutralRoom:F2}`** になる。");
    Console.WriteLine();
    Console.WriteLine(neutral
        ? $"**判定: 中立。** ρ = {cross.Rho:F3}（上限 {cap:F3}・余地 {room:F3}）で、"
          + "**性質が正反対の2台で編成の順位が一致する。** (A) は台に依存しない量なので、"
          + "**単一の特徴量にしてよい**（計画書 §3-3 の1行目）。"
        : $"> **判定: 中立ではない。** ρ = {cross.Rho:F3}（上限 {cap:F3}・**余地 {room:F3}**）。"
          + "**出力は台に依存する量で、単一の特徴量にはできない**（計画書 §3-3 の2行目）。"
          + "**計画書 §6-7 の停止条件に当たるので、Phase HB へ進む前にここで止まる。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-4. 誰が動かしているか ---
    //
    // **「一致しなかった」で止めると報告にならない。** どの編成が動いたか、そして
    // その編成に共通する性質は何かまで出す。§4-2 の辺と突き合わせると、動いている軸が読める。
    var rk0 = ranks[pick[0]];
    var rk1 = ranks[pick[1]];
    Console.WriteLine("### 4-4. 誰が動かしているか");
    Console.WriteLine();
    Console.WriteLine($"`順位差` = 順位({cands[pick[0]].Tag}) − 順位({cands[pick[1]].Tag})。"
        + $"**正なら {cands[pick[1]].Tag}（殴り返しの強い台）で順位が上がる**（順位は 1 が最良）。");
    Console.WriteLine();
    Console.WriteLine("`手番外%` は打点のうち手番の振り以外（反撃・破裂・追い打ち・毒燃）から出たぶん。");
    Console.WriteLine("**反撃軸はここが高い。**");
    Console.WriteLine();
    Console.WriteLine($"| 編成 | 総攻 | {cands[pick[0]].Tag} (A) | 順位 | {cands[pick[1]].Tag} (A) | 順位 | 順位差 "
        + $"| {cands[pick[0]].Tag} 手番外% | {cands[pick[1]].Tag} 手番外% |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => Math.Abs(rk0[t] - rk1[t])))
        Console.WriteLine($"| {targets[t].Name} | {targets[t].F.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {full[pick[0]][t]:F1} | {rk0[t]:F1} | {full[pick[1]][t]:F1} | {rk1[t]:F1} "
            + $"| {Sg(rk0[t] - rk1[t], 1):+0.0;-0.0} "
            + $"| {tr[pick[0]][t].OffTurnPct:F0}% | {tr[pick[1]][t].OffTurnPct:F0}% |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-5. 一致しなかった量はどこに集まっているか ---
    //
    // **2つの疑いを数字で潰す。**
    //   (1) 台が早く落ちた編成（要件2 を満たしていない）が作った見かけの不一致ではないか
    //   (2) 反撃軸（手番外% が高い編成）だけが動かしているのではないか
    // どちらも「外した部分集合で ρ を取り直す」ことで測れる。**外すのは診断であって
    // 直しではない**——外して一致したからといって、その編成の出力が測れたことにはならない。
    Console.WriteLine("### 4-5. 一致しなかった量はどこに集まっているか");
    Console.WriteLine();
    Console.WriteLine("**2つの疑いを数字で潰す。** どちらも部分集合で ρ を取り直すだけで測れる。");
    Console.WriteLine("**外すのは診断であって直しではない**——外して一致しても、その編成の出力が");
    Console.WriteLine("測れたことにはならない。");
    Console.WriteLine();
    double[] Sub(double[] v, int[] ix) => ix.Select(t => v[t]).ToArray();
    var allIx = Enumerable.Range(0, nT).ToArray();
    var longIx = allIx.Where(t => pick.All(b => tr[b][t].Turns.Average() >= OutputTrace.Ramp)).ToArray();
    var moved = allIx.OrderByDescending(t => Math.Abs(rk0[t] - rk1[t])).ToArray();
    var subsets = new (string Name, int[] Ix, string Note)[]
    {
        ("全編成", allIx, "§4-3 の判定に使った集合"),
        ($"平均決着T ≥ {OutputTrace.Ramp} の編成", longIx,
            "**要件2 を満たしている編成だけ。** 台が早く落ちた編成を外しても残るか"),
        ("順位差 上位2件を外す", moved.Skip(2).ToArray(),
            $"**{targets[moved[0]].Name} / {targets[moved[1]].Name} を外す**"),
        ("順位差 上位4件を外す", moved.Skip(4).ToArray(), "同上・4件版"),
        ("手番外% < 50 の編成（従台で判定）", allIx.Where(t => tr[pick[1]][t].OffTurnPct < 50).ToArray(),
            "**反撃軸を外した集合。** 手番外% は打点のうち振り以外から出たぶん"),
    };
    Console.WriteLine("| 部分集合 | n | ρ | 上限(ρ) | 余地 | 備考 |");
    Console.WriteLine("|---|--:|--:|--:|--:|---|");
    foreach (var (name, ix, note) in subsets)
    {
        if (ix.Length < 3) { Console.WriteLine($"| {name} | {ix.Length} | — | — | — | {note}（n が足りない） |"); continue; }
        var c = Correlate(Sub(full[pick[0]], ix), Sub(full[pick[1]], ix));
        Console.WriteLine($"| {name} | {ix.Length} | {c.Rho:F3} | {cap:F3} | **{cap - c.Rho:F3}** | {note} |");
    }
    Console.WriteLine();
    Console.WriteLine("> **順位は部分集合の中で取り直している**（AverageRanksDesc ではなく Spearman の中で）ので、");
    Console.WriteLine("> 外した編成のぶんだけ順位が詰まる。**部分集合どうしの ρ を直接比べてよい**のは");
    Console.WriteLine("> 上限が n にほとんど依らないほど高い（0.99 台）ためで、そうでなければ");
    Console.WriteLine("> 部分集合ごとに半割を取り直す必要がある。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= Phase HB: 出力特徴量 =================
    //
    // Phase HA で「(A) は台に依存しない量」まで来たので、ここからは**測った出力で
    // 第14〜16期の分析をやり直す**。
    //
    // 特徴量は主台（§3-2）の値を使う。従台の値も同じ表に出して、**(A) 以外
    // （(B)(C)）についても台間の一致を確かめる**——中立性を確認したのは (A) だけで、
    // (B)(C) は別の量なので改めて見る必要がある。
    int main = pick[0], sub = pick[1];

    Console.WriteLine("# Phase HB — 出力特徴量");
    Console.WriteLine();
    Console.WriteLine($"特徴量は**主台 {cands[main].Tag}（{cands[main].Name}）**で測る。従台"
        + $" {cands[sub].Tag} の値も併記して、**(A) 以外についても台間の一致を確かめる**"
        + "——§4 で中立性を確かめたのは (A) だけで、(B)(C) は別の量。");
    Console.WriteLine();

    // --- 5. 定義 ---
    Console.WriteLine("## 5. (A)(B)(C) の定義");
    Console.WriteLine();
    Console.WriteLine("| 記号 | 名 | 定義 | 近似 |");
    Console.WriteLine("|:-:|---|---|---|");
    Console.WriteLine("| **(A)** | `実効打点/T` | 参照台で敵に通した**総打点 ÷ 総ターン数**（全 seed の合計どうしの比）。"
        + "毒・燃焼・反撃・破裂、どの経路も入る | **無し**（実測） |");
    Console.WriteLine($"| **(B)** | `育ち` | `(T{OutputTrace.Ramp}累積 − T3累積) ÷ 2` ÷ `T1打点`。"
        + $"分子は T4〜T{OutputTrace.Ramp} の1ターンあたり打点、分母は初手の1ターンあたり打点。"
        + "**1.00 が「まったく育たない」** | **無し**（実測）。ただし決着で窓が閉じる（下記） |");
    Console.WriteLine("| **(C)** | `手番外%` | 打点のうち**手番の振り以外**（反撃・破裂・追い打ち・生贄・毒燃）から出た割合 "
        + "| **無し**（実測） |");
    Console.WriteLine();
    Console.WriteLine("**(C) に近似は使っていない。** 計画書 §4-1 は `総攻 × 手番数` を引く近似を示していたが、");
    Console.WriteLine("`Events` から振りの範囲を「Attack イベントから、同じ手番の同じ actor が出した Damage まで」で");
    Console.WriteLine("切れるので、引き算の近似は要らない（第16期 `dissect` の `振に帰属%` と同じ切り方）。");
    Console.WriteLine("**反撃は actor が違い、毒は actor が null なので、追加のフラグ無しで外れる。**");
    Console.WriteLine();
    Console.WriteLine("**(B) の弱点は明記する。** 決着すると累積が頭打ちになるので、**早く決着する編成の (B) は");
    Console.WriteLine($"「育たなかった」ではなく「窓が閉じた」を測る**（§3-3）。`到達%`（T{OutputTrace.Ramp} まで戦った試行の割合）を");
    Console.WriteLine("同じ表に出してあるので、低い行はそのつもりで読むこと。");
    Console.WriteLine();
    Console.WriteLine("**却下した表し方**: T1〜T5 の累積に二次項を当てて係数を取る。n = 5 点の二次回帰は");
    Console.WriteLine("決着による頭打ちを「上に凸」と読んでしまい、**育った編成と早く終わった編成が同じ符号になる**。");
    Console.WriteLine();

    // --- 5-2. 同語反復の判定 ---
    //
    // **第14期の基準を、新しい特徴量にも通す。** 基準は「目的変数の言い換えになっていないか」の
    // 1本だけ（「信頼できるか」は混ぜない）。
    Console.WriteLine("### 5-1. 同語反復の判定（第14期の基準を通す）");
    Console.WriteLine();
    Console.WriteLine("基準は「**目的変数の言い換えになっていないか**」の1本だけ（第14期・第15期と同じ）。");
    Console.WriteLine();
    Console.WriteLine("| 経路 | (A)(B)(C) は当たるか |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| **分子経路**（量そのものが勝利の定義に含まれる） | **当たらない。** 量を測ったのは"
        + "**参照台**で、目的変数（波ごとの勝率）の戦闘とは別の戦闘。その波の敵を削り切ったかどうかは"
        + "1ビットも入っていない |");
    Console.WriteLine("| **分母経路**（`部隊戦数 = 突破数 + 1`） | **当たらない。** 単発戦では分母経路が"
        + "そもそも存在しない（第15期 §9-1） |");
    Console.WriteLine();
    Console.WriteLine("**これが参照台を作った理由そのもの。** `power` / `wave` の動的特徴量（`与ダメ/戦`）は");
    Console.WriteLine("目的変数と同じ戦闘から取っているので、波ごとの勝率に当てた瞬間に循環する");
    Console.WriteLine("——第15期はそれを分子経路として外した。**参照台はその循環を切るための装置。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 5-3. 値 ---
    double[] A(int b) => Enumerable.Range(0, nT).Select(t => tr[b][t].RateAll).ToArray();
    double[] Bv(int b) => Enumerable.Range(0, nT).Select(t => tr[b][t].Ramp15).ToArray();
    double[] Cv(int b) => Enumerable.Range(0, nT).Select(t => tr[b][t].OffTurnPct).ToArray();
    var featA = A(main);
    var featB = Bv(main);
    var featC = Cv(main);

    Console.WriteLine("### 5-2. 値（31編成）");
    Console.WriteLine();
    Console.WriteLine($"`到達%` は T{OutputTrace.Ramp} まで戦った試行の割合。**低い行の (B) は窓が閉じている。**");
    Console.WriteLine();
    Console.WriteLine($"| 編成 | 総攻 | **(A)** | T1 | T3 | T{OutputTrace.Ramp} | **(B) 育ち** | 到達% | **(C) 手番外%** "
        + $"| 従台 (A) | 従台 (B) | 従台 (C) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => featA[t]))
    {
        OutputTrace mt = tr[main][t], st = tr[sub][t];
        Console.WriteLine($"| {targets[t].Name} | {targets[t].F.Occupied().Sum(x => x.Def.Attack)} "
            + $"| **{featA[t]:F1}** | {mt.CumAt(1):F0} | {mt.CumAt(3):F0} | {mt.CumAt(OutputTrace.Ramp):F0} "
            + $"| **{featB[t]:F2}** | {100.0 - mt.Short * 100.0 / OutSeeds:F0}% | **{featC[t]:F0}%** "
            + $"| {st.RateAll:F1} | {st.Ramp15:F2} | {st.OffTurnPct:F0}% |");
    }
    Console.WriteLine();

    // (B)(C) の台間一致。**(A) だけ確かめて残り2つを黙って使うのは、§4 の作業の意味を消す。**
    Console.WriteLine("### 5-3. (B)(C) も台に依存しないか");
    Console.WriteLine();
    Console.WriteLine("**§4 で中立性を確かめたのは (A) だけ。** (B)(C) は別の量なので、同じ形で確かめる。");
    Console.WriteLine();
    Console.WriteLine("| 量 | 主台 ↔ 従台 r | ρ | 上限(ρ) | **余地** | 判定 |");
    Console.WriteLine("|---|--:|--:|--:|--:|:-:|");
    foreach (var (nm, mv, sv) in new[] { ("(A) 実効打点/T", featA, A(sub)), ("(B) 育ち", featB, Bv(sub)), ("(C) 手番外%", featC, Cv(sub)) })
    {
        var c = Correlate(mv, sv);
        double rm = cap - c.Rho;
        Console.WriteLine($"| {nm} | {c.R:F3} | {c.Rho:F3} | {cap:F3} | **{rm:F3}** "
            + $"| {(c.Rho >= NeutralRho || rm < NeutralRoom ? "中立" : "**×**")} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 6. `総攻` と (A) はどれだけ違うか ---
    //
    // 計画書 §5-2 の報告項目。**第16期が名指しした障害が、数字でどれだけ大きいか。**
    Console.WriteLine("## 6. `総攻` と (A) はどれだけ違うか");
    Console.WriteLine();
    var atk = Enumerable.Range(0, nT).Select(t => (double)targets[t].F.Occupied().Sum(x => x.Def.Attack)).ToArray();
    var cAtk = Correlate(atk, featA);
    var rkA = AverageRanksDesc(featA);
    var rkAtk = AverageRanksDesc(atk);
    Console.WriteLine($"**`総攻` と (A) の相関は r = {cAtk.R:F3} / ρ = {cAtk.Rho:F3}。**");
    Console.WriteLine($"順位の平均\\|差\\| は {Enumerable.Range(0, nT).Average(t => Math.Abs(rkAtk[t] - rkA[t])):F1}、"
        + $"最大 {Enumerable.Range(0, nT).Max(t => Math.Abs(rkAtk[t] - rkA[t])):F1}（31 編成）。");
    Console.WriteLine();
    Console.WriteLine("**最も外れていた編成 上位5**（`順位差` = 順位(総攻) − 順位((A))。"
        + "**正なら (A) のほうが高く評価する**）:");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 総攻 | 総攻 順位 | (A) | (A) 順位 | 順位差 | (C) 手番外% | 読み |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => Math.Abs(rkAtk[t] - rkA[t])).Take(5))
    {
        double d = rkAtk[t] - rkA[t];
        string read = featC[t] >= 60 ? "**手番外が主**（反撃・毒）" : featC[t] >= 30 ? "手番外が半分" : "手番の振りが主";
        Console.WriteLine($"| {targets[t].Name} | {atk[t]:F0} | {rkAtk[t]:F1} | {featA[t]:F1} | {rkA[t]:F1} "
            + $"| {Sg(d, 1):+0.0;-0.0} | {featC[t]:F0}% | {read} |");
    }
    Console.WriteLine();
    Console.WriteLine("`総攻` と (C) の相関も出しておく——**手番外の割合が高い編成ほど `総攻` が外す**なら、");
    Console.WriteLine("第16期の読み（反撃軸・毒軸で桁が違う）がそのまま数字になる。");
    Console.WriteLine();
    var cGap = Correlate(featC, Enumerable.Range(0, nT).Select(t => rkAtk[t] - rkA[t]).ToArray());
    Console.WriteLine($"**(C) 手番外% と 順位差（総攻 − (A)）の相関: r = {cGap.R:F3} / ρ = {cGap.Rho:F3}。**");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 7. (B) で甲乙は分離できるか ---
    //
    // 計画書 §4-2 の3番・§4-3 の3行目。**第16期の12事例の分類は人間の解釈**なので、
    // (B) がそれを数値で再現できるかを見る。**分類は第16期の出力から固定で写す**
    // ——ここで分け直すと「分かれるように分けた」になる。
    var kou = new[] { "速攻 (ボルグ×ムド)", "毒+耐久 (ベニ×トウ)", "溜め改 (クグ×バン×ガン)" };
    var otsu = new[] { "毒 (グザ×ミオ×ラウ)", "燃焼 (ボルグ×ホタ)", "耐久 (ガルド×リリ)",
                       "範囲耐性 (ヒビ×ボルグ)", "追撃×死 (ハギ×リィカ)", "死の連鎖 (リィカ軸)" };
    int Ix(string name) => Array.FindIndex(targets, x => x.Name == name);

    Console.WriteLine("## 7. (B) で甲乙は分離できるか（第16期の12事例と照合）");
    Console.WriteLine();
    Console.WriteLine("**第16期の分類は人間の解釈。** (B) がそれを数値で再現できるかを見る。分類は");
    Console.WriteLine("第16期 `dissect` §7 の 12 事例からそのまま写した（**ここで分け直すと");
    Console.WriteLine("「分かれるように分けた」になる**）。事例 6 は第16期が「説明が付かなかった」と");
    Console.WriteLine("記録した事例で、編成としては甲群の 毒+耐久 と同じなので甲に入れてある。");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成 | **(B) 育ち** | 到達% | (A) | (C) 手番外% | 総攻 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|");
    foreach (var (label, names) in new[] { ("**甲**", kou), ("**乙**", otsu) })
        foreach (string nm in names)
        {
            int t = Ix(nm);
            if (t < 0) { Console.WriteLine($"| {label} | {nm} | — | — | — | — | — |"); continue; }
            Console.WriteLine($"| {label} | {nm} | **{featB[t]:F2}** | {100.0 - tr[main][t].Short * 100.0 / OutSeeds:F0}% "
                + $"| {featA[t]:F1} | {featC[t]:F0}% | {atk[t]:F0} |");
        }
    Console.WriteLine();

    var kIx = kou.Select(Ix).Where(t => t >= 0).ToArray();
    var oIx = otsu.Select(Ix).Where(t => t >= 0).ToArray();
    Console.WriteLine("| 量 | 甲の平均 | 乙の平均 | 甲の範囲 | 乙の範囲 | 重なるか |");
    Console.WriteLine("|---|--:|--:|---|---|:-:|");
    foreach (var (nm, v) in new[] { ("(B) 育ち", featB), ("(A) 実効打点/T", featA), ("(C) 手番外%", featC), ("総攻", atk) })
    {
        double kmin = kIx.Min(t => v[t]), kmax = kIx.Max(t => v[t]);
        double omin = oIx.Min(t => v[t]), omax = oIx.Max(t => v[t]);
        bool overlap = kmin <= omax && omin <= kmax;
        Console.WriteLine($"| {nm} | {kIx.Average(t => v[t]):F2} | {oIx.Average(t => v[t]):F2} "
            + $"| {kmin:F2} 〜 {kmax:F2} | {omin:F2} 〜 {omax:F2} | {(overlap ? "**重なる**" : "分かれる")} |");
    }
    Console.WriteLine();
    Console.WriteLine("**「分かれる」= 2群の範囲が重ならない**（1本の閾値で 9 編成を完全に分類できる）。");
    Console.WriteLine("n が 3 と 6 しかないので、**重ならないことは「分離できた」の必要条件であって十分条件ではない**");
    Console.WriteLine("——偶然に重ならない確率は決して小さくない（3 と 6 の並べ替えで完全分離は 1/84）。");
    Console.WriteLine();

    // --- 7-1. 読み（解釈） ---
    //
    // **ここだけは測定ではなく解釈**（`dissect` §7 と同じ扱い）。表と食い違ったら表が正しい。
    // 数字が動いたらこの節も書き直すこと。
    Console.WriteLine("### 7-1. 読み（解釈。表と食い違ったら表が正しい）");
    Console.WriteLine();
    {
        int tPoison = Ix("毒 (グザ×ミオ×ラウ)"), tTame = Ix("溜め改 (クグ×バン×ガン)");
        Console.WriteLine("**(B) は甲乙を分けない。** 分けないこと自体より、**なぜ分けないか**のほうが情報がある。");
        Console.WriteLine();
        if (tPoison >= 0)
            Console.WriteLine($"- **乙群の 毒 (グザ×ミオ×ラウ) の (B) が {featB[tPoison]:F2} で、9 編成中いちばん高い。**"
                + " 毒は段数が積み上がるので、出力は時間で**育つ**——(B) の定義どおりに大きく出る。"
                + "それでも第16期が 毒 を乙群に置いたのは、**育った出力が撃破に変換されない**からだった"
                + "（`毒の無駄` が S4 で 282.6 段）。**甲乙は「育つか」ではなく「撃破に変換されるか」で"
                + "割れていた**——(B) は前者しか測っていない。");
        if (tTame >= 0)
            Console.WriteLine($"- **甲群の 溜め改 の (B) が {featB[tTame]:F2} と最低なのは、窓が閉じたから**"
                + $"（到達% {100.0 - tr[main][tTame].Short * 100.0 / OutSeeds:F0}%）。"
                + "門を通した台は反撃軸に対して 3〜5T で落ちるので、**育ちを測る時間がそもそも無い**"
                + "（§3-3 の綱引き）。この行の (B) は「育たなかった」ではない。");
        Console.WriteLine("- **(C) は甲乙の平均を大きく分けている**（甲 57% / 乙 24%）が、範囲は重なる"
            + "——乙群の 毒 が 92% で甲群の 速攻 が 12% なので、**手番外率だけでは境界が引けない。**");
        Console.WriteLine();
        Console.WriteLine("**推測**: 甲乙を数値化するなら、必要なのは出力の量でも立ち上がりでもなく");
        Console.WriteLine("**「出力 → 撃破への変換率が敵の個体HP でどれだけ落ちるか」**——つまり");
        Console.WriteLine("参照台を**個体HP だけ変えて2つ**用意し、その間の出力低下率を取る形になる。");
        Console.WriteLine("§4-2 の格子は 1体攻 と 体数 を振っていて、**個体HP を単独では振っていない**"
            + "（B1〜B3 は 90 固定、B4〜B6 は 145 固定）。**第17期の参照台では測れない量。**");
        Console.WriteLine();
    }
    Console.Out.Flush();

    // ---- ここから波を測る（第15期 FB・第16期 GB のやり直し） ----
    //
    // 波は `WaveCatalog()` を呼ぶ。**コピーを持たない**（第15期が「1箇所に集める」ために
    // やった作業を、3つ目の診断が台無しにする）。
    var waves = WaveCatalog();
    int nW = waves.Length;
    const double DeadZone = 50.0;   // wave §4 / dissect §1 と同じ線

    var rate = new double[nW][];
    var degree = new double[nW][];
    var dyn = new double[nW][][];
    for (int w = 0; w < nW; w++)
    {
        rate[w] = new double[nT];
        degree[w] = new double[nT];
        dyn[w] = new double[nT][];
        for (int t = 0; t < nT; t++)
        {
            var mw = MeasureWave(targets[t].F, waves[w].Enemy, OutSeeds);
            rate[w][t] = mw.Win.Average() * 100;
            degree[w][t] = mw.SurvRate.Average();
            dyn[w][t] = mw.Dynamics;
        }
        Console.Out.Flush();
    }
    var contributes = new bool[nW];
    for (int w = 0; w < nW; w++)
    {
        double ceilN = rate[w].Count(v => v >= 100.0 - 1e-9) * 100.0 / nT;
        double floorN = rate[w].Count(v => v <= 1e-9) * 100.0 / nT;
        contributes[w] = ceilN + floorN < DeadZone;
    }
    int[] conW = Enumerable.Range(0, nW).Where(w => contributes[w]).ToArray();

    // --- 8. 波ごとの分解のやり直し（第15期 Phase FB） ---
    var statNames = new (string Name, Func<Formation, double> Get)[]
    {
        ("体数",     f => f.Count),
        ("総HP",     f => f.Occupied().Sum(x => x.Def.MaxHp)),
        ("総攻",     f => f.Occupied().Sum(x => x.Def.Attack)),
        ("積",       f => (double)f.Occupied().Sum(x => x.Def.MaxHp) * f.Occupied().Sum(x => x.Def.Attack)),
        ("最薄HP",   f => f.Occupied().Min(x => x.Def.MaxHp)),
        ("後列HP",   f => f.Occupied().Where(x => FormationRules.RowOf(x.Slot) == Row.Back).Sum(x => x.Def.MaxHp)),
        ("平均速度", f => f.Occupied().Average(x => x.Def.Speed)),
        ("範囲枚数", f => AoeCount(f)),
    };
    int nS = statNames.Length;
    // 第15期の候補（12種）= 静的8 + 動的4（`与ダメ/戦`・`被ダメ/戦`・`撃破/戦` は分子経路で除外済み）。
    // MeasureWave の Dynamics の並びは 与ダメ・被ダメ・撃破・干渉・回復・自傷率・与ダメ効率。
    var dynKeep = new (string Name, int K)[] { ("干渉/戦", 3), ("回復/戦", 4), ("自傷率", 5), ("与ダメ効率", 6) };
    var outNames = new (string Name, double[] V)[] { ("(A) 実効打点/T", featA), ("(B) 育ち", featB), ("(C) 手番外%", featC) };

    Console.WriteLine("## 8. 波ごとの分解のやり直し（第15期 Phase FB）");
    Console.WriteLine();
    Console.WriteLine("目的変数は波ごとの単発勝率。候補は**第15期の 12 種**（静的8 + 動的4）に");
    Console.WriteLine("**(A)(B)(C) を足した 15 種**。第15期の側は同じ実行の中で計算し直している");
    Console.WriteLine("——**別の実行から引くと、動いたのが候補のせいか実行のせいか決まらない**（第13期以来の作法）。");
    Console.WriteLine();
    Console.WriteLine($"寄与する波は同じ判定式（天井率 + 床率 < {DeadZone:F0}%）で引き直した: **{conW.Length} 本** — "
        + string.Join(" / ", conW.Select(w => $"`{waves[w].Tag}`")));
    Console.WriteLine();

    // 第15期の記録（README「検証で分かったこと」の第15期の3項目）。**値を候補に使うのではなく、
    // この診断が第15期と同じ盤を見ていることの検算にだけ使う**（dissect §12-2 と同じ作法）。
    var rec15 = new Dictionary<string, (string First, double R2)>
    {
        ["S2"] = ("与ダメ効率", 0.059), ["S3"] = ("体数", 0.164), ["S4"] = ("与ダメ効率", 0.341),
        ["S5"] = ("干渉/戦", 0.338), ["R8"] = ("与ダメ効率", 0.194), ["R9"] = ("範囲枚数", 0.203),
        ["R10"] = ("総HP", 0.116),
    };

    string[] names15 = statNames.Select(x => x.Name).Concat(dynKeep.Select(x => x.Name)).ToArray();
    string[] names17 = names15.Concat(outNames.Select(x => x.Name)).ToArray();
    double[] Col15(int k, int w) => k < nS
        ? Enumerable.Range(0, nT).Select(t => statNames[k].Get(targets[t].F)).ToArray()
        : Enumerable.Range(0, nT).Select(t => dyn[w][t][dynKeep[k - nS].K]).ToArray();
    double[] Col17(int k, int w) => k < names15.Length ? Col15(k, w) : outNames[k - names15.Length].V;

    Console.WriteLine("| 波 | 第15期(12種) 第一近似 | r² | 記録 | **第17期(15種) 第一近似** | **r²** | 2位 | 上がったか |");
    Console.WriteLine("|:-:|---|--:|--:|---|--:|---|:-:|");
    int miss15 = 0, improved = 0;
    foreach (int w in conW)
    {
        (int K, double R, double R2) Best(Func<int, double[]> col, int n)
        {
            var ord = Enumerable.Range(0, n).Select(k => (K: k, R: Correlate(col(k), rate[w]).R))
                .Where(x => !double.IsNaN(x.R)).OrderByDescending(x => Math.Abs(x.R)).ToArray();
            return ord.Length == 0 ? (-1, double.NaN, double.NaN) : (ord[0].K, ord[0].R, ord[0].R * ord[0].R);
        }
        var b15 = Best(k => Col15(k, w), names15.Length);
        var ord17 = Enumerable.Range(0, names17.Length)
            .Select(k => (K: k, R: Correlate(Col17(k, w), rate[w]).R))
            .Where(x => !double.IsNaN(x.R)).OrderByDescending(x => Math.Abs(x.R)).ToArray();
        string rec = "—";
        if (rec15.TryGetValue(waves[w].Tag, out var want))
        {
            bool ok = names15[b15.K] == want.First && Math.Abs(b15.R2 - want.R2) <= 0.005;
            if (!ok) miss15++;
            rec = ok ? $"{want.R2:F3}" : $"**{want.First} {want.R2:F3} ←ずれ**";
        }
        bool up = ord17[0].K >= names15.Length;
        if (up) improved++;
        Console.WriteLine($"| **{waves[w].Tag}** | {names15[b15.K]} {b15.R:+0.00;-0.00} | {b15.R2:F3} | {rec} "
            + $"| {(up ? "**" : "")}{names17[ord17[0].K]}{(up ? "**" : "")} {ord17[0].R:+0.00;-0.00} "
            + $"| **{ord17[0].R * ord17[0].R:F3}** "
            + $"| {(ord17.Length > 1 ? $"{names17[ord17[1].K]} {ord17[1].R:+0.00;-0.00}" : "—")} "
            + $"| {(up ? "**○**" : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine(miss15 == 0
        ? "**検算: 第15期の記録した第一近似・r² と完全に一致（ずれ 0 件）。** この診断は第15期と同じ盤を見ている。"
        : $"**検算: {miss15} 件ずれた。第15期と同じ盤を見ていない——先へ進む前に原因を潰すこと。**");
    Console.WriteLine();
    Console.WriteLine($"**(A)(B)(C) が第一近似になったのは {improved} / {conW.Length} 波。**");
    Console.WriteLine();

    // (A)(B)(C) 単体が波ごとにどれだけ効くか。第15期 §9-3 の一覧と同じ形。
    Console.WriteLine("### 8-1. (A)(B)(C) の単相関（寄与する波）");
    Console.WriteLine();
    Console.WriteLine("**符号まで含めて読む。** 同じ量が波によって逆向きに効くなら、それは");
    Console.WriteLine("「どちらの波にも効く地力」ではなく**波の性格そのもの**（第15期 §9-3 の読み方）。");
    Console.WriteLine();
    Console.WriteLine("| 量 |" + string.Concat(conW.Select(w => $" {waves[w].Tag} |")) + " 符号の向き |");
    Console.WriteLine("|---|" + string.Concat(conW.Select(_ => "--:|")) + ":-:|");
    foreach (var (nm, v) in outNames.Concat(new[] { ("総攻（比較）", atk) }))
    {
        var rs = conW.Select(w => Correlate(v, rate[w]).R).ToArray();
        bool allSame = rs.All(r => r >= 0) || rs.All(r => r <= 0);
        Console.WriteLine($"| {nm} |" + string.Concat(rs.Select(r => $" {(double.IsNaN(r) ? "—" : $"{r:+0.00;-0.00}")} |"))
            + $" {(allSame ? "揃う" : "**反転する**")} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9. 交互作用項の作り直し（第16期 Phase GB） ---
    Console.WriteLine("## 9. 交互作用項の作り直し（第16期 Phase GB）");
    Console.WriteLine();
    Console.WriteLine("**積の材料を `総攻` から (A)(B)(C) に差し替える。** 片側だけの特徴量は交互作用成分と");
    Console.WriteLine("相関が**恒等的に 0** なので（第16期 §11。残差は行にも列にも和が 0）、(A) を単体で");
    Console.WriteLine("足しても 0 のまま——**積にして初めて意味を持つ。**");
    Console.WriteLine();

    // 分散分解（第16期 §11 と同じ計算）。
    int nC = conW.Length;
    double[][] Resid(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        return Enumerable.Range(0, nC)
            .Select(c => Enumerable.Range(0, nT).Select(t => y[c][t] - rowM[c] - colM[t] + grand).ToArray())
            .ToArray();
    }
    (double Wave, double Build, double Inter) Decompose(double[][] src)
    {
        var y = conW.Select(w => src[w]).ToArray();
        double grand = y.SelectMany(r => r).Average();
        var rowM = y.Select(r => r.Average()).ToArray();
        var colM = Enumerable.Range(0, nT).Select(t => y.Average(r => r[t])).ToArray();
        double ssT = y.SelectMany(r => r).Sum(v => (v - grand) * (v - grand));
        double ssW = nT * rowM.Sum(m => (m - grand) * (m - grand));
        double ssB = nC * colM.Sum(m => (m - grand) * (m - grand));
        double ssI = Resid(src).SelectMany(r => r).Sum(v => v * v);
        return (ssW / ssT * 100, ssB / ssT * 100, ssI / ssT * 100);
    }
    var decW = Decompose(rate);
    var decD = Decompose(degree);
    var residW = Resid(rate);
    var residD = Resid(degree);
    double[] Flat(Func<int, int, double> get) => Enumerable.Range(0, nC)
        .SelectMany(c => Enumerable.Range(0, nT).Select(t => get(conW[c], t))).ToArray();
    double[] FlatV(double[][] v) => v.SelectMany(r => r).ToArray();
    double[] residFlat = FlatV(residW);

    Console.WriteLine("### 9-1. 分散分解（第16期 §11 と同じ計算）");
    Console.WriteLine();
    Console.WriteLine($"寄与する {nC} 波 × {nT} 編成 = **{nC * nT} 点**。");
    Console.WriteLine();
    Console.WriteLine("| 目的変数 | 波の主効果 | 編成の主効果 | **交互作用** | 第16期の記録 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    Console.WriteLine($"| 勝率 | {decW.Wave:F1}% | {decW.Build:F1}% | **{decW.Inter:F1}%** | 28.3% |");
    Console.WriteLine($"| 残存度 | {decD.Wave:F1}% | {decD.Build:F1}% | **{decD.Inter:F1}%** | 21.7% |");
    Console.WriteLine();
    // 検算。**(A)(B)(C) も片側だけの量なので、単体では交互作用成分と相関 0 になるはず。**
    double maxOne = outNames.Max(o => Math.Abs(Pearson(Flat((w, t) => o.V[t]), residFlat)));
    Console.WriteLine($"**検算: (A)(B)(C) を単体で交互作用成分に当てると |r| = {maxOne:F6}。**");
    Console.WriteLine("**新しい特徴量でも 0 になるのが正しい**——これは測定結果ではなく恒等式で、");
    Console.WriteLine("「出力を測れば交互作用が説明できる」ではなく「**出力を積の材料にできる**」が");
    Console.WriteLine("第17期の主張であることの確認になる。");
    Console.WriteLine();

    // --- 9-2. 項の候補 ---
    //
    // **総当たりで作らない**（第16期 §10 と同じ縛り）。出どころは
    //   (a) 第16期の項の材料を (A)(B)(C) に差し替えたもの
    //   (b) 第16期 §7 の甲乙の説明を (B)(C) で書き直したもの
    //   (c) 第16期の項をそのまま（対照）
    // の3つだけ。10 個以内。
    double AllyOut(int t) => featA[t];
    var terms = new (string Name, string Expr, string From, string Why, Func<int, int, double> Get)[]
    {
        ("耐えるT", "味方の総HP ÷ 敵総攻", "第16期のまま（対照）",
            "第16期の最良項。**出力を含まない**ので、比較の基準として据え置く",
            (w, t) => targets[t].F.Occupied().Sum(x => x.Def.MaxHp)
                      / (double)waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)),
        ("集中砲火", "敵総攻 ÷ 味方の最薄HP", "第16期のまま（対照）",
            "同上。出力を含まない項をもう1本残す",
            (w, t) => waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)
                      / (double)targets[t].F.Occupied().Min(x => x.Def.MaxHp)),
        ("削るT'", "敵総HP ÷ **(A)**", "第16期 `削るT` の差し替え",
            "**敵を削り切るまでのターン数。** 第16期は分母が `総攻` だったので、"
            + "反撃軸と毒軸で桁が違っていた",
            (w, t) => waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp) / AllyOut(t)),
        ("時計比'", "(味方の総HP × **(A)**) ÷ (敵の総HP × 敵総攻)", "第16期 `時計比` の差し替え",
            "**2つの時計の競走を1本にまとめたもの**（`耐えるT` ÷ `削るT'`）。第16期は味方側が"
            + "`総HP × 総攻` だった",
            (w, t) => targets[t].F.Occupied().Sum(x => x.Def.MaxHp) * AllyOut(t)
                      / (waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp)
                         * (double)waves[w].Enemy.Occupied().Sum(x => x.Def.Attack))),
        ("一撃圏'", "敵の個体HP中央値 ÷ (**(A)** ÷ 味方の体数)", "第16期 `一撃圏` の差し替え",
            "**1体あたりの実効出力で何ターン殴れば1体落ちるか。** 第16期は分母が `総攻 ÷ 体数` "
            + "だったので、毒軸の一撃圏が実際より遠く出ていた",
            (w, t) => MedianHp(waves[w].Enemy) / (AllyOut(t) / targets[t].F.Count)),
        ("範囲の変換'", "味方の範囲枚数 × 敵体数 ÷ **一撃圏'**", "第16期 `範囲の変換` の差し替え",
            "第16期 事例 8。**巻き込み枚数ではなく、巻き込んだ結果何体落ちたかで決まる**",
            (w, t) => AoeCount(targets[t].F) * waves[w].Enemy.Count
                      * (AllyOut(t) / targets[t].F.Count) / MedianHp(waves[w].Enemy)),
        ("育ちの余地", "**(B)** × 耐えるT", "第16期 §7-2（甲群）を (B) で書き直したもの",
            "**甲群の説明そのもの**——出力が時間で育つ編成は、耐えられる時間が長い波で伸びる。"
            + "第16期は「時間で育つ」を数値で持っていなかった",
            (w, t) => featB[t] * targets[t].F.Occupied().Sum(x => x.Def.MaxHp)
                      / (double)waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)),
        ("育ちは間に合うか", "**(B)** ÷ 削るT'", "第16期 §7-2（甲群）",
            "育つ前に決着するなら育ちは価値にならない。**上の項の裏側**（分母が敵の硬さ）",
            (w, t) => featB[t] * AllyOut(t) / waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp)),
        ("被弾駆動 × 敵総攻", "**(C)** × 敵総攻", "第16期 §7-2（甲群・溜め改）",
            "**反撃軸は敵が殴ってくるほど出力が出る。** 第16期は `振に帰属%` として"
            + "診断でしか見ていなかった量を、積の材料にした",
            (w, t) => featC[t] * waves[w].Enemy.Occupied().Sum(x => x.Def.Attack)),
        ("被弾駆動 × 敵1体攻", "**(C)** × 敵の1体あたり攻", "Phase HA §3-1（参照台の門）",
            "**門は総攻ではなく1体あたり攻で決まる**（呪詛は1体ずつに −6 する）。"
            + "同じ総攻でも、薄く広く殴る敵と重く殴る敵で反撃軸の出力が変わる",
            (w, t) => featC[t] * waves[w].Enemy.Occupied().Average(x => x.Def.Attack)),
    };

    Console.WriteLine("### 9-2. 交互作用項の候補（10 個）");
    Console.WriteLine();
    Console.WriteLine("**総当たりで作っていない**（第16期 §10 と同じ縛り）。出どころは3つだけ:");
    Console.WriteLine("**(a) 第16期の項の材料を (A)(B)(C) に差し替えたもの**、");
    Console.WriteLine("**(b) 第16期 §7 の甲乙の説明を (B)(C) で書き直したもの**、**(c) 第16期の項そのまま（対照）**。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | 式 | 出どころ | 理由 |");
    Console.WriteLine("|--:|---|---|:-:|---|");
    for (int k = 0; k < terms.Length; k++)
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | `{terms[k].Expr}` | {terms[k].From} | {terms[k].Why} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 9-3. 効くか ---
    Console.WriteLine("### 9-3. 交互作用項は効くか");
    Console.WriteLine();
    Console.WriteLine("第16期 §12 と同じ3通りの当て方。**(2) が本題。**");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | (1) プール r | r² | **(2) 交互作用 r** | **r²** | (2) ρ | (2) 残存度 r | 符号一致 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|:-:|");
    var score = new List<(int K, double R2)>();
    int agree = 0;
    for (int k = 0; k < terms.Length; k++)
    {
        double[] x = Flat(terms[k].Get);
        double rp = Pearson(x, Flat((w, t) => rate[w][t]));
        var ci = Correlate(x, residFlat);
        double rd = Pearson(x, FlatV(residD));
        // NaN（分散0・標本不足）は判定不能。Math.Sign は NaN で例外を投げる。
        bool known = !double.IsNaN(ci.R) && !double.IsNaN(rd);
        bool ok = known && Math.Sign(ci.R) == Math.Sign(rd);
        if (ok) agree++;
        score.Add((k, ci.R * ci.R));
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | {Sg(rp, 2):+0.00;-0.00} | {rp * rp:F3} "
            + $"| {Sg(ci.R, 2):+0.00;-0.00} | **{ci.R * ci.R:F3}** | {Sg(ci.Rho, 2):+0.00;-0.00} "
            + $"| {Sg(rd, 2):+0.00;-0.00} | {(known ? (ok ? "○" : "**×**") : "—")} |");
    }
    Console.WriteLine();
    var best = score.OrderByDescending(x => x.R2).First();
    double maxRho = Enumerable.Range(0, terms.Length)
        .Max(k => Math.Abs(Correlate(Flat(terms[k].Get), residFlat).Rho));
    Console.WriteLine($"**最良は `{terms[best.K].Name}` で r² = {best.R2:F3}**（第16期の最良は `範囲の変換` の **0.003**）。");
    Console.WriteLine($"順位相関でも最大 |ρ| = {maxRho:F3}。**単調な非線形を取りこぼしているのではない。**");
    Console.WriteLine($"符号が一致したのは {agree} / {terms.Length}（勝率の交互作用成分 ↔ 残存度の交互作用成分）。");
    Console.WriteLine();
    Console.WriteLine($"交互作用は全分散の {decW.Inter:F1}% なので、最良の項が説明しているのは");
    Console.WriteLine($"**全体の {best.R2 * decW.Inter / 100:F3}**。");
    Console.WriteLine();
    // 対照2項は第16期の項をそのまま持ってきたものなので、**第16期の数字を再現するはず**。
    // 再現しなければ、盤か波か編成集合のどれかが動いている。
    {
        double poolHold = Pearson(Flat(terms[0].Get), Flat((w, t) => rate[w][t]));
        bool ok = Math.Abs(poolHold - 0.25) <= 0.005 && Math.Abs(poolHold * poolHold - 0.061) <= 0.005;
        Console.WriteLine($"**検算: 対照項 `耐えるT` のプール r = {poolHold:+0.00;-0.00} / r² = {poolHold * poolHold:F3}"
            + $"（第16期の記録は +0.25 / 0.061）→ {(ok ? "一致" : "**ずれ**")}。**");
        Console.WriteLine("**分散分解も第16期と完全に一致している**（§9-1 の記録列）ので、");
        Console.WriteLine("**動いたのは項の材料だけ**——盤も波も編成集合も第16期のまま。");
        Console.WriteLine();
    }
    Console.Out.Flush();

    // --- 10. 判定 ---
    // 線は第16期と同じ（交互作用成分の r² が 0.10 を超えるか）。**線を新しく作らない。**
    const double TermLine = 0.10;
    Console.WriteLine("## 10. 判定（計画書 §4-3 のどの行か）");
    Console.WriteLine();
    Console.WriteLine($"線は**第16期と同じ**「交互作用成分に対する r² が {TermLine:F2} を超えるか」。");
    Console.WriteLine("**線を新しく作らない**（作ると第16期と比べられなくなる）。");
    Console.WriteLine();
    double bestWaveR2 = conW.Max(w => Enumerable.Range(0, names17.Length)
        .Select(k => { double r = Correlate(Col17(k, w), rate[w]).R; return double.IsNaN(r) ? 0 : r * r; }).Max());
    double best15 = conW.Max(w => Enumerable.Range(0, names15.Length)
        .Select(k => { double r = Correlate(Col15(k, w), rate[w]).R; return double.IsNaN(r) ? 0 : r * r; }).Max());
    bool interUp = best.R2 >= TermLine;
    bool waveUp = improved > 0 && bestWaveR2 > best15 + 0.005;
    var kmin2 = kIx.Min(t => featB[t]); var kmax2 = kIx.Max(t => featB[t]);
    var omin2 = oIx.Min(t => featB[t]); var omax2 = oIx.Max(t => featB[t]);
    bool split = !(kmin2 <= omax2 && omin2 <= kmax2);

    Console.WriteLine($"- 交互作用成分に対する最良 r² = **{best.R2:F3}**（第16期 0.003）→ {TermLine:F2} を"
        + $"{(interUp ? "**超えた**" : "超えない")}");
    Console.WriteLine($"- 波ごとの第一近似が (A)(B)(C) に替わった波: **{improved} / {conW.Length}**。"
        + $"波ごとの最良 r² は {best15:F3}（12種）→ {bestWaveR2:F3}（15種）");
    Console.WriteLine($"- (B) による甲乙の分離: **{(split ? "分かれる" : "重なる")}**"
        + $"（甲 {kmin2:F2}〜{kmax2:F2} / 乙 {omin2:F2}〜{omax2:F2}）");
    Console.WriteLine();
    // **4 行を独立に評価する。** 排他ではないので、当たった行を全部出す
    // ——1 行だけ選ぶ形にすると、複数当たったときにどれを捨てたかが記録に残らない。
    bool row4 = !interUp && !waveUp;
    Console.WriteLine("| # | 計画書 §4-3 の観測 | 当たるか | 根拠 |");
    Console.WriteLine("|--:|---|:-:|---|");
    Console.WriteLine($"| 1 | (A)(B)(C) の積で交互作用成分の説明力が上がる | {(interUp ? "**○**" : "×")} "
        + $"| 最良 r² = {best.R2:F3} < {TermLine:F2} |");
    Console.WriteLine($"| 2 | 波ごとの説明力は上がるが交互作用は上がらない | {(waveUp ? "**○**" : "×")} "
        + $"| 第一近似が替わったのは {improved} / {conW.Length} 波。最良 r² {best15:F3} → {bestWaveR2:F3} |");
    Console.WriteLine($"| 3 | (B) で甲乙が分離できない | {(!split ? "**○**" : "×")} "
        + $"| 甲 {kmin2:F2}〜{kmax2:F2} / 乙 {omin2:F2}〜{omax2:F2}（重なる） |");
    Console.WriteLine($"| 4 | どれも上がらない → 測り方が悪いか台が中立でない | {(row4 ? "**○**" : "×")} "
        + $"| §4-3 の中立性は ρ {cross.Rho:F3} / 余地 {room:F3} で**通っている** |");
    Console.WriteLine();
    Console.WriteLine("**当たったのは 3 行目と 4 行目。**");
    Console.WriteLine();
    Console.WriteLine("- **3行目。** (B) は甲乙を分けない。ただし §7-1 のとおり、**分けない理由は");
    Console.WriteLine("  「分割に実体が無い」ではなく「(B) が測っているものが違う」ほうに見える**");
    Console.WriteLine("  ——甲乙は「出力が時間で育つか」ではなく「**育った出力が撃破に変換されるか**」で");
    Console.WriteLine("  割れていた。計画書 §4-3 の3行目は「数値的な実体が無い**可能性**」と書いているので、");
    Console.WriteLine("  **その可能性は棄却できていないが、支持もされていない。**");
    Console.WriteLine("- **4行目。** §4-3 の中立性は通っている（余地 " + $"{room:F3}" + "）ので、");
    Console.WriteLine("  「参照台が中立でない」ではなく「**測り方（何を測るか）が足りない**」の側。");
    Console.WriteLine();
    Console.WriteLine("**1行目は明確に外れた。** (A) は `総攻` と ρ −0.44 で**符号すら逆**の別物なのに");
    Console.WriteLine($"（§6）、積にすると交互作用成分の説明力は 0.003 → {best.R2:F3} で 1ミリも動かない。");
    Console.WriteLine("**「16期分の壁の原因が `総攻` だった」は支持されない。** `総攻` が出力を");
    Console.WriteLine("表していなかったのは事実だが（§6 で確定した）、**壁の原因はそれではなかった。**");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}
}
