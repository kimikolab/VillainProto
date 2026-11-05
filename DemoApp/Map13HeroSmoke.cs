using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 1-3 の追ってくる勇者の門（第459期 §4・`--map13-hero-smoke`・指示書 design/PHASE459_HERO_CHASE_SPEC.md）
//
// **画面を1つも作らない。** `MapStage.Stage13`（追ってくる勇者 `HeroRule.V459`）と `Map11State` ／ `Map11Orders` ／ `Map11Session` だけを回す。
// **ここには Godot の型を1つも使わない**（`Map13Smoke` と同じ扱い）。戦闘は勝ち負けを決めて精算だけする（駒の HP を 0 にする）。
//
//   (a) 門が開いた刻に、勇者が Lanes[road][2] に現れる・経路の選び方（隊のいない経路 → 敵の生存の少ない経路 → 中）
//   (b) 誰も当たらなければ 3 作戦ターンで拠点に着く（毎刻 1 歩・矢印の予告と同じマス）
//   (c) 拠点に隊がいれば迎撃・勝てば ＋20 で勇者は消える
//   (d) 拠点が空なら HomeLost（負けではない）・拠点での呼ぶ・休む・組み直し・戻るは断られ、中間拠点では通る・新しい隊は中間拠点から
//   (e) 拠点が落ちた後に本陣の守り（重）を抜けば勝ち（勇者は生きている・勇者が本陣にいても守りが先）
//   (f) 拠点を落とした勇者はいちばん近い隊へ毎刻 1 歩・落ちた拠点に着いた敵はそこで止まる
//   (g) 勇者が現れる前に空の拠点へ敵が着けば、今と同じく負け
//   (h) 表示: 戦闘の記録の T と画面の作戦ターン（`Map11Session.ShownTurn`）が同じ刻で一致・期限 18 の時間切れで 18
//
// 加えて、器具の 5 方針（1 隊残す・分担＋戻り番・籠城・守る・逃げる）× 100 seed を、第458期の 1-3（門番・`Stage13H458`）と並べる（<b>報告だけ・線は置かない</b>）。
// =====================================================================================

public static class Map13HeroSmoke
{
    public const int Seeds = 100;

    /// <param name="bots">偽なら器具の表（800 通し・数分）を省く（`--map13-hero-quick`・門 (a)〜(h) だけ）。</param>
    public static bool Run(Action<string> write, bool bots = true)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        MapStage stage = MapStage.Stage13;

        void Win(Map11State st, int squad, Map11State.Node node, bool intercept = false)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Enemies) u.Hp = 0;
            st.Resolve(squad, node, true, intercept);
        }
        void Lose(Map11State st, int squad, Map11State.Node node)
        {
            var prep = st.Prepare(squad, node);
            foreach (UnitState u in prep!.Value.Players) u.Hp = 0;
            st.Resolve(squad, node, false, false);
        }
        // 行き先へ歩かせる（当たった相手は勝たせる・敵は動かさない）。
        void Walk(Map11State st, int squad, Dest dest, int steps = 30)
        {
            for (int k = 0; k < steps && !st.Finished; k++)
            {
                if (st.FoeFacing(squad) is { } here) { Win(st, squad, here); continue; }
                if (st.AtDest(squad, dest)) break;
                if (Map11Orders.Apply(st, squad, dest, Map11Orders.Plan(st, squad, dest)) is { } met) Win(st, squad, met);
            }
        }
        // 敵を 1 刻動かす。道の上の勇者との接敵は返す（精算しない）・ほかの接敵は勝たせる・迎撃は拠点の隊で勝たせる（勇者の迎撃も返す）。
        List<Map11State.Encounter> Tick(Map11State st)
        {
            var heroMeets = new List<Map11State.Encounter>();
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (st.Finished) break;
                if (!e.Intercept)
                {
                    if (e.Node.IsHero) heroMeets.Add(e); else Win(st, e.Squad, e.Node);
                    continue;
                }
                if (e.Node.IsHero) heroMeets.Add(e);
                if (st.NextInterceptor(e.Node) is var j && j >= 0) Win(st, j, e.Node, intercept: true);
                st.CloseIntercept(e.Node);
            }
            return heroMeets;
        }
        // 道の敵を抜いた盤（本陣の守りは残す）・既定の 2 隊（5 枚ずつ）。
        Map11State Fresh(params int[] keepLanes)
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);
            st.UseDefaultSquads();
            for (int r = 0; r < st.Nodes.Length; r++)
                if (!keepLanes.Contains(r))
                    foreach (var n in st.Nodes[r]) if (!n.Def.Stationary) n.Cleared = true;
            return st;
        }
        string Cell(Map11State st, int id) => id == st.Graph.Home ? "拠点" : id == st.Graph.Portal ? "本陣" : st.Graph[id].Name;

        // 形 V1: 第1隊は北の砦・第2隊は南の関所（中間拠点 2 つ → 門が開く）。勇者は隊のいない中へ。
        Map11State V1()
        {
            var st = Fresh();
            var g = st.Graph;
            Walk(st, 0, Dest.Of(g, g.Lanes[0][3]));
            Walk(st, 1, Dest.Of(g, g.Lanes[2][3]));
            return st;
        }
        // 形 V2: 第1隊は北の砦 → 野営地 → 北の砦（北と中を取る）・第2隊は拠点に残る。
        Map11State V2(params int[] keepLanes)
        {
            var st = Fresh(keepLanes);
            var g = st.Graph;
            // 残した経路の敵は動かないように、道を歩くあいだだけ遠ざけない（歩くのは北と横の辺だけ）。
            Walk(st, 0, Dest.Of(g, g.Lanes[0][3]));
            Walk(st, 0, Dest.Of(g, g.Lanes[1][3]));
            Walk(st, 0, Dest.Of(g, g.Lanes[0][3]));
            return st;
        }

        // ---- (a) 現れる ----
        {
            var st = V1();
            var g = st.Graph;
            bool closed = st.Hero is null;
            int held = st.HeldOutposts.Count;
            Tick(st);
            var h = st.Hero;
            write($"  (a) 形 V1（第1隊 北の砦・第2隊 南の関所）: 中間拠点 {held} → 門 {(st.GateOpen ? "開" : "閉")}（開いた刻 {st.GateOpenedTurn}）"
                + $" → 勇者 {(h is null ? "なし" : $"{Cell(st, h.Pos)}（{st.LaneName(h.Def.Road)}・拠点まで あと {st.TurnsToHome(h)}）")}・現れた刻 {st.HeroTurn}");
            Req(closed && held == 2 && h is not null && st.HeroTurn == st.GateOpenedTurn && st.HeroTurn == 1
                && h.Pos == g.Lanes[1][2] && h.Def.Road == 1 && st.TurnsToHome(h) == 3,
                "(a) 門が開いた刻に、隊のいない経路（中）の拠点から 3 歩のマス（Lanes[1][2]）に勇者が現れる・拠点まで あと 3");
            // 経路の選び方: 隊のいない経路が 2 本で敵の生存が同じなら中・違えば少ない方。
            var v2 = V2();
            Tick(v2);
            // 中の道の大群（動かない・C3）だけを残す——野営地には敵がいないので取れる・中の道の敵の生存が南より多い。
            var v3 = V2();
            foreach (var n in v3.Nodes[1]) if (n.Def.Hold) n.Cleared = false;
            Tick(v3);
            string Where(Map11State s) => s.Hero is { } x ? s.LaneName(x.Def.Road)
                : $"現れない（中間拠点 {s.HeldOutposts.Count}・門 {s.GateOpen}・陥落 {s.Fallen}・刻 {s.Turn}）";
            write($"  (a) 形 V2（第1隊 北・第2隊 拠点・道の敵なし）→ {Where(v2)} ／ 中の道の大群（3 部隊）だけ残す → {Where(v3)}"
                + $"（中に残る敵 {v3.FoesOn(1).Count(n => n.Pos != v3.Graph.Portal)} 部隊）");
            Req(v2.Hero is { Def.Road: 1 } && v3.Hero is { Def.Road: 2 } && v3.Hero.Pos == v3.Graph.Lanes[2][2],
                "(a) 隊のいない経路のうち、敵の生存が同じなら中・中に敵が残っていれば少ない南（乱数は引かない）");
        }

        // ---- (b) 3 作戦ターンで拠点に着く ／ (d) 拠点が落ちる ／ (f) 追う ----
        {
            var st = V1();
            var g = st.Graph;
            Tick(st);
            var h = st.Hero!;
            var trail = new List<string>();
            bool planOk = true;
            for (int k = 0; k < 3 && !st.HomeLost; k++)
            {
                int? plan = st.PlannedFoeSteps().Where(p => p.Node.IsHero).Select(p => (int?)p.To).FirstOrDefault();
                Tick(st);
                planOk &= plan == h.Pos;
                trail.Add($"刻{st.Turn}:{Cell(st, h.Pos)}");
            }
            write($"  (b) 勇者: {Cell(st, g.Lanes[1][2])} → {string.Join(" → ", trail)}（矢印の予告と同じ {planOk}）");
            Req(trail.Count == 3 && h.Pos == g.Home && st.Turn == st.HeroTurn + 3 && planOk,
                "(b) 誰も当たらなければ、勇者は 3 作戦ターンで拠点に着く（毎刻 1 歩・矢印の予告と同じマス）");

            // (d) 拠点が落ちる（負けではない）。
            int fort = g.Lanes[0][3], gate = g.Lanes[2][3];
            write($"  (d) 拠点が落ちた {st.HomeLost}（刻 {st.HomeLostTurn}・{st.HomeLostBy}）・陥落 {st.Fallen}・終わり {st.Finished}"
                + $" ／ 第3隊・第4隊（枠を開けていない）は {Cell(st, st.Squads[2].Pos)}・{Cell(st, st.Squads[3].Pos)}");
            Req(st.HomeLost && !st.Fallen && !st.Finished && st.HomeLostBy == "勇者" && st.HomeLostTurn == st.HeroTurn + 3,
                "(d) 拠点が空なら拠点が落ちる（HomeLost）が、負けにならない（Finished は偽）");
            Req(st.Squads[2].Pos == fort && st.Squads[3].Pos == fort, "(d) 拠点に残っていた隊（枠を開けていない隊）は取っている中間拠点（北の砦）へ移る");

            // 拠点では断られる（第2隊を仮に拠点へ置いて試す・試した後は南の関所へ戻す）。
            var s1 = st.Squads[1];
            foreach (UnitState u in s1.Units!.Where(x => x.IsAlive)) u.Hp = Math.Max(1, u.MaxHp / 2);
            s1.Pos = g.Home; s1.Via = -1;
            bool homeReform = st.CanReform(1), homeRest = st.Rest(1);
            Map11Orders.Order homeOrder = Map11Orders.Plan(st, 1, Dest.Home);
            s1.Pos = g.Lanes[2][0]; s1.Via = 2;   // 分かれ道から拠点へ戻ろうとする
            bool stepBack = st.StepBack(1);
            Map11Orders.Order backOrder = Map11Orders.Plan(st, 1, Dest.Home);
            bool withdraw = st.CanWithdraw(1) || st.Withdraw(1);
            s1.Pos = gate; s1.Via = -1;
            write($"  (d) 拠点で: 組み直し {homeReform}・休む {homeRest}・行き先「拠点」の命令 {homeOrder} ／ 分かれ道から: 1 歩戻る {stepBack}・命令 {backOrder}・一瞬で戻す {withdraw}");
            Req(!homeReform && !homeRest && homeOrder == Map11Orders.Order.Wait && !stepBack && backOrder == Map11Orders.Order.Wait && !withdraw,
                "(d) 落ちた拠点では組み直し・休むが断られ、拠点へは戻れない（1 歩戻る・一瞬で戻す・行き先「拠点」は待つになる）");
            // 中間拠点では通る。
            var s0 = st.Squads[0];
            foreach (UnitState u in s0.Units!.Where(x => x.IsAlive)) u.Hp = Math.Max(1, u.MaxHp / 2);
            bool fortReform = st.CanReform(0), fortRest = st.Rest(0), trade = st.CanTrade;
            int goldBefore = st.Gold;
            bool slot = st.BuySlot();
            bool newReform = st.CanReform(2), pick = st.Recruit(0);
            write($"  (d) 北の砦で: 組み直し {fortReform}・休む {fortRest}・金の窓 {trade} ／ 3 隊目の枠 {slot}（金 {goldBefore} → {st.Gold}）→ 第3隊は {Cell(st, st.Squads[2].Pos)}・組み直し {newReform}・呼ぶ（無料） {pick}");
            Req(fortReform && fortRest && trade && slot && st.Squads[2].Pos == fort && newReform && pick,
                "(d) 取っている中間拠点では組み直し・休む・呼ぶが通り、新しい隊は中間拠点から出る");

            // (f) 追う: 拠点にいる勇者は、いちばん近い隊（北の砦の第1隊・南の関所の第2隊は同じ 4 歩 → 若い第1隊）へ毎刻 1 歩。
            var chase = st.HeroChase();
            var chaseTrail = new List<string>();
            bool chasePlan = true;
            List<Map11State.Encounter> met = new();
            for (int k = 0; k < 6 && met.Count == 0 && !st.Finished; k++)
            {
                int? plan = st.PlannedFoeSteps().Where(p => p.Node.IsHero).Select(p => (int?)p.To).FirstOrDefault();
                met = Tick(st);
                chasePlan &= plan == h.Pos;
                chaseTrail.Add($"刻{st.Turn}:{Cell(st, h.Pos)}");
            }
            write($"  (f) 追う相手 {(chase is { } c ? $"{st.Squads[c.Squad].Label}（あと {c.Steps}）" : "なし")} → {string.Join(" → ", chaseTrail)}"
                + $" → 接敵 {(met.Count > 0 ? $"{st.Squads[met[0].Squad].Label}@{Cell(st, met[0].Node.Pos)}" : "なし")}（矢印の予告と同じ {chasePlan}）");
            Req(chase is { Squad: 0, Steps: 4 } && met.Count == 1 && met[0].Squad == 0 && h.Pos == fort && chaseTrail.Count == 4 && chasePlan,
                "(f) 拠点を落とした勇者は、いちばん近い隊（同じ歩数なら若い番号）へ毎刻 1 歩で追い、そのマスで戦う");
            // 第1隊が負ける → 次は第2隊を追う。
            Lose(st, 0, h);
            var next = st.HeroChase();
            write($"  (f) 第1隊が勇者に負けた（失った {st.Squads[0].Lost}・勇者 HP {h.HpNow}）→ 次に追う {(next is { } n2 ? $"{st.Squads[n2.Squad].Label}（あと {n2.Steps}）" : "なし")}");
            Req(st.Squads[0].Lost && next is { Squad: 1 } && !st.Finished,
                "(f) 追っていた隊が倒れたら、次にいちばん近い隊（生きた駒のある隊だけ・第3隊は 0 枚なので数えない）を追う");
        }

        // ---- (f') 落ちた拠点に着いた敵はそこで止まる ----
        {
            var st = V1();
            var g = st.Graph;
            for (int k = 0; k < 4 && !st.HomeLost; k++) Tick(st);
            // 湧いた敵を 1 部隊、分かれ道まで来た体にする（湧いていなければ待つ）。
            for (int k = 0; k < 6 && !st.Spawns.Any(n => !n.IsHero && !n.Cleared); k++) Tick(st);
            Map11State.Node? sp = st.Spawns.FirstOrDefault(n => !n.IsHero && !n.Cleared && !n.IsReserve);
            bool placed = sp is not null;
            if (sp is not null) sp.Pos = g.Lanes[sp.Def.Road][0];
            int intercepts0 = st.Intercepts;
            Tick(st);
            int at1 = sp?.Pos ?? -1;
            int intercepts1 = st.Intercepts;
            Tick(st);
            Tick(st);
            write($"  (f') 湧いた {sp?.Def.Name ?? "なし"}（{(sp is null ? "" : st.LaneName(sp.Def.Road))}）: 分かれ道 → {Cell(st, at1)} → 2 刻後 {Cell(st, sp?.Pos ?? -1)}"
                + $"（抜かれた {sp?.Cleared}・迎撃の数 {intercepts0} → {intercepts1} → {st.Intercepts}・陥落 {st.Fallen}・終わり {st.Finished}）");
            Req(placed && at1 == g.Home && sp!.Pos == g.Home && !sp.Cleared && !st.Fallen && st.Intercepts == intercepts1,
                "(f') 落ちた拠点に着いた敵（湧き）はそこで止まる（負けにならない・迎撃もやり直さない）");
        }

        // ---- (c) 拠点に隊がいれば迎撃・勝てば ＋20 ----
        {
            var st = V2();
            var g = st.Graph;
            Tick(st);
            var h = st.Hero!;
            int gold0 = st.Gold;
            var meets = new List<Map11State.Encounter>();
            for (int k = 0; k < 4 && meets.Count == 0 && !st.Finished; k++) meets = Tick(st);
            int heroGold = st.GoldLog.Where(e => e.Reason.StartsWith(h.Def.Name, StringComparison.Ordinal)).Sum(e => e.Amount);
            write($"  (c) 形 V2（第2隊が拠点）: 勇者 {st.LaneName(h.Def.Road)} から 刻 {st.Turn} に {(meets.Count > 0 && meets[0].Intercept ? "【迎撃】" : "")}"
                + $" → 倒した {h.Cleared}・金 {gold0} → {st.Gold}（勇者の分 ＋{heroGold}）・拠点が落ちた {st.HomeLost}・結果の 1 行「{st.HeroOutcome}」");
            Req(meets.Count == 1 && meets[0].Intercept && h.Cleared && heroGold == 20 && !st.HomeLost && st.Turn == st.HeroTurn + 3,
                "(c) 拠点に隊がいれば迎撃になり、勝てば ＋20 で勇者は消える");
        }

        // ---- (d') 取っている中間拠点が無ければ新しい隊は出せない（後で取れば中間拠点へ移る） ----
        {
            var st = V1();
            var g = st.Graph;
            Tick(st);
            // 第1隊・第2隊を中間拠点の 1 つ手前へ下げ、中間拠点は取り返された体にする。
            Walk(st, 0, Dest.Of(g, g.Lanes[0][2]));
            Walk(st, 1, Dest.Of(g, g.Lanes[2][2]));
            st.HeldOutposts.Clear();
            for (int k = 0; k < 4 && !st.HomeLost; k++) Tick(st);
            bool trade = st.CanTrade, slot = st.BuySlot();
            int stuck = st.Squads.Count(q => !q.Lost && q.Pos == g.Home);
            Walk(st, 0, Dest.Of(g, g.Lanes[0][3]));
            write($"  (d') 中間拠点なしで拠点が落ちた: 金の窓 {trade}・枠を買う {slot}・拠点に取り残された隊 {stuck} → 第1隊が北の砦を取り直す → 第3隊・第4隊は {Cell(st, st.Squads[2].Pos)}・{Cell(st, st.Squads[3].Pos)}");
            Req(st.HomeLost && !trade && !slot && stuck == 2 && st.Squads[2].Pos == g.Lanes[0][3] && st.Squads[3].Pos == g.Lanes[0][3],
                "(d') 取っている中間拠点が無ければ新しい隊は出せない（金の窓も閉じる）・中間拠点を取れば拠点に残っていた隊はそこへ移る");
        }

        // ---- (e) 拠点が落ちた後に本陣の守り（重）を抜けば勝ち（勇者は生きている） ----
        {
            var st = V1();
            var g = st.Graph;
            for (int k = 0; k < 4 && !st.HomeLost; k++) Tick(st);
            var h = st.Hero!;
            Map11State.Node garrison = st.Nodes[1].Single(n => n.Def.Stationary);
            Walk(st, 1, Dest.Portal(g, 2));
            write($"  (e) 拠点が落ちた後、第2隊が南の関所から本陣へ: 守り {garrison.Cleared}・制圧 {st.Captured}・勝ち {st.Won}・終わり {st.Finished}・勇者 {Cell(st, h.Pos)}（倒した {h.Cleared}）・「{st.HeroOutcome}」");
            Req(st.HomeLost && garrison.Cleared && st.Won && st.Finished && !h.Cleared && !st.Fallen,
                "(e) 拠点が落ちた後に本陣の守り（重）を抜くと勝ち（勇者は生きている）");
            // 勇者が本陣のマスにいても、当たるのは守りが先・守りを抜けば制圧（逃げ切り）。
            var st2 = V1();
            for (int k = 0; k < 4 && !st2.HomeLost; k++) Tick(st2);
            var h2 = st2.Hero!;
            h2.Pos = st2.Graph.Portal;
            Map11State.Node g2 = st2.Nodes[1].Single(n => n.Def.Stationary);
            Walk(st2, 1, Dest.Of(st2.Graph, st2.Graph.Lanes[2][3]));
            Map11Orders.Apply(st2, 1, Dest.Portal(st2.Graph, 2), Map11Orders.Plan(st2, 1, Dest.Portal(st2.Graph, 2)));
            Map11Orders.Apply(st2, 1, Dest.Portal(st2.Graph, 2), Map11Orders.Plan(st2, 1, Dest.Portal(st2.Graph, 2)));
            Map11State.Node? first = st2.FoeFacing(1);
            bool firstGarrison = ReferenceEquals(first, g2);
            var order = new List<string>();
            for (int k = 0; k < 6 && !st2.Finished && st2.FoeFacing(1) is { } f; k++)
            {
                order.Add(f.IsHero ? "勇者" : f.Def.Name);
                if (f.IsHero) break;
                Win(st2, 1, f);
            }
            write($"  (e) 勇者が本陣に立っている: 第2隊が当たった順 {string.Join(" → ", order)} → 制圧 {st2.Captured}・勝ち {st2.Won}・勇者は本陣に {h2.Pos == st2.Graph.Portal}（倒した {h2.Cleared}）");
            Req(firstGarrison && !order.Contains("勇者") && st2.Won && !h2.Cleared, "(e) 勇者が本陣にいても守りが先（勇者は最後）・守りを抜けば制圧が先に成って勝ち（逃げ切り）");
        }

        // ---- (g) 勇者が現れる前の陥落は今と同じ ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, draft: true, stage: stage);   // 隊は 0 枚（拠点に出せる隊がいない）
            for (int k = 0; k < 10 && !st.Finished; k++) Tick(st);
            write($"  (g) 隊を出さない通し: 刻 {st.Turn} で 陥落 {st.Fallen}・拠点が落ちた（負けでない方） {st.HomeLost}・勇者 {(st.HeroTurn < 0 ? "まだ" : "現れた")}・終わり {st.Finished}");
            Req(st.Fallen && st.Finished && !st.HomeLost && st.HeroTurn < 0 && st.Turn == 3,
                "(g) 勇者が現れる前に空の拠点へ敵が着けば、今と同じく陥落で負け（刻 3）");
        }

        // ---- (h) 表示: 戦闘の記録と画面の作戦ターンが一致・期限 18 で 18 ----
        {
            Map11Session.Reset(0, draft: false, stage: stage);
            Map11State st = Map11Session.State!;
            int first = Map11Session.ShownTurn;
            bool match = true, advance = true;
            var seen = new List<string>();
            for (int guard = 0; guard < 40 && !st.Finished; guard++)
            {
                int before = Map11Session.ShownTurn;
                Map11Session.BeginTurn();
                advance &= Map11Session.ShownTurn == before;
                for (int step = 0; step < 60 && Map11Session.TurnRunning; step++)
                {
                    Map11Session.StepKind kind = Map11Session.StepTurn();
                    if (kind == Map11Session.StepKind.Intercept)
                    {
                        if (st.NextInterceptor(Map11Session.InterceptNode!) is var j && j >= 0 && Map11Session.ChooseInterceptor(j)) { }
                        else continue;
                    }
                    if (Map11Session.HasPendingBattle)
                    {
                        foreach (UnitState u in CampaignSession.CarriedEnemies!) u.Hp = 0;
                        CampaignSession.ClearCarriedBattle();
                        Map11Session.CompleteBattle(true);
                        var line = Map11Session.BattleLog[^1];
                        match &= line.Turn == Map11Session.ShownTurn && line.Turn == before;
                        if (seen.Count < 4) seen.Add($"T{line.Turn}{(line.Intercept ? "【迎撃】" : "")}（帯 {Map11Session.ShownTurn}）");
                        continue;
                    }
                    if (kind is Map11Session.StepKind.Done or Map11Session.StepKind.Fallen) break;
                }
                if (!st.Finished) advance &= Map11Session.ShownTurn == before + 1;
            }
            write($"  (h) 拠点で待ち続けた通し: 始め 作戦ターン {first} ・ 戦闘の記録 {string.Join(" ／ ", seen)} … ・ 終わり 作戦ターン {Map11Session.ShownTurn}（時間切れ {st.TimeUp}・内部の Turn {st.Turn}・記録 {Map11Session.BattleLog.Count} 行）");
            Req(first == 1 && match && advance && st.TimeUp && Map11Session.ShownTurn == stage.TurnCap && Map11Session.BattleLog.All(l => l.Turn >= 1 && l.Turn <= stage.TurnCap),
                "(h) 戦闘の記録の T と画面の作戦ターンが同じ刻で一致（1 から・作戦ターンの途中で動かない）・期限 18 の時間切れで「作戦ターン 18」");
            Map11Session.Reset(0, draft: true, stage: stage);
        }

        // ---- 報告のみ: 5 方針 × 100 seed（第458期の 1-3 と並べる） ----
        if (!bots) return ok;
        write("");
        write("  | 盤 | 方針 | 勝ち | 時間切れ | 陥落 | 全滅 | 拠点が落ちた | 勇者と戦った（平均） | 勇者を倒した | 勇者が本陣の連戦に加わった | 制圧の作戦ターン（中央値） | 戦闘（平均） |");
        write("  |---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        int crashed = 0;
        var policies = new[]
        {
            ("1 隊残す", true, false, false, Map12ChangeSmoke.HeroPlan.None),
            ("分担＋戻り番", true, true, false, Map12ChangeSmoke.HeroPlan.None),
            ("籠城", true, true, true, Map12ChangeSmoke.HeroPlan.None),
            ("守る", true, true, false, Map12ChangeSmoke.HeroPlan.Guard),
            ("逃げる", true, true, false, Map12ChangeSmoke.HeroPlan.Flee),
        };
        foreach (var (bname, board) in new[] { ("1-3 門番（第458期）", MapStage.Stage13H458), ("1-3 追う勇者（第459期）", stage) })
            foreach (var (pname, guard, split, turtle, plan) in policies)
            {
                if (!board.Hero.Chaser && plan != Map12ChangeSmoke.HeroPlan.None) continue;   // 門番の盤では守る・逃げるは分担＋戻り番と同じ
                int n = 0, won = 0, timeUp = 0, fallen = 0, wiped = 0, homeLost = 0, heroFights = 0, heroDown = 0, joined = 0, battles = 0;
                var capTurns = new List<int>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    bool counted = false;
                    try
                    {
                        Map12ChangeSmoke.Greedy(seed, board, guard, split, turtle, watch: q =>
                        {
                            if (!q.Finished || counted) return;
                            counted = true;
                            n++;
                            if (q.Won) won++;
                            else if (q.Fallen) fallen++;
                            else if (q.TimeUp) timeUp++;
                            else if (q.NoSquadsLeft) wiped++;
                            if (q.HomeLost) homeLost++;
                            heroFights += q.HeroBattles;
                            if (q.Hero is { Cleared: true }) heroDown++;
                            joined += q.HeroJoinedSiege;
                            if (q.Captured) capTurns.Add(q.CapturedTurn + 1);
                            battles += q.Battles;
                        }, siegeRest: true, heroPlan: plan);
                    }
                    catch (Exception ex) { crashed++; if (crashed <= 3) write($"  × {bname} {pname} seed {seed}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
                }
                if (n == 0) continue;
                double R(int k) => 100.0 * k / n;
                capTurns.Sort();
                write($"  | {bname} | {pname} | {R(won):F0} | {R(timeUp):F0} | {R(fallen):F0} | {R(wiped):F0} | {R(homeLost):F0} | {(double)heroFights / n:F2} | {R(heroDown):F0} | "
                    + $"{(double)joined / n:F2} | {(capTurns.Count == 0 ? "—" : capTurns[capTurns.Count / 2].ToString())} | {(double)battles / n:F1} |");
            }
        Req(crashed == 0, $"1-3（門番）× 3 方針と 1-3（追う勇者）× 5 方針を {Seeds} seed ずつ回して落ちない（数字は報告のみ・線は置かない）");
        return ok;
    }
}
