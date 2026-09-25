using BattleCore;
using Godot;
using System;
using System.Linq;

public partial class Main
{
    // 第200期: 味方の陣形パターン2（ひし形・前衛1枚）で戦を始める口（**再生の確認用**）。
    //
    //     -- --demo-shape=p2 [--demo-p2-front=<駒Id>]
    //
    // 編成画面は X 字の5枠のまま触らない（指示書 §6-7・次期）。ここでは組んだ X 字の編成を
    // **D ＝ 前1 の駒**（`--demo-p2-front` を渡せばその駒）、残りを X 字の席の順で A・C・E・B に詰め替える
    // ——`BattleSim` の `form2 run` 表B と同じ組み方。再生側（`BattlefieldView3D.PawnPosition`）は
    // 9席すべてに座標を持っているので、描き足しは要らない。
    private Formation ApplyDemoShape(Formation x)
    {
        string[] args = OS.GetCmdlineUserArgs();
        if (!args.Contains("--demo-shape=p2", StringComparer.Ordinal)) return x;

        string? frontArg = args.FirstOrDefault(a => a.StartsWith("--demo-p2-front=", StringComparison.Ordinal));
        string? frontId = frontArg?["--demo-p2-front=".Length..];
        int d = 0;
        if (frontId is not null)
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                if (x[i]?.Id == frontId) { d = i; break; }

        var rest = Enumerable.Range(0, FormationRules.PlayableSlotCount).Where(i => i != d).ToArray();
        Formation p = Formation.BuildDiamond(a: x[rest[0]], c: x[rest[1]], e: x[rest[2]], b: x[rest[3]], d: x[d]);
        GD.Print($"DEMO_SHAPE shape={p.Shape.Name} " + string.Join(" ",
            p.Occupied().Select(o => $"{(char)('A' + o.Slot)}:{o.Def.Name}")));
        return p;
    }
}
