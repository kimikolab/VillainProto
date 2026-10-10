using Godot;

public partial class BattlePawn3D
{
    private int _sharePortraitVersion;

    // 重なった発動では、新しい受け止めを古い待ち時間の完了で上書きしない。
    internal int BeginSharePain(bool flip, bool hold = false)
    {
        int version = ++_sharePortraitVersion;
        ShowMovementPortrait("doha_receive", 0.85, flip: flip);
        // 往路の実時間は演出側が管理する。処理落ちや停止で復帰タイマーと競合させない。
        if (hold) _movementPortraitTween?.Kill();
        return version;
    }

    internal bool GiveSharePower(int version, bool flip)
    {
        if (version != _sharePortraitVersion || MovementPortrait != "doha_receive"
            || !_alive || _victory) return false;
        ShowMovementPortrait("doha_give", 0.45, flip: flip);
        return true;
    }

    internal void EndSharePortrait()
    {
        _sharePortraitVersion++;
        if (MovementPortrait is "doha_receive" or "doha_give") ClearMovementPortrait();
    }

    internal void EndSharePortrait(int version)
    {
        if (version == _sharePortraitVersion) EndSharePortrait();
    }

    // 採用画像（1024×1536）の胸札と開いた掌。敵側・味方の左右には描画側で追従する。
    internal Vector3 ShareChestPoint(Camera3D camera) => MovementPortraitPoint(
        MovementPortrait == "doha_receive" ? new Vector2(610, 310) : new Vector2(525, 310), camera);
    internal Vector3 ShareHandPoint(Camera3D camera) => MovementPortraitPoint(new Vector2(900, 330), camera);
}
