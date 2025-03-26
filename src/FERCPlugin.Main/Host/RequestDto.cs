using FERCPlugin.Core.Models;

namespace FERCPlugin.Main.Host;
public class RequestDto {
    public Guid Id { get; set; }

    public bool IsIntakeBelow { get; set; }

    [IsEnum(typeof(LeftRightTypeEnum))]
    public string ServiceSideIntake { get; set; }

    public double FrameHeight { get; set; }

    public List<VentUnitItem> Intake { get; set; } = [];
    public List<VentUnitItem> Exhaust { get; set; } = [];

    public string Name { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
