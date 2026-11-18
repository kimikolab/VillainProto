using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// =====================================================================================
// 1-3「狩る者」の場面の写しと検査（第480期・指示書 §5 の「3D のレイアウトの検査に 1-3 の場面」・§7 のレビューの写し）。
//
//   Godot_console.exe --path DemoApp res://Map11Main.tscn -- --map3d --chapter1-rule=V480 --map-chapter-from=<1-3 の始めの写し>
//                     "--map11-capture=<置き場>/h.png,m3d-h-all"
//
// 1 回の起動で、始め → 門が開く直前 → 登場（落雷・扉・歩み出る・帯・セリフ 2 行・目標）→ 追われている → 殿の会戦の台 → 殿の結果 → 生還 → 結果の窓
// を順に撮り（`h_01_始め.png` …）、場面ごとに「出ているはずのもの」を確かめる。合否は `MAP3D_HUNTER_SCENES_COMPLETE ok=` の行。
// 盤は器具が進める（戦闘は頭なしで決着）。登場の演出だけは画面の本物の流れ（`HunterEntrance`）をそのまま流す。
// =====================================================================================
public partial class Map11Main
{
    private async Task HunterScenes(string basePath)
    {
        var fails = new List<string>();
        int shot = 0;
        string stem = basePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? basePath[..^4] : basePath;
        async Task Shot(string name)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            string path = $"{stem}_{++shot:00}_{name}.png";
            Error e = GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"MAP3D_HUNTER_SHOT {name} error={e} turn={Map11Session.ShownTurn} beat={HunterBeat} path={path}");
        }
        async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
        void Req(bool ok, string what) { GD.Print($"MAP3D_HUNTER_CHECK {(ok ? "○" : "×")} {what}"); if (!ok) fails.Add(what); }

        MapGraph g = St.Graph;
        var order = Enumerable.Range(0, St.SquadCap).Where(i => St.Squads[i].Units is { Count: > 0 }).OrderByDescending(i => St.Squads[i].Units!.Count).ToList();
        int a = order[0], b = order.Count > 1 ? order[1] : order[0];
        int bridge = g.Lanes[1].First(c => g.IsOutpost(c)), village = g.Lanes[2].First(c => g.IsOutpost(c));
        bool Alive(int i) => !St.Squads[i].Lost && St.Squads[i].Units is { } u && u.Any(x => x.IsAlive);

        // ---- 1) 始め ----
        await Wait(1.2);
        Req(_layout3d?.Sheet is { RuinedCamp: true, HomeStone: true }, "始め: 1-3 の表（石の砦の拠点・残党の野営地）で建っている");
        Req(_hud3d!.GoalText.Contains("中間拠点"), $"始め: 目標の札は「中間拠点を取る」（{_hud3d.GoalText}）");
        Req(MapMusic.Current == MapMusic.Track.Normal, $"始め: 曲の口は通常（{MapMusic.Current}）");
        await Shot("始め");

        // ---- 2) 門が開く直前（中間拠点を 1 つ取った所・野営地の門は閉じている） ----
        for (int k = 0; k < 10 && St.HeldOutposts.Count < 1 && !St.Finished; k++)
        {
            if (Alive(a)) Map11Session.SetDestination(a, Dest.Of(g, bridge));
            if (Alive(b)) Map11Session.SetDestination(b, Dest.Of(g, village));
            HeadlessTurn3D();
        }
        while (St.RelicOffer.Count > 0) St.ChooseRelic(0);
        Refresh();
        _cam3d!.FocusOn(PosOf(g.Portal) + new Vector3(-6, 0, 0), snap: true);
        await Wait(0.8);
        Req(St.HeldOutposts.Count >= 1 && !St.Rout, $"門が開く直前: 中間拠点を 1 つ取った（{St.HeldOutposts.Count}・{Map11Session.ShownTurn} 刻目）");
        Req(_map3d!.CampGate is not null && !_map3d.GateBlown && St.Hero is null, "門が開く直前: 野営地の門の扉は閉じていて、勇者はいない");
        await Shot("門が開く直前");

        // ---- 3) 登場（画面の本物の流れ） ----
        for (int k = 0; k < 12 && !St.Rout && !St.Finished; k++)
        {
            if (Alive(a)) Map11Session.SetDestination(a, Dest.Of(g, bridge));
            if (Alive(b)) Map11Session.SetDestination(b, Dest.Of(g, village));
            HeadlessTurn3D();
        }
        while (St.RelicOffer.Count > 0) St.ChooseRelic(0);
        Req(St.Rout && St.Hero is { Cleared: false }, $"登場: 門が開いた刻の終わりに勇者が立った（{Map11Session.ShownTurn} 刻目・中間拠点 {St.HeldOutposts.Count}）");
        if (!St.Rout) { Finish(); return; }
        _hunterForce = true;
        Map11Clock.Start();
        Refresh();
        var beats = new (string Beat, double After, string Name)[]
        {
            ("落雷", 0.20, "登場_落雷"), ("扉", 0.13, "登場_扉が吹き飛ぶ"), ("歩み出る", 0.75, "登場_歩み出る"), ("帯", 0.6, "登場_帯"),
            ("セリフ", 0.5, "登場_セリフ1"), ("セリフ", 3.0, "登場_セリフ2"), ("目標", 0.4, "登場_目標の札"),
        };
        var seen = new List<string>();
        string bandAtBeat = "";
        bool hiddenBefore = HeroPiece() is not { Shown: true };
        foreach (var (beat, after, name) in beats)
        {
            for (int f = 0; f < 3000 && HunterBeat != beat && HunterBeat != "終わり"; f++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (HunterBeat != beat) continue;
            if (!seen.Contains(beat)) seen.Add(beat);
            await Wait(after);
            if (beat == "帯") bandAtBeat = _hunterOver!.BandText;
            await Shot(name);
        }
        for (int f = 0; f < 6000 && HunterBusy; f++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Wait(0.6);
        Req(seen.SequenceEqual(new[] { "落雷", "扉", "歩み出る", "帯", "セリフ", "目標" }), $"登場: 演出が順に流れた（{string.Join(" → ", seen)}）");
        Req(hiddenBefore, "登場: 勇者の駒は、歩み出るまで盤に出ない");
        Req(bandAtBeat == Map11Lines.HunterName, $"登場: 帯は「{Map11Lines.HunterName}」（{bandAtBeat}）");
        Req(_map3d.GateBlown && HeroPiece() is { Shown: true }, "登場: 門の扉は吹き飛び、勇者の駒が出ている");
        Req(!HunterBusy && !Map11Clock.Running, "登場: 終わったら入力が戻り、時計は止まっている（押すまで刻まない）");
        Req(_hud3d.GoalAlert && _hud3d.GoalText == Map11Lines.GoalRout, $"登場: 目標の札が赤い「{Map11Lines.GoalRout}」（{_hud3d.GoalText}）");
        Req(MapMusic.Current == MapMusic.Track.Hero, $"登場: 曲の口は緊迫（{MapMusic.Current}・ファイル {(MapMusic.Has(MapMusic.Track.Hero) ? "あり" : "なし——黙る")}）");
        Req(_portalPlate3d is not { Visible: true }, "登場: 敵の拠点の名札は消えている");
        Req(HunterBarShown, "登場: 勇者の HP の棒が画面の上に出ている");
        await Shot("登場_終わり");

        // ---- 4) 追われている（全員まっすぐ帰る 1 刻） ----
        foreach (int i in order) if (Alive(i)) Map11Session.SetDestination(i, Dest.Home);
        int heroBefore = St.Hero!.Pos;
        HeadlessTurn3D();
        Refresh();
        _cam3d.FocusOn(PosOf(St.Hero.Pos) + new Vector3(-8, 0, 0), snap: true);
        await Wait(1.0);
        Req(St.Hero.Pos != heroBefore || St.HunterCatches > 0, $"追われている: 勇者が歩いた（{PlaceName(heroBefore)} → {PlaceName(St.Hero.Pos)}・追いついた {St.HunterCatches}）");
        Req(St.Finished || HunterDotCount > 0, $"追われている: 勇者の進む先の赤い点線（{HunterDotCount} 個）");
        string tags = string.Join(" ／ ", order.Select(i => $"{St.Squads[i].Label}:{_hud3d.CardTag(order.IndexOf(i) < 0 ? 0 : St.Squads.Take(i + 1).Count(q => !St.IsLocked(q.Index)) - 1)}"));
        await Wait(1.2);   // 足止めの印の裏の数えを待つ
        tags = string.Join(" ／ ", St.Squads.Where(q => !St.IsLocked(q.Index)).Select((q, n) => $"{q.Label}:{_hud3d.CardTag(n)}"));
        Req(St.Finished || St.Squads.Where(q => !St.IsLocked(q.Index)).Select((q, n) => (q, n)).Any(x => Alive(x.q.Index) && x.q.Pos != g.Home && _hud3d.CardTag(x.n).Length > 0),
            $"追われている: 拠点の外の隊の札に足止めの印（{tags}）");
        GD.Print($"MAP3D_HUNTER_STATE 目の印 {HunterEyeShown} ・ 生還の札 {HunterHomeMarks} ・ 札の印 {tags}");
        await Shot("追われている");

        // ---- 5) 殿（勇者にいちばん近い隊が、自分から勇者のマスへ入る）: 会戦の台 → 結果の帯 ----
        var outside = order.Where(i => Alive(i) && St.Squads[i].Pos != g.Home).OrderByDescending(i => St.Squads[i].Units!.Count(u => u.IsAlive)).ToList();
        if (!St.Finished && outside.Count > 0 && St.Hero.Pos != g.Portal)
        {
            int rear = outside[0];
            int holds = St.HunterHolds, catches = St.HunterCatches;
            bool stopped = false, vsHero = false;
            for (int k = 0; k < 4 && !vsHero && !St.Finished && Alive(rear); k++)
            {
                foreach (int i in order) if (Alive(i) && i != rear) Map11Session.SetDestination(i, Dest.Home);
                Map11Session.SetDestination(rear, Dest.Of(g, St.Hero.Pos));
                holds = St.HunterHolds;
                stopped = TickOnce();
                for (int f = 0; f < 600 && ClashShown && ClashOddsText == "…"; f++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await Wait(0.5);
                vsHero = ClashShown && Map11Session.PendingNode is { IsHero: true };
                if (vsHero) { rear = Map11Session.PendingSquad; break; }
                // 勇者でない相手との会戦・ほかの出来事は、頭なしで片付けて次の刻へ。
                for (int n = 0; n < 40 && Map11Session.TurnRunning && !St.Finished; n++)
                {
                    if (Map11Session.HasPendingBattle)
                    {
                        BattleResult r0 = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: false);
                        CampaignSession.ClearCarriedBattle();
                        Map11Session.CompleteBattle(r0.PlayerWon, r0);
                    }
                    Map11Session.StepKind kind0 = Map11Session.StepTurn();
                    if (kind0 is Map11Session.StepKind.Done or Map11Session.StepKind.Fallen) break;
                }
                _encounterOverlay.Visible = false;
                Refresh();
            }
            Req(stopped && vsHero, $"殿: 勇者との会戦の台が出た（{St.Squads[rear].Label}・{(Map11Session.FoePhase ? "追いつかれた" : "自分から入った")}）");
            if (vsHero)
            {
                Req(!ClashBackShown, "殿: 勇者との会戦に「引き返す」は無い");
                Req(ClashHoldText.Length > 0, $"殿: 台に足止めの印（{ClashHoldText}）・勝ち目 {ClashOddsText}・セリフ {ClashSayText}");
                Req(HunterBarShown, "殿: 勇者の HP の棒は台のあいだも出ている");
                await Shot("殿_会戦の台");
                // 戦闘は頭なしで決着させ、同じ台の上の結果の帯を見る。
                RememberClash();
                _encounterOverlay.Visible = false;
                int hpBefore = St.Hero.HpNow;
                BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: false);
                CampaignSession.ClearCarriedBattle();
                Map11Session.CompleteBattle(r.PlayerWon, r);
                Refresh();
                AfterBattle3D();
                await Wait(0.4);
                bool held = St.HunterHolds > holds;
                GD.Print($"MAP3D_HUNTER_STATE 殿の戦闘 {r.Turns} ターン・勝ち {r.PlayerWon}・止めた {held}・勇者 HP {hpBefore} → {St.Hero.HpNow}・帯「{ClashBandText}」");
                Req(St.Finished || ClashBandText == (held ? Map11Lines.RearBand : r.PlayerWon ? "勝ち" : "負け"), $"殿: 結果の帯（{ClashBandText}・{r.Turns} ターン・止めた {held}）");
                Req(St.Hero.HpNow < hpBefore, $"殿: 勇者の HP が減った（{hpBefore} → {St.Hero.HpNow}）");
                await Shot("殿_結果の帯");
                await Wait(2.2);
                for (int k = 0; k < 40 && Map11Session.TurnRunning && !St.Finished; k++)
                {
                    Map11Session.StepKind kind = Map11Session.StepTurn();
                    if (kind == Map11Session.StepKind.Battle)
                    {
                        BattleResult r2 = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: false);
                        CampaignSession.ClearCarriedBattle();
                        Map11Session.CompleteBattle(r2.PlayerWon, r2);
                    }
                    else if (kind is Map11Session.StepKind.Done or Map11Session.StepKind.Fallen) break;
                }
                _encounterOverlay.Visible = false;
            }
            else if (ClashShown) { _encounterOverlay.Visible = false; }
        }
        else GD.Print("MAP3D_HUNTER_STATE 殿の場面は作れなかった（拠点の外に隊がいない ／ 勇者がまだ門の前）");

        // ---- 6) 生還（残りは全員まっすぐ帰る） ----
        bool homeMark = false;
        for (int k = 0; k < 12 && !St.Finished; k++)
        {
            foreach (int i in order) if (Alive(i)) Map11Session.SetDestination(i, Dest.Home);
            HeadlessTurn3D();
            Refresh();
            await Wait(0.25);
            if (!homeMark && !St.Finished && HunterHomeMarks > 0)
            {
                homeMark = true;
                _cam3d.FocusOn(PosOf(g.Home) + new Vector3(8, 0, 0), snap: true);
                await Wait(0.5);
                await Shot("生還の札");
            }
        }
        Req(St.Finished, $"生還: 撤退の局面が終わった（{Map11Session.ShownTurn} 刻目・勝ち {St.Won}・生還 {St.HunterSurvivors}／{St.HunterStartUnits}・退かせた {St.HunterDriven}・全滅 {St.HunterLost}）");
        // ---- 7) 結果の窓 ----
        if (St.Finished)
        {
            Hs.Routed = true;
            ShowResult();
            await Wait(0.8);
            Req(_resultOverlay.Visible && _resultBody.Text.Contains("生還した駒"), $"結果の窓: 戦果（{_resultTitle.Text} ／ {_resultBody.Text.Replace("\n", " ・ ")}）");
            await Shot("結果の窓");
        }
        Finish();

        void Finish()
        {
            foreach (string f in fails) GD.Print("MAP3D_HUNTER_FAIL " + f);
            GD.Print($"MAP3D_HUNTER_SCENES_COMPLETE ok={fails.Count == 0} failed={fails.Count} shots={shot} stage={St.Stage.Id} lanes={string.Join("/", St.Graph.Lanes.Select(l => l.Length))}");
            GetTree().Quit(fails.Count == 0 ? 0 : 1);
        }
    }
}
