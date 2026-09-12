using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 画面下の固定の一覧（第125期 段3-e）。<b><c>BattleCore</c> を1行も触らない。</b>
///
/// <para>第124期の観察でポンが出した「HPゲージ下の攻撃力が目立たない」「戦闘中に配置移動も
/// 行われるので視認が難しい」（3-d）への答えが「スタレ風に画面下に味方のHPゲージやステータスを
/// 表示する方式も検討したい」（3-e）だった。<b>盤面の上の札は駒と一緒に動く</b>ので、
/// 逃亡・後退・突き飛ばし・かき回しが走るたびに読む位置が変わる——
/// <b>ここは席が変わっても動かない</b>ことが唯一の仕事である。</para>
///
/// <para><b>盤面上のゲージは消していない</b>（指示書 §6 の 3-e: 「両方出して構わない」）。
/// あちらは「その駒が」いま何を受けたかを見るためのもので、こちらは「5体の今」を一目で見るもの。</para>
///
/// <para><b>数字の出どころは <see cref="BattlePawn3D"/> だけ</b>——台本から数え直すと
/// 同じ言葉の表が2つできる（第124期 §4 と同じ判断）。並びは<b>開幕の席の順で固定</b>し、
/// 移動しても入れ替えない（入れ替えると「動かない」という唯一の取り柄が消える）。</para>
/// </summary>
public partial class PartyBar : PanelContainer
{
    private sealed class Row
    {
        public required int InstanceId { get; init; }
        public required Label Name { get; init; }
        public required Label Seat { get; init; }
        public required Label Hp { get; init; }
        public required ColorRect Fill { get; init; }
        public required Control Track { get; init; }
        public required Label Atk { get; init; }
        public required Label Status { get; init; }
    }

    private readonly List<Row> _rows = new();
    private HBoxContainer _body = null!;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.02f, 0.045f, 0.04f, 0.88f), UiKit.Line, 1, 8));

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        foreach (string side in new[] { "margin_left", "margin_right" }) margin.AddThemeConstantOverride(side, 12);
        foreach (string side in new[] { "margin_top", "margin_bottom" }) margin.AddThemeConstantOverride(side, 8);
        AddChild(margin);

        _body = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _body.AddThemeConstantOverride("separation", 10);
        margin.AddChild(_body);
    }

    /// <summary>開幕の味方だけを並べる（第125期 段3-e）。<b>席の順で固定</b>し、以後は並べ替えない。</summary>
    public void Begin(IReadOnlyList<DemoOpening> openings)
    {
        foreach (Node child in _body.GetChildren()) child.QueueFree();
        _rows.Clear();

        foreach (DemoOpening o in openings
                     .Where(x => x.Team == BattleContext.PlayerTeam)
                     .OrderBy(x => x.Slot))
        {
            var cell = new VBoxContainer { CustomMinimumSize = new Vector2(168, 0), MouseFilter = MouseFilterEnum.Ignore };
            cell.AddThemeConstantOverride("separation", 2);

            var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            head.AddThemeConstantOverride("separation", 6);
            Label seat = UiKit.Text(UiKit.SeatLabel(o.Slot), 10, UiKit.Faint);
            Label name = UiKit.Text(o.Name, 12, UiKit.Ink);
            head.AddChild(seat);
            head.AddChild(name);
            cell.AddChild(head);

            var track = new Control { CustomMinimumSize = new Vector2(168, 9), MouseFilter = MouseFilterEnum.Ignore };
            var back = new ColorRect { Color = new Color(0.10f, 0.15f, 0.13f, 0.95f), MouseFilter = MouseFilterEnum.Ignore };
            back.SetAnchorsPreset(LayoutPreset.FullRect);
            track.AddChild(back);
            var fill = new ColorRect { Color = UiKit.Heal, MouseFilter = MouseFilterEnum.Ignore };
            fill.SetAnchorsPreset(LayoutPreset.LeftWide);
            fill.Size = new Vector2(168, 9);
            track.AddChild(fill);
            cell.AddChild(track);

            var foot = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            foot.AddThemeConstantOverride("separation", 8);
            Label hp = UiKit.Text("", 11, UiKit.Muted);
            // 攻撃力は**見出しと同じ大きさ**で出す（3-d: 「HPゲージ下の攻撃力が目立たない」）。
            Label atk = UiKit.Text("", 12, UiKit.Gold);
            foot.AddChild(hp);
            foot.AddChild(atk);
            cell.AddChild(foot);

            Label status = UiKit.Text("", 10, UiKit.Violet);
            cell.AddChild(status);

            _body.AddChild(cell);
            _rows.Add(new Row
            {
                InstanceId = o.InstanceId,
                Name = name,
                Seat = seat,
                Hp = hp,
                Fill = fill,
                Track = track,
                Atk = atk,
                Status = status,
            });
        }
        Sync(null, -1);
    }

    /// <summary>
    /// 盤面の駒から引き直す（第125期 段3-e）。<b>台本からは数え直さない。</b>
    /// <paramref name="turnOwner"/> は手番の主の <c>InstanceId</c>（-1 なら誰の手番でもない時間）。
    /// </summary>
    public void Sync(BattlefieldView3D? field, int turnOwner)
    {
        foreach (Row row in _rows)
        {
            BattlePawn3D? pawn = field?.FindPawn(row.InstanceId);
            if (pawn is null) continue;

            float ratio = pawn.MaxHp <= 0 ? 0 : Mathf.Clamp(pawn.Hp / (float)pawn.MaxHp, 0f, 1f);
            // 最初の1回はレイアウトがまだ走っていないので `Size` が 0 になる。
            // そのときは最小サイズで引く（次の同期で実寸に直る）。
            float width = row.Track.Size.X > 1 ? row.Track.Size.X : row.Track.CustomMinimumSize.X;
            float height = row.Track.Size.Y > 1 ? row.Track.Size.Y : row.Track.CustomMinimumSize.Y;
            row.Fill.Size = new Vector2(width * ratio, height);
            row.Fill.Color = pawn.Hp <= 0 ? UiKit.Faint
                           : ratio <= 0.3f ? UiKit.Hurt
                           : ratio <= 0.6f ? UiKit.Gold
                           : UiKit.Heal;
            row.Hp.Text = $"HP {pawn.Hp}/{pawn.MaxHp}";
            row.Atk.Text = $"攻 {pawn.AttackValue} {UiKit.PatternLabel(pawn.Pattern)}";
            row.Seat.Text = UiKit.SeatLabel(pawn.Slot);
            row.Status.Text = pawn.StatusText;

            bool owner = pawn.InstanceId == turnOwner && pawn.Hp > 0;
            row.Name.AddThemeColorOverride("font_color", pawn.Hp <= 0 ? UiKit.Faint : owner ? UiKit.Gold : UiKit.Ink);
            row.Seat.AddThemeColorOverride("font_color", owner ? UiKit.Gold : UiKit.Faint);
        }
    }
}
