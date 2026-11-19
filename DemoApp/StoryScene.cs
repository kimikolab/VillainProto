using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 会話の画面（第481期・指示書 design/PHASE481_STORY_PART_SPEC.md §1 ＋ 追記 2・3）。**判定は 1 つも持たない**——渡された行を順に見せるだけ。
//
//   背景     いま映っている盤を、暗く（明るさ 40%）・少しぼかす（専用の背景絵は作らない・視点を流すのは呼ぶ側）
//   立ち絵   左右に 1 人ずつ・腰から上が見える大きさ（足元は帯の後ろへ潜る）・顔が画面の上 1／3 に来る。
//            話している側を明るく・手前に、もう一方は暗く（入れ替わり 0.2 秒）
//   帯       画面の下 1／5。本文は 2 行まで・文字は 1920×1080 で 38px。名札は話している側（左 ／ 右）の、帯の上の端
//   主人公   声 ＝ 部下のセリフと同じ黒い帯・金の線（追記 4）・名札なし・左寄せ・斜体にしない・直前に話した駒の立ち絵は明るいまま
//            内心 ＝ 紺の帯・名札なし・左寄せ・斜体・立ち絵は両方もう一段暗く（明るさ 20%）
//   名札のみ 立ち絵を出さず、名札と本文だけ（伝令・守将）。相手の立ち絵は暗くして残す
//   送り     クリック ／ Space ／ Enter。文字は 1 字ずつ出て、出ている途中に押すと全部出る。出切ると ▼（締めの帯にも）
//   飛ばす   右上の「スキップ」（場面の終わりへ）
//   演出     場面の頭に題の帯（1.2 秒）・`（暗転）`・締めの帯 `（帯）`
// =====================================================================================
public partial class StoryScene : Control
{
    public const float TitleSeconds = 1.2f, SwapSeconds = 0.2f, CharsPerSecond = 30f, FadeSeconds = 0.5f, DimLevel = 0.4f;
    /// <summary>文字の大きさ（1920×1080 のとき）と、帯の高さ（画面の高さに対して）。</summary>
    public const int BodyFont = 38, NameFont = 28;
    public const float BandShare = 0.2f;
    /// <summary>立ち絵の高さ（1080 のとき・絵の全体）と、顔の真ん中を置く高さ（画面の高さに対して）。</summary>
    public const float PortraitHeight = 1100f, FaceY = 0.215f;
    /// <summary>
    /// 駒ごとの置き方（追記 4）: 顔が絵の中で下にある駒は、絵を少し大きくして、上の端が切れてもよいから顔を上 1／3 の中へ置く。
    /// 駒の Id → （絵の倍率, 顔の真ん中を置く高さ（画面の高さに対して））。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (float Grow, float FaceAt)> PortraitFit = new Dictionary<string, (float, float)> { ["mudo"] = (1.06f, 0.30f) };
    /// <summary>立ち絵の明るさ: 話していない側 ／ 内心のあいだ。</summary>
    public const float DimPortrait = 0.34f, InnerPortrait = 0.20f;
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
    private bool _hero, _nameRight;

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
    /// <summary>帯（画面の下 1／5）。</summary>
    public Rect2 BandRect => new(0, Size.Y * (1 - BandShare), Size.X, Size.Y * BandShare);
    private float ColumnWidth => Math.Min(Size.X - 480 * S, 1560 * S);
    private float ColumnX => (Size.X - ColumnWidth) / 2;
    public Rect2 NameRect => _name.Visible ? new Rect2(_name.Position, _name.Size) : default;
    public Rect2 BodyRect => new(_body.Position, _body.Size);
    public Rect2 SkipRect => _skip.Visible ? new Rect2(_skip.Position, _skip.Size) : default;
    /// <summary>本文がいま何行か・本文の下の端。</summary>
    public int BodyLines => _body.GetLineCount();
    public float BodyBottom => _body.Position.Y + _body.GetLineCount() * _body.GetLineHeight() + Math.Max(0, _body.GetLineCount() - 1) * _body.GetThemeConstant("line_spacing");
    /// <summary>立ち絵の全体・顔の四角（出ていなければ空）・明るさ（0〜1）。</summary>
    public Rect2 PortraitRect(bool right) => (right ? _right : _left).Shown;
    public Rect2 FaceRect(bool right) => (right ? _right : _left).Face;
    public float PortraitBright(bool right) => (right ? _right : _left).Bright;
    public bool PortraitLit(bool right) => (right ? _right : _left).Lit;
    public bool PortraitShown(bool right) => (right ? _right : _left).Has;
    public string PortraitWho(bool right) => (right ? _right : _left).Who;
    public string NameText => _name.Visible ? _name.Text : "";
    /// <summary>名札が右の側に寄っているか。</summary>
    public bool NameOnRight => _nameRight;
    public string BodyText => _body.Text;
    /// <summary>主人公の行（声 ／ 内心）が出ているか・斜体か・本文は左寄せか。</summary>
    public bool HeroShown => _hero;
    public bool Italic { get; private set; }
    /// <summary>帯が紺か（内心だけ・追記 4——主人公の声は、部下のセリフと同じ黒い帯・金の線）。</summary>
    public bool NavyBand => Italic;
    public bool BodyLeft => _body.HorizontalAlignment == HorizontalAlignment.Left;
    public string TitleBandText => _titleShow > 0.01f ? _title : "";
    public string EndBandText => _bandShow > 0.01f ? _front.BandText : "";
    /// <summary>送りの印 ▼ が出る所にいるか（本文が出切った ／ 締めの帯）。</summary>
    public bool AdvanceMark => (_phase == Phase.Line && _shown >= _body.Text.Length) || _phase == Phase.Band;
    public bool Black => _black > 0.99f;
    public bool SkipShown => _skip.Visible;

    private void Layout()
    {
        if (!IsInsideTree()) return;
        float s = S;
        Rect2 band = BandRect;
        _body.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(BodyFont * s));
        _body.AddThemeConstantOverride("line_spacing", Mathf.RoundToInt(10 * s));
        _body.AddThemeConstantOverride("outline_size", Mathf.RoundToInt(4 * s));
        _body.Position = new Vector2(ColumnX, band.Position.Y + 42 * s);
        _body.Size = new Vector2(ColumnWidth, band.Size.Y - 56 * s);
        _name.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(NameFont * s));
        _name.AddThemeStyleboxOverride("normal", Map3DHud.Box(new Color(0.10f, 0.08f, 0.03f, 0.97f), Color.FromHtml("#f1c96b"), 2, 8, Mathf.RoundToInt(14 * s)));
        PlaceName();
        _skip.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(22 * s));
        _skip.Size = new Vector2(150 * s, 50 * s);
        _skip.Position = new Vector2(Size.X - 180 * s, 28 * s);
        // 立ち絵: 顔の真ん中を、画面の真ん中から左右に同じだけ離した所・画面の上 1／3 の中に置く。足元は帯の後ろへ潜る。
        float off = Math.Min(Size.X * 0.27f, 560 * s);
        _left.Anchor = new Vector2(Size.X / 2 - off, Size.Y * FaceY);
        _right.Anchor = new Vector2(Size.X / 2 + off, Size.Y * FaceY);
        _left.Height = _right.Height = PortraitHeight * s;
        _left.Top = _right.Top = 12 * s;
        _front.QueueRedraw();
    }

    /// <summary>名札は話している側（左 ／ 右）の、帯の上の端（半分だけ帯の上へ出る）。</summary>
    private void PlaceName()
    {
        float s = S;
        Rect2 band = BandRect;
        _name.ResetSize();
        float x = _nameRight ? ColumnX + ColumnWidth - _name.Size.X : ColumnX;
        _name.Position = new Vector2(x, band.Position.Y - 30 * s);
    }

    // ---------------- 流れ ----------------

    /// <summary>場面を始める。<paramref name="lines"/> は、その通しで出す行（条件はもう選んである）。</summary>
    public void Play(StoryScript.Scene scene, IReadOnlyList<StoryScript.Line> lines, bool allowSkip = true)
    {
        SceneNo = scene.No;
        _lines = lines;
        _title = scene.Title;
        _at = -1;
        _clock = Elapsed = _black = _bandShow = 0;
        _blackoutDone = false;
        _all = 0;
        _left.Clear(); _right.Clear();
        _skip.Visible = allowSkip;
        ClearText();
        Visible = true;
        _phase = Phase.Title;
        Layout();
        GD.Print($"STORY_SCENE_BEGIN 場面 {scene.No}「{scene.Title}」行 {_lines.Count}");
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

    private void ClearText() { _body.Text = ""; _name.Visible = false; _hero = false; Italic = false; }

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
                ClearText();
                _left.Level = _right.Level = DimPortrait;
                _phase = Phase.Band; _clock = 0; _autoWait = 2.5f;
                return;
        }
        _phase = Phase.Line;
        _hero = line.Hero;
        Italic = line.Side == StoryScript.Side.Inner;
        if (line.Side == StoryScript.Side.Inner) _left.Level = _right.Level = InnerPortrait;   // 内心: 両方もう一段暗く
        else if (line.Side == StoryScript.Side.Voice)
        {
            // 声: 直前に話した駒の立ち絵は明るいまま（会話の相手）。内心の後なら、暗くした立ち絵をふつうの暗さへ戻す。
            if (_left.Level < DimPortrait) _left.Level = DimPortrait;
            if (_right.Level < DimPortrait) _right.Level = DimPortrait;
        }
        else
        {
            Slot on = line.Side == StoryScript.Side.Right ? _right : _left, other = line.Side == StoryScript.Side.Right ? _left : _right;
            // 名札のみ: その側の立ち絵は出さない（絵の置き場を持つ人物は、絵があれば出す）。相手は暗くして残す。
            if (other.Who == line.Speaker) other.Clear();   // 追記 4: 候補で替わった話者が向かいにも立っていたら、向かいを下げる（同じ顔を 2 つ出さない）
            on.Set(line.Speaker, line.Shadow, line.Variant);
            on.Level = 1;
            other.Level = DimPortrait;
            MoveChild(on, _front.GetIndex() - 1);   // 話している側を手前に
        }
        _name.Text = line.Speaker;
        _name.Visible = !_hero;
        _nameRight = line.Side == StoryScript.Side.Right;
        PlaceName();
        _body.AddThemeFontOverride("font", Italic ? _italic! : Map3DFonts.Regular);
        _body.HorizontalAlignment = HorizontalAlignment.Left;   // 主人公の行も左寄せ（行頭をそろえる）
        _body.AddThemeColorOverride("font_color", Italic ? Color.FromHtml("#d6e0ff") : Color.FromHtml("#f2f0e6"));
        _body.Text = line.Text;
        _body.VisibleCharacters = 0;
        _shown = 0;
        _autoWait = AutoWaitOf();
        _front.QueueRedraw();
        string who = line.Side == StoryScript.Side.Inner ? "内心" : line.Side == StoryScript.Side.Voice ? "声" : line.Speaker + (line.Shadow ? "（名札のみ）" : "");
        GD.Print($"STORY_LINE 場面 {SceneNo} {_at + 1}／{_lines.Count} {who}｜{line.Text}");
    }

    /// <summary>検査・写しの口: その行を出し切った姿にする（入れ替わりの動きも終わらせる）。行は順に呼ぶこと（立ち絵は前の行から続く）。</summary>
    public void ShowForCheck(int index)
    {
        if (index < 0 || index >= _lines.Count) return;
        _clock = TitleSeconds; _titleShow = 0; _all = 1; _black = 0; _bandShow = 0;
        _at = index - 1;
        Next();
        if (_phase == Phase.Line) { _shown = _body.Text.Length; _body.VisibleCharacters = -1; }
        if (_phase == Phase.FadeOut) { _phase = Phase.Hold; _black = 1; _clock = 0; _left.Clear(); _right.Clear(); ClearText(); }   // 暗転で立ち絵は入れ替わる
        if (_phase == Phase.Band) _bandShow = 1;
        _left.Settle(); _right.Settle();
        Apply();
    }

    /// <summary>検査・写しの口: 題の帯が出ている姿にする。</summary>
    public void ShowTitleForCheck() { _phase = Phase.Title; _clock = TitleSeconds * 0.5f; _titleShow = 1; _all = 1; Apply(); }

    /// <summary>いま出している行の並び。</summary>
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
                    ClearText();
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
    public static float ReadSeconds(IEnumerable<StoryScript.Line> lines)
        => TitleSeconds + lines.Sum(l => l.Kind == StoryScript.Kind.Say ? l.Text.Length / ReadCharsPerSecond + ReadPause
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

    private static readonly Dictionary<string, Texture2D?> ArtCache = new(StringComparer.Ordinal);
    /// <summary>駒でない人物の絵（`design/art/story/` の下・あれば読む・無ければ null ＝ 立ち絵なし）。</summary>
    private static Texture2D? StoryArt(string file)
    {
        if (ArtCache.TryGetValue(file, out Texture2D? cached)) return cached;
        Texture2D? tex = null;
        try
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "design", "art", "story", file));
            if (System.IO.File.Exists(path))
            {
                using Image image = Image.LoadFromFile(path);
                if (!image.IsEmpty()) { image.GenerateMipmaps(); tex = ImageTexture.CreateFromImage(image); }
            }
            GD.Print($"STORY_ART {file} {(tex is null ? "無い——立ち絵なし" : path)}");
        }
        catch (Exception e) { GD.PrintErr($"STORY_ART_FAILED {file} {e.Message}"); }
        return ArtCache[file] = tex;
    }

    /// <summary>立ち絵の差分（第482期・`assets/portraits/battle/<id>_<差分>_right.png`・あれば読む・無ければ null ＝ 待機の絵）。取り込み（.import）を待たずにファイルを直に読む。</summary>
    private static Texture2D? VariantArt(string unitId, string variant)
    {
        string key = "差分:" + unitId + ":" + variant;
        if (ArtCache.TryGetValue(key, out Texture2D? cached)) return cached;
        Texture2D? tex = null;
        try
        {
            string path = ProjectSettings.GlobalizePath("res://" + StoryScript.VariantFile(unitId, variant));
            if (System.IO.File.Exists(path))
            {
                using Image image = Image.LoadFromFile(path);
                if (!image.IsEmpty()) { image.GenerateMipmaps(); tex = ImageTexture.CreateFromImage(image); }
            }
            GD.Print($"STORY_ART 差分 {unitId}:{variant} {(tex is null ? "無い——待機の絵" : path)}");
        }
        catch (Exception e) { GD.PrintErr($"STORY_ART_FAILED {unitId}:{variant} {e.Message}"); }
        return ArtCache[key] = tex;
    }

    /// <summary>駒でない人物の絵の中の顔（第482期 追記・腰から上の絵は顔が下にある）: 真ん中（幅・高さに対して）と、幅に対する顔の四角の大きさ。絵は左向き。</summary>
    private static readonly Dictionary<string, Vector3> PersonFrame = new(StringComparer.Ordinal)
    {
        ["聖女"] = new(0.42f, 0.29f, 0.30f), ["伝令"] = new(0.47f, 0.32f, 0.30f),
    };

    /// <summary>覚えた絵を捨てる（検査が絵を置いたり外したりした後に呼ぶ）。</summary>
    internal static void ForgetArt() => ArtCache.Clear();
    public string PortraitSource(bool right) => (right ? _right : _left).Source;

    /// <summary>立ち絵 1 人ぶん（左 ／ 右）。絵が無い人物は何も出さない。</summary>
    private sealed partial class Slot : Control
    {
        private readonly bool _rightSide;
        private readonly TextureRect _art = new() { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        private float _level, _in;
        /// <summary>絵の中の顔: 真ん中（幅・高さに対して）と、幅に対する顔の四角の大きさ。</summary>
        private Vector3 _frame = new(0.5f, 0.14f, 0.36f);
        private bool _flip;
        /// <summary>その駒だけの置き方（<see cref="PortraitFit"/>）: 絵の倍率と、顔の真ん中を置く高さ（0 ＝ ふつうの置き方）。</summary>
        private float _grow = 1f, _faceAt;
        /// <summary>顔の真ん中を置く所（画面の座標）・絵の全体の高さ・絵の上の端の下限。</summary>
        public Vector2 Anchor;
        public float Height = 1100, Top = 12;
        /// <summary>明るさの行き先（1 ＝ 話している・0.34 ＝ 話していない・0.20 ＝ 内心のあいだ）。</summary>
        public float Level;
        public float Fade = 1;
        public string Who { get; private set; } = "";
        public bool Has => _art.Texture is not null;
        public bool Lit => Has && Level > 0.99f;
        public float Bright => Has ? _level : 0;
        /// <summary>いま絵を描いている四角・顔の四角（画面の座標・出ていなければ空）。</summary>
        public Rect2 Shown { get; private set; }
        public Rect2 Face { get; private set; }

        public Slot(bool right)
        {
            _rightSide = right;
            MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_art);
            _art.Material = Cutout();
        }

        public void Clear() { Who = ""; _variant = ""; Source = ""; _art.Texture = null; Level = _level = 0; Shown = Face = default; }
        private string _variant = "";
        /// <summary>今の絵の出どころ（検査用）: 「差分 …」「待機」「人物 …」「勇者」・絵が無ければ空。</summary>
        public string Source { get; private set; } = "";

        /// <summary>その側の人物を替える。<paramref name="nameOnly"/> ＝ 立ち絵を出さない（名札のみ）。</summary>
        public void Set(string speaker, bool nameOnly, string variant = "")
        {
            if (Who == speaker && _variant == variant) return;
            if (Who != speaker) _in = 0;   // 同じ人の差分の入れ替えは、出直さずにその場で替える
            Who = speaker;
            _variant = variant;
            Source = "";
            Texture2D? tex = null;
            bool facesRight = true;
            _frame = new Vector3(0.5f, 0.14f, 0.36f);
            (_grow, _faceAt) = (1f, 0f);
            // 第482期 §6: 駒でない人物は `design/art/story/` の絵を話者の名前で引く（表のファイル名 → `<名前>.png`）。絵があれば「名札のみ」でも出す。
            bool person = speaker != StoryScript.HeroSpeaker && StoryScript.UnitOf(speaker) is null;
            var found = person ? StoryScript.ArtFilesOf(speaker).Select(f => (File: f, Tex: StoryArt(f))).FirstOrDefault(x => x.Tex is not null) : default;
            if (found.Tex is not null)
            {
                tex = found.Tex; facesRight = false; Source = "人物 " + found.File;
                if (PersonFrame.TryGetValue(speaker, out Vector3 at)) _frame = at;
            }
            else if (nameOnly || person) tex = null;
            else if (speaker == StoryScript.HeroSpeaker) { tex = HunterArt.Standing(); facesRight = false; _frame = new Vector3(0.43f, 0.09f, 0.24f); Source = tex is null ? "" : "勇者"; }
            else if (StoryScript.UnitOf(speaker) is { } def)
            {
                // 第482期 §6: 差分（`ガン:静`）の絵があればそれを、無ければ待機の絵。
                tex = variant.Length > 0 ? VariantArt(def.Id, variant) : null;
                Source = tex is not null ? "差分 " + StoryScript.VariantFile(def.Id, variant) : "待機";
                tex ??= UiKit.HasCustomBattlePortrait(def.Id) ? UiKit.BattlePortrait(null!, def.Id) : UiKit.HasCustomPortrait(def.Id) ? UiKit.Portrait(null!, def.Id) : null;
                if (tex is null) Source = "";
                _frame = UnitFaceIcon.FrameOf(def.Id);
                if (PortraitFit.TryGetValue(def.Id, out var fit)) (_grow, _faceAt) = fit;
            }
            _art.Texture = tex;
            _art.Visible = tex is not null;
            // 絵は画面の内側を向く（左の人は右向き・右の人は左向き）。
            _flip = tex is not null && facesRight == _rightSide;
            _art.FlipH = _flip;
        }

        public void Settle() { _level = Level; _in = 1; }

        public void Tick(float dt)
        {
            _level = Mathf.MoveToward(_level, Level, dt / SwapSeconds);
            _in = Mathf.MoveToward(_in, 1, dt / SwapSeconds);
        }

        public void Apply()
        {
            if (_art.Texture is not { } t) { Visible = false; Shown = Face = default; return; }
            Visible = true;
            // 話している側は少し大きく（手前）・明るく。大きさは顔の真ん中を軸に変える。
            float lit = Mathf.InverseLerp(DimPortrait, 1f, Mathf.Clamp(_level, DimPortrait, 1f));
            float scale = Mathf.Lerp(0.93f, 1.0f, lit);
            float h = Height * scale * _grow, w = h * t.GetWidth() / Math.Max(1, t.GetHeight());
            float fx = _flip ? 1 - _frame.X : _frame.X;
            // 顔が大きく下にある絵（頭の大きい駒）は、絵の上の端が画面から出ない所まで下げる。
            // 置き方を決めてある駒（ムド）は、上の端が切れてもよい——顔を決めた高さへ置く。
            float y = _faceAt > 0 ? Anchor.Y / FaceY * _faceAt - _frame.Y * h : Math.Max(Top, Anchor.Y - _frame.Y * h);
            Vector2 at = new(Anchor.X - fx * w, y);
            Shown = new Rect2(at, new Vector2(w, h));
            float side = _frame.Z * w;
            Face = new Rect2(at.X + fx * w - side / 2, at.Y + _frame.Y * h - side / 2, side, side);
            _art.Position = at;
            _art.Size = new Vector2(w, h);
            ((ShaderMaterial)_art.Material).SetShaderParameter("tint", new Color(_level, _level, _level, _in * Fade));
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
            // ---- 文字の帯（下 1／5）: 黒い帯・金の線（部下のセリフと主人公の声）。紺の帯は内心だけ（追記 4） ----
            Rect2 band = _s.BandRect;
            Color back = _s.NavyBand ? new Color(0.06f, 0.09f, 0.27f, 0.96f) : new Color(0.03f, 0.035f, 0.05f, 0.95f);
            Color edge = _s.NavyBand ? Color.FromHtml("#7f9bff") : Color.FromHtml("#f1c96b");
            // 題の帯・締めの帯が出ているあいだは、空の文字の帯を出さない。
            float bandAll = all * (1 - Math.Max(_s._titleShow, _s._bandShow)) * (_s._phase == Phase.Title ? 0 : 1);
            DrawRect(band, new Color(back.R, back.G, back.B, back.A * bandAll));
            DrawRect(new Rect2(band.Position, new Vector2(size.X, 3 * s)), new Color(edge.R, edge.G, edge.B, 0.9f * bandAll));
            bool blink = Mathf.PosMod(_s._clock, 1.0f) < 0.6f;
            // 送りの印（出切ったら点滅）。
            if (_s._phase == Phase.Line && _s._shown >= _s._body.Text.Length && blink)
                Mark(new Vector2(_s.ColumnX + _s.ColumnWidth + 30 * s, band.End.Y - 40 * s), new Color(edge.R, edge.G, edge.B, all));
            // ---- 暗転 ----
            if (_s._black > 0) DrawRect(new Rect2(Vector2.Zero, size), new Color(0, 0, 0, _s._black));
            // ---- 題の帯（場面の頭）と締めの帯 ----
            Strip(_s._title, _s._titleShow, size.Y * 0.40f, 60, Color.FromHtml("#ffe6a8"));
            Strip(BandText, _s._bandShow, size.Y * 0.44f, 72, Colors.White);
            // 締めの帯にも送りの印（押すと次へ進むことが分かるように）。
            if (_s._phase == Phase.Band && _s._bandShow > 0.9f && blink) Mark(new Vector2(size.X / 2, size.Y * 0.44f + 100 * s), new Color(1, 1, 1, all));

            void Mark(Vector2 c, Color ink)
                => DrawColoredPolygon(new[] { c + new Vector2(-10, -7) * s, c + new Vector2(10, -7) * s, c + new Vector2(0, 9) * s }, ink);

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
