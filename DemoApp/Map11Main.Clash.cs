using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 遭遇の台（第479期の追記・ポンの決定 2026-11-16・3D の層）。遭遇・連戦・迎撃を、盤の中央を空けたまま画面の下 1／3 に出す。
//
//   盤の上: 当たる隊に青い輪・敵に赤い輪
//   下 1／3: 左 ＝ 味方の陣形の台 ／ 右 ＝ 敵の陣形の台（第479期の続き 2 で入れ替えた——戦闘画面・マップと同じ向き・前列どうしが中央で向き合う）
//           席に顔・HP の棒・☆・倒れた席は暗く・味方は隣どうしの関係の線・強い敵の顔に王冠・味方の先頭の駒が勝ち目を 1 行話す
//   中央下: 勝ち目の印（同じ戦闘を裏で 20 回回した勝ちの数——16 以上 ＝ 優勢・8〜15 ＝ 互角・7 以下 ＝ 劣勢）・賞金・ボタン
//   戦闘から戻ったら、同じ台の上に結果（勝ち ／ 負けの帯・倒れた駒が暗くなる）を 2 秒
//
// **画面は判定を1つも持たない。** 戦う ／ 引き返す ／ 続ける ／ 引く ／ 迎撃に出す は `Map11Session` の口を呼ぶだけ。
// 勝ち目は駒の写し（`Map11State.CloneForOdds`）で `BattleEngine.Run` を回して数えるだけで、盤は 1 ビットも動かさない。
// 今までの窓（遭遇 ／ 連戦 ／ 迎撃）は 3D では中身を隠したまま「出ている」の印として残す（通しの門・遊ぶ口が読む）。
// =====================================================================================

public partial class Map11Main
{
    private enum ClashMode { None, Encounter, Siege, Intercept, Result }

    internal const int OddsRuns = 20, OddsGood = 16, OddsEven = 8;
    /// <summary>裏の 20 回のうち、この回数以上で倒れた味方の駒の顔に割れの印を付ける（第479期の続き・ポン 2026-11-17・敵の側は付けない・文なし）。</summary>
    internal const int RiskRuns = 10;

    private ClashMode _clashMode;
    private Control? _clash;
    private PanelContainer _clashPanel = null!;
    private ClashBoard _clashFoe = null!, _clashOwn = null!;
    private ClashLead _clashFoeLead = null!, _clashOwnLead = null!;
    private Label _clashOdds = null!, _clashGold = null!, _clashBand = null!, _clashSay = null!, _clashHold = null!;
    /// <summary>足止めの印（第480期・勇者との会戦の台だけ・検査と遊ぶ口が読む）。</summary>
    internal string ClashHoldText => _clashHold.Visible ? _clashHold.Text : "";
    internal string ClashBandText => _clashBand.Visible ? _clashBand.Text : "";
    internal bool ClashBackShown => _clashB.Visible;
    private volatile int _oddsHeld = -1;
    private static int _clashHoldsBefore;
    /// <summary>台のセリフ（検査と遊ぶ口が読む）。</summary>
    internal string ClashSayText => _clashSay.Visible ? _clashSay.Text : "";
    /// <summary>台の左右（検査が読む）: 味方の台と敵の台の四角（古い UI の座標）。</summary>
    internal (Rect2 Own, Rect2 Foe) ClashSides => (_clashOwn.GetGlobalRect(), _clashFoe.GetGlobalRect());
    internal bool ClashFoeCrown => _clashFoeLead.Crowned;
    internal string ClashFoeTrait => _clashFoeLead.TraitKey;
    private Button _clashA = null!, _clashB = null!, _clashC = null!, _clashPrev = null!, _clashNext = null!;
    private MeshInstance3D? _ringOwn, _ringFoe, _ringBack;
    private volatile int[]? _oddsFell;
    private bool _backPanned;
    /// <summary>「引いて待つ」「引き返す」に触れているあいだ光らせている、引く先のマス（-1 ＝ 光らせていない・検査と遊ぶ口が読む）。</summary>
    internal int ClashBackCell { get; private set; } = -1;
    /// <summary>遊ぶ口 ／ 検査が「触れている」ことにする口（本物のマウスが無い頭なしの検査のため・見た目だけ）。</summary>
    internal bool ClashBackHover;
    internal int ClashRiskCount => _clashOwn.RiskCount;
    internal Rect2 ClashBackRect => _ringBack is { Visible: true } r ? ScreenBox(r.GlobalPosition, Vector3.Right * 2.6f, Vector3.Back * 2.6f) : default;
    private int _clashSquad = -1;
    private Map11State.Node? _clashNode;
    private readonly List<int> _clashPicks = new();
    private int _clashPickAt, _clashFrames, _clashToken;
    private float _clashResultLeft;
    private volatile int _oddsWins = -1, _oddsToken = -1;

    /// <summary>戦闘へ出る前の姿（戦闘から戻ったとき、同じ台の上に結果を出すため・場面を作り直しても残す）。</summary>
    private sealed record ClashSnap(int Squad, Map11State.Node Node, int Logs, int Gold, string OwnName, string FoeName,
                                    List<ClashBoard.Seat> Own, List<ClashBoard.Seat> Foe, Map11State State);
    private static ClashSnap? _clashFought;

    /// <summary>遭遇の台が出ているか（遊ぶ口・検査・全体図が読む）。</summary>
    internal bool ClashShown => _clash is { Visible: true };
    /// <summary>遭遇の台の四角（画面の座標）。</summary>
    internal Rect2 ClashRect { get { Rect2 r = _clashPanel.GetGlobalRect(); return new Rect2(r.Position * _uiScale, r.Size * _uiScale); } }
    internal string ClashOddsText => _clashOdds.Text;

    private void BuildClash(Control root)
    {
        if (!Map3DMode.Active) return;
        _clash = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _clash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_clash);
        _clashPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.045f, 0.06f, 0.93f), BorderColor = Color.FromHtml("#4c5a66"), BorderWidthTop = 3,
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 8, ContentMarginBottom = 10,
        };
        _clashPanel.AddThemeStyleboxOverride("panel", box);
        _clashPanel.AnchorLeft = 0; _clashPanel.AnchorRight = 1; _clashPanel.AnchorTop = 2f / 3f; _clashPanel.AnchorBottom = 1;
        _clashPanel.OffsetLeft = _clashPanel.OffsetRight = _clashPanel.OffsetTop = _clashPanel.OffsetBottom = 0;
        _clash.AddChild(_clashPanel);
        _clashPanel.GuiInput += e => { if (_clashMode == ClashMode.Result && e is InputEventMouseButton { Pressed: true }) EndClashResult(); };

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        _clashPanel.AddChild(row);
        // 左 ＝ 味方（台の外の端に旗と先頭の駒・前列は中央寄り ＝ 右）
        _clashOwnLead = new ClashLead(PlayerColor);
        _clashOwn = new ClashBoard(frontOnRight: true, PlayerColor);
        row.AddChild(_clashOwnLead);
        row.AddChild(_clashOwn);
        // 中央下 ＝ 勝ち目・賞金・ボタン
        var mid = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        mid.AddThemeConstantOverride("separation", 8);
        row.AddChild(mid);
        _clashBand = UiKit.Text("", 44, Colors.White);
        _clashBand.HorizontalAlignment = HorizontalAlignment.Center;
        mid.AddChild(_clashBand);
        // 味方の先頭の駒のセリフ（味方の台の側 ＝ 左へ寄せた吹き出し）。
        _clashSay = UiKit.Text("", 20, Colors.White);
        _clashSay.AddThemeStyleboxOverride("normal", Map3DHud.Box(new Color(0.09f, 0.12f, 0.16f, 0.96f), PlayerColor.Darkened(0.1f), 2, 12, 6));
        _clashSay.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _clashSay.Visible = false;
        mid.AddChild(_clashSay);
        _clashOdds = UiKit.Text("", 34, GoldColor);
        _clashOdds.HorizontalAlignment = HorizontalAlignment.Center;
        mid.AddChild(_clashOdds);
        _clashHold = UiKit.Text("", 24, GoldColor);
        _clashHold.HorizontalAlignment = HorizontalAlignment.Center;
        _clashHold.Visible = false;
        mid.AddChild(_clashHold);
        var info = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        info.AddThemeConstantOverride("separation", 16);
        _clashGold = UiKit.Text("", 22, Color.FromHtml("#ffd874"));
        info.AddChild(_clashGold);
        mid.AddChild(info);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 10);
        mid.AddChild(buttons);
        Button Make(string text, Color color, int width, Action act)
        {
            Button b = UiKit.ActionButton(text, color);
            b.CustomMinimumSize = new Vector2(width, 60);
            b.AddThemeFontSizeOverride("font_size", 22);
            b.Pressed += act;
            buttons.AddChild(b);
            return b;
        }
        _clashPrev = Make("◀", UiKit.Muted, 52, () => ClashPick(-1));
        _clashA = Make("戦う", GoldColor, 190, ClashGo);
        _clashB = Make("引き返す", PlayerColor, 190, ClashBack);
        _clashNext = Make("▶", UiKit.Muted, 52, () => ClashPick(1));
        _clashC = UiKit.ActionButton("組み直す", UiKit.Muted);
        _clashC.CustomMinimumSize = new Vector2(0, 34);
        _clashC.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        _clashC.Pressed += OpenBreakReform;
        mid.AddChild(_clashC);
        // 右 ＝ 敵（前列は中央寄り ＝ 左）
        _clashFoe = new ClashBoard(frontOnRight: false, EnemyColor);
        _clashFoeLead = new ClashLead(EnemyColor);
        row.AddChild(_clashFoe);
        row.AddChild(_clashFoeLead);
    }

    // ---------------- 出す ----------------

    private List<ClashBoard.Seat> SeatsOf(IEnumerable<UnitState> units, bool own)
        => units.OrderBy(u => u.Slot).Select(u => new ClashBoard.Seat(u.Slot, u.Def, u.IsAlive ? u.Hp : 0, Math.Max(1, u.MaxHp), u.IsAlive,
            own && St.Stage.Stars.On ? St.StarOf(St.Stage.Stars.BaseOf(u.Def).Id).Star : 0)).ToList();

    private void ShowClash(ClashMode mode)
    {
        if (_clash is null) return;
        _clashMode = mode;
        _clashBand.Visible = false;
        _clashOdds.Visible = true;
        _clashPicks.Clear();
        Map11State.Node? node = mode switch
        {
            ClashMode.Encounter => Map11Session.PendingNode,
            ClashMode.Siege => Map11Session.SiegeNode,
            ClashMode.Intercept => Map11Session.InterceptNode,
            _ => null,
        };
        if (node is null) return;
        int squad = mode switch { ClashMode.Encounter => Map11Session.PendingSquad, ClashMode.Siege => Map11Session.SiegeSquad, _ => -1 };
        if (mode == ClashMode.Intercept)
        {
            for (int i = 0; i < St.Squads.Length; i++)
                if (!St.Squads[i].Lost && St.Squads[i].Cell < 0 && St.CanSend(i) && !node.Tried.Contains(i)) _clashPicks.Add(i);
            if (_clashPicks.Count == 0) return;
            _clashPickAt = Math.Clamp(_clashPicks.IndexOf(_selected), 0, _clashPicks.Count - 1);
            squad = _clashPicks[_clashPickAt];
        }
        if (squad < 0) return;
        _clashNode = node;
        _clashA.Text = mode == ClashMode.Siege ? "続けて戦う" : "戦う";
        _clashB.Text = mode == ClashMode.Siege ? "引いて待つ" : "引き返す";
        _clashB.Visible = mode == ClashMode.Siege || (mode == ClashMode.Encounter && St.Stage.EncounterRetreat && !Map11Session.PendingIntercept);
        if (HunterBoard && node.IsHero) _clashB.Visible = false;   // 第480期: 勇者との会戦に「引き返す」は無い（規則は `Map11State.FacingHunter`）
        _clashPrev.Visible = _clashNext.Visible = mode == ClashMode.Intercept && _clashPicks.Count > 1;
        _clashC.Visible = mode == ClashMode.Intercept && Map11Session.InterceptBreak && Enumerable.Range(0, St.Squads.Length).Any(St.CanReform);
        _clashA.Visible = true;
        FillClash(squad, node);
        _clashFrames = 0;
        _clash.Visible = true;
    }

    /// <summary>台の中身（隊と敵）を入れ直し、勝ち目を数え直す。</summary>
    private void FillClash(int squad, Map11State.Node node)
    {
        _clashSquad = squad;
        Map11State.Squad s = St.Squads[squad];
        List<UnitState> own = St.PeekSquad(squad), foe = St.PeekFoe(node);
        var ownSeats = SeatsOf(own, own: true);
        var links = Map11Relations.Of(own.Where(u => u.IsAlive).GroupBy(u => u.Slot).ToDictionary(g => g.Key, g => g.First().Def));
        _clashOwn.Set(ownSeats, links);
        _clashFoe.Set(SeatsOf(foe, own: false), null);
        bool hunter = HunterBoard && node.IsHero;
        string foeName = hunter ? Map11Lines.HunterName : node.IsHero ? "勇者" : node.Def.Name;
        _clashHold.Visible = false;
        _clashOwnLead.Set(own.Where(u => u.IsAlive).OrderBy(u => u.Slot).Select(u => u.Def).FirstOrDefault(), s.Label, $"{own.Count(u => u.IsAlive)} 枚");
        _clashFoeLead.Set(foe.Where(u => u.IsAlive).OrderBy(u => u.Slot).Select(u => u.Def).FirstOrDefault(), foeName, $"{foe.Count(u => u.IsAlive)} 体", crown: node.Def.Strong || hunter,
            trait: node.IsHero ? "" : Map11Lines.FoeTraitOf(foe.Where(u => u.IsAlive).Select(u => u.Def)));
        _clashSay.Visible = false;
        int gold = St.Stage.Gold.On && node.Def.Gold >= 0 ? St.KillGold(node) : 0;
        _clashGold.Text = gold > 0 ? $"賞金 ＋{gold}" : "";
        _clashGold.Visible = gold > 0;
        // 勝ち目: 同じ戦闘を裏で 20 回（駒の写しで）。印は数え終わってから出る。
        _clashOdds.Text = "…";
        _clashOdds.AddThemeColorOverride("font_color", UiKit.Muted);
        int token = ++_clashToken;
        _oddsToken = -1;
        var ownCopy = own.Where(u => u.IsAlive).Select(Map11State.CloneForOdds).ToList();
        var foeCopy = foe.Where(u => u.IsAlive).Select(Map11State.CloneForOdds).ToList();
        if (ownCopy.Count == 0 || foeCopy.Count == 0) { _clashOdds.Text = ""; return; }
        int holdNeed = hunter ? St.Stage.Hero.HoldTurns : 0;
        _oddsHeld = -1;
        System.Threading.Tasks.Task.Run(() =>
        {
            int wins = 0, held = 0;
            var fell = new int[9];
            try
            {
                for (int k = 0; k < OddsRuns; k++)
                {
                    var mine = ownCopy.Select(Map11State.CloneForOdds).ToList();
                    BattleResult run = BattleEngine.Run(mine, foeCopy.Select(Map11State.CloneForOdds).ToList(), 7919 * k + 13, verbose: false);
                    if (run.Turns >= holdNeed) held++;   // 第480期: 足止めに要るターン数以上続いた回数（勇者との会戦だけ読む）
                    if (run.PlayerWon) wins++;
                    foreach (UnitState u in mine) if (!u.IsAlive && u.Slot >= 0 && u.Slot < fell.Length) fell[u.Slot]++;   // 戦闘の終わりに倒れていた駒を数えるだけ
                }
            }
            catch (Exception) { wins = -1; }
            _oddsHeld = holdNeed > 0 ? held : -1;
            _oddsFell = fell;
            _oddsWins = wins;
            _oddsToken = token;
        });
    }

    /// <summary>勝ち目の印（勝ちの数 → 文と色）。</summary>
    internal static (string Text, string Color) OddsOf(int wins)
        => wins >= OddsGood ? ("優勢", "#71d7a1") : wins >= OddsEven ? ("互角", "#f1c96b") : ("劣勢", "#ff6b5e");

    // ---------------- ボタン ----------------

    private void ClashPick(int step)
    {
        if (_clashMode != ClashMode.Intercept || _clashPicks.Count < 2 || _clashNode is null) return;
        _clashPickAt = (_clashPickAt + step + _clashPicks.Count) % _clashPicks.Count;
        _selected = _clashPicks[_clashPickAt];
        FillClash(_selected, _clashNode);
    }

    private void ClashGo()
    {
        switch (_clashMode)
        {
            case ClashMode.Encounter: Fight(); break;
            case ClashMode.Siege: SiegeContinue(); break;
            case ClashMode.Intercept:
                int squad = _clashSquad;
                _interceptOverlay.Visible = false;
                if (!Map11Session.ChooseInterceptor(squad)) { ShowToast("その隊は迎撃に出せません", EnemyColor, 3.0f); Pump(); return; }
                _selected = squad;
                Refresh();
                Fight();
                break;
        }
    }

    private void ClashBack()
    {
        if (_clashMode == ClashMode.Siege) { SiegeRetreat(); return; }
        if (_clashMode != ClashMode.Encounter) return;
        int squad = Map11Session.PendingSquad;
        _encounterOverlay.Visible = false;
        if (Map11Session.DeclineBattle())
            ShowToast($"{St.Squads[squad].Label} は引き返した", GoldColor, 3.0f);
        Refresh();
        Pump();
    }

    // ---------------- 戦闘から戻った後の結果 ----------------

    /// <summary>戦闘へ出る直前の姿を覚える（`Fight` から）。</summary>
    private void RememberClash()
    {
        if (_clash is null || Map11Session.PendingNode is not { } node || Map11Session.PendingSquad < 0) { _clashFought = null; return; }
        int squad = Map11Session.PendingSquad;
        _clashHoldsBefore = St.HunterHolds;
        _clashFought = new ClashSnap(squad, node, Map11Session.BattleLog.Count, St.Gold, St.Squads[squad].Label, HunterBoard && node.IsHero ? Map11Lines.HunterName : node.IsHero ? "勇者" : node.Def.Name,
            SeatsOf(St.PeekSquad(squad), own: true), SeatsOf(St.PeekFoe(node), own: false), St);
    }

    /// <summary>戦闘から戻った: 同じ台の上に結果を出してから、作戦ターンの続きを流す（3D だけ・結果が無ければすぐ流す）。</summary>
    private void AfterBattle3D()
    {
        ClashSnap? snap = _clashFought;
        _clashFought = null;
        // 第480期: 勇者が退いた（HP が半分を割った ／ 0 になった）——結果の前に、門へ戻る勇者と帯「勇者は退いた」。
        if (HunterBoard && St.Finished && St.HunterDriven && !Hs.Routed && _hunterOver is not null && !_linesOff) { _ = HunterRouted(); return; }
        if (_clash is null || snap is null || !ReferenceEquals(snap.State, St) || Map11Session.BattleLog.Count <= snap.Logs || St.Finished) { Pump(); return; }
        Map11Session.Line line = Map11Session.BattleLog[^1];
        // 戦う前の席に、いまの姿（生きているか・HP）を重ねる。隊から外れた駒は倒れた駒。
        List<UnitState> ownNow = St.Squads[snap.Squad].Units ?? new List<UnitState>();
        string BaseId(UnitDef d) => St.Stage.Stars.On ? St.Stage.Stars.BaseOf(d).Id : d.Id;
        // 第480期: 席ではなく駒で引く——戦闘の中で席が入れ替わる駒（ハネ・ワタ・ウツ）が、生きているのに倒れた顔で出ていた。
        var own = snap.Own.Select(seat => ownNow.FirstOrDefault(u => u.IsAlive && BaseId(u.Def) == BaseId(seat.Def)) is { } u
            ? seat with { Hp = u.Hp, Max = Math.Max(1, u.MaxHp), Alive = true } : seat with { Hp = 0, Alive = false }).ToList();
        List<UnitState> foeNow = snap.Node.Units ?? new List<UnitState>();
        var foe = snap.Foe.Select(seat => !snap.Node.Cleared && foeNow.FirstOrDefault(u => u.Slot == seat.Slot && u.IsAlive) is { } u
            ? seat with { Hp = u.Hp, Max = Math.Max(1, u.MaxHp), Alive = true }
            // 第480期: 勇者は駒の列を持たずに残 HP だけを持ち越す（`Node.HpNow`）——抜いていなければ生きている。
            : snap.Node.IsHero && !snap.Node.Cleared ? seat with { Hp = snap.Node.HpNow, Max = Math.Max(1, snap.Node.DefMaxHp), Alive = true }
            : seat with { Hp = 0, Alive = false }).ToList();
        _clashMode = ClashMode.Result;
        _clashSquad = snap.Squad;
        _clashNode = snap.Node;
        _clashOwn.Set(own, null);
        _clashFoe.Set(foe, null);
        _clashOwnLead.Set(own.Where(x => x.Alive).Select(x => x.Def).FirstOrDefault() ?? own.Select(x => x.Def).FirstOrDefault(), snap.OwnName, $"{own.Count(x => x.Alive)} 枚", down: !own.Any(x => x.Alive));
        _clashFoeLead.Set(foe.Where(x => x.Alive).Select(x => x.Def).FirstOrDefault() ?? foe.Select(x => x.Def).FirstOrDefault(), snap.FoeName, $"{foe.Count(x => x.Alive)} 体", down: !foe.Any(x => x.Alive), crown: snap.Node.Def.Strong);
        // 第480期: 勇者に負けたが足を止めた戦闘は「殿」（金）。
        bool rear = HunterBoard && snap.Node.IsHero && !line.Won && St.HunterHolds > _clashHoldsBefore;
        _clashBand.Text = rear ? Map11Lines.RearBand : line.Won ? "勝ち" : "負け";
        _clashBand.AddThemeColorOverride("font_color", rear ? Color.FromHtml("#f1c96b") : line.Won ? Color.FromHtml("#71d7a1") : Color.FromHtml("#ff6b5e"));
        _clashHold.Visible = false;
        _clashBand.Visible = true;
        _clashOdds.Visible = false;
        _clashSay.Visible = false;
        int gold = St.Gold - snap.Gold;
        _clashGold.Text = gold > 0 ? $"賞金 ＋{gold}" : "";
        _clashGold.Visible = gold > 0;
        _clashA.Visible = _clashB.Visible = _clashC.Visible = _clashPrev.Visible = _clashNext.Visible = false;
        _clashResultLeft = 2.0f;
        _clashFrames = 0;
        _clash.Visible = true;
        // 誰が何ターン目に倒れたか（倒れた駒の列から読むだけ・写しの行に添える）。
        string fell = string.Join("・", own.Where(x => !x.Alive).Select(x => St.FallenUnits.LastOrDefault(u => BaseId(u.Def) == BaseId(x.Def)) is { } f ? $"{x.Def.Name}（{f.LastDeathTurn}T）" : x.Def.Name));
        GD.Print($"MAP3D_CLASH_RESULT {snap.OwnName} 対 {snap.FoeName} {(line.Won ? "勝ち" : "負け")} 味方 {own.Count(x => x.Alive)}／{own.Count} 敵 {foe.Count(x => x.Alive)}／{foe.Count} 賞金 {gold} {St.Turn + 1} 刻目 倒れた: {(fell.Length > 0 ? fell : "—")}");
    }

    private void EndClashResult()
    {
        if (_clashMode != ClashMode.Result) return;
        _clashMode = ClashMode.None;
        _clash!.Visible = false;
        if (Map11Session.TurnRunning) Pump(); else Refresh();
    }

    // ---------------- 毎フレーム ----------------

    private void ProcessClash3D(float delta)
    {
        if (_clash is null || _hud3d is null) return;
        bool live = _clashMode == ClashMode.Result || (_clashMode == ClashMode.Encounter && _encounterOverlay.Visible)
                    || (_clashMode == ClashMode.Siege && _siegeOverlay.Visible) || (_clashMode == ClashMode.Intercept && _interceptOverlay.Visible);
        if (!live || _reform) { if (_clash.Visible) _clash.Visible = false; if (!live) _clashMode = ClashMode.None; }
        bool shown = _clash.Visible;
        _hud3d.SetBottomShown(!shown);   // 遭遇のあいだ、いつもの隊の札と刻のボタンは隠す（下の場所を台が使う）
        // 輪は当たる前だけ（結果の帯のあいだは出さない——抜いた敵の輪が隊の足元に残って見えた）。
        if (_ringOwn is not null) _ringOwn.Visible = shown && _clashMode != ClashMode.Result;
        if (_ringFoe is not null) _ringFoe.Visible = shown && _clashMode != ClashMode.Result;
        // 「引いて待つ」「引き返す」に触れているあいだ、引く先のマスを盤の上で光らせる（文は足さない）。
        int backCell = -1;
        if (shown && _clashSquad >= 0 && _clashB.Visible && _clashMode is ClashMode.Siege or ClashMode.Encounter && (_clashB.IsHovered() || ClashBackHover))
            backCell = St.RetreatCellOf(_clashSquad, siege: _clashMode == ClashMode.Siege);
        if (!shown) ClashBackHover = false;
        ClashBackCell = backCell;
        if (backCell >= 0 && _map3d is not null)
        {
            _ringBack ??= ClashRing(Color.FromHtml("#ffe27a"));
            float beat = 1.25f + 0.16f * Mathf.Sin(_clock3d * 7f);
            _ringBack.GlobalPosition = PosOf(backCell) + new Vector3(0, 0.42f, 0);
            _ringBack.Scale = new Vector3(beat, 0.5f, beat);
        }
        if (_ringBack is not null) _ringBack.Visible = backCell >= 0;
        // 引く先が画面の上 2／3 の外なら、隊と引く先の真ん中へ視点を寄せる（触れているあいだだけ・離したら元の 2 者へ戻す）。
        if (backCell >= 0 && !_backPanned && _map3d is not null)
        {
            Vector2 v = GetViewport().GetVisibleRect().Size;
            Vector2 on = _camera.UnprojectPosition(PosOf(backCell));
            if (!new Rect2(40, 40, v.X - 80, v.Y * 2f / 3f - 80).HasPoint(on))
            {
                _cam3d!.FocusOn((PosOf(backCell) + ClashPos(null, _clashSquad)) / 2, snap: false);
                _backPanned = true;
            }
        }
        else if (backCell < 0 && _backPanned) { _backPanned = false; _clashFrames = 0; }
        if (!shown) return;

        if (_clashMode == ClashMode.Result)
        {
            _clashResultLeft -= delta;
            if (_clashResultLeft <= 0) { EndClashResult(); return; }
        }
        // 勝ち目の印（裏で数え終わっていれば出す）。
        if (_clashMode != ClashMode.Result && _oddsToken == _clashToken && _clashOdds.Text == "…")
        {
            if (_oddsWins < 0) _clashOdds.Text = "";
            else
            {
                var (text, color) = OddsOf(_oddsWins);
                _clashOdds.Text = text;
                _clashOdds.AddThemeColorOverride("font_color", Color.FromHtml(color));
                _clashOdds.TooltipText = $"同じ戦闘を {OddsRuns} 回回して {_oddsWins} 勝";
                // 倒れやすい駒（20 回のうち 10 回以上で倒れた）の顔に割れの印。味方の側だけ。
                int[] fell = _oddsFell ?? new int[9];
                var risky = new HashSet<int>(Enumerable.Range(0, fell.Length).Where(i => fell[i] >= RiskRuns));
                _clashOwn.SetRisk(risky);
                string names = string.Join("・", St.PeekSquad(_clashSquad).Where(u => u.IsAlive && risky.Contains(u.Slot)).OrderBy(u => u.Slot).Select(u => $"{u.Def.Name}（{fell[u.Slot]}／{OddsRuns}）"));
                // 味方の先頭の駒が勝ち目に合わせて 1 行話す（文は `Map11Lines`・倒れやすい駒は倒れた回数の多い順）。
                var alive = St.PeekSquad(_clashSquad).Where(u => u.IsAlive).OrderBy(u => u.Slot).ToList();
                string said = "";
                if (alive.Count > 0)
                {
                    var riskyDefs = alive.Where(u => risky.Contains(u.Slot)).OrderByDescending(u => fell[u.Slot]).ThenBy(u => u.Slot).Select(u => u.Def).ToList();
                    said = Map11Lines.OddsLine(alive[0].Def, _oddsWins, OddsGood, OddsEven, riskyDefs).Text;
                    // 第480期 §3-5: 特徴のある敵に初めて当たるときは、勝ち目の 1 行の代わりに見分けの 1 行（雷 ／ 火 ／ 毒 ／ 盾 ／ 弓 ／ 槍）。
                    if (_clashNode is { IsHero: false } tn && Map11Lines.FoeTraitOf(St.PeekFoe(tn).Where(u => u.IsAlive).Select(u => u.Def)) is { Length: > 0 } trait
                        && Map11Lines.FoeTraitLines.TryGetValue(trait, out string? traitLine) && Chapter1.Active && Chapter1.Rule.Lean && Map11Lines.Mark("foe." + trait))
                        said = traitLine;
                    // 第480期: 勇者との会戦。自分から入った殿は「ここは任せろ。行け！」・追いつかれたとき（初回）は「……無理だ。引け！」。
                    if (HunterBoard && _clashNode is { IsHero: true })
                    {
                        if (St.IsRearFight(_clashSquad, Map11Session.FoePhase)) said = _oddsHeld >= 0 && _oddsHeld < OddsEven ? Map11Lines.HunterRearWeak : Map11Lines.HunterRear;
                        else if (_oddsWins < OddsEven && Map11Lines.Mark("1-3.hunter.caught")) said = Map11Lines.HunterCaught;   // 勝ち目が劣勢のときだけ（優勢の隊に「無理だ」と言わせない）
                    }
                    _clashSay.Text = $"「{said}」";
                    _clashSay.Visible = true;
                }
                // 第480期: 足止めの印（裏の 20 回のうち、足止めに要るターン数以上続いた回数）。
                string holdText = "";
                if (HunterBoard && _clashNode is { IsHero: true } && _oddsHeld >= 0)
                {
                    var (ht, hc) = HoldOf(_oddsHeld);
                    holdText = ht;
                    _clashHold.Text = ht;
                    _clashHold.AddThemeColorOverride("font_color", Color.FromHtml(hc));
                    _clashHold.TooltipText = $"同じ戦闘を {OddsRuns} 回回して、{St.Stage.Hero.HoldTurns} ターン以上続いたのが {_oddsHeld} 回";
                    _clashHold.Visible = true;
                }
                GD.Print($"MAP3D_CLASH_ODDS {St.Squads[_clashSquad].Label} 対 {_clashNode?.Def.Name} {_oddsWins}／{OddsRuns} {text} 割れの印: {(names.Length > 0 ? names : "—")} セリフ: {(said.Length > 0 ? $"{alive[0].Def.Name}「{said}」" : "—")}"
                    + (holdText.Length > 0 ? $" 足止め: {_oddsHeld}／{OddsRuns} {holdText}（{(!St.IsRearFight(_clashSquad, Map11Session.FoePhase) ? "追いつかれた" : Map11Session.FoePhase ? "殿に出した（勇者の番に当たった）" : "自分から入った")}）" : ""));
            }
        }

        // ---- 盤の上の輪（当たる隊 ＝ 青・敵 ＝ 赤）と、2 者を画面の上 2／3 に入れる視点 ----
        if (_clashSquad < 0 || _clashNode is null || _map3d is null || _clashMode == ClashMode.Result) return;
        Vector3 a = ClashPos(null, _clashSquad), b = ClashPos(_clashNode, -1);
        _ringOwn ??= ClashRing(Color.FromHtml("#3d8bff"));
        _ringFoe ??= ClashRing(Color.FromHtml("#ff4a3d"));
        float pulse = 1f + 0.06f * Mathf.Sin(_clock3d * 5f);
        _ringOwn.GlobalPosition = a + new Vector3(0, 0.34f, 0); _ringOwn.Scale = new Vector3(pulse, 1, pulse);
        _ringFoe.GlobalPosition = b + new Vector3(0, 0.36f, 0); _ringFoe.Scale = new Vector3(pulse, 1, pulse);
        _clashFrames++;
        if (_clashFrames == 1 || _clashFrames == 4)
        {
            Vector2 view = GetViewport().GetVisibleRect().Size;
            Vector3 mid = (a + b) / 2;
            if (_clashFrames == 1) _cam3d!.FocusOn(mid, snap: true);
            else
            {
                // 2 者の真ん中を、画面の上 2／3 の真ん中あたり（高さの 36%）へ。
                float y = _camera.UnprojectPosition(mid).Y, perUnit = _camera.UnprojectPosition(mid + Vector3.Back).Y - y;
                if (Mathf.Abs(perUnit) > 0.5f) _cam3d!.FocusOn(mid + Vector3.Back * ((y - view.Y * 0.36f) / perUnit), snap: true);
            }
        }
    }

    /// <summary>その隊 ／ その敵部隊の駒が盤の上で立っている場所。</summary>
    private Vector3 ClashPos(Map11State.Node? foe, int squad)
    {
        foreach (var p in _mapPicks)
            if ((foe is not null ? ReferenceEquals(p.Foe, foe) : p.Foe is null && p.Squad == squad) && _pieces3d.TryGetValue(p.Marker, out Map3DPiece? piece) && piece.Shown)
                return piece.GlobalPosition with { Y = 0 };
        return PosOf(foe is not null ? foe.Pos : St.Squads[squad].Pos);
    }

    /// <summary>輪が載っている 2 者の画面の四角（検査が読む）。</summary>
    internal (Rect2 Own, Rect2 Foe) ClashRings()
    {
        Rect2 Box(Vector3 at) => ScreenBox(at, Vector3.Right * 2.1f, Vector3.Back * 2.1f);
        return (Box(_ringOwn?.GlobalPosition ?? Vector3.Zero), Box(_ringFoe?.GlobalPosition ?? Vector3.Zero));
    }

    private MeshInstance3D ClashRing(Color color)
    {
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 1.75f, OuterRadius = 2.1f, Rings = 48, RingSegments = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = color, EmissionEnabled = true, Emission = color, EmissionEnergyMultiplier = 1.6f,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false,
        };
        ring.Scale = new Vector3(1, 0.3f, 1);
        _map3d!.AddChild(ring);
        return ring;
    }
}

/// <summary>陣形の台（遭遇の台の左右）。席に顔・HP の棒・☆。倒れた席は暗く。味方は隣どうしの関係の線。描くだけ。</summary>
public partial class ClashBoard : Control
{
    public readonly record struct Seat(int Slot, UnitDef Def, int Hp, int Max, bool Alive, int Star);

    private readonly bool _frontOnRight;
    private readonly Color _tint;
    private List<Seat> _seats = new();
    private List<Map11Relations.Link> _links = new();
    private HashSet<int> _risk = new();
    private readonly Control _marks = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly List<UnitFaceIcon> _faces = new();
    private const float Face = 62, StepX = 104, StepY = 74;

    /// <summary>席 → 列（0 ＝ 前・1 ＝ 中・2 ＝ 後）と段（0〜2）。編成の 5 席と、召喚の 4 席。</summary>
    private static readonly (int Col, int Row)[] Grid = { (0, 0), (0, 2), (1, 1), (2, 0), (2, 2), (1, 0), (1, 2), (0, 1), (2, 1) };

    public ClashBoard(bool frontOnRight, Color tint)
    {
        _frontOnRight = frontOnRight;
        _tint = tint;
        CustomMinimumSize = new Vector2(StepX * 2 + Face + 34, StepY * 2 + Face + 40);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Ignore;
        _marks.SetAnchorsPreset(LayoutPreset.FullRect);
        _marks.Draw += DrawMarks;
        AddChild(_marks);
    }

    internal int RiskCount => _risk.Count(slot => _seats.Any(s => s.Slot == slot && s.Alive));

    /// <summary>倒れやすい席（割れの印を付ける席）。顔の上に描く。</summary>
    internal void SetRisk(HashSet<int> slots)
    {
        _risk = slots;
        _marks.QueueRedraw();
    }

    /// <summary>割れの印（顔の右上・暗い丸に白い割れ目）。文は出さない。</summary>
    private void DrawMarks()
    {
        foreach (Seat s in _seats)
        {
            if (!s.Alive || !_risk.Contains(s.Slot)) continue;
            Vector2 c = Centre(s.Slot) + new Vector2(Face / 2 - 8, -Face / 2 + 8);
            _marks.DrawCircle(c, 13, new Color(0.10f, 0.03f, 0.03f, 0.95f));
            _marks.DrawArc(c, 13, 0, Mathf.Tau, 24, Color.FromHtml("#ff6b5e"), 2f, true);
            var crack = new[] { c + new Vector2(2, -10), c + new Vector2(-3, -3), c + new Vector2(3, 0), c + new Vector2(-2, 10) };
            _marks.DrawPolyline(crack, Colors.White, 2.5f, true);
            _marks.DrawLine(c + new Vector2(-3, -3), c + new Vector2(-8, -1), Colors.White, 1.5f, true);
            _marks.DrawLine(c + new Vector2(3, 0), c + new Vector2(8, 3), Colors.White, 1.5f, true);
        }
    }

    internal int SeatCount => _seats.Count;
    internal int DarkCount => _seats.Count(s => !s.Alive);
    internal int LinkCount => _links.Count;

    private Vector2 Centre(int slot)
    {
        var (col, row) = Grid[Math.Clamp(slot, 0, Grid.Length - 1)];
        float x = _frontOnRight ? Size.X - 17 - Face / 2 - col * StepX : 17 + Face / 2 + col * StepX;
        return new Vector2(x, 12 + Face / 2 + row * StepY);
    }

    internal void Set(List<Seat> seats, List<Map11Relations.Link>? links)
    {
        _seats = seats;
        _links = links ?? new List<Map11Relations.Link>();
        _risk = new HashSet<int>();
        foreach (UnitFaceIcon f in _faces) { RemoveChild(f); f.QueueFree(); }
        _faces.Clear();
        foreach (Seat s in seats)
        {
            var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(Face, Face), Size = new Vector2(Face, Face), MouseFilter = MouseFilterEnum.Ignore, TooltipText = s.Def.Name };
            face.SetUnit(s.Def);
            face.Modulate = s.Alive ? Colors.White : new Color(0.28f, 0.28f, 0.30f);
            AddChild(face);
            _faces.Add(face);
        }
        MoveChild(_marks, -1);   // 印は顔の上
        Place();
        QueueRedraw();
        _marks.QueueRedraw();
    }

    private void Place()
    {
        for (int i = 0; i < _faces.Count && i < _seats.Count; i++)
            _faces[i].Position = Centre(_seats[i].Slot) - new Vector2(Face, Face) / 2;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) { Place(); QueueRedraw(); }
    }

    public override void _Draw()
    {
        // 台: 編成の 5 席の床（席が空でも描く）。
        for (int slot = 0; slot < 5; slot++)
        {
            Vector2 c = Centre(slot);
            var tile = new Rect2(c - new Vector2(Face / 2 + 7, Face / 2 + 7), new Vector2(Face + 14, Face + 26));
            // 空の席は薄い床だけ（詰めない——席の位置は意味があるので動かさない・ポン 2026-11-17）。
            bool empty = _seats.All(s => s.Slot != slot);
            DrawRect(tile, new Color(_tint.R * 0.22f, _tint.G * 0.22f, _tint.B * 0.22f, empty ? 0.22f : 0.85f));
            DrawRect(tile, new Color(_tint.R, _tint.G, _tint.B, empty ? 0.14f : 0.45f), false, empty ? 1f : 1.5f);
        }
        // 隣どうしの関係の線（得 ＝ 緑・損 ＝ 赤・両方 ＝ 金——今の編成の画面と同じ色）。条件つきは細く。
        foreach (Map11Relations.Link l in _links)
        {
            Color c = SeatLinks.ColorOf(l.Sign);
            DrawLine(Centre(l.From), Centre(l.To), new Color(c.R, c.G, c.B, 0.9f), l.Conditional ? 2f : 4f, true);
        }
        Font font = SeatLinks.LabelFont;
        foreach (Seat s in _seats)
        {
            Vector2 c = Centre(s.Slot);
            // HP の棒
            var bar = new Rect2(c.X - Face / 2, c.Y + Face / 2 + 4, Face, 6);
            DrawRect(bar, new Color(0, 0, 0, 0.6f));
            if (s.Alive) DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * Mathf.Clamp((float)s.Hp / s.Max, 0, 1), bar.Size.Y)), Color.FromHtml("#71d7a1"));
            // ☆
            if (s.Star > 0)
                DrawString(font, new Vector2(c.X - Face / 2, c.Y + Face / 2 + 24), new string('★', s.Star), HorizontalAlignment.Left, -1, 12, s.Alive ? Color.FromHtml("#ffd874") : new Color(0.5f, 0.5f, 0.5f));
        }
    }
}

/// <summary>台の端: 隊の旗と先頭の駒の顔・名前と枚数。描くだけ。</summary>
public partial class ClashLead : VBoxContainer
{
    private readonly UnitFaceIcon _face = new() { CustomMinimumSize = new Vector2(112, 112), MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _name = UiKit.Text("", 20, Colors.White), _count = UiKit.Text("", 18, UiKit.Muted);
    private readonly Color _tint;

    public ClashLead(Color tint)
    {
        _tint = tint;
        CustomMinimumSize = new Vector2(170, 0);
        Alignment = AlignmentMode.Center;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 4);
        var flag = new ColorRect { Color = tint, CustomMinimumSize = new Vector2(150, 10), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(flag);
        _face.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        AddChild(_face);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.ClipText = true;
        _count.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_name);
        AddChild(_count);
    }

    internal string NameText => _name.Text;
    internal bool Crowned => _crown.Visible;
    private readonly Control _crown = new() { MouseFilter = MouseFilterEnum.Ignore, Visible = false };

    /// <summary>王冠の印（顔の上の真ん中・文なし）。強い敵（区画だけの倍率が付く部隊・守将）だけ。</summary>
    private void DrawCrown()
    {
        Vector2 c = new(_crown.Size.X / 2, 4);
        var gold = Color.FromHtml("#ffd24a");
        var shape = new[]
        {
            c + new Vector2(-24, 14), c + new Vector2(-28, -12), c + new Vector2(-13, 0), c + new Vector2(0, -18),
            c + new Vector2(13, 0), c + new Vector2(28, -12), c + new Vector2(24, 14),
        };
        _crown.DrawColoredPolygon(shape, gold);
        _crown.DrawPolyline(shape.Append(shape[0]).ToArray(), Color.FromHtml("#5a3d05"), 2f, true);
        _crown.DrawRect(new Rect2(c + new Vector2(-24, 8), new Vector2(48, 6)), Color.FromHtml("#c98f12"));
        foreach (Vector2 tip in new[] { shape[1], shape[3], shape[5] }) _crown.DrawCircle(tip, 4, Color.FromHtml("#fff1c4"));
    }

    private readonly Control _traitMark = new() { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
    private string _trait = "";
    /// <summary>特徴の印の鍵（検査が読む）。</summary>
    internal string TraitKey => _traitMark.Visible ? _trait : "";

    internal void Set(UnitDef? lead, string name, string count, bool down = false, bool crown = false, string trait = "")
    {
        if (_traitMark.GetParent() is null)
        {
            _traitMark.SetAnchorsPreset(LayoutPreset.FullRect);
            _traitMark.Draw += () => FoeTraitIcon.Draw(_traitMark, new Vector2(14, 14), 20, _trait);
            _face.AddChild(_traitMark);
        }
        _trait = trait;
        _traitMark.Visible = trait.Length > 0 && lead is not null;
        _traitMark.QueueRedraw();
        if (_crown.GetParent() is null)
        {
            _crown.SetAnchorsPreset(LayoutPreset.FullRect);
            _crown.Draw += DrawCrown;
            _face.AddChild(_crown);
        }
        _crown.Visible = crown && lead is not null;
        _crown.QueueRedraw();
        _face.Modulate = down ? new Color(0.28f, 0.28f, 0.30f) : Colors.White;
        _face.Visible = lead is not null;
        if (lead is not null) _face.SetUnit(lead);
        _name.Text = name;
        _name.AddThemeFontSizeOverride("font_size", name.Length > 8 ? 15 : 20);   // 第480期: 長い名前（「白雷の勇者　ライゼル」）は小さく
        _name.AddThemeColorOverride("font_color", _tint.Lightened(0.35f));
        _count.Text = count;
    }
}
