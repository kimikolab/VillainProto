using BattleCore;
using Godot;
using System;
using System.Collections.Generic;

public partial class BattlePawn3D
{
    private readonly List<Sprite3D> _plankPieces = new();
    private readonly List<Sprite3D> _scrapPieces = new();
    private int _plankAmount;
    private int _displayedArmor;
    public event Action? ArmorDepleted;
    // 台本に載った残量だけを見る。死亡・勝利の掃除では発火しない。
    public void ObserveArmor(int remaining)
    {
        remaining = Math.Max(0, remaining);
        if (_alive && !_victory && _displayedArmor > 0 && remaining == 0) ArmorDepleted?.Invoke();
        _displayedArmor = remaining;
    }
    private float _plankEmber;
    private Vector3? _plankAidOrigin;
    private float _plankWorkPulse;
    internal int PlankCraftTier { get; private set; }
    internal bool PlankAidActive => _plankAidOrigin is not null;

    public void RushToPlank(BattlePawn3D target)
    {
        if (!_alive || _victory) return;
        _plankAidOrigin = Position;
        if (target == this) return; // 自分への応急処置ではその場で打つ。
        var delta = target.Position - Position;
        float distance = delta.Length();
        // 大きな背負子で患者を隠さないよう、画面上の横・少し手前で作業する。
        var camera = GetViewport().GetCamera3D();
        var right = camera?.GlobalBasis.X ?? Vector3.Right;
        var near = camera?.GlobalBasis.Z ?? Vector3.Back;
        right.Y = near.Y = 0;
        var destination = distance > 1.4f
            ? target.Position + right.Normalized() * (Team == 0 ? -1.25f : 1.25f) + near.Normalized() * 0.35f
            : Position;
        BeginMotion().TweenProperty(this, "position", destination, 0.10 / Math.Max(0.1, AnimationSpeed))
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
    public void HammerPlank() => _plankWorkPulse = 0.085f;
    public void ReturnFromPlank()
    {
        if (_plankAidOrigin is not { } origin) return;
        _plankAidOrigin = null;
        if (!_alive || _victory) return;
        BeginMotion().TweenProperty(this, "position", origin, 0.12 / Math.Max(0.1, AnimationSpeed))
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
    }
    public void RaisePlankCraft(int tier)
    {
        PlankCraftTier = Math.Max(PlankCraftTier, tier);
        HammerPlank();
    }
    private void ProcessPlankWork(float delta)
    {
        float pulse = Mathf.Sin(_plankWorkPulse / 0.085f * Mathf.Pi);
        float direction = Team == 0 ? 1 : -1;
        _sprite.Rotation += new Vector3(0, 0, -direction * pulse * 0.12f);
        _sprite.Position += new Vector3(direction * pulse * 0.055f, -pulse * 0.025f, 0);
        _plankWorkPulse = Mathf.Max(0, _plankWorkPulse - delta);
    }
    internal int PlankPieceCount => _plankPieces.Count;
    public Vector3 ScrapPoint => UnitId == "tsugi" ? PortraitPoint(0.30f, 0.30f) : FxPoint;

    public void SetPlank(int amount)
    {
        if (!_alive || _victory) { _plankAidOrigin = null; _plankWorkPulse = 0; }
        _plankAmount = _alive && !_victory ? Math.Max(0, amount) : 0;
        int count = _plankAmount == 0 ? 0 : Math.Clamp(1 + _plankAmount / 15, 1, 6);
        ResizePieces(_plankPieces, count);
        SetStatusIcon(StatusKeys.Plank, _plankAmount > 0);
        UpdatePlank(0);
    }
    public void SetScrapStock(int amount)
    {
        ResizePieces(_scrapPieces, !_alive || _victory || amount <= 0 ? 0 : Math.Clamp(1 + amount / 15, 1, 4));
        UpdatePlank(0);
    }
    private void ResizePieces(List<Sprite3D> pieces, int count)
    {
        while (pieces.Count > count)
        { var last = pieces[^1]; last.Visible = false; last.QueueFree(); pieces.RemoveAt(pieces.Count - 1); }
        while (pieces.Count < count)
        {
            var piece = LiliFx.Sprite(this, FxPoint, PlankFx.Piece(pieces.Count), 0.29f);
            piece.RenderPriority = 4;
            pieces.Add(piece);
        }
    }
    private void UpdatePlank(float delta)
    {
        for (int i = 0; i < _plankPieces.Count; i++)
        {
            var piece = _plankPieces[i];
            piece.GlobalPosition = PortraitPoint(0.46f + (i % 3 - 1) * 0.085f, 0.49f + i / 3 * 0.08f);
            piece.Rotation = new Vector3(0, 0, (i % 2 == 0 ? 1 : -1) * 0.20f);
            piece.Modulate = _burning ? new Color("685044") : Colors.White;
        }
        for (int i = 0; i < _scrapPieces.Count; i++)
            _scrapPieces[i].GlobalPosition = ScrapPoint + new Vector3((i - 1) * 0.1f, i * 0.07f, 0);
        _plankEmber -= delta;
        if (_burning && _plankAmount > 0 && _plankEmber <= 0)
        {
            _plankEmber = 0.45f;
            PlankFx.Burst(this, FxPoint, 0, 0.35 / Math.Max(0.1, AnimationSpeed), true);
        }
    }
    public void PlankKnockout()
    {
        // AnimateDeath の後で動きだけ差し替える。死亡状態・UI の掃除は既存経路に任せる。
        double seconds = 0.38 / Math.Max(0.1, AnimationSpeed);
        _hpBack.Visible = _hpFill.Visible = false;
        var tween = BeginMotion().SetParallel();
        tween.TweenProperty(this, "position", Position + new Vector3(Team == 0 ? -0.85f : 0.85f, 0.12f, 0.2f), seconds);
        tween.TweenProperty(this, "rotation:z", Team == 0 ? 0.65f : -0.65f, seconds);
        tween.TweenProperty(_sprite, "modulate:a", 0f, seconds);
    }
}
