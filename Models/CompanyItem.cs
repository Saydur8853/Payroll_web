namespace TG.Payroll.Web.Models;

public sealed class CompanyItem
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string CompanyLogoPath { get; set; } = string.Empty;
}
