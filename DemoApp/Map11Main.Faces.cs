using BattleCore;
using Godot;
using System.Linq;
using System.Text.RegularExpressions;

public partial class Map11Main
{
    private HBoxContainer _encounterFaces = null!;
    private VBoxContainer _resultFaces = null!;
    private MapUnitBadge? _waitingReserveBadge;
    private PanelContainer? _portalInfo;
    private Label? _portalCaption;

    private void RefreshPortalCaption()
    {
        if (!St.Portal.On) return;
        if (_portalInfo is null)
        {
            _portalInfo = Panel();
            _portalInfo.MouseFilter = Control.MouseFilterEnum.Ignore;
            _portalInfo.CustomMinimumSize = new Vector2(238, 0);
            _portalCaption = UiKit.Text("", 14, UiKit.Ink);
            _portalCaption.MouseFilter = Control.MouseFilterEnum.Ignore;
            _portalInfo.AddChild(_portalCaption);
            _mapPickLayer.AddChild(_portalInfo);
            _portalMarker.UseMapCaption();
        }
        _portalCaption!.Text = _portalMarker.Caption + (_portalMarker.Note.Length > 0 ? Nl + _portalMarker.Note : "");
    }

    // 代表は前1（席0）。空席・戦死なら、生きている駒の席番号が若い順。
    // 全滅や未編成の隊に既定の顔ぶれを戻さない。
    private static UnitDef? Representative(Map11State.Squad squad)
        => squad.Lost ? null : squad.Units is { } units
            ? units.Where(u => u.IsAlive).OrderBy(u => u.Slot).FirstOrDefault()?.Def
            : squad.Def.F.Occupied().OrderBy(u => u.Slot).Select(u => u.Def).FirstOrDefault();

    private static UnitDef? Representative(Map11State.Node foe)
        => foe.Cleared ? null : foe.Units is { } units
            ? units.Where(u => u.IsAlive).OrderBy(u => FormationRules.RowOf(u.Slot)).ThenBy(u => u.Slot).FirstOrDefault()?.Def
            : foe.Def.Enemy.Occupied().OrderBy(u => FormationRules.RowOf(u.Slot)).ThenBy(u => u.Slot).Select(u => u.Def).FirstOrDefault();

    private static string WaveMark(string name)
    {
        Match match = Regex.Match(name, "第([^波]+)波");
        return match.Success ? match.Groups[1].Value : name.Contains("勇者") ? "勇" : "敵";
    }

    private static void SetBadge(MapUnitBadge badge, Map11State.Squad squad)
    {
        int count = squad.Lost ? 0 : squad.Units?.Count(u => u.IsAlive) ?? squad.Def.F.Occupied().Count();
        badge.ShowUnit(Representative(squad), count, count == 0 ? 0 : SquadHpPercent(squad), squad.Label);
    }

    private static void SetBadge(MapUnitBadge badge, Map11State.Node foe)
    {
        int count = foe.Cleared ? 0 : foe.Units?.Count(u => u.IsAlive) ?? foe.Def.Enemy.Occupied().Count();
        // 第447期 §0-2: 抜くと入る金を札に出す（湧いた部隊は安い・予備隊は最初からいる部隊と同じ）。
        GoldRule g = St.Stage.Gold;
        string reward = g.On && !foe.Cleared ? $" ＋{(foe.FromPortal && !foe.IsReserve ? g.SpawnKill : g.Kill)}" : "";
        badge.ShowUnit(Representative(foe), count, NodeHpPercent(foe), (foe.IsReserve ? "予備隊" : "第" + WaveMark(foe.Def.Name) + "波") + reward,
            enemy: true, wave: WaveMark(foe.Def.Name));
        badge.TooltipText = $"{foe.Def.Name}　{Representative(foe)?.Name}　{count}体　HP {NodeHpPercent(foe):F0}%";
    }

    private static void AddHandFace(Button button, UnitDef def)
    {
        var face = new UnitFaceIcon { Position = new Vector2(5, 5), Size = new Vector2(42, 42) };
        face.SetUnit(def);
        button.AddChild(face);
        button.CustomMinimumSize = new Vector2(0, 54);
        button.ClipText = true;
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled" })
        {
            var style = (StyleBox)button.GetThemeStylebox(state).Duplicate();
            style.ContentMarginLeft = 54;
            button.AddThemeStyleboxOverride(state, style);
        }
    }

    private void RefreshEncounterFaces(Map11State.Squad squad, Map11State.Node foe)
    {
        foreach (Node child in _encounterFaces.GetChildren()) { _encounterFaces.RemoveChild(child); child.QueueFree(); }
        var own = new MapUnitBadge { MouseFilter = Control.MouseFilterEnum.Ignore };
        SetBadge(own, squad); _encounterFaces.AddChild(own);
        Label versus = UiKit.Text("×", 24, UiKit.Muted);
        versus.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _encounterFaces.AddChild(versus);
        var enemy = new MapUnitBadge { MouseFilter = Control.MouseFilterEnum.Ignore };
        SetBadge(enemy, foe); _encounterFaces.AddChild(enemy);
    }

    private void RefreshResultFaces()
    {
        foreach (Node child in _resultFaces.GetChildren()) { _resultFaces.RemoveChild(child); child.QueueFree(); }
        _resultFaces.AddChild(UiKit.Text("終了時の隊", 15, UiKit.Muted));
        foreach (var squad in St.Squads.Where(s => !St.IsLocked(s.Index)))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            var badge = new MapUnitBadge { MouseFilter = Control.MouseFilterEnum.Ignore };
            SetBadge(badge, squad);
            row.AddChild(badge);
            var members = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            if (squad.Units is { } units)
                foreach (var unit in units.Where(u => u.IsAlive).OrderBy(u => u.Slot))
                {
                    var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(38, 38) };
                    face.SetUnit(unit.Def); members.AddChild(face);
                }
            row.AddChild(members);
            if (squad.Lost || badge.Count == 0) row.AddChild(UiKit.Text(squad.Lost ? "失われた" : "空席", 14, UiKit.Muted));
            _resultFaces.AddChild(row);
        }
    }
}
