using BattleCore;
using static Common;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// stage hane（第172期 部C）—— **「ハネは入れないほうが勝てるのでは」を、マップの上で確かめる**
//
// 指示書は design/PHASE172_REFORM_SPEC.md §3。**線は置かない**（採否を決める測定ではない。
// ハネを転生させるかどうかの材料を出すだけ）。
//
// 台は第168期 部B で3つの線を同時に通した1点をそのまま使う——
// **`S4/S4`（両方の道が斥候級の4体）× 道中の回復 50% × 正の割り当て × seed 800**。
// **敵も道も隊も1つも作らない**（`ScoutSquads` / `ScoutSquad` / `BandBand` をそのまま呼ぶ）。
//
// 振るのは**かき回し隊の 後3 の1枠だけ**:
//   対照  突き返しのハネ（現行）
//   素体  特性なし・同数値（`checkup` の素体差し替えと同じ作り方）
//   候補  第172期 §1-2 の控えの駒 6 枚
//
//     dotnet run --project BattleSim -c Release 0 stage hane
//     dotnet run --project BattleSim -c Release 0 stage hane "kubi,sekki"   # 候補を絞る
// =====================================================================================

static partial class StageDiag
{
    /// <summary>
    /// 控えの駒 7 枚（<b>`DemoApp` の <c>Map11.Reserves</c> と同じ並び</b>）。
    /// <b>第173期 §1-3 #4 に空焚きのホタを足して 6 → 7 枚にした</b>
    /// ——控えには火を撒く駒（ボルグ）も火を読む駒（ヒヨ）もいるのに、<b>火の受け手だけが欠けていた</b>。
    ///
    /// <para><b>ここは写しである。</b> 選ぶ規則そのものは `DemoApp/Map11.cs`（`ReserveRanking`）が持ち、
    /// その出力は <c>--map11-phase172</c> が表にする——`BattleSim` は `DemoApp` を参照できないので、
    /// <b>規則を2回書くより、結果を1行で写して出どころを書くほうが安全</b>と判断した。
    /// 差し替えたらこの行も直すこと。</para>
    /// </summary>
    const string HaneReserves = "kubi,sekki,hota,sero,borg,gan,hagi";

    /// <summary>かき回し隊で振る席（後3）。<b>`突き返し (ハネ×ウツ)` の行でハネが座っている席。</b></summary>
    const int HaneSlot = 4;

    /// <summary>ウツ＝逆しま。ハネの「隣の腕を鈍らせる」が餌になっているかを見る相手（§3 の併記）。</summary>
    const string HaneReader = "utsu";

    static void HaneEntry(string arg)
    {
        string[] want = arg.Trim().Length == 0
            ? HaneReserves.Split(',')
            : arg.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

        var (a, b, subs) = ScoutSquads(out string filler, out _);
        var (cand, med) = BandReserve(a, b, BandProbeSeeds);
        Formation res = cand[0].F;
        int[] straight = { 1, 0 };   // 正: カド隊 → 南(1) ／ かき回し隊 → 北(0)

        // 道は第168期 部B の通った 1 点（`S4/S4`）。**`EnemyCatalog` から引くだけ。**
        int s4 = Array.FindIndex(ScoutForms, f => f.Name == "S4");
        var roads = new[]
        {
            (IReadOnlyList<Formation>)new[] { ScoutSquad(1, s4), EnemyCatalog.Stages[2].Enemy },
            (IReadOnlyList<Formation>)new[] { ScoutSquad(3, s4), EnemyCatalog.Stages[4].Enemy },
        };

        UnitDef seated = b[HaneSlot] ?? throw new InvalidOperationException("後3 が空いている");

        Console.WriteLine("# 第172期 部C —— かき回し隊の 後3 を振る（`S4/S4` × 回復 "
            + $"{ScoutRecover}% × 正 × seed 0..{BandSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("**線は置かない。** 採否を決める測定ではなく、"
            + "「ハネは入れないほうが勝てるのでは」（第171期のポンの疑い）に数字を当てるだけ。");
        Console.WriteLine();
        Console.WriteLine($"カド隊 {Show(a)}");
        Console.WriteLine();
        Console.WriteLine($"かき回し隊 {Show(b)}"
            + (subs.Length == 0 ? "" : $"（{string.Join(" / ", subs.Select(x => $"{x.Slot} {x.From} → {x.To}"))}）"));
        Console.WriteLine();
        Console.WriteLine($"第3隊 `{cand[0].Name}`（61 行の中央値 {med:F1}%）。"
            + $"振るのは **後3 の1枠だけ**（いまは {seated.Name}）。");
        Console.WriteLine();

        // 版を並べる。**素体は `checkup` と同じ作り方**（同じ数値・札だけ落とす）。
        var versions = new List<(string Tag, string Name, Formation F)>
        {
            ("対照", seated.Name, b),
            ("素体", $"{seated.Name}（特性なし）", WithSlot(b, HaneSlot, Bare(seated))),
        };
        foreach (string id in want)
        {
            UnitDef d = UnitCatalog.Everyone.FirstOrDefault(u => u.Id == id)
                        ?? throw new ArgumentException("その駒がいない: " + id);
            versions.Add(("候補", d.Name, WithSlot(b, HaneSlot, d)));
        }

        Console.WriteLine("## 1. 踏破率・2 本抜き率・部分点");
        Console.WriteLine();
        Console.WriteLine("| 版 | 後3 | 踏破率 | かき回し隊×北 の 2 本抜き | 部分点 | 戦闘回数 "
            + $"| 対照との差（踏破） | ウツの `与えた総害` |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        double baseFull = 0, baseHarm = 0;
        for (int i = 0; i < versions.Count; i++)
        {
            var (tag, name, f) = versions[i];
            var harm = new Dictionary<string, double>();
            BandStat st = BandBand(new[] { a, f }, res, straight, roads, 0, ScoutRecover, harm);
            double reader = harm.GetValueOrDefault(HaneReader);
            if (i == 0) { baseFull = st.Full; baseHarm = reader; }
            Console.WriteLine($"| {tag} | {name} | {st.Full:F1}% | {st.TwoB:F1}% | {st.Partial:F3} "
                + $"| {st.Battles:F2} | {(i == 0 ? "—" : $"{st.Full - baseFull:+0.0;-0.0;0.0}")} "
                + $"| {reader:F1}{(i == 0 ? "" : $"（{reader - baseHarm:+0.0;-0.0;0.0}）")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**ウツの `与えた総害` は 1 マップあたりの平均**（`TallyByUnit[\"{HaneReader}\"].DamageToEnemy` を"
            + "そのマップの全戦闘ぶん足して、seed で割ったもの）。**判定には1ビットも使わない——読むだけ。**");
        Console.WriteLine();
        Console.WriteLine("**自己検査 (f)**: 対照の行が第169期の値（踏破 95.6% ／ 2 本抜き 38.0%）と一致すること。"
            + "一致しなければ、台が第168期 部B の1点からずれている。");
    }

    /// <summary>その席だけ差し替えた編成を作る（元の <see cref="Formation"/> は触らない）。</summary>
    static Formation WithSlot(Formation src, int slot, UnitDef def)
    {
        var f = new Formation();
        foreach ((int s, UnitDef d) in src.Occupied()) f[s] = d;
        f[slot] = def;
        return f;
    }

    /// <summary>
    /// 素体（<c>checkup</c> の素体差し替えと同じ）。<b>数値・攻撃型・手番はそのまま、札だけ落とす。</b>
    /// </summary>
    static UnitDef Bare(UnitDef d) => new()
    {
        Id = d.Id + "_bare",
        Name = d.Name + "（素体）",
        MaxHp = d.MaxHp,
        Attack = d.Attack,
        Speed = d.Speed,
        Pattern = d.Pattern,
        Advances = d.Advances,
        Traits = Array.Empty<TraitId>(),
    };
}
