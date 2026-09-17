using BattleCore;
using static Common;

// =====================================================================================
// hold モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "hold")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 hold
// =====================================================================================

static class HoldDiag
{
// 第106期 —— 保留の4枚を決める（ムド・ノノ・ササ・ハリ）。
//
// **触ったのは3つのノブ（`RageRule` / `MenderCostRule` / `LooseRule`）と、
// 4つ目の通貨（強化・弱体）の観測だけ。** 駒・数値・`CompareBuilds()` / `CrossBuilds()` /
// `Stages` / `UnitCatalog.All` は1行も触っていない（受け入れ条件は `compare` 305 セル 0 件）。
//
//     dotnet run --project BattleSim -c Release 0 hold phase0    # §4 と (T1)
//     dotnet run --project BattleSim -c Release 0 hold run <p>   # §3（p = 0..4）
//     dotnet run --project BattleSim -c Release 0 hold hari      # (T3)
//     dotnet run --project BattleSim -c Release 0 hold tables    # 表A〜F
//     dotnet run --project BattleSim -c Release 0 hold check     # 自己検査
public static void Run(string[] args, int stageIndex)
{
    string hdMode = args.Length > 2 ? args[2] : "tables";
    var hdCompare = CompareBuilds();
    var hdCross = CrossBuilds();
    var hdBuilds = hdCompare.Concat(hdCross).ToArray();
    IReadOnlyList<EnemyCatalog.Stage> hdStages = EnemyCatalog.Stages;
    const int HdSeeds = 200;
    var hdName = UnitCatalog.All.ToDictionary(d => d.Id, d => d.Name);
    string HdNm(string id) => hdName.TryGetValue(id, out string? n) ? n : id;

    // 版（§3-1）。**既定は3つとも現行**なので P0 は docs/balance.md と一致する。
    var hdVer = new (string Tag, string Desc, RageRule R, MenderCostRule M, LooseRule L)[]
    {
        ("P0", "現行（対照）",           RageRule.Default,             MenderCostRule.Default, LooseRule.Default),
        ("P1", "ムドのみ Count（Gain = 3・指示書 §2-1）", new RageRule(RageMode.Count, RageRule.SpecGain), MenderCostRule.Default, LooseRule.Default),
        ("P2", "ノノのみ Percent = 50",   RageRule.Default,             new MenderCostRule(50), LooseRule.Default),
        ("P3", "ササのみ Shove = true",   RageRule.Default,             MenderCostRule.Default, new LooseRule(true)),
        ("P4", "3つとも",                 new RageRule(RageMode.Count, RageRule.SpecGain), new MenderCostRule(50), new LooseRule(true)),
        ("P5", "ムドのみ Count・Gain = 5（Phase 0 から引き直した対照）",
                                          new RageRule(RageMode.Count, RageRule.MeasuredGain), MenderCostRule.Default, LooseRule.Default),
    };
    BattleResult HdRun(int p, Formation f, Formation e, int seed, bool verbose = false)
        => BattleEngine.Run(f, e, seed, verbose,
                            rage: hdVer[p].R, menderCost: hdVer[p].M, loose: hdVer[p].L);

    bool HdHas((string Name, Formation F) b, string id)
        => b.F.Occupied().Any(o => o.Item2.Id == id);
    string[] HdRowsWith(string id) => hdBuilds.Where(b => HdHas(b, id)).Select(b => b.Name).ToArray();

    // ---------------------------------------------------------------------------------
    // 駒ごとの帳簿
    // ---------------------------------------------------------------------------------
    const int HdN = 36;
    const int HdBattles = 0, HdSettle = 1, HdTurns = 2, HdUsed = 3,
              HdDmgIn = 4, HdDmgOff = 5, HdHealIn = 6, HdHealOff = 7,
              HdStatIn = 8, HdStatOff = 9, HdBuffIn = 10, HdBuffOff = 11,
              HdLive = 12, HdDeaths = 13, HdPeak = 14, HdPeakTurn = 15,
              HdRageF = 16, HdRageG = 17, HdMendF = 18, HdMendH = 19, HdMendP = 20,
              HdShove = 21, HdCap = 22, HdNoTgt = 23,
              HdAttacks = 24, HdDmgEnemy = 25, HdEncore = 26, HdMove = 27,
              HdReach = 28;   // HdReach..+3 = 到達した戦数 ／ +4..+7 = 到達ターンの和

    var hdRate = new double[hdVer.Length][][];
    var hdAcc = new Dictionary<string, long[]>[hdVer.Length];
    var hdBuffTrait = new long[hdVer.Length][];
    var hdBuffLoss = new long[hdVer.Length][];
    var hdG = new long[hdVer.Length][];    // [ver][0..15] 総計（4通貨 × 4分割）
    var hdNoMark = new long[hdVer.Length][];
    long hdShoveOverCap = 0;               // 自己検査 (d)
    int hdBattlesRun = 0;

    void HdMeasure(int p)
    {
        hdRate[p] = new double[hdStages.Count][];
        for (int w = 0; w < hdStages.Count; w++) hdRate[p][w] = new double[hdBuilds.Length];
        var acc = hdAcc[p] = new Dictionary<string, long[]>();
        var bt = hdBuffTrait[p] = new long[Enum.GetValues(typeof(TraitId)).Length];
        var bl = hdBuffLoss[p] = new long[bt.Length];
        var g = hdG[p] = new long[19];
        var nm = hdNoMark[p] = new long[2];

        long[] Get(string id)
        {
            if (!acc.TryGetValue(id, out long[]? a)) acc[id] = a = new long[HdN];
            return a;
        }

        for (int w = 0; w < hdStages.Count; w++)
        {
            var foeIds = new HashSet<string>(hdStages[w].Enemy.Occupied().Select(o => o.Item2.Id));
            bool band = w > 0;   // 駒ごとの帳簿は第2〜5波だけ（規約 (G10)）
            for (int b = 0; b < hdBuilds.Length; b++)
            {
                var mine = hdBuilds[b].F.Occupied().Select(o => o.Item2.Id).Distinct()
                                      .Where(x => !foeIds.Contains(x)).ToArray();
                int wins = 0;
                for (int seed = 0; seed < HdSeeds; seed++)
                {
                    BattleResult r = HdRun(p, hdBuilds[b].F, hdStages[w].Enemy, seed);
                    hdBattlesRun++;
                    if (r.PlayerWon) wins++;

                    g[0] += r.TurnDmgAll; g[1] += r.TurnDmgIn; g[2] += r.TurnDmgOff; g[3] += r.TurnDmgNone;
                    g[4] += r.TurnHealAll; g[5] += r.TurnHealIn; g[6] += r.TurnHealOff; g[7] += r.TurnHealNone;
                    g[8] += r.TurnStatusAll; g[9] += r.TurnStatusIn; g[10] += r.TurnStatusOff; g[11] += r.TurnStatusNone;
                    g[12] += r.TurnBuffAll; g[13] += r.TurnBuffIn; g[14] += r.TurnBuffOff; g[15] += r.TurnBuffNone;
                    if (r.BuffGainByTrait is not null)
                        for (int i = 0; i < bt.Length; i++) { bt[i] += r.BuffGainByTrait[i]; bl[i] += r.BuffLossByTrait![i]; }
                    nm[0] += r.BuffGainNoMark; nm[1] += r.BuffLossNoMark;
                    g[16] += r.RageFiresFromFoe; g[17] += r.RageFiresFromAlly; g[18] += r.RageFiresNoSource;

                    if (!band) continue;
                    foreach (string id in mine)
                    {
                        long[] a = Get(id);
                        a[HdBattles]++; a[HdSettle] += r.Turns;
                        if (!r.TallyByUnit.TryGetValue(id, out UnitTally? t)) continue;
                        a[HdTurns] += t.TurnsTaken; a[HdUsed] += t.TurnAttacks + t.TurnSkills + t.TurnCharges;
                        a[HdDmgIn] += t.DmgOutInTurn; a[HdDmgOff] += t.DmgOutOffTurn;
                        a[HdHealIn] += t.HealOutInTurn; a[HdHealOff] += t.HealOutOffTurn;
                        a[HdStatIn] += t.StatusOutInTurn; a[HdStatOff] += t.StatusOutOffTurn;
                        a[HdBuffIn] += t.BuffOutInTurn; a[HdBuffOff] += t.BuffOutOffTurn;
                        a[HdLive] += t.LastActiveTurn; a[HdDeaths] += t.Deaths > 0 ? 1 : 0;
                        a[HdPeak] += t.AtkPeak; a[HdPeakTurn] += t.AtkPeakTurn;
                        a[HdRageF] += t.RageCountFires; a[HdRageG] += t.RageGain;
                        a[HdMendF] += t.MendFires; a[HdMendH] += t.MendHealed; a[HdMendP] += t.MendPaidRaw;
                        a[HdShove] += t.LooseShoves; a[HdCap] += t.LooseCapped; a[HdNoTgt] += t.LooseNoTarget;
                        a[HdAttacks] += t.Attacks; a[HdDmgEnemy] += t.DamageToEnemy; a[HdEncore] += t.EncoreFires;
                        a[HdMove] += t.CarryAmount is null ? 0 : t.CarryAmount[UnitTally.CarryMove];
                        if (t.LooseShoves > r.Turns) hdShoveOverCap++;
                        if (t.AtkProbeTurn is not null)
                            for (int i = 0; i < UnitTally.AtkProbes.Length; i++)
                                if (t.AtkProbeTurn[i] > 0) { a[HdReach + i]++; a[HdReach + 4 + i] += t.AtkProbeTurn[i]; }
                    }
                }
                hdRate[p][w][b] = wins * 100.0 / HdSeeds;
            }
        }
    }

    long[] HdA(int p, string id) => hdAcc[p].TryGetValue(id, out long[]? a) ? a : new long[HdN];
    double HdPer(long[] a, int i) => a[HdBattles] == 0 ? 0 : (double)a[i] / a[HdBattles];
    double HdAvg(int p, int b)
    {
        double s = 0;
        for (int w = 1; w < hdStages.Count; w++) s += hdRate[p][w][b];
        return s / (hdStages.Count - 1);
    }

    // 4通貨の分類（線は手番外の割合 50%）。**通貨ごとに割れる駒は「混合」**。
    (string Kind, string Detail) HdClass(long[] a)
    {
        var lab = new[] { "与ダメ", "回復", "状態", "強化弱体" };
        var pair = new[] { (HdDmgIn, HdDmgOff), (HdHealIn, HdHealOff), (HdStatIn, HdStatOff), (HdBuffIn, HdBuffOff) };
        var offs = new double[4];
        for (int i = 0; i < 4; i++)
        {
            double d = a[pair[i].Item1] + a[pair[i].Item2];
            offs[i] = d == 0 ? double.NaN : a[pair[i].Item2] / d;
        }
        var live = Enumerable.Range(0, 4).Where(i => !double.IsNaN(offs[i])).ToArray();
        if (live.Length == 0) return ("出力なし", "4通貨とも 0");
        string detail = string.Join(" / ", live.Select(i => $"{lab[i]} {offs[i] * 100:0.0}%"));
        bool anyOff = live.Any(i => offs[i] > 0.5), anyIn = live.Any(i => offs[i] <= 0.5);
        return (anyOff && anyIn ? "混合" : anyOff ? "手番外型" : "手番型", detail);
    }

    // ---------------------------------------------------------------------------------
    // ササのローカル台（移動軸の読み手と同席させる）。**CompareBuilds() は1行も触らない**
    // ——73行にはササと ヨミ/バサ/ハネ/シオ が同席する行が1つも無いので、
    // Q3 の後半（読み手のいる行が動くか）は原理的にそこでは測れない。
    // 出力役にドルガ（攻38）を必ず入れてある（第26期「台が死んでいる」を避けるため）。
    // ObRows() / SgRows() と同じ扱いで、診断の中だけに存在する。
    // ---------------------------------------------------------------------------------
    (string Name, Formation F)[] HdSasaRows() => new (string, Formation)[]
    {
        ("ササ単騎（対照）", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sasa,
                                           center: UnitCatalog.Sero, back1: UnitCatalog.Dolga, back3: UnitCatalog.Nel)),
        ("ササ×軋み (ヨミ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sasa,
                                           center: UnitCatalog.Yomi, back1: UnitCatalog.Dolga, back3: UnitCatalog.Sero)),
        ("ササ×喧噪 (バサ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sasa,
                                           center: UnitCatalog.Basa, back1: UnitCatalog.Dolga, back3: UnitCatalog.Yomi)),
        ("ササ×突き返し (ハネ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sasa,
                                              center: UnitCatalog.Hane, back1: UnitCatalog.Dolga, back3: UnitCatalog.Utsu)),
        ("ササ×怯み (シオ)", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sasa,
                                           center: UnitCatalog.Shio, back1: UnitCatalog.Dolga, back3: UnitCatalog.Yomi)),
    };

    // ---------------------------------------------------------------------------------
    // Phase 0
    // ---------------------------------------------------------------------------------
    if (hdMode == "phase0")
    {
        Console.WriteLine("# 保留の4枚 —— 実装前の地図（第106期 Phase 0）");
        Console.WriteLine();
        Console.WriteLine("**戦闘は (T1) の計数取得のためだけに回す。** `docs/` には置かない。");
        Console.WriteLine();

        var hdSw0 = System.Diagnostics.Stopwatch.StartNew();
        HdMeasure(0);
        hdSw0.Stop();

        Console.WriteLine("## 0-1. (T1) `AtkBonus` を動かす経路の全数（**観測**。手で書いていない）");
        Console.WriteLine();
        Console.WriteLine($"73行 × 5波 × seed 0..{HdSeeds - 1} = **{hdBattlesRun:#,0} 戦**（{hdSw0.Elapsed.TotalSeconds:0.0} 秒）を、");
        Console.WriteLine("`UnitState.AtkBonus` の setter で第94期 (T2) の印ごとに割った。");
        Console.WriteLine("**窓口（`Whet` / `Dull`）を通らない自己強化もここに出る**——観測点が setter だから。");
        Console.WriteLine();
        Console.WriteLine("| 特性（印） | 上げた量 | 下げた量 | 向き |");
        Console.WriteLine("|---|--:|--:|---|");
        var hdBt = hdBuffTrait[0]; var hdBl = hdBuffLoss[0];
        var hdOrder = Enumerable.Range(0, hdBt.Length)
            .Where(i => hdBt[i] + hdBl[i] > 0)
            .OrderByDescending(i => hdBt[i] + hdBl[i]).ToArray();
        foreach (int i in hdOrder)
        {
            string dir = hdBt[i] > 0 && hdBl[i] > 0 ? "両方" : hdBt[i] > 0 ? "強化" : "弱体";
            Console.WriteLine($"| `{(TraitId)i}` | {hdBt[i]:#,0} | {hdBl[i]:#,0} | {dir} |");
        }
        Console.WriteLine($"| **（印なし）** | {hdNoMark[0][0]:#,0} | {hdNoMark[0][1]:#,0} | — |");
        Console.WriteLine();
        Console.WriteLine($"**経路は {hdOrder.Length} 本**（印が立っているもの）。");
        Console.WriteLine("**境界と蘇生の一括消去は `ResetAtkBonus` を通るので setter に来ない**");
        Console.WriteLine("——第68期の判断をそのまま引き継いだ（帳簿に載せずに戻す）。単発戦ではどのみち走らない。");
        Console.WriteLine("**逆しま（`Perverse`）は `ModifyAttack` で `AtkBonus` を1点も動かさないので、この表に出ない。**");
        Console.WriteLine();

        Console.WriteLine("## 0-2. §2-1 ムド —— 「1発」と数える集合の内訳（**版に依らない**）");
        Console.WriteLine();
        long[] hdMud0 = HdA(0, "mudo");
        var rageHolders = UnitCatalog.All.Where(d => d.Traits.Contains(TraitId.Rage)).ToArray();
        Console.WriteLine($"- 憤怒の保持者は **{rageHolders.Length} 枚**（{string.Join(" / ", rageHolders.Select(d => d.Name))}）"
            + " —— **`RageRule` は両方に効く。**");
        Console.WriteLine($"- ムドの在席行: **{HdRowsWith("mudo").Length} 行** — {string.Join(" / ", HdRowsWith("mudo"))}");
        Console.WriteLine($"- ムドの発火: **{HdPer(hdMud0, HdRageF):0.00} 回/戦**（第96期の被弾 7.21 回/戦 と突き合わせる）");
        Console.WriteLine($"- 1発あたりの上昇（現行）: **{(hdMud0[HdRageF] == 0 ? 0 : (double)hdMud0[HdRageG] / hdMud0[HdRageF]):0.00}**");
        Console.WriteLine($"- 到達点: `AtkBonus` **{HdPer(hdMud0, HdPeak):0.0}**（素の攻 {UnitCatalog.Mudo.Attack} と合わせて "
            + $"**攻 {UnitCatalog.Mudo.Attack + HdPer(hdMud0, HdPeak):0.0}**）／ 到達ターン **{HdPer(hdMud0, HdPeakTurn):0.00}**");
        Console.WriteLine();
        Console.WriteLine("**決めたこと**: `Count` でも**発火する集合を1ビットも変えない**"
            + "（巻き込み・自傷・毒や燃焼の刻みを含めたまま）。理由は2つ:");
        Console.WriteLine();
        Console.WriteLine("1. 「殴られた」の絵に合わせて味方の刃と刻みを外すと、"
            + "**「1発あたりの量」と「1発と数える集合」の2つが同時に動く**"
            + "——P1 と P0 の差がどちらのせいか決まらなくなる。**変数を1つに絞る。**");
        Console.WriteLine("2. **+3 の較正そのものが「全部込みの 7.21 回/戦」から引かれている**"
            + "（第96期の内訳は 敵 ＋ 味方の刃 4 枚 ＋ 出どころ無し）。集合を狭めると較正が崩れる。");
        Console.WriteLine();
        Console.WriteLine("出どころの内訳は表A の下（`hold tables`）と `hold check` (b) に出す。");
        Console.WriteLine();

        Console.WriteLine("## 0-3. §2-2 ノノ —— 現行の式");
        Console.WriteLine();
        Console.WriteLine("```");
        Console.WriteLine("int amount = Math.Min(Amount + PerWound * w, self.Hp - 1);   // 癒す量（**変えない**）");
        Console.WriteLine("ctx.Heal(patient, amount);");
        Console.WriteLine("int paid = amount * ctx.MenderCost.Percent / 100;            // 第106期。**引く量だけ**");
        Console.WriteLine("self.Hp -= paid;");
        Console.WriteLine("```");
        Console.WriteLine();
        Console.WriteLine($"- `MenderTrait.Amount` = {MenderTrait.Amount} ／ `PerWound` = {MenderTrait.PerWound}"
            + $" ／ ノノの最大HP = {UnitCatalog.Nono.MaxHp}");
        var mendHolders = UnitCatalog.All.Where(d => d.Traits.Contains(TraitId.Mender)).ToArray();
        Console.WriteLine($"- 継ぎ当ての保持者（`UnitCatalog.All`）: **{mendHolders.Length} 枚**"
            + $"（{string.Join(" / ", mendHolders.Select(d => d.Name))}）");
        Console.WriteLine("- 敵側の従軍司祭長（`Chaplain`）も同じ特性を持つが、**`Stages` に1体も出ていない**"
            + "（第二波は施しの司祭長に差し替え済み）ので、単発戦では敵側に効かない。");
        Console.WriteLine($"- ノノの在席行: **{HdRowsWith("nono").Length} 行** — {string.Join(" / ", HdRowsWith("nono"))}");
        Console.WriteLine("- **上限（`self.Hp - 1`）は癒す量のほうに残す**ので、1回の癒しの大きさは1点も変わらない。");
        Console.WriteLine();

        Console.WriteLine("## 0-4. §2-3 ササ —— 現行の実装と、弾く先の決め方");
        Console.WriteLine();
        Console.WriteLine("現行の `LooseTrait` は **`OnBattleStart` のログ1行だけ**で、効果の本体は");
        Console.WriteLine("`BattleEngine.ApplyDamage` の中にある（「隣に味方がいない駒の被ダメ −35%」）。");
        Console.WriteLine("**第96期の分類 A（条件が編成時に確定する完全な係数）そのものの形。**");
        Console.WriteLine();
        Console.WriteLine("**弾く先（`Shove = true`）**: 対象は隣接する生存味方のうち**席番号が最小**の1体。");
        Console.WriteLine("行き先は編成枠 0-4 のうち対象自身と保持者の席を除いた3つで、");
        Console.WriteLine("**(1) 保持者に隣接しない席 → (2) 空席 → (3) 席番号が小さいほう** の順に決める。");
        Console.WriteLine("**乱数は1つも引かない**（`ctx.PickOne` を使わない・第89期 (h)）。");
        Console.WriteLine("**召喚枠（5-8）は行き先にしない**（`PlayableSlots` だけを見る。逃亡・後退と同じ作法）。");
        Console.WriteLine();
        Console.WriteLine("**空きが無いときは入れ替える**（「何もしない」枝は作らない）。理由は2つ:");
        Console.WriteLine();
        Console.WriteLine("- **(a)** 保持者が中央にいると全ての枠が隣接するので (1) は必ず空振りする。"
            + "そこで「何もしない」に倒すと**中央に置いた瞬間に機構が消える**——席の判断ではなく席の禁止になる。");
        Console.WriteLine("- **(b)** 空席は味方が倒れたときにしか生まれないので、空席限定にすると"
            + "**現行と同じ「味方の死に頼る」条件に戻る**（第97期に 73行の会戦で一度も成立しなかったのがそれ）。");
        Console.WriteLine();
        Console.WriteLine($"- ササの在席行: **{HdRowsWith("sasa").Length} 行** — {string.Join(" / ", HdRowsWith("sasa"))}");
        int hdSasaReader = hdBuilds.Count(b => HdHas(b, "sasa")
            && (HdHas(b, "yomi") || HdHas(b, "basa") || HdHas(b, "hane") || HdHas(b, "shio")));
        Console.WriteLine($"- **移動軸の読み手（ヨミ・バサ・ハネ・シオ）と同席する行は {hdSasaReader} 行**"
            + " —— **Q3 の後半は 73 行では原理的に測れない。** 表D にローカル台を組む"
            + $"（{HdSasaRows().Length} 行・`CompareBuilds()` は触らない）。");
        Console.WriteLine();

        Console.WriteLine("## 0-5. (T3) ハリを含む行（`Presets.Compare` / `Presets.Cross` から引いた）");
        Console.WriteLine();
        foreach (string n in HdRowsWith("hari")) Console.WriteLine($"- {n}");
        Console.WriteLine();

        Console.WriteLine("## 0-6. `SwapSlots` の既存の呼び出し口（**1つも挙動を変えていない**）");
        Console.WriteLine();
        Console.WriteLine("| 呼び出し口 | 誰が | 何を動かす |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 逃亡（`CowardTrait`・セロ） | 味方 | 自分を後列へ |");
        Console.WriteLine("| 棘守り（`ThornGuardTrait`・カド） | 味方 | 自分と守る相手 |");
        Console.WriteLine("| 喧噪（`TumultTrait`・バサ） | 味方 | 味方2体 |");
        Console.WriteLine("| 曝き（`ExposeTrait`・告発人） | 敵陣 | `HaulOutPair` |");
        Console.WriteLine("| 突き返し（`ShoveTrait`・ハネ） | 敵陣 | `HaulOutPair` |");
        Console.WriteLine("| **散開（`LooseTrait`・ササ）** | **味方** | **第106期に足した唯一の口** |");
        Console.WriteLine();
        Console.WriteLine("`SwapSlots` の中身は1文字も変えていない（移動の通知・`HasFallenBack` の記録・");
        Console.WriteLine("`Move` イベント・`NoteCarry` はあちらのまま）。自己検査 (e) が `CarryMove` で突き合わせる。");
        Console.WriteLine();

        Console.WriteLine("## 0-7. 予測（**測る前に書く**）");
        Console.WriteLine();
        Console.WriteLine("1. **ムドは上がるが小さい。** 到達点が同じで速度だけ変わるので、"
            + "**決着が短い波ほど効く**——第2波でいちばん伸びるはず。");
        Console.WriteLine("2. **ノノは上がる。** ただし**ボルグと同席する行でだけ大きい**"
            + "（巻き込みの後始末で予算を使い切っていたのが緩む）。");
        Console.WriteLine("3. **ササは勝率が動かない可能性が高い。** 被ダメ −35% を残したまま事象を足すだけ。"
            + "**Q3 は勝率で判定しない。**");
        Console.WriteLine("4. **ヨミが最も得をする**（`Displaced` は動かされるたびに攻撃力が上がり、その場で割り込む）。");
        Console.WriteLine("5. **拒否権は立たない。** 3つとも既存の駒の中で閉じており、engine の規則を変えない。");
        Console.WriteLine();
        Console.WriteLine("**予測1 には穴がある**——`RageRule` は**後備えのセッキにも効く**ので、"
            + "「ムドの行」だけを見ると後備えの行の変化を取り落とす。**表B に両方出す。**");
        return;
    }

    // ---------------------------------------------------------------------------------
    // 駒ごとの表（1版）
    // ---------------------------------------------------------------------------------
    void HdTableUnits(int p)
    {
        var ids = hdAcc[p].Keys.OrderByDescending(k => hdAcc[p][k][HdBuffIn] + hdAcc[p][k][HdBuffOff]).ToArray();
        Console.WriteLine("| 駒 | 在席戦 | 手番/戦 | 稼働 | 内ダメ/手番 | 外ダメ/戦 | 回復/戦 | 状態/戦 | **強弱/戦** | 分類 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (string id in ids)
        {
            long[] a = hdAcc[p][id];
            double util = a[HdSettle] == 0 ? 0 : (double)a[HdUsed] / a[HdSettle];
            double perTurn = a[HdTurns] == 0 ? 0 : (double)a[HdDmgIn] / a[HdTurns];
            Console.WriteLine($"| {HdNm(id)} | {a[HdBattles]} | {HdPer(a, HdTurns):0.00} | {util:0.00} | {perTurn:0.0} "
                + $"| {HdPer(a, HdDmgOff):0.0} | {HdPer(a, HdHealIn) + HdPer(a, HdHealOff):0.0} "
                + $"| {HdPer(a, HdStatIn) + HdPer(a, HdStatOff):0.00} "
                + $"| {HdPer(a, HdBuffIn) + HdPer(a, HdBuffOff):0.0} | {HdClass(a).Kind} |");
        }
        Console.WriteLine();
    }

    if (hdMode == "run")
    {
        int hdP = args.Length > 3 && int.TryParse(args[3], out int hdPp) ? hdPp : 0;
        hdP = Math.Clamp(hdP, 0, hdVer.Length - 1);
        var swR = System.Diagnostics.Stopwatch.StartNew();
        HdMeasure(hdP);
        swR.Stop();
        Console.WriteLine($"# 保留の4枚 —— {hdVer[hdP].Tag}（{hdVer[hdP].Desc}）");
        Console.WriteLine();
        Console.WriteLine($"73行 × 5波 × seed 0..{HdSeeds - 1} = **{hdBattlesRun:#,0} 戦**（{swR.Elapsed.TotalSeconds:0.0} 秒）。");
        Console.WriteLine("**駒ごとの帳簿の分母は第2〜5波**（規約 (G10)）。");
        Console.WriteLine();
        HdTableUnits(hdP);
        return;
    }

    // ---------------------------------------------------------------------------------
    // (T3) ハリの現状（表E を単独で出す）
    // ---------------------------------------------------------------------------------
    void HdTableHari()
    {
        long[] a = HdA(0, "hari");
        Console.WriteLine("## 表E. (T3) 縫いのハリ —— 現状を数字にする（**この期では去就を決めない**）");
        Console.WriteLine();
        if (a[HdBattles] == 0) { Console.WriteLine("**73行に在席しない。**"); Console.WriteLine(); return; }
        double util = a[HdSettle] == 0 ? 0 : (double)a[HdUsed] / a[HdSettle];
        Console.WriteLine("| 量 | 実測（第2〜5波） |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| 在席戦 | {a[HdBattles]:#,0} |");
        Console.WriteLine($"| 決着T | {HdPer(a, HdSettle):0.00} |");
        Console.WriteLine($"| 手番/戦（再行動を含む） | **{HdPer(a, HdTurns):0.00}** |");
        Console.WriteLine($"| 使えた手番/戦 | {HdPer(a, HdUsed):0.00} |");
        Console.WriteLine($"| **稼働率**（使えた手番 ÷ 決着T） | **{util * 100:0.0}%** |");
        Console.WriteLine($"| 振/戦（`Attacks`） | {HdPer(a, HdAttacks):0.00} |");
        Console.WriteLine($"| 再行動/戦（第104期） | {HdPer(a, HdEncore):0.00} |");
        Console.WriteLine($"| 生存T | {HdPer(a, HdLive):0.00} |");
        Console.WriteLine();
        Console.WriteLine("**4通貨の出力（1戦あたり）**");
        Console.WriteLine();
        Console.WriteLine("| 通貨 | 手番の中 | 手番の外 | 手番外% |");
        Console.WriteLine("|---|--:|--:|--:|");
        string Pc(long i, long o) => i + o == 0 ? "—" : $"{o * 100.0 / (i + o):0.0}%";
        Console.WriteLine($"| 与ダメージ | {HdPer(a, HdDmgIn):0.0} | {HdPer(a, HdDmgOff):0.0} | {Pc(a[HdDmgIn], a[HdDmgOff])} |");
        Console.WriteLine($"| 回復 | {HdPer(a, HdHealIn):0.0} | {HdPer(a, HdHealOff):0.0} | {Pc(a[HdHealIn], a[HdHealOff])} |");
        Console.WriteLine($"| 状態異常（回数） | {HdPer(a, HdStatIn):0.00} | {HdPer(a, HdStatOff):0.00} | {Pc(a[HdStatIn], a[HdStatOff])} |");
        Console.WriteLine($"| 強化・弱体 | {HdPer(a, HdBuffIn):0.0} | {HdPer(a, HdBuffOff):0.0} | {Pc(a[HdBuffIn], a[HdBuffOff])} |");
        Console.WriteLine();
        Console.WriteLine($"**分類: {HdClass(a).Kind}**（{HdClass(a).Detail}）");
        Console.WriteLine();
        Console.WriteLine("**ハリを含む行の勝率（第2〜5波平均）**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        for (int b = 0; b < hdBuilds.Length; b++)
        {
            if (!HdHas(hdBuilds[b], "hari")) continue;
            Console.Write($"| {hdBuilds[b].Name} ");
            for (int w = 1; w < hdStages.Count; w++) Console.Write($"| {hdRate[0][w][b]:0.0} ");
            Console.WriteLine($"| **{HdAvg(0, b):0.0}** |");
        }
        Console.WriteLine();
        Console.WriteLine("**第85期の律速（振り 2.15 回/戦）と、第104期の再行動が入った後の値を並べてある。**");
        Console.WriteLine("稼働率は第86期の 35〜40% 帯（**特性の発火** ÷ 決着T）とは別の量");
        Console.WriteLine("——ここは**手番** ÷ 決着T なので、そのまま比べてはいけない（第105期の訂正）。");
        Console.WriteLine();
    }

    if (hdMode == "hari")
    {
        var swH = System.Diagnostics.Stopwatch.StartNew();
        HdMeasure(0);
        swH.Stop();
        Console.WriteLine("# 保留の4枚 —— (T3) ハリの現状（第106期 `hold hari`）");
        Console.WriteLine();
        Console.WriteLine($"73行 × 5波 × seed 0..{HdSeeds - 1} = **{hdBattlesRun:#,0} 戦**（{swH.Elapsed.TotalSeconds:0.0} 秒）。");
        Console.WriteLine();
        HdTableHari();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    if (hdMode == "check")
    {
        Console.WriteLine("# 保留の4枚 —— 自己検査（第106期 `hold check`）");
        Console.WriteLine();
        Console.WriteLine("| 検査 | 内容 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine("| 必須1 | `compare` 305 セルが `docs/balance.md` と 0 件 | **別コマンド**（`0 compare` の diff） | — |");
        Console.WriteLine("| 必須2 | `docs/` 10ファイルの差分 | **別コマンド** | — |");
        Console.WriteLine("| 必須3 | 触っていないノブの既定 | **別コマンド**（`derive rules` の diff） | — |");
        Console.WriteLine("| 必須4 | `ctx.PickOne` の箇所数 | **別コマンド**（grep） | — |");

        // (a)/(e) 盤面が動かない台で、P0 と P4 の計数がすべて一致すること。
        // ムド・セッキ（憤怒）・ノノ（繕い）・ササ（散開）を1枚も含まない行では
        // 3つのノブがどれも1回も走らないので、盤面も計数も完全に同一になるはず。
        {
            var quiet = hdBuilds.Where(b => !HdHas(b, "mudo") && !HdHas(b, "sekki")
                                         && !HdHas(b, "nono") && !HdHas(b, "sasa")).ToArray();
            int nRow = Math.Min(10, quiet.Length), nSeed = 20;
            long[] v0 = new long[8], v1 = new long[8];
            int cells = 0, diffCells = 0;
            for (int w = 0; w < hdStages.Count; w++)
                for (int b = 0; b < nRow; b++)
                    for (int seed = 0; seed < nSeed; seed++)
                    {
                        BattleResult r0 = HdRun(0, quiet[b].F, hdStages[w].Enemy, seed);
                        BattleResult r4 = HdRun(4, quiet[b].F, hdStages[w].Enemy, seed);
                        cells++;
                        if (r0.PlayerWon != r4.PlayerWon || r0.Turns != r4.Turns) diffCells++;
                        long[] Acc(BattleResult r)
                        {
                            long[] v = new long[8];
                            foreach (UnitTally t in r.TallyByUnit.Values)
                            {
                                v[0] += t.TurnsTaken; v[1] += t.DmgOutInTurn + t.DmgOutOffTurn;
                                v[2] += t.HealOutInTurn + t.HealOutOffTurn;
                                v[3] += t.StatusOutInTurn + t.StatusOutOffTurn;
                                v[4] += t.BuffOutInTurn + t.BuffOutOffTurn;
                                v[5] += t.CarryAmount is null ? 0 : t.CarryAmount[UnitTally.CarryMove];
                                v[6] += t.RageCountFires; v[7] += t.MendFires;
                            }
                            return v;
                        }
                        long[] x0 = Acc(r0), x4 = Acc(r4);
                        for (int i = 0; i < 8; i++) { v0[i] += x0[i]; v1[i] += x4[i]; }
                    }
            int nd = Enumerable.Range(0, 8).Count(i => v0[i] != v1[i]);
            Console.WriteLine($"| (a) | 3つのノブが1回も走らない {nRow} 行 × 5波 × {nSeed} seed で P0 と P4 の計数が一致 "
                + $"| 食い違った列 **{nd}** / 8（勝敗・決着Tの食い違い {diffCells} / {cells} セル） | {(nd == 0 && diffCells == 0 ? "○" : "×")} |");
            Console.WriteLine($"| (e) | 同じ台で `SwapSlots` 由来の移動回数が一致（既存の呼び出し口が無傷） "
                + $"| {v0[5]:#,0} 対 {v1[5]:#,0} | {(v0[5] == v1[5] ? "○" : "×")} |");
        }

        // (b) Count 版で 1発あたりの上昇がちょうど 3.00
        {
            long fires = 0, gain = 0;
            for (int w = 1; w < hdStages.Count; w++)
                for (int b = 0; b < hdBuilds.Length; b++)
                {
                    if (!HdHas(hdBuilds[b], "mudo") && !HdHas(hdBuilds[b], "sekki")) continue;
                    for (int seed = 0; seed < 20; seed++)
                    {
                        BattleResult r = HdRun(1, hdBuilds[b].F, hdStages[w].Enemy, seed);
                        foreach (UnitTally t in r.TallyByUnit.Values) { fires += t.RageCountFires; gain += t.RageGain; }
                    }
                }
            double ratio = fires == 0 ? 0 : (double)gain / fires;
            Console.WriteLine($"| (b) | `Count` 版で 上昇 ÷ 発火 が {RageRule.SpecGain}.00 "
                + $"| **{ratio:0.0000}**（発火 {fires:#,0}） | {(Math.Abs(ratio - RageRule.SpecGain) < 1e-9 ? "○" : "×")} |");
        }

        // (c) 癒す量が変わっていないこと。**1戦の監査**で、最初の繕いの行を突き合わせる。
        {
            int rowIdx = Array.FindIndex(hdBuilds, b => HdHas(b, "nono"));
            string l0 = "—", l2 = "—";
            if (rowIdx >= 0)
            {
                string First(int p)
                {
                    BattleResult r = HdRun(p, hdBuilds[rowIdx].F, hdStages[1].Enemy, 0, verbose: true);
                    foreach (LogLine ln in r.Log) if (ln.Text.Contains("繕った")) return ln.Text.Trim();
                    return "（発火なし）";
                }
                l0 = First(0); l2 = First(2);
            }
            Console.WriteLine($"| (c) | 繕いの**癒す量**が版で変わらない（1戦の監査・最初の発火） "
                + $"| P0 `{l0}` ／ P2 `{l2}` | {(l0 == l2 ? "○" : "×")} |");
        }

        // (d) 弾きは 1ターン1回以下
        {
            long shoves = 0, turns = 0, over = 0;
            var rows = HdSasaRows().Concat(hdBuilds.Where(b => HdHas(b, "sasa"))).ToArray();
            for (int w = 1; w < hdStages.Count; w++)
                for (int b = 0; b < rows.Length; b++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = HdRun(3, rows[b].F, hdStages[w].Enemy, seed);
                        turns += r.Turns;
                        if (r.TallyByUnit.TryGetValue("sasa", out UnitTally? t))
                        {
                            shoves += t.LooseShoves;
                            if (t.LooseShoves > r.Turns) over++;
                        }
                    }
            Console.WriteLine($"| (d) | 弾きが 1ターン1回以下 | 弾き {shoves:#,0} / 延べターン {turns:#,0}"
                + $"（超過した試行 **{over}**） | {(over == 0 ? "○" : "×")} |");
        }

        Console.WriteLine("| (f) | `ctx.PickOne` を新たに使っていない | **別コマンド**（grep。第105期と同数） | — |");
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表A〜F
    // ---------------------------------------------------------------------------------
    var hdSw = System.Diagnostics.Stopwatch.StartNew();
    for (int p = 0; p < hdVer.Length; p++) HdMeasure(p);
    hdSw.Stop();

    Console.WriteLine("# 保留の4枚を決める（第106期 `hold tables`）");
    Console.WriteLine();
    Console.WriteLine($"73行（`compare` {hdCompare.Length} ＋ 交差帯 {hdCross.Length}） × 5波 × seed 0..{HdSeeds - 1} × {hdVer.Length} 版"
        + $" = **{hdBattlesRun:#,0} 戦**（{hdSw.Elapsed.TotalSeconds:0.0} 秒）。");
    Console.WriteLine("**判定の分母は第2〜5波**（規約 (G10)）。第一波は全行 100% の教習波なので入れない。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 内容 |");
    Console.WriteLine("|---|---|");
    foreach (var v in hdVer) Console.WriteLine($"| **{v.Tag}** | {v.Desc} |");
    Console.WriteLine();

    // ---------------- 表A ----------------
    Console.WriteLine("## 表A. (T1) 4つ目の通貨と、第105期 Q3 の再判定");
    Console.WriteLine();
    long[] gg = hdG[0];
    Console.WriteLine("| 通貨 | 手番の中 | 手番の外 | 誰のものでもない |");
    Console.WriteLine("|---|--:|--:|--:|");
    Console.WriteLine($"| 与ダメージ | {gg[1] * 100.0 / Math.Max(1, gg[0]):0.0}% | {gg[2] * 100.0 / Math.Max(1, gg[0]):0.0}% | {gg[3] * 100.0 / Math.Max(1, gg[0]):0.0}% |");
    Console.WriteLine($"| 回復 | {gg[5] * 100.0 / Math.Max(1, gg[4]):0.0}% | {gg[6] * 100.0 / Math.Max(1, gg[4]):0.0}% | {gg[7] * 100.0 / Math.Max(1, gg[4]):0.0}% |");
    Console.WriteLine($"| 状態異常（回数） | {gg[9] * 100.0 / Math.Max(1, gg[8]):0.0}% | {gg[10] * 100.0 / Math.Max(1, gg[8]):0.0}% | {gg[11] * 100.0 / Math.Max(1, gg[8]):0.0}% |");
    Console.WriteLine($"| **強化・弱体（量）** | **{gg[13] * 100.0 / Math.Max(1, gg[12]):0.0}%** | **{gg[14] * 100.0 / Math.Max(1, gg[12]):0.0}%** | **{gg[15] * 100.0 / Math.Max(1, gg[12]):0.0}%** |");
    Console.WriteLine();
    Console.WriteLine("**憤怒の発火の出どころ（版に依らない・全 `Rage` 保持者ぶん）**: "
        + $"敵 {gg[16] * 100.0 / Math.Max(1, gg[16] + gg[17] + gg[18]):0.0}% ／ "
        + $"味方の刃 {gg[17] * 100.0 / Math.Max(1, gg[16] + gg[17] + gg[18]):0.0}% ／ "
        + $"出どころ無し（毒・燃焼の刻み） {gg[18] * 100.0 / Math.Max(1, gg[16] + gg[17] + gg[18]):0.0}%。");
    Console.WriteLine();
    Console.WriteLine("### Q0 —— 第105期 Q3（第103期の順位を説明するか）を4通貨で引き直す");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 第103期の Δ相乗の順位 | 3通貨（第105期） | **4通貨（この期）** | 期待 | 判定 |");
    Console.WriteLine("|---|--:|---|---|---|:-:|");
    var hdQ3 = new (string Id, string Rank, string Old, string Want)[]
    {
        ("hagi", "1 / 51", "手番外型", "手番外型"), ("kado", "2 / 51", "手番外型", "手番外型"),
        ("hiyo", "3 / 51", "**出力なし**", "手番外型"),
        ("borg", "32 / 51", "手番型", "手番型"), ("dolga", "46 / 51", "手番型", "手番型"),
    };
    int hdQ3ok = 0;
    foreach (var q in hdQ3)
    {
        var kk = HdClass(HdA(0, q.Id));
        bool ok = kk.Kind == q.Want;
        if (ok) hdQ3ok++;
        Console.WriteLine($"| {HdNm(q.Id)} | {q.Rank} | {q.Old} | **{kk.Kind}**（{kk.Detail}） | {q.Want} | {(ok ? "○" : "×")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**{hdQ3ok} / {hdQ3.Length} 一致。**"
        + (hdQ3ok == hdQ3.Length ? " **Q0 は通った**——(G14) を書く条件を満たす。"
                                 : " **Q0 は落ちた**——(G14) は書かない。"));
    Console.WriteLine();
    Console.WriteLine("### 分類の分布（4通貨・第2〜5波）");
    Console.WriteLine();
    var hdKinds = hdAcc[0].Keys.ToDictionary(k => k, k => HdClass(hdAcc[0][k]));
    foreach (string kind in new[] { "手番外型", "混合", "手番型", "出力なし" })
    {
        var mem = hdAcc[0].Keys.Where(k => hdKinds[k].Kind == kind).OrderBy(k => k).ToArray();
        Console.WriteLine($"- **{kind}**（{mem.Length} 枚）: {(mem.Length == 0 ? "—" : string.Join(" / ", mem.Select(HdNm)))}");
    }
    Console.WriteLine();

    // ---------------- 表B ----------------
    Console.WriteLine("## 表B. Q1 —— 泥人形ムド（P0 対 P1）");
    Console.WriteLine();
    Console.WriteLine("**`RageRule` は憤怒の保持者すべてに効く**ので、ムドの行と後備えのセッキの行を両方出す。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 憤怒 | P0 | P1 | **Δ(P1)** | Δ(P5) | 第2波Δ | 第3波Δ | 第4波Δ | 第5波Δ |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    double hdMudSum = 0, hdMud5Sum = 0; int hdMudN = 0;
    for (int b = 0; b < hdBuilds.Length; b++)
    {
        bool hm = HdHas(hdBuilds[b], "mudo"), hs = HdHas(hdBuilds[b], "sekki");
        if (!hm && !hs) continue;
        double d = HdAvg(1, b) - HdAvg(0, b);
        double d5 = HdAvg(5, b) - HdAvg(0, b);
        hdMudSum += d; hdMudN++; hdMud5Sum += d5;
        Console.Write($"| {hdBuilds[b].Name} | {(hm ? "ムド" : "")}{(hm && hs ? "＋" : "")}{(hs ? "セッキ" : "")} "
            + $"| {HdAvg(0, b):0.0} | {HdAvg(1, b):0.0} | **{d:+0.0;-0.0;0.0}** | {d5:+0.0;-0.0;0.0} ");
        for (int w = 1; w < hdStages.Count; w++) Console.Write($"| {hdRate[1][w][b] - hdRate[0][w][b]:+0.0;-0.0;0.0} ");
        Console.WriteLine("|");
    }
    Console.WriteLine();
    Console.WriteLine($"**平均 Δ(P1) = {(hdMudN == 0 ? 0 : hdMudSum / hdMudN):+0.00;-0.00;0.00}pt（{hdMudN} 行）"
        + $" —— Q1 は {(hdMudSum > 0 ? "○" : "×")}**"
        + $"／ 平均 Δ(P5・較正し直した対照) = {(hdMudN == 0 ? 0 : hdMud5Sum / hdMudN):+0.00;-0.00;0.00}pt");
    Console.WriteLine();
    Console.WriteLine("### 到達点と到達の速さ（ムド・第2〜5波）");
    Console.WriteLine();
    Console.WriteLine("| 版 | 発火/戦 | 1発あたり | 到達点 `AtkBonus` | 到達ターン | +6到達% (T) | +12到達% (T) | +18到達% (T) | +24到達% (T) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int p in new[] { 0, 1, 5 })
    {
        long[] a = HdA(p, "mudo");
        Console.Write($"| {hdVer[p].Tag} | {HdPer(a, HdRageF):0.00} | {(a[HdRageF] == 0 ? 0 : (double)a[HdRageG] / a[HdRageF]):0.00} "
            + $"| {HdPer(a, HdPeak):0.0} | {HdPer(a, HdPeakTurn):0.00} ");
        for (int i = 0; i < UnitTally.AtkProbes.Length; i++)
        {
            double pct = a[HdBattles] == 0 ? 0 : a[HdReach + i] * 100.0 / a[HdBattles];
            double tt = a[HdReach + i] == 0 ? 0 : (double)a[HdReach + 4 + i] / a[HdReach + i];
            Console.Write($"| {pct:0.0}% ({tt:0.00}) ");
        }
        Console.WriteLine("|");
    }
    Console.WriteLine();
    Console.WriteLine("**設計どおりなら「到達点はほぼ同じ・到達ターンが早い」**（§2-1）。");
    Console.WriteLine();

    // ---------------- 表C ----------------
    Console.WriteLine("## 表C. Q2 —— 継ぎ当てのノノ（P0 対 P2）");
    Console.WriteLine();
    Console.WriteLine("| 行 | ボルグ同席 | P0 | P2 | Δ |");
    Console.WriteLine("|---|:-:|--:|--:|--:|");
    double hdNonoSum = 0, hdNonoBorg = 0, hdNonoNo = 0; int hdNonoN = 0, hdNonoBn = 0, hdNonoNn = 0;
    for (int b = 0; b < hdBuilds.Length; b++)
    {
        if (!HdHas(hdBuilds[b], "nono")) continue;
        bool borg = HdHas(hdBuilds[b], "borg");
        double d = HdAvg(2, b) - HdAvg(0, b);
        hdNonoSum += d; hdNonoN++;
        if (borg) { hdNonoBorg += d; hdNonoBn++; } else { hdNonoNo += d; hdNonoNn++; }
        Console.WriteLine($"| {hdBuilds[b].Name} | {(borg ? "○" : "")} | {HdAvg(0, b):0.0} | {HdAvg(2, b):0.0} | **{d:+0.0;-0.0;0.0}** |");
    }
    Console.WriteLine();
    Console.WriteLine($"**平均 Δ = {(hdNonoN == 0 ? 0 : hdNonoSum / hdNonoN):+0.00;-0.00;0.00}pt（{hdNonoN} 行）"
        + $" —— Q2 は {(hdNonoSum > 0 ? "○" : "×")}**");
    Console.WriteLine($"（ボルグ同席 {hdNonoBn} 行 {(hdNonoBn == 0 ? 0 : hdNonoBorg / hdNonoBn):+0.00;-0.00;0.00}pt ／ "
        + $"非同席 {hdNonoNn} 行 {(hdNonoNn == 0 ? 0 : hdNonoNo / hdNonoNn):+0.00;-0.00;0.00}pt —— **予測2 の検定**）");
    Console.WriteLine();
    Console.WriteLine("| 版 | 繕い/戦 | 癒した量/戦 | **引いた量/戦** | 生存T | 決着T | 稼働率 | 落% |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
    foreach (int p in new[] { 0, 2 })
    {
        long[] a = HdA(p, "nono");
        double util = a[HdSettle] == 0 ? 0 : (double)a[HdUsed] / a[HdSettle];
        Console.WriteLine($"| {hdVer[p].Tag} | {HdPer(a, HdMendF):0.00} | {HdPer(a, HdMendH):0.0} | **{HdPer(a, HdMendP):0.0}** "
            + $"| **{HdPer(a, HdLive):0.00}** | {HdPer(a, HdSettle):0.00} | {util * 100:0.0}% "
            + $"| {(a[HdBattles] == 0 ? 0 : a[HdDeaths] * 100.0 / a[HdBattles]):0.0}% |");
    }
    Console.WriteLine();

    // ---------------- 表D ----------------
    Console.WriteLine("## 表D. Q3 —— 散開のササ（P0 対 P3）");
    Console.WriteLine();
    long[] hdSasa0 = HdA(0, "sasa"), hdSasa3 = HdA(3, "sasa");
    Console.WriteLine("| 版 | 弾き/戦 | 上限で弾かれ/戦 | 隣に味方なし/戦 | 動かされ/戦（`CarryMove`） | 分類 |");
    Console.WriteLine("|---|--:|--:|--:|--:|---|");
    foreach (int p in new[] { 0, 3 })
    {
        long[] a = HdA(p, "sasa");
        Console.WriteLine($"| {hdVer[p].Tag} | **{HdPer(a, HdShove):0.00}** | {HdPer(a, HdCap):0.00} | {HdPer(a, HdNoTgt):0.00} "
            + $"| {HdPer(a, HdMove):0.00} | {HdClass(a).Kind}（{HdClass(a).Detail}） |");
    }
    Console.WriteLine();
    Console.WriteLine($"**弾いた回数 > 0 —— {(hdSasa3[HdShove] > 0 ? "○" : "×")}**"
        + $"（P0 は {hdSasa0[HdShove]}、P3 は {hdSasa3[HdShove]:#,0} 回）");
    Console.WriteLine();
    Console.WriteLine("### 73行の中のササ（**移動軸の読み手は1行も同席しない**）");
    Console.WriteLine();
    Console.WriteLine("| 行 | P0 | P3 | Δ |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int b = 0; b < hdBuilds.Length; b++)
    {
        if (!HdHas(hdBuilds[b], "sasa")) continue;
        Console.WriteLine($"| {hdBuilds[b].Name} | {HdAvg(0, b):0.0} | {HdAvg(3, b):0.0} | **{HdAvg(3, b) - HdAvg(0, b):+0.0;-0.0;0.0}** |");
    }
    Console.WriteLine();
    Console.WriteLine("### ローカル台（**`CompareBuilds()` は触っていない**）");
    Console.WriteLine();
    Console.WriteLine("移動軸の読み手（ヨミ・バサ・ハネ・シオ）と同席させた5行。出力役にドルガ（攻38）を必ず入れてある。");
    Console.WriteLine();
    Console.WriteLine("| 行 | P0 | P3 | Δ | 弾き/戦 | ヨミの `AtkBonus` 到達点 | 味方の延べ移動/戦 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    var hdLocal = HdSasaRows();
    foreach (var row in hdLocal)
    {
        var res = new double[2];
        double shove = 0, yomiPeak = 0, moves = 0;
        for (int p2 = 0; p2 < 2; p2++)
        {
            int ver = p2 == 0 ? 0 : 3;
            int wins = 0, n = 0;
            for (int w = 1; w < hdStages.Count; w++)
                for (int seed = 0; seed < HdSeeds; seed++)
                {
                    BattleResult r = HdRun(ver, row.F, hdStages[w].Enemy, seed);
                    n++;
                    if (r.PlayerWon) wins++;
                    if (p2 != 1) continue;
                    if (r.TallyByUnit.TryGetValue("sasa", out UnitTally? ts)) shove += ts.LooseShoves;
                    if (r.TallyByUnit.TryGetValue("yomi", out UnitTally? ty)) yomiPeak += ty.AtkPeak;
                    foreach (var kv in r.TallyByUnit)
                        if (row.F.Occupied().Any(o => o.Item2.Id == kv.Key))
                            moves += kv.Value.CarryAmount is null ? 0 : kv.Value.CarryAmount[UnitTally.CarryMove];
                }
            res[p2] = wins * 100.0 / n;
            if (p2 == 1) { shove /= n; yomiPeak /= n; moves /= n; }
        }
        Console.WriteLine($"| {row.Name} | {res[0]:0.0} | {res[1]:0.0} | **{res[1] - res[0]:+0.0;-0.0;0.0}** "
            + $"| {shove:0.00} | {yomiPeak:0.0} | {moves:0.00} |");
    }
    Console.WriteLine();
    Console.WriteLine("**Q3 は勝率で判定しない**（§3-2）。見るのは (1) 弾いた回数 > 0 と "
        + "(2) 移動軸の読み手と同席する行が動くこと。");
    Console.WriteLine();

    // ---------------- 表E ----------------
    HdTableHari();

    // ---------------- 表F ----------------
    Console.WriteLine("## 表F. Q4（切り分け）と拒否権");
    Console.WriteLine();
    Console.WriteLine("### Q4 —— P4 は P1・P2・P3 の和か");
    Console.WriteLine();
    double hdMaxGap = 0; string hdMaxRow = "—"; double hdSumGap = 0; int hdGapN = 0;
    var hdGaps = new List<(string Name, double D4, double Sum, double Gap)>();
    for (int b = 0; b < hdBuilds.Length; b++)
    {
        double d4 = HdAvg(4, b) - HdAvg(0, b);
        double sum = (HdAvg(1, b) - HdAvg(0, b)) + (HdAvg(2, b) - HdAvg(0, b)) + (HdAvg(3, b) - HdAvg(0, b));
        double gap = d4 - sum;
        if (Math.Abs(d4) < 1e-9 && Math.Abs(sum) < 1e-9) continue;
        hdGaps.Add((hdBuilds[b].Name, d4, sum, gap));
        hdSumGap += Math.Abs(gap); hdGapN++;
        if (Math.Abs(gap) > Math.Abs(hdMaxGap)) { hdMaxGap = gap; hdMaxRow = hdBuilds[b].Name; }
    }
    Console.WriteLine($"動いた行は **{hdGapN} / {hdBuilds.Length}**。"
        + $"**|食い違い| の平均 {(hdGapN == 0 ? 0 : hdSumGap / hdGapN):0.00}pt ／ 最大 {hdMaxGap:+0.0;-0.0;0.0}pt（{hdMaxRow}）。**");
    Console.WriteLine();
    Console.WriteLine("| 行 | ΔP4 | ΔP1+ΔP2+ΔP3 | 食い違い |");
    Console.WriteLine("|---|--:|--:|--:|");
    foreach (var g2 in hdGaps.OrderByDescending(x => Math.Abs(x.Gap)).Take(12))
        Console.WriteLine($"| {g2.Name} | {g2.D4:+0.0;-0.0;0.0} | {g2.Sum:+0.0;-0.0;0.0} | **{g2.Gap:+0.0;-0.0;0.0}** |");
    Console.WriteLine();

    Console.WriteLine("### 拒否権1 —— いずれかの波で −10.0pt 以上落ちた行（分母 = `compare` 61行・第91期 (G1)(G2)）");
    Console.WriteLine();
    Console.WriteLine("| 版 | 落ちた行 | 内訳 |");
    Console.WriteLine("|---|--:|---|");
    var hdFell = new Dictionary<int, List<(int Row, int Wave, double D)>>();
    for (int p = 1; p < hdVer.Length; p++)
    {
        var list = new List<(int, int, double)>();
        for (int b = 0; b < hdCompare.Length; b++)
            for (int w = 1; w < hdStages.Count; w++)
                if (hdRate[p][w][b] - hdRate[0][w][b] <= -10.0) list.Add((b, w, hdRate[p][w][b] - hdRate[0][w][b]));
        hdFell[p] = list;
        Console.WriteLine($"| {hdVer[p].Tag} | {list.Select(x => x.Item1).Distinct().Count()} 行 | "
            + (list.Count == 0 ? "—" : string.Join(" ／ ", list.Take(8).Select(x => $"{hdBuilds[x.Item1].Name} 第{x.Item2 + 1}波 {x.Item3:0.0}"))) + " |");
    }
    Console.WriteLine();
    // (G2) 壊れ / 制約 の分解
    foreach (int p in hdFell.Keys.OrderBy(k => k))
    {
        var rows = hdFell[p].Select(x => x.Item1).Distinct().ToArray();
        if (rows.Length == 0) continue;
        Console.WriteLine($"#### {hdVer[p].Tag} の分解（(G2)：駒が使えなくなる＝壊れ／組み合わせ固有＝制約）");
        Console.WriteLine();
        Console.WriteLine("| 落ちた行 | 駒 | その駒を含む**他の**行の全波平均の変化 | 判定 |");
        Console.WriteLine("|---|---|--:|---|");
        foreach (int b in rows)
        {
            bool broke = false;
            foreach (var o in hdBuilds[b].F.Occupied())
            {
                string id = o.Item2.Id;
                var others = Enumerable.Range(0, hdCompare.Length).Where(x => x != b && HdHas(hdBuilds[x], id)).ToArray();
                if (others.Length == 0)
                {
                    Console.WriteLine($"| {hdBuilds[b].Name} | {HdNm(id)} | （他の行が 0 行） | — |");
                    continue;
                }
                double avg = others.Average(x => HdAvg(p, x) - HdAvg(0, x));
                if (avg <= -3.0) broke = true;
                Console.WriteLine($"| {hdBuilds[b].Name} | {HdNm(id)} | {avg:+0.00;-0.00;0.00}pt（{others.Length} 行） | {(avg <= -3.0 ? "**壊れ**" : "制約")} |");
            }
            Console.WriteLine($"| **{hdBuilds[b].Name} の判定** | | | **{(broke ? "壊れ（拒否）" : "組み合わせ固有＝制約（拒否しない）")}** |");
        }
        Console.WriteLine();
    }

    Console.WriteLine("### 拒否権2 —— 主判定19行の第五波平均");
    Console.WriteLine();
    var hdPrim = Baseline.PrimaryRows.Select(n => Array.FindIndex(hdBuilds, b => b.Name == n)).Where(i => i >= 0).ToArray();
    Console.WriteLine($"（`Baseline.PrimaryRows` {Baseline.PrimaryRows.Length} 行のうち {hdPrim.Length} 行が引けた）");
    Console.WriteLine();
    Console.WriteLine("| 版 | 主判定19行の第五波平均 | 歯止め | 判定 |");
    Console.WriteLine("|---|--:|--:|:-:|");
    for (int p = 0; p < hdVer.Length; p++)
    {
        double f5 = hdPrim.Length == 0 ? 0 : hdPrim.Average(i => hdRate[p][hdStages.Count - 1][i]);
        Console.WriteLine($"| {hdVer[p].Tag} | {f5:0.0}% | {Baseline.PrimaryFifthFloor:0.0} | {(f5 >= Baseline.PrimaryFifthFloor ? "○" : "×")} |");
    }
    Console.WriteLine();
    Console.WriteLine("### 注意（規約 (G9)：拒否ではなく記録）—— 第五波が 95% を超えた行");
    Console.WriteLine();
    for (int p = 1; p < hdVer.Length; p++)
    {
        var added = Enumerable.Range(0, hdCompare.Length)
            .Where(b => hdRate[p][hdStages.Count - 1][b] > 95.0 && hdRate[0][hdStages.Count - 1][b] <= 95.0).ToArray();
        Console.WriteLine($"- **{hdVer[p].Tag}**: {added.Length} 行"
            + (added.Length == 0 ? "" : " — " + string.Join(" / ", added.Select(b => hdBuilds[b].Name))));
    }
    Console.WriteLine();
    Console.WriteLine("### 全体の動き（`compare` 61行 × 第2〜5波 = 244 セル）");
    Console.WriteLine();
    Console.WriteLine("| 版 | 動いたセル | 動いた行 | 平均Δ（61行・第2〜5波） |");
    Console.WriteLine("|---|--:|--:|--:|");
    for (int p = 1; p < hdVer.Length; p++)
    {
        int cells = 0; var rows = new HashSet<int>();
        for (int b = 0; b < hdCompare.Length; b++)
            for (int w = 1; w < hdStages.Count; w++)
                if (Math.Abs(hdRate[p][w][b] - hdRate[0][w][b]) > 1e-9) { cells++; rows.Add(b); }
        double avg = Enumerable.Range(0, hdCompare.Length).Average(b => HdAvg(p, b) - HdAvg(0, b));
        Console.WriteLine($"| {hdVer[p].Tag} | {cells} / {hdCompare.Length * (hdStages.Count - 1)} | {rows.Count} | {avg:+0.00;-0.00;0.00} |");
    }
    Console.WriteLine();
    Console.WriteLine($"（自己検査の詳細は `hold check`。弾きが決着ターンを超えた試行 **{hdShoveOverCap}** 件）");
    Console.WriteLine();
    return;
}
}
