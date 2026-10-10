public partial class BattlePawn3D
{
    internal void ResetAssetPreviewHome(Godot.Vector3 home)
    {
        _motion?.Kill();
        SetHome(home);
    }

    // 比較画面だけで使用。両候補で同じ燃焼中の立ち絵・熱表現を保つ。
    internal void SetAssetPreviewBurning(bool burning, bool purchased)
    {
        UsePurchasedBurn = false;
        SetFireRemaining(burning ? 3 : 0);
        SetFireLevel(burning ? 2 : 0);
        _fire.Visible = burning && !purchased;
    }
}
