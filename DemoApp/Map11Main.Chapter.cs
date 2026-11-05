using Godot;
using System.Linq;

public partial class Map11Main
{
    // =================================================================================
    // 第460期 §A —— 1 章の連なり（判定は持たない・`Chapter1` を呼ぶだけ）
    // =================================================================================

    private Button _resultQuit = null!;
    private Control? _introOverlay;

    /// <summary>結果画面の主ボタン。単発は「もう一度」、1 章は「次へ」「1 章の結果へ」「やり直す」「1 章を最初から」。</summary>
    private void OnResultAgain()
    {
        if (Chapter1.Active)
        {
            if (Chapter1.Over) { Chapter1.Begin(); GetTree().ReloadCurrentScene(); return; }
            if (St.Won)
            {
                if (Chapter1.Next()) { GetTree().ReloadCurrentScene(); return; }
                ShowChapterResult();
                return;
            }
            Chapter1.Retry();
            GetTree().ReloadCurrentScene();
            return;
        }
        Map11Session.Reset(stage: St.Stage);
        GetTree().ReloadCurrentScene();
    }

    /// <summary>負けたステージで「やめる」——1 章の結果へ。</summary>
    private void OnResultQuit()
    {
        Chapter1.Quit();
        ShowChapterResult();
    }

    /// <summary>
    /// ステージの結果画面を 1 章の形にする（`ShowResult` の最後に呼ぶ）。ステージの結果の本文は今のまま、頭に 1 章の何番目かを 1 行足し、ボタンを替える。
    /// 1 章でなければ「もう一度」だけ（第459期まで）。
    /// </summary>
    private void ChapterResultView()
    {
        _resultQuit.Visible = false;
        _replayButton.Text = "もう一度";
        if (!Chapter1.Active) return;
        Chapter1.Settle(St, Map11Session.RelicWorked, Map11Session.BattleLog);
        if (Chapter1.Over) { ShowChapterResult(); return; }
        Chapter1.Record r = Chapter1.Records[Chapter1.Index];
        _resultBody.Text = $"1 章 ・ {St.Stage.Id}（{Chapter1.Index + 1} ／ {Chapter1.Stages.Count}）・ やり直し {r.Retries} 回" + Nl + Nl + _resultBody.Text;
        if (St.Won)
            _replayButton.Text = Chapter1.Index < Chapter1.Stages.Count - 1 ? $"次へ（{Chapter1.Stages[Chapter1.Index + 1].Id}）" : "1 章の結果へ";
        else
        {
            _replayButton.Text = "やり直す（同じステージ・新しい配り）";
            _resultQuit.Visible = true;
        }
    }

    /// <summary>1 章の結果画面（ステージごとに 1 行 ＋ 合計・各ステージの結果画面はそのまま）。</summary>
    private void ShowChapterResult()
    {
        _resultTitle.Text = "1 章 の結果";
        foreach (Node child in _resultFaces.GetChildren()) child.QueueFree();
        var lines = Chapter1.ResultLines();
        lines.Add("");
        lines.Add($"1 章の seed {Chapter1.Seed}（ステージの seed: "
                  + string.Join(" ／ ", Chapter1.Records.Where(r => r.Played).Select(r => $"{r.Stage.Id} {r.Seed}")) + "）");
        _resultBody.Text = string.Join(Nl, lines);
        _replayButton.Text = "1 章を最初から";
        _resultQuit.Visible = false;
        _resultScroll.ScrollVertical = 0;
        _resultOverlay.Visible = true;
        GD.Print($"CHAPTER1_RESULT seed={Chapter1.Seed} " + string.Join(" | ", Chapter1.ResultLines()));
    }

    /// <summary>
    /// ステージの頭の札（第460期 §A-5・<b>口だけ</b>——見出し ＋ 2〜3 行の仮の文。本文と一枚の絵は後の期）。1 章のステージを始めた最初の 1 度だけ出す。
    /// </summary>
    private void ShowChapterIntro()
    {
        if (!Chapter1.Active || !Chapter1.IntroPending || St.Finished) return;
        Chapter1.IntroPending = false;
        var (title, body) = Chapter1.Intro(St.Stage);
        _introOverlay = Modal(_uiRoot, 300, 140, out VBoxContainer col, "1 章", GoldColor);
        col.AddChild(UiKit.Text(title, 24, Colors.White));
        Label text = UiKit.Text(body, 15, Color.FromHtml("#ccd5cb"));
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(text);
        Button go = UiKit.ActionButton("始める", GoldColor);
        go.CustomMinimumSize = new Vector2(0, 40);
        go.Pressed += () => { if (_introOverlay is not null) _introOverlay.Visible = false; };
        col.AddChild(go);
        _introOverlay.Visible = true;
    }
}
