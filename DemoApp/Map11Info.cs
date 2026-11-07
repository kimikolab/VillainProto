using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 情報の層（第170期）—— **仕組みは1つも足さない。読むだけ。**
//
// `Map11State`（進行の規則）には1行も触らない——(c) の 0.0pt 一致を保つ（R259）。
//
// 出どころ（**第173期に敵側も `UnitDef` へ寄せた**）:
//   味方15枚の説明     `UnitDef.PlusText` / `MinusText` / `Flavor`（`docs/units.md` の生成元そのもの）
//   敵18体の説明       **第173期 §1-4 で `EnemyCatalog` の定義に入れた**（それまでは全員空で、
//                      ここが `TraitId` ごとの1行を手書きで持っていた）。**手書きは0行になった**
//   盤面ルールの実績   `BattleResult.BoardRules`（粛・渇き）と `BattleResult.Yoke`（軛）
//
// 網羅性は `Map11Verify.Phase0` が**機械で確かめる**
// ——このマップに出る駒の説明文に1体でも欠けがあれば名指しで落ちる（R260）。
// =====================================================================================

public static class Map11Info
{
    // ---------------- 隊の勝ち筋の一行（指示書 §1-1・案B） ----------------

    /// <summary>
    /// <b>隊がどうやって勝つかだけ</b>を書く。**向いている道・苦手な相手は書かない**（案B）。
    ///
    /// <para><b>敵の表示と同じ語を使う</b>のが要点——「ターン外の行動」「回復」「1発」は、
    /// それぞれ 粛／渇き／軛 の括弧書きと<b>同じ語</b>。プレイヤーが2つを結べるのは語が同じときだけ。</para>
    ///
    /// <para>下書きは `Presets.Compare` の行の注記と各特性の <c>PlusText</c> から起こしたもの。
    /// <b>ポンが直す前提。</b></para>
    /// </summary>
    private static readonly Dictionary<string, string> Plans = new(StringComparer.Ordinal)
    {
        ["kado"] =
            "殴られて返す隊。ヒサが敵の矛先をカドへ集め、カドが**ターン外の行動**（反撃）で削る。"
            + "ガルドが単体攻撃を庇い、リリの**吸い取り**で保たせる。",
        ["hane"] =
            "隊列をかき回す隊。バサが毎ターン敵と味方を入れ替え、ヨミが動かされるたび"
            + "**ターン外の行動**（割り込み）で刺す。ハネの巻き添えで腕が鈍るほどウツが伸び、"
            + "ドルガの重い**1発**が薙ぎ払う。",
        ["hold"] =
            "味方の死で伸びる隊。ゾトが倒れて破裂し火を撒き、ヒヨが燃えている味方を強め、"
            + "リィカは味方が倒れるたび厚くなる。ヴェルが2度まで縫い戻し、"
            + "ゴルムは倒れるとき**回復**を残す。",
    };

    public static string PlanOf(string squadId) => Plans.GetValueOrDefault(squadId, "");

    // ---------------- 駒の1行（第173期 §1-4 に出どころを1つへ寄せた） ----------------

    /// <summary>
    /// その駒の説明文（味方も敵も同じ <see cref="UnitDef"/> から引く）。
    ///
    /// <para><b>第173期に手書きをやめた。</b> 第170期は敵の <c>PlusText</c> が18体とも空だったので、
    /// ここが <c>TraitId</c> ごとの1行を手書きで持っていた——しかし
    /// <b>同じ名前の駒が数値違いで2体いる</b>（巡礼騎士 攻15 / 攻24、狙撃手 溜めあり / なし）ので、
    /// 札ごとの1行では書き分けられない。第173期 §1-4 で
    /// <b>`EnemyCatalog` の定義そのものに文を入れた</b>ので、
    /// <b>味方と敵で出どころが同じ1本になった</b>（写しを持たない・自己検査 (b)）。</para>
    ///
    /// <para><b>網羅性は `Map11Verify.Phase0` が機械で確かめる</b>——このマップに出る駒に
    /// 1体でも空があれば名指しで落ちる（R260）。</para>
    /// </summary>
    public static IEnumerable<string> LinesOf(UnitDef def)
    {
        if (def.PlusText.Length > 0) yield return def.PlusText.Replace("**", "");
        if (def.MinusText.Length > 0) yield return "代わりに: " + def.MinusText.Replace("**", "");
    }

    /// <summary>
    /// 盤面ルールの札を持っているか（「この駒が倒れるとルールが消える」の印）。
    /// <b>第171期に <see cref="BoardRuleTags"/> へ寄せた</b>——戦闘中の保持者の札と
    /// マップの ★ が別々の一覧を持つと、片方だけ静かに古くなる（第124期 §4）。
    /// </summary>
    public static bool IsBoardRuleHolder(UnitDef def) => BoardRuleTags.IsHolder(def.Traits);

    // ---------------- 戦闘の後の1行（指示書 §1-4） ----------------

    /// <summary>倒れた味方の駒 1 枚の、倒れた一撃の出どころ（第467期 §3・<see cref="DownsOf"/>）。</summary>
    public enum DownCause { Foe, AllyShock, AllyOther, Unknown }

    /// <summary>
    /// 1 戦の台本から、<paramref name="players"/>（この戦闘の味方の <c>InstanceId</c>）のうち倒れた駒ごとに、倒れた一撃の出どころを分ける（第467期 §3・<b>読むだけ</b>）。
    /// 倒れた直前の、その駒への最後の <c>Damage</c> の出どころ（<c>ActorId</c>）が味方（自分・この戦闘で喚ばれた味方の側の駒を含む）で、その一撃が<b>雷・粉の連鎖</b>（直前の出来事を
    /// <c>Damage</c> ／ <c>Heal</c> ／ <c>Death</c> ／ <c>StatusGain</c> をまたいでさかのぼって、最初に当たるのが <c>Discharge</c>（放電）・<c>ShockGauge</c>（粉塵爆発・焼など感電の計器）・
    /// <c>ShockSpent</c>（起爆））なら <see cref="DownCause.AllyShock"/>。<c>Death</c> の倒した側（<c>ActorId</c>）は連鎖を起こした敵になるので読まない。
    /// 台本の無い戦闘（<c>verbose: false</c>）は空。
    /// </summary>
    public static List<(int Id, DownCause Cause)> DownsOf(BattleResult r, IReadOnlySet<int> players)
    {
        var list = new List<(int, DownCause)>();
        var ev = r.Events;
        // 出どころの味方には、この戦闘で喚ばれた味方の側の駒（召喚獣など）も入れる（倒れた枚数には入れない）。
        var side = new HashSet<int>(players);
        foreach (BattleEvent s in ev)
            if (s.Kind == BattleEventKind.Summon && s.TargetId is int sid && s.Team == BattleContext.PlayerTeam) side.Add(sid);
        for (int i = 0; i < ev.Count; i++)
        {
            BattleEvent e = ev[i];
            if (e.Kind != BattleEventKind.Death || e.TargetId is not int dead || !players.Contains(dead)) continue;
            int j = i - 1;
            while (j >= 0 && !(ev[j].Kind == BattleEventKind.Damage && ev[j].TargetId == dead)) j--;
            if (j < 0 || ev[j].ActorId is not int src) { list.Add((dead, DownCause.Unknown)); continue; }
            if (!side.Contains(src)) { list.Add((dead, DownCause.Foe)); continue; }
            int k = j - 1;
            while (k >= 0 && ev[k].Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Death or BattleEventKind.StatusGain) k--;
            bool shock = k >= 0 && ev[k].Kind is BattleEventKind.Discharge or BattleEventKind.ShockGauge or BattleEventKind.ShockSpent;
            list.Add((dead, shock ? DownCause.AllyShock : DownCause.AllyOther));
        }
        return list;
    }

    /// <summary>味方の雷・粉で倒れた駒の枚数（第467期 §3・戦闘の記録の「（味方の雷で n 枚）」）。</summary>
    public static int AllyShockDowns(BattleResult r, IReadOnlySet<int> players) => DownsOf(r, players).Count(d => d.Cause == DownCause.AllyShock);

    /// <summary>
    /// <b>盤面ルールが実際に何をしたか</b>を、味方側の数字だけで並べる。
    /// <b>評価の言葉は1つも書かない</b>（案B）——「相性が悪い」「送り先を間違えた」は出さない。
    ///
    /// <para><b>0 のルールは出さない。</b> 出すのは起きたことだけ。</para>
    ///
    /// <para>数字は <see cref="BattleResult"/> の帳簿から引くだけで、写しを持たない
    /// （自己検査 (e)）。陣営の添字は <c>0 = 敵</c> / <c>1 = 味方</c>。</para>
    /// </summary>
    public static List<string> RuleNotes(BattleResult r)
    {
        const int Player = 1;
        var notes = new List<string>();

        BoardRuleLedger b = r.BoardRules;

        long hush = b.HushBlocked[Player];
        if (hush > 0) notes.Add($"ターン外の行動が {hush} 回止められた（粛）");

        long droughtHits = b.DroughtHits[Player];
        if (droughtHits > 0)
            notes.Add($"回復が {droughtHits} 回（計 {b.DroughtEffective[Player]} 点）通らなかった（渇き）");

        // **`CutOnPlayer` ではなく `CutOnEnemy` を読む。** 添字は「切られた一撃が<b>当たった側</b>」で、
        // `CutOnPlayer` は<b>敵の大振りが切られた回数</b>＝味方の得になるほう。
        // 粛・渇きと揃えて「味方が失ったもの」を出すなら、読むのは
        // <b>味方が振った一撃が切られた回数</b>（＝敵に当たった側）である
        // ——第132期「軛は実質プレイヤー専用の税」が数えたのもこちら。
        long yokeHits = r.Yoke.CutOnEnemyHits;
        if (yokeHits > 0)
            notes.Add($"味方の一撃が 25 で切られた: {yokeHits} 回（計 {r.Yoke.CutOnEnemyLost} 点ぶん）（軛）");

        // 保持者の行は、そのルールが実際に何かをしたときだけ添える。
        AddHolder(BoardRuleLedger.RuleIndex.Hush, hush > 0, "粛");
        AddHolder(BoardRuleLedger.RuleIndex.Drought, droughtHits > 0, "渇き");
        AddHolder(BoardRuleLedger.RuleIndex.Yoke, yokeHits > 0, "軛");

        return notes;

        void AddHolder(BoardRuleLedger.RuleIndex idx, bool fired, string name)
        {
            if (!fired || b.HolderCount[(int)idx] == 0) return;
            int t = b.HolderFallTurn[(int)idx];
            notes.Add(t > 0
                ? $"　{name}の保持者は {t} ターン目に倒れた"
                : $"　{name}の保持者は最後まで倒せなかった");
        }
    }
}
