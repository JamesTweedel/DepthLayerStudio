using System.Collections.Generic;
using System.Drawing;

namespace DepthLayerStudio;

public static class MaterialPresets
{
    public static IReadOnlyList<LayerSpec> Create(string material)
    {
        var powerShift = material switch
        {
            "Opaque acrylic" => 4,
            "Coated slate" => 7,
            _ => 0,
        };

        var speedShift = material switch
        {
            "Opaque acrylic" => -220,
            "Coated slate" => -360,
            _ => 0,
        };

        var depthShift = material switch
        {
            "Opaque acrylic" => 0.3,
            "Coated slate" => 0.1,
            _ => 0,
        };

        return new List<LayerSpec>
        {
            new("light", "Light tone", Color.FromArgb(72, 140, 255), 0.24, 0.4 + depthShift, 1, 18 + powerShift, 4700 + speedShift),
            new("midtone", "Mid tone", Color.FromArgb(244, 194, 74), 0.51, 1.1 + depthShift, 1, 31 + powerShift, 3800 + speedShift),
            new("dark", "Dark detail", Color.FromArgb(244, 105, 105), 0.78, 1.8 + depthShift, 1, 44 + powerShift, 3100 + speedShift),
        };
    }
}
