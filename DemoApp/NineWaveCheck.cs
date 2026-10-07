using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 選択 → 出撃 → 戦績 → 再生し直し → 編成に戻る、を本物の画面で通す。
// Godot_console.exe --path DemoApp --headless res://NineWaveCheck.tscn
public partial class NineWaveCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    public override async void _Ready()
    {
        try
        {
            var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
            AddChild(main);
            T Read<T>(string name) => (T)typeof(Main).GetField(name, Flags)!.GetValue(main)!;
            void Call(string name, params object[] args) => typeof(Main).GetMethod(name, Flags)!.Invoke(main, args);
            typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
            typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
            var picker = Read<OptionButton>("_stagePicker");
            int count = EnemyCatalog.Stages.Count;
            // 第291期: 検証の波の後ろに試遊の波（区切り ＋ `EnemyCatalog.PlaytestStages`）が並ぶ。試遊の波は倍率が波ごとに固定。
            int playBase = count + 1 + EnemyCatalog.TestStages.Count + 1;
            Require(picker.ItemCount == playBase + EnemyCatalog.PlaytestStages.Count, "波の項目数");
            Require(picker.GetPopup().IsItemSeparator(count), "選べない見出し");
            Require(picker.GetPopup().IsItemSeparator(playBase - 1), "選べない見出し（試遊）");
            for (int index = 0; index < picker.ItemCount; index++)
            {
                if (index == count || index == playBase - 1) continue;
                picker.Select(index);
                picker.EmitSignal(OptionButton.SignalName.ItemSelected, index);
                var play = index >= playBase ? EnemyCatalog.PlaytestStages[index - playBase] : null;
                var test = index > count && play is null ? EnemyCatalog.TestStages[index - count - 1] : null;
                string title = play?.Name ?? test?.Name ?? EnemyCatalog.Stages[index].Name;
                Require(Read<RichTextLabel>("_detail").Text.Contains(title), "選択した波の説明");
                // 既定以外の倍率も、本編と同じ入力欄から渡す。
                Read<SpinBox>("_enemyHp").GetLineEdit().Text = "130";
                Read<SpinBox>("_enemyAttack").GetLineEdit().Text = "125";
                Call("StartBattle");
                await Finished(main);
                var opening = Read<List<DemoOpening>>("_battleOpening");
                var enemies = opening.Where(o => o.Team == BattleContext.EnemyTeam).ToArray();
                var scale = play?.Scale ?? new EnemyScaleRule(130, 125);
                var expected = play is not null ? BattleEngine.MaterializeEnemy(play.Enemy, scale)
                    : test is not null ? BattleEngine.MaterializeEnemy(test.Enemy, scale)
                    : BattleEngine.Materialize(EnemyCatalog.Stages[index].Enemy, BattleContext.EnemyTeam, scale);
                Require(enemies.Select(o => (o.Slot, o.Hp, o.Attack)).SequenceEqual(
                    expected.Select(u => (u.Slot, u.Hp, u.CurrentAttack))), "敵の人数・席・倍率");
                Require(enemies.Select(o => o.InstanceId).Distinct().Count() == enemies.Length, "同名の敵の識別");
                Require(Read<int>("_battleStageIndex") == (play is not null ? (play.Name.StartsWith("ボス", StringComparison.Ordinal) ? count - 1 : 0) : test is null ? index : 0), "背景の番号");
                Call("SetScoreVisible", true);
                var score = Read<ScorePanel>("_scorePanel");
                var body = (VBoxContainer)typeof(ScorePanel).GetField("_body", Flags)!.GetValue(score)!;
                Require(score.Visible && body.GetChildren().OfType<Label>()
                    .Any(l => !l.IsQueuedForDeletion() && l.Text.Contains(AkaPresentation.Text(title))), "戦績の波名");
                var result = Read<BattleResult>("_result");
                Call("ReplayBattle");
                await Finished(main);
                Require(ReferenceEquals(result, Read<BattleResult>("_result")), "再生し直しで台本を維持");
                Require(Read<string>("_battleTitle") == title, "再生し直しの波名");
                Call("ReturnToFormation");
                Require(!picker.Disabled && picker.Selected == index, "編成に戻っても波の選択を維持");
                GD.Print($"NINE_WAVE_CHECK stage={title} enemies={enemies.Length} ok=True");
            }
            GD.Print("NINE_WAVE_CHECK_COMPLETE ok=True");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task Finished(Main main)
    {
        for (int i = 0; i < 1200; i++)
        {
            if (!(bool)typeof(Main).GetField("_playing", Flags)!.GetValue(main)!) return;
            await ToSignal(GetTree().CreateTimer(0.025), SceneTreeTimer.SignalName.Timeout);
        }
        throw new InvalidOperationException("再生が完了しませんでした");
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
