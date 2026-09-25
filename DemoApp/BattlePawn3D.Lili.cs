using BattleCore;
using Godot;

public partial class BattlePawn3D
{
    private Sprite3D? _stigma;
    private float _liliReach;
    // 採用済み戦闘絵の杯の縁 (782, 344) / (1024, 1536)。左右反転にも追従する。
    public Vector3 ChalicePoint => PortraitPoint(782f / 1024f, 344f / 1536f);
    private Vector3 PortraitPoint(float x, float y)
    {
        var camera = GetViewport().GetCamera3D();
        var right = camera?.GlobalBasis.X ?? Vector3.Right;
        var up = camera?.GlobalBasis.Y ?? Vector3.Up;
        float width = _portraitHeight * _sprite.Texture.GetWidth() / _sprite.Texture.GetHeight();
        return _sprite.GlobalPosition + right * ((x - 0.5f) * width * (Team == 0 ? 1 : -1))
            + up * ((0.5f - y) * _portraitHeight);
    }
    public void ReachWithChalice() => _liliReach = 0.65f;
    private void ProcessLili(float delta)
    {
        _liliReach = Mathf.Max(0, _liliReach - delta);
        _sprite.Position += Vector3.Right * ((Team == 0 ? 1 : -1) * Mathf.Sin(_liliReach / 0.65f * Mathf.Pi) * 0.10f);
    }
    public void SetStigma(bool active)
    {
        active &= _alive && !_victory;
        if (active && _stigma is null)
            _stigma = LiliFx.Sprite(this, FxPoint + Vector3.Up * 0.42f, LiliFx.Drop, 0.38f);
        if (!active && _stigma is not null) { _stigma.QueueFree(); _stigma = null; }
        if (_alive) _statusIcons.Set(StatusKeys.Stigma, active);
    }
    internal bool HasStigma => _stigma is not null;
    public void ApplyTransferredStatus(string key, int amount)
    {
        if (!_alive) return;
        if (amount > 0) _statusSnapshot[key] = amount;
        else _statusSnapshot.Remove(key);
        if (key == StatusKeys.Poison) SetPoisonRemaining(amount);
        else if (key == StatusKeys.Concentrated) SetConcentrated(amount);
        else SetStatusIcon(key, amount > 0);
        if (key == StatusKeys.Burn) SetBurning(amount > 0);
        if (key == StatusKeys.Stun) SetFrightened(amount > 0);
        if (key == StatusKeys.Stagger)
        {
            if (amount > 0) AnimateStaggerFall();
            else ResetStaggerPose();
        }
    }
}
