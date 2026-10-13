using BattleCore;
using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class SomCheck
{
    // 本編と同じ描画・時計で二種の識別、寝返り、投射、中断を左右と倍速で確認する。
    private async Task SpiritRoles()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        field.UsePurchasedSomFx = !OS.GetCmdlineUserArgs().Contains("--som-no-purchased");
        await Wait(.15);
        static DemoOpening Open(UnitDef d, int id, int team, int slot) => new(id, team, d.Id, d.Name,
            slot, d.MaxHp, d.MaxHp, d.Attack, d.Pattern, d.Advances, d.Traits);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 4.0 })
        {
            field.BeginBattle(new[] { Open(UnitCatalog.Som, 1, team, 4), Open(UnitCatalog.Kugu, 2, team, 0),
                Open(UnitCatalog.Kado, 3, 1 - team, 0), Open(UnitCatalog.Borg, 4, 1 - team, 2) },
                "ソム — 手番の雷人形／割り込みの雷精", 0);
            var som = field.FindPawn(1)!; var partner = field.FindPawn(2)!;
            som.AnimationSpeed = speed;
            for (int i = 0; i < 2; i++)
            {
                bool emergency = i == 1;
                field.PlaceSomBurstBeast(som, new() { Turn = 1, Kind = BattleEventKind.Summon, TargetId = 30 + i,
                    Team = 1 - team, Slot = i == 0 ? 6 : 7 }, emergency, emergency ? partner : null, 0, 1, speed);
            }
            await Wait(.22 / speed);
            var spirits = field.SomBeasts.ToArray();
            Require(spirits[0].FigureTexture != spirits[1].FigureTexture, "二種の見た目を色以外で分ける");
            await Capture($"roles-team{team}-speed{speed}-summoned");
            field.RunSomBurstBeasts(som, new[] { 30, 31 }, false); await Wait(.22 / speed);
            field.SetSomClock(true, speed);
            var ages = spirits.Select(b => b.VisualAge).ToArray();
            var positions = spirits.Select(b => b.GlobalPosition).ToArray();
            await Wait(.12);
            Require(spirits.Select(b => b.VisualAge).SequenceEqual(ages)
                && spirits.Select(b => b.GlobalPosition).SequenceEqual(positions), "姿のちらつきと二種の飛行を同じ時計で止める");
            await Capture($"roles-team{team}-speed{speed}-flying");
            field.SetSomClock(false, speed * 2); await Wait(.23 / speed);
            Require(spirits.All(b => b.Progress == 1), "二種の飛行の途中で速度変更");
            field.SetSomClock(false, speed);
            field.TakeSomStance(som, new[] { 30, 31 }, false); await Wait(.38 / speed);
            Require(spirits.All(b => b.ContractCracked && b.FigureFlipped == (team == 0)), "二種とも同じ寝返りの合図");
            await Capture($"roles-team{team}-speed{speed}-betrayed");
            foreach (int id in new[] { 30, 31 }) field.ThrowSomLightning(new() { Turn = 1, Kind = BattleEventKind.BeastThrow, ActorId = id, TargetId = 2 }, speed);
            await Wait(.16 / speed);
            Require(spirits.All(b => b.BodyScale <= .76f), "二種とも自身の電気を槍として放つ");
            await Capture($"roles-team{team}-speed{speed}-throw");
            field.EndSomPresentation(); await Wait(.10);
            Require(field.SomBeastCount == 0 && !field.SomBolts.Any(), "姿・稲妻・購入火花を中断で消す");
            GD.Print($"SOM_SPIRIT_ROLES_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(.1);
    }
}
