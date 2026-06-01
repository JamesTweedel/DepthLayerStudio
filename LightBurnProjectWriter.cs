using System;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace DepthLayerStudio;

public static class LightBurnProjectWriter
{
    private const double MillimetersPerPixel = 0.1;
    private const double MarginMillimeters = 10;
    private const double DefaultLineInterval = 0.1;
    private const double DefaultDpi = 25.4 / DefaultLineInterval;
    private const double SecondsPerMinute = 60;

    public static void Save(SliceResult result, string projectPath)
    {
        var activeLayers = result.Layers
            .Where(layer => layer.Enabled && layer.Mask is not null)
            .ToList();

        if (activeLayers.Count == 0)
        {
            throw new InvalidOperationException("There are no enabled mask layers to save into a LightBurn project.");
        }

        var widthMm = Math.Max(1, result.Width * MillimetersPerPixel);
        var heightMm = Math.Max(1, result.Height * MillimetersPerPixel);
        var centerX = MarginMillimeters + widthMm / 2;
        var centerY = MarginMillimeters + heightMm / 2;

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
        };

        using var writer = XmlWriter.Create(projectPath, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("LightBurnProject");
        writer.WriteAttributeString("AppVersion", "2.0.00");
        writer.WriteAttributeString("FormatVersion", "1");
        writer.WriteAttributeString("MaterialHeight", "0");
        writer.WriteAttributeString("MirrorX", "False");
        writer.WriteAttributeString("MirrorY", "False");

        WriteUiPrefs(writer);

        for (var index = 0; index < activeLayers.Count; index += 1)
        {
            WriteCutSetting(writer, activeLayers[index], index);
        }

        for (var index = 0; index < activeLayers.Count; index += 1)
        {
            WriteBitmapShape(writer, activeLayers[index], index, widthMm, heightMm, centerX, centerY);
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteUiPrefs(XmlWriter writer)
    {
        writer.WriteStartElement("UIPrefs");
        WriteValue(writer, "Optimize_CutSelected", "0");
        WriteValue(writer, "Optimize_UseSelectedOrigin", "0");
        WriteValue(writer, "CutOrigin", "0");
        WriteValue(writer, "AlignH", "0");
        WriteValue(writer, "AlignV", "0");
        WriteValue(writer, "Optimize_ByLayer", "1");
        WriteValue(writer, "Optimize_ByGroup", "-1");
        WriteValue(writer, "Optimize_ByPriority", "1");
        WriteValue(writer, "Optimize_ReduceTravel", "1");
        WriteValue(writer, "Optimize_RemoveOverlaps", "0");
        writer.WriteEndElement();
    }

    private static void WriteCutSetting(XmlWriter writer, LayerSpec layer, int index)
    {
        writer.WriteStartElement("CutSetting_Img");
        writer.WriteAttributeString("type", "Image");

        WriteValue(writer, "index", index.ToString(CultureInfo.InvariantCulture));
        WriteValue(writer, "name", LayerName(index, layer));
        WriteValue(writer, "minPower", layer.Power.ToString(CultureInfo.InvariantCulture));
        WriteValue(writer, "maxPower", layer.Power.ToString(CultureInfo.InvariantCulture));
        WriteValue(writer, "minPower2", layer.Power.ToString(CultureInfo.InvariantCulture));
        WriteValue(writer, "maxPower2", layer.Power.ToString(CultureInfo.InvariantCulture));
        WriteValue(writer, "speed", Format(layer.Speed / SecondsPerMinute));
        WriteValue(writer, "kerf", "0");
        WriteValue(writer, "zOffset", "0");
        WriteValue(writer, "enableLaser1", "1");
        WriteValue(writer, "enableLaser2", "0");
        WriteValue(writer, "startDelay", "0");
        WriteValue(writer, "endDelay", "0");
        WriteValue(writer, "throughPower", "0");
        WriteValue(writer, "throughPower2", "0");
        WriteValue(writer, "enableCutThrough", "0");
        WriteValue(writer, "priority", index.ToString(CultureInfo.InvariantCulture));
        WriteValue(writer, "frequency", "20000");
        WriteValue(writer, "overrideFrequency", "0");
        WriteValue(writer, "PPI", "0");
        WriteValue(writer, "enablePPI", "0");
        WriteValue(writer, "doOutput", "1");
        WriteValue(writer, "hide", "0");
        WriteValue(writer, "runBlower", "0");
        WriteValue(writer, "blowerSpeedOverride", "0");
        WriteValue(writer, "blowerSpeedPercent", "100");
        WriteValue(writer, "overcut", "0");
        WriteValue(writer, "rampLength", "0");
        WriteValue(writer, "numPasses", layer.Passes.ToString(CultureInfo.InvariantCulture));
        WriteValue(writer, "zPerPass", "0");
        WriteValue(writer, "perforate", "0");
        WriteValue(writer, "perfLen", "1");
        WriteValue(writer, "perfSkip", "1");
        WriteValue(writer, "dotMode", "0");
        WriteValue(writer, "dotTime", "0");
        WriteValue(writer, "dotSpacing", "0");
        WriteValue(writer, "scanOpt", "individual");
        WriteValue(writer, "bidir", "1");
        WriteValue(writer, "crossHatch", "0");
        WriteValue(writer, "overscan", "1");
        WriteValue(writer, "overscanPercent", "2");
        WriteValue(writer, "floodFill", "0");
        WriteValue(writer, "interval", Format(DefaultLineInterval));
        WriteValue(writer, "angle", "0");
        WriteValue(writer, "angleIncrement", "0");
        WriteValue(writer, "passThrough", "1");
        WriteValue(writer, "ditherMode", "threshold");
        WriteValue(writer, "dpi", Format(DefaultDpi));
        WriteValue(writer, "linkDPItoInterval", "1");
        WriteValue(writer, "negative", "0");

        writer.WriteEndElement();
    }

    private static void WriteBitmapShape(
        XmlWriter writer,
        LayerSpec layer,
        int index,
        double widthMm,
        double heightMm,
        double centerX,
        double centerY)
    {
        if (layer.Mask is null)
        {
            return;
        }

        writer.WriteStartElement("Shape");
        writer.WriteAttributeString("Type", "Bitmap");
        writer.WriteAttributeString("CutIndex", index.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("W", Format(widthMm));
        writer.WriteAttributeString("H", Format(heightMm));
        writer.WriteAttributeString("Gamma", "1");
        writer.WriteAttributeString("Contrast", "0");
        writer.WriteAttributeString("Brightness", "0");
        writer.WriteAttributeString("EnhanceAmount", "0");
        writer.WriteAttributeString("EnhanceRadius", "0");
        writer.WriteAttributeString("EnhanceDenoise", "0");
        writer.WriteAttributeString("File", string.Empty);
        writer.WriteAttributeString("SourceHash", "0");
        writer.WriteAttributeString("Data", EncodeMask(layer));

        writer.WriteElementString("XForm", $"1 0 0 1 {Format(centerX)} {Format(centerY)}");
        writer.WriteEndElement();
    }

    private static string EncodeMask(LayerSpec layer)
    {
        using var stream = new MemoryStream();
        layer.Mask!.Save(stream, ImageFormat.Png);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static void WriteValue(XmlWriter writer, string name, string value)
    {
        writer.WriteStartElement(name);
        writer.WriteAttributeString("Value", value);
        writer.WriteEndElement();
    }

    private static string LayerName(int index, LayerSpec layer)
    {
        return $"C{index:00} {layer.Label}";
    }

    private static string Format(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
