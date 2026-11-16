using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 窓を減らして盤を見せる（第479期・指示書 design/PHASE479_LESS_WINDOWS_SPEC.md §1〜§3・3D の層）。**画面は判定を1つも持たない。**
//
//   1. 横の札: 遭遇・連戦・迎撃の窓を、暗幕なしで画面の右 1／4 に寄せる（盤は見えたまま・時計はもともと戦闘の前で止まっている）
//   2. 出来事の窓（第478期）はやめる: 中間拠点を取った ＝ 旗と浮く金 ／ 金の使いどころ ＝ 隊の札の上の小さなボタン ／ 負けた戦闘 ＝ 隊の札の 1 行
//   3. 説明はセリフで: 顔つきの 1 行（`Map11Lines`）を、盤の出来事に合わせて 1 度だけ出す
//   4. 結果の窓は 踏破・刻・失った駒の顔・金 だけ。細かい戦績は「詳細」の奥
// =====================================================================================

public partial class Map11Main
{
    /// <summary>横の札にした窓の中身（遭遇・連戦・迎撃）。</summary>
    private readonly List<Control> _sidePanels = new();

    /// <summary>窓を横の札にする（3D だけ）。暗幕は透かして入力も通す——出ているあいだは作戦ターンの途中なので、盤を押しても行き先は変わらない。</summary>
    private void MakeSide(Control overlay)
    {
        if (!Map3DMode.Active || overlay is not ColorRect dim || overlay.GetChildCount() == 0 || overlay.GetChild(0) is not Control panel) return;
        dim.Color = new Color(0, 0, 0, 0);
        dim.MouseFilter = Control.MouseFilterEnum.Ignore;
        // 札は透かさない（下の名札が文字の後ろに透けて読みにくかった）。
        if (panel is PanelContainer box) box.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.03f, 0.05f, 0.043f, 1f), UiKit.Line));
        panel.AnchorLeft = 0.75f; panel.AnchorRight = 0.992f;
        panel.AnchorTop = panel.AnchorBottom = 0.47f;
        panel.OffsetLeft = panel.OffsetRight = panel.OffsetTop = panel.OffsetBottom = 0;
        panel.GrowVertical = Control.GrowDirection.Both;
        _sidePanels.Add(panel);
    }

    /// <summary>いま出ている横の札の四角（画面の座標・名札を重ねない・検査が読む）。</summary>
    internal IEnumerable<Rect2> SideRects()
    {
        foreach (Control p in _sidePanels)
            if (p.IsVisibleInTree()) { Rect2 r = p.GetGlobalRect(); yield return new Rect2(r.Position * _uiScale, r.Size * _uiScale); }
    }

    /// <summary>横の札が出ているか。</summary>
    internal bool SideShown => _sidePanels.Any(p => p.IsVisibleInTree());

    /// <summary>盤を隠す窓（暗幕つき）が出ているか——結果・確かめ・頭の札・投資の札。3D の遭遇・連戦・迎撃は横の札なので数えない。</summary>
    internal bool CoverShown => _resultOverlay.Visible || _confirmOverlay.Visible || _introOverlay is { Visible: true } || _investOverlay is { Visible: true }
                                || (!Map3DMode.Active && (_encounterOverlay.Visible || _interceptOverlay.Visible || _siegeOverlay.Visible));

    private static IEnumerable<Button> ButtonsIn(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Button b && child is not MapUnitBadge) yield return b;
            foreach (Button deeper in ButtonsIn(child)) yield return deeper;
        }
    }

    /// <summary>上の真ん中の出来事の 1 行を短くする（3D・20 字まで）: 「／」より後ろの添え書きは落とす。</summary>
    internal static string ShortToast(string text)
    {
        int cut = text.IndexOf('／');
        if (cut > 0) text = text[..cut].TrimEnd();
        cut = text.IndexOf('（');
        if (cut > 0 && text.Length > 20) text = text[..cut].TrimEnd();
        return text.Length > 22 ? text[..21] + "…" : text;
    }

    // ---------------- 横の札の中身 ----------------

    /// <summary>遭遇の札（3D）: 敵の顔と数・名前・重さの印・賞金・「戦う」だけ。</summary>
    private void LeanEncounter(Map11State.Squad s, Map11State.Node node, int alive)
    {
        _encounterTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _encounterTitle.Text = (node.Cell < 0 ? "迎撃　" : "") + (node.IsHero ? "勇者" : node.Def.Name);
        _encounterFaces.AddThemeConstantOverride("separation", 10);
        string gold = St.Stage.Gold.On && node.Def.Gold >= 0 && St.KillGold(node) > 0 ? $"　[color=#ffd874]賞金 ＋{St.KillGold(node)}[/color]" : "";
        string tag = node.Def.Tag.Length > 0 ? $"　[b][color=#ffd874]{node.Def.Tag}[/color][/b]" : "";
        _encounterBody.AddThemeFontSizeOverride("normal_font_size", 17);
        _encounterBody.Text = $"{alive} 体 ・ HP {NodeHpPercent(node):F0}%{tag}{gold}";
        foreach (Button b in ButtonsIn(_encounterOverlay)) { b.AddThemeFontSizeOverride("font_size", 20); b.CustomMinimumSize = new Vector2(0, 52); }
    }

    /// <summary>連戦の札（3D）: 次の相手の顔と数・今の隊の枚数と HP・同じ大きさのボタン 2 つ。</summary>
    private void LeanSiege(Map11State.Squad s, Map11State.Node next, int alive, int foeAlive, int backCell)
    {
        string foe = next.IsHero ? "勇者" : next.Def.Name;
        _siegeTag.Text = "連戦";
        _siegeTitle.Text = $"次は {foe}";
        foreach (Node child in _siegeFaces.GetChildren())
            if (child is MapNoRecovery) { _siegeFaces.RemoveChild(child); child.QueueFree(); }
        _siegeFaces.AddThemeConstantOverride("separation", 10);
        _siegeStats.AddThemeFontSizeOverride("font_size", 18);
        _siegeStats.Text = $"{s.Label}　{alive} 枚 ・ HP {SquadHpPercent(s):F0}%\n{foe}　{foeAlive} 体 ・ HP {NodeHpPercent(next):F0}%";
        _siegeHp.Visible = false;
        _siegeBody.Visible = false;
        _siegeGo.Text = "続けて戦う";
        _siegeBack.Text = "引いて待つ";
        _siegeBack.TooltipText = $"{PlaceName(backCell)}へ戻る";
        _siegeGo.AddThemeFontSizeOverride("font_size", 20);
        _siegeBack.AddThemeFontSizeOverride("font_size", 20);
        if (next.Def.Name == MapStage.KeeperName) SayOnce(Map11Lines.Keeper12);
    }

    // ---------------- 隊の札: 小さなボタンと、失われた隊の 1 行 ----------------

    /// <summary>隊の札の上に出す小さなボタン（呼び戻す・強化・休む）。出せるかどうかは `Map11State` に聞くだけ。</summary>
    private (bool Revive, bool Enhance, bool Resting) CardActions(Map11State.Squad q)
    {
        if (!St.Stage.Gold.On && !St.Time.On) return default;
        if (St.Finished || Map11Session.TurnRunning || q.Lost || q.Units is not { Count: > 0 } || !St.CanReform(q.Index)) return default;
        bool revive = St.Stage.Gold.On && St.Stage.Gold.Revive > 0 && St.FallenUnits.Count > 0 && St.Gold >= St.Stage.Gold.Revive;
        bool enhance = St.Stage.Gold.On && St.Stage.Stars.On && St.EnhancePrice > 0 && St.Gold >= St.EnhancePrice;
        Dest here = q.Pos == St.Graph.Home ? Dest.Home : Dest.Of(St.Graph, q.Pos);
        bool rest = St.Time.On && St.Hurt(q.Index) && Map11Session.Destinations[q.Index] != here;
        return (revive, enhance, rest);
    }

    /// <summary>失われた隊の札の 1 行（「全滅　砦の守将 残り 1」）。戦闘の記録と盤を読むだけ。</summary>
    private string LostLine(Map11State.Squad q)
    {
        Map11Session.Line? l = Map11Session.BattleLog.LastOrDefault(x => !x.Won && x.Squad == q.Label);
        if (l is null) return "全滅";
        string foe = System.Text.RegularExpressions.Regex.Replace(l.Foe, @"^\d+\.\s*", "");
        Map11State.Node? node = St.Nodes.SelectMany(r => r).Concat(St.Spawns).FirstOrDefault(n => !n.Cleared && n.Def.Name == foe);
        int left = node?.Units?.Count(u => u.IsAlive) ?? node?.Def.Enemy.Occupied().Count() ?? 0;
        return left > 0 ? $"全滅　{foe} 残り {left}" : $"全滅　{foe}";
    }

    /// <summary>隊の札の上のボタンが押された（0 ＝ 呼び戻す ／ 1 ＝ 強化 ／ 2 ＝ 休む）。呼び戻す・強化は今の編成の窓をその隊で開く。</summary>
    private void OnSquadAction3D(int squad, int kind)
    {
        if (squad < 0 || squad >= St.Squads.Length || St.Finished || Map11Session.TurnRunning) return;
        Map11State.Squad s = St.Squads[squad];
        if (kind == 2)
        {
            Map11Session.SetDestination(squad, s.Pos == St.Graph.Home ? Dest.Home : Dest.Of(St.Graph, s.Pos));
            ShowToast($"{s.Label} は休む", GoldColor, 2.5f);
            Refresh();
            return;
        }
        _selected = squad;
        _guideSquadChosen = true;
        if (!_reform) ToggleReform(); else Refresh();
    }

    // ---------------- セリフ ----------------

    private static Map11State? _leanState;

    /// <summary>セリフを 1 度だけ出す（1 章の「窓を減らす」版・3D だけ）。言えたか。</summary>
    private bool SayOnce(string key)
    {
        if (_hud3d is null || !Chapter1.Active || !Chapter1.Rule.Lean || _linesOff) return false;
        Map11Lines.Line line = Map11Lines.Of(key);
        if (line.Stage.Length > 0 && line.Stage != St.Stage.Id) return false;
        if (Map11Lines.WasSaid(key) || Map11Lines.SpeakerOf(St, line.Speaker) is not { } who) return false;
        Map11Lines.Mark(key);
        _hud3d.Say(who, $"「{line.Text}」", Map11Lines.Seconds);
        GD.Print($"MAP3D_SAY key={key} who={who.Name} text={line.Text}");
        return true;
    }

    /// <summary>セリフを出さない（絵の口・レイアウトの検査——器具が盤を進める通し。検査が自分で出すときは <see cref="SayForCheck"/>）。</summary>
    private static bool _linesOff;

    /// <summary>毎フレーム: 盤の出来事に合わせてセリフを出す（出すのは 1 フレームに 1 つ・吹き出しが出ているあいだは待つ）。</summary>
    private void ProcessLines3D()
    {
        if (_hud3d is null || !Chapter1.Active || !Chapter1.Rule.Lean || _linesOff) return;
        if (!ReferenceEquals(_leanState, St))
        {
            _leanState = St;
            // 1 章を 1-1 から始め直したら、言ったことの覚えを捨てる。
            if (Chapter1.Index == 0 && Chapter1.Attempt == 0 && St.Turn == 0 && Map11Session.BattleLog.Count == 0) Map11Lines.Forget();
        }
        if (_hud3d.SayShown || CoverShown || _reform || St.Finished) return;
        string id = St.Stage.Id;
        if (id == "1-1")
        {
            if (St.Turn == 0 && SayOnce(Map11Lines.Start11)) return;
            // 峠の関に触れた: 行き先に触れたマスに峠の関がいる ／ 峠の関との遭遇の札が出た。
            bool gate = _hoverDestination is { } d && !d.Dest.AtHome && !d.Dest.IsPortal
                        && St.Nodes.SelectMany(r => r).Any(n => !n.Cleared && n.Pos == d.Dest.Id && n.Def.Name == MapStage.Stage11Ch476GateName);
            gate |= _encounterOverlay.Visible && Map11Session.PendingNode is { } pn && pn.Def.Name == MapStage.Stage11Ch476GateName;
            if (gate && SayOnce(Map11Lines.Gate11)) return;
        }
        else if (id == "1-2")
        {
            if (St.Turn == 0 && SayOnce(Map11Lines.Start12)) return;
            int pass = St.Graph.Cells.Where(c => c.Kind == MapGraph.Kind.Outpost && PlaceName(c.Id) == "関所").Select(c => c.Id).DefaultIfEmpty(-1).First();
            if (pass >= 0 && St.HeldOutposts.Contains(pass) && SayOnce(Map11Lines.Pass12)) return;
            if (pass >= 0 && St.Stage.PortalGate > 0 && St.GateOpen && !St.HeldOutposts.Contains(pass) && !Map11Lines.WasSaid(Map11Lines.Pass12) && SayOnce(Map11Lines.NoPass12)) return;
        }
        if (St.Stage.Gold.On && St.Gold >= Map11Lines.GoldLine && St.Turn > 0 && !Map11Session.TurnRunning) SayOnce(Map11Lines.Gold);
    }

    // ---------------- 結果の窓（3D）: 踏破・刻・失った駒の顔・金 だけ。細かい戦績は「詳細」の奥 ----------------

    private Button? _resultMore;
    private string _resultFull = "";
    private bool _resultOpen;

    private void LeanResult()
    {
        if (!Map3DMode.Active) return;
        _resultFull = _resultBody.Text;
        _resultOpen = false;
        if (_resultMore is null)
        {
            _resultMore = UiKit.ActionButton("詳細", UiKit.Muted);
            _resultMore.CustomMinimumSize = new Vector2(0, 30);
            _resultMore.Pressed += () => { _resultOpen = !_resultOpen; ShowLeanResult(); };
            _resultBody.GetParent().AddChild(_resultMore);
        }
        ShowLeanResult();
    }

    private void ShowLeanResult()
    {
        var lines = new List<string>();
        if (St.Portal.On && St.Captured) lines.Add($"制圧　{St.CapturedTurn + 1} 刻目" + (St.Stage.TurnCap > 0 && St.Stage.TimeLimit ? $"（期限 {St.Stage.TurnCap}）" : ""));
        else lines.Add($"{Map11Session.ShownTurn} 刻目");
        if (St.Stage.Gold.On) lines.Add($"金　{St.Gold}");
        _resultBody.Text = _resultOpen ? _resultFull : string.Join(Nl, lines);
        _resultMore!.Text = _resultOpen ? "詳細を閉じる" : "詳細";
        // 窓は中身の分だけ（詳細を開いたら今までの高さ）。
        if (_resultOverlay.GetChild(0) is Control panel) { panel.OffsetTop = _resultOpen ? -300 : -150; panel.OffsetBottom = _resultOpen ? 300 : 150; }
        // 失った駒の顔（詳細を開いたら、終わりの隊の並びに戻す）。
        if (_resultOpen) { RefreshResultFaces(); return; }
        foreach (Node child in _resultFaces.GetChildren()) { _resultFaces.RemoveChild(child); child.QueueFree(); }
        var fallen = St.FallenUnits.Select(u => u.Def).ToList();
        _resultFaces.AddChild(UiKit.Text(fallen.Count == 0 ? "失った駒　なし" : $"失った駒　{fallen.Count}", 15, UiKit.Muted));
        if (fallen.Count == 0) return;
        var faces = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        faces.AddThemeConstantOverride("h_separation", 4);
        foreach (UnitDef d in fallen)
        {
            var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(52, 52), TooltipText = d.Name, Modulate = new Color(0.72f, 0.66f, 0.66f) };
            face.SetUnit(d);
            faces.AddChild(face);
        }
        _resultFaces.AddChild(faces);
    }
}
