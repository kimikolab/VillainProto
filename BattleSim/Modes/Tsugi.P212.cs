using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第212期） —— 集中される前衛を守る（腕で応急処置が増える・出撃前の板・ササの身構え）
//
// 指示書は design/PHASE212_TSUGI6_SPEC.md ／ 報告は design/PHASE212_TSUGI6.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 tsugi phase212  # Q0-1〜Q0-3（Z0 ＝ 第211期の規定で数える）
// =====================================================================================

static partial class TsugiDiag
{
    /// <summary>ポンが遊んだ編成（第211期 §3.7）: パターン2 で 中衛・上 ガン ／ 後衛 ツギ ／ 中衛・中央 ドルガ ／ 前衛 ササ ／ 中衛・下 セロ。</summary>
    static Formation PonFormation() => Formation.BuildDiamond(UnitCatalog.Gan, UnitCatalog.Tsugi, UnitCatalog.Dolga, UnitCatalog.Sasa, UnitCatalog.Sero);

    static partial void RunMore212(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "phase212": Phase212(); handled = true; return;
            default: RunMore212b(mode, arg, ref handled); return;
        }
    }

    static partial void RunMore212b(string mode, string arg, ref bool handled);

    static void Phase212()
    {
        Console.WriteLine("# 第212期 `tsugi phase212` —— Q0-1〜Q0-3（第211期の盤面・seed 0..199・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**盤面は第211期のまま**（足したのは計数だけ・`compare` 305 セル 0 件差分）。台はツギの在席 3 行（`compare`）・リリの在席 8 行のリリの席にツギ（差し替え）・ポンの編成（パターン2・ガン入り）。");
        Console.WriteLine();

        var bands = new (string Band, Func<IEnumerable<Func<int, Agg>>> Rows)[]
        {
            ("compare", () => YRows("Y3").Where(r => r.Band == "compare").Select(r => r.At)),
            ("差し替え", () => YRows("Y3").Where(r => r.Band == "差し替え").Select(r => r.At)),
            ("ポンの編成", () => new Func<int, Agg>[] { st => Measure("pon|Y3", ApplyY(PonFormation(), "tsugi", "Y3"), st) }),
        };

        Console.WriteLine("## Q0-1 腕の段と、1ターンの上限で貼れなかった応急処置");
        Console.WriteLine();
        Console.WriteLine("**貼った** ・ **止まった** は応急処置を貼った／1ターン1回の上限で貼れなかった回数（回/戦）を、その時点の腕の段（0 / 1 / 2 / 3以上）ごとに。"
                          + "**上限を 1 ＋ 段にしたときの見当** ＝ 段 1 以上で止まった回数（上限が1つ以上増える場面・同じターンの3回目以降は段 2 以上でしか拾えないので上側の見当）。"
                          + "**戦の終わりの段** は段 0 / 1 / 2 / 3以上に届いた戦の割合。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 波 | 貼った 段0 / 1 / 2 / 3+ | 止まった 段0 / 1 / 2 / 3+ | 見当（段1以上で止まった） | 戦の終わりの段 0 / 1 / 2 / 3+ |");
        Console.WriteLine("|---|--:|---|---|--:|---|");
        foreach (var (band, rows) in bands)
            foreach (int st in Waves.Append(-1))
            {
                var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => rows().Select(r => r(w))).ToList();
                SumAll(ags, out int n, out var ts);
                long[] f = ts.FirstAidFiredByTier ?? new long[4], sp = ts.FirstAidSpentByTier ?? new long[4];
                long[] re = ts.PlankSkillReach ?? new long[4];
                long[] end = { n - re[1], re[1] - re[2], re[2] - re[3], re[3] };
                string P(long x) => ((double)x / Math.Max(1, n)).ToString("F2");
                Console.WriteLine("| " + band + " | " + WaveLab(st) + " | " + string.Join(" / ", f.Select(P)) + " | " + string.Join(" / ", sp.Select(P)) + " | "
                                  + P(sp[1] + sp[2] + sp[3]) + " | " + string.Join(" / ", end.Select(x => Pct(x, n))) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-2 同じ味方に同じターンに2回目を貼る場面");
        Console.WriteLine();
        Console.WriteLine("**止まった/戦** のうち、そのターンに既に応急処置を受けた味方だった割合（上限が増えたら「同じ味方への2回目」になる見当）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 止まった/戦 | うち同じ味方 |");
        Console.WriteLine("|---|--:|--:|");
        foreach (var (band, rows) in bands)
        {
            var ags = Waves.SelectMany(w => rows().Select(r => r(w))).ToList();
            SumAll(ags, out int n, out var ts);
            Console.WriteLine("| " + band + " | " + ((double)ts.FirstAidSpent / Math.Max(1, n)).ToString("F2") + " | " + Pct(ts.FirstAidSpentSame, ts.FirstAidSpent) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## Q0-3 板とササの身構え（ポンの編成・第211期の Y0 ／ Y1 ／ Y3）");
        Console.WriteLine();
        Console.WriteLine("身構えの上限（一撃を " + BraceRule.Default.Cap + " まで）は `ApplyDamageBody` の**破片の段の後ろ**にある（軛の直前）。**破片は切られる前の一撃を丸ごと受ける。**"
                          + "破片が一撃を受け切ると `OnDamaged` が鳴らないので、錯乱（弾き）も、切り落とした分の配りも起きない（第143期のコメントが「穴」として書いていた・`BraceArmorMuted`）。");
        Console.WriteLine();
        Console.WriteLine("**構え** ＝ 身を固めた手番/戦 ／ **切った** ＝ 上限で切った回数/戦・（ ）は切り落とした量/戦 ／ **配った** ＝ 弾いた味方の破片にした量/戦 ／ **弾き** ＝ 錯乱した回数/戦 ／ "
                          + "**板が先** ＝ 身を固めている間に上限を超える一撃を破片が先に受けた回数/戦 ／ **切れたはず** ＝ その一撃に上限が先なら切り落とせた量/戦 ／ "
                          + "**余計に割れた** ＝ 上限が先なら減らずに済んだ破片/戦 ／ **受け切り** ＝ そのうち破片が受け切って弾きも配りも鳴らなかった回数/戦 ／ **ササ倒れ** ＝ 戦の割合。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 全員生存 | ササ倒れ | 構え | 切った（量） | 配った | 弾き | ササへの板 | 板が先 | 切れたはず | 余計に割れた | 受け切り |");
        Console.WriteLine("|--:|---|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int st in Waves)
            foreach (var tag in new[] { "Y0", "Y1", "Y3" })
            {
                var a = Measure("pon|" + tag, ApplyY(PonFormation(), "tsugi", tag), st);
                var s = a.Of("sasa");
                string P(long x) => a.Per(x).ToString("F2");
                Console.WriteLine("| " + (st + 1) + " | " + tag + " | " + F1(a.CleanAll) + " | " + F1(100.0 * a.FallenBy.GetValueOrDefault("sasa") / Math.Max(1, a.N)) + "% | "
                                  + P(s.BraceGuards) + " | " + P(s.BraceCuts) + "（" + a.Per(s.BraceRefused).ToString("F1") + "） | " + a.Per(s.BraceGiven).ToString("F1") + " | " + P(s.BraceShoves) + " | "
                                  + P(s.PlankReceived) + " | " + P(s.BraceArmorHits) + " | " + a.Per(s.BraceArmorHypRefused).ToString("F1") + " | " + a.Per(s.BraceArmorExtraBurned).ToString("F1") + " | "
                                  + P(s.BraceArmorFullMuted) + " |");
            }
        Console.WriteLine();
        Console.WriteLine("`継ぎ当て×分散回復`（`compare`・X 字・ササ前1）も同じ表で:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 全員生存 | 構え | 切った（量） | 配った | 弾き | ササへの板 | 板が先 | 切れたはず | 余計に割れた | 受け切り |");
        Console.WriteLine("|--:|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (int st in Waves)
            foreach (var tag in new[] { "Y0", "Y1", "Y3" })
            {
                var r = YRows(tag).First(x => x.Band == "compare" && x.Name == "継ぎ当て×分散回復");
                var a = r.At(st); var s = a.Of("sasa");
                string P(long x) => a.Per(x).ToString("F2");
                Console.WriteLine("| " + (st + 1) + " | " + tag + " | " + F1(a.CleanAll) + " | " + P(s.BraceGuards) + " | " + P(s.BraceCuts) + "（" + a.Per(s.BraceRefused).ToString("F1") + "） | "
                                  + a.Per(s.BraceGiven).ToString("F1") + " | " + P(s.BraceShoves) + " | " + P(s.PlankReceived) + " | " + P(s.BraceArmorHits) + " | "
                                  + a.Per(s.BraceArmorHypRefused).ToString("F1") + " | " + a.Per(s.BraceArmorExtraBurned).ToString("F1") + " | " + P(s.BraceArmorFullMuted) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-6 ササが破片を持ちうる `compare` 行");
        Console.WriteLine();
        var sasaRows = CompareBuilds().Concat(CrossBuilds()).Where(r => r.F.Occupied().Any(o => o.Def.Id == "sasa")).Select(r => r.Name).ToList();
        Console.WriteLine("ササを含む行（`compare` 61 行 ＋ 交差帯 12 行）: " + (sasaRows.Count == 0 ? "なし" : string.Join(" ／ ", sasaRows)) + "。");
        Console.WriteLine();
    }
}
