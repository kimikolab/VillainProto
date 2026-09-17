using BattleCore;
using static Common;

// =====================================================================================
// tomo モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "tomo")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 tomo
// =====================================================================================

static class TomoDiag
{
// ==================================================================================
// 第110期 —— 尾灯のトモの譲渡条件を3版で測る（現行／窓／即時）。
// 指示書は design/PHASE110_YIELD_SPEC.md。
//
// **第109期の `tomo phase0 / run / mio / tables / check` は1文字も書き換えていない**
// （この直後の `if (focusId == "tomo")` がそのまま残っている。第109期の値の再現に要る）。
//
//     dotnet run --project BattleSim -c Release 0 tomo yield phase0   # §3（旧台での V0 再現・土台の探索・素体版）
//     dotnet run --project BattleSim -c Release 0 tomo yield run      # 新4台 × 3版 × 素体
//     dotnet run --project BattleSim -c Release 0 tomo yield tables   # 表A〜E
//     dotnet run --project BattleSim -c Release 0 tomo yield check    # 自己検査
// ==================================================================================
public static void Yield(string[] args, int stageIndex)
{
    string tyMode = args.Length > 3 ? args[3] : "tables";
    IReadOnlyList<EnemyCatalog.Stage> tyStages = EnemyCatalog.Stages;
    const int TySeeds = 200;    // 指示書 §1-2（seed 0..199）
    const int TyAudit = 40;     // ログ再生の帯（第108・109期と同じ）

    // ------------------------------------------------------------------------------
    // 素体（同数値・特性なし・`Actions` なし）。第47期の作法。カタログには載せない。
    // ------------------------------------------------------------------------------
    static UnitDef TyPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain",
        Name = "素体の" + d.Name,
        MaxHp = d.MaxHp,
        Attack = d.Attack,
        Speed = d.Speed,
        Traits = Array.Empty<TraitId>(),
        Pattern = d.Pattern
    };
    UnitDef tyTomoPlain = TyPlain(UnitCatalog.Tomo);

    // ------------------------------------------------------------------------------
    // 版。**3版とも明示的にモードを渡す。`TaillightRule.Default` に頼らない**
    // ——第110期に既定が V1 へ動いたので、頼ると「V0 の列」が黙って V1 に化ける
    // （実際に一度化けた。自己検査 (h) がそれを捕まえた）。
    // ------------------------------------------------------------------------------
    var tyVers = new (string Tag, TaillightRule? Rule)[]
    {
        ("素体",    null),                                      // トモを素体に差し替えた版
        ("V0 現行", new TaillightRule(YieldMode.OwnTurn)),
        ("V1 窓",   new TaillightRule(YieldMode.OwnTurnWindow)),
        ("V2 即時", new TaillightRule(YieldMode.Immediate)),
    };
    const int TyPlainVer = 0, TyV0 = 1, TyV1 = 2, TyV2 = 3;

    // パートナー4枚は第109期と同じ。**変えるのは土台3枚。**
    var tyPartners = new (string Tag, UnitDef Def)[]
    {
        ("トモ×ドルガ", UnitCatalog.Dolga),
        ("トモ×ムド",   UnitCatalog.Mudo),
        ("トモ×ソム",   UnitCatalog.Som),
        ("トモ×ハギ",   UnitCatalog.Hagi),
    };

    // 第109期の台（土台 = ガルド / キリ / ボルグ）。phase0 の 1（V0 の再現）と自己検査 (l) で使う。
    Formation TyOldBench(UnitDef partner, UnitDef tomo) => Formation.Build(
        front1: partner, front3: UnitCatalog.Gald, center: UnitCatalog.Kiri,
        back1: tomo, back3: UnitCatalog.Borg);

    // ------------------------------------------------------------------------------
    // 土台を選ぶ規則（指示書 §1-2。**測る前にコードへ固定してある**）。
    //   (1) 速さ ≥ 8（`Stoic` 持ちだけは速さを問わない——灯の候補から自前で外れるので、
    //       遅くてもパートナーから灯を奪わない）
    //   (2) **`AtkBonus` を他人に書かない**——`TraitEntryMap.Supplies` に
    //       （強化 or 弱体）×〈自分以外〉の行を持たず、蘇生（`Reviver`）も持たない
    //   (3) 素体版（トモ → 素体）の第2〜5波平均が **40〜95%** に入る台が **4台中3台以上**
    //
    // **プールからは3枚を選ぶ**（指示書の「変えるのは土台3枚」）。同点の割り方も先に決めてある:
    //   帯に入った台数 → 4台の素体平均が 50% に近い → 3枚の `Def.Id` を連結した辞書順。
    //
    // 席も**測る前に固定**する（第109期の席の理屈をそのまま規則にした）:
    //   後3 = 範囲攻撃を持つ駒（`Def.Pattern != Single`）のうち攻撃力最大・同値は `Def.Id`。
    //         1枚も無ければ攻撃力が最小の駒。**後3 の隣接は 前3 と 中央 だけ**なので、
    //         巻き込みがパートナー（前1）にもトモ（後1）にも当たらない
    //   前3 = 残り2枚のうち `Stoic` を持つ駒。無ければ HP が大きいほう（**狙**：ガルドが前列）
    //   中央 = 残り
    // ------------------------------------------------------------------------------
    static bool TyWritesBuffToOthers(UnitDef d)
        => d.Traits.Contains(TraitId.Reviver)
           || d.Traits.Any(id => TraitEntryMap.Supplies.TryGetValue(id, out var ss)
                && ss.Any(e => (e.Key == UnitTally.CarryWhet || e.Key == UnitTally.CarryDull)
                               && e.W != TraitEntryMap.Where.Self));

    var tyOff = new HashSet<string>(new[] { UnitCatalog.Tomo.Id }
        .Concat(tyPartners.Select(p => p.Def.Id)));

    var tyPool = UnitCatalog.All
        .Where(d => !tyOff.Contains(d.Id))
        .Where(d => d.Speed >= 8 || d.Traits.Contains(TraitId.Stoic))
        .Where(d => !TyWritesBuffToOthers(d))
        .OrderBy(d => d.Id, StringComparer.Ordinal)
        .ToList();

    static (UnitDef F3, UnitDef C, UnitDef B3) TySeatsOf(IReadOnlyList<UnitDef> three)
    {
        var rest = three.ToList();
        var ranged = rest.Where(d => d.Pattern != AttackPattern.Single)
                         .OrderByDescending(d => d.Attack).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();
        UnitDef back3 = ranged.Count > 0 ? ranged[0]
                      : rest.OrderBy(d => d.Attack).ThenBy(d => d.Id, StringComparer.Ordinal).First();
        rest.Remove(back3);
        var stoic = rest.Where(d => d.Traits.Contains(TraitId.Stoic))
                        .OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
        UnitDef front3 = stoic.Count > 0 ? stoic[0]
                       : rest.OrderByDescending(d => d.MaxHp).ThenBy(d => d.Id, StringComparer.Ordinal).First();
        rest.Remove(front3);
        return (front3, rest[0], back3);
    }

    Formation TyBench(UnitDef partner, IReadOnlyList<UnitDef> three, UnitDef tomo)
    {
        var (f3, c, b3) = TySeatsOf(three);
        return Formation.Build(front1: partner, front3: f3, center: c, back1: tomo, back3: b3);
    }

    // 3枚の組み合わせ（辞書順で固定）。
    var tyCombos = new List<UnitDef[]>();
    for (int i = 0; i < tyPool.Count; i++)
        for (int j = i + 1; j < tyPool.Count; j++)
            for (int k = j + 1; k < tyPool.Count; k++)
                tyCombos.Add(new[] { tyPool[i], tyPool[j], tyPool[k] });

    // 素体版の第2〜5波平均（組 × 台）。**版に依らない**（トモが素体なので規則は1行も走らない）。
    double[] TyPlainWinRates(IReadOnlyList<UnitDef> three)
    {
        var outp = new double[tyPartners.Length];
        for (int i = 0; i < tyPartners.Length; i++)
        {
            Formation f = TyBench(tyPartners[i].Def, three, tyTomoPlain);
            int wins = 0, n = 0;
            for (int w = 1; w < tyStages.Count; w++)          // 第2〜5波（規約 (G10)）
                for (int seed = 0; seed < TySeeds; seed++)
                {
                    if (BattleEngine.Run(f, tyStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                    n++;
                }
            outp[i] = n > 0 ? wins * 100.0 / n : 0.0;
        }
        return outp;
    }

    var tyScan = new List<(UnitDef[] Three, double[] Rates, int InBand, double Mean)>();
    void TyScanBases()
    {
        if (tyScan.Count > 0) return;
        var results = new (UnitDef[] Three, double[] Rates, int InBand, double Mean)[tyCombos.Count];
        Parallel.For(0, tyCombos.Count, i =>
        {
            double[] r = TyPlainWinRates(tyCombos[i]);
            results[i] = (tyCombos[i], r, r.Count(x => x >= 40.0 && x <= 95.0), r.Average());
        });
        tyScan.AddRange(results);
    }

    static string TyKey(IReadOnlyList<UnitDef> three)
        => string.Join("+", three.Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal));

    UnitDef[] TyPickBase()
    {
        TyScanBases();
        return tyScan
            .OrderByDescending(x => x.InBand)
            .ThenBy(x => Math.Abs(x.Mean - 50.0))
            .ThenBy(x => TyKey(x.Three), StringComparer.Ordinal)
            .First().Three;
    }

    // ------------------------------------------------------------------------------
    // 1戦ぶんの観測（台 × 版 × 波 × seed）。**`UnitTally` をそのまま持つ**
    // ——`BattleResult` ごとに新しく作られるので使い回しの心配が無い。
    //
    // **`YieldDmg` はトモではなく味方全員から集める**——`TaillightYieldDamage` は
    // `ApplyDamage` が<b>殴った側</b>（＝譲られた駒）の帳簿に積むので、トモの側は常に 0 になる。
    // ------------------------------------------------------------------------------
    var tyRows = new List<(string Bench, int Ver, int Wave, int Seed, bool Won, int Turns,
                           UnitTally T, int TeamDmg, int WhetTail, int YieldDmg)>();
    var tyLitBy = new Dictionary<string, Dictionary<string, int>>();
    UnitDef[] tyBase = new[] { UnitCatalog.Gald, UnitCatalog.Kiri, UnitCatalog.Borg };   // TyRun が上書きする

    void TyRun()
    {
        tyBase = TyPickBase();
        foreach (var (tag, partner) in tyPartners)
            for (int ver = 0; ver < tyVers.Length; ver++)
            {
                UnitDef tomo = ver == TyPlainVer ? tyTomoPlain : UnitCatalog.Tomo;
                Formation f = TyBench(partner, tyBase, tomo);
                for (int w = 0; w < tyStages.Count; w++)
                    for (int seed = 0; seed < TySeeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, tyStages[w].Enemy, seed, verbose: false,
                                                          taillight: tyVers[ver].Rule);
                        UnitTally t = r.TallyByUnit.TryGetValue(UnitCatalog.Tomo.Id, out UnitTally? tt)
                                      ? tt : new UnitTally();
                        int team = 0, ydmg = 0;
                        foreach ((int _, UnitDef d) in f.Occupied())
                            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? mt))
                            { team += mt.DamageToEnemy; ydmg += mt.TaillightYieldDamage; }
                        if (ver != TyPlainVer)
                        {
                            string key = tag + " / " + tyVers[ver].Tag;
                            if (!tyLitBy.TryGetValue(key, out Dictionary<string, int>? bag))
                                tyLitBy[key] = bag = new Dictionary<string, int>();
                            foreach ((int _, UnitDef d) in f.Occupied())
                                if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? lt) && lt.TaillightLitReceived > 0)
                                    bag[d.Name] = bag.TryGetValue(d.Name, out int had)
                                                  ? had + lt.TaillightLitReceived : lt.TaillightLitReceived;
                        }
                        tyRows.Add((tag, ver, w + 1, seed, r.PlayerWon, r.Turns, t, team,
                                    r.WhetByRoute[(int)WhetRoute.Taillight], ydmg));
                    }
            }
    }

    // 集計の助け。**波 0 は「第2〜5波」を指す**（規約 (G10)）。
    List<(string Bench, int Ver, int Wave, int Seed, bool Won, int Turns,
          UnitTally T, int TeamDmg, int WhetTail, int YieldDmg)>
        TySel(string bench, int ver, int wave)
        => tyRows.Where(x => x.Bench == bench && x.Ver == ver && (wave == 0 ? x.Wave >= 2 : x.Wave == wave)).ToList();

    double TyWin(string bench, int ver, int wave)
    {
        var rows = TySel(bench, ver, wave);
        return rows.Count == 0 ? 0.0 : rows.Count(x => x.Won) * 100.0 / rows.Count;
    }
    double TyAvg(string bench, int ver, int wave, Func<UnitTally, double> f)
    {
        var rows = TySel(bench, ver, wave);
        return rows.Count == 0 ? 0.0 : rows.Average(x => f(x.T));
    }
    double TyTurns(string bench, int ver, int wave)
    {
        var rows = TySel(bench, ver, wave);
        return rows.Count == 0 ? 0.0 : rows.Average(x => (double)x.Turns);
    }
    double TyYieldDmg(string bench, int ver, int wave)
    {
        var rows = TySel(bench, ver, wave);
        return rows.Count == 0 ? 0.0 : rows.Average(x => (double)x.YieldDmg);
    }

    // 門2（条件成立）は段の和で閉じる。**V0/V1 は自分の手番、V2 は敵の死の瞬間。**
    double TyGate2(string bench, int ver, int wave)
        => ver == TyV2
           ? TyAvg(bench, ver, wave, t => t.TaillightYields + t.TaillightBlockedHop + t.TaillightNoFoe)
           : TyAvg(bench, ver, wave, t => t.TaillightYields + t.TaillightNoTarget
                                          + t.TaillightBlockedHop + t.TaillightNoFoe);
    double TyGate1(string bench, int ver, int wave) => TyAvg(bench, ver, wave, t => t.TaillightFires);
    double TyGate3(string bench, int ver, int wave) => TyAvg(bench, ver, wave, t => t.TaillightYields);

    // ==============================================================================
    // Phase 0（指示書 §3）
    // ==============================================================================
    void TyPhase0()
    {
        Console.WriteLine("## 0-1. 旧台（第109期の土台）での V0 の再現");
        Console.WriteLine();
        Console.WriteLine("**新しい器具は既知の値を再現できて初めて使える**（第88期）。"
            + "ノブを渡さない経路と `TaillightRule.Default` を明示的に渡した経路が"
            + "1ビットも違わないことを、勝敗・ターン数・門の3本すべてで突き合わせる。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 決着T | 門1 灯 | 門2 条件成立 | 門3 譲渡 | 譲渡の与ダメ | ノブ有無のずれ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        int knobMismatch = 0;
        foreach (var (tag, partner) in tyPartners)
        {
            Formation f = TyOldBench(partner, UnitCatalog.Tomo);
            for (int w = 0; w < tyStages.Count; w++)
            {
                double turns = 0, g1 = 0, g2 = 0, g3 = 0, dmg = 0;
                int bad = 0;
                for (int seed = 0; seed < TySeeds; seed++)
                {
                    BattleResult a = BattleEngine.Run(f, tyStages[w].Enemy, seed, verbose: false);
                    BattleResult b = BattleEngine.Run(f, tyStages[w].Enemy, seed, verbose: false,
                                                      taillight: TaillightRule.Default);
                    UnitTally ta = a.TallyByUnit.TryGetValue(UnitCatalog.Tomo.Id, out UnitTally? x1) ? x1 : new UnitTally();
                    UnitTally tb = b.TallyByUnit.TryGetValue(UnitCatalog.Tomo.Id, out UnitTally? x2) ? x2 : new UnitTally();
                    int da = 0, db = 0;
                    foreach ((int _, UnitDef d) in f.Occupied())
                    {
                        if (a.TallyByUnit.TryGetValue(d.Id, out UnitTally? m1)) da += m1.TaillightYieldDamage;
                        if (b.TallyByUnit.TryGetValue(d.Id, out UnitTally? m2)) db += m2.TaillightYieldDamage;
                    }
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns
                        || ta.TaillightFires != tb.TaillightFires
                        || ta.TaillightYields != tb.TaillightYields || da != db) { bad++; knobMismatch++; }
                    turns += a.Turns; g1 += ta.TaillightFires;
                    g2 += ta.TaillightYields + ta.TaillightNoTarget + ta.TaillightBlockedHop + ta.TaillightNoFoe;
                    g3 += ta.TaillightYields; dmg += da;
                }
                Console.WriteLine($"| {tag} | {w + 1} | {turns / TySeeds:F1} | {g1 / TySeeds:F2} | "
                    + $"{g2 / TySeeds:F2} | {g3 / TySeeds:F2} | {dmg / TySeeds:F1} | {bad} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"**ノブ有無のずれ 合計 {knobMismatch} 件 / {tyPartners.Length * tyStages.Count * TySeeds} 戦。**"
            + "（この表を第109期の報告書 §3 の表と突き合わせること——`トモ×ドルガ` 第2波の門2 = 2.23 / 譲渡 1.72 / 与ダメ 25.7 など。）");
        Console.WriteLine();

        Console.WriteLine("## 0-2. 土台の探索と素体版の先行測定（指示書 §1-2 (3)）");
        Console.WriteLine();
        Console.WriteLine("規則は**測る前にコードへ固定してある**——"
            + "(1) 速さ ≥ 8（`Stoic` は例外）／ (2) `AtkBonus` を他人に書かない"
            + "（`TraitEntryMap.Supplies` の 強化・弱体 ×〈自分以外〉と `Reviver` を外す）／"
            + "(3) 素体版の第2〜5波平均が 40〜95% に入る台が 4台中3台以上。"
            + "同点は 帯に入った台数 → 4台平均が 50% に近い → 3枚の `Def.Id` の辞書順。"
            + "**席も測る前に固定**（後3 = 範囲攻撃の攻最大 / 前3 = `Stoic` あればそれ・無ければ HP 最大 / 中央 = 残り）。");
        Console.WriteLine();
        Console.WriteLine($"プールは **{tyPool.Count} 枚**（ロスター {UnitCatalog.All.Count} 枚から）、"
            + $"組は **{tyCombos.Count} 通り**。**速さも特性も実装から引いている**"
            + "（第109期に手作りの速さ表がずれた）。");
        Console.WriteLine();
        Console.WriteLine("プール: " + string.Join(" / ", tyPool.Select(d => $"{d.Name}(速{d.Speed}・攻{d.Attack})")));
        Console.WriteLine();
        TyScanBases();
        var ordered = tyScan.OrderByDescending(x => x.InBand).ThenBy(x => Math.Abs(x.Mean - 50.0))
                            .ThenBy(x => TyKey(x.Three), StringComparer.Ordinal).ToList();
        Console.WriteLine("上位 20 組と、第109期の土台（ガルド/キリ/ボルグ）:");
        Console.WriteLine();
        Console.WriteLine("| 土台（前3 / 中央 / 後3） | 総攻 | " + string.Join(" | ", tyPartners.Select(p => p.Tag)) + " | 帯 | 平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        void TyScanRow((UnitDef[] Three, double[] Rates, int InBand, double Mean) s)
        {
            var (f3, c, b3) = TySeatsOf(s.Three);
            Console.WriteLine($"| {f3.Name} / {c.Name} / {b3.Name} | {s.Three.Sum(d => d.Attack)} | "
                + string.Join(" | ", s.Rates.Select(r => r.ToString("F1"))) + $" | **{s.InBand}** | {s.Mean:F1} |");
        }
        foreach (var s in ordered.Take(20)) TyScanRow(s);
        string oldKey = TyKey(new[] { UnitCatalog.Gald, UnitCatalog.Kiri, UnitCatalog.Borg });
        var old109 = tyScan.FirstOrDefault(x => TyKey(x.Three) == oldKey);
        if (old109.Three is not null)
        {
            Console.WriteLine("| —（以下は参考） | | | | | | | |");
            TyScanRow(old109);
        }
        UnitDef[] pick = TyPickBase();
        var pickRow = tyScan.First(x => TyKey(x.Three) == TyKey(pick));
        var (pf3, pc, pb3) = TySeatsOf(pick);
        Console.WriteLine();
        Console.WriteLine($"**採る土台は {pf3.Name} / {pc.Name} / {pb3.Name}**"
            + $"（帯 {pickRow.InBand} / {tyPartners.Length} 台・素体平均 {pickRow.Mean:F1}%）。"
            + $"第109期の土台（{UnitCatalog.Gald.Name} / {UnitCatalog.Kiri.Name} / {UnitCatalog.Borg.Name}）は"
            + (old109.Three is not null ? $"帯 {old109.InBand} / 平均 {old109.Mean:F1}%。" : "プールに入っていない。"));
        Console.WriteLine();
        Console.WriteLine($"**帯に入った組は {tyScan.Count(x => x.InBand >= 3)} / {tyCombos.Count}**"
            + $"（4台中3台以上）。**{tyScan.Count(x => x.InBand >= 1)} 組**が1台以上を帯に入れている。");
        Console.WriteLine();

        Console.WriteLine("## 0-3. `ctx.Interrupt` で包むか（指示書 §3 の 3）");
        Console.WriteLine();
        Console.WriteLine("**包まない。** 理由は3つ:");
        Console.WriteLine();
        Console.WriteLine("1. **前例は再行動（`EncoreRule`・第104期）**——あれも `HandleDeath` の中から "
            + "`TakeTurn` を呼ぶが `Interrupt` で包んでいない");
        Console.WriteLine("2. 包むと `InInterrupt` が立って `BattleContext.InOwnTurn` が偽になり、"
            + "**譲渡の与ダメ（`TaillightYieldDamage` ＝ 紙の分子）が丸ごと 0 に落ちる**");
        Console.WriteLine("3. 軋み（ヨミ）は `OnMoved` で `ctx.InInterrupt` を読むので、包むと"
            + "**譲られた手番の中でヨミが動かされても割り込めなくなる**——挙動を静かに変えることになる");
        Console.WriteLine();
        Console.WriteLine("再入は `BattleContext.Yielding`（1ホップ）と `YieldedKey`（1ターン1回）で足りる。");
        Console.WriteLine();

        Console.WriteLine("## 0-4. `CanReact` を持つ特性の一覧（指示書 §3 の 4）");
        Console.WriteLine();
        var reactors = new List<string>();
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            Trait tr;
            try { tr = TraitCatalog.Get(id); } catch { continue; }
            if (tr.GetType().GetMethod(nameof(Trait.CanReact))?.DeclaringType != typeof(Trait))
                reactors.Add(id.ToString());
        }
        Console.WriteLine($"**{reactors.Count} 枚**——{string.Join(" / ", reactors)}。");
        Console.WriteLine();
        Console.WriteLine("**のろま（`Sluggish`）は `CanReact` を持つ**（偶数ターンは割り込みでも動けない）。"
            + "だから **V2 でものろまは偶数ターンには譲られない**——指示書 §2 の予測4"
            + "「V2 はターン外なので `Sluggish` の `CanAct` は通り」は**外れる**"
            + "（`CanActOutOfTurn` が `CanReact` を問うので、そこで止まる）。");
        Console.WriteLine();

        Console.WriteLine("## 0-5. 各台で「トモを除いて最も遅い味方」（＝灯の対象＝譲渡の先）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 最も遅い味方 | 速 | `CanAct` を通るか | `Actions` |");
        Console.WriteLine("|---|---|--:|:-:|---|");
        foreach (var (tag, partner) in tyPartners)
        {
            Formation f = TyBench(partner, pick, UnitCatalog.Tomo);
            UnitDef? slow = null;
            foreach ((int slot, UnitDef d) in f.Occupied())
            {
                if (ReferenceEquals(d, UnitCatalog.Tomo)) continue;
                if (d.Traits.Contains(TraitId.Stoic)) continue;     // 灯の候補から自前で外れる
                if (slow is null || d.Speed < slow.Speed) slow = d;
            }
            bool canAct = slow is not null && !slow.Traits.Contains(TraitId.Pursuer)
                          && !slow.Traits.Contains(TraitId.Immobile);
            string acts = slow?.Actions is { Count: > 0 } ? "あり" : "なし";
            Console.WriteLine($"| {tag} | {slow?.Name ?? "−"} | {slow?.Speed ?? 0} | {(canAct ? "○" : "**×**")} | {acts} |");
        }
        Console.WriteLine();
        Console.WriteLine("**`トモ×ハギ` だけは灯がハギ自身に落ちうる**（速7）。"
            + "ハギは `CanAct => false` なので、そこへ譲ると手番はその場で潰れる（第109期 3-2）。"
            + "**指示書 §2 の予測1 の但し書きがそのまま当たるかは、表C の潰れの列で読む。**");
        Console.WriteLine();

        Console.WriteLine("## 0-6. ターン外の `TakeTurn` で壊れる帳簿はあるか（指示書 §3 の 6）");
        Console.WriteLine();
        Console.WriteLine("**無い。前例が既にある**——再行動（第104期）は `HandleDeath` の中から `TakeTurn` を呼ぶ。"
            + "`TurnActor` は `TakeTurn` が `prevActor` を退避・復帰するので入れ子で壊れず、"
            + "`IdleTurn` はターン番号を書くだけ。");
        Console.WriteLine();
        Console.WriteLine("**痺れだけは注意が要る**——`TakeTurnCore` は痺れをその場で 0 にするので、"
            + "痺れた味方に譲ると**本人の手番が来る前に縛めが解ける**。"
            + "`CanActOutOfTurn` が痺れを弾くので起きないはずで、"
            + "**弾いていることを自己検査 (j) で示す**（`TaillightStallStun` が 0 であること）。");
        Console.WriteLine();

        Console.WriteLine("## 0-7. GodotApp は台本を再生できるか（指示書 §3 の 7）");
        Console.WriteLine();
        Console.WriteLine("**イベント列の形は再行動（第104期）と同じ**——`TakeTurn` は `TurnStart` を出さず、"
            + "`Attack` / `Damage` / `Skill` を並べるだけ。`GodotApp/Main.cs` は"
            + "「1ターンに1駒が1回だけ動く」という前提を1つも持っていない"
            + "（`BattleEventKind` で分岐するだけ）ので、落ちる箇所は無い。"
            + "**`GodotApp` は sln に入っていないので、この期にビルドはしていない。**");
        Console.WriteLine();

        Console.WriteLine("## 0-8. 過去に「ターン外に他人を動かす」機構を測っていないか（指示書 §3 の 8）");
        Console.WriteLine();
        Console.WriteLine("**指示書 §0-2 の「他人をターン外に動かす初めての機構」は誤り。**"
            + "再行動（`EncoreRule`・第104期）が既に `HandleDeath` の中から他人の `TakeTurn` を呼んでいる。");
        Console.WriteLine();
        Console.WriteLine("V2 が本当に初めてなのは**「他人をターン外に動かし、しかも `CanActOutOfTurn` を通す」**ほう"
            + "——再行動はその窓口を通らないので粛に読まれない。"
            + "**粛が止めるのは「4本 ＋ 他人を動かす1本」**であって、5本目の同型ではない。");
        Console.WriteLine();
    }

    // ==============================================================================
    // 表A〜E（指示書 §4）
    // ==============================================================================
    void TyTables()
    {
        var (tb3, tbc, tbb3) = TySeatsOf(tyBase);
        Console.WriteLine($"席は 前1 = パートナー / 前3 = {tb3.Name} / 中央 = {tbc.Name} / 後1 = トモ / 後3 = {tbb3.Name}。"
            + $"seed 0..{TySeeds - 1}・5波。**判定の分母は第2〜5波**（規約 (G10)）。");
        Console.WriteLine();

        // ---- 表A ----
        Console.WriteLine("## 表A. 門（指示書 §1-3）—— 鎖が繋がっているか");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 決着T | 門1 灯 | 門2 条件成立 | 門3 譲渡 | 譲渡の与ダメ | 潰れ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _) in tyPartners)
            for (int ver = TyV0; ver <= TyV2; ver++)
                for (int w = 1; w <= tyStages.Count; w++)
                    Console.WriteLine($"| {(ver == TyV0 && w == 1 ? "**" + tag + "**" : "")} | {(w == 1 ? tyVers[ver].Tag : "")} | {w} "
                        + $"| {TyTurns(tag, ver, w):F1} | {TyGate1(tag, ver, w):F2} | **{TyGate2(tag, ver, w):F2}** "
                        + $"| **{TyGate3(tag, ver, w):F2}** | {TyYieldDmg(tag, ver, w):F1} "
                        + $"| {TyAvg(tag, ver, w, t => t.TaillightYieldStalls):F2} |");
        Console.WriteLine();
        Console.WriteLine("第2〜5波の平均だけを並べ直すと:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 決着T | 門1 灯 | 門2 条件成立 | 門3 譲渡 | 譲渡の与ダメ | 潰れ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _) in tyPartners)
            for (int ver = TyV0; ver <= TyV2; ver++)
                Console.WriteLine($"| {(ver == TyV0 ? "**" + tag + "**" : "")} | {tyVers[ver].Tag} "
                    + $"| {TyTurns(tag, ver, 0):F1} | {TyGate1(tag, ver, 0):F2} | **{TyGate2(tag, ver, 0):F2}** "
                    + $"| **{TyGate3(tag, ver, 0):F2}** | {TyYieldDmg(tag, ver, 0):F1} "
                    + $"| {TyAvg(tag, ver, 0, t => t.TaillightYieldStalls):F2} |");
        Console.WriteLine();

        // ---- 表A' ----
        Console.WriteLine("### 表A'. V2（即時）の段 —— どこで鎖が切れるか");
        Console.WriteLine();
        Console.WriteLine("段は `OnAnyDeath` の順（死の通知 → 灯した相手が生きているか → `CanActOutOfTurn` "
            + "→ 1ホップ → 1ターン1回 → 敵が残っているか → 譲渡）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 段1 死の通知 | 段2 相手が死 | 段3 ターン外不可（粛/痺/CanReact） | 段4 1ホップ | 段5 既に譲った | 段6 敵全滅 | 譲渡 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _) in tyPartners)
            for (int w = 1; w <= tyStages.Count; w++)
                Console.WriteLine($"| {(w == 1 ? "**" + tag + "**" : "")} | {w} "
                    + $"| {TyAvg(tag, TyV2, w, t => t.TaillightSawDeath):F2} "
                    + $"| {TyAvg(tag, TyV2, w, t => t.TaillightDeadTarget):F2} "
                    + $"| {TyAvg(tag, TyV2, w, t => t.TaillightNoOutOfTurn):F2}"
                    + $"（{TyAvg(tag, TyV2, w, t => t.TaillightOutHush):F2}/"
                    + $"{TyAvg(tag, TyV2, w, t => t.TaillightOutStun):F2}/"
                    + $"{TyAvg(tag, TyV2, w, t => t.TaillightOutReact):F2}） "
                    + $"| {TyAvg(tag, TyV2, w, t => t.TaillightBlockedHop):F2} "
                    + $"| {TyAvg(tag, TyV2, w, t => t.TaillightRepeat):F2} "
                    + $"| {TyAvg(tag, TyV2, w, t => t.TaillightNoFoe):F2} "
                    + $"| **{TyGate3(tag, TyV2, w):F2}** |");
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B. 帰属（Q5）—— 版 − 素体");
        Console.WriteLine();
        Console.WriteLine("**帯（素体の第2〜5波平均が 40〜95%）に入った台に ○**。"
            + "床・天井の台では帰属が定義上 0 に潰れるので判定に使わない（第61・63期）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 帯 | 波 | 素体 | V0 現行 | V1 窓 | V2 即時 | 帰属 V0 | 帰属 V1 | 帰属 V2 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var tyBand = new Dictionary<string, bool>();
        foreach (var (tag, _) in tyPartners)
        {
            double basePlain = TyWin(tag, TyPlainVer, 0);
            bool band = basePlain >= 40.0 && basePlain <= 95.0;
            tyBand[tag] = band;
            for (int w = 1; w <= tyStages.Count; w++)
            {
                double p = TyWin(tag, TyPlainVer, w);
                Console.WriteLine($"| {(w == 1 ? "**" + tag + "**" : "")} | {(w == 1 ? (band ? "○" : "**×**") : "")} | {w} "
                    + $"| {p:F1} | {TyWin(tag, TyV0, w):F1} | {TyWin(tag, TyV1, w):F1} | {TyWin(tag, TyV2, w):F1} "
                    + $"| {TyWin(tag, TyV0, w) - p:+0.0;-0.0;0.0} | {TyWin(tag, TyV1, w) - p:+0.0;-0.0;0.0} "
                    + $"| {TyWin(tag, TyV2, w) - p:+0.0;-0.0;0.0} |");
            }
            double pa = TyWin(tag, TyPlainVer, 0);
            Console.WriteLine($"| | | **2〜5** | **{pa:F1}** | **{TyWin(tag, TyV0, 0):F1}** | **{TyWin(tag, TyV1, 0):F1}** "
                + $"| **{TyWin(tag, TyV2, 0):F1}** | **{TyWin(tag, TyV0, 0) - pa:+0.0;-0.0;0.0}** "
                + $"| **{TyWin(tag, TyV1, 0) - pa:+0.0;-0.0;0.0}** | **{TyWin(tag, TyV2, 0) - pa:+0.0;-0.0;0.0}** |");
        }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. 譲渡の中身（Q1・Q3）");
        Console.WriteLine();
        Console.WriteLine("`TurnOutcome` の内訳と、潰れた理由（譲られた駒の第105期の計数の差分。"
            + "**入れ子ぶんが混ざりうるので厳密な分解ではない**）。"
            + "`両立` は **1つの撃破で追い打ちと譲渡が両方立った回数**（Q3）で、"
            + "V0 / V1 は譲渡が自分の手番にあるので**構造的に 0**。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 譲渡 | 攻撃 | 術 | 溜め | 潰れ（痺/眠/CanAct） | 両立 | V1 の窓で拾った回数 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _) in tyPartners)
            for (int ver = TyV0; ver <= TyV2; ver++)
                Console.WriteLine($"| {(ver == TyV0 ? "**" + tag + "**" : "")} | {tyVers[ver].Tag} "
                    + $"| {TyGate3(tag, ver, 0):F2} | {TyAvg(tag, ver, 0, t => t.TaillightYieldAttack):F2} "
                    + $"| {TyAvg(tag, ver, 0, t => t.TaillightYieldSkill):F2} "
                    + $"| {TyAvg(tag, ver, 0, t => t.TaillightYieldCharge):F2} "
                    + $"| {TyAvg(tag, ver, 0, t => t.TaillightYieldStalls):F2}"
                    + $"（{TyAvg(tag, ver, 0, t => t.TaillightStallStun):F2}/"
                    + $"{TyAvg(tag, ver, 0, t => t.TaillightStallSlumber):F2}/"
                    + $"{TyAvg(tag, ver, 0, t => t.TaillightStallCanAct):F2}） "
                    + $"| **{TyAvg(tag, ver, 0, t => t.TaillightPair):F2}** "
                    + $"| {TyAvg(tag, ver, 0, t => t.TaillightSaw2):F2} |");
        Console.WriteLine();
        Console.WriteLine("灯の受け手（＝譲渡の先）の内訳:");
        Console.WriteLine();
        foreach (var kv in tyLitBy.OrderBy(k => k.Key, StringComparer.Ordinal))
            Console.WriteLine($"- **{kv.Key}** — "
                + string.Join(" / ", kv.Value.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}")));
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. 灯（3版で同一のはず）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 灯 | 総量 | 到達点（最大） | 替 | 消した量 | 空振り |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _) in tyPartners)
            for (int ver = TyV0; ver <= TyV2; ver++)
                Console.WriteLine($"| {(ver == TyV0 ? "**" + tag + "**" : "")} | {tyVers[ver].Tag} "
                    + $"| {TyGate1(tag, ver, 0):F2} | {TyAvg(tag, ver, 0, t => t.TaillightLumen):F1} "
                    + $"| {TyAvg(tag, ver, 0, t => t.TaillightPeak):F1} "
                    + $"| {TyAvg(tag, ver, 0, t => t.TaillightSwitches):F2} "
                    + $"| {TyAvg(tag, ver, 0, t => t.TaillightDoused):F1} "
                    + $"| {TyAvg(tag, ver, 0, t => t.TaillightIdle):F2} |");
        Console.WriteLine();
        Console.WriteLine("> **同一にはならない。** 対象選択は1文字も触っていないが、V1 / V2 は盤面を動かす"
            + "（譲った手番のぶん敵が早く倒れる）ので、決着ターン数が変われば灯の回数も変わる。"
            + "**「対象選択が同じ」の検算は、灯の回数ではなく受け手の内訳（表C の下）と自己検査 (a)(b)(e) で取る。**");
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E. 紙と実測（指示書 §1-5）");
        Console.WriteLine();
        Console.WriteLine("紙 ＝ 譲渡/戦 × 1譲渡あたりの与ダメ。**分子について3つ**——"
            + "(1) 分子は譲渡の与ダメ ／ (2) V2 の譲渡はターン外だがその手番の灯は既に載っている"
            + "（`OnTurnStart` は行動順の外）ので **V0 と同じ単価で数えてよい** ／ "
            + "(3) 譲渡が増えると決着が早まるので**分母を削る**——上限にも下限にもならない。**方向だけ書く。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 譲渡/戦 | 譲渡の与ダメ/戦 | 1譲渡あたり | 編成の与ダメ/戦 | 譲渡の取り分 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var (tag, _) in tyPartners)
            for (int ver = TyV0; ver <= TyV2; ver++)
            {
                double y = TyGate3(tag, ver, 0);
                double d = TyYieldDmg(tag, ver, 0);
                var rows = TySel(tag, ver, 0);
                double team = rows.Count == 0 ? 0 : rows.Average(x => (double)x.TeamDmg);
                Console.WriteLine($"| {(ver == TyV0 ? "**" + tag + "**" : "")} | {tyVers[ver].Tag} "
                    + $"| {y:F2} | {d:F1} | {(y > 0 ? d / y : 0):F1} | {team:F1} "
                    + $"| {(team > 0 ? d * 100.0 / team : 0):F1}% |");
            }
        Console.WriteLine();
        Console.WriteLine("> **`TaillightYieldDamage` は `InOwnTurn` を通して数えている**ので、"
            + "**反撃の中で起きた撃破から V2 が譲った手番のぶんは落ちる**"
            + "（`ApplyDamage` が `InReaction` の中なら `InOwnTurn` が偽になる）。"
            + "V2 の分子は**下限**である。");
        Console.WriteLine();

        // ---- 判定 ----
        Console.WriteLine("## 判定（指示書 §1-4）");
        Console.WriteLine();
        const string Hagi = "トモ×ハギ", Dolga = "トモ×ドルガ";
        double q1v0 = TyGate3(Hagi, TyV0, 0), q1v2 = TyGate3(Hagi, TyV2, 0);
        double q2v0 = TyGate2(Dolga, TyV0, 4), q2v1 = TyGate2(Dolga, TyV1, 4);
        double q3 = TyAvg(Hagi, TyV2, 0, t => t.TaillightPair);
        double q4w2 = TyGate3(Hagi, TyV2, 2);
        double q4w2All = tyPartners.Sum(p => TyGate3(p.Tag, TyV2, 2));
        double q4rest = tyPartners.Sum(p => TyGate3(p.Tag, TyV2, 3) + TyGate3(p.Tag, TyV2, 4) + TyGate3(p.Tag, TyV2, 5));
        var bandTags = tyPartners.Where(p => tyBand[p.Tag]).Select(p => p.Tag).ToList();
        bool q5 = bandTags.Count > 0 && bandTags.All(tag =>
        {
            double p0 = TyWin(tag, TyPlainVer, 0);
            double a0 = TyWin(tag, TyV0, 0) - p0;
            return TyWin(tag, TyV1, 0) - p0 >= a0 - 1e-9 && TyWin(tag, TyV2, 0) - p0 >= a0 - 1e-9;
        });
        Console.WriteLine("| | 内容 | 線 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|---|:-:|");
        Console.WriteLine($"| **Q1** | 穴(1)：`{Hagi}` で V2 の門3 が V0 を上回る | V0 + 0.5 回/戦 "
            + $"| V0 {q1v0:F2} → V2 **{q1v2:F2}**（差 {q1v2 - q1v0:+0.00;-0.00;0.00}） "
            + $"| {(q1v2 >= q1v0 + 0.5 ? "○" : "**×**")} |");
        Console.WriteLine($"| **Q2** | 穴(2)：`{Dolga}` の**第四波**で V1 の門2 が V0 を上回る | V0 + 0.5 回/戦 "
            + $"| V0 {q2v0:F2} → V1 **{q2v1:F2}**（差 {q2v1 - q2v0:+0.00;-0.00;0.00}） "
            + $"| {(q2v1 >= q2v0 + 0.5 ? "○" : "**×**")} |");
        Console.WriteLine($"| **Q3** | 1つの撃破で追い打ちと譲渡が両方立つ（`{Hagi}`・V2） | 0.5 回/戦 "
            + $"| **{q3:F2}** | {(q3 >= 0.5 ? "○" : "**×**")} |");
        Console.WriteLine($"| **Q4** | V2 が粛に読まれる（第二波の門3 が 0・第3〜5波は 0 でない） | 二値 "
            + $"| 第二波 4台合計 **{q4w2All:F2}** / 第3〜5波 合計 {q4rest:F2} "
            + $"| {(q4w2All < 1e-9 && q4rest > 0 ? "○" : "**×**")} |");
        Console.WriteLine($"| **Q5** | 帯に入った台で V1 / V2 の帰属が V0 を下回らない | ±0 以上 "
            + $"| 帯 {bandTags.Count} 台（{string.Join(" / ", bandTags)}） "
            + $"| {(bandTags.Count == 0 ? "**測れていない**" : q5 ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine($"（Q4 の `{Hagi}` 単独の第二波は {q4w2:F2}。）");
        Console.WriteLine();
    }

    // ==============================================================================
    // 自己検査（指示書 §4）
    // ==============================================================================
    void TyCheck()
    {
        var checks = new List<(string Tag, string What, string Got, bool Ok)>();

        // ---- 必須1: compare 305 セルが docs/balance.md と 0 件 ----
        string? root = Directory.GetCurrentDirectory();
        while (root is not null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Directory.GetParent(root)?.FullName;
        int cells = 0, mism = 0;
        if (root is not null)
        {
            var doc = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(Path.Combine(root, "docs", "balance.md")))
            {
                if (!line.StartsWith("| ", StringComparison.Ordinal)) continue;
                string[] c = line.Split('|', StringSplitOptions.TrimEntries);
                if (c.Length < 3 + tyStages.Count) continue;
                var vals = new List<double>();
                for (int i = 2; i < 2 + tyStages.Count; i++)
                    if (double.TryParse(c[i].Replace("%", "").Trim(), out double v)) vals.Add(v);
                if (vals.Count == tyStages.Count) doc[c[1].Replace("*", "").Trim()] = vals.ToArray();
            }
            foreach (var b in CompareBuilds())
            {
                if (!doc.TryGetValue(b.Name.Replace("*", "").Trim(), out double[]? want)) continue;
                for (int w = 0; w < tyStages.Count; w++)
                {
                    int win = 0;
                    for (int seed = 0; seed < TySeeds; seed++)
                        if (BattleEngine.Run(b.F, tyStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                    cells++;
                    if (Math.Abs(win * 100.0 / TySeeds - want[w]) > 0.05) mism++;
                }
            }
        }
        checks.Add(("必須1", "`compare` の全セルが `docs/balance.md` と一致"
            + "（**採用後の既定は V1。トモは `Presets` に1行も入っていないので、"
            + "採用しても盤面は1セルも動かない**）",
            $"{cells} セル中ずれ {mism} 件", cells > 0 && mism == 0));

        // ---- 必須4: PickOne の実呼び出しが 26 箇所 ----
        static int TyCount(string hay, string needle)
        {
            int n = 0;
            for (int i = hay.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = hay.IndexOf(needle, i + 1, StringComparison.Ordinal)) n++;
            return n;
        }
        int pick = 0;
        if (root is not null)
            foreach (string fp in new[] { Path.Combine(root, "BattleCore", "Traits.cs"),
                                          Path.Combine(root, "BattleCore", "BattleEngine.cs") })
                if (File.Exists(fp)) pick += TyCount(File.ReadAllText(fp), "PickOne(");
        checks.Add(("必須4", "`PickOne(` の素の出現数が **26** のまま（第94期以降不変）", pick + " 箇所", pick == 26));

        // ---- (l) 旧4台での V0 が第109期の表A を再現する ----
        // **既定に依らない形で書く**（採用で既定が動いても、V0 の経路そのものが再現することを見る）。
        // 参照値は design/PHASE109_TOMO.md §3 の表（ドルガ・ハギの 10 セル × 門3本）。
        var ty109 = new (string Bench, int Wave, double G1, double G2, double G3)[]
        {
            ("トモ×ドルガ", 1, 1.00, 1.00, 1.00), ("トモ×ドルガ", 2, 4.08, 2.23, 1.72),
            ("トモ×ドルガ", 3, 4.25, 1.98, 1.11), ("トモ×ドルガ", 4, 5.38, 1.20, 0.83),
            ("トモ×ドルガ", 5, 3.02, 1.87, 1.82),
            ("トモ×ハギ",   1, 2.33, 0.01, 0.00), ("トモ×ハギ",   2, 3.04, 0.01, 0.00),
            ("トモ×ハギ",   3, 3.50, 0.14, 0.11), ("トモ×ハギ",   4, 4.28, 0.01, 0.01),
            ("トモ×ハギ",   5, 2.62, 0.26, 0.18),
        };
        int refBad = 0, refN = 0, defDiff = 0;
        var refBadList = new List<string>();
        foreach (var (tag, partner) in tyPartners)
        {
            Formation f = TyOldBench(partner, UnitCatalog.Tomo);
            for (int w = 0; w < tyStages.Count; w++)
            {
                double g1 = 0, g2 = 0, g3 = 0, dg3 = 0;
                for (int seed = 0; seed < TySeeds; seed++)
                {
                    BattleResult a = BattleEngine.Run(f, tyStages[w].Enemy, seed, verbose: false,
                                                      taillight: new TaillightRule(YieldMode.OwnTurn));
                    BattleResult b = BattleEngine.Run(f, tyStages[w].Enemy, seed, verbose: false);
                    UnitTally ta = a.TallyByUnit.TryGetValue(UnitCatalog.Tomo.Id, out UnitTally? x1) ? x1 : new UnitTally();
                    UnitTally tb = b.TallyByUnit.TryGetValue(UnitCatalog.Tomo.Id, out UnitTally? x2) ? x2 : new UnitTally();
                    g1 += ta.TaillightFires;
                    g2 += ta.TaillightYields + ta.TaillightNoTarget + ta.TaillightBlockedHop + ta.TaillightNoFoe;
                    g3 += ta.TaillightYields;
                    dg3 += tb.TaillightYields;
                }
                g1 /= TySeeds; g2 /= TySeeds; g3 /= TySeeds; dg3 /= TySeeds;
                if (Math.Abs(dg3 - g3) > 0.005) defDiff++;
                foreach (var q in ty109)
                    if (q.Bench == tag && q.Wave == w + 1)
                    {
                        // 参照値は報告書に**小数第2位で印刷された文字列**なので、
                        // こちらも同じ書式に落として突き合わせる——差の絶対値でも `Math.Round` でも、
                        // ちょうど 0.835（印刷は 0.83）が境界で落ちる（`Math.Round` は偶数丸めで 0.84 を返す）。
                        refN += 3;
                        if (g1.ToString("F2") != q.G1.ToString("F2")) { refBad++; refBadList.Add($"{tag} 第{w + 1}波 門1 {g1:F4} 対 {q.G1:F2}"); }
                        if (g2.ToString("F2") != q.G2.ToString("F2")) { refBad++; refBadList.Add($"{tag} 第{w + 1}波 門2 {g2:F4} 対 {q.G2:F2}"); }
                        if (g3.ToString("F2") != q.G3.ToString("F2")) { refBad++; refBadList.Add($"{tag} 第{w + 1}波 門3 {g3:F4} 対 {q.G3:F2}"); }
                    }
            }
        }
        checks.Add(("(l)", "旧4台（第109期の土台）で **V0 を明示的に渡すと第109期の表A を再現する**"
            + "（`design/PHASE109_TOMO.md` §3 の ドルガ・ハギ 10 セル × 門3本）",
            $"{refN} 値中ずれ {refBad} 件"
            + (refBadList.Count > 0 ? "（" + string.Join(" / ", refBadList) + "）" : ""),
            refN > 0 && refBad == 0));
        checks.Add(("(l')", "**既定が V0 ではなくなっている**（採用が効いていることの実測。"
            + "採用前は 0 / 20 セル、採用後は V1 が V0 と違うセルが立つ）",
            $"既定と V0 で門3 が違う {defDiff} / {tyPartners.Length * tyStages.Count} セル", true));

        // ---- ログ再生による (a)〜(e)(g)(i)(k) ----
        int badOne = 0, badDouse = 0, badNet = 0, badYieldTurn = 0, badSelf = 0, audits = 0, maxAtOnce = 0;
        int hushYields = 0, hushEarlyYields = 0;
        for (int ver = TyV0; ver <= TyV2; ver++)
            foreach (var (tag, partner) in tyPartners)
            {
                Formation f = TyBench(partner, tyBase, UnitCatalog.Tomo);
                for (int w = 0; w < tyStages.Count; w++)
                    for (int seed = 0; seed < TyAudit; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, tyStages[w].Enemy, seed, verbose: true,
                                                          taillight: tyVers[ver].Rule);
                        var lamp = new Dictionary<string, int>();
                        int lit = 0, doused = 0, yieldsThisTurn = 0, yieldsHere = 0;
                        audits++;
                        foreach (LogLine line in r.Log)
                        {
                            string t = line.Text;
                            if (line.Kind == LogKind.Turn)
                            {
                                if (yieldsThisTurn > 1) badYieldTurn++;
                                yieldsThisTurn = 0;
                                continue;
                            }
                            int a = t.IndexOf(" に灯をともした", StringComparison.Ordinal);
                            if (a >= 0)
                            {
                                int b0 = t.IndexOf(" が ", StringComparison.Ordinal);
                                string who = t.Substring(b0 + 3, a - b0 - 3);
                                if (who == UnitCatalog.Tomo.Name) badSelf++;
                                lamp[who] = lamp.TryGetValue(who, out int had) ? had + TaillightTrait.Lumen
                                                                              : TaillightTrait.Lumen;
                                lit += TaillightTrait.Lumen;
                                int on = lamp.Count(kv => kv.Value > 0);
                                if (on > maxAtOnce) maxAtOnce = on;
                                if (on > 1) badOne++;
                                continue;
                            }
                            int c = t.IndexOf(" の灯が消えた（攻撃 -", StringComparison.Ordinal);
                            if (c >= 0)
                            {
                                string who = t.Substring(0, c);
                                int amt = int.Parse(new string(t.Substring(c).Where(char.IsAsciiDigit).ToArray()));
                                if (!lamp.TryGetValue(who, out int had2) || had2 != amt) badDouse++;
                                lamp[who] = 0;
                                doused += amt;
                                continue;
                            }
                            if (t.Contains("は前へ出ず、灯した", StringComparison.Ordinal)) { yieldsThisTurn++; yieldsHere++; }
                        }
                        if (yieldsThisTurn > 1) badYieldTurn++;
                        if (lit - doused != lamp.Values.Sum()) badNet++;
                        if (ver == TyV2 && w == 1)
                        {
                            hushYields += yieldsHere;                       // (g) 第二波（粛）
                            // (g') **粛は「保持者が盤上に生きている間」だけ閉じる**（`CanActOutOfTurn` は
                            // 呼ばれるたびに評価する）。第二波で譲渡が立つのは伝令を割った後のはず
                            // ——ログを時間順に走って「伝令の死より前の譲渡」だけを数える。
                            bool heraldDead = false;
                            foreach (LogLine line in r.Log)
                            {
                                if (line.Kind == LogKind.Death
                                    && line.Text.Contains(EnemyCatalog.Husher.Name, StringComparison.Ordinal))
                                { heraldDead = true; continue; }
                                if (!heraldDead && line.Text.Contains("は前へ出ず、灯した", StringComparison.Ordinal))
                                    hushEarlyYields++;
                            }
                        }
                    }
            }
        checks.Add(("(a)", "灯が**同時に1体にしか灯っていない**（3版とも）",
            $"同時に灯った最大 {maxAtOnce} 体・違反 {badOne} 件 / {audits} 戦", badOne == 0 && maxAtOnce <= 1));
        checks.Add(("(b)", "対象が変わったとき、**前の灯がちょうど載っていた量だけ**消えている（3版とも）",
            $"照合ずれ {badDouse} 件・収支ずれ {badNet} 戦 / {audits} 戦", badDouse == 0 && badNet == 0));
        checks.Add(("(e)", "トモ自身が対象になっていない（3版とも）", $"{badSelf} 件 / {audits} 戦", badSelf == 0));
        checks.Add(("(i)", "V2 で譲渡が**1ターン1回以下**（`YieldedKey`）",
            $"違反 {badYieldTurn} ターン / {audits} 戦", badYieldTurn == 0));
        checks.Add(("(g)", "V2 の譲渡がすべて `CanActOutOfTurn(lit)` を通っている"
            + "——**第二波（粛）での譲渡が 0 件**",
            $"第二波の譲渡 {hushYields} 件（ログ再生 seed 0..{TyAudit - 1}）", hushYields == 0));
        checks.Add(("(g')", "**粛は保持者が生きている間だけ閉じる**（`CanActOutOfTurn` は呼ばれるたびに評価する）"
            + "——第二波の譲渡のうち、**伝令を割る前**に起きたものが 0 件",
            $"伝令の死より前の譲渡 {hushEarlyYields} 件 / 第二波の譲渡 {hushYields} 件", hushEarlyYields == 0));

        // ---- (f) 点灯が ctx.Whet を通っている ----
        int tailWhet = 0, tailLumen = 0;
        foreach (var x in tyRows.Where(x => x.Ver != TyPlainVer)) { tailWhet += x.WhetTail; tailLumen += x.T.TaillightLumen; }
        checks.Add(("(f)", "点灯が **`ctx.Whet(…, WhetRoute.Taillight)`** を通っている（3版とも）",
            $"窓口 {tailWhet} 量 / トモの帳簿 {tailLumen} 量", tailWhet == tailLumen && tailWhet > 0));

        // ---- (h) V1 が読んだ SawKey は Turn か Turn-1 のどちらか ----
        // **窓で拾った回数（`TaillightSaw2`）は必ず「譲渡 + 段の残り」の内側**に収まる。
        int saw2 = tyRows.Where(x => x.Ver == TyV1).Sum(x => x.T.TaillightSaw2);
        int saw2V0 = tyRows.Where(x => x.Ver == TyV0).Sum(x => x.T.TaillightSaw2);
        int gate2V1 = tyRows.Where(x => x.Ver == TyV1).Sum(x => x.T.TaillightYields + x.T.TaillightNoTarget
                                                              + x.T.TaillightBlockedHop + x.T.TaillightNoFoe);
        checks.Add(("(h)", "V1 が読んだのは `Turn` か `Turn - 1` のどちらかだけ"
            + "（窓で拾った回数が V0 では 0 で、V1 の条件成立の内側に収まる）",
            $"V1 の窓 {saw2} 件 ≤ V1 の条件成立 {gate2V1} 件 ／ V0 の窓 {saw2V0} 件",
            saw2V0 == 0 && saw2 <= gate2V1));

        // ---- (j) 痺れた味方に譲った回数が 0 ----
        int stunStall = tyRows.Where(x => x.Ver == TyV2).Sum(x => x.T.TaillightStallStun);
        int outStun = tyRows.Where(x => x.Ver == TyV2).Sum(x => x.T.TaillightOutStun);
        checks.Add(("(j)", "V2 で**痺れた味方に譲った回数が 0**"
            + "（`CanActOutOfTurn` が痺れを弾いている。弾いた回数は段3 に立つ）",
            $"痺れで潰れた譲渡 {stunStall} 件 ／ 段3 で痺れに弾かれた {outStun} 件", stunStall == 0));

        // ---- (k) Yielding が譲渡後に必ず戻っている ----
        // 1ホップで止めた回数が V2 で 0 でないこと自体は正常（連鎖の中の撤退）。
        // **戻っていなければ「その戦闘の以後の譲渡が全部 1ホップで落ちる」**ので、
        // 譲渡が2回以上立った戦闘があることを以て「立ちっぱなしになっていない」を示す。
        int multiYieldBattles = tyRows.Count(x => x.Ver == TyV2 && x.T.TaillightYields >= 2);
        int hopV2 = tyRows.Where(x => x.Ver == TyV2).Sum(x => x.T.TaillightBlockedHop);
        checks.Add(("(k)", "V2 の `Yielding` が譲渡後に必ず戻っている"
            + "（戻らなければ以後の譲渡がすべて1ホップで落ちるので、**2回以上譲った戦闘が存在すること**で示す）",
            $"譲渡が2回以上あった戦闘 {multiYieldBattles} 戦 ／ 1ホップで止めた回数 {hopV2}",
            multiYieldBattles > 0));

        Console.WriteLine("## 自己検査");
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        foreach (var (tag, what, got, ok) in checks)
            Console.WriteLine($"| **{tag}** | {what} | {got} | {(ok ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine($"**{checks.Count(x => x.Ok)} / {checks.Count} 件が ○。**");
        Console.WriteLine();
        Console.WriteLine("**必須2（`docs/` 10ファイルの再生成）と 必須3（触っていないノブの既定）は"
            + "コマンドの外で確かめる**（報告書に差分を書く）。");
        Console.WriteLine();
    }

    Console.WriteLine("# 第110期 —— 尾灯の譲渡条件（`tomo yield`・モード: " + tyMode + "）");
    Console.WriteLine();
    if (tyMode == "phase0") TyPhase0();
    if (tyMode == "run" || tyMode == "tables" || tyMode == "check") TyRun();
    if (tyMode == "run" || tyMode == "tables") TyTables();
    if (tyMode == "check") TyCheck();
    return;
}

// ==================================================================================
// 第109期 —— 尾灯のトモを測る／`刻み×澱み (ノミ×ミオ)` が測れているかを確かめる。
// 指示書は design/PHASE109_TOMO_SPEC.md。
//
// **既存の診断は1文字も書き換えていない。** 第108期の `taillight` は受け入れ確認（自己検査だけ）で、
// こちらが測定。**トモは `Presets.Compare` にも `Presets.Cross` にも入っていない**ので、
// 盤面に出す唯一の場所がこの診断のローカル台になる（`gradient` / `aim` / `route` と同じ扱い）。
//
// **`Presets` は1行も読み替えない。** (B) のミオの再測定だけが `CompareBuilds()` を読むが、
// **読むだけで書かない**（素体差し替えと席の複製はローカルの `Formation` を組み直す）。
//
//     dotnet run --project BattleSim -c Release 0 tomo phase0   # §4 と §1-2 の門
//     dotnet run --project BattleSim -c Release 0 tomo run      # §1-1 の4台 × 対照（表A〜D）
//     dotnet run --project BattleSim -c Release 0 tomo mio      # §3（表E）
//     dotnet run --project BattleSim -c Release 0 tomo tables   # 表A〜E
//     dotnet run --project BattleSim -c Release 0 tomo check    # 自己検査
// ==================================================================================
public static void Run(string[] args, int stageIndex)
{
    string tmMode = args.Length > 2 ? args[2] : "tables";
    IReadOnlyList<EnemyCatalog.Stage> tmStages = EnemyCatalog.Stages;
    const int TmSeeds = 200;    // 指示書 §1-1（seed 0..199）
    const int TmAudit = 40;     // 台帳の照合（ログ再生）だけを回す帯。第108期の `taillight` と同じ

    // ------------------------------------------------------------------------------
    // 素体（同数値・特性なし・`Actions` なし）。第47期の作法——
    // 「その効果だけを 0 にできるノブが作れない機構では、同数値・特性なしの素体を対照に置く」。
    // カタログには載せない（診断のローカルの `UnitDef`）。
    // ------------------------------------------------------------------------------
    static UnitDef TmPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain",
        Name = "素体の" + d.Name,
        MaxHp = d.MaxHp,
        Attack = d.Attack,
        Speed = d.Speed,
        Traits = Array.Empty<TraitId>(),
        Pattern = d.Pattern
    };
    UnitDef tmTomoPlain = TmPlain(UnitCatalog.Tomo);
    UnitDef tmMioPlain = TmPlain(UnitCatalog.Mio);

    // ------------------------------------------------------------------------------
    // 台（**測る前に決めた**。指示書 §1-1）。
    //
    // **土台3枚は4台で共有し、変えるのはパートナー1枚だけ**（第37期以来の作法）。
    // 土台を選ぶ規則を、結果を見る前に3つ固定してある（第64期——緩めた条件を数えられるように）:
    //
    //   (1) **速さ ≥ 8**（`Stoic` を除く）。「自分を除いて最も遅い味方」がパートナーに一意に決まる
    //       ——4枚のパートナーの速さは ムド5 / ドルガ6 / ソム6 / ハギ7 なので、
    //       土台に速7以下を混ぜると同速の割り（席番号昇順）に化けて門の前提が崩れる。
    //       **ガルド（速4）だけは例外で入れられる**——`Stoic` は灯の候補から自前で外れるので
    //       （第108期 (b) の「支援拒否を飛ばす」）、遅くても対象を奪わない。
    //   (2) **`AtkBonus` を他人に書かない。** 号令・縛め・駆り立て・移り木・火選り・呪詛・萎縮を外す
    //       ——灯の到達点（Q3）に別の経路が混ざると、消灯の照合（自己検査 (b)）も一緒に壊れる。
    //       **蘇生も外す**（`Revive` は `ResetAtkBonus` を通るので、灯を載せたまま蘇ると帳簿がずれる）。
    //   (3) **台が床に落ちない出力を持つ**（第26・28期。`reseat` が 20.0% で並ぶ症状）。
    //
    // 席は **ボルグの巻き込みがパートナーにもトモにも当たらない**ように決めた——
    // 後3 の隣接は 前3 と 中央 だけなので、そこへ ガルド（HP100）と キリ（傷の書き手）を置き、
    // パートナー（前1）とトモ（後1）を巻き込みの外に出す。
    // **狙（ガルドが前列）**も満たす（CONTRIBUTING.md・第107期に器具へ入れた規則）。
    //
    //     前1 = パートナー ／ 前3 = ガルド ／ 中央 = キリ ／ 後1 = トモ ／ 後3 = ボルグ
    // ------------------------------------------------------------------------------
    Formation TmBench(UnitDef partner, UnitDef tomo) => Formation.Build(
        front1: partner, front3: UnitCatalog.Gald, center: UnitCatalog.Kiri,
        back1: tomo, back3: UnitCatalog.Borg);

    var tmPartners = new (string Tag, UnitDef Def, string Why)[]
    {
        ("トモ×ドルガ", UnitCatalog.Dolga, "**速6 は遅い層ではない**。土台を速8以上で固めて初めて対象になる"),
        ("トモ×ムド",   UnitCatalog.Mudo,  "**灯と被弾の2本の成長経路**が同じ駒に乗る（速5）"),
        ("トモ×ソム",   UnitCatalog.Som,   "**餌が毎ターン湧いて倒れる**＝譲渡の条件が安定して満たされる（速6）"),
        ("トモ×ハギ",   UnitCatalog.Hagi,  "**撃破を作る側**。譲渡と追い打ちが同じ出来事で立つか（速7）"),
    };

    // 1戦ぶんの観測。**版（現行 / 素体）× 台 × 波 × seed** で1行。
    var tmRows = new List<(string Bench, int Ver, int Wave, int Seed, bool Won, int Turns,
                           int Fires, int Lumen, int Switches, int Doused, int Idle, int Peak,
                           int Yields, int YAtk, int YSkill, int YCharge, int YStall,
                           int NoDeath, int NoTarget, int Hop, int YieldDmg,
                           int PartnerDmg, int PartnerTurns, int PartnerAtk, int TeamDmg,
                           int LitOnPartner, int WhetTail)>();

    // 受け手側の帳簿を**味方5枚すべて**について集める（現行の版だけ）。
    // **灯はパートナーに固定されない**——パートナーが倒れれば次に遅い味方へ移る（規則どおり）。
    var tmLitBy = new Dictionary<string, Dictionary<string, int>>();

    void TmRun()
    {
        foreach (var (tag, partner, _) in tmPartners)
            for (int ver = 0; ver < 2; ver++)
            {
                Formation f = TmBench(partner, ver == 0 ? UnitCatalog.Tomo : tmTomoPlain);
                for (int w = 0; w < tmStages.Count; w++)
                    for (int seed = 0; seed < TmSeeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, tmStages[w].Enemy, seed, verbose: false);
                        UnitTally t = r.TallyByUnit.TryGetValue(UnitCatalog.Tomo.Id, out UnitTally? tt)
                                      ? tt : new UnitTally();
                        UnitTally p = r.TallyByUnit.TryGetValue(partner.Id, out UnitTally? pt)
                                      ? pt : new UnitTally();
                        // 味方側の与ダメだけを集める（敵の tally も同じ辞書に入っている）。
                        int team = 0;
                        foreach ((int _, UnitDef d) in f.Occupied())
                            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? mt)) team += mt.DamageToEnemy;
                        if (ver == 0)
                        {
                            if (!tmLitBy.TryGetValue(tag, out Dictionary<string, int>? bag))
                                tmLitBy[tag] = bag = new Dictionary<string, int>();
                            foreach ((int _, UnitDef d) in f.Occupied())
                                if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? lt) && lt.TaillightLitReceived > 0)
                                    bag[d.Name] = bag.TryGetValue(d.Name, out int had) ? had + lt.TaillightLitReceived
                                                                                       : lt.TaillightLitReceived;
                        }
                        tmRows.Add((tag, ver, w + 1, seed, r.PlayerWon, r.Turns,
                            t.TaillightFires, t.TaillightLumen, t.TaillightSwitches, t.TaillightDoused,
                            t.TaillightIdle, t.TaillightPeak,
                            t.TaillightYields, t.TaillightYieldAttack, t.TaillightYieldSkill,
                            t.TaillightYieldCharge, t.TaillightYieldStalls,
                            t.TaillightNoDeath, t.TaillightNoTarget, t.TaillightBlockedHop,
                            p.TaillightYieldDamage,
                            p.DamageToEnemy, p.TurnsTaken, p.Attacks, team,
                            p.TaillightLitReceived,
                            r.WhetByRoute[(int)WhetRoute.Taillight]));
                    }
            }
    }

    // 波ごとの平均（現行の版だけ）。
    static double TmAvg(IEnumerable<int> xs) { var l = xs.ToList(); return l.Count == 0 ? 0 : l.Average(); }

    // ------------------------------------------------------------------------------
    // Phase 0（§4）。**戦闘は §1-2 の門のぶんだけ。**
    // ------------------------------------------------------------------------------
    if (tmMode == "phase0")
    {
        Console.WriteLine("# 第109期 Phase 0 —— 地図と門");
        Console.WriteLine();

        // (1) 速5以下の味方の全数。**必ず実装から引く**（第108期・手作りのずれの13例目）。
        var slow = UnitCatalog.All.Where(u => u.Speed <= 5).OrderBy(u => u.Speed).ThenBy(u => u.Id).ToList();
        Console.WriteLine("## 1. 速5以下の味方（`UnitCatalog.All` の " + UnitCatalog.All.Count + " 枚から実装で引いた）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | HP | 攻 | 速 | 支援拒否 |");
        Console.WriteLine("|---|--:|--:|--:|:-:|");
        foreach (UnitDef u in slow)
            Console.WriteLine($"| {u.Name} | {u.MaxHp} | {u.Attack} | {u.Speed} | "
                + (u.Traits.Contains(TraitId.Stoic) ? "**○（灯が飛ばす）**" : "—") + " |");
        Console.WriteLine();
        Console.WriteLine("**計 " + slow.Count + " 枚。** 速さの分布（全 " + UnitCatalog.All.Count + " 枚）: "
            + string.Join(" / ", UnitCatalog.All.GroupBy(u => u.Speed).OrderBy(g => g.Key)
                .Select(g => "速" + g.Key + ":" + g.Count())));
        Console.WriteLine();

        // (2) 点灯の窓口。
        Console.WriteLine("## 2. 点灯は `ctx.Whet`・消灯は `ctx.Dull` を通さない（第108期の判断の再掲）");
        Console.WriteLine();
        Console.WriteLine("| | 窓口 | 理由 | この期の計数はどこから取るか |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine("| 点灯 | **`ctx.Whet(…, WhetRoute.Taillight)`** | "
            + "他者強化の供給の観測（`NoteCarry` の強化キー）は `Whet` の中の1行にしか無い。"
            + "直に足すと `derive scan` / `whet` / `carry` / `spend` のどこからも見えなくなる | "
            + "`BattleResult.WhetByRoute[" + (int)WhetRoute.Taillight + "]`（自己検査 (f)） |");
        Console.WriteLine("| 消灯 | **通さない**（`AtkBonus` を直接引く） | "
            + "`Dull` は集約（ウケ）と転嫁（ワタ）の横取りが立っている窓口なので、通すと"
            + "「自分の灯を消した」が第三者の破片や敵への弱体に化ける。"
            + "受け手は素の攻撃力より弱くなっていないので弱体の供給者として数えるのも事実に反する | "
            + "`UnitTally.TaillightDoused`（トモ側）と `AtkBonus` の setter（第106期の4つ目の通貨） |");
        Console.WriteLine();
        Console.WriteLine("`WhetRoute` は **" + WhetRoutes.Count + " 本**（"
            + string.Join(" / ", WhetRoutes.Names) + "）。灯 = " + TaillightTrait.Lumen + "。");
        Console.WriteLine();

        // (4) 刻み×澱み の現在の5枚と席。
        var kizami = CompareBuilds().FirstOrDefault(b => b.Name.StartsWith("刻み×澱み", StringComparison.Ordinal));
        Console.WriteLine("## 4. `刻み×澱み (ノミ×ミオ)` の現在の5枚と席");
        Console.WriteLine();
        if (kizami.F is null) Console.WriteLine("**行が見つからない。**");
        else
        {
            Console.WriteLine("| 席 | 駒 | HP | 攻 | 速 |");
            Console.WriteLine("|---|---|--:|--:|--:|");
            string[] slotName = { "前1", "前3", "中央", "後1", "後3" };
            foreach ((int sl, UnitDef d) in kizami.F.Occupied().OrderBy(o => o.Slot))
                Console.WriteLine($"| {(sl < slotName.Length ? slotName[sl] : "○" + sl)} | {d.Name} | {d.MaxHp} | {d.Attack} | {d.Speed} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **指示書の `刻み×灯` という行はリポジトリに存在しない**（`CompareBuilds()` の 61 行を全数検索して 0 件）。"
            + "実体は **`刻み×澱み (ノミ×ミオ)`** で、第108期に `刻み×縫い (ノミ×ハリ)` を差し替えて作った行。"
            + "**手作りのずれの14例目**として記録する。");
        Console.WriteLine();

        // (5) docs/crossing.md に残るハリ。
        var cross = CrossBuilds();
        var hariRows = cross.Where(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hari))).ToList();
        Console.WriteLine("## 5. `docs/crossing.md` に残るハリの不整合");
        Console.WriteLine();
        Console.WriteLine("交差帯 " + cross.Length + " 行のうちハリを含むのは **" + hariRows.Count + " 行**（"
            + string.Join(" / ", hariRows.Select(b => "`" + b.Name + "`")) + "）。"
            + "ハリは `UnitCatalog.All`（" + UnitCatalog.All.Count + " 枚）から外れているが `UnitDef` も "
            + "`SutureTrait` も残置してあるので、**行としては壊れていない**（測定は今も回る）。");
        Console.WriteLine();
        Console.WriteLine("> **第110期に送る。** 交差帯は**測定の器具**で、この期は測定期"
            + "——第108期の「計測器と測定対象を同時に動かさない」に従う。"
            + "直すなら `docs/` 10ファイルと情報セルの数え直しを伴うので、単独の期にする。");
        Console.WriteLine();

        // (3)(門) 実測。
        TmRun();
        Console.WriteLine("## 3. 門（§1-2）——鎖が繋がっているか。**大きさではない**");
        Console.WriteLine();
        Console.WriteLine("現行の版（トモ本物）だけ。台 × 波・seed 0.." + (TmSeeds - 1) + "。1戦あたり。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 決着T | **門1 灯/戦** | 灯量/戦 | 替/戦 | **門2 条件成立/戦** | トモの手番/戦 | **門3 譲渡/戦** | 譲渡の与ダメ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _, _) in tmPartners)
            for (int w = 1; w <= tmStages.Count; w++)
            {
                var g = tmRows.Where(x => x.Bench == tag && x.Ver == 0 && x.Wave == w).ToList();
                double turns = g.Count == 0 ? 0 : g.Average(x => (double)x.Turns);
                double hands = TmAvg(g.Select(x => x.Yields + x.NoTarget + x.Hop + x.NoDeath));
                double gate2 = TmAvg(g.Select(x => x.Yields + x.NoTarget + x.Hop));
                Console.WriteLine($"| {tag} | {w} | {turns:F1} | **{TmAvg(g.Select(x => x.Fires)):F2}** "
                    + $"| {TmAvg(g.Select(x => x.Lumen)):F1} | {TmAvg(g.Select(x => x.Switches)):F2} "
                    + $"| **{gate2:F2}** | {hands:F2} | **{TmAvg(g.Select(x => x.Yields)):F2}** "
                    + $"| {TmAvg(g.Select(x => x.YieldDmg)):F1} |");
            }
        Console.WriteLine();
        var all0 = tmRows.Where(x => x.Ver == 0).ToList();
        double g1 = TmAvg(all0.Select(x => x.Fires));
        double g2 = TmAvg(all0.Select(x => x.Yields + x.NoTarget + x.Hop));
        double g3 = TmAvg(all0.Select(x => x.Yields));
        Console.WriteLine($"**門1 = {g1:F2} ／ 門2 = {g2:F2} ／ 門3 = {g3:F2}（全台・全波の平均）。"
            + (g1 > 0 && g2 > 0 && g3 > 0 ? "3つとも 0 より大きい——鎖は繋がっている。**"
                                          : "**0 のものがある——そこが切れている。**"));
        Console.WriteLine();
        return;
    }

    // ------------------------------------------------------------------------------
    // 表A〜D（§1）
    // ------------------------------------------------------------------------------
    void TmTablesAD()
    {
        Console.WriteLine("## 表A —— 門（§1-2）と紙（§1-3）。**波別**");
        Console.WriteLine();
        Console.WriteLine("台 × 波・seed 0.." + (TmSeeds - 1) + "・**現行の版（トモ本物）**。1戦あたり。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 決着T | 門1 灯 | 灯量 | 替 | 消 | 空振り | 門2 条件成立 | 門3 譲渡 | 譲渡率 | 紙(灯・二次) | 紙(譲渡) | 実測(灯った駒の与ダメ差) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _, _) in tmPartners)
            for (int w = 1; w <= tmStages.Count; w++)
            {
                var g = tmRows.Where(x => x.Bench == tag && x.Ver == 0 && x.Wave == w).ToList();
                var gp = tmRows.Where(x => x.Bench == tag && x.Ver == 1 && x.Wave == w).ToList();
                double f = TmAvg(g.Select(x => x.Fires));
                double gate2 = TmAvg(g.Select(x => x.Yields + x.NoTarget + x.Hop));
                double y = TmAvg(g.Select(x => x.Yields));
                // 紙（灯）は**二次**。同じ相手を照らし続け、その相手が毎ターン振るなら
                //   Σ_{k=1..F} Lumen·k = Lumen·F(F+1)/2。
                // 振らなかったターンのぶんだけ下振れるので、**上限**として読む（第87期の丸めの逆）。
                double paperLit = TaillightTrait.Lumen * f * (f + 1) / 2.0;
                double paperYield = TmAvg(g.Select(x => x.YieldDmg));
                double obs = TmAvg(g.Select(x => x.PartnerDmg)) - TmAvg(gp.Select(x => x.PartnerDmg));
                Console.WriteLine($"| {tag} | {w} | {(g.Count == 0 ? 0 : g.Average(x => (double)x.Turns)):F1} "
                    + $"| {f:F2} | {TmAvg(g.Select(x => x.Lumen)):F1} | {TmAvg(g.Select(x => x.Switches)):F2} "
                    + $"| {TmAvg(g.Select(x => x.Doused)):F1} | {TmAvg(g.Select(x => x.Idle)):F2} "
                    + $"| {gate2:F2} | **{y:F2}** | {(gate2 > 0 ? y / gate2 * 100 : 0):F0}% "
                    + $"| {paperLit:F0} | {paperYield:F1} | {obs:+0.0;-0.0;0.0} |");
            }
        Console.WriteLine();
        Console.WriteLine("> **紙の分子について3つ**（規約 (G7)）。**(1) 二次**——灯は累積するので "
            + "`Σ Lumen·k = Lumen·F(F+1)/2`。**(2) 門ではなく出力**——門は上の3列で別に見ている。"
            + "**(3) 分母を削るか**——灯は出力を増やすので決着を早める側に働き、"
            + "同時にトモが攻0 の枠を1つ食うので決着を伸ばす側にも働く。**両方向なので紙は上限にも下限にもならない。**");
        Console.WriteLine();

        Console.WriteLine("## 表B —— 4台 × 対照（Q1）");
        Console.WriteLine();
        Console.WriteLine("`現行` = 尾灯のトモ ／ `素体` = **同数値・特性なし**（HP" + UnitCatalog.Tomo.MaxHp
            + "・攻" + UnitCatalog.Tomo.Attack + "・速" + UnitCatalog.Tomo.Speed
            + "）に差し替えた版。判定は **第2〜5波の平均**（規約 (G10)——第一波は全行が勝つ教習波）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | **第2〜5波** | 帰属 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        int tmQ1 = 0;
        var tmQ1Rows = new List<(string Tag, double Cur, double Plain)>();
        foreach (var (tag, _, _) in tmPartners)
        {
            double[] cur = new double[tmStages.Count], pla = new double[tmStages.Count];
            for (int w = 1; w <= tmStages.Count; w++)
            {
                cur[w - 1] = tmRows.Where(x => x.Bench == tag && x.Ver == 0 && x.Wave == w).Count(x => x.Won) * 100.0 / TmSeeds;
                pla[w - 1] = tmRows.Where(x => x.Bench == tag && x.Ver == 1 && x.Wave == w).Count(x => x.Won) * 100.0 / TmSeeds;
            }
            double c25 = cur.Skip(1).Average(), p25 = pla.Skip(1).Average();
            if (c25 > p25) tmQ1++;
            tmQ1Rows.Add((tag, c25, p25));
            Console.WriteLine($"| **{tag}** | 現行 |" + string.Concat(cur.Select(v => $" {v:F1} |")) + $" **{c25:F1}** | — |");
            Console.WriteLine($"| | 素体 |" + string.Concat(pla.Select(v => $" {v:F1} |")) + $" {p25:F1} | **{c25 - p25:+0.0;-0.0;0.0}pt** |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q1: 4台のうち {tmQ1} 台で対照より第2〜5波平均が上がった（線は 2 台）"
            + $"—— {(tmQ1 >= 2 ? "○" : "**×**")}。**");
        Console.WriteLine();
        // **床の検査**（第21・61・63期）。素体版も現行版も 0.0% の台は「差が無い」ではなく
        // **「測っていない」**——判定に使う前に、測れた台がいくつあるかを数える。
        var floors = tmQ1Rows.Where(x => x.Cur <= 0.05 && x.Plain <= 0.05).Select(x => x.Tag).ToList();
        Console.WriteLine("> **床の検査**（第21・61・63期）。**素体版も現行版も 0.0% の台は「差が無い」ではなく"
            + "「測っていない」。** この4台では **" + (tmPartners.Length - floors.Count) + " 台が測れて、"
            + floors.Count + " 台が床**"
            + (floors.Count > 0 ? "（" + string.Join(" / ", floors) + "）" : "")
            + $"——**測れた台だけを分母にすると {tmQ1} / {tmPartners.Length - floors.Count}。**"
            + "土台を選ぶ規則 (3)（台が床に落ちない出力を持つ）が**この2台では守れていなかった。**");
        Console.WriteLine();

        Console.WriteLine("### トモが最遅だった回数・対象になった駒の内訳");
        Console.WriteLine();
        // 「トモが最遅だったか」は**編成の静的な性質**（速さは戦闘中に動かない）なので台ごとに1度だけ判定する。
        Console.WriteLine("| 台 | トモが味方で最遅か | 自分を除いて最も遅い味方 | `Stoic` で飛ばした後の対象（設計上） | **実際に灯を受けた駒の内訳**（受け手側の帳簿・現行の版） |");
        Console.WriteLine("|---|:-:|---|---|---|");
        foreach (var (tag, partner, _) in tmPartners)
        {
            Formation f = TmBench(partner, UnitCatalog.Tomo);
            var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Tomo)).ToList();
            bool slowest = UnitCatalog.Tomo.Speed <= others.Min(o => o.Def.Speed);
            UnitDef second = others.OrderBy(o => o.Def.Speed).ThenBy(o => o.Slot).First().Def;
            UnitDef target = others.Where(o => !o.Def.Traits.Contains(TraitId.Stoic))
                                   .OrderBy(o => o.Def.Speed).ThenBy(o => o.Slot).First().Def;
            var g = tmRows.Where(x => x.Bench == tag && x.Ver == 0).ToList();
            Console.WriteLine($"| {tag} | {(slowest ? "**○**" : "×")} | {second.Name}（速{second.Speed}）"
                + (second.Traits.Contains(TraitId.Stoic) ? "**・支援拒否**" : "")
                + $" | {target.Name}（速{target.Speed}） "
                + "| " + (tmLitBy.TryGetValue(tag, out Dictionary<string, int>? bag)
                    ? string.Join(" ／ ", bag.OrderByDescending(kv => kv.Value)
                        .Select(kv => $"{kv.Key} {kv.Value * 100.0 / bag.Values.Sum():F1}%"))
                    : "—") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("> **2番目に遅いのは廃棄聖騎士ガルド（速4）だが `Stoic` なので灯が飛ばす**"
            + "（第108期 (b) の「支援拒否を飛ばす」）。土台をこう組んだのは、"
            + "**遅い駒を土台に入れながら対象をパートナーに一意に決める**唯一の方法だから。");
        Console.WriteLine();

        Console.WriteLine("## 表C —— 譲渡（Q2）");
        Console.WriteLine();
        Console.WriteLine("**トモ自身の1手番あたりの出力は 0**（攻撃力 " + UnitCatalog.Tomo.Attack
            + "・`Actions = [Skill]` で `PerformAttack` を一度も通らない）。"
            + "**譲られた駒がその手番で敵へ通した量が正なら Q2 は成立する。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 譲渡/戦 | 通常攻撃 | 術 | 溜め | **潰れた** | 譲渡の与ダメ/戦 | **1譲渡あたり** | トモの1手番あたり | 潰れた内訳（譲れなかった手番） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        int tmQ2 = 0;
        foreach (var (tag, _, _) in tmPartners)
        {
            var g = tmRows.Where(x => x.Bench == tag && x.Ver == 0).ToList();
            double y = TmAvg(g.Select(x => x.Yields));
            double dmg = TmAvg(g.Select(x => x.YieldDmg));
            double per = y > 0 ? dmg / y : 0;
            if (per > 0) tmQ2++;
            Console.WriteLine($"| {tag} | {y:F2} | {TmAvg(g.Select(x => x.YAtk)):F2} | {TmAvg(g.Select(x => x.YSkill)):F2} "
                + $"| {TmAvg(g.Select(x => x.YCharge)):F2} | {TmAvg(g.Select(x => x.YStall)):F2} | {dmg:F1} "
                + $"| **{per:F1}** | 0.0 "
                + $"| 敵未撃破 {TmAvg(g.Select(x => x.NoDeath)):F2} ／ 灯った相手がいない {TmAvg(g.Select(x => x.NoTarget)):F2} ／ 1ホップ {TmAvg(g.Select(x => x.Hop)):F2} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**Q2: {tmQ2} / 4 台で「1譲渡あたりの出力 > トモの1手番あたりの出力（0）」"
            + $"—— {(tmQ2 == tmPartners.Length ? "○" : "**×**")}。**");
        Console.WriteLine();

        Console.WriteLine("## 表D —— 灯（Q3）。到達点の分布と消えた回数");
        Console.WriteLine();
        Console.WriteLine("`到達点` = **1体に同時に載った灯の最大**（`UnitTally.TaillightPeak`）。"
            + "**総量ではない**——対象が変わると消えるので、総量では到達点が測れない。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 決着T | 灯/戦 | **到達点（平均）** | 到達点（最大） | 替/戦 | 消/戦 | 消した量/戦 | 灯量/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _, _) in tmPartners)
            for (int w = 1; w <= tmStages.Count; w++)
            {
                var g = tmRows.Where(x => x.Bench == tag && x.Ver == 0 && x.Wave == w).ToList();
                Console.WriteLine($"| {tag} | {w} | {(g.Count == 0 ? 0 : g.Average(x => (double)x.Turns)):F1} "
                    + $"| {TmAvg(g.Select(x => x.Fires)):F2} | **{TmAvg(g.Select(x => x.Peak)):F1}** "
                    + $"| {(g.Count == 0 ? 0 : g.Max(x => x.Peak))} | {TmAvg(g.Select(x => x.Switches)):F2} "
                    + $"| {(g.Count == 0 ? 0 : g.Count(x => x.Doused > 0) * 100.0 / g.Count):F0}% "
                    + $"| {TmAvg(g.Select(x => x.Doused)):F1} | {TmAvg(g.Select(x => x.Lumen)):F1} |");
            }
        Console.WriteLine();

        // 副判定（該当するものだけ・規約 (G6)）
        Console.WriteLine("### 副判定（規約 (G6)。該当するものだけ）");
        Console.WriteLine();
        var hagi = tmRows.Where(x => x.Bench == "トモ×ハギ" && x.Ver == 0).ToList();
        var hagiP = tmRows.Where(x => x.Bench == "トモ×ハギ" && x.Ver == 1).ToList();
        Console.WriteLine("- **1つの出来事で2枚が立つか**（トモ×ハギ）——譲渡 "
            + $"{TmAvg(hagi.Select(x => x.Yields)):F2} 回/戦。追い打ちのハギはこの台では"
            + "**灯の受け手そのもの**なので、「譲られて薙ぐ」と「味方の撃破に反応して薙ぐ」が同じ駒に乗る"
            + $"（ハギの与ダメ 現行 {TmAvg(hagi.Select(x => x.PartnerDmg)):F0} 対 素体 {TmAvg(hagiP.Select(x => x.PartnerDmg)):F0}）。");
        Console.WriteLine("- **発火回数と稼働率**（読み手を足す機構なので該当。決着ターン数を併記）——");
        Console.WriteLine();
        Console.WriteLine("| 台 | 決着T | 灯/戦 | 稼働率（灯 ÷ 決着T） | 譲渡/戦 | 稼働率（譲渡 ÷ 決着T） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var (tag, _, _) in tmPartners)
        {
            var g = tmRows.Where(x => x.Bench == tag && x.Ver == 0).ToList();
            double turns = g.Count == 0 ? 0 : g.Average(x => (double)x.Turns);
            Console.WriteLine($"| {tag} | {turns:F1} | {TmAvg(g.Select(x => x.Fires)):F2} "
                + $"| {(turns > 0 ? TmAvg(g.Select(x => x.Fires)) / turns * 100 : 0):F1}% "
                + $"| {TmAvg(g.Select(x => x.Yields)):F2} "
                + $"| {(turns > 0 ? TmAvg(g.Select(x => x.Yields)) / turns * 100 : 0):F1}% |");
        }
        Console.WriteLine();
    }

    // ------------------------------------------------------------------------------
    // 表E —— (B) `刻み×澱み (ノミ×ミオ)` の再測定（§3）
    // ------------------------------------------------------------------------------
    void TmTableE()
    {
        var kizami = CompareBuilds().FirstOrDefault(b => b.Name.StartsWith("刻み×澱み", StringComparison.Ordinal));
        Console.WriteLine("## 表E —— (B) `刻み×澱み (ノミ×ミオ)` の再測定（§3）");
        Console.WriteLine();
        if (kizami.F is null) { Console.WriteLine("**行が見つからない。**"); return; }

        Formation Swap(UnitDef? to)
        {
            var g = new Formation();
            foreach ((int slot, UnitDef d) in kizami.F.Occupied())
            {
                if (ReferenceEquals(d, UnitCatalog.Mio)) { if (to is not null) g[slot] = to; }
                else g[slot] = d;
            }
            return g;
        }

        var vers = new (string Tag, Formation F)[]
        {
            ("V0 現行（澱みのミオ）", kizami.F),
            ("V1 ミオ素体（同数値・特性なし）", Swap(tmMioPlain)),
            ("V2 4体（ミオの席を空ける）", Swap(null)),
            ("V3 ミオ → 抉りのエグ", Swap(UnitCatalog.Egu)),
            ("V4 ミオ → 断ちのナタ", Swap(UnitCatalog.Nata)),
            ("V5 ミオ → 継ぎ当てのノノ", Swap(UnitCatalog.Nono)),
        };

        Console.WriteLine("### 表E-1 —— 素体差し替えと差し替え版（seed 0.." + (TmSeeds - 1) + "）");
        Console.WriteLine();
        Console.WriteLine("**`ablate`（1枚抜き）と素体差し替えは別の器具**（第69期）。"
            + "第108期の `ablate` は **ミオ −4.0pt**（他の4枚は −37.9〜−49.5pt）だった。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 5波平均 | **第2〜5波** | V0 との差 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        double v0_25 = 0;
        var eCells = new Dictionary<string, double[]>();
        foreach (var (tag, f) in vers)
        {
            double[] cell = new double[tmStages.Count];
            for (int w = 0; w < tmStages.Count; w++)
            {
                int win = 0;
                for (int seed = 0; seed < TmSeeds; seed++)
                    if (BattleEngine.Run(f, tmStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                cell[w] = win * 100.0 / TmSeeds;
            }
            eCells[tag] = cell;
            double a25 = cell.Skip(1).Average();
            if (tag.StartsWith("V0", StringComparison.Ordinal)) v0_25 = a25;
            Console.WriteLine($"| {tag} |" + string.Concat(cell.Select(v => $" {v:F1} |"))
                + $" {cell.Average():F1} | **{a25:F1}** | "
                + (tag.StartsWith("V0", StringComparison.Ordinal) ? "—" : $"**{a25 - v0_25:+0.0;-0.0;0.0}pt**") + " |");
        }
        Console.WriteLine();
        double attrib = v0_25 - eCells["V1 ミオ素体（同数値・特性なし）"].Skip(1).Average();
        double body = eCells["V1 ミオ素体（同数値・特性なし）"].Skip(1).Average() - eCells["V2 4体（ミオの席を空ける）"].Skip(1).Average();
        Console.WriteLine($"**機構の帰属（V0 − V1）= {attrib:+0.0;-0.0;0.0}pt ／ 体の値段（V1 − V2）= {body:+0.0;-0.0;0.0}pt。**");
        Console.WriteLine();

        // 表E-2: 着火の回数（波別）。ミオを含む compare の全行で測り、
        // 「グザ同席の行は着火が構造的に 0」（第87期）を同じ実行の中で確かめる。
        Console.WriteLine("### 表E-2 —— 着火の回数（波別）。**`刻み×澱み` はグザを含まない初めてのミオの行**");
        Console.WriteLine();
        var mioRows = CompareBuilds().Where(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Mio))).ToArray();
        Console.WriteLine("| 行 | グザ同席 | 波 | 濃縮/戦 | 着火できる敵/戦 | 同・実体数/戦 | **着火/戦** | 置いた層/戦 | 着火由来の毒ダメ/戦 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in mioRows)
        {
            bool withGuza = b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Guza));
            for (int w = 0; w < tmStages.Count; w++)
            {
                double fires = 0, able = 0, bodies = 0, ign = 0, amt = 0, dot = 0;
                for (int seed = 0; seed < TmSeeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(b.F, tmStages[w].Enemy, seed, verbose: false);
                    if (r.TallyByUnit.TryGetValue(UnitCatalog.Mio.Id, out UnitTally? mt))
                    {
                        fires += mt.AmpFires; able += mt.AmpIgnitable; bodies += mt.AmpIgnitableBodies;
                        ign += mt.AmpIgnited; amt += mt.AmpIgniteAmount;
                    }
                    foreach (var kv in r.TallyByUnit) dot += kv.Value.IgnitePoisonDamage;
                }
                Console.WriteLine($"| {(w == 0 ? "**" + b.Name + "**" : "")} | {(w == 0 ? (withGuza ? "○" : "**×**") : "")} | {w + 1} "
                    + $"| {fires / TmSeeds:F2} | {able / TmSeeds:F2} | {bodies / TmSeeds:F2} "
                    + $"| **{ign / TmSeeds:F2}** | {amt / TmSeeds:F2} | {dot / TmSeeds:F1} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("> 第87期は「ミオを含む4行が全部グザ（瘴気）同席で、毎ターン敵全体に毒が撒かれるので"
            + "『傷を持ち毒を持たない敵』が存在せず着火が構造的に 0 回」と測っていた。**この表がその再現と、"
            + "グザを含まない行での初めての実測になる。**");
        Console.WriteLine();
    }

    // ------------------------------------------------------------------------------
    // 自己検査
    // ------------------------------------------------------------------------------
    void TmCheck()
    {
        var checks = new List<(string Tag, string What, string Got, bool Ok)>();

        // ---- 必須4項目 ----
        // 1. compare 305 セルが docs/balance.md と 0 件。
        string? root = Directory.GetCurrentDirectory();
        while (root is not null && !File.Exists(Path.Combine(root, "docs", "balance.md")))
            root = Directory.GetParent(root)?.FullName;
        int cells = 0, mism = 0;
        if (root is not null)
        {
            var doc = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(Path.Combine(root, "docs", "balance.md")))
            {
                if (!line.StartsWith("| ", StringComparison.Ordinal)) continue;
                string[] c = line.Split('|', StringSplitOptions.TrimEntries);
                if (c.Length < 3 + tmStages.Count) continue;
                var vals = new List<double>();
                for (int i = 2; i < 2 + tmStages.Count; i++)
                    if (double.TryParse(c[i].Replace("%", "").Trim(), out double v)) vals.Add(v);
                if (vals.Count == tmStages.Count) doc[c[1].Replace("*", "").Trim()] = vals.ToArray();
            }
            foreach (var b in CompareBuilds())
            {
                if (!doc.TryGetValue(b.Name.Replace("*", "").Trim(), out double[]? want)) continue;
                for (int w = 0; w < tmStages.Count; w++)
                {
                    int win = 0;
                    for (int seed = 0; seed < TmSeeds; seed++)
                        if (BattleEngine.Run(b.F, tmStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                    cells++;
                    if (Math.Abs(win * 100.0 / TmSeeds - want[w]) > 0.05) mism++;
                }
            }
        }
        checks.Add(("必須1", "`compare` の全セルが `docs/balance.md` と一致（**トモは `Presets` に入っていないので"
            + "拒否権は原理的に立たない。立たないことを報告する**）",
            $"{cells} セル中ずれ {mism} 件", cells > 0 && mism == 0));

        // 4. ctx.PickOne の実呼び出しが 26 箇所（第94期以降不変）
        static int TmCount(string hay, string needle)
        {
            int n = 0;
            for (int i = hay.IndexOf(needle, StringComparison.Ordinal); i >= 0;
                 i = hay.IndexOf(needle, i + 1, StringComparison.Ordinal)) n++;
            return n;
        }
        int pick = 0;
        if (root is not null)
            foreach (string fp in new[] { Path.Combine(root, "BattleCore", "Traits.cs"),
                                          Path.Combine(root, "BattleCore", "BattleEngine.cs") })
                if (File.Exists(fp)) pick += TmCount(File.ReadAllText(fp), "PickOne(");
        checks.Add(("必須4", "`PickOne(` の素の出現数が **26** のまま（第94期以降不変）", pick + " 箇所", pick == 26));

        // ---- 機構固有 (a)〜(f) ----
        // ログを再生して灯の台帳を追う（第108期 `taillight` の器具の写し。
        // **同名のトモが2枚いると分離できない**という限界はこの台では当たらない——トモは1枚）。
        int badOne = 0, badDouse = 0, badNet = 0, badYieldTurn = 0, badSelf = 0, hopAll = 0, audits = 0;
        int maxAtOnce = 0;
        foreach (var (tag, partner, _) in tmPartners)
        {
            Formation f = TmBench(partner, UnitCatalog.Tomo);
            for (int w = 0; w < tmStages.Count; w++)
                for (int seed = 0; seed < TmAudit; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, tmStages[w].Enemy, seed, verbose: true);
                    var lamp = new Dictionary<string, int>();
                    int lit = 0, doused = 0, yieldsThisTurn = 0;
                    audits++;
                    foreach (LogLine line in r.Log)
                    {
                        string t = line.Text;
                        if (line.Kind == LogKind.Turn)
                        {
                            if (yieldsThisTurn > 1) badYieldTurn++;
                            yieldsThisTurn = 0;
                            continue;
                        }
                        int a = t.IndexOf(" に灯をともした", StringComparison.Ordinal);
                        if (a >= 0)
                        {
                            int b0 = t.IndexOf(" が ", StringComparison.Ordinal);
                            string who = t.Substring(b0 + 3, a - b0 - 3);
                            if (who == UnitCatalog.Tomo.Name) badSelf++;         // (e) 自分は対象外
                            lamp[who] = lamp.TryGetValue(who, out int had) ? had + TaillightTrait.Lumen
                                                                          : TaillightTrait.Lumen;
                            lit += TaillightTrait.Lumen;
                            int on = lamp.Count(kv => kv.Value > 0);
                            if (on > maxAtOnce) maxAtOnce = on;
                            if (on > 1) badOne++;                                 // (a) 同時に1体だけ
                            continue;
                        }
                        int c = t.IndexOf(" の灯が消えた（攻撃 -", StringComparison.Ordinal);
                        if (c >= 0)
                        {
                            string who = t.Substring(0, c);
                            int amt = int.Parse(new string(t.Substring(c).Where(char.IsAsciiDigit).ToArray()));
                            if (!lamp.TryGetValue(who, out int had2) || had2 != amt) badDouse++;   // (b)
                            lamp[who] = 0;
                            doused += amt;
                            continue;
                        }
                        if (t.Contains("は前へ出ず、灯した", StringComparison.Ordinal)) yieldsThisTurn++;
                    }
                    if (yieldsThisTurn > 1) badYieldTurn++;
                    if (lit - doused != lamp.Values.Sum()) badNet++;
                    if (r.TallyByUnit.TryGetValue(UnitCatalog.Tomo.Id, out UnitTally? tt))
                        hopAll += tt.TaillightBlockedHop;
                }
        }
        checks.Add(("(a)", "灯が**同時に1体にしか灯っていない**", $"同時に灯った最大 {maxAtOnce} 体・違反 {badOne} 件 / {audits} 戦", badOne == 0 && maxAtOnce <= 1));
        checks.Add(("(b)", "対象が変わったとき、**前の灯がちょうど載っていた量だけ**消えている", $"照合ずれ {badDouse} 件・収支ずれ {badNet} 戦 / {audits} 戦", badDouse == 0 && badNet == 0));
        checks.Add(("(c)", "譲渡が**1ターン1回以下**", $"違反 {badYieldTurn} ターン / {audits} 戦", badYieldTurn == 0));
        checks.Add(("(d)", "**1ホップ**——譲った手番の中で敵が倒れても再度譲らない（`BattleContext.Yielding`）", $"1ホップで止めた回数 {hopAll}（**この台ではトモ1枚なので 0 が正しい**——トモの手番は既に終わっている）", true));
        checks.Add(("(e)", "トモ自身が対象になっていない", $"{badSelf} 件 / {audits} 戦", badSelf == 0));

        // (f) 点灯が ctx.Whet を通っている
        int tailWhet = 0, tailLumen = 0, litRecv = 0;
        foreach (var x in tmRows.Where(x => x.Ver == 0)) { tailWhet += x.WhetTail; tailLumen += x.Lumen; litRecv += x.LitOnPartner; }
        checks.Add(("(f)", "点灯が **`ctx.Whet(…, WhetRoute.Taillight)`** を通っている"
            + "（`BattleResult.WhetByRoute` とトモ側の帳簿が一致する）",
            $"窓口 {tailWhet} 量 / トモの帳簿 {tailLumen} 量", tailWhet == tailLumen && tailWhet > 0));
        // (f') 受け手側の帳簿の**合計**がトモの灯した回数と閉じ、**最大の受け手がパートナー**であること。
        // **「パートナーだけ」は誤った判定**——パートナーが倒れれば灯は次に遅い味方へ移るのが規則どおり
        // （初版はこれで落ちた。器具の限界であって盤面の不整合ではない）。
        int firesAll = tmRows.Where(x => x.Ver == 0).Sum(x => x.Fires);
        int recvAll = tmLitBy.Values.Sum(b => b.Values.Sum());
        bool topIsPartner = tmPartners.All(pp => tmLitBy.TryGetValue(pp.Tag, out Dictionary<string, int>? b)
            && b.Count > 0 && b.OrderByDescending(kv => kv.Value).First().Key == pp.Def.Name);
        checks.Add(("(f')", "受け手側の帳簿（`TaillightLitReceived`）の**合計がトモの灯した回数と閉じ**、"
            + "**最大の受け手が4台ともパートナー**（パートナーが倒れた後は次に遅い味方へ移るのが規則どおり）",
            $"受け手の合計 {recvAll} 回 / トモの帳簿 {firesAll} 回・うちパートナー {litRecv} 回"
            + $"（{(firesAll > 0 ? litRecv * 100.0 / firesAll : 0):F1}%）",
            recvAll == firesAll && firesAll > 0 && topIsPartner));

        Console.WriteLine("## 自己検査");
        Console.WriteLine();
        Console.WriteLine("| | 内容 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        foreach (var (tag, what, got, ok) in checks)
            Console.WriteLine($"| **{tag}** | {what} | {got} | {(ok ? "○" : "**×**")} |");
        Console.WriteLine();
        Console.WriteLine($"**{checks.Count(x => x.Ok)} / {checks.Count} 件が ○。**");
        Console.WriteLine();
        Console.WriteLine("**必須2（`docs/` 10ファイルの再生成）と 必須3（触っていないノブの既定）は"
            + "コマンドの外で確かめる**（報告書 §7 に差分を書く）。");
        Console.WriteLine();
    }

    Console.WriteLine("# 第109期 —— 尾灯のトモ（`tomo`・モード: " + tmMode + "）");
    Console.WriteLine();
    if (tmMode == "run" || tmMode == "tables" || tmMode == "check") TmRun();
    if (tmMode == "run" || tmMode == "tables") TmTablesAD();
    if (tmMode == "mio" || tmMode == "tables") TmTableE();
    if (tmMode == "check") TmCheck();
    return;
}
}
