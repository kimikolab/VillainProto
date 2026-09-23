using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 駒どうしの関係（第171期 §1-2 ／ 第172期 §1-3 ／ **第173期 §1-1**）
// —— **仕組みは1つも足さない。席から引くだけ。**
//
// 観察ログの「**カドが後衛だったので前衛か中衛に配置したくなった**」（第170期 問い6）に対して、
// **その席である理由を図に描く**ための元データ。
//
// **線の元データを手書きしない**——関係の中身（誰から誰へ）は `FormationRules` の
// 隣接表・列・レーンを**実際の編成に当てて機械で引く**。手書きなのは
// 「その札がどの関係を読むか」の対応（下の `Rules`）1つだけで、
// **その網羅性は `Map11Verify.Phase0` が `BattleCore/Traits.cs` の走査と突き合わせて確かめる**（R260）。
//
// -------------------------------------------------------------------------------------
// **第173期に足したのは2つ。どちらも手書きの列で、どちらも門に載っている。**
//
//   (1) **`Sign`（得／損／両方）**——観察ログ「ボルグをリィカ軸へ入れたら、ボルグの自傷で
//       リィカが落ちた」（第172期 問い5）。**図にその関係が1本も無かった**のが問題だが、
//       引けるようにしただけでは足りない——**線が全部同じ色だと、置く前に「これは危ない」と
//       気づけない。** 得は緑、損は赤、両方は金（色の割り当ては `SeatLinks`）。
//
//   (2) **`Conditional`（点線）**——第172期までの `Unresolved`（線を引かない札）には
//       2種類が混ざっていた。**(a) 席からそもそも引けない**（かき回し＝無作為／灯＝速さ）と、
//       **(b) 席からは引けるが、いつ起きるかが条件付き**（巻き込み＝殴ったとき／
//       毒漏れ＝殴られたとき／火の粉＝殴ったとき／駆り立て＝攻撃力が最大の隣）。
//       **(b) は実線と同じ規則で引けるので、点線で引く。** (a) だけが `Unresolved` に残る。
//
// **描くのは席に依存する関係だけ。** 位置を問わない札（呪詛の漏れ・破裂・墓守）と、
// 盤面の状態で決まる札（繕い＝最も傷ついた味方・かき回し＝無作為）は
// **描かずに `Unresolved` で名指しする**（指示書 §1-2 の最後の行）。
// =====================================================================================

public static class Map11Relations
{
    /// <summary>
    /// その線が、<b>線の先にいる味方にとって</b>得か損か（第173期 §1-1）。
    /// <b>盤面は1ビットも読まない手書きの札</b>で、網羅性は `--map11-phase172` が門にする。
    /// </summary>
    public enum LinkSign
    {
        /// <summary>得（庇う・身代わり・支援が流れる・肩代わり）。</summary>
        Gain,

        /// <summary>損（巻き込み・火の粉・毒漏れ・腕が鈍る）。</summary>
        Loss,

        /// <summary>両方（標＝矛先を集める代わりに殴られる／開戦時に削る＝被弾で育つ駒なら起動）。</summary>
        Both,
    }

    /// <summary>得／損／両方 の1語（画面と走査の両方がここから引く）。</summary>
    public static string LabelOf(LinkSign sign) => sign switch
    {
        LinkSign.Gain => "得",
        LinkSign.Loss => "損",
        _ => "両方",
    };

    /// <summary>
    /// 関係の向き付き1本。<c>From</c> / <c>To</c> は席番号。
    /// <c>Conditional</c> が真なら<b>点線</b>（席からは引けるが、いつ起きるかが条件付き）。
    /// </summary>
    public sealed record Link(int From, int To, string Word, TraitId Trait,
                              LinkSign Sign, bool Conditional);

    /// <summary>席から引ける関係の種類。<b>述語はすべて <see cref="FormationRules"/> から引く。</b></summary>
    public enum Shape
    {
        /// <summary>隣接表（<see cref="FormationRules.AreAdjacent"/>）。</summary>
        Adjacent,

        /// <summary>隣接のうち最大HP1体だけ（囃し立て）。<b>同値なら乱数なので全部描く。</b></summary>
        AdjacentTopHp,

        /// <summary>隣接のうち支援を受け取れる駒だけ（突き返し・火選り・駆り立て）。</summary>
        AdjacentAcceptsSupport,

        /// <summary>同じ列の相方 ＋ 同じレーンの1つ前（棘守り）。</summary>
        RowPairOrAhead,

        /// <summary>自分より後ろの列の味方すべて（巨躯）。</summary>
        DeeperRow,

        /// <summary>支援拒否が隣へ流す先（<see cref="FormationRules.AreAdjacent"/> ＋ 受け取れること）。</summary>
        StoicSpill,
    }

    /// <summary>
    /// 手書きの1行。<c>Echo</c> は<b>その札の保持者の説明文に必ず出てくるはずの語</b>で、
    /// 第173期 §1-3 #5「線の1語と `PlusText`/`MinusText` の語が食い違う駒」を
    /// 機械で捕まえるためだけにある（`--map11-phase173` の Q0-3）。
    /// </summary>
    public sealed record Rule(Shape Shape, string Word, string Mean, LinkSign Sign,
                              bool Conditional, string Echo);

    /// <summary>
    /// <b>手書きなのはこの表だけ。</b> 札 → （席から引ける関係の形、線に添える1語、<b>その1語の意味</b>、
    /// <b>得／損／両方</b>、<b>点線か</b>、<b>説明文に出てくるはずの語</b>）。
    ///
    /// <para><b>語は「その線が何をしているか」だけを書く。</b> 良し悪しは書かない（案B）
    /// ——<b>良し悪しは語ではなく色で出す</b>（第173期 §1-1）。</para>
    ///
    /// <para><b>第172期に `Mean`（意味の1行）を足した</b>（指示書 §1-3）——第171期の観察ログ
    /// 「ヒサの標がどんな効果なのか分からない」（問い1）は、<b>線の語だけでは
    /// 何が起きるか分からない</b>ということだった。<b>網羅性は `--map11-phase172` が門にする</b>
    /// （全種に空でない1行・空でない `Echo`・`Sign` の印があること）。</para>
    /// </summary>
    private static readonly Dictionary<TraitId, Rule> Rules = new()
    {
        // ---- 実線（いつ起きるかが席だけで決まる） ----
        [TraitId.Marker] = new(Shape.AdjacentTopHp, "標",
            "敵の単体攻撃がこの駒へ集まりやすくなる（守りが厚ければ得、薄ければそのまま死ぬ）",
            LinkSign.Both, false, "標"),
        [TraitId.Guardian] = new(Shape.Adjacent, "庇う",
            "この駒への単体攻撃に割り込んで代わりに受ける（薙ぎ・貫き・全体は素通りする）",
            LinkSign.Gain, false, "庇う"),
        [TraitId.Stoic] = new(Shape.StoicSpill, "支援が流れる",
            "1体を選ぶ回復・強化を自分は受け取らず、この駒へ回す",
            LinkSign.Gain, false, "流れる"),
        [TraitId.ThornGuard] = new(Shape.RowPairOrAhead, "身代わり",
            "この駒が受けた傷を、決まった量まで代わりに引き受ける（超えた分は本人が受ける）",
            LinkSign.Gain, false, "身代わり"),
        [TraitId.Colossus] = new(Shape.DeeperRow, "肩代わり",
            "自分より後ろの列にいるこの駒の傷を、大半を飲み込んで代わりに受ける",
            LinkSign.Gain, false, "肩代わり"),
        [TraitId.Thorns] = new(Shape.Adjacent, "反撃が巻き込む",
            "殴られたときの反射が、この駒にも当たる（味方も削れる）",
            LinkSign.Loss, false, "巻き込"),
        [TraitId.Shove] = new(Shape.AdjacentAcceptsSupport, "腕が鈍る",
            "突き返すたび、この駒の攻撃力が下がる",
            LinkSign.Loss, false, "腕"),
        [TraitId.Favor] = new(Shape.AdjacentAcceptsSupport, "火が無いと鈍る",
            "この駒が燃えていなければ攻撃力が下がる（燃えている味方は位置を問わず強くなる）",
            LinkSign.Loss, false, "鈍る"),
        [TraitId.Sacrifice] = new(Shape.Adjacent, "開戦時に削る",
            "開戦時にこの駒の HP を削る（被弾で育つ駒なら、そのまま起動の合図になる）",
            LinkSign.Both, false, "開戦時"),

        // ---- 点線（席からは引けるが、いつ起きるかが条件付き。第173期 §1-1） ----
        [TraitId.Splash] = new(Shape.Adjacent, "巻き込む",
            "自分が殴るたび、与えた傷の半分がこの駒にも入る（自分の手番のたび）",
            LinkSign.Loss, true, "巻き込"),
        [TraitId.Cinder] = new(Shape.Adjacent, "火が移る",
            "自分が殴るたび、この駒にも火が点く（燃焼は毎ターン削るが、火を読む駒には燃料になる）",
            LinkSign.Loss, true, "火が移る"),
        [TraitId.Venom] = new(Shape.Adjacent, "毒が漏れる",
            "自分が殴られるたび、この駒にも毒が1層積む（毒は毎ターン層の分だけ削る）",
            LinkSign.Loss, true, "漏れる"),
        [TraitId.Goad] = new(Shape.AdjacentAcceptsSupport, "前に押し出す",
            "毎ターン、隣でいちばん攻撃力が高い1体に力を渡して前へ出す（渡した相手は狙われる）",
            LinkSign.Both, true, "押し出す"),
        // 第179期の残件（第178期 追補で入れた）。**倒れたときだけ起きる**ので点線
        // ——「隣であること」は席で決まり、**いつ降るかだけが席の外にある**（上の (b) 型）。
        [TraitId.Ash] = new(Shape.Adjacent, "灰が降る",
            "灰を抱えたまま倒れると、抱えていた灰が隣の味方に等分で降る（そのぶん HP が削れる）",
            LinkSign.Loss, true, "降る"),
        // 第180期に足し、**第181期に発火口を「暴発したとき」へ移した**。
        // **暴れたときだけ起きる**ので点線——「隣であること」は席で決まり、
        // **いつ散るかだけが席の外にある**（上の (b) 型）。
        [TraitId.Smear] = new(Shape.AdjacentAcceptsSupport, "泥が散る",
            "自分が暴発したとき、この駒に泥が散って攻撃力が下がる（累積して戻らない）",
            LinkSign.Loss, true, "泥"),
        // 第183期。**傷ついたときだけ縫う**ので点線——隣であることは席で決まり、
        // **いつ縫うか（隣が傷ついているか）だけが席の外にある**（上の (b) 型）。
        // 回復と縫い跡（最大HPが減る）が同じ針から出るので「両方」。
        [TraitId.Stitch] = new(Shape.AdjacentAcceptsSupport, "縫い合わせ",
            "この駒が傷ついていると、手番で回復する（縫うたびこの駒の最大HPが減る）",
            LinkSign.Both, true, "縫い合わせ"),
        // 第183期。**うつしたときだけ漏れる**ので点線（(b) 型）。
        [TraitId.TouchLeak] = new(Shape.Adjacent, "毒が付く",
            "自分が敵へ毒をうつすたび、この駒にも毒が1層付く（毒は毎ターン層の分だけ削る）",
            LinkSign.Loss, true, "毒が付く"),
        // 第184期（ヒサの転生）。**毎手番、隣でいちばん元気な1体を選び直す**ので点線（(b) 型）
        // ——隣であることは席で決まり、**誰が選ばれるか（現在HPが最大か）だけが席の外にある**。
        // 集まる攻撃（損）と半減（得）が同じ標から出るので「両方」。
        [TraitId.Beckon] = new(Shape.Adjacent, "矢面",
            "この駒が隣でいちばん元気なら標を付けて敵の攻撃を集め、代わりに受ける痛みを半分にする",
            LinkSign.Both, true, "標"),
        // 第184期。**標の相手以外の隣と入れ替わる**ので点線（誰が相手かは毎手番の標で決まる・(b) 型）。
        [TraitId.Flee] = new(Shape.Adjacent, "入れ替わる",
            "指差したあと、この駒と場所を入れ替えて逃げる（この駒は前へ押し出されることがある）",
            LinkSign.Loss, true, "入れ替わ"),
        // 第185期（バンの転生）。**範囲攻撃が同時に当たったときだけ受ける**ので点線（(b) 型）
        // ——隣であることは席で決まり、**いつ受けるか（薙ぎ・貫き・全体が両方に当たるか）だけが席の外にある**。
        // 判定は engine（`PerformAttack` / `ResolvePierce`）にあるので、札のソースの走査には出てこない（R264）。
        [TraitId.Footing] = new(Shape.Adjacent, "範囲を受ける",
            "薙ぎ・貫き・全体がこの駒と同時に当たるとき、この駒の分を代わりに受け止める（踏みしめた層の軽減が乗る）",
            LinkSign.Gain, true, "範囲攻撃"),
    };

    /// <summary>
    /// <b>席を読むのに、席からは引けない札</b>とその理由。**画面には描かず、ここで名指しする**
    /// （指示書 §1-2・自己検査 (f)）。
    ///
    /// <para><see cref="Map11Verify"/> の走査が「`Traits.cs` で位置を読んでいる」と判定した札は、
    /// <see cref="Rules"/> かこの表のどちらかに必ず居なければならない。</para>
    ///
    /// <para><b>第173期に4本がここから <see cref="Rules"/> の点線側へ移った</b>
    /// （巻き込み・火の粉・毒漏れ・駆り立て）——どれも
    /// 「隣接であることは席で決まり、<b>起きる条件だけが席の外にある</b>」型だった。</para>
    ///
    /// <para><b>第179期の `Ash`（灰が降る）は最初からその型なので、点線側へ直接入れた</b>
    /// （第178期 追補。**この門が捕まえた**——`AshTrait` は
    /// <c>FormationRules.AreAdjacent</c> を読むのに、どちらの表にも居なかった・R260）。</para>
    /// </summary>
    public static readonly Dictionary<TraitId, string> Unresolved = new()
    {
        [TraitId.Shuffler] = "毎ターン無作為に入れ替える（相手は席では決まらない）",
        [TraitId.Displaced] = "動かされた本人だけが反応する（相手がいない）",
        [TraitId.Coward] = "逃げ込む先はそのときの空席で決まる",
        [TraitId.Curse] = "味方全体へ漏れる（位置を問わない）",
        [TraitId.Expose] = "敵陣の席を動かす（味方どうしの線ではない）",
        [TraitId.Taillight] = "灯の相手は席ではなく速さと手番で決まる",
        [TraitId.Pyre] = "燃えている間だけ形が変わる（相手がいない）",
        [TraitId.Loose] = "外れ矢の行き先はそのときの盤面で決まる",
        [TraitId.Overbear] = "保持者がロスターにいない（棄却駒）",
        [TraitId.Slander] = "敵側の札（味方どうしの線ではない）",
        [TraitId.Finisher] = "相手は標の有無で決まる（席ではない）",
        [TraitId.Rally] = "味方全体へ配る（位置を問わない）",
        [TraitId.Cower] = "味方全体へ配る（位置を問わない）",
        [TraitId.Betrayed] = "倒した相手の席に湧く（開幕には無い）",
        [TraitId.Sniper] = "自分の列だけを読む（相手がいない）",
        [TraitId.Touch] = "敵陣の隣接を読む（味方どうしの線ではない）",
        [TraitId.Shame] = "敵陣の隣接を読む（味方どうしの線ではない）",
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
                if (!Rules.TryGetValue(t, out Rule? rule)) continue;
                foreach (int to in Resolve(rule.Shape, from, seats))
                    links.Add(new Link(from, to, rule.Word, t, rule.Sign, rule.Conditional));
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

    /// <summary>
    /// <b>席に紐づかない「隊全体にかかる札」</b>（第174期 部A #4）。
    /// <b>元データは手書きしない</b>——<see cref="Unresolved"/> のうち
    /// 理由に「位置を問わない」と書いてある札をそのまま引く。
    /// <b>一覧は1つきり</b>で、文もそこにある（同じ言葉の表を2つ作らない・第124期 §4）。
    /// </summary>
    public const string GlobalMark = "位置を問わない";

    public static IEnumerable<TraitId> WholeSquad
        => Unresolved.Where(kv => kv.Value.Contains(GlobalMark, StringComparison.Ordinal))
                     .Select(kv => kv.Key);

    /// <summary>
    /// その顔ぶれが持つ「隊全体にかかる札」を1行ずつ（第174期 部A #4）。
    /// <b>文は <see cref="Unresolved"/> のものをそのまま使う。</b>
    /// </summary>
    public static IEnumerable<(string Unit, string Text)> GlobalNotes(IEnumerable<UnitDef> units)
    {
        var seen = new HashSet<(string, TraitId)>();
        foreach (UnitDef d in units)
            foreach (TraitId t in d.Traits)
            {
                if (!Unresolved.TryGetValue(t, out string? why)) continue;
                if (!why.Contains(GlobalMark, StringComparison.Ordinal)) continue;
                if (!seen.Add((d.Id, t))) continue;
                yield return (d.Name, why);
            }
    }

    /// <summary>手書きの <see cref="Rules"/> が受け持つ札の一覧（走査の突き合わせ用）。</summary>
    public static IEnumerable<TraitId> Covered => Rules.Keys;

    /// <summary>その札に線があるか。</summary>
    public static bool Has(TraitId id) => Rules.ContainsKey(id);

    /// <summary>その札の1語（線の脇に出す）。</summary>
    public static string? WordOf(TraitId id) => Rules.TryGetValue(id, out Rule? r) ? r.Word : null;

    /// <summary>その1語の意味（第172期 §1-3）。線を押したときに添える。</summary>
    public static string? MeanOf(TraitId id) => Rules.TryGetValue(id, out Rule? r) ? r.Mean : null;

    /// <summary>その札の 得／損／両方（第173期 §1-1）。</summary>
    public static LinkSign? SignOf(TraitId id) => Rules.TryGetValue(id, out Rule? r) ? r.Sign : null;

    /// <summary>その札が点線か（席からは引けるが、いつ起きるかが条件付き）。</summary>
    public static bool IsConditional(TraitId id) => Rules.TryGetValue(id, out Rule? r) && r.Conditional;

    /// <summary>手書きの表そのもの（走査の突き合わせ用）。</summary>
    public static IEnumerable<(TraitId Id, Rule Rule)> Table
        => Rules.Select(kv => (kv.Key, kv.Value));
}
