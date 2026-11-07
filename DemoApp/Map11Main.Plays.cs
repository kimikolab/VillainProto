using Godot;
using System.Linq;

public partial class Map11Main
{
    // =================================================================================
    // 第466期 §2 —— 「1 章（写しから）」の写しの一覧（判定は持たない・`Chapter1.BeginFrom` を呼ぶだけ）
    // =================================================================================

    /// <summary>口 `--map-chapter-from=` を読んだか（起動 1 回につき 1 度だけ・シーンを読み直しても始め直さない）。</summary>
    private static bool _fromArgRead;
    /// <summary>一覧で選ぶ版（偽 ＝ 今の版・既定）。</summary>
    private static bool _pickSnapRule;
    private Control? _snapOverlay;

    /// <summary>
    /// 写しの一覧の窓（編成画面の札で「1 章（写しから）」を選んで作戦マップへ入ったとき）。押した写しのステージから 1 章を始める。
    /// 上の札で「今の版で始める」⇔「写しの版で始める」を替える（既定は今の版——調整した後の盤を前の持ち越しで遊ぶため）。
    /// 閉じると、いま後ろに出ている写し（いちばん新しい写し）のまま遊ぶ。
    /// </summary>
    private void ShowSnapPicker()
    {
        if (!Map11Session.SnapChoicePending) return;
        Map11Session.SnapChoicePending = false;
        var snaps = PlayLog.Starts();
        if (snaps.Count == 0) return;
        _snapOverlay = Modal(_uiRoot, 430, 300, out VBoxContainer col, "1 章（写しから）", GoldColor);
        col.AddChild(UiKit.Text("写しから始める", 22, Colors.White));
        Label help = UiKit.Text("遊びの写し（design/plays）のステージから 1 章を始める。以降は今の 1 章と同じ（次のステージへ持ち越す・やり直しはそのステージの始め）。"
                                + "「入った時点」は前のステージの終わりのまま（組み直しの窓から）、「1 刻目の前」は組み直しの後の隊。", 13, Color.FromHtml("#ccd5cb"));
        help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(help);
        string RuleText() => _pickSnapRule ? "敵の倍率など: 写しに記録された版で始める" : $"敵の倍率など: 今の版で始める（{Chapter1.Default.Name.Split('（')[0]}）";
        Button rule = UiKit.ActionButton(RuleText());
        rule.Pressed += () => { _pickSnapRule = !_pickSnapRule; rule.Text = RuleText(); };
        col.AddChild(rule);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 330), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(list);
        col.AddChild(scroll);
        foreach (PlayLog.Snap snap in snaps)   // 並びは `PlayLog.Starts`（遊んだ写しの新しい順 → 手で写した写し）
        {
            Button b = UiKit.ActionButton(snap.Label);
            b.Alignment = HorizontalAlignment.Left;
            b.TooltipText = snap.Path + (snap.Notes.Count > 0 ? "\n" + string.Join("\n", snap.Notes) : "") + (snap.Estimated.Count > 0 ? "\n推定: " + string.Join("・", snap.Estimated) : "");
            b.Pressed += () =>
            {
                Map11Session.PickChapterFrom(snap, _pickSnapRule);
                GD.Print($"CHAPTER1_FROM {snap.Path} 版 {Chapter1.Rule.Key} ステージ {Chapter1.CurrentStage.Id}");
                GetTree().ReloadCurrentScene();
            };
            list.AddChild(b);
        }
        Button close = UiKit.ActionButton($"閉じる（いま出ている写しのまま: {Chapter1.From?.Label ?? "—"}）");
        close.Pressed += () => { if (_snapOverlay is not null) _snapOverlay.Visible = false; };
        col.AddChild(close);
        _snapOverlay.Visible = true;
    }
}
