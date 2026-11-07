using BattleCore;
using Godot;
using System.Linq;

public partial class Map11Main
{
    // =================================================================================
    // 第467期 §1-3 —— 軸への投資の札の窓（判定は持たない・`Chapter1.PickInvest` を呼ぶだけ・見た目は仮で Codex）
    // =================================================================================

    private Control? _investOverlay;

    /// <summary>
    /// ステージの始めの投資の札（3 枚から 1 枚・見送りは無い・選ぶまで閉じない）。1 章のステージで、まだ選んでいないときだけ出す。
    /// 頭の札が出ていればその「始める」の後に出る（組み直しの窓より前）。
    /// </summary>
    private void ShowInvestPicker()
    {
        if (!Chapter1.InvestPending || _investOverlay is { Visible: true }) return;
        var cards = Chapter1.InvestCards;
        if (cards.Count == 0) return;
        InvestRule rule = St.Stage.Invest;
        _investOverlay = Modal(_uiRoot, 470, 250, out VBoxContainer col, "投資", GoldColor);
        col.AddChild(UiKit.Text($"{St.Stage.Id} ・ どの軸に投資する？", 22, Colors.White));
        Label help = UiKit.Text($"選んだ軸の駒が、呼ぶ候補に出やすくなる（投資 1 回につき候補 1 枚ごとに {rule.PerPick}%・上限 {rule.Cap}%）。"
                                + "選んだ軸は次のステージへ持ち越し、同じ軸を選べば重なる。金はかからない。", 13, Color.FromHtml("#ccd5cb"));
        help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(help);
        col.AddChild(UiKit.Text(St.InvestText, 13, GoldColor));
        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);
        for (int i = 0; i < cards.Count; i++)
        {
            string axis = cards[i];
            int index = i;
            var card = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            card.AddThemeConstantOverride("separation", 6);
            card.AddChild(UiKit.Text(axis, 22, GoldColor));
            Label line = UiKit.Text(UnitTags.LineOf(axis), 12, Color.FromHtml("#ccd5cb"));
            line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            line.CustomMinimumSize = new Vector2(0, 34);
            card.AddChild(line);
            // その軸の駒の顔 3〜4 体（持っていない駒を先に）。
            var faces = new HBoxContainer();
            faces.AddThemeConstantOverride("separation", 2);
            foreach (UnitDef d in InvestRule.PiecesOf(axis).OrderBy(d => St.OnBoardOrHand(d)).Take(4))
            {
                var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(52, 52), TooltipText = d.Name + (St.OnBoardOrHand(d) ? "（持っている）" : "") };
                face.SetUnit(d);
                faces.AddChild(face);
            }
            card.AddChild(faces);
            int have = St.OwnedAxisCount(axis), all = InvestRule.PiecesOf(axis).Count;
            card.AddChild(UiKit.Text($"手持ち {have} ／ {all} 体・投資 ×{St.InvestCount(axis)}", 12, UiKit.Muted));
            card.AddChild(UiKit.Text("この軸の駒が呼ぶ候補に出やすくなる", 12, UiKit.Muted));
            Button pick = UiKit.ActionButton($"{axis}に投資する", GoldColor);
            pick.CustomMinimumSize = new Vector2(0, 38);
            pick.Pressed += () => OnInvestPicked(index);
            card.AddChild(pick);
            row.AddChild(card);
        }
        _investOverlay.Visible = true;
    }

    /// <summary>投資の札を選んだ（`Chapter1.PickInvest` が候補を引き直す）。</summary>
    private void OnInvestPicked(int index)
    {
        var cards = Chapter1.InvestCards;
        if (!Chapter1.PickInvest(index)) { ShowToast("いまは選べません", EnemyColor, 3.0f); return; }
        if (_investOverlay is not null) _investOverlay.Visible = false;
        ShowToast($"{cards[index]}に投資した（{St.InvestText}）", GoldColor, 3.5f);
        GD.Print($"CHAPTER1_INVEST stage={St.Stage.Id} pick={cards[index]} cards={string.Join("/", cards)} {St.InvestText}");
        Refresh();
    }
}
