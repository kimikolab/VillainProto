using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 同じ表示入口を使い、実台本の件数・最終HP・再戦と、左右の描画を確認する。
public partial class FireCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            CheckIndex();
            if (OS.GetCmdlineUserArgs().Contains("--audio")) await CheckFireAudio();
            else if (OS.GetCmdlineUserArgs().Contains("--replay")) await Replay();
            else await Preview();
            GD.Print("FIRE_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void CheckIndex()
    {
        var events = new[] {
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, TargetId = 1, Text = FireLevelLabels.Stage, Amount = 4 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, TargetId = 1, Text = FireLevelLabels.Burnout, Amount = 4 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, TargetId = 1, Text = FireLevelLabels.Spent, Amount = 1 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, TargetId = 1 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, TargetId = 3, Text = FireLevelLabels.Rain, Amount = 2, Slot = 7 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Heal, ActorId = 2, TargetId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 3 },
        };
        var index = new FirePresentation(events);
        Require(index.Attacks.Count == 2 && index.Attacks[4].Level == 4
            && index.Attacks[4].Kind == FireLevelLabels.Burnout && index.Attacks[6].Ordinal == 7,
            "撃った・他者の割り込みを跨いでも大技の元の火勢を保持し、次の攻撃へ持ち越さない");
        Require(index.FastEvents.Contains(6) && index.FastEvents.Contains(7)
            && !index.FastEvents.Contains(8) && !index.FastEvents.Contains(9), "追撃の後隙だけ詰め、他者と次の攻撃には持ち越さない");
        var expanded = new[] {
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, Text = FireLevelLabels.Unleash, Amount = 4 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, Text = FireLevelLabels.Spent, Amount = 1 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, Text = FireLevelLabels.Blaze, Amount = 123 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 9, Amount = 369 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 1, TargetId = 2, Text = FireArmorLabels.BlazeAlly },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.StatusGain, ActorId = 1, TargetId = 2, Text = StatusKeys.Burn },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 2, TargetId = 2, Text = FireArmorLabels.Mend },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 1, TargetId = 3, Text = FireArmorLabels.BlazeAlly },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 3 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 1, TargetId = 4, Text = FireArmorLabels.BlazeAlly },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Heal, ActorId = 5, TargetId = 4 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 1, TargetId = 6, Text = FireArmorLabels.BlazeAlly },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.TurnStart },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 6 },
        };
        var next = new FirePresentation(expanded);
        Require(next.Attacks[3].Kind == FireLevelLabels.Blaze && next.Attacks[3].Level == 4,
            "爆炎は薙ぎに上書きされず、味方への名目量123を火勢と誤読しない");
        Require(next.AllyOutcomes[4] == FireAllyOutcome.Heal && next.AllyOutcomes[7] == FireAllyOutcome.Damage
            && next.AllyOutcomes[9] == FireAllyOutcome.Heal && next.AllyOutcomes[11] == FireAllyOutcome.Quiet,
            "満タンの癒し・反転・被災を区別し、次の手番へ結果を探しに行かない");
        var chain = Enumerable.Range(1, 5).SelectMany(k => new[] {
            new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 2, TargetId = 9, Text = FireLevelLabels.EmbersHit, Amount = 1, Slot = k },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, TargetId = 9, Amount = 84 },
            new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 2, TargetId = 9 },
        }).ToArray();
        var chained = new FirePresentation(chain);
        Require(chained.Attacks.Count == 5 && chained.Attacks.Values.Select(c => c.Ordinal).SequenceEqual(Enumerable.Range(1, 5))
            && chained.Attacks.Keys.All(chained.FastEvents.Contains), "単体相手にも残り火は台本どおり5発、5つの着弾を待つ");
        Require(FirePresentation.PowerText(new(FireLevelLabels.Burnout, 4, 0), 392).Contains("392"),
            "今回の一撃は台本の量そのものを表示し、倍率を重ねない");
    }

    private async Task Preview()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        {
            DemoOpening[] openings = {
                new(1,team,"borg","ボルグ",0,100,100,20,AttackPattern.Sweep,true),
                new(2,team,"hota","ホタ",2,100,100,20,AttackPattern.Single,false),
                new(3,team,"hiyo","ヒヨ",4,100,100,20,AttackPattern.Single,false),
                new(4,1-team,"knight","標的1",0,100,100,20,AttackPattern.Single,true),
                new(5,1-team,"knight","標的2",2,100,100,20,AttackPattern.Single,true),
                new(6,1-team,"knight","標的3",4,100,100,20,AttackPattern.Single,true),
            };
            field.BeginBattle(openings, "燃焼軸・演出確認", 3);
            foreach (var p in field.Pawns.Values) { p.AnimationSpeed = 1; p.SetFireRemaining(4); p.SetFireLevel(3); }
            var borg = field.FindPawn(1)!; var hota = field.FindPawn(2)!; var hiyo = field.FindPawn(3)!;
            var target = field.FindPawn(4)!;
            var hits = new[] { target, field.FindPawn(5)!, field.FindPawn(6)! };
            await Wait(0.90); await Capture("aura-" + team);
            await Wait(0.17); await Capture("aura-rise-" + team);
            for (int level = 1; level <= 4; level++)
            {
                hota.SetFireLevel(level);
                await Wait(0.65);
                if (team == 0) await Capture("level-" + level);
                var stageAttack = field.PlayFireAttack(hota, target, level == 1 ? new[] { target } : hits,
                    new(FireLevelLabels.Stage, level, 0), 1);
                if (level is 2 or 3)
                {
                    await Wait(level == 2 ? 0.24 : 0.62);
                    await Capture($"pierce-{level}-{team}");
                }
                await stageAttack;
                if (level is 2 or 3) { await Wait(0.045); await Capture($"pierce-impact-{level}-{team}"); }
                await Wait(0.40);
            }
            await Wait(0.60); await Capture("aura-critical-" + team);
            hota.ShowFirePower(new(FireLevelLabels.Burnout, 4, 0), 392);
            field.ShowFireCue(new() { Turn = 1, Kind = BattleEventKind.FireLevel, Text = FireLevelLabels.Overflow, Amount = 4 }, hiyo, hota, 1);
            await Wait(0.20); await Capture("overflow-" + team);
            field.ShowFireCue(new() { Turn = 1, Kind = BattleEventKind.FireLevel, Text = FireLevelLabels.Fed, Amount = 2 }, borg, hota, 1);
            await Wait(0.20); await Capture("fed-" + team);
            hota.ClearFirePower();
            Require(hota.FireLevel == 4, "攻撃力の上乗せ通知で火勢を変えない");
            field.ShowFireCue(new() { Turn = 1, Kind = BattleEventKind.FireLevel, Text = FireLevelLabels.Gift, Amount = 4 }, hiyo, hota, 1);
            await Wait(0.34); await Capture("gift-" + team);
            await Wait(0.18); await Capture("gift-receive-" + team);
            await Wait(0.22);
            field.ShowFireCue(new() { Turn = 1, Kind = BattleEventKind.FireLevel, Text = FireLevelLabels.GiftTurn }, hiyo, hota, 1);
            await Wait(0.20); await Capture("gift-turn-" + team);
            var attack = field.PlayFireAttack(hota, target, hits, new(FireLevelLabels.Burnout, 4, 0), 1);
            await Wait(0.66); await Capture("gather-" + team);
            await Wait(0.20); await Capture("sword-swing-" + team);
            await attack;
            await Wait(0.10); await Capture("burnout-" + team);
            await Wait(0.15); await Capture("inferno-" + team);
            for (int i = 1; i <= 10; i++)
                await field.PlayFireAttack(hota, hits[i % 3], new[] { hits[i % 3] }, new(FireLevelLabels.Rain, 3, i), 2);
            field.ShowFireCue(new() { Turn = 1, Kind = BattleEventKind.FireLevel, Text = FireLevelLabels.CallMark }, hota, borg, 1);
            Require(borg.FireMarkActive, "放熱の印は通知後に残る");
            await Wait(0.4); await Capture("mark-" + team);
            field.ShowFireCue(new() { Turn = 1, Kind = BattleEventKind.FireLevel, Text = FireLevelLabels.Called }, hiyo, borg, 1);
            Require(!borg.FireMarkActive, "指名で印が消える");
            var cleave = field.PlayFireAttack(borg, target, hits, new(FireLevelLabels.Unleash, 4, 0), 1);
            await Wait(0.59); await Capture("cleave-travel-" + team);
            await cleave;
            await Wait(0.06); await Capture("unleash-" + team);
            await Wait(0.60);
            var blaze = field.PlayFireAttack(borg, target, hits, new(FireLevelLabels.Blaze, 4, 0), 1);
            await blaze;
            await Wait(0.09); await Capture("blaze-" + team);
            field.ShowFireCue(new() { Turn = 1, Kind = BattleEventKind.FireArmor, Text = FireArmorLabels.BlazeAlly }, borg, hota, 1, FireAllyOutcome.Heal);
            field.ShowFireCue(new() { Turn = 1, Kind = BattleEventKind.FireArmor, Text = FireArmorLabels.BlazeAlly }, borg, hiyo, 1, FireAllyOutcome.Damage);
            await Wait(0.17); await Capture("blaze-allies-" + team);
            Require(!hota.FireInvasive && hiyo.FireInvasive, "同じ爆炎で味方の癒しと被災を描き分ける");
            for (int k = 1; k <= 5; k++)
            {
                hota.ShowFirePower(new(FireLevelLabels.EmbersHit, 1, k), 84);
                await field.PlayFireAttack(hota, target, new[] { target }, new(FireLevelLabels.EmbersHit, 1, k), 1);
                if (k == 3) await Capture("embers-chain-" + team);
            }
            hota.ClearFirePower();
            Require(field.FireBlazePlays == 1 && field.FireEmbersHitPlays == 5, "爆炎1回・単体への残り火5発を再生");
            field.ShowFireTick(hiyo, false, true, 0.4);
            Require(hiyo.FireInvasive, "ダメージ刻みは侵食");
            field.ShowFireTick(hiyo, true, false, 0.4);
            Require(!hiyo.FireInvasive, "回復刻みはオーラ");
            hota.SetFireRemaining(6); hota.SetFireLevel(1, true);
            Require(hota.FireLevel == 1 && hota.FireRemaining == 6, "火勢と残り時間は独立");
            borg.SetFireMark(true); borg.AnimateDeath();
            Require(!borg.FireMarkActive && borg.FireLevel == 0, "死亡で常設火と印を消す");
            // 待ちの途中に盤を作り直しても旧攻撃の着弾を出さない。
            var interrupted = field.PlayFireAttack(hota, target, hits, new(FireLevelLabels.Burnout, 4, 0), 1);
            field.BeginBattle(openings, "再戦", 0);
            await interrupted;
            Require(field.FireAttackPlays == 0 && field.Pawns.Values.All(p => p.FireLevel == 0), "再戦で旧演出を破棄");
            var interruptedCleave = field.PlayFireAttack(field.FindPawn(1), field.FindPawn(4),
                new[] { field.FindPawn(4)! }, new(FireLevelLabels.Unleash, 4, 0), 1);
            await Wait(0.50);
            field.BeginBattle(openings, "踏み込み中の再戦", 0);
            await interruptedCleave;
            Require(field.FireAttackPlays == 0, "踏み込み中の再戦で旧着弾を破棄");
            var interruptedBlaze = field.PlayFireAttack(field.FindPawn(1), field.FindPawn(4),
                new[] { field.FindPawn(4)! }, new(FireLevelLabels.Blaze, 4, 0), 1);
            field.BeginBattle(openings, "爆炎の溜め中の再戦", 0);
            await interruptedBlaze;
            Require(field.FireBlazePlays == 0 && field.Pawns.Values.All(p => p.FirePowerText.Length == 0),
                "爆炎の溜め中の再戦で旧着弾と一撃の表示を残さない");
        }
        field.QueueFree();
        await Wait(0.1);
    }

    private async Task Replay()
    {
        bool solo = OS.GetCmdlineUserArgs().Contains("--solo");
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        T Read<T>(string name) => (T)typeof(Main).GetField(name, Flags)!.GetValue(main)!;
        void Call(string name, params object[] args) => typeof(Main).GetMethod(name, Flags)!.Invoke(main, args);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        Call("ClearFormation");
        string[] keys = { "golm", "hisa", "borg", "hota", solo ? "shio" : "hiyo" };
        for (int i = 0; i < keys.Length; i++) Call("DropUnit", i, keys[i]);
        var picker = Read<OptionButton>("_stagePicker");
        picker.Select(3); picker.EmitSignal(OptionButton.SignalName.ItemSelected, 3);
        Read<SpinBox>("_enemyHp").GetLineEdit().Text = "400";
        Read<SpinBox>("_enemyAttack").GetLineEdit().Text = "300";
        Read<SpinBox>("_seed").Value = 0;
        Call("StartBattle");
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < 3000 && Read<bool>("_playing"); i++) await Wait(0.025);
            Require(!Read<bool>("_playing"), "実台本の再生が完走");
            var result = Read<BattleResult>("_result");
            var field = Read<BattlefieldView3D>("_battleField");
            var index = new FirePresentation(result.Events);
            Require(field.FireAttackPlays == index.Attacks.Count, "炎の攻撃を一件ずつ再生");
            Require(field.FireRainPlays == index.Attacks.Values.Count(c => c.Kind == FireLevelLabels.Rain), "火の雨の発数が一致");
            Require(field.FireBlazePlays == index.Attacks.Values.Count(c => c.Kind == FireLevelLabels.Blaze), "爆炎の発数が一致");
            Require(field.FireEmbersHitPlays == result.Events.Count(e => e.Kind == BattleEventKind.FireLevel && e.Text == FireLevelLabels.EmbersHit), "残り火の全発が専用演出を通る");
            Require(field.FireCuePlays == result.Events.Count(e => e.Kind is BattleEventKind.FireLevel or BattleEventKind.FireArmor), "炎の全通知を表示");
            foreach (string required in solo ? new[] { FireLevelLabels.BlazeSolo, FireLevelLabels.Blaze }
                : new[] { FireLevelLabels.Burnout, FireLevelLabels.Blaze, FireLevelLabels.Overflow, FireLevelLabels.Fed })
                Require(field.FireLabelsShown.Contains(required), "実戦に見せ場がある: " + required);
            if (solo)
                Require(index.AllyOutcomes.Values.Contains(FireAllyOutcome.Heal)
                    && index.AllyOutcomes.Values.Contains(FireAllyOutcome.Damage), "独りの爆炎でも、実台本に癒しと味方への傷が両方ある");
            foreach (var pawn in field.Pawns.Values)
            {
                var hp = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId
                    && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Death or BattleEventKind.Revive);
                if (hp is not null) Require(pawn.Hp == Math.Clamp(hp.HpAfter, 0, pawn.MaxHp), "最終HPが台本どおり");
            }
            GD.Print($"FIRE_REPLAY_OK pass={pass} cues={field.FireCuePlays} attacks={field.FireAttackPlays} rain={field.FireRainPlays} blaze={field.FireBlazePlays} embers={field.FireEmbersHitPlays}");
            if (pass == 0) Call("ReplayBattle");
        }
        main.QueueFree();
        await Wait(0.1);
    }

    private async Task Capture(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string dir = ProjectSettings.GlobalizePath("res://../outputs/fire-check");
        System.IO.Directory.CreateDirectory(dir);
        GetViewport().GetTexture().GetImage().SavePng(dir + "/" + name + ".png");
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
