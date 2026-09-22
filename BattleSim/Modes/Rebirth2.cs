using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// =====================================================================================
// rebirth2 モード（第180期） —— B群の転生 3〜5枚目：泥人形ムド／澱み喰いのヴィオ／鬨の号令ガン
//
// 指示書は design/PHASE180_REBIRTH_B2_SPEC.md ／ 報告は design/PHASE180_REBIRTH_B2.md。
//
// **数値の線は1本も置かない**（指示書 §0。第178期・第179期から引き継ぐ約束）。
// 回帰確認（3枚のどれも含まない行が動かないこと）と変化表・帳簿だけで、採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 rebirth2 phase0  # 前提を実装から引き直す
//     dotnet run --project BattleSim -c Release 0 rebirth2 run     # 1枚ずつの対照と3枚同時
//     dotnet run --project BattleSim -c Release 0 rebirth2 ledger  # 3枚の帳簿
//     dotnet run --project BattleSim -c Release 0 rebirth2 check   # 自己検査
//
// **3枚とも `Presets.Compare` に在席する**ので、スス（第179期）と違って
// **`compare` そのものが測定対象**になる（診断のローカル台は組まない）。
// 旧版の対照は「その駒だけ第179期の `Traits` に戻したローカルの `UnitDef`」で作る。
// =====================================================================================

static class Rebirth2Diag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": StageRun(); return;
            case "ledger": Ledger(); return;
            case "check": Check(); return;
            default:
                Console.WriteLine("rebirth2: モードは phase0 / run / ledger / check。");
                return;
        }
    }

    // =================================================================================
    // Phase 0 —— 前提を実装から引き直す
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第180期 `rebirth2 phase0` —— 前提を実装から引き直す");
        Console.WriteLine();
        if (!ParryScan.Init()) return;

        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));

        Q01();
        Q02();
        Q03(traits, engine);
        Q05(traits);
        Q06(traits);
        Q07();
        Q09(traits);
        Q10();
        Q08();
    }

    // ---- Q0-1: 3枚の在席行と、同席する行 ----------------------------------------
    static void Q01()
    {
        Console.WriteLine("## Q0-1. `Presets.Compare` 61 行での在席と、同席する行");
        Console.WriteLine();
        string[] ids = { "mudo", "vio", "gan" };
        var rows = new Dictionary<string, List<string>>();
        foreach (string id in ids) rows[id] = new List<string>();
        var both = new List<string>();

        foreach ((string name, Formation f) in CompareBuilds())
        {
            var here = ids.Where(id => f.Occupied().Any(o => o.Def.Id == id)).ToList();
            foreach (string id in here) rows[id].Add(name);
            if (here.Count >= 2) both.Add(name + "（" + string.Join(" × ", here) + "）");
        }

        Console.WriteLine("| 駒 | 在席枠 | 行 |");
        Console.WriteLine("|---|--:|---|");
        foreach (string id in ids)
            Console.WriteLine("| " + UnitCatalog.ById(id).Name + " | **" + rows[id].Count + "** | "
                              + string.Join(" ／ ", rows[id]) + " |");
        Console.WriteLine();
        Console.WriteLine("**2枚以上が同席する行: " + both.Count + " 行**"
                          + (both.Count == 0 ? "（＝3枚の切り分けは行の上で完全に分かれる）"
                                             : "——" + string.Join(" ／ ", both) + "。**この行は切り分けから外す。**"));
        Console.WriteLine();
        int none = CompareBuilds().Count(b => !b.F.Occupied().Any(o => ids.Contains(o.Def.Id)));
        Console.WriteLine("- **3枚のどれも含まない行: " + none + " / 61**"
                          + "（回帰確認の分母。ここは 0 件差分でなければならない）");
        Console.WriteLine();
    }

    // ---- Q0-2: ムドの被弾内訳 ----------------------------------------------------
    static void Q02()
    {
        Console.WriteLine("## Q0-2. ムドの被弾内訳（既存計数・第2〜5波の平均・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("`RageTrait.OnDamaged` は**式の分岐より手前**で数えるので、この内訳は版に依らない"
                          + "（`RageFiresNoSource` ＝ 出どころなし＝毒・燃焼の刻み ／ "
                          + "`FromAlly` ＝ 味方の刃（自傷を含む）／ `FromFoe` ＝ 敵の攻撃）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 刻み | 味方の刃 | 敵 | **1発と数える（味方＋敵）** | `Threshold=3` の暴発 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");

        double gDot = 0, gAlly = 0, gFoe = 0; int gN = 0, gRows = 0;
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!f.Occupied().Any(o => o.Def.Id == "mudo")) continue;
            double dot = 0, ally = 0, foe = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    dot += r.RageFiresNoSource; ally += r.RageFiresFromAlly; foe += r.RageFiresFromFoe;
                    n++;
                }
            double hits = (ally + foe) / n;
            Console.WriteLine("| " + name + " | " + (dot / n).ToString("F2") + " | " + (ally / n).ToString("F2")
                              + " | " + (foe / n).ToString("F2") + " | **" + hits.ToString("F2")
                              + "** | " + (hits / 3).ToString("F2") + " 回 |");
            gDot += dot; gAlly += ally; gFoe += foe; gN += n; gRows++;
        }
        Console.WriteLine("| **ムド在席 " + gRows + " 行の平均** | " + (gDot / gN).ToString("F2") + " | "
                          + (gAlly / gN).ToString("F2") + " | " + (gFoe / gN).ToString("F2") + " | **"
                          + ((gAlly + gFoe) / gN).ToString("F2") + "** | **"
                          + ((gAlly + gFoe) / gN / 3).ToString("F2") + " 回** |");
        Console.WriteLine();
        int mix = CompareBuilds().Count(b => b.F.Occupied().Any(o => o.Def.Id == "mudo")
                                          && b.F.Occupied().Any(o => o.Def.Id == "sekki"));
        Console.WriteLine("**注**: `RageFires*` は `BattleResult` 側の全駒の合計なので、"
                          + "もう1枚の憤怒の保持者（棘守りのセッキ）が同席すると混ざる"
                          + "——ムドとセッキが同席する行は **" + mix + " 行**。");
        Console.WriteLine();
    }

    // ---- Q0-3 / Q0-4: OnDamaged の中から攻撃を呼ぶ既存例と再入 -------------------
    static void Q03(string traits, string engine)
    {
        Console.WriteLine("## Q0-3 / Q0-4. `OnDamaged` の中から攻撃を呼ぶ既存例と、再入・死亡の止まり方");
        Console.WriteLine();
        string perform = "ctx.Perform" + "Attack(";
        string interrupt = "ctx.Inter" + "rupt(";
        string reaction = "ctx.Rea" + "ction(";

        var hits = new List<string>();
        string[] lines = traits.Replace("\r", "").Split('\n');
        string cls = "";
        for (int i = 0; i < lines.Length; i++)
        {
            Match m = Regex.Match(lines[i], @"class\s+(\w+Trait)\b");
            if (m.Success) cls = m.Groups[1].Value;
            if (lines[i].TrimStart().StartsWith("//")) continue;
            if (lines[i].Contains(perform) || lines[i].Contains(interrupt) || lines[i].Contains(reaction))
                hits.Add(cls + "  :" + (i + 1) + "  " + lines[i].Trim());
        }
        Console.WriteLine("`Traits.cs` で攻撃・割り込み・反撃の窓口を呼ぶ行（**" + hits.Count + " 本**）:");
        Console.WriteLine();
        foreach (string h in hits) Console.WriteLine("- `" + h + "`");
        Console.WriteLine();

        Console.WriteLine("| 問い | 答え |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 再入禁止フラグの置き場所 | **`BattleContext`**（`InInterrupt` "
                          + (engine.Contains("public bool InInterrupt") ? "あり" : "**無い**")
                          + "）。`Trait` は全戦闘で共有されるシングルトンで、"
                          + "`layout` は戦闘を並列実行するので `static` には置けない |");
        Console.WriteLine("| 無限再帰しない根拠 | `ctx.Interrupt` は**入れ子で即座に返る**"
                          + "（`InInterrupt` が真なら body を呼ばない）。暴発の連撃中に棘・祟りの跳ね返りで"
                          + "ムドがさらに被弾しても、カウンタは積むが暴発は起きない |");
        Console.WriteLine("| ループの止まり方 | `PerformAttack` は先頭で `actor.IsAlive` を見て黙って返る（"
                          + (engine.Contains("if (!actor.IsAlive) return;") ? "**あり**" : "**無い**")
                          + "）。**それでも1発ごとに `self.IsAlive` と敵陣の生存を見る**"
                          + "——空振りを回数ぶん呼ぶと、計数と台本に空の段が並ぶ |");
        Console.WriteLine();
    }

    // ---- Q0-5: 在庫の置き場所と会戦の持ち越し ------------------------------------
    static void Q05(string traits)
    {
        Console.WriteLine("## Q0-5. 在庫（ヴィオの「腹」・ムドの怒り）の置き場所と会戦の持ち越し");
        Console.WriteLine();
        Console.WriteLine("- `StatusKeys.All` の本数: **" + StatusKeys.All.Length + "**"
                          + "（会戦の境界が一律に 0 へ戻す一覧）");
        Console.WriteLine("- `UnitState.Counters` の**私有キー**（`StatusKeys.All` に無いもの）は"
                          + "**境界の掃除を通らない**ので、持ち越したくなければ `Trait.OnCarryOver` で捨てる"
                          + "（前例: `AshTrait.CycleKey`／`ShoveTrait.LastTurnKey`／`FixateTrait.MemoryKey`）");
        Console.WriteLine("- `OnCarryOver` を上書きしている札: **"
                          + Regex.Matches(traits, @"override void OnCarryOver").Count + " 本**");
        Console.WriteLine();
        Console.WriteLine("**判断**: 腹も怒りも**私有キー**（`StatusKeys` を1本も足さない）に置き、"
                          + "`OnCarryOver` で捨てる。状態異常のキーを足すと会戦の帳簿の添字が動く"
                          + "（第147期の混乱・第153期の預かりと同じ作法）。");
        Console.WriteLine();
    }

    // ---- Q0-6: 敵に付いた毒の読み手 ----------------------------------------------
    static void Q06(string traits)
    {
        Console.WriteLine("## Q0-6. **敵に付いた毒**を読む駒（吐き戻しの読み手が 0 でないこと）");
        Console.WriteLine();
        string key = "StatusKeys." + "Poison";
        string[] lines = traits.Replace("\r", "").Split('\n');
        string cls = "";
        Console.WriteLine("| クラス | 行 | 読んでいる式 |");
        Console.WriteLine("|---|--:|---|");
        int n = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            Match m = Regex.Match(lines[i], @"class\s+(\w+Trait)\b");
            if (m.Success) cls = m.Groups[1].Value;
            if (lines[i].TrimStart().StartsWith("//")) continue;
            if (!lines[i].Contains(key)) continue;
            string window = string.Join(" ", lines.Skip(Math.Max(0, i - 8)).Take(12));
            if (!window.Contains("Opponent(")) continue;
            Console.WriteLine("| `" + cls + "` | " + (i + 1) + " | `" + lines[i].Trim() + "` |");
            n++;
        }
        Console.WriteLine();
        Console.WriteLine("**敵陣の毒を読む箇所: " + n + " 件**（0 ならここで止める＝鎖が繋がっていない・R034）。");
        Console.WriteLine();
        foreach (TraitId t in new[] { TraitId.Devour, TraitId.Amplifier, TraitId.Contagion })
        {
            var who = UnitCatalog.All.Where(d => d.Traits.Contains(t)).Select(d => d.Name).ToList();
            Console.WriteLine("- `" + t + "` の保持者: " + (who.Count == 0 ? "—" : string.Join(" ／ ", who)));
        }
        Console.WriteLine();
    }

    // ---- Q0-7: SurrenderedTurn の判定 --------------------------------------------
    static void Q07()
    {
        Console.WriteLine("## Q0-7. `SurrenderedTurn` をガンの手番の時点で使うと誰を拾うか");
        Console.WriteLine();
        Console.WriteLine("`Trait.SurrenderedTurn(ctx, u)` は **その瞬間の `CanAct`** を問う"
                          + "——**「前のターンかどうか」はこの判定に入っていない。**");
        Console.WriteLine();
        Console.WriteLine("号令（`RallyTrait.OnTurnStart`）は **`IdleTurn == ctx.Turn - 1`** を"
                          + "**先に**見てから `SurrenderedTurn` を問う。実際の支払い条件は2つの積:");
        Console.WriteLine();
        Console.WriteLine("1. **前のターンに手番を落とした**（`IdleTurn` の記録）");
        Console.WriteLine("2. **いまも落とす型である**（`SurrendersTurn` が真の札で `CanAct` が偽）");
        Console.WriteLine();
        Console.WriteLine("**叩き起こしも同じ2つを問う**（指示書 §3-1）。ガンの手番は行動順ループの中なので、"
                          + "**そのターンに手番を差し出した速い味方**は `IdleTurn == ctx.Turn` になり"
                          + "**1 を満たさない＝拾わない**（号令の支払いと同じ前ターン基準）。");
        Console.WriteLine();
        Console.WriteLine("**`SurrendersTurn` が偽の札**（＝手番を差し出さない型。号令も叩き起こしも拾わない）:");
        Console.WriteLine();
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            if (TraitCatalog.Get(id).SurrendersTurn) continue;
            var who = UnitCatalog.All.Where(d => d.Traits.Contains(id)).Select(d => d.Name).ToList();
            Console.WriteLine("- `" + id + "`: " + (who.Count == 0 ? "（ロスターに保持者なし）" : string.Join(" ／ ", who)));
        }
        Console.WriteLine();
    }

    // ---- Q0-9: 攻撃力低下の窓口 ---------------------------------------------------
    static void Q09(string traits)
    {
        Console.WriteLine("## Q0-9. 攻撃力低下がどの窓口を通っているか（泥散りの合流先）");
        Console.WriteLine();
        string dull = "ctx.Du" + "ll(";
        string[] lines = traits.Replace("\r", "").Split('\n');
        string cls = "";
        Console.WriteLine("| クラス | 行 | 呼び出し |");
        Console.WriteLine("|---|--:|---|");
        foreach (int i in Enumerable.Range(0, lines.Length))
        {
            Match m = Regex.Match(lines[i], @"class\s+(\w+Trait)\b");
            if (m.Success) cls = m.Groups[1].Value;
            if (lines[i].TrimStart().StartsWith("//")) continue;
            if (!lines[i].Contains(dull)) continue;
            Console.WriteLine("| `" + cls + "` | " + (i + 1) + " | `" + lines[i].Trim() + "` |");
        }
        Console.WriteLine();
        Console.WriteLine("**`DullRoute` の本数: " + DullRoutes.Count + "**（"
                          + string.Join(" / ", DullRoutes.Names) + "）。");
        Console.WriteLine();
        Console.WriteLine("**ネル（呪詛）もハネ（突き返し）も `ctx.Dull` を通る**ので、"
                          + "泥散りも同じ窓口を通せば ウツ（逆しま）が読む `AtkBonus` の負の側へそのまま合流する"
                          + "（`PerverseTrait.ModifyAttack` / `ModifyHitCount` は `self.AtkBonus` を直接読む）。"
                          + "**`AtkBonus` を直に引かないこと**は第42期からの規約。");
        Console.WriteLine();
    }

    // ---- Q0-10: ムドとウツの同席 --------------------------------------------------
    static void Q10()
    {
        Console.WriteLine("## Q0-10. ムドとウツが**隣接して**同席する行");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | ムドの席 | ウツの席 | 隣接 |");
        Console.WriteLine("|---|---|---|---|:-:|");
        int adj = 0, same = 0;
        foreach ((string band, (string Name, Formation F)[] rows) in new[]
                 { ("compare", CompareBuilds()), ("交差帯", CrossBuilds()) })
            foreach ((string name, Formation f) in rows)
            {
                var m = f.Occupied().FirstOrDefault(o => o.Def.Id == "mudo");
                var u = f.Occupied().FirstOrDefault(o => o.Def.Id == "utsu");
                if (m.Def is null || u.Def is null) continue;
                same++;
                bool a = FormationRules.AreAdjacent(m.Slot, u.Slot);
                if (a) adj++;
                Console.WriteLine("| " + band + " | " + name + " | " + FormationRules.SeatNames[m.Slot]
                                  + " | " + FormationRules.SeatNames[u.Slot] + " | " + (a ? "**○**" : "×") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("**同席 " + same + " 行 ／ うち隣接 " + adj + " 行。**"
                          + (adj == 0 ? " **泥散りがウツへ届く行は盤面に1行も無い**"
                                        + "——交差帯に1行足す候補として報告に書く（この期の採否対象ではない）。" : ""));
        Console.WriteLine();
    }

    // ---- Q0-8: 過去の類似実験 -----------------------------------------------------
    static void Q08()
    {
        Console.WriteLine("## Q0-8. 類似機構の過去実験（`design/` の走査）");
        Console.WriteLine();
        string dir = Path.Combine(ParryScan.Root!, "design");
        if (!Directory.Exists(dir)) { Console.WriteLine("`design/` が引けない。"); return; }
        string[] words = { "RageMode", "回数で育つ", "暴発", "叩き起こし", "吐き戻し", "連撃" };
        Console.WriteLine("| 語 | 出てくる文書 |");
        Console.WriteLine("|---|---|");
        foreach (string w in words)
        {
            var files = Directory.GetFiles(dir, "*.md")
                .Where(p => File.ReadAllText(p).Contains(w))
                .Select(Path.GetFileName).ToList();
            Console.WriteLine("| `" + w + "` | " + (files.Count == 0 ? "—" : string.Join(" ／ ", files)) + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // 旧版の駒（第179期の姿）。**`UnitCatalog` は触らず、診断のローカルで作る**
    // （第60期の火選りの移設と同じ扱い。`Id` はそのままなので帳簿の引きも変わらない）。
    // =================================================================================

    static UnitDef Retrait(UnitDef d, params TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Pattern = d.Pattern, Advances = d.Advances, Actions = d.Actions,
        Traits = traits, PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor
    };

    /// <summary>第179期のムド（憤怒＋祟り）。</summary>
    static readonly UnitDef MudoOld = Retrait(UnitCatalog.Mudo, TraitId.Rage, TraitId.Hex);

    /// <summary>暴発は載せるが泥散りだけ外した版（指示書 §測定 2'）。</summary>
    static readonly UnitDef MudoNoSmear = Retrait(UnitCatalog.Mudo, TraitId.Erupt, TraitId.Hex);

    /// <summary>逆に泥散りだけ載せた版（代金の単独の効き）。</summary>
    static readonly UnitDef MudoSmearOnly = Retrait(UnitCatalog.Mudo, TraitId.Rage, TraitId.Smear, TraitId.Hex);

    /// <summary>第179期のヴィオ（澱み喰いだけ）。</summary>
    static readonly UnitDef VioOld = Retrait(UnitCatalog.Vio, TraitId.Blightfed);

    /// <summary>第179期のガン（号令だけ）。</summary>
    static readonly UnitDef GanOld = Retrait(UnitCatalog.Gan, TraitId.Rally);

    /// <summary>3枚とも第179期の姿に戻す（＝`docs/balance.md` を再現する版）。</summary>
    static Formation Old(Formation f) => Swap(Swap(Swap(f, "mudo", MudoOld), "vio", VioOld), "gan", GanOld);

    static Formation Swap(Formation f, string id, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == id ? to : d;
        return g;
    }

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    // =================================================================================
    // `run` —— 1枚ずつの対照と3枚同時
    // =================================================================================

    static void StageRun()
    {
        Console.WriteLine("# 第180期 `rebirth2 run` —— 1枚ずつの対照（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**旧** ＝ 3枚とも第179期の姿（`docs/balance.md` を再現する版）。"
                          + "**新** ＝ この期の既定。**どの行も3枚のうち1枚しか含まない**（Phase 0 Q0-1）ので、"
                          + "`ΔM + ΔV + ΔG` は定義上 `Δ全` と厳密に一致する。");
        Console.WriteLine();

        Console.WriteLine("## 表A. 動いた行（第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 主 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | Δ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");

        double sumOld = 0, sumNew = 0; int moved = 0, still = 0;
        foreach ((string name, Formation f) in CompareBuilds())
        {
            string who = Has(f, "mudo") ? "ムド" : Has(f, "vio") ? "ヴィオ" : Has(f, "gan") ? "ガン" : "";
            double[] a = Rates(Old(f)), b = Rates(f);
            bool diff = Enumerable.Range(0, 5).Any(i => Math.Abs(a[i] - b[i]) > 0.001);
            if (who.Length == 0) { if (diff) still++; continue; }
            moved++;
            sumOld += Mean25(a); sumNew += Mean25(b);
            Console.WriteLine("| " + name + " | " + who + " | 旧 | " + Cells(a) + " | " + Mean25(a).ToString("F1") + " | — |");
            Console.WriteLine("| | | **新** | " + Cells(b) + " | " + Mean25(b).ToString("F1") + " | **"
                              + (Mean25(b) - Mean25(a)).ToString("+0.0;-0.0;0.0") + "** |");
        }
        Console.WriteLine();
        Console.WriteLine("- 3枚のどれかを含む行: **" + moved + " 行**（旧の平均 " + (sumOld / moved).ToString("F1")
                          + "% → 新 " + (sumNew / moved).ToString("F1") + "%）");
        Console.WriteLine("- **3枚のどれも含まないのに動いた行: " + still + " 行**（0 が正）");
        Console.WriteLine();

        Console.WriteLine("## 表B. ムドの2変数（暴発／泥散り）を1つずつ切る");
        Console.WriteLine();
        Console.WriteLine("**旧** 憤怒＋祟り ／ **暴発のみ** 暴発＋祟り（泥散りを外した `yP`）／"
                          + "**泥散りのみ** 憤怒＋泥散り＋祟り ／ **新** 暴発＋泥散り＋祟り。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 旧 | 暴発のみ | 泥散りのみ | 新 | 暴発の寄与 | 泥散りの寄与 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "mudo")) continue;
            double o = Avg25(Swap(f, "mudo", MudoOld));
            double e = Avg25(Swap(f, "mudo", MudoNoSmear));
            double sm = Avg25(Swap(f, "mudo", MudoSmearOnly));
            double n = Avg25(f);
            Console.WriteLine("| " + name + " | " + o.ToString("F1") + " | " + e.ToString("F1")
                              + " | " + sm.ToString("F1") + " | " + n.ToString("F1")
                              + " | " + (e - o).ToString("+0.0;-0.0;0.0")
                              + " | " + (sm - o).ToString("+0.0;-0.0;0.0") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**「暴発の寄与」＝ 暴発のみ − 旧 ／ 「泥散りの寄与」＝ 泥散りのみ − 旧。**"
                          + "2つの和が `新 − 旧` と離れていれば、それは**2つが干渉している**ということ。");
        Console.WriteLine();

        Console.WriteLine("## 表B'. 泥散りの読み手（ウツ）を隣に置いた台");
        Console.WriteLine();
        Console.WriteLine("**盤面には ムドとウツが隣接する行が 1 行も無い**（Phase 0 Q0-10）ので、"
                          + "**`Presets` は1行も触らずに診断のローカルで組む**"
                          + "——`逆しま (ネル×ウツ)` の 前3（ガルド）を ムドに差し替えただけ"
                          + "（ムド 前3 は 中央・後3＝ウツ と隣接する）。**この期の採否対象ではない。**");
        Console.WriteLine();
        {
            var baseRow = CompareBuilds().First(b => b.Name == "逆しま (ネル×ウツ)").F;
            var stand = new Formation();
            foreach ((int slot, UnitDef d) in baseRow.Occupied())
                stand[slot] = slot == 1 ? UnitCatalog.Mudo : d;
            Console.WriteLine("| 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | Δ | ウツが受けた弱体 | ウツの与害 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
            double refAvg = 0;
            foreach ((string tag, UnitDef d) in new[]
                     { ("旧（憤怒）", MudoOld), ("暴発のみ", MudoNoSmear),
                       ("泥散りのみ", MudoSmearOnly), ("**新**", UnitCatalog.Mudo) })
            {
                Formation g = Swap(stand, "mudo", d);
                double[] w = Rates(g);
                double dull = 0, dmg = 0; int n = 0;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(g, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        n++;
                        if (!r.TallyByUnit.TryGetValue("utsu", out UnitTally? t)) continue;
                        dull += t.CarryAmount?[UnitTally.CarryDull] ?? 0; dmg += t.DamageToEnemy;
                    }
                if (tag.StartsWith("旧")) refAvg = Mean25(w);
                Console.WriteLine("| " + tag + " | " + Cells(w) + " | " + Mean25(w).ToString("F1")
                                  + " | " + (tag.StartsWith("旧") ? "—" : (Mean25(w) - refAvg).ToString("+0.0;-0.0;0.0"))
                                  + " | " + (dull / n).ToString("F1") + " | " + (dmg / n).ToString("F1") + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("**`ウツが受けた弱体`** は `UnitTally.CarryAmount[CarryDull]`（窓口を通って届いた総量）。"
                          + "ウツは弱体化されるほど強くなるので、**負の側へ深く行くほど 1発が重く・発数が多くなる**"
                          + "（`PerverseTrait.AttackPerDull` / `HitsPerDull`）。");
        Console.WriteLine();

        Console.WriteLine("## 表C. 交差帯 12 行");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | Δ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        int cmoved = 0, cstill = 0;
        foreach ((string name, Formation f) in CrossBuilds())
        {
            double[] a = Rates(Old(f)), b = Rates(f);
            bool diff = Enumerable.Range(0, 5).Any(i => Math.Abs(a[i] - b[i]) > 0.001);
            bool here = Has(f, "mudo") || Has(f, "vio") || Has(f, "gan");
            if (!here) { if (diff) cstill++; continue; }
            cmoved++;
            Console.WriteLine("| " + name + " | 旧 | " + Cells(a) + " | " + Mean25(a).ToString("F1") + " | — |");
            Console.WriteLine("| | **新** | " + Cells(b) + " | " + Mean25(b).ToString("F1") + " | **"
                              + (Mean25(b) - Mean25(a)).ToString("+0.0;-0.0;0.0") + "** |");
        }
        Console.WriteLine();
        Console.WriteLine("- 交差帯で3枚のどれかを含む行: **" + cmoved + " 行** ／ "
                          + "**含まないのに動いた行: " + cstill + " 行**（0 が正）");
        Console.WriteLine();
    }

    // =================================================================================
    // `ledger` —— 3枚の帳簿
    // =================================================================================

    static void Ledger()
    {
        Console.WriteLine("# 第180期 `rebirth2 ledger` —— 3枚の帳簿（第2〜5波の平均・1戦あたり）");
        Console.WriteLine();

        Console.WriteLine("## 表D. 暴発と泥散り（ムド 8 行）");
        Console.WriteLine();
        Console.WriteLine("**`燃料`** は数えた被弾（刻みと自傷を除く）、**`見送り`** は"
                          + "閾値に届いたが割り込みの入れ子だったので発火しなかった回数。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 燃料 | うち味方の刃 | 暴発 | 発数 | 最大連撃 | 見送り | 泥 | 弾かれ | ムドの与害 | ムドの生存T | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "mudo")) continue;
            double fuel = 0, fromAlly = 0, fires = 0, swings = 0, peak = 0, held = 0;
            double smear = 0, blocked = 0, dmg = 0, life = 0, win = 0; int n = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    n++;
                    if (r.PlayerWon) win++;
                    if (!r.TallyByUnit.TryGetValue("mudo", out UnitTally? t)) continue;
                    fuel += t.EruptFuel; fromAlly += t.EruptFuelFromAlly; fires += t.EruptFires;
                    swings += t.EruptSwings; peak += t.EruptPeak; held += t.EruptHeld;
                    smear += t.SmearDealt; blocked += t.SmearBlocked;
                    dmg += t.DamageToEnemy; life += t.LastActiveTurn;
                }
            Console.WriteLine("| " + name + " | " + (fuel / n).ToString("F2") + " | " + (fromAlly / n).ToString("F2")
                              + " | " + (fires / n).ToString("F2") + " | " + (swings / n).ToString("F2")
                              + " | " + (peak / n).ToString("F2") + " | " + (held / n).ToString("F2")
                              + " | " + (smear / n).ToString("F2") + " | " + (blocked / n).ToString("F2")
                              + " | " + (dmg / n).ToString("F1") + " | " + (life / n).ToString("F2")
                              + " | " + (100.0 * win / n).ToString("F1") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表D'. 暴発は波で止まるか（波別の暴発回数／戦）");
        Console.WriteLine();
        Console.WriteLine("**暴発はターン外の行動なので `CanActOutOfTurn` を通る**"
                          + "——粛（第二波の中央）が保持者の生きているあいだ全員に対して閉じる。"
                          + "**憤怒は受動の強化なので粛に触れなかった。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第2波（粛） | 第3波（渇き） | 第4波（軛） | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "mudo")) continue;
            var cells = new List<string>();
            for (int st = 1; st < 5; st++)
            {
                double fires = 0; int n = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    n++;
                    if (r.TallyByUnit.TryGetValue("mudo", out UnitTally? t)) fires += t.EruptFires;
                }
                cells.Add((fires / n).ToString("F2"));
            }
            Console.WriteLine("| " + name + " | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表E. 泥散りの受け手（誰の腕が鈍るか）");
        Console.WriteLine();
        Console.WriteLine("**受け手は席で決まる**（ムドに隣接する味方。支援拒否＝ガルドは弾く）ので、"
                          + "ここは盤面ではなく編成から引く。**量は1回の被弾につき 1 で、上の表の `泥` が総量。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | ムドの席 | 隣の味方 | 素の攻 | 支援拒否 |");
        Console.WriteLine("|---|---|---|--:|:-:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            var m = f.Occupied().FirstOrDefault(o => o.Def.Id == "mudo");
            if (m.Def is null) continue;
            foreach ((int slot, UnitDef d) in f.Occupied())
            {
                if (slot == m.Slot || !FormationRules.AreAdjacent(m.Slot, slot)) continue;
                bool stoic = d.Traits.Contains(TraitId.Stoic);
                Console.WriteLine("| " + name + " | " + FormationRules.SeatNames[m.Slot] + " | " + d.Name
                                  + " | " + d.Attack + " | " + (stoic ? "**○（弾く）**" : "×") + " |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 表F. 吐き戻し（ヴィオ 2 行）");
        Console.WriteLine();
        Console.WriteLine("**`腹へ`** は吸い上げて記帳した層（**版に依らない分母**）、**`吐いた`** はそのうち敵へ移した層。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 腹へ | 吐いた | 吐いた回数 | 敵に積まれた毒 | 敵が毒で失った HP | ミオ／ラウ／ベニ | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "vio")) continue;
            foreach ((string tag, Formation g) in new[] { ("旧", Swap(f, "vio", VioOld)), ("**新**", f) })
            {
                double store = 0, moved = 0, fires = 0, foePoison = 0, win = 0; int n = 0;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(g, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        n++;
                        if (r.PlayerWon) win++;
                        if (r.TallyByUnit.TryGetValue("vio", out UnitTally? t))
                        { store += t.SpitStored; moved += t.SpitMoved; fires += t.SpitFires; }
                        foePoison += r.PoisonTicksEnemy;
                    }
                string readers = string.Join(" ／ ", new[] { "mio", "rau", "beni" }
                    .Where(id => Has(g, id)).Select(id => UnitCatalog.ById(id).Name));
                Console.WriteLine("| " + name + " | " + tag + " | " + (store / n).ToString("F2")
                                  + " | " + (moved / n).ToString("F2") + " | " + (fires / n).ToString("F2")
                                  + " | — | " + (foePoison / n).ToString("F1")
                                  + " | " + (readers.Length == 0 ? "—" : readers)
                                  + " | " + (100.0 * win / n).ToString("F1") + " |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 表G. 叩き起こし（ガン 9 行）");
        Console.WriteLine();
        Console.WriteLine("**`空振り`** は「ガンが殴ったが、起こす相手が1体もいなかった」回数。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 起こした | 空振り | 起こされた駒 | 号令の溜め（量/戦） | 勝率（旧→新） |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "gan")) continue;
            double fires = 0, miss = 0, rallyTurn = 0, win = 0, winOld = 0; int n = 0;
            var woke = new Dictionary<string, double>();
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    BattleResult o = BattleEngine.Run(Swap(f, "gan", GanOld), EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    n++;
                    if (r.PlayerWon) win++;
                    if (o.PlayerWon) winOld++;
                    rallyTurn += r.WhetByRoute[(int)WhetRoute.RallyTurn];
                    if (r.TallyByUnit.TryGetValue("gan", out UnitTally? t)) { fires += t.ReveilleFires; miss += t.ReveilleMisses; }
                    foreach ((int _, UnitDef d) in f.Occupied())
                        if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? u) && u.ReveilleWoken > 0)
                        {
                            woke.TryGetValue(d.Id, out double v);
                            woke[d.Id] = v + u.ReveilleWoken;
                        }
                }
            string who = woke.Count == 0 ? "—" : string.Join(" ／ ", woke.OrderByDescending(kv => kv.Value)
                .Select(kv => UnitCatalog.ById(kv.Key).Name + " " + (kv.Value / n).ToString("F2")));
            Console.WriteLine("| " + name + " | " + (fires / n).ToString("F2") + " | " + (miss / n).ToString("F2")
                              + " | " + who + " | " + (rallyTurn / n).ToString("F2")
                              + " | " + (100.0 * winOld / n).ToString("F1") + " → "
                              + (100.0 * win / n).ToString("F1") + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // 自己検査
    // =================================================================================

    static void Check()
    {
        Console.WriteLine("# 第180期 `rebirth2 check` —— 自己検査");
        Console.WriteLine();

        // (a) 3枚を第179期の姿へ戻すと、61 行 × 5 波が採用前と一致する
        int cells = 0;
        foreach ((string _, Formation f) in CompareBuilds())
        {
            double[] a = Rates(Old(f));
            // 旧版どうしの比較なので、ここでは「旧に戻した版が自分自身と一致する」ことしか言えない。
            // 採用前の `docs/balance.md` との突き合わせは報告書の側で行う（値をそのまま出す）。
            _ = a;
        }
        Console.WriteLine("- (a) 旧版へ戻した 61 行の表は `run` の「旧」列がそのまま出す"
                          + "（採用前の `docs/balance.md` との突き合わせは報告書の側で行う）");

        // (b) 3枚のどれも含まない行は、旧と新で1セルも動かない
        cells = 0;
        int rows = 0;
        foreach ((string _, Formation f) in CompareBuilds())
        {
            if (Has(f, "mudo") || Has(f, "vio") || Has(f, "gan")) continue;
            rows++;
            double[] a = Rates(Old(f)), b = Rates(f);
            for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - b[i]) > 0.001) cells++;
        }
        Console.WriteLine("- (b) 3枚のどれも含まない " + rows + " 行で動いたセル: **" + cells + " / "
                          + rows * 5 + "**（0 が正）");

        // (c) 暴発は入れ子で走らない（見送りが計上され、発数 ≦ 燃料）
        {
            long swings = 0, fuel = 0, held = 0, fires = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                if (!Has(f, "mudo")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        if (!r.TallyByUnit.TryGetValue("mudo", out UnitTally? t)) continue;
                        swings += t.EruptSwings; fuel += t.EruptFuel; held += t.EruptHeld; fires += t.EruptFires;
                    }
            }
            Console.WriteLine("- (c) 暴発の収支: 燃料 " + fuel + " ／ 発火 " + fires + " ／ 発数 " + swings
                              + " ／ 入れ子の見送り " + held
                              + "（**発数 ≦ 燃料** " + (swings <= fuel ? "○" : "**×**") + "）");
        }

        // (d) 泥散りは支援拒否へ届かない
        {
            long dealt = 0, blocked = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                if (!Has(f, "mudo")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        if (!r.TallyByUnit.TryGetValue("mudo", out UnitTally? t)) continue;
                        dealt += t.SmearDealt; blocked += t.SmearBlocked;
                    }
            }
            Console.WriteLine("- (d) 泥散り: 撒いた " + dealt + " ／ 支援拒否に弾かれた " + blocked
                              + "（ガルド同席の行があるので **弾かれ > 0** が正）");
        }

        // (e) カドは叩き起こされない
        {
            long kado = 0, all = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                if (!Has(f, "gan")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        foreach ((int _, UnitDef d) in f.Occupied())
                            if (r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t))
                            {
                                all += t.ReveilleWoken;
                                if (d.Id == "kado") kado += t.ReveilleWoken;
                            }
                    }
            }
            Console.WriteLine("- (e) 叩き起こされた延べ回数 " + all + " ／ うち棘鎧のカド **" + kado + "**（0 が正）");
        }

        // (f) 吐き戻しは味方に毒を書かない／腹は吐いた分だけ減る
        {
            long store = 0, moved = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                if (!Has(f, "vio")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        if (!r.TallyByUnit.TryGetValue("vio", out UnitTally? t)) continue;
                        store += t.SpitStored; moved += t.SpitMoved;
                    }
            }
            Console.WriteLine("- (f) 吐き戻しの収支: 腹へ " + store + " ／ 吐いた " + moved
                              + "（**吐いた ≦ 腹へ** " + (moved <= store ? "○" : "**×**") + "）");
        }

        // (g) 新しく PickOne を使っていない
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string pick = "ctx.Pick" + "One(";
            int total = (traits.Length - traits.Replace(pick, "").Length) / pick.Length;
            int mine = 0;
            foreach (string cls in new[] { "Erupt" + "Trait", "Smear" + "Trait", "Spit" + "Trait", "Reveille" + "Trait" })
            {
                int i = traits.IndexOf("class " + cls);
                if (i < 0) continue;
                int j = traits.IndexOf("\npublic ", i + 10);
                string body = traits.Substring(i, (j < 0 ? traits.Length : j) - i);
                mine += (body.Length - body.Replace(pick, "").Length) / pick.Length;
            }
            Console.WriteLine("- (g) 第180期の4枚が `" + pick + "` を呼ぶ回数: **" + mine
                              + "**（0 が正）／ `Traits.cs` 全体の呼び出しは " + total + " 件");
        }

        // (h) 状態キーとノブ
        Console.WriteLine("- (h) `StatusKeys.All` の本数: **" + StatusKeys.All.Length + "**（第179期から動かない）"
                          + " ／ `DullRoute` の本数: **" + DullRoutes.Count + "**（泥散りで 10 → 11）"
                          + " ／ `OutOfTurnRoute` の本数: **" + OutOfTurnRoutes.Count + "**（暴発・叩き起こしで 6 → 8）");
        Console.WriteLine();
    }

    // =================================================================================
    // ヘルパ
    // =================================================================================

    static double[] Rates(Formation f, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static double Avg25(Formation f) => Mean25(Rates(f));
    static string Cells(double[] w) => string.Join(" | ", Enumerable.Range(1, 4).Select(i => w[i].ToString("F1")));
}
