namespace TG.Payroll.Web.Models;

public sealed class ControlItem
{
    public int ControlId { get; set; }
    public string ControlName { get; set; } = string.Empty;
    public int? CallingId { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public string ControlType { get; set; } = "MENU";
    public int Priority { get; set; } = 1;
}
