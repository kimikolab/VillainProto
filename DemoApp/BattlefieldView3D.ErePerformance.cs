using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    private const string DirgePerformanceGroup = "ere_performance";
    internal IEnumerable<GatherCast3D> DirgePerformances => GatherEffects.Where(f => f.IsInGroup(DirgePerformanceGroup));

    // Cの自然な演奏を保ち、開始から音符と五線譜を重ねる。手番の対象や時間は変えない。
    internal GatherCast3D ShowDirgePerformance(BattlePawn3D bard, float phraseSeconds, string? songClip = null,
        bool throughClock = false)
    {
        if (songClip is null)
        {
            songClip = NextDirgeSong();
            throughClock = songClip is not null;
        }
        float seconds = Math.Max(.1f, phraseSeconds), duration = seconds + .45f;
        var fx = GatherEffect(duration, seconds);
        fx.AddToGroup(DirgePerformanceGroup);
        if (throughClock && songClip is not null)
        {
            StartDirgeMusic(bard, songClip);
            var music = DirgeMusic;
            fx.Configure(duration, seconds, () => {
                // 開始の光の正常終了では曲を残し、途中中断なら一緒に止める。
                if (fx.Age < duration && IsInstanceValid(music)) music!.Cancel();
            });
        }
        else
            fx.Sound(songClip ?? "ere_harpsichord.wav", songClip is null ? -6 : -8,
                start: .02f, fadeIn: .02f, fadeOut: songClip is null ? .18f : .24f,
                end: songClip is null ? null : seconds);
        Color gold = new("ffe2a1"), pearl = new("e7f8ff");
        Vector3 Lute() => bard.GatherPortraitPoint(new Vector2(620, 475), _camera);
        fx.Flash(Lute(), GatherArt.Light, 1.25f, 0, .34f);
        fx.Purchased(Lute(), false, gold, .45f, .02f);

        var poolMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/fx/ere_song_light.gdshader") };
        var pool = FireFx.Card(fx, Vector3.Zero, Vector2.One * 3.65f, poolMaterial, ground: true);
        var uplightMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/fx/ere_song_uplight.gdshader"), RenderPriority = 1 };
        var uplight = FireFx.Card(fx, Vector3.Zero, new Vector2(2.25f, 2.10f), uplightMaterial);
        var luteLight = fx.Sprite(Lute(), GatherArt.Light, .86f, gold);
        luteLight.RenderPriority = 2;

        var staff = new ImmediateMesh();
        var ink = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true, AlbedoColor = gold,
            EmissionEnabled = true, Emission = gold * .28f, RenderPriority = 2,
        };
        fx.AddChild(new MeshInstance3D { Mesh = staff, MaterialOverride = ink,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        var notes = Enumerable.Range(0, 7).Select(i => fx.Sprite(bard.FxPoint, EreGiftArt.Note, .34f + (i % 3) * .035f, gold)).ToArray();

        // 五線は腰から楽器の周りを巡る。上半分を空けて顔とHPを覆わない。
        Vector3 ScorePoint(float angle, float line, float age, float opening)
        {
            float radius = .48f + opening * .73f;
            return bard.FxPoint + _camera.GlobalBasis.X * (Mathf.Cos(angle) * radius)
                + _camera.GlobalBasis.Y * (Mathf.Sin(angle) * .36f + Mathf.Sin(angle * 2 + age * .8f) * .10f - .24f + line)
                + _camera.GlobalBasis.Z * (Mathf.Sin(angle) * .15f);
        }

        fx.Draw(age => {
            if (!bard.PresentationAlive || (!bard.DirgeSinging && age < seconds)) { fx.Cancel(); return; }
            float opening = Mathf.SmoothStep(0, 1, Mathf.Clamp(age / .24f, 0, 1));
            float tail = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp((age - seconds) / .45f, 0, 1));
            float gain = (.18f + opening * .82f) * tail;
            float beat = .82f + .18f * Mathf.Sin(age * Mathf.Tau * 1.3f);
            pool.GlobalPosition = bard.GlobalPosition + Vector3.Up * .035f;
            poolMaterial.SetShaderParameter("age", age);
            poolMaterial.SetShaderParameter("gain", gain);
            uplight.GlobalPosition = bard.GlobalPosition + _camera.GlobalBasis.Y * 1.05f + _camera.GlobalBasis.Z * .08f;
            uplightMaterial.SetShaderParameter("age", age);
            uplightMaterial.SetShaderParameter("gain", gain);
            luteLight.GlobalPosition = Lute();
            luteLight.Modulate = new Color(gold, gain * (.26f + .12f * beat));
            luteLight.Scale = Vector3.One * (.85f + beat * .14f);
            ink.AlbedoColor = new Color(gold, gain * .58f);

            staff.ClearSurfaces();
            staff.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            for (int line = -2; line <= 2; line++)
            for (int segment = 0; segment < 64; segment++)
            {
                float a = segment * Mathf.Tau / 64, b = (segment + 1) * Mathf.Tau / 64;
                float y = line * .065f;
                var color = new Color(Colors.White, .45f + (1 + Mathf.Sin(a)) * .22f);
                staff.SurfaceSetColor(color);
                Vector3 p = fx.ToLocal(ScorePoint(a, y - .006f, age, opening));
                Vector3 q = fx.ToLocal(ScorePoint(a, y + .006f, age, opening));
                Vector3 r = fx.ToLocal(ScorePoint(b, y - .006f, age, opening));
                Vector3 s = fx.ToLocal(ScorePoint(b, y + .006f, age, opening));
                foreach (var point in new[] { p, q, r, r, q, s }) staff.SurfaceAddVertex(point);
            }
            staff.SurfaceEnd();
            for (int i = 0; i < notes.Length; i++)
            {
                float angle = age * 1.35f + i * Mathf.Tau / notes.Length;
                float depth = (1 + Mathf.Sin(angle)) * .5f;
                notes[i].GlobalPosition = ScorePoint(angle, (i % 3 - 1) * .065f, age, opening)
                    + _camera.GlobalBasis.Y * ((1 - tail) * .45f);
                notes[i].Modulate = new Color(i % 3 == 0 ? pearl : gold, gain * (.50f + depth * .42f));
                notes[i].Scale = Vector3.One * (.78f + depth * .24f);
            }
        });
        return fx;
    }
}
