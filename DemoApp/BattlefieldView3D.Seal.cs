using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private readonly HashSet<int> _hushHolders = new();
    private readonly HashSet<int> _fallenForSeal = new();
    private readonly HashSet<int> _hushTargets = new();
    private readonly Dictionary<(int Holder, int Target), HushChain3D> _hushChains = new();
    private int _sealGeneration;
    internal int HushChainCount => _hushChains.Count;

    private void ResetSeals()
    {
        _sealGeneration++;
        foreach (var chain in _hushChains.Values) chain.QueueFree();
        _hushChains.Clear();
        _hushHolders.Clear();
        _fallenForSeal.Clear();
        _hushTargets.Clear();
    }

    private void RegisterSealHolder(DemoOpening opening)
    {
        if (opening.Traits?.Contains(TraitId.Hush) == true)
            _hushHolders.Add(opening.InstanceId);
    }

    private void ConnectSeals()
    {
        foreach (int holderId in _hushHolders.Where(id => !_fallenForSeal.Contains(id)))
        foreach (int targetId in _hushTargets.Where(id => !_fallenForSeal.Contains(id)))
        {
            var key = (holderId, targetId);
            if (_hushChains.ContainsKey(key)) continue;
            var holder = FindPawn(holderId);
            var target = FindPawn(targetId);
            if (holder is null || target is null) continue;
            var chain = new HushChain3D();
            _fxRoot.AddChild(chain);
            chain.Configure(holder, target);
            _hushChains.Add(key, chain);
        }
    }

    public async Task ShowHushSeal(BattlePawn3D? target)
    {
        if (target is null) return;
        // 能力の一覧から対象を推測しない。一度実際に止められた駒だけに薄い鎖を残す。
        _hushTargets.Add(target.InstanceId);
        ConnectSeals();
        double duration = 0.86 / Math.Clamp(target.AnimationSpeed, 0.75, 1.6);
        int generation = _sealGeneration;
        BeginBonusAura(target, Color.FromHtml("#63d7ff"), duration, interrupted: true);
        await ToSignal(GetTree().CreateTimer(duration * 0.28), SceneTreeTimer.SignalName.Timeout);
        if (!IsInsideTree() || generation != _sealGeneration) return;
        _attackAudio.PlayHushBlock();
        foreach (var pair in _hushChains.Where(p => p.Key.Target == target.InstanceId))
            pair.Value.Tighten();
        await ToSignal(GetTree().CreateTimer(duration * 0.72), SceneTreeTimer.SignalName.Timeout);
    }

    public void SealPawnDied(BattlePawn3D? pawn)
    {
        if (pawn is null) return;
        _fallenForSeal.Add(pawn.InstanceId);
        bool shattered = false;
        foreach (var key in _hushChains.Keys.Where(k => k.Holder == pawn.InstanceId || k.Target == pawn.InstanceId).ToArray())
        {
            // 保持者の死亡だけが鎖を砕く。封じられた側の死亡は静かに消す。
            if (key.Holder == pawn.InstanceId)
            {
                _hushChains[key].Shatter();
                shattered = true;
            }
            else _hushChains[key].QueueFree();
            _hushChains.Remove(key);
        }
        // 一人の保持者から複数本伸びていても、死亡一回につき破砕音は一音だけ。
        if (shattered) _attackAudio.PlayHushBreak();
    }

    public void SealPawnRevived(BattlePawn3D pawn)
    {
        _fallenForSeal.Remove(pawn.InstanceId);
        ConnectSeals();
    }
}
