using BattleCore;
using static Common;

// =====================================================================================
// turn モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "turn")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 turn
// =====================================================================================

static class TurnDiag
{
public static void Phase0(string[] args, int stageIndex)
{
    var fpBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> fpStages = EnemyCatalog.Stages;
    const int FpSeeds = 200;   // compare / spread / whet / burn / favor と同じ帯

    var fpRows = fpBuilds.Where(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hiyo))).ToArray();

    Console.WriteLine("# 火選りを手番へ降ろす前の地図（第60期 Phase 0）");
    Console.WriteLine();
    Console.WriteLine("**盤面は1つも動かさない。** 0-1〜0-3 と 0-5 は戦闘0回、0-4 と 0-6 は既存の経路を読むだけ。");
    Console.WriteLine();

    // ---- 0-5 を先に出す（以降の表の分母になる）---------------------------------------
    Console.WriteLine("## 0-5. ヒヨを含む行（数え直し）");
    Console.WriteLine();
    Console.WriteLine($"`CompareBuilds()` は **{fpBuilds.Count()} 行**。うちヒヨを含むのは **{fpRows.Length} 行**。");
    Console.WriteLine();
    Console.WriteLine("| # | 行 | 前1 | 前3 | 中央 | 後1 | 後3 | ヒヨの席 | 次数 |");
    Console.WriteLine("|--:|---|---|---|---|---|---|:-:|--:|");
    for (int i = 0; i < fpRows.Length; i++)
    {
        Formation f = fpRows[i].F;
        string Cell(int s) { UnitDef? d = f[s]; return d is null ? "—" : d.Name; }
        int fs = f.Occupied().First(o => ReferenceEquals(o.Def, UnitCatalog.Hiyo)).Slot;
        int deg = Enumerable.Range(0, FormationRules.PlayableSlotCount)
                            .Count(s => s != fs && f[s] is not null && FormationRules.AreAdjacent(fs, s));
        Console.WriteLine($"| {i + 1} | {fpRows[i].Name} | {Cell(0)} | {Cell(1)} | {Cell(2)} | {Cell(3)} | {Cell(4)} "
                        + $"| **{FormationRules.SeatNames[fs]}** | {deg} |");
    }
    Console.WriteLine();

    // ---- 0-1. 止めうる経路 -----------------------------------------------------------
    Console.WriteLine("## 0-1. 移設後に火選りを止めうる経路（call-site の全数）");
    Console.WriteLine();
    Console.WriteLine("`BattleEngine` の行動順ループで手番が飛ぶ・行動が変わる箇所を上から全部挙げる。");
    Console.WriteLine();
    Console.WriteLine("| # | 経路 | 実装 | ヒヨに効くか |");
    Console.WriteLine("|--:|---|---|---|");
    Console.WriteLine("| 1 | 痺れ（`StatusKeys.Stun`） | 行動順ループの先頭で `continue` | **効く**（下で数える） |");
    Console.WriteLine("| 2 | まどろみ | `ctx.Colossus.Slumber && HasTrait(Colossus)` | **効かない**（規則の既定が `false`・保持者はゴルムだけ） |");
    Console.WriteLine("| 3 | `CanAct` 偽 | `actor.Traits.All(t => t.CanAct(...))` | **効かない**（上書きは のろま／不動／断ち／追い打ちの4本で、ヒヨは1つも持たない） |");
    Console.WriteLine("| 4 | `ActionIndex` の周期 | `Actions` の要素を順に消費 | **効かない**（`[Skill]` の1要素。空回りしない） |");
    Console.WriteLine("| 5 | 死亡 | `IsAlive` | 効く（移設前も同じ） |");
    Console.WriteLine();
    Console.WriteLine("**痺れの書き手は3本**（`SetCounter(StatusKeys.Stun, 1)` の全数から自傷を除いた）。");
    Console.WriteLine();
    Console.WriteLine("| 書き手 | 特性 | 誰に付くか | 移設後のヒヨに届くか |");
    Console.WriteLine("|---|---|---|---|");
    Console.WriteLine("| 縛め（クグ） | `BindTrait` | **味方**を無作為に1体（第2ターン以降） | 届く |");
    Console.WriteLine("| 痺れ | `ParalyzeTrait` | 殴った**相手** | 届く（敵側の保持者がヒヨを殴ったとき） |");
    Console.WriteLine("| 断罪 | `CondemnTrait` | 殴ってきた**攻撃者** | **移設で届かなくなる**（ヒヨは `PerformAttack` を通らなくなる） |");
    Console.WriteLine();
    Console.WriteLine("**行ごとの供給者（味方）**と**波ごとの供給者（敵）**を数える。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 味方の縛め | 味方の痺れ | 味方の断罪 |");
    Console.WriteLine("|---|--:|--:|--:|");
    foreach (var b in fpRows)
        Console.WriteLine($"| {b.Name} | {FvCountTrait(b.F, TraitId.Bind)} | {FvCountTrait(b.F, TraitId.Paralyze)} | {FvCountTrait(b.F, TraitId.Condemn)} |");
    Console.WriteLine();
    Console.WriteLine("| 波 | 敵の痺れ | 敵の断罪 | 保持者 |");
    Console.WriteLine("|---|--:|--:|---|");
    for (int w = 0; w < fpStages.Count; w++)
    {
        Formation e = fpStages[w].Enemy;
        var who = e.Occupied()
            .Where(o => o.Def.Traits.Contains(TraitId.Paralyze) || o.Def.Traits.Contains(TraitId.Condemn))
            .Select(o => o.Def.Name + (o.Def.Traits.Contains(TraitId.Paralyze) ? "（痺れ）" : "（断罪）"))
            .ToList();
        Console.WriteLine($"| 第{w + 1}波 | {FvCountTrait(e, TraitId.Paralyze)} | {FvCountTrait(e, TraitId.Condemn)} | "
                        + (who.Count == 0 ? "—" : string.Join(" / ", who)) + " |");
    }
    Console.WriteLine();

    // ---- 0-2. 粛との関係 -------------------------------------------------------------
    Console.WriteLine("## 0-2. 粛（`Hush`）との関係（**予測**）");
    Console.WriteLine();
    Console.WriteLine("粛は `BattleContext.CanActOutOfTurn` の最後1箇所でターン外の行動を止める。");
    Console.WriteLine("**行動順ループは `CanActOutOfTurn` を1度も呼ばない**（呼び出し元は特性側の4本＝棘・仇討ち・軋み・追い打ちだけ）。");
    Console.WriteLine("`OnTurnStart` も行動順ループの外側なので、こちらも通らない。");
    Console.WriteLine();
    Console.WriteLine("> **予測: 移設の前後で粛の効きは変わらない。第2波は動かない。**");
    Console.WriteLine("> `火選り無風型` の第2波 0.0% は不動のカドが粛の下で置物になる行の性質で、火選りとは無関係。");
    Console.WriteLine();

    // ---- 0-3. 速さの前後関係 ---------------------------------------------------------
    Console.WriteLine("## 0-3. 速さの前後関係（ヒヨは速6）");
    Console.WriteLine();
    Console.WriteLine("行動順は速さ降順 → チーム → スロット。**ヒヨより速い味方は、ヒヨが配る前にその手番を終えている。**");
    Console.WriteLine();
    Console.WriteLine("| 行 | ヒヨより速い味方 | 同速 | ヒヨより遅い味方 |");
    Console.WriteLine("|---|---|---|---|");
    foreach (var b in fpRows)
    {
        var others = b.F.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Hiyo)).Select(o => o.Def).ToList();
        string Grp(Func<UnitDef, bool> p)
        {
            var l = others.Where(p).Select(d => $"{d.Name}({d.Speed})").ToList();
            return l.Count == 0 ? "—" : string.Join(" / ", l);
        }
        Console.WriteLine($"| {b.Name} | {Grp(d => d.Speed > UnitCatalog.Hiyo.Speed)} | {Grp(d => d.Speed == UnitCatalog.Hiyo.Speed)} | {Grp(d => d.Speed < UnitCatalog.Hiyo.Speed)} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**ボルグ速{UnitCatalog.Borg.Speed} > ヒヨ速{UnitCatalog.Hiyo.Speed}** ——移設すれば、ヒヨの番が回る時点で火は既に点いている（これが移設の狙い）。");
    Console.WriteLine($"**ホタ速{UnitCatalog.Hota.Speed} > ヒヨ速{UnitCatalog.Hiyo.Speed}** ——**ホタはその手番に配られた強化を、そのターンには使えない。**");
    Console.WriteLine();
    Console.WriteLine("> **予測（P6・新しく生まれる遅れ）: 第58期の遅れは消えるのではなく形が変わる。**");
    Console.WriteLine("> 移設前は `OnTurnStart` がホタの手番より前なので、配った強化は**そのターンから**乗っていた。");
    Console.WriteLine("> 移設後はヒヨ（速6）がホタ（速7）より後に動くので、**乗るのは次のターンから**になる。");
    Console.WriteLine("> **「第1ターンだけ鈍らせる」（1回）は消えるが、「1ターン遅れて乗る」（毎ターン）が新しく生まれる。**");
    Console.WriteLine();

    // ---- 0-4. ヒヨの現在の出力 -------------------------------------------------------
    Console.WriteLine("## 0-4. ヒヨの現在の出力（移設で捨てる量）");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{FpSeeds - 1}。`振り` は `PerformAttack` を通った回数、`与ダメ` は敵に通した量。");
    Console.WriteLine("`空振り` は `FavorIdle`（盤上に燃えている味方が1体もいなかった手番）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 波 | 勝率 | 決着T | ヒヨの振り/戦 | ヒヨの与ダメ/戦 | 発火/戦 | 空振り/戦 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    double swSum = 0, dmgSum = 0; int nCell = 0;
    foreach (var b in fpRows)
    {
        double swAll = 0, dmAll = 0, fiAll = 0, idAll = 0;
        for (int w = 0; w < fpStages.Count; w++)
        {
            double win = 0, turns = 0, sw = 0, dm = 0, fi = 0, id = 0;
            for (int seed = 0; seed < FpSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(b.F, fpStages[w].Enemy, seed, verbose: false);
                if (r.PlayerWon) win++;
                turns += r.Turns; fi += r.FavorFires; id += r.FavorIdle;
                if (r.TallyByUnit.TryGetValue("hiyo", out UnitTally? t))
                { sw += t.Attacks; dm += t.DamageToEnemy; }
            }
            double n = FpSeeds;
            Console.WriteLine($"| {b.Name} | 第{w + 1}波 | {win * 100 / n:0.0}% | {turns / n:0.0} "
                            + $"| {sw / n:0.00} | {dm / n:0.0} | {fi / n:0.00} | {id / n:0.00} |");
            swAll += sw / n; dmAll += dm / n; fiAll += fi / n; idAll += id / n;
            swSum += sw / n; dmgSum += dm / n; nCell++;
        }
        Console.WriteLine($"| | **5波平均** | | | **{swAll / 5:0.00}** | **{dmAll / 5:0.0}** | **{fiAll / 5:0.00}** | **{idAll / 5:0.00}** |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine($"**移設で捨てる量は 20 セルの平均で 振り {swSum / nCell:0.00} 回/戦・与ダメ {dmgSum / nCell:0.0}/戦。**");
    Console.WriteLine();

    // ---- 0-6. 現在値 -----------------------------------------------------------------
    Console.WriteLine("## 0-6. `docs/balance.md` の現在値（4行 × 5波）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 情報セル |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var b in fpRows)
    {
        var v = new double[fpStages.Count];
        for (int w = 0; w < fpStages.Count; w++)
        {
            int win = 0;
            for (int seed = 0; seed < FpSeeds; seed++)
                if (BattleEngine.Run(b.F, fpStages[w].Enemy, seed, false).PlayerWon) win++;
            v[w] = win * 100.0 / FpSeeds;
        }
        int info = v.Count(x => x > 5.0 && x < 95.0);   // 第22期の狭義の中間帯
        Console.WriteLine($"| {b.Name} " + string.Concat(v.Select(x => $"| {x:0.0}% "))
                        + $"| {v.Average():0.0}% | **{info}** |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine("**情報セルは狭義の中間帯 5.0 < x < 95.0**（第22期）。Q5 はこの列で判定する。");
    Console.WriteLine();
    return;
}

public static void Run(string[] args, int stageIndex)
{
    var fvBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> fvStages = EnemyCatalog.Stages;
    string fvMode = args.Length > 2 ? args[2] : "";
    bool fvAlt = fvMode == "alt";
    int fvFrom = fvAlt ? 200 : 0;              // alt は別 seed 帯（200..599）
    int fvSeeds = fvAlt ? 400 : 200;           // それ以外は compare と同じ帯

    var fvRows = fvBuilds.Where(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hiyo))).ToArray();

    UnitDef fvTs = FvTurnStartDef(), fvPlainAct = FvPlainActDef(), fvPlain = FvPlainDef();
    Formation FvV0(Formation f) => FvSwap(f, UnitCatalog.Hiyo, fvTs);   // 移設前（ターン頭）
    Formation FvV1(Formation f) => f;                                    // 移設後（＝現行の盤面）
    Formation FvV2(Formation f) => FvSwap(f, UnitCatalog.Hiyo, fvPlainAct);
    Formation FvV3(Formation f) => FvSwap(f, UnitCatalog.Hiyo, fvPlain);

    // 版ごとの計数。ヒヨの id は版で変わるので、駒側の集計は「ヒヨの席にいる駒」で引く。
    var fvIds = new[] { "hiyo", "hiyo_ts", "hiyo_plain_act", "hiyo_plain" };

    FvStat FvMeasure(Formation f, Formation enemy, FavorRule? rule)
    {
        // 味方の総与ダメは**味方の `Def.Id` 集合**で割る（`TallyByUnit` は `Def.Id` で引くので
        // `InstanceId` の範囲では割れない。第59期 `BzStat` と同じ作法）。
        var mine = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        var z = new FvStat();
        for (int seed = fvFrom; seed < fvFrom + fvSeeds; seed++)
        {
            BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false, favor: rule);
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Fires += r.FavorFires; z.Idle += r.FavorIdle;
            z.Whetted += r.FavorWhetted; z.Dulled += r.FavorDulled;
            z.Given += r.FavorGiven; z.Taken += r.FavorTaken; z.ToPyre += r.FavorToPyre;
            foreach ((string k, int v) in r.FavorDullTo)
                z.DullTo[k] = z.DullTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.FavorWhetTo)
                z.WhetTo[k] = z.WhetTo.TryGetValue(k, out double a) ? a + v : v;
            foreach (string id in fvIds)
                if (r.TallyByUnit.TryGetValue(id, out UnitTally? t))
                { z.Swings += t.Attacks; z.Dmg += t.DamageToEnemy; z.Life += t.LastActiveTurn; }
            z.TeamDmg += r.TallyByUnit.Where(kv => mine.Contains(kv.Key)).Sum(kv => kv.Value.DamageToEnemy);
        }
        double n = fvSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n; z.Fires /= n; z.Idle /= n;
        z.Whetted /= n; z.Dulled /= n; z.Given /= n; z.Taken /= n; z.ToPyre /= n;
        z.Swings /= n; z.Dmg /= n; z.Life /= n; z.TeamDmg /= n;
        foreach (string k in z.DullTo.Keys.ToList()) z.DullTo[k] /= n;
        foreach (string k in z.WhetTo.Keys.ToList()) z.WhetTo[k] /= n;
        return z;
    }
    double[] FvWins(Formation f, FavorRule? rule)
    {
        var v = new double[fvStages.Count];
        for (int w = 0; w < fvStages.Count; w++)
        {
            int win = 0;
            for (int seed = fvFrom; seed < fvFrom + fvSeeds; seed++)
                if (BattleEngine.Run(f, fvStages[w].Enemy, seed, false, favor: rule).PlayerWon) win++;
            v[w] = win * 100.0 / fvSeeds;
        }
        return v;
    }
    static int FvInfo(double[] v) => v.Count(x => x > 5.0 && x < 95.0);
    static string FvCells(double[] v) => string.Concat(v.Select(x => $"| {x:0.0}% "));

    // ---- 係数の確定（第61期 §4）--------------------------------------------------------
    //
    // **乗算の比**を両 seed 帯で取る。1帯の比で桁の判断をしない（指示書 §4）。
    //
    //     比 = （乗算持ち＝熾のホタがいる行の伸び） ÷ （いない行の伸び）
    //
    // 比が 2.0 以上なら `Gain` は**ホタ専用のノブ**と判定して上げない。
    if (fvMode == "ratio")
    {
        Console.WriteLine("# `FavorRule` の係数の確定（第61期 §4）");
        Console.WriteLine();
        Console.WriteLine("`(4, 2)`（現行）と `(6, 2)` を火選り4行で比べ、**乗算の比**を両 seed 帯で取る。");
        Console.WriteLine("**乗算持ち（熾のホタ・`PyreTrait`）がいるのは `火選り (ヒヨ×ホタ)` の1行だけ。**");
        Console.WriteLine();
        var bands = new (string Tag, int From, int Seeds)[] { ("A 帯 0..199", 0, 200), ("B 帯 200..599", 200, 400) };
        var rows4 = fvBuilds.Where(b => b.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Hiyo))).ToArray();
        foreach ((string tag, int from, int seeds) in bands)
        {
            double[] WinsIn(Formation f, FavorRule? rule)
            {
                var v = new double[fvStages.Count];
                for (int w = 0; w < fvStages.Count; w++)
                {
                    int win = 0;
                    for (int seed = from; seed < from + seeds; seed++)
                        if (BattleEngine.Run(f, fvStages[w].Enemy, seed, false, favor: rule).PlayerWon) win++;
                    v[w] = win * 100.0 / seeds;
                }
                return v;
            }
            Console.WriteLine($"## {tag}");
            Console.WriteLine();
            Console.WriteLine("| 行 | 乗算 | (4,2) | (6,2) | 伸び | 情報セル (4,2) → (6,2) |");
            Console.WriteLine("|---|:-:|--:|--:|--:|--:|");
            double pyre = 0; var plainGains = new List<double>();
            foreach (var b in rows4)
            {
                bool hasPyre = b.F.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Pyre));
                double[] a = WinsIn(b.F, new FavorRule(4, 2));
                double[] c = WinsIn(b.F, new FavorRule(6, 2));
                double gain = c.Average() - a.Average();
                Console.WriteLine($"| {b.Name} | {(hasPyre ? "**あり**" : "—")} | {a.Average():0.0}% | {c.Average():0.0}% "
                                + $"| **{gain:+0.0;-0.0;0.0}** | {FvInfo(a)} → {FvInfo(c)} |");
                if (hasPyre) pyre = gain; else plainGains.Add(gain);
                Console.Out.Flush();
            }
            Console.WriteLine();
            double row2 = plainGains.Count > 0 ? plainGains[0] : 0;
            double mean = plainGains.Count > 0 ? plainGains.Average() : 0;
            Console.WriteLine($"- **行1 ÷ 行2（指示書と同じ形）: {pyre:0.0} ÷ {row2:0.0} = "
                            + $"{(Math.Abs(row2) > 0.05 ? (pyre / row2).ToString("0.00") : "—（分母が 0）")}**");
            Console.WriteLine($"- **行1 ÷ 乗算なし3行の平均: {pyre:0.0} ÷ {mean:0.0} = "
                            + $"{(Math.Abs(mean) > 0.05 ? (pyre / mean).ToString("0.00") : "—（分母が 0）")}**");
            Console.WriteLine();
        }
        Console.WriteLine("**判定**: 比が 2.0 未満なら `(6,2)` を採る。2.0 以上なら `Gain` はホタ専用のノブと判定して上げない。");
        Console.WriteLine();
        return;
    }

    // ---- 掃引だけ --------------------------------------------------------------------
    if (fvMode == "sweep")
    {
        Console.WriteLine("# 移設後の掃引（第60期 §4）");
        Console.WriteLine();
        Console.WriteLine("**4点だけ。** 移設で代金の相手の集合が変わっている可能性があるので、");
        Console.WriteLine("`(4, 4)` が現行値 `(4, 2)` を上回るかどうかが「集合が縮んだ」ことの独立な検算になる。");
        Console.WriteLine("**各点の対照は V2（素体）で足りる**（体の値段はノブで動かない）。");
        Console.WriteLine();
        var pts = new (string Tag, int G, int L)[] { ("現行 (4/2)", 4, 2), ("a (4/4)", 4, 4), ("b (6/2)", 6, 2), ("c (2/2)", 2, 2) };
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 素体との差 | 配った | 撒いた | 情報セル |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in fvRows)
        {
            double[] pv = FvWins(FvV2(b.F), null);
            Console.WriteLine($"| {b.Name} | V2 素体（手番） {FvCells(pv)}| **{pv.Average():0.0}%** | — | — | — | {FvInfo(pv)} |");
            foreach (var p in pts)
            {
                var rule = new FavorRule(p.G, p.L);
                double[] v = FvWins(FvV1(b.F), rule);
                double give = 0, take = 0;
                for (int w = 0; w < fvStages.Count; w++)
                {
                    FvStat z = FvMeasure(FvV1(b.F), fvStages[w].Enemy, rule);
                    give += z.Given / fvStages.Count; take += z.Taken / fvStages.Count;
                }
                Console.WriteLine($"| | {p.Tag} {FvCells(v)}| **{v.Average():0.0}%** | {v.Average() - pv.Average():+0.0;-0.0;0.0} "
                                + $"| {give:0.00} | {take:0.00} | {FvInfo(v)} |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        return;
    }

    // ---- 主表 ------------------------------------------------------------------------
    Console.WriteLine(fvAlt ? "# 移設の主表（別 seed 帯 200..599 の追試）" : "# 移設の主表（第60期）");
    Console.WriteLine();
    Console.WriteLine($"seed {fvFrom}..{fvFrom + fvSeeds - 1}。**V1（移設）は第60期に採用したので `UnitCatalog.Hiyo` そのもの**——");
    Console.WriteLine("**V1 の5波が `docs/balance.md` と一致することがこの診断の検算**（第36期 `gullet belly4` と同じ形）。");
    Console.WriteLine("V0 = 移設前（`OnTurnStart`）／V2 = 移設 + 素体（同数値・特性なし。**機構の帰属**）／");
    Console.WriteLine("V3 = 移設前の素体（`V0 − V3` が第58期の機構の帰属）。**V0 / V2 / V3 は診断のローカルの `UnitDef`。**");
    Console.WriteLine();

    Console.WriteLine("## 1. 勝率（4行 × 5波）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 情報セル | V1−V0 | V1−V2 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var attr = new List<(string Name, double A, double B, int I0, int I1)>();
    foreach (var b in fvRows)
    {
        double[] v0 = FvWins(FvV0(b.F), null);
        double[] v1 = FvWins(FvV1(b.F), null);
        double[] v2 = FvWins(FvV2(b.F), null);
        double[] v3 = FvWins(FvV3(b.F), null);
        Console.WriteLine($"| {b.Name} | V0 移設前 {FvCells(v0)}| **{v0.Average():0.0}%** | {FvInfo(v0)} | — | — |");
        Console.WriteLine($"| | **V1 移設（現行）** {FvCells(v1)}| **{v1.Average():0.0}%** | **{FvInfo(v1)}** "
                        + $"| **{v1.Average() - v0.Average():+0.0;-0.0;0.0}** | **{v1.Average() - v2.Average():+0.0;-0.0;0.0}** |");
        Console.WriteLine($"| | V2 素体（手番） {FvCells(v2)}| **{v2.Average():0.0}%** | {FvInfo(v2)} | — | — |");
        Console.WriteLine($"| | V3 素体（移設前） {FvCells(v3)}| **{v3.Average():0.0}%** | {FvInfo(v3)} "
                        + $"| （V0−V3 = {v0.Average() - v3.Average():+0.0;-0.0;0.0}） | |");
        attr.Add((b.Name, v1.Average() - v0.Average(), v1.Average() - v2.Average(), FvInfo(v0), FvInfo(v1)));
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine($"**帰属（V1 − V0）の4行平均: {attr.Average(a => a.A):+0.0;-0.0;0.0}pt** "
                    + $"／ **機構の帰属（V1 − V2）の4行平均: {attr.Average(a => a.B):+0.0;-0.0;0.0}pt**");
    Console.WriteLine($"**情報セルの合計: V0 {attr.Sum(a => a.I0)} → V1 {attr.Sum(a => a.I1)}**");
    Console.WriteLine();

    // ---- 計数 ------------------------------------------------------------------------
    Console.WriteLine("## 2. 計数（5波の平均）");
    Console.WriteLine();
    Console.WriteLine("`発火` は強化か弱体を1体でも配った手番、`空振り` は盤上に燃えている味方が1体もいなかった手番。");
    Console.WriteLine("`燃体` / `非燃体` は延べ体数、`配った` / `撒いた` は量。`熾火へ` は乗算持ちへ配った量。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 発火 | 空振り | 燃体 | 非燃体 | 配った | 撒いた | 熾火へ | ヒヨ振り | ヒヨ与ダメ | 味方総与ダメ | ヒヨ寿命T |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var q2 = new List<(string Name, double T0, double T1, double G0, double G1)>();
    foreach (var b in fvRows)
    {
        var acc = new Dictionary<string, FvStat>();
        foreach ((string tag, Formation f) in new[] { ("V0 移設前", FvV0(b.F)), ("V1 移設", FvV1(b.F)) })
        {
            var agg = new FvStat();
            for (int w = 0; w < fvStages.Count; w++)
            {
                FvStat z = FvMeasure(f, fvStages[w].Enemy, null);
                agg.Fires += z.Fires / 5; agg.Idle += z.Idle / 5; agg.Whetted += z.Whetted / 5;
                agg.Dulled += z.Dulled / 5; agg.Given += z.Given / 5; agg.Taken += z.Taken / 5;
                agg.ToPyre += z.ToPyre / 5; agg.Swings += z.Swings / 5; agg.Dmg += z.Dmg / 5;
                agg.TeamDmg += z.TeamDmg / 5; agg.Life += z.Life / 5;
                foreach ((string k, double v) in z.DullTo)
                    agg.DullTo[k] = agg.DullTo.TryGetValue(k, out double a) ? a + v / 5 : v / 5;
                foreach ((string k, double v) in z.WhetTo)
                    agg.WhetTo[k] = agg.WhetTo.TryGetValue(k, out double a) ? a + v / 5 : v / 5;
            }
            acc[tag] = agg;
            Console.WriteLine($"| {(tag.StartsWith("V0") ? b.Name : "")} | {tag} | {agg.Fires:0.00} | {agg.Idle:0.00} "
                            + $"| {agg.Whetted:0.00} | {agg.Dulled:0.00} | {agg.Given:0.00} | {agg.Taken:0.00} | {agg.ToPyre:0.00} "
                            + $"| {agg.Swings:0.00} | {agg.Dmg:0.0} | {agg.TeamDmg:0.0} | {agg.Life:0.0} |");
            Console.Out.Flush();
        }
        q2.Add((b.Name, acc["V0 移設前"].Taken, acc["V1 移設"].Taken, acc["V0 移設前"].Given, acc["V1 移設"].Given));

        // 受け手の内訳（Q1）
        Console.WriteLine($"| | **弱体の受け手 V0** | " + FvTop(acc["V0 移設前"].DullTo) + " |");
        Console.WriteLine($"| | **弱体の受け手 V1** | " + FvTop(acc["V1 移設"].DullTo) + " |");
        Console.WriteLine($"| | 強化の受け手 V0 | " + FvTop(acc["V0 移設前"].WhetTo) + " |");
        Console.WriteLine($"| | 強化の受け手 V1 | " + FvTop(acc["V1 移設"].WhetTo) + " |");
    }
    Console.WriteLine();
    Console.WriteLine("**Q2（`撒いた` の増減）**");
    Console.WriteLine();
    Console.WriteLine("| 行 | 撒いた V0 | 撒いた V1 | 差 | 配った V0 | 配った V1 | 差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    foreach (var q in q2)
        Console.WriteLine($"| {q.Name} | {q.T0:0.00} | {q.T1:0.00} | **{q.T1 - q.T0:+0.00;-0.00;0.00}** "
                        + $"| {q.G0:0.00} | {q.G1:0.00} | **{q.G1 - q.G0:+0.00;-0.00;0.00}** |");
    Console.WriteLine();

    // ---- Q4: 止まった手番 -------------------------------------------------------------
    Console.WriteLine("## 3. Q4 —— 移設で止まった手番（verbose・seed の先頭 50 本）");
    Console.WriteLine();
    Console.WriteLine("**痺れだけを数える。** 手番が飛ぶ経路は Phase 0-1 の5本で、ヒヨに効きうるのは痺れと死亡だけ。");
    Console.WriteLine("死亡と「決着して行動順ループが `break` した」は移設の前後で同じだけあるので、");
    Console.WriteLine("**新しい交絡になりうるのは痺れ1本**——`StatusSnapshot` の `痺` を数える");
    Console.WriteLine("（痺れは付いた次のターンの頭に立ち、その手番の先頭で消費される）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 波 | 痺れ V0/戦 | 痺れ V1/戦 | 生存T V1 | 撃った V1 | 差（死亡＋決着） |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
    double stun0All = 0, stun1All = 0;
    foreach (var b in fvRows)
    {
        for (int w = 0; w < fvStages.Count; w++)
        {
            double st0 = 0, st1 = 0, alive = 0, cast = 0;
            for (int seed = fvFrom; seed < fvFrom + 50; seed++)
            {
                var p0 = BattleEngine.Materialize(FvV0(b.F), BattleContext.PlayerTeam);
                int i0 = p0.FindIndex(u => u.Def.Id == "hiyo_ts");
                BattleResult r0 = BattleEngine.Run(p0, BattleEngine.Materialize(fvStages[w].Enemy, BattleContext.EnemyTeam), seed, verbose: true);
                st0 += r0.Events.Count(e => e.Kind == BattleEventKind.StatusSnapshot && e.TargetId == i0 && e.Text == "痺");

                var p1 = BattleEngine.Materialize(FvV1(b.F), BattleContext.PlayerTeam);
                int i1 = p1.FindIndex(u => u.Def.Id == "hiyo");
                BattleResult r1 = BattleEngine.Run(p1, BattleEngine.Materialize(fvStages[w].Enemy, BattleContext.EnemyTeam), seed, verbose: true);
                st1 += r1.Events.Count(e => e.Kind == BattleEventKind.StatusSnapshot && e.TargetId == i1 && e.Text == "痺");
                alive += r1.Events.Count(e => e.Kind == BattleEventKind.StatSnapshot && e.TargetId == i1);
                cast += r1.Events.Count(e => e.Kind == BattleEventKind.Skill && e.ActorId == i1);
            }
            Console.WriteLine($"| {(w == 0 ? b.Name : "")} | 第{w + 1}波 | {st0 / 50:0.00} | **{st1 / 50:0.00}** "
                            + $"| {alive / 50:0.00} | {cast / 50:0.00} | {(alive - cast) / 50:0.00} |");
            stun0All += st0 / 50; stun1All += st1 / 50;
            Console.Out.Flush();
        }
    }
    Console.WriteLine();
    Console.WriteLine($"**20 セルの合計で痺れは V0 {stun0All:0.00} 回/戦 → V1 {stun1All:0.00} 回/戦。**");
    Console.WriteLine("`差（死亡＋決着）` は移設に固有ではない——ヒヨが自分の手番の前に落ちたか、");
    Console.WriteLine("その手番が回る前に決着して `order` のループが `break` した回数。");
    Console.WriteLine();
    return;
}
}
