using BattleCore;
using static Common;

// =====================================================================================
// lili モード（第204期） —— 継ぎ当てのノノを「施しのリリ」に転生させる（殴れるヒーラー）
//
// 指示書は design/PHASE204_LILI_SPEC.md ／ 報告は design/PHASE204_LILI.md。
//
// **線は置かない**（指示書の冒頭）。回帰確認（リリを含まない行が動かないこと）と、
// 版の対照・帳簿だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 lili phase0   # Q0-1・Q0-3 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 lili run      # 在席行 × 版（旧／新／移さない／吸うだけ／30）
//     dotnet run --project BattleSim -c Release 0 lili ledger   # 帳簿（1戦あたり・第2〜5波）
//     dotnet run --project BattleSim -c Release 0 lili check [第203期のbalance.md]  # 自己検査
// =====================================================================================

static partial class LiliDiag
{
    const int Seeds = 200;

    /// <summary>リリ（転生前はノノ）の Id。<b>Phase 0 は転生の前に回すので両方を見る。</b></summary>
    static bool IsLili(UnitDef d) => d.Id is "lili" or "nono";

    static bool HasLili(Formation f) => f.Occupied().Any(o => IsLili(o.Def));

    /// <summary>
    /// 敵に状態を書く味方（<b>手で持つ表</b>・Q0-1 の見当）。本当に移った状態は `ledger` が実測で数える
    /// ——この表は「同じ行にいるか」を数えるためだけにある。
    /// </summary>
    static readonly (string Id, string Keys)[] Writers =
    {
        ("borg", "燃"), ("zoto", "燃"), ("guza", "毒"), ("sid", "毒・痺れ毒"), ("rau", "毒"), ("mio", "毒・濃縮"),
        ("tou", "痺"), ("nel", "呪"), ("mudo", "呪"), ("kugu", "組み付き"), ("shiga", "竦み"), ("kubi", "萎縮"),
        ("hane", "転倒"), ("basa", "混乱"), ("zan", "標"), ("sora", "標"), ("nomi", "傷"), ("kiri", "傷"),
    };

    /// <summary>味方の側で状態を読む駒（Q0-1）。</summary>
    static readonly (string Id, string Reads)[] Readers =
    {
        ("hota", "燃（×4・貫き・焼かれない）"), ("beni", "隣の毒・燃を回復に"), ("vio", "味方の毒を吸う"),
        ("uro", "破片（纏い）"), ("gare", "破片（礫）"), ("utsu", "弱体（`AtkBonus`・状態キーではない）"),
    };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "phase205": Phase205(); return;
            case "phase206": Phase206(); return;
            default:
                if (RunMore(mode, arg)) return;
                Console.WriteLine("lili: モードは phase0 / run / ledger / check。");
                return;
        }
    }

    static partial void RunMoreImpl(string mode, string arg, ref bool handled);

    static bool RunMore(string mode, string arg)
    {
        bool handled = false;
        RunMoreImpl(mode, arg, ref handled);
        return handled;
    }

    static string SlotName(int s) => s switch
    {
        0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => "○" + s,
    };

    // =================================================================================
    // Phase 0 —— 戦闘0回
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第204期 `lili phase0` —— Q0-1・Q0-3（戦闘0回）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var cross = CrossBuilds();

        Console.WriteLine("## Q0-1 在席行と、状態の書き手・読み手");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 席 | 隣の駒 | 状態の書き手（同じ行） | 読む味方（同じ行） |");
        Console.WriteLine("|---|---|---|---|---|---|");
        int n = 0;
        foreach (var (band, list) in new[] { ("compare", rows), ("交差帯", cross) })
        foreach (var (name, f) in list)
        {
            var occ = f.Occupied().ToList();
            var me = occ.Where(o => IsLili(o.Def)).ToList();
            if (me.Count == 0) continue;
            if (band == "compare") n++;
            int slot = me[0].Slot;
            var adj = occ.Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)).Select(o => o.Def.Name).ToList();
            var wr = occ.SelectMany(o => Writers.Where(w => w.Id == o.Def.Id).Select(w => o.Def.Name + "（" + w.Keys + "）")).ToList();
            var rd = occ.SelectMany(o => Readers.Where(w => w.Id == o.Def.Id).Select(w => o.Def.Name + "（" + w.Reads + "）")).ToList();
            Console.WriteLine("| " + band + " | " + name + " | " + SlotName(slot) + " | " + string.Join("・", adj) + " | "
                              + (wr.Count == 0 ? "—" : string.Join("・", wr)) + " | " + (rd.Count == 0 ? "—" : string.Join("・", rd)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("在席: `compare` **" + n + " / " + rows.Length + "** 行 ／ 含まない行 **" + rows.Count(r => !HasLili(r.F)) + "** 行。");
        Console.WriteLine();
        Console.WriteLine("## Q0-3 状態キーの分類（`StatusKeys.All` を全部当てる）");
        Console.WriteLine();
        Console.WriteLine("| キー | 表示 | 分類 | 移す | 重ね方 | 理由 |");
        Console.WriteLine("|---|---|---|:-:|---|---|");
        int missing = 0;
        foreach (string k in StatusKeys.All)
        {
            var c = Classify(k);
            if (c is null) { missing++; Console.WriteLine("| `" + k + "` | " + StatusKeys.LabelOf(k) + " | **分類なし** | ? | ? | ? |"); continue; }
            Console.WriteLine("| `" + k + "` | " + StatusKeys.LabelOf(k) + " | " + c.Value.Kind + " | " + (c.Value.Move ? "○" : "—") + " | "
                              + c.Value.Stack + " | " + c.Value.Why + " |");
        }
        Console.WriteLine();
        Console.WriteLine(missing == 0 ? "分類の抜け: **0 件**。" : "**分類の抜けが " + missing + " 件ある。止める**（R260）。");
        Console.WriteLine();
    }

    /// <summary>Q0-3 の分類（<b>手書き</b>。抜けは Phase 0 が名指しで落とす）。本物の除外は <c>KissTrait.Excluded</c>。</summary>
    static (string Kind, bool Move, string Stack, string Why)? Classify(string k) => k switch
    {
        "poison" => ("量", true, "足す", "層。味方に載っても同じ刻み"),
        "marked" => ("二値・陣営で読み方が変わる", true, "大きい方", "敵の標＝+50%・止め／味方の標＝狙われる。どちらの陣営でも「狙われる印」なので壊れない"),
        "stun" => ("二値", true, "大きい方", "次の手番とターン外を失う"),
        "burn" => ("残りターン", true, "大きい方（点け直しと同じ）", "`Ignite` は通さない（残りを 3 に上書きして複製になる・第49期）"),
        "idleTurn" => ("engine の記録", false, "—", "手番を落としたターン番号。状態ではない（業も除外）"),
        "armor" => ("量・資源", true, "足す", "敵の破片を奪う（実測で敵に載る経路は 0 本）"),
        "wound" => ("量", true, "足す", "傷"),
        "deep" => ("二値", true, "大きい方", "深手（既定で無効・載らない）"),
        "curse" => ("二値", true, "大きい方", "呪い。共有は同じ陣営の呪い持ちどうし（陣営をまたがない）"),
        "stagger" => ("二値", true, "大きい方", "転倒"),
        "confused" => ("二値", true, "大きい方", "混乱（次の一撃を自軍へ）"),
        "ward" => ("保持者の内部の残高", false, "—", "預かり（ノチ・保持者 0 枚）。持ち主と対でしか意味が無い"),
        "debt" => ("保持者の内部の残高", false, "—", "負債（アガ・保持者 0 枚）"),
        "ash" => ("保持者の内部の燃料", false, "—", "灰（スス）。味方どうしの傷の量で、敵には載らない"),
        "grappled" => ("相手と対の記録", false, "—", "組み付き。ほどくのは組み付いたクグの記憶だけ——味方に移すと誰もほどけず永久に手番を失う"),
        "cowed" => ("二値", true, "大きい方", "竦み"),
        "footing" => ("保持者の内部の層", false, "—", "据えの層（バン自身）。敵には載らない"),
        "daunted" => ("二値", true, "大きい方", "萎縮"),
        "concentrated" => ("量", true, "足す", "濃縮の印（刻みが 1+n 回）"),
        "numbed" => ("二値（値は経路）", true, "大きい方", "痺れ毒の印。与ダメが自分の毒の層 × 3% 下がる"),
        "guren" => ("保持者の内部の燃料", false, "—", "紅蓮（ベニ）。敵には載らない"),
        "stigma" => ("リリの数え札", false, "—", "指示書 §2.5「聖痕は移さない」"),
        "plank" => ("ツギの印", false, "—", "第207期・板の印。味方にしか付かない（敵には載らない）"),
        "shock" => ("二値", true, "大きい方", "第214期・感電。移った味方は殴られると弾けて隣の味方へ放電する（移すのは口移しの代金のまま）"),
        _ => null,
    };
}
