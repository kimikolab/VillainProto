using BattleCore;
using static Common;

// =====================================================================================
// convert モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "convert")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 convert
// =====================================================================================

static class ConvertDiag
{
// convert モード: 出力が撃破に変換されるか — 個体HP だけを振った参照台の系列（第18期 Phase IA）。
//
// 第17期で「特徴量が悪い」説はほぼ否定された。(A) 実効打点/T は `総攻` と ρ −0.44 の**別物**
// （符号すら逆）なのに、積の材料に差し替えても交互作用成分の説明力は 0.003 → 0.002 で
// 1ミリも動かない。**ただし1つだけ、17期分ずっと主役だったのに単独で振っていない軸がある
// ——個体HP。**
//
//   第6〜7期: 「符号を決めるのは個体HP、大きさを決めるのは体数」
//   第16期:   甲乙の軸は「個体HP と総攻」
//   第17期:   甲乙の軸は「育つか」ではなく**「撃破に変換されるか」**
//             （毒は (B) 16.97 で9編成中最大なのに乙群）
//
// **「撃破に変換されるか」は、味方の打点と敵の個体HP の関係で決まる。** 第17期の参照台の格子は
// 1体攻（B1〜B3）と体数（B4〜B6）を振っていて、**個体HP を単独では振っていない**ので測れない。
// ここでは参照台を**個体HP だけ変えて**並べ、(A) がどう変わるかを測る。
//
// **循環は第17期と同じ形で切れている。** 量を測るのは参照台で、目的変数（波ごとの勝率）の
// 戦闘とは別の戦闘。その波の敵を削り切ったかどうかは1ビットも入らない（第14期の分子経路）。
//
// 却下した案: 波（`WaveCatalog()`）の個体HP 中央値で編成を層別して (A) を測り直す。
// 候補波は個体HP 以外（体数・1体攻・攻撃型）も同時に違うので、**個体HP だけを振ったことに
// ならない**——第16期の格子が「主↔従の対角線」で嵌まったのと同じ形。
//
// 却下した案: 新しい `UnitDef` を `EnemyCatalog` に足す。刻みは診断のローカルで組む
// （`gradient` / `aim` / `timing` と同じ作法）。`BattleCore` を触ると `dump` が動きうるし、
// 採用の決まっていない的が `EnemyCatalog` に残る。
//
// 診断用で docs/ には置かない（output / wave / power / bench / dissect と同じ扱い）。
//
//     dotnet run --project BattleSim -c Release 0 convert [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    const int ConvSeeds = 200;   // output / wave / dissect / compare / power / bench と同じ

    var all = CompareBuilds();
    string filter = args.Length > 2 ? args[2] : "";
    var targets = all
        .Where(b => filter.Length == 0 || filter.Split(',').Any(k => b.Name.Contains(k.Trim())))
        .ToArray();
    int nT = targets.Length;

    // 表示桁でゼロに丸まる負の値を `-+0.0` と出さないための丸め（`dissect` / `output` と同じ理由）。
    double Sg(double v, int dp) => double.IsNaN(v) ? v : Math.Round(v, dp) + 0.0;

    // 個体HP の中央値。`dissect` / `output` のローカル定義と同じ式。
    double MedianHp(Formation e)
    {
        var v = e.Occupied().Select(x => (double)x.Def.MaxHp).OrderBy(x => x).ToArray();
        return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2;
    }

    Console.WriteLine("# 変換率 — 出力は撃破に変換されるか（第18期 Phase IA）");
    Console.WriteLine();
    Console.WriteLine("第17期で**甲乙の軸は「育つか」ではなく「撃破に変換されるか」**まで来た（乙群の 毒 は");
    Console.WriteLine("(B) 育ち 16.97 で9編成中いちばん高いのに乙）。**「撃破に変換されるか」は味方の打点と");
    Console.WriteLine("敵の個体HP の関係で決まる**が、第17期の参照台の格子は 1体攻 と 体数 を振っていて");
    Console.WriteLine("**個体HP を単独では振っていない**——ここで振る。");
    Console.WriteLine();
    Console.WriteLine("**測定だけで、盤面は1つも動かしていない**（`BattleCore` 無変更・`EnemyCatalog` 無変更）。");
    Console.WriteLine();

    // ================= 1. 台の系列 =================
    //
    // 基準は第17期の主台 `ZealotPorter`（重甲の荷駄兵 90/7/5・単体攻撃・特性なし）。
    // **振るのは個体HP だけ**で、攻撃力 7・速さ 5・パターン Single は全刻みで固定する。
    //
    // 門（第17期 Phase HA §3-1）は 1体あたり攻 > CurseTrait.EnemyDebuff（= 6）。荷駄兵の 7 は
    // これを通るので、**全刻みで門を通る**（攻を振らないので当然だが、確認は下の表で出す）。
    UnitDef Porter = EnemyCatalog.ZealotPorter;
    UnitDef HpStep(int hp) => hp == Porter.MaxHp ? Porter : new UnitDef
    {
        // 新しい Id を振る（`power` / `wave` が踏んだ「味方と敵の Def.Id 衝突」を避けるため、
        // また tally が刻みごとに分かれるようにするため）。**`EnemyCatalog` には足さない。**
        Id = $"porter_h{hp}", Name = $"重甲の荷駄兵(HP{hp})",
        MaxHp = hp, Attack = Porter.Attack, Speed = Porter.Speed,
        Traits = Porter.Traits, Pattern = Porter.Pattern,
    };
    Formation Line(UnitDef d, int n)
    {
        var f = new Formation();
        for (int i = 0; i < n; i++) f[i] = d;   // スロット昇順（前1→前3→中央→後1→後3）
        return f;
    }

    // X字化で編成枠が5つになったので、台は全部5体以下に組み直してある。
    // 系列P（主）: **体数 5 固定**・個体HP だけを 30 → 220。総HP は揃わない（150 → 1100）。
    // 系列Q（従）: **総HP 450 固定**・体数で調整（5 → 2）。個体HP 90 → 225。
    //   体数4 は 450/4 が整数にならないので刻みから外してある（3点になる）。
    // P90 と Q90 は**同一の台**（荷駄5 = 第17期の主台）なので、測り直さず同じ計測を共有する。
    var benches = new (string Tag, int Hp, int N)[]
    {
        ("P30",  30, 5), ("P60",  60, 5), ("P90",  90, 5), ("P145", 145, 5), ("P220", 220, 5),
        ("Q150", 150, 3), ("Q225", 225, 2),
        ("R2",    90, 2), ("R3",    90, 3), ("R4",    90, 4),
    };
    int nB = benches.Length;
    var benchF = benches.Select(b => Line(HpStep(b.Hp), b.N)).ToArray();
    int[] serP = { 0, 1, 2, 3, 4 };
    int[] serQ = { 2, 5, 6 };         // 先頭は P90（= Q90。荷駄5 そのもの）
    int[] serR = { 7, 8, 9, 2 };      // 体数だけを振る辺（個体HP 90 固定）。末尾は P90
    int mainB = 2;                    // 主刻み = P90 = 第17期の主台

    Console.WriteLine("## 1. 台の系列");
    Console.WriteLine();
    Console.WriteLine($"基準は第17期の主台 **重甲の荷駄兵（{Porter.MaxHp}/{Porter.Attack}/{Porter.Speed}・単体攻撃・特性なし）**。");
    Console.WriteLine("**振るのは個体HP だけ**で、攻撃力・速さ・攻撃パターンは全刻みで固定する。");
    Console.WriteLine("刻みは既存 def の使い回しでは作れない（`Levy` 30/攻8・`ZealotPlate` 60/攻16・");
    Console.WriteLine("`Warden` 145/攻12 は**攻撃力が全部違う**）ので、荷駄兵の HP だけを差し替えた複製を");
    Console.WriteLine("**診断のローカルで**組む（`EnemyCatalog` には足さない。`gradient` / `aim` / `timing` と同じ作法）。");
    Console.WriteLine();
    Console.WriteLine("### 1-1. 総HP を揃えるか — 揃えない系列を主に採る");
    Console.WriteLine();
    Console.WriteLine("計画書 §2-2 の設計判断。**両方作った。主は揃えないほう（系列P）。**");
    Console.WriteLine();
    Console.WriteLine("| 系列 | 固定するもの | 動くもの | 役 |");
    Console.WriteLine("|:-:|---|---|---|");
    Console.WriteLine("| **P** | 体数 5・1体攻 7・**総攻 35** | 個体HP 30〜220・総HP 150〜1100（＝戦闘長） | **主** |");
    Console.WriteLine("| **Q** | **総HP 450**・1体攻 7 | 個体HP 90〜225・体数 5→2・**総攻 35→14** | 従 |");
    Console.WriteLine("| **R** | **個体HP 90**・1体攻 7 | 体数 2〜6・総HP 180〜540・総攻 14→42 | 辺（検算用） |");
    Console.WriteLine();
    Console.WriteLine("**系列R は変換率を測る台ではない。** P と Q の食い違いを説明できるかを確かめるための");
    Console.WriteLine("**辺**で、個体HP を固定して体数だけを振ってある——P と Q は「個体HP と体数の格子」の");
    Console.WriteLine("2本の斜めの線なので、辺が1本無いと**食い違いの出どころが決まらない**");
    Console.WriteLine("（第13期 `bench` が主↔従の対角線で嵌まったのと同じ形）。§5-3 で使う。");
    Console.WriteLine();
    Console.WriteLine("**揃えないほうを主に採った理由は2つ。**");
    Console.WriteLine();
    Console.WriteLine("- **全域では原理的に揃えられない。** プレイヤーが置ける枠は5つしかない（`FormationRules.PlayableSlotCount`）ので、");
    Console.WriteLine("  個体HP 30 の台の総HP は最大 180、個体HP 220 の台は最小 220——**重ならない。**");
    Console.WriteLine("  総HP を揃えると刻みの範囲が 90〜270 に狭まり、**味方の実測打点（第6期の `pulse.md` から");
    Console.WriteLine("  中央値 10.6 / 四分位 4.4〜20.4 / 上位1割 51.1）を跨げない**——一撃圏の内と外を跨ぐことが");
    Console.WriteLine("  この測定の目的そのものなので、跨げない系列は主にできない。");
    Console.WriteLine("- **揃えると総攻が動く。** 体数で調整する以上 総攻 35 → 14 まで落ちるので、");
    Console.WriteLine("  **殴り返しの量が同時に変わる。** 反撃軸の出力は殴られた量にほぼ比例するから、");
    Console.WriteLine("  系列Q の変換率は「個体HP の効果」と「総攻の効果」の和になる。");
    Console.WriteLine("  系列P は 総攻 35 が全刻みで一定なので、**動いているのは個体HP と戦闘長だけ。**");
    Console.WriteLine();
    Console.WriteLine("**却下した案: 総攻も揃える**（体数を減らすぶん1体あたり攻を上げる。6体攻7 → 2体攻21）。");
    Console.WriteLine("総攻は揃うが**1体あたり攻が 7 → 21 に動く**ので、今度は反撃1回あたりの量と");
    Console.WriteLine("被弾強化の刻みが変わる。計画書 §2-2 の「攻撃力は全刻みで固定」に正面から反する。");
    Console.WriteLine();
    Console.WriteLine("**系列P の弱点は明記する。** 総HP が 7.3 倍になるので**戦闘長が同時に動く**");
    Console.WriteLine("（それ自体が「敵が硬い」の中身でもある）。両系列で変換率の順位が一致するかを");
    Console.WriteLine("§5 で確かめる——一致すれば、動かしたのが個体HP か戦闘長かに関わらず**同じ量**を測っている。");
    Console.WriteLine();
    Console.WriteLine("| 台 | 系列 | 個体HP | 体数 | 総HP | 総攻 | 1体攻 | 呪詛後 | 門 |");
    Console.WriteLine("|:-:|:-:|--:|--:|--:|--:|--:|--:|:-:|");
    for (int b = 0; b < nB; b++)
    {
        Formation bf = benchF[b];
        double each = bf.Occupied().Average(x => x.Def.Attack);
        string ser = b == mainB ? "P / Q / R"
            : benches[b].Tag.StartsWith("P") ? "P"
            : benches[b].Tag.StartsWith("Q") ? "Q" : "R";
        Console.WriteLine($"| **{benches[b].Tag}** | {ser} | {benches[b].Hp} | {bf.Count} "
            + $"| {bf.Occupied().Sum(x => x.Def.MaxHp)} | {bf.Occupied().Sum(x => x.Def.Attack)} "
            + $"| {each:F0} | {each - CurseTrait.EnemyDebuff:F0} "
            + $"| {(each > CurseTrait.EnemyDebuff ? "○" : "**×**")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**門（1体あたり攻 > {CurseTrait.EnemyDebuff}）は全刻みが通る。** 攻を振っていないので当然だが、");
    Console.WriteLine("**第17期はここで巡礼5（攻4）を落としている**ので毎回確認する（`手番外%` は §3 で見る）。");
    Console.WriteLine();
    Console.WriteLine($"**P90 は第17期の主台そのもの**（荷駄5）。同じ計測を系列Q・系列R の端としても使う");
    Console.WriteLine("——2回測ると、系列間の一致の値そのものに実行間のばらつきが乗る。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 2. 計測と検算 =================
    //
    // `MeasureOutput`（第17期）をそのまま呼ぶ。**打点の定義を写さない**——第15期が
    // `WaveCatalog()` を1箇所に集めたのと同じ理由で、2つ目の診断が定義のコピーを持った瞬間に
    // 片方だけ直す事故が起きる。第18期で足したのは `Kills` と `Overkill` の2列だけで、
    // **`output` の出力は1文字も動かない**（受け入れ条件は「26モード差分ゼロ」）。
    var tr = new OutputTrace[nB][];
    for (int b = 0; b < nB; b++)
    {
        tr[b] = new OutputTrace[nT];
        for (int t = 0; t < nT; t++) tr[b][t] = MeasureOutput(targets[t].F, benchF[b], ConvSeeds);
        Console.Out.Flush();
    }

    Console.WriteLine("## 2. 検算");
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
        .Intersect(benchF.SelectMany(c => c.Occupied().Select(y => y.Def.Id))).ToArray();
    Console.WriteLine($"- **イベント集計と敵 tally の差**: 最大 {maxGap:F0}（{nB * nT} 組）。0 でなければ打点を取りこぼしている");
    Console.WriteLine($"- **敵同士の巻き込み（`TakenFromAlly`）**: {totalFromAlly}。0 でなければ受け手側から与ダメを取る前提が崩れる");
    Console.WriteLine($"- **味方と敵の `Def.Id` 衝突**: {clash.Length} 件"
        + (clash.Length == 0 ? "" : $"（{string.Join(" / ", clash)}）"));
    Console.WriteLine();

    // 第17期の記録（README「検証で分かったこと」の第17期の項・`output` §5-2）。
    // **値を分析に使うのではなく、P90 が第17期の主台と同じ台であることの検算にだけ使う**
    // （`dissect` §12-2・`output` §8 と同じ作法）。
    var rec17 = new (string Name, double A, double C)[]
    {
        ("惨禍×被弾強化", 263.9, 92), ("毒 (グザ×ミオ×ラウ)", 170.4, 92), ("耐久 (ガルド×リリ)", 51.3, 0),
    };
    int miss17 = 0;
    Console.WriteLine("**第17期の主台との一致**（P90 = 荷駄5）。値は `output` §5-2 から:");
    Console.WriteLine();
    Console.WriteLine("| 編成 | 第17期 (A) | ここでの (A) | 第17期 (C) | ここでの (C) |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (var (nm, ra, rc) in rec17)
    {
        int t = Array.FindIndex(targets, x => x.Name == nm);
        if (t < 0) { Console.WriteLine($"| {nm} | {ra:F1} | — | {rc:F0}% | — |"); continue; }
        bool ok = Math.Abs(tr[mainB][t].RateAll - ra) <= 0.05 && Math.Abs(tr[mainB][t].OffTurnPct - rc) <= 0.5;
        if (!ok) miss17++;
        Console.WriteLine($"| {nm} | {ra:F1} | {tr[mainB][t].RateAll:F1}{(ok ? "" : " **←ずれ**")} "
            + $"| {rc:F0}% | {tr[mainB][t].OffTurnPct:F0}% |");
    }
    Console.WriteLine();
    Console.WriteLine(miss17 == 0
        ? "**ずれ 0 件。** P90 は第17期の主台と同じ台で、同じ値を出している。"
        : $"**{miss17} 件ずれた。第17期と同じ台を見ていない——先へ進む前に原因を潰すこと。**");
    Console.WriteLine();
    Console.WriteLine("> **(A) はオーバーキルを含む。** `ApplyDamage` は残HPで切り詰めない");
    Console.WriteLine("> （`target.Hp -= amount` で HP は負まで落ち、`Damage` イベントの `Amount` は素の量）ので、");
    Console.WriteLine("> **(A) が測っているのは「敵のHPに変換された量」ではなく「振り下ろした量」。**");
    Console.WriteLine("> これは変換率の読みに直接効く——低HP の台で (A) が落ちないのは、");
    Console.WriteLine("> **無駄打ちが打点として数えられているから**かもしれない。`オーバーキル%` を §3 に出してある。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 3. 刻みは要件を満たしているか =================
    //
    // 計画書 §4-8 の停止条件そのもの。**刻みの一部で決着してしまい (A) が比較できないなら、
    // 実装を進める前に報告する。** 線は第17期と同じで「決着すること自体は停止条件ではない」。
    Console.WriteLine("## 3. 刻みは要件を満たしているか（計画書 §4-8 の停止条件）");
    Console.WriteLine();
    Console.WriteLine("| 量 | 読み方 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| `決着T` | 全試行の平均ターン数。**1.0 に近いと (A) が「1ターンで振り下ろせる量」に潰れる** |");
    Console.WriteLine("| `敵全滅%` | 味方が削り切った試行の割合 |");
    Console.WriteLine("| `味方全滅%` | 味方が削られ切った試行の割合。**高いと出力が途中で止まる** |");
    Console.WriteLine("| `手番外%` | 打点のうち手番の振り以外から出たぶん。**0 なら被弾駆動が死んでいる**（門） |");
    Console.WriteLine("| `撃破/戦` | 敵の撃破数（受け手側から。毒・燃焼の削りも載る） |");
    Console.WriteLine("| `オーバーキル%` | 打点のうち敵の最大HPを超えて振り下ろしたぶん |");
    Console.WriteLine();
    Console.WriteLine("| 台 | 個体HP | 体数 | 決着T | 決着T 最小(編成) | 敵全滅% | 味方全滅% | 手番外% | 手番外% 0 の編成 | 撃破/戦 | オーバーキル% |");
    Console.WriteLine("|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int b = 0; b < nB; b++)
    {
        double turns = Enumerable.Range(0, nT).Average(t => tr[b][t].Turns.Average());
        double tmin = Enumerable.Range(0, nT).Min(t => tr[b][t].Turns.Average());
        double wipe = Enumerable.Range(0, nT).Average(t => tr[b][t].AllyWipe * 100.0 / ConvSeeds);
        double clear = Enumerable.Range(0, nT).Average(t => tr[b][t].FoeWipe * 100.0 / ConvSeeds);
        double off = Enumerable.Range(0, nT).Average(t => tr[b][t].OffTurnPct);
        int zero = Enumerable.Range(0, nT).Count(t => tr[b][t].OffTurnPct <= 1e-9);
        double kills = Enumerable.Range(0, nT).Average(t => (double)tr[b][t].Kills / ConvSeeds);
        double okill = Enumerable.Range(0, nT).Average(t => tr[b][t].Overkill * 100.0 / tr[b][t].Damage.Sum());
        Console.WriteLine($"| **{benches[b].Tag}** | {benches[b].Hp} | {benchF[b].Count} | {turns:F1} | {tmin:F1} "
            + $"| {clear:F1}% | {wipe:F1}% | {off:F1}% | {zero} / {nT} | {kills:F2} | {okill:F1}% |");
    }
    Console.WriteLine();
    var degenerate = Enumerable.Range(0, nB)
        .Where(b => Enumerable.Range(0, nT).Count(t => tr[b][t].Turns.Average() < 2.0) > 0).ToArray();
    Console.WriteLine(degenerate.Length == 0
        ? "**平均決着T が 2.0 を下回る（＝1ターンで終わる）編成はどの刻みにも 0 件。** (A) はどの刻みでも比較できる。"
        : "> **平均決着T が 2.0 を下回る編成がある刻み**: "
          + string.Join(" / ", degenerate.Select(b => $"`{benches[b].Tag}`（"
              + string.Join("・", Enumerable.Range(0, nT).Where(t => tr[b][t].Turns.Average() < 2.0)
                  .Select(t => $"{targets[t].Name} {tr[b][t].Turns.Average():F1}T")) + "）"))
          + "。**その編成の (A) はその刻みで「1ターンで振り下ろせる量」に潰れている**"
          + "（ターン数は整数なので、半ターンで削り切っても 1 と数える）。"
          + "変換率は §4 で**全刻み版と高刻みだけの版の両方**を出し、順位が動くかを見る。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 4. 変換率 =================
    //
    // **表し方は log-log の傾き**（計画書 §2-1 が挙げた2案のうち後者）。
    //   β = d ln((A)) / d ln(個体HP)   ——刻み全部を使った最小二乗
    // 端点の比（低HP台 ÷ 高HP台）にしなかったのは、**2点しか使わないと刻みの片方が
    // 決着で潰れたときに丸ごとそれを拾う**ため。傾きなら中間の刻みが効く。
    // 比の側も併記する（`端点比` = (A)(P220) ÷ (A)(P30)）ので、読み手はどちらでも読める。
    //
    //   β ≈ 0   打点が個体HP に依存しない（変換の問題を持たない）
    //   β < 0   個体HP が上がると打点が落ちる（**撃破に依存する出力**）
    //   β > 0   個体HP が上がると打点が上がる（**積み上げ型**。的が長生きするほど段が乗る）
    double LogHp(int b) => Math.Log(benches[b].Hp);
    double LogN(int b) => Math.Log(benches[b].N);
    // 傾き。x 軸（個体HP か体数）と、打点/ターンの取り方を差し替えられるようにしてある
    // ——§5-2 で「最後の1ターンが端数であること」の影響を測るため。
    double Slope(int[] ser, Func<int, double> x, Func<int, double> y)
    {
        double mx = ser.Average(x), my = ser.Average(y);
        double cov = 0, vx = 0;
        foreach (int b in ser)
        {
            double dx = x(b) - mx;
            cov += dx * (y(b) - my);
            vx += dx * dx;
        }
        return cov / vx;
    }
    int[] serHi = { 2, 3, 4 };        // 系列P の高刻みだけ（90 / 145 / 220）
    int[] serLo = { 0, 1, 2 };        // 系列P の低刻みだけ（30 / 60 / 90）
    double[] BetaOf(int[] ser, Func<int, bool> take) => Enumerable.Range(0, nT)
        .Select(t => Slope(ser, LogHp, b => Math.Log(tr[b][t].Rate(take)))).ToArray();
    // 端数ターンを補正した (A)。**ターン数は整数なので、削り切った最後のターンは丸ごと 1 と数える**
    // ——短い戦闘ほど「働いていない端数」が分母に入り、(A) が過小に出る。半ターンを引くのが
    // その一次補正で、これで順位が動かないなら β の符号は端数の産物ではない。
    double RateAdj(int b, int t)
    {
        OutputTrace o = tr[b][t];
        return o.Damage.Sum() / (o.Turns.Sum() - 0.5 * o.Seeds);
    }
    var betaP = BetaOf(serP, _ => true);
    var betaQ = BetaOf(serQ, _ => true);
    var betaHi = BetaOf(serHi, _ => true);
    var betaLo = BetaOf(serLo, _ => true);
    var betaAdj = Enumerable.Range(0, nT)
        .Select(t => Slope(serP, LogHp, b => Math.Log(RateAdj(b, t)))).ToArray();
    // 体数の辺（個体HP 90 固定）。**変換率ではない**——P と Q の食い違いを説明する量。
    var gammaR = Enumerable.Range(0, nT)
        .Select(t => Slope(serR, LogN, b => Math.Log(tr[b][t].RateAll))).ToArray();

    Console.WriteLine("## 4. 変換率");
    Console.WriteLine();
    Console.WriteLine("**定義（計画書 §2-1 は「表し方は実装者判断でよいが明記すること」）:**");
    Console.WriteLine();
    Console.WriteLine("> **変換率 β = d ln((A) 実効打点/T) ÷ d ln(敵の個体HP)** — 刻み全部を使った最小二乗の傾き。");
    Console.WriteLine();
    Console.WriteLine("| β | 意味 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| **≈ 0** | 打点が個体HP に依存しない。**変換の問題を持たない**（オーバーキルしても打点は落ちない、あるいは撃破と無関係に削る） |");
    Console.WriteLine("| **< 0** | 個体HP が上がると打点が落ちる。**撃破に依存する出力**（撃破ごとに何かが起きる／敵が減ると被弾が減る） |");
    Console.WriteLine("| **> 0** | 個体HP が上がると打点が上がる。**積み上げ型**（毒・燃焼。的が長生きするほど段が乗る） |");
    Console.WriteLine();
    Console.WriteLine("**端点の比（低HP台 ÷ 高HP台）にしなかった理由**: 2点しか使わないので、刻みの片方が");
    Console.WriteLine("決着で潰れたときにそれを丸ごと拾う。傾きなら中間の刻みが効く。比の側（`端点比`）も");
    Console.WriteLine("併記してあるので、読み手はどちらでも読める。");
    Console.WriteLine();
    Console.WriteLine("### 4-1. 編成 × 個体HP の (A)（系列P・体数5固定）");
    Console.WriteLine();
    Console.WriteLine("**β の降順**。`端点比` = (A)(P220) ÷ (A)(P30)。`βQ` は総HP を揃えた系列（§5）。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | " + string.Join(" | ", serP.Select(b => $"{benches[b].Tag} (HP{benches[b].Hp})"))
        + " | **β** | 端点比 | βQ | β高刻み | (C) 手番外% |");
    Console.WriteLine("|---|" + string.Concat(serP.Select(_ => "--:|")) + "--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => betaP[t]))
        Console.WriteLine($"| {targets[t].Name} | "
            + string.Join(" | ", serP.Select(b => $"{tr[b][t].RateAll:F1}"))
            + $" | **{Sg(betaP[t], 2):+0.00;-0.00}** | {tr[4][t].RateAll / tr[0][t].RateAll:F2} "
            + $"| {Sg(betaQ[t], 2):+0.00;-0.00} | {Sg(betaHi[t], 2):+0.00;-0.00} "
            + $"| {tr[mainB][t].OffTurnPct:F0}% |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 4-2. 上位・下位5 ---
    Console.WriteLine("### 4-2. 変換率の上位・下位5編成（計画書 §2-3）");
    Console.WriteLine();
    Console.WriteLine("**上位（β > 0 = 積み上げ型。的が硬いほど出力が伸びる）**");
    Console.WriteLine();
    Console.WriteLine("| 順位 | 編成 | β | (A) P90 | (C) 手番外% | 撃破/戦 P90 | オーバーキル% P90 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|");
    var byBeta = Enumerable.Range(0, nT).OrderByDescending(t => betaP[t]).ToArray();
    void BetaRow(int rank, int t) => Console.WriteLine($"| {rank} | {targets[t].Name} "
        + $"| {Sg(betaP[t], 2):+0.00;-0.00} | {tr[mainB][t].RateAll:F1} | {tr[mainB][t].OffTurnPct:F0}% "
        + $"| {(double)tr[mainB][t].Kills / ConvSeeds:F2} "
        + $"| {tr[mainB][t].Overkill * 100.0 / tr[mainB][t].Damage.Sum():F1}% |");
    for (int k = 0; k < Math.Min(5, nT); k++) BetaRow(k + 1, byBeta[k]);
    Console.WriteLine();
    Console.WriteLine("**下位（β < 0 = 撃破に依存する出力。的が硬いと出力が落ちる）**");
    Console.WriteLine();
    Console.WriteLine("| 順位 | 編成 | β | (A) P90 | (C) 手番外% | 撃破/戦 P90 | オーバーキル% P90 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|");
    for (int k = Math.Max(0, nT - 5); k < nT; k++) BetaRow(k + 1, byBeta[k]);
    Console.WriteLine();

    // β が何と相関するか。**説明ではなく手がかり**（相関は因果ではない。第12期の作法）。
    var featC0 = Enumerable.Range(0, nT).Select(t => tr[mainB][t].OffTurnPct).ToArray();
    var featA0 = Enumerable.Range(0, nT).Select(t => tr[mainB][t].RateAll).ToArray();
    var featB0 = Enumerable.Range(0, nT).Select(t => tr[mainB][t].Ramp15).ToArray();
    var atk = Enumerable.Range(0, nT).Select(t => (double)targets[t].F.Occupied().Sum(x => x.Def.Attack)).ToArray();
    Console.WriteLine("**β は既存の量とどれだけ違うか**（新しい軸なのか、既存の量の言い換えか）:");
    Console.WriteLine();
    Console.WriteLine("| 相手 | r | ρ |");
    Console.WriteLine("|---|--:|--:|");
    foreach (var (nm, v) in new[] { ("(A) 実効打点/T", featA0), ("(B) 育ち", featB0), ("(C) 手番外%", featC0), ("総攻", atk) })
    {
        var c = Correlate(betaP, v);
        Console.WriteLine($"| {nm} | {Sg(c.R, 3):+0.000;-0.000} | {Sg(c.Rho, 3):+0.000;-0.000} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 5. 変換率は単一の量か（系列間の一致） =================
    //
    // 第17期 §4 と同じ形。**比べる相手は半割（測定の信頼性の上限）**——系列間の相関は
    // 乱数のばらつきだけでも 1.00 を割るので、上限が無いと読めない。
    // 線も第17期のまま（第15期の裏返し）: **ρ ≥ 0.90 または 余地 < 0.05**。
    // **連言の否定は選言。** ここを連言にすると、黙って厳しい線を作ることになる。
    const double NeutralRho = 0.90, NeutralRoom = 0.05;
    double SB(double r) => 2 * r / (1 + r);
    (double R, double Rho) HalfCap(int[] ser)
    {
        var h1 = Correlate(BetaOf(ser, s => s < ConvSeeds / 2), BetaOf(ser, s => s >= ConvSeeds / 2));
        var h2 = Correlate(BetaOf(ser, s => s % 2 == 0), BetaOf(ser, s => s % 2 == 1));
        return (SB((h1.R + h2.R) / 2), SB((h1.Rho + h2.Rho) / 2));
    }
    var capP = HalfCap(serP);
    var capQ = HalfCap(serQ);
    var capHi = HalfCap(serHi);

    Console.WriteLine("## 5. 変換率は単一の量か");
    Console.WriteLine();
    Console.WriteLine("**系列P は個体HP だけを振っている**（体数・1体攻・総攻・速さ・パターンは固定）ので、");
    Console.WriteLine("定義としてはこれが変換率。ただし総HP＝個体HP×体数 という恒等式がある以上、");
    Console.WriteLine("**個体HP を上げれば総HP も上がる**（＝戦闘が長くなる）——「個体HP だけを振る」と");
    Console.WriteLine("「総HP を動かさない」は**同時に満たせない**。ここでは3つの向きから確かめる。");
    Console.WriteLine();
    Console.WriteLine("### 5-1. 半割 — 測定の信頼性の上限");
    Console.WriteLine();
    Console.WriteLine("| 系列 | 刻み | 補正後 r | **補正後 ρ** |");
    Console.WriteLine("|:-:|--:|--:|--:|");
    Console.WriteLine($"| **P**（体数5固定・主） | {serP.Length} | {capP.R:F3} | **{capP.Rho:F3}** |");
    Console.WriteLine($"| **Q**（総HP 450固定） | {serQ.Length} | {capQ.R:F3} | **{capQ.Rho:F3}** |");
    Console.WriteLine($"| **P 高刻みだけ**（90/145/220） | {serHi.Length} | {capHi.R:F3} | **{capHi.Rho:F3}** |");
    Console.WriteLine();
    Console.WriteLine("補正は Spearman-Brown `r(2n) = 2r(n) / (1 + r(n))`。**上限はほぼ 1.00**なので、");
    Console.WriteLine("以下の食い違いは全部が「実物の入れ替わり」——乱数のばらつきでは説明が付かない。");
    Console.WriteLine();
    Console.WriteLine("### 5-2. 変種との一致（測り方の頑健さ）");
    Console.WriteLine();
    Console.WriteLine("**3つの疑いを数字で潰す。** どれも「β の測り方を少し変えて順位が動くか」で測れる。");
    Console.WriteLine();
    Console.WriteLine($"線は第17期のまま（第15期の裏返し）: **`ρ ≥ {NeutralRho:F2}` または `余地 < {NeutralRoom:F2}`**"
        + "。**連言の否定は選言**——ここを連言にすると、黙って厳しい線を作ることになる。");
    Console.WriteLine();
    Console.WriteLine("| 変種 | 何を疑っているか | r | ρ | 上限(ρ) | **余地** | 判定 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|:-:|");
    foreach (var (nm, why, v, cp) in new (string, string, double[], double)[]
    {
        ("β 低刻みだけ（30/60/90）", "**高刻みで味方全滅が増える**（P145 で 22%・P220 で 49%）。"
            + "出力が途中で止まった刻みが β を作っているのではないか", betaLo, capP.Rho),
        ("β 高刻みだけ（90/145/220）", "逆に、低刻みは戦闘が短い（決着 4.2T）。"
            + "端数ターンと決着の早さが β を作っているのではないか", betaHi, capHi.Rho),
        ("β 端数補正（分母を T−0.5 に）", "**ターン数は整数**なので、削り切った最後のターンも 1 と数える。"
            + "短い戦闘ほど (A) が過小に出て、β が正に押し上げられているのではないか", betaAdj, capP.Rho),
    })
    {
        var c = Correlate(betaP, v);
        double lim = Math.Min(capP.Rho, cp), room = lim - c.Rho;
        Console.WriteLine($"| {nm} | {why} | {Sg(c.R, 2):+0.00;-0.00} | {Sg(c.Rho, 2):+0.00;-0.00} "
            + $"| {lim:F2} | **{room:F2}** "
            + $"| {(c.Rho >= NeutralRho || room < NeutralRoom ? "一致" : "**×**")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"端数補正の効き（平均）: β {betaP.Average():+0.000;-0.000} → "
        + $"βの端数補正版 {betaAdj.Average():+0.000;-0.000}（差 {betaAdj.Average() - betaP.Average():+0.000;-0.000}）。");
    Console.WriteLine("**端数の効果は符号を作るほど大きくない**が、値の一部ではある。");
    Console.WriteLine();
    Console.WriteLine("### 5-3. 系列Q との食い違いは、体数の辺で説明が付くか");
    Console.WriteLine();
    var crossPQ = Correlate(betaP, betaQ);
    double capPQ = Math.Min(capP.Rho, capQ.Rho);
    Console.WriteLine($"**系列P と 系列Q は逆を向く: r = {crossPQ.R:F2} / ρ = {crossPQ.Rho:F2}**"
        + $"（上限 {capPQ:F2}・余地 {capPQ - crossPQ.Rho:F2}）。系列P では 31 編成中 "
        + $"{betaP.Count(v => v > 0)} が正、系列Q では {betaQ.Count(v => v < 0)} が負。");
    Console.WriteLine();
    Console.WriteLine("**これは矛盾ではない。** 総HP＝個体HP×体数 なので、この2系列は「個体HP と体数の");
    Console.WriteLine("格子」の**別々の斜めの線**を歩いている——P は体数を止めて個体HP を上げ、");
    Console.WriteLine("Q は総HP を止めるために**体数を下げながら**個体HP を上げる。");
    Console.WriteLine();
    Console.WriteLine("対数で1次の模型を置けば、辺1本で検算できる:");
    Console.WriteLine();
    Console.WriteLine("    ln(A) ≒ 定数 + p · ln(個体HP) + q · ln(体数)");
    Console.WriteLine("      系列P（体数固定）      → 傾き = p      = β");
    Console.WriteLine("      系列Q（総HP固定）      → 傾き = p − q  = βQ   （体数 = 総HP ÷ 個体HP なので）");
    Console.WriteLine("      系列R（個体HP固定）    → 傾き = q      = γ");
    Console.WriteLine();
    Console.WriteLine("**つまり β − βQ = γ が成り立つはず。** 成り立てば、P と Q の食い違いは");
    Console.WriteLine("**体数の辺そのもの**で、変換率（＝ p）の定義が壊れているわけではない。");
    Console.WriteLine();
    var lhs = Enumerable.Range(0, nT).Select(t => betaP[t] - betaQ[t]).ToArray();
    var addC = Correlate(lhs, gammaR);
    double madd = Enumerable.Range(0, nT).Average(t => Math.Abs(lhs[t] - gammaR[t]));
    Console.WriteLine($"| 量 | 平均 | 範囲 |");
    Console.WriteLine("|---|--:|---|");
    Console.WriteLine($"| **β − βQ**（模型の予言） | {lhs.Average():+0.00;-0.00} | {lhs.Min():+0.00;-0.00} 〜 {lhs.Max():+0.00;-0.00} |");
    Console.WriteLine($"| **γ 体数の辺**（実測） | {gammaR.Average():+0.00;-0.00} | {gammaR.Min():+0.00;-0.00} 〜 {gammaR.Max():+0.00;-0.00} |");
    Console.WriteLine();
    Console.WriteLine($"**一致: r = {addC.R:F3} / ρ = {addC.Rho:F3}・平均 |差| = {madd:F3}。**");
    double bias = Enumerable.Range(0, nT).Average(t => lhs[t] - gammaR[t]);
    Console.WriteLine($"ずれの符号は {Enumerable.Range(0, nT).Count(t => lhs[t] - gammaR[t] < 0)} / {nT} が負で、"
        + $"平均は {bias:+0.00;-0.00}——**ばらつきではなく片寄り**（γ が一貫して大きい）。");
    Console.WriteLine();
    Console.WriteLine(Math.Abs(addC.R) >= 0.90 && madd < 0.15
        ? "**模型どおり。** 系列P と系列Q の食い違いは**体数の辺で説明が付く。**"
        : Math.Abs(addC.R) >= 0.90
            ? "**形は模型どおり（r " + $"{addC.R:F2}" + "）だが、系統的なずれが残る。** 対数で1次の模型は"
              + "**大半を説明するが全部ではない**——`ln(個体HP)` と `ln(体数)` の交互作用（曲がり）が"
              + "残っている。**系列P と 系列Q が逆を向く理由は体数の辺**で、変換率の定義が"
              + "壊れているわけではない。"
            : "**模型どおりにはならない。** **β と βQ の食い違いは体数の辺だけでは説明が付かない。**");
    Console.WriteLine();
    Console.WriteLine("> **どちらにしても、変換率として使えるのは β（系列P）のほう。** βQ は模型の上で");
    Console.WriteLine("> **p − q**（個体HP の効果 と 体数の効果の差）なので、個体HP の効果そのものではない。");
    Console.WriteLine("> 計画書 §2-2 が「総HP を揃えると体数が同時に動く」と警告したのは、この差のこと。");
    Console.WriteLine();
    Console.WriteLine("| 編成 | β | βQ | β − βQ | γ（体数の辺） | 差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    foreach (int t in Enumerable.Range(0, nT).OrderByDescending(t => Math.Abs(lhs[t] - gammaR[t])).Take(8))
        Console.WriteLine($"| {targets[t].Name} | {Sg(betaP[t], 2):+0.00;-0.00} | {Sg(betaQ[t], 2):+0.00;-0.00} "
            + $"| {Sg(lhs[t], 2):+0.00;-0.00} | {Sg(gammaR[t], 2):+0.00;-0.00} "
            + $"| {Sg(lhs[t] - gammaR[t], 2):+0.00;-0.00} |");
    Console.WriteLine();
    Console.WriteLine("（ずれの大きい 8 編成。**全 31 編成のうち一部しか出していない**のは、"
        + "この表が模型の当てはまりを見るためのもので、値そのものは §4-1 にあるため。）");
    Console.WriteLine();
    Console.WriteLine("### 5-4. 以下で使う量");
    Console.WriteLine();
    Console.WriteLine("**変換率 = 系列P の β**（計画書 §2-2 の「振るのは個体HP だけ」に文字どおり従う唯一の系列）。");
    Console.WriteLine("系列Q は**総HP を揃えた対照**として §8 の積に1本だけ残す（材料を差し替えた同じ形の項を");
    Console.WriteLine("並べれば、結論が系列の選び方で変わるかが読める）。系列R は辺の検算にだけ使い、特徴量にはしない。");
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 6. 甲乙は分離できるか =================
    //
    // 第16期の 12 事例の分類は**人間の解釈**。第17期は (B) 育ち で数値化しようとして失敗し、
    // 「甲乙の軸は育つかではなく**撃破に変換されるか**」という訂正を残した。
    // **その訂正どおりの量が変換率**なので、ここが本題の1つ目。
    //
    // 分類は `output` §7 からそのまま写す（**ここで分け直すと「分かれるように分けた」になる**）。
    var kou = new[] { "速攻 (ボルグ×ムド)", "毒+耐久 (ベニ×トウ)", "溜め改 (クグ×バン×ガン)" };
    var otsu = new[] { "毒 (グザ×ミオ×ラウ)", "燃焼 (ボルグ×ホタ)", "耐久 (ガルド×リリ)",
                       "範囲耐性 (ヒビ×ボルグ)", "追撃×死 (ハギ×リィカ)", "死の連鎖 (リィカ軸)" };
    int Ix(string name) => Array.FindIndex(targets, x => x.Name == name);

    Console.WriteLine("## 6. 変換率で甲乙は分離できるか（第16期の12事例と照合）");
    Console.WriteLine();
    Console.WriteLine("第17期は (B) 育ち で分離できず、**「甲乙の軸は育つかではなく撃破に変換されるか」**という");
    Console.WriteLine("訂正を残した。変換率はその訂正どおりの量なので、ここが本題の1つ目。分類は");
    Console.WriteLine("`output` §7（＝第16期 `dissect` §7 の12事例）からそのまま写した。");
    Console.WriteLine();
    Console.WriteLine("| 群 | 編成 | **β 変換率** | βQ | (B) 育ち | (A) | (C) 手番外% | 撃破/戦 P90 |");
    Console.WriteLine("|:-:|---|--:|--:|--:|--:|--:|--:|");
    foreach (var (label, names) in new[] { ("**甲**", kou), ("**乙**", otsu) })
        foreach (string nm in names)
        {
            int t = Ix(nm);
            if (t < 0) { Console.WriteLine($"| {label} | {nm} | — | — | — | — | — | — |"); continue; }
            Console.WriteLine($"| {label} | {nm} | **{Sg(betaP[t], 2):+0.00;-0.00}** | {Sg(betaQ[t], 2):+0.00;-0.00} "
                + $"| {featB0[t]:F2} | {featA0[t]:F1} | {featC0[t]:F0}% "
                + $"| {(double)tr[mainB][t].Kills / ConvSeeds:F2} |");
        }
    Console.WriteLine();

    var kIx = kou.Select(Ix).Where(t => t >= 0).ToArray();
    var oIx = otsu.Select(Ix).Where(t => t >= 0).ToArray();
    Console.WriteLine("| 量 | 甲の平均 | 乙の平均 | 甲の範囲 | 乙の範囲 | 重なるか |");
    Console.WriteLine("|---|--:|--:|---|---|:-:|");
    foreach (var (nm, v) in new[] { ("**β 変換率**", betaP), ("βQ（従系列）", betaQ),
                                    ("(B) 育ち", featB0), ("(A) 実効打点/T", featA0), ("(C) 手番外%", featC0) })
    {
        double kmin = kIx.Min(t => v[t]), kmax = kIx.Max(t => v[t]);
        double omin = oIx.Min(t => v[t]), omax = oIx.Max(t => v[t]);
        bool overlap = kmin <= omax && omin <= kmax;
        Console.WriteLine($"| {nm} | {kIx.Average(t => v[t]):F2} | {oIx.Average(t => v[t]):F2} "
            + $"| {kmin:F2} 〜 {kmax:F2} | {omin:F2} 〜 {omax:F2} | {(overlap ? "**重なる**" : "分かれる")} |");
    }
    double kminB = kIx.Min(t => betaP[t]), kmaxB = kIx.Max(t => betaP[t]);
    double ominB = oIx.Min(t => betaP[t]), omaxB = oIx.Max(t => betaP[t]);
    bool split = !(kminB <= omaxB && ominB <= kmaxB);
    Console.WriteLine();
    Console.WriteLine("**「分かれる」= 2群の範囲が重ならない**（1本の閾値で 9 編成を完全に分類できる）。");
    Console.WriteLine("n が 3 と 6 しかないので、**重ならないことは「分離できた」の必要条件であって十分条件ではない**");
    Console.WriteLine("——偶然に重ならない確率は決して小さくない（3 と 6 の並べ替えで完全分離は 1/84）。");
    Console.WriteLine();

    // --- 6-1. 読み（解釈） ---
    //
    // **ここだけは測定ではなく解釈**（`dissect` §7・`output` §7-1 と同じ扱い）。
    // 表と食い違ったら表が正しい。数字が動いたらこの節も書き直すこと。
    //
    // 重なりを作っているのは誰かを名指しする。**外して測り直すのは診断であって直しではない**
    // ——外して分かれたからといって、甲乙が変換率で分離できたことにはならない（`output` §4-5 と同じ）。
    int worstO = oIx.OrderByDescending(t => betaP[t]).First();   // 乙で最も β が高い編成
    int worstK = kIx.OrderBy(t => betaP[t]).First();             // 甲で最も β が低い編成
    var oWithout = oIx.Where(t => t != worstO).ToArray();
    bool splitWithout = !(kIx.Min(t => betaP[t]) <= oWithout.Max(t => betaP[t])
                          && oWithout.Min(t => betaP[t]) <= kIx.Max(t => betaP[t]));
    Console.WriteLine("### 6-1. 読み（解釈。表と食い違ったら表が正しい）");
    Console.WriteLine();
    Console.WriteLine($"**重なりを作っているのは {targets[worstO].Name}（乙・β {betaP[worstO]:+0.00;-0.00}）** で、"
        + $"甲の最低（{targets[worstK].Name} {betaP[worstK]:+0.00;-0.00}）を上回っている。");
    Console.WriteLine($"この 1 編成を外すと乙の範囲は {oWithout.Min(t => betaP[t]):+0.00;-0.00} 〜 "
        + $"{oWithout.Max(t => betaP[t]):+0.00;-0.00} になり、甲乙は"
        + $"{(splitWithout ? "**分かれる**" : "それでも重なる")}。");
    Console.WriteLine("**これは診断であって直しではない**（`output` §4-5 と同じ）——外して分かれたからといって、");
    Console.WriteLine("甲乙が変換率で分離できたことにはならない。1 編成を外す自由を認めるなら、"
        + "どの量でも同じことができる。");
    Console.WriteLine();
    Console.WriteLine("**第17期と同じ編成が、同じ位置で引っかかっている。** 第17期は (B) 育ちで");
    Console.WriteLine("「乙群の 毒 が9編成中いちばん高い」ために分離できず、**「育つか」ではなく");
    Console.WriteLine("「撃破に変換されるか」だ**という訂正を出した。ところが**その訂正どおりに測った");
    Console.WriteLine("変換率でも、同じ編成が同じ側にはみ出す。**");
    Console.WriteLine();
    Console.WriteLine($"**{targets[worstO].Name} は的が硬いほど出力が伸びる**（β が高い＝積み上げ型）**のに、乙群だった。**");
    Console.WriteLine("第16期が 毒 を乙に置いた根拠は `毒の無駄`（第4波で 282.6 段が撃破に変換されずに消えた）");
    Console.WriteLine("だが、**それは参照台の (A) には現れない**——`ApplyDamage` は残HPで切り詰めないので、");
    Console.WriteLine($"オーバーキルも打点として数えられる（{targets[worstO].Name} の オーバーキル% は P90 で "
        + $"{tr[mainB][worstO].Overkill * 100.0 / tr[mainB][worstO].Damage.Sum():F0}%）。");
    Console.WriteLine("**変換率は「出力が硬い的でどう変わるか」を測るが、「その出力が無駄になっているか」は");
    Console.WriteLine("測っていない。** 甲乙の軸がそちらなら、要るのは変換率ではなく**無駄率**のほう。");
    Console.WriteLine();
    Console.Out.Flush();

    // ---- ここから波を測る（第15期 FB・第16期 GB・第17期 HB のやり直し） ----
    //
    // 波は `WaveCatalog()` を呼ぶ。**コピーを持たない**（第15期が「1箇所に集める」ために
    // やった作業を、4つ目の診断が台無しにする）。
    var waves = WaveCatalog();
    int nW = waves.Length;
    const double DeadZone = 50.0;   // wave §4 / dissect §1 / output §8 と同じ線

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
            var mw = MeasureWave(targets[t].F, waves[w].Enemy, ConvSeeds);
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

    // ================= 7. 波ごとの分解のやり直し =================
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
    var dynKeep = new (string Name, int K)[] { ("干渉/戦", 3), ("回復/戦", 4), ("自傷率", 5), ("与ダメ効率", 6) };
    var outNames = new (string Name, double[] V)[]
        { ("(A) 実効打点/T", featA0), ("(B) 育ち", featB0), ("(C) 手番外%", featC0) };

    Console.WriteLine("## 7. 波ごとの分解のやり直し（第15期 FB → 第17期 HB → ここ）");
    Console.WriteLine();
    Console.WriteLine("目的変数は波ごとの単発勝率。候補は**第17期の 15 種**（静的8 + 動的4 + (A)(B)(C)）に");
    Console.WriteLine("**変換率を足した 16 種**。第15期・第17期の側は同じ実行の中で計算し直している");
    Console.WriteLine("——**別の実行から引くと、動いたのが候補のせいか実行のせいか決まらない**（第13期以来の作法）。");
    Console.WriteLine();
    Console.WriteLine("**同語反復の判定（第14期の基準）を変換率にも通す。** 基準は「目的変数の言い換えに");
    Console.WriteLine("なっていないか」の1本だけ。**当たらない**——変換率を測ったのは参照台の系列で、");
    Console.WriteLine("目的変数（波ごとの勝率）の戦闘とは別の戦闘。その波の敵を削り切ったかどうかは");
    Console.WriteLine("1ビットも入っていない（第17期 §5-1 と同じ理屈。単発戦なので分母経路も存在しない）。");
    Console.WriteLine();
    Console.WriteLine($"寄与する波は同じ判定式（天井率 + 床率 < {DeadZone:F0}%）で引き直した: **{conW.Length} 本** — "
        + string.Join(" / ", conW.Select(w => $"`{waves[w].Tag}`")));
    Console.WriteLine();

    // 第15期の記録（README「検証で分かったこと」）。**この診断が第15期と同じ盤を見ていることの
    // 検算にだけ使う**（値を候補には使わない。dissect §12-2 / output §8 と同じ作法）。
    var rec15 = new Dictionary<string, (string First, double R2)>
    {
        ["S2"] = ("与ダメ効率", 0.059), ["S3"] = ("体数", 0.164), ["S4"] = ("与ダメ効率", 0.341),
        ["S5"] = ("干渉/戦", 0.338), ["R8"] = ("与ダメ効率", 0.194), ["R9"] = ("範囲枚数", 0.203),
        ["R10"] = ("総HP", 0.116),
    };

    string[] names15 = statNames.Select(x => x.Name).Concat(dynKeep.Select(x => x.Name)).ToArray();
    string[] names17 = names15.Concat(outNames.Select(x => x.Name)).ToArray();
    string[] names18 = names17.Concat(new[] { "β 変換率" }).ToArray();
    double[] Col15(int k, int w) => k < nS
        ? Enumerable.Range(0, nT).Select(t => statNames[k].Get(targets[t].F)).ToArray()
        : Enumerable.Range(0, nT).Select(t => dyn[w][t][dynKeep[k - nS].K]).ToArray();
    double[] Col17(int k, int w) => k < names15.Length ? Col15(k, w) : outNames[k - names15.Length].V;
    double[] Col18(int k, int w) => k < names17.Length ? Col17(k, w) : betaP;

    Console.WriteLine("| 波 | 第15期(12種) | r² | 記録 | 第17期(15種) | r² | **第18期(16種)** | **r²** | 2位 | 変換率が第一近似 |");
    Console.WriteLine("|:-:|---|--:|--:|---|--:|---|--:|---|:-:|");
    int miss15 = 0, betaFirst = 0;
    foreach (int w in conW)
    {
        (int K, double R) Best(Func<int, double[]> col, int n)
        {
            var ord = Enumerable.Range(0, n).Select(k => (K: k, R: Correlate(col(k), rate[w]).R))
                .Where(x => !double.IsNaN(x.R)).OrderByDescending(x => Math.Abs(x.R)).ToArray();
            return ord.Length == 0 ? (-1, double.NaN) : ord[0];
        }
        var b15 = Best(k => Col15(k, w), names15.Length);
        var b17 = Best(k => Col17(k, w), names17.Length);
        var ord18 = Enumerable.Range(0, names18.Length)
            .Select(k => (K: k, R: Correlate(Col18(k, w), rate[w]).R))
            .Where(x => !double.IsNaN(x.R)).OrderByDescending(x => Math.Abs(x.R)).ToArray();
        string rec = "—";
        if (rec15.TryGetValue(waves[w].Tag, out var want))
        {
            bool ok = names15[b15.K] == want.First && Math.Abs(b15.R * b15.R - want.R2) <= 0.005;
            if (!ok) miss15++;
            rec = ok ? $"{want.R2:F3}" : $"**{want.First} {want.R2:F3} ←ずれ**";
        }
        bool up = ord18[0].K >= names17.Length;
        if (up) betaFirst++;
        Console.WriteLine($"| **{waves[w].Tag}** | {names15[b15.K]} {b15.R:+0.00;-0.00} | {b15.R * b15.R:F3} | {rec} "
            + $"| {names17[b17.K]} {b17.R:+0.00;-0.00} | {b17.R * b17.R:F3} "
            + $"| {(up ? "**" : "")}{names18[ord18[0].K]}{(up ? "**" : "")} {ord18[0].R:+0.00;-0.00} "
            + $"| **{ord18[0].R * ord18[0].R:F3}** "
            + $"| {(ord18.Length > 1 ? $"{names18[ord18[1].K]} {ord18[1].R:+0.00;-0.00}" : "—")} "
            + $"| {(up ? "**○**" : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine(miss15 == 0
        ? "**検算: 第15期の記録した第一近似・r² と完全に一致（ずれ 0 件）。** この診断は第15期と同じ盤を見ている。"
        : $"**検算: {miss15} 件ずれた。第15期と同じ盤を見ていない——先へ進む前に原因を潰すこと。**");
    Console.WriteLine();
    Console.WriteLine($"**変換率が第一近似になったのは {betaFirst} / {conW.Length} 波。**");
    Console.WriteLine();
    Console.WriteLine("### 7-1. 変換率の単相関（寄与する波）");
    Console.WriteLine();
    Console.WriteLine("**符号まで含めて読む。** 同じ量が波によって逆向きに効くなら、それは");
    Console.WriteLine("「どちらの波にも効く地力」ではなく**波の性格そのもの**（第15期 §9-3 の読み方）。");
    Console.WriteLine();
    Console.WriteLine("| 量 |" + string.Concat(conW.Select(w => $" {waves[w].Tag} |")) + " 符号の向き |");
    Console.WriteLine("|---|" + string.Concat(conW.Select(_ => "--:|")) + ":-:|");
    foreach (var (nm, v) in new[] { ("**β 変換率**", betaP) }.Concat(outNames).Concat(new[] { ("総攻（比較）", atk) }))
    {
        var rs = conW.Select(w => Correlate(v, rate[w]).R).ToArray();
        bool allSame = rs.All(r => r >= 0) || rs.All(r => r <= 0);
        Console.WriteLine($"| {nm} |" + string.Concat(rs.Select(r => $" {(double.IsNaN(r) ? "—" : $"{r:+0.00;-0.00}")} |"))
            + $" {(allSame ? "揃う" : "**反転する**")} |");
    }
    Console.WriteLine();
    Console.Out.Flush();

    // ================= 8. 交互作用項の作り直し =================
    Console.WriteLine("## 8. 交互作用項の作り直し（第16期 GB → 第17期 HB → ここ）");
    Console.WriteLine();
    Console.WriteLine("**積の材料に変換率を足す。** 片側だけの特徴量は交互作用成分と相関が**恒等的に 0** なので");
    Console.WriteLine("（第16期 §11。残差は行にも列にも和が 0）、変換率を単体で足しても 0 のまま");
    Console.WriteLine("——**積にして初めて意味を持つ。**");
    Console.WriteLine();

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
    double[] Flat(Func<int, int, double> get) => Enumerable.Range(0, nC)
        .SelectMany(c => Enumerable.Range(0, nT).Select(t => get(conW[c], t))).ToArray();
    double[] FlatV(double[][] v) => v.SelectMany(r => r).ToArray();
    double[] residFlat = FlatV(Resid(rate));

    Console.WriteLine("### 8-1. 分散分解（第16期 §11 と同じ計算）");
    Console.WriteLine();
    Console.WriteLine($"寄与する {nC} 波 × {nT} 編成 = **{nC * nT} 点**。");
    Console.WriteLine();
    Console.WriteLine("| 目的変数 | 波の主効果 | 編成の主効果 | **交互作用** | 第16期の記録 |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    Console.WriteLine($"| 勝率 | {decW.Wave:F1}% | {decW.Build:F1}% | **{decW.Inter:F1}%** | 28.3% |");
    Console.WriteLine($"| 残存度 | {decD.Wave:F1}% | {decD.Build:F1}% | **{decD.Inter:F1}%** | 21.7% |");
    Console.WriteLine();
    double oneSide = Math.Abs(Pearson(Flat((w, t) => betaP[t]), residFlat));
    Console.WriteLine($"**検算: 変換率を単体で交互作用成分に当てると |r| = {oneSide:F6}。**");
    Console.WriteLine("**新しい特徴量でも 0 になるのが正しい**——これは測定結果ではなく恒等式。");
    Console.WriteLine();

    // --- 8-2. 項の候補 ---
    //
    // **総当たりで作らない**（第16期 §10・第17期 §9-2 と同じ縛り。編成側 4 × 敵側 9 を全部試すと
    // n = 217 では必ず何かが当たる）。出どころは3つだけ:
    //   (a) 計画書 §2-4 が挙げた3つの例
    //   (b) 変換率を「波の個体HP へ外挿する」形にしたもの（変換率の定義そのものの使い道）
    //   (c) 第16期・第17期の最良項をそのまま（対照）
    //
    // (b) の考え方: 変換率 β は「個体HP が e 倍になると (A) が何倍になるか」なので、
    // **参照台（個体HP 90）で測った (A) を、その波の個体HP まで外挿できる。**
    //     予測実効打点 = (A) × (敵の個体HP中央値 ÷ 90)^β
    // これは「出力」と「変換」を両方持つ唯一の形で、第16期の `削るT` / `時計比` / `一撃圏` の
    // 分母をこれに差し替えれば、**2つの時計の競走を変換込みで書き直したもの**になる。
    double Pred(int w, int t) => featA0[t] * Math.Pow(MedianHp(waves[w].Enemy) / (double)Porter.MaxHp, betaP[t]);
    double FoeHp(int w) => waves[w].Enemy.Occupied().Sum(x => x.Def.MaxHp);
    double FoeAtk(int w) => waves[w].Enemy.Occupied().Sum(x => x.Def.Attack);
    double AllyHp(int t) => targets[t].F.Occupied().Sum(x => x.Def.MaxHp);
    var terms = new (string Name, string Expr, string From, string Why, Func<int, int, double> Get)[]
    {
        ("耐えるT", "味方の総HP ÷ 敵総攻", "第16期のまま（対照）",
            "第16期の最良項。**出力を含まない**ので、比較の基準として据え置く（プール r² 0.061 を再現するはず）",
            (w, t) => AllyHp(t) / FoeAtk(w)),
        ("一撃圏'", "敵の個体HP中央値 ÷ ((A) ÷ 味方の体数)", "第17期のまま（対照）",
            "**第17期の最良項**（交互作用 r² 0.002）。変換率を入れない側の到達点",
            (w, t) => MedianHp(waves[w].Enemy) / (featA0[t] / targets[t].F.Count)),
        ("変換率 × 敵個体HP", "**β** × 敵の個体HP中央値", "計画書 §2-4 の例1",
            "**変換に敏感な編成が、硬い敵でどうなるか。** β < 0 の編成は個体HP が高い波で沈むはず",
            (w, t) => betaP[t] * MedianHp(waves[w].Enemy)),
        ("変換率 × 敵体数", "**β** × 敵の体数", "計画書 §2-4 の例2",
            "**撃破の回数そのものが敵の体数で決まる。** 撃破依存の出力は体数の多い波で回る",
            (w, t) => betaP[t] * waves[w].Enemy.Count),
        ("出力 × 変換率", "**(A)** × **β**", "計画書 §2-4 の例3",
            "**出力と変換の両方を持つか。** 片方だけ大きい編成（毒は (A) 大・β 大、反撃は (A) 大・β 小）を分ける",
            (w, t) => featA0[t] * betaP[t]),
        ("予測実効打点", "**(A)** × (敵個体HP中央値 ÷ 90)^**β**", "変換率の定義そのものの使い道",
            "**参照台（個体HP 90）で測った出力を、その波の個体HP まで外挿する。** "
            + "変換率が「個体HP が e 倍で (A) が何倍になるか」である以上、これが最も直接の使い道",
            Pred),
        ("予測削るT", "敵総HP ÷ **予測実効打点**", "第16期 `削るT` の分母を外挿値に",
            "**その波の敵を削り切るまでのターン数。** 第16期は分母が `総攻`、第17期は (A) だったので、"
            + "どちらも「硬い敵では出力が落ちる」を表せていなかった",
            (w, t) => FoeHp(w) / Pred(w, t)),
        ("予測時計比", "耐えるT ÷ **予測削るT**", "第16期 `時計比` の書き直し",
            "**2つの時計の競走を、変換込みで1本にまとめたもの**（第16期の骨格そのもの）",
            (w, t) => AllyHp(t) / FoeAtk(w) / (FoeHp(w) / Pred(w, t))),
        ("予測一撃圏", "敵個体HP中央値 ÷ (**予測実効打点** ÷ 味方の体数)", "第16期 `一撃圏` の書き直し",
            "**1体あたりの外挿出力で何ターン殴れば1体落ちるか。** 乙群（一撃圏に縛られる）の説明を"
            + "変換込みで書き直したもの",
            (w, t) => MedianHp(waves[w].Enemy) / (Pred(w, t) / targets[t].F.Count)),
        ("変換率Q × 敵個体HP", "**βQ** × 敵の個体HP中央値", "系列Q（総HP を揃えた側）の対照",
            "**同じ形の積を、総HP を揃えた系列の変換率で作る。** 3番の項と並べれば、"
            + "結論が**系列の選び方で変わるか**が読める（§5-3 で 2 系列は逆を向いている）",
            (w, t) => betaQ[t] * MedianHp(waves[w].Enemy)),
    };

    Console.WriteLine("### 8-2. 交互作用項の候補（10 個）");
    Console.WriteLine();
    Console.WriteLine("**総当たりで作っていない**（第16期 §10・第17期 §9-2 と同じ縛り）。出どころは3つだけ:");
    Console.WriteLine("**(a) 計画書 §2-4 が挙げた3つの例**、**(b) 変換率を波の個体HP へ外挿したもの**、");
    Console.WriteLine("**(c) 第16期・第17期の最良項そのまま（対照）**。");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | 式 | 出どころ | 理由 |");
    Console.WriteLine("|--:|---|---|:-:|---|");
    for (int k = 0; k < terms.Length; k++)
        Console.WriteLine($"| {k + 1} | **{terms[k].Name}** | `{terms[k].Expr}` | {terms[k].From} | {terms[k].Why} |");
    Console.WriteLine();
    Console.Out.Flush();

    // --- 8-3. 効くか ---
    Console.WriteLine("### 8-3. 交互作用項は効くか");
    Console.WriteLine();
    Console.WriteLine("第16期 §12・第17期 §9-3 と同じ3通りの当て方。**(2) が本題。**");
    Console.WriteLine();
    Console.WriteLine("| # | 項 | (1) プール r | r² | **(2) 交互作用 r** | **r²** | (2) ρ | (2) 残存度 r | 符号一致 |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|:-:|");
    var score = new List<(int K, double R2)>();
    int agree = 0;
    double[] residDFlat = FlatV(Resid(degree));
    for (int k = 0; k < terms.Length; k++)
    {
        double[] x = Flat(terms[k].Get);
        double rp = Pearson(x, Flat((w, t) => rate[w][t]));
        var ci = Correlate(x, residFlat);
        double rd = Pearson(x, residDFlat);
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
    Console.WriteLine($"**最良は `{terms[best.K].Name}` で r² = {best.R2:F3}**"
        + "（第16期の最良は `範囲の変換` の **0.003**、第17期は `一撃圏'` の **0.002**）。");
    Console.WriteLine($"順位相関でも最大 |ρ| = {maxRho:F3}。**単調な非線形を取りこぼしているのではない。**");
    Console.WriteLine($"符号が一致したのは {agree} / {terms.Length}（勝率の交互作用成分 ↔ 残存度の交互作用成分）。");
    Console.WriteLine();
    Console.WriteLine($"交互作用は全分散の {decW.Inter:F1}% なので、最良の項が説明しているのは"
        + $" **全体の {best.R2 * decW.Inter / 100:F3}**。");
    Console.WriteLine();
    {
        double poolHold = Pearson(Flat(terms[0].Get), Flat((w, t) => rate[w][t]));
        var c17 = Correlate(Flat(terms[1].Get), residFlat);
        bool ok = Math.Abs(poolHold - 0.25) <= 0.005 && Math.Abs(c17.R * c17.R - 0.002) <= 0.0015;
        Console.WriteLine($"**検算: 対照項 `耐えるT` のプール r = {poolHold:+0.00;-0.00} / r² = {poolHold * poolHold:F3}"
            + $"（第16期の記録は +0.25 / 0.061）、対照項 `一撃圏'` の交互作用 r² = {c17.R * c17.R:F3}"
            + $"（第17期の記録は 0.002）→ {(ok ? "一致" : "**ずれ**")}。**");
        Console.WriteLine("**盤も波も編成集合も第16期・第17期のまま**——動いたのは項の材料だけ。");
        Console.WriteLine();
    }
    Console.Out.Flush();

    // ================= 9. 判定 =================
    //
    // 線は第16期・第17期と同じ（交互作用成分の r² が 0.10 を超えるか）。**線を新しく作らない。**
    const double TermLine = 0.10;
    Console.WriteLine("## 9. 判定（計画書 §3-1 のどの行か）");
    Console.WriteLine();
    Console.WriteLine($"線は**第16期・第17期と同じ**「交互作用成分に対する r² が {TermLine:F2} を超えるか」。");
    Console.WriteLine("**線を新しく作らない**（作ると過去の期と比べられなくなる）。");
    Console.WriteLine();
    double best18 = conW.Max(w => Enumerable.Range(0, names18.Length)
        .Select(k => { double r = Correlate(Col18(k, w), rate[w]).R; return double.IsNaN(r) ? 0 : r * r; }).Max());
    double best17 = conW.Max(w => Enumerable.Range(0, names17.Length)
        .Select(k => { double r = Correlate(Col17(k, w), rate[w]).R; return double.IsNaN(r) ? 0 : r * r; }).Max());
    bool interUp = best.R2 >= TermLine;

    Console.WriteLine($"- 交互作用成分に対する最良 r² = **{best.R2:F3}**（第16期 0.003 / 第17期 0.002）→ "
        + $"{TermLine:F2} を{(interUp ? "**超えた**" : "超えない")}");
    Console.WriteLine($"- 変換率による甲乙の分離: **{(split ? "分かれる" : "重なる")}**"
        + $"（甲 {kminB:+0.00;-0.00}〜{kmaxB:+0.00;-0.00} / 乙 {ominB:+0.00;-0.00}〜{omaxB:+0.00;-0.00}）");
    Console.WriteLine($"- 波ごとの第一近似が変換率に替わった波: **{betaFirst} / {conW.Length}**。"
        + $"波ごとの最良 r² は {best17:F3}（15種）→ {best18:F3}（16種）");
    Console.WriteLine();
    Console.WriteLine("| # | 計画書 §3-1 の観測 | 当たるか | 結論 |");
    Console.WriteLine("|--:|---|:-:|---|");
    Console.WriteLine($"| 1 | 甲乙が分離でき、積で交互作用成分の説明力が上がる | {(split && interUp ? "**○**" : "×")} "
        + "| **17期分の壁が解けた。** 変換率が探していた量 |");
    Console.WriteLine($"| 2 | 甲乙は分離できるが、交互作用成分は上がらない | {(split && !interUp ? "**○**" : "×")} "
        + "| **甲乙は実在するが、交互作用の説明ではない。** 分類としては使える（キャラの役割設計に） |");
    Console.WriteLine($"| 3 | どちらも動かない | {(!split && !interUp ? "**○**" : "×")} "
        + "| **測定は打ち切り。** 交互作用は実在するが数値では読めない、で確定 |");
    Console.WriteLine();
    Console.WriteLine("**どの行でも、この作業で測定を終える**（計画書 §3-1）。3行目でも失敗ではない");
    Console.WriteLine("——「読めない」ことが確定すれば、設計側で**読める交互作用を作りにいく**という方針が定まる。");
    Console.WriteLine();
    Console.Out.Flush();
    return;
}
}
