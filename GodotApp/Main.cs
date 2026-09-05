using Godot;
using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 戦闘画面のプロトタイプ。
///
/// <para><b>この画面は戦闘の判定を一切していない。</b>
/// <see cref="EngagementEngine.Run"/> は seed 決定的な純関数で会戦（5部隊連戦・持ち越しあり）を
/// 丸ごと計算し切るので、ここでやるのは戻ってきた各 Battle の
/// <see cref="BattleResult.Events"/> を時間に展開して見せることだけ。
/// 盤面の初期値も <see cref="BattleOpening"/>（持ち越した HP・攻撃力を含む開始盤面）から
/// 組むだけで、リアルタイムのシミュレーションループを持たない。</para>
///
/// <para>BattleSim の <c>replay</c>（JSON）を経由しないのは、Godot からは BattleCore を
/// 直接参照できるため。JSON は HTML ビューア用の経路として残してある。</para>
/// </summary>
public partial class Main : Control
{
    // ---- 見た目の定数 -------------------------------------------------

    static readonly Color CGround = Color.FromHtml("#0f1315");
    static readonly Color CPanel = Color.FromHtml("#161c1f");
    static readonly Color CPanel2 = Color.FromHtml("#1d2529");
    static readonly Color CLine = Color.FromHtml("#2a3438");
    static readonly Color CInk = Color.FromHtml("#d8dedb");
    static readonly Color CDim = Color.FromHtml("#889693");
    static readonly Color CFaint = Color.FromHtml("#5d6a6d");
    static readonly Color CDmg = Color.FromHtml("#c9694a");
    static readonly Color CFf = Color.FromHtml("#8b7cb8");
    static readonly Color CHeal = Color.FromHtml("#4f9d8b");
    static readonly Color CAccent = Color.FromHtml("#e0a94a");
    static readonly Color CEnemy = Color.FromHtml("#c07a68");
    static readonly Color CPlayer = Color.FromHtml("#6fa8b2");

    // 通貨の色（第97期 D2 / D4）。**盤面の数字と、カード上の札で同じ色を使う**
    // ——数字が浮いた瞬間に「どの通貨が働いたか」が色だけで読めるようにするため。
    static readonly Color CPoison = Color.FromHtml("#7fbf5f");   // 毒
    static readonly Color CBurn = Color.FromHtml("#e08a3c");     // 燃
    static readonly Color CWound = Color.FromHtml("#c2506b");    // 傷・深手
    static readonly Color CStun = Color.FromHtml("#b9a0d8");     // 痺
    static readonly Color CMark = Color.FromHtml("#d6c04a");     // 標
    static readonly Color CArmor = Color.FromHtml("#8fa3b8");    // 破片
    static readonly Color CDull = Color.FromHtml("#9a7fb0");     // なまり
    static readonly Color CCounter = Color.FromHtml("#e6c07a");  // 反撃（線の色）

    // ---- 盤面の幾何 ---------------------------------------------------
    //
    // 編成スロット 0-4 が X字（前1・前3 / 中央 / 後1・後3）、5-8 が召喚専用。
    // レーンは2本で、どちらも 前X → 中央 →〔○中X〕→ 後X の奥行き（BattleCore の FormationRules と同じ）。
    //
    // 表示は「前列どうしが向かい合う」向きに揃える。敵は奥→手前、味方は手前→奥。
    // 内側の列（○中1・中央・○中3）が召喚枠を含む中間層で、ここに駒が湧くと貫きがもう1段減衰する。
    // **貫きがレーンを前から走る**という規則が目で分かることがこの画面の要点。
    //
    // X字化で 3×3 の完全な格子になったので、旧盤面で必要だった -1（空き枠）の詰め物は消えた。
    // それでも均等グリッドとして描いてはいけない——列の意味（前 / 中間 / 後）が読めなくなる。
    //
    //     列は 後 / 中間 / 前 の順、各列は上から 行1・行2・行3。
    //     後1(3) ○後2(8) 後3(4) ／ ○中1(5) 中央(2) ○中3(6) ／ 前1(0) ○前2(7) 前3(1)
    static readonly int[][] EnemyLaneOrder = { new[] { 3, 8, 4 }, new[] { 5, 2, 6 }, new[] { 0, 7, 1 } };
    static readonly int[][] PlayerLaneOrder = { new[] { 0, 7, 1 }, new[] { 5, 2, 6 }, new[] { 3, 8, 4 } };

    // ---- 再生の間（イベント種ごと）------------------------------------
    // テンポそのものを見るための画面なので、ここが実質の演出設計。
    static readonly Dictionary<BattleEventKind, double> Dur = new()
    {
        [BattleEventKind.TurnStart] = 0.62, [BattleEventKind.Attack] = 0.40,
        [BattleEventKind.Damage] = 0.26, [BattleEventKind.Death] = 0.56,
        [BattleEventKind.Highlight] = 0.76, [BattleEventKind.Heal] = 0.24,
        [BattleEventKind.Status] = 0.26, [BattleEventKind.Move] = 0.34,
        [BattleEventKind.Summon] = 0.46, [BattleEventKind.Revive] = 0.52,
        // 溜めは「何も起きないターン」なので、間を長めに取らないと予告として読めない。
        [BattleEventKind.Charge] = 0.70,
        // 術は直後に効果のイベントが続くので、溜めほど間を取らない。
        [BattleEventKind.Skill] = 0.34,
        // 状態異常が付いた瞬間（第97期 D3）。**札が読めるだけの間**で足りる
        // ——1つの動作で何枚も付くので、ここを長く取ると盤面が止まって見える。
        [BattleEventKind.StatusGain] = 0.20,
    };

    // ---- 見るための編成 -------------------------------------------------
    //
    // **手写しをやめた（第97期 D6）。** BattleCore の <see cref="Presets"/> から
    // `compare` 61 行と交差帯 12 行をそのまま引く（合計 73 行）。
    // 写しを持つと必ずずれる——第94期に手写しの表で 29 件の誤りが出ている。
    // **勝率の検証は今まで通り BattleSim が正で、ここは同じ定義を見るだけ。**
    static (string Name, Formation F)[]? _builds;

    static (string Name, Formation F)[] Builds() =>
        _builds ??= Presets.Compare.Select(b => (Name: b.Name, F: b.F))
            .Concat(Presets.Cross.Select(b => (Name: "交差 " + b.Name, F: b.F)))
            .ToArray();

    // ---- 駒の状態（台本を適用して組み立てる）----------------------------

    sealed class Piece
    {
        public int Id, Team, Slot, Hp, MaxHp, Attack;
        /// <summary>カタログ上の素の攻撃力。<see cref="Attack"/>（現在値）と並べて出す。</summary>
        public int BaseAttack;
        public string Name = "";
        public AttackPattern Pattern;
        public bool Alive = true;
    }

    sealed class Card
    {
        public PanelContainer Root = null!;
        public StyleBoxFlat Style = null!;
        public Label Name = null!, Pat = null!, Hp = null!, Status = null!;
        public ProgressBar Bar = null!;
        public StyleBoxFlat Fill = null!;
    }

    /// <summary>
    /// 「誰が誰を叩いたか」の筋。ダメージが通るたびに1本積んで、時間で薄れる。
    ///
    /// <c>Attack</c> ではなく <c>Damage</c> を起点にしているのは、そちらのほうが読めるものが多いため。
    /// 薙ぎなら巻き込んだ数だけ、貫きならレーンを走った数だけ本数が出るので、
    /// **攻撃パターンの形がそのまま線の形になる。** 棘の反撃のように
    /// <c>PerformAttack</c> を通らない干渉も同じように出る。
    ///
    /// <para>第97期 D1: <c>Damage</c> にも <c>Pattern</c> が載るようになったので、
    /// 単体・薙ぎ・貫き・全体を線の形で描き分ける。<see cref="Prev"/> は
    /// **同じ一振りの直前の着弾**で、薙ぎならそこを繋いで扇に、貫きならレーンを走る筋になる。</para>
    /// </summary>
    struct Shot
    {
        public int FromTeam, FromSlot, ToTeam, ToSlot;
        public bool Friendly;

        /// <summary>ターン外の反応（棘・仇討ち・軋み・追い打ち）。線の色を変える。</summary>
        public bool Reaction;

        /// <summary>肩代わりが分割して中継した段（第85期）。破線で描く。</summary>
        public bool Relayed;

        /// <summary>刃ではなく「状態異常を書いた」筋（第97期 D3）。細い線で <see cref="Tint"/> の色。</summary>
        public bool Thin;
        public Color Tint;

        /// <summary>攻撃型。null は「型なし」（反撃・毒燃の刻み・中継・共有）。</summary>
        public AttackPattern? Pattern;

        /// <summary>同じ一振りの直前の着弾（扇・貫きの連結）。無ければ <c>HasPrev</c> が偽。</summary>
        public bool HasPrev;
        public int PrevTeam, PrevSlot;

        public double Life;
    }

    /// <summary>
    /// カードの上に浮いて消える札（第97期 D2 / D3 / D5）。
    ///
    /// <para>毒・燃焼の刻みは<b>誰からでもない</b>ので線を引かない——数字だけをカードの上に出す。
    /// 状態異常が付いた瞬間（<c>StatusGain</c>）は「+傷」のような短い札にする。</para>
    /// </summary>
    struct Pop
    {
        public int Team, Slot;
        public string Text;
        public Color Color;
        public int Stack;      // 同じカードに同時に複数出たときの段（重なって読めなくなるのを防ぐ）
        public double Life;
    }

    /// <summary>全体攻撃（<see cref="AttackPattern.All"/>）。線を引かず、その陣営の盤面を一瞬明るくする。</summary>
    struct Flash
    {
        public int Team;
        public Color Color;
        public double Life;
    }

    const double ShotLife = 0.55;
    const double PopLife = 0.4;
    const double FlashLife = 0.35;

    // ---- 状態 -----------------------------------------------------------

    int _buildIdx;
    string _playerName = "";
    Script _script = null!;
    int _battleIdx;
    BattleResult _result = null!;   // いま再生している Battle（_script.Battles[_battleIdx]）

    /// <summary>単発モードか（第98期 V4）。既定は会戦（今までどおり）。</summary>
    bool _single;
    /// <summary>単発モードで見る波（<see cref="EnemyCatalog.Stages"/> の添字）。</summary>
    int _stage;
    /// <summary>戦闘 seed。`compare` は 0..199 を回すので、その中の1本を選ぶ。</summary>
    int _seed;

    /// <summary>
    /// いま画面に出ている筋の「組」（第98期 V2）。1回の攻撃を1組として、
    /// <b>組が変わったら前の組の線を消す</b>——溜めると何が起きたか読めなくなる。
    /// </summary>
    int _shotGroup = int.MinValue;

    /// <summary>状態異常の細い線を出すか（第98期 V2）。**既定は切**。</summary>
    bool _showGainLines;
    List<Piece> _roster = new();
    readonly Dictionary<(int Team, int Slot), Card> _cards = new();
    readonly List<Shot> _shots = new();
    readonly List<Pop> _pops = new();
    readonly List<Flash> _flashes = new();
    ShotOverlay _overlay = null!;

    int _idx;
    bool _playing;
    double _wait;
    double _speed = 1.0;

    Label _lVerdict = null!, _lTurns = null!, _lChain = null!, _lPos = null!;
    Label _lEnemy = null!, _lPlayer = null!, _lChainBig = null!, _lChainNote = null!;
    Label _lProgress = null!, _lEngVerdict = null!, _lBanner = null!;
    OptionButton _pick = null!;
    OptionButton _pickStage = null!, _pickSeed = null!;
    Button _bPrevBattle = null!, _bNextBattle = null!, _bMode = null!, _bGainLines = null!;

    /// <summary>バナー表示の残り時間。>0 の間は再生を止めて、次の Battle への切り替えを待たせる。</summary>
    double _banner;
    const double BannerTime = 1.4;
    Button _bPlay = null!;
    HSlider _scrub = null!;
    RichTextLabel _feed = null!;
    readonly List<ColorRect> _pips = new();
    HBoxContainer _pipBox = null!;
    bool _syncing;   // スライダーへの書き戻しで ValueChanged が再入するのを止める

    // =====================================================================

    public override void _Ready()
    {
        // Godot の既定フォントは日本語グリフを持たない（そのままだと全部豆腐になる）。
        // ユニット名もログも日本語なので、OS のフォントを名前で拾って既定に据える。
        var theme = new Theme
        {
            DefaultFont = new SystemFont
            {
                FontNames = new[] { "Yu Gothic UI", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Segoe UI" },
                AllowSystemFallback = true,
            },
            DefaultFontSize = 14,
        };
        Theme = theme;

        var bg = new ColorRect { Color = CGround };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (string s in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(s, 16);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        root.AddChild(BuildHeader());

        var split = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 12);
        root.AddChild(split);
        split.AddChild(BuildBoard());
        split.AddChild(BuildFeed());

        root.AddChild(BuildTransport());

        // 攻撃の筋を描く層。カードより後に足すことでカードの上に乗る。
        _overlay = new ShotOverlay { Painter = PaintShots };
        _overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_overlay);

        // 部隊戦の切れ目に出すバナー（ENEMY REINFORCEMENTS / 2ND SQUAD）。最前面。
        _lBanner = Text("", 30, CAccent);
        _lBanner.SetAnchorsPreset(LayoutPreset.FullRect);
        _lBanner.HorizontalAlignment = HorizontalAlignment.Center;
        _lBanner.VerticalAlignment = VerticalAlignment.Center;
        _lBanner.Visible = false;
        AddChild(_lBanner);

        SyncModeUi();
        Load(0);
    }

    // ---- 攻撃の筋・浮く札 -----------------------------------------------

    /// <summary>そのカードの中心（overlay の座標系）。カードが無ければ偽を返す。</summary>
    bool CardCenter(Transform2D inv, int team, int slot, out Vector2 p)
    {
        p = Vector2.Zero;
        if (!_cards.TryGetValue((team, slot), out Card? c)) return false;
        p = inv * c.Root.GetGlobalRect().GetCenter();
        return true;
    }

    /// <summary>破線。肩代わりの中継（<c>Relayed</c>）はこれで描く。</summary>
    static void DrawDashed(ShotOverlay layer, Vector2 from, Vector2 to, Color c, float w)
    {
        const float Dash = 7f, Gap = 5f;
        float len = from.DistanceTo(to);
        if (len < 0.5f) return;
        Vector2 dir = (to - from) / len;
        for (float t = 0; t < len; t += Dash + Gap)
            layer.DrawLine(from + dir * t, from + dir * Math.Min(len, t + Dash), c, w, antialiased: true);
    }

    /// <summary>着弾側の印。向きが読めないと線は情報にならない。</summary>
    static void DrawHead(ShotOverlay layer, Vector2 from, Vector2 to, Color c, float size)
    {
        Vector2 d = (to - from).Normalized();
        if (d == Vector2.Zero) { layer.DrawCircle(to, size, c); return; }
        Vector2 n = new(-d.Y, d.X);
        layer.DrawColoredPolygon(new[] { to, to - d * size * 2f + n * size, to - d * size * 2f - n * size }, c);
    }

    /// <summary>
    /// 筋・浮く札・全体攻撃の閃光を描く。
    ///
    /// <para><b>形と色が凡例（<see cref="BuildLegend"/>）と一致していること</b>が、
    /// この画面が「何が起きたか」を伝えられるかどうかの全部。</para>
    /// </summary>
    void PaintShots(ShotOverlay layer)
    {
        Transform2D inv = layer.GetGlobalTransform().AffineInverse();

        // (1) 全体攻撃。線を引かず、その陣営の盤面をまるごと明るくする。
        foreach (Flash f in _flashes)
        {
            Rect2? box = null;
            for (int slot = 0; slot < FormationRules.TotalSlots; slot++)
                if (_cards.TryGetValue((f.Team, slot), out Card? c))
                {
                    Rect2 g = c.Root.GetGlobalRect();
                    Rect2 r = new(inv * g.Position, g.Size);
                    box = box is { } bb ? bb.Merge(r) : r;
                }
            if (box is not { } area) continue;
            Color c2 = f.Color;
            c2.A = (float)(f.Life / FlashLife) * 0.30f;
            layer.DrawRect(area.Grow(6), c2);
        }

        // (2) 筋。
        foreach (Shot s in _shots)
        {
            if (!CardCenter(inv, s.FromTeam, s.FromSlot, out Vector2 from)) continue;
            if (!CardCenter(inv, s.ToTeam, s.ToSlot, out Vector2 to)) continue;

            float k = (float)(s.Life / ShotLife);
            Color c = s.Thin ? s.Tint
                    : s.Reaction ? CCounter
                    : s.Friendly ? CFf
                    : CDmg;
            c.A = k * (s.Thin ? 0.85f : 0.9f);

            // 太さで型を出す。貫きはレーンを押し通るので一番太い。
            float w = s.Thin ? 1.5f
                    : s.Pattern switch
                    {
                        AttackPattern.Pierce => 5f * k + 2f,
                        AttackPattern.Sweep => 3f * k + 1f,
                        _ => 2f * k + 1f,
                    };

            if (s.Relayed) DrawDashed(layer, from, to, c, w);
            else layer.DrawLine(from, to, c, w, antialiased: true);

            // 同じ一振りの直前の着弾へ繋ぐ。薙ぎは扇に、貫きはレーンを走る筋になる。
            if (s.HasPrev && CardCenter(inv, s.PrevTeam, s.PrevSlot, out Vector2 prev))
            {
                Color cc = c; cc.A = k * 0.7f;
                layer.DrawLine(prev, to, cc, Math.Max(1.5f, w * 0.8f), antialiased: true);
            }

            if (s.Thin) layer.DrawCircle(to, 3f * k + 1.5f, c);
            else DrawHead(layer, from, to, c, 4f * k + 2.5f);
        }

        // (3) 浮く札（毒・燃焼の刻み／状態異常が付いた瞬間／移動）。
        Font font = layer.GetThemeDefaultFont();
        foreach (Pop p in _pops)
        {
            if (!CardCenter(inv, p.Team, p.Slot, out Vector2 at)) continue;
            float k = (float)(p.Life / PopLife);
            Color c = p.Color;
            c.A = Math.Min(1f, k * 1.6f);
            // 上へ浮きながら消える。段（Stack）は同じカードに同時に出たぶんのずらし。
            Vector2 pos = at + new Vector2(-14, -18 - (1f - k) * 14f - p.Stack * 15f);
            layer.DrawString(font, pos + new Vector2(1, 1), p.Text,
                             HorizontalAlignment.Left, -1, 15, new Color(0, 0, 0, c.A * 0.7f));
            layer.DrawString(font, pos, p.Text, HorizontalAlignment.Left, -1, 15, c);
        }
    }

    /// <summary>通貨（<see cref="StatusKeys"/> の札）の色。凡例とカードの札で同じ色を使う。</summary>
    static Color CurrencyColor(string key) => key switch
    {
        StatusKeys.Poison => CPoison,
        StatusKeys.Burn => CBurn,
        StatusKeys.Wound or StatusKeys.Deep => CWound,
        StatusKeys.Stun => CStun,
        StatusKeys.Marked => CMark,
        StatusKeys.Armor => CArmor,
        BattleContext.DullKey => CDull,
        _ => CAccent,
    };

    /// <summary>
    /// 「その通貨が働いた」（<see cref="BattleEventKind.Status"/>）の色。
    /// <c>Text</c> は engine が付けた日本語の札なので、そこから引く。
    /// </summary>
    static Color StatusWorkColor(string? label) => label switch
    {
        "燃焼" => CBurn,
        "毒" => CPoison,
        _ => CDmg,
    };

    /// <summary>
    /// その <paramref name="idx"/> の出来事が属する「組」（第98期 V2）。
    ///
    /// <para><b>1回の攻撃が1組。</b> ダメージは直前の <c>Attack</c> の位置を組の番号にするので、
    /// 薙ぎの巻き込みも貫きの各段も**同じ番号**になり、扇・数珠つなぎは崩れない。
    /// <c>Attack</c> を持たない出来事（毒燃の刻み・状態異常・移動・反撃）は自分の位置が組になる
    /// ——**1件ずつ独立して出る。**</para>
    /// </summary>
    int GroupOf(int idx)
    {
        if (_result.Events[idx].Kind != BattleEventKind.Damage) return idx;
        int aid = _result.Events[idx].ActorId ?? -1;
        for (int i = idx - 1; i >= 0; i--)
        {
            BattleEvent pe = _result.Events[i];
            if (pe.Kind is BattleEventKind.TurnStart or BattleEventKind.Skill or BattleEventKind.Charge) break;
            if (pe.Kind == BattleEventKind.Attack) return pe.ActorId == aid ? i : idx;
        }
        return idx;
    }

    /// <summary>直前に適用したイベントを、筋・札・閃光のどれかに変える。</summary>
    void PushShot(int idx, Dictionary<int, Piece> units)
    {
        BattleEvent e = _result.Events[idx];

        // **溜めない**（第98期 V2）。組が変わったら前の組の線と閃光を消す。
        // 浮く札（数字）は消さない——重ならないよう段違いに出しているし、
        // 「いま何点入ったか」は次の攻撃と一緒に読めたほうがいい。
        int grp = GroupOf(idx);
        if (grp != _shotGroup)
        {
            _shots.Clear();
            _flashes.Clear();
            _shotGroup = grp;
        }

        switch (e.Kind)
        {
            case BattleEventKind.Damage:
                PushDamage(idx, e, units);
                // 直撃の数字。**色は赤のまま**にして、毒（緑）・燃焼（橙）と並べたときに
                // 「どの通貨が削ったか」が数字の色だけで読めるようにする（第97期 D2）。
                if (e.TargetId is { } dtid && units.TryGetValue(dtid, out Piece? dt))
                    AddPop(dt, $"-{e.Amount}", e.FriendlyFire ? CFf : e.Reaction ? CCounter : CDmg);
                break;

            // 毒・燃焼の刻み（第97期 D2）。**誰からでもないので線を引かない。**
            case BattleEventKind.Status:
                if (e.TargetId is { } wid && units.TryGetValue(wid, out Piece? w))
                    AddPop(w, $"-{e.Amount}", StatusWorkColor(e.Text));
                break;

            // 状態異常が付いた瞬間（第97期 D3）。札を出し、書き手が分かれば細い線を引く。
            case BattleEventKind.StatusGain:
                if (e.TargetId is { } gid && units.TryGetValue(gid, out Piece? g) && e.Text is { } key)
                {
                    Color gc = CurrencyColor(key);
                    AddPop(g, $"+{StatusKeys.LabelOf(key)}{(e.Amount > 1 ? e.Amount.ToString() : "")}", gc);
                    if (_showGainLines && e.ActorId is { } aid2 && units.TryGetValue(aid2, out Piece? a2) && a2 != g)
                        _shots.Add(new Shot
                        {
                            FromTeam = a2.Team, FromSlot = a2.Slot, ToTeam = g.Team, ToSlot = g.Slot,
                            Thin = true, Tint = gc, Life = ShotLife,
                        });
                }
                break;

            // 移動（第97期 D5）。旧位置 → 新位置の矢印を1本引く。
            case BattleEventKind.Move:
                if (e.TargetId is { } mid && units.TryGetValue(mid, out Piece? m))
                {
                    int was = SlotBefore(idx, mid);
                    if (was >= 0 && was != m.Slot)
                        _shots.Add(new Shot
                        {
                            FromTeam = m.Team, FromSlot = was, ToTeam = m.Team, ToSlot = m.Slot,
                            Thin = true, Tint = CHeal, Life = ShotLife,
                        });
                    AddPop(m, "移動", CHeal);
                }
                break;
        }
    }

    void PushDamage(int idx, BattleEvent e, Dictionary<int, Piece> units)
    {
        if (e.ActorId is not { } aid || e.TargetId is not { } tid) return;
        if (!units.TryGetValue(aid, out Piece? a) || !units.TryGetValue(tid, out Piece? b)) return;
        if (a == b) return;   // 反動（追い打ちの踏み込みすぎ）は自分から自分なので線にならない

        // 全体攻撃は線を引かない——5本の線が交差するだけで、型としてはむしろ読めなくなる。
        if (e.Pattern == AttackPattern.All)
        {
            if (!_flashes.Any(f => f.Team == b.Team && f.Life > FlashLife * 0.8))
                _flashes.Add(new Flash { Team = b.Team, Color = a.Team == b.Team ? CFf : CDmg, Life = FlashLife });
            return;
        }

        // 同じ一振りの直前の着弾を探す。Attack より手前へは遡らない。
        bool hasPrev = false; int prevTeam = 0, prevSlot = 0;
        for (int i = idx - 1; i >= 0; i--)
        {
            BattleEvent pe = _result.Events[i];
            if (pe.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart
                        or BattleEventKind.Skill or BattleEventKind.Charge) break;
            if (pe.Kind == BattleEventKind.Damage && pe.ActorId == aid && pe.Pattern == e.Pattern
                && pe.TargetId is { } ptid && units.TryGetValue(ptid, out Piece? pp) && pp != b)
            {
                hasPrev = true; prevTeam = pp.Team; prevSlot = pp.Slot;
                break;
            }
        }

        _shots.Add(new Shot
        {
            FromTeam = a.Team, FromSlot = a.Slot,
            ToTeam = b.Team, ToSlot = b.Slot,
            Friendly = e.FriendlyFire || a.Team == b.Team,
            Reaction = e.Reaction,
            Relayed = e.Relayed,
            Pattern = e.Pattern,
            HasPrev = hasPrev, PrevTeam = prevTeam, PrevSlot = prevSlot,
            Life = ShotLife,
        });
    }

    /// <summary>同じカードに同時に出た札を段違いにする（重なると読めない）。</summary>
    void AddPop(Piece u, string text, Color color)
    {
        int stack = _pops.Count(q => q.Team == u.Team && q.Slot == u.Slot && q.Life > PopLife * 0.75);
        _pops.Add(new Pop { Team = u.Team, Slot = u.Slot, Text = text, Color = color,
                            Stack = Math.Min(stack, 3), Life = PopLife });
    }

    /// <summary>その駒が <paramref name="idx"/> の直前に居たスロット。移動の矢印の始点。</summary>
    int SlotBefore(int idx, int id)
    {
        for (int i = idx - 1; i >= 0; i--)
        {
            BattleEvent pe = _result.Events[i];
            if (pe.TargetId != id) continue;
            if (pe.Kind is BattleEventKind.Move or BattleEventKind.Revive or BattleEventKind.Summon)
                return pe.Slot;
        }
        Piece? p = _roster.FirstOrDefault(r => r.Id == id);
        return p?.Slot ?? -1;
    }

    // ---- UI 組み立て ----------------------------------------------------

    static StyleBoxFlat Box(Color bg, Color? border = null, int width = 1)
    {
        var sb = new StyleBoxFlat { BgColor = bg, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2,
                                    CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2 };
        if (border is { } b)
        {
            sb.BorderColor = b;
            sb.BorderWidthLeft = sb.BorderWidthRight = sb.BorderWidthTop = sb.BorderWidthBottom = width;
        }
        sb.ContentMarginLeft = sb.ContentMarginRight = 8;
        sb.ContentMarginTop = sb.ContentMarginBottom = 6;
        return sb;
    }

    static Label Text(string s, int size, Color color)
    {
        var l = new Label { Text = s };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    Control BuildHeader()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddChild(Text("戦闘再生装置", 18, CInk));

        // 73 行あるのでボタンは並べない（第97期 D6）。名前で選べれば足りる。
        var picker = new HBoxContainer();
        picker.AddThemeConstantOverride("separation", 6);
        var builds = Builds();
        _pick = new OptionButton { CustomMinimumSize = new Vector2(420, 0) };
        _pick.AddThemeFontSizeOverride("font_size", 12);
        for (int i = 0; i < builds.Length; i++) _pick.AddItem($"{i + 1:00}  {builds[i].Name}", i);
        _pick.ItemSelected += id => Load((int)id);
        picker.AddChild(_pick);

        var prevB2 = new Button { Text = "◀" };
        prevB2.Pressed += () => Load(Math.Max(0, _buildIdx - 1));
        picker.AddChild(prevB2);
        var nextB2 = new Button { Text = "▶" };
        nextB2.Pressed += () => Load(Math.Min(Builds().Length - 1, _buildIdx + 1));
        picker.AddChild(nextB2);
        picker.AddChild(Text($"編成 {builds.Length} 行（compare {Presets.Compare.Length} ＋ 交差帯 {Presets.Cross.Length}）", 11, CFaint));
        left.AddChild(picker);

        // モードの切り替え（第98期 V4）。
        //
        // **`compare` は各波を独立に測っているのに、画面は会戦しか再生できなかった。**
        // 96 期ぶんの測定は全部「各波を単独で戦ったときの勝率」なので、
        // 測っているものと見ている絵を突き合わせるには単発が要る。
        var modes = new HBoxContainer();
        modes.AddThemeConstantOverride("separation", 6);

        _bMode = new Button { Text = "会戦", CustomMinimumSize = new Vector2(70, 0), ToggleMode = true };
        _bMode.Pressed += () =>
        {
            _single = _bMode.ButtonPressed;
            _bMode.Text = _single ? "単発" : "会戦";
            SyncModeUi();
            Load(_buildIdx);
        };
        modes.AddChild(_bMode);

        _pickStage = new OptionButton { CustomMinimumSize = new Vector2(150, 0) };
        _pickStage.AddThemeFontSizeOverride("font_size", 12);
        for (int i = 0; i < EnemyCatalog.Stages.Count; i++) _pickStage.AddItem(EnemyCatalog.Stages[i].Name, i);
        _pickStage.ItemSelected += id => { _stage = (int)id; Load(_buildIdx); };
        modes.AddChild(_pickStage);

        // seed は `compare` が回す 0..199 の中から選ぶ。既定 0。
        _pickSeed = new OptionButton { CustomMinimumSize = new Vector2(90, 0) };
        _pickSeed.AddThemeFontSizeOverride("font_size", 12);
        foreach (int sd in new[] { 0, 1, 2, 3, 4, 5, 10, 25, 50, 100, 199 })
            _pickSeed.AddItem($"seed {sd}", sd);
        _pickSeed.ItemSelected += ix => { _seed = _pickSeed.GetItemId((int)ix); Load(_buildIdx); };
        modes.AddChild(_pickSeed);

        // 状態異常の細い線（第98期 V2）。**既定は切**——1回の攻撃で何本も伸びて線が読めなくなる。
        _bGainLines = new Button { Text = "書き手の線", ToggleMode = true };
        _bGainLines.AddThemeFontSizeOverride("font_size", 11);
        _bGainLines.Pressed += () =>
        {
            _showGainLines = _bGainLines.ButtonPressed;
            _shots.Clear();
            _overlay.QueueRedraw();
        };
        modes.AddChild(_bGainLines);

        left.AddChild(modes);

        // 波の選択ボタンは会戦の進行表示に置き換えた。どの波と戦うかは会戦が決める。
        _lProgress = Text("", 12, CDim);
        left.AddChild(_lProgress);
        row.AddChild(left);

        row.AddChild(Stat("会戦", out _lEngVerdict));
        row.AddChild(Stat("決着", out _lVerdict));
        row.AddChild(Stat("ターン", out _lTurns));
        row.AddChild(Stat("連鎖深度", out _lChain));
        return row;
    }

    /// <summary>
    /// モードに応じて出す部品を切り替える（第98期 V4）。
    /// 単発は 1 戦しか無いので部隊送りを隠し、会戦は波と seed を engine が決めるので隠す。
    /// </summary>
    void SyncModeUi()
    {
        _pickStage.Visible = _single;
        if (_bPrevBattle is not null) { _bPrevBattle.Visible = !_single; _bNextBattle.Visible = !_single; }
    }

    static Control Stat(string label, out Label value)
    {
        var v = new VBoxContainer();
        v.AddChild(Text(label, 10, CFaint));
        value = Text("–", 20, CInk);
        v.AddChild(value);
        return v;
    }

    Control BuildBoard()
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Box(CPanel, CLine));

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);

        _lEnemy = Text("敵", 10, CFaint);
        col.AddChild(_lEnemy);
        col.AddChild(Side(1));

        var div = Text("─── 接敵面 — 前列どうしが向かい合う ───", 10, CFaint);
        div.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(div);

        col.AddChild(Side(0));
        _lPlayer = Text("味方", 10, CFaint);
        col.AddChild(_lPlayer);

        // 連鎖メーター。畳みかけているかは勝率にも決着ターン数にも出ないので、ここだけが見える形。
        var chain = new PanelContainer();
        chain.AddThemeStyleboxOverride("panel", Box(CPanel2, CLine));
        var crow = new HBoxContainer();
        crow.AddThemeConstantOverride("separation", 12);
        _lChainBig = Text("0", 28, CFaint);
        crow.AddChild(_lChainBig);
        var cv = new VBoxContainer();
        cv.AddChild(Text("このターンの同時撃破", 10, CFaint));
        _pipBox = new HBoxContainer();
        _pipBox.AddThemeConstantOverride("separation", 5);
        cv.AddChild(_pipBox);
        crow.AddChild(cv);
        _lChainNote = Text("", 11, CDim);
        _lChainNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _lChainNote.HorizontalAlignment = HorizontalAlignment.Right;
        crow.AddChild(_lChainNote);
        chain.AddChild(crow);
        col.AddChild(chain);
        col.AddChild(BuildLegend());

        return panel;
    }

    /// <summary>
    /// 凡例（第97期 D1）。<b>線の形と色が何を意味するかを画面の隅に置く。</b>
    ///
    /// <para>描き分けを足しても、意味の対応表が画面の外にあると読めない
    /// ——この画面の目的は「何が起きたかが1目で区別できる」ことなので、凡例は演出ではなく本体。
    /// 色は <see cref="CurrencyColor"/> と同じ定数から引いている（写しを作らない）。</para>
    /// </summary>
    Control BuildLegend()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Box(CPanel2, CLine));

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 14);
        flow.AddThemeConstantOverride("v_separation", 2);
        panel.AddChild(flow);

        void Chip(string label, Color c)
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 4);
            h.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(14, 3),
                                       SizeFlagsVertical = SizeFlags.ShrinkCenter });
            h.AddChild(Text(label, 10, CDim));
            flow.AddChild(h);
        }

        Chip("単体 細1本", CDmg);
        Chip("薙ぎ 中太＋扇", CDmg);
        Chip("貫き 極太の連なり", CDmg);
        Chip("全体 盤面が光る", CDmg);
        Chip("反撃（棘・仇討ち・軋み。逆向き）", CCounter);
        Chip("巻き込み", CFf);
        Chip("中継（破線）", CDmg);
        Chip("移動", CHeal);
        flow.AddChild(Text("｜ 線は直前の1振りぶんだけ ／ 通貨の色 →", 10, CFaint));
        Chip("毒", CPoison);
        Chip("燃", CBurn);
        Chip("傷/深手", CWound);
        Chip("痺", CStun);
        Chip("標", CMark);
        Chip("破片", CArmor);
        Chip("なまり", CDull);
        flow.AddChild(Text("｜ 席の名前はカードの右上（前1/前3/中央/後1/後3）", 10, CFaint));

        return panel;
    }

    Control Side(int team)
    {
        int[][] order = team == 1 ? EnemyLaneOrder : PlayerLaneOrder;
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 10);

        foreach (int[] lane in order)
        {
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 6);
            col.CustomMinimumSize = new Vector2(150, 0);
            foreach (int slot in lane)
            {
                if (slot < 0)
                {
                    col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 54) });
                    continue;
                }
                Card c = MakeCard(team);
                _cards[(team, slot)] = c;
                col.AddChild(c.Root);
            }
            row.AddChild(col);
        }
        return row;
    }

    Card MakeCard(int team)
    {
        var c = new Card();
        c.Style = Box(CGround, CLine);
        c.Style.BorderWidthLeft = 3;
        c.Style.BorderColor = team == 1 ? CEnemy : CPlayer;

        c.Root = new PanelContainer { CustomMinimumSize = new Vector2(0, 54) };
        c.Root.AddThemeStyleboxOverride("panel", c.Style);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 3);
        c.Root.AddChild(v);

        var top = new HBoxContainer();
        c.Name = Text("", 12, CInk);
        c.Name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        c.Name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        top.AddChild(c.Name);
        c.Pat = Text("", 9, CFaint);
        top.AddChild(c.Pat);
        v.AddChild(top);

        c.Fill = new StyleBoxFlat { BgColor = team == 1 ? CEnemy : CPlayer };
        c.Bar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 5), MaxValue = 1 };
        c.Bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = CPanel2 });
        c.Bar.AddThemeStyleboxOverride("fill", c.Fill);
        v.AddChild(c.Bar);

        var bottom = new HBoxContainer();
        c.Hp = Text("", 10, CDim);
        c.Hp.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bottom.AddChild(c.Hp);
        c.Status = Text("", 10, CAccent);
        bottom.AddChild(c.Status);
        v.AddChild(bottom);
        return c;
    }

    Control BuildFeed()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(300, 0) };
        panel.AddThemeStyleboxOverride("panel", Box(CPanel, CLine));
        _feed = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollFollowing = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FitContent = false,
        };
        _feed.AddThemeFontSizeOverride("normal_font_size", 12);
        panel.AddChild(_feed);
        return panel;
    }

    Control BuildTransport()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Box(CPanel, CLine));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);

        // **手送りが主、自動再生が副**（第98期 V3）。左端に送りを置く。
        var back = new Button { Text = "◀", CustomMinimumSize = new Vector2(40, 0) };
        back.Pressed += () => { SetPlaying(false); Step(-1); };
        row.AddChild(back);

        var fwd = new Button { Text = "▶", CustomMinimumSize = new Vector2(40, 0) };
        fwd.Pressed += () => { SetPlaying(false); Step(1); };
        row.AddChild(fwd);

        var nextA = new Button { Text = "次の攻撃" };
        nextA.Pressed += NextAttack;
        row.AddChild(nextA);

        var nextT = new Button { Text = "次のターン" };
        nextT.Pressed += NextTurn;
        row.AddChild(nextT);

        // Battle の行き来（スクラブは Battle 内なので）。単発モードでは 1 戦しか無いので隠す。
        _bPrevBattle = new Button { Text = "◀部隊" };
        _bPrevBattle.Pressed += () => JumpBattle(-1);
        row.AddChild(_bPrevBattle);

        _bNextBattle = new Button { Text = "部隊▶" };
        _bNextBattle.Pressed += () => JumpBattle(1);
        row.AddChild(_bNextBattle);

        _scrub = new HSlider { SizeFlagsHorizontal = SizeFlags.ExpandFill, MinValue = 0, Step = 1 };
        _scrub.ValueChanged += v =>
        {
            if (_syncing) return;
            SetPlaying(false);
            _idx = (int)v;
            Redraw();
        };
        row.AddChild(_scrub);

        _lPos = Text("0 / 0", 11, CDim);
        _lPos.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(_lPos);

        // 自動再生は副次機能。**既定は停止**（`Load` が `SetPlaying(false)` で入る）。
        _bPlay = new Button { Text = "自動再生", CustomMinimumSize = new Vector2(80, 0) };
        _bPlay.Pressed += () => SetPlaying(!_playing);
        row.AddChild(_bPlay);

        // 0.25× を足した（第98期 V3）。1イベントずつ目で追うには 0.5× でも速い。
        foreach (double s in new[] { 0.25, 0.5, 1.0, 2.0, 4.0 })
        {
            double captured = s;
            var b = new Button { Text = $"{s}×" };
            b.AddThemeFontSizeOverride("font_size", 11);
            b.Pressed += () => _speed = captured;
            row.AddChild(b);
        }
        return panel;
    }

    // ---- 読み込み -------------------------------------------------------

    /// <summary>
    /// 再生する台本（第98期 V4）。**会戦と単発の違いをここ1箇所に閉じる。**
    ///
    /// <para>会戦は <see cref="EngagementEngine"/> が5部隊ぶんまとめて返すが、単発は
    /// <see cref="BattleEngine.Run(IReadOnlyList{UnitState}, IReadOnlyList{UnitState}, int, bool,
    /// ColossusRule?, YokeRule?, HushRule?, MartyrRule?, ExposeRule?, ShoveRule?, BearRule?,
    /// RelayRule?, SlanderRule?, OverbearRule?, ScaleRule?, ScapegoatRule?, DivertRule?, GoadRule?,
    /// FinisherRule?, FavorRule?, BlazeRule?, FunnelRule?, WhetMask?, CreakRule?, SeverRule?,
    /// ThinBladeRule?, ThornRule?, SutureRule?, SpillWoundRule?, MendRule?, IgniteRule?,
    /// GatherRule?, SoakRule?, DeepRule?, CurseRule?, CounterProbe?)"/> の1戦きり。
    /// <b>再生側から見ると「Battle の列と、その開始盤面の列」でしかない</b>ので、
    /// そこだけを持つ型に揃えて、画面のコードを1本にする。</para>
    /// </summary>
    sealed class Script
    {
        public bool IsEngagement;
        public List<BattleResult> Battles = new();
        public List<IReadOnlyList<BattleOpening>> Openings = new();
        /// <summary>各 Battle の敵の波（<see cref="EnemyCatalog.Stages"/> の添字）。</summary>
        public List<int> StageIx = new();
        /// <summary>各 Battle の味方部隊。単発は常に 0。</summary>
        public List<int> SquadIx = new();
        public bool Won;
        public int Cleared;
        public int Seed;
    }

    /// <summary>
    /// 単発の1戦を台本にする（第98期 V4）。
    ///
    /// <para><b>`compare` と同じ呼び出し</b>——<c>BattleEngine.Run(編成, 波, seed)</c> の
    /// <c>verbose</c> だけを真にする。他の引数は1つも渡さない（既定のノブで回る）ので、
    /// <c>docs/balance.md</c> のセルとそのまま突き合わせられる。</para>
    ///
    /// <para>開始盤面は <see cref="EngagementEngine"/> と<b>同じ手順</b>で組む——
    /// <c>Materialize</c> してから <c>Run</c> を呼び、<b>Run の後に</b> record にする。
    /// <c>InstanceId</c> は <c>ctx.Add</c> が Run の中で振るので、先に組むと空になる。</para>
    /// </summary>
    static Script RunSingle(Formation player, int stageIx, int seed)
    {
        List<UnitState> p = BattleEngine.Materialize(player, BattleContext.PlayerTeam);
        List<UnitState> e = BattleEngine.Materialize(EnemyCatalog.Stages[stageIx].Enemy, BattleContext.EnemyTeam);

        // Run の前に控える（Hp・Slot・型・HasFallenBack は戦闘で動く）。
        var pending = p.Concat(e)
            .Select(u => (Unit: u, u.Hp, u.MaxHp, Attack: u.CurrentAttack, u.Slot,
                          Pattern: u.CurrentPattern, u.HasFallenBack))
            .ToList();

        BattleResult r = BattleEngine.Run(p, e, seed, verbose: true);

        var s = new Script { IsEngagement = false, Won = r.PlayerWon, Cleared = r.PlayerWon ? 1 : 0, Seed = seed };
        s.Battles.Add(r);
        s.Openings.Add(pending.Select(x => new BattleOpening(
            x.Unit.InstanceId, x.Unit.TeamId, x.Unit.Def.Id, x.Unit.Def.Name,
            x.Slot, x.Hp, x.MaxHp, x.Attack, x.Unit.Def.Attack, x.Pattern, x.HasFallenBack)).ToList());
        s.StageIx.Add(stageIx);
        s.SquadIx.Add(0);
        return s;
    }

    /// <summary>会戦を台本にする（現行）。判定も持ち越しも <see cref="EngagementEngine"/> の中。</summary>
    static Script RunEngagement(Formation player, int seed)
    {
        EngagementResult eng = EngagementEngine.Run(new[] { player }, EnemyCatalog.EngagementColumn,
                                                    seed, verbose: true);
        var s = new Script
        {
            IsEngagement = true, Won = eng.PlayerWon, Cleared = eng.EnemySquadsCleared, Seed = seed,
            Battles = eng.Battles.ToList(), Openings = eng.Openings.ToList(),
            StageIx = eng.Pairings.Select(x => x.Item2).ToList(),
            SquadIx = eng.Pairings.Select(x => x.Item1).ToList(),
        };
        return s;
    }

    void Load(int buildIdx)
    {
        _buildIdx = buildIdx;

        var (name, player) = Builds()[buildIdx];
        _playerName = name;
        // ◀▶ で送ったときも一覧の選択を合わせる（Selected の代入は ItemSelected を出さない）。
        if (_pick is not null) _pick.Selected = buildIdx;

        // ここが全部。判定はエンジンが済ませて返す。会戦のルール（持ち越し・部隊交代）も
        // BattleCore 側（EngagementEngine）に閉じていて、この画面は Battles[b] の台本を
        // 再生するだけ。味方は当面1部隊（コンセプトメモの複数部隊はまず敵側だけで試す）。
        _script = _single ? RunSingle(player, _stage, _seed) : RunEngagement(player, _seed);

        LoadBattle(0);
        SetPlaying(false);
    }

    /// <summary>
    /// 台本の中の1つの Battle を再生対象に据える。盤面は Formation からではなく
    /// Openings[b]（持ち越した HP・攻撃力・パターンを含む開始盤面）から組む。
    /// Formation から def.MaxHp で組み直すと持ち越しが表示に出ない。
    /// </summary>
    void LoadBattle(int battleIdx)
    {
        _battleIdx = battleIdx;
        _result = _script.Battles[battleIdx];
        int pi = _script.SquadIx[battleIdx], ei = _script.StageIx[battleIdx];

        _roster.Clear();
        foreach (BattleOpening o in _script.Openings[battleIdx])
            _roster.Add(new Piece
            {
                Id = o.InstanceId, Team = o.TeamId, Slot = o.Slot, Name = o.Name,
                Hp = o.Hp, MaxHp = o.MaxHp,
                Attack = o.Attack, BaseAttack = o.BaseAttack, Pattern = o.Pattern,
            });

        _lPlayer.Text = _script.IsEngagement
            ? $"味方 — {_playerName}（第{pi + 1}部隊）"
            : $"味方 — {_playerName}";
        _lEnemy.Text = $"敵 — {EnemyCatalog.Stages[ei].Name}";

        // **いま見ているものが `docs/balance.md` のどのセルか**（第98期 V4 の目的）。
        _lProgress.Text = _script.IsEngagement
            ? $"会戦 ・ 敵部隊 {ei + 1}/{EnemyCatalog.EngagementColumn.Count} ・ "
              + $"突破 {_script.Cleared} ・ Battle {battleIdx + 1}/{_script.Battles.Count}"
            : $"単発 ・ balance.md のセル: 「{_playerName}」 × {EnemyCatalog.Stages[ei].Name} ・ seed {_script.Seed}"
              + $"（`compare` は seed 0..199 の 200 戦の勝率。これはその 1 戦）";

        _lEngVerdict.Text = _script.IsEngagement
            ? (_script.Won ? "勝利" : "敗北")
            : (_script.Won ? "勝ち" : "負け");
        _lEngVerdict.AddThemeColorOverride("font_color", _script.Won ? CHeal : CDmg);
        _lVerdict.Text = _result.PlayerWon ? "勝利" : "敗北";
        _lVerdict.AddThemeColorOverride("font_color", _result.PlayerWon ? CHeal : CDmg);
        _lTurns.Text = _result.Turns.ToString();
        _lChain.Text = _result.MaxEnemyKillsInOneTurn.ToString();
        _lChain.AddThemeColorOverride("font_color", CAccent);

        foreach (ColorRect p in _pips) p.QueueFree();
        _pips.Clear();
        int cap = Math.Max(1, _result.MaxEnemyKillsInOneTurn);
        for (int i = 0; i < cap; i++)
        {
            var pip = new ColorRect { Color = CPanel2, CustomMinimumSize = new Vector2(32, 10) };
            _pipBox.AddChild(pip);
            _pips.Add(pip);
        }

        _syncing = true;
        _scrub.MaxValue = _result.Events.Count;
        _syncing = false;

        _idx = 0;
        _shots.Clear();
        _pops.Clear();
        _flashes.Clear();
        _shotGroup = int.MinValue;
        _banner = 0;
        _lBanner.Visible = false;
        Redraw();
    }

    void JumpBattle(int delta)
    {
        SetPlaying(false);
        int b = Math.Clamp(_battleIdx + delta, 0, _script.Battles.Count - 1);
        if (b != _battleIdx) LoadBattle(b);
    }

    // ---- 台本を適用して盤面を作る ---------------------------------------

    Dictionary<int, Piece> BuildState(int upto, out int turn, out int kills, out int best,
                                      Dictionary<(int Id, string Key), int>? statuses = null)
    {
        // 複製の初期HPは p.Hp（Opening の持ち越しHP）。p.MaxHp にすると
        // 前の Battle で削られたぶんが巻き戻り、持ち越しが画面に出ない。
        var units = _roster.ToDictionary(
            p => p.Id,
            p => new Piece { Id = p.Id, Team = p.Team, Slot = p.Slot, Name = p.Name,
                             Hp = p.Hp, MaxHp = p.MaxHp,
                             Attack = p.Attack, BaseAttack = p.BaseAttack, Pattern = p.Pattern });
        turn = 0; kills = 0; best = 0;
        statuses?.Clear();

        for (int i = 0; i < upto; i++)
        {
            BattleEvent e = _result.Events[i];
            Piece? t = e.TargetId is { } tid && units.TryGetValue(tid, out Piece? p) ? p : null;

            switch (e.Kind)
            {
                case BattleEventKind.TurnStart:
                    turn = e.Turn; kills = 0;
                    // 継続効果はターン頭のスナップショットで組み直す。持ち越さない。
                    statuses?.Clear();
                    break;
                case BattleEventKind.StatusSnapshot:
                    if (statuses is not null && e.TargetId is { } stid && e.Text is { } skey)
                        statuses[(stid, skey)] = e.Amount;
                    break;
                case BattleEventKind.StatSnapshot:
                    if (t is not null)
                    {
                        t.Attack = e.Amount;
                        // 第98期 V1。**戦闘中に型が変わる駒が5枚ある**ので、開始時の型で固定すると
                        // 画面と盤面が食い違う（下がったセロが単体のまま出ていた）。
                        if (e.Pattern is { } np) t.Pattern = np;
                    }
                    break;
                case BattleEventKind.Damage:
                case BattleEventKind.Heal:
                    if (t is not null) t.Hp = e.HpAfter;
                    break;
                case BattleEventKind.Death:
                    if (t is not null)
                    {
                        t.Alive = false; t.Hp = 0;
                        if (t.Team == 1) { kills++; if (kills > best) best = kills; }
                    }
                    break;
                case BattleEventKind.Summon:
                    if (e.TargetId is { } sid)
                    {
                        UnitDef? def = UnitCatalog.All.FirstOrDefault(u => u.Name == e.Text);
                        units[sid] = new Piece
                        {
                            Id = sid, Team = e.Team ?? 0, Slot = e.Slot, Name = e.Text ?? "?",
                            Hp = e.HpAfter, MaxHp = def?.MaxHp ?? e.HpAfter,
                            Attack = def?.Attack ?? 0, BaseAttack = def?.Attack ?? 0,
                            Pattern = def?.Pattern ?? AttackPattern.Single,
                        };
                    }
                    break;
                case BattleEventKind.Revive:
                    if (t is not null) { t.Alive = true; t.Hp = e.HpAfter; t.Slot = e.Slot; }
                    break;
                case BattleEventKind.Move:
                    if (t is not null) t.Slot = e.Slot;
                    break;
            }
        }
        return units;
    }

    readonly Dictionary<(int Id, string Key), int> _statuses = new();

    void Redraw()
    {
        Dictionary<int, Piece> units = BuildState(_idx, out int turn, out int kills, out int best, _statuses);

        for (int team = 0; team <= 1; team++)
        {
            var bySlot = units.Values.Where(u => u.Team == team).ToDictionary(u => u.Slot, u => u);
            for (int slot = 0; slot < FormationRules.TotalSlots; slot++)
            {
                if (!_cards.TryGetValue((team, slot), out Card? c)) continue;
                if (!bySlot.TryGetValue(slot, out Piece? u))
                {
                    // 空きスロットも枠として残す。
                    // **Visible=false にすると Container が畳んでレーンの深さが崩れ、
                    // 接敵面が揃わなくなる。** 盤面の幾何が読めることがこの画面の要点。
                    // 空き枠自体も情報（召喚専用の4枠は、そこへ駒が湧くまで空いたままになる）。
                    c.Name.Text = "";
                    c.Pat.Text = FormationRules.SeatNames[slot];
                    c.Hp.Text = "";
                    c.Status.Text = "";
                    c.Bar.Value = 0;
                    c.Style.BgColor = new Color(0, 0, 0, 0);
                    c.Style.BorderColor = CLine;
                    c.Root.Modulate = new Color(1, 1, 1, 0.4f);
                    continue;
                }
                c.Style.BgColor = CGround;
                c.Style.BorderColor = team == 1 ? CEnemy : CPlayer;
                c.Name.Text = u.Name;
                // **席の名前を型と並べて出す**（第98期 Phase 0-1）。盤面は敵と味方で
                // 奥行きの向きが左右に反転しているので、カードの位置だけからは
                // 「その駒が前列か後列か」が読めない（実際に読み違いが出ている）。
                // 名前は `FormationRules.SeatNames` から引く——**各所で配列を手写ししない**（Models.cs の doc）。
                c.Pat.Text = $"{FormationRules.SeatNames[slot]}・{PatternLabel(u.Pattern)}";
                c.Bar.Value = u.MaxHp == 0 ? 0 : Math.Clamp((double)u.Hp / u.MaxHp, 0, 1);
                // 積み上げ系は素の値から離れるので、変わっていたら 素→現在 で出す。
                string atk = u.Attack == u.BaseAttack ? $"攻{u.Attack}" : $"攻{u.BaseAttack}→{u.Attack}";
                c.Hp.Text = $"{Math.Max(0, u.Hp)}/{u.MaxHp}   {atk}";
                c.Status.Text = StatusText(u.Id, turn);
                c.Root.Modulate = u.Alive ? Colors.White : new Color(1, 1, 1, 0.28f);
            }
        }

        for (int i = 0; i < _pips.Count; i++)
            _pips[i].Color = i < kills ? CAccent : CPanel2;
        _lChainBig.Text = kills.ToString();
        _lChainBig.AddThemeColorOverride("font_color", kills > 0 ? CAccent : CFaint);
        _lChainNote.Text = $"ここまでの最大 {best} / この戦闘の最大 {_result.MaxEnemyKillsInOneTurn}";

        RedrawFeed(units);

        // 直前に適用したイベントが干渉なら筋を積む。位置は「そのとき」の盤面から取る。
        if (_idx > 0) PushShot(_idx - 1, units);
        _overlay.QueueRedraw();

        _lPos.Text = $"{_idx} / {_result.Events.Count}";
        _bPlay.Text = _playing ? "停止" : "再生";
        _syncing = true;
        _scrub.Value = _idx;
        _syncing = false;
    }

    void RedrawFeed(Dictionary<int, Piece> units)
    {
        // 直近の20件だけ出す。全件を毎フレーム組み直すと長期戦（30ターン）で重い。
        int from = Math.Max(0, _idx - 20);
        _feed.Clear();
        for (int i = from; i < _idx; i++)
        {
            BattleEvent e = _result.Events[i];
            // スナップショットは表示用の配管。出来事ではないので一覧には出さない。
            if (e.Kind is BattleEventKind.StatusSnapshot or BattleEventKind.StatSnapshot) continue;
            Color c = e.Kind switch
            {
                // 第97期 D3/D4。通貨の色は盤面の札と揃える——別の色にすると対応が読めない。
                BattleEventKind.StatusGain => CurrencyColor(e.Text ?? ""),
                BattleEventKind.Status => StatusWorkColor(e.Text),
                BattleEventKind.Death => CDmg,
                BattleEventKind.Highlight => CAccent,
                BattleEventKind.Charge => CAccent,   // 予告は見せ場と同じ重さで浮かせる
                BattleEventKind.TurnStart => CInk,
                _ => e.FriendlyFire ? CFf : CDim,
            };
            bool now = i == _idx - 1;
            _feed.PushColor(now ? CInk : c);
            _feed.AddText((now ? "▸ " : "  ") + EventText(e, units) + "\n");
            _feed.Pop();
        }
    }

    /// <summary>
    /// 構造化イベントから文を組み立てる。**ログの文字列は解析しない**（LogKind の原則と同じ）。
    /// </summary>
    string EventText(BattleEvent e, Dictionary<int, Piece> units)
    {
        string N(int? id) => id is { } i && units.TryGetValue(i, out Piece? p) ? p.Name : "？";
        return e.Kind switch
        {
            BattleEventKind.TurnStart => $"── ターン {e.Turn}",
            BattleEventKind.Attack => $"{N(e.ActorId)} → {N(e.TargetId)}（{PatternLabel(e.Pattern ?? AttackPattern.Single)} {e.Amount}）",
            BattleEventKind.Damage => $"{N(e.TargetId)} に {e.Amount}{(e.FriendlyFire ? "（巻き込み）" : "")}",
            BattleEventKind.Heal => $"{N(e.TargetId)} が {e.Amount} 回復",
            BattleEventKind.Death => $"{N(e.TargetId)} 撃破",
            BattleEventKind.Summon => $"{e.Text} が現れた",
            BattleEventKind.Revive => $"{N(e.TargetId)} が繋ぎ直された",
            BattleEventKind.Move => $"{N(e.TargetId)} が動いた",
            BattleEventKind.Status => $"{N(e.TargetId)} — {e.Text} {e.Amount}",
            BattleEventKind.StatusGain =>
                $"{(e.ActorId is null ? "" : N(e.ActorId) + " → ")}{N(e.TargetId)} に "
                + $"{StatusKeys.LabelOf(e.Text ?? "")} +{e.Amount}",
            BattleEventKind.Highlight => e.Text ?? "",
            // 次に何が来るかを添える。溜めを見て回復や速攻を合わせるのが狙いなので、
            // 「溜めた」だけでは情報が足りない（Amount = 次の倍率、Pattern = 次の攻撃型）。
            BattleEventKind.Charge =>
                $"{N(e.ActorId)} は{e.Text ?? "力を溜めている"}"
                + $"（次: {PatternLabel(e.Pattern ?? AttackPattern.Single)} ×{e.Amount}%）",
            BattleEventKind.Skill => $"{N(e.ActorId)} は{e.Text ?? "術を使った"}",
            _ => e.Kind.ToString(),
        };
    }

    /// <summary>
    /// その駒がいま負っている継続効果を1行にまとめる（第97期 D4）。
    ///
    /// <para>札の文字列は engine が <see cref="StatusKeys.LabelOf"/> で付けたものがそのまま入っている
    /// （<c>StatusSnapshot</c> の <c>Text</c>）ので、ここで名前を作り直さない
    /// ——<b>手写しの表を作らない</b>。キーが増えれば札も自動で増える。</para>
    ///
    /// <para><b>手番（<c>IdleTurn</c>）だけは量ではなくターン番号</b>で、0 に戻す箇所が engine に
    /// 1つも無い。数として出すと「手番7」が最後まで残って読めないので、
    /// <b>そのターンに立っているときだけ「休」</b>と出す。</para>
    /// </summary>
    string StatusText(int id, int turn)
    {
        var parts = new List<string>();
        foreach (((int Id, string Key) k, int v) in _statuses)
        {
            if (k.Id != id || v <= 0) continue;
            if (k.Key == IdleLabel) { if (v >= turn) parts.Add("休"); continue; }
            parts.Add($"{k.Key}{v}");
        }
        parts.Sort(StringComparer.Ordinal);   // 並びを安定させる（毎フレーム入れ替わると読めない）
        return string.Join(" ", parts);
    }

    /// <summary>手番の札。<b>手で書かない</b>——engine と同じ関数から引く。</summary>
    static readonly string IdleLabel = StatusKeys.LabelOf(StatusKeys.IdleTurn);

    static string PatternLabel(AttackPattern p) => p switch
    {
        AttackPattern.Sweep => "薙ぎ",
        AttackPattern.Pierce => "貫き",
        AttackPattern.All => "全体",
        _ => "単体",
    };

    // ---- 再生 -----------------------------------------------------------

    void Step(int n)
    {
        _idx = Math.Clamp(_idx + n, 0, _result.Events.Count);
        Redraw();
    }

    void NextTurn()
    {
        SetPlaying(false);
        int i = _idx;
        while (i < _result.Events.Count && _result.Events[i].Kind != BattleEventKind.TurnStart) i++;
        _idx = Math.Min(_result.Events.Count, i + 1);
        Redraw();
    }

    /// <summary>
    /// 次の一振りまで飛ぶ（第98期 V3）。<see cref="BattleEventKind.Attack"/> を1つ**適用した直後**で止める
    /// ——止めた時点で線が出ていないと「次の攻撃へ」の意味が無い。
    /// スナップショットは出来事ではないので飛ばす（そこで止まると盤面が動かない）。
    /// </summary>
    void NextAttack()
    {
        SetPlaying(false);
        int i = _idx;
        while (i < _result.Events.Count && _result.Events[i].Kind != BattleEventKind.Attack) i++;
        // 見たいのは着弾なので、その振りの Damage までまとめて進める。
        if (i < _result.Events.Count)
        {
            int j = i + 1;
            while (j < _result.Events.Count
                   && _result.Events[j].Kind is BattleEventKind.Damage or BattleEventKind.StatusGain
                                             or BattleEventKind.Death or BattleEventKind.Heal
                                             or BattleEventKind.Status or BattleEventKind.Highlight) j++;
            _idx = j;
        }
        else _idx = _result.Events.Count;
        Redraw();
    }

    void SetPlaying(bool v)
    {
        _playing = v;
        if (v && _idx >= _result.Events.Count) _idx = 0;
        _wait = 0;
        _bPlay.Text = _playing ? "停止" : "再生";
    }

    public override void _Process(double delta)
    {
        // 筋は再生していなくても薄れさせる（コマ送りでも1本ずつ確かめられる）。
        // 速度に比例して薄れるので、4× でも線が渋滞しない。
        if (_shots.Count > 0 || _pops.Count > 0 || _flashes.Count > 0)
        {
            double fade = delta * Math.Max(1.0, _speed);
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                Shot s = _shots[i];
                s.Life -= fade;
                if (s.Life <= 0) _shots.RemoveAt(i);
                else _shots[i] = s;
            }
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                Pop q = _pops[i];
                q.Life -= fade;
                if (q.Life <= 0) _pops.RemoveAt(i);
                else _pops[i] = q;
            }
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                Flash f = _flashes[i];
                f.Life -= fade;
                if (f.Life <= 0) _flashes.RemoveAt(i);
                else _flashes[i] = f;
            }
            _overlay.QueueRedraw();
        }

        // 部隊戦の切れ目。バナーを一拍見せてから次の Battle をロードして続ける。
        if (_banner > 0)
        {
            _banner -= delta * _speed;
            if (_banner <= 0)
            {
                bool keep = _playing;
                LoadBattle(_battleIdx + 1);
                SetPlaying(keep);   // LoadBattle は状態を触らないが、明示して再生を継ぐ
            }
            return;
        }

        if (!_playing) return;
        if (_idx >= _result.Events.Count)
        {
            if (_battleIdx + 1 < _script.Battles.Count) { ShowInterlude(); return; }
            SetPlaying(false); Redraw(); return;
        }

        _wait -= delta * _speed;
        if (_wait > 0) return;

        BattleEvent e = _result.Events[_idx];
        _idx++;
        _wait = Dur.TryGetValue(e.Kind, out double d) ? d : 0.26;
        Redraw();
    }

    /// <summary>
    /// Battle の末尾。敵が替わるなら増援、味方が替わるなら次の部隊のバナーを見せる。
    /// 相打ちなら両方出る。左の生存駒は動かさない——持ち越し駒は前の Battle を終えた
    /// スロットのまま次の Opening に載るので、同じカード位置に表示され続ける。
    /// </summary>
    void ShowInterlude()
    {
        int pi = _script.SquadIx[_battleIdx], ei = _script.StageIx[_battleIdx];
        int npi = _script.SquadIx[_battleIdx + 1], nei = _script.StageIx[_battleIdx + 1];

        var parts = new List<string>();
        if (nei != ei) parts.Add("ENEMY REINFORCEMENTS");
        if (npi != pi) parts.Add($"{Ordinal(npi + 1)} SQUAD");
        _lBanner.Text = string.Join("  /  ", parts);
        _lBanner.Visible = true;
        _banner = BannerTime;
    }

    static string Ordinal(int n) => n switch { 1 => "1ST", 2 => "2ND", 3 => "3RD", _ => $"{n}TH" };

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } k) return;
        switch (k.Keycode)
        {
            case Key.Space: SetPlaying(!_playing); Redraw(); break;
            case Key.Right: SetPlaying(false); Step(1); break;
            case Key.Left: SetPlaying(false); Step(-1); break;
            default: return;
        }
        AcceptEvent();
    }
}
