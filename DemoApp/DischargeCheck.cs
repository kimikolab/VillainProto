using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// Godot_console.exe --headless --path DemoApp res://DischargeCheck.tscn
public partial class DischargeCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            CheckAttribution();
            var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
            AddChild(main);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var formation = Formation.Build(front1: UnitCatalog.Sid, front3: UnitCatalog.Beni,
                center: UnitCatalog.Mio, back1: UnitCatalog.Guza, back3: UnitCatalog.Kata);
            var players = BattleEngine.Materialize(formation, 0);
            var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[4].Enemy, 1);
            ((OptionButton)typeof(Main).GetField("_stagePicker", Flags)!.GetValue(main)!).Selected = 4;
            ((SpinBox)typeof(Main).GetField("_seed", Flags)!.GetValue(main)!).Value = 4;
            typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
            typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
            typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] { players, enemies, 4, 4, "放電の確認" });
            var teams = players.Concat(enemies).ToDictionary(u => u.InstanceId, u => u.TeamId);
            int kata = players.Single(u => u.Def.Id == "kata").InstanceId;
            var result = (BattleResult)typeof(Main).GetField("_result", Flags)!.GetValue(main)!;
            var credits = DischargePresentation.Count(result.Events, teams);
            Require(result.TallyByUnit["kata"].DamageToEnemy == 222 && credits[kata].Enemy == 96, $"第五波 seed4 放電(敵)={credits[kata].Enemy}");
            int hits = 0, heals = 0;
            for (int i = 0; i < result.Events.Count; i++)
            {
                var e = result.Events[i];
                if (DischargePresentation.Cause(result.Events, i) is null) continue;
                if (e.Kind == BattleEventKind.Heal) { heals++; continue; }
                hits++;
                var label = ((string, Color))typeof(Main).GetMethod("DamageSource", Flags)!.Invoke(main, new object?[] { i, e, null })!;
                Require(label.Item1.StartsWith("[放電]") && label.Item2 == ThunderFx.Cyan, "放電ログの字と色");
            }
            Require(hits > 0 && heals > 0, "ダメージ・回復反転の陽性対照");
            for (int i = 0; i < 600 && (bool)typeof(Main).GetField("_playing", Flags)!.GetValue(main)!; i++)
                await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
            Require(!(bool)typeof(Main).GetField("_playing", Flags)!.GetValue(main)!, "再生完走");
            var log = (RichTextLabel)typeof(Main).GetField("_battleLog", Flags)!.GetValue(main)!;
            Require(log.GetParsedText().Contains("[放電]") && log.GetParsedText().Contains("[放電→回復]"), "実際のログに放電と反転回復");
            typeof(Main).GetMethod("SetScoreVisible", Flags)!.Invoke(main, new object[] { true });
            var panel = (ScorePanel)typeof(Main).GetField("_scorePanel", Flags)!.GetValue(main)!;
            var grid = Descendants(panel).OfType<GridContainer>().First();
            var cells = grid.GetChildren().OfType<Label>().Select(l => l.Text).ToList();
            int enemyCol = cells.IndexOf("放電(敵)"), allyCol = cells.IndexOf("放電(味)");
            int row = cells.IndexOf(UnitCatalog.Kata.Name);
            Require(enemyCol > 0 && allyCol > 0 && row >= grid.Columns && cells[row + enemyCol] == "96", "戦績のカタ行に96");
            Require(cells[row + allyCol] == "16", "味方への放電を別欄に表示");
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng("res://../.tmp/discharge-score.png");
            }
            var quiet = BattleEngine.Run(BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Gald), 0),
                BattleEngine.Materialize(EnemyCatalog.Stages[0].Enemy, 1), 0, verbose: true);
            Require(DischargePresentation.Count(quiet.Events, teams).Count == 0
                && !Enumerable.Range(0, quiet.Events.Count).Any(i => DischargePresentation.Cause(quiet.Events, i) is not null), "放電なしの陰性対照");
            GD.Print($"DISCHARGE_CHECK_COMPLETE ok=True enemy={credits[kata].Enemy} ally={credits[kata].Ally} hits={hits} heals={heals}");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private static void CheckAttribution()
    {
        BattleEvent E(BattleEventKind kind, int? actor, int target, int amount = 0, string? text = null,
            TraitId? trait = null) => new() { Kind = kind, Turn = 1, ActorId = actor, TargetId = target,
                Amount = amount, Text = text, Team = target >= 10 ? 1 : 0, SourceTrait = trait,
                InverterId = trait == TraitId.Inverse ? 3 : null };
        var events = new[] {
            E(BattleEventKind.StatusGain, 1, 10, text: "shock"),
            E(BattleEventKind.StatusGain, 2, 10, text: "shock"),
            E(BattleEventKind.ShockSpent, 99, 10),
            E(BattleEventKind.Discharge, 10, 11, 8), E(BattleEventKind.Damage, 10, 11, 6),
            E(BattleEventKind.Damage, 10, 11, 50), // 後続の別ダメージを巻き込まない
            E(BattleEventKind.StatusGain, 1, 4, text: "shock"), E(BattleEventKind.ShockSpent, 99, 4),
            E(BattleEventKind.Discharge, 4, 5, 8), E(BattleEventKind.Damage, 4, 5, 8),
            E(BattleEventKind.Discharge, 4, 6, 8, trait: TraitId.Inverse), E(BattleEventKind.Heal, 3, 6, 8),
        };
        var credits = DischargePresentation.Count(events, new Dictionary<int, int> { [1] = 0, [2] = 0 });
        Require(credits[2].Enemy == 6 && credits[1].Enemy == 0 && credits[1].Ally == 8,
            "最後の付与者・実ダメージ・味方側・反転除外");
        Require(DischargePresentation.Cause(events, 11) is not null, "反転した回復を識別");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren()) { yield return child; foreach (var next in Descendants(child)) yield return next; }
    }
}
