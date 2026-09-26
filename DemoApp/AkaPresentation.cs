using BattleCore;
using Godot;

// 台本の識別子は変更せず、表示するときだけ血へ読み替える。
public static class AkaPresentation
{
    public static readonly Color Blood = new("713e42");
    public static string Text(string text) => text.Replace("灰", "血");
}
