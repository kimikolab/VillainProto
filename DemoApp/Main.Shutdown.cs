using Godot;
using System;
using System.Threading.Tasks;

public partial class Main
{
    // アプリを終了する時だけ全Tweenを止める。通常の再戦で別画面のTweenを巻き込まない。
    private async Task QuitAfterPresentation()
    {
        ++_playToken;
        _battleField.EndShockMarkPresentation();
        var tree = GetTree();
        foreach (var tween in tree.GetProcessedTweens()) tween.Kill();
        foreach (Node child in GetChildren()) child.QueueFree();
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        // C#側のTween / Tweener参照を、ネイティブのSceneTreeが生きている間に返す。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        tree.Quit();
    }
}
