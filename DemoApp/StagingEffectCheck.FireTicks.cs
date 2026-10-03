using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class StagingEffectCheck
{
    private static BattleEvent Tick(int id, string text, int? ordinal = null, int? count = null, bool inverse = false) =>
        new() { Turn = 1, Kind = BattleEventKind.Status, TargetId = id, Text = text,
            TickIndex = ordinal, TickCount = count, SourceTrait = inverse ? TraitId.Inverse : null,
            InverterId = inverse ? 2 : null };
    private static BattleEvent TickHp(int id, int amount, int hp, bool heal = false, int? actor = null) =>
        new() { Turn = 1, Kind = heal ? BattleEventKind.Heal : BattleEventKind.Damage,
            TargetId = id, ActorId = actor, Amount = amount, HpAfter = hp };

    private async Task CheckFireTicks(BattlefieldView3D field)
    {
        DemoOpening[] openings = [
            new(1, 0, "borg", "ボルグ", 0, 100, 100, 10, AttackPattern.Single, false),
            new(2, 0, "beni", "ベニ", 3, 100, 100, 10, AttackPattern.Single, false),
            new(3, 1, "knight", "焼かれる敵", 0, 100, 100, 10, AttackPattern.Single, false),
            new(4, 0, "hota", "静かなオーラ", 4, 100, 100, 10, AttackPattern.Single, false),
        ];
        var script = new List<BattleEvent> { new() { Turn = 1, Kind = BattleEventKind.TurnStart },
            Tick(1, "毒"), TickHp(1, 5, 95), Tick(2, "毒", inverse: true), TickHp(2, 5, 100, true, 2),
            Tick(3, "毒"), TickHp(3, 7, 93),
            new() { Turn = 1, Kind = BattleEventKind.FireArmor, TargetId = 1, ActorId = 1, Text = FireArmorLabels.Mend },
            TickHp(1, 5, 100, true, 1), Tick(2, "燃焼", inverse: true), TickHp(2, 6, 100, true, 2) };
        for (int k = 1; k <= 4; k++)
        {
            script.Add(Tick(3, "燃焼", k, 4));
            script.Add(TickHp(3, 6, 93 - k * 6));
        }
        script.Add(Tick(4, "燃焼"));
        script.Add(new() { Turn = 1, Kind = BattleEventKind.StatSnapshot, TargetId = 1 });
        // 手番中の起爆・爆炎の癒しは、この窓へ入れない。
        script.Add(new() { Turn = 1, Kind = BattleEventKind.Status, TargetId = 3, Text = "燃焼", ActorId = 2 });
        script.Add(new() { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 1, TargetId = 2, Text = FireArmorLabels.Convert });
        var original = script.ToArray();
        var plan = TurnTickPresentation.Build(script);
        Require(plan.Starts.Count == 1, "ターン頭だけを一つの区間にする");
        var range = plan.Starts.Values.Single();
        Require(range.Beats.Select(b => b.TargetId).SequenceEqual(new[] { 1, 2, 3, 4 }), "毒→火の2巡を駒ごとの一巡へ");
        Require(range.Beats[2].Numbers.Single(n => n.Burn).Amount == 24 && range.Beats[2].FirePulses == 4,
            "燃焼は一つの数字と実在する4回の小火");
        Require(range.Beats[0].FireHeal && !range.Beats[0].FireDamage && range.Beats[1].FireHeal,
            "火の癒しと反転はオーラ");
        Require(!range.Beats[3].FireDamage && !range.Beats[3].FireHeal, "帰結なしは静かなオーラ");
        Require(script.SequenceEqual(original), "元の出来事を変更・並べ替えない");
        CheckTickBarriers();
        CheckRealTickPlans();
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            field.BeginBattle(openings, "ターン頭の毒と燃焼", 1);
            foreach (var pawn in field.Pawns.Values) { pawn.AnimationSpeed = speed; pawn.SetFireLevel(4); }
            foreach (var beat in range.Beats)
            {
                var pawn = field.FindPawn(beat.TargetId)!;
                var hp = script.Take(range.End).LastOrDefault(e => e.TargetId == beat.TargetId
                    && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal);
                if (hp is not null) pawn.SetHp(hp.HpAfter);
                field.ShowTurnTickBeat(pawn, beat, speed);
                await Wait(0.18 / speed);
                Require(pawn.FireInvasive == beat.FireDamage, "焼かれる／オーラを台本の帰結で出し分ける");
                await Capture($"fire-tick-{beat.TargetId}-speed{speed}");
                await Wait(beat.Seconds / speed);
            }
            Require(field.TurnTickBeatPlays == 4 && field.TurnTickNumberPlays == 6 && field.TurnTickFirePulses == 7,
                "4体を各一拍、毒と火の6数字、火の7刻みで表示");
            Require(field.FindPawn(3)!.Hp == 69, "合計数字と最終HP");
        }
        field.BeginBattle(openings, "途中打ち切り", 1);
        var interrupted = range.Beats[2];
        field.ShowTurnTickBeat(field.FindPawn(3), interrupted, 1);
        field.BeginBattle(openings, "再開", 1);
        await Wait(0.8);
        Require(field.TurnTickBeatPlays == 0 && field.TurnTickNumberPlays == 0 && field.TurnTickFirePulses == 0,
            "再開で古い火の弾け・数字・計数を残さない");
        if (OS.GetCmdlineUserArgs().Contains("--verify")) await CheckFireTickReplay();
    }

    private static void CheckTickBarriers()
    {
        BattleEvent[] script = [
            new() { Turn = 1, Kind = BattleEventKind.TurnStart },
            Tick(1, "毒"), TickHp(1, 100, 0),
            new() { Turn = 1, Kind = BattleEventKind.Death, TargetId = 1 },
            new() { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, TargetId = 3, Text = FireLevelLabels.FoeSpread },
            Tick(3, "毒"), TickHp(3, 5, 95), Tick(3, "燃焼", 1, 4), TickHp(3, 95, 0),
            new() { Turn = 1, Kind = BattleEventKind.Death, TargetId = 3 },
            new() { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 3, TargetId = 2, Text = FireLevelLabels.FoeSpread },
            Tick(2, "燃焼", 1, 4), TickHp(2, 6, 94),
            new() { Turn = 1, Kind = BattleEventKind.StatSnapshot, TargetId = 2 },
        ];
        var ranges = TurnTickPresentation.Build(script).Starts.Values.ToArray();
        Require(ranges.Length == 3 && ranges[0].End == 3 && ranges[1].End == 9 && ranges[2].Start == 11,
            "死亡→延焼を跨がず台本の位置へ返す");
        Require(!ranges[0].Beats.Single().Burn && ranges[1].Beats.Single().FirePulses == 1,
            "毒で倒れた駒の燃焼を補わず、途中死亡の刻みも補わない");
        BattleEvent[] sip = [new() { Turn = 1, Kind = BattleEventKind.TurnStart },
            Tick(1, "毒", inverse: true), TickHp(1, 3, 100, true, 2),
            new() { Turn = 1, Kind = BattleEventKind.InverseSip, ActorId = 2, TargetId = 1 }, TickHp(2, 9, 90, true, 2),
            new() { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 2, TargetId = 1, Text = FireArmorLabels.Convert },
            TickHp(1, 6, 100, true, 2), new() { Turn = 1, Kind = BattleEventKind.StatSnapshot }];
        var beat = TurnTickPresentation.Build(sip).Starts.Values.Single().Beats.Single();
        Require(beat.Numbers.Sum(n => n.Amount) == 9 && beat.FireHeal, "啜りの回復を混ぜず、火の変換は同じ拍へ");
        BattleEvent[] reaction = [new() { Turn = 1, Kind = BattleEventKind.TurnStart },
            new() { Turn = 1, Kind = BattleEventKind.FireArmor, TargetId = 1, Text = FireArmorLabels.Mend, Reaction = true },
            new() { Turn = 1, Kind = BattleEventKind.StatSnapshot }];
        Require(TurnTickPresentation.Build(reaction).Starts.Count == 0, "割り込みの癒しを束ねへ取り込まない");
    }

    private static void CheckRealTickPlans()
    {
        int roots = 0, beats = 0;
        foreach (var row in Presets.Compare.Where(p => p.F.Occupied().Any(s => s.Def.Id is "beni" or "borg" or "hiyo")))
        for (int stage = 0; stage < 5; stage++)
        {
            var result = BattleEngine.Run(BattleEngine.Materialize(row.F, 0),
                BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1), 0, verbose: true);
            var plan = TurnTickPresentation.Build(result.Events);
            foreach (var range in plan.Starts.Values)
            {
                Require(!result.Events.Skip(range.Start).Take(range.End - range.Start).Any(e =>
                    e.Kind is BattleEventKind.Death or BattleEventKind.Attack or BattleEventKind.StatusSnapshot), "実台本でも死亡・手番・写しを跨がない");
                roots += range.Beats.Sum(b => b.Roots.Count);
                beats += range.Beats.Count;
            }
        }
        Require(roots > beats && beats > 0, "実戦でも駒ごとの束ねが発生する");
        GD.Print($"FIRE_TICK_PLAN_OK roots={roots} beats={beats}");
    }

    private async Task CheckFireTickReplay()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        T Read<T>(string name) => (T)typeof(Main).GetField(name, flags)!.GetValue(main)!;
        void Call(string name, params object[] args) => typeof(Main).GetMethod(name, flags)!.Invoke(main, args);
        typeof(Main).GetField("_fastSmoke", flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", flags)!.SetValue(main, 1000.0);
        Call("ClearFormation");
        string[] keys = { "golm", "hisa", "borg", "hota", "hiyo" };
        for (int i = 0; i < keys.Length; i++) Call("DropUnit", i, keys[i]);
        var picker = Read<OptionButton>("_stagePicker");
        picker.Select(3); picker.EmitSignal(OptionButton.SignalName.ItemSelected, 3);
        Read<SpinBox>("_enemyHp").GetLineEdit().Text = "400";
        Read<SpinBox>("_enemyAttack").GetLineEdit().Text = "300";
        Read<SpinBox>("_seed").Value = 0;
        Call("StartBattle");
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < 3000 && Read<bool>("_playing"); k++) await Wait(0.025);
            Require(!Read<bool>("_playing"), "本番の束ね経路が再戦でも完走");
            var result = Read<BattleResult>("_result");
            var field = Read<BattlefieldView3D>("_battleField");
            var plan = TurnTickPresentation.Build(result.Events);
            var hitPlan = FireHitPresentation.Build(result.Events);
            var hitCues = hitPlan.Contacts.Values.SelectMany(c => c).ToArray();
            Require(field.FireHitPlays == hitCues.Length && field.FireHitNumberPlays == hitCues.Sum(c => c.Beat.Numbers.Count), "本番の着弾ごとに燃焼数字を一度だけ表示");
            Require(hitPlan.Unpaired.Count == 0, "本番の被弾燃焼は全件を台本の着弾へ結べる");
            Require(field.TurnTickBeatPlays == plan.Starts.Values.Sum(r => r.Beats.Count), "本番でも予定した駒ごとの拍だけ表示");
            Require(field.TurnTickNumberPlays == plan.Starts.Values.Sum(r => r.Beats.Sum(b => b.Numbers.Count)), "本番でも刻み数字を重ねず合算");
            Require(Read<int>("_tickPlays") == result.Events.Count(TickPresentation.IsTick), "Statusの元の出来事を一件ずつ通す");
            Require(field.FireCuePlays == result.Events.Count(e => e.Kind is BattleEventKind.FireLevel or BattleEventKind.FireArmor), "変換・癒しの通知も一件ずつ通す");
            foreach (var pawn in field.Pawns.Values)
            {
                var hp = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId
                    && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Death or BattleEventKind.Revive);
                if (hp is not null) Require(pawn.Hp == Math.Clamp(hp.HpAfter, 0, pawn.MaxHp), "本番の最終HPが台本と一致");
            }
            GD.Print($"FIRE_TICK_REPLAY_OK pass={pass} beats={field.TurnTickBeatPlays} numbers={field.TurnTickNumberPlays}");
            if (pass == 0) Call("ReplayBattle");
        }
        main.QueueFree();
        await Wait(0.1);
    }
}
