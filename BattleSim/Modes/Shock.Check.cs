using BattleCore;
using static Common;

// =====================================================================================
// shock check（第214期）—— 自己検査（指示書 §8 の 2・3・7）。
// 1（`compare` 0 セル）と 4（アカ・K0 の台本）は `docs/` の再生成と `shockdigest` の突き合わせで見る（報告に書く）。
// =====================================================================================

static partial class ShockDiag
{
    const int CheckSeeds = 50;

    static partial void CheckImpl(string arg)
    {
        Console.WriteLine("# 第214期 `shock check` —— 自己検査");
        Console.WriteLine();
        var tables = Tables();
        int fails = 0;
        void Line(string name, bool ok, string detail)
        {
            if (!ok) fails++;
            Console.WriteLine("| " + name + " | " + detail + " | " + (ok ? "**○**" : "**×**") + " |");
        }

        // ---------------- 受け入れ 2: verbose の有無で勝敗・決着が同じ ----------------
        long runs = 0, mism = 0;
        var gate = new object();
        var events = new List<(string Label, int St, BattleResult R)>();
        var totK1 = new UnitTally(); var totK2 = new UnitTally();
        foreach (Table tb in tables)
            foreach (FormationShape sh in Shapes)
                foreach (var (tag, def) in Versions)
                {
                    Formation f = WithKata(tb.Seats[sh], def);
                    Parallel.For(0, 4 * CheckSeeds, j =>
                    {
                        int st = 1 + j / CheckSeeds, s = j % CheckSeeds;
                        Formation en = EnemyCatalog.Stages[st].Enemy;
                        BattleResult a = BattleEngine.Run(f, en, s, verbose: false);
                        BattleResult b = BattleEngine.Run(f, en, s, verbose: true);
                        bool same = a.PlayerWon == b.PlayerWon && a.Turns == b.Turns && a.PlayerSurvivors == b.PlayerSurvivors;
                        lock (gate)
                        {
                            runs++;
                            if (!same) mism++;
                            if (tag is "K1" or "K2") events.Add((tb.Tag + "/" + ShapeName(sh) + "/" + tag, st, b));
                            foreach (UnitTally t in b.TallyByUnit.Values) (tag == "K1" ? totK1 : tag == "K2" ? totK2 : new UnitTally()).Add(t);
                        }
                    });
                }

        // 破片の台（板を貼るツギの隣で、カタの感電を浴びた味方が殴られる）と、ベニの台（台1）
        Formation armorBench = Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Tsugi, center: UnitCatalog.Kata,
                                               back1: UnitCatalog.Guza, back3: UnitCatalog.Borg);
        var totArmor = new UnitTally();
        for (int st = 1; st < 5; st++)
            for (int s = 0; s < CheckSeeds; s++)
            {
                BattleResult b = BattleEngine.Run(armorBench, EnemyCatalog.Stages[st].Enemy, s, verbose: true);
                events.Add(("破片の台", st, b));
                foreach (UnitTally t in b.TallyByUnit.Values) totArmor.Add(t);
            }

        Console.WriteLine("| 検査 | 中身 | 判定 |");
        Console.WriteLine("|---|---|---|");
        Line("2 verbose の有無", mism == 0, runs + " 戦（4台 × 2陣形 × 4版 × 第2〜5波 × seed 0.." + (CheckSeeds - 1) + "）で勝敗・決着T・生存数の食い違い " + mism);

        // ---------------- 受け入れ 3・7: 台本から ----------------
        long chains = 0, dupDischarge = 0, kataInit = 0, rootNoDamage = 0, casts = 0, dupHit = 0, thunder = 0, kindMism = 0, kindCount = 0;
        long evThunder = 0, evSpent = 0, evDis = 0, evGain = 0, evDisInv = 0, wave2Spent = 0, hopOrder = 0;
        foreach (var (label, st, r) in events)
        {
            var shocked = new HashSet<int>();
            HashSet<int>? chain = null;
            HashSet<int>? cast = null;
            int lastHop = 0;
            var lastDamage = new Dictionary<int, int>();   // 駒 → 最後に HP へ届いた一撃の位置
            var lastGain = new Dictionary<int, int>();     // 駒 → 最後に感電が付いた位置
            int? thunderTarget = null;                     // 直前の雷の命中の相手（次の感電の付与・雷まで見張る）
            var evs = r.Events;
            for (int idx = 0; idx < evs.Count; idx++)
            {
                BattleEvent e = evs[idx];
                switch (e.Kind)
                {
                    case BattleEventKind.Damage:
                        if (e.TargetId is int dt) lastDamage[dt] = idx;
                        break;
                    case BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.Charge or BattleEventKind.TurnStart:
                        thunderTarget = null;   // 雷の手番が終わった
                        break;
                    case BattleEventKind.StatusGain when e.Text == StatusKeys.Shock:
                        evGain++;
                        if (e.TargetId is int g) { shocked.Add(g); lastGain[g] = idx; if (g == thunderTarget) thunderTarget = null; }
                        break;
                    case BattleEventKind.StatusTransfer when e.Text == StatusKeys.Shock:
                        if (e.TargetId is int tt) shocked.Add(tt);
                        if (e.SpreadFromId is int sf) shocked.Remove(sf);
                        break;
                    case BattleEventKind.ShockSpent:
                        evSpent++;
                        if (st == 1) wave2Spent++;
                        if (e.Slot == 0)
                        {
                            chains++;
                            chain = new HashSet<int>();
                            // 起点は「感電が付いた後に HP へ届いた一撃」を受けているはず（破片が受け切った一撃は `Damage` を出さない）
                            if (e.TargetId is int rt && lastDamage.GetValueOrDefault(rt, -1) < lastGain.GetValueOrDefault(rt, -1)) rootNoDamage++;
                            // 雷の命中の直後（その相手に感電が付くまで・次の雷まで）にその相手が起点になったら、雷が起爆したことになる
                            if (e.TargetId == thunderTarget) kataInit++;
                        }
                        if (chain is not null && e.TargetId is int x && !chain.Add(x)) dupDischarge++;
                        if (e.TargetId is int y) shocked.Remove(y);
                        break;
                    case BattleEventKind.Discharge:
                        evDis++;
                        if (e.SourceTrait == TraitId.Inverse) evDisInv++;
                        break;
                    case BattleEventKind.Thunder:
                        evThunder++;
                        thunder++;
                        thunderTarget = e.TargetId;
                        if (e.Slot == 1) { casts++; cast = new HashSet<int>(); lastHop = 0; }
                        if (e.Slot != lastHop + 1) hopOrder++;
                        lastHop = e.Slot;
                        if (cast is not null && e.TargetId is int h && !cast.Add(h)) dupHit++;
                        var labels = string.IsNullOrEmpty(e.Text) ? Array.Empty<string>() : e.Text!.Split(',');
                        if (labels.Length != e.StatusRemaining) kindMism++;
                        bool hadShock = e.TargetId is int z && shocked.Contains(z);
                        if (labels.Contains(StatusKeys.LabelOf(StatusKeys.Shock)) != hadShock) kindMism++;
                        if (hadShock) kindCount++;
                        break;
                }
            }
        }
        Line("3a 1回の連鎖で同じ駒が2回放電しない", dupDischarge == 0, "連鎖 " + chains + " 件・重複 " + dupDischarge);
        Line("3b カタの雷で起爆しない", kataInit == 0 && totK1.ShockThunderMuted > 0,
             "雷の命中の直後にその相手が連鎖の起点になった件数 " + kataInit + " ／ 感電している敵に雷が当たった（起爆せず）K1 " + totK1.ShockThunderMuted + " 回");
        Line("3c 破片が受け切った一撃で起爆しない", rootNoDamage == 0 && totArmor.ShockArmorMuted > 0,
             "連鎖の起点のうち「感電が付いた後に HP へ届いた一撃」が無い件数 " + rootNoDamage + " ／ 破片の台で感電した駒への一撃を破片が受け切った（起爆せず）" + totArmor.ShockArmorMuted + " 回");
        Line("3d K1 で刻みによる起爆 0", totK1.ShockTriggeredTick == 0 && totK1.ShockTickMuted > 0 && totK2.ShockTriggeredTick > 0,
             "K1 の刻みの連鎖 " + totK1.ShockTriggeredTick + "（感電した駒への刻み " + totK1.ShockTickMuted + " 回は起爆せず）／ K2 の刻みの連鎖 " + totK2.ShockTriggeredTick);
        Line("3e 雷が同じ敵に2回当たらない", dupHit == 0 && hopOrder == 0, "雷 " + casts + " 回・命中 " + thunder + "・重複 " + dupHit + "・跳ねの番号の飛び " + hopOrder);
        Line("3f 種類は命中の前に数える", kindMism == 0 && kindCount > 0,
             "種類の数と内訳の食い違い・「雷」の有無と直前の感電の食い違い " + kindMism + "（前の手番までに付いた感電を数えた命中 " + kindCount + "）");
        Line("7 台本の出来事", evThunder > 0 && evSpent > 0 && evDis > 0 && evGain > 0,
             "雷 " + evThunder + " ／ 感電が付いた " + evGain + " ／ 起爆 " + evSpent + " ／ 放電 " + evDis + "（うちベニの反転 " + evDisInv + "）");
        Line("粛の波でも放電する", wave2Spent > 0, "第二波（粛）の起爆 " + wave2Spent);
        Line("ThunderTrait.CountedKeys ＝ Phase 0 の一覧", ThunderTrait.CountedKeys.SequenceEqual(ShockKinds.Counted), string.Join("・", ThunderTrait.CountedKeys.Select(StatusKeys.LabelOf)));
        Console.WriteLine();
        Console.WriteLine(fails == 0 ? "SHOCK_CHECK ok=True" : "SHOCK_CHECK ok=False fails=" + fails);
    }
}
