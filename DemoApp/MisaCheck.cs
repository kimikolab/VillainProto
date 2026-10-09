using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 羽の台本対応、増減、左右、倍速、実戦と再生し直しを同じ演出口で確かめる。
public partial class MisaCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            CheckTimeline();
            await CheckFeathers();
            if (OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                await CheckReplay("試遊・標 ボス台", 0);
                await CheckReplay("試遊・標 道中", 1);
            }
            GD.Print("MISA_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void CheckTimeline()
    {
        int chased = 0, sprayed = 0, flowed = 0, friendly = 0, lost = 0;
        foreach (var row in Presets.Playtest.Where(x => x.Name.Contains("標")))
        for (int stage = 0; stage < 5; stage++)
        for (int seed = 0; seed < 4; seed++)
        {
            var result = BattleEngine.Run(BattleEngine.Materialize(row.F, 0),
                BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1), seed, verbose: true);
            var plan = MisaPresentation.Build(result.Events);
            Require(plan.HitsByCue.Count == result.Events.Count(e => e.Kind == BattleEventKind.Feather
                && e.Text is FeatherLabels.Chase or FeatherLabels.Flow or FeatherLabels.Spray), "全発に的がある");
            foreach (var (cueIndex, hitIndex) in plan.HitsByCue)
            {
                var cue = result.Events[cueIndex]; var hit = result.Events[hitIndex];
                Require(hitIndex > cueIndex && hit.ActorId == cue.ActorId && hit.TargetId is not null,
                    "同じ1発の的を参照");
                if (cue.Text == FeatherLabels.Spray)
                {
                    sprayed++;
                    Require(hit.Kind is BattleEventKind.Damage or BattleEventKind.Parry, "乱射はAttackを作らない");
                    if (hit.FriendlyFire) friendly++;
                }
                else
                {
                    chased++;
                    Require(plan.Attacks.Contains(hitIndex), "追尾で二重斬撃を出さない");
                    if (cue.Text == FeatherLabels.Flow) flowed++;
                }
            }
            lost += plan.LosingSprays.Count;
        }
        Require(chased > 0 && sprayed > 0 && flowed > 0 && friendly > 0 && lost > 0,
            $"陽性対照 chase={chased} spray={sprayed} flow={flowed} friendly={friendly} lost={lost}");
        BattleEvent[] boundary = [
            new() { Turn = 1, Kind = BattleEventKind.Feather, ActorId = 1, Text = FeatherLabels.Spray },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 3, Relayed = true },
            new() { Turn = 1, Kind = BattleEventKind.Parry, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Feather, ActorId = 1, Text = FeatherLabels.Chase },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 4, TargetId = 5 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 },
        ];
        var bounded = MisaPresentation.Build(boundary);
        Require(bounded.HitsByCue.Count == 1 && bounded.HitsByCue[0] == 2,
            "肩代わりや次の別攻撃の的を借りない");
        BattleEvent[] burst = [
            Cue(FeatherLabels.Chase, 1),
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Death, ActorId = 1, TargetId = 2 },
            Cue(FeatherLabels.Flow, 2),
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 3 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 3 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 4, TargetId = 3 },
            Cue(FeatherLabels.Chase, 3),
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 3 },
        ];
        var rapid = MisaPresentation.Build(burst);
        Require(rapid.FastEvents.SetEquals(new[] { 1, 2, 3 }) && rapid.LastShots.SetEquals(new[] { 4, 8 }),
            "標的が倒れても次の羽へ間を空けず、別の通常攻撃には早送りを持ち越さない");
        GD.Print($"MISA_TIMELINE_OK chase={chased} spray={sprayed} flow={flowed} friendly={friendly} lost={lost}");
    }

    private async Task CheckFeathers()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening[] openings = [
                // 帰還する旧版の回帰検査。規定の常駐羽はShockMarkCheck --mark-loopで検証する。
                new(1, team, "tome", "見境なしのミサ", 2, 100, 100, 10, AttackPattern.Single, false, UnitCatalog.TomeMb.Traits),
                new(2, 1 - team, "knight", "標のある敵", 0, 100, 100, 10, AttackPattern.Single, false),
                new(3, 1 - team, "axeman", "次の標", 4, 100, 100, 10, AttackPattern.Single, false),
                new(4, team, "sora", "味方", 4, 100, 100, 10, AttackPattern.Single, false),
            ];
            field.BeginBattle(openings, "ミサ・分離した羽の動作確認", 0);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            var misa = field.FindPawn(1)!;
            var feathers = misa.MisaFeathers!;
            await Wait(0.30);
            Require(feathers.Count == 1, "初期は1枚");
            feathers.SetCount(8);
            Require(feathers.Count == 8 && feathers.VisibleCount == 8, "在庫に応じた増加");
            await Capture($"misa-team{team}-speed{speed}-idle");
            var deployment = field.BeginMisaVolley(misa, 8, speed);
            await Wait(0.28 / speed);
            await Capture($"misa-team{team}-speed{speed}-deployment");
            await deployment;
            var positions = feathers.VisiblePositions;
            Require(positions.Max(p => p.X) - positions.Min(p => p.X) > 7
                && positions.Max(p => p.Z) - positions.Min(p => p.Z) > 3, "全羽が戦場の左右・奥行きへ散開");
            await Capture($"misa-team{team}-speed{speed}-deployed");
            var shot = field.PlayMisaShot(misa, field.FindPawn(2), Cue(FeatherLabels.Chase, 1), speed, false);
            Require(misa.MovementPortrait == "tome_control", "光線発射時の指揮立ち絵");
            await Wait(0.20 / speed);
            await Capture($"misa-team{team}-speed{speed}-attack");
            await shot;
            Require(feathers.BeamCount == 1 && feathers.LastMuzzle.DistanceTo(misa.FxPoint) > 4
                && feathers.LastMuzzle.DistanceTo(field.FindPawn(2)!.FxPoint) > 1.0f,
                "ミサと標的から離れた羽先から光線を発射");
            feathers.SetCount(9);
            await field.PlayMisaShot(misa, field.FindPawn(3), Cue(FeatherLabels.Flow, 2), speed, false);
            await Wait((MisaFeathers3D.DeploySeconds + 0.07) / speed);
            Require(feathers.InFlight == 0 && feathers.VisibleCount == 9 && feathers.IsDeployed,
                "連射中の増加と全域展開の維持");
            await field.PlayMisaShot(misa, field.FindPawn(4), Cue(FeatherLabels.Spray, 3), speed, true);
            await Wait(0.14 / speed);
            Require(feathers.VisibleCount == 8, "味方への乱射も着弾で消える");
            feathers.ConfirmLoss(8);
            Require(feathers.Count == 8 && feathers.VisibleCount == 8, "減少後の在庫と描画の一致");
            await field.BeginMisaVolley(misa, 1, speed);
            await field.PlayMisaShot(misa, field.FindPawn(2), Cue(FeatherLabels.Spray, 1), speed, false);
            await Wait(0.85 / speed);
            Require(feathers.Count == 1 && feathers.VisibleCount == 1 && feathers.InFlight == 0 && !feathers.IsDeployed
                && feathers.VisiblePositions.Single().DistanceTo(misa.FxPoint) < 2.6f,
                "減少通知の無い最後の1枚は手元に帰還");
            Require(misa.MovementPortrait is null, "射出後に通常絵へ戻る");
            Require(field.Pawns.Values.All(p => p.Hp == 100 && p.Position.DistanceTo(p.Home) < 0.01f),
                "演出はHPや席を変えない");
            Require(field.MisaShots == 4 && field.MisaSprays == 2 && field.MisaFlows == 1, "発数は一度ずつ");
            await field.BeginMisaVolley(misa, 8, speed);
            ulong first = 0;
            for (int i = 1; i <= 8; i++)
            {
                await field.PlayMisaShot(misa, field.FindPawn(i % 2 == 0 ? 2 : 3), Cue(FeatherLabels.Chase, i), speed, false);
                if (i == 1) first = Time.GetTicksUsec();
            }
            double burstSeconds = (Time.GetTicksUsec() - first) / 1_000_000.0;
            Require(burstSeconds * speed < 0.70, "8発を1倍換算0.7秒未満で重ね撃ち");
            Require(feathers.BeamCount == 12, "高速連射でも全発を描画");
            GD.Print($"MISA_BURST_OK team={team} speed={speed} firstToLast={burstSeconds:F3}s");
            await Capture($"misa-team{team}-speed{speed}-burst");
            feathers.SetCount(5);
            field.PlayDeath(misa);
            Require(!feathers.Visible, "死亡で羽を隠す");
            misa.SetHp(50); field.RevivePawn(misa);
            Require(feathers.Visible && feathers.Count == 5, "蘇生で在庫を保持");
            misa.AnimateVictory();
            Require(feathers.Visible == (team != 0), "味方の勝利絵の羽と二重描画しない");
            field.BeginBattle(openings, "", 0);
            misa = field.FindPawn(1)!; feathers = misa.MisaFeathers!;
            misa.AnimationSpeed = speed;
            await field.BeginMisaVolley(misa, 1, speed);
            shot = field.PlayMisaShot(misa, field.FindPawn(2), Cue(FeatherLabels.Chase, 1), speed, false);
            field.BeginBattle(openings, "", 0);
            await shot;
            Require(field.MisaShots == 0 && field.FindPawn(1)!.MisaFeathers!.Count == 1,
                "再開時に旧在庫と射出を持ち越さない");
            var interrupted = field.BeginMisaVolley(field.FindPawn(1), 8, speed);
            field.BeginBattle(openings, "", 0);
            await interrupted;
            Require(!field.FindPawn(1)!.MisaFeathers!.IsDeployed, "展開中の再戦でも旧演出が戻らない");
            GD.Print($"MISA_FEATHERS_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(0.1);
    }

    private async Task CheckReplay(string name, int stage)
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        await Wait(0.15);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Read(string key) => typeof(Main).GetField(key, flags)!.GetValue(main);
        typeof(Main).GetField("_speed", flags)!.SetValue(main, 4.0);
        var formation = Presets.Playtest.First(x => x.Name == name).F;
        var enemy = EnemyCatalog.PlaytestStages[stage];
        typeof(Main).GetMethod("EnterBattle", flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), BattleEngine.MaterializeEnemy(enemy.Enemy, enemy.Scale),
            stage == 0 ? 0 : 4, stage, name });
        var result = (BattleResult)Read("_result")!;
        var plan = MisaPresentation.Build(result.Events);
        var field = (BattlefieldView3D)Read("_battleField")!;
        for (int replay = 0; replay < 2; replay++)
        {
            if (replay != 0) typeof(Main).GetMethod("ReplayBattle", flags)!.Invoke(main, null);
            var timings = new Dictionary<int, List<ulong>>();
            ulong deadline = Time.GetTicksMsec() + 60_000;
            while ((bool)Read("_playing")! && Time.GetTicksMsec() < deadline)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                foreach (var pawn in field.Pawns.Values.Where(p => p.MisaFeathers is not null))
                {
                    if (!timings.TryGetValue(pawn.InstanceId, out var times)) timings[pawn.InstanceId] = times = new();
                    if (pawn.MisaFeathers!.VolleyBeamCount > times.Count) times.Add(Time.GetTicksUsec());
                }
            }
            Require(!(bool)Read("_playing")!, "実台本が完走");
            Require(field.MisaShots == plan.HitsByCue.Count && field.MisaShots > 0, "実台本の全発を再生");
            var intervals = new List<double>();
            foreach (var group in plan.HitsByCue.Keys.GroupBy(i => result.Events[i].ActorId!.Value))
            {
                var cues = group.ToArray();
                var times = timings[group.Key];
                Require(times.Count == cues.Length, "全光線の発射時刻を観測");
                for (int i = 1; i < cues.Length; i++)
                {
                    // 反撃など独立した攻撃演出を含まない、通常の羽の間隔を測る。
                    bool plain = Enumerable.Range(cues[i - 1] + 1, cues[i] - cues[i - 1] - 1).All(j =>
                        plan.FastEvents.Contains(j) && result.Events[j].Kind is
                            BattleEventKind.Attack or BattleEventKind.Damage or BattleEventKind.Scar or BattleEventKind.MarkLayer
                        && !result.Events[j].Reaction && !result.Events[j].Relayed && result.Events[j].ShareFromId is null
                        && result.Events[j].DeflectFromId is null);
                    if (plain) intervals.Add((times[i] - times[i - 1]) / 1_000_000.0 * 4);
                }
            }
            if (intervals.Count > 0)
            {
                Require(intervals.Average() < 0.12, "実再生でも攻撃・ダメージ表示の待ちが発射間隔へ積まれない");
                GD.Print($"MISA_REPLAY_TEMPO_OK stage={stage} replay={replay} pairs={intervals.Count} meanAt1x={intervals.Average():F3}s");
            }
            foreach (var group in result.Events.Where(e => e.TargetId is not null
                && e.Kind is BattleEventKind.Heal or BattleEventKind.Damage or BattleEventKind.Death or BattleEventKind.Revive or BattleEventKind.Scar)
                .GroupBy(e => e.TargetId))
            {
                int expected = Math.Max(0, group.Last().HpAfter);
                int? actual = field.FindPawn(group.Key)?.Hp;
                if (actual != expected)
                    foreach (var (e, i) in result.Events.Select((e, i) => (e, i)).Where(x => x.e.TargetId == group.Key).TakeLast(10))
                        GD.Print($"MISA_HP_TRACE #{i} {e.Kind} actor={e.ActorId} target={e.TargetId} hp={e.HpAfter} amount={e.Amount} text={e.Text}");
                Require(actual == expected, $"HPは台本の最終値と一致 id={group.Key} actual={actual} expected={expected}");
            }
            foreach (var pawn in field.Pawns.Values.Where(p => p.MisaFeathers is not null))
            {
                int count = result.Events.LastOrDefault(e => e.Kind == BattleEventKind.Feather && e.TargetId == pawn.InstanceId
                    && e.Text is FeatherLabels.Gain or FeatherLabels.Lost)?.Amount ?? 1;
                Require(pawn.MisaFeathers!.Count == count, "最終在庫は台本の最終値と一致");
            }
            GD.Print($"MISA_REPLAY_OK stage={stage} replay={replay} shots={field.MisaShots} spray={field.MisaSprays} flow={field.MisaFlows}");
        }
        main.QueueFree(); await Wait(0.2);
    }

    private static BattleEvent Cue(string label, int shot) => new() {
        Turn = 1, Kind = BattleEventKind.Feather, ActorId = 1, Text = label, Slot = shot,
    };
    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null || DisplayServer.GetName() == "headless") return;
        string directory = arg[14..];
        DirAccess.MakeDirRecursiveAbsolute(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, "画像保存");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
