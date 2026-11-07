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
public sealed record StarSign(string What, IReadOnlyList<int?> ReapCapPermil);

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
        if (!On || star >= Max) return b;
        UnitDef made = _defs.GetOrAdd((b, star), k =>
        {
            (UnitDef src, int s) = k;
            int p = Percent[s];
            return new UnitDef
            {
                Id = src.Id, Name = src.Name, MaxHp = MapFoeScale.Mul(src.MaxHp, p), Attack = MapFoeScale.Mul(src.Attack, p), Speed = src.Speed, Traits = src.Traits,
                Pattern = src.Pattern, Advances = src.Advances, Actions = src.Actions,
                PlusText = src.PlusText, MinusText = src.MinusText, Flavor = src.Flavor,
                ReapLineCapPermil = Signs.TryGetValue(src.Id, out StarSign? sign) && sign.ReapCapPermil[s] is int cap ? cap : src.ReapLineCapPermil,
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
        => On && Signs.TryGetValue(id, out StarSign? sign) && star < Max && sign.ReapCapPermil[star] is int cap ? $"{sign.What} {cap / 10.0:0.#}%" : "";
}
