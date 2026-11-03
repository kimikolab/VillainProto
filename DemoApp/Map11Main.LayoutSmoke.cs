using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Map11Main
{
    private static bool _layoutReplayPending;
    private static readonly List<string> LayoutFailures = new();

    // signal の直呼びではなく、画面上のボタン中央へマウス入力を送る。
    // 席の拡大・案内の重なり・画面切替・「もう一度」の実際の入力経路まで通す。
    private async void LayoutSmoke()
    {
        _clockSmoke = true;
        string? output = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--map-layout-capture=", StringComparison.Ordinal));
        string? directory = output?["--map-layout-capture=".Length..];
        if (directory is not null) System.IO.Directory.CreateDirectory(directory);
        try
        {
            if (_layoutReplayPending)
            {
                await Frames();
                Check("replay", St.Turn == 0 && St.Battles == 0 && _formationScreen.Visible
                    && !_resultOverlay.Visible && !Map11Clock.Running && St.Bench.Count > 0);
                await Shot("07-replay");
                Finish();
                return;
            }
            LayoutFailures.Clear();
            Map11Session.Reset(0, stage: St.Stage);
            Map11Clock.Bind(St);
            _reform = true;
            _selected = 0;
            _guideSquadChosen = _guideDestinationChosen = false;
            ClearHold();
            Refresh();
            await Frames();
            Check("formation", _formationScreen.Visible && !_operationHud.Visible && !_squadDrawer.Visible
                && _destButtons.All(d => !d.Node.IsVisibleInTree()));
            Check("five-seats-visible", FormationRules.PlayableSlots.All(s => VisibleRect(_squadDiagram.CardAt(s)!)));
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Space, Pressed = false });
            await Frames();
            Check("formation-space", St.Turn == 0 && !Map11Clock.Running);
            for (int squad = 0; squad < Math.Min(2, St.Squads.Length); squad++)
            {
                await Click(_squadButtons[squad]);
                foreach (int slot in FormationRules.PlayableSlots)
                {
                    await Click(_guideBenchButtons[0]);
                    Check($"hand-pick:{squad}:{slot}", Holding);
                    await Click(_squadDiagram.CardAt(slot)!);
                    Check($"seat-pick:{squad}:{slot}", !Holding && SeatsOf(squad).ContainsKey(slot));
                }
            }
            await Shot("01-formation");
            string units = Snapshot();
            await Click(_reformButton);
            Check("operation", !_formationScreen.Visible && _operationHud.Visible
                && !_squadDrawer.Visible && !_foeDrawer.Visible && !_historyDrawer.Visible);
            Check("squad-lines", _squadButtons.All(b => !b.Text.Contains('\n') && VisibleRect(b)));
            Check("switch-read-only", Snapshot() == units && St.Turn == 0);
            await Shot("02-operation");
            await Click(_squadButtons[0]);
            Check("selected-detail", _squadDrawer.Visible && _reformButton.IsVisibleInTree() && !_reformButton.Disabled);
            await Shot("03-selected");
            await Click(_reformButton);
            Check("reform", _formationScreen.Visible && !Map11Clock.Running && Snapshot() == units);
            await Click(_reformButton);
            await Click(_squadButtons[0]);
            DestButton dest = _destButtons.First(d => !d.Dest.AtHome && !d.Node.Disabled);
            await Click(dest.Node);
            Check("order", Map11Session.Destinations[0] == dest.Dest && St.Turn == 0);
            await Shot("04-orders");
            await Click(_run);
            if (_confirmOverlay.Visible)
                await Click(Descendants(_confirmOverlay).OfType<Button>().Single(b => b.Text == "このまま出る"));
            Check("clock-start", Map11Clock.Running);
            await Click(_run);
            Check("clock-pause", !Map11Clock.Running && St.Turn == 0);
            await Click(_reformButton);
            Check("reform-keeps-order", _reform && Map11Session.Destinations[0] == dest.Dest && Snapshot() == units);
            await Click(_reformButton);

            St.Send(0, 0); // 道の上の fixture。組み直しの可否は State の本物を読む。
            Refresh();
            await Frames();
            await Click(_squadButtons[0]);
            Check("away-cannot-reform", !St.CanReform(0) && _reformButton.Disabled);
            await Click(_squadButtons[1]);
            Check("home-can-reform", St.CanReform(1) && !_reformButton.Disabled);
            var foePick = _mapPicks.First(p => p.Foe is not null && !p.Foe.Cleared);
            await Click(foePick.Button);
            Check("foe-on-map", _foeDrawer.Visible && !_squadDrawer.Visible && ReferenceEquals(_foeView, foePick.Foe));
            await Shot("05-foe");
            await Click(_historyToggle);
            Check("history", _historyDrawer.Visible && !_foeDrawer.Visible);
            await Click(_historyToggle);

            // 長い結果の fixture。本文が画面より長くても再プレイのボタンは動かない。
            for (int i = 0; i < 12; i++)
                Map11Session.BattleLog.Add(new Map11Session.Line(i + 1, St.Squads[i % St.Squads.Length].Label,
                    St.Stage.LaneNames[i % St.Stage.LaneCount], "第三波 ／ 討伐隊本隊", true, 5, 80,
                    new[] { "盤面のルールによる働きと、持ち越した傷の記録" }, i + 1, false));
            Map11Session.TakeOpening();
            ShowResult();
            await Frames();
            Rect2 buttonRect = _replayButton.GetGlobalRect();
            Check("result-scroll", _resultScroll.GetVScrollBar().MaxValue > _resultScroll.GetVScrollBar().Page);
            Check("result-button", VisibleRect(_replayButton));
            await Shot("06-result");
            _resultScroll.ScrollVertical = 100000;
            await Frames();
            Check("result-button-fixed", _replayButton.GetGlobalRect() == buttonRect && VisibleRect(_replayButton));
            _layoutReplayPending = true;
            ClickNow(_replayButton);
            // 以後の検査は ReloadCurrentScene 後の新しい Map11Main が引き継ぐ。
        }
        catch (Exception e)
        {
            LayoutFailures.Add(e.Message);
            GD.PrintErr(e);
            Finish();
        }

        void Check(string name, bool ok)
        {
            GD.Print($"MAP_LAYOUT_SMOKE {name} ok={ok}");
            if (!ok) LayoutFailures.Add(name);
        }
        string Snapshot() => string.Join("|", St.Squads.Select(s => $"{s.Index}:{s.Pos}:{s.Lost}:"
            + string.Join(",", s.Units?.Select(u => $"{u.Def.Id}:{u.Slot}:{u.Hp}:{u.AtkBonus}") ?? Array.Empty<string>())))
            + ";" + string.Join(",", St.Bench.Select(u => $"{u.Def.Id}:{u.Hp}"));
        void Finish()
        {
            _layoutReplayPending = false;
            GD.Print($"MAP_LAYOUT_SMOKE_COMPLETE stage={St.Stage.Id} squads={St.Squads.Length} ok={LayoutFailures.Count == 0} failed=[{string.Join(",", LayoutFailures)}]");
            GetTree().Quit(LayoutFailures.Count == 0 ? 0 : 1);
        }
        async Task Frames()
        {
            for (int i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        async Task Click(Button button)
        {
            if (!button.IsVisibleInTree() || button.Disabled || !VisibleRect(button))
                throw new InvalidOperationException("画面から押せない: " + button.Text);
            ClickNow(button);
            await Frames();
        }
        void ClickNow(Button button)
        {
            // ParseInputEvent はウィンドウ座標。canvas_items の拡縮も通す。
            Vector2 at = GetViewport().GetFinalTransform() * button.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
        }
        async Task Shot(string name)
        {
            if (directory is null) return;
            await Frames();
            Check("capture:" + name, GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok);
        }
    }

    private bool VisibleRect(Control control)
    {
        Rect2 rect = control.GetGlobalRect();
        if (!control.IsVisibleInTree() || !GetViewport().GetVisibleRect().Encloses(rect)) return false;
        for (Node? parent = control.GetParent(); parent is Control clip; parent = parent.GetParent())
            if (clip.ClipContents && !clip.GetGlobalRect().Encloses(rect)) return false;
        return true;
    }
}
