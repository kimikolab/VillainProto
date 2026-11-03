using BattleCore;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// DemoApp の「敵ウェーブ」の試遊の欄（第452期 段0）。<see cref="EnemyCatalog.PlaytestStages"/>（ボス・精鋭）の後ろに、
/// <b>作戦マップの敵部隊</b>を単発で当てる波を足す。<b>`EnemyCatalog.PlaytestStages` 自体には足さない</b>——BattleSim の器具（`playtest291` ほか）が
/// その全部を回すので、足すと過去の器具の出力が動く。倍率は作戦マップと同じく掛けない（`EnemyScaleRule.None`）。
/// </summary>
public static class DemoPlaytestWaves
{
    public static IReadOnlyList<EnemyCatalog.PlaytestStage> All { get; } = EnemyCatalog.PlaytestStages
        .Concat(new[] { Of("band_fire"), Of("band_shields") })
        .ToArray();

    private static EnemyCatalog.PlaytestStage Of(string id)
    {
        EnemyCatalog.MapSquad m = EnemyCatalog.MapSquadOf(id);
        return new EnemyCatalog.PlaytestStage($"作戦マップ・{m.Name}", EnemyWave.Of(m.Enemy.Occupied().ToArray()), EnemyScaleRule.None);
    }
}
