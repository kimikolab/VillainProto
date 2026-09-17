using BattleCore;
using static Common;

// =====================================================================================
// curse モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "curse")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 curse
// =====================================================================================

static class CurseDiag
{
// =====================================================================================
// curse モード（第95期）—— 交差表を機械で引き直し、呪いの形を選ぶ
//
// 第94期が `TraitEntryMap` の 29 件を直した。**表は分類の器具なので、第92期の
// 「交差の空白表」と「繋いでいる機構は 55 組中 12 組だけ」はその瞬間に動いている。**
// そして 12 / 55 は第93期が「交差を1本引く」と決めた根拠そのものだった。
// **根拠が動いた状態で次の機構を選ぶのは危ない**ので、選ぶ前に引き直す。
//
//     dotnet run --project BattleSim -c Release 0 curse grid    # (S1) 交差表の引き直し
//     dotnet run --project BattleSim -c Release 0 curse phase0  # (S2) 呪いの数え物
//     dotnet run --project BattleSim -c Release 0 curse pick    # (S3) 選定規則の適用
//
// **盤面は1ビットも動かない。** 観測は第94期 (T2) の印（`CounterProbe`）を**読み出すだけ**で、
// `derive` のコードは1文字も書き換えていない（指示書 §5）。
public static void Run(string[] args, int stageIndex)
{
    string cuMode = args.Length > 2 ? args[2] : "";
    var cuStages = EnemyCatalog.Stages;
    int cuNK = UnitTally.CarryKeys.Length;                   // 11
    string CuK(int k) => UnitTally.CarryKeys[k];
    var cuCompare = CompareBuilds();
    var cuCross = CrossBuilds();
    var cuRows = cuCompare.Concat(cuCross).ToArray();
    const int CuSeeds = 20;                                  // `derive scan` と同じ

    // ---------------------------------------------------------------------------------
    // 観測（第94期 (T2) の写し。**印の立て方は engine 側にあるものをそのまま使う**）。
    // ---------------------------------------------------------------------------------
    int CuKeyOf(string key) => key switch
    {
        StatusKeys.Poison => UnitTally.CarryPoison,
        StatusKeys.Burn => UnitTally.CarryBurn,
        StatusKeys.Stun => UnitTally.CarryStun,
        StatusKeys.Marked => UnitTally.CarryMark,
        StatusKeys.Armor => UnitTally.CarryArmor,
        StatusKeys.Wound => UnitTally.CarryWound,
        StatusKeys.IdleTurn => UnitTally.CarryIdle,
        StatusKeys.Deep => -1,
        _ => Array.IndexOf(UnitTally.CarryKeys, key) is int i && i >= 0 ? i : -2
    };

    // 1つの版を走らせて (供給 / 消費 / 読み) の帳簿を返す。**盤面は読むだけ。**
    (Dictionary<(TraitId T, int K, int W), (long N, long Amt)> Sup,
     Dictionary<(TraitId T, int K, int W), (long N, long Amt)> Drain,
     Dictionary<(TraitId T, int K, int W), long> Read,
     double Battles, double Secs) CuObserve(
        SoakRule? soak = null, SpillWoundRule? spill = null, GatherRule? gather = null,
        IgniteRule? ignite = null, MendRule? mend = null, SutureRule? suture = null)
    {
        var obs = new Dictionary<(TraitId, int, int, int), (long N, long Amt)>();
        void Probe(TraitId trait, UnitState owner, UnitState target, string key, int delta)
        {
            if (key == BattleContext.FriendlyBladeKey || key == BattleContext.FriendlyBladeEngineKey) return;
            int k = CuKeyOf(key);
            if (k < 0) return;                                // 深手（-1）と私有キー（-2）は 11 キーに無い
            int w = ReferenceEquals(owner, target) ? 0 : owner.TeamId == target.TeamId ? 1 : 2;
            int dir = delta == 0 ? 0 : delta > 0 ? 1 : -1;
            var ky = (trait, k, w, dir);
            var cur = obs.TryGetValue(ky, out var v) ? v : (0L, 0L);
            obs[ky] = (cur.Item1 + 1, cur.Item2 + Math.Abs((long)delta));
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var row in cuRows)
            foreach (var st in cuStages)
                for (int seed = 0; seed < CuSeeds; seed++)
                    BattleEngine.Run(row.F, st.Enemy, seed, verbose: false,
                                     suture: suture, spillWound: spill, mend: mend,
                                     woundIgnite: ignite, gather: gather, soak: soak, probe: Probe);
        sw.Stop();
        var sup = new Dictionary<(TraitId, int, int), (long N, long Amt)>();
        var drain = new Dictionary<(TraitId, int, int), (long N, long Amt)>();
        var read = new Dictionary<(TraitId, int, int), long>();
        foreach (var kv in obs)
        {
            var k = (kv.Key.Item1, kv.Key.Item2, kv.Key.Item3);
            if (kv.Key.Item4 > 0) sup[k] = kv.Value;
            else if (kv.Key.Item4 < 0) drain[k] = kv.Value;
            else read[k] = kv.Value.N;
        }
        return (sup, drain, read, cuRows.Length * cuStages.Count * (double)CuSeeds, sw.Elapsed.TotalSeconds);
    }

    // 中継の判定（第94期 (T2) と同じ定義）: 同じ特性が同じキーで増と減を書き、増 ≤ 減。
    HashSet<(TraitId, int)> CuRelays(Dictionary<(TraitId T, int K, int W), (long N, long Amt)> sup,
                                     Dictionary<(TraitId T, int K, int W), (long N, long Amt)> drain)
    {
        var s = new Dictionary<(TraitId, int), long>();
        var d = new Dictionary<(TraitId, int), long>();
        foreach (var kv in sup) { var k = (kv.Key.T, kv.Key.K); s[k] = (s.TryGetValue(k, out long a) ? a : 0) + kv.Value.Amt; }
        foreach (var kv in drain) { var k = (kv.Key.T, kv.Key.K); d[k] = (d.TryGetValue(k, out long a) ? a : 0) + kv.Value.Amt; }
        return d.Where(x => x.Value > 0 && (!s.TryGetValue(x.Key, out long sa) || sa <= x.Value))
                .Select(x => x.Key).ToHashSet();
    }

    // `Counter` を通らない4キー（読みが原理的に観測できない側）。
    var cuUnobservableRead = new[] { UnitTally.CarryWhet, UnitTally.CarryDull,
                                     UnitTally.CarryMove, UnitTally.CarryHit }.ToHashSet();

    // 供給キー／読みキー。**観測 ∪ 表** で作る。
    //
    // **観測だけでは足りない**——第94期 (T2) が「未観測 13 件のうち 7 件は engine が窓口を持つ通貨」と
    // 出したとおり、engine の段（`ApplyDamage` の中・`ctx.Poison` の中・行動順ループ）では
    // 印が降りているので、その読み書きは**どの特性にも帰属しない**。
    // のろまの手番（供給）・分かちのなまり（供給）・据えの手番（読み）・滲み則の傷（読み 4 枚）が
    // まるごとこれに当たる。**観測を正とすると軸が2本まるごと消える**ので、表で補う。
    // どちらから来たかは表の列（`観`／`表`）に出す。
    Dictionary<TraitId, HashSet<int>> CuSupObs(
        Dictionary<(TraitId T, int K, int W), (long N, long Amt)> sup, HashSet<(TraitId, int)> relay)
    {
        var m = new Dictionary<TraitId, HashSet<int>>();
        foreach (var kv in sup)
        {
            if (relay.Contains((kv.Key.T, kv.Key.K))) continue;
            if (!m.TryGetValue(kv.Key.T, out var h)) m[kv.Key.T] = h = new HashSet<int>();
            h.Add(kv.Key.K);
        }
        return m;
    }
    Dictionary<TraitId, HashSet<int>> CuReadObs(Dictionary<(TraitId T, int K, int W), long> read)
    {
        var m = new Dictionary<TraitId, HashSet<int>>();
        foreach (var kv in read)
        {
            if (!m.TryGetValue(kv.Key.T, out var h)) m[kv.Key.T] = h = new HashSet<int>();
            h.Add(kv.Key.K);
        }
        return m;
    }
    Dictionary<TraitId, HashSet<int>> CuMergeTable(Dictionary<TraitId, HashSet<int>> obs, bool supplies)
    {
        var m = obs.ToDictionary(x => x.Key, x => new HashSet<int>(x.Value));
        foreach (var kv in supplies ? TraitEntryMap.Supplies : TraitEntryMap.Reads)
            foreach ((int k, _) in kv.Value)
            {
                if (!m.TryGetValue(kv.Key, out var h)) m[kv.Key] = h = new HashSet<int>();
                h.Add(k);
            }
        return m;
    }
    Dictionary<TraitId, HashSet<int>> CuSupKeys(
        Dictionary<(TraitId T, int K, int W), (long N, long Amt)> sup, HashSet<(TraitId, int)> relay)
        => CuMergeTable(CuSupObs(sup, relay), true);
    Dictionary<TraitId, HashSet<int>> CuReadKeys(Dictionary<(TraitId T, int K, int W), long> read)
        => CuMergeTable(CuReadObs(read), false);

    // engine の盤面規則が作る交差（**キーの読み ↔ キーの書き**）。
    // **`docs/rules.md`（第94期の生成物）の 30 本を1本ずつ見て、キーを2本またぐものだけを並べた。**
    // 印が降りている段なので観測には出ない——だから**書く側の量が規則を切ると動くこと**で検算する
    // （下の 0-2）。読む側は規則の定義そのもの。
    var cuEngineBridge = new (int Read, int Write, string Name, string Knob)[]
    {
        (UnitTally.CarryWound, UnitTally.CarryPoison, "滲み則", "`SoakRule { Poison = True }`"),
        (UnitTally.CarryWound, UnitTally.CarryPoison, "傷口の着火", "`IgniteRule { Enabled = True }`"),
        (UnitTally.CarryHit,   UnitTally.CarryWound,  "巻き込み則", "`SpillWoundRule { Enabled = True }`"),
        (UnitTally.CarryHit,   UnitTally.CarryWound,  "傷の引き取り", "`GatherRule { Enabled = True }`"),
        (UnitTally.CarryHit,   UnitTally.CarryWound,  "棘の傷（**残置**）", "`ThornRule { Wound = None }`"),
    };

    // ---------------------------------------------------------------------------------
    // (S1) grid —— 交差の空白表を機械で引き直す
    // ---------------------------------------------------------------------------------
    if (cuMode.Length == 0 || cuMode == "grid")
    {
        var cuBase = CuObserve();
        double cuGB = cuBase.Battles;
        var cuBaseRelay = CuRelays(cuBase.Sup, cuBase.Drain);
        var cuSupObs = CuSupObs(cuBase.Sup, cuBaseRelay);
        var cuReadObs = CuReadObs(cuBase.Read);
        var cuBaseSup = CuMergeTable(cuSupObs, true);
        var cuBaseRead = CuMergeTable(cuReadObs, false);

        Console.WriteLine("# 第95期 (S1) —— 交差の空白表を機械で引き直す");
        Console.WriteLine();
        Console.WriteLine($"台: `compare` {cuCompare.Length} 行 ＋ 交差帯 {cuCross.Length} 行 × 全 {cuStages.Count} 波 "
                          + $"× seed 0..{CuSeeds - 1} ＝ {cuGB:N0} 戦（{cuBase.Secs:F1} 秒）。");
        Console.WriteLine();
        Console.WriteLine("**軸は 11 キー**（新しい軸の定義を作らない）。"
                          + "**書き手・読み手は第94期 (T2) の観測 ∪ `TraitEntryMap`** で作る"
                          + "——観測だけでは足りない（下の 0-0）。どちらから来たかは列に出す。");
        Console.WriteLine();

        // --- 0-0. 観測と表の食い違い ------------------------------------------------------
        Console.WriteLine("## 0-0. 観測と表の食い違い（**器具の限界を先に出す**）");
        Console.WriteLine();
        Console.WriteLine("**印（`BattleContext.Mark`）は特性の実行中にしか立たない**ので、"
                          + "engine の段で起きる読み書きはどの特性にも帰属しない。"
                          + "**観測を正としてしまうと、手番の書き手が 0 枚になって軸が1本消える。**");
        Console.WriteLine();
        Console.WriteLine("| 種別 | 観測にあり表に無い | 表にあり観測に無い |");
        Console.WriteLine("|---|--:|--:|");
        int cuSupObsOnly = cuSupObs.Sum(kv => kv.Value.Count(k =>
            !(TraitEntryMap.Supplies.TryGetValue(kv.Key, out var t) && t.Any(x => x.Key == k))));
        int cuSupTabOnly = TraitEntryMap.Supplies.Sum(kv => kv.Value.Select(x => x.Key).Distinct()
            .Count(k => !(cuSupObs.TryGetValue(kv.Key, out var o) && o.Contains(k))));
        int cuReadObsOnly = cuReadObs.Sum(kv => kv.Value.Count(k =>
            !(TraitEntryMap.Reads.TryGetValue(kv.Key, out var t) && t.Any(x => x.Key == k))));
        int cuReadTabOnly = TraitEntryMap.Reads.Sum(kv => kv.Value.Select(x => x.Key).Distinct()
            .Count(k => !(cuReadObs.TryGetValue(kv.Key, out var o) && o.Contains(k))));
        Console.WriteLine($"| 供給（特性 × キー） | {cuSupObsOnly} | {cuSupTabOnly} |");
        Console.WriteLine($"| 読み（特性 × キー） | {cuReadObsOnly} | {cuReadTabOnly} |");
        Console.WriteLine();
        Console.WriteLine("| 特性 | キー | 種別 | 側 | 理由 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var kv in TraitEntryMap.Supplies.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
            foreach (int k in kv.Value.Select(x => x.Key).Distinct().OrderBy(x => x))
                if (!(cuSupObs.TryGetValue(kv.Key, out var o) && o.Contains(k)))
                    Console.WriteLine($"| `{kv.Key}` | {CuK(k)} | 供給 | **表のみ** | 台に居ないか、engine の段で書いている |");
        foreach (var kv in TraitEntryMap.Reads.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
            foreach (int k in kv.Value.Select(x => x.Key).Distinct().OrderBy(x => x))
                if (!(cuReadObs.TryGetValue(kv.Key, out var o) && o.Contains(k)))
                    Console.WriteLine($"| `{kv.Key}` | {CuK(k)} | 読み | **表のみ** "
                        + $"| {(cuUnobservableRead.Contains(k) ? "**観測範囲外**（`Counter` を通らない）" : "台に居ないか、engine の段で読んでいる")} |");
        Console.WriteLine();

        // --- 0-1. 軸の書き手 --------------------------------------------------------------
        HashSet<int> CuRowAxes(Formation f)
        {
            var s = new HashSet<int> { UnitTally.CarryHit };   // 被弾は敵が供給するので全行が持つ
            foreach ((_, UnitDef d) in f.Occupied())
                foreach (TraitId t in d.Traits)
                    if (cuBaseSup.TryGetValue(t, out var h)) foreach (int k in h) s.Add(k);
            return s;
        }
        var cuCmpAx = cuCompare.Select(r => CuRowAxes(r.F)).ToArray();
        var cuCrAx = cuCross.Select(r => CuRowAxes(r.F)).ToArray();

        Console.WriteLine("## 0-1. 軸の書き手（中継を除く。`観`＝観測された・`表`＝表にだけある）");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 書き手（特性） | `compare` 61行 | 交差帯12行 |");
        Console.WriteLine("|---|---|--:|--:|");
        for (int k = 0; k < cuNK; k++)
        {
            var ws = cuBaseSup.Where(x => x.Value.Contains(k))
                .Select(x => $"`{x.Key}`{(cuSupObs.TryGetValue(x.Key, out var o) && o.Contains(k) ? "" : "（表）")}")
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Console.WriteLine($"| {CuK(k)} | {(ws.Length == 0 ? "**0 枚**（敵が供給）" : string.Join("・", ws))} "
                              + $"| {cuCmpAx.Count(s => s.Contains(k))} | {cuCrAx.Count(s => s.Contains(k))} |");
        }
        Console.WriteLine();

        // --- 0-2. engine の規則の検算 -----------------------------------------------------
        Console.WriteLine("## 0-2. engine の規則が作る交差（**書く側の量を切って検算する**）");
        Console.WriteLine();
        Console.WriteLine("engine の段は印が降りているので**観測に出ない**。だから"
                          + "「その規則を切ると、書く側のキーの量が動くか」で実在を確かめる。"
                          + "**読む側は規則の定義そのもの**（`docs/rules.md` の 30 本を1本ずつ見た）。");
        Console.WriteLine();
        double CuAmtOf(Dictionary<(TraitId T, int K, int W), (long N, long Amt)> sup, int key)
            => sup.Where(x => x.Key.K == key).Sum(x => (double)x.Value.Amt);
        Console.WriteLine("| 規則 | ノブ | 読む | 書く | 既定の 量/戦 | 切ると 量/戦 | 差 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|");
        var cuEngOff = new (string Knob, Func<(Dictionary<(TraitId T, int K, int W), (long N, long Amt)> Sup,
                                               Dictionary<(TraitId T, int K, int W), (long N, long Amt)> Drain,
                                               Dictionary<(TraitId T, int K, int W), long> Read,
                                               double Battles, double Secs)> Run)[]
        {
            ("`SoakRule { Poison = True }`", () => CuObserve(soak: new SoakRule(false, false))),
            ("`IgniteRule { Enabled = True }`", () => CuObserve(ignite: new IgniteRule(false))),
            ("`SpillWoundRule { Enabled = True }`", () => CuObserve(spill: new SpillWoundRule(false))),
            ("`GatherRule { Enabled = True }`", () => CuObserve(gather: new GatherRule(false))),
        };
        var cuEngAmt = new Dictionary<string, Dictionary<(TraitId T, int K, int W), (long N, long Amt)>>();
        foreach (var e in cuEngOff) cuEngAmt[e.Knob] = e.Run().Sup;
        foreach (var b in cuEngineBridge)
        {
            double a = CuAmtOf(cuBase.Sup, b.Write) / cuGB;
            string off = "—", diff = "**切れない**（既定が既に off）";
            if (cuEngAmt.TryGetValue(b.Knob, out var s2))
            {
                double v = CuAmtOf(s2, b.Write) / cuGB;
                off = $"{v:F2}"; diff = $"**{v - a:+0.00;-0.00;0.00}**";
            }
            Console.WriteLine($"| {b.Name} | {b.Knob} | {CuK(b.Read)} | {CuK(b.Write)} | {a:F2} | {off} | {diff} |");
        }
        Console.WriteLine();
        Console.WriteLine("**差は「その規則を切ると盤面がどう動いたか」であって、規則が書いた量そのものではない**"
                          + "——engine の段の書き込みは印が降りていて観測に出ないので、"
                          + "ここで数えているのは**特性に帰属した書き込みの総量の変化**である。"
                          + "**引き取り（`GatherRule`）は中継**（第94期）なので盤面の総量を増やさない"
                          + "——それでも差が出るのは、傷の行き先が変わって戦闘そのものが動くため。");
        Console.WriteLine();
        Console.WriteLine("**engine の規則が作る交差は 2 組**"
                          + $"（{CuK(UnitTally.CarryWound)} × {CuK(UnitTally.CarryPoison)} と "
                          + $"{CuK(UnitTally.CarryHit)} × {CuK(UnitTally.CarryWound)}）。"
                          + "**30 本のノブのうち、キーを2本またぐのはこの5本だけ**"
                          + "——残りは1本のキーしか触らないか（`YokeRule` / `HushRule` / `MartyrRule` …）、"
                          + "特性の側に本体がある（`BearRule` / `RelayRule` / `FavorRule` …）。");
        Console.WriteLine();

        // --- 交差の集計 -------------------------------------------------------------------
        var cuBridge = new Dictionary<(int, int), List<(string What, bool Engine)>>();
        void CuAdd(int a, int b, string what, bool eng)
        {
            if (a == b) return;
            var p = a < b ? (a, b) : (b, a);
            if (!cuBridge.TryGetValue(p, out var l)) cuBridge[p] = l = new List<(string, bool)>();
            if (!l.Any(x => x.What == what)) l.Add((what, eng));
        }
        var cuTraitPairs = new Dictionary<TraitId, List<(int A, int B)>>();
        foreach (TraitId t in Enum.GetValues<TraitId>())
        {
            if (!cuBaseRead.TryGetValue(t, out var rr) || !cuBaseSup.TryGetValue(t, out var ss)) continue;
            var l = new List<(int A, int B)>();
            foreach (int r in rr) foreach (int s2 in ss)
            {
                if (r == s2) continue;
                var p = r < s2 ? (r, s2) : (s2, r);
                if (!l.Contains(p)) l.Add(p);
                CuAdd(r, s2, $"`{t}`", false);
            }
            if (l.Count > 0) cuTraitPairs[t] = l;
        }
        foreach (var b in cuEngineBridge) CuAdd(b.Read, b.Write, $"{b.Name} {b.Knob}", true);

        Console.WriteLine("## 表A —— 交差の空白表（55 組）");
        Console.WriteLine();
        Console.WriteLine("`61行` はその組の両方の軸の書き手がいる `compare` の行数、`12行` は交差帯。");
        Console.WriteLine();
        Console.WriteLine("| 組 | 61行 | 12行 | 機構（特性由来） | 機構（engine 由来） |");
        Console.WriteLine("|---|--:|--:|---|---|");
        int cuBridged = 0, cuBlank = 0, cuBlankBridged = 0;
        var cuBlankList = new List<(int A, int B)>();
        var cuBridgeList = new List<(int A, int B, int Rows, int XRows, string Trait, string Eng)>();
        for (int a = 0; a < cuNK; a++) for (int b = a + 1; b < cuNK; b++)
        {
            int n = cuCmpAx.Count(s => s.Contains(a) && s.Contains(b));
            int xn = cuCrAx.Count(s => s.Contains(a) && s.Contains(b));
            var l = cuBridge.TryGetValue((a, b), out var ll) ? ll : new List<(string What, bool Engine)>();
            string tr = string.Join(" / ", l.Where(x => !x.Engine).Select(x => x.What));
            string en = string.Join(" / ", l.Where(x => x.Engine).Select(x => x.What));
            if (l.Count > 0) cuBridged++;
            if (n == 0) { cuBlank++; cuBlankList.Add((a, b)); if (l.Count > 0) cuBlankBridged++; }
            cuBridgeList.Add((a, b, n, xn, tr, en));
            Console.WriteLine($"| {(l.Count > 0 ? "**" : "")}{CuK(a)} × {CuK(b)}{(l.Count > 0 ? "**" : "")} "
                              + $"| {(n == 0 ? "**0**" : n.ToString())} | {xn} "
                              + $"| {(tr.Length == 0 ? "—" : tr)} | {(en.Length == 0 ? "—" : en)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**繋いでいる機構が実在する組: {cuBridged} / 55**"
                          + $"（第92期は **12 / 55**）。");
        Console.WriteLine($"**`compare` 61 行に1行も無い組（空白）: {cuBlank} / 55**"
                          + $"（第92期は **12 / 55**）。**うち機構がある組: {cuBlankBridged}**"
                          + "（第92期は **1**＝毒×傷）。");
        Console.WriteLine();
        Console.WriteLine("| # | 空白の組 | 交差帯12行 | 繋いでいる機構 |");
        Console.WriteLine("|--:|---|--:|---|");
        for (int i = 0; i < cuBlankList.Count; i++)
        {
            var p = cuBlankList[i];
            var l = cuBridge.TryGetValue(p, out var ll) ? ll : new List<(string What, bool Engine)>();
            int xn = cuCrAx.Count(s => s.Contains(p.A) && s.Contains(p.B));
            Console.WriteLine($"| {i + 1} | {CuK(p.A)} × {CuK(p.B)} | {xn} "
                              + $"| {(l.Count == 0 ? "**無し**" : string.Join(" / ", l.Select(x => x.What)))} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表A-2 —— 機構がある組の一覧（第92期の 12 組と突き合わせる材料）");
        Console.WriteLine();
        Console.WriteLine("| # | 組 | 61行 | 12行 | 機構 |");
        Console.WriteLine("|--:|---|--:|--:|---|");
        int cuRank = 0;
        foreach (var p in cuBridgeList.Where(x => x.Trait.Length > 0 || x.Eng.Length > 0)
                                      .OrderByDescending(x => x.Rows))
            Console.WriteLine($"| {++cuRank} | **{CuK(p.A)} × {CuK(p.B)}** | {p.Rows} | {p.XRows} "
                              + $"| {string.Join(" / ", new[] { p.Trait, p.Eng }.Where(x => x.Length > 0))} |");
        Console.WriteLine();

        Console.WriteLine("## 表A-3 —— 1枚で軸をまたぐ特性の全数");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 保持者（ロスター） | 供給キー | 読みキー | 作る交差 |");
        Console.WriteLine("|---|---|---|---|---|");
        var cuAll51g = UnitCatalog.All.ToArray();
        foreach (var kv in cuTraitPairs.OrderByDescending(x => x.Value.Count)
                                       .ThenBy(x => x.Key.ToString(), StringComparer.Ordinal))
        {
            var holders = cuAll51g.Where(d => d.Traits.Contains(kv.Key)).Select(d => d.Name).ToArray();
            Console.WriteLine($"| `{kv.Key}` | {(holders.Length == 0 ? "**居ない**" : string.Join("・", holders))} "
                + $"| {string.Join("・", (cuBaseSup.TryGetValue(kv.Key, out var sh) ? sh : new HashSet<int>()).OrderBy(x => x).Select(CuK))} "
                + $"| {string.Join("・", (cuBaseRead.TryGetValue(kv.Key, out var rh) ? rh : new HashSet<int>()).OrderBy(x => x).Select(CuK))} "
                + $"| {string.Join(" / ", kv.Value.Select(p => $"{CuK(p.A)}×{CuK(p.B)}"))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**{cuTraitPairs.Count} 枚**が1枚で軸をまたぐ。");
        Console.WriteLine();
        return;
    }
    // ---------------------------------------------------------------------------------
    // 共通: 特性が上書きしているフックを **reflection で** 引く（手で写した表を使わない）。
    // 周期は「開戦時1回 / 毎ターン / 事象ごと」の3値で、フックの位置から決まる。
    // ---------------------------------------------------------------------------------
    string[] CuHooksOf(TraitId id)
    {
        Trait t;
        try { t = TraitCatalog.Get(id); } catch { return Array.Empty<string>(); }
        Type bt = typeof(Trait);
        var names = new[] { "OnBattleStart", "OnTurnStart", "OnAction", "OnAfterAttack", "OnDamaged",
                            "OnKill", "OnAllyDeath", "OnAllyDamaged", "OnAnyDeath", "OnDeath",
                            "OnMoved", "OnAllyMoved", "CanAct", "CanReact", "ModifyAttack",
                            "ModifyPattern", "ModifyIncomingDamage" };
        var hit = new List<string>();
        foreach (string n in names)
        {
            var mi = t.GetType().GetMethods().FirstOrDefault(m => m.Name == n && m.IsVirtual);
            if (mi is not null && mi.DeclaringType != bt) hit.Add(n);
        }
        return hit.ToArray();
    }
    string CuPeriodOf(TraitId id)
    {
        var h = CuHooksOf(id);
        if (h.Length == 0) return "**engine**（フックを1つも上書きしない札）";
        if (h.Contains("OnTurnStart") || h.Contains("OnAction")) return "毎ターン";
        if (h.Length == 1 && h[0] == "OnBattleStart") return "**開戦時1回**";
        if (h.Contains("OnBattleStart")) return "開戦時1回 ＋ 事象ごと";
        return "事象ごと";
    }

    // ---------------------------------------------------------------------------------
    // (S2) phase0 —— 呪いの数え物（**結論を出さない**）
    // ---------------------------------------------------------------------------------
    if (cuMode == "phase0")
    {
        var cuP0 = CuObserve();
        var cuP0Relay = CuRelays(cuP0.Sup, cuP0.Drain);
        double cuB = cuP0.Battles;

        Console.WriteLine("# 第95期 (S2) —— 呪いの Phase 0（数え物だけ。結論を出さない）");
        Console.WriteLine();
        Console.WriteLine($"台: `compare` {cuCompare.Length} 行 ＋ 交差帯 {cuCross.Length} 行 × 全 {cuStages.Count} 波 "
                          + $"× seed 0..{CuSeeds - 1} ＝ {cuB:N0} 戦（{cuP0.Secs:F1} 秒）。");
        Console.WriteLine();

        // --- 1. §0-2 の4点を実装で確認する -------------------------------------------------
        Console.WriteLine("## 1. 指示書 §0-2 の4点を実装で確認する");
        Console.WriteLine();
        var cuStatusFields = typeof(StatusKeys)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string) && f.IsLiteral)
            .Select(f => (f.Name, Val: (string)(f.GetRawConstantValue() ?? "")))
            .OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        Console.WriteLine($"**(1) `StatusKeys` の定数は {cuStatusFields.Length} 本**"
                          + $"／ `StatusKeys.All` は **{StatusKeys.All.Length} 本**"
                          + $"（{string.Join(" / ", StatusKeys.All.Select(k => $"`{k}`"))}）。");
        Console.WriteLine();
        Console.WriteLine("| 定数 | 値 | `All` に載る |");
        Console.WriteLine("|---|---|:-:|");
        foreach (var f in cuStatusFields)
            Console.WriteLine($"| `{f.Name}` | `{f.Val}` | {(StatusKeys.All.Contains(f.Val) ? "○" : "")} |");
        Console.WriteLine();
        bool cuHasCurseKey = cuStatusFields.Any(f => f.Name.Contains("Curse") || f.Val.Contains("curse"));
        Console.WriteLine($"**呪いのキーは {(cuHasCurseKey ? "**ある**" : "無い")}。**"
                          + "**指示書は7本と書いているが実際は8本**——第93期の `Deep`（深手）が入っている"
                          + "（`DeepRule` は既定 `false` だが、キーは `All` に載っている）。");
        Console.WriteLine();

        var cuAll51 = UnitCatalog.All.ToArray();
        var cuFoes = cuStages.SelectMany(s => s.Enemy.Occupied().Select(x => x.Def))
                             .GroupBy(d => d.Id).Select(g => g.First()).ToArray();
        string CuHolders(TraitId t)
        {
            var a = cuAll51.Where(d => d.Traits.Contains(t)).Select(d => d.Name).ToArray();
            var f = cuFoes.Where(d => d.Traits.Contains(t)).Select(d => d.Name).ToArray();
            return (a.Length == 0 ? "—" : string.Join("・", a)) + (f.Length == 0 ? "" : $" ／ 敵: {string.Join("・", f)}");
        }
        Console.WriteLine($"**(2) `TraitId.Curse`（呪詛）の保持者**: {CuHolders(TraitId.Curse)}。"
                          + $"フックは {string.Join(" / ", CuHooksOf(TraitId.Curse).Select(h => $"`{h}`"))}"
                          + $"（周期 = {CuPeriodOf(TraitId.Curse)}）で、書いているのは"
                          + $"**なまり（`Dull`）**——`EnemyDebuff` = {CurseTrait.EnemyDebuff} / "
                          + $"`AllyLeak` = {CurseTrait.AllyLeak}。**状態異常は1つも書かない。**");
        Console.WriteLine();
        Console.WriteLine($"**(3) 泥人形ムド**: `Traits` = "
                          + $"{string.Join(" / ", UnitCatalog.Mudo.Traits.Select(t => $"`{t}`"))}"
                          + $"（HP {UnitCatalog.Mudo.MaxHp} / 攻 {UnitCatalog.Mudo.Attack} / 速 {UnitCatalog.Mudo.Speed}）。"
                          + "**呪いとは無関係**。指示書の言うとおり「ムドの呪い」は未実装の構想。");
        Console.WriteLine();

        // --- 2. なまり（Dull）の書き手と読み手の全数 --------------------------------------
        Console.WriteLine("## 2. 表C-1 —— なまり（`Dull`）の書き手の全数（**観測 ＋ 周期**）");
        Console.WriteLine();
        Console.WriteLine("`DullRoute` の列挙（計数の正本）と、観測された書き込みを突き合わせる。"
                          + "**周期はフックの上書きを reflection で引いた**（手で写していない）。");
        Console.WriteLine();
        // 経路別の量は **`BattleResult.DullByRoute`（計数の正本）** から取る
        // ——印は特性の単位なので、同じ特性が2本の経路を持つ呪詛（敵側／味方漏れ）を割れない。
        var cuDullRoute = new double[DullRoutes.Count];
        var cuDullTaken = new double[DullRoutes.Count];
        foreach (var row in cuRows)
            foreach (var st in cuStages)
                for (int seed = 0; seed < CuSeeds; seed++)
                {
                    var rr = BattleEngine.Run(row.F, st.Enemy, seed, verbose: false);
                    for (int r = 0; r < DullRoutes.Count; r++)
                    {
                        cuDullRoute[r] += rr.DullByRoute[r];
                        cuDullTaken[r] += rr.DullTakenByRoute[r];
                    }
                }
        Console.WriteLine("| 経路 | 特性 | 保持者 | 周期 | 量/戦 | うち横取り |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        var cuDullTraitOf = new Dictionary<string, TraitId?>
        {
            ["その他"] = null,
            ["なまり"] = TraitId.Sharer,
            ["呪詛敵"] = TraitId.Curse,
            ["呪詛漏れ"] = TraitId.Curse,
            ["突き返し"] = TraitId.Shove,
            ["萎縮"] = TraitId.Cower,
            ["渡し"] = TraitId.Relay,
            ["誹り"] = TraitId.Slander,
            ["驕り"] = TraitId.Overbear,
            ["火選り"] = TraitId.Favor,
        };
        for (int r = 0; r < DullRoutes.Count; r++)
        {
            string rname = DullRoutes.Names[r];
            TraitId? t = cuDullTraitOf.TryGetValue(rname, out var tt) ? tt : null;
            Console.WriteLine($"| {rname} | {(t is null ? "—" : $"`{t}`")} "
                              + $"| {(t is null ? "—" : CuHolders(t.Value))} "
                              + $"| {(t is null ? "—" : CuPeriodOf(t.Value))} "
                              + $"| {cuDullRoute[r] / cuB:F2} | {cuDullTaken[r] / cuB:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**`DullRoute` は {DullRoutes.Count} 本**（`Other` を含む）。"
                          + "**同じ特性が2本の経路を持つのは呪詛だけ**（敵側と味方漏れ）。");
        Console.WriteLine();

        Console.WriteLine("## 表C-2 —— なまりの読み手の全数");
        Console.WriteLine();
        Console.WriteLine("**弱体の読みは `Counter` を通らない**（`AtkBonus` はカウンタではない）ので観測できない。"
                          + "ここだけは表（`TraitEntryMap.Reads`）から引く——**指示書の「読み手は熊の横取り」は不足**。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 保持者 | 場所 | 周期 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var kv in TraitEntryMap.Reads.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
            foreach ((int k, TraitEntryMap.Where w) in kv.Value)
            {
                if (k != UnitTally.CarryDull) continue;
                Console.WriteLine($"| `{kv.Key}` | {CuHolders(kv.Key)} | {w} | {CuPeriodOf(kv.Key)} |");
            }
        Console.WriteLine();

        // --- 3. 減算の読み手の全数 ---------------------------------------------------------
        Console.WriteLine("## 表C-3 —— 「他の汚れが消える」経路の全数（**観測**。形3の供給量そのもの）");
        Console.WriteLine();
        Console.WriteLine("観測のうち **`delta < 0`**（＝カウンタを減らした）ものを全部並べる。"
                          + "**中継**（同じ特性が同じキーで増と減を書き、増 ≤ 減）は印を付けてある"
                          + "——中継は盤面から汚れを消していない。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 保持者 | キー | 場所 | 周期 | 回/戦 | 量/戦 | 中継 |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|:-:|");
        double cuDrainTotal = 0, cuDrainNonRelay = 0;
        string[] cuWhereName = { "自分", "味方", "敵" };
        foreach (var kv in cuP0.Drain.OrderByDescending(x => x.Value.N))
        {
            bool relay = cuP0Relay.Contains((kv.Key.T, kv.Key.K));
            cuDrainTotal += kv.Value.N / cuB;
            if (!relay) cuDrainNonRelay += kv.Value.N / cuB;
            Console.WriteLine($"| `{kv.Key.T}` | {CuHolders(kv.Key.T)} | {CuK(kv.Key.K)} | {cuWhereName[kv.Key.W]} "
                              + $"| {CuPeriodOf(kv.Key.T)} | {kv.Value.N / cuB:F2} | {kv.Value.Amt / cuB:F2} "
                              + $"| {(relay ? "**中継**" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**減算は {cuP0.Drain.Count} 組 ＝ 合計 {cuDrainTotal:F2} 回/戦"
                          + $"（中継を除くと {cuDrainNonRelay:F2} 回/戦）。**");
        Console.WriteLine();

        // --- 4. Stoic が何を弾くか ---------------------------------------------------------
        Console.WriteLine("## 4. `Stoic`（ガルド）が何を弾くか");
        Console.WriteLine();
        Console.WriteLine("`BlocksSupport` を持つ特性を全数（reflection）で引き、**ガルドに実際に届いた量を観測で数える**。");
        Console.WriteLine();
        var cuBlockers = Enum.GetValues<TraitId>()
            .Where(t => { try { return TraitCatalog.Get(t).BlocksSupport; } catch { return false; } }).ToArray();
        Console.WriteLine($"`BlocksSupport` が真の特性: {string.Join(" / ", cuBlockers.Select(t => $"`{t}`"))}"
                          + $"（保持者: {string.Join(" ／ ", cuBlockers.Select(CuHolders))}）。");
        Console.WriteLine();
        // ガルドに届いた量を観測する（別の走査。**印は駒ではなく特性なので、受け手側を数え直す**）。
        var cuToStoic = new double[cuNK];
        var cuToOther = new double[cuNK];
        void CuStoicProbe(TraitId trait, UnitState owner, UnitState target, string key, int delta)
        {
            if (delta <= 0) return;
            int k = CuKeyOf(key);
            if (k < 0) return;
            if (!target.AcceptsSupport) cuToStoic[k] += delta; else cuToOther[k] += delta;
        }
        foreach (var row in cuRows)
            foreach (var st in cuStages)
                for (int seed = 0; seed < CuSeeds; seed++)
                    BattleEngine.Run(row.F, st.Enemy, seed, verbose: false, probe: CuStoicProbe);
        Console.WriteLine("| キー | `AcceptsSupport` が偽の駒へ 量/戦 | それ以外へ 量/戦 | 偽の駒の取り分 |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int k = 0; k < cuNK; k++)
        {
            double a = cuToStoic[k] / cuB, b = cuToOther[k] / cuB;
            Console.WriteLine($"| {CuK(k)} | {a:F2} | {b:F2} | {(a + b > 0 ? $"{a * 100 / (a + b):F1}%" : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine("**`AcceptsSupport` を見るかどうかは呼び出し側が決めている**（第42期からの持ち越し。"
                          + "窓口では揃えていない）ので、**新しい汚れを作るなら「見る／見ない」をその場で決めることになる。**");
        Console.WriteLine();

        // --- 5. 業（Scapegoat）の分母 ------------------------------------------------------
        Console.WriteLine("## 5. 業（`Scapegoat`）の `Kinds` の分母");
        Console.WriteLine();
        Console.WriteLine($"`StatusKeys.All` = {StatusKeys.All.Length} 本 → `ScapegoatTrait.Kinds` = "
                          + $"**{ScapegoatTrait.Kinds.Length} 本**"
                          + $"（{string.Join(" / ", ScapegoatTrait.Kinds.Select(k => $"`{k}`"))}）。"
                          + "**除外を並べる形なので、キーを1本足すと分母は自動で +1 になる。**");
        Console.WriteLine();
        bool cuSgRoster = cuAll51.Any(d => d.Traits.Contains(TraitId.Scapegoat));
        bool cuSgFoe = cuFoes.Any(d => d.Traits.Contains(TraitId.Scapegoat));
        Console.WriteLine($"業の保持者: ロスター（`UnitCatalog.All` {cuAll51.Length} 体）に "
                          + $"**{(cuSgRoster ? "居る" : "居ない")}** ／ 敵の波（`Stages`）に "
                          + $"**{(cuSgFoe ? "居る" : "居ない")}**。"
                          + $"{(cuSgRoster || cuSgFoe ? "**盤面が動く。**" : "**したがって分母が動いても盤面への影響は無い**（診断 `scapegoat` のローカル台だけが動く）。")}");
        Console.WriteLine();
        return;
    }
    // ---------------------------------------------------------------------------------
    // (S3) pick —— 形を選ぶ（**選定規則は測る前に固定してある。指示書 §3-2 をそのまま順に当てる**）
    // ---------------------------------------------------------------------------------
    if (cuMode == "pick")
    {
        // 汚れ ＝ 「駒に載る負の通貨」。**なまり（弱体）を含める**（指示書 §3-1 の形1 の一覧どおり）。
        int[] cuDirt = { UnitTally.CarryPoison, UnitTally.CarryBurn, UnitTally.CarryStun,
                         UnitTally.CarryMark, UnitTally.CarryArmor, UnitTally.CarryWound,
                         UnitTally.CarryDull };
        // 種類数を数えられるのは `Counter` を通る 6 キーだけ（なまりはカウンタではない）。
        string[] cuDirtCounter = { StatusKeys.Poison, StatusKeys.Burn, StatusKeys.Stun,
                                   StatusKeys.Marked, StatusKeys.Armor, StatusKeys.Wound };

        // --- 供給の観測（engine 帰属つき）------------------------------------------------
        // 「engine 由来」は grid と同じ定義: その規則を切ると1度も観測されない (特性, キー) の書き込み。
        var cuPickVers = new (string Name, Func<(Dictionary<(TraitId T, int K, int W), (long N, long Amt)> Sup,
                                                 Dictionary<(TraitId T, int K, int W), (long N, long Amt)> Drain,
                                                 Dictionary<(TraitId T, int K, int W), long> Read,
                                                 double Battles, double Secs)> Run)[]
        {
            ("既定", () => CuObserve()),
            ("滲み則 `SoakRule`", () => CuObserve(soak: new SoakRule(false, false))),
            ("巻き込み則 `SpillWoundRule`", () => CuObserve(spill: new SpillWoundRule(false))),
            ("引き取り `GatherRule`", () => CuObserve(gather: new GatherRule(false))),
            ("着火 `IgniteRule`", () => CuObserve(ignite: new IgniteRule(false))),
        };
        var cuPk = cuPickVers[0].Run();
        double cuB2 = cuPk.Battles;
        var cuPkRelay = CuRelays(cuPk.Sup, cuPk.Drain);
        var cuEngineSup = new Dictionary<(TraitId, int), string>();
        for (int v = 1; v < cuPickVers.Length; v++)
        {
            var r = cuPickVers[v].Run();
            var seen = r.Sup.Keys.Select(x => (x.T, x.K)).ToHashSet();
            foreach (var k in cuPk.Sup.Keys.Select(x => (x.T, x.K)).Distinct())
                if (!seen.Contains(k))
                    cuEngineSup[k] = cuEngineSup.TryGetValue(k, out string? o)
                        ? o + " / " + cuPickVers[v].Name : cuPickVers[v].Name;
        }

        // 汚れの書き込み（回/戦）を 駒由来 / engine 由来 に割る。
        //
        // **観測（印）だけでは engine の段が 0 に見える**——印は特性の実行中にしか立たないので、
        // `ApplyDamage` の中の巻き込み則も行動順ループも「誰の書き込みでもない」ことになる。
        // そこで**全数は `UnitTally.CarryCount`（窓口 `NoteCarry` の唯一の帳簿）**から取り、
        // **特性に帰属した分を引いた残りを engine 由来**とする。
        var cuDirtTotalByKey = new double[cuNK];
        foreach (var row in cuRows)
            foreach (var st in cuStages)
                for (int seed = 0; seed < CuSeeds; seed++)
                {
                    var rr = BattleEngine.Run(row.F, st.Enemy, seed, verbose: false);
                    foreach (var t in rr.TallyByUnit.Values)
                        if (t.CarryCount is not null)
                            for (int k = 0; k < cuNK; k++) cuDirtTotalByKey[k] += t.CarryCount[k];
                }
        for (int k = 0; k < cuNK; k++) cuDirtTotalByKey[k] /= cuB2;
        var cuDirtByKey = new double[cuNK];          // 特性に帰属した書き込み（観測）
        foreach (var kv in cuPk.Sup)
            if (cuDirt.Contains(kv.Key.K)) cuDirtByKey[kv.Key.K] += kv.Value.N / cuB2;
        var cuDirtEngineByKey = new double[cuNK];
        double cuDirtWritesUnit = 0, cuDirtWritesEngine = 0;
        foreach (int k in cuDirt)
        {
            double eng = Math.Max(0, cuDirtTotalByKey[k] - cuDirtByKey[k]);
            cuDirtEngineByKey[k] = eng;
            cuDirtWritesUnit += cuDirtByKey[k];
            cuDirtWritesEngine += eng;
        }
        // 減算（中継を除く）。形3 の供給。
        double cuDrainUnit = 0, cuDrainEngine = 0;
        var cuDrainKeys = new HashSet<int>();
        foreach (var kv in cuPk.Drain)
        {
            if (cuPkRelay.Contains((kv.Key.T, kv.Key.K))) continue;
            double n = kv.Value.N / cuB2;
            cuDrainKeys.Add(kv.Key.K);
            if (cuEngineSup.ContainsKey((kv.Key.T, kv.Key.K))) cuDrainEngine += n; else cuDrainUnit += n;
        }

        // --- 汚れの種類数（形1 の倍率）と分母 ---------------------------------------------
        double cuKindSum = 0, cuKindN = 0, cuKindMax = 0;
        var cuKindHist = new double[cuDirtCounter.Length + 1];
        void CuKindProbe(TraitId trait, UnitState owner, UnitState target, string key, int delta)
        {
            if (delta <= 0) return;
            int k = CuKeyOf(key);
            if (k < 0 || !cuDirt.Contains(k)) return;
            int kinds = cuDirtCounter.Count(x => target.Counter(x) > 0);
            cuKindSum += kinds; cuKindN++;
            if (kinds > cuKindMax) cuKindMax = kinds;
            cuKindHist[Math.Min(kinds, cuDirtCounter.Length)]++;
        }
        double cuTaken = 0, cuTurns = 0, cuN = 0;
        foreach (var row in cuRows)
            foreach (var st in cuStages)
                for (int seed = 0; seed < CuSeeds; seed++)
                {
                    var res = BattleEngine.Run(row.F, st.Enemy, seed, verbose: false, probe: CuKindProbe);
                    cuTaken += res.TallyByUnit.Values.Sum(t => t.DamageTaken);
                    cuTurns += res.Turns; cuN++;
                }

        Console.WriteLine("# 第95期 (S3) —— 形を選ぶ（選定規則を先に固定して当てる）");
        Console.WriteLine();
        Console.WriteLine($"台: `compare` {cuCompare.Length} 行 ＋ 交差帯 {cuCross.Length} 行 × 全 {cuStages.Count} 波 "
                          + $"× seed 0..{CuSeeds - 1} ＝ {cuB2:N0} 戦。"
                          + $"決着 {cuTurns / cuN:F2}T ／ 総被ダメ（両陣営）{cuTaken / cuN:F1} 点/戦。");
        Console.WriteLine();

        Console.WriteLine("## 表D-1 —— 3形の供給の内訳（**観測**）");
        Console.WriteLine();
        Console.WriteLine("| 形 | 供給源 | 駒由来 回/戦 | engine 由来 回/戦 | engine の割合 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        double cuTot = cuDirtWritesUnit + cuDirtWritesEngine;
        Console.WriteLine($"| **形1** 種類だけ重くなる | 汚れが載っていること（7 キー） | {cuDirtWritesUnit:F2} | {cuDirtWritesEngine:F2} "
                          + $"| {(cuTot > 0 ? $"{cuDirtWritesEngine * 100 / cuTot:F1}%" : "—")} |");
        Console.WriteLine($"| **形2** 書かれると隣へ伝播 | 汚れの**書き込み事象** | {cuDirtWritesUnit:F2} | {cuDirtWritesEngine:F2} "
                          + $"| {(cuTot > 0 ? $"{cuDirtWritesEngine * 100 / cuTot:F1}%" : "—")} |");
        double cuTot3 = cuDrainUnit + cuDrainEngine;
        Console.WriteLine($"| **形3** 汚れが消えるとき | **減算の読み手**（中継を除く・観測のみ） | {cuDrainUnit:F2} | {cuDrainEngine:F2} "
                          + $"| {(cuTot3 > 0 ? $"{cuDrainEngine * 100 / cuTot3:F1}%" : "—")} |");
        Console.WriteLine();
        Console.WriteLine("**キー別の内訳（汚れの書き込み・回/戦）**"
                          + "——`全体` は `UnitTally.CarryCount`（窓口の全数）、"
                          + "`特性` は印が立っていた分、`engine` はその差。");
        Console.WriteLine();
        Console.WriteLine("| キー | 全体 | 特性に帰属 | engine の段 | engine の割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (int k in cuDirt)
            Console.WriteLine($"| {CuK(k)} | {cuDirtTotalByKey[k]:F2} | {cuDirtByKey[k]:F2} | {cuDirtEngineByKey[k]:F2} "
                              + $"| {(cuDirtTotalByKey[k] > 0 ? $"{cuDirtEngineByKey[k] * 100 / cuDirtTotalByKey[k]:F1}%" : "—")} |");
        Console.WriteLine();

        Console.WriteLine("## 表D-2 —— 汚れの種類数（**形1 の倍率そのもの**）");
        Console.WriteLine();
        Console.WriteLine($"汚れが書き込まれた瞬間の受け手が**既に**背負っている種類の数"
                          + $"（`Counter` を通る 6 キーだけ。なまりは数えられない）。"
                          + $"標本 {cuKindN / cuN:F2} 件/戦・平均 **{(cuKindN > 0 ? cuKindSum / cuKindN : 0):F2} 種**・最大 {cuKindMax:F0} 種。");
        Console.WriteLine();
        Console.WriteLine("| 種類数 | 割合 |");
        Console.WriteLine("|--:|--:|");
        for (int i = 0; i < cuKindHist.Length; i++)
            if (cuKindHist[i] > 0)
                Console.WriteLine($"| {i} | {cuKindHist[i] * 100 / Math.Max(1, cuKindN):F1}% |");
        Console.WriteLine();
        Console.WriteLine("> **第49期の「味方に載る状態異常は4種類しかない」の受け手側の再測定。**"
                          + "**形1 の倍率が 1 に張り付くなら、形1 は「呪い」ではなく定数の弱体である。**");
        Console.WriteLine();

        // --- 増える交差（既存 55 組のうち、機構が新しく立つ組）-----------------------------
        // grid と同じ器具を使う。**新しいキーを作る版は既存 55 組を1つも増やさない**
        // ——新しい軸は 12 本目なので、増えるのは 11 本の「新しい組」のほうになる。
        var cuSupK = CuSupKeys(cuPk.Sup, cuPkRelay);
        var cuReadK = CuReadKeys(cuPk.Read);
        var cuHave = new HashSet<(int, int)>();
        foreach (TraitId t in Enum.GetValues<TraitId>())
        {
            if (!cuReadK.TryGetValue(t, out var rr) || !cuSupK.TryGetValue(t, out var ss)) continue;
            foreach (int r in rr) foreach (int s2 in ss)
                if (r != s2) cuHave.Add(r < s2 ? (r, s2) : (s2, r));
        }
        // 形ごとに「読むキーの集合」を出す。**書くのは呪い**。
        var cuFormReads = new (string Name, int[] Reads)[]
        {
            ("形1 種類だけ重くなる", cuDirt),
            ("形2 書かれると隣へ伝播", Array.Empty<int>()),     // 読むキーと書くキーが同じ＝交差にならない
            ("形3 汚れが消えるとき", cuDrainKeys.OrderBy(x => x).ToArray()),
        };
        Console.WriteLine("## 表D-3 —— 増える交差（**既存 55 組**のうち、機構が新しく立つ組）");
        Console.WriteLine();
        Console.WriteLine("**「呪い」を新しいキーにするか、既存のなまり（弱体）軸にするかで答えが変わる**"
                          + "（指示書 §0-2 の最初の判断）。新しいキーは 12 本目の軸なので、"
                          + "**既存 55 組は1つも増えない**——増えるのは新しい軸との組のほう。");
        Console.WriteLine();
        Console.WriteLine("| 形 | 読むキー | 新キー版: 既存55組の増分 | なまり軸版: 既存55組の増分 | 新しく立つ組 |");
        Console.WriteLine("|---|---|--:|--:|---|");
        foreach (var f in cuFormReads)
        {
            var added = new List<(int, int)>();
            foreach (int r in f.Reads)
            {
                if (r == UnitTally.CarryDull) continue;                 // 書くキーと同じ
                var p = r < UnitTally.CarryDull ? (r, UnitTally.CarryDull) : (UnitTally.CarryDull, r);
                if (!cuHave.Contains(p) && !added.Contains(p)) added.Add(p);
            }
            Console.WriteLine($"| **{f.Name}** "
                + $"| {(f.Reads.Length == 0 ? "（書くキーと同じ）" : string.Join("・", f.Reads.Select(CuK)))} "
                + $"| **0** | **{added.Count}** "
                + $"| {(added.Count == 0 ? "—" : string.Join(" / ", added.Select(p => $"{CuK(p.Item1)}×{CuK(p.Item2)}")))} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表D-4 —— 読み手になる既存駒");
        Console.WriteLine();
        Console.WriteLine("**なまり軸版なら、既存の弱体の読み手がそのまま呪いの読み手になる**"
                          + "（第90期の滲み則・第93期の自傷と同じ構造＝規則3）。");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 保持者 | 何を読むか |");
        Console.WriteLine("|---|---|---|");
        var cuAll51b = UnitCatalog.All.ToArray();
        foreach (var kv in TraitEntryMap.Reads.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
            foreach ((int k, TraitEntryMap.Where w) in kv.Value)
            {
                if (k != UnitTally.CarryDull) continue;
                var holders = cuAll51b.Where(d => d.Traits.Contains(kv.Key)).Select(d => d.Name).ToArray();
                Console.WriteLine($"| `{kv.Key}` | {(holders.Length == 0 ? "**ロスターに居ない**" : string.Join("・", holders))} "
                                  + $"| 弱体（{w}） |");
            }
        Console.WriteLine();
        Console.WriteLine("**新しいキーにするなら読み手は 0 枚から作ることになる**"
                          + "——第57期の燃焼（書き手1・読み手1）と同じ AND ゲートに戻る。");
        Console.WriteLine();

        Console.WriteLine("## 表D-5 —— 紙のスループット（**門ではなく出力**。規約 (G7)）");
        Console.WriteLine();
        Console.WriteLine("**分子について3つを先に書く**（規約 (G7)）:");
        Console.WriteLine();
        Console.WriteLine("| 形 | 線形か二次か | 門ではなく出力 | 分母を削るか | 発火/戦（上限） | 分子 = 発火 × 単価 |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        double cuKindAvg = cuKindN > 0 ? cuKindSum / cuKindN : 0;
        Console.WriteLine($"| **形1** | **線形**（種類数は上限 {cuDirtCounter.Length} で頭打ち） | 出力 = 弱体の量 "
                          + $"| **削らない**（汚れは他人が撒く） | {cuDirtWritesUnit + cuDirtWritesEngine:F2} "
                          + $"| 発火 × 単価 × {cuKindAvg:F2} |");
        Console.WriteLine($"| **形2** | **二次**（伝播した汚れがまた伝播しうる。再入ガードが要る） | 出力 = 汚れの量 "
                          + $"| **削らない** | {cuDirtWritesUnit + cuDirtWritesEngine:F2} | 発火 × 単価 |");
        Console.WriteLine($"| **形3** | **線形** | 出力 = 弱体の量 "
                          + $"| **削る**（呪いが出るのは汚れが消えるとき＝汚れの読み手の発火に食い込む） "
                          + $"| {cuDrainUnit + cuDrainEngine:F2} | 発火 × 単価 |");
        Console.WriteLine();
        Console.WriteLine($"分母: 総被ダメ（両陣営）**{cuTaken / cuN:F1} 点/戦** ／ 決着 **{cuTurns / cuN:F2}T**。");
        Console.WriteLine();

        Console.WriteLine("## 表D-6 —— 選定規則の適用（**上から順に当てる。数字を見てから動かさない**）");
        Console.WriteLine();
        Console.WriteLine("| 規則 | 形1 | 形2 | 形3 |");
        Console.WriteLine("|---|---|---|---|");
        bool r1a = cuDirtWritesEngine <= cuDirtWritesUnit;
        bool r1c = cuDrainEngine <= cuDrainUnit;
        Console.WriteLine($"| **1** 供給の過半が駒 | {(r1a ? "○" : "×")}（engine {(cuTot > 0 ? cuDirtWritesEngine * 100 / cuTot : 0):F1}%） "
                          + $"| {(r1a ? "○" : "×")}（同上） "
                          + $"| {(r1c ? "○" : "×")}（engine {(cuTot3 > 0 ? cuDrainEngine * 100 / cuTot3 : 0):F1}%） |");
        int[] cuAdd = new int[3];
        for (int i = 0; i < cuFormReads.Length; i++)
        {
            var added = new HashSet<(int, int)>();
            foreach (int r in cuFormReads[i].Reads)
            {
                if (r == UnitTally.CarryDull) continue;
                var p = r < UnitTally.CarryDull ? (r, UnitTally.CarryDull) : (UnitTally.CarryDull, r);
                if (!cuHave.Contains(p)) added.Add(p);
            }
            cuAdd[i] = added.Count;
        }
        Console.WriteLine($"| **2** 交差が2本以上増える（なまり軸版） | {(cuAdd[0] >= 2 ? "○" : "×")}（{cuAdd[0]} 本） "
                          + $"| {(cuAdd[1] >= 2 ? "○" : "×")}（{cuAdd[1]} 本） "
                          + $"| {(cuAdd[2] >= 2 ? "○" : "×")}（{cuAdd[2]} 本） |");
        int cuDullReaders = TraitEntryMap.Reads
            .Count(kv => kv.Value.Any(x => x.Key == UnitTally.CarryDull)
                         && cuAll51b.Any(d => d.Traits.Contains(kv.Key)));
        Console.WriteLine($"| **3** 既存駒が1文も増やさずに読み手になる | ○（弱体の読み手 {cuDullReaders} 枚） "
                          + $"| ○（同上） | ○（同上） |");
        Console.WriteLine($"| **4** 新キーなら `Kinds` と `Stoic` を説明できる "
                          + $"| **なまり軸版なら新キー無し**（`Kinds` {ScapegoatTrait.Kinds.Length} 本のまま） | 同左 | 同左 |");
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // (S4) —— 呪い則の測定（規約 (G8) の **甲**: 2×2 ＋ `compare` 61行 ＋ 交差帯12行 ＋ 自己検査）
    //
    //     curse run <v> [skip] [take]   # 2×2 を TSV へ（v = 0 対照 / 1 呪い則）
    //     curse tables <v0.tsv> <v1.tsv>  # 表E（主判定）
    //     curse veto                    # 拒否権（`compare` 61行・交差帯12行）
    //     curse check                   # 自己検査
    //
    // 2×2 の定数は**第81期 `pairs2` の写し**（第82〜88期と1つも変えていない）。
    // ---------------------------------------------------------------------------------
    const int CuTableSeed = 9_500_000;        // **第88期の 8,100,000 とは別の標本**（第89期の作法）
    const int CuK2 = 64, CuS2 = 2, CuBand = 0, CuM = 8;
    const int CuStrong = 7, CuWeakPct = 60, CuDrawCap = 20000;
    const double CuEps = 1e-9;
    var cuInv = System.Globalization.CultureInfo.InvariantCulture;
    var cuRoster = UnitCatalog.All.ToArray();
    int cuRN = cuRoster.Length;
    var cuIdx = new Dictionary<string, int>();
    for (int u = 0; u < cuRN; u++) cuIdx[cuRoster[u].Id] = u;

    // A ＝ 呪詛官ネル（規約 (G5)。弱体の供給の 74% を持つ。§4-2）
    const string CuAId = "nel";
    // 意図した相手（§4-1。**測る前に固定してある**）
    string[] cuIntended = { "utsu", "uke", "wata",                    // なまりの読み手 3 枚
                            "guza", "sid", "borg", "kugu", "hisa", "kiri", "nomi" };  // 汚れの書き手 7 枚

    BattleResult CuRunV(Formation f, Formation e, int seed, bool verbose, int v)
        => BattleEngine.Run(f, e, seed, verbose: verbose,
                            soak: SoakRule.Default with { DullPerKind = v != 0 ? 1 : 0 });
    string CuVName(int v) => v == 0 ? "V0（現行）" : "V1（呪い則）";

    // 弱い波（敵 MaxHp 0.6 倍・第70期以降と同一。`Stages` は書き換えない）
    var cuWeakCache = new Dictionary<string, UnitDef>();
    UnitDef CuWeakOf(UnitDef d)
    {
        if (cuWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * CuWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits, Pattern = d.Pattern, Actions = d.Actions
        };
        cuWeakCache[d.Id] = w;
        return w;
    }
    var cuWeak = cuStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = CuWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef CuPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var cuPlainMap = cuRoster.ToDictionary(d => d.Id, CuPlain);

    UnitDef[] CuFill(UnitDef[] pool, int strong0, int seed)
    {
        int rn = pool.Length;
        var rng = new Random(seed);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        int remain = rn, strong = strong0;
        var picked = new UnitDef[3];
        for (int r = 0; r < 3; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = pool[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= CuStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int CuDrawSeed(int pairIx, int draw)
    {
        ulong x = (ulong)CuTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    int[] CuSeats(UnitDef[] u)
    {
        var all5 = new[] { 0, 1, 2, 3, 4 };
        var front = all5.OrderByDescending(k => u[k].MaxHp)
                        .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        var rest = all5.Where(k => k != front[0] && k != front[1]).ToArray();
        var back = rest.OrderByDescending(k => u[k].Attack)
                       .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        int center = rest.Single(k => k != back[0] && k != back[1]);
        var r = new int[5];
        r[front[0]] = 0; r[front[1]] = 1; r[center] = 2; r[back[0]] = 3; r[back[1]] = 4;
        return r;
    }
    // TSV の添字: 0 = y11 ／ 1 = y01（**A 素体**）／ 2 = y10（**B 素体**）／ 3 = y00
    Formation CuForm(UnitDef[] u, int[] seats, int cell)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (cell & 1) != 0) || (k == 1 && (cell & 2) != 0);
            f[seats[k]] = plain ? cuPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double CuRate(Formation f, int v)
    {
        double sum = 0;
        for (int wi = 1; wi < cuStages.Count; wi++)
        {
            int wins = 0;
            for (int seed = CuBand; seed < CuBand + CuM; seed++)
                if (CuRunV(f, cuWeak[wi].Enemy, seed, false, v).PlayerWon) wins++;
            sum += wins * 100.0 / CuM;
        }
        return sum / (cuStages.Count - 1);
    }
    var cuPairIxOf = new int[cuRN, cuRN];
    {
        int pi = 0;
        for (int a = 0; a < cuRN; a++) for (int b = a + 1; b < cuRN; b++) { cuPairIxOf[a, b] = cuPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> CuFills(int a, int b)
    {
        var pool = cuRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (cuRoster[a].Attack >= CuStrong ? 1 : 0) + (cuRoster[b].Attack >= CuStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < CuS2 * CuK2 && draw < CuDrawCap; draw++)
        {
            var f = CuFill(pool, strong0, CuDrawSeed(cuPairIxOf[a, b], draw));
            var t = f.Select(d => cuIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }

    if (cuMode == "run")
    {
        int cuV = args.Length > 3 ? int.Parse(args[3]) : 0;
        int cuSkip = args.Length > 4 ? int.Parse(args[4]) : 0;
        int cuTake = args.Length > 5 ? int.Parse(args[5]) : int.MaxValue;
        int a0 = cuIdx[CuAId];
        var others = Enumerable.Range(0, cuRN).Where(u => u != a0).Skip(cuSkip).Take(cuTake).ToArray();
        foreach (int b in others)
        {
            var fills = CuFills(a0, b);
            var sb = new System.Text.StringBuilder();
            sb.Append(95).Append('\t').Append(cuV).Append('\t').Append(a0).Append('\t').Append(b)
              .Append('\t').Append(fills.Count);
            for (int t = 0; t < fills.Count; t++)
            {
                var team = new[] { cuRoster[a0], cuRoster[b], fills[t][0], fills[t][1], fills[t][2] };
                int[] seats = CuSeats(team);
                for (int cell = 0; cell < 4; cell++)
                    sb.Append('\t').Append(CuRate(CuForm(team, seats, cell), cuV).ToString("F6", cuInv));
            }
            Console.WriteLine(sb.ToString());
        }
        return;
    }

    if (cuMode == "tables")
    {
        (int V, int A, Dictionary<int, double[][]> D) CuRead(string path)
        {
            var d = new Dictionary<int, double[][]>();
            int vv = -1, aa = -1;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0) continue;
                var c = line.Split('\t');
                int v = int.Parse(c[1]), a = int.Parse(c[2]), b = int.Parse(c[3]), nT = int.Parse(c[4]);
                if (vv < 0) { vv = v; aa = a; }
                else if (v != vv || a != aa) throw new InvalidOperationException($"{path}: 版／A が混ざっている");
                var ys = new double[nT][];
                int at = 5;
                for (int t = 0; t < nT; t++) { ys[t] = new double[4]; for (int k = 0; k < 4; k++) ys[t][k] = double.Parse(c[at++], cuInv); }
                d[b] = ys;
            }
            return (vv, aa, d);
        }
        double CuSyn(double[] y) => y[0] - y[2] - y[1] + y[3];
        bool CuInfo(double[] y) => !(y[3] < CuEps && y[1] < CuEps) && !(y[3] > 100 - CuEps && y[1] > 100 - CuEps);
        double CuPct(IReadOnlyList<double> xs, double q)
        {
            if (xs.Count == 0) return double.NaN;
            var s = xs.OrderBy(v => v).ToArray();
            if (s.Length == 1) return s[0];
            double pos = q * (s.Length - 1);
            int lo = (int)Math.Floor(pos), hi = Math.Min(lo + 1, s.Length - 1);
            return s[lo] + (pos - lo) * (s[hi] - s[lo]);
        }

        var r0 = CuRead(args[3]);
        var r1 = CuRead(args[4]);
        if (r0.V != 0 || r1.V != 1) { Console.WriteLine("V0 の TSV を先に、V1 を後に渡すこと。"); return; }
        int aIx = r0.A;
        var bs = r0.D.Keys.Where(r1.D.ContainsKey).OrderBy(x => x).ToArray();

        // Δ相乗（台ごと）と系列平均。**フィルタ無しが主判定**（規約 (G3)。engine の規則なので）。
        var dAll = new Dictionary<int, double>();
        var dSer = new Dictionary<int, double[]>();
        var dInfo = new Dictionary<int, double>();
        var nInfo = new Dictionary<int, int>();
        int mism = 0, mismCells = 0;
        foreach (int b in bs)
        {
            var y0 = r0.D[b]; var y1 = r1.D[b];
            int n = Math.Min(y0.Length, y1.Length);
            var all = new List<double>(); var s0 = new List<double>(); var s1 = new List<double>();
            var inf = new List<double>();
            for (int t = 0; t < n; t++)
            {
                double d = CuSyn(y1[t]) - CuSyn(y0[t]);
                all.Add(d); (t % CuS2 == 0 ? s0 : s1).Add(d);
                if (CuInfo(y0[t])) inf.Add(d);
                // 自己検査: A を素体にしたセル（y01 / y00）は engine の規則なので**動いてよい**
                for (int k = 1; k < 4; k += 2) if (Math.Abs(y0[t][k] - y1[t][k]) > CuEps) { mismCells++; break; }
            }
            if (all.Count == 0) continue;
            dAll[b] = all.Average();
            dSer[b] = new[] { s0.Count > 0 ? s0.Average() : double.NaN, s1.Count > 0 ? s1.Average() : double.NaN };
            dInfo[b] = inf.Count > 0 ? inf.Average() : double.NaN;
            nInfo[b] = inf.Count;
            if (all.Count != n) mism++;
        }

        var intendedIx = cuIntended.Where(cuIdx.ContainsKey).Select(x => cuIdx[x]).ToHashSet();
        var unintended = dAll.Keys.Where(b => !intendedIx.Contains(b)).ToArray();
        double floor = CuPct(unintended.Select(b => Math.Abs(dAll[b])).ToArray(), 0.95);
        var ranked = dAll.OrderByDescending(x => x.Value).ToArray();

        Console.WriteLine("# 第95期 (S4) 表E —— 2×2 の主判定");
        Console.WriteLine();
        Console.WriteLine($"A ＝ {cuRoster[aIx].Name}（`{cuRoster[aIx].Id}`。規約 (G5)：弱体の供給の 74% を持つ）"
                          + $" ／ B ＝ 残り {bs.Length} 体 × {CuK2 * CuS2} 台 × 4 セル × 4 波 × seed {CuBand}..{CuBand + CuM - 1}。");
        Console.WriteLine();
        Console.WriteLine("**規約 (G3) により、engine の規則なので主判定は情報帯フィルタ<u>無し</u>で行う**"
                          + "（フィルタ有りは参考として併記する）。");
        Console.WriteLine();
        Console.WriteLine($"**増分尺度のノイズ床（第89期の規約：意図しない相手 {unintended.Length} 体の |Δ相乗| の 95%tile）"
                          + $" ＝ {floor:F2}pt。**");
        Console.WriteLine();
        Console.WriteLine("| 順位 | B | 意図 | Δ相乗 | 系列1 | 系列2 | 2系列とも正 | 床超え | 参考: フィルタ有り |");
        Console.WriteLine("|--:|---|:-:|--:|--:|--:|:-:|:-:|--:|");
        for (int i = 0; i < ranked.Length; i++)
        {
            int b = ranked[i].Key;
            bool both = dSer[b][0] > 0 && dSer[b][1] > 0;
            bool over = Math.Abs(dAll[b]) > floor;
            Console.WriteLine($"| {i + 1} | {cuRoster[b].Name} | {(intendedIx.Contains(b) ? "**○**" : "")} "
                              + $"| {dAll[b]:+0.00;-0.00;0.00} | {dSer[b][0]:+0.00;-0.00;0.00} | {dSer[b][1]:+0.00;-0.00;0.00} "
                              + $"| {(both ? "○" : "")} | {(over ? "○" : "")} "
                              + $"| {(double.IsNaN(dInfo[b]) ? "—" : dInfo[b].ToString("+0.00;-0.00;0.00"))}（{nInfo[b]} 台） |");
        }
        Console.WriteLine();
        var q11 = intendedIx.Where(dAll.ContainsKey)
                            .Where(b => dSer[b][0] > 0 && dSer[b][1] > 0 && Math.Abs(dAll[b]) > floor).ToArray();
        int q12 = unintended.Count(b => Math.Abs(dAll[b]) > floor);
        Console.WriteLine($"**Q1-1（意図した相手のうち少なくとも1枚が 2系列とも正 かつ 床超え）: "
                          + $"{q11.Length} / {intendedIx.Count(b => dAll.ContainsKey(b))} 枚**"
                          + $"{(q11.Length > 0 ? "（" + string.Join("・", q11.Select(b => cuRoster[b].Name)) + "）" : "")}"
                          + $" —— {(q11.Length > 0 ? "**○**" : "**×**")}");
        Console.WriteLine();
        Console.WriteLine($"**Q1-2（意図しない相手で床を超えた体数）: {q12} 体**"
                          + $"（床は 95%tile なので構成上おおよそ {unintended.Length * 5 / 100} 体が必ず超える）");
        Console.WriteLine();
        int bestRank = ranked.Select((x, i) => (x.Key, i)).Where(x => intendedIx.Contains(x.Key))
                             .Select(x => x.i + 1).DefaultIfEmpty(-1).Min();
        Console.WriteLine($"**Q2（意図した組の最良順位）: {bestRank} 位 / {ranked.Length}**");
        Console.WriteLine();
        Console.WriteLine($"**自己検査: A を素体にしたセル（y01 / y00）が版で動いた台は {mismCells} 件。**"
                          + "**engine の規則なので A を素体にしても規則は走る**ので、"
                          + "これは実装のバグではない（第91期 (G3)・第90期の自己検査 (a)）。"
                          + "**情報帯の選別に使っているのは V0 のセルだけ**なので、選別は汚れていない。");
        Console.WriteLine();
        return;
    }

    // なぜその B で立ったのか（**主判定の1位の因果を確かめる**）。
    // 2×2 と同じ台・同じ席の `y11` セルだけを V1 で回して、呪いの発火を B ごとに数える。
    if (cuMode == "why")
    {
        int a0 = cuIdx[CuAId];
        Console.WriteLine("# 第95期 (S4) 表F —— なぜその相手で立ったのか（**2×2 の台の上で発火を数える**）");
        Console.WriteLine();
        Console.WriteLine($"A ＝ {cuRoster[a0].Name} 固定。2×2 の `y11` セル（両方とも本物）だけを V1 で回す。");
        Console.WriteLine();
        Console.WriteLine("| B | 意図 | 発火 回/戦 | 空振り 回/戦 | 発火率 | 倍率 | 上乗せ 量/戦 | 弱体 V0→V1 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|---|");
        var intendedIx2 = cuIntended.Where(cuIdx.ContainsKey).Select(x => cuIdx[x]).ToHashSet();
        var rows = new List<(string Name, bool Int, double F, double D, double K, double Ad, double D0, double D1)>();
        foreach (int b in Enumerable.Range(0, cuRN).Where(u => u != a0))
        {
            var fills = CuFills(a0, b);
            double f = 0, dr = 0, kk = 0, ad = 0, d0 = 0, d1 = 0; int n = 0;
            foreach (var fill in fills)
            {
                var team = new[] { cuRoster[a0], cuRoster[b], fill[0], fill[1], fill[2] };
                int[] seats = CuSeats(team);
                Formation form = CuForm(team, seats, 0);
                for (int wi = 1; wi < cuStages.Count; wi++)
                    for (int seed = CuBand; seed < CuBand + CuM; seed++)
                    {
                        var r1 = CuRunV(form, cuWeak[wi].Enemy, seed, false, 1);
                        var r0 = CuRunV(form, cuWeak[wi].Enemy, seed, false, 0);
                        f += r1.SoakDullFired; dr += r1.SoakDullDry; kk += r1.SoakDullKinds; ad += r1.SoakDullAdded;
                        d0 += r0.DullTotal; d1 += r1.DullTotal; n++;
                    }
            }
            rows.Add((cuRoster[b].Name, intendedIx2.Contains(b), f / n, dr / n, kk / n, ad / n, d0 / n, d1 / n));
        }
        foreach (var r in rows.OrderByDescending(x => x.F))
            Console.WriteLine($"| {r.Name} | {(r.Int ? "**○**" : "")} | **{r.F:F2}** | {r.D:F2} "
                              + $"| {(r.F + r.D > 0 ? $"{r.F * 100 / (r.F + r.D):F1}%" : "—")} "
                              + $"| {(r.F > 1e-9 ? r.K / r.F : 0):F2} "
                              + $"| {r.Ad:F2} | {r.D0:F2} → {r.D1:F2} |");
        Console.WriteLine();
        return;
    }

    if (cuMode == "veto")
    {
        const int CuVetoSeeds = 200;
        Console.WriteLine("# 第95期 (S4) —— 拒否権（`compare` 61行・交差帯12行）");
        Console.WriteLine();
        double[] CuRow(Formation f, int v)
        {
            var r = new double[cuStages.Count];
            for (int st = 0; st < cuStages.Count; st++)
            {
                int w = 0;
                for (int seed = 0; seed < CuVetoSeeds; seed++)
                    if (CuRunV(f, cuStages[st].Enemy, seed, false, v).PlayerWon) w++;
                r[st] = w * 100.0 / CuVetoSeeds;
            }
            return r;
        }
        var prim = new HashSet<string>(Baseline.PrimaryRows);
        void CuBand2(string title, (string Name, Formation F)[] rows, bool primary)
        {
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine("| 行 | 波 | V0 | V1 | Δ |");
            Console.WriteLine("|---|---|--:|--:|--:|");
            int cells = 0, movedRows = 0, big = 0;
            double p5v0 = 0, p5v1 = 0; int p5n = 0;
            var bigRows = new List<(string Name, int Wave, double D)>();
            foreach (var (name, f) in rows)
            {
                var a = CuRow(f, 0); var b = CuRow(f, 1);
                bool moved = false;
                for (int st = 0; st < cuStages.Count; st++)
                {
                    if (Math.Abs(a[st] - b[st]) < CuEps) continue;
                    cells++; moved = true;
                    Console.WriteLine($"| {name} | {cuStages[st].Name} | {a[st]:F1}% | {b[st]:F1}% "
                                      + $"| **{b[st] - a[st]:+0.0;-0.0;0.0}** |");
                    if (b[st] - a[st] <= -10.0) { big++; bigRows.Add((name, st, b[st] - a[st])); }
                }
                if (moved) movedRows++;
                if (primary && prim.Contains(name)) { p5v0 += a[^1]; p5v1 += b[^1]; p5n++; }
            }
            Console.WriteLine();
            Console.WriteLine($"**動いたセル {cells} 件 / {rows.Length * cuStages.Count}・動いた行 {movedRows} / {rows.Length}。**");
            if (primary)
            {
                Console.WriteLine();
                Console.WriteLine($"**拒否権1（主判定 {p5n} 行の第五波平均）: {p5v0 / Math.Max(1, p5n):F1}% → "
                                  + $"{p5v1 / Math.Max(1, p5n):F1}%（歯止め {Baseline.PrimaryFifthFloor:F1}）"
                                  + $" —— {(p5v1 / Math.Max(1, p5n) >= Baseline.PrimaryFifthFloor ? "**○**" : "**×**")}**");
                Console.WriteLine();
                Console.WriteLine($"**拒否権3（(G1)(G2)：61 行の分母で −10.0pt 以上落ちたセル）: {big} 件**");
                foreach (var x in bigRows)
                    Console.WriteLine($"- {x.Name} / {cuStages[x.Wave].Name}: {x.D:+0.0;-0.0;0.0}pt");
            }
            Console.WriteLine();
        }
        CuBand2($"`compare` {cuCompare.Length} 行", cuCompare, true);
        CuBand2($"交差帯 {cuCross.Length} 行", cuCross, false);
        return;
    }

    if (cuMode == "check")
    {
        Console.WriteLine("# 第95期 (S4) —— 自己検査");
        Console.WriteLine();
        // 必須1: compare 305 セルが docs/balance.md と 0 件（**既定は無効なので動かない**）
        string? root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Path.GetDirectoryName(root);
        var cells = new List<string>();
        foreach (var b in cuCompare)
        {
            var row = new List<string>();
            foreach (var st in cuStages)
            {
                int w = 0;
                for (int seed = 0; seed < 200; seed++)
                    if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                row.Add($"{w * 100.0 / 200:F1}%");
            }
            cells.Add($"| {b.Name} | {string.Join(" | ", row)} |");
        }
        int diff = -1;
        if (root is not null)
        {
            var doc = File.ReadAllLines(Path.Combine(root, "docs", "balance.md"))
                          .Where(l => l.StartsWith("| ") && l.Contains('%')).ToArray();
            diff = Math.Abs(doc.Length - cells.Count);
            for (int i = 0; i < Math.Min(doc.Length, cells.Count); i++)
                if (doc[i].Trim() != cells[i].Trim()) diff++;
        }
        Console.WriteLine($"- **(a)** `compare` {cuCompare.Length} 行 × {cuStages.Count} 波 ＝ "
                          + $"{cuCompare.Length * cuStages.Count} セルを `docs/balance.md` と突き合わせ: "
                          + $"**ずれ {diff} 行**{(diff == 0 ? "（○）" : "（×）")}");

        // (b) 既定が無効であること
        var def = SoakRule.Default;
        Console.WriteLine($"- **(b)** `SoakRule.Default` = `{def}`（第96期 (R1) に `CurseRule` を畳んだ） —— "
                          + $"{(def.DullPerKind > 0 ? "**有効（採用済み）**" : "**無効（既定では1行も走らない）**")}");

        // (c) 規則を切った版と既定が1セルも違わないこと
        int same = 0, tot = 0;
        foreach (var b in cuCompare.Concat(cuCross))
            foreach (var st in cuStages)
            {
                int w0 = 0, w1 = 0;
                for (int seed = 0; seed < 200; seed++)
                {
                    if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w0++;
                    if (CuRunV(b.F, st.Enemy, seed, false, SoakRule.Default.DullPerKind > 0 ? 1 : 0).PlayerWon) w1++;
                }
                tot++; if (w0 == w1) same++;
            }
        Console.WriteLine($"- **(c)** 既定（引数を渡さない）と `SoakRule {{ DullPerKind = {SoakRule.Default.DullPerKind} }}` が"
                          + $"一致するセル: **{same} / {tot}**{(same == tot ? "（○）" : "（×）")}"
                          + "——**明示して渡した版と既定が同じ盤面を出すこと**の検算"
                          + "（採用の前後でどちらの版と比べるかが入れ替わる。第36期 `gullet belly4` と同型）");

        // (d) 規則が実際に発火すること（陽性対照）
        double fired = 0, dry = 0, kinds = 0, added = 0, dull0 = 0, dull1 = 0; int n = 0;
        var rt0 = new double[DullRoutes.Count];
        var rt1 = new double[DullRoutes.Count];
        foreach (var b in cuCompare.Concat(cuCross))
            foreach (var st in cuStages)
                for (int seed = 0; seed < 20; seed++)
                {
                    var r1 = CuRunV(b.F, st.Enemy, seed, false, 1);
                    var r0 = CuRunV(b.F, st.Enemy, seed, false, 0);
                    fired += r1.SoakDullFired; dry += r1.SoakDullDry; kinds += r1.SoakDullKinds; added += r1.SoakDullAdded;
                    dull0 += r0.DullTotal; dull1 += r1.DullTotal; n++;
                    for (int r = 0; r < DullRoutes.Count; r++)
                    { rt0[r] += r0.DullByRoute[r]; rt1[r] += r1.DullByRoute[r]; }
                }
        Console.WriteLine($"- **(d)** 陽性対照: 発火 **{fired / n:F2} 回/戦** ／ 空振り {dry / n:F2} 回/戦 "
                          + $"／ 倍率 **{(fired > 0 ? kinds / fired : 0):F2} 種** ／ 上乗せ **{added / n:F2} 量/戦**"
                          + $"（弱体の総量 {dull0 / n:F2} → {dull1 / n:F2}・**+{(dull1 - dull0) * 100 / Math.Max(1, dull0):F1}%**）");
        Console.WriteLine();
        Console.WriteLine("**経路別**（呪いがどの経路に乗ったか。**開戦時1回の経路には汚れが1つも無い**）:");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 周期 | V0 量/戦 | V1 量/戦 | 上乗せ |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        for (int r = 0; r < DullRoutes.Count; r++)
        {
            if (rt0[r] < CuEps && rt1[r] < CuEps) continue;
            TraitId? t = DullRoutes.Names[r] switch
            {
                "なまり" => TraitId.Sharer, "呪詛敵" => TraitId.Curse, "呪詛漏れ" => TraitId.Curse,
                "突き返し" => TraitId.Shove, "萎縮" => TraitId.Cower, "渡し" => TraitId.Relay,
                "誹り" => TraitId.Slander, "驕り" => TraitId.Overbear, "火選り" => TraitId.Favor, _ => null
            };
            Console.WriteLine($"| {DullRoutes.Names[r]} | {(t is null ? "—" : CuPeriodOf(t.Value))} "
                              + $"| {rt0[r] / n:F2} | {rt1[r] / n:F2} | **{(rt1[r] - rt0[r]) / n:+0.00;-0.00;0.00}** |");
        }

        // (e) V0 で計数が全部 0
        Console.WriteLine($"- **(e)** V0 で `SoakDullFired` が 0 であること: "
                          + $"{(cuCompare.Take(5).All(b => cuStages.All(st => CuRunV(b.F, st.Enemy, 0, false, 0).SoakDullFired == 0)) ? "○" : "×")}");

        // (f) ctx.PickOne を新たに使っていない
        int pick = 0;
        if (root is not null)
            foreach (string f in Directory.GetFiles(Path.Combine(root, "BattleCore"), "*.cs"))
                pick += System.Text.RegularExpressions.Regex.Matches(
                    string.Join("\n", File.ReadAllLines(f).Where(l => !l.TrimStart().StartsWith("//"))),
                    @"PickOne\(").Count;
        Console.WriteLine($"- **(f)** `BattleCore` の `PickOne(` 呼び出し **{pick} 箇所**"
                          + "（第95期は1つも足していない。`git diff` で確かめること）");
        Console.WriteLine();
        return;
    }
    Console.WriteLine("使い方: `curse grid` / `curse phase0` / `curse pick` / `curse run <v> [skip] [take]` / `curse tables <v0> <v1>` / `curse veto` / `curse check`");
    return;
}
}
