using Godot;
using System;
using System.Collections.Generic;

public partial class BattlefieldView3D
{
    private static readonly Color ZanRed = new("ff3459");
    private readonly Dictionary<int, (int Hits, int Damage, PopupLabel2D Label)> _zanTotals = new();
    private readonly List<Node3D> _zanRoots = new();
    private ColorRect? _zanFlash;
    private Tween? _zanFlashTween;
    internal int ZanFlurries, ZanDamage, ZanRecoilPlays, ZanLinks;
    internal int ZanSlashPlays, ZanWhiteFlashes;
    internal int ZanHudCount => _zanTotals.Count;
    internal bool ZanFlashVisible => _zanFlash?.Visible == true;

    private void ResetZanPresentation()
    {
        EndZanPresentation();
        ZanFlurries = ZanDamage = ZanRecoilPlays = ZanLinks = 0;
        ZanSlashPlays = ZanWhiteFlashes = 0;
    }
    internal void ResetZanTurn()
    {
        foreach (var total in _zanTotals.Values)
            if (LivePopup(total.Label)) { total.Label.Hide(); total.Label.QueueFree(); }
        _zanTotals.Clear();
    }
    private void EndZanPresentation()
    {
        ResetZanTurn();
        _zanFlashTween?.Kill();
        _zanFlash?.Hide();
        foreach (var pawn in _pawns.Values) pawn.EndVendetta();
        foreach (var root in _zanRoots)
            if (IsInstanceValid(root) && !root.IsQueuedForDeletion()) { root.Hide(); root.QueueFree(); }
        _zanRoots.Clear();
    }

    internal double ShowZanFlurry(BattlePawn3D actor, BattlePawn3D target, ZanPresentation.Flurry group, double speed)
    {
        ZanFlurries++;
        speed = Math.Max(0.1, speed);
        int hits = group.Hits.Count;
        int cuts = hits <= 2 ? 1 : hits <= 3 ? 2 : hits <= 5 ? 3 : hits <= 7 ? 4 : hits <= 9 ? 5 : 7;
        // 本数だけを増やす。90回の束でも斬撃の区間は0.24秒、帰還込みで0.72秒。
        double spacing = cuts == 1 ? 0 : Math.Min(0.08, 0.24 / (cuts - 1));
        double duration = cuts == 1 ? 0.52 : 0.72;
        int generation = _specialGeneration;
        var root = new Node3D(); _fxRoot.AddChild(root);
        _zanRoots.RemoveAll(n => !IsInstanceValid(n) || n.IsQueuedForDeletion());
        _zanRoots.Add(root);
        bool Live() => generation == _specialGeneration && !_markRallyEnded && IsVisibleInTree()
            && IsInstanceValid(actor) && IsInstanceValid(target);
        foreach (int id in group.Allies)
        {
            var ally = FindPawn(id);
            if (ally is null) continue;
            ZanLinks++;
            Vector3 from = ally.FxPoint, to = actor.FxPoint + Vector3.Up * 0.35f;
            ShockMarkFx.Beam(root, from, to, new Color(ZanRed, 0.55f), 0.022f, 0.20 / speed);
            var signal = ShockMarkFx.Sprite(root, from, ShockMarkFx.Halo, 0.20f, ZanRed);
            var flight = signal.CreateTween();
            flight.TweenProperty(signal, "global_position", to, 0.10 / speed);
            flight.TweenCallback(Callable.From(signal.QueueFree));
        }
        actor.BeginVendetta(target.Position + new Vector3(0, 0, 0.85f), duration);
        Vector3 blade = actor.MovementPortraitPoint(new Vector2(918, 579), _camera);
        ShockMarkFx.Beam(root, blade - _camera.GlobalBasis.X * 0.20f, blade + _camera.GlobalBasis.X * 0.28f,
            ZanRed, 0.035f, 0.20 / speed);
        _attackAudio.PlayZan(ZanSound.Cue);
        var sequence = root.CreateTween();
        sequence.TweenInterval(0.23 / speed);
        for (int i = 0; i < cuts; i++)
        {
            int cut = i;
            sequence.TweenCallback(Callable.From(() => {
                if (!Live()) return;
                DrawZanCut(root, target, hits, cut, cuts, speed);
            }));
            if (i < cuts - 1) sequence.TweenInterval(spacing / speed);
        }
        sequence.TweenCallback(Callable.From(() => {
            if (!Live()) return;
            // 標の丸い層マーカーと混同させない、四隅だけの赤い菱形。
            Vector3 at = target.FxPoint + Vector3.Up * 0.22f;
            Vector3 x = _camera.GlobalBasis.X * 0.62f, y = _camera.GlobalBasis.Y * 0.62f;
            foreach (var pair in new[] { (y, x), (x, -y), (-y, -x), (-x, y) })
                ShockMarkFx.Beam(root, at + pair.Item1, at + pair.Item1.Lerp(pair.Item2, 0.42f), ZanRed, 0.035f, 0.55 / speed);
            Float(target, "仇", ZanRed, true, 2.35f, 1.35f);
        }));
        sequence.TweenInterval(0.65 / speed);
        sequence.TweenCallback(Callable.From(root.QueueFree));
        Float(target, $"仇討ち ×{hits}", ZanRed, true, 3.75f, 1.1f);
        return duration;
    }

    private void DrawZanCut(Node3D root, BattlePawn3D target, int hits, int cut, int cuts, double speed)
    {
        const float slashScale = 1.20f;
        ZanSlashPlays++;
        bool heavy = hits >= 10 && cut == cuts - 1;
        float[] angles = hits >= 10 ? [15, 105, 45, 135, 75, 165, -12] : [30, -40, 5, 70, -70];
        float angle = Mathf.DegToRad(angles[cut]);
        Vector3 right = _camera.GlobalBasis.X, up = _camera.GlobalBasis.Y;
        Vector3 direction = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
        Vector3 at = target.FxPoint + Vector3.Up * 0.12f
            + (up * Mathf.Cos(angle) - right * Mathf.Sin(angle)) * ((cut % 3 - 1) * 0.09f);
        Color edge = hits >= 6 ? new Color("ff718b") : ZanRed;
        float reach = (heavy ? 1.65f : hits >= 6 ? 1.35f : 1.15f) * slashScale;
        var arc = ShockMarkFx.Sprite(root, at + _camera.GlobalBasis.Z * 0.10f,
            UiKit.LoadTexture("res://assets/fx/zan_dagger_arc.svg"), (heavy ? 3.4f : 2.7f) * slashScale, Colors.White);
        // 元絵の傾き約27度を補正し、カメラ面内で交差・放射状に回す。
        arc.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
        arc.GlobalBasis = _camera.GlobalBasis * new Basis(Vector3.Back, angle - Mathf.DegToRad(27));
        var fade = arc.CreateTween().SetParallel();
        fade.TweenProperty(arc, "scale", new Vector3(1.08f, heavy ? 1.05f : 0.85f, 1), 0.16 / speed);
        fade.TweenProperty(arc, "modulate:a", 0f, 0.19 / speed);
        fade.Finished += arc.QueueFree;
        ShockMarkFx.Beam(root, at - direction * reach, at + direction * reach, edge,
            (heavy ? 0.105f : hits >= 6 ? 0.055f : 0.040f) * slashScale, (heavy ? 0.22 : 0.17) / speed);
        ShockMarkFx.Sparks(root, at, new Color("ffd2d9"), heavy ? 10 : 5, heavy ? 0.85f : 0.55f, 0.20 / speed);
        // 乱れ斬りでも音の密度は従来どおり最大4斬撃＋締めの1音。
        if (cuts > 1 && cut == cuts - 1) _attackAudio.PlayZan(ZanSound.Finish);
        else if (cuts <= 5 || cut % 2 == 0) _attackAudio.PlayZan(ZanSound.Slash);
        if (heavy) FlashZanFinish(speed);
    }

    private void FlashZanFinish(double speed)
    {
        if (!_superFlashEnabled) return;
        ZanWhiteFlashes++;
        if (_zanFlash is null)
        {
            _zanFlash = new ColorRect { MouseFilter = MouseFilterEnum.Ignore };
            _zanFlash.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_zanFlash);
        }
        _zanFlashTween?.Kill();
        _zanFlash.Color = new Color(1, 0.95f, 0.96f, 0.16f);
        _zanFlash.Show();
        _zanFlashTween = _zanFlash.CreateTween();
        _zanFlashTween.TweenProperty(_zanFlash, "color:a", 0f, Math.Max(0.035, 0.09 / speed));
        _zanFlashTween.TweenCallback(Callable.From(_zanFlash.Hide));
    }

    internal void RecordZanHit(BattlePawn3D? actor, int damage)
    {
        ZanDamage += damage;
        if (actor is null) return;
        _zanTotals.TryGetValue(actor.InstanceId, out var previous);
        var label = previous.Label;
        if (label is null || !LivePopup(label))
        {
            label = CreatePopup(actor.Home + Vector3.Up * 3.18f, "", new Color("ffadb9"), 14);
            label.AddThemeStyleboxOverride("normal", UiKit.Box(new Color("21121d"), new Color("ab3152"), 1, 3));
        }
        int hits = previous.Hits + 1, total = previous.Damage + damage;
        label.Text = $"このターン 仇討ち×{hits}\n計 {total:N0}";
        _zanTotals[actor.InstanceId] = (hits, total, label);
    }

    private void PlaceZanTotals()
    {
        foreach (var entry in _zanTotals)
        {
            var pawn = FindPawn(entry.Key);
            if (pawn is not null && LivePopup(entry.Value.Label))
                entry.Value.Label.WorldPosition = pawn.GlobalPosition + Vector3.Up * 3.18f;
        }
    }

    internal void FinishZanFlurry(ZanPresentation.Flurry group)
    {
        var target = FindPawn(group.Enemy);
        if (target is not null)
            Float(target, group.Damage > 0 ? $"−{group.Damage:N0}  /  仇討ち×{group.Hits.Count}" : "仇討ち・防がれた",
                new Color("ff9dab"), true, 3.45f, 1.45f);
    }

    internal void ShowZanRecoil(BattlePawn3D? actor, double speed)
    {
        if (actor is null) return;
        ZanRecoilPlays++;
        ShockMarkFx.Sparks(_fxRoot, actor.FxPoint, new Color("b9193c"), 9, 0.42f, 0.22 / Math.Max(0.1, speed));
        _attackAudio.PlayZan(ZanSound.Recoil);
    }
}
