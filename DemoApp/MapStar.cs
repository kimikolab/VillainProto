using BattleCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// ☆の重ね（第463期・指示書 design/PHASE463_STAR_SPEC.md §1・1 章だけ）
//
// 同じキャラを重ねると☆が上がる。<b>☆3 が今の強さ</b>（☆3 の駒は今の定義そのもの）。
// ☆1 ／ ☆2 は最大HP と攻撃を掛けた定義の写し（四捨五入・下限 1）と、看板の数字（この期はハギの刈り取りの線の上限だけ）。
// 進み具合（☆と、次の☆へ重ねた枚数）は `Map11State.Stars` が持つ。<b>ここには Godot の型を1つも使わない。</b>
// =====================================================================================

/// <summary>いまの☆と、次の☆へ重ねた枚数（☆3 なら 0）。</summary>
public readonly record struct StarState(int Star, int Pips);

/// <summary>看板の数字 1 つ（第463期 §1-2・この期はハギだけ）。<c>ReapCapPermil[☆]</c> が刈り取りの線の上限（‰・null なら札のとおり）。</summary>
/// 第473期: ネルの漏れの看板（<c>LeakStart</c> ／ <c>LeakTurn</c> ／ <c>LeakFloor</c>・添字が☆・null なら札のとおり・`UnitDef.LeakStartPermil` ほかへ渡す）と、
/// 札の文の足し（<c>MinusAdd</c>・その☆の写しの「代わりに」の末尾に足す 1 行）。<b>☆3 に値があれば☆3 も写しになる</b>（無ければ今の定義そのもの）。
public sealed record StarSign(string What, IReadOnlyList<int?> ReapCapPermil)
{
    public IReadOnlyList<int?>? LeakStart { get; init; }
    public IReadOnlyList<int?>? LeakTurn { get; init; }
    public IReadOnlyList<int?>? LeakFloor { get; init; }
    public IReadOnlyList<string?>? MinusAdd { get; init; }

    public static T? At<T>(IReadOnlyList<T>? list, int star) => list is { } l && star >= 0 && star < l.Count ? l[star] : default;

    /// <summary>☆<paramref name="star"/> で今の定義から何か替えるか（☆3 を写しにするかの判定）。</summary>
    public bool Touches(int star) => At(ReapCapPermil, star) is not null || At(LeakStart, star) is not null || At(LeakTurn, star) is not null
                                     || At(LeakFloor, star) is not null || At(MinusAdd, star) is not null;
}

/// <summary>
/// ☆の規則（第463期）。<b><see cref="On"/> が偽なら☆は無い</b>（単発のステージ・第462期までの 1 章）——駒は今の定義のまま。
/// 表は <see cref="Percent"/>（☆ごとの最大HP・攻撃の百分率）と <see cref="Need"/>（その☆から次の☆へ上げるのに重ねる同じキャラの枚数）。
/// 「1 枚ごとに☆+1」の版は <c>Need = {0, 1, 1}</c> の値だけで作れる。
/// </summary>
public sealed class StarRule
{
    public required bool On { get; init; }
    public required string Name { get; init; }
    /// <summary>☆ごとの最大HP・攻撃の百分率（添字が☆・0 番は使わない）。最後の☆は 100（今の強さ）。</summary>
    public required IReadOnlyList<int> Percent { get; init; }
    /// <summary>その☆から次の☆へ上げるのに重ねる同じキャラの枚数（添字が☆・0 番と最後の☆は使わない）。</summary>
    public required IReadOnlyList<int> Need { get; init; }
    /// <summary>看板の数字（キャラの <c>Id</c> → 札）。</summary>
    public IReadOnlyDictionary<string, StarSign> Signs { get; init; } = new Dictionary<string, StarSign>();
    /// <summary>
    /// 第464期: 「強化」の値段（持っている駒に金で 1 枚重ねる）。<b>0 なら第463期の重ね</b>——呼ぶ候補に持っている駒が出て、呼ぶと重なる。
    /// 正なら呼ぶ候補は持っていないキャラだけ（第462期の作り）で、重ねは「強化」だけ（指示書 design/PHASE464_STAR_DIRECT_SPEC.md §1-1）。
    /// </summary>
    public int EnhancePrice { get; init; }
    /// <summary>重ねを呼ぶ候補で配るか（第463期の作り・<see cref="EnhancePrice"/> が 0）。</summary>
    public bool StackOffers => On && EnhancePrice <= 0;

    /// <summary>☆の上限（☆3）。</summary>
    public int Max => Percent.Count - 1;

    public static readonly StarRule Off = new() { On = false, Name = "☆なし", Percent = new[] { 0, 100 }, Need = new[] { 0, 0 } };

    /// <summary>第463期の値: ☆1 ×0.6 ・☆2 ×0.8 ・☆3 ×1.0 ／ ☆2 に 2 枚・☆3 に 3 枚 ／ ハギの刈り取りの線の上限 ☆1 25% ・☆2 35% ・☆3 は今のまま（50%）。</summary>
    public static readonly StarRule V463 = new()
    {
        On = true,
        Name = "第463期（☆1 ×0.6 ・☆2 ×0.8 ・☆3 ×1.0 ／ 2 枚・3 枚）",
        Percent = new[] { 0, 60, 80, 100 },
        Need = new[] { 0, 2, 3 },
        Signs = new Dictionary<string, StarSign>
        {
            [UnitCatalog.Hagi.Id] = new("刈り取りの線の上限", new int?[] { null, 250, 350, null }),
        },
    };

    /// <summary>
    /// 第464期の値: ☆の表・看板の数字は第463期のまま（☆1 ×0.6 ・☆2 ×0.8 ・☆3 ×1.0 ／ 2 枚・3 枚 ／ ハギ 25% ・35%）。
    /// 重ねは<b>「強化」で 1 枚 4 金</b>（呼ぶと同じ値・☆3 まで 5 枚 ＝ 20 金）。呼ぶ候補に持っている駒は出ない。
    /// </summary>
    public static readonly StarRule V464 = new()
    {
        On = true,
        Name = "第464期（☆1 ×0.6 ・☆2 ×0.8 ・☆3 ×1.0 ／ 2 枚・3 枚 ／ 強化 1 枚 4 金）",
        Percent = V463.Percent,
        Need = V463.Need,
        Signs = V463.Signs,
        EnhancePrice = 4,
    };

    /// <summary>第473期のネルの漏れの看板の 3 版（指示書 design/PHASE473_NEL_LEAK_SPEC.md §2）。☆の表・強化・ハギの看板は第464期のまま。</summary>
    static IReadOnlyDictionary<string, StarSign> WithNel(StarSign nel)
        => new Dictionary<string, StarSign>(V464.Signs) { [UnitCatalog.Nel.Id] = nel };

    static StarRule NelVersion(string tag, StarSign nel) => new()
    {
        On = true, Name = $"第473期 {tag}（☆1 ×0.6 ・☆2 ×0.8 ・☆3 ×1.0 ／ 2 枚・3 枚 ／ 強化 1 枚 4 金 ／ {nel.What}）",
        Percent = V464.Percent, Need = V464.Need, EnhancePrice = V464.EnhancePrice, Signs = WithNel(nel),
    };

    /// <summary>NL-f: ネルの漏れでは、味方の攻撃は素の値の半分より下がらない（☆を問わない・切り上げ）。</summary>
    public static readonly StarRule V473f = NelVersion("NL-f", new StarSign("漏れの床 攻撃の半分", new int?[] { null, null, null, null })
    {
        LeakFloor = new int?[] { null, 500, 500, 500 },
        MinusAdd = new string?[] { null, "味方への漏れは、攻撃力の半分まで", "味方への漏れは、攻撃力の半分まで", "味方への漏れは、攻撃力の半分まで" },
    });

    /// <summary>NL-s: 漏れの固定値（開戦 −5 ・呪うたび −2）に☆の倍率（☆1 ×0.6 ・☆2 ×0.8 ・☆3 は今のまま・切り捨て・最低 1）。</summary>
    public static readonly StarRule V473s = NelVersion("NL-s", new StarSign("漏れに☆の倍率", new int?[] { null, null, null, null })
    {
        LeakStart = new int?[] { null, 600, 800, null },
        LeakTurn = new int?[] { null, 600, 800, null },
        MinusAdd = new string?[] { null, "味方への漏れは ×0.6", "味方への漏れは ×0.8", null },
    });

    /// <summary>NL-1: ☆1 だけ開戦の漏れが無い（呪うたびの −2 は残す）。</summary>
    public static readonly StarRule V4731 = NelVersion("NL-1", new StarSign("☆1 は開戦の漏れなし", new int?[] { null, null, null, null })
    {
        LeakStart = new int?[] { null, 0, null, null },
        MinusAdd = new string?[] { null, "☆1 は開戦の漏れが無い", null, null },
    });

    /// <summary>名前で引く（`--axis-diag --stars-rule=`・第473期）。</summary>
    public static StarRule? ByTag(string tag) => tag switch
    {
        "V464" => V464, "NL-f" or "V473f" => V473f, "NL-s" or "V473s" => V473s, "NL-1" or "V4731" => V4731, _ => null,
    };

    /// <summary>☆<paramref name="star"/> の、次の☆へ要る枚数（☆の上限なら 0）。</summary>
    public int NeedAt(int star) => star >= Max ? 0 : Need[star];

    /// <summary>その☆・重ねた枚数の駒が持つ同じキャラの枚数（最初の 1 枚を含む・売るときの値段）。</summary>
    public int Copies(StarState s) => 1 + Enumerable.Range(1, Math.Max(0, s.Star - 1)).Sum(NeedAt) + s.Pips;

    /// <summary>1 枚重ねた後（☆が上がれば重ねた枚数は 0 から）。☆の上限では動かない。</summary>
    public StarState Add(StarState s)
    {
        if (s.Star >= Max) return s;
        int p = s.Pips + 1;
        return p >= NeedAt(s.Star) ? new StarState(s.Star + 1, 0) : new StarState(s.Star, p);
    }

    private readonly ConcurrentDictionary<(UnitDef, int), UnitDef> _defs = new();
    private readonly ConcurrentDictionary<UnitDef, UnitDef> _base = new(ReferenceEqualityComparer.Instance);

    /// <summary>☆の写しの元の定義（今の定義・☆の写しでなければそのまま）。</summary>
    public UnitDef BaseOf(UnitDef d) => _base.TryGetValue(d, out UnitDef? b) ? b : d;

    /// <summary>
    /// ☆<paramref name="star"/> の定義。<b>☆の上限（☆3）は今の定義そのもの</b>。それ以外は最大HP・攻撃を掛けた写し（四捨五入・下限 1）と看板の数字
    /// （同じ定義・同じ☆には同じインスタンス）。項目は `UnitDef.WithStats` と同じ並び（足したらここにも足す）。
    /// </summary>
    public UnitDef Def(UnitDef d, int star)
    {
        UnitDef b = BaseOf(d);
        if (!On || (star >= Max && !(Signs.TryGetValue(b.Id, out StarSign? top) && top.Touches(Max)))) return b;   // 第473期: ☆3 に看板の数字があれば☆3 も写し
        UnitDef made = _defs.GetOrAdd((b, Math.Min(star, Max)), k =>
        {
            (UnitDef src, int s) = k;
            int p = Percent[s];
            StarSign? sign = Signs.TryGetValue(src.Id, out StarSign? sg) ? sg : null;
            string? add = StarSign.At(sign?.MinusAdd, s);
            return new UnitDef
            {
                Id = src.Id, Name = src.Name, MaxHp = MapFoeScale.Mul(src.MaxHp, p), Attack = MapFoeScale.Mul(src.Attack, p), Speed = src.Speed, Traits = src.Traits,
                Pattern = src.Pattern, Advances = src.Advances, Actions = src.Actions,
                PlusText = src.PlusText, MinusText = add is null ? src.MinusText : src.MinusText.Length > 0 ? $"{src.MinusText} / {add}" : add, Flavor = src.Flavor,
                ReapLineCapPermil = StarSign.At(sign?.ReapCapPermil, s) is int cap ? cap : src.ReapLineCapPermil,
                LeakStartPermil = StarSign.At(sign?.LeakStart, s) ?? src.LeakStartPermil,   // 第473期: ネルの漏れの看板
                LeakTurnPermil = StarSign.At(sign?.LeakTurn, s) ?? src.LeakTurnPermil,
                LeakFloorPermil = StarSign.At(sign?.LeakFloor, s) ?? src.LeakFloorPermil,
            };
        });
        _base.TryAdd(made, b);
        return made;
    }

    /// <summary>札の上の☆（「☆1（1／2）」・☆の上限は「☆3」）。</summary>
    public string Text(StarState s) => s.Star >= Max || s.Pips == 0 ? $"☆{s.Star}" : $"☆{s.Star}（{s.Pips}／{NeedAt(s.Star)}）";

    /// <summary>呼ぶ候補の「重ねる」の文（「重ねる ☆1→（1／2）」・☆が上がる 1 枚は「重ねる ☆1（1／2）→☆2」）。</summary>
    public string StackText(StarState s)
    {
        StarState after = Add(s);
        return after.Star > s.Star ? $"重ねる {Text(s)}→☆{after.Star}" : $"重ねる ☆{s.Star}→（{after.Pips}／{NeedAt(s.Star)}）";
    }

    /// <summary>看板の数字の文（ハギ☆1 なら「刈り取りの線の上限 25%」・無ければ空）。</summary>
    public string SignText(string id, int star)
        => On && Signs.TryGetValue(id, out StarSign? sign) && star < Max && StarSign.At(sign.ReapCapPermil, star) is int cap ? $"{sign.What} {cap / 10.0:0.#}%"
         : On && Signs.TryGetValue(id, out StarSign? s2) && StarSign.At(s2.MinusAdd, Math.Min(star, Max)) is string add ? add : "";   // 第473期: ネルの漏れの文
}
