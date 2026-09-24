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

        // ---------------- Q0-7（指示書の変更で足した） ----------------
        Console.WriteLine("## Q0-7 起爆（カタ）の刻みに印の2倍を含めてよいか——二重に倍化しないか");
        Console.WriteLine();
        {
            int di = engine.IndexOf("void " + "DetonateOne(");
            int dj = di < 0 ? -1 : engine.IndexOf("static void " + "DetonateHit", di);
            string db = di < 0 || dj < 0 ? "" : engine.Substring(di, dj - di);
            if (db.Length == 0) { Console.WriteLine("**`DetonateOne` が見つからない。走査が空なので止める**（R034）。"); return; }
            Console.WriteLine("- `DetonateOne` の本体 " + db.Split('\n').Length + " 行。毒は `RawCounter(StatusKeys.Poison)`（**生の層**）から、燃焼は `BurnRules.Damage`（**定数**）から"
                              + "毎回作り直す: 毒 " + (db.Contains("u.RawCounter(StatusKeys.Poison)") ? "○" : "**×**")
                              + " ／ 燃焼 " + (db.Contains("BurnRules.Damage * mult") ? "○" : "**×**"));
            Console.WriteLine("- ターン頭の刻み（`TickStatuses`）の結果を読み直す経路: " + (db.Contains("PoisonTick") || db.Contains("BurnTaken") ? "**ある**" : "無い")
                              + "——**起爆は刻みを写すのではなく同じ式を別に計算している**ので、印の ×2 を起爆の中で1回掛けても二重にはならない");
            Console.WriteLine("- 起爆の中の既存の倍: 両方持ちの ×" + CatalystTrait.DualMultiplier + "（敵だけ）と旧 `Devour` の ×" + DevourTrait.AllyPoisonMultiplier
                              + "（保持者 0 枚）。**印は別の源の倍なので、印あり・両方持ちの敵は ×4 になる**（同じ印を2回掛けるのではない）");
            Console.WriteLine("- 判断: **起爆の刻みにも印を含める**（指示書の変更どおり）。印の分は起爆の帳簿とは別に数える");
        }
        Console.WriteLine();

        // ---------------- Q0-8 ----------------
        Console.WriteLine("## Q0-8 燃焼の刻みを読む札と、2倍にしたとき二重に倍化する経路");
        Console.WriteLine();
        Console.WriteLine("| ファイル | クラス／場所 | 読むもの | 保持者（All） |");
        Console.WriteLine("|---|---|---|---|");
        {
            string[] lines = traits.Split('\n');
            string cls = "";
            var seen = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                int ci = l.IndexOf("class ");
                if (ci >= 0 && (l.Contains("sealed class") || l.Contains("abstract class"))) cls = l.Substring(ci + 6).Split(' ', ':', '\r')[0];
                if (!l.Contains("Counter(StatusKeys." + "Burn)") || l.TrimStart().StartsWith("//") || seen.Contains(cls)) continue;
                seen.Add(cls);
                Console.WriteLine("| Traits.cs | " + cls + " | 燃えているか（`> 0`）＝**刻みの量は読まない** | " + HoldersOf(cls) + " |");
            }
            if (seen.Count == 0) { Console.WriteLine("**走査が空なので止める**（R034）。"); return; }
        }
        int burnConst = (engine.Length - engine.Replace("BurnRules." + "Damage", "").Length) / ("BurnRules." + "Damage").Length;
        Console.WriteLine("| BattleEngine.cs | `BurnRules.Damage` の出現 " + burnConst + " 箇所 | 刻み（ホタの枝・反転・本体）・起爆（反転・本体）・澱み分けの計数（`KindlePostBurn`） | — |");
        Console.WriteLine("| BattleEngine.cs | `ApplyDamage(…, burnTick: true)` | 破片の吸い（`BurnSoaked`）・浴びた量（`BurnTaken`）・軛の計数・害の経路 | — |");
        Console.WriteLine("| BattleEngine.cs | ベニの反転（`InverseHeal`） | 刻みの量をそのまま回復にする——**印があれば回復も2倍**（指示書の想定どおり） | — |");
        Console.WriteLine();
        {
            // OnDamaged で出どころ null（刻み）を弾かない札＝刻みの量を燃料にする札
            string[] lines = traits.Split('\n');
            string cls = "";
            var fuel = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                int ci = l.IndexOf("class ");
                if (ci >= 0 && (l.Contains("sealed class") || l.Contains("abstract class"))) cls = l.Substring(ci + 6).Split(' ', ':', '\r')[0];
                if (!l.Contains("override void " + "OnDamaged(")) continue;
                string body = string.Join("\n", lines.Skip(i).Take(14));
                bool skipsNull = body.Contains("source is null") || body.Contains("source == null") || body.Contains("source is not UnitState")
                                 || body.Contains("if (source is not");
                if (!skipsNull) fuel.Add(cls + "（" + HoldersOf(cls) + "）");
            }
            Console.WriteLine("- `OnDamaged` で出どころ null（毒・燃焼の刻み）を**弾かない**札（先頭 14 行の走査）: " + fuel.Count + " 本——"
                              + string.Join("・", fuel));
            Console.WriteLine("  これらは印で大きくなった刻みを**そのまま大きな被弾として読む**。倍の源は印1つなので二重ではない（流れるだけ）");
        }
        Console.WriteLine("- 熾のホタ（`Pyre`）は燃焼の刻みで HP を削られない（`Ember.Fireproof`）ので、印があっても 0 のまま");
        Console.WriteLine("- 着火の持続係数・`BurnTicks`・業の帰属（`NoteScapegoatDot`）: 回数と量の計数だけ。量の側（業）は印で2倍になった値を受け取る");
        Console.WriteLine("- **二重に倍化する経路は、起爆の両方持ち（×2 × 印 ×2 ＝ ×4・Q0-7）だけ**。刻みの本体は1箇所で1回だけ掛ける");
        Console.WriteLine();

        // ---------------- Q0-9 ----------------
        Console.WriteLine("## Q0-9 印を付ける相手の選び方（測る前に固定）");
        Console.WriteLine();
        Console.WriteLine("- 「次の刻み」＝ 毒の層（+4 の後）＋ 燃焼中なら `BurnRules.Damage`（" + BurnRules.Damage + "）。**印の ×2 は含めない**"
                          + "（含めると既に印のある駒を選び続け、重ねがけしない印が止まる）");
        Console.WriteLine("- 同値は席番号の小さい方。次の刻みが 0 の敵しかいなければ空振り（数える）");
        Console.WriteLine("- 印は周りの敵に**毒や火が無くても付く**（あとから来た毒・火に効く）。漏れも同じ（ミオの隣の味方全員）");
        Console.WriteLine("- **印の寿命は戦闘の終わりまで**（指示書に書かれていないので決めた）。重ねがけしない＝付いている駒にはもう一度付けない。"
                          + "会戦の境界では `StatusKeys.All` と一緒に消える");
        Console.WriteLine();

        // ---------------- Q0-10（2度目の変更で足した） ----------------
        Console.WriteLine("## Q0-10 刻みごとに発火するものの一覧と、2回反応すると壊れるものがないか");
        Console.WriteLine();
        {
            int a = engine.IndexOf("public void " + "TickStatuses()");
            int b = a < 0 ? -1 : engine.IndexOf("public bool " + "Detonate(", a);
            string tick = a < 0 || b < 0 ? "" : engine.Substring(a, b - a);
            int burnAt = tick.IndexOf("StatusKeys." + "Burn);");
            if (tick.Length == 0 || burnAt < 0) { Console.WriteLine("**`TickStatuses` が見つからない。走査が空なので止める**（R034）。"); return; }
            string pz = tick.Substring(0, burnAt), bz = tick.Substring(burnAt);
            Console.WriteLine("`TickStatuses` の本体（" + tick.Split('\n').Length + " 行）を毒のループと燃焼のループに割り、刻みのたびに走るものを並べた。"
                              + "**「1回だけ」** は印の2回目で走らせないもの（層・残りターンの減算と、それに付いた区間の帳簿）。");
            Console.WriteLine();
            Console.WriteLine("| ループ | 走るもの | 毒 | 燃焼 | 2回目 | 2回走ったとき |");
            Console.WriteLine("|---|---|:-:|:-:|---|---|");
            foreach (var (token, what, second, effect) in new[]
            {
                ("SetCounter(StatusKeys." + "Burn, left - 1)", "燃焼の残りターンを1減らす", "**1回だけ**", "（指示どおり1回分しか減らさない）"),
                ("CloseBurnEpisode(", "燃え尽きた区間を閉じる（第134期の帳簿）", "**1回だけ**", "減算に付いているので減算と一緒に1回"),
                ("bt.BurnTicks++", "燃焼の刻みの回数（計数）", "2回", "計数が2になるだけ"),
                ("DevourTrait.AllyPoisonMultiplier", "旧ベニの味方の毒 ×2（保持者 0 枚）", "2回", "量の計算。同じ量を2回使う"),
                ("Ember.TickHeal", "熾のホタの「焼かれない」の枝（既定 0）", "2回", "既定 0 なので何も起きない（ノブを上げると回復が2回）"),
                ("InvertsTick(", "ベニの反転の判定", "2回", "—"),
                ("InverseHeal(", "反転の回復（＋啜り）", "2回", "**回復が2回**（指示どおり）。`InverseRecipientTurns` はターンで重複を除くので延べ人数は1"),
                ("NoteTaintPostBite(", "澱み分けの層の刻みの計数", "2回", "計数だけ"),
                ("NoteKindlePostBurn(", "ベニの火の刻みの計数", "2回", "計数だけ"),
                ("ClearKindleHeld(", "ベニの火の印を消す（計数専用）", "2回", "2回目は既に消えている＝何もしない"),
                ("NoteScapegoatDot(", "業の帰属（保持者 0 枚）", "2回", "燃焼の側は業の借り（`OwedKey`）を1回ごとに1減らす——**2回走ると借りが2倍の速さで尽きる**。計数専用で保持者 0 枚なので盤面は壊れない"),
                ("NotePoisonBite(", "毒の刻みの額面（陣営別の計数）", "2回", "計数だけ"),
                ("IgnitePoisonDamage", "傷口の着火の持続係数（計数）", "2回", "計数だけ"),
                ("ApplyDamage(", "HP を削る（破片・軛・肩代わり・死亡処理・`OnDamaged`）", "2回", "**軛は1回ずつに効く**。1回目で倒れたら2回目は走らせない（生存を見る）"),
                ("BurnDeaths++", "燃焼で倒れた数（計数）", "2回", "生存を見るので二重には数えない"),
                ("Emit(", "台本の `Status`（刻みの絵）", "2回", "**同じ駒に `Status` と `Damage` の組が2つ並ぶ**（再生側は `Status` の直後の `Damage` を紐づけるので組のまま読める）"),
            })
            {
                bool inP = pz.Contains(token), inB = bz.Contains(token);
                Console.WriteLine("| " + (inP && inB ? "両方" : inP ? "毒" : inB ? "燃焼" : "**見つからない**") + " | " + what + " | "
                                  + (inP ? "○" : "—") + " | " + (inB ? "○" : "—") + " | " + second + " | " + effect + " |");
            }
            Console.WriteLine();
            Console.WriteLine("- `ApplyDamage` の先で刻み（出どころ null）に反応する札は Q0-8 の 5 本（ガルド・カドの棘守り・ドハ・ササ・0 枚1本）。どれも「浴びた量」を読むだけで、2回浴びれば2回読む");
            Console.WriteLine("- **壊れるもの: 見つからない**。注意は2つ——(1) 1回目で倒れた駒に2回目を当てない（死亡処理が2回走る）、(2) 業の燃焼の借りが2倍の速さで尽きる（計数専用・保持者 0 枚）");
        }
        {
            int di = engine.IndexOf("void " + "DetonateOne(");
            string db = di < 0 ? "" : engine.Substring(di, Math.Max(0, engine.IndexOf("static void " + "DetonateHit", di) - di));
            Console.WriteLine("- 起爆（`DetonateOne`）は減算を1つも持たない（層・残りターンを減らさない）: "
                              + (db.Contains("SetCounter(") ? "**持つ**" : "○") + "。**1体ぶんの本体を2回呼ぶだけで「2回」になり、1回だけにすべきものが無い**");
            Console.WriteLine("- **判断: 起爆の刻みも印で2回にする**（印の意味を「この駒の刻みは、どの口から来ても2回」に揃える）。両方持ちの ×2 は1回ずつに掛かる"
                              + "（2回 × 各 ×2）。1回目で倒れたら2回目は当てない");
        }
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
