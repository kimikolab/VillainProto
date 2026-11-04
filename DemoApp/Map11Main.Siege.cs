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

    private void BuildSiegeOverlay(Control root)
    {
        _siegeOverlay = Modal(root, 360, 130, out VBoxContainer col, "SIEGE ・ 本陣の連戦", EnemyColor);
        _siegeTitle = UiKit.Text("", 22, Colors.White);
        _siegeTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_siegeTitle);
        _siegeBody = UiKit.Text("", 14, Color.FromHtml("#ccd5cb"));
        _siegeBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _siegeBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_siegeBody);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);
        _siegeGo = UiKit.ActionButton("続けて戦う", GoldColor);
        _siegeGo.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _siegeGo.Pressed += SiegeContinue;
        row.AddChild(_siegeGo);
        _siegeBack = UiKit.ActionButton("引く", UiKit.Muted);
        _siegeBack.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
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
        int cell = St.Graph.Lanes[road][^1];
        bool held = St.HeldOutposts.Contains(cell);
        int alive = s.Units?.Count(u => u.IsAlive) ?? 0;
        int foeAlive = next.Units?.Count(u => u.IsAlive) ?? next.Def.Enemy.Occupied().Count();
        int after = St.Portal.On ? CountPortalFoes() : 0;
        _siegeTitle.Text = $"{s.Label} が本陣で勝った——次は {(next.IsHero ? "勇者" : next.Def.Name)}";
        _siegeBody.Text =
            $"{s.Label}: {alive} 枚 ・ HP {SquadHpPercent(s):F0}%（本陣では戦闘の後の回復は乗らない）\n"
            + $"{(next.IsHero ? "勇者" : next.Def.Name)}: {foeAlive} 体 ・ HP {NodeHpPercent(next):F0}%"
            + (after > 1 ? $"（本陣に残る敵 {after}）" : "") + "\n\n"
            + $"引く: {St.Graph[cell].Name}へ戻る（無料・時間はかからない）。"
            + (held ? "味方の中間拠点なので、組み直し・呼ぶ・休むができる。" : "敵のものなので組み直しはできない。")
            + " 本陣の敵はそのまま（HP は戻らない）。";
        _siegeGo.Text = $"続けて戦う（{(next.IsHero ? "勇者" : next.Def.Name)}）";
        _siegeBack.Text = $"引く（{St.Graph[cell].Name}へ）";
        _siegeOverlay.Visible = true;
    }

    /// <summary>敵の拠点のマスにいる敵部隊の数（窓の 1 行だけが読む）。</summary>
    private int CountPortalFoes()
        => St.Nodes.SelectMany(r => r).Count(n => !n.Cleared && n.Pos == St.Graph.Portal)
           + St.Spawns.Count(n => !n.Cleared && n.Pos == St.Graph.Portal)
           + (St.ReserveWaiting ? 1 : 0);

    private void SiegeContinue()
    {
        _siegeOverlay.Visible = false;
        if (Map11Session.ContinueSiege()) { Refresh(); ShowEncounter(); return; }
        Pump();
    }

    private void SiegeRetreat()
    {
        _siegeOverlay.Visible = false;
        int squad = Map11Session.SiegeSquad;
        string label = squad >= 0 ? St.Squads[squad].Label : "";
        if (Map11Session.RetreatSiege())
            ShowToast($"{label} は本陣から{St.Graph[St.Squads[squad].Pos].Name}へ引いた", GoldColor, 4.0f);
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
    private void CaptureSiegeSetup()
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
            St.Resolve(squad, q.Node, r.PlayerWon, intercept);
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
        Go(0, Dest.Of(St.Graph, St.Graph.Lanes[0][^1]));
        Go(1, Dest.Of(St.Graph, St.Graph.Lanes[1][^1]));
        for (int guard = 0; guard < 6 && St.Hero is null && !St.Finished; guard++)
            foreach (Map11State.Encounter e in St.AdvanceFoes())
                if (e.Intercept) { if (St.NextInterceptor(e.Node) is var j && j >= 0) Fight(j, e.Node, true); St.CloseIntercept(e.Node); }
                else Fight(e.Squad, e.Node, false);
        while (St.RelicOffer.Count > 0) St.ChooseRelic(0);   // 中間拠点の候補の窓は先に閉じる（絵が窓どうしで重ならないように）
        Map11Session.SetDestination(0, Dest.Portal(St.Graph, 0));
        Map11Session.SetDestination(1, Dest.Of(St.Graph, St.Graph.Lanes[1][^1]));
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
