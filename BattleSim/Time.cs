using BattleCore;

// =====================================================================================
// time モード（第126期） —— 軸が回る前に落ちる問題（時間を買う）
//
// 起点は2つの期が別の道から同じ場所に着いていること:
//
//   第117期（測定）  育ちは実在する（ムド 3.0 → 36.1 ＝ 12.0 倍）のに**傾きは 16 組すべて 1.0 未満**。
//                    足りないのはターン数で、「死なない」4枚のうち生存Tを +1.0T 以上買っているのは
//                    ゴルム1枚だけ。次の期の候補の第一位が「時間を買う機構を1本作る期」。
//   第125期（観察）  シナジーの無いボルグ 254・ドルガ 201 が、軸の駒（トメ 60・ノミ 122）を倍近く上回る。
//                    「どの編成も爽快感のある勝利ではなかった」が3期連続。
//
// **Phase 0 は測るだけ。** 盤面は1ビットも動かない——この診断が読むのは計数
// （`UnitTally.LastActiveTurn` / `Deaths` / `DamageTaken` / `DamageToEnemy`）だけで、
// engine には規則も窓口も1本も足していない。
//
// **`Program.cs` の top-level statements には置かない**（`TankDiag` / `Wound2Diag` と同じ判断。
// あちらは 69,000 行が全部で1つのメソッドで Release のビルドに 4 分半かかる）。
// **クロージャを1つも作らない形**（static メソッドと static フィールドだけ）で外に置く。
//
//     dotnet run --project BattleSim -c Release 0 time phase0   # Q0-1〜Q0-8
// =====================================================================================
static class TimeDiag
{
    // --- 定数（測る前に固定する）--------------------------------------------------------
    const int Seeds = 200;          // 帯A。`compare` と揃える（規約 (G14)）

    static string? _root;

    /// <summary>`Traits.cs` から索いた特性の本体（<c>TraitId</c> → クラス本体）。<b>空なら止める</b>。</summary>
    static readonly Dictionary<TraitId, string> _body = new();

    public static void Run(string mode)
    {
        if (!Init()) return;
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunTables(); return;
            default:
                Console.WriteLine("time: モードは phase0 / run（第126期）。");
                return;
        }
    }

    // ==================================================================================
    // 索く。**手で書いた分類は1件も無い**（第94期の作法）。
    // **索けなかったら止める**（第117期に踏んだ穴——走査が空でも分類はすべて偽を返すので、
    // 黙って土台の顔ぶれが変わる）。
    // ==================================================================================
    static bool Init()
    {
        _root = Directory.GetCurrentDirectory();
        while (_root != null && !File.Exists(Path.Combine(_root, "docs", "balance.md")))
            _root = Path.GetDirectoryName(_root);

        string path = _root is null ? "" : Path.Combine(_root, "BattleCore", "Traits.cs");
        if (!File.Exists(path))
        {
            Console.WriteLine("time: `BattleCore/Traits.cs` が見つからない（リポジトリ直下から実行すること）。"
                + " **走査が空なら止める**（第117期の器具の事故）。");
            return false;
        }

        string src = File.ReadAllText(path);
        var decl = System.Text.RegularExpressions.Regex.Matches(
            src, @"public (?:sealed |abstract )?class (\w+Trait)(?:\s*:\s*(\w+))?");
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        var baseOf = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < decl.Count; i++)
        {
            int st = decl[i].Index;
            int en = i + 1 < decl.Count ? decl[i + 1].Index : src.Length;
            byName[decl[i].Groups[1].Value] = src.Substring(st, en - st);
            if (decl[i].Groups[2].Success) baseOf[decl[i].Groups[1].Value] = decl[i].Groups[2].Value;
        }
        foreach (KeyValuePair<string, string> kv in byName)
        {
            string body = kv.Value;
            // 基底の本体を連結する（庇う＝`GuardianTrait : RedirectGainTrait` がこれ）。
            if (baseOf.TryGetValue(kv.Key, out string? b) && byName.TryGetValue(b, out string? bb)) body += bb;
            var m = System.Text.RegularExpressions.Regex.Match(body, @"TraitId Id => TraitId\.(\w+)");
            if (m.Success && Enum.TryParse(m.Groups[1].Value, out TraitId tid)) _body[tid] = body;
        }
        if (_body.Count == 0)
        {
            Console.WriteLine("time: `Traits.cs` の走査が **0 件**。止める（第117期）。");
            return false;
        }
        return true;
    }

    static bool Has(UnitDef d, params string[] needles)
    {
        foreach (TraitId t in d.Traits)
        {
            if (!_body.TryGetValue(t, out string? b)) continue;
            foreach (string n in needles) if (b.Contains(n, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary><c>TraitCatalog</c> に登録されているか（<c>Get</c> は未登録だと投げる）。</summary>
    static bool Registered(TraitId t)
    {
        try { return TraitCatalog.Get(t) is not null; }
        catch (KeyNotFoundException) { return false; }
    }

    // --- 分類（Q0-2）。**すべて実装から引く。手で駒名を並べない。**-----------------------
    //
    // 問いは「その駒の出力は第1ターンから最大か、それともターン数を要求するか」。
    // 3つに分ける（排他ではないので優先順位を固定する: 蓄積 > 連携 > 即時）。

    /// <summary>蓄積の印。自分の出力を戦闘中に積み上げる（＝ターン数を要求する）。</summary>
    static readonly string[] AccNeedles = { "self.AtkBonus +=", "ApplyStack", "SetCounter", "AddCounter" };

    /// <summary>連携の印。他の駒を読む／他の駒に書く（＝相方と時間を要求する）。</summary>
    static readonly string[] DepNeedles =
    {
        "ctx.Whet(", "ctx.Dull(", "ctx.Heal(", "ctx.Revive(", "ctx.Summon(",
        "SupportTargets", "MostHurtAlly", "LivingMembers", "AreAdjacent"
    };

    static bool Accumulates(UnitDef d) => Has(d, AccNeedles);
    static bool Depends(UnitDef d) => Has(d, DepNeedles);

    static string ClassOf(UnitDef d)
        => Accumulates(d) ? "蓄積" : Depends(d) ? "連携" : "即時";

    /// <summary>
    /// <b>どの印が当たったか</b>を「特性: 印」の形で返す（第119期「食い違いの理由を1行ずつ書く」を
    /// 手で書かずに機械で出すため）。空なら `即時`。
    /// </summary>
    static string WhyOf(UnitDef d)
    {
        var hits = new List<string>();
        foreach (TraitId t in d.Traits)
        {
            if (!_body.TryGetValue(t, out string? b)) continue;
            foreach (string n in AccNeedles)
                if (b.Contains(n, StringComparison.Ordinal)) hits.Add($"`{t}`→`{n}`");
            foreach (string n in DepNeedles)
                if (b.Contains(n, StringComparison.Ordinal)) hits.Add($"`{t}`→`{n}`");
        }
        return hits.Count == 0 ? "—（印なし）" : string.Join(" ／ ", hits.Distinct());
    }

    /// <summary>攻撃力の分類の線。<b>`UnitCatalog.All` の攻撃力の中央値</b>（手で決めない）。</summary>
    static int AtkLine()
    {
        int[] a = UnitCatalog.All.Select(d => d.Attack).OrderBy(x => x).ToArray();
        return a.Length == 0 ? 0 : a[a.Length / 2];
    }

    // ==================================================================================
    // Phase 0
    // ==================================================================================
    static void Phase0()
    {
        Console.WriteLine("# 第126期 Phase 0 —— 軸が回る前に落ちる問題");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 time phase0` の出力。");
        Console.WriteLine("**盤面は1ビットも動かない**（読むのは計数だけ。engine に規則も窓口も足していない）。");
        Console.WriteLine();

        Q07();
        Q05();
        Q06();
        Q04();
        Q08();
        Q01Q02Q03();
    }

    // ---------------------------------------------------------------------------------
    // Q0-7 —— 段0 の前提（`Actions` の枚数・攻撃型の内訳・手番を縛る特性）
    // ---------------------------------------------------------------------------------
    static void Q07()
    {
        Console.WriteLine("## Q0-7 —— 段0 の前提（`UnitCatalog.All` から機械で数える）");
        Console.WriteLine();

        var all = UnitCatalog.All;
        int withActions = 0;
        var actionNames = new List<string>();
        foreach (UnitDef d in all)
            if (d.Actions is { Count: > 0 }) { withActions++; actionNames.Add(d.Name); }

        Console.WriteLine($"味方の員数 **{all.Count} 枚**。");
        Console.WriteLine();
        Console.WriteLine("| | 枚数 | 顔ぶれ |");
        Console.WriteLine("|---|--:|---|");
        Console.WriteLine($"| `Actions` を持つ | **{withActions} / {all.Count}** | "
            + (actionNames.Count == 0 ? "—" : string.Join("・", actionNames)) + " |");
        Console.WriteLine();
        Console.WriteLine($"→ **「アクティブスキル」という欄を作ると {all.Count - withActions} 枚が空欄になる。**"
            + " 欄の名前は「手番」にして常に埋める（§1）。");
        Console.WriteLine();

        Console.WriteLine("### 常時の攻撃型の内訳");
        Console.WriteLine();
        Console.WriteLine("| 攻撃型 | 味方 | 敵（`EnemyCatalog.Stages` の参考） |");
        Console.WriteLine("|---|--:|--:|");
        foreach (AttackPattern p in Enum.GetValues<AttackPattern>())
        {
            int mine = all.Count(d => d.Pattern == p);
            int foes = EnemyCatalog.Stages
                .SelectMany(st => st.Enemy.Occupied().Select(x => x.Def))
                .Distinct().Count(d => d.Pattern == p);
            Console.WriteLine($"| {p} | {mine} | {foes} |");
        }
        Console.WriteLine();

        Console.WriteLine("### 手番を縛る特性");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 枚数 | 保持者 |");
        Console.WriteLine("|---|--:|---|");
        foreach (TraitId t in new[] { TraitId.Sluggish, TraitId.Immobile, TraitId.Pursuer, TraitId.Displaced })
        {
            var who = all.Where(d => d.Traits.Contains(t)).Select(d => d.Name).ToArray();
            Console.WriteLine($"| `{t}` | {who.Length} | {(who.Length == 0 ? "—" : string.Join("・", who))} |");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Q0-5 —— 第118期の `Regen` / `Nourish` の残置
    // ---------------------------------------------------------------------------------
    static void Q05()
    {
        Console.WriteLine("## Q0-5 —— 第118期の `Regen` / `Nourish` は残っているか");
        Console.WriteLine();
        Console.WriteLine("| 特性 | `Traits.cs` の本体 | `TraitCatalog` に登録 | `UnitCatalog.All` の保持者 |");
        Console.WriteLine("|---|:-:|:-:|--:|");
        foreach (TraitId t in new[] { TraitId.Regen, TraitId.Nourish })
        {
            bool body = _body.ContainsKey(t);
            bool cat = Registered(t);
            int holders = UnitCatalog.All.Count(d => d.Traits.Contains(t));
            Console.WriteLine($"| `{t}` | {(body ? "○" : "**×**")} | {(cat ? "○" : "**×**")} | {holders} |");
        }
        Console.WriteLine();
        Console.WriteLine($"`RegenTrait.Amount` = **{RegenTrait.Amount}** ／ "
            + $"`NourishRule.Default` = **{NourishRule.Default}**。");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Q0-6 —— ガルドの `Stoic` は自前の回復に当たるか
    // ---------------------------------------------------------------------------------
    static void Q06()
    {
        Console.WriteLine("## Q0-6 —— ガルドの `Stoic` は自前の回復（`RegenTrait`）に当たるか");
        Console.WriteLine();

        string eng = Path.Combine(_root ?? ".", "BattleCore", "BattleEngine.cs");
        string src = File.Exists(eng) ? File.ReadAllText(eng) : "";
        if (src.Length == 0)
        {
            Console.WriteLine("**`BattleEngine.cs` が読めない。止める。**");
            Console.WriteLine();
            return;
        }

        // `Heal` の本体を波括弧で切る（行番号では切らない・規約 (G15)）。
        string heal = Body(src, "public void Heal(UnitState target, int amount");
        if (heal.Length == 0)
        {
            Console.WriteLine("**`BattleContext.Heal` の走査が 0 件。止める**（第117期）。");
            Console.WriteLine();
            return;
        }

        bool guard = heal.Contains("AcceptsSupport", StringComparison.Ordinal);
        bool regenUsesHeal = _body.TryGetValue(TraitId.Regen, out string? rb)
                             && rb.Contains("ctx.Heal(", StringComparison.Ordinal);
        bool stoicBlocks = _body.TryGetValue(TraitId.Stoic, out string? sb)
                           && sb.Contains("BlocksSupport => true", StringComparison.Ordinal);
        bool galdStoic = UnitCatalog.Gald.Traits.Contains(TraitId.Stoic);

        Console.WriteLine("| 鎖 | どこを見たか | 事実 |");
        Console.WriteLine("|---|---|:-:|");
        Console.WriteLine($"| (1) `RegenTrait` は `ctx.Heal` を通るか | `Traits.cs` の `RegenTrait` 本体 | {(regenUsesHeal ? "**○ 通る**" : "×")} |");
        Console.WriteLine($"| (2) `Heal` は `AcceptsSupport` で弾くか | `BattleEngine.cs` の `Heal` 本体 | {(guard ? "**○ 弾く**" : "×")} |");
        Console.WriteLine($"| (3) `Stoic` は `BlocksSupport` か | `Traits.cs` の `StoicTrait` | {(stoicBlocks ? "**○**" : "×")} |");
        Console.WriteLine($"| (4) ガルドは `Stoic` を持つか | `UnitCatalog.Gald` | {(galdStoic ? "**○**" : "×")} |");
        Console.WriteLine();

        bool hit = regenUsesHeal && guard && stoicBlocks && galdStoic;
        Console.WriteLine(hit
            ? "→ **4つとも ○。ガルドに `Regen` を載せると効果は厳密に 0 になる。**"
              + " `ctx.Heal` の入口が `!target.AcceptsSupport` で返すので、"
              + "**自分で自分を回復する経路も窓口を通る以上、支援拒否に弾かれる。**"
              + " §4-3 の載せ先候補（ガルド）は**案A では成立しない**"
              + "——指示書 Q0-6 の「当たるなら案Aの載せ先が変わる」に該当する。"
            : "→ 鎖のどこかが切れている。上の表の × の行がその場所。");
        Console.WriteLine();

        // `AcceptsSupport` を見ない耐久の窓口（＝`Stoic` に届く経路）。
        Console.WriteLine("### `Stoic` に届く耐久の窓口（`Heal` 以外）");
        Console.WriteLine();
        Console.WriteLine("| 窓口 | 本体を索けたか | `AcceptsSupport` を見るか | ガルドに届くか |");
        Console.WriteLine("|---|:-:|:-:|:-:|");
        foreach (string sig in new[] { "public void Revive(", "public void Ignite(", "public void Wound(" })
        {
            string b = Body(src, sig);
            string nm = sig.Replace("public void ", "").Replace("(", "");
            if (b.Length == 0) { Console.WriteLine($"| `{nm}` | **×** | — | — |"); continue; }
            bool sees = b.Contains("AcceptsSupport", StringComparison.Ordinal);
            Console.WriteLine($"| `{nm}` | ○ | {(sees ? "○" : "**×**")} | {(sees ? "×" : "**○ 届く**")} |");
        }
        int armorWrites = System.Text.RegularExpressions.Regex.Matches(src, @"StatusKeys\.Armor").Count;
        Console.WriteLine();
        Console.WriteLine($"`StatusKeys.Armor` が `BattleEngine.cs` に現れる箇所 **{armorWrites} 件**。"
            + " 破片は `ctx.Heal` を通らない別資源なので、"
            + "**`Stoic` の駒に唯一届く耐久**（`CLAUDE.md`「誰の助けも届かない駒に唯一届く支援」）。");
        Console.WriteLine();
    }

    /// <summary>署名から始まるメソッド1本ぶんの本体（波括弧を数える・規約 (G15)）。</summary>
    static string Body(string src, string signature)
    {
        int a = src.IndexOf(signature, StringComparison.Ordinal);
        if (a < 0) return "";
        int b = src.IndexOf('{', a);
        if (b < 0) return "";
        int depth = 0;
        for (int i = b; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}' && --depth == 0) return src.Substring(b, i - b + 1);
        }
        return "";
    }

    // ---------------------------------------------------------------------------------
    // Q0-4 —— 残存・圧勝率の現在の定義を実装から取る
    // ---------------------------------------------------------------------------------
    static void Q04()
    {
        Console.WriteLine("## Q0-4 —— 残存・圧勝率の現在の定義（`BattleSim` の実装から取る）");
        Console.WriteLine();

        string prog = Path.Combine(_root ?? ".", "BattleSim", "Program.cs");
        string src = File.Exists(prog) ? File.ReadAllText(prog) : "";
        if (src.Length == 0)
        {
            Console.WriteLine("**`Program.cs` が読めない。止める。**");
            Console.WriteLine();
            return;
        }

        // `run` の `RunSolo` を構造で切る（行番号では切らない・規約 (G15)）。
        string solo = Body(src, "RunSolo(Formation f)");
        if (solo.Length == 0)
        {
            Console.WriteLine("**`RunSolo` の走査が 0 件。止める**（第117期）。");
            Console.WriteLine();
            return;
        }

        // 実装が実際に書いている閾値を正規表現で取る（手で写さない）。
        var blow = System.Text.RegularExpressions.Regex.Match(solo, @"PlayerSurvivors >= (\d+)");
        var narrow = System.Text.RegularExpressions.Regex.Match(solo, @"PlayerSurvivors <= (\d+)");
        string bv = blow.Success ? blow.Groups[1].Value : "?";
        string nv = narrow.Success ? narrow.Groups[1].Value : "?";

        Console.WriteLine("| 列 | 実装（`run` の `RunSolo`） | 「全員生存」か |");
        Console.WriteLine("|---|---|:-:|");
        Console.WriteLine("| `残存` | `PlayerWon` の試行だけで `PlayerSurvivors` を平均 | — |");
        Console.WriteLine($"| `圧勝率` | `PlayerSurvivors >= {bv}` ÷ 勝った試行 | {(bv == "5" ? "○" : "**× 違う**")} |");
        Console.WriteLine($"| `全滅勝ち` | `PlayerSurvivors <= {nv}` ÷ 勝った試行 | — |");
        Console.WriteLine();

        Console.WriteLine($"**`圧勝率` は「生存 {bv} 体以上」であって「全員生存」ではない。**"
            + " §4-1 の「定義が『全員生存』でないなら、『全員生存で勝った割合』を別の列として足す」に該当する"
            + "——段1 では **`完全勝利`（生存数 ≧ 出撃数）を別の列として足す**。");
        Console.WriteLine();

        // 分子が分母を超えうる（召喚）ことの確認。
        int five = Presets.Compare.Count(b => b.F.Occupied().Count() == 5);
        var summoners = UnitCatalog.All.Where(d => Has(d, "ctx.Summon(")).Select(d => d.Name).ToArray();
        Console.WriteLine($"出撃数が5体の行: **{five} / {Presets.Compare.Length}**。");
        Console.WriteLine($"`ctx.Summon` を呼ぶ味方: **{summoners.Length} 枚**"
            + (summoners.Length == 0 ? "" : "（" + string.Join("・", summoners) + "）")
            + "——**`PlayerSurvivors` は戦闘中に湧いた駒も数える**ので、"
            + "**召喚を持つ行では生存数が出撃数を超えうる**。"
            + "`完全勝利` は `>=` で書くこと（`==` だと胞子が立った戦を取り落とす）。");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Q0-8 —— 過去に「時間を買う」を測っていないか
    // ---------------------------------------------------------------------------------
    static void Q08()
    {
        Console.WriteLine("## Q0-8 —— 過去に「時間を買う」を測っていないか（`design/` の走査）");
        Console.WriteLine();

        string dir = Path.Combine(_root ?? ".", "design");
        if (!Directory.Exists(dir))
        {
            Console.WriteLine("**`design/` が見つからない。止める。**");
            Console.WriteLine();
            return;
        }

        // **自分の期の文書は分母から外す**（第123期の自己参照。指示書そのものがこの語を含む）。
        var hits = new List<(string File, int Count)>();
        foreach (string f in Directory.GetFiles(dir, "*.md").OrderBy(x => x, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(f);
            if (name.StartsWith("PHASE126", StringComparison.Ordinal)) continue;   // 自分の期
            int n = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), "時間を買う").Count;
            if (n > 0) hits.Add((name, n));
        }

        Console.WriteLine($"「時間を買う」を含む `design/*.md`（**自分の期 `PHASE126*` を除く**）: **{hits.Count} 件**");
        Console.WriteLine();
        Console.WriteLine("| ファイル | 出現数 |");
        Console.WriteLine("|---|--:|");
        foreach ((string f, int n) in hits) Console.WriteLine($"| `{f}` | {n} |");
        Console.WriteLine();
        Console.WriteLine(hits.Count == 0
            ? "→ **0 件。走査が空なので止める**（語が変わった可能性がある）。"
            : "→ **同じ機構を別の名前で2度測らないこと。** 第118期（`tank`）が本体で、第117期が起点。"
              + "**この期は第118期の `Regen` 単体の残置から始める。**");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Q0-1 / Q0-2 / Q0-3 —— 誰が何ターン目に落ちるか
    // ---------------------------------------------------------------------------------
    static void Q01Q02Q03()
    {
        // 推奨6行は第123〜125期と同一（`OffturnScan.WatchRows`）。**行名を `Presets` と照合する。**
        var rows = new List<(string Name, int Stage, Formation F)>();
        var missing = new List<string>();
        foreach ((string n, int st, int _) in OffturnScan.WatchRows)
        {
            var hit = Presets.Compare.FirstOrDefault(b => b.Name == n);
            if (hit.F is null) { missing.Add(n); continue; }
            rows.Add((n, st, hit.F));
        }

        Console.WriteLine("## Q0-1 —— 誰が何ターン目に落ちるか");
        Console.WriteLine();
        Console.WriteLine($"推奨6行（第123〜125期と同じ行・同じ波）× **seed 0..{Seeds - 1}**。"
            + " 行と波は `OffturnScan.WatchRows` から引く"
            + "（代表 seed は1戦の観察用なので、ここでは帯を掃く）。");
        Console.WriteLine($"`Presets` に存在しない行 **{missing.Count} 件**"
            + (missing.Count == 0 ? "" : "（" + string.Join("・", missing) + "）")
            + $" ／ 測る行 **{rows.Count} 行**。");
        Console.WriteLine();
        if (rows.Count == 0)
        {
            Console.WriteLine("**走査が空。止める**（第117期）。");
            return;
        }

        Console.WriteLine("`生存T` は `UnitTally.LastActiveTurn`（倒れた時点のターン。生き残れば決着ターン）の平均。");
        Console.WriteLine("`落ち率` は `Deaths >= 1` の試行の割合。`決着T` は全試行の平均。");
        Console.WriteLine("**`被弾/T` = `DamageTaken` ÷ 生存T** ／ **`紙のT` = `MaxHp` ÷ `被弾/T`**"
            + "——「HP と受ける削りだけで決まるなら何ターン生きるか」（Q0-3・規約 (G7)）。");
        Console.WriteLine();

        var perRow = new List<(string Row, int Stage, double SettleT, List<UnitStat> Units)>();

        foreach ((string name, int stage, Formation f) in rows)
        {
            var seats = f.Occupied().ToArray();
            var acc = new Dictionary<string, UnitStat>(StringComparer.Ordinal);
            foreach ((int slot, UnitDef d) in seats)
                acc[d.Id] = new UnitStat { Id = d.Id, Name = d.Name, Def = d, Slot = slot };

            double turnSum = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[stage].Enemy, seed, verbose: false);
                turnSum += r.Turns;
                foreach ((int _, UnitDef d) in seats)
                {
                    if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
                    UnitStat u = acc[d.Id];
                    u.AliveSum += t.LastActiveTurn;
                    u.TakenSum += t.DamageTaken;
                    u.DealtSum += t.DamageToEnemy;
                    if (t.Deaths >= 1) u.Fell++;
                    u.N++;
                }
            }
            perRow.Add((name, stage, turnSum / Seeds, acc.Values.OrderBy(u => u.Slot).ToList()));
        }

        // --- Q0-1 の表 ---------------------------------------------------------------
        foreach ((string name, int stage, double settle, List<UnitStat> units) in perRow)
        {
            Console.WriteLine($"### {name} — 第{stage + 1}波（決着T {settle:F2}）");
            Console.WriteLine();
            Console.WriteLine("| 席 | 駒 | 分類 | HP | 攻 | 生存T | 生存T/決着T | 落ち率 "
                + "| 被弾/戦 | 被弾/T | 紙のT | 実測/紙 | 与ダメ/戦 |");
            Console.WriteLine("|---|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (UnitStat u in units)
            {
                double alive = u.N == 0 ? 0 : u.AliveSum / u.N;
                double taken = u.N == 0 ? 0 : u.TakenSum / u.N;
                double dealt = u.N == 0 ? 0 : u.DealtSum / u.N;
                double perT = alive <= 0 ? 0 : taken / alive;
                double paper = perT <= 0 ? 0 : u.Def.MaxHp / perT;
                Console.WriteLine($"| {FormationRules.SeatNames[u.Slot]} | {u.Name} | {ClassOf(u.Def)} "
                    + $"| {u.Def.MaxHp} | {u.Def.Attack} | **{alive:F2}** "
                    + $"| {(settle <= 0 ? 0 : alive / settle):F2} | {u.Fell * 100.0 / Math.Max(1, u.N):F1}% "
                    + $"| {taken:F1} | {perT:F1} | {(paper <= 0 ? "—" : paper.ToString("F2"))} "
                    + $"| {(paper <= 0 ? "—" : (alive / paper).ToString("F2"))} | {dealt:F1} |");
            }
            Console.WriteLine();
        }

        // --- Q0-2 分類ごとの比較 -------------------------------------------------------
        Console.WriteLine("## Q0-2 —— 軸の駒と素の駒の差（分類は実装から機械で引く）");
        Console.WriteLine();
        Console.WriteLine("分け方（排他ではないので優先順位を固定する: **蓄積 > 連携 > 即時**）:");
        Console.WriteLine();
        Console.WriteLine("| 分類 | 判定（`Traits.cs` の本体を走査） | 意味 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| `蓄積` | `self.AtkBonus +=` / `ApplyStack` / `SetCounter` / `AddCounter` "
            + "| 戦闘中に自分の出力を積む＝**ターン数を要求する** |");
        Console.WriteLine("| `連携` | `ctx.Whet(` / `ctx.Dull(` / `ctx.Heal(` / `ctx.Revive(` / `ctx.Summon(` "
            + "/ `SupportTargets` / `MostHurtAlly` / `LivingMembers` / `AreAdjacent` "
            + "| 他の駒を読む／書く＝**相方と時間を要求する** |");
        Console.WriteLine("| `即時` | どちらでもない | 出力は第1ターンから最大 |");
        Console.WriteLine();

        // 指示書が名指しした顔ぶれと機械の分類の食い違いを全部出す（第119期）。
        Console.WriteLine("### 指示書が名指しした顔ぶれと、機械の分類の食い違い（第119期の作法）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 攻 | HP | 指示書 | 機械 | 一致 | 当たった印（実装から） |");
        Console.WriteLine("|---|--:|--:|---|---|:-:|---|");
        var named = new (UnitDef D, string Want)[]
        {
            (UnitCatalog.Dolga, "素"), (UnitCatalog.Borg, "素"),
            (UnitCatalog.Golm,  "素"), (UnitCatalog.Gald, "素"),
            (UnitCatalog.Tome, "軸"), (UnitCatalog.Sora, "軸"), (UnitCatalog.Hagi, "軸"),
            (UnitCatalog.Sero, "軸"), (UnitCatalog.Utsu, "軸"), (UnitCatalog.Nomi, "軸"),
            (UnitCatalog.Egu,  "軸"), (UnitCatalog.Nara, "軸"),
        };
        int agree = 0;
        foreach ((UnitDef d, string want) in named)
        {
            string got = ClassOf(d);
            bool ok = want == "素" ? got == "即時" : got != "即時";
            if (ok) agree++;
            Console.WriteLine($"| {d.Name} | {d.Attack} | {d.MaxHp} | {want} | {got} "
                + $"| {(ok ? "○" : "**×**")} | {WhyOf(d)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"一致 **{agree} / {named.Length}**。"
            + "**食い違った行は機械の分類のほうを採る**（指示書の顔ぶれは手で並べたもの）。"
            + " 右端の列が食い違いの理由——**指示書は「機構が時間を要求するか」と"
            + "「打点が第1ターンから出るか」を1つの言葉（「素」）に混ぜている。**"
            + " 機械は前者しか見ていないので、**打点は即時だが機構は連携／蓄積**という駒"
            + "（ボルグ・ゴルム・ガルド）で必ず割れる。");
        Console.WriteLine();

        Console.WriteLine("### 分類ごとの生存T（推奨6行の全席をまとめる）");
        Console.WriteLine();
        Console.WriteLine("| 分類 | 席数 | 平均HP | 平均攻 | 生存T | 生存T/決着T | 落ち率 | 与ダメ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string cls in new[] { "即時", "蓄積", "連携" })
        {
            int n = 0;
            double hp = 0, atk = 0, alive = 0, ratio = 0, fell = 0, dealt = 0;
            foreach ((string _, int _, double settle, List<UnitStat> units) in perRow)
                foreach (UnitStat u in units)
                {
                    if (ClassOf(u.Def) != cls || u.N == 0) continue;
                    n++;
                    hp += u.Def.MaxHp; atk += u.Def.Attack;
                    double a = u.AliveSum / u.N;
                    alive += a;
                    ratio += settle <= 0 ? 0 : a / settle;
                    fell += u.Fell * 100.0 / u.N;
                    dealt += u.DealtSum / u.N;
                }
            if (n == 0) { Console.WriteLine($"| {cls} | 0 | — | — | — | — | — | — |"); continue; }
            Console.WriteLine($"| {cls} | {n} | {hp / n:F0} | {atk / n:F1} | **{alive / n:F2}** "
                + $"| {ratio / n:F2} | {fell / n:F1}% | {dealt / n:F1} |");
        }
        Console.WriteLine();

        // --- Q0-3 HP で説明が付くか ----------------------------------------------------
        // --- 第2の切り方: 攻撃力（＝第1ターンから出ている打点）。指示書の「攻撃力・HP から」に対応 ---
        int line = AtkLine();
        Console.WriteLine("### 第2の切り方 —— 攻撃力（第1ターンから出ている打点）");
        Console.WriteLine();
        Console.WriteLine($"線は **`UnitCatalog.All` の攻撃力の中央値 = {line}**（手で決めない）。"
            + " 上の分類が見ているのは「機構が時間を要求するか」で、**打点の即時性は見ていない**"
            + "——指示書の「素の高出力」はこちらの軸である。");
        Console.WriteLine();
        Console.WriteLine("| 打点 | 席数 | 平均攻 | 平均HP | 生存T | 生存T/決着T | 落ち率 | 与ダメ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (bool hi in new[] { true, false })
        {
            int n = 0;
            double hp = 0, atk = 0, alive = 0, ratio = 0, fell = 0, dealt = 0;
            foreach ((string _, int _, double settle, List<UnitStat> units) in perRow)
                foreach (UnitStat u in units)
                {
                    if (u.N == 0 || (u.Def.Attack >= line) != hi) continue;
                    n++;
                    hp += u.Def.MaxHp; atk += u.Def.Attack;
                    double a = u.AliveSum / u.N;
                    alive += a;
                    ratio += settle <= 0 ? 0 : a / settle;
                    fell += u.Fell * 100.0 / u.N;
                    dealt += u.DealtSum / u.N;
                }
            string lbl = hi ? $"攻 ≧ {line}" : $"攻 < {line}";
            if (n == 0) { Console.WriteLine($"| {lbl} | 0 | — | — | — | — | — | — |"); continue; }
            Console.WriteLine($"| {lbl} | {n} | {atk / n:F1} | {hp / n:F0} | **{alive / n:F2}** "
                + $"| {ratio / n:F2} | {fell / n:F1}% | {dealt / n:F1} |");
        }
        Console.WriteLine();

        // --- 2×2: 打点の即時性 × 機構の依存 ------------------------------------------------
        Console.WriteLine("### 2×2 —— 打点の即時性 × 機構が時間を要求するか");
        Console.WriteLine();
        Console.WriteLine("**この2つを分けると、指示書の「軸が素の駒に負けている」が"
            + "どちらの軸の話なのかが読める。**");
        Console.WriteLine();
        Console.WriteLine("| | 席数 | 生存T | 落ち率 | 与ダメ/戦 | 顔ぶれ |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        foreach (bool hi in new[] { true, false })
            foreach (bool needsTime in new[] { false, true })
            {
                int n = 0;
                double alive = 0, fell = 0, dealt = 0;
                var who = new List<string>();
                foreach ((string _, int _, double _, List<UnitStat> units) in perRow)
                    foreach (UnitStat u in units)
                    {
                        if (u.N == 0) continue;
                        if ((u.Def.Attack >= line) != hi) continue;
                        if ((ClassOf(u.Def) != "即時") != needsTime) continue;
                        n++;
                        alive += u.AliveSum / u.N;
                        fell += u.Fell * 100.0 / u.N;
                        dealt += u.DealtSum / u.N;
                        if (!who.Contains(u.Name)) who.Add(u.Name);
                    }
                string lbl = (hi ? $"攻 ≧ {line}" : $"攻 < {line}")
                           + " × " + (needsTime ? "機構が時間を要求する" : "**要求しない**");
                if (n == 0) { Console.WriteLine($"| {lbl} | 0 | — | — | — | — |"); continue; }
                Console.WriteLine($"| {lbl} | {n} | **{alive / n:F2}** | {fell / n:F1}% "
                    + $"| {dealt / n:F1} | {string.Join("・", who)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-3 —— 落ちる原因は HP か席か狙われ方か");
        Console.WriteLine();

        var pts = new List<(double Hp, double Alive, double PerT, double Paper, int Slot, string Name)>();
        foreach ((string _, int _, double _, List<UnitStat> units) in perRow)
            foreach (UnitStat u in units)
            {
                if (u.N == 0) continue;
                double a = u.AliveSum / u.N;
                double perT = a <= 0 ? 0 : (u.TakenSum / u.N) / a;
                double paper = perT <= 0 ? 0 : u.Def.MaxHp / perT;
                pts.Add((u.Def.MaxHp, a, perT, paper, u.Slot, u.Name));
            }

        var withPaper = pts.Where(p => p.Paper > 0).ToList();
        Console.WriteLine($"標本 **{pts.Count} 席**（うち被弾のあった席 **{withPaper.Count}**）。");
        Console.WriteLine();
        Console.WriteLine("| 相関（推奨6行の全席） | r |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| 最大HP ↔ 生存T | **{R(pts.Select(p => p.Hp), pts.Select(p => p.Alive)):F3}** |");
        Console.WriteLine($"| 被弾/T ↔ 生存T | **{R(pts.Select(p => p.PerT), pts.Select(p => p.Alive)):F3}** |");
        Console.WriteLine($"| 紙のT ↔ 生存T | **{R(withPaper.Select(p => p.Paper), withPaper.Select(p => p.Alive)):F3}** |");
        Console.WriteLine();

        Console.WriteLine("### 席（前列 / 中列 / 後列）ごと");
        Console.WriteLine();
        Console.WriteLine("| 列 | 席数 | 平均HP | 生存T | 被弾/T | 紙のT |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (Row rw in new[] { Row.Front, Row.Mid, Row.Back })
        {
            var g = pts.Where(p => FormationRules.RowOf(p.Slot) == rw).ToList();
            if (g.Count == 0) { Console.WriteLine($"| {rw} | 0 | — | — | — | — |"); continue; }
            var gp = g.Where(p => p.Paper > 0).ToList();
            Console.WriteLine($"| {rw} | {g.Count} | {g.Average(p => p.Hp):F0} "
                + $"| **{g.Average(p => p.Alive):F2}** | {g.Average(p => p.PerT):F1} "
                + $"| {(gp.Count == 0 ? "—" : gp.Average(p => p.Paper).ToString("F2"))} |");
        }
        Console.WriteLine();
    }

    // ==================================================================================
    // 段2 —— 時間を買う機構をローカル台で測る（3案 × 対照）
    //
    // **台は第117期のものをそのまま使う**（前1 = 死なない / 中央 = 育つ / 前3・後1・後3 = 土台3枚）。
    // **`Presets` も `EnemyCatalog.Stages` も `UnitCatalog.All` も1文字も触らない。**
    // 機構は**中央（育つ駒）に載せる**——§5-2 の判定が「育つ駒の生存T」なので、
    // 載せ先が育つ駒でなければ判定そのものが成立しない。
    // ==================================================================================
    const int Half = 3;                              // 前半3T / 後半3T（第117期と同じ）
    const int MinTurns = 2 * Half;                   // 前半と後半が重ならない最短の戦
    static readonly int[] Waves = { 1, 2, 3, 4 };    // 第2〜5波（規約 (G10)）

    static IReadOnlyList<EnemyCatalog.Stage> Stages => EnemyCatalog.Stages;

    static UnitDef[] _grow = Array.Empty<UnitDef>();
    static UnitDef[] _hold = Array.Empty<UnitDef>();
    static UnitDef[] _fill = Array.Empty<UnitDef>();

    /// <summary>版。<b>V0 が対照</b>（何も載せない）。</summary>
    enum Ver { V0, A, B, C }

    static readonly (Ver V, string Name, TraitId? Extra)[] Versions =
    {
        (Ver.V0, "V0 対照",     null),
        (Ver.A,  "案A 自己回復", TraitId.Regen),
        (Ver.B,  "案B 猶予",     TraitId.Reprieve),
        (Ver.C,  "案C 育ち耐性", TraitId.Tempered),
    };

    /// <summary>土台を第117期と同じ規則で組む（実装から引く。<b>空なら止める</b>）。</summary>
    static bool InitStand()
    {
        _grow = new[] { UnitCatalog.Mudo, UnitCatalog.Kado, UnitCatalog.Yomi, UnitCatalog.Utsu };
        _hold = new[] { UnitCatalog.Gald, UnitCatalog.Vel, UnitCatalog.Mug, UnitCatalog.Golm };
        var eight = _grow.Concat(_hold).ToArray();

        // 埋め草は第117期／第118期と同じ規則（攻撃力を書かない・読まない・巻き込まない・
        // 移動を供給しない・紙でない）に、**回復しない**を足したもの——案A と交絡するため。
        var pool = new List<UnitDef>();
        foreach (UnitDef d in UnitCatalog.All)
        {
            if (eight.Contains(d)) continue;
            if (Has(d, "ctx.Whet(", "ctx.Dull(")) continue;                             // 攻撃力を書く
            if (d.Traits.Any(t => t is TraitId.Overload or TraitId.Perverse)) continue;  // 読む
            if (Has(d, "isFriendlyFire: true")) continue;                                // 巻き込む
            if (Has(d, "SwapSlots", "HaulOutPair", "FallBack", "ctx.Summon")) continue;  // 移動
            if (d.MaxHp < 50) continue;                                                  // 紙
            if (Has(d, "ctx.Heal(", "ctx.Revive(")) continue;                            // 回復
            if (Has(d, "self.AtkBonus +=")) continue;                                    // 自己強化
            pool.Add(d);
        }
        pool.Sort(delegate (UnitDef a, UnitDef b)
        {
            int c = b.Attack.CompareTo(a.Attack);
            return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
        });
        _fill = pool.Take(3).ToArray();
        return _fill.Length == 3;
    }

    /// <summary>前1 = 死なない ／ 中央 = 育つ ／ 前3・後1・後3 = 土台3枚（第117期と同じ席）。</summary>
    static Formation Bench(UnitDef grow, UnitDef hold) => Formation.Build(
        front1: hold, front3: _fill[0], center: grow, back1: _fill[1], back3: _fill[2]);

    /// <summary>その駒に札を1枚足した版（<b>数値は1つも変えない</b>）。</summary>
    static UnitDef With(UnitDef d, TraitId? extra) => extra is null ? d : new UnitDef
    {
        Id = d.Id + "_" + extra.Value,
        Name = d.Name,
        MaxHp = d.MaxHp,
        Attack = d.Attack,
        Speed = d.Speed,
        Pattern = d.Pattern,
        Traits = d.Traits.Concat(new[] { extra.Value }).ToArray(),
        Actions = d.Actions,
    };

    sealed class Agg
    {
        public int N, Ok, Wins, Blow, Perfect, GrowFell;
        public long Early, Late, GrowEarly, GrowLate;
        public double TurnSum, GrowAlive, SurvSum, GrowAtkMax;
        public double SurvAll;   // 第82期の `残存度`: **全試行**の生存数（負けは 0）。分母が版で動かない
        public long OverTurns, AliveTurns;   // 閾値を越えていたターン / ターン頭に生きていたターン
    }

    static void One(Formation f, Formation enemy, int seed, string growId, int baseAtk, Agg a)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false, boss: new BossRule(true));
        int L = Math.Max(1, r.Turns);
        a.N++;
        a.TurnSum += L;
        if (r.PlayerWon)
        {
            a.Wins++;
            a.SurvSum += r.PlayerSurvivors;
            a.SurvAll += r.PlayerSurvivors;   // 負けた試行は 0 のまま（分母は全試行）
            if (r.PlayerSurvivors >= 4) a.Blow++;
            if (r.PlayerSurvivors >= f.Occupied().Count()) a.Perfect++;
        }

        bool ok = L >= MinTurns;
        if (ok) a.Ok++;

        // 味方全体の与ダメ（前半3T / 後半3T）。
        // **戦ごとの比を平均しない。合計どうしを割る**（第117期と同じ）。
        if (ok)
        {
            var team = new int[BattleEngine.MaxTurns + 2];
            foreach ((int _, UnitDef d) in f.Occupied())
            {
                if (!r.TallyByUnit.TryGetValue(d.Id, out UnitTally? t)) continue;
                int[]? by = t.BossDmgByTurn;
                if (by is null) continue;
                for (int i = 0; i < team.Length && i < by.Length; i++) team[i] += by[i];
            }
            for (int t = 1; t <= Math.Min(Half, L); t++) a.Early += team[t];
            for (int t = Math.Max(1, L - Half + 1); t <= L && t < team.Length; t++) a.Late += team[t];
        }

        if (r.TallyByUnit.TryGetValue(growId, out UnitTally? g))
        {
            a.GrowAlive += g.BossLastAliveTurn;
            if (g.Deaths >= 1) a.GrowFell++;
            int[] atk = g.BossAtkByTurn ?? Array.Empty<int>();
            int mx = 0;
            for (int t = 1; t <= L && t < atk.Length; t++)
            {
                if (atk[t] <= 0) continue;          // 倒れた後のターンは数えない
                if (atk[t] > mx) mx = atk[t];
                a.AliveTurns++;
                // **到達（最大）ではなく到着（そのターン効いていたか）**（第115期）。
                if (atk[t] - baseAtk > TemperedTrait.Threshold) a.OverTurns++;
            }
            a.GrowAtkMax += mx;
            // 育つ駒**だけ**の傾き（§4-2）。
            int[]? by = g.BossDmgByTurn;
            if (ok && by is not null)
            {
                for (int t = 1; t <= Math.Min(Half, L) && t < by.Length; t++) a.GrowEarly += by[t];
                for (int t = Math.Max(1, L - Half + 1); t <= L && t < by.Length; t++) a.GrowLate += by[t];
            }
        }
    }

    static double Slope(long early, long late, int ok)
        => ok == 0 || early <= 0 ? 0 : late / (double)early;

    static void RunTables()
    {
        if (!InitStand())
        {
            Console.WriteLine("time: 埋め草が3枚そろわない。**走査が空なら止める**（第117期）。");
            return;
        }

        Console.WriteLine("# 第126期 段2 —— 時間を買う機構をローカル台で測る");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 time run` の出力。");
        Console.WriteLine("**台は第117期のもの**（前1 = 死なない ／ 中央 = 育つ ／ 前3・後1・後3 = 土台3枚）。");
        Console.WriteLine($"土台3枚は実装から引いた: **{string.Join("・", _fill.Select(d => d.Name))}**"
            + "（攻撃力を書かない・読まない・巻き込まない・移動を供給しない・回復しない・"
            + "自己強化しない・HP 50 以上のうち攻撃力の上位3枚）。");
        Console.WriteLine($"育つ4枚 × 死なない4枚 = **16 組** × 4版 × 第2〜5波 × seed 0..{Seeds - 1}。");
        Console.WriteLine();
        Console.WriteLine("**機構は中央（育つ駒）に載せる**——§5-2 の判定が「育つ駒の生存T」なので、"
            + "載せ先が育つ駒でなければ判定が成立しない。**数値は1つも変えず、札を1枚足すだけ。**");
        Console.WriteLine();
        Console.WriteLine("| 版 | 中身 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| V0 対照 | 何も載せない |");
        Console.WriteLine($"| 案A 自己回復 | `Regen`——毎ターン自分の HP を **{RegenTrait.Amount}** 戻す"
            + "（`ctx.Heal` を通る＝渇きが止める） |");
        Console.WriteLine("| 案B 猶予 | `Reprieve`——致死の一撃を**1戦に1度だけ** HP1 で耐える |");
        Console.WriteLine($"| 案C 育ち耐性 | `Tempered`——`AtkBonus` が **{TemperedTrait.Threshold}** を越えた分 "
            + $"**{TemperedTrait.PerPercent}** につき被ダメ −1%（上限 **{TemperedTrait.MaxPercent}%**） |");
        Console.WriteLine();
        Console.WriteLine("**3案とも代金を付けていない**（受け入れ条件 A6・§4-2 の注意）——素の効き方を先に測る。");
        Console.WriteLine();

        var cell = new Dictionary<(int, int, Ver), Agg>();
        for (int gi = 0; gi < _grow.Length; gi++)
            for (int hi = 0; hi < _hold.Length; hi++)
                foreach ((Ver v, string _, TraitId? extra) in Versions)
                {
                    UnitDef grow = With(_grow[gi], extra);
                    Formation f = Bench(grow, _hold[hi]);
                    var a = new Agg();
                    foreach (int w in Waves)
                        for (int seed = 0; seed < Seeds; seed++)
                            One(f, Stages[w].Enemy, seed, grow.Id, _grow[gi].Attack, a);
                    cell[(gi, hi, v)] = a;
                }

        // --- 表A: 組ごとの育つ駒の生存T ------------------------------------------------
        Console.WriteLine("## 表A —— 育つ駒の生存T（§5-2 の条件1。線は V0 比 **+1.0T**）");
        Console.WriteLine();
        Console.WriteLine("| 育つ駒 | 死なない駒 | V0 | 案A | Δ | 案B | Δ | 案C | Δ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int gi = 0; gi < _grow.Length; gi++)
            for (int hi = 0; hi < _hold.Length; hi++)
            {
                Agg z = cell[(gi, hi, Ver.V0)];
                double b0 = z.GrowAlive / z.N;
                var line = new List<string>();
                foreach (Ver v in new[] { Ver.A, Ver.B, Ver.C })
                {
                    Agg a = cell[(gi, hi, v)];
                    double x = a.GrowAlive / a.N;
                    double d = x - b0;
                    line.Add($"{x:F2} | {(d >= 1.0 ? $"**+{d:F2}**" : d.ToString("+0.00;-0.00;0.00"))}");
                }
                Console.WriteLine($"| {_grow[gi].Name} | {_hold[hi].Name} | {b0:F2} | {string.Join(" | ", line)} |");
            }
        Console.WriteLine();

        // --- 表A' : 門（案C の入力は実在するか）------------------------------------------
        //
        // **案C は `AtkBonus` を読む。** 読む相手が無ければ「効かなかった」ではなく「測っていない」
        // （第61期）。`到達攻` は `BossAtkByTurn` の最大＝ターン頭の `CurrentAttack` の最大なので、
        // **素の攻撃力と一致していたら `AtkBonus` は一度も動いていない。**
        Console.WriteLine("## 表A' —— 門: 案C の入力（`AtkBonus`）は実在するか");
        Console.WriteLine();
        Console.WriteLine("`到達攻` は V0 でのターン頭 `CurrentAttack` の最大（`BossAtkByTurn`）。");
        Console.WriteLine("**素の攻撃力と一致していたら `AtkBonus` は一度も動いていない**"
            + $"——案C の閾値は `AtkBonus > {TemperedTrait.Threshold}` なので、そこには1度も届かない。");
        Console.WriteLine();
        Console.WriteLine("| 育つ駒 | 素の攻 | 到達攻(V0) | 伸び | 届きうるか | **閾値超のターン割合** | 案C の Δ生存T |");
        Console.WriteLine("|---|--:|--:|--:|:-:|--:|--:|");
        for (int gi = 0; gi < _grow.Length; gi++)
        {
            double mx = 0, dA = 0;
            int n = 0;
            long over = 0, alv = 0;
            for (int hi = 0; hi < _hold.Length; hi++)
            {
                Agg z = cell[(gi, hi, Ver.V0)], c = cell[(gi, hi, Ver.C)];
                mx += z.GrowAtkMax / z.N;
                dA += c.GrowAlive / c.N - z.GrowAlive / z.N;
                over += c.OverTurns; alv += c.AliveTurns;   // **案C の版で数える**（実際に効いた側）
                n++;
            }
            mx /= n; dA /= n;
            double pct = alv == 0 ? 0 : over * 100.0 / alv;
            double grow = mx - _grow[gi].Attack;
            Console.WriteLine($"| {_grow[gi].Name} | {_grow[gi].Attack} | {mx:F1} | {grow:+0.0;-0.0;0.0} "
                + $"| {(grow > TemperedTrait.Threshold ? "**○**" : "**× 届かない**")} "
                + $"| **{pct:F1}%** | {dA:+0.00;-0.00;0.00} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **`到達攻` が素の攻撃力と同じ駒では、案C は1度も発火していない。**"
            + " その行の `Δ生存T = 0.00` は「効かなかった」ではなく**「測っていない」**（第61期）。");
        Console.WriteLine();

        EmitVersionSummary(cell);
    }

    static void EmitVersionSummary(Dictionary<(int, int, Ver), Agg> cell)
    {
        Console.WriteLine("## 表B —— 版ごとの通算（16 組をまとめる）");
        Console.WriteLine();
        Console.WriteLine("`傾き` は後半3T の与ダメ ÷ 前半3T の与ダメ（分母は **L ≥ 6 の戦だけ**・第117期と同じ。"
            + "**戦ごとの比を平均せず、合計どうしを割る**）。`育つ駒の傾き` は同じ計算をその駒の与ダメだけで。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 育つ駒の生存T | Δ | 落ち率 | 到達攻 | 傾き | 育つ駒の傾き "
            + "| 決着T | 勝率 | 残存 | **残存度** | 圧勝率 | 完全勝利 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        double baseAlive = 0;
        foreach ((Ver v, string name, TraitId? _) in Versions)
        {
            int n = 0, ok = 0, wins = 0, blow = 0, perf = 0, fell = 0;
            long e = 0, l = 0, ge = 0, gl = 0;
            double ga = 0, ts = 0, ss = 0, mx = 0, sa = 0;
            for (int gi = 0; gi < _grow.Length; gi++)
                for (int hi = 0; hi < _hold.Length; hi++)
                {
                    Agg a = cell[(gi, hi, v)];
                    n += a.N; ok += a.Ok; wins += a.Wins; blow += a.Blow; perf += a.Perfect; fell += a.GrowFell;
                    e += a.Early; l += a.Late; ge += a.GrowEarly; gl += a.GrowLate;
                    ga += a.GrowAlive; ts += a.TurnSum; ss += a.SurvSum; mx += a.GrowAtkMax;
                    sa += a.SurvAll;
                }
            double alive = ga / n;
            if (v == Ver.V0) baseAlive = alive;
            Console.WriteLine($"| {name} | **{alive:F2}** "
                + $"| {(v == Ver.V0 ? "—" : (alive - baseAlive).ToString("+0.00;-0.00;0.00"))} "
                + $"| {fell * 100.0 / n:F1}% | {mx / n:F1} | {Slope(e, l, ok):F2} | {Slope(ge, gl, ok):F2} "
                + $"| {ts / n:F2} | {wins * 100.0 / n:F1}% "
                + (wins == 0 ? "| — | — | — | — |"
                             : $"| {ss / wins:F2} | **{sa / n:F2}** | {blow * 100.0 / wins:F1}% "
                               + $"| **{perf * 100.0 / wins:F1}%** |"));
        }
        Console.WriteLine();

        Console.WriteLine("## 表C —— §5-2 の採否（条件1と2の両方を満たした案だけが段3 へ進む）");
        Console.WriteLine();
        Console.WriteLine("| 案 | 条件1 生存T +1.0T 以上 | 条件2 育つ駒の傾きが V0 超 | 条件3 残存が上がる | 段3 へ |");
        Console.WriteLine("|---|:-:|:-:|:-:|:-:|");
        double v0Alive = 0, v0Slope = 0, v0Surv = 0;
        foreach ((Ver v, string name, TraitId? _) in Versions)
        {
            int n = 0, ok = 0, wins = 0;
            long ge = 0, gl = 0;
            double ga = 0, ss = 0;
            for (int gi = 0; gi < _grow.Length; gi++)
                for (int hi = 0; hi < _hold.Length; hi++)
                {
                    Agg a = cell[(gi, hi, v)];
                    n += a.N; ok += a.Ok; wins += a.Wins;
                    ge += a.GrowEarly; gl += a.GrowLate; ga += a.GrowAlive; ss += a.SurvSum;
                }
            double alive = ga / n, sl = Slope(ge, gl, ok), sv = wins == 0 ? 0 : ss / wins;
            if (v == Ver.V0) { v0Alive = alive; v0Slope = sl; v0Surv = sv; continue; }
            bool c1 = alive - v0Alive >= 1.0, c2 = sl > v0Slope, c3 = sv > v0Surv;
            Console.WriteLine($"| {name} | {(c1 ? "**○**" : "×")} ({(alive - v0Alive).ToString("+0.00;-0.00;0.00")}) "
                + $"| {(c2 ? "**○**" : "×")} ({sl:F2} 対 {v0Slope:F2}) "
                + $"| {(c3 ? "○" : "×")} ({sv:F2} 対 {v0Surv:F2}) "
                + $"| {(c1 && c2 ? "**進める**" : "進めない")} |");
        }
        Console.WriteLine();
        Console.WriteLine("**条件1 と 条件2 の両方を満たした案だけを段3 へ進める**（§5-2）。"
            + "満たす案が複数なら最も単純なものを採る。**1つも満たさなければ採らない。**");
        Console.WriteLine();
        Console.WriteLine("> **条件3 の `残存` は版をまたぐと分母が動く**（規約 (G12)）。"
            + "`残存` の分母は「勝った試行」なので、**勝率が上がった版では"
            + "「今まで負けていた難しい戦」が分母に入ってくる**——"
            + "生存数が減ったのではなく、**ぎりぎりの勝ちが分母に足された**のかもしれない。"
            + " 表B の **`残存度`**（第82期）は**全試行**が分母で負けを 0 と数えるので、"
            + "**版をまたいでも分母が動かない。** "
            + "**条件3 は指示書どおり `残存` で判定した**（第64期・結果を見てから基準を変えない）が、"
            + "**読むときは `残存度` を併せて見ること。**");
        Console.WriteLine();
    }

    static double R(IEnumerable<double> xs, IEnumerable<double> ys)
    {
        double[] a = xs.ToArray(), b = ys.ToArray();
        int n = Math.Min(a.Length, b.Length);
        if (n < 2) return 0;
        double ma = 0, mb = 0;
        for (int i = 0; i < n; i++) { ma += a[i]; mb += b[i]; }
        ma /= n; mb /= n;
        double sa = 0, sb = 0, sab = 0;
        for (int i = 0; i < n; i++)
        {
            double da = a[i] - ma, db = b[i] - mb;
            sa += da * da; sb += db * db; sab += da * db;
        }
        return sa <= 0 || sb <= 0 ? 0 : sab / Math.Sqrt(sa * sb);
    }

    sealed class UnitStat
    {
        public string Id = "", Name = "";
        public UnitDef Def = null!;
        public int Slot, Fell, N;
        public double AliveSum, TakenSum, DealtSum;
    }
}
