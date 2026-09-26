using BattleCore;
using static Common;

// =====================================================================================
// whip check（第217期）—— 自己検査（受け入れ 2・3・6）。
// 受け入れ 1（シガのいない行は 0 セル・G0 の台本が実装の前後で一致）は `compare` と `shockdigest w217` の全文比較で見る（報告 §3）。
// =====================================================================================

static partial class WhipDiag
{
    static partial void CheckImpl(string arg)
    {
        var t0 = DateTime.Now;
        Console.WriteLine("# 第217期 `whip check` —— 自己検査");
        Console.WriteLine();
        bool allOk = true;
        void Verdict(string name, bool ok, string detail)
        {
            allOk &= ok;
            Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + name + ": " + detail);
        }

        var benches = new List<Formation>();
        foreach (var s in SeatSearch(115).Concat(SeatSearch(150))) benches.Add(s.Best);
        foreach (var (_, f) in W4Rows()) benches.Add(f);
        string[] newVers = { "G1", "G2", "G3", "G3H", "G3K" };

        // (2) verbose の有無で勝敗・決着ターン・生存数が同じ。
        long cells = 0, mism = 0;
        var gate = new object();
        foreach (Formation b in benches)
            foreach (string v in newVers)
            {
                Formation f = WithVer(b, v);
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

        // (3) 台本と帳簿で見る検査。
        long screamTurns = 0, screamOver = 0, lwEvents = 0, lwTallies = 0, lwMarkedEv = 0, lwMarkedTally = 0, lwOverAmount = 0, lwDischarge = 0;
        long splashMov = 0, cowered = 0, shouldCower = 0, wiredLeft = 0, wiredSwings = 0, guarded = 0, guardViol = 0, guardViolNext = 0, doubledEv = 0, doubledTally = 0;
        foreach (Formation b in benches)
            foreach (string v in newVers)
            {
                Formation f = WithVer(b, v);
                bool hasKugu = f.Occupied().Any(o => o.Def.Id == "kugu");
                for (int st = 1; st < 5; st++)
                    for (int s = 0; s < 50; s++)
                    {
                        var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                        var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, ShockDiag.Scale150);
                        BattleResult r = BattleEngine.Run(pl, en, s, verbose: true);
                        int shiga = pl.First(u => u.Def.Id == "shiga").InstanceId;
                        if (r.TallyByUnit.TryGetValue("shiga", out var t))
                        {
                            splashMov += t.WhipCheckSplashMovable; cowered += t.WhipCowered; shouldCower += t.WhipCheckShouldCower;
                            wiredLeft += t.WhipCheckWiredLeft; wiredSwings += t.WiredSwings; lwTallies += t.WiredSwings; lwMarkedTally += t.WiredMarked;
                            doubledTally += t.WhipDoubled;
                        }
                        foreach (var (id, tt) in r.TallyByUnit) guarded += tt.ShockStunGuarded;
                        var ev = r.Events;
                        // 悲鳴（竦みの StatusGain・書き手はシガ）: ターンごとの出どころの数。
                        foreach (var g in ev.Where(e => e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Cowed && e.ActorId == shiga).GroupBy(e => e.Turn))
                        {
                            screamTurns++;
                            if (g.Select(e => e.SpreadFromId).Distinct().Count() > 1) screamOver++;
                        }
                        doubledEv += r.Log.Count(l => l.Text.Contains("の鞭が動けない"));
                        var guardUntil = new Dictionary<int, int>();   // 駒 → 痺れで手番を失ったターン
                        for (int i = 0; i < ev.Count; i++)
                        {
                            BattleEvent e = ev[i];
                            if (e.Kind == BattleEventKind.LiveWire)
                            {
                                lwEvents++;
                                int k = 0;
                                for (int j = i + 1; j < ev.Count && ev[j].Kind == BattleEventKind.StatusGain && ev[j].Text == StatusKeys.Shock && ev[j].ActorId == shiga; j++) k++;
                                lwMarkedEv += k;
                                if (k > e.Amount) lwOverAmount++;
                                for (int j = i + 1; j < ev.Count; j++)
                                {
                                    if (ev[j].Kind is BattleEventKind.Attack or BattleEventKind.TurnStart or BattleEventKind.Skill) break;
                                    if (ev[j].Kind == BattleEventKind.ShockSpent && ev[j].TargetId == shiga) lwDischarge++;
                                }
                            }
                            // G3H: 痺れで手番を失った駒は、そのターンのうちは感電で痺れない（W1・W3 は次の自分の手番の前まで）。
                            if (v == "G3H")
                            {
                                if (e.Kind == BattleEventKind.Stun && e.Text == StunLabels.Lost && e.TargetId is int u) guardUntil[u] = e.Turn;
                                // 自分の手番: 手番の攻撃・術・溜め、または別の理由（竦み・転倒）で手番を失った（その手番の頭で印は落ちている）。
                                bool ownTurn = e.ActorId is int a && guardUntil.ContainsKey(a)
                                               && ((e.Kind == BattleEventKind.Attack && e.Reaction != true) || e.Kind is BattleEventKind.Skill or BattleEventKind.Charge);
                                if (ownTurn) guardUntil.Remove(e.ActorId!.Value);
                                if (e.Kind is BattleEventKind.Cowed or BattleEventKind.Stagger && e.Text == StunLabels.Lost && e.TargetId is int lu) guardUntil.Remove(lu);
                                if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Stun && i > 0 && ev[i - 1].Kind == BattleEventKind.ShockSpent
                                    && e.TargetId is int x && guardUntil.TryGetValue(x, out int lostTurn))
                                {
                                    if (lostTurn == e.Turn) guardViol++;
                                    else if (!hasKugu)
                                    {
                                        guardViolNext++;
                                        if (guardViolNext <= 3)
                                        {
                                            Console.WriteLine("  [G3H の違反候補] 第" + (st + 1) + "波 seed " + s + " 駒 " + x + " 失ったT " + lostTurn + " 痺れたT " + e.Turn + " —— その駒の出来事:");
                                            foreach (var q in ev.Take(i + 1).Where(q => (q.TargetId == x || q.ActorId == x) && q.Turn >= lostTurn))
                                                Console.WriteLine("    T" + q.Turn + " " + q.Kind + " actor=" + q.ActorId + " target=" + q.TargetId + " text=" + q.Text + " react=" + q.Reaction);
                                        }
                                    }
                                }
                            }
                        }
                    }
            }
        Verdict("(3a) 悲鳴は1手番に1回", screamOver == 0, "悲鳴の出たターン " + screamTurns + " のうち、出どころが2つ以上 " + screamOver);
        Verdict("(3b) 巻き込んだ動ける敵では怖気づかない", cowered == shouldCower && splashMov > 0,
                "主目標が動けず巻き込みに動ける敵がいた振り " + splashMov + "・怖気づいた " + cowered + " ＝ 主目標が動ける（感電していない）振り " + shouldCower);
        Verdict("(3c) 電気鞭の後にシガの感電が 0", wiredLeft == 0 && wiredSwings > 0, "電気鞭 " + wiredSwings + " 回のうち感電が残った " + wiredLeft);
        Verdict("(3d) 電気鞭でシガが放電しない", lwDischarge == 0, "電気鞭の後の同じ一撃の中でシガの感電が弾けた " + lwDischarge);
        Verdict("(3e) G3H: 痺れで手番を失った駒は次の自分の手番まで感電で痺れない", guardViol == 0 && guardViolNext == 0 && guarded > 0,
                "同じターンの違反 " + guardViol + "・次のターンの自分の手番の前の違反（クグのいない台だけ） " + guardViolNext + "・ハメ防止で痺れなかった " + guarded);
        Verdict("(6a) 電気鞭が台本に読める", lwEvents == lwTallies && lwMarkedEv == lwMarkedTally && lwOverAmount == 0,
                "LiveWire の出来事 " + lwEvents + " ＝ 帳簿 " + lwTallies + "・直後の感電の StatusGain " + lwMarkedEv + " ＝ 移した感電 " + lwMarkedTally + "・Amount を超えた " + lwOverAmount);
        Verdict("(6b) 2倍はログで数えられる（台本では Damage の量）", doubledEv == doubledTally, "ログ " + doubledEv + " ＝ 帳簿 " + doubledTally);

        // (1') 新しい札の保持者は 0 枚（規定は変えない）。
        var cards = new[] { TraitId.Scourge, TraitId.Lash, TraitId.LiveWire, TraitId.LiveWireGuard, TraitId.ScourgeShock };
        var held = UnitCatalog.Everyone.Where(u => u.Traits.Any(cards.Contains)).Select(u => u.Id).ToList();
        Verdict("(1') 新しい札の保持者は 0 枚", held.Count == 0, held.Count == 0 ? "0 枚（シガは " + string.Join(", ", UnitCatalog.Shiga.Traits) + "）" : string.Join(", ", held));

        Console.WriteLine();
        Console.WriteLine("WHIP217_CHECK " + (allOk ? "ok=True" : "ok=False") + " 所要 " + (DateTime.Now - t0).TotalSeconds.ToString("F0") + " 秒");
    }
}
