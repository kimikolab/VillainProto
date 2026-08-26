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

    // ---- 盤面の幾何 ---------------------------------------------------
    //
    // スロット 0-2 が前列 / 3 が中衛 / 4-5 が後列。レーンは3本で奥行きが違う。
    // レーン0={前1,後1} レーン1={前2,中,後2} レーン2={前3}（BattleCore の FormationRules と同じ）。
    //
    // 表示は「前列どうしが向かい合う」向きに揃える。敵は奥→手前、味方は手前→奥。
    // 深さが揃わないぶんは -1（空き枠）で詰めて、接敵面が一直線になるようにする。
    // **貫きがレーンを前から走る**という規則が目で分かることがこの画面の要点なので、
    // ここを 3×2 の均等グリッドにしてはいけない。
    static readonly int[][] EnemyLaneOrder = { new[] { -1, 4, 0 }, new[] { 5, 3, 1 }, new[] { -1, -1, 2 } };
    static readonly int[][] PlayerLaneOrder = { new[] { 0, 4, -1 }, new[] { 1, 3, 5 }, new[] { 2, -1, -1 } };

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
    };

    // ---- 見るための編成 -------------------------------------------------
    //
    // **BattleSim の CompareBuilds からの写し。** ここは「見る」ためだけの一覧で、
    // 勝率の検証は今まで通り BattleSim 側が正。編成を差し替えたら手で揃えること
    // （揃っていなくても勝率表は壊れないが、見ているものが別物になる）。
    static (string Name, Formation F)[] Builds() => new (string, Formation)[]
    {
        ("追撃×死 (ハギ×リィカ)", Formation.Build(front1: UnitCatalog.Hagi, front2: UnitCatalog.Zoto,
            mid: UnitCatalog.Golm, back1: UnitCatalog.Rica, back2: UnitCatalog.Vel)),
        ("反撃改 (ドハ×カド)", Formation.Build(front1: UnitCatalog.Hisa, front2: UnitCatalog.Kado,
            front3: UnitCatalog.Doha, mid: UnitCatalog.Nono, back1: UnitCatalog.Nel)),
        ("逆しま改 (クビ×ウツ)", Formation.Build(front2: UnitCatalog.Golm, front3: UnitCatalog.Gald,
            mid: UnitCatalog.Kubi, back1: UnitCatalog.Nel, back2: UnitCatalog.Utsu)),
        ("死の連鎖 (リィカ軸)", Formation.Build(front2: UnitCatalog.Zoto, front3: UnitCatalog.Mug,
            mid: UnitCatalog.Golm, back1: UnitCatalog.Rica, back2: UnitCatalog.Vel)),

        // ここから継続効果が見える編成。上の4つはどれも毒も燃焼も持たないので、
        // 状態異常の表示を確かめるには別の軸が要る。
        ("毒 (グザ×ミオ×ラウ)", Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Sid,
            mid: UnitCatalog.Guza, back1: UnitCatalog.Mio, back2: UnitCatalog.Rau)),
        ("毒+ベニ+ラウ", Formation.Build(front2: UnitCatalog.Gald, front3: UnitCatalog.Sid,
            mid: UnitCatalog.Guza, back1: UnitCatalog.Rau, back2: UnitCatalog.Beni)),
        ("燃焼 (ボルグ×ホタ)", Formation.Build(front1: UnitCatalog.Nono, front2: UnitCatalog.Gald,
            front3: UnitCatalog.Mudo, back1: UnitCatalog.Hota, back2: UnitCatalog.Borg)),
    };

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
    /// </summary>
    struct Shot
    {
        public int FromTeam, FromSlot, ToTeam, ToSlot;
        public bool Friendly;
        public double Life;
    }

    const double ShotLife = 0.55;

    // ---- 状態 -----------------------------------------------------------

    int _buildIdx;
    string _playerName = "";
    EngagementResult _eng = null!;
    int _battleIdx;
    BattleResult _result = null!;   // いま再生している Battle（_eng.Battles[_battleIdx]）
    List<Piece> _roster = new();
    readonly Dictionary<(int Team, int Slot), Card> _cards = new();
    readonly List<Shot> _shots = new();
    ShotOverlay _overlay = null!;

    int _idx;
    bool _playing;
    double _wait;
    double _speed = 1.0;

    Label _lVerdict = null!, _lTurns = null!, _lChain = null!, _lPos = null!;
    Label _lEnemy = null!, _lPlayer = null!, _lChainBig = null!, _lChainNote = null!;
    Label _lProgress = null!, _lEngVerdict = null!, _lBanner = null!;

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

        Load(0);
    }

    // ---- 攻撃の筋 -------------------------------------------------------

    void PaintShots(ShotOverlay layer)
    {
        Transform2D inv = layer.GetGlobalTransform().AffineInverse();

        foreach (Shot s in _shots)
        {
            if (!_cards.TryGetValue((s.FromTeam, s.FromSlot), out Card? a)) continue;
            if (!_cards.TryGetValue((s.ToTeam, s.ToSlot), out Card? b)) continue;

            Vector2 from = inv * a.Root.GetGlobalRect().GetCenter();
            Vector2 to = inv * b.Root.GetGlobalRect().GetCenter();

            float k = (float)(s.Life / ShotLife);
            Color c = s.Friendly ? CFf : CDmg;
            c.A = k * 0.9f;

            layer.DrawLine(from, to, c, 2f * k + 1f, antialiased: true);

            // 着弾側に印を置く。線だけだと向きが読めない。
            layer.DrawCircle(to, 4f * k + 2f, c);
        }
    }

    /// <summary>直前に適用したイベントが干渉なら、筋を1本積む。</summary>
    void PushShot(BattleEvent e, Dictionary<int, Piece> units)
    {
        if (e.Kind != BattleEventKind.Damage) return;
        if (e.ActorId is not { } aid || e.TargetId is not { } tid) return;
        if (!units.TryGetValue(aid, out Piece? a) || !units.TryGetValue(tid, out Piece? b)) return;
        if (a == b) return;   // 反動（追い打ちの踏み込みすぎ）は自分から自分なので線にならない

        _shots.Add(new Shot
        {
            FromTeam = a.Team, FromSlot = a.Slot,
            ToTeam = b.Team, ToSlot = b.Slot,
            Friendly = e.FriendlyFire || a.Team == b.Team,
            Life = ShotLife,
        });
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

        // 編成が増えると1行に収まらないので折り返す。
        var picker = new HFlowContainer();
        picker.AddThemeConstantOverride("h_separation", 6);
        picker.AddThemeConstantOverride("v_separation", 4);
        var builds = Builds();
        for (int i = 0; i < builds.Length; i++)
        {
            int captured = i;
            var b = new Button { Text = builds[i].Name };
            b.AddThemeFontSizeOverride("font_size", 12);
            b.Pressed += () => Load(captured);
            picker.AddChild(b);
        }
        left.AddChild(picker);

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

        _bPlay = new Button { Text = "再生", CustomMinimumSize = new Vector2(70, 0) };
        _bPlay.Pressed += () => SetPlaying(!_playing);
        row.AddChild(_bPlay);

        var back = new Button { Text = "◀" };
        back.Pressed += () => { SetPlaying(false); Step(-1); };
        row.AddChild(back);

        var fwd = new Button { Text = "▶" };
        fwd.Pressed += () => { SetPlaying(false); Step(1); };
        row.AddChild(fwd);

        var nextT = new Button { Text = "次T" };
        nextT.Pressed += NextTurn;
        row.AddChild(nextT);

        // スクラブは Battle 内なので、Battle の行き来はここで。
        var prevB = new Button { Text = "◀部隊" };
        prevB.Pressed += () => JumpBattle(-1);
        row.AddChild(prevB);

        var nextB = new Button { Text = "部隊▶" };
        nextB.Pressed += () => JumpBattle(1);
        row.AddChild(nextB);

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

        foreach (double s in new[] { 0.5, 1.0, 2.0, 4.0 })
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

    void Load(int buildIdx)
    {
        _buildIdx = buildIdx;

        var (name, player) = Builds()[buildIdx];
        _playerName = name;

        // ここが全部。判定はエンジンが済ませて返す。会戦のルール（持ち越し・部隊交代）も
        // BattleCore 側（EngagementEngine）に閉じていて、この画面は Battles[b] の台本を
        // 再生するだけ。味方は当面1部隊（コンセプトメモの複数部隊はまず敵側だけで試す）。
        _eng = EngagementEngine.Run(new[] { player }, EnemyCatalog.EngagementColumn,
                                    seed: 0, verbose: true);

        LoadBattle(0);
        SetPlaying(false);
    }

    /// <summary>
    /// 会戦の中の1つの Battle を再生対象に据える。盤面は Formation からではなく
    /// Openings[b]（持ち越した HP・攻撃力・パターンを含む開始盤面）から組む。
    /// Formation から def.MaxHp で組み直すと持ち越しが表示に出ない。
    /// </summary>
    void LoadBattle(int battleIdx)
    {
        _battleIdx = battleIdx;
        _result = _eng.Battles[battleIdx];
        (int pi, int ei) = _eng.Pairings[battleIdx];

        _roster.Clear();
        foreach (BattleOpening o in _eng.Openings[battleIdx])
            _roster.Add(new Piece
            {
                Id = o.InstanceId, Team = o.TeamId, Slot = o.Slot, Name = o.Name,
                Hp = o.Hp, MaxHp = o.MaxHp,
                Attack = o.Attack, BaseAttack = o.BaseAttack, Pattern = o.Pattern,
            });

        _lPlayer.Text = $"味方 — {_playerName}（第{pi + 1}部隊）";
        _lEnemy.Text = $"敵 — {EnemyCatalog.Stages[ei].Name}";
        _lProgress.Text = $"敵部隊 {ei + 1}/{EnemyCatalog.EngagementColumn.Count} ・ "
            + $"突破 {_eng.EnemySquadsCleared} ・ Battle {battleIdx + 1}/{_eng.Battles.Count}";
        _lEngVerdict.Text = _eng.PlayerWon ? "勝利" : "敗北";
        _lEngVerdict.AddThemeColorOverride("font_color", _eng.PlayerWon ? CHeal : CDmg);
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
        _banner = 0;
        _lBanner.Visible = false;
        Redraw();
    }

    void JumpBattle(int delta)
    {
        SetPlaying(false);
        int b = Math.Clamp(_battleIdx + delta, 0, _eng.Battles.Count - 1);
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
                    if (t is not null) t.Attack = e.Amount;
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
        Dictionary<int, Piece> units = BuildState(_idx, out _, out int kills, out int best, _statuses);

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
                    // 空き枠自体も情報（散開は隣に味方がいないことが条件、5体を6枠に入れる限り必ず1つ空く）。
                    c.Name.Text = "";
                    c.Pat.Text = "";
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
                c.Pat.Text = PatternLabel(u.Pattern);
                c.Bar.Value = u.MaxHp == 0 ? 0 : Math.Clamp((double)u.Hp / u.MaxHp, 0, 1);
                // 積み上げ系は素の値から離れるので、変わっていたら 素→現在 で出す。
                string atk = u.Attack == u.BaseAttack ? $"攻{u.Attack}" : $"攻{u.BaseAttack}→{u.Attack}";
                c.Hp.Text = $"{Math.Max(0, u.Hp)}/{u.MaxHp}   {atk}";
                c.Status.Text = StatusText(u.Id);
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
        if (_idx > 0) PushShot(_result.Events[_idx - 1], units);
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

    /// <summary>その駒がいま負っている継続効果を1行にまとめる。</summary>
    string StatusText(int id)
    {
        var parts = new List<string>();
        foreach (((int Id, string Key) k, int v) in _statuses)
            if (k.Id == id && v > 0) parts.Add($"{k.Key}{v}");
        parts.Sort(StringComparer.Ordinal);   // 並びを安定させる（毎フレーム入れ替わると読めない）
        return string.Join(" ", parts);
    }

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
        if (_shots.Count > 0)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                Shot s = _shots[i];
                s.Life -= delta * Math.Max(1.0, _speed);
                if (s.Life <= 0) _shots.RemoveAt(i);
                else _shots[i] = s;
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
            if (_battleIdx + 1 < _eng.Battles.Count) { ShowInterlude(); return; }
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
        (int pi, int ei) = _eng.Pairings[_battleIdx];
        (int npi, int nei) = _eng.Pairings[_battleIdx + 1];

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
