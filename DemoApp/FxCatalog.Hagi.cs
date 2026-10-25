using BattleCore;
using System.Collections.Generic;

public static partial class FxCatalog
{
    private static IEnumerable<FxCue> HagiCues()
    {
        string[] presets = { "試遊・ハギ 台B", "試遊・死神と墓守", "版・移動 ハギ" };
        for (int p = 0; p < presets.Length; p++)
        for (int wave = 0; wave < EnemyCatalog.PlaytestStages.Count; wave++)
            yield return new FxCue($"hagi.live.{p}.{wave}", $"{presets[p]} ／ {EnemyCatalog.PlaytestStages[wave].Name}",
                "死・ハギの大鎌", new[] { "hagi" }, FxCueKind.Live, PresetName: presets[p], Stage: wave == 0 ? 4 : 0,
                Highlight: e => e.Kind is BattleEventKind.Attack or BattleEventKind.Death, PlaytestWave: wave);
    }
}
