using Godot;

public partial class BattlefieldView3D
{
    private Control _turnLabel = null!;
    private Panel _turnPlate = null!;
    private Label _turnNumber = null!;
    private Tween? _turnLabelTween;
    private int _announcedTurn = -1;
    internal bool TurnLabelVisible => _turnLabel.Visible;
    internal string TurnLabelText => _turnNumber.Text;

    private void BuildTurnLabel()
    {
        _turnLabel = new Control
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            OffsetLeft = -142, OffsetRight = 142, OffsetTop = 102, OffsetBottom = 164,
            MouseFilter = MouseFilterEnum.Ignore, Visible = false,
        };
        AddChild(_turnLabel);
        _turnPlate = new Panel { Size = new Vector2(284, 62), MouseFilter = MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.025f, 0.055f, 0.06f, 0.94f),
            BorderColor = Color.FromHtml("#bda16a"),
            BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3, CornerRadiusBottomRight = 3,
            Skew = new Vector2(-0.16f, 0),
            ShadowColor = new Color(0, 0, 0, 0.35f), ShadowSize = 6,
        };
        _turnPlate.AddThemeStyleboxOverride("panel", style);
        _turnLabel.AddChild(_turnPlate);
        _turnNumber = UiKit.Text("", 30, Color.FromHtml("#fff1d0"));
        _turnNumber.Position = new Vector2(0, 6);
        _turnNumber.Size = new Vector2(284, 48);
        _turnNumber.HorizontalAlignment = HorizontalAlignment.Center;
        _turnNumber.VerticalAlignment = VerticalAlignment.Center;
        _turnNumber.MouseFilter = MouseFilterEnum.Ignore;
        _turnPlate.AddChild(_turnNumber);
        foreach (float x in new[] { 18f, 262f })
        {
            var accent = new ColorRect
            {
                Position = new Vector2(x, 25), Size = new Vector2(4, 12),
                RotationDegrees = 15, Color = Color.FromHtml("#e7c67d"),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _turnPlate.AddChild(accent);
        }
    }

    private void ResetTurnLabel()
    {
        _turnLabelTween?.Kill();
        _turnLabelTween = null;
        _announcedTurn = -1;
        _turnLabel.Visible = false;
    }

    private void AnnounceTurn(int turn)
    {
        if (turn <= 0 || turn == _announcedTurn) return;
        _announcedTurn = turn;
        _turnLabelTween?.Kill();
        _turnNumber.Text = $"TURN  {turn:00}";
        _turnLabel.Visible = true;
        _turnPlate.Position = new Vector2(-20, -6);
        _turnPlate.Modulate = new Color(1, 1, 1, 0);
        // 再生速度とは独立した実時間。次のターンが来たら古い表示を置き換える。
        var tween = _turnLabelTween = CreateTween();
        tween.TweenProperty(_turnPlate, "position", Vector2.Zero, 0.16)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(_turnPlate, "modulate:a", 1f, 0.12);
        tween.TweenInterval(0.76);
        tween.TweenProperty(_turnPlate, "modulate:a", 0f, 0.22);
        tween.Parallel().TweenProperty(_turnPlate, "position", new Vector2(12, -4), 0.22);
        tween.TweenCallback(Callable.From(() => _turnLabel.Visible = false));
    }
}
