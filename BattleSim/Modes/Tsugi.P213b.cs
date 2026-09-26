using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第213期）の本体 —— 版（Z2 / W1 / W2 / Z3）と run7 / check7
//
//     dotnet run --project BattleSim -c Release 0 tsugi run7      # 表A〜D（2台 × 第2〜5波 × 4版）
//     dotnet run --project BattleSim -c Release 0 tsugi check7 [第212期のbalance.md]  # 自己検査
//
// 版はササの札の差し替えだけ（ツギは第212期の Z2・カドは第211期の規定）:
// Z2 ＝ `Brace` ／ W1 ＝ ＋ `BraceCapFirst` ／ W2 ＝ ＋ `BraceCapFirst` ＋ `BraceHeldDeliver` ／ Z3 ＝ ＋ `BraceArmored`。
// **版の定義はこのファイルの中だけで作る**（partial class の静的初期化子の順・第196期）。
// =====================================================================================

static partial class TsugiDiag
{
    static readonly (string Tag, TraitId[] Sasa)[] BVersions =
    {
        ("Z2", new[] { TraitId.Brace }),
        ("W1", new[] { TraitId.Brace, TraitId.BraceCapFirst }),
        ("W2", new[] { TraitId.Brace, TraitId.BraceCapFirst, TraitId.BraceHeldDeliver }),
        ("Z3", new[] { TraitId.Brace, TraitId.BraceArmored }),
    };

    static readonly string[] BTags = { "Z2", "W1", "W2", "Z3" };

    static Formation ApplyB(Formation f, string tag)
    {
        Formation g = ApplyZ(f, "tsugi", "Z2");
        var sasa = Clone(UnitCatalog.Sasa, BVersions.First(v => v.Tag == tag).Sasa);
        var h = new Formation { Shape = g.Shape };
        foreach ((int slot, UnitDef u) in g.Occupied()) h[slot] = u.Id == "sasa" ? sasa : u;
        return h;
    }

    static Agg MeasureB(string key, Formation f, string tag, int st) => Measure(key + "|W" + tag, ApplyB(f, tag), st);

    static partial void RunMore213b(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run7": Run213(); handled = true; return;
            case "check7": Check213(arg); handled = true; return;
        }
    }

    static string FallenList(Agg a) => string.Join("・", a.FallenBy.Where(p => p.Value > 0).OrderByDescending(p => p.Value)
        .Select(p => (UnitCatalog.Everyone.FirstOrDefault(u => u.Id == p.Key)?.Name ?? p.Key) + " " + F1(100.0 * p.Value / Math.Max(1, a.N)) + "%")) is var s && s != "" ? s : "—";

    static void Run213()
    {
        Console.WriteLine("# 第213期 `tsugi run7` —— ササの身構え Z2 / W1 / W2 / Z3（seed 0..199・第2〜5波・**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**Z2** ＝ `Brace`（規定）／ **W1** ＝ ＋ 上限を破片より先に（受け切った一撃では何もしない）／ **W2** ＝ W1 ＋ 受け切った一撃でもそのターンの宛先へ配る ／ **Z3** ＝ 第212期の規定（対照）。"
                          + "ツギは第212期の Z2、カドは第211期の規定。");
        Console.WriteLine();

        // ---- 表A
        Console.WriteLine("## 表A 勝率・全員生存・倒れた駒");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 全員生存 | 倒れた駒（戦の割合） |");
        Console.WriteLine("|---|--:|---|--:|--:|---|");
        foreach (var (name, key, f) in BraceTables())
            foreach (int st in Waves)
                foreach (var tag in BTags)
                {
                    var a = MeasureB(key, f, tag, st);
                    Console.WriteLine("| " + name + " | " + (st + 1) + " | " + tag + " | " + F1(a.Win) + " | " + F1(a.CleanAll) + " | " + FallenList(a) + " |");
                }
        Console.WriteLine();
        Console.WriteLine("全員生存の第2〜5波の平均:");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", BTags) + " |");
        Console.WriteLine("|---|" + string.Concat(BTags.Select(_ => "--:|")));
        foreach (var (name, key, f) in BraceTables())
            Console.WriteLine("| " + name + " | " + string.Join(" | ", BTags.Select(t => F1(Waves.Average(st => MeasureB(key, f, t, st).CleanAll)))) + " |");
        Console.WriteLine();

        // ---- 表B
        Console.WriteLine("## 表B 身構えの帳簿（/戦）");
        Console.WriteLine();
        Console.WriteLine("**弾き** ＝ 錯乱して味方を弾いた回数（（ ）は受け切った一撃の中で弾いた回数）／ **配った** ＝ 弾いた味方の破片にした量（（ ）は受け切った一撃の中で配った量）／ "
                          + "**消えた** ＝ 宛先が無いままターンが変わって消えた保留 ／ **先に切った** ＝ 破片より先に上限で切った回数（量）／ "
                          + "**余計に割れた** ＝ 上限を超える一撃を破片が先に受けて、上限が先なら減らずに済んだ破片 ／ **ササの被ダメ** ＝ ササが HP で受けた量。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 弾き | 配った | 消えた | 先に切った（量） | 余計に割れた | ササの被ダメ |");
        Console.WriteLine("|---|--:|---|---|---|--:|---|--:|--:|");
        foreach (var (name, key, f) in BraceTables())
            foreach (int st in Waves)
                foreach (var tag in BTags)
                {
                    var a = MeasureB(key, f, tag, st); var s = a.Of("sasa");
                    Console.WriteLine("| " + name + " | " + (st + 1) + " | " + tag + " | " + a.Per(s.BraceShoves).ToString("F2") + "（" + a.Per(s.BraceShovesArmorOnly).ToString("F2") + "） | "
                                      + a.Per(s.BraceGiven).ToString("F1") + "（" + a.Per(s.BraceGivenArmorOnly).ToString("F1") + "） | " + a.Per(s.BraceLost).ToString("F1") + " | "
                                      + a.Per(s.BraceArmorEarly).ToString("F2") + "（" + a.Per(s.BraceArmorEarlyAmt).ToString("F1") + "） | " + a.Per(s.BraceArmorExtraBurned).ToString("F1") + " | "
                                      + a.Per(s.DamageTaken).ToString("F1") + " |");
                }
        Console.WriteLine();

        // ---- 表C
        Console.WriteLine("## 表C ササの板");
        Console.WriteLine();
        Console.WriteLine("**貼られた** ＝ 手番の板・応急処置・出撃前の板でササが受けた破片の量/戦（ツギが書いた `ArmorOut` のうちササの分は取れないので、ササが受けた回数（手番の板 `PlankReceived`・応急処置 `FirstAidReceived`）と量の見当＝板の印の間に破片が吸った量 `PlankSoaked`）／ "
                          + "**反射** ＝ ササの板から返った反射が削った HP/戦（`ReflectDealt`）・（ ）は回数 ／ **一撃/割れ** ＝ 板の印の間に敵の一撃を破片で受けた回数 ÷ 板が割れた回数。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 手番の板 | 応急処置 | 板で吸った | 反射（回） | 板で受けた一撃 | 割れた | 一撃/割れ |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|---|--:|--:|--:|");
        foreach (var (name, key, f) in BraceTables())
            foreach (int st in Waves)
                foreach (var tag in BTags)
                {
                    var a = MeasureB(key, f, tag, st); var s = a.Of("sasa");
                    Console.WriteLine("| " + name + " | " + (st + 1) + " | " + tag + " | " + a.Per(s.PlankReceived).ToString("F2") + " | " + a.Per(s.FirstAidReceived).ToString("F2") + " | "
                                      + a.Per(s.PlankSoaked).ToString("F1") + " | " + a.Per(s.ReflectDealt).ToString("F1") + "（" + a.Per(s.ReflectCount).ToString("F2") + "） | "
                                      + a.Per(s.PlankHitsTaken).ToString("F2") + " | " + a.Per(s.PlankBreaks).ToString("F2") + " | "
                                      + (s.PlankBreaks == 0 ? "—" : ((double)s.PlankHitsTaken / s.PlankBreaks).ToString("F2")) + " |");
                }
        Console.WriteLine();

        // ---- 表D
        Console.WriteLine("## 表D 錯乱の蓋（板のあるターン／無いターン）");
        Console.WriteLine();
        Console.WriteLine("ターンの頭（毒と燃焼の刻みの後）にササが板の印を持っていたか。**弾き/戦** はそのターンの弾きの合計 ÷ 戦、**弾き/ターン** はそのターン1つあたり。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 板のあるターン/戦 | 弾き/戦 | 弾き/ターン | 板の無いターン/戦 | 弾き/戦 | 弾き/ターン |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, key, f) in BraceTables())
            foreach (int st in Waves)
                foreach (var tag in BTags)
                {
                    var a = MeasureB(key, f, tag, st); var s = a.Of("sasa");
                    string R(long x, long d) => d == 0 ? "—" : ((double)x / d).ToString("F2");
                    Console.WriteLine("| " + name + " | " + (st + 1) + " | " + tag + " | " + a.Per(s.BraceTurnsPlank).ToString("F2") + " | " + a.Per(s.BraceShovesPlank).ToString("F2") + " | " + R(s.BraceShovesPlank, s.BraceTurnsPlank) + " | "
                                      + a.Per(s.BraceTurnsBare).ToString("F2") + " | " + a.Per(s.BraceShovesBare).ToString("F2") + " | " + R(s.BraceShovesBare, s.BraceTurnsBare) + " |");
                }
        Console.WriteLine();

        // ---- §7 規定の決め方
        Console.WriteLine("## §7 規定の条件");
        Console.WriteLine();
        double dZ = Waves.Average(st => MeasureB("継ぎ当て×分散回復", TsugiRows().First(r => r.Name == "継ぎ当て×分散回復").F, "Z2", st).CleanAll);
        double pZ = MeasureB("pon", PonFormation(), "Z2", 4).CleanAll;
        foreach (var tag in new[] { "W1", "W2" })
        {
            double d = Waves.Average(st => MeasureB("継ぎ当て×分散回復", TsugiRows().First(r => r.Name == "継ぎ当て×分散回復").F, tag, st).CleanAll);
            double p = MeasureB("pon", PonFormation(), tag, 4).CleanAll;
            int worse = 0;
            foreach (var (_, key, f) in BraceTables())
                foreach (int st in Waves)
                    if (MeasureB(key, f, tag, st).Of("sasa").BraceShoves > MeasureB(key, f, "Z2", st).Of("sasa").BraceShoves) worse++;
            bool a = d >= dZ - 2.0, b = p >= pZ - 2.0, c = worse == 0;
            Console.WriteLine("- **" + tag + "**: (a) 分散回復の全員生存の平均 " + F1(d) + "（Z2 " + F1(dZ) + "）" + (a ? "○" : "×")
                              + " ／ (b) ポンの編成 第五波 " + F1(p) + "（Z2 " + F1(pZ) + "）" + (b ? "○" : "×")
                              + " ／ (c) 弾きが Z2 を上回った台 × 波 " + worse + " / 8 " + (c ? "○" : "×") + " → " + (a && b && c ? "**満たす**" : "満たさない"));
        }
        Console.WriteLine();
    }

    // =================================================================================
    // check7 —— §8
    // =================================================================================

    static void Check213(string arg)
    {
        Console.WriteLine("# 第213期 `tsugi check7` —— 自己検査");
        Console.WriteLine();
        bool all = true;
        void Req(bool ok, string what) { all &= ok; Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + what); }

        // (1a) Z2 が第212期の Z2 列と一致: 第212期の Z2（ApplyZ("Z2")）と第213期の Z2（ApplyB("Z2")）は同じ札なので、ここでは台本で比べる。
        //      規定（`UnitCatalog.Sasa`）が Z2 の札と同じことも確かめる。
        Req(UnitCatalog.Sasa.Traits.SequenceEqual(BVersions[0].Sasa), "(1a) 規定のササの札は Z2 と同じ（" + string.Join("・", UnitCatalog.Sasa.Traits) + "）");

        // (1b) 第212期の表と比べる: ササのいない行は一致・ササのいる行（継ぎ当て×分散回復）の勝率も一致（第212期は Z2/Z3 で勝率 0 セル）
        string path = string.IsNullOrWhiteSpace(arg) ? ".tmp/balance212.md" : arg.Trim();
        if (File.Exists(path))
        {
            var old = File.ReadAllLines(path).Where(l => l.StartsWith("| ")).Select(l => l.Split('|').Select(c => c.Trim()).ToArray())
                          .Where(c => c.Length >= 8).GroupBy(c => c[1]).ToDictionary(g => g.Key, g => g.First().Skip(2).Take(5).ToArray());
            int bad = 0, cells = 0;
            foreach (var (name, f) in CompareBuilds())
            {
                if (!old.TryGetValue(name, out var o)) { bad++; continue; }
                for (int st = 0; st < 5; st++) { cells++; if (WinRate(f, st).ToString("F1") + "%" != o[st]) bad++; }
            }
            Req(bad == 0, "(1b) 規定（Z2）の `compare` が第212期の `docs/balance.md` と一致: " + cells + " セル中ずれ " + bad);
        }
        else Console.WriteLine("- (1b) 第212期の表が無い（" + path + "）");

        // (1c) ササに破片が乗らない行（ササのいない行）は W1・W2・Z3 で Z2 と台本が一致
        {
            int diff = 0, n = 0;
            foreach (var (_, f) in CompareBuilds().Concat(CrossBuilds()).Select(r => (r.Name, r.F)))
            {
                if (f.Occupied().Any(o => o.Def.Id == "sasa")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 5; seed++)
                    {
                        var ra = BattleEngine.Run(ApplyB(f, "Z2"), EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                        foreach (var tag in new[] { "W1", "W2" })
                        {
                            n++;
                            var rb = BattleEngine.Run(ApplyB(f, tag), EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                            if (ra.Events.Count != rb.Events.Count || ra.PlayerWon != rb.PlayerWon || ra.Turns != rb.Turns) diff++;
                        }
                    }
            }
            Req(diff == 0, "(1c) ササのいない行（`compare` ＋ 交差帯）は W1・W2 で Z2 と台本の長さ・勝敗・決着T が同じ（" + n + " 戦中ずれ " + diff + "）");
        }

        // (2) verbose の有無で勝敗・決着T が同じ（W1・W2）
        {
            int mism = 0;
            foreach (var tag in new[] { "W1", "W2" })
                foreach (var (_, _, f) in BraceTables())
                    for (int st = 1; st < 5; st++)
                        for (int seed = 0; seed < 50; seed++)
                        {
                            Formation g = ApplyB(f, tag);
                            var a = BattleEngine.Run(g, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                            var b = BattleEngine.Run(g, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                            if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) mism++;
                        }
            Req(mism == 0, "(2) verbose の有無で勝敗・決着T が同じ（W1・W2・2台 × 第2〜5波 × seed 0..49）: ずれ " + mism);
            Console.WriteLine("  - 新しい処理（破片より先の上限・受け切りの配り）は `Roll` も `PickOne` も呼ばない（`BraceTrait.Refuse` / `Deliver` は保留と宛先の私有キーだけを読み書きする）。");
        }

        // (3) 受け切りの扱い（2台 × 第2〜5波 × seed 0..199）
        foreach (var tag in BTags)
        {
            long shovesAO = 0, givenAO = 0, givenAON = 0, early = 0, extra = 0;
            foreach (var (_, key, f) in BraceTables())
                foreach (int st in Waves)
                {
                    var s = MeasureB(key, f, tag, st).Of("sasa");
                    shovesAO += s.BraceShovesArmorOnly; givenAO += s.BraceGivenArmorOnly; givenAON += s.BraceGivenArmorOnlyN; early += s.BraceArmorEarly; extra += s.BraceArmorExtraBurned;
                }
            switch (tag)
            {
                case "Z2":
                    Req(shovesAO == 0 && givenAON == 0 && early == 0, "(3) Z2: 受け切りで弾いた " + shovesAO + " ／ 受け切りで配った " + givenAON + " 回 ／ 破片より先に切った " + early + "（すべて 0）");
                    break;
                case "W1":
                    Req(shovesAO == 0 && givenAON == 0 && early > 0 && extra == 0, "(3) W1: 受け切りで弾いた " + shovesAO + " ／ 受け切りで配った " + givenAON + " 回（0）・破片より先に切った " + early + "（1 以上）・余計に割れた " + extra + "（0）");
                    break;
                case "W2":
                    Req(shovesAO == 0 && givenAON > 0 && early > 0 && extra == 0, "(3) W2: 受け切りで弾いた " + shovesAO + "（0）／ 受け切りで配った " + givenAON + " 回・" + givenAO + "（1 以上）・破片より先に切った " + early + " ・余計に割れた " + extra + "（0）");
                    break;
                case "Z3":
                    Req(shovesAO > 0 && early > 0, "(3) Z3（対照）: 受け切りで弾いた " + shovesAO + " ／ 受け切りで配った " + givenAON + " 回 ／ 破片より先に切った " + early + "（1 以上）");
                    break;
            }
        }

        // (3') W2 の配りは既存の宛先にだけ行く: 受け切りの配りのうち、そのターンにまだ弾いていなかった（＝宛先がそのターンの弾きで作られていない）回数
        {
            long deliv = 0, orphan = 0;
            foreach (var (_, key, f) in BraceTables())
                foreach (int st in Waves)
                {
                    var s = MeasureB(key, f, "W2", st).Of("sasa");
                    deliv += s.BraceGivenArmorOnlyN; orphan += s.BraceGivenOrphan;
                }
            Req(deliv > 0 && orphan == 0, "(3') W2: 受け切りの配り " + deliv + " 回のうち、そのターンに弾いていなかった " + orphan + "（新しい宛先 0・宛先は HP に届いた被弾の弾きだけが作る）");
        }

        Console.WriteLine();
        Console.WriteLine(all ? "**全項目 ○。**" : "**× がある。**");
    }
}
