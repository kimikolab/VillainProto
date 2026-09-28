using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 素材の復号・音枠・間引きと、飛行の着地／死亡／停止の競合を実際の表示入口で確認する。
public partial class MovementAudioCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
            foreach (var sound in BattleAttackAudio.MovementPaths)
            {
                var stream = (AudioStream)typeof(BattleAttackAudio).GetMethod("LoadSound", Flags)!.Invoke(audio, new object[] { sound.Value })!;
                Require(stream.GetLength() > 0, "音源の復号");
                GD.Print($"MOVEMENT_AUDIO_FILE_OK {sound.Key} seconds={stream.GetLength():F3}");
            }
            for (int i = 0; i < 12; i++) audio.PlayMovementSound(MovementSound.Wind);
            Require(audio.MovementSoundPlays[MovementSound.Wind] == 1, "密集した風を間引く");
            for (int i = 0; i < 6; i++) audio.PlayAttack("sero", 0, AttackPattern.Single, false, false);
            Require(audio.MovementSoundPlays[MovementSound.Bow] == 6, "弓は毎射撃に鳴る");
            Require(audio.GetChildren().OfType<AudioStreamPlayer>().Count(v => v.Playing) <= 4, "通常SEは最大4音");
            Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(v => v.PitchScale == 1), "原音のピッチ");
            audio.StopAll();
            Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(v => !v.Playing), "停止時に全音を止める");

            void Reset(double speed)
            {
                field.BeginBattle(new DemoOpening[] {
                    new(1, 0, "hane", "ハネ", 0, 100, 100, 11, AttackPattern.Single, true),
                    new(2, 1, "knight", "標的", 0, 100, 100, 10, AttackPattern.Single, true),
                    new(3, 0, "shio", "シオ", 3, 100, 100, 10, AttackPattern.Single, false),
                    new(4, 0, "basa", "バサ", 4, 100, 100, 10, AttackPattern.Sweep, true),
                    new(5, 0, "yomi", "ヨミ", 1, 100, 100, 10, AttackPattern.Single, true),
                    new(6, 0, "sero", "セロ", 2, 100, 100, 10, AttackPattern.Single, false),
                }, "移動SE確認", 0);
                foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            }
            foreach (double speed in new[] { 1.0, 2.0 })
            {
                Reset(speed);
                var hane = field.FindPawn(1)!; var target = field.FindPawn(2)!;
                var cue = new BattleEvent { Turn = 1, Kind = BattleEventKind.Blast, ActorId = 1, TargetId = 2 };
                await field.Attack(hane, target, AttackPattern.Pierce, new[] { target }, advance: false,
                    movementCue: cue, blastDestination: 3);
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Dropkick) == 1
                    && audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Collision) == 1,
                    "手番は接触の大キックと射出の氷音を各1回");
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Windup) == 1, "ハネの射出前に高速移動音");
                // 初回描画が重い環境ではawait復帰までに着地済みのこともある。実際の高さと照合する。
                Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Landing)
                    || target.BlastVisualPosition.Y <= target.Position.Y + 0.01f, "空中では着地音を鳴らさない");
                await Wait(0.4 / speed);
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Landing) == 1, "飛行終了で着地");
                field.MoveWithCue(target, 3, cue, hane);
                Require(audio.MovementSoundPlays[MovementSound.Landing] == 1, "後続Moveで二重に鳴らさない");
                field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Retreat, ActorId = 3, TargetId = 1, PartnerId = 3 }, speed);
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Vine) == 1, "蔓の見出しで鳴る");
                await field.Attack(field.FindPawn(4), target, AttackPattern.Sweep, new[] { target }, advance: false);
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.BasaSweep) == 1, "バサの攻撃は専用の薙ぎ音");
                await field.ShowBasaShuffle(field.FindPawn(4)!, new[] {
                    new BattleEvent { Turn = 1, Kind = BattleEventKind.Move, ActorId = 4, TargetId = 2, Slot = 0 },
                    new BattleEvent { Turn = 1, Kind = BattleEventKind.Move, ActorId = 4, TargetId = 1, Slot = 1 },
                }, speed);
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Tornado) == 1, "両陣地の竜巻で1回だけ強風1");
                field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Tailwind,
                    ActorId = 4, TargetId = 1, PartnerId = 3, SpreadFromId = 2 }, speed);
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Tailwind) == 1, "追い風は強風2");
                Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Wind), "新しい風の演出に旧風音を重ねない");
                await field.Attack(field.FindPawn(6), target, AttackPattern.Pierce, new[] { target }, advance: false);
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.SeroPierce) == 1
                    && audio.MovementSoundPlays.GetValueOrDefault(MovementSound.ArrowHit) == 1, "発射と着弾を各1回");
                Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Bow), "貫通に通常の弓音を重ねない");
                for (int shot = 0; shot < 5; shot++)
                    await field.Attack(field.FindPawn(6), target, AttackPattern.Single, new[] { target }, advance: false,
                        movementCue: new BattleEvent { Turn = 1, Kind = BattleEventKind.Barrage, ActorId = 6, TargetId = 2, Slot = shot + 1 });
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.SeroBarrage) == 5
                    && field.MovementBarragePlays == 5, "連撃は各矢に音と控えめな残光を1回ずつ");
                Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Bow), "連撃に通常の弓音を重ねない");
                field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Evade, ActorId = 6 }, speed);
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Evade) == 1, "通常回避で逃走音");
                field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.LastDodge, ActorId = 6 }, speed);
                field.ShowMovementCue(new BattleEvent { Turn = 1, Kind = BattleEventKind.Evade, ActorId = 6 }, speed);
                Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Evade) == 2, "必死の回避と直後の回避で音を重ねない");
                var yomi = field.FindPawn(5)!;
                foreach (bool reaction in new[] { false, true })
                {
                    int before = audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Sheathe);
                    await field.Attack(yomi, target, AttackPattern.Single, new[] { target }, reaction: reaction, advance: false);
                    Require(yomi.MovementPortrait is null
                        || audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Sheathe) == before, "抜刀姿勢中は納刀しない");
                    await Wait(0.7 / speed);
                    Require(audio.MovementSoundPlays.GetValueOrDefault(MovementSound.Sheathe) == before + 1, "通常・追加の差分終了で納刀");
                }
                GD.Print($"MOVEMENT_AUDIO_TIMING_OK speed={speed}");
            }
            Reset(1);
            var spring = new BattleEvent { Turn = 1, Kind = BattleEventKind.Spring, ActorId = 1, TargetId = 2 };
            field.MoveWithCue(field.FindPawn(2)!, 3, spring, field.FindPawn(1));
            field.FindPawn(2)!.AnimateDeath();
            await Wait(0.4);
            Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Landing), "死亡で移動の着地予約が消える");
            Reset(1);
            field.MoveWithCue(field.FindPawn(2)!, 3, spring, field.FindPawn(1));
            audio.StopAll();
            await Wait(0.4);
            Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Landing), "停止後に着地音が復活しない");
            Reset(1);
            var arrow = field.Attack(field.FindPawn(6), field.FindPawn(2), AttackPattern.Single,
                new[] { field.FindPawn(2)! }, advance: false);
            audio.StopAll();
            await arrow;
            Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.ArrowHit), "停止後に着弾音が復活しない");
            Reset(1);
            await field.Attack(field.FindPawn(5), field.FindPawn(2), AttackPattern.Single,
                new[] { field.FindPawn(2)! }, advance: false);
            audio.StopAll();
            await Wait(0.7);
            Require(!audio.MovementSoundPlays.ContainsKey(MovementSound.Sheathe), "停止後に納刀音が復活しない");
            Reset(1);
            var blast = field.Attack(field.FindPawn(1), field.FindPawn(2), AttackPattern.Pierce,
                new[] { field.FindPawn(2)! }, advance: false,
                movementCue: new BattleEvent { Turn = 1, Kind = BattleEventKind.Blast, ActorId = 1, TargetId = 2 });
            audio.StopAll();
            await blast;
            await Wait(0.5);
            Require(audio.MovementSoundPlays.Count == 0, "溜め中の停止後に衝突・着地音が復活しない");
            GD.Print("MOVEMENT_AUDIO_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
