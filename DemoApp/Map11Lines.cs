using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// セリフの表（第479期 §3・指示書 design/PHASE479_LESS_WINDOWS_SPEC.md）。**文はすべて仮**——後でポンが直す。直すのはこのファイルだけ。
//
// 画面の決まり（§1）: 説明は窓の文ではなく、顔つきの 1 行で。出すのは最初の 1 回だけ・盤の端に 4 秒・押せば消える・時計は止めない。
// ここは「いつ・誰が・何と言うか」の表と、「話す駒が手札にいなければ、いる駒の先頭が話す」だけを持つ。
// いつ出すか（盤のどの出来事か）は画面（`Map11Main.Lean.cs`）が盤に聞いて決める。**Godot の型は 1 つも使わない。**
// =====================================================================================
public static class Map11Lines
{
    /// <summary>セリフ 1 行。<c>Speaker</c> は駒の名前の一部（「リィカ」）。</summary>
    public sealed record Line(string Key, string Stage, string When, string Speaker, string Text);

    public const string Start11 = "1-1.start", Gate11 = "1-1.gate", Start12 = "1-2.start", Pass12 = "1-2.pass", NoPass12 = "1-2.nopass", Keeper12 = "1-2.keeper", Gold = "gold20";

    /// <summary>セリフの表（仮の文）。</summary>
    public static readonly IReadOnlyList<Line> All = new[]
    {
        new Line(Start11, "1-1", "1-1 の始め", "リィカ", "北は近いけど、関が固い。南は遠回りで楽"),
        new Line(Gate11, "1-1", "1-1 で峠の関に触れたとき", "ドルガ", "硬そうだ。……賞金は多いな"),
        new Line(Start12, "1-2", "1-2 の始め", "ガン", "中間拠点を 2 つ押さえりゃ、砦の門が開く"),
        new Line(Pass12, "1-2", "1-2 で関所を取ったとき", "ムド", "ここで止める。拠点までは、来させない"),
        new Line(NoPass12, "1-2", "1-2 で関所を取らずに門が開いたとき", "リリ", "留守、狙われるわよ"),
        new Line(Keeper12, "1-2", "1-2 で守将の前（連戦の札が出たとき）", "リィカ", "削られたら、引いて休める"),
        new Line(Gold, "", "金が 20 貯まったとき（最初の 1 回）", "ガン", "金が余ってる。強化しとけ"),
    };

    /// <summary>ステージの頭の札の目標の 1 行（20 字まで）。</summary>
    public static readonly IReadOnlyDictionary<string, string> Goals = new Dictionary<string, string>
    {
        ["1-1"] = "目標　敵の拠点を制圧する",
        ["1-2"] = "目標　中間拠点を 2 つ取り、砦を制圧する",
        ["1-3"] = "目標　砦を制圧する",
    };

    /// <summary>1-3 の頭の札の頭に足す主人公の内心（第478期は 1-2 の踏破の窓にあった予告の 1 行・仮）。</summary>
    public const string Stage13Lead = "砦は取った。——王国は、勇者を差し向けた。";

    /// <summary>金のセリフを出す金の額。</summary>
    public const int GoldLine = 20;
    /// <summary>吹き出しを出しておく秒数。</summary>
    public const float Seconds = 4f;

    public static Line Of(string key) => All.First(l => l.Key == key);

    /// <summary>話す駒: 手札（隊 → 控えの順）に <paramref name="speaker"/> がいればその駒、いなければ先頭の 1 体（誰もいなければ null）。</summary>
    public static UnitDef? SpeakerOf(Map11State st, string speaker)
    {
        var hand = st.Squads.Where(q => !q.Lost && q.Units is not null).SelectMany(q => q.Units!.Where(u => u.IsAlive).OrderBy(u => u.Slot)).Select(u => u.Def)
            .Concat(st.Bench.Select(u => u.Def)).ToList();
        return hand.FirstOrDefault(d => d.Name.EndsWith(speaker, StringComparison.Ordinal)) ?? hand.FirstOrDefault();
    }

    // ---- 言ったことの覚え（場面を読み直しても残す・1 章を 1-1 から始め直したら捨てる） ----
    private static readonly HashSet<string> Said = new();
    public static bool Mark(string key) => Said.Add(key);
    public static bool WasSaid(string key) => Said.Contains(key);
    public static void Forget() => Said.Clear();
}
