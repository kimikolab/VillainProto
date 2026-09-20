using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 第172期 Phase 0 —— 拠点での組み直し（`--map11-phase172`）
//
// **走査の対象は実装そのもの**（`BattleCore/Traits.cs` / `BattleCore/Models.cs` /
// `DemoApp/BattlefieldView.cs`）。この期に手書きしたのは
//   (1) `Map11Relations.Rules` の意味の1行（9 種）
//   (2) 控えの駒の名指し 2 枚（残り4枚は `Map11.ReserveRanking()` が規則で選ぶ）
// の2つだけで、**どちらも欠けがあればここで名指しで落ちる**（R260）。
//
// `Map11Verify` / `Map11Phase171` と同じく **Godot の型を1つも使わない**
// ——ソースの中身は引数で受け取る。
// =====================================================================================

public static class Map11Phase172
{
    public static bool Run(Action<string> write, string traitsSource, string modelsSource,
                           string fieldSource)
    {
        write("# 第172期 Phase 0 —— 拠点での組み直し");
        write("");
        bool ok = true;

        if (traitsSource.Length == 0 || modelsSource.Length == 0 || fieldSource.Length == 0)
        {
            write("**走査が空**: `Traits.cs` / `Models.cs` / `BattlefieldView.cs` のどれかを読めなかった。"
                + "「該当なし」と区別が付かないのでここで止める（R034）。");
            return false;
        }

        // ---------------- Q0-1 ----------------
        write("## Q0-1: 触らない限り `--map11-verify` が 0.0pt のままである根拠");
        write("");
        var fresh = new Map11State(0);
        bool untouched = fresh.Squads.All(s => s.Units is null);
        bool bench = fresh.Bench.Count == Map11.ReserveCount;
        ok &= untouched && bench;
        write("| # | 事実 | 実測 | 要る値 | 判定 |");
        write("|---|---|--:|--:|:-:|");
        write($"| 1 | 新しい `Map11State` で `Units` を持つ隊 "
            + $"| {fresh.Squads.Count(s => s.Units is not null)} | 0 | {(untouched ? "**○**" : "×")} |");
        write($"| 2 | 控えの駒 | {fresh.Bench.Count} 枚 | {Map11.ReserveCount} 枚 "
            + $"| {(bench ? "**○**" : "×")} |");
        write("");
        write("**組み直しは「触った隊の `Units` を実体化する」だけ**（`Map11State.Roster`）で、"
            + "触らなければ `Units` は null のまま、`Send` がこれまでどおり定義から作る。"
            + "**控えの駒の実体化（`Materialize`）は乱数を1つも引かない**ので、"
            + "作っておくだけでは戦闘の乱数列は1ビットも動かない。");
        write("");
        write("`Send` の変更は1点だけ——`if (Units is null && !Deployed)` を `??=` ＋ "
            + "`Deployed = true` にした。**第169期の版で「`Units` が null でなく `Deployed` が偽」に"
            + "なる道は1本も無い**（`Units` が null になるのは全滅のときだけで、そこでは `Lost` が立って"
            + "`Send` は手前で返る）ので、組み直しをしない通しでは同値である。"
            + "**実測の裏は `--map11-verify` の5つの量が第169期と差 0.0pt であること**（自己検査 (a)）。");
        write("");

        // ---------------- Q0-2 ----------------
        write("## Q0-2: 5 枚未満・空席ありの味方を `BattleEngine.Run` に渡せるか");
        write("");
        write("**渡せる。** 死者が出た隊は第169期から既にこの形で次の戦闘へ入っている"
            + "（`CrossBoundary` は生存者だけを返すので、**席が空いたリスト**がそのまま次の `Run` へ行く）。"
            + "ここでは念のため、枚数を落とした隊を実際に走らせて確かめる。");
        write("");
        write("| 残す席 | 枚数 | 決着 | 勝敗 | 落ちずに済んだか |");
        write("|---|--:|--:|:-:|:-:|");
        Formation baseF = Map11.Squads[0].F;
        Formation foe = Map11.Roads[0][0].Enemy;
        bool ran = true;
        foreach (int[] keep in new[] { new[] { 0, 1, 2, 3, 4 }, new[] { 0, 2, 3, 4 },
                                       new[] { 1, 4 }, new[] { 2 } })
        {
            try
            {
                var pu = BattleEngine.Materialize(baseF, BattleContext.PlayerTeam)
                                     .Where(u => keep.Contains(u.Slot)).ToList();
                var eu = BattleEngine.Materialize(foe, BattleContext.EnemyTeam);
                BattleResult r = BattleEngine.Run(pu, eu, 0, verbose: false);
                write($"| {string.Join(" / ", keep.Select(k => FormationRules.SeatNames[k]))} "
                    + $"| {pu.Count} | {r.Turns} | {(r.PlayerWon ? "勝" : "負")} | **○** |");
            }
            catch (Exception ex)
            {
                ran = false;
                write($"| {string.Join(" / ", keep.Select(k => FormationRules.SeatNames[k]))} "
                    + $"| — | — | — | **× {ex.GetType().Name}** |");
            }
        }
        ok &= ran;
        write("");
        write("**0 枚だけは禁じる**（指示書 §1-1）——`Map11State.CanSend` が偽を返し、画面も出せない。");
        write("");

        // ---------------- Q0-3 ----------------
        write("## Q0-3: 席を入れ替えたとき、`Slot` 以外に書き換えが要るもの");
        write("");
        bool noRowCache = !modelsSource.Contains("private Row _row", StringComparison.Ordinal)
                       && !modelsSource.Contains("private int _lane", StringComparison.Ordinal);
        write("| 量 | 出どころ | 書き換えが要るか |");
        write("|---|---|:-:|");
        write($"| `UnitState.Slot` | そのもの | **要る**（組み直しが書くのはここだけ） |");
        write($"| `Row` / レーン | `FormationRules.RowOf(Slot)` を**毎回引く**"
            + $"（キャッシュの field は {(noRowCache ? "0 件" : "**あり**")}） | 要らない |");
        write("| 隣接・列・前後 | `FormationRules` の表を席から引く | 要らない |");
        write("| `HasFallenBack` | 戦闘中に engine が立てる記録 "
            + "| **触らない**（組み直しは「下がった」ではない。第169期と同じ扱いで会戦を跨いで残る） |");
        write("| `InstanceId` | 次の `Run` が渡した並びで振り直す "
            + "| 要らない。ただし**リストを席の昇順に保つ**こと（`Materialize` / `CrossBoundary` と同じ並び） |");
        ok &= noRowCache;
        write("");
        int slotWrites = System.Text.RegularExpressions.Regex.Matches(traitsSource, @"\.Slot\s*=\s").Count;
        write($"`Traits.cs` で `Slot` に代入している箇所 **{slotWrites} 件**"
            + "（移動は engine の `SwapSlots` 1 箇所に寄っている——第124期 R039）。"
            + "**組み直しはその窓口を通らない**（戦闘の外で、盤面が1つも立っていない状態で書く）。");
        write("");

        // ---------------- Q0-4 ----------------
        write("## Q0-4: 控えの駒 6 枚の候補と根拠");
        write("");
        write("**手で選ばない**（指示書 §1-2）。線は3つ——(1) 3 隊 15 枚と重ならない ／ "
            + "(2) `Presets.Compare` の在席枠が 3 以上 ／ (3) **カド隊・かき回し隊・控え隊"
            + "それぞれの駒と同じ行に入った実績がある**。通った駒を「同席した (行, 相手) の組の数」"
            + "の多い順に並べ、上から4枚を採る。");
        write("");
        var rank = Map11.ReserveRanking();
        write("| # | 駒 | 在席枠 | カド隊と | かき回し隊と | 控え隊と | 計 | 線を通るか | 採否 |");
        write("|--:|---|--:|--:|--:|--:|--:|:-:|:-:|");
        var picked = Map11.Reserves.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        int shown = 0;
        foreach (Map11.ReserveRow r in rank)
        {
            shown++;   // **全部出す**——線を通らなかった駒がなぜ落ちたかを後から引けるように
            write($"| {shown} | {r.Def.Name} | {r.Seats} | {r.WithSquad[0]} | {r.WithSquad[1]} "
                + $"| {r.WithSquad[2]} | {r.Total} | {(r.AllThree ? "**○**" : "×")} "
                + $"| {(picked.Contains(r.Def.Id) ? "**採**" : "—")} |");
        }
        write("");
        write($"**名指しの2枚**（指示書 §1-2）: {string.Join(" / ", Map11.Reserves.Take(2).Select(d => d.Name))}"
            + "——クビは「送り先の一行が書けている駒で 3 隊に入っていない」、"
            + "セッキは「リィカの相方として実測が最良だった駒」。");
        write("");
        write($"**採った 6 枚**: {string.Join(" / ", Map11.Reserves.Select(d => d.Name))}");
        write("");
        bool six = Map11.Reserves.Length == Map11.ReserveCount
                && Map11.Reserves.Select(d => d.Id).Distinct().Count() == Map11.ReserveCount;
        ok &= six;
        write(six ? "**6 枚が重複なく揃った（○）。ポンが差し替える前提。**"
                  : "**× —— 6 枚に足りない。線が厳しすぎる。**");
        write("");

        // ---------------- Q0-5 ----------------
        write("## Q0-5: 編成画面の席の部品に線を重ねられるか。改造の規模");
        write("");
        int layers = System.Text.RegularExpressions.Regex.Matches(fieldSource, @"SetupLinkLayer").Count;
        bool overlaid = layers > 0;
        ok &= overlaid;
        write("| 問い | 答え |");
        write("|---|---|");
        write("| 席の座標は取れるか | **取れる。** `BattlefieldView.LayoutActors` が "
            + "`_slots[slot].Position` / `.Size` を毎回決めている（画面の広さで動くので、"
            + "**図のように固定の座標にはできない**——だから `SeatLinks` は席の四角を引数で受け取る形にした） |");
        write("| 編集の仕組みを触るか | **触らない。** ドロップ・削除・ドラッグの口（`FormationSlot`）は1文字も変えていない |");
        write($"| 改造の規模 | `BattlefieldView.cs` に層を2枚（`SetupLinkLayer` の出現 {layers} 件）と、"
            + "`Main.cs` に「選んでいる席を渡す」1 行 ＋ 右の欄に関係を出す 1 メソッド |");
        write("| 第171期 Q0-5 の「× 編集器なので使えない」との違い | あちらは**図の描画そのものを"
            + "`FormationSlot` に任せられるか**の問いで、答えは今も ×。"
            + "**今回は席の四角だけを借りて、線を別の層に重ねている** |");
        write("");
        write(overlaid ? "**重ねられた（○）。部B は実施した。**" : "**× —— 重ねられていない。**");
        write("");

        // ---------------- Q0-6 ----------------
        write("## Q0-6: 関係の走査を 52 枚へ広げると、拾えない札は何本か");
        write("");
        var scanned = Map11Phase171.ScanPositionalTraits(traitsSource);
        if (scanned.Count == 0)
        {
            write("**走査が空**: `Traits.cs` から位置の窓口を1件も引けなかった。ここで止める（R034）。");
            return false;
        }
        var roster = new HashSet<TraitId>(UnitCatalog.All.SelectMany(u => u.Traits));
        var onMap = Map11Phase171.TraitsOnThisMap();
        var covered = Map11Relations.Covered.ToHashSet();
        write($"`UnitCatalog.All`（**52 枚**）が持つ札は **{roster.Count} 種**"
            + $"（このマップに出るのは {onMap.Count} 種）。"
            + $"そのうち位置の窓口を読む札は **{scanned.Keys.Count(roster.Contains)} 本**。");
        write("");
        write("| 札 | クラス | 52 枚にいる | 線を引く | 引けない理由 |");
        write("|---|---|:-:|:-:|---|");
        int gap = 0;
        foreach ((TraitId id, string cls) in scanned.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
        {
            bool here = roster.Contains(id);
            bool drawn = covered.Contains(id);
            string why = Map11Relations.Unresolved.GetValueOrDefault(id, "");
            if (here && !drawn && why.Length == 0) { gap++; why = "**表に無い**"; }
            write($"| `{id}` | `{cls}` | {(here ? "○" : "—")} | {(drawn ? "**○**" : "×")} "
                + $"| {(why.Length == 0 ? "—" : why)} |");
        }
        write("");
        ok &= gap == 0;
        write(gap == 0
            ? "**52 枚ぶんでも、線を引くか・引けない理由が書いてある（○）。**"
              + " 第171期はこのマップに出る札だけを門にしていたので、**門が広がった。**"
            : $"**{gap} 本が `Map11Relations` のどちらの表にも無い（×）。**");
        write("");
        var orphan = covered.Where(c => !scanned.ContainsKey(c)).ToArray();
        write($"逆向き——線を引いているのに走査に出てこない札 **{orphan.Length} 件**: "
            + (orphan.Length == 0 ? "—" : string.Join(" / ", orphan.Select(o => $"`{o}`")))
            + "（R264: 窓口が engine 側にある札は、札のソースの走査では拾えない）。");
        write("");

        // ---------------- 意味の1行 ----------------
        write("## 線の1語の意味（`Map11Relations.Rules` の `Mean`）");
        write("");
        write("| 札 | 語 | 意味の1行 |");
        write("|---|---|---|");
        int noMean = 0;
        foreach ((TraitId id, string word, string mean) in
                 Map11Relations.Table.OrderBy(t => t.Id.ToString(), StringComparer.Ordinal))
        {
            if (mean.Trim().Length == 0) noMean++;
            write($"| `{id}` | {word} | {(mean.Trim().Length == 0 ? "**×（無い）**" : mean)} |");
        }
        write("");
        ok &= noMean == 0;
        write(noMean == 0
            ? $"**関係の形 {Map11Relations.Table.Count()} 種すべてに1行がある（○）。**"
            : $"**{noMean} 種に1行が無い（×）。**");
        write("");

        // ---------------- 組み直しの操作が規則どおりか ----------------
        write("## 操作の規則（画面から破れないこと・自己検査 (d)）");
        write("");
        var st = new Map11State(0);
        st.Send(0, 0);                       // カド隊を道へ出す
        bool onRoad = !st.CanReform(0);
        bool refusedSwap = !st.SwapSeats(0, 0, 0, 3);
        bool refusedBench = !st.SwapWithBench(0, 0, 0);
        bool refusedReset = !st.ResetSquad(0);

        var st2 = new Map11State(0);
        bool okSwap = st2.SwapSeats(1, 0, 1, 3);
        bool okAcross = st2.SwapSeats(1, 1, 2, 4);
        bool okBench = st2.SwapWithBench(1, 2, 0);
        // 全部を控えへ下げて 0 枚にする
        for (int slot = 0; slot < FormationRules.PlayableSlotCount; slot++)
            st2.SwapWithBench(1, slot, int.MaxValue);
        bool emptied = st2.Squads[1].Units is { Count: 0 };
        bool cannotSend = !st2.CanSend(1);
        st2.Send(1, 0);
        bool stayedHome = st2.Squads[1].Road < 0;

        write("| # | 規則 | 実測 | 判定 |");
        write("|---|---|---|:-:|");
        write($"| 1 | 道の上の隊は組み直せない | `CanReform` {(onRoad ? "偽" : "真")} "
            + $"/ 席 {(refusedSwap ? "拒否" : "**通った**")} / 控え {(refusedBench ? "拒否" : "**通った**")} "
            + $"/ 元に戻す {(refusedReset ? "拒否" : "**通った**")} "
            + $"| {(onRoad && refusedSwap && refusedBench && refusedReset ? "**○**" : "×")} |");
        write($"| 2 | 拠点なら席・隊どうし・控えの3つが通る "
            + $"| {(okSwap ? "○" : "×")} / {(okAcross ? "○" : "×")} / {(okBench ? "○" : "×")} "
            + $"| {(okSwap && okAcross && okBench ? "**○**" : "×")} |");
        write($"| 3 | 0 枚の隊は出せない | 空にできる {(emptied ? "○" : "×")} "
            + $"/ `CanSend` {(cannotSend ? "偽" : "**真**")} / 送っても拠点のまま {(stayedHome ? "○" : "×")} "
            + $"| {(emptied && cannotSend && stayedHome ? "**○**" : "×")} |");
        bool rules = onRoad && refusedSwap && refusedBench && refusedReset
                  && okSwap && okAcross && okBench && emptied && cannotSend && stayedHome;
        ok &= rules;
        write("");
        write("**死者は動かせない**——倒れた駒は `CrossBoundary` が落とすので、"
            + "拠点へ戻った隊のリストには**そもそも入っていない**（席が空く）。"
            + "空いた席には控えの駒を入れられる（指示書 §1-1）。");
        write("");

        write(ok ? "**Phase 0 ○**" : "**Phase 0 × —— 上の × を埋めること。**");
        return ok;
    }
}
