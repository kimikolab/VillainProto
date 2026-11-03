using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Map11Main
{
    private MapRoutePreview _routePreview = null!;
    private DestButton? _hoverDestination;
    private readonly List<Button> _freeOfferButtons = new();

    private void BuildRoutePreview()
    {
        _routePreview = new MapRoutePreview();
        _uiRoot.AddChild(_routePreview);
        _routePreview.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    private void UpdateRoutePreview()
    {
        if (_routePreview is null) return;
        _routePreview.Visible = !_reform && !GuideCovered && _guideSquadChosen;
        if (!_routePreview.Visible) { _hoverDestination = null; return; }
        DestButton? pick = _hoverDestination is { Node.Disabled: false } hover && hover.Node.IsVisibleInTree()
            ? hover : _guideDestinationChosen
                ? _destButtons.FirstOrDefault(d => d.Dest == Map11Session.Destinations[_selected] && !d.Node.Disabled) : null;
        if (pick is null)
        {
            _routePreview.SetRoute([], []);
            _orderHint.Text = St.Time.On ? $"{St.Squads[_selected].Label} の行き先: {Map11Orders.Where(St.Graph, Map11Session.Destinations[_selected])}"
                + $"　→ この作戦ターン: {OrderText(_selected)}" : "";
            return;
        }
        var squad = St.Squads[_selected];
        bool warp = St.CanWarp(_selected, pick.Dest);
        List<int> path = warp ? [squad.Pos, pick.Dest.Id] : St.Graph.Path(squad.Pos, squad.Via, pick.Dest.Id, pick.Dest.Lane);
        // 現在その経路上にいる敵を示す。未来の移動や戦闘の勝敗を画面では予測しない。
        var enemies = St.Nodes.SelectMany(n => n).Concat(St.Spawns)
            .Concat(St.Reserve is { } reserve ? new[] { reserve } : Array.Empty<Map11State.Node>())
            .Where(n => !n.Cleared && path.Contains(n.Pos)).Distinct().ToArray();
        Vector2 Project(int cell) => _camera.UnprojectPosition(PosOf(cell) + Vector3.Up * .5f);
        _routePreview.SetRoute(path.Select(Project).ToArray(), enemies.Select(n => Project(n.Pos)).ToArray());
        string home = pick.Dest.AtHome ? "　帰還後に回復・補充" : "";
        _orderHint.Text = $"{squad.Label} → {St.Graph[pick.Dest.Id].Name}　{(warp ? "ワープ" : "移動")} {Math.Max(0, path.Count - 1)}刻"
            + (enemies.Length > 0 ? $"　経路上の敵 {enemies.Length}隊" : "") + home;
    }

    // 横断路は細い板橋。主街道の土の帯と、色だけでなく床板・欄干の形で分ける。
    private void AddFootbridge(Vector3 from, Vector3 to)
    {
        Vector3 along = (to - from).Normalized();
        Vector3 across = along.Cross(Vector3.Up);
        float length = from.DistanceTo(to);
        var timber = CampaignMain.MakeMaterial(Color.FromHtml("#bfa477"), roughness: 1);
        void Beam(Vector3 at, Vector3 size)
        {
            var mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = at, MaterialOverride = timber };
            AddChild(mesh);
            mesh.LookAt(at + along, Vector3.Up);
        }
        for (float step = 1.4f; step < length - 1.1f; step += .55f)
            Beam(from + along * step + Vector3.Up * .14f, new Vector3(1.6f, .15f, .44f));
        foreach (float side in new[] { -1f, 1f })
        {
            Beam((from + to) / 2 + across * side * .8f + Vector3.Up * .75f,
                new Vector3(.12f, .13f, Math.Max(.1f, length - 2.4f)));
            for (float step = 1.4f; step < length - 1; step += 2.2f)
                Beam(from + along * step + across * side * .8f + Vector3.Up * .42f, new Vector3(.17f, .85f, .17f));
        }
    }
}

/// <summary>経路の見た目だけ。クリックを遮らず、地図の選択札の下へ敷く。</summary>
public partial class MapRoutePreview : Control
{
    internal Vector2[] Points { get; private set; } = [];
    internal Vector2[] Enemies { get; private set; } = [];
    public MapRoutePreview() { MouseFilter = MouseFilterEnum.Ignore; }
    public void SetRoute(Vector2[] points, Vector2[] enemies) { Points = points; Enemies = enemies; QueueRedraw(); }
    public override void _Draw()
    {
        if (Points.Length > 1)
        {
            DrawPolyline(Points, new Color(0.04f, .07f, .05f, .85f), 9, true);
            DrawPolyline(Points, UiKit.Gold, 4, true);
            for (int i = 1; i < Points.Length; i++)
            {
                Vector2 center = Points[i - 1].Lerp(Points[i], .6f);
                Vector2 direction = (Points[i] - Points[i - 1]).Normalized();
                Vector2 across = direction.Orthogonal();
                DrawColoredPolygon([center + direction * 9, center - direction * 5 + across * 5,
                    center - direction * 5 - across * 5], UiKit.Gold);
            }
        }
        foreach (Vector2 enemy in Enemies) DrawArc(enemy, 32, 0, Mathf.Tau, 48, UiKit.Enemy, 3, true);
    }
}
