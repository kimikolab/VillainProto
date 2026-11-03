using BattleCore;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    // 開戦・召喚の台本の保持者と、Sealed が実際に止めた対象だけを記憶する。
    private readonly Dictionary<string, HashSet<int>> _ruleHolders = new();
    private readonly Dictionary<string, HashSet<int>> _ruleTargets = new();
    private readonly Dictionary<(string Rule, int Holder, int Target), HushChain3D> _ruleChains = new();
    internal int RuleChainCount => _ruleChains.Count;

    private void ClearRuleChains()
    {
        foreach (var chain in _ruleChains.Values)
            if (IsInstanceValid(chain)) { chain.Hide(); chain.QueueFree(); }
        _ruleChains.Clear(); _ruleHolders.Clear(); _ruleTargets.Clear();
    }

    private void RegisterRuleChains(DemoOpening opening)
    {
        foreach (var (trait, rule) in new[] { (TraitId.Yoke, SealedLabels.Yoke), (TraitId.Drought, SealedLabels.Drought) })
        {
            if (opening.Traits?.Contains(trait) != true) continue;
            if (!_ruleHolders.TryGetValue(rule, out var holders)) _ruleHolders[rule] = holders = new();
            holders.Add(opening.InstanceId);
        }
        ConnectRuleChains();
    }

    private void ConnectRuleChains()
    {
        foreach (var (rule, holders) in _ruleHolders)
        {
            if (!_ruleTargets.TryGetValue(rule, out var targets)) continue;
            foreach (int holderId in holders.Where(id => !_fallenForSeal.Contains(id)))
            foreach (int targetId in targets.Where(id => !_fallenForSeal.Contains(id)))
            {
                var key = (rule, holderId, targetId);
                if (_ruleChains.ContainsKey(key)) continue;
                var holder = FindPawn(holderId); var target = FindPawn(targetId);
                if (holder is null || target is null) continue;
                var chain = new HushChain3D();
                _fxRoot.AddChild(chain);
                chain.Configure(holder, target, RuleColor(rule));
                chain.SetClock(() => (_deathPaused, _deathSpeed));
                _ruleChains[key] = chain;
            }
        }
    }

    private void TightenRuleChains(string rule, BattlePawn3D target)
    {
        if (!_ruleTargets.TryGetValue(rule, out var targets)) _ruleTargets[rule] = targets = new();
        targets.Add(target.InstanceId);
        ConnectRuleChains();
        foreach (var (key, chain) in _ruleChains)
            if (key.Rule == rule && key.Target == target.InstanceId) chain.Tighten();
    }

    private bool BreakRuleChains(int id)
    {
        bool shattered = false;
        foreach (var key in _ruleChains.Keys.Where(k => k.Holder == id || k.Target == id).ToArray())
        {
            if (key.Holder == id) { _ruleChains[key].Shatter(); shattered = true; }
            else _ruleChains[key].QueueFree();
            _ruleChains.Remove(key);
        }
        return shattered;
    }
}
