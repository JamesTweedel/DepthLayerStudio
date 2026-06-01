using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace DepthLayerStudio;

public sealed class SliceResult
{
    public SliceResult(string sourcePath, int width, int height, IReadOnlyList<LayerSpec> layers)
    {
        SourcePath = sourcePath;
        Width = width;
        Height = height;
        Layers = layers;
    }

    public string SourcePath { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<LayerSpec> Layers { get; }
    public string? ProjectPath { get; set; }
}

public static class ImageSlicer
{
    public static SliceResult Slice(string sourcePath, IEnumerable<LayerSpec> layerSpecs, int maxEdgePixels)
    {
        using var original = new Bitmap(sourcePath);
        var targetSize = GetTargetSize(original.Width, original.Height, maxEdgePixels);
        using var source = Resize(original, targetSize.Width, targetSize.Height);
        var layers = layerSpecs.Select(layer => layer.Clone()).ToList();
        var totalPixels = source.Width * source.Height;

        foreach (var layer in layers)
        {
            layer.Mask?.Dispose();
            layer.Mask = CreateMask(source, layer.Threshold, out var activePixels);
            layer.Coverage = activePixels * 100.0 / totalPixels;
            layer.SavedPath = null;
        }

        return new SliceResult(sourcePath, source.Width, source.Height, layers);
    }

    public static IReadOnlyList<string> Export(SliceResult result, string outputFolder, string notes)
    {
        Directory.CreateDirectory(outputFolder);

        var saved = new List<string>();
        var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(result.SourcePath));
        var notesPath = Path.Combine(outputFolder, $"depthlayer-{baseName}-lightburn-notes.txt");
        var projectPath = Path.Combine(outputFolder, $"depthlayer-{baseName}-lightburn-project.lbrn2");

        File.WriteAllText(notesPath, notes, Encoding.UTF8);
        saved.Add(notesPath);

        for (var index = 0; index < result.Layers.Count; index += 1)
        {
            var layer = result.Layers[index];

            if (!layer.Enabled || layer.Mask is null)
            {
                continue;
            }

            var layerName = SanitizeFileName(layer.Label);
            var filePath = Path.Combine(outputFolder, $"depthlayer-{baseName}-{index + 1:00}-{layerName}.png");
            layer.Mask.Save(filePath, ImageFormat.Png);
            layer.SavedPath = filePath;
            saved.Add(filePath);
        }

        LightBurnProjectWriter.Save(result, projectPath);
        result.ProjectPath = projectPath;
        saved.Add(projectPath);

        return saved;
    }

    public static string BuildNotes(SliceResult result, string material, string? outputFolder)
    {
        var builder = new StringBuilder();
        var activeLayers = result.Layers.Where(layer => layer.Enabled).ToList();

        builder.AppendLine("DepthLayer Studio - LightBurn handoff");
        builder.AppendLine($"Source photo: {result.SourcePath}");
        builder.AppendLine($"Material preset: {material}");
        builder.AppendLine($"Mask size: {result.Width} x {result.Height} px");
        builder.AppendLine($"Active masks: {activeLayers.Count}");

        if (!string.IsNullOrWhiteSpace(outputFolder))
        {
            builder.AppendLine($"Export folder: {outputFolder}");
        }

        if (!string.IsNullOrWhiteSpace(result.ProjectPath))
        {
            builder.AppendLine($"LightBurn project: {Path.GetFileName(result.ProjectPath)}");
        }

        builder.AppendLine();
        builder.AppendLine("Open the LightBurn project file to load the masks as separate LightBurn layers with the speed, power, and pass settings already assigned.");
        builder.AppendLine("The numbered PNG masks are exported as backup/reference files. The masks are cumulative: darker masks run after broader masks.");
        builder.AppendLine("Run a small scrap-material test before burning the full project.");
        builder.AppendLine();

        for (var index = 0; index < activeLayers.Count; index += 1)
        {
            var layer = activeLayers[index];
            var fileName = string.IsNullOrWhiteSpace(layer.SavedPath)
                ? "(export first)"
                : Path.GetFileName(layer.SavedPath);

            builder.AppendLine($"Layer {index + 1}: {layer.Label}");
            builder.AppendLine($"LightBurn layer: C{index:00} {layer.Label}");
            builder.AppendLine($"File: {fileName}");
            builder.AppendLine($"Depth cutoff: {layer.Threshold:P0}");
            builder.AppendLine($"Coverage: {layer.Coverage:0.0}%");
            builder.AppendLine($"Suggested settings: {layer.Passes} pass(es), {layer.Power}% power, {layer.Speed} mm/min, target depth {layer.DepthMm:0.0} mm");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public static Image CreatePreviewImage(string sourcePath, int maxWidth, int maxHeight)
    {
        using var original = new Bitmap(sourcePath);
        var scale = Math.Min(maxWidth / (double)original.Width, maxHeight / (double)original.Height);

        if (scale > 1)
        {
            scale = 1;
        }

        var width = Math.Max(1, (int)Math.Round(original.Width * scale));
        var height = Math.Max(1, (int)Math.Round(original.Height * scale));
        return Resize(original, width, height);
    }

    private static Bitmap CreateMask(Bitmap source, double threshold, out int activePixels)
    {
        var mask = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        activePixels = 0;

        for (var y = 0; y < source.Height; y += 1)
        {
            for (var x = 0; x < source.Width; x += 1)
            {
                var pixel = source.GetPixel(x, y);
                var depth = LuminanceToDepth(pixel);
                var active = depth >= threshold;
                mask.SetPixel(x, y, active ? Color.Black : Color.White);

                if (active)
                {
                    activePixels += 1;
                }
            }
        }

        return mask;
    }

    private static double LuminanceToDepth(Color pixel)
    {
        var luminance = (0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B) / 255.0;
        var darkness = 1.0 - luminance;
        var contrasted = Clamp01((darkness - 0.5) * 1.1 + 0.5);
        return Math.Pow(contrasted, 0.92);
    }

    private static double Clamp01(double value)
    {
        return Math.Max(0, Math.Min(1, value));
    }

    private static Size GetTargetSize(int width, int height, int maxEdgePixels)
    {
        var edge = Math.Max(1, maxEdgePixels);
        var scale = Math.Min(1.0, edge / (double)Math.Max(width, height));
        return new Size(
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static Bitmap Resize(Image image, int width, int height)
    {
        var resized = new Bitmap(width, height, PixelFormat.Format24bppRgb);

        using var graphics = Graphics.FromImage(resized);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(image, 0, 0, width, height);

        return resized;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
        cleaned = cleaned.Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(cleaned) ? "photo" : cleaned;
    }
}
