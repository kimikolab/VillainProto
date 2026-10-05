using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 駒の頭上の札（`PawnHud2D`）を画面の平面に並べる。<b>表示専用。</b>
/// 札は 3D の木ではなくここ（`_hudLayer`）にいて、毎フレーム駒の頭の位置へ写す。
/// 層は 3D の枠のすぐ上・見出しやカットインより下に置く（見せ場の帯が札で欠けないように）。
///
/// <para>詳細（名前・HP の数字・攻撃）は<b>カーソルを合わせた駒</b>と、<b>Alt を押している間の全員</b>に出す。
/// 合わせた駒の札は最前面へ上げる。</para>
/// </summary>
public partial class BattlefieldView3D
{
    private Control _hudLayer = null!;
    private readonly List<PawnHud2D> _hudOrder = new();
    /// <summary>いまカーソルが乗っている駒（頭なしの門が読む）。</summary>
    internal BattlePawn3D? HoveredPawn { get; private set; }

    private void BuildHudLayer()
    {
        _hudLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _hudLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_hudLayer);
    }

    private void AdoptHud(BattlePawn3D pawn)
    {
        _hudLayer.AddChild(pawn.Hud);
        PlaceHud(pawn);
    }

    private void PlacePawnHuds(double delta)
    {
        if (_hudLayer is null || _pawns.Count == 0) return;
        foreach (BattlePawn3D pawn in _pawns.Values) PlaceHud(pawn);

        HoveredPawn = PawnUnderMouse();
        bool showAll = IsVisibleInTree() && Input.IsKeyPressed(Key.Alt);
        foreach (BattlePawn3D pawn in _pawns.Values)
            if (IsInstanceValid(pawn.Hud)) pawn.Hud.Expanded = showAll || pawn == HoveredPawn;
        AvoidHudOverlaps((float)delta);

        // 奥の駒の札を先に描く（手前の札が上に重なる）。カーソルの駒は最前面。並びが変わったときだけ入れ替える。
        var order = _pawns.Values
            .Where(p => IsInstanceValid(p.Hud) && p.Hud.GetParent() == _hudLayer)
            .OrderBy(p => p == HoveredPawn)
            .ThenByDescending(p => _camera.GlobalPosition.DistanceSquaredTo(p.HudAnchor))
            .Select(p => p.Hud).ToList();
        if (order.SequenceEqual(_hudOrder)) return;
        _hudOrder.Clear();
        _hudOrder.AddRange(order);
        for (int i = 0; i < order.Count; i++) _hudLayer.MoveChild(order[i], i);
    }

    private void PlaceHud(BattlePawn3D pawn)
    {
        PawnHud2D hud = pawn.Hud;
        if (!IsInstanceValid(hud)) return;
        Vector3 anchor = pawn.HudAnchor;
        bool onScreen = pawn.IsVisibleInTree() && !_camera.IsPositionBehind(anchor);
        hud.Visible = onScreen;
        if (onScreen) hud.Position = _camera.UnprojectPosition(anchor).Round() + hud.Nudge.Round();
    }

    // ---- 重なり回避 ------------------------------------------------------------------------------
    //
    // 札は頭の真上が定位置。重なるときだけ小さくずらす。
    // - 置く順は「カーソルの駒 → カメラに近い駒」。手前の札は動かず、奥の札がよける。
    // - ずらし幅に上限を付ける（全員を際限なく押し上げると、上空に札が積もって持ち主が分からなくなる）。
    // - 前のフレームのずらしがまだ空いていればそれを使う（毎フレーム入れ替わってちらつかないように）。
    // - 実際の位置は目標へ短く寄せる。大きくずれた札は頭へ細い線を引く（`PawnHud2D._Draw`）。
    // - どこにも空きが無ければ、重なりが最も小さい位置で妥協する。

    private const float HudGap = 2f;
    private const float HudStep = 6f;
    private const float HudMaxUp = 54f;
    private const float HudMaxDown = 12f;
    private const float HudMaxSide = 40f;
    /// <summary>詳細を開いた札は大きいので、横へ逃がせる幅を広げる（細い線で持ち主は追える）。</summary>
    private const float HudMaxSideExpanded = 100f;
    private readonly Dictionary<PawnHud2D, Vector2> _hudTargets = new();
    private static readonly Vector2[] HudCandidates = BuildHudCandidates(HudMaxSide);
    private static readonly Vector2[] HudCandidatesExpanded = BuildHudCandidates(HudMaxSideExpanded);

    private static Vector2[] BuildHudCandidates(float maxSide)
    {
        // 縦だけの候補を先に（近い順）、次に横を混ぜた候補。
        var vertical = new List<float> { 0 };
        for (float d = HudStep; d <= HudMaxUp; d += HudStep)
        {
            vertical.Add(-d);
            if (d <= HudMaxDown) vertical.Add(d);
        }
        var list = vertical.Select(y => new Vector2(0, y)).ToList();
        for (float x = 20f; x <= maxSide; x += 20f)
            foreach (float y in vertical)
            {
                list.Add(new Vector2(-x, y));
                list.Add(new Vector2(x, y));
            }
        return list.ToArray();
    }

    private void AvoidHudOverlaps(float delta)
    {
        var placed = new List<Rect2>();
        var live = new HashSet<PawnHud2D>();
        var order = _pawns.Values
            .Where(p => IsInstanceValid(p.Hud) && p.Hud.Visible && p.Hud.Shown)
            .OrderBy(p => p != HoveredPawn)
            .ThenBy(p => _camera.GlobalPosition.DistanceSquaredTo(p.HudAnchor));
        foreach (BattlePawn3D pawn in order)
        {
            PawnHud2D hud = pawn.Hud;
            live.Add(hud);
            Vector2 home = hud.Position - hud.Nudge.Round();
            Rect2 shape = hud.Footprint.Grow(HudGap);
            Vector2 previous = _hudTargets.GetValueOrDefault(hud);
            Vector2[] candidates = hud.Expanded ? HudCandidatesExpanded : HudCandidates;

            Vector2 target = previous;
            if (Overlap(home + previous, shape, placed) > 0)
            {
                target = Vector2.Zero;
                float bestOverlap = float.MaxValue;
                foreach (Vector2 candidate in candidates)
                {
                    float overlap = Overlap(home + candidate, shape, placed);
                    if (overlap <= 0) { target = candidate; bestOverlap = 0; break; }
                    if (overlap < bestOverlap - 1) { target = candidate; bestOverlap = overlap; }
                }
            }
            else if (previous != Vector2.Zero)
            {
                // 空いたら定位置へ戻れるか試す（戻れるなら戻る）。
                foreach (Vector2 candidate in candidates)
                {
                    if (candidate.LengthSquared() >= previous.LengthSquared()) continue;
                    if (Overlap(home + candidate, shape, placed) <= 0) { target = candidate; break; }
                }
            }
            _hudTargets[hud] = target;
            placed.Add(new Rect2(home + target + shape.Position, shape.Size));

            // 目標へ短く寄せる（約 0.1 秒）。
            Vector2 nudge = hud.Nudge;
            nudge = nudge.Lerp(target, 1f - Mathf.Exp(-delta * 22f));
            if (nudge.DistanceSquaredTo(target) < 0.25f) nudge = target;
            hud.Nudge = nudge;
            hud.Position = home + nudge.Round();
        }
        foreach (PawnHud2D stale in _hudTargets.Keys.Where(h => !live.Contains(h)).ToArray())
        {
            _hudTargets.Remove(stale);
            if (IsInstanceValid(stale)) stale.Nudge = Vector2.Zero;
        }
    }

    private static float Overlap(Vector2 origin, Rect2 shape, List<Rect2> placed)
    {
        var rect = new Rect2(origin + shape.Position, shape.Size);
        float total = 0;
        foreach (Rect2 other in placed)
        {
            Rect2 hit = rect.Intersection(other);
            total += hit.Size.X * hit.Size.Y;
        }
        return total;
    }

    /// <summary>
    /// カーソルが乗っている駒。札から足元までの縦長の範囲で当て、重なったらカメラに近い駒を採る。
    /// 立ち絵の透明部分まで含むが、選ぶのは表示だけなので粗くてよい。
    /// </summary>
    private BattlePawn3D? PawnUnderMouse()
    {
        if (!IsVisibleInTree()) return null;
        Vector2 mouse = GetLocalMousePosition();
        if (!new Rect2(Vector2.Zero, Size).HasPoint(mouse)) return null;
        BattlePawn3D? best = null;
        float bestDistance = float.MaxValue;
        foreach (BattlePawn3D pawn in _pawns.Values)
        {
            if (pawn.Hp <= 0 || !IsInstanceValid(pawn.Hud) || !pawn.Hud.Visible || !pawn.Hud.Shown) continue;
            if (_camera.IsPositionBehind(pawn.GlobalPosition)) continue;
            Vector2 head = _camera.UnprojectPosition(pawn.HudAnchor);
            Vector2 feet = _camera.UnprojectPosition(pawn.GlobalPosition);
            float halfWidth = Mathf.Max(36f, (feet.Y - head.Y) * 0.24f);
            if (Mathf.Abs(mouse.X - head.X) > halfWidth || mouse.Y < head.Y - 24 || mouse.Y > feet.Y + 12) continue;
            float distance = _camera.GlobalPosition.DistanceSquaredTo(pawn.GlobalPosition);
            if (distance < bestDistance) { best = pawn; bestDistance = distance; }
        }
        return best;
    }
}
