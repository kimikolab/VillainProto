using Godot;
using System;
using System.Globalization;
using System.Linq;

/// <summary>
/// 3D のジオラマのカメラ（発注書 §2-2）。<b>固定の見下ろし</b>——回転は無く、拡大（ホイール）と横移動（右ドラッグ・矢印 ／ WASD）だけ。
/// <b>傾き・高さ・画角は書き出しの値</b>（<c>[Export]</c>）で、検証の層（F3）に今の値が出る。検証の層を出しているあいだは
/// T／G（傾き）・Y／H（高さ）・U／J（画角）で動かして決められる。起動の引数 <c>--map3d-cam=傾き,高さ,画角</c> でも渡せる。
/// 拡大と横移動は場面を読み直しても（戦闘から戻っても）残る。
/// </summary>
public partial class Map3DCamera : Node3D
{
    /// <summary>見下ろす角度（度・水平から下向き）。</summary>
    [Export(PropertyHint.Range, "30,80,0.5")] public float Tilt { get; set; } = 52f;
    /// <summary>カメラの高さ（地面から・拡大 1.0 のとき）。</summary>
    [Export(PropertyHint.Range, "15,90,0.5")] public float Height { get; set; } = 45f;
    /// <summary>画角（縦・度）。狭いほど箱庭らしく、広いほど奥行きが付く。</summary>
    [Export(PropertyHint.Range, "15,70,0.5")] public float Fov { get; set; } = 30f;
    /// <summary>拡大の下限と上限（1.0 が基準・小さいほど寄る）。</summary>
    [Export] public float ZoomNear { get; set; } = 0.50f;
    [Export] public float ZoomFar { get; set; } = 1.30f;
    /// <summary>横移動の限り（盤の真ん中からの距離）。</summary>
    [Export] public Vector2 PanLimit { get; set; } = new(19, 11);

    // 場面を読み直しても残す値（ステージが替わったら戻す）。
    private static string _stage = "";
    private static float _zoom = 1f, _zoomTarget = 1f;
    private static Vector2 _pan;
    private static Vector3? _tuned;   // 検証の層で動かした 傾き・高さ・画角

    private Vector3 _look;
    private float _reach = 1f;
    private bool _dragging;
    /// <summary>視点を動かせる範囲（見る点からの四角・第477期）。null なら <see cref="PanLimit"/> の ±。</summary>
    private Rect2? _range;
    /// <summary>寄せている先（見る点からのずれ・第477期）。右ドラッグ・矢印で動かしたら消える。</summary>
    private static Vector2? _glide;

    /// <summary>その場所が画面の真ん中に来るように、なめらかに寄せる（画面の外の知らせ・全体図を押したとき・出来事で時計が止まったとき）。</summary>
    internal void FocusOn(Vector3 world, bool snap = false)
    {
        Vector2 want = Clamp(new Vector2(world.X - _look.X, world.Z - _look.Z));
        if (snap) { _pan = want; _glide = null; Apply(); } else _glide = want;
    }

    private Vector2 Clamp(Vector2 p) => _range is { } r
        ? new Vector2(Mathf.Clamp(p.X, r.Position.X, r.End.X), Mathf.Clamp(p.Y, r.Position.Y, r.End.Y))
        : new Vector2(Mathf.Clamp(p.X, -PanLimit.X, PanLimit.X), Mathf.Clamp(p.Y, -PanLimit.Y, PanLimit.Y));

    /// <summary>いま地面のどこが画面に入っているか（x, z の四角・全体図の枠に使う・およその値）。</summary>
    internal Rect2 Seen()
    {
        float tilt = Mathf.DegToRad(Tilt);
        float distance = Height / Mathf.Sin(tilt) * _zoom * _reach;
        float halfZ = distance * Mathf.Tan(Mathf.DegToRad(Fov) / 2) / Mathf.Sin(tilt);
        Vector2 size = GetViewport()?.GetVisibleRect().Size ?? new Vector2(16, 9);
        float halfX = distance * Mathf.Tan(Mathf.DegToRad(Fov) / 2) * size.X / Math.Max(1, size.Y);
        Vector3 f = Focus;
        return new Rect2(f.X - halfX, f.Z - halfZ, halfX * 2, halfZ * 2);
    }

    public Camera3D Camera { get; } = new() { Near = 1f, Far = 400f };
    /// <summary>今の窓（引き出し）が開いているあいだ、盤を横へ寄せる量。</summary>
    internal float Shift { get; set; }
    internal float Zoom => _zoom;
    internal Vector3 Focus => _look + new Vector3(_pan.X + Shift, 0, _pan.Y);

    internal void Setup(string stageId, Vector3 look, float reach = 1f, Rect2? range = null)
    {
        _look = look;
        _reach = reach;
        _range = range;
        if (_stage != stageId) { _stage = stageId; _zoom = _zoomTarget = 1f; _pan = Vector2.Zero; _glide = null; }
        _pan = Clamp(_pan);
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--map3d-cam=", StringComparison.Ordinal)) is { } arg && _tuned is null)
        {
            float[] v = arg["--map3d-cam=".Length..].Split(',')
                .Select(t => float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : float.NaN).ToArray();
            if (v.Length == 3 && v.All(f => !float.IsNaN(f))) _tuned = new Vector3(v[0], v[1], v[2]);
        }
        if (_tuned is { } t) { Tilt = t.X; Height = t.Y; Fov = t.Z; }
        AddChild(Camera);
        Apply();
    }

    /// <summary>今の値でカメラを置く。</summary>
    internal void Apply()
    {
        float tilt = Mathf.DegToRad(Tilt);
        float distance = Height / Mathf.Sin(tilt) * _zoom * _reach;
        Vector3 focus = Focus;
        Camera.Fov = Fov;
        Camera.Projection = Camera3D.ProjectionType.Perspective;
        Camera.Position = focus + new Vector3(0, Mathf.Sin(tilt), Mathf.Cos(tilt)) * distance;
        Camera.LookAt(focus, Vector3.Up);
        if (_shake > 0) Camera.Position += new Vector3((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-0.5, 0.5), (float)GD.RandRange(-1.0, 1.0)) * _shakeAmp * (_shake / _shakeLife);
    }

    private float _shake, _shakeLife = 1, _shakeAmp;
    /// <summary>画面を短く揺らす（第480期・勇者の登場）。見た目だけ。</summary>
    internal void Shake(float seconds, float amp) { _shake = _shakeLife = Math.Max(0.05f, seconds); _shakeAmp = amp; }

    /// <summary>引き具合を決める（第479期・3D のレイアウトの検査が「引いた視点」を作る）。</summary>
    internal void SetZoom(float zoom) { _zoom = _zoomTarget = Mathf.Clamp(zoom, ZoomNear, ZoomFar); Apply(); }

    internal void ResetView() { _zoom = _zoomTarget = 1f; _pan = Clamp(Vector2.Zero); _glide = null; Apply(); }

    private void Pan(Vector2 by)
    {
        _glide = null;
        _pan = Clamp(_pan + by);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                _zoomTarget = Mathf.Clamp(_zoomTarget * 0.90f, ZoomNear, ZoomFar); break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                _zoomTarget = Mathf.Clamp(_zoomTarget * 1.11f, ZoomNear, ZoomFar); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right or MouseButton.Middle } button:
                _dragging = button.Pressed; break;
            case InputEventMouseMotion motion when _dragging:
            {
                // 画面の 1 画素が地面で何単位か（盤の真ん中あたりの見かけの大きさ）。
                float perPixel = 2 * Height / Mathf.Sin(Mathf.DegToRad(Tilt)) * _zoom * Mathf.Tan(Mathf.DegToRad(Fov) / 2)
                    / Math.Max(1, GetViewport().GetVisibleRect().Size.Y);
                Pan(new Vector2(-motion.Relative.X * perPixel, -motion.Relative.Y * perPixel / Mathf.Sin(Mathf.DegToRad(Tilt))));
                break;
            }
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Home }:
                ResetView(); break;
            case InputEventKey { Pressed: true, Keycode: var key } when Map3DMode.DebugShown:
            {
                // 検証の層を出しているあいだだけ、傾き・高さ・画角を動かせる（決めた値は報告か `[Export]` の既定へ写す）。
                float step = 1f;
                switch (key)
                {
                    case Key.T: Tilt = Mathf.Clamp(Tilt + step, 30, 80); break;
                    case Key.G: Tilt = Mathf.Clamp(Tilt - step, 30, 80); break;
                    case Key.Y: Height = Mathf.Clamp(Height + step, 15, 90); break;
                    case Key.H: Height = Mathf.Clamp(Height - step, 15, 90); break;
                    case Key.U: Fov = Mathf.Clamp(Fov + step, 15, 70); break;
                    case Key.J: Fov = Mathf.Clamp(Fov - step, 15, 70); break;
                    default: return;
                }
                _tuned = new Vector3(Tilt, Height, Fov);
                break;
            }
            default: return;
        }
    }

    public override void _Process(double delta)
    {
        Vector2 keys = Vector2.Zero;
        if (Input.IsKeyPressed(Key.Left) || Input.IsKeyPressed(Key.A)) keys.X -= 1;
        if (Input.IsKeyPressed(Key.Right) || Input.IsKeyPressed(Key.D)) keys.X += 1;
        if (Input.IsKeyPressed(Key.Up) || Input.IsKeyPressed(Key.W)) keys.Y -= 1;
        if (Input.IsKeyPressed(Key.Down) || Input.IsKeyPressed(Key.S)) keys.Y += 1;
        if (keys != Vector2.Zero && GetViewport().GuiGetFocusOwner() is null) Pan(keys * (float)delta * 16f * _zoom);
        _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1 - Mathf.Exp(-(float)delta * 12f));
        _shake = Math.Max(0, _shake - (float)delta);
        if (_glide is { } to)
        {
            _pan = _pan.Lerp(to, 1 - Mathf.Exp(-(float)delta * 6f));
            if (_pan.DistanceTo(to) < 0.05f) { _pan = to; _glide = null; }
        }
        Apply();
    }
}
