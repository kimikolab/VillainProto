using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 駒どうしの関係（第171期 §1-2）—— **仕組みは1つも足さない。席から引くだけ。**
//
// 観察ログの「**カドが後衛だったので前衛か中衛に配置したくなった**」（第170期 問い6）に対して、
// **その席である理由を図に描く**ための元データ。
//
// **線の元データを手書きしない**——関係の中身（誰から誰へ）は `FormationRules` の
// 隣接表・列・レーンを**実際の編成に当てて機械で引く**。手書きなのは
// 「その札がどの関係を読むか」の対応（下の `Rules`）1つだけで、
// **その網羅性は `Map11Verify.Phase0` が `BattleCore/Traits.cs` の走査と突き合わせて確かめる**（R260）。
//
// **描くのは席に依存する関係だけ。** 位置を問わない札（呪詛の漏れ・破裂・墓守）と、
// 盤面の状態で決まる札（繕い＝最も傷ついた味方・かき回し＝無作為）は
// **描かずに `Unresolved` で名指しする**（指示書 §1-2 の最後の行）。
// =====================================================================================

public static class Map11Relations
{
    /// <summary>関係の向き付き1本。<c>From</c> / <c>To</c> は席番号。</summary>
    public sealed record Link(int From, int To, string Word, TraitId Trait);

    /// <summary>席から引ける関係の種類。<b>述語はすべて <see cref="FormationRules"/> から引く。</b></summary>
    public enum Shape
    {
        /// <summary>隣接表（<see cref="FormationRules.AreAdjacent"/>）。</summary>
        Adjacent,

        /// <summary>隣接のうち最大HP1体だけ（囃し立て）。<b>同値なら乱数なので全部描く。</b></summary>
        AdjacentTopHp,

        /// <summary>隣接のうち支援を受け取れる駒だけ（突き返し・火選り）。</summary>
        AdjacentAcceptsSupport,

        /// <summary>同じ列の相方 ＋ 同じレーンの1つ前（棘守り）。</summary>
        RowPairOrAhead,

        /// <summary>自分より後ろの列の味方すべて（巨躯）。</summary>
        DeeperRow,

        /// <summary>支援拒否が隣へ流す先（<see cref="FormationRules.AreAdjacent"/> ＋ 受け取れること）。</summary>
        StoicSpill,
    }

    /// <summary>
    /// <b>手書きなのはこの表だけ。</b> 札 → （席から引ける関係の形、線に添える1語、<b>その1語の意味</b>）。
    ///
    /// <para><b>語は「その線が何をしているか」だけを書く。</b> 良し悪しは書かない（案B）。</para>
    ///
    /// <para><b>第172期に `Mean`（意味の1行）を足した</b>（指示書 §1-3）——第171期の観察ログ
    /// 「ヒサの標がどんな効果なのか分からない」（問い1）は、<b>線の語だけでは
    /// 何が起きるか分からない</b>ということだった。<b>網羅性は `--map11-phase172` が門にする</b>
    /// （9 種すべてに空でない1行があること）。</para>
    /// </summary>
    private static readonly Dictionary<TraitId, (Shape Shape, string Word, string Mean)> Rules = new()
    {
        [TraitId.Marker] = (Shape.AdjacentTopHp, "標",
            "敵の単体攻撃がこの駒へ集まりやすくなる"),
        [TraitId.Guardian] = (Shape.Adjacent, "庇う",
            "この駒への単体攻撃に割り込んで代わりに受ける（薙ぎ・貫き・全体は素通りする）"),
        [TraitId.Stoic] = (Shape.StoicSpill, "支援が流れる",
            "1体を選ぶ回復・強化を自分は受け取らず、この駒へ回す"),
        [TraitId.ThornGuard] = (Shape.RowPairOrAhead, "身代わり",
            "この駒が受けた傷を、決まった量まで代わりに引き受ける（超えた分は本人が受ける）"),
        [TraitId.Thorns] = (Shape.Adjacent, "反撃が巻き込む",
            "殴られたときの反射が、この駒にも当たる（味方も削れる）"),
        [TraitId.Sacrifice] = (Shape.Adjacent, "開戦時に削る",
            "開戦時にこの駒の HP を削る（被弾で育つ駒なら、そのまま起動の合図になる）"),
        [TraitId.Shove] = (Shape.AdjacentAcceptsSupport, "腕が鈍る",
            "突き返すたび、この駒の攻撃力が下がる"),
        [TraitId.Favor] = (Shape.AdjacentAcceptsSupport, "火が無いと鈍る",
            "この駒が燃えていなければ攻撃力が下がる（燃えている味方は逆に強くなる）"),
        [TraitId.Colossus] = (Shape.DeeperRow, "肩代わり",
            "自分より後ろの列にいるこの駒の傷を、大半を飲み込んで代わりに受ける"),
    };

    /// <summary>
    /// <b>席を読むのに、席からは引けない札</b>とその理由。**画面には描かず、ここで名指しする**
    /// （指示書 §1-2・自己検査 (f)）。
    ///
    /// <para><see cref="Map11Verify"/> の走査が「`Traits.cs` で位置を読んでいる」と判定した札は、
    /// <see cref="Rules"/> かこの表のどちらかに必ず居なければならない。</para>
    /// </summary>
    public static readonly Dictionary<TraitId, string> Unresolved = new()
    {
        [TraitId.Shuffler] = "毎ターン無作為に入れ替える（相手は席では決まらない）",
        [TraitId.Displaced] = "動かされた本人だけが反応する（相手がいない）",
        [TraitId.Coward] = "逃げ込む先はそのときの空席で決まる",
        [TraitId.Curse] = "味方全体へ漏れる（位置を問わない）",
        [TraitId.Expose] = "敵陣の席を動かす（味方どうしの線ではない）",
        [TraitId.Taillight] = "灯の相手は席ではなく速さと手番で決まる",
        [TraitId.Venom] = "毒の漏れは隣接だが、漏れるのは殴られたときだけ",
        [TraitId.Cinder] = "火の粉は隣接だが、移るのは殴ったときだけ",
        [TraitId.Pyre] = "燃えている間だけ形が変わる（相手がいない）",
        [TraitId.Splash] = "巻き込みは殴られた側の隣接で決まる",
        [TraitId.Loose] = "外れ矢の行き先はそのときの盤面で決まる",
        [TraitId.Goad] = "隣接のうち攻撃力が最大の味方（値が戦闘中に動く）",
        [TraitId.Overbear] = "保持者がロスターにいない（棄却駒）",
        [TraitId.Slander] = "敵側の札（味方どうしの線ではない）",
        [TraitId.Finisher] = "相手は標の有無で決まる（席ではない）",
        [TraitId.Rally] = "味方全体へ配る（位置を問わない）",
        [TraitId.Cower] = "味方全体へ配る（位置を問わない）",
        [TraitId.Betrayed] = "倒した相手の席に湧く（開幕には無い）",
        [TraitId.Sniper] = "自分の列だけを読む（相手がいない）",
    };

    /// <summary>
    /// 席の割り当てに、手書きの <see cref="Rules"/> を当てて線を引く。
    /// <b>述語は1つも自前で持たない</b>——全部 <see cref="FormationRules"/> から引く。
    /// </summary>
    /// <param name="seats">席番号 → 駒。倒れた駒は呼び出し側で外しておくこと。</param>
    public static List<Link> Of(IReadOnlyDictionary<int, UnitDef> seats)
    {
        var links = new List<Link>();
        foreach ((int from, UnitDef def) in seats.OrderBy(kv => kv.Key))
            foreach (TraitId t in def.Traits)
            {
                if (!Rules.TryGetValue(t, out var rule)) continue;
                foreach (int to in Resolve(rule.Shape, from, seats))
                    links.Add(new Link(from, to, rule.Word, t));
            }
        return links;
    }

    private static IEnumerable<int> Resolve(Shape shape, int from, IReadOnlyDictionary<int, UnitDef> seats)
    {
        IEnumerable<int> others = seats.Keys.Where(s => s != from);

        switch (shape)
        {
            case Shape.Adjacent:
                return others.Where(s => FormationRules.AreAdjacent(from, s));

            case Shape.AdjacentAcceptsSupport:
            case Shape.StoicSpill:
                return others.Where(s => FormationRules.AreAdjacent(from, s) && Accepts(seats[s]));

            case Shape.AdjacentTopHp:
            {
                var adj = others.Where(s => FormationRules.AreAdjacent(from, s)).ToList();
                if (adj.Count == 0) return Array.Empty<int>();
                int top = adj.Max(s => seats[s].MaxHp);
                // 同値は engine 側が乱数で割る（`MarkerTrait` の `PickOne`）ので、**全部描く**。
                return adj.Where(s => seats[s].MaxHp == top);
            }

            case Shape.RowPairOrAhead:
                return others.Where(s => FormationRules.AreSameRowPair(from, s)
                                      || FormationRules.IsLanePredecessor(s, from));

            case Shape.DeeperRow:
                return others.Where(s => FormationRules.DepthOf(FormationRules.RowOf(s))
                                       > FormationRules.DepthOf(FormationRules.RowOf(from)));

            default:
                return Array.Empty<int>();
        }
    }

    /// <summary>支援を受け取れるか。<b>判定は <see cref="Trait.BlocksSupport"/> から引く</b>（札の名前を手書きしない）。</summary>
    private static bool Accepts(UnitDef def)
        => !TraitCatalog.Resolve(def.Traits).Any(t => t.BlocksSupport);

    /// <summary>手書きの <see cref="Rules"/> が受け持つ札の一覧（走査の突き合わせ用）。</summary>
    public static IEnumerable<TraitId> Covered => Rules.Keys;

    /// <summary>その札に線があるか。</summary>
    public static bool Has(TraitId id) => Rules.ContainsKey(id);

    /// <summary>その札の1語（線の脇に出す）。</summary>
    public static string? WordOf(TraitId id) => Rules.TryGetValue(id, out var r) ? r.Word : null;

    /// <summary>その1語の意味（第172期 §1-3）。線を押したときに添える。</summary>
    public static string? MeanOf(TraitId id) => Rules.TryGetValue(id, out var r) ? r.Mean : null;

    /// <summary>手書きの表そのもの（走査の突き合わせ用）。</summary>
    public static IEnumerable<(TraitId Id, string Word, string Mean)> Table
        => Rules.Select(kv => (kv.Key, kv.Value.Word, kv.Value.Mean));
}
