namespace FERCPlugin.Core.Models
{
    public class VentUnitItem
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public int? DisplayIndex { get; set; }
        public double LengthTotal { get; set; }
        public double HeightTotal { get; set; }
        public double WidthTotal { get; set; }
        public List<VentUnitPanel> TopPanels { get; set; } = new();
        public List<VentUnitPanel> FloorPanels { get; set; } = new();
        public List<VentUnitPanel> BackPanels { get; set; } = new();
        public List<VentUnitChild> Children { get; set; } = new();
    }

    public class VentUnitPanel
    {
        public double SizeY { get; set; }
        public List<double> SizesX { get; set; } = new();
    }

    public class VentUnitChild
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public double LengthTotal { get; set; }
        public double HeightTotal { get; set; }
        public double WidthTotal { get; set; }
        public List<VentUnitPanel> ServicePanels { get; set; } = new();
        public List<VentUnitPipe> Pipes { get; set; } = new();
    }

    public class VentUnitPipe
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double D { get; set; }
    }
}
