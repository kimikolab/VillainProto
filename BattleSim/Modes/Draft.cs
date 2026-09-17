using BattleCore;
using static Common;

// =====================================================================================
// draft モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "draft")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 draft
// =====================================================================================

static class DraftDiag
{
// ============================================================================
// draft モード: ドラフト台（第69期）
//
// **測定台を1つ作るだけの期。** 新機構ゼロ・駒ゼロ・`docs/` 差分ゼロ。
// `CompareBuilds()` も `Stages` も `UnitCatalog` も1行も書き換えていない
// （素体と標本は診断のローカルに組む。`gradient` / `aim` / `scale` と同じ扱い）。
//
// 現在の 61 行は**すべて理想編成**で、コンボが確実に揃うことを前提にしている。
// 第68期の表D で、**ロスターの過半（27 / 51）が相方のいない盤面で一度も測られていない**
// ことが分かった。ここで答えるのは2つだけ:
//
//   問1: 相方が保証されない盤面で、各駒はどれだけ働くか
//   問2: 配置は何ptの価値があるか（無作為配置と規則配置の差がそのまま答え）
//
// **どちらも数えるだけ。設計判断をしない。採否も無い。**
//
//     dotnet run --project BattleSim -c Release 0 draft phase0  # N と M を決める（パイロット）
//     dotnet run --project BattleSim -c Release 0 draft         # 主表（表A〜F）・A 帯
//     dotnet run --project BattleSim -c Release 0 draft alt     # 同じ標本を B 帯（seed 200..）で
//     dotnet run --project BattleSim -c Release 0 draft check   # 陰性対照（Q6）
public static void Run(string[] args, int stageIndex)
{
    string dfArg = args.Length > 2 ? args[2] : "";
    IReadOnlyList<EnemyCatalog.Stage> dfStages = EnemyCatalog.Stages;
    int dfW = dfStages.Count;
    var dfSw = System.Diagnostics.Stopwatch.StartNew();
    var dfRoster = UnitCatalog.All.ToArray();
    int dfRN = dfRoster.Length;                     // 51

    // ---- 測る前に固定した定数（§1〜§2。**結果を見てから動かしていない**） ----------------
    //
    // 標本の乱数と測定の乱数を分ける（指示書 §1-1）。標本抽選は `DfSampleSeed + 標本番号` の
    // `Random` だけを使い、**戦闘 seed には一切触れない**。戦闘 seed の帯は 0.. と 200.. の2本で、
    // 標本抽選の系列（1000000..）とは重ならない。**帯を変えても標本は1体も変わらない**
    // （`draft check` の (1) がそれを直に確かめる）。
    const int DfSampleSeed = 1_000_000;
    const int DfBandA = 0, DfBandB = 200;

    // M（1標本あたりの戦闘 seed 数）は **8 に固定**。測る前に決めた値で、パイロットで動かさない
    // ——1標本の勝率が 4波 × 8 seed = 32 戦で決まる（刻み 3.1pt）。
    const int DfM = 8;

    // N（標本数）は phase0 のパイロットが §2-1 の式で出した値。
    //
    //     半幅 = 1.96 × s_u / √K_u ≤ 1.5   （s_u = 駒 u の寄与の標本間標準偏差）
    //     K_u  = N × 5 / 51                （51体から無作為5体なので 1駒あたりの在席標本数）
    //     ⇒ N ≥ (51 / 5) × (1.96 × s_max / 1.5)²      s_max = 対象駒の中の最大の s_u
    //
    // **s_max（最悪の駒）で決める**のは、Q2 が「対象駒すべてで ±1.5pt 未満」を要求するため。
    const int DfN = 11000;
    const int DfPilotN = 800;
    const double DfCiTarget = 1.5, DfZ = 1.96;

    // ---- 対象駒（測る前に固定。§2-1 の規則をそのまま当てた結果） --------------------------
    //
    //   (1) 第68期の表D で「相方なし行が 0」だった 27 駒 —— 単独で一度も測られていない駒。この期の主題
    //   (2) ＋ 移動軸の3枚（ヨミ・バサ・シオ）—— **3枚とも (1) に既に入っている**ので新規は 0
    //   (3) ＋ 対照として理想編成で寄与が最大級の3枚（第68期の表D の「相方あり行の寄与」上位で、
    //       かつ (1) に入っていないもの）= ゾト +68.8 / ウロ +47.8 / ネル +41.5
    //
    // 合計 **30 体**（指示書の「33 前後」の内側。(2) が (1) と完全に重なったぶんだけ少ない）。
    UnitDef[] dfTargets =
    {
        // (1) 相方なし行が 0 だった 27 駒（第68期 §10-1 の並び順）
        UnitCatalog.Mudo, UnitCatalog.Sid,  UnitCatalog.Mio,  UnitCatalog.Rau,  UnitCatalog.Beni,
        UnitCatalog.Vio,  UnitCatalog.Yomi, UnitCatalog.Basa, UnitCatalog.Shio, UnitCatalog.Utsu,
        UnitCatalog.Doha, UnitCatalog.Kubi, UnitCatalog.Sekki, UnitCatalog.Hota, UnitCatalog.Hibi,
        UnitCatalog.Shiga, UnitCatalog.Zan, UnitCatalog.Kiri, UnitCatalog.Egu,  UnitCatalog.Nata,
        UnitCatalog.Hari, UnitCatalog.Hane, UnitCatalog.Uke,  UnitCatalog.Wata, UnitCatalog.Kari,
        UnitCatalog.Tome, UnitCatalog.Hiyo,
        // (3) 対照
        UnitCatalog.Zoto, UnitCatalog.Uro,  UnitCatalog.Nel,
    };
    int dfT = dfTargets.Length;
    var dfTargetIdx = new Dictionary<string, int>();
    for (int i = 0; i < dfT; i++) dfTargetIdx[dfTargets[i].Id] = i;

    // 第68期 表D の published 値（駒ごとの平均寄与）。**器具の検算にしか使わない**
    // ——27 駒は「相方なし行 0」なので相方あり行の平均がそのまま全行の平均で、
    // 対照3枚だけ2列を行数で重み付けした（ゾト (2×68.8+7×30.1)/9 / ウロ (47.8+43.6)/2 /
    // ネル (3×41.5+3×12.8)/6）。表の丸めのぶん ±0.1pt の誤差が乗る。
    Dictionary<string, double> DfPublished68() => new()
    {
        [UnitCatalog.Mudo.Id] = 24.2, [UnitCatalog.Sid.Id] = 33.3, [UnitCatalog.Mio.Id] = 38.1,
        [UnitCatalog.Rau.Id] = 22.2, [UnitCatalog.Beni.Id] = 24.1, [UnitCatalog.Vio.Id] = 51.9,
        [UnitCatalog.Yomi.Id] = 51.0, [UnitCatalog.Basa.Id] = 34.3, [UnitCatalog.Shio.Id] = 29.6,
        [UnitCatalog.Utsu.Id] = 57.5, [UnitCatalog.Doha.Id] = 34.5, [UnitCatalog.Kubi.Id] = 45.0,
        [UnitCatalog.Sekki.Id] = 21.1, [UnitCatalog.Hota.Id] = 36.3, [UnitCatalog.Hibi.Id] = 32.5,
        [UnitCatalog.Shiga.Id] = 27.4, [UnitCatalog.Zan.Id] = 33.4, [UnitCatalog.Kiri.Id] = 13.9,
        [UnitCatalog.Egu.Id] = 26.8, [UnitCatalog.Nata.Id] = 19.5, [UnitCatalog.Hari.Id] = 28.6,
        [UnitCatalog.Hane.Id] = 14.6, [UnitCatalog.Uke.Id] = 23.0, [UnitCatalog.Wata.Id] = 44.6,
        [UnitCatalog.Kari.Id] = 26.4, [UnitCatalog.Tome.Id] = 36.7, [UnitCatalog.Hiyo.Id] = 15.8,
        [UnitCatalog.Zoto.Id] = 38.7, [UnitCatalog.Uro.Id] = 45.7, [UnitCatalog.Nel.Id] = 27.2,
    };

    // 素体 = **数値・型・速さが1つも違わず、特性だけを持たない駒**（第47期からの作法）。
    //
    // **`Actions` は落とす。** ミオ・ヒヨ（対象駒）とカド・ノノ（対象外）は `Actions = [Skill]` を
    // 持つが、あれは「術を手番の行動そのものにする」ための機構側の配管で、
    // カタログのコメントも「挙動の差を『攻撃が出ない』だけに絞るため」と書いている。
    // 残すと**素体が1ターンも何もしない駒**になり、機構の帰属に体の値段が丸ごと混ざる。
    // **測る前に決めてコードに固定した1点。**
    var dfPlainDef = dfTargets.Select(d => new UnitDef
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    }).ToArray();

    // ---- 相方の定義（第68期 §1-4 / 表D と**同じ機械的定義**をそのまま写した） --------------
    //
    // その標本の中で、その駒と**同じキーの書き手または読み手**である他の駒。
    // 判定は grep で作った 特性 → キー の対応表を機械的に当てる。
    // **`Trait` に属性を足さない**（第48期 census の作法。判定の根拠が
    // 「誰かが属性を正しく付けたか」に化けて grep で検算できなくなる）。
    // **engine 側の窓口は駒に属さないので入らない。**
    // **第141期: キー表は `Everyone`（`All ∪ Retired`）で引く。** `dfTargets` の 30 体はこの期の対象として固定してあり
    // エグ（第139期）とハリ（第108期）を含む——`dfRoster`（＝`All`）で表を作ると `dfKeyOfUnit["egu"]` で落ちる。
    // **抽選の母集団 `dfRoster` は `All` のまま**（列挙は触らない）。
    var dfKeyOfUnit = UnitCatalog.Everyone.ToDictionary(u => u.Id, TraitKeyMap.KeysOf);

    // ---- 標本の作り方（**測る前に固定**） ------------------------------------------------
    //
    // 1. `UnitCatalog.All`（51体）から無作為に5体（重複なし・部分 Fisher-Yates）
    // 2. R 版 = その5体を無作為に5席へ（**同じ `Random` の続き**。戦闘 seed には触れない）
    // 3. H 版 = §1-3 の4行の規則で決定的に置く
    //
    // §1-3 が書いていない側を1点だけ埋めた（**測る前に**決めてコードに固定）:
    // **前列の2体のうちどちらが席0でどちらが席1か / 後列の2体のうち席3と席4** は
    // 指示書に無いので、**並べ替えに使ったキーの順（HP降順・攻撃力降順、同値は `Def.Id` の辞書順）で
    // 若いほうを 席0 / 席3 に置く**。規則は決定的なままで、強さは1行も足していない。
    (UnitDef[] Units, int[] RSlot, int[] HSlot) DfSampleOf(int i)
    {
        var rng = new Random(DfSampleSeed + i);
        var idx = new int[dfRN];
        for (int k = 0; k < dfRN; k++) idx[k] = k;
        for (int k = 0; k < 5; k++)
        {
            int j = k + rng.Next(dfRN - k);
            (idx[k], idx[j]) = (idx[j], idx[k]);
        }
        var units = new UnitDef[5];
        for (int k = 0; k < 5; k++) units[k] = dfRoster[idx[k]];

        // R: 席の無作為割り当て
        var seats = new[] { 0, 1, 2, 3, 4 };
        for (int k = 0; k < 5; k++)
        {
            int j = k + rng.Next(5 - k);
            (seats[k], seats[j]) = (seats[j], seats[k]);
        }

        // H: 規則配置
        var all5 = new[] { 0, 1, 2, 3, 4 };
        var front = all5.OrderByDescending(k => units[k].MaxHp)
                        .ThenBy(k => units[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        var rest = all5.Where(k => k != front[0] && k != front[1]).ToArray();
        var back = rest.OrderByDescending(k => units[k].Attack)
                       .ThenBy(k => units[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        int center = rest.Single(k => k != back[0] && k != back[1]);
        var rule = new int[5];
        rule[front[0]] = 0; rule[front[1]] = 1; rule[center] = 2; rule[back[0]] = 3; rule[back[1]] = 4;

        return (units, seats, rule);
    }

    // ---- 1帯ぶんの走査 -------------------------------------------------------------------
    //
    // **素体の差し替えは席を動かさない。** H 版の席は「元の5体」から決めた並びをそのまま使う
    // （素体の `Def.Id` は `..._plain` なので、差し替えてから規則を引き直すと同値の辞書順が
    // ずれて席が動きうる。それは配置の差であって機構の差ではない）。
    DfResult DfMeasure(int n, int m, int band, string label)
    {
        var full = new int[n][];
        var plain = new int[n][];
        var present = new int[n][];
        var members = new int[n][];
        int done = 0;
        Console.Error.Write($"{label}: ");
        Parallel.For(0, n, i =>
        {
            var (units, rslot, hslot) = DfSampleOf(i);
            var mem = new int[5];
            for (int k = 0; k < 5; k++) mem[k] = Array.IndexOf(dfRoster, units[k]);
            members[i] = mem;

            var pres = new List<int>();
            var pos = new List<int>();
            for (int k = 0; k < 5; k++)
                if (dfTargetIdx.TryGetValue(units[k].Id, out int ti)) { pres.Add(ti); pos.Add(k); }
            present[i] = pres.ToArray();

            var fw = new int[2 * dfW];
            var pw = new int[2 * pres.Count * dfW];
            for (int ver = 0; ver < 2; ver++)
            {
                int[] slot = ver == 0 ? rslot : hslot;
                var f = new Formation();
                for (int k = 0; k < 5; k++) f[slot[k]] = units[k];
                for (int w = 0; w < dfW; w++)
                    for (int seed = band; seed < band + m; seed++)
                        if (BattleEngine.Run(f, dfStages[w].Enemy, seed, verbose: false).PlayerWon)
                            fw[ver * dfW + w]++;

                for (int t = 0; t < pres.Count; t++)
                {
                    var g = new Formation();
                    for (int k = 0; k < 5; k++)
                        g[slot[k]] = k == pos[t] ? dfPlainDef[pres[t]] : units[k];
                    for (int w = 0; w < dfW; w++)
                        for (int seed = band; seed < band + m; seed++)
                            if (BattleEngine.Run(g, dfStages[w].Enemy, seed, verbose: false).PlayerWon)
                                pw[(ver * pres.Count + t) * dfW + w]++;
                }
            }
            full[i] = fw; plain[i] = pw;
            int d = Interlocked.Increment(ref done);
            if (d % 1000 == 0) Console.Error.Write(".");
        });
        Console.Error.WriteLine();
        return new DfResult
        {
            N = n, M = m, W = dfW, Band = band,
            Full = full, Plain = plain, Present = present, Members = members
        };
    }

    // 標本の勝率（**第一波は集計から除外**。波は全部回している）
    double DfRate(DfResult r, int i, int ver)
    {
        int s2 = 0;
        for (int w = 1; w < r.W; w++) s2 += r.Full[i][ver * r.W + w];
        return s2 * 100.0 / ((r.W - 1) * r.M);
    }
    double DfPlainRate(DfResult r, int i, int ver, int t)
    {
        int p = r.Present[i].Length;
        int s2 = 0;
        for (int w = 1; w < r.W; w++) s2 += r.Plain[i][(ver * p + t) * r.W + w];
        return s2 * 100.0 / ((r.W - 1) * r.M);
    }

    // **符号付きの表示は自前でやる。** `:+0.0;-0.0` の負セクションは、値が -0.0 に丸まると
    // `-+0.0` を吐く（第68期の `carry leak` が同じ穴を踏んで、微小値を 0 に潰して回避していた）。
    // ここは符号付きの列が多いので書式の側で直す。
    string DfP1(double x) => (x < -0.05 ? "-" : "+") + Math.Abs(x).ToString("F1");
    string DfP2(double x) => (x < -0.005 ? "-" : "+") + Math.Abs(x).ToString("F2");

    (double Mean, double Sd, int N) DfStats(IReadOnlyList<double> xs)
    {
        int n = xs.Count;
        if (n == 0) return (0, 0, 0);
        double m = xs.Average();
        if (n < 2) return (m, 0, n);
        double v = xs.Sum(x => (x - m) * (x - m)) / (n - 1);
        return (m, Math.Sqrt(v), n);
    }
    double DfCorr(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        int n = a.Count;
        if (n < 2) return double.NaN;
        double ma = a.Average(), mb = b.Average();
        double sa = 0, sb = 0, sab = 0;
        for (int i = 0; i < n; i++) { double x = a[i] - ma, y = b[i] - mb; sa += x * x; sb += y * y; sab += x * y; }
        return (sa <= 0 || sb <= 0) ? double.NaN : sab / Math.Sqrt(sa * sb);
    }

    // 駒ごとの寄与（版別）を取り出す
    List<double>[] DfContrib(DfResult r, int ver)
    {
        var acc = new List<double>[dfT];
        for (int t = 0; t < dfT; t++) acc[t] = new List<double>();
        for (int i = 0; i < r.N; i++)
        {
            double full = DfRate(r, i, ver);
            for (int t = 0; t < r.Present[i].Length; t++)
                acc[r.Present[i][t]].Add(full - DfPlainRate(r, i, ver, t));
        }
        return acc;
    }

    string DfHistRow(string ver, string label, double[] xs)
    {
        int n = xs.Length;
        int b0 = xs.Count(x => x == 0);
        int b1 = xs.Count(x => x > 0 && x < 5);
        int b2 = xs.Count(x => x >= 5 && x < 25);
        int b3 = xs.Count(x => x >= 25 && x < 50);
        int b4 = xs.Count(x => x >= 50 && x < 75);
        int b5 = xs.Count(x => x >= 75 && x <= 95);
        int b6 = xs.Count(x => x > 95 && x < 100);
        int b7 = xs.Count(x => x == 100);
        var st = DfStats(xs);
        return $"| {ver} | {label} | {st.Mean:F2}% | {st.Sd:F1} "
               + $"| {b0 * 100.0 / n:F1} | {b1 * 100.0 / n:F1} | {b2 * 100.0 / n:F1} | {b3 * 100.0 / n:F1} "
               + $"| {b4 * 100.0 / n:F1} | {b5 * 100.0 / n:F1} | {b6 * 100.0 / n:F1} | {b7 * 100.0 / n:F1} "
               + $"| **{(b2 + b3 + b4 + b5) * 100.0 / n:F1}%** |";
    }

    // ======================================================================================
    // phase0: N と M を決める（パイロット）
    // ======================================================================================
    if (dfArg == "phase0")
    {
        Console.WriteLine("# 第69期 Phase 0 —— N と M を決める（ドラフト台）");
        Console.WriteLine();
        Console.WriteLine("**この節の判定式・規則はすべて測る前に書いてコードに固定してある。**");
        Console.WriteLine("パイロットが返すのは `s_max`（対象駒の寄与の標本間標準偏差の最大値）1つだけで、");
        Console.WriteLine("それを下の式に入れて N を出す。**式も精度の条件（±1.5pt）も動かさない。**");
        Console.WriteLine();
        Console.WriteLine("## 0-1. 対象駒（30 体・測る前に固定）");
        Console.WriteLine();
        Console.WriteLine("| # | 規則 | 体数 | 中身 |");
        Console.WriteLine("|--:|---|--:|---|");
        Console.WriteLine("| (1) | 第68期 表D で**相方なし行が 0**（＝単独で一度も測っていない） | 27 | この期の主題 |");
        Console.WriteLine("| (2) | ＋ 移動軸の3枚（ヨミ・バサ・シオ） | **0** | **3枚とも (1) に既に入っている**ので新規は無い |");
        Console.WriteLine("| (3) | ＋ 対照: 理想編成で寄与が最大級の3枚（表D の相方あり行の寄与・(1) 以外） | 3 | ゾト +68.8 / ウロ +47.8 / ネル +41.5 |");
        Console.WriteLine("| | **合計** | **30** | 指示書の「33 前後」の内側 |");
        Console.WriteLine();
        Console.WriteLine("| # | 駒 | HP | 攻 | 速 | 型 | `Actions` | キー |");
        Console.WriteLine("|--:|---|--:|--:|--:|---|---|---|");
        for (int t = 0; t < dfT; t++)
        {
            UnitDef d = dfTargets[t];
            string keys = string.Join("・", dfKeyOfUnit[d.Id].Select(k => UnitTally.CarryKeys[k]));
            Console.WriteLine($"| {t + 1} | {d.Name} | {d.MaxHp} | {d.Attack} | {d.Speed} | {d.Pattern} "
                              + $"| {(d.Actions is null ? "—" : "[Skill]")} | {(keys.Length == 0 ? "**無し**" : keys)} |");
        }
        Console.WriteLine();
        Console.WriteLine("`Actions` が `[Skill]` の駒は**素体でそれを落とす**（＝素体は普通に殴る）。");
        Console.WriteLine("残すと素体が1ターンも何もしない駒になり、機構の帰属に体の値段が丸ごと混ざる。");
        Console.WriteLine();

        Console.WriteLine("## 0-2. N を決める式（測る前に固定）");
        Console.WriteLine();
        Console.WriteLine("    半幅 = 1.96 × s_u / √K_u ≤ 1.5pt");
        Console.WriteLine("    K_u  = N × 5 / 51            （51体から無作為5体。1駒あたりの在席標本数）");
        Console.WriteLine("    ⇒ N ≥ (51 / 5) × (1.96 × s_max / 1.5)²");
        Console.WriteLine();
        Console.WriteLine("**`s_max`（最悪の駒）で決める**——Q2 が「対象駒すべてで ±1.5pt 未満」を要求するため、");
        Console.WriteLine("平均の s で決めると必ず半分の駒が落ちる。");
        Console.WriteLine($"**M は 8 に固定**（パイロットで動かさない）。1標本の勝率が {dfW - 1}波 × 8 seed = {(dfW - 1) * 8} 戦で決まる。");
        Console.WriteLine();

        var pilot = DfMeasure(DfPilotN, DfM, DfBandA, "pilot");
        double pilotSec = dfSw.Elapsed.TotalSeconds;
        long pilotBattles = 0;
        for (int i = 0; i < pilot.N; i++) pilotBattles += (long)2 * dfW * DfM * (1 + pilot.Present[i].Length);
        double dfThr = pilotBattles / pilotSec;

        Console.WriteLine($"## 0-3. パイロット（N = {DfPilotN} / M = {DfM} / A 帯 seed {DfBandA}..{DfBandA + DfM - 1}）");
        Console.WriteLine();
        Console.WriteLine($"戦闘 **{pilotBattles:N0}** 回・**{pilotSec:F1} 秒**（{dfThr:N0} 戦/秒・並列）。");
        Console.WriteLine();
        var rR = Enumerable.Range(0, pilot.N).Select(i => DfRate(pilot, i, 0)).ToArray();
        var rH = Enumerable.Range(0, pilot.N).Select(i => DfRate(pilot, i, 1)).ToArray();
        Console.WriteLine("| 版 | 平均勝率 | 標準偏差 | 5〜95% の標本 | =0% | =100% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach ((string nm, double[] xs) in new[] { ("R（無作為）", rR), ("H（規則）", rH) })
        {
            var st = DfStats(xs);
            Console.WriteLine($"| {nm} | {st.Mean:F1}% | {st.Sd:F1} "
                              + $"| {xs.Count(x => x >= 5 && x <= 95) * 100.0 / xs.Length:F1}% "
                              + $"| {xs.Count(x => x == 0) * 100.0 / xs.Length:F1}% "
                              + $"| {xs.Count(x => x == 100) * 100.0 / xs.Length:F1}% |");
        }
        Console.WriteLine();
        Console.WriteLine($"**配置の価値（H − R）の速報: {DfP2(rH.Average() - rR.Average())}pt**（パイロット。主表で測り直す）");
        Console.WriteLine();

        var cR = DfContrib(pilot, 0);
        var cH = DfContrib(pilot, 1);
        double sMax = 0; string sMaxName = "";
        Console.WriteLine("### 寄与の標本間標準偏差（パイロット）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席 | 寄与(R) | s(R) | 寄与(H) | s(H) | max s |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int t = 0; t < dfT; t++)
        {
            var a = DfStats(cR[t]); var b = DfStats(cH[t]);
            double sCand = Math.Max(a.Sd, b.Sd);
            if (sCand > sMax) { sMax = sCand; sMaxName = dfTargets[t].Name; }
            Console.WriteLine($"| {dfTargets[t].Name} | {a.N} | {DfP1(a.Mean)} | {a.Sd:F1} "
                              + $"| {DfP1(b.Mean)} | {b.Sd:F1} | {sCand:F1} |");
        }
        Console.WriteLine();
        double needK = Math.Pow(DfZ * sMax / DfCiTarget, 2);
        double needN = needK * dfRN / 5.0;
        Console.WriteLine($"**s_max = {sMax:F2}pt（{sMaxName}）**");
        Console.WriteLine();
        Console.WriteLine($"    K ≥ (1.96 × {sMax:F2} / 1.5)² = {needK:F0}");
        Console.WriteLine($"    N ≥ (51 / 5) × {needK:F0} = {needN:F0}");
        Console.WriteLine();
        double mainBattles = DfN * (double)DfM * 2 * dfW * (1.0 + 5.0 * dfT / dfRN);
        Console.WriteLine($"**採用する N = {DfN}**（上の下限を上へ丸めた値。コードに固定してある）。");
        Console.WriteLine();
        Console.WriteLine($"主表1帯の見積もり: 戦闘 **{mainBattles:N0}** 回 ≒ **{mainBattles / dfThr / 60:F1} 分**"
                          + "（A 帯 + B 帯で倍）。指示書 §2-1 の 30 分の内側。");
        Console.WriteLine();
        Console.WriteLine($"所要 {dfSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ======================================================================================
    // check: 陰性対照（Q6 / 指示書 §2-4）
    // ======================================================================================
    if (dfArg == "check")
    {
        Console.WriteLine("# 第69期 —— 陰性対照（Q6）");
        Console.WriteLine();
        Console.WriteLine("## (1) 標本の乱数と戦闘の乱数が混ざっていないこと");
        Console.WriteLine();
        Console.WriteLine("標本抽選は `Random(1000000 + 標本番号)` だけを使い、**戦闘 seed の帯を引数に取らない**。");
        Console.WriteLine("同じ標本番号を2回引いて1体も席も違わないことを 2000 標本で照合する");
        Console.WriteLine("（帯は抽選の引数に無いので、A 帯と B 帯で標本が変わりようがない）。");
        Console.WriteLine();
        int mismatch = 0;
        for (int i = 0; i < 2000; i++)
        {
            var a = DfSampleOf(i);
            var b = DfSampleOf(i);
            if (!a.Units.Select(u => u.Id).SequenceEqual(b.Units.Select(u => u.Id))
                || !a.RSlot.SequenceEqual(b.RSlot) || !a.HSlot.SequenceEqual(b.HSlot)) mismatch++;
        }
        Console.WriteLine($"- 再現性（同じ標本番号を2回引く）: 食い違い **{mismatch} 件 / 2000**");
        Console.WriteLine();
        Console.WriteLine("### 抽選が一様であることの確認（駒ごとの出現率）");
        Console.WriteLine();
        var appear = new int[dfRN];
        for (int i = 0; i < 11000; i++)
        {
            var (dfU, _, _) = DfSampleOf(i);
            foreach (UnitDef u in dfU) appear[Array.IndexOf(dfRoster, u)]++;
        }
        double expect = 11000 * 5.0 / dfRN;
        Console.WriteLine($"- 11000 標本での出現数: 期待値 **{expect:F1}** / 実測 **{appear.Min()}〜{appear.Max()}**"
                          + $"（最小 {dfRoster[Array.IndexOf(appear, appear.Min())].Name} / "
                          + $"最大 {dfRoster[Array.IndexOf(appear, appear.Max())].Name}）");
        Console.WriteLine($"- **一度も出ない駒 {appear.Count(x => x == 0)} 体**（0 でなければ標本が偏っている）");
        Console.WriteLine();

        Console.WriteLine("## (2) 標本に含まれない駒を素体に差し替えても1セルも動かないこと");
        Console.WriteLine();
        Console.WriteLine("指示書 §2-4 の陰性対照。**差し替えの窓口が「標本の中のその駒」だけを見ている**ことの検算で、");
        Console.WriteLine($"200 標本 × {dfW} 波 × seed 0..49 を、含まれない対象駒で素体化した版と突き合わせる。");
        Console.WriteLine();
        int cells = 0, diff = 0;
        for (int i = 0; i < 200; i++)
        {
            var (dfU, rslot, _) = DfSampleOf(i);
            var f = new Formation();
            for (int k = 0; k < 5; k++) f[rslot[k]] = dfU[k];
            int outer = Enumerable.Range(0, dfT).First(t => !dfU.Any(u => u.Id == dfTargets[t].Id));
            var g = new Formation();
            for (int k = 0; k < 5; k++)
                g[rslot[k]] = dfU[k].Id == dfTargets[outer].Id ? dfPlainDef[outer] : dfU[k];
            for (int w = 0; w < dfW; w++)
                for (int seed = 0; seed < 50; seed++)
                {
                    cells++;
                    var ra = BattleEngine.Run(f, dfStages[w].Enemy, seed, verbose: false);
                    var rb = BattleEngine.Run(g, dfStages[w].Enemy, seed, verbose: false);
                    if (ra.PlayerWon != rb.PlayerWon || ra.Turns != rb.Turns) diff++;
                }
        }
        Console.WriteLine($"- 照合セル **{cells:N0}** / 食い違い **{diff} 件**");
        Console.WriteLine();
        Console.WriteLine("## (3) 盤面");
        Console.WriteLine();
        Console.WriteLine("`draft` は `BattleEngine.Run` を読むだけで、`Traits.cs` / `UnitCatalog` / `Stages` /");
        Console.WriteLine("`CompareBuilds()` を1行も動かしていない。`compare` / `dump` / `engage` の再生成と");
        Console.WriteLine("`docs/` の diff は別に取る（報告書 §1-3）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {dfSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ======================================================================================
    // 主表（表A〜F）
    // ======================================================================================
    {
        bool alt = dfArg == "alt";
        int band = alt ? DfBandB : DfBandA;
        string bandName = alt ? "B" : "A";
        var res = DfMeasure(DfN, DfM, band, $"{bandName}帯");

        var dfRateArr = new double[2][];
        for (int v = 0; v < 2; v++) dfRateArr[v] = Enumerable.Range(0, res.N).Select(i => DfRate(res, i, v)).ToArray();
        var contrib = new[] { DfContrib(res, 0), DfContrib(res, 1) };
        string[] verName = { "R（無作為）", "H（規則）" };

        Console.WriteLine($"# 第69期 —— ドラフト台（{bandName} 帯 seed {band}..{band + DfM - 1}）");
        Console.WriteLine();
        Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 draft{(alt ? " alt" : "")}` の出力。**`docs/` には置かない。**");
        Console.WriteLine();
        Console.WriteLine($"標本 **{DfN:N0}**（`UnitCatalog.All` {dfRN} 体から無作為5体・重複なし）× 配置2版 × {dfW} 波 × seed {DfM} 本。");
        Console.WriteLine($"**第一波も回して集計から除外**（標本の勝率は第2〜{dfW}波の {(dfW - 1) * DfM} 戦）。");
        Console.WriteLine($"対象駒 **{dfT}** 体は素体（同数値・特性なし）との差でも測る。");
        Console.WriteLine();

        // ---- 表A ----
        Console.WriteLine("## 表A —— 標本の分布");
        Console.WriteLine();
        Console.WriteLine("波ごとの行は「その波だけ」の勝率（seed 8 本）。`全体` が第2〜5波の集計で、以下の表はすべてこれを使う。");
        Console.WriteLine("右の列は標本の割合（%）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 対象 | 平均 | 標準偏差 | =0 | (0,5) | [5,25) | [25,50) | [50,75) | [75,95] | (95,100) | =100 | 5〜95% |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int v = 0; v < 2; v++)
        {
            for (int w = 0; w < dfW; w++)
            {
                var xs = Enumerable.Range(0, res.N).Select(i => res.Full[i][v * dfW + w] * 100.0 / DfM).ToArray();
                Console.WriteLine(DfHistRow(verName[v], $"第{w + 1}波{(w == 0 ? "（集計外）" : "")}", xs));
            }
            Console.WriteLine(DfHistRow(verName[v], "**全体**", dfRateArr[v]));
        }
        Console.WriteLine();
        double q1 = dfRateArr[0].Count(x => x >= 5 && x <= 95) * 100.0 / DfN;
        Console.WriteLine($"**Q1**: R の `全体` で 5〜95% の標本が **{q1:F1}%**（判定は 60% 以上）"
                          + $"→ **{(q1 >= 60 ? "○" : "×")}**");
        Console.WriteLine();

        // ---- 表C（配置の価値・全体） ----
        double hr = dfRateArr[1].Average() - dfRateArr[0].Average();
        var pairDiff = Enumerable.Range(0, DfN).Select(i => dfRateArr[1][i] - dfRateArr[0][i]).ToArray();
        var hrSt = DfStats(pairDiff);
        Console.WriteLine("## 表C —— 配置の価値（H − R）");
        Console.WriteLine();
        Console.WriteLine("**同じ標本・同じ戦闘 seed の対応のある差**（標本ごとに H と R の両方を回している）。");
        Console.WriteLine();
        Console.WriteLine("| 量 | 値 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| R（無作為）の平均勝率 | {dfRateArr[0].Average():F2}% |");
        Console.WriteLine($"| H（規則）の平均勝率 | {dfRateArr[1].Average():F2}% |");
        Console.WriteLine($"| **H − R** | **{DfP2(hr)}pt** |");
        Console.WriteLine($"| 差の標準偏差 | {hrSt.Sd:F2} |");
        Console.WriteLine($"| 95% 信頼区間 | ±{DfZ * hrSt.Sd / Math.Sqrt(DfN):F2}pt |");
        Console.WriteLine($"| H が勝った標本 | {pairDiff.Count(x => x > 0) * 100.0 / DfN:F1}% |");
        Console.WriteLine($"| 同じ標本 | {pairDiff.Count(x => x == 0) * 100.0 / DfN:F1}% |");
        Console.WriteLine($"| R が勝った標本 | {pairDiff.Count(x => x < 0) * 100.0 / DfN:F1}% |");
        Console.WriteLine();
        Console.WriteLine("### 波ごと");
        Console.WriteLine();
        Console.WriteLine("| 波 | R | H | H − R |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int w = 0; w < dfW; w++)
        {
            double a = Enumerable.Range(0, DfN).Sum(i => (double)res.Full[i][0 * dfW + w]) * 100.0 / (DfN * (double)DfM);
            double b = Enumerable.Range(0, DfN).Sum(i => (double)res.Full[i][1 * dfW + w]) * 100.0 / (DfN * (double)DfM);
            Console.WriteLine($"| 第{w + 1}波{(w == 0 ? "（集計外）" : "")} | {a:F2}% | {b:F2}% | **{DfP2(b - a)}** |");
        }
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B —— 駒ごとの寄与");
        Console.WriteLine();
        Console.WriteLine("**寄与 = 標本の勝率 − その駒を素体（同数値・特性なし）に差し替えた版の勝率。**");
        Console.WriteLine("正なら「その駒の**特性**がいると強い」（体の値段は素体側に残るので入らない）。");
        Console.WriteLine("`理想` の併記は表F にある（同じ実行の中で 61 行を測り直した値）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | キー | 在席 | 寄与(R) | ±95% | 寄与(H) | ±95% | H − R |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        var ciFail = new List<string>();
        foreach (int t in Enumerable.Range(0, dfT).OrderByDescending(t => DfStats(contrib[0][t]).Mean))
        {
            var a = DfStats(contrib[0][t]); var b = DfStats(contrib[1][t]);
            double ca = DfZ * a.Sd / Math.Sqrt(Math.Max(1, a.N)), cb = DfZ * b.Sd / Math.Sqrt(Math.Max(1, b.N));
            if (ca >= DfCiTarget || cb >= DfCiTarget) ciFail.Add($"{dfTargets[t].Name}（±{Math.Max(ca, cb):F2}）");
            string keys = string.Join("・", dfKeyOfUnit[dfTargets[t].Id].Select(k => UnitTally.CarryKeys[k]));
            Console.WriteLine($"| {dfTargets[t].Name} | {keys} | {a.N} "
                              + $"| **{DfP1(a.Mean)}** | ±{ca:F2} | **{DfP1(b.Mean)}** | ±{cb:F2} "
                              + $"| {DfP1(b.Mean - a.Mean)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q2**: 信頼区間の半幅が ±{DfCiTarget} 未満でない駒 **{ciFail.Count} 体**"
                          + $"{(ciFail.Count == 0 ? "" : " —— " + string.Join(" / ", ciFail))}"
                          + $" → **{(ciFail.Count == 0 ? "○" : "×")}**");
        Console.WriteLine();

        // ---- 表C-2 駒ごとの配置の価値 ----
        Console.WriteLine("### 表C-2 —— 駒ごとの配置の価値");
        Console.WriteLine();
        Console.WriteLine("その駒がいる標本だけを集めた H − R。**「置き場所で得をする駒」の一覧がここに出る。**");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席 | R の勝率 | H の勝率 | H − R | ±95% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        var seatRows = new List<(int T, double D, double Ci, int N, double A, double B)>();
        for (int t = 0; t < dfT; t++)
        {
            var idxs = Enumerable.Range(0, DfN).Where(i => Array.IndexOf(res.Present[i], t) >= 0).ToArray();
            var d = idxs.Select(i => dfRateArr[1][i] - dfRateArr[0][i]).ToList();
            var st = DfStats(d);
            // **第141期: 在席 0 の対象を落とさない。** エグ・ハリは `dfTargets`（この期に固定した 30 体）には居るが
            // 抽選の母集団 `dfRoster`（＝`All`）には居ないので、標本が 1 つも無い（`Average` が空で落ちていた）。
            // 行は残して「—」で出す——「測っていない」を「無かった」に見せないため（第61期）。
            seatRows.Add((t, st.Mean, DfZ * st.Sd / Math.Sqrt(Math.Max(1, st.N)), idxs.Length,
                          idxs.Length == 0 ? double.NaN : idxs.Average(i => dfRateArr[0][i]),
                          idxs.Length == 0 ? double.NaN : idxs.Average(i => dfRateArr[1][i])));
        }
        foreach (var r2 in seatRows.OrderByDescending(x => x.N > 0 ? 1 : 0).ThenByDescending(x => x.D))
            Console.WriteLine(r2.N == 0
                ? $"| {dfTargets[r2.T].Name} | 0 | — | — | — | — |"
                : $"| {dfTargets[r2.T].Name} | {r2.N} | {r2.A:F2}% | {r2.B:F2}% "
                  + $"| **{DfP2(r2.D)}** | ±{r2.Ci:F2} |");
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D —— 相方の有無");
        Console.WriteLine();
        Console.WriteLine("相方 = **その標本の中で、その駒と同じキーの書き手または読み手である他の駒**");
        Console.WriteLine("（第68期 §1-4 の機械的定義をそのまま写した。特性 → キー の対応表も同じもの）。");
        Console.WriteLine("**第68期の表D で「相方なし行が 0」だった 27 駒の右半分が、ここで初めて埋まる。** R 版で判定する。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | キー | 相方あり | 寄与 | 相方なし | 寄与 | 差（なし − あり） | ±95% |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        int q5 = 0;
        for (int t = 0; t < dfT; t++)
        {
            var withL = new List<double>(); var soloL = new List<double>();
            int[] myKeys = dfKeyOfUnit[dfTargets[t].Id];
            for (int i = 0; i < DfN; i++)
            {
                int p = Array.IndexOf(res.Present[i], t);
                if (p < 0) continue;
                int[] mem = res.Members[i];
                bool has = false;
                for (int k = 0; k < 5 && !has; k++)
                {
                    if (dfRoster[mem[k]].Id == dfTargets[t].Id) continue;
                    if (dfKeyOfUnit[dfRoster[mem[k]].Id].Any(x => myKeys.Contains(x))) has = true;
                }
                double c = DfRate(res, i, 0) - DfPlainRate(res, i, 0, p);
                (has ? withL : soloL).Add(c);
            }
            var w2 = DfStats(withL); var s3 = DfStats(soloL);
            double d2 = (w2.N > 0 && s3.N > 0) ? s3.Mean - w2.Mean : double.NaN;
            double ci = (w2.N > 1 && s3.N > 1)
                ? DfZ * Math.Sqrt(w2.Sd * w2.Sd / w2.N + s3.Sd * s3.Sd / s3.N) : double.NaN;
            if (!double.IsNaN(d2) && Math.Abs(d2) >= 1.5) q5++;
            string keys = string.Join("・", myKeys.Select(k => UnitTally.CarryKeys[k]));
            Console.WriteLine($"| {dfTargets[t].Name} | {keys} | {w2.N} | {(w2.N > 0 ? $"{DfP1(w2.Mean)}" : "—")} "
                              + $"| {s3.N} | {(s3.N > 0 ? $"{DfP1(s3.Mean)}" : "—")} "
                              + $"| {(double.IsNaN(d2) ? "—" : $"**{DfP1(d2)}**")} "
                              + $"| {(double.IsNaN(ci) ? "—" : $"±{ci:F2}")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q5**: 相方あり/なしの寄与差が 1.5pt 以上ある駒 **{q5} 体**（判定は 10 体以上）"
                          + $" → **{(q5 >= 10 ? "○" : "×")}**");
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E —— 枚数効果");
        Console.WriteLine();
        Console.WriteLine("キーごとに、その標本にそのキーを持つ駒が何枚いたかで標本を分けて平均勝率を出す（R 版）。");
        Console.WriteLine("**`0枚` を必ず並べる**——1枚と2枚だけを比べると「2枚では一人前以下」が読めない。");
        Console.WriteLine();
        Console.WriteLine("| キー | 0枚 | n | 1枚 | n | 2枚 | n | 3枚以上 | n | 2枚 − 1枚 | 1枚 − 0枚 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int k = 0; k < UnitTally.CarryKeys.Length; k++)
        {
            var buckets = new List<double>[4];
            for (int b = 0; b < 4; b++) buckets[b] = new List<double>();
            for (int i = 0; i < DfN; i++)
            {
                int c = res.Members[i].Count(m => dfKeyOfUnit[dfRoster[m].Id].Contains(k));
                buckets[Math.Min(c, 3)].Add(dfRateArr[0][i]);
            }
            string Cell(int b) => buckets[b].Count == 0 ? "—" : $"{buckets[b].Average():F2}%";
            string Delta(int a2, int b2) => (buckets[a2].Count == 0 || buckets[b2].Count == 0)
                ? "—" : $"{DfP2(buckets[a2].Average() - buckets[b2].Average())}";
            Console.WriteLine($"| {UnitTally.CarryKeys[k]} | {Cell(0)} | {buckets[0].Count} | {Cell(1)} | {buckets[1].Count} "
                              + $"| {Cell(2)} | {buckets[2].Count} | {Cell(3)} | {buckets[3].Count} "
                              + $"| **{Delta(2, 1)}** | {Delta(1, 0)} |");
        }
        Console.WriteLine();

        // ---- 表F: 理想編成の測り直し ----
        Console.WriteLine("## 表F —— 理想編成との相関");
        Console.WriteLine();
        Console.WriteLine("**理想編成の値は同じ実行の中で測り直す**（別の実行から引かない。第13期の作法）。");
        Console.WriteLine("`理想·素体` は**ドラフト台とまったく同じ器具**（素体差し替え・第2〜5波・seed 0..199）を 61 行に当てた値。");
        Console.WriteLine("`理想·抜き5波` は**第68期の表D と同じ器具**（1枚抜き・全5波）で、published 値の再現になる。");
        Console.WriteLine();
        Console.WriteLine("**指示書 §3 の表F は「表B × 第68期の表D」だが、器具が違う**（素体差し替え 対 1枚抜き）ので、");
        Console.WriteLine("**器具を揃えた散布を主判定に、指示書どおりの散布を併記に**した。両方を下に出す。");
        Console.WriteLine();

        var idealRows = CompareBuilds();
        var idealFull = new double[idealRows.Length][];
        Parallel.For(0, idealRows.Length, r =>
        {
            var xs = new double[dfW];
            for (int w = 0; w < dfW; w++)
            {
                int win = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(idealRows[r].F, dfStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                xs[w] = win * 100.0 / 200;
            }
            idealFull[r] = xs;
        });

        var idealJobs = new List<(int Row, int T, int Slot)>();
        for (int r = 0; r < idealRows.Length; r++)
            foreach ((int slot, UnitDef d) in idealRows[r].F.Occupied())
                if (dfTargetIdx.TryGetValue(d.Id, out int ti)) idealJobs.Add((r, ti, slot));
        var idealPlain = new double[idealJobs.Count][];
        var idealDrop = new double[idealJobs.Count][];
        Parallel.For(0, idealJobs.Count, j =>
        {
            (int r, int ti, int slot) = idealJobs[j];
            var pf = idealRows[r].F.Clone(); pf[slot] = dfPlainDef[ti];
            var df = idealRows[r].F.Clone(); df[slot] = null;
            var a = new double[dfW]; var b = new double[dfW];
            for (int w = 0; w < dfW; w++)
            {
                int wa = 0, wb = 0;
                for (int seed = 0; seed < 200; seed++)
                {
                    if (BattleEngine.Run(pf, dfStages[w].Enemy, seed, verbose: false).PlayerWon) wa++;
                    if (BattleEngine.Run(df, dfStages[w].Enemy, seed, verbose: false).PlayerWon) wb++;
                }
                a[w] = wa * 100.0 / 200; b[w] = wb * 100.0 / 200;
            }
            idealPlain[j] = a; idealDrop[j] = b;
        });

        var idPlainC = new List<double>[dfT];
        var idDropC = new List<double>[dfT];
        var idDrop5 = new List<double>[dfT];
        for (int t = 0; t < dfT; t++) { idPlainC[t] = new List<double>(); idDropC[t] = new List<double>(); idDrop5[t] = new List<double>(); }
        for (int j = 0; j < idealJobs.Count; j++)
        {
            (int r, int ti, _) = idealJobs[j];
            double f25 = idealFull[r].Skip(1).Average();
            double f5 = idealFull[r].Average();
            idPlainC[ti].Add(f25 - idealPlain[j].Skip(1).Average());
            idDropC[ti].Add(f25 - idealDrop[j].Skip(1).Average());
            idDrop5[ti].Add(f5 - idealDrop[j].Average());
        }

        Console.WriteLine("| 駒 | 行数 | ドラフト(R) | ドラフト(H) | 理想·素体 | 理想·抜き | 理想·抜き5波 | 第68期 表D |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        var xR = new List<double>(); var xH = new List<double>();
        var yPlain = new List<double>(); var yDrop = new List<double>();
        var pub = DfPublished68();
        double maxRep = 0;
        for (int t = 0; t < dfT; t++)
        {
            if (idPlainC[t].Count == 0) continue;
            double a = DfStats(contrib[0][t]).Mean, b = DfStats(contrib[1][t]).Mean;
            double p = idPlainC[t].Average(), d3 = idDropC[t].Average(), d5 = idDrop5[t].Average();
            xR.Add(a); xH.Add(b); yPlain.Add(p); yDrop.Add(d3);
            string pv = "—";
            if (pub.TryGetValue(dfTargets[t].Id, out double v))
            {
                pv = $"{DfP1(v)}";
                maxRep = Math.Max(maxRep, Math.Abs(d5 - v));
            }
            Console.WriteLine($"| {dfTargets[t].Name} | {idPlainC[t].Count} | {DfP1(a)} | {DfP1(b)} "
                              + $"| {DfP1(p)} | {DfP1(d3)} | {DfP1(d5)} | {pv} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**器具の検算**: `理想·抜き5波` と第68期 表D の published 値の最大差 **{maxRep:F1}pt**"
                          + "（同じ器具・同じ seed 帯なので、表の丸めぶんだけずれるのが正しい）。");
        Console.WriteLine();
        Console.WriteLine("| 散布 | r | max\\|Δ\\| |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| **ドラフト(R) × 理想·素体（器具を揃えた・主判定）** | **{DfCorr(xR, yPlain):F3}** "
                          + $"| {Enumerable.Range(0, xR.Count).Max(i => Math.Abs(xR[i] - yPlain[i])):F1}pt |");
        Console.WriteLine($"| ドラフト(H) × 理想·素体 | {DfCorr(xH, yPlain):F3} "
                          + $"| {Enumerable.Range(0, xH.Count).Max(i => Math.Abs(xH[i] - yPlain[i])):F1}pt |");
        Console.WriteLine($"| ドラフト(R) × 理想·抜き（指示書どおり） | {DfCorr(xR, yDrop):F3} "
                          + $"| {Enumerable.Range(0, xR.Count).Max(i => Math.Abs(xR[i] - yDrop[i])):F1}pt |");
        Console.WriteLine($"| ドラフト(H) × 理想·抜き | {DfCorr(xH, yDrop):F3} "
                          + $"| {Enumerable.Range(0, xH.Count).Max(i => Math.Abs(xH[i] - yDrop[i])):F1}pt |");
        Console.WriteLine($"| 理想·素体 × 理想·抜き（**器具どうし**・台は同じ） | {DfCorr(yPlain, yDrop):F3} "
                          + $"| {Enumerable.Range(0, yPlain.Count).Max(i => Math.Abs(yPlain[i] - yDrop[i])):F1}pt |");
        Console.WriteLine();
        double rMain = DfCorr(xR, yPlain);
        Console.WriteLine($"**Q4**: 主判定 r = **{rMain:F3}** → "
                          + $"**{(rMain < 0.7 ? "台が違う（r < 0.7）" : rMain >= 0.9 ? "台が同じ（r ≥ 0.9）" : "中間（0.7〜0.9）")}**");
        Console.WriteLine();
        Console.WriteLine($"**Q3**: 表C の H − R = **{DfP2(hr)}pt**（判定は ≥ +1.5pt かつ両 seed 帯で符号一致）"
                          + $" → この帯では **{(hr >= 1.5 ? "○" : "×")}**。もう1帯は `draft alt` で取る。");
        Console.WriteLine();
        Console.WriteLine($"所要 {dfSw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }
}
}
