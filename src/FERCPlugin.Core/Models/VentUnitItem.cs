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
        public UtilizerUnitCutInfo CutInfo { get; set; } = new UtilizerUnitCutInfo();

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
        public VentUnitWindow Window { get; set; }
        public string ExhaustDirection { get; set; }
    }

    public class VentUnitPipe
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double D { get; set; }
    }

    public class VentUnitWindow
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double D { get; set; }
    }

    public class UtilizerUnitCutInfo
    {
        public bool HasLeftCut { get; set; }
        public bool HasRightCut { get; set; }
        public double CutSize { get; set; }
    }
}
