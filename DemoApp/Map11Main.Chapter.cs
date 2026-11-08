using BattleCore;
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
        foreach (Node child in _resultFaces.GetChildren()) { _resultFaces.RemoveChild(child); child.QueueFree(); }
        foreach (var record in Chapter1.Records)
        {
            var panel = Panel();
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 18);
            panel.AddChild(row);
            var art = new MapStageIllustration { State = record.Stage, CustomMinimumSize = new Vector2(170, 80) };
            row.AddChild(art);
            var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(words);
            words.AddChild(UiKit.Text($"{(record.Won ? "✓" : record.Quit ? "×" : "○")}  {record.Stage.Id}  {record.Result}", 20, record.Won ? PlayerColor : UiKit.Muted));
            string symbol = record.Hero.StartsWith("倒した") ? "⚔ " : record.Hero.StartsWith("逃げ切った") ? "➜ " : "";
            Label hero = UiKit.Text(symbol + record.Hero, 16, GoldColor);
            hero.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            words.AddChild(hero);
            words.AddChild(UiKit.Text($"＋{record.Earned}　／　−{record.Spent}　　やり直し {record.Retries}", 13, UiKit.Muted));
            _resultFaces.AddChild(panel);
        }
        var lines = Chapter1.ResultLines().Where(line => line.StartsWith("合計")).ToList();
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
    /// 絵の口 `--map-chapter-play=N`（第460期）: 1 章を seed N で始め、器具の方針（1-1 ／ 1-2 は分担＋戻り番・1-3 は逃げる）で
    /// 1-3 に勝つまで頭なしで回す（負ければやり直す）。戦闘は画面と同じ口（`Map11Session.BeginBattle` ／ `CompleteBattle`）——戦闘の記録と働いた札が結果に載る。
    /// 終わると 1-3 の勝った通しが `Map11Session.State` に残る（場面はそれを描き、結果画面は「1 章の結果へ」）。
    /// </summary>
    private static void PlayChapterForCapture(int seed)
    {
        Map11Session.PickChapter(seed);
        Map12ChangeSmoke.FightHook = (st, squad, node, intercept) =>
        {
            bool begun = intercept ? Map11Session.BeginIntercept(squad, node) : Map11Session.BeginBattle(squad, node);
            if (!begun) return;
            BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: true);
            CampaignSession.ClearCarriedBattle();
            Map11Session.CompleteBattle(r.PlayerWon, r);
        };
        try
        {
            for (int guard = 0; guard < 60 && Chapter1.Active && !Chapter1.Over; guard++)
            {
                Chapter1.AutoInvest();   // 第467期: 器具の選び方で投資（手持ちにいちばん多い軸）
                Map11State st = Map11Session.State!;
                bool last = Chapter1.Index == Chapter1.Stages.Count - 1;
                Map12ChangeSmoke.Greedy(st.Seed, st.Stage, guard: true, split: true, state: st, siegeRest: true,
                    heroPlan: last ? Map12ChangeSmoke.HeroPlan.Flee : Map12ChangeSmoke.HeroPlan.None);
                Chapter1.Settle(st, Map11Session.RelicWorked, Map11Session.BattleLog);
                if (!st.Won) Chapter1.Retry();
                else if (last) break;
                else Chapter1.Next();
            }
        }
        finally { Map12ChangeSmoke.FightHook = null; }
        Chapter1.IntroPending = false;
    }

    /// <summary>
    /// ステージの頭の札（第460期 §A-5・<b>口だけ</b>——見出し ＋ 2〜3 行の仮の文。本文と一枚の絵は後の期）。1 章のステージを始めた最初の 1 度だけ出す。
    /// </summary>
    private void ShowChapterIntro()
    {
        if (!Chapter1.Active || !Chapter1.IntroPending || St.Finished) return;
        Chapter1.IntroPending = false;
        var (title, body) = Chapter1.Intro(St.Stage);
        _introOverlay = Modal(_uiRoot, 330, 245, out VBoxContainer col, "1 章", GoldColor);
        col.AddChild(new MapStageIllustration { State = St.Stage, CustomMinimumSize = new Vector2(0, 160), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        col.AddChild(UiKit.Text(title, 24, Colors.White));
        Label text = UiKit.Text(body, 15, Color.FromHtml("#ccd5cb"));
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(text);
        Button go = UiKit.ActionButton("始める", GoldColor);
        go.CustomMinimumSize = new Vector2(0, 40);
        go.Pressed += () => { if (_introOverlay is not null) _introOverlay.Visible = false; ShowInvestPicker(); };   // 第467期: 続けて投資の札
        col.AddChild(go);
        // 第468期 §2-3: 1 章を 1-1 から始めた最初の窓に「写しから始める」（写しがあるときだけ・押すと写しの一覧・判定は持たない）。
        _snapButton = null;
        if (Chapter1.Index == 0 && Chapter1.Attempt == 0 && Chapter1.From is null && PlayLog.Starts().Count > 0)
        {
            _snapButton = UiKit.ActionButton($"写しから始める（{PlayLog.Starts().Count} 本）");
            _snapButton.CustomMinimumSize = new Vector2(0, 34);
            _snapButton.Pressed += () => { if (_introOverlay is not null) _introOverlay.Visible = false; OpenSnapPicker(fromIntro: true); };
            col.AddChild(_snapButton);
        }
        _introOverlay.Visible = true;
    }

    /// <summary>頭の札の「写しから始める」（第468期・絵の口 `snap-button` が押して確かめる）。</summary>
    private Button? _snapButton;
}
