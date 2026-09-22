using BattleCore;
using static Common;

// =====================================================================================
// rebirth モード（第178期） —— B群の転生 1枚目・2枚目（熾のホタ／逆しまのウツ）
//
// 指示書は design/PHASE178_REBIRTH_B1_SPEC.md ／ 報告は design/PHASE178_REBIRTH_B1.md。
//
//     dotnet run --project BattleSim -c Release 0 rebirth phase0   # Q0-1〜Q0-5（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 rebirth hota     # §1-1 の数え物（V0 対 V1）
//     dotnet run --project BattleSim -c Release 0 rebirth utsu     # ウツの帳簿（手数・出力・寿命）
//     dotnet run --project BattleSim -c Release 0 rebirth rows     # 動いた行の全表（自己検査 (b)）
//     dotnet run --project BattleSim -c Release 0 rebirth check    # 自己検査
//
// **`rows` / `check` の比較先は既定で `docs/balance.md`。**
// 第178期に `docs/` を作り直したので、**回し直すときは第177期の版を引数で渡すこと**
// （`git show <第177期の sha>:docs/balance.md > tmp` → `0 rebirth check tmp`）。
// 引数なしだと新しい版どうしを比べてしまい、(a) が自明に通って (a') が落ちる。
//
// **数値の線は置かない**（指示書 §0）。回帰確認だけで、採否はポンが遊んで決める。
//
// **V0 の作り方が2枚で違う。**
//   ホタ ＝ `EmberRule.Charred`（`Fireproof = false`）で第177期の盤面にそのまま戻せる
//   ウツ ＝ ノブを置いていないので、V0 は `docs/balance.md`（第177期の committed な値）で読む
// =====================================================================================

static class RebirthDiag
{
    const int Seeds = 200;

    static readonly string HotaId = UnitCatalog.Hota.Id;
    static readonly string UtsuId = UnitCatalog.Utsu.Id;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "hota": HotaCensus(); return;
            case "utsu": UtsuLedger(); return;
            case "rows": Rows(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("rebirth: モードは phase0 / hota / utsu / rows / check。");
                return;
        }
    }

    // =================================================================================
    // phase0 —— 実装から引き直す（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        string eng = ReadSrc("BattleCore/BattleEngine.cs");
        string tra = ReadSrc("BattleCore/Traits.cs");
        string main = ReadSrc("DemoApp/Main.cs");

        Console.WriteLine("# 第178期 `rebirth phase0` —— 実装から引き直す（戦闘0回）");
        Console.WriteLine();

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1. 燃焼の刻みはどこで `ApplyDamage` を呼ぶか");
        Console.WriteLine();
        var burnCalls = Lines(eng, "ApplyDamage(u, BurnRules.Damage");
        Console.WriteLine("| 呼び出し | 行 |");
        Console.WriteLine("|---|--:|");
        foreach (var (n, text) in burnCalls) Console.WriteLine("| `" + text.Trim() + "` | " + n + " |");
        Console.WriteLine();
        Console.WriteLine(burnCalls.Count == 1
            ? "**1箇所だけ**（`TickStatuses` の燃焼ループ）。ホタ本人だけ 0 にする最小の場所は"
              + "**その呼び出しの手前で `continue` する**ことである——`ApplyDamage` の中で 0 にすると"
              + "「浴びた量」を読む札（被弾強化・砕け・分かち）が 0 で発火し、帳簿にも段が残る。"
            : "**" + burnCalls.Count + " 箇所ある。1箇所で切れないので設計をやり直すこと。**");
        Console.WriteLine();
        Console.WriteLine("- 第178期の枝（`Ember.Fireproof && u.HasTrait(TraitId.Pyre)`）: **"
                          + (eng.Contains("Ember.Fireproof && u.HasTrait(TraitId.Pyre)") ? "あり" : "なし") + "**");
        Console.WriteLine("- `TraitId.Pyre` の保持者（`UnitCatalog.All` 52枚）: **"
                          + string.Join("・", UnitCatalog.All.Where(d => d.Traits.Contains(TraitId.Pyre)).Select(d => d.Name))
                          + "** ／ 敵側（`EnemyCatalog.Stages` 5波）: **"
                          + EnemyCatalog.Stages.Sum(st => st.Enemy.Occupied().Count(o => o.Def.Traits.Contains(TraitId.Pyre)))
                          + " 体**");
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2. ウツの `N` の仮置き（旧 3 倍と総ダメージが近い値）");
        Console.WriteLine();
        int baseAtk = UnitCatalog.Utsu.Attack;
        Console.WriteLine("素の攻撃力 ＝ **" + baseAtk + "**。下げ幅 d のとき、");
        Console.WriteLine("旧版（第177期まで）の1手番の出力は `攻 + 3d`。");
        Console.WriteLine("**両取りの新版は `(攻 + " + PerverseTrait.AttackPerDull + "d) × 発数`**"
                          + "（発数 ＝ `min(" + PerverseTrait.MaxHits + ", 1 + d/"
                          + PerverseTrait.HitsPerDull + ")`）。");
        Console.WriteLine("**倍率を 3 ではなく " + PerverseTrait.AttackPerDull + " にしてある**"
                          + "——両方 3 にすると `(9 + 3d) × (1 + d/3)` ＝ 旧版の二乗になる。");
        Console.WriteLine();
        Console.WriteLine("| 下げ幅 d | 旧 攻 ＝ 旧 出力 | 新 攻 | 新 発数 | **新 出力** | 旧比 | 軛(25)を跨ぐか |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|:-:|");
        foreach (int d in new[] { 0, 2, 3, 6, 9, 12, 15, 16, 17, 20, 45 })
        {
            int oldOut = baseAtk + 3 * d;
            int atk = baseAtk + d * PerverseTrait.AttackPerDull;
            int h = Math.Min(PerverseTrait.MaxHits, 1 + d / PerverseTrait.HitsPerDull);
            Console.WriteLine("| " + d + " | " + oldOut + " | " + atk + " | " + h + " | **" + atk * h + "** | "
                              + ((double)atk * h / oldOut).ToString("F2") + " | "
                              + (atk > YokeTrait.Cap ? "**跨ぐ**" : "—") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**N = " + PerverseTrait.HitsPerDull + " / 上限 " + PerverseTrait.MaxHits
                          + " 発 / 攻撃力は下げ幅の " + PerverseTrait.AttackPerDull + " 倍。**"
                          + "**出力は全域で旧版以上**になるが、**下げ幅が 16 を超えると1発が軛の上限 "
                          + YokeTrait.Cap + " を跨ぐ**ので、第四波ではそこから先が切られ始める。");
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3. 1手番に複数回振っても、手番そのものの判定は1回のままか");
        Console.WriteLine();
        Console.WriteLine("| 判定 | 引けたか | `BattleEngine.cs` の行 |");
        Console.WriteLine("|---|:-:|--:|");
        foreach (var (label, needle) in new[]
                 {
                     ("痺れ（`StatusKeys.Stun`）", "if (actor.RawCounter(StatusKeys.Stun) > 0)"),
                     ("転倒（`StatusKeys.Stagger`）", "if (actor.RawCounter(StatusKeys.Stagger) > 0)"),
                     ("`IdleTurn` を立てる箇所（まどろみ・転倒・否決）", "actor.SetCounter(StatusKeys.IdleTurn, Turn);"),
                     ("周期（`ActionIndex++`）", "actor.ActionIndex++;"),
                     ("**連撃（`SwingTurn`）**", "private void SwingTurn(UnitState actor"),
                 })
        {
            var hit = Lines(eng, needle);
            Console.WriteLine("| " + label + " | " + (hit.Count > 0 ? "○" : "**×**") + " | "
                              + string.Join(" / ", hit.Select(h => h.N)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**4つとも `SwingTurn` の定義より手前（＝呼び出しより手前）にある**ので、"
                          + "何発振っても手番は1回として数えられる（`TurnsTaken` / `TurnAttacks` も 1）。");
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4. 再生側は同じ手番の連続 `Attack` を1発ずつ描くか");
        Console.WriteLine();
        int link = Count(main, "candidate.Kind == BattleEventKind.Attack && candidate.ActorId == attack.ActorId) break;");
        Console.WriteLine("- `DemoApp/Main.cs` の紐づけが**同じ駒の次の `Attack` で切れる**箇所: **" + link + "**"
                          + "（`ApplyLinkedDamageAtOnce` と `FindAttackTargets` の2本）");
        Console.WriteLine("- ウツの攻撃型は **" + UnitCatalog.Utsu.Pattern + "** なので、"
                          + "同時着弾（`pattern != Single` のときだけ走る）には1度も入らない");
        Console.WriteLine();
        Console.WriteLine(link >= 2
            ? "**束ねない。** 連撃は `Attack` → `Damage` の組が発数ぶん並び、再生側は1発ずつ"
              + "`Attack()` の絵と `Delay(0.14)` を通す ＝ **1発ずつ音が鳴る。再生側の変更は 0 行。**"
            : "**規則が引けない。** 紐づけの形が変わっているので、実装を読み直すこと。");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5. 連撃・多段攻撃を測った期が過去にあるか");
        Console.WriteLine();
        string[] words = { "連撃", "多段" };
        var files = Directory.Exists("design")
            ? Directory.GetFiles("design", "*.md")
                .Where(f => !Path.GetFileName(f).StartsWith("PHASE178", StringComparison.Ordinal)
                            && words.Any(w => File.ReadAllText(f).Contains(w))).ToArray()
            : Array.Empty<string>();
        Console.WriteLine("- `design/*.md` で「連撃」「多段」を含むファイル（第178期を除く）: **" + files.Length + " 件**"
                          + (files.Length > 0 ? "（" + string.Join(" / ", files.Select(Path.GetFileName)) + "）" : ""));
        Console.WriteLine();
        Console.WriteLine("**1手番に同じ駒が2回以上 `PerformAttack` を通る経路は第178期の前から4本ある**"
                          + "——反撃（`ctx.Reaction`）／割り込み（`ctx.Interrupt`）／"
                          + "追い打ち（`OverreachTrait`・`OnAnyDeath`）／再行動（`EncoreRule`・"
                          + "`HandleDeath` から `TakeTurn` を入れ子で呼ぶ）。"
                          + "**どれも「手番の中で複数回」ではなく「手番の外」なので `ModifyHitCount` は通らない。**");
        Console.WriteLine();

        // ---- 窓口の全数 ----
        Console.WriteLine("## 窓口（Modify 系）の全数");
        Console.WriteLine();
        Console.WriteLine("| 窓口 | 既定の定義 | 上書きしている特性 |");
        Console.WriteLine("|---|--:|--:|");
        foreach (string m in new[] { "ModifyAttack", "ModifyPattern", "ModifyIncomingDamage", "ModifyHitCount" })
        {
            string ret = m == "ModifyPattern" ? "AttackPattern" : "int";
            Console.WriteLine("| `" + m + "` | " + Count(tra, "public virtual " + ret + " " + m + "(")
                              + " | " + Count(tra, "public override " + ret + " " + m + "(") + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // hota —— §1-1 の数え物（V0 ＝ 焼かれる / V1 ＝ 焼かれない）
    // =================================================================================

    static void HotaCensus()
    {
        var rows = RowsWith(HotaId);
        Console.WriteLine("# 第178期 `rebirth hota` —— §1-1 の数え物");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ `EmberRule.Charred`（第177期まで＝ホタも焼かれる）／"
                          + "V1 ＝ 既定（`Fireproof = true` ＝ 火には焼かれない）。**");
        Console.WriteLine("ホタを含む行 **" + rows.Length + " 行**（`compare` ＋ 交差帯）× 第2〜5波 × seed 0.."
                          + (Seeds - 1) + "。分母は規約 (G10)。");
        Console.WriteLine();
        if (rows.Length == 0) { Console.WriteLine("**走査が空。止める（R034）。**"); return; }

        Console.WriteLine("| 行 | 波 | 版 | 勝率 | 倒れた% | 倒れたT中央 | 死因:燃焼% | 死因:それ以外% | 燃えたT中央 | 燃焼で失った/戦 | 振/戦 | 与ダメ/戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var agg = new double[2, 8];
        int cells = 0;
        foreach (var (name, f) in rows)
            for (int st = 1; st < 5; st++)
            {
                Cen v0 = Census(f, st, HotaId, EmberRule.Charred);
                Cen v1 = Census(f, st, HotaId, EmberRule.Default);
                Line(name, st, "V0", v0);
                Line(name, st, "**V1**", v1);
                for (int i = 0; i < 2; i++)
                {
                    Cen c = i == 0 ? v0 : v1;
                    agg[i, 0] += c.Win; agg[i, 1] += c.FellPct; agg[i, 2] += c.FellTurn;
                    agg[i, 3] += c.BurnDeathPct; agg[i, 4] += c.FoeDeathPct; agg[i, 5] += c.BurnTurns;
                    agg[i, 6] += c.BurnTaken; agg[i, 7] += c.Dmg;
                }
                cells++;
            }
        Console.WriteLine();
        Console.WriteLine("## まとめ（" + cells + " セルの平均）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 | 倒れた% | 倒れたT中央 | 死因:燃焼% | 死因:それ以外% | 燃えたT中央 | 燃焼で失った/戦 | 与ダメ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < 2; i++)
            Console.WriteLine("| " + (i == 0 ? "V0 焼かれる" : "**V1 焼かれない**") + " | "
                              + F1(agg[i, 0] / cells) + "% | " + F1(agg[i, 1] / cells) + "% | "
                              + F1(agg[i, 2] / cells) + " | " + F1(agg[i, 3] / cells) + "% | "
                              + F1(agg[i, 4] / cells) + "% | " + F1(agg[i, 5] / cells) + " | "
                              + F1(agg[i, 6] / cells) + " | " + F1(agg[i, 7] / cells) + " |");
        Console.WriteLine();
        Console.WriteLine("**死因の分母は「倒れた試行」**（`UnitTally.BurnDeaths` が立った試行 ／ 立たなかった試行）。");
        Console.WriteLine("**燃焼の刻みで倒れるのは「最後の一撃が燃焼だった」ときだけ**なので、"
                          + "敵の攻撃で削られたあとに燃焼でとどめを刺された試行は燃焼側に入る。");
        Console.WriteLine();

        void Line(string name, int st, string ver, Cen c)
            => Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + ver + " | "
                                 + F1(c.Win) + "% | " + F1(c.FellPct) + "% | " + F1(c.FellTurn) + " | "
                                 + F1(c.BurnDeathPct) + "% | " + F1(c.FoeDeathPct) + "% | "
                                 + F1(c.BurnTurns) + " | " + F1(c.BurnTaken) + " | "
                                 + F1(c.Atk) + " | " + F1(c.Dmg) + " |");
    }

    // =================================================================================
    // utsu —— ウツの帳簿
    // =================================================================================

    static void UtsuLedger()
    {
        var rows = RowsWith(UtsuId);
        Console.WriteLine("# 第178期 `rebirth utsu` —— ウツの帳簿");
        Console.WriteLine();
        Console.WriteLine("ウツを含む行 **" + rows.Length + " 行**（`compare` ＋ 交差帯）× 第2〜5波 × seed 0.."
                          + (Seeds - 1) + "。");
        Console.WriteLine("**V0 のノブは置いていない**ので、ここに出るのは新版（連撃）だけである"
                          + "——比較の相手は `docs/balance.md` / `docs/pulse.md`（第177期の committed な値）。");
        Console.WriteLine();
        if (rows.Length == 0) { Console.WriteLine("**走査が空。止める（R034）。**"); return; }

        Console.WriteLine("| 行 | 波 | 勝率 | 振/戦 | うち2発目以降 | 1手番あたり発数 | 与ダメ/戦 | 被ダメ/戦 | 倒れた% | 倒れたT中央 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in rows)
            for (int st = 1; st < 5; st++)
            {
                Cen c = Census(f, st, UtsuId, EmberRule.Default);
                double turns = c.SwingTurns;
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + F1(c.Win) + "% | "
                                  + F1(c.Atk) + " | " + F1(c.Extra) + " | "
                                  + (turns > 0 ? (c.Atk / turns).ToString("F2") : "—") + " | "
                                  + F1(c.Dmg) + " | " + F1(c.Taken) + " | " + F1(c.FellPct) + "% | "
                                  + F1(c.FellTurn) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("**「1手番あたり発数」＝ `Attacks ÷ TurnAttacks`**（分母は<b>攻撃で終わった手番の数</b>）。"
                          + "**`Attacks − ExtraSwings` を分母にしてはいけない**"
                          + "——手番が 2 回しか無い行では分母が 0.06 まで潰れて 179 のような値が出る。");
        Console.WriteLine();

        // ---- 台本の確認（自己検査 (e) の材料） ----
        Console.WriteLine("## 台本の確認 —— 1手番の中に `Attack` が何件並ぶか（第5波・seed 0）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ウツの `Attack` 総数 | 連続の最大 | `Attacks`（帳簿） |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var (name, f) in rows)
        {
            BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[4].Enemy, 0, verbose: true);
            int id = InstanceIdOf(f, UtsuId);
            int total = 0, best = 0, cur = 0;
            foreach (BattleEvent e in res.Events)
            {
                if (e.Kind == BattleEventKind.TurnStart) { cur = 0; continue; }
                if (e.Kind != BattleEventKind.Attack) continue;
                if (e.ActorId == id) { total++; cur++; if (cur > best) best = cur; }
                else cur = 0;
            }
            int tally = res.TallyByUnit.TryGetValue(UtsuId, out UnitTally? t) ? t.Attacks : -1;
            Console.WriteLine("| " + name + " | " + total + " | **" + best + "** | " + tally
                              + (total == tally ? "" : " ← **食い違い（`InstanceId` の推定を疑うこと）**") + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // rows —— 動いた行の全表
    // =================================================================================

    static void Rows()
    {
        Console.WriteLine("# 第178期 `rebirth rows` —— 動いた行");
        Console.WriteLine();
        var want = ReadBalance("docs/balance.md");
        if (want.Count == 0) { Console.WriteLine("**`docs/balance.md` が読めない。**"); return; }

        var rows = CompareBuilds();
        var got = new double[rows.Length][];
        Parallel.For(0, rows.Length, i => got[i] = Win5(rows[i].F, EmberRule.Default));

        Console.WriteLine("## `compare` 61 行（`docs/balance.md` ＝ 第177期 との差）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ホタ | ウツ | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 |");
        Console.WriteLine("|---|:-:|:-:|--:|--:|--:|--:|--:|--:|");
        int moved = 0, movedOther = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            if (!want.TryGetValue(rows[i].Name, out double[]? w))
            { Console.WriteLine("- 行が引けない: " + rows[i].Name); continue; }
            bool any = Enumerable.Range(0, 5).Any(st => Math.Abs(got[i][st] - w[st]) > 0.001);
            if (!any) continue;
            bool has = Has(rows[i].F, HotaId) || Has(rows[i].F, UtsuId);
            moved++;
            if (!has) movedOther++;
            Console.WriteLine("| " + rows[i].Name + " | " + (Has(rows[i].F, HotaId) ? "○" : "—") + " | "
                              + (Has(rows[i].F, UtsuId) ? "○" : "—") + " | "
                              + string.Join(" | ", Enumerable.Range(0, 5).Select(st => Sg(got[i][st] - w[st]))) + " | "
                              + Sg(got[i].Skip(1).Average() - w.Skip(1).Average()) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 動いた行: **" + moved + " / " + rows.Length + "**");
        Console.WriteLine("- そのうち**ホタもウツも含まない行**: **" + movedOther + "**（0 なら自己検査 (a) は ○）");
        Console.WriteLine();

        var pri = Enumerable.Range(0, rows.Length)
            .Where(i => Baseline.PrimaryRows.Contains(rows[i].Name) && want.ContainsKey(rows[i].Name)).ToArray();
        var all = Enumerable.Range(0, rows.Length).Where(i => want.ContainsKey(rows[i].Name)).ToArray();

        Console.WriteLine("## 波ごとの平均と歯止め");
        Console.WriteLine();
        Console.WriteLine("| 群 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var (label, idx) in new[] { ("全61行", all), ("主判定19行", pri) })
        {
            Console.WriteLine("| " + label + " | 第177期 | "
                              + string.Join(" | ", Enumerable.Range(0, 5)
                                  .Select(st => F1(idx.Average(i => want[rows[i].Name][st])))) + " |");
            Console.WriteLine("| " + label + " | **第178期** | "
                              + string.Join(" | ", Enumerable.Range(0, 5)
                                  .Select(st => F1(idx.Average(i => got[i][st])))) + " |");
        }
        double fifth = pri.Average(i => got[i][4]);
        Console.WriteLine();
        Console.WriteLine("- 主判定19行の第五波: **" + F1(fifth) + "%**（歯止め " + Baseline.PrimaryFifthFloor
                          + "% との差 " + Sg(fifth - Baseline.PrimaryFifthFloor) + "pt）→ **"
                          + (fifth >= Baseline.PrimaryFifthFloor ? "○" : "×（拒否権1）") + "**");
        int info0 = all.Sum(i => want[rows[i].Name].Skip(1).Count(x => x > 0 && x < 100));
        int info1 = all.Sum(i => got[i].Skip(1).Count(x => x > 0 && x < 100));
        Console.WriteLine("- 情報セル（全61行・第2〜5波）: " + info0 + " → **" + info1 + "**");
        Console.WriteLine("- 第五波 95% 超の行: " + all.Count(i => want[rows[i].Name][4] > 95)
                          + " → **" + all.Count(i => got[i][4] > 95) + "**（(G9) により注意であって拒否ではない）");
        Console.WriteLine();

        Console.WriteLine("## 拒否権3（主判定行がいずれかの波で −10.0pt 以上落ちる）");
        Console.WriteLine();
        int veto = 0;
        foreach (int i in pri)
            for (int st = 0; st < 5; st++)
                if (got[i][st] - want[rows[i].Name][st] <= -10.0)
                {
                    veto++;
                    Console.WriteLine("- **" + rows[i].Name + " 第" + (st + 1) + "波: "
                                      + Sg(got[i][st] - want[rows[i].Name][st]) + "pt**");
                }
        Console.WriteLine(veto == 0 ? "- 該当 **0 件**" : "");
        Console.WriteLine();

        Console.WriteLine("## 交差帯（ホタ／ウツを含む行だけ・その場で測る）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ホタ | ウツ | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|:-:|:-:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in CrossBuilds())
        {
            if (!Has(f, HotaId) && !Has(f, UtsuId)) continue;
            double[] w1 = Win5(f, EmberRule.Default);
            Console.WriteLine("| " + name + " | " + (Has(f, HotaId) ? "○" : "—") + " | "
                              + (Has(f, UtsuId) ? "○" : "—") + " | " + string.Join(" | ", w1.Select(F1)) + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        string bal = arg.Length > 0 ? arg : "docs/balance.md";
        Console.WriteLine("# 第178期 自己検査 —— `rebirth check`");
        Console.WriteLine();

        var want = ReadBalance(bal);
        Console.WriteLine("## (a) ホタ・ウツを含まない行が `" + bal + "` と 0 件差分（回帰の本体）");
        Console.WriteLine();
        int cells = 0, bad = 0, cellsAll = 0, badAll = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            if (!want.TryGetValue(name, out double[]? w)) { Console.WriteLine("- 行が引けない: " + name); continue; }
            double[] got = Win5(f, EmberRule.Default);
            bool has = Has(f, HotaId) || Has(f, UtsuId);
            for (int st = 0; st < 5; st++)
            {
                cellsAll++;
                if (Math.Abs(got[st] - w[st]) > 0.001) badAll++;
                if (has) continue;
                cells++;
                if (Math.Abs(got[st] - w[st]) > 0.001) bad++;
            }
        }
        Console.WriteLine("- ホタ・ウツを含まない行: **" + cells + " セル / 食い違い " + bad + " 件** → "
                          + (bad == 0 ? "**○**" : "**×**"));
        Console.WriteLine("- 全61行（参考）: " + cellsAll + " セル / 食い違い " + badAll + " 件");
        Console.WriteLine();

        Console.WriteLine("## (a') `EmberRule.Charred` にすると、ウツ非在席の行が 0 件差分に戻る");
        Console.WriteLine();
        int c2 = 0, b2 = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            if (Has(f, UtsuId) || !want.TryGetValue(name, out double[]? w)) continue;
            double[] got = Win5(f, EmberRule.Charred);
            for (int st = 0; st < 5; st++) { c2++; if (Math.Abs(got[st] - w[st]) > 0.001) b2++; }
        }
        Console.WriteLine("- **" + c2 + " セル / 食い違い " + b2 + " 件** → " + (b2 == 0 ? "**○**" : "**×**"));
        Console.WriteLine("  （ホタの変更が `Fireproof` の1枝にすべて閉じていることの検算）");
        Console.WriteLine();

        Console.WriteLine("## (b) `ModifyHitCount` が 1 以外を返す駒");
        Console.WriteLine();
        var multi = UnitCatalog.Everyone
            .Where(d => TraitCatalog.Resolve(d.Traits).Any(t => t.ModifyHitCount(Probe(d), 1) != 1)).ToArray();
        Console.WriteLine("- `AtkBonus = 0` の素の状態で 1 を返さない駒: **" + multi.Length + " 枚**"
                          + (multi.Length > 0 ? "（" + string.Join("・", multi.Select(d => d.Name)) + "）" : "")
                          + " → " + (multi.Length == 0 ? "**○**" : "**×**"));
        foreach (int b in new[] { -2, -3, -12, -45, 6 })
        {
            UnitState p = Probe(UnitCatalog.Utsu);
            p.AtkBonus = b;
            var traits = TraitCatalog.Resolve(UnitCatalog.Utsu.Traits);
            int hits = traits.Aggregate(1, (h, t) => t.ModifyHitCount(p, h));
            int atk = traits.Aggregate(UnitCatalog.Utsu.Attack, (a, t) => t.ModifyAttack(p, a));
            int oldOut = b < 0 ? UnitCatalog.Utsu.Attack + 3 * -b : Math.Max(1, UnitCatalog.Utsu.Attack / 2);
            Console.WriteLine("- `AtkBonus = " + b + "`: **" + hits + " 発 × 攻 " + atk + " ＝ " + hits * atk
                              + "**（旧版なら攻 " + oldOut + " ＝ 旧比 "
                              + ((double)hits * atk / oldOut).ToString("F2") + "）");
        }
        Console.WriteLine();

        Console.WriteLine("## (c) `TickHeal` の既定が 0 で、1ビットも動かさない");
        Console.WriteLine();
        Console.WriteLine("- `EmberRule.Default` ＝ `" + EmberRule.Default + "`");
        Console.WriteLine("- `EmberRule.Charred` ＝ `" + EmberRule.Charred + "`");
        int c3 = 0, b3 = 0;
        foreach (var (_, f) in CompareBuilds().Where(r => Has(r.F, HotaId)))
        {
            double[] a = Win5(f, EmberRule.Default);
            double[] c = Win5(f, new EmberRule(false, true, 0));
            for (int st = 0; st < 5; st++) { c3++; if (Math.Abs(a[st] - c[st]) > 0.001) b3++; }
        }
        Console.WriteLine("- `new EmberRule(false, true, 0)` と既定: **" + c3 + " セル / 食い違い " + b3 + " 件** → "
                          + (b3 == 0 ? "**○**" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("## (d) 必須4: `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        string eng = ReadSrc("BattleCore/BattleEngine.cs"), tra = ReadSrc("BattleCore/Traits.cs");
        Console.WriteLine("- `PickOne(` の件数: **" + (Count(eng, "PickOne(") + Count(tra, "PickOne(")) + "**"
                          + "（連撃も火の免疫も乱数を1つも引かない）");
        Console.WriteLine();

        Console.WriteLine("## (e) 連撃が手番の外へ漏れていない");
        Console.WriteLine();
        Console.WriteLine("- `ModifyHitCount` を問う箇所: **" + Count(eng, "t.ModifyHitCount(actor, hits)") + "**"
                          + "（`SwingTurn` の1箇所だけなら ○）");
        Console.WriteLine("- `SwingTurn(actor` の呼び出し: **" + Count(eng, "SwingTurn(actor") + "**"
                          + "（`TakeTurnCore` の2つの出口だけなら ○。定義は `SwingTurn(UnitState actor` なので数に入らない）");
        Console.WriteLine();
    }

    static UnitState Probe(UnitDef d) => new() { Def = d, TeamId = 0, Hp = d.MaxHp, Slot = 0 };

    // =================================================================================
    // 器具
    // =================================================================================

    readonly record struct Cen(double Win, double FellPct, double FellTurn, double BurnDeathPct,
                               double FoeDeathPct, double BurnTurns, double BurnTaken,
                               double Atk, double Extra, double Dmg, double Taken, double SwingTurns);

    static Cen Census(Formation f, int st, string id, EmberRule ember)
    {
        var fellTurns = new List<int>();
        var burnTurns = new List<int>();
        int wins = 0, fell = 0, burnDead = 0;
        double atk = 0, extra = 0, dmg = 0, taken = 0, burnTaken = 0, swingTurns = 0;
        for (int seed = 0; seed < Seeds; seed++)
        {
            BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                verbose: false, ember: ember);
            if (res.PlayerWon) wins++;
            if (!res.TallyByUnit.TryGetValue(id, out UnitTally? t)) continue;
            burnTurns.Add(t.BurnTicks);
            atk += t.Attacks; extra += t.ExtraSwings; swingTurns += t.TurnAttacks;
            dmg += t.DamageToEnemy; taken += t.DamageTaken; burnTaken += t.BurnTaken;
            if (t.Deaths > 0)
            {
                fell++;
                fellTurns.Add(t.LastActiveTurn);
                if (t.BurnDeaths > 0) burnDead++;
            }
        }
        return new Cen(100.0 * wins / Seeds, 100.0 * fell / Seeds, Median(fellTurns),
                       fell > 0 ? 100.0 * burnDead / fell : 0,
                       fell > 0 ? 100.0 * (fell - burnDead) / fell : 0,
                       Median(burnTurns), burnTaken / Seeds,
                       atk / Seeds, extra / Seeds, dmg / Seeds, taken / Seeds, swingTurns / Seeds);
    }

    static double Median(List<int> xs)
    {
        if (xs.Count == 0) return 0;
        xs.Sort();
        int m = xs.Count / 2;
        return xs.Count % 2 == 1 ? xs[m] : (xs[m - 1] + xs[m]) / 2.0;
    }

    static double[] Win5(Formation f, EmberRule ember)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, ember: ember).PlayerWon)
                    wins++;
            w[st] = 100.0 * wins / Seeds;
        }
        return w;
    }

    /// <summary>
    /// 味方の <c>InstanceId</c> はスロット昇順・味方が先（<c>ctx.Add</c> が 0 から振る）。
    /// <b>台本の確認でしか使わない</b>ので、帳簿の <c>Attacks</c> と突き合わせて食い違いを表に出す。
    /// </summary>
    static int InstanceIdOf(Formation f, string id)
    {
        int i = 0;
        foreach (var (_, def) in f.Occupied())
        {
            if (def.Id == id) return i;
            i++;
        }
        return -1;
    }

    static (string Name, Formation F)[] RowsWith(string id)
        => CompareBuilds().Concat(CrossBuilds()).Where(r => Has(r.F, id)).ToArray();

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    static Dictionary<string, double[]> ReadBalance(string path)
    {
        var want = new Dictionary<string, double[]>();
        if (!File.Exists(path)) return want;
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            string[] c = line.Split('|', StringSplitOptions.TrimEntries);
            if (c.Length < 8) continue;
            var v = new double[5];
            bool ok = true;
            for (int i = 0; i < 5; i++)
                if (!double.TryParse(c[i + 2].Replace("%", ""), out v[i])) { ok = false; break; }
            if (ok) want[c[1]] = v;
        }
        return want;
    }

    static string ReadSrc(string rel) => File.Exists(rel) ? File.ReadAllText(rel) : "";

    static List<(int N, string Text)> Lines(string src, string needle)
    {
        var hits = new List<(int, string)>();
        string[] lines = src.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains(needle, StringComparison.Ordinal)) hits.Add((i + 1, lines[i]));
        return hits;
    }

    static int Count(string s, string needle)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    static string F1(double v) => v.ToString("F1");
    static string Sg(double v) => v.ToString("+0.0;-0.0;0.0");
}
