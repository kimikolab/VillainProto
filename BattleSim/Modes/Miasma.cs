using BattleCore;
using static Common;

// =====================================================================================
// miasma モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "miasma")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 miasma
// =====================================================================================

static class MiasmaDiag
{
public static void Phase0(string[] args, int stageIndex)
{
    var mpBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> mpStages = EnemyCatalog.Stages;
    const int MpSeeds = 200;   // compare / spread / turn / favor と同じ帯

    var mpRows = mpBuilds.Where(b => MsHas(b.F, UnitCatalog.Guza)).ToArray();

    Console.WriteLine("# 瘴気を手番へ降ろす前の地図（第61期 Phase 0）");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 miasma phase0` の出力。seed 0..{MpSeeds - 1}。");
    Console.WriteLine("**盤面は1つも動かしていない**（`UnitCatalog.Guza` は `Actions` を持たないので");
    Console.WriteLine("`ActsOnPattern` が偽 ＝ `MiasmaTrait.OnTurnStart` が従来どおり発火する）。");
    Console.WriteLine();

    // ---- 0-1. 窓口 -------------------------------------------------------------------
    Console.WriteLine("## 0-1. 瘴気の窓口（指示書 §1-1）");
    Console.WriteLine();
    Console.WriteLine($"- `MiasmaTrait.PerTurn` = **{MiasmaTrait.PerTurn}**（敵全体）／ "
                    + $"`MiasmaTrait.AllyLeak` = **{MiasmaTrait.AllyLeak}**（味方全体・**自分を含む**）");
    Console.WriteLine("- 発火口: `OnTurnStart`（現行）／`OnAction`（移設後）。分岐は `Trait.ActsOnPattern`");
    Console.WriteLine("- ターンの順序: `TickStatuses`（**毒 → 燃焼** の2ループ）→ `OnTurnStart` → 行動順ループ");
    Console.WriteLine("- **毒のカウンタは刻んでも減らない**（`TickStatuses` の毒ループは `ApplyDamage` を呼ぶだけ）");
    Console.WriteLine($"- 毒喰らい（ベニ）の倍率 `DevourTrait.AllyPoisonMultiplier` = **{DevourTrait.AllyPoisonMultiplier}**"
                    + "（**味方の毒の刻みにだけ掛かる**。`TickStatuses` の毒ループの中）");
    Console.WriteLine($"- 澱み喰い（ヴィオ）の `BlightfedTrait.GainPerStack` = **{BlightfedTrait.GainPerStack}**"
                    + "（`OnTurnStart`・味方の毒を**全部 0 にして**攻撃力へ。**自分の毒は吸わない**）");
    Console.WriteLine($"- 澱み（ミオ）の `AmplifierTrait.Step` = **{AmplifierTrait.Step}**"
                    + "——**既に第11期に `OnAction` へ降りている**（速8。グザ 速5 より速い）");
    Console.WriteLine();
    Console.WriteLine("`StatusKeys.Poison` を**読んで分岐する**駒側の窓口:");
    Console.WriteLine();
    var mpReaders = new (TraitId Id, string Where, string What)[]
    {
        (TraitId.Blightfed, "OnTurnStart",   "味方の毒を全部吸って攻撃力 +4/層（自分の毒は除く）"),
        (TraitId.Devour,    "OnTurnStart",   "毒に侵された敵の数 × 4 を味方全体に回復"),
        (TraitId.Amplifier, "OnAction",      "毒に侵された敵の毒を +4／傷を持ち毒を持たない敵には毒 1 を置く（第89期）"),
        (TraitId.Contagion, "OnAnyDeath",    "毒に侵された駒が倒れると残りの敵へ毒が飛ぶ"),
        (TraitId.Torment,   "OnAfterAttack", "毒／痺れを負った相手を責める"),
    };
    Console.WriteLine("| 特性 | フック | 何をするか | 8行での枚数 |");
    Console.WriteLine("|---|---|---|--:|");
    foreach (var r in mpReaders)
        Console.WriteLine($"| {r.Id} | `{r.Where}` | {r.What} | {mpRows.Sum(b => FvCountTrait(b.F, r.Id))} |");
    Console.WriteLine();
    Console.WriteLine("engine 側の窓口は `TickStatuses` の毒ループ1本（第50期の一覧のとおり）。");
    Console.WriteLine();

    // ---- 0-2. 発火順の地図 -------------------------------------------------------------
    Console.WriteLine("## 0-2. `OnTurnStart` の発火順（指示書 §1-2）——**これが本題**");
    Console.WriteLine();
    Console.WriteLine("engine は `ctx.AllUnits.Where(IsAlive)` の順に `OnTurnStart` を回す。");
    Console.WriteLine("`_units` は `Run` が `player` → `enemy` の順に `Add` した並びで、");
    Console.WriteLine("`Formation.Occupied()` はスロット昇順に返す ＝ **味方はスロット昇順**。");
    Console.WriteLine();
    Console.WriteLine("`ターン頭で発火する同席者` は 澱み喰い（ヴィオ）と 毒喰らい（ベニ）の2つ。");
    Console.WriteLine("**グザが先なら**その場で吸われる／その場で回復に化ける ＝ **味方は毒を1点も払わない**。");
    Console.WriteLine("**グザが後なら**毒は次の `TickStatuses` まで残って**味方が刻まれる**。");
    Console.WriteLine();
    Console.WriteLine("| 行 | グザの席 | ヴィオ | ベニ | 現行の前後 | 移設後 |");
    Console.WriteLine("|---|:-:|:-:|:-:|---|---|");
    foreach (var b in mpRows)
    {
        int gs = b.F.Occupied().First(o => ReferenceEquals(o.Def, UnitCatalog.Guza)).Slot;
        int vs = MsHas(b.F, UnitCatalog.Vio)
               ? b.F.Occupied().First(o => ReferenceEquals(o.Def, UnitCatalog.Vio)).Slot : -1;
        int bs = MsHas(b.F, UnitCatalog.Beni)
               ? b.F.Occupied().First(o => ReferenceEquals(o.Def, UnitCatalog.Beni)).Slot : -1;
        string Rel(int s, string who) => s < 0 ? "—" : gs < s ? $"**グザ → {who}**" : $"{who} → グザ";
        var parts = new[] { Rel(vs, "ヴィオ"), Rel(bs, "ベニ") }.Where(x => x != "—").ToArray();
        string now = parts.Length == 0 ? "（同席者なし）" : string.Join(" / ", parts);
        Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[gs]} "
                        + $"| {(vs < 0 ? "—" : FormationRules.SeatNames[vs])} "
                        + $"| {(bs < 0 ? "—" : FormationRules.SeatNames[bs])} "
                        + $"| {now} "
                        + $"| {(parts.Length == 0 ? "（変わらない）" : "**「同席者 → グザ」に潰れる**")} |");
    }
    Console.WriteLine();

    // ---- 0-3. 手番を止めうる経路 --------------------------------------------------------
    Console.WriteLine("## 0-3. 移設後にグザの手番を止めうる経路（指示書 §1-3）");
    Console.WriteLine();
    Console.WriteLine("行動順ループが手番を飛ばす経路は5本——**痺れ／まどろみ／`CanAct` 偽／死亡／決着の `break`**。");
    Console.WriteLine();
    Console.WriteLine($"- **まどろみ**: `ColossusRule.Default.Slumber` = **{ColossusRule.Default.Slumber}**"
                    + "（既定で無効。しかも巨躯の保持者にしか立たない）");
    Console.WriteLine("- **`CanAct` 偽**: グザの特性は `Miasma` 1つで `CanAct` を上書きしない ＝ **原理的に通らない**");
    Console.WriteLine("- **痺れ**: 味方へ痺れを書けるのは 縛め（`Bind`・味方を1体縛る）と、");
    Console.WriteLine("  敵側の 縛め／断罪（`Condemn`・**ターン外に動いた駒**を罰する）。");
    Console.WriteLine("  **グザはターン外に動かない**ので断罪は原理的に当たらない");
    Console.WriteLine("- **死亡 / 決着**: 速5 は遅いので、**自分の手番が回る前に落ちる／決着する**回数を実測する（下表）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 波 | 味方の縛め | 敵の縛め | 敵の断罪 | グザ寿命T | 決着T | 戦死率 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|");
    foreach (var b in mpRows)
    {
        for (int w = 0; w < mpStages.Count; w++)
        {
            double life = 0, turns = 0, dead = 0;
            for (int seed = 0; seed < MpSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(b.F, mpStages[w].Enemy, seed, verbose: false);
                turns += r.Turns;
                if (r.TallyByUnit.TryGetValue("guza", out UnitTally? t))
                {
                    life += t.LastActiveTurn;
                    if (t.Deaths > 0) dead++;
                }
            }
            Console.WriteLine($"| {(w == 0 ? b.Name : "")} | 第{w + 1}波 "
                            + $"| {FvCountTrait(b.F, TraitId.Bind)} "
                            + $"| {mpStages[w].Enemy.Occupied().Count(o => o.Def.Traits.Contains(TraitId.Bind))} "
                            + $"| {mpStages[w].Enemy.Occupied().Count(o => o.Def.Traits.Contains(TraitId.Condemn))} "
                            + $"| {life / MpSeeds:0.00} | {turns / MpSeeds:0.00} | {dead * 100.0 / MpSeeds:0.0}% |");
            Console.Out.Flush();
        }
    }
    Console.WriteLine();

    // ---- 0-4. グザの現在の出力 ----------------------------------------------------------
    Console.WriteLine("## 0-4. グザの現在の出力（指示書 §1-4）——**素体対照を置くかの判定**");
    Console.WriteLine();
    Console.WriteLine("**攻2。** `与ダメ/戦` が 10 を下回るなら移設で捨てる出力は無視できる（指示書 §1-4）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 振り/戦 | 与ダメ/戦 | 撒いた(敵) | 漏らした(味) | 味方の刻み額面 | 敵の刻み額面 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    double swAll = 0, dmgAll = 0; int cells = 0;
    foreach (var b in mpRows)
    {
        double sw = 0, dmg = 0, tf = 0, ta = 0, bp = 0, be = 0;
        for (int w = 0; w < mpStages.Count; w++)
            for (int seed = 0; seed < MpSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(b.F, mpStages[w].Enemy, seed, verbose: false);
                if (r.TallyByUnit.TryGetValue("guza", out UnitTally? t)) { sw += t.Attacks; dmg += t.DamageToEnemy; }
                tf += r.MiasmaToFoe; ta += r.MiasmaToAlly;
                bp += r.PoisonBitePlayer; be += r.PoisonBiteEnemy;
            }
        double n = MpSeeds * mpStages.Count;
        Console.WriteLine($"| {b.Name} | {sw / n:0.00} | **{dmg / n:0.0}** | {tf / n:0.0} | {ta / n:0.0} "
                        + $"| {bp / n:0.0} | {be / n:0.0} |");
        swAll += sw / n; dmgAll += dmg / n; cells++;
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine($"**8行平均: 振り {swAll / cells:0.00} 回/戦 ／ 与ダメ {dmgAll / cells:0.0}/戦。**");
    Console.WriteLine(dmgAll / cells < 10
        ? "→ **10/戦 を下回る。** 指示書 §1-4 により素体対照は省略してよいが、**費用が小さいので主表には置く**。"
        : "→ **10/戦 を上回る。** 素体対照（V3）を必ず置く。");
    Console.WriteLine();

    // ---- 0-5. 分母の検算 ----------------------------------------------------------------
    Console.WriteLine("## 0-5. 分母の検算（指示書 §1-5・§1-6・§1-7）");
    Console.WriteLine();
    var mpHiyo = mpBuilds.Where(b => MsHas(b.F, UnitCatalog.Hiyo)).ToArray();
    int overlap = mpBuilds.Count(b => MsHas(b.F, UnitCatalog.Guza) && MsHas(b.F, UnitCatalog.Hiyo));
    Console.WriteLine($"- **グザを含む行: {mpRows.Length}** / ヒヨを含む行: {mpHiyo.Length} / "
                    + $"**両者が同席する行: {overlap}**（指示書の前提の検算）");
    Console.WriteLine($"- `CompareBuilds()` の行数: **{mpBuilds.Length}** × {mpStages.Count} 波 = "
                    + $"**{mpBuilds.Length * mpStages.Count} セル**（うちグザを含まない "
                    + $"{mpBuilds.Length - mpRows.Length} 行 = **{(mpBuilds.Length - mpRows.Length) * mpStages.Count} セル**が受け入れ基準2）");
    var mpPrimary = Baseline.PrimaryRows.Where(n => mpBuilds.Any(b => b.Name == n && MsHas(b.F, UnitCatalog.Guza))).ToArray();
    var mpPrimaryHiyo = Baseline.PrimaryRows.Where(n => mpBuilds.Any(b => b.Name == n && MsHas(b.F, UnitCatalog.Hiyo))).ToArray();
    Console.WriteLine($"- **主判定 {Baseline.PrimaryRows.Length} 行のうちグザを含む行: {mpPrimary.Length}** "
                    + $"—— {(mpPrimary.Length == 0 ? "なし" : string.Join(" / ", mpPrimary))}");
    Console.WriteLine($"- 主判定 {Baseline.PrimaryRows.Length} 行のうちヒヨを含む行: **{mpPrimaryHiyo.Length}**"
                    + $"{(mpPrimaryHiyo.Length == 0 ? "（§4 の係数は歯止めの分母を動かさない）" : "")}");
    Console.WriteLine();

    var mpIdx = Baseline.PrimaryRows.Select(n => Array.FindIndex(mpBuilds, b => b.Name == n)).Where(i => i >= 0).ToArray();
    double mpFifth = 0;
    foreach (int i in mpIdx)
    {
        int win = 0;
        for (int seed = 0; seed < MpSeeds; seed++)
            if (BattleEngine.Run(mpBuilds[i].F, mpStages[^1].Enemy, seed, false).PlayerWon) win++;
        mpFifth += win * 100.0 / MpSeeds;
    }
    mpFifth /= mpIdx.Length;
    Console.WriteLine($"- **主判定 {mpIdx.Length} 行の第五波平均: {mpFifth:0.0}%** ／ "
                    + $"歯止め **{Baseline.PrimaryFifthFloor:0.0}%** ／ "
                    + $"**余裕 {mpFifth - Baseline.PrimaryFifthFloor:+0.0;-0.0;0.0}pt**");
    Console.WriteLine($"- 動きうるのは主判定のうち **{mpPrimary.Length} 行**なので、その行が第五波で 0% まで落ちても");
    Console.WriteLine($"  平均の低下は高々 **{mpPrimary.Length * 100.0 / mpIdx.Length:0.0}pt** —— 実測は §3 Q6 で出す。");
    Console.WriteLine();
    return;
}

public static void Run(string[] args, int stageIndex)
{
    var msBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> msStages = EnemyCatalog.Stages;
    string msMode = args.Length > 2 ? args[2] : "";
    bool msAlt = msMode == "alt";
    int msFrom = msAlt ? 200 : 0;              // alt は別 seed 帯（200..599）
    int msSeeds = msAlt ? 400 : 200;           // それ以外は compare と同じ帯

    var msRows = msBuilds.Where(b => MsHas(b.F, UnitCatalog.Guza)).ToArray();

    UnitDef msAct = MsActDef(), msCyc = MsCycleDef(), msPlain = MsPlainDef();
    Formation MsV0(Formation f) => f;                                  // 現行（ターン頭）＝ 盤面そのもの
    Formation MsV1(Formation f) => FvSwap(f, UnitCatalog.Guza, msAct);  // 移設
    Formation MsV2(Formation f) => FvSwap(f, UnitCatalog.Guza, msCyc);  // 周期
    Formation MsV3(Formation f) => FvSwap(f, UnitCatalog.Guza, msPlain);// 素体（手番）

    // グザの id は版で変わるので、駒側の集計は「グザの席にいる駒」で引く。
    var msIds = new[] { "guza", "guza_act", "guza_cyc", "guza_plain" };

    MsStat MsMeasure(Formation f, Formation enemy)
    {
        var mine = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        var z = new MsStat();
        for (int seed = msFrom; seed < msFrom + msSeeds; seed++)
        {
            BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Fires += r.MiasmaFires; z.ToFoe += r.MiasmaToFoe; z.ToAlly += r.MiasmaToAlly;
            z.BiteAlly += r.PoisonBitePlayer; z.BiteFoe += r.PoisonBiteEnemy;
            z.TicksAlly += r.PoisonTicksPlayer; z.TicksFoe += r.PoisonTicksEnemy;
            foreach (string id in msIds)
                if (r.TallyByUnit.TryGetValue(id, out UnitTally? t))
                { z.Swings += t.Attacks; z.Dmg += t.DamageToEnemy; z.Life += t.LastActiveTurn; }
            z.TeamDmg += r.TallyByUnit.Where(kv => mine.Contains(kv.Key)).Sum(kv => kv.Value.DamageToEnemy);
            z.TeamTaken += r.TallyByUnit.Where(kv => mine.Contains(kv.Key)).Sum(kv => kv.Value.DamageTaken);
            // ヴィオ／ベニの取り分。ヴィオの攻撃力は tally に無いので**与ダメで代理する**。
            if (r.TallyByUnit.TryGetValue("vio", out UnitTally? tv)) z.VioDmg += tv.DamageToEnemy;
            if (r.TallyByUnit.TryGetValue("beni", out UnitTally? tb)) z.BeniHeal += tb.Healed;
        }
        double n = msSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.Fires /= n; z.ToFoe /= n; z.ToAlly /= n;
        z.BiteAlly /= n; z.BiteFoe /= n; z.TicksAlly /= n; z.TicksFoe /= n;
        z.Swings /= n; z.Dmg /= n; z.Life /= n; z.TeamDmg /= n; z.TeamTaken /= n;
        z.VioDmg /= n; z.BeniHeal /= n;
        return z;
    }
    double[] MsWins(Formation f)
    {
        var v = new double[msStages.Count];
        for (int w = 0; w < msStages.Count; w++)
        {
            int win = 0;
            for (int seed = msFrom; seed < msFrom + msSeeds; seed++)
                if (BattleEngine.Run(f, msStages[w].Enemy, seed, false).PlayerWon) win++;
            v[w] = win * 100.0 / msSeeds;
        }
        return v;
    }
    // 情報セルは**2つの定義を併記する**（CLAUDE.md「情報セルの定義が2つある」）。
    // Info59 = 第59期 9-1（`0 < x < 100` を第2〜5波）。指示書 Q4 の「澱み喰いは現在 1」はこちら。
    static int MsInfo59(double[] v) => v.Skip(1).Count(x => x > 0.0 && x < 100.0);
    static int MsInfoMid(double[] v) => v.Count(x => x > 5.0 && x < 95.0);
    static string MsCells(double[] v) => string.Concat(v.Select(x => $"| {x:0.0}% "));

    // ---- ミオとの順序（第61期の追加測定）------------------------------------------------
    //
    // 主表で V1 の損が大きい上位4行（`追撃×毒` −37.8 / `澱み喰い` −37.1 / `毒+耐久` −18.1 /
    // `毒` −12.5）は、**澱み（ミオ・`Amplifier`）を含む4行とちょうど一致する。**
    // ミオは速8で**既に第11期に `OnAction` へ降りている**ので、グザ（速5）を手番へ降ろすと
    // **必ずミオより後**になる——第1ターンにミオが増幅する毒が盤上に1層も無くなる。
    //
    // **切り分けはミオを同数値・特性なしの素体に差し替えて測る**（`ScaleRule` が作れなかった
    // 「その効果だけを 0 にできるノブ」の代わり。第47期の作法）。`Actions` は残すので、
    // 差は**増幅そのもの**だけに閉じる。
    if (msMode == "mio")
    {
        var msMioPlain = new UnitDef
        {
            Id = "mio_plain", Name = "澱みのミオ（素体）", MaxHp = UnitCatalog.Mio.MaxHp,
            Attack = UnitCatalog.Mio.Attack, Speed = UnitCatalog.Mio.Speed,
            Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Mio.Pattern,
            Actions = new UnitAction[] { new(ActionKind.Skill, Label: "水を眺めている") }
        };
        Console.WriteLine("# 追加測定 —— V1 の損はミオとの順序か");
        Console.WriteLine();
        Console.WriteLine($"seed {msFrom}..{msFrom + msSeeds - 1}。**ミオを同数値・特性なしの素体に差し替えて測り直す**");
        Console.WriteLine("（`Actions` は残すので差は増幅そのものだけ）。");
        Console.WriteLine("**ミオを外すと V1−V0 の損が縮む**なら、損の出どころは");
        Console.WriteLine("「グザがミオより後になったこと」であって「手番へ降ろしたこと一般」ではない。");
        Console.WriteLine();
        Console.WriteLine("| 行 | ミオ | V0 | V1 | **V1−V0** | 敵の刻み V0 | V1 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var b in msRows)
        {
            if (!MsHas(b.F, UnitCatalog.Mio)) continue;
            foreach ((string tag, Formation baseF) in new[]
                     { ("そのまま", b.F), ("**素体**", FvSwap(b.F, UnitCatalog.Mio, msMioPlain)) })
            {
                double[] v0 = MsWins(MsV0(baseF));
                double[] v1 = MsWins(MsV1(baseF));
                double be0 = 0, be1 = 0;
                for (int w = 0; w < msStages.Count; w++)
                {
                    be0 += MsMeasure(MsV0(baseF), msStages[w].Enemy).BiteFoe / 5;
                    be1 += MsMeasure(MsV1(baseF), msStages[w].Enemy).BiteFoe / 5;
                }
                Console.WriteLine($"| {(tag == "そのまま" ? b.Name : "")} | {tag} | {v0.Average():0.0}% | {v1.Average():0.0}% "
                                + $"| **{v1.Average() - v0.Average():+0.0;-0.0;0.0}** | {be0:0.0} | {be1:0.0} |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        Console.WriteLine("参考——**ミオを含まない4行**（`毒+ベニ+ラウ` / `毒爆弾` / `毒→被弾強化` / `逸らし`）の");
        Console.WriteLine("V1−V0 は主表のとおり −2.7 / −7.2 / −1.7 / +2.1 で、**桁が違う。**");
        Console.WriteLine();
        return;
    }

    // ---- 席の入れ替え（Q1）------------------------------------------------------------
    if (msMode == "seat")
    {
        Console.WriteLine("# Q1 —— 順序の判断は消えたか（席の入れ替えの追試）");
        Console.WriteLine();
        Console.WriteLine($"seed {msFrom}..{msFrom + msSeeds - 1}。**駒は1枚も差し替えない**——");
        Console.WriteLine("同じ5枚のまま、グザと `OnTurnStart` の同席者の席だけを交換する。");
        Console.WriteLine();
        Console.WriteLine("**V0 では味方の毒の刻みが席で変わり、V1 では変わらない**なら、");
        Console.WriteLine("「席の左右が毒の代金の払い手を決めていた判断」が移設で消えたことの直接の証拠になる。");
        Console.WriteLine();
        var msPairs = new (string Row, UnitDef Other, string OtherName)[]
        {
            ("澱み喰い (グザ×ヴィオ)", UnitCatalog.Vio,  "ヴィオ"),
            ("毒爆弾 (ラウ×ヴィオ)",   UnitCatalog.Vio,  "ヴィオ"),
            ("毒+耐久 (ベニ×トウ)",    UnitCatalog.Beni, "ベニ"),
            ("毒+ベニ+ラウ",           UnitCatalog.Beni, "ベニ"),
        };
        Console.WriteLine("| 行 | 版 | 席 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 味方の刻み | 漏らした | 撒いた回 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var pr in msPairs)
        {
            var b = msBuilds.First(x => x.Name == pr.Row);
            foreach ((string tag, Func<Formation, Formation> ver) in new (string, Func<Formation, Formation>)[]
                     { ("V0 現行", MsV0), ("**V1 移設**", MsV1) })
            {
                foreach ((string seatTag, Formation baseF) in new[]
                         { ("そのまま", b.F), ($"グザ↔{pr.OtherName}", MsSwapSeats(b.F, UnitCatalog.Guza, pr.Other)) })
                {
                    Formation f = ver(baseF);
                    double[] v = MsWins(f);
                    double bite = 0, leak = 0, fires = 0;
                    for (int w = 0; w < msStages.Count; w++)
                    {
                        MsStat z = MsMeasure(f, msStages[w].Enemy);
                        bite += z.BiteAlly / msStages.Count;
                        leak += z.ToAlly / msStages.Count;
                        fires += z.Fires / msStages.Count;
                    }
                    Console.WriteLine($"| {(seatTag == "そのまま" && tag.StartsWith("V0") ? b.Name : "")} | {tag} | {seatTag} "
                                    + MsCells(v) + $"| **{v.Average():0.0}%** | **{bite:0.0}** | {leak:0.0} | {fires:0.00} |");
                    Console.Out.Flush();
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("`味方の刻み` は毒の刻みの**額面**（毒喰らいの倍率を掛けた後・肩代わり／破片／軛を通す前）。");
        Console.WriteLine("`漏らした` は瘴気が味方へ撒いた層の総量、`撒いた回` は瘴気の発火回数。");
        Console.WriteLine();
        return;
    }

    // ---- 主表 ---------------------------------------------------------------------------
    Console.WriteLine(msAlt ? "# 瘴気の移設の主表（別 seed 帯 200..599 の追試）" : "# 瘴気の移設の主表（第61期）");
    Console.WriteLine();
    Console.WriteLine($"seed {msFrom}..{msFrom + msSeeds - 1}。**V0（現行・`OnTurnStart`）は `UnitCatalog.Guza` そのもの**——");
    Console.WriteLine("**V0 の5波が `docs/balance.md` と一致することがこの診断の検算**。");
    Console.WriteLine("V1 = 移設（`[Skill]`）／V2 = 周期（`[Skill, Attack]`・供給が半分）／");
    Console.WriteLine("V3 = 移設 + 素体（同数値・特性なし。**V1 − V3 が機構の帰属**）。");
    Console.WriteLine("**V1 / V2 / V3 は診断のローカルの `UnitDef`。`UnitCatalog` は1バイトも触っていない。**");
    Console.WriteLine();

    Console.WriteLine("## 1. 勝率（8行 × 5波）");
    Console.WriteLine();
    Console.WriteLine("`情報セル` は2つの定義を併記する——`59期` は `0 < x < 100` を第2〜5波（指示書 Q4 の定義）、");
    Console.WriteLine("`中間帯` は `5 < x < 95` を全5波（`spread` §1 の定義）。**別の量なので混ぜない。**");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 情報 59期 | 中間帯 | 対 V0 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var msAttr = new List<(string Name, double A1, double A2, double A3, int I0, int I1, int I2, bool Vio, bool Beni)>();
    foreach (var b in msRows)
    {
        double[] v0 = MsWins(MsV0(b.F));
        double[] v1 = MsWins(MsV1(b.F));
        double[] v2 = MsWins(MsV2(b.F));
        double[] v3 = MsWins(MsV3(b.F));
        Console.WriteLine($"| {b.Name} | V0 現行 {MsCells(v0)}| **{v0.Average():0.0}%** | {MsInfo59(v0)} | {MsInfoMid(v0)} | — |");
        Console.WriteLine($"| | **V1 移設** {MsCells(v1)}| **{v1.Average():0.0}%** | **{MsInfo59(v1)}** | {MsInfoMid(v1)} "
                        + $"| **{v1.Average() - v0.Average():+0.0;-0.0;0.0}** |");
        Console.WriteLine($"| | **V2 周期** {MsCells(v2)}| **{v2.Average():0.0}%** | **{MsInfo59(v2)}** | {MsInfoMid(v2)} "
                        + $"| **{v2.Average() - v0.Average():+0.0;-0.0;0.0}** |");
        Console.WriteLine($"| | V3 素体（手番） {MsCells(v3)}| **{v3.Average():0.0}%** | {MsInfo59(v3)} | {MsInfoMid(v3)} "
                        + $"| （V1−V3 = {v1.Average() - v3.Average():+0.0;-0.0;0.0}） |");
        msAttr.Add((b.Name, v1.Average() - v0.Average(), v2.Average() - v0.Average(), v1.Average() - v3.Average(),
                    MsInfo59(v0), MsInfo59(v1), MsInfo59(v2),
                    MsHas(b.F, UnitCatalog.Vio), MsHas(b.F, UnitCatalog.Beni)));
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine("### Q2 / Q3 —— 帰属と符号");
    Console.WriteLine();
    Console.WriteLine("**閾値 |帰属| ≥ 1.5pt**（第57期。それ未満は 0 と読む）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 型 | V1−V0 | 判定 | V2−V0 | 判定 | V1−V3（機構） |");
    Console.WriteLine("|---|---|--:|:-:|--:|:-:|--:|");
    static string Sign(double x) => Math.Abs(x) < 1.5 ? "0" : x > 0 ? "**＋**" : "**−**";
    foreach (var a in msAttr)
        Console.WriteLine($"| {a.Name} | {(a.Vio ? "**ヴィオ行**" : a.Beni ? "**ベニ行**" : "—")} "
                        + $"| {a.A1:+0.0;-0.0;0.0} | {Sign(a.A1)} | {a.A2:+0.0;-0.0;0.0} | {Sign(a.A2)} | {a.A3:+0.0;-0.0;0.0} |");
    Console.WriteLine();
    Console.WriteLine($"**8行平均: V1−V0 {msAttr.Average(a => a.A1):+0.0;-0.0;0.0}pt ／ "
                    + $"V2−V0 {msAttr.Average(a => a.A2):+0.0;-0.0;0.0}pt ／ "
                    + $"V1−V3（機構）{msAttr.Average(a => a.A3):+0.0;-0.0;0.0}pt**");
    var vioRows = msAttr.Where(a => a.Vio).ToArray();
    var beniRows = msAttr.Where(a => a.Beni).ToArray();
    Console.WriteLine($"**ヴィオ行 {vioRows.Length} 行の V2−V0 平均: {vioRows.Average(a => a.A2):+0.0;-0.0;0.0}pt ／ "
                    + $"ベニ行 {beniRows.Length} 行: {beniRows.Average(a => a.A2):+0.0;-0.0;0.0}pt**");
    Console.WriteLine($"**Q4（情報セル 59期の合計）: V0 {msAttr.Sum(a => a.I0)} → V1 {msAttr.Sum(a => a.I1)} / V2 {msAttr.Sum(a => a.I2)}**");
    Console.WriteLine();

    // ---- 計数 ---------------------------------------------------------------------------
    Console.WriteLine("## 2. 計数（5波の平均・1戦あたり）");
    Console.WriteLine();
    Console.WriteLine("`撒いた回` は瘴気の発火回数、`敵へ` / `味方へ` は撒いた層の総量（味方側は撒いた本人を含む）。");
    Console.WriteLine("`味方の刻み` / `敵の刻み` は毒の刻みの**額面**（倍率を掛けた後・肩代わり／破片／軛を通す前）。");
    Console.WriteLine("`ヴィオ与ダメ` はヴィオの攻撃力の代理（`AtkBonus` は `UnitTally` に無い）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 撒いた回 | 敵へ | 味方へ | 味方の刻み | 敵の刻み | グザ振り | グザ与ダメ | グザ寿命T | 味方総与ダメ | 味方総被ダメ | 決着T | ヴィオ与ダメ |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    var msQ5 = new List<(string Name, double F0, double F1, double F2, double B0, double B1, double B2)>();
    foreach (var b in msRows)
    {
        var acc = new Dictionary<string, MsStat>();
        foreach ((string tag, Formation f) in new[] { ("V0 現行", MsV0(b.F)), ("V1 移設", MsV1(b.F)), ("V2 周期", MsV2(b.F)) })
        {
            var agg = new MsStat();
            for (int w = 0; w < msStages.Count; w++)
            {
                MsStat z = MsMeasure(f, msStages[w].Enemy);
                agg.Fires += z.Fires / 5; agg.ToFoe += z.ToFoe / 5; agg.ToAlly += z.ToAlly / 5;
                agg.BiteAlly += z.BiteAlly / 5; agg.BiteFoe += z.BiteFoe / 5;
                agg.Swings += z.Swings / 5; agg.Dmg += z.Dmg / 5; agg.Life += z.Life / 5;
                agg.TeamDmg += z.TeamDmg / 5; agg.TeamTaken += z.TeamTaken / 5;
                agg.Turns += z.Turns / 5; agg.VioDmg += z.VioDmg / 5; agg.BeniHeal += z.BeniHeal / 5;
            }
            acc[tag] = agg;
            Console.WriteLine($"| {(tag.StartsWith("V0") ? b.Name : "")} | {tag} | {agg.Fires:0.00} | {agg.ToFoe:0.0} | {agg.ToAlly:0.0} "
                            + $"| **{agg.BiteAlly:0.0}** | {agg.BiteFoe:0.0} | {agg.Swings:0.00} | {agg.Dmg:0.0} | {agg.Life:0.00} "
                            + $"| {agg.TeamDmg:0.0} | {agg.TeamTaken:0.0} | {agg.Turns:0.00} | {agg.VioDmg:0.0} |");
            Console.Out.Flush();
        }
        msQ5.Add((b.Name, acc["V0 現行"].Fires, acc["V1 移設"].Fires, acc["V2 周期"].Fires,
                          acc["V0 現行"].BiteAlly, acc["V1 移設"].BiteAlly, acc["V2 周期"].BiteAlly));
    }
    Console.WriteLine();

    // ---- Q5: 消えた発火 --------------------------------------------------------------------
    Console.WriteLine("## 3. Q5 —— 新しい交絡（消えた発火と、増えた味方の刻み）");
    Console.WriteLine();
    Console.WriteLine("**痺れは 0 枚（Phase 0-3）**——8行に縛めは1枚も無く、敵側の縛めも 0、");
    Console.WriteLine("断罪はグザがターン外に動かないので原理的に当たらない。");
    Console.WriteLine("したがって移設で消える発火は**「自分の手番が回る前に落ちた／決着した」だけ**である。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 撒いた回 V0 | V1 | 差 | V2 | V2/V1 | 味方の刻み V0 | V1 | 差 | V2 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var q in msQ5)
        Console.WriteLine($"| {q.Name} | {q.F0:0.00} | {q.F1:0.00} | **{q.F1 - q.F0:+0.00;-0.00;0.00}** | {q.F2:0.00} "
                        + $"| {(q.F1 > 0 ? q.F2 / q.F1 : 0):0.00} | {q.B0:0.0} | {q.B1:0.0} | **{q.B1 - q.B0:+0.0;-0.0;0.0}** | {q.B2:0.0} |");
    Console.WriteLine();
    Console.WriteLine($"**8行平均: 撒いた回 V0 {msQ5.Average(q => q.F0):0.00} → V1 {msQ5.Average(q => q.F1):0.00} "
                    + $"→ V2 {msQ5.Average(q => q.F2):0.00}**（V2/V1 = {(msQ5.Average(q => q.F1) > 0 ? msQ5.Average(q => q.F2) / msQ5.Average(q => q.F1) : 0):0.00}）");
    Console.WriteLine($"**味方の刻み V0 {msQ5.Average(q => q.B0):0.0} → V1 {msQ5.Average(q => q.B1):0.0} → V2 {msQ5.Average(q => q.B2):0.0}**");
    Console.WriteLine();

    // ---- Q6: 歯止め ----------------------------------------------------------------------
    Console.WriteLine("## 4. Q6 —— 歯止め（主判定19行の第五波）");
    Console.WriteLine();
    var msIdx = Baseline.PrimaryRows.Select(n => Array.FindIndex(msBuilds, b => b.Name == n)).Where(i => i >= 0).ToArray();
    Console.WriteLine("| 版 | 主判定19行の第五波平均 | 歯止め | 余裕 | 動いた行 |");
    Console.WriteLine("|---|--:|--:|--:|---|");
    foreach ((string tag, Func<Formation, Formation> ver) in new (string, Func<Formation, Formation>)[]
             { ("V0 現行", MsV0), ("**V1 移設**", MsV1), ("**V2 周期**", MsV2) })
    {
        double sum = 0; var moved = new List<string>();
        foreach (int i in msIdx)
        {
            Formation f = MsHas(msBuilds[i].F, UnitCatalog.Guza) ? ver(msBuilds[i].F) : msBuilds[i].F;
            int win = 0;
            for (int seed = msFrom; seed < msFrom + msSeeds; seed++)
                if (BattleEngine.Run(f, msStages[^1].Enemy, seed, false).PlayerWon) win++;
            double v = win * 100.0 / msSeeds;
            sum += v;
            if (MsHas(msBuilds[i].F, UnitCatalog.Guza)) moved.Add($"{msBuilds[i].Name} {v:0.0}%");
        }
        double avg = sum / msIdx.Length;
        Console.WriteLine($"| {tag} | **{avg:0.0}%** | {Baseline.PrimaryFifthFloor:0.0}% "
                        + $"| **{avg - Baseline.PrimaryFifthFloor:+0.0;-0.0;0.0}pt** | {string.Join(" / ", moved)} |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}
}
