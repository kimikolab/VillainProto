using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 第211期: 戦績表の2欄（破片(与)・反射）が本物の画面の経路（Main の入口 → 戦績パネル）で表示されることを確かめる台。
// 盤面にもカタログにも触らない。行は `継ぎ当て×分散回復`（ポンが遊んだ行）の第五波 seed 6。
//
//     Godot_console.exe --path DemoApp --headless --quit-after 60000 res://ScorePanelCheck.tscn
public partial class ScorePanelCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public override async void _Ready()
    {
        try
        {
            await Run();
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GD.Print("SCORE_CHECK_COMPLETE ok=False"); GetTree().Quit(1); }
    }

    private async Task Run()
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        string row = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--score-row=", StringComparison.Ordinal))?["--score-row=".Length..] ?? "継ぎ当て×分散回復";
        const int stage = 4, seed = 6;
        Formation f = Presets.Compare.First(r => r.Name == row).F;
        var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, BattleContext.EnemyTeam);
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] { players, enemies, seed, stage, "" });
        typeof(Main).GetMethod("SetScoreVisible", Flags)!.Invoke(main, new object[] { true });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var panel = (ScorePanel)typeof(Main).GetField("_scorePanel", Flags)!.GetValue(main)!;
        var result = (BattleResult)typeof(Main).GetField("_result", Flags)!.GetValue(main)!;
        GridContainer grid = Descendants(panel).OfType<GridContainer>().First();
        var cells = grid.GetChildren().OfType<Label>().Select(l => l.Text).ToList();
        int w = grid.Columns;
        var head = cells.Take(w).ToList();
        int ia = head.IndexOf("破片(与)"), ir = head.IndexOf("反射"), ie = head.IndexOf("与(敵)"), it = head.IndexOf("被");
        bool ok = panel.Visible && ia > 0 && ir > 0;
        GD.Print($"SCORE_CHECK row={row} stage={stage + 1} seed={seed} turns={result.Turns} won={result.PlayerWon} columns={w - 1} armorCol={ia} reflectCol={ir}");
        for (int k = w; k + w <= cells.Count; k += w)
        {
            string name = cells[k];
            string armor = ia > 0 ? cells[k + ia] : "?", refl = ir > 0 ? cells[k + ir] : "?";
            GD.Print($"SCORE_ROW {name} 破片(与)={armor} 反射={refl} 与(敵)={cells[k + ie]} 被={cells[k + it]}");
            // 表の値が帳簿と同じか
            var t = result.TallyByUnit.FirstOrDefault(p => UnitCatalog.Everyone.FirstOrDefault(u => u.Id == p.Key)?.Name == name).Value;
            if (t is not null) ok &= armor == t.ArmorOut.ToString() && refl == t.ReflectByPlank.ToString();
        }
        GD.Print($"SCORE_CHECK_COMPLETE ok={ok}");
    }

    private static IEnumerable<Node> Descendants(Node n)
    {
        foreach (Node c in n.GetChildren()) { yield return c; foreach (Node d in Descendants(c)) yield return d; }
    }
}
