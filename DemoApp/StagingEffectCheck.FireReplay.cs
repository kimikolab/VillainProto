using BattleCore;
using Godot;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class StagingEffectCheck
{
    // 第257期 foesurge log <版> 混ぜ-255 0 2 1 と同じ席・札・敵。
    // W4は確認用の駒の写しだけ。本編のカタログや燃焼規則は変更しない。
    private async Task CheckFireReplay(string version, bool verify, string board = "混ぜ-255")
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        UnitDef borg = UnitCatalog.Borg;
        var traits = borg.Traits.Where(t => t is not (TraitId.BlazeFoeSurgeMax or TraitId.BlazeFoeSurge2));
        if (version == "W4") traits = traits.Append(TraitId.BlazeFoeSurgeMax);
        borg = new UnitDef {
            Id = borg.Id, Name = borg.Name, MaxHp = borg.MaxHp, Attack = borg.Attack, Speed = borg.Speed,
            Traits = traits.ToArray(), Pattern = borg.Pattern,
            Advances = borg.Advances, Actions = borg.Actions, PlusText = borg.PlusText,
            MinusText = borg.MinusText, Flavor = borg.Flavor };
        var formation = board == "雷＋ボルグ"
            ? Formation.Build(front1: borg, front3: UnitCatalog.Tsugi, center: UnitCatalog.Beni, back1: UnitCatalog.Kata, back3: UnitCatalog.Mio)
            : Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Beni, center: borg, back1: UnitCatalog.Hiyo, back3: UnitCatalog.Hane);
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        T Read<T>(string name) => (T)typeof(Main).GetField(name, flags)!.GetValue(main)!;
        void Set(string name, object value) => typeof(Main).GetField(name, flags)!.SetValue(main, value);
        void Call(string name, params object[] args) => typeof(Main).GetMethod(name, flags)!.Invoke(main, args);
        Call("ClearFormation");
        var setup = Read<UnitDef?[]>("_formation");
        for (int i = 0; i < setup.Length; i++) setup[i] = formation[i];
        Call("RefreshFormation");
        var picker = Read<OptionButton>("_stagePicker");
        picker.Select(3); picker.EmitSignal(OptionButton.SignalName.ItemSelected, 3);
        Read<SpinBox>("_seed").Value = 0;
        Read<SpinBox>("_enemyHp").Value = 400;
        Read<SpinBox>("_enemyAttack").Value = 300;
        Read<OptionButton>("_presetPicker").Hide();
        Read<Label>("_presetState").Text = $"第257期・{board}・{version} 確認用";
        if (verify) { Set("_fastSmoke", true); Set("_speed", 1000.0); }
        else
        {
            string? speed = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--fire-speed="));
            if (speed is not null) Set("_speed", double.Parse(speed[13..], System.Globalization.CultureInfo.InvariantCulture));
        }
        var scale = new EnemyScaleRule(400, 300);
        Set("_battleEnemyScale", scale);
        Call("EnterBattle", BattleEngine.Materialize(formation, 0),
            BattleEngine.Materialize(EnemyCatalog.Stages[3].Enemy, 1, scale), 0, 3,
            $"第257期 {version}・{board}・400/300・seed 0");
        var result = Read<BattleResult>("_result");
        var original = result.Events.ToArray();
        var plan = new FirePresentation(result.Events);
        var hits = FireHitPresentation.Build(result.Events);
        var ticks = TurnTickPresentation.Build(result.Events);
        int blazeAfter = Array.FindIndex(original, e => e.Kind == BattleEventKind.FireArmor && e.Text == FireArmorLabels.BlazeAlly);
        Require(hits.Unpaired.Count == 0, "第257期の被弾燃焼を全件着弾へ結べる");
        if (board == "雷＋ボルグ") Require(hits.Contacts.Keys.Any(i => result.Events[i].Kind == BattleEventKind.Thunder),
            "カタの雷の着弾に実在する被弾燃焼が重なる");
        Require(version != "W0" || plan.FoeSurges.Count == 0, "W0に敵上げを補わない");
        if (version == "W4" && board == "混ぜ-255") Require(plan.FoeSurges.Count > 0, "混ぜ-255のW4に敵上げの陽性対照がある");
        string[] actual = result.Log.Select(l => l.Text).ToArray();
        string? source = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--foesurge-log="));
        if (source is not null)
        {
            string[] reference = File.ReadAllLines(source[15..]);
            int start = Array.FindIndex(reference, l => l == "=== 戦闘開始 ===");
            Require(start >= 0 && reference.Skip(start).SequenceEqual(actual), "Claude Codeのfoesurge logと全文一致");
            GD.Print($"FIRE_PHASE257_LOG_OK {version} lines={actual.Length}");
        }
        for (int pass = 0; pass < (verify ? 2 : 1); pass++)
        {
            var field = Read<BattlefieldView3D>("_battleField");
            bool captured = false;
            // 実速度では一戦と一時停止・速度変更を手元で観察できる。
            for (int k = 0; k < (verify ? 6000 : 144000) && Read<bool>("_playing"); k++)
            {
                if (!captured && (version == "W4" ? field.FireFoeSurgePlays > 0
                    : blazeAfter >= 0 && Read<int>("_eventIndex") >= blazeAfter))
                {
                    await Wait(verify ? 0.001 : 0.08);
                    await Capture($"fire-{version.ToLowerInvariant()}-battle-pass{pass}");
                    captured = true;
                }
                await Wait(0.025);
            }
            Require(!Read<bool>("_playing"), "第257期の一戦が最後まで完走");
            var cues = hits.Contacts.Values.SelectMany(c => c).ToArray();
            Require(field.FireHitPlays == cues.Length && field.FireHitNumberPlays == cues.Sum(c => c.Beat.Numbers.Count)
                && field.FireHitPulses == cues.Sum(c => c.Beat.FirePulses), "第257期でも各着弾の数字と刻みの件数が一致");
            Require(field.FireFoeSurgePlays == plan.FoeSurges.Count
                && field.FireFoeSurgeTargets == plan.FoeSurgeMembers.Count, "第257期の生き残った敵だけが一斉に跳ね上がる");
            Require(field.FireCuePlays == result.Events.Count(e => e.Kind is BattleEventKind.FireLevel or BattleEventKind.FireArmor)
                && Read<int>("_tickPlays") == result.Events.Count(TickPresentation.IsTick), "台本の見出しを一件ずつ通す");
            Require(field.TurnTickBeatPlays == ticks.Starts.Values.Sum(r => r.Beats.Count), "ターン頭の駒ごとの拍も維持");
            Require(result.Events.SequenceEqual(original), "台本の順と内容を再生中に変更しない");
            foreach (var pawn in field.Pawns.Values)
            {
                var hp = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId
                    && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Death or BattleEventKind.Revive);
                if (hp is not null) Require(pawn.Hp == Math.Clamp(hp.HpAfter, 0, pawn.MaxHp), "第257期の最終HPが台本と一致");
            }
            GD.Print($"FIRE_PHASE257_REPLAY_OK {version} board={board} pass={pass} turns={result.Turns} hits={field.FireHitPlays} numbers={field.FireHitNumberPlays} pulses={field.FireHitPulses} surgeBeats={field.FireFoeSurgePlays} surgeTargets={field.FireFoeSurgeTargets}");
            if (pass == 0 && verify) Call("ReplayBattle");
        }
        if (verify) { main.QueueFree(); await Wait(0.1); }
    }
}
