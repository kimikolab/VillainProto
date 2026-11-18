using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 窓を減らして盤を見せる（第479期・指示書 design/PHASE479_LESS_WINDOWS_SPEC.md §1〜§3・3D の層）。**画面は判定を1つも持たない。**
//
//   1. 遭遇・連戦・迎撃は、盤の中央を空けた「遭遇の台」（画面の下 1／3・`Map11Main.Clash.cs`・ポンの決定 2026-11-16 で横の札から替えた）
//   2. 出来事の窓（第478期）はやめる: 中間拠点を取った ＝ 旗と浮く金 ／ 金の使いどころ ＝ 隊の札の上の小さなボタン ／ 負けた戦闘 ＝ 隊の札の 1 行
//   3. 説明はセリフで: 顔つきの 1 行（`Map11Lines`）を、盤の出来事に合わせて 1 度だけ出す
//   4. 結果の窓は 踏破・刻・失った駒の顔・金 だけ。細かい戦績は「詳細」の奥
// =====================================================================================

public partial class Map11Main
{
    /// <summary>
    /// 今までの窓（遭遇・連戦・迎撃）は 3D では中身を出さない（暗幕も透かして入力も通す）——出すのは遭遇の台（`Map11Main.Clash.cs`）。
    /// 窓そのものは「出ている」の印として残す（通しの門・遊ぶ口・作戦ターンの流れが読む）。
    /// </summary>
    private void MakeSide(Control overlay)
    {
        if (!Map3DMode.Active || overlay is not ColorRect dim || overlay.GetChildCount() == 0 || overlay.GetChild(0) is not Control panel) return;
        dim.Color = new Color(0, 0, 0, 0);
        dim.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.Visible = false;
    }

    /// <summary>いま出ている遭遇の台の四角（画面の座標・名札を重ねない・検査が読む）。</summary>
    internal IEnumerable<Rect2> SideRects()
    {
        if (ClashShown) yield return ClashRect;
    }

    /// <summary>遭遇の台が出ているか。</summary>
    internal bool SideShown => ClashShown;

    /// <summary>盤を隠す窓（暗幕つき）が出ているか——結果・確かめ・頭の札・投資の札。3D の遭遇・連戦・迎撃は横の札なので数えない。</summary>
    internal bool CoverShown => StoryShown || _resultOverlay.Visible || _confirmOverlay.Visible || _introOverlay is { Visible: true } || _investOverlay is { Visible: true }
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
        // 第480期 追記 4: 横の辺は行ける道（中間拠点を初めて取ったときに 1 回）。
        else if (id == "1-3" && HunterBoard && !St.Rout && St.HeldOutposts.Count >= 1 && !Map11Session.TurnRunning && SayOnce(Map11Lines.Cross13)) return;
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

    private static readonly Color ResultDim = new(0.005f, 0.01f, 0.008f, 0.78f);

    /// <summary>
    /// 結果の窓の形（3D・第481期 §4-1）。<paramref name="side"/> ＝ ステージの結果: 暗幕なし・画面の右 1／3 の札・視点は味方の拠点へ。
    /// 偽 ＝ 1 章の結果: 今までどおり真ん中の窓（暗幕あり）。
    /// </summary>
    private void ResultCard3D(bool side)
    {
        if (!Map3DMode.Active || _resultOverlay is not ColorRect dim || dim.GetChildCount() == 0 || dim.GetChild(0) is not Control panel) return;
        dim.Color = side ? new Color(0, 0, 0, 0) : ResultDim;
        panel.AnchorTop = 0; panel.AnchorBottom = 1;
        if (side)
        {
            panel.AnchorLeft = 2f / 3f; panel.AnchorRight = 1;
            panel.OffsetLeft = 6; panel.OffsetRight = -14;
            panel.OffsetTop = 110; panel.OffsetBottom = -24;
            if (_cam3d is not null && !_resultFocused) { _resultFocused = true; _cam3d.FocusOn(PosOf(St.Graph.Home) + new Vector3(5, 0, 0)); }
        }
        else
        {
            panel.AnchorLeft = panel.AnchorRight = 0.5f;
            panel.OffsetLeft = -340; panel.OffsetRight = 340;
            panel.OffsetTop = 90; panel.OffsetBottom = -90;
        }
    }
    private bool _resultFocused;

    /// <summary>結果の札の四角（画面の座標）と、暗幕の濃さ（3D のレイアウトの検査が読む）。</summary>
    internal (Rect2 Card, float Dim) ResultCardRect()
    {
        if (_resultOverlay is not ColorRect dim || !dim.Visible || dim.GetChild(0) is not Control panel) return (default, 0);
        Rect2 r = panel.GetGlobalRect();
        return (new Rect2(r.Position * _uiScale, r.Size * _uiScale), dim.Color.A);
    }

    private void ShowLeanResult()
    {
        var lines = new List<string>();
        if (St.Portal.On && St.Captured) lines.Add($"制圧　{St.CapturedTurn + 1} 刻目" + (St.Stage.TurnCap > 0 && St.Stage.TimeLimit ? $"（期限 {St.Stage.TurnCap}）" : ""));
        else lines.Add($"{Map11Session.ShownTurn} 刻目");
        // 第480期: 撤退の局面の戦果（生還した駒の数・殿を務めた隊・勇者を退かせたか）。
        bool hunter = HunterBoard && St.Rout;
        if (hunter)
        {
            lines.Clear();
            lines.Add($"{Map11Session.ShownTurn} 刻目");
            lines.Add($"生還した駒　{St.HunterSurvivors} ／ {St.HunterStartUnits}");
            lines.Add("殿　" + (St.RearGuards.Count == 0 ? "なし" : string.Join("・", St.RearGuards.OrderBy(i => i).Select(i => St.Squads[i].Label))));
            if (St.HunterDriven) lines.Add(St.HunterNarrow ? "勇者は退いた（外へ出した隊は戻らなかった）" : "勇者を退かせた");
        }
        if (St.Stage.Gold.On) lines.Add($"金　{St.Gold}");
        _resultBody.Text = _resultOpen ? _resultFull : string.Join(Nl, lines);
        _resultMore!.Text = _resultOpen ? "詳細を閉じる" : "詳細";
        // 第481期 §4-1: 暗幕をやめ、画面の右 1／3 の札にする（盤は見えたまま・視点は味方の拠点へ）。「詳細」は同じ札の中で開く。
        ResultCard3D(side: true);
        // 失った駒の顔（詳細を開いたら、終わりの隊の並びに戻す）。
        if (_resultOpen) { RefreshResultFaces(); return; }
        foreach (Node child in _resultFaces.GetChildren()) { _resultFaces.RemoveChild(child); child.QueueFree(); }
        {
            // 生還した駒の顔（明るく）。第481期: 狩る者の盤だけでなく、どのステージの札にも出す（狩る者の盤は拠点へ戻った隊だけ）。
            var back = St.LivingSquads.Where(q => !hunter || St.HunterDriven || q.Pos == St.Graph.Home).SelectMany(q => q.Units!.Where(u => u.IsAlive).OrderBy(u => u.Slot)).Select(u => u.Def).ToList();
            _resultFaces.AddChild(UiKit.Text(back.Count == 0 ? "生還した駒　なし" : $"生還した駒　{back.Count}", 15, UiKit.Muted));
            if (back.Count > 0)
            {
                var row = new HFlowContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddThemeConstantOverride("h_separation", 4);
                foreach (UnitDef d in back)
                {
                    var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(52, 52), TooltipText = d.Name };
                    face.SetUnit(d);
                    row.AddChild(face);
                }
                _resultFaces.AddChild(row);
            }
        }
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
