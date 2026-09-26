using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// =====================================================================================
// mire check（第218期）—— 自己検査（受け入れ 2・3・6）。
// 受け入れ 1（ミオのいない行は 0 セル・M0 の台本が実装の前後で一致）は `compare` と `shockdigest m218` の全文比較で見る（報告）。
// =====================================================================================

static partial class MireDiag
{
    static partial void CheckImpl(string arg)
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第218期 `mire check` —— 自己検査");
        Console.WriteLine();
        bool allOk = true;
        void Verdict(string name, bool ok, string detail)
        {
            allOk &= ok;
            Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + name + ": " + detail);
        }

        var benches = Benches();
        string[] newVers = { "M1", "M2", "M3", "M3x", "M4", "M5" };

        // (2) verbose の有無で勝敗・決着ターン・生存数が同じ。
        long cells = 0, mism = 0;
        var gate = new object();
        foreach (var (_, bf, _) in benches)
            foreach (string v in newVers)
            {
                Formation f = WithMio(bf, VerOf(v));
                Parallel.For(0, 4 * 50, j =>
                {
                    int st = 1 + j / 50, s = j % 50;
                    var boss = new BossRule(false) { Scale = ShockDiag.Scale150 };
                    BattleResult q = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: false, boss: boss);
                    BattleResult w = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: true, boss: boss);
                    bool same = q.PlayerWon == w.PlayerWon && q.Turns == w.Turns && q.PlayerSurvivors == w.PlayerSurvivors;
                    lock (gate) { cells++; if (!same) mism++; }
                });
            }
        Verdict("(2) verbose の有無（新しい版 × 台 × 第2〜5波 × seed 0..49・倍率 150）", mism == 0, cells + " 戦で勝敗・決着ターン・生存数の食い違い " + mism);

        // (3)(6) 台本と帳簿。
        long slamTurnsOver = 0, slams = 0, slamEv = 0, conductEv = 0, conductTally = 0, conductGroupsOver = 0;
        long conductNotShocked = 0, conductHeads = 0, conductNoMark = 0, conductOnAlly = 0, slamOnShockedNoConduct = 0;
        long carryEv = 0, carryTally = 0, carryBadFrom = 0, handoffEv = 0, handoffTally = 0, handoffNoDeath = 0;
        long pctMax = 0, pctOver = 0, allyCutM3 = 0, allyCutM3x = 0, foeCut = 0, m0NewEvents = 0;
        var pctRe = new Regex(@"澱みに手を取られた（-(\d+)%");
        foreach (var (_, bf, _) in benches)
            foreach (var (v, def) in Versions)
            {
                Formation f = WithMio(bf, def);
                for (int st = 1; st < 5; st++)
                    for (int s = 0; s < 25; s++)
                        foreach (var scale in new[] { ShockDiag.Scale115, ShockDiag.Scale150 })
                        {
                            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                            var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, scale);
                            BattleResult r = BattleEngine.Run(pl, en, s, verbose: true);
                            int mio = pl.First(u => u.Def.Id == "mio").InstanceId;
                            var ally = pl.Select(u => u.InstanceId).ToHashSet();
                            var foe = en.Select(u => u.InstanceId).ToHashSet();
                            foreach (var (id, t) in r.TallyByUnit)
                            {
                                if (id == "mio") { slams += t.MireSlams; conductTally += t.MireConductHits; carryTally += t.MireCarried; handoffTally += t.MireHandoffs; }
                                bool isAlly = pl.Any(u => u.Def.Id == id);
                                if (isAlly) { if (v is "M3" or "M4" or "M5") allyCutM3 += t.MireDulledCut; if (v == "M3x") allyCutM3x += t.MireDulledCut; }
                                else foeCut += t.MireDulledCut;
                            }
                            foreach (LogLine l in r.Log)
                            {
                                var m = pctRe.Match(l.Text);
                                if (!m.Success) continue;
                                int p = int.Parse(m.Groups[1].Value);
                                if (p > pctMax) pctMax = p;
                                if (p > MireDullTrait.MaxPercent) pctOver++;
                            }
                            var shocked = new HashSet<int>();
                            var marks = new Dictionary<int, int>();
                            var slamsPerTurn = new Dictionary<int, int>();
                            var conductTurns = new Dictionary<int, int>();
                            int? lastSlamTarget = null; bool lastSlamConduct = false; int lastSlamTurn = -1;
                            var ev = r.Events;
                            for (int i = 0; i < ev.Count; i++)
                            {
                                BattleEvent e = ev[i];
                                int tg = e.TargetId ?? -1;
                                if (v == "M0" && e.Kind is BattleEventKind.MireSlam or BattleEventKind.MireConduct) m0NewEvents++;
                                if (v == "M0" && e.Kind == BattleEventKind.ConcentrateMark && e.Text is ConcentrateTrait.CarryLabel or ConcentrateTrait.HandoffLabel) m0NewEvents++;
                                switch (e.Kind)
                                {
                                    case BattleEventKind.StatusGain when e.Text == StatusKeys.Shock: shocked.Add(tg); break;
                                    case BattleEventKind.ShockSpent: shocked.Remove(tg); break;
                                    case BattleEventKind.ConcentrateMark:
                                        marks[tg] = e.Amount;
                                        if (e.Text == ConcentrateTrait.CarryLabel)
                                        {
                                            carryEv++;
                                            int j = i - 1;
                                            while (j >= 0 && !(ev[j].Kind == BattleEventKind.Discharge && ev[j].TargetId == tg)) j--;
                                            if (j < 0 || ev[j].ActorId is not int from || marks.GetValueOrDefault(from) <= 0) carryBadFrom++;
                                        }
                                        if (e.Text == ConcentrateTrait.HandoffLabel)
                                        {
                                            handoffEv++;
                                            int j = i - 1;
                                            while (j >= 0 && !(ev[j].Kind == BattleEventKind.Death && ev[j].TargetId == e.SpreadFromId)) j--;
                                            if (j < 0 || e.SpreadFromId is not int d || !foe.Contains(d)) handoffNoDeath++;
                                            else marks[d] = 0;
                                        }
                                        break;
                                    case BattleEventKind.MireSlam:
                                        slamEv++;
                                        slamsPerTurn[e.Turn] = slamsPerTurn.GetValueOrDefault(e.Turn) + 1;
                                        lastSlamTarget = tg; lastSlamTurn = e.Turn; lastSlamConduct = e.Slot == 1;
                                        if (e.Slot == 1) { conductHeads++; if (!shocked.Contains(tg)) conductNotShocked++; }
                                        else if (shocked.Contains(tg) && def.Traits.Contains(TraitId.MireConduct)) slamOnShockedNoConduct++;
                                        break;
                                    case BattleEventKind.MireConduct:
                                        conductEv++;
                                        if (!lastSlamConduct || e.SpreadFromId != lastSlamTarget || e.Turn != lastSlamTurn) conductGroupsOver++;
                                        conductTurns[e.Turn] = 1;
                                        if (marks.GetValueOrDefault(tg) <= 0) conductNoMark++;
                                        if (!foe.Contains(tg)) conductOnAlly++;
                                        break;
                                }
                            }
                            slamTurnsOver += slamsPerTurn.Count(kv => kv.Value > 1);
                        }
            }
        Verdict("(3a) 叩きつけは1手番に1回", slamTurnsOver == 0 && slams > 0, "叩きつけ " + slams + "・同じターンに2回以上 " + slamTurnsOver);
        Verdict("(3b) 通電は叩きつける相手が感電しているときだけ・1手番に1回", conductNotShocked == 0 && conductGroupsOver == 0 && slamOnShockedNoConduct == 0 && conductHeads > 0,
                "通電の起点 " + conductHeads + " のうち相手が感電していなかった " + conductNotShocked + "・起点の外の通電 " + conductGroupsOver + "・通電の札を持ち相手が感電していたのに起点にならなかった " + slamOnShockedNoConduct);
        Verdict("(3c) 通電は印を持つ敵だけに届く", conductNoMark == 0 && conductOnAlly == 0 && conductEv > 0, "通電の1本 " + conductEv + " のうち印の無い相手 " + conductNoMark + "・味方 " + conductOnAlly);
        Verdict("(3d) M3・M4・M5 で味方の与ダメが下がらない（M3x では下がる）", allyCutM3 == 0 && allyCutM3x > 0 && foeCut > 0,
                "M3/M4/M5 の味方の減り " + allyCutM3 + "・M3x の味方の減り " + allyCutM3x + "・敵の減り " + foeCut);
        Verdict("(3e) デバフの上限 −40% を超えない", pctOver == 0 && pctMax > 0, "ログの最大 −" + pctMax + "%・超えた " + pctOver);
        Verdict("(3f) 印を運ぶのは印を持つ駒が弾けたときだけ", carryBadFrom == 0 && carryEv > 0, "運び " + carryEv + " のうち直前の放電の出どころに印が無かった " + carryBadFrom);
        Verdict("(3g) 倒れたら移るのは敵が倒れたときだけ", handoffNoDeath == 0 && handoffEv > 0, "移り " + handoffEv + " のうち敵の死が無かった " + handoffNoDeath);
        Verdict("(6a) 叩きつけ・通電・運び・移りが台本に読める（出来事 ＝ 帳簿）", slamEv == slams && conductEv == conductTally && carryEv == carryTally && handoffEv == handoffTally,
                "叩きつけ " + slamEv + " ＝ " + slams + "・通電 " + conductEv + " ＝ " + conductTally + "・運び " + carryEv + " ＝ " + carryTally + "・移り " + handoffEv + " ＝ " + handoffTally);
        Verdict("(6b) M0 の台本に新しい出来事が無い", m0NewEvents == 0, "M0 の新しい出来事 " + m0NewEvents);

        // (4) 札の保持者は 0 枚（規定のミオは M0）。
        var cards = new[] { TraitId.MireSlam, TraitId.MireConduct, TraitId.MireDull, TraitId.MireDullAll, TraitId.MireCarry, TraitId.MireHandoff };
        var held = UnitCatalog.Everyone.SelectMany(u => u.Traits.Where(cards.Contains).Select(t => u.Id + ":" + t)).ToList();
        Verdict("(4) 新しい札の保持者は 0 枚・ミオは M0", held.Count == 0 && UnitCatalog.Mio.Traits.SequenceEqual(new[] { TraitId.Concentrate, TraitId.ConcentrateLeak }),
                held.Count == 0 ? "0 枚・ミオの札 " + string.Join(", ", UnitCatalog.Mio.Traits) : string.Join(" ／ ", held));

        Console.WriteLine();
        Console.WriteLine("MIRE218_CHECK " + (allOk ? "ok=True" : "ok=False") + " 所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }

    static partial void LogImpl(string arg)
    {
        // 例: "M台2 X字|M2|1|3|150"（台名の部分一致|版|波（0始まり）|seed|倍率）
        var p = arg.Split('|');
        var b = Benches().First(x => x.Name.Contains(p[0]));
        Formation f = WithMio(b.F, VerOf(p[1]));
        int st = int.Parse(p[2]), s = int.Parse(p[3]);
        var scale = p.Length > 4 && p[4] == "150" ? ShockDiag.Scale150 : ShockDiag.Scale115;
        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, s, verbose: true, boss: new BossRule(false) { Scale = scale });
        foreach (LogLine l in r.Log) Console.WriteLine(l.Text);
    }
}
