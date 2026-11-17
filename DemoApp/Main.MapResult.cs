using BattleCore;
using Godot;
using System.Linq;

// =====================================================================================
// 作戦マップ（3D）から来た戦闘の、終わりの札（第479期・指示書 design/PHASE479_LESS_WINDOWS_SPEC.md §2）。
// 出すのは 画面を横切る「勝利」「敗北」の帯だけで、1.5 秒で作戦マップへ戻る（第479期の続き 2・中央の箱と「詳細」はやめた）。倒れた駒・残りの HP・賞金は、戻った作戦マップの遭遇の台の上に出る（`Map11Main.Clash.cs`）。戦績の表は「詳細」の奥。
// **判定は 1 つも持たない**——勝敗と倒れた駒は `BattleResult`、残りの枚数と HP は作戦マップの戦闘の記録、賞金は金の増えた分を読むだけ。
// 再生の速さは次の戦闘へ持ち越す（Codex の試遊 2026-11-16: 戦闘ごとに ×2 に戻っていた）。
// =====================================================================================
public partial class Main
{
    /// <summary>選んだ再生の速さ（0 ＝ ×0.5 ／ 1 ＝ ×1 ／ 2 ＝ ×2 ／ 3 ＝ ×4）。場面を作り直しても残す。</summary>
    private static int _keptSpeed = 2;
    private static double SpeedOf(int index) => index switch { 0 => 0.5, 2 => 2.0, 3 => 4.0, _ => 1.0 };

    private CanvasLayer? _mapResultLayer;

    /// <summary>検証の欄（編成プリセット・敵ウェーブ・seed・ACTUAL CORE）。作戦マップ（3D）から来た戦闘では隠す。</summary>
    private readonly System.Collections.Generic.List<Control> _verifyControls = new();

    /// <summary>戦闘の終わりの帯を出しておく秒数（過ぎたら作戦マップへ戻る・押せばすぐ戻る）。</summary>
    internal const double MapEndSeconds = 1.5;

    /// <summary>作戦マップ（3D）から来た戦闘か（入ったときに決める——精算で `HasPendingBattle` が落ちても揺れない）。</summary>
    private bool _mapBattle3D;

    /// <summary>
    /// 作戦マップ（3D）から来た戦闘では、検証の欄と検証のボタン（最初から・ログを表示・戦績・作戦マップへ）を隠す（第479期の続き 2・ポン 2026-11-17）。
    /// 倍速と一時停止は残す。<b>見た目だけ</b>——戦闘の中身は 1 行も読まない。
    /// </summary>
    private void HideVerifyForMapBattle()
    {
        _mapBattle3D = Map11Session.HasPendingBattle && Map3DMode.Active;
        if (!_mapBattle3D) return;
        foreach (Control c in _verifyControls) c.Visible = false;
        _replay.Visible = _battleLogToggle.Visible = _score.Visible = _campaignBattle.Visible = false;
    }

    /// <summary>
    /// 作戦マップ（3D）の戦闘の終わり: 画面を横切る「勝利」「敗北」の帯だけ（第479期の続き 2）。<see cref="MapEndSeconds"/> 秒で作戦マップへ戻る・押せばすぐ戻る。
    /// 倒れた駒・残りの HP・賞金は戻った作戦マップの台の上、細かい戦績は作戦マップの「戦闘の記録」。<paramref name="auto"/> が偽なら帯を出すだけ（通しの門は自分で戻る）。
    /// </summary>
    private void ShowMapBattleResult(BattleResult result, bool auto)
    {
        _mapResultLayer?.QueueFree();
        _mapResultLayer = new CanvasLayer { Layer = 30 };
        AddChild(_mapResultLayer);
        Color tint = result.PlayerWon ? UiKit.Heal : UiKit.Hurt;
        var band = new ColorRect { Color = new Color(0.02f, 0.03f, 0.035f, 0.88f), MouseFilter = Control.MouseFilterEnum.Ignore };
        band.AnchorLeft = 0; band.AnchorRight = 1; band.AnchorTop = band.AnchorBottom = 0.44f;
        band.OffsetTop = -62; band.OffsetBottom = 62;
        _mapResultLayer.AddChild(band);
        foreach (float edge in new[] { 0f, 1f })
        {
            var line = new ColorRect { Color = tint, MouseFilter = Control.MouseFilterEnum.Ignore };
            line.AnchorLeft = 0; line.AnchorRight = 1; line.AnchorTop = line.AnchorBottom = edge;
            line.OffsetTop = edge == 0f ? 0 : -3; line.OffsetBottom = edge == 0f ? 3 : 0;
            band.AddChild(line);
        }
        Label title = UiKit.Text(result.PlayerWon ? "勝利" : "敗北", 64, tint);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        band.AddChild(title);
        MapEndBandShown = true;
        if (!auto) return;
        // 画面のどこを押してもすぐ戻る。
        var press = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        press.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        press.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        press.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        press.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _mapResultLayer.AddChild(press);
        bool left = false;
        void Leave()
        {
            if (left || !IsInsideTree()) return;
            left = true;
            GoToCampaign();
        }
        press.Pressed += Leave;
        GetTree().CreateTimer(MapEndSeconds).Timeout += Leave;
    }

    /// <summary>終わりの帯が出たか（検査が読む）。</summary>
    internal bool MapEndBandShown { get; private set; }

    private static bool _endShotWon, _endShotLost;

    /// <summary>絵の口 `--map3d-end-shot=<置き場>`: 終わりの札を写す（勝ち・負けを 1 枚ずつ）。遊ぶ起動では読まない。</summary>
    private async System.Threading.Tasks.Task ShotMapBattleEnd()
    {
        string? dir = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--map3d-end-shot=", System.StringComparison.Ordinal))?["--map3d-end-shot=".Length..].Trim('"');
        if (dir is null || _result is null) return;
        if (_result.PlayerWon ? _endShotWon : _endShotLost) return;
        if (_result.PlayerWon) _endShotWon = true; else _endShotLost = true;
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        System.IO.Directory.CreateDirectory(dir);
        string name = System.IO.Path.Combine(dir, _result.PlayerWon ? "battle_end_won.png" : "battle_end_lost.png");
        GetViewport().GetTexture().GetImage().SavePng(name);
        GD.Print($"MAP3D_END_SHOT {name}");
    }
}
