using BattleCore;
using Godot;
using System.Linq;

public partial class BattlePawn3D
{
    internal int RuleMarkCount => _ruleTag.GetChildCount();
    internal bool RuleMarksVisible => _ruleTag.Visible;

    private void BuildRuleMarks(DemoOpening opening)
    {
        _ruleTag = new Node3D();
        AddChild(_ruleTag);
        string[] rules = BoardRuleTags.TagsOf(opening.Traits).ToArray();
        for (int i = 0; i < rules.Length; i++)
        {
            Color color = BattlefieldView3D.RuleColor(rules[i]);
            // 同時保持も重ねず横に並べる。輪の上の菱形を常設の目印にする。
            var mark = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.23f, 0.23f, 0.08f) },
                Position = new Vector3((i - (rules.Length - 1) * 0.5f) * 0.38f, 0.38f, 0.7f),
                RotationDegrees = new Vector3(0, 0, 45),
                MaterialOverride = MakeMaterial(color, true, color),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _ruleTag.AddChild(mark);
        }
    }
}
