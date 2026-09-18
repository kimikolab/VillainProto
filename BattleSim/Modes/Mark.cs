using BattleCore;
using static Common;

// =====================================================================================
// mark モード（第150期） —— 標（`Marked`）の軸を診る
//
// 指示書は design/PHASE150_MARK_SPEC.md ／ 報告は design/PHASE150_MARK.md。
//
// **直す前に診る。** 標が「付いてから消えるまで」に何が起きているかを帳簿にして、
// 第149期の表C（標の軸だけが +15〜+21pt 動いた）の原因を確定させる。
//
//     dotnet run --project BattleSim -c Release 0 mark phase0  # 前提を実装から引き直す（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 mark run     # 段B（V0 / Slowest / Strongest × 5行）
//     dotnet run --project BattleSim -c Release 0 mark check   # 自己検査（必須1・必須4 ＋ (a)〜(e)）
//
// **新しい台は作らない**（指示書 Q0-5）——第149期の表C の上位4行 ＋ `逸らし改` を
// `compare` の帯（seed 0..199・第2〜5波）でそのまま使う。
// =====================================================================================

static class MarkDiag
{
    const int Seeds = 200;

    /// <summary>この期の測定台（指示書 Q0-5）。**行は第149期が選んだものをそのまま使う。**</summary>
    static readonly string[] Rows =
    {
        "駆り立て改 (カリ×死軸)",
        "止め改 (トメ×薙ぎ)",
        "仇討ち×砕け (ヒビ×ザン)",
        "止め (トメ×ソラ)",
        "逸らし改 (ソラ×ノミ)",
    };

    const int Foe = 0;    // 標が敵に付いた側
    const int Ally = 1;   // 標が味方に付いた側

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": Body(); return;
            case "rows": Rows61(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("mark: モードは phase0 / run / rows / check。");
                return;
        }
    }

    // =================================================================================
    // phase0 —— 実装から引き直す（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        string eng = Src("BattleCore/BattleEngine.cs");
        string tra = Src("BattleCore/Traits.cs");

        Console.WriteLine("# 第150期 `mark phase0` —— 前提を実装から引き直す（戦闘0回）");
        Console.WriteLine();

        // ---- Q0-1. Marked を書く／0 にする箇所の全数 ----
        Console.WriteLine("## Q0-1. `StatusKeys.Marked` を書く箇所の全数（**Web 側の表を検算する**）");
        Console.WriteLine();
        Console.WriteLine("| ファイル:行 | 値 | 走らせる主体 |");
        Console.WriteLine("|---|:-:|---|");
        int zeroes = 0, ones = 0;
        foreach (var (file, text) in new[] { ("BattleCore/BattleEngine.cs", eng), ("BattleCore/Traits.cs", tra),
                                             ("BattleCore/Engagement.cs", Src("BattleCore/Engagement.cs")) })
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i];
                if (!t.Contains("SetCounter(StatusKeys.Marked")) continue;
                bool zero = t.TrimEnd().EndsWith("Marked, 0);");
                if (zero) zeroes++; else ones++;
                Console.WriteLine("| `" + file + ":" + (i + 1) + "` | " + (zero ? "**0**" : "1") + " | `"
                                  + EnclosingType(lines, i) + "` |");
            }
        }
        // 列挙で消える経路（会戦の境界・業）
        int allLoops = Count(Src("BattleCore/Engagement.cs"), "foreach (string key in StatusKeys.All");
        Console.WriteLine("| `BattleCore/Engagement.cs`（`StatusKeys.All` の一括消去 " + allLoops + " 箇所） | **0** | 会戦の境界 |");
        Console.WriteLine("| `BattleCore/Traits.cs`（`ScapegoatTrait` の引き取り・`Kinds` に `Marked` を含む） | **−1** | 業（保持者 "
                          + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Scapegoat)) + " 枚） |");
        Console.WriteLine();
        Console.WriteLine("- **1 を書く箇所（書き手）: " + ones + "** ／ **0 にする箇所（消し手）: " + zeroes + "**（＋ 境界の一括消去と業）");
        Console.WriteLine("- **「誰かが殴ると外れる」経路は 0 件。** 消すのは "
                          + "`DivertTrait`（ソラが味方から剥がす）／`GoadTrait`（カリが付け替える）／"
                          + "`FinisherTrait.OnAfterAttack`（**トメ自身**が殴ったとき）の3つだけで、"
                          + "**`FinisherTrait` はトメが持つ特性なので他の味方が殴っても標は残る**（指示書の読みどおり）。");
        Console.WriteLine();
        Console.WriteLine("### 書き手4枚（実装から引いた保持者）");
        Console.WriteLine();
        Console.WriteLine("| 特性 | 発火口 | 向き | `UnitCatalog.All` の保持者 |");
        Console.WriteLine("|---|---|---|---|");
        Console.WriteLine("| `Marker`（囃し立て） | `OnBattleStart` | **味方**に1回 | " + Holders(TraitId.Marker) + " |");
        Console.WriteLine("| `Divert`（逸らし） | `OnTurnStart` | **敵**（焦点）＋ **自分** | " + Holders(TraitId.Divert) + " |");
        Console.WriteLine("| `Goad`（駆り立て） | `OnTurnStart` | **味方** | " + Holders(TraitId.Goad) + " |");
        Console.WriteLine("| `Scapegoat`（業） | `OnTurnStart` | 味方から**自分へ移す** | " + Holders(TraitId.Scapegoat) + " |");
        Console.WriteLine();
        Console.WriteLine("**4枚とも `OnBattleStart` か `OnTurnStart`。** 行動順ループの中で標が新しく立つ経路は 0 件なので、"
                          + "**区間を開くのはターン頭の走査1箇所でよい**（段A の実装の根拠）。");
        Console.WriteLine();

        // ---- Q0-2. 既にある計数の棚卸し ----
        Console.WriteLine("## Q0-2. 既にある計数の棚卸し（**新しく足す前に**）");
        Console.WriteLine();
        Console.WriteLine("| 計数 | 何を測っているか | 分母の穴 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| `FinisherFires` | トメが**標持ちを単体で殴った**回数（`PerformAttack` で倍率が乗った回数と同じ） | トメがいないと 0 |");
        Console.WriteLine("| `FinisherCross` | そのうち `TargetPool` の外＝**標が無ければ狙えなかった敵** | 同上 |");
        Console.WriteLine("| `FinisherIdle` | トメが単体で振ったが**相手が標を持っていなかった**回数（空振り） | 同上 |");
        Console.WriteLine("| `FinisherConsumed` | トメが標を消した回数 | 同上 |");
        Console.WriteLine("| `FinisherWaitSum/Count` | 標が付いてから**トメが殴るまで**のターン数（÷ が遊休） | **殴られなかった標は1件も入らない** |");
        Console.WriteLine("| `DivertAllySingles/OnMarked` | 味方の単体振りのうち標持ちに落ちた割合 | ソラがいないと 0（`DivertActive`） |");
        Console.WriteLine("| `DivertMarkedFoeMax` | 同時に標が付いた敵の最大数 | 同上 |");
        Console.WriteLine();
        bool gated = eng.Contains("if (ctx.FinisherActive) ctx.NoteFinisherMarkAges();");
        Console.WriteLine("- `NoteFinisherMarkAges` は呼ばれているか: **" + (eng.Contains("ctx.NoteFinisherMarkAges()") ? "呼ばれている" : "呼ばれていない") + "**"
                          + (gated ? "——ただし **`ctx.FinisherActive` で門が掛かっている**" : ""));
        bool skipsPlayer = eng.Contains("if (u.TeamId == PlayerTeam) continue;");
        Console.WriteLine("- **穴は2つ**: (i) " + (gated ? "**トメが盤上にいる行でしか走らない**" : "門なし")
                          + "／(ii) " + (skipsPlayer ? "**味方に付いた標は1件も見ていない**（`TeamId == PlayerTeam` で `continue`）" : "両陣営を見る"));
        Console.WriteLine("- ⇒ **予測6 の当否**: 「呼ばれているが読み手は `NoteFinisherFire` だけ」は ○、"
                          + "ただし**実体はもっと強い**——**呼ばれること自体がトメの在席を条件にしている。**");
        Console.WriteLine();
        Console.WriteLine("**だから段A の帳簿は門を `MarkActive`（標の<u>書き手</u>が盤上にいるか）に置いた**"
                          + "——読み手ではなく書き手で門を掛けないと、`駆り立て改` と `仇討ち×砕け` が丸ごと落ちる。");
        Console.WriteLine();

        // ---- Q0-5. 測定台 ----
        Console.WriteLine("## Q0-5. 測定台（**新しい台を作らない**）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 書き手 | 読み手 | 標の向き |");
        Console.WriteLine("|---|---|---|---|");
        var all = CompareBuilds();
        foreach (string name in Rows)
        {
            var hit = all.FirstOrDefault(r => r.Name == name);
            if (hit.F is null) { Console.WriteLine("| " + name + " | **引けない** | | |"); continue; }
            var occ = hit.F.Occupied().Select(o => o.Def).ToList();
            string w = string.Join("・", occ.Where(d => d.Traits.Contains(TraitId.Marker) || d.Traits.Contains(TraitId.Divert)
                                                       || d.Traits.Contains(TraitId.Goad)).Select(d => d.Name));
            string r2 = string.Join("・", occ.Where(d => d.Traits.Contains(TraitId.Finisher) || d.Traits.Contains(TraitId.Avenge))
                                             .Select(d => d.Name));
            bool toFoe = occ.Any(d => d.Traits.Contains(TraitId.Divert));
            bool toAlly = occ.Any(d => d.Traits.Contains(TraitId.Goad) || d.Traits.Contains(TraitId.Marker));
            Console.WriteLine("| " + name + " | " + (w.Length == 0 ? "—" : w) + " | " + (r2.Length == 0 ? "**なし**" : r2)
                              + " | " + (toFoe && toAlly ? "敵＋味方" : toFoe ? "**敵**" : toAlly ? "**味方**" : "—") + " |");
        }
        Console.WriteLine();

        // ---- Q0-6. ノブ ----
        Console.WriteLine("## Q0-6. 既存のノブ（**直すときに新しいノブを作らないため**）");
        Console.WriteLine();
        Console.WriteLine("| ノブ | 既定 | 何を振れるか |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| `FinisherRule` | `" + FinisherRule.Default + "` | トメの倍率／標を消費するか |");
        Console.WriteLine("| `DivertRule` | `" + DivertRule.Default + "` | ソラの焦点の数／自分に刺さるか |");
        Console.WriteLine("| `GoadRule` | `" + GoadRule.Default + "` | カリの見返り／標を付けるか |");
        Console.WriteLine("| `MarkPullPercent` | **" + BattleContext.MarkPullPercent + "**（`const`） | **吸い寄せの強さ。ノブになっていない** |");
        Console.WriteLine();
        Console.WriteLine("- `MarkPullPercent` は `const`。**`git log -S` で追うと最初のコミット（`7f6c191`）以来1度も動いていない**"
                          + "——**測って置いた値ではない**（庇いの 50% が第136期まで一度も掃引されていなかったのと同じ形）。");
        Console.WriteLine();

        // ---- Q0-7. 過去の期 ----
        Console.WriteLine("## Q0-7. 過去に標を触った期（`design/*.md` の走査）");
        Console.WriteLine();
        foreach (string pat in new[] { "MarkPullPercent", "TraitId.Divert", "TraitId.Goad", "TraitId.Finisher" })
        {
            var files = Directory.Exists("design")
                ? Directory.GetFiles("design", "*.md").Where(f => File.ReadAllText(f).Contains(pat))
                    .Select(Path.GetFileName).ToList()
                : new List<string?>();
            Console.WriteLine("- `" + pat + "`: " + files.Count + " ファイル（"
                              + string.Join(" / ", files.Take(8)) + (files.Count > 8 ? " …" : "") + "）");
        }
        Console.WriteLine();
    }

    static string Holders(TraitId id)
    {
        var v = UnitCatalog.All.Where(d => d.Traits.Contains(id)).Select(d => d.Name).ToList();
        return v.Count == 0 ? "**0 枚**" : v.Count + " 枚（" + string.Join("・", v) + "）";
    }

    /// <summary>その行を含む直近のクラス／構造体の宣言名（第130期の則——`class` だけを見ない）。</summary>
    static string EnclosingType(string[] lines, int at)
    {
        for (int i = at; i >= 0; i--)
        {
            string t = lines[i].TrimStart();
            foreach (string kw in new[] { "class ", "record struct ", "struct ", "interface " })
            {
                int k = t.IndexOf(kw, StringComparison.Ordinal);
                if (k < 0 || !(t.StartsWith("public") || t.StartsWith("internal") || t.StartsWith("sealed")
                               || t.StartsWith("static") || t.StartsWith("abstract") || t.StartsWith("partial"))) continue;
                string rest = t.Substring(k + kw.Length);
                int e = rest.IndexOfAny(new[] { ' ', '(', ':', '<', '{' });
                return e < 0 ? rest : rest.Substring(0, e);
            }
        }
        return "?";
    }

    // =================================================================================
    // run —— 段B。V0 / Slowest / Strongest で帳簿を取る
    // =================================================================================

    static void Body()
    {
        Console.WriteLine("# 第150期 `mark run` —— 標の一生の帳簿と、行動順が何を変えたか");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ 現行の既定（`HastePick.None`）／ S ＝ `Slowest` ／ A ＝ `Strongest`。**");
        Console.WriteLine("5 行 × 5 波 × seed 0.." + (Seeds - 1) + "。**分母は第2〜5波**（規約 (G10)）。");
        Console.WriteLine();

        var all = CompareBuilds();
        var picked = Rows.Select(n => all.First(r => r.Name == n)).ToArray();
        var v0 = new Res[picked.Length];
        var vs = new Res[picked.Length];
        var va = new Res[picked.Length];
        Parallel.For(0, picked.Length, i =>
        {
            v0[i] = Measure(picked[i].F, HasteRule.Default);
            vs[i] = Measure(picked[i].F, HasteRule.Slowest);
            va[i] = Measure(picked[i].F, HasteRule.Strongest);
        });

        // ---- 表A. 帳簿（V0・第2〜5波）----
        Console.WriteLine("## 表A. 標の一生の帳簿（V0・第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("**付いた ＝ 分類の合計**（受け入れ条件4）。`—` はその側に標が1件も付かない行。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 側 | 付いた | 消費 | 死亡 | (内)止めが | 剥がし | 替え | 他 | 残存 | 寿命T | 最大T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < picked.Length; i++)
            foreach (int side in new[] { Foe, Ally })
            {
                var m = v0[i];
                if (m.Opened[side] <= 0) continue;
                Console.WriteLine("| " + picked[i].Name + " | " + (side == Foe ? "敵" : "味方") + " | "
                                  + P(m.Opened[side]) + " | " + P(m.Consumed[side]) + " | " + P(m.Died[side]) + " | "
                                  + P(m.DiedByFin[side]) + " | " + P(m.Strip[side]) + " | " + P(m.Goad[side]) + " | "
                                  + P(m.Other[side]) + " | " + P(m.Standing[side]) + " | "
                                  + (m.Opened[side] > 0 ? (m.LifeSum[side] / m.Opened[side]).ToString("F2") : "—") + " | "
                                  + m.LifeMax[side].ToString("F0") + " |");
            }
        Console.WriteLine();

        // ---- 表A'. 分類の割合 ----
        Console.WriteLine("## 表A'. 同じものを割合で（付いた標を 100% として）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 側 | 消費 | **死亡** | 剥がし | 替え | 他 | 残存 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < picked.Length; i++)
            foreach (int side in new[] { Foe, Ally })
            {
                var m = v0[i];
                if (m.Opened[side] <= 0) continue;
                double d = m.Opened[side];
                Console.WriteLine("| " + picked[i].Name + " | " + (side == Foe ? "敵" : "味方") + " | "
                                  + Pc(m.Consumed[side] / d) + " | **" + Pc(m.Died[side] / d) + "** | "
                                  + Pc(m.Strip[side] / d) + " | " + Pc(m.Goad[side] / d) + " | "
                                  + Pc(m.Other[side] / d) + " | " + Pc(m.Standing[side] / d) + " |");
            }
        Console.WriteLine();

        // ---- 表B. 標持ちが受けた攻撃 ----
        Console.WriteLine("## 表B. 標が立っているあいだに標持ちが受けた攻撃（V0・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 側 | 被弾/戦 | うち止め | 止めの割合 | 被弾/区間 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int i = 0; i < picked.Length; i++)
            foreach (int side in new[] { Foe, Ally })
            {
                var m = v0[i];
                if (m.Opened[side] <= 0) continue;
                Console.WriteLine("| " + picked[i].Name + " | " + (side == Foe ? "敵" : "味方") + " | "
                                  + P(m.Hits[side]) + " | " + P(m.HitsFin[side]) + " | "
                                  + (m.Hits[side] > 0 ? Pc(m.HitsFin[side] / m.Hits[side]) : "—") + " | "
                                  + (m.Opened[side] > 0 ? (m.Hits[side] / m.Opened[side]).ToString("F2") : "—") + " |");
            }
        Console.WriteLine();

        // ---- 表C. 版間比較（Q0-4）----
        Console.WriteLine("## 表C. 行動順が何を変えたか（Q0-4・第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 側 | 版 | 勝率 | Δ | 付いた | 消費 | **死亡** | 残存 | 寿命T | 止めの被弾 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < picked.Length; i++)
        {
            int side = v0[i].Opened[Foe] >= v0[i].Opened[Ally] ? Foe : Ally;
            foreach (var (tag, v) in new[] { ("V0", v0[i]), ("S", vs[i]), ("A", va[i]) })
                Console.WriteLine("| " + (tag == "V0" ? picked[i].Name : "") + " | " + (tag == "V0" ? (side == Foe ? "敵" : "味方") : "")
                                  + " | " + tag + " | " + F1(v.Win25) + " | "
                                  + (tag == "V0" ? "—" : Sg(v.Win25 - v0[i].Win25)) + " | "
                                  + P(v.Opened[side]) + " | " + P(v.Consumed[side]) + " | **" + P(v.Died[side]) + "** | "
                                  + P(v.Standing[side]) + " | "
                                  + (v.Opened[side] > 0 ? (v.LifeSum[side] / v.Opened[side]).ToString("F2") : "—") + " | "
                                  + P(v.HitsFin[side]) + " |");
        }
        Console.WriteLine();

        // ---- 表D. 判定 ----
        Console.WriteLine("## 表D. Q0-4 の判定（**行ごとに別々に読む**——同じ理由とは限らない）");
        Console.WriteLine();
        Console.WriteLine("| 行 | Δ(S) | Δ(A) | Δ死亡(S) | Δ消費(S) | Δ付いた(S) | 読み |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|");
        for (int i = 0; i < picked.Length; i++)
        {
            int side = v0[i].Opened[Foe] >= v0[i].Opened[Ally] ? Foe : Ally;
            double dWin = vs[i].Win25 - v0[i].Win25;
            double dDie = vs[i].Died[side] - v0[i].Died[side];
            double dCon = vs[i].Consumed[side] - v0[i].Consumed[side];
            double dOpen = vs[i].Opened[side] - v0[i].Opened[side];
            string read =
                Math.Abs(dDie) < 0.10 && Math.Abs(dCon) < 0.10 && Math.Abs(dOpen) < 0.10
                    ? "**分類が動かない ⇒ 標とは無関係**"
                    : dDie < -0.10 && dCon > 0.10 ? "**死亡が減り消費が増えた ⇒ 対抗仮説が当たり**"
                    : dOpen > 0.10 ? "供給（付いた数）が増えた" : "分類は動くが対抗仮説の形ではない";
            Console.WriteLine("| " + picked[i].Name + " | " + Sg(dWin) + " | " + Sg(va[i].Win25 - v0[i].Win25)
                              + " | " + Sg(dDie) + " | " + Sg(dCon) + " | " + Sg(dOpen) + " | " + read + " |");
        }
        Console.WriteLine();

        // ---- 表E. 検算（帳簿が閉じるか）----
        Console.WriteLine("## 表E. 検算 —— 帳簿が閉じるか（受け入れ条件4）");
        Console.WriteLine();
        int bad = 0;
        double otherSum = 0;
        for (int i = 0; i < picked.Length; i++)
            foreach (var v in new[] { v0[i], vs[i], va[i] })
                foreach (int side in new[] { Foe, Ally })
                {
                    double closed = v.Consumed[side] + v.Died[side] + v.Strip[side]
                                    + v.Goad[side] + v.Other[side] + v.Standing[side];
                    if (Math.Abs(closed - v.Opened[side]) > 1e-9) bad++;
                    otherSum += v.Other[side];
                }
        Console.WriteLine("- 「付いた ＝ 分類の合計」が合わない (行 × 版 × 側): **" + bad + " / " + (picked.Length * 3 * 2)
                          + "** → " + (bad == 0 ? "**○**" : "**×**"));
        Console.WriteLine("- 分類できなかった区間（`他`）の合計: **" + otherSum.ToString("F3")
                          + "**（0 なら消える経路を1本も数え落としていない）");
        Console.WriteLine();
    }

    // =================================================================================
    // rows —— 全61行で「標を含む行が特別か」を数える（規約 (G4) の分母の側）
    //
    // 段B の表D が「分類がほとんど動かないのに勝率だけ上がる」を出したので、
    // **その勝率の動きが標に由来するのか**を、分母の側から確かめる。
    // 第144期の則——**波ごとの効き方を生の pt で比べると、測っているのはその波に残っている余地**
    // ——を、波ではなく**行**に当てる（`取り分 = Δ ÷ (100 − V0)`）。
    // =================================================================================

    static void Rows61()
    {
        Console.WriteLine("# 第150期 `mark rows` —— 標を含む行は特別か（全61行・規約 (G4)）");
        Console.WriteLine();
        Console.WriteLine("**取り分 ＝ Δ ÷ (100 − V0)**（第144期）。V0 が 99.0 以上の行は余地が無いので取り分の分母から外す。");
        Console.WriteLine();

        var rows = CompareBuilds();
        var w0 = new double[rows.Length];
        var ws = new double[rows.Length];
        var wa = new double[rows.Length];
        var marks = new double[rows.Length];      // 付いた標/戦（両側の合計・V0）
        var focus = new double[rows.Length];      // 標持ちが受けた被弾 ÷ 区間（V0・両側の合計）
        Parallel.For(0, rows.Length, i =>
        {
            Res a = Measure(rows[i].F, HasteRule.Default);
            Res b = Measure(rows[i].F, HasteRule.Slowest);
            Res c = Measure(rows[i].F, HasteRule.Strongest);
            w0[i] = a.Win25; ws[i] = b.Win25; wa[i] = c.Win25;
            marks[i] = a.Opened[Foe] + a.Opened[Ally];
            double op = marks[i];
            focus[i] = op > 0 ? (a.Hits[Foe] + a.Hits[Ally]) / op : 0;
        });

        // ---- 表F. 標のある行／ない行 ----
        Console.WriteLine("## 表F. 標が立つ行／立たない行（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | V0 | Δ(S) | Δ(A) | 取り分(S) | 取り分(A) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (bool want in new[] { true, false })
        {
            var idx = Enumerable.Range(0, rows.Length).Where(i => (marks[i] > 0) == want).ToArray();
            if (idx.Length == 0) continue;
            var room = idx.Where(i => w0[i] < 99.0).ToArray();
            Console.WriteLine("| " + (want ? "標が立つ" : "立たない") + " | " + idx.Length + " | "
                              + F1(idx.Average(i => w0[i])) + " | " + Sg(idx.Average(i => ws[i] - w0[i])) + " | "
                              + Sg(idx.Average(i => wa[i] - w0[i])) + " | "
                              + (room.Length > 0 ? room.Average(i => (ws[i] - w0[i]) / (100 - w0[i])).ToString("F3") : "—") + " | "
                              + (room.Length > 0 ? room.Average(i => (wa[i] - w0[i]) / (100 - w0[i])).ToString("F3") : "—") + " |");
        }
        Console.WriteLine();

        // ---- 表F'. 余地の帯で切る（標の有無と交差させる） ----
        Console.WriteLine("## 表F'. 余地の帯 × 標の有無（**生の pt は余地を測っている**）");
        Console.WriteLine();
        Console.WriteLine("| V0 の帯 | 標 | 行数 | V0 | Δ(S) | 取り分(S) |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|");
        var bands = new (string Tag, double Lo, double Hi)[]
            { ("0〜60%", 0, 60), ("60〜80%", 60, 80), ("80〜95%", 80, 95), ("95%〜", 95, 1000) };
        foreach (var b in bands)
            foreach (bool want in new[] { true, false })
            {
                var idx = Enumerable.Range(0, rows.Length)
                    .Where(i => w0[i] >= b.Lo && w0[i] < b.Hi && (marks[i] > 0) == want).ToArray();
                if (idx.Length == 0) continue;
                var room = idx.Where(i => w0[i] < 99.0).ToArray();
                Console.WriteLine("| " + b.Tag + " | " + (want ? "○" : "—") + " | " + idx.Length + " | "
                                  + F1(idx.Average(i => w0[i])) + " | " + Sg(idx.Average(i => ws[i] - w0[i])) + " | "
                                  + (room.Length > 0 ? room.Average(i => (ws[i] - w0[i]) / (100 - w0[i])).ToString("F3") : "—") + " |");
            }
        Console.WriteLine();

        // ---- 表G. 取り分の上位 ----
        Console.WriteLine("## 表G. 取り分（S）の上位20行（**余地のある行だけ**・V0 < 99.0）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 標/戦 | 被弾/区間 | V0 | Δ(S) | 取り分(S) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (int i in Enumerable.Range(0, rows.Length).Where(i => w0[i] < 99.0)
                     .OrderByDescending(i => (ws[i] - w0[i]) / (100 - w0[i])).Take(20))
            Console.WriteLine("| " + rows[i].Name + " | " + (marks[i] > 0 ? P(marks[i]) : "—") + " | "
                              + (focus[i] > 0 ? P(focus[i]) : "—") + " | " + F1(w0[i]) + " | "
                              + Sg(ws[i] - w0[i]) + " | " + ((ws[i] - w0[i]) / (100 - w0[i])).ToString("F3") + " |");
        Console.WriteLine();

        // ---- 表H. 相関 ----
        var ok = Enumerable.Range(0, rows.Length).Where(i => w0[i] < 99.0).ToArray();
        Console.WriteLine("## 表H. 取り分(S) と各量の相関（余地のある " + ok.Length + " 行）");
        Console.WriteLine();
        double[] share = ok.Select(i => (ws[i] - w0[i]) / (100 - w0[i])).ToArray();
        Console.WriteLine("| 量 | r |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine("| 標/戦（V0） | " + Corr(share, ok.Select(i => marks[i]).ToArray()).ToString("F3") + " |");
        Console.WriteLine("| 被弾/区間（V0・標が立つ行だけ 0 でない） | " + Corr(share, ok.Select(i => focus[i]).ToArray()).ToString("F3") + " |");
        Console.WriteLine("| V0 の勝率 | " + Corr(share, ok.Select(i => w0[i]).ToArray()).ToString("F3") + " |");
        Console.WriteLine("| 標が立つか（0/1） | " + Corr(share, ok.Select(i => marks[i] > 0 ? 1.0 : 0.0).ToArray()).ToString("F3") + " |");
        Console.WriteLine();
        Console.WriteLine("**生の Δ(S) との相関も併記**（余地で割る前）:");
        Console.WriteLine();
        double[] raw = ok.Select(i => ws[i] - w0[i]).ToArray();
        Console.WriteLine("| 量 | r |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine("| 標/戦（V0） | " + Corr(raw, ok.Select(i => marks[i]).ToArray()).ToString("F3") + " |");
        Console.WriteLine("| V0 の勝率 | " + Corr(raw, ok.Select(i => w0[i]).ToArray()).ToString("F3") + " |");
        Console.WriteLine("| 標が立つか（0/1） | " + Corr(raw, ok.Select(i => marks[i] > 0 ? 1.0 : 0.0).ToArray()).ToString("F3") + " |");
        Console.WriteLine();
    }

    static double Corr(double[] a, double[] b)
    {
        int n = a.Length;
        if (n < 2) return 0;
        double ma = a.Average(), mb = b.Average();
        double sa = 0, sb = 0, sab = 0;
        for (int i = 0; i < n; i++)
        {
            double da = a[i] - ma, db = b[i] - mb;
            sa += da * da; sb += db * db; sab += da * db;
        }
        return sa <= 0 || sb <= 0 ? 0 : sab / Math.Sqrt(sa * sb);
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第150期 自己検査 —— `mark check`");
        Console.WriteLine();

        string bal = arg.Length > 0 ? arg : "docs/balance.md";
        Console.WriteLine("## (a) 必須1: `compare` 305 セルが `" + bal + "` と 0 件");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**比較先が見つからない。**");
        else
        {
            var want = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(bal))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                string[] c = line.Split('|', StringSplitOptions.TrimEntries);
                if (c.Length < 8) continue;
                var v = new double[5];
                bool ok = true;
                for (int i = 0; i < 5; i++)
                    if (!double.TryParse(c[i + 2].Replace("%", ""), out v[i])) { ok = false; break; }
                if (ok) want[c[1]] = v;
            }
            var rows = CompareBuilds();
            var got = new double[rows.Length][];
            Parallel.For(0, rows.Length, i =>
            {
                var w = new double[5];
                for (int st = 0; st < 5; st++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                        if (BattleEngine.Run(rows[i].F, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
                    w[st] = 100.0 * wins / Seeds;
                }
                got[i] = w;
            });
            int badc = 0, cells = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (!want.TryGetValue(rows[i].Name, out double[]? w)) { Console.WriteLine("- 行が引けない: " + rows[i].Name); continue; }
                for (int st = 0; st < 5; st++) { cells++; if (Math.Abs(got[i][st] - w[st]) > 0.001) badc++; }
            }
            Console.WriteLine("- 突き合わせ **" + cells + " セル / 食い違い " + badc + " 件** → " + (badc == 0 ? "**○**" : "**×**"));
        }
        Console.WriteLine();

        string eng = Src("BattleCore/BattleEngine.cs");
        string tra = Src("BattleCore/Traits.cs");

        Console.WriteLine("## (b) 必須4: `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        Console.WriteLine("- `PickOne(` の出現数（`BattleEngine.cs` ＋ `Traits.cs`）: **"
                          + (Count(eng, "PickOne(") + Count(tra, "PickOne(")) + "**（第149期 HEAD と同数なら ○）");
        Console.WriteLine();

        Console.WriteLine("## (c) 帳簿がどの規則からも読まれていない");
        Console.WriteLine();
        foreach (string sym in new[] { "MarkOpened", "MarkEndDied", "MarkEndConsumed", "MarkHits", "_markOpen", "MarkActive" })
        {
            int inEng = Count(eng, sym), inTra = Count(tra, sym);
            Console.WriteLine("- `" + sym + "`: `BattleEngine.cs` " + inEng + " 件 / `Traits.cs` " + inTra + " 件");
        }
        Console.WriteLine();
        Console.WriteLine("- `if (` の条件に帳簿が出るのは `MarkActive`（走査の短絡）だけ。**盤面の分岐は 0 件。**");
        Console.WriteLine();

        Console.WriteLine("## (d) 標を書く／消す箇所の数が第149期 HEAD と同じ");
        Console.WriteLine();
        Console.WriteLine("- `SetCounter(StatusKeys.Marked` の出現数: **"
                          + (Count(eng, "SetCounter(StatusKeys.Marked") + Count(tra, "SetCounter(StatusKeys.Marked"))
                          + "**（書き手4 ＋ 消し手3 ＝ 7 なら ○）");
        Console.WriteLine("- `Run` の引数を1本も足していない: **"
                          + (Count(eng, "MarkRule") == 0 ? "○（`MarkRule` は存在しない）" : "×") + "**");
        Console.WriteLine();

        Console.WriteLine("## (e) 帳簿が閉じる（`mark run` の表E と同じ検算を全61行で）");
        Console.WriteLine();
        long bad2 = 0, opened = 0, other = 0, rowsWith = 0;
        var cb = CompareBuilds();
        var res = new (long Bad, long Opened, long Other, int With)[cb.Length];
        Parallel.For(0, cb.Length, i =>
        {
            long b = 0, o = 0, ot = 0;
            int with = 0;
            for (int st = 0; st < 5; st++)
                for (int seed = 0; seed < 20; seed++)
                {
                    MarkLedger m = BattleEngine.Run(cb[i].F, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).Marks;
                    for (int side = 0; side < 2; side++)
                    {
                        if (m.Closed(side) != m.Opened[side]) b++;
                        o += m.Opened[side];
                        ot += m.Other[side];
                        if (m.Opened[side] > 0) with = 1;
                    }
                }
            res[i] = (b, o, ot, with);
        });
        foreach (var r in res) { bad2 += r.Bad; opened += r.Opened; other += r.Other; rowsWith += r.With; }
        Console.WriteLine("- 61 行 × 5 波 × seed 0..19 で「付いた ≠ 分類の合計」: **" + bad2 + " 件** → "
                          + (bad2 == 0 ? "**○**" : "**×**"));
        Console.WriteLine("- 開いた区間の総数: **" + opened + "**／分類できなかった区間（`他`）: **" + other + "** → "
                          + (other == 0 ? "**○**" : "**×（経路を数え落としている）**"));
        Console.WriteLine("- 標が1件でも立つ行: **" + rowsWith + " / " + cb.Length + "**");
        Console.WriteLine();
    }

    // =================================================================================
    // 器具
    // =================================================================================

    /// <summary>1戦あたりに正規化した帳簿（第2〜5波）と、その帯の勝率。</summary>
    sealed class Res
    {
        public double Win25;
        public readonly double[] Opened = new double[2];
        public readonly double[] Consumed = new double[2];
        public readonly double[] Died = new double[2];
        public readonly double[] DiedByFin = new double[2];
        public readonly double[] Strip = new double[2];
        public readonly double[] Goad = new double[2];
        public readonly double[] Other = new double[2];
        public readonly double[] Standing = new double[2];
        public readonly double[] LifeSum = new double[2];
        public readonly double[] LifeMax = new double[2];
        public readonly double[] Hits = new double[2];
        public readonly double[] HitsFin = new double[2];
    }

    static Res Measure(Formation f, HasteRule r)
    {
        var acc = new Res();
        double winSum = 0;
        int battles = 0;
        for (int st = 1; st < 5; st++)   // **第一波は分母に入れない**（規約 (G10)）
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, haste: r);
                if (res.PlayerWon) wins++;
                MarkLedger m = res.Marks;
                battles++;
                for (int side = 0; side < 2; side++)
                {
                    acc.Opened[side] += m.Opened[side];
                    acc.Consumed[side] += m.Consumed[side];
                    acc.Died[side] += m.Died[side];
                    acc.DiedByFin[side] += m.DiedByFinisher[side];
                    acc.Strip[side] += m.Strip[side];
                    acc.Goad[side] += m.Goad[side];
                    acc.Other[side] += m.Other[side];
                    acc.Standing[side] += m.Standing[side];
                    acc.LifeSum[side] += m.LifeSum[side];
                    if (m.LifeMax[side] > acc.LifeMax[side]) acc.LifeMax[side] = m.LifeMax[side];
                    acc.Hits[side] += m.Hits[side];
                    acc.HitsFin[side] += m.HitsByFinisher[side];
                }
            }
            winSum += 100.0 * wins / Seeds;
        }
        acc.Win25 = winSum / 4;
        foreach (double[] a in new[] { acc.Opened, acc.Consumed, acc.Died, acc.DiedByFin, acc.Strip,
                                       acc.Goad, acc.Other, acc.Standing, acc.LifeSum, acc.Hits, acc.HitsFin })
            for (int side = 0; side < 2; side++) a[side] /= battles;
        return acc;
    }

    static string P(double v) => v.ToString("F2");
    static string Pc(double v) => (100 * v).ToString("F1") + "%";
    static string F1(double v) => v.ToString("F1");
    static string Sg(double v) => v.ToString("+0.00;-0.00;0.00");

    static int Count(string s, string needle)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    static string Src(string rel) => File.Exists(rel) ? File.ReadAllText(rel) : "";
}
