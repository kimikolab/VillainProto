using BattleCore;
using static Common;

// =====================================================================================
// lili モード（第205期）の本体 —— 版 V0〜V4 の梯子・第四波／第五波の帳簿・自己検査
//
// 版は札の差し替えだけ（`Run` の引数を増やさない）。隣り合う版の差がその1つの変数の寄与:
//   V0 第204期（最大HPの 20%・溢れは破片）／ V1 ＋痛み ／ V2 ＋溢れを捨てる ／ V3 ＋段 ／ V4 ＋強弱を移す（＝ `UnitCatalog.Lili`）
// =====================================================================================

static partial class LiliDiag
{
    static readonly (string Tag, UnitDef D)[] Ladder =
    {
        ("V0", Clone(UnitCatalog.Lili, new[] { TraitId.Kiss, TraitId.KissSpill })),
        ("V1", Clone(UnitCatalog.Lili, new[] { TraitId.Kiss, TraitId.KissSpill, TraitId.KissPain })),
        ("V2", Clone(UnitCatalog.Lili, new[] { TraitId.Kiss, TraitId.KissSpill, TraitId.KissPain, TraitId.KissVoid })),
        ("V3", Clone(UnitCatalog.Lili, new[] { TraitId.Kiss, TraitId.KissSpill, TraitId.KissPain, TraitId.KissVoid, TraitId.KissTier })),
        ("V4", UnitCatalog.Lili),
    };

    const string DiagRowName = "診断: 分かち×逆しま＋ネル";

    /// <summary>
    /// §4.3 の診断専用の行（`Presets.Compare` には入れない）。土台の、ウツ・リリ・ドハ（行の主題）でない最小の席（前3 のガルド）をネルに。
    /// <b>「ウツに隣接する席」で選ぶ規則は空になった</b>——X 字では前1 に隣接するのは中央（リリ）と後1（ドハ）だけ。ネルはウツの隣にいないので、
    /// 呪いの漏れ（隣の味方 −2）はウツに届かず、<b>ウツが弱体を受け取る経路はリリの強弱の移しだけ</b>になる（測りたい経路が1本に絞れる）。
    /// </summary>
    static Formation DiagRow()
    {
        Formation baseRow = CompareBuilds().First(r => r.Name == "分かち×逆しま (ドハ×ウツ)").F;
        var g = new Formation { Shape = baseRow.Shape };
        foreach ((int slot, UnitDef u) in baseRow.Occupied()) g[slot] = u;
        int pick = baseRow.Occupied().Where(o => o.Def.Id is not ("utsu" or "lili" or "doha")).Min(o => o.Slot);
        g[pick] = UnitCatalog.Nel;
        return g;
    }

    // =================================================================================
    // run2 —— §4.1
    // =================================================================================

    static void Run205()
    {
        Console.WriteLine("# 第205期 `lili run2` —— 在席行 × 版 V0〜V4（**線は置かない**・seed 0..199・平均は第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**V0** 第204期 ／ **V1** ＋痛み ／ **V2** ＋溢れを捨てる ／ **V3** ＋段 ／ **V4** ＋強弱を移す（規定）。"
                          + "差は隣り合う版（**参照先** V1−V0 ／ **破片** V2−V1 ／ **段** V3−V2 ／ **強弱** V4−V3）。`膠着` は 30 ターン上限（第2〜5波・800 戦中）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | V0 | V1 | V2 | V3 | V4 | V0 平均 | V1 | V2 | V3 | **V4** | 参照先 | 破片 | 段 | 強弱 | V4−V0 | 情報セル V0→V4 | 膠着 V0→V4 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var sum = new double[5]; int n = 0, info0 = 0, info4 = 0;
        var wave = new double[5, 5];
        var rows = LiliRows().Select(r => (r.Band, r.Name, r.F)).ToList();
        rows.Add(("診断", DiagRowName, DiagRow()));
        foreach (var (band, name, f) in rows)
        {
            var r = Ladder.Select(v => RatesAndStall(Apply(f, v.D))).ToArray();
            var m = r.Select(x => Mean25(x.W)).ToArray();
            if (band == "compare")
            {
                n++;
                for (int i = 0; i < 5; i++) { sum[i] += m[i]; for (int st = 1; st < 5; st++) wave[i, st] += r[i].W[st]; }
                info0 += InfoCells(r[0].W); info4 += InfoCells(r[4].W);
            }
            Console.WriteLine("| " + band + " | " + name + " | " + string.Join(" | ", r.Select(x => Cells(x.W))) + " | "
                              + string.Join(" | ", m.Select((x, i) => i == 4 ? "**" + x.ToString("F1") + "**" : x.ToString("F1"))) + " | "
                              + D(m[1] - m[0]) + " | " + D(m[2] - m[1]) + " | " + D(m[3] - m[2]) + " | " + D(m[4] - m[3]) + " | " + D(m[4] - m[0]) + " | "
                              + InfoCells(r[0].W) + " → " + InfoCells(r[4].W) + " | " + r[0].Stall.Skip(1).Sum() + " → " + r[4].Stall.Skip(1).Sum() + " |");
        }
        Console.WriteLine();
        if (n > 0)
        {
            Console.WriteLine("`compare` の在席 " + n + " 行の平均: " + string.Join(" ／ ", Ladder.Select((v, i) => v.Tag + " " + (sum[i] / n).ToString("F1"))) + "。情報セル V0 " + info0 + " → V4 " + info4 + "。");
            Console.WriteLine();
            Console.WriteLine("| 波 | V0 | V1 | V2 | V3 | V4 |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|");
            string[] wn = { "", "第2波", "第3波（渇き）", "第4波（軛）", "第5波" };
            for (int st = 1; st < 5; st++)
                Console.WriteLine("| " + wn[st] + " | " + string.Join(" | ", Enumerable.Range(0, 5).Select(i => (wave[i, st] / n).ToString("F1"))) + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ledger2 —— §4.2（第四波と第五波を別々に）・§4.3
    // =================================================================================

    sealed class L2
    {
        public int N, Wins, Died, Stall, RiteBattles, RiteFinishBattles;
        public double DiedTurnSum, TurnsSum, HealSum, DmgSum, KadoDmgSum, RiteFirstSum, UtsuDmgSum;
        public double LiliArmorPeakSum, OtherArmorPeakSum;
        public int LiliArmorPeakMax, OtherArmorPeakMax;
        public int[] TierHist = new int[4];
        public double[] TierTurnSum = new double[3]; public int[] TierTurnN = new int[3];
        public UnitTally T = new();
    }

    static L2 Collect2(Formation f, int stage, int seeds = Seeds)
    {
        var l = new L2();
        var gate = new object();
        Formation enemy = EnemyCatalog.Stages[stage].Enemy;
        Parallel.For(0, seeds, seed =>
        {
            BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
            if (!r.TallyByUnit.TryGetValue("lili", out UnitTally? me)) return;
            long heal = me.KissHealed + me.RiteHealed + me.ReturnHealed;
            int otherPeak = r.TallyByUnit.Where(kv => kv.Key != "lili").Select(kv => kv.Value.ArmorPeakSeen).DefaultIfEmpty(0).Max();
            r.TallyByUnit.TryGetValue("kado", out UnitTally? kado);
            r.TallyByUnit.TryGetValue("utsu", out UnitTally? utsu);
            lock (gate)
            {
                l.N++;
                if (r.PlayerWon) l.Wins++; else if (r.Turns >= BattleEngine.MaxTurns) l.Stall++;
                l.T.Add(me);
                l.HealSum += heal; l.DmgSum += me.DamageToEnemy; l.TurnsSum += r.Turns;
                if (kado is not null) l.KadoDmgSum += kado.DamageToEnemy;
                if (utsu is not null) l.UtsuDmgSum += utsu.DamageToEnemy;
                if (me.Deaths > 0) { l.Died++; l.DiedTurnSum += me.LastActiveTurn; }
                if (me.RiteFirstTurn > 0) { l.RiteBattles++; l.RiteFirstSum += me.RiteFirstTurn; }
                if (me.RiteFinish > 0) l.RiteFinishBattles++;
                l.TierHist[Math.Min(3, me.KissTierMax)]++;
                if (me.KissTierTurn is { } tt)
                    for (int k = 0; k < 3; k++) if (tt[k] > 0) { l.TierTurnSum[k] += tt[k]; l.TierTurnN[k]++; }
                l.LiliArmorPeakSum += me.ArmorPeakSeen; l.LiliArmorPeakMax = Math.Max(l.LiliArmorPeakMax, me.ArmorPeakSeen);
                l.OtherArmorPeakSum += otherPeak; l.OtherArmorPeakMax = Math.Max(l.OtherArmorPeakMax, otherPeak);
            }
        });
        return l;
    }

    static void Ledger205()
    {
        Console.WriteLine("# 第205期 `lili ledger2` —— 第四波と第五波を別々に（1戦あたり・seed 0..199）");
        Console.WriteLine();
        var rows = LiliRows().Select(r => (r.Band, r.Name, r.F)).ToList();
        rows.Add(("診断", DiagRowName, DiagRow()));
        var data = new Dictionary<(string, int, string), L2>();
        foreach (var (band, name, f) in rows)
        foreach (int st in new[] { 3, 4 })
        foreach (var (tag, d) in Ladder)
            data[(name, st, tag)] = Collect2(Apply(f, d), st);

        foreach (int st in new[] { 3, 4 })
        {
            string wn = st == 3 ? "第四波（城塞守備隊・軛）" : "第五波";
            Console.WriteLine("## " + wn);
            Console.WriteLine();
            Console.WriteLine("**痛み/手番** ＝ 手番の頭に測った痛みの平均 ／ **吸った量/回** ＝ 1体ずつ吸った回の実額（`KissDrained ÷ KissFires`）／ **回復/戦** ＝ リリが実際に癒した量（吸い取り・儀式・還る）／"
                              + " **捨てた/戦** ＝ 溢れて捨てた量 ／ **与ダメ:回復** ＝ リリの敵への与ダメ ÷ 回復 ／ **段** ＝ 戦で届いた段の最大の分布（0 / 1 / 2 / 3以上）と、段1・段2 に届いた平均ターン ／"
                              + " **儀式で決着** ＝ 儀式の吸い取りで最後の敵が倒れた戦の割合 ／ **破片** ＝ 観測できた最大値の平均（リリ・ほかの味方）／ **カド** ＝ カドの与ダメ/戦（カドのいる行）。");
            Console.WriteLine();
            Console.WriteLine("| 行 | 版 | 勝率 | 痛み/手番 | 吸った量/回 | 吸った回/戦 | 回復/戦 | 捨てた/戦 | 与ダメ/戦 | 与ダメ:回復 | 倒れた戦 | 倒れたT | 段 0/1/2/3+ | 段1 T | 段2 T | 儀式/戦 | 初回T | 儀式で決着 | 破片 リリ/ほか | カド | 膠着 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (var (band, name, f) in rows)
            {
                bool hasKado = f.Occupied().Any(o => o.Def.Id == "kado");
                foreach (var (tag, _) in Ladder)
                {
                    L2 l = data[(name, st, tag)]; UnitTally t = l.T;
                    string P(double x) => (x / Math.Max(1, l.N)).ToString("F1");
                    string pc(int x) => (100.0 * x / Math.Max(1, l.N)).ToString("F0");
                    Console.WriteLine("| " + (tag == "V0" ? name : "") + " | " + tag + " | " + (100.0 * l.Wins / Math.Max(1, l.N)).ToString("F1") + " | "
                                      + (t.KissActs == 0 ? "—" : ((double)t.KissPainSum / t.KissActs).ToString("F1")) + " | "
                                      + (t.KissFires == 0 ? "—" : ((double)t.KissDrained / t.KissFires).ToString("F1")) + " | "
                                      + (t.KissFires / Math.Max(1.0, l.N)).ToString("F2") + " | " + P(l.HealSum) + " | " + P(t.KissVoided) + " | " + P(l.DmgSum) + " | "
                                      + (l.HealSum == 0 ? "—" : (l.DmgSum / l.HealSum).ToString("F2")) + " | "
                                      + pc(l.Died) + "% | " + (l.Died == 0 ? "—" : (l.DiedTurnSum / l.Died).ToString("F1")) + " | "
                                      + string.Join("/", l.TierHist.Select(pc)) + " | "
                                      + (l.TierTurnN[0] == 0 ? "—" : (l.TierTurnSum[0] / l.TierTurnN[0]).ToString("F1")) + " | "
                                      + (l.TierTurnN[1] == 0 ? "—" : (l.TierTurnSum[1] / l.TierTurnN[1]).ToString("F1")) + " | "
                                      + (t.RiteFires / Math.Max(1.0, l.N)).ToString("F2") + " | " + (l.RiteBattles == 0 ? "—" : (l.RiteFirstSum / l.RiteBattles).ToString("F1")) + " | "
                                      + pc(l.RiteFinishBattles) + "% | " + P(l.LiliArmorPeakSum) + " / " + P(l.OtherArmorPeakSum) + " | "
                                      + (hasKado ? P(l.KadoDmgSum) : "—") + " | " + l.Stall + " |");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine("## 移した攻撃力の上げ下げ（V4・第四波＋第五波・受け取った駒ごと）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 件数/戦 | 弱体（負）/戦 | 強化（正）/戦 | 受け取った駒（件数/戦・符号つきの合計/戦） |");
        Console.WriteLine("|---|--:|--:|--:|---|");
        foreach (var (band, name, f) in rows)
        {
            var t = new UnitTally(); int nb = 0;
            foreach (int st in new[] { 3, 4 }) { var l = data[(name, st, "V4")]; t.Add(l.T); nb += l.N; }
            string P(double x) => (x / Math.Max(1, nb)).ToString("F2");
            string who = t.KissStolenBy is null ? "—" : string.Join("・", t.KissStolenBy.OrderByDescending(kv => kv.Value.N)
                .Select(kv => (UnitCatalog.Everyone.FirstOrDefault(u => u.Id == kv.Key)?.Name ?? kv.Key) + " " + P(kv.Value.N) + "（" + P(kv.Value.Sum) + "）"));
            Console.WriteLine("| " + name + " | " + P(t.KissStealN) + " | " + P(-t.KissStealNeg) + " | " + P(t.KissStealPos) + " | " + who + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## §4.3 診断専用の行（" + DiagRowName + "）の V3 と V4（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 | ウツが受け取った弱体/戦 | ウツの与ダメ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|");
        Formation dr = DiagRow();
        foreach (string tag in new[] { "V3", "V4" })
        {
            UnitDef d = Ladder.First(v => v.Tag == tag).D;
            int wins = 0, nb = 0; double utsuDmg = 0; var t = new UnitTally();
            foreach (int st in Waves25) { var l = Collect2(Apply(dr, d), st); wins += l.Wins; nb += l.N; utsuDmg += l.UtsuDmgSum; t.Add(l.T); }
            long toUtsu = t.KissStolenBy is not null && t.KissStolenBy.TryGetValue("utsu", out var u) ? u.Sum : 0;
            Console.WriteLine("| " + tag + " | " + (100.0 * wins / Math.Max(1, nb)).ToString("F1") + " | " + (toUtsu / (double)Math.Max(1, nb)).ToString("F2") + " | "
                              + (utsuDmg / Math.Max(1, nb)).ToString("F1") + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // check2 —— §6
    // =================================================================================

    static void Check205(string arg)
    {
        Console.WriteLine("# 第205期 `lili check2` —— 自己検査");
        Console.WriteLine();
        bool ok = true;
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        var old = ReadBalance(path);
        Console.WriteLine("## (1) `compare` の回帰（比べる表: `" + path + "`・" + old.Count + " 行）");
        Console.WriteLine();
        int noRows = 0, noDiff = 0, v0Rows = 0, v0Diff = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            if (!old.TryGetValue(name, out double[]? want)) { Console.WriteLine("- 行が見つからない: " + name); ok = false; continue; }
            if (!HasLili(f)) { noRows++; double[] got = Rates(f); for (int st = 0; st < 5; st++) if (Math.Abs(got[st] - want[st]) > 0.05) noDiff++; }
            else { v0Rows++; double[] got = Rates(Apply(f, Ladder[0].D)); for (int st = 0; st < 5; st++) if (Math.Abs(got[st] - want[st]) > 0.05) v0Diff++; }
        }
        Console.WriteLine("- リリを含まない " + noRows + " 行 × 5 波: 差 **" + noDiff + "** セル " + (noDiff == 0 ? "○" : "×"));
        Console.WriteLine("- リリの在席 " + v0Rows + " 行を V0 にした版 × 5 波: 差 **" + v0Diff + "** セル " + (v0Diff == 0 ? "○" : "×"));
        ok &= noDiff == 0 && v0Diff == 0;
        Console.WriteLine();

        Console.WriteLine("## (3) 台本（verbose）で見る規則（在席行 ＋ 診断行 × 第2〜5波 × seed 0..39・V1〜V4）");
        Console.WriteLine();
        var rows = LiliRows().Select(r => r.F).ToList();
        rows.Add(DiagRow());
        int battles = 0, painActs = 0, painMismatch = 0, amountMismatch = 0, overDrain = 0, armorAfterV2 = 0, tierDown = 0,
            riteTransfer = 0, riteSteal = 0, tierEvents = 0, stealEvents = 0, wasteEvents = 0, stealNoFlag = 0;
        for (int vi = 1; vi < Ladder.Length; vi++)
        {
            UnitDef d = Ladder[vi].D;
            bool voids = d.Traits.Contains(TraitId.KissVoid), steals = d.Traits.Contains(TraitId.KissSteal);
            foreach (Formation f0 in rows)
            foreach (int st in Waves25)
            for (int seed = 0; seed < 40; seed++)
            {
                Formation f = Apply(f0, d);
                var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, BossRule.Default.Scale);
                BattleResult r = BattleEngine.Run(pl, en, seed, verbose: true);
                battles++;
                var allies = pl.Select(u => u.InstanceId).ToHashSet();
                var hp = pl.ToDictionary(u => u.InstanceId, u => u.MaxHp);
                int? lili = pl.FirstOrDefault(u => u.Def.Id == "lili")?.InstanceId;
                long lostSince = 0;          // 前のリリの手番の終わりから、味方が失った HP（台本から組み直す）
                int drainsThisTurn = -1, slotThisTurn = 0, amountThisTurn = 0, lastTier = 0;
                bool inRite = false, inLiliTurn = false;
                foreach (var e in r.Events)
                {
                    switch (e.Kind)
                    {
                        case BattleEventKind.Damage when e.TargetId is int tg && allies.Contains(tg):
                            {
                                int before = hp.TryGetValue(tg, out int h) ? h : e.HpAfter + e.Amount;
                                // リリの手番の中（痛みを測ってから手番が終わるまで）の減りは、エンジンでも次の痛みに入らない（印は手番の終わりに控える）。
                                if (!inLiliTurn) lostSince += Math.Max(0, before - e.HpAfter);
                                hp[tg] = e.HpAfter;
                                break;
                            }
                        case BattleEventKind.Heal or BattleEventKind.Revive when e.TargetId is int tg2 && allies.Contains(tg2):
                            hp[tg2] = e.HpAfter; break;
                        case BattleEventKind.Summon when e.TargetId is int tg3 && e.Team == BattleContext.PlayerTeam:
                            allies.Add(tg3); hp[tg3] = e.HpAfter; break;
                        case BattleEventKind.TurnStart or BattleEventKind.Attack or BattleEventKind.Skill when inLiliTurn && e.ActorId != lili:
                            inLiliTurn = false; break;
                    }
                    if (e.Kind == BattleEventKind.StatusTransfer && inRite) riteTransfer++;
                    if (e.Kind != BattleEventKind.Kiss || e.ActorId != lili) continue;
                    switch (e.Text)
                    {
                        case KissLabels.Pain:
                            painActs++;
                            if (e.Amount != lostSince) painMismatch++;
                            if (e.StatusRemaining != KissTrait.PainAmount(e.Amount)) amountMismatch++;
                            lostSince = 0;   // 手番の頭で測ったので、手番が終わってから次の手番までを数え直す
                            inLiliTurn = true;
                            drainsThisTurn = 0; slotThisTurn = e.Slot; amountThisTurn = e.StatusRemaining ?? 0;
                            break;
                        case KissLabels.Drain:
                            if (drainsThisTurn >= 0 && ++drainsThisTurn > slotThisTurn) overDrain++;
                            break;
                        case KissLabels.Rite: inRite = true; drainsThisTurn = -1; break;
                        case KissLabels.RiteEnd: inRite = false; break;
                        case KissLabels.Armor: armorAfterV2 += voids ? 1 : 0; break;
                        case KissLabels.Waste: wasteEvents++; break;
                        case KissLabels.Tier: tierEvents++; if (e.Slot <= lastTier) tierDown++; lastTier = e.Slot; break;
                        case KissLabels.Steal: stealEvents++; if (inRite) riteSteal++; if (!steals) stealNoFlag++; break;
                    }
                }
            }
        }
        void Row(string what, int bad, string note = "")
        {
            Console.WriteLine("| " + what + " | " + bad + " | " + (bad == 0 ? "○" : "**×**") + " | " + note + " |");
            ok &= bad == 0;
        }
        Console.WriteLine("戦 " + battles + " ／ 痛みを測った手番 " + painActs + " ／ 段の出来事 " + tierEvents + " ／ 強弱が移った件数 " + stealEvents + " ／ 捨てた溢れの件数 " + wasteEvents + "。");
        Console.WriteLine();
        Console.WriteLine("| 規則 | 違反 | 判定 | 注 |");
        Console.WriteLine("|---|--:|:-:|---|");
        Row("痛み ＝ 台本の Damage から組み直した「前の手番から味方が失った HP」（破片で受けた分は入らない・肩代わりは二重にならない）", painMismatch,
            "Damage の Amount は破片の後の値で、HP の前後から実額を取る");
        Row("吸う量 ＝ max(床, 痛み × 割合)", amountMismatch);
        Row("1手番に吸う体数が 1 ＋ 段 を超えない", overDrain);
        Row("V2 以降でリリ由来の破片が 0", armorAfterV2);
        Row("段は下がらない", tierDown);
        Row("儀式で状態が移らない", riteTransfer);
        Row("儀式で強弱が移らない", riteSteal);
        Row("強弱が移るのは V4 だけ", stealNoFlag);
        Console.WriteLine();
        Console.WriteLine(ok ? "**全部 ○。**" : "**× がある。**");
    }
}
