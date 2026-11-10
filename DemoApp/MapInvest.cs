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
//
// 第468期（指示書 design/PHASE468_DOCTRINE_SPEC.md §1）: 札に<b>盤の札・金の札</b>（`Doctrine`・プラスとマイナスを両方持つ）を足した。
// 3 枚は「軸 1 ＋ 盤か金 1 ＋ どれでも 1」。盤・金の札は 1 章で同じ札が 2 度出ない（持っている札は札から外す）。
// 盤の札は開戦時の席で駒の数値の写しを差し（`Map11State.ArmDoctrine`）、金の札は値段を `Map11State` の 1 箇所で変える。
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

    /// <summary>
    /// 盤の札・金の札も出すか（第468期）。<b>偽なら第467期</b>（10 軸から重複なしの 3 枚・<see cref="CardsFor"/> の式も乱数も第467期のまま）。
    /// </summary>
    public bool Doctrines { get; init; }

    public static readonly InvestRule Off = new() { On = false, Name = "投資なし", Cards = 0, PerPick = 0, Cap = 0 };

    /// <summary>第467期の値: 札 3 枚 ／ 投資 1 回につき +25% ／ 上限 60%。</summary>
    public static readonly InvestRule V467 = new() { On = true, Name = "第467期（札 3 枚・1 回 +25%・上限 60%）", Cards = 3, PerPick = 25, Cap = 60 };

    /// <summary>第468期の値: 第467期に盤の札 3 枚・金の札 3 枚を足した（3 枚は「軸 1 ＋ 盤か金 1 ＋ どれでも 1」）。軸の寄せは第467期のまま。</summary>
    public static readonly InvestRule V468 = new()
    {
        On = true, Name = "第468期（札 3 枚＝軸 1・盤か金 1・どれでも 1・1 回 +25%・上限 60%）", Cards = 3, PerPick = 25, Cap = 60, Doctrines = true,
    };

    /// <summary>札が軸か（軸でなければ盤・金の札）。</summary>
    public static bool IsAxis(string card) => UnitTags.Axes.Contains(card);

    /// <summary>投資 <paramref name="picks"/> 回のときの、候補 1 枚が投資の枠から来る割合（%）。</summary>
    public int Percent(int picks) => !On || picks <= 0 ? 0 : Math.Min(Cap, PerPick * picks);

    /// <summary>
    /// そのステージの札（10 軸から重複なしで <see cref="Cards"/> 枚・<paramref name="seed"/> だけで決まる）。
    /// 1 章は <c>Chapter1.StageSeed(1 章の seed, ステージ, 0)</c> を渡す——<b>やり直しでも同じ 3 枚</b>（配りの回数を入れない）。
    /// </summary>
    /// 第468期: <paramref name="held"/> は前のステージまでに持った盤・金の札（その札は出さない）。<b>第467期の規則（<see cref="Doctrines"/> が偽）では読まない。</b>
    /// 第469期: <paramref name="open"/> は出してよい軸（そのステージで呼べる駒がまだいる軸・加入の表のある 1 章だけが渡す）。<b>null なら 10 軸すべて</b>（第468期まで）。
    /// 並べる乱数は 10 軸を混ぜてから外す——10 軸すべてを渡せば第468期と同じ札。
    public IReadOnlyList<string> CardsFor(int seed, IReadOnlyCollection<string>? held = null, IReadOnlyCollection<string>? open = null)
    {
        if (!On) return Array.Empty<string>();
        if (Doctrines) return DoctrineCards(seed, held ?? Array.Empty<string>(), open);
        var rng = new Random(unchecked(seed * 7919 + 467));
        string[] axes = UnitTags.Axes.ToArray();
        for (int i = axes.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (axes[i], axes[j]) = (axes[j], axes[i]); }
        return axes.Where(a => open is null || open.Contains(a)).Take(Cards).ToArray();
    }

    /// <summary>
    /// 第468期 §1-2 の 3 枚: <b>軸 1 枚 ＋ 盤か金 1 枚 ＋ どれでも 1 枚</b>（どれでも ＝ 残りの軸 ＋ 持っていない盤・金の札から 1 枚を等しく）。並びは seed で混ぜる。
    /// 持っている盤・金の札（<paramref name="held"/>）は出さない。盤・金の札が 1 枚も残っていなければ 2 枚目も軸。
    /// 第469期 §1-3: 軸は <paramref name="open"/> の軸だけ。<b>出せる軸が 1 つも無ければ、軸の枠も盤・金の札で埋める</b>（盤・金の札も尽きれば枚数が減る）。
    /// </summary>
    private IReadOnlyList<string> DoctrineCards(int seed, IReadOnlyCollection<string> held, IReadOnlyCollection<string>? open)
    {
        var rng = new Random(unchecked(seed * 7919 + 468));
        static void Shuffle(string[] xs, Random r) { for (int i = xs.Length - 1; i > 0; i--) { int j = r.Next(i + 1); (xs[i], xs[j]) = (xs[j], xs[i]); } }
        string[] all = UnitTags.Axes.ToArray();
        Shuffle(all, rng);
        var axes = all.Where(a => open is null || open.Contains(a)).ToList();
        string[] docArr = Doctrine.All.Select(d => d.Name).Where(n => !held.Contains(n)).ToArray();
        Shuffle(docArr, rng);
        var docs = docArr.ToList();
        static string Take(List<string> xs) { string x = xs[0]; xs.RemoveAt(0); return x; }
        var picked = new List<string>();
        bool noAxis = axes.Count == 0;
        if (!noAxis) picked.Add(Take(axes));                               // 軸の枠
        if (docs.Count > 0) picked.Add(Take(docs));                         // 盤か金の枠
        else if (axes.Count > 0) picked.Add(Take(axes));                    //   盤・金の札が尽きていれば軸
        if (noAxis && docs.Count > 0) picked.Add(Take(docs));               // 第469期: 出せる軸が無ければ軸の枠も盤・金の札
        var rest = new List<string>(axes);
        rest.AddRange(docs);
        while (picked.Count < Cards && rest.Count > 0) { int k = rng.Next(rest.Count); picked.Add(rest[k]); rest.RemoveAt(k); }
        string[] shown = picked.ToArray();
        Shuffle(shown, rng);
        return shown;
    }

    /// <summary>その軸の駒（編成に選べる 52 枚のうち・`UnitCatalog.All` の並び）。</summary>
    public static IReadOnlyList<UnitDef> PiecesOf(string axis) => UnitCatalog.All.Where(d => MapDraw.AxesOf(d).Contains(axis)).ToArray();

    /// <summary>
    /// 器具の選び方（指示書 §4 ／ §7）: <b>手持ちにいちばん多い軸</b>（隊 ・手札 ・倒れた駒のキャラを数える）。同じなら札の左から。
    /// 第468期 §3: 盤・金の札は数えない——<b>手持ちの軸が 1 体もいなければ札の左から</b>（盤・金の札でも）。第467期の札（軸だけ）では今のまま。
    /// </summary>
    public static int BotPick(Map11State st, IReadOnlyList<string> cards)
    {
        int best = 0, bestN = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            if (!IsAxis(cards[i])) continue;
            int n = st.OwnedAxisCount(cards[i]);
            if (n > bestN) { best = i; bestN = n; }
        }
        return best;
    }

    /// <summary>投資の 1 行（「投資: 燃焼 ×1・前のめり・略奪」・軸は回数・盤 ／ 金の札は名前だけ・投資が無ければ「投資: なし」）。</summary>
    public static string Text(IReadOnlyList<(string Stage, string Axis)> log)
        => log.Count == 0 ? "投資: なし"
            : "投資: " + string.Join("・", log.GroupBy(x => x.Axis).Select(g => Doctrine.Find(g.Key) is null ? $"{g.Key} ×{g.Count()}" : g.Key));
}

/// <summary>盤の札か金の札か（第468期）。</summary>
public enum DoctrineKind { Board, Gold }

/// <summary>
/// 盤の札・金の札（第468期 §1-1・指示書 design/PHASE468_DOCTRINE_SPEC.md）。<b>プラスとマイナスを両方持つ</b>（今の隊に合うかで迷う札）。
/// 盤の札は開戦時の席の列（前・中央・後）ごとの最大HP・攻撃の増減（%・同じ列に 2 枚が効けば足し合わせる）。
/// 金の札は値段の増減（抜いた金は ×(100 ＋ 割合)% の後に固定の増減・下限 0 ／ 呼ぶ・強化は下限 1 ／ 利子は作戦ターンの終わりに手持ち 10 につき）。
/// 数値の調整は遊んでから（この表を替える）。
/// </summary>
public sealed record Doctrine(string Name, DoctrineKind Kind, string Plus, string Minus)
{
    public int FrontHp { get; init; }
    public int FrontAtk { get; init; }
    public int MidHp { get; init; }
    public int MidAtk { get; init; }
    public int BackHp { get; init; }
    public int BackAtk { get; init; }
    /// <summary>敵の部隊を抜いた金・中間拠点を取った金の増減（%）。</summary>
    public int LootPercent { get; init; }
    /// <summary>その金の固定の増減（割合の後・下限 0）。</summary>
    public int LootFlat { get; init; }
    public int RecruitDelta { get; init; }
    public int EnhanceDelta { get; init; }
    /// <summary>作戦ターンの終わりに、手持ちの金 10 につき足す金。</summary>
    public int InterestPer10 { get; init; }
    /// <summary>売る値段を 0 にする。</summary>
    public bool NoSell { get; init; }

    /// <summary>その列の最大HP ／ 攻撃の増減（%）。</summary>
    public (int Hp, int Atk) Of(Row row) => row switch { Row.Front => (FrontHp, FrontAtk), Row.Mid => (MidHp, MidAtk), _ => (BackHp, BackAtk) };

    public static readonly Doctrine Forward = new("前のめり", DoctrineKind.Board, "前列の最大HP +25%・攻撃 +15%", "後列の攻撃 −15%")
    { FrontHp = 25, FrontAtk = 15, BackAtk = -15 };
    public static readonly Doctrine Rearguard = new("後詰め", DoctrineKind.Board, "後列の攻撃 +25%", "前列の最大HP −15%")
    { BackAtk = 25, FrontHp = -15 };
    public static readonly Doctrine Keystone = new("要", DoctrineKind.Board, "中央の席の最大HP・攻撃 +30%", "中央以外の最大HP −10%")
    { MidHp = 30, MidAtk = 30, FrontHp = -10, BackHp = -10 };
    public static readonly Doctrine Plunder = new("略奪", DoctrineKind.Gold, "敵の部隊を抜いた金 +50%（湧き・中間拠点・守り・勇者も）", "呼ぶ値段 +1")
    { LootPercent = 50, RecruitDelta = 1 };
    public static readonly Doctrine Hoard = new("蓄え", DoctrineKind.Gold, "作戦ターンの終わりに、手持ちの金 10 につき +1（利子）", "売る値段 0")
    { InterestPer10 = 1, NoSell = true };
    public static readonly Doctrine Thrift = new("倹約", DoctrineKind.Gold, "呼ぶ・強化の値段 −1", "敵の部隊を抜いた金 −2（下限 0）")
    { RecruitDelta = -1, EnhanceDelta = -1, LootFlat = -2 };

    /// <summary>盤の札 3 枚 ・金の札 3 枚。</summary>
    public static readonly IReadOnlyList<Doctrine> All = new[] { Forward, Rearguard, Keystone, Plunder, Hoard, Thrift };

    /// <summary>名前から札を引く（盤・金の札でなければ null）。</summary>
    public static Doctrine? Find(string name) => All.FirstOrDefault(d => d.Name == name);

    /// <summary>札の種類の名前（「盤の札」／「金の札」）。</summary>
    public string KindName => Kind == DoctrineKind.Board ? "盤の札" : "金の札";
}

/// <summary>
/// 盤の札の数値の写し（第468期 §1-1）。<b>☆の写しの後に掛ける</b>（四捨五入・下限 1・素の値が 0 の項目は 0 のまま）。
/// 同じ定義・同じ増減には同じインスタンス。写しの元は <see cref="BaseOf"/> で引ける（戦闘の後に元へ戻すため）。
/// </summary>
public static class DoctrineDefs
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(UnitDef, int, int), UnitDef> _defs = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<UnitDef, UnitDef> _base = new(ReferenceEqualityComparer.Instance);

    /// <summary>盤の札の写しの元の定義（写しでなければそのまま）。</summary>
    public static UnitDef BaseOf(UnitDef d) => _base.TryGetValue(d, out UnitDef? b) ? b : d;
    /// <summary>盤の札の写しか。</summary>
    public static bool IsCopy(UnitDef d) => _base.ContainsKey(d);

    /// <summary>最大HP ／ 攻撃を <paramref name="hp"/>% ／ <paramref name="atk"/>% 増減した写し（両方 0 なら元そのもの）。</summary>
    public static UnitDef Of(UnitDef d, int hp, int atk)
    {
        UnitDef b = BaseOf(d);
        if (hp == 0 && atk == 0) return b;
        UnitDef made = _defs.GetOrAdd((b, hp, atk), static k =>
        {
            (UnitDef src, int h, int a) = k;
            return src.WithStats(Mul(src.MaxHp, h), Mul(src.Attack, a));
        });
        _base.TryAdd(made, b);
        return made;
    }

    private static int Mul(int x, int pct) => x == 0 || pct == 0 ? x : MapFoeScale.Mul(x, 100 + pct);
}
