using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Map11Main
{
    // 操作の順番だけを案内する。駒・席・道の優劣は付けず、表示順の最初を指す。
    // 進行や配置の可否は既存の State とボタンから読み、ここでは書き換えない。
    private readonly List<Button> _guideBenchButtons = new();
    private MapActionGuide _guide = null!;
    private bool _guideSquadChosen, _guideDestinationChosen;
    private int? _guideEventCell;
    private string _guideKey = "";
    private int _guideRevealFrames;

    private void BuildGuide(Control root)
    {
        _guide = new MapActionGuide();
        root.AddChild(_guide);
        _guide.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    private bool GuideCovered => St.Finished || Map11Session.TurnRunning
        || _encounterOverlay.Visible || _interceptOverlay.Visible || _resultOverlay.Visible
        || _confirmOverlay.Visible || Map11Clock.Running || _running;

    private void RefreshGuide()
    {
        if (_guide is null) return;
        _guide.ShowReach(ReachButtons());
        if (GuideCovered) { PointGuide(null, "", ""); return; }
        if (_reform && St.FreePicks > 0)
        {
            Button? offer = _freeOfferButtons.FirstOrDefault(b => !b.Disabled);
            PointGuide(offer, "無料の1枚を選ぶ", "free-offer");
            return;
        }
        if (_guideEventCell is { } cell && _toastClock > 0)
        {
            // 出来事の文は既存のトーストだけ。場所の枠に同じ文を重ねない。
            Button? place = _destButtons.FirstOrDefault(d => d.Dest.Id == cell && !d.Node.Disabled)?.Node;
            PointGuide(place, "", $"event:{cell}");
            return;
        }

        if (_reform)
        {
            if (!St.CanReform(_selected))
            {
                PointSquad(s => St.CanReform(s), "拠点の隊を選ぶ");
                return;
            }
            var seats = SeatsOf(_selected);
            int empty = FormationRules.PlayableSlots.FirstOrDefault(s => !seats.ContainsKey(s), -1);
            if (Holding)
            {
                int slot = empty >= 0 ? empty : FormationRules.PlayableSlots.FirstOrDefault(
                    s => _holdSquad != _selected || _holdSlot != s, -1);
                PointGuide(_squadDiagram.CardAt(slot), empty >= 0 ? "席を選ぶ" : "入れ替える席を選ぶ",
                    $"seat:{_selected}:{slot}");
                return;
            }
            if (empty >= 0 && _guideBenchButtons.Count > 0)
            {
                PointGuide(_guideBenchButtons[0], seats.Count == 0 ? "駒を選ぶ" : "まだ置ける（このまま出てもよい）",
                    $"bench:{_selected}");
                return;
            }
            // 満席なら出撃へ。別の隊へ配る操作も今までどおり自由にできる。
            if (St.Squads.Any(s => St.CanSend(s.Index)))
                PointGuide(_reformButton, "", "deploy");
            else PointGuide(null, "", "");
            return;
        }

        if (!_guideSquadChosen)
        {
            PointSquad(s => St.CanSend(s), "隊を選ぶ");
            return;
        }
        if (!St.CanSend(_selected))
        {
            if (St.CanReform(_selected)) PointGuide(_reformButton, "駒を配置する", "reform");
            else PointSquad(s => St.CanSend(s), "隊を選ぶ");
            return;
        }
        if (!_guideDestinationChosen)
        {
            DestButton? dest = _destButtons.FirstOrDefault(d => !d.Node.Disabled && !St.AtDest(_selected, d.Dest));
            if (dest is not null)
            {
                PointGuide(dest.Node, "行き先を選ぶ", $"dest:{_selected}:{dest.Dest.Id}");
                return;
            }
        }
        PointGuide(_run, "", "run");
    }

    /// <summary>
    /// 第442期 §0-5 —— 隊を選んでいるあいだ、<b>行けるマス全部</b>を淡く光らせる（横の辺の先も）。
    /// 行けるかどうかは行き先の札が押せるか（`Disabled`・判定は `Map11State` の `CanSend` ／ `CanHead`）だけを読む。
    /// </summary>
    private IReadOnlyList<Button> ReachButtons()
    {
        if (GuideCovered || _reform || !_guideSquadChosen || _selected < 0 || _selected >= St.Squads.Length
            || !St.CanSend(_selected)) return Array.Empty<Button>();
        return _destButtons.Where(d => !d.Node.Disabled && !St.AtDest(_selected, d.Dest)).Select(d => d.Node).ToList();
    }

    private void PointSquad(Func<int, bool> eligible, string text)
    {
        int index = Enumerable.Range(0, St.Squads.Length).FirstOrDefault(eligible, -1);
        if (index < 0 && !_reform && St.CanReform(_selected))
        {
            index = _selected;
        }
        PointGuide(index >= 0 ? _squadButtons[index] : null, text, $"squad:{index}");
    }

    private void PointGuide(Button? target, string text, string key)
    {
        if (target is null || target.Disabled || !target.IsVisibleInTree())
        { target = null; key = ""; }
        _guide.PointTo(target, text);
        if (_guideKey == key) return;
        _guideKey = key;
        _guideRevealFrames = target is null ? 0 : 3;
    }

    private void RevealGuide()
    {
        // Container の配置が確定してから、内側→外側のスクロールを合わせる。
        // 案内先が変わったときだけ。手でスクロールして読む操作を奪わない。
        if (_guide.Target is not { } target
            || !GodotObject.IsInstanceValid(target)) return;
        Control reveal = target.GetParent() is SeatDiagram diagram ? diagram : target;
        for (Node? parent = target.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(reveal);
    }

    private void ProcessGuide(double delta)
    {
        RefreshGuide();
        // シーンを抜けたあとに await が再開しないよう、表示中のフレーム内で完結させる。
        if (_guideRevealFrames > 0 && --_guideRevealFrames == 0) RevealGuide();
        _guide.Tick(delta);
    }

    private void GuideEvent(Watch before)
    {
        // StopReason と同じ出来事のデータを読む。表示文から場所を逆算しない。
        if (St.ArrivedCount > before.Delivered) _guideEventCell = St.Graph.Home;
        else if (before.ReserveDepart < 0 && St.ReserveDepartTurn >= 0 && St.Reserve is { } reserve)
            _guideEventCell = reserve.Pos;
        else if (!before.Captured && St.Captured || St.SpawnCount > before.Spawns)
            _guideEventCell = St.Graph.Portal;
        else
        {
            for (int i = 0; i < St.Squads.Length; i++)
                if (!before.Arrived[i] && AtDest(i) && !St.Squads[i].Lost)
                { _guideEventCell = St.Squads[i].Pos; return; }
            for (int r = 0; r < St.Stage.LaneCount; r++)
                if (!before.Imminent[r] && St.NextNode(r) is { } n && n.Cell <= 0)
                { _guideEventCell = n.Pos; return; }
        }
    }
}

/// <summary>入力を遮らず、1つのボタンの枠と短い案内だけを重ねる。</summary>
public partial class MapActionGuide : Control
{
    public Button? Target { get; private set; }
    /// <summary>淡く光らせる札（第442期・選んだ隊の行けるマス）。<see cref="Target"/> と重なってもよい。</summary>
    public IReadOnlyList<Button> Reach { get; private set; } = Array.Empty<Button>();
    public string Message => _caption.Text;
    private readonly Label _caption = new()
    {
        MouseFilter = MouseFilterEnum.Ignore,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly StyleBoxFlat _frame = new()
    {
        BgColor = new Color(1f, 0.79f, 0.36f, 0.08f),
        BorderColor = Color.FromHtml("#ffda83"),
        BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        ShadowColor = new Color(1f, 0.75f, 0.3f, 0.25f), ShadowSize = 9,
    };
    private readonly StyleBoxFlat _captionBack = new()
    {
        BgColor = Color.FromHtml("#25271f"),
        BorderColor = Color.FromHtml("#ffda83"), BorderWidthLeft = 3,
        ContentMarginLeft = 10, ContentMarginRight = 10,
        ContentMarginTop = 6, ContentMarginBottom = 6,
    };
    private readonly StyleBoxFlat _reachFrame = new()
    {
        BgColor = new Color(1f, 0.79f, 0.36f, 0.16f),
        BorderColor = new Color(1f, 0.85f, 0.51f, 0.9f),
        BorderWidthLeft = 2, BorderWidthTop = 2, BorderWidthRight = 2, BorderWidthBottom = 2,
        CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
    };
    private float _time;
    private string _message = "";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _caption.AddThemeFontSizeOverride("font_size", 15);
        _caption.AddThemeColorOverride("font_color", Color.FromHtml("#ffda83"));
        _caption.AddThemeStyleboxOverride("normal", _captionBack);
        AddChild(_caption);
    }

    public void PointTo(Button? target, string message)
    {
        Target = target;
        _message = message;
        _caption.Text = message.Length > 0 ? "◀  " + message : "";
    }

    public void ShowReach(IReadOnlyList<Button> buttons) => Reach = buttons;

    /// <summary>枠を描ける札の画面上の四角（消えた・押せない・見えない・スクロールの外なら null）。</summary>
    private Rect2? Drawable(Button target)
    {
        if (!GodotObject.IsInstanceValid(target) || target.IsQueuedForDeletion() || target.Disabled
            || !target.IsVisibleInTree()) return null;
        Rect2 rect = target.GetGlobalRect();
        // 読むために手で流した場合、画面外の札の枠だけが浮かないようにする。
        for (Node? parent = target.GetParent(); parent is Control control; parent = parent.GetParent())
            if (control.ClipContents && !control.GetGlobalRect().Encloses(rect)) return null;
        rect.Position -= GlobalPosition;
        return rect;
    }

    public void Tick(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        _caption.Visible = false;
        foreach (Button b in Reach)
            if (!ReferenceEquals(b, Target) && Drawable(b) is { } r) DrawStyleBox(_reachFrame, r.Grow(2));
        if (Target is not { } target || Drawable(target) is not { } rect) return;
        _frame.ShadowColor = new Color(1f, 0.75f, 0.3f, 0.20f + 0.10f * Mathf.Sin(_time * 3f));
        DrawStyleBox(_frame, rect.Grow(3));
        if (_caption.Text.Length == 0) return;
        _caption.Visible = true;
        _caption.Size = _caption.GetMinimumSize();
        Vector2 viewport = GetViewportRect().Size;
        float x = rect.End.X + 14;
        // 配置図の別の席を案内文で隠さない。スクロールの中ならパネルの外へ出す。
        for (Node? parent = target.GetParent(); parent is Control control; parent = parent.GetParent())
            if (control is ScrollContainer)
                x = Math.Max(x, control.GetGlobalRect().End.X - GlobalPosition.X + 14);
        float y = rect.GetCenter().Y - _caption.Size.Y / 2;
        if (target.GetParent() is SeatDiagram diagram && diagram.DisplayScale > 1.1f)
        {
            bool lastRow = target.Position.Y + target.Size.Y >= diagram.Size.Y - 1;
            _caption.Text = (lastRow ? "▼  " : "▲  ") + _message;
            x = rect.Position.X;
            y = lastRow ? rect.Position.Y - _caption.Size.Y - 6 : rect.End.Y + 6;
        }
        else if (rect.Position.Y > viewport.Y - 100)
        {
            _caption.Text = "▼  " + _message;
            x = rect.Position.X;
            y = rect.Position.Y - _caption.Size.Y - 10;
        }
        if (x + _caption.Size.X > viewport.X - 12)
        { x = rect.Position.X - _caption.Size.X - 14; }
        _caption.Position = new Vector2(Math.Clamp(x, 12, Math.Max(12, viewport.X - _caption.Size.X - 12)),
            Math.Clamp(y, 12, Math.Max(12, viewport.Y - _caption.Size.Y - 12)));
    }
}
