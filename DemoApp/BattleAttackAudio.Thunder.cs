using Godot;
using System;

public partial class BattleAttackAudio
{
    private AudioStreamWav? _thunderPlaceholder, _sparkPlaceholder;

    public void PlayElectric(bool first, int step, int kinds)
    {
        int index = _nextVoice++ % _voices.Length;
        var voice = _voices[index];
        voice.Stop();
        float boost = Math.Min(3, kinds) * 0.8f;
        voice.VolumeDb += boost - _voiceBoosts[index];
        _voiceBoosts[index] = boost;
        string path = first ? "res://assets/audio/se/kata_thunder.mp3" : "res://assets/audio/se/kata_discharge.mp3";
        voice.Stream = FileAccess.FileExists(path) ? LoadSound(path)
            : first ? _thunderPlaceholder ??= ElectricPlaceholder(true) : _sparkPlaceholder ??= ElectricPlaceholder(false);
        // 再生速度ではなく、台本の跳ね・段にだけ音程を対応させる。
        voice.PitchScale = first ? 1f : 1f + Math.Min(step, 6) * 0.055f;
        voice.Play();
    }

    // 音源決定までの確認用。短いノイズ＋低い減衰音で、雷と接触音を聞き分ける。
    private static AudioStreamWav ElectricPlaceholder(bool thunder)
    {
        const int rate = 22050;
        int length = (int)(rate * (thunder ? 0.48 : 0.12));
        var data = new byte[length * 2];
        var random = new Random(214);
        double low = 0;
        for (int i = 0; i < length; i++)
        {
            double t = i / (double)rate, noise = random.NextDouble() * 2 - 1;
            low = low * 0.91 + noise * 0.09;
            double sound = thunder ? low * 2 + Math.Sin(t * 330) * 0.3 : noise * 0.5;
            sound *= Math.Exp(-t * (thunder ? 8 : 34)) * Math.Min(1, t * 1500);
            short value = (short)(Math.Clamp(sound, -1, 1) * 23000);
            data[i * 2] = (byte)(value & 255); data[i * 2 + 1] = (byte)((value >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Data = data };
    }
}
