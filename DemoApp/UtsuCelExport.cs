using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

// ウツの全身原画を分離・等比縮小する制作ツール。部位ワープや逆再生は行わない。
public partial class UtsuCelExport : Node
{
    private sealed record Cell(int Label, int X, int Y, int Width, int Height);
    public override void _Ready()
    {
        try { Export(); GD.Print("UTSU_CEL_EXPORT_OK whole-body dual-blade head-scale connected-prefix"); GetTree().Quit(); }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Export()
    {
        string repo = Path.GetFullPath(ProjectSettings.GlobalizePath("res://.."));
        string source = Path.Combine(repo, "design/art/utsu/attack-step-v3");
        using var spec = JsonDocument.Parse(File.ReadAllText(Path.Combine(source, "cel-animation.json")));
        var root = spec.RootElement;
        int width = root.GetProperty("canvas")[0].GetInt32(), height = root.GetProperty("canvas")[1].GetInt32();
        Vector2 Point(JsonElement point) => new(point[0].GetSingle(), point[1].GetSingle());
        Vector2 anchor = Point(root.GetProperty("anchor"));
        var poses = new Dictionary<string, (Image Image, object Geometry)>();
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        Directory.CreateDirectory(Path.Combine(source, "poses"));
        foreach (var group in root.GetProperty("poses").EnumerateArray().GroupBy(p => p.GetProperty("sheet").GetString()!))
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
                    void Visit(int next) { if (labels[next] == 0 && pixels[next * 4 + 3] > 128) { labels[next] = label; queue[last++] = next; } }
                    if (x > 0) Visit(p - 1); if (x + 1 < w) Visit(p + 1); if (y > 0) Visit(p - w); if (y + 1 < h) Visit(p + w);
                }
                if (last > 3000) found.Add(new(label, left, top, right - left + 1, bottom - top + 1));
            }
            int expected = root.GetProperty("sheets").GetProperty(group.Key).GetInt32();
            var cells = found.OrderBy(c => expected <= 2 ? 0 : Math.Clamp((c.Y + c.Height / 2) * 2 / h, 0, 1))
                .ThenBy(c => c.X + c.Width / 2).ToArray();
            if (cells.Length != expected) throw new InvalidOperationException($"{group.Key}: 全身セルが{expected}枚ではありません ({cells.Length})");
            foreach (var pose in group)
            {
                string key = pose.GetProperty("key").GetString()!; Cell cell = cells[pose.GetProperty("cell").GetInt32()];
                float scale = root.GetProperty("headPixels").GetSingle() / Point(pose.GetProperty("head")[0]).DistanceTo(Point(pose.GetProperty("head")[1]));
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
                // 透明部のRGBが縮小時の輪郭へ混ざらないよう、乗算済みアルファで再標本化する。
                crop.PremultiplyAlpha();
                crop.Resize((int)MathF.Round(cw * scale), (int)MathF.Round(ch * scale), Image.Interpolation.Lanczos);
                byte[] resized = crop.GetData();
                for (int i = 0; i < resized.Length; i += 4)
                    for (int c = 0; c < 3; c++) resized[i + c] = resized[i + 3] == 0 ? (byte)0 : (byte)Math.Clamp(resized[i + c] * 255 / resized[i + 3], 0, 255);
                using var clean = Image.CreateFromData(crop.GetWidth(), crop.GetHeight(), false, Image.Format.Rgba8, resized);
                int ox = (int)MathF.Round(anchor.X - (ground.X - x0) * scale), oy = (int)MathF.Round(anchor.Y - (ground.Y - y0) * scale);
                if (ox < 0 || oy < 0 || ox + clean.GetWidth() >= width || oy + clean.GetHeight() >= height)
                    throw new InvalidOperationException($"{key}: 全身が共通キャンバスから切れます");
                var canvas = Image.CreateEmpty(width, height, false, Image.Format.Rgba8); canvas.Fill(Colors.Transparent);
                canvas.BlitRect(clean, new Rect2I(0, 0, clean.GetWidth(), clean.GetHeight()), new Vector2I(ox, oy));
                canvas.SavePng(Path.Combine(source, "poses", key + ".png"));
                float[] Uv(JsonElement point) { Vector2 p = Point(point); return new[] { (ox + (p.X - x0) * scale) / width, (oy + (p.Y - y0) * scale) / height }; }
                var blades = pose.GetProperty("blades").EnumerateArray().Select(b => new { root = Uv(b[0]), tip = Uv(b[1]) }).ToArray();
                poses[key] = (canvas, new { blades });
                GD.Print($"UTSU_CEL_POSE {key} head={root.GetProperty("headPixels")} scale={scale:F3}");
            }
        }
        foreach (var clip in root.GetProperty("clips").EnumerateArray())
        {
            string name = clip.GetProperty("name").GetString()!;
            string dest = Path.Combine(repo, "DemoApp/assets/portraits/battle/attack_clip", name);
            if (Directory.Exists(dest) && !OS.GetCmdlineUserArgs().Contains("--utsu-reexport")) throw new InvalidOperationException($"{name}: 原画の上書き指定が必要です");
            if (Directory.Exists(dest))
            {
                string allowed = Path.GetFullPath(Path.Combine(repo, "DemoApp/assets/portraits/battle/attack_clip")) + Path.DirectorySeparatorChar;
                if (!Path.GetFullPath(dest).StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || !name.StartsWith("utsu_", StringComparison.Ordinal))
                    throw new InvalidOperationException("ウツの制作フォルダ外へは書き出しません");
                using var prior = JsonDocument.Parse(File.ReadAllText(Path.Combine(dest, "clip.json")));
                if (prior.RootElement.GetProperty("source").GetString() != root.GetProperty("sourceId").GetString()) throw new InvalidOperationException("異なる原画のクリップは上書きしません");
                foreach (string file in Directory.GetFiles(dest, "*.png")) File.Delete(file);
            }
            Directory.CreateDirectory(dest);
            var keys = clip.GetProperty("keys").EnumerateArray().SelectMany(p => Enumerable.Repeat(p[0].GetString()!, p[1].GetInt32())).ToArray();
            var vertical = clip.GetProperty("vertical").EnumerateArray().Select(v => v.GetSingle()).ToArray();
            if (keys.Length != vertical.Length) throw new InvalidOperationException(name + ": 跳躍曲線の長さ不一致");
            int contact = clip.GetProperty("contact").GetInt32();
            var tracks = new List<object>();
            for (int i = 0; i < keys.Length; i++)
            {
                var pose = poses[keys[i]]; pose.Image.SavePng(Path.Combine(dest, $"{name}_{i:000}.png"));
                float? advance = clip.TryGetProperty("advance", out var curve) ? curve[i].GetSingle() : null;
                tracks.Add(new { pose = keys[i], geometry = pose.Geometry, advance, trail = i >= contact - 2 && i <= contact + 2 });
            }
            File.WriteAllText(Path.Combine(dest, "clip.json"), JsonSerializer.Serialize(new {
                source = root.GetProperty("sourceId").GetString(), frames = keys.Length, fps = root.GetProperty("fps").GetInt32(), contact,
                anchor = new[] { anchor.X, anchor.Y }, idleAnchor = root.GetProperty("idleAnchor").EnumerateArray().Select(v => v.GetSingle()).ToArray(),
                scale = root.GetProperty("clipScale").GetSingle(), referencePortrait = "utsu",
                forward = clip.GetProperty("forward").EnumerateArray().Select(v => v.GetSingle()).ToArray(), vertical
            }, options) + "\n");
            File.WriteAllText(Path.Combine(dest, "motion.json"), JsonSerializer.Serialize(new { activeBlade = clip.GetProperty("activeBlade").GetInt32(), frames = tracks }, options) + "\n");
            GD.Print($"UTSU_CEL_CLIP {name} frames={keys.Length} contact={contact}");
        }
        foreach (var pose in poses.Values) pose.Image.Dispose();
    }
}
