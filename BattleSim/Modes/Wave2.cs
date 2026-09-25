using BattleCore;
using static Common;

// =====================================================================================
// wave2 モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "wave2")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 wave2
// =====================================================================================

static class Wave2Diag
{
public static void Run(string[] args, int stageIndex)
{
    const int W2Seeds = 200;        // compare / pulse と同じ
    const int W2ProbeSeeds = 20;    // 表C の課金対象の判定（verbose が要る）だけこの本数

    int w2Want = args.Length > 2 && int.TryParse(args[2], out int w2Arg) ? w2Arg : 2;
    int w2Idx = Math.Clamp(w2Want, 1, EnemyCatalog.Stages.Count) - 1;

    var w2Builds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> w2Stages = EnemyCatalog.Stages;

    // --- 機械的な判定 --------------------------------------------------------------------------
    // **役割名で数えない**（第32期の「単一窓口は call-site grep で数える」の適用）。
    // どれも窓口の呼び出し元をそのまま写しただけで、この配列以外に判定は置いていない。

    // ターン外の行動 = BattleContext.CanActOutOfTurn を通る4本（Traits.cs 側の呼び出し元）。
    // 肩代わり（庇う・分かち・巨躯・後備え・棘守り）はダメージの再分配であって行動ではないので入らない。
    var w2OutOfTurn = new[] { TraitId.Thorns, TraitId.Avenge, TraitId.Displaced, TraitId.Pursuer };

    // 回復 = ctx.Heal の呼び出し元9経路（CLAUDE.md の「駒の説明文から数えると必ず抜ける」）。
    // 巨躯（吸い・還し）と墓守は説明文のどこにも回復と書いていないが回復する。
    var w2Healers = new[]
    {
        TraitId.Drain, TraitId.Necro, TraitId.Colossus, TraitId.Mender, TraitId.Alms,
        TraitId.Suture, TraitId.Devour, TraitId.Drifter, TraitId.Forsake
    };

    // 後列に届く手段（指示書 §2 表B の「貫き・標・毒・範囲」）。
    //   範囲/貫き = Def.Pattern が Single 以外、または ModifyPattern を上書きする特性
    //   毒        = 敵に毒を積む特性（澱み喰い・毒喰らいは読み手なので入らない）
    //   標        = 敵に標を付ける特性。囃し立て（Marker）は**味方**に付けるので入らない
    var w2Repattern = new[] { TraitId.Sniper, TraitId.Necro, TraitId.Scale, TraitId.Pyre };
    var w2Poison = new[] { TraitId.Venom, TraitId.VenomHeavy, TraitId.Amplifier, TraitId.Contagion, TraitId.Miasma };
    var w2Mark = new[] { TraitId.Divert };

    bool W2HasAny(Formation f, TraitId[] ids)
        => f.Occupied().Any(o => o.Def.Traits.Any(ids.Contains));
    int W2CountHas(Formation f, TraitId[] ids)
        => f.Occupied().Count(o => o.Def.Traits.Any(ids.Contains));
    bool W2Reaches(Formation f)
        => f.Occupied().Any(o => o.Def.Pattern != AttackPattern.Single)
           || W2HasAny(f, w2Repattern) || W2HasAny(f, w2Poison) || W2HasAny(f, w2Mark);
    int W2TotalAttack(Formation f) => f.Occupied().Sum(o => o.Def.Attack);

    // --- 条件を組む ----------------------------------------------------------------------------
    // 同数値の対照駒。粛・渇き・軛・殉教はどれも素の駒と数値・型・速さが1つも違わない
    // ——差し替えれば盤面が完全に元へ戻る（第40期の告発人と同じ形）。
    var w2Controls = new Dictionary<string, (UnitDef Def, string Note)>
    {
        ["husher"] = (EnemyCatalog.Recruit, "討伐隊の新兵 45/11/6"),
        ["droughter"] = (EnemyCatalog.Knight, "巡礼騎士 75/15/7"),
        ["yoker"] = (EnemyCatalog.Warden, "城塞の重装兵 145/12/3"),
        ["axeman_g"] = (EnemyCatalog.Axeman2, "戦斧兵 52/11/5 薙ぎ"),
    };

    Formation W2Swap(Formation f, int slot, UnitDef? def)
    {
        Formation c = f.Clone();
        c[slot] = def;
        return c;
    }

    // 波ごとの「規則を切った版」。保持者を同数値の対照駒に差し替える。
    (Formation Enemy, string Label)? W2RuleOff(int idx)
    {
        Formation e = w2Stages[idx].Enemy;
        foreach ((int slot, UnitDef def) in e.Occupied())
            if (w2Controls.TryGetValue(def.Id, out var ctl))
                return (W2Swap(e, slot, ctl.Def),
                        FormationRules.SeatNames[slot] + " " + def.Name + " → " + ctl.Def.Name
                        + "（" + ctl.Note + "・同数値）");
        return null;
    }

    Formation w2Base = w2Stages[w2Idx].Enemy;

    var w2Conds = new List<(string Id, string Name, Formation Enemy, HushRule? Hush, YokeRule? Yoke)>
    {
        ("0", "基準（現行のまま）", w2Base, null, null)
    };

    // 条件1: 規則を無効にする。フラグのある規則はフラグ側を主に採り、対照駒側は検算 1' に回す。
    var w2Off = W2RuleOff(w2Idx);
    bool w2HasHush = w2Base.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Hush));
    bool w2HasYoke = w2Base.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Yoke));
    if (w2HasHush)
        w2Conds.Add(("1", "規則を無効（HushRule(Active:false)・盤面は現行のまま）",
                     w2Base, new HushRule(Active: false), null));
    else if (w2HasYoke)
        w2Conds.Add(("1", "規則を無効（YokeRule(Active:false)・盤面は現行のまま）",
                     w2Base, null, new YokeRule(YokeTrait.Cap, Active: false)));
    if (w2Off is { } off)
        w2Conds.Add((w2HasHush || w2HasYoke ? "1b" : "1",
                     "規則の保持者を同数値の対照に差し替え（" + off.Label + "）",
                     off.Enemy, null, null));

    // 条件2〜: 敵を1体ずつ空席にする。埋めない（埋めると2変数が動く）。
    int w2Seat = 2;
    foreach ((int slot, UnitDef def) in w2Base.Occupied())
    {
        w2Conds.Add((w2Seat.ToString(),
                     FormationRules.SeatNames[slot] + " " + def.Name + " を抜く（空席）",
                     W2Swap(w2Base, slot, null), null, null));
        w2Seat++;
    }

    // --- 測る ----------------------------------------------------------------------------------
    var w2Sw = new System.Diagnostics.Stopwatch();

    // 基準は全5波を回す（0-1 の分布と、docs/balance.md との突き合わせを兼ねる）。
    w2Sw.Restart();
    var w2Grid = new double[w2Builds.Length][];
    var w2Turns = new double[w2Builds.Length];
    var w2BackDmg = new double[w2Builds.Length];
    var w2BackKill = new double[w2Builds.Length];
    string[] w2BackIds = w2Base.Occupied()
        .Where(o => FormationRules.RowOf(o.Slot) == Row.Back)
        .Select(o => o.Def.Id).Distinct().ToArray();
    for (int b = 0; b < w2Builds.Length; b++)
    {
        Formation f = w2Builds[b].F;
        w2Grid[b] = new double[w2Stages.Count];
        for (int st = 0; st < w2Stages.Count; st++)
        {
            int wins = 0, turns = 0, backDmg = 0, backKill = 0;
            for (int seed = 0; seed < W2Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, w2Stages[st].Enemy, seed, verbose: false);
                if (r.PlayerWon) wins++;
                if (st != w2Idx) continue;
                turns += r.Turns;
                foreach (string id in w2BackIds)
                    if (r.TallyByUnit.TryGetValue(id, out UnitTally? t))
                    {
                        backDmg += t.DamageTaken;
                        backKill += t.Deaths;
                    }
            }
            w2Grid[b][st] = wins * 100.0 / W2Seeds;
            if (st == w2Idx)
            {
                w2Turns[b] = (double)turns / W2Seeds;
                w2BackDmg[b] = (double)backDmg / W2Seeds;
                w2BackKill[b] = (double)backKill / W2Seeds;
            }
        }
    }
    double w2GridSec = w2Sw.Elapsed.TotalSeconds;

    // 条件ごとの対象波の勝率
    var w2Cells = new double[w2Conds.Count][];
    var w2Secs = new double[w2Conds.Count];
    for (int c = 0; c < w2Conds.Count; c++)
    {
        w2Sw.Restart();
        var row = new double[w2Builds.Length];
        for (int b = 0; b < w2Builds.Length; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < W2Seeds; seed++)
                if (BattleEngine.Run(w2Builds[b].F, w2Conds[c].Enemy, seed, verbose: false,
                                     yoke: w2Conds[c].Yoke, hush: w2Conds[c].Hush).PlayerWon) wins++;
            row[b] = wins * 100.0 / W2Seeds;
        }
        w2Cells[c] = row;
        w2Secs[c] = w2Sw.Elapsed.TotalSeconds;
    }

    // --- 出力 ----------------------------------------------------------------------------------
    Console.WriteLine("# 波の解剖: 第" + (w2Idx + 1) + "波（" + w2Stages[w2Idx].Name + "）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 wave2 " + (w2Idx + 1) + "` の出力。");
    Console.WriteLine($"`CompareBuilds()` の {w2Builds.Length} 行 × seed 0..{W2Seeds - 1}。");
    Console.WriteLine("`Traits.cs` / `UnitCatalog.cs` / `Stages` / `CompareBuilds()` は1行も触っていない。");
    Console.WriteLine("**診断用なので `docs/` には置かない。**");

    // --- 0-1 ---
    Console.WriteLine();
    Console.WriteLine("## 0-1. 分布（基準・全5波）");
    Console.WriteLine();
    Console.WriteLine("| 波 | 平均 | 0.0% | 100.0% | 中間（情報セル） |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    for (int st = 0; st < w2Stages.Count; st++)
    {
        var v = w2Grid.Select(r => r[st]).ToArray();
        Console.WriteLine($"| 第{st + 1}波 | {v.Average():F2} | {v.Count(x => x == 0.0)} "
                          + $"| {v.Count(x => x == 100.0)} | {v.Count(x => x > 0.0 && x < 100.0)} |");
    }

    Console.WriteLine();
    Console.WriteLine("### `docs/balance.md` との突き合わせ用（compare と同じ書式）");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(w2Stages.Select((_, i) => $" 第{i + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(w2Stages.Select(_ => "---:|")));
    for (int b = 0; b < w2Builds.Length; b++)
        Console.WriteLine($"| {w2Builds[b].Name} |"
                          + string.Concat(w2Grid[b].Select(x => $" {x:F1}% |")));

    // --- 0-2 / 0-3 ---
    Console.WriteLine();
    Console.WriteLine("## 0-2 / 0-3. 全5波の敵と波ルール");
    Console.WriteLine();
    Console.WriteLine("| 波 | 席 | 駒 | HP | 攻 | 速 | 型 | 特性 | 行動 |");
    Console.WriteLine("|---|---|---|--:|--:|--:|---|---|---|");
    for (int st = 0; st < w2Stages.Count; st++)
        foreach ((int slot, UnitDef def) in w2Stages[st].Enemy.Occupied())
        {
            string acts = def.Actions is { } a
                ? string.Join(" → ", a.Select(x => x.Kind == ActionKind.Charge
                      ? "溜め" : x.Kind.ToString() + (x.AttackPercent == 100 ? "" : "(" + x.AttackPercent + "%)")))
                : "—";
            Console.WriteLine($"| 第{st + 1}波 | {FormationRules.SeatNames[slot]} | {def.Name} "
                + $"| {def.MaxHp} | {def.Attack} | {def.Speed} | {def.Pattern} "
                + $"| {(def.Traits.Count == 0 ? "—" : string.Join(" / ", def.Traits))} | {acts} |");
        }

    // --- 0-4 ---
    Console.WriteLine();
    Console.WriteLine("## 0-4. 第" + (w2Idx + 1) + "波の 100.0% 群 と 0.0% 群");
    Console.WriteLine();
    Console.WriteLine("`外` = ターン外の行動を持つ駒の枚数（棘・仇討ち・軋み・追い打ち）。");
    Console.WriteLine("`届` = 後列に届く手段（貫き・標・毒・範囲）を持つか。`癒` = 回復経路を持つか。");
    Console.WriteLine("`後列与ダメ` / `後列撃破` はこの波の後列2体に通した量（1戦あたり・実測）。");
    foreach (var (grpName, want) in new[] { ("100.0% 群", 100.0), ("0.0% 群", 0.0) })
    {
        Console.WriteLine();
        Console.WriteLine("### " + grpName);
        Console.WriteLine();
        Console.WriteLine("| 編成 | 駒 | 外 | 届 | 癒 | 総攻 | 決着T | 後列与ダメ | 後列撃破 |");
        Console.WriteLine("|---|---|--:|:-:|:-:|--:|--:|--:|--:|");
        for (int b = 0; b < w2Builds.Length; b++)
        {
            if (w2Grid[b][w2Idx] != want) continue;
            Formation f = w2Builds[b].F;
            Console.WriteLine($"| {w2Builds[b].Name} "
                + $"| {string.Join("・", f.Occupied().Select(o => o.Def.Name))} "
                + $"| {W2CountHas(f, w2OutOfTurn)} | {(W2Reaches(f) ? "○" : "—")} "
                + $"| {(W2HasAny(f, w2Healers) ? "○" : "—")} | {W2TotalAttack(f)} "
                + $"| {w2Turns[b]:F1} | {w2BackDmg[b]:F0} | {w2BackKill[b]:F2} |");
        }
    }

    // --- 表A ---
    Console.WriteLine();
    Console.WriteLine("## 表A. 関門ごとの寄与");
    Console.WriteLine();
    Console.WriteLine("**主要な指標は `100.0%` の列。** 平均ではない。");
    Console.WriteLine("`Δ100%` は基準からの増減で、**外して 100.0% が増えるならその関門は天井を押さえている。**");
    Console.WriteLine();
    Console.WriteLine("| 条件 | 内容 | 平均 | 100.0% | Δ100% | 0.0% | 中間 | 最大差 | 動いた行 | 秒 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    for (int c = 0; c < w2Conds.Count; c++)
    {
        double[] v = w2Cells[c], b0 = w2Cells[0];
        double maxDiff = 0; int moved = 0;
        for (int b = 0; b < v.Length; b++)
        {
            double d = v[b] - b0[b];
            if (Math.Abs(d) > Math.Abs(maxDiff)) maxDiff = d;
            if (d != 0) moved++;
        }
        Console.WriteLine($"| {w2Conds[c].Id} | {w2Conds[c].Name} | {v.Average():F2} "
            + $"| {v.Count(x => x == 100.0)} | {v.Count(x => x == 100.0) - b0.Count(x => x == 100.0):+0;-0;0} "
            + $"| {v.Count(x => x == 0.0)} | {v.Count(x => x > 0 && x < 100)} "
            + $"| {maxDiff:+0.0;-0.0;0.0} | {moved} | {w2Secs[c]:F1} |");
    }

    // --- 表B ---
    Console.WriteLine();
    Console.WriteLine("## 表B. 100% 群 と 0% 群 の性格");
    Console.WriteLine();
    var w2Hi = Enumerable.Range(0, w2Builds.Length).Where(b => w2Grid[b][w2Idx] == 100.0).ToArray();
    var w2Lo = Enumerable.Range(0, w2Builds.Length).Where(b => w2Grid[b][w2Idx] == 0.0).ToArray();
    var w2Mid = Enumerable.Range(0, w2Builds.Length)
                          .Where(b => w2Grid[b][w2Idx] > 0 && w2Grid[b][w2Idx] < 100).ToArray();
    string W2Cmp(double hi, double lo) =>
        lo == 0 && hi == 0 ? "—" : lo == 0 ? "∞" : hi == 0 ? "0.00倍" : $"{hi / lo:F2}倍";
    var w2Items = new (string Name, Func<int, double> V)[]
    {
        ("ターン外の行動を持つ駒の平均枚数", b => W2CountHas(w2Builds[b].F, w2OutOfTurn)),
        ("後列に届く手段を持つ行の割合(%)", b => W2Reaches(w2Builds[b].F) ? 100 : 0),
        ("回復を持つ行の割合(%)", b => W2HasAny(w2Builds[b].F, w2Healers) ? 100 : 0),
        ("平均の総攻撃力", b => W2TotalAttack(w2Builds[b].F)),
        ("決着ターンの平均", b => w2Turns[b]),
        ("（実測）後列2体への与ダメ/戦", b => w2BackDmg[b]),
        ("（実測）後列2体の撃破/戦", b => w2BackKill[b]),
    };
    Console.WriteLine($"| 項目 | 100%群({w2Hi.Length}行) | 0%群({w2Lo.Length}行) | 比 | 中間帯({w2Mid.Length}行) |");
    Console.WriteLine("|---|--:|--:|--:|--:|");
    foreach (var (nm, val) in w2Items)
    {
        double hi = w2Hi.Length == 0 ? 0 : w2Hi.Average(val);
        double lo = w2Lo.Length == 0 ? 0 : w2Lo.Average(val);
        double md = w2Mid.Length == 0 ? 0 : w2Mid.Average(val);
        Console.WriteLine($"| {nm} | {hi:F2} | {lo:F2} | {W2Cmp(hi, lo)} | {md:F2} |");
    }

    // --- 表C ---
    // 各波の規則を同数値の対照で外し、**課金対象を持つ行と持たない行に分けて**変化を見る。
    // 「課金対象を持たない編成にも効くか」がこの表の問い（指示書 §0 の仮説の検証）。
    Console.WriteLine();
    Console.WriteLine("## 表C. 波ルールの性格の比較（全波を実測）");
    Console.WriteLine();
    Console.WriteLine("**規則の外し方は全波で同じ**——保持者を**同数値・特性なしの対照駒**に差し替える");
    Console.WriteLine("（粛・渇き・軛・殉教はどれも素の駒と数値・型・速さが1つも違わない）。");
    Console.WriteLine("`課金対象` の判定は窓口の呼び出し元から機械的に引く（役割名で数えない）:");
    Console.WriteLine();
    Console.WriteLine("- 粛（第二波）= ターン外の行動を持つ駒 ≥1（`CanActOutOfTurn` を通る4本）");
    Console.WriteLine("- 渇き（第三波）= 回復経路の特性 ≥1（`ctx.Heal` の呼び出し元9経路）");
    Console.WriteLine($"- 軛（第四波）= 規則を外した盤面で単発 > {YokeTrait.Cap} を敵に通す行（seed 0..{W2ProbeSeeds - 1} の実測）");
    Console.WriteLine("- 殉教（第五波）= 規則を外した盤面で単体攻撃を敵に振る行（同上。庇うは Single にしか割り込まない）");
    Console.WriteLine();
    Console.WriteLine("| 波 | ルール | 対照 | 課金対象 | Δ平均(対象) | Δ平均(非対象) | 非対象で動いた行 | 非対象の最大差 | 課金対象を持たない編成にも効くか |");
    Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|");
    for (int st = 0; st < w2Stages.Count; st++)
    {
        var offC = W2RuleOff(st);
        if (offC is not { } o)
        {
            Console.WriteLine($"| 第{st + 1}波 | なし | — | — | — | — | — | — | 規則を持たない |");
            continue;
        }
        Formation offEnemy = o.Enemy;
        UnitDef holder = w2Stages[st].Enemy.Occupied()
                            .First(x => w2Controls.ContainsKey(x.Def.Id)).Def;

        // 課金対象の判定
        var taxed = new bool[w2Builds.Length];
        bool probe = holder.Traits.Contains(TraitId.Yoke) || holder.Traits.Contains(TraitId.Martyr);
        for (int b = 0; b < w2Builds.Length; b++)
        {
            Formation f = w2Builds[b].F;
            if (holder.Traits.Contains(TraitId.Hush)) taxed[b] = W2CountHas(f, w2OutOfTurn) > 0;
            else if (holder.Traits.Contains(TraitId.Drought)) taxed[b] = W2HasAny(f, w2Healers);
            else if (probe)
            {
                int lo = f.Count, hi = lo + offEnemy.Count;   // 敵の InstanceId の範囲（Add は味方→敵の順）
                bool hit = false;
                for (int seed = 0; seed < W2ProbeSeeds && !hit; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, offEnemy, seed, verbose: true);
                    hit = holder.Traits.Contains(TraitId.Yoke)
                        ? r.Events.Any(e => e.Kind == BattleEventKind.Damage && !e.FriendlyFire
                                            && e.TargetId >= lo && e.TargetId < hi
                                            && e.Amount > YokeTrait.Cap)
                        : r.Events.Any(e => e.Kind == BattleEventKind.Attack
                                            && e.Pattern == AttackPattern.Single
                                            && e.TargetId >= lo && e.TargetId < hi);
                }
                taxed[b] = hit;
            }
        }

        // 基準と対照
        double sumT = 0, sumN = 0, maxN = 0; int nT = 0, nN = 0, movedN = 0;
        for (int b = 0; b < w2Builds.Length; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < W2Seeds; seed++)
                if (BattleEngine.Run(w2Builds[b].F, offEnemy, seed, verbose: false).PlayerWon) wins++;
            double d = wins * 100.0 / W2Seeds - w2Grid[b][st];
            if (taxed[b]) { sumT += d; nT++; }
            else
            {
                sumN += d; nN++;
                if (d != 0) movedN++;
                if (Math.Abs(d) > Math.Abs(maxN)) maxN = d;
            }
        }
        string verdict = nN == 0 ? "非対象が0行（判定不能）"
                       : movedN == 0 ? "**効かない**（非対象は全行 ±0.0）"
                       : $"効く（非対象 {movedN}/{nN} 行が動く）";
        Console.WriteLine($"| 第{st + 1}波 | {holder.Name} | {w2Controls[holder.Id].Def.Name} "
            + $"| {nT}/{w2Builds.Length} | {(nT == 0 ? 0 : sumT / nT):+0.00;-0.00;0.00} "
            + $"| {(nN == 0 ? 0 : sumN / nN):+0.00;-0.00;0.00} | {movedN} "
            + $"| {maxN:+0.0;-0.0;0.0} | {verdict} |");
        Console.Out.Flush();
    }

    // 断罪（第五波）だけは catalog に同数値の対照が無いので、ここでローカルに作る
    // （`gradient` / `aim` / `route` と同じ扱い。`UnitCatalog` には戻さない）。
    // 数値・型・速さは Hero2 / Seer と1つも違わず、差分は Condemn を落としただけ。
    //
    // **課金対象は粛と同じではない。** 断罪は `ctx.InReaction` の中でしか発火しないので、
    // `ctx.Reaction` で包まれる2本（棘・仇討ち）だけが対象——軋みは `ctx.Interrupt`、
    // 追い打ちは `ctx.PerformAttack` の直呼びなので、`CanActOutOfTurn` は通るが断罪は踏まない。
    var w2Reactors = new[] { TraitId.Thorns, TraitId.Avenge };
    var w2PlainHero = new UnitDef
    {
        Id = "hero_v_plain", Name = "勇者候補（断罪なし）", MaxHp = 90, Attack = 20, Speed = 14,
        Traits = Array.Empty<TraitId>()
    };
    var w2PlainSeer = new UnitDef
    {
        Id = "seer_plain", Name = "審問官（断罪なし）", MaxHp = 76, Attack = 12, Speed = 10,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.All
    };
    {
        const int W2Fifth = 4;
        Formation cond = w2Stages[W2Fifth].Enemy.Clone();
        foreach ((int slot, UnitDef def) in w2Stages[W2Fifth].Enemy.Occupied())
        {
            if (def.Id == "hero_v") cond[slot] = w2PlainHero;
            if (def.Id == "seer") cond[slot] = w2PlainSeer;
        }
        double sumT = 0, sumN = 0, maxN = 0; int nT = 0, nN = 0, movedN = 0;
        for (int b = 0; b < w2Builds.Length; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < W2Seeds; seed++)
                if (BattleEngine.Run(w2Builds[b].F, cond, seed, verbose: false).PlayerWon) wins++;
            double d = wins * 100.0 / W2Seeds - w2Grid[b][W2Fifth];
            if (W2HasAny(w2Builds[b].F, w2Reactors)) { sumT += d; nT++; }
            else
            {
                sumN += d; nN++;
                if (d != 0) movedN++;
                if (Math.Abs(d) > Math.Abs(maxN)) maxN = d;
            }
        }
        string verdict = movedN == 0 ? "**効かない**（非対象は全行 ±0.0）"
                                     : $"効く（非対象 {movedN}/{nN} 行が動く）";
        Console.WriteLine($"| 第5波 | 断罪（勇者候補・審問官） | 同数値の断罪なし2体 "
            + $"| {nT}/{w2Builds.Length} | {(nT == 0 ? 0 : sumT / nT):+0.00;-0.00;0.00} "
            + $"| {(nN == 0 ? 0 : sumN / nN):+0.00;-0.00;0.00} | {movedN} "
            + $"| {maxN:+0.0;-0.0;0.0} | {verdict} |");
        Console.WriteLine();
        Console.WriteLine("> 第5波の行は2本ある——**殉教（庇う）と断罪は課金対象が違う。**");
        Console.WriteLine("> 断罪の対象は `ctx.Reaction` に包まれる2本（棘・仇討ち）だけで、");
        Console.WriteLine("> 軋み（`ctx.Interrupt`）と追い打ち（`PerformAttack` の直呼び）は `CanActOutOfTurn` を");
        Console.WriteLine("> 通るのに断罪は踏まない。**粛の4本より狭い。**");
    }

    // --- 付録 ---
    Console.WriteLine();
    Console.WriteLine("## 付録: 条件別の全行（対象波の勝率）");
    Console.WriteLine();
    Console.WriteLine("| 編成 |" + string.Concat(w2Conds.Select(c => $" {c.Id} |")));
    Console.WriteLine("|---|" + string.Concat(w2Conds.Select(_ => "---:|")));
    for (int b = 0; b < w2Builds.Length; b++)
        Console.WriteLine($"| {w2Builds[b].Name} |"
            + string.Concat(w2Cells.Select(r => $" {r[b]:F1} |")));

    Console.WriteLine();
    Console.WriteLine($"基準の全5波グリッド {w2GridSec:F1} 秒 / 条件ごとの所要は表A の `秒` 列。");
    return;
}
}
