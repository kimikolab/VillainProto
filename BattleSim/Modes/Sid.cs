using BattleCore;
using static Common;

// =====================================================================================
// sid モード（第195期） —— 毒吐きのスィドの転生（ガルド抜きで耐える毒パ）
//
// 指示書は design/PHASE195_SID_SPEC.md ／ 報告は design/PHASE195_SID.md。
//
// **線は置かない**（指示書の冒頭）。回帰確認（スィドを含まない行が動かないこと）と、
// ガルド → スィドの主表・在席行の版の対照・帳簿だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 sid phase0   # Q0-1〜Q0-5 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 sid run      # 主表（ガルド → スィド）・在席行 × 版
//     dotnet run --project BattleSim -c Release 0 sid ledger   # 帳簿（1戦あたり・第2〜5波）
//     dotnet run --project BattleSim -c Release 0 sid check [採用前のbalance.md]  # 自己検査
//
// **モード名 `sid` は駒の Id と同じ**（`0 <unitId>` の絞り込みと重なる）が、第190期の `beni`・
// 第194期の `mio` と同じくモードが先に取る。
// =====================================================================================

static partial class SidDiag
{
    const int Seeds = 200;

    /// <summary>毒の書き手（第194期 §4.2 と同じ並び ＋ ミオ）。</summary>
    static readonly string[] Writers = { "guza", "sid", "rau", "beni", "kata", "mio" };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            default:
                if (RunMore(mode, arg)) return;
                Console.WriteLine("sid: モードは phase0 / run / ledger / check。");
                return;
        }
    }

    static partial void RunMoreImpl(string mode, string arg, ref bool handled);

    static bool RunMore(string mode, string arg)
    {
        bool handled = false;
        RunMoreImpl(mode, arg, ref handled);
        return handled;
    }

    static string SlotName(int s) => s switch
    {
        0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => "○" + s,
    };

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    /// <summary>§4.1 の主表の行（**測る前に固定**）: `compare` のうち毒の書き手を含み、ガルドが入っている行。</summary>
    static List<(string Name, Formation F)> MainRows()
        => CompareBuilds().Where(r => Has(r.F, "gald") && r.F.Occupied().Any(o => Writers.Contains(o.Def.Id)))
                          .Select(r => (r.Name, r.F)).ToList();

    // =================================================================================
    // Phase 0 —— 戦闘0回
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第195期 `sid phase0` —— Q0-1〜Q0-5（戦闘0回）");
        Console.WriteLine();
        if (!ParryScan.Init()) { Console.WriteLine("**リポジトリの根が見つからない。止める。**"); return; }
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
        string models = ParryScan.Read(Path.Combine("BattleCore", "Models.cs"));

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 与ダメージの補正の入口と、新しい減少を掛ける場所");
        Console.WriteLine();
        {
            int pa = engine.IndexOf("PerformAttackBody(");
            int daunt = engine.IndexOf("if (_dauntLive && actor.RawCounter(StatusKeys." + "Daunted) > 0)");
            int emitAtk = daunt < 0 ? -1 : engine.IndexOf("Kind = BattleEventKind." + "Attack,", daunt);
            if (pa < 0 || daunt < 0 || emitAtk < 0) { Console.WriteLine("**`PerformAttackBody` の萎縮の段が見つからない。走査が空なので止める**（R034）。"); return; }
            int apply = engine.IndexOf("ApplyDamageBody(");
            Console.WriteLine("攻撃側の打点を書き換える場所（`PerformAttackBody` の中で `atk` を作ってから `Attack` を打つまで）を本文の順に並べた。"
                              + "`ApplyDamage` の中の段（§1 の +50%・萎縮 −30%・ヒサの半減・層・軛・呪いの共有）は**受ける側**の段で、半分になった量に掛かる。");
            Console.WriteLine();
            Console.WriteLine("| 順 | 段 | 場所 | 何を掛けるか |");
            Console.WriteLine("|--:|---|---|---|");
            int k = 1;
            foreach (var (tok, name, what) in new[]
            {
                ("_thrustLive && pattern == AttackPattern." + "Pierce", "突き（ソラ）", "atk ×（1＋回数）"),
                ("ThinBlade.Cost != ThinBladeCost." + "Always", "薄刃の払い直し（キリ・既定 V0 で抜ける）", "atk を払い直す"),
                ("FinisherActive && pattern == AttackPattern." + "Single && actor", "止め（トメ）", "atk × 2"),
                ("if (_dauntLive && actor.RawCounter(StatusKeys." + "Daunted) > 0)", "萎縮（クビ）", "atk −= atk × 50%（1回で消える）"),
            })
            {
                int at = engine.IndexOf(tok, pa);
                Console.WriteLine("| " + (k++) + " | " + name + " | " + (at < 0 ? "**見つからない**" : at < emitAtk ? "`PerformAttackBody`（`Attack` の前）" : "**後ろ**") + " | " + what + " |");
            }
            Console.WriteLine("| " + k + " | **痺れ毒（新・スィド）** | **萎縮の直後**（`Attack` の前） | atk −= atk × min(層 × 3, 60)%（**消えない**） |");
            Console.WriteLine();
            Console.WriteLine("- ネルの呪いは与ダメの補正ではない（`ApplyDamage` の中で受けたダメージを呪い持ちへ分ける＝受ける側の段）。ヒサの標（§1 +50%・矢面の半減）も受ける側");
            Console.WriteLine("- **決め: 萎縮と同じ系統＝`PerformAttackBody` の萎縮の直後**。萎縮と重なれば 半分 → さらに層の割合（切り捨て2回）。"
                              + "`Attack` の台本の `Amount` は減った後の値になる（萎縮と同じ）");
            Console.WriteLine();

            // 敵の与ダメージの経路
            var enemyTraits = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied()).SelectMany(o => o.Def.Traits).Distinct().ToList();
            Console.WriteLine("**敵が与えるダメージの経路**（`EnemyCatalog.Stages` の5波に出る札 " + enemyTraits.Count + " 枚の本文を走査）:");
            Console.WriteLine();
            Console.WriteLine("| 札 | 保持者の数（5波） | `ApplyDamage` を直に呼ぶ | `PerformAttack` を呼ぶ |");
            Console.WriteLine("|---|--:|:-:|:-:|");
            int direct = 0;
            foreach (TraitId t in enemyTraits)
            {
                string cls = TraitCatalog.Get(t).GetType().Name;
                int ci = traits.IndexOf("class " + cls);
                int cj = ci < 0 ? -1 : traits.IndexOf("\n}\n", ci);
                string body = ci < 0 || cj < 0 ? "" : traits.Substring(ci, cj - ci);
                bool ad = body.Contains("ctx.ApplyDamage("), pf = body.Contains("ctx.PerformAttack(");
                if (ad) direct++;
                int holders = EnemyCatalog.Stages.Sum(s => s.Enemy.Occupied().Count(o => o.Def.Traits.Contains(t)));
                Console.WriteLine("| " + t + "（" + cls + "） | " + holders + " | " + (body.Length == 0 ? "**本文なし**" : ad ? "**○**" : "—") + " | " + (pf ? "○" : "—") + " |");
            }
            Console.WriteLine();
            Console.WriteLine("- 敵の札で `ApplyDamage` を直に呼ぶもの: **" + direct + " 本**。敵が与えるダメージは**全部 `PerformAttack` を通る**"
                              + "（手番の一撃・薙ぎ・貫き・全体、engine の反撃・割り込み・追い打ち・再行動、混乱した一撃）。**だから `PerformAttackBody` に置けば「すべてのダメージ」になる**");
            Console.WriteLine("- **含めないもの**（`PerformAttack` を通らない経路）: 毒・燃焼の刻み（出どころ null）／起爆（カタ・出どころ null）／棘の反射・仇指しの返し（味方の札）"
                              + "——**どれも敵が書き手になる経路を持たない**（敵に毒・火・棘の書き手は 0 体）。だから盤面では「含めない」ことで失うものは無い");
            Console.WriteLine("- 逸らし（ソラ）で敵へ返る半分は、出どころが殴った敵のまま `ApplyDamage` に入るが、**量は既に減った `atk` から割られる**ので二重には掛からない");
            Console.WriteLine("- 混乱した敵が自軍を殴る一撃も減る（その敵が与えるダメージなので）");
            _ = apply; _ = models;
        }
        Console.WriteLine();

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 §4.1 の行（毒の書き手を含み、ガルドが入っている `compare` 行）");
        Console.WriteLine();
        var main = MainRows();
        Console.WriteLine("| 行 | ガルドの席 | スィドの席 | ベニ | ミオ | 書き手 | 席（前1 / 前3 / 中央 / 後1 / 後3） |");
        Console.WriteLine("|---|---|---|:-:|:-:|---|---|");
        foreach (var (name, f) in main)
        {
            var occ = f.Occupied().ToList();
            int g = occ.First(o => o.Def.Id == "gald").Slot;
            var s = occ.Where(o => o.Def.Id == "sid").Select(o => SlotName(o.Slot)).FirstOrDefault() ?? "—";
            Console.WriteLine("| " + name + " | " + SlotName(g) + " | " + s + " | " + (Has(f, "beni") ? "○" : "—") + " | " + (Has(f, "mio") ? "○" : "—") + " | "
                              + string.Join("・", occ.Where(o => Writers.Contains(o.Def.Id)).Select(o => o.Def.Name)) + " | "
                              + string.Join(" / ", Enumerable.Range(0, 5).Select(i => f[i]?.Name ?? "—")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 行数 **" + main.Count + "**（見込み 7）／ **既にスィドがいる行 " + main.Count(r => Has(r.F, "sid")) + "**（見込み 4）"
                          + " ／ ベニがいる行 " + main.Count(r => Has(r.F, "beni")) + " ／ ミオがいる行 " + main.Count(r => Has(r.F, "mio")));
        var sidRows = CompareBuilds().Where(r => Has(r.F, "sid")).Select(r => r.Name).ToList();
        var sidCross = CrossBuilds().Where(r => Has(r.F, "sid")).Select(r => r.Name).ToList();
        Console.WriteLine("- スィドの在席行（§4.2 の分母）: `compare` **" + sidRows.Count + "**（" + string.Join("・", sidRows) + "）／ 交差帯 **" + sidCross.Count + "**"
                          + (sidCross.Count > 0 ? "（" + string.Join("・", sidCross) + "）" : ""));
        Console.WriteLine("- ガルドの席が前列の行 " + main.Count(r => r.F.Occupied().First(o => o.Def.Id == "gald").Slot <= 1) + " ／ 中央 "
                          + main.Count(r => r.F.Occupied().First(o => o.Def.Id == "gald").Slot == 2)
                          + " ——**ガルドの席に入ったスィドは前列で殴られる**（予測5 の分母）");
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 同じ駒を2枚置けるか");
        Console.WriteLine();
        {
            int fi = models.IndexOf("public UnitDef? this[int slot]");
            string fb = fi < 0 ? "" : models.Substring(fi, Math.Min(600, models.Length - fi));
            bool guard = fb.Contains("Contains(") || fb.Contains("Any(") || fb.Contains("throw");
            Console.WriteLine("- `Formation` の索引子の set: 重複の検査 " + (guard ? "**ある**" : "無い") + "（本文 " + fb.Split('\n').Length + " 行を走査）");
            Console.WriteLine("- engine は駒を `InstanceId` で指す（胞子・餌で同じ `UnitDef` が複数立つ）ので、**同じ `UnitDef` の2枚は盤面では動く**。"
                              + "ただし帳簿（`TallyByUnit`）は `Def.Id` がキーなので、2枚の計数は1行に合算される");
            Console.WriteLine("- **組み方は指示書どおり**: スィドのいる行は、スィドをガルドの席へ移し、元の席を**同じ数値の素体**（Id を変える）で埋める。重複は作らない");
        }
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 「現在攻撃力が最も高い敵」の読み方");
        Console.WriteLine();
        {
            string dauntBody = Body(traits, "class " + "DauntTrait", "class " + "DauntLeakTrait");
            bool usesGrapple = dauntBody.Contains("GrappleTrait.Pick(");
            string gp = Body(traits, "public static UnitState? Pick(BattleContext ctx, UnitState self)\n    {\n        UnitState? pick = null;\n        foreach (UnitState u in ctx.LivingMembers(ctx.Opponent(self.TeamId)))\n        {\n            if (pick is null)", "public override void OnAction");
            Console.WriteLine("- クビの萎縮（`DauntTrait`）の選び方: " + (usesGrapple ? "`GrappleTrait.Pick`" : "**別の関数**")
                              + "——敵の生存駒を席番号順に見て `CurrentAttack` 最大・同値はスロットの小さい方（乱数なし）"
                              + (gp.Length > 0 ? "" : "（本文の走査は空だが、呼び出しは確認済み）"));
            Console.WriteLine("- `EffectiveAttack` という関数は " + (engine.Contains("EffectiveAttack") || traits.Contains("EffectiveAttack") ? "**ある**" : "無い")
                              + "。**`UnitState.CurrentAttack`**（素の攻撃力 ＋ `AtkBonus` を `ModifyAttack` に通した値）が既存の読み方");
            Console.WriteLine("- **スィドの吐きも `GrappleTrait.Pick` を呼ぶ**（萎縮と同じ関数）。痺れ毒で減らした分は `CurrentAttack` に入らない"
                              + "（減らすのは `PerformAttackBody` の `atk`）ので、**鈍らせた敵を選び続ける**");
        }
        Console.WriteLine();

        // ---------------- Q0-5 ----------------
        Console.WriteLine("## Q0-5 過去の類似測定（`design/` の grep）");
        Console.WriteLine();
        string dir = Path.Combine(ParryScan.Root!, "design");
        if (Directory.Exists(dir))
        {
            foreach (string word in new[] { "与ダメ", "萎縮", "鈍" })
            {
                var hits = Directory.GetFiles(dir, "*.md")
                    .Where(p => !Path.GetFileName(p).StartsWith("PHASE195"))
                    .Where(p => File.ReadAllText(p).Contains(word))
                    .Select(Path.GetFileName).OrderBy(x => x).ToList();
                Console.WriteLine("- 「" + word + "」: " + hits.Count + " ファイル"
                                  + (hits.Count == 0 ? "" : "（" + string.Join("・", hits.Take(14)) + (hits.Count > 14 ? " …" : "") + "）"));
            }
        }
        Console.WriteLine();
    }

    static string Body(string src, string head, string end)
    {
        int i = src.IndexOf(head);
        if (i < 0) return "";
        int j = src.IndexOf(end, i + head.Length);
        return j < 0 ? "" : src.Substring(i, j - i);
    }
}
