namespace TG.Payroll.Web.Models;

public sealed record CurrentUser(int Id, string Name, int CompanyId, string CompanyName, bool IsAdmin);
