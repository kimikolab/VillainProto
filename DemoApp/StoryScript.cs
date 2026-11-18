using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// ストーリーパートの台本（第481期・指示書 design/PHASE481_STORY_PART_SPEC.md §2）。**Godot の型は 1 つも使わない。**
//
// 台本はコードに埋めない——`DemoApp/story/chapter1.txt`（UTF-8）を起動時に読む（ポンがテキストだけ直して試せる）。
// 書式（1 行 1 セリフ・`｜` 区切り）:
//
//   # 場面 0｜辺境の砦｜1-1 の前          ← 場面の頭（番号・題・いつ）
//   伝令｜影・右｜新任の幹部殿。……        ← 話者｜位置｜文（位置は 左 ／ 右 ／ 内心・「影・」を付けると黒い影の立ち絵）
//   ——｜内心｜使えない砦に、……            ← 主人公の内心（立ち絵なし・名札なし）
//   ?生還｜リリ｜左｜でも、帰ってきたわ。  ← 1-3 の結果がそれのときだけ出る（生還 ／ 勇者は退いた ／ 辛くも退けた）
//   （暗転）                               ← 演出の行
//   （帯）1 章　完                         ← 演出の行（締めの帯）
//
// `//` で始まる行と空の行は読み飛ばす。ここは「読む・確かめる」だけで、いつ出すかは画面（`Map11Main.Story.cs`）が決める。
// =====================================================================================
public static class StoryScript
{
    public enum Side { Left, Right, Inner }
    public enum Kind { Say, Blackout, Band }

    /// <summary>台本の 1 行。<c>Only</c> は 1-3 の結果の条件（無ければ null）・<c>Row</c> はファイルの行番号（1 から）。</summary>
    public sealed record Line(Kind Kind, string Speaker, Side Side, bool Shadow, string Text, string? Only, int Row);

    public sealed record Scene(int No, string Title, string When, IReadOnlyList<Line> Lines)
    {
        /// <summary>その結果で出る行（<paramref name="outcome"/> が null なら `?` の行は出さない）。</summary>
        public IReadOnlyList<Line> For(string? outcome) => Lines.Where(l => l.Only is null || l.Only == outcome).ToList();
    }

    /// <summary>1-3 の結果の 3 つ（`?` の行の鍵）。</summary>
    public static readonly string[] Outcomes = { "生還", "勇者は退いた", "辛くも退けた" };

    /// <summary>1 行の長さの上限（全角で数える・半角は 0.5）と、1 場面の行数の上限（演出の行はまとめて 1・`?` の行はまとめて 1）。</summary>
    public const int MaxWidth = 46, MaxLines = 14;

    /// <summary>台本の置き場（`res://` からの相対）。</summary>
    public const string File = "story/chapter1.txt";

    /// <summary>勇者（立ち絵は `design/art/enemies/hero/`・駒の表には無い名前）。</summary>
    public const string HeroSpeaker = "ライゼル";

    public static IReadOnlyList<Scene> Parse(string text, List<string> errors)
    {
        var scenes = new List<Scene>();
        List<Line>? lines = null;
        string[] rows = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < rows.Length; i++)
        {
            string row = rows[i].Trim().TrimStart('﻿');
            int no = i + 1;
            if (row.Length == 0 || row.StartsWith("//", StringComparison.Ordinal)) continue;
            if (row.StartsWith('#'))
            {
                string[] head = row.TrimStart('#').Trim().Split('｜');
                string digits = new(head[0].Where(char.IsDigit).ToArray());
                if (head.Length < 2 || !int.TryParse(digits, out int number)) { errors.Add($"{no} 行目: 場面の頭が読めない（「# 場面 0｜題｜いつ」）: {row}"); lines = null; continue; }
                lines = new List<Line>();
                scenes.Add(new Scene(number, head[1].Trim(), head.Length > 2 ? head[2].Trim() : "", lines));
                continue;
            }
            if (lines is null) { errors.Add($"{no} 行目: 場面の頭（# 場面 …）より前に行がある: {row}"); continue; }
            if (row.StartsWith("（暗転）", StringComparison.Ordinal)) { lines.Add(new Line(Kind.Blackout, "", Side.Inner, false, "", null, no)); continue; }
            if (row.StartsWith("（帯）", StringComparison.Ordinal)) { lines.Add(new Line(Kind.Band, "", Side.Inner, false, row["（帯）".Length..].Trim(), null, no)); continue; }
            string[] parts = row.Split('｜');
            string? only = null;
            if (parts[0].StartsWith('?') || parts[0].StartsWith('？'))
            {
                only = parts[0][1..].Trim();
                if (!Outcomes.Contains(only)) errors.Add($"{no} 行目: 結果の条件「{only}」は {string.Join(" ／ ", Outcomes)} のどれでもない");
                parts = parts[1..];
            }
            if (parts.Length < 3) { errors.Add($"{no} 行目: 「話者｜位置｜文」になっていない: {row}"); continue; }
            string place = parts[1].Trim();
            bool shadow = place.Contains('影');
            Side? side = place.Contains("内心") ? Side.Inner : place.Contains('左') ? Side.Left : place.Contains('右') ? Side.Right : null;
            if (side is null) { errors.Add($"{no} 行目: 位置「{place}」は 左 ／ 右 ／ 内心 のどれでもない"); continue; }
            lines.Add(new Line(Kind.Say, parts[0].Trim(), side.Value, shadow, string.Join("｜", parts[2..]).Trim(), only, no));
        }
        return scenes;
    }

    /// <summary>話者の名前（「リィカ」）から駒を引く（駒の名前の末尾で引く・無ければ null ＝ 影で出す）。</summary>
    public static UnitDef? UnitOf(string speaker)
        => speaker.Length == 0 || speaker.All(c => c is '—' or '―' or 'ー') ? null
            : UnitCatalog.All.FirstOrDefault(d => d.Name.EndsWith(speaker, StringComparison.Ordinal))
              ?? UnitCatalog.Everyone.FirstOrDefault(d => d.Name.EndsWith(speaker, StringComparison.Ordinal));

    /// <summary>全角で数えた幅（半角は 0.5）。</summary>
    public static double WidthOf(string text) => text.Sum(c => c <= 0x7e || (c >= 0xff61 && c <= 0xff9f) ? 0.5 : 1.0);

    /// <summary>場面の行数（演出の行はまとめて 1・`?` の行はまとめて 1）。</summary>
    public static int CountOf(Scene s)
        => s.Lines.Count(l => l.Kind == Kind.Say && l.Only is null) + (s.Lines.Any(l => l.Kind != Kind.Say) ? 1 : 0) + (s.Lines.Any(l => l.Only is not null) ? 1 : 0);

    /// <summary>影で出る話者（立ち絵が引けない名前・台本が「影」と書いた話者は数えない）。</summary>
    public static IReadOnlyList<string> Unknown(IEnumerable<Scene> scenes)
        => scenes.SelectMany(s => s.Lines).Where(l => l.Kind == Kind.Say && l.Side != Side.Inner && !l.Shadow && l.Speaker != HeroSpeaker && UnitOf(l.Speaker) is null)
            .Select(l => l.Speaker).Distinct().ToList();

    /// <summary>
    /// 台本の検査（門 `--story-smoke`）: 全部読める・話者が引ける・1 行が全角 46 字以内・1 場面が 14 行以内・`?` の 3 つがそろっている・場面 0〜3 がある。
    /// 見つけたことを 1 行ずつ <paramref name="say"/> に出し、通ったかを返す。
    /// </summary>
    public static bool Check(string text, Action<string> say)
    {
        var errors = new List<string>();
        IReadOnlyList<Scene> scenes = Parse(text, errors);
        bool ok = true;
        void Req(bool pass, string what) { say($"STORY_CHECK {(pass ? "○" : "×")} {what}"); ok &= pass; }
        Req(errors.Count == 0, $"台本が全部読める（読めない行 {errors.Count}）");
        foreach (string e in errors) say("STORY_CHECK 　" + e);
        Req(Enumerable.Range(0, 4).All(n => scenes.Count(s => s.No == n) == 1), $"場面 0〜3 が 1 つずつある（{string.Join("・", scenes.Select(s => $"{s.No}「{s.Title}」"))}）");
        IReadOnlyList<string> unknown = Unknown(scenes);
        Req(unknown.Count == 0, $"話者が引ける（引けない名前: {(unknown.Count == 0 ? "なし" : string.Join("・", unknown))}）");
        var shadows = scenes.SelectMany(s => s.Lines).Where(l => l.Kind == Kind.Say && l.Shadow).Select(l => l.Speaker).Distinct().ToList();
        say($"STORY_CHECK 　影で出す人物: {(shadows.Count == 0 ? "なし" : string.Join("・", shadows))}");
        var wide = scenes.SelectMany(s => s.Lines.Select(l => (s, l))).Where(x => WidthOf(x.l.Text) > MaxWidth).ToList();
        Req(wide.Count == 0, $"1 行が全角 {MaxWidth} 字以内（超えた行 {wide.Count}・いちばん長い行 {scenes.SelectMany(s => s.Lines).Select(l => WidthOf(l.Text)).DefaultIfEmpty(0).Max():0.#} 字）");
        foreach (var (s, l) in wide) say($"STORY_CHECK 　{l.Row} 行目（場面 {s.No}）{WidthOf(l.Text):0.#} 字: {l.Text}");
        foreach (Scene s in scenes)
            Req(CountOf(s) <= MaxLines, $"場面 {s.No}「{s.Title}」は {CountOf(s)} 行（{MaxLines} 行以内・セリフ {s.Lines.Count(l => l.Kind == Kind.Say && l.Only is null)}・演出 {s.Lines.Count(l => l.Kind != Kind.Say)}・? {s.Lines.Count(l => l.Only is not null)}）");
        var only = scenes.SelectMany(s => s.Lines).Where(l => l.Only is not null).Select(l => l.Only!).ToList();
        Req(Outcomes.All(o => only.Count(x => x == o) == 1), $"? の 3 つがそろっている（{string.Join("・", only)}）");
        Req(scenes.Where(s => s.Lines.Any(l => l.Only is not null)).All(s => s.No == 3), "? の行は場面 3 にだけある");
        return ok;
    }

    // ---- 見た場面の覚え（やり直しでは出さない・1 章を始め直したら捨てる） ----
    private static readonly HashSet<int> Seen = new();
    public static bool WasSeen(int scene) => Seen.Contains(scene);
    public static void MarkSeen(int scene) => Seen.Add(scene);
    public static void Forget() => Seen.Clear();

    /// <summary>
    /// 門・器具・遊ぶ口の起動か（会話を既定で飛ばす・起動時の既定を「1 章」にしない）。`--story` があれば会話は出す・`--no-story` は必ず飛ばす。
    /// </summary>
    public static bool Automated(IEnumerable<string> args)
        => args.Any(a => a.Contains("smoke", StringComparison.Ordinal) || a.Contains("-quick", StringComparison.Ordinal) || a.Contains("-sweep", StringComparison.Ordinal)
                         || a.Contains("-audit", StringComparison.Ordinal) || a.Contains("-iso", StringComparison.Ordinal)
                         || a.StartsWith("--map11-", StringComparison.Ordinal) || a.StartsWith("--map3d-play=", StringComparison.Ordinal)
                         || a.StartsWith("--map3d-fps", StringComparison.Ordinal) || a.StartsWith("--map3d-tool-check", StringComparison.Ordinal)
                         || a.StartsWith("--map-chapter-play=", StringComparison.Ordinal) || a.StartsWith("--map-snap-picker", StringComparison.Ordinal)
                         || a.StartsWith("--demo-", StringComparison.Ordinal) || a.StartsWith("--foe-weight", StringComparison.Ordinal)
                         || a.StartsWith("--axis-diag", StringComparison.Ordinal) || a.StartsWith("--nel-leak", StringComparison.Ordinal)
                         || a == "--verify");

    /// <summary>会話を出す起動か。</summary>
    public static bool Enabled(IReadOnlyCollection<string> args)
        => !args.Contains("--no-story") && (args.Contains("--story") || !Automated(args));
}
