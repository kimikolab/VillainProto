using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    // 台本の書き手と対象を保持するだけ。被弾や戦闘規則から解除を推測しない。
    private readonly Dictionary<int, (BattlePawn3D Source, BattlePawn3D Target, BindingSilk3D Silk)> _bindings = new();
    public int ActiveBindingCount => _bindings.Count;

    public void SetBinding(BattlePawn3D? source, BattlePawn3D target, bool active)
    {
        if (source is null) return;
        EndBinding(source.InstanceId);
        if (!active) return;
        var silk = new BindingSilk3D();
        _fxRoot.AddChild(silk);
        silk.Configure(source, target, _camera);
        source.SetBinding(true);
        _bindings[source.InstanceId] = (source, target, silk);
        Float(target, "拘束", Color.FromHtml("#f2dfb7"), false);
    }

    private void EndBinding(int id, bool immediate = false)
    {
        if (!_bindings.Remove(id, out var binding)) return;
        binding.Source.SetBinding(false);
        if (!GodotObject.IsInstanceValid(binding.Silk)) return;
        if (immediate) { binding.Silk.Visible = false; binding.Silk.QueueFree(); }
        else binding.Silk.Release();
    }

    public void ClearBindingsFor(BattlePawn3D? pawn)
    {
        if (pawn is null) return;
        foreach (var pair in _bindings.ToArray())
            if (pair.Value.Source == pawn || pair.Value.Target == pawn) EndBinding(pair.Key);
    }

    public void ResetBindings()
    {
        foreach (int id in _bindings.Keys.ToArray()) EndBinding(id, true);
    }
}
