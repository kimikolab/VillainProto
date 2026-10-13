using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    // 投稿画像と同じボス・糸玉の密集で、放電と本人の粉焼を別々に止めて見る。
    private async Task CheckTouPowderReadability()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        var formation = Presets.Playtest.Single(p => p.Name == "試遊・感電 光の盾 鞭").F;
        var players = BattleEngine.Materialize(formation, 0);
        var enemy = BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None);
        var units = players.Concat(enemy).ToArray();
        var opening = units.Select(u => new DemoOpening(u.InstanceId, u.TeamId, u.Def.Id, u.Name, u.Slot,
            u.Hp, u.Def.MaxHp, u.CurrentAttack, u.CurrentPattern, u.Def.Advances, u.Def.Traits)).ToArray();
        var heroId = enemy.Single().InstanceId;
        int kuguId = players.Single(u => u.Def.Id == "kugu").InstanceId;
        void Reset(int density = 20)
        {
            field.BeginBattle(opening, $"粉の演出比較：粉 {density} ／ 赤い火花", 0);
            var hero = field.FindPawn(heroId)!;
            field.SetBinding(field.FindPawn(kuguId), hero, true);
            field.ShowTouPowder(new() { Turn = 8, Kind = BattleEventKind.ShockGauge, TargetId = heroId,
                Text = ShockGaugeLabels.Dense, Amount = density }, 1);
            foreach (int slot in Enumerable.Range(0, 9).Where(s => s != 2))
                field.ShowSilkBall(new() { Turn = 8, Kind = BattleEventKind.SilkBall, ActorId = kuguId,
                    TargetId = 100 + slot, PartnerId = heroId, Team = 1, Slot = slot, Text = SilkBallLabels.Place }, 1);
        }
        Reset(); await Wait(.40);
        field.ShowHeavyPowderDischarge(new() { Turn = 8, Kind = BattleEventKind.Discharge, ActorId = heroId,
            TargetId = 100, Amount = 168 }, 1);
        var heavy = field.PowderEffects.Single();
        heavy._Process(.055);
        field.SetTouPowderClock(true, 1);
        Require(heavy.Kind == TouPowderFxKind.Heavy && field.FindPawn(heroId)!.PowderBurnBodyHeat == 0,
            "横へ走る濃い雷では本人を焼いた表示にしない");
        Require(TouPowderFx3D.HeavyWidth(168) < .07f && TouPowderFx3D.HeavyWidth(168) > TouPowderFx3D.HeavyWidth(16),
            "濃さの差は太さで保ち、短い放電が大きな塊になる太さは抑える");
        await Capture("tou-compare-discharge");
        float previousRadius = 0; int previousRays = 0;
        foreach (int density in new[] { 1, 8, 32 })
        {
            Reset(density); await Wait(.40);
            var heroPawn = field.FindPawn(heroId)!;
            field.ShowTouPowder(new() { Turn = 8, Kind = BattleEventKind.ShockGauge, ActorId = heroId, TargetId = heroId,
                Text = ShockGaugeLabels.DenseBurn, Amount = density * 8, Slot = density }, 1);
            var burn = field.PowderEffects.Single();
            var burst = burn.BurnBurst ?? throw new InvalidOperationException("粉焼の放射火花がない");
            Require(burst.Radius > previousRadius && burst.RayCount > previousRays,
                "蓄積が増すと放射火花の大きさと本数が増える");
            Require(density < 8 || burst.PulseCount > 1, "濃い粉は火花が連続して弾ける");
            previousRadius = burst.Radius; previousRays = burst.RayCount;
            burn._Process(.12);
            field.SetTouPowderClock(true, 1);
            Require(heroPawn.PowderBurnBodyHeat > .3f, "糸玉が重なっていても本人の胴と火花が光る");
            await Capture($"tou-powder-spark-density{density}");
            if (density == 32 && OS.GetCmdlineUserArgs().Contains("--powder-spark-clip"))
            {
                for (int frame = 0; frame < 15; frame++)
                {
                    await Capture($"tou-powder-spark-frame-{frame:00}");
                    field.SetTouPowderClock(false, 1); burn._Process(.04); field.SetTouPowderClock(true, 1);
                }
            }
            field.SetTouPowderClock(false, 2); await Wait(.65);
            Require(heroPawn.PowderBurnBodyHeat == 0 && field.ActivePowderEffects == 0, "粉焼と残光を終了時に消す");
            GD.Print($"TOU_POWDER_SPARK_OK density={density} radius={burst.Radius:F2} rays={burst.RayCount} pulses={burst.PulseCount}");
        }
        GD.Print("TOU_POWDER_READABILITY_OK boss_regular density=20 silk=8 discharge_vs_burn");
        field.QueueFree(); await Wait(.1);
    }
}
