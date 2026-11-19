using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// =====================================================================================
// ストーリーパートを 1 章の通しに入れる（第481期・指示書 design/PHASE481_STORY_PART_SPEC.md §3 ＋ 追記 2・3）。**画面は判定を 1 つも持たない。**
//
//   場面 0 辺境の砦     1 章を始めた直後・1-1 の編成の前          背景 1-1
//   場面 1 流れ着く者   1-1 の結果を閉じた後・1-2 の編成の前      背景 1-1（味方の拠点へ寄せる）
//   場面 2 落とした砦   1-2 の結果を閉じた後・1-3 の編成の前      背景 1-2（取った砦へ寄せる）
//   場面 3 捨て駒       1-3 の結果を閉じた後・1 章の結果の前      背景 1-3（味方の拠点）→ 暗転後は野営地の門
//
// 会話の画面（`StoryScene`）は作戦マップの上に重ねる層で、背景はいま映っている盤（暗く・ぼかす）。会話のあいだは遊ぶ UI の層を隠し、視点をゆっくり横に流す。
// 台本の条件（`?生存:` ／ `?倒れた:` ／ 結果 ／ `?生還率`）は、終わった盤を読んで作った様子（`StoryContext`）で選ぶ——読むだけ。
// 単発のステージには出さない。門・器具・遊ぶ口は既定で飛ばす（`StoryScript.Enabled`・`--story` で出す・`--no-story` で必ず飛ばす）。
// 1 度見た場面はやり直しでは出さない（`StoryScript.WasSeen`・1 章を始め直すとまた出る）。
// =====================================================================================
public partial class Map11Main
{
    private StoryScene? _story;
    private CanvasLayer? _storyLayer;
    private readonly List<CanvasLayer> _storyHidden = new();
    private Vector3 _storyFocus;
    private float _storyClock, _storyZoom = 1f;
    private bool _storyDucked;
    private MapMusic.Track _storyTrackBefore;

    /// <summary>会話の画面が出ているか（時計・セリフ・演出はこのあいだ待つ）。</summary>
    internal bool StoryShown => _story is { Playing: true };

    private static bool StoryOn => Chapter1.Active && DisplayServer.GetName() != "headless" && StoryScript.Enabled(OS.GetCmdlineUserArgs());

    /// <summary>台本を読む（場面を出すたびに読み直す——ポンがテキストを直して、起動し直さずに次の場面から試せる）。読めなければ空。</summary>
    internal static IReadOnlyList<StoryScript.Scene> LoadStory(bool log = true)
    {
        string path = "res://" + StoryScript.File;
        if (!Godot.FileAccess.FileExists(path)) { GD.PrintErr($"STORY_SCRIPT_MISSING {path}"); return Array.Empty<StoryScript.Scene>(); }
        var errors = new List<string>();
        IReadOnlyList<StoryScript.Scene> scenes = StoryScript.Parse(Godot.FileAccess.GetFileAsString(path), errors);
        if (!log) return scenes;
        foreach (string e in errors) GD.PrintErr("STORY_SCRIPT_ERROR " + e);
        IReadOnlyList<string> unknown = StoryScript.Unknown(scenes);
        GD.Print($"STORY_SCRIPT {path} 場面 {scenes.Count} 行 {scenes.Sum(s => s.Lines.Count)} 読めない行 {errors.Count} 引けない話者（立ち絵なしで出す）: {(unknown.Count == 0 ? "なし" : string.Join("・", unknown))}");
        return scenes;
    }

    /// <summary>1-3 の結果（場面 3 の条件を選ぶ）。狩る者の盤でなければ「生還」。</summary>
    private static string StoryOutcome() => HunterBoard && St.HunterDriven ? (St.HunterNarrow ? "辛くも退けた" : "勇者は退いた") : "生還";

    /// <summary>
    /// 台本の条件を見るための、いまの盤（終わったステージ）の様子（追記 2・読むだけ）。
    /// 生きている ＝ 手持ち（隊と控え）で生きている駒 ／ 倒れた ＝ 倒れたままの駒 ／ 生還率 ＝ 出撃した駒（隊で生きている ＋ 倒れた）のうち生きて終えた割合。
    /// </summary>
    private static StoryScript.Context StoryContext()
    {
        string Base(UnitDef d) => St.Stage.Stars.On ? St.Stage.Stars.BaseOf(d).Id : d.Id;
        var inSquads = St.Squads.Where(q => q.Units is not null).SelectMany(q => q.Units!).ToList();
        var alive = inSquads.Where(u => u.IsAlive).Select(u => Base(u.Def)).Concat(St.Bench.Select(u => Base(u.Def))).ToHashSet();
        var fallen = St.FallenUnits.Select(u => Base(u.Def)).Concat(inSquads.Where(u => !u.IsAlive).Select(u => Base(u.Def))).Where(id => !alive.Contains(id)).ToHashSet();
        int live = inSquads.Count(u => u.IsAlive), sent = live + fallen.Count;
        int back = HunterBoard && St.Rout ? St.HunterSurvivors : live;
        return new StoryScript.Context(StoryOutcome(), sent == 0 ? 100 : 100.0 * back / sent, alive, fallen);
    }

    /// <summary>
    /// 場面 <paramref name="no"/> を出し、終わったら <paramref name="then"/> を呼ぶ。出さない（会話を飛ばす起動・もう見た・台本に無い）なら false——呼ぶ側がそのまま先へ進む。
    /// <paramref name="with"/> は条件を見る様子（検査が渡す・無ければ盤から作る・場面 0 は「何も起きていない」）。
    /// </summary>
    private bool PlayStory(int no, Action then, bool force = false, StoryScript.Context? with = null, StoryScript.Scene? custom = null)
    {
        if (!force && (!StoryOn || StoryScript.WasSeen(no))) return false;
        IReadOnlyList<StoryScript.Scene> all = LoadStory(log: with is null);
        if ((custom ?? all.FirstOrDefault(s => s.No == no)) is not { } scene) return false;   // custom ＝ 検査が渡す見本の場面（第482期・台本に無い場面）
        StoryScript.Context ctx = with ?? (no == 0 ? StoryScript.Context.Plain(all) : StoryContext());
        IReadOnlyList<StoryScript.Line> lines = scene.For(ctx);
        if (with is null)
        {
            GD.Print($"STORY_CONTEXT 場面 {no} 結果 {ctx.Outcome} 生還率 {ctx.Rate:F0}% 倒れた駒 {ctx.Fallen.Count}（{string.Join("・", ctx.Fallen)}）出す行 {lines.Count}／{scene.Lines.Count}");
        }
        if (_story is null)
        {
            _storyLayer = new CanvasLayer { Layer = 95, Name = "StoryLayer" };
            AddChild(_storyLayer);
            _story = new StoryScene();
            _storyLayer.AddChild(_story);
        }
        // 遊ぶ UI の層を隠す（3D だけ——背景は盤だけにする）。2D は今の画面ごと暗く・ぼかす。
        _storyHidden.Clear();
        if (Map3DMode.Active)
            foreach (CanvasLayer layer in GetChildren().OfType<CanvasLayer>())
                if (!ReferenceEquals(layer, _storyLayer) && layer.Visible) { layer.Visible = false; _storyHidden.Add(layer); }
        // 視点: 場面 1・3 は味方の拠点 ／ 場面 2 は取った砦 ／ 場面 0 は盤の真ん中。少し寄せて、ゆっくり横に流す。
        _storyClock = 0;
        if (_cam3d is not null)
        {
            _storyZoom = _cam3d.Zoom;
            Vector3 home = PosOf(St.Graph.Home), far = St.Portal.On ? PosOf(St.Graph.Portal) : home;
            _storyFocus = no switch { 0 => home.Lerp(far, 0.45f), 2 => far, _ => home };
            _cam3d.SetZoom(0.78f);
            _cam3d.FocusOn(_storyFocus + new Vector3(-2.5f, 0, 0), snap: true);
        }
        // 曲: `story.ogg` があれば替える。無ければ盤の曲を下げる。
        _storyTrackBefore = MapMusic.Current;
        _storyDucked = !MapMusic.Has(MapMusic.Track.Story);
        if (_storyDucked) MapMusic.Duck(true); else MapMusic.Play(this, MapMusic.Track.Story);
        Action? done = null;
        done = () =>
        {
            _story!.Finished -= done;
            _story.AtBlackout = null;
            StoryScript.MarkSeen(no);
            foreach (CanvasLayer layer in _storyHidden) if (GodotObject.IsInstanceValid(layer)) layer.Visible = true;
            _storyHidden.Clear();
            if (_cam3d is not null) { _cam3d.SetZoom(_storyZoom); if (no == 0) _cam3d.ResetView(); }
            if (_storyDucked) MapMusic.Duck(false); else MapMusic.Play(this, _storyTrackBefore);
            GD.Print($"STORY_DONE 場面 {no} {_story.Elapsed:F1} 秒（ふつうに読んで {StoryScene.ReadSeconds(lines):F0} 秒の見積もり）");
            then();
        };
        _story.Finished += done;
        // 場面 3 の暗転の後は、野営地の門（勇者が出てきた所）。
        _story.AtBlackout = no == 3 && _cam3d is not null ? () => { _storyFocus = _map3d?.CampGate ?? PortalPos; _storyClock = 0; _cam3d.FocusOn(_storyFocus + new Vector3(-2.5f, 0, 0), snap: true); } : null;
        _story.Play(scene, lines);
        return true;
    }

    /// <summary>毎フレーム: 会話のあいだ、背景の視点をゆっくり横に流す。</summary>
    private void ProcessStory(double delta)
    {
        if (!StoryShown || _cam3d is null) return;
        _storyClock += (float)delta;
        _cam3d.FocusOn(_storyFocus + new Vector3(-2.5f + Math.Min(9f, _storyClock * 0.22f), 0, 0), snap: true);
    }

    /// <summary>1 章を始めた直後（1-1 の編成の前）の場面 0。出したら true（頭の札は場面の後）。</summary>
    private bool StoryBeforeStage(Action then)
        => Chapter1.Active && Chapter1.Index == 0 && Chapter1.IntroPending && Chapter1.From is null && !St.Finished && St.Turn == 0 && Map11Session.BattleLog.Count == 0
           && !Map11Session.SnapChoicePending && PlayStory(0, then);

    /// <summary>勝ったステージの結果を閉じた後の場面（1-1 → 場面 1 ／ 1-2 → 場面 2 ／ 1-3 → 場面 3）。出したら true。</summary>
    private bool StoryAfterStage(Action then)
    {
        if (!Chapter1.Active || !St.Won) return false;
        _resultOverlay.Visible = false;
        if (PlayStory(Chapter1.Index + 1, then)) return true;
        _resultOverlay.Visible = true;
        return false;
    }

    // =================================================================================
    // 場面の写しと検査（`--map11-capture=<置き場>/s.png,story-all`）
    //
    //   Godot_console.exe --path DemoApp res://Map11Main.tscn --resolution 1920x1080 -- --map3d --map-chapter=7 "--map11-capture=<置き場>/s.png,story-all"
    //   （1-2 ／ 1-3 の盤は `--map-chapter-play=7 --map-chapter-stop=1|2` を足す。2560×1080 は `--resolution 2560x1080`）
    //
    // いまの盤に当たる場面（1-1 ＝ 場面 0・1 ／ 1-2 ＝ 場面 2 ／ 1-3 ＝ 場面 3）を、条件の組み合わせ（`StoryScript.Cases`）ごとに全部の行まで出す。
    // 写し: 題の帯・左が話す・右が話す・主人公の声・内心・名札のみ・勇者・暗転・締めの帯 ＋ 条件ごとに、条件つきの最初の行を 1 枚。
    // 検査: 本文は 2 行まで・帯からはみ出さない・左寄せ ／ 顔が帯・名札・スキップに重ならない・画面に入っている ／ 名札は話している側 ／
    //       話している側だけが明るい ／ 声は名札なし・斜体でない・相手は明るいまま ／ 内心は斜体・立ち絵は明るさ 20% ／ 名札のみは立ち絵なし ／ 締めの帯に送りの印。
    // 合否は `STORY_SCENES_COMPLETE ok=` の行。
    // =================================================================================
    private async Task StoryScenes(string basePath)
    {
        var fails = new List<string>();
        string stem = basePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? basePath[..^4] : basePath;
        Vector2 view = GetViewport().GetVisibleRect().Size;
        string tag = $"{view.X:F0}x{view.Y:F0}";
        int shot = 0;
        async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Shot(string name)
        {
            await Frames(3);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            string path = $"{stem}_{tag}_{++shot:00}_{name}.png";
            Error e = GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"STORY_SHOT {name} error={e} path={path}");
        }
        void Req(bool ok, string what) { GD.Print($"STORY_SCENE_CHECK {(ok ? "○" : "×")} {what}"); if (!ok) fails.Add(what); }

        IReadOnlyList<StoryScript.Scene> all = LoadStory();
        int[] mine = St.Stage.Id switch { "1-1" => new[] { 0, 1 }, "1-2" => new[] { 2 }, "1-3" => new[] { 3 }, _ => Array.Empty<int>() };
        Req(mine.Length > 0 && mine.All(n => all.Any(s => s.No == n)), $"{St.Stage.Id} の盤に当たる場面が台本にある（{string.Join("・", mine)}）");
        await Frames(20);
        float faceLow = 0;
        string faceLowWho = "";
        foreach (int no in mine)
        {
            if (all.FirstOrDefault(s => s.No == no) is not { } scene) continue;
            bool first = true;
            // 追記 4・5: 条件の組み合わせの後に、話者が 1 人ずつ倒れた場合（行は落ちない・候補を書いた行と言い換えは次の駒が言う）。
            var fallNames = StoryScript.FallCases(scene, all).Select(c => c.Name).ToHashSet();
            foreach (var (caseName, ctx) in StoryScript.Cases(scene, all).Concat(StoryScript.FallCases(scene, all)))
            {
                int failsBefore = fails.Count;
                bool fall = fallNames.Contains(caseName);
                // 話者が倒れた場合に、話者が替わる行（候補を書いた行・言い換えの組）。
                var swaps = new List<StoryScript.Line>();
                if (fall)
                {
                    IReadOnlyList<StoryScript.Line> up = scene.For(ctx with { Fallen = Array.Empty<string>() }), down = scene.For(ctx);
                    var was = up.ToDictionary(l => l.Row, l => l.Speaker);
                    swaps = down.Where(l => l.Kind == StoryScript.Kind.Say && !l.Hero && (!was.TryGetValue(l.Row, out string? w) || w != l.Speaker)).ToList();
                    GD.Print($"STORY_FALL 場面 {no}（{caseName}）出る行 {down.Count}（倒れていないとき {up.Count}）話者が替わる行 {swaps.Count}: {string.Join(" ／ ", swaps.Select(l => $"{l.Row} 行目 {l.Speaker}｜{l.Text}"))}");
                    if (down.Count != up.Count) fails.Add($"場面 {no}（{caseName}）: 行が落ちる（{up.Count} → {down.Count}）");
                }
                bool done = false;
                if (!PlayStory(no, () => done = true, force: true, with: ctx)) { Req(false, $"場面 {no}: 出せなかった"); break; }
                StoryScene sc = _story!;
                await Frames(4);
                Rect2 screen = new(Vector2.Zero, sc.Size), band = sc.BandRect;
                if (first)
                {
                    sc.ShowTitleForCheck();
                    Req(sc.TitleBandText == scene.Title, $"場面 {no}: 題の帯「{sc.TitleBandText}」");
                    Req(_storyHidden.Count > 0 || !Map3DMode.Active, $"場面 {no}: 遊ぶ UI の層を隠した（{_storyHidden.Count} 枚）");
                    Req(Mathf.IsEqualApprox(band.Size.Y, view.Y * StoryScene.BandShare) && Mathf.IsEqualApprox(band.End.Y, view.Y), $"場面 {no}: 文字の帯は画面の下 1／5（{band.Position.Y:F0}〜{band.End.Y:F0}）");
                    Req(sc.SkipShown, $"場面 {no}: 右上に「スキップ」");
                    await Shot($"場面{no}_題の帯");
                }
                var lines = sc.Lines;
                var picks = new Dictionary<int, string>();
                void Pick(string name, Func<StoryScript.Line, bool> match)
                {
                    int at = Enumerable.Range(0, lines.Count).FirstOrDefault(i => match(lines[i]), -1);
                    if (at >= 0 && !picks.ContainsKey(at)) picks[at] = name;
                }
                bool Talk(StoryScript.Line l) => l.Kind == StoryScript.Kind.Say && !l.Hero;
                if (first)
                {
                    Pick("左が話す", l => Talk(l) && l.Side == StoryScript.Side.Left && !l.Shadow);
                    Pick("右が話す", l => Talk(l) && l.Side == StoryScript.Side.Right && !l.Shadow && l.Speaker != StoryScript.HeroSpeaker);
                    Pick("主人公の声", l => l.Kind == StoryScript.Kind.Say && l.Side == StoryScript.Side.Voice);
                    Pick("内心", l => l.Kind == StoryScript.Kind.Say && l.Side == StoryScript.Side.Inner);
                    Pick("名札のみ", l => Talk(l) && l.Shadow);
                    Pick("勇者", l => l.Speaker == StoryScript.HeroSpeaker);
                    Pick("暗転", l => l.Kind == StoryScript.Kind.Blackout);
                    Pick("締めの帯", l => l.Kind == StoryScript.Kind.Band);
                }
                // 条件ごとに、条件つきの最初の行を 1 枚（ほかの写しと同じ行なら、名前に条件を添える）。
                if (fall)
                {
                    // 話者が倒れた場合: 話者が替わった行を 1 枚（無ければ、倒れた駒がそのまま話す行を 1 枚）。
                    string? downId = ctx.Fallen.FirstOrDefault();
                    int at = swaps.Count > 0 ? Enumerable.Range(0, lines.Count).FirstOrDefault(i => lines[i].Row == swaps[0].Row, -1)
                        : Enumerable.Range(0, lines.Count).FirstOrDefault(i => lines[i].Kind == StoryScript.Kind.Say && !lines[i].Hero && StoryScript.UnitOf(lines[i].Speaker)?.Id == downId, -1);
                    if (at >= 0) picks[at] = $"話者_{caseName}{(swaps.Count > 0 ? "_替わる" : "_そのまま話す")}";
                }
                else if (caseName.Length > 0 && Enumerable.Range(0, lines.Count).FirstOrDefault(i => lines[i].Cond is not null, -1) is var condAt and >= 0)
                    picks[condAt] =(picks.TryGetValue(condAt, out string? had) ? had + "_" : "") + $"条件_{caseName.Replace(" ", "")}";
                int widest = 0;
                bool? lastRight = null;   // 直前に話した駒の側（声のあいだも明るいまま）
                for (int i = 0; i < lines.Count; i++)
                {
                    StoryScript.Line line = lines[i];
                    sc.ShowForCheck(i);
                    await Frames(2);
                    string where = $"場面 {no}{(caseName.Length == 0 ? "" : $"（{caseName}）")} {i + 1} 行目（台本の {line.Row} 行目）";
                    if (line.Kind == StoryScript.Kind.Blackout) { lastRight = null; if (!sc.Black) fails.Add($"{where}: 暗転していない"); }
                    else if (line.Kind == StoryScript.Kind.Band)
                    {
                        if (sc.EndBandText != line.Text) fails.Add($"{where}: 締めの帯が「{sc.EndBandText}」");
                        if (!sc.AdvanceMark) fails.Add($"{where}: 締めの帯に送りの印が無い");
                    }
                    else
                    {
                        widest = Math.Max(widest, sc.BodyLines);
                        if (sc.BodyLines > 2) fails.Add($"{where}: 本文が {sc.BodyLines} 行");
                        if (sc.BodyBottom > band.End.Y - 6 || sc.BodyRect.Position.Y < band.Position.Y || sc.BodyRect.Position.X < 0 || sc.BodyRect.End.X > view.X)
                            fails.Add($"{where}: 本文が帯からはみ出す（下の端 {sc.BodyBottom:F0}・帯 {band.Position.Y:F0}〜{band.End.Y:F0}）");
                        if (!sc.BodyLeft) fails.Add($"{where}: 本文が左寄せでない");
                        if (!sc.AdvanceMark) fails.Add($"{where}: 送りの印が無い");
                        if (line.Hero)
                        {
                            bool inner = line.Side == StoryScript.Side.Inner;
                            if (!sc.HeroShown || sc.NameText.Length > 0) fails.Add($"{where}: 主人公の行なのに名札が出ている（「{sc.NameText}」）");
                            if (sc.Italic != inner) fails.Add($"{where}: 斜体の出し分けが違う（内心 {inner}・斜体 {sc.Italic}）");
                            if (sc.NavyBand != inner) fails.Add($"{where}: 帯の色が違う（紺は内心だけ・声は黒い帯と金の線）");
                            foreach (bool side in new[] { false, true })
                            {
                                if (!sc.PortraitShown(side)) continue;
                                if (inner && sc.PortraitBright(side) > StoryScene.InnerPortrait + 0.01f) fails.Add($"{where}: 内心なのに{(side ? "右" : "左")}の立ち絵が明るい（{sc.PortraitBright(side):F2}）");
                                if (!inner && lastRight == side && sc.PortraitWho(side).Length > 0 && !sc.PortraitLit(side)) fails.Add($"{where}: 声なのに、直前に話した{(side ? "右" : "左")}の立ち絵が暗い（{sc.PortraitBright(side):F2}）");
                            }
                            if (inner) lastRight = null;
                        }
                        else
                        {
                            bool right = line.Side == StoryScript.Side.Right;
                            bool art = StoryScript.ArtSpeakers.ContainsKey(line.Speaker) || sc.PortraitSource(right).StartsWith("人物", StringComparison.Ordinal);   // 第482期: 絵を置いた人物は「名札のみ」でも立ち絵が出る
                            if (sc.NavyBand) fails.Add($"{where}: 駒のセリフなのに帯が紺");
                            if (sc.NameText != line.Speaker) fails.Add($"{where}: 名札が「{sc.NameText}」（台本は {line.Speaker}）");
                            if (sc.NameOnRight != right || (sc.NameRect.GetCenter().X > view.X / 2) != right) fails.Add($"{where}: 名札が話している側（{(right ? "右" : "左")}）に寄っていない");
                            if (!screen.Encloses(sc.NameRect)) fails.Add($"{where}: 名札が画面の外");
                            if (Overlap(sc.NameRect, sc.BodyRect with { Size = new Vector2(sc.BodyRect.Size.X, sc.BodyBottom - sc.BodyRect.Position.Y) }) > 1) fails.Add($"{where}: 名札が本文に重なる");
                            if (line.Shadow && !art && sc.PortraitShown(right)) fails.Add($"{where}: 名札のみなのに立ち絵が出ている");
                            if (!line.Shadow && !art && (!sc.PortraitShown(right) || !sc.PortraitLit(right))) fails.Add($"{where}: 話している側（{(right ? "右" : "左")}）の立ち絵が出ていない ／ 明るくない");
                            if (sc.PortraitShown(!right) && sc.PortraitLit(!right)) fails.Add($"{where}: 話していない側も明るい");
                            lastRight = sc.PortraitShown(right) ? right : null;
                        }
                        foreach (bool side in new[] { false, true })
                        {
                            Rect2 face = sc.FaceRect(side);
                            if (face.Size == Vector2.Zero) continue;
                            string who = $"{(side ? "右" : "左")}の顔（{sc.PortraitWho(side)}）";
                            if (Overlap(face, band) > 1) fails.Add($"{where}: {who}が帯に重なる");
                            if (sc.NameRect.Size != Vector2.Zero && Overlap(face, sc.NameRect) > 1) fails.Add($"{where}: {who}が名札に重なる");
                            if (Overlap(face, sc.SkipRect) > 1) fails.Add($"{where}: {who}が「スキップ」に重なる");
                            if (!screen.Encloses(face)) fails.Add($"{where}: {who}が画面からはみ出す");
                            float low = face.GetCenter().Y / view.Y;
                            if (low > faceLow) { faceLow = low; faceLowWho = sc.PortraitWho(side); }
                        }
                    }
                    if (picks.TryGetValue(i, out string? name)) await Shot($"場面{no}_{name}");
                }
                Req(fails.Count == failsBefore, $"場面 {no}{(caseName.Length == 0 ? "" : $"（{caseName}）")}「{scene.Title}」{lines.Count} 行: 本文は {widest} 行まで・帯に収まる・顔が帯と名札と「スキップ」に重ならない・名札は話している側・主人公の声と内心の出し分け"
                    + $"（文字 {Mathf.RoundToInt(StoryScene.BodyFont * view.Y / 1080f)}px・ふつうに読んで {StoryScene.ReadSeconds(lines):F0} 秒）");
                sc.SkipAll();
                for (int i = 0; i < 120 && !done; i++) await Frames(1);
                Req(done && !StoryShown && _storyHidden.Count == 0, $"場面 {no}: 終わると遊ぶ UI が戻る");
                first = false;
            }
        }
        GD.Print($"STORY_SCENE_FACE いちばん低い顔の真ん中は画面の高さの {faceLow * 100:F0}%（{faceLowWho}・目安は上 1／3 ＝ 33%）");
        foreach (string f in fails) GD.Print("STORY_SCENE_FAIL " + f);
        GD.Print($"STORY_SCENES_COMPLETE stage={St.Stage.Id} view={tag} shots={shot} ok={fails.Count == 0} failed={fails.Count}");
        GetTree().Quit(fails.Count == 0 ? 0 : 1);
    }
}
