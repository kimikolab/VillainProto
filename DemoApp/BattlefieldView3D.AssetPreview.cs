using Godot;

public partial class BattlefieldView3D
{
    // 比較シーン専用。通常の戦闘は既存の背景選択・演出入口を使う。
    internal Node3D AssetPreviewEffects => _fxRoot;
    internal bool? AssetPreviewFireballs { get; set; }
    internal int? AssetPreviewFirePierceVariant { get; set; }
    internal bool LegacySceneryForComparison { get; init; }
    internal int AssetPreviewPierceImpacts { get; private set; }
    internal int AssetPreviewFireballImpacts { get; private set; }
    internal void CancelAssetPreviewAttack()
    {
        _fireGeneration++;
        AssetPreviewFireballImpacts = 0;
        AssetPreviewPierceImpacts = 0;
        _fireShadeTween?.Kill();
        _fireShade?.Hide();
        _attackAudio.StopFireSounds();
    }
    private Node3D? _assetPreviewScenery;

    internal void SetAssetPreviewScenery(Node3D candidate, int variant)
    {
        if (_assetPreviewScenery != null)
        {
            _assetPreviewScenery.Hide();
            _assetPreviewScenery.ProcessMode = ProcessModeEnum.Disabled;
        }
        _assetPreviewScenery = candidate;
        bool purchased = variant != 0;
        if (candidate.GetParent() == null) _world.AddChild(candidate);
        _scenery.Visible = !purchased;
        _scenery.ProcessMode = purchased ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        candidate.Visible = purchased;
        candidate.ProcessMode = purchased ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        SetMeadowSky(variant == 3);
        _headline.Text = variant switch {
            1 => "背景 B：購入素材・平面（初回比較）",
            2 => "背景 C：既存の起伏・草 ＋ 購入素材の木・茂み・岩",
            3 => "背景 D：風に揺れる草原 — 既存の地面・草 ＋ 購入植生・青空",
            _ => "背景 A：既存の草原"
        };
        _subline.Text = variant == 3 ? "Cの起伏・地面・草・風を使用 / 購入素材の木・茂みと青空・遠景の霞 / カメラと駒は共通"
            : "A–Cは共通の照明 / Dは空と環境光も調整 / カメラと駒は共通";
    }

}
