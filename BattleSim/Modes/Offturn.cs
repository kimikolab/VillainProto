using BattleCore;
using static Common;

// =====================================================================================
// offturn モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "offturn")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 offturn
// =====================================================================================

static class OffturnDiag
{
// offturn モード: **手番の外を見せる期**（第125期）。
//
// この期は採否の判定を1つも持たない（第123・124期と同じ性格）。盤面の規則は1本も足しておらず、
// 足したのは表示専用のイベント1種（`BattleEventKind.Intercept`）と、誰も読まない計数2本だけ。
//
// **走査は件数を出し、0件を異常として止める**（第117期「引けなかったときと『該当なし』の区別」）。
//
//     dotnet run --project BattleSim -c Release 0 offturn phase0            # Q0-1〜Q0-9（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 offturn check             # A3 / A4 と実戦の発火
//     dotnet run --project BattleSim -c Release 0 offturn tempo <旧Main.cs>  # A6（再生の総尺の前後）
public static void Run(string[] args, int stageIndex)
{
    string ofMode = args.Length > 2 ? args[2] : "phase0";

    // リポジトリの根。`docs/balance.md` を目印にたどる（`watch` と同じ引き方）。
    string? ofRoot = Directory.GetCurrentDirectory();
    while (ofRoot != null && !File.Exists(Path.Combine(ofRoot, "docs", "balance.md")))
        ofRoot = Path.GetDirectoryName(ofRoot);
    if (ofRoot == null)
    {
        Console.WriteLine("**走査が空**: リポジトリの根を引けなかった（`docs/balance.md` が見つからない）。ここで止める。");
        return;
    }
    string ofEnginePath = Path.Combine(ofRoot, "BattleCore", "BattleEngine.cs");
    string ofTraitsPath = Path.Combine(ofRoot, "BattleCore", "Traits.cs");
    string ofModelsPath = Path.Combine(ofRoot, "BattleCore", "Models.cs");
    string ofDemoPath = Path.Combine(ofRoot, "DemoApp", "Main.cs");
    foreach (string p in new[] { ofEnginePath, ofTraitsPath, ofModelsPath, ofDemoPath })
        if (!File.Exists(p))
        {
            Console.WriteLine("**走査が空**: `" + p + "` が無い。ここで止める。");
            return;
        }
    string ofEngine = File.ReadAllText(ofEnginePath);
    string ofTraits = File.ReadAllText(ofTraitsPath);

    var ofFoeDefs = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(x => x.Def))
        .GroupBy(d => d.Id).Select(g => g.First()).ToList();
    string OfNameOf(string id)
        => UnitCatalog.All.Concat(ofFoeDefs).FirstOrDefault(d => d.Id == id)?.Name ?? id;

    // 特性 → 保持者。
    var ofTraitOwners = new Dictionary<TraitId, List<UnitDef>>();
    foreach (UnitDef d in UnitCatalog.All)
        foreach (TraitId t in d.Traits)
        {
            if (!ofTraitOwners.TryGetValue(t, out var l)) ofTraitOwners[t] = l = new List<UnitDef>();
            l.Add(d);
        }
    string OfOwners(TraitId id)
        => ofTraitOwners.TryGetValue(id, out var l) ? string.Join(" / ", l.Select(d => d.Name)) : "**味方に0体**";

    // 特性クラス名 → TraitId（reflection。手で並べない）。
    var ofClassToId = new Dictionary<string, TraitId>(StringComparer.Ordinal);
    foreach (TraitId id in Enum.GetValues<TraitId>())
    {
        IReadOnlyList<Trait> resolved;
        try { resolved = TraitCatalog.Resolve(new[] { id }); } catch { continue; }
        foreach (Trait t in resolved) ofClassToId[t.GetType().Name] = id;
    }

    // ----------------------------------------------------------------------
    // Q0-1 —— 介入の鎖。`SelectTargetChain` の本文から機械で引く。
    // ----------------------------------------------------------------------
    string ofChain = OffturnScan.Body(ofEngine, "private UnitState? SelectTargetChain(");
    if (ofChain.Length == 0)
    {
        Console.WriteLine("**走査が空**: `SelectTargetChain` の本文を切り出せなかった。ここで止める。");
        return;
    }
    var ofChainSites = System.Text.RegularExpressions.Regex
        .Matches(ofChain, @"EmitIntercept\(\s*(\w+)\s*,\s*(\w+)\s*,\s*InterceptLabels\.(\w+)\s*\)")
        .Select(m => (Guard: m.Groups[1].Value, Target: m.Groups[2].Value, Field: m.Groups[3].Value))
        .ToList();
    var ofGuardPicks = System.Text.RegularExpressions.Regex
        .Matches(ofChain, @"NoteGuardPick\(GuardKind\.(\w+)")
        .Select(m => m.Groups[1].Value).ToList();
    string OfLabelOf(string field)
        => typeof(InterceptLabels).GetField(field)?.GetRawConstantValue() as string ?? field;

    // ----------------------------------------------------------------------
    // Q0-3 —— `ctx.Reaction` / `ctx.Interrupt` に包まれた段。
    // ----------------------------------------------------------------------
    var ofWrapped = new List<(string Wrap, string Class)>();
    foreach (System.Text.RegularExpressions.Match m in
             System.Text.RegularExpressions.Regex.Matches(ofTraits, @"ctx\.(Reaction|Interrupt)\("))
        ofWrapped.Add((m.Groups[1].Value, OffturnScan.EnclosingTrait(ofTraits, m.Index)));

    // ----------------------------------------------------------------------
    // Q0-4 —— `relayed: true` を渡す呼び出し口（肩代わりの中継）。
    // ----------------------------------------------------------------------
    var ofRelaySites = new List<string>();
    foreach (System.Text.RegularExpressions.Match m in
             System.Text.RegularExpressions.Regex.Matches(ofEngine, @"relayed:\s*true"))
    {
        int from = Math.Max(0, m.Index - 1800);
        var near = System.Text.RegularExpressions.Regex.Matches(
            ofEngine.Substring(from, m.Index - from), @"TraitId\.(\w+)").ToList();
        ofRelaySites.Add(near.Count == 0 ? "—" : near[near.Count - 1].Groups[1].Value);
    }

    // ----------------------------------------------------------------------
    // Q0-5 —— `OnTurnStart` を上書きしている特性。
    // ----------------------------------------------------------------------
    var ofTurnStartIds = new List<TraitId>();
    foreach (TraitId id in Enum.GetValues<TraitId>())
    {
        IReadOnlyList<Trait> resolved;
        try { resolved = TraitCatalog.Resolve(new[] { id }); } catch { continue; }
        foreach (Trait t in resolved)
        {
            var mi = t.GetType().GetMethod("OnTurnStart");
            if (mi is not null && mi.DeclaringType != typeof(Trait) && !ofTurnStartIds.Contains(id))
                ofTurnStartIds.Add(id);
        }
    }

    // ----------------------------------------------------------------------
    // 推奨6行（第124期の観察ログと同じ6行・同じ seed）。行名は `Presets` と完全一致で照合する。
    // ----------------------------------------------------------------------
    var ofAll = CompareBuilds().Concat(CrossBuilds()).ToArray();
    var ofByName = new Dictionary<string, Formation>(StringComparer.Ordinal);
    foreach (var (n, f) in ofAll) ofByName[n] = f;
    var ofRows = new List<(string Name, Formation F, int Stage, int Seed)>();
    var ofRowMissing = new List<string>();
    foreach (var (n, st, sd) in OffturnScan.WatchRows)
    {
        if (ofByName.TryGetValue(n, out Formation? f)) ofRows.Add((n, f, st, sd));
        else ofRowMissing.Add(n);
    }

    // ======================================================================
    // phase0 —— Q0-1〜Q0-9。**戦闘0回。**
    // ======================================================================
    if (ofMode == "phase0")
    {
        Console.WriteLine("# 手番の外の地図 —— Phase 0（第125期・`offturn phase0`）");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 offturn phase0` の出力。**戦闘0回。**");
        Console.WriteLine("すべて実装から引いている（手で並べた表は1つも無い）。走査が空なら止める。");
        Console.WriteLine();

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1. 介入の鎖の全段");
        Console.WriteLine();
        int ofKinds = ofChainSites.Select(x => OfLabelOf(x.Field)).Distinct(StringComparer.Ordinal).Count();
        Console.WriteLine("`SelectTargetChain` の本文 **" + ofChain.Length + " 字**から引いた。"
            + "差し替えの段 **" + ofChainSites.Count + " 箇所** ／ 段の名前 **" + ofKinds + " 種**"
            + " ／ `InterceptLabels.All` **" + InterceptLabels.All.Length + " 種**。");
        Console.WriteLine();
        Console.WriteLine("| # | 段（鎖の順） | 割り込む駒 | 本来の標的 | `BattleEvent` | 保持者（味方） |");
        Console.WriteLine("|--:|---|---|---|:-:|---|");
        for (int i = 0; i < ofChainSites.Count; i++)
        {
            var ofSite = ofChainSites[i];
            string owners = ofSite.Field switch
            {
                "RearGuard" => OfOwners(TraitId.RearGuard),
                "Guardian" => OfOwners(TraitId.Guardian),
                "Martyr" => OfOwners(TraitId.Martyr),
                "ThornGuard" => OfOwners(TraitId.ThornGuard),
                _ => "—（`StatusKeys.Marked` を engine が読む）",
            };
            Console.WriteLine("| " + (i + 1) + " | " + OfLabelOf(ofSite.Field) + " | `" + ofSite.Guard + "` | `" + ofSite.Target
                + "` | `Intercept` | " + owners + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**`NoteGuardPick` が数える段**（第120期・診断専用）は " + ofGuardPicks.Count + " 箇所"
            + "（" + string.Join(" / ", ofGuardPicks.Distinct().Select(g => "`" + g + "`")) + "）"
            + "——鎖の外の巨躯を含み、**標の段を数えていない。**");
        Console.WriteLine();
        Console.WriteLine("> **第124期まではこの鎖の全段が `Log` の文字列にしか出ていなかった**（第123期 (iii)）。");
        Console.WriteLine("> 駒は1ミリも動かないので、画面では「庇った」ではなく「庇った駒が殴られた」としか見えない。");
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2. 介入の計数");
        Console.WriteLine();
        var ofTallyFields = typeof(UnitTally).GetFields()
            .Where(f => f.FieldType == typeof(int)).Select(f => f.Name).ToArray();
        Console.WriteLine("`UnitTally` の int フィールド **" + ofTallyFields.Length + " 本**。");
        Console.WriteLine();
        Console.WriteLine("| フィールド | 有無 | 意味 |");
        Console.WriteLine("|---|:-:|---|");
        foreach (string w in new[] { "Intercepts", "Shouldered", "Swallowed", "DamageTaken", "TakenFromAlly" })
            Console.WriteLine("| `" + w + "` | " + (ofTallyFields.Contains(w) ? "○" : "**×**") + " | " + (w switch
            {
                "Intercepts" => "**第125期 段1 に足した。** 割り込んで主目標を引き受けた回数（鎖の全段）",
                "Shouldered" => "**第125期 段1 に足した。** 中継の段が実際に削った量（`relayed`）",
                "Swallowed" => "既存（第36期）。巨躯が飲み込んだ**名目量**。分かちは数えない",
                "DamageTaken" => "既存。**庇ったから増えたのか殴られたから増えたのかが分けられない**（第124期 Q0-3）",
                _ => "既存。味方由来のぶん",
            }) + " |");
        Console.WriteLine();
        Console.WriteLine("**既存の計数で代用できるか**: `NoteGuardPick`（第120期）は鎖の4段を数えているが、"
            + "**(a) 傷の在庫の計数（第120期）が生きているときだけ (b) 味方側だけ (c) 駒ごとではなく段ごと**なので、"
            + "戦績パネル（駒ごと・常時）には使えない。**`Swallowed` は巨躯だけ**で分かちを数えない。");
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3. `Reaction` が立つ経路");
        Console.WriteLine();
        if (ofWrapped.Count == 0)
        {
            Console.WriteLine("**走査が空**: `ctx.Reaction` / `ctx.Interrupt` を1件も引けなかった。ここで止める。");
            return;
        }
        Console.WriteLine("`Traits.cs` の呼び出し口 **" + ofWrapped.Count + " 箇所**"
            + "（`Reaction` " + ofWrapped.Count(x => x.Wrap == "Reaction")
            + " ／ `Interrupt` " + ofWrapped.Count(x => x.Wrap == "Interrupt") + "）。");
        Console.WriteLine();
        Console.WriteLine("| 包み | 特性 | 保持者（味方） |");
        Console.WriteLine("|---|---|---|");
        foreach (var (wrap, cls) in ofWrapped)
            Console.WriteLine("| `ctx." + wrap + "` | `" + cls + "` | "
                + (ofClassToId.TryGetValue(cls, out TraitId tid) ? OfOwners(tid) : "—") + " |");
        Console.WriteLine();
        bool ofPursuerWrapped = ofWrapped.Any(x => x.Class == "PursuerTrait");
        string ofPursuerBody = OffturnScan.Body(ofTraits, "public sealed class PursuerTrait");
        bool ofPursuerDirect = ofPursuerBody.Contains("ctx.PerformAttack", StringComparison.Ordinal);
        Console.WriteLine("**追い打ち（`PursuerTrait`・ハギ）**: 包まれているか "
            + (ofPursuerWrapped ? "**包まれている**" : "**包まれていない**")
            + " ／ `ctx.PerformAttack` を直に呼ぶか " + (ofPursuerDirect ? "**呼ぶ**" : "呼ばない")
            + " → `Models.cs` のコメントの主張は"
            + (!ofPursuerWrapped && ofPursuerDirect ? "**今も正しい**" : "**ずれている**") + "。");
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4. `Relayed` が立つ経路");
        Console.WriteLine();
        Console.WriteLine("`relayed: true` の呼び出し口 **" + ofRelaySites.Count + " 箇所**。");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 特性 | 保持者（味方） |");
        Console.WriteLine("|---|---|---|");
        foreach (string tr in ofRelaySites)
            Console.WriteLine("| `ApplyDamage(relayed: true)` | `TraitId." + tr + "` | "
                + (Enum.TryParse(tr, out TraitId rid) ? OfOwners(rid) : "—") + " |");
        Console.WriteLine();
        Console.WriteLine("**庇い（ガルド）は `Relayed` では立たない。** 庇いは `SelectTargetChain` が"
            + "**主目標そのものを差し替える**ので、`ApplyDamage` は最初から庇った駒へ1回だけ入る"
            + "（分割も中継も無い）——だから Q0-1 の側（`Intercept`）でしか出せない。");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5. ターン頭に発火する味方駒");
        Console.WriteLine();
        if (ofTurnStartIds.Count == 0)
        {
            Console.WriteLine("**走査が空**: `OnTurnStart` を上書きする特性を1件も引けなかった。ここで止める。");
            return;
        }
        var ofTurnStartAllies = UnitCatalog.All.Where(d => d.Traits.Any(ofTurnStartIds.Contains)).ToList();
        Console.WriteLine("`OnTurnStart` を上書きする特性 **" + ofTurnStartIds.Count + " 本** ／ "
            + "それを持つ味方駒 **" + ofTurnStartAllies.Count + " / " + UnitCatalog.All.Count + " 体**。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | ターン頭に走る特性 |");
        Console.WriteLine("|---|---|");
        foreach (UnitDef d in ofTurnStartAllies.OrderBy(x => x.Id, StringComparer.Ordinal))
            Console.WriteLine("| " + d.Name + " | "
                + string.Join(" / ", d.Traits.Where(ofTurnStartIds.Contains).Select(t => "`" + t + "`")) + " |");
        Console.WriteLine();
        Console.WriteLine("> **これは行動順ループの<u>外</u>で全部走り終わる**（`BattleEngine.Run` の順序は");
        Console.WriteLine("> `EmitTurnStart` → `TickStatuses` → `OnTurnStart` の全駒ループ → 行動順ループ）。");
        Console.WriteLine("> **誰の手番でもない時間に起きている**のに、いまの再生は1本の時間軸に並べている。");
        Console.WriteLine();

        // ---- Q0-6 ----
        Console.WriteLine("## Q0-6. ゾトの破裂の攻撃型");
        Console.WriteLine();
        string ofBomber = OffturnScan.Body(ofTraits, "public sealed class BomberTrait");
        if (ofBomber.Length == 0)
        {
            Console.WriteLine("**走査が空**: `BomberTrait` の本文を切り出せなかった。ここで止める。");
            return;
        }
        var ofBomberCalls = System.Text.RegularExpressions.Regex
            .Matches(ofBomber, @"ctx\.ApplyDamage\([^;]*\);").Select(m => m.Value).ToList();
        Console.WriteLine("`BomberTrait` の `ctx.ApplyDamage` **" + ofBomberCalls.Count + " 箇所**"
            + "（うち `pattern:` を渡すもの **"
            + ofBomberCalls.Count(c => c.Contains("pattern:", StringComparison.Ordinal)) + " 箇所**）。");
        Console.WriteLine();
        foreach (string c in ofBomberCalls)
            Console.WriteLine("- `" + c.Replace("\r", "").Replace("\n", " ") + "`");
        Console.WriteLine();
        Console.WriteLine("**`AttackPattern.All` は1度も通らない。** 破裂は敵全員・味方全員へ"
            + "**1体ずつ `ApplyDamage` を呼ぶ**だけで、`Attack` イベントも出さない"
            + "——台本に載るのは `Highlight`（破裂した）と、**`Pattern` が `null` の `Damage` が人数ぶん**。"
            + "**「型なし」として描かれる**のがポンの「爆発している感がない」の実体（P5-a の分母）。");
        Console.WriteLine();

        // ---- Q0-7 ----
        Console.WriteLine("## Q0-7. 味方の常時の攻撃型の分布");
        Console.WriteLine();
        Console.WriteLine("| 型 | 味方（`UnitCatalog.All`） | 敵（`EnemyCatalog` の全波） |");
        Console.WriteLine("|---|--:|--:|");
        foreach (AttackPattern p in Enum.GetValues<AttackPattern>())
            Console.WriteLine("| " + p + " | " + UnitCatalog.All.Count(d => d.Pattern == p)
                + " | " + ofFoeDefs.Count(d => d.Pattern == p) + " |");
        Console.WriteLine();
        Console.WriteLine("味方 **" + UnitCatalog.All.Count + " 体** ／ 敵の実体 **" + ofFoeDefs.Count + " 体**"
            + "（全波の重複を除いた数）。");
        Console.WriteLine();
        Console.WriteLine("**味方に常時の貫き・全体は 0 体**（`Pierce` / `All` は `EnemyCatalog` 側にしかない）。"
            + "戦闘中に型が変わる経路は `Trait.ModifyPattern`（熾火＝貫き・積み過ぎ＝薙ぎ・軋み＝薙ぎ）と"
            + "溜めの `PatternOverride` だけで、**`All` になる味方は1体もいない。**");
        Console.WriteLine();

        // ---- Q0-8 ----
        Console.WriteLine("## Q0-8. `DemoApp` の再生の時間軸");
        Console.WriteLine();
        var ofBeats = OffturnScan.Beats(File.ReadAllText(ofDemoPath));
        if (ofBeats.Sites == 0)
        {
            Console.WriteLine("**走査が空**: `ApplyEvent` の `Delay` を1件も引けなかった。ここで止める。");
            return;
        }
        Console.WriteLine("`DemoApp/Main.cs` の `ApplyEvent` から引いた間 **" + ofBeats.Sites + " 箇所**"
            + "（うち条件付き **" + ofBeats.CondSites + " 箇所**は別に数える）／ "
            + "種類 **" + ofBeats.Map.Count + " 件**（速度 ×1 のときの秒）。");
        Console.WriteLine();
        Console.WriteLine("| 種類 | 1件あたりの間（秒） | 条件付き（上限） |");
        Console.WriteLine("|---|--:|--:|");
        foreach (var kv in ofBeats.Map.OrderByDescending(k => k.Value))
            Console.WriteLine("| `" + kv.Key + "` | " + kv.Value.ToString("0.00")
                + " | " + (ofBeats.Cond.GetValueOrDefault(kv.Key) is var c && c > 0 ? c.ToString("0.00") : "—") + " |");
        Console.WriteLine();
        Console.WriteLine("**構造**: `BeginPlayback` が `_result.Events` を**先頭から1件ずつ**取り出し、"
            + "`ApplyEvent` が種類ごとに絵を出して `Delay` で間を置く。"
            + "**ターンの区切り（`TurnStart`）以外に束ねる単位が無い**ので、"
            + "**割り込み・肩代わり・ターン頭の一括はどれも「そういう順番で起きた1件」として流れる。**");
        Console.WriteLine();
        Console.WriteLine("> **この期に直すのはここである**（§5-1）。情報は既に台本にあるのに読めないのは、"
            + "**いつ起きたかが時間軸から引けない**から。");
        Console.WriteLine();

        // ---- Q0-9 ----
        Console.WriteLine("## Q0-9. 過去に介入の可視化を測っていないか");
        Console.WriteLine();
        string ofDesign = Path.Combine(ofRoot, "design");
        var ofHits = new List<(string File, int Relayed, int Guard, int Intervene)>();
        foreach (string f in Directory.GetFiles(ofDesign, "*.md"))
        {
            string t = File.ReadAllText(f);
            int r = System.Text.RegularExpressions.Regex.Matches(t, "Relayed").Count;
            int g = System.Text.RegularExpressions.Regex.Matches(t, "庇").Count;
            int i = System.Text.RegularExpressions.Regex.Matches(t, "介入").Count;
            if (r + g + i > 0) ofHits.Add((Path.GetFileName(f), r, g, i));
        }
        if (ofHits.Count == 0)
        {
            Console.WriteLine("**走査が空**: `design/` に1件も当たらなかった。ここで止める。");
            return;
        }
        Console.WriteLine("`design/` の Markdown **" + Directory.GetFiles(ofDesign, "*.md").Length + " 件**中、"
            + "`Relayed` / 庇 / 介入 のどれかに当たるのは **" + ofHits.Count + " 件**。上位のみ:");
        Console.WriteLine();
        Console.WriteLine("| ファイル | `Relayed` | 庇 | 介入 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var h in ofHits.OrderByDescending(x => x.Relayed + x.Guard + x.Intervene).Take(10))
            Console.WriteLine("| `" + h.File + "` | " + h.Relayed + " | " + h.Guard + " | " + h.Intervene + " |");
        Console.WriteLine();
        Console.WriteLine("**「介入を画面に出せるか」を測った期は無い。** 第85期は `Relayed` を"
            + "**計数の札**として足した期（味方の刃を数えるため）で画面の話ではなく、"
            + "第123期 (iii) と第124期 Q0-3 / P5 は**「出せない」と記録しただけ**で手を付けていない。");
        Console.WriteLine();
        Console.WriteLine("**推奨6行の照合**: `Presets` に存在しない行 **" + ofRowMissing.Count + " 件**"
            + (ofRowMissing.Count == 0 ? "。" : ": " + string.Join(" / ", ofRowMissing)));
        return;
    }

    // ======================================================================
    // check —— A3 / A4 と、実戦で介入が出ることの確認。
    // ======================================================================
    if (ofMode == "check")
    {
        Console.WriteLine("# 自己検査（第125期・`offturn check`）");
        Console.WriteLine();

        var ofLabelsUsed = ofChainSites.Select(x => OfLabelOf(x.Field))
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var ofLabelsAll = InterceptLabels.All.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        bool a3 = ofLabelsUsed.SequenceEqual(ofLabelsAll, StringComparer.Ordinal);
        Console.WriteLine("- **A3** 介入の段 **" + ofChainSites.Count + " 箇所** → 名前 **" + ofLabelsUsed.Length
            + " 種** ／ `InterceptLabels.All` **" + ofLabelsAll.Length + " 種** → " + (a3 ? "**○**" : "**×**"));
        Console.WriteLine("  - 使われた名前: " + string.Join(" / ", ofLabelsUsed));
        Console.WriteLine("  - **後備えだけ呼び出し口が2つ**（単体の鎖と範囲の鎖）なので、箇所 "
            + ofChainSites.Count + " と種 " + ofLabelsUsed.Length + " がずれるのは正しい。");

        int ofReads = 0, ofWrites = 0;
        foreach (string f in new[] { ofEnginePath, ofTraitsPath, ofModelsPath,
                                     Path.Combine(ofRoot, "BattleCore", "Presets.cs"),
                                     Path.Combine(ofRoot, "BattleCore", "UnitCatalog.cs"),
                                     Path.Combine(ofRoot, "BattleCore", "Engagement.cs") })
        {
            if (!File.Exists(f)) continue;
            string t = File.ReadAllText(f);
            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(t, @"BattleEventKind\.Intercept"))
            {
                // **コメント（`///` の文書化コメントを含む）は数えない。** 規則が読んでいるかを見たいので、
                // 行頭が `//` の行に出るものは除く（`<see cref=...>` がここに来る）。
                int bol = t.LastIndexOf('\n', m.Index) + 1;
                if (t.Substring(bol, m.Index - bol).TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                int from = Math.Max(0, m.Index - 10);
                if (t.Substring(from, m.Index - from).Contains("Kind = ", StringComparison.Ordinal)) ofWrites++;
                else ofReads++;
            }
        }
        Console.WriteLine("- **A4** `BattleCore` が `BattleEventKind.Intercept` に触る箇所（コメントを除く）: 書く **"
            + ofWrites + "** ／ **読む " + ofReads + "** → " + (ofReads == 0 ? "**○**" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("| 行 | 波 | seed | 出来事 | `Intercept` | `Reaction` | `Relayed` | 介入した駒 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
        int ofTotal = 0;
        foreach (var (name, f, ofStage, seed) in ofRows)
        {
            BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[ofStage].Enemy, seed, verbose: true);
            int ic = r.Events.Count(e => e.Kind == BattleEventKind.Intercept);
            ofTotal += ic;
            var who = r.TallyByUnit.Where(kv => kv.Value.Intercepts > 0)
                .OrderByDescending(kv => kv.Value.Intercepts)
                .Select(kv => OfNameOf(kv.Key) + "×" + kv.Value.Intercepts).ToArray();
            Console.WriteLine("| " + name + " | 第" + (ofStage + 1) + "波 | " + seed + " | " + r.Events.Count
                + " | " + ic + " | " + r.Events.Count(e => e.Reaction) + " | "
                + r.Events.Count(e => e.Relayed) + " | " + (who.Length == 0 ? "—" : string.Join(" / ", who)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**推奨6行の `Intercept` 合計 " + ofTotal + " 件。**"
            + " 0 件なら「載せたが出ていない」なのでここで疑う。");
        Console.WriteLine();

        int nq = 0, nv = 0, sq = 0, sv = 0;
        foreach (var (_, f, ofStage, seed) in ofRows)
        {
            BattleResult q = BattleEngine.Run(f, EnemyCatalog.Stages[ofStage].Enemy, seed, verbose: false);
            BattleResult v = BattleEngine.Run(f, EnemyCatalog.Stages[ofStage].Enemy, seed, verbose: true);
            nq += q.TallyByUnit.Values.Sum(t => t.Intercepts);
            nv += v.TallyByUnit.Values.Sum(t => t.Intercepts);
            sq += q.TallyByUnit.Values.Sum(t => t.Shouldered);
            sv += v.TallyByUnit.Values.Sum(t => t.Shouldered);
        }
        Console.WriteLine("- **計数は `verbose` に依らない**: `Intercepts` 非 verbose **" + nq + "** ／ verbose **"
            + nv + "** → " + (nq == nv ? "**○**" : "**×**"));
        Console.WriteLine("- **同じく `Shouldered`**: 非 verbose **" + sq + "** ／ verbose **" + sv + "** → "
            + (sq == sv ? "**○**" : "**×**"));
        return;
    }

    // ======================================================================
    // tempo —— A6。再生の総尺を、推奨6行の台本 × `ApplyEvent` の間の表で出す。
    // ======================================================================
    if (ofMode == "tempo")
    {
        if (args.Length < 4)
        {
            Console.WriteLine("usage: offturn tempo <比較する旧 Main.cs のパス>");
            return;
        }
        string ofOldPath = args[3];
        if (!File.Exists(ofOldPath))
        {
            Console.WriteLine("**走査が空**: `" + ofOldPath + "` が無い。ここで止める。");
            return;
        }
        var oldB = OffturnScan.Beats(File.ReadAllText(ofOldPath));
        var newB = OffturnScan.Beats(File.ReadAllText(ofDemoPath));
        if (oldB.Sites == 0 || newB.Sites == 0)
        {
            Console.WriteLine("**走査が空**: 間を引けなかった（旧 " + oldB.Sites + " / 新 " + newB.Sites + "）。ここで止める。");
            return;
        }
        Console.WriteLine("# 再生の総尺（第125期・`offturn tempo`）");
        Console.WriteLine();
        Console.WriteLine("無条件の間を旧 **" + oldB.Sites + " 箇所** ／ 新 **" + newB.Sites + " 箇所**、"
            + "条件付きを旧 **" + oldB.CondSites + " 箇所** ／ 新 **" + newB.CondSites + " 箇所**引いた。");
        Console.WriteLine("**総尺は無条件のぶんだけで出す**——条件付きは「1件あたり必ず掛かる時間」ではないので、"
            + "重み付けに使うと上限しか出せない（同じ規則で両方を分けている）。速度 ×1。");
        Console.WriteLine();
        Console.WriteLine("| 種類 | 旧（秒/件） | 新（秒/件） | 旧・条件付き | 新・条件付き |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (string k in oldB.Map.Keys.Concat(newB.Map.Keys).Distinct(StringComparer.Ordinal)
                             .OrderBy(x => x, StringComparer.Ordinal))
            Console.WriteLine("| `" + k + "` | " + oldB.Map.GetValueOrDefault(k).ToString("0.00")
                + " | " + newB.Map.GetValueOrDefault(k).ToString("0.00")
                + " | " + (oldB.Cond.GetValueOrDefault(k) > 0 ? oldB.Cond.GetValueOrDefault(k).ToString("0.00") : "—")
                + " | " + (newB.Cond.GetValueOrDefault(k) > 0 ? newB.Cond.GetValueOrDefault(k).ToString("0.00") : "—")
                + " |");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | seed | 出来事 | 旧（秒） | 新（秒） | 差 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        double to = 0, tn = 0;
        foreach (var (name, f, ofStage, seed) in ofRows)
        {
            BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[ofStage].Enemy, seed, verbose: true);
            double o = 0, n = 0;
            foreach (BattleEvent e in r.Events)
            {
                string k = e.Kind.ToString();
                o += oldB.Map.GetValueOrDefault(k);
                n += newB.Map.GetValueOrDefault(k);
            }
            to += o; tn += n;
            Console.WriteLine("| " + name + " | 第" + (ofStage + 1) + "波 | " + seed + " | " + r.Events.Count
                + " | " + o.ToString("0.0") + " | " + n.ToString("0.0") + " | " + (n - o).ToString("+0.0;-0.0;0.0") + " |");
        }
        Console.WriteLine("| **合計** | | | | **" + to.ToString("0.0") + "** | **" + tn.ToString("0.0")
            + "** | **" + (tn - to).ToString("+0.0;-0.0;0.0") + "**（"
            + (to > 0 ? (tn / to - 1) * 100 : 0).ToString("+0.0;-0.0;0.0") + "%） |");
        Console.WriteLine();
        Console.WriteLine("**`Intercept` は旧の表に無い**（第124期には存在しない種類）ので、"
            + "その件数ぶんはまるごと増分として出る。**手番の中を詰めて手番の外に配る**（§5-2）のが要件。");
        Console.WriteLine();
        Console.WriteLine("条件付きの間（新）: "
            + (newB.Cond.Values.Sum() <= 0 ? "—"
               : string.Join(" / ", newB.Cond.Where(x => x.Value > 0)
                   .Select(x => "`" + x.Key + "` " + x.Value.ToString("0.00") + " 秒"))) 
            + "。**発火した回数ぶんだけ乗る**（反撃・同時着弾は毎回は起きない）。");
        return;
    }

    Console.WriteLine("mode: phase0 / check / tempo <旧Main.cs>");
    return;
}
}
