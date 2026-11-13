using Godot;
using System;
using System.Linq;

// 作戦マップの 3D のジオラマ（見た目の層）の土台。発注は design/BRIEF_MAP3D_STAGE11.md。
// ここにあるのは「切り替え・画面の基準・検証用の層・道具の確かめ」だけで、地形・駒・UI は入っていない
// （見た目の段の担当が `Map3DStage` の中身と `BuildMap3D` の先を作る）。
// 2D の経路（`Map3DMode.Active` が偽）では、F2 を聞く節を 1 つ足す以外に何もしない。
public partial class Map11Main
{
    /// <summary>3D のジオラマの根（地形・道・駒をこの下に置く）。2D のときは null。</summary>
    private Map3DStage? _map3d;

    /// <summary><c>_Ready</c> が盤と UI を組んだ直後に 1 度呼ぶ。</summary>
    private void BuildMap3D()
    {
        AddChild(new Map3DSwitch());
        string[] args = OS.GetCmdlineUserArgs();
        if (args.Contains("--map3d-tool-check", StringComparer.Ordinal)) Callable.From(Map3DToolCheck).CallDeferred();
        if (!Map3DMode.Active) return;

        // 画面の基準は 3D の層にいる間だけ 1920×1080（縦横比を保ち、窓の全面を使う）。出るときに戻す。
        Window window = GetWindow();
        window.ContentScaleSize = Map3DMode.BaseSize;
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Expand;

        _map3d = new Map3DStage { Name = "Map3DStage" };
        AddChild(_map3d);
        AddChild(new Map3DDebugLayer(() => _camera));
    }

    public override void _ExitTree()
    {
        if (_map3d is null) return;
        // 戦闘・編成の画面（2D の経路）は project.godot の基準のまま。
        Window window = GetWindow();
        window.ContentScaleSize = new Vector2I(
            (int)ProjectSettings.GetSetting("display/window/size/viewport_width"),
            (int)ProjectSettings.GetSetting("display/window/size/viewport_height"));
        window.ContentScaleAspect = Window.ContentScaleAspectEnum.Keep;
    }

    /// <summary>
    /// 道と川の道具を実行時に 1 本ずつ動かす確かめ（`--map3d-tool-check`）。1-1 の北の道のマスを結ぶ線から作る。
    /// 合否は <c>MAP3D_TOOL_CHECK_COMPLETE</c> の行で読む。
    /// </summary>
    private void Map3DToolCheck()
    {
        bool ok = Map3DMode.ToolsAvailable;
        int roadVerts = 0, riverVerts = 0;
        if (ok)
        {
            var lane = St.Graph.Lanes[0];
            var points = new[] { St.Graph.Home }.Concat(lane).Append(St.Graph.Portal).Distinct().Select(PosOf).ToArray();
            roadVerts = Map3DStage.Generate(this, PurchasedAssets.NatureRoadTool, "road_path", "generate_road", points);
            riverVerts = Map3DStage.Generate(this, PurchasedAssets.NatureRiverTool, "river_path", "generate_river",
                new[] { new Vector3(9, 0, -14), new Vector3(8, 0, 0), new Vector3(10, 0, 14) });
            ok = roadVerts > 0 && riverVerts > 0;
        }
        GD.Print($"MAP3D_TOOL_CHECK_COMPLETE ok={ok} tools={Map3DMode.ToolsAvailable} road_vertices={roadVerts} river_vertices={riverVerts}");
        if (!OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--map11-capture=", StringComparison.Ordinal))) GetTree().Quit(ok ? 0 : 1);
    }
}

/// <summary>
/// 3D のジオラマの根。<b>土台では空</b>——見た目の段がここに地形・道・駒を組む。
/// 道具（GDScript の <c>@tool</c>）を C# から呼ぶ口だけ持つ。
/// </summary>
public partial class Map3DStage : Node3D
{
    /// <summary>
    /// 購入素材の道具（道 ／ 川）を 1 本動かす。<paramref name="points"/> を結ぶ <see cref="Path3D"/> を作って道具に渡し、
    /// 道具が子に作ったメッシュの頂点の数を返す（0 なら作れていない）。原本の場面は書き換えない（実体を 1 つ作るだけ）。
    /// </summary>
    internal static int Generate(Node3D parent, string toolScene, string pathProperty, string method, Vector3[] points)
    {
        if (!ResourceLoader.Exists(toolScene) || points.Length < 2) return 0;
        var curve = new Curve3D();
        foreach (Vector3 p in points) curve.AddPoint(p);
        var path = new Path3D { Curve = curve };
        parent.AddChild(path);
        var tool = GD.Load<PackedScene>(toolScene).Instantiate<Node3D>();
        parent.AddChild(tool);
        tool.Set(pathProperty, path);
        tool.Call(method);
        return tool.GetChildren().OfType<MeshInstance3D>().Sum(m => m.Mesh?.GetFaces().Length ?? 0);
    }
}

/// <summary>F2 で 2D ／ 3D を切り替える（作戦ターンを流している最中は聞かない）。場面を読み直すだけで、通しの状態は `Map11Session` に残る。</summary>
public partial class Map3DSwitch : Node
{
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F2 } || Map11Session.TurnRunning) return;
        Map3DMode.Select(!Map3DMode.On);
        GetViewport().SetInputAsHandled();
        GetTree().ReloadCurrentScene();
    }
}

/// <summary>
/// 検証用の UI の層（F3 で出し入れ・既定は隠す）。遊ぶ人に見せる UI とは別の <see cref="CanvasLayer"/> にして、
/// seed・作戦ターン・fps・カメラの値を 1 か所に出す。見た目の段はここに足す（遊ぶ UI の側に検証の表示を混ぜない）。
/// </summary>
public partial class Map3DDebugLayer : CanvasLayer
{
    private readonly Func<Camera3D> _camera;
    private readonly Label _text = UiKit.Text("", 20, UiKit.Ink);

    public Map3DDebugLayer(Func<Camera3D> camera) { _camera = camera; }

    public override void _Ready()
    {
        Layer = 90;
        Visible = Map3DMode.DebugShown;
        var back = new PanelContainer { Position = new Vector2(24, 200), MouseFilter = Control.MouseFilterEnum.Ignore };
        back.AddChild(_text);
        AddChild(back);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F3 }) return;
        Visible = Map3DMode.DebugShown = !Map3DMode.DebugShown;
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (!Visible || Map11Session.State is not { } st) return;
        Camera3D c = _camera();
        Vector3 at = c.Position, rot = c.RotationDegrees;
        string lens = c.Projection == Camera3D.ProjectionType.Orthogonal ? $"平行 大きさ {c.Size:F1}" : $"画角 {c.Fov:F1}";
        _text.Text = $"検証（F3 で隠す ／ F2 で 2D）\n{st.Stage.Id}　seed {st.Seed}　作戦ターン {st.Turn}　戦闘 {st.Battles}\n"
            + $"fps {Engine.GetFramesPerSecond():F0}　道具 {(Map3DMode.ToolsAvailable ? "あり" : "なし")}\n"
            + $"カメラ 位置 ({at.X:F1}, {at.Y:F1}, {at.Z:F1})　傾き {-rot.X:F1}°　{lens}";
    }
}
