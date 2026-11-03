using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Map11Main
{
    // 本物のボタンの signal を通し、案内と配置・時計の接続を確認する。
    // res://Map11Main.tscn -- --map-guide-smoke [--map-stage=1-2]
    // 絵も保存する場合は --map-guide-capture=<ディレクトリ>（headless を外す）。
    private async void GuideSmoke()
    {
        var fails = new List<string>();
        string? capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--map-guide-capture=", StringComparison.Ordinal));
        string? directory = capture?["--map-guide-capture=".Length..];
        if (directory is not null) System.IO.Directory.CreateDirectory(directory);
        var stage = St.Stage;
        _clockSmoke = true; // 時間はこの門だけで回す。
        try
        {
            Map11Session.Reset(0, stage: stage);
            Map11Clock.Bind(St);
            _selected = 0;
            _reform = true;
            _guideSquadChosen = _guideDestinationChosen = false;
            ClearHold();
            Refresh();
            await Shot("01-hand");
            Check("hand", _guide.Target == _guideBenchButtons[0]);
            Check("empty-destination", _destButtons.Where(d => !d.Dest.AtHome).All(d => d.Node.Disabled));
            int bench = St.Bench.Count;
            Press(_guide.Target);
            await Shot("02-seat");
            Check("pick-without-mutation", St.Bench.Count == bench && SeatsOf(0).Count == 0 && St.Turn == 0);
            Check("seat", _guide.Target == _squadDiagram.CardAt(FormationRules.PlayableSlots[0]));
            Press(_guide.Target);
            await Shot("03-spare");
            Check("spare", SeatsOf(0).Count == 1 && St.Bench.Count == bench - 1
                && _guide.Message.Contains("まだ置ける", StringComparison.Ordinal));

            // 1枚でも出撃は可能。時計の入口の既存確認はそのまま残る。
            Press(_reformButton);
            await Shot("04-squad");
            Check("squad", !_reform && _guide.Target == _squadButtons[0]);
            Press(_guide.Target);
            await Shot("05-destination");
            Check("destination", _destButtons.Any(d => d.Node == _guide.Target && !d.Node.Disabled));
            Press(_guide.Target);
            await Shot("06-run");
            Check("run", _guide.Target == _run && St.Turn == 0);
            Press(_run);
            await Shot("07-confirm");
            Check("confirm", _confirmOverlay.Visible && _guide.Target is null && !Map11Clock.Running);
            // 「組み直す」で戻り、満席まで配る。
            Button back = Descendants(_confirmOverlay).OfType<Button>().Single(b => b.Text == "組み直す");
            Press(back);
            Check("back-to-reform", _reform && _guide.Target == _guideBenchButtons[0]);
            while (SeatsOf(0).Count < FormationRules.PlayableSlots.Length)
            {
                Press(_guideBenchButtons[0]);
                Press(_guide.Target);
            }
            await Shot("08-deploy");
            Check("deploy", _guide.Target == _reformButton && _reformButton.Text == "出撃");

            // 満席で手札をつまんだ場合も、空席がなくても入れ替える札を指す。
            Press(_guideBenchButtons[0]);
            Check("full-swap", _guide.Target is { Disabled: false } && _guide.Message.Contains("入れ替える席"));
            Press(_guideBenchButtons[0]); // つまむのをやめる。

            // 既定編成でも案内先の判定が駒を実体化しないこと。
            Map11Session.Reset(0, draft: false, stage: stage);
            Map11Clock.Bind(St);
            _reform = false;
            ClearHold();
            _guideSquadChosen = _guideDestinationChosen = false;
            Refresh();
            int turn = St.Turn;
            for (int i = 0; i < 20; i++) ProcessGuide(0.016);
            Check("read-only", St.Turn == turn && St.Battles == 0 && St.Squads.All(s => s.Units is null));
            Press(_squadButtons[0]);
            PickDestination(Dest.Of(St.Graph, St.Graph.Lanes[0][0]));
            Press(_run);
            RefreshGuide();
            Check("running-hidden", Map11Clock.Running && _guide.Target is null);
            Press(_run);
            Check("paused", !Map11Clock.Running && St.Turn == turn && _guide.Target == _run);
            // いったん拠点待機へ。最初の刻の敵前進を出来事として見る。
            PickDestination(Dest.Home);
            Press(_run);
            Map11Clock.IntervalOverride = 0;
            ClockStep(0);
            await Shot("09-event");
            Check("event", !Map11Clock.Running && _guideEventCell is not null
                && _destButtons.Any(d => d.Dest.Id == _guideEventCell && d.Node == _guide.Target)
                && _toast.Text.Length > 0);

            // 出撃中の隊に組み直しの手札・席を案内しない。
            St.Send(0, 0);
            _guideEventCell = null;
            _reform = true;
            Select(0);
            RefreshGuide();
            Check("away", !St.CanReform(0) && _guide.Target == _squadButtons[1]);
            _reform = false;
            _toastClock = 0;
            Refresh();
        }
        catch (Exception e) { fails.Add(e.Message); GD.PrintErr(e); }
        GD.Print($"MAP_GUIDE_SMOKE_COMPLETE stage={stage.Id} ok={fails.Count == 0} failed=[{string.Join(",", fails)}]");
        GetTree().Quit(fails.Count == 0 ? 0 : 1);

        void Check(string name, bool ok)
        {
            GD.Print($"MAP_GUIDE_SMOKE {name} ok={ok} target={_guideKey}");
            if (!ok) fails.Add(name);
        }
        void Press(Button? button)
        {
            if (button is null || button.Disabled) throw new InvalidOperationException("案内先が押せない: " + _guideKey);
            button.EmitSignal(BaseButton.SignalName.Pressed);
            RefreshGuide();
        }
        async Task Shot(string name)
        {
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (directory is null) return;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Error error = GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png"));
            Check("capture:" + name, error == Error.Ok);
        }
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
}
