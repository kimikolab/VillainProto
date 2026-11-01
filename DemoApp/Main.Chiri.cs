using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private ChiriPresentation _chiri = new();
    private bool _chiriFastEvent;

    private async Task<bool> PlayChiri(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (_chiri.Highlights.Contains(index)) { AppendLog(e.Text ?? ""); return true; }
        if (ChiriPresentation.IsCast(e))
        {
            AppendLog($"{NameOf(e.ActorId)} — 仕掛ける");
            await _battleField.BeginChiriCast(actor, _chiri.CastsWithPlant.Contains(index));
            return true;
        }
        if (!ChiriPresentation.IsBomb(e)) return false;
        switch (e.Text)
        {
            case BattleContext.BombPlantText:
                _battleField.PlantChiriBomb(actor, target, e.Amount);
                AppendLog($"  {NameOf(e.ActorId)} → {NameOf(e.TargetId)}：爆弾を仕掛けた");
                if (_chiri.PlantEnds.Contains(index)) await _battleField.FinishChiriPlant(actor);
                break;
            case BattleContext.BombFuseText:
                _battleField.ShortenChiriFuse(target, e.Amount);
                AppendLog($"  {NameOf(e.TargetId)} の導火線：残り {e.Amount}");
                // 第3打の火花から爆発までだけ小さな間を保つ。
                if (e.Amount == 0) await Delay(.06);
                break;
            case BattleContext.BombBlastText:
            case BattleContext.BombMirrorText:
                if (_chiri.Bursts.TryGetValue(index, out var cue)) await _battleField.ShowChiriBurst(cue);
                break;
            case BattleContext.BombDudText:
                _battleField.DudChiriBomb(target);
                AppendLog($"  {NameOf(e.TargetId)} の爆弾は不発");
                await Delay(.12);
                break;
        }
        return true;
    }
}
