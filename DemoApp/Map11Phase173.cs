using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 第173期 Phase 0 —— 組み直しの仕上げ（`--map11-phase173`）
//
// **盤面の挙動は1ビットも変えない期**なので、ここが確かめるのは全部「手書きの欠け」である。
// 手書きしたのは3つだけで、**3つとも欠ければ名指しで落ちる**（R260）:
//   (1) `Map11Relations.Rules` の `Sign`（得／損／両方）と `Conditional`（点線）  → Q0-1 / Q0-2
//   (2) 各駒の説明文（`UnitDef.PlusText` / `MinusText`）に出る語                 → Q0-3
//   (3) 敵 18 体の `PlusText`                                                    → Q0-4
//
// `Map11Verify` / `Map11Phase171` / `Map11Phase172` と同じく **Godot の型を1つも使わない**
// ——ソースの中身は引数で受け取る。
// =====================================================================================

public static class Map11Phase173
{
    public static bool Run(Action<string> write, string traitsSource)
    {
        write("# 第173期 Phase 0 —— 組み直しの仕上げ");
        write("");
        bool ok = true;

        if (traitsSource.Length == 0)
        {
            write("**走査が空**: `Traits.cs` を読めなかった。"
                + "「該当なし」と区別が付かないのでここで止める（R034）。");
            return false;
        }

        // ---------------- Q0-1 ----------------
        write("## Q0-1: `Unresolved` の札を (a)(b) に仕分ける");
        write("");
        write("**(a) 席からそもそも引けない** ／ **(b) 席からは引けるが、いつ起きるかが条件付き**。"
            + "第172期は2つが1つの表に混ざっていたので、**(b) が「線を引かない札」に埋もれていた**"
            + "——観察ログ「ボルグをリィカ軸へ入れたら、ボルグの自傷でリィカが落ちた。"
            + "図にその関係が1本も無かった」（問い5）の正体はこれである。");
        write("");
        var scanned = Map11Phase171.ScanPositionalTraits(traitsSource);
        if (scanned.Count == 0)
        {
            write("**走査が空**: `Traits.cs` から位置の窓口を1件も引けなかった。ここで止める（R034）。");
            return false;
        }
        var roster = new HashSet<TraitId>(UnitCatalog.All.SelectMany(u => u.Traits));
        var conditional = Map11Relations.Table.Where(t => t.Rule.Conditional).ToList();
        write("| 仕分け | 札 | 語 | 得／損 | 理由 |");
        write("|:-:|---|---|:-:|---|");
        foreach ((TraitId id, Map11Relations.Rule r) in
                 conditional.OrderBy(t => t.Id.ToString(), StringComparer.Ordinal))
            write($"| **(b)** | `{id}` | {r.Word} | **{Map11Relations.LabelOf(r.Sign)}** | {r.Mean} |");
        foreach ((TraitId id, string why) in
                 Map11Relations.Unresolved.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
            write($"| (a) | `{id}` | — | — | {why} |");
        write("");
        int inRoster = conditional.Count(t => roster.Contains(t.Id));
        write($"**(b) は {conditional.Count} 本**（うち 52 枚のロスターに保持者がいるのは {inRoster} 本）"
            + $"、**(a) は {Map11Relations.Unresolved.Count} 本**。");
        bool bSome = conditional.Count > 0 && inRoster == conditional.Count;
        ok &= bSome;
        write(bSome
            ? "**(b) の全部に保持者がいる（○）** —— 点線は実際に盤上へ出る。"
            : "**× —— (b) に保持者のいない札がある。**");
        write("");

        // ---------------- Q0-2 ----------------
        write("## Q0-2: 関係の形すべての 得／損／両方");
        write("");
        write("**手書きの表**（`Map11Relations.Rules` の `Sign`）。"
            + "**網羅性は `--map11-phase172` が門にする**ので、ここでは内訳だけを出す。");
        write("");
        write("| 得／損 | 本数 | 札 |");
        write("|:-:|--:|---|");
        foreach (Map11Relations.LinkSign sign in Enum.GetValues<Map11Relations.LinkSign>())
        {
            var hit = Map11Relations.Table.Where(t => t.Rule.Sign == sign)
                                          .OrderBy(t => t.Id.ToString(), StringComparer.Ordinal).ToList();
            write($"| **{Map11Relations.LabelOf(sign)}** | {hit.Count} "
                + $"| {(hit.Count == 0 ? "—" : string.Join(" / ", hit.Select(t => $"`{t.Id}`")))} |");
        }
        write("");
        bool allSigns = Enum.GetValues<Map11Relations.LinkSign>()
                            .All(sg => Map11Relations.Table.Any(t => t.Rule.Sign == sg));
        ok &= allSigns;
        write(allSigns
            ? "**3 つとも実在する（○）。** 「両方」を中立色で逃げずに済んでいる。"
            : "**× —— 使われていない印がある。**");
        write("");

        // ---------------- Q0-3 ----------------
        write("## Q0-3: 線の1語と説明文の語が食い違っている駒（52 枚）");
        write("");
        write("**線の語がそのまま説明文に出てこないと、図と文が別の機構に見える。**"
            + "`Map11Relations.Rules` の `Echo`（その札の保持者の説明文に必ず出てくるはずの語）を"
            + "`UnitCatalog.All` の全保持者に当てる。**落ちた駒は説明文の側を寄せる**（指示書 §1-3 #5）。");
        write("");
        write("| 札 | 語 | 出るはずの語 | 保持者 | 出ているか |");
        write("|---|---|---|---|:-:|");
        int echoMiss = 0, echoRows = 0;
        foreach ((TraitId id, Map11Relations.Rule r) in
                 Map11Relations.Table.OrderBy(t => t.Id.ToString(), StringComparer.Ordinal))
            foreach (UnitDef d in UnitCatalog.All.Where(u => u.Traits.Contains(id)))
            {
                echoRows++;
                bool hit = (d.PlusText + d.MinusText).Contains(r.Echo, StringComparison.Ordinal);
                if (!hit) echoMiss++;
                write($"| `{id}` | {r.Word} | 「{r.Echo}」 | {d.Name} | {(hit ? "**○**" : "**×**")} |");
            }
        write("");
        ok &= echoMiss == 0;
        write(echoMiss == 0
            ? $"**{echoRows} 行すべてで語が一致した（○）。**"
            : $"**{echoMiss} / {echoRows} 行で語が食い違っている（×）。**");
        write("");
        write("**第173期に説明文を直したのは4枚**——囃し立てのヒサ（「標」を入れた）／"
            + "廃棄聖騎士ガルド（「肩代わり」→「庇う（身に受けて肩代わりする）」。"
            + "**「肩代わり」は大喰らいゴルムの線の語なので、2 枚が同じ語を名乗っていた**）／"
            + "墓守リィカ（「戦闘開始時」→「開戦時」）／突き返しのハネ（「腕が鈍る」を入れた）。");
        write("");

        // ---------------- Q0-4 ----------------
        write("## Q0-4: 敵 18 体の `PlusText`");
        write("");
        write("**`--map11-phase0` が門にする**（このマップに出る敵に1体でも空があれば落ちる）。"
            + "ここでは中身を並べる——**同じ名前で数値の違う駒が2組いる**"
            + "（巡礼騎士 攻15 / 攻24、狙撃手 溜めあり / なし）のが、"
            + "**札ごとの1行ではなく駒ごとの文にした理由**である。");
        write("");
        write("| 道 | 部隊 | 席 | 駒 | `Id` | HP/攻/速 | 説明文 |");
        write("|---|---|---|---|---|---|---|");
        int foeCount = 0, foeMiss = 0;
        foreach (IReadOnlyList<Map11.RoadNode> road in Map11.Roads)
            foreach (Map11.RoadNode n in road)
                foreach ((int slot, UnitDef d) in n.Enemy.Occupied())
                {
                    foeCount++;
                    if (d.PlusText.Length == 0) foeMiss++;
                    write($"| {Map11.RoadNames[n.Road]} | {n.Name} | {FormationRules.SeatNames[slot]} "
                        + $"| {d.Name} | `{d.Id}` | {d.MaxHp}/{d.Attack}/{d.Speed} "
                        + $"| {(d.PlusText.Length == 0 ? "**×（空）**" : d.PlusText.Replace("**", ""))} |");
                }
        write("");
        ok &= foeMiss == 0;
        write(foeMiss == 0
            ? $"**{foeCount} 体すべてに説明文がある（○）。** `Flavor` はこの期では空のまま"
              + "（指示書 §1-4：Web 側で書いて次の期に入れる）。"
            : $"**{foeMiss} 体が空（×）。**");
        write("");

        // ---------------- Q0-5 ----------------
        write("## Q0-5: `UnitDef` の文字列を変えても門が動かないこと");
        write("");
        write("| 門 | 読む場所 | 文字列で動くか |");
        write("|---|---|:-:|");
        write("| `compare` の 305 セル | 勝率（`BattleEngine`） "
            + "| **動かない**（`PlusText` / `MinusText` を読む規則は engine に 0 件） |");
        write("| `docs/units.md` | `dump` が **`UnitCatalog.All` だけ**を出す "
            + "| **味方を直せば動く**（第173期は4枚＝文言だけ）。**敵は1行も出ない** |");
        write("| `audit` | 生成物の編成数 | 動かない |");
        write("| `derive rules` | `record struct ...Rule` の宣言 "
            + "| 動かない（説明文は `Rule` の宣言ではない） |");
        write("| `checkup` の (iii) | **`MinusText` の語**（数値のマイナスを拾う） "
            + "| **語を消すと動く**。第173期は `MinusText` を2枚しか触っておらず、"
            + "どちらも語を**足しただけ**（リィカ「戦闘開始時」→「開戦時」・"
            + "ハネに「腕が鈍る」を追記）で、拾う語には1つも当たらない |");
        write("");
        write("**敵に文を入れても生成物が1行も動かない**のは、`dump` が"
            + "`UnitCatalog.All` しか出さないからである（`## ステージ` の節は名前と数値だけ）。");
        write("");

        // ---------------- 自己検査 (d) の規則 ----------------
        write("## 拠点へ戻す（自己検査 (d) の規則）");
        write("");
        write("**第172期まで、道を抜き切った隊は二度と拠点へ戻れなかった**"
            + "——引き返す口は接敵の窓だけで、その窓は `NextNode` が null になると開かない。"
            + "`Road` が立ったままなので `CanReform` も偽のままになる。");
        write("");
        var st = new Map11State(0);
        bool homeNo = !st.CanWithdraw(0);                 // 拠点にいる隊は戻せない（既に拠点）
        st.Send(0, 0);
        bool onRoad = st.CanWithdraw(0) && !st.CanReform(0);
        bool back = st.Withdraw(0);
        bool reformable = st.CanReform(0) && st.Squads[0].Road < 0;
        // 道を抜き切った形（`NextNode` が null）でも戻せること。
        var st2 = new Map11State(0);
        st2.Send(0, 0);
        foreach (Map11State.Node n in st2.Nodes[0]) n.Cleared = true;
        bool clearedRoad = st2.NextNode(0) is null && st2.CanWithdraw(0) && st2.Withdraw(0)
                        && st2.CanReform(0);
        // 全滅した隊は戻せない。
        var st3 = new Map11State(0);
        st3.Send(1, 1);
        st3.Squads[1].Units = null;
        st3.Squads[1].Lost = true;
        bool lostNo = !st3.CanWithdraw(1) && !st3.Withdraw(1);

        write("| # | 規則 | 実測 | 判定 |");
        write("|---|---|---|:-:|");
        write($"| 1 | 拠点にいる隊は「戻す」対象ではない | `CanWithdraw` {(homeNo ? "偽" : "**真**")} "
            + $"| {(homeNo ? "**○**" : "×")} |");
        write($"| 2 | 道の上の隊は戻せて、戻ると組み直せる "
            + $"| 道の上 {(onRoad ? "○" : "×")} / 戻った {(back ? "○" : "×")} "
            + $"/ 組み直せる {(reformable ? "○" : "×")} "
            + $"| {(onRoad && back && reformable ? "**○**" : "×")} |");
        write($"| 3 | **抜け切った道にいる隊も戻せる**（第173期 §1-2 の直し） "
            + $"| {(clearedRoad ? "戻せた" : "**戻せない**")} | {(clearedRoad ? "**○**" : "×")} |");
        write($"| 4 | 全滅した隊は戻せない | {(lostNo ? "拒否" : "**通った**")} "
            + $"| {(lostNo ? "**○**" : "×")} |");
        bool rules = homeNo && onRoad && back && reformable && clearedRoad && lostNo;
        ok &= rules;
        write("");
        write("**書き換えるのは `Road` だけ**——`Units` / `Home` / `Cleared` / `Deployed` は"
            + "1ビットも動かさない（`Home` を動かすと 2 本抜きの分子が変わる）。"
            + "**戻すこと自体に代金は無い**（時間の代金は次の期の話・指示書 §5）。");
        write("");

        // ---------------- 自己検査 (e) ----------------
        write("## ボルグをリィカの隣に置くと損の線が出るか（自己検査 (e)）");
        write("");
        write("第172期の観察ログ（問い5）「**ボルグをリィカ軸へ入れたら、ボルグの自傷でリィカが落ちた。"
            + "図にその関係が1本も無かった**」そのものを、席に当てて確かめる。");
        write("");
        var probe = new Dictionary<int, UnitDef>
        {
            [0] = UnitCatalog.Borg,   // 前1
            [2] = UnitCatalog.Rica,   // 中央（前1 と隣接）
        };
        var drawn = Map11Relations.Of(probe);
        write("| 向き | 語 | 得／損 | 点線 |");
        write("|---|---|:-:|:-:|");
        foreach (Map11Relations.Link l in drawn)
            write($"| {UnitCatalog.All.First(u => u.Id == probe[l.From].Id).Name} → "
                + $"{probe[l.To].Name} | {l.Word} | **{Map11Relations.LabelOf(l.Sign)}** "
                + $"| {(l.Conditional ? "┄" : "—")} |");
        write("");
        int loss = drawn.Count(l => l.Sign == Map11Relations.LinkSign.Loss && l.Conditional);
        bool probeOk = loss >= 2;
        ok &= probeOk;
        write(probeOk
            ? $"**損の点線が {loss} 本出る（○）** —— 巻き込みと火の粉。第172期は 0 本だった。"
            : $"**× —— 損の点線が {loss} 本しか出ない。**");
        write("");

        write(ok ? "**Phase 0 ○**" : "**Phase 0 × —— 上の × を埋めること。**");
        return ok;
    }
}
