using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 体験版の加入の表（第469期・指示書 design/PHASE469_DEMO_ROSTER_SPEC.md §1・1 章だけ）
//
// 1 章の 1-1 は**決まった 6 枚**で始まり（第464期の配りの代わり・やり直しても同じ 6 枚）、
// 呼ぶ候補に出る駒は**そのステージまでの表の駒だけ**（積み上がる・1-1 は 6 体 → 1-2 は 20 体 → 1-3 は 27 体）。
// 表に無い駒（52 枚のうち始めの 6 枚と表の 27 体のほかの 19 体）は 1 章では呼ぶ候補に出ない。
// 呼ぶ候補の引き方（1 枚目の「手持ちと噛む駒」・投資の枠・持っていないキャラだけ）は今のまま——母数だけがこの表になる（`Map11State.OfferPool`）。
// 第469期 §1-3: 投資の軸の札は、そのステージで呼べる駒（まだ持っていないもの）が 1 体以上いる軸だけ（`Chapter1.InvestCards`）。
// **表は駒の名前で書く**（値だけで駒の出入りを替えられるように・名前が `UnitCatalog.All` に無ければ最初に引いたときに落ちる）。
// **ここには Godot の型を1つも使わない。**
// =====================================================================================

/// <summary>
/// 加入の表（第469期）。<b><see cref="On"/> が偽なら表は無い</b>（単発のステージ・第468期までの 1 章）——始めの手札は配りの規則のまま、呼ぶ候補は 52 枚から。
/// </summary>
public sealed class RosterRule
{
    public required bool On { get; init; }
    public required string Name { get; init; }
    /// <summary>始めの手札（駒の名前）が決まっているステージ（1 章の 1-1）。</summary>
    public string StartStage { get; init; } = "";
    /// <summary>始めの手札（駒の名前・☆は盤の☆の規則で☆1）。</summary>
    public IReadOnlyList<string> Start { get; init; } = Array.Empty<string>();
    /// <summary>
    /// 始めの手札の「既定の組み方」（第476期・駒の名前 → 隊の番号（0 から）と席）。空なら決まっていない（`Map11State` が空いた席へ順に置く）。
    /// 値は `design/weights/p471_start6` の第1隊（測りの主力）と同じ: 主力 5 枚 ＋ 残りの 1 枚は第2隊。
    /// </summary>
    public IReadOnlyList<(string Unit, int Squad, int Slot)> StartSeats { get; init; } = Array.Empty<(string, int, int)>();
    /// <summary>ステージごとに、そのステージから呼べる駒（駒の名前・表の並びがステージの順・積み上がる）。</summary>
    public IReadOnlyList<(string Stage, IReadOnlyList<string> Units)> Joins { get; init; } = Array.Empty<(string, IReadOnlyList<string>)>();

    public static readonly RosterRule Off = new() { On = false, Name = "加入の表なし（52 枚から）" };

    /// <summary>
    /// 第469期の表（ポンの決定 2026-11-08〜10）: 始めは魔王陣営の 6 枚（魔族・魔物寄り）・1-1 から魔族 ／ 魔物・人間の追放者は 1-2 から流れ着く。
    /// 指示書の後の変更（ポン 2026-11-10）: 始めの 6 枚のゾト → ドルガ（ゾトは 1-1 で呼べる駒へ）——1-1 の軸の札は生贄・標・手番・燃焼（ゾトの燃焼）から。
    /// </summary>
    public static readonly RosterRule V469 = new()
    {
        On = true,
        Name = "第469期（始めの 6 枚・1-1 で 6 体 → 1-2 で 20 体 → 1-3 で 27 体）",
        StartStage = "1-1",
        // ポン 2026-11-10: 始めの 6 枚はドルガ入り（ゾトは 1-1 で呼べる駒へ・ドルガと入れ替え）。
        Start = new[] { "墓守リィカ", "胞子体ムグ", "のろまの巨兵ドルガ", "呪詛官ネル", "施しのリリ", "泥人形ムド" },
        Joins = new (string, IReadOnlyList<string>)[]
        {
            ("1-1", new[] { "彼岸花のハギ", "継ぎ接ぎのヴェル", "分かちのドハ", "見境なしのミサ", "鬨の号令ガン", "爆ぜるゾト" }),
            ("1-2", new[] { "電気鞭のシガ", "縛めのクグ", "痺れ粉のトウ", "背かれのソム",
                            "焼け残りのボルグ", "熾のホタ", "火選りのヒヨ",
                            "突き返しのハネ", "軋みのヨミ", "喧噪のバサ",
                            "囃し立てのヒサ", "廃棄聖騎士ガルド", "仇討ちのザン",
                            "刻みのノミ" }),
            ("1-3", new[] { "毒吐きのスィド", "瘴気袋のグザ", "澱み喰いのヴィオ",
                            "砕け盾のヒビ", "継ぎ当てのツギ", "礫のガレ", "錯乱のササ" }),
        },
    };

    /// <summary>
    /// 第471期の表（指示書 design/PHASE471_STAGE11_SPEC.md §1-1・ポンの決定 2026-11-12）: 始めの 6 枚の<b>ネル → ガン</b>（ネルは 1-1 で呼べる駒へ・ガンと入れ替え）。
    /// 2 枚ずつ 3 組が見える——生贄（リィカ・ムグ）・手番（ドルガ・ガン）・呪い（リリ・ムド）。1-2 ／ 1-3 の表は第469期のまま。
    /// </summary>
    public static readonly RosterRule V471 = new()
    {
        On = true,
        Name = "第471期（始めの 6 枚はガン入り・1-1 で 6 体 → 1-2 で 20 体 → 1-3 で 27 体）",
        StartStage = "1-1",
        Start = new[] { "墓守リィカ", "胞子体ムグ", "のろまの巨兵ドルガ", "鬨の号令ガン", "施しのリリ", "泥人形ムド" },
        StartSeats = new[] { ("泥人形ムド", 0, 0), ("のろまの巨兵ドルガ", 0, 1), ("鬨の号令ガン", 0, 2), ("墓守リィカ", 0, 3), ("施しのリリ", 0, 4), ("胞子体ムグ", 1, 1) },
        Joins = new (string, IReadOnlyList<string>)[]
        {
            ("1-1", new[] { "彼岸花のハギ", "継ぎ接ぎのヴェル", "分かちのドハ", "見境なしのミサ", "爆ぜるゾト", "呪詛官ネル" }),
            V469.Joins[1],
            V469.Joins[2],
        },
    };

    /// <summary>
    /// 第475期の表（指示書 design/PHASE475_ROSTER35_SPEC.md §1・ポンの決定 2026-11-14）: 体験版は 35 体。
    /// <b>逆しまのウツ・渡しのワタを 1-2 から</b>（呪いの読み手・第473期の報告）・<b>彼岸花のハギを 1-3 へ遅らせる</b>（1-1 で呼べるのは 5 体・埋めない）。
    /// 始めの 6 枚は第471期のまま。表の並びは第471期の並びの後ろに足した。
    /// </summary>
    public static readonly RosterRule V475 = new()
    {
        On = true,
        Name = "第475期（始めの 6 枚はガン入り・1-1 で 5 体 → 1-2 で 21 体 → 1-3 で 29 体・ウツ ／ ワタは 1-2 から・ハギは 1-3 から）",
        StartStage = "1-1",
        Start = V471.Start,
        StartSeats = V471.StartSeats,
        Joins = new (string, IReadOnlyList<string>)[]
        {
            ("1-1", new[] { "継ぎ接ぎのヴェル", "分かちのドハ", "見境なしのミサ", "爆ぜるゾト", "呪詛官ネル" }),
            ("1-2", V469.Joins[1].Units.Concat(new[] { "逆しまのウツ", "渡しのワタ" }).ToArray()),
            ("1-3", V469.Joins[2].Units.Concat(new[] { "彼岸花のハギ" }).ToArray()),
        },
    };

    /// <summary>版のキーから表を引く（道具の `--roster=`・無ければ null）。</summary>
    public static RosterRule? ByKey(string key) => key switch { "V469" => V469, "V471" => V471, "V475" => V475, _ => null };

    /// <summary>名前から駒を引く（`UnitCatalog.All`・無ければ落ちる——表の書き損じを最初の 1 回で見つける）。</summary>
    public static UnitDef Find(string name)
        => UnitCatalog.All.FirstOrDefault(d => d.Name == name) ?? throw new InvalidOperationException($"加入の表の駒が編成に選べる 52 枚に無い: {name}");

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(RosterRule, string), IReadOnlyList<UnitDef>> _pools = new();

    /// <summary>始めの手札（そのステージで決まっていなければ null）。</summary>
    public IReadOnlyList<UnitDef>? StartFor(string stage) => On && stage == StartStage && Start.Count > 0 ? Start.Select(Find).ToArray() : null;

    /// <summary>そのステージから呼べるようになる駒（表の並び・表に無いステージは空）。</summary>
    public IReadOnlyList<UnitDef> JoinsAt(string stage)
        => On ? Joins.Where(j => j.Stage == stage).SelectMany(j => j.Units).Select(Find).ToArray() : Array.Empty<UnitDef>();

    /// <summary>
    /// そのステージで呼べる駒（表の始めからそのステージまでを積み上げる・<b>`UnitCatalog.All` の並び</b>——呼ぶ候補の列を同じ seed で並べるため）。
    /// 表に無いステージは表の全部。表が無ければ 52 枚。
    /// </summary>
    public IReadOnlyList<UnitDef> PoolFor(string stage)
    {
        if (!On) return UnitCatalog.All;
        return _pools.GetOrAdd((this, stage), static k =>
        {
            var (rule, id) = k;
            int upto = rule.Joins.ToList().FindIndex(j => j.Stage == id);
            var names = rule.Joins.Take(upto < 0 ? rule.Joins.Count : upto + 1).SelectMany(j => j.Units).Select(Find).Select(d => d.Id).ToHashSet();
            return UnitCatalog.All.Where(d => names.Contains(d.Id)).ToArray();
        });
    }

    /// <summary>そのステージで呼べる駒か（表が無ければいつも真）。</summary>
    public bool Allows(string stage, UnitDef d) => !On || PoolFor(stage).Any(x => x.Id == d.Id);
}
