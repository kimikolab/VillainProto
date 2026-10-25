using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

// ハギ用の制作書き出し。連結した全身セルだけを等比拡縮し、接地と刃の追跡点を揃える。
public partial class HagiCelExport : Node
{
    private sealed record Cell(int Label, int X, int Y, int Width, int Height);
    public override void _Ready()
    {
        try { Export(); GD.Print("HAGI_CEL_EXPORT_OK whole-body rigid-weapon no-warp"); GetTree().Quit(); }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static void Export()
    {
        string repo = Path.GetFullPath(ProjectSettings.GlobalizePath("res://.."));
        string source = Path.Combine(repo, "design/art/hagi/attack-acrobatics-v1");
        using var spec = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, "cel-animation.json")));
        const int width = 1920, height = 960, groundX = 880, groundY = 940;
        var poses = new Dictionary<string, (Image Image, object Track)>();
        Directory.CreateDirectory(Path.Combine(source, "poses"));
        foreach (var group in spec.RootElement.GetProperty("poses").EnumerateArray().GroupBy(p => p.GetProperty("sheet").GetString()!))
        {
            using var sheet = Image.LoadFromFile(Path.Combine(source, group.Key)); sheet.Convert(Image.Format.Rgba8);
            int w = sheet.GetWidth(), h = sheet.GetHeight(); byte[] pixels = sheet.GetData();
            int[] labels = new int[w * h], queue = new int[w * h]; var found = new List<Cell>(); int label = 0;
            for (int at = 0; at < labels.Length; at++)
            {
                if (labels[at] != 0 || pixels[at * 4 + 3] <= 128) continue;
                label++; int first = 0, last = 1, left = w, top = h, right = 0, bottom = 0;
                queue[0] = at; labels[at] = label;
                while (first < last)
                {
                    int p = queue[first++], x = p % w, y = p / w;
                    left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                    void Visit(int q) { if (labels[q] == 0 && pixels[q * 4 + 3] > 128) { labels[q] = label; queue[last++] = q; } }
                    if (x > 0) Visit(p - 1); if (x + 1 < w) Visit(p + 1); if (y > 0) Visit(p - w); if (y + 1 < h) Visit(p + w);
                }
                if (last > 3000) found.Add(new(label, left, top, right - left + 1, bottom - top + 1));
            }
            var cells = found.OrderBy(c => Math.Clamp((c.Y + c.Height / 2) * 2 / h, 0, 1)).ThenBy(c => c.X + c.Width / 2).ToArray();
            if (cells.Length != 4) throw new InvalidOperationException($"{group.Key}: 全身セルが4枚ではありません ({cells.Length})");
            foreach (var pose in group)
            {
                string key = pose.GetProperty("key").GetString()!;
                Cell cell = cells[pose.GetProperty("cell").GetInt32()];
                Vector2 Point(JsonElement p) => new(p[0].GetSingle(), p[1].GetSingle());
                Vector2 headTop = Point(pose.GetProperty("head")[0]), headChin = Point(pose.GetProperty("head")[1]);
                float scale = 124 / headTop.DistanceTo(headChin);
                Vector2 ground = Point(pose.GetProperty("ground"));
                int x0 = Math.Max(0, cell.X - 3), y0 = Math.Max(0, cell.Y - 3);
                int cw = Math.Min(w - x0, cell.Width + 6), ch = Math.Min(h - y0, cell.Height + 6);
                var rgba = new byte[cw * ch * 4];
                for (int cy = 0; cy < ch; cy++) for (int cx = 0; cx < cw; cx++)
                {
                    int sx = x0 + cx, sy = y0 + cy, p = sy * w + sx; bool belongs = labels[p] == cell.Label;
                    for (int dy = -2; dy <= 2 && !belongs; dy++) for (int dx = -2; dx <= 2 && !belongs; dx++)
                        if (sx + dx >= 0 && sx + dx < w && sy + dy >= 0 && sy + dy < h && labels[(sy + dy) * w + sx + dx] == cell.Label) belongs = true;
                    if (belongs) Array.Copy(pixels, p * 4, rgba, (cy * cw + cx) * 4, 4);
                }
                using var crop = Image.CreateFromData(cw, ch, false, Image.Format.Rgba8, rgba);
                crop.Resize((int)MathF.Round(cw * scale), (int)MathF.Round(ch * scale), Image.Interpolation.Lanczos);
                int ox = (int)MathF.Round(groundX - (ground.X - x0) * scale), oy = (int)MathF.Round(groundY - (ground.Y - y0) * scale);
                if (ox < 0 || oy < 0 || ox + crop.GetWidth() > width || oy + crop.GetHeight() > height)
                    throw new InvalidOperationException($"{key}: 原画が共通キャンバスから切れます");
                var canvas = Image.CreateEmpty(width, height, false, Image.Format.Rgba8); canvas.Fill(Colors.Transparent);
                canvas.BlitRect(crop, new Rect2I(0, 0, crop.GetWidth(), crop.GetHeight()), new Vector2I(ox, oy));
                canvas.SavePng(Path.Combine(source, "poses", key + ".png"));
                float[] Uv(Vector2 p) => new[] { (ox + (p.X - x0) * scale) / width, (oy + (p.Y - y0) * scale) / height };
                var contour = pose.GetProperty("contour").EnumerateArray().Select(p => Uv(Point(p))).ToArray();
                poses[key] = (canvas, new { socket = Uv(Point(pose.GetProperty("socket"))), tip = Uv(Point(pose.GetProperty("tip"))), contour });
                GD.Print($"HAGI_CEL_POSE {key} head=124 scale={scale:F3}");
            }
        }
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        foreach (var clip in spec.RootElement.GetProperty("clips").EnumerateArray())
        {
            string key = clip.GetProperty("key").GetString()!, name = "hagi_" + key;
            var keys = clip.GetProperty("keys").EnumerateArray().SelectMany(p => Enumerable.Repeat(p[0].GetString()!, p[1].GetInt32())).ToArray();
            var vertical = clip.GetProperty("vertical").EnumerateArray().Select(v => v.GetSingle()).ToArray();
            if (vertical.Length != keys.Length) throw new InvalidOperationException($"{key}: 跳躍曲線のコマ数不一致");
            int contact = clip.GetProperty("contact").GetInt32();
            string dest = Path.Combine(repo, "DemoApp/assets/portraits/battle/attack_clip", name); Directory.CreateDirectory(dest);
            if (Directory.GetFiles(dest, "*.png").Length > 0) throw new InvalidOperationException($"{name}: 既存の連番は上書きしません。別の版名を使ってください");
            var tracks = new List<object>();
            for (int i = 0; i < keys.Length; i++)
            {
                var pose = poses[keys[i]]; pose.Image.SavePng(Path.Combine(dest, $"{name}_{i:000}.png"));
                tracks.Add(new { pose = keys[i], geometry = pose.Track, trail = i >= contact - 2 && i <= contact + 3 });
            }
            File.WriteAllText(Path.Combine(dest, "clip.json"), JsonSerializer.Serialize(new {
                source = "hagi-acrobatics-v1", frames = keys.Length, size = new[] { width, height }, fps = 30,
                contact, anchor = new[] { groundX, groundY }, idleAnchor = new[] { 512, 1506 }, scale = 1.6,
                referencePortrait = "hagi", forward = new float[keys.Length], vertical,
                motion = "全身原画。水平の回転斬りと空中ひねり。部位ワープ・逆再生・クロスフェードなし"
            }, options) + "\n");
            File.WriteAllText(Path.Combine(dest, "motion.json"), JsonSerializer.Serialize(new { source = "hagi-acrobatics-v1", frames = tracks }, options) + "\n");
            GD.Print($"HAGI_CEL_CLIP {name} frames={keys.Length} contact={contact}");
        }
        foreach (var pose in poses.Values) pose.Image.Dispose();
    }
}
