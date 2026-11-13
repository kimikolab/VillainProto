using Godot;
using System;
using System.Linq;

/// <summary>
/// 作戦マップの見た目の切り替え（2D ／ 3D のジオラマ）。<b>規則は 1 行も持たない</b>——見た目の層を選ぶだけ。
/// 既定は 2D。`--map3d` か F2 で 3D へ。購入素材（NatureBlocks）が無い環境では 2D に落ちる。
/// 発注は `design/BRIEF_MAP3D_STAGE11.md`、素材の入れ方は `design/MAP3D_ASSETS.md`。
/// </summary>
internal static class Map3DMode
{
    /// <summary>3D の層の画面の基準（16:9）。2D の経路は `project.godot` の基準のまま。</summary>
    internal static readonly Vector2I BaseSize = new(1920, 1080);

    /// <summary>写しと fps を揃えるための seed（`--map-seed=` と同じ値を 2 本の担当が使う）。</summary>
    internal const int CompareSeed = 11;

    private static bool _argsRead;
    private static bool _noAssets;

    /// <summary>3D を選んでいるか（引数か F2）。素材が無ければ <see cref="Active"/> は偽のまま。</summary>
    internal static bool On { get; private set; }

    /// <summary>検証用の UI の層を出しているか（既定は隠す・F3）。</summary>
    internal static bool DebugShown { get; set; }

    /// <summary>木・茂み・岩・環境が入っているか（`--map3d-no-assets` は無い環境の真似）。</summary>
    internal static bool AssetsAvailable => !_noAssets && PurchasedAssets.NatureAvailable;

    /// <summary>道・川・ばらまきの道具が入っているか。無くても 3D には入れる（道は自作の帯に落とす）。</summary>
    internal static bool ToolsAvailable => !_noAssets && PurchasedAssets.NatureToolsAvailable;

    /// <summary>3D の層を実際に組むか。<b>画面の側が読むのはこれだけ。</b></summary>
    internal static bool Active => On && AssetsAvailable;

    /// <summary>起動の引数を 1 度だけ読む（シーンを読み直しても F2 の選択を上書きしない）。</summary>
    internal static void ReadArgs()
    {
        if (_argsRead) return;
        _argsRead = true;
        string[] args = OS.GetCmdlineUserArgs();
        _noAssets = args.Contains("--map3d-no-assets", StringComparer.Ordinal);
        DebugShown = args.Contains("--map3d-debug", StringComparer.Ordinal);
        if (args.Contains("--map3d", StringComparer.Ordinal)) Select(true);
    }

    /// <summary>2D ／ 3D を選ぶ。選んだ結果（素材が無くて落ちたか）を 1 行で出す。</summary>
    internal static void Select(bool on)
    {
        On = on;
        string reason = !on ? "off" : AssetsAvailable ? "ok" : "no-assets";
        GD.Print($"MAP3D_MODE on={On} active={Active} tools={ToolsAvailable} reason={reason}");
    }
}
