using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// 戦闘側の既存 CounterProbe で実値を読むだけの副台本。層や魂の規則は再実装しない。
// Board は internal のため、この読み取り境界だけで参照する。BattleCore の公開 API は変更しない。
internal sealed class DeathPresentation
{
    internal readonly record struct State(int Id, int Layers, int Lives, int Sound, int MaxHp, bool Split = false);
    private static readonly PropertyInfo Board = typeof(UnitState).GetProperty("Board", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("死軸の観測先 Board が見つかりません");
    internal readonly SortedDictionary<int, List<State>> Frames = new();
    internal readonly HagiPresentation Hagi = new();
    private readonly Dictionary<int, State> _last = new();
    private BattleContext? _context;
    internal void Observe(TraitId trait, UnitState owner, UnitState target, string key, int delta)
    {
        _context ??= (BattleContext?)Board.GetValue(target);
        if (_context is not null) Capture(_context.Events.Count, _context.AllUnits);
    }
    internal void Finish(int index)
    {
        if (_context is not null) Capture(index, _context.AllUnits);
        _context = null; // 再生に必要なのは写しだけ。計算用の盤面を保持しない。
    }
    private void Capture(int index, IReadOnlyList<UnitState> units)
    {
        if (_context is not null) Hagi.Capture(index, units, _context);
        foreach (var u in units)
        {
            var s = new State(u.InstanceId, u.RawCounter("necro"), u.RawCounter(VelSoulTrait.LivesKey),
                u.RawCounter(DirgeSoundTrait.SoundKey), u.MaxHp, u.RawCounter(VelSoulTrait.SplitKey) > 0);
            if (_last.TryGetValue(s.Id, out var old) && s == old) continue;
            _last[s.Id] = s;
            if (!Frames.TryGetValue(index, out var list)) Frames[index] = list = new();
            // 同じ台本位置では最後の実値だけを残す。
            list.RemoveAll(x => x.Id == s.Id); list.Add(s);
        }
    }
    internal static DeathPresentation Record(IReadOnlyList<UnitState> players, IReadOnlyList<UnitState> enemies,
        int seed, out BattleResult result)
    {
        var track = new DeathPresentation();
        bool relevant = players.Concat(enemies).Any(u => u.Def.Id is "rica" or "vel" or "ere" or "hagi");
        result = BattleEngine.Run(players, enemies, seed, verbose: true, probe: relevant ? track.Observe : null);
        track.Finish(result.Events.Count);
        return track;
    }
    internal static int Stage(int layers) => layers >= 9 ? 3 : layers >= 6 ? 2 : layers >= 3 ? 1 : 0;
}
