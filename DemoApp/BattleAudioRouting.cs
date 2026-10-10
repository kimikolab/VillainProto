using Godot;

// 粛がこもらせるのは戦場の音。鎖そのものの停止音・破裂音はMasterへ直接流す。
internal static class BattleAudioRouting
{
    internal static readonly StringName FieldBus = "BattleAtmosphere";

    internal static int EnsureFieldBus()
    {
        int index = AudioServer.GetBusIndex(FieldBus);
        if (index >= 0) return index;
        // パン用バスより手前に置く。GodotのSendは左側のバスへ流れる。
        AudioServer.AddBus(1);
        AudioServer.SetBusName(1, FieldBus);
        AudioServer.SetBusSend(1, "Master");
        return 1;
    }
}
