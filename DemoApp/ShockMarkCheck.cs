using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 台本の5プリセットと再戦、左右・倍速・消去を実際の演出口で検証する。
public partial class ShockMarkCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            CheckSounds();
            CheckZanPlan();
            bool web = OS.GetCmdlineUserArgs().Contains("--web");
            bool rally = OS.GetCmdlineUserArgs().Contains("--rally");
            bool loop = OS.GetCmdlineUserArgs().Contains("--mark-loop");
            bool hisa = OS.GetCmdlineUserArgs().Contains("--hisa");
            bool hush = OS.GetCmdlineUserArgs().Contains("--hush");
            bool doha = OS.GetCmdlineUserArgs().Contains("--doha");
            if (!OS.GetCmdlineUserArgs().Contains("--replay-only"))
            {
                if (doha) { await CheckDohaPortraits(); await CheckDohaBatches(); }
                else if (hush) await CheckHushVisuals();
                else if (hisa) await CheckHisaVisuals();
                else if (loop) await CheckMarkLoopVisuals();
                else if (OS.GetCmdlineUserArgs().Contains("--mark-readability")) await CheckMarkReadability();
                else if (OS.GetCmdlineUserArgs().Contains("--zan-tiers")) await CheckZanTiers();
                else if (rally) await CheckRallyVisuals();
                else if (web) await CheckWebVisuals();
                else await CheckVisuals();
            }
            if (doha && OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                for (int stage = 0; stage < 3; stage++) await Replay("試遊・標 守り型", stage, 0);
                // 添付ログと同じ勇者候補・審問官・槍騎兵を含む第五波でも、ゴルムとの連鎖を追う。
                await Replay("試遊・標 守り型", 4, 0, campaign: true);
            }
            else if (OS.GetCmdlineUserArgs().Contains("--mark-readability") && OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                await Replay("試遊・標 守り型", 2, 7);
                await Replay("試遊・標 三人組", 1, 0);
            }
            else if (hush && OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                foreach (string preset in new[] { "試遊・標 ボス台", "標経済 (ヒサ×ザン×ミサ)", "試遊・標 三人組" })
                    await Replay(preset, 1, 0, campaign: true);
            }
            else if (hisa && OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                foreach (string preset in new[] { "試遊・標 循環", "試遊・標 三人組" })
                    for (int stage = 0; stage < 3; stage++) await Replay(preset, stage, 0);
                await Replay("試遊・標 三人組", 1, 0, campaign: true);
                await Replay("試遊・標 三人組", 2, 5);
            }
            else if (loop && OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                foreach (string preset in new[] { "試遊・標 循環", "試遊・標 三人組", "試遊・標 守り型" })
                    for (int stage = 0; stage < 3; stage++) await Replay(preset, stage, 0);
            }
            else if (rally && OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                foreach (string preset in new[] { "試遊・標 循環", "試遊・標 三人組", "試遊・標 守り型" })
                    for (int stage = 0; stage < 2; stage++) await Replay(preset, stage, 0);
                await Replay("試遊・標 三人組", 1, 2);
                await Replay("試遊・標 守り型", 1, 7);
            }
            else if (web && OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                foreach (string preset in new[] { "試遊・感電 糸", "試遊・感電 雷の型" })
                {
                    await Replay(preset, 0, 0, campaign: true);
                    for (int stage = 0; stage < 3; stage++) await Replay(preset, stage, 0);
                }
            }
            else if (OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                await Replay("試遊・感電 火の型", 1, 0);
                await Replay("試遊・感電 雷の型", 1, 0);
                await Replay("試遊・感電 糸", 0, 0);
                await Replay("試遊・標 ボス台", 0, 0);
                await Replay("試遊・標 道中", 1, 4);
            }
            // 大量の演出を破棄した直後にMonoを終了させず、Resourceの解放をGodotへ流す。
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Wait(0.3);
            GD.Print("SHOCK_MARK_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void CheckSounds()
    {
        var audio = new BattleAttackAudio(); AddChild(audio);
        int existingVoices = audio.GetChildren().OfType<AudioStreamPlayer>().Count();
        foreach (var cue in Enum.GetValues<ShockMarkSound>())
        {
            audio.PlayShockMark(cue);
            Require(audio.GetChildren().OfType<AudioStreamPlayer>().Any(p => p.Stream?.GetLength() > 0), "専用SEを生成して再生");
        }
        Require(audio.GetChildren().OfType<AudioStreamPlayer>().Count() <= existingVoices + 6 + BattleAttackAudio.ShockMarkAssetVoiceLimit,
            "合成SEと指定音源それぞれの同時発音を制限");
        foreach (var cue in Enum.GetValues<ZanSound>()) audio.PlayZan(cue);
        var zanVoices = (System.Collections.Generic.Dictionary<ZanSound, AudioStreamPlayer>)
            typeof(BattleAttackAudio).GetField("_zanVoices", Flags)!.GetValue(audio)!;
        Require(zanVoices.Count == 4 && zanVoices.Values.All(p => p.Stream is AudioStreamMP3 && p.Stream.GetLength() > 0),
            "ザンの指定MP3を4種類とも読み込む（仮音ではない）");
        for (int i = 0; i < 4; i++) audio.PlayZan(ZanSound.Slash);
        Require(zanVoices.Count == 4 && zanVoices.Values.All(p => p.MaxPolyphony == 2 && p.PitchScale == 1),
            "連打しても音程と同時発音上限を保つ");
        foreach (var cue in zanVoices)
            GD.Print($"ZAN_SOUND_OK {cue.Key} seconds={cue.Value.Stream.GetLength():0.000}");
        audio.StopAll();
        Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(p => !p.Playing), "終了時にSEを止める");
        audio.QueueFree();
        GD.Print("SHOCK_MARK_SOUNDS_OK");
    }

    private async Task CheckVisuals()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening O(int id, int side, UnitDef unit, int slot) => new(id, side, unit.Id, unit.Name, slot,
                100, 100, 10, AttackPattern.Single, false, unit.Traits);
            DemoOpening[] opening = [ O(1, team, UnitCatalog.Tome, 2), O(2, team, UnitCatalog.Shiga, 0),
                O(3, team, UnitCatalog.Kata, 4), O(4, team, UnitCatalog.Tou, 3), O(5, team, UnitCatalog.Kugu, 1),
                new(6, 1 - team, "knight", "標のある敵", 0, 100, 100, 10, AttackPattern.Single, false),
                new(7, 1 - team, "axeman", "次の標", 2, 100, 100, 10, AttackPattern.Single, false) ];
            field.BeginBattle(opening, "感電・標 演出確認", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var misa = field.FindPawn(1)!; var shiga = field.FindPawn(2)!; var kata = field.FindPawn(3)!;
            var tou = field.FindPawn(4)!; var kugu = field.FindPawn(5)!; var enemy = field.FindPawn(6)!;
            await Wait(0.2);
            field.ShowCharge(shiga, 4, true, speed); field.ShowCloud(kata, 8, speed);
            field.ShowMarkLayer(enemy, 5, speed); field.ShowScar(enemy, 70, 50, speed);
            enemy.SetHp(70);
            Require(enemy.MaxHp == 70 && Math.Abs(enemy.Hud.ScarFraction - 0.3f) < 0.001f, "回復しても欠損が残る");
            shiga.SetShocked(false);
            Require(shiga.StoredCharge == 4, "感電が消えても蓄電は残る");
            misa.MisaFeathers!.SetCount(8); await field.BeginMisaVolley(misa, 8, speed);
            await Wait(0.3 / speed);
            await Capture($"team{team}-speed{speed}-stock-cloud-mark");
            var shot = field.PlayMisaShot(misa, enemy, new() { Turn = 1, Kind = BattleEventKind.Feather,
                Text = FeatherLabels.Chase, Slot = 1, Amount = 8 }, speed, false);
            await Wait(0.20 / speed); await Capture($"team{team}-speed{speed}-beam"); await shot;
            Require(misa.MisaFeathers.BeamCount == 1, "羽から光線が出た");
            field.ShowInterrupt(shiga, enemy, speed);
            Require(shiga.InterruptWhip && shiga.MovementPortrait == "shiga_interrupt", "割り込み差分");
            var interrupt = field.Attack(shiga, enemy, AttackPattern.Single, new[] { enemy }, reaction: true);
            await Wait(0.40 / speed); await Capture($"team{team}-speed{speed}-interrupt"); await interrupt;
            field.ShowCharge(shiga, 3, false, speed);
            Require(!shiga.InterruptWhip && shiga.StoredCharge == 3, "割り込み後の消費");
            field.StrikeThunder(null, enemy, 1, 2, speed, kata);
            await Capture($"team{team}-speed{speed}-thunder");
            Require(kata.Thundercloud == 8, "落雷しても雲を消費しない");
            field.BeginPowderAttack(tou);
            var powder = field.ShowPowder(tou, null, enemy, PowderRoute.Main, speed);
            await Wait(0.10 / speed); await Capture($"team{team}-speed{speed}-powder"); await powder;
            await field.ShowPowder(tou, enemy, field.FindPawn(7), PowderRoute.Spread, speed);
            await field.ShowPowder(tou, null, shiga, PowderRoute.Leak, speed);
            field.SetBinding(kugu, enemy, true); await Wait(0.25 / speed);
            field.ShowThreadDischarge(kugu, enemy, kata, false, speed);
            await Wait(0.10 / speed); await Capture($"team{team}-speed{speed}-thread");
            field.ClearBindingsFor(kugu);
            field.ShowThreadDischarge(kugu, enemy, null, true, speed);
            await Wait(0.21 / speed); await Capture($"team{team}-speed{speed}-snap");
            await Wait(0.6 / speed);
            Require(field.ActiveBindingCount == 0 && field.ThreadPlays == 2 && field.ThreadReleasePlays == 1, "解除と通電");
            Require(field.Pawns.Values.Where(p => p != enemy).All(p => p.Hp == 100), "演出からHPを減らさない");
            field.ShowCower(shiga, speed);
            field.EndShockMarkPresentation(); await Wait(0.05);
            Require(field.Pawns.Values.All(p => !p.ShockMarkActive), "終了で常駐演出を消す");
            field.BeginBattle(opening, "再開", 0);
            var pending = field.ShowPowder(field.FindPawn(4), null, field.FindPawn(6), PowderRoute.Main, speed);
            field.BeginBattle(opening, "再開", 0); await pending;
            Require(field.PowderMainPlays == 0 && field.Pawns.Values.All(p => p.StoredCharge == 0 && p.Thundercloud == 0
                && p.MarkLayers == 0 && p.Hud.ScarFraction == 0), "再戦に旧状態を持ち越さない");
            GD.Print($"SHOCK_MARK_VISUAL_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(0.2);
    }

    private async Task Replay(string name, int stage, int seed, bool campaign = false)
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
        object? Read(string key) => typeof(Main).GetField(key, Flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
        var formation = Presets.Playtest.Concat(Presets.Compare).First(p => p.Name == name).F;
        var enemy = campaign ? BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1)
            : BattleEngine.MaterializeEnemy(EnemyCatalog.PlaytestStages[stage].Enemy, EnemyCatalog.PlaytestStages[stage].Scale);
        typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), enemy, seed, stage, name });
        var result = (BattleResult)Read("_result")!;
        var field = (BattlefieldView3D)Read("_battleField")!;
        int Count(BattleEventKind kind, string? label = null) => result.Events.Count(e => e.Kind == kind && (label is null || e.Text == label));
        for (int pass = 0; pass < 2; pass++)
        {
            for (int k = 0; k < 1200 && (bool)Read("_playing")!; k++) await Wait(0.05);
            Require(!(bool)Read("_playing")!, "実戦の再生完走");
            Require(field.CommandPlays == Count(BattleEventKind.Command)
                && field.CommandBallPlays == Count(BattleEventKind.CommandBall)
                && field.CoverPlays == Count(BattleEventKind.Cover), "号令・玉・庇いが台本件数と一致");
            var hisaPlan = HisaCommandPresentation.Build(result.Events);
            foreach (int missing in Enumerable.Range(0, result.Events.Count)
                .Where(i => result.Events[i].Kind == BattleEventKind.Command && !hisaPlan.Layers.ContainsKey(i)))
                GD.Print("HISA_UNMATCHED " + string.Join(" / ", result.Events.Skip(Math.Max(0, missing - 12)).Take(13)
                    .Select(e => $"{e.Kind}:{e.Text} {e.ActorId}>{e.TargetId} n={e.Amount}")));
            Require(hisaPlan.Layers.Count == Count(BattleEventKind.Command), "実戦の号令の直前の層を全件結べる");
            if (OS.GetCmdlineUserArgs().Contains("--hisa"))
            {
                // この検証の編成は全てミサ入り。敵の初回付与もMarkLayerに記録される。
                int expectedMarkSounds = result.Events.Where((e, i) => e.Kind == BattleEventKind.MarkLayer
                    && e.Amount > e.Slot && !hisaPlan.DeferredLayers.Contains(i)).Count()
                    + hisaPlan.Layers.Count;
                Require(field.MarkAddPlays == expectedMarkSounds, $"実戦の敵への標SE: {field.MarkAddPlays}/{expectedMarkSounds}");
                GD.Print($"HISA_MARK_AUDIO_OK pass={pass} plays={field.MarkAddPlays}");
            }
            Require(hisaPlan.CoverEnds.Count == Count(BattleEventKind.Cover), "実戦の庇いと被弾を全件結べる");
            if (campaign && name == "試遊・標 三人組" && stage == 1)
            {
                var openings = (System.Collections.Generic.List<DemoOpening>)Read("_battleOpening")!;
                var hushers = openings.Where(o => o.Traits?.Contains(TraitId.Hush) == true).Select(o => o.InstanceId).ToHashSet();
                foreach (var e in result.Events)
                {
                    if (e.Kind == BattleEventKind.Death && e.TargetId is int id) hushers.Remove(id);
                    if (e.Kind == BattleEventKind.HushState && e.Text == HushStateLabels.Shatter && e.ActorId is int broken)
                        hushers.Remove(broken);
                    if (hushers.Count > 0) Require(e.Kind is not (BattleEventKind.Cover or BattleEventKind.MarkRally),
                        "有効な粛が残っている間は庇いと叫びが出ない");
                }
                Require(field.QuietPlays > 0, "第2波の止められた通知で黙る");
            }
            GD.Print($"HISA_REPLAY_OK {name} stage={stage} seed={seed} campaign={campaign} pass={pass} command={field.CommandPlays} balls={field.CommandBallPlays} spill={field.CommandSpills} cover={field.CoverPlays} quiet={field.QuietPlays}");
            Require(field.HushCracks == Count(BattleEventKind.HushState, HushStateLabels.Crack)
                && field.HushShatters == Count(BattleEventKind.HushState, HushStateLabels.Shatter), "ひび・砕けが台本と一致");
            var hushPlan = HushPresentation.Build(result.Events, (System.Collections.Generic.List<DemoOpening>)Read("_battleOpening")!);
            Require(field.KnightRipostes == hushPlan.Ripostes.Count, "騎士の肩代わりを含む反撃を一度だけ再生");
            Require(!field.HushMuted && field.HushGaugeCount == 0 && !field.HushOpeningActive, "戦闘終了で粛の常駐と開戦演出を消す");
            GD.Print($"HUSH_REPLAY_OK {name} pass={pass} cracks={field.HushCracks} shatters={field.HushShatters} ripostes={field.KnightRipostes} first={field.KnightFirstRipostes}");
            Require(field.InterruptPlays == Count(BattleEventKind.ShockGauge, ShockGaugeLabels.Interrupt), "割り込み件数");
            Require(field.WhipChainPlays == Count(BattleEventKind.ShockGauge, ShockGaugeLabels.WhipChain), "連鎖鞭の件数");
            Require(field.WhipGatherStrands == result.Events.Where(e => e.Kind == BattleEventKind.ShockGauge
                && e.Text == ShockGaugeLabels.WhipChain).Sum(e => e.Slot), "弾けた数と集まる電気の本数");
            Require(field.WebSpinPlays == Count(BattleEventKind.Web, WebLabels.Spin)
                && field.WebRechargePlays == Count(BattleEventKind.Web, WebLabels.Recharge)
                && field.WebBallPlays == Count(BattleEventKind.Web, WebLabels.Ball), "網の台本件数");
            Require(field.SilkPlacePlays == Count(BattleEventKind.SilkBall, SilkBallLabels.Place)
                && field.SilkPopPlays == Count(BattleEventKind.SilkBall, SilkBallLabels.Pop)
                && field.SilkRechargePlays == Count(BattleEventKind.SilkBall, SilkBallLabels.Recharge), "糸玉の台本件数");
            var ballIds = result.Events.Where(e => e.Kind == BattleEventKind.SilkBall && e.Text == SilkBallLabels.Place)
                .Select(e => e.TargetId).ToHashSet();
            Require(field.SilkDischargePlays == result.Events.Count(e => e.Kind == BattleEventKind.Discharge
                && (ballIds.Contains(e.ActorId) || ballIds.Contains(e.TargetId))), "糸玉の往復放電");
            Require(field.ActiveWebCount == 0 && field.SilkBallCount == 0, "終了後に網と糸玉を残さない");
            Require(field.CloudPlays == Count(BattleEventKind.ShockGauge, ShockGaugeLabels.Cloud), "雷雲件数");
            Require(field.CowerPlays == Count(BattleEventKind.ShockGauge, ShockGaugeLabels.Cower), "怖気件数");
            Require(field.ChargePlays == result.Events.Count(e => e.Kind == BattleEventKind.ShockGauge
                && e.Text is ShockGaugeLabels.ChargeGain or ShockGaugeLabels.ChargeSpent or ShockGaugeLabels.ChargeDrained), "蓄電件数");
            Require(field.PowderMainPlays == result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.PowderRoute == PowderRoute.Main), "主目標への粉");
            Require(field.PowderSpreadPlays == result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.PowderRoute == PowderRoute.Spread), "隣への粉");
            Require(field.PowderLeakPlays == result.Events.Count(e => e.Kind == BattleEventKind.StatusGain && e.PowderRoute == PowderRoute.Leak), "味方への粉");
            Require(field.ThreadPlays == result.Events.Count(e => e.Kind == BattleEventKind.Discharge && e.SourceTrait == TraitId.Thread), "糸の放電件数");
            Require(field.ThreadReleasePlays == result.Events.Count(e => e.Kind == BattleEventKind.Discharge && e.SourceTrait == TraitId.Thread && e.Text == ThreadLabels.Release), "解除時の放電件数");
            Require(field.MarkLayerPlays == Count(BattleEventKind.MarkLayer) && field.ScarPlays == Count(BattleEventKind.Scar), "標と爪痕の件数");
            Require(field.MisaShots == MisaPresentation.Build(result.Events).HitsByCue.Count, "光線の発数");
            Require(field.FeatherMarkPlays == Count(BattleEventKind.FeatherMark)
                && field.FeatherMarkAllyPlays == Count(BattleEventKind.FeatherMark, FeatherMarkLabels.Ally), "標撃ちの全発と誤射");
            Require(field.FramedAccusations == Count(BattleEventKind.Framed, FramedLabels.Accuse)
                && field.FramedVendettas == Count(BattleEventKind.Framed, FramedLabels.Vendetta), "濡れ衣の対");
            Require(field.RoundStarts == Count(BattleEventKind.VendettaRound, VendettaRoundLabels.Start)
                && field.RoundSlashes == Count(BattleEventKind.VendettaRound, VendettaRoundLabels.Slash), "仇巡りの手番数と太刀数");
            Require(field.RoundFinishes == MarkLoopPresentation.Build(result.Events).LastSlashes.Count, "最後の実在する太刀で締める");
            Require(field.SharePowerPlays == Count(BattleEventKind.ShareGive, ShareGiveLabels.Power)
                && field.BeckonFeatherPlays == Count(BattleEventKind.BeckonFeather), "力配りと矢面の半減");
            var sharePlan = DohaSharePresentation.Build(result.Events);
            Require(field.SharePowerBatches == sharePlan.Ends.Values.Sum(cues => cues.Select(e => e.ActorId).Distinct().Count())
                && field.SharePowerLinks == sharePlan.Ends.Values.Sum(cues => cues.Select(e => (e.ActorId, e.TargetId)).Distinct().Count())
                && field.SharePowerAmount == result.Events.Where(e => e.Kind == BattleEventKind.ShareGive
                    && e.Text == ShareGiveLabels.Power).Sum(e => e.Amount), "束ねても相手と強化量を失わない");
            GD.Print($"DOHA_BATCH_REPLAY cues={field.SharePowerPlays} batches={field.SharePowerBatches} links={field.SharePowerLinks} amount={field.SharePowerAmount}");
            Require(field.Pawns.Values.All(p => !p.RoundMoving), "終了後に仇巡りを残さない");
            GD.Print($"MARK_LOOP_REPLAY_OK {name} stage={stage} seed={seed} pass={pass} feather={field.FeatherMarkPlays}/{field.FeatherMarkAllyPlays} framed={field.FramedAccusations}/{field.FramedVendettas} round={field.RoundStarts}/{field.RoundSlashes}/{field.RoundTravels} share={field.SharePowerPlays} beckon={field.BeckonFeatherPlays}");
            var rallyPlan = MarkRallyPresentation.Build(result.Events);
            var zanPlan = (ZanPresentation)Read("_zan")!;
            Require(field.MarkRallyCues == Count(BattleEventKind.MarkRally), "叫びの回復台本件数");
            int mergedVoices = zanPlan.Starts.Values.Sum(g => g.Rallies.GroupBy(r => r.Cues[0].ActorId).Count());
            Require(field.MarkRallyPlays == rallyPlan.Starts.Count - zanPlan.RallyStarts.Count + mergedVoices,
                "仇討ちの束の叫びだけを1声にまとめる");
            Require(rallyPlan.Starts.Count == result.TallyByUnit.Values.Sum(t => t.RallyFires - t.RallyNone),
                "engineの独立した叫び回数と一致");
            int ordinaryLights = rallyPlan.Starts.Where(p => !zanPlan.RallyStarts.Contains(p.Key)).Sum(p => p.Value.Cues.Count(e => e.Amount > 0));
            int mergedLights = zanPlan.Starts.Values.Sum(g => g.Rallies.SelectMany(r => r.Cues)
                .GroupBy(e => (e.ActorId, e.TargetId)).Count(c => c.Sum(e => e.Amount) > 0));
            Require(field.MarkRallyLights == ordinaryLights + mergedLights, "回復先ごとの光・量0では飛ばさない");
            Require(field.InsightPlays == Count(BattleEventKind.Insight), "見切りの件数");
            var zanIds = field.Pawns.Values.Where(p => p.UnitId == "zan").Select(p => (int?)p.InstanceId).ToHashSet();
            Require(field.VengeancePlays == result.Events.Count(e => e.Kind is BattleEventKind.Damage or BattleEventKind.Parry
                && e.Reaction && !e.FriendlyFire && !e.Relayed && zanIds.Contains(e.ActorId)), "仇討ちだけに予告・返り血には付けない");
            Require(field.ZanFlurries == zanPlan.Starts.Count && field.ZanHudCount == 0, "束ごとの斬撃・終了時HUD消去");
            Require(field.VengeancePlays == result.TallyByUnit.Values.Sum(t => t.VendettaFires), "仇討ち回数がengineの独立集計と一致");
            Require(field.ZanDamage == result.Events.Where(e => e.Kind == BattleEventKind.Damage
                && e.Reaction && !e.FriendlyFire && !e.Relayed && zanIds.Contains(e.ActorId)).Sum(e => e.Amount),
                "合計表示が台本のダメージと一致（通常の数字と同じくオーバーキルを含む）");
            Require(field.ZanRecoilPlays == zanPlan.Starts.Values.Count(g => g.Recoils.Count > 0), "返り血は束ごとに1回");
            GD.Print($"ZAN_REPLAY_OK {name} stage={stage} seed={seed} pass={pass} hits={field.VengeancePlays} flurries={field.ZanFlurries} damage={field.ZanDamage} blood={field.ZanRecoilPlays} links={field.ZanLinks}");
            GD.Print($"MARK_RALLY_REPLAY_OK {name} stage={stage} seed={seed} pass={pass} shout={field.MarkRallyPlays} heal={field.MarkRallyLights} insight={field.InsightPlays} guards={field.InsightGuards} vengeance={field.VengeancePlays}");
            foreach (var pawn in field.Pawns.Values)
            {
                var lastHp = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind is BattleEventKind.Damage
                    or BattleEventKind.Heal or BattleEventKind.Revive or BattleEventKind.Death or BattleEventKind.Scar);
                if (lastHp is not null) Require(pawn.Hp == Math.Max(0, lastHp.HpAfter), $"最終HP id={pawn.InstanceId}");
                var scar = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.Scar);
                if (scar is not null) Require(pawn.MaxHp == scar.Slot, "最終最大HP");
                var mark = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.MarkLayer);
                Require(pawn.MarkLayers == (mark?.Amount ?? 0), "最終標層数");
                var cloud = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.ShockGauge && e.Text == ShockGaugeLabels.Cloud);
                Require(pawn.Thundercloud == Math.Clamp(cloud?.Amount ?? 0, 0, 8), "最終雷雲の描画段階（上限8）");
                var charge = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind == BattleEventKind.ShockGauge
                    && e.Text is ShockGaugeLabels.ChargeGain or ShockGaugeLabels.ChargeSpent or ShockGaugeLabels.ChargeDrained);
                Require(pawn.StoredCharge == (charge?.Amount ?? 0), "最終蓄電");
                Require(!pawn.ShockMarkActive, "終了で常駐効果を止める");
            }
            GD.Print($"SHOCK_MARK_REPLAY_OK {name} pass={pass} interrupt={field.InterruptPlays} cloud={field.CloudPlays} powder={field.PowderMainPlays}/{field.PowderSpreadPlays}/{field.PowderLeakPlays} thread={field.ThreadPlays}/{field.ThreadReleasePlays} beam={field.MisaShots} scar={field.ScarPlays} mark={field.MarkLayerPlays}");
            GD.Print($"SHOCK_WEB_REPLAY_OK {name} campaign={campaign} stage={stage} pass={pass} web={field.WebSpinPlays}/{field.WebRechargePlays}/{field.WebBallPlays} silk={field.SilkPlacePlays}/{field.SilkPopPlays}/{field.SilkDischargePlays} chain={field.WhipChainPlays} gather={field.WhipGatherStrands} flash={field.WhipWhiteFlashes}");
            if (pass == 0) typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
        }
        main.QueueFree(); await Wait(0.2);
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null || DisplayServer.GetName() == "headless") return;
        DirAccess.MakeDirRecursiveAbsolute(arg[14..]);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(System.IO.Path.Combine(arg[14..], name + ".png")) == Error.Ok, "画像保存");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
