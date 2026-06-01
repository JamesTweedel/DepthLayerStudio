using System.Drawing;

namespace DepthLayerStudio;

public sealed class LayerSpec
{
    public LayerSpec(
        string key,
        string label,
        Color color,
        double threshold,
        double depthMm,
        int passes,
        int power,
        int speed)
    {
        Key = key;
        Label = label;
        Color = color;
        Threshold = threshold;
        DepthMm = depthMm;
        Passes = passes;
        Power = power;
        Speed = speed;
    }

    public string Key { get; }
    public string Label { get; set; }
    public Color Color { get; set; }
    public double Threshold { get; set; }
    public double DepthMm { get; set; }
    public int Passes { get; set; }
    public int Power { get; set; }
    public int Speed { get; set; }
    public bool Enabled { get; set; } = true;
    public double Coverage { get; set; }
    public Bitmap? Mask { get; set; }
    public string? SavedPath { get; set; }

    public LayerSpec Clone()
    {
        return new LayerSpec(Key, Label, Color, Threshold, DepthMm, Passes, Power, Speed)
        {
            Enabled = Enabled,
            Coverage = Coverage,
            SavedPath = SavedPath,
            Mask = Mask is null ? null : new Bitmap(Mask),
        };
    }
}
