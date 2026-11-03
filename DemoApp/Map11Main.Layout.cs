using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Map11Main
{
    // 編成／作戦は同じ State と同じ操作部品を使い、置き場所と表示だけを切り替える。
    private Control _uiRoot = null!, _formationScreen = null!, _operationHud = null!;
    private Control _formationSquadHost = null!, _operationSquadHost = null!, _deployHost = null!;
    private VBoxContainer _formationSeatsHost = null!, _formationInfoHost = null!, _operationDetailHost = null!;
    private VBoxContainer _squadContent = null!, _squadInfoContent = null!, _detailActions = null!;
    private Control _diagramHost = null!;
    private PanelContainer _squadDrawer = null!, _foeDrawer = null!, _historyDrawer = null!;
    private Button _closeSquad = null!, _enemyToggle = null!, _historyToggle = null!, _replayButton = null!;
    private Label _formationCount = null!, _squadTitle = null!;
    private ScrollContainer _resultScroll = null!;
    private MapOverview _overview = null!;
    private bool _showSquadDetail, _showFoes, _showHistory;
    private bool? _laidOutReform;
    private Vector2 _layoutSize;
    private string _mapHeadlineText = "";
    private readonly List<(Button Button, Marker Marker, Map11State.Node? Foe, int Squad)> _mapPicks = new();
    private Control _mapPickLayer = null!;

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        _uiRoot = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Theme = new Theme
            {
                DefaultFont = new SystemFont
                {
                    FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" },
                    AllowSystemFallback = true,
                },
                DefaultFontSize = 14,
            },
        };
        layer.AddChild(_uiRoot);
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var header = new ColorRect { Color = Color.FromHtml("#15241e"), MouseFilter = Control.MouseFilterEnum.Ignore };
        _uiRoot.AddChild(header);
        Bounds(header, 0, 0, 0, 180, anchorRight: 1);
        _headline = UiKit.Text("", 21, UiKit.Ink);
        _headline.Position = new Vector2(24, 18);
        _headline.Size = new Vector2(1390, 30);
        _uiRoot.AddChild(_headline);
        _toast = UiKit.Text("", 15, GoldColor);
        _toast.Position = new Vector2(24, 54);
        _toast.Size = new Vector2(1390, 26);
        _toast.ClipText = true;
        _uiRoot.AddChild(_toast);

        _mapPickLayer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _uiRoot.AddChild(_mapPickLayer);
        _mapPickLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        // 地図上の隊・敵を押す領域より、行き先のボタンを手前に置く。
        BuildDestButtons(_uiRoot);
        BuildOperationHud();
        BuildFormationScreen();
        BuildSquadDrawer();
        BuildFoeDrawer();
        BuildHistoryDrawer();
        BuildSharedSquadControls();
        BuildGuide(_uiRoot);
        BuildEncounterOverlay(_uiRoot);
        BuildInterceptOverlay(_uiRoot);
        BuildResultOverlay(_uiRoot);
        BuildConfirmOverlay(_uiRoot);
    }

    private static VBoxContainer Column(Control parent, int gap = 10)
    {
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", gap);
        parent.AddChild(col);
        return col;
    }

    private static Control Fill(Control parent)
    {
        var node = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(node);
        node.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return node;
    }

    private static void Bounds(Control node, float left, float top, float right, float bottom,
        float anchorLeft = 0, float anchorRight = 0, float anchorBottom = 0, float anchorTop = 0)
    {
        node.AnchorLeft = anchorLeft; node.AnchorRight = anchorRight;
        node.AnchorTop = anchorTop; node.AnchorBottom = anchorBottom;
        node.OffsetLeft = left; node.OffsetTop = top; node.OffsetRight = right; node.OffsetBottom = bottom;
    }

    private void BuildOperationHud()
    {
        _operationHud = Fill(_uiRoot);
        var toolbar = new HBoxContainer();
        toolbar.AddThemeConstantOverride("separation", 10);
        _operationHud.AddChild(toolbar);
        Bounds(toolbar, 24, 94, -24, 136, anchorRight: 1);
        _clockLabel = UiKit.Text("", 15, UiKit.Ink);
        _clockLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        toolbar.AddChild(_clockLabel);
        _clockGauge = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Step = 0, ShowPercentage = false,
            CustomMinimumSize = new Vector2(100, 8), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        toolbar.AddChild(_clockGauge);
        toolbar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _run = UiKit.ActionButton("▶", GoldColor);
        _run.CustomMinimumSize = new Vector2(150, 42);
        _run.Pressed += ToggleClock;
        toolbar.AddChild(_run);
        Button Speed(string text, bool fast)
        {
            Button b = UiKit.ActionButton(text, UiKit.Muted);
            b.CustomMinimumSize = new Vector2(48, 42);
            b.TooltipText = fast ? "速さ 2×（1 刻 0.6 秒）" : "速さ 1×（1 刻 1.2 秒）";
            b.Pressed += () => { Map11Clock.Fast = fast; RefreshClock(); };
            toolbar.AddChild(b);
            return b;
        }
        _speed1 = Speed("1×", false); _speed2 = Speed("2×", true);
        _advanceTurn = UiKit.ActionButton("1ターン", UiKit.Muted);
        _advanceTurn.Pressed += AdvanceTurn;
        toolbar.AddChild(_advanceTurn);
        _enemyToggle = UiKit.ActionButton("敵部隊", EnemyColor);
        _enemyToggle.Pressed += () => { _showFoes = !_showFoes; _showSquadDetail = _showHistory = false; RefreshScreen(); };
        toolbar.AddChild(_enemyToggle);
        _historyToggle = UiKit.ActionButton("戦闘の記録", UiKit.Muted);
        _historyToggle.Pressed += () => { _showHistory = !_showHistory; _showFoes = _showSquadDetail = false; RefreshScreen(); };
        toolbar.AddChild(_historyToggle);
        _orderHint = UiKit.Text("", 13, GoldColor);
        _operationHud.AddChild(_orderHint);
        Bounds(_orderHint, 24, 146, -24, 174, anchorRight: 1);

        _operationSquadHost = new PanelContainer();
        _operationSquadHost.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Panel, UiKit.Line));
        _operationHud.AddChild(_operationSquadHost);
        Bounds(_operationSquadHost, 20, -78, -20, -20, anchorRight: 1, anchorTop: 1, anchorBottom: 1);
    }

    private void BuildFormationScreen()
    {
        _formationScreen = new ColorRect { Color = Color.FromHtml("#111d19") };
        _uiRoot.AddChild(_formationScreen);
        Bounds(_formationScreen, 0, 88, 0, 0, anchorRight: 1, anchorBottom: 1);
        _formationSquadHost = new MarginContainer();
        _formationScreen.AddChild(_formationSquadHost);
        Bounds(_formationSquadHost, 20, 14, -270, 84, anchorRight: 1);
        _deployHost = new MarginContainer();
        _formationScreen.AddChild(_deployHost);
        Bounds(_deployHost, -246, 14, -20, 64, anchorLeft: 1, anchorRight: 1);
        _formationCount = UiKit.Text("", 13, UiKit.Muted);
        _formationScreen.AddChild(_formationCount);
        Bounds(_formationCount, 24, 94, -24, 122, anchorRight: 1);

        PanelContainer hand = Panel();
        _formationScreen.AddChild(hand);
        Bounds(hand, 20, 134, 350, -20, anchorBottom: 1);
        var handCol = Column(hand);
        _benchTitle = UiKit.Text("手札", 20, UiKit.Ink);
        handCol.AddChild(_benchTitle);
        _benchList = new VBoxContainer();
        _benchList.AddThemeConstantOverride("separation", 8);
        ScrollContainer benchScroll = Scroll(_benchList, 0);
        benchScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        handCol.AddChild(benchScroll);
        _resetButton = UiKit.ActionButton("この隊を既定に戻す", UiKit.Muted);
        _resetButton.Pressed += ResetSquad;
        handCol.AddChild(_resetButton);
        _defaultButton = UiKit.ActionButton("既定の3隊で始める", UiKit.Muted);
        _defaultButton.Pressed += UseDefaultSquads;
        handCol.AddChild(_defaultButton);

        PanelContainer seats = Panel();
        _formationScreen.AddChild(seats);
        Bounds(seats, 370, 134, -380, -20, anchorRight: 1, anchorBottom: 1);
        _formationSeatsHost = new VBoxContainer();
        _leftScroll = Scroll(_formationSeatsHost, 0);
        seats.AddChild(_leftScroll);

        PanelContainer info = Panel();
        _formationScreen.AddChild(info);
        Bounds(info, -360, 134, -20, -20, anchorLeft: 1, anchorRight: 1, anchorBottom: 1);
        var infoCol = Column(info);
        infoCol.AddChild(UiKit.Text(St.Stage.Title, 16, UiKit.Muted));
        _overview = new MapOverview { State = St, CustomMinimumSize = new Vector2(280, 160) };
        infoCol.AddChild(_overview);
        _formationInfoHost = new VBoxContainer();
        ScrollContainer infoScroll = Scroll(_formationInfoHost, 0);
        infoScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        infoCol.AddChild(infoScroll);
    }

    private void BuildSquadDrawer()
    {
        _squadDrawer = Panel();
        _uiRoot.AddChild(_squadDrawer);
        Bounds(_squadDrawer, 20, 184, 354, -94, anchorBottom: 1);
        var col = Column(_squadDrawer);
        _detailActions = new VBoxContainer();
        col.AddChild(_detailActions);
        _closeSquad = UiKit.ActionButton("隊の詳細を閉じる", UiKit.Muted);
        _closeSquad.Pressed += () => { _showSquadDetail = false; RefreshScreen(); };
        _detailActions.AddChild(_closeSquad);
        _operationDetailHost = new VBoxContainer();
        _operationDetailHost.AddThemeConstantOverride("separation", 10);
        ScrollContainer scroll = Scroll(_operationDetailHost, 0);
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(scroll);
    }

    private void BuildSharedSquadControls()
    {
        _squadList = new HBoxContainer();
        _squadList.AddThemeConstantOverride("separation", 10);
        _formationSquadHost.AddChild(_squadList);
        for (int i = 0; i < St.Squads.Length; i++)
        {
            int index = i;
            Button b = UiKit.ActionButton("", UiKit.Line);
            b.Alignment = HorizontalAlignment.Left;
            b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            b.ClipText = true;
            b.Pressed += () => Select(index);
            _squadList.AddChild(b);
            _squadButtons.Add(b);
        }
        _reformButton = UiKit.ActionButton("出撃", GoldColor);
        _reformButton.CustomMinimumSize = new Vector2(0, 44);
        _reformButton.Pressed += ToggleReform;
        _deployHost.AddChild(_reformButton);

        _squadContent = Column(_formationSeatsHost);
        _squadTitle = UiKit.Text("", 20, UiKit.Ink);
        _squadContent.AddChild(_squadTitle);
        _squadContent.AddChild(LinkLegend());
        _reformHint = UiKit.Text("", 13, GoldColor);
        _reformHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _squadContent.AddChild(_reformHint);
        _diagramHost = new Control();
        _squadContent.AddChild(_diagramHost);
        _squadDiagram = new SeatDiagram();
        _squadDiagram.SlotSelected += OnSquadSlot;
        _diagramHost.AddChild(_squadDiagram);
        _squadSummary = new VBoxContainer();
        _squadSummary.AddThemeConstantOverride("separation", 4);
        _squadContent.AddChild(Scroll(_squadSummary, 126));

        _squadInfoContent = Column(_formationInfoHost);
        _squadInfoContent.AddChild(UiKit.Text("選んだ駒", 18, UiKit.Ink));
        _squadFocus = new VBoxContainer();
        _squadFocus.AddThemeConstantOverride("separation", 8);
        _squadInfoContent.AddChild(_squadFocus);
        _squadMore = UiKit.ActionButton("くわしく", UiKit.Muted);
        _squadMore.Pressed += () => { _squadDetailBox.Visible = !_squadDetailBox.Visible; Refresh(); };
        _squadInfoContent.AddChild(_squadMore);
        _squadDetail = new VBoxContainer();
        _squadDetail.AddThemeConstantOverride("separation", 6);
        _squadDetailBox = Scroll(_squadDetail, 230);
        _squadDetailBox.Visible = false;
        _squadInfoContent.AddChild(_squadDetailBox);
    }

    private void BuildFoeDrawer()
    {
        _foeDrawer = Panel();
        _uiRoot.AddChild(_foeDrawer);
        Bounds(_foeDrawer, -354, 184, -20, -94, anchorLeft: 1, anchorRight: 1, anchorBottom: 1);
        var outer = Column(_foeDrawer);
        Button close = UiKit.ActionButton("敵部隊を閉じる", UiKit.Muted);
        close.Pressed += () => { _showFoes = false; RefreshScreen(); };
        outer.AddChild(close);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        ScrollContainer scroll = Scroll(col, 0);
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        outer.AddChild(scroll);
        _roadList = new VBoxContainer();
        col.AddChild(Scroll(_roadList, 170));
        _foeDiagram = new SeatDiagram();
        _foeDiagram.SlotSelected += slot => { _foePick = _foePick == slot ? -1 : slot; Refresh(); };
        col.AddChild(_foeDiagram);
        _foeFocus = new VBoxContainer();
        col.AddChild(_foeFocus);
        _foeMore = UiKit.ActionButton("くわしく", UiKit.Muted);
        _foeMore.Pressed += () => { _foeDetailBox.Visible = !_foeDetailBox.Visible; Refresh(); };
        col.AddChild(_foeMore);
        _foeDetail = new VBoxContainer();
        _foeDetailBox = Scroll(_foeDetail, 230);
        _foeDetailBox.Visible = false;
        col.AddChild(_foeDetailBox);
    }

    private void BuildHistoryDrawer()
    {
        _historyDrawer = Panel();
        _uiRoot.AddChild(_historyDrawer);
        Bounds(_historyDrawer, -640, 184, -20, -94, anchorLeft: 1, anchorRight: 1, anchorBottom: 1);
        var col = Column(_historyDrawer);
        Button close = UiKit.ActionButton("戦闘の記録を閉じる", UiKit.Muted);
        close.Pressed += () => { _showHistory = false; RefreshScreen(); };
        col.AddChild(close);
        _history = new VBoxContainer();
        ScrollContainer scroll = Scroll(_history, 0);
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(scroll);
    }

    private static void MoveUi(Control control, Control parent)
    {
        if (control.GetParent() != parent) control.Reparent(parent, keepGlobalTransform: false);
    }

    private void RefreshScreen()
    {
        if (_formationScreen is null) return;
        _formationScreen.Visible = _reform;
        _headline.Text = _reform ? $"編成　｜　{St.Stage.Title}" : "作戦　｜　" + _mapHeadlineText;
        _operationHud.Visible = !_reform;
        _orderHint.Visible = _guideSquadChosen;
        _squadDrawer.Visible = !_reform && _showSquadDetail;
        _foeDrawer.Visible = !_reform && _showFoes;
        _historyDrawer.Visible = !_reform && _showHistory;
        _mapPickLayer.Visible = !_reform;
        _enemyToggle.Text = _showFoes ? "敵部隊 ▴" : "敵部隊";
        _historyToggle.Text = _showHistory ? "戦闘の記録 ▴" : "戦闘の記録";
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (_laidOutReform != _reform || _layoutSize != viewport)
        {
            MoveUi(_squadList, _reform ? _formationSquadHost : _operationSquadHost);
            MoveUi(_reformButton, _reform ? _deployHost : _detailActions);
            MoveUi(_squadContent, _reform ? _formationSeatsHost : _operationDetailHost);
            MoveUi(_squadInfoContent, _reform ? _formationInfoHost : _operationDetailHost);
            float scale = _reform ? Math.Clamp((viewport.Y - 448) / SeatDiagram.Size2.Y, 1f, 1.65f) : 1f;
            _diagramHost.CustomMinimumSize = SeatDiagram.Size2 * scale;
            _squadDiagram.SetDisplayScale(scale);
            _squadDiagram.Position = Vector2.Zero;
            _laidOutReform = _reform;
            _layoutSize = viewport;
        }
        _squadTitle.Text = St.Squads[_selected].Label + (_reform ? " の編成" : " の配置");
        _formationCount.Text = string.Join("　／　", St.Squads.Select(s =>
            $"{s.Label} {SeatsOf(s.Index).Count}枚・空席{FormationRules.PlayableSlots.Length - SeatsOf(s.Index).Count}"))
            + $"　　未配属 {St.Bench.Count}枚";
        _overview.State = St;
        _overview.QueueRedraw();
        foreach (var s in St.Squads)
        {
            int alive = s.Lost ? 0 : s.Units?.Count(u => u.IsAlive) ?? s.Def.F.Occupied().Count();
            string hp = alive == 0 ? "—" : $"{SquadHpPercent(s):F0}%";
            string info = $"{s.Label}　{alive}枚　HP {hp}";
            string order = s.Lost ? "失われた" : OrderText(s.Index);
            Button b = _squadButtons[s.Index];
            b.Text = _reform ? info + Nl + (St.CanReform(s.Index) ? $"空席 {FormationRules.PlayableSlots.Length - SeatsOf(s.Index).Count}" : "道の上・編成不可")
                : info + "　" + order;
            b.TooltipText = info + "　" + order;
        }
        RefreshMapPicks();
        FrameOperationMap();
        RefreshDestButtons();
    }

    private void FrameOperationMap()
    {
        // 横長の盤に合わせ、空いた横幅を使う。選んだ隊の詳細は地図に重ねず左に確保する。
        bool wide = St.Stage.LaneCount > 2;
        Vector3 look = new(wide ? 7 : 9, 0, -2);
        if (!_reform && _showSquadDetail) look.X -= 7;
        if (!_reform && (_showFoes || _showHistory)) look.X += 7;
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = wide ? 44 : 40;
        _camera.Position = look + new Vector3(0, 46, 34);
        _camera.LookAt(look, Vector3.Up);
    }

    private void RefreshMapPicks()
    {
        for (int i = 0; i < _squadMarkers.Count; i++) AddMapPick(_squadMarkers[i], null, i);
        int index = 0;
        foreach (var road in St.Nodes)
            foreach (var foe in road) AddMapPick(_nodeMarkers[index++], foe, -1);
        for (int i = 0; i < St.Spawns.Count; i++) AddMapPick(_spawnMarkers[i], St.Spawns[i], -1);
        PositionMapPicks();
    }

    private void AddMapPick(Marker marker, Map11State.Node? foe, int squad)
    {
        int existing = _mapPicks.FindIndex(p => p.Marker == marker);
        if (existing >= 0)
        {
            _mapPicks[existing] = (_mapPicks[existing].Button, marker, foe, squad);
            return;
        }
        var b = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, Size = new Vector2(130, 48) };
        b.TooltipText = foe?.Def.Name ?? St.Squads[squad].Label;
        b.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        b.AddThemeStyleboxOverride("hover", UiKit.Box(new Color(1, 1, 1, 0.08f), foe is null ? PlayerColor : EnemyColor));
        b.Pressed += () =>
        {
            var current = _mapPicks.First(p => p.Marker == marker);
            if (current.Foe is null) Select(current.Squad);
            else { ShowFoe(current.Foe); _showFoes = true; _showSquadDetail = _showHistory = false; Refresh(); }
        };
        _mapPickLayer.AddChild(b);
        _mapPicks.Add((b, marker, foe, squad));
    }

    private void PositionMapPicks()
    {
        foreach (var pick in _mapPicks)
        {
            pick.Button.Visible = !_reform && (pick.Foe is { } foe ? !foe.Cleared : !St.Squads[pick.Squad].Lost);
            pick.Button.Position = _camera.UnprojectPosition(pick.Marker.Root.Position) + new Vector2(-65, -62);
        }
    }
}

/// <summary>編成中の小さな地図。盤の定義と現在位置を描くだけで、行き先の入力は受けない。</summary>
public partial class MapOverview : Control
{
    public Map11State State { get; set; } = null!;
    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Draw()
    {
        if (State is null) return;
        var cells = State.Graph.Cells;
        float minX = cells.Min(c => c.Pos.X), maxX = cells.Max(c => c.Pos.X);
        float minZ = cells.Min(c => c.Pos.Z), maxZ = cells.Max(c => c.Pos.Z);
        Vector2 Point(int id)
        {
            var p = State.Graph[id].Pos;
            return new Vector2(22 + (p.X - minX) / Math.Max(1, maxX - minX) * (Size.X - 44),
                22 + (p.Z - minZ) / Math.Max(1, maxZ - minZ) * (Size.Y - 44));
        }
        foreach (var (a, b) in State.Graph.Edges) DrawLine(Point(a), Point(b), UiKit.Line, 3, true);
        foreach (var c in cells)
        {
            Vector2 at = Point(c.Id);
            Color color = c.Id == State.Graph.Home ? UiKit.Heal : c.Id == State.Graph.Portal ? UiKit.Enemy : UiKit.Muted;
            if (c.Kind == MapGraph.Kind.Home) DrawRect(new Rect2(at - Vector2.One * 7, Vector2.One * 14), color);
            else if (c.Kind == MapGraph.Kind.Outpost) DrawRect(new Rect2(at - Vector2.One * 5, Vector2.One * 10), color, false, 2);
            else DrawCircle(at, c.Kind == MapGraph.Kind.Portal ? 8 : 4, color);
        }
        foreach (var s in State.Squads.Where(s => !s.Lost && State.CanSend(s.Index)))
            DrawCircle(Point(s.Pos) + new Vector2(0, 12 + s.Index * 5), 3, UiKit.Heal);
    }
}
