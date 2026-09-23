using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// =====================================================================================
// 頭なしの通し確認（第169期・自己検査 (c)）
//
// **`Map11State` を、第168期 `BandOnce` と同じ自動規則で回す。**
// 遊ぶ側（`Map11Main`）と<b>同じクラス・同じメソッド</b>を通るので、
// 「器具の数字が出る」ことがそのまま「マップの進行が器具と同じ規則である」ことになる。
//
// 違いは1つだけ——**誰が「どの隊をどの道へ」を決めるか**。ここでは器具と同じ固定の割り当て
// （正: カド隊 → 南 ／ ハネ隊 → 北。逆はその入れ替え）で、控えは「隊が全滅した道へ1回だけ」。
//
//     Godot_console.exe --path DemoApp --headless -- --map11-verify
//     Godot_console.exe --path DemoApp --headless -- --map11-verify=200   # seed 本数を変える
// =====================================================================================

public static class Map11Verify
{
    /// <summary>1 帯あたりの seed 本数（第168期 <c>BandSeeds</c> と同じ）。</summary>
    public const int DefaultSeeds = 800;

    /// <summary>第168期 部B `S4/S4` × 回復 50% の実測（自己検査 (c) の相手）。</summary>
    public const double ExpectStraight = 95.6;
    public const double ExpectCross = 32.0;
    public const double ExpectRallyCross = 38.5;
    public const double ExpectTwoKadoSouth = 40.4;
    public const double ExpectTwoHaneNorth = 38.0;
    public const double Tolerance = 3.0;

    private readonly record struct Once(
        int Cleared, double Partial, bool TwoRunA, bool TwoRunB, bool ReserveUsed, bool ReserveCleared);

    public sealed record Stat(double Full, double Partial, double TwoA, double TwoB,
                              int ReserveN, double Rally);

    /// <summary>
    /// 1 マップぶん自動で回す。<b>第168期 <c>BandOnce</c> の進行をそのまま写した</b>
    /// ——隊 0 → 1 → 2 の順に 1 戦ずつ、担当の道が抜け切っていれば残っている道へ回る。
    /// </summary>
    private static Once RunOne(int[] assign, int seed)
    {
        var st = new Map11State(seed);
        st.Send(0, assign[0]);
        st.Send(1, assign[1]);
        bool usedReserve = false;

        while (st.Battles < Map11.BattleCap)
        {
            bool acted = false;
            for (int s = 0; s < st.Squads.Length; s++)
            {
                Map11State.Squad sq = st.Squads[s];
                if (sq.Units is null || sq.Road < 0) continue;

                // 担当の道が抜け切っていたら、残っている道のいちばん手前の番号へ回る
                // （`Home` は動かさない——2 本抜きは担当の道でしか数えない）。
                if (st.NextNode(sq.Road) is null)
                {
                    int alt = -1;
                    for (int road = 0; road < Map11.RoadCount; road++)
                        if (st.NextNode(road) is not null) { alt = road; break; }
                    if (alt < 0) break;
                    sq.Road = alt;
                }

                if (st.Prepare(s) is not { } prep) continue;
                BattleResult r = BattleEngine.Run(prep.Players, prep.Enemies, prep.Seed, verbose: false);
                acted = true;
                st.Resolve(s, prep.Node, r.PlayerWon);

                // 隊が全滅したら、控えがその道の続きへ **1 回だけ** 出る。
                if (sq.Units is null && !usedReserve && st.Squads[^1].Units is null
                    && !st.Squads[^1].Deployed)
                {
                    usedReserve = true;
                    st.Send(st.Squads.Length - 1, prep.Node.Def.Road);
                }

                if (st.AllCleared || st.Squads.All(x => x.Units is null)) goto fin;
            }
            if (!acted) break;
        }
    fin:
        Map11State.Squad res = st.Squads[^1];
        bool rallied = usedReserve && res.Home >= 0 && st.NextNode(res.Home) is null;
        return new Once(st.ClearedCount, st.Partial(),
                        st.Squads[0].Cleared >= 2, st.Squads[1].Cleared >= 2,
                        usedReserve, rallied);
    }

    /// <summary>
    /// <b>第170期 Phase 0</b> —— 説明文の出どころと帳簿を<b>実装から引き直す</b>（戦闘0回）。
    /// 手書きの <see cref="Map11Info"/> の札に欠けがあれば名指しで落ちる。
    /// </summary>
    public static bool Phase0(Action<string> write)
    {
        write("# 第170期 Phase 0 —— 情報の出どころ（戦闘0回）");
        write("");

        // ---- Q0-1a: 味方 15 枚 ----
        write("## Q0-1a: 味方 15 枚の説明文の出どころ（`UnitDef.PlusText` / `MinusText` / `Flavor`）");
        write("");
        write("| 隊 | 席 | 駒 | プラス | マイナス | 由来 |");
        write("|---|---|---|:-:|:-:|:-:|");
        int allyMissing = 0;
        foreach (Map11.SquadDef s in Map11.Squads)
            foreach ((int slot, UnitDef d) in s.F.Occupied())
            {
                bool p = d.PlusText.Length > 0, m = d.MinusText.Length > 0, f = d.Flavor.Length > 0;
                if (!p || !m || !f) allyMissing++;
                write($"| {s.Name} | {FormationRules.SeatNames[slot]} | {d.Name} "
                    + $"| {(p ? "○" : "**×**")} | {(m ? "○" : "**×**")} | {(f ? "○" : "**×**")} |");
            }
        write("");
        write(allyMissing == 0
            ? "**15 枚すべてに3つとも入っている。手書きは0行で済む。**"
            : $"**{allyMissing} 枚に欠けがある。**");
        write("");

        // ---- Q0-1b: 敵 18 体 ----
        write("## Q0-1b: 敵 18 体の説明文の出どころ");
        write("");
        write("| 道 | 部隊 | 席 | 駒 | HP/攻/速 | 札 | プラス | 由来 |");
        write("|---|---|---|---|---|---|:-:|:-:|");
        int foeCount = 0, foeWithText = 0;
        var seen = new HashSet<TraitId>();
        var foeMissing = new List<string>();
        foreach (IReadOnlyList<Map11.RoadNode> road in Map11.Roads)
            foreach (Map11.RoadNode n in road)
                foreach ((int slot, UnitDef d) in n.Enemy.Occupied())
                {
                    foeCount++;
                    if (d.PlusText.Length > 0) foeWithText++;
                    else if (!foeMissing.Contains(d.Name)) foeMissing.Add(d.Name);
                    foreach (TraitId t in d.Traits) seen.Add(t);
                    write($"| {Map11.RoadNames[n.Road]} | {n.Name} | {FormationRules.SeatNames[slot]} "
                        + $"| {d.Name} | {d.MaxHp}/{d.Attack}/{d.Speed} "
                        + $"| {(d.Traits.Count == 0 ? "—" : string.Join(" ", d.Traits))} "
                        + $"| {(d.PlusText.Length > 0 ? "○" : "**×**")} "
                        + $"| {(d.Flavor.Length > 0 ? "○" : "**×**")} |");
                }
        write("");
        write($"**敵 {foeCount} 体のうち、説明文を持つのは {foeWithText} 体。**");
        write("");
        write("**第173期 §1-4 にこの列を埋めた**（それまでは 0 体で、`DemoApp` 側が "
            + "`TraitId` ごとの1行を手書きで持っていた）。**手書きは 0 行になった**"
            + "——同じ名前の駒が数値違いで2体いる（巡礼騎士 攻15 / 攻24、狙撃手 溜めあり / なし）ので、"
            + "**札ごとの1行では書き分けられない**というのが、駒ごとの文にした理由である。");
        write("");

        // ---- 説明文の網羅性（第173期 §1-4 で手書きの表から `UnitDef` へ移した） ----
        write("## 説明文の網羅性（`UnitDef.PlusText`・このマップに出る敵）");
        write("");
        write($"このマップの敵が持つ札は **{seen.Count} 種**"
            + $"（{string.Join(" / ", seen.OrderBy(t => t.ToString()))}）。"
            + "**札ごとではなく駒ごとに文があること**を数える。");
        write(foeMissing.Count == 0
            ? $"**{foeCount} 体すべてに `PlusText` がある。**"
            : $"**`PlusText` が空の駒: {string.Join(" / ", foeMissing)}**");
        write("");
        write("**`MinusText` / `Flavor` は空のままでよい**（指示書 §1-4：無理に作らない）。"
            + "フレーバーは Web 側で書いて次の期に入れる。");
        write("");

        // ---- Q0-2〜Q0-4: 帳簿 ----
        write("## Q0-2〜Q0-4: 帳簿から引ける量");
        write("");
        write("| # | 量 | 引く場所 | 陣営別か | 使う |");
        write("|---|---|---|:-:|:-:|");
        write("| Q0-2 | `BattleResult` を `Map11` 側で読めるか | `Main.EnterBattle` が持つ `_result` を"
            + " `Map11Session.CompleteBattle` に渡す | — | **○** |");
        write("| Q0-2 | 粛が止めた回数 | `BoardRules.HushBlocked[1]` | ○（0=敵 / 1=味方） | **○** |");
        write("| Q0-2 | 渇きが止めた回復 | `BoardRules.DroughtHits[1]` / `DroughtEffective[1]` | ○ | **○** |");
        write("| Q0-3 | 軛が切った一撃 | `Yoke.CutOnPlayerHits` / `CutOnPlayerLost` | ○（専用の2組） | **○** |");
        write("| Q0-2 | 保持者が倒れたターン | `BoardRules.HolderFallTurn[RuleIndex]` | — | **○** |");
        write("| Q0-4 | 断罪（痺れさせた回数） | **帳簿は無い**（`BattleEventKind.Stun` の台本イベントだけ） "
            + "| — | × |");
        write("| Q0-4 | 殉教者の庇い | `TallyByUnit[\"martyr\"].Intercepts`（**敵の駒も入っている**） | — "
            + "| ×（この期では出さない） |");
        write("| Q0-4 | 曝き | `ExposeCount`（`ExposeRule.Default` は無効なので常に 0） | — | × |");
        write("");
        write("**第五波の断罪・殉教は §1-4 には出さない**（指示書 Q0-4: 帳簿はこの期では足さない）。"
            + "殉教の庇いは `Intercepts` に数字があるが、**敵味方を混ぜた駒ごとの表**なので"
            + "「味方側の数字だけ」という §1-4 の書式に合わない。");
        write("");

        // ---- §4: 封じを戦闘中に見せるための材料 ----
        write("## §4（調べるだけ）—— 「封じられている」を戦闘中に見せるには");
        write("");
        write("| # | 問い | 実装から引いた答え |");
        write("|---|---|---|");
        write("| S-1 | 粛が止めた瞬間、台本にイベントは出ているか | **出ていない。ログの文字列にも無い。** "
            + "`BattleContext.CanActOutOfTurn` は `NoteHushBlocked`（計数）を呼ぶだけ。"
            + "**足すなら1箇所**——同メソッドの `if (hushed)` の中（`sole` が真のときだけ） |");
        write("| S-2 | 渇き | **出ていない。ログにも無い。** `Heal` 入口の `if (DroughtBinding)` の中が1箇所 |");
        write("| S-2 | 軛 | **ログの文字列には出ている**（`LogKind.Trigger`「軛が … を切った」）が、"
            + "**台本（`BattleEvent`）には無い**。`ApplyDamage` の `if (yokeBinding)` の中が1箇所 |");
        write("| S-3 | 「保持者が生きている間ずっと封じられている」を再生側が描けるか | **描ける。**"
            + "保持者の生死は `Death` イベントで分かり、どの駒が保持者かは `Map11Info.IsBoardRuleHolder` で引ける。"
            + "ただし `DemoOpening` は `Traits` を運んでいないので、**そこに1フィールド足す必要がある** |");
        write("| S-4 | 知らないイベントを再生側は無視できるか | **できる。** `Main.ApplyEvent` の "
            + "`switch (e.Kind)` に `default:` が無いので、未知の種類は何もせず素通りする |");
        write("");
        write("**`BattleEventKind` を1つ足すと `docs/watch.md` が動く**"
            + "（`watch` の盲点表が `Enum.GetNames(typeof(BattleEventKind))` を列挙している）。"
            + "**この期では足さない。**");
        write("");

        // ---- Q0-5 ----
        write("## Q0-5: 勝ち筋の下書きを書くために読んだ場所");
        write("");
        write("`Presets.Compare` の各行の注記と、各駒の `UnitDef.PlusText`。**3本とも報告書に貼ってある。**");
        write("");
        foreach (Map11.SquadDef s in Map11.Squads)
            write($"- **{s.Name}**: {Map11Info.PlanOf(s.Id)}");
        write("");

        // ---- §1-4 の3本が実際に出るか（1戦ずつ回して確かめる） ----
        write("## §1-4 の3本が実際に出るか（3 隊 × 4 区画を1戦ずつ）");
        write("");
        write("**3つとも「0 なら出さない」ので、出ないこと自体は不具合ではない。**"
            + "ここで確かめるのは**出るべき局面で出るか**。");
        write("");
        write("| 隊 | 相手 | 盤面ルール | 出た文 |");
        write("|---|---|---|---|");
        var fired = new HashSet<string>();
        foreach (Map11.SquadDef sq in Map11.Squads)
            foreach (IReadOnlyList<Map11.RoadNode> road in Map11.Roads)
                foreach (Map11.RoadNode n in road)
                {
                    // 1戦だけ。**盤面には何も残さない**（この場で作って捨てる）。
                    var pu = BattleEngine.Materialize(sq.F, BattleContext.PlayerTeam);
                    var eu = BattleEngine.Materialize(n.Enemy, BattleContext.EnemyTeam, Map11.EnemyScale);
                    BattleResult res = BattleEngine.Run(pu, eu, 0, verbose: false);
                    var notes = Map11Info.RuleNotes(res);
                    if (notes.Count == 0) continue;   // 「0 なら出さない」ので行にもしない
                    foreach (string x in notes)
                        foreach (string key in new[] { "粛", "渇き", "軛" })
                            if (x.Contains(key, StringComparison.Ordinal)) fired.Add(key);
                    write($"| {sq.Name} | {n.Name} | {Map11.RuleLineOf(n.Enemy)} "
                        + $"| {string.Join(" ／ ", notes.Select(x => x.Trim()))} |");
                }
        write("");
        write(fired.Count == 3
            ? "**3つとも出た**（粛・渇き・軛）。"
            : $"出たのは {fired.Count} 種（{string.Join(" / ", fired)}）"
              + "——残りは seed 0 のこの1戦では起きなかっただけで、読む場所は同じ3つの帳簿。");
        write("");

        bool ok = allyMissing == 0 && foeMissing.Count == 0;
        write(ok ? "**Phase 0 ○ —— 味方も敵も説明文の出どころは `UnitDef` の1本で、網羅性は機械で確かめられる。**"
                 : "**Phase 0 × —— 上の × を埋めること。**");
        return ok;
    }

    public static Stat Band(int[] assign, int seed0, int seeds)
    {
        var o = new Once[seeds];
        Parallel.For(0, seeds, i => o[i] = RunOne(assign, seed0 + i));
        int rn = o.Count(x => x.ReserveUsed);
        return new Stat(
            100.0 * o.Count(x => x.Cleared >= Map11.TotalNodes) / seeds,
            o.Average(x => x.Partial),
            100.0 * o.Count(x => x.TwoRunA) / seeds,
            100.0 * o.Count(x => x.TwoRunB) / seeds,
            rn, rn == 0 ? 0 : 100.0 * o.Count(x => x.ReserveCleared) / rn);
    }

    /// <summary>標準出力へ表を出し、自己検査 (c) の合否を返す。</summary>
    public static bool Report(int seeds, Action<string> write)
    {
        int[] straight = { 1, 0 };   // 正: カド隊 → 南(1) ／ ハネ隊 → 北(0)
        int[] cross = { 0, 1 };      // 逆

        Stat p = Band(straight, 0, seeds);
        Stat q = Band(cross, 0, seeds);

        write("# 第169期 自己検査 (c) —— `DemoApp` の進行ロジックを頭なしで回す");
        write("");
        write($"seed 0..{seeds - 1}。道中の回復 {Map11.RecoverPercent}%。"
            + "隊・道・控えの出し方は第168期 部B の自動規則と同じ。");
        write("");
        write("| 量 | 器具（第168期 `S4/S4` × 50%） | `DemoApp` | 差 | 線 ±3.0pt |");
        write("|---|--:|--:|--:|:-:|");

        bool ok = true;
        void Row(string name, double want, double got)
        {
            double d = got - want;
            bool hit = Math.Abs(d) <= Tolerance;
            ok &= hit;
            write($"| {name} | {want:F1}% | **{got:F1}%** | {d:+0.0;-0.0;0.0} | {(hit ? "**○**" : "×")} |");
        }
        // 隊 1 は第172期に「ハネ隊」→「かき回し隊」へ改名した（指示書 §3）。
        // **器具（第168期）の行名も併記する**——過去の表と突き合わせるときに追えなくなるため。
        Row("踏破率 正（カド隊→南／かき回し隊→北）", ExpectStraight, p.Full);
        Row("踏破率 逆", ExpectCross, q.Full);
        Row("2 本抜き カド隊×南（正）", ExpectTwoKadoSouth, p.TwoA);
        Row("2 本抜き かき回し隊×北（正・第168期の「ハネ隊」）", ExpectTwoHaneNorth, p.TwoB);
        Row("挽回率 逆", ExpectRallyCross, q.Rally);
        write("");
        write($"部分点 正 {p.Partial:F3} ／ 逆 {q.Partial:F3}（対比 {p.Partial - q.Partial:+0.000;-0.000}）。"
            + $"控えが出た試行 正 {p.ReserveN} / {seeds} ／ 逆 {q.ReserveN} / {seeds}。");
        write("");
        write(ok ? "**(c) ○ —— 5 つの量すべてが ±3.0pt 以内。**"
                 : "**(c) × —— 帯から外れた量がある。**");
        return ok;
    }
}
