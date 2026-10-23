using Godot;
using System.Threading.Tasks;

public partial class Main
{
    // 台本の歌唱1回を見せる時間。停止中は進めず、途中の速度変更にも追従する。
    private async Task HoldDirgeSong(int token)
    {
        if (_fastSmoke) return;
        double elapsed = 0;
        while (elapsed < 0.80 && token == _playToken && _battleMode)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_paused) elapsed += GetProcessDeltaTime() * _speed;
        }
    }
}
