using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

// 実台本4本を本番の入口で完走し、受け渡し・最終HP・再戦と表示時計を確かめる。
public partial class DebuffCheck : Control
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
    private Main _main = null!;
    private T Read<T>(string name) => (T)typeof(Main).GetField(name, Flags)!.GetValue(_main)!;
    private void Call(string name, params object[] args) => typeof(Main).GetMethod(name, Flags)!.Invoke(_main, args);

    public override async void _Ready()
    {
        try
        {
            string? capture = OS.GetCmdlineUserArgs().FirstOrDefault(s => s.StartsWith("--capture-dir="))?["--capture-dir=".Length..];
            if (capture is not null) { await Capture(capture); GetTree().Quit(); return; }
            _main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(_main);
            _main.ViewerFastPlayback = true; _main.SetPlaybackSpeed(1000);
            Read<SpinBox>("_seed").Value = 0;
            var rows = Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).Concat(Presets.Versions).ToArray();
            int boss = EnemyCatalog.Stages.Count + 1 + EnemyCatalog.TestStages.Count + 1;
            foreach (string name in new[] { "試遊・呪い", "試遊・カーズ 呪い" })
            {
                int preset = Array.FindIndex(rows, r => r.Name == name);
                Require(preset >= 0, "指定の試遊台がある"); Call("ApplyPreset", preset);
                foreach (int stage in new[] { boss, 3 })
                {
                    Read<OptionButton>("_stagePicker").Select(stage); Call("StartBattle");
                    var result = Read<BattleResult>("_result");
                    string script = JsonSerializer.Serialize(result.Events);
                    string sourceScript = JsonSerializer.Serialize(_main.SourceBattleResult!.Events);
                    await Until(() => !_main.IsPlaying);
                    CheckReplay(result);
                    Require(JsonSerializer.Serialize(result.Events) == script, "台本を変更しない");
                    Require(JsonSerializer.Serialize(_main.SourceBattleResult!.Events) == sourceScript, "元のBattleCoreの台本を変更しない");
                    Call("ReplayBattle"); await Until(() => !_main.IsPlaying); CheckReplay(result);
                    GD.Print($"DEBUFF_REPLAY_OK preset={name} stage={stage} events={result.Events.Count} replay=True hp=True");
                    Call("ReturnToFormation");
                }
            }
            await CheckLayersAndClock();
            await CheckCastClock();
            await CheckKubiBell();
            await CheckCastSeek();
            await CheckUpdatedBrief();
            await CheckSeek();
            GD.Print("DEBUFF_CHECK_COMPLETE ok=True"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void CheckReplay(BattleResult result)
    {
        CheckRhythmReplay(result);
        CheckCastReplay(result);
        var field = _main.ViewerField;
        var plan = DebuffPresentation.Build(result.Events, _main.ViewerOpenings);
        bool hasWata = _main.ViewerOpenings.Any(o => o.UnitId == "wata");
        Require(hasWata ? plan.Passes.Count > 0 && plan.Costs.Count > 0 : plan.Passes.Count == 0,
            "ワタがいる台だけで渡しを描く（カーズ台はワタと入れ替え）");
        Require(field.DebuffDeliveries == plan.Passes.Count, "渡した件数を増減しない");
        Require(field.DebuffIntakes == plan.Passes.Values.Count(p => p.Intake && p.From != p.Actor), "分配は吸い込みを重ねない");
        Require(field.DebuffCosts == plan.Costs.Count, "代金を払った通知だけが傷む");
        var swings = UtsuPresentation.Build(result.Events, _main.ViewerOpenings);
        var balances = new Dictionary<int, long>(); int expectedBlades = 0;
        for (int i = 0; i < result.Events.Count; i++)
        {
            var e = result.Events[i];
            if (e.TargetId is int target && _main.ViewerOpenings.Any(o => o.InstanceId == target && o.UnitId == "utsu"))
            {
                if (e.Kind == BattleEventKind.StatusGain && e.Text == "dull") balances[target] = balances.GetValueOrDefault(target) + e.Amount;
                if (e.Kind == BattleEventKind.Whet) balances[target] = balances.GetValueOrDefault(target) - e.Amount;
            }
            if (swings.Swings.TryGetValue(i, out var swing) && balances.GetValueOrDefault(swing.Actor) > 0) expectedBlades++;
        }
        Require(expectedBlades > 0 && field.DebuffBladeStrokes == expectedBlades, "呪われた実際の接触ごとに一筋だけ刃が走る");
        Require(Read<int>("_eventIndex") == result.Events.Count, "すべての通知を消費する");
        foreach (var pawn in field.Pawns.Values)
        {
            var last = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId
                && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Revive);
            if (last is not null) Require(pawn.Hp == last.HpAfter, "最終HPが台本と一致する");
        }
    }

    private static DemoOpening[] Openings(bool mirror = false) => new DemoOpening[] {
        new(1, mirror ? 1 : 0, "wata", "渡しのワタ", 0, 84, 84, 8, AttackPattern.Single, true),
        new(2, mirror ? 1 : 0, "utsu", "逆しまのウツ", 4, 66, 66, 9, AttackPattern.Single, true),
        new(3, mirror ? 1 : 0, "nel", "呪詛官ネル", 2, 90, 90, 8, AttackPattern.Single, false),
        new(4, mirror ? 1 : 0, "mudo", "泥人形ムド", 1, 100, 100, 8, AttackPattern.Single, true),
        new(5, mirror ? 1 : 0, "kubi", "萎縮のクビ", 3, 80, 80, 8, AttackPattern.Single, false),
        new(6, mirror ? 0 : 1, "knight", "近衛騎士", 0, 300, 300, 20, AttackPattern.Single, true),
        new(7, mirror ? 0 : 1, "knight", "近衛騎士", 1, 300, 300, 20, AttackPattern.Single, true),
    };

    private async Task CheckLayersAndClock()
    {
        var field = _main.ViewerField; field.Visible = true; field.BeginBattle(Openings(), "", 0);
        var utsu = field.FindPawn(2)!; var wata = field.FindPawn(1)!;
        field.SetDeathClock(false, 1);
        void Receive(BattleEventKind kind, int amount) => Call("ReadUtsuDebuff",
            new BattleEvent { Kind = kind, Turn = 0, TargetId = 2, Text = "dull", Amount = amount }, utsu);
        // 本番と同じ索引初期化で収支を空にする。
        _main.EnterScripted(Openings(), Array.Empty<BattleEvent>(), "層の確認");
        await Until(() => !_main.IsPlaying);
        field.BeginBattle(Openings(), "", 0); utsu = field.FindPawn(2)!; wata = field.FindPawn(1)!;
        field.SetDeathClock(false, 1);
        Receive(BattleEventKind.StatusGain, 3); Require(field.UtsuDebuffLayers(2) == 2, "下げ幅3で元の刃と追加の刃");
        Receive(BattleEventKind.StatusGain, 9); Require(field.UtsuDebuffLayers(2) == 5, "下げ幅12で5本");
        Receive(BattleEventKind.StatusGain, 100); Require(field.UtsuDebuffLayers(2) == 5, "上限を超えても本数を増やさない");
        Receive(BattleEventKind.Whet, 113); Require(field.UtsuDebuffLayers(2) == 0, "強化で半減側へ渡ると全層が剥がれる");
        Receive(BattleEventKind.StatusGain, 1); Require(field.UtsuDebuffLayers(2) == 0, "弱体と強化の符号を保つ");
        Receive(BattleEventKind.StatusGain, 12); await Wait(.22);
        var pass = field.ShowDebuffPass(wata, field.FindPawn(3)!, utsu, true);
        field.SetDeathClock(true, 1);
        var effects = Descendants(field).OfType<DebuffFx3D>().ToArray();
        var ages = effects.Select(e => e.Age).ToArray(); await Wait(.12);
        Require(effects.Select(e => e.Age).SequenceEqual(ages) && !pass.IsCompleted, "停止中は糸と層と待ち手も停止");
        field.SetDeathClock(false, 2); await Until(() => pass.IsCompleted);
        await pass;
        var pulse = field.ShowDebuffPass(wata, field.FindPawn(3)!, field.FindPawn(6)!, true);
        field.BeginBattle(Openings(), "再戦", 0); await Until(() => pulse.IsCompleted); await Frame();
        Require(!Descendants(field).OfType<DebuffFx3D>().Any(), "中断・再戦で糸と層が残らない");
        utsu = field.FindPawn(2)!; field.SetUtsuDebuff(utsu, 5, false); utsu.AnimateDeath(); await Frame(); await Frame();
        Require(!Descendants(field).OfType<DebuffLayers3D>().Any(), "死亡で層を片付ける");
        field.BeginBattle(Openings(true), "反転", 0); utsu = field.FindPawn(2)!;
        field.SetUtsuDebuff(utsu, 5, false); field.ShowDebuffCost(field.FindPawn(1)!);
        await Wait(.05); Require(Descendants(field).OfType<DebuffLayers3D>().Count() == 1, "反対陣営でも同じ層");
        field.BeginBattle(Openings(), "勝利", 0); utsu = field.FindPawn(2)!; field.SetUtsuDebuff(utsu, 5, false);
        utsu.AnimateVictory(); await Frame(); await Frame();
        Require(!Descendants(field).OfType<DebuffLayers3D>().Any(), "勝利後に層を残さない");
        field.Visible = false; await Frame();
        Require(!Descendants(field).OfType<DebuffFx3D>().Any(), "編成へ戻るとすべて消える");
        GD.Print("DEBUFF_CLOCK_OK layers over whet pause speed death victory cancel mirror=True");
    }

    private async Task CheckSeek()
    {
        var events = new BattleEvent[] {
            new() { Kind = BattleEventKind.StatusGain, Turn = 0, TargetId = 2, ActorId = 3, Text = "dull", Amount = 12 },
            new() { Kind = BattleEventKind.Whet, Turn = 0, TargetId = 2, ActorId = 3, Amount = 13, AttackAfter = 4 },
            new() { Kind = BattleEventKind.TurnStart, Turn = 1 },
        };
        _main.EnterScripted(Openings(), events, "シーク確認"); await Until(() => !_main.IsPlaying);
        await _main.SeekTo(1); Require(_main.ViewerField.UtsuDebuffLayers(2) == 5, "シークで層を復元");
        _main.ResumePlayback(); await Until(() => !_main.IsPlaying);
        Require(_main.ViewerField.UtsuDebuffLayers(2) == 0, "再開後の強化で剥がれる");
        GD.Print("DEBUFF_SEEK_OK restore replay=True");
    }

    private async Task Capture(string directory)
    {
        Directory.CreateDirectory(directory);
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        field.BeginBattle(Openings(), "", 0); field.SetDeathClock(false, 1); await Wait(.5);
        field.SetUtsuDebuff(field.FindPawn(2)!, 5, false); await Wait(.22);
        var pass = field.ShowDebuffPass(field.FindPawn(1)!, field.FindPawn(3)!, field.FindPawn(2)!, true);
        await FreezeThread(1); await Save("wata-utsu.png");
        field.SetDeathClock(false, 1); await pass;
        var enemy = field.ShowDebuffPass(field.FindPawn(1)!, field.FindPawn(5)!, field.FindPawn(6)!, true);
        await FreezeThread(2); await Save("wata-enemy.png");
        field.SetDeathClock(false, 1); await enemy;
        field.SetUtsuDebuff(field.FindPawn(2)!, 0, false); await Wait(.08);
        field.SetDeathClock(true, 1); await Save("utsu-peeling.png");
        await CaptureCasts(field, Save);
        await CaptureKubiBell(field, Save);
        await CaptureUpdatedBrief(field, Save);
        async Task FreezeThread(int deliveries)
        {
            await Until(() => field.DebuffDeliveries >= deliveries);
            await Until(() => Descendants(field).OfType<DebuffThread3D>().LastOrDefault(t => !t.IsQueuedForDeletion()) is { Age: >= .13 });
            field.SetDeathClock(true, 1);
        }
        async Task Save(string name)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, name));
        }
        GD.Print($"DEBUFF_CAPTURE_OK {directory}");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Until(Func<bool> done)
    {
        ulong until = Time.GetTicksMsec() + 60000;
        while (!done() && Time.GetTicksMsec() < until) { if (_main?.ViewerPlaybackError is { } error) throw error; await Frame(); }
        Require(done(), "時間内に再生が完了");
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
