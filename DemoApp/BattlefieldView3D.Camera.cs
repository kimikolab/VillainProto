using Godot;

public partial class BattlefieldView3D
{
    private static readonly float DefaultCameraElevation = Mathf.RadToDeg(Mathf.Atan2(7.15f, 12.9f));
    private static readonly float CameraRadius = new Vector2(7.15f, 12.9f).Length();
    private Tween? _cameraMotion;
    private HSlider _cameraSlider = null!;
    private Label _cameraAngleLabel = null!;
    private HSlider _cameraYawSlider = null!;
    private HSlider _cameraZoomSlider = null!;
    private Label _cameraYawLabel = null!;
    private Label _cameraZoomLabel = null!;
    public float CameraElevation { get; private set; } = DefaultCameraElevation;
    public float CameraYaw { get; private set; }
    public float CameraZoom { get; private set; } = 1;

    private void BuildCameraControls()
    {
        var panel = new PanelContainer
        {
            AnchorLeft = 1, AnchorRight = 1,
            OffsetLeft = -310, OffsetRight = -18, OffsetTop = 12, OffsetBottom = 12,
        };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Shadow, UiKit.Line, 1, 10));
        var layout = new VBoxContainer();
        var toggle = new Button
        {
            Text = "▶ 視点を調節", ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            TooltipText = "上下・左右・ズームの調節を開閉します。",
        };
        toggle.AddThemeFontSizeOverride("font_size", 12);
        layout.AddChild(toggle);
        var rows = new VBoxContainer { Visible = false };
        toggle.Toggled += expanded =>
        {
            rows.Visible = expanded;
            toggle.Text = expanded ? "▼ 視点を調節" : "▶ 視点を調節";
            // 閉じた後に透明なパネル領域が戦場を覆わないよう、高さも縮める。
            panel.CallDeferred(Control.MethodName.ResetSize);
        };
        var heading = new HBoxContainer();
        _cameraAngleLabel = UiKit.Text("", 12, UiKit.Player);
        _cameraAngleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        heading.AddChild(_cameraAngleLabel);
        var reset = new Button { Text = "元の視点", FocusMode = FocusModeEnum.None };
        reset.AddThemeFontSizeOverride("font_size", 11);
        reset.Pressed += ResetCameraView;
        heading.AddChild(reset);
        rows.AddChild(heading);
        var controls = new HBoxContainer();
        controls.AddChild(UiKit.Text("横", 11, UiKit.Muted));
        _cameraSlider = new HSlider
        {
            MinValue = 0, MaxValue = 50, Step = 0.5,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(150, 22),
            FocusMode = FocusModeEnum.None,
            TooltipText = "視点の角度を変更（0° は水平）。再生中・一時停止中とも調節できます。",
        };
        _cameraSlider.ValueChanged += angle => SetCameraElevation((float)angle);
        controls.AddChild(_cameraSlider);
        controls.AddChild(UiKit.Text("俯瞰", 11, UiKit.Muted));
        rows.AddChild(controls);
        _cameraYawLabel = UiKit.Text("", 11, UiKit.Player);
        rows.AddChild(_cameraYawLabel);
        _cameraYawSlider = AddCameraSlider(rows, "左", "右", -35, 35, 1,
            "戦場の中央を見ながら左右へ回り込みます。", value => SetCameraYaw((float)value));
        _cameraZoomLabel = UiKit.Text("", 11, UiKit.Player);
        rows.AddChild(_cameraZoomLabel);
        _cameraZoomSlider = AddCameraSlider(rows, "遠", "近", 0.75, 1.3, 0.01,
            "カメラを前後に移動します。近づくと画面外に出る駒もあります。", value => SetCameraZoom((float)value));
        layout.AddChild(rows);
        panel.AddChild(layout);
        AddChild(panel);
        SetCameraElevation(CameraElevation);
    }

    public void SetCameraElevation(float degrees)
    {
        CameraElevation = Mathf.Clamp(degrees, 0, 50);
        ApplyCameraView();
    }

    public void SetCameraYaw(float degrees)
    {
        CameraYaw = Mathf.Clamp(degrees, -35, 35);
        ApplyCameraView();
    }

    public void SetCameraZoom(float zoom)
    {
        CameraZoom = Mathf.Clamp(zoom, 0.75f, 1.3f);
        ApplyCameraView();
    }

    public void ResetCameraView()
    {
        CameraElevation = DefaultCameraElevation;
        CameraYaw = 0;
        CameraZoom = 1;
        ApplyCameraView();
    }

    private static HSlider AddCameraSlider(VBoxContainer rows, string left, string right,
        double min, double max, double step, string tooltip, System.Action<double> changed)
    {
        var row = new HBoxContainer();
        row.AddChild(UiKit.Text(left, 11, UiKit.Muted));
        var slider = new HSlider
        {
            MinValue = min, MaxValue = max, Step = step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(150, 22), FocusMode = FocusModeEnum.None,
            TooltipText = tooltip,
        };
        slider.ValueChanged += value => changed(value);
        row.AddChild(slider);
        row.AddChild(UiKit.Text(right, 11, UiKit.Muted));
        rows.AddChild(row);
        return slider;
    }

    private void ApplyCameraView()
    {
        float radians = Mathf.DegToRad(CameraElevation);
        float yaw = Mathf.DegToRad(CameraYaw);
        float radius = CameraRadius / CameraZoom;
        // 注視点からの距離を保って回り込む。再生や攻撃の復帰先もこの位置に統一する。
        _cameraHome = CameraFocus + new Vector3(Mathf.Sin(yaw) * Mathf.Cos(radians),
            Mathf.Sin(radians), Mathf.Cos(yaw) * Mathf.Cos(radians)) * radius;
        _cameraMotion?.Kill();
        _camera.Position = _cameraHome;
        _camera.Fov = CameraFov;
        _camera.LookAt(CameraFocus, Vector3.Up);
        _cameraSlider.SetValueNoSignal(CameraElevation);
        _cameraAngleLabel.Text = $"視点  {CameraElevation:0}°";
        _cameraYawSlider.SetValueNoSignal(CameraYaw);
        _cameraZoomSlider.SetValueNoSignal(CameraZoom);
        _cameraYawLabel.Text = $"左右  {CameraYaw:+0;-0;0}°";
        _cameraZoomLabel.Text = $"ズーム  {CameraZoom:0.00}×";
    }
}
