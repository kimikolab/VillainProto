using BattleCore;
using static Common;

// =====================================================================================
// mio モード（第194期） —— 澱みのミオの転生（一点濃縮）
//
// 指示書は design/PHASE194_MIO_SPEC.md ／ 報告は design/PHASE194_MIO.md。
//
// **線は置かない**（指示書の冒頭）。回帰確認（ミオを含まない行が動かないこと）と、
// 版の対照・差し替え表・帳簿だけ。採否はポンが遊んで決める。
//
//     dotnet run --project BattleSim -c Release 0 mio phase0   # Q0-1〜Q0-6 の数え物（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 mio run      # 在席行 × 版（旧／新／マイナスなし／素体）・差し替え表（主表）
//     dotnet run --project BattleSim -c Release 0 mio ledger   # 帳簿（1戦あたり・第2〜5波）
//     dotnet run --project BattleSim -c Release 0 mio check [採用前のbalance.md]  # 自己検査
// =====================================================================================

static partial class MioDiag
{
    const int Seeds = 200;

    /// <summary>毒の書き手（指示書 §4.2 の並び）。**ミオは入れない**（ミオ自身は差し替えの受け手）。</summary>
    static readonly string[] Writers = { "guza", "sid", "rau", "beni", "kata" };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            default:
                if (RunMore(mode, arg)) return;
                Console.WriteLine("mio: モードは phase0 / run / ledger / check。");
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

    static bool HasMio(Formation f) => f.Occupied().Any(o => o.Def.Id == "mio");

    // =================================================================================
    // Phase 0 —— 戦闘0回
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第194期 `mio phase0` —— Q0-1〜Q0-6（戦闘0回）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var cross = CrossBuilds();

        // ---------------- Q0-1 ----------------
        Console.WriteLine("## Q0-1 ミオの在席行と隣の駒");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | ミオの席 | 隣の駒 | 同席の書き手 | 隣の書き手 | ベニ（同席／隣） | ヴィオ（同席／隣） |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (var (band, list) in new[] { ("compare", rows), ("交差帯", cross) })
        foreach (var (name, f) in list)
        {
            var occ = f.Occupied().ToList();
            var me = occ.Where(o => o.Def.Id == "mio").ToList();
            if (me.Count == 0) continue;
            int slot = me[0].Slot;
            var adj = occ.Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)).Select(o => o.Def).ToList();
            var wr = occ.Where(o => Writers.Contains(o.Def.Id)).Select(o => o.Def.Name).ToList();
            var wrAdj = adj.Where(d => Writers.Contains(d.Id)).Select(d => d.Name).ToList();
            string Pair(string id) => (occ.Any(o => o.Def.Id == id) ? "○" : "—") + "／" + (adj.Any(d => d.Id == id) ? "**○**" : "—");
            Console.WriteLine("| " + band + " | " + name + " | " + SlotName(slot) + " | "
                              + string.Join("・", adj.Select(d => d.Name)) + " | "
                              + (wr.Count == 0 ? "—" : string.Join("・", wr)) + " | "
                              + (wrAdj.Count == 0 ? "—" : string.Join("・", wrAdj)) + " | "
                              + Pair("beni") + " | " + Pair("vio") + " |");
        }
        Console.WriteLine();
        int none = rows.Count(r => !HasMio(r.F));
        int noneX = cross.Count(r => !HasMio(r.F));
        Console.WriteLine("ミオを含まない行: `compare` **" + none + " / " + rows.Length + "** ／ 交差帯 **" + noneX + " / " + cross.Length + "**。");
        Console.WriteLine();

        // §4.2 の差し替え表の分母
        Console.WriteLine("### §4.2 差し替え表の分母（毒の書き手を含み、ミオを含まない `compare` 行）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 書き手 | ベニ | ヴィオ | 替える候補（書き手・ミオ以外） |");
        Console.WriteLine("|---|---|---|---|---|");
        int swapRows = 0;
        foreach (var (name, f) in rows)
        {
            var occ = f.Occupied().ToList();
            if (!occ.Any(o => Writers.Contains(o.Def.Id)) || HasMio(f)) continue;
            swapRows++;
            Console.WriteLine("| " + name + " | "
                              + string.Join("・", occ.Where(o => Writers.Contains(o.Def.Id)).Select(o => o.Def.Name)) + " | "
                              + (occ.Any(o => o.Def.Id == "beni") ? "○" : "—") + " | "
                              + (occ.Any(o => o.Def.Id == "vio") ? "○" : "—") + " | "
                              + occ.Count(o => !Writers.Contains(o.Def.Id)) + " 枚 |");
        }
        Console.WriteLine();
        Console.WriteLine("分母: **" + swapRows + "** 行（グザを含む "
                          + rows.Count(r => !HasMio(r.F) && r.F.Occupied().Any(o => o.Def.Id == "guza")) + " 行）。");
        Console.WriteLine();

        if (!ParryScan.Init()) return;
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
        string models = ParryScan.Read(Path.Combine("BattleCore", "Models.cs"));

        // ---------------- Q0-2 ----------------
        Console.WriteLine("## Q0-2 現行の `Thicken` の処理順と計数");
        Console.WriteLine();
        int ti = traits.IndexOf("private static void " + "Thicken(");
        if (ti < 0) { Console.WriteLine("**`Thicken` が見つからない。走査が空なので止める**（R034）。"); return; }
        string tb = traits.Substring(ti, traits.IndexOf("\n}\n", ti) - ti);
        var counters = System.Text.RegularExpressions.Regex.Matches(tb, @"at\.(Amp\w+)").Select(m => m.Groups[1].Value).Distinct().ToList();
        Console.WriteLine("- 本体 " + tb.Split('\n').Length + " 行。敵を `LivingMembers`（席番号順）で1体ずつ見る。"
                          + "毒 0 なら傷を見て着火（`WoundIgnite`）して `continue`、毒があれば `+" + AmplifierTrait.Step + "`。**乱数を引かない**。");
        Console.WriteLine("- 書き込みは `SetCounter` 直（`ctx.Poison` の窓口を通らない＝滲み則・`StatusGain` の台本に載らない）: "
                          + (tb.Contains("ctx.Poison(") ? "**通る**" : "通らない"));
        Console.WriteLine("- 触る計数: " + string.Join("・", counters.Select(c => "`" + c + "`")));
        Console.WriteLine("- 手番（`OnAction`）で撃つ: " + (traits.Contains("OnAction(BattleContext ctx, UnitState self, UnitAction action) => Thicken") ? "○" : "×")
                          + " ／ `Actions` を持たない保持者はターン頭（`ActsOnPattern`）");
        Console.WriteLine("- **既存の計数を壊さない形**: 新しい札（`Concentrate`）は `Thicken` の本体をそのまま呼んでから (2) を足す。"
                          + "(2) の計数は `UnitTally` に新しい欄を足し、`Amp*` には触らない");
        Console.WriteLine();

        // ---------------- Q0-3 ----------------
        Console.WriteLine("## Q0-3 毒の層の型と上限");
        Console.WriteLine();
        bool counterInt = models.Contains("public void SetCounter(string key, int v)");
        Console.WriteLine("- カウンタの型: " + (counterInt ? "`int`（`UnitState.Counters`）" : "**不明**"));
        Console.WriteLine("- 層の上限: `Poison(` 窓口にも `TickStatuses` にも**無い**（`Math.Min` の走査: 毒の書き込み行に "
                          + (engine.Contains("Math.Min(u.RawCounter(StatusKeys.Poison)") ? "ある（読みだけ）" : "無い") + "）");
        Console.WriteLine("- 1 層から倍々で `int.MaxValue` を越えるまで: **" + (int)Math.Ceiling(Math.Log2(int.MaxValue)) + " 回**。"
                          + "10 層からなら " + (int)Math.Ceiling(Math.Log2(int.MaxValue / 10.0)) + " 回、100 層からなら "
                          + (int)Math.Ceiling(Math.Log2(int.MaxValue / 100.0)) + " 回");
        Console.WriteLine("- 戦闘の上限は 30 ターン。**倍々は毎ミオの手番（1ターン1回）**なので、同じ駒が 30 回倍になれば桁あふれする");
        Console.WriteLine("- **倒れない限り倍々が続く駒**: ベニの反転の内側（隣がベニ、またはベニ自身）にいる味方——毒の刻みが回復に変わるので、"
                          + "ミオの隣でベニの隣なら層だけが積もり続ける。ヴィオは毎ターン頭に吸うので積もらない");
        Console.WriteLine("- さらに倍を掛ける読み手: ベニの旧札 `Devour`（×" + DevourTrait.AllyPoisonMultiplier + "・保持者 0 枚）と"
                          + "カタの両方持ち（×" + CatalystTrait.DualMultiplier + "）。**層 × 4 が `int` に収まる必要がある**");
        Console.WriteLine();

        // ---------------- Q0-4 ----------------
        Console.WriteLine("## Q0-4 毒の層を読む札");
        Console.WriteLine();
        Console.WriteLine("`Traits.cs` で `StatusKeys.Poison` を読むクラス（書くだけの行も含む）と、engine 側の読み。保持者は `UnitCatalog.All`。");
        Console.WriteLine();
        Console.WriteLine("| ファイル | クラス／場所 | 読み方 | 保持者（All） |");
        Console.WriteLine("|---|---|---|---|");
        {
            string[] lines = traits.Split('\n');
            string cls = "";
            var seen = new Dictionary<string, List<string>>();
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                int ci = l.IndexOf("class ");
                if (ci >= 0 && (l.Contains("sealed class") || l.Contains("abstract class") || l.Contains("partial class")))
                    cls = l.Substring(ci + 6).Split(' ', ':', '\r')[0];
                if (!l.Contains("StatusKeys." + "Poison") || l.TrimStart().StartsWith("//")) continue;
                string how = l.Contains("SetCounter(StatusKeys." + "Poison, 0)") ? "0 にする（吸う）"
                           : l.Contains("SetCounter(") ? "書く"
                           : l.Contains("Counter(StatusKeys." + "Poison)") ? "読む" : "その他";
                if (!seen.TryGetValue(cls, out var hs)) seen[cls] = hs = new();
                if (!hs.Contains(how)) hs.Add(how);
            }
            foreach (var (c, hs) in seen)
                Console.WriteLine("| Traits.cs | " + c + " | " + string.Join("・", hs) + " | " + HoldersOf(c) + " |");
        }
        foreach (var (where, what) in new[]
        {
            ("TickStatuses", "層の分だけ刻む（減らない）・ベニの反転で回復へ"),
            ("DetonateOne", "カタの起爆で層の分だけもう1回（両方持ちは ×2）"),
            ("NoteScapegoatDot", "業の帰属（計数）"),
            ("Poison(", "書く窓口（滲み則 +1/+2）"),
        })
            Console.WriteLine("| BattleEngine.cs | " + where + " | " + what + " | " + (engine.Contains(where) ? "—" : "**見つからない**") + " |");
        Console.WriteLine();

        // ---------------- Q0-5 ----------------
        Console.WriteLine("## Q0-5 行動順（1ターンの中）");
        Console.WriteLine();
        Console.WriteLine("ターンは `TickStatuses`（毒 → 燃焼）→ `OnTurnStart`（席順）→ 行動順ループ（速さ降順・同速は乱数）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 速さ | 働く場所 |");
        Console.WriteLine("|---|--:|---|");
        foreach (var (d, where) in new[]
        {
            (UnitCatalog.Mio, "手番（濃縮）"), (UnitCatalog.Sid, "被弾時（毒撃・漏れ）"),
            (UnitCatalog.Vio, "ターン頭（吸い上げ）・攻撃の後（吐き戻し）"), (UnitCatalog.Kata, "手番（起爆）"),
            (UnitCatalog.Beni, "手番（火→毒→毒）・刻みの反転"), (UnitCatalog.Guza, "手番（瘴気）"),
            (UnitCatalog.Rau, "攻撃の後（うつす）・死骸（疫み）"),
        })
            Console.WriteLine("| " + d.Name + " | " + d.Speed + " | " + where + " |");
        Console.WriteLine();
        Console.WriteLine("- **刻み（ターン頭の最初）が先、濃縮（速8）は後**。倍にした層は次のターン頭に刻まれる");
        Console.WriteLine("- 速8 のミオは速5 のグザより先に動く——**同じターンにグザが撒いた +2 は、そのターンの濃縮に乗らない**（次のターンに乗る）");
        Console.WriteLine("- ヴィオの吸い上げは `OnTurnStart`＝**刻みの後**。ミオが倍にした味方の毒は、次のターン頭に**1回刻まれてから**吸われる");
        Console.WriteLine();

        // ---------------- Q0-6 ----------------
        Console.WriteLine("## Q0-6 過去の類似測定（`design/` の grep）");
        Console.WriteLine();
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "design");
        if (Directory.Exists(dir))
        {
            foreach (string word in new[] { "2倍", "倍々", "濃縮", "指数" })
            {
                var hits = Directory.GetFiles(dir, "*.md")
                    .Where(p => !Path.GetFileName(p).StartsWith("PHASE194"))
                    .Where(p => File.ReadAllText(p).Contains(word))
                    .Select(Path.GetFileName).OrderBy(x => x).ToList();
                Console.WriteLine("- 「" + word + "」: " + hits.Count + " ファイル"
                                  + (hits.Count == 0 ? "" : "（" + string.Join("・", hits.Take(12)) + (hits.Count > 12 ? " …" : "") + "）"));
            }
        }
        Console.WriteLine();
    }

    static string HoldersOf(string cls)
    {
        if (cls.Length == 0) return "—";
        var hit = UnitCatalog.All.Where(d => d.Traits.Any(t =>
        {
            try { return TraitCatalog.Get(t).GetType().Name == cls; } catch (KeyNotFoundException) { return false; }
        })).Select(d => d.Name).ToList();
        return hit.Count == 0 ? "0 枚" : string.Join("・", hit);
    }
}
