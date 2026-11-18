using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// ストーリーパートの台本（第481期・指示書 design/PHASE481_STORY_PART_SPEC.md §2 ＋ 追記 2・3）。**Godot の型は 1 つも使わない。**
//
// 台本はコードに埋めない——`DemoApp/story/chapter1.txt`（UTF-8）を読む（ポンがテキストだけ直して試せる）。
// 書式（1 行 1 セリフ・`｜` 区切り）:
//
//   # 場面 0｜辺境の砦｜1-1 の前          ← 場面の頭（番号・題・いつ）
//   ガン｜左｜おう、……                    ← 話者｜位置｜文（位置は 左 ／ 右）
//   伝令｜名札のみ｜新任の幹部殿。……      ← 立ち絵を出さず、名札と本文だけ（「影・右」と書いても同じ——立ち絵なし）
//   ——｜声｜つまり、俺とお揃いだ。        ← 主人公が声に出したセリフ（立ち絵なし・名札なし・紺の帯・斜体にしない・相手の立ち絵は明るいまま）
//   ——｜内心｜任されたのは、……            ← 主人公の内心（立ち絵なし・名札なし・紺の帯・斜体・立ち絵は両方暗く）
//   （暗転）  ／  （帯）1 章　完           ← 演出の行
//
// 行の頭に条件を書ける（条件に合う行だけ出す）:
//   ?生存:ドルガ｜…        その駒が直前のステージの終わりに手持ちで生きている
//   ?倒れた:ドルガ｜…      その駒が直前のステージで倒れた、または手持ちにいなかった
//   ?勇者は退いた｜… ?辛くも退けた｜… ?生還｜…    1-3 の結果
//   ?生還率>=70｜… ?生還率<70｜…                  1-3 の結果が「生還」のとき、出撃した駒のうち生きて終えた割合
// 条件の無い行で、話者が直前のステージで倒れていたら、その行は飛ばす（場面 0 と、頭に `!` を付けた行は必ず出す）。
// `//` で始まる行と空の行は読み飛ばす。ここは「読む・確かめる」だけで、いつ出すかは画面（`Map11Main.Story.cs`）が決める。
// =====================================================================================
public static class StoryScript
{
    /// <summary>位置。<c>Voice</c> ＝ 主人公の声・<c>Inner</c> ＝ 主人公の内心（どちらも立ち絵なし・名札なし）。</summary>
    public enum Side { Left, Right, Inner, Voice }
    public enum Kind { Say, Blackout, Band }

    /// <summary>
    /// 台本の 1 行。<c>Shadow</c> は立ち絵を出さない（「名札のみ」「影」）・<c>Cond</c> は行の頭の条件（無ければ null）・
    /// <c>Must</c> は頭の `!`（話者が倒れていても必ず出す）・<c>Row</c> はファイルの行番号（1 から）。
    /// </summary>
    public sealed record Line(Kind Kind, string Speaker, Side Side, bool Shadow, string Text, string? Cond, int Row, bool Must = false)
    {
        /// <summary>主人公の行（声 ／ 内心）。</summary>
        public bool Hero => Side is Side.Inner or Side.Voice;
    }

    /// <summary>
    /// 条件を見るための、直前のステージの終わりの様子（追記 2）。<c>Outcome</c> ＝ 1-3 の結果・<c>Rate</c> ＝ 出撃した駒のうち生きて終えた割合（%）・
    /// <c>Alive</c> ＝ 手持ち（隊と控え）で生きている駒の Id・<c>Fallen</c> ＝ そのステージで倒れたままの駒の Id。
    /// </summary>
    public sealed record Context(string Outcome, double Rate, IReadOnlyCollection<string> Alive, IReadOnlyCollection<string> Fallen)
    {
        /// <summary>何も起きていない（場面 0・検査の既定）: 生還・100%・台本に出る駒は全員生きている。</summary>
        public static Context Plain(IEnumerable<Scene> scenes)
            => new(Outcomes[0], 100, scenes.SelectMany(s => s.Lines).SelectMany(l => new[] { l.Speaker, CondUnit(l.Cond) }).Select(n => UnitOf(n ?? "")?.Id)
                .Where(id => id is not null).Select(id => id!).ToHashSet(), Array.Empty<string>());
    }

    public sealed record Scene(int No, string Title, string When, IReadOnlyList<Line> Lines)
    {
        /// <summary>
        /// その様子で出る行。条件つきの行は条件に合うものだけ。条件の無い行で、話者が直前のステージで倒れていたら飛ばす
        /// （場面 0 と `!` の行は必ず出す・飛ばした行は <paramref name="skipped"/> に入る）。
        /// </summary>
        public IReadOnlyList<Line> For(Context ctx, List<Line>? skipped = null)
        {
            var shown = new List<Line>();
            foreach (Line l in Lines)
            {
                if (l.Cond is not null) { if (Holds(l.Cond, ctx)) shown.Add(l); continue; }
                if (No != 0 && !l.Must && l.Kind == Kind.Say && !l.Hero && UnitOf(l.Speaker) is { } who && ctx.Fallen.Contains(who.Id)) { skipped?.Add(l); continue; }
                shown.Add(l);
            }
            return shown;
        }
    }

    /// <summary>1-3 の結果の 3 つ（条件の鍵）。「生還」は `?生還率>=70` ／ `?生還率<70` で分けて書いてもよい（生還率の条件は結果が「生還」のときだけ成り立つ）。</summary>
    public static readonly string[] Outcomes = { "生還", "勇者は退いた", "辛くも退けた" };
    public const string AliveKey = "生存:", FallenKey = "倒れた:", RateKey = "生還率";

    /// <summary>条件が「生存:X」「倒れた:X」なら X（駒の名前）。それ以外は null。</summary>
    public static string? CondUnit(string? cond)
        => cond is null ? null : cond.StartsWith(AliveKey, StringComparison.Ordinal) ? cond[AliveKey.Length..].Trim()
            : cond.StartsWith(FallenKey, StringComparison.Ordinal) ? cond[FallenKey.Length..].Trim() : null;

    /// <summary>条件が「生還率>=70」の形なら（比べ方, 数）。それ以外は null。</summary>
    public static (string Op, double N)? CondRate(string? cond)
    {
        if (cond is null || !cond.StartsWith(RateKey, StringComparison.Ordinal)) return null;
        string rest = cond[RateKey.Length..].Replace('＞', '>').Replace('＜', '<').Replace('＝', '=').Trim();
        foreach (string op in new[] { ">=", "<=", ">", "<" })
            if (rest.StartsWith(op, StringComparison.Ordinal) && double.TryParse(rest[op.Length..].Trim().TrimEnd('%', '％'), out double n)) return (op, n);
        return null;
    }

    /// <summary>条件が読めるか（門が見る）。</summary>
    public static bool CondKnown(string cond) => Outcomes.Contains(cond) || CondRate(cond) is not null || (CondUnit(cond) is { Length: > 0 } name && UnitOf(name) is not null);

    /// <summary>条件がその様子で成り立つか。</summary>
    public static bool Holds(string cond, Context ctx)
    {
        if (CondUnit(cond) is { } name)
        {
            bool alive = UnitOf(name) is { } d && ctx.Alive.Contains(d.Id) && !ctx.Fallen.Contains(d.Id);
            return cond.StartsWith(AliveKey, StringComparison.Ordinal) ? alive : !alive;   // 倒れた ＝ 倒れた、または手持ちにいなかった
        }
        if (CondRate(cond) is { } r)
            return ctx.Outcome == Outcomes[0] && r.Op switch { ">=" => ctx.Rate >= r.N, "<=" => ctx.Rate <= r.N, ">" => ctx.Rate > r.N, _ => ctx.Rate < r.N };
        return ctx.Outcome == cond;
    }

    /// <summary>1 行の長さの上限（全角で数える・半角は 0.5）と、1 場面の行数の上限（追記 3 で 16・演出の行はまとめて 1・条件つきの行は、いちばん多く出る場合で数える）。</summary>
    public const int MaxWidth = 46, MaxLines = 16;

    /// <summary>台本の置き場（`res://` からの相対）。</summary>
    public const string File = "story/chapter1.txt";

    /// <summary>勇者（立ち絵は `design/art/enemies/hero/`・駒の表には無い名前）。</summary>
    public const string HeroSpeaker = "ライゼル";

    /// <summary>
    /// 駒の表に無いが、絵の置き場を持つ話者（名前 → `design/art/story/` の下のファイル名）。絵が無ければ立ち絵なし（名札と本文だけ）。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ArtSpeakers = new Dictionary<string, string> { ["聖女"] = "saint_shadow.png" };

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
            string? cond = null;
            bool must = false;
            if (parts[0].StartsWith('?') || parts[0].StartsWith('？'))
            {
                cond = parts[0][1..].Trim();
                if (!CondKnown(cond)) errors.Add($"{no} 行目: 条件「{cond}」が読めない（{string.Join(" ／ ", Outcomes)} ／ 生存:駒 ／ 倒れた:駒 ／ 生還率>=数 ／ 生還率<数）");
                parts = parts[1..];
            }
            else if (parts[0].StartsWith('!') || parts[0].StartsWith('！')) { must = true; parts[0] = parts[0][1..]; }
            if (parts.Length < 3) { errors.Add($"{no} 行目: 「話者｜位置｜文」になっていない: {row}"); continue; }
            string place = parts[1].Trim();
            bool shadow = place.Contains('影') || place.Contains("名札");
            // 「名札のみ」は左右を書かなくてよい（名札は左に出す・「名札のみ・右」と書けば右）。
            Side? side = place.Contains("内心") ? Side.Inner : place.Contains('声') ? Side.Voice : place.Contains('右') ? Side.Right : place.Contains('左') || shadow ? Side.Left : null;
            if (side is null) { errors.Add($"{no} 行目: 位置「{place}」は 左 ／ 右 ／ 名札のみ ／ 声 ／ 内心 のどれでもない"); continue; }
            lines.Add(new Line(Kind.Say, parts[0].Trim(), side.Value, shadow, string.Join("｜", parts[2..]).Trim(), cond, no, must));
        }
        return scenes;
    }

    /// <summary>話者の名前（「リィカ」）から駒を引く（駒の名前の末尾で引く・無ければ null）。</summary>
    public static UnitDef? UnitOf(string speaker)
        => speaker.Length == 0 || speaker.All(c => c is '—' or '―' or 'ー') ? null
            : UnitCatalog.All.FirstOrDefault(d => d.Name.EndsWith(speaker, StringComparison.Ordinal))
              ?? UnitCatalog.Everyone.FirstOrDefault(d => d.Name.EndsWith(speaker, StringComparison.Ordinal));

    /// <summary>全角で数えた幅（半角は 0.5）。</summary>
    public static double WidthOf(string text) => text.Sum(c => c <= 0x7e || (c >= 0xff61 && c <= 0xff9f) ? 0.5 : 1.0);

    /// <summary>
    /// 条件の組み合わせを全部作る（門と場面の検査が使う）: 結果 3 通り × 生還率（条件の数の上と下）× 条件に出る駒それぞれの 生きている ／ 倒れた。
    /// </summary>
    public static IEnumerable<(string Name, Context Ctx)> Cases(Scene s, IEnumerable<Scene> all)
    {
        Context plain = Context.Plain(all);
        var conds = s.Lines.Where(l => l.Cond is not null).Select(l => l.Cond!).Distinct().ToList();
        var units = conds.Select(CondUnit).Where(n => n is not null).Select(n => n!).Distinct().ToList();
        var rates = conds.Select(CondRate).Where(r => r is not null).Select(r => r!.Value.N).Distinct().ToList();
        string[] outcomes = conds.Any(c => Outcomes.Contains(c) || CondRate(c) is not null) ? Outcomes : new[] { Outcomes[0] };
        foreach (string outcome in outcomes)
        {
            List<double> tries = outcome == Outcomes[0] && rates.Count > 0 ? rates.SelectMany(n => new[] { n, n - 1 }).Distinct().OrderByDescending(n => n).ToList() : new List<double> { 100 };
            foreach (double rate in tries)
                for (int mask = 0; mask < 1 << units.Count; mask++)
                {
                    var down = units.Where((_, i) => (mask >> i & 1) == 1).ToList();
                    var fallen = down.Select(n => UnitOf(n)!.Id).ToHashSet();
                    string name = string.Join("・", new[] { outcomes.Length > 1 ? outcome : "", outcome == Outcomes[0] && rates.Count > 0 ? $"生還率 {rate:0}%" : "" }
                        .Concat(units.Select(n => $"{n}が{(down.Contains(n) ? "倒れた" : "生きている")}")).Where(x => x.Length > 0));
                    yield return (name, plain with { Outcome = outcome, Rate = rate, Fallen = fallen });
                }
        }
    }

    /// <summary>場面の行数（演出の行はまとめて 1・条件つきの行は、いちばん多く出る場合で数える）。</summary>
    public static int CountOf(Scene s, IEnumerable<Scene> all)
        => Cases(s, all).Max(c => s.For(c.Ctx).Count(l => l.Kind == Kind.Say)) + (s.Lines.Any(l => l.Kind != Kind.Say) ? 1 : 0);

    /// <summary>引けない話者（駒の表にも、絵の置き場の表にも無い名前・「名札のみ」と書いた話者は数えない）。</summary>
    public static IReadOnlyList<string> Unknown(IEnumerable<Scene> scenes)
        => scenes.SelectMany(s => s.Lines).Where(l => l.Kind == Kind.Say && !l.Hero && !l.Shadow && l.Speaker != HeroSpeaker && !ArtSpeakers.ContainsKey(l.Speaker) && UnitOf(l.Speaker) is null)
            .Select(l => l.Speaker).Distinct().ToList();

    /// <summary>
    /// 台本の検査（門 `--story-smoke`）: 全部読める・話者が引ける・1 行が全角 46 字以内・1 場面が 16 行以内・場面 0〜3 がある・
    /// 条件つきの行は対になる条件の行がそろっている（生存:X ⇔ 倒れた:X ／ 生還率>=N ⇔ 生還率<N ／ 結果は 3 つとも——「生還」は生還率の対でもよい）・
    /// どの条件の組み合わせでも、条件つきの行のかたまりから 1 行以上が出る。見つけたことを 1 行ずつ <paramref name="say"/> に出し、通ったかを返す。
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
        var plain = scenes.SelectMany(s => s.Lines).Where(l => l.Kind == Kind.Say && !l.Hero && (l.Shadow || ArtSpeakers.ContainsKey(l.Speaker))).Select(l => l.Speaker).Distinct().ToList();
        say($"STORY_CHECK 　駒でない人物（名札のみ ／ 絵の置き場）: {(plain.Count == 0 ? "なし" : string.Join("・", plain))}");
        say($"STORY_CHECK 　主人公の行: 声 {scenes.Sum(s => s.Lines.Count(l => l.Kind == Kind.Say && l.Side == Side.Voice))}・内心 {scenes.Sum(s => s.Lines.Count(l => l.Kind == Kind.Say && l.Side == Side.Inner))}");
        var wide = scenes.SelectMany(s => s.Lines.Select(l => (s, l))).Where(x => WidthOf(x.l.Text) > MaxWidth).ToList();
        Req(wide.Count == 0, $"1 行が全角 {MaxWidth} 字以内（超えた行 {wide.Count}・いちばん長い行 {scenes.SelectMany(s => s.Lines).Select(l => WidthOf(l.Text)).DefaultIfEmpty(0).Max():0.#} 字）");
        foreach (var (s, l) in wide) say($"STORY_CHECK 　{l.Row} 行目（場面 {s.No}）{WidthOf(l.Text):0.#} 字: {l.Text}");
        foreach (Scene s in scenes)
            Req(CountOf(s, scenes) <= MaxLines, $"場面 {s.No}「{s.Title}」は {CountOf(s, scenes)} 行（{MaxLines} 行以内・条件なしのセリフ {s.Lines.Count(l => l.Kind == Kind.Say && l.Cond is null)}・演出 {s.Lines.Count(l => l.Kind != Kind.Say)}・条件つき {s.Lines.Count(l => l.Cond is not null)}）");
        // ---- 条件つきの行は、対になる条件の行がそろっている ----
        foreach (Scene s in scenes)
        {
            var conds = s.Lines.Where(l => l.Cond is not null).Select(l => l.Cond!).ToList();
            if (conds.Count == 0) continue;
            var missing = new List<string>();
            foreach (string name in conds.Select(CondUnit).Where(n => n is not null).Select(n => n!).Distinct())
            {
                int alive = conds.Count(c => c == AliveKey + name), fell = conds.Count(c => c == FallenKey + name);
                if (alive == 0 || fell == 0) missing.Add($"{(alive == 0 ? AliveKey : FallenKey)}{name}");
            }
            foreach (var (op, n) in conds.Select(CondRate).Where(r => r is not null).Select(r => r!.Value).Distinct())
            {
                string pair = op switch { ">=" => "<", "<" => ">=", ">" => "<=", _ => ">" };
                if (!conds.Select(CondRate).Any(r => r is { } x && x.Op == pair && x.N == n)) missing.Add($"{RateKey}{pair}{n:0}");
            }
            if (conds.Any(c => Outcomes.Contains(c) || CondRate(c) is not null))
                foreach (string o in Outcomes)
                    if (!conds.Contains(o) && !(o == Outcomes[0] && conds.Any(c => CondRate(c) is not null))) missing.Add(o);
            // どの組み合わせでも、条件つきの行のかたまり（続いている条件つきの行）から 1 行以上が出る。
            var empty = new List<string>();
            int cases = 0;
            foreach (var (caseName, ctx) in Cases(s, scenes))
            {
                cases++;
                var shown = s.For(ctx).ToHashSet();
                for (int i = 0; i < s.Lines.Count; i++)
                {
                    if (s.Lines[i].Cond is null || (i > 0 && s.Lines[i - 1].Cond is not null)) continue;
                    int end = i;
                    while (end < s.Lines.Count && s.Lines[end].Cond is not null) end++;
                    if (!s.Lines.Skip(i).Take(end - i).Any(shown.Contains)) empty.Add($"{caseName}（台本の {s.Lines[i].Row} 行目から）");
                }
            }
            Req(missing.Count == 0 && empty.Count == 0, $"場面 {s.No}: 条件つきの行は対がそろっている（条件 {string.Join("・", conds.Distinct())}"
                + (missing.Count > 0 ? $"・足りない対 {string.Join("・", missing)}" : "") + (empty.Count > 0 ? $"・1 行も出ない場合 {string.Join(" ／ ", empty)}" : "") + $"・組み合わせ {cases} 通り）");
        }
        Req(scenes.Where(s => s.Lines.Any(l => l.Cond is { } c && (Outcomes.Contains(c) || CondRate(c) is not null))).All(s => s.No == 3), "1-3 の結果の条件（結果・生還率）は場面 3 にだけある");
        Req(scenes.Where(s => s.No == 0).All(s => s.Lines.All(l => l.Cond is null)), "場面 0 に条件つきの行は無い（直前のステージが無い）");
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
