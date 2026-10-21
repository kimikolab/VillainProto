using Godot;

// 旧「溜める→放つ」の検証入口を、現在の「今の傷を詠む」の実戦検証へ接続する。
public partial class SusuPortraitCheck : Node
{
    public override void _Ready()
    {
        var check = GD.Load<PackedScene>("res://GatherCastCheck.tscn").Instantiate<GatherCastCheck>();
        check.ForceVerify = true;
        AddChild(check);
    }
}
