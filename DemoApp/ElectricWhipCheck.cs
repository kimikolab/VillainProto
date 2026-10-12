using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 左右・倍速・接触時刻・一時停止・途中中断と購入素材なしの描画を確認する。
public partial class ElectricWhipCheck : Control
{
    private BattlefieldView3D _field = null!;
    private string? _captureDir;
    private Node3D FxRoot => (Node3D)typeof(BattlefieldView3D).GetField("_fxRoot",
        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_field)!;
    private ElectricWhip3D Active => FxRoot.GetChildren().OfType<ElectricWhip3D>().Last(f => !f.IsQueuedForDeletion());

    public override async void _Ready()
    {
        try
        {
            _captureDir = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?[14..];
            _field = new BattlefieldView3D(); _field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_field);
            if (OS.GetCmdlineUserArgs().Contains("--preview")) await Preview();
            else await Verify();
            _field.EndShockMarkPresentation(); _field.QueueFree(); await Wait(0.1);
            GD.Print("ELECTRIC_WHIP_CHECK_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void Reset(int team = 0, double speed = 1, ElectricWhipStyle style = ElectricWhipStyle.Flurry)
    {
        _field.WhipPreviewStyle = style;
        DemoOpening O(int id, int side, UnitDef def, int slot) => new(id, side, def.Id, def.Name,
            slot, 100, 100, 10, AttackPattern.Sweep, false, def.Traits);
        _field.BeginBattle(new[] { O(1, team, UnitCatalog.Shiga, 0),
            O(2, 1-team, EnemyCatalog.Knight, 0), O(3, 1-team, EnemyCatalog.Knight, 2),
            O(4, 1-team, EnemyCatalog.Knight, 3) }, "シガ・電撃鞭", 0);
        foreach (var p in _field.Pawns.Values) p.AnimationSpeed = speed;
        _field.FindPawn(1)!.SetShocked(true);
    }

    private async Task Verify()
    {
        foreach (var style in Enum.GetValues<ElectricWhipStyle>())
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            Reset(team, speed, style); await Wait(0.12);
            var actor = _field.FindPawn(1)!;
            var hits = new[] { _field.FindPawn(2)!, _field.FindPawn(3)!, _field.FindPawn(4)! }
                .OrderBy(p => p.FxPoint.Z).ToArray();
            var times = new List<float>(); var contacts = new List<BattlePawn3D>();
            _field.AttackContact = p => contacts.Add(p);
            var attack = _field.ShowWhipSweep(actor, hits, _ => times.Add(Active.Elapsed));
            var fx = Active;
            await attack;
            Require(contacts.SequenceEqual(hits) && times.Count == 3, "対象ごとに一度、同じ順序で接触");
            for (int i = 0; i < 3; i++)
            {
                float expected = 0.64f * (0.22f + 0.55f * i / 2);
                Require(times[i] >= expected && times[i] < expected + 0.05f * speed, "既存の命中時刻");
            }
            Require(!ResourceLoader.Exists(ElectricWhip3D.SparksPath) || fx.PurchasedBursts == 3, "購入素材を命中ごとに一度");
            await Wait(0.45 / speed);
            Require(!FxRoot.GetChildren().OfType<ElectricWhip3D>().Any(), "破片まで消去");
            Require(_field.Pawns.Values.All(p => p.Hp == 100), "HPは演出から変更しない");
            GD.Print($"ELECTRIC_WHIP_TIMING_OK style={style} team={team} speed={speed} times={string.Join(',', times)}");
        }

        Reset(); await Wait(0.1);
        var shiga = _field.FindPawn(1)!; var target = _field.FindPawn(2)!;
        _field.ShowInterrupt(shiga, target, 1); shiga.WhipChainSize = 5;
        var single = _field.ShowTormentHit(shiga, target);
        var lash = Active;
        lash.UsePurchasedSparks = false;
        await Wait(0.12);
        _field.SetWhipClock(true, 1);
        float pausedAt = lash.Elapsed; var origin = shiga.WhipOrigin(FxRoot.GetViewport().GetCamera3D());
        await Wait(0.9);
        Require(lash.Elapsed == pausedAt && !single.IsCompleted && lash.Contacts == 0, "停止中は鞭も接触も進まない");
        Require(shiga.MovementPortrait == "shiga_interrupt"
            && shiga.WhipOrigin(FxRoot.GetViewport().GetCamera3D()).DistanceTo(origin) < 0.03f, "停止中も手元と差分を保つ");
        _field.SetWhipClock(false, 2);
        while (lash.Contacts == 0) await Wait(0.01);
        Require(lash.Elapsed >= 0.372f && lash.Elapsed < 0.43f, "単体の接触は0.60×0.62秒");
        Require(lash.PurchasedBursts == 0 && _field.WhipWhiteFlashes == 1, "代替描画と4連鎖以上の白閃光");
        _field.SetWhipClock(true, 2); pausedAt = lash.Elapsed;
        await Wait(0.2); Require(lash.Elapsed == pausedAt, "命中後の破片も一時停止");
        _field.SetWhipClock(false, 2); await single; await Wait(0.3);

        foreach (string cancel in new[] { "death", "end", "replay", "hide" })
        {
            Reset(); await Wait(0.06); shiga = _field.FindPawn(1)!; target = _field.FindPawn(2)!;
            var pending = _field.ShowTormentHit(shiga, target); var canceled = Active;
            await Wait(0.05);
            if (cancel == "death") _field.PlayDeath(shiga);
            else if (cancel == "end") _field.EndShockMarkPresentation();
            else if (cancel == "hide") _field.Hide();
            else Reset();
            await Wait(0.12);
            Require(pending.IsCompleted && !IsInstanceValid(canceled), "中断で待ち手と描画を解放: " + cancel);
            _field.Show();
        }
        GD.Print("ELECTRIC_WHIP_LIFECYCLE_OK");
    }

    private async Task Preview()
    {
        // PNG保存の待ち時間で高速の一振りを飛ばさない。書き出し時刻で実速へ戻せる。
        Engine.TimeScale = 0.2;
        var args = OS.GetCmdlineUserArgs();
        var styles = args.Contains("--compare") ? Enum.GetValues<ElectricWhipStyle>()
            : new[] { args.Contains("--wide") ? ElectricWhipStyle.WideSwing : ElectricWhipStyle.Flurry };
        foreach (var style in styles)
        foreach (int team in OS.GetCmdlineUserArgs().Contains("--once") ? new[] { 0 } : new[] { 0, 1 })
        foreach (bool sweep in args.Contains("--sweep-only") ? new[] { true } : new[] { true, false })
        {
            Reset(team, 0.5, style); await Wait(0.35);
            var actor = _field.FindPawn(1)!; var target = _field.FindPawn(2)!;
            if (!sweep) { _field.ShowInterrupt(actor, target, 0.5); actor.WhipChainSize = 5; }
            var attack = sweep ? _field.ShowWhipSweep(actor,
                new[] { target, _field.FindPawn(3)!, _field.FindPawn(4)! }, p => p.AnimateHit())
                : _field.ShowTormentHit(actor, target);
            var fx = Active;
            int frame = 0;
            string prefix = $"{(style == ElectricWhipStyle.Flurry ? "flurry" : "wide")}-team{team}-{(sweep ? "sweep" : "single")}";
            var timings = new List<string>();
            while (IsInstanceValid(fx) && !fx.IsQueuedForDeletion())
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (_captureDir is not null)
                {
                    timings.Add(fx.Elapsed.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Require(GetViewport().GetTexture().GetImage().SavePng($"{_captureDir}/{prefix}-{frame++:D3}.png") == Error.Ok, "プレビュー保存");
                }
            }
            if (_captureDir is not null)
                System.IO.File.WriteAllLines($"{_captureDir}/{prefix}-times.txt", timings);
            await attack; await Wait(0.25);
        }
        Engine.TimeScale = 1;
    }

    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
