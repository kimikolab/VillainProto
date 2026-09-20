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
            + "ガルドが単体攻撃を庇い、ノノの**回復**で保たせる。",
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
