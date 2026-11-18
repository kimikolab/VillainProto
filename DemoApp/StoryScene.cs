using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 会話の画面（第481期・指示書 design/PHASE481_STORY_PART_SPEC.md §1）。**判定は 1 つも持たない**——台本（`StoryScript`）を順に見せるだけ。
//
//   背景     いま映っている盤を、暗く（明るさ 40%）・少しぼかす（専用の背景絵は作らない・視点を流すのは呼ぶ側）
//   立ち絵   左右に 1 人ずつ。話している側を明るく・手前に、もう一方は暗く（入れ替わり 0.2 秒）
//   帯       画面の下 1／4。左上に名札（駒の名前の短い形）・本文は 2 行まで・文字は 1920×1080 で 34px
//   内心     立ち絵を両方暗くし、帯を紺に・名札なし・斜体で中央寄せ
//   影       立ち絵の無い人物は、人の形の黒い影（1 つの形を使い回す）＋ 名札
//   送り     クリック ／ Space ／ Enter。文字は 1 字ずつ出て、出ている途中に押すと全部出る
//   飛ばす   右上の「スキップ」（場面の終わりへ）
//   演出     場面の頭に題の帯（1.2 秒）・`（暗転）`・締めの帯 `（帯）`
// =====================================================================================
public partial class StoryScene : Control
{
    public const float TitleSeconds = 1.2f, SwapSeconds = 0.2f, CharsPerSecond = 30f, FadeSeconds = 0.5f, DimLevel = 0.4f;
    /// <summary>文字の大きさ（1920×1080 のとき）。</summary>
    public const int BodyFont = 34, NameFont = 28;
    /// <summary>ふつうに読む速さ（1 秒あたりの字数）と、1 行ごとの間（自動送り・かかった秒数の見積もりに使う）。</summary>
    public const float ReadCharsPerSecond = 9f, ReadPause = 0.5f;

    /// <summary>場面が終わった（飛ばしたときも）。</summary>
    public event Action? Finished;
    /// <summary>暗転の真ん中（画面が真っ黒のとき）に 1 度呼ぶ——呼ぶ側が背景の視点を替える。</summary>
    public Action? AtBlackout { get; set; }
    /// <summary>自動送り（遊ぶ口・ふつうに読む速さで送る）。</summary>
    public bool Auto { get; set; }

    private enum Phase { Idle, Title, Line, FadeOut, Hold, FadeIn, Band, Ending }
    private Phase _phase = Phase.Idle;
    private IReadOnlyList<StoryScript.Line> _lines = Array.Empty<StoryScript.Line>();
    private string _title = "";
    private int _at = -1;
    private float _clock, _shown, _black, _autoWait, _titleShow, _bandShow, _all = 1f;
    private bool _blackoutDone;
    /// <summary>場面にかかった秒数（題の帯から終わりまで）。</summary>
    public float Elapsed { get; private set; }
    public int SceneNo { get; private set; } = -1;
    public bool Playing => _phase != Phase.Idle;

    private readonly ColorRect _back = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Slot _left = new(false), _right = new(true);
    private readonly Label _body = new() { AutowrapMode = TextServer.AutowrapMode.Arbitrary, MouseFilter = MouseFilterEnum.Ignore, ClipText = false };
    private readonly Label _name = new() { MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _skip = UiKit.ActionButton("スキップ", UiKit.Muted);
    private readonly Front _front;
    private static FontVariation? _italic;
    private static ShaderMaterial? _blur;
    private bool _inner;

    public StoryScene()
    {
        _front = new Front(this);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _blur ??= new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = @"shader_type canvas_item;
uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform float lod = 2.4;
uniform float dim = 0.4;
uniform float show = 1.0;
void fragment() {
    vec3 sharp = textureLod(screen, SCREEN_UV, 0.0).rgb;
    vec3 soft = textureLod(screen, SCREEN_UV, lod).rgb;
    COLOR = vec4(mix(sharp, soft * dim, show), 1.0);
}",
            },
        };
        _back.Material = _blur;
        AddChild(_back);
        _back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_left);
        AddChild(_right);
        AddChild(_front);
        _front.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _italic ??= new FontVariation { BaseFont = Map3DFonts.Regular, VariationTransform = new Transform2D(1f, 0.2f, 0f, 1f, 0f, 0f) };
        _body.AddThemeColorOverride("font_color", Color.FromHtml("#f2f0e6"));
        _body.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        AddChild(_body);
        _name.AddThemeFontOverride("font", Map3DFonts.Bold);
        _name.AddThemeColorOverride("font_color", Color.FromHtml("#ffe6a8"));
        AddChild(_name);
        _skip.Pressed += SkipAll;
        AddChild(_skip);
        Resized += Layout;
        Layout();
    }

    // ---------------- 置き方（1920×1080 を基準に、画面の高さで拡げる） ----------------

    private float S => Math.Max(0.4f, Size.Y / 1080f);
    /// <summary>帯（画面の下 1／4）。</summary>
    public Rect2 BandRect => new(0, Size.Y * 0.75f, Size.X, Size.Y * 0.25f);
    private float ColumnWidth => Math.Min(Size.X - 480 * S, 1560 * S);
    private float ColumnX => (Size.X - ColumnWidth) / 2;
    public Rect2 NameRect => _name.Visible ? new Rect2(_name.Position, _name.Size) : default;
    public Rect2 BodyRect => new(_body.Position, _body.Size);
    /// <summary>本文がいま何行か・本文の下の端。</summary>
    public int BodyLines => _body.GetLineCount();
    public float BodyBottom => _body.Position.Y + _body.GetLineCount() * _body.GetLineHeight() + Math.Max(0, _body.GetLineCount() - 1) * _body.GetThemeConstant("line_spacing");
    public Rect2 PortraitRect(bool right) => (right ? _right : _left).Shown;
    public bool PortraitLit(bool right) => (right ? _right : _left).Lit;
    public bool PortraitShadow(bool right) => (right ? _right : _left).IsShadow;
    public string NameText => _name.Visible ? _name.Text : "";
    public string BodyText => _body.Text;
    public bool InnerShown => _inner;
    public string TitleBandText => _titleShow > 0.01f ? _title : "";
    public string EndBandText => _bandShow > 0.01f ? _front.BandText : "";
    public bool Black => _black > 0.99f;
    public bool SkipShown => _skip.Visible;

    private void Layout()
    {
        if (!IsInsideTree()) return;
        float s = S;
        Rect2 band = BandRect;
        _body.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(BodyFont * s));
        _body.AddThemeConstantOverride("line_spacing", Mathf.RoundToInt(12 * s));
        _body.AddThemeConstantOverride("outline_size", Mathf.RoundToInt(4 * s));
        _body.Position = new Vector2(ColumnX, band.Position.Y + 84 * s);
        _body.Size = new Vector2(ColumnWidth, band.Size.Y - 100 * s);
        _name.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(NameFont * s));
        _name.AddThemeStyleboxOverride("normal", Map3DHud.Box(new Color(0.10f, 0.08f, 0.03f, 0.96f), Color.FromHtml("#f1c96b"), 2, 8, Mathf.RoundToInt(14 * s)));
        _name.Position = new Vector2(ColumnX, band.Position.Y + 16 * s);
        _name.ResetSize();
        _skip.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(22 * s));
        _skip.Size = new Vector2(150 * s, 50 * s);
        _skip.Position = new Vector2(Size.X - 180 * s, 28 * s);
        // 立ち絵の箱: 帯の上の端に立つ（帯と名札に重ならない）。左右は画面の真ん中から同じだけ離す。
        float boxW = 560 * s, boxH = 690 * s, off = Math.Min(Size.X * 0.29f, 600 * s);
        _left.Box = new Rect2(Size.X / 2 - off - boxW / 2, band.Position.Y - boxH, boxW, boxH);
        _right.Box = new Rect2(Size.X / 2 + off - boxW / 2, band.Position.Y - boxH, boxW, boxH);
        _front.QueueRedraw();
    }

    // ---------------- 流れ ----------------

    /// <summary>場面を始める。<paramref name="outcome"/> は 1-3 の結果（`?` の行を選ぶ・無ければ null）。</summary>
    public void Play(StoryScript.Scene scene, string? outcome, bool allowSkip = true)
    {
        SceneNo = scene.No;
        _lines = scene.For(outcome);
        _title = scene.Title;
        _at = -1;
        _clock = Elapsed = _black = _bandShow = 0;
        _blackoutDone = false;
        _all = 0;
        _left.Clear(); _right.Clear();
        _skip.Visible = allowSkip;
        _body.Text = ""; _name.Visible = false; _inner = false;
        Visible = true;
        _phase = Phase.Title;
        Layout();
        GD.Print($"STORY_SCENE_BEGIN 場面 {scene.No}「{scene.Title}」行 {_lines.Count} 結果 {outcome ?? "—"}");
    }

    /// <summary>クリック ／ Space ／ Enter。出ている途中なら全部出す・出切っていれば次へ。</summary>
    public void Advance()
    {
        switch (_phase)
        {
            case Phase.Title: _clock = TitleSeconds; break;
            case Phase.Line:
                if (_shown < _body.Text.Length) { _shown = _body.Text.Length; _body.VisibleCharacters = -1; _autoWait = AutoWaitOf(); }
                else Next();
                break;
            case Phase.Band: End(); break;
        }
    }

    /// <summary>場面の終わりへ飛ばす。</summary>
    public void SkipAll()
    {
        if (_phase is Phase.Idle or Phase.Ending) return;
        GD.Print($"STORY_SCENE_SKIP 場面 {SceneNo}（{_at + 1} 行目で）");
        if (!_blackoutDone && _lines.Any(l => l.Kind == StoryScript.Kind.Blackout)) { _blackoutDone = true; AtBlackout?.Invoke(); }
        End();
    }

    private void End()
    {
        _phase = Phase.Ending;
        _clock = 0;
    }

    private float AutoWaitOf() => _body.Text.Length / ReadCharsPerSecond + ReadPause;

    private void Next()
    {
        _at++;
        if (_at >= _lines.Count) { End(); return; }
        StoryScript.Line line = _lines[_at];
        switch (line.Kind)
        {
            case StoryScript.Kind.Blackout:
                _phase = Phase.FadeOut; _clock = 0;
                return;
            case StoryScript.Kind.Band:
                _front.BandText = line.Text;
                _body.Text = ""; _name.Visible = false; _inner = false;
                _left.Lit = _right.Lit = false;
                _phase = Phase.Band; _clock = 0; _autoWait = 2.5f;
                return;
        }
        _phase = Phase.Line;
        _inner = line.Side == StoryScript.Side.Inner;
        if (_inner) _left.Lit = _right.Lit = false;
        else
        {
            Slot on = line.Side == StoryScript.Side.Right ? _right : _left, other = line.Side == StoryScript.Side.Right ? _left : _right;
            on.Set(line.Speaker, line.Shadow);
            on.Lit = true;
            other.Lit = false;
            MoveChild(on, _front.GetIndex() - 1);   // 話している側を手前に
        }
        _name.Text = line.Speaker;
        _name.Visible = !_inner;
        _name.ResetSize();
        _body.AddThemeFontOverride("font", _inner ? _italic! : Map3DFonts.Regular);
        _body.HorizontalAlignment = _inner ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _body.AddThemeColorOverride("font_color", _inner ? Color.FromHtml("#cfdcff") : Color.FromHtml("#f2f0e6"));
        _body.Text = line.Text;
        _body.VisibleCharacters = 0;
        _shown = 0;
        _autoWait = AutoWaitOf();
        _front.QueueRedraw();
        GD.Print($"STORY_LINE 場面 {SceneNo} {_at + 1}／{_lines.Count} {(_inner ? "内心" : line.Speaker + (line.Shadow ? "（影）" : ""))}｜{line.Text}");
    }

    /// <summary>検査・写しの口: その行を出し切った姿にする（入れ替わりの動きも終わらせる）。</summary>
    public void ShowForCheck(int index)
    {
        if (index < 0 || index >= _lines.Count) return;
        _clock = TitleSeconds; _titleShow = 0; _all = 1; _black = 0;
        _at = index - 1;
        Next();
        if (_phase == Phase.Line) { _shown = _body.Text.Length; _body.VisibleCharacters = -1; }
        if (_phase == Phase.FadeOut) { _phase = Phase.Hold; _black = 1; _clock = 0; _left.Clear(); _right.Clear(); _body.Text = ""; _name.Visible = false; _inner = false; }   // 暗転で立ち絵は入れ替わる
        if (_phase == Phase.Band) _bandShow = 1;
        _left.Settle(); _right.Settle();
        Apply();
    }

    /// <summary>検査・写しの口: 題の帯が出ている姿にする。</summary>
    public void ShowTitleForCheck() { _phase = Phase.Title; _clock = TitleSeconds * 0.5f; _titleShow = 1; _all = 1; Apply(); }

    /// <summary>その場面のその結果の行（検査が「どの行が左か・右か・内心か・影か」を引く）。</summary>
    public IReadOnlyList<StoryScript.Line> Lines => _lines;

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { Advance(); AcceptEvent(); }
    }

    public override void _Input(InputEvent e)
    {
        if (!Playing) return;
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space or Key.Enter or Key.KpEnter })
        {
            Advance();
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventKey) GetViewport().SetInputAsHandled();   // 会話のあいだ、下の画面のキー（F2・視点）は聞かない
    }

    public override void _Process(double delta)
    {
        if (_phase == Phase.Idle) return;
        float dt = (float)delta;
        _clock += dt;
        if (_phase != Phase.Ending) Elapsed += dt;
        _all = Mathf.MoveToward(_all, _phase == Phase.Ending ? 0 : 1, dt / 0.35f);
        _titleShow = Mathf.MoveToward(_titleShow, _phase == Phase.Title && _clock < TitleSeconds - 0.2f ? 1 : 0, dt / 0.2f);
        _bandShow = Mathf.MoveToward(_bandShow, _phase == Phase.Band ? 1 : 0, dt / 0.4f);
        switch (_phase)
        {
            case Phase.Title:
                if (_clock >= TitleSeconds) Next();
                break;
            case Phase.Line:
                if (_shown < _body.Text.Length)
                {
                    _shown = Math.Min(_body.Text.Length, _shown + dt * CharsPerSecond);
                    _body.VisibleCharacters = _shown >= _body.Text.Length ? -1 : (int)_shown;
                }
                if (Auto && (_autoWait -= dt) <= 0) Next();
                break;
            case Phase.FadeOut:
                _black = Mathf.MoveToward(_black, 1, dt / FadeSeconds);
                if (_black >= 1) { _phase = Phase.Hold; _clock = 0; }
                break;
            case Phase.Hold:
                if (_clock >= 0.4f)
                {
                    if (!_blackoutDone) { _blackoutDone = true; AtBlackout?.Invoke(); }
                    _left.Clear(); _right.Clear();
                    _body.Text = ""; _name.Visible = false; _inner = false;
                    _phase = Phase.FadeIn;
                }
                break;
            case Phase.FadeIn:
                _black = Mathf.MoveToward(_black, 0, dt / FadeSeconds);
                if (_black <= 0) Next();
                break;
            case Phase.Band:
                if (Auto && (_autoWait -= dt) <= 0) End();
                break;
            case Phase.Ending:
                if (_all <= 0)
                {
                    _phase = Phase.Idle;
                    Visible = false;
                    GD.Print($"STORY_SCENE_END 場面 {SceneNo} {Elapsed:F1} 秒");
                    Finished?.Invoke();
                    return;
                }
                break;
        }
        _left.Tick(dt); _right.Tick(dt);
        Apply();
    }

    private void Apply()
    {
        _blur!.SetShaderParameter("show", _all);
        _blur.SetShaderParameter("dim", DimLevel);
        _left.Fade = _right.Fade = _all;
        _left.Apply(); _right.Apply();
        _body.Modulate = _name.Modulate = _skip.Modulate = new Color(1, 1, 1, _all);
        _front.QueueRedraw();
    }

    /// <summary>ふつうに読んでかかる秒数の見積もり（題の帯 ＋ 1 行ごとに 字数 ÷ 9 ＋ 0.5 秒 ＋ 暗転 1.4 秒 ＋ 締めの帯 2.5 秒）。</summary>
    public static float ReadSeconds(StoryScript.Scene scene, string? outcome)
        => TitleSeconds + scene.For(outcome).Sum(l => l.Kind == StoryScript.Kind.Say ? l.Text.Length / ReadCharsPerSecond + ReadPause
            : l.Kind == StoryScript.Kind.Blackout ? FadeSeconds * 2 + 0.4f : 2.5f);

    private static Shader? _cutoutShader;
    /// <summary>
    /// 立ち絵の背景を抜く（戦闘の画面の `UiKit.PortraitCanvasMaterial` と同じ決め方: 四隅が透けていれば素材のアルファ・単色の背景なら色の差で抜く）。
    /// 明るさと透け（`tint`）を自分で掛けるので、立ち絵ごとに材質を 1 つ持つ。
    /// </summary>
    private static ShaderMaterial Cutout()
    {
        _cutoutShader ??= new Shader
        {
            Code = @"shader_type canvas_item;
uniform vec4 tint : source_color = vec4(1.0);
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    vec4 top_left = texture(TEXTURE, vec2(0.02, 0.02));
    vec4 top_right = texture(TEXTURE, vec2(0.98, 0.02));
    vec4 bottom_left = texture(TEXTURE, vec2(0.02, 0.98));
    vec4 bottom_right = texture(TEXTURE, vec2(0.98, 0.98));
    vec3 top_bg = mix(top_left.rgb, top_right.rgb, UV.x);
    vec3 bottom_bg = mix(bottom_left.rgb, bottom_right.rgb, UV.x);
    vec3 expected_bg = mix(top_bg, bottom_bg, UV.y);
    float silhouette = smoothstep(0.075, 0.19, distance(c.rgb, expected_bg));
    float corner_alpha = min(min(top_left.a, top_right.a), min(bottom_left.a, bottom_right.a));
    float has_alpha_background = 1.0 - step(0.08, corner_alpha);
    float mask = mix(silhouette, 1.0, has_alpha_background);
    if (has_alpha_background < 0.5 && expected_bg.g > 0.8 && max(expected_bg.r, expected_bg.b) < 0.15) {
        mask = 1.0 - smoothstep(0.02, 0.18, c.g - max(c.r, c.b));
        c.g = min(c.g, max(c.r, c.b));
    }
    COLOR = vec4(c.rgb * tint.rgb, c.a * mask * tint.a);
}",
        };
        return new ShaderMaterial { Shader = _cutoutShader };
    }

    /// <summary>立ち絵 1 人ぶん（左 ／ 右）。絵が無ければ人の形の影。</summary>
    private sealed partial class Slot : Control
    {
        private readonly bool _rightSide;
        private readonly TextureRect _art = new() { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        private string _who = "";
        private float _lit, _in;
        public Rect2 Box;
        public bool Lit;
        public float Fade = 1;
        public bool IsShadow { get; private set; }
        public bool Has => _who.Length > 0;
        /// <summary>いま絵（または影）を描いている四角（画面の座標）。</summary>
        public Rect2 Shown { get; private set; }

        public Slot(bool right)
        {
            _rightSide = right;
            MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_art);
            _art.Material = Cutout();
        }

        public void Clear() { _who = ""; _art.Texture = null; IsShadow = false; Lit = false; _lit = 0; Shown = default; QueueRedraw(); }

        public void Set(string speaker, bool shadow)
        {
            if (_who == speaker) return;
            _who = speaker;
            _in = 0;
            Texture2D? tex = null;
            bool facesRight = true;
            if (!shadow)
            {
                if (speaker == StoryScript.HeroSpeaker) { tex = HunterArt.Standing(); facesRight = false; }
                else if (StoryScript.UnitOf(speaker) is { } def)
                    tex = UiKit.HasCustomBattlePortrait(def.Id) ? UiKit.BattlePortrait(null!, def.Id) : UiKit.HasCustomPortrait(def.Id) ? UiKit.Portrait(null!, def.Id) : null;
            }
            IsShadow = tex is null;
            _art.Texture = tex;
            _art.Visible = tex is not null;
            // 絵は画面の内側を向く（左の人は右向き・右の人は左向き）。
            _art.FlipH = tex is not null && facesRight == _rightSide;
            QueueRedraw();
        }

        public void Settle() { _lit = Lit ? 1 : 0; _in = 1; }

        public void Tick(float dt)
        {
            _lit = Mathf.MoveToward(_lit, Lit ? 1 : 0, dt / SwapSeconds);
            _in = Mathf.MoveToward(_in, 1, dt / SwapSeconds);
        }

        public void Apply()
        {
            if (!Has) { Visible = false; return; }
            Visible = true;
            // 話している側は少し大きく（手前）・明るく。もう一方は小さく・暗く。
            float scale = Mathf.Lerp(0.90f, 1.0f, _lit);
            float bright = Mathf.Lerp(0.34f, 1.0f, _lit);
            Vector2 box = Box.Size * scale;
            Vector2 size = box;
            if (_art.Texture is { } t)
            {
                float aspect = t.GetWidth() / (float)Math.Max(1, t.GetHeight());
                size = aspect > box.X / box.Y ? new Vector2(box.X, box.X / aspect) : new Vector2(box.Y * aspect, box.Y);
            }
            else size = new Vector2(box.Y * 0.56f, box.Y * 0.92f);
            Vector2 at = new(Box.Position.X + Box.Size.X / 2 - size.X / 2, Box.End.Y - size.Y);
            Shown = new Rect2(at, size);
            _art.Position = at;
            _art.Size = size;
            ((ShaderMaterial)_art.Material).SetShaderParameter("tint", new Color(bright, bright, bright, _in * Fade));
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (!Has || !IsShadow) return;
            // 人の形の影（頭・首・肩から裾へ広がる外套）。1 つの形を誰にでも使い回す。
            Rect2 r = Shown;
            float a = _in * Fade;
            Color ink = new(0.015f, 0.015f, 0.025f, 0.97f * a), rim = new Color(0.42f, 0.47f, 0.58f, Mathf.Lerp(0.25f, 0.75f, _lit) * a);
            Vector2 P(float x, float y) => r.Position + new Vector2(_rightSide ? 1 - x : x, y) * r.Size;
            var body = new[]
            {
                P(0.40f, 0.235f), P(0.60f, 0.235f), P(0.63f, 0.29f), P(0.86f, 0.345f), P(0.95f, 0.47f), P(0.99f, 1f),
                P(0.01f, 1f), P(0.05f, 0.47f), P(0.14f, 0.345f), P(0.37f, 0.29f),
            };
            DrawColoredPolygon(body, ink);
            DrawPolyline(body.Take(6).ToArray(), rim, 3f, true);
            DrawPolyline(body.Skip(6).Append(body[0]).ToArray(), rim, 3f, true);
            Vector2 head = P(0.5f, 0.135f);
            float rad = r.Size.Y * 0.115f;
            DrawCircle(head, rad, ink);
            DrawArc(head, rad, 0, Mathf.Tau, 48, rim, 3f, true);
        }
    }

    /// <summary>帯・題の帯・締めの帯・暗転（立ち絵の手前に描く）。</summary>
    private sealed partial class Front : Control
    {
        private readonly StoryScene _s;
        public string BandText = "";
        public Front(StoryScene scene) { _s = scene; MouseFilter = MouseFilterEnum.Ignore; }

        public override void _Draw()
        {
            float s = _s.S, all = _s._all;
            Vector2 size = Size;
            Font bold = Map3DFonts.Bold;
            // ---- 文字の帯（下 1／4）: ふつうは黒・内心は紺 ----
            Rect2 band = _s.BandRect;
            Color back = _s._inner ? new Color(0.06f, 0.09f, 0.27f, 0.93f) : new Color(0.03f, 0.035f, 0.05f, 0.92f);
            Color edge = _s._inner ? Color.FromHtml("#7f9bff") : Color.FromHtml("#f1c96b");
            // 題の帯・締めの帯が出ているあいだは、空の文字の帯を出さない。
            float bandAll = all * (1 - Math.Max(_s._titleShow, _s._bandShow)) * (_s._phase == Phase.Title ? 0 : 1);
            DrawRect(band, new Color(back.R, back.G, back.B, back.A * bandAll));
            DrawRect(new Rect2(band.Position, new Vector2(size.X, 3 * s)), new Color(edge.R, edge.G, edge.B, 0.9f * bandAll));
            // 送りの印（出切ったら点滅）。
            if (_s._phase == Phase.Line && _s._shown >= _s._body.Text.Length && Mathf.PosMod(_s._clock, 1.0f) < 0.6f)
            {
                Vector2 c = new(_s.ColumnX + _s.ColumnWidth + 30 * s, band.End.Y - 46 * s);
                DrawColoredPolygon(new[] { c + new Vector2(-10, -7) * s, c + new Vector2(10, -7) * s, c + new Vector2(0, 9) * s }, new Color(edge.R, edge.G, edge.B, all));
            }
            // ---- 暗転 ----
            if (_s._black > 0) DrawRect(new Rect2(Vector2.Zero, size), new Color(0, 0, 0, _s._black));
            // ---- 題の帯（場面の頭）と締めの帯 ----
            Strip(_s._title, _s._titleShow, size.Y * 0.40f, 60, Color.FromHtml("#ffe6a8"));
            Strip(BandText, _s._bandShow, size.Y * 0.44f, 72, Colors.White);

            void Strip(string text, float show, float y, int fontSize, Color ink)
            {
                if (show <= 0.01f || text.Length == 0) return;
                float h = 130 * s;
                int px = Mathf.RoundToInt(fontSize * s);
                DrawRect(new Rect2(0, y - h / 2, size.X, h), new Color(0.02f, 0.025f, 0.04f, 0.9f * show));
                DrawRect(new Rect2(0, y - h / 2, size.X, 3 * s), new Color(ink.R, ink.G, ink.B, 0.85f * show));
                DrawRect(new Rect2(0, y + h / 2 - 3 * s, size.X, 3 * s), new Color(ink.R, ink.G, ink.B, 0.85f * show));
                Vector2 ts = bold.GetStringSize(text, HorizontalAlignment.Left, -1, px);
                DrawString(bold, new Vector2((size.X - ts.X) / 2, y + px * 0.36f), text, HorizontalAlignment.Left, -1, px, new Color(ink.R, ink.G, ink.B, show));
            }
        }
    }
}
