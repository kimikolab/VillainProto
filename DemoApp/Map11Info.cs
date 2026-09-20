using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 情報の層（第170期）—— **仕組みは1つも足さない。読むだけ。**
//
// `Map11State`（進行の規則）には1行も触らない——(c) の 0.0pt 一致を保つ（R259）。
//
// 出どころ:
//   味方15枚の説明     `UnitDef.PlusText` / `MinusText` / `Flavor`（`docs/units.md` の生成元そのもの）
//   敵18体の説明       **元データが無い**（敵の `UnitDef` は3つとも空文字）。
//                      代わりに `TraitId` ごとの1行を下の `TraitLines` に持つ。**ここだけ手書き**
//   盤面ルールの実績   `BattleResult.BoardRules`（粛・渇き）と `BattleResult.Yoke`（軛）
//
// 手書きの `TraitLines` は `Map11Verify.Phase0` が**網羅性を機械で確かめる**
// ——このマップに出る駒の札に1つでも欠けがあれば名指しで落ちる。
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

    // ---------------- 札の1行（敵に元データが無いぶんを埋める） ----------------

    /// <summary>
    /// <b>ここだけ手書き。</b> 敵の <see cref="UnitDef"/> は <c>PlusText</c> / <c>MinusText</c> /
    /// <c>Flavor</c> がすべて空なので、<see cref="TraitId"/> ごとに1行を持つしかない。
    ///
    /// <para><b>用語は括弧の中を主、名前を従にする</b>（指示書 §1-3）——
    /// 「ターン外の行動が止まる（粛）」の順。観察ログの「()で効果を書いてくれていたから
    /// なんとか分かった」に合わせた。</para>
    ///
    /// <para><b>網羅性は `Map11Verify.Phase0` が機械で確かめる。</b>
    /// このマップに出る駒の札が1つでも欠ければ名指しで出る。</para>
    /// </summary>
    private static readonly Dictionary<TraitId, string> TraitLines = new()
    {
        // --- 盤面ルール（保持者が倒れると消える） ---
        [TraitId.Hush] = "ターン外の行動が止まる（粛）—— 反撃・追撃・割り込みが両軍とも出なくなる",
        [TraitId.Drought] = "回復が通らない（渇き）—— 両軍とも、どんな回復も1点も入らない",
        [TraitId.Yoke] = "1発が 25 で切られる（軛）—— 両軍とも、1回のダメージが 25 を超えない",
        [TraitId.Inversion] = "行動順が逆さになる（逆位）—— 遅い駒から動く",

        // --- 敵側の札 ---
        [TraitId.Condemn] = "反撃してきた相手を痺れさせる（断罪）—— ターン外に動く駒だけが代金を払う",
        [TraitId.Martyr] = "味方への単体攻撃に割り込んで身代わりになる（殉教）—— 薙ぎ・貫き・全体は素通りする",
        [TraitId.Expose] = "殴ったあと、敵陣の駒を引きずり出す（曝き）",
        [TraitId.Executioner] = "1体倒すたびに攻撃力が上がる（処刑）—— 放っておくと後半ほど重くなる",

        // --- 味方側にも出る札（中身のパネルの補助。PlusText と重ねて出す） ---
        [TraitId.Immobile] = "自分からは決して攻撃しない（不動）",
        [TraitId.Stoic] = "1体を選ぶ回復・強化を受け取らず、隣の味方へ流す（支援拒否）",
        [TraitId.Ephemeral] = "戦闘が終わると消える（儚い）—— 蘇生されず、次の戦闘へ持ち越さない",
    };

    public static string? TraitLineOf(TraitId id) => TraitLines.GetValueOrDefault(id);

    /// <summary>その駒の札のうち、1行が書いてあるものだけを並べる。</summary>
    public static IEnumerable<string> TraitLinesOf(UnitDef def)
        => def.Traits.Select(TraitLineOf).Where(s => s is not null)!;

    /// <summary>盤面ルールの札を持っているか（「この駒が倒れるとルールが消える」の印）。</summary>
    public static bool IsBoardRuleHolder(UnitDef def)
        => def.Traits.Any(t => t is TraitId.Hush or TraitId.Drought or TraitId.Yoke or TraitId.Inversion);

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
