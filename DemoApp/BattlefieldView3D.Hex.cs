using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    public async Task ShowCurseApplication(BattlePawn3D? source, BattlePawn3D target, double speed)
    {
        if (source is null)
        {
            HexMudFx.Splash(_fxRoot, _fxRoot.ToLocal(target.FxPoint), speed);
            return;
        }
        var flight = HexMudFx.Transfer(_fxRoot, _fxRoot.ToLocal(source.FxPoint),
            new[] { _fxRoot.ToLocal(target.FxPoint) }, speed, application: true);
        await ToSignal(flight, Node.SignalName.TreeExiting);
        // 削除通知の最中は兄弟ノードを追加できない。着弾表示は次の安全な描画拍へ渡す。
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    public async Task ShowCurseTransfer(BattlePawn3D? source, IReadOnlyList<BattlePawn3D> targets, double speed)
    {
        if (source is null || targets.Count == 0) return;
        var flight = HexMudFx.Transfer(_fxRoot, _fxRoot.ToLocal(source.FxPoint),
            targets.Select(p => _fxRoot.ToLocal(p.FxPoint)).ToArray(), speed);
        await ToSignal(flight, Node.SignalName.TreeExiting);
        // 削除通知の最中は兄弟ノードを追加できない。着弾表示は次の安全な描画拍へ渡す。
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
