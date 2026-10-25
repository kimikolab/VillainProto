using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

public enum FxCueKind { Scripted, Live }

public sealed record FxUnit(UnitDef Definition, bool Enemy)
{
    public string Key => (Enemy ? "enemy:" : "player:") + Definition.Id;
    public string Title => (Enemy ? "敵: " : "") + Definition.Name;
}

public sealed record FxCue(
    string Id, string Title, string Axis, IReadOnlyList<string> Units, FxCueKind Kind,
    Func<DemoOpening[]>? Openings = null, Func<BattleEvent[]>? Events = null,
    string? PresetName = null, int Stage = 0, int Seed = 0,
    Func<BattleEvent, bool>? Highlight = null,
    FxUnit? Subject = null, Func<Formation>? BuildFormation = null, double HoldSeconds = 0,
    int? RicaLayers = null, string? RicaIdleVariant = null)
{
    public bool Generated => Subject is not null;
}

public static partial class FxCatalog
{
    public static IReadOnlyList<FxCue> Registered { get; } = Array.AsReadOnly(new FxCue?[]
    {
        Thunder(), Storm(), Poison(), Swallow(), Dropkick(),
        RicaMotion(0, false), RicaMotion(1, false), RicaMotion(2, false), RicaMotion(3, false),
        RicaMotion(0, true), RicaMotion(1, true), RicaMotion(2, true), RicaMotion(3, true),
        RicaIdleAlternative(1), RicaIdleAlternative(2), RicaIdleAlternative(3),
        new FxCue("live.thunder", "感電 雷の型（実戦）", "台本（実戦）", new[] { "kata", "tou", "shiga" },
            FxCueKind.Live, PresetName: "試遊・感電 雷の型", Stage: 4, Seed: 0,
            Highlight: e => e.Kind is BattleEventKind.Thunder or BattleEventKind.Discharge),
        new FxCue("live.mark", "標 守り型（実戦）", "台本（実戦）", new[] { "golm", "zan", "hisa" },
            FxCueKind.Live, PresetName: "試遊・標 守り型", Stage: 1, Seed: 0,
            Highlight: e => e.Kind is BattleEventKind.GolmArm or BattleEventKind.Swallow),
    }.OfType<FxCue>().ToArray());

    private static readonly Lazy<IReadOnlyList<FxCue>> AllCues = new(() =>
        Array.AsReadOnly(Registered.Concat(AllUnits()).ToArray()));
    public static IReadOnlyList<FxCue> All => AllCues.Value;
    public static FxCue Find(string id) => Registered.FirstOrDefault(c => c.Id == id) ?? All.Single(c => c.Id == id);
    public static DemoOpening[] GetOpenings(string id, bool mirror = false) => Find(id).Openings!()
        .Select(o => mirror ? o with { Team = 1 - o.Team } : o).ToArray();

    public static bool TryGetPreset(FxCue cue, out Formation formation)
    {
        if (cue.BuildFormation is not null)
        {
            formation = cue.BuildFormation();
            return cue.Stage >= 0 && cue.Stage < EnemyCatalog.Stages.Count;
        }
        var row = Presets.Playtest.Concat(Presets.Versions).FirstOrDefault(p => p.Name == cue.PresetName);
        formation = row.F!;
        return formation is not null && cue.Stage >= 0 && cue.Stage < EnemyCatalog.Stages.Count;
    }

    public static bool IsVisibleEvent(BattleEvent e) => e.Kind is
        BattleEventKind.Attack or BattleEventKind.Thunder or BattleEventKind.Discharge or BattleEventKind.Move
        or BattleEventKind.Death or BattleEventKind.Summon or BattleEventKind.Swallow or BattleEventKind.GolmArm
        or BattleEventKind.Blast or BattleEventKind.DisarrayStage or BattleEventKind.StatusGain;

    // 各 Check の固定盤面を唯一の登録元にする。検査本体は Check 側に残す。
    internal static BattleEvent Move(int target, int slot) => new()
        { Turn = 1, Kind = BattleEventKind.Move, ActorId = 1, TargetId = target, Slot = slot };
}
