using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// ボルグの薙ぎを攻撃の連番（attack_clip/borg_sweep/・採用は F-5055）で見せる。台本の対象・命中の通知は従来どおり。
// 起動引数 --borg-attack-clip=none で従来の「構え→振り抜き」の差分絵に戻せる（比べるとき用）。
// 経緯と作り方は design/art/borg/attack-sweep-v1/notes.md。
public partial class BattlefieldView3D
{
    internal static string? BorgAttackClip { get; set; } = OS.GetCmdlineUserArgs()
        .FirstOrDefault(a => a.StartsWith("--borg-attack-clip=", StringComparison.Ordinal))?["--borg-attack-clip=".Length..] switch
    {
        "none" => null,
        null or "" => "borg_sweep",
        var v => v,
    };

    /// <summary>踏み込んだ駒は、攻撃の連番を振り切ってから元の席へ戻る（連番が無ければすぐ戻る）。</summary>
    private static async void ReturnAfterAttackClip(BattlePawn3D pawn)
    {
        int motionVersion = pawn.AttackMotionVersion;
        // 同じ攻撃内で連番が切り替わった場合も振り切りまで待つ。次の攻撃には帰還を持ち越さない。
        while (IsInstanceValid(pawn) && pawn.IsInsideTree() && pawn.AttackMotionVersion == motionVersion && pawn.AttackClipPlaying)
            await pawn.AttackClipFinished;
        // 次の攻撃の踏み込み中に旧帰還がBeginMotionを呼ぶと、待機中のTweenをKillして再生が止まる。
        if (IsInstanceValid(pawn) && pawn.IsInsideTree() && pawn.AttackMotionVersion == motionVersion)
        { if (pawn.UnitId == "rica") pawn.ReleaseRicaPose(); pawn.ReturnFromAttack(); }
    }

    internal int BorgAttackClipPlays { get; private set; }

    /// <summary>連番があれば再生を始め、刃が当たるコマまで待って true を返す（連番の残りは並行して流れる）。無ければ false。</summary>
    private async Task<bool> PlayBorgAttackClipToContact(BattlePawn3D actor)
    {
        if (BorgAttackClip is not { } name || BattlePawn3D.AttackClipOf(name) is null) return false;
        BorgAttackClipPlays++;
        var contact = new TaskCompletionSource();
        _ = actor.PlayAttackClip(name, () => contact.TrySetResult());
        await contact.Task;
        return true;
    }

    /// <summary>燃焼の攻撃（PlayFireAttack）用: 連番を流し、刃が当たるコマで剣閃を出して true を返す。無ければ false。</summary>
    private async Task<bool> PlayBorgAttackClip(BattlePawn3D actor, BattlePawn3D target, int level, double speed)
    {
        if (!await PlayBorgAttackClipToContact(actor)) return false;
        if (!IsInstanceValid(actor) || !IsInstanceValid(target)) return true;
        FireUltimateFx.Crescent(_fxRoot, actor.FxPoint, target.FxPoint, _camera, 0.40 / speed, 1.6f + level * 0.22f);
        await ToSignal(GetTree().CreateTimer(Math.Max(0.001, 0.10 / speed)), SceneTreeTimer.SignalName.Timeout);
        return true;
    }
}
