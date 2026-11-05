using BattleCore;
using Godot;
using System.Linq;

public partial class Map11Main
{
    /// <summary>
    /// 第459期 §7 —— 追ってくる勇者の絵（`--map-stage=1-3` と使う・`--map11-capture=&lt;path&gt;,chase` ／ `,chase-lost`）。
    /// 既定の組み方で 2 隊を組み、北と南の道の敵と中の道の物見・火の部隊を抜いた体にして、第1隊を北の砦・第2隊を南の関所へ歩かせる
    /// （中間拠点 2 つで門が開く → 勇者が中の道の拠点から 3 歩のマスに現れる）。戦闘は画面と同じ口（`Map11Session.CompleteBattle`）で頭なしに決着させる。
    /// <c>chase</c> は勇者が現れた刻で、<c>chase-lost</c> は拠点が落ちた刻で止める——<b>止まるのは画面の時計と同じ `StopReason`</b>（出来事で時計が止まることの確かめを兼ねる）。
    /// </summary>
    private void CaptureChaseSetup(bool lost)
    {
        St.UseDefaultSquads();
        _reform = false;
        ClearHold();
        Map11Session.TakeOpening();
        foreach (Map11State.Node n in St.Nodes.SelectMany(r => r))
            if (!n.Def.Stationary && !n.Def.Hold) n.Cleared = true;
        Map11Session.SetDestination(0, Dest.Of(St.Graph, St.Graph.Lanes[0][3]));
        Map11Session.SetDestination(1, Dest.Of(St.Graph, St.Graph.Lanes[2][3]));
        string? stop = null;
        for (int guard = 0; guard < 12 && stop is null && !St.Finished; guard++)
        {
            Watch before = Take();
            Map11Session.BeginTurn();
            for (int step = 0; step < 30 && Map11Session.TurnRunning; step++)
            {
                Map11Session.StepKind kind = Map11Session.StepTurn();
                if (kind == Map11Session.StepKind.Intercept
                    && !(St.NextInterceptor(Map11Session.InterceptNode!) is var j && j >= 0 && Map11Session.ChooseInterceptor(j))) break;
                if (Map11Session.HasPendingBattle)
                {
                    BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: true);
                    CampaignSession.ClearCarriedBattle();
                    Map11Session.CompleteBattle(r.PlayerWon, r);
                    continue;
                }
                if (kind is Map11Session.StepKind.Done or Map11Session.StepKind.Fallen) break;
            }
            if (StopReason(before) is { } why && (lost ? !before.HomeLost && St.HomeLost : before.HeroTurn < 0 && St.HeroTurn >= 0))
            {
                stop = why;
                GuideEvent(before);
            }
        }
        while (St.RelicOffer.Count > 0) St.ChooseRelic(0);   // 中間拠点の札の窓は閉じる（絵が窓どうしで重ならないように・`CaptureSiegeSetup` と同じ）
        Refresh();
        if (stop is not null) ShowToast(stop, GoldColor, 30f);
        GD.Print($"MAP11_CAPTURE_CHASE lost={lost} stopped={stop is not null} turn={Map11Session.ShownTurn} heroTurn={St.HeroTurn} "
                 + $"hero={(St.Hero is { } h ? St.Graph[h.Pos].Name + "/" + St.LaneName(h.Def.Road) : "-")} homeLost={St.HomeLost} fallen={St.Fallen} "
                 + $"chase={(St.HeroChase() is { } c ? St.Squads[c.Squad].Label + ":" + c.Steps : "-")} why={stop}");
    }
}
