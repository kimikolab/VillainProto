using BattleCore;
using Godot;
using System.Linq;

// =====================================================================================
// 本陣の連戦の合間の窓（第457期 §0-1・`SiegeRule`）。**画面は判定を1つも持たない**——
// 「合間か」「次の相手」は `Map11Session.SiegeNode` ／ `Map11State.SiegeNext`、「引く」の行き先は `Map11State.SiegeRetreat` が決める。
// 時計は止まったまま（作戦ターンの途中）。「続けて戦う」は接敵の窓へ、「引く」は作戦ターンの続きを流す。
// =====================================================================================

public partial class Map11Main
{
    private Control _siegeOverlay = null!;
    private Label _siegeTitle = null!;
    private Label _siegeBody = null!;
    private Button _siegeGo = null!;
    private Button _siegeBack = null!;
    private Label _siegeTag = null!;
    private HBoxContainer _siegeFaces = null!;
    private ProgressBar _siegeHp = null!;
    /// <summary>今の隊と次の相手の枚数・HP（第478期 §4-4: 窓の上に大きく）。</summary>
    private Label _siegeStats = null!;

    private void BuildSiegeOverlay(Control root)
    {
        _siegeOverlay = Modal(root, 380, 285, out VBoxContainer col, "SIEGE ・ 本陣の連戦", EnemyColor);
        _siegeTag = (Label)col.GetChild(0);   // 第458期: 大群のマスでは「〈マス〉の連戦」に替える
        _siegeTitle = UiKit.Text("", 22, Colors.White);
        _siegeTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_siegeTitle);
        _siegeFaces = new HBoxContainer();
        _siegeFaces.AddThemeConstantOverride("separation", 45);
        _siegeFaces.CustomMinimumSize = new Vector2(0, 130);
        col.AddChild(_siegeFaces);
        _siegeStats = UiKit.Text("", 21, Color.FromHtml("#ffe9a8"));
        _siegeStats.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_siegeStats);
        _siegeHp = new ProgressBar { CustomMinimumSize = new Vector2(0, 30), ShowPercentage = true };
        _siegeHp.AddThemeStyleboxOverride("fill", UiKit.Box(PlayerColor, PlayerColor));
        col.AddChild(_siegeHp);
        _siegeBody = UiKit.Text("", 14, Color.FromHtml("#ccd5cb"));
        _siegeBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _siegeBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_siegeBody);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);
        _siegeGo = UiKit.ActionButton("続けて戦う", GoldColor);
        _siegeGo.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _siegeGo.CustomMinimumSize = new Vector2(0, 58);
        _siegeGo.Pressed += SiegeContinue;
        row.AddChild(_siegeGo);
        _siegeBack = UiKit.ActionButton("引く", PlayerColor);   // 第478期 §4-4: 「続けて戦う」と同じ大きさ・同じ強さの色
        _siegeBack.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _siegeBack.CustomMinimumSize = new Vector2(0, 58);
        _siegeBack.Pressed += SiegeRetreat;
        row.AddChild(_siegeBack);
    }

    /// <summary>連戦の合間の窓を出す（`StepTurn` が `Siege` で止まった）。</summary>
    private void ShowSiege()
    {
        if (Map11Session.SiegeNode is not { } next || Map11Session.SiegeSquad < 0) { Pump(); return; }
        int squad = Map11Session.SiegeSquad;
        Map11State.Squad s = St.Squads[squad];
        _selected = squad;
        ShowFoe(next);
        int road = s.Road >= 0 && s.Road < St.Stage.LaneCount ? s.Road : 0;
        // 第458期: 本陣のほか、敵が重なったマス（1-3 の大群）でも出る。引く先は本陣なら経路の奥、大群なら入ってきた側の隣（`SiegeRetreat` と同じ式）。
        bool portal = s.Pos == St.Graph.Portal;
        string where = portal ? "本陣" : PlaceName(s.Pos);
        int cell = portal ? St.Graph.Lanes[road][^1] : St.Graph.TowardHome(road, s.Pos);
        bool held = St.HeldOutposts.Contains(cell);
        int alive = s.Units?.Count(u => u.IsAlive) ?? 0;
        int foeAlive = next.Units?.Count(u => u.IsAlive) ?? next.Def.Enemy.Occupied().Count();
        int after = portal ? (St.Portal.On ? CountPortalFoes() : 0)
            : Enumerable.Range(0, St.Stage.LaneCount).Sum(r => St.FoesOn(r).Count(n => n.Pos == s.Pos));
        foreach (Node child in _siegeFaces.GetChildren()) { _siegeFaces.RemoveChild(child); child.QueueFree(); }
        var own = new MapUnitBadge { MouseFilter = Control.MouseFilterEnum.Ignore };
        SetBadge(own, s); _siegeFaces.AddChild(own);
        _siegeFaces.AddChild(new MapNoRecovery { CustomMinimumSize = new Vector2(62, 94), TooltipText = "戦闘後の回復なし" });
        var enemy = new MapUnitBadge { MouseFilter = Control.MouseFilterEnum.Ignore };
        SetBadge(enemy, next); enemy.SetStaging(next.IsHero, after); _siegeFaces.AddChild(enemy);
        Label remaining = UiKit.Text($"× {after}", 36, GoldColor);
        remaining.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _siegeFaces.AddChild(remaining);
        _siegeHp.Value = SquadHpPercent(s);
        _siegeTag.Text = portal ? "SIEGE ・ 本陣の連戦" : $"SIEGE ・ {where}の連戦";
        _siegeTitle.Text = $"{s.Label} が{where}で勝った——次は {(next.IsHero ? "勇者" : next.Def.Name)}";
        _siegeStats.Text = $"{s.Label}　{alive} 枚 ・ HP {SquadHpPercent(s):F0}%\n次の相手　{(next.IsHero ? "勇者" : next.Def.Name)}　{foeAlive} 体 ・ HP {NodeHpPercent(next):F0}%"
            + (after > 1 ? $"（{where}に残る敵 {after}）" : "");
        if (portal && St.Stage.SiegeRetreatToOutpost && St.Graph.Lanes[road].Where(c => St.HeldOutposts.Contains(c)).Select(c => (int?)c).LastOrDefault() is { } heldCell)
        { cell = heldCell; held = true; }   // 引く先の表示は `SiegeRetreat` と同じ式（取っている中間拠点へ引く盤では、その中間拠点）
        _siegeBody.Text =
            HeroWaitLines(s, next)
            + $"引く: {PlaceName(cell)}へ戻る（無料・時間はかからない）。"
            + (held ? "味方の中間拠点なので、組み直し・呼ぶ・休むができる。" : St.Graph.IsOutpost(cell) ? "敵のものなので組み直しはできない。" : "")
            + $" {where}の敵はそのまま（HP は戻らない）。";
        _siegeGo.Text = $"続けて戦う（{(next.IsHero ? "勇者" : next.Def.Name)}）";
        _siegeBack.Text = St.Stage.OutpostRewards is not null ? $"引いて待つ（{PlaceName(cell)}へ）" : $"引く（{PlaceName(cell)}へ）";
        _siegeHp.Visible = _siegeBody.Visible = true;
        _siegeOverlay.Visible = true;
        if (Map3DMode.Active) { ShowClash(ClashMode.Siege); if (next.Def.Name == MapStage.KeeperName) SayOnce(Map11Lines.Keeper12); }   // 第479期の追記: 3D は遭遇の台
    }

    /// <summary>
    /// 第477期 §3-b: 次が勇者のとき、ほかの隊の枚数・HP・位置（本陣まで あと n 刻）を出す（新しい 1-2 の盤だけ・<b>表示だけ</b>）。
    /// 「引いて待つ」を選びやすくするための行で、判定は持たない。
    /// </summary>
    private string HeroWaitLines(Map11State.Squad s, Map11State.Node next)
    {
        bool keeper = next.Def.Name == MapStage.KeeperName;
        if ((!next.IsHero && !keeper) || St.Stage.OutpostRewards is null) return "";
        var lines = new System.Collections.Generic.List<string>();
        foreach (Map11State.Squad q in St.Squads)
        {
            if (q.Index == s.Index || St.IsLocked(q.Index) || q.Lost || q.Units is not { } us || !us.Any(u => u.IsAlive)) continue;
            int n = us.Count(u => u.IsAlive);
            string at = q.Pos == St.Graph.Portal ? "本陣にいる（続けて当てられる）"
                : St.Graph.Path(q.Pos, q.Via, St.Graph.Portal, -1) is { Count: > 1 } path ? $"{PlaceName(q.Pos)}（本陣まで あと {path.Count - 1} 刻）" : PlaceName(q.Pos);
            lines.Add($"{q.Label}: {n} 枚 ・ HP {SquadHpPercent(q):F0}% ・ {at}");
        }
        return (keeper ? "砦の守将は硬い。削られたら中間拠点へ引いて休める（守将の傷は残る）。\n"
                       : "門番は 2 隊で順に当てて削る相手（勇者の HP は戻らない）。もう 1 隊がまだなら、引いて待てる。\n")
               + (lines.Count == 0 ? "ほかに動ける隊はいない。\n\n" : string.Join("\n", lines) + "\n\n");
    }

    /// <summary>敵の拠点のマスにいる敵部隊の数（窓の 1 行だけが読む）。</summary>
    private int CountPortalFoes()
        => St.Nodes.SelectMany(r => r).Count(n => !n.Cleared && n.Pos == St.Graph.Portal)
           + St.Spawns.Count(n => !n.Cleared && n.Pos == St.Graph.Portal)
           + (St.ReserveWaiting ? 1 : 0);

    private void SiegeContinue()
    {
        _siegeOverlay.Visible = false;
        // 第479期（Codex の試遊 2026-11-16）: 3D では「続けて戦う」を押したらそのまま開戦する（同じ相手にもう 1 度「戦う」を押させない）。
        if (Map11Session.ContinueSiege()) { Refresh(); if (Map3DMode.Active) Fight(); else ShowEncounter(); return; }
        Pump();
    }

    private void SiegeRetreat()
    {
        _siegeOverlay.Visible = false;
        int squad = Map11Session.SiegeSquad;
        string label = squad >= 0 ? St.Squads[squad].Label : "";
        if (Map11Session.RetreatSiege())
            ShowToast($"{label} は{(_siegeTag.Text.Contains("本陣") ? "本陣" : "連戦")}から{St.Graph[St.Squads[squad].Pos].Name}へ引いた", GoldColor, 4.0f);
        Refresh();
        Pump();
    }
    /// <summary>
    /// 敵の拠点のマスに出ている敵の札が、すべて画面の内側に収まっているか（第457期 (bl)・絵の口 `hero` ／ `siege` が出す）。
    /// </summary>
    private string PortalCardsInside()
    {
        Rect2 view = GetViewport().GetVisibleRect().Grow(-8);
        int n = 0, inside = 0;
        foreach (Marker m in _spawnMarkers.Concat(_nodeMarkers))
        {
            if (!m.Shown) continue;
            Vector3 p = m.Root.Position;
            if (p.DistanceTo(PortalPos) > 8.5f) continue;   // 敵の拠点のまわりの札だけ
            n++;
            if (view.HasPoint(_camera.UnprojectPosition(p))) inside++;
        }
        return $"cards={n} inside={inside == n}";
    }

    /// <summary>
    /// 第457期 §3: **本陣の連戦の合間の窓**の絵（`--map-stage=1-2` と使う）。北の砦・野営地を取って門を開け（勇者が本陣の前に立つ）、
    /// 第1隊の行き先を本陣にして作戦ターンを流し、守りとの戦闘を頭なしで決着させて（台本つき）、連戦の窓で止める。
    /// </summary>
    /// <param name="horde">第458期: 1-3 の大群の連戦の窓（`--map-stage=1-3` と `,horde`）。第1隊を中の道の野営地まで戦わせて進め、行き先を大群のマス（C3）にする。</param>
    private void CaptureSiegeSetup(bool horde = false)
    {
        St.UseDefaultSquads();
        _reform = false;
        ClearHold();
        Map11Session.TakeOpening();
        void Fight(int squad, Map11State.Node node, bool intercept)
        {
            var prep = intercept ? St.PrepareIntercept(squad, node) : St.Prepare(squad, node);
            if (prep is not { } q) return;
            BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: true);
            St.Resolve(squad, q.Node, r.PlayerWon, intercept, r.Turns);
        }
        void Go(int squad, Dest to)
        {
            for (int guard = 0; guard < 12 && St.Squads[squad].Pos != to.Id && !St.Finished; guard++)
            {
                if (St.FoeFacing(squad) is { } here) { Fight(squad, here, false); continue; }
                if (Map11Orders.Apply(St, squad, to, Map11Orders.Plan(St, squad, to)) is { } met) Fight(squad, met, false);
            }
            while (St.FoeFacing(squad) is { } here && !St.Finished) Fight(squad, here, false);
        }
        if (horde) Go(0, Dest.Of(St.Graph, St.Graph.Lanes[1][3]));
        else
        {
            Go(0, Dest.Of(St.Graph, St.Graph.Lanes[0][^1]));
            Go(1, Dest.Of(St.Graph, St.Graph.Lanes[1][^1]));
        }
        for (int guard = 0; guard < 6 && !horde && St.Hero is null && !St.Finished; guard++)
            foreach (Map11State.Encounter e in St.AdvanceFoes())
                if (e.Intercept) { if (St.NextInterceptor(e.Node) is var j && j >= 0) Fight(j, e.Node, true); St.CloseIntercept(e.Node); }
                else Fight(e.Squad, e.Node, false);
        while (St.RelicOffer.Count > 0) St.ChooseRelic(0);   // 中間拠点の候補の窓は先に閉じる（絵が窓どうしで重ならないように）
        Map11Session.SetDestination(0, horde ? Dest.Of(St.Graph, St.Graph.Lanes[1][4]) : Dest.Portal(St.Graph, 0));
        Map11Session.SetDestination(1, horde ? Dest.Home : Dest.Of(St.Graph, St.Graph.Lanes[1][^1]));
        bool siege = false;
        for (int guard = 0; guard < 6 && !siege && !St.Finished; guard++)
        {
            if (!Map11Session.TurnRunning) Map11Session.BeginTurn();
            for (int step = 0; step < 20 && Map11Session.TurnRunning && !siege; step++)
            {
                Map11Session.StepKind kind = Map11Session.StepTurn();
                if (kind == Map11Session.StepKind.Siege) { siege = true; break; }
                if (kind == Map11Session.StepKind.Battle)
                {
                    BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: true);
                    CampaignSession.ClearCarriedBattle();
                    Map11Session.CompleteBattle(r.PlayerWon, r);
                    continue;
                }
                if (kind == Map11Session.StepKind.Intercept)
                {
                    if (St.NextInterceptor(Map11Session.InterceptNode!) is var j && j >= 0 && Map11Session.ChooseInterceptor(j))
                    {
                        BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: true);
                        CampaignSession.ClearCarriedBattle();
                        Map11Session.CompleteBattle(r.PlayerWon, r);
                        continue;
                    }
                    break;
                }
                if (kind is Map11Session.StepKind.Done or Map11Session.StepKind.Fallen) break;
            }
        }
        Refresh();
        if (siege) ShowSiege();
        GD.Print($"MAP11_CAPTURE_SIEGE seed={Map11Session.Seed} siege={siege} turn={St.Turn} squad={Map11Session.SiegeSquad} next={(Map11Session.SiegeNode is { } nx ? (nx.IsHero ? "勇者" : nx.Def.Name) : "-")} "
                 + $"hp={(Map11Session.SiegeSquad >= 0 ? SquadHpPercent(St.Squads[Map11Session.SiegeSquad]) : 0):F0}");
    }
}
