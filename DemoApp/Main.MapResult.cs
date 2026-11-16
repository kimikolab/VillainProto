using BattleCore;
using Godot;
using System.Linq;

// =====================================================================================
// 作戦マップ（3D）から来た戦闘の、終わりの札（第479期・指示書 design/PHASE479_LESS_WINDOWS_SPEC.md §2）。
// 出すのは 勝敗・倒れた駒の顔・残りの HP・賞金・「作戦マップへ」だけ。戦績の表は「詳細」の奥（今までの「戦績 (T)」と同じ表）。
// **判定は 1 つも持たない**——勝敗と倒れた駒は `BattleResult`、残りの枚数と HP は作戦マップの戦闘の記録、賞金は金の増えた分を読むだけ。
// 再生の速さは次の戦闘へ持ち越す（Codex の試遊 2026-11-16: 戦闘ごとに ×2 に戻っていた）。
// =====================================================================================
public partial class Main
{
    /// <summary>選んだ再生の速さ（0 ＝ ×0.5 ／ 1 ＝ ×1 ／ 2 ＝ ×2 ／ 3 ＝ ×4）。場面を作り直しても残す。</summary>
    private static int _keptSpeed = 2;
    private static double SpeedOf(int index) => index switch { 0 => 0.5, 2 => 2.0, 3 => 4.0, _ => 1.0 };

    private CanvasLayer? _mapResultLayer;

    /// <summary>作戦マップ（3D）の戦闘の終わりの札を出す。<paramref name="gold"/> はこの戦闘で増えた金。</summary>
    private void ShowMapBattleResult(BattleResult result, int gold)
    {
        _mapResultLayer?.QueueFree();
        _mapResultLayer = new CanvasLayer { Layer = 30 };
        AddChild(_mapResultLayer);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.03f, 0.05f, 0.043f, 0.96f), result.PlayerWon ? UiKit.Heal : UiKit.Hurt));
        _mapResultLayer.AddChild(panel);
        panel.AnchorLeft = panel.AnchorRight = 0.5f;
        panel.AnchorTop = panel.AnchorBottom = 0.80f;
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Begin;
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);
        Label title = UiKit.Text(result.PlayerWon ? "勝利" : "敗北", 34, result.PlayerWon ? UiKit.Heal : UiKit.Hurt);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);
        // 倒れた駒の顔（出撃した駒のうち、決着のときに立っていない駒）。
        var fallen = result.PlayerStarterFallen.Select(id => UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id)).Where(d => d is not null).ToList();
        if (fallen.Count > 0)
        {
            var faces = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            faces.AddThemeConstantOverride("separation", 6);
            foreach (UnitDef? d in fallen)
            {
                var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(60, 60), TooltipText = d!.Name, Modulate = new Color(0.75f, 0.62f, 0.62f) };
                face.SetUnit(d);
                faces.AddChild(face);
            }
            col.AddChild(faces);
        }
        Map11Session.Line? last = Map11Session.BattleLog.LastOrDefault();
        string line = (last is null ? "" : $"残り {last.Alive} 枚 ・ HP {last.HpPercent:F0}%") + (gold > 0 ? $"　　賞金 ＋{gold}" : "");
        if (fallen.Count > 0) line = $"倒れた {fallen.Count}　　" + line;
        Label stat = UiKit.Text(line, 22, Colors.White);
        stat.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(stat);
        Button back = UiKit.ActionButton("作戦マップへ", UiKit.Gold);
        back.CustomMinimumSize = new Vector2(0, 60);
        back.AddThemeFontSizeOverride("font_size", 24);
        back.Pressed += GoToCampaign;
        col.AddChild(back);
        Button more = UiKit.ActionButton("詳細", UiKit.Muted);
        more.CustomMinimumSize = new Vector2(0, 32);
        more.Pressed += ToggleScore;
        col.AddChild(more);
    }

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
