namespace TG.Payroll.Web.Models;

public sealed class CompanyItem
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyNameBang { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string AddressBang { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string CompanyLogoPath { get; set; } = string.Empty;
    public byte[]? CompanyLogo { get; set; }

    public string? LogoSource
    {
        get
        {
            if (CompanyLogo is not null && CompanyLogo.Length > 0)
            {
                var mime = CompanyLogo.Length > 3 && CompanyLogo[0] == 0x89 && CompanyLogo[1] == 0x50 && CompanyLogo[2] == 0x4E && CompanyLogo[3] == 0x47
                    ? "image/png"
                    : (CompanyLogo.Length > 2 && CompanyLogo[0] == 0xFF && CompanyLogo[1] == 0xD8 && CompanyLogo[2] == 0xFF ? "image/jpeg" : "image/png");
                return $"data:{mime};base64,{Convert.ToBase64String(CompanyLogo)}";
            }
            if (!string.IsNullOrWhiteSpace(CompanyLogoPath))
            {
                if (CompanyLogoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    CompanyLogoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    CompanyLogoPath.StartsWith("/", StringComparison.OrdinalIgnoreCase) ||
                    CompanyLogoPath.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                {
                    return CompanyLogoPath;
                }
                return $"/api/company/logo/{CompanyId}";
            }
            return null;
        }
    }
}
