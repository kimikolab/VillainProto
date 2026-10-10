using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckHushVisuals()
    {
        int bus = BattleAudioRouting.EnsureFieldBus();
        int originalAudioEffects = AudioServer.GetBusEffectCount(bus);
        int masterEffects = AudioServer.GetBusEffectCount(0);
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening O(int id, int side, UnitDef unit, int slot) => new(id, side, unit.Id, unit.Name, slot,
                100, 100, 10, AttackPattern.Single, unit.Id == "husher", unit.Traits);
            DemoOpening[] opening = [ O(1, team, UnitCatalog.Hisa, 2), O(2, 1-team, EnemyCatalog.HusherHD15, 2),
                O(3, 1-team, EnemyCatalog.KnightGR, 0), O(4, team, UnitCatalog.Zan, 0) ];
            field.BeginBattle(opening, "沈黙と報い", 1);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var hisa = field.FindPawn(1)!; var holder = field.FindPawn(2)!; var knight = field.FindPawn(3)!;
            var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
            Require(field.HushOpeningActive && field.HushOpeningSourceCount == 1 && field.HushOpeningProgress == 0,
                "開戦は伝令を起点に、まだ戦場を染めていない状態から開始");
            await Wait(.16);
            Require(!audio.ShockAssetPlays.ContainsKey(ShockMarkSound.HushOpening), "集光中は展開音を先走らせない");
            await Capture($"hush-team{team}-speed{speed}-opening-source");
            await Wait(.55);
            var openingVoice = ((AudioStreamPlayer?[])typeof(BattleAttackAudio).GetField("_shockAssetVoices", Flags)!.GetValue(audio)!)[28];
            Require(openingVoice is { Playing: true, Stream: AudioStreamMP3, PitchScale: 1 }
                && openingVoice.Bus == "Master" && audio.ShockAssetPlays[ShockMarkSound.HushOpening] == 1,
                "波が広がるときに重力魔法を一度、原音程・こもり無しで再生");
            GD.Print($"HUSH_OPENING_AUDIO_OK seconds={openingVoice!.Stream.GetLength():0.000} speed={speed}");
            Require(field.HushOpeningActive && field.HushOpeningProgress is > 0 and < 1,
                "沈黙の膜が途中まで広がる拍を持つ");
            await Capture($"hush-team{team}-speed{speed}-opening-spread");
            await field.WaitForHushOpening();
            Require(!field.HushOpeningActive && field.HushOpeningProgress == 1 && holder.Hp == 100,
                "開戦の広がりが終わってから台本へ進める。HPは不変");
            await field.Attack(holder, hisa, AttackPattern.Single, new[] { hisa }, advance: false);
            Require(field.HushSlaps == 0, "砕ける前は通常の攻撃を維持");
            Require(field.HushMuted && field.HushGaugeCount == 1, "T0から沈黙を表示");
            Require(AudioServer.GetBusEffectCount(bus) == originalAudioEffects + 1, "戦場の音にこもりを一つだけ追加");
            Require(AudioServer.GetBusEffectCount(0) == masterEffects, "鎖の音が通るMasterはこもらせない");
            var chainVoices = (AudioStreamPlayer[])typeof(BattleAttackAudio).GetField("_hushVoices", Flags)!.GetValue(audio)!;
            field.ShowHisaQuiet(hisa, 0, speed);
            await field.ShowHushSeal(hisa);
            Require(chainVoices.All(v => v.Bus == "Master") && chainVoices.Any(v => v.Stream is not null),
                "粛の下でも既存の鎖の停止音をこもり無しで再生");
            for (int n = 1; n <= 15; n++)
                await field.ShowHushState(holder, n == 8 ? knight : hisa, new() { Kind = BattleEventKind.HushState,
                    Turn = 0, Text = HushStateLabels.Crack, Amount = n, Slot = 15 }, speed);
            await Capture($"hush-team{team}-speed{speed}-cracks");
            ulong began = Time.GetTicksMsec();
            int panelsBeforeShatter = field.GetChildren().OfType<PanelContainer>().Count();
            var shatter = field.ShowHushState(holder, null, new() { Kind = BattleEventKind.HushState,
                Turn = 0, Text = HushStateLabels.Shatter, Amount = 15, Slot = 15 }, speed);
            await Wait(.20);
            Require(chainVoices.Any(v => v.Playing && v.Bus == "Master"), "規定回数で既存の鎖の破裂音を再生");
            var cry = ((AudioStreamPlayer?[])typeof(BattleAttackAudio).GetField("_shockAssetVoices", Flags)!.GetValue(audio)!)[27];
            Require(cry is { Playing: true, Stream: AudioStreamMP3, PitchScale: 1 }
                && audio.ShockAssetPlays.TryGetValue(ShockMarkSound.HushCry, out int cries) && cries == 1,
                "破砕時に指定MP3の悲鳴を原音程で一度だけ再生");
            Require(((Tween?[])typeof(BattleAttackAudio).GetField("_shockAssetFades", Flags)!.GetValue(audio)!)[27] is null,
                "悲鳴を倍速や途中フェードで切らない");
            GD.Print($"HUSH_CRY_OK seconds={cry!.Stream.GetLength():0.000} speed={speed}");
            Require(!field.HushMuted && field.HushChainCount == 0 && holder.HushShattered, "生存したまま鎖・膜を解除し差分を固定");
            Require(!shatter.IsCompleted && holder.Hp == 100, "後続の死亡より前に報いの拍を確保");
            Require(field.GetChildren().OfType<PanelContainer>().Count() == panelsBeforeShatter, "破砕時に別枠のカットインを追加しない");
            Require(holder.MovementPortrait == "husher_shatter_burst", "指定音声と同時に破砕の叫び姿を表示");
            await Capture($"hush-team{team}-speed{speed}-shatter");
            await shatter;
            Require(Time.GetTicksMsec() - began >= 800, "倍速でも見せ場が読める長さ");
            Require(holder.MovementPortrait is null && holder.HushShattered, "破砕の拍が終わると覆って立つ姿へ復帰");
            await field.Attack(holder, hisa, AttackPattern.Single, new[] { hisa });
            Require(field.HushSlaps == 1 && hisa.Hp == 100 && holder.Hp == 100,
                "砕けた伝令だけ平手へ切り替え、演出はHPを変更しない");
            await Capture($"hush-team{team}-speed{speed}-slap");
            await field.ShowKnightRiposte(knight, hisa, speed);
            await Capture($"hush-team{team}-speed{speed}-riposte");
            await field.ShowKnightRiposte(knight, hisa, speed);
            Require(field.KnightRipostes == 2 && field.KnightFirstRipostes == 1, "解放後の最初だけ強調");
            field.SealPawnDied(holder); holder.AnimateDeath();
            holder.AnimateRevive(); field.SealPawnRevived(holder);
            Require(!field.HushMuted && holder.HushShattered, "砕けた伝令の復帰でも封印を復元しない");
            await field.Attack(holder, hisa, AttackPattern.Single, new[] { hisa }, advance: false);
            Require(field.HushSlaps == 2, "破砕後の蘇生でも平手のまま");
            field.BeginBattle(opening, "再戦", 1);
            Require(field.HushMuted && !field.FindPawn(2)!.HushShattered && field.HushCracks == 0, "再戦で状態を初期化");
            Require(field.HushSlaps == 0, "再戦で平手の記録を初期化");
            var pending = field.ShowHushState(field.FindPawn(2), null, new() { Kind = BattleEventKind.HushState,
                Turn = 0, Text = HushStateLabels.Shatter, Amount = 15, Slot = 15 }, speed);
            field.BeginBattle(opening, "途中で再戦", 1);
            await pending;
            Require(field.HushMuted, "古い待機が新しい膜を解除しない");
            field.EndShockMarkPresentation();
            Require(!field.HushMuted && field.HushGaugeCount == 0, "終了で音と膜を復元");
            Require(!field.HushOpeningActive, "終了で開戦の波も止める");
            Require(!openingVoice.Playing, "終了で展開音も停止");
            Require(AudioServer.GetBusEffectCount(bus) == originalAudioEffects, "終了で自分の音響効果だけを解除");
            field.BeginBattle(opening, "開戦途中で再戦", 1);
            Task oldOpening = field.WaitForHushOpening();
            field.BeginBattle(opening.Where(o => o.Traits?.Contains(TraitId.Hush) != true).ToArray(), "粛なし", 1);
            await oldOpening;
            await field.WaitForHushOpening();
            Require(!field.HushMuted && !field.HushOpeningActive && field.HushOpeningSourceCount == 0,
                "粛のない次戦へ古い開戦波と待機を持ち越さない");
            await Wait(.35);
            Require(!openingVoice.Playing && !audio.ShockAssetPlays.ContainsKey(ShockMarkSound.HushOpening),
                "集光途中の再戦では古い展開音の予約も破棄");
            GD.Print($"HUSH_VISUAL_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(.2);
    }
}
