using Godot;
using System;

/// <summary>
/// 盤面の上に「誰が誰を叩いたか」の筋を描くだけの層。
///
/// 板そのものではなく別の層にしてあるのは、CanvasItem は自分の <c>_Draw</c> を
/// 子より先に描くため。板に直接描くと線がカードの下に潜る。
/// 描く中身は <see cref="Painter"/> に持たせて、盤面の状態は Main 側に置いたままにする。
/// </summary>
public partial class ShotOverlay : Control
{
    public Action<ShotOverlay>? Painter;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Draw() => Painter?.Invoke(this);
}
