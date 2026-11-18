using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// =====================================================================================
// 1-3「狩る者」の演出（第480期・指示書 design/PHASE480_STAGE13_HUNTER_SPEC.md §3-1 ／ §3-2・3D の層）。**画面は判定を1つも持たない。**
//
//   登場: 時計が止まる → 視点が門へ → 白い閃光 → 青白い落雷 → 門の扉が吹き飛ぶ → 勇者が歩み出る → 帯「白雷の勇者　ライゼル」
//         → 曲が替わる → セリフ 2 行 → 目標の札が赤く「生きて拠点へ戻れ」 → 視点が 勇者 → いちばん近い隊 → 拠点 → 時計が動かせる
//   追われている間: 勇者の進む先の太い赤の点線（2 歩ぶん先まで濃く）・次の刻で追いつかれる隊に赤い目の印・拠点に入った隊に「生還」
//                   画面の上に勇者の HP の棒（減った分は赤く残る・半分の所に線）
//   退いたとき: 勇者が門へ歩いて戻る・帯「勇者は退いた」・曲が戻る
//
// 読むのは `Map11State` の今の盤だけ（`Rout` ／ `HunterChase` ／ `Hero`）。文は `Map11Lines`（仮）。
// =====================================================================================
public partial class Map11Main
{
    /// <summary>通しに結び付けて覚える（戦闘から戻るたびに場面は読み直されるので、登場を繰り返さない）。</summary>
    private sealed class HunterSeen
    {
        public bool Entrance, Stepped, Routed;
        public HashSet<int> Home = new();
    }
    private static readonly ConditionalWeakTable<Map11State, HunterSeen> HunterSeenOf = new();
    private static HunterSeen Hs => HunterSeenOf.GetValue(St, _ => new HunterSeen());

    private static bool HunterBoard => St.Stage.Hero.Hunter;
    /// <summary>登場の演出・退く演出の最中か（時計・命令・遊ぶ口はこのあいだ待つ）。</summary>
    internal bool HunterBusy { get; private set; }
    private bool _hunterSkip, _hunterLeaving;
    private HunterOverlay? _hunterOver;
    private readonly List<MeshInstance3D> _hunterDots = new();
    private BattleAttackAudio? _hunterAudio;
    /// <summary>絵の口・検査が登場の演出を自分で流すとき（<see cref="_linesOff"/> でも飛ばさない）。</summary>
    private bool _hunterForce;
    /// <summary>登場の演出がどこまで進んだか（検査と遊ぶ口が読む）。</summary>
    internal string HunterBeat { get; private set; } = "";
    internal bool HunterEyeShown => _hunterOver is { } o && o.Marks.Any(m => m.Kind == HunterOverlay.MarkKind.Eye);
    internal int HunterHomeMarks => _hunterOver?.Marks.Count(m => m.Kind == HunterOverlay.MarkKind.Home) ?? 0;
    internal int HunterDotCount => _hunterDots.Count(d => d.Visible);
    internal Rect2 HunterBarRect => _hunterOver?.BarRect ?? default;
    internal bool HunterBarShown => _hunterOver is { BarShown: true };

    /// <summary>勇者が退いた帯と結果の題（追記 4: 外へ出した隊の駒が 1 枚も生きていなければ「辛くも退けた」・規則は同じ勝ち B）。</summary>
    private static string HunterGoneText => St.HunterNarrow ? Map11Lines.HunterNarrow : Map11Lines.HunterGone;

    // ---- 殿に出す（追記 4）: 撤退中に勇者の駒に触れると、選んでいる隊から勇者までの点線と「殿に出す」の札。押すと行き先が勇者になる ----
    private bool _rearHoverOn;
    private bool RearPickable(Map11State.Node foe)
        => HunterBoard && foe is { IsHero: true, Cleared: false } && St.Rout && Hs.Stepped && !St.Finished && !Map11Session.TurnRunning && !HunterBusy
           && _selected >= 0 && _selected < St.Squads.Length && !St.Squads[_selected].Lost && St.CanSend(_selected) && St.Squads[_selected].Pos != foe.Pos;
    /// <summary>勇者の駒に触れているあいだの行き先の札（道の点線が読む）。</summary>
    private DestButton? RearHover
        => _rearHoverOn && St.Hero is { } h && RearPickable(h) && Map11Session.RearDest(_selected) is { } d ? _destButtons.FirstOrDefault(b => b.Dest == d) : null;
    internal bool RearMarkShown => _hunterOver is { } o && o.Marks.Any(m => m.Kind == HunterOverlay.MarkKind.Rear);
    private void PickRear()
    {
        if (!Map11Session.SetRear(_selected)) return;
        _guideSquadChosen = _guideDestinationChosen = true;
        GD.Print($"MAP3D_HUNTER_REAR {St.Squads[_selected].Label} を殿に出す（行き先 {Map11Orders.Where(St.Graph, Map11Session.Destinations[_selected])}）");
        Refresh();
    }

    /// <summary>勇者の駒をまだ盤に出さないか（登場の演出で歩み出るまで）。</summary>
    private static bool HunterHidden(Map11State.Node foe) => HunterBoard && foe.IsHero && !Hs.Stepped;
    /// <summary>撤退の局面を画面に出してよいか（登場の演出が目標の札を替える所まで進んだ後）。</summary>
    private bool HunterRoutShown => HunterBoard && St.Rout && Hs.Stepped && HunterBeat is not ("門へ" or "落雷" or "扉" or "歩み出る" or "帯" or "セリフ");

    private void BuildHunter3D()
    {
        if (_map3d is not null) MapMusic.Play(this, St.Finished ? (St.Won ? MapMusic.Track.Win : MapMusic.Track.Lose) : HunterBoard && St.Rout ? MapMusic.Track.Hero : MapMusic.Track.Normal);
        HunterArt.Use(HunterBoard && _map3d is not null ? St.Stage.Hero.Unit?.Id : null);   // 追記 4: 立ち絵があれば駒・会戦の台・帯に使う
        if (!HunterBoard || _map3d is null) return;
        var layer = new CanvasLayer { Layer = 9, Name = "HunterLayer" };
        AddChild(layer);
        _hunterOver = new HunterOverlay();
        layer.AddChild(_hunterOver);
        _hunterOver.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _hunterOver.Skip += () => _hunterSkip = true;
        // 戦闘から戻った・やり直した: もう登場を見せた通しなら、扉は吹き飛んだ後。
        if (St.Rout && Hs.Entrance) _map3d.BlowCampGate(instant: true);
    }

    private async Task HWait(double seconds)
    {
        double left = seconds;
        while (left > 0 && !_hunterSkip && IsInsideTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            left -= GetProcessDeltaTime();
        }
    }

    private Map3DPiece? HeroPiece()
    {
        foreach (var p in _mapPicks)
            if (p.Foe is { IsHero: true } && _pieces3d.TryGetValue(p.Marker, out Map3DPiece? piece)) return piece;
        return null;
    }

    private void HunterSound(ShockMarkSound cue)
    {
        try
        {
            if (_hunterAudio is null) { _hunterAudio = new BattleAttackAudio(); AddChild(_hunterAudio); }
            _hunterAudio.PlayShockMark(cue);
        }
        catch (Exception e) { GD.PrintErr($"MAP3D_HUNTER_SOUND {cue} {e.Message}"); }
    }

    /// <summary>
    /// 勇者の登場（指示書 §3-1）。説明の窓は出さない。押すと残りの待ちを飛ばす（扉・帯・目標の札は必ず通る）。
    /// </summary>
    private async Task HunterEntrance()
    {
        if (HunterBusy || _map3d is null || _cam3d is null || _hunterOver is null || _hud3d is null) return;
        HunterBusy = true;
        _hunterSkip = false;
        HunterSeen hs = Hs;
        hs.Entrance = true;
        Map11Clock.Stop();
        _hunterOver.Block(true);
        _hud3d.HideSay();
        GD.Print($"MAP3D_HUNTER_ENTRANCE turn={Map11Session.ShownTurn} gate={St.GateOpen} hero={St.Hero?.HpNow}");
        try
        {
            // 1) 視点が門へ滑る（0.8 秒）
            HunterBeat = "門へ";
            Vector3 gate = _map3d.CampGate ?? PortalPos;
            _cam3d.FocusOn(gate + new Vector3(-4, 0, 0));
            await HWait(0.8);
            // 2) 白い閃光 → 青白い落雷 → 扉が内側から吹き飛ぶ・土煙・画面が揺れる。勇者の駒が歩み出る
            HunterBeat = "落雷";
            _hunterOver.Flash(0.9f, 0.45f);
            await HWait(0.12);
            _hunterOver.Bolt(_camera.UnprojectPosition(gate + new Vector3(0, 0.6f, 0)), 0.5f);
            HunterSound(ShockMarkSound.ThunderHeavy);
            await HWait(0.16);
            _map3d.BlowCampGate();
            MapStagingAudio.Play(this, MapStageCue.Kind.Fall);
            _cam3d.Shake(0.5f, 0.55f);
            HunterBeat = "扉";
            await HWait(0.35);
            hs.Stepped = true;
            Refresh();
            if (HeroPiece() is { } piece)
            {
                Vector3 stand = piece.GlobalPosition;
                piece.Snap((_layout3d?.Sheet?.Camp ?? PortalPos) + new Vector3(-2.2f, 0, 0));
                piece.Walk(new[] { gate + new Vector3(-0.6f, 0, 0), stand with { Y = PosOf(St.Graph.Portal).Y } }, 1.1f);
            }
            HunterBeat = "歩み出る";
            await HWait(1.1);
            // 3) 画面を横切る帯（1.5 秒・王冠は 3 つ）
            HunterBeat = "帯";
            _hunterOver.Band(Map11Lines.HunterName, 3, Color.FromHtml("#dff1ff"), 1.5f);
            // 4) 曲が緊迫の曲に替わる
            MapMusic.Play(this, MapMusic.Track.Hero);
            await HWait(1.5);
            // 5) セリフ 2 行（顔つき・各 2.5 秒）
            HunterBeat = "セリフ";
            if (Map11Lines.SpeakerOf(St, Map11Lines.HunterWarnSpeaker) is { } who)
            {
                _hud3d.Say(who, $"「{Map11Lines.HunterWarn}」", 2.5f);
                GD.Print($"MAP3D_SAY key=1-3.hunter.warn who={who.Name} text={Map11Lines.HunterWarn}");
                await HWait(2.5);
            }
            _hud3d.SayInner($"（{Map11Lines.HunterInner}）", 2.5f);
            GD.Print($"MAP3D_SAY key=1-3.hunter.inner who=— text={Map11Lines.HunterInner}");
            await HWait(2.5);
            _hud3d.HideSay();
            // 6) 目標の札が赤く替わる（`RefreshHud3D` が盤を読んで替える）。視点が 勇者 → いちばん近い隊 → 拠点
            HunterBeat = "目標";
            Refresh();
            _hud3d.PulseGoal();
            if (St.HunterChase() is { } chase)
            {
                _cam3d.FocusOn(PosOf(St.Squads[chase.Squad].Pos));
                await HWait(1.0);
            }
            _cam3d.FocusOn(PosOf(St.Graph.Home) + new Vector3(8, 0, 0));
            await HWait(1.0);
        }
        catch (Exception e) { GD.PrintErr($"MAP3D_HUNTER_ENTRANCE_FAILED {e}"); }
        finally
        {
            hs.Stepped = true;
            if (IsInsideTree())
            {
                _map3d.BlowCampGate(instant: true);
                _hunterOver.Block(false);
                HunterBusy = false;
                HunterBeat = "終わり";
                Refresh();
            }
        }
    }

    /// <summary>勇者が退いた（HP が半分を割った ／ 0 になった）: 勇者が門へ歩いて戻る・帯「勇者は退いた」・曲が戻る。終わったら結果へ。</summary>
    private async Task HunterRouted()
    {
        if (_hunterOver is null || _cam3d is null || _map3d is null) { Pump(); return; }
        HunterBusy = true;
        _hunterSkip = false;
        Hs.Routed = true;
        _hunterLeaving = true;
        HunterBeat = "退く";
        _hunterOver.Block(true);
        GD.Print($"MAP3D_HUNTER_ROUTED turn={Map11Session.ShownTurn} hp={St.Hero?.HpNow}");
        try
        {
            Refresh();
            Vector3 gate = _map3d.CampGate ?? PortalPos;
            if (HeroPiece() is { } piece)
            {
                _cam3d.FocusOn(piece.GlobalPosition);
                await HWait(0.6);
                piece.Walk(new[] { gate }, 1.6f);
            }
            _hunterOver.Band(HunterGoneText, 0, Color.FromHtml("#f1c96b"), 1.8f);
            MapStagingAudio.Play(this, MapStageCue.Kind.Open);
            MapMusic.Play(this, MapMusic.Track.Win);
            await HWait(1.9);
        }
        catch (Exception e) { GD.PrintErr($"MAP3D_HUNTER_ROUTED_FAILED {e}"); }
        finally
        {
            _hunterLeaving = false;
            if (IsInsideTree())
            {
                _hunterOver.Block(false);
                HunterBusy = false;
                Refresh();
                Pump();
            }
        }
    }

    /// <summary>毎フレーム（`ProcessMap3D` から）: 登場のきっかけ・赤い点線・目の印・「生還」・勇者の HP の棒。</summary>
    private void ProcessHunter3D(float delta)
    {
        if (!HunterBoard || _hunterOver is null || _map3d is null || _layout3d is null) return;
        HunterSeen hs = Hs;
        bool quiet = !Map11Session.TurnRunning && !CoverShown && !ClashShown && !_reform;
        if (St.Rout && !hs.Entrance && !HunterBusy)
        {
            if ((_linesOff && !_hunterForce) || St.Finished)
            {
                // 絵の口・検査（器具が盤を進める通し）と、同じ刻に終わった通しは演出を飛ばす。
                hs.Entrance = hs.Stepped = true;
                _map3d.BlowCampGate(instant: true);
                Refresh();
            }
            else if (quiet) _ = HunterEntrance();
        }
        bool live = St.Rout && hs.Stepped && !St.Finished && St.Hero is { Cleared: false };
        // ---- 勇者の進む先: 太い赤の点線（2 歩ぶん先まで濃く・その先は細く） ----
        int used = 0;
        _hunterOver.Marks.Clear();
        if (live && !HunterBusy && !ClashShown && !CoverShown && !_reform && St.Hero is { } h && St.HunterChase() is { } chase)
        {
            Map11State.Squad target = St.Squads[chase.Squad];
            List<int> cells = St.Graph.Path(h.Pos, h.Pos == St.Graph.Portal ? -1 : h.Def.Road, target.Pos, -1);
            if (cells.Count > 1)
            {
                List<Vector3> line = _layout3d.Trace(cells);
                int steps = St.HunterStepsNext;   // 追記 4 の (ウ): 初めて歩く刻は 1 歩
                float near = Map3DLayout.LengthOf(_layout3d.Trace(cells.Take(Math.Min(cells.Count, steps + 1)).ToList()));
                float length = Map3DLayout.LengthOf(line);
                const float gap = 1.15f;
                float phase = _clock3d * 2.2f % gap;
                for (float at = phase; at < length - 1.2f && used < 120; at += gap, used++)
                {
                    if (used >= _hunterDots.Count)
                    {
                        var dot = new MeshInstance3D
                        {
                            Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.06f, RadialSegments = 10 },
                            MaterialOverride = new StandardMaterial3D
                            {
                                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Color.FromHtml("#ff3524"),
                                EmissionEnabled = true, Emission = Color.FromHtml("#ff3524"), EmissionEnergyMultiplier = 1.8f,
                            },
                            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                        };
                        _map3d.AddChild(dot);
                        _hunterDots.Add(dot);
                    }
                    bool strong = at <= near;
                    _hunterDots[used].Visible = true;
                    _hunterDots[used].Scale = strong ? new Vector3(2.3f, 1, 2.3f) : new Vector3(0.95f, 1, 0.95f);
                    _hunterDots[used].Position = Map3DLayout.Along(line, at) + new Vector3(0, 0.26f, 0);
                }
            }
            // 次の刻で追いつかれる隊に赤い目の印（文なし）。
            if (chase.Steps <= St.HunterStepsNext && OnScreen(ClashPos(null, chase.Squad) + new Vector3(0, 6.4f, 0)) is { } eye)
                _hunterOver.Marks.Add((eye, HunterOverlay.MarkKind.Eye));
        }
        for (int i = used; i < _hunterDots.Count; i++) _hunterDots[i].Visible = false;
        // ---- 殿に出す: 勇者の駒に触れているあいだ ／ 選んでいる隊が殿のあいだ、勇者の駒の上に札 ----
        if (live && !HunterBusy && !ClashShown && !CoverShown && !_reform && St.Hero is { } rh && (RearHover is not null || (Map11Session.IsRear(_selected) && St.Squads[_selected].Pos != rh.Pos))
            && HeroPiece() is { } rearPiece && OnScreen(rearPiece.GlobalPosition + new Vector3(0, 7.6f, 0)) is { } rearAt)
            _hunterOver.Marks.Add((rearAt, HunterOverlay.MarkKind.Rear));
        // ---- 拠点に入った隊: 短い音と、名札に「生還」 ----
        if (St.Rout && hs.Stepped)
        {
            foreach (Map11State.Squad s in St.Squads)
            {
                if (s.Lost || s.Pos != St.Graph.Home || s.Units is not { } u || !u.Any(x => x.IsAlive) || St.IsLocked(s.Index)) { hs.Home.Remove(s.Index); continue; }
                if (hs.Home.Add(s.Index) && !HunterBusy && !_linesOff)
                {
                    HunterSound(ShockMarkSound.RallyHeal);
                    GD.Print($"MAP3D_HUNTER_HOME {s.Label} {u.Count(x => x.IsAlive)} 枚 {Map11Session.ShownTurn} 刻目");
                }
                if (!ClashShown && !CoverShown && !_reform && OnScreen(ClashPos(null, s.Index) + new Vector3(0, 6.4f, 0)) is { } at)
                    _hunterOver.Marks.Add((at, HunterOverlay.MarkKind.Home));
            }
        }
        // ---- 勇者の HP の棒（画面の上・撤退の局面のあいだ） ----
        bool bar = St.Rout && hs.Stepped && St.Hero is not null && !CoverShown && !_reform;
        if (bar && St.Hero is { } hero)
            _hunterOver.SetBar(Map11Lines.HunterName, hero.HpNow, hero.DefMaxHp, St.Stage.Hero.RoutPercent);
        _hunterOver.BarShown = bar;
        _hunterOver.Tick(delta);
        // 足止めの印の裏の数えが終わったら、隊の札を引き直す。
        if (_holdDone.Count != _holdSeen) { _holdSeen = _holdDone.Count; RefreshHud3D(); }
    }

    private Vector2? OnScreen(Vector3 world)
    {
        if (_camera.IsPositionBehind(world)) return null;
        Vector2 p = _camera.UnprojectPosition(world), view = GetViewport().GetVisibleRect().Size;
        return p.X < 20 || p.Y < 60 || p.X > view.X - 20 || p.Y > view.Y - 150 ? null : p;
    }

    // ---------------- 足止めの印（会戦の台・隊の札） ----------------

    /// <summary>足止めの印（裏の 20 回のうち、足止めに要るターン数以上続いた回数 → 文と色）。線は指示書の追記 3（16 以上 ／ 7 以下）。</summary>
    internal static (string Text, string Color) HoldOf(int held)
        => held >= OddsGood ? ("足止めできる", "#71d7a1") : held < OddsEven ? ("足止めできない", "#ff6b5e") : ("五分", "#f1c96b");

    private readonly HashSet<string> _holdRunning = new();
    private int _holdSeen;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _holdDone = new();

    /// <summary>
    /// その隊がいま勇者に当たったら足を止められるか（撤退の局面のあいだ・隊の札に出す）。裏で 20 回回して数えるだけで、盤は 1 ビットも動かさない。
    /// 数え終わるまでは null。
    /// </summary>
    private int? HoldMark(int squad)
    {
        if (!HunterBoard || !St.Rout || St.Hero is not { Cleared: false } hero || St.Stage.Hero.HoldTurns <= 0) return null;
        Map11State.Squad s = St.Squads[squad];
        if (s.Lost || s.Units is not { } units || !units.Any(u => u.IsAlive)) return null;
        string key = $"{squad}:{hero.HpNow}:" + string.Join(",", units.Where(u => u.IsAlive).OrderBy(u => u.Slot).Select(u => $"{u.Slot}.{u.Def.Id}.{u.Hp}.{u.MaxHp}"));
        if (_holdDone.TryGetValue(key, out int held)) return held;
        if (_holdRunning.Add(key))
        {
            var own = St.PeekSquad(squad).Where(u => u.IsAlive).Select(Map11State.CloneForOdds).ToList();
            var foe = St.PeekFoe(hero).Where(u => u.IsAlive).Select(Map11State.CloneForOdds).ToList();
            int need = St.Stage.Hero.HoldTurns;
            Task.Run(() =>
            {
                int n = 0;
                try
                {
                    for (int k = 0; k < OddsRuns; k++)
                        if (BattleEngine.Run(own.Select(Map11State.CloneForOdds).ToList(), foe.Select(Map11State.CloneForOdds).ToList(), 7919 * k + 13, verbose: false).Turns >= need) n++;
                }
                catch (Exception) { n = -1; }
                _holdDone[key] = n;
            });
        }
        return null;
    }
}

/// <summary>
/// 敵部隊の特徴の印（第480期 §3-5・文なし）: 雷 ／ 火 ／ 毒 ／ 盾 ／ 弓 ／ 槍。暗い丸の上に色つきの形を描くだけ。
/// 会戦の台の敵の顔と、盤の上の敵の名札が同じ絵を使う。
/// </summary>
internal static class FoeTraitIcon
{
    internal static Color ColorOf(string key) => Color.FromHtml(key switch
    {
        "雷" => "#ffe24a", "火" => "#ff7a2a", "毒" => "#7ad65a", "盾" => "#9fb6d6", "弓" => "#e8d9a8", "槍" => "#d7dde6", _ => "#ffffff",
    });

    internal static void Draw(CanvasItem on, Vector2 c, float r, string key)
    {
        if (key.Length == 0) return;
        Color ink = ColorOf(key);
        on.DrawCircle(c, r, new Color(0.05f, 0.05f, 0.07f, 0.96f));
        on.DrawArc(c, r, 0, Mathf.Tau, 28, ink, Math.Max(1.5f, r * 0.13f), true);
        float s = r / 10f;
        Vector2 P(float x, float y) => c + new Vector2(x, y) * s;
        switch (key)
        {
            case "雷":
                on.DrawColoredPolygon(new[] { P(1.5f, -7), P(-4, 1), P(-0.5f, 1), P(-2, 7), P(4.5f, -1.5f), P(0.8f, -1.5f) }, ink);
                break;
            case "火":
                on.DrawColoredPolygon(new[] { P(0, -7), P(3.2f, -2.5f), P(4.6f, 1.5f), P(3, 5), P(0, 6.5f), P(-3, 5), P(-4.6f, 1.5f), P(-2.2f, -1.5f), P(-1.2f, 1) }, ink);
                on.DrawColoredPolygon(new[] { P(0, -0.5f), P(1.8f, 2.6f), P(0, 5), P(-1.8f, 2.6f) }, Color.FromHtml("#ffe9a8"));
                break;
            case "毒":
                on.DrawColoredPolygon(new[] { P(0, -7), P(3.6f, -0.5f), P(4.2f, 2.5f), P(2.6f, 5.6f), P(0, 6.6f), P(-2.6f, 5.6f), P(-4.2f, 2.5f), P(-3.6f, -0.5f) }, ink);
                on.DrawCircle(P(-1.4f, 2.2f), 1.3f * s, new Color(0.05f, 0.2f, 0.05f));
                on.DrawCircle(P(1.6f, 3.6f), 0.9f * s, new Color(0.05f, 0.2f, 0.05f));
                break;
            case "盾":
                on.DrawColoredPolygon(new[] { P(-4.6f, -5.5f), P(4.6f, -5.5f), P(4.6f, 0.5f), P(2.6f, 4.6f), P(0, 6.8f), P(-2.6f, 4.6f), P(-4.6f, 0.5f) }, ink);
                on.DrawLine(P(0, -4.5f), P(0, 5), new Color(0.15f, 0.2f, 0.28f), 1.6f * s, true);
                on.DrawLine(P(-3.4f, -1.2f), P(3.4f, -1.2f), new Color(0.15f, 0.2f, 0.28f), 1.6f * s, true);
                break;
            case "弓":
                on.DrawArc(P(-2.5f, 0), 6.4f * s, -1.05f, 1.05f, 14, ink, 1.8f * s, true);
                on.DrawLine(P(0.7f, -5.6f), P(0.7f, 5.6f), ink, 0.9f * s, true);
                on.DrawLine(P(-5.5f, 0), P(5.8f, 0), ink, 1.3f * s, true);
                on.DrawColoredPolygon(new[] { P(6.8f, 0), P(3.8f, -2), P(3.8f, 2) }, ink);
                break;
            case "槍":
                on.DrawLine(P(-5.2f, 5.2f), P(3, -3), ink, 1.7f * s, true);
                on.DrawColoredPolygon(new[] { P(6.2f, -6.2f), P(0.8f, -4.2f), P(4.2f, -0.8f) }, ink);
                on.DrawLine(P(-0.6f, -1.6f), P(1.6f, 0.6f), ink, 1.2f * s, true);
                break;
        }
    }
}

/// <summary>狩る者の演出の 2D の層: 閃光・落雷・帯・目の印・「生還」・勇者の HP の棒・入力の止め。描くだけ。</summary>
public partial class HunterOverlay : Control
{
    public enum MarkKind { Eye, Home, Rear }
    internal List<(Vector2 At, MarkKind Kind)> Marks { get; } = new();
    internal event Action? Skip;
    internal bool BarShown { get; set; }
    internal Rect2 BarRect { get; private set; }

    private float _flash, _flashFade = 1, _bolt, _boltLife, _band, _bandLife, _clock;
    private Vector2 _boltAt;
    private readonly List<Vector2[]> _boltLines = new();
    private string _bandText = "", _barName = "";
    private int _bandCrowns, _hp, _max = 1, _line;
    private Color _bandColor = Colors.White;
    private readonly Random _random = new(480);

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    /// <summary>演出のあいだ、盤と遊ぶ UI への入力を止める（押すと残りの待ちを飛ばす）。</summary>
    internal void Block(bool on) => MouseFilter = on ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true }) { Skip?.Invoke(); AcceptEvent(); }
    }

    internal void Flash(float strength, float seconds) { _flash = strength; _flashFade = strength / Math.Max(0.05f, seconds); }

    internal void Bolt(Vector2 at, float seconds)
    {
        _boltAt = at; _bolt = _boltLife = seconds;
        _boltLines.Clear();
        // 幹 1 本と枝 2 本（上の端から門へ・折れ線）。
        for (int branch = 0; branch < 3; branch++)
        {
            var pts = new List<Vector2>();
            float top = branch == 0 ? 0 : at.Y * (0.25f + 0.2f * branch);
            Vector2 from = new(at.X + (branch == 0 ? _random.Next(-90, 90) : 0), top), to = branch == 0 ? at : at + new Vector2((branch == 1 ? -1 : 1) * _random.Next(120, 240), -_random.Next(40, 140));
            if (branch > 0) from = new Vector2(at.X + (float)(_random.NextDouble() - 0.5) * 60, top);
            int n = branch == 0 ? 11 : 5;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                Vector2 p = from.Lerp(to, t);
                if (i > 0 && i < n) p.X += _random.Next(-34, 34);
                pts.Add(p);
            }
            _boltLines.Add(pts.ToArray());
        }
    }

    internal void Band(string text, int crowns, Color color, float seconds)
    {
        _bandText = text; _bandCrowns = crowns; _bandColor = color; _band = _bandLife = seconds;
    }
    internal string BandText => _band > 0 ? _bandText : "";

    internal void SetBar(string name, int hp, int max, int linePercent) { _barName = name; _hp = Math.Max(0, hp); _max = Math.Max(1, max); _line = linePercent; }

    internal void Tick(float delta)
    {
        _clock += delta;
        _flash = Math.Max(0, _flash - _flashFade * delta);
        _bolt = Math.Max(0, _bolt - delta);
        _band = Math.Max(0, _band - delta);
        QueueRedraw();
    }

    private static void Crown(CanvasItem on, Vector2 c, float s, Color gold)
    {
        var shape = new[]
        {
            c + new Vector2(-12, 8) * s, c + new Vector2(-14, -7) * s, c + new Vector2(-6, 0) * s, c + new Vector2(0, -10) * s,
            c + new Vector2(6, 0) * s, c + new Vector2(14, -7) * s, c + new Vector2(12, 8) * s,
        };
        on.DrawColoredPolygon(shape, gold);
        on.DrawPolyline(shape.Append(shape[0]).ToArray(), Color.FromHtml("#5a3d05"), 1.5f, true);
    }

    public override void _Draw()
    {
        Vector2 size = Size;
        Font font = Map3DFonts.Bold;
        // ---- 勇者の HP の棒（上の真ん中・減った分は赤く残る・退く線の印） ----
        if (BarShown)
        {
            float w = Math.Min(620, size.X * 0.42f), h = 22;
            var bar = new Rect2((size.X - w) / 2, 106, w, h);
            BarRect = new Rect2(bar.Position - new Vector2(8, 30), bar.Size + new Vector2(16, 40));
            DrawRect(BarRect, new Color(0.03f, 0.04f, 0.06f, 0.82f));
            DrawRect(BarRect, Color.FromHtml("#8fa6c8"), false, 2);
            Crown(this, bar.Position + new Vector2(14, -14), 0.8f, Color.FromHtml("#ffd24a"));
            DrawString(font, bar.Position + new Vector2(34, -7), _barName, HorizontalAlignment.Left, -1, 20, Color.FromHtml("#dff1ff"));
            string num = $"{_hp} ／ {_max}";
            DrawString(font, bar.Position + new Vector2(w - 190, -7), num, HorizontalAlignment.Right, 190, 18, Color.FromHtml("#b9bfb4"));
            DrawRect(bar, Color.FromHtml("#7a1f1a"));                                   // 減った分（赤）
            DrawRect(new Rect2(bar.Position, new Vector2(w * _hp / _max, h)), Color.FromHtml("#e8f4ff"));   // 残り（白）
            if (_line > 0 && _line < 100)
            {
                float x = bar.Position.X + w * _line / 100f;
                DrawLine(new Vector2(x, bar.Position.Y - 5), new Vector2(x, bar.End.Y + 5), Color.FromHtml("#f1c96b"), 3);
            }
            DrawRect(bar, Color.FromHtml("#0d1016"), false, 2);
        }
        else BarRect = default;
        // ---- 目の印・「生還」 ----
        foreach (var (at, kind) in Marks)
        {
            if (kind == MarkKind.Eye)
            {
                float beat = 1f + 0.12f * Mathf.Sin(_clock * 9f);
                Vector2 r = new Vector2(24, 13) * beat;
                var eye = new List<Vector2>();
                for (int i = 0; i <= 12; i++) { float t = i / 12f; eye.Add(at + new Vector2(Mathf.Lerp(-r.X, r.X, t), -Mathf.Sin(t * Mathf.Pi) * r.Y)); }
                for (int i = 12; i >= 0; i--) { float t = i / 12f; eye.Add(at + new Vector2(Mathf.Lerp(-r.X, r.X, t), Mathf.Sin(t * Mathf.Pi) * r.Y)); }
                DrawColoredPolygon(eye.ToArray(), new Color(0.12f, 0.02f, 0.02f, 0.95f));
                DrawPolyline(eye.Append(eye[0]).ToArray(), Color.FromHtml("#ff3524"), 3f, true);
                DrawCircle(at, 7.5f * beat, Color.FromHtml("#ff3524"));
                DrawCircle(at, 3f, new Color(0.1f, 0, 0));
            }
            else
            {
                bool rearMark = kind == MarkKind.Rear;
                string text = rearMark ? Map11Lines.RearPick : "生還";
                Vector2 ts = font.GetStringSize(text, HorizontalAlignment.Left, -1, 22);
                var box = new Rect2(at - new Vector2(ts.X / 2 + 12, 16), new Vector2(ts.X + 24, 32));
                DrawRect(box, rearMark ? new Color(0.16f, 0.11f, 0.02f, 0.92f) : new Color(0.04f, 0.14f, 0.10f, 0.92f));
                DrawRect(box, Color.FromHtml(rearMark ? "#f1c96b" : "#71d7a1"), false, 2);
                DrawString(font, new Vector2(box.Position.X + 12, box.Position.Y + 24), text, HorizontalAlignment.Left, -1, 22, Color.FromHtml(rearMark ? "#ffe6a8" : "#b8f5d3"));
            }
        }
        // ---- 帯（画面を横切る・名前と王冠） ----
        if (_band > 0)
        {
            float t = 1 - _band / Math.Max(0.01f, _bandLife);
            float show = Mathf.Clamp(t / 0.12f, 0, 1) * Mathf.Clamp((1 - t) / 0.15f, 0, 1);
            float y = size.Y * 0.36f, hh = 96;
            DrawRect(new Rect2(0, y - hh / 2, size.X, hh), new Color(0.02f, 0.03f, 0.06f, 0.86f * show));
            DrawRect(new Rect2(0, y - hh / 2, size.X, 3), new Color(_bandColor.R, _bandColor.G, _bandColor.B, show));
            DrawRect(new Rect2(0, y + hh / 2 - 3, size.X, 3), new Color(_bandColor.R, _bandColor.G, _bandColor.B, show));
            Vector2 ts = font.GetStringSize(_bandText, HorizontalAlignment.Left, -1, 52);
            float slide = (1 - Mathf.Clamp(t / 0.12f, 0, 1)) * 140;
            Vector2 at = new((size.X - ts.X) / 2 + slide, y + 18);
            DrawString(font, at, _bandText, HorizontalAlignment.Left, -1, 52, new Color(_bandColor.R, _bandColor.G, _bandColor.B, show));
            // 追記 4: 立ち絵があれば、名前の左に顔。
            if (HunterArt.On && HunterArt.Face is { } bandFace)
                DrawTextureRect(bandFace, new Rect2(at.X - hh - 18, y - hh / 2 + 6, hh - 12, hh - 12), false, new Color(1, 1, 1, show));
            for (int i = 0; i < _bandCrowns; i++)
                Crown(this, new Vector2(size.X / 2 + (i - (_bandCrowns - 1) / 2f) * 44 + slide, y - hh / 2 - 18), 1.3f, new Color(1f, 0.82f, 0.29f, show));
        }
        // ---- 落雷（青白・固定） ----
        if (_bolt > 0)
        {
            float a = Mathf.Clamp(_bolt / Math.Max(0.01f, _boltLife), 0, 1);
            bool flick = Mathf.PosMod(_bolt, 0.14f) > 0.04f;
            if (flick)
                foreach (Vector2[] line in _boltLines)
                {
                    DrawPolyline(line, new Color(0.45f, 0.72f, 1f, 0.55f * a), 22, true);
                    DrawPolyline(line, new Color(0.75f, 0.9f, 1f, 0.9f * a), 9, true);
                    DrawPolyline(line, new Color(1, 1, 1, a), 3.5f, true);
                }
            DrawCircle(_boltAt, 70 * a + 20, new Color(0.8f, 0.92f, 1f, 0.5f * a));
        }
        // ---- 白い閃光 ----
        if (_flash > 0) DrawRect(new Rect2(Vector2.Zero, size), new Color(1, 1, 1, Mathf.Clamp(_flash, 0, 1)));
    }
}

/// <summary>
/// 作戦マップの曲の口（第480期 §3-4）。<b>曲そのものは置いていない</b>——ファイルがあれば鳴らし、無ければ黙る。
/// 置き場と名前は design/MAP_AUDIO.md（`DemoApp/audio/map_normal.ogg` ／ `map_hero.ogg` ／ `map_win.ogg` ／ `map_lose.ogg`）。
/// 切り替えは 1 秒で重ねて替える。場面を読み直しても（戦闘から戻っても）同じ曲なら鳴らし直さない——プレイヤーは木の根に 1 つだけ置く。
/// </summary>
internal static class MapMusic
{
    internal enum Track { None, Normal, Hero, Win, Lose }
    /// <summary>曲の音量（dB）。設定の画面に音量の項目ができたら、ここをその値で置き換える（いまは音量の設定が無い）。</summary>
    internal static float VolumeDb = -12f;
    internal const float FadeSeconds = 1.0f;
    private static readonly Dictionary<Track, string> Files = new()
    {
        [Track.Normal] = "res://audio/map_normal.ogg", [Track.Hero] = "res://audio/map_hero.ogg",
        [Track.Win] = "res://audio/map_win.ogg", [Track.Lose] = "res://audio/map_lose.ogg",
    };
    private static Node? _root;
    private static AudioStreamPlayer? _now;
    /// <summary>いま頼まれている曲（ファイルが無くても覚える・検査が読む）。</summary>
    internal static Track Current { get; private set; } = Track.None;
    /// <summary>いま実際に鳴っているか（ファイルがあったか）。</summary>
    internal static bool Sounding => _now is not null && GodotObject.IsInstanceValid(_now) && _now.Playing;
    internal static bool Has(Track track) => Files.TryGetValue(track, out string? path) && ResourceLoader.Exists(path);

    internal static void Play(Node from, Track track)
    {
        if (track == Current) return;
        GD.Print($"MAP_MUSIC {Current} → {track} file={(Has(track) ? Files[track] : "（無い——黙る）")}");
        Current = track;
        if (_root is null || !GodotObject.IsInstanceValid(_root))
        {
            if (!from.IsInsideTree()) return;
            _root = new Node { Name = "MapMusic" };
            from.GetTree().Root.CallDeferred(Node.MethodName.AddChild, _root);
        }
        AudioStreamPlayer? old = _now is not null && GodotObject.IsInstanceValid(_now) ? _now : null;
        _now = null;
        if (old is not null)
        {
            Tween fade = old.CreateTween();
            fade.TweenProperty(old, "volume_db", -60f, FadeSeconds);
            fade.TweenCallback(Callable.From(old.QueueFree));
        }
        if (!Has(track) || GD.Load<AudioStream>(Files[track]) is not { } stream) return;
        if (stream is AudioStreamOggVorbis ogg) ogg.Loop = track is Track.Normal or Track.Hero;
        var player = new AudioStreamPlayer { Stream = stream, VolumeDb = -60f, Bus = AudioServer.GetBusIndex("Music") >= 0 ? "Music" : "Master" };
        _root.CallDeferred(Node.MethodName.AddChild, player);
        player.Ready += () =>
        {
            player.Play();
            player.CreateTween().TweenProperty(player, "volume_db", VolumeDb, FadeSeconds);
        };
        _now = player;
    }

    /// <summary>作戦マップを離れる（戦闘へ・題へ）: 1 秒で消す。</summary>
    internal static void Stop(Node from) => Play(from, Track.None);
}

/// <summary>
/// 狩る者の立ち絵（第480期 追記 4・表示だけ）。`design/art/enemies/hero/` に待機の絵（<c>idle-left.png</c> ／ <c>idle.png</c>）があれば読む
/// ——フォルダの直下が先、無ければ下のフォルダのうちいちばん新しいもの。顔は絵の上 1/4 から真ん中を四角く切り出す。
/// 絵が無ければ何もしない（今の仮の絵のまま）。透過の無い絵は板にしない（顔だけ使う）。
/// </summary>
internal static class HunterArt
{
    private static bool _tried;
    private static string? _heroId;
    internal static bool On { get; private set; }
    internal static Texture2D? Face { get; private set; }
    internal static Texture2D? Board { get; private set; }
    internal static string Source { get; private set; } = "";

    /// <summary>その盤で使う（<paramref name="heroId"/> ＝ 勇者の駒の Id・null で外す）。</summary>
    internal static void Use(string? heroId)
    {
        if (_heroId is not null) UnitFaceIcon.Override(_heroId, null);
        _heroId = null;
        On = false;
        if (heroId is null) return;
        Load();
        if (Face is null) return;
        _heroId = heroId;
        On = true;
        UnitFaceIcon.Override(heroId, Face);
    }

    private static string? Find()
    {
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "design", "art", "enemies", "hero"));
        if (!System.IO.Directory.Exists(dir)) return null;
        string[] names = { "idle-left.png", "idle.png", "idle-right.png" };
        foreach (string n in names) if (System.IO.File.Exists(System.IO.Path.Combine(dir, n))) return System.IO.Path.Combine(dir, n);
        return System.IO.Directory.GetDirectories(dir)
            .SelectMany(d => names.Select(n => System.IO.Path.Combine(d, n))).Where(System.IO.File.Exists)
            .OrderByDescending(System.IO.File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    private static void Load()
    {
        if (_tried) return;
        _tried = true;
        try
        {
            if (Find() is not { } path) return;
            using Image image = Image.LoadFromFile(path);
            if (image.IsEmpty()) return;
            image.Convert(Image.Format.Rgba8);
            Source = path;
            // 顔: 絵の上 1/4（高さ）から、真ん中を四角く。丸く抜いて端をなじませる（`UnitFaceIcon.TextureFor` と同じ形）。
            int side = Math.Min(image.GetWidth(), image.GetHeight() / 4);
            using Image crop = image.GetRegion(new Rect2I((image.GetWidth() - side) / 2, 0, side, side));
            crop.Resize(128, 128, Image.Interpolation.Lanczos);
            for (int py = 0; py < 128; py++)
                for (int px = 0; px < 128; px++)
                {
                    Color color = crop.GetPixel(px, py);
                    color.A *= Math.Clamp(63.5f - new Vector2(px - 63.5f, py - 63.5f).Length(), 0, 1);
                    crop.SetPixel(px, py, color);
                }
            crop.GenerateMipmaps();
            Face = ImageTexture.CreateFromImage(crop);
            // 板: 四隅が透けている絵だけ（背景つきの絵は板にしない）。
            bool cut = image.GetPixel(2, 2).A < 0.1f && image.GetPixel(image.GetWidth() - 3, 2).A < 0.1f;
            if (cut)
            {
                if (image.GetHeight() > 720) image.Resize(Math.Max(1, image.GetWidth() * 720 / image.GetHeight()), 720, Image.Interpolation.Lanczos);
                image.GenerateMipmaps();
                Board = ImageTexture.CreateFromImage(image);
            }
            GD.Print($"MAP3D_HUNTER_ART {path} 顔 ○ 板 {(cut ? "○" : "×（背景つき——顔だけ使う）")}");
        }
        catch (Exception e) { GD.PrintErr($"MAP3D_HUNTER_ART_FAILED {e.Message}"); }
    }
}
