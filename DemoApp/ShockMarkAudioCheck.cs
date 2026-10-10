using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 指定音源の復号、連射、鞭の2音・雷の単音、羽の帰還・消失・中断を実際の演出口で確認する。
public partial class ShockMarkAudioCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            // 戦場用の共有バスは常設。左右定位用の一時バスだけを解放数で検査する。
            BattleAudioRouting.EnsureFieldBus();
            int initialBuses = AudioServer.BusCount;
            var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
            var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
            AudioStream Sound(string file) => (AudioStream)typeof(BattleAttackAudio).GetMethod("LoadSound", Flags)!
                .Invoke(audio, new object[] { "res://assets/audio/se/" + file })!;
            bool Playing(string file) => audio.GetChildren().OfType<AudioStreamPlayer>().Any(v => v.Playing && v.Stream == Sound(file));
            int Count(ShockMarkSound cue) => audio.ShockAssetPlays.GetValueOrDefault(cue);
            if (OS.GetCmdlineUserArgs().Contains("--mark-mix"))
            {
                await CheckMarkMix(field, audio);
                field.QueueFree(); await Wait(.1);
                GD.Print("MARK_AUDIO_MIX_OK"); GetTree().Quit(); return;
            }
            foreach (string file in BattleAttackAudio.ShockMarkAssetFiles)
            {
                var stream = Sound(file);
                Require(stream.GetLength() > 0.02, "指定音源の復号: " + file);
                using var playback = stream.InstantiatePlayback();
                playback.Start();
                var samples = playback.MixAudio(1, (int)(AudioServer.GetMixRate() * 0.25));
                float peak = samples.Length == 0 ? 0 : samples.Max(s => Math.Max(Math.Abs(s.X), Math.Abs(s.Y)));
                Require(peak > 0.001, "短い再生尺の頭にも実音がある: " + file);
                playback.Stop();
                GD.Print($"SHOCK_AUDIO_FILE_OK {file} seconds={stream.GetLength():F3} headPeak={peak:F3}");
            }
            int baseVoices = audio.GetChildren().OfType<AudioStreamPlayer>().Count();
            void Reset(int team, double speed)
            {
                DemoOpening[] opening = [
                    // 帰還音は旧版の羽で検証。規定の常駐羽には帰還音を出さない。
                    new(1, team, "tome", "ミサ", 2, 100, 100, 10, AttackPattern.Single, false, UnitCatalog.TomeMb.Traits),
                    new(2, team, "shiga", "シガ", 0, 100, 100, 10, AttackPattern.Single, false, UnitCatalog.Shiga.Traits),
                    new(3, team, "kata", "カタ", 4, 100, 100, 10, AttackPattern.Single, false, UnitCatalog.Kata.Traits),
                    new(4, 1 - team, "knight", "標的", 0, 100, 100, 10, AttackPattern.Single, false),
                    new(5, 1 - team, "axeman", "次の標的", 4, 100, 100, 10, AttackPattern.Single, false),
                ];
                field.BeginBattle(opening, "指定SE・連射と重ね音", 0);
                foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            }
            BattleEvent Cue(int slot, string label = FeatherLabels.Chase) => new() {
                Turn = 1, Kind = BattleEventKind.Feather, ActorId = 1, Slot = slot, Amount = 8, Text = label };
            foreach (int team in new[] { 0, 1 })
            foreach (double speed in new[] { 1.0, 2.0 })
            {
                Reset(team, speed); await Wait(0.12);
                var misa = field.FindPawn(1)!; var target = field.FindPawn(4)!;
                await field.BeginMisaVolley(misa, 8, speed);
                Require(Count(ShockMarkSound.Deploy) == 1, "全域展開は1回");
                for (int shot = 1; shot <= 8; shot++)
                    await field.PlayMisaShot(misa, target, Cue(shot), speed, false);
                Require(Count(ShockMarkSound.Beam) == 8 && Count(ShockMarkSound.BeamHit) == 8,
                    "高速連射でも発射と着弾を全発ぶん鳴らす");
                Require(audio.MisaBeamVariants.Distinct().Count() == 3, "直近3発で指定の3音を使う");
                Require(Playing("misa_beam_hit.wav") && audio.MisaBeamVariants.Any(Playing), "発射と着弾が重なる");
                await Wait(0.30 / speed);
                Require(Count(ShockMarkSound.FeatherMove) == 1, "最後の羽を撃った後に帰還音");
                misa.MisaFeathers!.ConfirmLoss(8);
                Require(Count(ShockMarkSound.FeatherMove) == 1, "在庫確定で帰還音を二重再生しない");

                await field.BeginMisaVolley(misa, 8, speed);
                await field.PlayMisaShot(misa, field.FindPawn(5), Cue(1, FeatherLabels.Flow), speed, false);
                Require(Count(ShockMarkSound.FeatherMove) == 2, "次の標的へ旋回するときに移動音");
                await field.PlayMisaShot(misa, target, Cue(2, FeatherLabels.Spray), speed, true);
                Require(Count(ShockMarkSound.FeatherLost) == 0, "羽が消える前は破砕音を出さない");
                await Wait(0.15 / speed);
                Require(Count(ShockMarkSound.FeatherLost) == 1, "消失の粒子と同時に破砕音");

                Reset(team, speed); await Wait(0.05);
                var shiga = field.FindPawn(2)!; target = field.FindPawn(4)!;
                field.ShowInterrupt(shiga, target, speed);
                var whip = field.Attack(shiga, target, AttackPattern.Single, new[] { target }, reaction: true, advance: false);
                await Wait(0.06 / speed);
                Require(Count(ShockMarkSound.ElectricWhip) == 0, "鞭は振り始めではなく接触で鳴る");
                for (int k = 0; k < 100 && Count(ShockMarkSound.ElectricWhip) == 0; k++) await Wait(0.01);
                Require(Count(ShockMarkSound.ElectricWhip) == 1 && Playing("shiga_electric_whip.mp3")
                    && Playing("shiga_electric_hit.wav"), "鞭と電撃が接触で同時に鳴る");
                await whip;
                await field.ShowWhipSweep(shiga, new[] { target, field.FindPawn(5)! });
                Require(Count(ShockMarkSound.ElectricWhip) == 2, "薙ぎでも1振りに1組");
                audio.StopAll();
                var kata = field.FindPawn(3)!; kata.SetThundercloud(8);
                field.StrikeThunder(null, target, 1, 2, speed, kata);
                Require(Count(ShockMarkSound.ThunderHeavy) == 1 && Playing("kata_thunder_heavy_4.mp3")
                    && audio.GetChildren().OfType<AudioStreamPlayer>().Count(v => v.Playing) == 1,
                    "大落雷は雷魔法4だけを再生する");
                Require(!Playing("kata_thunder.mp3"), "大落雷に従来の主雷音を重ねない");
                field.StrikeThunder(target, field.FindPawn(5), 2, 2, speed, kata);
                Require(Count(ShockMarkSound.ThunderHeavy) == 1, "跳ねる段で大落雷を重複させない");
                Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(v => !v.Playing || v.PitchScale == 1 || v.Stream == Sound("kata_discharge.mp3")),
                    "指定音源の音程は倍速でも変えない");
                Require(audio.GetChildren().OfType<AudioStreamPlayer>().Count() <= baseVoices + 6 + BattleAttackAudio.ShockMarkAssetVoiceLimit,
                    "指定音源の再生枠は固定上限内");

                // 発射待ち・消失待ちの最中に死亡・再戦しても、新しい音を持ち越さない。
                misa = field.FindPawn(1)!;
                await field.BeginMisaVolley(misa, 8, speed);
                await field.PlayMisaShot(misa, target, Cue(1, FeatherLabels.Spray), speed, true);
                field.PlayDeath(misa);
                int lost = Count(ShockMarkSound.FeatherLost), moved = Count(ShockMarkSound.FeatherMove);
                await Wait(0.3 / speed);
                Require(Count(ShockMarkSound.FeatherLost) == lost && Count(ShockMarkSound.FeatherMove) == moved, "死亡後は消失・帰還音なし");
                Reset(team, speed); await Wait(0.05);
                misa = field.FindPawn(1)!; target = field.FindPawn(4)!;
                await field.BeginMisaVolley(misa, 8, speed);
                var pending = field.PlayMisaShot(misa, target, Cue(1), speed, false);
                Reset(team, speed); await pending; await Wait(0.3);
                Require(audio.ShockAssetPlays.Count == 0 && audio.GetChildren().OfType<AudioStreamPlayer>().All(v => !v.Playing),
                    "再戦で発射予約とフェードを破棄し全音停止");
                GD.Print($"SHOCK_AUDIO_TIMING_OK team={team} speed={speed}");
            }
            // 戦闘終了と画面離脱でも、残っている長い音を止める。
            audio.PlayShockMark(ShockMarkSound.ThunderHeavy);
            field.EndShockMarkPresentation(); await Wait(0.05);
            Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(v => !v.Playing), "戦闘終了で停止");
            audio.PlayShockMark(ShockMarkSound.Deploy);
            field.Hide(); await Wait(0.05);
            Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(v => !v.Playing), "画面離脱で停止");
            field.QueueFree(); await Wait(0.1);
            Require(AudioServer.BusCount == initialBuses, "左右定位用のバスを片付ける");
            GD.Print("SHOCK_MARK_AUDIO_CHECK_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
