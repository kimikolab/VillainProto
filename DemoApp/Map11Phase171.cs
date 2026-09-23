using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 第171期 Phase 0 —— 可読性の土台（`--map11-phase171`）
//
// **走査の対象は実装そのもの**（`BattleCore/Traits.cs` / `BattleCore/BattleEngine.cs`）。
// この期に手書きしたのは `Map11Relations.Rules` と `BoardRuleTags` の2つだけで、
// **どちらも欠けがあればここで名指しで落ちる**（R260）。
//
// `Map11Verify` と同じく **Godot の型を1つも使わない**——ソースの中身は引数で受け取る。
// =====================================================================================

public static class Map11Phase171
{
    /// <summary>
    /// ソースの走査に使う位置の窓口。<b><see cref="FormationRules"/> の公開メンバの名前</b>で、
    /// 「席・隣接・列・レーンを読んでいる」の判定はこの語の出現だけで決まる。
    /// </summary>
    public static readonly string[] PositionApis =
    {
        "AreAdjacent", "AreSameRowPair", "IsLanePredecessor", "SweepTargets",
        "PlayableSlotsOfRow", "SlotsOfRow", "LanePath", "LanesOf", "DepthOf",
    };

    /// <summary>
    /// クラス塊に割って、位置の窓口を読んでいる <c>*Trait</c> と、その <see cref="TraitId"/> を引く。
    /// <b>1クラスが複数の <c>TraitId</c> を持つ場合があるので全部拾う</b>（R051）。
    /// </summary>
    public static Dictionary<TraitId, string> ScanPositionalTraits(string traitsSource)
    {
        var found = new Dictionary<TraitId, string>();
        var heads = System.Text.RegularExpressions.Regex
            .Matches(traitsSource, @"\bclass\s+(\w*Trait\w*)\b")
            .Select(m => (m.Index, Name: m.Groups[1].Value)).ToList();
        for (int i = 0; i < heads.Count; i++)
        {
            int from = heads[i].Index;
            int to = i + 1 < heads.Count ? heads[i + 1].Index : traitsSource.Length;
            string body = traitsSource[from..to];
            // **`FormationRules.` の接頭を必ず要求する。** 素の語で当てると
            // `ctx.WoundDepthOf` が `DepthOf` に当たって、傷を読む札 6 本が
            // 「席を読んでいる」に化ける（最初の走査で実際に踏んだ）。
            if (!PositionApis.Any(a => body.Contains("FormationRules." + a, StringComparison.Ordinal))) continue;
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex
                         .Matches(body, @"TraitId\s+Id\s*=>\s*TraitId\.(\w+)"))
                if (Enum.TryParse(m.Groups[1].Value, out TraitId id)) found[id] = heads[i].Name;
        }
        return found;
    }

    /// <summary>このマップに出る駒（味方 15 ＋ 敵 18）が持つ札を全部集める。</summary>
    public static HashSet<TraitId> TraitsOnThisMap()
    {
        var all = new HashSet<TraitId>();
        foreach (Map11.SquadDef s in Map11.Squads)
            foreach ((int _, UnitDef d) in s.F.Occupied())
                foreach (TraitId t in d.Traits) all.Add(t);
        foreach (IReadOnlyList<Map11.RoadNode> road in Map11.Roads)
            foreach (Map11.RoadNode n in road)
                foreach ((int _, UnitDef d) in n.Enemy.Occupied())
                    foreach (TraitId t in d.Traits) all.Add(t);
        return all;
    }

    /// <summary>
    /// 第171期 Phase 0。ソースの中身は引数で受け取る（読めなければ空文字で渡すこと）。
    /// <b>戦闘は自己検査 (c) のぶんだけ</b>（3 隊 × 4 区画 × <paramref name="battlesPerPair"/> 戦）。
    /// </summary>
    public static bool Run(Action<string> write, string traitsSource, string engineSource,
                           int battlesPerPair = 20)
    {
        write("# 第171期 Phase 0 —— 可読性の土台");
        write("");
        bool ok = true;

        if (traitsSource.Length == 0 || engineSource.Length == 0)
        {
            write("**走査が空**: `BattleCore/Traits.cs` か `BattleCore/BattleEngine.cs` を読めなかった。"
                + "「該当なし」と区別が付かないのでここで止める（R034）。");
            return false;
        }

        // ---- Q0-1 ----
        write("## Q0-1: 封じを新しい種類にするか、`Highlight` に乗せるか");
        write("");
        int highlightSites = System.Text.RegularExpressions.Regex
            .Matches(engineSource, @"Kind = BattleEventKind\.Highlight").Count;
        int highlightLogs = System.Text.RegularExpressions.Regex
            .Matches(engineSource, @"LogKind\.Highlight").Count;
        write($"`BattleEngine.cs` の `Highlight` の初期化子 **{highlightSites} 箇所** ／ "
            + $"`LogKind.Highlight` の呼び口 **{highlightLogs} 箇所**。");
        write("");
        write("`Highlight` は `ctx.Log(..., LogKind.Highlight)` が**自動で流す**もので、"
            + "再生側（`Main.ApplyEvent`）は `e.Text` をそのまま**バナーに出す**"
            + "（破裂・覚醒などの見せ場）。封じを混ぜると"
            + "**再生側が封じを他の強調と区別できない**——指示書 Q0-1 の基準はこの1つなので、"
            + "**新しい種類（`Sealed`）にした**。3本（粛・渇き・軛）は `Text`（`SealedLabels`）で分ける"
            + "——転倒・痺れ・混乱が「付与」と「発動」を1種類に持つのと同じ形で、"
            + "種類を3つに割ると再生側が同じ形の分岐を3つ持つことになる。");
        write("");

        // ---- Q0-2 ----
        write("## Q0-2: `BattleEventKind` を1つ足すと何が動くか");
        write("");
        string[] kinds = Enum.GetNames(typeof(BattleEventKind));
        bool hasSealed = kinds.Contains("Sealed", StringComparer.Ordinal);
        ok &= hasSealed;
        write($"`BattleEventKind` は **{kinds.Length} 種**"
            + $"（`Sealed` があるか: {(hasSealed ? "**○**" : "**×**")}）。");
        write("");
        write("| 生成物 | 動くか | 中身 |");
        write("|---|:-:|---|");
        write("| `docs/watch.md` | **○** | `watch phase0` の盲点表が "
            + "`Enum.GetNames(typeof(BattleEventKind))` を列挙している"
            + "（種類数が1つ増え、1-b の表に `Sealed` の行が増える） |");
        write("| `docs/balance.md` | × | `compare` は `verbose: false`。"
            + "**`Emit` は verbose のときしか積まない**ので1件も作られない |");
        write("| `audit` | × | 生成物の編成数しか見ない |");
        write("| `docs/rules.md` | × | ノブ（`record struct`）の一覧で、イベントの種類は見ない |");
        write("| 他の 11 ファイル | × | どれも `BattleEventKind` を列挙していない |");
        write("");

        // ---- Q0-3 ----
        write("## Q0-3: 台本に足しても盤面が動かない根拠");
        write("");
        int emitCalls = System.Text.RegularExpressions.Regex
            .Matches(engineSource, @"EmitSealed\(").Count;
        bool sites = emitCalls == 4;   // 定義 1 ＋ 呼び口 3
        ok &= sites;
        int bodyStart = engineSource.IndexOf("internal void EmitSealed(", StringComparison.Ordinal);
        string emitBody = bodyStart < 0
            ? "" : engineSource[bodyStart..Math.Min(engineSource.Length, bodyStart + 900)];
        string[] forbidden = { "Roll(", "PickOne(", "Shuffle(", "SetCounter(", ".Hp -=", ".Hp +=" };
        string[] hits = forbidden.Where(f => emitBody.Contains(f, StringComparison.Ordinal)).ToArray();
        ok &= hits.Length == 0;
        write($"`EmitSealed(` の出現 **{emitCalls} 件**"
            + $"（定義 1 ＋ 呼び口 3 で 4 が正: {(sites ? "**○**" : "**×**")}）。");
        write($"本体が触る禁止の窓口（乱数・カウンタ・HP）: "
            + (hits.Length == 0 ? "**0 件 ○**" : $"**{string.Join(" / ", hits)} ×**"));
        write("");
        write("呼び口は3つとも**既にある計数の合流点**——`NoteHushBlocked`（`sole` が真のときだけ）／"
            + "`NoteDroughtBlocked`／`ApplyDamage` の `yokeBinding` の中。"
            + "**判定式も評価の順序も1文字も動かしていない**ので、乱数の消費も盤面ルールの答えも変わらない。"
            + "実測の裏は `compare` 305 セルが `docs/balance.md` と 0 件差分であること（自己検査 (a)）。");
        write("");

        // ---- Q0-4 ----
        write("## Q0-4: 席を読む札を実装から列挙し、関係の表と突き合わせる");
        write("");
        var scanned = ScanPositionalTraits(traitsSource);
        if (scanned.Count == 0)
        {
            write("**走査が空**: `Traits.cs` から位置の窓口を1件も引けなかった。ここで止める（R034）。");
            return false;
        }
        HashSet<TraitId> onMap = TraitsOnThisMap();
        var covered = Map11Relations.Covered.ToHashSet();
        write($"`Traits.cs` の `*Trait` のうち位置の窓口"
            + $"（{string.Join(" / ", PositionApis.Select(a => "`FormationRules." + a + "`"))}）を読むもの "
            + $"**{scanned.Count} 本**。このマップに出る札は **{onMap.Count} 種**。");
        write("");
        write("| 札 | クラス | このマップに出る | 線を引く | 引けない理由 |");
        write("|---|---|:-:|:-:|---|");
        int gap = 0;
        foreach ((TraitId id, string cls) in scanned.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
        {
            bool here = onMap.Contains(id);
            bool drawn = covered.Contains(id);
            string why = Map11Relations.Unresolved.GetValueOrDefault(id, "");
            // **門はこのマップに出る札だけ。** 出ない札まで揃えるのは別の作業。
            if (here && !drawn && why.Length == 0) { gap++; why = "**表に無い**"; }
            write($"| `{id}` | `{cls}` | {(here ? "○" : "—")} | {(drawn ? "**○**" : "×")} "
                + $"| {(why.Length == 0 ? "—" : why)} |");
        }
        write("");
        ok &= gap == 0;
        write(gap == 0
            ? "**このマップに出る札はすべて、線を引くか・引けない理由が書いてある（○）。**"
            : $"**{gap} 本が `Map11Relations` のどちらの表にも無い（×）。**");
        write("");

        var orphan = covered.Where(c => !scanned.ContainsKey(c)).ToArray();
        write($"逆向きの検査——線を引いているのに走査に出てこない札 **{orphan.Length} 件**: "
            + (orphan.Length == 0 ? "—" : string.Join(" / ", orphan.Select(o => $"`{o}`")))
            + "。**2 件とも窓口が engine 側にある**ので走査に出ない——`Stoic` は "
            + "`BattleContext.SupportTargets` が隣接表を読み、`Colossus` は `ApplyDamage` が "
            + "`FormationRules.DepthOf` で前後を見る。**線そのものは正しい**が、"
            + "**「札のソースを走査する」形では engine 側の窓口を拾えない**"
            + "——この2本だけは手書きの表が走査より先に立っている。");
        write("");

        // ---- 盤面ルールの札 × 封じの名前 ----
        write("### 盤面ルールの札（`BoardRuleTags`）× 封じの名前（`SealedLabels`）");
        write("");
        var tagWords = BoardRuleTags.All.Select(kv => kv.Value).ToHashSet(StringComparer.Ordinal);
        var missingTag = SealedLabels.All.Where(w => !tagWords.Contains(w)).ToArray();
        ok &= missingTag.Length == 0;
        write($"札 **{tagWords.Count} 語**（{string.Join(" / ", tagWords)}）／ "
            + $"封じ **{SealedLabels.All.Length} 語**（{string.Join(" / ", SealedLabels.All)}）。"
            + (missingTag.Length == 0
                ? " **封じの3語はすべて札の側にもある（○）**"
                  + "——保持者の札（★）と封じの浮き文字が必ず同じ語になる。"
                : $" **札に無い封じ: {string.Join(" / ", missingTag)}（×）**"));
        write("");

        // ---- Q0-5 ----
        write("## Q0-5: 図を描く部品");
        write("");
        write("| 候補 | 使えるか | 理由 |");
        write("|---|:-:|---|");
        write("| `PartyBar` | × | **味方の開幕 5 枚を席の順に横一列で固定する**もので、"
            + "X 字の位置関係を持たない。数字を `BattlePawn3D` から引き直す作りなので戦闘シーン専用 |");
        write("| 編成画面（`BattlefieldView` の `FormationSlot`） | × | ドロップ・削除の口が付いた**編集器**で、"
            + "読み取り専用に落とすと出撃・プリセット・ロスターが道連れになる（第169期と同じ判断） |");
        write("| `FormationRules.SeatNames` / `AdjacencyTable` | **○** | "
            + "**席の意味はここから引く**（写しを持たない）。描画そのものは `Map11Main` に新しく作る |");
        write("");

        // ---- 自己検査 (c) ----
        write($"## 自己検査 (c): 帳簿の数 ＝ 台本の封じイベントの数（3 隊 × 4 区画 × {battlesPerPair} 戦）");
        write("");
        write("| 隊 | 相手 | 粛 帳簿/台本 | 渇き 帳簿/台本 | 軛 帳簿/台本 | 一致 |");
        write("|---|---|--:|--:|--:|:-:|");
        bool allMatch = true;
        long tH = 0, tHe = 0, tD = 0, tDe = 0, tY = 0, tYe = 0;
        foreach (Map11.SquadDef sq in Map11.Squads)
            foreach (IReadOnlyList<Map11.RoadNode> road in Map11.Roads)
                foreach (Map11.RoadNode n in road)
                {
                    long h = 0, he = 0, d = 0, de = 0, y = 0, ye = 0;
                    for (int seed = 0; seed < battlesPerPair; seed++)
                    {
                        var pu = BattleEngine.Materialize(sq.F, BattleContext.PlayerTeam);
                        var eu = BattleEngine.Materialize(n.Enemy, BattleContext.EnemyTeam, Map11.EnemyScale);
                        // **台本が要るので `verbose: true`。** 帳簿は verbose に依らず積まれる。
                        BattleResult r = BattleEngine.Run(pu, eu, seed, verbose: true);
                        h += r.BoardRules.HushBlocked[0] + r.BoardRules.HushBlocked[1];
                        d += r.BoardRules.DroughtHits[0] + r.BoardRules.DroughtHits[1];
                        y += r.Yoke.CutOnPlayerHits + r.Yoke.CutOnEnemyHits;
                        foreach (BattleEvent e in r.Events)
                        {
                            if (e.Kind != BattleEventKind.Sealed) continue;
                            if (e.Text == SealedLabels.Hush) he++;
                            else if (e.Text == SealedLabels.Drought) de++;
                            else if (e.Text == SealedLabels.Yoke) ye++;
                        }
                    }
                    bool hit = h == he && d == de && y == ye;
                    allMatch &= hit;
                    tH += h; tHe += he; tD += d; tDe += de; tY += y; tYe += ye;
                    write($"| {sq.Name} | {n.Name} | {h}/{he} | {d}/{de} | {y}/{ye} "
                        + $"| {(hit ? "○" : "**×**")} |");
                }
        write($"| **計** | — | **{tH}/{tHe}** | **{tD}/{tDe}** | **{tY}/{tYe}** "
            + $"| {(allMatch ? "**○**" : "**×**")} |");
        write("");
        ok &= allMatch;
        write(allMatch
            ? "**(c) ○ —— 3 本とも帳簿と台本が1件も違わない。** 粛は `HushBlocked`"
              + "（**粛が単独の原因だったぶん**。`HushBlockedAny` ではない）、渇きは `DroughtHits`、"
              + "軛は `YokeCutOnPlayerHits + YokeCutOnEnemyHits`（両陣営の和）。"
            : "**(c) × —— 帳簿と台本がずれている。**");
        write("");

        write(ok ? "**Phase 0 ○**" : "**Phase 0 × —— 上の × を埋めること。**");
        return ok;
    }
}
