using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// =====================================================================================
// 素材の受け口の検査（第482期・指示書 design/PHASE482_ART_TOOL_SPEC.md §6・§7）。**採用の絵は置かない——口だけ。**
//
//   Godot_console.exe --path DemoApp res://Map11Main.tscn --resolution 1920x1080 -- --map3d --map-chapter=7 "--map11-capture=<置き場（絶対パス）>/a.png,m3d-art-hooks"
//
// 受け口は 3 つ。どれも「仮の 1 枚を置くと出る・外すと元に戻る」を確かめる（仮の絵は検査が作って、終わりに必ず消す）。
//   敵の顔      assets/portraits/enemy/<駒の id>_face.png        顔が無い駒（champion）は 字 → 絵 → 字 ／ 顔がある駒（levy）は 今の顔 → 絵 → 今の顔
//   人物の絵    design/art/story/<話者の名前>.png                 名札だけ → 絵 → 名札だけ
//   立ち絵の差分 assets/portraits/battle/<id>_<差分>_right.png     待機の絵 → 差分の絵 → 待機の絵（台本は「ガン:検査｜左｜文」）
// 合否は `ART_HOOKS_COMPLETE ok=` の行。
// =====================================================================================
public partial class Map11Main
{
    private async Task ArtHookScenes(string basePath)
    {
        var fails = new List<string>();
        string stem = basePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? basePath[..^4] : basePath;
        int shot = 0;
        async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
        async Task Shot(string name)
        {
            await Frames(3);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            string path = $"{stem}_{++shot:00}_{name}.png";
            Error e = GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ART_HOOK_SHOT {name} error={e} path={path}");
        }
        void Req(bool ok, string what) { GD.Print($"ART_HOOK_CHECK {(ok ? "○" : "×")} {what}"); if (!ok) fails.Add(what); }

        // ---- 仮の絵を置く・消す（消し忘れない: 置いたものは全部ここに覚える） ----
        var made = new List<string>();
        var madeDirs = new List<string>();
        void Put(string path, Image image)
        {
            string dir = System.IO.Path.GetDirectoryName(path)!;
            if (!System.IO.Directory.Exists(dir)) { System.IO.Directory.CreateDirectory(dir); madeDirs.Add(dir); }
            if (System.IO.File.Exists(path)) throw new InvalidOperationException($"検査の仮の絵と同じ名前の絵がもうある（上書きしない）: {path}");
            Error e = image.SavePng(path);
            if (e != Error.Ok) throw new InvalidOperationException($"仮の絵を置けない: {path} {e}");
            made.Add(path);
        }
        void Sweep()
        {
            foreach (string path in made)
                foreach (string f in new[] { path, path + ".import" })
                    if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
            made.Clear();
            foreach (string dir in madeDirs)
                if (System.IO.Directory.Exists(dir) && !System.IO.Directory.EnumerateFileSystemEntries(dir).Any()) System.IO.Directory.Delete(dir);
            madeDirs.Clear();
        }

        try
        {
            await Frames(20);
            // =========================== 敵の顔 ===========================
            const string none = "champion", has = "levy";
            Color mark = new(0.10f, 0.62f, 1f);
            bool Marked(Texture2D? t)
            {
                if (t is null) return false;
                using Image i = t.GetImage();
                Color c = i.GetPixel(i.GetWidth() / 2, i.GetHeight() / 2);
                return Math.Abs(c.R - mark.R) < 0.06f && Math.Abs(c.G - mark.G) < 0.06f && Math.Abs(c.B - mark.B) < 0.06f;
            }
            async Task Redraw()
            {
                UnitFaceIcon.Forget(none);
                UnitFaceIcon.Forget(has);
                foreach (Map3DPiece piece in _pieces3d.Values) piece.Stale();
                Refresh();
                await Frames(8);
            }
            string faceDir = ProjectSettings.GlobalizePath(UnitFaceIcon.EnemyFaceDir);
            var levies = _mapPicks.Where(p => p.Foe is not null && Representative(p.Foe!)?.Id == has).ToList();
            Req(levies.Count > 0, $"この盤に、頭の駒が {has} の敵部隊がいる（{levies.Count} 部隊）");
            if (levies.Count > 0 && _cam3d is not null) _cam3d.FocusOn(PosOf(levies[0].Foe!.Pos), snap: true);
            bool BadgesMarked() => levies.Count > 0 && levies.All(p => Marked(((MapUnitBadge)p.Button).Face.FaceTexture));
            bool BadgesPlain() => levies.All(p => ((MapUnitBadge)p.Button).Face.FaceTexture is { } t && !Marked(t));

            await Redraw();
            Req(UnitFaceIcon.TextureFor(none) is null, $"置く前: 顔の無い駒（{none}）は字");
            Req(UnitFaceIcon.TextureFor(has) is { } before && !Marked(before) && BadgesPlain(), $"置く前: 顔のある駒（{has}）は今の顔（盤の札も）");
            await Shot("敵の顔_置く前");

            using (Image face = Image.CreateEmpty(256, 256, false, Image.Format.Rgba8))
            {
                face.Fill(mark);
                Put(System.IO.Path.Combine(faceDir, none + "_face.png"), face);
                Put(System.IO.Path.Combine(faceDir, has + "_face.png"), face);
            }
            await Redraw();
            Req(Marked(UnitFaceIcon.TextureFor(none)), $"置いた: {UnitFaceIcon.EnemyFaceDir}/{none}_face.png が、字の代わりに出る");
            Req(Marked(UnitFaceIcon.TextureFor(has)), $"置いた: {has}_face.png が、今の顔より先に使われる");
            Req(BadgesMarked(), $"置いた: 盤の敵部隊の札の顔が替わる（{levies.Count} 部隊）");
            var icon = new UnitFaceIcon();
            icon.SetUnit(Representative(levies.FirstOrDefault().Foe!), enemy: true, wave: "敵");
            Req(Marked(icon.FaceTexture), "置いた: 顔の部品（遭遇の台・結果が使うもの）も同じ絵を引く");
            icon.Free();
            await Shot("敵の顔_置いた");

            Sweep();
            await Redraw();
            Req(UnitFaceIcon.TextureFor(none) is null, $"外した: {none} は字に戻る");
            Req(UnitFaceIcon.TextureFor(has) is { } after && !Marked(after) && BadgesPlain(), $"外した: {has} は今の顔に戻る（盤の札も）");
            Req(!System.IO.Directory.Exists(faceDir) || !System.IO.Directory.EnumerateFiles(faceDir, "*_face.png").Any(), "外した: 置き場に仮の絵が残っていない");
            await Shot("敵の顔_外した");

            // =========================== 会話: 立ち絵の差分・人物の絵 ===========================
            const string variant = "検査", person = "検査の人";
            var errors = new List<string>();
            IReadOnlyList<StoryScript.Scene> parsed = StoryScript.Parse(
                $"# 場面 9｜受け口の検査｜検査\nガン:{variant}｜左｜差分の行（絵があれば差分・無ければ待機の絵）。\nガン｜左｜待機の行。\n{person}｜名札のみ｜人物の行（絵があれば出る・無ければ名札だけ）。", errors);
            Req(errors.Count == 0 && parsed.Count == 1 && parsed[0].Lines.Count == 3 && parsed[0].Lines[0].Variant == variant, $"見本の台本が読める（差分「{(parsed.Count == 1 ? parsed[0].Lines[0].Variant : "")}」）");
            StoryScript.Scene sample = parsed[0];
            UnitDef gan = StoryScript.UnitOf("ガン")!;
            string variantPath = ProjectSettings.GlobalizePath("res://" + StoryScript.VariantFile(gan.Id, variant));
            string personPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "design", "art", "story", person + ".png"));

            async Task<(string Variant, string Idle, bool PersonShown, string PersonFrom)> Round(string name)
            {
                StoryScene.ForgetArt();
                bool done = false;
                if (!PlayStory(9, () => done = true, force: true, with: StoryScript.Context.Plain(parsed), custom: sample)) { Req(false, $"{name}: 見本の場面を出せなかった"); return ("", "", false, ""); }
                StoryScene sc = _story!;
                await Frames(4);
                sc.ShowForCheck(0);
                await Frames(3);
                string a = sc.PortraitSource(false);
                await Shot($"会話_{name}_差分の行");
                sc.ShowForCheck(1);
                await Frames(3);
                string b = sc.PortraitSource(false);
                sc.ShowForCheck(2);
                await Frames(3);
                bool shown = sc.PortraitShown(false);
                string from = sc.PortraitSource(false);
                await Shot($"会話_{name}_人物の行");
                sc.SkipAll();
                for (int i = 0; i < 120 && !done; i++) await Frames(1);
                return (a, b, shown, from);
            }

            var r0 = await Round("置く前");
            Req(r0.Variant == "待機" && r0.Idle == "待機", $"置く前: 差分の絵が無ければ待機の絵（差分の行 ＝ {r0.Variant}・ふつうの行 ＝ {r0.Idle}）");
            Req(!r0.PersonShown, "置く前: 絵の無い人物は名札だけ");

            // 仮の絵: 今のガンの立ち絵を左右に返して青くしたもの（差分と人物で同じ絵を使う・見て分かればよい）。
            using (Image fake = Image.LoadFromFile(ProjectSettings.GlobalizePath($"res://assets/portraits/battle/{gan.Id}_idle_right.png")))
            {
                fake.Convert(Image.Format.Rgba8);
                fake.Resize(fake.GetWidth() / 2, fake.GetHeight() / 2, Image.Interpolation.Bilinear);
                for (int y = 0; y < fake.GetHeight(); y++)
                    for (int x = 0; x < fake.GetWidth(); x++)
                    {
                        Color c = fake.GetPixel(x, y);
                        fake.SetPixel(x, y, new Color(c.B * 0.6f, c.G * 0.8f, Math.Min(1, c.R + 0.25f), c.A));
                    }
                Put(variantPath, fake);
                fake.FlipX();
                Put(personPath, fake);
            }
            var r1 = await Round("置いた");
            Req(r1.Variant == "差分 " + StoryScript.VariantFile(gan.Id, variant), $"置いた: 「ガン:{variant}」の行は差分の絵（{r1.Variant}）");
            Req(r1.Idle == "待機", $"置いた: 差分を書いていない行は待機の絵のまま（{r1.Idle}）");
            Req(r1.PersonShown && r1.PersonFrom == $"人物 {person}.png", $"置いた: design/art/story/{person}.png が話者の名前で引かれる（{r1.PersonFrom}）");

            Sweep();
            var r2 = await Round("外した");
            Req(r2.Variant == "待機" && r2.Idle == "待機" && !r2.PersonShown, $"外した: 待機の絵・名札だけに戻る（差分の行 ＝ {r2.Variant}・人物の絵 {(r2.PersonShown ? "あり" : "なし")}）");
            Req(!System.IO.File.Exists(variantPath) && !System.IO.File.Exists(personPath), "外した: 仮の絵が残っていない");
            StoryScene.ForgetArt();
        }
        catch (Exception e) { GD.PrintErr($"ART_HOOKS_FAILED {e}"); fails.Add("検査が落ちた: " + e.Message); }
        finally { Sweep(); UnitFaceIcon.Forget("champion"); UnitFaceIcon.Forget("levy"); }
        foreach (string f in fails) GD.Print("ART_HOOK_FAIL " + f);
        GD.Print($"ART_HOOKS_COMPLETE stage={St.Stage.Id} shots={shot} ok={fails.Count == 0} failed={fails.Count}");
        GetTree().Quit(fails.Count == 0 ? 0 : 1);
    }
}
