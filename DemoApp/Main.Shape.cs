using BattleCore;
using Godot;
using System;
using System.Linq;

public partial class Main
{
    // =================================================================================
    // 第201期: 編成画面で陣形を選ぶ（X 字 ⇔ パターン2）
    //
    // 置いた駒は**編成の枠 0〜4** に持つ（`_formation`）。X 字なら枠 i ＝ 席 i、パターン2なら
    // 枠 i ＝ `FormationShape.Diamond.FrameNames[i]`（中衛・上／後衛／中衛・中央／前衛／中衛・下）。
    // 切り替えたときは**前1 の駒が前衛に来る**ように詰め替える（第200期の `--demo-shape=p2` と同じ規則。
    // 2回切り替えると元に戻る）。**判定は1つも持たない**——陣形の表は `FormationShape` にしかない。
    // =================================================================================

    private FormationShape _shape = FormationShape.X;
    private Button _shapeButton = null!;

    private string SeatLabel(int frame) => _shape.FrameNames[frame];

    private string ShapeButtonText() => ReferenceEquals(_shape, FormationShape.Diamond) ? "陣形: パターン2 ⇄" : "陣形: X字 ⇄";

    private void ToggleShape()
        => SetShape(ReferenceEquals(_shape, FormationShape.Diamond) ? FormationShape.X : FormationShape.Diamond);

    private void SetShape(FormationShape shape)
    {
        if (_battleMode || ReferenceEquals(shape, _shape)) return;
        if (ReferenceEquals(shape, FormationShape.Diamond)) XToDiamond(_formation); else DiamondToX(_formation);
        _shape = shape;
        _shapeButton.Text = ShapeButtonText();
        _field.SetSetupShape(shape);
        Notice(ReferenceEquals(shape, FormationShape.Diamond)
            ? "陣形をパターン2（ひし形・前衛1枚）にしました。前1 の駒が前衛に立ちます"
            : "陣形を X 字に戻しました");
        RefreshFormation();
    }

    /// <summary>X 字の枠 → パターン2の枠: 前1 → 前衛、前3 → 中衛・上、中央 → 中衛・中央、後1 → 中衛・下、後3 → 後衛。</summary>
    private static void XToDiamond(UnitDef?[] f)
    {
        UnitDef?[] x = (UnitDef?[])f.Clone();
        f[3] = x[0]; f[0] = x[1]; f[2] = x[2]; f[4] = x[3]; f[1] = x[4];
    }

    /// <summary><see cref="XToDiamond"/> の逆。</summary>
    private static void DiamondToX(UnitDef?[] f)
    {
        UnitDef?[] p = (UnitDef?[])f.Clone();
        f[0] = p[3]; f[1] = p[0]; f[2] = p[2]; f[3] = p[4]; f[4] = p[1];
    }

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
