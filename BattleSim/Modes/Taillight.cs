using BattleCore;
using static Common;

// =====================================================================================
// taillight モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "taillight")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 taillight
// =====================================================================================

static class TaillightDiag
{
//     dotnet run --project BattleSim -c Release 0 spread
// ==================================================================================
// 第107期 —— 保留を閉じる（席・ムド・ハリ）。指示書は design/PHASE107_HOLD2_SPEC.md。
//
// **既存の診断は1文字も書き換えていない。** (S1)(S2)(S3) をモードで分けてある
// （報告書とコミットも別々にする——`docs/balance.md` の差分がどれの帰結か読めなくなるため）。
// ==================================================================================
// 第108期 —— 尾灯（`TaillightTrait`）の受け入れ確認。**測定ではない。**
//
// 指示書 §4 の「必要なら最小の確認用モードを1つ足す」。**第109期の `tomo`（測定）とは別物**で、
// ここがやるのは自己検査 (a)〜(g) だけ——トモは `Presets.Compare` にも `Presets.Cross` にも
// 入っていない（受け入れ 3）ので、**盤面に出す唯一の場所がこの診断のローカル台になる**。
//
// 台は診断のローカルに組む（`gradient` / `aim` / `route` / `sever` と同じ扱い）。
// **`Presets` も `UnitCatalog.All` も1行も読み替えない。**
//
// **ログの文字列を数えている。** UI は `LogKind` を見るという規約に反して見えるが、
// 確かめたいのは「灯がどの駒に何点載っているか」の推移そのもので、
// **消えたことは盤面の値に痕跡を残さない**（`gullet log` / `yoke log` / `sever` と同じ理由）。
//
//     dotnet run --project BattleSim -c Release 0 taillight
// ==================================================================================
public static void Run(string[] args, int stageIndex)
{
    const int TlSeeds = 40;
    IReadOnlyList<EnemyCatalog.Stage> tlStages = EnemyCatalog.Stages;

    static Formation TlF(params UnitDef[] m)
    {
        var f = new Formation();
        for (int i = 0; i < m.Length; i++) f[i] = m[i];
        return f;
    }

    // 台。**速さの並びを手で決めてある**——「自分を除いて最も遅い味方」の答えが台ごとに一意に決まる。
    var tlBenches = new (string Name, Formation F, string Expect)[]
    {
        // 速さ: ゴルム3 < ドルガ6 < ノミ7 < キリ12。トモ（3）は自分なので対象外。
        ("(b) 一意", TlF(UnitCatalog.Golm, UnitCatalog.Dolga, UnitCatalog.Tomo, UnitCatalog.Nomi, UnitCatalog.Kiri),
            UnitCatalog.Golm.Name),
        // 速さ: バン2 = セッキ2 < ドルガ6 < ノミ7。**同速は席番号の昇順**なので前1のバン。
        ("(b) 同速→席番号", TlF(UnitCatalog.Ban, UnitCatalog.Sekki, UnitCatalog.Tomo, UnitCatalog.Dolga, UnitCatalog.Nomi),
            UnitCatalog.Ban.Name),
        // 速さ: ガルド4 < ザン5 < ノミ7 < キリ12。**ガルドは支援拒否**なので飛ばしてザンへ。
        ("(b) 支援拒否を飛ばす", TlF(UnitCatalog.Gald, UnitCatalog.Zan, UnitCatalog.Tomo, UnitCatalog.Nomi, UnitCatalog.Kiri),
            UnitCatalog.Zan.Name),
        // トモ2枚。**互いに譲り合っても無限に往復しない**ことの確認（1ホップ）。
        ("(f) トモ2枚", TlF(UnitCatalog.Tomo, UnitCatalog.Tomo, UnitCatalog.Golm, UnitCatalog.Dolga, UnitCatalog.Nomi),
            UnitCatalog.Golm.Name),
    };

    Console.WriteLine("# 第108期 —— 尾灯（`TaillightTrait`）の受け入れ確認");
    Console.WriteLine();
    Console.WriteLine("**測定ではない**（第109期に `tomo` を作る）。トモは `Presets.Compare` にも "
        + "`Presets.Cross` にも入っていないので、盤面に出す唯一の場所がこの診断のローカル台。");
    Console.WriteLine();
    Console.WriteLine("台は 5波 × seed 0.." + (TlSeeds - 1) + "。灯 = " + TaillightTrait.Lumen + "。");
    Console.WriteLine();

    // ------------------------------------------------------------------------------
    // ログを再生して「いま誰に何点の灯が載っているか」を追う。
    // 灯る:  「… が ○○ に灯をともした（攻撃 +5 → n）」
    // 消える:「○○ の灯が消えた（攻撃 -m）」
    // 譲る:  「… は前へ出ず、灯した ○○ に道を譲る」
    // ------------------------------------------------------------------------------
    var tlRows = new List<(string Bench, int Wave, int Seed, int Turns,
                           int Lit, int Doused, int Switches, int Yields, int Hop,
                           int MaxLampsAtOnce, int BadTarget, int BadDouse, int BadYieldPerTurn,
                           int Fires, int Idle, int NoDeath, int NoTarget, int NetLeft)>();

    foreach (var (name, form, expect) in tlBenches)
        for (int w = 0; w < tlStages.Count; w++)
            for (int seed = 0; seed < TlSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(form, tlStages[w].Enemy, seed, verbose: true);

                // **判定は「トモ1枚あたり1ターン1回」**。トモが2枚の台では 2 回が正しい
                // （それぞれが自分の手番で1回ずつ譲る。1ホップが止めるのは<b>入れ子</b>のほう）。
                int tomoCount = form.Occupied().Count(o => ReferenceEquals(o.Def, UnitCatalog.Tomo));

                var lamp = new Dictionary<string, int>();   // 駒名 → いま載っている灯
                int maxAtOnce = 0, badTarget = 0, badDouse = 0, badYield = 0;
                int lit = 0, doused = 0, turn = 0, yieldsThisTurn = 0, badYieldTurns = 0;

                foreach (LogLine line in r.Log)
                {
                    string t = line.Text;
                    if (line.Kind == LogKind.Turn)
                    {
                        if (yieldsThisTurn > tomoCount) badYieldTurns++;
                        yieldsThisTurn = 0;
                        turn++;
                        continue;
                    }

                    int a = t.IndexOf(" に灯をともした", StringComparison.Ordinal);
                    if (a >= 0)
                    {
                        int b = t.IndexOf(" が ", StringComparison.Ordinal);
                        string who = t.Substring(b + 3, a - b - 3);
                        // (b) 台ごとに答えは一意。**まだ誰も倒れていないあいだだけ**判定する
                        //     （味方が倒れると「最も遅い生存味方」が変わるのは仕様どおりなので）。
                        if (lamp.Count == 0 && lit == 0 && who != expect) badTarget++;
                        lamp[who] = lamp.TryGetValue(who, out int had) ? had + TaillightTrait.Lumen
                                                                      : TaillightTrait.Lumen;
                        lit += TaillightTrait.Lumen;
                        maxAtOnce = Math.Max(maxAtOnce, lamp.Count(kv => kv.Value > 0));
                        continue;
                    }

                    int c = t.IndexOf(" の灯が消えた（攻撃 -", StringComparison.Ordinal);
                    if (c >= 0)
                    {
                        // **`ctx.Log` は先頭の空白を落として `LogLine.Indent` に分けて持つ**
                        // （BattleEngine.Log）ので、駒名は行頭から始まる。
                        string who = t.Substring(0, c);
                        int amt = int.Parse(new string(t.Substring(c).Where(char.IsAsciiDigit).ToArray()));
                        // (c) 消える量は、その駒に載っていた累計とちょうど同じ
                        if (!lamp.TryGetValue(who, out int had2) || had2 != amt) badDouse++;
                        lamp[who] = 0;
                        doused += amt;
                        continue;
                    }

                    if (t.Contains("は前へ出ず、灯した", StringComparison.Ordinal)) yieldsThisTurn++;
                }
                if (yieldsThisTurn > tomoCount) badYieldTurns++;
                badYield = badYieldTurns;

                int sw = 0, yl = 0, hop = 0, fires = 0, idle = 0, nod = 0, not_ = 0;
                foreach (var kv in r.TallyByUnit)
                {
                    sw += kv.Value.TaillightSwitches; yl += kv.Value.TaillightYields;
                    hop += kv.Value.TaillightBlockedHop; fires += kv.Value.TaillightFires;
                    idle += kv.Value.TaillightIdle; nod += kv.Value.TaillightNoDeath;
                    not_ += kv.Value.TaillightNoTarget;
                }

                tlRows.Add((name, w + 1, seed, r.Turns, lit, doused, sw, yl, hop,
                            maxAtOnce, badTarget, badDouse, badYield, fires, idle, nod, not_,
                            lamp.Values.Sum()));
            }

    Console.WriteLine("## 表A —— 台ごとの集計（1戦あたり）");
    Console.WriteLine();
    Console.WriteLine("| 台 | 戦 | 決着T | 灯/戦 | 消/戦 | 替/戦 | 譲/戦 | 空振り/戦 | 敵未撃破の手番/戦 | 同時に灯る最大 | 1ホップ | 消の照合ずれ | 収支ずれ |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var g in tlRows.GroupBy(x => x.Bench))
    {
        double n = g.Count();
        Console.WriteLine($"| {g.Key} | {n:F0} | {g.Average(x => x.Turns):F1} | {g.Sum(x => x.Lit) / n:F1} "
            + $"| {g.Sum(x => x.Doused) / n:F1} | {g.Sum(x => x.Switches) / n:F2} | {g.Sum(x => x.Yields) / n:F2} "
            + $"| {g.Sum(x => x.Idle) / n:F2} | {g.Sum(x => x.NoDeath) / n:F2} "
            + $"| **{g.Max(x => x.MaxLampsAtOnce)}** | {g.Sum(x => x.Hop)} "
            + $"| {g.Sum(x => x.BadDouse)} | {g.Count(x => x.Lit - x.Doused != x.NetLeft)} |");
    }
    Console.WriteLine();

    // ------------------------------------------------------------------------------
    // 自己検査
    // ------------------------------------------------------------------------------
    var tlChecks = new List<(string Tag, string What, string Got, bool Ok)>();

    // (a) ハリと縫いの実装が残っている
    bool hariAlive = UnitCatalog.Hari.Id == "hari"
                     && UnitCatalog.Hari.Traits.Contains(TraitId.Suture)
                     && TraitCatalog.Get(TraitId.Suture) is SutureTrait;
    string hariWhere = UnitCatalog.All.Any(u => ReferenceEquals(u, UnitCatalog.Hari)) ? "`All` に居る" : "`All` から外れている";
    tlChecks.Add(("(a)", "`UnitCatalog.Hari` と `SutureTrait` / `SutureRule` / `SutureFireRule` が残置されている",
        $"{UnitCatalog.Hari.Name}（{hariWhere}）・既定 {SutureRule.Default} / {SutureFireRule.Default}", hariAlive));

    // (b) 対象は自分を除く最も遅い味方（同速は席番号昇順・支援拒否は飛ばす）
    foreach (var g in tlRows.GroupBy(x => x.Bench).Where(g => g.Key.StartsWith("(b)", StringComparison.Ordinal)))
        tlChecks.Add(("(b)", g.Key + " の初灯が期待どおり",
            "ずれ " + g.Sum(x => x.BadTarget) + " 件 / 灯 " + g.Sum(x => x.Fires) + " 回",
            g.Sum(x => x.BadTarget) == 0));

    // (c)(c') は**トモ1枚の台でだけ判定する**。
    // この照合は駒名をキーにした帳簿なので、**同名のトモが2枚いると2つの灯を分離できない**
    // ——2枚とも同じ「最も遅い味方」を照らすと lamp[その味方] に両方の灯が混ざり、
    // 片方が消したときの量（自分のぶんだけ）と食い違う。**器具の限界であって盤面の不整合ではない**
    // （下の表A で (f) の台にだけ ずれが出ていることがその証拠）。
    var tlSolo = tlRows.Where(x => !x.Bench.StartsWith("(f)", StringComparison.Ordinal)).ToList();

    // (c) 消える量は、その駒に載っていた累計とちょうど同じ
    tlChecks.Add(("(c)", "対象が変わったとき、前の灯が**ちょうど載っていた量だけ**消える（トモ1枚の台）",
        "ずれ " + tlSolo.Sum(x => x.BadDouse) + " 件 / 消灯 " + tlSolo.Count(x => x.Doused > 0) + " 戦"
        + "（トモ2枚の台は同名で分離できないので除外。ずれ " + tlRows.Sum(x => x.BadDouse) + " 件）",
        tlSolo.Sum(x => x.BadDouse) == 0));

    // (c') 収支: 載った総量 − 消した総量 = 戦闘終了時に残っている灯
    int tlBadNet = tlSolo.Count(x => x.Lit - x.Doused != x.NetLeft);
    tlChecks.Add(("(c')", "灯った総量 − 消した総量 ＝ 終了時に残っている灯（収支が閉じる・トモ1枚の台）",
        "ずれ " + tlBadNet + " 戦 / " + tlSolo.Count + " 戦", tlBadNet == 0));

    // (d) 灯は1体にしか灯らない
    // **トモ1枚につき灯は1つ**。2枚の台で 2 体に灯るのは規則どおり（それぞれが1体を照らす）。
    int tlMax1 = tlRows.Where(x => !x.Bench.StartsWith("(f)", StringComparison.Ordinal))
                       .Max(x => x.MaxLampsAtOnce);
    int tlMax2 = tlRows.Where(x => x.Bench.StartsWith("(f)", StringComparison.Ordinal))
                       .Max(x => x.MaxLampsAtOnce);
    tlChecks.Add(("(d)", "同時に灯が載っている駒が**トモの枚数まで**（1枚の台で 1 体・2枚の台で 2 体）",
        "1枚の台 最大 " + tlMax1 + " 体 / 2枚の台 最大 " + tlMax2 + " 体（全 " + tlRows.Count + " 戦）",
        tlMax1 <= 1 && tlMax2 <= 2));

    // (e) 手番の譲渡は1ターン1回以下
    tlChecks.Add(("(e)", "手番の譲渡が**トモ1枚あたり1ターン1回以下**",
        "違反 " + tlRows.Sum(x => x.BadYieldPerTurn) + " ターン / 譲渡 " + tlRows.Sum(x => x.Yields) + " 回",
        tlRows.Sum(x => x.BadYieldPerTurn) == 0));

    // (f) 1ホップ（トモ2枚でも往復しない）
    var tlTwo = tlRows.Where(x => x.Bench.StartsWith("(f)", StringComparison.Ordinal)).ToList();
    tlChecks.Add(("(f)", "トモ2枚の台が**全戦とも完走する**（無限に譲り合わない）",
        tlTwo.Count + " 戦とも決着（平均 " + (tlTwo.Count > 0 ? tlTwo.Average(x => x.Turns) : 0).ToString("F1")
        + "T）・1ホップで止めた回数 " + tlTwo.Sum(x => x.Hop), tlTwo.Count > 0 && tlTwo.All(x => x.Turns > 0)));

    // (g) ctx.PickOne を新たに使っていない（第89期 (h)。候補2個以上で Roll を消費する）
    static int TlCount(string hay, string needle)
    {
        int n = 0;
        for (int i = hay.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = hay.IndexOf(needle, i + 1, StringComparison.Ordinal)) n++;
        return n;
    }
    string? tlRoot = Directory.GetCurrentDirectory();
    while (tlRoot is not null && !File.Exists(Path.Combine(tlRoot, "CLAUDE.md")))
        tlRoot = Directory.GetParent(tlRoot)?.FullName;
    int tlPick = 0, tlPickInTrait = 0;
    if (tlRoot is not null)
    {
        foreach (string f in new[] { Path.Combine(tlRoot, "BattleCore", "Traits.cs"),
                                     Path.Combine(tlRoot, "BattleCore", "BattleEngine.cs") })
            if (File.Exists(f))
                tlPick += TlCount(File.ReadAllText(f), "PickOne(");

        string tf = Path.Combine(tlRoot, "BattleCore", "Traits.cs");
        if (File.Exists(tf))
        {
            string src = File.ReadAllText(tf);
            int i = src.IndexOf("public sealed class TaillightTrait", StringComparison.Ordinal);
            if (i >= 0)
            {
                int j = src.IndexOf("public sealed class", i + 20, StringComparison.Ordinal);
                if (j < 0) j = src.IndexOf("public readonly record struct", i, StringComparison.Ordinal);
                if (j < 0) j = src.Length;
                tlPickInTrait = TlCount(src.Substring(i, j - i), "PickOne(");
            }
        }
    }
    tlChecks.Add(("(g)", "`PickOne(` の素の出現数が **26** のまま（第94期以降不変）で、"
        + "**`TaillightTrait` の中は 0**",
        tlPick + " 箇所（うち `TaillightTrait` の中 " + tlPickInTrait + "）",
        tlPick == 26 && tlPickInTrait == 0));

    Console.WriteLine("## 表B —— 自己検査");
    Console.WriteLine();
    Console.WriteLine("| | 内容 | 実測 | 判定 |");
    Console.WriteLine("|---|---|---|:-:|");
    foreach (var (tag, what, got, ok) in tlChecks)
        Console.WriteLine($"| **{tag}** | {what} | {got} | {(ok ? "○" : "**×**")} |");
    Console.WriteLine();
    Console.WriteLine($"**{tlChecks.Count(x => x.Ok)} / {tlChecks.Count} 件が ○。**");
    Console.WriteLine();

    // ------------------------------------------------------------------------------
    // 1戦の監査（読んで確かめるための生ログ）
    // ------------------------------------------------------------------------------
    // 灯が**移る**戦を選ぶ（対象の死 → 消灯 → 次へ、が1本の中で読める戦）。
    var tlPick2 = tlRows.Where(x => x.Bench == "(b) 一意" && x.Switches > 0 && x.Yields > 0)
                        .OrderBy(x => x.Wave).ThenBy(x => x.Seed).FirstOrDefault();
    Console.WriteLine("## 表C —— 1戦の監査（`(b) 一意`・第" + Math.Max(1, tlPick2.Wave) + "波・seed " + tlPick2.Seed + "）");
    Console.WriteLine();
    Console.WriteLine("**灯が移る戦**（対象が倒れる → 消灯 → 次に遅い味方へ）を選んである。");
    Console.WriteLine();
    Console.WriteLine("```");
    BattleResult tlAudit = BattleEngine.Run(tlBenches[0].F,
        tlStages[Math.Max(0, tlPick2.Wave - 1)].Enemy, tlPick2.Seed, verbose: true);
    foreach (LogLine line in tlAudit.Log)
        if (line.Kind == LogKind.Turn || line.Text.Contains("灯", StringComparison.Ordinal)
            || line.Text.Contains("道を譲る", StringComparison.Ordinal)
            || line.Text.Contains("倒れた", StringComparison.Ordinal))
            Console.WriteLine(line.Text);
    Console.WriteLine("```");
    Console.WriteLine();
    return;
}
}
