using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 駒のタグの小札（第450期・指示書 design/PHASE450_TAGS_SPEC.md §0-2）。<b>表示だけ・判定は1つも持たない</b>。
/// 語は <see cref="UnitDef.Tags"/>（表は `BattleCore` の `UnitTags`）。<b>「合う」とは言わない</b>——同じタグの駒を光らせない。
/// 色は 軸（燃焼・毒など）／ 役割 ／ 盤面ルール の 3 種だけ。見た目の作り込みは Codex（第450期の申し送り）。
/// </summary>
public static class UnitTagChips
{
    /// <summary>タグごとの 1 行の説明（第451期・`UnitTags.Lines`）を改行でつないだもの（触れたときの説明に使う）。</summary>
    public static string Help(UnitDef d)
        => string.Join("\n", d.Tags.Select(t => UnitTags.LineOf(t) is { Length: > 0 } l ? $"{t}: {l}" : t));

    /// <summary>タグを「・」でつないだ 1 行（説明・札の文字に使う）。<paramref name="skip"/> の語は除く。</summary>
    public static string Text(UnitDef d, string skip = "")
        => string.Join("・", d.Tags.Where(t => skip.Length == 0 || !skip.Contains(t)));

    /// <summary>小札を横に並べた箱。</summary>
    public static HBoxContainer Make(UnitDef d, int fontSize = 10)
    {
        var box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        foreach (string t in d.Tags) box.AddChild(Chip(t, fontSize));
        return box;
    }

    private static PanelContainer Chip(string word, int fontSize)
    {
        Color tint = UnitTags.Axes.Contains(word) ? UiKit.Gold
                   : UnitTags.Roles.Contains(word) ? UiKit.Muted
                   : UiKit.Enemy;   // 盤面ルールの語（粛・渇き・軛・逆位・断罪）
        var style = new StyleBoxFlat
        {
            BgColor = new Color(tint, 0.16f),
            BorderColor = new Color(tint, 0.70f),
            ContentMarginLeft = 3, ContentMarginRight = 3, ContentMarginTop = 0, ContentMarginBottom = 0,
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        // 第451期: 触れるとそのタグの 1 行（`Pass` なので押した操作は下の札へ流れる）。
        var chip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Pass, TooltipText = UnitTags.LineOf(word) is { Length: > 0 } l ? $"{word}: {l}" : word };
        chip.AddThemeStyleboxOverride("panel", style);
        Label label = UiKit.Text(word, fontSize, tint);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        chip.AddChild(label);
        return chip;
    }
}
