using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    internal int DirgeGiftPlays, DirgeActPlays, DirgeEnemyPlays;
    private readonly List<DirgeAura3D> _dirgeAuras = new();
    internal IEnumerable<DirgeAura3D> DirgeAuras => _dirgeAuras.Where(a => IsInstanceValid(a) && !a.IsQueuedForDeletion());
    internal void ClearDirgeAuras()
    {
        foreach (var aura in DirgeAuras.ToArray()) aura.Stop();
        _dirgeAuras.Clear();
        foreach (var pawn in _pawns.Values) pawn.SetDirgeSinging(false);
    }
    private DirgeAura3D ReceiveDirge(BattlePawn3D bard, BattlePawn3D target, Color tint)
    {
        _dirgeAuras.RemoveAll(a => !IsInstanceValid(a) || a.IsQueuedForDeletion());
        var existing = DirgeAuras.FirstOrDefault(a => a.GiverId == bard.InstanceId && a.TargetId == target.InstanceId && !a.Releasing);
        if (existing is not null) return existing;
        var aura = new DirgeAura3D(); _fxRoot.AddChild(aura);
        aura.Configure(bard.InstanceId, target, _camera, tint, () => (_gatherPaused, _gatherSpeed));
        _dirgeAuras.Add(aura); return aura;
    }

    internal GatherCast3D ShowDirgeGift(BattlePawn3D bard, BattlePawn3D target, bool begins, bool keepSinging = false)
    {
        bool enemy = bard.Team != target.Team;
        Color tint = enemy ? EreGiftArt.Enemy : EreGiftArt.Ally;
        if (begins) { DirgeActPlays++; DeathSongs++; } else DirgeGiftPlays++;
        if (enemy) DirgeEnemyPlays++;
        float duration = begins ? .90f : .76f;
        float contact = begins ? .28f : .60f;
        var fx = GatherEffect(duration, contact);
        if (!begins) bard.SetDirgeSinging(true);
        fx.Configure(duration, contact, () => {
            if (!begins && !keepSinging && IsInstanceValid(bard)) bard.SetDirgeSinging(false);
        });
        fx.Sound(begins ? "fire_gift_turn" : "fire_gift", begins ? -15 : -18, fadeOut: .16f);
        fx.Draw(age => {
            if (!target.PresentationAlive) { fx.Cancel(); return; }
        });
        if (begins)
        {
            // 受け取った音が胸へ集まり、弾ける瞬間に台本の追加行動へ繋ぐ。
            var aura = ReceiveDirge(bard, target, tint);
            aura.Release();
            fx.Configure(duration, contact, () => { if (IsInstanceValid(aura)) aura.Stop(); });
        }
        if (!begins)
        {
            // 五線を伝う音符。流れの向きと着点を、攻撃の弾や炎から区別する。
            Vector3 from = bard.FxPoint + Vector3.Up * .35f;
            Vector3 to = target.FxPoint + Vector3.Up * .35f;
            Vector3 side = _camera.GlobalBasis.Y;
            var mesh = new ImmediateMesh();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            Vector3 Point(float p, float line) => fx.ToLocal(from.Lerp(to, p)
                + Vector3.Up * (Mathf.Sin(p * Mathf.Pi) * .65f) + side * line);
            for (int line = -2; line <= 2; line++)
            for (int i = 0; i < 40; i++)
            {
                float p = i / 40f, q = (i + 1) / 40f;
                Vector3 a = Point(p, line * .075f - .009f), b = Point(p, line * .075f + .009f);
                Vector3 c = Point(q, line * .075f - .009f), d = Point(q, line * .075f + .009f);
                foreach (var v in new[] { a, b, c, c, b, d }) mesh.SurfaceAddVertex(v);
            }
            mesh.SurfaceEnd();
            var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, AlbedoColor = tint };
            fx.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            fx.Draw(age => material.AlbedoColor = new Color(tint,
                Mathf.Sin(Mathf.Clamp(age / duration, 0, 1) * Mathf.Pi) * .48f));
            for (int i = 0; i < 5; i++)
            {
                int k = i;
                var note = fx.Sprite(from, EreGiftArt.Note, .42f, tint);
                fx.Draw(age => {
                    float p = (age - k * .045f) / .42f;
                    note.Visible = p >= 0 && p <= 1;
                    p = Mathf.Clamp(p, 0, 1);
                    note.GlobalPosition = from.Lerp(to, p) + Vector3.Up * (Mathf.Sin(p * Mathf.Pi) * .65f)
                        + side * ((k % 3 - 1) * .075f);
                    note.Modulate = new Color(tint, Mathf.Sin(p * Mathf.Pi) * .6f + .4f);
                });
            }
            fx.Purchased(to, false, tint, .65f, .46f);
            fx.At(.46f, () => { if (target.PresentationAlive) ReceiveDirge(bard, target, tint); });
        }
        return fx;
    }
}

internal static class EreGiftArt
{
    internal static readonly Color Ally = new("b9f4ff"), Enemy = new("ffabbe");
    private static Texture2D? _note, _encore;
    private static Texture2D Svg(string body)
    {
        var image = new Image();
        image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='128' height='128' viewBox='0 0 128 128'>" + body + "</svg>");
        return ImageTexture.CreateFromImage(image);
    }
    internal static Texture2D Note => _note ??= Svg("<g fill='white'><ellipse cx='38' cy='92' rx='20' ry='13' transform='rotate(-22 38 92)'/><path d='M52 91V19L102 8V70H94V32L60 40V91Z'/><ellipse cx='81' cy='74' rx='20' ry='13' transform='rotate(-22 81 74)'/></g>");
    internal static Texture2D Encore => _encore ??= Svg("<g fill='none' stroke='white' stroke-width='4' stroke-linecap='round'><path d='M100 34A47 47 0 1 0 109 78'/><path d='M80 33L103 35 104 12' stroke-width='6'/></g><g fill='white'><ellipse cx='52' cy='82' rx='13' ry='9' transform='rotate(-22 52 82)'/><path d='M61 82V37L84 31V43L67 48V82Z'/></g>");
}
