using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 1 章の連なり（第460期 §A・指示書 design/PHASE460_CHAPTER1_SPEC.md）
//
// **各ステージの規則は 1 行も持たない。** 1-1 → 1-2 → 1-3 を順に並べ、結果をまとめるだけの器。
// ステージをまたいで持ち越すものは無い（全快・死者も戻る・手札は配り直し・金とレリックは無し・隊の枠は 2 に戻る）——
// 各ステージは `Map11Session.Reset` で単発と同じに始まる。同じ 1 章の seed なら同じ通し（ステージの seed は `StageSeed` で決まる）。
// **ここには Godot の型を1つも使わない**（画面は `Map11Main.Chapter.cs`・頭なしの門は `Chapter1Smoke`）。
// =====================================================================================

/// <summary>
/// 1 章の規則の版（第461期）。<b><c>Carry</c> が偽なら第460期</b>（持ち越し無し・各ステージは単発と同じに始まる・1-1 は単発の 1-1）。
/// 真なら第461期の持ち越し（`design/PHASE461_CARRYOVER_SPEC.md`）。
/// 第462期: <c>Scales</c> はステージごとの敵の数値の倍率（1-1 ／ 1-2 ／ 1-3・null ならすべて ×1.0）。
/// </summary>
public readonly record struct ChapterRule(bool Carry, string Name, IReadOnlyList<MapFoeScale>? Scales = null)
{
    /// <summary>第460期の 1 章（持ち越し無し）。`--chapter1-smoke` はこれで回す。</summary>
    public static readonly ChapterRule V460 = new(false, "第460期（持ち越し無し）");
    /// <summary>第461期の 1 章（1-1 は小さく始め、隊と駒・金の余り・レリック・隊の枠を次のステージへ持ち越す）。</summary>
    public static readonly ChapterRule V461 = new(true, "第461期（持ち越し）");
    /// <summary>
    /// 第462期の 1 章（第461期の持ち越し ＋ 敵の数値の倍率 1-1 ×1.0 ／ 1-2 ×1.3 ／ 1-3 ×1.6・指示書 design/PHASE462_CHAPTER_SCALE_SPEC.md §1-1）。
    /// 表は「それ以外」と「勇者」の 2 列（この期は同じ値）。値だけを替えるときはこの表を替える。
    /// </summary>
    public static readonly ChapterRule V462 = new(true, "第462期（持ち越し・敵の倍率）",
        new MapFoeScale[] { MapFoeScale.None, new(FoePercent: 130, HeroPercent: 130), new(FoePercent: 160, HeroPercent: 160) });

    /// <summary>ステージ <paramref name="index"/>（0 ＝ 1-1）の敵の倍率。</summary>
    public MapFoeScale ScaleOf(int index) => Scales is { } s && index < s.Count ? s[index] : MapFoeScale.None;
}

public static class Chapter1
{
    /// <summary>遊ぶ 1 章の規則（編成画面の札「1 章」・絵の口が始める版）。</summary>
    public static ChapterRule Default => ChapterRule.V462;   // 第461期から持ち越しのある 1 章・第462期から敵の倍率
    /// <summary>いまの 1 章の規則（<see cref="Begin"/> が決める）。</summary>
    public static ChapterRule Rule { get; private set; } = Default;

    /// <summary>その規則の 1 章のステージ（遊ぶ版）。</summary>
    /// 第461期: 持ち越しのある 1 章の 1-1 は <see cref="MapStage.Stage11Ch"/>（手札 6・隊の枠 2・金 8）。
    /// 第462期: 倍率のある 1 章は、各ステージの敵に倍率を掛けた写し（<see cref="MapStage.Scaled"/>・同じ倍率なら同じインスタンス）。
    public static IReadOnlyList<MapStage> StagesFor(ChapterRule rule)
        => new[] { rule.Carry ? MapStage.Stage11Ch : MapStage.Stage11, MapStage.Stage12, MapStage.Stage13 }
            .Select((s, i) => s.Scaled(rule.ScaleOf(i))).ToArray();

    /// <summary>いまの 1 章のステージ。</summary>
    public static IReadOnlyList<MapStage> Stages => StagesFor(Rule);

    /// <summary>1 ステージぶんの記録（1 章の結果画面の 1 行）。</summary>
    public sealed class Record
    {
        public required MapStage Stage { get; init; }
        /// <summary>いま遊んでいる（遊んだ）配りの seed。</summary>
        public int Seed { get; set; }
        public int Retries { get; set; }
        public bool Won { get; set; }
        public bool Quit { get; set; }
        public bool Played { get; set; }
        /// <summary>制圧した作戦ターン（勝った配り・1 から）。</summary>
        public int Turn { get; set; }
        public string Hero { get; set; } = "—";
        public int Earned { get; set; }
        public int Spent { get; set; }
        /// <summary>働いた札（「◆名（駒）」→ 回数・勝った ／ 最後の配り）。</summary>
        public Dictionary<string, int> Relics { get; } = new();
        public string Result => Won ? $"制圧（作戦ターン {Turn}）" : Quit ? "やめた" : Played ? "途中" : "—";
        /// <summary>このステージに入った時点の持ち越し（第461期・1-1 と持ち越しの無い 1 章は null）。やり直しはここへ戻る。</summary>
        public Map11State.CarryPack? Entry { get; set; }
        /// <summary>このステージの終わり（最後に終わった配り）の持ち越し（第461期・結果画面の「育った」列と、勝てば次のステージの <see cref="Entry"/>）。</summary>
        public Map11State.CarryPack? EndPack { get; set; }
        /// <summary>終わりの手札・駒・隊の枠・金・レリック（第461期 §1-4・遊んでいなければ「—」）。</summary>
        public string Grown => EndPack is { } p
            ? $"手札 {p.Bench.Count} 枚（駒 {p.Units} 枚）・隊の枠 {p.SquadCap}・金 {p.Gold}・レリック {p.Relics}"
            : "—";
    }

    public static bool Active { get; private set; }
    public static int Seed { get; private set; }
    /// <summary>いまのステージ（0 ＝ 1-1）。</summary>
    public static int Index { get; private set; }
    /// <summary>いまのステージの何回目の配りか（0 から・やり直すたびに 1 増える）。</summary>
    public static int Attempt { get; private set; }
    /// <summary>1 章の結果まで来た（1-3 に勝った ／ やめた）。</summary>
    public static bool Over { get; private set; }
    /// <summary>ステージの頭の札をまだ出していない（画面が出して消す）。</summary>
    public static bool IntroPending { get; set; }
    public static List<Record> Records { get; } = new();
    public static MapStage CurrentStage => Stages[Index];

    /// <summary>ステージの seed（1 章の seed ・ステージ ・何回目の配り から決まる・乱数は引かない）。</summary>
    public static int StageSeed(int chapterSeed, int stage, int attempt)
        => (int)((uint)(chapterSeed * 7919 + (stage + 1) * 104729 + attempt * 1299709) % 1_000_000u);

    /// <summary>1 章を始める（1-1 から）。seed を渡さなければ振る。規則を渡さなければ <see cref="Default"/>。</summary>
    public static void Begin(int? seed = null, ChapterRule? rule = null)
    {
        Rule = rule ?? Default;
        Seed = seed ?? new Random().Next(0, 1_000_000);
        Active = true;
        Over = false;
        Index = 0;
        Attempt = 0;
        Records.Clear();
        foreach (MapStage s in Stages) Records.Add(new Record { Stage = s });
        StartStage();
    }

    /// <summary>1 章をやめて単発に戻る（編成画面で札を替えたとき）。</summary>
    public static void End()
    {
        Active = false;
        Over = false;
        IntroPending = false;
        _settled = null;
    }

    private static void StartStage()
    {
        Record r = Records[Index];
        r.Seed = StageSeed(Seed, Index, Attempt);
        r.Played = true;
        r.Won = r.Quit = false;
        _settled = null;
        Map11Session.Reset(r.Seed, draft: true, stage: Stages[Index]);
        // 第461期 §1-1 ／ §1-3: 持ち越しのある 1 章では、入った時点の持ち越しを載せる（やり直しも同じ写しへ戻る・戦闘と湧きの seed だけが新しい）。
        if (Rule.Carry && r.Entry is { } entry) Map11Session.State!.ApplyCarry(entry);
        IntroPending = true;
    }

    private static Map11State? _settled;

    /// <summary>
    /// 終わったステージを記録する（1 つの通しにつき 1 度だけ・結果画面が出るたびに呼んでよい）。
    /// <paramref name="relics"/> と <paramref name="log"/> は画面の通しの `Map11Session.RelicWorked` ／ `BattleLog`（頭なしの器具は空でよい）。
    /// </summary>
    public static void Settle(Map11State st, IReadOnlyDictionary<(string Unit, TraitId Relic), int> relics, IReadOnlyList<Map11Session.Line> log)
    {
        if (!Active || !st.Finished || ReferenceEquals(_settled, st)) return;
        _settled = st;
        Record r = Records[Index];
        r.Won = st.Won;
        r.Turn = st.Won ? st.CapturedTurn + 1 : 0;
        r.Hero = HeroText(st, log);
        r.Earned = st.GoldLog.Where(e => e.Amount > 0).Sum(e => e.Amount);
        r.Spent = -st.GoldLog.Where(e => e.Amount < 0).Sum(e => e.Amount);
        r.Relics.Clear();
        foreach (var kv in relics.Where(kv => kv.Value > 0))
            r.Relics[$"◆{RelicCatalog.Info(kv.Key.Relic).Name}（{kv.Key.Unit}）"] = kv.Value;
        if (Rule.Carry) r.EndPack = st.TakeCarry();   // 第461期
    }

    /// <summary>勝ったステージの次へ（1-3 の後は 1 章の結果）。次のステージを始めたら true。</summary>
    public static bool Next()
    {
        if (!Active || !Records[Index].Won) return false;
        if (Index >= Stages.Count - 1) { Over = true; return false; }
        Map11State.CarryPack? carry = Rule.Carry ? Records[Index].EndPack : null;   // 第461期: 勝った配りの終わりを持ち越す
        Index++;
        Attempt = 0;
        Records[Index].Entry = carry;
        StartStage();
        return true;
    }

    /// <summary>負けたステージをやり直す（同じステージ・新しい配り）。やり直しの回数を 1 増やす。</summary>
    public static void Retry()
    {
        if (!Active) return;
        Records[Index].Retries++;
        Attempt++;
        StartStage();
    }

    /// <summary>やめる（1 章の結果へ）。</summary>
    public static void Quit()
    {
        if (!Active) return;
        Records[Index].Quit = true;
        Over = true;
    }

    /// <summary>勇者の列（指示書 §A-4）。1-1 は勇者がいない（—）。</summary>
    public static string HeroText(Map11State st, IReadOnlyList<Map11Session.Line> log)
    {
        if (!st.Stage.Hero.On) return "—";
        if (st.Hero is not { } h) return "現れなかった";
        Map11Session.Line? down = log.LastOrDefault(l => l.Won && l.Foe.EndsWith(h.Def.Name, StringComparison.Ordinal));
        string when = down is not null ? $"（作戦ターン {down.Turn}）" : "";
        if (h.Cleared)
            return st.Stage.Hero.Chaser && (down?.Intercept ?? h.Pos == st.Graph.Home) ? $"拠点で迎え撃って倒した{when}" : $"倒した{when}";
        if (st.Stage.Hero.Chaser && st.Won) return st.HomeLost ? "逃げ切った（拠点を落とされた）" : "逃げ切った（拠点は落とされなかった）";
        return st.HeroOutcome;
    }

    /// <summary>1 章の結果（ステージごとに 1 行・最後に合計の 1 行）。</summary>
    public static List<string> ResultLines()
    {
        static string Top(IEnumerable<KeyValuePair<string, int>> relics)
        {
            var top = relics.GroupBy(kv => kv.Key).Select(g => (Name: g.Key, Count: g.Sum(x => x.Value)))
                .OrderByDescending(x => x.Count).ThenBy(x => x.Name, StringComparer.Ordinal).Take(3).ToList();
            return top.Count == 0 ? "—" : string.Join("・", top.Select(x => $"{x.Name} {x.Count} 回"));
        }
        var lines = new List<string>();
        foreach (Record r in Records)
            lines.Add($"{r.Stage.Id} ・ {r.Result} ・ やり直し {r.Retries} 回 ・ 勇者 {(r.Played ? r.Hero : "—")} ・ 金 稼いだ {r.Earned} ／ 使った {r.Spent} ・ 働いた札 {Top(r.Relics)}"
                      + (Rule.Carry ? $" ・ 終わり {r.Grown}" : ""));   // 第461期 §1-4: 育ったことが見える列（持ち越しの無い 1 章は第460期のまま）
        lines.Add($"合計 ・ 制圧 {Records.Count(r => r.Won)} ／ {Records.Count} ・ やり直し {Records.Sum(r => r.Retries)} 回"
                  + $" ・ 金 稼いだ {Records.Sum(r => r.Earned)} ／ 使った {Records.Sum(r => r.Spent)} ・ 働いた札 {Top(Records.SelectMany(r => r.Relics))}");
        return lines;
    }

    /// <summary>ステージの頭の札（見出し ＋ 2〜3 行・<b>仮の文</b>——本文は後で Web 側が書く）。</summary>
    /// 第462期 §1-3: 敵に倍率のかかったステージは最後に「敵の強さ ×1.3」の 1 行（1-1 ＝ ×1.0 は出さない）。
    public static (string Title, string Body) Intro(MapStage s)
    {
        var (title, body) = IntroText(s);
        return s.FoeScale.Active ? (title, body + "\n" + s.FoeScale.IntroLine) : (title, body);
    }

    private static (string Title, string Body) IntroText(MapStage s) => s.Id switch
    {
        "1-1" => ("1 章 ・ 1-1 捨て駒の初陣",
                  "捨てられた駒たちの最初の作戦。北と南の道から来る敵を受けながら、敵の拠点を取る。\n（仮の文）"),
        "1-2" => ("1 章 ・ 1-2 砦を取れ",
                  "中間拠点を 2 つ取ると本陣の門が開く。門の前には勇者が立つ——倒さなければ本陣へは入れない。\n（仮の文）"),
        "1-3" => ("1 章 ・ 1-3 狩る者",
                  "門が開くと、勇者がこちらの拠点を狩りに来る。拠点で迎え撃つか、拠点を捨てて本陣へ急ぐか。\n（仮の文）"),
        _ => ($"1 章 ・ {s.Id}", ""),
    };
}
