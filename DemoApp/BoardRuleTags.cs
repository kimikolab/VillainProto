using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 盤面ルールの札（第171期）—— **表示専用。判定は1つも持たない。**
//
// 「この駒が生きている間、両軍ともこれを封じられている」を画面に出すための、
// `TraitId` → 1語 の対応。**この期に手書きしたのはこの表だけ**で、
// 網羅性は `Map11Verify.Phase0` が `SealedLabels.All` と突き合わせて機械で確かめる（R260）。
//
// **同じ言葉の表を2つ作らない**（第124期 §4）——`Map11Info.IsBoardRuleHolder` も
// `Map11Main` の敵パネルも、盤面ルールかどうかの判定はここ1箇所から引く。
// =====================================================================================

public static class BoardRuleTags
{
    /// <summary>
    /// 盤面ルールの札と、画面に出す1語。<b>語は <see cref="SealedLabels"/> と同じ文字列</b>
    /// ——封じの瞬間の表示（「粛」）と保持者の札（「★ 粛」）が違う語だと、
    /// プレイヤーが2つを結べない（第170期 §1-1 の「同じ語を使う」と同じ理由）。
    ///
    /// <para><b>逆位（<see cref="TraitId.Inversion"/>）だけは <see cref="SealedLabels"/> に相手がいない</b>
    /// ——行動順を反転させるだけで何も「止めない」ので、封じの瞬間というものが無い。
    /// 保持者の札としては出す。</para>
    /// </summary>
    private static readonly Dictionary<TraitId, string> Tags = new()
    {
        [TraitId.Hush] = SealedLabels.Hush,
        [TraitId.Drought] = SealedLabels.Drought,
        [TraitId.Yoke] = SealedLabels.Yoke,
        [TraitId.Inversion] = "逆位",
    };

    /// <summary>札の一覧（<c>TraitId</c> と語の対）。走査はここを引くこと。</summary>
    public static IEnumerable<KeyValuePair<TraitId, string>> All => Tags;

    /// <summary>その札が盤面ルールなら1語、そうでなければ null。</summary>
    public static string? TagOf(TraitId id) => Tags.GetValueOrDefault(id);

    /// <summary>その駒が持つ盤面ルールの語（複数持っていれば全部）。</summary>
    public static IEnumerable<string> TagsOf(IEnumerable<TraitId>? traits)
        => traits is null ? Array.Empty<string>()
                          : traits.Select(TagOf).Where(t => t is not null).Select(t => t!).Distinct();

    /// <summary>盤面ルールの保持者か。</summary>
    public static bool IsHolder(IEnumerable<TraitId>? traits)
        => traits is not null && traits.Any(t => Tags.ContainsKey(t));

    /// <summary>駒の上に出す札（「★ 粛」）。持っていなければ空。</summary>
    public static string LabelFor(IEnumerable<TraitId>? traits)
    {
        string[] tags = TagsOf(traits).ToArray();
        return tags.Length == 0 ? "" : "★ " + string.Join(" / ", tags);
    }
}
