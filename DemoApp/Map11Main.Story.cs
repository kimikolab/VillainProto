using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// =====================================================================================
// ストーリーパートを 1 章の通しに入れる（第481期・指示書 design/PHASE481_STORY_PART_SPEC.md §3）。**画面は判定を 1 つも持たない。**
//
//   場面 0 辺境の砦     1 章を始めた直後・1-1 の編成の前          背景 1-1
//   場面 1 流れ着く者   1-1 の結果を閉じた後・1-2 の編成の前      背景 1-1（味方の拠点へ寄せる）
//   場面 2 落とした砦   1-2 の結果を閉じた後・1-3 の編成の前      背景 1-2（取った砦へ寄せる）
//   場面 3 捨て駒       1-3 の結果を閉じた後・1 章の結果の前      背景 1-3（味方の拠点）→ 暗転後は野営地の門
//
// 会話の画面（`StoryScene`）は作戦マップの上に重ねる層で、背景はいま映っている盤（暗く・ぼかす）。会話のあいだは遊ぶ UI の層を隠し、視点をゆっくり横に流す。
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
        GD.Print($"STORY_SCRIPT {path} 場面 {scenes.Count} 行 {scenes.Sum(s => s.Lines.Count)} 読めない行 {errors.Count} 引けない話者（影で出す）: {(unknown.Count == 0 ? "なし" : string.Join("・", unknown))}");
        return scenes;
    }

    /// <summary>1-3 の結果（場面 3 の `?` の行を選ぶ）。狩る者の盤でなければ「生還」。</summary>
    private static string StoryOutcome() => HunterBoard && St.HunterDriven ? (St.HunterNarrow ? "辛くも退けた" : "勇者は退いた") : "生還";

    /// <summary>
    /// 場面 <paramref name="no"/> を出し、終わったら <paramref name="then"/> を呼ぶ。出さない（会話を飛ばす起動・もう見た・台本に無い）なら false——呼ぶ側がそのまま先へ進む。
    /// </summary>
    private bool PlayStory(int no, Action then, bool force = false)
    {
        if (!force && (!StoryOn || StoryScript.WasSeen(no))) return false;
        if (LoadStory().FirstOrDefault(s => s.No == no) is not { } scene) return false;
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
            GD.Print($"STORY_DONE 場面 {no} {_story.Elapsed:F1} 秒（ふつうに読んで {StoryScene.ReadSeconds(scene, no == 3 ? StoryOutcome() : null):F0} 秒の見積もり）");
            then();
        };
        _story.Finished += done;
        // 場面 3 の暗転の後は、野営地の門（勇者が出てきた所）。
        _story.AtBlackout = no == 3 && _cam3d is not null ? () => { _storyFocus = _map3d?.CampGate ?? PortalPos; _storyClock = 0; _cam3d.FocusOn(_storyFocus + new Vector3(-2.5f, 0, 0), snap: true); } : null;
        _story.Play(scene, no == 3 ? StoryOutcome() : null);
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
    // いまの盤に当たる場面（1-1 ＝ 場面 0・1 ／ 1-2 ＝ 場面 2 ／ 1-3 ＝ 場面 3）を、題の帯・左が話す・右が話す・内心・影（あれば）＋ 暗転 ＋ 締めの帯 の順に撮る。
    // 全部の行について「本文が 2 行まで・帯からはみ出さない」「立ち絵が帯と名札に重ならない」を確かめる。合否は `STORY_SCENES_COMPLETE ok=` の行。
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
        foreach (int no in mine)
        {
            if (all.FirstOrDefault(s => s.No == no) is not { } scene) continue;
            string?[] outcomes = no == 3 ? StoryScript.Outcomes : new string?[] { null };
            bool first = true;
            foreach (string? outcome in outcomes)
            {
                bool done = false;
                if (!PlayStory(no, () => done = true, force: true)) { Req(false, $"場面 {no}: 出せなかった"); break; }
                StoryScene sc = _story!;
                await Frames(4);
                Rect2 screen = new(Vector2.Zero, sc.Size), band = sc.BandRect;
                if (first)
                {
                    sc.ShowTitleForCheck();
                    Req(sc.TitleBandText == scene.Title, $"場面 {no}: 題の帯「{sc.TitleBandText}」");
                    Req(_storyHidden.Count > 0 || !Map3DMode.Active, $"場面 {no}: 遊ぶ UI の層を隠した（{_storyHidden.Count} 枚）");
                    Req(Mathf.IsEqualApprox(band.Size.Y, view.Y / 4) && Mathf.IsEqualApprox(band.End.Y, view.Y), $"場面 {no}: 文字の帯は画面の下 1／4（{band.Position.Y:F0}〜{band.End.Y:F0}）");
                    Req(sc.SkipShown, $"場面 {no}: 右上に「スキップ」");
                    await Shot($"場面{no}_題の帯");
                }
                var lines = sc.Lines;
                // 撮る行: 左が話す・右が話す・内心・影（あれば）・暗転・締めの帯の、それぞれ最初の 1 行。
                var picks = new Dictionary<int, string>();
                void Pick(string name, Func<StoryScript.Line, bool> match)
                {
                    int at = Enumerable.Range(0, lines.Count).FirstOrDefault(i => match(lines[i]), -1);
                    if (at >= 0 && !picks.ContainsKey(at)) picks[at] = name;
                }
                if (first)
                {
                    Pick("左が話す", l => l.Kind == StoryScript.Kind.Say && l.Side == StoryScript.Side.Left && !l.Shadow);
                    Pick("右が話す", l => l.Kind == StoryScript.Kind.Say && l.Side == StoryScript.Side.Right && !l.Shadow);
                    Pick("内心", l => l.Kind == StoryScript.Kind.Say && l.Side == StoryScript.Side.Inner);
                    Pick("影", l => l.Kind == StoryScript.Kind.Say && l.Shadow);
                    Pick("勇者", l => l.Speaker == StoryScript.HeroSpeaker);
                    Pick("暗転", l => l.Kind == StoryScript.Kind.Blackout);
                    Pick("締めの帯", l => l.Kind == StoryScript.Kind.Band);
                }
                else Pick($"結果_{outcome}", l => l.Only == outcome);
                int widest = 0;
                for (int i = 0; i < lines.Count; i++)
                {
                    StoryScript.Line line = lines[i];
                    sc.ShowForCheck(i);
                    await Frames(2);
                    string where = $"場面 {no}{(outcome is null ? "" : $"（{outcome}）")} {i + 1} 行目（台本の {line.Row} 行目）";
                    if (line.Kind == StoryScript.Kind.Say)
                    {
                        widest = Math.Max(widest, sc.BodyLines);
                        if (sc.BodyLines > 2) fails.Add($"{where}: 本文が {sc.BodyLines} 行");
                        if (sc.BodyBottom > band.End.Y - 8 || sc.BodyRect.Position.Y < band.Position.Y || sc.BodyRect.Position.X < 0 || sc.BodyRect.End.X > view.X)
                            fails.Add($"{where}: 本文が帯からはみ出す（下の端 {sc.BodyBottom:F0}・帯 {band.Position.Y:F0}〜{band.End.Y:F0}）");
                        bool inner = line.Side == StoryScript.Side.Inner;
                        if (inner != sc.InnerShown || (inner && (sc.NameText.Length > 0 || sc.PortraitLit(false) || sc.PortraitLit(true)))) fails.Add($"{where}: 内心の見せ方が違う（名札「{sc.NameText}」・明るい立ち絵 左 {sc.PortraitLit(false)} 右 {sc.PortraitLit(true)}）");
                        if (!inner)
                        {
                            bool right = line.Side == StoryScript.Side.Right;
                            if (sc.NameText != line.Speaker) fails.Add($"{where}: 名札が「{sc.NameText}」（台本は {line.Speaker}）");
                            if (!sc.PortraitLit(right) || sc.PortraitLit(!right)) fails.Add($"{where}: 話している側（{(right ? "右" : "左")}）だけが明るくなっていない");
                            if (sc.PortraitShadow(right) != (line.Shadow || (line.Speaker != StoryScript.HeroSpeaker && StoryScript.UnitOf(line.Speaker) is null))) fails.Add($"{where}: 影 ／ 立ち絵の出し分けが台本と違う（影 {sc.PortraitShadow(right)}）");
                            if (line.Speaker == StoryScript.HeroSpeaker && sc.PortraitShadow(right)) fails.Add($"{where}: 勇者の立ち絵が引けていない（影で出た）");
                            if (!band.Encloses(sc.NameRect)) fails.Add($"{where}: 名札が帯の外");
                            if (Overlap(sc.NameRect, sc.BodyRect with { Size = new Vector2(sc.BodyRect.Size.X, sc.BodyBottom - sc.BodyRect.Position.Y) }) > 1) fails.Add($"{where}: 名札が本文に重なる");
                        }
                        foreach (bool side in new[] { false, true })
                        {
                            Rect2 p = sc.PortraitRect(side);
                            if (p.Size == Vector2.Zero) continue;
                            if (Overlap(p, band) > 1) fails.Add($"{where}: {(side ? "右" : "左")}の立ち絵が帯に重なる");
                            if (sc.NameRect.Size != Vector2.Zero && Overlap(p, sc.NameRect) > 1) fails.Add($"{where}: {(side ? "右" : "左")}の立ち絵が名札に重なる");
                            if (!screen.Encloses(p)) fails.Add($"{where}: {(side ? "右" : "左")}の立ち絵が画面からはみ出す");
                        }
                    }
                    else if (line.Kind == StoryScript.Kind.Blackout && !sc.Black) fails.Add($"{where}: 暗転していない");
                    else if (line.Kind == StoryScript.Kind.Band && sc.EndBandText != line.Text) fails.Add($"{where}: 締めの帯が「{sc.EndBandText}」");
                    if (picks.TryGetValue(i, out string? name)) await Shot($"場面{no}_{name}");
                }
                Req(!fails.Any(f => f.StartsWith($"場面 {no}", StringComparison.Ordinal)), $"場面 {no}{(outcome is null ? "" : $"（{outcome}）")}「{scene.Title}」{lines.Count} 行: 本文は {widest} 行まで・帯からはみ出さない・立ち絵が帯と名札に重ならない・話す側だけが明るい"
                    + $"（文字 {Mathf.RoundToInt(StoryScene.BodyFont * view.Y / 1080f)}px・ふつうに読んで {StoryScene.ReadSeconds(scene, outcome):F0} 秒）");
                sc.SkipAll();
                for (int i = 0; i < 120 && !done; i++) await Frames(1);
                Req(done && !StoryShown && _storyHidden.Count == 0, $"場面 {no}: 終わると遊ぶ UI が戻る");
                first = false;
            }
        }
        foreach (string f in fails) GD.Print("STORY_SCENE_FAIL " + f);
        GD.Print($"STORY_SCENES_COMPLETE stage={St.Stage.Id} view={tag} shots={shot} ok={fails.Count == 0} failed={fails.Count}");
        GetTree().Quit(fails.Count == 0 ? 0 : 1);
    }
}
