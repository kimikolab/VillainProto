using Godot;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 出来事の窓（第478期 §4・3D の層・中間拠点ごとの見返りを持つ盤だけ）。**画面は判定を1つも持たない**——
// 「取ったか」「組み直せるか」「呼び戻せるか」「強化できるか」は `Map11State` に聞き、ここは窓を出すだけ。
//
//   1. 負けた戦闘の後: 「誰が何ターン目に倒れたか」（戦闘の記録の添え書きを読むだけ・結果の窓まで待たない）
//   2. 中間拠点を取った: 見返りと、金の使いどころ（呼び戻す ／ 強化 ／ 休む）のボタン
//   3. 傷んだ隊・金を使える隊が拠点か取った中間拠点で止まった: 同じボタン
//
// 作戦ターンが流れておらず、ほかの窓が 1 つも出ていないときにだけ出す（`ProcessNotices3D` が毎フレーム見る）。
// 見たものの覚え（static）は場面を読み直しても残す——戦闘から戻るたびに同じ窓を出さないため。盤（`Map11State`）が替われば捨てる。
// =====================================================================================

public partial class Map11Main
{
    private Control? _noticeOverlay;
    private Label _noticeTag = null!, _noticeTitle = null!, _noticeBody = null!;
    private Button _noticeRevive = null!, _noticeEnhance = null!, _noticeRest = null!, _noticeClose = null!;
    private int _noticeSquad = -1;

    private static Map11State? _noticeState;
    private static int _noticeCaptures, _noticeLosses;
    private static int[] _noticePos = System.Array.Empty<int>();
    private static int _noticeSpent, _noticeSpentTurn;
    /// <summary>出来事の窓を出さない（レイアウトの検査・絵の口——器具が盤を進める通し）。</summary>
    private static bool _noticesOff;

    /// <summary>出来事の窓が出ているか（遊ぶ口・検査が読む）。</summary>
    internal bool NoticeShown => _noticeOverlay is { Visible: true };

    private void BuildNoticeOverlay(Control root)
    {
        _noticeOverlay = Modal(root, 380, 165, out VBoxContainer col, "OUTPOST", GoldColor);
        _noticeTag = (Label)col.GetChild(0);
        _noticeTitle = UiKit.Text("", 24, Colors.White);
        _noticeTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_noticeTitle);
        _noticeBody = UiKit.Text("", 16, Color.FromHtml("#dfe6dc"));
        _noticeBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _noticeBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_noticeBody);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);
        Button Add(string text, Color color, System.Action act)
        {
            Button b = UiKit.ActionButton(text, color);
            b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            b.CustomMinimumSize = new Vector2(0, 56);
            b.Pressed += act;
            row.AddChild(b);
            return b;
        }
        _noticeRevive = Add("呼び戻す", GoldColor, () => NoticeOpenReform());
        _noticeEnhance = Add("強化", GoldColor, () => NoticeOpenReform());
        _noticeRest = Add("休む", PlayerColor, NoticeRest);
        _noticeClose = Add("閉じる", UiKit.Muted, () => { _noticeOverlay!.Visible = false; Refresh(); });
    }

    /// <summary>今の編成の窓（呼び戻す・強化のある窓）を、その隊で開く。</summary>
    private void NoticeOpenReform()
    {
        _noticeOverlay!.Visible = false;
        if (_noticeSquad >= 0) { _selected = _noticeSquad; _guideSquadChosen = true; }
        if (!_reform) ToggleReform(); else Refresh();
    }

    /// <summary>その隊の行き先を今いる場所にする（傷んでいれば次の刻から休む——休むかどうかは `Map11Orders.Plan` が決める）。</summary>
    private void NoticeRest()
    {
        _noticeOverlay!.Visible = false;
        if (_noticeSquad >= 0)
        {
            Map11State.Squad s = St.Squads[_noticeSquad];
            Map11Session.SetDestination(_noticeSquad, s.Pos == St.Graph.Home ? Dest.Home : Dest.Of(St.Graph, s.Pos));
            ShowToast($"{s.Label} は {PlaceName(s.Pos)} で休む（刻を進めると回復する）", GoldColor, 4.0f);
        }
        Refresh();
    }

    /// <summary>金の札を光らせるか（金が 20 以上・使える場所に隊がいる・2 刻使っていない）。見たものの覚えから読むだけ。</summary>
    private bool GoldIdle3D()
    {
        if (St.Stage.OutpostRewards is null || !St.Stage.Gold.On || St.Finished || !ReferenceEquals(_noticeState, St)) return false;
        bool place = St.Squads.Any(q => !q.Lost && q.Units is { Count: > 0 } && St.CanReform(q.Index));
        return St.Gold >= 20 && place && St.Turn - _noticeSpentTurn >= 2;
    }

    /// <summary>毎フレーム: 出すべき出来事の窓があれば 1 つ出す（3D の層・新しい 1-2 の盤だけ）。</summary>
    private void ProcessNotices3D(bool modal)
    {
        if (_noticeOverlay is null || St.Stage.OutpostRewards is null || _noticesOff) return;
        int spent = -St.GoldLog.Where(e => e.Amount < 0).Sum(e => e.Amount);
        if (!ReferenceEquals(_noticeState, St))
        {
            // 盤が替わった（ステージの始め・やり直し）: 今の姿を「もう見た」ことにする。
            _noticeState = St;
            _noticeCaptures = St.OutpostCaptures;
            _noticeLosses = Map11Session.BattleLog.Count;
            _noticePos = St.Squads.Select(q => q.Pos).ToArray();
            _noticeSpent = spent;
            _noticeSpentTurn = St.Turn;
            return;
        }
        if (spent != _noticeSpent) { _noticeSpent = spent; _noticeSpentTurn = St.Turn; }
        if (modal || _noticeOverlay.Visible || _reform || St.Finished || Map11Session.TurnRunning || _running || St.RelicOffer.Count > 0) return;

        // ---- 1. 負けた戦闘 ----
        if (Map11Session.BattleLog.Count > _noticeLosses)
        {
            var fresh = Map11Session.BattleLog.Skip(_noticeLosses).Where(l => !l.Won).ToList();
            _noticeLosses = Map11Session.BattleLog.Count;
            if (fresh.Count > 0)
            {
                Map11Session.Line l = fresh[^1];
                string foe = System.Text.RegularExpressions.Regex.Replace(l.Foe, @"^\d+\.\s*", "");
                var body = new List<string>();
                foreach (Map11Session.Line x in fresh)
                {
                    body.Add($"{x.Squad} 対 {System.Text.RegularExpressions.Regex.Replace(x.Foe, @"^\d+\.\s*", "")}（{(x.Intercept ? "拠点の迎撃・" : "")}{x.Road}）: 残り {x.Alive} 枚");
                    body.AddRange(x.Notes);
                }
                if (St.Stage.Gold.Revive > 0 && St.FallenUnits.Count > 0)
                    body.Add($"倒れた駒は、拠点か取った中間拠点にいる隊の編成の窓で呼び戻せる（1 枚 金 {St.Stage.Gold.Revive}・今の金 {St.Gold}）。");
                ShowNotice("LOST ・ 負けた戦闘", EnemyColor, $"{l.Squad} が {foe} に負けた", body, -1);
                return;
            }
        }

        // ---- 2. 中間拠点を取った ／ 3. 使える場所で止まった ----
        bool took = St.OutpostCaptures > _noticeCaptures;
        _noticeCaptures = St.OutpostCaptures;
        if (_noticePos.Length != St.Squads.Length) _noticePos = St.Squads.Select(q => q.Pos).ToArray();
        int squad = -1;
        bool atNew = false;
        for (int i = 0; i < St.Squads.Length; i++)
        {
            Map11State.Squad s = St.Squads[i];
            bool moved = _noticePos[i] != s.Pos;
            _noticePos[i] = s.Pos;
            if (s.Lost || s.Units is not { Count: > 0 } || !St.CanReform(i)) continue;
            bool outpost = St.AtOutpost(i);
            if (took && outpost && (moved || squad < 0)) { squad = i; atNew = true; }
            else if (moved && !atNew && squad < 0 && (outpost || s.Pos == St.Graph.Home) && St.Turn > 0) squad = i;
        }
        if (squad < 0) return;
        Map11State.Squad q = St.Squads[squad];
        bool canRevive = St.Stage.Gold.Revive > 0 && St.FallenUnits.Count > 0 && St.Gold >= St.Stage.Gold.Revive;
        bool canEnhance = St.Stage.Stars.On && St.EnhancePrice > 0 && St.Gold >= St.EnhancePrice;
        bool hurt = St.Hurt(squad);
        if (!atNew && !canRevive && !hurt && !(canEnhance && St.Gold >= 20)) return;
        var lines = new List<string>();
        if (atNew && St.RewardAt(q.Pos) is { } rw) lines.Add($"見返り: {(rw with { Gold = St.OutpostGoldAt(q.Pos) }).Text}。ここでは組み直し・呼ぶ・強化・休むができる。");
        lines.Add($"{q.Label}: {q.Units!.Count(u => u.IsAlive)} 枚 ・ HP {SquadHpPercent(q):F0}%　／　金 {St.Gold}");
        if (St.FallenUnits.Count > 0) lines.Add($"倒れている駒: {string.Join("・", St.FallenUnits.Select(u => u.Def.Name))}");
        if (hurt) lines.Add("傷んでいる——ここで休めば、刻を進めるたびに回復する。");
        ShowNotice(atNew ? "OUTPOST ・ 中間拠点を取った" : "BASE ・ 金を使える場所", GoldColor,
            atNew ? $"{q.Label} が {PlaceName(q.Pos)} を取った" : $"{q.Label} が {PlaceName(q.Pos)} にいる", lines, squad);
    }

    private void ShowNotice(string tag, Color tagColor, string title, List<string> body, int squad)
    {
        _noticeSquad = squad;
        _noticeTag.Text = tag;
        _noticeTag.AddThemeColorOverride("font_color", tagColor);
        _noticeTitle.Text = title;
        _noticeBody.Text = string.Join(Nl, body);
        bool reform = squad >= 0 && St.CanReform(squad);
        _noticeRevive.Visible = reform && St.Stage.Gold.Revive > 0 && St.FallenUnits.Count > 0;
        _noticeRevive.Text = $"呼び戻す（金 {St.Stage.Gold.Revive}）";
        _noticeRevive.Disabled = St.Gold < St.Stage.Gold.Revive;
        _noticeEnhance.Visible = reform && St.Stage.Stars.On && St.EnhancePrice > 0;
        _noticeEnhance.Text = $"強化（金 {St.EnhancePrice}）";
        _noticeEnhance.Disabled = St.Gold < St.EnhancePrice;
        _noticeRest.Visible = squad >= 0 && St.Hurt(squad);
        Map11Clock.Stop();
        _noticeOverlay!.Visible = true;
    }
}
