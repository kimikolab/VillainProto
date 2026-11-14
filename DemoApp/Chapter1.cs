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
/// 第463期: <c>Stars</c> は☆の規則（null なら☆なし・<see cref="StarRule"/>）。
/// 第467期: <c>Invest</c> は軸への投資の規則（null なら投資なし・<see cref="InvestRule"/>）。
/// </summary>
/// 第469期: <c>Roster</c> は体験版の加入の表（null なら表なし・<see cref="RosterRule"/>）。
/// 第470期: <c>First</c> は持ち越しのある 1 章の 1-1 の盤（null なら <see cref="MapStage.Stage11Ch"/>・第461〜469期）。
/// <c>Story</c> はステージの頭の札を物語の文（<see cref="Chapter1.Tales470"/>）にするか（偽なら第460期からの仮の文）。
public readonly record struct ChapterRule(bool Carry, string Name, IReadOnlyList<MapFoeScale>? Scales = null, StarRule? Stars = null, DealRule? Deal = null,
                                          InvestRule? Invest = null, RosterRule? Roster = null, MapStage? First = null, bool Story = false)
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

    /// <summary>
    /// 第463期の 1 章（第462期 ＋ ☆の重ね・指示書 design/PHASE463_STAR_SPEC.md §1）。駒は☆1 で手に入り、同じキャラを重ねて☆3（今の強さ）まで上がる（<see cref="StarRule.V463"/>）。
    /// 敵の倍率は §1-4: <b>1-1 を ×0.6</b>（☆1 の 6 枚で始めるので・勇者の列も同じ値）・1-2 ×1.3 ・1-3 ×1.6 は第462期のまま。
    /// </summary>
    public static readonly ChapterRule V463 = new(true, "第463期（持ち越し・敵の倍率・☆の重ね）",
        new MapFoeScale[] { new(FoePercent: 60, HeroPercent: 60), new(FoePercent: 130, HeroPercent: 130), new(FoePercent: 160, HeroPercent: 160) }, StarRule.V463);

    /// <summary>
    /// 第464期の 1 章（指示書 design/PHASE464_STAR_DIRECT_SPEC.md §1）。☆の表は第463期のまま、重ねは<b>「強化」で金で直に</b>（<see cref="StarRule.V464"/>・呼ぶ候補に持っている駒は出ない）。
    /// 敵の倍率は §1-2: <b>1-2 ×0.9 ・1-3 ×1.3</b>（第462期の比 1.3 ／ 1.6 を、☆の育ちの見込み（1-2 の始めは☆1 中心・1-3 の始めは☆2 中心）で割り戻した値）・1-1 ×0.6 は第463期のまま・勇者の列も同じ値。
    /// </summary>
    public static readonly ChapterRule V464 = new(true, "第464期（持ち越し・敵の倍率・☆の強化）",
        new MapFoeScale[] { new(FoePercent: 60, HeroPercent: 60), new(FoePercent: 90, HeroPercent: 90), new(FoePercent: 130, HeroPercent: 130) }, StarRule.V464, DealRule.V464);

    /// <summary>
    /// 第465期の 1 章（指示書 design/PHASE465_HERO_NOMEND_SPEC.md §1）。第464期に、<b>勇者（1-2 の門番・1-3 の追う勇者）から毎手番の固定回復を外す</b>
    /// （`MapFoeScale.HeroMends = false`・札 `BossMend300` を持たない・HP は持ち越すので隊を順に当てて削れる）と、<b>1-3 を ×1.6 に戻す</b>（第462〜463期の値・勇者の列も）。
    /// ☆・強化・1-1 の配り・持ち越しは第464期のまま。
    /// </summary>
    public static readonly ChapterRule V465 = new(true, "第465期（持ち越し・敵の倍率・☆の強化・勇者の回復なし）",
        new MapFoeScale[] { new(60, 60, HeroMends: false), new(90, 90, HeroMends: false), new(160, 160, HeroMends: false) }, StarRule.V464, DealRule.V464);

    /// <summary>
    /// 第467期の 1 章（指示書 design/PHASE467_INVEST_SPEC.md §1）。第465期に、<b>各ステージの始めの軸への投資</b>（10 軸からランダムな 3 枚の札から 1 枚・持ち越して重なる・
    /// 呼ぶ候補 1 枚ごとに投資 1 回 +25%（上限 60%）の割合で投資した軸の駒から引く・<see cref="InvestRule.V467"/>）を足しただけ。敵の倍率 ・☆ ・強化 ・1-1 の配りは第465期のまま。
    /// </summary>
    public static readonly ChapterRule V467 = new(true, "第467期（持ち越し・敵の倍率・☆の強化・勇者の回復なし・軸への投資）",
        V465.Scales, StarRule.V464, DealRule.V464, InvestRule.V467);

    /// <summary>
    /// 第468期の 1 章（指示書 design/PHASE468_DOCTRINE_SPEC.md §1）。第467期の投資の札に<b>盤の札・金の札</b>（プラスとマイナスを両方持つ・<see cref="Doctrine"/>）を足しただけ
    /// （3 枚は「軸 1 ＋ 盤か金 1 ＋ どれでも 1」・盤 ／ 金の札は 1 章で 2 度出ない・<see cref="InvestRule.V468"/>）。敵の倍率 ・☆ ・強化 ・1-1 の配り ・軸の寄せは第467期のまま。
    /// </summary>
    public static readonly ChapterRule V468 = new(true, "第468期（持ち越し・敵の倍率・☆の強化・勇者の回復なし・軸への投資・盤と金の札）",
        V465.Scales, StarRule.V464, DealRule.V464, InvestRule.V468);

    /// <summary>
    /// 第469期の 1 章（指示書 design/PHASE469_DEMO_ROSTER_SPEC.md §1）。第468期に<b>体験版の加入の表</b>（<see cref="RosterRule.V469"/>）を載せただけ:
    /// 1-1 は決まった 6 枚で始まり（第464期の配りは使わない・やり直しても同じ 6 枚）、呼ぶ候補はそのステージまでの表の駒だけ（1-1 は 6 体・1-2 は 20 体・1-3 は 27 体）、
    /// 投資の軸の札はそのステージで呼べる駒（まだ持っていないもの）がいる軸だけ。敵の倍率 ・☆ ・強化 ・盤と金の札 ・手札 6 ・隊の枠 2 ・金 8 は第468期のまま。
    /// </summary>
    public static readonly ChapterRule V469 = new(true, "第469期（持ち越し・敵の倍率・☆の強化・勇者の回復なし・軸への投資・盤と金の札・加入の表）",
        V465.Scales, StarRule.V464, DealRule.Off, InvestRule.V468, RosterRule.V469);

    /// <summary>
    /// 第470期の 1 章（指示書 design/PHASE470_CH1_POLISH_SPEC.md §1）。第469期に、<b>投資の札は 1-2 から</b>（1-1 は札なし）と
    /// <b>一度取った軸は札に出さない</b>（<see cref="InvestRule.V470"/>）と、<b>1-1 の道の先頭を軽い部隊に</b>（<see cref="MapStage.Stage11Ch470"/>・北 第一波 ／ 物見の兵・南 徴募兵）を載せた。
    /// ステージの頭の札は物語の文（<see cref="Chapter1.Tales470"/>・仮・1-2 は「流れ着いた者」の段）。敵の倍率（1-1 ×0.6 も）・☆ ・強化 ・盤と金の札 ・加入の表は第469期のまま。
    /// </summary>
    public static readonly ChapterRule V470 = new(true, "第470期（持ち越し・敵の倍率・☆の強化・勇者の回復なし・投資は 1-2 から・盤と金の札・加入の表・1-1 の先頭は軽い部隊）",
        V465.Scales, StarRule.V464, DealRule.Off, InvestRule.V470, RosterRule.V469, MapStage.Stage11Ch470, Story: true);

    /// <summary>
    /// 第471期の 1 章（指示書 design/PHASE471_STAGE11_SPEC.md §1）。第470期に、<b>始めの 6 枚のネル → ガン</b>（<see cref="RosterRule.V471"/>・ネルは 1-1 で呼べる駒へ）と、
    /// <b>1-1 の奥と湧きを雑兵に</b>（<see cref="MapStage.Stage11Ch471"/>・盾の列 ／ 騎士の小隊 ／ 湧き 徴募兵・傭兵崩れ・波ルールなし）を載せた。
    /// 投資の札・頭の札の文・敵の倍率（1-1 ×0.6）・☆ ・強化 ・盤と金の札は第470期のまま。
    /// </summary>
    public static readonly ChapterRule V471 = new(true, "第471期（持ち越し・敵の倍率・☆の強化・勇者の回復なし・投資は 1-2 から・盤と金の札・加入の表・始めの 6 枚はガン入り・1-1 は雑兵）",
        V465.Scales, StarRule.V464, DealRule.Off, InvestRule.V470, RosterRule.V471, MapStage.Stage11Ch471, Story: true);

    /// <summary>
    /// 第474期の 1 章（指示書 design/PHASE474_STAGE11_ASYM_SPEC.md §1）。第471期に、<b>1-1 の盤を非対称に</b>（<see cref="MapStage.Stage11Ch474"/>・北 2 マス 短くて硬い ／ 南 4 マス 長くて軽い）を載せただけ。
    /// 北の 2 マス目の峠の関は動かず（抜くと金 +6）、1-1 も<b>敵の拠点を制圧したら勝ち</b>（ポン 2026-11-14）。
    /// 始めの 6 枚・呼べる駒・倍率（1-1 ×0.6）・湧き・金・投資の札・頭の札の文は第471期のまま。1-2 ／ 1-3 も第471期のまま。
    /// </summary>
    public static readonly ChapterRule V474 = new(true, "第474期（持ち越し・敵の倍率・☆の強化・勇者の回復なし・投資は 1-2 から・盤と金の札・加入の表・始めの 6 枚はガン入り・1-1 は道の長さが違う）",
        V465.Scales, StarRule.V464, DealRule.Off, InvestRule.V470, RosterRule.V471, MapStage.Stage11Ch474, Story: true);

    /// <summary>
    /// 版の一覧（第466期・遊びの写しが版をキーで持つ——写しの版で 1 章を始め直すため）。<b>版を足したらここにも足す。</b>
    /// </summary>
    public static IReadOnlyList<(string Key, ChapterRule Rule)> Versions
        => new[] { ("V460", V460), ("V461", V461), ("V462", V462), ("V463", V463), ("V464", V464), ("V465", V465), ("V467", V467), ("V468", V468), ("V469", V469),
                   ("V470", V470), ("V471", V471), ("V474", V474) };

    /// <summary>版のキー（「V465」・一覧に無ければ名前）。</summary>
    public string Key { get { string n = Name; return Versions.FirstOrDefault(v => v.Rule.Name == n).Key ?? n; } }

    /// <summary>キーから版を引く（無ければ null）。</summary>
    public static ChapterRule? ByKey(string key) => Versions.Where(v => v.Key == key).Select(v => (ChapterRule?)v.Rule).FirstOrDefault();

    /// <summary>ステージ <paramref name="index"/>（0 ＝ 1-1）の敵の倍率。</summary>
    public MapFoeScale ScaleOf(int index) => Scales is { } s && index < s.Count ? s[index] : MapFoeScale.None;
}

public static class Chapter1
{
    /// <summary>遊ぶ 1 章の規則（編成画面の札「1 章」・絵の口が始める版）。</summary>
    /// 第474期: `--chapter1-rule=<版のキー>` で起動したときだけ、その版を遊ぶ 1 章にする（<see cref="ReadPlayRuleArg"/>・既定は変えない）。
    public static ChapterRule Default => _playRule ?? ChapterRule.V474;   // 第474期から 1-1 は道の長さが違う盤・第461期から持ち越しのある 1 章・第462期から敵の倍率・第463期から☆の重ね・第464期から☆は強化で・第465期から勇者の回復なし・第467期から軸への投資・第468期から盤と金の札・第469期から加入の表・第470期から 1 章の手直し・第471期から始めの 6 枚はガン入り
    private static ChapterRule? _playRule;

    /// <summary>
    /// 起動の引数 `--chapter1-rule=V474` を読む（第474期・まだ既定にしていない版を画面で遊ぶ口）。版が無ければ何もしない。
    /// 引数が無ければ 1 行も変わらない。読んだ版のキーを返す（無ければ null）。
    /// </summary>
    public static string? ReadPlayRuleArg(IEnumerable<string> args)
    {
        string? key = args.FirstOrDefault(a => a.StartsWith("--chapter1-rule=", StringComparison.Ordinal))?["--chapter1-rule=".Length..];
        if (key is null || ChapterRule.ByKey(key) is not { } rule) return null;
        _playRule = rule;
        return key;
    }

    /// <summary>いまの 1 章の規則（<see cref="Begin"/> が決める）。</summary>
    public static ChapterRule Rule { get; private set; } = Default;

    /// <summary>その規則の 1 章のステージ（遊ぶ版）。</summary>
    /// 第461期: 持ち越しのある 1 章の 1-1 は <see cref="MapStage.Stage11Ch"/>（手札 6・隊の枠 2・金 8）。
    /// 第462期: 倍率のある 1 章は、各ステージの敵に倍率を掛けた写し（<see cref="MapStage.Scaled"/>・同じ倍率なら同じインスタンス）。
    public static IReadOnlyList<MapStage> StagesFor(ChapterRule rule)
        => new[] { rule.Carry ? rule.First ?? MapStage.Stage11Ch : MapStage.Stage11, MapStage.Stage12, MapStage.Stage13 }   // 第470期: 版が替えた 1-1
            .Select((s, i) => s.Scaled(rule.ScaleOf(i)).Starred(rule.Stars ?? StarRule.Off)   // 第463期: ☆の規則を載せる（☆なしならそのまま）
                .Dealt(i == 0 ? rule.Deal ?? DealRule.Off : DealRule.Off)                      // 第464期: 1-1 の配りの規則（52 枚から・軸の保証）
                .Invested(i >= (rule.Invest?.FromStage ?? 0) ? rule.Invest ?? InvestRule.Off : InvestRule.Off)   // 第467期: 軸への投資（第470期から 1-1 は札なし）
                .Rostered(rule.Roster ?? RosterRule.Off)).ToArray();                            // 第469期: 加入の表（3 ステージとも）

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
        public string Result => Won ? $"制圧（作戦ターン {Turn}）" : Quit ? "やめた" : Played ? "途中" : Skipped ? "—（写しから始めた）" : "—";
        /// <summary>このステージの始めに選んだ投資の軸（第467期・投資の無い 1 章 ／ まだ選んでいなければ null）。</summary>
        public string? Invested { get; set; }
        /// <summary>写しから始めた 1 章で、写しより前のステージ（第466期・遊んでいない）。</summary>
        public bool Skipped { get; set; }
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
        From = null;
        Records.Clear();
        foreach (MapStage s in Stages) Records.Add(new Record { Stage = s });
        PlayLog.BeginChapter(Seed);   // 第466期: 遊びの写しの置き場（書かない起動では何もしない）
        StartStage();
    }

    /// <summary>写しから始めた 1 章なら、その写し（第466期）。</summary>
    public static PlayLog.Snap? From { get; private set; }

    /// <summary>
    /// 写しから 1 章を始める（第466期 §2）。写しのステージ・1 章の seed・配りの回数から始め、写しの持ち越しを載せる
    /// （1 章の 1-1 の自動の写しは配りから——`Snap.Entry` が偽）。以降は今の 1 章と同じ（次のステージへ持ち越す・やり直しはそのステージの始め）。
    /// <paramref name="snapRule"/> が真なら写しに記録された版で、偽なら<b>今の版</b>（<see cref="Default"/>・既定——調整した後の盤を前の持ち越しで遊ぶため）。
    /// 写しの☆は、その版に☆が無ければ捨て、写しに☆が無ければ☆3 とする（`PlayLog.ToCarry`）。
    /// 第469期: <paramref name="now"/> は「今の版」（省けば <see cref="Default"/>・門は版を固定して渡す）。
    /// </summary>
    public static void BeginFrom(PlayLog.Snap snap, bool snapRule = false, ChapterRule? now = null)
    {
        Rule = snapRule && ChapterRule.ByKey(snap.Rule) is { } r ? r : now ?? Default;
        Seed = snap.ChapterSeed;
        Active = true;
        Over = false;
        Index = Math.Clamp(snap.StageIndex, 0, Stages.Count - 1);
        Attempt = Math.Max(0, snap.Attempt);
        From = snap;
        Records.Clear();
        foreach (MapStage s in Stages) Records.Add(new Record { Stage = s });
        for (int i = 0; i < Index; i++) Records[i].Skipped = true;
        if (snap.Entry && snap.Pack is { } p) Records[Index].Entry = PlayLog.ToCarry(p, Stages[Index].Stars);
        PlayLog.BeginChapter(Seed, snap.Stage);
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
        // 第467期: このステージの投資（持ち越しにこのステージの投資が入っていれば——写しの `_first` ／ `_end` から始めたとき——選んだことにする）。
        r.Invested = Map11Session.State!.InvestLog.Where(x => x.Stage == Stages[Index].Id).Select(x => x.Axis).LastOrDefault();
        _openAxes = OpenAxesOf(Map11Session.State!);   // 第469期 §1-3: 入った時点で呼べる駒のいる軸（やり直しは同じ入りなので同じ）
        PlayLog.OnStart(Map11Session.State!, Rule.Carry ? r.Entry : null);   // 第466期: 入った時点の写し（書くだけ）
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
        PlayLog.OnEnd(st, log, r);   // 第466期: 終わりの写し ＋ 戦闘の記録（書くだけ）
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

    // ---------------- 軸への投資（第467期 §1・判定は `Map11State` ／ `InvestRule`） ----------------

    /// <summary>
    /// いまのステージの投資の札（10 軸から重複なしの 3 枚）。<b>1 章の seed とステージだけで決まる</b>（配りの回数を入れない——やり直しでも同じ 3 枚）。投資の無い 1 章は空。
    /// 第468期: 盤・金の札は、前のステージまでに選んだ札を出さない（このステージで選んだ札は数えない——選んだ後も同じ 3 枚）。
    /// </summary>
    /// 第469期 §1-3: 加入の表のある 1 章では、軸の札は<b>このステージに入った時点で呼べる駒（まだ持っていないもの）が 1 体以上いる軸</b>だけ（<see cref="OpenAxes"/>）。
    public static IReadOnlyList<string> InvestCards
        => Active && CurrentStage.Invest.On ? CurrentStage.Invest.CardsFor(StageSeed(Seed, Index, 0), HeldBefore(), OpenAxes) : Array.Empty<string>();

    private static IReadOnlyCollection<string>? _openAxes;

    /// <summary>
    /// 軸の札に出してよい軸（第469期・加入の表の無い 1 章は null ＝ 10 軸すべて）。ステージを始めたとき（持ち越しを載せた後・呼ぶ前）に決める——
    /// 呼んだ後に札を読み直しても同じ 3 枚。
    /// </summary>
    public static IReadOnlyCollection<string>? OpenAxes => Active ? _openAxes : null;

    /// <summary>その盤でいま呼べる駒（まだ持っていないもの）がいる軸（<see cref="UnitTags.Axes"/> の並び・加入の表の無い盤は null）。</summary>
    public static IReadOnlyCollection<string>? OpenAxesOf(Map11State st)
        => st.Stage.Roster.On ? UnitTags.Axes.Where(a => st.RosterLeft.Any(d => MapDraw.AxesOf(d).Contains(a))).ToList() : null;

    /// <summary>前のステージまでに選んだ盤・金の札（第468期・いまのステージで選んだ札は入れない）。</summary>
    /// 第470期 §1-2: 軸も 1 度だけの版（<see cref="InvestRule.Once"/>）は、前のステージまでに選んだ軸も入れる。
    private static IReadOnlyCollection<string> HeldBefore()
        => Map11Session.State is { } st ? HeldOf(CurrentStage, st.InvestLog) : new HashSet<string>();

    /// <summary>ステージ <paramref name="stage"/> の札から外す札（前のステージまでの投資の記録から・門と器具の通しも同じ式で札を引く）。</summary>
    public static IReadOnlyCollection<string> HeldOf(MapStage stage, IEnumerable<(string Stage, string Axis)> log)
        => log.Where(x => x.Stage != stage.Id && (stage.Invest.Once || Doctrine.Find(x.Axis) is not null)).Select(x => x.Axis).ToHashSet();

    /// <summary>このステージの投資をまだ選んでいない（画面が札の窓を出す・選ぶまで閉じない）。</summary>
    public static bool InvestPending => Active && !Over && Map11Session.State is { } st && ReferenceEquals(st.Stage, CurrentStage) && st.InvestPending;

    /// <summary>札 <paramref name="card"/> 番目（0 から）の軸に投資する。選べたら true。</summary>
    public static bool PickInvest(int card)
    {
        var cards = InvestCards;
        if (!InvestPending || card < 0 || card >= cards.Count) return false;
        if (!Map11Session.State!.Invest(cards[card])) return false;
        Records[Index].Invested = cards[card];
        return true;
    }

    /// <summary>器具の選び方で投資する（手持ちにいちばん多い軸・同じなら札の左から・絵の口と器具の通しが使う）。選ぶものが無ければ何もしない。</summary>
    public static void AutoInvest()
    {
        if (InvestPending) PickInvest(InvestRule.BotPick(Map11Session.State!, InvestCards));
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
                      + (Rule.Carry ? $" ・ 終わり {r.Grown}" : "")    // 第461期 §1-4: 育ったことが見える列（持ち越しの無い 1 章は第460期のまま）
                      + (Rule.Invest is { On: true } ? $" ・ 投資 {r.Invested ?? "—"}" : ""));   // 第467期（投資の無い 1 章は第466期のまま）
        lines.Add($"合計 ・ 制圧 {Records.Count(r => r.Won)} ／ {Records.Count} ・ やり直し {Records.Sum(r => r.Retries)} 回"
                  + $" ・ 金 稼いだ {Records.Sum(r => r.Earned)} ／ 使った {Records.Sum(r => r.Spent)} ・ 働いた札 {Top(Records.SelectMany(r => r.Relics))}");
        return lines;
    }

    /// <summary>ステージの頭の札（見出し ＋ 2〜3 行・<b>仮の文</b>——本文は後で Web 側が書く）。</summary>
    /// 第462期 §1-3: 敵に倍率のかかったステージは最後に「敵の強さ ×1.3」の 1 行（1-1 ＝ ×1.0 は出さない）。
    public static (string Title, string Body) Intro(MapStage s)
    {
        var (title, body) = Rule.Story && Tales470.TryGetValue(s.Id, out Tale? t) ? ($"1 章 ・ {s.Id} {t.Heading}", t.Body) : IntroText(s);   // 第470期 §1-4
        return s.FoeScale.Active ? (title, body + "\n" + s.FoeScale.IntroLine) : (title, body);
    }

    /// <summary>ステージの頭の札の文 1 枚（第470期 §1-4・主人公の内心・<b>仮</b>）。<c>Drifters</c> は「このステージから呼べる駒」の顔を別の段に分ける駒（見出しは <c>DriftHeading</c>）。</summary>
    public sealed record Tale(string Heading, string Body, string? DriftHeading = null, IReadOnlyList<string>? Drifters = null);

    /// <summary>
    /// 第470期の頭の札の文（指示書 design/PHASE470_CH1_POLISH_SPEC.md §1-4・<b>仮——後で直す・値だけ替えればよい</b>）。物語の文を持つ版（<see cref="ChapterRule.Story"/>）だけが読む。
    /// 1-2 は人間の追放者（ヒサ・ガルド・ザン）の顔を「流れ着いた者」の段に分ける。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Tale> Tales470 = new Dictionary<string, Tale>
    {
        ["1-1"] = new("捨て駒の初陣",
                      "軍が寄越したのは、使えない奴らと使えない砦。上等だ。\n砦の墓地には先客がいた。墓守のエルフだ——「ここの墓は、私が守ってる。あなたたちが入るなら、それも守る」"),
        ["1-2"] = new("砦を取れ",
                      "砦に灯りがともると、妙な連中が流れ着くようになった。王国に居場所をなくした人間だ。魔王軍に人間？　——俺も人間だ。使える奴は使う。",
                      "流れ着いた者", new[] { "囃し立てのヒサ", "廃棄聖騎士ガルド", "仇討ちのザン" }),
        ["1-3"] = new("狩る者",
                      "本隊は撤退した。俺たちは捨て駒だった——慣れたもんだ。拾った連中まで、捨てる気は無い。"),
    };

    /// <summary>
    /// 「このステージから呼べる駒」の顔の段（第470期 §1-4・見出し ＋ 駒）。物語の文を持つ版で <see cref="Tale.Drifters"/> があれば、その駒を見出しつきの 2 段目に分ける。
    /// それ以外は 1 段（見出しなし・第469期のまま）。加入の表の無い盤は空。
    /// </summary>
    public static IReadOnlyList<(string? Heading, IReadOnlyList<UnitDef> Units)> JoinGroups(MapStage s)
    {
        IReadOnlyList<UnitDef> joins = s.Roster.JoinsAt(s.Id);
        if (joins.Count == 0) return Array.Empty<(string?, IReadOnlyList<UnitDef>)>();
        if (!(Active && Rule.Story && Tales470.TryGetValue(s.Id, out Tale? t) && t.Drifters is { Count: > 0 } drift))
            return new[] { ((string?)null, joins) };
        var rest = joins.Where(d => !drift.Contains(d.Name)).ToArray();
        var came = joins.Where(d => drift.Contains(d.Name)).ToArray();
        var groups = new List<(string?, IReadOnlyList<UnitDef>)>();
        if (rest.Length > 0) groups.Add((null, rest));
        if (came.Length > 0) groups.Add((t.DriftHeading, came));
        return groups;
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
