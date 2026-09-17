using BattleCore;
using static Common;

// =====================================================================================
// carry モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "carry")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 carry
// =====================================================================================

static class CarryDiag
{
// 棚卸し: 条件付き変質を載せられる駒はどれか（第68期・調査）。
// **この期は新しい機構を1つも作らない。駒を作らない。最後の1枠を使わない。`docs/` の差分はゼロ。**
//
// 第66・67期はヨミ1枚で2回落ちた。落ちた理由は駒の性質ではなく**供給の時間分布**だった
// （第67期 7-2: 開戦時一括の供給は閾値を上げても 100% → 0% の2値にしか割れない）。
// ここでやるのは、第67期の格子（閾値ごとの到達%と到達T）を
// **ヨミ専用から全51体・全キーへ一般化する**こと。**既存モードは1行も書き換えていない。**
//
// 足したのは **誰も読んで分岐しない計数**（`UnitTally.Carry*`。窓口 `Whet` / `Dull` /
// `ApplyDamage` / `SwapSlots` と `UnitState.SetCounter` の5箇所で積む）だけで、
// **特性の側には1行も足していない**。`compare` は 305 セル 0 件で一致する（Q5）。
//
//     dotnet run --project BattleSim -c Release 0 carry        # 表A・表B と Q1〜Q3（両 seed 帯）
//     dotnet run --project BattleSim -c Release 0 carry keys   # 表C（供給の形の一覧。同じ走査を A 帯だけで）
//     dotnet run --project BattleSim -c Release 0 carry solo   # 表D（単独成立度）と Q4
//     dotnet run --project BattleSim -c Release 0 carry leak   # 表E（`AcceptsSupport` の漏れの地図）
//     dotnet run --project BattleSim -c Release 0 carry check  # Q5（陰性対照）
public static void Run(string[] args, int stageIndex)
{
    string cyArg = args.Length > 2 ? args[2] : "";
    IReadOnlyList<EnemyCatalog.Stage> cyStages = EnemyCatalog.Stages;
    var cyRows = CompareBuilds();
    int cyNK = UnitTally.CarryKeys.Length;
    int cyNP = UnitTally.CarryProbes.Length;
    var cySw = System.Diagnostics.Stopwatch.StartNew();

    // ---- 形の判定（**測る前に固定**。指示書 §1-3 の判定式をコードにしたもの） ----------
    //
    // 不発: 格子1 への到達% < 20
    // 2値 : 到達% が 80 以上の最大格子と 20 未満の最小格子が**隣り合う**、
    //       または到達T の 最大−最小 < 0.5 ターン（**到達した格子点が2つ以上あるときだけ**
    //       ——1点しか無い「幅」は幅ではない。指示書が書いていない側を埋めた1点）
    // 連続: 到達% が 20〜80 に入る格子が 2 つ以上、かつ**その帯の格子点の上で**到達T が単調増加
    //       （帯の外＝裾の到達T は「そこまで届いた試行」の偏りで前後する。
    //        指示書の1文が同じ格子点について2つの条件を並べていると読んだ）
    // 他  : どれにも当たらない
    //
    // 優先順位は 不発 → 2値 → 連続 → 他。**重なりうるので順序を測る前に決めてある。**
    const string CyMute = "不発", CyBin = "2値", CyCont = "連続", CyElse = "他";
    string CyForm(int trials, int[] n, double[] tsum)
    {
        if (trials == 0) return CyMute;
        var p = new double[cyNP];
        for (int i = 0; i < cyNP; i++) p[i] = n[i] * 100.0 / trials;
        if (p[0] < 20.0) return CyMute;

        for (int i = 0; i + 1 < cyNP; i++)
            if (p[i] >= 80.0 && p[i + 1] < 20.0) return CyBin;

        var hit = Enumerable.Range(0, cyNP).Where(i => n[i] > 0).ToArray();
        if (hit.Length >= 2)
        {
            var tt = hit.Select(i => tsum[i] / n[i]).ToArray();
            if (tt.Max() - tt.Min() < 0.5) return CyBin;
        }

        var band = Enumerable.Range(0, cyNP).Where(i => p[i] >= 20.0 && p[i] <= 80.0).ToArray();
        if (band.Length >= 2)
        {
            bool mono = true;
            for (int j = 1; j < band.Length; j++)
            {
                double a = tsum[band[j - 1]] / n[band[j - 1]];
                double b = tsum[band[j]] / n[band[j]];
                if (b < a - 1e-9) { mono = false; break; }
            }
            if (mono) return CyCont;
        }
        return CyElse;
    }

    // ---- 1帯ぶんの走査。行 × 駒 のセルに積む -------------------------------------------
    Dictionary<(string Row, string Unit), CyCell> CyScan(int seed0, int seedN)
    {
        var data = new Dictionary<(string Row, string Unit), CyCell>();
        foreach ((string rn, Formation f) in cyRows)
        {
            var members = f.Occupied().Select(o => o.Def).ToArray();
            foreach (UnitDef d in members) data[(rn, d.Id)] = new CyCell(cyNK, cyNP);
            for (int w = 0; w < cyStages.Count; w++)
                for (int seed = seed0; seed < seed0 + seedN; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, cyStages[w].Enemy, seed, verbose: false);
                    foreach (UnitDef d in members)
                    {
                        CyCell c = data[(rn, d.Id)];
                        c.Trials++;
                        if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
                        c.Attacks += t.Attacks;
                        c.AtkGain += t.CarryAtkGain;
                        c.Taken += t.DamageTaken;
                        c.Deaths += t.Deaths;
                        if (t.CarryAmount is int[] am)
                            for (int k = 0; k < cyNK; k++) { c.Amount[k] += am[k]; c.Count[k] += t.CarryCount![k]; }
                        if (t.CarryProbeTurn is int[][] pt)
                            for (int k = 0; k < cyNK; k++)
                            {
                                if (pt[k] is not int[] pk) continue;
                                for (int i = 0; i < cyNP; i++)
                                {
                                    if (pk[i] == 0) continue;
                                    c.ProbeN[k * cyNP + i]++;
                                    c.ProbeT[k * cyNP + i] += pk[i];
                                }
                            }
                    }
                }
            Console.Error.Write(".");
        }
        Console.Error.WriteLine();
        return data;
    }

    // 駒 → その駒を含む行（compare の並び順）
    var cyUnitRows = new Dictionary<string, List<string>>();
    foreach ((string rn, Formation f) in cyRows)
        foreach ((_, UnitDef d) in f.Occupied())
        {
            if (!cyUnitRows.TryGetValue(d.Id, out var l)) cyUnitRows[d.Id] = l = new List<string>();
            if (!l.Contains(rn)) l.Add(rn);
        }
    var cyRoster = UnitCatalog.All.Where(u => cyUnitRows.ContainsKey(u.Id)).ToArray();

    string CyFormOf(CyCell c, int k)
    {
        var n = new int[cyNP];
        var t = new double[cyNP];
        for (int i = 0; i < cyNP; i++) { n[i] = c.ProbeN[k * cyNP + i]; t[i] = c.ProbeT[k * cyNP + i]; }
        return CyForm(c.Trials, n, t);
    }

    // ---- 表E: `AcceptsSupport` の漏れの地図 ---------------------------------------------
    if (cyArg == "leak")
    {
        // ガルドから `Stoic` **だけ**を外した版。数値も庇うも1つも動かさない
        // （規則にノブを増やさず駒の側で塞ぐ。第47期・`gradient` / `aim` と同じ扱い）。
        var cyGaldOpen = new UnitDef
        {
            Id = UnitCatalog.Gald.Id,
            Name = "ガルド（Stoic なし）",
            MaxHp = UnitCatalog.Gald.MaxHp,
            Attack = UnitCatalog.Gald.Attack,
            Speed = UnitCatalog.Gald.Speed,
            Traits = new[] { TraitId.Guardian },
            PlusText = "", MinusText = "", Flavor = ""
        };

        var galdRows = cyRows.Where(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Gald.Id)).ToArray();

        Console.WriteLine("# 表E —— `AcceptsSupport` の漏れの地図（第68期）");
        Console.WriteLine();
        Console.WriteLine($"seed 0..199 × {cyStages.Count} 波。ガルド（`Stoic`）を含む **{galdRows.Length} 行**。");
        Console.WriteLine("`SupportTargets` は受け取らない駒のぶんを**隣接する味方へそのまま流す**ので、");
        Console.WriteLine("ガルドの隣は**直接ぶんと拡散ぶんで二重に受ける**（第67期 7-4 の一般化）。");
        Console.WriteLine();
        Console.WriteLine("漏れ量は対照との差で測る——**ガルドから `Stoic` だけを外した版**（数値も庇うも同一）を");
        Console.WriteLine("同じ席で回し、隣接する味方の受取の差を取る。**この対照は盤面を動かす**");
        Console.WriteLine("（`Stoic` は受取そのものを止める性質なので、切れば拡散が消えると同時に本人が受け取り始める）");
        Console.WriteLine("——第56期の作法どおり、読みは「狙った経路の計数が動いたか」の1本で取る。");
        Console.WriteLine();
        Console.WriteLine("| 行 | ガルドの席 | 次数 | 隣接する味方 | 強化・隣（現行） | 強化・隣（Stoic なし） | 漏れ（強化） "
                          + "| 弱体・隣（現行） | 弱体・隣（Stoic なし） | 漏れ（弱体） | ガルド本人（現行→Stoic なし） |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|---|");

        double totWhet = 0, totDull = 0;
        foreach ((string rn, Formation f) in galdRows)
        {
            int gslot = f.Occupied().First(o => o.Def.Id == UnitCatalog.Gald.Id).Slot;
            var neigh = f.Occupied().Where(o => o.Slot != gslot && FormationRules.AreAdjacent(gslot, o.Slot))
                                    .Select(o => o.Def).ToArray();
            var open = new Formation();
            foreach ((int sl, UnitDef d) in f.Occupied())
                open[sl] = d.Id == UnitCatalog.Gald.Id ? cyGaldOpen : d;

            (double W, double D, double GW, double GD) Measure(Formation g)
            {
                double w = 0, d2 = 0, gw = 0, gd = 0;
                int n = 0;
                for (int wv = 0; wv < cyStages.Count; wv++)
                    for (int seed = 0; seed < 200; seed++)
                    {
                        BattleResult r = BattleEngine.Run(g, cyStages[wv].Enemy, seed, verbose: false);
                        n++;
                        foreach (UnitDef nd in neigh)
                            if (r.TallyByUnit.TryGetValue(nd.Id, out UnitTally? t) && t.CarryAmount is int[] a)
                            {
                                w += a[UnitTally.CarryWhet];
                                d2 += a[UnitTally.CarryDull];
                            }
                        if (r.TallyByUnit.TryGetValue(UnitCatalog.Gald.Id, out UnitTally? gt) && gt.CarryAmount is int[] ga)
                        {
                            gw += ga[UnitTally.CarryWhet];
                            gd += ga[UnitTally.CarryDull];
                        }
                    }
                return (w / n, d2 / n, gw / n, gd / n);
            }

            var cur = Measure(f);
            var alt = Measure(open);
            double lkW = cur.W - alt.W, lkD = cur.D - alt.D;
            // 表示上 0 に丸まる負の微小値を潰す（書式の負セクションに落ちて `-+0.00` になる）
            if (Math.Abs(lkW) < 0.005) lkW = 0;
            if (Math.Abs(lkD) < 0.005) lkD = 0;
            int deg = 0;
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                if (FormationRules.AreAdjacent(gslot, i)) deg++;
            totWhet += lkW;
            totDull += lkD;
            Console.WriteLine($"| {rn} | {FormationRules.SeatNames[gslot]} | {deg} | {string.Join(" / ", neigh.Select(x => x.Name))} "
                              + $"| {cur.W:F2} | {alt.W:F2} | **{lkW:+0.00;-0.00}** "
                              + $"| {cur.D:F2} | {alt.D:F2} | **{lkD:+0.00;-0.00}** "
                              + $"| 強化 {cur.GW:F2}→{alt.GW:F2} / 弱体 {cur.GD:F2}→{alt.GD:F2} |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine($"**漏れの合計（{galdRows.Length} 行平均）: 強化 {totWhet / galdRows.Length:+0.00;-0.00} 量/戦 / "
                          + $"弱体 {totDull / galdRows.Length:+0.00;-0.00} 量/戦。**");
        Console.WriteLine();
        Console.WriteLine($"所要 {cySw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ---- Q5: 陰性対照 --------------------------------------------------------------------
    if (cyArg == "check")
    {
        Console.WriteLine("# Q5 —— 陰性対照（第68期）");
        Console.WriteLine();
        Console.WriteLine($"`compare` と同じ走査（{cyRows.Length} 行 × {cyStages.Count} 波 × seed 0..199）を");
        Console.WriteLine("**`verbose` の有無**で2回回し、1セルも違わないことを確かめる。");
        Console.WriteLine("計数は `UnitTally` の私有カウンタで誰も読んで分岐しないので原理的には一致するが、");
        Console.WriteLine("第68期は `UnitState.SetCounter` と `AtkBonus` の setter に通知を足したので、");
        Console.WriteLine("**乱数列が動いていないこと**をここで直に見る（`docs/balance.md` との照合は `compare` の diff で別に取る）。");
        Console.WriteLine();
        int diff = 0;
        Console.WriteLine("| 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach ((string rn, Formation f) in cyRows)
        {
            var win = new double[cyStages.Count];
            for (int w = 0; w < cyStages.Count; w++)
            {
                int a = 0, b = 0;
                for (int seed = 0; seed < 200; seed++)
                {
                    if (BattleEngine.Run(f, cyStages[w].Enemy, seed, verbose: false).PlayerWon) a++;
                    if (BattleEngine.Run(f, cyStages[w].Enemy, seed, verbose: true).PlayerWon) b++;
                }
                if (a != b) diff++;
                win[w] = a / 2.0;
            }
            Console.WriteLine($"| {rn} | " + string.Join(" | ", win.Select(x => $"{x:F1}%")) + " |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine($"**`verbose` の有無で食い違ったセル {diff} 件 / {cyRows.Length * cyStages.Count}。**");
        Console.WriteLine();
        Console.WriteLine($"所要 {cySw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ---- 表D: 単独成立度（指示書 §1-4） --------------------------------------------------
    if (cyArg == "solo")
    {
        // 相方の定義（**機械的**）: その行で、その駒と**同じキーの書き手または読み手**である他の駒。
        // 判定は grep で作った 特性 → キー の対応表（下）を機械的に当てる。
        // **`Trait` に属性を足さない**——判定の根拠が「誰かが属性を正しく付けたか」に化けて
        // grep で検算できなくなる（第48期 census の作法）。
        //
        // 出典は `BattleCore/Traits.cs` の grep:
        //   `StatusKeys.*` の `SetCounter` / `Counter` ／ `ctx.Whet` ／ `ctx.Dull` ／ `ctx.Ignite` ／
        //   `ctx.SwapSlots` ／ `OnMoved` / `OnAllyMoved` / `OnDamaged` の override。
        // **engine 側の窓口は駒に属さないのでここには入らない**（第50期の窓口一覧の裏返し）。
        // 対応表は `TraitKeyMap`（Program.cs の末尾）に1箇所だけ置いてある。
        int[] CyKeysOf(UnitDef d) => TraitKeyMap.KeysOf(d);

        Console.WriteLine("# 表D —— 単独成立度（第68期）");
        Console.WriteLine();
        Console.WriteLine($"seed 0..199 × {cyStages.Count} 波 × {cyRows.Length} 行。**寄与 = フル編成 − その1枚を抜いた版**");
        Console.WriteLine("（正なら「その駒がいると強い」。`ablate` の `差` とは符号が逆）。");
        Console.WriteLine();
        Console.WriteLine("**相方の定義（機械的・測る前に固定）**: その行で、その駒と**同じキーの書き手または読み手**である他の駒。");
        Console.WriteLine("キーの割り当ては 特性 → キー の対応表（`Traits.cs` の grep から作り、診断のローカルに置いた）。");
        Console.WriteLine("**キーを1つも持たない駒**（数値だけの駒）は相方の判定ができないので別に並べる。");
        Console.WriteLine();

        double CyWin(Formation f)
        {
            int wins = 0, n = 0;
            foreach (EnemyCatalog.Stage st in cyStages)
                for (int seed = 0; seed < 200; seed++)
                {
                    n++;
                    if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                }
            return wins * 100.0 / n;
        }

        var soloWith = new Dictionary<string, List<double>>();
        var soloAlone = new Dictionary<string, List<double>>();
        // **第141期: 箱は `Everyone`（`All ∪ Retired`）で作る。** `compare` の行にはエグ（第139期に `All` から外した）が
        // 残っているので、`cyRoster`（＝`All` のうち compare に出る駒）だけで箱を作ると `soloWith["egu"]` で落ちる。
        // **表（D-2 以降）は `cyRoster` を回すので行数は動かない**——箱が増えるだけ。
        foreach (UnitDef u in UnitCatalog.Everyone.Where(x => cyUnitRows.ContainsKey(x.Id))) { soloWith[u.Id] = new List<double>(); soloAlone[u.Id] = new List<double>(); }

        Console.WriteLine("## D-1. 行 × 駒（寄与と相方の有無）");
        Console.WriteLine();
        Console.WriteLine("| 行 | フル | 駒 | 抜いた後 | 寄与 | その駒のキー | 相方 |");
        Console.WriteLine("|---|--:|---|--:|--:|---|---|");
        foreach ((string rn, Formation f) in cyRows)
        {
            double full = CyWin(f);
            var members = f.Occupied().ToArray();
            bool first = true;
            foreach ((int slot, UnitDef d) in members)
            {
                var ab = new Formation();
                foreach ((int ms, UnitDef md) in members) if (ms != slot) ab[ms] = md;
                double contrib = full - CyWin(ab);

                int[] mine = CyKeysOf(d);
                var mates = members.Where(o => o.Def.Id != d.Id && CyKeysOf(o.Def).Intersect(mine).Any())
                                   .Select(o => o.Def.Name).ToArray();
                // **キーを1つも持たない駒も「相方なし」に数える**（指示書 §1-4 の字義どおり
                // ——同じキーの書き手／読み手である他の駒が存在しないので、定義上「なし」）。
                // ただしその「なし」は盤面の性質ではなく**キーの一覧が §1-2 の11本しか無いこと**の
                // 影から出ているので、Q4 は保守読み（キー無しを除く）と併記する。
                if (mates.Length > 0) soloWith[d.Id].Add(contrib);
                else soloAlone[d.Id].Add(contrib);

                Console.WriteLine($"| {(first ? rn : "")} | {(first ? $"{full:F1}%" : "")} | {d.Name} "
                    + $"| {full - contrib:F1}% | {contrib:+0.0;-0.0}pt "
                    + $"| {(mine.Length == 0 ? "**キー無し**" : string.Join("・", mine.Select(k => UnitTally.CarryKeys[k])))} "
                    + $"| {(mates.Length == 0 ? "**なし**" : string.Join(" / ", mates))} |");
                first = false;
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine("## D-2. 駒ごとの単独成立度");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 行数 | キー | 相方あり行 | 平均寄与 | 相方なし行 | 平均寄与 | 差（なし − あり） |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|");
        foreach (UnitDef u in cyRoster.OrderByDescending(x => soloAlone[x.Id].Count).ThenBy(x => x.Id))
        {
            var wl = soloWith[u.Id];
            var al = soloAlone[u.Id];
            int[] mk = CyKeysOf(u);
            string wm = wl.Count > 0 ? $"{wl.Average():+0.0;-0.0}pt" : "—";
            string am = al.Count > 0 ? $"{al.Average():+0.0;-0.0}pt" : "—";
            string dd = wl.Count > 0 && al.Count > 0 ? $"{al.Average() - wl.Average():+0.0;-0.0}pt" : "—";
            Console.WriteLine($"| {u.Name} | {cyUnitRows[u.Id].Count} "
                + $"| {(mk.Length == 0 ? "**無し**" : string.Join("・", mk.Select(k => UnitTally.CarryKeys[k])))} "
                + $"| {wl.Count} | {wm} | {al.Count} | {am} | {dd} |");
        }
        Console.WriteLine();

        var haveAlone = cyRoster.Where(u => soloAlone[u.Id].Count > 0).ToArray();
        var noKey = cyRoster.Where(u => CyKeysOf(u).Length == 0).ToArray();
        var haveAloneKeyed = haveAlone.Where(u => CyKeysOf(u).Length > 0).ToArray();
        var noAlone = cyRoster.Where(u => soloAlone[u.Id].Count == 0).ToArray();
        Console.WriteLine("**Q4 は2通りに読める。指示書の字義（キーを持たない駒には相方が存在しない）と、");
        Console.WriteLine("保守読み（キーが無いのは §1-2 のキー一覧が11本しか無いせいなので判定不能として除く）。**");
        Console.WriteLine();
        Console.WriteLine("| 読み | 相方なし行を持つ駒 | Q4（20 駒以上） |");
        Console.WriteLine("|---|--:|---|");
        Console.WriteLine($"| 字義（キー無しも数える） | {haveAlone.Length} | {(haveAlone.Length >= 20 ? "○" : "×")} |");
        Console.WriteLine($"| 保守（キー無しを除く） | {haveAloneKeyed.Length} | {(haveAloneKeyed.Length >= 20 ? "○" : "×")} |");
        Console.WriteLine();
        Console.WriteLine($"**相方なし行が 0 の駒（＝単独では測っていない・第69期の穴）: {noAlone.Length} 駒**");
        Console.WriteLine();
        foreach (UnitDef u in noAlone)
            Console.WriteLine($"- {u.Name}（{cyUnitRows[u.Id].Count} 行・キー: "
                + string.Join("・", CyKeysOf(u).Select(k => UnitTally.CarryKeys[k])) + "）");
        Console.WriteLine();
        Console.WriteLine($"**キーを1つも持たない駒: {noKey.Length} 駒** —— "
            + (noKey.Length == 0 ? "なし" : string.Join(" / ", noKey.Select(u => u.Name))));
        Console.WriteLine("（§1-2 のキー一覧に**回復と死が無い**ぶんがここへ落ちる。");
        Console.WriteLine("墓守・継ぎ接ぎ・胞子体は死の、継ぎ当て・置き去り・散開は回復の駒である。）");
        Console.WriteLine();
        Console.WriteLine($"所要 {cySw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ---- ここから先は 表A / 表B / 表C（走査は共通） --------------------------------------
    var cyA = CyScan(0, 200);
    double cyTa = cySw.Elapsed.TotalSeconds;

    double CyAmt(UnitDef u, int k, Dictionary<(string Row, string Unit), CyCell> data)
    {
        double sum = 0;
        long n = 0;
        foreach (string rn in cyUnitRows[u.Id]) { CyCell c = data[(rn, u.Id)]; sum += c.Amount[k]; n += c.Trials; }
        return n > 0 ? sum / n : 0;
    }
    int CyFormRows(UnitDef u, int k, string form, Dictionary<(string Row, string Unit), CyCell> data)
        => cyUnitRows[u.Id].Count(rn => CyFormOf(data[(rn, u.Id)], k) == form);

    // ---- 表C: 供給の形の一覧（`carry keys`） ---------------------------------------------
    if (cyArg == "keys")
    {
        // 経路は `Traits.cs` の grep（フックの位置）から作った静的な表で、
        // **測っているのは右2列だけ**（その供給元を含む行で、そのキーがどの形になったか）。
        // `Trait` が null の行は engine 側／敵側の供給なので、分母は全行になる。
        var cyRoutes = new (int Key, string Route, TraitId? Trait, string Holder, string Hook, string Kind)[]
        {
            (UnitTally.CarryWhet, "号令の鬨", TraitId.Rally, "鬨の号令ガン", "OnBattleStart", "開戦時一括"),
            (UnitTally.CarryWhet, "号令の溜め", TraitId.Rally, "鬨の号令ガン", "OnTurnStart", "毎ターン"),
            (UnitTally.CarryWhet, "縛め", TraitId.Bind, "縛めのクグ", "OnTurnStart", "毎ターン"),
            (UnitTally.CarryWhet, "駆り立て", TraitId.Goad, "駆り立てのカリ", "OnTurnStart", "毎ターン"),
            (UnitTally.CarryWhet, "火選り", TraitId.Favor, "火選りのヒヨ", "OnAction", "毎ターン"),
            (UnitTally.CarryWhet, "移り木", TraitId.Drifter, "移り木のシオ", "OnAllyMoved", "事象ごと（移動）"),
            (UnitTally.CarryWhet, "吐き戻し", TraitId.Colossus, "大喰らいゴルム", "ApplyDamage（engine）", "事象ごと（被弾）"),
            (UnitTally.CarryDull, "呪詛（味方漏れ）", TraitId.Curse, "呪詛官ネル", "OnBattleStart", "開戦時一括"),
            (UnitTally.CarryDull, "萎縮", TraitId.Cower, "萎縮のクビ", "OnBattleStart", "開戦時一括"),
            (UnitTally.CarryDull, "火選りの鈍り", TraitId.Favor, "火選りのヒヨ", "OnAction", "毎ターン"),
            (UnitTally.CarryDull, "突き返しのよろけ", TraitId.Shove, "突き返しのハネ", "OnMoved / OnAllyMoved", "事象ごと（移動）"),
            (UnitTally.CarryDull, "分かちのなまり", TraitId.Sharer, "分かちのドハ", "ApplyDamage（engine）", "事象ごと（被弾）"),
            (UnitTally.CarryPoison, "瘴気", TraitId.Miasma, "瘴気袋のグザ", "OnAction", "毎ターン"),
            (UnitTally.CarryPoison, "澱み（増幅）", TraitId.Amplifier, "澱みのミオ", "OnAction", "毎ターン"),
            (UnitTally.CarryPoison, "毒撃", TraitId.Venom, "毒吐きのスィド", "OnDamaged", "事象ごと（被弾）"),
            (UnitTally.CarryPoison, "疫み", TraitId.Contagion, "疫みのラウ", "OnAnyDeath", "事象ごと（死）"),
            (UnitTally.CarryBurn, "火の粉", TraitId.Cinder, "焼け残りのボルグ", "OnAfterAttack", "事象ごと（攻撃）"),
            (UnitTally.CarryBurn, "破裂の着火", TraitId.Bomber, "爆ぜるゾト", "OnDeath", "事象ごと（死）"),
            (UnitTally.CarryStun, "縛め（味方）", TraitId.Bind, "縛めのクグ", "OnTurnStart", "毎ターン"),
            (UnitTally.CarryStun, "痺れ粉", TraitId.Paralyze, "痺れ粉のトウ", "OnAfterAttack", "事象ごと（攻撃）"),
            (UnitTally.CarryStun, "責め苦", TraitId.Torment, "責め苦のシガ", "OnAfterAttack", "事象ごと（攻撃）"),
            (UnitTally.CarryStun, "抉りの怯み", TraitId.Gouge, "抉りのエグ", "OnAfterAttack", "事象ごと（攻撃）"),
            (UnitTally.CarryStun, "仇討ちの代金", TraitId.Avenge, "仇討ちのザン", "OnAllyDamaged（自分に）", "事象ごと（味方の被弾）"),
            (UnitTally.CarryStun, "断罪（敵）", null, "勇者候補 / 審問官", "OnDamaged", "事象ごと（被弾）"),
            (UnitTally.CarryMark, "囃し立て", TraitId.Marker, "囃し立てのヒサ", "OnBattleStart", "開戦時一括"),
            (UnitTally.CarryMark, "逸らし", TraitId.Divert, "逸らしのソラ", "OnTurnStart", "毎ターン"),
            (UnitTally.CarryMark, "駆り立ての標", TraitId.Goad, "駆り立てのカリ", "OnTurnStart", "毎ターン"),
            (UnitTally.CarryArmor, "砕け", TraitId.Shatter, "砕け盾のヒビ", "OnDamaged", "事象ごと（被弾）"),
            (UnitTally.CarryArmor, "鱗（自前）", TraitId.Scale, "鱗のウロ", "OnAllyDeath", "事象ごと（死）"),
            (UnitTally.CarryArmor, "引き受け", TraitId.Bear, "引き受けのウケ", "Dull（engine）", "事象ごと（弱体）"),
            (UnitTally.CarryWound, "裂き", TraitId.Rend, "裂きのキリ", "OnAfterAttack", "事象ごと（攻撃）"),
            (UnitTally.CarryWound, "刻み", TraitId.Carve, "刻みのノミ", "OnAfterAttack", "事象ごと（攻撃）"),
            (UnitTally.CarryIdle, "engine の痺れ振替", null, "engine", "行動順ループ", "事象ごと（痺れ）"),
            (UnitTally.CarryHit, "engine の被弾", null, "engine", "ApplyDamage", "事象ごと（被弾）"),
            (UnitTally.CarryMove, "喧噪", TraitId.Shuffler, "喧噪のバサ", "OnTurnStart", "毎ターン"),
            (UnitTally.CarryMove, "逃亡", TraitId.Coward, "逃亡兵セロ", "OnTurnStart", "毎ターン"),
            (UnitTally.CarryMove, "棘守り", TraitId.ThornGuard, "棘鎧のカド", "OnDamaged", "事象ごと（被弾）"),
            (UnitTally.CarryMove, "突き返し", TraitId.Shove, "突き返しのハネ", "OnMoved / OnAllyMoved", "事象ごと（移動）"),
            (UnitTally.CarryMove, "曝き（敵・第五波）", null, "告発人", "OnAfterAttack", "事象ごと（攻撃）"),
        };

        Console.WriteLine("# 表C —— 供給の形の一覧（第68期）");
        Console.WriteLine();
        Console.WriteLine($"A 帯 seed 0..199 × {cyStages.Count} 波 × {cyRows.Length} 行。");
        Console.WriteLine("経路・フック・分類は `BattleCore/Traits.cs` の grep（フックの位置）から。");
        Console.WriteLine("**測っているのは右2列だけ**——その供給元を含む行で、そのキーの形が何になったか");
        Console.WriteLine("（分母はその行の5枚ぶん。`Trait` が engine 側・敵側の経路は全行が分母）。");
        Console.WriteLine();
        Console.WriteLine("| キー | 経路 | 供給元 | フック | 分類 | 含む行 | 受け手の形 連 / 二 / 他 / 不 |");
        Console.WriteLine("|---|---|---|---|---|--:|---|");
        foreach (var rt in cyRoutes)
        {
            var rows = rt.Trait is TraitId tid
                ? cyRows.Where(b => b.F.Occupied().Any(o => o.Def.Traits.Contains(tid))).ToArray()
                : cyRows;
            int co = 0, bi = 0, el = 0, mu = 0;
            foreach ((string rn, Formation f) in rows)
                foreach ((_, UnitDef d) in f.Occupied())
                {
                    if (!cyA.TryGetValue((rn, d.Id), out CyCell? c)) continue;
                    string fm = CyFormOf(c, rt.Key);
                    if (fm == CyCont) co++;
                    else if (fm == CyBin) bi++;
                    else if (fm == CyElse) el++;
                    else mu++;
                }
            Console.WriteLine($"| {UnitTally.CarryKeys[rt.Key]} | {rt.Route} | {rt.Holder} | `{rt.Hook}` | **{rt.Kind}** "
                + $"| {rows.Length} | {co} / {bi} / {el} / {mu} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 分類ごとの集計");
        Console.WriteLine();
        Console.WriteLine("| 分類 | 経路数 | キー |");
        Console.WriteLine("|---|--:|---|");
        foreach (var g in cyRoutes.GroupBy(r => r.Kind).OrderByDescending(g => g.Count()))
            Console.WriteLine($"| {g.Key} | {g.Count()} | "
                + string.Join("・", g.Select(r => UnitTally.CarryKeys[r.Key]).Distinct()) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {cySw.Elapsed.TotalSeconds:F1} 秒。");
        return;
    }

    // ---- 表A・表B と Q1〜Q3 ---------------------------------------------------------------
    var cyB = CyScan(200, 400);
    double cyTb = cySw.Elapsed.TotalSeconds - cyTa;

    Console.WriteLine("# 棚卸し —— 条件付き変質を載せられる駒はどれか（第68期 carry）");
    Console.WriteLine();
    Console.WriteLine($"A 帯 seed 0..199 / B 帯 seed 200..599 × {cyStages.Count} 波 × {cyRows.Length} 行。");
    Console.WriteLine($"ロスター {UnitCatalog.All.Count} 体のうち compare に出る **{cyRoster.Length} 体**が対象。");
    Console.WriteLine("**盤面は1ビットも動かない**（計数だけ。`compare` は 305 セル 0 件で一致する）。");
    Console.WriteLine();
    Console.WriteLine("キーの単位: " + string.Join(" / ", Enumerable.Range(0, cyNK)
        .Select(k => $"{UnitTally.CarryKeys[k]} = {UnitTally.CarryUnits[k]}")) + "。");
    Console.WriteLine($"格子は **{string.Join(" / ", UnitTally.CarryProbes)}**（第67期の `CreakWhetProbes` と同じ9点）。");
    Console.WriteLine();

    // ---- 表A-1 --------------------------------------------------------------------------
    Console.WriteLine("## 表A-1. 駒 × キー の在庫（1戦あたりの届いた累計・A 帯）");
    Console.WriteLine();
    Console.WriteLine("`自前` は `AtkBonus` が上がった総量から窓口経由（強化）を引いた残り＝**自己強化9本の合計**。");
    Console.WriteLine("**条件の候補には入れない**（第66期: 自分で作れる値を条件にすると閾値が起動スイッチになる）。");
    Console.WriteLine("`被ダメ` は被弾の**量**（被弾のキー自体は回数）。");
    Console.WriteLine();
    Console.Write("| 駒 | 行数 | 振/戦 | 自前 | 被ダメ |");
    foreach (string k in UnitTally.CarryKeys) Console.Write($" {k} |");
    Console.WriteLine();
    Console.Write("|---|--:|--:|--:|--:|");
    foreach (string _ in UnitTally.CarryKeys) Console.Write("--:|");
    Console.WriteLine();
    foreach (UnitDef u in cyRoster.OrderBy(x => x.Id))
    {
        long tr = 0;
        double sw = 0, self = 0, tk = 0;
        foreach (string rn in cyUnitRows[u.Id])
        {
            CyCell c = cyA[(rn, u.Id)];
            tr += c.Trials; sw += c.Attacks; tk += c.Taken;
            self += c.AtkGain - c.Amount[UnitTally.CarryWhet];
        }
        Console.Write($"| {u.Name} | {cyUnitRows[u.Id].Count} | {sw / tr:F2} | {self / tr:F1} | {tk / tr:F0} |");
        for (int k = 0; k < cyNK; k++)
        {
            double v = CyAmt(u, k, cyA);
            Console.Write(v > 0 ? $" {v:F2} |" : " — |");
        }
        Console.WriteLine();
    }
    Console.WriteLine();

    // ---- 表A-2 --------------------------------------------------------------------------
    Console.WriteLine("## 表A-2. 駒 × キー の形（該当行数。`連` = 連続 / `二` = 2値 / `他` = どちらでもない）");
    Console.WriteLine();
    Console.WriteLine("**不発の行は書かない**（届いていない行がほとんどで、書くと表が読めなくなる）。");
    Console.WriteLine("セルの合計がその駒の行数に満たないぶんが不発。**A 帯の判定。**");
    Console.WriteLine();
    Console.Write("| 駒 | 行数 |");
    foreach (string k in UnitTally.CarryKeys) Console.Write($" {k} |");
    Console.WriteLine();
    Console.Write("|---|--:|");
    foreach (string _ in UnitTally.CarryKeys) Console.Write("---|");
    Console.WriteLine();
    foreach (UnitDef u in cyRoster.OrderBy(x => x.Id))
    {
        Console.Write($"| {u.Name} | {cyUnitRows[u.Id].Count} |");
        for (int k = 0; k < cyNK; k++)
        {
            int co = CyFormRows(u, k, CyCont, cyA);
            int bi = CyFormRows(u, k, CyBin, cyA);
            int el = CyFormRows(u, k, CyElse, cyA);
            var parts = new List<string>();
            if (co > 0) parts.Add($"**連{co}**");
            if (bi > 0) parts.Add($"二{bi}");
            if (el > 0) parts.Add($"他{el}");
            Console.Write(parts.Count == 0 ? " — |" : " " + string.Join(" ", parts) + " |");
        }
        Console.WriteLine();
    }
    Console.WriteLine();

    // ---- 表B ----------------------------------------------------------------------------
    Console.WriteLine("## 表B. 載せられる駒の候補（`連続` の行が 2 行以上ある 駒 × キー）");
    Console.WriteLine();
    Console.WriteLine("並びは `連続` の行数の多い順。`B帯` は同じ判定を再現帯でやり直した行数。");
    Console.WriteLine("`型` は `Def.Pattern`（**戦闘中の型ではない**——熾のホタは燃えている間だけ貫きになる）。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | キー | 連続の行数 A / B | 平均量 | 振/戦 | 型 | 含む行数 | 既存の1文（`PlusText`） |");
    Console.WriteLine("|---|---|--:|--:|--:|---|--:|---|");
    var cyCand = new List<(UnitDef U, int K, int A, int B)>();
    foreach (UnitDef u in cyRoster)
        for (int k = 0; k < cyNK; k++)
        {
            int a = CyFormRows(u, k, CyCont, cyA);
            if (a < 2) continue;
            cyCand.Add((u, k, a, CyFormRows(u, k, CyCont, cyB)));
        }
    foreach (var (u, k, a, b) in cyCand.OrderByDescending(x => x.A).ThenBy(x => x.U.Id))
    {
        long tr = 0;
        double sw = 0;
        foreach (string rn in cyUnitRows[u.Id]) { CyCell c = cyA[(rn, u.Id)]; tr += c.Trials; sw += c.Attacks; }
        Console.WriteLine($"| {u.Name} | {UnitTally.CarryKeys[k]} | **{a}** / {b} | {CyAmt(u, k, cyA):F2} "
            + $"| {sw / tr:F2} | {u.Pattern} | {cyUnitRows[u.Id].Count} | {u.PlusText} |");
    }
    Console.WriteLine();
    var cyCarriers = cyCand.Select(x => x.U.Id).Distinct().ToArray();
    Console.WriteLine($"**候補の 駒 × キー は {cyCand.Count} 組、駒の実数は {cyCarriers.Length} 体。**");
    Console.WriteLine($"**Q1 = {(cyCarriers.Length >= 3 ? "○" : "×")}**（表B の駒 {cyCarriers.Length} / 3）。");
    Console.WriteLine();

    // ---- Q2 -----------------------------------------------------------------------------
    Console.WriteLine("## Q2. 形の判定は両 seed 帯で一致するか");
    Console.WriteLine();
    int same = 0, tot = 0, sameLive = 0, totLive = 0;
    var mis = new List<string>();
    foreach (UnitDef u in cyRoster)
        foreach (string rn in cyUnitRows[u.Id])
            for (int k = 0; k < cyNK; k++)
            {
                string fa = CyFormOf(cyA[(rn, u.Id)], k);
                string fb = CyFormOf(cyB[(rn, u.Id)], k);
                tot++;
                if (fa == fb) same++;
                if (fa == CyMute && fb == CyMute) continue;
                totLive++;
                if (fa == fb) sameLive++;
                else mis.Add($"{u.Name} × {UnitTally.CarryKeys[k]} × {rn}: A={fa} / B={fb}");
            }
    Console.WriteLine("| 分母 | 一致 | 一致率 |");
    Console.WriteLine("|---|--:|--:|");
    Console.WriteLine($"| 全セル（駒 × キー × 行） | {same} / {tot} | **{same * 100.0 / tot:F1}%** |");
    Console.WriteLine($"| どちらかの帯で不発でないセル | {sameLive} / {totLive} | **{sameLive * 100.0 / totLive:F1}%** |");
    Console.WriteLine();
    Console.WriteLine($"**Q2 = {(same * 100.0 / tot >= 95.0 ? "○" : "×")}**（全セル一致率 {same * 100.0 / tot:F1}% / 95.0%）。");
    Console.WriteLine();
    Console.WriteLine($"食い違ったセル {mis.Count} 件のうち先頭 40 件:");
    Console.WriteLine();
    foreach (string m in mis.Take(40)) Console.WriteLine($"- {m}");
    Console.WriteLine();

    // ---- Q3: ヨミの再現（器具の検算） -----------------------------------------------------
    Console.WriteLine("## Q3. ヨミの再現（器具の検算）");
    Console.WriteLine();
    Console.WriteLine("第67期 7-2 の判定を、一般化した格子で引き直す。");
    Console.WriteLine("**隊列崩し = 2値 / 移動改・突き出し = 連続**が出れば器具は同じものを測っている。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 強化の形 A | 強化の形 B | 押され | " + string.Join(" | ", UnitTally.CarryProbes) + " |");
    Console.WriteLine("|---|---|---|--:|" + string.Concat(UnitTally.CarryProbes.Select(_ => "--:|")));
    foreach (string rn in cyUnitRows[UnitCatalog.Yomi.Id])
    {
        CyCell c = cyA[(rn, UnitCatalog.Yomi.Id)];
        Console.Write($"| {rn} | **{CyFormOf(c, UnitTally.CarryWhet)}** "
            + $"| {CyFormOf(cyB[(rn, UnitCatalog.Yomi.Id)], UnitTally.CarryWhet)} "
            + $"| {(double)c.Amount[UnitTally.CarryWhet] / c.Trials:F1} |");
        for (int i = 0; i < cyNP; i++)
            Console.Write($" {c.ProbeN[UnitTally.CarryWhet * cyNP + i] * 100.0 / c.Trials:F1}% |");
        Console.WriteLine();
        Console.Write("|  |  |  | 到達T |");
        for (int i = 0; i < cyNP; i++)
        {
            int n = c.ProbeN[UnitTally.CarryWhet * cyNP + i];
            Console.Write(n > 0 ? $" {c.ProbeT[UnitTally.CarryWhet * cyNP + i] / n:F2} |" : " — |");
        }
        Console.WriteLine();
    }
    Console.WriteLine();
    Console.WriteLine($"所要 A 帯 {cyTa:F1} 秒 / B 帯 {cyTb:F1} 秒。");
    Console.WriteLine();
    return;
}
}
