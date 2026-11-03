using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class Map11Main
{
    private async void AddendumSmoke()
    {
        _clockSmoke = true;
        string? directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--map-addendum-capture="))?["--map-addendum-capture=".Length..];
        if (directory is not null) System.IO.Directory.CreateDirectory(directory);
        try
        {
            Map11Session.Reset(0, stage: St.Stage); Map11Clock.Bind(St);
            St.UseDefaultSquads(); _selected = 0; _reform = false;
            _guideSquadChosen = true; _guideDestinationChosen = false;
            Refresh(); await Frames();
            var dest = _destButtons.First(d => d.Dest.Id == St.Graph.Lanes[0][^1]);
            Dest original = Map11Session.Destinations[0];
            Vector2 mouse = GetViewport().GetFinalTransform() * dest.Node.GetGlobalRect().GetCenter();
            Input.ParseInputEvent(new InputEventMouseMotion { Position = mouse, GlobalPosition = mouse });
            await Frames();
            Require(_hoverDestination == dest && _routePreview.Points.Length > 1, "実際のマウス位置で経路が出る");
            Require(_routePreview.Enemies.Length > 0, "経路上の敵が表示される");
            Require(Map11Session.Destinations[0] == original && St.Turn == 0, "ホバーは命令や時刻を書き換えない");
            Require(_orderHint.Text.Contains("移動") && _orderHint.Text.Contains("刻"), "経路から刻を出す");
            await Shot("01-route");
            Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(8, 188), GlobalPosition = new Vector2(8, 188) });
            await Frames(); Require(_routePreview.Points.Length == 0, "ホバー解除で仮の経路を消す");

            if (St.ReserveWaiting && St.Reserve is { } waiting)
            {
                _hoverDestination = _destButtons.First(d => d.Dest.IsPortal);
                UpdateRoutePreview();
                Vector2 at = _camera.UnprojectPosition(PosOf(waiting.Pos) + Vector3.Up * .5f);
                Require(_routePreview.Enemies.Contains(at), "敵拠点で待つ予備隊も経路の敵に含む");
                _hoverDestination = null;
            }
            foreach (var (a, b) in St.Graph.Edges.Where(e => St.Graph.IsCross(e.A, e.B)))
            {
                St.Squads[0].Pos = a;
                St.Squads[0].Via = St.Graph[a].Lane;
                _hoverDestination = _destButtons.Single(d => d.Dest.Id == b);
                UpdateRoutePreview();
                Require(_routePreview.Points.Length == 2 && _orderHint.Text.Contains("移動 1刻"), "横の道も1刻の経路になる");
            }
            St.Squads[0].Pos = St.Graph.Home; St.Squads[0].Via = -1; _hoverDestination = null;
            foreach (int cell in _outpostArt.Keys)
            {
                var enemy = _outpostArt[cell].Texture;
                St.HeldOutposts.Add(cell); RefreshOutposts();
                Require(_outpostArt[cell].Texture != enemy, "拠点の所有者で絵を替える");
            }
            await Shot("02-allied-outposts");
            St.HeldOutposts.Clear(); RefreshOutposts();

            UnitDef[] units = [EnemyCatalog.Recruit, EnemyCatalog.Levy, EnemyCatalog.Laborer, EnemyCatalog.Drifter,
                EnemyCatalog.Axeman, EnemyCatalog.Archer, EnemyCatalog.Priest, EnemyCatalog.Warden, EnemyCatalog.Knight,
                EnemyCatalog.ZealotSquire, EnemyCatalog.Husher, EnemyCatalog.Droughter, EnemyCatalog.Yoker, EnemyCatalog.Accuser];
            var sheet = Panel(); _uiRoot.AddChild(sheet);
            Bounds(sheet, 32, 190, -32, -100, anchorRight: 1, anchorBottom: 1);
            var grid = new GridContainer { Columns = 7 }; sheet.AddChild(grid);
            foreach (UnitDef unit in units)
            {
                var col = new VBoxContainer { CustomMinimumSize = new Vector2(154, 140) }; grid.AddChild(col);
                var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(100, 100) }; col.AddChild(face); face.SetUnit(unit, true, "敵");
                Require(face.FaceTexture is not null, "敵の顔: " + unit.Id);
                col.AddChild(UiKit.Text(unit.Name, 13));
            }
            foreach (var (id, name) in new[] { ("burn", "燃焼"), ("poison", "毒") })
            {
                using var source = new Image();
                Require(source.LoadSvgFromString(System.IO.File.ReadAllText(ProjectSettings.GlobalizePath($"res://assets/map/status/{id}.svg"))) == Error.Ok, name + "の印");
                var col = new VBoxContainer(); grid.AddChild(col);
                col.AddChild(new TextureRect { Texture = ImageTexture.CreateFromImage(source), CustomMinimumSize = new Vector2(48, 48), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
                col.AddChild(UiKit.Text(name, 13));
            }
            await Shot("03-assets"); sheet.QueueFree(); await Frames();
            if (St.Stage.Gold.SlotFreePick)
            {
                Require(St.BuySlot(), "枠を買う"); _reform = true; Refresh(); await Frames();
                Require(_guide.Target is { } target && _freeOfferButtons.Contains(target) && VisibleRect(target), "無料候補を表示して光らせる");
                await Shot("04-free-pick");
                int gold = St.Gold, bench = St.Bench.Count;
                _guide.Target!.EmitSignal(Button.SignalName.Pressed); await Frames();
                Require(St.FreePicks == 0 && St.Gold == gold && St.Bench.Count == bench + 1, "無料候補を選び通常の案内へ戻る");
                Require(!_freeOfferButtons.Contains(_guide.Target!), "無料案内の消去");
                _reform = false; Refresh(); await Frames();
                St.GoldLog.Add(new Map11State.GoldEvent(6, "演出検査", St.Graph.Lanes[0][1]));
                FloatNewGold(); await Frames();
                Require(_uiRoot.GetChildren().OfType<MapGoldBurst>().Count() == 1, "稼ぎの記録1件に金貨の束1つ");
                FloatNewGold(); Require(_uiRoot.GetChildren().OfType<MapGoldBurst>().Count() == 1, "再描画で重複しない");
                await Shot("05-gold");
                await ToSignal(GetTree().CreateTimer(1.8), SceneTreeTimer.SignalName.Timeout);
                Require(!_uiRoot.GetChildren().OfType<MapGoldBurst>().Any(), "金貨は終了時に消す");
            }
            GD.Print($"MAP_ADDENDUM_SMOKE_COMPLETE stage={St.Stage.Id} ok=True"); GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr(e); GetTree().Quit(1); }
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); GD.Print("MAP_ADDENDUM_OK " + message); }
        async Task Frames() { for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Shot(string name)
        {
            if (directory is null) return;
            await Frames(); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, name);
        }
    }
}
