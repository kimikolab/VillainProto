using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 軸への投資（第467期・指示書 design/PHASE467_INVEST_SPEC.md §1・1 章だけ）
//
// 各ステージの始めに、10 軸（`UnitTags.Axes`）から重複なしの 3 枚の札が出て、1 枚を選ぶ（金はかからない・見送りは無い）。
// 選んだ軸は持ち越し、同じ軸を 2 回選べば重なる。効き方は<b>呼ぶ候補の寄せ</b>だけ——候補の 1 枚ごとに、投資 1 回につき
// <see cref="InvestRule.PerPick"/>%（上限 <see cref="InvestRule.Cap"/>%）の割合で、投資した軸の駒から引く（`Map11State.NextOffer`）。
// 進み具合（どのステージで何を選んだか）は `Map11State.InvestLog` が持ち、`CarryPack.Invest` で持ち越す。
// <b>ここには Godot の型を1つも使わない。</b>
// =====================================================================================

/// <summary>
/// 投資の規則（第467期）。<b><see cref="On"/> が偽なら投資は無い</b>（単発のステージ・第466期までの 1 章）——呼ぶ候補は今の引き方のまま、乱数も引かない。
/// </summary>
public sealed class InvestRule
{
    public required bool On { get; init; }
    public required string Name { get; init; }
    /// <summary>ステージの始めに出る札の枚数（重複なし）。</summary>
    public int Cards { get; init; } = 3;
    /// <summary>投資 1 回で足す「投資の枠から引く」割合（%）。</summary>
    public int PerPick { get; init; } = 25;
    /// <summary>その割合の上限（%）。</summary>
    public int Cap { get; init; } = 60;

    public static readonly InvestRule Off = new() { On = false, Name = "投資なし", Cards = 0, PerPick = 0, Cap = 0 };

    /// <summary>第467期の値: 札 3 枚 ／ 投資 1 回につき +25% ／ 上限 60%。</summary>
    public static readonly InvestRule V467 = new() { On = true, Name = "第467期（札 3 枚・1 回 +25%・上限 60%）", Cards = 3, PerPick = 25, Cap = 60 };

    /// <summary>投資 <paramref name="picks"/> 回のときの、候補 1 枚が投資の枠から来る割合（%）。</summary>
    public int Percent(int picks) => !On || picks <= 0 ? 0 : Math.Min(Cap, PerPick * picks);

    /// <summary>
    /// そのステージの札（10 軸から重複なしで <see cref="Cards"/> 枚・<paramref name="seed"/> だけで決まる）。
    /// 1 章は <c>Chapter1.StageSeed(1 章の seed, ステージ, 0)</c> を渡す——<b>やり直しでも同じ 3 枚</b>（配りの回数を入れない）。
    /// </summary>
    public IReadOnlyList<string> CardsFor(int seed)
    {
        if (!On) return Array.Empty<string>();
        var rng = new Random(unchecked(seed * 7919 + 467));
        string[] axes = UnitTags.Axes.ToArray();
        for (int i = axes.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (axes[i], axes[j]) = (axes[j], axes[i]); }
        return axes.Take(Math.Min(Cards, axes.Length)).ToArray();
    }

    /// <summary>その軸の駒（編成に選べる 52 枚のうち・`UnitCatalog.All` の並び）。</summary>
    public static IReadOnlyList<UnitDef> PiecesOf(string axis) => UnitCatalog.All.Where(d => MapDraw.AxesOf(d).Contains(axis)).ToArray();

    /// <summary>
    /// 器具の選び方（指示書 §4 ／ §7）: <b>手持ちにいちばん多い軸</b>（隊 ・手札 ・倒れた駒のキャラを数える）。同じなら札の左から。
    /// </summary>
    public static int BotPick(Map11State st, IReadOnlyList<string> cards)
    {
        int best = 0, bestN = -1;
        for (int i = 0; i < cards.Count; i++)
        {
            int n = st.OwnedAxisCount(cards[i]);
            if (n > bestN) { best = i; bestN = n; }
        }
        return best;
    }

    /// <summary>投資の 1 行（「投資: 燃焼 ×1・感電 ×1」・投資が無ければ「投資: なし」）。</summary>
    public static string Text(IReadOnlyList<(string Stage, string Axis)> log)
        => log.Count == 0 ? "投資: なし"
            : "投資: " + string.Join("・", log.GroupBy(x => x.Axis).Select(g => $"{g.Key} ×{g.Count()}"));
}
